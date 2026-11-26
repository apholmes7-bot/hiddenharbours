/* Hidden Harbours — STATION LIGHT 2 (stationLight2.js → globalThis.StationLight2), 2026-09-27.
   The live light the light station and its buoys share. It is CoastalPass.light's law (the houses', wharf buildings' and
   trees': sky × visibility from nine occlusion maps + sun × N·L × a sun map + ground bounce, one band ladder, harmony's
   grade, rain by porosity with glints, height-heavy fog, snow held by material, a backlit rim, emitters and warm point lamps)
   with WharfRig2's water terms added for anything that stands in the sea (the tide's splash line, the damp band above it
   and the bounce dapple on shaded faces) and three things the station needs:
     · faces may be left out of the occlusion maps (occ:false: glass, lens, thin rails), so the sun comes in at a window;
     · o.indoor = [sunK, skyK] applies only to faces flagged ind (the finish inside, floors, the stair), not to a box;
     · an emitter picks its ramp (emc: warm · fire · lamp · red · green · white · yellow), so a lens flashes in its colour.
   No dither; ringless unless o.outline. Pure: no DOM, no clock.
   API: model(faces, MATS, o) · frame(mdl, C, W, H, o) · relight(fr, sky, o) · shadeAt · castShadow · contact · view ·
        lampNeed · skyOf · REF_SKY · NIGHT_SKY · UNLIT_SKY · EMR · faceBand
   C = {ct, st, se, ce, S, ox, oy}: sx = ox + xr·S, sy = oy − (yr·se + z·ce)·S, depth yr·ce − z·se (xr = x ct − y st, yr = x st + y ct). */
(function (root) {
  'use strict';
  const DEG = Math.PI / 180, TAU = Math.PI * 2, LOOP = 16, CE = Math.cos(40 * DEG), SE = Math.sin(40 * DEG);
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const nrm = (v) => { const L = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / L, v[1] / L, v[2] / L]; };
  const crs = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const toView = (w) => [w[0], -w[2] * CE + w[1] * SE, w[2] * SE + w[1] * CE];
  const dirOf = (az, el) => { const c = Math.cos(el * DEG); return [Math.sin(az * DEG) * c, -Math.cos(az * DEG) * c, Math.sin(el * DEG)]; };
  const REF_SUN = dirOf(290, 55), NIGHT_SUN = dirOf(125, 42);
  const REF_SKY = { name: 'Afternoon · reference key', time: 14, clock: '14:00', key: 'sun', sunW: REF_SUN, sunV: toView(REF_SUN), sunI: 1, sunI0: 1, skyI: 0.60, expo: 1, kc: '#fff0cf', ac: '#1d3b4a', ka: 0.16, aa: 0.30,
    rc: '#bcd6e2', ra: 0.14, wash: '#fff4dd', wa: 0.03, amb: 0.62, fogC: '#c3cdce', skyC: '#b0c9d8', fog: 0, wet: 0, snow: 0, cloud: 0, wind: { w: 0.2, gust: 0.4, dir: 1 } };
  const NIGHT_SKY = { name: 'Night · moon', time: 22.75, clock: '22:45', key: 'moon', sunW: NIGHT_SUN, sunV: toView(NIGHT_SUN), sunI: 0.22, sunI0: 0.22, skyI: 0.30, expo: 1, kc: '#a8bce0', ac: '#101a33', ka: 0.22, aa: 0.62,
    rc: '#7f98c8', ra: 0.30, wash: '#16223f', wa: 0.46, amb: 0.36, fogC: '#4a5670', skyC: '#27365a', fog: 0, wet: 0, snow: 0, cloud: 0, wind: { w: 0.1, gust: 0.3, dir: 1 } };
  const UNLIT_SKY = { name: 'unlit', sunW: [0, 0, 1], sunV: toView([0, 0, 1]), sunI: 0, skyI: 1.25, expo: 1, kc: '#ffffff', ac: '#000000', ka: 0, aa: 0, wash: '#ffffff', wa: 0, amb: 1, fogC: '#ffffff', fog: 0, wet: 0, snow: 0, grade: false };
  const KEYLINE = '#101d21', SNOWR = ['#5c7180', '#7d93a0', '#a8bcc4', '#cfdde1', '#eef4f4'].map(h2r);
  const EMR = {
    warm: ['#5c3a1a', '#93602b', '#c88b3d', '#e6b35a', '#f6d68b', '#fff0c2'], fire: ['#5a1e0c', '#8f3312', '#c85a1c', '#ec8a2c', '#f9b552', '#ffe08e'],
    lamp: ['#7a5a22', '#b58a3a', '#e2bb62', '#f8dc92', '#fff1c8', '#fffbee'], red: ['#4a0f0c', '#7a1a14', '#b02a1e', '#e0483a', '#ff7a66', '#ffc2b4'],
    green: ['#0c3a22', '#146034', '#1f8a4a', '#3cbf6a', '#7ae59a', '#c6f7d2'], white: ['#5a5e60', '#8e9496', '#c2c8c8', '#e6ecea', '#f6faf8', '#ffffff'],
    yellow: ['#5a4410', '#8f6c16', '#c89a22', '#ecc23e', '#f9dc78', '#fff2c0'] };
  for (const k in EMR) EMR[k] = EMR[k].map(h2r);
  const faceBand = (E) => E >= 0.72 ? 1 : E >= 0.24 ? 0 : E >= 0.095 ? -1 : E >= 0.04 ? -2 : -3;
  const hsh = (a, b, c, s) => { let h = Math.imul(a | 0, 374761393) + Math.imul(b | 0, 668265263) + Math.imul(c | 0, 1440670441) + Math.imul(s | 0, 1274126177) | 0; h ^= h >>> 13; h = Math.imul(h, 1103515245) | 0; h ^= h >>> 16; return (h >>> 0) / 4294967296; };
  function vn3(x, y, z, s) { const xi = Math.floor(x), yi = Math.floor(y), zi = Math.floor(z), fx = x - xi, fy = y - yi, fz = z - zi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy), w = fz * fz * (3 - 2 * fz);
    const L = (a, b, t) => a + (b - a) * t, c = (i, j, k) => hsh(xi + i, yi + j, zi + k, s);
    return L(L(L(c(0, 0, 0), c(1, 0, 0), u), L(c(0, 1, 0), c(1, 1, 0), u), v), L(L(c(0, 0, 1), c(1, 0, 1), u), L(c(0, 1, 1), c(1, 1, 1), u), v), w); }

  /* a model: faces {v, mat, b, db, uv, tex, tag, em, emc, occ, ao, ind, fire} and MATS {key: {ramp, por, gloss, hold, base}} */
  function model(faces, MATS, o) {
    o = o || {}; const MI = {}, MA = [];
    Object.keys(MATS).forEach((k, i) => { MI[k] = i; const M = MATS[k], r = M.ramp.map(h2r), n = r.length;
      MA.push({ key: k, ramp: r, n, base: M.base != null ? M.base : n >= 6 ? 3 : n >= 5 ? 2 : Math.max(0, (n - 1) >> 1), por: M.por != null ? M.por : 0.2, gloss: M.gloss != null ? M.gloss : 0.1, hold: M.hold != null ? M.hold : 0.7 }); });
    const TAGS = [''], TI = { '': 0 }, EM = [''], EI = { '': 0 };
    const fs = faces.map(f => { let ti = 0, ei = 0; if (f.tag) { if (TI[f.tag] == null) { TI[f.tag] = TAGS.length; TAGS.push(f.tag); } ti = TI[f.tag]; }
      if (f.em) { if (EI[f.em] == null) { EI[f.em] = EM.length; EM.push(f.em); } ei = EI[f.em]; }
      return { v: f.v, mi: MI[f.mat] != null ? MI[f.mat] : 0, b: f.b || 0, db: f.db || 0, uv: f.uv || null, tex: f.tex || null, ti, ei, emc: f.emc || null, fire: !!f.fire, ind: !!f.ind, occ: f.occ !== false, ao: f.ao != null ? !!f.ao : f.occ !== false, src: f }; });
    return { faces: fs, occF: fs.filter(f => f.occ), aoF: fs.filter(f => f.ao), MA, MI, TAGS, TI, EM, EI, ao: null, sun: new Map(), aoRes: o.aoRes || 12, sunRes: o.sunRes || 24 };
  }
  function occMap(faces, Ld, res) {
    const w = nrm(Ld), hint = Math.abs(w[2]) < 0.95 ? [0, 0, 1] : [1, 0, 0], u = nrm(crs(hint, w)), v = crs(w, u); let u0 = 1e9, u1 = -1e9, v0 = 1e9, v1 = -1e9; const tri = [];
    for (const f of faces) { const pv = f.v.map(q => [q[0] * u[0] + q[1] * u[1] + q[2] * u[2], q[0] * v[0] + q[1] * v[1] + q[2] * v[2], q[0] * w[0] + q[1] * w[1] + q[2] * w[2]]);
      for (const q of pv) { if (q[0] < u0) u0 = q[0]; if (q[0] > u1) u1 = q[0]; if (q[1] < v0) v0 = q[1]; if (q[1] > v1) v1 = q[1]; } tri.push(pv); }
    if (!tri.length) { u0 = v0 = 0; u1 = v1 = 1; } u0 -= 2 / res; v0 -= 2 / res;
    const gw = Math.max(1, Math.ceil((u1 - u0) * res) + 4), gh = Math.max(1, Math.ceil((v1 - v0) * res) + 4), T = new Float32Array(gw * gh).fill(-1e9);
    for (const pv of tri) for (let t = 1; t + 1 < pv.length; t++) {
      const A = pv[0], B = pv[t], C = pv[t + 1], ax = (A[0] - u0) * res, ay = (A[1] - v0) * res, bx = (B[0] - u0) * res, by = (B[1] - v0) * res, cx = (C[0] - u0) * res, cy = (C[1] - v0) * res;
      const area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay), sg = area < 0 ? -1 : 1, lab = Math.hypot(bx - ax, by - ay) || 1, lbc = Math.hypot(cx - bx, cy - by) || 1, lca = Math.hypot(ax - cx, ay - cy) || 1;
      const tmin = Math.min(A[2], B[2], C[2]), tmax = Math.max(A[2], B[2], C[2]), deg = Math.abs(area) < 1e-6;
      const X0 = Math.max(0, Math.floor(Math.min(ax, bx, cx) - 1)), X1 = Math.min(gw - 1, Math.ceil(Math.max(ax, bx, cx) + 1)), Y0 = Math.max(0, Math.floor(Math.min(ay, by, cy) - 1)), Y1 = Math.min(gh - 1, Math.ceil(Math.max(ay, by, cy) + 1));
      for (let y = Y0; y <= Y1; y++) for (let x = X0; x <= X1; x++) { const px = x + 0.5, py = y + 0.5;
        const e0 = ((bx - ax) * (py - ay) - (by - ay) * (px - ax)) * sg, e1 = ((cx - bx) * (py - by) - (cy - by) * (px - bx)) * sg, e2 = ((ax - cx) * (py - cy) - (ay - cy) * (px - cx)) * sg;
        if (e0 / lab < -0.6 || e1 / lbc < -0.6 || e2 / lca < -0.6) continue;
        let tv; if (deg) tv = tmax; else { const wa = clamp(e1 / Math.abs(area), 0, 1), wb = clamp(e2 / Math.abs(area), 0, 1), wc = clamp(1 - wa - wb, 0, 1); tv = clamp(wa * A[2] + wb * B[2] + wc * C[2], tmin, tmax); }
        const i = y * gw + x; if (tv > T[i]) T[i] = tv; } }
    return { u, v, w, u0, v0, res, gw, gh, T };
  }
  function litIn(M, x, y, z, bias) { const pu = x * M.u[0] + y * M.u[1] + z * M.u[2], pv = x * M.v[0] + y * M.v[1] + z * M.v[2], iu = Math.floor((pu - M.u0) * M.res), iv = Math.floor((pv - M.v0) * M.res);
    if (iu < 0 || iv < 0 || iu >= M.gw || iv >= M.gh) return 1; return (x * M.w[0] + y * M.w[1] + z * M.w[2]) >= M.T[iv * M.gw + iu] - bias ? 1 : 0; }
  const SKYD = [[0, 0, 1]]; for (let k = 0; k < 8; k++) { const a = k * Math.PI / 4 + 0.2, c = Math.cos(32 * DEG); SKYD.push([Math.cos(a) * c, Math.sin(a) * c, Math.sin(32 * DEG)]); }
  const aoMaps = (mdl) => mdl.ao || (mdl.ao = SKYD.map(d => occMap(mdl.aoF, d, mdl.aoRes)));
  const toLocal = (C, w) => [w[0] * C.ct - w[1] * C.st, -w[0] * C.st - w[1] * C.ct, w[2]];
  function unproj(sx, sy, z, C) { const xr = (sx - C.ox) / C.S, yr = (-(sy - C.oy) / C.S - z * C.ce) / C.se; return [xr * C.ct + yr * C.st, -xr * C.st + yr * C.ct, z]; }
  function frame(mdl, C, W, H, o) {
    o = o || {}; const N = W * H, gb = { a: new Uint8Array(N), fid: new Int32Array(N).fill(-1), mi: new Uint16Array(N), sb: new Int8Array(N), nx: new Float32Array(N), ny: new Float32Array(N), nz: new Float32Array(N),
      X: new Float32Array(N), Y: new Float32Array(N), Z: new Float32Array(N), d: new Float32Array(N).fill(1e9), tg: new Uint16Array(N), em: new Uint16Array(N), ind: new Uint8Array(N) };
    const V = [0, -C.ce, C.se], only = o.only || null;
    for (let fi = 0; fi < mdl.faces.length; fi++) { const f = mdl.faces[fi]; if (only && !only(f)) continue; const nv = f.v.length; if (nv < 3) continue;
      const P = f.v.map(p => { const xr = p[0] * C.ct - p[1] * C.st, yr = p[0] * C.st + p[1] * C.ct; return [C.ox + xr * C.S, C.oy - (yr * C.se + p[2] * C.ce) * C.S, yr * C.ce - p[2] * C.se, xr, yr, p[2]]; });
      let n0 = 0, n1 = 0, n2 = 0; for (let k = 0; k < nv; k++) { const a = P[k], b = P[(k + 1) % nv]; n0 += (a[4] - b[4]) * (a[5] + b[5]); n1 += (a[5] - b[5]) * (a[3] + b[3]); n2 += (a[3] - b[3]) * (a[4] + b[4]); }
      let n = nrm([n0, n1, n2]); if (n[0] * V[0] + n[1] * V[1] + n[2] * V[2] < 0) n = [-n[0], -n[1], -n[2]];
      const nl = [n[0] * C.ct + n[1] * C.st, -n[0] * C.st + n[1] * C.ct, n[2]], bias = clamp(Math.round(f.b), -2, 1), tex = f.tex, uv = f.uv, ind = f.ind ? 1 : 0;
      for (let t = 1; t + 1 < nv; t++) { const a = P[0], b = P[t], c = P[t + 1], area = (b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1]); if (Math.abs(area) < 1e-7) continue;
        const X0 = Math.max(0, Math.floor(Math.min(a[0], b[0], c[0]))), X1 = Math.min(W - 1, Math.ceil(Math.max(a[0], b[0], c[0]))), Y0 = Math.max(0, Math.floor(Math.min(a[1], b[1], c[1]))), Y1 = Math.min(H - 1, Math.ceil(Math.max(a[1], b[1], c[1])));
        const A = f.v[0], B = f.v[t], Cc = f.v[t + 1], ua = uv ? uv[0] : null, ub = uv ? uv[t] : null, uc = uv ? uv[t + 1] : null;
        for (let y = Y0; y <= Y1; y++) for (let x = X0; x <= X1; x++) { const px = x + 0.5, py = y + 0.5, w0 = ((b[0] - px) * (c[1] - py) - (c[0] - px) * (b[1] - py)) / area, w1 = ((c[0] - px) * (a[1] - py) - (a[0] - px) * (c[1] - py)) / area, w2 = 1 - w0 - w1;
          if (w0 < -0.001 || w1 < -0.001 || w2 < -0.001) continue; const d = w0 * a[2] + w1 * b[2] + w2 * c[2], i = y * W + x; if (d - f.db >= gb.d[i]) continue;
          let sbv = bias; if (tex && uv) sbv += Math.round(tex(w0 * ua[0] + w1 * ub[0] + w2 * uc[0], w0 * ua[1] + w1 * ub[1] + w2 * uc[1]));
          gb.d[i] = d - f.db; gb.a[i] = 1; gb.fid[i] = fi; gb.mi[i] = f.mi; gb.sb[i] = sbv < -3 ? -3 : sbv > 1 ? 1 : sbv; gb.nx[i] = nl[0]; gb.ny[i] = nl[1]; gb.nz[i] = nl[2];
          gb.X[i] = w0 * A[0] + w1 * B[0] + w2 * Cc[0]; gb.Y[i] = w0 * A[1] + w1 * B[1] + w2 * Cc[1]; gb.Z[i] = w0 * A[2] + w1 * B[2] + w2 * Cc[2]; gb.tg[i] = f.ti; gb.em[i] = f.ei; gb.ind[i] = ind; } } }
    if (!o.keepSpecks) for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const i = y * W + x; if (!gb.a[i]) continue;
      if (!((x > 0 && gb.a[i - 1]) || (x < W - 1 && gb.a[i + 1]) || (y > 0 && gb.a[i - W]) || (y < H - 1 && gb.a[i + W]))) { gb.a[i] = 0; gb.fid[i] = -1; gb.d[i] = 1e9; } }
    const fr = { mdl, C, W, H, gb, MA: mdl.MA }; if (!o.noSky) skyVis(fr); return fr;
  }
  function skyVis(fr) { const gb = fr.gb, N = fr.W * fr.H, sv = new Uint8Array(N), AO = aoMaps(fr.mdl);
    for (let i = 0; i < N; i++) { if (!gb.a[i]) continue; const nx = gb.nx[i], ny = gb.ny[i], nz = gb.nz[i], X = gb.X[i] + nx * .03, Y = gb.Y[i] + ny * .03, Z = gb.Z[i] + nz * .03; let acc = 0, wt = 0;
      for (let k = 0; k < SKYD.length; k++) { const d = SKYD[k], wk = nx * d[0] + ny * d[1] + nz * d[2]; if (wk <= 0) continue; wt += wk; const tn = Math.min(10, Math.sqrt(Math.max(0, 1 - wk * wk)) / Math.max(wk, .05));
        acc += wk * litIn(AO[k], X + nx * .05 * tn, Y + ny * .05 * tn, Z + nz * .05 * tn, .06 + .07 * tn); }
      sv[i] = wt > 1e-3 ? Math.round(acc / wt * 255) : 0; }
    fr.sv = sv; }
  function sunMap(mdl, Ld) { const key = Ld.map(q => Math.round(q * 300)).join(','); let S = mdl.sun.get(key);
    if (!S) { S = occMap(mdl.occF, Ld, mdl.sunRes); if (mdl.sun.size > 12) mdl.sun.delete(mdl.sun.keys().next().value); mdl.sun.set(key, S); } return S; }
  const sunBias = (nl) => .03 + .045 * Math.min(12, Math.sqrt(Math.max(0, 1 - nl * nl)) / Math.max(nl, .05)), sunOff = (nl) => .03 + .09 * (1 - nl);

  /* o: emit(name) → 0–1 · lights [{p, c, I, r}] · indoor [sunK, skyK] for ind faces · tide, wind, frame (water terms) · ground (fog datum) · outline */
  function relight(fr, sky, o) {
    o = o || {}; sky = sky || REF_SKY; const gb = fr.gb, W = fr.W, H = fr.H, N = W * H, MA = fr.MA, out = new Uint8ClampedArray(N * 4), unlit = sky.grade === false && !sky.sunI;
    const Lw = sky.sunW || REF_SKY.sunW, Lv = sky.sunV || toView(Lw), sunI = sky.sunI == null ? 1 : sky.sunI, skyI = sky.skyI == null ? .6 : sky.skyI, expo = sky.expo || 1;
    const snow = sky.snow || 0, wet = sky.wet || 0, fog = sky.fog || 0, grade = sky.grade !== false && o.grade !== false, sunOn = sunI > .01 && Lw[2] > .02;
    const Ll = nrm(toLocal(fr.C, Lw)), SM = sunOn ? sunMap(fr.mdl, Ll) : null, gB = .13 * (sunI * Math.max(0, Lw[2]) + .35 * skyI) * (1 + .8 * snow), snowK = Math.round(snow * 254), cover = snow > .02;
    const tide = o.tide != null && !unlit ? +o.tide : null, wv = o.wind && o.wind.w != null ? o.wind.w : sky.wind && sky.wind.w != null ? sky.wind.w : .2;
    const splash = tide != null ? tide + .06 + .12 * wv : -1e9, damp = tide != null ? tide + .40 : -1e9, fz = tide != null ? tide : (o.ground || 0), fi = o.frame | 0, ph = TAU * fi / LOOP;
    let lx = Lv[0], ly = Lv[1]; const ll = Math.hypot(lx, ly); if (ll > 1e-3) { lx /= ll; ly /= ll; }
    const backlit = sunOn && Lv[2] < -.12 && sunI > .12, kc = h2r(sky.kc || '#ffffff'), ac = h2r(sky.ac || '#000000'), wc = h2r(sky.wash || '#ffffff'), fc = h2r(sky.fogC || '#c3cdce');
    const EM = fr.mdl.EM, elv = new Float32Array(EM.length); if (o.emit) for (let k = 1; k < EM.length; k++) elv[k] = clamp(+o.emit(EM[k]) || 0, 0, 1);
    const lamps = (o.lights || []).filter(l => l && l.I > .005), lcol = lamps.map(l => h2r(l.c || '#ffc774')), IN = o.indoor || null, cache = new Map(), P0 = MA.length;
    const colour = (pid, bi, lit, fq, spc, wk, wq, li) => { const key = (((((((pid * 6 + bi) * 2 + lit) * 5 + fq) * 3 + spc) * 3 + wk) * 4 + wq) * 16) + (li & 15); let c = cache.get(key); if (c) return c;
      const R = pid < P0 ? MA[pid].ramp : SNOWR, n = R.length, j = clamp(bi, 0, n - 1), r = R[j].slice(), top = R[n - 1];
      if (spc === 1) for (let k = 0; k < 3; k++) r[k] = top[k] + (kc[k] - top[k]) * .55; else if (spc === 2) for (let k = 0; k < 3; k++) r[k] = top[k] + (255 - top[k]) * .6;
      if (grade) { const u = j / Math.max(1, n - 1), ta = (sky.aa || 0) * (1 - u) * (1 - (sky.amb || 0) * .35), tk = (sky.ka || 0) * u * (lit ? 1 : .3), wd = wk === 2 ? .30 : wk === 1 ? .12 : 0;
        for (let k = 0; k < 3; k++) { r[k] += (ac[k] - r[k]) * ta * (1 - wq * .3); r[k] += (kc[k] - r[k]) * tk; r[k] *= 1 - wd; if (sky.wa) r[k] += (wc[k] - r[k]) * sky.wa * (1 - wq * .35); if (fq) r[k] += (fc[k] - r[k]) * fq * .17; } }
      if (wq && lcol.length) { const L = lcol[li % lcol.length], t = [0, .2, .36, .5][wq]; for (let k = 0; k < 3; k++) r[k] += (L[k] - r[k]) * t; }
      c = [clamp(Math.round(r[0]), 0, 255), clamp(Math.round(r[1]), 0, 255), clamp(Math.round(r[2]), 0, 255)]; cache.set(key, c); return c; };
    const E = new Float32Array(N), EL = new Float32Array(N), LI = new Uint8Array(N), LT = new Uint8Array(N);
    for (let i = 0; i < N; i++) { if (!gb.a[i]) continue; const nx = gb.nx[i], ny = gb.ny[i], nz = gb.nz[i], X = gb.X[i], Y = gb.Y[i], Z = gb.Z[i], sv = fr.sv[i] / 255, nl = nx * Ll[0] + ny * Ll[1] + nz * Ll[2];
      const so = nl > 0 ? sunOff(nl) : 0, vis = SM && nl > 0 ? litIn(SM, X + nx * so, Y + ny * so, Z + nz * so, sunBias(nl)) : 0, sun = sunI * (nl > 0 ? Math.pow(nl, 1.25) * Math.min(1, nl / .14) : 0) * vis;
      const inn = IN && gb.ind[i], sK = inn ? IN[0] : 1, kK = inn ? IN[1] : 1;
      let cz = 0; if (tide != null && sunOn && !vis && Z > tide && Z < tide + 1.6 && nz < .5 && sunI > .15) { const k = 1 - (Z - tide) / 1.6, pat = vn3(X * 3.1 + Math.cos(ph) * .9, Y * 3.1 + Math.sin(ph) * .9, Z * 1.3, 53); cz = (.04 + (pat > .62 ? .16 : 0)) * sunI * k * Math.max(0, Lw[2]); }
      E[i] = (.36 * skyI * kK * (.30 + .70 * sv) * (.45 + .55 * Math.max(0, nz)) + sun * sK + gB * (.5 - .5 * nz) * (.5 + .5 * sv) + cz) * expo; LT[i] = sun * sK > .22 ? 1 : 0;
      if (lamps.length) { let s = 0, best = 0, bi = 0; for (let k = 0; k < lamps.length; k++) { const l = lamps[k], dx = l.p[0] - X, dy = l.p[1] - Y, dz = l.p[2] - Z, d2 = dx * dx + dy * dy + dz * dz, r = l.r || 3; if (d2 > r * r) continue;
          const d = Math.sqrt(d2) || 1e-3, ndl = (nx * dx + ny * dy + nz * dz) / d; if (ndl <= .02) continue; const q = 1 - d / r, v = l.I * (.35 + .65 * ndl) * q * q; s += v; if (v > best) { best = v; bi = k; } }
        EL[i] = s; LI[i] = bi; } }
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const i = y * W + x; if (!gb.a[i]) continue;
      const m = MA[gb.mi[i]], nz = gb.nz[i], Z = gb.Z[i], ek = gb.em[i], el = ek ? elv[ek] : 0, o4 = i * 4, inn = gb.ind[i]; let c;
      if (el > .04) { const f = fr.mdl.faces[gb.fid[i]], R = (f.emc && EMR[f.emc]) || (f.fire ? EMR.fire : EMR.warm), j = clamp(1 + Math.round(el * 2.6) + gb.sb[i] + (gb.sb[i] > 0 ? 1 : 0), 0, 5); c = R[j];
        if (fog > .01) { const k2 = fog * .25; c = [c[0] + (fc[0] - c[0]) * k2, c[1] + (fc[1] - c[1]) * k2, c[2] + (fc[2] - c[2]) * k2]; }
        out[o4] = c[0]; out[o4 + 1] = c[1]; out[o4 + 2] = c[2]; out[o4 + 3] = 255; continue; }
      const tot = E[i] + EL[i], db = unlit ? 0 : faceBand(tot), lit = LT[i]; let pid = gb.mi[i], bi = m.base + gb.sb[i] + db, spc = 0, wk = 0, wq = 0;
      const dd = gb.d[i]; if (!unlit && ((x > 0 && gb.a[i - 1] && dd - gb.d[i - 1] > .16) || (x < W - 1 && gb.a[i + 1] && dd - gb.d[i + 1] > .16) || (y > 0 && gb.a[i - W] && dd - gb.d[i - W] > .16) || (y < H - 1 && gb.a[i + W] && dd - gb.d[i + W] > .16))) bi -= 1;
      if (!unlit) { const wt = Z < splash ? .9 : Z < damp ? .45 : 0, ww = Math.max(wt, inn ? 0 : wet), wp = ww * Math.max(m.por, wt ? .25 : 0); wk = wp > .21 ? 2 : wp > .06 ? 1 : 0;
        if (ww > .3 && lit && nz > .5 && bi >= m.base + 1 && hsh(x, y, fi, 17) < ww * m.gloss * .35) spc = 2; }
      if (cover && !inn && m.hold > 0 && nz > .3 && fr.sv[i] > 46 && Z > splash) { const e = .60 * vn3(gb.X[i] * 1.6, gb.Y[i] * 1.6, Z * 1.6, 17) + .34 * vn3(gb.X[i] * 5.5, gb.Y[i] * 5.5, Z * 5.5, 29) + (1 - nz) * 1.4 + (1 - fr.sv[i] / 255) * .7 + (1 - m.hold) * .6;
        const k2 = (e + .12) / 1.3; if (k2 < 1 && Math.ceil(k2 * 254) <= snowK) { pid = P0; bi = 3 + db + (nz > .9 ? 1 : 0) - (fr.sv[i] < 115 ? 1 : 0); wk = 0; spc = 0; } }
      if (backlit && pid !== P0 && nz > .25) { const edge = (x === 0 || !gb.a[i - 1]) || (x === W - 1 || !gb.a[i + 1]) || (y === 0 || !gb.a[i - W]);
        if (edge) { const sxn = gb.nx[i] * fr.C.ct - gb.ny[i] * fr.C.st; if (sxn * lx < -.05 || y === 0 || !gb.a[i - W]) { spc = 1; bi = m.n - 1; } } }
      if (EL[i] > .02) { const wf = EL[i] / (tot + 1e-6); wq = wf > .62 ? 3 : wf > .36 ? 2 : wf > .14 ? 1 : 0; }
      let fq = 0; if (fog > .01) fq = Math.round(clamp(fog * (.62 + .38 * (1 - clamp((Z - fz) / 9, 0, 1))) * (inn ? .6 : 1), 0, 1) * 4);
      c = colour(pid, Math.max(0, bi), lit, fq, spc, wk, wq, LI[i]); out[o4] = c[0]; out[o4 + 1] = c[1]; out[o4 + 2] = c[2]; out[o4 + 3] = 255; }
    if (o.outline) { const k = h2r(KEYLINE), a = gb.a; for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const i = y * W + x; if (a[i]) continue;
      if ((x > 0 && a[i - 1]) || (x < W - 1 && a[i + 1]) || (y > 0 && a[i - W]) || (y < H - 1 && a[i + W])) { out[i * 4] = k[0]; out[i * 4 + 1] = k[1]; out[i * 4 + 2] = k[2]; out[i * 4 + 3] = 255; } } }
    return out;
  }
  /* 3 full sun shadow (2 under a thin sun) · 2 partial · 1 canopy (sky straight down blocked; stays under overcast) · 0 open */
  function shadeAt(fr, sky, x, y, z) { sky = sky || REF_SKY; const sunI = sky.sunI == null ? 1 : sky.sunI, Lw = sky.sunW || REF_SKY.sunW; let L = 0;
    if (sunI > .04 && Lw[2] > .03) { const SM = sunMap(fr.mdl, nrm(toLocal(fr.C, Lw))); let b = 0; for (const [dx, dy] of [[-.05, -.05], [.05, -.05], [-.05, .05], [.05, .05]]) b += litIn(SM, x + dx, y + dy, z + .02, .05) ? 0 : 1;
      if (b >= 3) L = sunI > .25 ? 3 : 2; else if (b >= 1) L = 2; }
    if (L < 1 && !litIn(aoMaps(fr.mdl)[0], x, y, z + .02, .1)) L = 1; return L; }
  function castShadow(fr, sky, o) { o = o || {}; sky = sky || REF_SKY; const z = o.z || 0, W = fr.W, H = fr.H, lv = new Uint8Array(W * H), C = fr.C, Lw = sky.sunW || REF_SKY.sunW;
    let bx = o.box; if (!bx) { let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9, zt = 0; for (const f of fr.mdl.faces) for (const p of f.v) { if (p[0] < x0) x0 = p[0]; if (p[0] > x1) x1 = p[0]; if (p[1] < y0) y0 = p[1]; if (p[1] > y1) y1 = p[1]; if (p[2] > zt) zt = p[2]; }
      const Ll = nrm(toLocal(C, Lw)), k = Ll[2] > .03 ? zt / Math.max(Ll[2], .14) : 0, ex = Math.abs(Ll[0]) * k + .5, ey = Math.abs(Ll[1]) * k + .5; bx = [x0 - ex, x1 + ex, y0 - ey, y1 + ey]; }
    for (let yy = 0; yy < H; yy++) for (let xx = 0; xx < W; xx++) { const p = unproj(xx + .5, yy + .5, z, C); if (p[0] < bx[0] || p[0] > bx[1] || p[1] < bx[2] || p[1] > bx[3]) continue; lv[yy * W + xx] = shadeAt(fr, sky, p[0], p[1], z); }
    return { x0: 0, y0: 0, w: W, h: H, lv, z }; }
  /* where the model pierces the water at the tide t: 2 on the line, 1 beside it (the foam lace goes there) */
  function contact(fr, t) { const gb = fr.gb, W = fr.W, H = fr.H, m = new Uint8Array(W * H);
    for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) { const i = y * W + x; if (!gb.a[i]) continue; const zi = gb.Z[i];
      if (zi >= t - .03 && zi <= t + .03) { m[i] = 2; for (const j of [i - 1, i + 1, i + W]) if (!m[j]) m[j] = 1; }
      else if (zi < t && gb.a[i - W] && gb.Z[i - W] >= t) { m[i] = 2; if (!m[i - 1]) m[i - 1] = 1; if (!m[i + 1]) m[i + 1] = 1; } }
    return m; }
  function view(fr, ch, sky, o) {
    if (!ch || ch === 'lit') return relight(fr, sky || REF_SKY, o); if (ch === 'unlit') return relight(fr, UNLIT_SKY, Object.assign({}, o, { lights: [], tide: null }));
    const gb = fr.gb, N = fr.W * fr.H, out = new Uint8ClampedArray(N * 4), Lw = (sky || REF_SKY).sunW, Ll = nrm(toLocal(fr.C, Lw)), SM = ch === 'sun' ? sunMap(fr.mdl, Ll) : null; let d0 = 1e9, d1 = -1e9, z1 = 1;
    for (let i = 0; i < N; i++) if (gb.a[i]) { if (gb.d[i] < d0) d0 = gb.d[i]; if (gb.d[i] > d1) d1 = gb.d[i]; if (gb.Z[i] > z1) z1 = gb.Z[i]; }
    for (let i = 0; i < N; i++) { if (!gb.a[i]) continue; let r = 128, g = 128, b = 128;
      if (ch === 'normal') { const C = fr.C, xr = gb.nx[i] * C.ct - gb.ny[i] * C.st, yr = gb.nx[i] * C.st + gb.ny[i] * C.ct; r = (xr * .5 + .5) * 255; g = (yr * .5 + .5) * 255; b = (gb.nz[i] * .5 + .5) * 255; }
      else if (ch === 'ao') r = g = b = fr.sv[i];
      else if (ch === 'sun') { const nl = gb.nx[i] * Ll[0] + gb.ny[i] * Ll[1] + gb.nz[i] * Ll[2], v = nl > 0 && Lw[2] > .02 && litIn(SM, gb.X[i] + gb.nx[i] * .03, gb.Y[i] + gb.ny[i] * .03, gb.Z[i] + gb.nz[i] * .03, sunBias(nl)) ? nl : 0; r = 24 + v * 231; g = 22 + v * 200; b = 30 + v * 120; }
      else if (ch === 'height') r = g = b = clamp(gb.Z[i] / z1, 0, 1) * 235 + 20;
      else if (ch === 'depth') r = g = b = 255 - (gb.d[i] - d0) / Math.max(1e-3, d1 - d0) * 235;
      else if (ch === 'tags') { const t = gb.tg[i]; if (t) { r = 60 + 180 * hsh(t, 1, 3, 5); g = 60 + 180 * hsh(t, 2, 3, 5); b = 60 + 180 * hsh(t, 3, 3, 5); } else r = g = b = 40; }
      else if (ch === 'emit') { const e = gb.em[i]; if (e) { r = 235; g = 150 * hsh(e, 4, 1, 2) + 80; b = 60; } else r = g = b = 30; }
      else if (ch === 'indoor') { if (gb.ind[i]) { r = 200; g = 170; b = 90; } else { r = 50; g = 70; b = 80; } }
      else if (ch === 'mat') { const m = fr.MA[gb.mi[i]], c = m.ramp[clamp(m.base + gb.sb[i], 0, m.n - 1)]; r = c[0]; g = c[1]; b = c[2]; }
      out[i * 4] = r; out[i * 4 + 1] = g; out[i * 4 + 2] = b; out[i * 4 + 3] = 255; }
    return out; }
  function lampNeed(sky) { sky = sky || REF_SKY; if (sky.need != null) return sky.need; const L = sky.sunW || REF_SKY.sunW, s0 = sky.key === 'moon' ? 0 : (sky.sunI0 != null ? sky.sunI0 : (sky.sunI || 0));
    const illum = s0 * Math.max(.05, L[2]) + (sky.skyI || 0) * .5 * (1 - .35 * (sky.cloud || 0)); return clamp((.52 - illum) / .34, 0, 1); }
  function skyOf(o) { o = o || {}; if (o.sky) return o.sky; const WS = root.WeatherSky;
    if (o.time != null && WS) return WS.at({ time: o.time, cloud: o.cloud, rain: o.rain, fog: o.fog, snow: o.snow, wind: o.wind, gust: o.gust });
    return o.night ? NIGHT_SKY : REF_SKY; }
  root.StationLight2 = { version: '2.0', REF_SKY, NIGHT_SKY, UNLIT_SKY, KEYLINE, SNOWR, EMR, SKYD, faceBand, hsh, vn3, model, occMap, litIn, frame, relight, shadeAt, castShadow, contact, view, lampNeed, skyOf, toLocal, unproj, toView, dirOf };
})(typeof globalThis !== 'undefined' ? globalThis : window);
