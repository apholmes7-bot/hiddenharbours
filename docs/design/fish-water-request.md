# Fish under water: request to the water lanes

Status: **request for review, no look accepted and no implementation authorized by this note**.
P1 and P3. The numbers below are proposed test inputs and limits, not new shipping tunings.
No shader, material, palette, scene, sprite, terrain, light or plate expectation changes here.

## Audited starting point

Source audit at `3e7188a426c00dde0b18f94ab9512ea1a2bc0c21`; paths below are repository-relative.
The broader living-fish design is in draft #1044; the inactive school model is in draft #1041.
Neither is assumed merged. This request adds numbers to that design without editing its files.

| Existing source | Consequence |
|---|---|
| `Assets/_Project/Code/Core/Art/SortingBands.cs`: painted bed -21 through -18, submerged plants -8, sea -5, wharf -4 through 1 | These orders are existing facts, not suggested new orders. |
| `Assets/_Project/Code/Fishing/FishSchoolPresenter.cs`: swimmer default -3 | Swimmers are above the sea in compositing order. A baked tint is an approximation, not evidence of transmission through the surface. Some wharf orders also require occlusion review. |
| `Assets/_Project/Art/Shaders/HiddenHarboursWaterOverlay.shader`: alpha blend of `_HHWaterScreenTex` | Simply moving a SpriteRenderer below the sea does not prove it participates in the displaced offscreen composite. |
| `Assets/_Project/Art/Shaders/HiddenHarboursWater.shader`: private `ShallowsBend` | Existing bed bend rounds `slope * depth * _ShallowsFloorRefraction * ppm` to whole ground texels, with `ppm = max(_PixelsPerMetre, 1)`. A consumer must share this truth rather than invent another wave oscillator. |

## One read, explicit units

Request a shared, opt-in water read; the following API names are **proposed and do not exist**:
`FishWaterRead.hlsl`, function `HHFishWaterRead(worldXY, depthMetres)`, returning refracted world
XY, transmission RGB, visibility in [0,1] and composed surface elevation in metres. A future
consumer property `_HHFishDepthMetres` and keyword `_HH_FISH_UNDERWATER` default to zero/off.
The water lane chooses the implementation and a single compositing route before activation.

Fish supplies stable entity id, actual ground-plane XY, body elevation, depth below the local
composed sea/standing-water surface, substrate contact elevation, and a bounded body footprint.
One world unit is one metre; canonical sprite PPU is 32. Ground refraction uses the water's
**actual** `_PixelsPerMetre`; it must not assume ground texels always equal sprite pixels.
Use the same sampled simulation instant and existing published wave phase as the water view.
Display interpolation may be smooth, but must not move physical occupancy or sonar positions.

Distinguish body submergence `d = max(0, surfaceElevation - bodyElevation)` from total water
column `D = max(0, surfaceElevation - bedElevation)` and height above substrate
`h = max(0, bodyElevation - bedElevation)`. Reject unknown support/depth; do not call it zero.
The composed standing-water read must agree with the existing standing-water contract.
Shader displacement is presentation; it must never rewrite tide, wet/dry gameplay, grounding
or the height map. Held, deck-supported and contained animals leave the underwater domain.

## Numeric request

All values in this table are **candidate settings for the owning art lane to evaluate**, kept
in its future Def/material controls. No palette RGB or species balance is prescribed.

| Requirement | Candidate numbers and test |
|---|---|
| Depth tint and fade | Reuse the existing water hue/grade once. For a neutral diagnostic only, request transmission `T(d)=2^(-d/2m)`, so the half-transmission depth is 2 m. Samples at depths 0, 0.5, 1, 2, 4, 8 m give T = 1, 0.840896, 0.707107, 0.5, 0.25, 0.0625. Numeric tolerance 0.00001 in the diagnostic. This is a calibration fixture, not an approved sea colour or biological visibility cutoff. |
| Visibility | Output finite [0,1]. At depth zero the diagnostic returns 1; increasing depth in a fixed column must not increase visibility. A diagnostic visibility of zero hides only the rendering, leaving physical presence and sonar/capture eligibility unchanged. Production clarity coefficients come from the water lane's existing grade, with a separate explicit visibility control if needed. |
| Refraction | Use the existing surface slope and depth-scaled bend, rounded on the shared ground grid. Candidate maximum offset magnitude: 2 ground texels, or `2/ppm` metres. Clamp vector length before final grid rounding and report the resulting rounded bound (up to `sqrt(2)/2` additional texel). At depth 0 or refraction strength 0, offset is exactly 0. At ppm 32, the pre-round cap is 0.0625 m. Bend once in one coordinate frame; never shift fish gameplay XY. |
| Substrate shadow | Receiver is the actual supported bed point, bent in the same coordinate frame as the bed. Candidate neutral opacity `A = 0.20 * exp(-h/0.50m) * T(D)`. At D=0, h = 0, 0.5, 1, 2 m yields A = 0.20, 0.073576, 0.027067, 0.003663. Shadow strength 0 gives no shadow. Width is capped at the supplied physical footprint; no shadow on dry or unknown substrate. These are requested samples, not new shadow art. |
| Domain transition | For a diagnostic body at the surface, depth is exactly 0 on both sides of entry/exit. Test at -0.001, 0, +0.001 m relative elevation; no second fish, double transmission, duplicate shadow or one-tick pop between domains. Animation and the appearance of a breach remain art work. |

No new near-surface glint, feeding boil, ripple asset or light is requested in this slice.
For clams and walking crustaceans, substrate support comes from a future certified bed query;
the school patch's conservative depth cannot place an individual body or its shadow.

## Compositing, acceptance and budget

The water lane must document one route: a fish shaded above the sea with equivalent
transmission, or a deliberate insertion into the water composite. Its choice must specify
how hulls, wakes, wharf decks, pilings, submerged plants and the bed occlude it. Do not prescribe
a new sorting number here or infer physical depth from screen Y. A bottom shadow rendered as
an ordinary sprite above the sea is not equivalent to a shadow inside the sampled bed.

Requested fixture matrix: 6 depths (0, 0.5, 1, 2, 4, 8 m), 3 shadow separations (0, 0.5, 2 m),
2 water routes (sea and standing water), 2 sea states (calm and rough), and 2 light samples
(day and night). Fix world seed, clock, camera, grade and wave phase. Include real occluders
and a positive control proving the enabled diagnostic changes a nonempty fish region.
This is 48 primary depth/water/state/light combinations and 144 shadow probes when all 3
separations are tested per case. Not every combination is physically valid; record excluded dry cases.

Before/after with the feature disabled, a clear optional shadow texture and zero strength
must be **byte-equal over the entire plate**, not merely below a percentage threshold.
Enabled numeric probes test the table; enabled visual plates need the art lane's judgement.
Never rebaseline a plate to make this request pass. A headless/no-graphics CI skip is not a
visual proof. This document supplies no plate and accepts no appearance.

Proposed activation budget at 1920x1080 on the existing desktop/laptop target: incremental
GPU median at most 0.20 ms, incremental main-thread median at most 0.10 ms, zero steady-state
managed allocations, at most 1 additional batched draw, and **0 new full-resolution render
targets**. Reuse the current water composite. At 60 fps these are requested portions of the
16.67 ms frame, not measured results. Compare 5 alternating on/off pairs after warmup, 120
frames per sample; report medians and the largest sample rather than hiding load variance.
Price any revised route explicitly before activation. Existing terrain/water PR ownership
remains intact; this request does not instruct another lane to edit its in-flight files.
