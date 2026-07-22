# Spike verdict: 3D-displaced water, rendered from the iso perspective — does it make wave formations READABLE?

**Branch:** `spike/3d-water` (unmerged, PR-less — the `spike/3d-boats` precedent). Throwaway code; evidence and this verdict are the deliverable.

**The question (owner, P1):** *"The advantage would be to visually SEE the wave formations and tell larger waves — currently it's hard to tell."* Legibility is the test; looks are secondary.

**Method (one sea, no forks):** the displaced surface is lifted by the EXACT production wave field — the same `_WaveTrain0..3 / _WavePhases / _WaveFieldParams` globals `WaveFieldBridge` publishes, evaluated by a line-for-line twin of the production `WaveFieldSample` (ADR 0018). The scenario is deterministic (wind 10.78 m/s, seaState 0.75, `WaveFieldSettings.Default`, PhaseSeed 0 → 4 trains, total amplitude 1.047 m). The "big wave" was **found, not authored**: a scan over `t = 0..1800 s` located the in-view moment the four trains pile up to 100 % of the amplitude envelope (t = 1513.5 s at world (−6.5, 2.1), h = 1.045 m). The typical tallest in-view crest over the window is 0.834 m — the big one is **×1.25 the everyday tallest**, a genuinely rare, genuinely bigger wave. Same sim state, same 30 × 16.875 m viewport (960×540 @ 32 px/m), both sides.

Evidence: `Evidence~/` here, and a full copy (all 40-frame sequences included) in the session scratchpad `…/scratchpad/3dwater/`. `spike-log.txt` carries every number quoted below.

---

## The readability A/B (the core experiment)

| image | what it shows |
|---|---|
| `still_A_production(_annotated).png` | Production water (in-memory copy of the owner's `Water.mat`, uniform-deep sea), at the big-wave moment. The crosshair marks where the sim says the biggest wave of a half-hour window is. **There is nothing to see there.** Whitecap streaks mark MANY crests with equal salience; the swell-read bands are swamped at this sea state. |
| `still_B_displaced_x1p0(_annotated).png` | The displaced render, same instant, height ×1. The big wave is **pointable**: the one crest wearing a solid foam core on the widest bright band, with real silhouette lift (a 1 m crest raises its screen edge 32 px). |
| `still_AB_x1p0.png`, `still_AB_x2p0.png` | Side-by-side pairs. |
| `filmstrip_A_production.png` vs `filmstrip_B_x1p0/x2p0.png` | The approach (40 frames at 0.35 s in `scratchpad/3dwater/frames/`). In A every frame is statistically identical foam-speckle; in B the big crest visibly builds, arrives, breaks and passes. |

**Verdict: YES — the big wave becomes tellable, already at ×1, clearly at ×1.5–2.** ×3 starts to shear the iso framing apart (crests visually detach from their troughs) — past the style bar.

**The honest decomposition (read before roadmapping).** B's still-frame legibility comes from three channels, and only some of them need 3D:

1. **Envelope-relative shading + foam** — B shades by `height / totalAmplitude` in quantised bands and keys foam on the sharpened crest factor with a high threshold, so only near-envelope crests wear solid caps. This is the single biggest still-frame win, and **the flat production shader could ship most of it without any 3D**: it already computes the same height/crest per pixel; its caps are simply tuned to mark every local crest with equal salience, and its value bands are tuned subtle. A "cheap flat alternative" PR (whitecap density and band contrast keyed to envelope-relative height) would close a real fraction of the owner's complaint at near-zero cost — the spike recommends trying it regardless of the 3D decision.
2. **Geometry: silhouette lift + occlusion** — uniquely 3D. At ×1 a 1 m crest lifts 32 px and its edge occludes the water behind it; in motion this is what makes the sea read as FORMATIONS (a wave you look AT, not a brightness pattern painted on a floor). The approach filmstrips show it; stills undersell it.
3. **Waterline on the hull** — uniquely 3D, see the probe below. This is the "see it lift the boat" cue that closes the see==feel loop.

## The waterline-on-hull probe (secondary, high-value)

`probe_waterline_on_hull.png` (+ `probe_fixed_f00/f06.png`): the committed lobster-boat mesh (`Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset`, ADR 0022 phase 4) drawn through the facet pass, and the displaced sea depth-tested against it in ONE z-buffer, in the rig's true iso frame (`IsoFacetMath.RigToWorld`, elevation 40°). With the hull held fixed, the waterline visibly climbs and falls **about a metre along the planking** across one dominant period — f00 vs f06 is night and day. `probe_riding_*.png` shows the composition with heave (hull lifted along rig-z by the field's height under it): boat and sea move together and the waterline settles. **The owner's "water changes height on the hull" is real and cheap** — no new tech was needed beyond a shared depth buffer; the hull renders with the production facet material built from the committed def (ramps, LN, Bayer, verbatim).

Probe caveats: the keyline pass is skipped, so the mostly-white topsides read flat-white (production draws a 1 px keyline that separates the panels); and the fixed-hull variant shows air under the keel in deep troughs — production would pair displacement with the existing `BoatWaveMotion` heave, which is the riding variant.

## Style assessment

- Run 1 measured a real trap: **full-range Bayer dithering of the value bands visually reconstructs the smooth gradient** — the result read as airbrushed 3D, not this game. Restricting dither to a window around each band boundary (the rigs' language: solid bands, dithered edges) restored the pixel-art read. Any production shader must posterize hard and dither only edges.
- With banded values from the owner's own palette anchors (`_PaletteDeep/Mid/Shallow/Foam` read off `Water.mat`), world-locked pixel cells (PPU-snapped coords, the facet dither discipline — zero crawl by construction) and crest-keyed two-tone foam, the displaced surface **still reads as this game** — flatter and cleaner than the owner's layered production look (no painted ripple/foam textures, no FBM patchwork, no reflections), i.e. a legibility skeleton the production dressing would go back on top of.
- The displaced surface deliberately keeps the game-faithful framing for A/B (ground plane unforeshortened, height lifting screen-y by metres — exactly the production heave convention `IsoFacetMath.HeaveOffset`), so it drops into the current camera without re-authoring the world.

## Perf ballpark (honest, crude)

RTX 4060, D3D12, 960×540, CPU stopwatch around command-buffer submit+flush (NOT GPU timestamps; batch-editor numbers are noisy — treat as order-of-magnitude):

| grid cell | verts | tris | submit ms (best/avg over runs) |
|---|---|---|---|
| 16 px (0.5 m) | 2.8 k | ~5 k | 0.7–1.9 / 1–4 |
| 8 px (0.25 m) | 11 k | ~21 k | 0.7–2.8 / 0.8–4.2 |
| **4 px (0.125 m)** | **43 k** | **~86 k** | **0.6–2.9 / 0.8–3.9** |
| 2 px (0.0625 m) | 173 k | ~345 k | 1.1–2.9 / 1.3–5.6 |

The vertex stage is 4 sin/cos per vert; the fragment evaluates the same 4-train twin the production water already pays per pixel today. A 4 px grid is visually sufficient (2 px sharpened silhouettes marginally) and is far inside the desktop 60 fps budget; mobile-portability discipline says start at 8 px and let the crest silhouette tolerance decide.

## What production integration would actually require

1. **The shore seam is the hard problem.** The spike renders open sea. In production the water quad meets the painted tilemap coast and the walkability waterline (ADR 0009) — a displaced crest at the beach would slide the visible waterline pixels off the sim's wet/dry contour (P1 integrity). Integration must feather displacement to zero by depth (e.g. over the existing `_HeightTex` read) so the shore keeps today's exact contour, and accept that "3D waves" are an open-water feature.
2. **Sorting with sprites.** A displaced mesh cannot ZWrite into the shared scene buffer (it would punch holes in sprites — the ADR 0022 lesson, verbatim). The proven pattern already exists: the facet feature's off-screen pass + overlay quad + SortingGroup. Water joins that private pass; boats already live there — which is ALSO what makes waterline-on-hull free (one shared private z-buffer).
3. **One scale constant.** Boat heave (`HeaveOffset`, ×1 metres) and surface lift must share one exaggeration constant; if the owner tunes ×1.5, buoys/oars/wake anchors scale with it (the overlay-pose lesson: never rescale one consumer alone).
4. **Screen-anchored water layers need review.** Layers that live in the water fragment (foam, caustics, swell-read) ride the displaced surface for free; screen-geometry effects (moon-glitter column, reflection smear) would bend over crests — probably charming, needs eyes.
5. **Whitecap salience retune** (flat-shader-shippable, recommended first): key cap density/solidity to envelope-relative crest height so big waves get the foam.
6. **Raw-command-buffer traps for whoever builds it** (measured here, cost this spike two re-runs): outside URP's frame there is NO reversed-Z translation — production shaders' hardcoded/default `ZTest LEqual` silently kill every fragment against a raw cleared depth buffer (both the facet pass and the water quad did this). Inside URP this is a non-issue; editor tooling that renders off-screen must calibrate (this spike's `CalibrateDepth`).

## Contradictions / limits of this evidence

- The A-side drives `_Time` and the wave globals to the scenario's game time, but the owner's full runtime dressing (day/night tint, moods, reflections driven by a live sky) is absent on both sides equally; A is darker/harsher than in-game sailing feel. The comparison is fair (same absences both sides) but neither image is exactly "the shipped game".
- Big-wave salience in B partly comes from tuning choices (band count 7, cap threshold 0.62) made once and not cherry-picked per frame — but no human-subject test was run; "pointable" is this agent's read of the images plus the deterministic annotation.
- The rms figure (0.834 m) is the RMS of per-frame in-view maxima, not the RMS wave height.

## Bottom line

- Displaced water makes big waves tellable — **yes**, and the motion/occlusion cues plus waterline-on-hull are things the flat shader can NEVER give.
- Recommended shape if pursued: **×1.5 exaggeration, 4 px grid, open-water only (depth-feathered), through the existing facet off-screen pass**, with the envelope-relative whitecap retune shipped in the flat shader first as the cheap partial win.
- The probe upgrade path is real today: hull meshes and the displaced sea already share a language and a z-buffer; "the sea climbs her planking as the crest passes" is a screenshot in this folder, not a hope.
