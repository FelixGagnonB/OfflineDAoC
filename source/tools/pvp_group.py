"""How each group of a recorded evening held together and reacted, fight by fight.

    python pvp_group.py <recording.jsonl> [team name]

For every fight a team of bots was in (twenty seconds or longer), from the recording alone:

  spread     how far the farthest living member was from the middle of the group (the middle
             value over the fight)
  far        the share of the time a living member that is not a healer had no living healer
             of its group within 1,850 units (PvpPositionWeights.HealRange)
  carried    how far the middle of the group ended from where the fight began, and how far of
             that was away from where the enemy stood at the start ("back")
  kite       under the order Extend: how far the group went back in all, away from the enemy
             of that second, and the seconds it was under that order
  legs       the legs back it took as one (bots/pvp/PvpKite.cs), and the most enemies that
             aimed at it against its own side at that moment ("on")
  stale      the longest time on one kill target that did not die of it
  orders     the changes of order a minute
  raised     members raised during the fight

Then the same by kind of team, for fights against as many groups as its own and for fights
against more, and every time a group fell back as one: when, how many aimed at it against
its side, the legs it took, how far its middle went, who died on either side meanwhile and
whether it was wiped within half a minute; every time a group that had lost broke off, and
whether it got away; and what the teams that can fall back dealt and
took a second while they did, while they stood with too many on them, and otherwise. With a
team's name: that team's fights, ten seconds at a time.
"""
import collections
import json
import math
import statistics
import sys

HEAL_RANGE = 1_850
HEALERS = {"Cleric", "Druid", "Healer", "Friar"}


def load(path):
    head, classes = None, {}
    teams, snaps, squads, deaths, raises = (collections.defaultdict(list) for _ in range(5))
    kites, hits, runs = [], [], []
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            kind = line[6:10]
            if kind == "roam":
                head = json.loads(line)
            elif kind == "spaw":
                for bot in json.loads(line)["bots"]:
                    classes[bot["n"]] = bot["cls"]
            elif kind == "team":
                row = json.loads(line)
                teams[row["name"]].append(row)
            elif kind == "snap":
                row = json.loads(line)
                snaps[row["team"]].append((row["t"], row["n"], row["x"], row["y"], row["alive"]))
            elif kind == "squa":
                row = json.loads(line)
                squads[row["team"]].append(row)
            elif kind == "hit\"":
                row = json.loads(line)
                if row.get("at") and row.get("dt") and row["at"] != row["dt"] and row["dmg"] > 0:
                    hits.append((row["t"] // 1000, row["at"], row["dt"], row["dmg"]))
            elif kind == "deat":
                row = json.loads(line)
                deaths[row["team"]].append(row)
            elif kind == "dec\"":
                if '"kind":"Raise"' in line:
                    row = json.loads(line)
                    raises[row["team"]].append(row["t"])
                elif '"kind":"Kite' in line or '"kind":"Cornered"' in line:
                    row = json.loads(line)
                    if row["pick"]["kind"] in ("KiteCalled", "KiteLeg", "KiteEnds", "Cornered"):
                        kites.append((row["t"], row["team"], row["pick"]))
            elif kind == "ev\",":
                row = json.loads(line)
                ev = row.get("ev")
                if isinstance(ev, dict) and ev.get("what") == "join" and head is not None:
                    head["teams"].append({"name": ev["team"], "kind": ev.get("kind"), "realm": ev.get("realm")})
                elif isinstance(ev, dict) and ev.get("what") in ("breakoff", "away", "caught", "wipe"):
                    runs.append((row["t"], ev["team"], ev["what"], ev.get("alive", 0)))
    return head, classes, teams, snaps, squads, deaths, raises, kites, hits, runs


def by_second(rows):
    """second -> {member: (x, y)} of the living, the last sample of each second."""
    out = collections.defaultdict(dict)
    for t, n, x, y, alive in rows:
        if alive:
            out[t // 1000][n] = (x, y)
        else:
            out[t // 1000].pop(n, None)
    return out


def middle(points):
    points = list(points)
    return (sum(p[0] for p in points) / len(points), sum(p[1] for p in points) / len(points)) if points else None


def fights_of(rows):
    """Stretches in which the team was fighting somebody: (first row, last row, every team it fought, the most at once)."""
    fight = None
    for row in rows + [dict(t=10 ** 12, fighting=[], alive=0)]:
        if row["fighting"]:
            if fight is None:
                fight = [row, row, set(), 0]
            fight[1] = row
            fight[2] |= set(row["fighting"])
            fight[3] = max(fight[3], len(row["fighting"]))
        elif fight is not None:
            yield tuple(fight)
            fight = None


def measure(name, first, last, foes, classes, where, squads, deaths, raises, detail=False):
    begin, end = first["t"] // 1000, last["t"] // 1000
    own = where[name]
    spreads, far, counted = [], 0, 0
    start = start_foe = None
    kite, extend_seconds, before = 0.0, 0, None
    order_of = {}
    legs, kites, last_legs, most_on = 0, 0, 0, (0, 0)
    for row in squads.get(name, []):
        if begin <= row["t"] // 1000 <= end:
            order_of[row["t"] // 1000] = row
            now = row.get("legs") or 0
            if now > last_legs:
                legs += now - last_legs
                kites += last_legs == 0
            last_legs = now
            if (row.get("on") or 0) > most_on[0]:
                most_on = (row.get("on") or 0, row.get("side") or 0)
    lines = []
    for second in range(begin, end + 1):
        mine = own.get(second) or {}
        if not mine:
            before = None
            continue
        at = middle(mine.values())
        theirs = middle(point for foe in foes for point in (where[foe].get(second) or {}).values())
        if start is None:
            start, start_foe = at, theirs
        spreads.append(max(math.dist(point, at) for point in mine.values()))
        healers = [point for member, point in mine.items() if classes.get(member) in HEALERS]
        for member, point in mine.items():
            if classes.get(member) in HEALERS or not healers:
                continue
            counted += 1
            if min(math.dist(point, healer) for healer in healers) > HEAL_RANGE:
                far += 1
        order = (order_of.get(second) or {}).get("order")
        if order == "Extend":
            extend_seconds += 1
            if before is not None and theirs is not None:
                away = (before[0] - theirs[0], before[1] - theirs[1])
                length = math.hypot(*away) or 1
                kite += ((at[0] - before[0]) * away[0] + (at[1] - before[1]) * away[1]) / length
        before = at
        if detail and (second - begin) % 10 == 0:
            lines.append(f"    {second:5} s  {order or '-':8} alive {len(mine)}  spread {spreads[-1]:5.0f}  "
                         f"enemy at {(math.dist(at, theirs) if theirs else 0):5.0f}  from the start {math.dist(at, start):5.0f}  "
                         f"on it {(order_of.get(second) or {}).get('on', '-')}/{(order_of.get(second) or {}).get('side', '-')}  "
                         f"legs {(order_of.get(second) or {}).get('legs', '-')}  kill {(order_of.get(second) or {}).get('kill')}")
    if start is None:
        return None
    back = 0.0
    if start_foe is not None:
        away = (start[0] - start_foe[0], start[1] - start_foe[1])
        length = math.hypot(*away) or 1
        back = ((before or start)[0] - start[0]) * away[0] / length + ((before or start)[1] - start[1]) * away[1] / length
    # Kill targets: runs of the same name, and whether the run ended with that enemy dead.
    dead_at = collections.defaultdict(list)
    for foe in foes:
        for row in deaths.get(foe, []):
            dead_at[row["n"]].append(row["t"] // 1000)
    stale, run, since, changes, last_order = 0, None, begin, 0, None
    seconds = sorted(order_of)
    for second in seconds + [end + 1]:
        row = order_of.get(second) or {}
        if second <= end and row.get("order") != last_order:
            changes += last_order is not None
            last_order = row.get("order")
        kill = row.get("kill") if second <= end else None
        if kill != run:
            if run is not None and not any(since <= when <= second + 2 for when in dead_at.get(run, [])):
                stale = max(stale, second - since)
            run, since = kill, second
    return dict(team=name, begin=begin, seconds=end - begin, foes=len(foes), most=None, alive=(first["alive"], last["alive"]),
                spread=statistics.median(spreads), far=far / counted if counted else 0.0,
                carried=math.dist(before or start, start), back=back, kite=kite, extend=extend_seconds, stale=stale,
                changes=changes * 60 / max(1, end - begin), legs=legs, kites=kites, on=most_on,
                raised=sum(1 for t in raises.get(name, []) if begin <= t // 1000 <= end), lines=lines)


def main():
    head, classes, teams, snaps, squads, deaths, raises, kites, hits, runs = load(sys.argv[1])
    only = sys.argv[2] if len(sys.argv) > 2 else None
    kinds = {team["name"]: team.get("kind") or "-" for team in head["teams"]}
    where = {name: by_second(rows) for name, rows in snaps.items()}
    results = []
    for name, rows in teams.items():
        if only and name != only:
            continue
        for first, last, foes, most in fights_of(rows):
            if last["t"] - first["t"] < 20_000:
                continue
            got = measure(name, first, last, sorted(foes), classes, where, squads, deaths, raises, detail=bool(only))
            if got:
                got["most"] = most
                results.append(got)
    if not results:
        print("No fight of twenty seconds or more.")
        return
    header = f"{'team':28} {'kind':7} {'at':>5} {'s':>4} {'vs':>2} {'alive':>6} {'spread':>6} {'far':>4} {'carried':>7} {'back':>6} {'kite':>6} {'ext s':>5} {'stale':>5} {'ord/min':>7} {'raised':>6} {'legs':>4} {'on':>6}"
    if only:
        print(header)
    for got in sorted(results, key=lambda got: (got["team"], got["begin"])):
        if only:
            print(f"{got['team'][:28]:28} {kinds.get(got['team'], '-')[:7]:7} {got['begin']:5} {got['seconds']:4} {got['most']:2} "
                  f"{got['alive'][0]}->{got['alive'][1]:<3} {got['spread']:6.0f} {got['far']:4.0%} {got['carried']:7.0f} {got['back']:6.0f} "
                  f"{got['kite']:6.0f} {got['extend']:5} {got['stale']:5} {got['changes']:7.1f} {got['raised']:6} {got['legs']:4} {got['on'][0]:3}/{got['on'][1]:<2}")
            print("\n".join(got["lines"]))
    if only:
        return

    def table(title, rows):
        print(f"\n{title}")
        print(f"  {'kind':8} {'fights':>6} {'lasted':>6} {'spread':>6} {'far':>5} {'carried':>7} {'back':>6} {'under Extend':>12} {'kite':>6} {'stale':>5} {'ord/min':>7} {'raised':>6} {'fell back':>9} {'legs':>5}")
        groups = collections.defaultdict(list)
        for got in rows:
            groups[kinds.get(got["team"], "-")].append(got)
            groups["all"].append(got)
        for kind in sorted(groups, key=lambda kind: (kind == "all", kind)):
            of = groups[kind]
            med = lambda key: statistics.median(got[key] for got in of)
            total = sum(got["seconds"] for got in of)
            print(f"  {kind:8} {len(of):6} {med('seconds'):6.0f} {med('spread'):6.0f} {sum(got['far'] * got['seconds'] for got in of) / max(1, total):5.0%} "
                  f"{med('carried'):7.0f} {med('back'):6.0f} {sum(got['extend'] for got in of) / max(1, total):12.0%} {med('kite'):6.0f} "
                  f"{med('stale'):5.0f} {med('changes'):7.1f} {sum(got['raised'] for got in of) / len(of):6.1f} "
                  f"{sum(1 for got in of if got['legs']):9} {sum(got['legs'] for got in of):5}")

    print(f"{len(results)} fights of twenty seconds or more. The middle value of each measure, by kind of team "
          f"(far and under Extend: of all the fighting time; raised: a fight; fell back: the fights in which the group went back as one, and the legs in all).")
    table("Against one group at the most at a time", [got for got in results if got["most"] <= 1])
    table("Against two groups or more at a time", [got for got in results if got["most"] >= 2])
    kite_list(kites, kinds, teams, where, deaths)
    run_list(runs, kinds, where)
    pressure(squads, hits, teams)


def run_list(runs, kinds, where):
    """Every time a group that had lost broke off, and how it ended."""
    begun = [(t, team, alive) for t, team, what, alive in runs if what == "breakoff"]
    print(f"\nBroke off: {len(begun)} times.")
    if not begun:
        return
    print(f"  {'team':28} {'kind':7} {'at':>5} {'left':>4} {'ran s':>5} {'ran':>6} {'ended'}")
    ends = collections.Counter()
    for t, team, alive in begun:
        end = min(((when, what, left) for when, who, what, left in runs if who == team and what != "breakoff" and when >= t), default=None)
        how = {"away": "got away", "caught": "turned to fight", "wipe": "caught and wiped"}.get(end[1], "?") if end else "still running"
        ends[how] += 1
        own = where.get(team, {})
        a = middle((own.get(t // 1000) or {}).values())
        b = None
        for second in range((end[0] if end else t) // 1000, t // 1000 - 1, -1):
            b = middle((own.get(second) or {}).values())
            if b:
                break
        print(f"  {team[:28]:28} {kinds.get(team, '-')[:7]:7} {t // 1000:5} {alive:4} {((end[0] - t) // 1000 if end else 0):5} "
              f"{(math.dist(a, b) if a and b else 0):6.0f} {how}{f' with {end[2]}' if end and end[1] == 'away' else ''}")
    print("  " + ", ".join(f"{how}: {count}" for how, count in ends.most_common()))


def pressure(squads, hits, teams):
    """What a second costs and brings a group that falls back, one that stands with too many on it, and one in an even fight."""
    by_second = {team: {row["t"] // 1000: row for row in rows} for team, rows in squads.items()}
    fighting = {team: {row["t"] // 1000 for row in rows if row["fighting"]} for team, rows in teams.items()}
    able = {team for team, rows in by_second.items() if any("on" in row for row in rows.values())}

    def state(team, second):
        row = by_second.get(team, {}).get(second)
        if row is None or second not in fighting.get(team, ()) or "on" not in row:
            return None
        if row.get("kite"):
            return "falling back"
        if row["on"] * 100 >= max(1, row.get("side") or 1) * 125:
            return "standing, too many on it"
        return "otherwise"

    seconds, dealt, taken = collections.Counter(), collections.Counter(), collections.Counter()
    for team in able:
        for second in by_second[team]:
            what = state(team, second)
            if what:
                seconds[what] += 1
    for second, by, on, damage in hits:
        if state(by, second):
            dealt[state(by, second)] += damage
        if state(on, second):
            taken[state(on, second)] += damage
    if not seconds:
        return
    print("\nA second of a group's fight (every team that counts what is on it):")
    for what in ("falling back", "standing, too many on it", "otherwise"):
        if seconds[what]:
            print(f"  {what:26} {seconds[what]:6} s   dealt {dealt[what] / seconds[what]:5.0f}   taken {taken[what] / seconds[what]:5.0f}")


def kite_list(kites, kinds, teams, where, deaths):
    """Every time a group fell back as one."""
    calls = [(t, team, pick) for t, team, pick in kites if pick["kind"] == "KiteCalled"]
    cornered = collections.Counter(team for t, team, pick in kites if pick["kind"] == "Cornered")
    print(f"\nFell back as one: {len(calls)} times, {len({team for _, team, _ in calls})} teams. With no ground behind it: {sum(cornered.values())} times.")
    if not calls:
        return
    print(f"  {'team':28} {'kind':7} {'at':>5} {'on it':>6} {'legs':>4} {'for s':>5} {'went':>5} {'alive':>6} {'its dead':>8} {'their dead':>10} {'then'}")
    all_deaths = [row for rows in deaths.values() for row in rows]
    for t, team, pick in calls:
        end = min((when for when, who, what in kites if who == team and what["kind"] == "KiteEnds" and when > t), default=None)
        legs = max([what.get("legs", 1) for when, who, what in kites if who == team and what["kind"] in ("KiteCalled", "KiteLeg") and t <= when <= (end or t + 600_000)], default=1)
        rows = teams.get(team, [])
        last = max((row["t"] for row in rows), default=t)
        end = end or last
        own = where.get(team, {})
        a, b = middle((own.get(t // 1000) or {}).values()), None
        for second in range(end // 1000, t // 1000 - 1, -1):
            b = middle((own.get(second) or {}).values())
            if b:
                break
        alive = lambda at: next((row["alive"] for row in rows if row["t"] >= at), 0)
        mine = sum(1 for row in all_deaths if row["team"] == team and t <= row["t"] <= end)
        theirs = sum(1 for row in all_deaths if row.get("byTeam") == team and t <= row["t"] <= end)
        wiped = any(row["alive"] == 0 for row in rows if end <= row["t"] <= end + 30_000)
        print(f"  {team[:28]:28} {kinds.get(team, '-')[:7]:7} {t // 1000:5} {pick.get('score', 0):3}/{pick.get('side', 0):<2} {legs:4} {(end - t) // 1000:5} "
              f"{(math.dist(a, b) if a and b else 0):5.0f} {alive(t)}->{alive(end):<3} {mine:8} {theirs:10} {'wiped' if wiped else 'lives'}")


if __name__ == "__main__":
    main()
