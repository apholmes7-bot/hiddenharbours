# ADR 0042 — The squash is an art fact: the world plane vs the bake projection

- **Status: ACCEPTED** — ruled by `lead-architect` 2026-08-29, measurement-first (three parallel
  consumer/convention sweeps and a coordinator spot-check); recorded and enforced 2026-09-01 by the
  station-frame migration this ADR ships with (`fix/station-frame-squash`).
- **Date:** 2026-08-29 (the ruling) · 2026-09-01 (this record and the migration).
- **Decision owner:** `lead-architect` — a convention that crosses every kit. Implemented by
  `world-content`; the guard written to `qa-test`'s discipline.
- **Serves:** **P3 (A Living Working Coast)** — a forecourt whose kerb, bollards and shop walls stop
  the player exactly where they are DRAWN — and **P5 (Cozy but with Teeth)**: a body stopped by an
  invisible wall, or walking through a painted one, is the wrongness a player feels before they can
  name it.
- **Related:** `0004-perspective-and-scene-strategy.md` (1 tile = 1 m — the ground plane this ADR
  keeps), `0034-ground-bearings-not-world-xy.md` (the BEARING half of the same question; its decision
  covers headings only), `0026-rig-pivot-conventions.md` (the pivot is the footprint's ground centre —
  what "about the pivot" means below), `0036-interior-levels-as-layers.md` (a seamless interior shares
  its shell's cell and pivot), `0022-3d-boat-hulls.md` (`BoatVisualDef.ArtBakeElevationDegrees` and
  `DeckAreaMath` — the per-artwork precedent).

## Context

Two kits answered "is world Y squashed?" in writing, and gave opposite answers.

The house and shop family (`InteriorFootprint`, `BuildingFacing`, `ShopPlacement`,
`StPetersInteriors`) places every wall, door anchor and prop footprint by **rotating on the ground and
then multiplying the ground-Y component by `sin 40°` = 0.6427876** — the shared bake camera's
elevation (`SpriteLightMath.GroundDepthScale`). Its file headers say, in capitals, that THE WORLD XY
PLANE IS THE SQUASHED GROUND PLANE (`InteriorFootprint.cs`, `IsoGround.cs`).

The gas-station kit (`StationCatalog.LocalToWorld`) placed every collider, standing spot and piece
offset as a **pure rotation in unsquashed metres**, and its remark said the opposite: *"the world plane
here IS the ground plane; the iso squash is a RENDER transform the sprite already carries"*.
`SpriteLightResponse.hlsl` says the same in capitals (*"THE 0.766/0.643 DO NOT APPLY TO WORLD
POSITIONS"*). When the C-store gained a doorway (#687), the interior placement had to pick one frame,
picked the station's, and recorded the disagreement as open.

**The question as posed is ill-posed.** Measured, the project runs two coherent regimes split by
CATEGORY, not by kit — and both kits were half right:

- **Move-and-measure** — player velocity, boat physics, mooring ropes, channel widths, NPC route
  lengths and speeds, lamp radii, wave lift, interact ranges, the 1 m tile grid, the painted height
  field: **world XY is the GROUND plane, 1 world unit = 1 m** (ADR 0004, never superseded). The hlsl's
  capitals are true of this.
- **Picture-placement** — any geometry that must COINCIDE with baked ¾ art: wall colliders, door
  anchors, prop footprints, pivot separations inside one rig composition. This places under the ART's
  own projection — **rotate on the ground, then × sin(bake elevation) on ground-Y** — because the squash
  is baked into the pixels and **nothing at render time transforms anything** (verified: no parent
  scales, no projection override, no shader Y-squash). `InteriorFootprint`'s capitals are true of this.

### The three measurements

1. **Cell increments, height-free.** The island sheets grow one 3.30 m bolt-down slot at a time. Their
   cropped cells (`Assets/_Project/Art/Sprites/GasStation/Iso/GasStation.json`) grow
   **ΔW = 104 · 106 · 106 px** and **ΔH = 67 · 68 · 68 px** per slot (s1→s2→s3→s4). ΔW ÷ 32 = 3.30 m
   across, as authored; **ΔH ÷ ΔW = 0.6442 · 0.6415 · 0.6415 ≈ sin 40°**. Unsquashed ground would need
   ΔH = ΔW. The kerb is 0.165 m tall on every island, so height cannot be what grows: this is the
   ground alone, read off the pixels.
2. **Re-projection against the drawn cell** (this PR's alignment tests, prototyped offline over the
   sidecar and the sheet contract before they were written in C#). Every collider on every exterior
   piece, rotated then squashed, fits inside its own cropped cell at all eight facings — worst case
   0.4 px (the vent risers) — and the islands' and storefronts' WHOLE ground footprints, step-over kerb
   included, fit exactly. Rotated only — the kit's placement until this ADR — at the shipped facing
   (cell 2) the island_s2 kerb overflows its cell by **1.04 m (33 px)**, the C-store's plan by
   **1.07 m (34 px)**, and the canopies and larger islands by up to 72 px. (The small dispensers'
   `flat` plinths are drawn inside their published footprints and overhang the ink by up to 3.6 px at
   the diagonals; nothing collides with a plinth.)
3. **Sibling kits un-squash on read.** `BuildingFacing.DoorModelMetres` divides door-anchor pixels by
   the depth scale, and all eight shipped house/shop door anchors recover exactly ±L/2 — a door in a
   gable wall — to seven digits. A squashed picture read through an unsquashed frame would not.

### The damage, at Route 91

The forecourt is drawn at facing 2: the plan's DEPTH axis lies along world X (never squashed) and its
ACROSS axis along world Y. That is why the defect was masked — the front edge, the store's position and
the pump standback were all right, and what was wrong was everything measured across the island.

| geometry (plan metres, local x) | placed until now | under the art's projection | move |
|---|---|---|---|
| C-store wall ring, each side wall (11.6 m across) | ±5.80 m | ±3.73 m | **2.07 m in, each side** |
| island_s2 kerb footprint (7.76 m) | 7.76 m long | 4.99 m | **2.77 m shorter** |
| bollards (±3.59) | ±3.59 m | ±2.31 m | **1.28 m each** |
| dispensers on the two slots (±1.65) | ±1.65 m | ±1.06 m | 0.59 m each |
| canopy posts (±3.30) | ±3.30 m | ±2.12 m | 1.18 m each |
| price pylon (−6.275) | 6.28 m off the island | 4.03 m | 2.24 m |
| loaner-can row (−4.0) | 4.00 m off | 2.57 m | **1.43 m** |
| fill cluster (4.475) · vent (6.775) | 4.48 · 6.78 m | 2.88 · 4.36 m | 1.60 · 2.42 m |
| C-store position (local x 0) · forecourt front edge (depth axis) | — | — | **unchanged** |

The interim that opened the C-store (#687) pinned a depth scale of 1 and proved an algebraic identity:
both frames agreed because both were unsquashed. Its control probed the one point that moves 0.000 m at
the shipped facing, and its remark's "three metres" was wrong — the C-store's back wall moves
8.2 ÷ 2 × (1 − 0.643) = **1.46 m**; 2.93 m is the change in TOTAL depth.

## Decision

1. **The squash is a per-artwork ART FACT, not a world fact.** Geometry that must coincide with baked
   ¾ art places under that art's own projection: rotate on the ground, THEN multiply ground-Y by
   sin(bake elevation) — 0.6427876 for every 40° kit. `InteriorFootprint.ModelToWorld` is the
   canonical form; `BoatVisualDef.ArtBakeElevationDegrees` + `DeckAreaMath` (default 90° = no squash)
   is the per-artwork precedent.
2. **World XY stays the GROUND plane for everything that moves or measures** (ADR 0004). Speeds,
   ranges, the tile grid, the height field, lamp radii, interact ranges and the sitings of independent
   objects are world metres and are not squashed.
3. **The station kit migrates to regime 1** (this PR). `StationCatalog.LocalDirToWorld` rotates then
   squashes and `WorldDirToLocal` un-squashes then un-rotates; every collider is a polygon PATH
   re-derived per facing from one shared shape list (`StationCatalog.ColliderShapes`), because a
   `localRotation` cannot shear, a Box cannot shear and a Circle cannot be an ellipse — `ColliderZRotation`
   and `TurnColliders` are retired; the whole-scene reach audit marches in the piece's own ground frame
   and projects each candidate to world to test it; the doorway cut passes the shared depth scale and the
   `NoSquash` interim is gone. The prefabs are rebuilt with polygon paths at cell 0's projection.
   **Regime 2 inside the same kit:** the wharf pedestals' SPACING — two 2 m WORLD interact ranges kept
   apart — is a siting, not a rig composition, so `WharfPumpPositions` stays the one authority and each
   pedestal is placed at its world siting; only its own colliders and standing spot are squashed. The
   loaner-can row's spacing is interact separation and world metres for the same reason: its centre
   projects with the forecourt (it is a place on the drawn apron), its step is stretched in the layout's
   frame so the cans stand 0.6 m apart on screen rather than 0.39 m.
4. **Scope corrections, by this record and not by rewriting code.** `IsoGround.cs`'s and
   `InteriorFootprint.cs`'s "THE WORLD XY PLANE IS THE SQUASHED GROUND PLANE" is true of
   picture-placement — a bearing read off the screen; a room's walls — and each header now carries one
   paragraph saying so and linking here; their maths is untouched. `SpriteLightResponse.hlsl`'s "THE
   0.766/0.643 DO NOT APPLY TO WORLD POSITIONS" is true of move-and-measure (lamp positions, ranges) and
   stays as written. ADR 0034's decision covers bearings only and is untouched.

## Consequences

- Every station collider, standing spot and piece offset lands on the picture it belongs to. All of it
  is builder-authored: the owner's next NMC Build click re-derives it, and no scene is committed.
- **Station interact reaches become ~1.56× more generous north–south than east–west.** A 1.2 m ground
  arm is 0.77 world units up the screen and 1.20 across, against a 2 m world interact range. The village
  has carried the same asymmetry since its first house (ADR 0034's pacing note); it is now consistent
  rather than divergent.
- **The guard.** `GasStationCansAndInteriorTests` asserts that every exterior piece's projected
  COLLIDERS fit inside its own drawn cell at all eight facings (and the islands' and storefronts' whole
  ground footprints with them), and that the UNSQUASHED projection does not (island_s2 and store_sStore
  at the shipped facing, by more than 0.5 m) — the test that would have caught the defect on day one. `NineMileCreekStationTests` repeats the fit on the PLACED scene, asserts
  no collider child is rotated, and pins the wharf pedestals to their world sitings (the canary).
  `StationPieceDefTests` pins every prefab's collider paths to the Def's footprint at cell 0.
- The reach audit's `MovedMetres` is ground metres. A body's collider is a world-space circle while the
  audit inflates by the rig's body radius in ground metres — the two agree across and differ by the
  squash up the screen; the audit is the more conservative of the two on that axis, and nothing at either
  site is stranded by it.

## Open, and deliberately NOT settled here

- **Ground speed is still direction-dependent.** `PlayerWalkController` moves at uniform WORLD speed, so
  a walk north covers 1.56× the ground metres of a walk east. ADR 0034 left that as a pacing question;
  so does this.
- **The boat lane has not been measured for either regime** — ADR 0034's own open item, unchanged.
- Whether a body's world circle should become a ground ellipse in the reach audit is a refinement
  nobody has needed; it would move standing spots by at most the squash of a 0.22 m radius.
