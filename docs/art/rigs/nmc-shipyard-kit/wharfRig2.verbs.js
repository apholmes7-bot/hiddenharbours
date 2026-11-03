/* Hidden Harbours — WHARF RIG, PASS 2 · FIXTURES AND VERBS FOR CHARACTER v9.2 (globalThis.WharfVerbs2; adds addFixtures to WharfGeo2).
   Load after wharfRig2.geo.js and wharfRig2.fam.js, before wharfRig2.kit.js' gameplay() is called.

   Every fixture a figure uses is built to a number from export/character-v9.2-import-kit reports/worldfit.txt, the heights at
   which EVERY creator body (375) makes every limb target of the clip to within 5 mm, and publishes the verb that uses it: where
   to stand, which way to face, the v9.2 clips in order with their opts, and what the engine does between them.

     fish-cleaning table  top 0.72    chop 0.598–0.760 · bench 0.643–0.853        gut · bait
     dock box             lid 0.85    lift / place / toss 0.718–0.978             stow · fetch
     wharf davit          warp 1.05, sheave offset × 0.52 (0.400–0.632 at 1.05)    haulTrap
     life-ring post       ring grip at stowV 0.95 (+0.095) · reach 0–1.012         throwRing (reach, then toss 0.93)
     service pedestal     tap at stowV 0.95 (+0.095)                               fillWater · plugIn
     boarding step        0.60 (board 0.549–0.724)                                 stepAboard in two
     ladder               rung 0.24 (0.211–0.273) · top-out = board at 3 rungs = 0.72 (0.549–0.724)
   Fixture frame: the figure's root at (0, 0) facing +y; the fixture stands ahead of it. Placed with G.xform (rot, x, y).    */
(function (root) {
  'use strict';
  const G = root.WharfGeo2; if (!G || !G.resolve) throw new Error('wharfRig2.verbs.js: load wharfRig2.geo.js and wharfRig2.fam.js first');
  const { at, F, box, pipe, tube, decalZ, decalY, xform, plankTex, sawnTex, CHAR } = G;
  const r2 = (v) => Math.round(v * 100) / 100, r3 = (v) => Math.round(v * 1000) / 1000;

  // v9.2 world fit, every creator body (reports/worldfit.txt); REACH is the tool rest's lift, GRIP the hand above it
  const FITS = { rail: [0.549, 0.724], rung: [0.211, 0.273], load: [0.718, 0.978], bench: [0.643, 0.853], knife: [0.598, 0.760], rest: [0, 1.012],
    warp: { workZ: 1.05, sheaveK: [0.400, 0.632] }, seat: [0.250, 0.291], saddleScale: [0.34, 0.49],
    stepDown: [0.06, 1.30], stepUp: [0.549, 0.724], stepUpNoGrip: [0.06, 1.30] };
  const REACH = { rests: { ground: 0, stowV: 0.95, stowH: 1.05 }, gripRise: 0.095, want: [0.132, 0.265] };

  const FIX = {
    fishTable: { label: 'fish-cleaning table', w: 1.6, d: 0.66, h: 0.72, stand: 0.14, edge: 0.85, verbs: ['gut', 'bait'], fit: [['knife', 0.72], ['bench', 0.72]] },
    dockBox: { label: 'dock box', w: 1.0, d: 0.62, h: 0.85, stand: 0.22, edge: 0.88, verbs: ['stow', 'fetch'], fit: [['load', 0.85]] },
    davit: { label: 'wharf davit + hauler', w: 0.5, d: 0.5, h: 1.9, stand: 0, edge: 0.55, sheaveK: 0.52, verbs: ['haulTrap'], fit: [['warp', 1.05]] },
    ringPost: { label: 'life-ring post', w: 0.7, d: 0.2, h: 1.45, stand: 0.25, edge: 0.50, verbs: ['throwRing'], fit: [['rest', 0.95]] },
    pedestal: { label: 'service pedestal', w: 0.3, d: 0.24, h: 1.1, stand: 0.30, edge: 0.60, verbs: ['fillWater', 'plugIn'], fit: [['rest', 0.95]] },
    boardStep: { label: 'boarding step', w: 0.8, d: 0.55, h: 0.60, stand: 0.05, edge: 0.62, verbs: ['stepAboard'], fit: [['rail', 0.60]] },
    fishSpot: { label: 'fishing spot', w: 0.9, d: 0, h: 0, stand: 0, edge: 0.45, verbs: ['fish'], fit: [] },
  };

  // ============================ THE FIXTURES (local frame, z = the deck) =====================================
  function ringBuoy(out, x, y, z, R, r) { const n = 16, m = 6;
    const P = (i, j) => { const th = i / n * Math.PI * 2, ph = j / m * Math.PI * 2, rr = R + r * Math.cos(ph); return [x + Math.cos(th) * rr, y + r * Math.sin(ph), z + Math.sin(th) * rr]; };
    for (let i = 0; i < n; i++) for (let j = 0; j < m; j++) { const i2 = (i + 1) % n, j2 = (j + 1) % m; out.push(F([P(i, j), P(i2, j), P(i2, j2), P(i, j2)], ((i + 2) >> 2) & 1 ? 'board' : 'red', Math.sin(j / m * Math.PI * 2) * 0.22)); } }
  const BUILD = {
    fishTable(out, z, g) { const T = FIX.fishTable, hw = T.w / 2, y0 = T.stand, y1 = y0 + T.d, top = z + T.h; at('table', g, 1);
      for (const [lx, ly] of [[-hw + 0.07, y0 + 0.07], [hw - 0.07, y0 + 0.07], [-hw + 0.07, y1 - 0.07], [hw - 0.07, y1 - 0.07]]) box(out, lx - 0.035, lx + 0.035, ly - 0.035, ly + 0.035, z, top - 0.06, 'wood', -0.3, sawnTex(), true);
      box(out, -hw + 0.04, hw - 0.04, y0 + 0.04, y1 - 0.04, z + 0.16, z + 0.20, 'wood', -0.5, sawnTex(), false);
      box(out, -hw, hw, y0, y1, top - 0.06, top, 'wood', 0.1, plankTex(0.15), false);
      decalZ(out, top + 0.004, -0.55, 0.35, y0 + 0.05, y0 + 0.48, 'board', 0.1, null, 0.03);
      box(out, -hw, hw, y1 - 0.03, y1, top, top + 0.16, 'wood', -0.1, sawnTex(), false);
      pipe(out, hw - 0.12, y1 - 0.09, 0.018, top, top + 0.34, 'galv', 0.2, 6); tube(out, [hw - 0.12, y1 - 0.09, top + 0.34], [hw - 0.12, y1 - 0.26, top + 0.29], 0.016, 6, 'galv', 0.2, null, true); },
    dockBox(out, z, g) { const B = FIX.dockBox, hw = B.w / 2, y0 = B.stand, y1 = y0 + B.d, h = B.h; at('dockbox', g, 1);
      box(out, -hw, hw, y0, y1, z, z + 0.05, 'hdpe', -0.6, null, true);
      box(out, -hw + 0.01, hw - 0.01, y0 + 0.01, y1 - 0.01, z + 0.05, z + h - 0.05, 'board', 0, null, true);
      box(out, -hw - 0.02, hw + 0.02, y0 - 0.02, y1 + 0.01, z + h - 0.05, z + h, 'board', 0.2, null, false);
      decalY(out, y0 - 0.02, -1, -0.06, 0.06, z + h - 0.17, z + h - 0.05, 'galv', 0.2, null, 0.02); },
    davit(out, z, g) { const D = FIX.davit, k = D.sheaveK, sv = [0.24 * k, 0.34 * k, 1.05 + 0.26], px = 0.34, py = 0.42, top = z + D.h; at('davit', g, 1);
      pipe(out, px, py, 0.16, z, z + 0.06, 'iron', 0.05, 10); pipe(out, px, py, 0.075, z + 0.06, top, 'galv', 0.1, 10);
      tube(out, [px, py, top - 0.05], [px, py + 1.25, top - 0.05], 0.05, 8, 'galv', 0.25, null, true);
      tube(out, [px, py + 0.25, top - 0.05], [px, py, top - 0.62], 0.03, 6, 'galv', 0.1, null, false);
      pipe(out, px, py + 1.22, 0.07, top - 0.21, top - 0.09, 'iron', 0.2, 8);
      tube(out, [px - 0.07, py, z + sv[2]], [sv[0] + 0.06, sv[1], z + sv[2]], 0.035, 6, 'galv', 0.1, null, true);
      tube(out, [sv[0] + 0.05, sv[1], z + sv[2]], [sv[0] - 0.05, sv[1], z + sv[2]], 0.17, 12, 'iron', 0.15, null, true);
      tube(out, [sv[0] - 0.05, sv[1], z + sv[2]], [sv[0] - 0.07, sv[1], z + sv[2]], 0.06, 8, 'yel', 0.3, null, true);
      at('line', g, 1); tube(out, [sv[0], sv[1] + 0.05, z + sv[2] + 0.17], [px, py + 1.22, top - 0.21], 0.012, 4, 'rope', 0.1, null, false);
      tube(out, [px, py + 1.22, top - 0.21], [px, py + 1.22, z - 1.6], 0.012, 4, 'rope', 0.1, null, false); },
    ringPost(out, z, g) { const R = FIX.ringPost, gz = REACH.rests.stowV + REACH.gripRise, Rr = 0.27, cx = REACH.want[0], cy = REACH.want[1] + 0.03; at('ringpost', g, 1);
      box(out, cx - 0.05, cx + 0.05, cy + 0.08, cy + 0.18, z, z + R.h, 'wood', 0.05, sawnTex(), false);
      for (const a of [-0.9, 0.9]) tube(out, [cx + Math.sin(a) * Rr, cy + 0.08, z + gz - Rr + Math.cos(a) * Rr], [cx + Math.sin(a) * Rr, cy - 0.02, z + gz - Rr + Math.cos(a) * Rr], 0.014, 5, 'galv', 0.2, null, true);
      ringBuoy(out, cx, cy, z + gz - Rr, Rr, 0.05); },
    pedestal(out, z, g) { const P = FIX.pedestal, cx = REACH.want[0], x0 = cx - 0.15, x1 = cx + 0.15, y0 = P.stand, y1 = y0 + P.d, tz = z + REACH.rests.stowV + REACH.gripRise; at('pedestal', g, 1);
      box(out, x0, x1, y0, y1, z, z + P.h - 0.08, 'board', 0, null, true);
      box(out, x0 - 0.02, x1 + 0.02, y0 - 0.02, y1 + 0.02, z + P.h - 0.08, z + P.h, 'yel', 0.25, null, false);
      decalY(out, y0, -1, x0 + 0.04, x1 - 0.04, z + 0.52, z + 0.80, 'hdpe', -0.2, null, 0.02);
      tube(out, [cx, y0, tz], [cx, REACH.want[1], tz], 0.016, 6, 'galv', 0.3, null, true); pipe(out, cx, REACH.want[1] + 0.02, 0.03, tz + 0.01, tz + 0.05, 'red', 0.3, 6); },
    boardStep(out, z, g) { const S = FIX.boardStep, hw = S.w / 2, y0 = S.stand, y1 = y0 + S.d, h = S.h; at('step', g, 0);
      box(out, -hw, hw, y0, y1, z, z + h - 0.04, 'wood', -0.1, sawnTex(), true);
      box(out, -hw - 0.01, hw + 0.01, y0 - 0.01, y1 + 0.01, z + h - 0.04, z + h, 'wood', 0.15, plankTex(0.14), false);
      decalZ(out, z + h + 0.003, -hw + 0.03, hw - 0.03, y0 + 0.01, y0 + 0.07, 'yel', 0.2, null, 0.02); },
  };

  // ============================ PLACEMENT ===================================================================
  /* s.fixtures: false | 'auto' | [{kind, x, face, off}] in module metres. Auto: by family, along the deck edges, never within
     reach of a ladder, a cleat, a bollard, a gap, a guide pile, the swim ladder or the gangway landing. A fixture stands with
     its back to the edge and the figure faces the water (davit, ring post, pedestal) or the fixture (table, dock box). */
  const AUTO = { pier: ['ringPost', 'fishTable', 'dockBox', 'davit', 'fishSpot', 'fishSpot'], crib: ['ringPost', 'fishTable', 'dockBox', 'fishSpot'], quay: ['ringPost', 'ringPost', 'fishSpot'],
    float: ['ringPost', 'dockBox', 'fishSpot'], finger: [] };
  const EDGE_PREF = { ringPost: 'berth', davit: 'berth', pedestal: 'berth', fishTable: 'back', dockBox: 'back', boardStep: 'berth', fishSpot: 'back' };
  function plan(s, P) {
    if (s.fixtures === false || !AUTO[s.family] && !Array.isArray(s.fixtures)) return [];
    const hx = P.hx, hy = P.hy, faces = s.faces || [], taken = { water: [], shore: [] }, add = (face, x, r) => taken[face].push([x - r, x + r]);
    for (const l of P.ladders) add(l.face, l.x, 1.0); for (const c of P.cleats) add(c.face, c.x, 0.5); for (const b of P.bollards) add(b.face, b.x, 0.6); for (const r of P.rings) add(r.face, r.x, 0.45);
    for (const f of ['water', 'shore']) for (const gp of (P.gaps[f] || [])) taken[f].push([gp[0] - 0.4, gp[1] + 0.4]);
    for (const gd of (s._guides || [])) { add(gd.y > 0 ? 'water' : 'shore', gd.x, 0.7); }
    if (s._swimX != null) add('water', s._swimX, 1.0);
    if (s.landing != null) { add('water', s.landing, 1.4); add('shore', s.landing, 1.4); }
    for (const f of ['water', 'shore']) for (const gp of (s.open[f] || [])) taken[f].push([gp[0] - 0.6, gp[1] + 0.6]);
    const free = (face, x, r) => x - r > -hx + 0.3 && x + r < hx - 0.3 && !taken[face].some(t => x + r > t[0] && x - r < t[1]);
    const berthFace = faces.indexOf('water') >= 0 ? 'water' : faces[0] || 'water', backFace = ['water', 'shore'].find(f => faces.indexOf(f) < 0) || (faces.indexOf('shore') >= 0 ? 'shore' : 'water');
    const want = Array.isArray(s.fixtures) ? s.fixtures : AUTO[s.family].map(kind => ({ kind }));
    const out = []; let n = 0;
    for (const w of want) { const K = FIX[w.kind]; if (!K) continue; const r = K.w / 2 + 0.25, pref = w.face || (EDGE_PREF[w.kind] === 'back' ? backFace : berthFace);
      let x = w.x != null ? w.x : null, face = pref;
      if (x == null) { const cand = []; for (let t = -hx + 0.8; t <= hx - 0.8 + 1e-6; t += 0.25) cand.push(+t.toFixed(2));
        const start = [0, -1, 1, 0.5][n++ % 4]; cand.sort((a, b) => Math.abs(a - start * hx * 0.5) - Math.abs(b - start * hx * 0.5));
        for (const f2 of w.face ? [pref] : [pref, pref === 'water' ? 'shore' : 'water']) { if (faces.indexOf(f2) < 0 && f2 !== backFace) continue; x = cand.find(c => free(f2, c, r)); if (x != null) { face = f2; break; } }
        if (x == null) continue; }
      add(face, x, r); const side = face === 'water' ? 1 : -1, d = w.off != null ? w.off : K.edge;
      out.push({ id: w.kind + out.length, kind: w.kind, face, x: r3(x), y: r3(side * (hy - d)), rot: side > 0 ? 0 : Math.PI, side }); }
    return out;
  }
  // the hook addFittings() calls: build the planned fixtures into the module's faces (floats: they ride the group)
  function addFixtures(out, s, T, P, g, top) {
    if (!{ pier: 1, crib: 1, quay: 1, float: 1, finger: 1 }[s.family]) return [];
    const list = plan(s, P);
    for (const f of list) { f.z = top; f.grp = g; if (!BUILD[f.kind]) continue; const tmp = []; G.CTX.grp = g; BUILD[f.kind](tmp, top, g); xform(tmp, f.rot, f.x, f.y, 0); for (const q of tmp) out.push(q); }
    return list;
  }
  G.addFixtures = addFixtures;

  // ============================ VERBS =======================================================================
  const WALK = 0.727, LADDER_RATE = r3(2 * CHAR.rung / 1.1), TOPOUT = r3(3 * CHAR.rung);
  const inR = (v, R) => v >= R[0] - 1e-9 && v <= R[1] + 1e-9;
  const fitRow = (param, v) => { const R = FITS[param]; return { param, value: v, range: R || null, ok: R ? inR(v, R) : true }; };
  /* BOARDING STEPS, measured with the v9.2 rig itself (evalClip, every limb of every frame within 5 mm: the kit's gate) over the
     375 creator bodies. boardDown grips nothing and fits every body at every railZ in its clamp, 0.06–1.30. board puts the left
     hand on the edge it steps onto and fits every body only in 0.549–0.724 (per body 0.387–0.549 up to 0.724–1.117): below, the
     tall adults cannot reach down to the edge (0.20 m short at 0.34, a float step); above, the short youths cannot reach up.
     So: a step down is boardDown; a step up in the window is board; a lower one is boardDown played backward (the same frames
     reversed, so the same fit, exact for every body); a higher one onto a hull goes over the boarding step (0.60, board) and
     then the rest; off a hull up onto a deck it is boardDown backward to 1.30; past 1.30, the ladder or nothing.          */
  const STEP = { level: 0.06, down: FITS.stepDown, up: FITS.stepUp, upNoGrip: FITS.stepUpNoGrip, measured: '375 creator bodies · characterIsoRig9 02df29ec… · poses 42179e44… · 5 mm gate' };
  const ADV = { board: 'to the landing over t 0.28\u20130.88', boardDown: 'to the landing over t 0.04\u20130.86', back: 'to the landing over t 0.14\u20130.96 (the clip reversed)' };
  function stepPlan(dz, o) {
    o = o || {}; const s = r3(Math.abs(dz)), lad = o.ladder || null;
    const P = (how, seq, fits, x) => Object.assign({ dz: r3(dz), how, seq, fits, first: seq.length ? (seq[0].clip || seq[0].verb) : null, opts: seq.length && seq[0].opts ? seq[0].opts : null }, x || {});
    const down = (h) => ({ clip: 'boardDown', once: true, opts: { railZ: r3(h) }, advance: ADV.boardDown });
    const upG = (h) => ({ clip: 'board', once: true, opts: { railZ: r3(h) }, advance: ADV.board, note: 'the left hand takes the edge' });
    const upN = (h) => ({ clip: 'boardDown', once: true, reverse: true, opts: { railZ: r3(h) }, advance: ADV.back, note: 'no grip', request: 'stepUp' });
    const up1 = (h) => inR(h, STEP.up) ? upG(h) : upN(h);
    if (s < STEP.level) return P('level', [{ clip: 'walk' }], true);
    if (dz < 0) return s <= STEP.down[1] ? P('step down', [down(s)], true) : lad ? P('ladder', [{ verb: 'climbDown', ladder: lad }], true) : P('none', [], false, { note: 'a drop of more than 1.30 m and no ladder at this berth' });
    if (s <= STEP.up[1]) return P(s < STEP.up[0] ? 'low step up' : 'step up', [up1(s)], true);
    const rest = r3(s - FIX.boardStep.h);
    if (o.stepBox && rest >= STEP.level && rest <= STEP.up[1]) return P('boarding step', [upG(FIX.boardStep.h), up1(rest)], true, { boardingStep: { fixture: 'boardStep', h: FIX.boardStep.h, rest } });
    if (s <= STEP.upNoGrip[1]) return P('high step up', [upN(s)], true, lad ? { alt: { verb: 'climbUp', ladder: lad } } : null);
    return lad ? P('ladder', [{ verb: 'climbUp', ladder: lad }], true) : P('none', [], false, { note: 'a climb of more than 1.30 m and no ladder at this berth' });
  }
  const REQUESTS = [
    { clip: 'ladderOn / ladderOff', why: 'v9.2 has the ladder loop and no way on or off it at the top', now: 'down: blend 300 ms from idle at the deck spot (0.55 m inboard, back to the water) to ladderDown u 0 at the top station (root 0.435 m out from the face, z = deck \u2212 0.72). Up: board at railZ 0.72, exact for every creator body, with the root advanced 0.60 m onto the deck over the drive (t 0.28\u20130.88).' },
    { clip: 'ladderToHull', why: 'stepping back off a ladder onto a boat alongside', now: 'stop the loop with the lower foot at the sole, blend 250 ms to idle on the hull, then turn' },
    { clip: 'walkSlope', why: 'gangways (18.4\u00b0 comfortable, 24\u00b0 steep) and slipways (to 15\u00b0): v9.2 walk plants on a level', now: 'walk at 0.727 m/s along the slope, root z linear hinge \u2192 toe; at 18\u00b0 the feet float at most \u00b11.5 px' },
    { clip: 'board travel', why: 'board and boardDown end at idle over the start xy; their horizontal travel is not published', now: 'advance the root to the landing over the drive (t 0.28\u20130.88); the kit gives the landing' },
    { clip: 'treadToLadder', why: 'a swimmer taking the bottom rung', now: 'blend 300 ms from tread at the ladder root to ladderDown reversed' },
    { clip: 'stepUp', why: 'board grips the edge it steps onto, and below 0.549 m the tallest creator bodies cannot reach it: a boat\u2019s sole up onto a float is 0.3\u20130.5 m', now: 'boardDown played backward, which fits every creator body from 0.06 to 1.30 m (measured on all 375)' },
  ];
  const SEQ = {
    gut: () => ({ seq: [{ clip: 'chop', loop: true, opts: { workZ: FIX.fishTable.h } }], note: 'the cut lands 0.32 m ahead, on the cutting board' }),
    bait: () => ({ seq: [{ clip: 'bench', loop: true, opts: { workZ: FIX.fishTable.h } }] }),
    stow: () => ({ seq: [{ clip: 'place', once: true, opts: { workZ: FIX.dockBox.h }, from: 'a carry (idle+tray, idle+pot) or lift' }], note: 'the load sets 0.42 m ahead, on the lid' }),
    fetch: () => ({ seq: [{ clip: 'place', once: true, reverse: true, opts: { workZ: FIX.dockBox.h } }], note: 'place played backward takes the load off the lid (lift starts at 0.30 m, below it)' }),
    haulTrap: () => ({ seq: [{ clip: 'hauler', loop: true, opts: { workZ: 1.05, sheave: [r3(0.24 * FIX.davit.sheaveK), r3(0.34 * FIX.davit.sheaveK), 1.31] } }], note: 'the warp runs from the sheave over the arm-tip block to the water' }),
    throwRing: () => ({ seq: [{ clip: 'reach', once: true, opts: { rest: 'stowV' } }, { clip: 'toss', once: true, opts: { workZ: 0.93 }, note: 'the ring leaves the hand at tossRelease 0.52' }] }),
    fillWater: () => ({ seq: [{ clip: 'reach', once: true, opts: { rest: 'stowV' } }], note: 'the hand is on the tap from 0.62 of the clip' }),
    plugIn: () => ({ seq: [{ clip: 'reach', once: true, opts: { rest: 'stowV' } }] }),
    stepAboard: () => ({ seq: [{ clip: 'board', once: true, opts: { railZ: FIX.boardStep.h } }, { clip: 'board | boardDown', once: true, opts: { railZ: 'the rest of the step' } }], note: 'a step of 1.149\u20131.324 m splits into two that fit every creator body' }),
    fish: () => ({ seq: [{ clip: 'hold', loop: true }, { clip: 'castBack', once: true }, { clip: 'castRelease', once: true }, { clip: 'hold', loop: true }, { chain: 'bite \u2192 strike \u2192 reel \u2192 land' }], note: 'stand 0.45 m back from the edge, face out' }),
  };
  function augment(gp, fr) {
    const mdl = fr.mdl, FL = [], V = [], blk = gp.BLOCKERS, yawOf = (d) => r2(Math.atan2(d[0], d[1]) / G.DEG);
    for (const M of mdl.modules) { const P = M.P; if (!P || !P.fixtures) continue; const c = Math.cos(M.rot), sn = Math.sin(M.rot), dirH = (v) => [v[0] * c - v[1] * sn, v[0] * sn + v[1] * c];
      const pose = M.s.floating ? fr.poses[M.grp] : null;
      for (const f of P.fixtures) { const K = FIX[f.kind], fwd = dirH([-Math.sin(f.rot), Math.cos(f.rot)]), q = M.toH(f.x, f.y), z = pose ? r3(pose.fwd([f.x, f.y, f.z])[2]) : r3(f.z);
        const stand = [r2(q[0]), r2(q[1]), z], id = M.id + '.' + f.id, rides = M.s.floating ? M.grp : null;
        const rec = { id, kind: f.kind, label: K.label, module: M.id, face: f.face, stand, yaw: yawOf(fwd), height: K.h, rides, fits: K.fit.map(([p, v]) => p === 'warp' ? { param: 'warp sheave scale at 1.05', value: FIX.davit.sheaveK, range: FITS.warp.sheaveK, ok: inR(FIX.davit.sheaveK, FITS.warp.sheaveK) } : fitRow(p, v)) };
        if (K.d > 0) { const hw = K.w / 2, rt = [fwd[1], -fwd[0]], pt = (u, v) => [r2(stand[0] + rt[0] * u + fwd[0] * v), r2(stand[1] + rt[1] * u + fwd[1] * v)];
          rec.footprint = [pt(-hw, K.stand), pt(hw, K.stand), pt(hw, K.stand + K.d), pt(-hw, K.stand + K.d)];
          if (f.kind !== 'boardStep') blk.push({ shape: 'poly', poly: rec.footprint, h: K.h, kind: f.kind, module: M.id, rides }); }
        FL.push(rec);
        for (const v of K.verbs) V.push(Object.assign({ id: id + '.' + v, verb: v, fixture: id, module: M.id, stand, yaw: rec.yaw, rides, fits: rec.fits }, SEQ[v]())); } }
    for (const L of gp.CLIMB) { const st = r3(L.topZ - TOPOUT), rootXY = [L.root.x, L.root.y], wet = L.atTide.footInWater, swim = L.kind === 'swim ladder';
      V.push({ id: L.id + '.climbDown', verb: swim ? 'swimIn' : 'climbDown', ladder: L.id, module: L.module, stand: [L.exit.x, L.exit.y, L.exit.z], yaw: r2(L.faceYaw + 180), rides: L.rideGroup,
        seq: [{ clip: 'idle', turnTo: L.faceYaw, note: 'face the deck, back to the water' }, { join: 'blend', ms: 300, to: [rootXY[0], rootXY[1], st], request: 'ladderOn' },
          { clip: 'ladderDown', loop: true, opts: L.opts, rootMotion: [0, 0, -LADDER_RATE], until: 'root z at the station wanted (bottom rung ' + L.bottomRungZ + ', or the hull sole)' },
          { clip: wet ? 'tread' : 'idle', note: wet ? 'the pelvis is under the water: tread at the ladder root' : 'off onto a hull', request: wet ? null : 'ladderToHull' }], fits: [fitRow('rung', L.rung)] });
      V.push({ id: L.id + '.climbUp', verb: swim ? 'swimOut' : 'climbUp', ladder: L.id, module: L.module, stand: [rootXY[0], rootXY[1], L.bottomRungZ], yaw: L.faceYaw, rides: L.rideGroup,
        seq: [wet ? { clip: 'tread', then: 'blend 300 ms', request: 'treadToLadder' } : null, { clip: 'ladderDown', loop: true, reverse: true, opts: L.opts, rootMotion: [0, 0, LADDER_RATE], until: 'root z = ' + st + ', the top station' },
          { clip: 'board', once: true, opts: { railZ: TOPOUT }, advance: { m: 0.60, over: [0.28, 0.88] }, note: 'the left hand takes the grab hoop at the deck edge' }].filter(Boolean),
        fits: [fitRow('rung', L.rung), fitRow('rail', TOPOUT)] }); }
    const fitsOf = (p) => p.seq.filter(q => q.clip === 'board' || q.clip === 'boardDown').map(q => fitRow(q.clip === 'board' ? 'stepUp' : q.reverse ? 'stepUpNoGrip' : 'stepDown', q.opts.railZ));
    for (const B of gp.BOARD) { const lad = B.step < 0 ? B.ladder : null, A = B.aboard || stepPlan(B.step, { ladder: lad, stepBox: true }), Z = B.ashore || stepPlan(-B.step, { ladder: lad });
      V.push({ id: B.berth + '.stepAboard', verb: 'stepAboard', berth: B.berth, hull: B.hull, stand: [B.at[0], B.at[1], B.deckZ], yaw: B.yaw, rides: B.rides, step: B.step, how: A.how, seq: A.seq, fits: fitsOf(A), fitsCreatorBodies: A.fits, boardingStep: A.boardingStep || null, note: A.note || null });
      V.push({ id: B.berth + '.stepAshore', verb: 'stepAshore', berth: B.berth, hull: B.hull, stand: [B.at[0], B.at[1], B.hullSoleZ], yaw: r2(B.yaw + 180), rides: 'the hull', step: r3(-B.step), how: Z.how, seq: Z.seq, fits: fitsOf(Z), fitsCreatorBodies: Z.fits, alt: Z.alt || null, note: Z.note || null }); }
    for (const T of gp.TIE) { const f = [fitRow('rest', T.opts.lift)];
      V.push({ id: T.id + '.tieUp', verb: 'tieUp', tie: T.id, module: T.module, stand: T.stand, yaw: T.yaw, rides: T.rideGroup, seq: [{ clip: 'reach', once: true, opts: T.opts }, { clip: 'haul', loop: true, until: 'the line is fast' }], fits: f });
      V.push({ id: T.id + '.castOff', verb: 'castOff', tie: T.id, module: T.module, stand: T.stand, yaw: T.yaw, rides: T.rideGroup, seq: [{ clip: 'reach', once: true, opts: T.opts }, { clip: 'toss', once: true, opts: { workZ: 0.93 }, note: 'the line goes aboard at tossRelease 0.52' }], fits: f }); }
    for (const Wk of gp.WALK) if (Wk.kind === 'gangway') for (const dn of [true, false])
      V.push({ id: Wk.module + (dn ? '.walkDown' : '.walkUp'), verb: 'walkGangway', gangway: Wk.module, dir: dn ? 'hinge \u2192 toe' : 'toe \u2192 hinge', slopeDeg: Wk.slopeDeg, walkable: Wk.walkable, comfortable: Wk.comfortable,
        seq: [{ clip: 'walk', speed: r3(WALK * Math.cos(Wk.slopeDeg * G.DEG)), rootZ: 'linear along the ramp; the toe end rides the float', request: 'walkSlope' }] });
    gp.FIXTURES = FL; gp.VERBS = V; gp.CHAR_FIT = fitReport(); gp.CHAR_REQUESTS = REQUESTS;
    return gp;
  }
  function fitReport() {
    const rows = [Object.assign({ fixture: 'ladder', what: 'rung spacing' }, fitRow('rung', CHAR.rung)), Object.assign({ fixture: 'ladder', what: 'top-out: board at three rungs' }, fitRow('rail', TOPOUT)),
      { fixture: 'boarding', what: 'step down \u00b7 boardDown', param: 'stepDown', value: '\u2264 1.30', range: STEP.down, ok: true },
      { fixture: 'boarding', what: 'step up \u00b7 board, hand on the edge', param: 'stepUp', value: '0.549\u20130.724', range: STEP.up, ok: true },
      { fixture: 'boarding', what: 'lower step up \u00b7 boardDown backward', param: 'stepUpNoGrip', value: '< 0.549', range: STEP.upNoGrip, ok: true },
      Object.assign({ fixture: 'cleat', what: 'reach to the horn' }, fitRow('rest', r3(G.FIT.cleat.h - 0.025 - REACH.gripRise))), Object.assign({ fixture: 'bollard', what: 'reach to the head' }, fitRow('rest', r3(G.FIT.bollard.h - 0.10 - REACH.gripRise)))];
    for (const k in FIX) for (const [p, v] of FIX[k].fit) rows.push(Object.assign({ fixture: FIX[k].label, what: p }, p === 'warp' ? { param: 'warp sheave scale at 1.05', value: FIX.davit.sheaveK, range: FITS.warp.sheaveK, ok: inR(FIX.davit.sheaveK, FITS.warp.sheaveK) } : fitRow(p, v)));
    rows.push({ fixture: 'gangway', what: 'slope at chart datum', param: 'deg', value: G.FIT.gang.maxSlope, range: [0, 18.43], ok: true, note: 'v9.2 walk is level: walkSlope requested' });
    return rows;
  }
  root.WharfVerbs2 = { FITS, REACH, FIX, BUILD, AUTO, plan, addFixtures, SEQ, REQUESTS, augment, fitReport, TOPOUT, LADDER_RATE, STEP, stepPlan };
})(typeof globalThis !== 'undefined' ? globalThis : window);
