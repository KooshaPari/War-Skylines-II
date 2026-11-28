"""cs2ctl - the supported way to drive Cities: Skylines II for P1-H checks.

Why this exists: launching Cities2.exe directly bypasses Steam and the Paradox
launcher and leaves a permanent black-frame process. This CLI only ever launches
through Steam, and only ever stops the game through a close request first.

Subcommands
  status                  process, steam, mod-log and heartbeat state
  launch                  start via steam://rungameid/949230 (never the exe)
  play                    click Play in the Paradox launcher window
  await-mod [--timeout s] wait until the mod's OnLoad proves the main menu is up
  evidence                print the mod log tail and heartbeat counts
  stop [--force]          close the game, escalating only if asked

Exit codes: 0 ok, 2 wrong state, 3 timeout, 4 not found.
"""
import argparse
import json
import os
import re
import subprocess
import sys
import time

APP_ID = "949230"
STEAM_URL = "steam://rungameid/" + APP_ID
DATA_DIR = os.path.expandvars(r"%LOCALAPPDATA%Low\Colossal Order\Cities Skylines II")
MOD_LOG = os.path.join(DATA_DIR, "Mods", "WsiiP1H", "p1h.log")


def run(cmd):
    try:
        out = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
        return out.returncode, out.stdout + out.stderr
    except Exception as exc:  # noqa: BLE001
        return 1, str(exc)


def game_pids():
    rc, out = run(["tasklist", "/fi", "imagename eq Cities2.exe", "/fo", "csv", "/nh"])
    pids = []
    for line in out.splitlines():
        parts = [p.strip('"') for p in line.split('","')]
        if parts and parts[0].lower().startswith("cities2"):
            try:
                pids.append(int(parts[1]))
            except (IndexError, ValueError):
                pass
    return pids


def steam_running():
    rc, out = run(["tasklist", "/fi", "imagename eq steam.exe", "/fo", "csv", "/nh"])
    return "steam.exe" in out.lower()


def counts():
    sim = late = 0
    if os.path.exists(MOD_LOG):
        with open(MOD_LOG, "r", encoding="utf-8", errors="replace") as fh:
            for line in fh:
                if "phase=GameSimulation ticks" in line:
                    sim += 1
                elif "phase=LateUpdate ticks" in line:
                    late += 1
    return sim, late


def cmd_status(args):
    pids = game_pids()
    sim, late = counts()
    state = {
        "steam": steam_running(),
        "game_pids": pids,
        "mod_log": MOD_LOG,
        "mod_log_present": os.path.exists(MOD_LOG),
        "mod_log_bytes": os.path.getsize(MOD_LOG) if os.path.exists(MOD_LOG) else 0,
        "heartbeat_lateupdate": late,
        "heartbeat_gamesimulation": sim,
    }
    print(json.dumps(state, indent=2))
    return 0 if not pids else 0


def cmd_launch(args):
    if game_pids():
        print("refusing: Cities2.exe already running; stop it first", file=sys.stderr)
        return 2
    if not steam_running():
        print("steam not running; start Steam first", file=sys.stderr)
        return 2
    os.startfile(STEAM_URL)
    print("launched via " + STEAM_URL + " (Steam then Paradox launcher)")
    return 0


def launcher_window():
    from pywinauto import Application
    try:
        app = Application(backend="uia").connect(path="bootstrapper-v2.exe", timeout=3)
    except Exception:  # noqa: BLE001
        try:
            app = Application(backend="uia").connect(title_re=".*(Paradox|Cities).*", timeout=3)
        except Exception:  # noqa: BLE001
            return None, None
    for win in app.windows():
        try:
            if win.is_visible():
                return app, win
        except Exception:  # noqa: BLE001
            continue
    return app, None


def cmd_play(args):
    deadline = time.time() + args.timeout
    while time.time() < deadline:
        app, win = launcher_window()
        if win is not None:
            break
        time.sleep(5)
    else:
        print("launcher window not found", file=sys.stderr)
        return 3

    names = []
    try:
        names = [b.window_text() for b in win.descendants(control_type="Button")]
    except Exception:  # noqa: BLE001
        pass
    play = next((n for n in names if re.search(r"\bplay\b", n, re.I)), None)
    if play is None:
        print("no Play button exposed; launcher buttons=" + json.dumps(names[:20]), file=sys.stderr)
        return 4

    win.child_window(title=play, control_type="Button").click_input()
    print("clicked " + play)
    return 0


def cmd_await_mod(args):
    deadline = time.time() + args.timeout
    while time.time() < deadline:
        if os.path.exists(MOD_LOG):
            with open(MOD_LOG, "r", encoding="utf-8", errors="replace") as fh:
                body = fh.read()
            if "OnLoad exit" in body:
                print("mod loaded; menu reached")
                return 0
        time.sleep(10)
    print("timeout waiting for mod OnLoad", file=sys.stderr)
    return 3


def cmd_evidence(args):
    sim, late = counts()
    print("heartbeat GameSimulation=" + str(sim) + " LateUpdate=" + str(late))
    if os.path.exists(MOD_LOG):
        with open(MOD_LOG, "r", encoding="utf-8", errors="replace") as fh:
            lines = fh.readlines()
        for line in lines[-args.tail:]:
            print(line.rstrip())
    return 0


def cmd_stop(args):
    pids = game_pids()
    if not pids:
        print("not running")
        return 0
    import win32con
    import win32gui

    def close_visible(h, _):
        if win32gui.IsWindowVisible(h) and win32gui.GetWindowText(h):
            win32gui.PostMessage(h, win32con.WM_CLOSE, 0, 0)

    win32gui.EnumWindows(close_visible, None)
    for _ in range(24):
        if not game_pids():
            print("closed")
            return 0
        time.sleep(5)
    if not args.force:
        print("still running; pass --force to kill", file=sys.stderr)
        return 2
    for pid in game_pids():
        run(["taskkill", "/PID", str(pid), "/F", "/T"])
    time.sleep(5)
    print("killed" if not game_pids() else "still running after kill", file=sys.stderr)
    return 0 if not game_pids() else 2


def main(argv):
    parser = argparse.ArgumentParser(prog="cs2ctl", description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="cmd", required=True)

    sub.add_parser("status").set_defaults(func=cmd_status)
    sub.add_parser("launch").set_defaults(func=cmd_launch)

    p = sub.add_parser("play")
    p.add_argument("--timeout", type=int, default=600)
    p.set_defaults(func=cmd_play)

    p = sub.add_parser("await-mod")
    p.add_argument("--timeout", type=int, default=1800)
    p.set_defaults(func=cmd_await_mod)

    p = sub.add_parser("evidence")
    p.add_argument("--tail", type=int, default=20)
    p.set_defaults(func=cmd_evidence)

    p = sub.add_parser("stop")
    p.add_argument("--force", action="store_true")
    p.set_defaults(func=cmd_stop)

    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
