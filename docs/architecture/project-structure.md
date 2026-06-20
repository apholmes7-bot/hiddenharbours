# Project Structure & Repository Layout

> How the Unity project and this repo are physically organised, and the one-time setup
> that makes parallel multi-agent work safe. Read this before creating the Unity project.

## 1. The repo root *is* the Unity project root

This repository root holds both the Unity project (`Assets/`, `Packages/`, `ProjectSettings/`)
and the design/process docs (`docs/`, `agents/`, `backlog/`, `.github/`). Unity ignores the
non-Unity folders. Do **not** nest the Unity project in a subfolder — keeping it at root lets
`.gitignore`, `.gitattributes`, and Git LFS apply cleanly.

```
Hidden Harbours/                 ← repo root = Unity project root
├── Assets/                      ← Unity content (see §2)
├── Packages/                    ← Unity package manifest (committed)
├── ProjectSettings/             ← Unity project settings (committed)
├── docs/                        ← design + architecture docs
├── agents/                      ← multi-agent operating system (roles, coordination)
├── backlog/                     ← roadmap work items
├── .github/                     ← PR template, CI later
├── CLAUDE.md                    ← master instructions for AI agents
├── README.md
├── .gitignore  .gitattributes  .editorconfig
```

`Library/`, `Temp/`, `Logs/`, `obj/`, `Build/` are **generated** and git-ignored — never commit them.

## 2. `Assets/` layout (feature-first)

Organise by **feature/module**, not by type. Everything the team authors lives under
`Assets/_Project/` (the leading underscore keeps it sorted to the top, above imported assets).

```
Assets/
├── _Project/
│   ├── Code/
│   │   ├── Core/            → asmdef: HiddenHarbours.Core        (services, events, save, util, contracts)
│   │   ├── Environment/     → asmdef: HiddenHarbours.Environment (clock, tide, weather, EnvironmentService)
│   │   ├── Boats/           → asmdef: HiddenHarbours.Boats       (hull/engine/hold/gear, physics, nav)
│   │   ├── Fishing/         → asmdef: HiddenHarbours.Fishing     (gear, catch resolution, fishing UI hooks)
│   │   ├── Economy/         → asmdef: HiddenHarbours.Economy     (market, business, staff, production, logistics)
│   │   ├── World/           → asmdef: HiddenHarbours.World       (regions, scene flow, NPC, routines, quests)
│   │   ├── Player/          → asmdef: HiddenHarbours.Player      (player controller, inventory, stamina)
│   │   ├── UI/              → asmdef: HiddenHarbours.UI          (HUD, menus, market/management screens)
│   │   └── Audio/           → asmdef: HiddenHarbours.Audio       (audio director, adaptive music/sfx)
│   ├── Data/               → ScriptableObject assets (one file per entity — see §4)
│   │   ├── Fish/  Boats/  Regions/  NPCs/  Commodities/  Recipes/  Gear/  Bait/  Staff/  Config/
│   ├── Art/                → Sprites/ Tilesets/ Characters/ Boats/ UI/ VFX/   (LFS-tracked)
│   ├── Audio/              → Music/ SFX/ Ambient/                              (LFS-tracked)
│   ├── Scenes/            → Bootstrap.unity + one scene per region (see §3)
│   ├── Prefabs/           → Boats/ NPCs/ UI/ Props/ Systems/
│   └── Settings/          → URP assets, Input Actions, build profiles
├── ThirdParty/            → Asset Store / external packages (kept separate, may be LFS)
└── Tests/
    ├── EditMode/          → asmdef: HiddenHarbours.Tests.EditMode (logic, determinism)
    └── PlayMode/          → asmdef: HiddenHarbours.Tests.PlayMode (integration)
```

Editor-only tools live in `Assets/_Project/Code/<Module>/Editor/` with their own
`HiddenHarbours.<Module>.Editor` asmdef (Editor platform only). General tooling →
`HiddenHarbours.Tools.Editor`.

## 3. Scene strategy (why this prevents merge pain)

- **`Bootstrap.unity`** is the only scene in the build list at index 0. It spins up the
  persistent services (Core/Environment/Economy/Save/Input/Audio) and then additively loads the
  starting region. Persistent systems live here as a `[DontDestroyOnLoad]` root.
- **One scene per region** (`CoddleCove.unity`, `PortGreywick.unity`, `TheSunkers.unity`, …),
  loaded **additively**. Two agents working on two different regions touch two different scene
  files → **no merge conflict**. This is the single most important reason we split scenes.
- **Prefab-first authoring.** Build content as prefabs and drop prefab *instances* in scenes.
  Editing the prefab doesn't dirty the scene, so scene `.unity` files stay small and stable.
  See `adr/0004-perspective-and-scene-strategy.md`.

## 4. Data assets: one entity per file

Every piece of game content (a fish, a boat, a region, an NPC, a recipe) is **one
ScriptableObject asset in its own file** under `Assets/_Project/Data/...`. Adding the 100th fish
is a *new file*, never an edit to a shared one — so content agents can add fish, boats, and
recipes in parallel with zero conflicts. See `architecture/data-model.md` and
`adr/0003-data-driven-content.md`.

## 5. Assembly definitions (asmdefs) — the dependency rules

asmdefs do three jobs at once: speed up compiles, **enforce architecture**, and reduce merge
conflicts. The dependency graph flows **one direction** — nothing depends on a feature module's
internals:

```
                 ┌─────────────────────────┐
   everything →  │  HiddenHarbours.Core     │  (no deps; defines shared contracts/interfaces,
                 └─────────────────────────┘   the EventBus, SaveService, EnvironmentSample, IDs)
                              ▲
        ┌──────────┬──────────┼──────────┬──────────┬──────────┐
   Environment   Boats     Fishing    Economy     World      Audio
        ▲          ▲          ▲           ▲          ▲
        └──────────┴────►  Player  ◄──────┴──────────┘
                              ▲
                             UI   (depends on the modules whose data it displays, via Core contracts)
```

Rules:
- A feature module may depend on **Core** and on **published contracts** (interfaces/data in Core),
  never on another feature module's concrete classes. Cross-module communication goes through
  **Core interfaces** or the **EventBus**.
- If module A needs something from module B, that something gets promoted to a **contract in Core**.
- `UI` and `Player` are allowed to reference multiple modules (they're composition layers), but
  still prefer Core contracts + events.
- Tests reference the modules they test.

When a PR adds a cross-module dependency that isn't through Core, `lead-architect` reviews it.

## 6. One-time machine setup (do this once, in order)

```bash
# 1. Install Git LFS (once per machine)
git lfs install

# 2. Configure Unity's YAML smart-merge tool in your GLOBAL git config.
#    Replace <UnityPath> with your editor install. Example (macOS):
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver \
  '"/Applications/Unity/Hub/Editor/6000.3.0f1/Unity.app/Contents/Tools/UnityYAMLMerge" merge -p %O %B %A %A'
#    (Windows path is typically:
#     "C:/Program Files/Unity/Hub/Editor/6000.3.0f1/Editor/Data/Tools/UnityYAMLMerge.exe")
```

Then **inside Unity** → `Edit > Project Settings`:
- `Editor > Asset Serialization > Mode = Force Text` (scenes/prefabs become diffable YAML).
- `Editor > Version Control > Mode = Visible Meta Files`.
- `Editor > Enter Play Mode Settings` → consider disabling domain reload for faster iteration (optional).

The `.gitattributes` in this repo already routes `*.unity`/`*.prefab`/`*.asset` through
`unityyamlmerge` and stores binaries in LFS — but the two steps above must be done per machine or
those rules can't work.

## 7. Naming conventions

| Thing | Convention | Example |
|-------|-----------|---------|
| Namespaces | `HiddenHarbours.<Module>` | `HiddenHarbours.Boats` |
| C# types/methods/properties | PascalCase | `EnvironmentService`, `GetTideHeight()` |
| Private fields | `_camelCase` | `_tideHeight` |
| Locals/params | camelCase | `seaState` |
| Constants | PascalCase or ALL_CAPS for true consts | `MaxCrew`, `SECONDS_PER_DAY` |
| Data asset files | `PascalCase` entity name | `AtlanticCod.asset`, `TheDory.asset` |
| Scenes | PascalCase region | `CoddleCove.unity` |
| Prefabs | PascalCase | `Dory.prefab`, `NpcTownsfolk.prefab` |
| Branches | `type/short-desc` | `feat/dory-controller`, `fix/tide-drift` |

## 8. Open questions
- Addressables vs Resources vs direct refs for loading the (eventually large) `Data/` catalog — lean Addressables once content scales; fine to start simple in M0. (`lead-architect` owns this call.)
- Whether `ThirdParty/` assets go in LFS or are re-imported per machine.
