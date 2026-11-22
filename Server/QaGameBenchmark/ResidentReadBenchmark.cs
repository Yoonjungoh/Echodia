using System.Net;

namespace QaGameBenchmark;

internal record ReadBurst(int Wave, DateTimeOffset StartedUtc, DateTimeOffset FinishedUtc,
    double StartMs, double EndMs, Sample[] Reads);

internal sealed class ReadLevel(int concurrency)
{
    public int Concurrency { get; } = concurrency;
    public List<ReadBurst> Bursts { get; } = new();
    public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FinishedUtc { get; private set; }
    public double WindowMs { get; private set; }
    public int Attempted => Bursts.Sum(b => b.Reads.Length);
    public int Completed => Bursts.Sum(b => b.Reads.Count(r => r.Outcome == "Success"));
    public int Failed => Attempted - Completed;
    public Percentiles SuccessLatency => Summary.Durations(Bursts.SelectMany(b => b.Reads)
        .Where(r => r.Outcome == "Success").Select(r => r.DurationMs));
    public Dictionary<string,int> Outcomes => Bursts.SelectMany(b => b.Reads)
        .GroupBy(r => r.Outcome).ToDictionary(g => g.Key, g => g.Count());
    public double? CompletedRequestsPerSecond => WindowMs > 0 ? Completed * 1000d / WindowMs : null;
    internal void Finish(double start)
    {
        WindowMs = Attempt.Now - start;
        FinishedUtc = DateTimeOffset.UtcNow;
    }
}

internal sealed class ResidentReadBenchmark(IPAddress endpoint, string tlsName, FixtureUser[] users, int[] levels)
{
    internal const int WavesPerLevel = 6;
    internal const int IntervalMs = 1000;
    internal Func<GameConnection> ConnectionFactory { get; init; } = () => new GameConnection();
    public List<Attempt> Sessions { get; } = new();
    public List<ReadLevel> Levels { get; } = new();
    public string StopReason { get; private set; } = "Completed";

    static async Task<Sample> ReadAtBarrier(GameConnection socket, Task gate, CancellationToken ct)
    {
        await gate;
        return await Benchmark.ProbeInventory(socket, ct);
    }

    // The final interval is included so that six successful bursts never inflate the paced throughput.
    static async Task WaitForNextSlot(double deadline, CancellationToken ct)
    {
        while (deadline > Attempt.Now)
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1,deadline - Attempt.Now)), ct);
    }

    internal async Task Run(CancellationToken ct)
    {
        var sockets = new List<GameConnection>();
        try
        {
            foreach (var concurrency in levels)
            {
                while (sockets.Count < concurrency)
                {
                    var user = users[sockets.Count];
                    var socket = ConnectionFactory();
                    var session = new Attempt();
                    sockets.Add(socket);
                    Sessions.Add(session);
                    await Benchmark.Enter(endpoint, tlsName, socket, user, session, () => {}, Task.CompletedTask, ct);
                    await session.Step("initialInventory", socket.ReadInventory, ct);
                }
                // Keep the sequential warmup reads outside every measured window.
                await Task.Delay(IntervalMs, ct);
                var level = new ReadLevel(concurrency);
                Levels.Add(level);
                double start = Attempt.Now;
                try
                {
                    for (int wave = 1; wave <= WavesPerLevel; wave++)
                    {
                        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        var readers = sockets.Select(socket => ReadAtBarrier(socket, gate.Task, ct)).ToArray();
                        var utc = DateTimeOffset.UtcNow;
                        var sent = Attempt.Now;
                        gate.SetResult();
                        var results = await Task.WhenAll(readers);
                        level.Bursts.Add(new ReadBurst(wave, utc, DateTimeOffset.UtcNow, sent, Attempt.Now, results));
                        if (results.Any(r => r.Outcome != "Success"))
                        {
                            StopReason = "ReadFailure";
                            return;
                        }
                        // One finished request per participant, then at least a second between actual read starts.
                        await WaitForNextSlot(results.Max(r => r.StartMs) + IntervalMs, ct);
                    }
                }
                finally { level.Finish(start); }
            }
        }
        catch (Exception e)
        {
            StopReason = "SetupOrRun_" + Attempt.Code(e, ct);
        }
        finally
        {
            for (int i = 0; i < sockets.Count; i++)
            {
                await Benchmark.Close(sockets[i], Sessions[i], ct);
                await sockets[i].DisposeAsync();
            }
            if (StopReason == "Completed" && Sessions.Any(s => s.Outcome != "Success"))
                StopReason = "SessionCloseFailure";
        }
    }
}
