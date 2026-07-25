# Hidden Harbours — Road / Path / Sidewalk Kit

Flat **32×32 near-plan** ground tiles that sit IN the ground plane exactly like `Grass.png` /
`Dirt.png`, so they auto-register with the iso houses, the wharf deck and the shoreline flats.
32 px = 1 m · camera from the south · no AA · muted North-Atlantic KTC ramps · hash-value noise.

Procedural: **one rig bakes every tile**, and a full **47-tile blob autotiler** picks each tile's
shape from its 8 neighbours. This zip ships the rig (the source of truth), a pre-baked reference
atlas per surface, and an in-context preview.

## Files

- **roadPathRig.js** → `globalThis.RoadKit` — the parametric source. Plain browser script, no deps.
- **RoadIso_&lt;surface&gt;_new_blob47.png** × 7 — pre-baked reference atlases (one per surface at
  `new` wear, grass verge, no markings). 12 cols × 4 rows, sorted by neighbour mask: isolated · caps ·
  straights · bends · tees · crosses (with concave grass fillets). `worn`/`cracked`, other verge
  grounds, markings, and full painted maps all bake from the rig / the demo page.
- **road-scene.png** — in-context preview: asphalt crossing · concrete sidewalk · cobble & sand
  paths · brick apron, flush with the grass and the iso buildings.

## Surfaces · wear · verge

- **7 surfaces**: `dirt · gravel · concrete · asphalt · cobble · sand · brick`
- **3 wear states**: `new → worn → cracked`
- **Verge ground**: shoulders blend into `grass · dirt · sand`

## Autotiling — full 47-blob

Every cell re-derives its shape from **4 edges + 4 diagonals**:

- A **connected** side fills to the tile border (seamless with same-family neighbours).
- A **disconnected** side pulls the surface in to an organic grass/earth verge.
- Two disconnected adjacent sides **round the outer corner** (caps, bends, isolated blobs).
- Two connected sides whose shared diagonal is empty **carve a concave grass fillet** (the T / + armpits).

Structured patterns (brick / cobble / concrete joints / lane markings) phase on **global tile coords**
(`gx,gy`) so they run seamlessly across the whole map.

## Markings · profiles

`markings: ['edge','centerDash','centerDouble','laneDash','crosswalk','stop','curb']`. In the demo the
PROFILE picks what paints itself: **footpath** bare · **sidewalk** curb lip · **lane** edge lines ·
**two-lane** adds the centre line. Crosswalks drop onto straights that meet a junction. Painted
harbour-gold + bone-white — never neon.

## Rig API (`globalThis.RoadKit`)

- `TILE = 32` · `SURFACES` · `WEAR` · `GROUNDS`
- `render(surface, opts)` → `{ data:Uint8ClampedArray, w:32, h:32 }`, where
  `opts = { con:{n,e,s,w}, diag:{ne,nw,se,sw}, wear, ground, markings:[…], axis:'v'|'h'|'x', gx, gy, seed }`
- `renderGround(ground, {gx,gy,seed})` → the verge / field tile (paint non-road cells with this so
  edges match)
- `BLOB47` → `[{ mask, label, con, diag, axis }]` — the canonical 47-tile set
- `canon(mask)` · `fromMask(mask)` → `{con,diag,axis}` · `PAL` → palette object

## Wiring cheat-sheet

1. Build a **0/1 road mask** over your tile grid.
2. Per **road** cell: `con` = which of N/E/S/W neighbours are road, `diag` = which diagonals; call
   `render(surface, {con,diag,wear,ground,markings,gx,gy,seed})`.
3. **Non-road** cells: `renderGround(ground,{gx,gy,seed})` so verges register with the road shoulders.
4. **Markings** follow the profile: `edge` / centre lines on straights; `crosswalk` on straights that
   sit next to a junction.
5. Everything phases on `gx,gy` — keep the same `seed` across a map and the surface stays seamless;
   change `wear` for age.

## Demo page (in the main project, not this zip)

`Road Path Iso.dc.html` — drag on a grid to watch the blob-47 resolve live; surface / wear / verge /
profile chips, layout presets, the 47-tile atlas + painted-map PNG downloads, and the rig source.
