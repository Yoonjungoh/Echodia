# Echodia QA host counters

The Windows Performance Monitor recording complements the game client’s per-request JSON. A busy host can spend time on operating-system work or background services. Report host CPU and game-process CPU separately when attributing a delay.

## What was compared

The single-host configuration ran game, login, SQL Express and Memurai on the original Windows game EC2. The four-host configuration kept that game host and placed Kestrel login, SQL Express and Redis on their dedicated hosts. Both configurations ran the same game and login binaries. Both caches used TLS and the same AOF policy. Local SQL shared memory on one host, private TLS over TCP on four hosts, different cache implementations and different available resources are part of the comparison.

## Capture and alignment

1. Point the signed-in test client at the private QA listener through the same loopback tunnel in both configurations. Use the isolated fixture database and its synthetic accounts. Warm up before measuring.
2. On each participating host, start `Measure-QAHost.ps1 -Action Start -Series single-main -Role Game` with the corresponding series and role. Use `split-main` for the four-host measurement. Record a separate series for the already-connected inventory workload.
3. After the client finishes, run the same command with `-Action Stop`. Windows produces a native BLG recording, a CSV exported by relog and JSON metadata including the time zone, CPU count and enabled counter paths. The one-second sample interval is too coarse to attribute one fast inventory response to a particular CPU sample.
4. For the run-wide host table, keep valid counter samples from the start of the first measured wave through completion of the last measured wave, including rests between waves. For client throughput, divide completed sessions by the sum of measured wave durations, excluding those rests. Report refusals separately. The client calculates nearest-rank percentiles from successful observations and reports their sample counts.
5. A Performance Monitor screenshot of the full BLG also includes warmup and time around the measured waves. Label the two windows separately. Treat `LoginNone` as an observed protocol outcome. The game protocol uses that response for a busy login gate and for some backend failures.

The reconnect client and the resident-read client were frozen separately to keep each executable identical across its two runs. The reconnect source is in [df58647](https://github.com/Yoonjungoh/Echodia/commit/df58647eece90a9ad1e631f422dd2018ac45924a). The resident-read source is in [21b14aa](https://github.com/Yoonjungoh/Echodia/commit/21b14aa838831e7ede3d0bb1f235afc8ef04f1b9). The isolated server configuration and validation are in [2d7037b](https://github.com/Yoonjungoh/Echodia/commit/2d7037bc80d7eaf37b190684dd591ad2bcb59040).
