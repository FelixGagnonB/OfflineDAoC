"""Finds points to walk between in a frontier zone, for a roam map (docs/PVP-8V8-ROADMAP.md, M11.3).

    python pvp_map_points.py <game runtime> <region> <zone min x> <zone min y> <zone size> <start>... [--step 6000]

A start is `Realm:x,y` (where the realm's teams come into the world, in front of a keep).
The script lays a grid over the zone, asks the running scrim server for the way on foot between
grid points next to each other and from each start to the grid (`nav-route`), and keeps a leg
when the way on foot is within 15% of the straight line. It prints, to be read and then written
into PvpRoam.cs by hand:

  - the grid points that have ground and at least two such legs,
  - the legs between them,
  - for each start, the points it reaches directly, nearest first.

It only asks; it changes nothing on the server.
"""
import json
import math
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def route(runtime, region, a, b):
    out = subprocess.run([sys.executable, os.path.join(HERE, "pvp_request.py"), runtime, "nav-route", f"Region={region}",
                          f"X1={a[0]}", f"Y1={a[1]}", f"X2={b[0]}", f"Y2={b[1]}"], capture_output=True, text=True).stdout
    try:
        data = json.loads(out).get("Data") or {}
    except json.JSONDecodeError:
        return None
    if not data.get("Reached"):
        return None
    return float(data.get("Length") or 0)


def main():
    words = sys.argv[1:]
    step = 6000
    if "--step" in words:
        at = words.index("--step")
        step = int(words[at + 1])
        del words[at:at + 2]
    args = words
    runtime, region = args[0], int(args[1])
    min_x, min_y, size = int(args[2]), int(args[3]), int(args[4])
    starts = {}
    for word in args[5:]:
        realm, at = word.split(":")
        starts[realm] = tuple(int(v) for v in at.split(","))

    margin = step // 2
    grid = [(x, y) for x in range(min_x + margin, min_x + size, step) for y in range(min_y + margin, min_y + size, step)]
    legs = {}
    for a in grid:
        for b in grid:
            if a < b and math.dist(a, b) <= step * 1.5:
                length = route(runtime, region, a, b)
                if length is not None and length <= 1.15 * math.dist(a, b) and length >= 0.8 * math.dist(a, b):
                    legs[(a, b)] = length
    degree = {}
    for a, b in legs:
        degree[a] = degree.get(a, 0) + 1
        degree[b] = degree.get(b, 0) + 1
    points = sorted(point for point, count in degree.items() if count >= 2)
    print(f"region {region}: {len(grid)} grid points, {len(points)} with two legs or more, {len(legs)} legs")
    for point in points:
        print(f"  point {point[0]},{point[1]}  legs {degree[point]}")
    for (a, b), length in sorted(legs.items()):
        if a in points and b in points:
            print(f"  leg {a[0]},{a[1]} - {b[0]},{b[1]}  on foot {length:,.0f}  straight {math.dist(a, b):,.0f}")
    for realm, start in starts.items():
        reached = []
        for point in points:
            if math.dist(start, point) <= 40_000:
                length = route(runtime, region, start, point)
                if length is not None and length <= 1.2 * math.dist(start, point):
                    reached.append((length, point))
        print(f"  start {realm} {start[0]},{start[1]}: " + "; ".join(f"{p[0]},{p[1]} ({l:,.0f})" for l, p in sorted(reached)[:6]))


if __name__ == "__main__":
    main()
