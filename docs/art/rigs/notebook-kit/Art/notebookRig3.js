/* Hidden Harbours — THE PLAYER'S NOTEBOOK, pass 3. The book is the player's MAIN UI surface
   (owner, 2026-08-17): active quests with their steps, finished quests, every how-to/knowledge
   page, and the lists she writes for herself. No menus — a physical book.

   Supersedes notebookRig2.js (NotebookRig2). Global: NotebookKit / NotebookIsoKit.
   REQUIRES Art/dialogueBubbleRig.js — not optionally. See THE TYPE below.

   ─────────────────────────────────────────────────────────────────────────────────────────────
   WHAT CHANGED, AND WHY IT HAD TO

   1. ONE TYPE SYSTEM WITH THE BUBBLE KIT — and it is an import, not a promise.
      Pass 2 set the page in a 4 x 8 caps-only cell. The bubble kit shipped a 5 x 10 cell with a
      full lowercase set, because speech in mixed case reads as voice. Knowledge pages are the
      longest running text in the game — paragraphs, not labels — so lowercase is not optional
      here either, and two faces in one game is the thing nobody notices until it is everywhere.
      So this rig does not carry a face at all: CELL and FONT come from BubbleKit, and probeType()
      fails if they ever diverge. A player reads a bubble and a page in the same voice because it
      is literally the same glyph set, drawn CLEAN on the paper as it is in the bubble.
      ROUGHNESS IS OFF (owner, 2026-08-17). Passes 1-2 roughened the letters to read as written; at
      one cell that costs more legibility than it buys character, and knowledge pages are the longest
      running text in the game. What carries "she wrote this" is the FURNITURE - her pencil-ruled
      leaves, her wobbling checkboxes, her strikes, pencil weight for her own lines, and the write-on
      laying a line down a cell at a time. The roughened mode survives behind hand(.., {rough:1}),
      lives on the taste board, and is still asserted by probeHand().
      COST, stated once: the cell went 4 x 8 -> 5 x 10, so every number below is a new number. A
      22-col 16-line spread was 209 x 143 px in pass 2 and is 305 x 192 px here (+46% w, +34% h).
      Taken once, before the freeze.

   2. THE ENTRY IS A QUEST, NOT A LINE. Pass 2's row was one line of text with a tick box. A quest
      is a TITLE, an optional context line, and a STEP LIST whose current step is the one thing the
      eye must find first. Three checkbox states (unchecked / checked / current), one checkbox
      family, reused verbatim by the player's own note lines.

   3. OVERFLOW IS MORE PAGES. flow() lays blocks across BOTH leaves and refuses to split a quest
      across the gutter unless it cannot fit a leaf at all; what is left over is a page-turn, never
      a scrollbar. pageTurn() draws the 3 flip frames. probeFlow() asserts the arithmetic.

   4. NO BAKED TEXT. Every title, step, knowledge line, tab label and note is caller data. What
      ships is furniture with measured rects: chips, rules, boxes, slots, marks, stamps.

   5. SIX TASTE BOARDS, drawn by the rig from the same functions the page calls, so a tile cannot
      look one way on the board and another way on the page. boards() marks what ships; the owner's
      calls are flagged in contract().flagged, not resolved here.

   WHAT DID NOT CHANGE: the finding. True cursive does not survive 6 px of cap height with no
   anti-aliasing — the joins fill and the line greys out. What survives is an upright hand-set
   letterform carrying baseline wobble, dropped pixels and per-line drift, with the crowding held
   INSIDE the cell (glyph 4 wide in a 5 wide cell) so the advance stays exact: wrap, caret, fill
   and hit-boxes are arithmetic, not guesses.

   Diegetic law: knowledge lives in things and people. The book is a thing she writes in, so it must
   never read as a menu or a panel - but the LETTERS are the game's one face, clean. The object does
   the talking: stock, rules, tabs, boxes, ink and pencil weight, wear. */
(function (root) {
  'use strict';

  const BK = root.BubbleKit;
  if (!BK) {
    console.error('[notebookRig3] Art/dialogueBubbleRig.js must load FIRST — the notebook has no ' +
      'face of its own. One type system across the talking UI and the book UI is the whole point.');
    return;
  }

  const PPU = 32, VERSION = 3;

  /* ============================== the type =================================
     Imported, not declared. `rule` is the only number this kit adds: where the printed rule sits
     inside the imported line box, one row under the baseline, so descenders cross it the way they
     do on real ruled stock. */
  const FONT = BK.FONT;
  const BC = BK.CELL;                                  /* adv 5 · w 4 · box 8 · line 10 · base 5 */
  const CELL = { adv: BC.adv, w: BC.w, box: BC.box, cap: BC.cap, xh: BC.xh, desc: BC.desc,
    baseline: BC.baseline, line: BC.line, leading: BC.leading,
    slack: BC.adv - BC.w, rule: BC.baseline + 2, pitch: BC.line,
    from: 'BubbleKit.CELL — imported, never redeclared' };

  const textW = (str, s) => { s = s || 1; return Math.max(0, String(str).length * CELL.adv * s - s); };
  const colsFor = (px, s) => { s = s || 1; return Math.max(0, Math.floor((px + s) / (CELL.adv * s))); };
  const linesFor = (px, s) => { s = s || 1; return Math.max(0, Math.floor(px / (CELL.pitch * s))); };
  const wrap = (str, cols) => BK.wrap(String(str == null ? '' : str), Math.max(1, cols));

  function rng32(seed) { let a = (seed | 0) >>> 0; return function () { a |= 0; a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }

  /* the same glyphs with no roughening and no write-on limit. probeHand() diffs the roughened mode
     against this, and the sheet sets its own captions in it. The PAGE draws through hand() at
     rough 0, which is pixel-identical to this. */
  function text(ctx, str, x, y, s, col) {
    ctx.fillStyle = col; str = String(str); s = s || 1;
    for (let i = 0; i < str.length; i++) {
      const G = FONT[str[i]] || FONT['?'], rows = G.g || G, t = G.t || 0;
      for (let r = 0; r < rows.length; r++) for (let c = 0; c < rows[r].length; c++)
        if (rows[r][c] === '#') ctx.fillRect(x + (i * CELL.adv + c) * s, y + (t + r) * s, s, s);
    }
    return x + str.length * CELL.adv * s - s;
  }

  /* THE FACE ON THE PAGE. Cell-locked: origins[i].x is always x + i·adv·s, whatever the seed.

     ROUGHNESS SHIPS OFF (owner, 2026-08-17: the roughened hand was unreadable at page sizes). The
     page is set in the bubble kit's face EXACTLY as the bubble sets it — same glyphs, same cell, same
     weight, one voice you can actually read. What makes the book read as HERS is the furniture: her
     pencil rules, her wobbling checkboxes, her strikes, and pencil weight for her own text. The
     roughening (baseline wobble, dropped strokes, crowding inside the cell, per-line drift) is still
     here behind `rough`, drawn on the taste board, and costs one flag to switch back on.
       opt.rough  0 = the bubble kit's face, clean (DEFAULT) · 1 = the roughened hand
       opt.limit  cells revealed (the write-on). The rng stream does NOT depend on it, so a glyph
                  drawn at limit k is the same pixels at limit k+9 — the row never shimmers.
       opt.press  how much of the stroke a roughened pencil left behind. IGNORED when rough is 0:
                  ink and pencil are told apart by COLOUR, not by holes in the letters.
       opt.skew   per-line drift off the rule; roughened only
     y is the TOP of the glyph box, exactly as BubbleKit.text() takes it. */
  let ROUGH = 0;
  let CALLS = null;                    /* the probes count draw calls through this; null otherwise */
  const tally = (k) => { if (CALLS) CALLS[k] = (CALLS[k] || 0) + 1; };

  function hand(ctx, str, x, y, s, col, colLo, seed, opt) {
    opt = opt || {}; s = s || 1;
    const rough = opt.rough == null ? ROUGH : opt.rough;
    const rnd = rng32(seed);
    const press = !rough ? 1 : (opt.press == null ? 0.90 : opt.press);
    const skew = !rough ? 0 : (opt.skew == null ? (rnd() - 0.5) * 0.09 : opt.skew);
    const S = String(str == null ? '' : str);
    const limit = opt.limit == null ? S.length : Math.max(0, Math.min(S.length, Math.round(opt.limit)));
    const origins = [];
    for (let i = 0; i < S.length; i++) {
      const G = FONT[S[i]] || FONT['?'], rows = G.g || G, t = G.t || 0;
      const cx = x + i * CELL.adv * s;                              /* THE CELL — exact, always */
      const dy = !rough ? 0 : Math.round((rnd() - 0.5) * 1.7) * s + Math.round(i * skew) * s;
      const dx = !rough ? 0 : (rnd() < 0.30 ? CELL.slack : 0) * s;  /* the crowding, inside the cell */
      const px = [];
      for (let r = 0; r < rows.length; r++) for (let c = 0; c < rows[r].length; c++) {
        if (rows[r][c] !== '#') continue;
        if (!rough) { px.push([c, t + r, true]); continue; }
        const keep = rnd() <= press, dark = rnd() > 0.22;
        if (keep) px.push([c, t + r, dark]);
      }
      origins.push({ i, ch: S[i], x: cx, y: y + dy, dx, dy });
      if (i >= limit || !ctx) continue;
      for (const p of px) { ctx.fillStyle = p[2] ? col : colLo;
        ctx.fillRect(cx + dx + p[0] * s, y + dy + p[1] * s, s, s); }
    }
    return { w: textW(S, s), cells: S.length, adv: CELL.adv * s, origins, drawn: limit, limit,
      cellExact: true, rough: !!rough, str: S };
  }

  /* a pen line with a wobble and the odd skip: rules she drew, ticks, strikes, brackets, frames. */
  function penStroke(ctx, x0, y0, x1, y1, col, colLo, seed, drop, s) {
    s = s || 1;
    const rnd = rng32(seed), n = Math.max(2, Math.round(Math.hypot(x1 - x0, y1 - y0) / s));
    for (let i = 0; i <= n; i++) { const t = i / n;
      if (rnd() < (drop == null ? 0.10 : drop)) continue;
      ctx.fillStyle = rnd() > 0.3 ? col : colLo;
      ctx.fillRect(Math.round((x0 + (x1 - x0) * t) / s) * s + (rnd() < 0.18 ? s : 0),
                   Math.round((y0 + (y1 - y0) * t) / s) * s + (rnd() < 0.18 ? s : 0), s, s); }
  }

  /* ============================== palettes =================================
     ONE lit state ships. The lamplit palette is a PREVIEW of the in-engine day/night grade
     (bible §6) — the book is read at night and the owner asked to see it. No new colours
     (ADR 0015): every hex below is carried from pass 1/2 or lifted from a shipped rig. */
  const DAY = {
    cover: ['#1e2a2c', '#263436', '#2f4043', '#3a4d50'],
    edge: '#b9b19c', edgeLo: '#9a9280',
    page: '#d9d3c2', pageHi: '#e5dfcf', pageLo: '#c3bcaa', gutter: '#a49b88',
    rule: '#a9b0ad', square: '#bdbfb4', margin: '#a8654c',
    ink: '#22303c', inkLo: '#3d4d58', pencil: '#4d525a', pencilLo: '#666c75',
    tab: '#c9bfa4', tabLate: '#cfc7b6', tape: '#c8b98f', cloth: '#b09e80', tabInk: '#2b3742',
    shadow: '#b3ab98', stamp: '#a8654c', stampLo: '#8e5340', ribbon: '#8e4534', ribbonLo: '#743629',
  };
  const NIGHT = {
    cover: ['#131b1d', '#182224', '#1e2a2c', '#263436'],
    edge: '#7d735f', edgeLo: '#655d4c',
    page: '#94856a', pageHi: '#a39376', pageLo: '#7b6e58', gutter: '#635947',
    rule: '#6e6f63', square: '#7b7a6c', margin: '#7c4a37',
    ink: '#1b242c', inkLo: '#2d3841', pencil: '#3a3f46', pencilLo: '#4c525a',
    tab: '#8b8168', tabLate: '#8f8874', tape: '#8a7d5c', cloth: '#7b6e58', tabInk: '#1f2830',
    shadow: '#736b58', stamp: '#7c4a37', stampLo: '#653c2e', ribbon: '#743629', ribbonLo: '#5a2a20',
  };
  const GOLD = BK.GOLD;                                  /* the bubble kit's selection gold, shared */
  const SKIN = BK.SKIN;
  const PROV = {
    cover: 'notebookRig.js pass 1 — waxed softcover, salt-bleached',
    page: 'notebookRig.js pass 1 — cartridge stock',
    margin: 'notebookRig.js pass 1 — the one printed colour on the stock',
    stamp: 'the printed margin colour, reused: the harbourmaster stamps in the same ink the stock is ruled in',
    ribbon: 'notebookRig.js pass 1 RIBBON ramp (the closed object\u2019s bookmark)',
    cloth: 'BubbleKit.WOOL = headIsoRig3.js HATCOLS.oat',
    gold: 'BubbleKit.GOLD = consoleIsoRig.js GOLD — fleet cove pinstripe',
    skin: 'BubbleKit.SKIN = headIsoRig3.js SKINS.fair steps 2-5',
  };

  /* ============================== the geometry =============================
     px at scale 1. Three lanes, and every row kind shares them, so a title, a step, a knowledge
     line and a note line all land on the same two x positions down the whole book.

       padL │ cursorLane (the hand) │ boxLane (box + air) │ text lane (cols × adv − 1) │ padR
             ^cursorX               ^titleX/boxX          ^textX
     A TITLE starts at titleX and runs titleCols wide; a STEP's box sits at boxX and its text at
     textX, cols wide. titleCols = cols + boxLane/adv exactly — boxLane is two cells for that
     reason and no other. */
  const GEO = {
    coverPad: 3, edge: 1, gutter: 6, tabCol: 28,
    padL: 4, padR: 4, cursorLane: 12, boxLane: 2 * CELL.adv, box: 6,
    ruleInset: 5, headH: 16, padB: 8, pitch: CELL.pitch, sq: CELL.adv,
    tabH: 12, tabGap: 3, tabTop: 7, maxTabs: 8, tabPad: 3,
    minCols: 16, maxCols: 40, minLines: 6, maxLines: 22, maxScale: 6,
    ribbonW: 3, plateMinLines: 5,
  };
  const pageW = (cols) => GEO.padL + GEO.cursorLane + GEO.boxLane + (cols * CELL.adv - 1) + GEO.padR;
  const pageH = (lines) => GEO.headH + lines * GEO.pitch + GEO.padB;
  const totalW = (cols) => 2 * pageW(cols) + GEO.gutter + 2 * (GEO.coverPad + GEO.edge) + GEO.tabCol;
  const totalH = (lines) => pageH(lines) + 2 * (GEO.coverPad + GEO.edge);
  const titleColsFor = (cols) => cols + GEO.boxLane / CELL.adv;
  const sizeFor = (cols, lines, s) => { s = s || 1;
    return { s, cols, lines, titleCols: titleColsFor(cols), w: totalW(cols) * s, h: totalH(lines) * s,
      cells: 2 * cols * lines }; };

  /* THE PLACEMENT ANSWER. Give it the room you have; it returns the largest INTEGER cell scale that
     still holds a legal spread, and the cols and lines that fill that room. Never clamp a returned
     rect yourself — pass the room in and read `clamped`, or the flag becomes a lie. */
  function fit(availW, availH, o) {
    o = o || {};
    const maxS = o.maxScale || GEO.maxScale;
    const minC = o.minCols || GEO.minCols, minL = o.minLines || GEO.minLines;
    const maxC = o.maxCols || GEO.maxCols, maxL = o.maxLines || GEO.maxLines;
    for (let s = Math.max(1, Math.min(maxS, o.scale || maxS)); s >= 1; s--) {
      const cols = Math.min(maxC, Math.floor((Math.floor(availW / s) - totalW(0)) / (2 * CELL.adv)));
      const lines = Math.min(maxL, Math.floor((Math.floor(availH / s) - totalH(0)) / GEO.pitch));
      if (cols >= minC && lines >= minL) {
        const z = sizeFor(cols, lines, s);
        return Object.assign(z, { clamped: false, slackX: Math.floor(availW - z.w),
          slackY: Math.floor(availH - z.h), min: { cols: minC, lines: minL } });
      }
      if (o.scale) break;                     /* an explicit scale is honoured, not walked down */
    }
    const z = sizeFor(minC, minL, 1);
    return Object.assign(z, { clamped: true, slackX: Math.floor(availW - z.w),
      slackY: Math.floor(availH - z.h), min: { cols: minC, lines: minL },
      why: 'the smallest legal spread (' + z.w + '\u00d7' + z.h + ' px, ' + minC + '\u00d7' + minL +
        ' cells a leaf at scale 1) does not fit this rect. Give the book more room, or close it \u2014 ' +
        'do not scale the hand below one cell.' });
  }

  /* ============================== layout ===================================
     Nothing drawn. Both leaves get the same row grid; what differs is the stock printed on them. */
  function layout(X, Y, WW, HH, o) {
    o = o || {};
    const F = o.fit || fit(WW, HH, o);
    const s = F.s, cols = F.cols, lines = F.lines, G = (v) => v * s;
    const x = Math.round(X) + (o.align === 'topleft' ? 0 : Math.max(0, Math.floor((WW - F.w) / 2)));
    const y = Math.round(Y) + (o.align === 'topleft' ? 0 : Math.max(0, Math.floor((HH - F.h) / 2)));
    const tabW = G(GEO.tabCol), cp = G(GEO.coverPad), ed = G(GEO.edge);
    const pw = G(pageW(cols)), gut = G(GEO.gutter);
    const cover = { x, y, w: F.w - tabW, h: F.h };
    const block = { x: cover.x + cp, y: cover.y + cp, w: cover.w - 2 * cp, h: cover.h - 2 * cp };
    const inner = { x: block.x + ed, y: block.y + ed, w: block.w - 2 * ed, h: block.h - 2 * ed };
    const gutterRect = { x: inner.x + pw, y: inner.y, w: gut, h: inner.h };

    const leaf = (i) => {
      const lx = i === 0 ? inner.x : inner.x + pw + gut;
      const cursorX = lx + G(GEO.padL), titleX = cursorX + G(GEO.cursorLane);
      const textX = titleX + G(GEO.boxLane);
      const first = inner.y + G(GEO.headH), rows = [];
      for (let r = 0; r < lines; r++) {
        const top = first + r * G(GEO.pitch), rule = top + G(CELL.rule);
        rows.push({ i: r, leaf: i, rule, top,
          band: { x: lx, y: top, w: pw, h: G(GEO.pitch) },
          box: { x: titleX, y: top + G(1), w: G(GEO.box), h: G(GEO.box) },
          title: { x: titleX, y: top, cols: F.titleCols, w: F.titleCols * G(CELL.adv) - s, h: G(CELL.box) },
          text: { x: textX, y: top, cols, w: cols * G(CELL.adv) - s, h: G(CELL.box) },
          cursor: { x: cursorX, y: top },
          hit: { x: lx, y: top, w: pw, h: G(GEO.pitch) } });
      }
      const headRow = { x: titleX, y: inner.y + G(3), w: pw - G(GEO.padL + GEO.cursorLane + GEO.padR),
        h: G(CELL.box), rule: inner.y + G(GEO.headH - 5), cols: colsFor(pw - G(GEO.padL + GEO.cursorLane + GEO.padR), s) };
      return { i, x: lx, y: inner.y, w: pw, h: inner.h, cursorX, titleX, boxX: titleX, textX,
        marginX: lx + G(GEO.ruleInset), head: headRow, rows,
        foot: { x: lx + G(GEO.padL), y: inner.y + inner.h - G(GEO.padB - 1), w: pw - G(2 * GEO.padL) },
        pageNo: { x: i === 0 ? lx + G(GEO.padL) : lx + pw - G(GEO.padL) - textW('00', s),
          y: inner.y + inner.h - G(GEO.padB - 1), w: textW('00', s), h: G(CELL.box) } };
    };
    const leaves = [leaf(0), leaf(1)];
    const R = leaves[1];
    const plateLines = Math.max(GEO.plateMinLines, Math.min(9, lines - 4));
    const plate = { x: R.textX - G(CELL.adv), y: R.rows[Math.min(1, lines - 1)].top,
      w: R.w - (R.textX - G(CELL.adv) - R.x) - G(GEO.padR), h: G(GEO.pitch) * plateLines,
      lines: plateLines, cap: { x: R.textX - G(CELL.adv), y: R.rows[Math.min(1, lines - 1)].top + G(GEO.pitch) * plateLines + G(2) } };

    const tabs = [];
    for (let i = 0; i < GEO.maxTabs; i++) {
      const th = G(GEO.tabH);
      tabs.push({ i, x: cover.x + cover.w, y: y + G(GEO.tabTop) + i * (th + G(GEO.tabGap)),
        w: tabW, h: th, cols: colsFor(tabW - G(GEO.tabPad + 1), s),
        label: { x: cover.x + cover.w + G(GEO.tabPad), y: y + G(GEO.tabTop) + i * (th + G(GEO.tabGap)) + G(2),
          w: tabW - G(GEO.tabPad + 1), h: G(CELL.box) },
        cursor: { x: cover.x + cover.w + tabW, y: y + G(GEO.tabTop) + i * (th + G(GEO.tabGap)) + G(1) } });
    }
    return { version: VERSION, s, fit: F, cols, titleCols: F.titleCols, lines, cells: 2 * cols * lines,
      x, y, w: F.w, h: F.h, cover, block, spread: inner, inner, gutter: gutterRect, leaves,
      left: leaves[0], right: leaves[1], pitch: G(GEO.pitch), lh: G(GEO.pitch), headH: G(GEO.headH),
      tabs, tabW, tabMountX: cover.x + cover.w, plate,
      ribbon: { x: inner.x + pw - G(GEO.ribbonW + 2), y: cover.y, w: G(GEO.ribbonW), h: cover.h + G(9) },
      dogEar: { x: R.x + R.w - G(9), y: R.y + R.h - G(9), w: G(9), h: G(9) },
      cornerNext: { x: R.x + R.w - G(11), y: R.y + R.h - G(9), w: G(9), h: G(7) },
      cornerPrev: { x: leaves[0].x + G(2), y: leaves[0].y + leaves[0].h - G(9), w: G(9), h: G(7) },
      overflowAt: { x: R.textX, y: R.rows[lines - 1].top + G(GEO.pitch) + G(1) } };
  }

  /* ==================== flow: blocks into leaves, then pages ================
     A quest is a BLOCK: title (+ context) + steps. A knowledge section is a block. A note line is
     a block. Blocks are kept whole — a quest does not straddle the gutter unless it cannot fit a
     leaf at all — and what will not fit is not drawn small, it is the NEXT PAGE. */
  function blockRows(b, L) {
    const rows = [];
    const T = L.titleCols, C = L.cols;
    if (b.kind === 'quest') {
      wrap(b.title || '', T).forEach((t, j) => rows.push({ t: 'title', text: t, first: j === 0 }));
      if (b.context) wrap(b.context, T).forEach((t) => rows.push({ t: 'context', text: t }));
      (b.steps || []).forEach((st, si) => wrap(st.text || '', C).forEach((w, j) =>
        rows.push({ t: 'step', text: w, first: j === 0, state: st.state || 'todo', si })));
      if (b.gap !== false) rows.push({ t: 'blank' });
    } else if (b.kind === 'section') {
      if (b.head) rows.push({ t: 'head', text: b.head });
      wrap(b.body || '', T).forEach((t) => rows.push({ t: 'body', text: t }));
      if (b.slot) for (let i = 0; i < (b.slotLines || GEO.plateMinLines); i++) rows.push({ t: 'slot', i });
      if (b.gap !== false) rows.push({ t: 'blank' });
    } else if (b.kind === 'note') {
      const cols = b.box ? C : T;
      wrap(b.text || '', cols).forEach((w, j) =>
        rows.push({ t: b.box ? 'check' : 'prose', text: w, first: j === 0, state: b.state || 'todo' }));
      if (b.gap) rows.push({ t: 'blank' });
    } else if (b.kind === 'newnote') {
      rows.push({ t: 'newnote' });
    }
    return rows;
  }

  function flow(blocks, L, o) {
    o = o || {};
    const placed = [], perLeaf = L.lines, used = [0, 0];
    let leafI = 0, dropped = 0, firstDropped = -1;
    for (let k = 0; k < (blocks || []).length; k++) {
      const b = blocks[k], specs = blockRows(b, L);
      if (!specs.length) continue;
      let need = specs.length;
      /* trailing blank never forces a page turn on its own */
      const bodyNeed = specs[specs.length - 1].t === 'blank' ? need - 1 : need;
      if (leafI < 2 && used[leafI] + bodyNeed > perLeaf) {
        if (bodyNeed <= perLeaf) { leafI++; }                  /* keep it whole: next leaf */
        else if (used[leafI] === 0) { /* it cannot fit a leaf at all: split it, honestly */ }
        else { leafI++; }
      }
      if (leafI > 1) { dropped += 1; if (firstDropped < 0) firstDropped = k; continue; }
      const rows = [], leaf = L.leaves[leafI];
      let li = leafI;
      for (const spec of specs) {
        if (used[li] >= perLeaf) {                              /* only a giant block gets here */
          if (li === 0) { li = 1; } else { dropped += 1; if (firstDropped < 0) firstDropped = k; break; }
        }
        rows.push({ spec, r: L.leaves[li].rows[used[li]] });
        used[li] += 1;
      }
      if (!rows.length) continue;
      const r0 = rows[0].r, rN = rows[rows.length - 1].r;
      placed.push({ k, block: b, leaf: rows[0].r.leaf, rows,
        rect: { x: r0.band.x, y: r0.band.y, w: r0.band.w, h: rN.band.y + rN.band.h - r0.band.y },
        hit: { x: r0.band.x, y: r0.band.y, w: r0.band.w, h: rN.band.y + rN.band.h - r0.band.y } });
      leafI = rows[rows.length - 1].r.leaf;
      if (used[leafI] >= perLeaf && leafI === 0) leafI = 1;
    }
    return { placed, overflow: dropped, firstDropped, used,
      capacity: perLeaf * 2, filled: used[0] + used[1] };
  }

  /* ============================== the pieces ===============================
     One function per drawable thing, because sheet() and boards() call exactly these. */
  function stampMask(ctx, mask, x, y, cols, s) {
    s = s || 1;
    for (let r = 0; r < mask.length; r++) for (let c = 0; c < mask[r].length; c++) {
      const ch = mask[r][c]; if (ch === '.') continue;
      const col = cols[ch]; if (!col) continue;
      ctx.fillStyle = col; ctx.fillRect(x + c * s, y + r * s, s, s);
    }
  }

  /* the bubble kit's cursors, imported. One hand points at bubbles and at pages. */
  const CURSORS = {
    hand: { label: 'POINTING HAND', ship: true, ramp: SKIN,
      note: 'BubbleKit.CURSORS.hand, unchanged and imported. One selection vocabulary across the ' +
        'talking UI and the book UI \u2014 the player learns the pointer once. It rests in the margin ' +
        'lane, over the printed margin rule, which is where a hand actually goes on a page.' },
    cuff: { label: 'HAND + CUFF', ship: false, ramp: SKIN,
      note: 'The bubble kit\u2019s knitted-cuff alternate. Still on the owner\u2019s board there; if it wins ' +
        'there it wins here, because there is only one cursor family.' },
    tack: { label: 'BRASS TACK', ship: false, ramp: GOLD,
      note: 'An object pressed into the page \u2014 no anatomy to get wrong. Reads as a rivet before it ' +
        'reads as a pointer.' },
  };
  const CURSOR_ORDER = ['hand', 'cuff', 'tack'];
  function cursorSpec(kind) { return BK.CURSORS[kind] || BK.CURSORS.hand; }
  function cursor(ctx, x, y, kind, o) {
    o = o || {}; const s = o.s || 1, P = o.night ? NIGHT : DAY;
    const C = cursorSpec(kind), R = (CURSORS[kind] || CURSORS.hand).ramp;
    stampMask(ctx, C.mask, x, y, { k: P.ink, p: R[2], h: R[3], l: R[1], d: R[0], g: GOLD[2] }, s);
    return { x, y, w: C.w * s, h: C.h * s,
      point: { x: x + C.point[0] * s, y: y + C.point[1] * s } };
  }

  /* selection treatments — the bubble kit's four, on paper */
  const HIGHLIGHTS = {
    pill: { label: 'GOLD PILL', ship: true,
      note: 'The bubble kit\u2019s selected row in the same cove gold. Selection must never be missed and ' +
        'the owner ruled on it once; this rig does not re-open it. NOTE the collision below: if the ' +
        'CURRENT step also takes gold, gold stops meaning "you are here".' },
    bracket: { label: 'PENCIL BRACKET', ship: false,
      note: 'Hand-drawn brackets round the row, as if she marked it. The most diegetic of the four and ' +
        'the strongest contender if gold ever reads as UI on paper stock.' },
    rule: { label: 'GOLD RULE', ship: false, note: 'Paper stays, a gold rule under the row. Lightest touch, leans hardest on the cursor.' },
    press: { label: 'PRESSED ROW', ship: false, note: 'The row is inset a pixel, shade above and light below. Costs no colour; quietest.' },
  };
  const HL_ORDER = ['pill', 'bracket', 'rule', 'press'];

  function highlight(ctx, rect, kind, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, s = o.s || 1;
    const b = { x: rect.x + s * 2, y: rect.y, w: rect.w - s * 4, h: rect.h };
    if (kind === 'pill') {
      ctx.fillStyle = GOLD[2]; ctx.fillRect(b.x + s, b.y, b.w - 2 * s, b.h);
      ctx.fillStyle = GOLD[3]; ctx.fillRect(b.x + s, b.y, b.w - 2 * s, s);
      ctx.fillStyle = GOLD[1]; ctx.fillRect(b.x + s, b.y + b.h - s, b.w - 2 * s, s);
      ctx.fillStyle = GOLD[0]; ctx.fillRect(b.x, b.y + s, s, b.h - 2 * s);
      ctx.fillRect(b.x + b.w - s, b.y + s, s, b.h - 2 * s);
    } else if (kind === 'bracket') {
      const c = P.pencil, cl = P.pencilLo, k = 3 * s;
      penStroke(ctx, b.x, b.y + s, b.x + k, b.y + s, c, cl, 61, 0.14, s);
      penStroke(ctx, b.x, b.y + s, b.x, b.y + b.h - s, c, cl, 62, 0.14, s);
      penStroke(ctx, b.x, b.y + b.h - s, b.x + k, b.y + b.h - s, c, cl, 63, 0.14, s);
      penStroke(ctx, b.x + b.w, b.y + s, b.x + b.w - k, b.y + s, c, cl, 64, 0.14, s);
      penStroke(ctx, b.x + b.w, b.y + s, b.x + b.w, b.y + b.h - s, c, cl, 65, 0.14, s);
      penStroke(ctx, b.x + b.w, b.y + b.h - s, b.x + b.w - k, b.y + b.h - s, c, cl, 66, 0.14, s);
    } else if (kind === 'rule') {
      ctx.fillStyle = GOLD[2]; ctx.fillRect(b.x + s, b.y + b.h - 2 * s, b.w - 2 * s, s);
    } else if (kind === 'press') {
      ctx.fillStyle = P.pageLo; ctx.fillRect(b.x + s, b.y, b.w - 2 * s, s);
      ctx.fillStyle = P.pageHi; ctx.fillRect(b.x + s, b.y + b.h - s, b.w - 2 * s, s);
    }
    return b;
  }

  /* ---- the checkbox family. Drawn ONCE, reused by quest steps and by her own note lines. ---- */
  const BOXES = {
    pencil: { label: 'PENCIL SQUARE', ship: true, note: 'Four pencil strokes with the corners not quite meeting. She ruled it herself, so it wobbles.' },
    printed: { label: 'PRINTED SQUARE', ship: false, note: 'A clean printed box with a hand tick in it. Reads as a form she was given \u2014 wrong for her own lists, arguably right for quests.' },
    round: { label: 'PENCIL CIRCLE', ship: false, note: 'A scratched ring. Softer, and a filled ring makes a strong CURRENT marker \u2014 but a circle at 6 px costs a pixel of legibility.' },
  };
  const BOX_ORDER = ['pencil', 'printed', 'round'];

  function tickBox(ctx, x, y, s, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, w = GEO.box * s, k = o.seed == null ? 200 : o.seed;
    const style = o.style || 'pencil';
    const c = o.ink ? P.ink : P.pencil, cl = o.ink ? P.inkLo : P.pencilLo;
    if (style === 'printed') {
      ctx.fillStyle = P.rule; ctx.fillRect(x, y, w, s); ctx.fillRect(x, y + w - s, w, s);
      ctx.fillRect(x, y, s, w); ctx.fillRect(x + w - s, y, s, w);
    } else if (style === 'round') {
      const ring = ['.##.', '#..#', '#..#', '.##.'];
      stampMask(ctx, ring, x + s, y + s, { '#': c }, s);
    } else {
      penStroke(ctx, x, y, x + w - s, y, c, cl, k, 0.22, s);
      penStroke(ctx, x + w - s, y, x + w - s, y + w - s, c, cl, k + 1, 0.22, s);
      penStroke(ctx, x + w - s, y + w - s, x, y + w - s, c, cl, k + 2, 0.22, s);
      penStroke(ctx, x, y + w - s, x, y, c, cl, k + 3, 0.22, s);
    }
    return { x, y, w, h: w };
  }
  function tickMark(ctx, x, y, s, o) {                    /* done: ticked in the box, never emptied */
    o = o || {}; const P = o.night ? NIGHT : DAY, w = GEO.box * s, k = o.seed == null ? 400 : o.seed;
    tally('tick');
    penStroke(ctx, x + s, y + 2 * s, x + 2 * s, y + w - 2 * s, P.ink, P.inkLo, k, 0.08, s);
    penStroke(ctx, x + 2 * s, y + w - 2 * s, x + w + s, y - s, P.ink, P.inkLo, k + 1, 0.08, s);
  }

  /* ---- the CURRENT step: the one thing the eye must find first ---- */
  const CURRENTS = {
    arrow: { label: 'INK ARROW', ship: true,
      note: 'The box lane carries a solid ink arrowhead instead of a box. Costs no colour, cannot ' +
        'collide with the selection gold, and reads at one glance because nothing else on the page is solid.' },
    pill: { label: 'GOLD PILL', ship: false,
      note: 'The bubble kit\u2019s emphasis, on the step row. Strongest read on the page \u2014 and the reason ' +
        'it does not ship by default: gold already means SELECTED. Two meanings for one colour on one ' +
        'page is the owner\u2019s call, not the rig\u2019s.' },
    dot: { label: 'INKED BOX', ship: false,
      note: 'The box stays and takes a heavy ink dot. Quietest and most diegetic \u2014 she blacked in the ' +
        'one she is on \u2014 but at 6 px it is a smudge, not a signal.' },
  };
  const CURRENT_ORDER = ['arrow', 'pill', 'dot'];
  function currentMark(ctx, box, s, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, style = o.style || 'arrow';
    if (style === 'arrow') {
      stampMask(ctx, ['#...', '##..', '###.', '####', '###.', '##..', '#...'],
        box.x + s, box.y - s, { '#': P.ink }, s);
    } else if (style === 'dot') {
      tickBox(ctx, box.x, box.y, s, o);
      ctx.fillStyle = P.ink; ctx.fillRect(box.x + 2 * s, box.y + 2 * s, 2 * s, 2 * s);
    } else {
      tickBox(ctx, box.x, box.y, s, o);
    }
    return box;
  }

  /* ---- the DONE treatment: three, one ships ---- */
  const DONES = {
    strike: { label: 'STRUCK + TICKED', ship: true,
      note: 'A pencil line through the title and a tick in every box. Carried from pass 2, costs nothing, ' +
        'and keeps a finished quest legible \u2014 the page is a record of what she did, so nothing is deleted.' },
    stamp: { label: 'HARBOUR STAMP', ship: false,
      note: 'A stamped DONE in the printed margin ink, over the title row. The best read on a Done tab at ' +
        'a glance and the most characterful \u2014 but it is the harbourmaster\u2019s mark on HER book, so it ' +
        'implies someone else signs her work off. Owner\u2019s call.' },
    fade: { label: 'INK FADE', ship: false,
      note: 'The whole block redrawn at a light press, as if the ink has gone. Quietest, cheapest, and the ' +
        'most honest about time passing \u2014 loses the tick, which is the satisfying part.' },
  };
  const DONE_ORDER = ['strike', 'stamp', 'fade'];
  function strike(ctx, x, y, w, s, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, k = o.seed == null ? 420 : o.seed;
    tally('strike');
    penStroke(ctx, x - s, y + (CELL.baseline - 2) * s, x + w + s, y + (CELL.baseline - 2) * s - s,
      P.pencil, P.pencilLo, k, 0.06, s);
  }
  function doneStamp(ctx, x, y, s, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, word = o.word || 'DONE';
    tally('stamp');
    const w = textW(word, s) + 6 * s, h = (CELL.box + 2) * s, k = o.seed == null ? 470 : o.seed;
    penStroke(ctx, x, y, x + w, y, P.stamp, P.stampLo, k, 0.28, s);
    penStroke(ctx, x, y + h, x + w, y + h, P.stamp, P.stampLo, k + 1, 0.28, s);
    penStroke(ctx, x, y, x, y + h, P.stamp, P.stampLo, k + 2, 0.28, s);
    penStroke(ctx, x + w, y, x + w, y + h, P.stamp, P.stampLo, k + 3, 0.28, s);
    hand(ctx, word, x + 3 * s, y + 2 * s, s, P.stamp, P.stampLo, k + 4, { press: 0.72, skew: 0 });
    return { x, y, w, h };
  }
  /* the caret: the cell the next character lands in. The page being written IS the feedback here,
     the way the bubble populating is the feedback there. */
  function caret(ctx, x, y, s, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY;
    tally('caret');
    ctx.fillStyle = o.pencil ? P.pencil : P.ink;
    ctx.fillRect(x, y, s, (CELL.baseline + 1) * s);
    return { x, y, w: s, h: (CELL.baseline + 1) * s };
  }

  /* ---- tabs: physical dividers off the fore-edge. Four families; Knowledge takes sub-tabs. ---- */
  const TAB_STYLES = {
    card: { label: 'HAND-LETTERED CARD', ship: true, note: 'Cut card, hand-lettered in ink. The four family tabs are these.' },
    tape: { label: 'PENCIL ON TAPE', ship: false, note: 'A strip of masking tape, pencilled. Right for tabs SHE added later \u2014 knowledge sub-tabs, most likely.' },
    cloth: { label: 'STITCHED CLOTH', ship: false, note: 'Oat cloth stub, no lettering room to speak of. Best as a colour-coded family marker if labels move onto the page instead.' },
  };
  const TAB_STYLE_ORDER = ['card', 'tape', 'cloth'];
  /* the SHAPE of the tab set the owner ruled on. Labels are DATA — these are placeholders for the
     four families, and `subs` is a count the caller sets from the knowledge registry. */
  const TAB_FAMILIES = [
    { key: 'active', label: 'OPEN', page: 'quests', style: 'card' },
    { key: 'done', label: 'DONE', page: 'done', style: 'card' },
    { key: 'know', label: 'KNOW', page: 'knowledge', style: 'card', subs: 3 },
    { key: 'notes', label: 'MINE', page: 'notes', style: 'card' },
  ];
  function tabStub(ctx, b, t, state, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, s = o.s || 1;
    const style = t.style || 'card', sub = !!t.sub;
    const back = style === 'tape' ? P.tape : style === 'cloth' ? P.cloth : (sub ? P.tabLate : P.tab);
    const ox = 9 * s, on = state === 'on';
    const w = b.w + ox - 2 * s - (sub ? 3 * s : 0);
    ctx.fillStyle = back; ctx.fillRect(b.x - ox, b.y, w, b.h);
    ctx.fillStyle = P.shadow; ctx.fillRect(b.x - ox, b.y + b.h - s, w, s);
    if (style === 'tape') { const rnd = rng32(70 + b.i * 13); ctx.fillStyle = P.pencilLo;
      for (let yy = 0; yy < b.h; yy += s) if (rnd() > 0.5) ctx.fillRect(b.x - ox + w - s, b.y + yy, s, s); }
    if (style !== 'cloth') {
      const lab = String(t.label || '').slice(0, b.cols - (sub ? 1 : 0));
      hand(ctx, lab, b.label.x, b.label.y, s, sub || style === 'tape' ? P.pencil : P.tabInk,
        sub || style === 'tape' ? P.pencilLo : P.inkLo, 70 + b.i * 13, { press: 0.93, skew: 0 });
    }
    if (on) {                                       /* selected: the tab is pulled out and inked */
      ctx.fillStyle = P.tabInk; ctx.fillRect(b.x - ox, b.y, s, b.h);
      ctx.fillRect(b.x - ox, b.y, w, s);
      ctx.fillStyle = P.pageHi; ctx.fillRect(b.x - ox + s, b.y + s, w - 2 * s, s);
    }
    return { x: b.x - ox, y: b.y, w, h: b.h, state };
  }

  /* ---- the book as an object: ribbon, dog-ear, corner marks, page numbers, wear, turn ---- */
  function ribbon(ctx, L, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, s = L.s, R = L.ribbon;
    const at = o.at == null ? R.x : o.at;
    ctx.fillStyle = P.ribbonLo; ctx.fillRect(at, R.y, R.w, R.h);
    ctx.fillStyle = P.ribbon; ctx.fillRect(at, R.y, R.w - s, R.h - 2 * s);
    ctx.fillStyle = P.ribbonLo;                                       /* the cut tail */
    ctx.fillRect(at, R.y + R.h - 2 * s, R.w, s);
    ctx.fillRect(at + R.w - s, R.y + R.h - 4 * s, s, 2 * s);
    return { x: at, y: R.y, w: R.w, h: R.h };
  }
  function dogEar(ctx, L, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, s = L.s, d = L.dogEar;
    for (let i = 0; i < d.w; i += s) {
      ctx.fillStyle = P.pageHi; ctx.fillRect(d.x + i, d.y + d.h - (d.w - i), d.w - i, s);
      ctx.fillStyle = P.shadow; ctx.fillRect(d.x + i, d.y + i, s, s);
    }
    return d;
  }
  function cornerMark(ctx, rect, dir, s, o) {          /* next / prev: a pencil chevron in the corner */
    o = o || {}; const P = o.night ? NIGHT : DAY, k = dir === 'next' ? 880 : 881;
    const x = rect.x, y = rect.y, h = rect.h;
    if (dir === 'next') {
      penStroke(ctx, x, y, x + 4 * s, y + Math.floor(h / 2), P.pencil, P.pencilLo, k, 0.1, s);
      penStroke(ctx, x + 4 * s, y + Math.floor(h / 2), x, y + h, P.pencil, P.pencilLo, k + 1, 0.1, s);
    } else {
      penStroke(ctx, x + 4 * s, y, x, y + Math.floor(h / 2), P.pencil, P.pencilLo, k, 0.1, s);
      penStroke(ctx, x, y + Math.floor(h / 2), x + 4 * s, y + h, P.pencil, P.pencilLo, k + 1, 0.1, s);
    }
    return rect;
  }
  function pageNumber(ctx, leaf, n, s, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, str = String(n);
    const x = leaf.i === 0 ? leaf.pageNo.x : leaf.pageNo.x + leaf.pageNo.w - textW(str, s);
    hand(ctx, str, x, leaf.pageNo.y, s, P.pencilLo, P.pencilLo, 900 + n, { press: 0.62, skew: 0 });
    return { x, y: leaf.pageNo.y, w: textW(str, s), h: CELL.box * s };
  }
  /* WEAR. Cheap: nibbled fore-edge, a stain, a rubbed cover corner. Three levels, no new art. */
  function wear(ctx, L, level, o) {
    o = o || {}; if (!level) return null;
    const P = o.night ? NIGHT : DAY, s = L.s, rnd = rng32(1300 + level);
    const n = level * 26;
    for (let i = 0; i < n; i++) {                                     /* the fore-edge, nibbled */
      const yy = L.block.y + Math.floor(rnd() * (L.block.h / s)) * s;
      ctx.fillStyle = rnd() > 0.4 ? P.edgeLo : P.shadow;
      ctx.fillRect(L.block.x + L.block.w - s, yy, s, s);
      if (level > 1) ctx.fillRect(L.block.x, yy, s, s);
    }
    if (level > 1) {                                                  /* a stain on the right leaf */
      const cx = L.right.x + Math.floor(L.right.w * 0.62), cy = L.right.y + Math.floor(L.right.h * 0.3);
      for (let i = 0; i < 40; i++) {
        const a = rnd() * Math.PI * 2, r = (0.4 + rnd() * 0.6) * 7 * s;
        ctx.fillStyle = P.pageLo;
        ctx.fillRect(Math.round((cx + Math.cos(a) * r) / s) * s, Math.round((cy + Math.sin(a) * r * 0.7) / s) * s, s, s);
      }
    }
    return { level };
  }
  /* THE PAGE TURN. A flip, not a scroll: 3 frames, drawn OVER the spread. The leaf lifts off the
     right, stands at the gutter, lands on the left. Overflow is more pages. */
  const TURN_FRAMES = 3;
  function curlSheet(ctx, x0, x1, y, h, bow, P, s, rules) {
    const n = Math.max(1, Math.round((x1 - x0) / s));
    for (let i = 0; i < n; i++) {
      const u = n === 1 ? 0.5 : i / (n - 1), px = x0 + i * s;
      const inset = Math.round(bow * Math.sin(Math.PI * u) / s) * s;
      ctx.fillStyle = (i < 1 || i > n - 2) ? P.pageLo : (u < 0.35 ? P.pageHi : P.page);
      ctx.fillRect(px, y + inset, s, h - 2 * inset);
      if (rules) for (const ry of rules) {
        const yy = ry + Math.round(inset * (1 - 2 * (ry - y) / h));
        if (yy > y + inset && yy < y + h - inset) { ctx.fillStyle = P.rule; ctx.fillRect(px, yy, s, s); }
      }
    }
  }
  function pageTurn(ctx, L, frame, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, s = L.s, R = L.right, Lf = L.left;
    const rules = R.rows.map(r => r.rule);
    if (frame === 1) {                                   /* lifting off the right leaf */
      ctx.fillStyle = P.shadow; ctx.fillRect(R.x + Math.floor(R.w * 0.30), R.y, Math.floor(R.w * 0.06), R.h);
      curlSheet(ctx, R.x + Math.floor(R.w * 0.34), R.x + R.w, R.y, R.h, 3 * s, P, s, rules);
    } else if (frame === 2) {                            /* standing at the gutter */
      ctx.fillStyle = P.shadow; ctx.fillRect(R.x, R.y, Math.floor(R.w * 0.5), R.h);
      curlSheet(ctx, L.gutter.x + s, L.gutter.x + Math.floor(R.w * 0.42), R.y - 2 * s, R.h + 4 * s, 6 * s, P, s, null);
    } else if (frame === 3) {                            /* landing on the left leaf */
      ctx.fillStyle = P.shadow; ctx.fillRect(Lf.x + Math.floor(Lf.w * 0.62), Lf.y, Math.floor(Lf.w * 0.06), Lf.h);
      curlSheet(ctx, Lf.x, Lf.x + Math.floor(Lf.w * 0.66), Lf.y, Lf.h, 3 * s, P, s, Lf.rows.map(r => r.rule));
    }
    return { frame, frames: TURN_FRAMES };
  }

  /* ---- the illustration slot. Art reserves the room and says where; content is a later drop. --- */
  function plate(ctx, L, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, s = L.s, p = o.rect || L.plate;
    penStroke(ctx, p.x, p.y, p.x + p.w, p.y, P.pencil, P.pencilLo, 810, 0.3, s);
    penStroke(ctx, p.x, p.y + p.h, p.x + p.w, p.y + p.h, P.pencil, P.pencilLo, 811, 0.3, s);
    penStroke(ctx, p.x, p.y, p.x, p.y + p.h, P.pencil, P.pencilLo, 812, 0.3, s);
    penStroke(ctx, p.x + p.w, p.y, p.x + p.w, p.y + p.h, P.pencil, P.pencilLo, 813, 0.3, s);
    for (const c of [[p.x, p.y, 1, 1], [p.x + p.w, p.y, -1, 1], [p.x, p.y + p.h, 1, -1], [p.x + p.w, p.y + p.h, -1, -1]]) {
      ctx.fillStyle = P.pencil;
      ctx.fillRect(c[0] + (c[2] > 0 ? 0 : -3 * s), c[1] + (c[3] > 0 ? 0 : -s), 3 * s, s);
      ctx.fillRect(c[0] + (c[2] > 0 ? 0 : -s), c[1] + (c[3] > 0 ? 0 : -3 * s), s, 3 * s);
    }
    if (o.caption !== false) hand(ctx, o.caption || 'as Moss drew it', p.x + 2 * s,
      (p.cap ? p.cap.y : p.y + p.h + 2 * s), s, P.pencil, P.pencilLo, 814, { press: 0.7 });
    if (o.fill) o.fill(ctx, p, s);
    return p;
  }
  function newNoteMark(ctx, row, s, o) {                 /* the affordance: a fresh box and a cross */
    o = o || {}; const P = o.night ? NIGHT : DAY;
    tickBox(ctx, row.box.x, row.box.y, s, Object.assign({}, o, { seed: 640 + row.i }));
    penStroke(ctx, row.cursor.x + 3 * s, row.cursor.y + 3 * s, row.cursor.x + 9 * s, row.cursor.y + 3 * s,
      P.pencilLo, P.pencilLo, 641, 0.2, s);
    penStroke(ctx, row.cursor.x + 6 * s, row.cursor.y, row.cursor.x + 6 * s, row.cursor.y + 6 * s,
      P.pencilLo, P.pencilLo, 642, 0.2, s);
    return { x: row.band.x, y: row.band.y, w: row.band.w, h: row.band.h, box: row.box };
  }
  function overflowMark(ctx, L, n, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, s = L.s, a = L.overflowAt;
    hand(ctx, n + ' more', a.x, a.y - CELL.box * s, s, P.pencil, P.pencilLo, 950, { press: 0.78, skew: 0 });
    const ax = a.x + textW(n + ' more ', s) + 2 * s, ay = a.y - 3 * s;
    penStroke(ctx, ax, ay, ax + 5 * s, ay, P.pencil, P.pencilLo, 951, 0.1, s);
    penStroke(ctx, ax + 3 * s, ay - 2 * s, ax + 5 * s, ay, P.pencil, P.pencilLo, 952, 0.1, s);
    penStroke(ctx, ax + 3 * s, ay + 2 * s, ax + 5 * s, ay, P.pencil, P.pencilLo, 953, 0.1, s);
    return { x: a.x, y: a.y - CELL.box * s, w: textW(n + ' more', s) + 8 * s, h: CELL.box * s };
  }

  /* ============================== the stocks ===============================
     What is printed on a leaf. `mine` is the player's own paper: the same stock, but the rules are
     HERS — pencil, against a straight edge, so they wobble. Distinct by drawing, not by a new colour. */
  const STOCKS = {
    ruled: { label: 'PRINTED RULED', ship: true, note: 'Cartridge stock, printed rules at the imported line pitch, one printed margin rule. The book as it was bought.' },
    squared: { label: 'PRINTED SQUARED', ship: true, note: 'The facing leaf. The grid IS the text cell, so a diagram slot lands on the same lattice as the type.' },
    mine: { label: 'HER OWN RULES', ship: true, note: 'Blank stock she ruled herself in pencil. This is what the My Notes leaves are, and it is why they read as hers: no printed margin, wobbling rules.' },
    plain: { label: 'BLANK', ship: true, note: 'No printing at all. For a spread that is all diagram, and for the empty state.' },
  };
  function drawStock(ctx, leaf, kind, o) {
    o = o || {}; const P = o.night ? NIGHT : DAY, s = o.s || 1;
    if (kind === 'ruled') {
      ctx.fillStyle = P.rule;
      for (const r of leaf.rows) ctx.fillRect(leaf.x + 2 * s, r.rule, leaf.w - 4 * s, s);
      ctx.fillStyle = P.margin; ctx.fillRect(leaf.marginX, leaf.y + 2 * s, s, leaf.h - 4 * s);
    } else if (kind === 'squared') {
      ctx.fillStyle = P.square; const sq = CELL.adv * s;
      for (let x = leaf.x + 2 * s; x < leaf.x + leaf.w - 2 * s; x += sq) ctx.fillRect(x, leaf.y + 2 * s, s, leaf.h - 4 * s);
      for (let y = leaf.y + 2 * s; y < leaf.y + leaf.h - 2 * s; y += sq) ctx.fillRect(leaf.x + 2 * s, y, leaf.w - 4 * s, s);
    } else if (kind === 'mine') {
      for (const r of leaf.rows) penStroke(ctx, leaf.x + 3 * s, r.rule, leaf.x + leaf.w - 4 * s, r.rule,
        P.pencilLo, P.pencilLo, 1100 + r.i * 3 + leaf.i, 0.10, s);
    }
    return kind;
  }

  /* ============================== the spread ===============================
     drawUnit(ctx, X, Y, W, H, o) is the whole book. o.page names the furniture; o.blocks is the
     caller's data. Nothing here is baked. */
  const PAGES = {
    quests: { label: 'ACTIVE QUESTS', stocks: ['ruled', 'ruled'], note: 'title + context + step list, flowing across both leaves' },
    done: { label: 'FINISHED', stocks: ['ruled', 'ruled'], note: 'the same anatomy under the done treatment' },
    knowledge: { label: 'HOW IT IS DONE', stocks: ['ruled', 'squared'], note: 'section header, rule lines, illustration slot on the squared leaf' },
    notes: { label: 'MY OWN LISTS', stocks: ['mine', 'mine'], note: 'her paper, her rules, her checkboxes, and a place to start a new one' },
  };
  const PAGE_ORDER = ['quests', 'done', 'knowledge', 'notes'];

  function drawRowSpec(ctx, L, item, row, o) {
    const P = o.night ? NIGHT : DAY, s = L.s, spec = row.spec, r = row.r, b = item.block;
    const done = !!b.done, dstyle = o.done || 'strike';
    const fade = done && dstyle === 'fade';
    const inkC = fade ? P.pencilLo : P.ink, inkL = fade ? P.pencilLo : P.inkLo;
    const penC = fade ? P.pencilLo : P.pencil, penL = P.pencilLo;
    const basePress = fade ? 0.55 : 0.9;
    const sel = o.sel === item.k;
    const hlKind = o.highlight || 'pill';
    const gold = sel && hlKind === 'pill';
    /* a row still being written gets no strike, no tick and no stamp: those are marks on FINISHED
       text, and drawing them over cells that are not there yet is how a write-on reads as a bug. */
    const written = spec.limit == null || spec.limit >= String(spec.text || '').length;
    let lane = null;
    if (spec.t === 'title') {
      const h = hand(ctx, spec.text, r.title.x, r.title.y, s, gold ? P.ink : inkC, gold ? P.inkLo : inkL,
        300 + item.k * 7 + r.i, { press: basePress, limit: spec.limit });
      if (done && dstyle === 'strike' && written) strike(ctx, r.title.x, r.title.y, h.w, s, { night: o.night, seed: 420 + item.k });
      lane = { x: r.title.x, y: r.title.y, pencil: false };
    } else if (spec.t === 'context') {
      hand(ctx, spec.text, r.title.x, r.title.y, s, penC, penL, 340 + item.k * 5 + r.i, { press: 0.68 });
      lane = { x: r.title.x, y: r.title.y, pencil: true };
    } else if (spec.t === 'head') {
      hand(ctx, spec.text, r.title.x, r.title.y, s, inkC, inkL, 360 + item.k * 3, { press: 0.95, skew: 0 });
      penStroke(ctx, r.title.x, r.rule, r.title.x + Math.min(r.title.w, textW(spec.text, s) + 6 * s), r.rule,
        inkC, inkL, 361 + item.k, 0.14, s);
    } else if (spec.t === 'body' || spec.t === 'prose') {
      hand(ctx, spec.text, r.title.x, r.title.y, s, spec.t === 'prose' ? penC : inkC,
        spec.t === 'prose' ? penL : inkL, 380 + item.k * 11 + r.i, { press: spec.t === 'prose' ? 0.8 : basePress, limit: spec.limit });
      lane = { x: r.title.x, y: r.title.y, pencil: spec.t === 'prose' };
    } else if (spec.t === 'step' || spec.t === 'check') {
      const isCheck = spec.t === 'check';
      const state = done && !isCheck ? 'done' : spec.state;
      if (spec.first) {
        if (state === 'current') currentMark(ctx, r.box, s, { night: o.night, style: o.current || 'arrow', box: o.box, seed: 220 + item.k * 4 + spec.si });
        else tickBox(ctx, r.box.x, r.box.y, s, { night: o.night, style: o.box || 'pencil', ink: !isCheck && !fade, seed: 220 + item.k * 4 + spec.si });
        if (state === 'done' && !fade && written) tickMark(ctx, r.box.x, r.box.y, s, { night: o.night, seed: 400 + item.k * 4 + spec.si });
      }
      const cur = state === 'current' && (o.current || 'arrow') === 'pill';
      if (cur) highlight(ctx, r.band, 'pill', { s, night: o.night });
      const col = cur ? P.ink : (isCheck ? penC : inkC), colLo = cur ? P.inkLo : (isCheck ? penL : inkL);
      const h = hand(ctx, spec.text, r.text.x, r.text.y, s, col, colLo, 420 + item.k * 13 + spec.si * 3 + r.i,
        { press: isCheck ? 0.8 : basePress, limit: spec.limit });
      if (state === 'done' && !fade && written) strike(ctx, r.text.x, r.text.y, h.w, s, { night: o.night, seed: 440 + item.k + spec.si });
      lane = { x: r.text.x, y: r.text.y, pencil: isCheck };
    } else if (spec.t === 'newnote') {
      newNoteMark(ctx, r, s, { night: o.night, style: o.box || 'pencil' });
      hand(ctx, o.newNoteLabel || 'add a line', r.text.x, r.text.y, s, P.pencilLo, P.pencilLo, 660, { press: 0.5 });
    }
    /* ONE caret a block, in the cell the next character lands in — the frontier row and nowhere else */
    if (lane && item.caretRow === row && o.caretOn !== false)
      caret(ctx, lane.x + Math.max(0, spec.limit || 0) * CELL.adv * s, lane.y, s,
        { night: o.night, pencil: lane.pencil });
  }

  function drawUnit(ctx, X, Y, WW, HH, opts) {
    const o = opts || {}, P = o.night ? NIGHT : DAY;
    const L = layout(X, Y, WW, HH, o), s = L.s, G = (v) => v * s;
    const pageKind = o.page || 'quests';
    const PG = PAGES[pageKind] || PAGES.quests;
    const stocks = o.stocks || PG.stocks;
    const fams = o.tabs || TAB_FAMILIES;
    const tabList = [];
    for (const f of fams) {
      tabList.push(Object.assign({}, f));
      if (f.subs) for (let i = 0; i < f.subs; i++)
        tabList.push({ key: f.key + '.' + i, label: (o.subLabels && o.subLabels[i]) || '', sub: true, style: 'tape', page: f.page, parent: f.key });
    }
    const activeKey = o.active || fams[0].key;
    ctx.save(); ctx.imageSmoothingEnabled = false;

    /* tabs first: they sit UNDER the fore-edge, only the stubs show */
    const tabRects = [];
    for (let i = 0; i < tabList.length && i < L.tabs.length; i++) {
      const t = tabList[i], st = t.key === activeKey ? 'on' : (o.tabCursor === t.key ? 'cursor' : 'off');
      tabRects.push(Object.assign({ key: t.key, i }, tabStub(ctx, L.tabs[i], t, st, { s, night: o.night })));
      if (st === 'cursor') cursor(ctx, L.tabs[i].cursor.x, L.tabs[i].cursor.y, o.cursor || 'hand', { s, night: o.night });
    }
    /* cover, block, leaves, gutter */
    ctx.fillStyle = P.cover[1]; ctx.fillRect(L.cover.x, L.cover.y, L.cover.w, L.cover.h);
    ctx.fillStyle = P.cover[3]; ctx.fillRect(L.cover.x, L.cover.y, L.cover.w, G(1));
    ctx.fillStyle = P.cover[0]; ctx.fillRect(L.cover.x, L.cover.y + L.cover.h - G(1), L.cover.w, G(1));
    ctx.fillStyle = P.edge; ctx.fillRect(L.block.x, L.block.y, L.block.w, L.block.h);
    ctx.fillStyle = P.page;
    for (const lf of L.leaves) ctx.fillRect(lf.x, lf.y, lf.w, lf.h);
    for (let i = 0; i < L.gutter.w; i += s) {                       /* the pages curve into the spine */
      ctx.fillStyle = i / L.gutter.w < 0.5 ? P.gutter : P.pageLo;
      ctx.fillRect(L.gutter.x + i, L.gutter.y, s, L.gutter.h);
    }
    ctx.fillStyle = P.pageLo;
    ctx.fillRect(L.left.x, L.left.y, s, L.left.h);
    ctx.fillRect(L.right.x + L.right.w - s, L.right.y, s, L.right.h);
    for (let i = 0; i < 2; i++) drawStock(ctx, L.leaves[i], stocks[i] || 'ruled', { s, night: o.night });

    /* the running head, per leaf, and the tab family it belongs to */
    const heads = o.heads || [PG.label, ''];
    for (let i = 0; i < 2; i++) {
      const lf = L.leaves[i], hd = heads[i];
      if (!hd) continue;
      hand(ctx, hd, lf.head.x, lf.head.y, s, P.ink, P.inkLo, 5 + i, { press: 0.95, skew: 0.02 });
      penStroke(ctx, lf.head.x, lf.head.rule, lf.x + lf.w - G(GEO.padR + 2), lf.head.rule + s,
        P.ink, P.inkLo, 9 + i, 0.16, s);
    }
    if (o.count) hand(ctx, o.count, L.left.x + L.left.w - G(GEO.padR) - textW(o.count, s), L.left.head.y,
      s, P.pencil, P.pencilLo, 7, { press: 0.8, skew: 0 });

    /* the caller's data */
    const blocks = o.blocks || [];
    const FL = flow(blocks, L, o);
    for (const item of FL.placed) {
      if (item.block.fill != null) {                 /* the write-on, across the whole block */
        const total = item.rows.reduce((n, r) => n + (r.spec.text ? r.spec.text.length : 0), 0);
        const f = item.block.fill;
        const cells = (f > 0 && f <= 1 && !Number.isInteger(f)) ? Math.round(f * total) : Math.round(f);
        let used = 0;
        for (const row of item.rows) { const len = row.spec.text ? row.spec.text.length : 0;
          row.spec.limit = Math.max(0, Math.min(len, cells - used)); used += len;
          if (!item.caretRow && len && row.spec.limit < len) item.caretRow = row; }
        item.cells = total; item.filled = Math.min(cells, total);
      }
      const sel = o.sel === item.k;
      if (sel && (o.highlight || 'pill') !== 'none')     /* the TITLE row, not the whole block: a
            block-wide pill drowns the steps it is meant to point at */
        highlight(ctx, item.rows[0].r.band, o.highlight || 'pill', { s, night: o.night });
      for (const row of item.rows) drawRowSpec(ctx, L, item, row, o);
      /* the illustration slot FLOWS: it is the rows the section reserved for it, not a fixed rect,
         so knowledge text can never run through the frame */
      const slotRows = item.rows.filter(r => r.spec.t === 'slot');
      if (slotRows.length && o.slot !== false) {
        const a = slotRows[0].r, b = slotRows[slotRows.length - 1].r;
        const px = a.title.x - G(2);
        plate(ctx, L, { night: o.night, caption: item.block.slotCaption || o.slotCaption,
          rect: { x: px, y: a.top, w: a.band.x + a.band.w - G(GEO.padR) - px,
            h: b.top + G(GEO.pitch) - a.top - G(3), lines: slotRows.length,
            cap: { x: a.title.x, y: b.top + G(GEO.pitch) - G(1) } } });
      }
      if (item.block.done && (o.done || 'strike') === 'stamp' && (item.filled == null || item.filled >= item.cells))
        doneStamp(ctx, item.rect.x + item.rect.w - G(GEO.padR) - (textW('DONE', s) + 6 * s),
          item.rows[0].r.top, s, { night: o.night, seed: 470 + item.k });
      if (sel && o.cursor !== 'none')
        cursor(ctx, item.rows[0].r.cursor.x, item.rows[0].r.cursor.y, o.cursor || 'hand', { s, night: o.night });
    }
    if (FL.overflow) overflowMark(ctx, L, FL.overflow, { s, night: o.night });
    if (!blocks.length) {
      const r1 = L.left.rows[1] || L.left.rows[0];
      hand(ctx, o.emptyNote || 'nothing here yet', r1.title.x, r1.title.y, s, P.pencilLo, P.pencilLo, 900, { press: 0.55 });
    }
    /* the object: ribbon, wear, page numbers, corner marks, dog-ear, and the flip */
    if (o.ribbon !== false) ribbon(ctx, L, { night: o.night, at: o.ribbonAt });
    if (o.wear) wear(ctx, L, o.wear, { night: o.night });
    const pno = o.pageNo == null ? 12 : o.pageNo;
    if (pno) { pageNumber(ctx, L.left, pno, s, { night: o.night }); pageNumber(ctx, L.right, pno + 1, s, { night: o.night }); }
    if (o.prev !== false) cornerMark(ctx, L.cornerPrev, 'prev', s, { night: o.night });
    if (o.next !== false || FL.overflow) cornerMark(ctx, L.cornerNext, 'next', s, { night: o.night });
    if (o.dogEar !== false) dogEar(ctx, L, { night: o.night });
    if (o.turn) pageTurn(ctx, L, o.turn, { night: o.night });
    ctx.restore();
    return Object.assign(L, { page: pageKind, flow: FL, tabRects, activeKey,
      placed: FL.placed, overflow: FL.overflow });
  }

  function canvasFor(w, h) {
    const c = (typeof document !== 'undefined') ? document.createElement('canvas') : new root.OffscreenCanvas(w, h);
    c.width = Math.max(1, Math.round(w)); c.height = Math.max(1, Math.round(h)); return c;
  }
  function render(o) {
    o = o || {};
    const F = (o.cols && o.lines) ? sizeFor(o.cols, o.lines, o.scale || 1)
      : fit(o.W || 560, o.H || 400, o);
    const c = canvasFor(F.w, F.h), ctx = c.getContext('2d');
    ctx.imageSmoothingEnabled = false;
    drawUnit(ctx, 0, 0, F.w, F.h, Object.assign({ align: 'topleft', fit: Object.assign({ clamped: false }, F) }, o));
    return c;
  }

  /* ============================== the boards ===============================
     Six taste calls, live tiles, drawn by the same functions the page calls. What ships is marked;
     the owner's calls stay open in contract().flagged. */
  const COVERS = {
    waxed: { label: 'WAXED DARK TEAL', ship: true, ramp: DAY.cover,
      note: 'Pass 1\u2019s cover, salt-bleached. Reads as the boats\u2019 own paintwork; disappears into a dark scene, which is why the ribbon exists.',
      prov: PROV.cover },
    oxblood: { label: 'OXBLOOD LEATHER', ship: false, ramp: ['#3f1d16', '#5a2a20', '#743629', '#8e4534'],
      note: 'The ribbon ramp taken up to the cover. Warmest and most book-like, and the only option that reads instantly against water.',
      prov: PROV.ribbon },
    canvas: { label: 'OILED CANVAS', ship: false, ramp: ['#5c5140', '#948468', '#b09e80', '#c9b899'],
      note: 'Oat cloth: a chandlery ledger, not a diary. Palest of the three, so the closed icon holds up at 3 \u00d7 5 px.',
      prov: PROV.cloth },
  };
  const COVER_ORDER = ['waxed', 'oxblood', 'canvas'];
  const PLAYER_TEXT = {
    clean: { label: 'THE BUBBLE KIT\u2019S FACE', ship: true,
      note: 'The page is set in exactly the face the bubbles are set in \u2014 same glyphs, same cell, same weight. SHIPS (owner, 2026-08-17): the roughened hand was unreadable at page sizes, and knowledge pages are the longest running text in the game. What makes the book HERS is the furniture \u2014 her pencil rules, her wobbling boxes, her strikes \u2014 not damaged letters.' },
    rough: { label: 'THE ROUGHENED HAND', ship: false,
      note: 'The same glyphs with baseline wobble, dropped strokes, crowding inside the cell and per-line drift. Reads as written rather than typeset and holds up enlarged \u2014 and at one cell it costs legibility, which is why it is a flag and not the default. Still drawn and still measured: probeHand() runs against this mode.' },
  };
  const PLAYER_TEXT_ORDER = ['clean', 'rough'];

  function boards(o) {
    o = o || {};
    const night = !!o.night, P = night ? NIGHT : DAY;
    const paper = (ctx, x, y, w, h) => { ctx.fillStyle = P.page; ctx.fillRect(x, y, w, h); };
    const fragment = (ctx, x, y, s, over) => {
      const z = sizeFor(14, 4, s);
      drawUnit(ctx, x, y, z.w, z.h, Object.assign({ align: 'topleft', scale: s, minCols: 14, minLines: 4, night,
        page: 'quests', heads: ['', ''], pageNo: 0, ribbon: false, dogEar: false, prev: false, next: false,
        tabs: [{ key: 'active', label: 'OPEN', page: 'quests', style: 'card' }],
        blocks: [{ kind: 'quest', title: 'Mend the north traps', steps: [
          { text: 'twine from Ella', state: 'done' }, { text: 'set them by dusk', state: 'current' }] }] }, over));
      return { w: z.w, h: z.h };
    };
    return [
      { key: 'cover', label: 'THE COVER', pick: o.cover || 'waxed',
        why: 'the closed book is also the icon gameplay mounts, so this colour has to read at 3 \u00d7 5 px AND against water',
        tiles: COVER_ORDER.map(k => ({ key: k, label: COVERS[k].label, ship: COVERS[k].ship, note: COVERS[k].note,
          w: 46, h: 30, draw: (ctx, x, y, s) => {
            s = s || 1; const R = COVERS[k].ramp;
            ctx.fillStyle = R[1]; ctx.fillRect(x, y, 46 * s, 30 * s);
            ctx.fillStyle = R[3]; ctx.fillRect(x, y, 46 * s, s);
            ctx.fillStyle = R[0]; ctx.fillRect(x, y + 29 * s, 46 * s, s);
            ctx.fillStyle = P.edge; ctx.fillRect(x + 41 * s, y + 2 * s, 4 * s, 26 * s);
            ctx.fillStyle = P.ribbon; ctx.fillRect(x + 34 * s, y, 3 * s, 33 * s);
            ctx.fillStyle = R[2]; ctx.fillRect(x + 4 * s, y + 4 * s, 30 * s, s);
          } })) },
      { key: 'stock', label: 'PAPER STOCK', pick: o.stock || 'ruled',
        why: 'the printed leaves and her own leaves must not be the same drawing, or My Notes reads as another printed tab',
        tiles: ['ruled', 'squared', 'mine', 'plain'].map(k => ({ key: k, label: STOCKS[k].label, ship: STOCKS[k].ship,
          note: STOCKS[k].note, w: 66, h: 34, draw: (ctx, x, y, s) => {
            s = s || 1; paper(ctx, x, y, 66 * s, 34 * s);
            const fake = { x, y, w: 66 * s, h: 34 * s, marginX: x + GEO.ruleInset * s, i: 0,
              rows: [0, 1, 2].map(i => ({ i, rule: y + (7 + i * CELL.pitch) * s })) };
            drawStock(ctx, fake, k, { s, night });
          } })) },
      { key: 'tab', label: 'TAB STYLE', pick: o.tab || 'card',
        why: 'the four families are permanent furniture; knowledge sub-tabs arrive as the player learns, so they may want to look added-later',
        tiles: TAB_STYLE_ORDER.map(k => ({ key: k, label: TAB_STYLES[k].label, ship: TAB_STYLES[k].ship,
          note: TAB_STYLES[k].note, w: 30, h: GEO.tabH + 2, draw: (ctx, x, y, s) => {
            s = s || 1;
            tabStub(ctx, { i: 0, x: x + 20 * s, y: y + s, w: GEO.tabCol * s, h: GEO.tabH * s, cols: 2,
              label: { x: x + 20 * s + GEO.tabPad * s, y: y + 3 * s } },
              { label: 'AC', style: k }, k === 'card' ? 'on' : 'off', { s, night });
          } })) },
      { key: 'box', label: 'THE CHECKBOX', pick: o.box || 'pencil',
        why: 'drawn once and reused by quest steps AND her own lists, so it has to survive both voices',
        tiles: BOX_ORDER.map(k => ({ key: k, label: BOXES[k].label, ship: BOXES[k].ship, note: BOXES[k].note,
          w: 22, h: GEO.box + 2, draw: (ctx, x, y, s) => {
            s = s || 1; paper(ctx, x, y, 22 * s, (GEO.box + 2) * s);
            tickBox(ctx, x + s, y + s, s, { night, style: k, seed: 300 });
            tickBox(ctx, x + 12 * s, y + s, s, { night, style: k, seed: 340 });
            tickMark(ctx, x + 12 * s, y + s, s, { night, seed: 360 });
          } })) },
      { key: 'current', label: 'THE CURRENT STEP', pick: o.current || 'arrow',
        why: 'the one row the eye must find first \u2014 and the one place the bubble kit\u2019s gold might be spent twice',
        tiles: CURRENT_ORDER.map(k => ({ key: k, label: CURRENTS[k].label, ship: CURRENTS[k].ship, note: CURRENTS[k].note,
          w: sizeFor(14, 4, 1).w, h: sizeFor(14, 4, 1).h,
          draw: (ctx, x, y, s) => fragment(ctx, x, y, s || 1, { current: k }) })) },
      { key: 'done', label: 'A FINISHED QUEST', pick: o.done || 'strike',
        why: 'the Done tab is a page of these, so whatever this is, it is what that tab looks like',
        tiles: DONE_ORDER.map(k => ({ key: k, label: DONES[k].label, ship: DONES[k].ship, note: DONES[k].note,
          w: sizeFor(14, 4, 1).w, h: sizeFor(14, 4, 1).h,
          draw: (ctx, x, y, s) => fragment(ctx, x, y, s || 1, { done: k,
            blocks: [{ kind: 'quest', title: 'Bait for Ella', done: true,
              steps: [{ text: 'two totes, salted', state: 'done' }, { text: 'left at the shed', state: 'done' }] }] }) })) },
      { key: 'player', label: 'THE FACE ON THE PAGE', pick: o.player || 'clean',
        why: 'the shipped answer is the bubble kit\u2019s face, clean \u2014 the roughened hand stays drawn and measured so the call can be re-made against real pages rather than a screenshot',
        tiles: PLAYER_TEXT_ORDER.map(k => ({ key: k, label: PLAYER_TEXT[k].label, ship: PLAYER_TEXT[k].ship,
          note: PLAYER_TEXT[k].note, w: textW('rope, if the store has it', 1) + 4, h: CELL.box + 4,
          draw: (ctx, x, y, s) => {
            s = s || 1; const str = 'rope, if the store has it';
            paper(ctx, x, y, (textW(str, 1) + 4) * s, (CELL.box + 4) * s);
            hand(ctx, str, x + 2 * s, y + 2 * s, s, P.pencil, P.pencilLo, 4400,
              { press: 0.8, rough: k === 'rough' ? 1 : 0 });
          } })) },
    ];
  }

  /* ==================== the ladder, generated ============================== */
  function metricsTable(o) {
    o = o || {};
    const ladder = [];
    for (const cols of (o.cols || [16, 20, 24, 30, 40]))
      for (const lines of (o.lines || [6, 10, 16, 22])) {
        const z = sizeFor(cols, lines, 1);
        ladder.push({ cols, lines, titleCols: z.titleCols, spreadPx: [z.w, z.h],
          metres: [+(z.w / PPU).toFixed(2), +(z.h / PPU).toFixed(2)],
          pagePx: [pageW(cols), pageH(lines)], cellsPerSpread: 2 * cols * lines,
          atScale2: [z.w * 2, z.h * 2], atScale3: [z.w * 3, z.h * 3] });
      }
    return {
      cell: Object.assign({}, CELL, { glyph: [CELL.w, CELL.box],
        note: 'IMPORTED from BubbleKit.CELL. advance is EXACT; the crowding that reads as an uneven ' +
          'hand is a ' + CELL.slack + ' px nudge inside the cell (glyph ' + CELL.w + ' wide in a ' +
          CELL.adv + ' wide cell). The printed rule sits at row ' + CELL.rule + ' of the line box, one ' +
          'under the baseline, so descenders cross it as they do on real ruled stock.' }),
      shared: { with: 'dialogue-bubble-kit', what: 'CELL and FONT are imported, not copied — a player ' +
        'reads a bubble and a page in the same voice because it is the same glyph set',
        probe: 'probeType() fails if the two ever diverge' },
      case: { body: 'sentence case — knowledge pages are the longest running text in the game and mixed case is what makes them readable',
        labels: 'CAPS on tab chips, running heads and the DONE stamp — those are labels, not voice',
        was: 'pass 2 set the whole book in CAPS on a 4 \u00d7 8 caps-only cell' },
      cost: { cellWas: '4 \u00d7 8 (notebookRig2)', cellIs: CELL.adv + ' \u00d7 ' + CELL.line,
        example: 'a 22-col 16-line spread: 209 \u00d7 143 px \u2192 ' + sizeFor(22, 16, 1).w + ' \u00d7 ' + sizeFor(22, 16, 1).h + ' px',
        note: 'taken ONCE, before the freeze, so every number here is already the mixed-case number' },
      wrap: 'charsPerLine = cols = floor((textLaneWidth + s) / (' + CELL.adv + '\u00b7s)) · NotebookKit.colsFor(px, s) · wrap() is BubbleKit.wrap, shared',
      fill: 'one cell per character, left to right, line by line; layout().leaves[i].rows[] gives each ruled line its cell origin',
      lanes: { padL: GEO.padL, cursor: GEO.cursorLane, box: GEO.boxLane, boxSize: GEO.box, padR: GEO.padR,
        marginRuleAt: GEO.ruleInset,
        note: 'a TITLE starts at titleX and runs titleCols wide; a STEP box sits at boxX and its text at ' +
          'textX, cols wide. titleCols = cols + boxLane/adv = cols + ' + (GEO.boxLane / CELL.adv) + ' exactly — ' +
          'the box lane is two cells for that reason and no other.' },
      formula: { pageW: 'padL + cursorLane + boxLane + (cols \u00d7 ' + CELL.adv + ' \u2212 1) + padR = ' + CELL.adv + 'c + ' + pageW(0),
        pageH: 'headH + lines \u00d7 ' + CELL.pitch + ' + padB = ' + CELL.pitch + 'L + ' + pageH(0),
        spreadW: '2 \u00d7 pageW + gutter + 2 \u00d7 (coverPad + edge) + tabCol = ' + (2 * CELL.adv) + 'c + ' + totalW(0),
        spreadH: 'pageH + 2 \u00d7 (coverPad + edge) = ' + CELL.pitch + 'L + ' + totalH(0),
        step: 'one more line = +' + CELL.pitch + ' px of height; one more column = +' + (2 * CELL.adv) + ' px of width (both leaves)' },
      min: { cols: GEO.minCols, lines: GEO.minLines, px: [totalW(GEO.minCols), totalH(GEO.minLines)] },
      max: { cols: GEO.maxCols, lines: GEO.maxLines, px: [totalW(GEO.maxCols), totalH(GEO.maxLines)],
        note: 'past ' + GEO.maxCols + ' columns the line wants the facing leaf, not a wider book' },
      ladder, geo: Object.assign({}, GEO),
      scaleRule: 'INTEGER scale only — fit() picks it. A fractional scale puts the hand on a half cell and the wobble stops reading as a hand and starts reading as a bug.',
      caret: { w: 1, h: CELL.baseline + 1, cell: [CELL.adv, CELL.pitch] },
      pages: PAGE_ORDER.map(k => Object.assign({ key: k }, PAGES[k])),
    };
  }

  /* ==================== the invariant, as arithmetic ======================= */
  function readBudget(o) {
    o = o || {};
    const out = o.out || [1920, 1080], tiers = o.tiers || [2, 3, 4];
    const share = o.share == null ? 0.86 : o.share;
    const cases = tiers.map(z => {
      const world = [Math.floor(out[0] / z), Math.floor(out[1] / z)];
      const room = [Math.floor(world[0] * share), Math.floor(world[1] * share)];
      const F = fit(room[0], room[1], o);
      const areaPct = +(F.w * F.h / (world[0] * world[1]) * 100).toFixed(1);
      return { zoom: z, worldPx: world, roomPx: room, scale: F.s, cols: F.cols, lines: F.lines,
        spreadPx: [F.w, F.h], cellsPerSpread: 2 * F.cols * F.lines,
        pctOfScreen: [+(F.w / world[0] * 100).toFixed(1), +(F.h / world[1] * 100).toFixed(1)],
        areaPct, worldVisiblePct: +(100 - areaPct).toFixed(1), clamped: F.clamped,
        holds: !F.clamped && F.s >= 1 && F.cols >= GEO.minCols && F.lines >= GEO.minLines
          && F.w <= room[0] && F.h <= room[1],
        closest: z === Math.max.apply(null, tiers) };
    });
    const closest = cases.filter(c => c.closest)[0] || cases[cases.length - 1];
    return {
      rule: 'the book is READ, not glanced at: the hand never draws below one cell (scale 1), the spread is placed at an INTEGER cell scale, and it is a held object rather than a full-screen menu.',
      shareRule: 'the spread takes at most ' + Math.round(share * 100) + '% of the world rect in each axis. That is ONE implementation of "it is still an object in a scene", not the rule — pass share.',
      occupancyAnswer: 'at the CLOSEST tier (' + closest.zoom + '\u00d7) the open book occupies ' + closest.areaPct +
        '% of the screen (' + closest.spreadPx.join(' \u00d7 ') + ' px of ' + closest.worldPx.join(' \u00d7 ') +
        '), leaving ' + closest.worldVisiblePct + '% of the world visible. It DOMINATES, by design — it is held ' +
        'up in front of her — and the presenter now chooses that knowing the fraction.',
      typeRule: 'a fractional scale is the failure mode this replaces: derive u = width / anything and the hand lands on half cells.',
      assumption: 'output ' + out[0] + '\u00d7' + out[1] + ', per-tier pixel-perfect zoom (ruling #327). If the shipped tier ladder differs, pass tiers[].',
      cases, closest, worst: cases.reduce((a, b) => (b.cellsPerSpread < a.cellsPerSpread ? b : a), cases[0]),
      copyBudget: 'THE NUMBER THE WRITING LANE NEEDS: at the closest tier a step line holds ' + closest.cols +
        ' characters and a title line ' + titleColsFor(closest.cols) + ', with ' + closest.lines +
        ' ruled lines a leaf. A step longer than that is not clipped — it wraps and eats a line, and a ' +
        'quest whose block outgrows a leaf moves whole to the facing leaf. Write to ' + closest.cols +
        ' and nothing surprises anybody.',
      holds: cases.every(c => c.holds),
      clampNote: 'never clamp a returned rect yourself. Pass the room in and read `clamped`, or the flag becomes a lie about what is on screen.',
    };
  }

  /* ==================== the probes: four claims, runnable =================== */
  function inkOf(cv) {
    const d = cv.getContext('2d').getImageData(0, 0, cv.width, cv.height).data, set = [];
    for (let i = 3; i < d.length; i += 4) set.push(d[i] > 8 ? 1 : 0);
    return set;
  }
  /* CLAIM 1: one type system. The cell IS the bubble kit's cell, and every glyph the book can draw
     is a glyph the bubble can draw. A copied face drifts; an imported one cannot. */
  function probeType() {
    const keys = ['adv', 'w', 'box', 'cap', 'xh', 'desc', 'baseline', 'line', 'leading'];
    const bad = keys.filter(k => CELL[k] !== BK.CELL[k]);
    const sameFont = FONT === BK.FONT;
    const glyphs = Object.keys(FONT).length;
    return { pass: bad.length === 0 && sameFont, sameFontObject: sameFont, mismatched: bad, glyphs,
      cell: [CELL.adv, CELL.line], bubbleCell: [BK.CELL.adv, BK.CELL.line],
      lowercase: 'abcdefghijklmnopqrstuvwxyz'.split('').every(c => !!FONT[c]),
      claim: 'the notebook\u2019s cell and glyph set ARE the bubble kit\u2019s, by reference — one voice across the talking UI and the book UI, enforced rather than promised.',
      fails: bad.length ? 'cell mismatch on ' + bad.join(', ') : (sameFont ? null : 'the FONT object was copied instead of imported — it will drift') };
  }
  /* CLAIM 2: the hand is irregular AND cell-exact. */
  function probeHand(o) {
    o = o || {};
    const str = o.text || 'The punt is due back on the tide';
    const s = o.s || 1, seed = o.seed == null ? 3001 : o.seed;
    const W = textW(str, s) + 8 * s, H = (CELL.box + 8) * s;
    const A = canvasFor(W, H), B = canvasFor(W, H);
    const h = hand(A.getContext('2d'), str, 2 * s, 4 * s, s, '#ffffff', '#ffffff', seed,
      Object.assign({ rough: 1 }, o.opt));
    text(B.getContext('2d'), str, 2 * s, 4 * s, s, '#ffffff');
    const a = inkOf(A), b = inkOf(B);
    let diff = 0, inkA = 0, inkB = 0;
    for (let i = 0; i < a.length; i++) { inkA += a[i]; inkB += b[i]; if (a[i] !== b[i]) diff++; }
    const cellsExact = h.origins.every((g, i) => g.x === 2 * s + i * CELL.adv * s);
    const wobble = h.origins.filter(g => g.dy !== 0).length, crowd = h.origins.filter(g => g.dx !== 0).length;
    const diffPct = +(diff / Math.max(1, inkB) * 100).toFixed(1);
    const wobblePct = +(wobble / h.origins.length * 100).toFixed(1);
    return { pass: cellsExact && diffPct >= 8 && wobblePct >= 25, cellsExact, glyphs: h.origins.length,
      diffPct, wobblePct, crowdedGlyphs: crowd, droppedPx: Math.max(0, inkB - inkA), inkHand: inkA, inkPrint: inkB,
      claim: 'the ROUGHENED mode (flagged OFF) differs from the clean face pixel by pixel — wobble, crowding, dropped strokes — while every cell origin stays exactly x + i\u00b7' + CELL.adv + '\u00b7s. The page ships clean, so what this proves is that switching roughness back on cannot break the arithmetic.',
      fails: cellsExact ? (diffPct < 8 ? 'the hand has stopped being a hand: it is within ' + diffPct + '% of print' : null)
        : 'a cell origin moved — wrap, caret, fill and hit-boxes are all now lies' };
  }
  /* CLAIM 3: the write-on never redraws what it already wrote. */
  function probeWrite(o) {
    o = o || {};
    const str = o.text || 'mend the north traps', s = o.s || 1, seed = o.seed == null ? 3101 : o.seed;
    const W = textW(str, s) + 8 * s, H = (CELL.box + 8) * s;
    let prev = null, firstBreak = -1, grew = 0;
    for (let k = 0; k <= str.length; k++) {
      const cv = canvasFor(W, H);
      hand(cv.getContext('2d'), str, 2 * s, 4 * s, s, '#ffffff', '#ffffff', seed, { limit: k });
      const cur = inkOf(cv);
      if (prev) { let lost = 0, gain = 0;
        for (let i = 0; i < cur.length; i++) { if (prev[i] && !cur[i]) lost++; if (!prev[i] && cur[i]) gain++; }
        if (lost && firstBreak < 0) firstBreak = k;
        if (gain) grew++; }
      prev = cur;
    }
    /* PART 2, THE MARKS. A part-written block carries exactly ONE caret and no marks on text that is
       not there yet; a finished one carries its strike and tick and no caret. Counted through the
       real draw calls, because both failures are invisible in a screenshot of the settled page. */
    const marks = (fill) => {
      const cv = canvasFor(320, 120), c2 = cv.getContext('2d');
      CALLS = {};
      drawUnit(c2, 0, 0, 320, 120, { align: 'topleft', scale: 1, minCols: 16, minLines: 8,
        page: 'quests', heads: ['', ''], pageNo: 0, ribbon: false, dogEar: false, prev: false, next: false,
        done: 'strike', blocks: [{ kind: 'quest', title: 'Bait for Ella', done: true, fill,
          steps: [{ text: 'two totes, salted', state: 'done' }, { text: 'left at the shed', state: 'done' }] }] });
      const got = CALLS; CALLS = null; return got;
    };
    const part = marks(9), whole = marks(400);
    const marksOk = (part.caret || 0) === 1 && !(part.strike || 0) && !(part.tick || 0)
      && !(whole.caret || 0) && (whole.strike || 0) >= 1 && (whole.tick || 0) >= 1;
    return { pass: firstBreak < 0 && grew > 0 && marksOk, steps: str.length + 1, firstBreak,
      growingSteps: grew, marksOk, midWrite: part, finished: whole,
      claim: 'ink at fill k is a subset of ink at fill k+1; a part-written block carries exactly ONE caret and no strike or tick on text that has not been written yet; a finished one carries its marks and no caret.',
      fails: firstBreak >= 0 ? 'ink disappeared between fill ' + (firstBreak - 1) + ' and ' + firstBreak + ' — the rng stream depends on the limit'
        : (marksOk ? null : 'mid-write drew ' + (part.caret || 0) + ' caret(s), ' + (part.strike || 0) + ' strike(s), ' +
          (part.tick || 0) + ' tick(s); finished drew ' + (whole.caret || 0) + ' caret(s), ' + (whole.strike || 0) + ' strike(s), ' + (whole.tick || 0) + ' tick(s)') };
  }
  /* CLAIM 4: overflow is more pages, and the arithmetic is exact. Nothing is silently dropped, no
     quest straddles the gutter that could have fitted a leaf, and placed + overflow = given. */
  function probeFlow(o) {
    o = o || {};
    const L = layout(0, 0, totalW(GEO.minCols), totalH(8), { align: 'topleft', scale: 1, minLines: 8 });
    const mk = (n, steps) => ({ kind: 'quest', title: 'Quest number ' + n + ' with a long enough title to wrap',
      context: 'asked by the harbourmaster', steps: Array.from({ length: steps }, (_, i) => ({ text: 'step ' + (i + 1) + ' of this one', state: i ? 'todo' : 'current' })) });
    const blocks = [mk(1, 3), mk(2, 4), mk(3, 2), mk(4, 6), mk(5, 3)];
    const F = flow(blocks, L, {});
    const straddle = F.placed.filter(p => {
      const leaves = new Set(p.rows.map(r => r.r.leaf));
      const rowsNeeded = p.rows.length;
      return leaves.size > 1 && rowsNeeded <= L.lines;
    });
    const exact = F.placed.length + F.overflow === blocks.length;
    const withinPage = F.placed.every(p => p.rows.every(r => r.r.i < L.lines));
    return { pass: exact && straddle.length === 0 && withinPage,
      given: blocks.length, placed: F.placed.length, overflow: F.overflow,
      rowsUsed: F.used, capacity: F.capacity, straddled: straddle.length,
      claim: 'blocks are kept whole across the gutter when they could fit a leaf, nothing is drawn past the last ruled line, and placed + overflow = given — so the pencil "n more" is a fact and the page turn is the only answer to a long list.',
      fails: exact ? (straddle.length ? straddle.length + ' quest(s) straddled the gutter that would have fitted one leaf' : (withinPage ? null : 'a row was placed past the last ruled line')) : 'placed + overflow \u2260 given: the count on the page is a lie' };
  }
  function probes() { return { type: probeType(), hand: probeHand(), write: probeWrite(), flow: probeFlow() }; }

  /* ==================== reference sheet, drawn by the rig =================== */
  const SHEET_W = 600;
  function sheetSpec(o) {
    o = o || {};
    const night = !!o.night, P = night ? NIGHT : DAY;
    const items = []; let x = 4, rowY = 30, rowH = 0;
    const place_ = (name, w, h, draw, note) => {
      const cell = Math.max(w, textW(name, 1), textW(w + 'x' + h, 1));
      if (x + cell > SHEET_W - 8) { x = 4; rowY += rowH + 26; rowH = 0; }
      items.push({ name, x, y: rowY, w, h, cell, draw, note });
      x += cell + 10; rowH = Math.max(rowH, h);
    };
    const paper = (c, X, Y, w, h) => { c.fillStyle = P.page; c.fillRect(X, Y, w, h); };
    const fakeLeaf = (X, Y, w, n) => ({ x: X, y: Y, w, h: 4 + n * CELL.pitch, i: 0, marginX: X + GEO.ruleInset,
      rows: Array.from({ length: n }, (_, i) => ({ i, rule: Y + 2 + CELL.rule + i * CELL.pitch })) });

    for (const k of ['ruled', 'squared', 'mine']) place_('stock.' + k, 70, 34,
      (c, X, Y) => { paper(c, X, Y, 70, 34); drawStock(c, fakeLeaf(X, Y, 70, 3), k, { s: 1, night }); },
      STOCKS[k].label + ' \u00b7 pitch ' + CELL.pitch + ', rule at row ' + CELL.rule);
    for (const k of BOX_ORDER) place_('box.' + k, GEO.box, GEO.box,
      (c, X, Y) => { paper(c, X - 1, Y - 1, GEO.box + 2, GEO.box + 2); tickBox(c, X, Y, 1, { night, style: k }); },
      BOXES[k].label + (BOXES[k].ship ? ' \u00b7 SHIPS' : ''));
    place_('box.ticked', GEO.box + 1, GEO.box + 1, (c, X, Y) => { paper(c, X - 1, Y - 1, GEO.box + 3, GEO.box + 3);
      tickBox(c, X, Y, 1, { night }); tickMark(c, X, Y, 1, { night }); }, 'done \u00b7 ticked, never emptied');
    for (const k of CURRENT_ORDER) place_('current.' + k, GEO.box + 2, GEO.box + 2,
      (c, X, Y) => { paper(c, X - 1, Y - 1, GEO.box + 4, GEO.box + 4); currentMark(c, { x: X, y: Y + 1, w: GEO.box, h: GEO.box }, 1, { night, style: k }); },
      CURRENTS[k].label + (CURRENTS[k].ship ? ' \u00b7 SHIPS' : ''));
    place_('caret', 1, CELL.baseline + 1, (c, X, Y) => { paper(c, X - 2, Y - 1, 5, CELL.baseline + 3); caret(c, X, Y, 1, { night }); },
      'the cell the next character lands in');
    place_('stamp.done', textW('DONE', 1) + 6, CELL.box + 2,
      (c, X, Y) => { paper(c, X - 1, Y - 1, textW('DONE', 1) + 8, CELL.box + 4); doneStamp(c, X, Y, 1, { night }); },
      'harbour stamp \u00b7 on the board, not shipped');
    for (const k of CURSOR_ORDER) { const C = cursorSpec(k);
      place_('cursor.' + k, C.w, C.h, (c, X, Y) => { paper(c, X - 1, Y - 1, C.w + 2, C.h + 2); cursor(c, X, Y, k, { night }); },
        'BubbleKit \u00b7 point ' + C.point.join(',') + (CURSORS[k].ship ? ' \u00b7 SHIPS' : '')); }
    for (const k of TAB_STYLE_ORDER) place_('tab.' + k, GEO.tabCol + 9, GEO.tabH,
      (c, X, Y) => tabStub(c, { i: 0, x: X + 9, y: Y, w: GEO.tabCol, h: GEO.tabH, cols: 2, label: { x: X + 9 + GEO.tabPad, y: Y + 2 } },
        { label: 'AC', style: k }, k === 'card' ? 'on' : 'off', { s: 1, night }),
      TAB_STYLES[k].label + (TAB_STYLES[k].ship ? ' \u00b7 SHIPS' : ''));
    place_('mark.new', 22, GEO.pitch, (c, X, Y) => { paper(c, X, Y, 22, GEO.pitch);
      newNoteMark(c, { i: 0, band: { x: X, y: Y, w: 22, h: GEO.pitch }, box: { x: X + 13, y: Y + 1 }, cursor: { x: X + 1, y: Y + 1 } }, 1, { night }); },
      'a new line on her page');
    place_('mark.next', 5, 7, (c, X, Y) => { paper(c, X - 1, Y - 1, 7, 9); cornerMark(c, { x: X, y: Y, w: 5, h: 6 }, 'next', 1, { night }); }, 'page corner \u00b7 next');
    place_('mark.prev', 5, 7, (c, X, Y) => { paper(c, X - 1, Y - 1, 7, 9); cornerMark(c, { x: X, y: Y, w: 5, h: 6 }, 'prev', 1, { night }); }, 'page corner \u00b7 prev');
    place_('dogear', 9, 9, (c, X, Y) => { paper(c, X, Y, 9, 9); dogEar(c, { s: 1, dogEar: { x: X, y: Y, w: 9, h: 9 } }, { night }); }, 'the corner she turned down');
    place_('ribbon', GEO.ribbonW + 2, 26, (c, X, Y) => { c.fillStyle = P.cover[1]; c.fillRect(X, Y, GEO.ribbonW + 2, 26);
      ribbon(c, { s: 1, ribbon: { x: X + 1, y: Y, w: GEO.ribbonW, h: 22 } }, { night }); }, 'bookmark \u00b7 where she left off');
    for (const k of HL_ORDER) place_('select.' + k, 40, GEO.pitch, (c, X, Y) => { paper(c, X, Y, 40, GEO.pitch);
      highlight(c, { x: X, y: Y, w: 40, h: GEO.pitch }, k, { s: 1, night });
      hand(c, 'this row', X + 4, Y + 1, 1, k === 'pill' ? P.ink : P.ink, P.inkLo, 4500, { press: 0.9 }); },
      HIGHLIGHTS[k].label + (HIGHLIGHTS[k].ship ? ' \u00b7 SHIPS' : ''));
    place_('type.clean', textW('The wind backed round', 1), CELL.box,
      (c, X, Y) => { paper(c, X - 1, Y - 1, textW('The wind backed round', 1) + 2, CELL.box + 2);
        hand(c, 'The wind backed round', X, Y, 1, P.ink, P.inkLo, 4001, {}); },
      'THE PAGE FACE \u00b7 cell ' + CELL.adv + 'x' + CELL.line + ' \u00b7 BubbleKit glyphs, pixel for pixel');
    place_('type.pencil', textW('if the store has it', 1), CELL.box,
      (c, X, Y) => { paper(c, X - 1, Y - 1, textW('if the store has it', 1) + 2, CELL.box + 2);
        hand(c, 'if the store has it', X, Y, 1, P.pencil, P.pencilLo, 4003, {}); },
      'her own lines \u00b7 the SAME face in pencil weight \u2014 ink and pencil differ by COLOUR, never by holes');
    place_('type.rough', textW('the roughened hand', 1), CELL.box,
      (c, X, Y) => { paper(c, X - 1, Y - 1, textW('the roughened hand', 1) + 2, CELL.box + 2);
        hand(c, 'the roughened hand', X, Y, 1, P.ink, P.inkLo, 4005, { rough: 1, press: 0.9 }); },
      'the alternate \u00b7 DOES NOT SHIP \u00b7 hand(.., {rough:1}), on the board and measured by probeHand()');
    /* the four page kinds, each a real spread drawn by the real function */
    for (const k of PAGE_ORDER) {
      const z = sizeFor(GEO.minCols, 6, 1);
      place_('page.' + k, z.w, z.h, (c, X, Y) => {
        drawUnit(c, X, Y, z.w, z.h, { align: 'topleft', scale: 1, minLines: 6, night, page: k,
          heads: [PAGES[k].label, ''], pageNo: 12, blocks: SAMPLE[k] });
      }, PAGES[k].label + ' \u00b7 ' + PAGES[k].note);
    }
    for (let f = 1; f <= TURN_FRAMES; f++) {
      const z = sizeFor(GEO.minCols, 6, 1);
      place_('turn.' + f, z.w, z.h, (c, X, Y) => {
        drawUnit(c, X, Y, z.w, z.h, { align: 'topleft', scale: 1, minLines: 6, night, page: 'quests',
          heads: ['', ''], pageNo: 12, blocks: SAMPLE.quests, turn: f });
      }, 'page turn frame ' + f + ' of ' + TURN_FRAMES);
    }
    return { items, w: SHEET_W, h: rowY + rowH + 34, night };
  }
  function sheet(o) {
    const spec = sheetSpec(o), cv = canvasFor(spec.w, spec.h), ctx = cv.getContext('2d');
    ctx.imageSmoothingEnabled = false;
    ctx.fillStyle = '#141a1e'; ctx.fillRect(0, 0, spec.w, spec.h);
    text(ctx, 'notebook kit pass 3 - cell ' + CELL.adv + 'x' + CELL.line + ' imported from the bubble kit - 32 px = 1 m',
      4, 5, 1, '#7fd6c9');
    for (const it of spec.items) {
      ctx.fillStyle = '#1d262b'; ctx.fillRect(it.x - 1, it.y - 1, it.w + 2, it.h + 2);
      it.draw(ctx, it.x, it.y);
      text(ctx, it.name, it.x, it.y + it.h + 4, 1, '#55666d');
      text(ctx, it.w + 'x' + it.h, it.x, it.y + it.h + 14, 1, '#3f4d53');
    }
    return cv;
  }

  /* ==================== sample data (HARNESS + SHEET ONLY) =================
     Placeholder-length data so the sheet and the harness can show a real spread. It is not part of
     the contract, nothing in the game reads it, and every string here is caller data in the game. */
  const SAMPLE = {
    quests: [
      { kind: 'quest', title: 'Mend the north traps', context: 'Ella, at the co-op',
        steps: [{ text: 'twine from the store', state: 'done' }, { text: 'four traps by dusk', state: 'current' },
          { text: 'take them out on the ebb', state: 'todo' }] },
      { kind: 'quest', title: 'The berth fee', context: 'harbourmaster, by day 20',
        steps: [{ text: 'pay before the fuel', state: 'todo' }] },
    ],
    done: [
      { kind: 'quest', title: 'Bait for Ella', done: true,
        steps: [{ text: 'two totes, salted', state: 'done' }, { text: 'left at the shed', state: 'done' }] },
      { kind: 'quest', title: 'Row the punt back', done: true, steps: [{ text: 'on the evening tide', state: 'done' }] },
    ],
    knowledge: [
      { kind: 'section', head: 'THE FLATS', body: 'They dry two days after a new moon, and the clams sit a hand under the surface where the water still runs.' },
      { kind: 'section', head: 'A HERRING NET', body: 'Wants forty fathom of cork line, no less. Moss says less and it fishes like a fence.', slot: true },
    ],
    notes: [
      { kind: 'note', text: 'rope, if the store has it', box: true, state: 'todo' },
      { kind: 'note', text: 'paint for the punt', box: true, state: 'done' },
      { kind: 'note', text: 'ask about the old mooring', box: true, state: 'current' },
      { kind: 'note', text: 'The tide was wrong all week and nobody said why.', gap: true },
      { kind: 'newnote' },
    ],
  };

  /* ============================== contract ================================= */
  function contract(o) {
    const MT = metricsTable();
    const BD = boards();
    return {
      kit: 'notebook-kit', version: VERSION, rig: 'notebookRig3.js', global: 'NotebookKit',
      requires: ['Art/dialogueBubbleRig.js (BubbleKit — the FACE and the CURSORS)', 'Art/isoSolid.js (the closed object only)'],
      supersedes: 'notebookRig2.js (NotebookRig2) — pass 2. Both may be loaded during the wiring PR.',
      generated: 'read out of Art/notebookRig3.js at its defaults — regenerate, never hand-edit',
      purpose: 'THE PLAYER\u2019S MAIN UI SURFACE. Active quests with their steps, finished quests, every knowledge page, and the lists she writes herself. No menus — a physical book.',
      doctrine: {
        law: 'knowledge lives in things and people — so the notebook is a thing she writes in, and it must never read as a menu or a panel',
        consequence: 'the page is set in the SHARED face, clean — the bubble kit’s glyphs pixel for pixel, no second face in the game and no roughened variant on the page by default. What carries “she wrote this” is the FURNITURE: her pencil-ruled leaves, her wobbling checkboxes, her strikes, pencil weight for her own lines, and the write-on laying a line down a cell at a time. Damaged letters were tried in passes 1–2 and reversed — they cost legibility, which is the one thing a knowledge page cannot spend.',
        noBakedText: 'NOTHING in this kit contains a word of game text. Titles, steps, knowledge lines, tab labels and notes are caller data; what ships is furniture with measured rects. The strings in NotebookKit.SAMPLE exist for the harness and the reference sheet and are not part of this contract.',
        object: 'never chrome, never a panel: paper, ink, pencil, tabs, a ribbon. Overflow is MORE PAGES — pageTurn() has the frames and there is no scrollbar anywhere in the kit.',
      },
      grid: { ppu: PPU, unit: '32 px = 1 m', declared: 'THIS KIT IS AUTHORED AT 32 px/m — the assets grid, same as every shipped rig and the bubble kit. 24 px/m is the camera-side number and authoring there would put UI pixels on a different pitch from world pixels.', scaleRule: MT.scaleRule },
      type: MT,
      finding: {
        pass1: 'true cursive does not survive this cap height with no anti-aliasing — the joins fill and the line greys out. An upright hand-set letterform with baseline wobble, dropped pixels and per-line drift reads as written and stays legible enlarged.',
        pass2: 'the four irregularities are not equal. Wobble, dropped pixels and drift cost nothing but paper; uneven ADVANCE costs the arithmetic — no wrap, no caret, no fill, no hit-box. The irregularity moved INSIDE the cell.',
        pass3: 'the face itself was the last thing left to share. It is now imported from the bubble kit rather than declared here, so "one voice" is a reference, not a promise — and mixed case arrived with it, which is what knowledge pages needed. The same pass REVERSED THE ROUGHENING: the page ships clean (owner, 2026-08-17), because wobble and dropped strokes at one cell cost more legibility than they buy character. probeHand() still runs against the roughened mode, so switching it back on cannot break the arithmetic.',
        test: 'probeType() · probeHand() · probeWrite() · probeFlow(). Run all four in the import PR and fail on them.',
      },
      book: {
        closed: 'NotebookIsoKit — the world prop AND the icon gameplay mounts wherever the open verb lives. ~3 \u00d7 5 px in the world, 8 facings, cover ramp from the cover board.',
        spread: 'drawUnit() — two leaves, a gutter, a fore-edge, tabs off the edge. layout().leaves[i] publishes each leaf\u2019s text inset rect (x, y, w, h) and its row grid.',
        turn: { frames: TURN_FRAMES, api: 'pageTurn(ctx, layout, frame 1..' + TURN_FRAMES + ')',
          note: 'a flip, not a scroll: lift off the right leaf, stand at the gutter, land on the left. ~70 ms a frame at 3 frames is a page turn you can read; slower reads as a cutscene.' },
        ribbon: 'ribbon(ctx, layout, {at}) — the bookmark, 3 px, cover-height plus a tail. It is also the only thing that reads on a dark cover, which is why the cover board matters.',
        wear: { api: 'wear(ctx, layout, level 1..2)', ships: 'CHEAP — nibbled fore-edge at level 1, plus a stain at level 2. Flagged not gold-plated: per-page wear, ring stains and a cracked spine are a separate ask.' },
        pageNumbers: 'pageNumber(ctx, leaf, n) — pencil, in the outside corner of each leaf. layout().leaves[i].pageNo is the rect.',
        cornerMarks: 'cornerMark(ctx, rect, "next"|"prev") — layout().cornerNext / cornerPrev. The dog-ear is decoration; these two are the affordance.',
      },
      tabs: {
        families: TAB_FAMILIES.map(f => ({ key: f.key, placeholderLabel: f.label, page: f.page, subs: f.subs || 0 })),
        ruling: 'FOUR families ship as furniture: Active · Done · Knowledge · My Notes. Knowledge takes N SUB-TABS and N is DATA — the kit ships the chip and the label rect, never a label.',
        mount: 'layout().tabMountX is the mount line (the fore-edge). layout().tabs[i] is chip + label rect + cursor rest, i in 0..' + (GEO.maxTabs - 1) + '.',
        chip: { w: GEO.tabCol + 9, h: GEO.tabH, pad: GEO.tabPad, gap: GEO.tabGap, top: GEO.tabTop, max: GEO.maxTabs },
        maxLabelChars: 'colsFor(tabW − pad − 1) — ' + colsFor(GEO.tabCol - GEO.tabPad - 1, 1) + ' characters at scale 1, clipped not shrunk. Sub-tabs lose one to the narrower stub.',
        states: ['on — pulled out, inked edge, lit top row', 'off — flush, unlit', 'cursor — the pointing hand rests on the stub at layout().tabs[i].cursor'],
        styles: TAB_STYLE_ORDER.map(k => ({ key: k, label: TAB_STYLES[k].label, ship: !!TAB_STYLES[k].ship, note: TAB_STYLES[k].note })),
      },
      quest: {
        anatomy: 'block = { kind:"quest", title, context?, steps:[{text, state}], done? }. blockRows() turns it into ruled lines; flow() places it.',
        titleRow: 'starts at leaf.titleX, titleCols wide, ink, the hand at press 0.9',
        contextLine: 'optional, same x, pencil at press 0.68 — the asker or the deadline, never a second title',
        step: { box: 'leaf.rows[i].box · ' + GEO.box + ' \u00d7 ' + GEO.box + ' px at scale 1', text: 'leaf.rows[i].text · cols wide',
          states: ['unchecked — the box, empty', 'checked — the box + a tick, and the text struck through', 'current — the CURRENT marker (see currents)'] },
        currents: CURRENT_ORDER.map(k => ({ key: k, label: CURRENTS[k].label, ship: !!CURRENTS[k].ship, note: CURRENTS[k].note })),
        done: DONE_ORDER.map(k => ({ key: k, label: DONES[k].label, ship: !!DONES[k].ship, note: DONES[k].note })),
        checkboxFamily: BOX_ORDER.map(k => ({ key: k, label: BOXES[k].label, ship: !!BOXES[k].ship, note: BOXES[k].note })),
        reuse: 'the checkbox family is drawn ONCE here and reused verbatim by the player\u2019s own note lines — same function, same rect, different ink weight',
        hitBoxes: 'flow().placed[k].hit is the whole block (a quest that wraps is one thing to click); .rows[j].r.hit is the ruled line; .rows[j].r.box is the box a mouse actually aims at',
      },
      knowledge: { header: 'block = { kind:"section", head, body, slot? } — head in ink with a ruled underline, body in the hand at titleCols',
        rules: 'the printed ruled stock carries the body; the facing leaf is squared so a diagram lands on the same lattice as the type',
        slot: { rect: 'the rows the section RESERVED (slotLines, default ' + GEO.plateMinLines + ') — the frame flows with the text, so knowledge copy can never run through it. layout().plate is the default full-leaf rect for a plate that is not part of a section.',
          api: 'plate(ctx, layout, {rect, caption, fill}) · block = { kind:"section", head, body, slot:true, slotLines?, slotCaption? }',
          note: 'ART RESERVES THE ROOM AND SAYS WHERE. The frame, the corner ticks and the caption ship now; what goes inside is a later content drop (knots, a fish plate, a rigged line).' } },
      notes: { stock: 'the leaves are stock "mine" — the same paper, ruled by HER in pencil, wobbling, with no printed margin rule. Distinct by drawing, not by a new colour.',
        lines: 'block = { kind:"note", text, box?, state? } — prose lines run titleCols wide with no box; task lines take the shared checkbox family at cols wide',
        newNote: 'block = { kind:"newnote" } — newNoteMark() draws a fresh box and a pencil cross in the margin lane; its rect is the affordance\u2019s hit-box',
        playerText: PLAYER_TEXT_ORDER.map(k => ({ key: k, label: PLAYER_TEXT[k].label, ship: !!PLAYER_TEXT[k].ship, note: PLAYER_TEXT[k].note })) },
      flow: { api: 'flow(blocks, layout) → { placed[], overflow, used[], capacity, filled }',
        rule: 'blocks are kept whole. A quest that fits a leaf but not the remaining rows moves to the facing leaf; a quest that fits neither is split, because the alternative is not drawing it.',
        overflow: 'what is left over is NOT drawn and NOT shrunk: overflowMark() writes a pencil "n more" with an arrow, and the answer is pageTurn(). There is no scroll state in this kit.',
        proof: 'probeFlow()' },
      selection: { shared: 'the pointing hand and the cove gold are BubbleKit.CURSORS / BubbleKit.GOLD, imported — one selection vocabulary across the talking UI and the book UI',
        cursors: CURSOR_ORDER.map(k => { const C = cursorSpec(k);
          return { key: k, label: CURSORS[k].label, ship: !!CURSORS[k].ship, w: C.w, h: C.h, point: C.point, note: CURSORS[k].note }; }),
        highlights: HL_ORDER.map(k => ({ key: k, label: HIGHLIGHTS[k].label, ship: !!HIGHLIGHTS[k].ship, note: HIGHLIGHTS[k].note })),
        rests: 'a row cursor rests at rows[i].cursor (the margin lane, ' + GEO.cursorLane + ' px, sized for the bubble kit\u2019s 10 px hand); a tab cursor rests at tabs[i].cursor',
        collision: 'GOLD MEANS SELECTED. If the CURRENT step also takes gold (currents.pill), one colour carries two meanings on one page — flagged, not decided.' },
      palette: { rule: 'no new colours (ADR 0015)', litStates: 'ONE. The lamplit palette is a preview of the in-engine day/night grade (bible §6), not a second authored state.',
        day: DAY, night: NIGHT, gold: { ramp: GOLD, prov: PROV.gold }, skin: { ramp: SKIN, prov: PROV.skin },
        covers: COVER_ORDER.map(k => ({ key: k, label: COVERS[k].label, ship: !!COVERS[k].ship, ramp: COVERS[k].ramp, prov: COVERS[k].prov })),
        provenance: PROV },
      boards: BD.map(b => ({ key: b.key, label: b.label, why: b.why, ships: (b.tiles.filter(t => t.ship)[0] || {}).key || null,
        tiles: b.tiles.map(t => ({ key: t.key, label: t.label, ship: !!t.ship, px: [t.w, t.h], note: t.note })) })),
      readBudget: readBudget(o),
      probes: probes(),
      sheet: sheetSpec().items.map(i => ({ piece: i.name, px: [i.w, i.h], crop: [i.x, i.y, i.w, i.h], note: i.note })),
      api: {
        drawUnit: 'drawUnit(ctx, X, Y, W, H, o) → layout + flow · the spread into any rect, cell-exact and centred',
        layout: 'layout(X, Y, W, H, o) → rects and hit-boxes, nothing drawn',
        fit: 'fit(availW, availH, o) → the integer scale and the cols/lines that fill the room',
        flow: 'flow(blocks, layout) → placement and overflow, nothing drawn',
        render: 'render({cols, lines, scale}) → canvas sized from the CELLS · render({W, H}) fits the biggest legal spread',
        pieces: 'tickBox · tickMark · currentMark · strike · doneStamp · caret · cursor · highlight · tabStub · drawStock · plate · newNoteMark · ribbon · dogEar · cornerMark · pageNumber · wear · pageTurn · overflowMark',
        type: 'hand(ctx, str, x, y, s, col, colLo, seed, {limit, press, skew, rough}) → {w, cells, adv, origins[]} · ROUGH DEFAULTS TO 0: the clean shared face, pixel-identical to BubbleKit.text(). press and skew apply only when rough is 1 — ink and pencil are told apart by COLOUR, never by holes in the letters. text() is the same glyphs unroughened, used by probeHand() and the sheet captions · wrap() and FONT are the bubble kit\u2019s',
        numbers: 'metricsTable() · readBudget({out, tiers, share}) · probes() · boards() · sheet()/sheetSpec() · contract()',
      },
      breaking: [
        'THE CELL CHANGED: 4 \u00d7 8 caps-only \u2192 ' + CELL.adv + ' \u00d7 ' + CELL.line + ' mixed case, imported from BubbleKit. Every pixel number in the pass-2 contract is stale.',
        'body text is SENTENCE CASE; caps are for labels (tab chips, running heads, the stamp)',
        'an entry is a BLOCK (title + context + steps), not a row. place() is gone; flow() replaces it and returns block placement across both leaves.',
        'drawUnit takes o.page (quests · done · knowledge · notes) and o.blocks; o.entries is gone',
        'globals are NotebookKit / NotebookIsoKit',
      ],
      flagged: [
        'THE DONE TREATMENT. Struck + ticked ships by default (cheapest, carried, keeps the tick). The harbour stamp is the best read on a Done tab and the most characterful, but it is the harbourmaster\u2019s mark in HER book — that is a fiction call, not an art call. Ink fade is quietest and loses the tick. All three are drawn and on the board.',
        'THE CURRENT STEP. The ink arrow ships because gold already means SELECTED, and spending it twice on one page is how a colour stops meaning anything. If the owner wants the bubble kit\u2019s gold-pill emphasis on the current step, the swap is one string — and then selection needs its own mark (the pencil bracket is drawn and measured).',
        'THE FACE ON THE PAGE — RESOLVED 2026-08-17, recorded here because it reverses pass 1 and 2. The page is set in the bubble kit\u2019s face CLEAN: the roughened hand (wobble, dropped strokes, crowding, drift) was unreadable at page sizes, and knowledge pages are the longest running text in the game. The roughening is still drawn, still measured by probeHand(), and one flag away (`rough`) if the owner wants it back for short pages. What carries "she wrote this" now is the furniture: her pencil-ruled leaves, her wobbling boxes, her strikes, pencil weight for her own lines.',
        'THE COVER. Waxed dark teal ships (pass 1). Oxblood reads instantly against water and is the best closed-icon at 3 \u00d7 5 px; oiled canvas reads as a ledger rather than a diary. Whichever wins, the ribbon stays — it is the only thing that reads on a dark cover.',
        'HOW MUCH SCREEN THE OPEN BOOK TAKES. readBudget().occupancyAnswer states the fraction at the closest tier instead of asserting a screenshot. The kit\u2019s position: it is a held object and it may dominate; the number is published so the presenter chooses knowingly.',
        'KNOWLEDGE SUB-TAB COUNT. N is data. The chip clips at ' + colsFor(GEO.tabCol - GEO.tabPad - 1, 1) + ' characters and ' + GEO.maxTabs + ' stubs fit the fore-edge at any legal size — past that the tab column needs a second bank, which is a layout change, not a label change.',
        'WEAR. Two levels ship cheap. Per-page wear, ring stains, a cracked spine and a swollen block are a separate ask with real art in them.',
        'THE ILLUSTRATION SLOT\u2019S CONTENTS. The rect, the frame and the caption ship. Nobody has said what draws inside it.',
      ],
    };
  }

  root.NotebookKit = {
    version: VERSION, PPU, CELL, GEO, FONT, DAY, NIGHT, GOLD, SKIN, PROV, SAMPLE,
    STOCKS, BOXES, BOX_ORDER, CURRENTS, CURRENT_ORDER, DONES, DONE_ORDER,
    CURSORS, CURSOR_ORDER, HIGHLIGHTS, HL_ORDER, TAB_FAMILIES, TAB_STYLES, TAB_STYLE_ORDER,
    COVERS, COVER_ORDER, PLAYER_TEXT, PLAYER_TEXT_ORDER, PAGES, PAGE_ORDER, TURN_FRAMES,
    text, textW, colsFor, linesFor, wrap, hand, penStroke, rng32,
    pageW, pageH, totalW, totalH, titleColsFor, sizeFor, fit, layout, blockRows, flow,
    tickBox, tickMark, currentMark, strike, doneStamp, caret, cursor, highlight, tabStub,
    drawStock, plate, newNoteMark, ribbon, dogEar, cornerMark, pageNumber, wear, pageTurn, overflowMark,
    drawUnit, paintInto: drawUnit, render, boards,
    metricsTable, readBudget, probeType, probeHand, probeWrite, probeFlow, probes,
    sheet, sheetSpec, contract,
  };

  /* ========================================================================= */
  /* ======================== the closed world object ========================
     Carried from pass 2 (at 3 \u00d7 5 px there was nothing to fix) with the cover ramp now taken from
     the cover board, so whichever tile the owner picks is one argument. This is also the ICON
     gameplay mounts wherever the open verb lives. */
  const IS = root.IsoSolid;
  if (!IS) return;
  const IW = 14, IH = 12, icx = 7, icy = 8;
  const NW_ = 0.100, NL = 0.145, NT = 0.018;
  const PAGES_RAMP = ['#7d7563', '#948b76', '#aca289', '#c2b89e', '#d3c9ae'];
  const BAND = ['#0f1416', '#171e21', '#20292c', '#2b3538'];
  const RIB = ['#5a2a20', '#743629', '#8e4534', '#a75742'];
  const coverRamp = (k) => { const r = (COVERS[k] || COVERS.waxed).ramp;
    return [r[0], r[0], r[1], r[2], r[3]]; };

  function facesOf(opts) {
    opts = opts || {};
    const F = [], push = (a) => { for (const f of a) F.push(f); };
    const z = NT / 2;
    push(IS.box([0, 0, z], [NW_ / 2 - 0.003, NL / 2 - 0.003, NT / 2 - 0.001], 'PAGES', 0.50, 0.002, null, 'pages'));
    push(IS.box([0, 0, z], [NW_ / 2, NL / 2, NT / 2], 'COVER', 0.50, 0, null, 'cover'));
    push(IS.box([0, 0, NT + 0.002], [NW_ / 2 - 0.012, NL / 2 - 0.016, 0.003], 'COVER', 0.95, 0.004, null, 'cover'));
    if (opts.strap !== false)
      push(IS.box([NW_ / 2 - 0.014, 0, z], [0.005, NL / 2 + 0.002, NT / 2 + 0.002], 'BAND', 0.50, 0.006, null, 'strap'));
    if (opts.ribbon !== false)
      push(IS.box([-0.012, -NL / 2 - 0.010, 0.002], [0.004, 0.012, 0.002], 'RIBBON', 0.50, 0.004, null, 'ribbon'));
    return F;
  }
  const _F = {};
  function isoRender(dir, opts) {
    opts = opts || {};
    const ck = opts.cover || 'waxed';
    const key = ck + (opts.strap === false ? 's0' : 's1') + (opts.ribbon === false ? 'r0' : 'r1');
    if (!_F[key]) _F[key] = facesOf(opts);
    return IS.paint(_F[key], { W: IW, H: IH, cx: icx, cy: icy, dir, elev: opts.elev,
      keyline: opts.keyline != null ? opts.keyline : false, key: '#12191a',
      MATS: { COVER: { ramp: coverRamp(ck), off: 0 }, PAGES: { ramp: PAGES_RAMP, off: 0 },
        BAND: { ramp: BAND, off: 0 }, RIBBON: { ramp: RIB, off: 0 } } });
  }
  function anchors(dir, opts) {
    opts = opts || {};
    const B = IS.camBasis({ dir, elev: (opts || {}).elev });
    const p = (x, y, z) => { const v = IS.proj(x, y, z, B, icx, icy);
      return { dx: Math.round(v.sx - icx), dy: Math.round(v.sy - icy) }; };
    return { carry: p(0, 0, NT / 2), cover: p(0, 0, NT), spine: p(-NW_ / 2, 0, NT / 2) };
  }
  root.NotebookIsoKit = {
    W: IW, H: IH, pivot: { x: icx, y: icy }, pivotIs: 'ground under centre',
    dims: { w: NW_, l: NL, t: NT }, px_per_m: PPU, defaultElev: IS.DEFAULT_ELEV, dirs: 8,
    render: isoRender, anchors, facesOf, covers: COVER_ORDER,
    contract: () => ({ part: 'closed object + the open verb\u2019s icon', rig: 'notebookRig3.js', global: 'NotebookIsoKit',
      cell: [IW, IH], pivot: [icx, icy], pivotIs: 'ground under the centre (standard prop convention)',
      dims_m: { w: NW_, l: NL, t: NT }, ppu: PPU, px: '~3 \u00d7 5 px in the world', dirs: 8,
      keyline: 'none (ADR 0031)',
      read: 'dark cover block, one pale sliver of page edge, a darker band where the elastic crosses, and the ribbon tail. Nothing else exists at this size.',
      covers: COVER_ORDER.map(k => ({ key: k, ship: !!COVERS[k].ship, ramp: COVERS[k].ramp })),
      icon: 'the SAME art is the mounted icon wherever the open verb lives — the dev-key ledger is exhausted, so the book opens off an existing verb and this is what that verb shows',
      anchors: 'carry (into the palm, spine inboard) \u00b7 cover \u00b7 spine',
      facings: 'bake all 8: at 3 \u00d7 5 px they differ by a pixel or two and they are free' }),
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
