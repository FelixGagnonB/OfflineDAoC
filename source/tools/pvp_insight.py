"""Read recorded scrim matches as a coach would watch them (docs/PVP-8V8-ROADMAP.md, M6).

    python pvp_insight.py <folder or .jsonl files> [--batch <prefix>] [--since <prefix>] [--until <prefix>]
                          [--stories] [--weights]

--batch keeps the files whose name starts with the prefix; --since keeps those named at or after
it (names start with the batch's date and time, so --since 20261119-01 is "from one o'clock UTC
on": a night's run made of several batches); --until keeps those named before it.

Three things the per-build report of pvp_analyze.py does not give:

1. The story of each fight. From the three readings (fighters held, casters free to cast,
   healers free to heal) and the numbers alive, second by second, it finds the turning point: the
   moment after which the winner stayed ahead for good. It then says what the loser's state was
   in the ten seconds before it, and labels the fight with what went wrong. --stories prints the
   paragraph for every match; without it only the summary over the batch is printed.

2. Missed chances. Situations where something was there to be done and nobody did it, counted in
   seconds per match for each group: an enemy healer free to cast with an idle member in range,
   a trained member with no heal landing while a healer was free, a pursuer that could be rooted
   with a free rooter in range, an enemy that could be mezzed with a free mezzer in range.

   The first death is read the same way: who died first, how many were on it, what its healers
   were doing in its last ten seconds and whether heals reached it. The side that loses the
   first member nearly always loses the fight, so this is where most fights are decided.

3. Which weights matter. In a batch run with Vary=50 (pvp_batch.py) every team of every match
   fought with its own draw of the weights, some moved a step down and some a step up. For each
   weight this compares how the teams did with it down, unmoved and up, after taking out what
   the pairing of realms explains by itself, and lists the weights by how surely they moved the
   result. --weights prints this part alone and reads only the first and last line of each
   file, so it is quick on a large batch.

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


def first_death(match):
    """The first member to die: who, when, who was on it, and what its healers did in its last ten seconds."""
    bots, contact, rows = match["bots"], match["contact"], match["rows"]
    deaths = [r for r in rows if r["k"] == "death" and r["n"] in bots and r["t"] >= contact]
    if not deaths:
        return None
    died = min(deaths, key=lambda r: r["t"])
    victim, at = died["n"], died["t"]
    team = bots[victim]["team"]
    since = at - 10_000
    window = [r for r in rows if "t" in r and since <= r["t"] <= at]
    hits = [r for r in window if r["k"] == "hit" and r.get("d") == victim and not r.get("dpet") and r.get("dmg", 0) > 0]
    damage = sum(r["dmg"] for r in hits)
    attackers = {(r["a"], bool(r.get("pet"))) for r in hits}
    heals = [r for r in window if r["k"] == "heal" and r.get("d") == victim and not r.get("dpet") and r.get("asked", 0) >= 100 and r["n"] != victim]
    healed = sum(r["got"] for r in heals)
    healers = {n for n, b in bots.items() if b["team"] == team and b["cls"] in HEALERS and n != victim}
    asleep = busy = clear = far = total = 0
    last = {}
    held = own = 0
    for r in window:
        if r["k"] != "snap":
            continue
        if r["n"] == victim:
            own += 1
            held += bool(r["mez"] or r["stun"])
            last[victim] = r
        elif r["n"] in healers:
            total += 1
            if not r["alive"]:
                continue
            if r["mez"] or r["stun"]:
                asleep += 1
            elif r.get("intr") or r.get("sil"):
                busy += 1
            else:
                clear += 1
                if victim in last and math.dist((r["x"], r["y"]), (last[victim]["x"], last[victim]["y"])) > 1700:
                    far += 1
    trained = sum(1 for r in rows if r["k"] == "squad" and r["team"] == team and r.get("trained") == victim and r["t"] <= at)
    share = lambda part: part / total if total else 0.0
    if not healers or total == 0:
        why = "it had no healer left" if not healers else "nothing recorded of its healers"
    elif share(asleep) >= 0.5:
        why = "its healers were asleep or stunned"
    elif share(asleep) + share(busy) >= 0.6:
        why = "its healers were kept from casting"
    elif clear and far / clear >= 0.6:
        why = "it was out of its healers' reach"
    elif not heals:
        why = "its healers were free and no heal came"
    elif healed * 2 < damage:
        why = "it was healed and out-damaged"
    else:
        why = "it was healed and died all the same"
    kind = "healer" if bots[victim]["cls"] in HEALERS else "caster" if bots[victim]["cls"] in SOFT else "fighter"
    group = match["head"]["a"] if team == "A" else match["head"]["b"]
    text = (f"First death: {group} {bots[victim]['build']} at {(at - contact) // 1000} s, trained for {trained} s, {len(attackers)} on it in its last ten seconds "
            f"({damage} damage taken, {len(heals)} heals for {healed}); its healers: {share(asleep):.0%} asleep or stunned, {share(busy):.0%} interrupted, "
            f"{share(clear):.0%} free" + (f" ({far / clear:.0%} of that out of reach)" if clear else "")
            + (f"; it was asleep or stunned {held / own:.0%} of that time" if own and held else "") + f". Read as: {why}.")
    return {"text": text, "why": why, "group": group, "build": bots[victim]["build"], "kind": kind, "at": (at - contact) // 1000,
            "attackers": len(attackers), "held": own and held / own >= 0.3}


def sleeps(match):
    """Every mez a member sat through: how long it lasted and what ended it."""
    bots, contact, rows = match["bots"], match["contact"], match["rows"]
    spans, asleep = [], {}
    for r in rows:
        if r["k"] != "snap" or r["n"] not in bots:
            continue
        n, mezzed = r["n"], bool(r["mez"]) and r["alive"]
        if mezzed and n not in asleep:
            asleep[n] = [r["t"], r["t"]]
        elif mezzed:
            asleep[n][1] = r["t"]
        elif n in asleep:
            spans.append((n, *asleep.pop(n)))
    spans += [(n, a, b) for n, (a, b) in asleep.items()]
    wakes = [(r["t"], r["n"]) for r in rows if r["k"] == "wake"]
    cures = [(r["t"], r["d"]) for r in rows if r["k"] == "cast" and r.get("st") == "CureMezz"]
    deaths = {r["n"]: r["t"] for r in rows if r["k"] == "death"}
    out = []
    for n, start, end in spans:
        near = lambda events, slack: any(who == n and end - slack <= at <= end + slack for at, who in events)
        ended = ("died asleep" if n in deaths and abs(deaths[n] - end) <= 1500 else "cured by a groupmate" if near(cures, 1500)
                 else "woken by an enemy's hit" if near(wakes, 1500) else "ran out or was purged")
        b = bots[n]
        out.append({"group": match["head"]["a"] if b["team"] == "A" else match["head"]["b"], "healer": b["cls"] in HEALERS,
                    "kind": "healer" if b["cls"] in HEALERS else "caster" if b["cls"] in SOFT else "fighter",
                    "seconds": (end - start) / 1000 + 1, "ended": ended, "opening": start - contact <= 15_000})
    return out


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


def head_and_end(path):
    """The first line of a recording and its "end" line, without reading what lies between."""
    with open(path, "rb") as handle:
        first = handle.readline()
        handle.seek(0, os.SEEK_END)
        size = handle.tell()
        handle.seek(max(0, size - 4096))
        tail = handle.read().decode("utf-8", "replace").splitlines()
    try:
        head = json.loads(first)
    except ValueError:
        return None, None
    if head.get("k") != "match":
        return None, None
    for line in reversed(tail):
        if line.startswith('{"k":"end"'):
            return head, json.loads(line)
    return head, None


def draws(head, end):
    """The two teams of a match that drew their weights: what each drew and how it did."""
    if head is None or end is None:
        return []
    a, b = (head.get("weights") or {}).get("Drawn"), (head.get("weightsB") or {}).get("Drawn")
    if a is None or b is None:
        return []
    out = []
    for team, own, enemy, drawn, sign in (("A", head["a"], head["b"], a, 1), ("B", head["b"], head["a"], b, -1)):
        bots = [bot for bot in head["bots"] if bot["team"] == team]
        winner = end.get("Winner")
        out.append({"group": own, "enemy": enemy, "drawn": drawn,
                    "has": {bot["cls"] for bot in bots} | {bot["build"] for bot in bots},
                    "win": 1.0 if winner == own else 0.0 if winner == enemy else 0.5,
                    "margin": sign * (end["AliveA"] - end["AliveB"])})
    return out


def weights_report(teams, top=30):
    if not teams:
        print("No match in this batch drew its weights (run pvp_batch.py with Vary=50).")
        return
    # What the pairing explains by itself is taken out first: Albion beating Hibernia says nothing about a weight.
    cells = collections.defaultdict(list)
    for team in teams:
        cells[(team["group"], team["enemy"])].append(team)
    for members in cells.values():
        for key in ("win", "margin"):
            mean = sum(m[key] for m in members) / len(members)
            for m in members:
                m[key + "_left"] = m[key] - mean
    paths = sorted({p for team in teams for p in team["drawn"]})
    rows = []
    for p in paths:
        parts = p.split(".")
        # A class's or a build's own weight is judged on the teams that have that class or build.
        needs = parts[1] if parts[0] in ("ClassBase", "BuildLean", "HealsFirstBelow") and len(parts) > 1 else None
        sample = [team for team in teams if needs is None or needs in team["has"]]
        xs = [team["drawn"].get(p, 0) for team in sample]
        n = len(sample)
        mean_x = sum(xs) / n if n else 0
        var_x = sum((x - mean_x) ** 2 for x in xs)
        if n < 20 or var_x == 0:
            continue
        by = {level: [team for team, x in zip(sample, xs) if x == level] for level in (-1, 0, 1)}
        win = {level: (50 + 100 * sum(m["win_left"] for m in group) / len(group)) if group else None for level, group in by.items()}
        slope, sure = {}, {}
        for key in ("win_left", "margin_left"):
            ys = [team[key] for team in sample]
            mean_y = sum(ys) / n
            slope[key] = sum((x - mean_x) * (y - mean_y) for x, y in zip(xs, ys)) / var_x
            left = sum((y - mean_y - slope[key] * (x - mean_x)) ** 2 for x, y in zip(xs, ys)) / max(1, n - 2)
            sure[key] = slope[key] / math.sqrt(left / var_x) if left > 0 else 0.0
        rows.append((p, n, win, slope, sure))
    matches = len(teams) // 2
    if not rows:
        print(f"Which weights moved the results: {matches} matches drew their weights, too few to compare "
              "(a weight needs twenty team draws, and several hundred matches before anything shows).")
        return
    by_chance = max(1, round(len(rows) * 0.05))
    print(f"Which weights moved the results: {matches} matches, {len(teams)} team draws, {len(rows)} weights.")
    if matches < 300:
        print(f"  WARNING: {matches} matches are too few. Below a few hundred this table is noise; read it for the method only.")
    print("  'win' is the share of fights won with the weight down, unmoved and up, on a scale where 50 is")
    print("  what the pairing of realms gives by itself. 'members' is how many more members a team had left")
    print("  at the end for each step up. 'sure' is that effect in units of its own uncertainty: beyond 2 or")
    print(f"  -2 it is unlikely to be chance for one weight alone, but among {len(rows)} weights about")
    print(f"  {by_chance} pass 2 by chance and fewer than one passes 3.\n")
    cell = lambda value: "   -" if value is None else f"{value:4.0f}"
    print(f"  {'weight':44}{'teams':>6}  {'win: down':>9} {'same':>4} {'up':>4}  {'members':>8} {'sure':>6}")
    rows.sort(key=lambda row: -abs(row[4]["margin_left"]))
    for p, n, win, slope, sure in rows[:top]:
        print(f"  {p:44}{n:6}  {cell(win[-1]):>9} {cell(win[0])} {cell(win[1])}  {slope['margin_left']:+8.2f} {sure['margin_left']:+6.1f}")
    strong = sum(1 for row in rows if abs(row[4]["margin_left"]) >= 2)
    print(f"\n  {strong} of {len(rows)} weights pass 2; chance alone would give about {by_chance}.")
    moved = [len(team["drawn"]) for team in teams]
    print(f"  A team had {sum(moved) / len(moved):.0f} weights moved on average.")


def main(argv):
    args = [a for a in argv[1:] if not a.startswith("--")]
    batch = argv[argv.index("--batch") + 1] if "--batch" in argv else None
    since = argv[argv.index("--since") + 1] if "--since" in argv else None
    until = argv[argv.index("--until") + 1] if "--until" in argv else None
    args = [a for a in args if a not in (batch, since, until)]
    if not args:
        print(__doc__)
        return 2
    files = []
    for a in args:
        files += sorted(glob.glob(os.path.join(a, "*.jsonl"))) if os.path.isdir(a) else [a]
    if batch:
        files = [f for f in files if os.path.basename(f).startswith(batch)]
    if since:
        files = [f for f in files if os.path.basename(f) >= since]
    if until:
        files = [f for f in files if os.path.basename(f) < until]
    teams = []
    for f in files:
        teams += draws(*head_and_end(f))
    if "--weights" in argv:
        weights_report(teams)
        return 0
    # One match in memory at a time: a recording is about two megabytes and a night's batch has hundreds.
    stories, played_matches = [], 0
    firsts, slept = [], []
    total, played = collections.defaultdict(collections.Counter), collections.Counter()
    for f in files:
        m = load(f)
        if not m:
            continue
        played_matches += 1
        s = story(m)
        if s:
            stories.append((None, s))
        f = first_death(m)
        if f:
            firsts.append(f)
            if s:
                s["text"] += " " + f["text"]
        slept += sleeps(m)
        found = chances(m)
        for team, group in (("A", m["head"]["a"]), ("B", m["head"]["b"])):
            total[group].update(found[team])
            played[group] += 1
    if not played_matches:
        print("No finished recorded matches found.")
        return 1
    print(f"{played_matches} matches, {len(stories)} with a winner.\n")
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

    if firsts:
        print(f"\nWhy the first member died ({len(firsts)} fights):")
        for why, count in collections.Counter(f["why"] for f in firsts).most_common():
            print(f"  {why:42} {count:4}  {100 * count / len(firsts):3.0f}%")
        times = sorted(f["at"] for f in firsts)
        on_it = sorted(f["attackers"] for f in firsts)
        print(f"  It died {times[len(times) // 2]} s after contact (median) with {on_it[len(on_it) // 2]} enemies on it; "
              f"{100 * sum(1 for f in firsts if f['held']) / len(firsts):.0f}% were asleep or stunned for a third or more of their last ten seconds.")
        print("  Who dies first, by group:")
        by_group = collections.defaultdict(collections.Counter)
        for f in firsts:
            by_group[f["group"]][f["build"]] += 1
        for group, builds in sorted(by_group.items()):
            deaths = sum(builds.values())
            print(f"    {group} ({deaths}): " + ", ".join(f"{build} {100 * count / deaths:.0f}%" for build, count in builds.most_common(5)))
        print("  Why, by group:")
        by_group = collections.defaultdict(collections.Counter)
        for f in firsts:
            by_group[f["group"]][f["why"]] += 1
        for group, whys in sorted(by_group.items()):
            print(f"    {group}: " + ", ".join(f"{why} {count}" for why, count in whys.most_common(4)))

    if slept:
        print("\nMezzes sat through, by group (per match; 'opening' is within 15 s of contact):")
        print(f"  {'':10}{'healers asleep':>15}{'in the opening':>15}{'median length':>15}   what ended a healer's sleep")
        for group in sorted(played):
            mine = [s for s in slept if s["group"] == group and s["healer"]]
            if not mine:
                print(f"  {group:10}{0:15.1f}")
                continue
            lengths = sorted(s["seconds"] for s in mine)
            ended = collections.Counter(s["ended"] for s in mine)
            print(f"  {group:10}{len(mine) / played[group]:15.1f}{sum(1 for s in mine if s['opening']) / played[group]:15.1f}{lengths[len(lengths) // 2]:13.0f} s   "
                  + ", ".join(f"{why} {100 * count / len(mine):.0f}%" for why, count in ended.most_common()))
        everyone = collections.defaultdict(float)
        for s in slept:
            everyone[(s["group"], s["kind"])] += s["seconds"]
        print("  seconds asleep a match, all members: " + "; ".join(
            f"{group}: " + ", ".join(f"{kind}s {everyone[(group, kind)] / played[group]:.0f}" for kind in ("healer", "caster", "fighter")) for group in sorted(played)))

    print("\nMissed chances, seconds a match, by group:")
    keys = ["enemy healer free", "enemy healer free, a member idle in range", "pursuer on the backline",
            "pursuer rootable, a rooter free in range", "enemy mezzable, a mezzer free in range",
            "trained 3 s and more with no heal", "trained with no heal, a healer free in range"]
    print(f"  {'':46}" + "".join(f"{group:>12}" for group in sorted(total)))
    for key in keys:
        print(f"  {key:46}" + "".join(f"{total[group][key] / played[group]:12.0f}" for group in sorted(total)))
    if teams:
        print()
        weights_report(teams)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
