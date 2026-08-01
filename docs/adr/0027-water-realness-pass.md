# ADR 0027 — The realness pass: ten physically-grounded water upgrades, every one pixelized

- **Status:** **Accepted** (2026-07-28) — the owner ratified the phase order and named **wave variability**
  the priority; `lead-architect` signed off the plumbing split the same day (P1 = Tier A only:
  `col.rgb`/visual octaves, no new render plumbing, nothing the hulls ride). Flipped from Proposed in the
  first P1 code PR (#2 caustics), as sanctioned by the P1 handoff. The ADR text itself remains docs-only;
  the P1 PRs carry the code.
- **Revision (2026-07-31, at the P3 #10 build):** the ADR made #10 conditional on a **per-zoom-tier amplitude
  fade** and told the build to drop the item if that fade could not be made stable. A report-only spike measured
  the camera instead of assuming it, and the condition **does not arise**: `assetsPPU` is locked, so a ripple's
  footprint is 3.2 px at *every* framing. What varies is the ripple **count** (6× more sea on screen at the widest
  framing), so the fade is keyed to the **framing**, not to pixel size. Recorded as an amendment under #10 (the
  2026-07-30 ruling deferred it to this build) and pinned by `RipplePixelFootprintTests`.
- **Date:** 2026-07-28
- **Revision (2026-07-28, same day, PR #305 → follow-up):** the owner asked whether the plan delivers *"waves
  more variability, moving in different directions, ripples, variance in sizes, speed, building and collapsing."*
  Auditing each ask against the shipped shader exposed **two genuine gaps in the original eight** — nothing in the
  plan linked **wave speed to wavelength** (dispersion), and nothing added a **ripple/capillary band** at all. Both
  are now items **#9** and **#10**. The audit also found the shipped variability layers sitting well under their
  design defaults — but **deliberately so** (finding 4: the values are hand-tuned, and half of them are eased from
  the mood materials, not `Water.mat`). That adds a **zero-code tuning phase (P0)** ahead of everything — framed as
  a **revision of the owner’s own art direction, not a bug fix** — and **pulls #5 (the spectrum) up from last to
  second** because it is the keystone for what the owner actually asked for.
- **Decision owner:** `lead-architect` (render plumbing, the wave-field seam, the ADR-0018/0023 impact);
  `art-pipeline` owns the look (σ, colour, thresholds, all tunables).
- **Flagged from:** the owner (2026-07-28), reviewing which techniques from Unity's HDRP Water System are worth
  taking after we ruled out adopting the system itself (it is HDRP-only; we are URP 17.5 on the 2D Renderer):
  *"i maybe want that effect, as long as the end look gets pixelated, i can appreciate realness attributes …
  i think i want reflections too"*, then: *"make sure the plan includes all 8 elements."* This ADR records all
  eight, tiered by what they touch — plus **#9 and #10**, added the same day when the owner's variability
  question exposed them as gaps (see Revision above).
- **Related:** `0010-water-rendering.md` (the layered shader + nine shipped addenda — **this ADR revisits its
  eighth addendum's rejection of reflections**), `0018-shared-wave-field.md` (the field hulls ride),
  `0023-displaced-water-surface.md` (`DisplacedSea`, the calibrated iso-depth convention),
  `0022-3d-boat-hulls.md` (`IsoFacetHullFeature` — the RenderGraph precedent this plan depends on),
  `0014-painted-seabed-height-authoring.md` (the bake-over-a-world-rect pattern),
  `0015-water-palette-guard-rail.md` (the final colour owner), `0017-weather-driven-water-palette.md`
  (where turbidity lands), `0026-rig-pivot-conventions.md` (the mirror axis),
  `design/water-rendering.md` §5.12, §16, §17, §11.

---

## Context

Unity's Water System is not adoptable here — it ships in HDRP, and `Packages/manifest.json` pins
`com.unity.render-pipelines.universal` 17.5.0 on the 2D Renderer. Adopting it would mean an HDRP conversion that
takes out Light2D, the sprite shadows, `HiddenHarboursDayNight`, and the whole iso-facet hull stack. What *is*
worth taking is the physics it embodies. Eight candidates were identified against the shipped shader (3,209 lines,
nine addenda); #9 and #10 were added when the owner's variability question exposed them as gaps. This ADR decides
all ten.

### Three findings from the live material that shape the decision

Read from `Assets/_Project/Art/Materials/Water.mat`, not assumed:

1. **`_USE_DEPTHRAMP` is ON with a painted texture assigned, and `_DepthBands: 0`.** The base colour is a smooth
   lookup into a **hand-painted 1D LUT** over a linear depth axis (0.15 → 4.0 m,
   `HiddenHarboursWater.shader:2386`). A LUT is **strictly more general** than any closed-form absorption curve —
   anything `e^(−σd)` computes, the owner can already paint, per-channel, including non-physical shapes he prefers.
   `_DeepBlueStrength: 0.45` is standing evidence that the physical answer is *not* the wanted answer: that bounded
   pull toward navy exists precisely because the settled deep end wasn't what he wanted.
2. **`_ShallowTranslucency: 0`** — the §17 see-through shallows shipped in Arc C are **off**. This prop is
   mood-eased (finding 4a), so `Water.mat` alone would not settle it — but **no mood material carries a non-zero
   value** either (`Water_FoggySmother` sets it explicitly to 0; the rest do not override it). It is off everywhere.
3. **The seabed arrives ungraded and untintable.** §17.1 shows the bottom by *lowering `col.a`* so a seabed sprite
   drawn behind the sea plane bleeds through the `SrcAlpha OneMinusSrcAlpha` blend. The water shader never sees
   that colour, so it cannot absorb it; and a **scalar** alpha cannot express **per-channel** transmission at all.
   This is also why §17.3 documents see-through and caustics as partly cancelling, to be tuned around.

Finding 1 kills the obvious version of absorption. Finding 3 is where the physics does work no existing knob does.

### Finding 4 — the variability layers are built and set LOW, and that is deliberate art direction

The owner asked for *"more variability, moving in different directions, ripples, variance in sizes, speed, building
and collapsing."* Most of that is **shipped and set well under its design defaults**. The values are **not drift**
— the evidence says they were chosen. Two things must be established before anyone touches a number.

**(a) Which values actually run.** `_FbmStrength`, `_OceanSwellStrength`, `_OceanSwellSharpness` (and
`_ShallowTranslucency`) are in `WaterSurface.MoodFloatNames`, and the scene carries `_weatherPaletteEnabled: 1` —
so at runtime they are **eased across the eight mood materials in `Art/Materials/WaterPresets/`** (ADR 0017).
Their `Water.mat` values are **baselines that get overwritten**. Tuning them in `Water.mat` does nothing.

| Property | Design default | Where it lives | Live value |
|---|---|---|---|
| `_WindChop` | 0.4 | `Water.mat` (fixed) | **0.07** — the wind-chop octave at ~1/6 |
| `_Octave3Weight` | 0.3 | `Water.mat` (fixed) | **0.108** — the cross-swell (second direction) at ~1/3 |
| `_FoamEvolveSpeed` | 0.25 | `Water.mat` (fixed) | **0.1** — foam morphs 2.5× slower than designed |
| `_FbmScale` | 0.05 | `Water.mat` (fixed) | **3.26** — see (b) |
| `_FbmStrength` | 0.18 | **mood-eased** | 0.035 – 0.08 across all 8 moods |
| `_OceanSwellStrength` | 0.16 | **mood-eased** | 0.05 (fog) → **0.16 (storm — the design default)** |
| `_OceanSwellSharpness` | 1.4 | **mood-eased** | 6 (calm) → 2.5 (storm) |

**(b) `_FbmScale` = 3.26 is DELIBERATE — ruled, not open.** It is *not* mood-eased, so 3.26 runs everywhere. Git
shows it was already **3.27** before `f2574a4` (*"commit the owner’s water tuning as the baseline (2026-07-05
session)"*) and was nudged to **3.26** in that commit — the signature of a slider being dragged, not a default left
behind. It is a chosen value; the same commit moved `_OceanSwellStrength` 0.09 → 0.12. **Do not "fix" it.**

**(c) The mood ramp works as designed, and that reframes the ask.** `_OceanSwellStrength` runs 0.05 in fog and
**reaches the 0.16 design default at storm**; `_OceanSwellSharpness` broadens from 6 to 2.5 as the sea builds. The
swell layer is not turned off — it is **weather-scaled, and the sea has mostly been seen in calm weather.** Before
concluding the sea lacks variability, **look at it in a storm.** That one observation may resolve much of the ask
for free, and it costs a single weather override to test.

⚠️ `_Chop`, `_Roughness` and `_Flow` are **sim-pushed** (`WaterSurface.PushUniforms`); never tune them in a
material (`design/water-rendering.md` §12.1).

**This is why P0 exists — but P0 is a REVISION of the owner’s own art direction, not a bug fix.** Every one of these
numbers was chosen; `_FbmStrength` sits at 0.035–0.08 in **all eight** mood materials independently, which is about
as clear a statement of intent as a preset library can make. P0 asks the owner whether he wants to revise that now,
and it edits **the mood materials** for mood-eased props and **`Water.mat`** for the fixed ones. Same lesson as
`_DepthBands: 0`: **audit what is enabled, and where it lives, before building.**

### The determinism tax (this sets the tiering, not visual impact)

Anything that changes the **wave field** must be mirrored as a C# twin with headless tests, because hulls
**physically ride** it (ADR 0018 / 0023, `WaveFieldBridge`, `DisplacedSea`) — the field is gameplay, not dressing.
Anything touching only `col.rgb` / `col.a` is art-pass work with no sim risk. That gap is worth roughly 3× in
effort and risk, and it is why the order below is not the order of raw visual payoff.

### The owner's condition: pixelated output

ADR 0010 decision (2) already mandates pixelized world coords in every layer. Finding 1 adds a wrinkle: because
`_DepthBands: 0`, the water's **base** is currently a smooth gradient, so pixel character cannot be assumed to come
from the base ramp. **Every layer in this ADR therefore carries its own quantization control, defaulting ON.**
That is the concrete form of the owner's "as long as the end look gets pixelated."

---

## Decision

### Tier A — `col.rgb` / `col.a` only (no sim risk, no new plumbing)

**(1) #7 — Depth absorption applies to the TRANSMITTED SEABED, never to the water's own colour.** — **SHIPPED
(P4, 2026-07-29).** Built as decided; `design/water-rendering.md` §17.7 is the live spec (§17.1/§17.3 retired in
place). σ is factored as one mood-eased `_Turbidity` (1/m) × a fixed `_AbsorptionRatio`, which is what let the
"one turbidity parameter" ruling survive contact with `MoodFloatNames` being a **float** list.
The painted `_DepthRamp` stays the colour authority for the water body (finding 1). Absorption is applied to the
**bottom seen through the column**, which the LUT does not and cannot cover (finding 3). Two parts:

- **`_SeabedTex` becomes a shader input**, baked over the **same world rect** as `_HeightTex` — reusing the
  established `_HeightWorldMin` / `_HeightWorldSize` pattern (ADR 0014). The shader then composites the bottom
  **itself**, so `col.a` stays opaque, the alpha-blend dependency disappears, and **§17.3's cancellation dissolves
  by construction** rather than being tuned around.
- **Per-channel Beer-Lambert:** `T = exp(−σ_rgb · 2d)`, path `2d` because light descends and returns. σ (1/m,
  per-channel) is **one turbidity parameter** replacing today's two independently-tuned constants
  (`_ShallowSeeThroughDepth` = 0.6 m vs the ramp's 0.15/4.0 m). Red extinguishes first, which is what produces the
  characteristic shift with depth for free.
- **Pixelized + posterized:** the seabed sample uses the existing pixelize helper; `_AbsorptionBands` quantizes
  transmission into discrete steps, **defaulting ON**.
- `_ShallowTranslucency` (finding 2) is **superseded**, not revived — it and `_ShallowMinAlpha` retire when this
  lands. σ = 0 is an exact passthrough to today's look.

**(2) #2 — Caustics are driven by the shared wave field, not an independent noise.**
`_Caustic*` currently scrolls its own noise, so the shimmer on the seabed has no relationship to the swell visibly
rolling over it. Caustics are focused light: brightest where the surface is locally **convex toward the sun**.
Derive them from the local **curvature** of `WaveFieldSample()` (already available in HLSL, §16.2), keeping the
existing `_CausticDepth` gate and the `_CausticDayGate` sun gate. **No new uniform.** Cheapest real win on the list.

**(3) #3 — Foam gains a convergence (Jacobian) gate.**
Unity spawns foam where the surface **Jacobian goes negative** — where the surface pinches and folds — not merely
where it is tall. Our `_FoamCrestGate` + `WhitecapLifecycle` is the tall-wave version only, which is why crossing
trains never foam at their intersections. Add a convergence term from finite differences of the displacement field
and feed it as an **additional** gate alongside the existing crest factor. This is what produces a confused sea.

**(4) #4 — Band wavelengths scale with sea state.**
`_NoiseScale`, `_WindChopScale`, `_CrossSwellScale`, `_OceanSwellScale` are fixed. Real seas grow in **wavelength**
as they build, not only in amplitude, so today a storm reads as the same-sized water moving harder. Scale each
band's spatial frequency by a curve in the already-pushed `_Chop`. **Visual octaves first (Tier A);** promoting the
same law into `_WaveFieldParams` is Tier B and is deliberately deferred to (6).

**(4b) #9 — Wave speed is DERIVED from wavelength (dispersion), not hand-set per octave.**
Today each octave carries an independent, hand-tuned speed — `_WindChopSpeed` 0.09, `_CrossSwellSpeed` 0.025,
`_OceanSwellSpeed` 0.018 — with **no relationship to its wavelength**. Real water disperses: long waves travel
faster than short ones, `c = √(gλ/2π)` in deep water. Unrelated rates are a large part of why a multi-scale sea
reads as **stacked layers sliding over each other** rather than one body of water.

- **Derive each band's speed from its own wavelength**, through a single master `_DispersionScale` (a game-feel
  scalar — true ocean speeds at our world scale would read wrong), keeping a per-band multiplier so the owner
  retains art direction (rule 6). Setting `_DispersionScale = 0` falls back to today's independent speeds exactly.
- **Shallow water uses the depth-limited form** `c = √(g·d)` off the **same read-only `depth`** every other layer
  already consumes. Waves therefore **slow and bunch as they approach shore** — which is the physical *cause* of
  the effect §5.12's shoreward bias hand-builds. Dispersion does not replace that bias; it earns part of it.
- **Pairs with #4** — #4 sets each band's wavelength, #9 then sets its speed. Building #4 without #9 leaves the
  sea-state response half-wired: waves that grow longer but do not speed up.
- **Tier A while it drives the visual octaves; Tier B when promoted into `_WaveFieldParams`** — same discipline as
  #1 and #4, and the promotion rides with (6).

**(4c) #10 — A capillary RIPPLE band, gated by wind and by wave steepness.** — **SHIPPED (P3, 2026-07-31).**
The finest band in the shader is `_WindChopScale` at 0.7 — that is **chop, not ripples**, and nothing in the
original eight adds one. Ripples are what makes water read as *water* close up. Add a short-wavelength octave
(~0.08–0.15 m) riding **on** the larger waves:

> **Amendment (#388, 2026-08-01):** the shipped wavelength is **0.17 m** — outside the band above, and
> forced: at the shader's shipped sampling grid (`_PixelsPerUnit` 24 → 4.17 cm cells) *nothing* in
> 0.08–0.15 m clears the ≥4-cells-per-cycle moiré floor (0.15 m = 3.6 cells). 0.17 m = 4.08 cells is the
> closest honest value to the band's intent.

- **Gated by wind** (`_Roughness`) — no wind, no ripples, glass stays glass — and by **local wave steepness**, so
  ripples sit on the **windward faces** of larger waves and are absent in their lee, which is what wind ripples
  actually do. The steepness term is the slope of the existing field; no new uniform.
- **Amplitude-capped to read as surface TEXTURE, not displacement.** This band must never enter the field hulls
  ride — a ripple is not a force. Tier A, permanently.

> ⚠️ **This is the most alias-prone layer in the shader, and the pixel grid is why.** At PPU=32 one pixel ≈ 3.1 cm,
> so a 0.10 m ripple is ~3 px — resolvable, but barely. It must be quantized on the **world** grid (the crawl law)
> and dithered at its threshold, or it degenerates into shimmer. ~~**And it must fade out with camera zoom:** at a
> wider zoom tier a ripple falls below one pixel and becomes pure aliasing, so its amplitude scales down per
> discrete zoom tier. Prototype the zoom fade before committing to the band — if it cannot be made stable across
> the zoom tiers, this item is not worth shipping.~~ **The zoom half of this warning is RETIRED — see the
> amendment below.** The world-grid quantization and the threshold dither stand exactly as written; both shipped.

#### Amendment (2026-07-31, at the #10 build) — the kill condition does not arise; a FRAMING fade replaces it

The 2026-07-30 ruling deferred this amendment to the #10 build, on the evidence of a report-only spike
(`spike/ripples-zoom-fade`, unmerged). Two findings, both now pinned by `RipplePixelFootprintTests`
(EditMode, headless):

**(1) The sub-pixel kill condition describes a camera this game does not have.**
`PixelPerfectCamera.assetsPPU` is **locked at 32** (`ArtCameraSetup`, mirrored by `CameraFollow.AssetsPPU`) and
`CameraFollow` frames a tier by changing the **reference resolution**, never the world-metres-per-pixel ratio
(`upscaleRT` is never set; `gridSnapping = PixelSnapping`). One pixel is therefore 1/32 m = 3.125 cm at **every
framing the game has** — the 5.625 m live-haul framing through the 90 m Coastal Packet — and a 0.10 m ripple is
**3.2 px throughout**. It never goes sub-pixel. **There is no per-tier amplitude fade to build, and #10 is not
dropped.** The original warning assumed a camera that changes orthographic size at a fixed screen resolution.
Because the finding rests entirely on `assetsPPU` staying locked, it is **pinned, not merely recorded**: the
fixture goes red the moment the footprint stops being tier-invariant.

**(2) The real risk is COUNT, not size — so the fade is keyed to the FRAMING.**
What varies across the tiers is how much sea is on screen: 5.625 m at the tightest, 33.75 m at the widest, so the
widest frame carries **6× more ripple cycles down-screen** (56 → 338). At the tight framing the band reads as
texture on the swell; at the wide it reads as a dense field competing with the swell bands. **The constraint that
replaces the ADR's:** amplitude fades with **how much sea the camera frames**, not with pixel size —
`_SeaFramingHeight`, a physics-push global (metres of sea on screen), read through
`WaterRipple.FramingFade01(framing, near, far, floor)`. It is a **push, not a mood float**: it is derived from the
camera, so it must never enter `WaterSurface.MoodFloatNames` (the double-drive trap). The ADR's threshold dither is
unaffected and still right.

**What did not change.** #10 stays **Tier A permanently** (`col.rgb` only; never `depth`/`clip()`/`_WaterLevel`/the
height read/the field hulls ride), world-grid quantized (the crawl law), threshold-dithered, and off by default
until the owner dials it in.

### Tier B — the shared wave field (C# twin + headless tests mandatory)

**(5) #1 — Wind fetch, read from the height map.** — **SHIPPED (P6, 2026-07-31), and TIER B from the
start.** Built as decided except for the phasing: the visual-first intermediate below was **not built**.
The ADR 0018 amendment (2026-07-31) carries the reasoning and is the live spec; the short version is that
a visual-only fetch is the `_OceanSwellScale` incident by construction — glass drawn behind the headland,
open-water swell still boarding the boat in it — so the envelope landed in the one field both consumers
read, with every field consumer and both shader stages wired in the same commit. Ships **OFF**
(`WaveFetchSettings.Strength` 0 = exactly 1 = the byte-identical passthrough); the owner dials it in, and
**owes a feel verdict** before it goes further, exactly as the spectrum did.
Fetch — how far wind has blown over open water — sets wavelength and amplitude, and it is the single most
gameplay-legible item on this list for an island game: **lee shores go calm, exposed shores build**, visible before
it is felt. Read it in-shader by marching upwind along `−_WindDir` across `_HeightTex`, counting water samples
until land or a cap. Precedent: `ShoreDir` already central-differences `SeabedElevation` in-shader (§5.12).

> ⚠️ **Implementation constraint:** the march must be a **fixed iteration count**. `WaterShaderCompileGuardTests`
> guards the magenta class, and `[unroll]` over a runtime bound is one of its known traps.

Phased deliberately: **fetch modulates the visual layers first** (Tier A cost), and is promoted into the field
hulls ride only in the later phase, where it earns a twin and tests. Visual-only fetch means the player *sees* the
lee before the boat *feels* it — an acceptable intermediate, and an explicit one.

**(6) #5 — Spectrum-shaped swell (JONSWAP weighting + wave groups).**
The largest visual gap and the honest reason people reach for FFT: a handful of sine trains repeats at large scale
and never produces **sets**. Replace the flat train weighting in `_WaveFieldParams` with a **JONSWAP-shaped
amplitude distribution** across N trains plus **directional spreading** (`cos^2s(θ − θ_wind)`), and add **wave
grouping** — the beat between neighbouring frequencies — which is what gives three big ones then a lull.

**Explicitly NOT an FFT.** A weighted train sum stays deterministic, twinnable in C#, and cheap. Twinning an FFT
headless is the trap; the value is the **spectrum shape**, which a train sum can carry.

This changes what hulls ride. It requires a `WaveFieldBridge` twin, headless determinism tests, and an ADR 0018
amendment. Treat the boat-feel change as a **first-class outcome to be verified by the owner**, not a side effect.

### Tier C — new render plumbing

**(7) #8 — Reflections: a filtered renderer list into an RT, wave-warped by the water shader.** — **SHIPPED (P5, 2026-07-29).** Built as decided; `design/water-rendering.md` §26 is the live spec. Two things the decision text could not have known, both recorded there: the mirror axis had to be **published per renderer** (the SpriteRenderer identity-matrix trap — every sprite, not an edge case), and the pre/post-grade split for night-lit sources needs **no flag channel**, because premultiplied output makes "rgb exceeds coverage" mean exactly "compensated light content".

ADR 0010's eighth addendum **rejected** reflections. That rejection is revisited here on a **new fact**, which is
the only thing that justifies reopening an ADR. It read: a reflection pass "would need a second camera + render
target wired into the 2D URP renderer (**unverifiable here**) and a second draw of the scene."

Since then **ADR 0022 phase 3 shipped `IsoFacetHullFeature`** — a working RenderGraph injection into the 2D
renderer, registered in `Renderer2D.asset`, with per-camera `RTHandle`s, LightMode-filtered renderer lists
(`HHHullFacet` / `HHHullDeck` / `HHWater`), a globally-bound resolve texture, and an explicit **zero-cost-when-idle**
contract. The wiring is no longer unverifiable — it is in the repo and under test. And the cost is not a second
scene draw: it is **one filtered list of near-water objects**.

- **New LightMode `HHReflect` → `_HHReflectTex`**, a fourth renderer list joining the existing recording, honouring
  the same zero-cost-when-idle contract (no reflective renderer ⇒ nothing enqueued).
- **The mirror axis is the ground-contact pivot** — the ADR 0026 convention — applied in the vertex stage. For mesh
  hulls it is the calibrated iso-depth waterplane ADR 0023 already computes. The pivot *is* the waterline contact,
  so the convention we already settled is exactly the axis a reflection needs.
- **The water shader samples `_HHReflectTex` with the UV warped by `WaveFieldSample()`** — so reflections wobble on
  the **same crests the hull is riding**. This is the payoff, it reuses ADR 0018, and it needs **no new uniform**.
  It is also the reason this beats per-object mirrored duplicates: one place to do the warp.
- **Sea-state response is already built.** Reuse the shipped `WaterReflection.ReflectionStrength()` /
  `ReflectionSharpness()`: sharp on glass, broken in chop, gone in a storm. Object reflections inherit the P1 mood
  behaviour for free.
- **Pixelation — the lookup snaps in WORLD space, not RT/screen space.** The RT inherits the camera's render
  resolution and is point-filtered, but that alone is *screen*-locked: with `CameraFollow` panning continuously
  behind the boat, a screen-snapped reflection lookup **crawls** on every pan. The warped sample coordinate is
  therefore quantized on the **world** PPU grid (the `Pixelize` helper), so a reflection cell belongs to a place on
  the water and stays there as the camera moves — the same zero-crawl-by-construction law the shader's world-locked
  Bayer dither and the ADR 0022 facet pass already hold. See "Pixelation" below.
- **Composition:** over the §11 sky mirror (a boat reads on top of reflected cloud), under the foam (whitecaps read
  on top of the boat). Pre-grade, so it dims with night like the rest of the sea — **except** night-lit sources,
  which ride the §11.6 post-grade compensation exactly as the moon glitter does, or a lit wheelhouse at night will
  be crushed to ~3% by the multiply overlay.

**(8) #6 — An advected foam buffer for wake.**
Unity's foam generators write into a buffer that **advects and decays**. That *is* the "trail left behind you"
architecture, versus attaching a trail to the boat. A persistent RT, ping-ponged per frame: advect along the
existing `FoamDriftDir` (the wind/current blend), decay, inject where hulls pass; sampled by the water shader as a
mask that **adds** to the existing foam rather than replacing it.

> ⚠️ **Its cells are anchored to the WORLD PPU grid, with camera-relative *addressing* only.** A wake is a mark
> left on a place in the sea; if the buffer's cells are camera-relative the whole trail crawls under every pan —
> the one artefact that would make it read as a screen filter instead of a wake. The scroll-on-camera-move must
> therefore be in whole world cells. Same law as the reflection lookup (see "Pixelation" below).

> ⚠️ **This intersects the dynamic-wake work already in flight.** Deciding the buffer architecture *after* those PRs
> land is materially more expensive than deciding it now. This item is sequenced early for that reason alone, not
> because its visual payoff outranks the others.

> ~~✅ **RULED (owner, 2026-07-29): DEFERRED to the fleet era.**~~ **SUPERSEDED by the owner's 2026-08-01
> ruling — see the amendment below. The deferral block is kept for the record; it is no longer in force.**
> The dynamic-wake PRs this item was racing landed
> first, and they deliver the architecture goal — the shipped `BoatWakeEmitter` trail is **deposited in the world**
> where the hull passed, **advects with the current**, and **decays in place** (pooled sprites, not an RT). What a
> buffer still adds — wakes that **merge** across boats, unbounded persistence at fixed cost, shader-level blending
> with the field foam — are multi-boat payoffs, and M1 sails one boat. The early-decision rationale above is
> therefore moot: the retrofit this item was racing has already happened, benignly. **Re-opens when multiple hulls
> sail at once** (M2/M3 fleet/automation); the emitter's world-space deposit events are the intended injection seam,
> so nothing rots while it waits. Options note: `dev/NOTE-2026-07-29-adr0027-item6-wake-buffer.md` (option A taken;
> option B — the buffer as the trail's persistence medium, sprites keeping only the young churn band — is the
> recorded shape for the fleet-era build).

#### Amendment (2026-08-01, at the #6 build) — the deferral is overturned, and the ask gained a LOOK-TARGET

**The ruling, verbatim.** The owner reopened this the same day, in these words:

> *"I want foam to move to now as well. The hull doesn't create realistic foam when bobbing etc."*

The first sentence overturns the 2026-07-29 deferral: #6 builds now, in M1, one boat. The second sentence is
the more consequential half, because **it is a look-target the original decision text never had.** Everything
above frames #6 as an *architecture* upgrade to a wake — merging trails, unbounded persistence, shader-level
blending — all of which are multi-boat payoffs, which is exactly why deferring it was defensible. None of that
is what the owner is looking at. He is looking at a hull **working against the water**: bobbing at anchor,
rolling in a chop, slapping at the waterline. That case is served by **nothing today**, and it is not a fleet
problem — it is visible on one moored dory.

**Why `BoatWakeEmitter` cannot cover it, stated precisely.** The emitter keys on **speed**
(`WakeLineGeometry.SpeedOnset` is 0 at rest, by design and correctly). A moored boat has no speed, so the
emitter is silent — as it should be, for a *wake*. The missing signal is the hull's **vertical motion relative
to the local wave surface**, which nothing in the project reads. That is the gap this item now exists to fill,
and it is why the deferral's "the retrofit already happened, benignly" reasoning does not carry: the emitter
delivered the *trail* architecture goal, not the *churn* one.

**What was built.** The decision's shape above, unchanged, plus the second injection channel the owner's
sentence demands:

- **Injection reads BOTH components of hull-vs-water relative motion.** *Horizontal:* speed through the water
  (world velocity minus the tidal current, so a boat carried along by the stream churns nothing). *Vertical:*
  |relative heave rate| — the rate at which the gap between hull and wave surface opens or closes. A hull that
  tracked the surface perfectly would displace nothing, so **the churn is the hull's inertia losing the race
  with the wave face**; `FoamInjector` samples the same displaced sea the ride already samples (through the
  `DisplacedSea` Core seam, at the surface's own `FreqScale`) and models the hull's response as a first-order
  lag. Response time 0 ⇒ perfect tracking ⇒ the bob channel is identically silent, which is a real off switch.
  ⚠️ It **reads** the ride and never writes to Boats — no force, no pose, no heave — and holds no reference to
  any boat class, so a buoy or a raft can carry one later.
- **The slap shaping is super-linear** (`FoamBuffer.Shape01`, exponent default 2.5): a hard slap churns
  *disproportionately* more than a gentle rise-and-fall, which is the owner's distinction made numeric rather
  than a steeper straight line. Knees, exponents and weights are tunables (rule 6), never literals.
- **The cell law was taken further than the warning above asks.** Not only the camera scroll but the **wind
  drift too** is reduced to whole world cells, with the sub-cell remainder banked. That is not extra rigour for
  its own sake: a fractional scroll must be *resampled*, and resampling a buffer into itself every frame is a
  blur filter — the wake would smear into a smudge within seconds. Whole-cell moves are exact integer copies.
- **Its own grid constant** (`FOAM_CELLS_PER_UNIT` = 8 cells/m = 4 screen px at PPU 32), never the material's
  `_PixelsPerUnit`. This is not a style preference: **`Water.mat` ships `_PixelsPerUnit` at 24**, not 32 — the
  owner has dragged that slider — and the C# twin cannot read a material, so quantizing through it would let
  the two halves of the seam disagree silently. Same ruling, same reason, as `FETCH_MARCH_PPU`. It is also
  deliberately *coarser* than the pixel grid, which is the Pixelation section's own "foam coarser than
  caustics" scale hierarchy taken up rather than ignored.

**The determinism boundary, stated honestly (this is new, and it is a real exception).** The buffer is
**accumulated VISUAL state**. It is *not* a deterministic function of `(worldSeed, gameTime)`, and it is
**allowed to differ run-to-run** with frame pacing — exactly as particles are. It therefore:

- feeds **no simulation** — nothing in Boats, Fishing, Economy or the wave field may read it;
- enters **no save** (rule 5 / ADR 0008), and needs none: it is regenerated by sailing;
- is read by **the water shader's foam compose and nothing else**, which is the invariant that keeps the
  exception contained. Every function in the `FoamBuffer` twin is nonetheless pure and deterministic in its own
  arguments — that is what makes the maths testable headless even though the accumulation is not.

This is the first place in the realness pass where "deterministic from seed and time" is knowingly not held,
so it is recorded as a boundary rather than left to be discovered.

**`BoatWakeEmitter` STAYS, and the two ADD.** The emitter is the young, bright churn at the stern; the buffer
is the mark left on the sea that persists and drifts after the boat has gone, plus the bobbing case. Whether
the emitter's trail should later be *thinned* in favour of the buffer is an **owner look call** and is
deliberately not pre-empted here.

**Ships OFF, both gates.** `_WakeFoamStrength` = 0 in `Water.mat` and all eight presets, and nothing in the
shipped scenes carries a `FoamInjector` — so there is no "buffer off" branch to keep byte-identical, only an
absent pass. That absence **is** the passthrough proof, the same shape as #8's.

---

## Pixelation — the owner's condition, made concrete per item

### Why per-layer WORLD-space, and not one pixelize at the end (owner question, 2026-07-28)

Asked directly: *"can pixelation just happen at the end? instead of every step? would that lead to better results?"*
Recorded here because the answer is not obvious and the question will recur.

**End-stage pixelation already happens** — every scene runs a `PixelPerfectCamera`, so the frame is rendered at a
fixed low internal resolution and upscaled. The per-layer snap is not a second copy of that; it solves a different
problem.

**Screen-space quantization is locked to the camera; world-space is locked to a place in the sea.** With
`CameraFollow` panning continuously behind the boat, a field computed at full precision and quantized only at the
end **crawls**: the world→pixel mapping slides underneath every feature and edge pixels flip on and off. Snapping
the *sample coordinate* in world space (the `Pixelize` helper, `floor(p·ppu)/ppu`) means a foam cell belongs to a
spot on the water and stays there while the camera moves. The shader already states this law for its world-locked
Bayer dither — *"world-derived dither cannot crawl under camera translation … zero crawl by construction"* — and it
is the same discipline as the ADR 0022 facet pass and the boat rigs.

**It has also been measured.** The dither comment records a spike where the smooth-then-quantize approach
*"dissolves the quantised bands back into a smooth gradient and the surface reads as airbrushed 3D, not this
game (spike run-1, measured)."* That is the failure mode a blanket end-stage switch would reintroduce.

**What the question does correctly identify:** every layer currently pixelizes onto the *same* grid, so all feature
edges land on shared cell lines — part of why the sea can read as one blocky field rather than elements at distinct
scales. The remedy is **not** end-stage; it is (a) **deliberately different grids per layer** (foam coarser than
caustics), and (b) **edge-window dither** to recover sub-cell detail. Both mechanisms exist already
(`_CapDitherBand`, `_EnvelopeBandDitherWin`) but are not used as an explicit scale hierarchy. Treat that as the
follow-up this question earns, tracked against M3-18.

**The consequence for Tier C:** #8 and #6 introduce the project's first *render targets* in the water path, and a
render target is screen-space by default — exactly the crawl trap. Both must quantize in **world** space (see their
decisions above). This is the one place where "pixelate at the end" would have silently been the wrong call.

| Item | How the output is pixelized |
|---|---|
| #7 absorption | Seabed sampled on pixelized world coords; `_AbsorptionBands` quantizes transmission (**default ON**) |
| #2 caustics | Curvature sampled on the existing pixelized grid; keeps the current caustic quantization |
| #3 convergence foam | Feeds the existing foam threshold, which is already banded/dithered |
| #4 band scaling | Scales frequency only — the pixelize step is downstream and unchanged |
| #1 fetch | Fixed-step march on pixelized coords (the crawl law, mirrored by the C# twin so both sides march the same points); the band is carried as `_WaveFetchParams2.y` but **defaults OFF** — see below |
| #5 spectrum | Field is quantized where it is read, exactly as today |
| #9 dispersion | Changes speed only — no new sampling, so the pixelize step is untouched |
| #10 ripples | World-grid quantized (`Pixelize`) **and** posterized into solid steps with a Bayer-dithered window at each step edge (`_RippleBands` / `_RippleDitherWin`, **default ON**). ~~amplitude faded per discrete zoom tier~~ → **faded by the FRAMING** (`_SeaFramingHeight`): the footprint is tier-invariant, the CYCLE COUNT is not (see the #10 amendment) |
| #8 reflections | RT at camera render resolution, point filter, warped lookup snapped to the **world** PPU grid (screen-snapping crawls on every pan) |
| #6 wake buffer | Cells anchored to a **world** grid; camera-relative addressing only, scrolled in whole world cells — **and so is the wind drift**, remainder banked, because a fractional scroll must be resampled and resampling a buffer into itself every frame is a blur filter. On its **own** constant (`FOAM_CELLS_PER_UNIT` = 8/m = 4 px), deliberately coarser than the pixel grid and deliberately **not** `_PixelsPerUnit` (which ships at 24). Value posterized + edge-dithered on the shared world-locked Bayer cell, **default ON** |

---

## Phasing

**Re-ordered 2026-07-28** after the owner named wave variability as the priority. #5 moves from last to second,
because it is the keystone for *variance in sizes*, *directions* and *building and collapsing*. A new **P0** goes
ahead of everything, because it is free and it tells us how much of the ask is already paid for.

| Phase | Items | Tier | Why here |
|---|---|---|---|
| **P0** | **Tuning pass — no code.** ① **First: view the sea in a STORM** (finding 4c) — the swell already ramps to its design default there. ② Then, if still wanted: raise `_WindChop` / `_Octave3Weight` / `_FoamEvolveSpeed` in **`Water.mat`**, and `_FbmStrength` / `_OceanSwell*` in the **eight mood materials**. ⛔ `_FbmScale` is ruled deliberate — leave it | — | **Free.** Separates "missing" from "weather-scaled" from "deliberately low" **before** committing to the riskiest item. ⚠️ This **revises the owner’s tuning**, so it is his call, not a fix. |
| **P1** | #2 caustics, #3 convergence foam, #4 band scaling, **#9 dispersion** (all visual) | A | Cheapest, no sim risk, no plumbing. #9 pairs with #4 — wavelength and speed must land together. |
| **P2** ✅ | **#5 spectrum + grouping** ⬆ (was P6); ~~#4/#9 promoted into `_WaveFieldParams`~~ | B | **Pulled up, and SHIPPED** — PR A widened the field 4 → 8 trains (the ADR 0018 amendment); PR B added the JONSWAP weighting, the `cos^2s` fan and grouping behind `SpectrumBlend` (**default 0** — the passthrough discipline; the owner dials it in). ⚠️ **The #4/#9 "promotion" turned out to be an AUDIT RESULT, not code**: the field's trains already disperse (`WaveTrain.PhaseSpeed` *is* the relation) and already grow λ with wind, so applying the visual-octave laws there would double-apply them **and** move drawn geometry the interior-mask/clamp stack guards. Recorded in the ADR 0018 amendment so it is not re-litigated. **The owner's feel verdict is pending and gates P3.** |
| **P3** ✅ | **#10 ripples** — **SHIPPED 2026-07-31** | A | After #5 deliberately — the ripple band rides the *spectrum's* waves (its windward gate reads `WaveFieldSample`'s slope), not the octaves #5 replaces. Unblocked by the owner's feel verdict on the tuned sea (2026-07-31, the #372 baseline). The item's kill condition was retired on spike evidence, not waived — see the #10 amendment. Ships **OFF** (`_RippleStrength` 0); the owner dials it in. |
| **P4** ✅ | #7 absorption + `_SeabedTex` bake — **SHIPPED 2026-07-29** | A | Self-contained; retires §17.1/§17.3 rather than tuning around them. Landed out of phase order (P2/P3 still open) precisely because it depends on nothing above — the independence the table already claimed. |
| **P5** ✅ | #8 reflections (`HHReflect` list, pivot mirror, wave warp, composition) — **SHIPPED 2026-07-29** | C | The owner's second explicit ask; depends on nothing above — which is why it landed with P2/P3 still open. |
| **P6** ✅ | **#1 fetch** — **SHIPPED 2026-07-31** | ~~A→B~~ **B** | ~~Visual first; promotion earns a twin.~~ **The visual-first step was dropped on evidence** (the `_OceanSwellScale` incident): a shader-only damp IS seen ≠ felt. Landed Tier B in one commit — C# twin, headless tests, ADR 0018 amendment — and ships OFF. |
| **P7** ✅ | **#6 advected foam buffer** — **SHIPPED 2026-08-01** | C | ~~⏱ Parallel; DEFERRED to the fleet era 2026-07-29~~ → **the owner overturned the deferral on 2026-08-01** and, in doing so, gave the item a look-target it never had: *"the hull doesn't create realistic foam when bobbing."* Bobbing churn is a ONE-boat defect, not a fleet payoff, and nothing serves it — `BoatWakeEmitter` keys on speed and a moored dory has none. Landed after #8 by ruling (both are render targets on the same feature and the same shader region, so they are serial, never parallel). Ships **OFF** (`_WakeFoamStrength` 0 **and** no `FoamInjector` in any scene); the owner dials it in. See the item's amendment. |

**The cost of this re-order, stated plainly.** Pulling #5 to P2 brings the Tier B risk forward: it changes what the
hulls ride, so the ADR 0018 amendment, the C# twins and the **owner feel verdict** all land *before* absorption and
reflections do. That is the right trade if wave variability is the priority — but it means the visible payoff of
P4/P5 arrives later, and a bad feel verdict at P2 stalls the queue behind it. P0 exists partly to de-risk exactly
that: if the knobs deliver most of the ask, #5 can be scoped smaller or deferred again on evidence.

P1, P4, P5 and the parallel #6 are independent across lanes. P2→P3 are serial and gated on the ADR 0018 amendment.

---

## Determinism, save & performance (the invariants held)

- **Every item is visual-only except P2 (the spectrum) and P6 (fetch).** #2/#3/#4/#7/#8/#6/#9/#10 touch `col.rgb` / `col.a`
  (and, for #6/#8, their own render targets) and **never** `depth`, `clip()`, `_WaterLevel`, the height read, or
  the sim. Nothing enters the save (rule 5 / ADR 0008). **#10 is Tier A permanently** — a ripple is surface
  texture, not a force, and must never enter the field hulls ride.
- ⚠️ **#6 is the ONE knowing exception to "deterministic from `(worldSeed, gameTime)`", and it is bounded.**
  The foam buffer is **accumulated visual state**: it is allowed to differ run-to-run with frame pacing, exactly
  as particles are. It feeds no sim, saves nothing, and may be read by **the water shader's foam compose and
  nothing else** — that last clause is what contains the exception. Its maths (`FoamBuffer`) is nonetheless pure
  and deterministic in its own arguments, which is what keeps it testable headless. Full statement in the #6
  amendment.
- **#5 and #1 do change the field hulls ride** (the #4/#9 "promotions" turned out to be an audit result, not code — see P2). They are deterministic functions of
  `(worldSeed, gameTime)` + authored height, recomputed and never saved — but they require C# twins, headless
  determinism tests, and an **ADR 0018 amendment**. This is stated as a gate, not a footnote.
- **Rule 6 throughout:** every new constant is a material property. Every item defaults to **passthrough**
  (σ = 0, strength 0, bands off where they would change tuned values), so the shipped `Water.mat` look stays
  byte-identical until the owner dials each in — the discipline every ADR-0010 addendum has kept.
- **Rule 7:** #8 and #6 each add one filtered list / one RT, both honouring the existing zero-cost-when-idle
  contract. The `HHReflect` list needs a **distance-or-layer rule** so it stays small (see open questions).
  #6's budget, measured at its 96 m default: **768² × R8 × 2 (ping-pong) = 1.1 MB per camera**, one fullscreen
  blit per frame over a fixed 8-slot injection loop — against the seabed bake's 1.0 MB per region. Its
  resolution is **derived** from the window extent (`extent × cells-per-metre`), so "one texel = one world cell"
  holds by construction and cannot be broken by typing a different number. A single channel is all a coverage
  mask needs, which is what keeps the later mobile port affordable. **Both** of its gates are shut when idle:
  no hull churning water (every `FoamInjector` unregisters off the water) **or** the owner's dial at 0 ⇒ no
  target, no blit, nothing recorded.

## Test & CI guards

- **New pure twins, headless:** `WaterAbsorption.Transmission(σ, depth)` / `BandTransmission` (monotone decreasing;
  per-channel ordering — red extinguishes before blue; σ = 0 exact passthrough; the 2× path applied; banding
  quantizes). `WaterFoam.Convergence`. **`WaveFetch`** (in **Core**, not `Art` — the sim consumers reach it and Boats references only Core; the name below predates the Tier-B ruling): `Fetch01` (open water = 1, land upwind = 0, land SHADOWS the water behind it, monotone as you leave a lee, upwind ≠ downwind, the tide cannot pop the shore gate), `Amplitude01`, `Band01`, `Envelope01` (strength 0 = EXACTLY 1), plus `MarchStepCount_MatchesTheShader` — the tripwire that reads `FETCH_MARCH_STEPS` out of the shader source so the fixed-`[unroll]` seam cannot drift. Spectrum weighting + grouping twins for **P2**.
  **`WaterDispersion.PhaseSpeed(λ, depth)`** — monotone increasing in wavelength (long waves outrun short ones),
  the shallow branch slows toward zero depth, deep and shallow forms agree at the transition, and
  `_DispersionScale = 0` reproduces today's independent per-octave speeds exactly.
  **`WaterRipple`** — ⚠️ the per-zoom-tier amplitude fade this line originally demanded **does not exist and must
  not be built** (the #10 amendment: the footprint is tier-invariant, so there is nothing to key it to). What
  shipped instead: `WindGate01` (zero at/below the onset, monotone, saturating), `WindwardGate01` (ripples on the
  windward faces, the lee floor behind a crest, gate 0 = everywhere), `FramingFade01` (**the anti-density guard** —
  full at a tight framing, → the floor at a wide one, and **1 when the global is unset**, so a bare material never
  silently renders blank), and `Band01`/`RippleValue` (the posterize + edge dither). Plus
  `RipplePixelFootprintTests`, the tripwire that pins the tier-invariance the whole design rests on.
  **`FoamBuffer`** (#6, shipped) — `WorldCellOrigin` (the cell law: unmoved by a sub-cell pan, whole cells
  otherwise), `SourceOffsetCells` (a mark stays on its patch of water across a pan, and travels downwind by
  exactly the cells the drift earned — the sign tripwire), `AdvectCells` (the sub-cell remainder is banked, so
  no drift is lost in either direction), `DecayFactor` (halves at the half-life, composes so lifetime is
  frame-rate independent, half-life 0 kills rather than preserves), `Shape01` (zero at rest, monotone,
  saturating, and **super-linear above exponent 1** — the owner's slap-vs-swell distinction as a number),
  `Injection01` (either channel alone can churn — the bobbing case is asserted directly), `FollowSurface` /
  `RelativeHeaveRate` (**response time 0 ⇒ identically zero churn**, the invariant proving the signal is
  relative motion and not a re-read of wave height; and a short chop churns >2× a lazy swell of the same
  height). Plus the two **measured sabotages** the item is judged on — drop the world-cell snap and the crawl
  is reported as cells of slide across a sub-cell pan; break the half-life and the trail is reported as still
  at full strength after two minutes — and the `FOAM_CELLS_PER_UNIT` / `FOAM_MAX_INJECTORS` tripwires that read
  the advect shader's source, plus a guard that the pass never reaches for `_PixelsPerUnit` (which ships at
  **24**, not 32).
- **`WaterShaderCompileGuardTests` continues to force-compile the shipped variant** — no `+` in any `[Header]` or
  property string, **no `[unroll]` over a runtime bound** (directly relevant to #1's march). The magenta class stays
  guarded.
- ⚠️ **CI has no graphics device.** Rendering tests crash the editor rather than failing cleanly. Any pass-level test
  for #8/#6 must follow the existing `IsoFacetUrpPassTests` pattern; all new math stays headless.
- ⚠️ **A runtime-created shader is INVISIBLE to the magenta guard** (found at #6). `WaterShaderCompileGuardTests`
  discovers shaders through the **materials** that reference them, and the foam advect material is built by
  `CoreUtils.CreateEngineMaterial` — so no material asset points at it and it would never have been compiled by
  CI. `FoamBufferPassTests` force-imports it and reads `ShaderUtil.GetShaderMessages` directly. **Any future
  `Hidden/` pass shader has the same hole and needs the same treatment.**

## Rejected alternatives

- **Adopting Unity's Water System.** HDRP-only; we are URP 2D. Adoption means a pipeline conversion that removes
  Light2D, sprite shadows, and the iso-facet hull stack — and lands a photoreal 3D ocean into a PPU=32 top-down
  game whose ADR 0010 decision (2) mandates the opposite.
- **Beer-Lambert driving the water's own base colour.** The painted `_DepthRamp` LUT is strictly more general
  (finding 1); this would remove owner control and add no expressiveness. `_DeepBlueStrength: 0.45` shows the
  physical answer was already overridden by hand.
- **Keeping §17.1's alpha-blend see-through.** A scalar alpha cannot express per-channel transmission, and the
  bottom arrives ungraded (finding 3). `_SeabedTex` is what makes absorption possible at all.
- **Screen-space reflections.** The 2D renderer has no scene depth/normals to march, and screen-space loses
  off-screen sources — a boat's reflection would pop at the screen edge.
- **A planar reflection camera re-drawing the scene.** ADR 0010 addendum 8's rejection, still correct on cost. The
  filtered-list approach is what changed, not the verdict on a full second draw.
- **Per-object mirrored sprite duplicates.** No single place to warp by the wave field, and it doubles renderer
  count and sorting complexity.
- **An FFT ocean spectrum.** The value is the spectrum *shape*, which a weighted train sum carries; the FFT itself
  is what makes headless twinning intractable.

## Open questions (for the owning lanes, before each phase starts)

- ~~**`_SeabedTex` bake budget**~~ **ANSWERED at P4** (`design/water-rendering.md` §17.7, measured): **512²
  RGBA32, point-filtered, no mips, uncompressed = 1.0 MB per region**, against `_HeightTex`'s 192² R8 = 36 KB.
  Over St Peters' 160 × 120 m rect that is 0.31 × 0.23 m per texel (≈ 10 × 7.5 screen px at PPU 32) — posterized
  further downstream by the world-grid pixelize and `_AbsorptionBands`. 256 → 0.26 MB, 1024 → 4.2 MB; the
  resolution is a field on the bake tool. Per region: one PNG next to the painted height map, the `_HeightTex`
  convention.
- ~~**Which objects get `HHReflect`?**~~ **ANSWERED at P5: whichever ones the owner tags, bounded three ways.**
  (a) **Layer** — the renderer must carry `ReflectionRegistry.RenderingLayer`, which only the
  `ReflectiveObject` component sets. ⚠️ The shader TAG alone is not enough: the tree shader carries the
  `HHReflect` pass, so tag-only filtering would sweep every tree in the scene into the list (the trap ADR
  0023 hit with the flat Sea sprite). (b) **Distance** — the reflector's ground ELEVATION must be within its
  own `maxHeightAboveWater` of the water level, so a clifftop tree does not reflect in the harbour below it.
  (c) **Frustum** — Unity's culling drops off-screen reflectors for free. **Worst case 64 renderers**
  (`MaxReflectors`), one filtered list, one `ARGBHalf` target; over the cap it logs once and still draws,
  because a silent truncation reads as "everything is covered" when it is not.
- ~~**Reflection fidelity at PPU=32**~~ **PROBED at P5, and the CHEAP answer wins: a flat mirrored silhouette is
  enough.** `ObjectReflectionProbeTests` renders it end-to-end on the real water material through the real
  feature pass at PPU 32: 256 source pixels above the waterline, **255 reflected below**, tapering away from
  the waterline (a mirror, not a translated copy) and not bleeding into open water. **No interior mask and no
  self-occlusion were built, and none is needed at this pixel scale.** ⚠️ CI cannot adjudicate it (no graphics
  device — a render there crashes the editor), so the fixture gates on a device and skips loudly.
- ~~**σ authoring**~~ **ANSWERED at P4: by mood, not by region, and not by Def.** σ is factored as **one**
  mood-eased turbidity scalar (`_Turbidity`, 1/m — in `WaterSurface.MoodFloatNames`) × a fixed per-channel ratio
  (`_AbsorptionRatio`, authored art). So ADR 0017 eases it per weather from the eight preset materials and
  `Water_StirredBrown` **is** a derived state: high σ over the same painted ramp. A turbidity Def stays available
  later (a river mouth wanting its own σ) but nothing needs it yet — one number per mood covers the ask, and it
  is the cheapest thing that could work. Shipped spread: Tropical 0.12 → StirredBrown 3.0; `Water.mat` stays 0.
- **Does `_DepthBands` get re-enabled?** It is `0` today (finding 1). Every item here carries its own quantization
  so the answer does not block this plan — but it is the owner's call and worth making deliberately. ⚠️ **P4
  makes the contrast visible for the first time:** with `_AbsorptionBands` ON by default the *bottom* is
  posterized while the *water body* over it is still a smooth ramp, so wherever the seabed reads strongly you get
  banded bottom against smooth sea. Whether that reads as deliberate or as a mismatch is worth looking at once
  the owner bakes a seabed. It did **not** block P4.
- ~~**`_ShallowTranslucency: 0`**~~ **RETIRED at P4, confirmed not revived.** It was 0 in every material from the
  day it shipped, and it could only ever fade a **scalar** alpha over an **ungraded** sprite the shader never
  saw — the two defects §17.7 exists to fix. `_ShallowTranslucency` / `_ShallowSeeThroughDepth` /
  `_ShallowMinAlpha` are gone from the shader, from `Water.mat`, from `Water_FoggySmother.mat` and from
  `MoodFloatNames` (nothing else read them; `_Turbidity` takes its place in the eased set).
- **Boat feel under P2 (was P6).** The spectrum changes what hulls ride. The owner owes a feel verdict, and it
  should be taken against the same hull set as the ADR 0023 verdict so the comparison is honest. Now arrives
  earlier in the queue — see "the cost of this re-order".
- **Does P0 shrink #5?** If restoring the dialled-down layers delivers most of the variability ask, #5 can be
  scoped down or deferred again. Decide this **on the evidence from P0**, not in advance.
- ~~**`_FbmScale` — defect or taste?**~~ **RULED: deliberate** (finding 4b — it was 3.27 before the owner’s tuning
  commit `f2574a4` and was nudged to 3.26 in it). Not mood-eased, so 3.26 runs everywhere. **Leave it alone.**
- **Does the sea just need worse weather?** `_OceanSwellStrength` already reaches its 0.16 design default at storm
  (finding 4c). Check the storm mood **before** spending anything on P0 or #5 — it is one weather override.
- ~~**Can #10 survive the zoom tiers?**~~ **ANSWERED at P3: yes, and the question was mis-posed.** A ~3 px ripple
  is *not* sub-pixel at wider zoom — `assetsPPU` is locked, so the footprint is **3.2 px at every framing** and
  the premise ("sub-pixel at wider zoom") was never true on this camera. The real risk is the **cycle count**
  (6× more sea on screen at the widest framing), answered by the framing-keyed fade rather than a per-tier one.
  Full reasoning in the #10 amendment; pinned by `RipplePixelFootprintTests`.
- ~~**Should #1 ship visual-only first?**~~ **ANSWERED at P6: no, and the question was the wrong shape.**
  The ADR framed visual-first as a cheaper intermediate. It is not cheaper — it is the `_OceanSwellScale`
  incident with a new cause: the drawn sea and the sampler behind the hull clamp running different
  amplitudes. Fetch landed Tier B in one commit. Full reasoning in the ADR 0018 amendment (2026-07-31).
- **What reach does fetch want per region?** `StepMeters` × the fixed 24 steps sets it globally (96 m at
  the default), which suits St Peters' 160 × 120 m rect. A big open region and a tight harbour may want
  different reaches — **measure once the owner has dialled the strength up**, do not add a per-region knob
  in advance (the `_FbmScale` lesson: find out whether a number is deliberate before "fixing" it).
- **Does #9 subsume part of §5.12's shoreward bias?** Depth-limited dispersion slows and bunches waves near shore,
  which is what the hand-built bias approximates. If so, `_ShorewardBias` may want reducing rather than removing —
  measure, don't assume.
