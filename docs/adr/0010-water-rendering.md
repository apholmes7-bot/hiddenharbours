# ADR 0010 — Water rendering: a layered, height-map-driven URP Shader Graph (pixelized), unified with the tide-walkability seam

- **Status:** **Accepted** — lead-architect. Records the **decision and plan** for the game's hero
  water look. This change is **docs only**: it ships no shader, no scene, no Core/builder change. The
  shader itself is built in the **art/rendering pass** — M1 **VS-24** (the §3.6 water + global-grade
  backbone) deepening into the **M2/M3** advanced-rendering work — **after** the St Peters mechanics
  prove fun and the placeholder pixel art is dropped (CLAUDE.md rule 8).
- **Date:** 2026-06-23
- **Decision owner:** lead-architect (the URP shader/rendering plumbing is a cross-cutting/architectural
  call — `agents/coordination.md` §1.1 "Water/fog/lighting" seam, §8; CLAUDE.md rule 4). art-pipeline
  owns the *look* (palette, foam/caustic/specular textures); the two tune together.
- **Flagged from:** the owner picking a **target look** for water, referencing a Unity URP Shader Graph
  water tutorial — a **main water shader** composed from **caustic, specular, and sea-foam subgraphs**.
  We record the plan now so the later art pass builds *toward* a known target instead of improvising.
- **Related:** `design/water-rendering.md` (the layer-by-layer build recipe this ADR decides),
  `design/art-and-audio-bible.md` §2.1/§2.2 (tide-aware shoreline, walkable seabed), §3.5 ("Water is a
  first-class P1 system" — the surface spec), §3.6 (water backbone), §4 (palette), §6.1 (advanced-
  rendering roadmap — now points here), `design/time-tides-weather.md` §3.5 (the water-depth rule),
  §5.1 (the forward-additive `EnvironmentSample` reshape) and OQ1 (tide→visual-cue mapping — this ADR
  resolves the rendering side); `0009-tidal-exposure-and-region-display-name-seams.md`
  (`Core.TidalExposure` + `IEnvironmentService.WaterLevelAt` — the shared height-map rule this shader
  reuses), `0006-boat-art-pipeline.md` (the author-in-3D/ship-as-2D, one-implied-light precedent),
  `0001-engine-choice.md` (2D URP), `0005-pc-first-target.md` (the 60fps desktop budget, mobile-portable).

## Context

Water is a **first-class P1 system** ("The Sea Has Moods"), not a backdrop (art-bible §3.5). The owner
has now chosen a concrete target: a **layered URP Shader Graph** built like the referenced tutorial —
a main water shader assembled from **subgraphs** for caustics, specular, and sea-foam, over a
depth-driven base. Three forces shape how we adopt it:

1. **We are pixel-art, not smooth 3D water.** Everything renders at **PPU = 32** with one constant
   scale (art-bible §3.1). A photoreal URP water surface would fight the look. The shader must read as
   *pixel art*.
2. **The waterline is gameplay, and it moves.** The shoreline is **tide-driven**: water *extent*
   follows `WaterLevel`, the headline P1 visual (art-bible §2.1/§2.2, §3.6). The St Peters opening
   leans on a falling tide baring a **sandbar** the player walks across (canon §5.8;
   world-and-regions §6.0/§7). So the water render and the tide *gameplay* must agree on "submerged or
   exposed here, now?" — they cannot drift, or what the player *sees* and what the physics *does* part
   ways (the P1 integrity rule, art-bible §2.2 close).
3. **It's later-phase, and shared.** The shader is M1 VS-24 → M2/M3 work, owned across two lanes
   (lead-architect: shader-graph plumbing; art-pipeline: look). Building it now would be scope creep
   (rule 8) and waste effort while placeholder art is still in.

ADR 0009 already shipped the Core seam that #2 needs: `IEnvironmentService.WaterLevelAt(t)` (the
deterministic water surface, metres above chart datum, recomputed from `(worldSeed, gameTime)`, never
saved) and `Core.TidalExposure` (`WaterDepth`/`IsExposed`/`IsSubmerged` over an authored terrain
elevation). What was unrecorded is **how the water *render* consumes the same data** — which this ADR
fixes.

## Decision

**(1) Water is a layered URP Shader Graph, not frame-by-frame animation, built foundation → polish.**
Five layers, each a subgraph where the tutorial uses one (`design/water-rendering.md` is the recipe):

1. **Depth gradient** — base colour ramped shallow → deep from **water depth**, where
   `depth = waterLevel − terrainHeight` (the same arithmetic as `TidalExposure.WaterDepth`).
2. **Surface distortion** — animated scrolling perlin/value-noise × time for swell and surface motion.
3. **Sea-foam fringe** — a foam texture **masked by a blurred-edge / depth≈0 band** so foam hugs the
   *moving* waterline (where the gradient's depth crosses zero), not a fixed painted edge.
4. **Specular** — sun/sky highlights glinting on the surface.
5. **Caustics** — perlin × time light-ripple in the **shallows** (depth-gated), the shimmer over a
   visible seabed.

**(2) Pixel-art fidelity is mandatory.** Every layer/subgraph **pixelizes world coordinates**
(`Position → Multiply → Floor → Divide`) so noise, foam, specular and caustics all snap to the PPU=32
pixel grid and the surface reads as pixel art, not smooth 3D water. This is non-negotiable, holds the
LOCKED §2/§3 rules, and is the single rule the art pass cannot skip.

**(3) The height map is the one shared source of truth — water render *and* tide gameplay read it.**
A single **height map** (per-region terrain elevation, metres above chart datum) feeds **three**
consumers off the *same* data:
- **water rendering** (this shader) — the depth gradient and foam band;
- **tide walkability** — `Core.TidalExposure.IsExposed(WaterLevelAt(t), terrainElevation)` / the
  `IEnvironmentService.WaterLevelAt` seam from ADR 0009 / #59 (the on-foot walkability sim);
- **boat-cross** — "deep enough = passable" (boat draught vs `WaterDepth`).

`depth = waterLevel − terrainHeight` everywhere. The St Peters **sandbar is just a low ridge in the
height map**: as the deterministic tide falls, its `depth` crosses zero, the shader's foam band sweeps
across it, and the *same* zero-crossing makes it walkable — render and sim cannot disagree because they
read one number. This generalizes the canon **seabed-elevation / bathymetry heightfield** (the single
source of truth named in time-tides-weather §3.5, §5.1) to **all** terrain (land above datum included),
and resolves the rendering half of that doc's **OQ1** (tide→visual-cue mapping).

**(4) Static terrain edges are Rule Tiles; the live moving waterline + foam live in the shader.**
Terrain-*type* boundaries that don't move (grass↔sand↔rock) are authored with **Rule Tiles** (the
existing autotile approach). The **waterline and its foam move with `WaterLevel`** every tide, so they
live in the **shader's depth≈0 band** — *not* re-stamped into tiles per frame (that would be per-frame
authoring churn and would fork the truth away from the height map). Static type-edges = tiles; the live
wet edge = shader.

**(5) Ownership & phasing.**
- **lead-architect** owns the **URP Shader Graph plumbing** (the layer/subgraph structure, the
  height-map sampling, the pixelize node pattern, the `WaterLevelAt` hookup).
- **art-pipeline** owns the **look** (palette per §4, foam/caustic/specular textures, tuning).
- **Now (in the St Peters greybox, gameplay-relevant):** the **height map** + a **flat depth-tint**
  (shallow→deep colour, no animation) fold in, because the height map *is* the walkability data and a
  readable depth tint helps the fun-check. This is world/gameplay greybox work on the ADR-0009 seam —
  **not** the shader.
- **Later (M1 VS-24 → M2/M3 advanced rendering, after mechanics + art-drop):** the full five-layer
  shader slots onto the *same* height-map data. No data migration — the shader is a new *consumer* of
  an existing field.

**No code, no shader assets, no scene/Core/builder change ship with this ADR.** It records intent.

### Determinism & save (the invariant guarded)

The water render is a **pure function** of the deterministic `WaterLevelAt(t)` (recomputed from
`(worldSeed, gameTime)`, never saved — CLAUDE.md rule 5) and the **authored, read-only** height map.
Surface/caustic *animation* is driven by `time` for visual motion only and feeds **no** simulation —
it never influences walkability, grounding, or any saved state. Nothing about water rendering enters
the save (ADR 0008); the height map is authored content, not save state.

### Performance posture (planned, validated at build time)

Targets the 60fps desktop budget, mobile-portable (ADR 0005): a small fixed set of texture samples +
noise per pixel, **no per-frame CPU allocation** (the shader samples `WaterLevelAt` as a material
float, set on tide change / slow tick, not rebuilt per frame), pooled/static materials. The
tutorial-style runtime shader vs the M3 **3D-water→2D bake** (art-bible §6.1, mirroring the ADR 0006
boat bake) is decided by a **profiled spike** in the art pass (extends art-bible **OQ2**) — this ADR
does not pre-commit that fork.

## Consequences

- **One source of truth for the shoreline.** Render, on-foot walkability, and boat-cross all read the
  height map through `depth = waterLevel − terrainHeight`; the visible waterline and the playable
  shoreline are the *same* line by construction (P1 integrity). The St Peters sandbar "just works" as a
  low ridge.
- **Zero new coupling, zero new Core surface.** The shader reuses the **already-shipped** ADR-0009
  seam (`WaterLevelAt`, `TidalExposure`); no new interface, signal, or save field is needed for the
  plan. (Any later helper — e.g. a per-position height-map *sampler* in Core — would be its own
  additive ADR when the build needs it.)
- **Pixel look preserved.** The pixelize-world-coords rule keeps URP water reading as PPU=32 pixel art.
- **In-phase.** Nothing is built now beyond the greybox height map + flat tint (gameplay-relevant);
  the shader waits for VS-24/M2/M3 and the art-drop, per rule 8.
- **Docs in the same change:** this ADR + `design/water-rendering.md` (the recipe) + the art-bible §6.1
  pointer, per the lead-architect DoD.

## Rejected alternatives

- **Hand-pixelled / frame-by-frame animated water tiles.** Doesn't scale to sea-state-driven amplitude,
  reflections, and a *moving* tide waterline; the owner's chosen target is the layered shader. Tiles
  stay only for **static** terrain-type edges (decision (4)).
- **A photoreal (non-pixelized) URP water surface** straight from the tutorial. Fights the LOCKED
  pixel-art look (§2/§3). The pixelize-coords rule (decision (2)) is what reconciles the tutorial
  technique with our art direction.
- **Re-stamping the waterline/foam into the tilemap each tide tick.** Per-frame authoring churn, and it
  forks the shoreline truth away from the height map. The live edge belongs in the shader's depth≈0
  band; tiles carry only static type-edges.
- **Separate height fields for rendering vs gameplay** (a "visual" seabed and a "physics" seabed). The
  exact drift ADR 0009 exists to prevent — one height map, three consumers.
- **Saving the water/foam state.** Violates rule 5 (recompute from seed+time); the render is a pure
  function of `WaterLevelAt(t)` + the authored height map.
- **Building the shader now.** Scope creep (rule 8) and wasted effort while placeholder art is in and
  the St Peters loop is unproven. Plan now; build in the art pass.

## Open questions (later, for the owning lanes / the art pass)

- **Height-map authoring source & Core sampler.** ADR 0009 takes `terrainElevation` as a *caller-
  supplied* parameter; how the world authors it per position (tile heightfield texture vs per-feature
  zones — world-and-regions §9.4, time-tides-weather §3.5) and whether the shader and sim should share
  a Core-owned **per-position sampler** is a build-time call (its own additive ADR if needed).
- **Runtime shader vs M3 3D-water→2D bake.** Decided by a profiled spike in the art pass (art-bible
  §6.1, OQ2). The layer recipe in `design/water-rendering.md` applies either way.
- **Per-region water plane offset.** A region that offsets its local water plane from raw tide height
  overrides `IEnvironmentService.WaterLevelAt` (the ADR-0009 hook); the shader reads whatever that
  returns — no shader change needed.
- **Foam-band width / depth thresholds, palette ramps, caustic intensity.** Tunables, owned by
  art-pipeline; exposed as material/Def values per the no-magic-numbers rule (CLAUDE.md rule 6), not
  hard-coded in the graph.

## Addendum — shipped art-pass tunables (anti-tiling + beach swash)

Two visual fixes shipped on `HiddenHarboursWater.shader` after the owner saw the painted-texture pass
running live (`design/water-rendering.md` §5.6 documents the mechanism). Both are **visual-only** — they
drive no sim, save nothing, and read the *same* deterministic depth (the invariant above holds):

- **Anti-tiling** — `_UntileStrength` (0..1, default 0.6, ON). An IQ-style hash-untile + domain warp on
  the scrolling painted slots (`_SurfaceTex`/`_FoamTex`/`_CausticTex`/`_SparkleTex`) so the small painted
  tile's repeat grid stops reading at CALM. Kept pixel-snapped (the offset is applied to the world coord
  before the pixelize), so the pixel-art rule (decision (2)) still holds.
- **Always-on beach swash** — `_SwashAmplitude` (m, default 0.3), `_SwashSpeed` (default 0.5),
  `_SwashScale` (default 0.25). A fast `_Time`-driven sine that advances/recedes the **foam fringe**
  continuously (the "waves in and out" the slow tide alone didn't give). **Confined to the depth≈0 foam
  band** by a gate and applied only to a *local foam-only depth* — the real `depth` driving `clip()`, the
  deep tint, and the caustic gate is never touched, so the cosmetic wash **cannot move the gameplay
  waterline** (the P1 integrity rule / determinism invariant above). Its math has a C# twin
  (`WaterSurface.SwashOffset`/`SwashBandGate`) unit-tested headless for the band-confinement invariant.

## Addendum — shipped art-pass tunables (wind direction + syncopation + FBM variance)

A third visual upgrade shipped on `HiddenHarboursWater.shader` after the owner saw the surface "stay
organized in a pattern" and "march one direction." **Root cause (a shader/sim split):** the sim's wind
*already varies direction over time* (`WeatherModel.SampleWind` — prevailing-wander + gust veer), but the
shader **discarded** wind direction — it scrolled *every* animated layer along `_FlowDir` (the tidal
**current**, a fixed axis) and used wind only as the scalar `_Roughness`. The whole sea therefore slid
down one axis regardless of weather. The fix makes the surface follow the **wind** (intensity **and**
direction), adds multi-rate/multi-direction wave octaves (syncopation), and adds organic low-frequency
variance (FBM) that also scatters the specular sparkles. Mechanism: `design/water-rendering.md` §5.7.

Like the swash, **all of it is visual-only** — it touches only `col.rgb` / the foam dressing and **never**
`depth`, `clip()`, the deep-tint, the caustic gate, or `_WaterLevel`; it drives no sim and saves nothing
(the determinism / P1-integrity invariant above holds). Every new constant is a material property
(rule 6), and every new octave/field is **pixelized** (decision (2) holds). The new layers default **ON
at a modest strength** so the change is visible immediately yet fully dial-able on `Water.mat`.

- **Wind direction pushed** — new `_WindDir` (Vector) + a `WaterSurface.WindDirection(windVector)` pure
  static helper mirroring `FlowDirection` (normalize; fall back to `Vector2.up` on near-zero wind,
  NaN-safe), pushed in `PushUniforms` right after `_FlowDir`. Headless EditMode-tested
  (`WindDirection_FollowsTheWind_NormalizesAndFallsBackOnSlackWind`). Wind *strength* still drives
  `_Roughness` separately; only the **direction** is added.
- **Wind chop octave** — `_WindChop` (0.4), `_WindChopScale` (0.7), `_WindChopSpeed` (0.09): a value-noise
  octave scrolled along `_WindDir` at its own rate (a separate scroll from `_FlowDir`).
- **Syncopation** — `SurfaceNoise` refactored to 3 octaves on distinct (direction, rate): current swell
  (`_FlowDir`/`_Flow`), wind chop (`_WindDir`/`_WindChopSpeed`), and a slow perpendicular cross-swell
  (`_CrossSwellDir` — `(0,0)` = auto-perpendicular — `_CrossSwellSpeed` 0.025, `_CrossSwellScale` 0.16),
  mixed by `_Octave2Weight` (0.35) / `_Octave3Weight` (0.3).
- **FBM low-freq variance** — a new `Fbm()` field (`_FbmScale` 0.05, `_FbmDriftSpeed` 0.012) that (i)
  tints `col.rgb` toward `_FbmTint` by `_FbmStrength` (0.18) for broad slow patches, and (ii) **gates the
  specular** by `smoothstep(_FbmGateLo 0.35, _FbmGateHi 0.7, fbm)` so sparkles cluster organically; the
  hard 4-step glint posterize is now the tunable `_SpecBands` (4).

To calm the look back toward the old single-direction surface, set `_WindChop` / `_Octave2Weight` /
`_Octave3Weight` / `_FbmStrength` to 0 on `Water.mat`.

## Addendum — shipped art-pass tunables (cohesion pass: rolling swell + wind-streaked foam + flow-with-body)

A fourth visual upgrade shipped on `HiddenHarboursWater.shader` after the owner saw the §5.7 surface read as
a **field of separate specks** rather than **one large body**, and noticed the foam/whitecap layers scrolling
on a diagonal **opposite** to the surface (`float2(-t*_Flow, t*_Flow)`). This **cohesion pass** adds three
coupled layers so the sea reads as one connected, rolling body. Mechanism: `design/water-rendering.md` §5.8.

Like every prior addendum, **all of it is visual-only** — it touches only `col.rgb` / the foam dressing and
**never** `depth`, `clip()`, the deep-tint, the caustic gate, or `_WaterLevel`; it drives no sim and saves
nothing (the determinism / P1-integrity invariant above holds). Every new constant is a material property
(rule 6), every new field is **pixelized** (decision (2)), and the new layers default **ON at a modest
strength** so the cohesion is visible immediately yet fully dial-able on `Water.mat`. Crucially, **everything
keys off the LIVE, time-wandering sim directions** — the wind (`_WindDir`, `WeatherModel`) and the tidal
current (`_FlowDir`, the PR #95 drifting current bearing), both already pushed by `WaterSurface.cs` — so the
whole body **reorients as the weather shifts** (the P1 "sea has moods" integrity); no angle is hardcoded.

- **Rolling ocean swell (the keystone)** — one big, long-wavelength swell field (`SwellField`) over worldXY
  whose 0..1 crest factor modulates the **base-colour brightness** (crests lighter, troughs darker) so broad
  light/dark **bands roll across the WHOLE surface**, the small variance riding on top. Direction
  defaults to the wandering **wind** (`SwellDir()`; `_OceanSwellDir (0,0)` = auto-from-`_WindDir`, override
  optional). Props: `_OceanSwellDir`, `_OceanSwellScale` (0.025, small = long wavelength), `_OceanSwellSpeed`
  (0.018), `_OceanSwellStrength` (0.16), `_OceanSwellSharpness` (1.4).
- **Wind-streaked foam (wind rows)** — the open-water whitecap speckle is sampled on a coord **compressed
  perpendicular to `_WindDir`** so features **elongate into streaks ALONG the wind**, anisotropic instead of
  round speckle. Prop: `_FoamStreakStretch` (3.5; 1 = round). The existing wind/roughness + deep-water gating
  is unchanged.
- **Coupling + the opposite-motion fix** — the **same** swell field rides the whitecaps on the swell crests
  (`_FoamCrestGate` 0.6) and biases the specular toward the lit swell faces (`_SpecSwellBias` 0.35, still the
  one implied sun). The foam churn + whitecap scroll's old fixed counter-diagonal is **replaced** by a drift
  along a **blend of the wind and the tidal current** (`_FoamDriftWindVsCurrent` 0.6: 0 = current-led, 1 =
  wind-led) so the foam flows **with** the body and reorients with the weather.

**No new C# uniform.** The swell axis and foam-drift axis are derived **in-shader** from the already-pushed
`_WindDir`/`_FlowDir`; `WaterSurface.cs` gains only **non-pushed pure twins** (`SwellDirection`,
`FoamDriftDirection`) so the direction logic is unit-tested headless (`ArtRenderingTests.cs`) — the
determinism guard for the reorientation. The CI shader-compile guard (`WaterShaderCompileGuardTests.cs`)
continues to force-compile the shipped `Water.mat` variant: no `+` in any `[Header]`/Property string, no
`[unroll]` over a runtime bound (the magenta class stays guarded).

To dissolve the cohesion back toward the §5.7 look, set `_OceanSwellStrength` / `_FoamCrestGate` /
`_SpecSwellBias` to 0 and `_FoamStreakStretch` to 1 on `Water.mat`.

## Addendum — shipped art-pass tunables (living foam: evolving field + soft (metaball) threshold)

A fifth visual upgrade shipped on `HiddenHarboursWater.shader` after the owner saw the open-water whitecaps
(and the foam-fringe churn) read as a **repeating pattern** whose shapes **never change**. **Root cause:** the
foam was a **fixed-shape noise stamp that only TRANSLATED** — a single `ValueNoise` sample scrolled by the
foam-drift, masked by a **hard `step()`**. A sliding stamp under a hard cut is, by construction, a sliding
repeat; the blobs could never change shape, merge, or separate. This pass makes the foam **EVOLVE, not just
translate**. Mechanism: `design/water-rendering.md` §5.9.

Like every prior addendum, **all of it is visual-only** — it touches only `col.rgb` / `col.a` (the foam blend)
and **never** `depth`, `clip()`, the deep-tint, the caustic gate, or `_WaterLevel`; it drives no sim and saves
nothing (the determinism / P1-integrity invariant above holds). Every new constant is a material property
(rule 6), every new field is **pixelized** (decision (2)), and the new layers default **ON at a modest
strength** so the change is visible immediately yet fully dial-able on `Water.mat`. **Crucially the foam still
travels with the weather** — the in-place evolution is layered ON TOP of the existing wind+current foam drift
(`_FoamDriftWindVsCurrent`, both axes sim-driven), so it morphs *and* flows; the evolution is added, not a
replacement.

- **Evolving field (the keystone)** — a new in-shader pseudo-3D value-noise helper (`EvolvingField`) replaces
  the single translating `ValueNoise` for both the whitecaps and the fringe churn. It blends two **time-offset**
  `ValueNoise` samples of the **same** coord with an **animated mix** (two boil pairs, half a step out of phase,
  smoothly crossfaded → continuous + seamless), so maxima rise/fall **in place**: bright spots appear, grow,
  drift, shrink, vanish. Props: `_FoamEvolveSpeed` (0.25 — boil/morph rate, 0 = frozen shapes), `_FoamBlobScale`
  (2.2 — blob size).
- **Merge / separate via a soft threshold** — the foam mask becomes
  `smoothstep(_FoamThreshold − _FoamThresholdSoft, _FoamThreshold + _FoamThresholdSoft, field)` instead of a
  hard `step`. The soft band is the metaball mechanism: a rising valley between two maxima crosses the lower
  edge and the blobs **MERGE**; a dipping field between them **SEPARATES** them; a maximum crossing the band
  **fades** in/out. Props: `_FoamThreshold` (0.55), `_FoamThresholdSoft` (0.18). The swell-crest gate
  (`_FoamCrestGate`), wind-streak stretch (`_FoamStreakStretch`), and wind-roughness threshold-lowering all keep
  working on top.
- **Kill the residual repeat** — the procedural value-noise is hash-based (non-tiling), but the painted
  `_WhitecapTex` slot (ON) was the one scrolling slot still sampled through a plain `PaintUV` (skipping the
  anti-tiling path). It is now routed through the existing **`UntileSampleW`** (IQ-style hash-untile + domain
  warp, dialed by `_UntileStrength`), like every other painted slot — kept pixel-snapped.

**No new C# uniform.** The evolving field and the soft threshold are derived **in-shader** off `_Time` and the
already-pushed `_WindDir`/`_FlowDir`; `WaterSurface.cs` pushes nothing new. The GPU value-noise field can't be
unit-tested headless, but the **soft-threshold math** — the part that produces the merge/separate behaviour —
gains **non-pushed pure twins** (`FoamSoftThreshold` + a general `Smoothstep`) so the mechanism is unit-tested
headless (`ArtRenderingTests.cs`: the soft band is partial coverage not a 0/1 step; monotonic in the field; a
risen valley between two maxima fills in = MERGE, a low valley reads bare = SEPARATE). The CI shader-compile
guard (`WaterShaderCompileGuardTests.cs`) continues to force-compile the shipped `Water.mat` variant: no `+` in
any `[Header]`/Property string, no `[unroll]` over a runtime bound (the magenta class stays guarded).

To revert toward the old translating-stamp look, set `_FoamEvolveSpeed` to 0 (shapes stop morphing, foam only
drifts) and `_FoamThresholdSoft` small (toward a hard edge) on `Water.mat`.

## Addendum — shipped art-pass tunables (foam density + whitecap lifecycle: dense solid core, condition-driven, born-on-the-crest)

A sixth visual upgrade shipped on `HiddenHarboursWater.shader` after the owner saw the §5.9 soft (metaball)
threshold read as **MILKY EVERYWHERE** — it lost the **dense, solid-white** whitecaps the painted `_FoamTex`
gave. The milky look is accurate for **calm / dissipating** foam, but a **building / rough** sea needs solid
density; the owner also wanted a natural **wave lifecycle** (foam **forms** as waves build → peaks into dense
**whitecaps** → **collapses** / dissipates). This pass is **additive on #100/#101** — it **keeps** their
evolving-field merge/separate + the milky soft fade as the **LIGHT/dissipating end**, and makes the density
**condition-appropriate**. Mechanism: `design/water-rendering.md` §5.11.

Like every prior addendum, **all of it is visual-only** — it touches only `col.rgb` / `col.a` (the foam
dressing) and **never** `depth`, `clip()`, the deep-tint, the caustic gate, or `_WaterLevel`; it drives no sim
and saves nothing (the determinism / P1-integrity invariant above holds). Every new constant is a material
property (rule 6), every field stays **pixelized** (decision (2)), the new levers default **ON at a modest
strength**, fully dial-able on `Water.mat`. **No new C# uniform** — the three shaping functions derive
in-shader from the already-pushed `_Roughness`/`_WindDir`/`_FlowDir` + `_Time`; `WaterSurface.cs` is untouched.

- **Dual-zone density (restore the solid core)** — a new in-shader `SolidCore(field, thr, density)` lifts the
  foam coverage to **FULL opacity** where the evolving field is **WELL above** the threshold (above a new
  `_FoamSolidThreshold`, sitting **above** `_FoamThreshold`), leveraging the painted solid-white `_FoamTex` at
  the **dense heart**, while the #101 `smoothstep` soft band survives **only near the threshold boundary** (the
  milky soft edge). Result: a dense solid heart + soft milky edges, not milky-everywhere. Applied to **both**
  the shoreline foam fringe and the open-water whitecaps. Props: `_FoamSolidThreshold` (0.78).
- **Condition-driven density** — a master `_FoamDensity` raised by wind via `_FoamDensityWind` × the existing
  `_Roughness` (`FoamDensity()` → `saturate(_FoamDensity + _Roughness · _FoamDensityWind)`). Density both
  **lifts** the solid-core opacity and **widens** the solid zone (slides the effective solid level down toward
  the threshold as the sea roughens), so **CALM → sparse + milky** and **ROUGH → dense, solid, widespread
  whitecaps** — automatically, with the weather. Props: `_FoamDensity` (0.6), `_FoamDensityWind` (0.5).
- **Wave lifecycle (form → whitecap → collapse)** — `WhitecapLifecycle(crest, density)` ties the open-water
  whitecaps to the **rolling-swell crest factor** (`SwellField`, §5.8 — reused, no new field): a cap is **BORN
  dense & solid on the breaking crest** (a sharp break band at the crest top, narrowed by
  `_WhitecapFormSharpness`, at `_WhitecapPeakDensity` opacity — which **replaces the old hard `0.6` cap
  ceiling**), then **AGES into milky residual** as the crest passes (`crest^_WhitecapCollapseRate` decays the
  solid lift), the residual **spreading downwind** via the existing `_FoamStreakStretch`. Off-crest only the
  milky soft mask remains (the dissipating look). A separate axis from `_FoamCrestGate` (which gates *where*
  caps appear — the lifecycle shapes *how dense* they are). Props: `_WhitecapFormSharpness` (0.5),
  `_WhitecapPeakDensity` (0.95), `_WhitecapCollapseRate` (1.5).

The evolving foam FIELD is GPU value-noise (not unit-testable headless), but the three shaping functions are
pure functions of the uniforms + the crest factor, mirrored as C# twins and unit-tested headless
(`Assets/Tests/EditMode/Art/FoamDensityLifecycleTests.cs`): wind raises density and saturates; the solid core
is 0 near the threshold and 1 well above it yet **always keeps a milky band** (the dual zone — never all-milky
nor all-solid); density **widens** the solid zone; the lifecycle **peaks on the breaking crest, collapses in
the trough**, ages monotonically off-crest, and **density gates the whole solid look** (calm = milky
everywhere, rough = dense crests). The CI shader-compile guard (`WaterShaderCompileGuardTests.cs`) continues to
force-compile the shipped `Water.mat` variant: no `+` in any `[Header]`/Property string, no `[unroll]` over a
runtime bound (the magenta class stays guarded).

To revert toward the §5.9 milky-everywhere look, set `_FoamDensity` to 0 and `_WhitecapPeakDensity` to ~0.6
(the old cap ceiling) on `Water.mat` — the merge/separate soft mask is then the whole look again.

## Addendum — shipped art-pass tunables (shoreward swell + foam bias: waves roll IN near the coast)

A seventh visual upgrade shipped on `HiddenHarboursWater.shader` after the owner reported the sea's surface
artifacts / movement / foam appearing to **ORIGINATE AT THE SHORELINE and travel OUTWARD to sea** — "foam blowing
out of the sand." **Root cause (a shader/sim direction split):** the §5.8 cohesion pass keyed the rolling
**swell** axis (`SwellDir()`) and the **foam-drift** axis (`FoamDriftDir()`) off the **wind** (and tidal
current), both of which **wander over time** (PR #95/#96 drift) and blow **offshore part of the time** — so when
the wind pointed land→sea the swell crest bands + the near-shore foam streamed **OUT** from the beach. Real swell
is generated far offshore and rolls **shoreward regardless of the local wind**; foam at the wet edge runs **up**
the beach. Mechanism: `design/water-rendering.md` §5.12.

Like every prior addendum, **all of it is visual-only** — it steers only the swell-brightness bands + the
foam/whitecap dressing and **never** `depth`, `clip()`, the deep-tint, the caustic gate, or `_WaterLevel`; it
drives no sim and saves nothing (the determinism / P1-integrity invariant above holds). The shoreward direction
is **READ** from the **same** baked height map the depth/foam already consume (one source of truth, ADR 0009/0010)
— it is a purely visual steer for the swell/foam layers; it does **not** change what the sim reads. Every new
constant is a material property (rule 6), the gradient sampling stays **pixelized** (decision (2)), and the bias
defaults **ON at a modest strength**, fully dial-able on `Water.mat`. **The open sea is unchanged** — the bias
fades to nothing past a tunable falloff depth, so deep water keeps the §5.8 wind-driven cohesion.

- **Shore direction from the height gradient (`ShoreDir`)** — the seabed elevation rises toward land, so the
  **gradient** of elevation points toward shallower water = toward the shore. Sampled per-pixel via a central
  difference of `SeabedElevation` (the baked `_HeightTex`) at `± _ShoreSampleStep`; returns `(0,0)` on a flat
  seabed / when no height map is baked, so an open-water region (no `TidalTerrain`) keeps the pure wind/current
  direction (no behaviour change there).
- **Near-shore weight + bias** — `ShorewardWeight(depth)` is full (= `_ShorewardBias`) at the wet edge, fading to
  0 by `_ShorewardFalloff` metres deep; `BiasTowardShore` lerps the swell/foam axis toward `ShoreDir` by that
  weight (re-normalized, NaN-safe). `SwellField` and both foam-drift call sites pass the per-pixel `depth`, so the
  crest bands advance toward the beach and the foam runs up the shore near the coast while the open sea is untouched.

**No new C# uniform.** The shore direction is derived **in-shader** from the already-baked `_HeightTex`;
`WaterSurface.cs` pushes nothing new. The GPU height-gradient sampling can't be unit-tested headless, but the
**direction-blend + the near-shore weight** — the part that produces the roll-in — gain **non-pushed pure twins**
(`WaterSurface.ShorewardWeight` + `WaterSurface.BiasTowardShore`), unit-tested headless (`ArtRenderingTests.cs`:
the weight is full at the edge / zero past the falloff / monotonic / bias-0 + zero-falloff safe; the blend steers
toward the shore by the weight, keeps the base axis when there is no shore direction (open water), and is NaN-safe
on opposed directions). The CI shader-compile guard (`WaterShaderCompileGuardTests.cs`) continues to force-compile
the shipped `Water.mat` variant: no `+` in any `[Header]`/Property string, no `[unroll]` over a runtime bound (the
magenta class stays guarded).

**New tunables (additive — none of the owner's existing tuned values changed):** `_ShorewardBias` (0.7, master
strength; **0 = old wind-led behaviour**), `_ShorewardFalloff` (2.5 m, depth over which the bias fades to none),
`_ShoreSampleStep` (0.4 m, the gradient sample step). To revert to the §5.8 pure wind/current cohesion everywhere,
set `_ShorewardBias` to 0 on `Water.mat`.

## Addendum — shipped art-pass tunables (sky reflections: strong+sharp on CALM, gone in a storm)

An eighth visual upgrade shipped on `HiddenHarboursWater.shader`: the sea now **reflects the sky**. On
**CALM / glassy** water it adds a clean, mirror-like sheen — the **current sky colour** smeared down the
surface as a vertical-ish band (the stylized pixel-art mirror cue) plus a **brighter sun streak/glitter**
toward the sun. As the **sea-state** rises the reflection's **sharpness drops** (it smears/scatters across
the chop) and its **strength falls**, reaching **~0 by a tunable sea-state** (a storm doesn't mirror), with
wind dimming/scattering it further. So **calm → strong + sharp, lively → broken + dim, gale → gone** — the
reflection *is* a P1 "sea has moods" tell. Mechanism: `design/water-rendering.md` §11.

**Stylized single-pass, in-shader — NO reflection camera / NO extra render pass.** A real planar-reflection
pass would need a second camera + render target wired into the 2D URP renderer (unverifiable here) and a
second draw of the scene — both outside the rule-7 60fps budget and the pixel-art look. Instead the
"reflection" is the sky colour **faked** as a vertical smear + sun-aligned glitter, pixelized on the PPU
grid like every other layer. Rejected: a true reflection-camera/RT pass (perf + wiring) and a baked cubemap
(static, can't track the day/night sky).

Like every prior addendum, **all of it is visual-only** — it adds to `col.rgb` like every other water layer
and **never** touches `depth`, `clip()`, the deep tint, the caustic gate, or `_WaterLevel`; it drives no sim
and saves nothing (the determinism / P1-integrity invariant above holds). It composites **after** the
caustics + specular but **before** the foam (whitecaps read on top of the mirror). Every new constant is a
material property (rule 6), the layer stays **pixelized** (decision (2)), and it defaults **ON at a modest
strength** so it reads immediately yet `_ReflectionStrength = 0` returns the exact pre-feature look.

- **Sea-state drives it (NO new C# uniform).** The calm↔stormy behaviour reads the **already-pushed**
  `_Chop` (0 glass .. 1 storm; `WaterSurface` sets it from `Choppiness(SeaState)`) and `_Roughness` (wind
  whitecaps). Two in-shader curves — `ReflectionStrength()` (full on glass, `1 − smoothstep(0,
  _ReflectionFadeChop, _Chop)`, wind-dimmed, master-scaled) and `ReflectionSharpness()` (mirror at calm,
  smeared by `_Chop·_ReflectionChopScatter + _Roughness·_ReflectionWindScatter`) — shape the smear width +
  opacity. `WaterSurface.cs` is **untouched** (no new uniform push).
- **Reflects the CURRENT sky (day/night).** The reflected colour is the **day/night `_DayNightTint`**
  global (ADR 0013) — warm at dusk, dark at night, bright at noon — dialed by `_ReflectionSkyTint` against
  the authored `_ReflectionColor`; the **sun streak** sits toward `_SunDir` and fades with `_SunElevation`.
  `_DayNightTint` + `_SunElevation` are declared as **read-only globals** in the shader (alongside the
  existing `_SunDir`); with the cycle off (near-black global / `_SunDir == 0`) it falls back to
  `_ReflectionColor` and treats the sun as up (mirroring the specular's existing fallback) — never a black
  sky from an unset global.

**New tunables (additive — none of the owner's existing tuned values changed):** `_ReflectionStrength`
(0.6, master; **0 = off / today's look**), `_ReflectionFadeChop` (0.6, the `_Chop` where it is gone),
`_ReflectionWindFade` (0.5, wind dim), `_ReflectionChopScatter` (1.5) / `_ReflectionWindScatter` (0.8, the
chop/wind smear), `_ReflectionSkyTint` (0.85, live-sky weight), `_ReflectionColor` (pale sky, cycle-off
fallback), `_ReflectionSmear` (1.6 m, calm smear length), `_ReflectionSunStreak` (0.9) / `_ReflectionSunSharp`
(6.0, the sun streak). To turn reflections OFF (the pre-feature look), set `_ReflectionStrength` to 0.

The reflection FIELD is GPU value-noise (not unit-testable headless), but the **sea-state response curves**
are pure functions mirrored as C# twins in a **new** `Assets/_Project/Code/Art/WaterReflection.cs`
(`ReflectionStrength` / `ReflectionSharpness`, reusing `WaterSurface.Smoothstep`) and unit-tested headless
(`Assets/Tests/EditMode/Art/WaterReflectionTests.cs`): strong on glass, monotonic fade to 0 by the
fade-chop and stays gone, wind dims further, master 0 = off at every sea-state; sharpness is a mirror at
calm and smears monotonically toward 0, clamped. The CI shader-compile guard
(`WaterShaderCompileGuardTests.cs`) continues to force-compile the shipped `Water.mat` variant: no `+` in
any `[Header]`/Property string, no `[unroll]` over a runtime bound (the magenta class stays guarded).
