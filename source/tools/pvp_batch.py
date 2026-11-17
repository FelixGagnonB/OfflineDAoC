"""Run 8v8 scrim series side by side on the scrim server, unattended, and report.

    python pvp_batch.py [Pairs=alb-main:mid-main,mid-main:hib-main,alb-main:hib-main]
                        [Matches=4] [Copies=1] [RealmRank=1] [Limit=300]
                        [BrainA=squad] [BrainB=squad]
                        [--game D:\\OfflineDAoC-scrim] [--weights file.json] [--versus old.json|builtin]
                        [--out report.txt]

Each pairing gets its own lane of the arena and all lanes fight at once (docs/PVP-8V8-ROADMAP.md,
M2.5); Copies=2 gives each pairing two lanes. There are six lanes, the first four on level
ground and the last two on hillier ground. The tool starts the scrim
server, checks the lanes, runs the matches, stops the server and runs pvp_analyze.py on every
batch. It prints one line per finished match and a summary; the full reports go to --out
(default: <game folder>\\runtime\\logs\\pvp-batch-<time>.txt).

--weights copies that file beside the server as pvp-weights.json for this run. Without it the
run uses the built-in weights: a pvp-weights.json left beside the server is set aside
(renamed to pvp-weights.unused.json) so that it cannot change the result unnoticed.

--versus compares two sets of weights inside the same matches: one team fights with the new
set (--weights, or the built-in ones) and the other with the old set (a file, or "builtin").
Each pairing then gets two lanes (Copies=2 unless given), with the sets the other way round
in the second, so every realm fights with both sets against each opponent; the summary ends
with how many matches the new set won. A change that is switched by a weight needs no
separate base run this way.

The game folder must be a scrim install (scrim_server.py refuses any other), and only one
server can use port 10300, so nothing else may be running there.
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULTS = {
    "Pairs": "alb-main:mid-main,mid-main:hib-main,alb-main:hib-main",
    "Matches": "4", "Copies": "1", "RealmRank": "1", "Limit": "300",
    "BrainA": "squad", "BrainB": "squad",
}


def tool(name, *args):
    return subprocess.run([sys.executable, os.path.join(HERE, name), *args], capture_output=True, text=True)


def request(runtime, command, **args):
    """One control-channel request; returns the server's whole answer."""
    with tempfile.TemporaryDirectory() as folder:
        out = os.path.join(folder, "answer.json")
        done = tool("pvp_request.py", runtime, command, *[f"{k}={v}" for k, v in args.items()], "--out", out)
        if not os.path.isfile(out):
            return {"Ok": False, "Error": (done.stderr or done.stdout).strip(), "Data": None}
        with open(out, encoding="utf-8") as handle:
            return json.load(handle)


def server_memory():
    out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq CoreServer.exe", "/FO", "CSV", "/NH"], capture_output=True, text=True).stdout
    return "; ".join(line.split('","')[-1].strip('" ') for line in out.splitlines() if "CoreServer" in line) or "?"


def main(argv):
    options = dict(DEFAULTS)
    game, weights, out, versus = r"D:\OfflineDAoC-scrim", None, None, None
    given = set()
    rest = argv[1:]
    while rest:
        item = rest.pop(0)
        if item in ("-h", "--help"):
            print(__doc__)
            return 0
        if item == "--game":
            game = rest.pop(0)
        elif item == "--weights":
            weights = rest.pop(0)
        elif item == "--out":
            out = rest.pop(0)
        elif item == "--versus":
            versus = rest.pop(0)
        elif "=" in item and item.split("=", 1)[0] in DEFAULTS:
            key, value = item.split("=", 1)
            options[key] = value
            given.add(key)
        else:
            print(f"Unexpected argument: {item}\n\n{__doc__}")
            return 2
    runtime = os.path.join(game, "runtime")
    stamp = time.strftime("%Y%m%d-%H%M%S")
    out = out or os.path.join(runtime, "logs", f"pvp-batch-{stamp}.txt")
    if versus and "Copies" not in given:
        options["Copies"] = "2"
    series = len(options["Pairs"].split(",")) * int(options["Copies"])
    started = time.time()

    # The weights this run uses are made explicit before the server reads them.
    beside = os.path.join(runtime, "server", "pvp-weights.json")
    if weights:
        shutil.copyfile(weights, beside)
        print(f"weights: {weights}")
    elif os.path.isfile(beside):
        os.replace(beside, os.path.join(runtime, "server", "pvp-weights.unused.json"))
        print("weights: built-in (a pvp-weights.json beside the server was set aside as pvp-weights.unused.json)")
    else:
        print("weights: built-in")
    old = os.path.join(runtime, "server", "pvp-weights-old.json")
    if versus:
        if versus == "builtin":
            with open(old, "w", encoding="utf-8") as handle:
                handle.write("{}\n")
        else:
            shutil.copyfile(versus, old)
        print(f"compared against: {versus} (each pairing fights both ways round)")
        if int(options["Copies"]) % 2:
            print("WARNING: with an odd number of copies the realms do not get both sets equally often.")
    elif os.path.isfile(old):
        os.replace(old, os.path.join(runtime, "server", "pvp-weights-old.unused.json"))

    up = tool("scrim_server.py", "start", game)
    print(up.stdout.strip() or up.stderr.strip(), flush=True)
    if up.returncode != 0:
        return 1
    failed = False
    batches = []
    try:
        lanes = request(runtime, "scrim-lanes")
        if not lanes["Ok"]:
            print("lane check failed:", lanes["Error"])
            return 1
        bad = [lane for lane in lanes["Data"][:series] if not (lane["GroundA"] is not None and lane["GroundB"] is not None and lane["OpenLine"])]
        for lane in bad:
            print(f"lane {lane['Lane']} is not usable: {lane}")
        if bad or series > len(lanes["Data"]):
            print(f"{series} series asked for; {len(lanes['Data'])} lanes, {len(bad)} of the needed ones unusable.")
            return 1

        begun = request(runtime, "scrim-start", Lanes="1", Versus="1" if versus else "0", **options)
        if not begun["Ok"]:
            print("scrim-start failed:", (begun["Error"] or "")[:2000])
            return 1
        print(f"{series} series started, {options['Matches']} matches each, realm rank {options['RealmRank']}", flush=True)
        seen = {}
        runs = []
        while True:
            time.sleep(10)
            status = request(runtime, "scrim-status")
            if not status["Ok"] or not status["Data"]:
                print("status failed:", status["Error"], flush=True)
                continue
            runs = status["Data"]["Runs"]
            for run in runs:
                for result in run["Results"][seen.get(run["Lane"], 0):]:
                    print(f"{time.time() - started:5.0f}s lane {run['Lane']} {run['A']} vs {run['B']} match {result['Number']}: "
                          f"{result['Winner']} by {result['End']} in {result['Seconds']}s, contact {result['SecondsToContact']}s, "
                          f"alive {result['AliveA']}/{result['AliveB']}", flush=True)
                seen[run["Lane"]] = len(run["Results"])
            if not status["Data"]["Running"]:
                break
        batches = sorted({run["Batch"] for run in runs})
        for run in runs:
            if run["Error"]:
                failed = True
                print(f"lane {run['Lane']} stopped on an error:\n{run['Error'][:1500]}")
        if versus:
            tally = {"new": 0, "old": 0, "draw": 0}
            by_pair = {}
            for run in runs:
                pair = by_pair.setdefault(f"{run['A']} vs {run['B']}", {"new": 0, "old": 0, "draw": 0})
                for result in run["Results"]:
                    side = run["SetA"] if result["Winner"] == run["A"] else run["SetB"] if result["Winner"] == run["B"] else "draw"
                    tally[side] += 1
                    pair[side] += 1
            total = sum(tally.values())
            print(f"new weights won {tally['new']} of {total} matches, old weights {tally['old']}, draws {tally['draw']}")
            for pair, counts in by_pair.items():
                print(f"    {pair}: new {counts['new']}, old {counts['old']}, draws {counts['draw']}")
        left = request(runtime, "arena-status")
        count = left["Data"]["Count"] if left["Ok"] else "?"
        print(f"bots left in the world: {count}; server memory {server_memory()}")
        failed = failed or count != 0
    finally:
        down = tool("scrim_server.py", "stop", game)
        print(down.stdout.strip() or down.stderr.strip(), flush=True)

    folder = os.path.join(runtime, "server", "logs", "scrims")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8") as handle:
        for batch in batches:
            report = tool("pvp_analyze.py", folder, "--batch", batch).stdout
            handle.write(report + "\n")
            print()
            for line in report.splitlines():
                text = line.strip()
                if line.startswith("Batch ") or text.startswith(("wins:", "WARNING", "asleep ten", "mezzes landing", "attacks landing", "kill-target calls")) or line.startswith("team "):
                    print(line.split(", stunned")[0])
    print(f"\nfull reports: {out}")
    print(f"total time: {(time.time() - started) / 60:.1f} minutes")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
