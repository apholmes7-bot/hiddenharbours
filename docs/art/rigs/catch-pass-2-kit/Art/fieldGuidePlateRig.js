/* Hidden Harbours — THE FIELD GUIDE PLATE (catch review, pass C). One knowledge page per species with
   a def, drawn on the notebook kit: the def's words on the ruled leaf, the species on the squared leaf
   in the room the kit reserves for a plate that is not part of a section (layout().plate).

   Global: FieldGuidePlate. REQUIRES, loaded first and in their own order: the catch rigs
   (Art/isoSolid.js, fishIsoRig2.js, shellfishRig2.js, crustaceanRig2.js) and the notebook kit
   (Art/dialogueBubbleRig.js, notebookRig3.js). This rig draws no species and no furniture of its own:
   the species come from the catch rigs as they stand, the page, frame, corner ticks and caption from
   the kit. What it adds is where the drawing sits, at what scale, and the bar that says so.

   ─────────────────────────────────────────────────────────────────────────────────────────────
   THE PAGE SCALE. A page is not the world. In the world 32 px = 1 m holds; on a plate the species is
   drawn at a page scale of its own, P page units to the metre, and the plate says so with a bar.
   - P is a multiple of 20, so every bar on the ladder (1 m, 50, 25, 20, 10, 5 cm) is whole units.
   - The catch rig draws one of its pixels per page unit (m = 1): it is asked for its plate drawing at
     scale P / 32 and nothing is resampled. The kit's integer scale s then takes a unit to s x s px,
     so a plate at s2 or s3 is the s1 plate of its page size scaled, pixel for pixel (probeScale).
   - Every species is its own rig's plate drawing (renderPlate), for plates only, in a PW x PH cell
     (160 x 96) that holds the largest frame: the game's species at more pixels, with the marks a
     field guide shows (each rig's MARKS). The rigs' world cells are not touched.
   - fit() takes the largest P whose drawing the rig's cell does not clip and that sits inside the
     plate, PAD units clear of its frame, with a bar. The frame binds, so P is the page size's: one P
     for a species at a page size (cols x lines), its tiers alike. The scan starts a tenth above where
     the drawing at P_REF, grown in proportion, meets the frame (a drawing grows with P, so no larger
     P fits), and at most at P_MAX; probeFit checks that the next step up does not fit.
   - The bar is the longest on the ladder no longer than the drawing, at least BAR_MIN units, set in
     the caption row a cell after its label, straight, in pencil. The label is the caption: it takes
     the place of the kit's default.

   THE WORDS are the caller's, read from the def: its name, its Flavor up to the first team note
   ("<WORD> NOTE:"), and one line for each field it carries (weight band, seasons, gear, depth).
   blocks() sets them as the kit's sections. Nothing here writes a word but the bar's label.

   WHERE THE PLATE GOES. On the squared leaf when the words keep to the ruled leaf. When they run onto
   the squared leaf the plate turns the page, as the kit turns what will not fit: the next spread
   carries the name and the plate (render().pages[1]).

   CATCH REVIEW PASS D (2026-10-28; the owner's ruling 2 of 10-28, a plate-size clam drawing in
   Shellfish2, which the plate draws): the clam is drawn at P / 32 by Shellfish2.renderPlate, as every
   other species is by its rig, in place of its item at m = 10. FIXED_M goes; P_MAX rises from 400 to
   640 so the clam's cell binds, as every other species' does. VERSION 2.

   CATCH REVIEW PASS E (2026-10-28; the owner's ruling of 10-28, "Bigger, with field marks"): each
   species is drawn for its plate by its own rig's renderPlate (FishIso2, Crustacean2, Shellfish2), at
   about 3x pass D's, filling the frame, with the marks a field guide shows. The cell no longer binds,
   so P is one per page size, not one per species; P_MAX rises from 640 to 2000 and the scan starts
   near the fit (P_REF); the views are the rigs' own. VERSION 3.

   THE OWNER'S LOOK AT PASS E (2026-10-28; "the crab legs look a little off"): Crustacean2's plate
   draws its own limbs, at rest, so the lobster's and the crab's views say "at rest" for "walking". */
(function (root) {
  'use strict';

  const N = root.NotebookKit, FI = root.FishIso2, CR = root.Crustacean2, SH = root.Shellfish2;
  if (!N || !FI || !CR || !SH) {
    console.error('[fieldGuidePlateRig] load the catch rigs (FishIso2, Crustacean2, Shellfish2) and the ' +
      'notebook kit (NotebookKit) FIRST: the plate draws its species and its page with theirs.');
    return;
  }

  const VERSION = 3, PPU = N.PPU;                      /* the world: 32 px = 1 m */
  const STEP = 20, P_MAX = 2000, P_REF = 200;          /* P, page units per metre: a multiple of STEP */
  const BARS = [1, 0.5, 0.25, 0.2, 0.1, 0.05];         /* metres, longest first */
  const BAR_MIN = 6, PAD = 3;

  /* ============================== the views ================================
     Which rig draws each def's species. Keyed by the def's Id. The view is the rig's renderPlate's own
     (pass E): the fish lie side-on, facing left; the flounder, the lobster and the crab are seen from
     above, as they lie; the clam lies side-on, its foot end left. Every view is the rig's default look
     (ringless, no water, no spoil); the lobster is the live one. */
  const VIEWS = {
    'fish.atlantic_cod':     { rig: 'FishIso2', species: 'cod', view: 'side-on, facing left' },
    'fish.haddock':          { rig: 'FishIso2', species: 'haddock', view: 'side-on, facing left' },
    'fish.pollock':          { rig: 'FishIso2', species: 'pollock', view: 'side-on, facing left' },
    'fish.mackerel':         { rig: 'FishIso2', species: 'mackerel', view: 'side-on, facing left' },
    'fish.striped_bass':     { rig: 'FishIso2', species: 'bass', view: 'side-on, facing left' },
    'fish.winter_flounder':  { rig: 'FishIso2', species: 'flounder', view: 'from above, head left' },
    'fish.atlantic_herring': { rig: 'FishIso2', species: 'herring', view: 'side-on, facing left' },
    'fish.lobster':          { rig: 'Crustacean2', species: 'lobster', view: 'from above, head left, at rest' },
    'fish.rock_crab':        { rig: 'Crustacean2', species: 'crab', view: 'from above, legs across, at rest' },
    'fish.soft_shell_clam':  { rig: 'Shellfish2', species: 'clam', view: 'side-on, the left valve, siphon right' },
  };
  const RIG = { FishIso2: FI, Crustacean2: CR, Shellfish2: SH };
  const ORDER = Object.keys(VIEWS);

  /* ============================== the drawing ==============================
     The rig's own plate drawing at scale P / 32, cropped to what it drew, as runs of one colour. A drawing
     that touches its cell's edge is taken as clipped. Only opaque pixels are drawn; partial ones are
     counted (every view above is binary, so a count is a finding). */
  const _D = {};
  const hex2 = (n) => (n < 16 ? '0' : '') + n.toString(16);
  function drawing(id, P) {
    const v = VIEWS[id]; if (!v) return null;
    const key = id + '@' + P; if (_D[key]) return _D[key];
    const R = RIG[v.rig], m = 1, scale = P / PPU, W = R.PW, H = R.PH, rgba = R.renderPlate(v.species, scale);
    let x0 = W, y0 = H, x1 = -1, y1 = -1, partial = 0;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const a = rgba[(y * W + x) * 4 + 3];
      if (a > 0 && a < 255) partial++;
      if (a === 255) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; } }
    if (x1 < 0) return (_D[key] = { id, P, m, scale, cell: [W, H], w: 0, h: 0, runs: [], clipped: false, partial, empty: true });
    const runs = [];
    for (let y = y0; y <= y1; y++) { let x = x0;
      while (x <= x1) { const i = (y * W + x) * 4;
        if (rgba[i + 3] !== 255) { x++; continue; }
        const col = '#' + hex2(rgba[i]) + hex2(rgba[i + 1]) + hex2(rgba[i + 2]); let n = 1;
        while (x + n <= x1) { const j = (y * W + x + n) * 4;
          if (rgba[j + 3] !== 255 || '#' + hex2(rgba[j]) + hex2(rgba[j + 1]) + hex2(rgba[j + 2]) !== col) break; n++; }
        runs.push([x - x0, y - y0, n, col]); x += n; } }
    return (_D[key] = { id, P, m, scale, cell: [W, H], box: [x0, y0, x1, y1], w: x1 - x0 + 1, h: y1 - y0 + 1, runs,
      clipped: x0 === 0 || y0 === 0 || x1 === W - 1 || y1 === H - 1, partial });
  }

  /* ============================== the fit ==================================
     rect is the plate in screen px at scale s (layout().plate). Everything below is in page units. */
  const labelOf = (mtr) => (mtr >= 1 ? mtr + ' m' : Math.round(mtr * 100) + ' cm');
  function barFor(P, len, rowW) {
    for (const mtr of BARS) {
      const units = Math.round(mtr * P), label = labelOf(mtr), x = 2 + (label.length + 1) * N.CELL.adv;
      if (units > len || units < BAR_MIN || x + units > rowW - 2) continue;
      return { m: mtr, units, label, x, whole: Math.abs(mtr * P - units) < 1e-9 };
    }
    return null;
  }
  /* the drawing at P inside a plate rw x rh units, PAD clear, with a bar; or null */
  function fitsAt(id, P, rw, rh) {
    const d = drawing(id, P); if (!d || d.empty || d.clipped) return null;
    const w = d.w * d.m, h = d.h * d.m; if (w > rw - 2 * PAD || h > rh - 2 * PAD) return null;
    const bar = barFor(P, w, rw); return bar ? { d, w, h, bar } : null;
  }
  function startP(id, rw, rh) {
    const r = drawing(id, P_REF); if (!r || r.empty || r.clipped) return P_MAX;
    const P = 1.1 * P_REF * Math.min((rw - 2 * PAD) / r.w, (rh - 2 * PAD) / r.h);
    return Math.max(STEP, Math.min(P_MAX, STEP * Math.ceil(P / STEP)));
  }
  function fit(id, rect, s) {
    s = s || 1; const v = VIEWS[id]; if (!v) return null;
    const rw = rect.w / s, rh = rect.h / s;
    for (let P = startP(id, rw, rh); P >= STEP; P -= STEP) {
      const a = fitsAt(id, P, rw, rh); if (!a) continue;
      return { id, P, m: a.d.m, scale: a.d.scale, units: { w: a.w, h: a.h }, bar: a.bar, drawing: a.d, view: v.view,
        at: { x: Math.floor((rw - a.w) / 2), y: Math.floor((rh - a.h) / 2) } };
    }
    return null;
  }

  /* the plate's fill, for the kit's plate(ctx, layout, {fill}): the drawing centred in the frame, then the
     bar in the caption row after its label */
  function fillFor(f, o) {
    o = o || {}; const P = o.night ? N.NIGHT : N.DAY;
    return function (ctx, p, s) {
      ctx.save(); ctx.imageSmoothingEnabled = false;
      const d = f.drawing, U = d.m * s, x0 = p.x + f.at.x * s, y0 = p.y + f.at.y * s;
      for (const r of d.runs) { ctx.fillStyle = r[3]; ctx.fillRect(x0 + r[0] * U, y0 + r[1] * U, r[2] * U, U); }
      const by = p.cap ? p.cap.y : p.y + p.h + 2 * s, bx = p.x + f.bar.x * s, n = f.bar.units;
      ctx.fillStyle = P.pencil;
      ctx.fillRect(bx, by + 3 * s, n * s, s);
      ctx.fillRect(bx, by + s, s, 5 * s);
      ctx.fillRect(bx + (n - 1) * s, by + s, s, 5 * s);
      ctx.restore();
    };
  }

  /* ============================== the words ================================
     words = { name, body, facts: [] }, the caller's, from the def. An empty body leaves the head's own
     empty row as the gap (the kit gives every section one body row). */
  function blocks(w) {
    w = w || {};
    const out = [{ kind: 'section', head: w.name || '', body: w.body || '', gap: !!w.body }];
    for (const f of w.facts || []) out.push({ kind: 'section', body: f, gap: false });
    return out;
  }
  const nameBlock = (w) => ({ kind: 'section', head: (w && w.name) || '', body: '', gap: false });

  /* ============================== the page =================================
     plan() decides, nothing drawn: does the plate keep to the first spread, or turn the page.
     draw() draws spread o.spread (0, or 1 when it turned) into a rect, as drawUnit does, with
     o.page 'knowledge' and the knowledge tab. o.plate false draws the kit alone: its empty room,
     its own caption. */
  function kitOpts(o) { return Object.assign({}, o, { page: 'knowledge', active: o.active || 'know' }); }
  function plan(X, Y, WW, HH, o) {
    o = o || {};
    const L = N.layout(X, Y, WW, HH, kitOpts(o)), B = blocks(o.words), F = N.flow(B, L, {});
    const turned = F.used[1] > 0 || F.overflow > 0;
    return { L, blocks: B, flow: F, turned, pages: turned ? 2 : 1 };
  }
  function drawPlate(ctx, U, o) {
    if (o.plate === false) { N.plate(ctx, U, { night: o.night }); return { rect: U.plate, kit: true, fit: null }; }
    const f = fit(o.id, U.plate, U.s);
    if (!f) { N.plate(ctx, U, { night: o.night }); return { rect: U.plate, fit: null }; }
    N.plate(ctx, U, { night: o.night, caption: f.bar.label, fill: fillFor(f, o) });
    return { rect: U.plate, fit: f };
  }
  function draw(ctx, X, Y, WW, HH, o) {
    o = o || {};
    const pl = plan(X, Y, WW, HH, o), at = Math.min(o.spread || 0, pl.pages - 1);
    const B = at === 0 ? pl.blocks
      : [nameBlock(o.words)].concat(pl.flow.overflow ? pl.blocks.slice(pl.flow.firstDropped) : []);
    const pno = (o.pageNo == null ? 12 : o.pageNo) + 2 * at;
    const U = N.drawUnit(ctx, X, Y, WW, HH, Object.assign(kitOpts(o), { blocks: B, pageNo: pno }));
    const plate = at === 0 && pl.turned ? null : drawPlate(ctx, U, o);
    return { U, plate, turned: pl.turned, pages: pl.pages, spread: at };
  }

  function canvasFor(w, h) {
    const c = (typeof document !== 'undefined') ? document.createElement('canvas') : new root.OffscreenCanvas(w, h);
    c.width = Math.max(1, Math.round(w)); c.height = Math.max(1, Math.round(h)); return c;
  }
  /* render({id, words, cols, lines, scale, plate}) → { pages: [{canvas, U, plate}], turned, size } */
  function render(o) {
    o = o || {};
    const F = N.sizeFor(o.cols || 17, o.lines || 12, o.scale || 1);
    const ko = Object.assign({}, o, { align: 'topleft', fit: Object.assign({ clamped: false }, F) });
    const pl = plan(0, 0, F.w, F.h, ko), pages = [];
    for (let i = 0; i < pl.pages; i++) {
      const c = canvasFor(F.w, F.h), ctx = c.getContext('2d'); ctx.imageSmoothingEnabled = false;
      const r = draw(ctx, 0, 0, F.w, F.h, Object.assign({}, ko, { spread: i }));
      pages.push({ canvas: c, U: r.U, plate: r.plate });
    }
    return { pages, turned: pl.turned, size: F };
  }

  /* ============================== the probes ===============================
     words: { [defId]: {name, body, facts} }, the caller's. Tiers are the kit's read budget's. */
  const tiers = () => N.readBudget().cases.map((c) => ({ zoom: c.zoom, cols: c.cols, lines: c.lines, scale: c.scale }));
  const tierKey = (t) => t.cols + 'x' + t.lines + ' s' + t.scale;
  const layoutAt = (t) => { const F = N.sizeFor(t.cols, t.lines, t.scale);
    return N.layout(0, 0, F.w, F.h, { align: 'topleft', fit: Object.assign({ clamped: false }, F) }); };

  function probeFit() {
    const fails = [], plates = {};
    for (const id of ORDER) {
      const Ps = {};
      for (const t of tiers()) {
        const L = layoutAt(t), f = fit(id, L.plate, L.s), k = tierKey(t), page = t.cols + 'x' + t.lines;
        if (!f) { fails.push(id + ' ' + k + ': no fit'); continue; }
        (Ps[page] = Ps[page] || []).push(f.P);
        const rw = L.plate.w / L.s, rh = L.plate.h / L.s;
        if (f.P + STEP <= P_MAX && fitsAt(id, f.P + STEP, rw, rh)) fails.push(id + ' ' + k + ': P ' + (f.P + STEP) + ' fits too');
        if (f.P % STEP) fails.push(id + ' ' + k + ': P ' + f.P + ' is not a multiple of ' + STEP);
        if (!f.bar.whole || f.bar.units < BAR_MIN || f.bar.units > f.units.w) fails.push(id + ' ' + k + ': bar ' + f.bar.units);
        if (f.bar.x + f.bar.units > rw - 2) fails.push(id + ' ' + k + ': bar runs past the caption row');
        if (f.drawing.clipped) fails.push(id + ' ' + k + ': clipped by the cell');
        if (f.at.x < PAD || f.at.y < PAD || f.at.x + f.units.w > rw - PAD || f.at.y + f.units.h > rh - PAD) fails.push(id + ' ' + k + ': not inside the frame');
        if (f.drawing.partial) fails.push(id + ' ' + k + ': ' + f.drawing.partial + ' partial pixels left out');
        plates[id] = plates[id] || {};
        if (!plates[id][page]) plates[id][page] = { P: f.P, m: f.m, scale: f.scale, units: [f.units.w, f.units.h],
          bar: f.bar.label, barUnits: f.bar.units, cell: f.drawing.cell, view: f.view };
      }
      for (const page in Ps) if (new Set(Ps[page]).size > 1) fails.push(id + ' ' + page + ': P differs across its tiers (' + Ps[page].join(', ') + ')');
    }
    return { pass: !fails.length, plates, tiers: tiers().map(tierKey),
      claim: 'each species fits at the largest page scale P, a multiple of ' + STEP + ', that holds it (the next step up does not), one P for a page size at each of its tiers; the drawing is the rig’s own plate drawing, not clipped by its cell, ' + PAD + ' units clear of the frame; the bar is whole units, at least ' + BAR_MIN + ', no longer than the drawing, and inside the caption row.',
      fails: fails.length ? fails : null };
  }

  function probeWords(words) {
    words = words || {};
    const fails = [], pages = {}, missing = {};
    for (const t of tiers()) {
      const k = tierKey(t); pages[k] = {};
      for (const id of ORDER) {
        const w = words[id]; if (!w) { fails.push(id + ': no words given'); continue; }
        for (const ch of [w.name || ''].concat(w.body || '', w.facts || []).join('')) if (!N.FONT[ch]) missing[ch] = (missing[ch] || 0) + 1;
        const L = layoutAt(t), B = blocks(w), F = N.flow(B, L, {}), turned = F.used[1] > 0 || F.overflow > 0;
        if (F.placed.length + F.overflow !== B.length) fails.push(id + ' ' + k + ': placed + overflow != blocks');
        let turnLeft = true;
        if (turned) { const B2 = [nameBlock(w)].concat(F.overflow ? B.slice(F.firstDropped) : []), F2 = N.flow(B2, L, {});
          turnLeft = F2.used[1] === 0 && !F2.overflow; if (!turnLeft) fails.push(id + ' ' + k + ': the turned page runs onto the plate’s leaf'); }
        pages[k][id] = { pages: turned ? 2 : 1, rowsUsed: F.used.slice(), blocks: B.length };
      }
    }
    const miss = Object.keys(missing);
    if (miss.length) fails.push('characters not in the face: ' + JSON.stringify(miss.join('')));
    return { pass: !fails.length, pages, missingGlyphs: miss.join(''),
      claim: 'every character is in the kit’s face; the words keep to the ruled leaf, or the plate turns the page and the next spread keeps the plate’s leaf clear; placed + overflow = blocks.',
      fails: fails.length ? fails : null };
  }

  /* the fill alone on clear paper, read back over the whole spread */
  function fillOnly(id, t) {
    const F = N.sizeFor(t.cols, t.lines, t.scale), L = layoutAt(t), f = fit(id, L.plate, L.s);
    const c = canvasFor(F.w, F.h), ctx = c.getContext('2d');
    if (f) fillFor(f)(ctx, L.plate, L.s);
    return { w: F.w, h: F.h, data: ctx.getImageData(0, 0, F.w, F.h).data, L, f };
  }
  function probeScale() {
    const fails = []; let compared = 0;
    const base = tiers().filter((t) => t.scale > 1);
    for (const id of ORDER) for (const t of base) {
      const one = fillOnly(id, Object.assign({}, t, { scale: 1 })), big = fillOnly(id, t), k = t.scale;
      let bad = 0;
      for (let y = 0; y < big.h; y++) for (let x = 0; x < big.w; x++) {
        const i = (y * big.w + x) * 4, j = (Math.floor(y / k) * one.w + Math.floor(x / k)) * 4;
        for (let c = 0; c < 4; c++) if (big.data[i + c] !== one.data[j + c]) { bad++; break; } }
      compared++; if (bad) fails.push(id + ' ' + tierKey(t) + ': ' + bad + ' px differ from s1 x' + k);
    }
    return { pass: !fails.length, compared,
      claim: 'the plate’s fill (drawing and bar) at the kit’s s2 and s3 tiers is the s1 fill scaled, pixel for pixel: one rig pixel is one page unit, and a unit is s x s px.',
      fails: fails.length ? fails : null };
  }
  function probeInk() {
    const fails = []; let inked = 0;
    for (const id of ORDER) for (const t of tiers()) {
      const r = fillOnly(id, t), p = r.L.plate, s = r.L.s, cap = p.cap.y;
      let out = 0;
      for (let y = 0; y < r.h; y++) for (let x = 0; x < r.w; x++) { if (!r.data[(y * r.w + x) * 4 + 3]) continue; inked++;
        const inFrame = x > p.x && x < p.x + p.w && y > p.y && y < p.y + p.h;
        const inCap = x >= p.x && x < p.x + p.w && y >= cap && y < cap + N.CELL.box * s;
        if (!inFrame && !inCap) out++; }
      if (out) fails.push(id + ' ' + tierKey(t) + ': ' + out + ' px outside the frame and the caption row');
    }
    return { pass: !fails.length, inked,
      claim: 'everything the plate draws lies inside the kit’s frame or in its caption row.',
      fails: fails.length ? fails : null };
  }
  function probes(words) {
    const r = { fit: probeFit(), words: probeWords(words), scale: probeScale(), ink: probeInk() };
    r.pass = r.fit.pass && r.words.pass && r.scale.pass && r.ink.pass;
    return r;
  }

  root.FieldGuidePlate = {
    version: VERSION, PPU, STEP, P_MAX, P_REF, BARS, BAR_MIN, PAD, VIEWS, ORDER,
    drawing, barFor, fit, fillFor, blocks, plan, draw, render, tiers,
    probeFit, probeWords, probeScale, probeInk, probes,
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
