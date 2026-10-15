# ADR 0018 — One shared deterministic WAVE FIELD: sim and shader read the SAME waves (C#/HLSL twins), so the boat rocks on what you see and foam rides real crests

- **Status:** **Accepted** *(status flipped 2026-09-01 by the lead-architect review: shipped in #147/#152/#313 and the owner's spectrum feel verdict of 2026-07-31 ratified the field — original line follows)* — **Proposed — awaiting owner sign-off.** Docs-only: this ADR ships no code. Merging this
  PR = the owner's go-ahead to start the phased consumer PRs (**Arc B** below). Records the decision
  that waves become **one deterministic directional wave field** — a small sum of wave trains — that
  BOTH the simulation (seakeeping, boat rocking) and the water shader (swell displacement, whitecaps)
  sample, so what the player sees is what the hull feels, by construction.
- **Date:** 2026-07-02
- **Decision owner:** lead-architect (a new shared Core contract read by two feature lanes + the
  water/lighting seam — `agents/coordination.md` §1.1, CLAUDE.md rule 4). gameplay-systems owns the
  seakeeping consumers (boat motion/forces); art-pipeline owns the look (the swell/whitecap rework and
  the shader twin's tuning).
- **Serves:** **P1 "The Sea Has Moods"** (the sea's state becomes a force you *feel and steer through*,
  not paint) and **P5 "Cozy but with Teeth"** (a beam sea that rocks the vessel is the foundation for
  real weather risk). Directly from the owner: waves must be **PHYSICAL** — *"a wave to the beam needs
  to rock the vessel, sailing through the waves to the bow rocks the bow and stern"*; players must
  *"navigate the boat over the waves correctly"*; and the current whitecaps are *"unconvincing… a foggy
  white soup"* because the foam is noise, not something riding real crests.
- **Related:** `0014-painted-seabed-height-authoring.md` (the proven principle this transplants into
  TIME: **one source, sim + render both read it** — "paint = sail" becomes "wave = rock"),
  `0010-water-rendering.md` (the layered shader whose swell/foam layers this reworks),
  `0013-dynamic-lighting.md` + `LightMath`/`MoonMath`/`WaterReflection`/`WaterPaletteGrade` (the
  **headless-twin pattern**: pure C# reference mirrored in HLSL, pinned by EditMode tests),
  `0009-tidal-exposure-…` (the render==sim integrity seam; the wave field rides ON the tide level, it
  never replaces it), `0015`/`0017` (palette guard-rail + weather palette — unaffected layers above),
  `0005-pc-first-target.md` (the 60fps budget, mobile-portable), `design/water-rendering.md`.
- **In-flight dependency:** the **continuous sea-state axis** (`SeaState01`, a 0..1 float on
  `EnvironmentSample` replacing raw enum consumption — branch `fix/continuous-sea-state-axis`, not yet
  on `main` at time of writing). The wave field derives from it when it lands; until then a pure
  enum→0..1 mapping (the shape `WeatherWaterPalette.SeaStateAxis01` already uses) is the stand-in, so
  Arc B is not blocked on it.

## Context

Today the swell is **paint**. The water shader's `SwellField` is value-noise brightness bands
(`_OceanSwellStrength`/`Sharpness`/`Scale`/`Speed`, direction auto-derived from `_WindDir`), and the
whitecap lifecycle keys off that noise field's "crest factor". It looks like moving water — but it
exists **only in HLSL**. The simulation cannot sample it:

1. **The boat cannot feel the sea.** There is no wave height/slope the Boats lane can query, so the
   hull glides flat through a gale. The owner's ask — beam sea rocks the vessel, head sea pitches bow
   and stern, and the player *navigates over the waves correctly* — is impossible against noise that
   lives on the GPU.
2. **The foam has nothing real to ride.** Whitecaps gate on noise, not on crests that build, break,
   and pass — hence the *"foggy white soup"*. A convincing whitecap needs a crest with a position, a
   direction, and a lifetime.

The project has already solved this exact class of problem twice, and both precedents point the same
way:

- **ADR 0014 (space):** ONE painted height map drives the water render AND the tide sim — *painted ==
  sailed*, drift structurally impossible. The wave surface is the same shape of problem **in time**:
  one wave truth, two consumers.
- **The headless-twin pattern (`DayNightMath`, `MoonMath`, `WaterReflection`, `WaterPaletteGrade`):**
  pure C# math is the reference, the shader carries a line-by-line HLSL transcription of it, and
  EditMode tests pin the C# side headless. That is how GPU-visible math stays testable without
  opening Unity.

And the determinism spine is already in place: the wind is a smooth deterministic field
(`WeatherModel.SampleWind` from `(worldSeed, gameTime)`), sea state derives from it, and every
consumer reads it through `GameServices.Environment` (rule 4). Waves are the missing derived layer.

## Decision

**Introduce ONE deterministic directional WAVE FIELD — a small sum (3–4) of directional wave trains —
as pure Core math sampled by BOTH the simulation and the water shader. The C# implementation is the
reference; the shader re-implements the same sum from the same published parameters (the HLSL twin).
It replaces the noise swell as the water's primary displacement over a transition period. This ADR
decides the contract and placement; the consumers ship as separate phased PRs (Arc B).**

### (1) The model — 3–4 directional wave trains derived from wind + sea state

A wave train is `{ direction, wavelength, amplitude, phase speed, phase offset }`. The field is the
sum of 3–4 trains whose parameters are a **pure function** of the deterministic wind
(`EnvironmentSample.WindVector`) and the continuous sea-state axis (`SeaState01`): the primary train
runs downwind at the dominant wavelength; the secondary trains sit at tunable angular offsets with
shorter wavelengths and smaller amplitudes (the cross-chop that makes a real sea read); amplitudes
scale with `SeaState01`. **Crest sharpening** — a Gerstner-style horizontal pinch toward crests, or
an equivalent cheap shaping exponent — makes crests read as crests sitting above broad troughs, not
sine mush. Every constant is a tunable (rule 6), and the derivation
(`WaveMath.TrainsFrom(windVector, seaState01)`) is deterministic: same inputs, same trains, on both
sides of the twin.

Two owner rulings (2026-07-02) are **requirements of the model, not options**:

- **Dispersion is canon.** *"The larger the distance between crests, the faster the wave."* Phase
  speed is **derived from wavelength via the deep-water dispersion relation** —
  `c = sqrt(g·λ / 2π)`, i.e. `c ∝ √λ` — so long swells visibly outrun short chop and a mixed sea
  reads true. Computed once per train per tick (not per pixel): free at runtime, physical by
  construction. A train carries its wavelength; its speed is not an independent tunable.
- **Glass calm is SACRED.** At `SeaState01 ≈ 0` the train amplitudes scale to (near) **zero** — no
  minimum swell, no floor — and the water becomes the full mirror: the reflection layers (sun/moon
  glitter, sky — ADR 0017/#137) read at maximum on the dead-calm glass. Glass means glass.

### (2) The API — `WaveField.Sample(worldPos, time)` → height, slope, crestFactor

```
WaveSample WaveMath.Sample(Vector2 worldPos, double timeSeconds, in WaveTrains trains)
  → float Height        // surface offset (m) about the tide level
  → Vector2 Slope       // surface gradient — the "which way does the deck tilt" read
  → float CrestFactor   // 0..1, high on a breaking crest — the foam driver
```

Pure, stateless, allocation-free (`readonly struct`s). Deterministic from
`(worldSeed-driven wind, gameTime, position)` — **recomputed, never saved** (rule 5). The wave height
is an offset **about** `WaterLevelAt(t)`; the tide/exposure seam (ADR 0009) is untouched — waves do
not move the walkability waterline.

### (3) Placement — pure math in **Core** (`Core/Environment/WaveMath.cs`), because BOTH lanes consume it

The deciding constraint is the asmdef graph: `HiddenHarbours.Boats`, `HiddenHarbours.Environment`,
and `HiddenHarbours.Art` each reference **only** `HiddenHarbours.Core`. `LightMath`/`MoonMath` live in
Art because Art is their *sole* consumer — but the wave field is consumed by the Boats sim (forces,
rocking) AND the Art side (shader constants), and an Art placement would force a
feature-module-to-feature-module reference (rule 4 violation). The codebase's actual convention for
"pure math two lanes must share" is **Core**: `Core/Boats/BoatKinematics.cs`,
`Core/Environment/TidalExposure.cs`. So:

- **`Core/Environment/WaveMath.cs`** — the pure static class (`TrainsFrom`, `Sample`) + the small
  `WaveTrain`/`WaveTrains`/`WaveSample` structs. **Additive-only** Core change: no existing interface
  is modified, no new service/state is introduced — consumers derive trains from the
  `EnvironmentSample` + clock they already read through `GameServices` (rule 4 holds with zero new
  coupling).
- **The shader-globals publisher is an Art-side self-installing bridge** (`Art/WaveFieldBridge`),
  mirroring `GrassWindBridge`/`MoonCycle` exactly: a `RuntimeInitializeOnLoadMethod` host reads
  `GameServices.Environment` + `GameServices.Clock` on a throttled tick, computes the trains via the
  Core `WaveMath`, and publishes packed globals (e.g. `_WaveTrainDirWavelen[N]`,
  `_WaveTrainAmpSpeedPhase[N]`, a sharpening float, a count) via `Shader.SetGlobalVector` — every
  water pixel then reads one set of globals with no per-object wiring.
- **Sim consumers** (Boats) call `WaveMath` directly through Core — they never touch the bridge, the
  shader, or Art.

### (4) The HLSL twin — same sum, same parameters, parity pinned headless

The water shader re-implements the identical 3–4-train sum from the published globals — a
line-by-line transcription of `WaveMath`, changed **in the same PR** whenever the C# reference
changes (the `MoonMath`/`WaterReflection` discipline). Enforcement, per the project's proven pattern:

- **EditMode parity/determinism tests on the C# side** sample `WaveMath` across a grid of positions ×
  times × sea-states and pin the results (plus the invariants: determinism, amplitude monotonic in
  `SeaState01`, crestFactor ∈ [0,1], slope consistent with height via finite difference within
  epsilon). Any drift in the reference fails red, headless.
- **Epsilon philosophy: visual parity, not bit-exactness.** The GPU evaluates in different precision
  (halfs, fast-math trig) and we cannot execute HLSL headless; the contract is that the two twins
  agree to well under a pixel-visible difference (order 1e-3 of the wave amplitude at sim-relevant
  scales), not bitwise. The C# side is the authoritative reference the sim trusts; the shader is its
  transcription.
- **`WaterShaderCompileGuardTests`** (the existing magenta guard) force-compiles the reworked shader
  variant, so a broken twin fails CI red, not magenta-in-build.

### (5) Consumers — phased, each its own PR (Arc B; this ADR builds none of them)

1. **B1 — shader swell + whitecap REWORK (art-pipeline).** The wave-train sum replaces `SwellField`
   as the primary displacement/brightness source, and the foam is re-keyed: whitecaps **form on
   `CrestFactor`** with a lifecycle — form as the crest builds → break at the peak → streak downwind
   (`_FoamStreakStretch`) → fade to milky residual — replacing the noise soup. The existing
   `_Whitecap*` lifecycle tunables carry over, re-keyed off the real crest.
2. **B2 — boat VISUAL motion (gameplay-systems + art-pipeline).** Bob/roll/pitch from
   `Height`/`Slope` sampled at the hull against its heading: a beam sea rolls, a head sea pitches bow
   and stern. Presentation-only first — no forces — so the owner can feel the read before it bites.
3. **B3 — seakeeping forces v1 (gameplay-systems).** Data-driven per-hull response on **`BoatHullDef`**
   (rule 2 — a dory corks about, a tanker shrugs), behind a **`GameConfig` toggle** so the owner can
   tune or switch it off without code (rule 6). **Punishing seas are ratified** (owner, 2026-07-02):
   *"waves should be punishing in certain areas at certain times"* — v1 ships with **real
   consequences**, not cosmetic wobble, modulated on two axes:
   - **TIME** — the sea state / weather, via the continuous `SeaState01` axis the trains already
     scale with (a Gale beam sea bites; a Calm one doesn't);
   - **PLACE** — **exposure**: open water takes the full sea, the lee of land is sheltered. For v1 a
     simple deterministic exposure factor suffices (e.g. derived from the ADR 0014 painted height
     map / distance-to-land — a `feat/water-depth-distance-to-land` prototype branch explored this
     signal once); the **exact** exposure model is a design detail for the B3 PR, but the principle
     — *place matters* — is decided here.
4. **(Later, its own arc) — shore breakers.** Shoaling: the ADR 0014 painted height map feeds depth
   into the train parameters so waves steepen, slow, and break toward the beach. Explicitly out of
   Arc B's scope — logged, not built (rule 8).

### (6) Relationship to the existing swell — replace over a transition, the tuned look survives

The wave field **replaces the noise swell as the primary displacement** over a transition period, not
in one cut: B1 lands with the old `_OceanSwell*` path intact behind the existing tunables, and the
owner's tuned values **map onto the new train scales** (`_OceanSwellStrength` → overall amplitude
scale, `_OceanSwellSharpness` → crest shaping, `_OceanSwellScale` → dominant wavelength) so the
current look carries across rather than resetting. Everything ABOVE the displacement is untouched by
construction: the palette guard-rail (ADR 0015), the weather-driven mood blend (ADR 0017), the sky
reflection / moon / stars, and the day/night overlay (ADR 0013) all compose downstream exactly as
today.

### Determinism & save (the invariant guarded)

The field is a pure function of `(worldSeed-driven wind, gameTime, position)` — the wind is already
deterministic (`WeatherModel`), the trains derive purely from it + `SeaState01`, and `Sample` is
stateless. **Recomputed, never saved** (rule 5): no save-format change, no schema bump (ADR 0008). No
hidden RNG — if a train needs phase variety it hashes off the world seed through the same
deterministic path the Environment module already uses. All cross-module reads go through
`GameServices` accessors (rule 4). The EditMode grid tests are the determinism guard, headless.

### Performance posture (rule 7 — 60fps desktop, mobile-portable)

3–4 trig wave trains per water pixel is a handful of `sin`/`cos` and dot products — well inside the
budget for a shader already layering fbm, caustics, reflections, and foam, and it *replaces* (not
adds to) the noise-swell evaluation it supersedes. The C# side is a few `Sample` calls per boat per
sim tick (bow/stern/beam probes), allocation-free structs, on the existing throttled cadence. The
bridge publishes a fixed handful of global vectors per throttled tick — the `GrassWindBridge` cost
profile. **No render textures, no FFT, no CPU↔GPU readback, no per-frame allocation.** Mobile stays
portable: the whole feature is arithmetic.

## Consequences

- **The sea becomes physical.** The hull rocks on the same crests the player sees; steering bow-on
  into a swell versus taking it on the beam becomes a real, learnable read (P1/P5 — the owner's ask,
  verbatim).
- **Foam finally rides real crests.** The whitecap lifecycle keys off `CrestFactor` from an actual
  advancing wave train — form, break, streak, fade — instead of gating on noise (the "foggy white
  soup" cause removed at the root).
- **Sim==render by construction, in time as in space.** ADR 0014's invariant transplanted: one wave
  truth, two consumers; the twins share parameters and tests, so they cannot silently diverge.
- **Small additive Core surface, zero new coupling.** One pure static class + three small structs in
  `Core/Environment`; no interface changes, no new service, no save change. Boats and Art still
  reference only Core.
- **The owner keeps the wheel.** Train derivation constants are tunables, per-hull response is
  `BoatHullDef` data, seakeeping sits behind a `GameConfig` toggle, and the tuned `Water.mat` look
  maps onto the new scales rather than resetting.
- **Phased and revertible.** Each Arc B consumer is its own small PR; B1 keeps the old swell path
  during the transition; B2 is visual-only before B3 adds forces.
- **Docs in the same arc** (lead-architect DoD): this ADR now; `design/water-rendering.md` gains the
  wave-field section in B1.

## Rejected alternatives

- **Shader-only waves (the status quo).** The boat can never feel a GPU-only field, and the foam has
  nothing real to ride — the two owner complaints ARE this alternative's failure mode. Rejected by
  the ask itself.
- **Full buoyancy physics** (submerged-volume integration, water-plane meshes, rigidbody buoyancy
  voxels). Massive overkill for a ¾ top-down *feel* game — we need height/slope/crest at a few probe
  points, not displaced-volume hydrostatics. The per-hull response curve on `BoatHullDef` delivers
  the feel at a fraction of the cost and stays owner-tunable.
- **FFT ocean spectra** (Phillips/JONSWAP, Tessendorf). The realism gold standard and wrong here on
  every axis: render textures + compute per frame (mobile-hostile, rule 7), a continuous spectrum
  that fights the pixel-art read (ADR 0004's KTC mood wants legible directional bands), and
  non-trivial CPU mirroring for the sim. 3–4 authored trains are art-directable; a spectrum is not.
- **Per-frame CPU readback of the shader's field.** GPU→CPU sync stalls the pipe (a non-starter at
  60fps), inverts the dependency (sim trusting render), and still isn't deterministic across GPUs.
  The twin pattern exists precisely to avoid this.
- **Placing `WaveMath` in Art (beside `LightMath`/`MoonMath`).** Boats cannot reference Art (asmdef
  graph, rule 4) — the sim side would need a feature-to-feature reference or a copy-paste fork. Those
  twins are Art-local because Art is their only consumer; this one has two lanes, so it goes where
  the codebase's shared pure math already lives: Core.
- **A new stateful Core service (`IWaveService`).** Needless surface: the field is a pure function of
  data consumers already have (`EnvironmentSample` + clock). Statics + structs are cheaper, testable,
  and additive; if a region someday needs its own train profile, a `WaveProfile` alongside
  `TideProfile`/`WindProfile` is the natural additive step — still no service.
- **Saving wave state / integrating waves in the save.** Violates rule 5 — recomputed from
  `(worldSeed, gameTime)`, like tide and wind. Not saved, ever.

## Owner rulings (2026-07-02 — decided, no longer open)

Three questions this ADR originally left open were ruled on by the owner before it merged; the
decisions are baked into the sections above and recorded here so they read as settled canon:

- **Dispersion is canon** (§(1)): phase speed derives from wavelength — `c ∝ √λ`, the deep-water
  dispersion relation — so long swells outrun short chop. A requirement, not a tunable.
- **Punishing seas are ratified** (§(5) B3): *"waves should be punishing in certain areas at certain
  times"* — seakeeping v1 ships with real consequences, modulated by **TIME** (`SeaState01`) and
  **PLACE** (exposure: open water vs the lee of land). The exact exposure model is a B3 design
  detail; the principle is decided.
- **Glass calm is sacred** (§(1)): at `SeaState01 ≈ 0` the trains flatten to (near) zero — no
  minimum swell — and the reflection layers read at maximum. Dead-calm glass reflecting the sun
  glitter is the owner's explicit want. Glass means glass.

## Addendum (2026-07-03) — the presentation smoothing layer (`WaveFieldAnimator`)

B2's first playtest surfaced a structural flaw in how a *presentation* consumer refreshes the
trains: `TrainsFrom` is a pure function of the continuously drifting wind, so each re-derivation
moves the dominant wavelength — and with it k and the dispersion-derived c — while the closed-form
phase `θ = k·(d·pos − c·t) + φ` multiplies both by a LARGE running t. Every refresh therefore
JUMPED the phase (the owner's "jittery" rocking, proportionally worst on a small-amplitude calm
sea). The fix is **`Core/Environment/WaveFieldAnimator.cs`** — a small stateful presentation-side
helper both look consumers share (B2's `BoatWaveMotion` now; the B1 shader bridge should adopt it
when it lands): per tick it **eases** each train's wavelength/amplitude/direction toward the
`TrainsFrom` targets (fps-independent exponential), re-derives phase speed from the **eased**
wavelength through the canon dispersion relation (speed still never free), and accumulates each
train's phase **incrementally** (`Φ += k·c·dt`, wrapped) so the phase is continuous **by
construction** — the accumulated phase rides in `PhaseOffset`, so the pure `WaveMath.Sample`
(at t = 0) remains the single evaluator. **Glass stays sacred:** easing toward zero is asymptotic,
so amplitudes whose target is silent snap to exactly 0 below a tiny floor — dead calm still
reaches the true mirror.

**Scope honesty (the contract boundary):** the animator is **presentation-only and stateful** —
deterministic given the same tick sequence, but **NOT a pure function of gameTime** (different
frame rates ease along different paths; nothing is saved). The sim contract above is unchanged:
**B3 seakeeping forces and anything gameplay-consequential keep reading the pure
`WaveMath.TrainsFrom` + `Sample(pos, gameTime)` path** — pointing the sim at the animator requires
a lead-architect decision first.

## Amendment (2026-07-29) — the field widens to **8 trains**, and the dominant train stops being an accident

**Status: proposed with the ADR 0027 P2 PR-A code; `lead-architect` reviews it there.** This ADR
fixed the model at "3–4 directional wave trains" (§(1)). ADR 0027 decision (6) — the JONSWAP
spectrum, pulled up to P2 after the owner ruled the sea reads as *"a rigid pattern"* at every
non-storm weather he has seen — cannot be built inside that number. This amendment raises the
ceiling and names the one contract that was previously implicit.

### (a) `MaxTrains` 4 → 8

Four trains are enough to hand-author *a primary plus cross-chop*, which is exactly what §(1)
describes and exactly what shipped. They are **not** enough to carry a spectrum, for two independent
reasons:

- **A shape needs samples.** A JONSWAP amplitude distribution across four frequencies is four points
  on a curve — a peak and not much else. "Variance in sizes" is the owner's ask, and four sizes,
  three of them fixed fractions of the first, is what he is already looking at.
- **Groups are a beat between NEIGHBOURS.** Wave grouping — three big ones then a lull — is not a new
  oscillator; it falls out of closely-spaced frequencies interfering. Four trains spaced at ratios
  0.55 / 0.38 / 0.22 of the dominant are not neighbours in any sense that beats at a readable period.
  Widening is what makes grouping available *for free* rather than faked.

**8, not 12 or 16 — measured, not assumed.** The cost is linear in train count and it is paid in the
fragment shader `WAVE_MAX_TRAINS` times per `WaveFieldSample()` call, at up to nine call sites per
pixel (the main sample, four caustic-curvature taps, four foam-convergence taps) — and the loop is
`[unroll]`ed with the live count masked *inside*, so **dead slots still cost ALU**. Measured with
`Hidden Harbours/Dev/Wave-field cost report` (compiled D3D11 fragment bytecode, both water passes,
same machine, shader-only difference):

| | N = 4 | N = 8 | Δ |
|---|---|---|---|
| Compiled fragment bytecode | 242,540 B | 273,188 B | **+30,648 B (+12.6 %)** |
| Worst-case train evaluations / pixel | 36 | 72 | ×2 |

The two rows disagree, and that disagreement is the argument: the train evaluations double, but the
whole fragment program grows 12.6 % — the wave sum is one term in a 3,300-line shader. Equivalently
the wave field is **~12.6 % of the fragment program at 4 trains, ~22 % at 8**, and each further
step of 4 costs another flat ~30.6 kB: **N = 12 is +25.3 %, N = 16 is +37.9 %**. Twelve is about
where the wave term stops being one cost among many and becomes the shader's largest single item,
while the spectrum's readable payoff is already saturating. 8 is the last number that buys a real
spectrum without changing what the water shader mostly is.

⚠️ Program size is a proxy for ALU, **not** a frame-time measurement — reproducible and
machine-independent where a wall-clock number on one GPU would not be, but it is not the same claim.
And 72/pixel is the worst case: eight of the nine fragment call sites sit behind the caustic and
convergence gates, so a pixel with both off pays 8.

⚠️ **The width is a SEAM, and it is load-bearing in five places at once**: `WaveTrains.MaxTrains`,
`PackedWaveField.MaxTrains`, the bridge's uniform push, the production shader's `WAVE_MAX_TRAINS`
loop bound, and the shore-seam proof shader that mirrors it. Widening four of the five is **not a
compile error** — it is a reader quietly sampling a narrower sea than the shader draws, which is this
ADR's core invariant failing silently. `WaveFieldSeamWidthTests` asserts the width structurally,
including by reading both shader sources as text, because that is the only way a C# test can hold the
HLSL half of a twin.

### (b) The payload becomes one value: `PackedWaveField`

§(3) described the bridge publishing "packed globals"; in practice that meant six loose `Vector4`s
threaded through every signature that reads the sea. At eight trains that is eleven, and the hull
clamp would have taken sixteen parameters. They are now one `readonly struct` passed by `in`
(allocation-free, rule 7). The point is not brevity: it is that **the width now lives in one place**,
so a future widening cannot leave a reader behind. For the same reason `WaveFieldBridge.PublishGlobals`
is public — the rendering acceptance suites and the shore-seam proof harness each used to spell the
`SetGlobalVector` calls out by hand, which is three copies of a seam to forget.

### (c) `DominantIndex` — the spectral peak, named

§(2) and the 2026-07-03 addendum have consumers reading "the dominant train": `BoatWaveMotion`, the
buoys, the drift weed and `WaveFieldAnimator.DominantPhaseDegrees` all read its phase **forward**
(never `atan2` — see `WaveMath.TrainPhaseDegrees` for what reconstruction cost in measured stutter),
and the shader's whitecap lifecycle keys on its face sign. Every one of them took **slot 0**, and
every one of them was right — because under a flat weighting the biggest train is always the downwind
primary.

A JONSWAP re-weighting moves the peak. The convention would then have the hull rocking to one train
while the foam broke on another, silently, with nothing red. `WaveTrains.DominantIndex` makes the
choice explicit and publishes it in `_WaveFieldParams.w` (formerly `reserved`, formerly always 0 —
which is what the flat weighting still publishes, so the payload stays byte-identical until the
spectrum lands).

### (d) What is NOT amended

- **Dispersion stays canon.** `WaveTrain.PhaseSpeed` is still derived from wavelength in the
  constructor; there is still no way to build a train with a free-standing speed. ADR 0027 #9's
  `_DispersionScale` governs the **visual octaves** only and is deliberately *not* applied to the
  field's trains — they already disperse, and applying it there would double-apply the law and move
  drawn geometry the interior-mask/clamp stack guards (`WaterDispersion`'s class doc, PR #310).
- **Glass is still sacred.** Amplitudes are still exactly 0 at `SeaState01 = 0`. A spectral *floor* is
  the obvious way to get "variance in sizes" wrong, so `WaveSpectrumPassthroughTests` asserts the
  ruling by name.
- **Nothing is saved.** The field is still recomputed from `(worldSeed, gameTime)`; no save change.
- **The 3–4-train model is not deprecated.** `TrainsFrom` still derives exactly four trains at the
  shipped settings, bit-for-bit — PR A is the seam, not the sea. `WaveFieldSettings` still describes
  a primary plus three secondaries, and `SecondaryTrainCount` now clamps against
  `DerivedSecondaryTrainSlots` rather than `MaxTrains - 1` (they were the same number by coincidence;
  following `MaxTrains` would publish underived slots as live).

### (e) The spectrum that the width exists for (PR B)

`WaveSpectrum` + `WaveFieldSettings.Spectrum*` land in the follow-up PR: JONSWAP amplitudes,
`cos^2s` directional spreading, and wave grouping as the beat between neighbouring frequencies.
**No shader change was needed** — the widened seam already carries the trains, which is what the
two-PR split was for.

`SpectrumBlend` ships at **0**, so the sea is unchanged until the owner dials it in. Every slot lerps
from its legacy self toward its spectral self and slots 4–7 fade in from exactly zero amplitude, so
the field is continuous even though the live count jumps 4 → 8 the instant the dial leaves zero.

⚠️ **`Σ amplitudes` is preserved, not `Σ amplitudes²`.** Energy is the more physical normalization,
but Σ A is the crest-factor normalizer the whitecap lifecycle divides by AND the bound the watertight
hull clamp scans against — growing it would quietly reduce foam and raise every hull. Preserving the
envelope means the spectrum's peaks reach the height the hand-authored sea reached and what it adds is
the **lulls between them**, which is the ask, at no risk to two calibrated systems.

### (f) The settings unify onto `GameConfig` — the "later Arc B PR" is this one

§(5) deferred a GameConfig unification to "a later Arc B PR", and every consumer carried a
`// keep identical to the others` comment in the meantime. **There were eight copies**, not the two
the spectrum PR reported: `WaveFieldBridge`, `BoatWaveMotion`, `BoatController` (the sim path),
`BoatWakeEmitter`, `BuoyWaveVisual`, `TrapHaulController`, `SeaweedPresenter` — and each of them
began at `WaveFieldSettings.Default`, so they agreed only for as long as nobody tuned anything.
`SpectrumBlend` turned that into a one-slider mistake.

- **`GameConfig.WaveField` / `GameConfig.WaveFieldAnimator`** are now the only instances.
- **`GameServices.WaveField` / `.WaveFieldAnimator`** are the reads; `GameRoot` wires the asset.
  Unwired (EditMode, a bare art scene, any test rig) falls back to `Default`, so nothing breaks.
- **Resolved per read, not cached at `Awake`**, so dragging a slider during play moves the sea —
  which is how the owner will actually judge the spectrum.
- ⚠️ **`Config != null`, never `?.`/`??`.** `GameConfig` is a `UnityEngine.Object`; the
  null-propagating operators bypass its overloaded `==`, so a destroyed asset would read as alive and
  throw. Pinned by a test that destroys one.

⚠️ **The sharpest edge this closes:** `WaveFieldBridge` is a runtime-created `HideAndDontSave` host.
It has no inspector, so *the water's copy was the one the owner could never reach* — a tuned
`GameConfig` would have moved the hull, the wake and the buoys while the drawn sea stayed at Default.

`WaveFieldSettingsUnificationTests` scans the assemblies and fails if any type outside `GameConfig`
declares one of these structs as a field, so the eight copies cannot come back.

### The cost stated plainly

This changes what the hulls physically ride the moment `SpectrumBlend` leaves 0. The C# twins,
headless determinism tests and passthrough proofs are the technical gate; **the owner's feel verdict
is the real one**, and per ADR 0027 it should be taken against the same hull set as the ADR 0023
verdict so the comparison is honest.

## Amendment (2026-07-31) — the field gains a **spatial amplitude envelope**: wind fetch (ADR 0027 #1)

The field was, until now, purely a function of *time*: `TrainsFrom(wind, seaState)` produced one set of
trains and every position on the sea rode the same ones. **Wind fetch makes it a function of place as
well** — how far the wind has blown over open water before it reaches you sets how big the waves get
there, so lee shores go calm and exposed shores build. This is the first thing in the water stack that
varies the field *across the map*, so it is amended here rather than left to ADR 0027.

### (a) The shape of the change — an envelope, not new trains

`WaveFetch.EnvelopeAt(pos, …)` marches upwind over the authored seabed and returns a scalar in
(0, 1]. Consumers multiply the field's **height and slope** by it. Nothing about the trains changes:
same count, same wavelengths, same phases, same `TotalAmplitude`.

**⚠️ Fetch modulates AMPLITUDE only — never wavelength, never speed.** Real fetch sets all three, and
scaling wavelength here is the obvious next step. It is wrong three times over, and the reasons are
worth recording because the temptation will recur:

1. **A per-position wavelength is not a wave field.** The analytic slope `A·p·s^(p−1)·cos θ·k·d`, the
   phase continuity `WaveFieldAnimator` accumulates, and the crest-factor normalizer all assume **one
   `k` per train across space**. Vary `k` with position and the slope stops being the derivative of
   the height — and the slope is what the deck tilt, the caustic curvature and #10's windward gate all
   read. The field would still *look* plausible while being internally inconsistent.
2. **Speed is not ours to set.** `WaveTrain.PhaseSpeed` **is** the dispersion relation, derived from
   the wavelength at construction. The ADR 0027 P2 audit already found that applying the visual-octave
   laws to the field would double-apply what the trains carry; this is the same trap wearing a
   different hat.
3. **`freqScale` is off limits.** It is the displaced vertex stage's own knob
   (`_OceanSwellScale/0.025`), and the watertight clamp scans against it. The `_OceanSwellScale`
   incident is what happens when something touches it without the clamp following.

Amplitude is the one term that can vary spatially and leave a coherent field behind.

### (b) Why it is Tier B and not the visual-first phasing ADR 0027 wrote

ADR 0027 phased #1 as "fetch modulates the visual layers first, promoted into the field later",
calling visual-only fetch *"an acceptable intermediate, and an explicit one"*. **That intermediate is
not built.** A shader-only damp is the `_OceanSwellScale` incident by construction: the drawn sea at
one amplitude, the sampler behind the hull at another. The player would see glass behind the headland
and feel the open-water swell in it — and the more the model was dialled up, the wider the gap. The
envelope therefore lands in the one field both consumers read, or it does not land. Every field
consumer is wired through it in the same commit (`BoatWaveMotion`, `BoatController`'s seakeeping,
`BuoyWaveVisual`, `BoatWakeEmitter`, `TrapHaulController`, `SeaweedPresenter`), plus both shader
stages — fragment and the displaced vertex stage.

### (c) What falls out, and what stays safe

- **A lee loses its whitecaps for free.** The crest factor is `height / TotalAmplitude`; the envelope
  scales the numerator and deliberately **not** the denominator. This is correct physics arriving
  without being wired, and it is the reason the envelope must never be folded into the trains.
- **The watertight hull clamp needs no change.** The envelope is ≤ 1, so the fetched sea is never
  taller than the unfetched sea the clamp was calibrated against. It stays a valid bound at every
  strength — it can only over-dry, never flood, the direction ADR 0023 already chose.
- **The slowly-varying-envelope approximation, stated.** `∇(E·h) = E·∇h + h·∇E`; consumers take the
  first term only. `E` turns over the fetch scale (tens of metres), `h` over the wavelength (metres),
  so the dropped term is small by construction. The same approximation licenses evaluating **one
  envelope per hull** rather than one per probe.

### (d) The seam, and its tripwire

The march is a **fixed** iteration count — `WaveFetch.MarchSteps` in C#, `FETCH_MARCH_STEPS` in HLSL.
ADR 0027 states this as an implementation constraint, not a preference: `[unroll]` over a runtime
bound is a known magenta trap (#96). `WaveFetchTests.MarchStepCount_MatchesTheShader` reads the count
out of the shader source and fails red if the two halves ever drift, the
`RipplePixelFootprintTests` pattern.

The march's **coordinate grid** is a second half-seam of the same kind — `WaveFetch.PixelsPerUnit` in
C#, `FETCH_MARCH_PPU`/`FetchPixelize` in HLSL, pinned by `MarchPixelGrid_MatchesTheShader`. It is
deliberately **not** the material's `_PixelsPerUnit`: that is an art knob (shipped `Water.mat` sets it
24, the presets 12), and quantizing the march through it would let a pixel-scale tweak move the lee
the hull rides — the seen ≠ felt split handed back to a tuning slider. A source-text test also asserts
the march body never calls the material-driven `Pixelize()`, because a value twin cannot see which
HLSL function the march called.

**The geometry source is exactly parallel on the painted path, and only bake-resolution-approximate on
the analytic one.** Where a region's seabed is authored as a height map, both marches read the same
texels and the lee boundary is identical by construction. Where it is analytic, the C# side evaluates
the exact function while the shader reads the low-resolution baked `_HeightTex`, so the two lee
boundaries can disagree by up to the bake's cell size. That seam pre-dates this amendment, but the
envelope makes it **load-bearing** — it is now a difference in what the hull rides, not only in what
is drawn. Accepted for now (regions in play are painted); if an analytic region ever ships with the
dial up, raise the bake resolution or move the C# side onto the same baked sampler.

### (e) It ships OFF

`WaveFetchSettings.Strength` is 0, which returns **exactly** 1 — so the sea, drawn *and* ridden, is
byte-identical until the owner dials it in. **The owner owes a feel verdict before it goes further**,
for the same reason the spectrum did: this changes what the hulls ride.

## Open questions (for Arc B, with the look in front of us)

- **Fetch: does it want a per-region reach?** `StepMeters` × 24 sets the upwind reach globally (96 m
  at the default). A large open region and a tight harbour may want different reaches. Measure once the
  owner has dialled the strength up — do not add a per-region knob in advance.
- ~~**Train count: 3 or 4?**~~ **Superseded by the 2026-07-29 amendment:** the ceiling is 8, and
  `TrainsFrom` derives 4 until the P2 spectrum re-weights them.
- ~~**How the settings get unified onto GameConfig**~~ **DONE (2026-07-29 amendment (f)):** eight
  per-component copies collapsed onto `GameConfig.WaveField`, read through `GameServices`.
- **How long does the legacy noise-swell path live?** Proposed: through Arc B as the fallback, removed
  once the owner signs off the reworked look — its tunables then become the mapped train scales.
- **The exact v1 exposure model** (B3): painted-height-map shoaling signal vs a distance-to-land
  falloff vs a hand-tunable per-region factor — pick when B3 is in hand; deterministic either way.

## Note (2026-09-03) — the DRAWN sea now runs this field at its own wavelengths (water fidelity PR 6)

The one-sea rule this ADR establishes had a caveat that lived in ADR 0023: the shader drew the field at
`_OceanSwellScale / 0.025` — **2.8** on every shipped material — so the sea on screen was the same
amplitude envelope at wavelengths 2.8x shorter than the field these trains describe. Consumers coped by
threading that scale through the `DisplacedSea` seam (the ride, the watertight clamp, the foam injector,
the lip spray, and the bore's phase since ADR 0040 rev 3).

On the owner's 2026-09-02 ruling (*"a longer slower swell"*) all nine materials went to **0.025**, so the
scale is **1** and the drawn sea IS this field — same wavelengths, same periods, same evaluation. Nothing
in this ADR changes; the caveat simply stops applying. The seam and every scaled read stay, because the
knob is still the owner's. See ADR 0023's 2026-09-03 amendment for the detail and the cost numbers.

## Amendment (2026-10-13) — Stage A: the sea remembers

Stage A is built in W4-2. Wind sea direction and height now respond over different past
windows for different wavelengths (P1, P5). `WaveMath.TrainsFrom` remains unchanged: it is
the steady state, including its exact float results. The shipped fixed spectral ladder
keeps dispersion and travel phase independent of wind.

### Windows and the clock

The six new fields live in `GameConfig.WaveField`, with matching serialized asset values:

| Field | Default | Unit | Range |
|---|---:|---|---|
| `MemoryShortSeconds` | 30 | game seconds | 1–600 |
| `MemoryMiddleSeconds` | 120 | game seconds | 1–3600, at least short |
| `MemoryLongSeconds` | 600 | game seconds | 1–7200, at least middle |
| `MemoryShortMaxWavelengthMeters` | 8.5 | metres | 1–100 |
| `MemoryMiddleMaxWavelengthMeters` | 20 | metres | 1–100, at least short limit |
| `MemoryTapCount` | 16 | cells per window | 4–64 |

Zero or missing fields resolve to these defaults. On the shipped ladder, slots 5–7 use
30 seconds, slots 2–4 use 120 seconds, and slots 0–1 use 600 seconds. This three-band
curve retains a fast chop response while reducing the long crests' shared turning. At
75 game seconds per clock hour, these windows span 24, 96 and 480 clock minutes.
The water-four prediction compared 30, 120 and 600 seconds; the five-seed, three-day
W4-2 repeat at St Peters gives long-wave p95 slide 0.464 m/game second at radius 262 m,
versus 3.545 before. NMC gives 0.427 versus 3.139 at radius 278 m. The whole field's
60-second relative Hs change falls from 149.6% to 55.6% at p95 in St Peters; the faster
middle and short bands explain why this exceeds the uniform-600-second prediction.

For each window W and tap count N, the world grid has spacing W/N and origin zero.
Let q=floor(t/(W/N)) and f be its fractional remainder. Sample wind at (q−j)W/N,
j=0…N, with weights f, 1…1, 1−f, divided by N. These are the exact fractional-cell
weights of a trailing box integral over a held wind law. Every tap is at or before t;
there is no forecast. Incoming and outgoing cells have zero weight at their boundary,
so even a stepped wind enters continuously. The finite grid adds up to one cell of
response delay; the grids continue into negative time when a world begins.

Wind vectors are averaged directly, weighting bearing by wind speed. A zero wind has
no bearing contribution, and angle wrap needs no special case. The continuous sea-state
axis is averaged with the same weights for the derivation's glass gate and legacy
height law. Differences from the first tap are accumulated in double, preserving a
steady input bit for bit. Three calls to the unchanged derivation supply the three
bands; the largest finished amplitude determines the dominant slot. When all windows
agree, the original dominant metadata is preserved, including on a silent spectrum.

### Core seam and consumers

`IWaveWindSource.WaveWindAt(double)` is the new additive Core contract. It returns
`WaveWindSample` (wind vector and continuous sea state). `EnvironmentService` implements
it using `WeatherModel.SampleWind`, the active region profile, seed, clock scale and dev
weather override. Core never depends on the Environment module. Historical queries use
the destination region's law, not the route travelled to reach it.

`WaveFieldAnimator` loses all parameter easing and accumulated state. It evaluates
`WaveMemory` and bakes the closed-form travel phase into the returned trains. Its Current
value is output only; reset, pause, a backward jump, or a cold load cannot affect the
answer at t. Its previous settings remain serialized for compatibility and are inactive.
The scalar Smooth helper remains for separate presentation motion. No save fields change.

The bridge sends the same trains to `PackedWaveField` and `SharedWaveField`.
`BoatController` seakeeping now reads that publication at time zero because travel is
already baked. Before publication it derives raw memory trains from the Core seam and
samples them at the clock; fixtures without the seam derive a steady current wind.
Other depth, exposure, fetch and dominant-train reads keep their existing contracts.
`WaterSurface` reads `WaveMemory.ShortWind` each frame at the same clock for `_WindDir`
and `_Chop`; the separate 3-second wind and 2-second chop eases are removed. Current
and tide easing remain separate. No shader loop, material, foam or fetch changes land.

Wind-step continuity is guarded on the fixed shipped spectral ladder. Editing train
count or disabling that ladder is an immediate configuration change, not past weather;
there is no hidden frame-dependent fade for such edits. Slot-emergence tests now cover
continuous spectrum blend and waves growing from calm through the wind window.

### Danger timing and cost

A steady 4-to-10 m/s wind step at shipped St Peters storm field settings reaches Hs
0.404 m (50% of its final 0.808 m) after 21.4 clock minutes, and 90% after 396.8 minutes.
The previous simulation changed immediately; the previous drawn 1.5-second ease reached
these levels after 0.25 and 2.24 clock minutes. Long seas build and subside slowly, so a
wind warning now precedes the full sea and a lull can retain waves. A wholly calm window
still reaches exact glass. This duration change needs the Phase B sailing check.

The headless warm sampler uses 51 wind-law calls and up to three train derivations per
field evaluation, about 9 microseconds on the development machine. A 100,000-tick run
allocated zero bytes after warm-up. Decorative short wind adds 17 taps per evaluation.
GPU work and payload width are unchanged; editor/native publication and visual evidence
remain Phase B checks.

### Stages B and C: ruled, not built here

- **Stage B, W4-3:** fetch growth acts per wavelength. Short chop can regrow in a lee
  while long waves need a longer clear reach. The shader and every field consumer must
  share that per-train amplitude envelope; dispersion is unchanged.
- **Stage C, W4-5:** a swell independent of local wind takes the two shortest slots,
  currently 6.34 m and 5.00 m, retaining eight trains. Glass stays glass in harbours and
  sheltered water; low swell may cross the open coast in a calm. The open-water glass
  premise changes when that stage lands, not in Stage A.
- **Order:** C2a → W4-1 → W4-2 → W4-3 → W4-4a/b → W4-5 (Stage C) → W4-6a/b
  (Stage D, bathymetry) → B2 (breakers) → P1 (wake riding). W4-2 lands after W4-1.
