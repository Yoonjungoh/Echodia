using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using QaGameBenchmark;

if (args.Contains("--help") || args.Length == 0)
{
    Console.WriteLine("QaGameBenchmark --endpoint 127.0.0.1 --tls-name CERTIFICATE_NAME --fixture accounts.json --label single|split|local --output NEW.json [--level 1|4|8|16] [--repeats 1..5]");
    return args.Length == 0 ? 2 : 0;
}
var json = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true
};
try
{
    string[] known = ["--endpoint","--tls-name","--fixture","--label","--output","--level","--repeats"];
    if (args.Length % 2 != 0) throw new ScenarioError("InvalidOptions");
    var options = new Dictionary<string,string>();
    for (int i = 0; i < args.Length; i += 2)
        if (!known.Contains(args[i]) || !options.TryAdd(args[i], args[i + 1])) throw new ScenarioError("InvalidOptions");
    string Required(string key) => options.GetValueOrDefault(key) ?? throw new ScenarioError("Missing_" + key);
    if (!IPAddress.TryParse(Required("--endpoint"), out var address) || address.AddressFamily != AddressFamily.InterNetwork)
        throw new ScenarioError("ExpectedTestIPv4");
    var octets = address.GetAddressBytes();
    if (!IPAddress.IsLoopback(address) && !(octets[0] == 172 && octets[1] == 31))
        throw new ScenarioError("EndpointOutsideTestNetwork");
    string tlsName = Required("--tls-name");
    if (Uri.CheckHostName(tlsName) is not (UriHostNameType.Dns or UriHostNameType.IPv4))
        throw new ScenarioError("InvalidTlsName");
    string label = Required("--label");
    if (label is not ("single" or "split" or "local")) throw new ScenarioError("InvalidTopology");
    int[] allowedLevels = [1,4,8,16];
    int[] levels = options.TryGetValue("--level", out var l) ? [int.Parse(l, CultureInfo.InvariantCulture)] : allowedLevels;
    int repeats = int.Parse(options.GetValueOrDefault("--repeats", "3"), CultureInfo.InvariantCulture);
    if (!levels.All(allowedLevels.Contains) || repeats is < 1 or > 5) throw new ScenarioError("InvalidLoadBound");
    var source = new FileInfo(Required("--fixture"));
    if (source.Length <= 0 || source.Length > 32768) throw new ScenarioError("InvalidFixtureSize");
    Fixture? fixture;
    await using (var input = source.OpenRead())
        fixture = await JsonSerializer.DeserializeAsync<Fixture>(input, json);
    if (fixture?.Accounts?.Length != 17) throw new ScenarioError("Expected17FixtureAccounts");
    for (int i = 0; i < fixture.Accounts.Length; i++)
    {
        var u = fixture.Accounts[i];
        if (u == null || u.Id != $"EchodiaBench{i + 1:00}" || u.Character != $"BenchHero{i + 1:00}" ||
            u.Password is not {Length: >= 12 and <= 128})
            throw new ScenarioError("InvalidFixtureIdentity");
    }
    string output = Path.GetFullPath(Required("--output"));
    await using var result = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(12));
    Console.CancelKeyPress += (_,e) => { e.Cancel = true; cancellation.Cancel(); };
    var startedUtc = DateTimeOffset.UtcNow;
    var test = new Benchmark(address,tlsName,fixture.Accounts,levels,repeats);
    await test.Run(cancellation.Token);
    var report = new
    {
        schemaVersion = 1, label, startedUtc, finishedUtc = DateTimeOffset.UtcNow,
        runtime = RuntimeInformation.FrameworkDescription, driverCpuCount = Environment.ProcessorCount,
        driverVersion = typeof(Benchmark).Assembly.GetCustomAttributes(false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion,
        endpoint = address.ToString(), port = GameConnection.Port, tlsName, levels, repeats,
        timeoutSecondsPerStage = 12, interRoundRestMs = 1500, residentProbeScheduleMs = new[] { 0,75,150 },
        stopReason = test.StopReason, warmup = test.Warmup, residentSession = test.ResidentSession, residentIdleFailures = test.ResidentIdleFailures, waves = test.Waves,
        summaries = test.Waves.GroupBy(w => w.Concurrency).Select(Summary.Level).ToArray()
    };
    await JsonSerializer.SerializeAsync(result, report, json);
    Console.WriteLine($"rounds={test.Waves.Count} stop={test.StopReason}");
    return test.StopReason == "Completed" ? 0 : 1;
}
catch (Exception e)
{
    Console.Error.WriteLine(e is ScenarioError ? e.Message : e.GetType().Name);
    return 2;
}
