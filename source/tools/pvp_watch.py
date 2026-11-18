"""Start 8v8 scrims on a scrim server that is already running, to watch them in the game.

    python pvp_watch.py [Pairs=alb-main:mid-main,mid-main:hib-main,alb-main:hib-main]
                        [Matches=20] [RealmRank=1] [Limit=300] [Vary=0]
                        [--game D:\\OfflineDAoC-scrim]
    python pvp_watch.py stop [--game D:\\OfflineDAoC-scrim]

Unlike pvp_batch.py this neither starts nor stops the server: start it from the launcher in
the scrim folder (START OFFLINE DAOC.cmd), so that you can enter the game on it. To fly and
teleport your account must be a GM: enter the game once, stop the server, tick "Make me GM" in
the launcher, start again.

Each pairing fights in its own lane of the arena in Emain Macha (region 200), match after
match. The tool prints where each lane is as a /jump command to type in the game (then /fly to
hover), and one line for every finished match. Every match is recorded as usual, so what you
see can be looked up afterwards: note the lane, the match number and roughly when.

Ctrl+C, or "pvp_watch.py stop", ends the fights and removes the bots. The server stays up.
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pvp_batch import request  # noqa: E402  (the control-channel helper)

DEFAULTS = {
    "Pairs": "alb-main:mid-main,mid-main:hib-main,alb-main:hib-main",
    "Matches": "20", "RealmRank": "1", "Limit": "300", "Vary": "0",
    "BrainA": "squad", "BrainB": "squad",
}


def main(argv):
    options = dict(DEFAULTS)
    game, stop = r"D:\OfflineDAoC-scrim", False
    rest = argv[1:]
    while rest:
        item = rest.pop(0)
        if item in ("-h", "--help"):
            print(__doc__)
            return 0
        if item == "--game":
            game = rest.pop(0)
        elif item == "stop":
            stop = True
        elif "=" in item and item.split("=", 1)[0] in DEFAULTS:
            key, value = item.split("=", 1)
            options[key] = value
        else:
            print(f"Unexpected argument: {item}\n\n{__doc__}")
            return 2
    if "scrim" not in os.path.basename(os.path.normpath(game)).lower():
        print("The game folder must be a scrim install (its name contains \"scrim\"): this tool never touches the game you play.")
        return 2
    runtime = os.path.join(game, "runtime")
    if not request(runtime, "ping")["Ok"]:
        print(f"No scrim server is answering. Start it from the launcher in {game} (START OFFLINE DAOC.cmd), wait until it is up, and run this again.")
        return 1
    if stop:
        done = request(runtime, "scrim-stop")
        print("Fights stopped and bots removed." if done["Ok"] else f"scrim-stop failed: {done['Error']}")
        return 0 if done["Ok"] else 1
    status = request(runtime, "scrim-status")
    if status["Ok"] and status["Data"] and status["Data"]["Running"]:
        print("Scrims are already running on this server. Use \"pvp_watch.py stop\" first to start others.")
        return 1
    if os.path.isfile(os.path.join(runtime, "server", "pvp-weights.json")):
        print("Note: a pvp-weights.json lies beside the server and replaces the built-in weights.")

    pairs = options["Pairs"].split(",")
    lanes = request(runtime, "scrim-lanes")
    if not lanes["Ok"]:
        print("lane check failed:", lanes["Error"])
        return 1
    begun = request(runtime, "scrim-start", Lanes="1", Copies="1", **options)
    if not begun["Ok"]:
        print("scrim-start failed:", (begun["Error"] or "")[:2000])
        return 1
    print(f"{len(pairs)} lanes fighting, {options['Matches']} matches each, realm rank {options['RealmRank']}. In the game, as a GM:\n")
    for lane, pair in zip(lanes["Data"], pairs):
        (ax, ay), (bx, by) = [tuple(int(v) for v in lane[end].split(",")) for end in ("A", "B")]
        height = int(lane["GroundMiddle"] or lane["GroundA"] or 0) + 100
        print(f"  lane {lane['Lane']}  {pair.replace(':', ' against ')}:   /jump to {(ax + bx) // 2} {(ay + by) // 2} {height} 200")
    print("\n  then /fly to hover over the fight. The groups start 3,000 units either side of that point and meet near it.")
    print("  Ctrl+C here ends the fights and removes the bots; the server stays up.\n", flush=True)

    started = time.time()
    seen = {}
    try:
        while True:
            time.sleep(5)
            status = request(runtime, "scrim-status")
            if not status["Ok"] or not status["Data"]:
                print("status failed:", status["Error"], flush=True)
                continue
            for run in status["Data"]["Runs"]:
                for result in run["Results"][seen.get(run["Lane"], 0):]:
                    print(f"{time.strftime('%H:%M:%S')} lane {run['Lane']} {run['A']} against {run['B']}, match {result['Number']}: "
                          f"{result['Winner']} by {result['End']} in {result['Seconds']} s, alive {result['AliveA']}/{result['AliveB']}"
                          f"   (recording: {run['Batch']}-match-{result['Number']:03d}.jsonl)", flush=True)
                seen[run["Lane"]] = len(run["Results"])
                if run["Error"]:
                    print(f"lane {run['Lane']} stopped on an error:\n{run['Error'][:1500]}", flush=True)
            if not status["Data"]["Running"]:
                break
    except KeyboardInterrupt:
        request(runtime, "scrim-stop")
        print("\nFights stopped and bots removed.")
    print(f"watched for {(time.time() - started) / 60:.0f} minutes; recordings are in {os.path.join(runtime, 'server', 'logs', 'scrims')}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
