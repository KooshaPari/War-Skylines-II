# Slice 1: load, admit, tick

Scope: the smallest end-to-end chain that can be observed in the real game.

## Acceptance criteria and results

Verified 2026-11-25 by running the real `Cities2.exe`, not by inspection.

| # | Criterion | How it is observed | Result |
|---|---|---|---|
| S1-1 | The mod compiles against the installed `Game.dll` | `dotnet build`: 0 warnings, 0 errors | **PASS** |
| S1-2 | The built assembly implements `Game.Modding.IMod` and derives the system from `Game.GameSystemBase` | `scripts/verify-assembly.ps1`, 6 checks | **PASS** |
| S1-3 | Command admission is correct and idempotent | `tests/Wsii.P1H.Semantics`, 14 checks | **PASS** |
| S1-4 | The mod is installed in the game's mods folder with a valid manifest | `scripts/deploy.ps1` output | **PASS** |
| S1-5 | `OnLoad` runs in the real game and writes `p1h.log` with the expected admissions | `Mods\WsiiP1H\p1h.log` after a game start | **PASS** |
| S1-6 | `P1HProbeSystem` ticks during simulation | `heartbeat` lines in `p1h.log` after a city loads | **NOT_RUN** |

## S1-5 evidence, quoted from the game

Log written by the deployed mod inside the real game process:

```
2026-11-25T12:23:49.200Z [WsiiP1H] OnLoad enter
2026-11-25T12:23:49.203Z [WsiiP1H] mod-dir C:/Users/koosh/AppData/LocalLow/Colossal Order/Cities Skylines II\Mods\WsiiP1H exists=True
2026-11-25T12:23:49.204Z [WsiiP1H] log-path C:/Users/koosh/AppData/LocalLow/Colossal Order/Cities Skylines II\Mods\WsiiP1H\p1h.log
2026-11-25T12:23:49.207Z [WsiiP1H] admit id=cmd-1 type=noop result=Accepted:ok
2026-11-25T12:23:49.208Z [WsiiP1H] admit id=cmd-1 type=noop result=Replayed:duplicate-id
2026-11-25T12:23:49.209Z [WsiiP1H] admit id=cmd-1 type=noop result=Rejected:payload-conflict:cmd-1
2026-11-25T12:23:49.210Z [WsiiP1H] admit id=cmd-2 type=teleport result=Rejected:unsupported-type:teleport
2026-11-25T12:23:49.210Z [WsiiP1H] admit id=cmd-3 type=noop result=Rejected:stale-revision:0
2026-11-25T12:23:49.212Z [WsiiP1H] admit id=cmd-4 type=noop result=Rejected:past-tick:-5<0
2026-11-25T12:23:49.212Z [WsiiP1H] admit id=cmd-5 type=transfer result=Accepted:ok
2026-11-25T12:23:49.213Z [WsiiP1H] admit id= type=noop result=Rejected:missing-id
2026-11-25T12:23:49.213Z [WsiiP1H] fixture source=embedded:Wsii.P1H.commands.txt accepted=2 replayed=1 rejected=5 lines=8
2026-11-25T12:23:49.216Z [WsiiP1H] system-registered P1HProbeSystem phase=GameSimulation
2026-11-25T12:23:49.216Z [WsiiP1H] OnLoad exit
```

Every verdict matches the offline checks in `tests/Wsii.P1H.Semantics` exactly: 8 lines,
2 accepted, 1 replayed, 5 rejected, same reasons, same order. `UpdateAt<P1HProbeSystem>` was
accepted by the real `UpdateSystem`, so the registration path is real, not merely compilable.

## Defects the real run found that inspection did not

The first in-game run (build 10,240 bytes) failed in two ways while every static check passed:

1. `typeof(Mod).Assembly.Location` is **empty** for game-loaded mods, so `mod-dir` resolved to the
   game install root and the fixture lookup missed: `fixture-missing ...\Cities Skylines II\fixtures\commands.txt`,
   `accepted=0 ... lines=0`. Admission therefore never ran in game.
2. Because the log path was derived from the same bad directory, `p1h.log` was never written
   beside the mod, and the write into `Program Files` failed silently.

Fix in build 11,776 bytes, verified by the run above:

- `ResolveModDirectory()` trusts `Assembly.Location` only when that directory actually contains
  `mod.json`, otherwise it uses `Application.persistentDataPath\Mods\WsiiP1H`.
- The fixture is embedded (`Wsii.P1H.commands.txt`) and read from the manifest resource first, so
  delivery cannot break on file layout. The log now names its own source: `source=embedded:...`.

Both defects were invisible to the offline suite and to the assembly checks. That is the concrete
reason the real run was required, and the reason S1-5 is reported from a log rather than a review.

## Why S1-6 is still open

A heartbeat requires a city to simulate. Reaching a city means clicking through the main menu into
a save, which is GUI automation of the user's own game session; it was not done. The system is
registered and will tick as soon as a city loads.

## Next slice

Replace the embedded fixture with a command source the game owns: one `ISerializable` component
holding pending envelopes and the applied-id set, fed from `P1HProbeSystem`, so admission and
save/reload parity are exercised together. That is the AC-17 and AC-20 boundary from
`wsii-enhancement/docs/GAP_MATRIX.md`.
