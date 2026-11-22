using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Channels;

namespace QaGameBenchmark;

internal record FixtureUser([property: System.Text.Json.Serialization.JsonPropertyName("account")] string Id, string Password, string Character);
internal record Fixture(FixtureUser[] Accounts);
internal record Sample(double StartMs, double DurationMs, string Outcome)
{
    internal double EndMs => StartMs + DurationMs;
}
internal sealed class Attempt
{
    static readonly long Origin = Stopwatch.GetTimestamp();
    internal static double Now => Stopwatch.GetElapsedTime(Origin).TotalMilliseconds;
    public string Outcome { get; set; } = "Success";
    public string? FailedStage { get; set; }
    public string? TlsVersion { get; set; }
    public Dictionary<string,Sample> Stages { get; } = new();

    internal async Task Step(string name, Func<CancellationToken,Task> action, CancellationToken runToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(runToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        double start = Now;
        string outcome = "Success";
        try { await action(deadline.Token); }
        catch (Exception error)
        {
            outcome = Code(error, runToken);
            Outcome = outcome;
            FailedStage = name;
            throw;
        }
        finally { Stages.Add(name, new Sample(start, Now - start, outcome)); }
    }

    internal static string Code(Exception e, CancellationToken token) => e switch
    {
        ScenarioError x => x.Message,
        OperationCanceledException => token.IsCancellationRequested ? "Cancelled" : "Timeout",
        ChannelClosedException {InnerException: {} cause} => Code(cause, token),
        AuthenticationException => "TlsValidationOrHandshakeFailure",
        EndOfStreamException or ChannelClosedException => "TransportClosed",
        SocketException x => "Socket" + x.SocketErrorCode,
        IOException => "TransportIo",
        _ => e.GetType().Name
    };
}

internal record Probe(int ScheduledOffsetMs, Sample Sample, bool OverlapsLogin);
internal record Wave(int Concurrency, int Repetition, DateTimeOffset StartedUtc, double StartMs, double DurationMs, Attempt[] Players,
    Sample IdleRead, Probe[] ResidentReads)
{
    public bool AdmissionSaturated => Players.Count(p => p.Outcome == "LoginNone") * 2 >= Players.Length;
}
internal record Percentiles(int Count, double? P50Ms, double? P95Ms, double? MaxMs);

internal static class Summary
{
    internal static Percentiles Durations(IEnumerable<double> values)
    {
        var sample = values.Order().ToArray();
        if (sample.Length == 0) return new(0,null,null,null);
        double At(double p) => sample[(int)Math.Ceiling(p * sample.Length) - 1];
        return new(sample.Length, At(.50), At(.95), sample[^1]);
    }

    internal static object Level(IEnumerable<Wave> input)
    {
        var rounds = input.ToArray();
        var players = rounds.SelectMany(w => w.Players).ToArray();
        var succeeded = players.Where(p => p.Outcome == "Success").ToArray();
        double seconds = rounds.Sum(w => w.DurationMs) / 1000;
        string[] names = ["tcp","tls","welcome","login","channel","list","select","enter","initialInventory","closeNotify"];
        var stages = names.ToDictionary(name => name, name =>
        {
            var attempts = players.Where(p => p.Stages.ContainsKey(name)).Select(p => p.Stages[name]).ToArray();
            return new
            {
                attempted = attempts.Length, succeeded = attempts.Count(s => s.Outcome == "Success"),
                outcomes = attempts.GroupBy(s => s.Outcome).ToDictionary(x => x.Key, x => x.Count()),
                successLatency = Durations(attempts.Where(s => s.Outcome == "Success").Select(s => s.DurationMs))
            };
        });
        var reads = rounds.SelectMany(w => w.ResidentReads).ToArray();
        return new
        {
            concurrency = rounds[0].Concurrency, repeats = rounds.Length, attempted = players.Length,
            complete = succeeded.Length, completionRate = (double)succeeded.Length / players.Length,
            outcomes = players.GroupBy(p => p.Outcome).ToDictionary(g => g.Key,g => g.Count()),
            attemptedSessionsPerSecond = players.Length / seconds, completedSessionsPerSecond = succeeded.Length / seconds,
            wallSeconds = seconds, stages,
            successEndToEnd = Durations(succeeded.Select(p => p.Stages["closeNotify"].EndMs - p.Stages["tcp"].StartMs)),
            residentIdle = Durations(rounds.Where(w => w.IdleRead.Outcome == "Success").Select(w => w.IdleRead.DurationMs)),
            residentOverlappingLogin = Durations(reads.Where(x => x.OverlapsLogin && x.Sample.Outcome == "Success").Select(x => x.Sample.DurationMs)),
            residentProbeOutcomes = reads.GroupBy(x => x.Sample.Outcome).ToDictionary(g => g.Key,g => g.Count())
        };
    }
}
