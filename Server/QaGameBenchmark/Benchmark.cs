using System.Net;

namespace QaGameBenchmark;

internal sealed class Benchmark(IPAddress endpoint, string tlsName, FixtureUser[] users, int[] levels, int repeats)
{
    internal Func<GameConnection> ConnectionFactory { get; init; } = () => new GameConnection();
    public List<Sample> ResidentIdleFailures { get; } = new();
    public List<Attempt> Warmup { get; } = new();
    public List<Wave> Waves { get; } = new();
    public Attempt ResidentSession { get; } = new();
    public string StopReason { get; private set; } = "Completed";
    static readonly int[] ProbeOffsets = [0, 75, 150];

    async Task Enter(GameConnection socket, FixtureUser user, Attempt a, Action ready, Task loginGate, CancellationToken ct)
    {
        await a.Step("tcp", t => socket.Connect(endpoint, t), ct);
        await a.Step("tls", t => socket.Authenticate(tlsName, t), ct);
        a.TlsVersion = socket.TlsVersion;
        await a.Step("welcome", socket.Welcome, ct);
        ready();
        await loginGate.WaitAsync(ct);
        await a.Step("login", t => socket.Login(user, t), ct);
        await a.Step("channel", socket.SelectServer, ct);
        await a.Step("list", t => socket.ListCharacter(user, t), ct);
        await a.Step("select", socket.SelectCharacter, ct);
        await a.Step("enter", t => socket.Enter(user, t), ct);
    }

    static async Task Close(GameConnection socket, Attempt a, CancellationToken ct)
    {
        if (socket.TlsVersion == null) return;
        var original = (a.Outcome, a.FailedStage);
        try { await a.Step("closeNotify", socket.Close, ct); }
        catch { /* The close outcome is retained even when an earlier step failed. */ }
        if (original.Outcome != "Success") (a.Outcome, a.FailedStage) = original;
    }

    async Task<Attempt> Peer(FixtureUser user, Task start, Action ready, Task loginGate, CancellationToken ct)
    {
        var a = new Attempt();
        await using var socket = ConnectionFactory();
        try
        {
            await start.WaitAsync(ct);
            await Enter(socket, user, a, ready, loginGate, ct);
            await a.Step("initialInventory", socket.ReadInventory, ct);
        }
        catch (Exception error)
        {
            if (a.Outcome == "Success") a.Outcome = Attempt.Code(error, ct);
        }
        finally
        {
            ready(); // Also unblocks the wave if this connection failed before login.
            await Close(socket, a, ct);
        }
        return a;
    }

    static async Task<Sample> ProbeInventory(GameConnection resident, CancellationToken ct)
    {
        var a = new Attempt();
        try { await a.Step("read", resident.ReadInventory, ct); }
        catch { /* Keep failures and deadlines in the report. */ }
        return a.Stages["read"];
    }

    static async Task<List<(int Offset,Sample Sample)>> ProbeBurst(GameConnection resident, double gateTime, CancellationToken ct)
    {
        var probes = new List<(int,Sample)>();
        foreach (int offset in ProbeOffsets)
        {
            double delay = gateTime + offset - Attempt.Now;
            try { if (delay > 0) await Task.Delay(TimeSpan.FromMilliseconds(delay), ct); }
            catch (OperationCanceledException) { break; }
            var sample = await ProbeInventory(resident, ct);
            probes.Add((offset, sample));
            if (sample.Outcome != "Success") break;
        }
        return probes;
    }

    async Task<Wave> Round(GameConnection resident, int level, int repetition, CancellationToken ct)
    {
        var idle = await ProbeInventory(resident, ct);
        if (idle.Outcome != "Success")
        {
            ResidentIdleFailures.Add(idle);
            throw new ScenarioError("ResidentIdle_" + idle.Outcome);
        }
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var login = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = Enumerable.Range(0, level)
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var clients = Enumerable.Range(0, level).Select(i =>
            Peer(users[i], start.Task, () => ready[i].TrySetResult(), login.Task, ct)).ToArray();
        var utc = DateTimeOffset.UtcNow;
        var waveStart = Attempt.Now;
        start.SetResult();
        await Task.WhenAll(ready.Select(r => r.Task));
        var loginStart = Attempt.Now;
        login.SetResult();
        var readTask = ProbeBurst(resident, loginStart, ct);
        var players = await Task.WhenAll(clients);
        var elapsed = Attempt.Now - waveStart; // Excludes later observer sampling and inter-round rest.
        var reads = await readTask;
        var loginSamples = players.SelectMany(p => p.Stages.Where(k => k.Key == "login").Select(k => k.Value)).ToArray();
        var observed = reads.Select(r => new Probe(r.Offset, r.Sample,
            loginSamples.Any(s => r.Sample.StartMs < s.EndMs && r.Sample.EndMs > s.StartMs))).ToArray();
        return new(level, repetition, utc, waveStart, elapsed, players, idle, observed);
    }

    internal async Task Run(CancellationToken ct)
    {
        // Warming each participating character once also separates first-login quest/stat initialization.
        foreach (var user in users.Take(levels.Max()))
        {
            var result = await Peer(user, Task.CompletedTask, () => {}, Task.CompletedTask, ct);
            Warmup.Add(result);
            if (result.Outcome != "Success") { StopReason = "Warmup_" + result.Outcome; return; }
        }
        await using var resident = ConnectionFactory();
        try
        {
            await Enter(resident, users[16], ResidentSession, () => {}, Task.CompletedTask, ct);
            await ResidentSession.Step("initialInventory", resident.ReadInventory, ct);
            await Task.Delay(1500, ct);
            foreach (var level in levels)
            {
                for (int rep = 1; rep <= repeats; rep++)
                {
                    var wave = await Round(resident, level, rep, ct);
                    Waves.Add(wave);
                    int complete = wave.Players.Count(p => p.Outcome == "Success");
                    Console.WriteLine($"concurrency={level} repetition={rep} completed={complete}/{level}");
                    if (wave.Players.Any(p => p.Outcome is not ("Success" or "LoginNone" or "ServerChannelFull"))
                        || wave.ResidentReads.Any(p => p.Sample.Outcome != "Success"))
                    {
                        StopReason = "UnexpectedFailure"; return;
                    }
                    await Task.Delay(1500, ct); // Allow the normal asynchronous logout to release the account.
                }
            }
        }
        catch (Exception e)
        {
            StopReason = Attempt.Code(e, ct);
            if (ResidentSession.Outcome != "Success") StopReason = "Resident_" + ResidentSession.Outcome;
        }
        finally
        {
            await Close(resident, ResidentSession, ct);
            if (StopReason == "Completed" && ResidentSession.Outcome != "Success")
                StopReason = "Resident_" + ResidentSession.Outcome;
        }
    }
}
