using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Google.Protobuf;
using Google.Protobuf.Protocol;

namespace QaGameBenchmark;

// The benchmark can send only the fixed QA player journey and the game's heartbeat.
internal sealed class GameConnection(X509Certificate2? testRoot = null) : IAsyncDisposable
{
    internal const int Port = 17777;
    readonly TcpClient client = new(AddressFamily.InterNetwork) { NoDelay = true };
    readonly CancellationTokenSource stop = new();
    readonly SemaphoreSlim writeLock = new(1, 1);
    readonly Channel<IMessage> replies = Channel.CreateBounded<IMessage>(16);
    SslStream? tls;
    Task? receiving;
    internal string? TlsVersion { get; private set; }

    internal async Task Connect(IPAddress address, CancellationToken ct) =>
        await client.ConnectAsync(address, Port, ct);

    internal async Task Authenticate(string tlsName, CancellationToken ct)
    {
        tls = new SslStream(client.GetStream());
        var options = new SslClientAuthenticationOptions
        {
            TargetHost = tlsName,
            CertificateRevocationCheckMode = X509RevocationMode.Online
        };
        if (testRoot != null)
        {
            options.CertificateChainPolicy = new X509ChainPolicy
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                CustomTrustStore = { testRoot },
                RevocationMode = X509RevocationMode.NoCheck
            };
        }
        await tls.AuthenticateAsClientAsync(options, ct);
        TlsVersion = tls.SslProtocol.ToString();
        receiving = Receive();
    }

    async Task Receive()
    {
        var header = new byte[4];
        byte[] body = new byte[1024];
        Exception? failure = null;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                await tls!.ReadExactlyAsync(header, stop.Token);
                int length = BinaryPrimitives.ReadUInt16LittleEndian(header) - header.Length;
                if (length < 0) throw new InvalidDataException("InvalidFrame");
                var kind = (MsgId)BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2));
                if (length > body.Length) Array.Resize(ref body, length);
                await tls.ReadExactlyAsync(body.AsMemory(0, length), stop.Token);
                if (kind == MsgId.SPing)
                {
                    var ping = S_Ping.Parser.ParseFrom(body, 0, length);
                    await Send(MsgId.CPong, new C_Pong { PingId = ping.PingId }, stop.Token);
                    continue;
                }
                MessageParser? parser = kind switch
                {
                    MsgId.SConnected => S_Connected.Parser,
                    MsgId.SLogin => S_Login.Parser,
                    MsgId.SSelectServer => S_SelectServer.Parser,
                    MsgId.SRequestPlayerList => S_RequestPlayerList.Parser,
                    MsgId.SSelectPlayer => S_SelectPlayer.Parser,
                    MsgId.SEnterGame => S_EnterGame.Parser,
                    MsgId.SRequestInitGameRoomData => S_RequestInitGameRoomData.Parser,
                    MsgId.SUpdateItemDataAll => S_UpdateItemDataAll.Parser,
                    MsgId.SUpdateCurrencyDataAll => S_UpdateCurrencyDataAll.Parser,
                    _ => null // Other normal room notifications do not acknowledge this journey.
                };
                if (parser != null && !replies.Writer.TryWrite(parser.ParseFrom(body, 0, length)))
                    throw new InvalidDataException("ReplyBacklog");
            }
        }
        catch (Exception error) { failure = error; }
        finally { replies.Writer.TryComplete(failure); }
    }

    async Task Send(MsgId kind, IMessage packet, CancellationToken ct)
    {
        var frame = new byte[checked(packet.CalculateSize() + 4)];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), (ushort)kind);
        using (var stream = new MemoryStream(frame, 4, frame.Length - 4, true))
            packet.WriteTo(stream);
        try
        {
            await writeLock.WaitAsync(ct);
            try { await tls!.WriteAsync(frame, ct); }
            finally { writeLock.Release(); }
        }
        finally { if (kind == MsgId.CLogin) CryptographicOperations.ZeroMemory(frame); }
    }

    async Task<T> Expect<T>(CancellationToken ct) where T : class, IMessage
    {
        var reply = await replies.Reader.ReadAsync(ct);
        return reply as T ?? throw new ScenarioError("UnexpectedReply_" + reply.Descriptor.Name);
    }

    internal async Task Welcome(CancellationToken ct) => await Expect<S_Connected>(ct);

    internal async Task Login(FixtureUser user, CancellationToken ct)
    {
        await Send(MsgId.CLogin, new C_Login { Id = user.Id, Password = user.Password }, ct);
        var result = (await Expect<S_Login>(ct)).LoginStatus;
        if (result != LoginStatus.Success) throw new ScenarioError("Login" + result);
    }

    internal async Task SelectServer(CancellationToken ct)
    {
        await Send(MsgId.CSelectServer, new C_SelectServer { ServerId = 1, ChannelId = 1 }, ct);
        var r = await Expect<S_SelectServer>(ct);
        if (r.EnterServerResult != EnterServerResult.Success) throw new ScenarioError("Server" + r.EnterServerResult);
        if (r.ServerId != 1 || r.ChannelId != 1) throw new ScenarioError("WrongChannel");
    }

    int characterId;
    internal async Task ListCharacter(FixtureUser user, CancellationToken ct)
    {
        await Send(MsgId.CRequestPlayerList, new C_RequestPlayerList(), ct);
        var list = (await Expect<S_RequestPlayerList>(ct)).PlayerInfoList;
        if (list.Count != 1 || list[0].Name != user.Character || list[0].PlayerId <= 0)
            throw new ScenarioError("FixtureCharacterMismatch");
        characterId = list[0].PlayerId;
    }

    internal async Task SelectCharacter(CancellationToken ct)
    {
        await Send(MsgId.CSelectPlayer, new C_SelectPlayer { PlayerId = characterId, ServerId = 1, ChannelId = 1 }, ct);
        var r = await Expect<S_SelectPlayer>(ct);
        if (!r.CanSelect || r.PlayerId != characterId) throw new ScenarioError("CharacterSelectionRejected");
    }

    internal async Task Enter(FixtureUser user, CancellationToken ct)
    {
        await Send(MsgId.CEnterGame, new C_EnterGame { PlayerId = characterId, ServerId = 1, ChannelId = 1 }, ct);
        var r = await Expect<S_EnterGame>(ct);
        if (r.ObjectState?.Name != user.Character || r.MapId <= 0)
            throw new ScenarioError("CharacterEntryMismatch");
    }

    internal async Task ReadInventory(CancellationToken ct)
    {
        await Send(MsgId.CRequestInitGameRoomData, new C_RequestInitGameRoomData(), ct);
        var state = await Expect<S_RequestInitGameRoomData>(ct);
        var items = await Expect<S_UpdateItemDataAll>(ct);
        var balances = await Expect<S_UpdateCurrencyDataAll>(ct);
        if (state.InitGameRoomData?.Level != 1 || state.InitGameRoomData.Exp != 0 || items.ItemInfoList.Count != 0 ||
            balances.CurrencyData?.Gold != 1000 || balances.CurrencyData.Level != 1 || balances.CurrencyData.Exp != 0)
            throw new ScenarioError("FixtureInventoryMismatch");
    }

    internal async Task Close(CancellationToken ct)
    {
        await writeLock.WaitAsync(ct);
        try { if (tls?.IsAuthenticated == true) await tls.ShutdownAsync().WaitAsync(ct); }
        finally { writeLock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        client.Dispose();
        tls?.Dispose();
        if (receiving != null) await receiving;
        writeLock.Dispose();
        stop.Dispose();
    }
}

internal sealed class ScenarioError(string code) : Exception(code);
