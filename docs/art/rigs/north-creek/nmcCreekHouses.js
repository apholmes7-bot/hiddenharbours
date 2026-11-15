/* NINE MILE CREEK, KEY SCENES: THE NORTH CREEK REDRAWN (hh-nmc-key-scenes/north-creek/, 2026-10-02): its seven waterfront houses
   (globalThis.NmcCreekHouses). One kind, creekHouse, on NmcStores' builder with Shore Lane's walls, sashes and doors: load
   plaza-stores/nmcStores.js and shore-lane/nmcShoreLane.js first. Plan metres (e east, n north, z up), the origin on the main body's
   centre at its main floor (z 0), drawn through the fixed camera (orthographic, 40 deg, looking north, 32 px = 1 m) on CoastalPass.light
   and relit from any WeatherSky.at() sky. No dither, no keyline. The camera sees each house's south face: the north bank's houses show
   their creek sides (their doors are on the lane side, north), the south bank's their fronts.
   A house (o):
     parts     boxes, each with its walls, openings and roof: { id, e0, e1, n0, n1, H (the eave over z 0), z0 (its floor, default 0),
               roof { t gable|hip|gambrel|shed|flat, r e|n (the ridge's run), p (pitch), ov, hi S|N (shed), bk, pu (gambrel), mat, tex, fascia },
               side { mat, tex }, S/N/E/W { b, g, u, a: one pattern a storey }, skip [faces], glass (a glazed gable face), up (the upper floor) }
     walkout   { h, mat, tex }: the basement storey opens on the south face, h below the main floor; else each part stands on a foundation
     found     { mat, tex } the foundation where the ground falls away
     g         the ground round it: a grid of heights from z 0 { e0, n0, s, w, h, z }
     decks . porches . patios . walls . steps . chimneys . dormers . furn . lights
   Openings, one token a bay, the bays even across the face: w window . W a pair . T tall . P picture . G glass wall . v small . h hopper
   (a basement window) . D door . F French doors . S slider . g garage door . '.' a blank bay. o.modern draws casements and fixed glass
   in o.frame (black or bronze) where the others take white sashes. */
(function (root) {
  'use strict';
  const NS = root.NmcStores, SL = root.NmcShoreLane; if (!NS || !SL) throw new Error('nmcCreekHouses.js: load nmcStores.js and nmcShoreLane.js first');
  const { md, hsh, TEX, F, box, plate, flat, stick, cyl, fbox, lamp } = NS.lib, { wallX, sash, doorX, roofQ, rail, at, lbox, adk, chairD, sofa, table, rtable, umbrella, plant, corner } = SL.lib;
  const B = NS.B, K = NS.KINDS, fl = Math.floor, clamp = (v, a, b) => v < a ? a : v > b ? b : v;

  // ---- ramps, dark to light, added to the stores' table ----------------------------------------------------------------------------------
  Object.assign(NS.RAMP, {
    char: ['#222528', '#2b2f33', '#353a3f', '#40464c', '#4c535a', '#5a6269'], cedarN: ['#5c3d22', '#724d2c', '#8a5f37', '#a07244', '#b48654', '#c69b68'],
    sage2: ['#3c4839', '#4b5947', '#5c6b57', '#6f7f69', '#83937c', '#98a890'], seamD: ['#1b1e21', '#24282c', '#2e3337', '#393f44', '#454c52', '#535b62'],
    stoneW: ['#4a3f33', '#5e5142', '#736452', '#887863', '#9c8c76', '#b0a08a'], stoneD: ['#2e2c2a', '#3a3835', '#474441', '#55514d', '#635f5a', '#726d67'],
    paver: ['#58564f', '#6a6861', '#7d7b73', '#908d85', '#a3a097', '#b5b2a8'], mulch: ['#26170e', '#332013', '#41291a', '#4f3321', '#5e3d29', '#6d4832'],
    hydB: ['#2b3868', '#364787', '#4357a3', '#566aba', '#6e82cd', '#8d9edb'], boxw: ['#122516', '#182f1c', '#1f3b24', '#27482d', '#305636', '#3a6541'],
    aqua: ['#173f4a', '#1f5462', '#28697a', '#348092', '#4596a8', '#5cadbd'], blueD: ['#14253a', '#1b3150', '#234066', '#2d517d', '#3a6494', '#4c78a9'],
    bronze: ['#1d1915', '#26211c', '#302a24', '#3b342c', '#473f36', '#544a40'], cushion: ['#5d5a52', '#77746a', '#919083', '#aaa99c', '#c2c1b4', '#d6d5ca'],
    mossG: ['#26332a', '#2f3e33', '#3a4a3e', '#46574a', '#536557', '#617465'], buff: ['#6e6250', '#887a65', '#a1937c', '#b8ab93', '#ccc0a9', '#ddd3be'],
  });
  // ---- surfaces: u along the face (or e), v up it (or n), metres ---------------------------------------------------------------------------
  Object.assign(TEX, {
    bb: (u) => { const m = md(u, 0.41); return m < 0.045 ? 1 : m < 0.066 ? -1 : 0; },
    vboard: (u) => { const i = fl(u / 0.15); if (md(u, 0.15) < 0.013) return -1; const h = hsh(i, 7, 101); return h < 0.28 ? -1 : h > 0.82 ? 1 : 0; },
    seamR: (u) => { const m = md(u, 0.43); return m < 0.035 ? 1 : m < 0.06 ? -1 : 0; },
    ledge: (u, v) => { const j = fl(v / 0.11), uo = u + hsh(j, 4, 103) * 0.9, w = 0.35 + 0.55 * hsh(fl(uo / 0.6), j, 104), i = fl(uo / w); if (md(v, 0.11) < 0.018 || md(uo, w) < 0.02) return -2; const h = hsh(i, j, 106); return h < 0.3 ? -1 : h > 0.75 ? 1 : 0; },
    paver: (u, v) => { const r = fl(v / 0.2), uo = u + (r & 1) * 0.2; if (md(v, 0.2) < 0.016 || md(uo, 0.4) < 0.016) return -2; const h = hsh(fl(uo / 0.4), r, 107); return h < 0.25 ? -1 : h > 0.8 ? 1 : 0; },
    slabs: (u, v) => { if (md(u, 0.6) < 0.018 || md(v, 0.6) < 0.018) return -2; const h = hsh(fl(u / 0.6), fl(v / 0.6), 108); return h < 0.2 ? -1 : h > 0.85 ? 1 : 0; },
    form: (u, v) => md(v, 0.3) < 0.014 ? -1 : (hsh(fl(u / 1.2), fl(v / 0.3), 109) < 0.2 ? -1 : 0),
    gdoor: (u, v) => { const r = md(v, 0.53); if (r < 0.03) return -2; const c = md(u, 0.65); return c < 0.03 ? -1 : (r > 0.1 && r < 0.43 && c > 0.08 && c < 0.57) ? 1 : 0; },
    carriage: (u, v) => { const c = md(u, 1.3); if (c < 0.04) return -2; if (md(u, 0.13) < 0.012) return -1; return (Math.abs(md(u, 1.3) - 0.65) < 0.04 && v < 1.4) || Math.abs(v - 1.35) < 0.04 ? 1 : 0; },
    screen: (u, v) => (md(u, 0.07) < 0.012 || md(v, 0.07) < 0.012) ? 0 : -1,
    blockW: (u, v) => { const r = fl(v / 0.2), uo = u + (r & 1) * 0.225; if (md(v, 0.2) < 0.02 || md(uo, 0.45) < 0.02) return -2; const h = hsh(fl(uo / 0.45), r, 113); return h < 0.35 ? -1 : h > 0.8 ? 1 : 0; },
  });

  // ---- the ground round the house: a grid of heights from the main floor (o.g), bilinear -------------------------------------------------
  function grid(g) { if (!g || !g.z) return () => -0.5; const { e0, n0, s, w, h, z } = g;
    return (e, n) => { const fx = clamp((e - e0) / s, 0, w - 1.0001), fy = clamp((n - n0) / s, 0, h - 1.0001), i = fl(fx), j = fl(fy), tx = fx - i, ty = fy - j, k = j * w + i;
      return z[k] + (z[k + 1] - z[k]) * tx + (z[k + w] - z[k]) * ty + (z[k] - z[k + 1] - z[k + w] + z[k + w + 1]) * tx * ty; }; }
  // ---- a part's roof as a height over the plan --------------------------------------------------------------------------------------------
  function roofOf(P) { const R = P.roof || { t: 'flat' }, ec = (P.e0 + P.e1) / 2, nc = (P.n0 + P.n1) / 2, hw = (P.e1 - P.e0) / 2, hd = (P.n1 - P.n0) / 2, H = P.H, p = R.p || 0.7, rN = R.r === 'n';
    const dIn = (e, n) => rN ? hw - Math.abs(e - ec) : hd - Math.abs(n - nc); let z;
    if (R.t === 'flat') z = () => H;
    else if (R.t === 'shed') z = R.hi === 'N' ? (e, n) => H + p * (n - P.n0) : (e, n) => H + p * (P.n1 - n);
    else if (R.t === 'hip') z = (e, n) => H + p * Math.min(hd - Math.abs(n - nc), hw - Math.abs(e - ec));
    else if (R.t === 'gambrel') { const bk = R.bk || 1.0, pu = R.pu || 0.42; z = (e, n) => { const d = dIn(e, n); return H + (d <= bk ? p * d : p * bk + pu * (d - bk)); }; }
    else z = (e, n) => H + p * dIn(e, n);
    return { z, ec, nc, hw, hd, H, p, rN, R, zR: z(ec, nc) }; }
  const FACES = (P) => ({ S: { a: [P.e0, P.n0], b: [P.e1, P.n0], out: [0, -1] }, E: { a: [P.e1, P.n0], b: [P.e1, P.n1], out: [1, 0] }, N: { a: [P.e1, P.n1], b: [P.e0, P.n1], out: [0, 1] }, W: { a: [P.e0, P.n1], b: [P.e0, P.n0], out: [-1, 0] } });
  // a wall's top along it, from u = L back to 0, at each of the roof's breaks
  function topLine(Rf, f, L, cap) { const d = [(f.b[0] - f.a[0]) / L, (f.b[1] - f.a[1]) / L], pt = (u) => [f.a[0] + d[0] * u, f.a[1] + d[1] * u], us = new Set([0, L]), kE = [Rf.ec], kN = [Rf.nc];
    if (Rf.R.t === 'gambrel') { const bk = Rf.R.bk || 1; if (Rf.rN) kE.push(Rf.ec - Rf.hw + bk, Rf.ec + Rf.hw - bk); else kN.push(Rf.nc - Rf.hd + bk, Rf.nc + Rf.hd - bk); }
    for (const e of kE) if (Math.abs(d[0]) > 1e-6) { const u = (e - f.a[0]) / d[0]; if (u > 1e-3 && u < L - 1e-3) us.add(u); }
    for (const n of kN) if (Math.abs(d[1]) > 1e-6) { const u = (n - f.a[1]) / d[1]; if (u > 1e-3 && u < L - 1e-3) us.add(u); }
    return [...us].sort((x, y) => y - x).map(u => { const p = pt(u); return [u, cap != null ? Math.min(cap, Rf.z(p[0], p[1])) : Rf.z(p[0], p[1])]; }); }

  // ---- openings ---------------------------------------------------------------------------------------------------------------------------
  const TOK = { Q: [2.0, 0.08, 2.45], Z: [2.0, 0.3, 2.3], w: [0.9, 0.85, 2.25], v: [0.7, 1.35, 2.15], W: [1.86, 0.85, 2.25], T: [0.9, 0.3, 2.3], P: [2.1, 0.6, 2.25], G: [2.6, 0.08, 2.45], h: [0.8, 0.0, 0.6], D: [0.95, 0, 2.1], F: [1.6, 0, 2.1], S: [2.4, 0, 2.15], g: [2.6, 0, 2.2] };
  function bays(pat, L, f0, m) { const toks = (pat || '').trim().split(/\s+/).filter(Boolean), out = []; if (!toks.length) return out; m = m != null ? m : 0.45; const bw = (L - 2 * m) / toks.length;
    toks.forEach((t0, k) => { const sp = t0.split(':'), t = sp[0], T = TOK[t]; if (!T) return; const uc = sp.length > 1 ? +sp[1] : m + bw * (k + 0.5); if (t === 'W') { out.push({ t: 'w', u0: uc - 0.93, u1: uc - 0.05, z0: f0 + T[1], z1: f0 + T[2] }, { t: 'w', u0: uc + 0.05, u1: uc + 0.93, z0: f0 + T[1], z1: f0 + T[2] }); return; }
      out.push({ t, u0: uc - T[0] / 2, u1: uc + T[0] / 2, z0: f0 + T[1], z1: f0 + T[2] }); }); return out; }
  function pane(C, Wf, h, o) { const M = Wf.M, fm = o.frame || 'frame', em = 'win:' + (C.win++), fw = 0.06;
    F(C, [M(h.u0, h.z0, -0.06), M(h.u1, h.z0, -0.06), M(h.u1, h.z1, -0.06), M(h.u0, h.z1, -0.06)], 'glass', { tag: 'win.glass', em });
    if (h.z1 - h.z0 > 0.6) F(C, [M(h.u0 + 0.1, h.z1 - 0.5, -0.055), M(h.u0 + 0.42, h.z1 - 0.5, -0.055), M(h.u0 + 0.16, h.z1 - 0.16, -0.055)], 'glassHi', { tag: 'win.glass', em });
    const T = (u0, u1, z0, z1, o0, o1) => fbox(C, M, u0, u1, z0, z1, o0, o1, fm, { tag: 'win.frame' });
    T(h.u0 - fw, h.u0, h.z0, h.z1, -0.06, 0.02); T(h.u1, h.u1 + fw, h.z0, h.z1, -0.06, 0.02); T(h.u0 - fw, h.u1 + fw, h.z1, h.z1 + fw, -0.06, 0.02); T(h.u0 - fw - 0.02, h.u1 + fw + 0.02, h.z0 - fw, h.z0, -0.06, 0.05);
    const [c, r] = o.mull || [1, 1]; for (let k = 1; k < c; k++) { const u = h.u0 + (h.u1 - h.u0) * k / c; T(u - 0.025, u + 0.025, h.z0, h.z1, -0.06, -0.02); }
    for (let k = 1; k < r; k++) { const z = h.z0 + (h.z1 - h.z0) * k / r; T(h.u0, h.u1, z - 0.025, z + 0.025, -0.06, -0.02); }
    if (o.transom) T(h.u0, h.u1, h.z1 - 0.48, h.z1 - 0.43, -0.06, -0.02); }
  function slide(C, Wf, h, fm) { const M = Wf.M, um = (h.u0 + h.u1) / 2;
    for (const [a, b, o0] of [[h.u0, um + 0.04, -0.1], [um - 0.04, h.u1, -0.06]]) { const em = 'win:' + (C.win++);
      F(C, [M(a + 0.07, h.z0 + 0.07, o0 - 0.004), M(b - 0.07, h.z0 + 0.07, o0 - 0.004), M(b - 0.07, h.z1 - 0.07, o0 - 0.004), M(a + 0.07, h.z1 - 0.07, o0 - 0.004)], 'glass', { tag: 'door.glass', em });
      F(C, [M(a + 0.16, h.z1 - 0.6, o0 + 0.001), M(a + 0.5, h.z1 - 0.6, o0 + 0.001), M(a + 0.22, h.z1 - 0.2, o0 + 0.001)], 'glassHi', { tag: 'door.glass', em });
      for (const [u0, u1, z0, z1] of [[a, a + 0.07, h.z0, h.z1], [b - 0.07, b, h.z0, h.z1], [a, b, h.z0, h.z0 + 0.07], [a, b, h.z1 - 0.07, h.z1]]) fbox(C, M, u0, u1, z0, z1, o0, o0 + 0.03, fm, { tag: 'door.frame' }); }
    for (const [u0, u1, z0, z1] of [[h.u0 - 0.08, h.u1 + 0.08, h.z1, h.z1 + 0.1], [h.u0 - 0.06, h.u0, h.z0, h.z1], [h.u1, h.u1 + 0.06, h.z0, h.z1]]) fbox(C, M, u0, u1, z0, z1, -0.03, 0.04, fm, { tag: 'door.trim' }); }
  function gdoor(C, Wf, h, g) { const M = Wf.M, m = g.mat || 'trimW';
    fbox(C, M, h.u0, h.u1, h.z0, h.z1, -0.1, -0.06, m, { tag: 'garage.door', tex: TEX[g.tex || 'gdoor'] });
    if (g.lites !== false) { const em = 'win:' + (C.win++), n = 4, wq = (h.u1 - h.u0 - 0.36) / n; for (let k = 0; k < n; k++) { const a = h.u0 + 0.18 + k * wq + 0.05, b = a + wq - 0.1;
      F(C, [M(a, h.z1 - 0.5, -0.055), M(b, h.z1 - 0.5, -0.055), M(b, h.z1 - 0.22, -0.055), M(a, h.z1 - 0.22, -0.055)], 'glass', { tag: 'garage.lite', em }); } }
    for (const [u0, u1, z0, z1] of [[h.u0 - 0.12, h.u0, h.z0, h.z1 + 0.12], [h.u1, h.u1 + 0.12, h.z0, h.z1 + 0.12], [h.u0 - 0.12, h.u1 + 0.12, h.z1, h.z1 + 0.14]]) fbox(C, M, u0, u1, z0, z1, -0.02, 0.05, g.trim || 'trimW', { tag: 'garage.trim' }); }
  function opening(C, Wf, h, face) { const o = C.s.o, fr = o.frame || 'trimW', mod = !!o.modern;
    if (h.t === 'D') return doorX(C, Wf, h, { mat: o.door || 'barn', glass: true, trim: fr, lantern: h.u1 + 0.35 });
    if (h.t === 'F') return doorX(C, Wf, h, { french: true, mat: mod ? fr : 'trimW', trim: fr });
    if (h.t === 'S') return slide(C, Wf, h, mod ? fr : 'trimW');
    if (h.t === 'g') return gdoor(C, Wf, h, o.gdoor || {});
    if (h.t === 'G' || h.t === 'Q') return pane(C, Wf, h, { frame: mod ? fr : 'trimW', mull: [h.t === 'G' ? 3 : 2, 1], transom: true });
    if (h.t === 'Z') { const M = Wf.M; F(C, [M(h.u0, h.z0, -0.05), M(h.u1, h.z0, -0.05), M(h.u1, h.z1, -0.05), M(h.u0, h.z1, -0.05)], 'dark', { tag: 'porch.screen', tex: TEX.screen, uv: [[h.u0, h.z0], [h.u1, h.z0], [h.u1, h.z1], [h.u0, h.z1]] });
      for (const [u0, u1, z0, z1] of [[h.u0 - 0.07, h.u0, h.z0, h.z1], [h.u1, h.u1 + 0.07, h.z0, h.z1], [h.u0 - 0.07, h.u1 + 0.07, h.z1, h.z1 + 0.08], [h.u0 - 0.07, h.u1 + 0.07, h.z0 - 0.08, h.z0], [h.u0, h.u1, h.z0 + 0.55, h.z0 + 0.62]]) fbox(C, M, u0, u1, z0, z1, -0.05, 0.03, fr, { tag: 'porch.screenFrame' }); return; }
    if (h.t === 'h') return pane(C, Wf, h, { frame: mod ? fr : 'trimW', mull: [2, 1] });
    if (mod) return pane(C, Wf, h, { frame: fr, mull: h.t === 'P' ? [3, 1] : h.t === 'T' ? [1, 2] : [1, 1] });
    const sh = o.shutters && face === 'S' && (h.t === 'w' || h.t === 'T') ? o.shutters : null;
    return sash(C, Wf, h, { lites: h.t === 'P' ? [3, 2] : (o.lites || [2, 2]), trim: fr, shutter: sh, box: o.boxes && face === 'S' && h.t === 'w' && h.z0 < 1.2 ? o.boxes : null }); }

  // ---- the stairs: a straight flight from top [e, n, z] along dir to the ground (risers about 0.185, treads 0.28) -----------------------
  function flight(C, top, dir, w, gr, o) { o = o || {}; const hw = w / 2, P = (s, x) => [top[0] + dir[0] * s - dir[1] * x, top[1] + dir[1] * s + dir[0] * x]; let N = 3, gF = top[2] - 0.5;
    if (o.zFoot != null) { gF = o.zFoot; N = Math.max(2, Math.round((top[2] - gF) / 0.185)); } else for (let it = 0; it < 4; it++) { const g = gr(...P(Math.max(0.3, (N - 1) * 0.28 + 0.1), 0)); N = Math.max(2, Math.round((top[2] - g) / 0.185)); gF = g; }
    const rz = (top[2] - gF) / N, run = (N - 1) * 0.28, mat = o.mat || 'deck';
    for (let k = 1; k < N; k++) { const s0 = (k - 1) * 0.28, s1 = k * 0.28, z1 = top[2] - k * rz, p = P(s0, -hw), q = P(s1, hw), zb = o.solid ? Math.min(gr(...P((s0 + s1) / 2, 0)), z1) - 0.18 : z1 - 0.05;
      box(C, Math.min(p[0], q[0]), Math.max(p[0], q[0]), Math.min(p[1], q[1]), Math.max(p[1], q[1]), zb, z1, mat, { tag: 'stair.tread', tex: o.tex ? TEX[o.tex] : null, topTex: o.topTex ? TEX[o.topTex] : (o.solid ? null : TEX.boardsE) }); }
    if (!o.solid) for (const x of [-hw - 0.03, hw + 0.03]) { const a = P(0, x), b = P(run + 0.28, x); stick(C, [a[0], a[1], top[2] - 0.12], [b[0], b[1], gF], 0.1, mat, { tag: 'stair.stringer' }); }
    if (o.rail && o.rail.t !== 'none') { const rm = o.rail.t === 'black' || o.rail.t === 'cable' || o.rail.t === 'glass' ? 'iron' : o.rail.t === 'wood' ? 'wood' : 'trimW';
      for (const x of [-hw - 0.06, hw + 0.06]) { const a = P(0, x), b = P(run, x); stick(C, [a[0], a[1], top[2] + 0.9], [b[0], b[1], gF + rz + 0.9], 0.07, o.rail.cap || rm, { tag: 'stair.rail' });
        for (const [s, zz] of [[0, top[2]], [run, gF + rz]]) { const p = P(s, x); box(C, p[0] - 0.04, p[0] + 0.04, p[1] - 0.04, p[1] + 0.04, zz - 0.05, zz + 0.95, rm, { tag: 'stair.post' }); }
        for (let s = 0.2; s < run - 0.1; s += 0.3) { const p = P(s, x), zz = top[2] - rz * (s / 0.28 + 0.5); box(C, p[0] - 0.015, p[0] + 0.015, p[1] - 0.015, p[1] + 0.015, zz, zz + 0.86, rm, { tag: 'stair.baluster', noTop: true }); } } }
    return { foot: P(run + 0.28, 0), zFoot: gF, run, N }; }
  // ---- a rail along a -> b at z: white or wood or black balusters (Shore Lane's rail), cable or glass between posts ----------------------
  function railX(C, a, b, z, r, gaps) { r = r || { t: 'white' }; if (r.t === 'none') return; const L = Math.hypot(b[0] - a[0], b[1] - a[1]); if (L < 0.05) return;
    if (r.t !== 'cable' && r.t !== 'glass') return rail(C, a, b, z, { mat: r.mat || (r.t === 'black' ? 'iron' : r.t === 'wood' ? 'wood' : 'trimW'), gaps, h: 0.95 });
    const d = [(b[0] - a[0]) / L, (b[1] - a[1]) / L], P = (s) => [a[0] + d[0] * s, a[1] + d[1] * s], pm = r.mat || 'iron';
    let runs = [[0, L]]; for (const [g0, g1] of gaps || []) runs = runs.flatMap(([s0, s1]) => (g1 <= s0 || g0 >= s1) ? [[s0, s1]] : [[s0, g0], [g1, s1]].filter(q => q[1] - q[0] > 0.05));
    for (const [s0, s1] of runs) { const n = Math.max(1, Math.ceil((s1 - s0) / 1.5)); for (let k = 0; k <= n; k++) { const p = P(s0 + (s1 - s0) * k / n); box(C, p[0] - 0.035, p[0] + 0.035, p[1] - 0.035, p[1] + 0.035, z, z + 0.98, pm, { tag: 'rail.post' }); }
      const A = P(s0), Bq = P(s1); stick(C, [A[0], A[1], z + 0.99], [Bq[0], Bq[1], z + 0.99], 0.08, r.cap || 'wood', { tag: 'rail.top' });
      if (r.t === 'cable') for (let c = 1; c <= 6; c++) stick(C, [A[0], A[1], z + 0.135 * c], [Bq[0], Bq[1], z + 0.135 * c], 0.018, 'steel', { tag: 'rail.cable' });
      else { const Q = [[A[0], A[1], z + 0.1], [Bq[0], Bq[1], z + 0.1], [Bq[0], Bq[1], z + 0.92], [A[0], A[1], z + 0.92]]; F(C, Q, 'glassHi', { tag: 'rail.glass', b: -1 }); F(C, Q.slice().reverse(), 'glassHi', { tag: 'rail.glass', b: -1 }); } } }

  // ---- a deck on posts: boards running E-W, a rim and a beam, posts to the ground, its rail open where its stairs leave -----------------------
  function deckX(C, d, gr) { const z = d.z, mat = d.mat || 'deck', { e0, e1, n0, n1 } = d, rz = z - 0.26, stone = d.posts === 'stone';
    flat(C, e0, e1, n0, n1, z, mat, { tag: 'deck.boards', tex: TEX.boardsE });
    box(C, e0 - 0.04, e1 + 0.04, n0 - 0.04, n0, rz, z, mat, { tag: 'deck.rim', noN: true }); box(C, e0 - 0.04, e0, n0, n1, rz, z, mat, { tag: 'deck.rim', noE: true }); box(C, e1, e1 + 0.04, n0, n1, rz, z, mat, { tag: 'deck.rim', noW: true });
    if (rz - 0.22 > gr((e0 + e1) / 2, n0) + 0.3) { box(C, e0, e1, n0 + 0.03, n0 + 0.22, rz - 0.24, rz, mat, { tag: 'deck.beam' });
      const np = Math.max(1, Math.round((e1 - e0) / (d.bay || 2.6))), pw = stone ? 0.23 : 0.085;
      for (let k = 0; k <= np; k++) { const e = e0 + 0.15 + (e1 - e0 - 0.3) * k / np, nn = n0 + 0.13, g = gr(e, nn);
        box(C, e - pw, e + pw, nn - pw, nn + pw, g - 0.15, rz - 0.24, stone ? (d.pier || 'stoneW') : (d.postMat || mat), { tag: 'deck.post', tex: stone ? TEX[d.pierTex || 'fieldstone'] : null }); } }
    const gaps = { S: [], E: [], W: [] }; for (const s of d.stairs || []) gaps[s.side].push(s.side === 'S' ? [s.at - e0 - s.w / 2, s.at - e0 + s.w / 2] : s.side === 'E' ? [s.at - n0 - s.w / 2, s.at - n0 + s.w / 2] : [n1 - s.at - s.w / 2, n1 - s.at + s.w / 2]);
    railX(C, [e0, n0], [e1, n0], z, d.rail, gaps.S); if (!d.noE) railX(C, [e1, n0], [e1, n1], z, d.rail, gaps.E); if (!d.noW) railX(C, [e0, n1], [e0, n0], z, d.rail, gaps.W);
    const out = []; for (const s of d.stairs || []) { const dir = { S: [0, -1], E: [1, 0], W: [-1, 0] }[s.side], top = s.side === 'S' ? [s.at, n0, z] : s.side === 'E' ? [e1, s.at, z] : [e0, s.at, z];
      out.push(flight(C, top, dir, s.w, gr, { mat: s.mat || mat, rail: d.rail })); } return out; }
  // ---- a porch: its floor on a skirt, columns and a beam on its open edge, a roof (shed or a pediment), a rail, its steps; or screened -------
  function porchX(C, p, gr) { const S = p.side !== 'N', nO = S ? p.n0 : p.n1, nI = S ? p.n1 : p.n0, sg = S ? -1 : 1, z = p.z, R = p.roof, ov = R.ov != null ? R.ov : 0.3, nOO = nO + sg * ov;
    const gmin = Math.min(gr(p.e0, nO), gr(p.e1, nO), gr((p.e0 + p.e1) / 2, nO)) - 0.15;
    box(C, p.e0, p.e1, p.n0, p.n1, gmin, z, p.skirt || 'stoneW', { tag: 'porch.floor', tex: TEX[p.skirtTex || 'fieldstone'], top: p.floor || 'deck', topTex: TEX[p.floorTex || 'boardsN'] });
    const cm = (p.cols && p.cols.mat) || 'trimW', nc = (p.cols && p.cols.n) || 4, ct = (p.cols && p.cols.t) || 'sq', zt = R.z0, cols = [];
    for (let k = 0; k < nc; k++) cols.push(p.e0 + 0.2 + (p.e1 - p.e0 - 0.4) * k / (nc - 1));
    for (const e of cols) { const n = nO - sg * 0.2;
      if (ct === 'taper') { box(C, e - 0.3, e + 0.3, n - 0.3, n + 0.3, z, z + 0.95, p.pier || 'stoneW', { tag: 'porch.pier', tex: TEX.fieldstone, top: 'cap' }); box(C, e - 0.17, e + 0.17, n - 0.17, n + 0.17, z + 0.95, z + 1.6, cm, { tag: 'porch.column' }); box(C, e - 0.13, e + 0.13, n - 0.13, n + 0.13, z + 1.6, zt - 0.3, cm, { tag: 'porch.column' }); }
      else if (ct === 'round') { cyl(C, e, n, 0.13, z, zt - 0.3, 10, cm, { tag: 'porch.column' }); box(C, e - 0.17, e + 0.17, n - 0.17, n + 0.17, z, z + 0.12, cm, { tag: 'porch.base' }); box(C, e - 0.17, e + 0.17, n - 0.17, n + 0.17, zt - 0.42, zt - 0.3, cm, { tag: 'porch.capital' }); }
      else box(C, e - 0.1, e + 0.1, n - 0.1, n + 0.1, z, zt - 0.3, cm, { tag: 'porch.post' }); }
    box(C, p.e0 - 0.05, p.e1 + 0.05, nO - sg * 0.32 - 0.12, nO - sg * 0.32 + 0.12, zt - 0.32, zt, cm, { tag: 'porch.beam' });
    const rm = R.mat || 'roofA', tx = TEX[R.tex || 'roofSh'], fm = R.fascia || 'trimW', E0 = p.e0 - ov, E1 = p.e1 + ov;
    if (R.t === 'gable') { const ec = (p.e0 + p.e1) / 2, pg = R.p || 0.55, zr = zt + pg * (E1 - E0) / 2, n1 = nI;
      if (S) { roofQ(C, [E1, nOO, zt], [E1, n1, zt], [ec, n1, zr], [ec, nOO, zr], rm, { tex: tx }); roofQ(C, [E0, n1, zt], [E0, nOO, zt], [ec, nOO, zr], [ec, n1, zr], rm, { tex: tx }); }
      else { roofQ(C, [E1, n1, zt], [E1, nOO, zt], [ec, nOO, zr], [ec, n1, zr], rm, { tex: tx }); roofQ(C, [E0, nOO, zt], [E0, n1, zt], [ec, n1, zr], [ec, nOO, zr], rm, { tex: tx }); }
      F(C, S ? [[E0 + 0.12, nOO + 0.04, zt], [E1 - 0.12, nOO + 0.04, zt], [ec, nOO + 0.04, zr - 0.12]] : [[E1 - 0.12, nOO - 0.04, zt], [E0 + 0.12, nOO - 0.04, zt], [ec, nOO - 0.04, zr - 0.12]], fm, { tag: 'porch.pediment' });
      for (const sgn of [-1, 1]) stick(C, [ec + sgn * (E1 - ec), nOO, zt - 0.03], [ec, nOO, zr - 0.03], 0.14, fm, { tag: 'porch.rake' }); stick(C, [E0, nOO, zt - 0.06], [E1, nOO, zt - 0.06], 0.16, fm, { tag: 'porch.fascia' }); }
    else { const z1 = R.z1 != null ? R.z1 : zt + 0.7, zl = zt - ov * (z1 - zt) / Math.abs(nI - nO);
      if (S) roofQ(C, [E0, nOO, zl], [E1, nOO, zl], [E1, nI, z1], [E0, nI, z1], rm, { tex: tx }); else roofQ(C, [E1, nOO, zl], [E0, nOO, zl], [E0, nI, z1], [E1, nI, z1], rm, { tex: tx });
      stick(C, [E0, nOO, zl - 0.07], [E1, nOO, zl - 0.07], 0.16, fm, { tag: 'porch.fascia' }); for (const e of [E0, E1]) stick(C, [e, nOO, zl - 0.04], [e, nI, z1 - 0.04], 0.12, fm, { tag: 'porch.rake' }); }
    const gaps = (p.steps || []).map(s => [s.at - p.e0 - s.w / 2 - 0.05, s.at - p.e0 + s.w / 2 + 0.05]);
    if (p.screen) { for (let k = 0; k + 1 < cols.length; k++) { const a = cols[k] + 0.12, b = cols[k + 1] - 0.12, n = nO - sg * 0.2; if (gaps.some(g => p.e0 + g[0] < b && p.e0 + g[1] > a)) continue;
        F(C, S ? [[a, n, z + 0.1], [b, n, z + 0.1], [b, n, zt - 0.32], [a, n, zt - 0.32]] : [[b, n, z + 0.1], [a, n, z + 0.1], [a, n, zt - 0.32], [b, n, zt - 0.32]], 'dark', { tag: 'porch.screen', tex: TEX.screen, uv: [[a, z], [b, z], [b, zt], [a, zt]] });
        box(C, a, b, n - 0.05, n + 0.05, z + 0.85, z + 0.92, cm, { tag: 'porch.screenRail' }); } }
    else if (p.rail) railX(C, [p.e0, nO - sg * 0.2], [p.e1, nO - sg * 0.2], z, { t: p.rail === true ? 'white' : p.rail, mat: cm }, gaps);
    for (const s of p.steps || []) flight(C, [s.at, nO, z], [0, sg], s.w, gr, { solid: true, mat: s.mat || 'stoneW', tex: s.tex || 'fieldstone', topTex: s.topTex || null });
    if (p.light) { const L = p.light; lamp(C, L[0], L[1], L[2], '#ffc774', 0.5, 4.2); } }
  // ---- a patio: its paving at z, and a course of its edge down to the lawn where the lawn falls away ------------------------------------------
  function patioX(C, p, gr) { const z = p.z, m = p.mat || 'paver';
    F(C, [[p.e0, p.n0, z], [p.e1, p.n0, z], [p.e1, p.n1, z], [p.e0, p.n1, z]], m, { tag: 'patio', tex: TEX[p.tex || 'paver'], uv: [[p.e0, p.n0], [p.e1, p.n0], [p.e1, p.n1], [p.e0, p.n1]] });
    const em = p.edge || 'stoneW', et = TEX[p.edgeTex || 'fieldstone'];
    for (let e = p.e0; e < p.e1 - 0.01; e += 0.5) { const e1 = Math.min(p.e1, e + 0.5), g = Math.min(gr(e + 0.05, p.n0 - 0.4), gr(e1 - 0.05, p.n0 - 0.4)); if (g < z - 0.06) box(C, e, e1, p.n0 - 0.14, p.n0, g - 0.15, z + 0.03, em, { tag: 'patio.edge', tex: et, top: 'cap', noN: true }); }
    for (const side of p.sides || []) { const e = side === 'E' ? p.e1 : p.e0; for (let n = p.n0; n < p.n1 - 0.01; n += 0.5) { const n1 = Math.min(p.n1, n + 0.5), g = gr(e + (side === 'E' ? 0.4 : -0.4), (n + n1) / 2); if (g < z - 0.06) box(C, side === 'E' ? e : e - 0.14, side === 'E' ? e + 0.14 : e, n, n1, g - 0.15, z + 0.03, em, { tag: 'patio.edge', tex: et, top: 'cap' }); } } }
  // ---- a retaining wall along an axis-aligned polyline, its face to the low side, its top stepping with the ground behind it -----------------
  function wallR(C, w, gr) { const t = w.t || 0.35, m = w.mat || 'stoneW', tx = TEX[w.tex || 'fieldstone'], cap = w.cap || m;
    for (let i = 0; i + 1 < w.pts.length; i++) { const a = w.pts[i], b = w.pts[i + 1], L = Math.hypot(b[0] - a[0], b[1] - a[1]); if (L < 0.05) continue; const d = [(b[0] - a[0]) / L, (b[1] - a[1]) / L], sd = w.side || 1, nr = [-d[1] * sd, d[0] * sd];
      const k = Math.max(1, Math.round(L / (w.step || 1.0)));
      for (let j = 0; j < k; j++) { const s0 = L * j / k, s1 = L * (j + 1) / k, sm = (s0 + s1) / 2, pe = a[0] + d[0] * sm + nr[0] * (t + 0.4), pn = a[1] + d[1] * sm + nr[1] * (t + 0.4);
        const top = w.top != null ? w.top : Math.round(Math.max(gr(pe, pn) + 0.1, w.z0 + (w.min || 0.3)) / 0.1) * 0.1; if (top <= w.z0 + 0.12) continue;
        const P0 = [a[0] + d[0] * s0, a[1] + d[1] * s0], P1 = [a[0] + d[0] * s1, a[1] + d[1] * s1], Q0 = [P0[0] + nr[0] * t, P0[1] + nr[1] * t], Q1 = [P1[0] + nr[0] * t, P1[1] + nr[1] * t];
        const xs = [P0[0], P1[0], Q0[0], Q1[0]], ys = [P0[1], P1[1], Q0[1], Q1[1]], e0 = Math.min(...xs), e1 = Math.max(...xs), n0 = Math.min(...ys), n1 = Math.max(...ys);
        box(C, e0, e1, n0, n1, w.z0 - 0.15, top, m, { tag: 'wall.retaining', tex: tx, noTop: true }); box(C, e0 - 0.03, e1 + 0.03, n0 - 0.03, n1 + 0.03, top, top + 0.07, cap, { tag: 'wall.cap' }); } } }
  // ---- garden furniture and fittings, each in its own frame (Shore Lane's at(): x to its right, y the way it faces) ------------------------
  function lounger(C, e, n, z, f, m) { const L = at(e, n, f), o = { tag: 'seat.lounger' }; lbox(C, L, -0.33, 0.33, -0.9, 0.55, z + 0.28, z + 0.36, m, o); for (const [x, y] of [[-0.3, -0.85], [0.3, -0.85], [-0.3, 0.5], [0.3, 0.5]]) lbox(C, L, x - 0.025, x + 0.025, y - 0.025, y + 0.025, z, z + 0.28, 'wood', o);
    lbox(C, L, -0.32, 0.32, -0.92, -0.62, z + 0.36, z + 0.88, m, o); lbox(C, L, -0.3, 0.3, -0.3, 0.52, z + 0.36, z + 0.44, 'cushion', { tag: 'seat.cushion', b: 1 }); }
  function grill(C, e, n, z, f) { const L = at(e, n, f); lbox(C, L, -0.4, 0.4, -0.25, 0.25, z + 0.55, z + 0.95, 'iron', { tag: 'grill.box' }); lbox(C, L, -0.38, 0.38, -0.22, 0.22, z + 0.95, z + 1.12, 'iron', { tag: 'grill.lid', b: 1 });
    for (const [x, y] of [[-0.35, -0.2], [0.35, -0.2], [-0.35, 0.2], [0.35, 0.2]]) lbox(C, L, x - 0.025, x + 0.025, y - 0.025, y + 0.025, z, z + 0.55, 'steel', { tag: 'grill.leg' }); lbox(C, L, 0.42, 0.75, -0.2, 0.2, z + 0.8, z + 0.84, 'steel', { tag: 'grill.shelf' }); }
  function firepit(C, e, n, z, m, lit) { cyl(C, e, n, 0.62, z, z + 0.42, 12, m || 'stoneW', { tag: 'fire.ring', top: 'cap' }); cyl(C, e, n, 0.44, z + 0.42, z + 0.43, 12, 'dark', { tag: 'fire.bowl' });
    for (const [a, b] of [[-0.18, -0.05], [0.12, 0.1], [0.0, -0.16]]) box(C, e + a - 0.16, e + a + 0.16, n + b - 0.05, n + b + 0.05, z + 0.43, z + 0.52, 'wood', { tag: 'fire.log' }); if (lit) lamp(C, e, n, z + 0.8, '#ffa04a', 0.9, 5.5); }
  function hottub(C, e, n, z, m, cover) { box(C, e - 1.1, e + 1.1, n - 1.1, n + 1.1, z, z + 0.92, m || 'cedarN', { tag: 'tub.cabinet', tex: TEX.vboard, top: cover ? 'char' : 'cap' });
    if (cover) { box(C, e - 1.12, e + 1.12, n - 1.12, n + 1.12, z + 0.92, z + 1.02, 'char', { tag: 'tub.cover', topTex: TEX.cover }); } else flat(C, e - 0.92, e + 0.92, n - 0.92, n + 0.92, z + 0.93, 'aqua', { tag: 'tub.water' });
    box(C, e - 0.5, e + 0.5, n - 1.55, n - 1.1, z, z + 0.3, m || 'cedarN', { tag: 'tub.step', tex: TEX.vboard }); }
  function pergola(C, e0, e1, n0, n1, z, h, m) { for (const e of [e0, e1]) for (const n of [n0, n1]) box(C, e - 0.08, e + 0.08, n - 0.08, n + 0.08, z, z + h, m, { tag: 'pergola.post' });
    for (const n of [n0, n1]) box(C, e0 - 0.35, e1 + 0.35, n - 0.05, n + 0.05, z + h - 0.25, z + h, m, { tag: 'pergola.beam' });
    for (let e = e0 - 0.2; e <= e1 + 0.2; e += 0.42) box(C, e - 0.03, e + 0.03, n0 - 0.4, n1 + 0.4, z + h, z + h + 0.16, m, { tag: 'pergola.rafter' }); }
  function kitchenO(C, e0, e1, n0, n1, z) { box(C, e0, e1, n0, n1, z, z + 0.9, 'stoneD', { tag: 'kitchen.base', tex: TEX.ledge, top: 'conc' }); const em = (e0 + e1) / 2;
    box(C, em - 0.4, em + 0.4, n0 + 0.08, n1 - 0.08, z + 0.9, z + 1.05, 'steel', { tag: 'kitchen.grill', b: 1 }); box(C, em - 0.42, em + 0.42, n1 - 0.12, n1 - 0.04, z + 1.05, z + 1.45, 'steel', { tag: 'kitchen.hood' }); }
  function bench(C, e, n, z, f, m) { const L = at(e, n, f), o = { tag: 'seat.bench' }; lbox(C, L, -0.75, 0.75, -0.2, 0.2, z + 0.42, z + 0.47, m, o); lbox(C, L, -0.75, 0.75, -0.24, -0.2, z + 0.47, z + 0.88, m, { tag: o.tag, tex: TEX.slats });
    for (const x of [-0.65, 0.65]) lbox(C, L, x - 0.04, x + 0.04, -0.2, 0.2, z, z + 0.42, m, o); }
  function shrub(C, e, n, z, r, m, bloom) { cyl(C, e, n, r, z, z + r * 1.1, 9, m || 'boxw', { tag: 'plant.shrub', top: m || 'boxw' }); cyl(C, e, n, r * 0.72, z + r * 1.1, z + r * 1.45, 8, m || 'boxw', { tag: 'plant.shrub', top: bloom || m || 'boxw' }); }
  function furnish(C, f, zOf) { const z = zOf(f), m = f.m;
    switch (f.k) {
      case 'adk': return adk(C, f.e, f.n, z, f.f || 'S', m || 'trimW');
      case 'chair': return chairD(C, f.e, f.n, z, f.f || 'S', m || 'wood');
      case 'table': return table(C, f.e - f.w / 2, f.e + f.w / 2, f.n - f.d / 2, f.n + f.d / 2, z, f.h || 0.74, m || 'wood', { topTex: TEX.boardsE });
      case 'rtable': return rtable(C, f.e, f.n, z, f.r || 0.45, f.h || 0.72, m || 'iron');
      case 'umb': return umbrella(C, f.e, f.n, z, f.r || 1.3, f.h || 2.3, m || 'navy', f.m2 || 'trimW');
      case 'sofa': return sofa(C, f.e, f.n, z, f.f || 'S', f.w || 2.0, m || 'cushion');
      case 'lounger': return lounger(C, f.e, f.n, z, f.f || 'S', m || 'wood');
      case 'grill': return grill(C, f.e, f.n, z, f.f || 'S');
      case 'firepit': return firepit(C, f.e, f.n, z, m, f.lit);
      case 'hottub': return hottub(C, f.e, f.n, z, m, f.cover);
      case 'pergola': return pergola(C, f.e0, f.e1, f.n0, f.n1, z, f.h || 2.6, m || 'cedarN');
      case 'kitchen': return kitchenO(C, f.e0, f.e1, f.n0, f.n1, z);
      case 'bench': return bench(C, f.e, f.n, z, f.f || 'S', m || 'wood');
      case 'plant': return plant(C, f.e, f.n, z, f.s || 1, f.bloom || null);
      case 'shrub': return shrub(C, f.e, f.n, z, f.r || 0.5, m, f.bloom);
      case 'lamp': { box(C, f.e - 0.08, f.e + 0.08, f.n - 0.08, f.n + 0.08, z, z + 0.3, 'iron', { tag: 'lantern', em: 'glow:cream' }); lamp(C, f.e, f.n, z + 0.15, '#ffc774', 0.45, 3.6); return; }
      default: return; } }

  /* ==================================================================================================================================
     THE HOUSE ======================================================================================================================== */
  B.creekHouse = function (C) {
    const o = C.s.o, gr = grid(o.g), W0 = o.walkout || null, BH = W0 ? (W0.h || 2.75) : 0, zB = -BH, FD = o.found || { mat: 'conc', tex: 'form' }, fr = o.frame || 'trimW';
    C.floor = 0; let ex0 = 1e9, ex1 = -1e9, ey0 = 1e9, ey1 = -1e9, zTop = 0; const grow = (e0, e1, n0, n1) => { ex0 = Math.min(ex0, e0); ex1 = Math.max(ex1, e1); ey0 = Math.min(ey0, n0); ey1 = Math.max(ey1, n1); };
    for (const P of o.parts) { const Rf = roofOf(P), Fc = FACES(P), side = P.side || { mat: 'clapW', tex: 'clap' }, sm = side.mat, stx = TEX[side.tex || 'clap'], pz = P.z0 || 0, up = P.up != null ? P.up : 2.75, tm = P.trim || o.trim || 'trimW';
      grow(P.e0, P.e1, P.n0, P.n1); zTop = Math.max(zTop, Rf.zR);
      for (const k of ['S', 'E', 'N', 'W']) { if (P.skip && P.skip.includes(k)) continue; const f = Fc[k], L = Math.hypot(f.b[0] - f.a[0], f.b[1] - f.a[1]), pat = P[k] || {}, ptAt = (u) => [f.a[0] + (f.b[0] - f.a[0]) * u / L, f.a[1] + (f.b[1] - f.a[1]) * u / L];
        // the foundation, or the walkout's storey on the south face: from the ground to the floor
        const wo = W0 && k === 'S' && P.base === 'walkout', fb = []; for (let u = 0; u <= L + 1e-6; u += Math.max(0.5, L / Math.ceil(L))) { const p = ptAt(Math.min(u, L)); fb.push([Math.min(u, L), Math.min(gr(p[0] + f.out[0] * 0.3, p[1] + f.out[1] * 0.3), pz - 0.18) - 0.12]); }
        if (wo) for (const q of fb) q[1] = Math.min(q[1], zB - 0.12);
        const fhol = bays(pat.b, L, wo ? zB : Math.min(...fb.map(q => q[1])) + 0.12).filter(h => wo || h.t === 'h'); if (!wo) for (const h of fhol) { h.z0 = pz - 0.7; h.z1 = pz - 0.12; }
        const fo = fb.concat([[L, pz], [0, pz]]), fz0 = Math.max(...fb.map(q => q[1])), fh = fhol.filter(h => wo || h.z0 > fz0 + 0.05), Ff = wallX(C, { a: f.a, b: f.b, out: f.out, outline: fo, holes: fh, mat: wo ? W0.mat : FD.mat, tex: TEX[wo ? (W0.tex || 'fieldstone') : (FD.tex || 'form')], inMat: 'conc', revMat: wo ? fr : 'conc', t: 0.24, tag: wo ? 'house.walkout' : 'house.foundation' });
        for (const h of fh) opening(C, Ff, h, k);
        // the siding: from the floor to the eave, the gable to the roof; a glazed gable cut at the eave
        const glassG = P.glass === k, outline = [[0, pz], [L, pz]].concat(topLine(Rf, f, L, glassG ? P.H : null)), hol = bays(pat.g, L, pz).concat(bays(pat.u, L, pz + up)).concat(bays(pat.a, L, P.H + 0.12, pat.am != null ? pat.am : L * 0.3));
        const Wf = wallX(C, { a: f.a, b: f.b, out: f.out, outline, holes: hol, mat: sm, tex: stx, inMat: 'plaster', revMat: fr, t: 0.18, tag: 'house.' + P.id });
        for (const h of hol) opening(C, Wf, h, k);
        fbox(C, Wf.M, -0.05, L + 0.05, pz - 0.02, pz + 0.13, 0, 0.05, tm, { tag: 'house.watertable' });
        if (glassG) { const ec = Rf.ec, zr = Rf.zR - 0.35, hwg = (P.e1 - P.e0) / 2 - 0.45, nF = f.a[1] + f.out[1] * 0.0, zG = P.H + 0.15, Mf = (e, z, off) => [e, nF + f.out[1] * off, z], em = 'win:' + (C.win++);
          const tri = [Mf(ec - hwg, zG, -0.06), Mf(ec + hwg, zG, -0.06), Mf(ec, zG + (zr - zG) * 1.0, -0.06)]; F(C, k === 'S' ? tri : tri.slice().reverse(), 'glass', { tag: 'win.glass', em });
          F(C, k === 'S' ? [Mf(ec - 1.0, zG + 0.6, -0.055), Mf(ec - 0.2, zG + 0.6, -0.055), Mf(ec - 0.7, zG + 1.4, -0.055)] : [Mf(ec - 0.2, zG + 0.6, 0.055), Mf(ec - 1.0, zG + 0.6, 0.055), Mf(ec - 0.7, zG + 1.4, 0.055)], 'glassHi', { tag: 'win.glass', em });
          const Rk = (a, b) => { const v = [b[0] - a[0], b[2] - a[2]], L2 = Math.hypot(v[0], v[1]); stick(C, [a[0], nF + f.out[1] * 0.02, a[2]], [b[0], nF + f.out[1] * 0.02, b[2]], 0.12, fr, { tag: 'win.frame' }); void L2; };
          Rk([ec - hwg, 0, zG], [ec + hwg, 0, zG]); Rk([ec - hwg, 0, zG], [ec, 0, zr]); Rk([ec + hwg, 0, zG], [ec, 0, zr]); for (const de of [-hwg / 2, 0, hwg / 2]) { const zt = zG + (zr - zG) * (1 - Math.abs(de) / hwg); Rk([ec + de, 0, zG], [ec + de, 0, zt]); }
          const band = (pts) => { const Q = pts.map(q => [q[0], nF + f.out[1] * 0.004, q[1]]); F(C, k === 'S' ? Q : Q.slice().reverse(), sm, { tag: 'house.gableBand', tex: stx, uv: Q.map(q => [q[0], q[2]]) }); };
          band([[P.e0, P.H], [P.e1, P.H], [ec + hwg, zG], [ec - hwg, zG]]); band([[P.e0, P.H], [ec - hwg, zG], [ec, zr], [ec, Rf.zR]]); band([[ec + hwg, zG], [P.e1, P.H], [ec, Rf.zR], [ec, zr]]); }
      }
      // corner boards; the roof
      if (P.corners !== false) for (const [e, n] of [[P.e0, P.n0], [P.e1, P.n0], [P.e0, P.n1], [P.e1, P.n1]]) { const se = e > (P.e0 + P.e1) / 2 ? 1 : -1, sn = n > (P.n0 + P.n1) / 2 ? 1 : -1; box(C, e - 0.075 + 0.04 * se, e + 0.075 + 0.04 * se, n - 0.075 + 0.04 * sn, n + 0.075 + 0.04 * sn, pz - 0.05, P.H, tm, { tag: 'wall.corner' }); }
      roofX(C, P, Rf);
    }
    for (const d of o.dormers || []) dormerX(C, o.parts.find(p => p.id === (d.part || 'body')), d, fr);
    for (const c of o.chimneys || []) { const zb = c.ground ? Math.min(gr(c.e0, c.n0), gr(c.e1, c.n0)) - 0.15 : c.z0; box(C, c.e0, c.e1, c.n0, c.n1, zb, c.z1, c.mat || 'brick', { tag: 'roof.chimney', tex: TEX[c.tex || 'brick'] });
      box(C, c.e0 - 0.06, c.e1 + 0.06, c.n0 - 0.06, c.n1 + 0.06, c.z1, c.z1 + 0.1, c.capMat || 'conc', { tag: 'roof.chimneyCap' }); if (c.flue) cyl(C, (c.e0 + c.e1) / 2, (c.n0 + c.n1) / 2, 0.1, c.z1 + 0.1, c.z1 + 0.35, 6, 'iron', { tag: 'roof.flue' }); zTop = Math.max(zTop, c.z1 + 0.35); }
    for (const p of o.patios || []) { patioX(C, p, gr); grow(p.e0, p.e1, p.n0, p.n1); }
    for (const w of o.walls || []) wallR(C, w, gr);
    const stairs = []; for (const d of o.decks || []) { stairs.push(...deckX(C, d, gr)); grow(d.e0, d.e1, d.n0, d.n1); }
    for (const p of o.porches || []) { porchX(C, p, gr); grow(p.e0, p.e1, p.n0, p.n1); }
    for (const s of o.steps || []) flight(C, s.top, s.dir, s.w, gr, { solid: true, mat: s.mat || 'stoneW', tex: s.tex || 'fieldstone', topTex: s.topTex || null, zFoot: s.zFoot });
    const zOf = (f) => typeof f.z === 'number' ? f.z : f.z === 'patio' ? (o.patioZ != null ? o.patioZ : zB - 0.05) : f.z === 'deck' ? ((o.decks && o.decks[0]) ? o.decks[0].z : 0) : gr(f.e, f.n);
    for (const f of o.furn || []) furnish(C, f, zOf);
    for (const L of o.lights || []) lamp(C, L[0], L[1], L[2], '#ffc774', 0.5, 4.2);
    C.dims = { w: r2(ex1 - ex0), d: r2(ey1 - ey0), h: r2(zTop - Math.min(zB, -0.6)), floor: 0, basement: W0 ? zB : null, foot: [r2(ex0), r2(ex1), r2(ey0), r2(ey1)] };
    C.indoor = [o.parts[0].e0, o.parts[0].e1, o.parts[0].n0, o.parts[0].n1, 0.5, 0.9];
    const dr = o.entry || { e: 0, n: o.parts[0].n1, facing: 'N' }; C.door = { e: dr.e, n: dr.n, z: dr.z || 0, width: 0.95, height: 2.1, facing: dr.facing };
    C.st.push({ id: 'frontDoor', verb: 'enter', at: [dr.e, dr.n, dr.z || 0], stand: [dr.e, dr.n + (dr.facing === 'S' ? -1.2 : 1.2)] });
    for (const s of o.stations || []) C.st.push(s); stairs.forEach((s, k) => C.st.push({ id: 'deckStair' + k, verb: 'descend', at: [s.foot[0], s.foot[1], s.zFoot], stand: [s.foot[0], s.foot[1]] }));
  };
  const r2 = (v) => Math.round(v * 100) / 100;
  // the roof of a part, its fascias, rakes and ridge
  function roofX(C, P, Rf) { const R = P.roof || { t: 'flat' }, ov = R.ov != null ? R.ov : 0.4, mat = R.mat || 'roofA', tex = TEX[R.tex || 'roofSh'], fm = R.fascia || 'trimW', p = Rf.p, H = P.H, E0 = P.e0 - ov, E1 = P.e1 + ov, N0 = P.n0 - ov, N1 = P.n1 + ov;
    const rq = (a0, a1, b1, b0) => roofQ(C, a0, a1, b1, b0, mat, { tex }), tri = (a, b, c) => { const L = Math.hypot(b[0] - a[0], b[1] - a[1]); F(C, [a, b, c], mat, { tag: 'roof', tex, uv: [[0, 0], [L, 0], [L / 2, Math.hypot(c[0] - (a[0] + b[0]) / 2, c[1] - (a[1] + b[1]) / 2, c[2] - a[2])]] }); };
    const fas = (a, b) => stick(C, a, b, 0.17, fm, { tag: 'roof.fascia' });
    if (R.t === 'flat') { box(C, E0, E1, N0, N1, H, H + 0.3, fm, { tag: 'roof.flat', top: mat, topTex: TEX.membrane }); return; }
    if (R.t === 'shed') { const hiS = R.hi !== 'N', zLo = H - ov * p, zHi = H + p * (P.n1 - P.n0) + ov * p;
      if (hiS) rq([E1, N1, zLo], [E0, N1, zLo], [E0, N0, zHi], [E1, N0, zHi]); else rq([E0, N0, zLo], [E1, N0, zLo], [E1, N1, zHi], [E0, N1, zHi]);
      const nL = hiS ? N1 : N0, nH = hiS ? N0 : N1; fas([E0, nL, zLo - 0.08], [E1, nL, zLo - 0.08]); fas([E0, nH, zHi - 0.08], [E1, nH, zHi - 0.08]); for (const e of [E0, E1]) stick(C, [e, nL, zLo - 0.05], [e, nH, zHi - 0.05], 0.15, fm, { tag: 'roof.rake' }); return; }
    if (R.t === 'hip') { const zE = H - ov * p, zR = Rf.zR, ec = Rf.ec, nc = Rf.nc, hwE = (E1 - E0) / 2, hdE = (N1 - N0) / 2;
      if (hwE >= hdE) { const r = hwE - hdE, A = [ec - r, nc, zR], Bp = [ec + r, nc, zR]; rq([E0, N0, zE], [E1, N0, zE], Bp, A); rq([E1, N1, zE], [E0, N1, zE], A, Bp); tri([E1, N0, zE], [E1, N1, zE], Bp); tri([E0, N1, zE], [E0, N0, zE], A);
        if (r > 0.01) stick(C, A, Bp, 0.15, mat, { tag: 'roof.ridge' }); for (const [q, c] of [[[E0, N0, zE], A], [[E0, N1, zE], A], [[E1, N0, zE], Bp], [[E1, N1, zE], Bp]]) stick(C, q, c, 0.13, mat, { tag: 'roof.hip' }); }
      else { const r = hdE - hwE, A = [ec, nc - r, zR], Bp = [ec, nc + r, zR]; roofQ(C, [E1, N0, zE], [E1, N1, zE], Bp, A, mat, { tex }); roofQ(C, [E0, N1, zE], [E0, N0, zE], A, Bp, mat, { tex }); tri([E0, N0, zE], [E1, N0, zE], A); tri([E1, N1, zE], [E0, N1, zE], Bp);
        if (r > 0.01) stick(C, A, Bp, 0.15, mat, { tag: 'roof.ridge' }); for (const [q, c] of [[[E0, N0, zE], A], [[E1, N0, zE], A], [[E0, N1, zE], Bp], [[E1, N1, zE], Bp]]) stick(C, q, c, 0.13, mat, { tag: 'roof.hip' }); }
      fas([E0, N0, zE - 0.08], [E1, N0, zE - 0.08]); fas([E1, N1, zE - 0.08], [E0, N1, zE - 0.08]); fas([E0, N0, zE - 0.08], [E0, N1, zE - 0.08]); fas([E1, N0, zE - 0.08], [E1, N1, zE - 0.08]); return; }
    if (R.t === 'gambrel') { const bk = R.bk || 1.0, pu = R.pu || 0.42, zE = H - ov * p, zK = H + p * bk, zR = Rf.zR, nc = Rf.nc, k0 = P.n0 + bk, k1 = P.n1 - bk;
      rq([E0, N0, zE], [E1, N0, zE], [E1, k0, zK], [E0, k0, zK]); rq([E0, k0, zK], [E1, k0, zK], [E1, nc, zR], [E0, nc, zR]); rq([E1, N1, zE], [E0, N1, zE], [E0, k1, zK], [E1, k1, zK]); rq([E1, k1, zK], [E0, k1, zK], [E0, nc, zR], [E1, nc, zR]);
      stick(C, [E0 - 0.02, nc, zR + 0.04], [E1 + 0.02, nc, zR + 0.04], 0.15, mat, { tag: 'roof.ridge' }); for (const e of [E0, E1]) { for (const [a, b] of [[[N0, zE], [k0, zK]], [[k0, zK], [nc, zR]], [[N1, zE], [k1, zK]], [[k1, zK], [nc, zR]]]) stick(C, [e, a[0], a[1] - 0.04], [e, b[0], b[1] - 0.04], 0.13, fm, { tag: 'roof.rake' }); }
      fas([E0, N0, zE - 0.08], [E1, N0, zE - 0.08]); fas([E0, N1, zE - 0.08], [E1, N1, zE - 0.08]); return; }
    const zE = H - ov * p, zR = Rf.zR;
    if (!Rf.rN) { rq([E0, N0, zE], [E1, N0, zE], [E1, Rf.nc, zR], [E0, Rf.nc, zR]); rq([E1, N1, zE], [E0, N1, zE], [E0, Rf.nc, zR], [E1, Rf.nc, zR]);
      stick(C, [E0 - 0.02, Rf.nc, zR + 0.04], [E1 + 0.02, Rf.nc, zR + 0.04], 0.15, mat, { tag: 'roof.ridge' }); for (const e of [E0, E1]) for (const n of [N0, N1]) stick(C, [e, n, zE - 0.04], [e, Rf.nc, zR - 0.04], 0.13, fm, { tag: 'roof.rake' });
      for (const n of [N0, N1]) fas([E0, n, zE - 0.1], [E1, n, zE - 0.1]); }
    else { const ec = Rf.ec; rq([E1, N0, zE], [E1, N1, zE], [ec, N1, zR], [ec, N0, zR]); rq([E0, N1, zE], [E0, N0, zE], [ec, N0, zR], [ec, N1, zR]);
      stick(C, [ec, N0 - 0.02, zR + 0.04], [ec, N1 + 0.02, zR + 0.04], 0.15, mat, { tag: 'roof.ridge' }); for (const n of [N0, N1]) for (const e of [E0, E1]) stick(C, [e, n, zE - 0.04], [ec, n, zR - 0.04], 0.13, fm, { tag: 'roof.rake' });
      for (const e of [E0, E1]) fas([e, N0, zE - 0.1], [e, N1, zE - 0.1]); } }
  // a dormer on a part's south slope: gabled (its own small gable to the camera) or a shed dormer (a long low roof back to the main one)
  function dormerX(C, P, d, fr) { const Rf = roofOf(P), z = (n) => Rf.z(d.e, n), inv = (zz) => { let a = P.n0, b = Rf.nc; for (let i = 0; i < 30; i++) { const m = (a + b) / 2; if (z(m) < zz) a = m; else b = m; } return (a + b) / 2; };
    const hw = d.w / 2, nd = P.n0 + (d.n != null ? d.n : 0.7), zf = z(nd) - 0.06, sm = d.mat || (P.side && P.side.mat) || 'clapW', stx = TEX[d.tex || (P.side && P.side.tex) || 'clap'], rm = (P.roof && P.roof.mat) || 'roofA', rtx = TEX[(P.roof && P.roof.tex) || 'roofSh'], fm = (P.roof && P.roof.fascia) || 'trimW';
    if (d.t === 'shed') { const zE = zf + (d.h || 1.45), ps = d.p || 0.28, meet = (() => { let a = nd, b = Rf.nc; for (let i = 0; i < 30; i++) { const m = (a + b) / 2; if (z(m) < zE + ps * (m - nd)) a = m; else b = m; } return (a + b) / 2; })(), zM = zE + ps * (meet - nd);
      const hol = bays(d.win || 'w w w', d.w, zf + 0.15 - 0.85 + 0.0, 0.3).map(h => Object.assign(h, { z0: zf + 0.2, z1: Math.min(zE - 0.12, zf + 0.2 + (h.z1 - h.z0)) }));
      const Wd = wallX(C, { a: [d.e - hw, nd], b: [d.e + hw, nd], out: [0, -1], top: zE, z0: zf, holes: hol, mat: sm, tex: stx, inMat: 'plaster', revMat: fr, t: 0.12, tag: 'house.dormer' }); for (const h of hol) opening(C, Wd, h, 'D');
      for (const sgn of [-1, 1]) { const e = d.e + sgn * hw, pts = [[e, nd, zf], [e, meet, zM], [e, nd, zE]]; F(C, sgn > 0 ? pts : [pts[1], pts[0], pts[2]], sm, { tag: 'house.dormerCheek', tex: stx, uv: pts.map(q => [q[1], q[2]]) }); }
      roofQ(C, [d.e - hw - 0.15, nd - 0.25, zE - 0.07], [d.e + hw + 0.15, nd - 0.25, zE - 0.07], [d.e + hw + 0.15, meet, zM], [d.e - hw - 0.15, meet, zM], rm, { tex: rtx }); stick(C, [d.e - hw - 0.15, nd - 0.26, zE - 0.12], [d.e + hw + 0.15, nd - 0.26, zE - 0.12], 0.14, fm, { tag: 'roof.fascia' }); return; }
    const zdE = zf + (d.h || 1.3), zdR = zdE + (d.p || 0.9) * hw, hol = [{ t: d.win === 'W' ? 'w' : 'w', u0: hw - (d.win === 'W' ? 0.62 : 0.36), u1: hw + (d.win === 'W' ? 0.62 : 0.36), z0: zf + 0.18, z1: zdE - 0.1 }];
    const Wd = wallX(C, { a: [d.e - hw, nd], b: [d.e + hw, nd], out: [0, -1], outline: [[0, zf], [2 * hw, zf], [2 * hw, zdE], [hw, zdR], [0, zdE]], holes: hol, mat: sm, tex: stx, inMat: 'plaster', revMat: fr, t: 0.12, tag: 'house.dormer' }); for (const h of hol) opening(C, Wd, h, 'D');
    for (const sgn of [1, -1]) { const e = d.e + sgn * hw, pts = [[e, nd, zf + 0.06], [e, inv(zdE), zdE], [e, nd, zdE]]; F(C, sgn > 0 ? pts : [pts[1], pts[0], pts[2]], sm, { tag: 'house.dormerCheek', tex: stx, uv: pts.map(q => [q[1], q[2]]) }); }
    const de = hw + 0.14, nf = nd - 0.16; roofQ(C, [d.e + de, nf, zdE - 0.06], [d.e + de, inv(zdE - 0.06), zdE - 0.06], [d.e, inv(zdR), zdR], [d.e, nf, zdR], rm, { tex: rtx }); roofQ(C, [d.e - de, inv(zdE - 0.06), zdE - 0.06], [d.e - de, nf, zdE - 0.06], [d.e, nf, zdR], [d.e, inv(zdR), zdR], rm, { tex: rtx });
    for (const sgn of [1, -1]) stick(C, [d.e + sgn * de, nf, zdE - 0.08], [d.e, nf, zdR - 0.02], 0.1, fm, { tag: 'roof.rake' }); }

  Object.assign(K, { creekHouse: { label: 'A waterfront house on the north creek', foot: [-12, 12, -12, 9] } });
  root.NmcCreekHouses = { revision: '1.0 (the north creek redrawn, 10-02)', kinds: ['creekHouse'], lib: { grid, roofOf, bays, pane, slide, gdoor, flight, railX, deckX, porchX, patioX, wallR, lounger, grill, firepit, hottub, pergola, bench, shrub, furnish } };
})(typeof globalThis !== 'undefined' ? globalThis : this);
