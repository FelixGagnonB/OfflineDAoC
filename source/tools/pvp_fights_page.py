"""A page to look at your fights of an evening: pick a fight, watch it from above, read what happened.

    python pvp_fights_page.py                         the evenings of the game you play (D:\\OfflineDAoC)
    python pvp_fights_page.py --scrim                 the evenings of the copy for trying things
    python pvp_fights_page.py <recording.jsonl>       one evening
    python pvp_fights_page.py <folder of recordings>  every evening in it that has a fight of yours
        [--out <folder>]   where the pages go (default: <game>\\runtime\\logs\\fights)
        [--last N]         only the N newest evenings (default 8)
        [--redraw]         write again the pages that are there already
        [--team "<name>"]  the fights of a team of bots instead of yours
        [--open]           open the list in your browser when it is written

It writes one page for each evening (<recording>.html) and index.html, which lists them, and
says where they are; open index.html in a browser. On an evening's page: a list of your fights
to pick from; the fight seen from above, to play and to drag through (north is up, the grid is
1,000 units); every member's health, power and state at that second; what the recording says of
the fight in plain sentences; the damage and the dead; and every event, each a click away.

Everything comes from the recording the server writes beside itself (logs\\roams). Nothing is
sent anywhere: a page is one file with its data in it. A recording is your game: the pages are
not for the repository.
"""
import collections
import datetime
import glob
import html
import json
import math
import os
import sys

HEALERS = {"Cleric", "Druid", "Healer", "Shaman", "Friar"}
FIGHTERS = {"Armsman", "Paladin", "Mercenary", "Reaver", "Warrior", "Berserker", "Savage", "Thane", "Skald", "Hero", "Champion", "Blademaster",
            "Warden", "Valewalker", "Minstrel", "Infiltrator", "Shadowblade", "Nightshade", "Ranger", "Scout", "Hunter"}
KINDS = {"Bad": "bad", "Clingy": "casual", "Adder": "casual 8v8", "Decent": "decent", "Strong": "strong", "Elite": "elite", "Hunter": "zerg hunters"}
REALMS = {"_FirstPlayerRealm": "Albion", "_LastPlayerRealm": "Hibernia"}
ABILITY_NAMES = {"SpeedOfSound": "Speed of Sound", "GroupPurge": "Group Purge", "MasteryOfConcentration": "Mastery of Concentration",
                 "BatteryOfLife": "Battery of Life", "BunkerOfFaith": "Bunker of Faith", "SoldiersBarricade": "Soldier's Barricade",
                 "BrilliantAura": "Brilliant Aura of Deflection", "AmelioratingMelodies": "Ameliorating Melodies", "FaithHealing": "Faith Healing",
                 "PerfectRecovery": "Perfect Recovery", "AvoidPain": "Avoid Pain", "ArmorOfFaith": "Armor of Faith", "EmptyMind": "The Empty Mind",
                 "MCL": "Mystic Crystal Lore", "RagingPower": "Raging Power", "IgnorePain": "Ignore Pain", "IgnorePainTank": "Ignore Pain",
                 "FirstAid": "First Aid", "SecondWind": "Second Wind", "Purge": "Purge", "PurgeReduced": "Purge"}
BEFORE, AFTER, NEAR = 8, 5, 7_000


def rank(level):
    return f"{int(level) // 10 + 1}L{int(level) % 10}"


def time_of(line):
    at = line.find('"t":')
    if at < 0:
        return -1
    end = at + 4
    while end < len(line) and line[end].isdigit():
        end += 1
    return int(line[at + 4:end] or -1)


def read(path, me=None):
    """The fights of one evening, ready for the page; None when the team fought nobody."""
    teams, bots, mine_rows = {}, {}, []
    player = None
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            kind = line[6:10]
            if kind == "roam":
                row = json.loads(line)
                for team in row["teams"]:
                    teams[team["name"]] = {"realm": REALMS.get(team.get("realm"), team.get("realm")), "kind": team.get("kind"), "player": team.get("player")}
                    if me is None and team.get("player"):
                        me, player = team["name"], team["player"]
                for bot in row.get("bots") or []:
                    bots[bot["n"]] = bot
            elif kind == "spaw":
                for bot in json.loads(line)["bots"]:
                    bots[bot["n"]] = bot
            elif kind == "ev\"," and '"what":"join"' in line:
                ev = json.loads(line)["ev"]
                teams.setdefault(ev["team"], {"realm": REALMS.get(ev.get("realm"), ev.get("realm")), "kind": ev.get("kind"), "player": None})
                teams[ev["team"]]["kind"] = ev.get("kind")
            elif kind == "team" and me is not None and f'"name":{json.dumps(me)}' in line:
                row = json.loads(line)
                mine_rows.append((row["t"], bool(row.get("fighting")), row.get("zone")))
    if me is None or me not in teams:
        return None
    spans = []
    for t, fighting, zone in mine_rows:
        if fighting:
            if spans and t - spans[-1][1] < 15_000:
                spans[-1][1] = t
            else:
                spans.append([t, t, zone])
    spans = [s for s in spans if s[1] - s[0] >= 5_000]
    if not spans:
        return None
    windows = [(s[0] - BEFORE * 1000, s[1] + AFTER * 1000) for s in spans]

    def window_of(t):
        for index, (a, b) in enumerate(windows):
            if a <= t <= b:
                return index
        return -1

    rows = [[] for _ in spans]
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            kind = line[6:10]
            if kind not in ("snap", "squa", "hit\"", "heal", "deat", "dec\"", "ev\",", "team"):
                continue
            index = window_of(time_of(line))
            if index < 0:
                continue
            if kind == "dec\"" and not ('"Player' in line or '"RealmAbility"' in line or '"Purge"' in line or '"GroupPurge"' in line):
                continue
            try:
                rows[index].append(json.loads(line))
            except ValueError:
                pass

    realm = lambda name: (teams.get(name) or {}).get("realm")
    cls = lambda name: (bots.get(name) or {}).get("cls", "?")
    fights = []
    for number, (span, inside) in enumerate(zip(spans, rows), 1):
        start, end = windows[number - 1]
        first, last = start // 1000, end // 1000
        seconds = last - first + 1
        # Who is in it: the enemies it traded blows with or was set against, its realm-mates on the same enemies, and whoever stood near.
        met, centre = set(), {}
        for row in inside:
            if row["k"] == "hit":
                if row["at"] == me and row.get("dt"):
                    met.add(row["dt"])
                if row.get("dt") == me and row.get("at"):
                    met.add(row["at"])
            elif row["k"] == "team":
                centre.setdefault(row["name"], {})[row["t"] // 1000] = (row["x"], row["y"])
                if row["name"] == me:
                    met.update(row.get("fighting") or [])
        enemies = sorted(name for name in met if name != me and realm(name) != realm(me))
        friends = sorted({row["at"] for row in inside if row["k"] == "hit" and row.get("dt") in enemies and row["at"] != me and realm(row["at"]) == realm(me)})
        near = set()
        for name, where in centre.items():
            if name in (me, *enemies, *friends):
                continue
            for second, (x, y) in where.items():
                close = min(((abs(second - s), p) for s, p in centre.get(me, {}).items()), default=None)
                if close and close[0] <= 6 and math.dist((x, y), close[1]) <= NEAR:
                    near.add(name)
                    break
        order = [me, *enemies, *friends, *sorted(near)]
        team_index = {name: i for i, name in enumerate(order)}
        members, member_index = [], {}
        for row in inside:
            if row["k"] == "snap" and row["team"] in team_index and row["n"] not in member_index:
                bot = bots.get(row["n"]) or {}
                member_index[row["n"]] = len(members)
                c = bot.get("cls") or ("?" if row["n"] != player else "You")
                members.append({"n": row["n"], "team": team_index[row["team"]], "cls": c,
                                "role": "H" if c in HEALERS else "F" if c in FIGHTERS else "C", "hp": bot.get("hp") or 0, "pow": bot.get("pow") or 0,
                                "rl": bot.get("rl", 0), "you": row["n"] == player})
        # The player's own class and hit points are not in the header: take the most seen.
        for row in inside:
            if row["k"] == "snap" and row["n"] in member_index:
                m = members[member_index[row["n"]]]
                m["hp"] = max(m["hp"], row.get("hp") or 0)
                m["pow"] = max(m["pow"], row.get("pow") or 0)
        xs = [row["x"] for row in inside if row["k"] == "snap" and row["n"] in member_index]
        ys = [row["y"] for row in inside if row["k"] == "snap" and row["n"] in member_index]
        if not xs:
            continue
        min_x, min_y = min(xs), min(ys)
        frames = [[None] * len(members) for _ in range(seconds)]
        squad = [None] * seconds
        flow = [[0, 0, 0] for _ in range(seconds)]
        events = []
        held = collections.Counter()
        seen = collections.Counter()
        dealt, taken, healing = collections.Counter(), collections.Counter(), collections.Counter()
        by_team_dealt, by_team_taken = collections.Counter(), collections.Counter()
        last_order, kiting = None, False
        used = collections.Counter()
        for row in inside:
            second = row["t"] // 1000 - first
            if not 0 <= second < seconds:
                continue
            k = row["k"]
            if k == "snap":
                i = member_index.get(row["n"])
                if i is None:
                    continue
                m = members[i]
                flags = (1 if row.get("alive") else 0) | (2 if row.get("mez") else 0) | (4 if row.get("stun") else 0) | (8 if row.get("root") else 0) | \
                        (16 if row.get("ns") else 0) | (32 if row.get("dis") else 0) | (64 if row.get("casting") else 0)
                frames[second][i] = [row["x"] - min_x, row["y"] - min_y,
                                     round(100 * (row.get("hp") or 0) / m["hp"]) if m["hp"] else 0,
                                     round(100 * (row.get("pow") or 0) / m["pow"]) if m["pow"] else -1,
                                     row.get("end", 0), flags, member_index.get(row.get("target") or row.get("aim"), -1)]
                if row.get("alive"):
                    side = "mine" if m["team"] == 0 else "theirs" if order[m["team"]] in enemies else None
                    if side:
                        seen[side] += 1
                        seen[side, m["role"]] += 1
                        for what in ("mez", "stun", "root", "ns", "dis"):
                            if row.get(what):
                                held[side, what] += 1
                                if what in ("mez", "stun"):
                                    held[side, "out", m["role"]] += 1
            elif k == "squad" and row["team"] == me:
                squad[second] = [row.get("order") or "", member_index.get(row.get("kill"), -1), 1 if row.get("kite") else 0,
                                 (row.get("own") or {}).get("alive", 0), (row.get("theirs") or {}).get("alive", 0), row.get("on", 0)]
                if row.get("order") != last_order:
                    last_order = row.get("order")
                    events.append([second, "order", f"Order: {last_order}"])
                if bool(row.get("kite")) != kiting:
                    kiting = bool(row.get("kite"))
                    if kiting:
                        events.append([second, "kite", "Your group falls back as one"])
            elif k == "hit" and row.get("dmg", 0) > 0:
                if row["at"] == me and row.get("dt") in enemies:
                    flow[second][0] += row["dmg"]
                    dealt[row["a"]] += row["dmg"]
                    by_team_dealt[row["dt"]] += row["dmg"]
                elif row.get("dt") == me and row["at"] in enemies:
                    flow[second][1] += row["dmg"]
                    taken[row["d"]] += row["dmg"]
                    by_team_taken[row["at"]] += row["dmg"]
            elif k == "heal" and row["team"] == me:
                flow[second][2] += row.get("got", 0)
                healing[row["n"]] += row.get("got", 0)
            elif k == "death" and row["team"] in team_index:
                yours = row["team"] == me
                events.append([second, "dead-mine" if yours else "dead-theirs" if row["team"] in enemies else "dead-other",
                               f"{row['n']} ({cls(row['n'])}, {row['team']}) dies" + (f", killed by {row['by']} ({row.get('byTeam')})" if row.get("by") else ""),
                               member_index.get(row["n"], -1)])
            elif k == "dec":
                pick = row.get("pick") or {}
                kind = pick.get("kind", "")
                if kind.startswith("Player") and row.get("team") == me:
                    word = {"PlayerCall": pick.get("order"), "PlayerRetreat": "retreat", "PlayerRest": "rest", "PlayerAssist": f"assist on {pick.get('d')}",
                            "PlayerControl": f"control on {pick.get('d')}"}.get(kind, kind)
                    if not events or events[-1][1] != "call" or events[-1][2] != f"You call: {word}":
                        events.append([second, "call", f"You call: {word}"])
                elif kind in ("RealmAbility", "Purge", "GroupPurge") and row.get("team") in team_index:
                    name = ABILITY_NAMES.get(pick.get("spell") or kind, pick.get("spell") or kind)
                    yours = row["team"] == me
                    used["mine" if yours else "theirs", name] += 1
                    if yours or (row["team"] in enemies and name in ("Group Purge", "Speed of Sound", "Battery of Life", "Bunker of Faith", "Perfect Recovery", "Faith Healing")):
                        text = f"{row['n']} ({cls(row['n'])}{'' if yours else ', ' + row['team']}) uses {name}"
                        if not any(e[2] == text and second - e[0] <= 3 for e in events[-12:]):
                            events.append([second, "ability-mine" if yours else "ability-theirs", text, member_index.get(row["n"], -1)])
            elif k == "ev":
                ev = row.get("ev")
                if not isinstance(ev, dict):
                    continue
                what = ev.get("what")
                if what in ("inccall", "addcall") and ev.get("team") == me:
                    events.append([second, "say", f"Your group: {ev.get('text')}"])
                elif what == "draw" and ev.get("mate") == me:
                    events.append([second, "draw", f"{ev['team']} comes on your fight: " + ("it joins" if ev.get("joins", True) else "it leaves you to it")])
                elif what == "fight" and ev.get("other") == me:
                    events.append([second, "join", f"{ev['team']} ({realm(ev['team'])}, {KINDS.get((teams.get(ev['team']) or {}).get('kind'), '?')}) is on you"])
                elif what in ("wipe", "breakoff", "away") and ev.get("team") in team_index:
                    name = ev["team"]
                    words = {"wipe": "is wiped", "breakoff": "breaks off and runs", "away": "gets away"}[what]
                    events.append([second, "wipe-mine" if name == me else "wipe", ("Your group" if name == me else name) + " " + words])
        events.sort(key=lambda e: e[0])
        deaths_mine = [e for e in events if e[1] == "dead-mine"]
        deaths_theirs = [e for e in events if e[1] == "dead-theirs"]
        kills = sum(1 for row in inside if row["k"] == "death" and row["team"] in enemies and row.get("byTeam") == me)
        wiped = any(e[1] == "wipe-mine" and "wiped" in e[2] for e in events)
        standing = next((s[3] for s in reversed(squad) if s), None)
        peak = max((s[4] for s in squad if s), default=0)
        most_on = max((s[5] for s in squad if s), default=0)

        # What the recording says of it, in sentences.
        insight = []
        length = span[1] // 1000 - span[0] // 1000
        insight.append(f"{length} seconds against {len(enemies)} enemy group{'s' if len(enemies) != 1 else ''}" +
                       (f" ({peak} of them alive at the most, {most_on} aiming at your group at the most)" if peak else "") +
                       (f", with {len(friends)} group{'s' if len(friends) != 1 else ''} of your realm on the same enemies" if friends else "") + ".")
        total_out, total_in, total_heal = sum(f[0] for f in flow), sum(f[1] for f in flow), sum(f[2] for f in flow)
        insight.append(f"Your group dealt {total_out:,} damage and took {total_in:,}; your healers gave back {total_heal:,}" +
                       (f" ({100 * total_heal // total_in}% of what was taken)." if total_in else "."))
        if deaths_mine:
            e = deaths_mine[0]
            insight.append(f"Your first death came {e[0] - BEFORE} seconds in: {e[2]}." +
                           (f" The last of your {len(deaths_mine)} fell {deaths_mine[-1][0] - e[0]} seconds later." if len(deaths_mine) > 1 else ""))
        else:
            insight.append("Nobody of your group died.")
        if deaths_theirs:
            insight.append(f"The first enemy died {deaths_theirs[0][0] - BEFORE} seconds in; {len(deaths_theirs)} enemies died in all, {kills} of them killed by your group.")
        else:
            insight.append("No enemy died.")
        for side, who in (("mine", "Your members"), ("theirs", "The enemy's members")):
            if seen[side]:
                share = lambda what: 100 * held[side, what] // seen[side]
                insight.append(f"{who} were asleep {share('mez')}% of the time they were alive, stunned {share('stun')}%, rooted {share('root')}%, "
                               f"nearsighted {share('ns')}%, diseased {share('dis')}%.")
        if seen["mine", "H"]:
            insight.append(f"Your healers could not act (asleep or stunned) {100 * held['mine', 'out', 'H'] // seen['mine', 'H']}% of the time.")
        # An enemy fighter within reach of a healer or caster of yours.
        pressed = total = 0
        soft = [i for i, m in enumerate(members) if m["team"] == 0 and m["role"] != "F"]
        hard = [i for i, m in enumerate(members) if order[m["team"]] in enemies and m["role"] == "F"]
        for frame in frames:
            living = [frame[i] for i in soft if frame[i] and frame[i][5] & 1]
            if not living:
                continue
            total += 1
            foes = [frame[i] for i in hard if frame[i] and frame[i][5] & 1 and not frame[i][5] & 6]
            if any(math.dist(a[:2], b[:2]) <= 350 for a in living for b in foes):
                pressed += 1
        if total:
            insight.append(f"An enemy fighter stood on one of your healers or casters {100 * pressed // total}% of the fight.")
        low = []
        for i, m in enumerate(members):
            if m["team"] == 0 and m["role"] == "H":
                final = next((frame[i] for frame in reversed(frames) if frame[i] and frame[i][5] & 1), None)
                if final and final[3] >= 0:
                    low.append(f"{m['n']} {final[3]}%")
        if low:
            insight.append("Power of your healers when they were last alive: " + ", ".join(low) + ".")
        mine_used = [f"{name}{' x' + str(n) if n > 1 else ''}" for (side, name), n in used.most_common() if side == "mine"]
        if mine_used:
            insight.append("Your group pressed: " + ", ".join(mine_used) + ".")
        their_used = [f"{name} x{n}" for (side, name), n in used.most_common() if side == "theirs"]
        if their_used:
            insight.append("The others pressed: " + ", ".join(their_used) + ".")
        changes = sum(1 for e in events if e[1] == "order")
        if changes > 1 and length >= 30:
            insight.append(f"Your group's order changed {changes} times ({changes * 60 / length:.1f} a minute).")
        calls = [e for e in events if e[1] == "call"]
        if calls:
            insight.append("You called: " + ", ".join(f"{e[2][10:]} at {e[0] - BEFORE} s" for e in calls[:10]) + ("..." if len(calls) > 10 else "") + ".")

        result = "wiped" if wiped else f"{standing if standing is not None else '?'} of your group standing at the end"
        label = (f"Fight {number}: {span[0] // 60000}:{span[0] // 1000 % 60:02d} into the evening, {length} s, {span[2] or '?'}; "
                 f"{len(enemies)} enemy group{'s' if len(enemies) != 1 else ''}; your group killed {kills}, lost {len(deaths_mine)}; {result}")
        described = []
        for name in order:
            levels = [m["rl"] for m in members if order[m["team"]] == name and not m["you"]]
            described.append({"name": name, "realm": realm(name) or "?", "kind": KINDS.get((teams.get(name) or {}).get("kind"), "yours" if name == me else "?"),
                              "side": "mine" if name == me else "enemy" if name in enemies else "friend" if name in friends else "near",
                              "rank": rank(sum(levels) / len(levels)) if levels else "?",
                              "dealt": by_team_taken.get(name, 0), "took": by_team_dealt.get(name, 0),
                              "dead": sum(1 for e in events if e[1].startswith("dead") and len(e) > 3 and e[3] >= 0 and members[e[3]]["team"] == team_index[name])})
        fights.append({"label": label, "first": first, "before": BEFORE, "teams": described,
                       "members": [{"n": m["n"], "t": m["team"], "c": m["cls"], "r": m["role"], "y": 1 if m["you"] else 0} for m in members],
                       "frames": frames, "squad": squad, "flow": flow, "events": events, "insight": insight,
                       "dealt": dealt.most_common(), "taken": taken.most_common(), "healing": healing.most_common()})
    if not fights:
        return None
    return {"me": me, "player": player, "realm": realm(me), "fights": fights}


PAGE = r"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>__TITLE__</title>
<style>
:root { --bg:#14161a; --panel:#1d2026; --line:#2c313a; --text:#e6e8eb; --dim:#98a1ad; --alb:#ff5a4d; --mid:#5aa0ff; --hib:#8fe65a; --accent:#ffd24a; }
@media (prefers-color-scheme: light) { :root { --bg:#f4f5f7; --panel:#ffffff; --line:#d6dae0; --text:#1b1e23; --dim:#5d6672; --alb:#d83a2e; --mid:#2f6fd0; --hib:#3d9a14; --accent:#b07a00; } }
* { box-sizing:border-box; }
body { margin:0; background:var(--bg); color:var(--text); font:14px/1.45 system-ui, "Segoe UI", sans-serif; }
header { padding:12px 16px; border-bottom:1px solid var(--line); display:flex; flex-wrap:wrap; gap:10px; align-items:center; }
h1 { font-size:17px; margin:0 12px 0 0; } h2 { font-size:14px; margin:14px 0 6px; color:var(--dim); text-transform:uppercase; letter-spacing:.04em; }
select, button, input[type=range] { font:inherit; color:var(--text); background:var(--panel); border:1px solid var(--line); border-radius:6px; padding:5px 8px; }
select { max-width:100%; flex:1 1 420px; } button { cursor:pointer; } button:hover { border-color:var(--dim); }
main { display:grid; grid-template-columns:minmax(0,1fr) 340px; gap:12px; padding:12px 16px; }
@media (max-width: 1000px) { main { grid-template-columns:1fr; } }
.panel { background:var(--panel); border:1px solid var(--line); border-radius:8px; padding:10px 12px; }
#map { width:100%; height:auto; display:block; border-radius:6px; background:#0d0f12; }
.controls { display:flex; gap:8px; align-items:center; margin-top:8px; flex-wrap:wrap; }
.controls input[type=range] { flex:1 1 200px; padding:0; }
#clock { font-variant-numeric:tabular-nums; min-width:150px; }
label.tick { color:var(--dim); display:inline-flex; gap:4px; align-items:center; }
.row { display:grid; grid-template-columns:110px 1fr 46px; gap:6px; align-items:center; padding:2px 0; cursor:default; }
.row .who { white-space:nowrap; overflow:hidden; text-overflow:ellipsis; } .row .who small { color:var(--dim); }
.bars { display:grid; gap:2px; } .bar { height:7px; background:var(--line); border-radius:3px; overflow:hidden; } .bar i { display:block; height:100%; }
.hp i { background:#58c05a; } .hp.low i { background:#e0a030; } .hp.crit i { background:#e04a3a; } .pow i { background:#5a8fe0; }
.state { font-size:11px; color:var(--accent); text-align:right; white-space:nowrap; } .dead { opacity:.4; }
.team-head { margin-top:8px; font-weight:600; } .Albion { color:var(--alb); } .Midgard { color:var(--mid); } .Hibernia { color:var(--hib); }
ul.plain { margin:0; padding-left:18px; } ul.plain li { margin:3px 0; }
table { border-collapse:collapse; width:100%; } th, td { text-align:left; padding:3px 8px 3px 0; border-bottom:1px solid var(--line); font-variant-numeric:tabular-nums; }
th { color:var(--dim); font-weight:500; }
#events { max-height:320px; overflow:auto; } #events div { padding:2px 4px; border-radius:4px; cursor:pointer; display:grid; grid-template-columns:52px 1fr; gap:6px; }
#events div:hover { background:var(--line); } #events .now { background:var(--line); }
.e-dead-mine { color:var(--alb); } .e-dead-theirs { color:var(--hib); } .e-call { color:var(--accent); } .e-say, .e-order, .e-kite { color:var(--dim); }
#flow { width:100%; height:120px; display:block; background:#0d0f12; border-radius:6px; margin-top:8px; }
.below { padding:0 16px 24px; display:grid; grid-template-columns:repeat(auto-fit, minmax(340px, 1fr)); gap:12px; }
.legend { color:var(--dim); font-size:12px; margin-top:6px; }
</style></head><body>
<header><h1>__TITLE__</h1><select id="pick"></select></header>
<main>
  <div class="panel">
    <canvas id="map" width="1100" height="760"></canvas>
    <div class="controls">
      <button id="play">Play</button>
      <select id="speed" style="flex:0 0 auto"><option value="1">1x</option><option value="2" selected>2x</option><option value="4">4x</option><option value="8">8x</option></select>
      <select id="view" style="flex:0 0 auto" title="What the picture shows"><option value="4000">close, on your group</option><option value="6500" selected>on your group</option><option value="10000">wide, on your group</option><option value="0">the whole fight</option></select>
      <input id="at" type="range" min="0" max="1" value="0" step="1">
      <span id="clock"></span>
    </div>
    <div class="controls">
      <label class="tick"><input type="checkbox" id="names" checked> names (the others' in the close view)</label>
      <label class="tick"><input type="checkbox" id="aims" checked> who your group is on</label>
      <label class="tick"><input type="checkbox" id="trails" checked> trails</label>
      <label class="tick"><input type="checkbox" id="others" checked> groups standing near</label>
      <span id="order" class="legend"></span>
    </div>
    <div class="legend">Square: a fighter. Circle: a caster. Triangle: a healer. White edge: your group; double ring: you. Purple ring: asleep; yellow: stunned; brown: rooted. Red cross-hair: your group's kill target. A cross: dead. Space plays and stops; the arrow keys step a second.</div>
    <canvas id="flow" width="1100" height="120"></canvas>
    <div class="legend">Each second: damage your group dealt (up, green) and took (down, red); the line is how many of your group were alive.</div>
  </div>
  <div class="panel" id="side"></div>
</main>
<div class="below">
  <div class="panel"><h2>What the recording says</h2><ul class="plain" id="insight"></ul></div>
  <div class="panel"><h2>The groups</h2><div id="teams"></div><h2>Your group, member by member</h2><div id="tables"></div></div>
  <div class="panel"><h2>What happened, in order (click to go there)</h2><div id="events"></div></div>
</div>
<script>
const DATA = __DATA__;
const $ = id => document.getElementById(id);
const css = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
let fight = null, second = 0, playing = false, timer = null, box = null, whole = null;
const realmColor = realm => realm === "Albion" ? css("--alb") : realm === "Midgard" ? css("--mid") : realm === "Hibernia" ? css("--hib") : "#999";
const fmt = s => { const sign = s < 0 ? "-" : ""; s = Math.abs(s); return sign + Math.floor(s / 60) + ":" + String(s % 60).padStart(2, "0"); };

function choose(index) {
  fight = DATA.fights[index]; second = 0; stop();
  $("at").max = fight.frames.length - 1; $("at").value = 0;
  let minX = 1e9, minY = 1e9, maxX = -1e9, maxY = -1e9;
  for (const frame of fight.frames) for (const m of frame) if (m) { minX = Math.min(minX, m[0]); maxX = Math.max(maxX, m[0]); minY = Math.min(minY, m[1]); maxY = Math.max(maxY, m[1]); }
  const pad = 400; whole = { x: minX - pad, y: minY - pad, w: maxX - minX + 2 * pad, h: maxY - minY + 2 * pad }; box = whole;
  $("insight").innerHTML = fight.insight.map(line => "<li>" + esc(line) + "</li>").join("");
  $("teams").innerHTML = "<table><tr><th>Group</th><th>Kind</th><th>Rank</th><th>Dealt to you</th><th>Took from you</th><th>Dead</th></tr>" +
    fight.teams.map(t => `<tr><td class="${t.realm}">${esc(t.name)}${t.side === "friend" ? " (with you)" : t.side === "near" ? " (near)" : ""}</td><td>${esc(t.kind)}</td><td>${t.rank}</td><td>${t.side === "enemy" ? t.dealt.toLocaleString() : ""}</td><td>${t.side === "enemy" ? t.took.toLocaleString() : ""}</td><td>${t.dead}</td></tr>`).join("") + "</table>";
  const cls = {}; fight.members.forEach(m => cls[m.n] = m.c);
  const names = [...new Set([...fight.dealt, ...fight.taken, ...fight.healing].map(p => p[0]))];
  const get = (list, n) => { const p = list.find(p => p[0] === n); return p ? p[1].toLocaleString() : ""; };
  $("tables").innerHTML = "<table><tr><th>Member</th><th>Dealt</th><th>Took</th><th>Healed</th></tr>" +
    names.map(n => `<tr><td>${esc(n)} <small>${esc(cls[n] || "")}</small></td><td>${get(fight.dealt, n)}</td><td>${get(fight.taken, n)}</td><td>${get(fight.healing, n)}</td></tr>`).join("") + "</table>";
  $("events").innerHTML = fight.events.map((e, i) => `<div class="e-${e[1]}" data-at="${e[0]}" data-i="${i}"><span>${fmt(e[0] - fight.before)}</span><span>${esc(e[2])}</span></div>`).join("");
  draw();
}
const esc = text => String(text).replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));

function place(m, canvas) {
  const scale = Math.min(canvas.width / box.w, canvas.height / box.h);
  return [(m[0] - box.x) * scale + (canvas.width - box.w * scale) / 2, (m[1] - box.y) * scale + (canvas.height - box.h * scale) / 2, scale];
}

// What the picture shows: the whole fight, or so many units across with your group in the middle.
function view(canvas) {
  const wide = Number($("view").value);
  if (!wide) return whole;
  let mine = [];
  for (let s = second; s >= 0 && !mine.length; s--) {
    const all = fight.members.map((m, i) => m.t === 0 ? fight.frames[s][i] : null).filter(Boolean), living = all.filter(p => p[5] & 1);
    mine = living.length ? living : s === 0 ? all : [];
  }
  if (!mine.length) return whole;
  const cx = mine.reduce((sum, p) => sum + p[0], 0) / mine.length, cy = mine.reduce((sum, p) => sum + p[1], 0) / mine.length, high = wide * canvas.height / canvas.width;
  return { x: cx - wide / 2, y: cy - high / 2, w: wide, h: high };
}

function draw() {
  const canvas = $("map"), g = canvas.getContext("2d"), frame = fight.frames[second], showOthers = $("others").checked;
  box = view(canvas);
  g.clearRect(0, 0, canvas.width, canvas.height);
  const scale = Math.min(canvas.width / box.w, canvas.height / box.h);
  // The grid: 1,000 units.
  g.strokeStyle = "rgba(255,255,255,.07)"; g.fillStyle = "rgba(255,255,255,.35)"; g.lineWidth = 1; g.font = "11px system-ui";
  const offX = (canvas.width - box.w * scale) / 2, offY = (canvas.height - box.h * scale) / 2;
  for (let x = Math.ceil(box.x / 1000) * 1000; x < box.x + box.w; x += 1000) { const px = (x - box.x) * scale + offX; g.beginPath(); g.moveTo(px, 0); g.lineTo(px, canvas.height); g.stroke(); }
  for (let y = Math.ceil(box.y / 1000) * 1000; y < box.y + box.h; y += 1000) { const py = (y - box.y) * scale + offY; g.beginPath(); g.moveTo(0, py); g.lineTo(canvas.width, py); g.stroke(); }
  g.fillText("N", canvas.width - 18, 16); g.fillText("grid: 1,000 units", 8, canvas.height - 8);
  const visible = i => { const side = fight.teams[fight.members[i].t].side; return showOthers || side === "mine" || side === "enemy" || side === "friend"; };
  // Trails of your group: the last eight seconds.
  if ($("trails").checked) {
    fight.members.forEach((m, i) => {
      if (m.t !== 0) return;
      g.strokeStyle = "rgba(255,255,255,.25)"; g.beginPath(); let started = false;
      for (let s = Math.max(0, second - 8); s <= second; s++) { const p = fight.frames[s][i]; if (!p || !(p[5] & 1)) continue; const [x, y] = place(p, canvas); started ? g.lineTo(x, y) : g.moveTo(x, y); started = true; }
      g.stroke();
    });
  }
  // Who your group is on.
  if ($("aims").checked) {
    fight.members.forEach((m, i) => {
      const p = frame[i]; if (m.t !== 0 || !p || !(p[5] & 1) || p[6] < 0) return;
      const q = frame[p[6]]; if (!q || fight.members[p[6]].t === 0) return;
      const [x1, y1] = place(p, canvas), [x2, y2] = place(q, canvas);
      g.strokeStyle = "rgba(255,210,74,.35)"; g.beginPath(); g.moveTo(x1, y1); g.lineTo(x2, y2); g.stroke();
    });
  }
  const squad = fight.squad[second];
  fight.members.forEach((m, i) => {
    const p = frame[i]; if (!p || !visible(i)) return;
    const [x, y] = place(p, canvas), team = fight.teams[m.t], mine = m.t === 0, alive = p[5] & 1, r = mine ? 7 : 5;
    g.globalAlpha = alive ? (team.side === "near" ? .55 : 1) : .45;
    g.fillStyle = realmColor(team.realm); g.strokeStyle = mine ? "#fff" : "rgba(0,0,0,.6)"; g.lineWidth = mine ? 2 : 1;
    if (!alive) { g.strokeStyle = realmColor(team.realm); g.lineWidth = 2; g.beginPath(); g.moveTo(x - 5, y - 5); g.lineTo(x + 5, y + 5); g.moveTo(x + 5, y - 5); g.lineTo(x - 5, y + 5); g.stroke(); g.globalAlpha = 1; return; }
    g.beginPath();
    if (m.r === "F") g.rect(x - r, y - r, 2 * r, 2 * r);
    else if (m.r === "H") { g.moveTo(x, y - r - 1); g.lineTo(x + r + 1, y + r); g.lineTo(x - r - 1, y + r); g.closePath(); }
    else g.arc(x, y, r, 0, 2 * Math.PI);
    g.fill(); g.stroke();
    if (m.y) { g.strokeStyle = "#fff"; g.lineWidth = 1.5; g.beginPath(); g.arc(x, y, r + 5, 0, 2 * Math.PI); g.stroke(); }
    const ring = p[5] & 2 ? "#c06aff" : p[5] & 4 ? "#ffe04a" : p[5] & 8 ? "#b0783a" : null;
    if (ring) { g.strokeStyle = ring; g.lineWidth = 2.5; g.beginPath(); g.arc(x, y, r + 3.5, 0, 2 * Math.PI); g.stroke(); }
    if (squad && squad[1] === i) { g.strokeStyle = "#ff3b30"; g.lineWidth = 2; g.beginPath(); g.arc(x, y, r + 8, 0, 2 * Math.PI); g.moveTo(x - r - 12, y); g.lineTo(x + r + 12, y); g.moveTo(x, y - r - 12); g.lineTo(x, y + r + 12); g.stroke(); }
    // Health under the mark.
    g.fillStyle = "rgba(0,0,0,.6)"; g.fillRect(x - 9, y + r + 3, 18, 3); g.fillStyle = p[2] > 60 ? "#58c05a" : p[2] > 30 ? "#e0a030" : "#e04a3a"; g.fillRect(x - 9, y + r + 3, 18 * Math.max(0, Math.min(100, p[2])) / 100, 3);
    if ($("names").checked && (mine || scale > .2)) { g.fillStyle = mine ? "#fff" : "rgba(255,255,255,.7)"; g.font = (mine ? "bold " : "") + "11px system-ui"; g.fillText(m.y ? "YOU" : m.n.slice(0, 9), x + r + 4, y - r); }
    g.globalAlpha = 1;
  });
  const into = second - fight.before;
  $("clock").textContent = (into < 0 ? "before the fight " : "into the fight ") + fmt(into) + "  (evening " + fmt(fight.first + second) + ")";
  $("order").textContent = squad ? "Your group's order: " + squad[0] + (squad[2] ? " (falling back as one)" : "") + "; alive: yours " + squad[3] + ", theirs " + squad[4] + "; aiming at you: " + squad[5] : "";
  $("at").value = second;
  side(frame); flow();
  document.querySelectorAll("#events div").forEach(el => el.classList.toggle("now", Math.abs(el.dataset.at - second) <= 1));
}

function side(frame) {
  const groups = fight.teams.map(() => []);
  fight.members.forEach((m, i) => groups[m.t].push(i));
  let out = "";
  fight.teams.forEach((team, t) => {
    if (team.side === "near" && !$("others").checked) return;
    const alive = groups[t].filter(i => frame[i] && frame[i][5] & 1).length;
    out += `<div class="team-head ${team.realm}">${esc(t === 0 ? "Your group" : team.name)} <small style="color:var(--dim)">${t === 0 ? "" : esc(team.kind) + ", rank " + team.rank + ", "}${alive} alive</small></div>`;
    for (const i of groups[t]) {
      const m = fight.members[i], p = frame[i];
      if (!p) { out += `<div class="row dead"><span class="who">${esc(m.n)} <small>${esc(m.c)}</small></span><span></span><span></span></div>`; continue; }
      const alive = p[5] & 1, states = [];
      if (!alive) states.push("dead"); if (p[5] & 2) states.push("asleep"); if (p[5] & 4) states.push("stun"); if (p[5] & 8) states.push("root"); if (p[5] & 16) states.push("NS"); if (p[5] & 32) states.push("dis");
      out += `<div class="row${alive ? "" : " dead"}"><span class="who">${m.y ? "<b>You</b>" : esc(m.n)} <small>${esc(m.c)}</small></span><span class="bars"><span class="bar hp${p[2] <= 30 ? " crit" : p[2] <= 60 ? " low" : ""}"><i style="width:${Math.max(0, Math.min(100, p[2]))}%"></i></span>${p[3] >= 0 ? `<span class="bar pow"><i style="width:${Math.max(0, Math.min(100, p[3]))}%"></i></span>` : ""}</span><span class="state">${states.join(" ")}</span></div>`;
    }
  });
  $("side").innerHTML = out;
}

function flow() {
  const canvas = $("flow"), g = canvas.getContext("2d"), n = fight.flow.length, mid = canvas.height / 2;
  g.clearRect(0, 0, canvas.width, canvas.height);
  const top = Math.max(1, ...fight.flow.map(f => Math.max(f[0], f[1]))), w = canvas.width / n;
  fight.flow.forEach((f, s) => {
    g.fillStyle = "#58c05a"; g.fillRect(s * w, mid - f[0] / top * (mid - 4), Math.max(1, w - .5), f[0] / top * (mid - 4));
    g.fillStyle = "#e04a3a"; g.fillRect(s * w, mid, Math.max(1, w - .5), f[1] / top * (mid - 4));
  });
  g.strokeStyle = "rgba(255,255,255,.8)"; g.lineWidth = 1.5; g.beginPath();
  fight.frames.forEach((frame, s) => { const alive = fight.members.filter((m, i) => m.t === 0 && frame[i] && frame[i][5] & 1).length; const y = canvas.height - 6 - alive / 8 * (canvas.height - 12); s ? g.lineTo(s * w, y) : g.moveTo(s * w, y); });
  g.stroke();
  for (const e of fight.events) if (e[1] === "dead-mine" || e[1] === "dead-theirs") { g.fillStyle = e[1] === "dead-mine" ? "#ff3b30" : "#8fe65a"; g.fillRect(e[0] * w, e[1] === "dead-mine" ? canvas.height - 5 : 0, 2, 5); }
  g.fillStyle = css("--accent"); g.fillRect(second * w, 0, 2, canvas.height);
  g.strokeStyle = "rgba(255,255,255,.3)"; g.beginPath(); g.moveTo(fight.before * w, 0); g.lineTo(fight.before * w, canvas.height); g.stroke();
}

function stop() { playing = false; clearInterval(timer); $("play").textContent = "Play"; }
function start() { playing = true; $("play").textContent = "Stop"; clearInterval(timer); timer = setInterval(() => { if (second >= fight.frames.length - 1) { stop(); return; } second++; draw(); }, 1000 / Number($("speed").value)); }
$("play").onclick = () => playing ? stop() : (second >= fight.frames.length - 1 && (second = 0), start());
$("speed").onchange = () => playing && start();
$("at").oninput = () => { second = Number($("at").value); draw(); };
$("flow").onclick = e => { const r = e.target.getBoundingClientRect(); second = Math.max(0, Math.min(fight.frames.length - 1, Math.floor((e.clientX - r.left) / r.width * fight.frames.length))); draw(); };
$("events").onclick = e => { const el = e.target.closest("div[data-at]"); if (el) { second = Math.max(0, Math.min(fight.frames.length - 1, Number(el.dataset.at))); draw(); } };
for (const id of ["names", "aims", "trails", "others", "view"]) $(id).onchange = draw;
document.addEventListener("keydown", e => {
  if (e.target.tagName === "SELECT" || e.target.tagName === "INPUT") return;
  if (e.code === "Space") { e.preventDefault(); $("play").click(); }
  if (e.code === "ArrowRight") { second = Math.min(fight.frames.length - 1, second + 1); draw(); }
  if (e.code === "ArrowLeft") { second = Math.max(0, second - 1); draw(); }
});
$("pick").innerHTML = DATA.fights.map((f, i) => `<option value="${i}">${esc(f.label)}</option>`).join("");
$("pick").onchange = () => choose(Number($("pick").value));
// A link may name a fight and a second of it: page.html#f=2&t=45
const asked = new URLSearchParams(location.hash.slice(1));
const opening = asked.has("f") ? Math.max(0, Math.min(DATA.fights.length - 1, Number(asked.get("f")) - 1)) : DATA.fights.length - 1;
choose(opening); $("pick").value = opening;
if (asked.has("t")) { second = Math.max(0, Math.min(fight.frames.length - 1, Number(asked.get("t")) + fight.before)); draw(); }
</script></body></html>
"""


def when(path):
    name = os.path.basename(path)
    try:
        stamp = datetime.datetime.strptime(name[:15], "%Y%m%d-%H%M%S").replace(tzinfo=datetime.timezone.utc).astimezone()
        return stamp.strftime("%A %d %B %Y, %H:%M")
    except ValueError:
        return name


def write_page(recording, out_folder, me=None, redraw=False):
    """Writes the page of one evening; returns (page file, the evening's data or a short record of it) or None."""
    page = os.path.join(out_folder, os.path.basename(recording)[:-len(".jsonl")] + ".html")
    note = page + ".txt"
    if os.path.exists(page) and os.path.exists(note) and not redraw and os.path.getmtime(page) >= os.path.getmtime(recording):
        return page, json.load(open(note, encoding="utf-8"))
    data = read(recording, me)
    if data is None:
        return None
    title = f"{data['me']}, {when(recording)}"
    text = PAGE.replace("__TITLE__", html.escape(title)).replace("__DATA__", json.dumps(data, separators=(",", ":")).replace("</", "<\\/"))
    os.makedirs(out_folder, exist_ok=True)
    open(page, "w", encoding="utf-8", newline="\n").write(text)
    record = {"title": title, "fights": [fight["label"] for fight in data["fights"]]}
    json.dump(record, open(note, "w", encoding="utf-8"))
    return page, record


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    flag = lambda name: name in sys.argv
    value = lambda name, default=None: sys.argv[sys.argv.index(name) + 1] if name in sys.argv else default
    skip = {value("--out"), value("--last"), value("--team")}
    args = [a for a in args if a not in skip]
    game = "D:\\OfflineDAoC-scrim" if flag("--scrim") else "D:\\OfflineDAoC"
    source = args[0] if args else os.path.join(game, "runtime", "server", "logs", "roams")
    if os.path.isfile(source):
        recordings = [source]
        game = os.path.abspath(os.path.join(os.path.dirname(source), "..", "..", "..", ".."))
    else:
        recordings = sorted(glob.glob(os.path.join(source, "*.jsonl")), reverse=True)
        game = os.path.abspath(os.path.join(source, "..", "..", "..", ".."))
    out = value("--out") or os.path.join(game, "runtime", "logs", "fights")
    if not recordings:
        print(f"No recording in {source}. An evening writes one beside the server, in logs\\roams.")
        return 1
    last = int(value("--last", "8"))
    done = []
    for recording in recordings:
        if len(done) >= last:
            break
        # (An evening of bots alone has no fight of yours: its name has no "play" in it. A team is named for those.)
        if len(recordings) > 1 and not value("--team") and "-play" not in os.path.basename(recording):
            continue
        print(f"reading {os.path.basename(recording)} ...", flush=True)
        made = write_page(recording, out, value("--team"), flag("--redraw"))
        if made:
            done.append((recording, *made))
    if not done:
        print("No fight of yours in these recordings.")
        return 1
    rows = "".join(f'<li><a href="{html.escape(os.path.basename(page))}">{html.escape(record["title"])}</a><ul>' +
                   "".join(f"<li>{html.escape(label)}</li>" for label in record["fights"]) + "</ul></li>" for recording, page, record in done)
    index = os.path.join(out, "index.html")
    open(index, "w", encoding="utf-8", newline="\n").write(
        "<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'><title>Your 8v8 fights</title>"
        "<style>body{font:15px/1.5 system-ui,'Segoe UI',sans-serif;max-width:900px;margin:24px auto;padding:0 16px;background:#14161a;color:#e6e8eb}"
        "a{color:#8fc7ff}li{margin:6px 0}ul ul{color:#98a1ad;font-size:13px}@media (prefers-color-scheme: light){body{background:#f4f5f7;color:#1b1e23}a{color:#1f5fbf}ul ul{color:#5d6672}}</style>"
        f"</head><body><h1>Your 8v8 fights</h1><p>Newest evening first. Pick an evening, then a fight on its page.</p><ul>{rows}</ul></body></html>")
    print(f"{len(done)} evening{'s' if len(done) != 1 else ''}, {sum(len(r['fights']) for _, _, r in done)} fights.")
    print(f"Open: {index}")
    if flag("--open"):
        os.startfile(index)
    return 0


if __name__ == "__main__":
    sys.exit(main())
