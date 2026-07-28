# ADR 0027 — The realness pass: eight physically-grounded water upgrades, every one pixelized

- **Status:** **Proposed** — awaiting `lead-architect` sign-off (renderer-feature plumbing is a cross-cutting
  architectural call, CLAUDE.md rule 4 / `coordination.md` §1.1) and owner ratification of the phase order.
  This change is **docs only**: it ships no shader, no C#, no scene, no material change.
- **Date:** 2026-07-28
- **Decision owner:** `lead-architect` (render plumbing, the wave-field seam, the ADR-0018/0023 impact);
  `art-pipeline` owns the look (σ, colour, thresholds, all tunables).
- **Flagged from:** the owner (2026-07-28), reviewing which techniques from Unity's HDRP Water System are worth
  taking after we ruled out adopting the system itself (it is HDRP-only; we are URP 17.5 on the 2D Renderer):
  *"i maybe want that effect, as long as the end look gets pixelated, i can appreciate realness attributes …
  i think i want reflections too"*, then: *"make sure the plan includes all 8 elements."* This ADR records all
  eight, tiered by what they touch.
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
nine addenda); this ADR decides all eight.

### Three findings from the live material that shape the decision

Read from `Assets/_Project/Art/Materials/Water.mat`, not assumed:

1. **`_USE_DEPTHRAMP` is ON with a painted texture assigned, and `_DepthBands: 0`.** The base colour is a smooth
   lookup into a **hand-painted 1D LUT** over a linear depth axis (0.15 → 4.0 m,
   `HiddenHarboursWater.shader:2386`). A LUT is **strictly more general** than any closed-form absorption curve —
   anything `e^(−σd)` computes, the owner can already paint, per-channel, including non-physical shapes he prefers.
   `_DeepBlueStrength: 0.45` is standing evidence that the physical answer is *not* the wanted answer: that bounded
   pull toward navy exists precisely because the settled deep end wasn't what he wanted.
2. **`_ShallowTranslucency: 0`** — the §17 see-through shallows shipped in Arc C are **off** in the live material.
3. **The seabed arrives ungraded and untintable.** §17.1 shows the bottom by *lowering `col.a`* so a seabed sprite
   drawn behind the sea plane bleeds through the `SrcAlpha OneMinusSrcAlpha` blend. The water shader never sees
   that colour, so it cannot absorb it; and a **scalar** alpha cannot express **per-channel** transmission at all.
   This is also why §17.3 documents see-through and caustics as partly cancelling, to be tuned around.

Finding 1 kills the obvious version of absorption. Finding 3 is where the physics does work no existing knob does.

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

**(1) #7 — Depth absorption applies to the TRANSMITTED SEABED, never to the water's own colour.**
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

### Tier B — the shared wave field (C# twin + headless tests mandatory)

**(5) #1 — Wind fetch, read from the height map.**
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

**(7) #8 — Reflections: a filtered renderer list into an RT, wave-warped by the water shader.**

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
- **Pixelation:** the RT inherits the camera's render resolution, point-filtered, and the warped lookup snaps to the
  PPU grid — pixel art by construction rather than by post-hoc filtering.
- **Composition:** over the §11 sky mirror (a boat reads on top of reflected cloud), under the foam (whitecaps read
  on top of the boat). Pre-grade, so it dims with night like the rest of the sea — **except** night-lit sources,
  which ride the §11.6 post-grade compensation exactly as the moon glitter does, or a lit wheelhouse at night will
  be crushed to ~3% by the multiply overlay.

**(8) #6 — An advected foam buffer for wake.**
Unity's foam generators write into a buffer that **advects and decays**. That *is* the "trail left behind you"
architecture, versus attaching a trail to the boat. A persistent camera-relative RT, ping-ponged per frame:
advect along the existing `FoamDriftDir` (the wind/current blend), decay, inject where hulls pass; sampled by the
water shader as a mask that **adds** to the existing foam rather than replacing it.

> ⚠️ **This intersects the dynamic-wake work already in flight.** Deciding the buffer architecture *after* those PRs
> land is materially more expensive than deciding it now. This item is sequenced early for that reason alone, not
> because its visual payoff outranks the others.

---

## Pixelation — the owner's condition, made concrete per item

| Item | How the output is pixelized |
|---|---|
| #7 absorption | Seabed sampled on pixelized world coords; `_AbsorptionBands` quantizes transmission (**default ON**) |
| #2 caustics | Curvature sampled on the existing pixelized grid; keeps the current caustic quantization |
| #3 convergence foam | Feeds the existing foam threshold, which is already banded/dithered |
| #4 band scaling | Scales frequency only — the pixelize step is downstream and unchanged |
| #1 fetch | Fixed-step march on pixelized coords; `_FetchBands` quantizes the result |
| #5 spectrum | Field is quantized where it is read, exactly as today |
| #8 reflections | RT at camera render resolution, point filter, warped lookup snapped to the PPU grid |
| #6 wake buffer | RT cells aligned to the PPU world grid |

---

## Phasing

| Phase | Items | Tier | Why here |
|---|---|---|---|
| **P1** | #2 caustics, #3 convergence foam, #4 band scaling (visual) | A | Cheapest, no sim risk, no plumbing. Immediate legibility win. |
| **P2** | #7 absorption + `_SeabedTex` bake | A | Self-contained; retires §17.1/§17.3 rather than tuning around them. |
| **P3** | #8 reflections (`HHReflect` list, pivot mirror, wave warp, composition) | C | The owner's second explicit ask; depends on nothing above. |
| **P4** | #6 advected foam buffer | C | **Sequenced against the in-flight wake PRs** — decide before they land. |
| **P5** | #1 fetch (visual), then into the field | A→B | Visual first; promotion earns a twin. |
| **P6** | #5 spectrum + grouping; #4 promoted into `_WaveFieldParams` | B | Largest payoff, largest risk. Changes boat feel — owner verdict required. |

P1–P4 are independent and can proceed in parallel across lanes. P5–P6 are serial and gated on ADR 0018 amendment.

---

## Determinism, save & performance (the invariants held)

- **Every item is visual-only except P5-promotion and P6.** #2/#3/#4/#7/#8/#6 touch `col.rgb` / `col.a` (and, for
  #6/#8, their own render targets) and **never** `depth`, `clip()`, `_WaterLevel`, the height read, or the sim.
  Nothing enters the save (rule 5 / ADR 0008).
- **#1-promoted and #5 do change the field hulls ride.** They are deterministic functions of
  `(worldSeed, gameTime)` + authored height, recomputed and never saved — but they require C# twins, headless
  determinism tests, and an **ADR 0018 amendment**. This is stated as a gate, not a footnote.
- **Rule 6 throughout:** every new constant is a material property. Every item defaults to **passthrough**
  (σ = 0, strength 0, bands off where they would change tuned values), so the shipped `Water.mat` look stays
  byte-identical until the owner dials each in — the discipline every ADR-0010 addendum has kept.
- **Rule 7:** #8 and #6 each add one filtered list / one RT, both honouring the existing zero-cost-when-idle
  contract. The `HHReflect` list needs a **distance-or-layer rule** so it stays small (see open questions).

## Test & CI guards

- **New pure twins, headless:** `WaterAbsorption.Transmission(σ, depth)` / `BandTransmission` (monotone decreasing;
  per-channel ordering — red extinguishes before blue; σ = 0 exact passthrough; the 2× path applied; banding
  quantizes). `WaterFoam.Convergence`. `WaterFetch.Fetch01`. Spectrum weighting + grouping twins for P6.
- **`WaterShaderCompileGuardTests` continues to force-compile the shipped variant** — no `+` in any `[Header]` or
  property string, **no `[unroll]` over a runtime bound** (directly relevant to #1's march). The magenta class stays
  guarded.
- ⚠️ **CI has no graphics device.** Rendering tests crash the editor rather than failing cleanly. Any pass-level test
  for #8/#6 must follow the existing `IsoFacetUrpPassTests` pattern; all new math stays headless.

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

- **`_SeabedTex` bake budget** — resolution, texture memory, per-region baking. Mirror `_HeightTex`'s budget and
  measure against rule 7 before P2 starts.
- **Which objects get `HHReflect`?** Boats, wharf structures, trees and characters near the water. Needs a
  distance-or-layer rule so the list stays bounded — an unbounded reflective set is the perf failure mode here.
- **Reflection fidelity at PPU=32** — is a flat mirrored silhouette enough, or must reflections honour the interior
  mask and hull self-occlusion? Cheapest correct answer wins; probe before building.
- **σ authoring** — per region by hand, or derived from a turbidity Def? Either way σ joins
  `WaterSurface.MoodFloatNames` so ADR 0017 eases it per weather, making `Water_StirredBrown` a **derived** state
  rather than a hand-picked colour.
- **Does `_DepthBands` get re-enabled?** It is `0` today (finding 1). Every item here carries its own quantization
  so the answer does not block this plan — but it is the owner's call and worth making deliberately.
- **`_ShallowTranslucency: 0`** — the Arc C see-through has been dark since it shipped. P2 supersedes it; confirm
  that is the intent rather than reviving it first.
- **Boat feel under P6.** The spectrum changes what hulls ride. The owner owes a feel verdict, and it should be
  taken against the same hull set as the ADR 0023 verdict so the comparison is honest.
