# Hidden Harbours — Road / Path / Sidewalk Kit v3

Near-plan ground tiles that register with `Grass.png` / the shore flats / the wharf deck — now
**stood up to the family vertical**. Height draws at **24.5 px = 1 m** (cos 40°·32, the same ruler
as every building wall, utility prop and ShoreIso2 cliff band). South edges carry real vertical
faces, kerbs are stone runs, and every face seats on a **baked alpha contact shadow**. Ringless
(ADR 0031) · one shared key · harbour-master ramps lifted verbatim.

Procedural: **one rig bakes everything** — blob-47 grid tiles AND freeform coverage-painted paths.

## Files

- **roadPathRig3.js** → `globalThis.RoadKit3` — the parametric source of truth. Plain browser script, no deps.
- **RoadIso3_<surface>_new_blob47.png** × 11 — reference atlases (`new` wear, lane profile, grass-soil
  spill, no markings). 12 cols × 4 rows of **32×64 cells**, sorted by neighbour mask. Other wear /
  substrates / markings / profiles bake from the rig.
- **street-scene.png** — the integration proof: v3 street composited with live `wharfBuildingRig`
  + `utilityIsoRig` sprites (sheds, pole line + wire spans, lamp, hydrant, manhole, catch basin),
  plus a freeform-painted desire line.

## Cell geometry — READ THIS FIRST (changed from v1/v2)

- Cell is **32 × 64 px**. The ground square (z = 0) is **rows 10–41**; rows 0–9 are headroom for
  raised decks (boardwalk piles); rows 42–63 (the **SKIRT**) take drop faces + shadows.
- Blit each cell at **(tileX·32, tileY·32 − 10)**.
- **Composite in two passes, painter N→S:** pass 1 blits rows 0–41 of every cell; pass 2 blits every
  cell's SKIRT (rows 42–63) in the same order, so faces stay in front of the flat top of the cell
  below. **Shadow pixels carry real alpha — draw with normal blending.**
- Constants exported: `TILE 32 · CELL_H 64 · HEAD 10 · SKIRT 42 · S 32 · ZS 24.5`.

## Surfaces · wear · profiles · pieces

- **11 surfaces**: `dirt · oiledDirt · gravel · shell · sand · concrete · apron · asphalt · cobble · brick · boardwalk`
- **3 wear states**: `new → worn → cracked` (cracks and potholes are grooves in the height field;
  paint wears off with the surface)
- **Profiles**: `path` flush · `sidewalk` +125 mm on a kerbstone run (4 px face) · `lane` edge-lined ·
  `road2` centre-lined. Roads carry a subtle 50 mm camber.
- **Pieces**: `road · apron · slipway · driveway · shoulder`. The quay apron drops a **16 px face**
  by default (0.65 m) — the exact face height wharfKit's quay cap bakes, so they butt cleanly.

## Rig API (`globalThis.RoadKit3`)

- `render(surface, opts)` → `{ data:Uint8ClampedArray(32·64·4), w:32, h:64 }`
  `opts = { con:{n,e,s,w}, diag:{ne,nw,se,sw}, axis:'v'|'h'|'x', wear, profile, piece, markings:[…],
  drop, kerb, gx, gy, seed, cross:{l,r}, abut:{n,e,s,w}, abutZ:{n,e,s,w}, ground }`
  - **abut** marks sides where a *different paved surface* continues (sidewalk↔road): that edge gets
    the shared kerb face dropping to `abutZ[side]` metres — no verge, no nibble, no soil gutter.
  - **ground** (`grass · dirt · sand · shingle · deck · none`) picks the spill material so soil
    never lands on clean shingle or a wharf deck.
- `renderFree(surface, opts)` — **the not-a-perfect-tile path**: `opts.cov` is a coverage sampler
  `(u,v in global metres) → 0..1` (paint it with any round brush). Same shading, kerbs, faces,
  spill; edge lines follow the curve. Uncached — repaint only dirty cells.
- `renderGround(mat, {gx,gy,seed})` — preview ground only; the game composites ShoreIso2 / the grass tileset.
- `gameplay(surface, opts)` — walk/drive/kerb-blocker sidecar in metres, at the v3 heights.
- `BLOB47 · canon(mask) · fromMask(mask) · topPoly · PAL / MATS`.

## Wiring cheat-sheet

1. Build the road/footway mask. Per paved cell: `con` = same-surface neighbours, `abut` = other-paved
   neighbours (+ their top heights in `abutZ`), `diag` for fillets; pass the map's `ground`.
2. **Pattern space is world-oriented**: pass `gy` increasing NORTHWARD (screen-down rows pass
   `gy = mapHeight−1−row`). Same `seed` map-wide; patterns, dashes and edge wobble stay seamless.
3. Composite with the two-pass skirt rule above. Alpha-blend — the contact shadows depend on it.
4. Markings: `edge`/centre on straights (`cross:{l,r}` = contiguous road cells either side, so a
   3-wide carriageway lines its outer cells only); `crosswalk` on straights beside a junction.
   Junction test: run-ratio ambiguous OR `min(runV,runH) ≥ 4`.
5. Freeform paths: keep a coverage field (≈0.125 m samples), paint with a soft round brush, call
   `renderFree` per touched cell (+1 cell dilation for spill).

## Changelog v2 → v3

- Vertical scale 12 → **24.5 px/m** (the turntable's); wharfKit-style face language (S faces, N rim).
- Baked alpha **contact shadows** under every face (ShoreIso2 v8 occlusion canon).
- **abut** edges: road↔sidewalk share a kerb face — v2 grew a soil gutter between them.
- Free edges wander on seam-coherent clump noise; **renderFree** paints grid-free paths.
- Spill follows the substrate; uv page no longer mirrors per row (v2 broke plank/brick/dash phase
  at every N–S seam); tee junctions now detected (v2's run-ratio never fired on short stubs).
- New surfaces vs v1 kit: oiled dirt, clam shell, wharf apron, boardwalk (0.35 m deck on piles).

## Demo page (in the main project, not this zip)

`Road Path Iso v3.dc.html` — paint grid roads + freeform paths live, the integration scene with the
building/services rigs, per-surface strips, atlas + painted-map PNG downloads, and the review that
drove the rebuild (three open debts flagged for the neighbour rigs).
