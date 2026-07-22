# ADR 0023 — The water becomes a displaced surface (the displaced-water arc)

- **Status:** **ACCEPTED 2026-07-22** by the owner — the displaced-water arc is greenlit, with **no
  flat-shader interim** (the envelope-relative whitecap retune the spike offered as a cheap partial
  win ships *inside* this arc, not before it). Records the decision measured by the `spike/3d-water`
  harness (unmerged; verdict at `Assets/_Project/Code/Tools/Editor/Spike3dWater/VERDICT.md` on that
  branch) and the **shore-seam proof this ADR's PR ships** (Core `ShoreFadeMath`, the
  `ShoreSeamProof` editor harness, and `ShoreFadeMathTests`).
- **Date:** 2026-07-22
- **Decision owner:** lead-architect (the render==sim P1 integrity seam and the URP rendering
  plumbing — `agents/coordination.md` §1.1 "Water/fog/lighting"). **art-pipeline** owns the look of
  the displaced surface; **gameplay-systems** owns the hull-heave consumers; **tools-editor** owns
  the proof harness's home.
- **Serves:** **P1 "The Sea Has Moods"** — the owner's explicit ask: *"visually SEE the wave
  formations and tell larger waves — currently it's hard to tell."* Legibility is the point; looks
  are secondary. Also **P5** (a sea you can read is a sea that can be fairly dangerous).
- **Builds on:** [ADR 0018](0018-shared-wave-field.md) (the one deterministic wave field),
  [ADR 0022](0022-3d-boat-hulls.md) (the off-screen MRT + overlay integration pattern and the mesh
  hulls the waterline climbs), [ADR 0014](0014-painted-seabed-height-authoring.md) /
  [ADR 0009](0009-tidal-exposure-and-region-display-name-seams.md) /
  [ADR 0010](0010-water-rendering.md) (one height map, three consumers — now **four**).

---

## Context

The production water is a flat quad: the shared wave field (ADR 0018) is *painted* onto it as
brightness bands and whitecaps, but no pixel ever moves. The spike measured the consequence: a
genuine 100%-of-envelope wave event — **found, not authored**, by a deterministic scan of the
reference sea (wind 10.78 m/s, seaState 0.75, `WaveFieldSettings.Default`, PhaseSeed 0; the event is
**t = 1513.5 s at world (−6.5, 2.1), h = 1.045 m of a 1.047 m envelope, ×1.25 the typical tallest
in-view crest**) — is **invisible** in production flat water. Whitecaps mark every crest with equal
salience; the big one drowns in the speckle.

The spike's A/B answered the question: vertically displacing the same field makes the big wave
**pointable** already at ×1, obvious at ×1.5–2, and the motion cues (silhouette lift, crest
occlusion, the waterline climbing a hull's planking) are things the flat shader can **never** give.

## Decision

**The sea becomes a vertically displaced mesh surface, lifted by the existing deterministic wave
field, drawn through the ADR 0022 off-screen pattern — with displacement faded to exactly zero at
the walkable waterline by the shore seam this ADR proves.** The constraints below are part of the
decision.

### (1) The ONE-SEA rule

The displaced surface displaces the **existing** height field only: the `WaveMath` trains published
by `WaveFieldBridge` (`_WaveTrain0..3 / _WavePhases / _WaveFieldParams`), evaluated by the same HLSL
twin the flat water already runs per pixel. **Never a foreign sim, never a second wave model, never
saved state** — waves stay recomputed from `(worldSeed, gameTime)` (CLAUDE.md rule 5). The vertex
stage is the same math as the fragment stage, at a different sampling density.

### (2) Exaggeration ×1.5, ONE shared constant

The readability sweet spot is **×1.5–2 displacement exaggeration; ×1.5 is the default**. ×1 already
reads; **×3 breaks the iso framing** (crests visually detach from troughs — spike-measured). The
seam work adds an analytic reason to prefer ×1.5 (§ The shore seam): at ×1.5 the screen mapping is
provably shear-free for the reference sea (worst shore-normal displacement slope 0.82 < 1); at ×2
the worst-case alignment can exceed 1.

The constant is **SHARED**: the surface's vertex lift, a mesh hull's visual heave, and every anchor
that rides the water (buoys, oars, wake) read their screen lift through the same
`ShoreFadeMath.DisplacedHeight(h, depth, band, exaggeration)` with the same exaggeration value — so
a boat's heave rides exactly the sea it is drawn on. This is the overlay-pose lesson (never rescale
one consumer alone) made structural. The value itself becomes owner-tunable data (`GameConfig`
plumbing lands with phase 2); it is a parameter, not a constant, in every API.

### (3) The style law

Measured in the spike and binding on every implementation:

- **Dither BAND EDGES only.** Full-range Bayer dissolves the quantised value bands back into a
  smooth gradient — the surface reads as airbrushed 3D, not this game. Solid bands, dithered edges
  (the boat rigs' own language).
- **Shade from the owner's palette anchors** (`_PaletteDeep/Mid/Shallow/Foam` off `Water.mat`), so
  the displaced surface wears his colours and the ADR 0015 palette guard-rail keeps applying.
- **Dither cells are WORLD-locked** (PPU-quantised world coords index the Bayer matrix — the
  ADR 0022 facet discipline). Zero crawl by construction.

### (4) Envelope-relative shading and foam

A named component of the arc, not an optional dressing: value bands and whitecap salience key on
**height relative to the field's envelope** (`height / TotalAmplitude`), not on absolute height.
This carried much of the spike's still-frame legibility: only near-envelope crests wear solid foam
cores, so the rare big wave is marked and the everyday chop is not. See § Whitecap salience.

### (5) Waterline on the hull

Proven by the spike's probe: hull meshes (ADR 0022) and the displaced sea share one private z-buffer
in the iso frame, and the waterline **climbs ~1 m along the lobster boat's planking per dominant
period**. This is the owner's explicit "water changes height on the hull" ask and it is cheap — no
new tech beyond the shared depth buffer the facet pass already owns. It pairs with (2): the hull's
heave and the surface's lift use the shared constant, so boat and sea agree.

### (6) Integration route: the ADR 0022 MRT + overlay pattern

A displaced mesh cannot ZWrite into the shared scene depth buffer — sprites z-test against it and a
depth-writing mesh punches holes in every later sprite (the ADR 0022 lesson, verbatim). The water
mesh therefore joins the facet feature's **off-screen MRT pass with the private depth buffer**, and
an **overlay quad** re-composes the result in-scene through the SortingGroup, exactly as mesh hulls
do. Boats already live in that pass — which is what makes (5) free.

## The shore seam (the hard problem, solved and proved here)

### The problem

Displacement moves water pixels along screen-y; land tiles do not move. At the coast a displaced
crest would slide the visible waterline off the sim's wet/dry contour — water drawn over dry sand,
or a bared strip where a trough pulls the water away. That breaks the **P1 integrity rule** the
whole terrain stack is built on (ADR 0009/0010/0014: the visible waterline and the playable
waterline are the same line by construction), and it would break it *differently at every tide*,
because the walkable waterline is the moving depth-0 iso-contour of the painted height field.

### The mechanism

**Displacement amplitude is multiplied by a smooth fade of local still-water depth** —
`Core.ShoreFadeMath` (new, additive):

```
fade  = smoothstep(0, band, depth)          depth = WaterLevelAt(t) − ElevationAt(pos)
lift  = waveHeight × exaggeration × fade
```

- **Zero at the waterline by construction.** The fade's zero set is the depth-0 contour *itself* —
  the exact same `WaterLevelAt − ElevationAt` read the water shader, the walkability gate and
  boat-crossing already share (one height map; this is the **fourth consumer**). There is no second
  contour to drift, and as the tide moves the waterline, the seam moves with it automatically. The
  shader's `clip()` contract is untouched: the contour is still computed from *undisplaced* ground
  position, and displacement now dies before it can contradict it.
- **The band is DERIVED, not a free magic number** (rule 6):
  `band = 2 × envelope × exaggeration × maxShoreGradient` (`RecommendedBandMeters`). Two analytic
  hazards bound it. *Overlap* (a crest's lift crossing the contour): lift ≤ A·s·smoothstep(d/B)
  against a ground distance ≥ d/g gives a worst ratio of 1.125·A·s·g/B → safe iff B ≥ 1.125·A·s·g.
  *Fold* (the screen mapping y + s·h·fade going non-monotone — the shear that visually detaches
  crests): the worst in-band term, the full envelope crest parked at the fade's steepest point, is
  s·A·(1.5/B)·g → B = 1.5·A·s·g is exactly marginal. Coefficient **2** covers both with margin.
- **The seam's ground footprint is steepness-independent:** band/gradient = 2·A·s metres — **≈3.1 m
  of shallows at the reference sea's envelope × 1.5** on *every* coast. Steep shores get a deeper
  band over the same ground width.
- **Physicality note, stated honestly:** real shoaling waves *grow* before breaking; ours fade. This
  is a deliberate style/integrity choice — the coast contour is sacred (P1), shore foam is the
  production dressing's job, and "3D waves are an open-water feature" (the spike's own framing).

### The proof (evidence, not prose)

Two halves, both deterministic, both in this ADR's PR:

**Headless numeric proof — `ShoreFadeMathTests`** (EditMode, CI-safe, no GPU): on the reference sea,
along shore-normal transects (each screen column is exactly a 1D problem, because displacement only
moves pixels along screen-y) over gentle flats (g 0.05), a beach (0.15), a steep shelf (0.5) and a
south-facing shore (−0.15), at tides −0.6 / 0 / +0.75 m, over two dominant periods plus the
envelope-event instant:

1. **Contour-pinned:** displacement is *exactly* 0 (float-equal) at and beyond the depth-0 contour,
   every tide, every time.
2. **No tear:** no sample's screen position ever crosses the waterline (max overlap ≤ 1e-4 m over
   every transect), and the screen mapping stays strictly monotone — **including with the
   100%-envelope event deliberately parked at mid-band**, the analytically worst placement.
3. **Open sea untouched:** at depth ≥ band the fade is exactly 1 and the displaced height is
   bit-identical to the un-seamed value; the t = 1513.5 s event still displaces at full ×1.5.

**Rendered proof — the `ShoreSeamProof` editor harness**
(`Assets/_Project/Code/Tools/Editor/ShoreSeamProof/`, evidence committed in its `Evidence~/`,
spike-pattern): draws the land layer flat and the displaced sea over it with the production `clip()`
contract, renders **boundary masks** (water = red, land = green) and measures the rendered
water/land boundary against the analytic contour per screen column, 12 frames over a dominant
period, all four shores × three tides. Pass bar: |deviation| ≤ 1 px (sub-pixel rasterization only).
The **seam-off control** (the naive port) is rendered alongside and must tear visibly — proving the
measurement is sensitive, and giving the owner the A/B (`ab_beach_*_seamOn_vs_off.png`). The
open-sea render with the seam enabled is **pixel-identical** (diff count 0) to the un-seamed render
at the event moment. Numbers: `Evidence~/proof-log.txt`.

## Two harness traps (spike-measured; binding on all future off-screen tooling)

1. **No reversed-Z translation outside URP's frame.** Production shaders' `ZTest LEqual` (explicit
   or ShaderLab-default) silently kills every fragment against a raw cleared depth buffer in a
   hand-rolled command-buffer harness. Inside URP this is a non-issue; any editor tool that renders
   off-screen must **calibrate the depth convention at runtime** (`CalibrateDepth()` — both the
   spike and the seam harness do). Corollary from ADR 0022 phase 3: the production render-graph path
   uses plain `LEqual`; the `GEqual`/clear-0 convention belongs to raw harnesses only.
2. **The owner's `Water.mat` carries the baked St Peters height map.** In an abstract test viewport
   it reads as *land* and `clip()`s everything to background. Any harness that borrows the
   production material must force a uniform-deep height source (disable `_USE_HEIGHTTEX` **and**
   substitute a black height texture — belt and braces, both spike-proven necessary).

## Performance envelope (rule 7)

Spike-measured on the owner's RTX 4060 (D3D12, 960×540, CPU submit+flush — order-of-magnitude, not
GPU timestamps): a **4 px grid (0.125 m cell) is 43 k verts ≈ 0.6–3.9 ms** and visually sufficient;
2 px only marginally sharpens silhouettes at 4× the verts. The vertex stage is 4 sin/cos per vert;
the fragment stage is the same 4-train evaluation the flat water already pays per pixel. Well inside
the desktop 60 fps budget; the mobile-portability discipline says the production feature starts at
**8 px and lets crest-silhouette tolerance argue it down**. The seam adds one texture read + a
smoothstep per vertex (the height map the fragment stage already samples).

## Screen-anchored layers against a displaced surface (analysis — implementation in phase 4)

Layers that live **in the water fragment** (foam, caustics, swell-read, depth tint) ride the
displaced surface for free — they are painted on the mesh. Layers built from **screen-space
geometry** assume a flat sea and need review:

- **Moon glitter / reflection smear:** the camera-anchored moon column samples a fixed screen band.
  On a displaced surface the band should bend — glints riding crest tops, gaps in the troughs, and
  at ×1.5 a 1 m crest moves its glint 48 px. The honest options: (a) leave it screen-flat (cheap,
  visibly wrong over big swell), (b) sample the glitter in the water fragment so it inherits
  displacement (the fragment knows its own lift — likely right, needs the HDR-compensation care
  from the moon work), or (c) offset the screen band by the displaced height under it. The spike's
  read: bending "would probably be charming, needs eyes" — an art-pipeline call once phase 2 gives
  them a surface to look at.
- **Rain rings, drift lines, seaweed/flotsam sprites:** anchored to world positions on the flat
  plane today; they gain a `DisplacedHeight` read (the shared constant, again) to sit on the
  surface. Emitter-lane work, phase 4.

## Whitecap salience retune (analysis — implementation with phase 2)

Today's flat-water caps mark **every** local crest with equal salience — which is exactly what hides
the big one (the spike's control image: the 100%-envelope event sits in uniform speckle). The
retune: key cap density and core solidity on **envelope-relative** crest height (the field's
`CrestFactor` against a high threshold, the spike used 0.62 with a solid core margin), so ordinary
chop wears thin dithered streaks and near-envelope crests wear solid cores. Owner ruling folds this
INTO the arc (no flat-shader interim), so it lands as part of the displaced surface's fragment
stage, sharing the exact thresholds the spike tuned. Near shore, cap salience fades with the seam
(the dying edge must not wear open-sea caps); shore foam remains the production dressing's separate
layer.

## Phased build plan

Each phase independently shippable, `main` green throughout:

1. **Seam architecture** ← this PR. `Core.ShoreFadeMath` (pure, additive), the headless proof
   (`ShoreFadeMathTests`), the rendered proof + committed evidence (`ShoreSeamProof`), this ADR.
2. **The displaced surface in production.** The water mesh in the ADR 0022 off-screen pass + overlay
   quad; the vertex twin reads the painted height map for the seam; envelope-relative bands + the
   whitecap salience retune in the fragment stage; `GameConfig` exposure of the exaggeration and the
   band coefficient; A/B toggle against the flat water for the owner. Perf gate: 60 fps desktop,
   8 px grid start.
3. **Hull waterline + shared heave.** Boats' visual heave switches to `DisplacedHeight` (same
   constant, same fade — a boat nosing into the shallows settles as the water does); the hull
   waterline via the shared private z-buffer (the spike's probe, productionised). gameplay-systems
   decides whether seakeeping *forces* read the faded or unfaded height (see==feel vs feel-the-swell
   — flagged, not decided here).
4. **Salience/glitter retunes.** Moon glitter vs displacement (the analysis above), rain rings /
   drift lines / flotsam riding the surface, storm-lane and reflection review. Art-pipeline lane,
   owner eyes throughout.

## Acceptance bar

- **The t = 1513.5 s envelope event must be visibly readable in normal play** at the default
  exaggeration — pointable on screen the way the spike's annotated stills are, in the production
  scene with the production dressing. (The event is pinned by `ShoreFadeMathTests`, so if the wave
  model moves it, the pin moves loudly.)
- **The coast shows no tear at any tide level:** the rendered waterline tracks the sim's depth-0
  contour within 1 px at every tide, on every shore steepness, seam-off never ships. The walkable
  waterline (ADR 0009) and the clip contour remain byte-identical to today's.
- The style law (3) holds: bands + edge dither, owner palette, world-locked cells, zero crawl.
- 60 fps desktop budget held with the surface enabled (rule 7).

## Alternatives considered

- **Ship the whitecap/band retune in the flat shader first, defer 3D.** The spike recommended
  offering it; the owner ruled **no flat-shader interim** (2026-07-22) — the retune ships inside the
  arc.
- **Fade displacement by distance-to-shoreline instead of depth.** A second contour source (a
  distance field) that must be baked, re-baked when tide moves, and can drift from the walkability
  contour — the exact drift ADR 0010/0014 exist to forbid. Depth *is* the game's shore-proximity
  axis and is already in the shader.
- **Let waves shoal (grow then break) at the coast, like real surf.** Visually richer and initially
  attractive, but it moves pixels *past* the walkable waterline by design — P1 integrity cannot be
  proven for it, and the tear-safety bound stops existing. Shore surf can come back later as
  clip-safe *dressing* (foam, swash) on top of the faded surface, where it lives today.
- **Displace in the flat shader (parallax/UV warp) without a mesh.** No real silhouette, no
  occlusion, no shared z-buffer with hulls — the three cues that motivated the arc. Rejected.
- **A separate high-detail sea sim for the displaced surface.** Violates the one-sea rule; the boat
  would rock on a different sea than the player reads. Rejected outright.

## References

- Spike: branch `spike/3d-water` — `Assets/_Project/Code/Tools/Editor/Spike3dWater/VERDICT.md` and
  its `Evidence~/` (A/B stills, filmstrips, the waterline-on-hull probe, perf table).
- Seam proof: `Assets/_Project/Code/Core/Environment/ShoreFadeMath.cs`,
  `Assets/Tests/EditMode/ShoreFadeMathTests.cs`,
  `Assets/_Project/Code/Tools/Editor/ShoreSeamProof/` (+ `Evidence~/proof-log.txt`).
- The integration pattern: ADR 0022 § Open questions (1) — the MRT + overlay + private depth
  architecture, and `IsoFacetHullFeature`'s conventions.
