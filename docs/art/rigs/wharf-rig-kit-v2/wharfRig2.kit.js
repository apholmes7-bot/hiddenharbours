/* Hidden Harbours — WHARF RIG, PASS 2 · THE KIT (globalThis.WharfRig2Kit).
   Harbours made of modules, and the gameplay sidecar every module and harbour publishes.

   HARBOURS  fishing · commercial · jetty. A harbour is a list of modules at a position and a heading (any angle;
     joins are 90°), a bed(x, y) that every pile, wall and chain stops on, the gangways (hinge on a fixed deck,
     toe on a float group), and the berth lines that run across a chain of modules. model('harbour:fishing')
     builds them as ONE model, so a pier shades the float beside it and the water shader takes one shadow.
     Floats that are pinned together (a main float and its fingers) are one group: one rock, one waterplane.

   GAMEPLAY  gameplay(key, o) → hidden-harbours/wharf-gameplay@2, metres in the model frame (a harbour's frame is
     +x east, +y north, +z up at chart datum). Sections: SPRITE · WALK (with slopes, holes and the float's
     ride) · BLOCKERS · CLIMB (ladders, rung by rung, where the climber's root goes) · TIE (every cleat, bollard
     and ring: where to stand, which way to face, character v9.2's reach and haul opts) · BOARD (per berth, the
     step at this tide and the clip that takes it) · BERTHS (hull, class, footprint, depth at datum) · FLOAT
     (the 16-frame rock and rockFor) · SOCKETS · WATER · SNOW · SHADOW · SORT · CHAR.                            */
(function (root) {
  'use strict';
  const G = root.WharfGeo2; if (!G || !G.resolve) throw new Error('wharfRig2.kit.js: load the geo and fam files first');
  const PI2 = Math.PI / 2, r2 = (v) => Math.round(v * 100) / 100, r3 = (v) => Math.round(v * 1000) / 1000;

  // ============================ HARBOURS =====================================================================
  const HARBOURS = {
    fishing: { label: 'Fishing harbour', tideRange: 1.8, note: 'A crib wharf on the beach, a pile pier to deep water, a gangway down to a float with three finger slips, a slipway and a riprap edge. Land to the south at 2.75 m, the sea to the north.',
      extent: [-22, -6, 22, 33],
      bed: (x, y) => y < -2 ? 2.75 : y < 10 ? 2.75 - (y + 2) * 0.3375 : -1.3 - Math.min(2.4, (y - 10) * 0.3),
      layout(b) {
        const deck = Math.round((b.tideRange + 1.0) / 0.05) * 0.05, gL = +G.gangLength(deck - 0.45).toFixed(1), fy = [9.2, 13.9, 18.6], hingeY = 9.5, fx = 10.9;
        const mods = [
          { id: 'crib', key: 'crib', at: [-8, 1.5], rot: 0, o: { bays: 4, bayLen: 3.0, width: 5, faces: ['water'], open: { water: [[4.2, 6.2]] } } },
          { id: 'pier', key: 'pier', at: [1.5, 14.0], rot: PI2, o: { bays: 10, bayLen: 3.0, width: 4.2, faces: ['water'], open: { shore: [[hingeY - 14 - 0.8, hingeY - 14 + 0.8]] } } },
          { id: 'float', key: 'float', at: [fx, 14], rot: PI2, grp: 1, o: { bays: 4, bayLen: 3.0, width: 2.4, hull: 'timber', faces: ['water'], guideFace: 'water',
            open: { water: [[hingeY - 14 - 0.8, hingeY - 14 + 0.8]], shore: fy.map(y => [y - 14 - 0.7, y - 14 + 0.7]) } } },
          ...fy.map((y, k) => ({ id: 'finger' + k, key: 'finger', at: [fx + 4.2, y], rot: 0, grp: 1, o: { bays: 2, bayLen: 3.0, width: 1.2, freeboard: 0.45, ends: { nx: 'join' }, faces: ['water'], guideFace: 'end' } })),
          { id: 'slip', key: 'slipway', at: [-18, 4.5], rot: PI2, o: { run: 13, width: 3.6, deckZ: 2.8, toeZ: -1.0 } },
          { id: 'edge', key: 'riprap', at: [12, 0], rot: 0, o: { bays: 5, bayLen: 3.0, width: 7, deckZ: 2.75, crestBeam: false } },
        ];
        // the float's near edge sits 0.5 m short of the toe at chart datum (the shortest reach), so the rollers never leave it
        return { mods, gangs: [{ id: 'gangway', hinge: [3.62, hingeY, deck], u: [1, 0], L: gL, W: 1.24, floatGrp: 1, land: [fx - 0.4, hingeY] }] };
      } },
    commercial: { label: 'Commercial quay', tideRange: 1.8, note: 'Three 48 m steel-sheet quay modules joined into one 144 m face on 5.6 m of water: the coastal packet, a stern trawler and a side dragger berth on it at once.',
      extent: [-76, -14, 76, 40], bed: (x, y) => y < 5 ? 2.8 : -5.6,
      layout(b) {
        const bp = G.berthPlan(-72, 72, 'quay', ['packet', 'trawler', 'dragger'], 5.6), mods = [];
        for (let k = 0; k < 3; k++) { const cx = -48 + k * 48, sh = { water: Object.assign({}, bp, { list: bp.list.map(e => shiftBerth(e, -cx)) }) };
          mods.push({ id: 'quay' + k, key: 'quay', at: [cx, 0], rot: 0, o: { face: 'steelSheet', curb: 'yellow', bays: 12, bayLen: 4.0, width: 10, ends: { nx: k ? 'join' : 'free', px: k < 2 ? 'join' : 'free' }, berthPlanned: sh } }); }
        return { mods, gangs: [], lines: [{ id: 'quayFace', at: [-72, 5], to: [72, 5], plan: bp }] };
      } },
    jetty: { label: 'Tanker jetty', tideRange: 1.8, note: 'The gas tanker’s terminal: a loading platform on steel piles, breasting dolphins with panel fenders, mooring dolphins, and the catwalks between them. Nothing here is walked to from shore in this plate.',
      extent: [-62, -26, 62, 30], bed: () => -9,
      layout(b) {
        const deck = Math.round((b.tideRange + 1.8) / 0.05) * 0.05, face = 7.2, tb = { list: [{ id: 'berth0', hull: 'tanker', cls: 'E', label: 'gas tanker', loa: 110, beam: 17.4, draft: 6.2, gun: 3.4, sole: 2.4, x: 0, x0: -55, x1: 55, slot: [-65, 65], fits: true, afloatAtDatum: true, ukc: 1.5, standOff: 1.2, line: G.CLASSES.E.line, fender: G.CLASSES.E.fender, fenderX: [], lines: [] }], faceLen: 130, kind: 'jetty', utilisation: 1 };
        const mods = [{ id: 'platform', key: 'pier', at: [0, 0], rot: 0, o: { struct: 'steelPile', deck: 'concrete', bays: 8, bayLen: 3.0, width: 12, deckZ: deck, curb: 'yellow', rail: 'pipe', railSides: ['shore', 'ends'], faces: ['water'], fittings: { fender: 0 }, berthPlanned: { water: shiftList(tb, 0) } } }];
        for (const sg of [-1, 1]) {
          mods.push({ id: 'breast' + (sg > 0 ? 'E' : 'W'), key: 'dolphin', at: [sg * 22, 4.0], rot: 0, o: { style: 'steel', role: 'breasting', width: 5, deckZ: deck, open: { nx: sg > 0 ? [[0, 1]] : [], px: sg < 0 ? [[0, 1]] : [], shore: [[0, 1]] } } });
          mods.push({ id: 'moor' + (sg > 0 ? 'E' : 'W'), key: 'dolphin', at: [sg * 48, -4], rot: 0, o: { style: 'steel', role: 'mooring', width: 3.6, deckZ: deck, open: { nx: sg > 0 ? [[0, 1]] : [], px: sg < 0 ? [[0, 1]] : [] } } });
          mods.push({ id: 'walkA' + (sg > 0 ? 'E' : 'W'), key: 'catwalk', at: [sg * 15.75, 4.0], rot: 0, o: { run: 7.5, width: 1.2, deckZ: deck } });
          const ax = sg * 24.5, ay = 4.0, bx = sg * 46.2, by = -4, L = Math.hypot(bx - ax, by - ay);
          mods.push({ id: 'walkB' + (sg > 0 ? 'E' : 'W'), key: 'catwalk', at: [(ax + bx) / 2, (ay + by) / 2], rot: Math.atan2(by - ay, bx - ax), o: { run: +L.toFixed(2), width: 1.2, deckZ: deck } });
        }
        return { mods, gangs: [], lines: [{ id: 'tankerBerth', at: [-55, face], to: [55, face], plan: tb }] };
      } },
  };
  function shiftBerth(e, dx) { const o = Object.assign({}, e); for (const k of ['x', 'x0', 'x1']) o[k] = r2(e[k] + dx); o.slot = e.slot.map(v => r2(v + dx)); o.fenderX = e.fenderX.map(v => r2(v + dx)); o.lines = e.lines.map(v => r2(v + dx)); return o; }
  function shiftList(bp, dx) { return Object.assign({}, bp, { list: bp.list.map(e => shiftBerth(e, dx)) }); }

  // ============================ MODULES THAT JOIN ============================================================
  /* snap(a, sa, b, sb, o): place module b ({id, key, o}) so that its socket sb meets socket sa of the placed module a
     ({id, key, at, rot, o}), facing it, and mark the join on both. An end socket joins: its end wall, rail return and first
     pile row drop. A side socket opens a span in the bull rail, the berths and the fixtures as wide as what butts onto it.
     o.slide moves the meeting point along a's face (its own x on a side socket, its own y on an end). b takes a's tide range;
     a fixed b takes a's deck height, a floating b a's freeboard and float group, so the decks meet flush and rock as one.
     A float never snaps to a fixed deck: that is a gangway (gangwayFrom). Returns b with at and rot set.                */
  const sockOf = (m) => { const s = G.resolve(m.key, m.o || {}); return { s, k: G.sockets(s) }; };
  const isEnd = (k) => k.id === 'nx' || k.id === 'px', rot2 = (v, c, s) => [v[0] * c - v[1] * s, v[0] * s + v[1] * c];
  function snap(a, sa, b, sb, o) {
    o = o || {}; a.o = a.o || {}; b.o = Object.assign({}, b.o || {});
    if (a.o.tideRange != null && b.o.tideRange == null) b.o.tideRange = a.o.tideRange;
    const A = sockOf(a), kA = A.k.find(k => k.id === sa), fam = (G.PRESETS[b.key] || {}).family || b.key, bFloat = !!G.FLOATING[fam];
    if (!kA) throw new Error('snap: ' + (a.id || a.key) + ' has no socket ' + sa + ' (it has ' + A.k.map(k => k.id).join(' ') + ')');
    if (bFloat !== !!A.s.floating) throw new Error('snap: a float meets a fixed deck through a gangway (gangwayFrom), not a socket');
    if (bFloat) { if (b.o.freeboard == null) b.o.freeboard = A.s.freeboard; b.grp = a.grp || 1; } else if (b.o.deckZ == null && a.o.deckZ != null) b.o.deckZ = a.o.deckZ;
    const B = sockOf(b), kB = B.k.find(k => k.id === sb);
    if (!kB) throw new Error('snap: ' + (b.id || b.key) + ' has no socket ' + sb + ' (it has ' + B.k.map(k => k.id).join(' ') + ')');
    const sl = o.slide || 0, pa = isEnd(kA) ? [kA.at[0], kA.at[1] + sl] : [kA.at[0] + sl, kA.at[1]], ra = a.rot || 0, ca = Math.cos(ra), sa2 = Math.sin(ra);
    const pw = rot2(pa, ca, sa2), nw = rot2(kA.n, ca, sa2); pw[0] += a.at[0]; pw[1] += a.at[1];
    let rb = Math.atan2(-nw[1], -nw[0]) - Math.atan2(kB.n[1], kB.n[0]); rb = Math.atan2(Math.sin(rb), Math.cos(rb));
    const q = rot2([kB.at[0], kB.at[1]], Math.cos(rb), Math.sin(rb));
    b.at = [r3(pw[0] - q[0]), r3(pw[1] - q[1])]; b.rot = +rb.toFixed(6);
    const mark = (m, k, x, w) => { if (isEnd(k)) { m.o.ends = Object.assign({}, m.o.ends, { [k.id]: 'join' }); return; }
      m.o.open = Object.assign({}, m.o.open); m.o.open[k.face] = (m.o.open[k.face] || []).concat([[r2(x - w / 2), r2(x + w / 2)]]); };
    mark(a, kA, pa[0], isEnd(kB) ? B.s.W : kB.w); mark(b, kB, kB.at[0], isEnd(kA) ? A.s.W : kA.w);
    return b;
  }
  /* gangwayFrom(a, sa, o): a gangway hung off a fixed module's socket, heading out along its normal. Its length gives 1:3 at
     chart datum onto a float of freeboard o.freeboard (0.45); the toe rolls from reach[0] (datum) to reach[1] (highest water).
     Place the float with its near edge on edgeAt (0.5 m short of the toe at datum); the landing plate is drawn at land.   */
  function gangwayFrom(a, sa, o) {
    o = o || {}; const A = sockOf(a), k = A.k.find(q => q.id === sa);
    if (!k) throw new Error('gangwayFrom: ' + (a.id || a.key) + ' has no socket ' + sa); if (A.s.floating) throw new Error('gangwayFrom: hang a gangway off a fixed deck');
    const sl = o.slide || 0, p = isEnd(k) ? [k.at[0], k.at[1] + sl] : [k.at[0] + sl, k.at[1]], c = Math.cos(a.rot || 0), s = Math.sin(a.rot || 0), pw = rot2(p, c, s), u = rot2(k.n, c, s).map(r3);
    const hinge = [r3(a.at[0] + pw[0]), r3(a.at[1] + pw[1]), A.s.deckZ], fb = o.freeboard != null ? o.freeboard : 0.45, L = o.L || +G.gangLength(hinge[2] - fb).toFixed(1);
    const span = (z) => Math.sqrt(Math.max(0.25, L * L - Math.pow(Math.max(0, hinge[2] - z), 2))), s0 = span(fb), s1 = span(A.s.tideRange + fb), at = (d) => [r3(hinge[0] + u[0] * d), r3(hinge[1] + u[1] * d)];
    return { id: o.id || 'gangway', hinge, u, L, W: o.W || 1.24, floatGrp: o.floatGrp || 1, land: at(s0 + 0.1), edgeAt: at(s0 - 0.5), reach: [r2(s0), r2(s1)] };
  }
  /* define(id, def): register a harbour, 'harbour:<id>' everywhere after (frame, relight, gameplay, the stage). def: label, note,
     tideRange, bed(x, y), and either mods/gangs/lines or layout(base) returning them; extent (x0, y0, x1, y1 in metres) is
     measured from the modules if not given; road(x, y) marks the path on the land for the stage.                          */
  function define(id, def) {
    if (!id || typeof id !== 'string' || !def) throw new Error('WharfRig2Kit.define(id, def)');
    const H = Object.assign({ label: id, note: '', tideRange: 1.8, bed: () => -2 }, def);
    if (!H.layout) { const mods = def.mods || [], gangs = def.gangs || [], lines = def.lines || []; H.layout = () => ({ mods, gangs, lines }); }
    if (!H.extent) { let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9; const L = H.layout({ tideRange: H.tideRange });
      for (const m of L.mods) { const s = G.resolve(m.key, Object.assign({ tideRange: H.tideRange }, m.o)), c = Math.cos(m.rot || 0), sn = Math.sin(m.rot || 0);
        for (const [x, y] of [[-s.L / 2, -s.W / 2], [s.L / 2, -s.W / 2], [s.L / 2, s.W / 2], [-s.L / 2, s.W / 2]]) { const X = m.at[0] + x * c - y * sn, Y = m.at[1] + x * sn + y * c; x0 = Math.min(x0, X); x1 = Math.max(x1, X); y0 = Math.min(y0, Y); y1 = Math.max(y1, Y); } }
      H.extent = [Math.floor(x0 - 3), Math.floor(y0 - 5), Math.ceil(x1 + 3), Math.ceil(y1 + 4)]; }
    HARBOURS[id] = H; if (root.WharfRig2 && root.WharfRig2.clearCache) root.WharfRig2.clearCache(); return 'harbour:' + id;
  }
  const PICK = ['variant', 'season', 'stage', 'age', 'tideRange', 'tide', 'curb', 'rail', 'hull', 'growth'];
  function buildHarbour(id, o) {
    o = o || {}; const H = HARBOURS[id] || HARBOURS.fishing, base = { tideRange: H.tideRange };
    for (const k of PICK) if (o[k] != null) base[k] = o[k];
    const L = H.layout(Object.assign({ tideRange: H.tideRange }, base)), faces = [], modules = [], groups = {};
    for (const m of L.mods) {
      const c = Math.cos(m.rot), sn = Math.sin(m.rot), toH = (x, y) => [m.at[0] + x * c - y * sn, m.at[1] + x * sn + y * c];
      const mo = Object.assign({}, base, m.o, { bed: (x, y) => { const q = toH(x, y); return H.bed(q[0], q[1]); } });
      if (m.o.hull == null && base.hull) mo.hull = base.hull;
      const s = G.resolve(m.key, mo), T = G.frame(s), tmp = [];
      s.grp = s.floating ? (m.grp || 1) : 0; G.CTX.grp = 0; G.CTX.ux = 0;
      G.FAMILIES[s.family].build(tmp, s, T); const P = G.addFittings(tmp, s, T); G.xform(tmp, m.rot, m.at[0], m.at[1], 0);
      for (const f of tmp) faces.push(f);
      const M = { id: m.id, key: m.key, s, P, rot: m.rot, at: m.at, toH, grp: s.grp }; modules.push(M);
      if (s.floating) { const g = s.grp, grp = groups[g] || (groups[g] = { id: g, kind: 'float', members: [], chains: [], fb: s.freeboard, ax: m.rot, corners: [] });
        grp.members.push(m.id); const hx = s.L / 2, hy = s.W / 2; for (const [x, y] of [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]]) grp.corners.push(toH(x, y));
        if (s._chain) { const a = toH(s._chain.anchor[0], s._chain.anchor[1]), t = toH(s._chain.attach[0], s._chain.attach[1]); grp.chains.push({ anchor: [a[0], a[1], s._chain.anchor[2]], attach: [t[0], t[1], s._chain.attach[2]] }); } }
    }
    for (const g in groups) { const grp = groups[g], ca = Math.cos(grp.ax), sa = Math.sin(grp.ax); let u0 = 1e9, u1 = -1e9, v0 = 1e9, v1 = -1e9;
      for (const [x, y] of grp.corners) { const u = x * ca + y * sa, v = -x * sa + y * ca; u0 = Math.min(u0, u); u1 = Math.max(u1, u); v0 = Math.min(v0, v); v1 = Math.max(v1, v); }
      const uc = (u0 + u1) / 2, vc = (v0 + v1) / 2; grp.cx = uc * ca - vc * sa; grp.cy = uc * sa + vc * ca; grp.Lx = u1 - u0; grp.By = v1 - v0; }
    // gangway landing plates ride their float
    for (const gw of L.gangs) { const g = gw.floatGrp, grp = groups[g]; if (!grp) continue; G.at('landing', g, 0);
      const ux = gw.u[0], uy = gw.u[1], px = -uy, py = ux, p = gw.land, z = grp.fb + 0.006, a = 0.65, b0 = -0.35, b1 = 1.0;
      faces.push(G.F([[p[0] + ux * b0 + px * -a, p[1] + uy * b0 + py * -a, z], [p[0] + ux * b1 + px * -a, p[1] + uy * b1 + py * -a, z], [p[0] + ux * b1 + px * a, p[1] + uy * b1 + py * a, z], [p[0] + ux * b0 + px * a, p[1] + uy * b0 + py * a, z]], 'galv', 0, 0.03, null, null)); }
    G.CTX.grp = 0;
    const s0 = G.resolve('pier', base); s0.family = 'harbour'; s0.label = H.label;
    return { key: 'harbour:' + id, s: s0, T: G.frame(s0), faces, P: null, groups, gangs: L.gangs.map(g => Object.assign({}, g)), modules, lines: L.lines || [], tideRange: s0.tideRange, harbour: H, id, bed: H.bed };
  }

  // ============================ GAMEPLAY =====================================================================
  function gameplay(key, o) {
    o = o || {}; const W = root.WharfRig2, fr = W.frame(key, o), mdl = fr.mdl, CH = G.CHAR, FIT = G.FIT, tide = fr.tide;
    const walk = [], blockers = [], climb = [], tie = [], board = [], berths = [], sockets = [], holes = [], fenders = [];
    const zOf = (M, zl, x, y) => { if (!M.s.floating) return zl; const P = fr.poses[M.grp]; return P ? P.fwd([x, y, zl])[2] : zl + tide; };
    const dirH = (M, v) => { const c = Math.cos(M.rot), s = Math.sin(M.rot); return [v[0] * c - v[1] * s, v[0] * s + v[1] * c]; };
    const yawOf = (d) => r2(Math.atan2(d[0], d[1]) / G.DEG);
    for (const M of mdl.modules) {
      const s = M.s, P = M.P, hx = s.L / 2, hy = s.W / 2, fam = s.family, top = s.floating ? s.freeboard : s.deckZ, H = (x, y) => M.toH(x, y).map(r2);
      const poly = (x0, y0, x1, y1) => [[x0, y0], [x1, y0], [x1, y1], [x0, y1]].map(([x, y]) => H(x, y));
      const surf = { pier: s.deck === 'concrete' ? 'concrete' : 'plank', crib: s.cap === 'concrete' ? 'concrete' : 'plank', quay: s.face === 'block' ? 'granite setts' : 'concrete', float: s.hull === 'plastic' ? 'hdpe grid' : s.hull === 'concrete' ? 'concrete' : 'plank', finger: s.hull === 'plastic' ? 'hdpe grid' : 'plank', slipway: 'ribbed concrete', catwalk: 'steel grating', dolphin: 'concrete', riprap: 'armour stone' }[fam];
      if (fam === 'slipway') { const x0 = -hx, x1 = hx, z0 = s.deckZ, z1 = s.toeZ, wet = z1 < tide ? r2(x0 + (x1 - x0) * (z0 - tide) / (z0 - z1)) : null;
        walk.push({ module: M.id, poly: poly(x0, -hy + 0.26, x1, hy - 0.26), surface: surf, z0: r2(z0), z1: r2(z1), axis: dirH(M, [1, 0]).map(r3), slopeDeg: r2(Math.atan2(z0 - z1, s.L) / G.DEG), zAt: 'z0 + (z1 − z0)·t, t = 0 at the crest, 1 at the toe', underwaterFrom_t: wet == null ? null : r3((wet - x0) / s.L), launchable: tide > z1 + 0.35 }); }
      else if (fam === 'riprap') walk.push({ module: M.id, poly: poly(-hx, -hy, hx, hy), surface: surf, walkable: false, note: 'loose stone: scramble, not walk' });
      else if (fam === 'dolphin' && s.style === 'timber') fenders.push({ type: 'dolphin', x: r2(M.at[0]), y: r2(M.at[1]), r: r2(FIT.dolphin.spread + FIT.dolphin.r), module: M.id, note: 'a pile cluster: a fender and a mooring post, not a deck' });
      else if (fam !== 'gangway') walk.push({ module: M.id, poly: poly(-hx, -hy, hx, hy), surface: surf, z: s.floating ? r3(zOf(M, top, M.at[0], M.at[1])) : r2(top), rides: s.floating ? M.grp : null, clearWidth: r2(s.W - (fam === 'catwalk' ? 0.1 : 0)) });
      for (const h of (s._holes || [])) holes.push({ module: M.id, poly: poly(h[0], h[1], h[2], h[3]), kind: 'missing boards', fall: true });
      if (!P) continue;
      for (const b of P.bollards) { const q = H(b.x, b.y); blockers.push({ shape: 'circle', x: q[0], y: q[1], r: FIT.bollard.rBase + 0.04, h: FIT.bollard.h, kind: 'bollard', module: M.id }); }
      for (const c of P.cleats) { const q = H(c.x, c.y); blockers.push({ shape: 'circle', x: q[0], y: q[1], r: 0.26, h: FIT.cleat.h, kind: 'cleat', low: true, stepOver: true, module: M.id }); }
      for (const r of P.rails) blockers.push({ shape: 'wall', a: H(r[0], r[1]), b: H(r[2], r[3]), h: FIT.rail.h, kind: 'rail', module: M.id });
      for (const g of (s._guides || [])) { const q = H(g.x, g.y); blockers.push({ shape: 'circle', x: q[0], y: q[1], r: g.r + 0.10, h: r2(g.top - top), kind: 'guide pile', module: M.id }); }
      // CLIMB: every ladder, rung by rung; a float's swim ladder rides it
      const lads = P.ladders.map(l => ({ l, bot: -FIT.ladder.below, fl: false }));
      if (s._swimX != null) lads.push({ l: { x: s._swimX, y: hy, side: 1, face: 'water', berth: null }, bot: -0.85, fl: true });
      lads.forEach((L2, k) => { const l = L2.l, out = dirH(M, [0, l.side]), plane = H(l.x, l.y + l.side * FIT.ladder.off), rungs = G.rungList(top, L2.bot), zs = rungs.map(z => r3(zOf(M, z, plane[0], plane[1])));
        const topZ = r3(zOf(M, top, plane[0], plane[1])), root3 = [r2(plane[0] + out[0] * CH.standoff), r2(plane[1] + out[1] * CH.standoff)], exit = H(l.x, l.y - l.side * 0.55);
        climb.push({ id: M.id + '.ladder' + k, module: M.id, kind: L2.fl ? 'swim ladder' : 'wharf ladder', berth: l.berth ? M.id + '.' + l.face + '.' + l.berth : null, plane, out: out.map(r3), faceYaw: yawOf([-out[0], -out[1]]),
          rung: FIT.ladder.rung, width: FIT.ladder.w, standoff: CH.standoff, rungZ: zs, topZ, bottomRungZ: zs[zs.length - 1], grabZ: r3(topZ + FIT.ladder.grab),
          root: { x: root3[0], y: root3[1], zFrom: zs[zs.length - 1], zTo: zs[0], note: 'the figure faces the ladder (faceYaw) with its root standoff out from the rung plane; root z = the rung its lower foot is on' },
          exit: { x: exit[0], y: exit[1], z: topZ, note: 'standing spot on the deck behind the grab hoops' }, rideGroup: L2.fl ? M.grp : null,
          atTide: { rungsDry: zs.filter(z => z > tide).length, rungsWet: zs.filter(z => z <= tide).length, footInWater: zs[zs.length - 1] <= tide, swimmerCanReach: zs.some(z => z <= tide && z > tide - 0.9) },
          clip: 'ladderDown', play: 'forward to go down, reversed to climb; root motion ' + r3(2 * FIT.ladder.rung / 1.1) + ' m/s', opts: { rung: FIT.ladder.rung, ladderW: FIT.ladder.w },
          fitsCreatorBodies: FIT.ladder.rung >= CH.rungFit[0] && FIT.ladder.rung <= CH.rungFit[1] }); });
      // TIE: stand inboard, face the water, v9.2's reach puts the hand on the horn
      const tieOf = (t, kind, hz, holds, line) => { const out = dirH(M, [0, t.side]), yaw = yawOf(out), fx = out[0], fy = out[1], rx = fy, ry = -fx, at3 = H(t.x, t.y), z = zOf(M, t.z != null ? t.z : top, at3[0], at3[1]), lift = Math.max(0, hz - CH.gripRise);
        const stand = [r2(at3[0] - rx * CH.want[0] - fx * CH.want[1]), r2(at3[1] - ry * CH.want[0] - fy * CH.want[1])];
        tie.push({ id: M.id + '.' + kind + tie.length, type: kind, module: M.id, berth: t.berth ? M.id + '.' + t.face + '.' + t.berth : null, lead: t.lead || null, at: [at3[0], at3[1], r3(z + hz)], stand: [stand[0], stand[1], r3(z)], yaw,
          clip: 'reach', opts: { lift: r3(lift), want: [CH.want[0], CH.want[1], r3(lift + CH.gripRise)] }, then: { clip: 'haul', leadYaw: yaw }, line, holds,
          fitsCreatorBodies: lift >= CH.reachLift[0] && lift <= CH.reachLift[1], rideGroup: s.floating ? M.grp : null, dolphin: fam === 'dolphin' ? s.role : undefined }); };
      for (const c of P.cleats) tieOf(c, 'cleat', FIT.cleat.h - 0.025, s.floating ? 'A · B' : 'A · B · C', '≤ 22 mm');
      for (const b of P.bollards) tieOf(b, 'bollard', FIT.bollard.h - 0.10, fam === 'dolphin' ? 'D · E' : 'C · D', fam === 'dolphin' ? '40 mm + wires' : '≤ 40 mm');
      for (const r of P.rings) tieOf(r, 'ring', 0.03, 'A', '≤ 16 mm');
      for (const t of P.tyres) { const q = H(t.x, t.y); fenders.push({ type: 'tyre', x: q[0], y: q[1], r: 0.5, module: M.id }); }
      for (const t of P.foams) { const q = H(t.x, t.y); fenders.push({ type: 'foam', x: q[0], y: q[1], r: 0.28, module: M.id }); }
      for (const t of P.panels) { const q = H(t.x, t.y); fenders.push({ type: 'panel', x: q[0], y: q[1], w: FIT.panel.w, module: M.id }); }
      // BERTHS + BOARD
      for (const face in P.berths) { const side = face === 'water' ? 1 : -1, out = dirH(M, [0, side]), bp = P.berths[face];
        for (const b of bp.list) { if (b.x < -hx - 0.1 || b.x > hx + 0.1) continue;
          const standOff = b.standOff != null ? b.standOff : s.floating ? 0.12 : (b.cls === 'D' ? FIT.panel.stand + FIT.panel.t : 0.45), ctr = H(b.x, side * (hy + standOff + b.beam / 2)), deckZ = zOf(M, top, ctr[0], ctr[1]);
          // the shallowest bed under the hull: where its keel touches first. A drying berth's hull sits on it below that tide.
          let bu = -1e9; for (const fx of [0, 0.5, 1]) for (const fy of [0, 0.5, 1]) bu = Math.max(bu, s.bedAt(b.x0 + (b.x1 - b.x0) * fx, side * (hy + standOff + b.beam * fy)));
          const floatsFrom = r2(bu + b.draft + G.CLASSES[b.cls].ukc), wl = Math.max(tide, bu + b.draft);
          const bid = M.id + '.' + face + '.' + b.id, sole = wl + b.sole, step = r3(sole - deckZ), near = climb.find(c => c.module === M.id && c.berth === bid), lad = near && step < 0 ? near.id : null;
          // the step each way, in clips every creator body makes (WharfVerbs2.stepPlan, measured over the 375)
          const SP = root.WharfVerbs2 && root.WharfVerbs2.stepPlan, aboard = SP ? SP(step, { ladder: lad, stepBox: true }) : null, ashore = SP ? SP(-step, { ladder: lad }) : null;
          const clip = aboard ? (aboard.how === 'ladder' ? 'ladder' : aboard.first || 'none') : Math.abs(step) < 0.06 ? 'walk' : Math.abs(step) > CH.railClamp[1] ? 'ladder' : step > 0 ? 'board' : 'boardDown';
          berths.push({ id: bid, module: M.id, face, hull: b.hull, label: b.label, cls: b.cls, loa: b.loa, beam: b.beam, draft: b.draft,
            footprint: { centre: ctr, heading: dirH(M, [1, 0]).map(r3), length: b.loa, width: b.beam }, line: b.line, fender: b.fender, bedUnder: r2(bu), depthAtDatum: r2(-bu),
            afloatAtDatum: floatsFrom <= 0, floatsFrom, dries: floatsFrom > 0, afloatNow: tide >= floatsFrom, aground: tide < bu + b.draft });
          board.push({ berth: bid, hull: b.hull, at: H(b.x, side * hy), yaw: yawOf(out), deckZ: r3(deckZ), hullSoleZ: r3(sole), step, clip, opts: aboard ? aboard.opts : clip === 'board' || clip === 'boardDown' ? { railZ: r3(Math.abs(step)) } : null,
            fitsCreatorBodies: aboard ? aboard.fits && ashore.fits : clip === 'walk' || (clip !== 'ladder' && Math.abs(step) >= CH.railFit[0] && Math.abs(step) <= CH.railFit[1]),
            how: aboard ? { aboard: aboard.how, ashore: ashore.how } : null, aboard, ashore, ladder: near ? near.id : null, rides: s.floating ? M.grp : null }); } }
      for (const k of G.sockets(s)) { const q = M.toH(k.at[0], k.at[1]), n = dirH(M, k.n); sockets.push({ id: M.id + '.' + k.id, module: M.id, at: [r2(q[0]), r2(q[1]), k.at[2]], n: n.map(r3), w: k.w, type: k.type, joined: !!k.joined, face: k.face || null }); }
    }
    for (const g of fr.gangs) { const h = g.land; walk.push({ module: g.id, kind: 'gangway', hinge: g.hinge || null, toe: h.map(r3), L: g.L, span: g.span, drop: g.dz, slopeDeg: g.angDeg, surface: 'alloy treads @ 0.30 m', clearWidth: FIT.gang.clear, rails: [FIT.gang.mid, FIT.gang.rail],
      walkable: g.angDeg <= FIT.gang.steep, comfortable: g.angDeg <= FIT.gang.maxSlope, note: 'hinge fixed, toe rolls on the float deck; z along it is linear hinge → toe' }); }
    // the toe on its float at the two extremes of the tide: the shortest reach is at chart datum, the longest at highest water
    mdl.gangs.forEach((gs, k) => { const g = fr.gangs[k], grp = gs.floatGrp != null ? mdl.groups[gs.floatGrp] : null, w = g && walk.find(q => q.kind === 'gangway' && q.module === g.id); if (!grp || !w) return;
      const span = (z) => Math.sqrt(Math.max(0.25, gs.L * gs.L - Math.pow(Math.max(0, gs.hinge[2] - z), 2))), ca = Math.cos(grp.ax || 0), sa = Math.sin(grp.ax || 0);
      const mar = (sp) => { const x = gs.hinge[0] + gs.u[0] * sp - grp.cx, y = gs.hinge[1] + gs.u[1] * sp - grp.cy, u = x * ca + y * sa, v = -x * sa + y * ca; return r2(Math.min(grp.Lx / 2 - Math.abs(u), grp.By / 2 - Math.abs(v))); };
      w.hinge = gs.hinge.map(r3); w.toeOnFloat_m = { datum: mar(span(grp.fb)), highestWater: mar(span(mdl.tideRange + grp.fb)), rule: 'the toe rollers stay at least 0.3 m inside the float deck at every tide' }; });
    // a dolphin's bollard takes the lines of the berth it stands off; every berth lists its ties and its ladder
    const d2 = (a, b) => (a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]);
    for (const t of tie) if (t.dolphin && !t.berth && berths.length) { let best = null, bd = 1e18; for (const b of berths) { const d = d2(t.at, b.footprint.centre); if (d < bd) { bd = d; best = b; } }
      if (best && Math.sqrt(bd) < best.loa / 2 + 30) { t.berth = best.id; t.lead = t.dolphin === 'mooring' ? 'head · stern' : 'breast · spring'; } }
    for (const b of berths) { b.ties = tie.filter(t => t.berth === b.id).map(t => t.id); const L = climb.find(c => c.berth === b.id); b.ladder = L ? L.id : null; }
    const FLOAT = {}; for (const g in mdl.groups) { const loop = []; for (let f = 0; f < W.LOOP; f++) { const m = W.frame(key, Object.assign({}, o, { frame: f })).motion[g]; loop.push({ heave: r3(m.heave), rollDeg: r2(m.roll / G.DEG), pitchDeg: r2(m.pitch / G.DEG) }); }
      const grp = mdl.groups[g]; FLOAT[g] = { group: +g, members: grp.members || ['m0'], freeboard: grp.fb, waterplane: [r2(grp.Lx), r2(grp.By)], centre: [r2(grp.cx), r2(grp.cy)], axisDeg: r2((grp.ax || 0) / G.DEG), loop,
        law: 'heave, roll, pitch = the waterplane average (sinc) and first moment of three loop-periodic components (1, 2, 1 cycles per 16 frames) running with the water shader toward the shore; roll/pitch ×0.7, heave ×0.9',
        rockFor: 'WharfRig2.rockFor(frame, group, yawDeg) → { roll, pitch } for CharacterIso9.render({ roll, pitch })' }; }
    const gp = {
      schema: 'hidden-harbours/wharf-gameplay@2', rig: 'wharfRig2.js + wharfRig2.geo.js + wharfRig2.fam.js + wharfRig2.verbs.js + wharfRig2.kit.js', exportSymbol: 'WharfRig2', key, label: mdl.s.label,
      generated: 'WharfRig2Kit.gameplay() off the live model — do not hand-edit, regenerate',
      frame: { units: 'metres', axes: mdl.harbour ? '+x east, +y north (the sea), +z up' : '+x along the module, +y its water face, +z up', origin: mdl.harbour ? 'harbour origin at chart datum' : 'module centre at chart datum', scale_px_per_m: fr.ppu, elev_deg: 40, dirs: 8, dir: fr.dir },
      params: { variant: mdl.s.variant, season: mdl.s.season, stage: mdl.s.stage, age: mdl.s.age, tideRange: mdl.tideRange, tide: r3(tide), frame: fr.fi, wind: fr.wind, sea: o.sea || null },
      SPRITE: { cell: [fr.w, fr.h], pivot: [fr.px, fr.py], pivotIs: 'the projection of the model origin (x 0, y 0, z 0): blit at screen(origin) − pivot', loop: W.LOOP, moving: !!fr.mov.length },
      WALK: walk, HOLES: holes, BLOCKERS: blockers, CLIMB: climb, TIE: tie, BOARD: board, BERTHS: berths, FENDERS: fenders, FLOAT, SOCKETS: sockets,
      WATER: { bakedWaterPixels: 0, rule: 'every pixel whose z is under the tide (view "sub") is the water shader’s: draw the sea over it, the first 0.16 m through three steps of water', contact: 'WharfRig2.contact(frame): the waterline, for the foam lace', shade: 'castShadow levels → TerrainLight6 relight o.lv' },
      SNOW: { map: 'frame.snow, one byte per pixel', rule: 'snowed = round(cover × 254) >= byte; 255 never', never: 'vertical faces, growth, sky visibility < 0.18, below tide + 0.25 + 0.2·wind' },
      SHADOW: { levels: '1 canopy (under a deck, stays in overcast) · 2 partial · 3 full', call: 'WharfRig2.castShadow(frame, sky, {z}) or shadeAt(frame, sky, x, y, z)' },
      SORT: { floorLayer: 'lay 0: decks and all under them draw under figures', raised: 'lay 1: rails, bollards, grab hoops, dolphin rails, gangway rails — y-sort each by its base row', channel: 'view "layer"' },
      CHAR: Object.assign({ from: 'export/character-v9.2-import-kit reports/worldfit.txt' }, CH, { built: { rung: FIT.ladder.rung, ladderW: FIT.ladder.w, grab: FIT.ladder.grab, cleatHorn: FIT.cleat.h, bollard: FIT.bollard.h, gangwaySlopeMax: FIT.gang.maxSlope, gangwayClear: FIT.gang.clear, rail: FIT.rail.h } }),
    };
    return root.WharfVerbs2 ? root.WharfVerbs2.augment(gp, fr) : gp;
  }
  // the whole fleet against the harbours: which berth takes which hull, how deep it needs to be
  function fleetTable() { return G.fleetFit({ finger: 1.2, float: 2.5, pier: 3.2, crib: 0.6, quay: 5.6, jetty: 9, slipway: 0.8 }); }

  /* THE COVE, assembled from sockets rather than coordinates: define() + snap() + gangwayFrom(), the way the scene editor or
     the game would build a harbour of its own. Only the crib head, the slipway, the edge and the dolphin are placed by hand. */
  define('cove', { label: 'Cove wharf', tideRange: 1.2,
    note: 'Assembled from sockets, not coordinates: a crib head on the beach, a pier snapped to its water face, a gangway hung off the pier, a cube float at its toe with two fingers snapped to it, a slipway, a sandstone edge and a timber dolphin off the pier head. PEI tides, 1.2 m; land to the south.',
    bed: (x, y) => y < -3 ? 2.15 : y < 6 ? 2.15 - (y + 3) * 0.3056 : -0.6 - Math.min(2.0, (y - 6) * 0.25),
    road: (x, y) => Math.abs(x + 6) < 1.9 && y < -3.6,
    layout(b) {
      const T = { tideRange: b.tideRange }, fb = 0.32;
      const head = { id: 'head', key: 'crib', at: [-6, -1.0], rot: 0, o: Object.assign({ bays: 2, bayLen: 3.2, width: 5, faces: ['water'] }, T) };
      const pier = snap(head, 'water1', { id: 'pier', key: 'pier', o: { bays: 3, bayLen: 3.0, width: 3.0, faces: ['water'] } }, 'nx');
      const gw = gangwayFrom(pier, 'shore2', { freeboard: fb, floatGrp: 1 }), u = gw.u, n = [-u[1], u[0]], hw = 1.25, along = 2.1;
      const fl = { id: 'float', key: 'float', grp: 1, rot: +Math.atan2(-u[0], u[1]).toFixed(6), at: [r3(gw.edgeAt[0] + u[0] * hw + n[0] * along), r3(gw.edgeAt[1] + u[1] * hw + n[1] * along)],
        o: Object.assign({ bays: 3, bayLen: 3.0, width: 2.5, hull: 'plastic', freeboard: fb, curb: 'yellow', faces: ['water'], open: { shore: [[along - 0.8, along + 0.8]] } }, T) };
      const fin = (id) => ({ id, key: 'finger', o: { bays: 2, bayLen: 3.0, width: 1.2, hull: 'plastic', faces: ['water'], guideFace: 'end' } });
      const f0 = snap(fl, 'water0', fin('finger0'), 'nx', { slide: 0.6 }), f1 = snap(fl, 'water2', fin('finger1'), 'nx');
      const px = sockOf(pier).k.find(k => k.id === 'px'), c = Math.cos(pier.rot), s = Math.sin(pier.rot);
      const dol = { id: 'dolphin', key: 'timberDolphin', at: [r2(pier.at[0] + (px.at[0] + 1.4) * c), r2(pier.at[1] + (px.at[0] + 1.4) * s)], rot: 0, o: Object.assign({}, T) };
      const slip = { id: 'slip', key: 'slipway', at: [-15, 2.5], rot: PI2, o: Object.assign({ run: 12, width: 3.6, deckZ: 2.15, toeZ: -0.7 }, T) };
      const edge = { id: 'edge', key: 'redEdge', at: [4.5, -1.8], rot: 0, o: Object.assign({ bays: 3, bayLen: 3.0, width: 5, deckZ: 2.15, crestBeam: false }, T) };
      return { mods: [head, pier, fl, f0, f1, dol, slip, edge], gangs: [gw] };
    } });

  root.WharfRig2Kit = { HARBOURS, buildHarbour, gameplay, fleetTable, shiftBerth, define, snap, gangwayFrom, sockOf };
})(typeof globalThis !== 'undefined' ? globalThis : window);
