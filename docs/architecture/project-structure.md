# Project Structure & Repository Layout

> How the Unity project and this repo are physically organised, and the one-time setup
> that makes parallel multi-agent work safe. **Reviewed against the real tree on 2026-09-01**
> (lead-architect): §2, §3, §5, §6 and §8 were corrected to what actually exists; the drift is
> recorded in §9 so nobody re-derives it.

## 1. The repo root *is* the Unity project root

This repository root holds both the Unity project (`Assets/`, `Packages/`, `ProjectSettings/`)
and the design/process docs (`docs/`, `agents/`, `backlog/`, `.github/`). Unity ignores the
non-Unity folders. Do **not** nest the Unity project in a subfolder — keeping it at root lets
`.gitignore`, `.gitattributes`, and Git LFS apply cleanly.

```
Hidden Harbours/                 <- repo root = Unity project root
+-- Assets/                      <- Unity content (see section 2)
+-- Packages/                    <- Unity package manifest (committed): URP 17.5, Input System 1.19, the 2D suite
+-- ProjectSettings/             <- Unity project settings (committed): editor pinned at 6000.5.0f1
+-- docs/                        <- design + architecture docs, ADRs (docs/adr/README.md is the index), art rigs
+-- agents/                      <- multi-agent operating system (roles, coordination)
+-- backlog/                     <- roadmap work items (backlog.md; plan-to-m1.md is the current M1 audit)
+-- .github/                     <- PR template, CI (EditMode + PlayMode on every PR; no GPU there)
+-- CLAUDE.md                    <- master instructions for AI agents
+-- HANDOFF-<date>-<lane>.md     <- paste-ready lane charters (UNTRACKED by convention; the coordinator writes them)
+-- README.md   SETUP-UNITY.md   <- SETUP-UNITY describes the original June scaffold; historical
+-- .gitignore  .gitattributes  .editorconfig
```

`Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, `artifacts/` are **generated** and git-ignored —
never commit them. Rig bakes and eyeball plates that must be reviewed go under `docs/art/spikes/`
(LFS), not `artifacts/`.

## 2. `Assets/` layout (feature-first) — as it is

Organise by **feature/module**, not by type. Everything the team authors lives under
`Assets/_Project/` (the leading underscore keeps it sorted to the top, above imported assets).

```
Assets/
+-- _Project/
|   +-- Code/                      (one asmdef per module; the dependency rules are section 5)
|   |   +-- Core/                  HiddenHarbours.Core          no deps: contracts, EventBus, save, GameConfig,
|   |   |                                                       the deterministic maths (WaveMath, BreakerMath, TidalExposure...),
|   |   |                                                       the Core seams every module reads (SharedWaveField, DisplacedSea...)
|   |   +-- Environment/           HiddenHarbours.Environment   clock, tide, weather, EnvironmentService
|   |   +-- Boats/                 HiddenHarbours.Boats         hulls, force model, seakeeping, wake, interiors runtime, lamps
|   |   +-- Vehicles/              HiddenHarbours.Vehicles      road vehicles: a different thing from a hull (ADR 0035)
|   |   +-- Fishing/               HiddenHarbours.Fishing       gear, catch resolution, clam dig, traps (references Economy: section 5)
|   |   +-- Economy/               HiddenHarbours.Economy       market, buyers, shops, licences, freshness, business
|   |   +-- World/                 HiddenHarbours.World         regions, scene flow, NPCs, routines, quests, dialogue
|   |   +-- Player/                HiddenHarbours.Player        on-foot controller, inventory, deck rider (composition layer)
|   |   +-- UI/ (+ Editor/)        HiddenHarbours.UI            HUD, notebook, menus, diegetic instruments
|   |   +-- Audio/                 HiddenHarbours.Audio         audio director, procedural audio
|   |   +-- Art/ (+ Editor/)       HiddenHarbours.Art           RENDERING plumbing: water, lighting, foam, reflections,
|   |   |                                                       facet hulls, sprite paths, emitters (co-owned: coordination 1.1)
|   |   +-- App/ (+ Editor/)       HiddenHarbours.App           the composition root (GameRoot), shell flow, dev bootstraps;
|   |   |                                                       App.Editor = the scene BUILDERS (StPeters, NineMileCreek, WestWater...)
|   |   +-- Tools/Editor/          HiddenHarbours.Tools.Editor  authoring tools; sub-assemblies JsEngine.Editor (embedded V8),
|   |   |                                                       RigBaking.Editor (the in-engine rig bakers, ADR 0021/0022),
|   |   |                                                       RigStudio.Editor, SpikeDeckCharacterMesh.Editor
|   |   +-- Spike/DeckCharacterMesh/  HiddenHarbours.SpikeDeckCharacterMesh: ADR 0024's spike assembly, still shipped (section 9)
|   +-- Data/                      ScriptableObject assets, one entity per file (section 4). Folders as of 2026-09-01:
|   |                              Art, Bites, Boats (+ Containers, DeckGear, Decks, Helms, HullMeshes, HullProps, Interiors,
|   |                              Owners, PaintSchemes, Skippers, Visuals), Characters, Commodities, Config (GameConfig),
|   |                              Decor, Fish, FuelContainers, FuelStations, Homes, NPCs, NavBuoys, Recipes, Regions,
|   |                              Resources (quest/knowledge defs the notebook self-loads), RodFights, Routines, Spike,
|   |                              Staff, StationPieces, Tackle, Terrain (painted seabed), Tools, Traps, Vehicles
|   +-- Art/                       Boats, Characters, Fishing, Foliage, Materials, Palette, Portraits, Shaders,
|   |                              Sprites, Terrain, Textures, Tilesets, UI, VFX, Editor   (LFS-tracked binaries)
|   +-- Audio/                     (LFS-tracked)
|   +-- Plugins/                   vendored native/managed dependencies (the ClearScript/V8 host lives here)
|   +-- Prefabs/                   Boats, Decor, FuelStorage, GasStation, NPCs, Props, Systems, UI
|   +-- Resources/                 player-global, self-installing assets (boat interior cells, wake sprite library...): section 8
|   +-- Scenes/                    StPeters (build index 0), NineMileCreek, WestWater, Greybox (the M0 cove), Greywick (legacy: section 3)
|   +-- Settings/                  URP assets, render features
+-- Scenes/SampleScene.unity       Unity template debris, still in the build list: remove (section 9)
+-- Settings/, InputSystem_Actions.inputactions, DefaultVolumeProfile, URP global settings
+-- Tests/
    +-- EditMode/                  HiddenHarbours.Tests.EditMode (the broad suite) plus per-area assemblies:
    |                              Tests.Art / Audio / Economy / RigBaking / RigSpike / RigStudio / Sell /
    |                              SpikeDeckCharacterMesh / UI / World .EditMode
    +-- PlayMode/                  HiddenHarbours.Tests.PlayMode (integration; region scenes, journeys)
```

Editor-only code lives in `Assets/_Project/Code/<Module>/Editor/` with its own
`HiddenHarbours.<Module>.Editor` asmdef (Editor platform only) — `App.Editor`, `Art.Editor`,
`UI.Editor` exist. General tooling → `HiddenHarbours.Tools.Editor` and its sub-assemblies.
There is no `Assets/ThirdParty/`; external code is vendored under `Assets/_Project/Plugins/`.

## 3. Scene strategy (why this prevents merge pain) — and how boot really works

- **There is no `Bootstrap.unity`.** The persistent core — the services root (`GameRoot`,
  `GameClock`, `EnvironmentService`, `PlayerWallet`, the glanceable HUD), the on-foot player, the
  dory, the follow camera, the `ControlSwitcher` and the travel rig — is **baked into each start
  scene** by `PersistentCoreBuilder` (`App/Editor`), every piece tagged `PersistentObject`
  (`DontDestroyOnLoad`), so every start scene boots the identical core and it cannot diverge
  between scenes. `GameRoot` is the composition root (`tech-architecture.md` §2).
  **`StPeters.unity` is build index 0** — the game opens there.
- **One scene per region**, loaded **additively**: `StPeters.unity`, `NineMileCreek.unity`,
  `WestWater.unity` (the bay's first open-water scene). A region reached by travel receives the
  core from the start scene; each carries an inactive `DevRegionBootstrap` dev core so the owner
  can press Play in it directly. Two agents on two regions touch two different scene files →
  **no merge conflict**. `CoddleCove` has a region def and, as of this review, no scene (it is
  M2's home harbour). `Greybox.unity` is the M0 cove greybox. `Greywick.unity` is the pre-rename
  town scene (D1: Greywick *is* Nine Mile Creek) — no region def references it; it is legacy and
  in the build list (§9).
- **Committed hand-authored scenes are the source of truth** (ADR 0011 / 0019): builders CREATE a
  scene's logic once and REFRESH it; the owner paints and places in the editor; his rebuilt
  scenes are banked in `chore(scenes)` PRs. Scene files are large (a Nine Mile Creek rebuild is
  a ~250k-line diff), which is why builders own logic and prefabs own content.
- **Prefab-first authoring.** Build content as prefabs and drop prefab *instances* in scenes.
  See `adr/0004-perspective-and-scene-strategy.md`.

## 4. Data assets: one entity per file

Every piece of game content (a fish, a boat, a region, an NPC, a recipe, a hull mesh, a vehicle)
is **one ScriptableObject asset in its own file** under `Assets/_Project/Data/...`. Adding the
100th fish is a *new file*, never an edit to a shared one — so content agents can add fish,
boats, and recipes in parallel with zero conflicts. See `architecture/data-model.md` and
`adr/0003-data-driven-content.md`. Two families are generated rather than hand-authored and are
committed by name after a bake: `Data/Boats/HullMeshes/` (ADR 0022) and `Data/Boats/Interiors/`
(ADR 0038) — never `git add -A` after a Unity run, because a run rewrites tracked boat assets.

## 5. Assembly definitions (asmdefs) — the dependency rules, and the real graph

asmdefs do three jobs at once: speed up compiles, **enforce architecture**, and reduce merge
conflicts. The dependency graph flows **one direction** — nothing depends on a feature module's
internals. As wired on 2026-09-01 (`references` in each `.asmdef`):

```
                          +---------------------------+
     everything  ------>  |   HiddenHarbours.Core     |  no deps: contracts, EventBus, save, config, the maths, the seams
                          +---------------------------+
                                        ^
   +----------+----------+----------+---+------+----------+----------+----------+
 Environment  Boats   Vehicles   Economy    World      Audio      UI        Art (+URP)
                                    ^
                                 Fishing  --> Economy   (recorded exception, below)

              Player  --> Boats, Economy, Art            (composition layer)
              App     --> Environment, Boats, Fishing, Economy, Player   (the composition root)
              Tools.*.Editor --> whatever they bake or inspect (editor-only; RigBaking reads Art, Boats, Vehicles, World)
```

Rules:
- A feature module may depend on **Core** and on **published contracts** (interfaces/data in Core),
  never on another feature module's concrete classes. Cross-module communication goes through
  **Core interfaces** or the **EventBus**. When module A needs something from module B, that
  something gets promoted to a **contract in Core** (the `DisplacedSea` / `SharedWaveField` /
  `SeaPalette` seams are the pattern).
- `Player`, `UI` and `App` are composition layers and may reference several modules; still prefer
  Core contracts + events. `Art` is rendering plumbing and depends on Core only — Boats cannot
  reference Art (which is why the water's `FreqScale` had to travel through a Core seam).
- Editor assemblies may read feature data to bake it; they never ship.
- Tests reference the modules they test.

**Recorded exception — `Fishing → Economy`** (since #65, 2026-06-23: the clam-dig licence and gear
gates). It predates the seam discipline and has been reviewed in place; the clean fix is a Core
seam for licences and gear offers (`ILicenseService`-shaped), logged for `lead-architect`. Do not
add a second feature-to-feature edge on its precedent.

When a PR adds a cross-module dependency that isn't through Core, `lead-architect` reviews it.

## 6. One-time machine setup (do this once, in order)

```bash
# 1. Install Git LFS (once per machine)
git lfs install

# 2. Configure Unity's YAML smart-merge tool in your GLOBAL git config.
#    The project is pinned at 6000.5.0f1 (ProjectSettings/ProjectVersion.txt). Windows:
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver \
  '"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'
#    (macOS path: "/Applications/Unity/Hub/Editor/6000.5.0f1/Unity.app/Contents/Tools/UnityYAMLMerge")
```

Then **inside Unity** → `Edit > Project Settings`:
- `Editor > Asset Serialization > Mode = Force Text` (scenes/prefabs become diffable YAML).
- `Editor > Version Control > Mode = Visible Meta Files`.
- `Editor > Enter Play Mode Settings` → consider disabling domain reload for faster iteration (optional).

The `.gitattributes` in this repo already routes `*.unity`/`*.prefab`/`*.asset` through
`unityyamlmerge` and stores binaries in LFS — but the two steps above must be done per machine or
those rules can't work. `docs/art/rigs/**/*.js` carries no `text` attribute: with
`core.autocrlf=true` a checkout gives CRLF while git stores LF, so two sha256 digests of "the same
rig" are both right (`tr -d '\r'` before comparing).

## 7. Naming conventions

| Thing | Convention | Example |
|-------|-----------|---------|
| Namespaces | `HiddenHarbours.<Module>` | `HiddenHarbours.Boats` |
| C# types/methods/properties | PascalCase | `EnvironmentService`, `GetTideHeight()` |
| Private fields | `_camelCase` | `_tideHeight` |
| Locals/params | camelCase | `seaState` |
| Constants | PascalCase or ALL_CAPS for true consts | `MaxCrew`, `SECONDS_PER_DAY` |
| Def ids | `type.snake_case`, append-only and stable | `fish.atlantic_cod`, `region.west_water` |
| Data asset files | `PascalCase` entity name | `AtlanticCod.asset`, `Dory.asset`, `CapeIslanderIsoHullMesh.asset` |
| Scenes | PascalCase region | `StPeters.unity`, `NineMileCreek.unity` |
| Prefabs | PascalCase | `Dory.prefab`, `NavBuoy.prefab` |
| Branches | `type/short-desc` | `feat/dory-controller`, `fix/tide-drift`, `docs/adr-0041-cape-rollout` |

## 8. Open questions (as of 2026-09-01)
- **Addressables vs `Resources`.** `Resources` is the mechanism in use and the honest one at this
  scale for player-global, self-installing assets (the notebook's kit and defs, boat interior cells,
  the wake sprite library) — see `tech-architecture.md` §10 for the reasoning. Addressables remain
  the migration target if the catalog outgrows it; nothing forces it yet. (`lead-architect` owns.)
- **Handoff files.** Lane charters are written to the repo root untracked (`HANDOFF-*.md`, 19 at
  this review). They are the project's operational history; committing them under `docs/handoffs/`
  would let cloud sessions read them. Owner's call — they carry machine paths.

## 9. Drift recorded at the 2026-09-01 review (fix or decide; do not re-derive)
- `Assets/Scenes/SampleScene.unity` (the Unity template scene) is in `EditorBuildSettings` at
  index 2. Remove it from the build list and delete the file — a one-line owner action in the editor.
- `Greywick.unity` (pre-rename) is in the build list at index 3 with no region def behind it.
  Retire or keep as a reference — the owner's scene, the owner's call.
- `Spike/DeckCharacterMesh` is a spike assembly shipping in `Code/` (ADR 0024 ratified the idea).
  Promote what it owns into `Art`/`Boats` or retire it; `lead-architect` to audit.
- `Fishing → Economy` (§5) — the one feature-to-feature edge; promote the contract to Core.
- This document previously described a `Bootstrap.unity`, a mobile build target, `Assets/ThirdParty/`,
  `Data/Gear|Bait/` and a nine-module code layout, none of which exist as written. The fixes above
  are the record; the reason each drifted is that docs were not updated in the PRs that changed them
  (`agents/coordination.md` §1's last row is the rule — update docs in the same PR as the change).
