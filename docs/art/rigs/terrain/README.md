# Hidden Harbours — Terrain Material Kit

**12 materials × 3 intensity steps = 36 tileable albedo maps.** 32 px/m, sRGB, alpha always 255.
Everything here is exactly periodic; sample it with **Repeat + Point**, no filtering.

```
tex/          36 PNGs — <Material>_Lo.png · <Material>.png · <Material>_Hi.png
materials.json  machine-readable manifest (size, metres, projection, offset flag, step names)
ladders.png   contact sheet: every material at Lo · base · Hi, tiling inside each window
bake/         terrainBake.js + terrainBake2.js — the parametric source of truth
```

---

## 1. The contract — what is and is not in these files

**Baked:** intrinsic material colour, grain, and a small **non-directional** cavity/crown term
(crevices darker, crowns lighter). That term is view- and light-independent, so it survives a
day/night colour grade without ever contradicting the sun.

**Not baked, because the shader owns them live:** directional key light, cast shadow, sun AO,
water, pools, wet and damp bands, specular sheen, foam, tide. Do not try to "remove" lighting
from these maps — there is none to remove.

Luminance is held inside roughly **20–236** so a grade has headroom at both ends.

---

## 2. Sampling the intensity ladder

Each material is one 0–1 channel in the splat/intensity map. The shader brackets the value and
lerps between two neighbouring steps of the **same** material:

```hlsl
// intensity: 0..1 from the painted channel for this material
float f  = intensity * 2.0;
int   a  = (int)floor(f);
int   b  = min(a + 1, 2);
float k  = f - a;
half4 c  = lerp(SampleStep(a, uv), SampleStep(b, uv), k);
```

The three steps share seeds, palettes and low-frequency layout, so this lerp moves one material
through **its own range** — it never cross-fades two unrelated images.

**Nearest instead of lerping is valid and cheaper** (`int s = (int)round(intensity * 2.0)`). It
costs the smooth transition, not correctness. Use it on distant chunks.

Pack the three steps of a material as a **Texture2DArray** (`_Lo`=0, base=1, `_Hi`=2) and the
bracket becomes two array slices in one sampler.

### What the ladder means, per material

| Material | 0.0 `_Lo` | 0.5 base | 1.0 `_Hi` |
|---|---|---|---|
| Ripple | relict, all but planed off | working ripple field | storm-built, gravel lag in troughs |
| Shingle | pea gravel and coarse grit | mixed shingle | cobble lag, big stones proud |
| Grass | grazed, trodden thin turf | sward | rank meadow with seed heads |
| Marram | pioneer sprigs on open sand | stand | closed hummock, heavy thatch |
| Sand | hardpack, quiet and fine | dry sand with lineation | coarse, shelly, wind-rippled |
| Shelf | intact pavement | slabs plucked out, second bed showing | stripped to the third bed |
| Sandstone | fresh face, recently collapsed | weathered — joints open, blocks off | badly eroded, deep hollows |
| Bank | firm, faintly rilled | well rilled, clods loose | failing — deep gullies, hanging turf |
| Silt | soft fresh sheet, barely drained | drained, burrowed, patchy crust | crusted, cracked into curled plates |
| Dirt | a worn line, stubble and moss holding | bare compacted earth, pebble lag | churned — clods, loose tilth, grooves |
| Marsh | pioneer sprigs on open creek mud | closed cordgrass sward | high marsh — thatch, wrack, pans |
| Sedge | short sedge lawn, peat and moss showing | tussocks | rank tussocks, heavy thatch, rush |

Because wear lives on the ladder, **a footpath, a grazed headland or a storm-scoured berm is a
brush stroke on the intensity channel, not a new material slot.**

---

## 3. Two projections — read this before you write UVs

| | Materials | UV |
|---|---|---|
| **Plan** | Ripple, Shingle, Grass, Marram, Sand, Shelf, Silt, Dirt, Marsh, Sedge | world XZ ÷ tile metres |
| **Face** | **Sandstone, Bank** | **s along the cliff, t DOWN it** |

Sandstone and Bank are cliff **faces**. They belong on face geometry — the vertical band between
beach and headland — not on the terrain mesh. `t` increases downward: bedding in Sandstone runs
horizontal in `s`, rills in Bank run down `t`. Rotate the UVs and the geology is nonsense.

```
plan:  uv = worldXZ / metres          // metres = 8 or 16, see materials.json
face:  uv = float2(distanceAlongCliff, heightBelowTop) / 16.0
```

---

## 4. Per-chunk UV offsets — allowed on 8 of 12

Every tile's low-frequency mean is flattened before write, so a hashed per-chunk UV offset will
not reveal a repeating light/dark blotch.

**Apply offsets to:** Shingle · Grass · Sand · Shelf · Silt · Dirt · Marsh · Sedge
**Never to:** Ripple · Marram · Sandstone · Bank

Those four are directional. An offset slices a ripple train, a wind-combed stand, a bedding plane
or a rill gully apart at the chunk border. All four carry enough low-frequency variation of their
own to hide the repeat without help.

```hlsl
float2 off = hashOffsetAllowed
           ? float2(hash21(chunkID), hash21(chunkID + 17.0))
           : 0.0;
uv += off;
```

Offset the **whole material**, all three steps by the same amount, or the ladder lerp will
cross-fade misaligned images.

---

## 5. Blending between materials

Two-material blends work directly: sample both materials at their own intensities and lerp on the
splat weight. Keep the blend band narrow — about **1 m** at 32 px/m — because these are albedo
maps with baked micro-cavity, and a wide dissolve reads as fog.

Pairs known to blend cleanly, since they share substrate ramps:

- Marram ↔ Sand (Marram's substrate uses the Sand ramp)
- Marsh ↔ Silt (Marsh sits on the same anoxic mud)
- Sedge ↔ Dirt (both on the red-bed soil ramps)
- Sandstone ↔ Bank (the hard and soft cliff, same red beds)

Height-aware blending (favour the higher of the two cavity terms) is a clear improvement on a
straight lerp for Shingle and Shelf, where the cavity carries real relief.

---

## 6. Import settings

| Setting | Value |
|---|---|
| Texture Type | Default (Sprite if the painter samples through sprites) |
| Wrap Mode | **Repeat** |
| Filter Mode | **Point (no filter)** |
| Compression | **None**, or High Quality — DXT blocking is visible on the flats |
| sRGB (Color Texture) | **on** |
| Generate Mip Maps | off if the camera is locked to 1:1; on with Point filtering if it zooms |
| Max Size | ≥ native (512 / 256) |

---

## 7. Seams

Seam step is measured against the distribution of each tile's **own** interior column and row
steps. For all 36, the seam falls inside that distribution; for most, below its mean. The busiest
is Marsh at the base step, whose seam column sits at the 96th percentile of its own interior
columns — busy, not discontinuous. Nothing here needs a fixup pass.

If you see a visible seam in engine, suspect **bilinear filtering or a mip chain** before you
suspect the tile.

---

## 8. Re-baking

`bake/terrainBake.js` (the original six) and `bake/terrainBake2.js` (the six PEI additions) are
plain browser scripts, no dependencies. Load the first, then the second — `terrainBake2.js`
registers into the same `TB.MATS` / `TB.CFG` / `TB.SPEC` tables.

```js
TB.bake('sandstone', 2, createCanvas)   // → { albedo: <canvas>, H: Float32Array, N: 512 }
```

`H` is the height field the cavity pass was derived from. If you ever want a real normal map or a
parallax/height channel, take it from `H` at bake time rather than deriving one from the albedo —
the albedo has colour variation in it that is not relief.

Anything you want changed — palette, coverage, a fourth ladder step, a different tile size — change
the rig and re-bake. **Do not hand-edit the PNGs**; they will be overwritten.
