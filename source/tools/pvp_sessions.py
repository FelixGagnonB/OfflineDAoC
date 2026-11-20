"""List the owner's 8v8 play sessions on one page, each with its replay (docs/PVP-8V8-PLAYBOOK.md, section 9).

    python pvp_sessions.py [<scrims folder>] [--out <replay folder>] [--redraw]

Reads every recording of a play session (<stamp>-play-<a>-vs-<b>-match-NNN.jsonl), draws the
replay of each one that has none yet (pvp_replay.py) and writes index.html in the replay
folder: when, what the player played, against whom, at what realm rank, how it ended and how
long it took, newest first. A session with no blow struck is listed without a replay.

Defaults: the scrim install's recordings (D:\\OfflineDAoC-scrim\\runtime\\server\\logs\\scrims)
and D:\\OfflineDAoC-scrim\\runtime\\logs\\replay. Nothing is sent anywhere; the page is a file.
"""
import datetime
import glob
import html
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def read(path):
    with open(path, encoding="utf-8") as f:
        return [json.loads(line) for line in f if line.strip()]


def summary(path):
    rows = read(path)
    head = rows[0]
    bots = {b["n"]: b for b in head["bots"]}
    player = next((b for b in head["bots"] if b.get("player")), None)
    hits = [r for r in rows if r.get("k") == "hit"]
    end = next((r for r in rows if r.get("k") == "end"), None)
    ready = next((r for r in rows if r.get("k") == "ready"), None)
    name = os.path.basename(path)[:-6]
    stamp = datetime.datetime.strptime(name[:15], "%Y%m%d-%H%M%S").replace(tzinfo=datetime.timezone.utc).astimezone()
    deaths = {"A": 0, "B": 0}
    for r in rows:
        if r.get("k") == "death" and r.get("n") in bots:
            deaths[bots[r["n"]]["team"]] += 1
    own = head["a"] if not player or player["team"] == "A" else head["b"]
    if end:
        result = ("won" if end["Winner"] == own else "lost" if end["Winner"] != "draw" else "draw") + ", " + end["End"]
        length = end["Seconds"]
    else:
        result = "stopped" if hits else "stopped before a blow"
        last = max(r.get("t", 0) for r in rows)
        length = max(0, (last - (ready["t"] if ready else 0)) // 1000)
    return {
        "name": name, "when": stamp.strftime("%Y-%m-%d %H:%M"), "you": player["cls"] if player else "?",
        "own": head["a"], "enemy": head["b"], "rank": head.get("realmLevel", 0) // 10 + 1,
        "result": result, "seconds": length, "fought": bool(hits),
        "deaths": "%d / %d" % (deaths["A"], deaths["B"]),
    }


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    scrims = args[0] if args else r"D:\OfflineDAoC-scrim\runtime\server\logs\scrims"
    out = r"D:\OfflineDAoC-scrim\runtime\logs\replay"
    if "--out" in sys.argv:
        out = sys.argv[sys.argv.index("--out") + 1]
        args = [a for a in args if a != out]
        scrims = args[0] if args else scrims
    redraw = "--redraw" in sys.argv
    os.makedirs(out, exist_ok=True)
    sessions = []
    for path in sorted(glob.glob(os.path.join(scrims, "*-play-*.jsonl")), reverse=True):
        try:
            s = summary(path)
        except Exception as error:  # a recording cut short by a restart
            print("skipped %s: %s" % (os.path.basename(path), error))
            continue
        folder = os.path.join(out, s["name"])
        page = os.path.join(folder, "replay.html")
        if s["fought"] and (redraw or not os.path.exists(page)):
            done = subprocess.run([sys.executable, os.path.join(HERE, "pvp_replay.py"), path, "--out", folder],
                                  capture_output=True, text=True)
            if done.returncode != 0:
                print("no replay for %s: %s" % (s["name"], (done.stderr or done.stdout).strip()[-200:]))
        s["page"] = s["name"] + "/replay.html" if os.path.exists(page) else None
        sessions.append(s)
    rows = []
    for s in sessions:
        link = '<a href="%s">replay</a>' % html.escape(s["page"]) if s["page"] else ""
        colour = "#2a7" if s["result"].startswith("won") else "#c33" if s["result"].startswith("lost") else "#888"
        rows.append("<tr><td>%s</td><td>%s</td><td>%s against %s</td><td>%d</td><td style='color:%s'>%s</td><td>%d s</td><td>%s</td><td>%s</td></tr>" % (
            s["when"], html.escape(s["you"]), html.escape(s["own"]), html.escape(s["enemy"]), s["rank"], colour,
            html.escape(s["result"]), s["seconds"], s["deaths"], link))
    doc = """<!doctype html><html><head><meta charset="utf-8"><title>8v8 play sessions</title>
<style>body{font:14px Segoe UI,Arial,sans-serif;margin:24px;background:#fafaf7;color:#222}
table{border-collapse:collapse}td,th{padding:5px 12px;border-bottom:1px solid #ddd;text-align:left}
th{font-weight:600}h1{font-size:20px}p{color:#555;max-width:60em}</style></head><body>
<h1>8v8 play sessions</h1>
<p>%d sessions, newest first. Deaths are your side / the enemy. A session you stopped with /8v8 stop has no result.
The time is this computer's.</p>
<table><tr><th>When</th><th>You</th><th>Groups</th><th>Rank</th><th>Result</th><th>Length</th><th>Deaths</th><th></th></tr>
%s</table></body></html>""" % (len(sessions), "\n".join(rows))
    index = os.path.join(out, "index.html")
    with open(index, "w", encoding="utf-8") as f:
        f.write(doc)
    print("%d sessions; %s" % (len(sessions), index))


if __name__ == "__main__":
    main()
