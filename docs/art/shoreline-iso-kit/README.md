# Hidden Harbours — Shoreline ISO Tile Kit (v7)

The PEI red-sandstone coast, rebuilt as an **ISO terrain kit** that matches the boat bake.
One zip: the baked tile/sprite sheets, their contract + sprite sidecar, and the parametric
rig that re-bakes any of them. This is the **updated** shoreline set — it replaces the older
near-plan `shorelineKitRig` sheets (still in the project at `Art/Tilesets/Shoreline/`).

Conventions (ADR-0006 / 0022): square **32×32**, **32 px = 1 m**, ¾ camera from the **SOUTH
at 40°** (the fleet's turntable elevation), upper-left key light, **band-edge-only Bayer dither
world-locked to global pixel coords** (zero crawl), no AA, KTC ramps.

## The water contract (why this kit bakes ZERO water)

The engine shader owns **all** water (ADR 0010 / 0012 / 0023): it clips at the live depth-0 tide
contour, rides foam/swash on it, and pins the displaced 3D surface to that same line (ShoreFadeMath).
So **no tile here bakes water, foam, a waterline, or shallows.**

- **The tide sweeps whole flats**, so every ground material is authored to read right dry AND
  submerged — the shader wets, tints and covers it by depth.
- **Rule-tiles carry terrain-TYPE edges only** (grass↔sand↔rock) + permanent landforms (cliff, dune).
  The live wet edge is never a tile — butt any tile straight against shader water; nothing to line up.

## Files

Tile sheets pin by their **top-left cell origin**; sprites pin by the pivots in the sidecar.

- **ShoreIsoGround.png** (96×192) — opaque ground. Rows: `grass · marram · sand · ripple ·
  shingle · shelf`. Cols: 3 adjacent world tiles (noise is world-locked; bake any gx,gy from the rig).
- **ShoreIsoFringe.png** (384×96) — transparent terrain-type overlays; stamp OVER the neighbour's
  ground tile. Rows: `grass · marram · sand`. Cols: `edN edE edS edW · coNE coSE coSW coNW ·
  inNE inSE inSW inNW` (4 edge · 4 outer corner · 4 inner corner).
- **ShoreIsoCliff.png** (320×96) — red-sandstone cliff bands. Rows: `cap · mid · toe`. Cols:
  `faceS cornSW cornSE sideW sideE innSW innSE diagSW diagSE caveToe`. Stack `cap + mid×N + toe`
  for any height (~1.3 m of face per band at the 40° camera); strata key on global row Y so bands align.
- **ShoreIsoDune.png** — marram dune bank, single band, same 9 corner/edge pieces (no cap/mid/toe).
- **ShoreIsoSprites.png + .json** — freestanding rock: sea stacks `reef/s/m/l` + slab boulders
  `bs/bm/bl`. `.json` carries each item's rect + base-centre pivot.
- **ShorelineIso.json** — the kit contract (grid, water note, per-sheet row/col maps, rig path).
- **shoreIsoKitRig.js** — the parametric source; re-bakes any tile/height/sprite at will.
- **_preview-hero.png** — the assembled coast (reference only, not for import). The open water in it
  is a PREVIEW emulating the shader (depth-0 contour, DepthRamp tint `#8EA59C`→`#0F2227`, foam lace) —
  in engine the water is live.

## Rig API (`globalThis.ShoreIso`)

Plain browser script, no deps. Every call returns `{ data:Uint8ClampedArray, w, h }`.

- `GROUND = ['grass','marram','sand','ripple','shingle','shelf']`
- `ground(mat, {gx,gy,seed})` → 32×32 opaque tile (world-locked noise → seamless, never repeats)
- `FRINGE_PIECES` (12) · `fringe(mat, piece, {seed,lip})` → overlay on transparency
- `CLIFF_PIECES = ['faceS','cornSW','cornSE','sideW','sideE','innSW','innSE','diagSW','diagSE']`
- `cliff(piece, {band:'cap'|'mid'|'toe', gy, seed, feature:'cave'|null})` — `feature:'cave'` carves the arch
- `column(piece, rows, {seed,feature})` → a pre-stacked cliff column (cap + mid×N + toe)
- `dune(piece, {seed})` → single-band dune piece
- `stack(size 'reef'|'s'|'m'|'l', {seed})` · `boulder(size 's'|'m'|'l', {seed})` → pure-rock sprites

## Wiring cheat-sheet

1. **GROUND** — paint the terrain type per cell with `ground(mat,{gx,gy})`. Noise is a pure function
   of world coords, so neighbours butt seamlessly and runs never repeat.
2. **FRINGE** — where two materials meet, stamp `fringe(mat,piece)` over the neighbour's tile for a
   ragged tongue (grass/marram carry a 1px soil under-shadow on camera-facing edges).
3. **LANDFORMS** — build cliffs with `column()` (or hand-stack cap/mid/toe) to any height; `dune()`
   for the marram bank. Corners wrap a lit-W / shaded-E facet; side pieces are the thin edge-on strip.
4. **WATER** — do nothing. Butt land straight against the shader's water; the tide meets the toe /
   flats wherever they stand.
5. **SPRITES** — drop sea stacks / slab boulders anywhere via their base-centre pivots.

## Known limits (v7)

- N-facing cliff back-lips reuse the plateau grass tile (occluded at this camera); diagonal pieces
  are 45° only.
- Overlay dressing (marram tufts, driftwood, fences, spruce) is NOT in this kit — it comes from the
  Wildflowers / Seaweed / Shoreline Finds kits + `ShoreOverlays.png`; all composite fine on this ground.
- The old near-plan sheets remain untouched in `Art/Tilesets/Shoreline/`.

## Demo page (in the main project, not this zip)

`Shoreline Kit.dc.html` — the full v7 handoff with the assembled coast, every sheet, the water
contract, and the earlier passes for reference.
