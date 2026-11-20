"""How two roaming groups came upon each other. Usage: pvp_count_roam.py <glob> [...]

For each recorded match: when each side first knew of an enemy (seconds after the ready, and how
far apart the nearest two were then), who knew first and by how much, where the enemy stood
from the way the surprised side was heading, how spread each side was at the first blow, and
who won.
"""
import collections
import glob
import json
import math
import statistics
import sys


def shape(points):
    n = len(points)
    if n < 2:
        return (0, n)
    near = [min(math.hypot(a[0] - b[0], a[1] - b[1]) for j, b in enumerate(points) if j != i) for i, a in enumerate(points)]
    return int(statistics.median(near)), max(sum(1 for b in points if math.hypot(a[0] - b[0], a[1] - b[1]) <= 350) for a in points)


leads = []
for path in sorted(f for g in sys.argv[1:] for f in glob.glob(g)):
    rows = [json.loads(line) for line in open(path, encoding="utf-8")]
    head = rows[0]
    bots = {b["n"]: b for b in head["bots"]}
    ready = next((r["t"] for r in rows if r.get("k") == "ready"), None)
    hits = [r for r in rows if r.get("k") == "hit"]
    if ready is None or not hits:
        continue
    first = hits[0]["t"]
    by = collections.defaultdict(dict)
    for r in rows:
        if r.get("k") == "snap":
            by[r["t"]][r["n"]] = r
    times = sorted(by)

    def at(t):
        return by[min(times, key=lambda x: abs(x - t))]

    def side(snap, team):
        return [(s["x"], s["y"]) for n, s in snap.items() if bots[n]["team"] == team and s["alive"]]

    def centre(points):
        return (sum(p[0] for p in points) / len(points), sum(p[1] for p in points) / len(points))

    knew = {}
    for r in rows:
        if r.get("k") == "squad" and r["t"] >= ready and (r.get("known") or 0) > 0 and r["team"] not in knew:
            snap = at(r["t"])
            a, b = side(snap, "A"), side(snap, "B")
            knew[r["team"]] = (r["t"], int(min(math.hypot(p[0] - q[0], p[1] - q[1]) for p in a for q in b)))
    end = next((r for r in rows if r.get("k") == "end"), None)
    line = "%s first blow %3d s after the ready" % (path[-14:-6], (first - ready) // 1000)
    if len(knew) == 2:
        early = min(knew, key=lambda team: knew[team][0])
        late = "B" if early == "A" else "A"
        lead = (knew[late][0] - knew[early][0]) / 1000
        leads.append(lead)
        # Where the first side stood from the way the late side was heading, when the first side saw it.
        t0 = knew[early][0]
        now, before = centre(side(at(t0), late)), centre(side(at(t0 - 3000), late))
        there = centre(side(at(t0), early))
        heading = math.atan2(now[1] - before[1], now[0] - before[0])
        bearing = math.atan2(there[1] - now[1], there[0] - now[0])
        off = abs((math.degrees(bearing - heading) + 180) % 360 - 180)
        name = head["a"] if early == "A" else head["b"]
        line += " | %s knew first, at %d apart, %.0f s before the other (who had it %d degrees off its way)" % (name[:3], knew[early][1], lead, off)
    blow = at(first)
    line += " | at the blow: %s %s, %s %s" % (head["a"][:3], shape(side(blow, "A")), head["b"][:3], shape(side(blow, "B")))
    if end:
        line += " | %s won in %d s" % (end["Winner"][:3], end["Seconds"])
    print(line)
if leads:
    print("%d matches; one side knew of the other a median of %.0f s before it was known itself; 2 s or more in %d" % (
        len(leads), statistics.median(leads), sum(1 for x in leads if x >= 2)))
