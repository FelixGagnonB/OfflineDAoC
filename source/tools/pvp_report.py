"""A detailed report on a batch of recorded 8v8 scrims, as a Markdown document.

    python pvp_report.py <folder or .jsonl files> [--batch <prefix>] [--since <prefix>] [--until <prefix>]
                         [--out report.md] [--title "..."]

Where pvp_insight.py gives the short reading of a batch, this writes everything out in tables:
results, the course of a fight, the opening, the first death, what the healers were doing,
who wakes whom, where each class stands and when it dies, kill targets and assist, damage,
interrupts, control, orders, each class's decisions, and the missed chances. Every figure is
per match unless it says otherwise, and each table says how to read it.

It reads one match at a time, so a night's batch fits in memory (about two seconds a match).
"""
import collections
import glob
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pvp_insight as insight  # noqa: E402

HEALERS, SOFT = insight.HEALERS, insight.SOFT
CURERS = {"Sorcerer", "Minstrel", "Bard", "Healer"}      # the classes with a cure mez on this server


def kind_of(bot):
    return "healer" if bot["cls"] in HEALERS else "caster" if bot["cls"] in SOFT else "fighter"


def cures(bot):
    return bot["cls"] in CURERS


def median(values, default=0):
    values = sorted(values)
    return values[len(values) // 2] if values else default


def pct(part, whole):
    return f"{100 * part / whole:.0f}%" if whole else "-"


class Batch:
    def __init__(self):
        c, d = collections.Counter, collections.defaultdict
        self.files = 0
        self.played = c()                    # group -> matches
        self.results = d(c)                  # pairing -> winner -> n
        self.lengths, self.survivors, self.contact = d(list), d(list), d(list)
        self.first_mez = d(list)             # (pairing, group) -> seconds after contact of the first mez on it
        self.first_death_at, self.turn_at = d(list), d(list)
        self.lead = d(c)                     # (measure, second) -> "won"/"lost"/"even"
        self.readings = d(lambda: d(list))   # (second, "winner"/"loser") -> key -> values
        self.firsts = []
        self.state = d(c)                    # (group, kind) -> state -> seconds
        self.slept = []
        self.cure = d(c)                     # (group, build) -> begun/finished
        self.curer = d(c)                    # (group, build) -> state while a healer sleeps
        self.curer_seconds = c()
        self.bots = c()                      # (group, build) -> bot-matches
        self.advance = d(lambda: d(list))    # (group, build) -> second -> place
        self.deaths = d(list)                # (group, build) -> seconds after contact
        self.first_dead = c()                # (group, build)
        self.calls = d(c)                    # group -> target kind
        self.assist = d(c)                   # group -> hits / on kill / damage / damage on kill
        self.damage, self.hits = d(c), d(c)  # group -> source -> damage, hits
        self.taken = d(c)                    # group -> victim kind -> damage
        self.on_healers = d(c)               # group (victim side) -> source -> attacks landing
        self.heal_casts = d(c)               # group -> begun / finished / why stopped
        self.healed = d(c)                   # (group, build) -> asked / got / count
        self.orders = d(c)                   # (group, "won"/"lost") -> order -> seconds
        self.control = d(c)                  # group (the side that did it) -> (what, victim kind) -> seconds
        self.picks = d(c)                    # (group, build) -> decision kind
        self.casts = d(c)                    # (group, build) -> spell type
        self.low_end = d(c)                  # (group, build) -> low / all
        self.chances = d(c)
        self.labels = d(c)                   # losing group -> label
        self.matches_with_winner = 0

    def add(self, m):
        head, rows, bots, contact, end = m["head"], m["rows"], m["bots"], m["contact"], m["end"]
        group = {"A": head["a"], "B": head["b"]}
        pairing = f"{head['a']} against {head['b']}"
        winner = "A" if end.get("Winner") == head["a"] else "B" if end.get("Winner") == head["b"] else None
        self.files += 1
        for team in "AB":
            self.played[group[team]] += 1
        self.results[pairing][end.get("Winner") or "draw"] += 1
        self.lengths[pairing].append(end["Seconds"])
        self.contact[pairing].append(end.get("SecondsToContact", 0))
        if winner:
            self.survivors[pairing].append(end["AliveA"] if winner == "A" else end["AliveB"])
        for bot in bots.values():
            self.bots[(group[bot["team"]], bot["build"])] += 1
        snaps, squads = insight.seconds(m)
        order = sorted(snaps)
        team_of = lambda name: bots[name]["team"]
        gof = lambda name: group[bots[name]["team"]]

        # The course of the fight, second by second.
        mezzed_at = {}
        for second in order:
            now = snaps[second]
            for team in "AB":
                if team not in mezzed_at and any(s["alive"] and s["mez"] for n, s in now.items() if team_of(n) == team):
                    mezzed_at[team] = second
            for name, s in now.items():
                bot = bots[name]
                g, k = group[bot["team"]], kind_of(bot)
                state = ("dead" if not s["alive"] else "asleep" if s["mez"] else "stunned" if s["stun"]
                         else "rooted or slowed" if k == "fighter" and s["root"]
                         else "being hit, cannot cast" if k != "fighter" and (s.get("intr") or s.get("sil")) else "free")
                self.state[(g, k)][state] += 1
                if s["alive"]:
                    enemy = group["B" if bot["team"] == "A" else "A"]
                    for flag, what in (("mez", "asleep"), ("stun", "stunned"), ("root", "rooted or slowed"), ("ns", "nearsighted"), ("dis", "diseased")):
                        if s.get(flag):
                            self.control[enemy][(what, k)] += 1
                    if k == "fighter":
                        self.low_end[(g, bot["build"])]["all"] += 1
                        self.low_end[(g, bot["build"])]["low"] += s["end"] < 20
            for team in "AB":
                line = squads[second].get(team)
                if line and winner:
                    self.orders[(group[team], "won" if team == winner else "lost")][line["order"]] += 1
            # While a healer sleeps: what is each groupmate that could wake it doing?
            for team in "AB":
                sleepers = {n for n, s in now.items() if team_of(n) == team and bots[n]["cls"] in HEALERS and bots[n]["build"] != "healer-pac" and s["alive"] and s["mez"]}
                if not sleepers:
                    continue
                self.curer_seconds[group[team]] += 1
                for n, s in now.items():
                    if team_of(n) != team or not cures(bots[n]) or n in sleepers:
                        continue
                    what = ("dead" if not s["alive"] else "asleep" if s["mez"] else "stunned" if s["stun"]
                            else "being hit, cannot cast" if s.get("intr") or s.get("sil") else "casting" if s["casting"] else "walking" if s["moving"] else "standing idle")
                    self.curer[(group[team], bots[n]["build"])][what] += 1
        for team in "AB":
            if team in mezzed_at:
                self.first_mez[(pairing, group[team])].append(mezzed_at[team])
        if winner and order:
            loser = "B" if winner == "A" else "A"
            for second in (5, 10, 20, 30, 45, 60):
                if second not in snaps:
                    continue
                read = {team: insight.reading(snaps[second], bots, team) for team in "AB"}
                for key, better in (("asleep", -1), ("alive", 1)):
                    diff = (read[winner][key] - read[loser][key]) * better
                    self.lead[(key, second)]["won" if diff > 0 else "lost" if diff < 0 else "even"] += 1
                for side, team in (("winner", winner), ("loser", loser)):
                    for key in ("alive", "asleep", "held", "casters", "healers"):
                        self.readings[(second, side)][key].append(read[team][key])
            # Where each member stands along the line from its own group to the enemy's.
            if 0 in snaps:
                middle = {}
                for team in "AB":
                    mine = [s for n, s in snaps[0].items() if team_of(n) == team]
                    middle[team] = (sum(s["x"] for s in mine) / len(mine), sum(s["y"] for s in mine) / len(mine)) if mine else None
                if middle["A"] and middle["B"]:
                    for second in (5, 10, 20):
                        for n, s in snaps.get(second, {}).items():
                            if not s["alive"]:
                                continue
                            own, foe = middle[team_of(n)], middle["B" if team_of(n) == "A" else "A"]
                            ax, ay = foe[0] - own[0], foe[1] - own[1]
                            if ax or ay:
                                self.advance[(gof(n), bots[n]["build"])][second].append(100 * ((s["x"] - own[0]) * ax + (s["y"] - own[1]) * ay) / (ax * ax + ay * ay))

        # Events.
        kill = {}
        for r in rows:
            k = r["k"]
            if k == "squad":
                kill[r["team"]] = r.get("kill")
            elif k == "call" and r.get("d") in bots:
                self.calls[group[r["team"]]][kind_of(bots[r["d"]])] += 1
            elif k == "hit" and r.get("a") in bots and r.get("d") in bots and not r.get("dpet") and r.get("at") != r.get("dt"):
                g, victim = gof(r["a"]), bots[r["d"]]
                landed = r["res"] not in ("Missed", "Blocked", "Evaded", "Parried")
                source = bots[r["a"]]["build"] + (" (pet)" if r.get("pet") else "")
                if r.get("dmg", 0) > 0:
                    self.damage[g][source] += r["dmg"]
                    self.hits[g][source] += 1
                    self.taken[group[victim["team"]]][kind_of(victim)] += r["dmg"]
                    if not r.get("pet"):
                        self.assist[g]["hits"] += 1
                        self.assist[g]["damage"] += r["dmg"]
                        if kill.get(bots[r["a"]]["team"]) == r["d"]:
                            self.assist[g]["hits on the kill target"] += 1
                            self.assist[g]["damage on the kill target"] += r["dmg"]
                if landed and victim["cls"] in HEALERS and r["t"] >= contact:
                    attacker = bots[r["a"]]
                    self.on_healers[group[victim["team"]]][
                        "an enemy pet" if r.get("pet") else ("a fighter" if kind_of(attacker) == "fighter" else "a caster or healer")
                        + (", spell or shout" if r["type"] == "Spell" else ", melee")] += 1
            elif k == "death" and r["n"] in bots and r["t"] >= contact:
                self.deaths[(gof(r["n"]), bots[r["n"]]["build"])].append((r["t"] - contact) / 1000)
            elif k in ("begin", "cast", "stop") and r["n"] in bots and not r.get("pet"):
                bot = bots[r["n"]]
                g = group[bot["team"]]
                if r.get("st") == "CureMezz" and k != "stop":
                    self.cure[(g, bot["build"])]["begun" if k == "begin" else "finished"] += 1
                if k == "cast" and r["t"] >= contact:
                    self.casts[(g, bot["build"])][r["st"]] += 1
                if bot["cls"] in HEALERS and r["t"] >= contact:
                    if k == "begin":
                        self.heal_casts[g]["begun"] += 1
                    elif k == "cast" and r.get("castTime", 0) > 0:
                        self.heal_casts[g]["finished"] += 1
                    elif k == "stop":
                        self.heal_casts[g][r["why"]] += 1
            elif k == "heal" and r["n"] in bots and not r.get("pet") and r.get("asked", 0) >= 100 and r["t"] >= contact:
                key = (gof(r["n"]), bots[r["n"]]["build"])
                self.healed[key]["asked"] += r["asked"]
                self.healed[key]["got"] += r["got"]
                self.healed[key]["count"] += 1
            elif k == "dec" and r["n"] in bots and r["t"] >= contact:
                self.picks[(gof(r["n"]), bots[r["n"]]["build"])][r["pick"]["kind"] if r.get("pick") else "nothing worth casting"] += 1

        story = insight.story(m)
        first = insight.first_death(m)
        if story:
            self.matches_with_winner += 1
            self.turn_at[pairing].append(story["turn"])
            self.labels[story["loser"]].update(story["labels"])
            self.labels[story["loser"]]["fights lost"] += 1
        if first:
            first["won"] = first["group"] == end.get("Winner")
            self.firsts.append(first)
            self.first_death_at[pairing].append(first["at"])
            self.first_dead[(first["group"], first["build"])] += 1
        self.slept += insight.sleeps(m)
        found = insight.chances(m)
        for team in "AB":
            self.chances[group[team]].update(found[team])


def table(out, header, rows, note=None):
    out.append("| " + " | ".join(header) + " |")
    out.append("|" + "|".join("---" for _ in header) + "|")
    for row in rows:
        out.append("| " + " | ".join(str(cell) for cell in row) + " |")
    if note:
        out.append("")
        out.append(note)
    out.append("")


def write(b, title):
    out = []
    groups = sorted(b.played)
    per = lambda value, g: value / b.played[g] if b.played[g] else 0
    out += [f"# {title}", "", f"{b.files} matches. Every figure is per match unless it says otherwise; \"s\" is seconds; "
            "times are counted from first contact (the first blow or hostile cast).", ""]

    out += ["## 1. Results", ""]
    rows = []
    for pairing in sorted(b.results):
        won = b.results[pairing]
        rows.append([pairing, sum(won.values()), ", ".join(f"{who} {n}" for who, n in won.most_common()),
                     f"{median(b.lengths[pairing])} s", f"{median(b.contact[pairing])} s", f"{median(b.survivors[pairing])} of 8",
                     pct(sum(1 for s in b.survivors[pairing] if s >= 6), len(b.survivors[pairing]))])
    table(out, ["Pairing", "Matches", "Wins", "Median length", "Walk to contact", "Winner's survivors (median)", "Won with 6 or more alive"], rows,
          "A winner with six or more alive means the fight was one-sided; few such fights means they were close.")

    out += ["## 2. The course of a fight", ""]
    rows = []
    for pairing in sorted(b.results):
        a, c = pairing.split(" against ")
        rows.append([pairing, f"{median(b.first_mez[(pairing, a)], '-')} s / {median(b.first_mez[(pairing, c)], '-')} s",
                     f"{median(b.turn_at[pairing])} s", f"{median(b.first_death_at[pairing])} s", f"{median(b.lengths[pairing])} s"])
    table(out, ["Pairing", "First mez lands on the first / second group", "Turning point", "First death", "Match length (with the walk in)"], rows,
          "The turning point is the first second from which the eventual winner stays ahead to the end, by numbers alive and the three readings.")

    out += ["## 3. When is the fight decided?", "",
            "For each moment: in how many fights the side that was ahead on that measure went on to win.", ""]
    rows = []
    for key, label in (("asleep", "Fewer members asleep"), ("alive", "More members alive")):
        for second in (5, 10, 20, 30, 45, 60):
            c = b.lead[(key, second)]
            decided = c["won"] + c["lost"]
            if decided + c["even"]:
                rows.append([label, f"{second} s", decided, pct(c["won"], decided), c["even"]])
    table(out, ["Measure", "At", "Fights where one side was ahead", "The side ahead won", "Fights level on it"], rows)
    first_lost = sum(1 for f in b.firsts if not f["won"])
    out += [f"The side that lost the first member lost the fight in {pct(first_lost, len(b.firsts))} of {len(b.firsts)} fights.", ""]
    rows = []
    for second in (10, 20, 30, 60):
        for side in ("winner", "loser"):
            r = b.readings[(second, side)]
            if r["alive"]:
                mean = lambda key: sum(r[key]) / len(r[key])
                rows.append([f"{second} s", side, f"{mean('alive'):.1f}", f"{mean('asleep'):.1f}", f"{100 * mean('healers'):.0f}%", f"{100 * mean('casters'):.0f}%", f"{100 * mean('held'):.0f}%"])
    table(out, ["At", "Side", "Alive", "Asleep", "Healers free to heal", "Healers and casters able to cast", "Fighters held"], rows,
          "The owner's three readings, for the eventual winner and loser. The earlier a row pair differs, the earlier that reading tells the result.")

    out += ["## 4. The first death", ""]
    if b.firsts:
        rows = [[why, n, pct(n, len(b.firsts))] for why, n in collections.Counter(f["why"] for f in b.firsts).most_common()]
        table(out, ["Why the first member died", "Fights", "Share"], rows,
              f"It died {median([f['at'] for f in b.firsts])} s after contact (median) with {median([f['attackers'] for f in b.firsts])} enemies on it; "
              f"{pct(sum(1 for f in b.firsts if f['held']), len(b.firsts))} were themselves asleep or stunned for a third or more of their last ten seconds. "
              "\"Why\" looks at the victim's own healers in its last ten seconds.")
        rows = []
        for g in groups:
            mine = [f for f in b.firsts if f["group"] == g]
            if not mine:
                rows.append([g, 0, "-", "-", "-"])
                continue
            who = collections.Counter(f["build"] for f in mine)
            why = collections.Counter(f["why"] for f in mine)
            rows.append([g, f"{len(mine)} of {b.played[g]}", ", ".join(f"{build} {pct(n, len(mine))}" for build, n in who.most_common(4)),
                         ", ".join(f"{w} {pct(n, len(mine))}" for w, n in why.most_common(3)), f"{median([f['at'] for f in mine])} s"])
        table(out, ["Group", "Lost the first member in", "Who it was", "Why", "When"], rows)

    out += ["## 5. What the healers were doing", ""]
    for kind, label in (("healer", "Healers (Cleric, Druid, Healer)"), ("caster", "Casters and support (Bard, Shaman included)"), ("fighter", "Fighters")):
        states = sorted({s for g in groups for s in b.state[(g, kind)]}, key=lambda s: -sum(b.state[(g, kind)][s] for g in groups))
        rows = [[g] + [pct(b.state[(g, kind)][s], sum(b.state[(g, kind)].values())) for s in states] for g in groups]
        table(out, [label] + states, rows)
    out += ["Share of the fight from contact to the end, the dead included. \"Being hit\" means an attack landed in the last moments, so no cast can start.", ""]
    rows = []
    for g in groups:
        c = b.heal_casts[g]
        rows.append([g, f"{per(c['begun'], g):.0f}", f"{per(c['finished'], g):.0f}", f"{per(c['attacked'], g):.1f}",
                     f"{per(c['refused by the game'], g):.1f}", f"{per(c['dropped by the caster'], g):.1f}"])
    table(out, ["Group", "Healers' casts begun", "Finished", "Stopped by an attack", "Refused by the game", "Dropped by the caster"], rows,
          "\"Refused by the game\" is a cast the game ended itself: the target went out of range or sight, died, or the caster moved.")
    rows = []
    for g in groups:
        c = b.on_healers[g]
        total = sum(c.values())
        rows.append([g, f"{per(total, g):.0f}"] + [f"{per(c[k], g):.0f}" for k in
                    ("an enemy pet", "a fighter, melee", "a fighter, spell or shout", "a caster or healer, spell or shout", "a caster or healer, melee")])
    table(out, ["Attacks landing on its healers", "All", "Enemy pets", "Fighters, melee", "Fighters, shouts", "Casters' spells", "Casters, melee"], rows)
    rows = []
    for (g, build), c in sorted(b.healed.items()):
        if c["count"] / b.bots[(g, build)] >= 1:
            rows.append([g, build, f"{c['count'] / b.bots[(g, build)]:.1f}", f"{c['got'] / b.bots[(g, build)]:.0f}", pct(c["got"], c["asked"])])
    table(out, ["Group", "Build", "Heals of 100 and more landing", "Health restored", "Of what the heals could give"], rows,
          "Per bot and match. The last column is low partly by design: an instant group heal reaches members who are not hurt, and heals over time tick on a full member.")

    out += ["## 6. Sleep, and who ends it", ""]
    rows = []
    for g in groups:
        for kind in ("healer", "caster", "fighter"):
            mine = [s for s in b.slept if s["group"] == g and s["kind"] == kind]
            if not mine:
                continue
            ended = collections.Counter(s["ended"] for s in mine)
            rows.append([g, kind + "s", f"{per(len(mine), g):.1f}", f"{per(sum(1 for s in mine if s['opening']), g):.1f}",
                         f"{median([s['seconds'] for s in mine]):.0f} s", f"{per(sum(s['seconds'] for s in mine), g):.0f} s",
                         pct(ended["cured by a groupmate"], len(mine)), pct(ended["woken by an enemy's hit"], len(mine)),
                         pct(ended["ran out or was purged"], len(mine)), pct(ended["died asleep"], len(mine))])
    table(out, ["Group", "Who slept", "Mezzes sat through", "In the first 15 s", "Median length", "Seconds asleep", "Cured", "Woken by a hit", "Ran out", "Died asleep"], rows)
    rows = []
    for (g, build), c in sorted(b.cure.items()):
        rows.append([g, build, f"{c['begun'] / b.bots[(g, build)]:.1f}", f"{c['finished'] / b.bots[(g, build)]:.1f}"])
    table(out, ["Group", "Build", "Cure mez begun", "Cure mez finished"], rows, "Per bot and match. An instant cure shows as finished without having begun.")
    rows = []
    for (g, build), c in sorted(b.curer.items()):
        total = sum(c.values())
        rows.append([g, build, f"{per(b.curer_seconds[g], g):.0f} s"] + [pct(c[s], total) for s in
                    ("dead", "asleep", "stunned", "being hit, cannot cast", "casting", "walking", "standing idle")])
    table(out, ["Group", "Who could wake it", "A healer is asleep for", "Dead", "Asleep", "Stunned", "Being hit", "Casting", "Walking", "Idle"], rows,
          "While one of the group's healers that only heal is asleep: what each groupmate with a cure mez is doing during those seconds.")

    out += ["## 7. Each member: where it stands, when it dies", ""]
    for g in groups:
        rows = []
        total_damage = sum(b.damage[g].values())
        for (gg, build), n in sorted(b.bots.items()):
            if gg != g:
                continue
            died = b.deaths[(g, build)]
            adv = b.advance[(g, build)]
            low = b.low_end[(g, build)]
            rows.append([build, pct(len(died), n), f"{median(died):.0f} s" if died else "-", pct(b.first_dead[(g, build)], b.played[g]),
                         " / ".join(f"{median(adv[s]):.0f}" if adv[s] else "-" for s in (5, 10, 20)),
                         f"{b.damage[g][build] / n:.0f} ({pct(b.damage[g][build], total_damage)})",
                         f"{b.hits[g][build] / n:.0f}", pct(low["low"], low["all"]) if low["all"] else "-"])
        table(out, [g, "Dies in", "Median death", "First to die (of all its fights)", "Forward at 5 / 10 / 20 s", "Damage (share of the group's)", "Damaging hits", "Under 20% endurance"], rows)
    out += ["\"Forward\" is the member's place along the line from where its own group's middle was at contact (0) to where the enemy's was (100). "
            "The two middles are about 1,700 units apart, so 50 is the middle of the field. Damage is per bot and match.", ""]
    rows = []
    for g in groups:
        pets = {k: v for k, v in b.damage[g].items() if k.endswith("(pet)")}
        total = sum(b.damage[g].values())
        rows.append([g, f"{per(total, g):.0f}", ", ".join(f"{k} {pct(v, total)}, {per(b.hits[g][k], g):.0f} hits" for k, v in sorted(pets.items(), key=lambda kv: -kv[1])) or "none",
                     ", ".join(f"{kind}s {pct(b.taken[g][kind], sum(b.taken[g].values()))}" for kind in ("healer", "caster", "fighter"))])
    table(out, ["Group", "Damage dealt", "Of which its pets", "The damage it took fell on its"], rows)

    out += ["## 8. Kill targets and assist", ""]
    rows = []
    for g in groups:
        c, a = b.calls[g], b.assist[g]
        total = sum(c.values())
        rows.append([g, f"{per(total, g):.1f}", ", ".join(f"{kind}s {pct(c[kind], total)}" for kind in ("healer", "caster", "fighter")),
                     pct(a["hits on the kill target"], a["hits"]), pct(a["damage on the kill target"], a["damage"])])
    table(out, ["Group", "Kill-target calls", "Called on", "Its hits that landed on the called target", "Its damage on the called target"], rows,
          "Pets are left out of the last two columns. A low share means the group's damage is spread: interrupts and peels count against it, by design, but so does a train that is not together.")

    out += ["## 9. Control done to the enemy", ""]
    rows = []
    for g in groups:
        c = b.control[g]
        rows.append([g] + [f"{per(c[(what, kind)], g):.0f}" for what, kind in
                    (("asleep", "healer"), ("asleep", "caster"), ("asleep", "fighter"), ("stunned", "healer"), ("stunned", "caster"), ("stunned", "fighter"),
                     ("rooted or slowed", "fighter"), ("nearsighted", "healer"), ("nearsighted", "caster"), ("diseased", "fighter"), ("diseased", "healer"))])
    table(out, ["Done by", "Healers asleep", "Casters asleep", "Fighters asleep", "Healers stunned", "Casters stunned", "Fighters stunned",
                "Fighters rooted or slowed", "Healers nearsighted", "Casters nearsighted", "Fighters diseased", "Healers diseased"], rows,
          "Seconds in a match that enemy members spent in each state, added over the members (two healers asleep for ten seconds is twenty).")

    out += ["## 10. Orders", ""]
    names = sorted({o for c in b.orders.values() for o in c}, key=lambda o: -sum(c[o] for c in b.orders.values()))
    rows = []
    for g in groups:
        for result in ("won", "lost"):
            c = b.orders[(g, result)]
            if sum(c.values()):
                rows.append([g, result] + [pct(c[o], sum(c.values())) for o in names])
    table(out, ["Group", "In fights it"] + names, rows, "Share of the fight under each order, from first contact. Hold comes before contact and is not counted here.")

    out += ["## 11. What each member decided", ""]
    for g in groups:
        rows = []
        for (gg, build), c in sorted(b.picks.items()):
            if gg != g:
                continue
            n = b.bots[(g, build)]
            casts = b.casts[(g, build)]
            rows.append([build, ", ".join(f"{kind} {count / n:.1f}" for kind, count in c.most_common(7)),
                         ", ".join(f"{st} {count / n:.1f}" for st, count in casts.most_common(5))])
        table(out, [g, "Decisions recorded", "Casts finished, by type"], rows)
    out += ["Per bot and match, from contact. Decisions are the squad's and the scored choice's; a fighter's swings and a healer's heals are not decisions here.", ""]

    out += ["## 12. Missed chances", ""]
    keys = ["enemy healer free", "enemy healer free, a member idle in range", "pursuer on the backline", "pursuer rootable, a rooter free in range",
            "enemy mezzable, a mezzer free in range", "trained 3 s and more with no heal", "trained with no heal, a healer free in range"]
    table(out, ["Seconds a match"] + groups, [[key] + [f"{per(b.chances[g][key], g):.0f}" for g in groups] for key in keys],
          "Each line counts member-seconds: two enemy healers free for ten seconds is twenty.")

    out += ["## 13. What went wrong for the loser around the turning point", ""]
    rows = []
    for g in groups:
        c = collections.Counter(b.labels[g])
        lost = c.pop("fights lost", 0)
        if lost:
            rows.append([g, lost, ", ".join(f"{label} {pct(n, lost)}" for label, n in c.most_common(6))])
    table(out, ["Group", "Fights lost", "State in the ten seconds before the turning point (a fight can carry several)"], rows)
    return "\n".join(out) + "\n"


def main(argv):
    option = lambda name: argv[argv.index(name) + 1] if name in argv else None
    batch, since, until, out, title = (option(name) for name in ("--batch", "--since", "--until", "--out", "--title"))
    skip = {batch, since, until, out, title}
    args = [a for a in argv[1:] if not a.startswith("--") and a not in skip]
    if not args:
        print(__doc__)
        return 2
    files = []
    for a in args:
        files += sorted(glob.glob(os.path.join(a, "*.jsonl"))) if os.path.isdir(a) else [a]
    name = lambda f: os.path.basename(f)
    files = [f for f in files if (not batch or name(f).startswith(batch)) and (not since or name(f) >= since) and (not until or name(f) < until)]
    b = Batch()
    for number, f in enumerate(files, 1):
        m = insight.load(f)
        if m:
            b.add(m)
        if number % 25 == 0:
            print(f"{number} of {len(files)} read", file=sys.stderr, flush=True)
    if not b.files:
        print("No finished recorded matches found.")
        return 1
    text = write(b, title or f"8v8 scrims: {batch or since or 'recorded matches'}")
    if out:
        with open(out, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(text)
        print(f"{b.files} matches; report written to {out}")
    else:
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
