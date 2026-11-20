"""How well a group's damage follows its own kill call. Usage: count_focus.py <label> <glob> [<glob> ...]"""
import bisect
import collections
import glob
import json
import sys

MELEE = {"Savage", "Berserker", "Warrior", "Skald", "Armsman", "Paladin", "Minstrel", "Hero", "Blademaster"}
HEAL = {"Cleric", "Druid", "Healer", "Shaman", "Bard"}


def kind(cls):
    return "fighter" if cls in MELEE else "healer" if cls in HEAL else "caster"


def dmg(r):
    return (r.get("dmg") or 0) + (r.get("crit") or 0)


def run(files, label, sets=False):
    acc = collections.defaultdict(lambda: collections.defaultdict(float))
    lens = collections.defaultdict(list)
    dist = collections.defaultdict(collections.Counter)
    n = 0
    for f in files:
        rows = [json.loads(l) for l in open(f, encoding="utf-8")]
        if not any(r.get("k") == "end" for r in rows):
            continue
        hits = [r for r in rows if r.get("k") == "hit"]
        if not hits:
            continue
        n += 1
        head = rows[0]
        B = {b["n"]: b for b in head["bots"]}
        first = hits[0]["t"]
        key = {"A": (head.get("setA") or "all") if sets else "all", "B": (head.get("setB") or "all") if sets else "all"}
        times = {"A": [], "B": []}
        kills = {"A": [], "B": []}
        for r in rows:
            if r.get("k") == "squad" and r["t"] >= first:
                times[r["team"]].append(r["t"])
                kills[r["team"]].append(r.get("kill"))

        def kill_at(team, t):
            i = bisect.bisect_right(times[team], t) - 1
            return kills[team][i] if i >= 0 else None

        deaths = {r["n"]: r["t"] for r in rows if r.get("k") == "death"}
        for team in "AB":
            a = acc[key[team]]
            prev = None
            start = None
            for t, k in zip(times[team], kills[team]):
                a["sec"] += 1
                a["nokill"] += k is None
                if k != prev:
                    if prev is not None:
                        a["calls"] += 1
                        d = deaths.get(prev)
                        a["killed"] += d is not None and start <= d <= t + 1500
                        lens[key[team]].append((t - start) / 1000)
                    prev, start = k, t
            a["teams"] += 1
        win = collections.defaultdict(set)
        for r in hits:
            if r.get("a") not in B or r.get("d") not in B:
                continue
            who = B[r["a"]]
            if who["team"] == B[r["d"]]["team"] or who.get("player"):
                continue
            k = kill_at(who["team"], r["t"])
            d = dmg(r)
            if d <= 0 or k is None:
                continue
            a = acc[key[who["team"]]]
            c = "pet" if r.get("pet") else kind(who["cls"])
            a["dmg"] += d
            a["dmg_on"] += d if r["d"] == k else 0
            a["dmg:" + c] += d
            a["on:" + c] += d if r["d"] == k else 0
            win[(who["team"], (r["t"] - first) // 10000)].add(r["d"])
        for (team, _), v in win.items():
            dist[key[team]][min(len(v), 6)] += 1
        for r in rows:
            if r.get("k") == "snap" and r["t"] >= first and r["alive"] and r["n"] in B and not B[r["n"]].get("player"):
                b = B[r["n"]]
                k = kill_at(b["team"], r["t"])
                if k is None or r.get("target") not in B or B[r["target"]]["team"] == b["team"]:
                    continue
                a = acc[key[b["team"]]]
                a["tsec:" + kind(b["cls"])] += 1
                a["ton:" + kind(b["cls"])] += r["target"] == k
            if r.get("k") == "cast" and "Resurrect" in (r.get("st") or "") and r.get("n") in B:
                acc[key[B[r["n"]]["team"]]]["rez"] += 1
            if r.get("k") == "death" and r.get("n") in B:
                acc[key[B[r["n"]]["team"]]]["deaths"] += 1
        end = [r for r in rows if r.get("k") == "end"][0]
        for team, alive in (("A", end["AliveA"]), ("B", end["AliveB"])):
            acc[key[team]]["wins"] += (end["Winner"] == head["a" if team == "A" else "b"])
    print("== %s: %d matches" % (label, n))
    for name, a in acc.items():
        t = max(1, a["teams"])
        print("  [%s] %d teams, won %d" % (name, a["teams"], a["wins"]))
        print("    damage landing on the group's own kill target: %.0f%%" % (100 * a["dmg_on"] / max(1, a["dmg"])),
              {c: "%.0f%%" % (100 * a["on:" + c] / max(1, a["dmg:" + c])) for c in ("fighter", "caster", "healer", "pet")})
        print("    seconds with an enemy targeted that it is the kill target:",
              {c: "%.0f%%" % (100 * a["ton:" + c] / max(1, a["tsec:" + c])) for c in ("fighter", "caster", "healer")})
        L = sorted(lens[name]) or [0]
        print("    kill calls %.1f a team; ended with the target dead %.0f%%; median length %.0f s; no kill target %.0f%% of the time" % (
            a["calls"] / t, 100 * a["killed"] / max(1, a["calls"]), L[len(L) // 2], 100 * a["nokill"] / max(1, a["sec"])))
        tw = sum(dist[name].values()) or 1
        print("    enemies a group damages in any 10 s:", {k: "%.0f%%" % (100 * v / tw) for k, v in sorted(dist[name].items())})
        print("    deaths %.1f a team, resurrections cast %.2f a team" % (a["deaths"] / t, a["rez"] / t))


if __name__ == "__main__":
    label = sys.argv[1]
    sets = "--sets" in sys.argv
    files = sorted(f for g in sys.argv[2:] if not g.startswith("--") for f in glob.glob(g))
    run(files, label, sets)
