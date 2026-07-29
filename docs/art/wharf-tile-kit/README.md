# Hidden Harbours — Wharf / Dock Tile Kit

The working-waterfront deck: **square 32×32 near-plan** tiles that sit in the ground plane like
`Grass.png` / `WharfDeck.png`, plus the mooring hardware and the shore-armour arms. The camera looks
from the **south**, so S-facing (and SE/SW-diagonal) edges drop a tall vertical face over the water,
while N/E/W open edges get a raised curb only. **32 px = 1 m**, KTC pixel conventions — no AA,
upper-left key light, quantised ramps, hash-value noise — shared with the dory / lighthouse /
wharf-building rigs.

8-direction runs come from the **45° diagonal** deck pieces, not a diamond grid (the demo's turn 1
chose a square grid over true iso).

## Files

Deck tiles pin by their **top-left cell origin** (deck fills rows 0–31; the face/foam overhangs rows
32–55 downward, over the tile/water below). Overlays pin by the pivots in their sidecar.

- **WharfAtlas.png** (544×392) **+ .json** — deck tiles, cell **32×56** (32 deck + 24 face/foam).
  Rows: `quay` concrete · `lowpier` ~1 m landing stage · `tallpier` ~1.6 m pier with piles +
  under-deck shadow + reflections · `float` (grey plank + yellow safety curb), whose **four rows are
  the bob frames f0..f3**. Columns are the standard 17-piece auto-tile set:
  `ctr · edN edE edS edW · coNE coSE coSW coNW · capN capS capE capW · diNE diSE diSW diNW`
  (the four `di*` are the 45° diagonal edges).
- **WharfOverlays.png** (520×41) **+ .json** — 14 pivoted sprites, drawn **after** the deck tile: wood
  and galvanised-pipe railings (h / v / 45°), horn cleat, cast bollard, recessed ring, timber dolphin,
  access ladder, tyre fender, pile head, aluminium gangway. Each carries a pivot (edge-line for rails,
  base for cleats / bollards / dolphin, centre for the ring, top for ladder / tyre / gangway).
- **WharfBreakwaters.png** (144×240) **+ .json** — 12 armour pieces, cell **48×60**: types
  `riprap · crib · wall · sheet` × variants `straight · diag (45°) · end`, so arms turn on the diagonal
  grid.
- **wharfKitRig.js** → `globalThis.WharfKit` — the parametric source that **re-bakes the deck tiles**
  (the four materials, any edge/diagonal combo, any bob frame). Overlays and breakwaters ship as baked
  PNG/JSON.
- **_preview-hero.png** — reference only: an assembled harbour (quay, tall pier, floating dock with
  gangway, breakwater arm) with a moored dory for scale.

## Rig API (`globalThis.WharfKit`) — deck tiles

Plain browser script, no deps.

- `TILE = 32` · `CELL_H = 56` · `MATERIALS = ['float','lowpier','tallpier','quay']` · `PAL` · `FACE_H`
- `render(material, opts)` → `{ data:Uint8ClampedArray(32*56*4), w:32, h:56 }`, where
  `opts = { open:{n,e,s,w}, cut:null|'ne'|'se'|'sw'|'nw', inner:null|'ne'|'se'|'sw'|'nw', frame:0..3 }`.
  An `open` side drops to water (curb; S also gets the tall face); `cut` is a 45° diagonal deck edge
  (face on S-ish cuts); `frame` is the float bob phase (ignored by the fixed materials).

**Atlas variant ↔ rig opts** — how the baked columns map back to the rig:
`edS` = `open:{s:true}` (and `edN/E/W` the matching side) · `coSE` = `open:{s:true,e:true}` (outer
corner, the two named sides open) · `capS` = an end cap with three sides open (deck connects on one
side only) · `diSE` = `cut:'se'` (and so on) · inner/concave corners use `inner:'se'` etc. Each cell's
material + variant is also spelled out in `WharfAtlas.json`.

## Wiring cheat-sheet

1. Build your dock footprint on the tile grid; per **deck** cell derive which of N/E/S/W are open water
   and whether a corner is a 45° `cut`, then `render(material, {open, cut, frame})`.
2. Blit each cell at its tile's **screen origin** (deck top-left); the 24 px face/foam draws down over
   the cell below — so paint back-to-front (north rows first).
3. **Floats** animate: cycle `frame` 0→1→2→3 at ~6 fps for the ±1 px heave (offsets 0, −1, 0, +1).
4. **Overlays** go on after the deck, anchored by their sidecar pivot — rails on open edges, cleats /
   bollards / rings on the deck, ladders / tyres / gangways hanging off a face, dolphins in the water.
5. **Breakwaters** run along the arm: `straight` for the E–W run, `diag` for a 45° turn, `end` to cap.

## Known limits (v1)

- Faces render on the **south + SE/SW diagonal** only (single-camera convention) — a thin finger pier
  reads best running N–S.
- Inner (concave) corners reuse the straight-edge curb.
- The rig re-bakes the **deck tiles**; overlays and breakwaters are baked sheets (edit them via their
  PNGs). Say the word for side-face pier pieces, concave corners, or a parametric overlay / breakwater
  rig.

## Demo page (in the main project, not this zip)

`Wharf Kit.dc.html` — the full handoff: the assembled-harbour hero, the tile atlas, the live float-bob
loop, the palette, the overlay + breakwater sheets, and the projection options from turn 1.
