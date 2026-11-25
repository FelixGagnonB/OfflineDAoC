"""Proposes a roam map for a frontier zone: points to walk between, the legs between them and where each realm comes in.

    python pvp_map_pick.py <game runtime> <zone id> <out.json>

It reads the zone and its keeps from the game's database (read only) and asks the running scrim
server for ways on foot (`nav-route`, which changes nothing). What it does:

  1. lays a grid of 7,000 over the zone and keeps the ground that can be walked: grid points
     joined to the four next to them by a way within 15% of the straight line, the largest such piece;
  2. picks up to nine points on it at least 18,000 apart (15,000, then 13,000, where that gives
     fewer than seven points or no round to walk), the best joined first, none within
     9,000 of a keep (a keep's guards leave the bots alone, not a player of another realm, and a
     realm that has the keep stands up in front of it: nobody's way goes through there);
  3. keeps a leg between two of them, up to 27,000 apart, when the way on foot is within 13% of
     the straight line, does not pass within 3,500 of a keep, and no third point lies on it;
  4. puts the realm of the zone in front of one of its keeps, and the two others at walkable
     places 6,000 or more off the legs, the three as far from one another as can be.

The answer is a proposal: look at it (pvp_map_draw.py draws it) before it goes into the server.
The ground found in step 1 is kept beside the answer (<out>.ground.json) and read again by the
next run for that zone.
"""
import itertools
import json
import math
import os
import sqlite3
import sys
import time
import uuid

STEP, SPACING, LONGEST, MOST = 7000, 18000, 27000, 9
HOME = {1: "Albion", 100: "Midgard", 200: "Hibernia"}
CALLS = 0


def route(runtime, region, a, b):
    """The way on foot from a to b, or None."""
    global CALLS
    CALLS += 1
    server = os.path.join(runtime, "server")
    path = os.path.join(server, "pvp-request.json")
    # (The server takes a request away when it has answered it: one sent at that moment is lost, and is sent again.)
    for _ in range(20):
        request = {"Id": uuid.uuid4().hex, "Command": "nav-route",
                   "Args": {"Region": str(region), "X1": str(a[0]), "Y1": str(a[1]), "X2": str(b[0]), "Y2": str(b[1])}}
        with open(path + ".tmp", "w", encoding="utf-8") as handle:
            json.dump(request, handle)
        os.replace(path + ".tmp", path)
        deadline = time.time() + 4
        while time.time() < deadline:
            time.sleep(0.05)
            try:
                with open(os.path.join(server, "pvp-result.json"), encoding="utf-8") as handle:
                    result = json.load(handle)
            except (OSError, ValueError):
                continue
            if result.get("Id") == request["Id"]:
                data = result.get("Data") or {}
                time.sleep(0.1)
                return float(data.get("Length") or 0) if data.get("Reached") else None
    raise SystemExit("No answer from the server. Is the scrim server running?")


def from_segment(point, a, b):
    (px, py), (ax, ay), (bx, by) = point, a, b
    length = (bx - ax) ** 2 + (by - ay) ** 2
    t = 0 if length == 0 else max(0, min(1, ((px - ax) * (bx - ax) + (py - ay) * (by - ay)) / length))
    return math.dist(point, (ax + t * (bx - ax), ay + t * (by - ay)))


def component(nodes, edges):
    """The largest connected piece."""
    left, best = set(nodes), set()
    while left:
        piece, edge = set(), [left.pop()]
        while edge:
            node = edge.pop()
            piece.add(node)
            for a, b in edges:
                other = b if a == node else a if b == node else None
                if other is not None and other in left:
                    left.discard(other)
                    edge.append(other)
        if len(piece) > len(best):
            best = piece
    return best


def compass(point, ox, oy, size):
    col, row = min(2, int((point[0] - ox) * 3 / size)), min(2, int((point[1] - oy) * 3 / size))
    return [["north-west", "north", "north-east"], ["west", "middle", "east"], ["south-west", "south", "south-east"]][row][col]


def main():
    runtime, zone_id, out = sys.argv[1], int(sys.argv[2]), sys.argv[3]
    db = sqlite3.connect("file:" + os.path.join(runtime, "data", "opendaoc.sqlite3.db").replace("\\", "/") + "?mode=ro", uri=True)
    name, region, ox, oy, width = db.execute("select Name, RegionID, OffsetX, OffsetY, Width from Zones where ZoneID = ?", (zone_id,)).fetchone()
    ox, oy, size = ox * 8192, oy * 8192, width * 8192
    keeps = [(keep, (x, y), level) for keep, x, y, level in
             db.execute("select Name, X, Y, BaseLevel from Keep where Region = ?", (region,)) if ox <= x < ox + size and oy <= y < oy + size]
    home = HOME[region]

    def good(a, b, most):
        length = route(runtime, region, a, b)
        return length if length is not None and 0.8 * math.dist(a, b) <= length <= most * math.dist(a, b) else None

    # 1. The ground.
    margin = (size - (size // STEP) * STEP) // 2 + STEP // 2
    grid = [(x, y) for x in range(ox + margin, ox + size, STEP) for y in range(oy + margin, oy + size, STEP)]
    kept = out + ".ground.json"
    if os.path.exists(kept):
        short = [(tuple(a), tuple(b)) for a, b in json.load(open(kept, encoding="utf-8"))]
    else:
        short = [(a, b) for a in grid for b in grid if a < b and math.dist(a, b) <= STEP * 1.1 and good(a, b, 1.15) is not None]
        json.dump(short, open(kept, "w", encoding="utf-8"))
    ground = component(grid, short)
    short = [(a, b) for a, b in short if a in ground and b in ground]
    degree = {point: sum(1 for leg in short if point in leg) for point in ground}

    # 2. The points, and 3. the legs.
    middle = (ox + size / 2, oy + size / 2)
    asked = {}

    def layout(spacing):
        points = []
        for point in sorted(ground, key=lambda p: (-degree[p], math.dist(p, middle))):
            if degree[point] >= 3 and all(math.dist(point, keep[1]) >= 9000 for keep in keeps) and all(math.dist(point, other) >= spacing for other in points) and len(points) < MOST:
                points.append(point)
        legs = {}
        for a, b in itertools.combinations(points, 2):
            if math.dist(a, b) <= LONGEST and all(from_segment(keep[1], a, b) >= 3500 for keep in keeps):
                if (a, b) not in asked:
                    asked[(a, b)] = good(a, b, 1.13)
                if asked[(a, b)] is not None:
                    legs[(a, b)] = asked[(a, b)]
        for a, b in list(legs):
            if any(c not in (a, b) and math.dist(a, c) + math.dist(c, b) <= 1.08 * math.dist(a, b) for c in points):
                del legs[(a, b)]
        points = sorted(component(points, list(legs)))
        return points, {leg: length for leg, length in legs.items() if leg[0] in points and leg[1] in points}

    for spacing in (SPACING, 15000, 13000):
        points, legs = layout(spacing)
        if len(points) >= 7 and len(legs) > len(points):
            break

    # 4. The starts: the realm of the zone in front of a keep, the others at walkable places off the legs.
    def off_legs(point):
        return min(from_segment(point, a, b) for a, b in legs) if legs else 0

    def firsts(start):
        reached = []
        for point in sorted(points, key=lambda p: math.dist(p, start))[:4]:
            length = route(runtime, region, start, point)
            if length is not None and length <= 1.3 * math.dist(start, point):
                reached.append((length, point))
        return [point for _, point in sorted(reached)[:2]]

    doors = []
    for keep, at, level in keeps:
        for turn in range(12):
            spot = (int(at[0] + math.cos(turn * math.pi / 6) * 2800), int(at[1] + math.sin(turn * math.pi / 6) * 2800))
            to = min(points, key=lambda p: math.dist(p, spot))
            length = route(runtime, region, spot, to)
            if length is not None and length <= 1.25 * math.dist(spot, to):
                doors.append((length, keep, spot, level))
    # (One door a keep: the one with the shortest way out. A relic keep only when there is no other.)
    by_keep = {}
    for length, keep, spot, level in sorted(doors):
        by_keep.setdefault(keep, (spot, level))
    homes = [(keep, spot) for keep, (spot, level) in by_keep.items() if level < 60] or [(keep, spot) for keep, (spot, level) in by_keep.items()]
    def camps_off(least):
        return [point for point in ground if point not in points and degree[point] >= 2 and off_legs(point) >= least and
                all(math.dist(point, keep[1]) >= 8000 for keep in keeps)]

    camps = camps_off(6000) if len(camps_off(6000)) >= 6 else camps_off(4500)
    best = None
    for (keep, door), (a, b) in itertools.product(homes, itertools.combinations(camps, 2)):
        apart = min(math.dist(door, a), math.dist(door, b), math.dist(a, b))
        if best is None or apart > best[0]:
            best = (apart, keep, door, a, b)
    if best is None:
        raise SystemExit(f"{name}: no three places to start from ({len(homes)} keeps with a way out, {len(camps)} places off the legs)")
    _, home_keep, door, a, b = best
    # Which of the two others is whose: Hibernia the western one in Albion, the northern one in Midgard; Midgard north of Albion in Hibernia.
    others = [realm for realm in ("Albion", "Midgard", "Hibernia") if realm != home]
    if home == "Albion":
        first, second = ("Hibernia", "Midgard")
        a, b = sorted((a, b))
    else:
        first, second = ("Hibernia", "Albion") if home == "Midgard" else ("Midgard", "Albion")
        a, b = sorted((a, b), key=lambda p: p[1])
    starts = [dict(realm=home, at=door, keep=home_keep, first=firsts(door)),
              dict(realm=first, at=a, keep=None, first=firsts(a)), dict(realm=second, at=b, keep=None, first=firsts(b))]

    # The names, and the quiet point: the one farthest from where anybody comes in.
    names, used = {}, set()
    for keep, at, _ in keeps:
        nearest = min(points, key=lambda p: math.dist(p, at))
        if math.dist(nearest, at) <= 12000 and nearest not in names:
            names[nearest] = keep.replace("Dun ", "").replace("Caer ", "").replace("Castle ", "").replace(" Faste", "")
    for point in points:
        if point not in names:
            word = compass(point, ox, oy, size)
            names[point] = word if word == "middle" else word + " field"
        while names[point] in used:
            names[point] += "*"   # to be told apart by hand
        used.add(names[point])
    quiet = max(points, key=lambda p: min(math.dist(p, start["at"]) for start in starts))

    result = dict(zone=zone_id, name=name, region=region, offset=[ox, oy], map=f"z{zone_id:03d}.dds",
                  points=[dict(at=list(point), name=names[point], quiet=point == quiet) for point in points],
                  ways=[[points.index(a), points.index(b)] for a, b in sorted(legs)],
                  lengths=[round(legs[leg]) for leg in sorted(legs)],
                  starts=[dict(realm=start["realm"], at=list(start["at"]), keep=start["keep"], first=[points.index(p) for p in start["first"]]) for start in starts],
                  keeps=[dict(name=keep, at=list(at)) for keep, at, _ in keeps])
    with open(out, "w", encoding="utf-8") as handle:
        json.dump(result, handle, indent=1)
    print(f"{name}: {len(ground)} of {len(grid)} grid points are one piece of ground, {len(points)} points {spacing:,} apart, {len(legs)} legs, "
          f"starts {best[0]:,.0f} apart at the least ({home_keep}), {CALLS} ways asked -> {out}")
    for start in result["starts"]:
        if not start["first"]:
            print(f"  WARNING: {start['realm']} reaches no point from {start['at']}")


if __name__ == "__main__":
    main()
