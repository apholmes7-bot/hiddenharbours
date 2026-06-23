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
| **EventBus** | Decoupled pub/sub between modules (`FishCaught`, `TideChanged`, `BoatGrounded`, `DayStarted`, `MarketTick`). | Typed events; no module references another's classes. |
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

### 4.2 Region display-name seam (UI reads names without referencing World) — ADR 0009

**`Core.RegionDisplayNames`** — a tiny static registry mapping a scene name / region id → player-facing
display name ("Coddle Cove", "Port Greywick"). The **world** (owner of `RegionDef`) registers at boot;
the **UI** (Core-only) reads `Resolve(key, fallback)` so the crossing fade card titles correctly
(closes the ui-ux #54 follow-up) without a UI→World reference. Presentation metadata: unsaved, no
determinism concern.

## 5. Boat & entity architecture (composition)

A boat is a `Rigidbody2D` (Box2D-v3 backend in Unity 6.3) assembled from data-configured
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
  `ActiveBoatChanged` signals; re-applying that loaded state into the live gameplay objects is the owning
  lanes' follow-up (they read `ISaveService.Current`). Migration is forward-only via `SaveMigration`
  (v0→v1 is a no-op upgrade: empty fleet/flag lists + a version bump, scalars untouched).

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
