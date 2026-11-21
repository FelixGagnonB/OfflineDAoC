#!/usr/bin/env python3
"""Counts a recorded roam (docs/PVP-8V8-ROADMAP.md, M9.7) and cuts its fights out as matches.

A roam is one recording for a whole session with any number of teams (logs/roams, format 4:
see bots/pvp/PvpRecorder.cs). This prints what happened in it: the events in order, every fight
(who, where, how long, who died, how it ended) and a line for each team. With --cut each fight of
exactly two teams is also written as a match recording (format 3), which pvp_replay.py,
pvp_referee.py and pvp_analyze.py read like any scrim.

    python pvp_roam.py <recording.jsonl>                 the newest in a folder when given a folder
    python pvp_roam.py <recording.jsonl> --cut [folder]  default: "cuts" beside the recording
"""
import collections
import json
import os
import sys

GAP = 20_000        # a fight is over when its teams have not hit each other for this long (ms)
LEAD_IN = 15_000    # a cut starts this long before the first blow
MIN_HITS = 6        # fewer blows than this between two teams is not a fight


def load(path):
    rows = []
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError:
                break  # the last line of a roam still being written
    if not rows or rows[0].get("k") != "roam":
        sys.exit("%s is not the recording of a roam." % path)
    return rows


def clock(ms):
    seconds = int(ms // 1000)
    return "%d:%02d" % (seconds // 60, seconds % 60)


def find_fights(rows, realm):
    """Teams hitting each other, joined into fights: a blow belongs to a fight one of its teams is in."""
    fights, active = [], []
    for row in rows:
        if row["k"] != "hit":
            continue
        a, d = row.get("at"), row.get("dt")
        if not a or not d or a == d or realm.get(a) == realm.get(d):
            continue
        t = row["t"]
        active = [fight for fight in active if fight["last"] >= t - GAP]
        touching = [fight for fight in active if a in fight["teams"] or d in fight["teams"]]
        if not touching:
            fight = {"start": t, "last": t, "teams": {}, "hits": 0, "pairs": collections.Counter()}
            active.append(fight)
            fights.append(fight)
        else:
            fight = touching[0]
            for other in touching[1:]:
                fight["start"] = min(fight["start"], other["start"])
                fight["hits"] += other["hits"]
                fight["pairs"] += other["pairs"]
                for team, since in other["teams"].items():
                    fight["teams"][team] = min(since, fight["teams"].get(team, since))
                active.remove(other)
                fights.remove(other)
        for team in (a, d):
            fight["teams"].setdefault(team, t)
        fight["last"] = t
        fight["hits"] += 1
        fight["pairs"][tuple(sorted((a, d)))] += 1
    return [fight for fight in fights if fight["hits"] >= MIN_HITS]


def alive_at(snaps, team, t):
    """How many of a team were alive in its last rows at or before a moment."""
    latest = {}
    for row in snaps.get(team, ()):
        if row["t"] > t:
            break
        latest[row["n"]] = row
    return sum(1 for row in latest.values() if row["alive"]), latest


def describe(fights, rows, snaps):
    deaths = [row for row in rows if row["k"] == "death"]
    for fight in fights:
        start, end = fight["start"], fight["last"]
        fight["deaths"] = collections.Counter(row["team"] for row in deaths
                                              if start <= row["t"] <= end + 3_000 and row["team"] in fight["teams"])
        fight["alive"], xs, ys = {}, [], []
        for team, since in fight["teams"].items():
            fight["alive"][team], _ = alive_at(snaps, team, end + 3_000)
            # Where it began: the teams of the first blows, not one that came a minute later from elsewhere.
            if since > start + 5_000:
                continue
            _, at_start = alive_at(snaps, team, start)
            xs += [row["x"] for row in at_start.values()]
            ys += [row["y"] for row in at_start.values()]
        fight["at"] = (int(sum(xs) / len(xs)), int(sum(ys) / len(ys))) if xs else None
        standing = [team for team in fight["teams"] if fight["alive"][team] > 0]
        fight["wiped"] = [team for team in fight["teams"] if fight["alive"][team] == 0]
        fight["end"] = "wipe" if fight["wiped"] else "broke off"
        fight["standing"] = standing


def report(path, rows, fights):
    head = rows[0]
    last = max((row["t"] for row in rows if "t" in row), default=0)
    end = rows[-1] if rows[-1].get("k") == "end" else None
    print("%s: %s, %s%s" % (os.path.basename(path), head["label"], clock(last), "" if end else " (still running, or stopped without an end)"))
    print("Teams: " + ", ".join("%s%s" % (team["name"], " (the player: %s)" % team["player"] if team.get("player") else "") for team in head["teams"]))

    print("\nWhat happened")
    for row in rows:
        if row["k"] == "ev" and row.get("text"):
            print("  %6s  %s" % (clock(row["t"]), row["text"]))

    print("\nFights (%d)" % len(fights))
    for number, fight in enumerate(fights, 1):
        first = sorted(fight["teams"], key=fight["teams"].get)
        opening = [team for team in first if fight["teams"][team] <= fight["start"] + 5_000]
        later = [team for team in first if team not in opening]
        line = "  %2d. %6s  %-5s  %s" % (number, clock(fight["start"]), clock(fight["last"] - fight["start"]), " against ".join(opening))
        if later:
            line += "; then " + ", ".join("%s (+%ds)" % (team, (fight["teams"][team] - fight["start"]) // 1000) for team in later)
        print(line)
        print("        deaths: " + ", ".join("%s %d" % (team, fight["deaths"][team]) for team in first) +
              "; left standing: " + (", ".join("%s %d" % (team, fight["alive"][team]) for team in fight["standing"]) or "nobody") +
              ("; at %d,%d" % fight["at"] if fight["at"] else ""))

    print("\nTeams")
    paid = collections.Counter()
    for row in rows:
        if row["k"] == "ev" and (row.get("ev") or {}).get("what") == "pay":
            paid[row["ev"]["to"]] += row["ev"]["share"] * row["ev"]["members"]
    kills = collections.Counter(row.get("byTeam") for row in rows if row["k"] == "death" and row.get("byTeam") and row.get("byTeam") != row["team"])
    died = collections.Counter(row["team"] for row in rows if row["k"] == "death")
    wipes = collections.Counter((row.get("ev") or {}).get("team") for row in rows if row["k"] == "ev" and (row.get("ev") or {}).get("what") == "wipe")
    fighting = collections.Counter()
    for fight in fights:
        for team, since in fight["teams"].items():
            fighting[team] += fight["last"] - since
    print("  %-14s %6s %10s %6s %6s %6s %9s %8s" % ("team", "fights", "in a fight", "kills", "deaths", "wipes", "points", "a member"))
    for team in head["teams"]:
        name = team["name"]
        members = sum(1 for bot in head["bots"] if bot["team"] == name) or 1
        print("  %-14s %6d %10s %6d %6d %6d %9d %8d" % (
            name, sum(1 for fight in fights if name in fight["teams"]), clock(fighting[name]),
            kills[name], died[name], wipes[name], paid[name], paid[name] // members))
    two = sum(1 for fight in fights if len(fight["teams"]) == 2)
    print("\n%d of %d fights were between two teams only%s." % (two, len(fights), "" if not fights else "; the largest had %d" % max(len(fight["teams"]) for fight in fights)))


# The keys of a line that hold a team's name.
TEAM_KEYS = ("team", "at", "dt", "byTeam")


def cut(path, rows, fights, snaps, folder):
    """Each fight of two teams as a match recording: side A is the team that was there first."""
    head = rows[0]
    os.makedirs(folder, exist_ok=True)
    stem = os.path.splitext(os.path.basename(path))[0]
    group = {team["name"]: team["name"] for team in head["teams"]}
    written = []
    for number, fight in enumerate(fights, 1):
        if len(fight["teams"]) != 2:
            continue
        a, b = sorted(fight["teams"], key=fight["teams"].get)
        side = {a: "A", b: "B"}
        begin, end = max(0, fight["start"] - LEAD_IN), fight["last"] + 3_000
        # Who is in each team when the fight starts: the header's members, or those of its last standing up before it.
        members = {}
        for bot in head["bots"]:
            if bot["team"] in side:
                members.setdefault(bot["team"], {})[bot["n"]] = bot
        for row in rows:
            if row["k"] == "spawn" and row["team"] in side and row["t"] <= fight["start"]:
                members[row["team"]] = {bot["n"]: bot for bot in row["bots"]}
        names = {name for team in members.values() for name in team}
        out = [{
            "k": "match", "v": 3, "batch": stem + "-fight", "number": number, "utc": head["utc"],
            "realmLevel": max((bot.get("rl") or 1) for team in members.values() for bot in team.values()),
            "a": group[a], "b": group[b], "weights": head.get("weights"), "weightsB": None, "setA": None, "setB": None,
            "roam": os.path.basename(path), "roamStart": fight["start"],
            "bots": [dict(bot, team=side[team]) for team in (a, b) for bot in members[team].values()],
        }]
        for row in rows[1:]:
            t = row.get("t")
            if t is None or t < begin or t > end or row["k"] in ("team", "ev", "spawn", "end"):
                continue
            # A line of these two teams only; somebody of a third team in it keeps the name and loses the team, as a guard does.
            own = [row.get(key) for key in TEAM_KEYS if row.get(key)]
            if row["k"] == "snap":
                if row["n"] not in names:
                    continue
            elif not any(team in side for team in own):
                continue
            line = dict(row, t=t - begin)
            for key in TEAM_KEYS:
                if key in line and line[key] is not None:
                    line[key] = side.get(line[key])
            if row["k"] == "snap":
                line.pop("team", None)
            elif line.get("team") is None and "team" in row and row["k"] in ("squad", "dec", "call", "cast", "begin", "stop"):
                continue
            out.append(line)
        alive_a, alive_b = fight["alive"][a], fight["alive"][b]
        out.append({
            "k": "end", "t": end - begin,
            "Winner": "draw" if alive_a == alive_b else group[a] if alive_a > alive_b else group[b],
            "End": fight["end"], "Seconds": (end - begin) // 1000, "SecondsToContact": (fight["start"] - begin) // 1000,
            "AliveA": alive_a, "AliveB": alive_b,
        })
        name = os.path.join(folder, "%s-fight-%02d-%s-vs-%s.jsonl" % (stem, number, a.replace("#", "-"), b.replace("#", "-")))
        with open(name, "w", encoding="utf-8") as handle:
            for line in out:
                handle.write(json.dumps(line, separators=(",", ":")) + "\n")
        written.append(name)
    return written


def main():
    args = [arg for arg in sys.argv[1:]]
    if not args:
        sys.exit(__doc__)
    path = args[0]
    if os.path.isdir(path):
        found = sorted(name for name in os.listdir(path) if name.endswith(".jsonl"))
        if not found:
            sys.exit("No recording in %s." % path)
        path = os.path.join(path, found[-1])
    rows = load(path)
    realm = {team["name"]: team["realm"] for team in rows[0]["teams"]}
    snaps = collections.defaultdict(list)
    for row in rows:
        if row["k"] == "snap":
            snaps[row["team"]].append(row)
    fights = find_fights(rows, realm)
    describe(fights, rows, snaps)
    report(path, rows, fights)
    if "--cut" in args:
        at = args.index("--cut")
        folder = args[at + 1] if at + 1 < len(args) else os.path.join(os.path.dirname(os.path.abspath(path)), "cuts")
        written = cut(path, rows, fights, snaps, folder)
        print("\n%d fights of two teams written to %s" % (len(written), folder))
        for name in written:
            print("  " + os.path.basename(name))


if __name__ == "__main__":
    main()
