# Cut-off pools stay still

Status: built. The owner's ask (2026-11-19): pools of water cut off from the sea
kept the sea's pulsing swell; once they are not attached to the waves they
should be still pools. Serves P1 (the sea has moods, and still water is one of
them).

## What changed

Swell, surf and the shore's moving edge now reach only the water joined to the
open sea at the current tide. A pool the falling tide has stranded behind a bar
keeps its water but holds still: no wave lift, no swell shading, no surf, no
run-up, no swashing edge, no shore foam line. When the rising tide tops the bar
the pool fades back into the sea's motion over a short blend in tide height,
with no pop.

## The connectivity signal

For each seabed cell, the region stores its **sill**: the lowest water level at
which that cell's water is joined to the open sea.

```
S(cell) = min over paths to an open-sea seed of (max seabed height on the path)
u       = clamp((level - S) / ReconnectBlendMeters, 0, 1)
reach   = u * u * (3 - 2 * u)
```

"Joined at level L" is exactly `S < L` on a four-neighbour grid, so one flood
answers every tide. The tide changes; the sill map never does. Nothing is
flooded per frame or per tick and nothing is uploaded at a tide step.

- **Seeds.** Map-edge cells at or below `SeaReach.SeedMaxElevationMeters`
  (-2.5 m): the ocean at the region's border. A shallow pond or a basin cropped
  by the raster edge is not a seed. A seabed with no seed builds no map and the
  region keeps the older look (it is not declared fixed).
- **Hollows.** A cell whose sill is less than `MinPoolDepthMeters` (5 cm) above
  its own floor is treated as always open. Without this rule every ripple in a
  beach would freeze the wash, and an open shoreline cell (whose sill equals its
  own floor) would fade its own swell.
- **Flood.** A bucket priority flood over the seabed quantized to
  `QuantumMeters` (1 mm). O(cells + buckets), each cell enters the queue once,
  ties broken by cell order: the same seabed and settings give the same map bit
  for bit. No randomness, nothing saved; recomputed from the terrain at load
  (rule 5).
- **The read.** The factor at a point is the factor of the four nearest cells,
  bilinear between their centres, each weighted by how wet the cell stands
  (`WetBandMeters`, 5 cm, of water over its floor gives full weight). Dry ground
  never votes: a cut-off pool's rim stays at 0 and the sea beside a dry bar stays
  at 1, while a reconnecting pool's outline is a one-cell ramp instead of cell
  steps. At a cell centre the read is exactly that cell; where all four cells are
  open it is exactly 1. A point read (the first build) drew the reconnecting pool
  as a pale patch with a stair-stepped outline.
- **Source.** The painted seabed texture the water already draws (the same
  bytes the sim's tidal terrain decodes), or the baked tidal-terrain grid. The
  distance-to-land fallback has no sills and never builds a map.

## Where it is applied

| Consumer | Gate |
| --- | --- |
| Water `frag` | The fetch factors are multiplied by the reach before every wave-field read, so lift, slope, crests, whitecaps and the swell read all go to zero together. The height-read warp, the surf block's outputs (break, cover, lip, barrel, run-up, left foam, face slope), the drawn edge's swash, the bore's edge blend, the wash's lee share, the fringe swash and the shore foam line are each multiplied by the reach. |
| Water `vertDisplaced` | The vertex fetch factors take the same reach, so displaced chunks ride no swell in a pool. |
| Seabed light | The shallow caustic veins drift with time. In cut-off water the same net is drawn frozen (the veins at a fixed time), cross-faded by the reach, so a stranded pool's floor holds still and reconnection eases back into the drift. |
| Foam buffer advect | The bore's deposit `front` is multiplied by the reach, so no surf residue is laid in a pool. Foam already there drifts and decays as before. |
| CPU readers | `GameServices.FetchAt` applies the reach to its `WaveFetchSample` (`WithSeaReach`), so hulls, buoys, hull foam and breakers read no swell in a pool. |

Hull wakes and other local contacts are not gated: a boat in a still pool still
makes its own ripples. The capillary ripple layer is wind-driven (no wind, no
ripples) and is left on: wind ruffles a tide pool too.

Every gate is a multiply by a factor that is exactly 1 where no map is bound,
off the map's rectangle, or where the water is joined to the sea. Open water and
the open shoreline are bit-for-bit what shipped.

## Tide input

The CPU readers use the deterministic tide, `IEnvironmentService.WaterLevelAt`.
The shader uses the drawn `_WaterLevel`, which eases toward that same tide on
the slow tier. The cut-off moment then matches the bar the player sees, and the
reach can never lag behind the drawn waterline. The two differ only by the
existing easing.

## Data and code

- `Core/Environment/SeaReachSettings.cs`: the tunables, in `GameConfig.SeaReach`
  (on/off, reconnect blend 0.25 m, seed depth -2.5 m, hollow depth 0.05 m,
  quantum 1 mm, wet band 0.05 m).
- `Core/Environment/SeaReach.cs`: `SeaReachMath` (the factor),
  `SeaReachField` (the flood and the wet-weighted read) and the `SeaReach` Core
  seam (publish and clear by owner; the factor is 1 with no field).
- `Art/SeaReachGlobals.cs`: builds the field when `WaterSurface` publishes its
  seabed, uploads it as a two-channel half-float texture (sill with the
  sentinels made finite, and floor), and binds `_HHSeaReachTex`,
  `_HHSeaReachRect`, `_HHSeaReachParams` and `_HHSeaReachWet`. Cached by texture, rect and settings; released with the
  water.
- `Art/Shaders/Include/SeaReach.hlsl`: `SeaReach01`, the HLSL twin of the C#
  read (same cells, same weights, same off-map rule, same smoothstep). Four
  `Load()`s blended in code, never a hardware-filtered sample, so a dry cell's
  value can never reach the water.

## Cost

- Load: one flood per region, O(cells + buckets). St Peters' painted seabed is
  1520 x 1040 cells (1.58 M) over a 16 m elevation span (16,000 one-millimetre
  buckets), with about 25 MB of transient arrays.
- Resident: two floats per cell on the CPU (sill and floor, 12.6 MB for St
  Peters) and two halves per cell on the GPU (6.3 MB).
- Per frame: no CPU work. Per water pixel: four texture `Load`s and up to four
  smoothsteps, and the same once per vertex and once per foam-buffer texel. In
  cut-off water only, one extra caustic-vein evaluation.

## Corrections to the earlier design draft

- **No new World dependency.** The draft proposed extracting
  `TerrainPlanFlood` into Core. The flood here is a separate bucket flood in
  Core, tested against an independent breadth-first flood. `TerrainPlanFlood`
  floods from every raster border, which is the wrong seed rule for the sea.
- **Seeds come from a rule, not authored data.** The draft asked for explicit
  sea seeds in region data. Every shipped region's ocean meets the map edge
  deeper than -2.5 m and no shipped pond does, so a depth rule on the border
  covers them with no new data. A region where it would not hold builds no map
  and keeps the older look.
- **The open shoreline.** The draft's formula alone would fade the swell along
  every open shore, because an open shoreline cell's sill equals its own floor.
  The hollow rule treats those cells (and shallow beach ripples) as always open.
- **Texture format.** The draft proposed a single RFloat sill. The read needs
  the floor too (for wetness); RGHalf holds a sill to about a millimetre near
  datum and a floor to under a centimetre, and RGFloat is the fallback.
- **Point sampling.** The draft asked for a point read so a joined cell could
  never leak into a pool. That is right for the cut-off state but draws the
  reconnection in cell steps. The wet-weighted read keeps the no-leak property
  (dry ground has no vote) and feathers the outline.
- **Swell was not the only motion.** The draft gated swell, surf and foam. The
  shallow caustic veins also drift with time and made a stranded pool's floor
  crawl; they are now held still in cut-off water.
- **GPU tide input.** The draft asked the shader to read the deterministic tide
  rather than the eased drawn level. The shader uses the drawn level so the
  stillness starts exactly when the drawn bar dries. The CPU uses the
  deterministic tide.
- **Retained pool level** is unchanged. A stranded pool still drains with the
  tide down to its own floor, as before. Holding a pool's level at its sill after
  cut-off is a separate change (it moves the gameplay waterline) and is not part
  of this one.

## Tests

`HiddenHarbours.Tests.EditMode.SeaReachTests` (synthetic height fields):

- open sea is 1 at every tide
- a basin behind a one-cell bar is 0 below the bar and 1 one blend above it
- the factor is continuous and monotonic through the blend, with no step
- the sills agree with an independent four-neighbour flood at seven tides
- the same inputs give the same field, and the same factor for the same seed
  and time in any query order
- a basin cropped by the map edge is not ocean
- a sealed basin joins only over the land
- a diagonal contact and a one-cell bar do not leak
- the read feathers between wet cells, and dry ground never votes (a pool's rim
  at a dry bar is 0, the sea beside it is 1, the reconnecting outline is a ramp)
- beach hollows under 5 cm stay open
- reads allocate nothing, and off the map the factor is 1
- the Core seam publishes and clears by owner, and is 1 with no field
- the fetch sample carries the reach and is unchanged at 1
- the shipped config is usable, and the shader carries the twin read and the
  still seabed light

## Verification by eye

St Peters' tidal channel at about (-10, 96) lies on the flat about 17 m inside
the low-tide shoreline. Its bar is at -0.50 m and its floor at -1.39 m, so it
strands every low tide below -0.50 m. Clips at a low near noon (level -0.90 m,
12.45 h), framed on the pool with the live shoreline in the same shot, were
taken on main and on this branch, with a second clip following the rising
tide from -0.75 m through reconnection to -0.15 m.

- **Stranded.** On main the pool carries a white swashing rim and its floor
  crawls. After: no rim foam and no swash, and the floor's light holds still.
  Mean frame-to-frame change inside the pool fell from 1.42 to 0.37 (grey
  levels), while the open sea in the same shot stayed at 0.94 to 0.95. The
  open shoreline's motion map is unchanged.
- **Reconnecting.** The pool takes the sea's motion back over about 0.25 m of
  tide (36 s at the game's rate), with no step. Mid-blend it reads as a calm
  patch with a soft outline that fills in.

## Known limits

- **Rim band while reconnecting.** For the first part of the blend a faint pale
  band sits just inside the drowned rim, then fades. During reconnection the
  transition from the pool's reach to the sea's sits on the shallow rim, and the
  surf and wash terms there follow the partial reach.
- **Rim specks.** For the few centimetres of tide when the bar is within the
  run-up height of the water, a pool's dry rim can still show single swash
  pixels where the drawn edge's reach comes from the joined side.
- **Ponds above the tide** (the still-water map) are also unreachable from the
  sea, so they take no shore foam line either. They never had swell; their
  fringe line is now gone.
- **Retained level** is as before (see above).
- **Wind ripples** stay on in a pool (wind-driven, not swell).