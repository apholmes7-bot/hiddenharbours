# Pixel terrain pack — `Art/Textures/TerrainPx/`

Baked by `Art/_pxKit3Bake.js` from `Art/pxKit.js` + `Art/pxKit2.js` through `Art/pixelLanguage.js`
(pass six). The face material still bakes from `Art/pxTerrainRig.js` via `Art/_pxBake.js`. Pixel-first,
hue-true 5-band palettes; shadows cooled toward `#1d3b4a`, lights warmed toward `#e8b06a` (the tree rig's
constants); no dither; ringless. **Regenerate rather than hand-edit.**

## Pass six — 1 px ground texel, and a channel for the paint tool

The ground was authored at a 2 px texel and the sprites at 1 px, so the floor read one step coarser than
everything standing on it. It is now 1 px, the sprites' texel. **Nothing in the engine contract moved:**
tiles are still 256 px = 8 m at 32 px/m, strips still 256 × 24, corners still 48 × 48, UVs unchanged. What
doubled is the texel grid inside the tile, 128 → 256 (`PxKit.DETAIL`). Marks are authored in the old
texels and scaled up, so a cobble is the same 12 cm and has four times the texels to be a cobble with.
Height stays physical and `relief` scales, which keeps one height ruler (`PxLang.H_KIT`) across the kit.

Every material also ships **`_blend`** — what a paint tool needs to lay it over another without averaging
two banded palettes (averaging invents colour that is in neither, which is what the authored strips exist
to replace). R is the order the texels arrive in as the splat weight rises, with every texel of one mark
carrying one value so marks arrive whole; G is height on the shared ruler; B is a mark id. The scoring
rule, the per-material `markLead`/`bias`, and the two frontier rules that recover the cut face and the lit
rim on a painted boundary are all in `TerrainPx.json` under `blend`. **A paint tool chooses per texel; it
never mixes two materials.**

## The language

- **One mark.** Every material is a quiet flat body plus lumps — an ellipse one flat band, key-ward tip a
  band up, down/right seam a band down, a cap toward the light on the big ones, and the shade it throws.
  Tussock, cobble, clod, pebble, fieldstone, boulder: the same lump, 4–10 texels so it reads at 1×.
- **Density, never value.** A slow field decides where marks crowd. No value zones.
- **No directional feature in a wrapping tile.** Ripples are short broken crescents; ruts do not exist here.
- **Value ladder:** see `TerrainPx.json` `valueLadder` — all 21 sorted by mid-band luminance.
- **Edges are authored** (see `Edges/`), or chosen per texel from `_blend`. Never cross-fade two materials.

## Files

`<Mat>{_Lo|""|_Hi}.png` + `_unlit` `_mask` `_normal` `_blend` — 256² = 8 m at 32 px/m, wrapping, 1 px texel.
Grass · Dirt · Path · Mud · Sand · Shingle · Ledge · Marram · Foreshore · Ripple · Silt · Shelf · Talus ·
Bank · Marsh · Sedge · Rockweed · Musselbed · Oysterreef · Eelgrass · Irishmoss. Grass, Dirt, Sand and
Shingle also ship `_v2` and `_v3` (base step, other seeds) — place them at random so a field does not
repeat every 8 m. Bank is a **face** material (s along the cliff, t down it), never on the terrain mesh.
Manifest `TerrainPx.json`.

`Sandstone_<W|SW|S|SE|E>{_Lo|""|_Hi}.png` + channels — the face material, 384 × 288 = 12 × 9 m
(pass two, **not** re-baked in pass six — still a 2 px texel). `Sandstone{…}.png` (unsuffixed) is the S aspect.

`Edges/<A><B>_<N|S|E|W>.png` + channels — edge strips, A over B. 256 × 24 (N, S) / 24 × 256 (E, W).
A's side ships opaque for its first 6 px — lay the strip so those rows sit inside the A tile and the
engine's hard boundary falls under them; B's side is alpha except the marks. Baked per orientation with
the key upper-left: **never rotate a strip.** Pairs: GrassDirt GrassPath GrassSand GrassShingle GrassMud
DirtSand ShingleSand LedgeSand GrassMarram MarramSand SandForeshore ForeshoreRipple LedgeRockweed
TalusShelf MusselbedSilt MarshSilt. Corners `<A><B>_o<Q>` (outer: A fills quadrant Q = NW NE SE SW) and
`<A><B>_i<Q>` (inner: B fills quadrant Q), 48 × 48, same contract. `Wrack_N` / `Wrack_W` — the tide's
line for the sand: weed, straw, shell, pebble on alpha. Manifest `Edges/Edges.json`.

Sprites in the same language (1 px texel, bottom-centre pivot, ringless, same four channels):
`Art/Sprites/Scatter/Rocks.png` boulders (3 kinds × 6 sizes × 2, 48 × 56 cells) ·
`Art/Sprites/Scatter/Stones.png` pebbles (pass two) · `Art/Foliage/Shrubs/Shrubs.png` (5 × 3, 64 × 64) ·
`Art/Foliage/Flowers/FlowersPx.png` (5 species × single/clump/patch, 48 × 40).
Wall: `Art/Tilesets/CliffPx/` — Sandstone reworked as beds + spalls + sockets; brow sod is this kit's turf.

| Channel | Contents |
|---|---|
| albedo | pre-lit at the fixed upper-left key, one band up/down at most |
| `_unlit` | the authored bands, no directional light |
| `_mask` | R key N·L · G rim · B height (0 = the tile's lowest texel) · A coverage |
| `_normal` | texture-space normal, R x · G y-up · B out of the surface |
| `_blend` | R coverage order 1–255 · G height on `blend.hKit` (comparable between materials) · B mark id, 0 = matrix |

## Steps

Lo · base · Hi is the kit's intensity ladder. Grass: grazed-to-soil · sward · rank with flowers (tussocks are
fountains of blades; the marks drift straw-green / blue-green at one value). Dirt: stubble and moss · trodden · churned. Path: after rain · dry · bone dry. Mud: cracked skin · puddled ·
flooded. Sand: damp · dry · wind-rippled. Shingle: pea gravel · cobbles · cobble lag. Ledge: one bench ·
benches · pans and weed.

## Import settings

Wrap Repeat (Clamp on `Edges/`) · Filter Point · Compression None · sRGB on (**off** on `_normal`) ·
Mip Maps off at 1:1.

## Open

The bible's top-of-frame key vs. the tree rig's upper-left — every bake here is upper-left; `PxLang.setKey`
flips all of it once the owner rules.
