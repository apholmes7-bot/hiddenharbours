# ADR 0046 — Still water above the tide: water = max(tide, still), one map for the sim and the render, and a 16-bit height map

- **Status: PROPOSED** — written by terrain pass 9's PR 4 lane (`feat/still-water-r16-height`).
  PR 4 builds the machinery and binds **no still map anywhere**, so every region plays exactly as it
  did before it. **The owner rules** on the contract (§3–§5) and on the recommendation in §8 that the
  committed height maps stay 8-bit. The plate (§10) is judged on its pictures after an editor slot.
  `lead-architect` records.
- **Date:** 2026-09-25
- **Decision owner:** the owner. Part 1's decisions 6, 7 and 11 were ruled as recommended on
  2026-09-25. `lead-architect` owns the Core seam and records this ADR. The other roles:
  - **`gameplay-systems`** owns the on-foot read.
  - **`art-pipeline`** co-owns the two shader reads, for the still-water sampling only.
  - **`world-content`** owns the painted terrain that registers the still water.
  - **PR 5** owns the first still map.
- **Serves:**
  - **P1 The Sea Has Moods:** a pond lies still while the tide works below it, and the sea's edge
    stops stepping across a flat shore.
  - **P3 Living, Working Coast:** there are brooks and ponds above the tide's reach, and the
    islander wades and swims them.
  - **P5 Cozy but with Teeth:** a pond's middle is too deep to wade, so the islander swims it.
- **Amends:** [`docs/design/time-tides-weather.md`](../design/time-tides-weather.md) §3.5. The
  water over a point was the tide; it is now the higher of the tide and the still water there.
  [`docs/architecture/tech-architecture.md`](../architecture/tech-architecture.md) §4.1 gains a new
  Core seam. [ADR 0014](0014-painted-seabed-height-authoring.md)'s open question "R8 vs R16
  precision" is answered by §8: the paint tool writes R16. **Specified by**
  [`docs/design/st-peters-terrain-pass-9.md`](../design/st-peters-terrain-pass-9.md) §4.3 ("What PR
  4's seam must give") and §11 ("PR 4").

---

## 1. The ruling

The owner ruled on 2026-09-25 at 12:21:18Z, typed in part 1's lane: *"i take youur
recommendations"*. That accepted all twelve of part 1's decisions as recommended. Three belong to
this ADR:

- **6:** streams above the tide are real water, through PR 4's seam.
- **7:** PR 2 and PR 4 run in parallel.
- **11:** a 16-bit height map, from −4 to +7 m, carried by PR 4.

The scope came at 10:51Z, with part 1's report: *"also real streams and ponds with real water like
suggested."*

## 2. The problem

**The tide gives one level for a whole region.** `IEnvironmentService.WaterLevelAt(totalSeconds)`
takes a time and nothing else. Every depth in the game is that level minus the ground, so nothing
above the highest spring tide can hold water. At St Peters the highest spring tide is +2.2 m.

- **Bog Pond** has its surface at +5.30 m over a bed at +4.65 m. Today it reads as dry ground at
  every tide.
- **A brook's fresh reach** is 0.10–0.14 m of water over its bed. Today it cannot exist at all.

Painting water there would draw water that the islander walks through without getting wet. The water
shader allows the picture and the sim to disagree only in a bounded, cosmetic way: the swash moves
the drawn edge by at most 0.35 m of level. A whole pond that exists only in the picture is far
outside that.

**The height map's 8 bits are too coarse for this water.** One 8-bit step is:

- 3.9 cm over St Peters' 10 m range;
- 4.3 cm over the −4 … +7 m that part 1 plans.

So a 0.10–0.14 m brook reach is only two or three steps deep. Part 1 also measured that on a flat
shore, one step moves the waterline up to 1.42 m sideways.

## 3. The decision: one composition

**The water level at a plan point is max(tide, still).** Core writes this rule once:

```csharp
public static float Compose(float tideLevel, float stillLevel)
    => stillLevel > tideLevel ? stillLevel : tideLevel;
```

- **It is a comparison, not `Mathf.Max`.** "No still water" is `StillWaterLevels.None`, which is
  −∞. With a comparison, None and NaN both return the tide exactly. Every region without a still map
  relies on that.
- **Depth is still `water − ground`** (`TidalExposure.WaterDepth`). So the wade and swim bands apply
  to a pond exactly as they do to the sea: `WadeDepth` is 0.5 m and `SwimLimit` is 2.0 m. Where the
  ground rises above the still level it is dry, with no second rule.
- **Bog Pond's middle is 0.65 m deep and Fen Pool's is 0.55 m,** so both are swum. A brook's fresh
  reach is waded.
- **The still water never lowers the sea.** Where the tide stands higher, as at a brook's mouth at
  high water, the tide wins.
- **A built surface still decides the standing height.** A footbridge's deck over a brook is dry,
  exactly as a wharf deck over the sea is (`StandableSurfaces.StandingElevation`).

## 4. The contract (Core, `Core/Environment/IStillWater.cs`)

**`IStillWater`**
- `float StillLevelAt(Vector2 worldPos)` gives the still surface in metres above chart datum, or
  `StillWaterLevels.None` where there is no still water. It is deterministic: it depends only on the
  plan point. It does not move with the tide; the tide rises through it.
- `StillWaterMap Map { get; }` gives the texture, world rectangle and level range that the render
  samples. So the ponds that are drawn and the ponds that are waded come from the same bytes.

**`StillWaterMap`**
- Holds `Texture`, `WorldMin`, `WorldSize`, `MinLevel` and `MaxLevel`.
- `IsBound` uses Unity's own null test, so a destroyed texture reads as unbound.
- `default` is the unbound map.

**`EmptyStillWater.Instance`** means there is no still water anywhere.

**`StillWaterLevels`**
- `None` (−∞) and `CodeCount` (65535).
- `Compose`.
- `WaterLevelAt(environment, still, t, pos)` is the tide at `t` composed with the still level at
  `pos`. A null `still` gives the tide alone.
- `DecodeCode01` and `EncodeCode` handle the map's code (§5).

**`GameServices.StillWater`** is **never null**, following the `IFishSchools` precedent.
- With nothing registered, it reads `EmptyStillWater.Instance`.
- A registrant that is a destroyed Unity object also reads as empty.
- `StillWaterChanged` fires on every change.
- `IsRegisteredStillWater(x)` lets a registrant clear only its own still water.
- `Reset()` clears it along with the other seams.

**Who registers it.** `PaintedTidalTerrain` registers its `PaintedHeightMap.StillWater` beside the
terrain in `OnEnable`. It registers null too, when the map has none, and the last writer wins. In
`OnDisable` it clears the slot only if the slot still holds its own still water. The still water
always goes where the terrain goes.

**Derived, never saved (rule 5).** A deriver renders the still map from the region's terrain plan;
that deriver is PR 5's. Nobody paints the map by hand and nothing writes it to the save. The save
format does not change, and the tide is still recomputed, not saved.

**Seams (rule 4).**
- The contract is Core's.
- **World** implements it: `World/PaintedStillWater.cs`.
- **Player** reads it through Core only: `TidalWalkability`.
- **Art** publishes its map to the shaders: `Art/StillWaterGlobals.cs`.
- No feature module references another's concrete class.

## 5. The still map

**The format.** It is a 16-bit, single-channel texture (R16) on **the height map's own scale**: the
same world rectangle and the same `min … max` range as the region's `PaintedHeightMap`.
- The R channel holds the still surface.
- **Code 0 means "no still water".** A real level encodes to at least 1 (`EncodeCode`), so it can
  never read back as none.
- It lives on `PaintedHeightMap`'s optional `_stillLevelTexture`; empty means no still water.
- It must be CPU-readable. If it is not, the map reports no still water and logs a warning, because
  the render must not draw water the sim cannot read.

**Off the map's rectangle there is no still water.** The height map clamps to its edge, but a pond
must not run to the horizon.

**Filter the codes, then decode.** The sim and the shaders both do this in the same order:
1. They filter the normalized code bilinearly. The sim does it in `PaintedStillWater`, through
   `PaintedHeightField`'s bilinear filter, which matches the shader's. The shaders do it in
   `StillLevelAt`, at LOD 0.
2. They then apply one rule: if `r > 0` the level is `lerp(min, max, r)`; otherwise there is none.

Decoding first would average a pond with −∞ at its rim.

**The rim needs one texel of dilation.** Between a wet texel and a code-0 texel, the filtered code
falls toward 0, and its level falls toward `min`. So within the last texel the level sinks below the
pond's surface, and the water's edge pulls in by up to one texel, in the sim and the render alike.
- PR 5's deriver writes each still level at least one texel beyond its shoreline.
- Outside the shoreline the ground stands above that level, so the extra texels read as dry.
- At a brook's step between two reaches, the level ramps from one reach's level to the next across
  one texel.
- **At the head of tide the extra texel is wet, not dry.** Below the head the channel runs on
  under the tide, so no ground stands above the fresh reach's level there. A reach cut exactly at the
  head loses its level through its last texel. Then, at the highest tide, a dry riffle parts it from
  the sea: 0.22 m long on the plate's brook (§10). So the deriver writes the fresh reach one texel
  past the head. The filtered level then holds to the head:
  - at low tide the fresh water ends there;
  - at the highest tide it meets the tide with a step of the brook's own depth, and no dry gap.

**Cost (rule 7).** The map is decoded once into a flat `float[]`: 6.3 MB for a 1520 × 1040 map.
Each query is then four array reads and a lerp. There is never a texture read per call.

## 6. Who reads the still water, and who stays on the tide

### 6.1 The readers

1. **On foot.**
   - `StandableSurfaces.OnFootDepth(terrain, environment, still, surfaces, t, pos)` composes the
     water, and `OnFootDepthNow` passes it `GameServices.StillWater`.
   - `TidalWalkability.IsWalkable`, `DepthAt` and `BandAt` gain overloads that take the still water.
     The old signatures pass no still water, so every existing caller and test reads the tide exactly
     as before.
2. **The water shader** (`HiddenHarbours/Water`). Only the fragment's depth composes. The tide's line
   stays word for word, because `DisplacedWaterMathTests` pins it; the composition is the next line:
   `depth = max(depth, StillLevelAt(worldXY + warp) - elevation);`
   - **The edge work reads the composed depth.** That is the swash's drawn edge, the foam band, the
     cosmetic fringe and the surf's beach gate. So a pond's edge is drawn by the same edge code as the
     sea's.
   - **A pond gets no swell.** The fetch march, the displaced shore fade and the shoal read all stay
     on the tide. Around a pond above the tide, the fetch march finds only land, so no wave field
     rides the pond.
3. **The tidal face** (`HiddenHarbours/TidalFace`). Each pixel reads
   `sea = max(_HHSeaLevelWorld.x, StillLevelAt(faceWorldXY))`. A face standing in a pond is wet up to
   the pond's level, not the tide's.

**Open until the plate.** The swash's in-and-out beat is gated by the sea state, not by fetch. So a
pond's rim may breathe at the sea's swash beat. The shift scales with the shore's floored slope and
is capped at 0.35 m of level. Whether it should is a question of looks. The plate's pictures answer it, and a gate belongs to
the water lane, not to this ADR.

### 6.2 What stays on the tide

| Consumer | Why it stays on the tide |
|---|---|
| Boats: `BoatCrossing`, `HullTideRide`, `BoatController`, and `ControlSwitcher`'s plank and deck | A hull rides the sea, and the plan carries no boat above the tide. A boat on a pond would be a new reader and would need an amendment here. |
| `ClamSpot`, `TrapPlacement` | Clams and traps are intertidal and sea gear. |
| `FishingController` | A pond is not a fishing ground in pass 9. Fish in fresh water would be new content and a new reader. |
| `VehicleGrounding` | The roads skirt the ponds. PR 5 flags it if a track fords a brook. |
| The presenters: `GullFlock`, `SeaweedPresenter`, `ShorePlant`, `SurfSpray`, `FoamInjector`, `ReflectiveObject` | They dress the sea's edge. A reflection in a pond is a later look. |
| `ArrivalOpening` and the radar | They belong to the sea. |
| `TerrainSplatSurface`'s wet band (PR 2's) | It is the tide's wet band. Whether a pond's rim darkens is PR 2's call or PR 5's. |
| `RegionValidation.OnFootDepthAt` (editor) | It validates at the tide's swing. Once a still map exists, PR 5 decides whether it reads the still water. |
| `TidalFaceWaterline` (C#) | It configures the face's lip. The per-pixel composition is in the shader. |
| The water's displaced shore fade, shoal read, and fetch and surf marches | A pond has no swell (§6.1). |
| The analytic `TidalTerrain` | Unchanged; see §9. |

## 7. The render path

**`Art/StillWaterGlobals`** publishes three shader globals from `GameServices.StillWater.Map`, the
same texture the sim decodes:
- `_HHStillTex`;
- `_HHStillRect`, which is (minX, minY, sizeX, sizeY);
- `_HHStillRange`, which is (min, max, bound, 0).

**How the globals are held.**
- Every live `WaterSurface` holds the globals: it calls `Hold` in `OnEnable` and `Release` in
  `OnDisable`.
- While anything holds them, the globals follow `StillWaterChanged`.
- The last `Release` publishes "unset": a 1 × 1 code-0 fallback texture, a zero rectangle and
  bound 0.
- A new play session starts with no holders, so a stopped session cannot leave a pond drawn in the
  editor.

**`Art/Shaders/Include/StillWater.hlsl`** is the one HLSL read, and both shaders include it.
`StillLevelAt(worldXY)` returns −1e30 when the globals are unset, off the rectangle, or at code 0.
Otherwise it returns `lerp(min, max, r)`.
- It is a **twin** of `StillWaterLevels.DecodeCode01` and `PaintedStillWater.StillLevelAt`. Change
  one, and change all three in the same PR.
- Its globals are declared outside every per-material CBUFFER.

**Unset draws exactly what was drawn before.** `max(depth, -1e30 - elevation)` is `depth`, and
`max(tide, -1e30)` is the tide.

**`HiddenHarbours/WaterOverlay` is unchanged.** It re-composes pixels that the water shader already
drew off-screen (ADR 0023), so the still water is already in those pixels. `SeabedGlobals` is
unchanged.

## 8. One height precision: R16

Decision 11 widens the painted height map to 16 bits. PR 4 carries the machinery. The range stays
each map's own data: −4 … +7 m belongs to PR 5's new St Peters map and is not a global change.

**The writer.** The terrain paint tool has two height writers: a stroke's commit, and a map's
creation or analytic export. Both now go through `App/Editor/PaintedHeightPng`.
- It writes codes `round(r × 65535)` with `SetPixelData`, never `SetPixels32`: `SetPixels32`'s
  Color32 would narrow the map back to eight bits.
- It encodes them as a 16-bit grayscale PNG.
- It imports the PNG readable, linear, unmipped and uncompressed, with a **Standalone override that
  names R16** instead of trusting Automatic.

**The sim reader.** `PaintedHeightMap.ReadNormalizedR` reads an R16 texture's codes directly:
`GetPixelData<ushort>` divided by 65535. It reads any other format through `GetPixels().r`, exactly
as before. So an 8-bit map decodes to the same metres it always did, and a 16-bit map keeps all
sixteen bits.

**The shader reader.** On the painted path, `WaterSurface` hands the shaders the painted PNG itself,
so the format it loads in is the format the GPU samples. The samplers are `TEXTURE2D` and read into a
`float`, not `TEXTURE2D_HALF` and not `half`. Both shaders keep their decode,
`lerp(_HeightMin, _HeightMax, r)`.

That holds on the painted path only. A sea on Auto, which every committed region's sea is, does not
read the painted PNG: it bakes the registered terrain into a texture of its own, at most 256², in R16
where the device samples R16 (`SeabedBakeMath.SelectFormat`). The bake keeps the painted map's
precision but has its own density.

**A finding for PR 5: the bake's density.** St Peters' sea bakes 256² over its 760 × 520 m, which
gives 2.97 × 2.03 m cells. A 1.2 m brook falls between those samples, so the islander would wade
water the sea does not draw, and a pond's edge would be smeared across a cell. The still map itself
is sampled at its own resolution, so the fault is in the terrain the water is drawn over, not in the
still level. PR 5 must feed the painted map to St Peters' sea, or bake finer, before its brooks
can be seen. The plate photographs the same density on a second sea (§10).

**The committed maps stay 8-bit.** PR 4 converts neither `StPetersSeabed_HeightTex.png` (−4 … +6 m)
nor `NineMileCreekSeabed_HeightTex.png` (−6 … +6 m). The owner decides this; the recommendation is
to leave them.
- Converting them would be lossless. 65535 = 255 × 257, so code k becomes 257·k, the same height.
- But it gains nothing until someone paints them. It also doubles each file, and it is a binary
  change with no picture to judge it by.
- The first stroke on either map rewrites it at 16 bits, without moving a texel.

**A re-bake is not the same as a widening.** Re-exporting a map from the analytic coast, through a
builder or `BakeAnalyticCoast`, now writes 16 bits of the analytic heights. That moves each texel by
up to half an 8-bit step, toward the analytic value. PR 4 re-bakes nothing: St Peters' scene cannot
be rebuilt, and a batchmode rebuild over an existing scene now refuses to run (#876).

**The scene exporter reads 8 bits only.** `decode_r8` in `Tools/scene-export/hhexport/heightmap.py`
decodes a greyscale-8 PNG and refuses anything else. It does not misread a 16-bit map: every ground
layer built from the height map returns "the height texture did not decode as an 8-bit greyscale
PNG" and is left out of the export. PR 4 commits no 16-bit map, so the export is unchanged. But the
first 16-bit map will hit it, and that is not only PR 5's new map: **the first stroke on a committed
map rewrites it at 16 bits**, and the export then loses that region's ground layers until the
exporter decodes 16 bits. This is flagged for PR 5 and for tools-editor, who own the exporter; the
owner decides who carries it.

**Mobile stays viable (ADR 0005).**
- Not every mobile GPU samples R16; GLES 3.0 needs an extension. A mobile port adds its own platform
  override beside the Standalone one, and checks the format the map loads in, as the Standalone test
  does.
- The shaders must keep their `float` samplers. A `half` read has an 11-bit mantissa and would
  narrow the map again.
- The sim keeps a `float[]` for each decoded map: 6.3 MB for 1520 × 1040, and the same again for a
  still map. The port's memory budget has to count it.

## 9. What PR 4 does not do

- **It binds no still map anywhere.** It commits no St Peters map, no plan Defs, no deriver and no
  scene change. Those belong to PR 5 (spec §11).
- **St Peters' sim reads the analytic `TidalTerrain`,** and its scene has no `PaintedTidalTerrain`
  (spec §12 risk 2, "the analytic sim against the painted map"). So nothing registers still water
  there yet. PR 5's adoption binds the painted terrain and the still map together. If the analytic
  sim stays, PR 5 names another registrant.
- **It changes no tide.** The tide window, the bar crest at +0.88 m and the gut all come from the
  tide model and the terrain, and PR 4 changes neither.

## 10. Consequences

- **It changes nothing until a region has a still map.** With no still map:
  - `Compose` returns the tide exactly;
  - the shaders' read is −1e30;
  - every existing signature passes no still water.
  Every region plays as it did.
- **Three readers use one rule on one set of bytes.** The pond the water draws, the pond a face is
  wet to, and the pond the islander swims are the same texture, decoded by the same rule.
- **The swash question (§6.1) stays open until the plate.** In Phase B, in an editor slot the owner
  grants, a scratch scene is built with a test pond and a stepped brook reach, from a still map made
  for the plate. The plate commits no scene. It shows:
  - the pond at high and at low tide;
  - the brook's fresh reach meeting the tide at its head;
  - a pond's middle, deeper than `WadeDepth`, being swum.

  Its code is `StillWaterPlate`, in App.Editor. Run it from the menu at Hidden Harbours ▸ Dev ▸
  Still Water Plate, or in batchmode with
  `-executeMethod HiddenHarbours.App.Editor.StillWaterPlate.RunBatch`. It needs a GPU and refuses
  without one. It saves no scene and no asset, and writes its pictures and its manifest only to
  `Evidence~/still-water-plate/`. Its brook is written one texel past its head of tide (§5).

  It photographs two seas over the same maps:
  - **Sea 1 is on the painted path.** It reads the plate's R16 height map at its 0.25 m texels,
    with the still map registered and without it, at low, mid and high tide, and once in a working
    sea at high tide.
  - **Sea 2 is on Auto.** It bakes the terrain at 3 m cells, about St Peters' density (§8). It is
    a diagnostic row for PR 5 and is not judged.

  Beside them it draws the walker's own depth bands, with tide water in blue and fresh water in
  green. The owner judges the plate on its pictures. It fails, and exits non-zero, when:
  - a probe's band is not the one its hand arithmetic gives;
  - the maps do not decode to the plate's levels;
  - sea 1 is not on the painted path, or sea 2 does not bake the plate's terrain;
  - a pond's middle is not drawn as water with the still map registered;
  - water is drawn at a pond or the brook with no still map registered;
  - water is drawn on the plateau, above every tide;
  - the sea's pushed level is not the tide, or `_HHStillRange` does not say whether a still map is
    registered;
  - or a shader is still compiling at a capture.

  It warns, for the owner's eye, when the brook is not drawn, when the drawn water and the walker's
  disagree by more than half a square metre, and when a working sea spills past a pond's rim.
- **Guards** (PR 4's tests):
  - `StillWaterSeamTests`: the composition, determinism, the map code, the decode on its own map's
    rectangle and scale, and no still water without a map.
  - `TidalWalkabilityStillWaterTests`: a pond is swum and a brook's fresh reach waded, at every
    tide; a deck over a pond stands dry; off the still water every answer is the tide's, bit for
    bit; the live reads follow the registered still water.
  - `StillWaterRegistrationPlayTests` (PlayMode): a region's terrain registers its still water, the
    last terrain to enable owns it, a region with no still map registers none, and a disable clears
    only its own.
  - `StillWaterGlobalsTests`: the render's globals publish the seam's own map, follow it while a sea
    holds them, and read "none" once the last sea lets go.
  - `StillWaterShaderTests`: the shared read in the water and the faces, no level read in the
    overlay (§7), the unset value, one composition site per shader, and `float` precision.
  - `PaintedHeightMapR16Tests`: a baked height comes back within one 16-bit step, and an 8-bit map
    widens on its first write without moving a texel.
  - `PaintedHeightMapNeutralityTests`: every committed map decodes to the base code's metres, and
    with no still map the water is the tide.
  - Changed: `SeabedHeightImportTests` (`AMapTheToolWrites_IsLoadedAtSixteenBits`: the tool's map
    imports as R16) and `WaterFidelityPlateSweepTests` (every plate pins no still water, reads
    `_HHStillRange` back into its manifest, and fails if a still map is drawn).
