# War-Skylines-II

War Skylines II builds for Cities: Skylines II. Verified development build 0.1.1.47.

This repository is the **P1-H binding lane**: the companion mod that carries the P1-R economic
semantics (proved offline in `wsii-enhancement/p1r`) into the real game runtime.

## What exists here

| Path | Purpose |
|---|---|
| `src/Wsii.P1H/Mod.cs` | `Game.Modding.IMod` entry point, verified against the installed `Game.dll` |
| `src/Wsii.P1H/P1HProbeSystem.cs` | `GameSystemBase` registered at `SystemUpdatePhase.GameSimulation` |
| `src/Wsii.P1H/CommandLedger.cs` | Game-free command admission: idempotent replay, payload conflict, stale revision, past tick |
| `src/Wsii.P1H/fixtures/commands.txt` | Shared fixture, replayed identically by the mod and by the offline checks |
| `tests/Wsii.P1H.Semantics/` | Zero-dependency console checks over that same fixture |
| `scripts/check.ps1` | One gate: build, semantics, artifact checks, deploy freshness. `-Deploy` also installs |
| `scripts/selftest-gate.ps1` | Proves the gate can fail: stale install, missing game ABI, foreign assembly, corrupted binary |
| `scripts/compare-log.ps1` | Diff the game's admission log against the offline verdicts |
| `scripts/verify-assembly.ps1` | Reflection-only assertion that the built DLL matches the game API |
| `scripts/deploy.ps1` | Copy build output into the local `Mods\WsiiP1H` folder |
| `docs/INTEGRATION_MAP.md` | The verified integration surface, with evidence per claim |
| `docs/SLICE_1.md` | Slice 1 acceptance criteria and current result |

## Build

```powershell
dotnet build src\Wsii.P1H\Wsii.P1H.csproj -c Release
```

Targets `net48`, referencing `Game.dll` and friends from
`C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II\Cities2_Data\Managed`.
Override with `-p:GameManaged=<path>` on another install.

## Verify

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check.ps1            # gate, no install
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check.ps1 -Deploy    # gate, then install
```

The gate runs build, semantics, artifact checks against both the build output and the installed
copy, and reports `deploy-in-sync` or `STALE-DEPLOY`. Builds are deterministic, so that comparison
is meaningful.

This lane does **not** open pull requests against `realbalakona/War-Skylines-II`. Work stays in the
`KooshaPari` fork.

Observed on 2026-11-25:

- build: 0 warnings, 0 errors
- semantics checks: 28 checks (14 fixture verdicts + 14 edge cases), `PASS 8 fixture lines, 2 accepted, 1 replayed, 5 rejected`
- assembly checks: 8 checks, `PASS mod assembly is loadable and API-correct`
- deploy: `WsiiP1H.dll`, `mod.json`, `fixtures\commands.txt` copied to `Mods\WsiiP1H`, SHA-256 parity asserted
- **in game**: `Cities2.exe` loaded the mod and wrote `Mods\WsiiP1H\p1h.log` with
  `OnLoad enter`, all eight admissions matching the offline results, `fixture source=embedded:...`,
  both systems registered, `OnLoad exit`. Quoted in `docs/SLICE_1.md`.
- **in game, ticking**: `heartbeat phase=LateUpdate ticks=600` after ~10.5 minutes at the main menu,
  so the game's update loop really does call our `OnUpdate`. Quoted in `docs/SLICE_1.md`.

## Not yet verified

The `GameSimulation` phase itself. Both probes register and one is proven to tick, but
`heartbeat phase=GameSimulation` needs a city simulating, which means loading a save through the
game menu.

The first in-game run also proved the offline suite insufficient on its own: it exposed an empty
`Assembly.Location` that broke fixture and log resolution while all static checks passed. Both
defects and their fixes are recorded in `docs/SLICE_1.md`.
