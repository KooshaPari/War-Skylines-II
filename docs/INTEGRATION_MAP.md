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
| `P1HProbeSystem` derives from `Game.GameSystemBase` | ok |
| assembly references `Game` | ok |

`PASS mod assembly is loadable and API-correct`

## 4. Deployment seam

`scripts\deploy.ps1` copies `WsiiP1H.dll`, `mod.json` and `fixtures\commands.txt` into
`Mods\WsiiP1H\`. Deployed on 2026-11-25; the manifest shape matches the shipped
`.CivicSurvival\mod.json` exactly:

```json
{ "id": "WsiiP1H", "author": "KooshaPari", "version": "0.1.0", "dependencies": [] }
```

## 5. Activation seam, and why it is not automated

`content_load.json` lists enabled content as absolute `path` entries. The playset files under
`playsets_backup\*.json` store `{displayName, enabled, position, pdxId}` and, across all four
populated playsets, contain **zero** entries without a `pdxId`. Local folder mods such as
`WarSkylinesII` and `.CivicSurvival` therefore are not activated through the playset file: Skyve,
which is installed here, owns the local-mod list (`ModsData\Skyve\ModDllCache.json` contains
`WarSkylinesII`).

Editing either surface is not a safe automated step:

- the game was running with a live session, and the launcher rewrites `content_load.json`;
- Skyve's cache is the user's mod-manager state, not ours to rewrite.

So activation is a single explicit toggle in Skyve or the launcher, followed by a game restart.
That restart is the only remaining step between this build and in-game evidence.

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
