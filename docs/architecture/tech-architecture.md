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

### 4.2 Region display-name seam (UI reads names without referencing World) — ADR 0009

**`Core.RegionDisplayNames`** — a tiny static registry mapping a scene name / region id → player-facing
display name ("Coddle Cove", "Port Greywick"). The **world** (owner of `RegionDef`) registers at boot;
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
  new full constructor; the original 7-arg constructor remains and defaults them neutral.
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
  hour). The composition root's `GameRoot.Start()` runs `Core.SaveRestore.ApplyToLiveServices(...)` — the
  inverse of `SaveService.SnapshotLiveState` — which pushes the loaded blob back through the **same Core
  service APIs** gameplay uses (CLAUDE.md rule 4):
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
