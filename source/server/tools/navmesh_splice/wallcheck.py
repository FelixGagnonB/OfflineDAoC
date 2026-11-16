"""How much of a zone's invisible walls does the installed navmesh let bots walk across?

The client's bound.csv lists each zone's invisible walls. A navmesh built correctly has no
walkable polygon on a wall line. Meshes built before the 0.35 builder fix carry only the first
half of every wall, so bots crossed zone edges and climbed mountains players cannot.

    python wallcheck.py <game folder>\\runtime <zone id> [<zone id> ...]

Reads the database read-only. Exit code 0 when every zone is at most MAX_OPEN_PERCENT open,
1 otherwise. Used by docs/PVP-8V8-ROADMAP.md step M0.3 for the 8v8 arena zone.
"""
import math
import os
import sqlite3
import sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "navmesh_shadow_islands"))
sys.path.insert(0, os.path.join(HERE, "..", "..", "..", "..", "tools", "asset-tool"))
import archive   # noqa: E402
import navparse  # noqa: E402

SAMPLE_STEP = 50          # world units between samples along a wall
GRID = 256                # spatial bucket size for polygons
DISABLED = 0x10           # the only poly flag the server's default filter excludes
MAX_OPEN_PERCENT = 2.0    # door frames and mesh rounding leave a little; a broken mesh shows 50+


def read_walls(runtime, zone, offset_x, offset_y):
    folder = os.path.join(runtime, "client-opendaoc", "app", "zones", f"zone{zone:03d}")
    for pack in ("dat", "csv"):
        path = os.path.join(folder, f"{pack}{zone:03d}.mpk")
        if not os.path.isfile(path):
            continue
        try:
            _, entries = archive.read(open(path, "rb").read())
        except ValueError:
            continue
        for entry in entries:
            if entry.name.lower() != "bound.csv":
                continue
            walls = []
            for line in entry.data.decode("latin1").splitlines():
                cells = [c for c in line.strip().split(",") if c.strip()]
                if len(cells) < 6:
                    continue
                count = min(int(cells[1]), (len(cells) - 2) // 2)
                walls.append([(float(cells[2 + 2 * i]) + offset_x, float(cells[3 + 2 * i]) + offset_y)
                              for i in range(count)])
            return walls
    return None


def walkable_grid(nav_path):
    grid = defaultdict(list)
    for tile in navparse.load(nav_path):
        for verts, _, flags, _, kind in tile.polys:
            if kind != 0 or flags == DISABLED:
                continue
            poly = [navparse.to_world(tile.verts[v])[:2] for v in verts]
            xs = [p[0] for p in poly]
            ys = [p[1] for p in poly]
            for gx in range(int(min(xs) // GRID), int(max(xs) // GRID) + 1):
                for gy in range(int(min(ys) // GRID), int(max(ys) // GRID) + 1):
                    grid[(gx, gy)].append(poly)
    return grid


def inside(point, poly):
    sign = 0
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        cross = (b[0] - a[0]) * (point[1] - a[1]) - (b[1] - a[1]) * (point[0] - a[0])
        if abs(cross) < 1e-9:
            continue
        if sign == 0:
            sign = 1 if cross > 0 else -1
        elif (cross > 0) != (sign > 0):
            return False
    return True


def check(runtime, zone, db):
    row = db.execute("select OffsetX, OffsetY, Name from Zones where ZoneID=?", (zone,)).fetchone()
    if row is None:
        return f"zone {zone}: not in the database", False
    offset_x, offset_y, name = row[0] * 8192, row[1] * 8192, row[2]
    nav_path = os.path.join(runtime, "server", "navmesh", f"zone{zone:03d}.nav")
    if not os.path.isfile(nav_path):
        return f"zone {zone} {name}: no navmesh file", False
    walls = read_walls(runtime, zone, offset_x, offset_y)
    if not walls:
        return f"zone {zone} {name}: no bound.csv in the client (nothing to check)", True
    grid = walkable_grid(nav_path)
    samples = open_samples = 0
    length = 0.0
    for wall in walls:
        for a, b in zip(wall, wall[1:]):
            segment = math.dist(a, b)
            length += segment
            steps = max(1, int(segment // SAMPLE_STEP))
            for k in range(steps):
                f = (k + 0.5) / steps
                p = (a[0] + f * (b[0] - a[0]), a[1] + f * (b[1] - a[1]))
                samples += 1
                if any(inside(p, poly) for poly in grid.get((int(p[0] // GRID), int(p[1] // GRID)), ())):
                    open_samples += 1
    percent = 100.0 * open_samples / max(1, samples)
    ok = percent <= MAX_OPEN_PERCENT
    return (f"zone {zone} {name}: {len(walls)} walls, {length:,.0f} units, "
            f"{open_samples} of {samples} samples walkable = {percent:.1f}% open  "
            f"[{'OK' if ok else 'BOTS CAN CROSS'}]"), ok


def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return 2
    runtime = argv[1]
    db_path = os.path.join(runtime, "data", "opendaoc.sqlite3.db").replace("\\", "/")
    db = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True)
    all_ok = True
    for zone in argv[2:]:
        message, ok = check(runtime, int(zone), db)
        print(message)
        all_ok &= ok
    return 0 if all_ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
