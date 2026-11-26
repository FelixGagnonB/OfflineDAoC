"""Behind the driver: how evenly the bot that drives the player's group ran, and how its player followed.

    python source/tools/pvp_behind_driver.py <recording.jsonl> [driver name]

Reads a recording of an 8v8 evening (runtime/server/logs/roams) and compares, second by second while
the group is on the road, the driver's speed over the ground with its player's, on the flat and on
slopes. From recordings of 2026-11-26 on it also reads what the player's client said of its speed
within each second (pn, pslow, pfast): a client on /stick that stops and starts says 0 or a low
speed and its full speed in the same second, which a position a second cannot show.
(docs/PVP-8V8-ROADMAP.md, M13.30 and M13.36.)
"""
import collections
import json
import math
import sys


def pct(values, share):
    values = sorted(values)
    return values[min(len(values) - 1, int(share * len(values)))] if values else 0


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    path = sys.argv[1]
    teams, snaps, drivers = {}, collections.defaultdict(dict), collections.Counter()
    name = None
    with open(path, encoding="utf-8") as lines:
        for line in lines:
            if '"k":"team"' in line:
                row = json.loads(line)
                if row.get("px") is not None:
                    name = name or row["name"]
                    if row["name"] == name:
                        teams[row["t"]] = row
            elif '"k":"snap"' in line:
                row = json.loads(line)
                snaps[row["t"]][row["n"]] = row
            elif '"Driver' in line:
                row = json.loads(line)
                drivers[(row.get("team"), row["n"])] += 1
    if name is None:
        print("No team with a player in this recording.")
        return 1
    driver = sys.argv[2] if len(sys.argv) > 2 else next((who for (team, who), _ in drivers.most_common() if team == name), None)
    if driver is None:
        print(f"{name}: no driver found; give its name.")
        return 1
    print(f"{name}, driven by {driver}")

    times = sorted(t for t in teams if driver in snaps.get(t, {}))
    rows = []
    for before, after in zip(times, times[1:]):
        seconds = (after - before) / 1000.0
        if not 0 < seconds <= 1.5:
            continue
        d0, d1, t0, t1 = snaps[before][driver], snaps[after][driver], teams[before], teams[after]
        if t0["state"] != "Roaming" or t1["state"] != "Roaming" or not (d0.get("moving") and d1.get("moving")):
            continue
        rows.append({
            "t": after / 1000.0, "x": d1["x"], "y": d1["y"],
            "driver": math.hypot(d1["x"] - d0["x"], d1["y"] - d0["y"]) / seconds,
            "player": math.hypot(t1["px"] - t0["px"], t1["py"] - t0["py"]) / seconds,
            "climb": (d1["z"] - d0["z"]) / seconds,
            "apart": math.hypot(t1["px"] - d1["x"], t1["py"] - d1["y"]),
            "follows": bool(t0.get("pmoving") and t1.get("pmoving")),
            "gap": t1.get("gap") or 0, "pn": t1.get("pn"), "pslow": t1.get("pslow"), "pfast": t1.get("pfast"),
            "pmax": t1.get("pmax"), "pace": t1.get("pace"),
        })
    both = [row for row in rows if row["follows"]]
    print(f"{len(rows)} seconds with the driver on the move, {len(both)} of them with its player on the move too")

    def show(label, chosen):
        if not chosen:
            return
        drv, plr, apart = [r["driver"] for r in chosen], [r["player"] for r in chosen], [r["apart"] for r in chosen]
        slow = sum(1 for r in chosen if r["player"] < 0.75 * r["driver"])
        print(f"  {label:22} {len(chosen):5} s  driver {pct(drv, .1):4.0f}/{pct(drv, .5):4.0f}/{pct(drv, .9):4.0f}"
              f"  player {pct(plr, .1):4.0f}/{pct(plr, .5):4.0f}/{pct(plr, .9):4.0f}  (10%/median/90%)"
              f"  player under 3/4 of the driver: {slow}  apart {pct(apart, .5):4.0f}/{pct(apart, .9):4.0f}")

    show("all", both)
    show("flat (under 40 a s)", [r for r in both if abs(r["climb"]) < 40])
    show("slope (40 to 120)", [r for r in both if 40 <= abs(r["climb"]) < 120])
    show("steep, up", [r for r in both if r["climb"] >= 120])
    show("steep, down", [r for r in both if r["climb"] <= -120])

    print("\nSeconds in which its player was under 3/4 of the driver's speed (the driver over 300):")
    for r in [r for r in both if r["driver"] > 300 and r["player"] < 0.75 * r["driver"]][:60]:
        print(f"  {r['t']:7.0f} s  driver {r['driver']:4.0f}  player {r['player']:4.0f}  climb {r['climb']:5.0f}  apart {r['apart']:5.0f}"
              f"  silence {r['gap']} ms  at {r['x']},{r['y']}  pace {r['pace']}")

    measured = [r for r in both if r["pn"]]
    if not measured:
        print("\n(No speeds of the player's client within a second in this recording: it is from before 2026-11-26.)")
        return 0
    # A stop and start within a second: its client said a speed under half of its fastest in that second.
    stutter = [r for r in measured if r["pfast"] and r["pslow"] < 0.5 * r["pfast"]]
    print(f"\nWhat its client said within a second ({len(measured)} seconds): a stop and start (slowest under half of the fastest) in {len(stutter)}")
    for label, chosen in (("flat", [r for r in measured if abs(r["climb"]) < 40]), ("slope", [r for r in measured if 40 <= abs(r["climb"]) < 120]),
                          ("steep", [r for r in measured if abs(r["climb"]) >= 120])):
        if chosen:
            share = sum(1 for r in chosen if r in stutter) * 100.0 / len(chosen)
            print(f"  {label:6} {len(chosen):5} s  with a stop and start: {share:4.1f}%   packets a second {pct([r['pn'] for r in chosen], .5)}")
    for r in stutter[:60]:
        print(f"  {r['t']:7.0f} s  client {r['pslow']}..{r['pfast']} of {r['pmax']}  driver {r['driver']:4.0f}  climb {r['climb']:5.0f}"
              f"  apart {r['apart']:5.0f}  silence {r['gap']} ms  pace {r['pace']}  at {r['x']},{r['y']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
