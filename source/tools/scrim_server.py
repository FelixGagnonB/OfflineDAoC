"""Start, stop and check the scrim server without the launcher (docs/PVP-8V8-ROADMAP.md).

    python scrim_server.py status [<game folder>]
    python scrim_server.py start  [<game folder>]     waits until the server accepts connections
    python scrim_server.py stop   [<game folder>]     orderly stop through the 8v8 control channel
    python scrim_server.py stop   [<game folder>] --force    also ends a server that does not answer

The game folder defaults to D:\\OfflineDAoC-scrim. Any other folder must have "scrim" in its
name: this tool must never start or stop the game the owner plays. Only one server can use
port 10300, so `start` refuses when anything already listens there.
"""
import json
import os
import socket
import subprocess
import sys
import time

DEFAULT_GAME = r"D:\OfflineDAoC-scrim"
PORT = 10300


def port_open():
    with socket.socket() as probe:
        probe.settimeout(1)
        return probe.connect_ex(("127.0.0.1", PORT)) == 0


def servers():
    """Running CoreServer processes as (pid, executable path)."""
    # PowerShell exits 1 when no such process exists; that is "none", not an error.
    out = subprocess.run(
        ["powershell", "-NoProfile", "-Command",
         "Get-Process CoreServer -ErrorAction SilentlyContinue | Select-Object Id, Path | ConvertTo-Json -Compress"],
        capture_output=True, text=True).stdout.strip()
    if not out:
        return []
    data = json.loads(out)
    return [(item["Id"], item["Path"] or "") for item in (data if isinstance(data, list) else [data])]


def ours(game):
    exe = os.path.normcase(os.path.join(game, "runtime", "server", "CoreServer.exe"))
    return [pid for pid, path in servers() if os.path.normcase(path) == exe]


def start(game):
    running = servers()
    if running or port_open():
        raise SystemExit(f"A server is already running or port {PORT} is in use: {running}")
    server = os.path.join(game, "runtime", "server")
    logs = os.path.join(game, "runtime", "logs")
    os.makedirs(logs, exist_ok=True)
    env = dict(os.environ)
    dotnet = os.path.join(game, "tools", "dotnet")
    if os.path.isdir(dotnet):   # the game's own .NET, as the launcher uses
        env["DOTNET_ROOT"] = env["DOTNET_ROOT_X64"] = dotnet
    for stale in ("pvp-request.json", "pvp-result.json"):
        try:
            os.remove(os.path.join(server, stale))
        except OSError:
            pass
    console = open(os.path.join(logs, "scrim-console.log"), "w", encoding="utf-8", errors="replace")
    process = subprocess.Popen(
        [os.path.join(server, "CoreServer.exe")], cwd=server, env=env,
        stdin=subprocess.DEVNULL, stdout=console, stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NO_WINDOW | subprocess.CREATE_NEW_PROCESS_GROUP)
    deadline = time.time() + 240
    while time.time() < deadline:
        if process.poll() is not None:
            raise SystemExit(f"The server exited during startup (code {process.returncode}). See {console.name}")
        if port_open():
            print(f"Scrim server is up (pid {process.pid}), port {PORT}. Console log: {console.name}")
            return 0
        time.sleep(1)
    raise SystemExit("The server did not open its port within 240 seconds.")


def stop(game, force):
    pids = ours(game)
    if not pids:
        print("The scrim server is not running.")
        return 0
    tool = os.path.join(os.path.dirname(os.path.abspath(__file__)), "pvp_request.py")
    asked = subprocess.run([sys.executable, tool, os.path.join(game, "runtime"), "stop-server"],
                           capture_output=True, text=True)
    deadline = time.time() + (90 if asked.returncode == 0 else 0)
    while time.time() < deadline and ours(game):
        time.sleep(1)
    left = ours(game)
    if not left:
        print("Scrim server stopped.")
        return 0
    if not force:
        raise SystemExit(f"The scrim server (pid {left}) did not stop on request. Use --force to end it.")
    for pid in left:
        subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], capture_output=True)
    print(f"Scrim server ended by force (pid {left}).")
    return 0


def main(argv):
    force = "--force" in argv
    argv = [a for a in argv if a != "--force"]
    if len(argv) < 2 or argv[1] not in ("status", "start", "stop"):
        print(__doc__)
        return 2
    game = os.path.abspath(argv[2]) if len(argv) > 2 else DEFAULT_GAME
    if "scrim" not in os.path.basename(game).lower():
        raise SystemExit(f"{game} is not a scrim folder. This tool only handles folders with 'scrim' in the name.")
    if not os.path.isfile(os.path.join(game, "runtime", "server", "CoreServer.exe")):
        raise SystemExit(f"No server found in {game}.")
    if argv[1] == "status":
        mine = ours(game)
        others = [entry for entry in servers() if entry[0] not in mine]
        print(f"scrim server: {'running, pid ' + str(mine) if mine else 'not running'}; "
              f"port {PORT}: {'open' if port_open() else 'closed'}; other servers: {others or 'none'}")
        return 0
    return start(game) if argv[1] == "start" else stop(game, force)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
