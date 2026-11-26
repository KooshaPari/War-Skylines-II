# Integration map: P1-H binding to Cities: Skylines II

Status: verified against the installed game on KOOSHAPARI-DESK, 2026-11-25. Every row below was
observed, not assumed. Re-verify if a game update replaces `Game.dll`.

## 1. Runtime target is present

| Fact | Evidence |
|---|---|
| Game install | `C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II` |
| Managed assemblies | `Cities2_Data\Managed\Game.dll` plus Unity/Colossal dependencies |
| Game running during authoring | `Cities2.exe` PID 44764, 8.1 GB working set |
| Installed mods folder | `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods` |
| Neighbouring mods | `WarSkylinesII`, `.CivicSurvival`, `.CitiesMenuObserver` |
| Mod manager present | `ModsSettings\Skyve`, `ModsData\Skyve\ModDllCache.json` |

## 2. The mod entry contract, read from `Game.dll`

Reflection-only load of `Game.dll` (no execution) produced the exact interface:

```
Game.Modding.IMod (interface)
  Void OnLoad(Game.UpdateSystem updateSystem)
  Void OnDispose()
```

Base chain of a simulatable system:

```
Game.GameSystemBase                     [Game.dll]
  <- Colossal.Entities.COSystemBase     [Colossal.Core]
  <- Unity.Entities.SystemBase          [Unity.Entities]
  <- Unity.Entities.ComponentSystemBase [Unity.Entities]
```

`OnUpdate()` is `abstract` and `protected`, declared on `Unity.Entities.SystemBase`.
`Game.UpdateSystem` exposes `UpdateAt(SystemUpdatePhase)`, `UpdateBefore`, `UpdateAfter`,
`Update(ComponentSystemBase, uint, int)` and `RegisterGPUSystem`.

Assembly metadata of the shipped War Skylines II mod (`WarSkylinesII.dll`):

```
TargetFramework  .NETFramework,Version=v4.8
AssemblyVersion  0.1.1.50
Configuration    Release
```

Consequence: a companion mod must target `net48`. That is what this repository builds.

## 3. What the build now proves

`src/Wsii.P1H` compiles to `artifacts\Release\WsiiP1H.dll` with 0 warnings and 0 errors against
the real `Game.dll`, `Colossal.Core.dll`, `Unity.Entities.dll` and `UnityEngine.CoreModule.dll`.

`scripts\verify-assembly.ps1` then asserts, from the built artifact:

| Check | Result |
|---|---|
| target-framework `.NETFramework,Version=v4.8` | ok |
| `Game.Modding.IMod` resolves in `Game.dll` | ok |
| entry type `Wsii.P1H.Mod` present | ok |
| `Mod` implements `Game.Modding.IMod` | ok |
| `P1HProbeSystem` and `P1HMenuProbeSystem` derive from `Game.GameSystemBase` | ok |
| assembly references `Game` | ok |
| embedded fixture `Wsii.P1H.commands.txt` present | ok |

`PASS mod assembly is loadable and API-correct`

That is a contract check, not acceptance. Acceptance came from running the game; see
`SLICE_1.md` for the log the deployed mod wrote inside `Cities2.exe`.

## 4. Deployment seam

`scripts\deploy.ps1` copies `WsiiP1H.dll`, `mod.json` and `fixtures\commands.txt` into
`Mods\WsiiP1H\`. Deployed on 2026-11-25; the manifest shape matches the shipped
`.CivicSurvival\mod.json` exactly:

```json
{ "id": "WsiiP1H", "author": "KooshaPari", "version": "0.1.0", "dependencies": [] }
```

## 5. Activation seam, verified

`content_load.json` holds `enabledMods` as absolute `path` entries. Adding one entry for a local
folder mod is sufficient:

```json
{ "path": "C:\\Users\\koosh\\AppData\\LocalLow\\Colossal Order\\Cities Skylines II\\Mods\\WsiiP1H" }
```

Verified: after that single insertion and a game start, the mod's `OnLoad` ran inside `Cities2.exe`
(see `SLICE_1.md`). No launcher toggle was needed, and the playset files were not touched.

Two corrections to earlier assumptions, both established by running the game:

- The playset files under `playsets_backup\*.json` store `{displayName, enabled, position, pdxId}`
  and contain **zero** entries without a `pdxId`, which is why local folder mods never appear
  there. They are not the enablement surface for local mods; `content_load.json` is.
- The Paradox Launcher was **not** required. Launching `Cities2.exe` directly loaded the enabled
  mod set, including ours.

Caution when editing this file: a literal PowerShell replace through `cmd.exe` produced doubled
backslashes and a path the game would not match. Validate after writing by parsing the JSON and
asserting `Test-Path $entry.path`. A backup was kept at `content_load.json.bak-p1h`.

## 5a. Path resolution inside a game-loaded mod

`typeof(Mod).Assembly.Location` is **empty** when the game loads a managed mod from its own loader.
Any path derived from it silently becomes the install root, which is read-only and wrong. The
first in-game run failed exactly this way: the fixture was not found and the log could not be
written. The mod now uses `Application.persistentDataPath\Mods\WsiiP1H` unless the assembly
location demonstrably contains `mod.json`, ships its fixture as an embedded resource, and logs the
resolved directory and log path so the log proves which branch ran.

## 6. Where P1-R semantics land in game

| P1-R concept | P1-H landing point |
|---|---|
| Command envelope + idempotency | `Wsii.P1H.CommandLedger`, admitted inside `OnLoad`, later fed from the simulation tick |
| Tick clock | `P1HProbeSystem.OnUpdate`, registered at `SystemUpdatePhase.GameSimulation` |
| Persistent state | WSII already ships ~35 `ISerializable` components (AC-16 present); P1 commands need one owned component, not a new save system |
| Proof surface | `p1h.log` beside the mod, plus `Player.log` via `UnityEngine.Debug` |

## 7. Risks carried forward

- **Decompile-only upstream.** WSII source is unavailable; the extension API is the only seam, so
  everything here is a companion mod, never a patch of shipped IL.
- **Licensing.** No decompiled code or derived assets are copied into this repository.
- **Game-version drift.** `TargetFramework v4.8` and the `IMod` shape were read from this install;
  a future patch can move them, and `verify-assembly.ps1` is the tripwire that will notice.
