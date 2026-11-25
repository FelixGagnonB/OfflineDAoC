"""Build, install on the scrim copy and run an evening of bots there, in one go: the check before a build goes to the game that is played.

    python scrim_test.py                     build, unit tests, install, eight minutes of twelve groups a realm
    python scrim_test.py --minutes 15        a longer evening
    python scrim_test.py --groups 8          fewer groups of each realm
    python scrim_test.py --skip-build        install and run what is built already
    python scrim_test.py --game <folder>     another scrim copy (its name must have "scrim" in it)

It does by itself what was done by hand before every install: `dotnet build`, `deploy_scrim.ps1
-Apply` (which runs the unit tests and keeps a backup), `scrim_server.py start --test`, an
evening of bots (`roam-start Prime=<groups> Zones=tonight`), a reading of the server log and
of the recording, and the stop of the evening and of the server.

Everything runs at below normal priority, so the game that is played keeps the processor.
The game that is played may be running the whole time: the scrim server uses the test ports
(scrim_server.py), and the install looks only at the scrim copy's own server and client. It
never touches the game that is played. It does not start when the scrim copy is in use (an
evening or a fight is running on it, or a game client of the scrim copy is open).

The answer is printed and kept in <game>\\runtime\\logs\\scrim-test-last.txt. Exit code 0: the
build passed (no failed tick, no failed ability, no fault in the log once the server was up, and,
in an evening of six minutes or more, fights were fought); 1: it did not; 2: it could not be run.
"""
import collections
import glob
import json
import math
import os
import subprocess
import sys
import time

import scrim_server

HERE = os.path.dirname(os.path.abspath(__file__))
SERVER_SOURCE = os.path.normpath(os.path.join(HERE, "..", "server"))
# The zones where nobody adds (PvpRoam.EightZone, PvpRealWeights.CleanZonesHaveNoAdds).
NO_ADDS = {"thid", "breifine", "cruachan", "collory", "jamtland", "uppland", "yggdra", "pennine", "sauvage", "snowdonia"}
# Written at every start of the server, by the game's own code: not a fault of a build.
KNOWN = ("An item with the same key has already been added. Key: &tc",)


def run(command, **more):
    # Below normal priority: a build and the unit tests take every processor for a minute, and the game that is
    # played must not wait for them (2026-11-25: its server's loop fell to 62% of its rate during a build).
    return subprocess.run(command, capture_output=True, text=True, errors="replace",
                          creationflags=subprocess.BELOW_NORMAL_PRIORITY_CLASS, **more)


def request(runtime, *words):
    """One request to the server's control channel; its answer as a dict, or None."""
    done = run([sys.executable, os.path.join(HERE, "pvp_request.py"), runtime, *words])
    start = done.stdout.find("{")
    try:
        return json.loads(done.stdout[start:]) if start >= 0 else None
    except ValueError:
        return None


def running(answer):
    return '"Running": true' in json.dumps(answer or {})


def client_of(game):
    """Game clients running out of this game folder."""
    out = run(["powershell", "-NoProfile", "-Command",
               "Get-Process game, camelot, connect -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Path"]).stdout
    root = os.path.normcase(game.rstrip("\\") + "\\")
    return [line for line in out.splitlines() if os.path.normcase(line.strip()).startswith(root)]


def read_log(path, offset):
    try:
        with open(path, "rb") as handle:
            if os.path.getsize(path) < offset:
                offset = 0   # a new log file
            handle.seek(offset)
            return handle.read().decode("utf-8", errors="replace").splitlines()
    except OSError:
        return []


def read_recording(path):
    """What the evening's recording says: teams, kinds, zones, fights, and how clean the zones with no adds were."""
    rows = []
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            if line.strip():
                try:
                    rows.append(json.loads(line))
                except ValueError:
                    pass   # the last line, cut off while the server was writing
    if not rows:
        return None
    head = rows[0]
    kinds = collections.Counter(str(team.get("kind")) for team in head.get("teams", []))
    events = collections.Counter(row["ev"]["what"] for row in rows if row.get("k") == "ev" and isinstance(row.get("ev"), dict))
    zones = {}
    one = collections.Counter()
    more = collections.Counter()
    beside = collections.Counter()
    moments = collections.defaultdict(list)
    for row in rows:
        if row.get("k") != "team" or not row.get("zone"):
            continue
        zones[row["name"]] = row["zone"]
        foes = len(row.get("fighting") or [])
        if foes == 1:
            one[row["zone"]] += 1
        elif foes > 1:
            more[row["zone"]] += 1
        if foes:
            moments[(row["zone"], int(row.get("t", 0)) // 1000)].append(row)
    # Two fights in one place: a team in a fight with another fight, not its own, within 4,500 units (PvpRoam.OneFightWithin).
    for (zone, _), there in moments.items():
        for row in there:
            if any(other is not row and other["name"] not in row["fighting"] and row["name"] not in other["fighting"] and
                   not set(other["fighting"]) & set(row["fighting"]) and
                   math.dist((row["x"], row["y"]), (other["x"], other["y"])) < 4500 for other in there):
                beside[zone] += 1
    return dict(teams=len(head.get("teams", [])), kinds=dict(kinds), events=dict(events),
                zones=dict(collections.Counter(zones.values())), one=dict(one), more=dict(more), beside=dict(beside))


def main(argv):
    def option(name, otherwise):
        return argv[argv.index(name) + 1] if name in argv else otherwise
    minutes = float(option("--minutes", 8))
    groups = int(option("--groups", 12))
    game = os.path.abspath(option("--game", scrim_server.DEFAULT_GAME))
    if "scrim" not in os.path.basename(game).lower():
        raise SystemExit(f"{game} is not a scrim folder. This tool only handles folders with 'scrim' in the name.")
    runtime = os.path.join(game, "runtime")
    lines = []

    def say(text):
        print(text, flush=True)
        lines.append(text)

    def finish(code):
        try:
            with open(os.path.join(runtime, "logs", "scrim-test-last.txt"), "w", encoding="utf-8") as handle:
                handle.write("\n".join(lines) + "\n")
        except OSError:
            pass
        return code

    say(f"Scrim test, {time.strftime('%Y-%m-%d %H:%M')}: {groups} groups a realm for {minutes:g} minutes on {game}")
    others = [path for _, path in scrim_server.servers() if os.path.normcase(path) != os.path.normcase(os.path.join(runtime, "server", "CoreServer.exe"))]
    if others:
        say(f"Another server is running and is left alone: {', '.join(others)}")

    # 1. The scrim copy must be free.
    if client_of(game):
        say("NOT RUN: a game client of the scrim copy is open.")
        return finish(2)
    if scrim_server.ours(game):
        if running(request(runtime, "roam-status")) or running(request(runtime, "scrim-status")):
            say("NOT RUN: an evening or a fight is running on the scrim copy.")
            return finish(2)
        scrim_server.stop(game, False)

    # 2. Build.
    if "--skip-build" not in argv:
        built = run(["dotnet", "build", "Dawn of Light.sln", "-c", "Release", "--nologo", "-v", "q"], cwd=SERVER_SOURCE)
        errors = sorted({line.strip() for line in built.stdout.splitlines() if " error " in line})
        if built.returncode != 0 or errors:
            say(f"FAILED: the build has {len(errors)} errors.")
            for line in errors[:10]:
                say("  " + line[:300])
            return finish(1)
        say("Build: no errors.")

    # 3. Unit tests, backup and install (deploy_scrim.ps1 does the three).
    deployed = run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                    os.path.join(SERVER_SOURCE, "tools", "deploy_scrim.ps1"), "-Runtime", runtime, "-Apply"])
    out = deployed.stdout + deployed.stderr
    tests = next((line.strip() for line in out.splitlines() if "Passed!" in line or "Failed!" in line), None)
    backup = next((line.strip() for line in out.splitlines() if "Backup:" in line), None)
    already = next((line.strip() for line in out.splitlines() if "already has this build" in line), None)
    if deployed.returncode == 0 and already:
        say("Install: the scrim copy has this build already (its unit tests ran when it was installed).")
    elif deployed.returncode != 0 or backup is None:
        say("FAILED: the build was not installed on the scrim copy.")
        for line in [line for line in out.splitlines() if line.strip()][-8:]:
            say("  " + line[:300])
        return finish(1)
    else:
        say(f"Unit tests: {tests}")
        say(backup)

    # 4. The server, on the test ports; then an evening of bots.
    log = os.path.join(runtime, "server", "logs", "server.log")
    began = time.time()
    try:
        scrim_server.start(game, scrim_server.TEST_PORT, low=True)
    except SystemExit as failure:
        say(f"FAILED: the server did not start: {failure}")
        return finish(1)
    # (The log is read from here: what the game's own data makes the server write at every start is no fault of a build.)
    offset = os.path.getsize(log) if os.path.isfile(log) else 0
    passed = False
    try:
        answer = request(runtime, "roam-start", f"Prime={groups}", "Pool=kite1", "Listener=hibernia", "Zones=tonight", "Map=emain")
        if not answer or not answer.get("Ok"):
            say(f"FAILED: the evening did not begin: {json.dumps(answer)[:400]}")
            return finish(1)
        deadline = time.time() + minutes * 60
        while time.time() < deadline:
            time.sleep(min(30, max(1, deadline - time.time())))
            if not scrim_server.ours(game):
                break
        alive = bool(scrim_server.ours(game))
        still = alive and running(request(runtime, "roam-status"))

        # 5. What the log and the recording say.
        text = read_log(log, offset)
        ticks = [line for line in text if "roam tick failed" in line]
        abilities = [line for line in text if "BOT_REALM_ABILITY_FAILED" in line]
        faults = [line for line in text if ("Exception" in line or "| ERR |" in line or "| FTL |" in line) and not any(known in line for known in KNOWN)]
        used = sum(1 for line in text if "BOT_REALM_ABILITY_USED" in line)
        recordings = [path for path in glob.glob(os.path.join(runtime, "server", "logs", "roams", "*.jsonl")) if os.path.getmtime(path) >= began]
        read = read_recording(max(recordings, key=os.path.getmtime)) if recordings else None
        say(f"Server: {'up' if alive else 'DOWN'} at the end, the evening {'still running' if still else 'NOT running'}.")
        say(f"Log: {len(ticks)} failed ticks, {len(abilities)} failed abilities, {len(faults)} other faults, {used} realm abilities used.")
        for line in (ticks + abilities + faults)[:8]:
            say("  " + line[:300])
        fights = 0
        if read is None:
            say("Recording: none was written.")
        else:
            fights = read["events"].get("fight", 0) // 2
            say(f"Recording: {read['teams']} teams ({', '.join(f'{count} {kind}' for kind, count in sorted(read['kinds'].items()))}); "
                f"{fights} fights, {read['events'].get('wipe', 0)} wipes.")
            say("Zones: " + ", ".join(f"{zone} {count}" for zone, count in sorted(read["zones"].items())))
            for zone in sorted(set(read["one"]) | set(read["more"])):
                one, more = read["one"].get(zone, 0), read["more"].get(zone, 0)
                say(f"  {zone}{' (nobody adds)' if zone in NO_ADDS else ''}: in a fight with one enemy group {one} team-samples, with two or more {more}"
                    + (" <- LOOK AT THIS" if zone in NO_ADDS and more > 0 else "")
                    + (f"; beside another fight {read['beside'].get(zone, 0)}" if zone in NO_ADDS else ""))
        # (Groups come out one after the other and buff first: in less than six minutes there may be no fight yet.)
        fought = fights > 0 or minutes < 6
        if fights == 0:
            say("No fight was fought" + (": too short an evening to expect one." if fought else "."))
        passed = alive and still and not ticks and not abilities and not faults and fought
    finally:
        # 6. The evening and the server are stopped again, whatever happened.
        if scrim_server.ours(game):
            request(runtime, "scrim-stop")
            try:
                scrim_server.stop(game, True)
            except SystemExit as failure:
                say(str(failure))
        # (The next plain start is on the port a game client connects to.)
        scrim_server.set_ports(game, scrim_server.PORT)
    say("PASSED: this build can go to the game that is played." if passed else "FAILED: read the lines above before this build goes anywhere.")
    return finish(0 if passed else 1)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
