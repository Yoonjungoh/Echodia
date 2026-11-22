using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Xunit;

namespace QaGameBenchmark.Tests;

public class JourneyTests
{
    [Fact]
    public async Task SameJourneyReportsAdmissionAndResidentOverlap()
    {
        await using var fixture = new LoopbackGame();
        var run = fixture.MakeRun([1, 4], 2);
        await run.Run(CancellationToken.None);
        Assert.Equal("Completed", run.StopReason);
        Assert.All(run.Warmup, p => Assert.Equal("Success", p.Outcome));
        Assert.Equal(4, run.Waves.Count);
        foreach (var wave in run.Waves)
        {
            Assert.Single(wave.Players, p => p.Outcome == "Success");
            Assert.Equal(wave.Concurrency - 1, wave.Players.Count(p => p.Outcome == "LoginNone"));
            Assert.Equal(wave.Concurrency > 1, wave.AdmissionSaturated);
            Assert.All(wave.ResidentReads, p => Assert.Equal("Success", p.Sample.Outcome));
            Assert.Contains(wave.ResidentReads, p => p.OverlapsLogin);
            Assert.All(wave.Players, p => Assert.Equal("Success", p.Stages["closeNotify"].Outcome));
            Assert.True(wave.DurationMs > 0);
        }
    }

    [Fact]
    public async Task CertificateNameMismatchStopsBeforeCredentials()
    {
        await using var fixture = new LoopbackGame();
        var run = fixture.MakeRun([1], 1, "wrong.example.invalid");
        await run.Run(CancellationToken.None);
        Assert.Empty(run.Waves);
        Assert.Equal("TlsValidationOrHandshakeFailure", run.Warmup.Single().Outcome);
        Assert.Equal(0, fixture.Logins);
    }

    [Fact]
    public async Task IncompleteInventoryTimesOutAndCannotBeCountedAsComplete()
    {
        await using var fixture = new LoopbackGame(dropCurrency: true);
        var run = fixture.MakeRun([1], 1);
        await run.Run(CancellationToken.None);
        Assert.Empty(run.Waves);
        Assert.Equal("Timeout", run.Warmup.Single().Outcome);
        Assert.Equal("initialInventory", run.Warmup.Single().FailedStage);
    }

    // Only a loopback listener, with a per-test CA trusted inside this test instance.
    sealed class LoopbackGame : IAsyncDisposable
    {
        readonly IPAddress address = IPAddress.Parse("127.0.0.2");
        readonly TcpListener listener;
        readonly FixtureUser[] users;
        readonly X509Certificate2 root;
        readonly X509Certificate2 leaf;
        readonly CancellationTokenSource stop = new();
        readonly List<Task> connections = new();
        readonly Task acceptLoop;
        readonly bool dropCurrency;
        int authenticating;
        internal int Logins;

        internal LoopbackGame(bool dropCurrency = false)
        {
            this.dropCurrency = dropCurrency;
            users = Enumerable.Range(1,17).Select(i => new FixtureUser($"EchodiaBench{i:00}", Guid.NewGuid().ToString("N"), $"BenchHero{i:00}")).ToArray();
            using var rootKey = RSA.Create(2048);
            var caReq = new CertificateRequest("CN=QA isolated test CA", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            caReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
            caReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign,true));
            root = caReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(2));
            using var serverKey = RSA.Create(2048);
            var req = new CertificateRequest("CN=qa.test.invalid", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName("qa.test.invalid");
            req.CertificateExtensions.Add(san.Build());
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection {new Oid("1.3.6.1.5.5.7.3.1")},true));
            using var publicLeaf = req.Create(root, DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddHours(1), RandomNumberGenerator.GetBytes(16));
            using var combined = publicLeaf.CopyWithPrivateKey(serverKey);
            leaf = X509CertificateLoader.LoadPkcs12(combined.Export(X509ContentType.Pfx), null);
            listener = new TcpListener(address, GameConnection.Port);
            listener.Start(20);
            acceptLoop = Accept();
        }

        internal Benchmark MakeRun(int[] levels, int repeats, string name = "qa.test.invalid") =>
            new(address,name,users,levels,repeats) { ConnectionFactory = () => new GameConnection(root) };

        async Task Accept()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                    connections.Add(Serve(await listener.AcceptTcpClientAsync(stop.Token)));
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }

        async Task Serve(TcpClient peer)
        {
            using var tcp = peer;
            using var tls = new SslStream(tcp.GetStream());
            try
            {
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = leaf }, stop.Token);
                async Task Send(MsgId id,IMessage m)
                {
                    byte[] frame = new byte[m.CalculateSize() + 4];
                    BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
                    BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2),(ushort)id);
                    m.ToByteArray().CopyTo(frame,4);
                    await tls.WriteAsync(frame,stop.Token);
                }
                await Send(MsgId.SConnected,new S_Connected());
                await Send(MsgId.SPing,new S_Ping { PingId = 7 });
                FixtureUser? user = null;
                var header = new byte[4];
                while (true)
                {
                    await tls.ReadExactlyAsync(header,stop.Token);
                    byte[] body = new byte[BinaryPrimitives.ReadUInt16LittleEndian(header) - 4];
                    await tls.ReadExactlyAsync(body,stop.Token);
                    switch ((MsgId)BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2)))
                    {
                        case MsgId.CPong: break;
                        case MsgId.CLogin:
                            var login = C_Login.Parser.ParseFrom(body);
                            user = users.Single(u => u.Id == login.Id && u.Password == login.Password);
                            Interlocked.Increment(ref Logins);
                            if (Interlocked.CompareExchange(ref authenticating,1,0) != 0)
                                await Send(MsgId.SLogin,new S_Login { LoginStatus = LoginStatus.None });
                            else
                            {
                                await Task.Delay(160, stop.Token); // Known observable overlap; never use as an engine timing.
                                Interlocked.Exchange(ref authenticating,0);
                                await Send(MsgId.SLogin,new S_Login { LoginStatus = LoginStatus.Success });
                            }
                            break;
                        case MsgId.CSelectServer:
                            await Send(MsgId.SSelectServer,new S_SelectServer { ServerId = 1, ChannelId = 1, EnterServerResult = EnterServerResult.Success }); break;
                        case MsgId.CRequestPlayerList:
                            await Send(MsgId.SRequestPlayerList,new S_RequestPlayerList { PlayerInfoList = {new PlayerSelectInfo { PlayerId = 1, Name = user!.Character }} }); break;
                        case MsgId.CSelectPlayer:
                            await Send(MsgId.SSelectPlayer,new S_SelectPlayer { CanSelect = true, PlayerId = 1 }); break;
                        case MsgId.CEnterGame:
                            await Send(MsgId.SEnterGame,new S_EnterGame { MapId = 1, ObjectState = new ObjectState { Name = user!.Character } }); break;
                        case MsgId.CRequestInitGameRoomData:
                            await Send(MsgId.SRequestInitGameRoomData,new S_RequestInitGameRoomData {InitGameRoomData = new InitGameRoomData {Level = 1}});
                            await Send(MsgId.SUpdateItemDataAll,new S_UpdateItemDataAll());
                            if (!dropCurrency) await Send(MsgId.SUpdateCurrencyDataAll,new S_UpdateCurrencyDataAll {CurrencyData = new CurrencyData {Level = 1, Gold = 1000}});
                            break;
                        default: throw new InvalidOperationException("Unexpected QA journey request.");
                    }
                }
            }
            catch (Exception e) when (e is EndOfStreamException or IOException or AuthenticationException or OperationCanceledException) { }
        }

        public async ValueTask DisposeAsync()
        {
            stop.Cancel(); listener.Stop();
            await acceptLoop;
            await Task.WhenAll(connections);
            leaf.Dispose(); root.Dispose(); stop.Dispose();
        }
    }
}
