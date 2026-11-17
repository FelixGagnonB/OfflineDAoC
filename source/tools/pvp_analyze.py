"""Turn recorded scrim matches into a report (docs/PVP-8V8-ROADMAP.md, M3).

    python pvp_analyze.py <folder or .jsonl files> [--batch <prefix>] [--json out.json]

Reads the JSON-lines files written by the server's PvpRecorder (bots/pvp/PvpRecorder.cs) and
prints, per batch: how the matches ended, and for each side and each build what it dealt, took,
cast and how much of the fight it spent mesmerized, stunned or out of power.

Snapshots are one second apart, so every "time" figure is a count of seconds.
"""
import collections
import glob
import json
import os
import sys

DAMAGE_RESULTS = {"HitUnstyled", "HitStyle"}
HEAL_TYPES = {"Heal", "CombatHeal", "HealOverTime", "SpreadHeal", "FatigueConsumingHeal", "InstantHeal"}
CONTROL_TYPES = {"Mesmerize", "Mez", "Stun", "SpeedDecrease", "StyleStun", "Nearsight", "Amnesia", "Disease", "Confusion"}


def load(path):
    match = {"file": os.path.basename(path), "snaps": [], "hits": [], "casts": [], "deaths": [], "end": None, "head": None}
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            row = json.loads(line)
            kind = row.get("k")
            if kind == "match":
                match["head"] = row
            elif kind == "snap":
                match["snaps"].append(row)
            elif kind == "hit":
                match["hits"].append(row)
            elif kind == "cast":
                match["casts"].append(row)
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
    for h in match["hits"]:
        dmg = (h["dmg"] or 0) + (h["crit"] or 0)
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
    for c in match["casts"]:
        if c["n"] not in stats:
            continue
        s = stats[c["n"]]
        s["casts"] += 1
        s["power_spent"] += c["pow"] or 0
        kind = "heal" if c["st"] in HEAL_TYPES else "control" if c["st"] in CONTROL_TYPES else "other"
        s["casts_" + kind] += 1
        spells[c["n"]][f'{c["st"]}'] += 1
    return {"head": head, "bots": bots, "stats": stats, "spells": spells, "contact": contact,
            "end": match["end"], "deaths": match["deaths"], "file": match["file"]}


def pct(part, whole):
    return f"{100 * part / whole:3.0f}%" if whole else "  - "


def report(results):
    if not results:
        print("No recorded matches found.")
        return {}
    head = results[0]["head"]
    print(f"Batch {head['batch']}: {head['a']} (A) against {head['b']} (B), realm level {head['realmLevel']}, {len(results)} matches\n")
    print(f"{'match':>5} {'winner':10} {'ended by':11} {'length':>6} {'contact':>7} {'alive A/B':>9} {'deaths':>6}")
    wins = collections.Counter()
    for r in results:
        e = r["end"] or {}
        wins[e.get("Winner", "unfinished")] += 1
        print(f"{r['head']['number']:>5} {e.get('Winner', 'unfinished'):10} {e.get('End', '-'):11} {e.get('Seconds', 0):>5}s "
              f"{(r['contact'] or 0) // 1000:>6}s {str(e.get('AliveA', '-')) + '/' + str(e.get('AliveB', '-')):>9} {len(r['deaths']):>6}")
    print("\nwins:", dict(wins))

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
    for team in "AB":
        t = collections.Counter()
        for (tm, _), c in total.items():
            if tm == team:
                t.update(c)
        print(f"\nteam {team}: dealt {t['dealt']}, taken {t['taken']}, mezzed {pct(t['mez'], t['alive']).strip()} of the fight, "
              f"stunned {pct(t['stun'], t['alive']).strip()}, idle {pct(t['idle'], t['alive']).strip()}, "
              f"mezzed while holding mez immunity {t['mez_while_immune']}s, hits on own side {t['friendly_fire']}")
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
