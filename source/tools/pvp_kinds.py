"""What each kind of team did in a recorded evening, to see whether the kinds play differently (docs/PVP-8V8-ROADMAP.md, M12.6).

    python pvp_kinds.py <recording.jsonl> [more recordings]

One line for each kind, over all the seconds its teams were in a fight:

  on call    the share of its damage that landed on the enemy its group had called
  targets    how many different enemies a fighter hit in a minute of fighting
  calls      whom it called: healers, casters, fighters (shares of the time a call stood)
  woken      sleeping enemies it woke, a minute
  in melee   the share of the time one of its healers or casters had an enemy fighter within 350 units
  clump      how far its farthest healer or caster stood from the middle of them (the middle value)
  far        the share of the time a fighter had no healer of its group within 1,850 units
  heals at   the health of the patient when a heal was chosen (the middle value)
  raises     the dead it began to raise, a minute
  peels      the times a shield fighter went for an enemy on its group's back line or set a guard, a minute
  duty       the times somebody was given an enemy to keep from casting, a minute
  mez        the mezzes it chose to cast, a minute
  fell back, broke off, wiped    how often, in all
"""
import collections
import json
import math
import statistics
import sys

HEALERS = {"Cleric", "Druid", "Healer"}
FIGHTERS = {"Armsman", "Paladin", "Mercenary", "Reaver", "Warrior", "Berserker", "Savage", "Thane", "Skald", "Hero", "Champion", "Blademaster",
            "Warden", "Valewalker", "Minstrel", "Friar", "Infiltrator", "Shadowblade", "Nightshade", "Ranger", "Scout", "Hunter"}
ORDER = ["Bad", "Clingy", "Adder", "Decent", "Strong", "Elite", "Hunter"]


def main():
    total = collections.defaultdict(collections.Counter)
    lists = collections.defaultdict(lambda: collections.defaultdict(list))
    teams_of = collections.defaultdict(set)
    for path in sys.argv[1:]:
        kinds, cls, team_of = {}, {}, {}
        squad = collections.defaultdict(dict)
        fighting = collections.defaultdict(dict)
        pos = collections.defaultdict(dict)
        hits, wakes, decs, events = [], [], [], []
        with open(path, encoding="utf-8") as handle:
            for line in handle:
                k = line[6:10]
                if k == "roam":
                    for team in json.loads(line)["teams"]:
                        kinds[team["name"]] = team.get("kind")
                elif k == "ev\",":
                    row = json.loads(line)
                    ev = row.get("ev")
                    if isinstance(ev, dict):
                        if ev.get("what") == "join":
                            kinds[ev["team"]] = ev.get("kind")
                        elif ev.get("what") in ("wipe", "breakoff", "away", "dirty"):
                            events.append((ev["team"], ev["what"]))
                elif k == "spaw":
                    for bot in json.loads(line)["bots"]:
                        cls[bot["n"]] = bot["cls"]
                        team_of[bot["n"]] = bot["team"]
                elif k == "squa":
                    row = json.loads(line)
                    squad[row["team"]][row["t"] // 1000] = row
                elif k == "team":
                    row = json.loads(line)
                    if row["fighting"]:
                        fighting[row["name"]][row["t"] // 1000] = row["fighting"]
                elif k == "snap":
                    row = json.loads(line)
                    if row["alive"]:
                        pos[row["t"] // 1000][row["n"]] = (row["team"], row["x"], row["y"])
                elif k == "hit\"":
                    row = json.loads(line)
                    if row.get("at") and row.get("dt") and row["at"] != row["dt"] and row["dmg"] > 0 and not row.get("pet"):
                        hits.append((row["t"] // 1000, row["a"], row["at"], row["d"], row["dmg"]))
                elif k == "wake":
                    row = json.loads(line)
                    if row.get("byTeam") and row["byTeam"] != row["team"]:
                        wakes.append(row["byTeam"])
                elif k == "dec\"":
                    row = json.loads(line)
                    if row.get("pick"):
                        decs.append((row["t"] // 1000, row["team"], row["pick"]))
        kind_of = lambda team: kinds.get(team) or "-"
        for team, seconds in fighting.items():
            kind = kind_of(team)
            teams_of[kind].add(team)
            total[kind]["seconds"] += len(seconds)
            last_legs = 0
            for second in sorted(squad.get(team, {})):
                row = squad[team][second]
                legs = row.get("legs") or 0
                total[kind]["fell back"] += last_legs == 0 and legs > 0
                last_legs = legs
            for second, foes in seconds.items():
                mine = {n: v for n, v in pos[second].items() if v[0] == team}
                theirs = [(n, v) for n, v in pos[second].items() if v[0] in foes]
                soft = {n: v for n, v in mine.items() if cls.get(n) not in FIGHTERS}
                healers = [v for n, v in mine.items() if cls.get(n) in HEALERS]
                enemy_fighters = [v for n, v in theirs if cls.get(n) in FIGHTERS]
                for n, v in soft.items():
                    total[kind]["soft seconds"] += 1
                    if any(math.dist((v[1], v[2]), (e[1], e[2])) <= 350 for e in enemy_fighters):
                        total[kind]["soft in melee"] += 1
                if len(soft) >= 3:
                    mid = (sum(v[1] for v in soft.values()) / len(soft), sum(v[2] for v in soft.values()) / len(soft))
                    lists[kind]["clump"].append(max(math.dist((v[1], v[2]), mid) for v in soft.values()))
                if healers:
                    for n, v in mine.items():
                        if cls.get(n) in FIGHTERS:
                            total[kind]["fighter seconds"] += 1
                            if min(math.dist((v[1], v[2]), (h[1], h[2])) for h in healers) > 1850:
                                total[kind]["fighter far"] += 1
                call = (squad.get(team, {}).get(second) or {}).get("kill")
                if call:
                    total[kind]["call seconds"] += 1
                    c = cls.get(call)
                    total[kind]["call healer" if c in HEALERS else "call fighter" if c in FIGHTERS else "call caster"] += 1
        per_fighter = collections.defaultdict(set)
        for second, attacker, team, victim, damage in hits:
            if second not in fighting.get(team, ()):
                continue
            kind = kind_of(team)
            total[kind]["damage"] += damage
            if (squad.get(team, {}).get(second) or {}).get("kill") == victim:
                total[kind]["damage on call"] += damage
            if cls.get(attacker) in FIGHTERS:
                per_fighter[(team, attacker, second // 60)].add(victim)
        for (team, attacker, minute), victims in per_fighter.items():
            lists[kind_of(team)]["targets"].append(len(victims))
        for team in wakes:
            total[kind_of(team)]["woken"] += 1
        for second, team, pick in decs:
            if second not in fighting.get(team, ()):
                continue
            kind, what = kind_of(team), pick["kind"]
            if what == "Heal" and isinstance(pick.get("hp"), (int, float)):
                lists[kind]["heals at"].append(pick["hp"])
            elif what == "Raise":
                total[kind]["raises"] += 1
            elif what in ("Peel", "Intercept", "Guard", "Defend"):
                total[kind]["peels"] += 1
            elif what == "Duty":
                total[kind]["duty"] += 1
            elif what in ("Mez", "MezArea"):
                total[kind]["mez"] += 1
        for team, what in events:
            total[kind_of(team)][{"wipe": "wiped", "breakoff": "broke off", "away": "got away", "dirty": "left a dirty fight"}[what]] += 1

    print(f"{'kind':8} {'teams':>5} {'min':>5} {'on call':>7} {'targets':>7} {'calls: healer/caster/fighter':>28} {'woken':>6} {'in melee':>8} {'clump':>6} {'far':>5} "
          f"{'heals at':>8} {'raises':>6} {'peels':>6} {'duty':>5} {'mez':>5} {'fell back':>9} {'broke off':>9} {'wiped':>5}")
    for kind in ORDER + sorted(set(total) - set(ORDER)):
        t = total.get(kind)
        if not t or not t["seconds"]:
            continue
        minutes = t["seconds"] / 60
        calls = max(1, t["call seconds"])
        med = lambda key: statistics.median(lists[kind][key]) if lists[kind][key] else 0
        left = f" (+{t['left a dirty fight']} dirty)" if t["left a dirty fight"] else ""
        print(f"{kind:8} {len(teams_of[kind]):5} {minutes:5.0f} {t['damage on call'] / max(1, t['damage']):7.0%} {med('targets'):7.1f} "
              f"{t['call healer'] / calls:10.0%} {t['call caster'] / calls:8.0%} {t['call fighter'] / calls:8.0%} {t['woken'] / minutes:6.2f} "
              f"{t['soft in melee'] / max(1, t['soft seconds']):8.0%} {med('clump'):6.0f} {t['fighter far'] / max(1, t['fighter seconds']):5.0%} "
              f"{med('heals at'):8.0f} {t['raises'] / minutes:6.2f} {t['peels'] / minutes:6.2f} {t['duty'] / minutes:5.2f} {t['mez'] / minutes:5.2f} "
              f"{t['fell back']:9} {t['broke off']:6}/{t['got away']:<2} {t['wiped']:5}{left}")


if __name__ == "__main__":
    main()
