# Echodia QA game benchmark

This driver compares the same game journey on single-host and split-host deployments. Both deployments need an isolated database, an isolated login reservation store, and identical game binaries and fixture data. The driver connects to one loopback or project VPC IPv4 address on TCP 17777. With a local SSH forward, it verifies the public game certificate against the name or IP in its SAN. It uses the driver's operating system trust store and checks revocation.

## Fixture

Supply an ACL-protected JSON file containing an ordered array named `accounts`. Each entry has `account`, `character`, and `password` strings. Entries 01 through 17 must map `EchodiaBench01` through `EchodiaBench17` to `BenchHero01` through `BenchHero17`. Each account owns one level-1 character in the initial map, with 0 experience, 1000 gold, and an empty inventory. Each password must have 12 through 128 characters.

Provision the accounts before opening the listener. The game's normal login endpoint can register an unknown account. A signup response stops measurement. The driver itself sends no creation requests. Normal entry and logout can initialize quests and stats and persist the logout position, so use the dedicated QA data on both configurations.

## Run

```powershell
dotnet publish Server/QaGameBenchmark/QaGameBenchmark.csproj -c Release -o ./qa-client
./qa-client/QaGameBenchmark.exe --endpoint 127.0.0.1 --tls-name $gameCertificateName --label single --fixture $privateFixture --output ./single-run.json
```

Use `--label split` for the four-host configuration. The standard run performs three waves at each of 1, 4, 8 and 16 reconnecting players. `--level` selects one of those sizes, and `--repeats` accepts 1 through 5. Each output requires a fresh file name. Account 17 remains connected throughout measured waves. There are at most 17 simultaneous sessions, including that observer.

## Measurements

Each participating character warms up once with the full journey. In each measured wave the clients start TCP connections at one barrier, finish verified TLS and wait for the server greeting. A second barrier releases their normal login requests together. Successful clients select channel 1 on server 1, request their character list, select their own character, enter its map and request initial inventory. The driver checks the character name, level, experience, balances, and empty bag. Each client sends TLS close-notify when finished. Server-side logout and persistence can finish later.

The observer reads once immediately before each wave, then at scheduled offsets of 0, 75 and 150 ms from the login barrier. These reads are sequential on one connection. The result stores the actual start and duration of each read, including delays past its scheduled offset, and whether it overlapped any measured login request. The server sends three distinct replies for an inventory read. Missing or unexpected replies fail the read.

A wave records its UTC start and its monotonic start in milliseconds. Subtract the latter from a phase's `startMs` to locate that phase relative to the wave's UTC start. Each phase records the outcome and elapsed time. Durations of successful samples are summarized by nearest-rank p50, p95, maximum, and sample count. Throughput is measured per second using total measured wave wall time, from releasing the TCP barrier to the last participant finishing. Warmup, observer-only time and rest intervals are excluded. A successful session includes all checks and close-notify. A rejected login remains in the attempted-session count.

`LoginNone` is the actual protocol response. The server can emit it for admission contention or an unavailable authentication provider. A wave with at least half of all participants receiving that response has `admissionSaturated=true`. Normal admission rejections do not trigger retries or raise the 16-player cap. A transport error, a 12-second phase timeout, an incorrect fixture response or an observer failure stops further waves. The driver waits 1.5 seconds between waves for normal account release.

The synthetic loopback tests exercise a verified TLS handshake, normal replies, concurrent admission rejection, a mismatched certificate name, and a missing inventory response. Their injected timing is solely for verifying the driver. Run them with `dotnet test Server/QaGameBenchmark.Tests/QaGameBenchmark.Tests.csproj -c Release`.

## Concurrent resident inventory reads

Run the same command with `--mode resident-reads` and a fresh output file. This synthetic microbenchmark isolates the cost of concurrent UI data refreshes for characters already in the game. It describes a repeatable access pattern; ordinary player behavior needs separate observation.

Authenticated residents enter sequentially and warm their own inventory once. The same connections stay open as the cohort grows through 1, 4, 8 and 16 residents. Each level has six synchronized waves. Every resident sends one normal inventory request per wave and validates all three replies. The next wave starts after every outstanding reply and at least one second after the latest actual request start in the previous wave. A failed read ends escalation.

Each wave retains the UTC and monotonic intervals for its barrier and responses, the outcome and latency of each participant in fixture order, and the expected packet name for a reply deadline. The per-level summary reports attempts, completions, failures, p50/p95/max of completed reads, and completed requests per second across six paced windows. Sequential warmup and login have their own timing records. The final pacing interval is included, so 16 residents cannot exceed 16 measured requests per second.
