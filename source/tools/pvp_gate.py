#!/usr/bin/env python3
"""The regression gate of the 8v8 bots (docs/PVP-8V8-ROADMAP.md, M10.3).

A fixed set of bots-only matches is judged by the referee's rules (pvp_referee.py) and compared with
a baseline kept in the repository (pvp-gate-baseline.json). A rule that passed in the baseline and
fails now is a drop, and the gate fails (exit code 1). A rule that failed before and fails still is
listed and does not block; one that passes now is listed as gained.

    python pvp_gate.py --run [--game D:\\OfflineDAoC-scrim]      plays the fixed set, then checks it
    python pvp_gate.py <scrims folder> --batch <prefix>          checks a batch that was played already
    ... --update                                                  makes that batch the new baseline

The fixed set: the three main pairings, four matches each, realm rank 5, five minutes at most
(pvp_batch.py; about twenty minutes). Twelve matches swing: a drop is a reason to look, with
pvp_referee.py --before, and --update is for when the look says the new build is the better one.
Only the rules' titles, marks and measured values are kept in the baseline, no names.
"""
import datetime
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pvp_referee  # noqa: E402

BASELINE = os.path.join(HERE, "pvp-gate-baseline.json")
FIXED_SET = ["Pairs=alb-main:mid-main,mid-main:hib-main,alb-main:hib-main", "Matches=4", "Copies=1", "RealmRank=5", "Limit=300"]


def judge(folder, batch):
    matches = pvp_referee.load(folder, batch)
    if not matches:
        sys.exit("No finished match of batch %s in %s." % (batch, folder))
    results = [rule(matches) for rule in pvp_referee.RULES]
    return {
        "made": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d %H:%M UTC"), "batch": batch, "matches": len(matches),
        "rules": [{"title": r.title, "passed": r.passed, "measured": r.measured} for r in results],
    }


def compare(now, before):
    """The lines of the comparison, and how many rules dropped."""
    was = {rule["title"]: rule for rule in before["rules"]}
    lines, drops = [], 0
    for rule in now["rules"]:
        old = was.get(rule["title"])
        if old is None:
            mark = "new "
        elif old["passed"] and rule["passed"] is False:
            mark, drops = "DROP", drops + 1
        elif old["passed"] is False and rule["passed"]:
            mark = "gain"
        elif rule["passed"] is False:
            mark = "fail"
        else:
            mark = "ok  "
        lines.append("[%s] %s" % (mark, rule["title"]))
        lines.append("         now:    %s" % rule["measured"])
        if old is not None:
            lines.append("         before: %s" % old["measured"])
    return lines, drops


def play(game):
    """Plays the fixed set and returns the scrims folder and the batch's prefix."""
    folder = os.path.join(game, "runtime", "server", "logs", "scrims")
    before = set(os.listdir(folder)) if os.path.isdir(folder) else set()
    done = subprocess.run([sys.executable, os.path.join(HERE, "pvp_batch.py"), *FIXED_SET, "--game", game])
    if done.returncode != 0:
        sys.exit("The fixed set did not run to its end (pvp_batch.py: %d)." % done.returncode)
    new = sorted(name for name in set(os.listdir(folder)) - before if name.endswith(".jsonl"))
    if not new:
        sys.exit("The fixed set left no recording in %s." % folder)
    return folder, new[0][:15]


def main(argv):
    args = argv[1:]
    if not args:
        print(__doc__)
        return 1
    update = "--update" in args
    game = args[args.index("--game") + 1] if "--game" in args else r"D:\OfflineDAoC-scrim"
    if "--run" in args:
        folder, batch = play(game)
    elif "--batch" in args:
        folder, batch = args[0], args[args.index("--batch") + 1]
    else:
        print(__doc__)
        return 1
    now = judge(folder, batch)
    print("8v8 gate: %d matches of batch %s" % (now["matches"], batch))
    if update or not os.path.isfile(BASELINE):
        with open(BASELINE, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(now, handle, indent=1)
            handle.write("\n")
        failing = sum(1 for rule in now["rules"] if rule["passed"] is False)
        print("%s: %d rules, %d of them failing. Written to %s" % ("New baseline" if update else "First baseline", len(now["rules"]), failing, BASELINE))
        return 0
    with open(BASELINE, encoding="utf-8") as handle:
        before = json.load(handle)
    print("against the baseline of %s (%d matches of batch %s)\n" % (before["made"], before["matches"], before["batch"]))
    lines, drops = compare(now, before)
    print("\n".join(lines))
    print("\n%s" % ("GATE FAILS: %d rule%s dropped." % (drops, "" if drops == 1 else "s") if drops else "Gate passes: no rule dropped."))
    return 1 if drops else 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main(sys.argv))
