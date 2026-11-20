"""What each realm's members start a fight with: resistance buffs, and how long the buffing took. Usage: count_ready.py <glob> [...]"""
import collections
import glob
import json
import statistics
import sys

have = collections.defaultdict(collections.Counter)
members = collections.Counter()
seconds = []
timed_out = 0
casts = collections.Counter()
n = 0
for f in sorted(x for g in sys.argv[1:] for x in glob.glob(g)):
    rows = [json.loads(l) for l in open(f, encoding="utf-8")]
    head = rows[0]
    B = {b["n"]: b for b in head["bots"]}
    ready = [r for r in rows if r.get("k") == "ready"]
    if not ready:
        continue
    n += 1
    seconds.append(ready[0]["seconds"])
    timed_out += bool(ready[0].get("timedOut"))
    realm = {"A": head["a"][:3], "B": head["b"][:3]}
    for b in ready[0]["buffs"]:
        if b["n"] not in B:
            continue
        r = realm[B[b["n"]]["team"]]
        members[r] += 1
        for x in b["b"]:
            if "ResistBuff" in x:
                have[r][x] += 1
    for r in rows:
        if r.get("k") == "cast" and "ResistBuff" in (r.get("st") or "") and r.get("n") in B and r["t"] <= ready[0]["t"]:
            casts[(B[r["n"]]["build"], r["spell"], r.get("val"))] += 1
print("%d matches; buffing took median %s s (max %s), timed out in %d" % (n, statistics.median(seconds) if seconds else "-", max(seconds) if seconds else "-", timed_out))
for r in sorted(have):
    print("  %s: of %d members at the ready:" % (r, members[r]), {k: "%d%%" % round(100 * v / members[r]) for k, v in sorted(have[r].items())})
for r in sorted(members):
    if r not in have:
        print("  %s: no resistance buff on any of %d members" % (r, members[r]))
print("  cast before the ready by:", casts.most_common(12))
