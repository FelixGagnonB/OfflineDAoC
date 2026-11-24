"""Reads the recording of a prime-time evening (docs/PVP-8V8-ROADMAP.md, M11).

    python pvp_prime.py <recording.jsonl>

Prints, per team: its kind, whom it walks with, how far from that team it was (the middle
value and the share of the time within its keeping distance), its fights, adds, kills, deaths
and wipes; then the draws by kind (how often each kind joined a fight it came on), the fights
by how many teams were in them, the same by zone with the kinds of team that were in each, the
legs walked, and what was said in realm chat.
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
    positions = collections.defaultdict(dict)  # second -> team -> (x, y, state, alive, fighting, zone)
    last = 0
    where, on_road, begun = {}, collections.Counter(), collections.Counter()   # team -> zone now; (zone, team) -> seconds; zone -> fights begun
    for row in rows:
        kind = row.get("k")
        if kind == "team":
            positions[row["t"] // 1000][row["name"]] = (row["x"], row["y"], row["state"], row["alive"], tuple(row.get("fighting") or ()), row.get("zone"))
            last = max(last, row["t"] // 1000)
            where[row["name"]] = row.get("zone")
            if row["state"] in ("Roaming", "Fighting") and row["alive"] > 0:
                on_road[(row.get("zone"), row["name"])] += 1
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
                begun[where.get(ev["team"])] += 1
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
    sizes, zone_sizes = collections.Counter(), collections.defaultdict(collections.Counter)
    for second, teams in positions.items():
        seen = set()
        for name, state in teams.items():
            if name in seen or not state[4]:
                continue
            group, queue = {name}, [name]
            while queue:
                current = queue.pop()
                for other in teams.get(current, (0, 0, "", 0, (), None))[4]:
                    if other not in group:
                        group.add(other)
                        queue.append(other)
            seen |= group
            sizes[len(group)] += 1
            zone_sizes[state[5]][min(len(group), 5)] += 1
    total = sum(sizes.values())
    print("\nSeconds of fighting by the number of teams in the fight:")
    for size in sorted(sizes):
        print(f"  {size} teams: {sizes[size]:5} s ({sizes[size] / total:.0%})")

    # By zone: a team is of the zone it was on the road in the longest.
    home = {}
    for (zone, name), seconds in on_road.items():
        if zone is not None and seconds > home.get(name, (None, 0))[1]:
            home[name] = (zone, seconds)
    if len({zone for zone, _ in home.values()}) > 1:
        print("\nBy zone (the teams that were there the longest; fights begun; seconds of fighting by teams in the fight):")
        for zone in sorted({zone for zone, _ in home.values()}):
            there = [name for name, (its, _) in home.items() if its == zone]
            by_kind = collections.Counter(kinds.get(name, "-") for name in there)
            by_realm = collections.Counter(realms.get(name, "?")[:3] for name in there)
            fought = sum(zone_sizes[zone].values())
            split = ", ".join(f"{size}{'+' if size == 5 else ''}: {count / fought:.0%}" for size, count in sorted(zone_sizes[zone].items())) if fought else "no fighting"
            print(f"  {zone:10} {len(there):2} teams ({', '.join(f'{kind} {count}' for kind, count in sorted(by_kind.items()))}; "
                  f"{', '.join(f'{realm} {count}' for realm, count in sorted(by_realm.items()))}); {begun[zone]:3} fights; {split}")

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
