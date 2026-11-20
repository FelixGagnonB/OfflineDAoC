"""A night of 8v8 scrims, unattended: several batches one after another, then the insight report.

    python pvp_night.py [Varied=3] [Plain=1] [Matches=34] [Vary=50] [Spread=40]
                        [--game D:\\OfflineDAoC-scrim] [--out report.txt]

Each batch is one run of pvp_batch.py with the three pairings in two lanes each (six lanes at
once), so a batch of Matches=34 is 204 matches and takes about two hours and a half. The server
is started fresh for every batch and stopped after it, so one batch failing does not cost the
night. The plain batches come first and fight with the built-in weights: they are what the
stories and the missed chances are read from. The varied batches follow, with every team
drawing its own weights (pvp_batch.py Vary=): they are what shows which weights matter.

At the end pvp_insight.py is run twice, on the plain batches (stories, missed chances) and on
the varied ones (weights), and both reports go to --out (default:
<game folder>\\runtime\\logs\\pvp-night-<time>.txt). The same reports can be made again later
from the recordings; the two commands are printed at the end.
"""
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULTS = {"Varied": "3", "Plain": "1", "Matches": "34", "Vary": "50", "Spread": "40"}


def main(argv):
    options = dict(DEFAULTS)
    game, out = r"D:\OfflineDAoC-scrim", None
    rest = argv[1:]
    while rest:
        item = rest.pop(0)
        if item in ("-h", "--help"):
            print(__doc__)
            return 0
        if item == "--game":
            game = rest.pop(0)
        elif item == "--out":
            out = rest.pop(0)
        elif "=" in item and item.split("=", 1)[0] in DEFAULTS:
            key, value = item.split("=", 1)
            options[key] = value
        else:
            print(f"Unexpected argument: {item}\n\n{__doc__}")
            return 2
    runtime = os.path.join(game, "runtime")
    folder = os.path.join(runtime, "server", "logs", "scrims")
    out = out or os.path.join(runtime, "logs", f"pvp-night-{time.strftime('%Y%m%d-%H%M%S')}.txt")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    started = time.time()
    # Recordings are named by the UTC time their batch began: everything from here on is this night's.
    first = {}
    failed = 0
    plan = ["plain"] * int(options["Plain"]) + ["varied"] * int(options["Varied"])
    for number, kind in enumerate(plan, 1):
        stamp = time.strftime("%Y%m%d-%H%M%S", time.gmtime())
        first.setdefault(kind, stamp)
        print(f"\n=== batch {number} of {len(plan)} ({kind}), {(time.time() - started) / 3600:.1f} h into the night ===", flush=True)
        args = [sys.executable, os.path.join(HERE, "pvp_batch.py"), f"Matches={options['Matches']}", "Copies=2", "--game", game]
        if kind == "varied":
            args += [f"Vary={options['Vary']}", f"Spread={options['Spread']}"]
        done = subprocess.run(args)
        if done.returncode != 0:
            failed += 1
            print(f"batch {number} ended with an error (code {done.returncode}); going on with the next.", flush=True)
            # Whatever happened, no server may be left running for the next batch.
            subprocess.run([sys.executable, os.path.join(HERE, "scrim_server.py"), "stop", game])
        time.sleep(5)

    insight = os.path.join(HERE, "pvp_insight.py")
    commands = []
    if "plain" in first:
        # The plain batches end where the varied ones begin.
        commands.append(("Stories and missed chances, from the plain batches",
                         [sys.executable, insight, folder, "--since", first["plain"]] + (["--until", first["varied"]] if "varied" in first else [])))
    if "varied" in first:
        commands.append(("Which weights matter, from the varied batches", [sys.executable, insight, folder, "--since", first["varied"], "--weights"]))
    with open(out, "w", encoding="utf-8") as handle:
        for title, command in commands:
            report = subprocess.run(command, capture_output=True, text=True).stdout
            handle.write(f"{title}\n{'=' * len(title)}\n{report}\n")
            print(f"\n{title}\n{report}")
    print(f"night's report: {out}")
    for title, command in commands:
        print(f"again: python {' '.join(command[1:])}")
    print(f"total time: {(time.time() - started) / 3600:.1f} hours; {failed} of {len(plan)} batches ended with an error")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
