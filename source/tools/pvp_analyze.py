"""Turn recorded scrim matches into a report (docs/PVP-8V8-ROADMAP.md, M3).

    python pvp_analyze.py <folder or .jsonl files> [--batch <prefix>] [--json out.json]

Reads the JSON-lines files written by the server's PvpRecorder (bots/pvp/PvpRecorder.cs) and
prints, per batch: how the matches ended, and for each side and each build what it dealt, took,
cast and how much of the fight it spent mesmerized, stunned or out of power.

Snapshots are one second apart, so every "time" figure is a count of seconds.

From recorder format 3 on it also reports where each group stands (how close its healers and
casters are to enemy fighters, how far apart they keep, how far apart the groups are), casts
started against finished and what stopped them, health actually restored and wasted, who broke
mezzes, and what became of the member the enemy's fighters were on. Older recordings give the
position figures only. pvp_review.py prints one match second by second.
"""
import collections
import glob
import json
import math
import os
import statistics
import sys

DAMAGE_RESULTS = {"HitUnstyled", "HitStyle"}
HEAL_TYPES = {"Heal", "CombatHeal", "HealOverTime", "SpreadHeal", "FatigueConsumingHeal", "InstantHeal"}
# Classes whose casting the other side wants to stop: the healers and the casters.
BACKLINE = {"Cleric", "Friar", "Druid", "Bard", "Healer", "Shaman", "Sorcerer", "Theurgist", "Cabalist", "Wizard",
            "Runemaster", "Spiritmaster", "Eldritch", "Enchanter", "Mentalist"}
HEALERS = {"Cleric", "Friar", "Druid", "Healer"}
CONTROL_TYPES = {"Mesmerize", "Mez", "Stun", "SpeedDecrease", "StyleStun", "Nearsight", "Amnesia", "Disease", "Confusion"}


def load(path):
    match = {"file": os.path.basename(path), "snaps": [], "hits": [], "casts": [], "deaths": [], "decs": [], "calls": [], "end": None, "head": None, "ready": None,
             "rows": []}
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            row = json.loads(line)
            kind = row.get("k")
            # Everything in file order as well, for what has to be read against the squad's state at that moment.
            if kind in ("begin", "stop", "cast", "heal", "wake", "squad", "death", "snap"):
                match["rows"].append(row)
            if kind == "match":
                match["head"] = row
            elif kind == "snap":
                match["snaps"].append(row)
            elif kind == "hit":
                match["hits"].append(row)
            elif kind == "cast":
                match["casts"].append(row)
            elif kind == "ready":
                match["ready"] = row
            elif kind == "dec":
                match["decs"].append(row)
            elif kind == "call":
                match["calls"].append(row)
            elif kind == "death":
                match["deaths"].append(row)
            elif kind == "end":
                match["end"] = row
    return match if match["head"] else None


def analyze(match):
    head = match["head"]
    bots = {b["n"]: b for b in head["bots"]}
    stats = {n: collections.Counter() for n in bots}
    spells = {n: collections.Counter() for n in bots}
    contact = next((s["t"] for s in match["snaps"] if s["combat"]), None)
    outside = collections.Counter()
    # Bots that were dead and are alive again: raised beside their corpse, or (a fault) stood up
    # somewhere else, which is the old release to a bind point.
    corpse, raised, released = {}, 0, 0
    for s in match["snaps"]:
        if s["n"] not in stats:
            continue
        if not s["alive"]:
            corpse[s["n"]] = (s["x"], s["y"])
        elif s["n"] in corpse:
            x, y = corpse.pop(s["n"])
            if abs(s["x"] - x) + abs(s["y"] - y) > 3000:
                released += 1
            else:
                raised += 1
    for s in match["snaps"]:
        if contact is None or s["t"] < contact or s["n"] not in stats:
            continue
        c = stats[s["n"]]
        c["seconds"] += 1
        if not s["alive"]:
            c["dead"] += 1
            continue
        c["alive"] += 1
        c["mez"] += bool(s["mez"])
        c["stun"] += bool(s["stun"])
        c["snared"] += bool(s["root"])
        c["mez_while_immune"] += bool(s["mez"] and s["mezImm"])
        c["casting"] += bool(s["casting"])
        c["idle"] += not (s["mez"] or s["stun"] or s["casting"] or s["moving"] or s["target"])
        c["no_power"] += bool(bots[s["n"]]["pow"] and s["pow"] < 0.1 * bots[s["n"]]["pow"])
        c["end_pow"] = s["pow"]
    # The opening: how many of each team are mezzed ten seconds after contact.
    asleep10 = collections.Counter()
    if contact is not None:
        seen = set()
        for s in match["snaps"]:
            # The first snapshot of each bot in that second; two can fall inside it.
            if contact + 10000 <= s["t"] < contact + 11000 and s["n"] in bots and s["n"] not in seen:
                seen.add(s["n"])
                if s["alive"] and s["mez"]:
                    asleep10[bots[s["n"]]["team"]] += 1
    # The opening, mez by mez: how many mezzes landed on each team in the first ten seconds, and
    # the most enemies any one area mez put to sleep. "Asleep after ten seconds" adds up every mez
    # of those ten seconds; five or more asleep has so far never been one spell.
    area = {c["spell"] for c in match["casts"] if c["st"] in ("Mesmerize", "Mez") and c["radius"]}
    opening_mez = collections.Counter()
    caught = collections.defaultdict(set)
    if contact is not None:
        for h in match["hits"]:
            if (h.get("st") in ("Mesmerize", "Mez") and h.get("at") and h.get("dt") and h["at"] != h["dt"] and not h.get("dpet")
                    and h["res"] not in ("Missed", "Resisted") and h["t"] <= contact + 10000):
                opening_mez[h["dt"]] += 1
                if h["spell"] in area:
                    caught[(h["dt"], h["t"] // 400, h["a"], h["spell"])].add(h["d"])
    largest_catch = collections.Counter()
    for (team, _, _, _), victims in caught.items():
        largest_catch[team] = max(largest_catch[team], len(victims))
    # Attacks that landed on each team's healers and casters (each one interrupts), and attacks a
    # guard took on its shield for somebody else.
    pressure = collections.Counter()
    guards = collections.Counter()
    for h in match["hits"]:
        dmg = (h["dmg"] or 0) + (h["crit"] or 0)
        if h.get("guardFor") and h["d"] in bots and h["guardFor"] in bots:
            guards[(bots[h["d"]]["team"], bots[h["d"]]["build"], bots[h["guardFor"]]["build"])] += 1
        elif (h.get("at") and h.get("dt") and h["at"] != h["dt"] and not h.get("dpet") and h["d"] in bots
              and bots[h["d"]]["cls"] in BACKLINE and h["res"] in DAMAGE_RESULTS):
            pressure[h["dt"]] += 1
        if h.get("at") and h["a"] in stats:
            a = stats[h["a"]]
            a["attacks"] += 1
            if h["res"] in DAMAGE_RESULTS or dmg > 0:
                a["dealt"] += dmg
                a["hits_landed"] += 1
            if h.get("at") == h.get("dt"):
                a["friendly_fire"] += 1
        if h.get("dt") and h["d"] in stats:
            stats[h["d"]]["taken"] += dmg
            # Damage from something that is in neither group: a keep guard, a monster.
            if not h.get("at") and dmg > 0:
                outside[h["a"]] += dmg
    for c in match["casts"]:
        if c["n"] not in stats:
            continue
        s = stats[c["n"]]
        s["casts"] += 1
        s["power_spent"] += c["pow"] or 0
        kind = "heal" if c["st"] in HEAL_TYPES else "control" if c["st"] in CONTROL_TYPES else "other"
        s["casts_" + kind] += 1
        spells[c["n"]][f'{c["st"]}'] += 1
    # Power each bot had when the fight began (the last snapshot before contact).
    start_power = {}
    for s in match["snaps"]:
        if contact is not None and s["t"] <= contact:
            start_power[s["n"]] = s["pow"]
    # Squad-brain decisions: what was picked (by kind, with its score), and on turns where nothing
    # was cast, what stood in the way.
    picks = {n: collections.Counter() for n in bots}
    scores = {n: collections.Counter() for n in bots}
    blocks = {n: collections.Counter() for n in bots}
    for d in match["decs"]:
        if d["n"] not in picks:
            continue
        lost = (d.get("blocked") or {}).get("previous cast lost")
        if lost:
            # Drop the spell name and the milliseconds so equal causes add up.
            blocks[d["n"]]["cast lost (" + lost.split("; ", 1)[-1] + ")"] += 1
        if d.get("pick"):
            picks[d["n"]][d["pick"]["kind"]] += 1
            scores[d["n"]][d["pick"]["kind"]] += d["pick"]["score"]
        else:
            picks[d["n"]]["nothing"] += 1
            for reason, detail in (d.get("blocked") or {}).items():
                if reason == "previous cast lost":
                    continue
                named = reason in ("too far", "best score too low", "cast refused", "asleep or claimed")
                blocks[d["n"]][reason if named else f"{reason}: {detail}"] += 1
    return {"head": head, "bots": bots, "stats": stats, "spells": spells, "contact": contact,
            "picks": picks, "scores": scores, "blocks": blocks, "ready": match["ready"],
            "start_power": start_power, "outside": outside, "raised": raised, "released": released,
            "pressure": pressure, "guards": guards, "asleep10": asleep10,
            "opening_mez": opening_mez, "largest_catch": largest_catch,
            "calls": collections.Counter(c["team"] for c in match["calls"]),
            "fight": fight(match, bots, contact),
            "moved_heals": collections.Counter(d["team"] for d in match["decs"] if (d.get("pick") or {}).get("moved")),
            "end": match["end"], "deaths": match["deaths"], "file": match["file"]}


def fight(match, bots, contact):
    """Positions, casting, healing, mez breaks and the trained member of one match (see the module text)."""
    out = {"near": collections.defaultdict(list), "apart": [], "spacing": collections.defaultdict(list),
           "begun": collections.Counter(), "finished": collections.Counter(), "stopped": collections.defaultdict(collections.Counter),
           "healed": collections.Counter(), "wasted": collections.Counter(), "on_trained": collections.Counter(),
           "wakes": collections.Counter(), "trained_seconds": collections.Counter(), "trained_died": collections.Counter(),
           "trained_calls": collections.Counter(), "ns": collections.Counter(), "soft_alive": collections.Counter(),
           "taken": collections.Counter(), "read": collections.Counter(), "order": collections.Counter(), "format3": False}
    if contact is None:
        return out
    soft = {n for n, b in bots.items() if b["cls"] in BACKLINE}
    team = {n: b["team"] for n, b in bots.items()}
    build = {n: b["build"] for n, b in bots.items()}
    by_second = collections.defaultdict(dict)
    for s in match["snaps"]:
        if s["n"] in bots and s["t"] >= contact:
            by_second[s["t"] // 1000].setdefault(s["n"], s)
    for second, snaps in sorted(by_second.items()):
        alive = {n: s for n, s in snaps.items() if s["alive"]}
        for side in "AB":
            mine = [n for n in alive if team[n] == side]
            foes = [n for n in alive if team[n] != side]
            fighters = [n for n in foes if n not in soft]
            mine_soft = [n for n in mine if n in soft]
            # The three readings of a fight (docs/PVP-8V8-PLAYBOOK.md, section 2): fighters
            # controlled, casters interrupted, healers busy.
            for n in mine:
                s = alive[n]
                held = s["mez"] or s["stun"]
                if n not in soft:
                    out["read"][(side, "fighter")] += 1
                    out["read"][(side, "fighter held")] += bool(held or s["root"])
                else:
                    free = not (held or s.get("intr") or s.get("sil"))
                    out["read"][(side, "caster")] += 1
                    out["read"][(side, "caster free")] += free
                    if bots[n]["cls"] in HEALERS:
                        out["read"][(side, "healer")] += 1
                        out["read"][(side, "healer free")] += free
            out["read"][(side, "healer seconds")] += sum(1 for n in snaps if team[n] == side and bots[n]["cls"] in HEALERS)
            for n in mine_soft:
                here = (alive[n]["x"], alive[n]["y"])
                out["soft_alive"][side] += 1
                out["ns"][side] += bool(alive[n].get("ns"))
                if fighters:
                    out["near"][side].append(min(math.dist(here, (alive[f]["x"], alive[f]["y"])) for f in fighters))
                others = [m for m in mine_soft if m != n]
                if others:
                    out["spacing"][side].append(min(math.dist(here, (alive[m]["x"], alive[m]["y"])) for m in others))
        a = [(s["x"], s["y"]) for n, s in alive.items() if team[n] == "A"]
        b = [(s["x"], s["y"]) for n, s in alive.items() if team[n] == "B"]
        if a and b:
            out["apart"].append(math.dist((sum(p[0] for p in a) / len(a), sum(p[1] for p in a) / len(a)),
                                          (sum(p[0] for p in b) / len(b), sum(p[1] for p in b) / len(b))))
    trained = {"A": None, "B": None}
    kill = {"A": None, "B": None}
    for row in match["rows"]:
        kind = row["k"]
        if kind == "squad":
            out["format3"] = True
            trained[row["team"]], kill[row["team"]] = row.get("trained"), row.get("kill")
            if row.get("trained") and row["t"] >= contact:
                out["trained_seconds"][row["team"]] += 1
            # A hold is before contact; the other orders are counted from contact on.
            if row.get("order") and (row["t"] >= contact or row["order"] == "Hold"):
                out["order"][(row["team"], row["order"])] += 1
        elif kind == "begin" and row["n"] in build and not row.get("pet"):
            out["begun"][row["n"]] += 1
        elif kind == "cast" and row["n"] in build and not row.get("pet") and row.get("castTime"):
            out["finished"][row["n"]] += 1
        elif kind == "stop" and row["n"] in build and not row.get("pet"):
            why = row["why"]
            if why == "attacked" and row.get("by"):
                why = "attacked by " + (build.get(row["by"], row["by"]).split("-")[0] + (" pet" if row.get("byPet") else ""))
            out["stopped"][row["n"]][why] += 1
        elif kind == "heal" and row.get("n") in build and row.get("d") in build and row["asked"] >= 100:
            # A heal that was cast: the small ticks of regeneration buffs and lifetaps are left out.
            out["healed"][row["n"]] += row["got"]
            out["wasted"][row["n"]] += row["asked"] - row["got"]
            if trained.get(team[row["d"]]) == row["d"]:
                out["on_trained"][row["n"]] += row["got"]
        elif kind == "wake" and row.get("n") in team and row.get("byTeam") and row["byTeam"] != row["team"]:
            # The group that broke the mez: on its own kill target (meant), or on somebody else (a mistake).
            out["wakes"][(row["byTeam"], "its kill target" if kill.get(row["byTeam"]) == row["n"] else "somebody else")] += 1
        elif kind == "death" and row["n"] in team and trained.get(team[row["n"]]) == row["n"]:
            out["trained_died"][team[row["n"]]] += 1
    for d in match["decs"]:
        if (d.get("pick") or {}).get("kind") == "Trained":
            out["trained_calls"][d["team"]] += 1
    return out


def pct(part, whole):
    return f"{100 * part / whole:3.0f}%" if whole else "  - "


def median(values):
    return statistics.median(values) if values else float("nan")


def report(results):
    if not results:
        print("No recorded matches found.")
        return {}
    head = results[0]["head"]
    print(f"Batch {head['batch']}: {head['a']} (A) against {head['b']} (B), realm level {head['realmLevel']}, {len(results)} matches\n")
    print(f"{'match':>5} {'winner':10} {'ended by':11} {'length':>6} {'buffing':>8} {'contact':>7} {'alive A/B':>9} {'deaths':>6} {'raised':>6} {'power at contact':>16}")
    wins = collections.Counter()
    for r in results:
        e = r["end"] or {}
        wins[e.get("Winner", "unfinished")] += 1
        ready = r.get("ready")
        buffing = "-" if not ready else f"{ready['seconds']}s" + ("!" if ready["timedOut"] else "")
        had = sum(r["start_power"].get(n, 0) for n, b in r["bots"].items() if b["pow"])
        could = sum(b["pow"] for b in r["bots"].values() if b["pow"])
        print(f"{r['head']['number']:>5} {e.get('Winner', 'unfinished'):10} {e.get('End', '-'):11} {e.get('Seconds', 0):>5}s {buffing:>8} "
              f"{e.get('SecondsToContact', -1):>6}s {str(e.get('AliveA', '-')) + '/' + str(e.get('AliveB', '-')):>9} {len(r['deaths']):>6} {r['raised']:>6} {pct(had, could):>16}")
        if ready and ready["timedOut"]:
            print(f"      ready check timed out; still buffing: {', '.join(r['bots'][n]['build'] for n in ready['notReady'] if n in r['bots'])}")
    print("\nwins:", dict(wins))
    released = sum(r["released"] for r in results)
    if released:
        print(f"WARNING, {released} dead bots stood up away from their corpse (released to a bind point): "
              "winners, survivors and every figure below are not to be trusted.")
    outside = collections.Counter()
    for r in results:
        outside.update(r["outside"])
    if outside:
        print("WARNING, damage from outside the two groups (guards, monsters): " + ", ".join(f"{k} {v}" for k, v in outside.most_common(6)))

    # Per build, summed over the batch (two Clerics of different builds stay apart; two Blademasters merge).
    total = collections.defaultdict(collections.Counter)
    spell_total = collections.defaultdict(collections.Counter)
    for r in results:
        for name, c in r["stats"].items():
            key = (r["bots"][name]["team"], r["bots"][name]["build"])
            total[key].update({k: v for k, v in c.items() if k != "end_pow"})
            total[key]["end_pow_sum"] += c.get("end_pow", 0)
            total[key]["max_pow_sum"] += r["bots"][name]["pow"]
            total[key]["n"] += 1
            spell_total[key].update(r["spells"][name])
    print(f"\n{'':2}{'build':17} {'dealt':>7} {'taken':>7} {'mezzed':>6} {'stun':>5} {'idle':>5} {'cast':>5} {'dead':>5} {'no pow':>6} "
          f"{'pow left':>8} {'casts':>5} {'heal':>4} {'ctrl':>4}  most used spell types")
    summary = {}
    for (team, build), c in sorted(total.items()):
        top = ", ".join(f"{k} {v}" for k, v in spell_total[(team, build)].most_common(4))
        print(f"{team} {build:17} {c['dealt']:>7} {c['taken']:>7} {pct(c['mez'], c['alive']):>6} {pct(c['stun'], c['alive']):>5} "
              f"{pct(c['idle'], c['alive']):>5} {pct(c['casting'], c['alive']):>5} {pct(c['dead'], c['seconds']):>5} "
              f"{pct(c['no_power'], c['alive']):>6} {pct(c['end_pow_sum'], c['max_pow_sum']):>8} "
              f"{c['casts']:>5} {c['casts_heal']:>4} {c['casts_control']:>4}  {top}")
        summary[f"{team} {build}"] = dict(c)
    pick_total = collections.defaultdict(collections.Counter)
    score_total = collections.defaultdict(collections.Counter)
    block_total = collections.defaultdict(collections.Counter)
    for r in results:
        for name in r["picks"]:
            key = (r["bots"][name]["team"], r["bots"][name]["build"])
            pick_total[key].update(r["picks"][name])
            score_total[key].update(r["scores"][name])
            block_total[key].update(r["blocks"][name])
    if any(pick_total.values()):
        print("\nSquad-brain decisions (casts picked by kind with their average score; 'nothing' = a turn, sampled every 2 s, with no cast):")
        for key in sorted(pick_total):
            if not pick_total[key]:
                continue
            p = pick_total[key]
            chosen = ", ".join(f"{k} {v}" + (f" (avg {score_total[key][k] / v:.0f})" if k != "nothing" else "") for k, v in p.most_common())
            print(f"{key[0]} {key[1]:17} {chosen}")
            if block_total[key]:
                print(f"{'':20}in the way: " + ", ".join(f"{k} x{v}" for k, v in block_total[key].most_common(6)))
            summary[f"{key[0]} {key[1]}"]["picks"] = dict(p)
    for team in "AB":
        t = collections.Counter()
        for (tm, _), c in total.items():
            if tm == team:
                t.update(c)
        print(f"\nteam {team}: dealt {t['dealt']}, taken {t['taken']}, mezzed {pct(t['mez'], t['alive']).strip()} of the fight, "
              f"stunned {pct(t['stun'], t['alive']).strip()}, idle {pct(t['idle'], t['alive']).strip()}, "
              f"mezzed while holding mez immunity {t['mez_while_immune']}s, hits on own side {t['friendly_fire']}")
        landed = sum(r["pressure"][team] for r in results)
        print(f"        attacks landing on its healers and casters: {landed / len(results):.0f} a match")
        asleep = [r["asleep10"][team] for r in results]
        print(f"        asleep ten seconds after contact: {sum(asleep) / len(results):.1f} a match ({', '.join(str(a) for a in asleep)})")
        landed = [r["opening_mez"][team] for r in results]
        catches = [r["largest_catch"][team] for r in results]
        print(f"        mezzes landing on it in those ten seconds: {sum(landed) / len(results):.1f} a match; "
              f"most caught by one area mez: {max(catches)} ({', '.join(str(c) for c in catches)})")
        calls = sum(r["calls"][team] for r in results)
        moved = sum(r["moved_heals"][team] for r in results)
        if calls or moved:
            print(f"        kill-target calls: {calls / len(results):.1f} a match; heals sent to another patient because one was already on its way: {moved / len(results):.1f} a match")
        guards = collections.Counter()
        for r in results:
            guards.update({k: v for k, v in r["guards"].items() if k[0] == team})
        for (_, guard, ward), count in guards.most_common(4):
            print(f"        guard: {guard} took {count / len(results):.1f} attacks a match meant for {ward}")
        # Where the group stands, from the once-a-second positions.
        near = [d for r in results for d in r["fight"]["near"][team]]
        spacing = [d for r in results for d in r["fight"]["spacing"][team]]
        if near:
            print(f"        its healers and casters: nearest enemy fighter {median(near):.0f} units away (median), within 500 units "
                  f"{pct(sum(1 for d in near if d < 500), len(near)).strip()} of the time, within 1,000 {pct(sum(1 for d in near if d < 1000), len(near)).strip()}; "
                  f"nearest other healer or caster of the group {median(spacing):.0f} units away")
        alive = sum(r["fight"]["soft_alive"][team] for r in results)
        if any(r["fight"]["format3"] for r in results):
            ns = sum(r["fight"]["ns"][team] for r in results)
            seconds = sum(r["fight"]["trained_seconds"][team] for r in results)
            calls = sum(r["fight"]["trained_calls"][team] for r in results)
            died = sum(r["fight"]["trained_died"][team] for r in results)
            meant = sum(r["fight"]["wakes"][(team, "its kill target")] for r in results)
            mistake = sum(r["fight"]["wakes"][(team, "somebody else")] for r in results)
            print(f"        its healers and casters were nearsighted {pct(ns, alive).strip()} of the time; a member had the enemy's fighters on it "
                  f"{seconds / len(results):.0f} s a match ({calls / len(results):.1f} times), and died while it did {died / len(results):.1f} times a match")
            print(f"        mezzes it broke: {meant / len(results):.1f} a match on its kill target, {mistake / len(results):.1f} on somebody else")
            read = collections.Counter()
            for r in results:
                read.update({k[1]: v for k, v in r["fight"]["read"].items() if k[0] == team})
            print(f"        the three readings: its fighters were mezzed, stunned, rooted or slowed {pct(read['fighter held'], read['fighter']).strip()} of the time; "
                  f"its healers and casters were free to cast {pct(read['caster free'], read['caster']).strip()} of the time; "
                  f"its healers were free to heal {pct(read['healer free'], read['healer']).strip()} of the time they lived, and dead "
                  f"{pct(read['healer seconds'] - read['healer'], read['healer seconds']).strip()} of the fight")
            order = collections.Counter()
            for r in results:
                order.update({k[1]: v for k, v in r["fight"]["order"].items() if k[0] == team})
            if order:
                print("        its order: " + ", ".join(f"{name.lower()} {pct(seconds, sum(order.values())).strip()}" for name, seconds in order.most_common()) + " of the fight")
    apart = [d for r in results for d in r["fight"]["apart"]]
    if apart:
        print(f"\nThe two groups' middles are {median(apart):.0f} units apart during the fight (median); under 500 units {pct(sum(1 for d in apart if d < 500), len(apart)).strip()} of the time.")
    if any(r["fight"]["format3"] for r in results):
        cast_total = collections.defaultdict(collections.Counter)
        stop_total = collections.defaultdict(collections.Counter)
        for r in results:
            f = r["fight"]
            for name, b in r["bots"].items():
                key = (b["team"], b["build"])
                cast_total[key]["begun"] += f["begun"][name]
                cast_total[key]["finished"] += f["finished"][name]
                cast_total[key]["healed"] += f["healed"][name]
                cast_total[key]["wasted"] += f["wasted"][name]
                cast_total[key]["on_trained"] += f["on_trained"][name]
                cast_total[key]["n"] += 1
                stop_total[key].update(f["stopped"][name])
        print("\nCasts with a cast time, per match: begun, finished, and what stopped the rest. Health restored by heals of 100 or more, what of them was wasted on a target already full, and the share that went to the member the enemy's fighters were on.")
        print(f"{'':2}{'build':17} {'begun':>6} {'finished':>8}  {'healed':>7} {'wasted':>7} {'on trained':>10}  stopped by")
        for key in sorted(cast_total):
            c = cast_total[key]
            if not c["begun"] and not c["healed"]:
                continue
            n = c["n"]
            why = ", ".join(f"{k} {v / n:.1f}" for k, v in stop_total[key].most_common(5))
            print(f"{key[0]} {key[1]:17} {c['begun'] / n:6.1f} {c['finished'] / n:8.1f}  {c['healed'] / n:7.0f} {c['wasted'] / n:7.0f} "
                  f"{pct(c['on_trained'], c['healed']):>10}  {why}")
    return {"batch": head["batch"], "matches": len(results), "wins": dict(wins), "builds": summary}


def main(argv):
    args = [a for a in argv[1:] if not a.startswith("--")]
    batch = argv[argv.index("--batch") + 1] if "--batch" in argv else None
    out = argv[argv.index("--json") + 1] if "--json" in argv else None
    args = [a for a in args if a not in (batch, out)]
    if not args:
        print(__doc__)
        return 2
    files = []
    for a in args:
        files += sorted(glob.glob(os.path.join(a, "*.jsonl"))) if os.path.isdir(a) else [a]
    if batch:
        files = [f for f in files if os.path.basename(f).startswith(batch)]
    by_batch = collections.defaultdict(list)
    for f in files:
        m = load(f)
        if m:
            by_batch[m["head"]["batch"]].append(analyze(m))
    summaries = []
    for name in sorted(by_batch):
        summaries.append(report(sorted(by_batch[name], key=lambda r: r["head"]["number"])))
        print("\n" + "-" * 100 + "\n")
    if out:
        with open(out, "w", encoding="utf-8") as handle:
            json.dump(summaries, handle, indent=2)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
