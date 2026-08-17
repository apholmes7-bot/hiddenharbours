/* Hidden Harbours — DIALOGUE BUBBLE KIT. The talking UI, as objects in the scene.

   THE MOVE (owner, 2026-08-17): the bubble comes from the CHARACTER. No portraits, no panel over
   the speaker, no HUD chrome. The world stays visible behind the exchange, so every piece here is
   drawn at the world's own grid — 32 px = 1 m, the assets grid every rig in this repo bakes at
   (fleet, shore, trees, calendar). A bubble pixel is a prop pixel; that is what makes it an object
   and not an overlay. (The other grid, 24 px/m, is the camera's; nothing is authored there.)

   PASS 2 (owner ruling, 2026-08-17): MIXED CASE. Pass 1 printed speech in the town's 3x5 caps.
   Ruled against: both references (Animal Crossing, Eastward) set speech in mixed case, and that is
   most of why their dialogue reads as a voice instead of as signage. Caps stay where they belong —
   the name chip and the option rows, which are LABELS. The face below is therefore a new cut:
   4 x 6 caps, 4-row x-height, 2 descender rows, one 5 x 10 cell. Everything derived from the cell
   (panel ladder, option row height, chip height, caret) moved with it, ONCE, before the freeze.

   SEVEN PIECES, one visual language:
     1. panel()    9-slice speech bubble — warm cream paper, 1 px ink ring, chamfer 2, MEASURED insets
     2. tail()     six anchored tails: down (bubble above the speaker) and UP (bubble below, for a
                   speaker near the top of the screen). The tail TIP is a measured pixel; the
                   presenter aims that pixel at the speaker's own anchor.
     3. chip()     the name tag riding the bubble's edge — top-left for down tails, bottom-left for up
     4. options()  the SECOND family — 2-4 selectable rows, grows by one row height
     5. cursor()   the selection cursor. Pointing hand ships; three alternates are drawn for the owner
     6. marker()   the continue marker at the lower edge, 2-frame idle bob
     7. caret()    the fill caret — one text cell wide, because the SOUND IS THE BUBBLE POPULATING
                   and the fill cadence is the characterisation channel

   THE INVARIANT, not the pixel count: NEITHER BUBBLE EVER OCCLUDES THE SPEAKER'S SPRITE RECT. The
   whole point of no-portraits is that the character IS the portrait. speakerClear (20 px) is one
   implementation of that rule; screenBudget() is the arithmetic that proves it at a zoom tier.

   LIGHT: top-of-frame key (art bible §1) — the inner light row is at the TOP of the panel, the
   shade row at the BOTTOM. No upper-left bias anywhere, no keyline pass beyond the panel's own
   ink ring, which is the object's edge and not an outline (ADR 0031: form from shading + edge).
   ONE LIT STATE: the day/night grade is an in-engine layer (bible §6), so nothing here bakes twice.

   COLOUR: no new colours (ADR 0015). Every hex below is lifted from a shipped rig and says where
   from, per value, in contract().

   Exposes globalThis.BubbleKit. No dependencies. Runs in the browser and in the bake sandbox. */
(function (root) {
  'use strict';

  const PPU = 32;

  /* ============================ the type ============================
     ONE CELL = 5 x 10 px. Glyph box 4 wide; cap 6 rows; x-height 4 rows; 2 descender rows; 2 rows
     of leading. Monospace by construction — the fill advances exactly one cell per character, so a
     caret can own a cell and a per-character reveal never reflows a line.

     PROVENANCE: newly drawn for this kit (2026-08-17). Nothing borrowed, no licence to carry. The
     wall calendar's 3x5 caps stay where they are — that face is print, this one is speech.

     Glyphs are stored as { t, g }: `t` is the top row of the glyph inside the 8-row box (0 = cap
     top, 2 = x-height top, and descenders simply run past row 5, the baseline row). Plain arrays
     mean t = 0. */
  const F = (t, g) => ({ t, g });
  const FONT = {
    'A': F(0, ['.##.', '#..#', '#..#', '####', '#..#', '#..#']),
    'B': F(0, ['###.', '#..#', '###.', '#..#', '#..#', '###.']),
    'C': F(0, ['.###', '#...', '#...', '#...', '#...', '.###']),
    'D': F(0, ['###.', '#..#', '#..#', '#..#', '#..#', '###.']),
    'E': F(0, ['####', '#...', '###.', '#...', '#...', '####']),
    'F': F(0, ['####', '#...', '###.', '#...', '#...', '#...']),
    'G': F(0, ['.###', '#...', '#...', '#.##', '#..#', '.###']),
    'H': F(0, ['#..#', '#..#', '####', '#..#', '#..#', '#..#']),
    'I': F(0, ['###.', '.#..', '.#..', '.#..', '.#..', '###.']),
    'J': F(0, ['.###', '...#', '...#', '...#', '#..#', '.##.']),
    'K': F(0, ['#..#', '#.#.', '##..', '##..', '#.#.', '#..#']),
    'L': F(0, ['#...', '#...', '#...', '#...', '#...', '####']),
    'M': F(0, ['#..#', '####', '####', '#..#', '#..#', '#..#']),
    'N': F(0, ['#..#', '##.#', '#.##', '#..#', '#..#', '#..#']),
    'O': F(0, ['.##.', '#..#', '#..#', '#..#', '#..#', '.##.']),
    'P': F(0, ['###.', '#..#', '#..#', '###.', '#...', '#...']),
    'Q': F(0, ['.##.', '#..#', '#..#', '#..#', '#.#.', '.#.#']),
    'R': F(0, ['###.', '#..#', '#..#', '###.', '#.#.', '#..#']),
    'S': F(0, ['.###', '#...', '.##.', '...#', '...#', '###.']),
    'T': F(0, ['####', '.#..', '.#..', '.#..', '.#..', '.#..']),
    'U': F(0, ['#..#', '#..#', '#..#', '#..#', '#..#', '.##.']),
    'V': F(0, ['#..#', '#..#', '#..#', '#..#', '.##.', '.##.']),
    'W': F(0, ['#..#', '#..#', '#..#', '####', '####', '#..#']),
    'X': F(0, ['#..#', '#..#', '.##.', '.##.', '#..#', '#..#']),
    'Y': F(0, ['#..#', '#..#', '.##.', '.#..', '.#..', '.#..']),
    'Z': F(0, ['####', '...#', '..#.', '.#..', '#...', '####']),

    'a': F(2, ['.##.', '#..#', '#..#', '.###']),
    'b': F(0, ['#...', '#...', '###.', '#..#', '#..#', '###.']),
    'c': F(2, ['.###', '#...', '#...', '.###']),
    'd': F(0, ['...#', '...#', '.###', '#..#', '#..#', '.###']),
    'e': F(2, ['.##.', '####', '#...', '.###']),
    'f': F(0, ['..##', '.#..', '###.', '.#..', '.#..', '.#..']),
    'g': F(2, ['.###', '#..#', '#..#', '.###', '...#', '###.']),
    'h': F(0, ['#...', '#...', '###.', '#..#', '#..#', '#..#']),
    'i': F(0, ['.#..', '....', '.#..', '.#..', '.#..', '.#..']),
    'j': F(0, ['..#.', '....', '..#.', '..#.', '..#.', '..#.', '..#.', '##..']),
    'k': F(0, ['#...', '#...', '#.#.', '##..', '#.#.', '#..#']),
    'l': F(0, ['.#..', '.#..', '.#..', '.#..', '.#..', '.##.']),
    'm': F(2, ['####', '#.##', '#.##', '#.##']),
    'n': F(2, ['###.', '#..#', '#..#', '#..#']),
    'o': F(2, ['.##.', '#..#', '#..#', '.##.']),
    'p': F(2, ['###.', '#..#', '#..#', '###.', '#...', '#...']),
    'q': F(2, ['.###', '#..#', '#..#', '.###', '...#', '...#']),
    'r': F(2, ['#.##', '##..', '#...', '#...']),
    's': F(2, ['.###', '##..', '..##', '###.']),
    't': F(0, ['.#..', '.#..', '###.', '.#..', '.#..', '.###']),
    'u': F(2, ['#..#', '#..#', '#..#', '.###']),
    'v': F(2, ['#..#', '#..#', '.##.', '.##.']),
    'w': F(2, ['#..#', '#..#', '####', '.##.']),
    'x': F(2, ['#..#', '.##.', '.##.', '#..#']),
    'y': F(2, ['#..#', '#..#', '.###', '...#', '###.']),
    'z': F(2, ['####', '..#.', '.#..', '####']),

    '0': F(0, ['.##.', '#..#', '#.##', '##.#', '#..#', '.##.']),
    '1': F(0, ['.#..', '##..', '.#..', '.#..', '.#..', '###.']),
    '2': F(0, ['###.', '...#', '..#.', '.#..', '#...', '####']),
    '3': F(0, ['###.', '...#', '.##.', '...#', '...#', '###.']),
    '4': F(0, ['#..#', '#..#', '#..#', '####', '...#', '...#']),
    '5': F(0, ['####', '#...', '###.', '...#', '...#', '###.']),
    '6': F(0, ['.###', '#...', '###.', '#..#', '#..#', '.##.']),
    '7': F(0, ['####', '...#', '..#.', '.#..', '.#..', '.#..']),
    '8': F(0, ['.##.', '#..#', '.##.', '#..#', '#..#', '.##.']),
    '9': F(0, ['.##.', '#..#', '#..#', '.###', '...#', '##..']),

    '.': F(5, ['.#..']),
    ',': F(5, ['.#..', '#...']),
    ':': F(2, ['.#..', '....', '....', '.#..']),
    ';': F(2, ['.#..', '....', '....', '.#..', '#...']),
    '!': F(0, ['.#..', '.#..', '.#..', '.#..', '....', '.#..']),
    '?': F(0, ['###.', '...#', '..#.', '.#..', '....', '.#..']),
    "'": F(0, ['.#..', '.#..']),
    '\u2019': F(0, ['.#..', '.#..']),
    '"': F(0, ['#.#.', '#.#.']),
    '-': F(3, ['####']),
    '\u2014': F(3, ['####']),
    '/': F(0, ['...#', '..#.', '..#.', '.#..', '.#..', '#...']),
    '(': F(0, ['..#.', '.#..', '#...', '#...', '.#..', '..#.']),
    ')': F(0, ['.#..', '..#.', '...#', '...#', '..#.', '.#..']),
    '&': F(0, ['.##.', '#..#', '.##.', '#.#.', '#..#', '.###']),
    '+': F(2, ['.#..', '###.', '.#..']),
    '=': F(2, ['####', '....', '####']),
    '%': F(0, ['#..#', '...#', '..#.', '.#..', '#...', '#..#']),
    ' ': F(0, ['....']),
  };
  /* the ONE cell every derived number below comes from */
  const CELL = { adv: 5, w: 4, cap: 6, xh: 4, desc: 2, box: 8, line: 10, baseline: 5, leading: 2 };
  const textW = (s) => Math.max(0, String(s).length * CELL.adv - 1);
  const colsFor = (px) => Math.floor((px + 1) / CELL.adv);

  /* y is the TOP of the glyph box (cap top). limit reveals the first n characters — the fill. */
  function text(ctx, str, x, y, col, limit) {
    ctx.fillStyle = col; str = String(str);
    const n = limit == null ? str.length : Math.max(0, Math.min(str.length, limit));
    for (let i = 0; i < n; i++) {
      const G = FONT[str[i]] || FONT['?'], rows = G.g || G, t = G.t || 0;
      for (let r = 0; r < rows.length; r++) for (let c = 0; c < rows[r].length; c++)
        if (rows[r][c] === '#') ctx.fillRect(x + i * CELL.adv + c, y + t + r, 1, 1);
    }
    return x + n * CELL.adv;
  }

  function wrap(str, cols) {
    const words = String(str).split(/\s+/).filter(Boolean), out = [];
    let line = '';
    for (const w of words) {
      if (!line) { line = w; }
      else if (line.length + 1 + w.length <= cols) { line += ' ' + w; }
      else { out.push(line); line = w; }
      while (line.length > cols) { out.push(line.slice(0, cols)); line = line.slice(cols); }
    }
    if (line) out.push(line);
    return out.length ? out : [''];
  }

  /* ============================ stock ============================
     Four stocks. `cream` ships (owner, 2026-08-17); the other three are the taste board kept live
     so the choice stays reviewable against the real scene instead of against a screenshot. */
  const STOCKS = {
    cream: { label: 'WARM CREAM PAPER', ship: true,
      note: 'The Animal Crossing read in harbour colours — the fleet house cream, so the bubble is painted from the same tin as the boats it stands in front of.',
      paper: '#e8ebe5', hi: '#f2f4ee', lo: '#d6dbd7', ink: '#232a32', inkSoft: '#5d6a70', edge: '#171d27',
      prov: 'paper/hi/lo = KTC_HOUS steps 4/5/3 (lobsterBoatVariantsIsoRig.js) · edge = KTC_BOOT[2] · ink = HAIRS.black[2] (headIsoRig3.js)' },
    newsprint: { label: 'GREY NEWSPRINT', ship: false,
      note: "The wall calendar's cheap stock. Right if speech should read as printed matter; wrong if the bubble should feel warmer than the town's paperwork.",
      paper: '#cdc8b9', hi: '#dcd7c8', lo: '#b8b2a1', ink: '#2c2b29', inkSoft: '#6a675f', edge: '#2c2b29',
      prov: 'calendarRig.js DAY.paper / paperHi / paperLo / ink / inkSoft' },
    sailcloth: { label: 'SAILCLOTH', ship: false,
      note: 'Bone-white canvas, cooler and flatter than cream. Reads as harbour kit rather than as paper — the owner\u2019s pick as the strongest alternate if cream ever feels too AC.',
      paper: '#e9e6df', hi: '#f2f4ee', lo: '#c9c4b8', ink: '#231d14', inkSoft: '#5c5140', edge: '#231d14',
      prov: 'paper = ShoreFinds BONE · hi = KTC_HOUS[5] · lo/inkSoft = HATCOLS.oat[3]/[1] · ink/edge = ShoreFinds KEYLINE' },
    enamel: { label: 'ENAMEL BOARD', ship: false,
      note: 'Pale enamel over a hard dark edge — the boats\u2019 house sides. The most object-like, the least paper-like; the ink ring goes near-black.',
      paper: '#d6dbd7', hi: '#e8ebe5', lo: '#a2aaae', ink: '#10141b', inkSoft: '#3d454e', edge: '#0a0d12',
      prov: 'paper/hi/lo = KTC_HOUS steps 3/4/1 · ink/edge = KTC_BOOT[1]/[0] · inkSoft = BOOT[3] (headIsoRig3.js)' },
  };
  const STOCK_ORDER = ['cream', 'newsprint', 'sailcloth', 'enamel'];

  const GOLD = ['#7a5a1c', '#a8842a', '#e0b13a', '#f2cf6a'];   // consoleIsoRig GOLD — fleet cove pinstripe
  const SKIN = ['#a8724a', '#c98d63', '#e0a981', '#f0c9a2'];   // headIsoRig3 SKINS.fair 2..5
  const WOOL = ['#5c5140', '#948468', '#b09e80', '#c9b899'];   // headIsoRig3 HATCOLS.oat
  const STEEL = ['#3a4148', '#7a858c', '#c3ced2', '#e6edee'];  // doryMotorRig STEEL

  /* ============================ metrics ============================
     Every number is a px count at 32 px = 1 m, and every one of them is in contract() with the
     reason. Nothing here is a ratio: a 1 px inset is a 1 px inset at every bubble size. Everything
     type-derived is written as its formula against CELL, so the cell can never drift from the art. */
  const M = {
    ppu: PPU,
    cell: [CELL.adv, CELL.line],
    panel: { border: 1, chamfer: 2, corner: 6, insetL: 5, insetT: 5, insetR: 5, insetB: 5, slice: 18 },
    say: { minCols: 8, maxCols: 34, minLines: 1, maxLines: 4, lineH: CELL.line, glyphBox: CELL.box, capH: CELL.cap },
    tail: { w: 7, gap: 3 },
    chip: { h: CELL.cap + 6, padX: 4, mountX: 4, overlap: 4, capH: CELL.cap },
    opt: { rowH: CELL.box + 5, gutter: 11, insetT: 4, insetB: 4, insetR: 6, minRows: 2, maxRows: 4,
           pillChamfer: 1, speakerClear: 20 },
    marker: { w: 7, h: 4, insetR: 11, dropB: 2, bob: [0, 1], ms: 420 },
    caret: { w: 1, h: CELL.cap, cell: [CELL.adv, CELL.line], ms: 260 },
    cursor: { w: 10, h: 9, pointDX: 9, pointDY: 3 },
  };

  /* ============================ pixel masks ============================
     'k' ink · 'p' paper · 'h' light · 'l' shade · 'g' gold · 'd' dark accent · '.' nothing

     SIX TAILS. The down set anchors a bubble ABOVE the speaker (the default); the up set anchors one
     BELOW, for a speaker near the top of the screen. The up masks are the down masks mirrored in y
     and stored explicitly, because a tail is 2 colours with NO shading — nothing in it fights the
     panel's light, and a flip that the kit performs is one the import does not have to reason about. */
  const TAILS = {
    left: { label: 'BELOW-LEFT', dir: 'down', tip: [1, 6], mask: [
      'kpppppk', 'kppppk.', 'kpppk..', 'kpppk..', '.kppk..', '.kpk...', '.k.....'] },
    centre: { label: 'BELOW-CENTRE', dir: 'down', tip: [3, 5], mask: [
      'kpppppk', '.kpppk.', '.kpppk.', '..kpk..', '..kpk..', '...k...'] },
    right: { label: 'BELOW-RIGHT', dir: 'down', tip: [5, 6], mask: [
      'kpppppk', '.kppppk', '..kpppk', '..kpppk', '..kppk.', '...kpk.', '.....k.'] },
    leftUp: { label: 'ABOVE-LEFT', dir: 'up', tip: [1, 0], mask: [
      '.k.....', '.kpk...', '.kppk..', 'kpppk..', 'kpppk..', 'kppppk.', 'kpppppk'] },
    centreUp: { label: 'ABOVE-CENTRE', dir: 'up', tip: [3, 0], mask: [
      '...k...', '..kpk..', '..kpk..', '.kpppk.', '.kpppk.', 'kpppppk'] },
    rightUp: { label: 'ABOVE-RIGHT', dir: 'up', tip: [5, 0], mask: [
      '.....k.', '...kpk.', '..kppk.', '..kpppk', '..kpppk', '.kppppk', 'kpppppk'] },
  };
  const TAIL_ORDER = ['left', 'centre', 'right', 'leftUp', 'centreUp', 'rightUp'];

  /* The pointing hand, redrawn (owner note: pass 1 read as a hand/pointer hybrid). What fixes it at
     10 px: the FIST is a closed mass with its own outline, and the INDEX FINGER leaves it as two paper
     rows with an ink line above and below and a capped tip — a cylinder, not a spur. The lit row along
     the top of the finger and the shaded palm heel are the top-of-frame key doing the rest. */
  const HAND = [
    '..kk......', '.kppk.....', '.kpppkkkkk', 'kpppppphhk',
    'kpppppkkk.', 'kppppk....', 'kpllpk....', 'kplpk.....', '.kkk......'];
  const CURSORS = {
    hand: { label: 'POINTING HAND', ship: true, w: 10, h: 9, point: [9, 3], mask: HAND, ramp: SKIN,
      note: 'Redrawn 2026-08-17 (pass 1 read as a hand/pointer hybrid): a closed fist with a thumb knuckle, and an index finger that leaves it as a THREE-row form \u2014 ink line, lit paper row, ink line \u2014 so the finger is a cylinder against the fist mass instead of a spur off a blob. Owner\u2019s pick.' },
    glove: { label: 'WOOL WORK GLOVE', ship: false, w: 10, h: 10, point: [9, 3], ramp: WOOL, mask: [
      '..kk......', '.kppk.....', '.kpppkkkkk', 'kpppppphhk',
      'kpppppkkk.', 'kppppk....', 'kppppk....', 'kddddk....', 'kddddk....', '.kkkk.....'],
      note: 'The same hand with a knitted cuff instead of a wrist. The owner\u2019s "most ours" \u2014 worth a live trial; it costs one row and a dark band the row highlight also wants.' },
    tack: { label: 'BRASS TACK', ship: false, w: 6, h: 6, point: [5, 2], ramp: GOLD, mask: [
      '.kkk..', 'khppk.', 'kppppk', 'kpllpk', '.kllk.', '..kk..'],
      note: 'An object pressed into the row instead of a hand — no anatomy to get wrong at this size. Reads as a rivet before it reads as a pointer.' },
    hook: { label: 'FISH HOOK', ship: false, w: 7, h: 10, point: [6, 2], ramp: STEEL, mask: [
      '.kk....', '.kpk...', '.kpkkk.', '.kppppk', '.kpppk.', '.kppk..',
      '.kpk...', 'kppk...', 'kpk....', '.k.....'],
      note: 'Charming at 6x, ambiguous at 1x (owner, 2026-08-17 — check the 1x row on the board before keeping it). It also points with its barb, which is a threat, not an invitation.' },
  };
  const CURSOR_ORDER = ['hand', 'glove', 'tack', 'hook'];

  const MARKER = ['kkkkkkk', '.kpppk.', '..kpk..', '...k...'];

  const HIGHLIGHTS = {
    pill: { label: 'GOLD PILL', ship: true,
      note: 'Cove gold behind the row, dark ink on it. The AC read in fleet colours; the one place a saturated colour is allowed, because selection is the one thing that must never be missed.' },
    invert: { label: 'INVERTED ROW', ship: false,
      note: 'Ink and paper swap. No new colour at all, and it survives any grade — but it reads as a redaction on paper stock.' },
    rule: { label: 'GOLD RULE', ship: false,
      note: 'Paper stays, a gold rule under the row. Lightest touch, leans hardest on the cursor to carry the state.' },
    press: { label: 'PRESSED ROW', ship: false,
      note: 'The row is inset a pixel with its own shade above and light below, so selection is a physical state of the object. Quietest, and the only one that costs no colour.' },
  };
  const HL_ORDER = ['pill', 'invert', 'rule', 'press'];

  function stampMask(ctx, mask, x, y, cols) {
    for (let r = 0; r < mask.length; r++) for (let c = 0; c < mask[r].length; c++) {
      const ch = mask[r][c]; if (ch === '.') continue;
      const col = cols[ch]; if (!col) continue;
      ctx.fillStyle = col; ctx.fillRect(x + c, y + r, 1, 1);
    }
  }

  /* ============================ the 9-slice panel ============================ */
  function chamferOf(j, h, cham) {
    const k = Math.min(j, h - 1 - j);
    return k >= cham ? 0 : cham - k;
  }
  function panel(ctx, x, y, w, h, o) {
    o = o || {};
    const S = STOCKS[o.stock] || (o.stock && o.stock.paper ? o.stock : STOCKS.cream);
    const cham = o.chamfer == null ? M.panel.chamfer : o.chamfer;
    const paper = o.paper || S.paper, hi = o.hi || S.hi, lo = o.lo || S.lo, edge = o.edge || S.ink;
    for (let j = 0; j < h; j++) {
      const inset = chamferOf(j, h, cham), x0 = x + inset, x1 = x + w - 1 - inset;
      const cap = (j === 0 || j === h - 1) || inset >= cham;
      if (cap) { ctx.fillStyle = edge; ctx.fillRect(x0, y + j, x1 - x0 + 1, 1); continue; }
      ctx.fillStyle = edge; ctx.fillRect(x0, y + j, 1, 1); ctx.fillRect(x1, y + j, 1, 1);
      const band = j === 1 ? hi : (j === h - 2 ? lo : paper);
      ctx.fillStyle = band; ctx.fillRect(x0 + 1, y + j, x1 - x0 - 1, 1);
    }
    return { x, y, w, h, ink: edge, paper,
      text: { x: x + M.panel.insetL, y: y + M.panel.insetT, w: w - M.panel.insetL - M.panel.insetR,
              h: h - M.panel.insetT - M.panel.insetB } };
  }
  function slices(w, h) {
    const c = M.panel.corner;
    return { corner: c, cols: [[0, c], [c, w - 2 * c], [w - c, c]], rows: [[0, c], [c, h - 2 * c], [h - c, c]],
      note: 'corners copied, edges repeated 1 px, centre flat paper' };
  }

  /* ============================ tail ============================ */
  function tail(ctx, x, y, kind, o) {
    o = o || {};
    const T = TAILS[kind] || TAILS.centre;
    const S = STOCKS[o.stock] || STOCKS.cream;
    stampMask(ctx, T.mask, x, y, { k: o.edge || S.ink, p: o.paper || S.paper });
    return { x, y, w: M.tail.w, h: T.mask.length, dir: T.dir, tip: { x: x + T.tip[0], y: y + T.tip[1] } };
  }
  function tailX(kind, x, w) {
    const inset = M.panel.corner + 2;
    if (kind === 'left' || kind === 'leftUp') return x + inset;
    if (kind === 'right' || kind === 'rightUp') return x + w - inset - M.tail.w;
    return x + Math.round((w - M.tail.w) / 2);
  }

  /* ============================ name chip ============================ */
  function chip(ctx, x, y, label, o) {
    o = o || {};
    const S = STOCKS[o.stock] || STOCKS.cream;
    const w = M.chip.padX * 2 + textW(label), h = M.chip.h;
    panel(ctx, x, y, w, h, { chamfer: 1, paper: GOLD[2], hi: GOLD[3], lo: GOLD[1], edge: o.edge || S.ink });
    text(ctx, label, x + M.chip.padX, y + 3, S.ink);
    return { x, y, w, h };
  }

  /* ============================ continue marker + caret ============================ */
  function marker(ctx, x, y, frame, o) {
    o = o || {};
    const S = STOCKS[o.stock] || STOCKS.cream;
    const dy = M.marker.bob[(frame | 0) % M.marker.bob.length];
    stampMask(ctx, MARKER, x, y + dy, { k: o.edge || S.ink, p: o.paper || S.paper });
    return { x, y: y + dy, w: M.marker.w, h: M.marker.h, dy };
  }
  function caret(ctx, x, y, o) {
    o = o || {};
    const S = STOCKS[o.stock] || STOCKS.cream;
    ctx.fillStyle = o.ink || S.ink;
    ctx.fillRect(x, y, M.caret.w, M.caret.h);
    return { x, y, w: M.caret.w, h: M.caret.h };
  }

  /* ============================ cursor ============================ */
  function cursor(ctx, x, y, kind, o) {
    o = o || {};
    const C = CURSORS[kind] || CURSORS.hand;
    const S = STOCKS[o.stock] || STOCKS.cream;
    stampMask(ctx, C.mask, x, y, { k: o.edge || S.ink, p: C.ramp[2], h: C.ramp[3], l: C.ramp[1], d: C.ramp[0] });
    return { x, y, w: C.w, h: C.h, point: { x: x + C.point[0], y: y + C.point[1] } };
  }

  /* ============================ the speech bubble ============================ */
  function layoutSay(x, y, o) {
    o = o || {};
    const cols = Math.max(M.say.minCols, Math.min(M.say.maxCols, o.cols || 22));
    const lines = o.lines || wrap(o.text || '', cols);
    const shown = Math.min(lines.length, o.maxLines || M.say.maxLines);
    const w = M.panel.insetL + (cols * CELL.adv - 1) + M.panel.insetR;
    const h = M.panel.insetT + (shown - 1) * CELL.line + CELL.box + M.panel.insetB;
    const kind = TAILS[o.tail] ? o.tail : 'centre';
    const T = TAILS[kind], up = T.dir === 'up';
    const tx = tailX(kind, x, w);
    const ty = up ? y - (T.mask.length - 1) : y + h - 1;
    const rows = [];
    for (let i = 0; i < shown; i++) rows.push({ i, x: x + M.panel.insetL, y: y + M.panel.insetT + i * CELL.line, text: lines[i] });
    /* the chip rides the edge AWAY from the tail, so a tail and a name never fight for a corner */
    const chipY = up ? y + h - M.chip.overlap : y - (M.chip.h - M.chip.overlap);
    return { x, y, w, h, cols, lines, shown, overflow: lines.length - shown,
      chars: lines.reduce((n, l) => n + l.length, 0),
      tailKind: kind, tailDir: T.dir,
      text: { x: x + M.panel.insetL, y: y + M.panel.insetT, w: cols * CELL.adv - 1,
              h: (shown - 1) * CELL.line + CELL.box, cols, rows: shown },
      rows,
      tail: { x: tx, y: ty, w: M.tail.w, h: T.mask.length, dir: T.dir,
              tip: { x: tx + T.tip[0], y: ty + T.tip[1] } },
      chip: o.name ? { x: x + M.chip.mountX, y: chipY, w: M.chip.padX * 2 + textW(o.name), h: M.chip.h,
                       side: up ? 'bottom' : 'top' } : null,
      marker: { x: x + w - M.marker.insetR, y: y + h - M.marker.dropB },
      top: Math.min(y, ty, o.name ? chipY : y),
      bottom: Math.max(y + h - 1, ty + T.mask.length - 1, o.name ? chipY + M.chip.h - 1 : 0) };
  }
  /* place a bubble so its tail TIP lands on `anchor` (the speaker's own anchor pixel). A down tail
     puts the bubble above the anchor, an up tail below it; the gap is measured either way.

     `clear` is the distance from the anchor to the speaker's INK edge in that direction — the head
     anchor is the head CENTRE, so a tail aimed 3 px off the anchor buries itself in the crown. Pass
     clear from the rig's own ink box (the harness measures it) and the tail stops gap px clear of the
     sprite instead of gap px from a point inside it. Default 0 keeps the raw anchor behaviour. */
  function mount(anchor, o) {
    o = o || {};
    const L = layoutSay(0, 0, o);
    const gap = o.gap != null ? o.gap : M.tail.gap;
    const lift = gap + (o.clear || 0);
    const sign = L.tailDir === 'up' ? 1 : -1;
    return { x: Math.round(anchor.x) - L.tail.tip.x,
             y: Math.round(anchor.y) + sign * lift - L.tail.tip.y,
             gap, clear: o.clear || 0, lift,
             layoutW: L.w, layoutH: L.h, tailDir: L.tailDir };
  }
  function say(ctx, x, y, o) {
    o = o || {};
    const S = STOCKS[o.stock] || STOCKS.cream;
    const L = layoutSay(x, y, o);
    panel(ctx, x, y, L.w, L.h, { stock: o.stock });
    tail(ctx, L.tail.x, L.tail.y, L.tailKind, { stock: o.stock });
    const revealed = o.fill == null ? Infinity : (o.fill <= 1 && o.fill > 0 && !Number.isInteger(o.fill)
      ? Math.round(o.fill * L.chars) : Math.round(o.fill));
    let used = 0, head = null;
    for (const r of L.rows) {
      const left = revealed - used;
      if (left > 0) text(ctx, r.text, r.x, r.y, S.ink, left);
      if (head === null && left < r.text.length) head = { x: r.x + Math.max(0, left) * CELL.adv, y: r.y };
      used += r.text.length;
    }
    const done = revealed >= L.chars;
    if (o.caret !== false && !done && head) caret(ctx, head.x, head.y, { stock: o.stock });
    if (o.name) chip(ctx, L.chip.x, L.chip.y, o.name, { stock: o.stock });
    if (o.marker !== false && done) marker(ctx, L.marker.x, L.marker.y, o.frame || 0, { stock: o.stock });
    return Object.assign(L, { revealed: Math.min(revealed, L.chars), done, caretAt: head });
  }

  /* ============================ the option bubble ============================ */
  function layoutOptions(x, y, o) {
    o = o || {};
    const rows = (o.rows || []).slice(0, M.opt.maxRows);
    const cols = o.cols || Math.max(6, ...rows.map(r => String(r).length));
    const wid = M.panel.insetL + M.opt.gutter + (cols * CELL.adv - 1) + M.opt.insetR;
    const h = M.opt.insetT + rows.length * M.opt.rowH + M.opt.insetB;
    const out = rows.map((label, i) => {
      const ry = y + M.opt.insetT + i * M.opt.rowH;
      return { i, label, y: ry, h: M.opt.rowH,
        pill: { x: x + M.panel.insetL - 1, y: ry, w: wid - 2 * (M.panel.insetL - 1), h: M.opt.rowH },
        text: { x: x + M.panel.insetL + M.opt.gutter, y: ry + Math.round((M.opt.rowH - CELL.cap) / 2) },
        cursor: { x: x + 2, y: ry + Math.round((M.opt.rowH - M.cursor.h) / 2) } };
    });
    return { x, y, w: wid, h, cols, rows: out, count: rows.length, rowH: M.opt.rowH,
      grow: 'one row = +' + M.opt.rowH + ' px of height, width unchanged' };
  }
  function options(ctx, x, y, o) {
    o = o || {};
    const S = STOCKS[o.stock] || STOCKS.cream;
    const L = layoutOptions(x, y, o);
    const sel = Math.max(0, Math.min(L.count - 1, o.sel == null ? 0 : o.sel));
    const hl = HIGHLIGHTS[o.highlight] ? o.highlight : 'pill';
    panel(ctx, x, y, L.w, L.h, { stock: o.stock });
    for (const r of L.rows) {
      const on = r.i === sel;
      let ink = S.ink;
      if (on && hl === 'pill') {
        panel(ctx, r.pill.x, r.pill.y, r.pill.w, r.pill.h, { chamfer: M.opt.pillChamfer, paper: GOLD[2], hi: GOLD[3], lo: GOLD[1], edge: GOLD[0] });
      } else if (on && hl === 'invert') {
        panel(ctx, r.pill.x, r.pill.y, r.pill.w, r.pill.h, { chamfer: M.opt.pillChamfer, paper: S.ink, hi: S.inkSoft, lo: S.ink, edge: S.ink });
        ink = S.paper;
      } else if (on && hl === 'rule') {
        ctx.fillStyle = GOLD[2]; ctx.fillRect(r.pill.x + 1, r.pill.y + r.pill.h - 2, r.pill.w - 2, 1);
      } else if (on && hl === 'press') {
        ctx.fillStyle = S.lo; ctx.fillRect(r.pill.x + 1, r.pill.y + 1, r.pill.w - 2, 1);
        ctx.fillStyle = S.hi; ctx.fillRect(r.pill.x + 1, r.pill.y + r.pill.h - 2, r.pill.w - 2, 1);
      }
      const push = (on && hl === 'press') ? 1 : 0;
      text(ctx, r.label, r.text.x + push, r.text.y + push, ink);
      if (on) cursor(ctx, r.cursor.x, r.cursor.y + push, o.cursor || 'hand', { stock: o.stock });
    }
    return Object.assign(L, { sel, highlight: hl });
  }
  /* THE INVARIANT: neither bubble occludes the speaker's sprite rect. The list therefore hangs off
     the speech bubble on the side AWAY from the tail, keeping speakerClear from the tail column.
     The away side is a PREFERENCE, not a guarantee — pass bounds {x0,x1} and the mount takes the
     side that fits, reporting which. clamped:true means neither side fits: shorten the rows or move
     the camera, do not nudge pixels. */
  function optionsMount(sayLayout, o) {
    o = o || {};
    const L = layoutOptions(0, 0, o);
    const clear = o.speakerClear != null ? o.speakerClear : M.opt.speakerClear;
    /* the list hangs off the bubble's LOWER edge in both directions: with a down tail that is away
       from nothing vertically (the horizontal clear does the work), and with an up tail the speaker
       is above the bubble, so lower is further from her. One rule, two directions. */
    const y = sayLayout.bottom + 4;
    const B = o.bounds || null;
    const xr = Math.max(sayLayout.tail.x + clear, sayLayout.x + sayLayout.w + 4 - L.w);
    const xl = Math.min(sayLayout.tail.x - clear - L.w, sayLayout.x - 4);
    const fits = (x) => !B || (x >= B.x0 && x + L.w <= B.x1);
    const away = (sayLayout.tailKind === 'right' || sayLayout.tailKind === 'rightUp') ? 'left' : 'right';
    let side = away, x = away === 'left' ? xl : xr;
    if (!fits(x)) {
      const alt = away === 'left' ? xr : xl;
      if (fits(alt)) { side = away === 'left' ? 'right' : 'left'; x = alt; }
    }
    let clamped = false;
    if (B && !fits(x)) { x = Math.max(B.x0, Math.min(B.x1 - L.w, x)); clamped = true; }
    /* VERTICAL fallback, same shape as the horizontal one: bounds may carry y0/y1, and if the list
       runs off that edge it takes the other side of the bubble before anything is clamped. A caller
       that clamps a returned y itself gets a lie in `clamped`, so the bound belongs here. */
    let vSide = 'below', yy = y;
    const fitsY = (v) => !B || B.y0 == null || (v >= B.y0 && v + L.h <= B.y1);
    if (!fitsY(yy)) {
      const alt = sayLayout.top - 4 - L.h;
      if (fitsY(alt)) { vSide = 'above'; yy = alt; }
      else { yy = Math.max(B.y0, Math.min(B.y1 - L.h, yy)); clamped = true; }
    }
    const clearFromTail = side === 'left' ? sayLayout.tail.x - (x + L.w) : x - sayLayout.tail.x;
    return { x, y: yy, w: L.w, h: L.h, side, away, vSide, clamped, clearFromTail };
  }

  /* ============================ type metrics, as a table ============================
     The gameplay fill needs the line arithmetic, not the crop boxes: cell, line height, text inset
     box and chars per line at every panel size. Generated, so it cannot drift from the art. */
  function metricsTable(o) {
    o = o || {};
    const ladder = [];
    for (const cols of (o.cols || [8, 14, 18, 22, 26, 34])) {
      for (const lines of (o.lines || [1, 2, 3, 4])) {
        const L = layoutSay(0, 0, { lines: Array(lines).fill('X'.repeat(cols)), cols, tail: 'centre' });
        ladder.push({ cols, lines, panel: [L.w, L.h], metres: [+(L.w / PPU).toFixed(2), +(L.h / PPU).toFixed(2)],
          textBox: [L.text.w, L.text.h], charsPerLine: cols, cells: cols * lines,
          baselines: L.rows.map(r => r.y - L.y + CELL.baseline) });
      }
    }
    const opts = [];
    for (const rows of [2, 3, 4]) {
      const L = layoutOptions(0, 0, { rows: Array(rows).fill('X'.repeat(o.optCols || 18)), cols: o.optCols || 18 });
      opts.push({ rows, px: [L.w, L.h], rowH: L.rowH, textX: L.rows[0].text.x - L.x, textY: L.rows[0].text.y - L.y });
    }
    return {
      cell: { w: CELL.adv, h: CELL.line, glyph: [CELL.w, CELL.box], advance: CELL.adv,
        capHeight: CELL.cap, xHeight: CELL.xh, ascender: CELL.cap, descender: CELL.desc,
        baselineRow: CELL.baseline, leading: CELL.leading, lineHeight: CELL.line },
      wrap: 'charsPerLine = cols = floor((textBoxWidth + 1) / ' + CELL.adv + ')  ·  BubbleKit.colsFor(px)',
      fill: 'one cell per character, left to right, line by line; layout.rows[] gives each line its cell origin, and layout.chars the total the cadence divides',
      panelFormula: { w: 'insetL + (cols × ' + CELL.adv + ' − 1) + insetR', h: 'insetT + (lines − 1) × ' + CELL.line + ' + ' + CELL.box + ' + insetB' },
      ladder, options: opts,
      optionFormula: { w: 'insetL + gutter + (cols × ' + CELL.adv + ' − 1) + insetR', h: 'insetT + rows × ' + M.opt.rowH + ' + insetB',
        step: '+' + M.opt.rowH + ' px per row (' + opts.map(o2 => o2.px[1]).join(' / ') + ' px for 2 / 3 / 4 rows)' },
      chip: { h: M.chip.h, formula: 'padX × 2 + (chars × ' + CELL.adv + ' − 1)', textY: 3, case: 'CAPS — a label, not voice' },
      caret: { w: M.caret.w, h: M.caret.h, cell: M.caret.cell },
      case: { body: 'sentence case (owner ruling, 2026-08-17)', labels: 'name chip and option rows in CAPS' },
    };
  }

  /* ============================ the invariant, as arithmetic ============================
     THE RULE: the speech panel, the name chip and the option bubble never occlude the speaker's
     sprite rect. The TAIL is the exception and the point of the kit — it is the pointer, it reaches
     TOWARDS the speaker, and it stops `gap` px clear of the sprite's ink edge (which is why mount()
     takes `clear`). screenBudget() proves both halves at a zoom tier instead of eyeballing one
     layout: it builds the WORST case (widest panel, most lines, most rows), mounts it the way the
     presenter will, and reports panel occlusion (must be zero) and the tail's real clearance. */
  function screenBudget(o) {
    o = o || {};
    const out = o.out || [1920, 1080];
    const tiers = o.tiers || [2, 3, 4];
    /* speaker rect RELATIVE TO THE HEAD ANCHOR. Defaults are the character rig's own ink box at
       PPU 32 (adult, idle, dir 4); pass measured values from the rig and the answer is measured. */
    const sp = o.speaker || { dx: -8, dy: -7, w: 16, h: 43 };
    /* clear is DIRECTIONAL: the anchor-to-ink distance in the tail's own direction. A down tail
       clears the crown; an up tail hangs the bubble below the speaker's FEET, because below the head
       is her body — that is the whole reason the up set exists and the reason it needs its own
       number. Aim an up tail at the rig's hip/foot anchor, or pass this clear from the head anchor. */
    const clearOf = (dir) => dir === 'up' ? (sp.dy + sp.h) : Math.max(0, -sp.dy);
    const gap = o.gap != null ? o.gap : M.tail.gap;
    const cols = o.cols || M.say.maxCols, lines = o.lines || M.say.maxLines;
    const rows = o.rows || M.opt.maxRows, optCols = o.optCols || 18;
    const anchor = { x: 0, y: 0 };
    const s = { x0: anchor.x + sp.dx, x1: anchor.x + sp.dx + sp.w,
                y0: anchor.y + sp.dy, y1: anchor.y + sp.dy + sp.h };
    const hit = (r) => Math.max(0, Math.min(r.x1, s.x1) - Math.max(r.x0, s.x0)) *
                       Math.max(0, Math.min(r.y1, s.y1) - Math.max(r.y0, s.y0));
    const per = (kind) => {
      const sayO = { lines: Array(lines).fill('X'.repeat(cols)), cols, tail: kind, name: o.name || 'GINNY' };
      const clear = o.clear != null ? o.clear : clearOf(TAILS[kind].dir);
      const mt = mount(anchor, Object.assign({ clear, gap }, sayO));
      const L = layoutSay(mt.x, mt.y, sayO);
      const optO = { rows: Array(rows).fill('X'.repeat(optCols)), cols: optCols };
      const om = optionsMount(L, optO);
      /* the panels: speech body, name chip, option bubble. The tail is judged separately. */
      const panels = [{ x0: L.x, x1: L.x + L.w, y0: L.y, y1: L.y + L.h },
                      { x0: om.x, x1: om.x + om.w, y0: om.y, y1: om.y + om.h }];
      if (L.chip) panels.push({ x0: L.chip.x, x1: L.chip.x + L.chip.w, y0: L.chip.y, y1: L.chip.y + L.chip.h });
      const occl = panels.reduce((n, r) => n + hit(r), 0);
      const box = { x0: Math.min(L.x, om.x, L.chip ? L.chip.x : L.x), x1: Math.max(L.x + L.w, om.x + om.w),
                    y0: Math.min(L.top, om.y), y1: Math.max(L.bottom, om.y + om.h) };
      const tipToInk = L.tailDir === 'up' ? (L.tail.tip.y - s.y1) : (s.y0 - L.tail.tip.y);
      return { tail: kind, listSide: om.side, flipped: om.side !== om.away, clamped: om.clamped,
        clear, union: [box.x1 - box.x0, box.y1 - box.y0], panelOccludesPx: occl,
        tailTipToInk: tipToInk, holds: occl === 0 && tipToInk >= gap };
    };
    const cases = TAIL_ORDER.map(per);
    const worst = cases.reduce((a, b) => (b.union[0] * b.union[1] > a.union[0] * a.union[1] ? b : a), cases[0]);
    return {
      rule: "the speech panel, the name chip and the option bubble never occlude the speaker's sprite rect \u2014 the character IS the portrait. speakerClear (" + M.opt.speakerClear + " px) is one implementation of it, not the rule.",
      tailRule: 'the TAIL is the pointer and the exception: it reaches towards the speaker and stops gap (' + gap + ' px) clear of the sprite ink, measured through mount({clear}) rather than guessed from the anchor. clear is DIRECTIONAL \u2014 a down tail clears the crown, an up tail hangs the bubble below the FEET (below the head is her body), so aim an up tail at the hip/foot anchor or pass the anchor\u2192ink-bottom distance.',
      assumption: 'output ' + out[0] + '\u00d7' + out[1] + ', per-tier pixel-perfect zoom (ruling #327). If the shipped tier ladder differs, pass tiers[] \u2014 the arithmetic does not change.',
      speakerRect: sp, clear: { down: clearOf('down'), up: clearOf('up') }, gap, worstCase: { cols, lines, rows, optCols },
      cases, worst,
      tiers: tiers.map(z => {
        const world = [Math.floor(out[0] / z), Math.floor(out[1] / z)];
        return { zoom: z, worldPx: world,
          unionPct: [+(worst.union[0] / world[0] * 100).toFixed(1), +(worst.union[1] / world[1] * 100).toFixed(1)],
          areaPct: +(worst.union[0] * worst.union[1] / (world[0] * world[1]) * 100).toFixed(1),
          fits: worst.union[0] <= world[0] && worst.union[1] <= world[1],
          closest: z === Math.max.apply(null, tiers) };
      }),
      holds: cases.every(c => c.holds),
    };
  }

  /* ============================ render helpers ============================ */
  function canvasFor(w, h) {
    const c = (typeof document !== 'undefined') ? document.createElement('canvas') : new root.OffscreenCanvas(w, h);
    c.width = w; c.height = h; return c;
  }
  function render(kind, o) {
    o = o || {};
    let L, cv, ctx;
    if (kind === 'say') {
      L = layoutSay(0, 0, o);
      const padTop = L.y - L.top, padBot = L.bottom - (L.y + L.h - 1);
      cv = canvasFor(L.w, L.h + padTop + padBot);
      ctx = cv.getContext('2d'); say(ctx, 0, padTop, o);
      return cv;
    }
    if (kind === 'options') { L = layoutOptions(0, 0, o); cv = canvasFor(L.w, L.h); ctx = cv.getContext('2d'); options(ctx, 0, 0, o); return cv; }
    if (kind === 'slice') { cv = canvasFor(M.panel.slice, M.panel.slice); ctx = cv.getContext('2d'); panel(ctx, 0, 0, M.panel.slice, M.panel.slice, o); return cv; }
    if (kind === 'tail') { const T = TAILS[o.tail] || TAILS.centre; cv = canvasFor(M.tail.w, T.mask.length); ctx = cv.getContext('2d'); tail(ctx, 0, 0, o.tail || 'centre', o); return cv; }
    if (kind === 'chip') { const w = M.chip.padX * 2 + textW(o.name || 'NAME'); cv = canvasFor(w, M.chip.h); ctx = cv.getContext('2d'); chip(ctx, 0, 0, o.name || 'NAME', o); return cv; }
    if (kind === 'cursor') { const C = CURSORS[o.cursor] || CURSORS.hand; cv = canvasFor(C.w, C.h); ctx = cv.getContext('2d'); cursor(ctx, 0, 0, o.cursor || 'hand', o); return cv; }
    if (kind === 'marker') { cv = canvasFor(M.marker.w, M.marker.h + 1); ctx = cv.getContext('2d'); marker(ctx, 0, 0, o.frame || 0, o); return cv; }
    throw new Error('BubbleKit.render: unknown piece ' + kind);
  }

  /* ============================ reference sheet ============================ */
  const SHEET_TEXT = 'The wind backed round at dawn and the punt came home light.';
  function sheetSpec(o) {
    o = o || {};
    const stock = o.stock || 'cream';
    const items = [];
    let x = 4, y = 14, rowY = y, rowH = 0;
    const place = (name, w, h, draw, note) => {
      const label = name.replace(/\./g, ' '), dim = w + 'x' + h;
      const cell = Math.max(w, label.length * CELL.adv - 1, dim.length * CELL.adv - 1);
      if (x + cell > 560) { x = 4; rowY += rowH + 26; rowH = 0; }
      items.push({ name, x, y: rowY, w, h, cell, draw, note });
      x += cell + 10; rowH = Math.max(rowH, h);
    };
    place('panel.slice', M.panel.slice, M.panel.slice, (c, X, Y) => panel(c, X, Y, M.panel.slice, M.panel.slice, { stock }), '9-slice source tile — corner 6, edge 1 px repeat');
    const one = layoutSay(0, 0, { lines: ['X'.repeat(8)], cols: 8, tail: 'centre' });
    place('panel.min', one.w, one.h, (c, X, Y) => panel(c, X, Y, one.w, one.h, { stock }), '8 cols, 1 line');
    const two = layoutSay(0, 0, { lines: ['X'.repeat(22), 'X'.repeat(22)], cols: 22, tail: 'centre' });
    place('panel.wide', two.w, two.h, (c, X, Y) => panel(c, X, Y, two.w, two.h, { stock }), '22 cols, 2 lines');
    for (const k of TAIL_ORDER) place('tail.' + k, M.tail.w, TAILS[k].mask.length, (c, X, Y) => tail(c, X, Y, k, { stock }), TAILS[k].dir + ' · tip ' + TAILS[k].tip.join(','));
    place('chip', M.chip.padX * 2 + textW('GINNY'), M.chip.h, (c, X, Y) => chip(c, X, Y, 'GINNY', { stock }), 'mount x+4, overlap 4');
    place('marker.f0', M.marker.w, M.marker.h, (c, X, Y) => marker(c, X, Y, 0, { stock }), 'bob frame 0');
    place('marker.f1', M.marker.w, M.marker.h + 1, (c, X, Y) => marker(c, X, Y, 1, { stock }), 'bob frame 1 (+1 px)');
    place('caret', M.caret.w, M.caret.h, (c, X, Y) => caret(c, X, Y, { stock }), 'cell ' + M.caret.cell.join('x'));
    for (const k of CURSOR_ORDER) place('cursor.' + k, CURSORS[k].w, CURSORS[k].h, (c, X, Y) => cursor(c, X, Y, k, { stock }), 'point ' + CURSORS[k].point.join(','));
    place('type.caps', textW('ABCDEFGHIJKLM'), CELL.box, (c, X, Y) => text(c, 'ABCDEFGHIJKLM', X, Y, STOCKS[stock].ink), 'cap 6, cell 5x10');
    place('type.lower', textW('abcdefghijklm'), CELL.box, (c, X, Y) => text(c, 'abcdefghijklm', X, Y, STOCKS[stock].ink), 'x-height 4, descender 2');
    place('type.rest', textW('nopqrstuvwxyz'), CELL.box, (c, X, Y) => text(c, 'nopqrstuvwxyz', X, Y, STOCKS[stock].ink), 'newly drawn for this kit');
    place('type.figs', textW('0123456789 .,:;!?'), CELL.box, (c, X, Y) => text(c, '0123456789 .,:;!?', X, Y, STOCKS[stock].ink), 'figures + points');
    for (const n of [2, 3, 4]) {
      const rows = ['ABOUT THE BOAT', 'ABOUT THE WEATHER', 'NOTHING TODAY', 'GOODBYE'].slice(0, n);
      const L = layoutOptions(0, 0, { rows, cols: 18 });
      place('options.' + n, L.w, L.h, (c, X, Y) => options(c, X, Y, { rows, cols: 18, sel: 0, stock, highlight: 'pill' }), n + ' rows · +' + M.opt.rowH + ' px each');
    }
    for (const k of HL_ORDER) {
      const rows = ['SELECTED ROW', 'OTHER ROW'];
      const L = layoutOptions(0, 0, { rows, cols: 14 });
      place('highlight.' + k, L.w, L.h, (c, X, Y) => options(c, X, Y, { rows, cols: 14, sel: 0, stock, highlight: k }), HIGHLIGHTS[k].label);
    }
    for (const k of ['left', 'centreUp']) {
      const sayO = { text: SHEET_TEXT, cols: 22, name: 'Ginny', tail: k, stock, marker: true };
      const SL = layoutSay(0, 0, sayO);
      const padTop = SL.y - SL.top, padBot = SL.bottom - (SL.y + SL.h - 1);
      place('say.' + k, SL.w, SL.h + padTop + padBot, (c, X, Y) => say(c, X, Y + padTop, sayO), 'panel + tail + chip + text + marker');
    }
    return { items, w: 570, h: rowY + rowH + 30, stock };
  }
  function sheet(o) {
    const spec = sheetSpec(o);
    const cv = canvasFor(spec.w, spec.h);
    const ctx = cv.getContext('2d');
    ctx.fillStyle = '#141a1e'; ctx.fillRect(0, 0, spec.w, spec.h);
    text(ctx, 'Bubble kit \u00b7 ' + STOCKS[spec.stock].label.toLowerCase() + ' \u00b7 32 px = 1 m', 4, 3, '#7fd6c9');
    for (const it of spec.items) {
      ctx.fillStyle = '#1d262b'; ctx.fillRect(it.x - 1, it.y - 1, it.w + 2, it.h + 2);
      it.draw(ctx, it.x, it.y);
      text(ctx, it.name.replace(/\./g, ' '), it.x, it.y + it.h + 4, '#55666d');
      text(ctx, it.w + 'x' + it.h, it.x, it.y + it.h + 15, '#3f4d53');
    }
    return cv;
  }

  /* ============================ contract ============================ */
  function contract() {
    const MT = metricsTable();
    return {
      kit: 'dialogue-bubble-kit', version: 2, rig: 'dialogueBubbleRig.js', global: 'BubbleKit',
      generated: 'read out of Art/dialogueBubbleRig.js at its defaults — regenerate, never hand-edit',
      grid: {
        ppu: PPU, unit: '32 px = 1 m',
        why: 'The bubble is an object IN the scene (diegetic doctrine), so it is authored on the same grid as the props it stands among — 32 px/m, the assets grid every shipped rig in this repo bakes at. The 24 px/m grid is the camera-side number (memory two-pixel-grids-ppu-vs-assetsppu); authoring there would put UI pixels on a different pitch from world pixels and the bubble would read as an overlay.',
        crop: 'Every piece is an integer px count at this grid, so a 24-grid import is a straight rescale decision for the wiring PR, not a re-author.',
      },
      light: { key: 'top-of-frame (art bible §1)', built: 'inner light row directly under the top edge, shade row directly above the bottom edge', keyline: 'none beyond the panel ink ring (ADR 0031) — the ring is the object edge, not an outline pass', states: 'ONE lit state; the day/night grade is the in-engine layer (bible §6)' },
      type: Object.assign({
        face: 'newly drawn for this kit, 2026-08-17 — nothing borrowed, no licence to carry',
        lowercase: 'YES — full a–z with a 4-row x-height, ascenders to cap height, and 2 descender rows (g j p q y)',
        caseRuling: 'speech body in sentence case (owner, 2026-08-17: both references set speech mixed, and that is why their dialogue reads as voice not signage). Name chip and option rows stay CAPS — labels, not voice.',
        costSplit: {
          drawingTheLowercaseSet: 'DONE in this pass — 26 lowercase glyphs + repositioned points. No layout cost of its own.',
          tallerLineBox: 'cell 4×8 → 5×10: +1 px advance, +2 px line height. A 22-col 4-line panel goes 97×39 → 119×48 (+23% w, +23% h); the option ladder goes 30/41/52 → ' + MT.options.map(o => o.px[1]).join('/') + ' px (+' + M.opt.rowH + ' per row).',
          whyBoth: 'Reported separately because only the second one moves any other number. It was taken ONCE, before the freeze, so every derived value below is already the mixed-case value.',
        },
      }, MT),
      panel: Object.assign({}, M.panel, {
        textInset: { l: M.panel.insetL, t: M.panel.insetT, r: M.panel.insetR, b: M.panel.insetB },
        textBox: 'x+5, y+5, w-10, h-10 — the rect text may fill, measured, not derived',
        slices: slices(M.panel.slice, M.panel.slice),
        sizeFormula: MT.panelFormula,
        minCell: { cols: M.say.minCols, lines: M.say.minLines },
        maxCell: { cols: M.say.maxCols, lines: M.say.maxLines },
        ladderNote: 'type.ladder is the full cols × lines table with panel px, text box and chars per line',
      }),
      tail: {
        w: M.tail.w,
        set: TAIL_ORDER.map(k => ({ key: k, label: TAILS[k].label, dir: TAILS[k].dir, w: M.tail.w, h: TAILS[k].mask.length, tip: TAILS[k].tip })),
        anchorRule: 'the tip pixel is THE anchor — the presenter aims it at the speaker anchor. tail.tip is given relative to the tail sprite origin; layoutSay() returns it in bubble space.',
        placement: { left: 'x + 8', centre: 'x + round((w-7)/2)', right: 'x + w - 15' },
        upSet: 'SHIPPED, not flipped at import: leftUp / centreUp / rightUp anchor a bubble BELOW the speaker, for a speaker near the top of the screen. A tail is 2 colours with NO shading, so a y-flip fights nothing in the panel light — but the kit performs it so the import never has to reason about it, and so the tip pixel is measured in both directions.',
        join: "the tail's edge row overwrites the panel's edge row on that side, so the ink ring opens and the paper is continuous",
        gap: M.tail.gap, gapNote: 'px the tip stops CLEAR OF THE SPRITE INK, not of the anchor: mount({clear}) takes the anchor\u2192ink-edge distance (the head anchor is the head centre, so 3 px off the anchor would bury the tip in the crown). The harness measures clear off the rig\u2019s own alpha \u2014 7 px for the adult idle at dir 4.',
        chipInteraction: 'the chip rides the edge AWAY from the tail — top-left for down tails, bottom-left for up tails — so a name and a tail never fight for a corner',
      },
      chip: Object.assign({}, M.chip, { paper: GOLD[2], ramp: GOLD, mount: 'x + 4, y − (h − 4) for down tails; y + panelH − 4 for up tails',
        widthFormula: MT.chip.formula, case: 'CAPS', perSpeakerTint: 'FLAGGED, not built — a tint would slot at chip.paper only; the ink and the ramp steps stay.' }),
      options: Object.assign({}, M.opt, {
        widthFormula: MT.optionFormula.w, heightFormula: MT.optionFormula.h, ladder: MT.optionFormula.step,
        gutterNote: 'the ' + M.opt.gutter + ' px gutter is the cursor lane — the cursor never overlaps text',
        highlight: HL_ORDER.map(k => ({ key: k, label: HIGHLIGHTS[k].label, ship: !!HIGHLIGHTS[k].ship, note: HIGHLIGHTS[k].note })),
        invariant: "THE RULE: neither bubble ever occludes the speaker's sprite rect (the character IS the portrait). speakerClear = " + M.opt.speakerClear + ' px is ONE implementation of it; screenBudget() is the proof at a zoom tier.',
        mount: 'optionsMount(sayLayout, {bounds}): hangs off the speech bubble on the side AWAY from the tail (below it for down tails, above it for up tails), keeping speakerClear from the tail column',
        mountFallback: 'the away side is a preference, not a guarantee — a below-right tail near the left edge of the screen has no room there. Pass bounds {x0,x1} (drawable span, same coordinate space) and the mount takes the side that fits, returning side / away / clearFromTail / clamped. clamped:true means neither side fits: shorten the rows or move the camera, do not nudge pixels.',
      }),
      cursor: CURSOR_ORDER.map(k => ({ key: k, label: CURSORS[k].label, ship: !!CURSORS[k].ship, w: CURSORS[k].w, h: CURSORS[k].h, point: CURSORS[k].point, note: CURSORS[k].note })),
      marker: Object.assign({}, M.marker, { shape: '7x4 solid triangle, ink ring + paper fill', anim: '2-frame idle bob, +1 px, ' + M.marker.ms + ' ms per frame', mount: 'x + w − 11, y + h − 2 — rides the bottom edge, right of centre, clear of every tail' }),
      caret: Object.assign({}, M.caret, { shape: '1 × ' + M.caret.h + ' ink bar in the cell the next character lands in', shown: 'while filling only; it hands off to the continue marker when the line completes', why: 'the sound IS the bubble populating — the caret is the visible half of that cadence' }),
      stocks: STOCK_ORDER.map(k => ({ key: k, label: STOCKS[k].label, ship: !!STOCKS[k].ship, paper: STOCKS[k].paper, hi: STOCKS[k].hi, lo: STOCKS[k].lo, ink: STOCKS[k].ink, edge: STOCKS[k].edge, provenance: STOCKS[k].prov, note: STOCKS[k].note })),
      palette: { rule: 'no new colours (ADR 0015)', gold: { ramp: GOLD.slice(0, 3), prov: 'consoleIsoRig.js GOLD — fleet cove pinstripe' },
        skin: { ramp: SKIN, prov: 'headIsoRig3.js SKINS.fair steps 2-5' }, wool: { ramp: WOOL, prov: 'headIsoRig3.js HATCOLS.oat' },
        steel: { ramp: STEEL, prov: 'doryMotorRig.js STEEL' } },
      occupancy: screenBudget(),
      api: {
        say: 'say(ctx,x,y,{text,cols,name,tail,fill,frame,stock,marker,caret}) -> layout',
        layoutSay: 'layoutSay(x,y,o) — same maths, nothing drawn (top/bottom include chip and tail)',
        mount: 'mount(anchor,o) -> {x,y} so the tail tip lands on the speaker anchor, above or below by tail dir',
        options: 'options(ctx,x,y,{rows,sel,cols,highlight,cursor,stock}) -> layout',
        pieces: 'panel · tail · chip · cursor · marker · caret · slices(w,h)',
        metrics: 'metricsTable({cols,lines,optCols}) — the line arithmetic the fill needs',
        budget: 'screenBudget({out,tiers,speaker,cols,lines,rows}) — the invariant as arithmetic',
        sheet: 'sheet({stock}) -> canvas · sheetSpec({stock}) -> the same crop-box table as data',
      },
    };
  }

  root.BubbleKit = {
    PPU, CELL, M, FONT, STOCKS, STOCK_ORDER, TAILS, TAIL_ORDER, CURSORS, CURSOR_ORDER,
    HIGHLIGHTS, HL_ORDER, GOLD, SKIN, WOOL, STEEL, MARKER, SHEET_TEXT,
    text, textW, colsFor, wrap, panel, slices, tail, tailX, chip, cursor, marker, caret,
    say, layoutSay, mount, options, layoutOptions, optionsMount,
    metricsTable, screenBudget, render, sheet, sheetSpec, contract, version: 2,
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
