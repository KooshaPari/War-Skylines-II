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
dotnet run --project tests\Wsii.P1H.Semantics -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assembly.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\deploy.ps1
```

Observed on 2026-11-25:

- build: 0 warnings, 0 errors
- semantics checks: 14 checks, `PASS 8 fixture lines, 2 accepted, 1 replayed, 5 rejected`
- assembly checks: 6 checks, `PASS mod assembly is loadable and API-correct`
- deploy: `WsiiP1H.dll`, `mod.json`, `fixtures\commands.txt` copied to `Mods\WsiiP1H`

## Not yet verified

In-game execution. The assembly builds and matches the API, and it is deployed, but it has not
been loaded by a running game. Activation of a local folder mod here is owned by Skyve, and a
running `Cities2.exe` session (PID 44764) had to be left alone. See `docs/SLICE_1.md`.
