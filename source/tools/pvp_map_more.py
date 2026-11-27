"""More points and legs for a frontier zone that has a roam map already (PvpRoamZones.cs).

    python pvp_map_more.py <zone word> [<zone word> ...] [--out <folder>] [--write]

For the zones the 8v8 zone is drawn from: their first maps (pvp_map_pick.py) have seven to nine
points 18,000 and more apart, and in six of the nine a point or three with one leg only, which a
group can but walk back from. This keeps a zone's points, legs and starts as they are (the starts
were looked at on the client's map and played) and adds to them:

  1. the ground, as pvp_map_pick.py finds it (a grid of 7,000, the largest piece that can be walked);
  2. points on it 9,800 or more from every point there is and from one another (as on Agramon), none within
     9,000 of a keep or of where a realm without a keep comes in, the best joined first, up to
     fourteen points in all;
  3. a leg between any two points up to 22,500 apart whose way on foot is within 13% of the
     straight line, that passes no keep within 3,500 and no camp within 4,500, and has no third
     point within 2,500 of it (a leg of the first map with a new point on it goes: its two halves
     take its place); and for a point that still has one leg only, one more where there is a
     way at all, up to 30% longer than the straight line. No new leg ends in water or goes through
     2,000 units of it (asked of the mesh every 2,000 units along it; a stream is crossed as the
     zone has it); a leg of the first map that does is named, and stays.

It asks the running scrim server for the ways (`nav-route`, which changes nothing): start it with
`scrim_server.py start --test`. The answer is printed, and kept as <out>/<word>_more.json for
pvp_map_draw.py; with --write the zone's two lists in PvpRoamZones.cs are replaced.
"""
import itertools
import json
import math
import os
import re
import sqlite3
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scrim_server
import scrim_test

STEP, SPACING, LONGEST, MOST = 7000, 9800, 22500, 14
SOURCE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "server", "GameServer", "bots", "pvp", "PvpRoamZones.cs")
ZONE = re.compile(r'(Zone\("(\w+)", "([^"]+)", "[^"]+", (\d+), (\d+),\s*\[)(.*?)(\],\s*\[)(.*?)(\],\s*(\d+),\s*\[)(.*?)\]\);', re.S)


def from_segment(point, a, b):
    length = (b[0] - a[0]) ** 2 + (b[1] - a[1]) ** 2
    t = 0 if length == 0 else max(0, min(1, ((point[0] - a[0]) * (b[0] - a[0]) + (point[1] - a[1]) * (b[1] - a[1])) / length))
    return math.dist(point, (a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])))


def compass(point, ox, oy, size):
    col, row = min(2, int((point[0] - ox) * 3 / size)), min(2, int((point[1] - oy) * 3 / size))
    return [["north-west", "north", "north-east"], ["west", "middle", "east"], ["south-west", "south", "south-east"]][row][col]


def more(word, runtime, out, write):
    text = open(SOURCE, encoding="utf-8", newline="").read()
    found = next((m for m in ZONE.finditer(text) if m.group(2) == word), None)
    if found is None:
        raise SystemExit(f"{word}: no such map in PvpRoamZones.cs")
    name, region, zone_id = found.group(3), int(found.group(4)), int(found.group(5))
    points = [(int(x), int(y)) for x, y, _ in re.findall(r'\((\d+), (\d+), "([^"]+)"\)', found.group(6))]
    names = {(int(x), int(y)): n for x, y, n in re.findall(r'\((\d+), (\d+), "([^"]+)"\)', found.group(6))}
    first = [(points[int(a)], points[int(b)]) for a, b in re.findall(r'\((\d+), (\d+)\)', found.group(8))]
    camps = [(int(x), int(y)) for x, y, keep in re.findall(r'\(eRealm\.\w+, (\d+), (\d+), (true|false)', found.group(11)) if keep == "false"]
    db = sqlite3.connect("file:" + os.path.join(runtime, "data", "opendaoc.sqlite3.db").replace("\\", "/") + "?mode=ro", uri=True)
    ox, oy, width = db.execute("select OffsetX, OffsetY, Width from Zones where ZoneID = ?", (zone_id,)).fetchone()
    ox, oy, size = ox * 8192, oy * 8192, width * 8192
    keeps = [(x, y) for x, y in db.execute("select X, Y from Keep where Region = ?", (region,)) if ox <= x < ox + size and oy <= y < oy + size]
    asked = [0]

    def route(a, b):
        asked[0] += 1
        answer = scrim_test.request(runtime, "nav-route", f"Region={region}", f"X1={a[0]}", f"Y1={a[1]}", f"X2={b[0]}", f"Y2={b[1]}")
        data = (answer or {}).get("Data") or {}
        return float(data.get("Length") or 0) if data.get("Reached") else None

    def good(a, b, most):
        length = route(a, b)
        return length if length is not None and 0.8 * math.dist(a, b) <= length <= most * math.dist(a, b) else None

    def wet(a, b):
        """Whether the straight line from a to b is in water, asked of the mesh every 2,000 units (a request takes half a second)."""
        steps = max(1, int(math.dist(a, b) // 2000))
        for step in range(steps + 1):
            x, y = a[0] + (b[0] - a[0]) * step / steps, a[1] + (b[1] - a[1]) * step / steps
            answer = scrim_test.request(runtime, "water", f"Region={region}", f"X={int(x)}", f"Y={int(y)}")
            if ((answer or {}).get("Data") or {}).get("InWater"):
                return True
        return False

    # 1. The ground.
    margin = (size - (size // STEP) * STEP) // 2 + STEP // 2
    grid = [(x, y) for x in range(ox + margin, ox + size, STEP) for y in range(oy + margin, oy + size, STEP)]
    kept = os.path.join(out, word + ".ground.json")
    if os.path.exists(kept):
        short = [(tuple(a), tuple(b)) for a, b in json.load(open(kept, encoding="utf-8"))]
    else:
        short = [(a, b) for a in grid for b in grid if a < b and math.dist(a, b) <= STEP * 1.1 and good(a, b, 1.15) is not None]
        json.dump(short, open(kept, "w", encoding="utf-8"))
    degree = {point: sum(1 for leg in short if point in leg) for point in grid}
    # (The piece of ground the map is on: what can be reached from its points over the grid.)
    ground, edge = set(), [point for point in points if degree.get(point)]
    while edge:
        node = edge.pop()
        if node not in ground:
            ground.add(node)
            edge += [b if a == node else a for a, b in short if node in (a, b)]

    # 2. The points.
    middle = (ox + size / 2, oy + size / 2)
    added = []
    for point in sorted(ground, key=lambda p: (-degree[p], math.dist(p, middle))):
        if len(points) + len(added) >= MOST:
            break
        if (degree[point] >= 3 and all(math.dist(point, keep) >= 9000 for keep in keeps) and all(math.dist(point, camp) >= 9000 for camp in camps) and
                all(math.dist(point, other) >= SPACING for other in points + added)):
            added.append(point)
    everything = points + added

    # 3. The legs.
    def clear(a, b):
        return (all(from_segment(keep, a, b) >= 3500 for keep in keeps) and all(from_segment(camp, a, b) >= 4500 for camp in camps) and
                not any(c not in (a, b) and from_segment(c, a, b) < 2500 for c in everything))

    legs = {}
    for a, b in first:
        if not any(c not in (a, b) and from_segment(c, a, b) < 2500 for c in added):
            legs[(a, b)] = None
    for a, b in itertools.combinations(everything, 2):
        if (a, b) in legs or (b, a) in legs or math.dist(a, b) > LONGEST or not clear(a, b):
            continue
        length = good(a, b, 1.13)
        if length is not None and not wet(a, b):
            legs[(a, b)] = length
    # (A point with one leg gets one more where there is a way at all, up to 30% longer than the straight line, the shortest first.)
    for point in everything:
        if sum(1 for leg in legs if point in leg) == 1:
            for other in sorted(everything, key=lambda p: math.dist(p, point)):
                if other != point and (point, other) not in legs and (other, point) not in legs and math.dist(point, other) <= LONGEST and clear(point, other):
                    length = good(point, other, 1.3)
                    if length is not None and not wet(point, other):
                        legs[(point, other)] = length
                        break
    # (A point no leg reaches is left out; the points of the first map all stay.)
    added = [point for point in added if any(point in leg for leg in legs)]
    everything = points + added
    legs = {leg: length for leg, length in legs.items() if leg[0] in everything and leg[1] in everything}

    used = set(names.values())
    for point in added:
        word_ = compass(point, ox, oy, size)
        base = word_ if word_ == "middle" else word_ + " field"
        same = next((other for other, its in names.items() if its == base), None)
        label = base if same is None else ("inner " if math.dist(point, middle) < math.dist(same, middle) else "outer ") + base
        label = next(word for word in [label] + [f"{order} {base}" for order in ("second", "third", "fourth", "fifth", "sixth")] if word not in used)
        names[point] = label
        used.add(label)

    ways = sorted((everything.index(a), everything.index(b)) for a, b in legs)
    count = {index: sum(1 for way in ways if index in way) for index in range(len(everything))}
    lengths = [math.dist(everything[a], everything[b]) for a, b in ways]
    gone = [leg for leg in first if leg not in legs]
    for a, b in first:
        if (a, b) in legs and wet(a, b):
            print(f"  a leg of the first map crosses water: {names[a]} to {names[b]}")
    print(f"{name}: {len(points)} points and {len(first)} legs -> {len(everything)} points and {len(ways)} legs of {min(lengths):,.0f} to {max(lengths):,.0f} units "
          f"({sum(lengths) / len(lengths):,.0f} on average); {len(gone)} of the first legs gone; "
          f"dead ends: {[names[everything[i]] for i in count if count[i] == 1] or 'none'}; {asked[0]} ways asked")
    result = dict(zone=zone_id, name=name, region=region, offset=[ox, oy], map=f"z{zone_id:03d}.dds",
                  points=[dict(at=list(point), name=names[point], quiet=False) for point in everything],
                  ways=[list(way) for way in ways], lengths=[round(length) for length in lengths],
                  starts=[dict(realm="", at=list(camp), keep=None, first=[]) for camp in camps], keeps=[dict(name="", at=list(keep)) for keep in keeps])
    json.dump(result, open(os.path.join(out, word + "_more.json"), "w", encoding="utf-8"), indent=1)
    points_text = ", ".join(f'({x}, {y}, "{names[(x, y)]}")' for x, y in everything)
    ways_text = ", ".join(f"({a}, {b})" for a, b in ways)
    if write:
        text = text[:found.start(6)] + points_text + text[found.end(6):found.start(8)] + ways_text + text[found.end(8):]
        open(SOURCE, "w", encoding="utf-8", newline="").write(text)
    return len(everything), len(ways), min(lengths), max(lengths)


def main():
    arguments = sys.argv[1:]
    write = "--write" in arguments
    out = arguments[arguments.index("--out") + 1] if "--out" in arguments else "."
    words = [word for index, word in enumerate(arguments) if not word.startswith("--") and (index == 0 or arguments[index - 1] != "--out")]
    runtime = os.path.join(scrim_server.DEFAULT_GAME, "runtime")
    for word in words:
        more(word, runtime, out, write)


if __name__ == "__main__":
    main()
