# Slice 1: load, admit, tick

Scope: the smallest end-to-end chain that can be observed in the real game.

## Acceptance criteria

| # | Criterion | How it is observed | Result |
|---|---|---|---|
| S1-1 | The mod compiles against the installed `Game.dll` | `dotnet build` exit 0, 0 warnings, 0 errors | PASS |
| S1-2 | The built assembly implements `Game.Modding.IMod` and derives the system from `Game.GameSystemBase` | `scripts/verify-assembly.ps1`, 6 checks | PASS |
| S1-3 | Command admission is correct and idempotent | `tests/Wsii.P1H.Semantics`, 14 checks over the shared fixture | PASS |
| S1-4 | The mod is installed in the game's mods folder with a valid manifest | `scripts/deploy.ps1` output | PASS |
| S1-5 | `OnLoad` writes `p1h.log` in game | `Mods\WsiiP1H\p1h.log` after a game start | **NOT_RUN** |
| S1-6 | `P1HProbeSystem` ticks in simulation | `heartbeat` lines in `p1h.log` after a city loads | **NOT_RUN** |

Observed on 2026-11-25: S1-1..S1-4 pass; S1-5 and S1-6 require the game to load this mod.

## Why S1-5 and S1-6 are not yet run

`Cities2.exe` was running (PID 44764) with the user's live session. Loading a newly deployed mod
requires both activation and a restart, and activation here is owned by Skyve rather than a file
we can safely rewrite. Verified alternative: the assembly-level checks in S1-2 cover exactly the
surface a load would exercise first (interface identity, type resolution, base class, reference
set), so a failure at load would most likely be a game-version change rather than a code defect.

## To close the slice

1. Open Skyve (or the launcher's mod list) and enable `WsiiP1H`.
2. Start Cities: Skylines II.
3. `Mods\WsiiP1H\p1h.log` should contain, in order:
   - `OnLoad enter`
   - `mod-dir ...\Mods\WsiiP1H`
   - two `admit` lines with `result=Accepted:ok`, one `result=Replayed:duplicate-id`,
     five `Rejected:...` lines
   - `fixture admitted accepted=2 replayed=1 rejected=5 lines=8`
   - `system-registered P1HProbeSystem phase=GameSimulation`
   - `OnLoad exit`
4. After a city simulates, `heartbeat ticks=256`, `512`, ... appear.

If `OnLoad-ERROR` appears instead, the message names the failing call and the slice stops there.

## Next slice after this

Replace the fixture replay with a command source the game owns: one `ISerializable` component
holding pending envelopes and the applied-id set, fed from the simulation tick, so admission and
save/reload parity are exercised together. That is the AC-17 and AC-20 boundary from
`wsii-enhancement/docs/GAP_MATRIX.md`.
