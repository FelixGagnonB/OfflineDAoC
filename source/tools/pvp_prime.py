"""Reads the recording of a prime-time evening (docs/PVP-8V8-ROADMAP.md, M11).

    python pvp_prime.py <recording.jsonl>

Prints, per team: its kind, whom it walks with, how far from that team it was (the middle
value and the share of the time within its keeping distance), its fights, adds, kills, deaths
and wipes; then the draws by kind (how often each kind joined a fight it came on), the fights
by how many teams were in them, the legs walked, and what was said in realm chat.
"""
import collections
import json
import math
import statistics
import sys


def load(path):
    rows = []
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if line:
                rows.append(json.loads(line))
    return rows


def main():
    rows = load(sys.argv[1])
    head = rows[0]
    kinds = {team["name"]: team.get("kind") or "-" for team in head["teams"]}
    # (The server writes the enum's other names for two of the realms.)
    written = {"_FirstPlayerRealm": "Albion", "_LastPlayerRealm": "Hibernia"}
    realms = {team["name"]: written.get(team["realm"], team["realm"]) for team in head["teams"]}
    company, legs, chat, draws = {}, collections.Counter(), [], collections.defaultdict(lambda: [0, 0])
    fights, wipes, sets_off = collections.Counter(), collections.Counter(), {}
    ports, logoffs, turns = [], [], collections.Counter()
    positions = collections.defaultdict(dict)  # second -> team -> (x, y, state, alive, fighting)
    last = 0
    for row in rows:
        kind = row.get("k")
        if kind == "team":
            positions[row["t"] // 1000][row["name"]] = (row["x"], row["y"], row["state"], row["alive"], tuple(row.get("fighting") or ()))
            last = max(last, row["t"] // 1000)
        elif kind == "ev" and isinstance(row.get("ev"), dict):
            ev = row["ev"]
            what = ev.get("what")
            if what == "company":
                company[ev["team"]] = ev["beside"]
            elif what == "leg":
                legs[ev["team"]] += 1
            elif what == "chat":
                chat.append((row["t"] // 1000, ev["team"], ev["text"]))
            elif what == "draw":
                draws[kinds.get(ev["team"], "-")][0 if ev["joins"] else 1] += 1
            elif what == "fight":
                fights[ev["team"]] += 1
            elif what == "wipe":
                wipes[ev["team"]] += 1
            elif what == "setoff":
                sets_off[ev["team"]] = row["t"] // 1000
            elif what == "port":
                ports.append((row["t"] // 1000, ev["team"], ev["zone"]))
            elif what == "logoff":
                logoffs.append((row["t"] // 1000, ev["team"], ev.get("wipes")))
            elif what == "join":
                kinds[ev["team"]] = ev.get("kind") or "-"
                realms[ev["team"]] = ev.get("realm") or "?"
                if ev.get("beside"):
                    company[ev["team"]] = ev["beside"]
            elif what == "turn":
                turns[ev["team"]] += 1
    end = next((row for row in reversed(rows) if row.get("k") == "end"), None)
    totals = {team["name"]: team for team in (end or {}).get("teams", [])}

    print(f"{len(kinds)} teams, {last} s")
    print(f"{'team':28} {'realm':9} {'kind':7} {'legs':>4} {'fights':>6} {'wipes':>5} {'kills':>5} {'deaths':>6}  walks with (middle distance, share of the time within reach)")
    for name in kinds:
        line = f"{name[:28]:28} {realms[name][:9]:9} {kinds[name]:7} {legs[name]:4} {fights[name]:6} {wipes[name]:5} " \
               f"{totals.get(name, {}).get('Kills', '?'):>5} {totals.get(name, {}).get('Deaths', '?'):>6}"
        if name in company:
            head_team = company[name]
            reach = 2000 if kinds[name] == "Bad" else 4500
            distances = []
            for second, teams in positions.items():
                if name in teams and head_team in teams and second >= sets_off.get(name, 0):
                    mine, theirs = teams[name], teams[head_team]
                    if mine[2] in ("Roaming", "Fighting") and theirs[2] in ("Roaming", "Fighting") and mine[3] > 0 and theirs[3] > 0:
                        distances.append(math.hypot(mine[0] - theirs[0], mine[1] - theirs[1]))
            if distances:
                within = sum(1 for distance in distances if distance <= reach * 1.25) / len(distances)
                line += f"  {head_team} ({statistics.median(distances):,.0f}, {within:.0%} of {len(distances)} s)"
            else:
                line += f"  {head_team} (never both on the road)"
        print(line)

    print("\nDraws on a fight it came on, by kind (joins / leaves it):")
    for kind, (joins, leaves) in sorted(draws.items()):
        print(f"  {kind:7} {joins:3} / {leaves:3}")

    # Fights by how many teams were in them: connected groups of teams fighting each other, second by second.
    sizes = collections.Counter()
    for second, teams in positions.items():
        seen = set()
        for name, state in teams.items():
            if name in seen or not state[4]:
                continue
            group, queue = {name}, [name]
            while queue:
                current = queue.pop()
                for other in teams.get(current, (0, 0, "", 0, ()))[4]:
                    if other not in group:
                        group.add(other)
                        queue.append(other)
            seen |= group
            sizes[len(group)] += 1
    total = sum(sizes.values())
    print("\nSeconds of fighting by the number of teams in the fight:")
    for size in sorted(sizes):
        print(f"  {size} teams: {sizes[size]:5} s ({sizes[size] / total:.0%})")

    if ports or logoffs:
        print(f"\n{len(ports)} ports to another frontier, {len(logoffs)} log-offs:")
        for second, team, zone in ports[:20]:
            print(f"  {second:5} s  {team} ports to {zone}")
        for second, team, count in logoffs[:20]:
            print(f"  {second:5} s  {team} logs off (wipes: {count})")
    if turns:
        print(f"\nTurned away from a fight: {sum(turns.values())} times, {len(turns)} teams")

    print(f"\nRealm chat ({len(chat)} lines):")
    for second, team, text in chat[:60]:
        print(f"  {second:5} s  {team}: {text}")


if __name__ == "__main__":
    main()
