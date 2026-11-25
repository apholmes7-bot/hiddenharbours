# Living fish: the water boundary

Read-only shader/sorting audit at `3e7188a426c00dde0b18f94ab9512ea1a2bc0c21`. No shaders, includes, materials, sprites, lighting or plates are changed. Everything below is a request/design until the owning water/art roles review it.

## What happens today

`Core/Art/SortingBands.cs:38` fixes the sea at -5, submerged shore plants at -8 and the painted bed at -21…-18. Swimmers default to -3 (`Fishing/FishSchoolPresenter.cs:47`). Thus today's swimmer is **composited above the sea surface**, although below a normal hull/wake. Its baked tint and reduced alpha make it read as submerged. Do not describe that as physically behind the water. The wharf band spans -4…1, so swimmer -3 also requires explicit wharf-occlusion review; a promise about hull order alone is insufficient.

The water uses transparent alpha blending (`Art/Shaders/HiddenHarboursWater.shader:1122`). The displaced offscreen surface is put back into the scene by `HiddenHarboursWaterOverlay.shader:40` through `_HHWaterScreenTex`. Its SortingGroup inherits the flat surface's sorting slot (`Art/DisplacedWaterSurface.cs:647`). Moving a fish under that group is not enough to guarantee the desired transmission: the surface may cover it, and the offscreen render does not automatically include SpriteRenderers.

The shader has `_HeightTex`, `_WaterLevel`, `_DepthRamp` and `_SeabedTex` (`:1151`, `:1160`, `:1165`, `:1662`). `ShallowsBend` (`:2366`) bends the *sampled floor* by rounded surface slope × depth × `_ShallowsFloorRefraction`, on the existing pixel grid. That private function is not a public fish shader include. Its water-depth key remains read-only for simulation and wet/dry clipping. `Include/StillWater.hlsl:30` publishes `StillLevelAt`, with a no-map passthrough, so standing water and sea should use the same composed level in any later fish view.

## Intended picture and inputs

| Effect | Fish supplies | Water supplies |
|---|---|---|
| Depth tint/fade | World XY, metres below the composed surface, species sprite and current vitality | Existing palette/grade, column transmission and visibility, with deep fish still present to sonar |
| Refraction wobble | Actual fish depth; stable sample time | Shared displaced-surface slope, existing pixel-grid bend and current refraction strength |
| Bed shadow | Position, actual body height over sampled bed, bounded footprint | Receiving bed position and water transmission; shadow remains on supported bed and fades with separation |
| Near-surface glint | A brief surface/turn cue, explicitly not every fish every frame | Same light/sun and wave slope as the surface; clipped/attenuated below the surface |
| Feeding ripples/boils | Stable event id, centre, radius, game-clock start, finite duration and strength | A bounded pooled surface-effect route, no hidden gameplay force or waterline change |
| Sorting/occlusion | Depth domain: submerged, breaching, held, deck or container; real anchors | Stable surface compositing slot and a documented path through water, pilings and hull occluders |

A breach may cross to the above-surface domain for its arc and return at a zero-height endpoint. A body on deck follows the hull support plane. Neither depth nor sorting order should be inferred from screen Y alone. Use one model read for water visibility, sonar marks and bite influence when the new field is eventually wired; do not introduce a visually plausible second school population.

## Possible without a water shader change

- Keep today's pooled sprites and `SwimmerVisibility` table: Full in Tidepool/Shallows, Dim in Inshore, Shadow in Midwater, None in Deep/Abyssal (`Fishing/SwimmerVisibility.cs:60`). The presenter adds 0.65 alpha in Inshore (`FishSchoolPresenter.cs:369`). This is current approximation, not a new approved tint.
- Use existing baked swim/event cells and the local `ShoalMath` pose; a logic change can provide new coordinates without editing art. Activation still needs an art review and plate; this charter provides neither.
- Supply depth and water level through the existing Core environment/terrain/standing-water contracts. `SharedWaveField.TryGet` supplies the published physical wave trains (`Core/Environment/SharedWaveField.cs:22`); a future CPU effect can consume the same wave truth instead of adding random wobble.
- Reuse existing event plumbing for a finite surface cue where a suitable art asset already exists. Feeding intent does not itself guarantee an existing boil asset or builder connection. A sprite ripple above the sea needs no water edit, but is not physical displacement.
- Keep the baked fish shadow/near-surface highlights as an approximation. A separately projected **new** bed shadow or glint effect is a renderer/art task and cannot be claimed to exist just because an atlas contains a shadow cell.

## REQUEST to the water lanes

These are exact proposed API names, **not claims that they exist**. The one-line include/keyword is a consumer hook; implementing its backing path may be substantial. Keep all changes opt-in and zero-strength/default-off, preserve the current depth key, and do not rebaseline a plate to accept a regression.

1. Export `Assets/_Project/Art/Shaders/Include/FishWaterRead.hlsl` with `HHFishWaterRead(float2 worldXY, float depthMetres)` returning a struct containing `float2 refractedWorldXY`, `float3 transmissionRGB`, `float visibility01`, and `float surfaceLevelMetres`. Share/refactor the existing `ShallowsBend` implementation and grade/absorption read rather than copying its constants. A future fish shader's consumer line would be `#include "Assets/_Project/Art/Shaders/Include/FishWaterRead.hlsl"`.
2. Reserve keyword `_HH_FISH_UNDERWATER` and per-instance property `_HHFishDepthMetres` for that future sprite path, default keyword off. Confirm whether fish should be shaded above Sea with equivalent transmission, or inserted into the offscreen water composite. Choose one documented ordering/occlusion route; do not blindly move -3 below -5.
3. If physical fish shadows must be inside the sampled bed, publish an opt-in bed-prop/shadow input, proposed `_HHFishBedShadowTex`, in the same bent floor coordinate frame. A normal SpriteRenderer shadow outside the water composite is not equivalent. Default clear texture must be pixel-identical.
4. For feeding disturbances, offer a bounded Core event seam (proposed `FishSurfaceCue` value: id, XY, radius, startGameSeconds, durationGameSeconds, strength01) into the existing surface-effect pool. Ask for a shader keyword only if the water lane chooses to shade those cues there: proposed `_HH_FISH_SURFACE_CUES`, default off. This is visual energy only; it must not modify tide, grounding depth or the gameplay wave field.

Water/art owners must price the chosen route in draw calls, CPU time, GPU ms and target-device memory. The field adds no render work while disabled. No visual approval is implied by these requests, and no changes to water-owned files are required to review the logic PR.
