# Hidden Harbours — Terrain Material Kit v2

**16 materials × 3 intensity steps = 48 tileable albedo maps, plus 4 edge strips × 3 steps = 12 RGBA decals.**
32 px/m, sRGB. Everything tileable here is exactly periodic; sample it with **Repeat + Point**, no filtering.

```
tex/            48 PNGs — <Material>_Lo.png · <Material>.png · <Material>_Hi.png
edges/          12 PNGs — <Edge>_Lo.png · <Edge>.png · <Edge>_Hi.png   (RGBA, 256×128)
materials.json  manifest: size, metres, projection, offset flag, step names
edges.json      manifest: anchor, wrap rules, usual neighbours, the sampling formula
ladders.jpg     contact sheet — every material at Lo · base · Hi, tiling inside each window
edges.jpg       contact sheet — every strip at Lo · base · Hi, over its usual neighbours
bake/           terrainBake.js + terrainBake2.js + terrainBake3.js — the parametric source of truth
```

### What changed since v1

- **Four new materials** for the shoreline: **Foreshore**, **Talus**, **Ledge**, **Rockweed**.
- **Edge strips** — a new asset type. See §6; it is the part of this kit that needs new shader work.
- **Sand was re-ramped** from a grey-cream granite-coast sand to Island pink-tan. Marram's substrate
  and Shingle's grit matrix share that ramp, so **Marram and Shingle were rebaked too**. If you have
  v1 files in the project, replace all nine; do not mix passes.
- **Ledge is now on the no-chunk-offset list** with the other directional materials.

---

## 1. The contract — what is and is not in these files

**Baked:** intrinsic material colour, grain, and a small **non-directional** cavity/crown term
(crevices darker, crowns lighter). That term is view- and light-independent, so it survives a
day/night colour grade without ever contradicting the sun.

**Not baked, because the shader owns them live:** directional key light, cast shadow, sun AO,
water, pools, wet and damp bands, specular sheen, foam, tide. Do not try to "remove" lighting from
these maps — there is none to remove.

Luminance is held inside roughly **20–236** so a grade has headroom at both ends.

Alpha is 255 across all 48 tiles. The 12 edge strips are the single deliberate exception.

**Foreshore in particular is a dry albedo.** It looks lighter and less red than the reference
photographs of a PEI low-tide flat because those photographs are of *wet* sand. Run your wet band
over it and it lands where it should. Baking the wet in would freeze the tide.

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
bracket becomes two array slices in one sampler. The edge strips array the same way.

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
| **Foreshore** | planed — firm quiet sand, runnels only | a working wave-ripple field | megaripple, shell and gravel lag |
| **Talus** | a scatter of fallen slabs | a closed apron | a deep chaotic blockfield |
| **Ledge** | intact bevelled pavement | dissected — benches, scour pans, weed | stripped to the third bed |
| **Rockweed** | barnacled rock, scattered tufts | a closed olive canopy | deep drape, bladders, Ulva in the wet |

Because wear lives on the ladder, **a footpath, a grazed headland or a storm-scoured berm is a
brush stroke on the intensity channel, not a new material slot.**

---

## 3. Two projections — read this before you write UVs

| | Materials | UV |
|---|---|---|
| **Plan** | Ripple, Shingle, Grass, Marram, Sand, Shelf, Silt, Dirt, Marsh, Sedge, Foreshore, Talus, Ledge, Rockweed | world XZ ÷ tile metres |
| **Face** | **Sandstone, Bank** | **s along the cliff, t DOWN it** |

Sandstone and Bank are cliff **faces**. They belong on face geometry — the vertical band between
beach and headland — not on the terrain mesh. `t` increases downward: bedding in Sandstone runs
horizontal in `s`, rills in Bank run down `t`. Rotate the UVs and the geology is nonsense.

**What a plan view gets at the foot of a cliff is Talus**, not the face material.

```
plan:  uv = worldXZ / metres          // metres = 8 or 16, see materials.json
face:  uv = float2(distanceAlongCliff, heightBelowTop) / 16.0
```

---

## 4. Per-chunk UV offsets — allowed on 9 of 16

Every tile's low-frequency mean is flattened before write, so a hashed per-chunk UV offset will not
reveal a repeating light/dark blotch.

**Apply offsets to:** Shingle · Grass · Sand · Shelf · Silt · Dirt · Marsh · Sedge · Talus
**Never to:** Ripple · Marram · Sandstone · Bank · Foreshore · Rockweed · Ledge

Those seven are directional. An offset slices a ripple train, a wind-combed stand, a bedding plane,
a rill gully or a lie of fronds apart at the chunk border. All seven carry enough low-frequency
variation of their own to hide the repeat without help.

```hlsl
float2 off = hashOffsetAllowed
           ? float2(hash21(chunkID), hash21(chunkID + 17.0))
           : 0.0;
uv += off;
```

Offset the **whole material**, all three steps by the same amount, or the ladder lerp will
cross-fade misaligned images. Edge strips take no offset at all — they are laid along a spline,
not tiled on a chunk grid.

---

## 5. Blending between materials

Two-material blends work directly: sample both at their own intensities and lerp on the splat
weight. Keep the blend band narrow — about **1 m** at 32 px/m — because these are albedo maps with
baked micro-cavity, and a wide dissolve reads as fog. **Under an edge strip, tighten it to about a
quarter of a metre**: the decal is what draws the transition, and a wide fade underneath just makes
mush for it to sit on.

Pairs known to blend cleanly, since they share substrate ramps:

- Marram ↔ Sand ↔ Foreshore (all three on the Island sand ramp)
- Marsh ↔ Silt (Marsh sits on the same anoxic mud)
- Sedge ↔ Dirt (both on the red-bed soil ramps)
- Sandstone ↔ Bank (the hard and soft cliff, same red beds)
- Talus ↔ Ledge ↔ Rockweed (all on the red-bed rock ramps)

Height-aware blending (favour the higher of the two cavity terms) is a clear improvement on a
straight lerp for Shingle, Shelf, Talus and Ledge, where the cavity carries real relief.

---

## 6. Edge strips — the new part

A shoreline is made of **lines**: the sod lip, the erosion scarp, the strand line, the upper limit
of the weed. A noise cross-fade between two band materials cannot draw a line, and it is the line
the eye reads. So each of those four is an asset in its own right.

| Strip | Size | Covers | Line sits at | Usually joins |
|---|---|---|---|---|
| Turf | 256×128 | 8 × 4 m | t = 0.40 | grass → sandstone / bank / talus |
| Scarp | 256×128 | 8 × 4 m | t = 0.24 | grass → foreshore / shingle |
| Wrack | 256×128 | 8 × 4 m | t = 0.46 | sand → foreshore |
| Weedline | 256×128 | 8 × 4 m | t = 0.34 | ledge / shelf → rockweed |

**s runs along the shore and tiles. t runs across the boundary and does not.**
t = 0 is landward/upper, t = 1 seaward/lower. Set **Wrap S = Repeat, Wrap T = Clamp**.

Alpha falls to zero at both t ends. That is the whole point: one strip lays over whatever two
materials happen to meet there, so the same turf lip serves grass-onto-sandstone,
grass-onto-till and grass-onto-talus without a variant.

### Laying one down

Give the shader a signed distance `d` in metres from a shoreline spline and an along-shore arc
length `arc` — the same two numbers you already need to offset bands along the normal:

```hlsl
float  s   = frac(arc / 8.0);
float  t   = (d - lineDistance) / 4.0 + anchor;    // anchor from the table above
if (t < 0.0 || t > 1.0) return bandRGB;            // outside the strip entirely
half4  e   = SampleEdgeLadder(kind, edgeIntensity, float2(s, t));
return lerp(bandRGB, e.rgb, e.a);
```

Offsetting the bands along the **normal** rather than along world X keeps the band widths constant
around a bend, and gives you `d` for free. Arc length can be approximated by the along-shore
parameter of the spline; nothing here is sensitive to a percent of stretch.

The strip's own intensity is a painted channel like any other, so a brow can fail in one bay and
hold in the next off a single asset. Two strips can overlap at a narrow band — composite them
seaward-first, landward-last.

### Straight alpha, not premultiplied

The PNGs carry straight alpha. If your import pipeline premultiplies, either turn that off or
divide it back out before the lerp, or the dark parts of the wrack rope will crush.

### What is deliberately not in a strip

Driftwood **logs** are props — see the shore finds rig — not texture. Only chips and sticks are in
the wrack line. Foam, swash and the wet band are the water shader's. And the cliff-toe boundary
(talus fading into the platform) has **no strip yet**; it is currently a plain noise blend and is
the obvious candidate for a fifth.

---

## 7. Import settings

| Setting | Materials (`tex/`) | Edge strips (`edges/`) |
|---|---|---|
| Texture Type | Default | Default |
| Wrap Mode | **Repeat** | **Repeat S · Clamp T** |
| Filter Mode | **Point (no filter)** | **Point (no filter)** |
| Alpha Is Transparency | — | **on** |
| Alpha Source | — | Input Texture Alpha |
| Compression | **None**, or High Quality | **None** |
| sRGB (Color Texture) | **on** | **on** |
| Generate Mip Maps | off at 1:1; on with Point if it zooms | **off** |
| Max Size | ≥ native (512 / 256) | ≥ 256 |

DXT blocking is visible on the flats, and worse on an alpha decal. Leave these uncompressed until
memory says otherwise.

---

## 8. Seams

Seam step is measured against the distribution of each tile's **own** interior column and row
steps. For all 48, the seam falls inside that distribution; for most, below its mean. The busiest
is Marsh at the base step, whose seam column sits at the 96th percentile of its own interior
columns — busy, not discontinuous. Nothing here needs a fixup pass.

Edge strips are periodic in **s only**, by construction. There is no t seam to check because t
clamps and the alpha is zero at both ends.

If you see a visible seam in engine, suspect **bilinear filtering or a mip chain** before you
suspect the tile.

---

## 9. Re-baking

`bake/terrainBake.js`, `terrainBake2.js` and `terrainBake3.js` are plain browser scripts with no
dependencies. Load them **in that order** — each registers into the same `TB.MATS` / `TB.CFG` /
`TB.SPEC` tables.

```js
TB.bake('rockweed', 2, createCanvas)      // → { albedo: <canvas>, H: Float32Array, N: 256 }
TB3.bakeEdge('weedline', 1, createCanvas) // → { albedo: <canvas>, W: 256, H: 128 }
```

`H` is the height field the cavity pass was derived from. If you ever want a real normal map or a
parallax/height channel, take it from `H` at bake time rather than deriving one from the albedo —
the albedo has colour variation in it that is not relief.

Anything you want changed — palette, coverage, a fourth ladder step, a different tile size, a fifth
edge — change the rig and re-bake. **Do not hand-edit the PNGs**; they will be overwritten.

---

## 10. Rules the rig has paid for, if you extend it

1. **Never let a line feature fall below one texel.** A joint modulated toward zero width aliases
   into dotted stitching. Fade it with opacity and hold the width.
2. **Never shear a Worley lattice to get elongated cells.** Use anisotropic cell counts.
3. **A ramp has to run the whole way**, black hollow to pale dry crown. A ramp that only uses its
   middle reads as a flat wall however much detail sits on it.
4. **Never let a cell field carry a broad wash.** Cell fields draw objects; noise fields draw
   washes. Tie a dry crust to a Worley lattice and you get crazed plaster.
5. **Joints are lines, not polygons.** A 2D `d2−d1` net reads as cracked glaze. A joint set wants a
   1D lattice of wandering traces.
6. **Never ring a feature you mean to fill.** Thatch inside a tussock reads; thatch around it turns
   the tile into a field of worms.
7. **A ripple field has to clear in places** — and the crest-breaking field must be stretched
   ALONG the crest, not across it, or the tile bands into vertical panels of watered silk.
8. **Let the cavity pass carry a ripple, not the ramp.** Feeding height straight into value at full
   strength is what makes wet sand come out glossy.
9. **Talus tessellates; shingle scatters.** Inset a Voronoi cell for angular packed blocks; a
   radius test around the site gives round pebbles however much you jitter it.
10. **A canopy needs gaps**, and never build the substrate under one out of a Worley net — the weed
    fills the polygons and outlines them.
11. **On an edge strip, fibre is value, not coverage.** Fill a solid ribbon and put the fibre
    inside it, or the strand line comes out as dry-brush scratches.
12. **A band of even width is a painted stripe.** Modulate the width and both boundaries of every
    line along s, or it reads as a decal — because it is one.
