"""Send one request to the 8v8 control channel of a running server and print the answer.

    python pvp_request.py <game folder>\\runtime ping
    python pvp_request.py <game folder>\\runtime audit-builds [Build=hero] [--out report.json]

The server (bots/pvp/PvpControlChannel.cs) polls pvp-request.json beside CoreServer.exe and
answers in pvp-result.json. Exit code 0 when the server answered Ok, 1 otherwise.
"""
import json
import os
import sys
import time
import uuid


def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return 2
    server = os.path.join(argv[1], "server")
    out = None
    args = {}
    rest = argv[3:]
    while rest:
        item = rest.pop(0)
        if item == "--out":
            out = rest.pop(0)
        elif "=" in item:
            key, value = item.split("=", 1)
            args[key] = value
        else:
            raise SystemExit(f"Unexpected argument: {item}")
    request = {"Id": uuid.uuid4().hex, "Command": argv[2], "Args": args}
    request_path = os.path.join(server, "pvp-request.json")
    result_path = os.path.join(server, "pvp-result.json")
    temporary = request_path + ".tmp"
    with open(temporary, "w", encoding="utf-8") as handle:
        json.dump(request, handle)
    os.replace(temporary, request_path)

    deadline = time.time() + 120
    while time.time() < deadline:
        time.sleep(0.5)
        try:
            with open(result_path, encoding="utf-8") as handle:
                result = json.load(handle)
        except (OSError, ValueError):
            continue
        if result.get("Id") != request["Id"]:
            continue
        if out:
            with open(out, "w", encoding="utf-8") as handle:
                json.dump(result, handle, indent=2)
            print(f"{result['Command']}: {'ok' if result['Ok'] else 'FAILED'} -> {out}")
        else:
            print(json.dumps(result, indent=2))
        if not result["Ok"]:
            print(result.get("Error") or "", file=sys.stderr)
        return 0 if result["Ok"] else 1
    # A request nobody took is not left behind: a server started later would carry it out (a "stop-server" asked of a
    # game that was not running stopped it as soon as it came up).
    try:
        with open(request_path, encoding="utf-8") as handle:
            if json.load(handle).get("Id") == request["Id"]:
                os.remove(request_path)
    except (OSError, ValueError):
        pass
    print("No answer within 120 seconds. Is the server running, and is it this build?", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
