/* NINE MILE CREEK, KEY SCENES: THE SOUTH SHORE REDRAWN (hh-nmc-key-scenes/south-shore/, 2026-10-04): the six shingle-style houses on the
   bluff and across Route 91, and their props (globalThis.NmcShoreEstates). Kinds on NmcStores' builder, drawn with the north creek's house
   builder and its lib: load plaza-stores/nmcStores.js, shore-lane/nmcShoreLane.js and its props, and north-creek/nmcCreekHouses.js first.
   Plan metres (e east, n north, z up), the origin on the main body's centre at its main floor; the camera sees each house's south face.
   estateHouse  a creekHouse recipe, and what the shingle style adds to it:
     parts[] with kind 'gambrelN'  a wing under a gambrel whose ridge runs north and south, its gambrel end to the camera:
               { e0 e1 n0 n1 H z0 up roof { p (lower), pu (upper), bk (the knee in from the eave), ov, mat, tex, fascia } side S/N/E/W skip trim }
     towers    [{ e, n, R, z0, H, up, side, trim, faces { S SE E W SW: { g, u } }, roof { ov, kr, kh, ah, mat, tex }, finial }] an octagonal tower
               under a bell-cast roof
     cupolas   [{ e, n, z, w, h, mat, roofMat, vane }] a louvred cupola astride a ridge, its weathervane
     walks     [{ e0, e1, n0, n1, z, zc, rail 'white' | 'chip', deck, curb }] a flat roof walk over a ridge, its balustrade (chip: Chinese
               Chippendale, the lattice of the HGTV house's rails)
     balconies [{ e0, e1, n0, n1, z, rail, open [sides] }] a railed deck on a porch roof
     hearths   [{ e, n, w, h, lit }] an outdoor fireplace in a chimney's south face, its firewood
     xfurn     [{ k planter | bikes | flag | lantern | chaise, ... }] what the creek's furniture lacks
   estatePool  a pool on its terrace: bluestone deck and coping, the water, its tiled far wall, steps, loungers, an umbrella, a fence
   bikeRack, flagpole, stoneWall, gatePiers, bluffDeck, lawnChairs, outdoorShower, arbor, boatRack, beachSet (Shore Lane's) */
(function (root) {
  'use strict';
  const NS = root.NmcStores, SL = root.NmcShoreLane, CH = root.NmcCreekHouses; if (!NS || !SL || !CH) throw new Error('nmcShoreEstates.js: load nmcStores.js, nmcShoreLane.js and nmcCreekHouses.js first');
  const { md, hsh, TEX, F, box, plate, flat, stick, cyl, fbox, lamp } = NS.lib, { wallX, sash, doorX, roofQ, rail, at, lbox, adk, chairD, table, rtable, umbrella, plant } = SL.lib;
  const { grid, bays, pane, slide, gdoor, flight, railX, deckX, lounger, grill, shrub, furnish } = CH.lib;
  const B = NS.B, K = NS.KINDS, fl = Math.floor, D2R = Math.PI / 180, clamp = (v, a, b) => v < a ? a : v > b ? b : v, r2 = (v) => Math.round(v * 100) / 100;

  // ---- ramps, dark to light ----------------------------------------------------------------------------------------------------------
  Object.assign(NS.RAMP, {
    shingleP: ['#6f716b', '#888a84', '#a1a39d', '#b8bab4', '#cccec8', '#dddfd9'], shingleB: ['#4e4338', '#625548', '#776858', '#8b7c6a', '#9f907d', '#b2a491'],
    shingleS: ['#4f4f4b', '#62625d', '#777770', '#8c8b84', '#a19f97', '#b5b3ab'],
    roofC: ['#3b3631', '#4a443d', '#5a534a', '#6a6258', '#7b7266', '#8c8375'], roofR: ['#3a2420', '#4a2e28', '#5b3830', '#6c443a', '#7e5145', '#905f52'],
    roofS: ['#2e3236', '#393e43', '#454b51', '#525960', '#60686f', '#6f777f'], trimT: ['#59574e', '#6e6c62', '#838177', '#98968b', '#acaa9f', '#bebcb1'],
    stoneG: ['#474744', '#595955', '#6c6c67', '#807f79', '#94928b', '#a8a69e'], blueS: ['#3c4549', '#4b555a', '#5b666b', '#6c777c', '#7e898e', '#919ca0'],
    poolE: ['#1f5a6e', '#277088', '#3087a0', '#3c9db6', '#4fb3c9', '#6cc7d9'], poolEd: ['#173f55', '#1e516b', '#266482', '#307898', '#3d8bab', '#4f9fbe'],
    poolT: ['#1d3f5c', '#254f72', '#2f6089', '#3b729f', '#4a85b3', '#5d98c4'], mintB: ['#3d6656', '#4d7c69', '#5f927c', '#75a891', '#8ebea7', '#a8d2bd'],
    pinkB: ['#7a3c4c', '#94505f', '#ad6474', '#c27b8a', '#d495a2', '#e3b0ba'], copper: ['#2b4a42', '#355b51', '#406d61', '#4e8273', '#5f9787', '#74ac9b'],
    brass: ['#4e3d12', '#6a541c', '#876d28', '#a38737', '#bba14c', '#d1b866'], flagR: ['#5a0f12', '#7a1519', '#9c1d21', '#bb2a2c', '#d23f3c', '#e25b55'],
    hedgeP: ['#14281a', '#1b3422', '#22402a', '#2b4e33', '#355d3d', '#416d48'], canvasW: ['#8d8a80', '#a5a297', '#bbb8ad', '#cfccc1', '#dfddd3', '#ecebe3'],
  });
  Object.assign(TEX, {
    shake: (u, v) => { const r = fl(v / 0.19), uo = u + hsh(r, 3, 201) * 0.3, w = 0.16 + 0.14 * hsh(fl(uo / 0.26), r, 202), i = fl(uo / w); if (md(v, 0.19) < 0.03) return -2; if (md(uo, w) < 0.025) return -1; const h = hsh(i, r, 204); return h < 0.3 ? -1 : h > 0.78 ? 1 : 0; },
    louvre: (u, v) => md(v, 0.09) < 0.035 ? -2 : 0,
    rippleE: (u, v) => { const a = Math.sin(u * 3.1 + Math.sin(v * 2.3) * 1.7) + Math.sin(v * 4.2 - u * 1.3); return a > 1.25 ? 1 : a < -1.5 ? -1 : 0; },
    granite: (u, v) => { const j = fl(v / 0.3), uo = u + hsh(j, 8, 207) * 0.7, w = 0.32 + 0.5 * hsh(fl(uo / 0.5), j, 208), i = fl(uo / w); if (md(v, 0.3) < 0.035 || md(uo, w) < 0.035) return -2; const h = hsh(i, j, 209); return h < 0.28 ? -1 : h > 0.72 ? 1 : 0; },
  });

  // ---- the openings: the creek builder's own rule, with shutters on a tower's faces too --------------------------------------------------
  function opening(C, Wf, h, face) { const o = C.s.o, fr = o.frame || 'trimW', mod = !!o.modern;
    if (h.t === 'D') return doorX(C, Wf, h, { mat: o.door || 'barn', glass: true, trim: fr, lantern: h.u1 + 0.35 });
    if (h.t === 'F') return doorX(C, Wf, h, { french: true, mat: mod ? fr : 'trimW', trim: fr });
    if (h.t === 'S') return slide(C, Wf, h, mod ? fr : 'trimW');
    if (h.t === 'g') return gdoor(C, Wf, h, o.gdoor || {});
    if (h.t === 'G' || h.t === 'Q') return pane(C, Wf, h, { frame: mod ? fr : 'trimW', mull: [h.t === 'G' ? 3 : 2, 1], transom: true });
    if (h.t === 'h') return pane(C, Wf, h, { frame: mod ? fr : 'trimW', mull: [2, 1] });
    if (mod) return pane(C, Wf, h, { frame: fr, mull: h.t === 'P' ? [3, 1] : h.t === 'T' ? [1, 2] : [1, 1] });
    const sh = o.shutters && face === 'S' && (h.t === 'w' || h.t === 'T') ? o.shutters : null;
    return sash(C, Wf, h, { lites: h.t === 'P' ? [3, 2] : (o.lites || [2, 2]), trim: fr, shutter: sh, box: o.boxes && face === 'S' && h.t === 'w' && h.z0 < 1.2 ? o.boxes : null }); }
  const foundation = (C, f, L, pz, gr, FD) => { const fb = []; for (let u = 0; u <= L + 1e-6; u += Math.max(0.5, L / Math.ceil(L))) { const uu = Math.min(u, L), q = [f.a[0] + (f.b[0] - f.a[0]) * uu / L, f.a[1] + (f.b[1] - f.a[1]) * uu / L];
      fb.push([uu, Math.min(gr(q[0] + f.out[0] * 0.3, q[1] + f.out[1] * 0.3), pz - 0.18) - 0.12]); }
    wallX(C, { a: f.a, b: f.b, out: f.out, outline: fb.concat([[L, pz], [0, pz]]), holes: [], mat: FD.mat, tex: TEX[FD.tex || 'form'], inMat: 'conc', revMat: 'conc', t: 0.24, tag: 'house.foundation' }); };

  // ---- a wing under a north-south gambrel, its gambrel ends to the camera and the lane --------------------------------------------------
  function gambrelN(C, P, o, gr) {
    const R = P.roof || {}, ov = R.ov != null ? R.ov : 0.4, p = R.p || 1.9, pu = R.pu || 0.5, bk = R.bk || 1.3, H = P.H, pz = P.z0 || 0, up = P.up != null ? P.up : H + 0.15;
    const ec = (P.e0 + P.e1) / 2, hw = (P.e1 - P.e0) / 2, zK = H + p * bk, zR = zK + pu * (hw - bk), K0 = P.e0 + bk, K1 = P.e1 - bk;
    const side = P.side || { mat: 'shingleG', tex: 'shingle' }, sm = side.mat, stx = TEX[side.tex || 'shingle'], fr = o.frame || 'trimW', tm = P.trim || o.trim || 'trimW', FD = o.found || { mat: 'conc', tex: 'form' };
    const FC = { S: { a: [P.e0, P.n0], b: [P.e1, P.n0], out: [0, -1], end: true }, E: { a: [P.e1, P.n0], b: [P.e1, P.n1], out: [1, 0] }, N: { a: [P.e1, P.n1], b: [P.e0, P.n1], out: [0, 1], end: true }, W: { a: [P.e0, P.n1], b: [P.e0, P.n0], out: [-1, 0] } };
    for (const k of ['S', 'E', 'N', 'W']) { if (P.skip && P.skip.includes(k)) continue; const f = FC[k], L = Math.hypot(f.b[0] - f.a[0], f.b[1] - f.a[1]), pat = P[k] || {};
      foundation(C, f, L, pz, gr, FD);
      const outline = f.end ? [[0, pz], [L, pz], [L, H], [L - bk, zK], [hw, zR], [bk, zK], [0, H]] : [[0, pz], [L, pz], [L, H], [0, H]];
      const hol = bays(pat.g, L, pz).concat(f.end ? bays(pat.u, L, up, pat.um != null ? pat.um : bk + 0.2) : []).concat(f.end ? bays(pat.a, L, zK + 0.08, pat.am != null ? pat.am : L * 0.36) : []);
      const Wf = wallX(C, { a: f.a, b: f.b, out: f.out, outline, holes: hol, mat: sm, tex: stx, inMat: 'plaster', revMat: fr, t: 0.18, tag: 'house.' + P.id });
      for (const h of hol) opening(C, Wf, h, k); fbox(C, Wf.M, -0.05, L + 0.05, pz - 0.02, pz + 0.13, 0, 0.05, tm, { tag: 'house.watertable' }); }
    if (P.corners !== false) for (const [e, n] of [[P.e0, P.n0], [P.e1, P.n0], [P.e0, P.n1], [P.e1, P.n1]]) { const se = e > ec ? 1 : -1, sn = n > (P.n0 + P.n1) / 2 ? 1 : -1; box(C, e - 0.075 + 0.04 * se, e + 0.075 + 0.04 * se, n - 0.075 + 0.04 * sn, n + 0.075 + 0.04 * sn, pz - 0.05, H, tm, { tag: 'wall.corner' }); }
    const E0 = P.e0 - ov, E1 = P.e1 + ov, N0 = P.n0 - ov, N1 = P.n1 + ov, zE = H - ov * p, mat = R.mat || 'roofA', tex = TEX[R.tex || 'roofSh'], fm = R.fascia || 'trimW';
    roofQ(C, [E0, N1, zE], [E0, N0, zE], [K0, N0, zK], [K0, N1, zK], mat, { tex }); roofQ(C, [K0, N1, zK], [K0, N0, zK], [ec, N0, zR], [ec, N1, zR], mat, { tex });
    roofQ(C, [E1, N0, zE], [E1, N1, zE], [K1, N1, zK], [K1, N0, zK], mat, { tex }); roofQ(C, [K1, N0, zK], [K1, N1, zK], [ec, N1, zR], [ec, N0, zR], mat, { tex });
    stick(C, [ec, N0 - 0.02, zR + 0.04], [ec, N1 + 0.02, zR + 0.04], 0.15, mat, { tag: 'roof.ridge' });
    for (const n of [N0, N1]) { const pts = [[E0, zE], [K0, zK], [ec, zR], [K1, zK], [E1, zE]]; for (let i = 0; i + 1 < pts.length; i++) stick(C, [pts[i][0], n, pts[i][1] - 0.04], [pts[i + 1][0], n, pts[i + 1][1] - 0.04], 0.13, fm, { tag: 'roof.rake' }); }
    for (const e of [E0, E1]) stick(C, [e, N0, zE - 0.1], [e, N1, zE - 0.1], 0.17, fm, { tag: 'roof.fascia' });
    for (const d of P.dormers || []) { // a shed dormer in a gambrel's lower slope, to the east or the west (its face edge-on to the camera: a cheek and a roof)
      const sg = d.side === 'E' ? 1 : -1, eF = sg > 0 ? K1 - 0.15 : K0 + 0.15, eO = eF + sg * 0.9, n0 = d.n - d.w / 2, n1 = d.n + d.w / 2;
      box(C, Math.min(eF, eO), Math.max(eF, eO), n0, n1, H + 0.2, zK - 0.05, sm, { tag: 'house.dormer', tex: stx, top: mat }); }
    return { zTop: zR, box: [P.e0, P.e1, P.n0, P.n1] }; }

  // ---- an octagonal tower under a bell-cast roof ---------------------------------------------------------------------------------------
  function towerX(C, T, o, gr) {
    const R = T.R, z0 = T.z0 || 0, H = T.H, sm = (T.side && T.side.mat) || 'shingleG', stx = TEX[(T.side && T.side.tex) || 'shingle'], fr = o.frame || 'trimW', tm = T.trim || o.trim || 'trimW', FD = o.found || { mat: 'conc', tex: 'form' };
    const ang = (k) => (-112.5 + 45 * k) * D2R, V = (k, r) => [T.e + r * Math.cos(ang(k)), T.n + r * Math.sin(ang(k))], NAME = ['S', 'SE', 'E', 'NE', 'N', 'NW', 'W', 'SW'];
    for (let k = 0; k < 8; k++) { const mid = (-90 + 45 * k) * D2R, out = [Math.cos(mid), Math.sin(mid)]; if (out[1] > 0.3) continue;
      const a = V(k, R), b = V(k + 1, R), L = Math.hypot(b[0] - a[0], b[1] - a[1]), pat = (T.faces || {})[NAME[k]] || {}, f = { a, b, out };
      foundation(C, f, L, z0, gr, FD);
      const hol = bays(pat.g, L, z0, 0.22).concat(bays(pat.u, L, z0 + (T.up || 2.75), 0.22));
      const Wf = wallX(C, { a, b, out, outline: [[0, z0], [L, z0], [L, H], [0, H]], holes: hol, mat: sm, tex: stx, inMat: 'plaster', revMat: fr, t: 0.18, tag: 'house.tower' });
      for (const h of hol) opening(C, Wf, h, NAME[k]); fbox(C, Wf.M, -0.03, L + 0.03, z0 - 0.02, z0 + 0.13, 0, 0.05, tm, { tag: 'house.watertable' });
      if (T.band) fbox(C, Wf.M, -0.03, L + 0.03, z0 + T.band - 0.07, z0 + T.band + 0.07, 0, 0.04, tm, { tag: 'house.band' }); }
    for (let k = 0; k <= 8; k++) { if (Math.sin(ang(k)) > 0.5) continue; const p = V(k, R + 0.03); box(C, p[0] - 0.07, p[0] + 0.07, p[1] - 0.07, p[1] + 0.07, z0 - 0.05, H, tm, { tag: 'wall.corner' }); }
    const rf = T.roof || {}, ov = rf.ov != null ? rf.ov : 0.45, RE = R + ov, RK = R * (rf.kr || 0.6), zE = H - (rf.drop || 0.12), zK = H + (rf.kh || 0.75), zA = H + (rf.ah || 3.1), mat = rf.mat || 'roofA', tex = TEX[rf.tex || 'roofSh'], fm = rf.fascia || tm;
    for (let k = 0; k < 8; k++) { const a = V(k, RE), b = V(k + 1, RE), c = V(k + 1, RK), d = V(k, RK), L = Math.hypot(b[0] - a[0], b[1] - a[1]);
      roofQ(C, [a[0], a[1], zE], [b[0], b[1], zE], [c[0], c[1], zK], [d[0], d[1], zK], mat, { tex });
      const Lk = Math.hypot(c[0] - d[0], c[1] - d[1]); F(C, [[d[0], d[1], zK], [c[0], c[1], zK], [T.e, T.n, zA]], mat, { tag: 'roof', tex, uv: [[0, 0], [Lk, 0], [Lk / 2, Math.hypot(RK, zA - zK)]] });
      stick(C, [a[0], a[1], zE - 0.06], [b[0], b[1], zE - 0.06], 0.16, fm, { tag: 'roof.fascia' }); void L;
      stick(C, [a[0], a[1], zE + 0.02], [d[0], d[1], zK + 0.02], 0.1, mat, { tag: 'roof.hip' }); stick(C, [d[0], d[1], zK + 0.02], [T.e, T.n, zA], 0.09, mat, { tag: 'roof.hip' }); }
    stick(C, [T.e, T.n, zA - 0.1], [T.e, T.n, zA + 0.95], 0.06, T.finial || 'copper', { tag: 'roof.finial' }); cyl(C, T.e, T.n, 0.11, zA + 0.35, zA + 0.55, 8, T.finial || 'copper', { tag: 'roof.finial' });
    return { zTop: zA + 0.95, box: [T.e - R, T.e + R, T.n - R, T.n + R] }; }

  // ---- a cupola astride a ridge, its louvres, its pyramid roof and weathervane ------------------------------------------------------------
  function cupolaX(C, c) { const hw = c.w / 2, z = c.z, h = c.h || 0.95, m = c.mat || 'trimW', rm = c.roofMat || 'roofA', e0 = c.e - hw, e1 = c.e + hw, n0 = c.n - hw, n1 = c.n + hw;
    box(C, e0 - 0.1, e1 + 0.1, n0 - 0.1, n1 + 0.1, z - (c.sink || 0.7), z + 0.14, m, { tag: 'cupola.base' });
    box(C, e0, e1, n0, n1, z + 0.14, z + 0.14 + h, m, { tag: 'cupola.body' });
    plate(C, e0 + 0.1, e1 - 0.1, n0 - 0.005, z + 0.3, z + h - 0.02, 'dark', { tag: 'cupola.louvre', tex: TEX.louvre });
    const zE = z + 0.14 + h, zA = zE + (c.peak || 0.75) * hw + 0.35, o = 0.14, E0 = e0 - o, E1 = e1 + o, N0 = n0 - o, N1 = n1 + o, ap = [c.e, c.n, zA];
    for (const [a, b] of [[[E0, N0, zE], [E1, N0, zE]], [[E1, N0, zE], [E1, N1, zE]], [[E1, N1, zE], [E0, N1, zE]], [[E0, N1, zE], [E0, N0, zE]]]) { const L = Math.hypot(b[0] - a[0], b[1] - a[1]); F(C, [a, b, ap], rm, { tag: 'cupola.roof', tex: TEX.roofSh, uv: [[0, 0], [L, 0], [L / 2, 1]] }); }
    stick(C, [E0, N0, zE - 0.05], [E1, N0, zE - 0.05], 0.1, m, { tag: 'cupola.fascia' });
    if (c.vane !== false) { const zv = zA + 0.05; stick(C, [c.e, c.n, zv], [c.e, c.n, zv + 1.1], 0.04, 'iron', { tag: 'vane.rod' });
      for (const [a, b] of [[[c.e - 0.32, c.n, zv + 0.55], [c.e + 0.32, c.n, zv + 0.55]], [[c.e, c.n - 0.32, zv + 0.55], [c.e, c.n + 0.32, zv + 0.55]]]) stick(C, a, b, 0.025, 'iron', { tag: 'vane.arms' });
      F(C, [[c.e - 0.5, c.n, zv + 0.82], [c.e + 0.42, c.n, zv + 0.82], [c.e + 0.5, c.n, zv + 0.9], [c.e + 0.2, c.n, zv + 1.02], [c.e - 0.5, c.n, zv + 0.94]], c.vaneMat || 'copper', { tag: 'vane.whale', b: 1 });
      F(C, [[c.e - 0.5, c.n, zv + 0.94], [c.e + 0.2, c.n, zv + 1.02], [c.e + 0.5, c.n, zv + 0.9], [c.e + 0.42, c.n, zv + 0.82], [c.e - 0.5, c.n, zv + 0.82]], c.vaneMat || 'copper', { tag: 'vane.whale' }); }
    return zA + 1.15; }

  // ---- balustrades: white turned balusters (Shore Lane's rail), or the Chinese Chippendale lattice --------------------------------------
  function chip(C, a, b, z, m, gaps) { const L = Math.hypot(b[0] - a[0], b[1] - a[1]); if (L < 0.1) return; const d = [(b[0] - a[0]) / L, (b[1] - a[1]) / L], P = (s, zz) => [a[0] + d[0] * s, a[1] + d[1] * s, zz];
    let runs = [[0, L]]; for (const [g0, g1] of gaps || []) runs = runs.flatMap(([s0, s1]) => (g1 <= s0 || g0 >= s1) ? [[s0, s1]] : [[s0, g0], [g1, s1]].filter(q => q[1] - q[0] > 0.1));
    for (const [s0, s1] of runs) { const n = Math.max(1, Math.round((s1 - s0) / 0.95)), w = (s1 - s0) / n;
      for (let k = 0; k <= n; k++) { const p = P(s0 + k * w, z); box(C, p[0] - 0.05, p[0] + 0.05, p[1] - 0.05, p[1] + 0.05, z, z + 0.95, m, { tag: 'rail.post' }); }
      stick(C, P(s0, z + 0.93), P(s1, z + 0.93), 0.08, m, { tag: 'rail.cap' }); stick(C, P(s0, z + 0.12), P(s1, z + 0.12), 0.06, m, { tag: 'rail.bottom' });
      for (let k = 0; k < n; k++) { const sa = s0 + k * w + 0.06, sb = s0 + (k + 1) * w - 0.06, sm = (sa + sb) / 2;
        stick(C, P(sa, z + 0.15), P(sb, z + 0.89), 0.035, m, { tag: 'rail.lattice' }); stick(C, P(sb, z + 0.15), P(sa, z + 0.89), 0.035, m, { tag: 'rail.lattice' });
        stick(C, P(sa, z + 0.52), P(sm, z + 0.15), 0.03, m, { tag: 'rail.lattice' }); stick(C, P(sm, z + 0.89), P(sb, z + 0.52), 0.03, m, { tag: 'rail.lattice' }); } } }
  function balustrade(C, pts, z, t, m, gaps) { for (let i = 0; i + 1 < pts.length; i++) { const g = gaps && gaps[i]; if (t === 'chip') chip(C, pts[i], pts[i + 1], z, m || 'trimW', g); else railX(C, pts[i], pts[i + 1], z, { t: t || 'white', mat: m }, g); } }
  function walkX(C, w) { const zc = w.zc != null ? w.zc : w.z - 0.7;
    box(C, w.e0, w.e1, w.n0, w.n1, zc, w.z, w.curb || 'trimW', { tag: 'walk.curb', top: w.deck || 'deck', topTex: TEX.boardsE });
    balustrade(C, [[w.e0 + 0.05, w.n0 + 0.05], [w.e1 - 0.05, w.n0 + 0.05], [w.e1 - 0.05, w.n1 - 0.05], [w.e0 + 0.05, w.n1 - 0.05], [w.e0 + 0.05, w.n0 + 0.05]], w.z, w.rail || 'white', w.railMat || 'trimW');
    return w.z + 1.0; }
  function balconyX(C, b) { flat(C, b.e0, b.e1, b.n0, b.n1, b.z, b.deck || 'deck', { tag: 'balcony.deck', tex: TEX.boardsN });
    box(C, b.e0 - 0.05, b.e1 + 0.05, b.n0 - 0.05, b.n0 + 0.05, b.z - 0.22, b.z + 0.02, b.fascia || 'trimW', { tag: 'balcony.fascia' });
    const sides = { S: [[b.e0, b.n0 + 0.06], [b.e1, b.n0 + 0.06]], E: [[b.e1 - 0.06, b.n0], [b.e1 - 0.06, b.n1]], W: [[b.e0 + 0.06, b.n1], [b.e0 + 0.06, b.n0]] };
    for (const k of b.sides || ['S', 'E', 'W']) balustrade(C, sides[k], b.z, b.rail || 'white', b.railMat); }
  function hearthX(C, h, gr) { const z = h.z != null ? h.z : gr(h.e, h.n - 0.2), hw = h.w / 2, n = h.n - 0.02;
    F(C, [[h.e - hw, n, z + 0.05], [h.e + hw, n, z + 0.05], [h.e + hw, n, z + h.h], [h.e - hw, n, z + h.h]], 'dark', { tag: 'hearth.mouth', b: -1 });
    box(C, h.e - hw - 0.2, h.e + hw + 0.2, n - 0.12, n + 0.02, z + h.h, z + h.h + 0.28, h.lintel || 'stoneG', { tag: 'hearth.lintel', tex: TEX.granite });
    box(C, h.e - hw - 0.3, h.e + hw + 0.3, n - 0.55, n, z, z + 0.12, h.lintel || 'stoneG', { tag: 'hearth.hearthstone' });
    for (const [a, b, c] of [[-0.28, 0.15, 0.06], [0.05, 0.22, 0.2], [0.24, 0.12, 0.08]]) box(C, h.e + a - 0.18, h.e + a + 0.18, n + 0.1, n + 0.45, z + b - 0.06, z + b + 0.06, 'wood', { tag: 'hearth.log' });
    if (h.lit) { F(C, [[h.e - 0.32, n - 0.01, z + 0.14], [h.e + 0.3, n - 0.01, z + 0.14], [h.e + 0.05, n - 0.01, z + 0.62]], 'flyO', { tag: 'hearth.fire', em: 'glow:amber' }); lamp(C, h.e, n - 0.6, z + 0.6, '#ffa04a', 0.9, 5.5); }
    if (h.wood) { const [we, wn] = h.wood; for (let r = 0; r < 4; r++) for (let k = 0; k < 5 - (r & 1); k++) cyl(C, we - 0.5 + k * 0.24 + (r & 1) * 0.12, wn, 0.11, z + r * 0.2, z + r * 0.2 + 0.2, 6, r & 1 ? 'deck' : 'wood', { tag: 'hearth.firewood', top: 'butter' }); } }
  // what the creek's furniture lacks: a black planter with its boxwood ball, the bikes, a lantern on a post, a chaise
  function planter(C, e, n, z, s, m) { s = s || 1; box(C, e - 0.3 * s, e + 0.3 * s, n - 0.3 * s, n + 0.3 * s, z, z + 0.72 * s, m || 'iron', { tag: 'planter', top: 'soil' }); cyl(C, e, n, 0.3 * s, z + 0.7 * s, z + 1.15 * s, 9, 'boxw', { tag: 'planter.boxwood', top: 'boxw' }); }
  function wheel(C, e, n, z, r, m) { const k = 14; for (let i = 0; i < k; i++) { const a = i / k * 2 * Math.PI, b = (i + 1) / k * 2 * Math.PI; stick(C, [e + Math.cos(a) * r, n, z + r + Math.sin(a) * r], [e + Math.cos(b) * r, n, z + r + Math.sin(b) * r], 0.045, m || 'dark', { tag: 'bike.tyre' }); }
    stick(C, [e, n, z + r], [e, n, z + r + 0.02], 0.06, 'steel', { tag: 'bike.hub' }); }
  function bike(C, e, n, z, f, m, basket) { const s = f === 'W' ? -1 : 1, X = (x) => e + s * x, r = 0.33;
    wheel(C, X(-0.52), n, z, r); wheel(C, X(0.52), n, z, r);
    const BB = [X(-0.02), n, z + 0.3], SH = [X(-0.2), n, z + 0.86], HT = [X(0.36), n, z + 0.82], RA = [X(-0.52), n, z + r], FA = [X(0.52), n, z + r];
    for (const [a, b] of [[BB, SH], [BB, HT], [SH, HT], [BB, RA], [SH, RA], [HT, FA]]) stick(C, a, b, 0.05, m, { tag: 'bike.frame' });
    stick(C, [X(-0.24), n, z + 0.95], [X(-0.06), n, z + 0.95], 0.08, 'dark', { tag: 'bike.seat' }); stick(C, [HT[0], n - 0.26, z + 0.98], [HT[0], n + 0.26, z + 0.98], 0.035, 'steel', { tag: 'bike.bars' });
    stick(C, HT, [HT[0], n, z + 0.98], 0.04, m, { tag: 'bike.stem' }); if (basket) box(C, X(0.42) - 0.16, X(0.42) + 0.16, n - 0.17, n + 0.17, z + 0.82, z + 1.04, 'wood', { tag: 'bike.basket', tex: TEX.slats, top: 'dark' }); }
  function xfurn(C, f, gr) { const z = typeof f.z === 'number' ? f.z : gr(f.e, f.n);
    switch (f.k) {
      case 'planter': return planter(C, f.e, f.n, z, f.s, f.m);
      case 'bikes': return (f.list || [['mintB', true], ['pinkB', false]]).forEach(([m, bk], i) => bike(C, f.e, f.n + i * 0.55, z, f.f || 'E', m, bk));
      case 'lantern': { box(C, f.e - 0.06, f.e + 0.06, f.n - 0.06, f.n + 0.06, z, z + 1.6, 'iron', { tag: 'lantern.post' }); box(C, f.e - 0.13, f.e + 0.13, f.n - 0.13, f.n + 0.13, z + 1.6, z + 1.95, 'iron', { tag: 'lantern', s: { mat: 'glass', em: 'glow:cream' } }); lamp(C, f.e, f.n, z + 1.8, '#ffc774', 0.55, 4.6); return; }
      case 'chaise': return lounger(C, f.e, f.n, z, f.f || 'S', f.m || 'trimW');
      default: return furnish(C, f, () => z); } }

  /* ==================================================================================================================================
     THE HOUSE: the creek's house, then the shingle style's wings, towers, cupolas, roof walks, balconies and hearths ======================= */
  B.estateHouse = function (C) {
    const o = C.s.o, gr = grid(o.g), std = o.parts.filter(P => !P.kind), cust = o.parts.filter(P => P.kind === 'gambrelN');
    const s0 = C.s; C.s = Object.assign({}, s0, { o: Object.assign({}, o, { parts: std.length ? std : [Object.assign({}, cust[0], { kind: undefined, skip: ['S', 'E', 'N', 'W'], roof: { t: 'flat' }, H: 0.01 })] }) });
    B.creekHouse(C); C.s = s0;
    let zTop = C.dims ? C.dims.h : 0; const fb = C.dims ? C.dims.foot.slice() : [0, 0, 0, 0], grow = (b) => { fb[0] = Math.min(fb[0], b[0]); fb[1] = Math.max(fb[1], b[1]); fb[2] = Math.min(fb[2], b[2]); fb[3] = Math.max(fb[3], b[3]); };
    for (const P of cust) { const q = gambrelN(C, P, o, gr); zTop = Math.max(zTop, q.zTop); grow(q.box); }
    for (const T of o.towers || []) { const q = towerX(C, T, o, gr); zTop = Math.max(zTop, q.zTop); grow(q.box); }
    for (const c of o.cupolas || []) zTop = Math.max(zTop, cupolaX(C, c));
    for (const w of o.walks || []) zTop = Math.max(zTop, walkX(C, w));
    for (const b of o.balconies || []) balconyX(C, b);
    for (const h of o.hearths || []) hearthX(C, Object.assign({}, h, { lit: h.lit || o.lit }), gr);
    for (const p of o.pilasters || []) { box(C, p.e - 0.19, p.e + 0.19, p.n - 0.06, p.n + 0.08, -0.25, p.h - 0.3, 'trimW', { tag: 'house.pilaster' }); box(C, p.e - 0.25, p.e + 0.25, p.n - 0.1, p.n + 0.08, p.h - 0.3, p.h, 'trimW', { tag: 'house.capital' }); }
    for (const f of o.friezes || []) box(C, f.e0, f.e1, f.n - 0.07, f.n + 0.02, f.z0, f.z1, 'trimW', { tag: 'house.frieze' });
    for (const f of o.xfurn || []) xfurn(C, f, gr);
    if (C.dims) { C.dims.w = r2(fb[1] - fb[0]); C.dims.d = r2(fb[3] - fb[2]); C.dims.h = r2(Math.max(C.dims.h, zTop + 0.6)); C.dims.foot = fb.map(r2); }
  };

  /* ==================================================================================================================================
     THE POOL ON ITS TERRACE ============================================================================================================= */
  B.estatePool = function (C) { const o = C.s.o, w = o.w || 10, d = o.d || 4.6, cp = o.coping || 0.4, zd = 0.05, zw = -0.13, dk = o.deck || [-w / 2 - 2, w / 2 + 2, -d / 2 - 2.2, d / 2 + 1.6];
    const pe0 = -w / 2, pe1 = w / 2, pn0 = -d / 2, pn1 = d / 2, H = [pe0 - cp, pe1 + cp, pn0 - cp, pn1 + cp], dm = o.deckMat || 'blueS', gr = grid(o.g);
    for (const [a, b, c, e] of [[dk[0], dk[1], dk[2], H[2]], [dk[0], dk[1], H[3], dk[3]], [dk[0], H[0], H[2], H[3]], [H[1], dk[1], H[2], H[3]]]) if (b - a > 0.05 && e - c > 0.05) box(C, a, b, c, e, Math.min(gr(a, c), gr(b, c)) - 0.1, 0.02, dm, { tag: 'pool.deck', top: dm, topTex: TEX.slabs, tex: TEX.slabs });
    for (const [a, b, c, e] of [[H[0], H[1], H[2], pn0], [H[0], H[1], pn1, H[3]], [H[0], pe0, pn0, pn1], [pe1, H[1], pn0, pn1]]) box(C, a, b, c, e, zd - 0.06, zd, o.copingMat || 'stone', { tag: 'pool.coping' });
    const sh = o.shallow != null ? o.shallow : pe0 + w * 0.35; flat(C, pe0, sh, pn0, pn1, zw, 'poolE', { tag: 'pool.water', tex: TEX.rippleE, b: 0.4 }); flat(C, sh, pe1, pn0, pn1, zw, 'poolEd', { tag: 'pool.water', tex: TEX.rippleE });
    plate(C, pe0, pe1, pn1, zw, zd - 0.06, 'poolT', { tag: 'pool.tile', b: 0.5 });
    for (let k = 0; k < 3; k++) flat(C, pe0, pe0 + 0.42 * (3 - k), pn0 + 0.5 + k * 0.15, pn1 - 0.5 - k * 0.15, zw + 0.001 + k * 0.0005, 'poolE', { tag: 'pool.steps', b: 1 });
    if (o.lit) for (const e of [pe0 + w * 0.3, pe0 + w * 0.7]) lamp(C, e, pn1 - 0.4, zw - 0.2, '#8fe0ff', 0.75, 4.5);
    for (const f of o.furn || []) xfurn(C, f, () => 0.02);
    if (o.fence) { const fz = 0.02, fm = o.fence.mat || 'iron', fh = o.fence.h || 1.2, pts = [[dk[0], dk[2]], [dk[1], dk[2]], [dk[1], dk[3]], [dk[0], dk[3]], [dk[0], dk[2]]], gate = o.fence.gate || null;
      for (let i = 0; i + 1 < pts.length; i++) { const a = pts[i], b = pts[i + 1], L = Math.hypot(b[0] - a[0], b[1] - a[1]), dd = [(b[0] - a[0]) / L, (b[1] - a[1]) / L], g = gate && gate[0] === i ? gate.slice(1) : null;
        for (let s = 0; s <= L + 1e-6; s += 0.12) { if (g && s > g[0] && s < g[1]) continue; const p = [a[0] + dd[0] * s, a[1] + dd[1] * s]; box(C, p[0] - 0.012, p[0] + 0.012, p[1] - 0.012, p[1] + 0.012, fz, fz + fh, fm, { tag: 'pool.picket', noTop: true }); }
        for (const zz of [fz + 0.1, fz + fh - 0.05]) stick(C, [a[0], a[1], zz], [b[0], b[1], zz], 0.04, fm, { tag: 'pool.fenceRail' }); } }
    C.dims = { w: r2(dk[1] - dk[0]), d: r2(dk[3] - dk[2]), h: 2.6, floor: 0, foot: dk.map(r2) }; C.indoor = null;
    C.st.push({ id: 'poolSteps', verb: 'swim', at: [pe0 + 0.6, 0, zw], stand: [pe0 - 0.8, 0] }); };

  /* ==================================================================================================================================
     PROPS ==================================================================================================================================== */
  // the bikes in their white rack (the HGTV house's): a timber rack, its slots, bikes standing in it side-on to the camera
  B.bikeRack = function (C) { const o = C.s.o, n = (o.bikes || []).length || 2, len = Math.max(1.4, n * 0.6 + 0.4);
    for (const e of [-0.55, 0.55]) { box(C, e - 0.05, e + 0.05, -len / 2, len / 2, 0, 0.08, 'trimW', { tag: 'rack.foot' }); box(C, e - 0.04, e + 0.04, -len / 2, -len / 2 + 0.08, 0, 0.62, 'trimW', { tag: 'rack.end' }); box(C, e - 0.04, e + 0.04, len / 2 - 0.08, len / 2, 0, 0.62, 'trimW', { tag: 'rack.end' }); }
    box(C, -0.6, 0.6, -len / 2, -len / 2 + 0.08, 0.5, 0.62, 'trimW', { tag: 'rack.rail' }); box(C, -0.6, 0.6, len / 2 - 0.08, len / 2, 0.5, 0.62, 'trimW', { tag: 'rack.rail' }); box(C, 0.3, 0.42, -len / 2, len / 2, 0.36, 0.46, 'trimW', { tag: 'rack.bar' });
    (o.bikes || [['mintB', true], ['pinkB', false]]).forEach(([m, bk], i) => bike(C, 0, -len / 2 + 0.45 + i * 0.6, 0, o.f || 'E', m, bk));
    C.dims = { w: 1.9, d: len, h: 1.1, floor: 0 }; };
  // a flagpole on the lawn, its flag hanging in the calm (red, white and red)
  B.flagpole = function (C) { const o = C.s.o, h = o.h || 7.5; cyl(C, 0, 0, 0.07, 0, h, 8, 'trimW', { tag: 'flag.pole' }); cyl(C, 0, 0, 0.12, h, h + 0.22, 8, 'brass', { tag: 'flag.ball' });
    const fw = o.fw || 1.8, fh = o.fh || 0.9, zt = h - 0.1, droop = o.droop != null ? o.droop : 0.35, P = (u, v) => [0.08 + u * fw * (1 - droop * 0.4), -0.02 - u * 0.12, zt - v * fh - u * u * droop * 0.9];
    for (const [u0, u1, m] of [[0, 0.25, 'flagR'], [0.25, 0.75, 'trimW'], [0.75, 1, 'flagR']]) { const Q = [P(u0, 1), P(u1, 1), P(u1, 0), P(u0, 0)]; F(C, Q, m, { tag: 'flag.cloth', b: 1 }); F(C, Q.slice().reverse(), m, { tag: 'flag.cloth' }); }
    const c = P(0.5, 0.5); F(C, [[c[0] - 0.09, c[1] - 0.012, c[2] - 0.1], [c[0] + 0.09, c[1] - 0.012, c[2] - 0.1], [c[0], c[1] - 0.012, c[2] + 0.14]], 'flagR', { tag: 'flag.leaf' });
    C.dims = { w: 2.2, d: 0.5, h: h + 0.3, floor: 0 }; };
  // a dry-stone wall along a polyline (plan metres from the piece), its top stepping, its stones fieldstone or granite
  B.stoneWall = function (C) { const o = C.s.o, pts = o.pts || [[-(o.len || 6) / 2, 0], [(o.len || 6) / 2, 0]], h = o.h || 0.8, t = o.t || 0.55, m = o.mat || 'stoneW', tx = TEX[o.tex || 'fieldstone'], gr = grid(o.g);
    // each run of stone a prism turned along the wall (not a box on the plan's axes, which steps along a curving road), its top a course of capstones
    const prism = (A0, A1, B1, B0, z0, z1, s0, s1, w, tag) => { const Q = (p, z) => [p[0], p[1], z], uvS = [[s0, z0], [s1, z0], [s1, z1], [s0, z1]];
      F(C, [Q(B0, z0), Q(B1, z0), Q(B1, z1), Q(B0, z1)], m, { tag, tex: tx, uv: uvS }); F(C, [Q(A1, z0), Q(A0, z0), Q(A0, z1), Q(A1, z1)], m, { tag, tex: tx, uv: uvS });
      F(C, [Q(A0, z0), Q(B0, z0), Q(B0, z1), Q(A0, z1)], m, { tag, tex: tx, uv: [[0, z0], [w, z0], [w, z1], [0, z1]] }); F(C, [Q(B1, z0), Q(A1, z0), Q(A1, z1), Q(B1, z1)], m, { tag, tex: tx, uv: [[0, z0], [w, z0], [w, z1], [0, z1]] });
      F(C, [Q(B0, z1), Q(B1, z1), Q(A1, z1), Q(A0, z1)], m, { tag, tex: tx, uv: [[s0, 0], [s1, 0], [s1, w], [s0, w]] }); };
    let s = 0; for (let i = 0; i + 1 < pts.length; i++) { const a = pts[i], b = pts[i + 1], L = Math.hypot(b[0] - a[0], b[1] - a[1]); if (L < 0.05) continue; const d = [(b[0] - a[0]) / L, (b[1] - a[1]) / L], nr = [-d[1], d[0]], k = Math.max(1, Math.round(L / 1.6));
      for (let j = 0; j < k; j++) { const s0 = L * j / k, s1 = L * (j + 1) / k + (j + 1 < k || i + 2 < pts.length ? 0.04 : 0), P0 = [a[0] + d[0] * s0, a[1] + d[1] * s0], P1 = [a[0] + d[0] * s1, a[1] + d[1] * s1];
        const off = (p, q) => [p[0] + nr[0] * q, p[1] + nr[1] * q], g = Math.min(gr(P0[0], P0[1]), gr(P1[0], P1[1])), hh = h * (0.95 + 0.08 * hsh(fl((s + s0) * 3), 7, 211));
        prism(off(P0, t / 2), off(P1, t / 2), off(P1, -t / 2), off(P0, -t / 2), g - 0.2, g + hh, s + s0, s + s1, t, 'wall.stone');
        prism(off(P0, t / 2 - 0.07), off(P1, t / 2 - 0.07), off(P1, -t / 2 + 0.07), off(P0, -t / 2 + 0.07), g + hh, g + hh + 0.11, s + s0, s + s1, t - 0.14, 'wall.capstones'); }
      s += L; }
    C.dims = { w: 1, d: 1, h, floor: 0 }; };
  // gate piers at a drive's mouth: two square piers, capped, a lantern on each; white board or stone
  B.gatePiers = function (C) { const o = C.s.o, gap = o.gap || 4.2, w = o.w || 0.55, h = o.h || 1.5, m = o.mat || 'trimW', gr = grid(o.g);
    for (const e of [-gap / 2 - w / 2, gap / 2 + w / 2]) { const g = gr(e, 0) - 0.1; box(C, e - w / 2, e + w / 2, -w / 2, w / 2, g, h, m, { tag: 'pier', tex: m === 'trimW' ? TEX.clap : TEX[o.tex || 'fieldstone'] }); box(C, e - w / 2 - 0.06, e + w / 2 + 0.06, -w / 2 - 0.06, w / 2 + 0.06, h, h + 0.1, o.cap || 'trimW', { tag: 'pier.cap' });
      if (o.lanterns !== false) { box(C, e - 0.13, e + 0.13, -0.13, 0.13, h + 0.1, h + 0.48, 'iron', { tag: 'pier.lantern', s: { mat: 'glass', em: 'glow:cream' } }); lamp(C, e, 0, h + 0.3, '#ffc774', 0.5, 4.2); } }
    if (o.sign) { box(C, -0.6, 0.6, -0.02, 0.02, h - 0.45, h - 0.1, 'trimW', { tag: 'pier.sign' }); }
    C.dims = { w: gap + 2 * w, d: w, h: h + 0.5, floor: 0 }; };
  // a lookout deck at the bluff's lip, its bench, a gap in its rail for the stair down
  B.bluffDeck = function (C) { const o = C.s.o, w = o.w || 2.8, d = o.d || 2.2, gr = grid(o.g), z = o.z != null ? o.z : 0.12, m = o.mat || 'deck', e0 = -w / 2, e1 = w / 2, n0 = -d / 2, n1 = d / 2, rl = { t: o.rail || 'white' };
    flat(C, e0, e1, n0, n1, z, m, { tag: 'deck.boards', tex: TEX.boardsE }); box(C, e0 - 0.04, e1 + 0.04, n0 - 0.04, n0, z - 0.24, z, m, { tag: 'deck.rim', noN: true });
    for (const e of [e0 + 0.12, e1 - 0.12]) for (const n of [n0 + 0.12, n1 - 0.12]) box(C, e - 0.08, e + 0.08, n - 0.08, n + 0.08, gr(e, n) - 0.15, z - 0.02, m, { tag: 'deck.post' });
    const st = [], gaps = []; for (const s of o.stairs || []) { gaps.push([s.at - e0 - s.w / 2, s.at - e0 + s.w / 2]);   // the stair down the bluff: one straight flight from the deck's south edge to the beach (s.zFoot)
      st.push(flight(C, [s.at, n0, z], [0, -1], s.w, gr, { mat: s.mat || m, rail: rl, zFoot: s.zFoot })); }
    railX(C, [e0, n0], [e1, n0], z, rl, gaps); railX(C, [e1, n0], [e1, n1], z, rl); railX(C, [e0, n1], [e0, n0], z, rl);
    for (const f of o.furn || []) xfurn(C, f, () => z);
    C.dims = { w, d, h: 1.2, floor: z }; st.forEach((s, k) => C.st.push({ id: 'bluffStair' + k, verb: 'descend', at: [s.foot[0], s.foot[1], s.zFoot], stand: [s.foot[0], s.foot[1] - 0.6] })); };
  // two Adirondacks and a side table on the lawn, turned to the sea; or four round a fire ring
  B.lawnChairs = function (C) { const o = C.s.o, m = o.m || 'trimW', n = o.n || 2, z = 0; if (o.ring) { const r = 1.35; for (let k = 0; k < n; k++) { const a = (k / n) * 2 * Math.PI + (o.a0 || 0.4), e = Math.cos(a) * r, nn = Math.sin(a) * r, f = Math.abs(Math.cos(a)) > Math.abs(Math.sin(a)) ? (Math.cos(a) > 0 ? 'W' : 'E') : (Math.sin(a) > 0 ? 'S' : 'N'); adk(C, e, nn, z, f, Array.isArray(m) ? m[k % m.length] : m); }
      CH.lib.firepit(C, 0, 0, z, 'stoneW', !!o.lit); }
    else { for (let k = 0; k < n; k++) adk(C, -0.75 * (n - 1) / 2 * 2 + k * 1.5 - 0, 0, z, o.f || 'S', Array.isArray(m) ? m[k % m.length] : m); rtable(C, 0, 0.1, z, 0.25, 0.45, Array.isArray(m) ? m[0] : m); }
    C.dims = { w: 3, d: 3, h: 1.1, floor: 0 }; };
  // the beach's: an outdoor shower in cedar, its towel; an arbor of white posts over a gate; a rack of kayaks and paddleboards
  B.outdoorShower = function (C) { box(C, -0.7, 0.7, -0.05, 0.05, 0.1, 1.9, 'cedarN', { tag: 'shower.wall', tex: TEX.vboard }); box(C, 0.65, 0.75, -1.2, 0, 0.1, 1.9, 'cedarN', { tag: 'shower.wall', tex: TEX.vboard }); box(C, -0.75, -0.65, -1.2, 0, 0.1, 1.9, 'cedarN', { tag: 'shower.wall', tex: TEX.vboard });
    flat(C, -0.7, 0.7, -1.2, 0, 0.06, 'deck', { tag: 'shower.floor', tex: TEX.boardsE }); stick(C, [0, -0.15, 1.6], [0, -0.15, 2.2], 0.04, 'steel', { tag: 'shower.pipe' }); box(C, -0.6, 0.2, -0.08, -0.04, 1.3, 1.75, 'skyB' in NS.RAMP ? 'skyB' : 'navy', { tag: 'shower.towel' }); C.dims = { w: 1.5, d: 1.3, h: 2.2, floor: 0 }; };
  B.arbor = function (C) { const o = C.s.o, w = o.w || 1.6, h = o.h || 2.3; for (const e of [-w / 2, w / 2]) for (const n of [-0.3, 0.3]) box(C, e - 0.05, e + 0.05, n - 0.05, n + 0.05, 0, h, 'trimW', { tag: 'arbor.post' });
    for (let k = 0; k <= 6; k++) { const e = -w / 2 - 0.15 + (w + 0.3) * k / 6; box(C, e - 0.03, e + 0.03, -0.45, 0.45, h, h + 0.1, 'trimW', { tag: 'arbor.slat' }); } for (const n of [-0.3, 0.3]) box(C, -w / 2 - 0.25, w / 2 + 0.25, n - 0.04, n + 0.04, h - 0.12, h, 'trimW', { tag: 'arbor.beam' });
    if (o.roses) for (const e of [-w / 2, w / 2]) for (let k = 0; k < 5; k++) cyl(C, e + (hsh(k, 3, 215) - 0.5) * 0.2, -0.32, 0.18, 0.2 + k * 0.42, 0.55 + k * 0.42, 7, 'leaf', { tag: 'arbor.rose', top: k % 2 ? 'pink' : 'leaf' });
    C.dims = { w: w + 0.6, d: 1, h: h + 0.2, floor: 0 }; };
  B.boatRack = function (C) { const o = C.s.o, items = o.items || ['yel', 'red'], L = o.len || 3.4; for (const e of [-L * 0.3, L * 0.3]) { box(C, e - 0.05, e + 0.05, -0.05, 0.05, 0, 1.4, 'wood', { tag: 'rack.post' }); for (const z of [0.5, 1.0]) box(C, e - 0.05, e + 0.05, -0.45, 0.05, z, z + 0.06, 'wood', { tag: 'rack.arm' }); }
    items.forEach((m, i) => { const z = 0.56 + i * 0.5, t = o.sup && i === items.length - 1; box(C, -L / 2, L / 2, -0.42, t ? -0.02 : -0.06, z, z + (t ? 0.1 : 0.28), m, { tag: t ? 'rack.board' : 'rack.kayak', top: m }); if (!t) { F(C, [[-L / 2, -0.42, z + 0.28], [-L / 2 - 0.3, -0.24, z + 0.16], [-L / 2, -0.06, z + 0.28]], m, { tag: 'rack.bow' }); F(C, [[L / 2, -0.06, z + 0.28], [L / 2 + 0.3, -0.24, z + 0.16], [L / 2, -0.42, z + 0.28]], m, { tag: 'rack.bow' }); } });
    C.dims = { w: L + 0.6, d: 0.6, h: 1.6, floor: 0 }; };

  Object.assign(K, {
    estateHouse: { label: 'A shingle-style house on the south shore', foot: [-22, 22, -16, 14] }, estatePool: { label: 'A pool on its terrace', foot: [-9, 9, -6, 6] },
    bikeRack: { label: 'Bikes in their rack', foot: [-1, 1, -1.4, 1.4] }, flagpole: { label: 'A flagpole', foot: [-0.5, 2.2, -0.5, 0.5] }, stoneWall: { label: 'A dry-stone wall', foot: [-12, 12, -12, 12] },
    gatePiers: { label: 'Gate piers at a drive', foot: [-3.5, 3.5, -0.5, 0.5] }, bluffDeck: { label: 'A lookout deck at the bluff\u2019s lip', foot: [-2, 2, -1.6, 1.6] }, lawnChairs: { label: 'Adirondacks on the lawn', foot: [-2, 2, -2, 2] },
    outdoorShower: { label: 'An outdoor shower', foot: [-0.8, 0.8, -1.3, 0.2] }, arbor: { label: 'An arbor over a gate', foot: [-1.2, 1.2, -0.6, 0.6] }, boatRack: { label: 'A rack of kayaks and a paddleboard', foot: [-2.2, 2.2, -0.6, 0.2] },
  });
  root.NmcShoreEstates = { revision: '1.0 (the south shore redrawn, 10-04)', kinds: ['estateHouse', 'estatePool', 'bikeRack', 'flagpole', 'stoneWall', 'gatePiers', 'bluffDeck', 'lawnChairs', 'outdoorShower', 'arbor', 'boatRack'],
    lib: { opening, gambrelN, towerX, cupolaX, chip, balustrade, walkX, balconyX, hearthX, planter, bike, wheel, xfurn } };
})(typeof globalThis !== 'undefined' ? globalThis : this);
