"""Read recorded scrim matches as a coach would watch them (docs/PVP-8V8-ROADMAP.md, M6).

    python pvp_insight.py <folder or .jsonl files> [--batch <prefix>] [--stories]

Two things the per-build report of pvp_analyze.py does not give:

1. The story of each fight. From the three readings (fighters held, casters free to cast,
   healers free to heal) and the numbers alive, second by second, it finds the turning point: the
   moment after which the winner stayed ahead for good. It then says what the loser's state was
   in the ten seconds before it, and labels the fight with what went wrong. --stories prints the
   paragraph for every match; without it only the summary over the batch is printed.

2. Missed chances. Situations where something was there to be done and nobody did it, counted in
   seconds per match for each group: an enemy healer free to cast with an idle member in range,
   a trained member with no heal landing while a healer was free, a pursuer that could be rooted
   with a free rooter in range, an enemy that could be mezzed with a free mezzer in range.

Needs recordings of format 3 (they carry "heal", "squad" and the interrupted flag).
"""
import collections
import glob
import json
import math
import os
import sys

SOFT = {"Cleric", "Friar", "Druid", "Bard", "Healer", "Shaman", "Sorcerer", "Theurgist", "Cabalist", "Wizard",
        "Runemaster", "Spiritmaster", "Eldritch", "Enchanter", "Mentalist"}
HEALERS = {"Cleric", "Friar", "Druid", "Healer"}
MEZZERS = {"Sorcerer", "Bard", "Healer"}
ROOTERS = {"Shaman", "Sorcerer", "Theurgist", "Cabalist", "Runemaster", "Healer", "Eldritch", "Skald"}
REACH = 1500


def load(path):
    rows = [json.loads(line) for line in open(path, encoding="utf-8") if line.strip()]
    if not rows or rows[0].get("k") != "match":
        return None
    head = rows[0]
    end = next((r for r in rows if r["k"] == "end"), None)
    contact = next((r["t"] for r in rows if r["k"] == "snap" and r.get("combat")), None)
    if end is None or contact is None:
        return None
    return {"head": head, "rows": rows, "end": end, "contact": contact, "bots": {b["n"]: b for b in head["bots"]},
            "file": os.path.basename(path)}


def free(snap):
    return snap["alive"] and not (snap["mez"] or snap["stun"] or snap.get("intr") or snap.get("sil"))


def seconds(match):
    """Per second from contact: every bot's first snapshot, and each team's squad line."""
    bots, contact = match["bots"], match["contact"]
    snaps, squads = collections.defaultdict(dict), collections.defaultdict(dict)
    for r in match["rows"]:
        if "t" not in r or r["t"] < contact:
            continue
        second = (r["t"] - contact) // 1000
        if r["k"] == "snap" and r["n"] in bots:
            snaps[second].setdefault(r["n"], r)
        elif r["k"] == "squad":
            squads[second].setdefault(r["team"], r)
    return snaps, squads


def reading(snaps, bots, team):
    mine = {n: s for n, s in snaps.items() if bots[n]["team"] == team}
    alive = {n: s for n, s in mine.items() if s["alive"]}
    fighters = [s for n, s in alive.items() if bots[n]["cls"] not in SOFT]
    casters = [s for n, s in alive.items() if bots[n]["cls"] in SOFT]
    healers = [s for n, s in mine.items() if bots[n]["cls"] in HEALERS]
    share = lambda part, whole: len(part) / len(whole) if whole else 0.0
    return {"alive": len(alive), "asleep": sum(1 for s in alive.values() if s["mez"]),
            "held": share([s for s in fighters if s["mez"] or s["stun"] or s["root"]], fighters),
            "casters": share([s for s in casters if free(s)], casters),
            "healers": share([s for s in healers if free(s)], healers),
            "healers_dead": sum(1 for s in healers if not s["alive"])}


def lead(a, b):
    """How far ahead group a is: numbers first, then healers, casters and fighters."""
    return (a["alive"] - b["alive"]) / 8 + 0.5 * (a["healers"] - b["healers"]) + 0.25 * (a["casters"] - b["casters"]) - 0.25 * (a["held"] - b["held"])


def story(match):
    bots, head, end = match["bots"], match["head"], match["end"]
    snaps, squads = seconds(match)
    winner = "A" if end.get("Winner") == head["a"] else "B" if end.get("Winner") == head["b"] else None
    if winner is None or not snaps:
        return None
    loser = "B" if winner == "A" else "A"
    order = sorted(snaps)
    readings = {s: {t: reading(snaps[s], bots, t) for t in "AB"} for s in order}
    leads = {s: lead(readings[s][winner], readings[s][loser]) for s in order}
    # The turning point: the first second from which the winner is ahead to the end.
    turn = order[-1]
    for s in reversed(order):
        if leads[s] <= 0:
            break
        turn = s
    window = [s for s in order if turn - 10 <= s <= turn]
    mean = lambda key: sum(readings[s][loser][key] for s in window) / len(window)
    deaths = [(r["n"], (r["t"] - match["contact"]) // 1000) for r in match["rows"] if r["k"] == "death" and r["n"] in bots]
    first = min(deaths, key=lambda d: d[1]) if deaths else None
    lost = [n for n, at in deaths if bots[n]["team"] == loser and turn - 10 <= at <= turn + 2]
    labels = []
    if any(bots[n]["cls"] in HEALERS for n in lost):
        labels.append("a healer died")
    if mean("healers") <= 0.5:
        labels.append("healers could not heal")
    if max(readings[s][loser]["asleep"] for s in window) >= 3:
        labels.append("three or more asleep")
    if mean("held") >= 0.5:
        labels.append("fighters held")
    if mean("casters") <= 0.4:
        labels.append("casters could not cast")
    if lost and not any(bots[n]["cls"] in HEALERS for n in lost):
        labels.append("lost a " + ("caster" if bots[lost[0]]["cls"] in SOFT else "fighter"))
    if not labels:
        labels.append("no single cause")
    orders = collections.Counter(squads[s].get(loser, {}).get("order") for s in window)
    trained = collections.Counter(squads[s].get(loser, {}).get("trained") for s in window if squads[s].get(loser, {}).get("trained"))
    name = lambda n: f"{bots[n]['team']} {bots[n]['build']}"
    text = (f"{head['a']} (A) against {head['b']} (B), match {head['number']}: {end['Winner']} won in {end['Seconds']} s, alive {end['AliveA']}/{end['AliveB']}. "
            + (f"First death: {name(first[0])} at {first[1]} s. " if first else "")
            + f"Turning point at {turn} s. In the ten seconds before it the loser had {mean('healers'):.0%} of its healers free, "
              f"{mean('casters'):.0%} of its healers and casters able to cast, {mean('held'):.0%} of its fighters held, up to "
              f"{max(readings[s][loser]['asleep'] for s in window)} asleep"
            + (f"; it lost {', '.join(name(n) for n in lost)}" if lost else "")
            + (f"; most trained: {name(trained.most_common(1)[0][0])}" if trained and trained.most_common(1)[0][0] in bots else "")
            + (f"; its order was mostly {orders.most_common(1)[0][0]}" if orders and orders.most_common(1)[0][0] else "")
            + f". Read as: {', '.join(labels)}.")
    return {"text": text, "labels": labels, "turn": turn, "length": end["Seconds"], "loser": head["a"] if loser == "A" else head["b"],
            "first_death_side": bots[first[0]]["team"] if first else None, "winner_side": winner}


def chances(match):
    """Missed chances, in seconds, for each team."""
    bots = match["bots"]
    snaps, squads = seconds(match)
    out = {t: collections.Counter() for t in "AB"}
    dist = lambda a, b: math.dist((a["x"], a["y"]), (b["x"], b["y"]))
    idle = lambda s: free(s) and not s["casting"] and not s["moving"]
    heals = collections.defaultdict(list)
    for r in match["rows"]:
        if r["k"] == "heal" and r.get("d") in bots and r.get("asked", 0) >= 100:
            heals[r["d"]].append((r["t"] - match["contact"]) // 1000)
    trained_since = {}
    for second in sorted(snaps):
        now = snaps[second]
        alive = {n: s for n, s in now.items() if s["alive"]}
        for team in "AB":
            mine = {n: s for n, s in alive.items() if bots[n]["team"] == team}
            theirs = {n: s for n, s in alive.items() if bots[n]["team"] != team}
            kill = squads[second].get(team, {}).get("kill")
            # An enemy healer free to cast, with a member of ours standing idle in range.
            for n, s in theirs.items():
                if bots[n]["cls"] in HEALERS and free(s):
                    out[team]["enemy healer free"] += 1
                    if any(idle(m) and dist(m, s) <= REACH for m in mine.values()):
                        out[team]["enemy healer free, a member idle in range"] += 1
            # A fighter on our healers and casters that a root would hold, with a rooter free in range.
            for n, s in theirs.items():
                target = alive.get(s.get("target"))
                if (bots[n]["cls"] not in SOFT and not (s["mez"] or s["stun"]) and target is not None and s.get("target") in mine
                        and bots[s["target"]]["cls"] in SOFT and dist(s, target) <= 350):
                    out[team]["pursuer on the backline"] += 1
                    if not (s["root"] or s.get("snareImm")) and any(
                            bots[m]["cls"] in ROOTERS and free(o) and dist(o, s) <= REACH for m, o in mine.items()):
                        out[team]["pursuer rootable, a rooter free in range"] += 1
            # An enemy that could be mezzed, with a mezzer of ours free in range.
            mezzers = [o for m, o in mine.items() if bots[m]["cls"] in MEZZERS and free(o)]
            for n, s in theirs.items():
                if n != kill and not (s["mez"] or s["stun"] or s.get("mezImm")) and any(dist(o, s) <= REACH for o in mezzers):
                    out[team]["enemy mezzable, a mezzer free in range"] += 1
            # Our trained member: how long until a heal lands, and whether a healer was free meanwhile.
            trained = squads[second].get(team, {}).get("trained")
            key = (team, trained)
            if trained and trained in mine:
                trained_since.setdefault(key, second)
                waited = second - trained_since[key]
                healed = any(trained_since[key] <= at <= second for at in heals[trained])
                if not healed and waited >= 3:
                    out[team]["trained 3 s and more with no heal"] += 1
                    if any(bots[m]["cls"] in HEALERS and free(o) and dist(o, mine[trained]) <= 2000 for m, o in mine.items() if m != trained):
                        out[team]["trained with no heal, a healer free in range"] += 1
            for old in [k for k in trained_since if k[0] == team and k != key]:
                del trained_since[old]
    return out


def main(argv):
    args = [a for a in argv[1:] if not a.startswith("--")]
    batch = argv[argv.index("--batch") + 1] if "--batch" in argv else None
    args = [a for a in args if a != batch]
    if not args:
        print(__doc__)
        return 2
    files = []
    for a in args:
        files += sorted(glob.glob(os.path.join(a, "*.jsonl"))) if os.path.isdir(a) else [a]
    if batch:
        files = [f for f in files if os.path.basename(f).startswith(batch)]
    matches = [m for m in (load(f) for f in files) if m]
    if not matches:
        print("No finished recorded matches found.")
        return 1
    stories = [(m, story(m)) for m in matches]
    stories = [(m, s) for m, s in stories if s]
    print(f"{len(matches)} matches, {len(stories)} with a winner.\n")
    if "--stories" in argv:
        for _, s in stories:
            print(s["text"] + "\n")

    print("What went wrong for the loser around the turning point (a fight can carry several):")
    labels = collections.Counter(label for _, s in stories for label in s["labels"])
    for label, count in labels.most_common():
        print(f"  {label:28} {count:4}  {100 * count / len(stories):3.0f}% of fights")
    by_loser = collections.defaultdict(collections.Counter)
    for _, s in stories:
        by_loser[s["loser"]].update(s["labels"])
        by_loser[s["loser"]]["fights lost"] += 1
    for loser, c in sorted(by_loser.items()):
        lost = c.pop("fights lost")
        print(f"  when {loser} lost ({lost}): " + ", ".join(f"{label} {count}" for label, count in c.most_common(4)))
    turns = sorted(s["turn"] for _, s in stories)
    lengths = sorted(s["length"] for _, s in stories)
    took_first = sum(1 for _, s in stories if s["first_death_side"] and s["first_death_side"] != s["winner_side"])
    print(f"\nThe turning point comes at {turns[len(turns) // 2]} s (median) in fights of {lengths[len(lengths) // 2]} s; "
          f"the side that lost the first member lost the fight in {100 * took_first / len(stories):.0f}% of them.")

    print("\nMissed chances, seconds a match, by group:")
    total, played = collections.defaultdict(collections.Counter), collections.Counter()
    for m in matches:
        found = chances(m)
        for team, group in (("A", m["head"]["a"]), ("B", m["head"]["b"])):
            total[group].update(found[team])
            played[group] += 1
    keys = ["enemy healer free", "enemy healer free, a member idle in range", "pursuer on the backline",
            "pursuer rootable, a rooter free in range", "enemy mezzable, a mezzer free in range",
            "trained 3 s and more with no heal", "trained with no heal, a healer free in range"]
    print(f"  {'':46}" + "".join(f"{group:>12}" for group in sorted(total)))
    for key in keys:
        print(f"  {key:46}" + "".join(f"{total[group][key] / played[group]:12.0f}" for group in sorted(total)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
