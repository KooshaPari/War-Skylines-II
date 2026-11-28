---
name: cs2-ops
description: Drive Cities Skylines II safely for P1-H evidence using tools/cs2ctl.py. Use whenever launching, stopping, or reading the game, or when checking whether the mod ticked. Never launch Cities2.exe directly.
---

# CS2 ops (P1-H evidence runs)

## Hard rules

- **Never launch `Cities2.exe` directly.** Bypassing Steam and the Paradox launcher leaves a
  permanent black-frame process (operator-confirmed 2026-11-28). Always `cs2ctl launch`, which opens
  `steam://rungameid/949230`, then `cs2ctl play` to press Play in the launcher.
- **One driver at a time.** This machine runs several agent panes; another pane may be using the
  game. Check `cs2ctl status` and say what you are about to do in the pane before taking it.
- **Stop with a close request first.** `cs2ctl stop` posts WM_CLOSE and waits; only `--force` kills.

## Loop

```powershell
py tools\cs2ctl.py status                 # steam, pids, mod log, heartbeat counts
py tools\cs2ctl.py launch                 # Steam URL only
py tools\cs2ctl.py play                   # clicks Play in the launcher
py tools\cs2ctl.py await-mod --timeout 1800   # waits for the mod's own "OnLoad exit"
py tools\cs2ctl.py evidence --tail 20     # heartbeat counts + log tail
py tools\cs2ctl.py stop                   # graceful, then --force if needed
```

`await-mod` is the honest readiness signal: the mod logs `OnLoad exit` when the game has loaded
mods, which is later than the window appearing and much later than the process starting.

## What "success" looks like

- `status` shows `mod_log_present: true` and `heartbeat_lateupdate` climbing while the game idles.
- `heartbeat_gamesimulation` stays 0 until a city is actually simulating. Only a loaded save makes
  it non-zero, and loading a save is an in-menu action, not a launch flag.

## Verified facts this skill encodes

- `Game.Modding.IMod.OnLoad` runs before the main menu is interactive; `p1h.log` appearing is the
  first usable readiness signal.
- UIA exposes **zero** buttons inside the Unity game window, so gameplay clicks cannot be found by
  name. The Paradox launcher is Chromium-based and does expose buttons; that is why `play` works
  and why menu navigation inside the game does not.
- Direct-exe launch leaves a black-frame process; Steam + launcher does not.

## Failure modes seen

| Symptom | Cause | Action |
|---|---|---|
| Black frame, process alive, no log | direct `Cities2.exe` launch | kill it, relaunch via `launch` + `play` |
| Window exists, no `p1h.log` for many minutes | still loading mods/asset DB | keep waiting; `await-mod` covers 30 min |
| `p1h.log` present, `mod-dir` shows the install root | old build without the path fix | redeploy (`scripts\check.ps1 -Deploy`) |
| `heartbeat_gamesimulation: 0` with lateupdate climbing | no city loaded | expected; needs a save |
