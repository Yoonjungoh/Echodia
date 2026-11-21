"""Summarize JSON lines emitted by TransportBenchmark, using only complete runs."""

import json
import math
import statistics
import sys
from collections import defaultdict
from pathlib import Path


def read_run(path):
    raw = Path(path).read_bytes()
    encoding = "utf-16" if raw.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8-sig"
    header, *rows = map(json.loads, raw.decode(encoding).splitlines())
    expected = {(mode, size, peers, n)
                for mode in ("LegacySession", "GameTlsSession", "StreamTcp", "StreamTls")
                for size in (64, 1024) for peers in (1, 8)
                for n in range(1, header["repeats"] + 1)}
    seen = set()
    for row in rows:
        key = (row["mode"], row["size"], row["peers"], row["round"])
        if key in seen or row["result"]["matched"] != row["peers"] * header["messages"]:
            raise ValueError(f"Duplicate round or incomplete replies in {path}")
        seen.add(key)
    if expected != seen:
        raise ValueError(f"Incomplete experiment in {path}")
    return header, rows


def summarize(paths):
    groups = defaultdict(list)
    metadata = None
    replies = 0
    for path in paths:
        header, rows = read_run(path)
        if metadata is not None and header != metadata:
            raise ValueError("Separate files with different runtime settings")
        metadata = header
        for row in rows:
            result = row["result"]
            replies += result["matched"]
            groups[row["size"], row["peers"], row["mode"]].append(result)
    print(json.dumps(metadata))
    print(f"Verified replies: {replies}")
    print("bytes peers mode rounds p50_us p95_us p99_us replies/s allocated_B/reply")
    for (size, peers, mode), results in sorted(groups.items()):
        metrics = ("p50", "p95", "p99", "repliesPerSecond", "allocatedBytesPerReply")
        medians = [statistics.median(row[k] for row in results) for k in metrics]
        print(size, peers, mode, len(results), *(f"{v:.2f}" for v in medians))
    handshakes = sorted(t for (_, peers, mode), rows in groups.items()
                        if peers == 1 and mode == "GameTlsSession"
                        for row in rows for t in row["handshakeUs"])
    print(f"Single-client full TLS handshakes: n={len(handshakes)} "
          f"median_us={statistics.median(handshakes):.2f} "
          f"p95_us={handshakes[math.ceil(.95 * len(handshakes)) - 1]:.2f}")


if __name__ == "__main__":
    if len(sys.argv) == 1:
        sys.exit("Usage: python summarize.py run.jsonl [matching-repeat.jsonl ...]")
    summarize(sys.argv[1:])
