# Settled St Peters plate variance: findings and remaining blocker

Status: **partial diagnosis; the south-sand residue and exact historical path
rendering remain unresolved**.
This document records measurements and source traces. It changes no plate,
expectation, capture fixture, renderer, shader, material, importer or scene.

## Original evidence, preserved and remeasured

The saved comparison used base `3e7188a426c00dde0b18f94ab9512ea1a2bc0c21` and
`1d3a1c95de44546c1a697a7f36b2235e580f1ade`. Their tracked difference is a Python
capture wrapper, its tests and documentation; their runtime Assets match.
All 87 manifest hashes from three saved captures, including captions, were
verified. Every saved capture records exactly one execution of
`StPetersSplatWarpPlatePlayTests.TheSplatEdges_PlatedSettledAtNoon`.

The original two-arm comparison is below. Every arm's own plate/control pair
differs by **zero pixels**. Cross-launch differences are raw RGBA comparisons over
all 2,073,600 pixels (1920 x 1080), without registration, masking or tolerance.

| Pose | Changed pixels | Shadows re-ticked, base | Shadows re-ticked, comparison |
|---|---:|---:|---:|
| board-1-the-path-and-the-steps | 0 | 435 | 435 |
| board-2-the-sand-to-the-water | 0 | 388 | 388 |
| board-3-the-bay-east | 0 | 388 | 388 |
| window-south-sand-patch | 260 | 389 | 389 |
| window-south-sand-west | 0 | 322 | 322 |
| window-ginnys-path | 155,808 | 563 | 343 |
| window-shore-path-east | 0 | 563 | 562 |

For the two nonzero frames, the captions agree on camera centre, orthographic
size, core clock (2700 seconds), shader clock (`_Time.y` 43200), frozen time scales
and the six ambient emitter hosts held off. The large frame's enabled shadow
population differs by 220. A zero repeat control therefore does not establish an
identical population across launches.

A third saved baseline launch also has zero repeat noise. Compared with the
second baseline it differs by **1,884 pixels** at south sand with 389 re-ticked
shadows in each, and **933 pixels** at the path with 563 in each. Equal counts
are not a proof of equal caster identities or equal renderer state. These numbers
remain separate measurements, not an averaged noise allowance.

## Concrete stale-visibility path

[`SunCasterInstaller.Update`, `ScanLoadedScenes` and `ApplyVisibility`](../../Assets/_Project/Code/Art/SunCasterInstaller.cs)
refresh distance culling on an **unscaled** timer.
The [shipped rules](../../Assets/_Project/Resources/SunCasterRules.asset) specify
**two seconds** between arrival scans and a **96 m** camera-distance radius.
Freezing `Time.timeScale` does not freeze that timer.

[`FrameOn` in the settled harness](../../Assets/Tests/PlayMode/StPetersSplatWarpPlatePlayTests.cs)
moves the play camera and waits frames for stillness. It does not synchronously
refresh the installer. `ReTickTheSpriteShadows` visits only components whose
`isActiveAndEnabled` is already true. It updates their shadow appearance through
`OnEnable`; it cannot enable the casters the installer culled at the previous
camera position. A fast capture can be locally stable while retaining that old
visibility decision. Added readbacks can change elapsed timing and hide the race.

The path pose follows south-sand-west: the centres move from `(-2, -87.5)` to
`(65, 27.5)`, about 133 m, exceeding the 96 m radius. This is a concrete way for
visible path casters to remain disabled until the next scan. No tuneable or
production culling rule needs changing to test the hypothesis.

## Controlled captures on the investigation base

Investigation base: `d9d4b8558aaa77b49b6bad9dbb35345d2059814c`.
The installer, sprite-shadow component, rules, profile, shade material and shader
were checked against the historical base. Temporary instrumentation was added
to the existing test only, compiled offline, and removed byte-for-byte afterward.
Each diagnostic launch **ran exactly the requested one case and passed**. Neither
launch changed a published assertion or saved an asset.

The first diagnostic synchronously rescanned visibility, captured, restored the
original enable states and captured again. It happened to enter every pose with
already settled visibility: rescanning changed zero pixels in all seven poses.
Restoration changed zero pixels. A separate equal-order shade-quad ordering probe
changed 0, 7, 12, 0, 0, 4,865 and 0 pixels in pose order, then restored to exactly
zero in every pose. In particular, it changed **zero** south-sand pixels, so it
does **not** explain the original 260-pixel residue.

The second diagnostic tested the stale-camera premise directly, inside one
synchronous A/B/A sequence. A scans at the current camera; B scans at the previous
pose and then renders from the unchanged current camera; the final A scans at the
current camera again. The core clock and shader-time pin remain in force.

| Pose | A vs previous-camera culling B | Final A vs first A | Original difference pixels covered |
|---|---:|---:|---:|
| window-south-sand-patch | 0 | 0 | 0 of 260 |
| window-ginnys-path | 160,652 | 0 | 155,652 of 155,808 |

At the path, **140,484** historical pixels also match
the forced-culling probe's signed RGBA delta exactly. The probe's state counters:
`original enabled 563; refreshed enabled 563; flipped 0; refreshed changed 0 px; restored changed 0 px; previous camera (-2, -87.5) enabled 343; previous-cull changed 160652 px; cull restored changed 0 px`.
All seven raw plate/control pairs, all seven culling A/A restorations and all
seven original-state restorations are exactly zero. This establishes the effect
of stale visibility without turning any historical difference into an accepted
baseline. Mask overlap describes correlation with the old frame; missing
historical renderer identities prevent reconstruction of its exact draw stream.

## What remains parked

The original 260 south-sand pixels and the 156 path pixels outside the
forced-culling overlap remain unassigned to a renderer or property.
Spatial coverage also does not explain every original channel amplitude:
140,484 of the 155,808 signed RGBA deltas match the probe exactly.
Their inclusive top-left-origin bounding box is `(123,492)-(1036,777)`, and the
maximum channel difference is 65. The path's historical maximum is 30. Those raw
differences, controls, captions and diagnostic images are retained unchanged
outside version control. Their values must not become an exclusion mask or a
new baseline.

The missing evidence is a per-renderer state record for the original shutters.
A follow-up capture should journal stable caster identities, enabled states,
sprite cells, transforms, draw order and material/property values at each shutter,
then reproduce the south-sand residue with paired controls. Keep clock pinning,
all original assertions and raw comparisons. Any proposed visibility-settle fix
needs its own scoped review; this investigation publishes documentation only.

Both diagnostic editors were closed and their leases returned with all launched
processes gone and the shared save unchanged. The final tracked diff contains
this document only; no Assets, ProjectSettings, gameplay tuning, save schema or
pixel expectation changed.
