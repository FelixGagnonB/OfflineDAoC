#!/usr/bin/env python3
"""Counts a recorded roam (docs/PVP-8V8-ROADMAP.md, M9.7) and cuts its fights out as matches.

A roam is one recording for a whole session with any number of teams (logs/roams, format 4:
see bots/pvp/PvpRecorder.cs). This prints what happened in it: the events in order, every fight
(who, where, how long, who died, how it ended) and a line for each team. With --cut each fight of
exactly two teams is also written as a match recording (format 3), which pvp_replay.py,
pvp_referee.py and pvp_analyze.py read like any scrim.

    python pvp_roam.py <recording.jsonl>                 the newest in a folder when given a folder
    python pvp_roam.py <recording.jsonl> --cut [folder]  default: "cuts" beside the recording
    python pvp_roam.py <recording.jsonl> --html [file]   default: <recording>.html beside it

--html writes the roam as a page for the browser: the zone from above (north is up), every member
a dot in its realm's colour, to play and scrub, with the events and the fights to jump to and a
view that follows one team. It needs nothing but the page. A team with no enemy near is recorded
every fifth second, so its dots move in steps while it walks.
"""
import collections
import json
import os
import sys

GAP = 20_000        # a fight is over when its teams have not hit each other for this long (ms)
LEAD_IN = 15_000    # a cut starts this long before the first blow
MIN_HITS = 6        # fewer blows than this between two teams is not a fight


def load(path):
    rows = []
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError:
                break  # the last line of a roam still being written
    if not rows or rows[0].get("k") != "roam":
        sys.exit("%s is not the recording of a roam." % path)
    return rows


def clock(ms):
    seconds = int(ms // 1000)
    return "%d:%02d" % (seconds // 60, seconds % 60)


def find_fights(rows, realm):
    """Teams hitting each other, joined into fights: a blow belongs to a fight one of its teams is in."""
    fights, active = [], []
    for row in rows:
        if row["k"] != "hit":
            continue
        a, d = row.get("at"), row.get("dt")
        if not a or not d or a == d or realm.get(a) == realm.get(d):
            continue
        t = row["t"]
        active = [fight for fight in active if fight["last"] >= t - GAP]
        touching = [fight for fight in active if a in fight["teams"] or d in fight["teams"]]
        if not touching:
            fight = {"start": t, "last": t, "teams": {}, "hits": 0, "pairs": collections.Counter()}
            active.append(fight)
            fights.append(fight)
        else:
            fight = touching[0]
            for other in touching[1:]:
                fight["start"] = min(fight["start"], other["start"])
                fight["hits"] += other["hits"]
                fight["pairs"] += other["pairs"]
                for team, since in other["teams"].items():
                    fight["teams"][team] = min(since, fight["teams"].get(team, since))
                active.remove(other)
                fights.remove(other)
        for team in (a, d):
            fight["teams"].setdefault(team, t)
        fight["last"] = t
        fight["hits"] += 1
        fight["pairs"][tuple(sorted((a, d)))] += 1
    return [fight for fight in fights if fight["hits"] >= MIN_HITS]


def alive_at(snaps, team, t):
    """How many of a team were alive in its last rows at or before a moment."""
    latest = {}
    for row in snaps.get(team, ()):
        if row["t"] > t:
            break
        latest[row["n"]] = row
    return sum(1 for row in latest.values() if row["alive"]), latest


def describe(fights, rows, snaps):
    deaths = [row for row in rows if row["k"] == "death"]
    for fight in fights:
        start, end = fight["start"], fight["last"]
        fight["deaths"] = collections.Counter(row["team"] for row in deaths
                                              if start <= row["t"] <= end + 3_000 and row["team"] in fight["teams"])
        fight["alive"], xs, ys = {}, [], []
        for team, since in fight["teams"].items():
            fight["alive"][team], _ = alive_at(snaps, team, end + 3_000)
            # Where it began: the teams of the first blows, not one that came a minute later from elsewhere.
            if since > start + 5_000:
                continue
            _, at_start = alive_at(snaps, team, start)
            xs += [row["x"] for row in at_start.values()]
            ys += [row["y"] for row in at_start.values()]
        fight["at"] = (int(sum(xs) / len(xs)), int(sum(ys) / len(ys))) if xs else None
        standing = [team for team in fight["teams"] if fight["alive"][team] > 0]
        fight["wiped"] = [team for team in fight["teams"] if fight["alive"][team] == 0]
        fight["end"] = "wipe" if fight["wiped"] else "broke off"
        fight["standing"] = standing


def report(path, rows, fights):
    head = rows[0]
    last = max((row["t"] for row in rows if "t" in row), default=0)
    end = rows[-1] if rows[-1].get("k") == "end" else None
    print("%s: %s, %s%s" % (os.path.basename(path), head["label"], clock(last), "" if end else " (still running, or stopped without an end)"))
    print("Teams: " + ", ".join("%s%s" % (team["name"], " (the player: %s)" % team["player"] if team.get("player") else "") for team in head["teams"]))

    print("\nWhat happened")
    for row in rows:
        if row["k"] == "ev" and row.get("text"):
            print("  %6s  %s" % (clock(row["t"]), row["text"]))

    print("\nFights (%d)" % len(fights))
    for number, fight in enumerate(fights, 1):
        first = sorted(fight["teams"], key=fight["teams"].get)
        opening = [team for team in first if fight["teams"][team] <= fight["start"] + 5_000]
        later = [team for team in first if team not in opening]
        line = "  %2d. %6s  %-5s  %s" % (number, clock(fight["start"]), clock(fight["last"] - fight["start"]), " against ".join(opening))
        if later:
            line += "; then " + ", ".join("%s (+%ds)" % (team, (fight["teams"][team] - fight["start"]) // 1000) for team in later)
        print(line)
        print("        deaths: " + ", ".join("%s %d" % (team, fight["deaths"][team]) for team in first) +
              "; left standing: " + (", ".join("%s %d" % (team, fight["alive"][team]) for team in fight["standing"]) or "nobody") +
              ("; at %d,%d" % fight["at"] if fight["at"] else ""))

    print("\nTeams")
    paid = collections.Counter()
    for row in rows:
        if row["k"] == "ev" and (row.get("ev") or {}).get("what") == "pay":
            paid[row["ev"]["to"]] += row["ev"]["share"] * row["ev"]["members"]
    kills = collections.Counter(row.get("byTeam") for row in rows if row["k"] == "death" and row.get("byTeam") and row.get("byTeam") != row["team"])
    died = collections.Counter(row["team"] for row in rows if row["k"] == "death")
    wipes = collections.Counter((row.get("ev") or {}).get("team") for row in rows if row["k"] == "ev" and (row.get("ev") or {}).get("what") == "wipe")
    fighting = collections.Counter()
    for fight in fights:
        for team, since in fight["teams"].items():
            fighting[team] += fight["last"] - since
    print("  %-14s %6s %10s %6s %6s %6s %9s %8s" % ("team", "fights", "in a fight", "kills", "deaths", "wipes", "points", "a member"))
    for team in head["teams"]:
        name = team["name"]
        members = sum(1 for bot in head["bots"] if bot["team"] == name) or 1
        print("  %-14s %6d %10s %6d %6d %6d %9d %8d" % (
            name, sum(1 for fight in fights if name in fight["teams"]), clock(fighting[name]),
            kills[name], died[name], wipes[name], paid[name], paid[name] // members))
    two = sum(1 for fight in fights if len(fight["teams"]) == 2)
    print("\n%d of %d fights were between two teams only%s." % (two, len(fights), "" if not fights else "; the largest had %d" % max(len(fight["teams"]) for fight in fights)))


# The keys of a line that hold a team's name.
TEAM_KEYS = ("team", "at", "dt", "byTeam")


def cut(path, rows, fights, snaps, folder):
    """Each fight of two teams as a match recording: side A is the team that was there first."""
    head = rows[0]
    os.makedirs(folder, exist_ok=True)
    stem = os.path.splitext(os.path.basename(path))[0]
    group = {team["name"]: team["name"] for team in head["teams"]}
    written = []
    for number, fight in enumerate(fights, 1):
        if len(fight["teams"]) != 2:
            continue
        a, b = sorted(fight["teams"], key=fight["teams"].get)
        side = {a: "A", b: "B"}
        begin, end = max(0, fight["start"] - LEAD_IN), fight["last"] + 3_000
        # Who is in each team when the fight starts: the header's members, or those of its last standing up before it.
        members = {}
        for bot in head["bots"]:
            if bot["team"] in side:
                members.setdefault(bot["team"], {})[bot["n"]] = bot
        for row in rows:
            if row["k"] == "spawn" and row["team"] in side and row["t"] <= fight["start"]:
                members[row["team"]] = {bot["n"]: bot for bot in row["bots"]}
        names = {name for team in members.values() for name in team}
        out = [{
            "k": "match", "v": 3, "batch": stem + "-fight", "number": number, "utc": head["utc"],
            "realmLevel": max((bot.get("rl") or 1) for team in members.values() for bot in team.values()),
            "a": group[a], "b": group[b], "weights": head.get("weights"), "weightsB": None, "setA": None, "setB": None,
            "roam": os.path.basename(path), "roamStart": fight["start"],
            "bots": [dict(bot, team=side[team]) for team in (a, b) for bot in members[team].values()],
        }]
        for row in rows[1:]:
            t = row.get("t")
            if t is None or t < begin or t > end or row["k"] in ("team", "ev", "spawn", "end"):
                continue
            # A line of these two teams only; somebody of a third team in it keeps the name and loses the team, as a guard does.
            own = [row.get(key) for key in TEAM_KEYS if row.get(key)]
            if row["k"] == "snap":
                if row["n"] not in names:
                    continue
            elif not any(team in side for team in own):
                continue
            line = dict(row, t=t - begin)
            for key in TEAM_KEYS:
                if key in line and line[key] is not None:
                    line[key] = side.get(line[key])
            if row["k"] == "snap":
                line.pop("team", None)
            elif line.get("team") is None and "team" in row and row["k"] in ("squad", "dec", "call", "cast", "begin", "stop"):
                continue
            out.append(line)
        alive_a, alive_b = fight["alive"][a], fight["alive"][b]
        out.append({
            "k": "end", "t": end - begin,
            "Winner": "draw" if alive_a == alive_b else group[a] if alive_a > alive_b else group[b],
            "End": fight["end"], "Seconds": (end - begin) // 1000, "SecondsToContact": (fight["start"] - begin) // 1000,
            "AliveA": alive_a, "AliveB": alive_b,
        })
        name = os.path.join(folder, "%s-fight-%02d-%s-vs-%s.jsonl" % (stem, number, a.replace("#", "-"), b.replace("#", "-")))
        with open(name, "w", encoding="utf-8") as handle:
            for line in out:
                handle.write(json.dumps(line, separators=(",", ":")) + "\n")
        written.append(name)
    return written


PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>__TITLE__</title>
<style>
:root { --bg:#f7f6f2; --panel:#ffffff; --ink:#1d1d1b; --soft:#6b6a65; --line:#d9d7cf; --map:#eeece4;
        --alb:#c62d2d; --mid:#2d5fd8; --hib:#1f8f3c; --dead:#9a9891; --mark:#b8860b; }
@media (prefers-color-scheme: dark) {
  :root { --bg:#161614; --panel:#1f1f1c; --ink:#ecebe6; --soft:#a3a29b; --line:#3a3934; --map:#24241f;
          --alb:#ef6a6a; --mid:#7aa2ff; --hib:#5fcf7d; --dead:#6f6e68; --mark:#e0b84a; } }
* { box-sizing: border-box; }
body { margin:0; background:var(--bg); color:var(--ink); font:14px/1.4 system-ui, "Segoe UI", sans-serif; }
header { padding:12px 16px 4px; }
h1 { font-size:18px; margin:0 0 2px; }
header p { margin:0; color:var(--soft); }
#bar { display:flex; flex-wrap:wrap; gap:8px; align-items:center; padding:8px 16px; }
#bar input[type=range] { flex:1 1 240px; min-width:160px; }
button, select { font:inherit; color:var(--ink); background:var(--panel); border:1px solid var(--line); border-radius:6px; padding:4px 10px; }
#clock { font-variant-numeric:tabular-nums; min-width:64px; }
#wrap { display:flex; flex-wrap:wrap; gap:12px; padding:0 16px 16px; }
#mapbox { flex:3 1 520px; min-width:0; }
canvas { width:100%; height:auto; display:block; background:var(--map); border:1px solid var(--line); border-radius:8px; }
#side { flex:1 1 300px; min-width:0; display:flex; flex-direction:column; gap:12px; }
.panel { background:var(--panel); border:1px solid var(--line); border-radius:8px; padding:8px 10px; }
.panel h2 { font-size:13px; margin:0 0 6px; color:var(--soft); font-weight:600; text-transform:uppercase; letter-spacing:.04em; }
table { border-collapse:collapse; width:100%; font-variant-numeric:tabular-nums; }
td, th { padding:2px 6px 2px 0; text-align:left; white-space:nowrap; }
th { color:var(--soft); font-weight:500; }
.dot { display:inline-block; width:9px; height:9px; border-radius:50%; margin-right:6px; }
#events, #fights { max-height:260px; overflow:auto; }
.row { padding:2px 4px; border-radius:4px; cursor:pointer; white-space:normal; }
.row:hover { background:var(--map); }
.row.now { background:var(--map); font-weight:600; }
.row span { color:var(--soft); font-variant-numeric:tabular-nums; margin-right:6px; }
</style></head><body>
<header><h1>__TITLE__</h1><p>North is up. A dot is a member, in its realm's colour; a ring marks the player's group; grey is dead.</p></header>
<div id="bar">
  <button id="play">Play</button>
  <select id="speed"><option value="1">1x</option><option value="4" selected>4x</option><option value="16">16x</option><option value="60">60x</option></select>
  <span id="clock">0:00</span>
  <input id="time" type="range" min="0" max="0" value="0">
  <label>View <select id="follow"><option value="">the whole zone</option></select></label>
</div>
<div id="wrap">
  <div id="mapbox"><canvas id="map" width="1000" height="760"></canvas></div>
  <div id="side">
    <div class="panel"><h2>Teams</h2><table id="teams"></table></div>
    <div class="panel"><h2>Fights</h2><div id="fights"></div></div>
    <div class="panel"><h2>What happened</h2><div id="events"></div></div>
  </div>
</div>
<script>
const D = __DATA__;
const css = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
const colour = realm => css(realm === "Midgard" ? "--mid" : realm === "Hibernia" ? "--hib" : "--alb");
const canvas = document.getElementById("map"), g = canvas.getContext("2d");
const time = document.getElementById("time"), clock = document.getElementById("clock"), follow = document.getElementById("follow");
time.max = D.frames.length - 1;
D.teams.forEach((team, i) => follow.add(new Option(team.name + (team.player ? " (" + team.player + ")" : ""), i)));
const fmt = s => Math.floor(s / 60) + ":" + String(s % 60).padStart(2, "0");
let s = 0, timer = null;

function bounds() {
  let x0 = Infinity, x1 = -Infinity, y0 = Infinity, y1 = -Infinity;
  const take = (x, y) => { if (x < 0) return; x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); };
  if (follow.value !== "") {
    const frame = D.frames[s];
    D.members.forEach((m, i) => { if (m[0] == follow.value) take(frame[i * 3], frame[i * 3 + 1]); });
    const cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, half = Math.max(3500, (x1 - x0) / 2 + 2500, (y1 - y0) / 2 + 2500);
    return [cx - half * 1.3, cx + half * 1.3, cy - half, cy + half];
  }
  return D.box;
}

function draw() {
  const frame = D.frames[s], [x0, x1, y0, y1] = bounds();
  const w = canvas.width, h = canvas.height, pad = 24;
  const scale = Math.min((w - 2 * pad) / (x1 - x0), (h - 2 * pad) / (y1 - y0));
  const ox = (w - (x1 - x0) * scale) / 2, oy = (h - (y1 - y0) * scale) / 2;
  const px = x => ox + (x - x0) * scale, py = y => oy + (y - y0) * scale;
  g.clearRect(0, 0, w, h);
  g.font = "13px system-ui, sans-serif";
  // The places teams start from and walk between.
  g.fillStyle = css("--soft"); g.strokeStyle = css("--line");
  D.places.forEach(place => { const x = px(place[1]), y = py(place[2]); g.beginPath(); g.rect(x - 4, y - 4, 8, 8); g.stroke(); g.fillText(place[0], x + 8, y + 4); });
  // A fight going on now: a ring where it began.
  D.fights.forEach(f => { if (s >= f.start && s <= f.end && f.at) { g.strokeStyle = css("--mark"); g.lineWidth = 2; g.beginPath(); g.arc(px(f.at[0]), py(f.at[1]), Math.max(18, 1800 * scale), 0, 7); g.stroke(); g.lineWidth = 1; } });
  const sums = D.teams.map(() => [0, 0, 0]);
  D.members.forEach((m, i) => {
    const x = frame[i * 3], y = frame[i * 3 + 1], alive = frame[i * 3 + 2];
    if (x < 0) return;
    const team = D.teams[m[0]];
    g.fillStyle = alive ? colour(team.realm) : css("--dead");
    g.beginPath(); g.arc(px(x), py(y), alive ? 4 : 3, 0, 7); g.fill();
    if (m[2]) { g.strokeStyle = css("--ink"); g.lineWidth = 2; g.beginPath(); g.arc(px(x), py(y), 7, 0, 7); g.stroke(); g.lineWidth = 1; }
    if (alive) { sums[m[0]][0] += x; sums[m[0]][1] += y; sums[m[0]][2]++; }
  });
  D.teams.forEach((team, i) => {
    if (!sums[i][2]) return;
    const x = px(sums[i][0] / sums[i][2]), y = py(sums[i][1] / sums[i][2]);
    if (team.player) { g.strokeStyle = css("--ink"); g.beginPath(); g.arc(x, y, 16, 0, 7); g.stroke(); }
    g.fillStyle = colour(team.realm);
    // Names of teams that stand together are set one under the other.
    g.fillText(team.name + " (" + sums[i][2] + ")", x + 14, y - 26 + i * 13);
  });
  clock.textContent = fmt(s); time.value = s;
  const rows = D.teams.map((team, i) => {
    const state = D.states[s] ? D.states[s][i] : "";
    return "<tr><td><span class=dot style='background:" + colour(team.realm) + "'></span>" + team.name + "</td><td>" + sums[i][2] + " alive</td><td>" + (state || "") + "</td></tr>";
  });
  document.getElementById("teams").innerHTML = "<tr><th>team</th><th></th><th>doing</th></tr>" + rows.join("");
  mark("events", D.events.map(e => e[0])); mark("fights", D.fights.map(f => f.start), D.fights.map(f => f.end));
}

function mark(id, starts, ends) {
  const box = document.getElementById(id); let last = -1;
  starts.forEach((start, i) => { if (ends ? s >= start && s <= ends[i] : start <= s) last = ends ? i : i; });
  [...box.children].forEach((row, i) => row.classList.toggle("now", ends ? s >= starts[i] && s <= ends[i] : i === last));
  // The list follows the clock (inside its own box only).
  const now = box.querySelector(".now");
  if (now && box.dataset.at !== String(last)) { box.dataset.at = String(last); box.scrollTop = Math.max(0, now.offsetTop - box.offsetTop - 60); }
}

function list(id, items, text) {
  const box = document.getElementById(id);
  items.forEach(item => { const row = document.createElement("div"); row.className = "row"; row.innerHTML = text(item);
    row.onclick = () => { s = Math.max(0, Math.min(D.frames.length - 1, item.start !== undefined ? item.start - 5 : item[0])); draw(); }; box.appendChild(row); });
  if (!items.length) box.textContent = "None.";
}
list("events", D.events, e => "<span>" + fmt(e[0]) + "</span>" + e[1]);
list("fights", D.fights, f => "<span>" + fmt(f.start) + "</span>" + f.label);

document.getElementById("play").onclick = function () {
  if (timer) { clearInterval(timer); timer = null; this.textContent = "Play"; return; }
  this.textContent = "Pause";
  timer = setInterval(() => { s += Math.max(1, Math.round(document.getElementById("speed").value / 10));
    if (s >= D.frames.length - 1) { s = D.frames.length - 1; clearInterval(timer); timer = null; document.getElementById("play").textContent = "Play"; } draw(); }, 100);
};
document.getElementById("speed").onchange = () => {};
time.oninput = () => { s = +time.value; draw(); };
follow.onchange = draw;
matchMedia("(prefers-color-scheme: dark)").addEventListener("change", draw);
// page.html#t=140 opens at that second; #t=140&team=2 follows the third team.
const asked = new URLSearchParams(location.hash.slice(1));
if (asked.get("t")) s = Math.max(0, Math.min(D.frames.length - 1, +asked.get("t") || 0));
if (asked.get("team") && D.teams[+asked.get("team")]) follow.value = asked.get("team");
draw();
</script></body></html>
"""


def page(path, rows, fights, out):
    """The roam as one page: where every member was each second, the events and the fights."""
    head = rows[0]
    teams = [team["name"] for team in head["teams"]]
    members, index = [], {}

    def member(team, name, player=False):
        if (team, name) not in index and team in teams:
            index[(team, name)] = len(members)
            members.append([teams.index(team), name, 1 if player else 0])

    for bot in head["bots"]:
        member(bot["team"], bot["n"], bot.get("player", False))
    for row in rows:
        if row["k"] == "spawn":
            for bot in row["bots"]:
                member(row["team"], bot["n"], bot.get("player", False))
    last = max((row["t"] for row in rows if "t" in row), default=0) // 1000
    by_second = collections.defaultdict(list)
    states = collections.defaultdict(dict)
    for row in rows:
        if row["k"] == "snap" and (row.get("team"), row["n"]) in index:
            by_second[row["t"] // 1000].append(row)
        elif row["k"] == "team" and row["name"] in teams:
            fighting = row.get("fighting") or []
            states[row["t"] // 1000][row["name"]] = "fights " + ", ".join(fighting) if fighting else row["state"].lower()
    # A team with no enemy near is written every fifth second: a member stays where it was last seen.
    current = [-1, -1, 0] * len(members)
    state = [""] * len(teams)
    frames, state_rows = [], []
    xs, ys = [], []
    for second in range(last + 1):
        for row in by_second.get(second, ()):
            at = index[(row["team"], row["n"])] * 3
            current[at:at + 3] = [row["x"], row["y"], 1 if row["alive"] else 0]
            xs.append(row["x"])
            ys.append(row["y"])
        for name, what in states.get(second, {}).items():
            state[teams.index(name)] = what
        frames.append(list(current))
        state_rows.append(list(state))
    if not xs:
        sys.exit("Nothing to draw in this recording yet.")
    margin = 2_000
    box = [min(xs) - margin, max(xs) + margin, min(ys) - margin, max(ys) + margin]
    # The places of the map: where each team first stood, by its realm.
    places = []
    for team in head["teams"]:
        first = [frames[min(2, last)][index[key] * 3:index[key] * 3 + 2] for key in index if key[0] == team["name"]]
        first = [spot for spot in first if spot[0] >= 0]
        label = team["realm"].replace("_FirstPlayerRealm", "Albion") + " start"
        if first and not any(place[0] == label for place in places):
            places.append([label, int(sum(spot[0] for spot in first) / len(first)), int(sum(spot[1] for spot in first) / len(first))])
    listed = []
    for number, fight in enumerate(fights, 1):
        order = sorted(fight["teams"], key=fight["teams"].get)
        listed.append({
            "start": fight["start"] // 1000, "end": fight["last"] // 1000, "at": fight["at"],
            "label": "%d. %s, %s: deaths %s" % (number, " / ".join(order), clock(fight["last"] - fight["start"]),
                                                 ", ".join("%s %d" % (team, fight["deaths"][team]) for team in order)),
        })
    data = {
        "teams": [{"name": team["name"], "realm": team["realm"].replace("_FirstPlayerRealm", "Albion"), "player": team.get("player")} for team in head["teams"]],
        "members": members, "frames": frames, "states": state_rows, "box": box, "places": places, "fights": listed,
        "events": [[row["t"] // 1000, row["text"]] for row in rows if row["k"] == "ev" and row.get("text")],
    }
    title = "Roam of %s, %s" % (head["utc"][:16].replace("T", " "), clock(last * 1000))
    with open(out, "w", encoding="utf-8") as handle:
        handle.write(PAGE.replace("__TITLE__", title).replace("__DATA__", json.dumps(data, separators=(",", ":"))))
    return out


def option(args, name):
    """The value after an option, or None when the next word is another option or there is none."""
    at = args.index(name)
    return args[at + 1] if at + 1 < len(args) and not args[at + 1].startswith("--") else None


def main():
    args = [arg for arg in sys.argv[1:]]
    if not args:
        sys.exit(__doc__)
    path = args[0]
    if os.path.isdir(path):
        found = sorted(name for name in os.listdir(path) if name.endswith(".jsonl"))
        if not found:
            sys.exit("No recording in %s." % path)
        path = os.path.join(path, found[-1])
    rows = load(path)
    realm = {team["name"]: team["realm"] for team in rows[0]["teams"]}
    snaps = collections.defaultdict(list)
    for row in rows:
        if row["k"] == "snap":
            snaps[row["team"]].append(row)
    fights = find_fights(rows, realm)
    describe(fights, rows, snaps)
    report(path, rows, fights)
    if "--html" in args:
        out = page(path, rows, fights, option(args, "--html") or os.path.splitext(os.path.abspath(path))[0] + ".html")
        print("\nThe roam as a page: %s" % out)
    if "--cut" in args:
        folder = option(args, "--cut") or os.path.join(os.path.dirname(os.path.abspath(path)), "cuts")
        written = cut(path, rows, fights, snaps, folder)
        print("\n%d fights of two teams written to %s" % (len(written), folder))
        for name in written:
            print("  " + os.path.basename(name))


if __name__ == "__main__":
    main()
