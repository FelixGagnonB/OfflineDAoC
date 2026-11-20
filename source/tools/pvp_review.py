"""Print one recorded scrim match second by second, to read a fight like a replay in text.

    python pvp_review.py <recording.jsonl> [--from S] [--to S] [--bot <build or name>]

Times are seconds from first contact (negative before it). Each second lists what happened in
it: the groups' kill target and trained member when they change, casts begun, finished and
stopped (with why), mezzes and stuns that landed, mezzes broken, heals, damage taken, deaths.
Every five seconds a line says how far apart the groups stand and how close the nearest enemy
fighter is to each side's healers and casters.

--bot limits it to one bot (a build such as healer-pac, or a bot's name) and adds that bot's
state every second: health, power, what it is doing, who it is on, the nearest enemy fighter.

Recordings older than format 3 have no "begin", "stop", "heal", "wake" or "squad" lines, so
those parts are simply absent for them. pvp_analyze.py gives the figures over a whole batch.
"""
import collections
import json
import math
import sys

BACKLINE = {"Cleric", "Friar", "Druid", "Bard", "Healer", "Shaman", "Sorcerer", "Theurgist", "Cabalist", "Wizard",
            "Runemaster", "Spiritmaster", "Eldritch", "Enchanter", "Mentalist"}


def main(argv):
    args = argv[1:]
    if not args or args[0] in ("-h", "--help"):
        print(__doc__)
        return 2
    path = args[0]
    start = float(args[args.index("--from") + 1]) if "--from" in args else -5
    stop = float(args[args.index("--to") + 1]) if "--to" in args else 1e9
    only = args[args.index("--bot") + 1] if "--bot" in args else None

    rows = [json.loads(line) for line in open(path, encoding="utf-8") if line.strip()]
    head = rows[0]
    bots = {b["n"]: b for b in head["bots"]}
    # Two bots of one build in a team (two Blademasters) are told apart by a number.
    label, seen = {}, collections.Counter()
    for b in head["bots"]:
        seen[(b["team"], b["build"])] += 1
    counter = collections.Counter()
    for b in head["bots"]:
        key = (b["team"], b["build"])
        counter[key] += 1
        label[b["n"]] = f"{b['team']} {b['build']}" + (f" #{counter[key]}" if seen[key] > 1 else "")

    def who(name, pet=None):
        return label.get(name, str(name)) + (" pet" if pet else "")

    focus = None
    if only:
        focus = next((n for n in bots if n == only or bots[n]["build"] == only), None)
        if focus is None:
            print(f"No bot named or built '{only}' in this match. Builds: {', '.join(sorted({b['build'] for b in bots.values()}))}")
            return 1

    contact = next((r["t"] for r in rows if r["k"] == "snap" and r.get("combat")), None)
    if contact is None:
        print("The two groups never fought in this recording.")
        return 1
    end = next((r for r in rows if r["k"] == "end"), None)
    print(f"{head['batch']} match {head['number']}: {head['a']} (A) against {head['b']} (B), realm level {head['realmLevel']}; "
          + (f"{end['Winner']} by {end['End']} after {end['Seconds']} s, alive {end['AliveA']}/{end['AliveB']}" if end else "unfinished"))
    print("Times are seconds from first contact.\n")

    events = collections.defaultdict(list)       # second -> lines
    damage = collections.defaultdict(lambda: collections.Counter())   # second -> (attacker, target) -> damage
    healing = collections.defaultdict(lambda: collections.Counter())
    snaps = collections.defaultdict(dict)
    state = {"A": (None, None, None), "B": (None, None, None)}

    def involved(*names):
        return focus is None or focus in names

    for r in rows:
        k = r["k"]
        if "t" not in r:
            continue
        second = math.floor((r["t"] - contact) / 1000)
        if k == "snap":
            snaps[second].setdefault(r["n"], r)
        elif k == "squad":
            now = (r.get("kill"), r.get("trained"), r.get("order"))
            if now != state[r["team"]]:
                old = state[r["team"]]
                state[r["team"]] = now
                parts = []
                if now[0] != old[0]:
                    parts.append(f"kill target {who(now[0]) if now[0] else 'none'}")
                if now[1] != old[1]:
                    parts.append(f"trained member {who(now[1]) if now[1] else 'none'}")
                if now[2] != old[2] and now[2] and (old[2] or now[2] != "Engage"):
                    own, theirs = r.get("own") or {}, r.get("theirs") or {}
                    parts.append(f"order {now[2].upper()} (own fighters held {own.get('fh')}, casters free {own.get('cf')}, healers free {own.get('hf')}; "
                                 f"theirs {theirs.get('fh')}, {theirs.get('cf')}, {theirs.get('hf')})")
                if not parts:
                    continue
                if focus is None or focus in now or focus in old:
                    events[second].append(f"group {r['team']}: " + ", ".join(parts))
        elif k == "call" and involved(r.get("d")) and not any(x["k"] == "squad" for x in rows[:50]):
            events[second].append(f"group {r['team']} calls {who(r['d'])} (score {r['score']})")
        elif k == "begin" and involved(r["n"], r.get("d")):
            events[second].append(f"{who(r['n'], r.get('pet'))} begins {r['spell']} on {who(r['d'])} ({r['castTime'] / 1000:.1f} s)")
        elif k == "stop" and involved(r["n"], r.get("by")):
            by = f" by {who(r['by'], r.get('byPet'))}" if r.get("by") else ""
            events[second].append(f"{who(r['n'], r.get('pet'))} loses {r['spell']}: {r['why']}{by}")
        elif k == "cast" and involved(r["n"], r.get("d")) and (r.get("castTime") or r["st"] in ("Mesmerize", "Stun", "Amnesia", "CureMezz")):
            events[second].append(f"{who(r['n'], r.get('pet'))} casts {r['spell']}" + (f" on {who(r['d'])}" if r.get("d") else ""))
        elif k == "hit":
            if r.get("st") in ("Mesmerize", "Mez", "Stun", "StyleStun", "Nearsight") and r.get("at") != r.get("dt") and involved(r["a"], r["d"]):
                landed = "resisted by" if r["res"] in ("Missed", "Resisted") else "lands on"
                events[second].append(f"{r['st']} from {who(r['a'], r.get('pet'))} {landed} {who(r['d'])}")
            amount = (r.get("dmg") or 0) + (r.get("crit") or 0)
            if amount > 0 and r.get("at") != r.get("dt") and involved(r["a"], r["d"]):
                damage[second][(who(r["a"], r.get("pet")), who(r["d"]))] += amount
            if r.get("guardFor") and involved(r["d"], r["guardFor"]):
                events[second].append(f"{who(r['d'])} takes on its shield a blow meant for {who(r['guardFor'])}")
        elif k == "heal" and r.get("n") and involved(r["n"], r["d"]):
            healing[second][(who(r["n"]), who(r["d"]), r["asked"] - r["got"])] += r["got"]
        elif k == "wake" and involved(r["n"], r.get("by")):
            events[second].append(f"{who(r['n'])} is woken by {who(r['by'], r.get('pet'))} ({r.get('spell') or r.get('style') or r.get('type')})")
        elif k == "death" and involved(r["n"], r.get("by")):
            events[second].append(f"*** {who(r['n'])} dies, killed by {who(r['by'], r.get('pet'))}")
        elif k == "dec" and r.get("pick") and focus == r["n"]:
            p = r["pick"]
            events[second].append(f"  decides {p['kind']}" + (f" {p['spell']}" if p.get("spell") else "") + (f" on {who(p['d'])}" if p.get("d") else "")
                                  + f" (score {p.get('score')})")
        elif k == "dec" and not r.get("pick") and focus == r["n"]:
            events[second].append(f"  decides nothing: {r.get('blocked')}")

    def nearest_fighter(name, alive):
        here = (alive[name]["x"], alive[name]["y"])
        foes = [n for n in alive if bots[n]["team"] != bots[name]["team"] and bots[n]["cls"] not in BACKLINE]
        return min((math.dist(here, (alive[f]["x"], alive[f]["y"])) for f in foes), default=None)

    for second in sorted(set(snaps) | set(events) | set(damage) | set(healing)):
        if second < start or second > stop:
            continue
        lines = list(events[second])
        for (healer, target, wasted), got in healing[second].items():
            lines.append(f"{healer} heals {target} for {got}" + (f" ({wasted} wasted)" if wasted > 0 else ""))
        taken = collections.Counter()
        for (attacker, target), amount in damage[second].items():
            taken[target] += amount
        for target, amount in taken.most_common(3):
            by = ", ".join(a for (a, tg), _ in damage[second].most_common() if tg == target)
            lines.append(f"{target} takes {amount} from {by}")
        alive = {n: s for n, s in snaps[second].items() if s["alive"]}
        if focus and focus in snaps[second]:
            s = snaps[second][focus]
            doing = ("DEAD" if not s["alive"] else "mezzed" if s["mez"] else "stunned" if s["stun"]
                     else f"casting {s['casting']}" if s["casting"] else "moving" if s["moving"] else "standing")
            flags = ", ".join(name for name, on in (("nearsighted", s.get("ns")), ("diseased", s.get("dis")), ("interrupted", s.get("intr")), ("rooted", s.get("root"))) if on)
            near = nearest_fighter(focus, alive) if focus in alive else None
            lines.insert(0, f"[{label[focus]}: health {s['hp']}, power {s['pow']}, {doing}"
                            + (f", {flags}" if flags else "") + (f", on {who(s['aim'])}" if s.get("aim") else "")
                            + (f", nearest enemy fighter {near:.0f}" if near is not None else "") + "]")
        if focus is None and second % 5 == 0 and alive:
            a = [(s["x"], s["y"]) for n, s in alive.items() if bots[n]["team"] == "A"]
            b = [(s["x"], s["y"]) for n, s in alive.items() if bots[n]["team"] == "B"]
            if a and b:
                middle = math.dist((sum(p[0] for p in a) / len(a), sum(p[1] for p in a) / len(a)),
                                   (sum(p[0] for p in b) / len(b), sum(p[1] for p in b) / len(b)))
                parts = [f"groups {middle:.0f} apart, alive {len(a)}/{len(b)}"]
                for side in "AB":
                    near = [d for d in (nearest_fighter(n, alive) for n in alive if bots[n]["team"] == side and bots[n]["cls"] in BACKLINE) if d is not None]
                    asleep = sum(1 for n, s in alive.items() if bots[n]["team"] == side and s["mez"])
                    if near:
                        parts.append(f"{side}: nearest enemy fighter to its healers and casters {min(near):.0f} to {max(near):.0f}, {asleep} asleep")
                lines.insert(0, "[" + "; ".join(parts) + "]")
        if lines:
            print(f"{second:+4d}s  " + "\n       ".join(lines))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
