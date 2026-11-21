extern alias legacy;

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using ServerCore;
using LegacySession = legacy::ServerCore.PacketSession;

const int repeats = 6;
const int messages = 600;
const int warmup = 100;
using var signingKey = RSA.Create(2048);
var request = new CertificateRequest("CN=localhost", signingKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var names = new SubjectAlternativeNameBuilder();
names.AddDnsName("localhost");
request.CertificateExtensions.Add(names.Build());
request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddHours(2));
// Windows services and SSH logon sessions may have no user key store.
var store = args.Contains("--machine-key") ? X509KeyStorageFlags.MachineKeySet : X509KeyStorageFlags.DefaultKeySet;
using var certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx), (string?)null, store);
var options = new SslServerAuthenticationOptions
{
    ServerCertificateContext = SslStreamCertificateContext.Create(certificate, null, offline: true),
    EnabledSslProtocols = SslProtocols.Tls12,
    AllowTlsResume = false
};
var clientOptions = new SslClientAuthenticationOptions
{
    TargetHost = "localhost", EnabledSslProtocols = SslProtocols.Tls12, AllowTlsResume = false,
    RemoteCertificateValidationCallback = (_, peer, _, errors) =>
        peer != null && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == 0 &&
        peer.GetRawCertData().AsSpan().SequenceEqual(certificate.RawData)
};
using var process = Process.GetCurrentProcess();
Console.WriteLine(JsonSerializer.Serialize(new { os = RuntimeInformation.OSDescription, runtime = Environment.Version.ToString(),
    cpuCount = Environment.ProcessorCount, repeats, messages, warmup, protocol = "TLS 1.2", resume = false }));
var modes = new[] { Mode.LegacySession, Mode.GameTlsSession, Mode.StreamTcp, Mode.StreamTls };
foreach (var mode in modes)
    await Measure(mode, 1, 64, 40);
foreach (int size in new[] { 64, 1024 })
foreach (int peers in new[] { 1, 8 })
for (int round = 0; round < repeats; round++)
{
    var order = round % 2 == 0 ? modes : modes.Reverse();
    foreach (var mode in order)
    {
        var result = await Measure(mode, peers, size, messages);
        Console.WriteLine(JsonSerializer.Serialize(new { round = round + 1, mode = mode.ToString(), size, peers, result }));
    }
}

async Task<object> Measure(Mode mode, int peers, int size, int count)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    var token = timeout.Token;
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start(peers);
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    var ready = new SemaphoreSlim(0, peers);
    var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var finished = new SemaphoreSlim(0, peers);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var connectTimes = new double[peers];
    var handshakeTimes = new double[peers];
    var serverTasks = Enumerable.Range(0, peers).Select(_ => Serve()).ToArray();
    var clientTasks = Enumerable.Range(0, peers).Select(Client).ToArray();
    foreach (Task worker in serverTasks.Concat<Task>(clientTasks))
    {
        _ = worker.ContinueWith(failed =>
        {
            Console.Error.WriteLine(failed.Exception!.GetBaseException());
            timeout.Cancel();
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
    try
    {
        for (int i = 0; i < peers; i++) await ready.WaitAsync(token);
        long memory = GC.GetTotalAllocatedBytes(precise: true);
        var cpu = process.TotalProcessorTime;
        long t = Stopwatch.GetTimestamp();
        start.SetResult();
        for (int i = 0; i < peers; i++) await finished.WaitAsync(token);
        double seconds = Stopwatch.GetElapsedTime(t).TotalSeconds;
        double cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - memory;
        release.SetResult();
        var samples = (await Task.WhenAll(clientTasks)).SelectMany(row => row).Order().ToArray();
        await Task.WhenAll(serverTasks).WaitAsync(token);
        return new { matched = samples.Length, p50 = P(samples, .50), p95 = P(samples, .95), p99 = P(samples, .99),
            repliesPerSecond = samples.Length / seconds, allocatedBytesPerReply = (double)allocated / samples.Length,
            processCpuMs = cpuMs, connectUs = connectTimes, handshakeUs = handshakeTimes };
    }
    finally
    {
        release.TrySetCanceled();
        start.TrySetCanceled();
        timeout.Cancel();
        ready.Dispose();
        finished.Dispose();
    }

    async Task Serve()
    {
        var accepted = await listener.AcceptTcpClientAsync(token);
        accepted.NoDelay = true;
        if (mode == Mode.LegacySession)
        {
            // The historical session closes its own socket in Disconnect.
            var legacySession = new LegacyEcho();
            legacySession.Start(accepted.Client);
            await legacySession.Closed.Task.WaitAsync(token);
            return;
        }
        using var socket = accepted;
        using var network = socket.GetStream();
        bool useTls = mode is Mode.StreamTls or Mode.GameTlsSession;
        using var tls = useTls ? new SslStream(network, leaveInnerStreamOpen: true) : null;
        if (tls != null) await tls.AuthenticateAsServerAsync(options, token);
        if (mode == Mode.GameTlsSession)
        {
            var session = new GameEcho();
            session.Start(tls!, socket.Client.RemoteEndPoint!);
            await session.Closed.Task.WaitAsync(token);
        }
        else
        {
            Stream stream = (Stream?)tls ?? network;
            var buffer = new byte[size];
            for (int i = 0; i < warmup + count; i++)
            {
                await stream.ReadExactlyAsync(buffer, token);
                await stream.WriteAsync(buffer, token);
            }
        }
    }

    async Task<double[]> Client(int id)
    {
        using var socket = new TcpClient { NoDelay = true };
        long t = Stopwatch.GetTimestamp();
        await socket.ConnectAsync(endpoint.Address, endpoint.Port, token);
        connectTimes[id] = Stopwatch.GetElapsedTime(t).TotalMicroseconds;
        using var network = socket.GetStream();
        bool useTls = mode is Mode.StreamTls or Mode.GameTlsSession;
        using var tls = useTls ? new SslStream(network, leaveInnerStreamOpen: true) : null;
        if (tls != null)
        {
            t = Stopwatch.GetTimestamp();
            await tls.AuthenticateAsClientAsync(clientOptions, token);
            handshakeTimes[id] = Stopwatch.GetElapsedTime(t).TotalMicroseconds;
        }
        Stream stream = (Stream?)tls ?? network;
        var frame = new byte[size];
        var answer = new byte[size];
        new Random(id + 1711).NextBytes(frame);
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)size));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 1);
        var samples = new double[count];
        for (int i = -warmup; i < count; i++)
        {
            if (i == 0) { ready.Release(); await start.Task.WaitAsync(token); }
            BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4), i);
            t = Stopwatch.GetTimestamp();
            await stream.WriteAsync(frame, token);
            await stream.ReadExactlyAsync(answer, token);
            if (i >= 0) samples[i] = Stopwatch.GetElapsedTime(t).TotalMicroseconds;
            if (!frame.AsSpan().SequenceEqual(answer)) throw new InvalidDataException("Echo frame mismatch.");
        }
        finished.Release();
        await release.Task.WaitAsync(token);
        return samples;
    }
}

static double P(double[] sorted, double percentile) => sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];

enum Mode { LegacySession, GameTlsSession, StreamTcp, StreamTls }

sealed class GameEcho : PacketSession
{
    public TaskCompletionSource Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override void OnConnected(EndPoint remote) { }
    public override void OnDisconnected(EndPoint remote) => Closed.TrySetResult();
    public override void OnSend(int written) { }
    public override void OnRecvPacket(ArraySegment<byte> frame) => Send(new ArraySegment<byte>(frame.ToArray()));
}

sealed class LegacyEcho : LegacySession
{
    public TaskCompletionSource Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override void OnConnected(EndPoint remote) { }
    public override void OnDisconnected(EndPoint remote) => Closed.TrySetResult();
    public override void OnSend(int written) { }
    public override void OnRecvPacket(ArraySegment<byte> frame) => Send(new ArraySegment<byte>(frame.ToArray()));
}
