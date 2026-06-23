# ADR 0009 — Two additive Core seams for the St Peters opening: tidal exposure + region display names

- **Status:** **Accepted** — lead-architect. Lays the Core contracts the **next** wave's St Peters
  greybox build (world terrain + gameplay walkability sim + the crossing fade card) shares. This
  change delivers only the seams + the canon/design updates; the tide sim, the St Peters scene, and
  the UI/World adoption are downstream work in their owning lanes.
- **Date:** 2026-06-23
- **Decision owner:** lead-architect (Core contracts / cross-module integration, CLAUDE.md rule 4)
- **Flagged from:** the owner-ratified decision to **pull the M2 St Peters opening forward and build
  it as a greybox prototype now** (canon `vision-and-pillars.md` §5.8). Two cross-module needs fell
  out of it: (1) terrain (world) and the walkability sim (gameplay) must **agree** on "submerged or
  exposed here, now?"; (2) the crossing fade card must read **"Coddle Cove" / "Port Greywick"**, not
  the scene name — the explicit ui-ux **#54** follow-up that `UI/RegionFade.ArrivalTitle` documents as
  "a follow-up once region display names are exposed via Core."
- **Related:** `0007-active-boat-heading-seam.md` (the **pull-style** Core-contract precedent this
  follows), `design/time-tides-weather.md` §3.5/§5.1 (the deterministic tide + the water-depth rule),
  `design/world-and-regions.md` §6.0/§7 (the St Peters sandbar + the walkable-seabed rule),
  `Core/Environment/EnvironmentSample.cs` & `Core/Services/IEnvironmentService.cs` (extended here),
  `Core/Services/GameServices.cs` & `Core/Events/GameSignals.cs` (the existing Core seams these mirror).

## Context

The decided opening (canon §5.8) leans on a falling tide from minute one: as the deterministic tide
**falls**, exposed seabed becomes **walkable**, a **tide-gated sandbar** between St Peters and Port
Greywick bares as a walking path, and the player crosses **on foot** before owning any boat. Two
modules must cooperate without referencing each other (rule 4):

1. **Tidal exposure.** The **world** authors terrain elevation (its tilemap/heightfield); the
   **gameplay** on-foot walkability sim decides whether a tile is walkable *right now*. Both must
   answer "is this position submerged or exposed at the current tide?" from **one** source of truth,
   or the shoreline the player walks and the terrain the world drew will drift apart. The tide is
   already deterministic (`IEnvironmentService`, recomputed from `(worldSeed, gameTime)`, never saved
   — CLAUDE.md rule 5); what is missing is a *named water-level accessor* and the *one exposure rule*
   built on it. `EnvironmentSample` today exposes `TideHeight` but no exposure/depth helper.

2. **Region display names.** `UI/RegionFadeOverlay` reads only `SceneManager.activeSceneChanged` (a
   scene name) and titles the arrival card via `RegionFade.ArrivalTitle(sceneName)` — which yields
   "Greywick"/"Greybox", not the canon "Port Greywick"/"Coddle Cove". The proper display names live on
   `World.RegionDef.DisplayName`, but the UI references **only** Core by design, so it cannot read
   World. The same UI→Core-only boundary that ADR 0007 protected for boat heading applies here.

## Decision

Add **two additive, pull-style Core seams**. No new save state, no new EventBus signal.

### (a) Tidal-exposure query — `IEnvironmentService.WaterLevelAt` + `Core.TidalExposure`

- **`IEnvironmentService.WaterLevelAt(double totalSeconds)`** — the deterministic water-surface level
  for the active region, **metres above chart datum**. Added as a **default interface method** that
  returns `TideHeightAt(totalSeconds)` (the tide *is* the water level for the inshore/intertidal
  regions), so the existing `Environment.EnvironmentService` and the test fakes **compile unchanged**.
  It is named separately from `TideHeightAt` so consumers express intent ("the water level I walk
  under") and so a region that later offsets its local water plane from raw tide height can **override
  this one accessor** without touching call sites.
- **`Core.TidalExposure`** — a pure, engine-light static helper (no `UnityEngine`, no RNG):
  - `WaterDepth(waterLevel, terrainElevation)` → `waterLevel − terrainElevation` (≤ 0 = dry/exposed);
  - `IsExposed(waterLevel, terrainElevation)` → `terrainElevation >= waterLevel`; `IsSubmerged(...)`;
  - a convenience `IsExposed(IEnvironmentService, totalSeconds, terrainElevation)` that pulls the level
    and returns `false` (safe "treat as submerged" default for a walkability gate) when the service is
    null.

  This is the **single rule** the world (terrain authoring) and gameplay (walkability sim) both read,
  in the same metres-above-datum frame the tide model and boat grounding use — so on-foot walkability
  and boat draught compare against the **same number** (`design/time-tides-weather.md` §3.5/§5.1).

### (b) Region display names — `Core.RegionDisplayNames`

A tiny Core-owned static registry mapping a **key (scene name and/or stable region id) → display
name**. The **world** (owner of `RegionDef`) **registers** mappings at boot (World → Core is allowed);
the **UI** (Core-only) **reads** via `RegionDisplayNames.Resolve(key, fallback)`. Neither module gains
a reference to the other — the seam stays in Core, exactly like `GameServices` and the EventBus.
`Resolve` falls back to a caller-supplied string, so the UI's existing `RegionFade.ArrivalTitle(...)`
derivation becomes the fallback and the card always shows something readable. Adoption (the UI
one-liner and the World registrar) is the owning lanes' follow-up; this change ships only the seam.

### Pull, not push; no new signal

Both seams are **pull** queries, matching ADR 0007's rationale: exposure is sampled on demand exactly
as `EnvironmentSample` is (a `struct`/scalar return, zero per-call GC), and display names are static
metadata. A *push* (`OnTileExposed` per tile per tick, or a `RegionDisplayNameChanged` event) would be
per-tick spam / needless bookkeeping for data that callers already poll. So **no `GameSignals` entry
is added** this wave — the lead-architect-owned signal file is unchanged.

### Determinism & save (the invariant guarded)

Tidal exposure is a **pure function** of the deterministic water level (recomputed from
`(worldSeed, gameTime)`) and authored terrain elevation. **No RNG; nothing saved** — the tide is never
serialized, and exposure is reconstructed, exactly per CLAUDE.md rule 5 and the lead-architect DoD.
Display names are presentation metadata, also unsaved. **No save-format change**, so no schema bump or
migration is required.

## Consequences

- **Zero coupling added.** Core gains contracts only; World implements the registrar + terrain field;
  gameplay implements the walkability sim against `TidalExposure`; UI reads `RegionDisplayNames` and
  may delegate `WaterLevelAt` overriding to the Environment module — all in their own lanes, all
  through Core.
- **Non-breaking.** `WaterLevelAt` is a default interface method → existing implementers/fakes are
  untouched. The two new types are pure additions. The `EnvironmentSample` struct is **not** reshaped
  (the M2 `WaterDepth`/`SeabedElevation`/`WavePush` fields remain the documented forward-additive
  change in `design/time-tides-weather.md` §5.1, not done here).
- **The next wave is unblocked:** world authors St Peters terrain elevation + registers display names;
  gameplay builds the falling-tide walkability sim on `TidalExposure`; ui-ux closes #54 by resolving
  the fade-card title through `RegionDisplayNames`.
- **Docs updated in the same change** (canon §5.8 + the four design docs + this ADR), per the
  lead-architect DoD ("new shared contracts documented in the same PR").

## Rejected alternatives

- **Add `WaterLevelAt` as a required interface member** — breaks the gameplay-lane `EnvironmentService`
  and both test fakes in a Core PR, needing their sign-off this wave. The default interface method is
  additive and lets the owner override later when the real per-region water plane is wired.
- **Reshape `EnvironmentSample` now** (add `WaterDepth`/`SeabedElevation`) — premature; the M2 sample
  reshape is already a documented forward-additive change, and the exposure query needs only the water
  level + an authored elevation the caller supplies, not a per-position seabed baked into the sample.
- **UI reads `World.RegionDef` directly** — breaks the UI→Core-only boundary (rule 4), the very reason
  the seam exists.
- **Region names via a new `GameSignals` event** — display names don't *change*; a static registry the
  UI reads on the existing scene-change callback is simpler and allocation-free.
- **Bake exposure logic into the boat-grounding code** — would fork the rule between on-foot
  walkability and boat draught; the whole point is **one** shared rule both consume.

## Open questions (later, for the owning lanes)

- **Within-region elevation source.** The exposure helper takes `terrainElevation` as a parameter; how
  the world supplies it per position (tile heightfield vs per-feature zones) is a world-lane call in
  `design/world-and-regions.md` §9.4 / `design/time-tides-weather.md` §3.5 — out of scope for the seam.
- **Registry key choice.** World may register by **scene name** (what the fade card has) and/or by
  **region id**; `RegionDisplayNames` accepts either. If region scenes are ever reused across regions,
  prefer the id key — flag at adoption.
- **Localization.** Display names should ultimately come from the localization tables (per the design
  docs); `RegionDisplayNames` holds the resolved string, so a localized registrar slots in unchanged.
