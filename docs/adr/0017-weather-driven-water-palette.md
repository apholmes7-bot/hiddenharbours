# ADR 0017 — Weather-driven water palette: the deterministic weather eases the sea's MOOD through the preset library (a 2-axis blend, MPB-only, opt-in)

- **Status:** **Accepted** — lead-architect + art-pipeline. Ships a runtime **weather→water-mood** blend on
  `WaterSurface` that, when enabled, EASES the sea's MOOD/COLOUR through the §12 / ADR 0010 water preset
  library as the deterministic `EnvironmentSample` shifts. **Opt-in** (off = today's static look), **MPB-only**
  (the `Water.mat` asset is never written), and it drives **only** the mood/colour props — **never** the
  physics props `WaterSurface` already feeds from the sim.
- **Date:** 2026-06-30
- **Decision owner:** lead-architect (the URP shader/rendering plumbing + the `WaterSurface` sim→shader bridge
  are the cross-cutting/architectural seam — `agents/coordination.md` §1.1 "Water/fog/lighting"; CLAUDE.md
  rule 4). art-pipeline owns the *anchor preset moods* + the mapping feel (the two tune together).
- **Flagged from:** the owner asking the weather to **"cycle through the water presets, in a realistic
  fashion"** — i.e. make the deterministic weather drive the water palette/mood, blending realistically across
  the preset library as conditions change (P1 "the sea has moods"). This is the **exact open question** ADR
  0015 logged — *"Per-region palette by mood/weather … A future Core-driven palette selection would be its own
  additive change."* — now answered.
- **Related:** `0010-water-rendering.md` (the layered shader + §12 the preset library this blend mixes),
  `0015-water-palette-guard-rail.md` (the FINAL grade that bounds the blended output; this ADR's blend feeds
  it), `0013-dynamic-lighting.md` (the day/night MULTIPLY overlay that composes downstream), `0009-tidal-
  exposure-…` (the deterministic `WaterLevelAt` / the height-map depth truth the blend must NOT touch),
  `0005-pc-first-target.md` (the 60fps budget, mobile-portable). Design recipe + tunables:
  `design/water-rendering.md` §14.

## Context

`WaterSurface` already reads the deterministic `EnvironmentSample` (via `GameServices.Environment`) each
throttled tick and OWNS the per-renderer `MaterialPropertyBlock` that overrides the water material. Today it
drives the **physics** props from the sim — `_Chop` (sea-state), `_Roughness` (wind), `_Flow`/`_FlowDir`
(tidal current), `_WindDir`, `_WaterLevel` — so calm↔storm *motion* already happens automatically with the
weather on any preset (`design/water-rendering.md` §12.1).

What did **not** track the weather is the sea's **MOOD/COLOUR** — the palette, foam character, swell,
specular, caustics, reflection. The §12 preset library captures those as distinct sea moods
(GlassyCalm / StormGrey / FoggySmother / NorthAtlantic / …), but a preset is a **static** choice: the owner
applies one and the sea wears that palette regardless of conditions. The owner wants the weather to **move
through** those moods — a calm clear day reading serene, a building sea greying and choppening toward storm,
a smother washing the sea pale — blending **realistically** between them as the deterministic conditions
change.

Two hard constraints shape the design (the same ones every §5.6–§13 water layer obeys):

- **Determinism / P1 (rule 5).** The mood must be a **pure function** of the deterministic
  `EnvironmentSample` (recomputed from `(worldSeed, gameTime)`, never saved). Any smoothing is a
  presentation-only ease; it drives no sim and saves nothing.
- **Non-destructive + composable.** It must **not** mutate the shared `Water.mat` asset, must **not**
  re-drive the physics props the sim already owns (no double-drive), and must compose with the ADR 0015
  palette guard-rail and the ADR 0013 day/night overlay (both downstream of the material values it sets).

## Decision

**(1) A pure 2-axis blend from the deterministic `EnvironmentSample`, across four anchor preset MOODS.**
The model (`WeatherWaterPalette`, a pure C# static class) turns the sample into 0..1 weights over four anchor
moods — a region **BASE**, a **CALM** mood, a **STORM** mood, and a **FOG** mood — that sum to 1:

- **Sea-state axis** — the normalised sea-state (Glass=0 .. Storm=1), shaped by a tunable
  threshold + curve, drives a **CALM ↔ STORM** lerp: a serene clear sea at low sea-state lerping toward the
  greyer/choppier/desaturated **Storm** mood as it rises. *(Amended: the axis is now CONTINUOUS at the
  source — `EnvironmentSample.SeaState01`, the piecewise-linear inverse of the `SeaFromWind` wind
  thresholds, equal to the old `SeaStateAxis01(enum)` value at every band edge — so the eased weights track
  a smooth target instead of a 1/7-stepping one, killing the visible pop when wind noise crossed a band
  threshold.)*
- **Fog axis** — `(1 − Visibility)`, shaped by a tunable threshold + curve, pulls the whole thing toward the
  **FOG** mood (pale, desaturated, low-contrast, soft).
- **Combine** — the sea-state lerp produces a calm↔storm base mood; the fog amount then pulls THAT toward
  fog. So a **foggy storm reads mostly fog** (the smother dominates the look), a **foggy calm reads
  pale-serene**, a **clear gale reads storm** — the realistic ordering. (`BlendWeights`:
  `storm = (1−fog)·seaAmt`, `calm = (1−fog)·(1−seaAmt)·calmReach`, `fog = fogAmt`, `base` backfills.)

The default anchors (St Peters): **Base = the live `Water.mat`** (left UNWIRED — `WaterSurface` resolves the
base to the renderer's own `sharedMaterial`, so the calm baseline tracks the owner's `Water.mat` tuning; see
decision (5)), **Calm = Water_GlassyCalm**, **Storm = Water_StormGrey**, **Fog = Water_FoggySmother** — the
calm/storm/fog presets from `Art/Materials/WaterPresets/`.

**(2) Integrated into `WaterSurface`, pushed via the EXISTING MPB.** The blend is an **opt-in mode** on
`WaterSurface` (a master enable + strength, four assignable anchor materials, the axis tunables). Each
throttled tick — the same tick that pushes the physics props — it: reads the `EnvironmentSample`, computes the
weights (the pure helper), **EASES** them toward the target (a frame-rate-independent exponential ease, the
same form as `SmoothVectorToward`, so the mood never POPS), applies the master strength (lerp back toward the
base anchor), then **blends the MOOD props** by reading each anchor material's value per key and writing the
weighted result onto the **same** `MaterialPropertyBlock` — alongside (never replacing) the physics props.

**(3) Blend ONLY the mood/colour props — the disjoint, non-sim-overridden set.** The blend writes exactly the
§12.1 non-sim-overridden keys: the palette grade props (`_Palette*` anchors + floor/ceil/sat-cap/pull/grade
strength + night-floor), the colours (`_DeepColor`/`_ShallowColor`/`_FoamColor`/`_SpecColor`/`_FbmTint`/
`_CausticColor`/`_ReflectionColor`), swell character (`_OceanSwellStrength`/`Sharpness`/`Scale`), foam
character (`_FoamDensity`/`_FoamThreshold*`/`_FoamStreakStretch`/`_FoamSolidThreshold`/`_FoamCrestGate`/
`_FoamSoftness`/`_FoamWidth`/`_FoamNoise`/`_FoamTexStrength`/`_WhitecapTexStrength`/`_Whitecap*` lifecycle),
`_SurfaceTint`/`_SurfaceTexStrength`/`_FbmStrength`/`_SparkleTexStrength`, `_SpecAmount`/`_SpecSharpness`/
`_SpecSwellBias`, `_CausticAmount`/`_CausticScale`/`_CausticDepth`/`_CausticTexStrength`, and the reflection
knobs (`_ReflectionStrength`/`_ReflectionColor`/`_ReflectionSkyTint`/`_ReflectionSmear`/`_ReflectionSunStreak`/
`Sharp`/the chop+wind scatter/fade). The key set is **read from the anchor materials at runtime** (per-key,
`HasProperty`-guarded), so it can't drift from what the presets actually carry.

It **deliberately EXCLUDES** every PHYSICS prop `WaterSurface` already drives from the sim — `_Chop`,
`_Roughness`, `_Flow`, `_FlowDir`, `_WindDir`, `_WaterLevel`, `_HeightTex`/`_Height*`. The two sets are
**disjoint and compose**: the sim still drives the motion (chop/foam roughens physically with the sea-state),
the weather blend sets the look. No double-drive.

**(4) Opt-in, MPB-only, cleared on disable.** The master enable defaults **OFF**, so today's static look (the
owner's `Water.mat` preset) is unchanged. The master **strength** defaults such that 0 = the base anchor only
= today's look (the feature is inert). Every blended value goes onto the per-renderer `MaterialPropertyBlock`
— the shared `Water.mat` asset is **never written** — and the MPB is cleared on `OnDisable` like the rest of
`WaterSurface`'s overrides, so removing/disabling the component restores the authored material.

**(5) Wired into St Peters; opt-in elsewhere.** `StPetersBuilder` enables the mode on the Sea's `WaterSurface`
and assigns the **storm / fog / calm** anchor presets (persisted via `SerializedObject` so they survive in the
saved scene), so a **Build St Peters Scene** re-run gives weather-driven water immediately. No other scene
turns it on. The **BASE / calm anchor is left UNWIRED on purpose**: `WaterSurface.ResolveBaseAnchor` then
falls back to the renderer's own `sharedMaterial` — the **live `Water.mat`** — as the calm baseline the
storm/fog moods blend *relative to*. So weather-off / strength-0 reads as **exactly `Water.mat`**, and the
owner's constant hand-tuning of `Water.mat` always flows through the calm sea — rather than being silently
shadowed by a frozen preset COPY (assigning an explicit base would *pin* the calm look to that copy; we keep
the field available for that opt-in but St Peters does not use it).

### Determinism & save (the invariant guarded)

The mood is a **pure function** of the deterministic `EnvironmentSample` (sea-state + visibility, recomputed
from `(worldSeed, gameTime)`, never saved — rule 5). The ease is a **presentation-only** exponential
smoothing of the *visible* mood (frame-rate independent; the same precedent as `WaterSurface`'s flow
momentum); it lags only what the player SEES, drives no simulation, and saves nothing. The blend reads the
sim through the Core `GameServices.Environment` accessor only (rule 4) and never writes it. Nothing about the
water render — including this mood blend — enters the save (ADR 0008).

### Performance posture (rule 7 — 60fps desktop, mobile-portable)

Runs on the **existing throttled tick** (a few Hz), not per frame. **No per-frame allocation:** the anchor
material array, the cached `Shader.PropertyToID` set, and the scratch weight buffers are all resolved/
preallocated once on enable; per tick it does one weighted sum across ≤4 materials per mood key onto the
already-pooled MPB. At the master strength's identity (0) the blend collapses to the base anchor (a cheap
constant). No extra texture taps, no extra passes, no new Core surface.

## Consequences

- **The sea now MOVES through its moods with the weather** — serene clear → grey choppy storm → pale smother
  — blending realistically between the authored presets, the P1 "the sea has moods" pillar made live (not a
  static palette choice).
- **Composes cleanly with the existing stack** — the blend sets the material's mood VALUES; the ADR 0015
  palette guard-rail still bounds the FINAL output downstream in the shader, and the ADR 0013 day/night
  overlay still multiplies on top. Both are downstream of the values blended here, so they compose by
  construction.
- **Zero new Core surface, zero new coupling.** No new interface/signal/save field; it reuses the
  already-shipped `EnvironmentSample` accessor and the preset materials. `WaterSurface` gains an opt-in mode;
  the shader, `Water.mat`, the preset `.mat` assets, the day/night system, and the guard-rail are untouched.
- **Opt-in + non-destructive + revertible.** Off by default (today's exact look); on only where wired; the
  master strength 0 is inert; everything rides the MPB, so the `Water.mat` asset is never mutated and the
  override clears on disable.
- **Determinism preserved.** Pure function of the deterministic sample; the ease is presentation-only; saves
  nothing — the tide/weather are still recomputed from `(worldSeed, gameTime)` (rule 5).
- **Docs in the same change** (lead-architect DoD): this ADR + `design/water-rendering.md` §14, and the
  ADR 0015 open question is now resolved.

## Rejected alternatives

- **Re-driving the physics props from the weather blend too** (folding `_Chop`/`_Roughness`/`_Flow` into the
  preset blend). Double-drives what the sim already owns and would let a preset fight the deterministic motion.
  The two sets are kept **disjoint** — the sim owns the motion, the blend owns the look.
- **Mutating `Water.mat` (or swapping `sharedMaterial`) as the weather changes.** Writes a shared asset
  (a stray diff + a save prompt in-editor) and is not per-renderer. The per-renderer MPB is the correct,
  non-destructive channel — the same one `WaterSurface` already uses.
- **A Core-driven palette SELECTION** (the sim picks one preset by weather state). A hard switch pops between
  moods and can't render a "foggy building sea". A continuous **blend** with an ease reads as a real,
  transitioning sea — and the owner asked for *blending* "in a realistic fashion".
- **Grading the weather mood in the shader from the raw sample** (no anchor presets). Throws away the §12
  art-directed moods the owner already tuned; the anchors ARE the art direction. The blend mixes them, it
  doesn't reinvent them.
- **Saving the blended mood / driving it from save state.** Violates rule 5 — the mood is a pure function of
  the deterministic sample; the ease state is transient presentation.
- **Per-frame blend.** Outside the rule-7 budget and pointless — the weather is slow; the existing throttled
  tick + the ease is smooth and cheap.

## Open questions (later / for the art pass)

- **Per-region anchor sets.** A region could carry its own four anchor moods (a warmer sheltered harbour vs
  the cold open Atlantic) on its `RegionDef`, with the builder reading them. Today the anchors are assigned
  per-`WaterSurface` (St Peters wires the cold North-Atlantic family). A Core/Region-driven anchor set would
  be its own additive change.
- **More than four anchors / a richer axis space** (e.g. a dawn/dusk warm mood on a time axis, or a "swell
  from a distant storm" axis). The 2-axis {sea-state, fog} model covers the owner's ask; extra axes are an
  art-pass call.
- **Exposing the mapping in `GameConfig`** if the owner wants one global "how hard the weather swings the
  palette" knob across all water — currently per-`WaterSurface` (rule 6 satisfied on the component).
- **Tying the ease response time to a region's weather volatility** so a fast-changing region's sea visibly
  churns its mood faster — a feel call for the art/gameplay pass.
