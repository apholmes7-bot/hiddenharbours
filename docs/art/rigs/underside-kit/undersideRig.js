/* UNDERSIDE RIG. What the calm water shows of a deck that stands over it: the timber seen from BELOW,
   laid by the calm mirror where the reflected face of a deck draws no art (the gaps between a pier's
   piles). Floats and their gangways have none: a float's low freeboard leaves a band of about 11 px,
   which its own drawn side fills, and a gangway's band sits on the float it lands on.
   32 px = 1 m, flat pixel tiles, no AA, binary alpha, one texel per art pixel on screen (no mixels).

   ONE SHEET, ONE TEXTURE. Every piece is a cell in this one sheet, so the mirror binds a single
   texture whatever is in view. Cells are stacked top-down in PIECES order; the baker flips rows into
   Unity's bottom-up order and each UndersideDef names its cell in those coordinates.

   HOW A CELL IS READ. Row 0 is the cell's BOTTOM row, the one laid farthest down the water: in a
   mirror the deck's top edge lands farthest from the waterline, so the bottom FASCIA rows are the
   deck's edge beam (its reflected top lip first, a touch lighter), and every row above them is the
   underside receding back under the deck toward the waterline. The fascia rows are laid once; the
   rows above them repeat up the screen with a period of (h - fascia). Columns repeat across x,
   locked to the world's art grid.

   PLAN SCALE. Up the screen the plan's depth is 1:1 (the camera's lift only moves the deck), so a
   stringer every 0.6 m under the deck is a dark band every 19 or 20 rows.

   DARK, BUT WITH CONTRAST THE MIRROR CAN CARRY. This is timber in its own shadow, seen through
   water; the mirror lays it at about half weight (the 0.6 dial times the 0.85 underside share), then
   its 0.82 darken and the day/night tint, all in linear light. That squeezes a sheet step into very
   little on screen: the first ramps (a grey range of 0.12) landed within 1 to 3 steps of 255 of each
   other over noon water, one flat slab. So the joists, caps and stringers stay near black and the
   plank field between them is lifted to a grey of about 0.35, which lands about 12 steps of 255 above
   a joist on screen at noon and still well under the open water beside it (grey about 0.58), so the
   whole band still reads as shade.

   Exposes globalThis.Underside = { PX_PER_M, W, H, PIECES, sheet() }. sheet() returns RGBA, W x H,
   rows top-down. */
(function (root) {
  const S = 32;

  // id, cell size, fascia rows. Cells stack top-down in this order; x is always 0.
  const PIECES = [
    { id: 'underside.stp_pier', w: 96, h: 26, fascia: 6 },
    { id: 'underside.nmc_pier', w: 80, h: 21, fascia: 5 },
  ];
  const W = 96;
  let H = 0;
  PIECES.forEach((p) => { p.x = 0; p.y = H; H += p.h; });

  // Integer hash: the grain is the same on every host.
  function hash(x, y, k) {
    let h = (x * 374761393 + y * 668265263 + k * 2147483647) | 0;
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
  }
  const rgb = (hex) => [parseInt(hex.slice(1, 3), 16), parseInt(hex.slice(3, 5), 16), parseInt(hex.slice(5, 7), 16)];

  // Each painter answers a ramp index (0 darkest) for cell column x and cell row r (0 = the bottom).
  // Ramps are five steps of the piece's timber in shadow: 0 and 1 the beams and gaps, 2 the
  // plank field, 3 and 4 the lip and the slivers of daylight.
  const RAMPS = {
    stp: ['#0b0907', '#2a231c', '#64543f', '#75634d', '#86735b'],
    nmc: ['#0a0908', '#28241f', '#5f564b', '#6f6559', '#7f7468'],
  };

  // A timber pier seen from below: plank seams every 6 px across, a stringer band every period rows,
  // a pile bent (cap beam) every bent columns, and the edge beam as the fascia.
  function pier(bent, fascia, period) {
    return (x, r) => {
      if (r < fascia) {
        // The edge beam: its reflected top lip first, a bolt head at each bent.
        if (r === 0) return 3;
        if (x % bent >= bent / 2 - 1 && x % bent <= bent / 2 && r === Math.floor(fascia / 2)) return 0;
        if (r === fascia - 1) return 1;
        return hash(x, r, 1) < 0.2 ? 1 : 2;
      }
      const b = (r - fascia) % period;
      const bx = x % bent;
      if (bx < 6) {
        // The cap beam over a pile bent, running back under the deck: darker, with its own edge.
        if (bx === 0 || bx === 5) return 0;
        return hash(x, r, 2) < 0.25 ? 0 : 1;
      }
      if (b < 3) return b === 1 ? 0 : 1;            // a stringer, its shadowed belly in the middle
      if (x % 6 === 0) return 0;                    // the seam between two planks
      if (x % 6 === 1 && hash(x, r, 3) < 0.08) return 3;   // a sliver of daylight through a seam
      return hash(x, r, 4) < 0.05 ? 1 : 2;           // sparse grain: the field step is wide now
    };
  }

  const PAINT = {
    'underside.stp_pier': { ramp: RAMPS.stp, fn: pier(96, 6, 20) },
    'underside.nmc_pier': { ramp: RAMPS.nmc, fn: pier(80, 5, 16) },
  };

  function sheet() {
    const out = new Uint8ClampedArray(W * H * 4);
    PIECES.forEach((p) => {
      const paint = PAINT[p.id];
      const ramp = paint.ramp.map(rgb);
      for (let row = 0; row < p.h; row++) {
        const r = p.h - 1 - row;              // row from the cell's bottom
        for (let x = 0; x < p.w; x++) {
          const c = ramp[paint.fn(x, r)];
          const j = ((p.y + row) * W + x) * 4;
          out[j] = c[0]; out[j + 1] = c[1]; out[j + 2] = c[2]; out[j + 3] = 255;
        }
      }
    });
    return out;
  }

  root.Underside = { PX_PER_M: S, W, H, PIECES, RAMPS, sheet };
})(typeof globalThis !== 'undefined' ? globalThis : window);
