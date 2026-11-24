"""The fights of the player's group in a recorded evening, one by one (docs/PVP-8V8-GUIDE.md).

    python pvp_myfights.py <recording.jsonl>             a line for each fight
    python pvp_myfights.py <recording.jsonl> last        the last fight in full
    python pvp_myfights.py <recording.jsonl> 3           the third fight in full
    python pvp_myfights.py <recording.jsonl> all         every fight in full
    python pvp_myfights.py <recording.jsonl> last --team "Mag Mell Wanderers"    the same for a team of bots

A fight is the time the group had somebody to fight, with gaps of under 15 seconds closed. In
full: who was against it (kind of team, level of play, realm rank), who of its realm came by and
whether it joined, the damage and the healing, who died when and by whom, what the group was
ordered and whether it fell back, what the enemy groups did (fell back, turned on somebody, broke
off, left a fight no longer clean), whom they called, what the player called (/8v8 call), and how
long each side was held by control.
"""
import collections
import json
import sys

KINDS = {"Bad": "bad", "Clingy": "casual, stays near others", "Adder": "casual 8v8, adds", "Decent": "decent", "Strong": "strong",
         "Elite": "elite", "Hunter": "elite, hunts packs"}
LEVELS = {"Bad": "bad", "Clingy": "casual", "Adder": "casual", "Decent": "seasoned", "Strong": "sharp", "Elite": "elite", "Hunter": "elite"}
QUIET = {"pay", "leg", "setoff", "wait", "out", "logoff", "hide", "rest", "standup", "turn", "chat", "company", "port", "join"}


def rank(level):
    return f"{int(level) // 10 + 1}L{int(level) % 10}"


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    if not args:
        print(__doc__)
        return 1
    path, which = args[0], (args[1] if len(args) > 1 else None)
    me = sys.argv[sys.argv.index("--team") + 1] if "--team" in sys.argv else None

    teams, kinds, bots = {}, {}, {}
    rows = []
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            try:
                row = json.loads(line)
            except ValueError:
                continue
            k = row.get("k")
            if k == "roam":
                for team in row["teams"]:
                    teams[team["name"]] = team
                    kinds[team["name"]] = team.get("kind")
                    if me is None and team.get("player"):
                        me = team["name"]
                # (The player's seven are in the header: they are in the world when the recording begins.)
                for bot in row.get("bots") or []:
                    bots[bot["n"]] = (bot["team"], bot["cls"], bot.get("rl", 0))
            elif k == "spawn":
                for bot in row["bots"]:
                    bots[bot["n"]] = (bot["team"], bot["cls"], bot.get("rl", 0))
            elif k == "ev" and isinstance(row.get("ev"), dict) and row["ev"].get("what") == "join":
                kinds[row["ev"]["team"]] = row["ev"].get("kind")
                teams.setdefault(row["ev"]["team"], {"name": row["ev"]["team"], "realm": row["ev"].get("realm")})
                rows.append(row)
            elif k in ("team", "ev", "hit", "heal", "death", "squad", "call", "snap"):
                rows.append(row)
            elif k == "dec" and str((row.get("pick") or {}).get("kind", "")).startswith("Player"):
                rows.append(row)
    if me is None or me not in teams:
        print("No team of a player in this recording; name one with --team.")
        return 1
    # (The server's enum writes its other names for Albion and Hibernia.)
    realm_of = lambda name: {"_FirstPlayerRealm": "Albion", "_LastPlayerRealm": "Hibernia"}.get((teams.get(name) or {}).get("realm"), (teams.get(name) or {}).get("realm"))
    cls = lambda name: bots.get(name, (None, "?", 0))[1]

    def describe(name):
        kind = kinds.get(name)
        levels = [v[2] for v in bots.values() if v[0] == name]
        what = f"{KINDS[kind]}, plays at {LEVELS[kind]}" if kind in KINDS else "a player's group" if (teams.get(name) or {}).get("player") else "?"
        return f"{name} ({realm_of(name)}, {what}" + (f", rank {rank(sum(levels) / len(levels))}" if levels else "") + ")"

    # The fights: the spans in which the group had somebody to fight.
    spans = []
    for row in rows:
        if row["k"] == "team" and row["name"] == me and row.get("fighting"):
            if spans and row["t"] - spans[-1][1] < 15_000:
                spans[-1][1] = row["t"]
            else:
                spans.append([row["t"], row["t"]])
    if not spans:
        print(f"{me} fought nobody in this recording.")
        return 0
    wanted = range(len(spans)) if which == "all" else [len(spans) - 1] if which == "last" else [int(which) - 1] if which else []
    print(f"{describe(me)}: {len(spans)} fights")

    for index, (start, end) in enumerate(spans):
        start, end = start - 5_000, end + 3_000
        inside = [row for row in rows if start <= row["t"] <= end]
        met = collections.Counter()
        for row in inside:
            if row["k"] == "hit":
                if row["at"] == me and row.get("dt"):
                    met[row["dt"]] += 1
                if row.get("dt") == me and row.get("at"):
                    met[row["at"]] += 1
            elif row["k"] == "team" and row["name"] == me:
                for other in row.get("fighting") or []:
                    met[other] += 0
        enemies = [name for name in met if name != me and realm_of(name) != realm_of(me)]
        deaths = [row for row in inside if row["k"] == "death"]
        mine_dead = [row for row in deaths if row["team"] == me]
        killed = [row for row in deaths if row["team"] in enemies and row.get("byTeam") == me]
        alive = [row["alive"] for row in inside if row["k"] == "team" and row["name"] == me]
        zone = next((row.get("zone") for row in inside if row["k"] == "team" and row["name"] == me), None)
        wiped = any(row["k"] == "ev" and (row.get("ev") or {}).get("what") == "wipe" and row["ev"].get("team") == me for row in inside)
        print(f"\nfight {index + 1}: {start // 1000:,} s to {end // 1000:,} s ({(end - start) // 1000} s), {zone}; "
              f"{'wiped' if wiped else f'{alive[-1] if alive else 0} standing at the end'}; killed {len(killed)}, lost {len(mine_dead)}; "
              f"against {len(enemies)} {'group' if len(enemies) == 1 else 'groups'}")
        if index not in wanted:
            print("  against: " + ", ".join(f"{name} ({KINDS.get(kinds.get(name), '?')})" for name in enemies))
            continue
        for name in enemies:
            print("  against " + describe(name))
        # Its realm's groups that came by.
        for row in inside:
            ev = row.get("ev") if row["k"] == "ev" else None
            if isinstance(ev, dict) and ev.get("what") in ("draw", "evens") and ev.get("mate") == me:
                print(f"  {row['t'] // 1000:,} s  {describe(ev['team'])} came by: " + ("it joined" if ev.get("joins", True) else "it left you to it"))
        dealt, taken, dealers, victims = collections.Counter(), collections.Counter(), collections.Counter(), collections.Counter()
        friends = collections.Counter()
        healed = 0
        for row in inside:
            if row["k"] == "hit" and row.get("dmg", 0) > 0:
                if row["at"] == me and row.get("dt") in enemies:
                    dealt[row["dt"]] += row["dmg"]
                    dealers[row["a"]] += row["dmg"]
                elif row.get("dt") == me and row["at"] in enemies:
                    taken[row["at"]] += row["dmg"]
                    victims[row["d"]] += row["dmg"]
                elif row.get("dt") in enemies and realm_of(row["at"]) == realm_of(me):
                    friends[row["at"]] += row["dmg"]
            elif row["k"] == "heal" and row["team"] == me:
                healed += row.get("got", 0)
        print(f"  damage dealt {sum(dealt.values()):,} ({', '.join(f'{v:,} on {n}' for n, v in dealt.most_common())})")
        print(f"  damage taken {sum(taken.values()):,} ({', '.join(f'{v:,} from {n}' for n, v in taken.most_common())}); healed {healed:,}")
        if friends:
            print("  its realm's other groups on the same enemies: " + ", ".join(f"{n} {v:,}" for n, v in friends.most_common()))
        print("  dealt by: " + ", ".join(f"{n} ({cls(n)}) {v:,}" for n, v in dealers.most_common()))
        print("  taken by: " + ", ".join(f"{n} ({cls(n)}) {v:,}" for n, v in victims.most_common()))
        print("  deaths:")
        for row in deaths:
            if row["team"] == me or row["team"] in enemies:
                print(f"    {row['t'] / 1000:8,.1f} s  {'yours ' if row['team'] == me else 'theirs'}  {row['n']} ({cls(row['n'])}, {row['team']}) by {row.get('by')} ({row.get('byTeam')})")
        # What the group was ordered, and whether it fell back.
        orders, last = [], None
        kite = collections.Counter()
        for row in inside:
            if row["k"] != "squad":
                continue
            if row["team"] == me:
                if row["order"] != last:
                    last = row["order"]
                    orders.append(f"{row['t'] // 1000:,} s {last}")
                if row.get("kite"):
                    kite["s"] += 1
                    kite["legs"] = max(kite["legs"], row.get("legs", 0))
                    kite["turned"] += 1 if row.get("prey") else 0
        print("  orders: " + ", ".join(orders))
        # What the player called (/8v8 call), and whether the caller was alive.
        called = []
        down = {row["n"]: row["t"] for row in deaths if row["team"] == me}
        for row in inside:
            if row["k"] == "dec" and row["team"] == me:
                pick = row["pick"]
                word = {"PlayerCall": pick.get("order"), "PlayerRetreat": "retreat", "PlayerRest": "rest", "PlayerAssist": f"assist {pick.get('d')}",
                        "PlayerControl": f"control {pick.get('d')}"}.get(pick["kind"], pick["kind"])
                caller = pick.get("by") or (pick.get("d") if pick["kind"] in ("PlayerCall", "PlayerRetreat", "PlayerRest") else None)
                dead = caller in down and down[caller] <= row["t"]
                called.append(f"{row['t'] / 1000:,.1f} s {word}" + (" (the caller was dead)" if dead else ""))
        if called:
            print("  the player called: " + ", ".join(called))
        if kite["s"]:
            print(f"  it fell back as one: {kite['legs']} legs, {kite['turned']} of its seconds turned on somebody who had followed")
        # What each enemy group did.
        for name in enemies:
            did = collections.Counter()
            calls = collections.Counter()
            for row in inside:
                if row["k"] == "squad" and row["team"] == name:
                    did["fell back, s"] += 1 if row.get("kite") else 0
                    did["legs"] = max(did["legs"], row.get("legs", 0) if row.get("kite") else 0)
                    did["turned on somebody, s"] += 1 if row.get("prey") else 0
                    did["running, s"] += 1 if row.get("leaving") else 0
                elif row["k"] == "call" and row["team"] == name and row.get("d") and bots.get(row["d"], (None,))[0] == me or \
                        row["k"] == "call" and row["team"] == name and row.get("d") and (teams.get(me) or {}).get("player") == row.get("d"):
                    calls[f"{row['d']} ({cls(row['d'])})"] += 1
                elif row["k"] == "ev" and isinstance(row.get("ev"), dict) and row["ev"].get("team") == name and row["ev"].get("what") in ("breakoff", "away", "caught", "dirty", "wipe"):
                    did[row["ev"]["what"]] += 1
            words = [f"{k} {v}" for k, v in did.items() if v]
            print(f"  {name}: " + (", ".join(words) if words else "stood and fought") +
                  ("; called " + ", ".join(f"{n} x{v}" for n, v in calls.most_common(6)) if calls else ""))
        # Control.
        held, seen = collections.Counter(), collections.Counter()
        for row in inside:
            if row["k"] == "snap" and row.get("alive"):
                side = "yours" if row["team"] == me else "theirs" if row["team"] in enemies else None
                if side:
                    seen[side] += 1
                    for what in ("mez", "stun", "root", "ns", "dis"):
                        held[side, what] += 1 if row.get(what) else 0
        for side in ("yours", "theirs"):
            if seen[side]:
                print(f"  {side}, share of the time: " + ", ".join(f"{name} {100 * held[side, what] / seen[side]:.0f}%" for what, name in
                                                                  (("mez", "asleep"), ("stun", "stunned"), ("root", "rooted"), ("ns", "nearsighted"), ("dis", "diseased"))))
    return 0


if __name__ == "__main__":
    sys.exit(main())
