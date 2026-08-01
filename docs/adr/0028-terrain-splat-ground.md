# ADR 0028 — Splat-shaded ground: the terrain is a painted field, not a grid of tiles

- **Status:** Accepted (owner-directed, 2026-07-30)
- **Deciders:** owner, art-pipeline, lead-architect
- **Phase:** M1 (owner-directed look arc; visual-only, no sim or save impact)
- **Related:** ADR 0010 (water rendering), ADR 0012 (shoreline rendering), ADR 0014 (painted
  seabed height authoring), ADR 0019 (hand-authored scenes + refresh), ADR 0023 (displaced
  water surface), `docs/design/water-rendering.md`

## Context

The ground of St Peters is painted as a Tilemap: `StPetersShorePainter` classifies every
1 m cell against the height field (`StPetersShoreMap`) and stamps a 32 px ground tile per
cell from the shoreline-ISO kit, with fringe overlays where materials lap. The kit is good
art, but the method has a ceiling the owner has now hit (2026-07-30): **a grid of repeated
cells reads as a grid of repeated cells.** At gameplay zoom the grass and the bared flats
show visible periodic repetition; against the reference footage the owner supplied (smooth
splat-blended terrain with organic transitions and a breathing tide line) the difference is
jarring, and "if I'm not happy looking at the ground, it's hard to feel inspired."

The sea half of that reference we already have. ADR 0010/0014 made the water a **shader
over a painted continuous field**: one height map, a shader-owned waterline, depth-graded
colour — no tiles anywhere. The land half stayed on tiles.

The repetition is inherent to tiling, not a defect of the tile art. Fixing it means
changing the method, not the art.

## Decision

Render the region's ground as a **single full-region mesh with a splat-style terrain
shader** (`HiddenHarbours/TerrainSplat`), replacing the per-cell ground/fringe tile layers.
The same move ADR 0010 made for the sea, applied to the land:

1. **One field, not many cells.** The shader consumes the SAME painted height data the
   water and the walk gate read (`_HeightTex` / `_HeightMin` / `_HeightMax` /
   `_HeightWorldMin` / `_HeightWorldSize` — the exact uniform vocabulary
   `HiddenHarboursWater.shader` already uses), so the picture and the gameplay can never
   disagree (rule 5, and ADR 0012's "one number" principle).
2. **The bands come from the classifier, not a second opinion.** The shader ports
   `StPetersShoreMap`'s elevation ladder (paint floor −2.6, ripple −1.7, sand −0.4,
   marram 1.6, grass 4.2; weather coast: shingle −0.4; sandbar spine exempt from the
   wiggle) as *soft* thresholds — a metre-scale blend with the same two-octave meander
   noise (0.8 m @ 16 m + 0.3 m @ 6 m) instead of a per-cell hard pick. The CPU classifier
   remains the source of truth: the builder pushes its constants into the material at
   build time, and a test pins the shipped material defaults to the C# constants so the
   two cannot drift silently.
3. **Repetition is broken by construction.** Detail comes from world-space value noise
   (aperiodic — nothing to repeat), quantised to the art's pixel grid so it reads as
   pixel-art grain, plus a low-frequency macro-variation tint at tens of metres. When
   hand-painted detail textures land (see Consequences), they are sampled stochastically
   (hashed per-cell offsets, the `Hash22 * 64` idiom already proven in the water shader)
   so their repeats never align either.
4. **The tide line gets its wet band.** The shader reads `_WaterLevel` (same push as
   `WaterSurface`) and darkens a tunable band of ground just above the waterline — the
   highest-impact detail in the owner's reference, and free once land and sea share the
   height field and the tide.
5. **Sorting:** the ground mesh renders through a `SortingGroup` (the ADR 0023 mesh-in-2D
   pattern) BELOW the tile band, at order **−21** — under the retained contact/rock
   layers (−18) and, critically, under the Sea plane (−5), so the ADR 0012 tide reveal
   (`clip(depth)`) keeps working unchanged.
6. **The kit is not deleted.** Ground + fringe tile layers are simply not stamped when
   splat ground is on (a builder flag, default ON for St Peters); contact pieces and the
   reef rocks remain. Flipping the flag and rebuilding restores the tiled coast exactly —
   the A/B the owner steers by (ADR 0019 refresh model). Hard-edged uses of the iso kit
   (cliff faces, sculpted edges) remain available where wanted.

### What this is NOT

- **Not a sim change.** The shader is look-only; walkability, clam-baring, and the
  crossing gate keep reading `ITidalTerrain` / `WaterLevelAt` directly.
- **Not a save change.** Nothing new is saved; everything is recomputed from painted data
  plus `(worldSeed, gameTime)`.
- **Not a new authoring model.** The owner keeps painting height in the Terrain Paint
  Tool (ADR 0014). A later arc step adds a *material override* brush only if band-from-
  height proves insufficient — not before (rule: earn the complexity).

## The arc (small PRs, in order)

1. **PR 1 — the shader + the surface** (this ADR's landing): `HiddenHarbours/TerrainSplat`
   + `TerrainSplatSurface` (full-region quad, `[ExecuteAlways]`, MPB-driven) + St Peters
   builder wiring + compile-guard and band-pin tests. Procedural two-tone grain per
   material stands in for detail textures.
2. **PR 2 — detail textures**: 6 hand-painted tileable detail textures (art-director
   brief below), stochastic sampling, per-material UV scales.
3. **PR 3 — transition dressing**: band-edge scatter (shell hash, wrack line at high-water
   mark, pebble spill where shingle meets sand) — decor systems already exist.
4. **PR 4 — the other regions**: Nine Mile Creek + Coddle Cove adopt the surface; retire
   the greybox colour grids where superseded.

### Art contract (PR 2 input — the owner's ask list)

Six tileable detail textures, KTC palette, authored like every other rig source under
`docs/art/rigs/` and imported per `.gitattributes`/LFS. **Sizing is set by the world area a
canvas covers at the locked PPU 32, not by texture-memory thrift** (owner challenge,
2026-07-30): stochastic offsets stop repeats *aligning* but cannot add variety that is not
on the canvas, so materials whose features live at the 5–15 m scale get the larger canvas.
All six together ≈ 2 MB uncompressed — noise against the budget.

| Key | Material | Size | Covers | Reads as |
|---|---|---|---|---|
| `ripple` | red flats | **512 × 512** | 16 × 16 m | iron-red rippled mud, meandering ridge trains — the clam ground, the hero |
| `shingle` | cobble | **512 × 512** | 16 × 16 m | wave-rounded cobble with stone-size sorting bands, the bar's walking line |
| `grass` | meadow turf | 256 × 256 | 8 × 8 m | close mossy turf, not lawn — wind-burned island green |
| `marram` | dune grass | 256 × 256 | 8 × 8 m | sparse olive blades over sand showing through |
| `sand` | beach | 256 × 256 | 8 × 8 m | pale storm-sifted sand, faint drift lines |
| `shelf` | scoured rock | 256 × 256 | 8 × 8 m | wave-planed dark platform with weed stain |

Each must tile seamlessly (the shader's stochastic offsets hide repeats but not seams) and
stay readable at 1:1 zoom under the day/night grade (ADR 0013). If a material still reads
flat at 16 m, the fix is a second, larger-scale variation texture layered in the shader —
not a bigger detail canvas.

### PR 2 addendum (2026-07-30, on kit delivery)

The delivered kit (`docs/art/rigs/terrain/` — bake rigs are the source of truth, PNGs are
derived) exceeded the contract: **12 materials × 3 intensity steps** (_Lo / base / _Hi).
The ladder replaces "one texture per material": low intensity is *designed* to read sparse
(pioneer sprigs, planed-off ripple), so a painted channel's value serves as **both** the
blend weight against the height bands and the ladder position — a footpath or a grazed
headland is a brush stroke on intensity, not a new material slot. Ten plan-projection
materials are wired (the contract six + Silt, Dirt, Marsh, Sedge, paint-only); the two
FACE-projection materials (**Sandstone, Bank** — cliff faces with directional geology) are
imported but deliberately unwired until cliff geometry exists. Textures are packed into two
GUID-stable Texture2DArrays (`TerrainTexArrayBuilder`); the kit's rules are honoured in the
shader: Repeat + Point sampling, hashed per-cell UV offsets only on the manifest's
offset-allowed materials (never the directional ripple/marram), macro variation stays
shader-side because the kit flattens each tile's low-frequency mean on purpose. Painting
lands as RGBA splat maps (one channel per material) authored by the Terrain Paint Tool —
the "material override brush" this ADR deferred, now owner-directed.

### PR 3 addendum (2026-08-01, terrain kit v2)

Kit v2 added four shoreline materials — **Foreshore, Talus, Ledge, Rockweed** — taking the
wired plan-projection set from ten to **fourteen** at canonical indices 10–13. Ten channels
had already filled three RGBA maps through C.g, so a **fourth splat map (`StPetersSplatD`)**
joined A/B/C: 16 channels, 14 spoken for, D.b and D.a free. Indices 0–9 are frozen and
asserted — committed splat PNGs encode what each index *means*, so a reorder would repaint
painted ground silently instead of failing. Sand was re-ramped to the Island pink-tan in
this pass; Marram and Shingle share that substrate, so all three were rebaked together and
must be replaced as a set.

v2 also introduced a new asset TYPE: four **edge strips** (turf lip, scarp, wrack line,
weed line) — RGBA decals laid *along* a boundary rather than tiled across the ground,
because a weight blend draws a gradient and a shoreline is made of lines. They are imported
with their decal import settings pinned, but deliberately **not wired**: sampling one needs
a signed distance and an along-shore arc length from a shoreline spline, which this
shader's world-XZ addressing does not provide. Design, and the channel-budget consequence
that four painted strip intensities do not fit the two remaining slots:
`docs/design/terrain-edge-strips.md`.

## Consequences

- ✅ The repetition class of defect is gone structurally, not papered over.
- ✅ One draw call + a few noise samples replaces thousands of tile sprites in the ground
  band — comfortably inside the 60 fps budget, and kinder to the later mobile port.
- ✅ Land and sea now share one authoring model (painted fields + shaders), one height
  map, one tide number.
- ⚠ The shader is a second implementation of the band *look* (the CPU classifier remains
  authoritative for gameplay and for rocks/contact placement). The pin test holds the
  constants together; the meander noise is intentionally NOT bit-identical (look-only,
  same parameters, different lattice hash — documented in the shader header).
- ⚠ Fringe tile art (3 rows of the iso kit) goes dormant on St Peters. Kept in the repo;
  hard edges may re-use it.
- ⚠ Anything that assumed "ground = Tilemap" (e.g. future grid-snapped decals) must read
  the height field instead — which is the correct seam anyway (rule 4).
