"""What a set of weights did, team by team, in a comparison batch (pvp_batch.py --versus).

    python pvp_versus.py <scrims folder> --batch <prefix>[,<prefix>...] [--classes Shaman,Healer]

In a comparison every match has one team on the new weights and one on the old, so a count
over the match as a whole (pvp_referee.py) mixes the two. This counts each team's own doings
and prints new beside old: wins, what happened to fighters that reached its healers and casters,
how its back line spent its time, its fighters' endurance and styles, its deaths, and how often
its order changed. --classes adds what those classes cast, a fight.

Small samples swing: two dozen team-fights a side are a check, not a result.
"""
import collections
import glob
import json
import math
import os
import statistics
import sys

SOFT = {"Cleric", "Friar", "Druid", "Bard", "Healer", "Shaman", "Sorcerer", "Theurgist", "Cabalist", "Wizard",
        "Runemaster", "Spiritmaster", "Eldritch", "Enchanter", "Mentalist"}
HEALERS = {"Cleric", "Druid", "Healer", "Shaman", "Bard", "Friar"}
MOVES = ("Approach", "GetAway", "StandOff", "Spread", "Kite", "Follow", "CloseUp", "FallBack")


def read(path):
    rows = []
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            if line.strip():
                rows.append(json.loads(line))
    return rows


def main(argv):
    if len(argv) < 4 or argv[2] != "--batch":
        print(__doc__)
        return 2
    folder, prefixes = argv[1], argv[3].split(",")
    classes = argv[argv.index("--classes") + 1].split(",") if "--classes" in argv else []
    files = sorted(path for prefix in prefixes for path in glob.glob(os.path.join(folder, prefix + "*.jsonl")))
    stats = {name: collections.defaultdict(list) for name in ("new", "old")}
    casts = {name: collections.Counter() for name in ("new", "old")}
    matches = 0
    for path in files:
        rows = read(path)
        if not rows or rows[0].get("k") != "match" or not rows[0].get("setA"):
            continue
        head = rows[0]
        end = next((row for row in rows if row.get("k") == "end"), None)
        hits = [row for row in rows if row.get("k") == "hit"]
        if end is None or not hits:
            continue
        matches += 1
        bots = {bot["n"]: bot for bot in head["bots"]}
        sets = {"A": head["setA"], "B": head["setB"]}
        group = {"A": head["a"], "B": head["b"]}
        contact = hits[0]["t"]
        per = collections.defaultdict(dict)
        for row in rows:
            if row.get("k") == "snap" and row.get("n") in bots:
                per[(row["t"] - contact) // 1000][row["n"]] = row

        def soft(name):
            return bots[name]["cls"] in SOFT

        for team in "AB":
            mine = stats[sets[team]]
            mine["fights"].append(1)
            mine["wins"].append(1 if end.get("Winner") == group[team] else 0)
            # Fighters of the other team that reach this team's healers and casters in the first 25 seconds.
            arrived = set()
            for second in range(-10, 26):
                now = per.get(second, {})
                for name, snap in now.items():
                    if bots[name]["team"] == team or soft(name) or not snap["alive"] or name in arrived:
                        continue
                    aim = snap.get("aim") or snap.get("target")
                    if aim not in bots or bots[aim]["team"] != team or not soft(aim) or aim not in now or not now[aim]["alive"]:
                        continue
                    if math.hypot(snap["x"] - now[aim]["x"], snap["y"] - now[aim]["y"]) > 350:
                        continue
                    arrived.add(name)
                    first = next((later - second for later in range(second, second + 16)
                                  if name in per.get(later, {}) and (per[later][name]["mez"] or per[later][name]["stun"] or per[later][name]["root"])), None)
                    mine["dive stopped in 3 s"].append(1 if first is not None and first <= 3 else 0)
                    mine["dive stopped in 6 s"].append(1 if first is not None and first <= 6 else 0)
            # How the back line spent the seconds it was alive and free, and the fighters their endurance.
            for second, now in per.items():
                if second < 0:
                    continue
                for name, snap in now.items():
                    if bots[name]["team"] != team or not snap["alive"]:
                        continue
                    if soft(name):
                        if snap.get("mez") or snap.get("stun"):
                            continue
                        mine["back line casting"].append(1 if snap.get("casting") else 0)
                        mine["back line walking"].append(1 if snap.get("moving") and not snap.get("casting") else 0)
                    else:
                        mine["fighter under 20% endurance"].append(1 if snap.get("end", 100) < 20 else 0)
            melee = [row for row in hits if row.get("a") in bots and bots[row["a"]]["team"] == team and not soft(row["a"])
                     and not row.get("pet") and str(row.get("type", "")).startswith("Melee")]
            if melee:
                mine["melee swings with a style"].append(sum(1 for row in melee if row.get("style")) / len(melee))
            deaths = [row for row in rows if row.get("k") == "death" and row.get("n") in bots and bots[row["n"]]["team"] == team and not row.get("pet")]
            mine["deaths"].append(len(deaths))
            healer_deaths = [(row["t"] - contact) / 1000 for row in deaths if bots[row["n"]]["cls"] in HEALERS]
            mine["healer deaths"].append(len(healer_deaths))
            if healer_deaths:
                mine["first healer death (s)"].append(min(healer_deaths))
            orders = []
            for row in rows:
                if row.get("k") == "squad" and row.get("team") == team and row.get("order") and contact <= row["t"] <= contact + 60_000:
                    if not orders or orders[-1] != row["order"]:
                        orders.append(row["order"])
            mine["order changes in the first minute"].append(max(0, len(orders) - 1))
            picks = collections.Counter()
            for row in rows:
                if row.get("k") == "dec" and row.get("n") in bots and bots[row["n"]]["team"] == team and soft(row["n"]) \
                        and contact <= row["t"] <= contact + 60_000:
                    picks[(row.get("pick") or {}).get("kind")] += 1
            for kind in MOVES:
                mine["back line moves in the first minute: " + kind].append(picks[kind])
            for row in rows:
                if row.get("k") == "cast" and row.get("n") in bots and bots[row["n"]]["team"] == team and not row.get("pet") \
                        and bots[row["n"]]["cls"] in classes and row["t"] >= contact:
                    casts[sets[team]][(bots[row["n"]]["cls"], row.get("st"))] += 1

    print(f"{matches} comparison matches from {', '.join(prefixes)}")
    if not matches:
        return 1
    fights = {name: len(stats[name]["fights"]) for name in stats}
    print(f"{'':52} {'new':>10} {'old':>10}")
    print(f"{'team-fights':52} {fights['new']:>10} {fights['old']:>10}")
    shares = {"wins", "dive stopped in 3 s", "dive stopped in 6 s", "back line casting", "back line walking",
              "fighter under 20% endurance", "melee swings with a style"}
    keys = [key for key in stats["new"] if key != "fights"] + [key for key in stats["old"] if key not in stats["new"] and key != "fights"]
    for key in keys:
        cells = []
        for name in ("new", "old"):
            values = stats[name].get(key, [])
            if not values:
                cells.append("-")
            elif key in shares:
                cells.append(f"{100 * sum(values) / len(values):.0f}%" + (f" of {len(values)}" if key.startswith("dive") or key == "wins" else ""))
            elif key.endswith("(s)"):
                cells.append(f"{statistics.median(values):.0f}")
            else:
                cells.append(f"{sum(values) / len(values):.1f}")
        print(f"{key:52} {cells[0]:>10} {cells[1]:>10}")
    if classes:
        print("\ncasts a team-fight after contact, by class and kind of spell:")
        for key in sorted(set(casts["new"]) | set(casts["old"])):
            print(f"  {key[0] + ' ' + str(key[1]):50} {casts['new'][key] / max(1, fights['new']):>10.1f} {casts['old'][key] / max(1, fights['old']):>10.1f}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
