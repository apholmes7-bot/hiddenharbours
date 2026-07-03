# ADR 0018 — One shared deterministic WAVE FIELD: sim and shader read the SAME waves (C#/HLSL twins), so the boat rocks on what you see and foam rides real crests

- **Status:** **Proposed — awaiting owner sign-off.** Docs-only: this ADR ships no code. Merging this
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
scale with `SeaState01` so Glass is near-flat and Storm is heavy. **Crest sharpening** — a
Gerstner-style horizontal pinch toward crests, or an equivalent cheap shaping exponent — makes crests
read as crests sitting above broad troughs, not sine mush. Every constant is a tunable (rule 6), and
the derivation (`WaveMath.TrainsFrom(windVector, seaState01)`) is deterministic: same inputs, same
trains, on both sides of the twin.

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
   tune or switch it off without code (rule 6).
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

## Open questions (for the owner — answer before/while Arc B lands)

- **How punishing is a beam sea in M1 vs M2?** B2/B3 can range from *comfort wobble* (pure feel, no
  danger) to *capsize risk in a Gale beam sea* (P5 teeth). Proposed default: M1 ships feel-only
  (B2) + gentle forces (B3, toggle on, tuned soft); real danger (shipping water, capsize) waits for
  M2 weather — but the owner sets the dial (`GameConfig` + `BoatHullDef`).
- **Are wave trains visible at Glass/Calm at all?** A truly glassy sea (mirror reflections, ADR
  0017's serene mood) argues for amplitude → ~0 below a low `SeaState01` threshold; a barely-living
  long swell even on calm days argues for a small floor. Tunable either way — owner's art call.
- **Train count: 3 or 4?** Both fit the budget; 4 reads richer cross-chop, 3 is cheaper on mobile.
  Decide in B1 with the look in front of us.
- **How long does the legacy noise-swell path live?** Proposed: through Arc B as the fallback, removed
  once the owner signs off the reworked look — its tunables then become the mapped train scales.
