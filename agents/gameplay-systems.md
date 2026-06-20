# Gameplay Systems — Charter

**Mission.** Build the signature moment-to-moment game: the deterministic sea (clock, tide, wind,
weather), the boat as a force-driven planar rigid body, the danger model (grounding, capsize,
breakdown, rescue), the fishing catch loop, and the player controller with stamina. This is where
P1 and P5 are *felt*.

**Pillars you most serve.** **P1 The Sea Has Moods** (tide/wind/current as real forces you read) and
**P5 Cozy, but with Teeth** (grounding, swamping, stranding, tow). Supporting **P2** (the boat ladder
as physics that gets heavier/slower up-tier) and **P4** (every job done by hand first).

**You own.**
- `Assets/_Project/Code/Environment/` — `TimeService`'s consumers and the **`EnvironmentService`**:
  the 24h clock (`gameTime` as `double`), the semidiurnal tide formula + spring/neap envelope, wind
  field, sea-state, fog, the front scheduler, and the per-tick **`EnvironmentSample`** the boat reads.
- `Assets/_Project/Code/Boats/` — the composable boat (Hull/Engine/Hold/Gear/Instruments/Safety),
  `BoatPhysicsController` (Box2D-v3 force assembly: engine + sail + anisotropic hydro-drag + windage +
  rudder), and the danger components (`GroundingCheck`, `StabilityCheck`, `IngressCheck`, `EngineHealth`,
  `FuelCheck`, `RescueController`).
- `Assets/_Project/Code/Fishing/` — gear methods (handline/longline/net-trawl/trap/dredge), the
  catch-gating **`catchContext`** contract, and catch resolution + size roll.
- `Assets/_Project/Code/Player/` — the player controller, inventory, and the light stamina system.
- `Assets/_Project/Data/Boats/`, `Gear/`, `Bait/` — one Def per entity (`BoatHullDef`, `EngineDef`,
  `InstrumentDef`, `GearDef`, `BaitDef`).

**You do NOT own / hand off.**
- **What** fish exist / catch *rates* / value → **economy-sim** (`Data/Fish`, the catch *resolver
  tables*). You own the gear/boat/tide *gating* interface; they resolve it into a species + quantity.
- Region geometry, seabed heightfields, scenes → **world-content** (you *consume* `WaterDepth`/
  `SeabedElevation`; they author it).
- HUD widgets (tide gauge, wind, the sailing controls) → **ui-ux**; you expose the data + react to
  intents (`SetThrottle`, `SetHeading`, `Haul`). Boat/water art → **art-pipeline**. Engine/sea SFX →
  **audio**. Shipwright buy flow / tow *pricing* → **economy-sim**.

**Read first.** `../docs/design/time-tides-weather.md` (the tide/weather math + the FORCES interface
§5) · `../docs/design/boats-and-navigation.md` (force model §2, danger §3, rescue §3.7) ·
`../docs/design/fish-and-content.md` (the catch contract boundary) · `coordination.md` (§1, §7
handoffs) · `../docs/architecture/tech-architecture.md` (§4 Environment→Boat contract).

**Core responsibilities.**
- Implement `EnvironmentService` as the single source of environmental truth — pure O(1) functions of
  `(worldSeed, gameTime, position)`, sampled at 4 Hz per active boat, emitting `EnvironmentSample`.
- Build the boat force model so a boat *sets* with the current and is shoved by wind with engine off,
  and tracks-forward/skids-sideways with `dragSide >> dragFwd`; rudder authority scales with way.
- Implement grounding (`draught > waterDepth`), broach/capsize (sea-state vs seaworthiness vs load),
  taking-on-water, breakdown/fuel, and the telegraphed, never-fatal rescue/tow set-piece.
- Build the one-thumb fishing tension/landing interaction and the catch-context that gates it by
  region + gear + boat + tide + weather.

**Definition of Done — domain specifics** (beyond `coordination.md` §3).
- **Determinism tests are mandatory:** tide height, tide rate, wind, sea state, and a built tide table
  for a given `(seed, gameTime, region)` are bit-stable across runs and across save/load. No
  `System.Random` with hidden state in any sim path — hash/Perlin keyed by seed+time+position only.
- Boat physics need not be bit-deterministic (real-time, player-driven), but it **reads** a
  deterministic sample; saves store boat *state* (fit, health, fuel, bilge, load, position), never a
  physics-frame snapshot.
- Danger is **telegraphed and survivable**: every stranding follows ignored warnings and resolves to
  time/money/partial-load + a rescue — never a wipe, never loss of the boat. Capsize is rare and earned.
- Mobile: struct samples, pooled effects, no per-frame GC in the physics/sample hot path; one active
  boat dominates cost.

**Collaboration & handoffs.** You depend on world-content for seabed/current-field data and on
art/audio for boat/water/SFX placeholders (greybox now, file a backlog item). economy-sim depends on
your `catchContext`; ui-ux depends on your exposed tide/wind/heading state and consumes intents.
Anything you need from another module is a **Core contract**, tagged to lead-architect.

**Per-phase focus.**
- **M0:** `EnvironmentService` v0 (clock + deterministic semidiurnal tide + `OnDayRollover` + sleep/skip);
  dory controller v0 (kinematic top-down move + follow-cam, *not* the force model); fishing v0
  (spot → bite → tension band + landing gauge); catch resolver v0 with economy-sim.
- **M1:** boat **force model v1** (thrust + anisotropic hydro-drag + windage + rudder) consuming the
  full FORCES `EnvironmentSample`; `EnvironmentService` v1 (wind field, sea-state, tide table); the
  Punt as a composable boat. *Prototype the dory on a real phone early — this is the #1 design risk.*
- **M2+:** the danger model (grounding/capsize/ingress/breakdown + rescue/tow); weather fronts/fog/storms
  + forecast tools; offshore gear (net/trawl) and the dragger/trawler tiers (M3).

**Guardrails.**
- Never save tide/weather/sea state — recompute it. Never add hidden global randomness to a sim system.
- Don't hard-code a boat stat, tide constant, or catch number — it's a Def or `GameConfig` value.
- Don't reach into economy-sim's catch tables or world-content's scenes; meet them at the contract.
- Keep `gameTime` a `double` — `float` loses sub-minute precision over a multi-year save.
- Don't let the dory feel like a car: throttle is a tactical held slider, and you can't steer dead in
  the water. Respect the seamanship fantasy.
