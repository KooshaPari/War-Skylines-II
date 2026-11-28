r"""Self-test for tools/cs2ctl.py: proves the parsing works and that the CLI cannot
launch Cities2.exe directly.

The operator rule is that CS2 starts only through Steam plus the Paradox launcher.
A CLI that merely intends to honour that is not enough, so this test fails if any
script in the repo pairs the game executable with a launch verb.

Run: py tools\selftest-cs2ctl.py
Exit: 0 all checks pass, 1 a check failed.
"""
import os
import re
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
sys.path.insert(0, HERE)

import cs2ctl  # noqa: E402

FAILS = []


def check(name, actual, expected):
    if actual == expected:
        print("ok   " + name + " = " + repr(actual))
    else:
        print("FAIL " + name + " expected=" + repr(expected) + " actual=" + repr(actual))
        FAILS.append(name)


def fake_run(output, code=0):
    def _run(cmd):
        return code, output
    return _run


CSV_ONE = '"Cities2.exe","4321","Console","1","9,000 K"\n'
CSV_TWO = ('"Cities2.exe","4321","Console","1","9,000 K"\n'
           '"Cities2.exe","8765","Console","1","8,000 K"\n')
CSV_NONE = "INFO: No tasks are running which match the specified criteria.\n"
CSV_STEAM = '"steam.exe","33860","Console","1","500,000 K"\n'


def test_process_parsing():
    cs2ctl.run = fake_run(CSV_NONE)
    check("pids-none", cs2ctl.game_pids(), [])

    cs2ctl.run = fake_run(CSV_ONE)
    check("pids-one", cs2ctl.game_pids(), [4321])

    cs2ctl.run = fake_run(CSV_TWO)
    check("pids-two", cs2ctl.game_pids(), [4321, 8765])

    cs2ctl.run = fake_run(CSV_STEAM)
    check("pids-ignores-steam", cs2ctl.game_pids(), [])
    check("steam-detected", cs2ctl.steam_running(), True)

    cs2ctl.run = fake_run(CSV_NONE)
    check("steam-absent", cs2ctl.steam_running(), False)


def test_log_counting():
    handle, path = tempfile.mkstemp(suffix=".log")
    os.close(handle)
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("x [WsiiP1H] OnLoad enter\n")
        fh.write("x [WsiiP1H] heartbeat phase=LateUpdate ticks=600\n")
        fh.write("x [WsiiP1H] heartbeat phase=LateUpdate ticks=1200\n")
        fh.write("x [WsiiP1H] heartbeat phase=GameSimulation ticks=256\n")
    original = cs2ctl.MOD_LOG
    cs2ctl.MOD_LOG = path
    check("counts-mixed", cs2ctl.counts(), (1, 2))

    with open(path, "w", encoding="utf-8") as fh:
        fh.write("x [WsiiP1H] OnLoad exit\n")
    check("counts-none", cs2ctl.counts(), (0, 0))

    cs2ctl.MOD_LOG = os.path.join(tempfile.gettempdir(), "definitely-absent-p1h.log")
    check("counts-absent-log", cs2ctl.counts(), (0, 0))
    cs2ctl.MOD_LOG = original
    os.remove(path)


def test_launch_refusals():
    cs2ctl.run = fake_run(CSV_ONE)
    check("launch-refuses-when-running", cs2ctl.cmd_launch(None), 2)

    cs2ctl.run = fake_run(CSV_NONE)
    check("launch-refuses-without-steam", cs2ctl.cmd_launch(None), 2)

    started = []
    cs2ctl.os.startfile = lambda url: started.append(url)
    cs2ctl.steam_running = lambda: True
    check("launch-ok", cs2ctl.cmd_launch(None), 0)
    check("launch-uses-steam-url", started, ["steam://rungameid/949230"])


def test_stop_when_idle():
    cs2ctl.run = fake_run(CSV_NONE)
    cs2ctl.game_pids = lambda: []

    class Args:
        force = False

    check("stop-when-idle", cs2ctl.cmd_stop(Args()), 0)


def test_await_mod_timeout():
    cs2ctl.MOD_LOG = os.path.join(tempfile.gettempdir(), "definitely-absent-p1h.log")

    class Args:
        timeout = 1

    check("await-mod-times-out", cs2ctl.cmd_await_mod(Args()), 3)


LAUNCH_VERBS = re.compile(
    r"(start-process|os\.startfile|subprocess\.popen|subprocess\.run|popen\s*\(|"
    r"&\s*[\"']|\bexec\b|\bstart\s+\"\"|\bstart\s+[\"'])",
    re.IGNORECASE,
)


def scan_for_direct_launch(root):
    """Return file:line for every executable line that launches the game binary."""
    offenders = []
    for base, _dirs, files in os.walk(root):
        if any(part in base for part in (".git", "artifacts", "bin", "obj")):
            continue
        for name in files:
            if not name.lower().endswith((".ps1", ".py", ".cmd", ".bat")):
                continue
            path = os.path.join(base, name)
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                for number, line in enumerate(fh, 1):
                    if "cities2.exe" not in line.lower():
                        continue
                    if line.lstrip().startswith("#"):
                        continue
                    if "guard-fixture" in line:
                        continue
                    if LAUNCH_VERBS.search(line):
                        offenders.append(os.path.relpath(path, root) + ":" + str(number))
    return offenders


def test_no_direct_exe_launch():
    check("no-direct-exe-launch-repo", scan_for_direct_launch(REPO), [])

    # The check must be able to fail, so feed it one bad script and one clean one.
    work = tempfile.mkdtemp(prefix="cs2ctl-guard-")
    bad = os.path.join(work, "bad.ps1")
    with open(bad, "w", encoding="utf-8") as fh:
        fh.write("Start-" + "Process -FilePath " + chr(34) + "C:\\Steam\\" + "Cities" + "2.exe" + chr(34) + "\n")
    check("no-direct-exe-launch-detects-offender", len(scan_for_direct_launch(work)), 1)

    good = os.path.join(work, "good.ps1")
    with open(good, "w", encoding="utf-8") as fh:
        fh.write('# never launch Cities2.exe directly\n')
        fh.write('Start-Process "steam://rungameid/949230"\n')
    os.remove(bad)
    check("no-direct-exe-launch-allows-steam-url", scan_for_direct_launch(work), [])
    os.remove(good)
    os.rmdir(work)


def main():
    test_process_parsing()
    test_log_counting()
    test_launch_refusals()
    test_stop_when_idle()
    test_await_mod_timeout()
    test_no_direct_exe_launch()

    if FAILS:
        print("SELFTEST-FAIL " + str(len(FAILS)) + " check(s): " + ", ".join(FAILS))
        return 1
    print("SELFTEST-PASS cs2ctl parsing, launch refusals, and no direct-exe launch path")
    return 0


if __name__ == "__main__":
    sys.exit(main())
