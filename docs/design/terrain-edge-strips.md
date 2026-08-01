# Terrain edge strips — the lines a blend cannot draw

**Status:** imported, not wired. The twelve PNGs are in the repo under
`Assets/_Project/Art/Terrain/Edges/` with their decal import settings pinned by
`TerrainSplatBandPinTests`. Nothing samples them yet. This note is the design so the
next person does not have to re-derive it from the kit README.

**Lane:** art-pipeline (the strip array + shader) with a `gameplay-systems` dependency —
the shoreline spline is the part that does not exist yet. See §3.

---

## 1. Why they exist

A shoreline is made of **lines**: the sod lip where turf breaks over a cliff, the erosion
scarp behind a beach, the strand line where the last high tide left its rope of weed, the
upper limit of the rockweed. A noise cross-fade between two band materials cannot draw a
line — it draws a gradient — and it is the line the eye reads as "shore".

The splat system we have blends materials by *weight*. Two materials meeting at a painted
boundary give a soft ragged transition, which is right for sand-into-shingle and wrong for
a sod lip. So each of the four lines is an asset in its own right: an RGBA decal laid
*along* the boundary rather than tiled *across* the ground.

## 2. What shipped in the kit

Four strips × three intensity steps, `256 × 128` px, straight alpha (not premultiplied),
covering **8 m along shore × 4 m across the boundary** at the kit's 32 px/m.

| Strip | Line sits at `t =` | Usually joins | Also works over |
|---|---|---|---|
| Turf | 0.40 | grass → sandstone | grass\|bank, grass\|talus, grass\|ledge |
| Scarp | 0.24 | grass → foreshore | dirt\|shingle, grass\|sand, sedge\|silt |
| Wrack | 0.46 | sand → foreshore | sand\|shingle, shingle\|ripple, marram\|sand |
| Weedline | 0.34 | ledge → rockweed | shelf\|rockweed, talus\|rockweed |

**`s` runs along the shore and tiles. `t` runs across the boundary and does not** — `t = 0`
is landward/upper, `t = 1` seaward/lower, and the alpha falls to zero at both `t` ends.
That zero-alpha margin is the whole trick: one strip lays over *whatever two materials
happen to meet there*, so a single turf lip serves grass-onto-sandstone, grass-onto-till
and grass-onto-talus without a variant per pair.

Import settings are therefore load-bearing and already pinned: **Wrap S = Repeat,
Wrap T = Clamp**, alpha-is-transparency on, mip maps off, Point filter, uncompressed. A
repeating `t` would wrap the seaward edge of the strip back onto its landward edge.

## 3. What is missing — the honest blocker

The sampling formula needs two numbers per fragment that the terrain shader does not
currently have:

- **`d`** — signed distance in metres from the shoreline, negative landward.
- **`arc`** — along-shore arc length, for `s`.

```hlsl
float  s = frac(arc / 8.0);
float  t = (d - lineDistance) / 4.0 + anchor;   // anchor from the table above
if (t < 0.0 || t > 1.0) return bandRGB;         // outside the strip entirely
half4  e = SampleEdgeLadder(kind, edgeIntensity, float2(s, t));
return lerp(bandRGB, e.rgb, e.a);
```

Today `HiddenHarboursTerrainSplat.shader` addresses everything by **world XZ** — materials
tile on a world grid, bands classify by elevation. Neither gives `d` or `arc`. St Peters'
coast is an analytic iso-contour of the painted height field (`StPetersShoreMap`), so `d`
is derivable, but it is a real piece of work and it belongs with whoever owns the
shoreline, not with a texture import.

Offsetting the height bands along the shoreline **normal** rather than along world X would
produce `d` as a by-product, and would independently fix band widths stretching around a
bend. That is the natural way in.

## 4. Design decisions already made

- **Intensity is painted, like any material.** Each strip's step (`_Lo` faint scatter →
  base → `_Hi` storm line) is a 0..1 channel, so a brow can fail in one bay and hold in the
  next off a single asset.
- **Compositing order: seaward first, landward last** where two strips overlap in a narrow
  band.
- **Tighten the material blend underneath to ~0.25 m** (versus ~1 m elsewhere). The decal
  is what draws the transition; a wide fade under it is just mush for it to sit on.
- **No chunk offset, ever.** Strips are laid along a spline, not tiled on a chunk grid.
- **Driftwood logs are props**, not texture — only chips and sticks are in the wrack line.
  Foam, swash and the wet band stay the water shader's.

## 5. Channel budget — read before scoping

The splat model is now **four RGBA maps = 16 channels, 14 spoken for** (kit v2 filled
C.b/C.a and D.r/D.g). **Two remain: D.b and D.a.**

Four strips wanting a painted intensity channel each therefore **do not fit**. Options,
cheapest first:

1. **Region-wide intensity per strip** — a float on the surface, no channel at all. Loses
   "fails in one bay, holds in the next"; probably fine for a first pass.
2. **Two strips painted, two constant** — spends the last two channels and forecloses the
   next material.
3. **A fifth splat map (`StPetersSplatE`)** — 4 more channels. Same shape as the v2 change:
   `TerrainSplatBrush.TextureCount/TextureSuffixes`, `TerrainSplatAssets`, the shader's
   sampler block + Properties, `TerrainSplatSurface.ConfigureSplat`, `StPetersBuilder`, and
   the pin tests. `TerrainSplatBandPinTests.SplatMapCount_CoversEveryMaterialChannel` fails
   the moment a 17th material is declared, so this cannot be walked into by accident.

Note the strips would want their **own** array anyway — they are 256×128, and
`TerrainTexArrayBuilder` builds square 256/512 arrays. A `TerrainEdge256x128` array with
12 slices (4 strips × 3 steps) is the parallel to `Order256`/`Order512`.

## 6. The fifth strip the kit knows is missing

The cliff-toe boundary — talus fading into the platform — has **no strip**. The kit README
names it as the obvious candidate. Re-bake with `TB3.bakeEdge` if it is wanted;
`docs/art/rigs/terrain/terrainBake3.js` is the source.

---

**Kit source:** `docs/art/rigs/terrain/` — `README.md` §6, `edges.json`, `edges.jpg`
(contact sheet: every strip at Lo · base · Hi over its usual neighbours), `terrainBake3.js`.
