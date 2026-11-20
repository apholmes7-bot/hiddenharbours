# Cut-off pools: connectivity design and blocked integration

Status: design only. No runtime behavior changes. The implementation is parked
because open draft PR #1336 owns the main water shader, `WaterSurface.cs`,
`FoamInjectionRegistry.cs`, `FoamInjector.cs`, and both `GameConfig` files.
The audit below describes base `50682d4c1596003f5fbc79f2eb668ffa112d0ca9`.

The intended result serves P1: after the tide closes a basin's connection to the
sea, its existing water stops receiving ocean swell, swash, and surf foam. Rising
water restores those effects continuously after the connection opens.

## Current path

| Source | Observed role and missing connection |
| --- | --- |
| `Assets/_Project/Code/Art/WaterSurface.cs` | `BakeHeightMapIfNeeded` chooses painted heights, `ITidalTerrain`, or a distance-to-land fallback. `FeedPaintedHeightTexture` binds the same painted height bytes used by terrain. The generated path is 96 square by default, clamped to 16 through 256. Both publish through `SeabedGlobals`. There is no ocean-connectivity channel. |
| `WaterSurface.cs`, `SampleSimTargets` and `PublishSeaLevel` | Samples `IEnvironmentService.WaterLevelAt(clock.TotalSeconds)` on the slow tier; the frame tier eases the drawn level. `_WaterLevel` and `_HHSeaLevelWorld.x` describe that same drawn level. Connectivity must distinguish this presentation easing from the deterministic tide input. |
| `Assets/_Project/Code/Art/WaveFieldBridge.cs` | Publishes wave trains and phases plus fetch, swell shelter, and breaker settings. `SharedWaveField` exposes the same trains through Core. The bridge uses the clock's total and flow seconds and the environment seed; it has no connected-water test. |
| Main water shader, `frag` | Computes the moving height-read warp before depth; composes depth with `StillLevelAt`; then applies `BeachSwash`, bore run-up, edge clipping, swell shading, and shore foam. A still-water level changes depth, but does not gate these motion paths. Fetch and shelter are not a flood-fill connectivity test. |
| Main water shader, `vertDisplaced` | Samples the wave field, computes tide-relative depth, and applies `vHeight * _WaveExaggeration * ShoreFade01(...)`. Local depth can remain positive behind a closed bar. No still-water or connectivity gate suppresses this lift. |
| `Assets/_Project/Code/Art/DisplacedWaterSurface.cs` | Copies the flat surface's property block to displaced chunks and publishes scale and shore fade through Core's `DisplacedSea`. A change to vertex lift must have matching CPU consumers. |
| `Assets/_Project/Art/Shaders/Include/StillWater.hlsl`, `Assets/_Project/Code/Core/Environment/IStillWater.cs`, `Assets/_Project/Code/World/PaintedStillWater.cs` | The existing map stores a standing level, not reachability. CPU and shader compose `max(tide, still)`. Nonzero map coverage alone is not proof of a wet or disconnected pool. |
| `Assets/_Project/Code/Art/SeabedGlobals.cs`, `FoamInjectionRegistry.cs`, `FoamInjector.cs`, `CurlingBreakerSampler.cs` | Published seabed/sea level, foam transport, and CPU surface-lift/breaker readers are additional integration points. Gating only the visible fringe would leave other ocean motion active. PR #1336 changes the foam composition and injection path. |
| `Assets/_Project/Code/World/TerrainPlan/TerrainPlanFlood.cs` | `SpillLevels` already computes deterministic minimum spill elevations with a priority flood, four-neighbor traversal, and cell-index tie breaks. It seeds every raster border and lives in World. Art must not call it directly; arbitrary region borders are not necessarily open sea. |

The defect therefore cannot be fixed by changing the existing still-level map or
turning down global sea state. Both leave independent motion paths incorrect.

## Proposed connectivity signal

For each cell, compute the lowest ocean-connection elevation:

```
S(cell) = min over paths to an open-sea seed (max terrain elevation on the path)
u       = clamp((tide - S(cell)) / ReconnectionBlendMeters, 0, 1)
swell   = u * u * (3 - 2 * u)
```

An unreachable cell has `S = +infinity` and factor zero. At or below its spill
height, the basin has exactly zero ocean motion. Above that height, the factor
increases continuously, with zero endpoint derivatives, and reaches exactly one
at one blend width above the sill. Open deep water stays at one. Multiplying a
continuous wave by this factor gives continuous reconnection and disconnection;
there is no frame-history lerp and no residual swell below the sill.

This is a static representation of flood filling at every possible tide level.
For the chosen four-neighbor grid, `S < tide` is equivalent to a flood from the
open-sea seeds through cells with terrain below the tide. It avoids both a
per-frame flood and repeated whole-texture uploads at tide steps.

1. Read the authoritative height source and its world rectangle at region load
   or terrain revision. Use the same precision and coordinate convention as the
   depth sampler. Do not infer topology from the distance-to-land fallback.
2. Supply explicit ocean-connected seed cells or boundary segments in region
   water data. Do not assume that every raster border, inland lake, or shared
   region edge reaches the ocean. Cross-region connections need matching boundary
   data; a cropped basin touching a border is a required rejection case.
3. Extract or adapt the pure priority-flood kernel into Core, retaining the
   existing World entry point and its tests. Support explicit seeds and stable
   cell-index tie breaks. Do not introduce an Art-to-World dependency or a second
   independently maintained spill algorithm.
4. Build an immutable threshold field once. Publish its CPU sampler through a
   proposed Core `ISeaConnectivity` seam. The rendering publisher binds the same
   values and world transform per surface, including displaced chunks. Register,
   replace, and release by region/owner so additive loading cannot mix maps.
5. Use a point-sampled, linear RFloat threshold texture without mipmaps; infinity
   can be represented by an explicit unreachable sentinel with a validity flag.
   Never bilinearly mix a connected cell's threshold into a cut-off basin. CPU
   and shader must agree on cell-center addressing, bounds, and the sentinel.
6. Evaluate the smooth factor from the current deterministic tide on CPU and GPU.
   Publish one identical tide input for these consumers. Do not derive
   connectivity from `Time.time`, elapsed frame delta, the existing eased drawn
   waterline, or the moment a region happened to load. A cheap tide evaluation
   and scalar uniform update do not rebuild or upload the connectivity texture.

Settings belong in `GameConfig` or the water Def: a strictly positive
`ReconnectionBlendMeters`, ocean seed data, and any resolution or quantization
policy. Invalid settings fail validation; there is no hidden epsilon that turns
on waves below a bar. The blend width needs an explicit default and graphics
review before implementation is accepted. A maximum cell size must preserve the
narrowest authored bar and channel; do not downsample to 96 square merely because
that is the existing fallback bake's default. Validate four-neighbor raster
connectivity against the rendered, bilinear height field, including diagonal
pinholes and narrow sills. Refine the grid or conservatively sample connecting
edges where those disagree.

No save field or random source is needed. Thresholds depend only on terrain and
seed data; tide depends on the existing world seed and game time. Identical input
content, seed, and time must give identical results after reload, time scrub, or
a different frame cadence.

Keep standing-water level and ocean reachability separate. Preserve the existing
`IStillWater` level and `max(tide, still)` composition for supported pools. A spill
threshold alone does not prove that a dry basin ever filled: do not manufacture
water in every depression. If a target tidal pool has no retained-level source,
derive that level from deterministic tidal history through Core before claiming
it stays filled after cutoff. This design does not implement that extension or
claim that masking waves alone solves retained volume.

## Integration after the owned changes settle

Apply the same factor to ocean wave height and slope, the moving height-read
warp, cosmetic swash, bore run-up, surf/fringe foam generation, and ocean-driven
wet-edge motion. Cover both the flat fragment and displaced vertex paths, plus
the surface-height and breaker CPU readers. Do not stop the global wave clock:
the rest of the sea must keep moving.

Existing foam history must not keep receiving or displaying ocean surf in a
disconnected cell. Integrate that policy with PR #1336's single foam composition
and injection path, including transport across a newly dry bar. Preserve the
distinction between ocean surf and locally generated interaction effects; a
single final multiplier on all foam would erase valid local contacts as well.
The acceptance scene must contain no local disturbances when asserting a still
pool. Reflections, wave-derived shading, curling breakers, and tidal-face wetness
need a consumer audit so no separate ocean pulse survives the primary gate.

For missing height/seed data, retain an explicit compatibility fallback and
report that region as unsupported. Never declare it fixed or guess that its
border is the ocean. A source revision invalidates the threshold cache; enable
and disable must bind and release both CPU and GPU state without stale globals.

## Cost and verification contract

The proposed priority flood is `O(N log N)` at load/revision and `O(N)` storage.
An indexed heap with input heights, thresholds, cell indices, heap positions,
and one state byte uses approximately `17N` bytes, excluding container overhead.
The RFloat texture adds `4N` GPU bytes. At 96 square that is about 153 KiB CPU and
36 KiB GPU; at 256 square it is about 1.06 MiB CPU and 256 KiB GPU; at 1024 square
it becomes 17 MiB CPU and 4 MiB GPU. These are estimates, not measurements.
The height array can be shared when the owner and lifetime permit it.

Allocate the work arrays at construction/revision, reuse them for rebuilds, and
retain the threshold buffer for upload. Steady state needs no flood, buffer
allocation, or texture upload. Sampling adds a threshold read and smoothstep
where the factor is needed; the actual vertex/fragment and foam cost must be
measured. Neither 60 fps nor a load-time budget has been demonstrated here.

The proposed EditMode fixture is `CutOffPoolConnectivityTests`. Its exact class
filter must use its eventual full namespace. All cases below are required;
none have been added or run by this document-only change.

| Planned case | Required assertion |
| --- | --- |
| `OpenSeaStaysOneAtBothTides` | Explicit sea seeds and adjacent deep water are exactly 1 at low and high tide. |
| `BasinBehindBarDisconnectsAtLowTide` | A wet basin below the bar receives exactly 0; at high tide beyond the blend width it receives exactly 1. |
| `ReconnectionAndCutoffAreContinuous` | At the sill the factor is 0, halfway through the blend it is 0.5, and at the upper endpoint it is 1. Epsilon probes on both sides converge, in either tide direction. |
| `ThresholdsMatchIndependentFlood` | Compare against an independent four-neighbor reachability flood across several tide levels and two competing escape paths. |
| `EqualSeedAndTimeIgnoreEvaluationOrder` | Real tide inputs from the same seed and time agree after fresh construction, replay, time reversal, and different intermediate query orders. |
| `UnseededBoundaryIsNotOcean` | An inland basin cropped by the raster stays unconnected; a different seeded edge remains open. |
| `NarrowBarsAndDiagonalContactsDoNotLeak` | A one-cell bar and diagonal-only contact cannot transmit ocean swell. |
| `CpuAndTextureThresholdSamplingAgree` | Check world bounds, cell centers, sentinels, and exact buffer values at the same positions. |
| `RepeatedSamplingAllocatesNothing` | After warmup, repeated sampling reuses storage and does not rebuild or upload the threshold field. |
| `RegionReplacementClearsOldConnectivity` | Loading, terrain replacement, and unloading cannot leave the previous region's field active. |

Run the exact fixture filter and record discovered, executed, passed, failed,
and skipped counts. Zero executed cases is a failure, not proof. Preserve the
existing still-water, shore, wave, and terrain flood assertions; extend their
contracts where required rather than deleting or weakening them.

## Current evidence and remaining blockers

- Source audit completed; no runtime, asset, shader, or existing test edits.
- Open PR #1336 was observed at head
  `1afb837d287a0e0c694ec7b8cc4dbb03e35c2cfe`. Its overlap blocks integration;
  algorithmic cost has not been shown to be the blocker. Recheck file ownership
  before resuming, using the approved base for that run.
- New connectivity EditMode tests: **0 added, 0 executed**. The integration is
  parked; existing unrelated test results are not evidence for this feature.
- No editor or graphics run was performed. Shader compilation, still-pool
  appearance over wave phases, foam history, smooth reconnection, CPU/GPU
  agreement in a running region, retained pool level, region transitions, and
  frame/texture cost are all unverified.
- Required graphics proof: fixed-seed low/high-tide basin views and a sequence
  through reconnection in both directions, covering flat and displaced water,
  an open-sea control, foam history, a wet edge, and a region reload. Review those
  images before accepting the implementation; do not replace pixel expectations
  simply to make a changed image pass.

Resume with the settled water/foam integration, explicit ocean boundaries, and
a granted editor lease. This draft preserves a concrete design and the source
audit; it does not deliver or claim the requested visible fix.
