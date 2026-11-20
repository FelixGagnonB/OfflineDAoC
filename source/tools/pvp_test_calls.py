"""One lane match in which a member of side A makes a player's calls: regroup at +20 s, assist at +50 s. Prints what the group did."""
import collections
import glob
import json
import math
import os
import subprocess
import sys
import time

REPO = r"C:\Workspace\OfflineDAoC"
RUNTIME = r"D:\OfflineDAoC-scrim\runtime"
SCRIMS = os.path.join(RUNTIME, "server", "logs", "scrims")
A, B = (sys.argv[1], sys.argv[2]) if len(sys.argv) > 2 else ("alb-main", "mid-main")
WHO = sys.argv[3] if len(sys.argv) > 3 else ""
TARGET = sys.argv[4] if len(sys.argv) > 4 else "warrior"


def request(*args):
    done = subprocess.run([sys.executable, os.path.join(REPO, "source", "tools", "pvp_request.py"), RUNTIME, *args], capture_output=True, text=True)
    try:
        return json.loads(done.stdout)
    except Exception:
        return {"Ok": False, "Error": done.stdout[-300:] + done.stderr[-300:]}


before = set(os.listdir(SCRIMS))
print(request("scrim-start", "A=" + A, "B=" + B, "Matches=1", "RealmRank=5", "BrainA=squad", "BrainB=squad", "Limit=600", "Lanes=1").get("Ok"))
path = None
for _ in range(60):
    new = [f for f in os.listdir(SCRIMS) if f not in before and f.endswith(".jsonl")]
    if new:
        path = os.path.join(SCRIMS, new[0])
        break
    time.sleep(1)
print(path)


def rows():
    out = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            try:
                out.append(json.loads(line))
            except Exception:
                pass
    return out


first = None
for _ in range(240):
    hits = [r for r in rows() if r.get("k") == "hit"]
    if hits:
        first = hits[0]["t"]
        break
    time.sleep(1)
print("first blow at", first)


def now_t():
    return max(r.get("t", 0) for r in rows())


calls = []
while now_t() < first + 20_000:
    time.sleep(1)
calls.append(("regroup", now_t(), request("scrim-call", "Lane=0", "Team=A", "Call=regroup", "Who=" + WHO)))
while now_t() < first + 50_000:
    time.sleep(1)
calls.append(("assist", now_t(), request("scrim-call", "Lane=0", "Team=A", "Call=assist", "Who=" + WHO, "Target=" + TARGET)))
for name, t, answer in calls:
    print(name, "at +%d s:" % ((t - first) // 1000), answer.get("Ok"), answer.get("Data") or answer.get("Error"))
while now_t() < first + 85_000 and not any(r.get("k") == "end" for r in rows()):
    time.sleep(1)
request("scrim-stop")

data = rows()
bots = {b["n"]: b for b in data[0]["bots"]}
by = collections.defaultdict(dict)
for r in data:
    if r.get("k") == "snap":
        by[r["t"]][r["n"]] = r
ts = sorted(by)
regroup = calls[0]
caller = (regroup[2].get("Data") or {}).get("Caller")
if caller:
    print("\nregroup on %s (%s): members of side A farther than 1,700 from it, and the farthest, second by second" % (caller, bots[caller]["build"]))
    for t in ts:
        if regroup[1] - 4_000 <= t <= regroup[1] + 16_000 and caller in by[t]:
            c = by[t][caller]
            d = [math.hypot(s["x"] - c["x"], s["y"] - c["y"]) for n, s in by[t].items() if bots[n]["team"] == "A" and s["alive"] and n != caller]
            order = next((r["order"] for r in data if r.get("k") == "squad" and r.get("team") == "A" and r["t"] == t), "?")
            print("  %+3d s  order %-8s far %d  farthest %5d" % ((t - regroup[1]) // 1000, order, sum(1 for x in d if x > 1700), max(d) if d else 0))
assist = calls[1]
called = (assist[2].get("Data") or {}).get("Assist")
if called:
    print("\nassist on %s (%s): side A's kill target, members targeting it, damage on it, second by second" % (called, bots[called]["build"]))
    for t in ts:
        if assist[1] - 4_000 <= t <= assist[1] + 28_000:
            kill = next((r.get("kill") for r in data if r.get("k") == "squad" and r.get("team") == "A" and r["t"] == t), None)
            on = sum(1 for n, s in by[t].items() if bots[n]["team"] == "A" and s["alive"] and s.get("target") == called)
            dmg = sum((r.get("dmg") or 0) + (r.get("crit") or 0) for r in data if r.get("k") == "hit" and r.get("d") == called and r.get("at") == "A" and t <= r["t"] < t + 1000)
            hp = by[t].get(called, {}).get("hp")
            if (t - assist[1]) // 1000 % 2 == 0:
                print("  %+3d s  kill %-12s targeting it %d  damage %4d  its health %s" % ((t - assist[1]) // 1000, bots[kill]["build"] if kill in bots else kill, on, dmg, hp))
print("\ndecisions:", collections.Counter(r["pick"]["kind"] for r in data if r.get("k") == "dec" and r.get("pick") and r["pick"].get("kind") in ("PlayerCall", "PlayerAssist", "FallBack")))
