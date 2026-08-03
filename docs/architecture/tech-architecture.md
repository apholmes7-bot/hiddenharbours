# Technical Architecture

> The systems backbone. How the game boots, how services talk, how content is data-driven,
> how the world stays deterministic enough to save reliably, and how it stays fast on a phone.
> Companion docs: `architecture/project-structure.md`, `architecture/data-model.md`, the ADRs.

## 1. Guiding principles

1. **Data-driven content.** Fish, boats, regions, NPCs, recipes are *data* (ScriptableObjects),
   not code. Agents add content in parallel by adding assets. (`adr/0003`)
2. **Deterministic simulation.** Time, tide, weather, and the market are computed
   deterministically from `(seed, gameTime, fewStateValues)`. This means most of the world does
   **not** need saving — it can be *recomputed*. Saves stay small and robust. (P1 also benefits:
   the sea behaves lawfully, so players can learn it.)
3. **Decoupled modules talking through Core.** Feature modules never reach into each other; they
   communicate via Core interfaces and an EventBus. (`project-structure.md` §5)
4. **Composition over inheritance.** Boats, NPCs, and facilities are assembled from small
   components configured by data, not deep class trees.
5. **Mobile-first budgets.** Every system is written assuming a mid-range phone: tight draw
   calls, pooled objects, simulation throttled by distance/visibility.
6. **One perspective, one input abstraction.** Intent-based input so touch today maps to
   mouse/gamepad later without rewrites. (`design/ux-and-mobile-controls.md`)

## 2. Boot & lifetime

```
Bootstrap.unity (build index 0)
   └─ GameRoot  [DontDestroyOnLoad]
        ├─ Installs persistent services (composition root):
        │     EventBus, SaveService, TimeService, EnvironmentService,
        │     RegionService (scene loader), EconomyService, NpcDirector,
        │     InputService, AudioDirector, ContentDatabase
        ├─ Loads ContentDatabase (all ScriptableObject defs)
        ├─ Restores save (or starts new game at Uncle Ned's cottage)
        └─ Additively loads the active region scene
```

Use a lightweight **composition root** (a single installer that constructs services and injects
dependencies). A full DI framework (Zenject/VContainer) is optional — start with a simple manual
installer + a `ServiceLocator` exposed through Core interfaces. `lead-architect` owns this choice
(`adr` candidate).

## 3. Core services (the spine)

| Service | Responsibility | Notes / determinism |
|---------|----------------|---------------------|
| **EventBus** | Decoupled pub/sub between modules (`FishCaught`, `FishingStateChanged`, `TideChanged`, `BoatGrounded`, `DayStarted`, `MarketTick`). | Typed events; no module references another's classes. |
| **TimeService** | The 24h clock, day/week/season/year, time scale, sleep/wait. `gameTime` is a `double` (in-game seconds). | The master clock everything derives from. |
| **EnvironmentService** | Computes **tide, wind, weather, sea state, visibility** from `(worldSeed, gameTime, region)`. Emits an `EnvironmentSample` per region per tick. | **Deterministic** → not saved, recomputed. (`design/time-tides-weather.md`) |
| **RegionService** | Additive load/unload of region scenes, the `MapGraph`, travel/transit, fog-of-war reveal state. | Reveal state is saved; geometry is authored. |
| **ContentDatabase** | Loads and indexes all ScriptableObject defs; lookup by id. | Read-only at runtime. |
| **EconomyService** | Market supply/demand sim tick, buyers, contracts, business/production/staff simulation. | Market state is **saved** (it's path-dependent). (`design/economy-and-business.md`) |
| **NpcDirector** | Drives NPC routines/schedules against time/tide/weather; tiered simulation (active/nearby/dormant). | Positions recomputed on demand for dormant NPCs. (`design/npcs-and-routines.md`) |
| **InputService** | Translates raw input → **intents** (`MoveIntent`, `ThrottleIntent`, `InteractIntent`, `SetHeadingIntent`). | Platform-swappable. |
| **SaveService** | Versioned save/load, autosave, app-suspend safety. | See §6. |
| **AudioDirector** | Adaptive ambient/music/SFX driven by region + EnvironmentSample. | (`design/art-and-audio-bible.md`) |

## 4. The Environment → Boat force contract (P1, the signature loop)

`EnvironmentService` produces, each physics tick, a sample the boat physics consumes:

```csharp
public readonly struct EnvironmentSample {
    public readonly Vector2 WindVector;     // direction * strength (m/s)
    public readonly Vector2 CurrentVector;  // tidal current "set" (m/s)
    public readonly float   TideHeight;     // metres relative to chart datum
    public readonly SeaState SeaState;      // Glass … Storm
    public readonly float   Visibility;     // 0..1 (fog)
}
```

The boat reads its local sample and applies forces; this is what makes navigation a *skill*
(`design/boats-and-navigation.md`). Local **water depth = seabedHeight − tideHeight**; when a
boat's **draught > water depth → grounding** (ties tide to boats to regions in one clean number).

### 4.1 Tidal-exposure seam (on-foot walkability shares the grounding rule) — ADR 0009

The **same** water-level rule the boat uses for grounding also answers "is this spot submerged or
exposed at the current tide?" for the **on-foot** player — the falling-tide walkable seabed and the
St Peters tide-gated sandbar (`design/world-and-regions.md` §7, `design/time-tides-weather.md` §3.5).
Two additive Core pieces, both deterministic (recomputed from `(worldSeed, gameTime)`, never saved):

- **`IEnvironmentService.WaterLevelAt(double t)`** — the active region's deterministic water surface
  (m above datum). A **default interface method** returning `TideHeightAt(t)` (additive; existing
  implementers unchanged; overridable when a region offsets its water plane).
- **`Core.TidalExposure`** — pure helper: `WaterDepth(waterLevel, terrainElevation)`,
  `IsExposed(...)`, `IsSubmerged(...)`. The **one shared rule** the **world** (terrain authoring) and
  **gameplay** (walkability sim) both read, so the shoreline they draw and the seabed the player walks
  can never disagree. Built in the next wave; the seam is defined now.
- **`Core.ITidalTerrain` + `GameServices.TidalTerrain`** — the per-position **terrain-elevation source**
  (the "height map") that supplies the `terrainElevation` the helper above and the boat-grounding rule
  need. `ElevationAt(Vector2 worldPos)` returns authored ground height (m above datum, higher = drier),
  deterministic and unsaved. The **world** registers the active region's terrain via the optional,
  scene-scoped `GameServices.TidalTerrain` accessor (same pattern as `ActiveBoat`/`Licenses`); **gameplay**
  and the future **water depth-gradient shader** read it through Core, never referencing World. **Null =
  open water** (everywhere submerged / no walkable ground) — callers null-check rather than throw. Closes
  ADR 0009's "within-region elevation source" open question; world + gameplay can now build in parallel.
- **`Core.IFishSchools` + `Core.FishSchool`/`FishMark` + `GameServices.FishSchools`** — the **fish-school**
  seam (ADR 0025 S3): where "there are fish here" is asked and answered. A `FishSchool` is an *area*
  (centre + `RadiusMetres`) at a *depth in metres*, for a *while* (`[Start, End)` game seconds), carrying a
  `MarkCount` and a species-id set. **Gameplay** produces schools and reads `SchoolsAt` to raise the bite
  rate and weight the species roll; **UI** reads `MarksAt` to draw the sonar — one model, two readers, so
  the marks on the glass are literally the object that changes the fishing (the owner's honesty invariant).
  `MarkCount` is that invariant in one field: it is both how many fish the glass draws and the expected
  bite rate. Both calls fill a caller-owned list and allocate nothing (rule 7); depths cross the seam in
  **metres** and the presenter divides by the player's RANGE at paint time, so a normalised depth is never
  stored. Schools are **recomputed, never saved** (rule 5) — no DTO, no save field.
  **⚠ Unlike every other optional service on `GameServices`, this one is NEVER null**: absent a registered
  model it is `EmptyFishSchools`, an honest empty sea. That is deliberate — it is what lets the finder's UI
  and the fish model be built in parallel (the UI host draws an empty sonar with no model in the project,
  and the model swaps in with a single assignment), and it is the right shipped behaviour in a bare art
  scene, in EditMode, and in a region with no fish authored yet. Producers **clear the registration on
  disable** (assign null — the getter turns that back into the empty sea); the getter also checks Unity
  fake-null, so a destroyed MonoBehaviour producer degrades to the empty sea rather than throwing.
  **The producing model** (ADR 0025 S3a, gameplay-side) is `Fishing.FishSchoolModel` over
  `Fishing.FishSchoolMath`: a *pure function* of `(worldSeed, gameTime, place, weather, season)` — the
  world is diced into cells and time into slots, and each `(cell, slot)` is one hashed coin-flip gated by
  **location** (water deep enough), **weather** (sea state) and **date** (season), with everything else
  about the school drawn from the same key. No spawner, no `Update`, no timer, nothing saved. It is
  registered by `FishingController` (so schools exist whether or not any boat has a finder fitted) off the
  **same species array the catch resolver rolls from**, and the fishing path reads the registered seam —
  not its own instance — through `Fishing.SchoolInfluence`, which is what makes the honesty invariant
  structural rather than remembered. Tuning: `GameConfig.FishSchools`.
- **`IEnvironmentService.SeaState01At(double t)`** — the continuous sea state (0 glass .. 1 storm) at an
  **arbitrary** time; the weather twin of `TideHeightAt`, and additive in the same shape (a **default
  interface method** returning `Sample().SeaState01`, overridden by the real `EnvironmentService` with the
  pure `WeatherModel` evaluation). Needed by any consumer reasoning about a *span* rather than this
  instant: the fish-school sim decides a school from the weather at the moment it formed and then lets it
  stand for its whole window — reading "now" instead would make schools blink in and out as the wind
  wandered across a threshold, and the finder would faithfully draw the blinking.
- **`Core.IStandableSurface` + `Core.StandableSurfaces`** — the **standable-structure** seam: things
  BUILT (a wharf deck today; boat decks and washboards in M2) that a person stands *on*, whose standing
  height is their own rather than the seabed's. `TryGetDeckElevation(worldPos, out deck)` answers "am I
  over you, and how high is your deck" in **one** call — a query, not a `Rect` property, so a deck that
  MOVES and ROTATES is a later *implementation* rather than a contract change. Registrants add themselves
  to the static `StandableSurfaces` registry (many at once, so it mirrors `EventBus` rather than the
  single-slot `TidalTerrain` accessor) and relinquish on disable; **an empty registry is bit-identical to
  the pre-seam terrain-vs-water answer**. The whole rule is one substitution — `StandingElevation` picks
  the highest deck over a position, else the ground — feeding the existing `TidalExposure` maths, so
  `StandableSurfaces.OnFootDepth` is the **single composition** the walk gate, the sprint gate, the wade
  bands and the body's waterline all read. ⚠ **On-foot only:** never the water render, the
  boat-cross/grounding depth, the clam-baring or the seabed bake — a pier does not shoal the berth
  beneath it. Added because the St Peters wharf stands over a dredged −1.0 m slip in a tide-gated region,
  so the sim called the ratified disembark point 4.5 m of open sea at high water.

### 4.2 Region display-name seam (UI reads names without referencing World) — ADR 0009

**`Core.RegionDisplayNames`** — a tiny static registry mapping a scene name / region id → player-facing
display name ("Coddle Cove", "Nine Mile Creek"). The **world** (owner of `RegionDef`) registers at boot;
the **UI** (Core-only) reads `Resolve(key, fallback)` so the crossing fade card titles correctly
(closes the ui-ux #54 follow-up) without a UI→World reference. Presentation metadata: unsaved, no
determinism concern.

### 4.3 Icon seam (UI shows the sprite for an id without referencing the owning module)

**`Core.IconRegistry`** — the icon twin of `RegionDisplayNames`: a tiny static registry mapping a stable
content **id** (a fish/clam species id, a `gear.*`/`license.*`/`boat.*` id, or a `ui.*` glyph key) →
its **icon sprite**. It exists because the sell screen / catch card / HUD see a `CatchItem` (Core, which
caches only id/name/value so Boats/Economy depend on Core alone) and the UI assembly references only
Core — so the UI cannot reach a `FishSpeciesDef.Sprite` (Fishing) or a gear/boat offer sprite (Economy)
directly. An authored **`Core.IconLibrary`** asset (one `Resources/IconLibrary.asset`, id → sprite rows)
is published into the registry at boot by the self-installing **`Core.IconRegistrar`**
(`RuntimeInitializeOnLoadMethod`, mirroring `SaveService` — no scene/builder wiring). The UI resolves
icons by id via `IconRegistry.Get(id)`; a null result (none registered / EditMode) falls back to the
text-only read (icon is reinforcement, never the only channel — accessibility §8). The fish/clam defs
also carry their own `FishSpeciesDef.Sprite` (assigned) — the library is the single Core-readable place
that *also* gathers the gear/licence/boat/coin/hold icons the UI lane doesn't own a sprite field for.
Presentation metadata: unsaved, no determinism concern.

### 4.4 Fishing-state contract (UI/audio read the rod through Core) — Rod Fishing v2, Wave 1

**`Core.FishingPhase` + `Core.FishingState` + `Core.FishingStateChanged`** — the Fishing module
publishes a read-only snapshot of the live rod interaction on the EventBus each phase transition and
fight tick; UI (the transient rod gauge, later VS-14's HUD) and audio consume it through Core only,
never referencing Fishing. The contract's rules:

- **`FishingPhase` is append-only.** VS-13's members are frozen at ints 0–7 (`Idle, Waiting, Bite,
  Fighting, Tending, Landed, Snapped, NoBite`); Rod Fishing v2 appended 8–12 (`WindBack, Cast,
  Sinking, FightDeep, FightSurface` — design/rod-fishing-v2-brainstorm.md §2–3). Never renumber.
  `Fighting` remains the **legacy single-phase fight** for species without a `RodFightDef` (the
  TrapDef→DeckWorkDef opt-in pattern); v2 species fight `FightDeep → FightSurface`. Consumers group
  "any fight" via `FishingState.IsFightPhase`, not by re-listing phases.
- **`FishingState` grows additively.** The VS-13 fields (`Phase, Tension01, Landing01, FishId,
  DisplayName, Category, WeightKg`) keep their exact semantics. v2 added three diegetic reads —
  `Depth01` (held position in the water column, §2.3), `SlackWindowOpen` (the PULL-now tell, §3),
  `RodBend01` (rod-curvature presentation read, distinct from the `Tension01` danger axis) — via a
  new full constructor; the original 7-arg constructor remains and defaults them neutral. Wave 3
  added `FishOffsetX/Y` (the line's far end, **fight phases only** — a pinned contract), and the
  presenter wave added four presentation reads the rod/line/bobber renderer runs on: `CastCharge01`
  (live wind-back charge / cast-flight progress), `CastAimX/Y` (the cast-path far end outside the
  fight — aim preview, flying line, resting bobber, legacy-fight hook spot) and `RigDepthM` (raw
  metres of line down on the weighted path — paces the count-the-fall sink ripples). Every earlier
  constructor still compiles and defaults the newer reads neutral.
- **Species fight personality is data**: `Fishing.RodFightDef` (Data/RodFights, ids `rodfight.*`,
  append-only) carries the tuning the pure `RodFightMath` (Wave 2) consumes. The fishing fight is
  real-time and RNG-injected — **not** part of the `(worldSeed, gameTime)` determinism contract,
  and never saved.

Guarded by `Assets/Tests/EditMode/FishingV2ContractTests.cs` (frozen ints, additive-struct,
Def invariants).

## 5. Boat & entity architecture (composition)

A boat is a `Rigidbody2D` (Box2D-v3 backend in Unity 6.5) assembled from data-configured
components:

```
BoatEntity
├─ Hull        (mass, drag profile, stability, draught, hold capacity)   ← BoatHullDef
├─ Engine      (thrust, fuel burn) / Sail (for relevant tiers)           ← EngineDef
├─ Hold        (cargo/catch, HU capacity, spoilage timers)               ← runtime state
├─ GearMounts  (handline / longline / net / traps)                       ← GearDef[]
├─ Instruments (compass, depth sounder, radar/GPS)                       ← InstrumentDef[]
└─ Damage      (hull integrity, flooding, breakdown)                     ← runtime state
```

NPCs and facilities follow the same pattern: small components + a Def asset. This keeps systems
parallel-friendly (a new boat = a new Def + prefab, not new subclasses).

## 6. Save system

- **Save only what can't be recomputed.** Persisted: player (position, money, stamina, skills,
  licenses, inventory/hold), owned boats & upgrades, owned property & furnishings, **market
  state**, business/staff/production state, NPC relationships & quest/world flags, region reveal
  state, `worldSeed`, and `gameTime`. **Not persisted:** tide/wind/weather (recomputed from seed
  + time), authored geometry, dormant NPC positions (recomputed).
- **Versioned + migratable.** Every save carries a schema version; `SaveService` runs migrations
  forward. Plan for this from M0 — changing the save format mid-development is otherwise painful.
- **Mobile safety.** Autosave on day-end and on app-suspend (`OnApplicationPause`); the player can
  also save anywhere. Writes are atomic (write temp → rename) so a killed app never corrupts a save.
- **Format:** JSON via a stable DTO layer for readability/debuggability in M0; can move to a
  binary/compressed format later behind the same interface.
- **Schema v1 (VS-08, shipped).** The first concrete schema persists `schemaVersion`, `worldSeed`,
  `gameTime` (the master `double`), `money`, `dayIndex`, `ownedBoats` + `activeHullId`, and the
  onboarding flags — see `adr/0008-save-schema-and-versioning.md`. `SaveService` is a **self-installing**
  persistent service (`[RuntimeInitializeOnLoadMethod]`, no scene wiring) reached through Core via
  `GameServices.Save` (`ISaveService`). The VS-21 onboarding flags are **consolidated** off PlayerPrefs
  into this slot (`World.SaveFlagStore` backs `OnboardingFlags`). It captures money/time/seed on demand
  through the existing Core seams and learns the owned/active boat from the `BoatPurchased` /
  `ActiveBoatChanged` signals. Migration is forward-only via `SaveMigration`
  (v0→v1 is a no-op upgrade: empty fleet/flag lists + a version bump, scalars untouched; v1→v2 adds the
  licence/repair/gear lists and marks every already-owned boat repaired).
- **Load-restore (VS-08, shipped).** Loading a save no longer just fills `Current` — it is **re-applied to
  the live game** so a save resumes exactly where it was saved. `SaveService` exposes
  `ISaveService.LoadedExistingSave` (true only for a resumed game, so a *new* game keeps its authored start
  hour). The composition root hands to `Core.ShellFlow`, whose "enter the world" step runs
  `Core.SaveRestore.ApplyToLiveServices(...)` — the inverse of `SaveService.SnapshotLiveState` — which
  pushes the loaded blob back through the **same Core service APIs** gameplay uses (CLAUDE.md rule 4):
  - **Clock** → `IGameClock.SeekTo(double)` (additive, default-no-op interface method; `GameClock` seeks its
    backing time and re-baselines its rollover guards so it does **not** replay the skipped days).
  - **Money** → brought to the saved balance via `IWallet.Add(delta)` (so `MoneyChanged` fires for the HUD).
  - **Licences** → `ILicenseService.Grant(id)` (idempotent — the same call the vendor makes).
  - **Owned boats / repaired-boat state / gear** → read **live** off `ISaveService.Current`: `OwnedFleet`
    re-grants the saved active hull on the new `GameLoaded` signal (through its existing purchase-swap path),
    while `RepairLedger`/`PlayerGear` query the save directly — so simply loading the blob restores them.
  - **`GameLoaded`** (new `GameSignals` event, no payload) is published once after restore as the single edge
    lanes holding *derived* live state re-sync on. A new game raises it too, so subscribers have one code path.
  The **determinism invariant holds** (rule 5): tide/wind/weather are **never** restored — only the clock that
  drives them is, and the environment is recomputed from `(worldSeed, restored gameTime)`. Restore is
  service-injected + static, so the mapping is fully headless-testable (`SaveRestoreTests`), with a PlayMode
  round-trip + tide-determinism guard (`SaveLoadRestorePlayTests`).
- **The shell (M1 §7.8) — one boot path, two phases.** Boot does **not** land in the world. `GameRoot.Start()`
  calls `Core.ShellFlow.EnterTitle()`, which stops the clock (`IGameClock.IsPaused` — the project's ONE pause
  path, no second clock) and publishes `ShellPhaseChanged`; the save stays **unapplied** until the player picks
  **Continue** (`ShellFlow.ContinueGame()` → the restore above) or **New game**
  (`ISaveService.BeginNewGame()` → a fresh blob written to disk immediately, then the same restore with a null
  blob). The title is a **state of the persistent core, not a second scene**, so the persistent-core +
  additive-region contract (ADR 0004) does not fork; `GameRoot._bootToTitle` turns it off for the dev
  region-iteration cores. The UI side (`UI.ShellPresenter`, self-installing like `SaveService`) renders the
  phase, so App never references UI.
  - **Pause and settings.** `Core.ShellPause` stops the world on the same one path (`IGameClock.IsPaused`,
    restoring what it found) and `ShellFlow.WorldInputBlocked` — true at the title or under a pause menu —
    is what the player rig honours so the helm cannot be steered from behind a stopped clock. The four bus
    volumes come through `Core.IAudioMix` (`GameServices.AudioMix`, registered by `AudioDirector`), and
    settings persist in **PlayerPrefs** (`Core.GameSettings`), NOT the save: they belong to the machine, must
    survive New Game, and must not cost a schema version. **Quit to title** saves, then `App.ShellRestart`
    destroys every `PersistentObject` root and reloads the boot scene — the rebuilt core calls
    `EnterTitle()` itself, so there is still one path to the title, and a new game can never start on a
    half-played world.
  - **Whoever registers a service, unregisters it — and nothing else.** The launch-scoped singletons
    (`SaveService`, `AudioDirector`, `LicenseService`, the `CatchFactory` registrar) install themselves once
    per launch and deliberately **survive** the quit-to-title teardown; their bootstraps and their
    `Awake`/`OnEnable` registrations do not run a second time. So `GameRoot.OnDestroy` takes back only the
    slots `GameRoot` filled (clock, environment, wallet, config), each guarded on `ReferenceEquals` exactly as
    the singletons guard their own — never a wholesale `GameServices.Reset()`, which would strip the survivors
    for the rest of the launch (no save service at all: no Continue, no writes, a settings sheet claiming
    there is no sound). `GameServices.Reset()` remains for tests, which call it explicitly.
    Pinned by `ShellRestartPlayTests`.
  - Two consequences worth knowing before you add boot code: **`SaveService` refuses to write while at the
    title** (`SaveService.WritesAllowed`) — the live services still hold boot defaults, so an
    autosave-on-quit there would overwrite a real save with an empty one; and **anything that seeds itself
    from the save must do so on the `GameLoaded` edge, not in `Start()`** — at the title the loaded blob is
    the *outgoing* game's. `Core.SaveReady.Run(host, action)` is the one-liner for that (used by
    `StartingGear` / `StartingBait` / `StartingPots` / `FrontedFeeGrant`); `LicenseService` rebuilds its held
    set on the same edge.
  - **The title is a shot of the world, and it says which build it is.** There is no key art: the persistent
    core is already rendering the harbour behind the page, always at the authored start hour (the save is
    unapplied until the player chooses, so the light behind the title is always first light). The page is
    composed over it — a wash that is dense down the type column and thin over the harbour
    (`PaperUi.MakeWash` + `ShellGradient`, a vertex ramp so a gradient costs no texture) inside a letterbox
    frame — rather than a flat scrim that would spend the one picture we already have. In the corner,
    `UI.BuildInfo.StampLine` names the version and **which build** (`Application.buildGUID`, seven
    characters; "editor" where no build was made), which is §7.8's exit criterion: a playtest report that
    cannot be pinned to a build is worth very little. Moving the CAMERA to a composed title framing is a
    separate, App-lane question — the UI assembly reaches Core only, by design.

## 7. Tick & performance model

- **Three clocks:** Unity `Update` (rendering/input), `FixedUpdate` (boat physics), and a coarse
  **simulation tick** (~1–4 Hz) for economy/NPC/weather evolution. Heavy world simulation runs on
  the slow tick, not per frame.
- **Tiered simulation.** Only the active region simulates in detail; neighbours simulate coarsely;
  distant regions are statistical (the NPC fleet still "fishes" to move the market — as numbers,
  not agents). NPCs use Active/Nearby/Dormant tiers.
- **Mobile budgets:** target 60 fps on mid-range phones (30 fps floor), pooled sprites/objects,
  sprite atlases, minimal overdraw on the parallax water, draw-call discipline via SRP batching.
- **Time scaling:** fast-forward (sleep/wait) advances `gameTime` and runs catch-up sim ticks
  deterministically rather than real-time stepping.

## 8. Testing & CI

- **EditMode tests** for pure logic and **determinism** (e.g., tide height at a given
  `(seed, time)` is stable; market price formula; save round-trips). These are cheap and catch the
  scariest bugs.
- **PlayMode tests** for integration (boat applies environment forces; fishing yields a catch;
  scene load/unload).
- **CI (post-M0):** GameCI on GitHub Actions to build + run tests on PRs. `qa-test` owns this.

## 9. Multi-platform readiness (don't build now, don't block later)
- Input through `InputService` intents → desktop/gamepad later is a new input map, not a rewrite.
- UI built responsive (safe areas, anchors, scalable) so it reflows from phone to desktop.
- No hard assumptions about touch in gameplay code — gameplay reacts to *intents*.

## 10. Open questions (owned by `lead-architect`)
- DI framework (manual installer vs VContainer) — start manual, revisit if wiring gets heavy.
- Addressables adoption point for the content catalog.
- Networking/cloud-save: out of scope for now; keep SaveService behind an interface so a cloud
  backend can slot in later.
