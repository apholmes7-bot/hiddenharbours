/* Hidden Harbours — TERRAIN LIGHT 6 (cliff & rock pass 6).  TerrainLight5, verbatim, plus three optional
   inputs so a floor can sit under cliffs:  o.occ (Uint8, per tile texel: 1 = the sun is blocked by terrain
   that is not in this tile's height field — a cliff, a stack), o.skyv (Float32, per tile texel: macro sky
   visibility 0–1 from the same terrain, multiplied into the sky term) and o.seaDir (−1 when the open sea
   lies SOUTH of the shore, so the swell runs north up the screen to it). With none of the three set it
   is TerrainLight5 exactly. Everything below is TerrainLight5's own header.

   TERRAIN LIGHT 5 (pixel terrain pass 9).  TerrainLight4, verbatim, with a shoreline.

   Pass 8's sea met the land on one contour: foam was every texel within 18 mm of the swash front,
   gated by a hash, so a steep ledge edge — where elevation jumps — wore a perfect dashed white ring
   and a gentle beach a ruled dashed line. This module keeps the whole of TerrainLight4 (sky ×
   visibility, sun × self-shadow, live tip and seam, one band ladder, surface classes, snow by shape,
   the tide and the swash, fog and grade) and changes only the water's edge:

     A. SLOPE.  gbuf() derives the slope of the elevation field. Foam's width is measured in water
        depth and grows with the slope, the wind and the fetch: surge foam piles up a few texels deep
        against rock, a sheet-thin lace runs up a sand beach.
     B. LACE.  Foam inside that width is a lace — a value-noise field circling through its own domain
        once per 16-frame loop, so it tiles in time — denser at the front, torn behind it, and it
        changes pattern on the backwash.
     C. STREAKS.  Behind the front, in shallow water on gentle ground, sparse foam streaks drift on the
        same loop; more of them in wind.

   The API is TerrainLight4's: gbuf · relight · dynamic · view · swashAt · sunVis. CLASS_OF is shared
   with TerrainLight4 when that is loaded, so PxKit8's registered palettes classify here too.          */
(function (root) {
  'use strict';
  const P = root.PxLang; if (!P) throw new Error('terrainLight5: pixelLanguage.js must load first');
  const { hash2 } = P;
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r) => '#' + r.map(v => clamp(Math.round(v), 0, 255).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2r(a), B = h2r(b); return r2h([0, 1, 2].map(i => A[i] + (B[i] - A[i]) * t)); };
  const ELEV = 40, CE = Math.cos(ELEV * Math.PI / 180), SE = Math.sin(ELEV * Math.PI / 180), COLD = '#1d3b4a';
  const VIEW = [0, CE, SE];                                   // toward the camera, floor space (east, south, up)
  const TAU = Math.PI * 2, LOOP = 16;

  // ---- surface classes -----------------------------------------------------------------------------
  const CL = { VEG: 0, SOIL: 1, SAND: 2, ROCK: 3, MUD: 4, WATER: 5, WEED: 6, SHELL: 7, LITTER: 8 };
  const CLASSES = [
    { key: 'veg', por: 0.10, gloss: 0.10, hold: 1.00, puddle: 0.5 },
    { key: 'soil', por: 0.30, gloss: 0.10, hold: 1.00, puddle: 1.0 },
    { key: 'sand', por: 0.36, gloss: 0.22, hold: 1.00, puddle: 0.7 },
    { key: 'rock', por: 0.20, gloss: 0.60, hold: 0.90, puddle: 1.0 },
    { key: 'mud', por: 0.12, gloss: 0.75, hold: 0.85, puddle: 1.0 },
    { key: 'water', por: 0, gloss: 1, hold: 0, puddle: 0 },
    { key: 'weed', por: 0.14, gloss: 0.85, hold: 0.60, puddle: 0.3 },
    { key: 'shell', por: 0.16, gloss: 0.70, hold: 1.00, puddle: 0.2 },
    { key: 'litter', por: 0.22, gloss: 0.00, hold: 1.00, puddle: 0.3 },
  ];
  const CLASS_COL = ['#5d8a3c', '#9a6642', '#d8c08c', '#8c8c90', '#4a3a2c', '#3f7f96', '#7a7a2a', '#e8e0d0', '#c8b070'];
  /* palette mid colour → class. PxKit8 registers every base it paints with; anything else is
     classified by its colour, which is right for the kit's own ramps and a guess for strangers. */
  const CLASS_OF = root.TerrainLight4 ? root.TerrainLight4.CLASS_OF : new Map();
  function classify(rgb) {
    const k = r2h(rgb); if (CLASS_OF.has(k)) return CLASS_OF.get(k);
    const [r, g, b] = rgb, mx = Math.max(r, g, b), mn = Math.min(r, g, b), l = (mx + mn) / 2, s = mx - mn;
    if (b > r + 8 && b >= g - 6 && l < 150) return CL.WATER;
    if (s < 22) return l < 70 ? CL.MUD : CL.ROCK;
    if (g > r && g > b) return CL.VEG;
    if (l > 150 && r > b + 30) return CL.SAND;
    if (l < 72) return CL.MUD;
    return CL.SOIL;
  }

  const SNOWR = ['#5c7180', '#7d93a0', '#a8bcc4', '#cfdde1', '#eef4f4'].map(h2r);
  const FOAMR = ['#6d8a90', '#9ab4b6', '#c6d7d6', '#e2ebe9', '#f6faf8'].map(h2r);
  const DEEP = '#234b58', SHOAL = '#3a6a6c';
  const waterRamp = (body, sky) => [mix(mix(body, '#000000', 0.42), COLD, 0.30), mix(mix(body, '#000000', 0.20), COLD, 0.14), body,
    mix(body, sky.skyC || '#b0c9d8', 0.42), mix(mix(sky.skyC || '#b0c9d8', '#ffffff', 0.30), sky.kc || '#fff0cf', 0.20)].map(h2r);

  // ---- noise -----------------------------------------------------------------------------------------
  function vn(x, y, s) {
    const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
    const a = hash2(xi, yi, s), b = hash2(xi + 1, yi, s), c = hash2(xi, yi + 1, s), d = hash2(xi + 1, yi + 1, s);
    return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
  }

  // ---- the G-buffer ------------------------------------------------------------------------------------
  function gbuf(t, o) {
    o = o || {};
    const n = t.n, m = t.m, N = n * m, rel = o.relief == null ? 1.2 : o.relief, wrap = o.wrap !== false;
    const ix = wrap ? (x, y) => (((y % m) + m) % m) * n + (((x % n) + n) % n) : (x, y) => (y < 0 ? 0 : y >= m ? m - 1 : y) * n + (x < 0 ? 0 : x >= n ? n - 1 : x);
    const H = new Float32Array(N); let hmax = -1e9;
    for (let i = 0; i < N; i++) { H[i] = t.h[i] * rel; if (H[i] > hmax) hmax = H[i]; }
    const nx = new Float32Array(N), ny = new Float32Array(N), nz = new Float32Array(N);
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x, a = -(H[ix(x + 1, y)] - H[ix(x - 1, y)]) * 0.5, b = -(H[ix(x, y + 1)] - H[ix(x, y - 1)]) * 0.5, L = Math.hypot(a, b, 1);
      nx[i] = a / L; ny[i] = b / L; nz[i] = 1 / L;
    }
    const box = (r) => {                                      // separable box mean, same edge rule as ix
      const A = new Float32Array(N), B = new Float32Array(N), k = 1 / (2 * r + 1);
      for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) { let s = 0; for (let d = -r; d <= r; d++) s += H[ix(x + d, y)]; A[y * n + x] = s * k; }
      for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) { let s = 0; for (let d = -r; d <= r; d++) s += A[ix(x, y + d)]; B[y * n + x] = s * k; }
      return B;
    };
    const m2 = box(2), m5 = box(5), m10 = box(10), proud = new Float32Array(N), hollow = new Float32Array(N), pond = new Float32Array(N), ao = new Float32Array(N);
    for (let i = 0; i < N; i++) { proud[i] = Math.max(0, H[i] - m2[i]); hollow[i] = Math.max(0, m5[i] - H[i]); pond[i] = Math.max(0, m10[i] - H[i]); }
    const DIRS = [[1, 0], [-1, 0], [0, 1], [0, -1], [0.707, 0.707], [-0.707, 0.707], [0.707, -0.707], [-0.707, -0.707]], ST = [1, 2, 3, 5];
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x, h0 = H[i]; let occ = 0;
      for (const [dx, dy] of DIRS) { let best = 0; for (const s of ST) { const d = (H[ix(Math.round(x + dx * s), Math.round(y + dy * s))] - h0) / s; if (d > best) best = d; } occ += Math.min(1.0, best); }
      ao[i] = clamp(1 - occ * 0.065, 0.45, 1);
    }
    const P0 = t.pals.length, pc = new Uint8Array(P0);
    for (let p = 0; p < P0; p++) pc[p] = o.cls && o.cls[p] != null ? o.cls[p] : classify(t.pals[p][2]);
    const cls = new Uint8Array(N), band0 = new Int8Array(N), live = o.live || t.live || null;
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x, p = t.pal[i], k = t.mark[i];
      cls[i] = pc[p]; let b = t.band[i];
      if (k && !(live && live[p])) {                          // un-bake the authored upper-left tip and down-right seam
        const seam = t.mark[ix(x + 1, y)] !== k || t.mark[ix(x, y + 1)] !== k, tip = t.mark[ix(x - 1, y)] !== k && t.mark[ix(x, y - 1)] !== k;
        if (seam && !tip) b += 1; else if (tip && !seam) b -= 1;
      }
      band0[i] = clamp(b, 0, 4);
    }
    let slope = null;
    if (o.elev) { const E = o.elev; slope = new Float32Array(N); for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) { const a = E[ix(x + 1, y)] - E[ix(x - 1, y)], b = E[ix(x, y + 1)] - E[ix(x, y - 1)]; slope[y * n + x] = Math.hypot(a, b) * 0.5; } }
    return { slope, t, n, m, N, wrap, rel, ix, H, hmax, nx, ny, nz, ao, proud, hollow, pond, cls, pc, band0, elev: o.elev || null, far: o.far || null, water: o.water || null, fetch: o.fetch || null };
  }

  // ---- shared bits of the relight ------------------------------------------------------------------------
  const SSTEP = [1, 2, 3, 4, 6, 8, 11, 15, 20, 27, 36];
  function sunVis(G, x, y, L) {
    const lh = Math.hypot(L[0], L[1]); if (L[2] <= 0.02) return 0; if (lh < 1e-3) return 1;
    const ux = L[0] / lh, uy = L[1] / lh, tn = L[2] / lh, i0 = G.ix(x, y), h0 = G.H[i0] + 0.25, room = G.hmax - h0;
    for (const s of SSTEP) { if (tn * s > room) break; if (G.H[G.ix(Math.round(x + ux * s), Math.round(y + uy * s))] > h0 + tn * s) return 0; }
    return 1;
  }
  /* the swash: how far above the still level the sheet of the last wave reaches along x, at this frame */
  function swashAt(x, fi, sky) {
    const w = sky.wind ? sky.wind.w : 0.2, A = 0.05 + 0.15 * w, ph = TAU * fi / LOOP + 2.4 * vn(x / 95, 0.5, 71) + x * 0.006;
    return { s: A * (0.5 + 0.5 * Math.sin(ph)), A, back: Math.cos(ph) < 0 };
  }
  /* snow exposure: low = snows first. Hollows and the flat take it, proud tips and slopes shed it,
     canopy shade and wet or thin classes hold less. One smooth field plus a little grain, so a patch
     has an edge and not a spray. */
  function snowE(G, X, Y, i, lvl) {
    const f = 0.60 * vn(X / 9, Y / 7, 17) + 0.34 * vn(X / 3, Y / 3, 29) + 0.06 * hash2(X, Y, 31);
    return f + Math.min(0.5, G.proud[i] * 0.35) + (1 - G.nz[i]) * 1.4 - Math.min(0.3, G.hollow[i] * 0.3) + (lvl >= 1 ? 0.22 : 0) + (1 - CLASSES[G.cls[i]].hold) * 0.5;
  }
  /* a puddle is a hollow that is broad enough to hold water: the texel and its four neighbours all
     lie low (a joint line one texel wide is not a puddle), and the class decides how readily */
  function isPud(G, X, Y, i, c, wetT) {
    const cp = CLASSES[c].puddle; if (!cp) return false;
    const h = G.pond[i], fill = (wetT - 0.25) / 0.75;
    if (h * cp <= (1 - fill) * 1.1 + 0.18) return false;
    const lo = h * 0.55;
    return G.pond[G.ix(X + 1, Y)] > lo && G.pond[G.ix(X - 1, Y)] > lo && G.pond[G.ix(X, Y + 1)] > lo && G.pond[G.ix(X, Y - 1)] > lo;
  }
  function bandOff(E, grade) {
    if (!grade) return 0;
    const r = E / 0.995;
    return r >= 1.25 ? 1 : r >= 0.30 ? 0 : r >= 0.14 ? -1 : r >= 0.06 ? -2 : -3;
  }
  const UNLIT = { name: 'unlit', sunF: [0, 0, 1], sunI: 0, skyI: 1.25, expo: 1, kc: '#ffffff', ac: '#000000', ka: 0, aa: 0, wash: '#ffffff', wa: 0, amb: 1, fogC: '#ffffff', fog: 0, wet: 0, snow: 0, grade: false, skyC: '#b0c9d8' };

  // ---- relight ---------------------------------------------------------------------------------------------
  function relight(G, sky, o) {
    o = o || {};
    const t = G.t, X0 = o.x0 | 0, Y0 = o.y0 | 0, W = o.w || G.n, Hh = o.h || G.m, out = o.out || new Uint8ClampedArray(W * Hh * 4);
    const grade = sky.grade !== false, L = sky.sunF || sky.sunW || [0, 0, 1], sunI = sky.sunI || 0, skyI = sky.skyI == null ? 0.6 : sky.skyI, expo = sky.expo || 1;
    const snow = sky.snow || 0, wet0 = sky.wet || 0, rain = sky.rain || 0, fog = sky.fog || 0, fi = ((o.frame | 0) % LOOP + LOOP) % LOOP, wind = sky.wind ? sky.wind.w : 0.2;
    const occ = o.occ || null, skyv = o.skyv || null, sdir = o.seaDir || 1, lv = o.lv || null, tide = o.tide, tideHigh = o.tideHigh == null ? (tide == null ? null : tide + 0.35) : o.tideHigh, elev = G.elev;
    const kS = 0.36, kD = 1.0, P0 = t.pals.length, sunOn = sunI > 0.01 && L[2] > 0.02;
    const EX = [SNOWR, FOAMR, waterRamp(DEEP, sky), waterRamp(SHOAL, sky)], PS = P0, PF = P0 + 1, PW = P0 + 2, PWS = P0 + 3;
    const kc = h2r(sky.kc || '#ffffff'), ac = h2r(sky.ac || '#000000'), wc = h2r(sky.wash || '#ffffff'), fc = h2r(sky.fogC || '#c3cdce'), sc = h2r(sky.skyC || '#b0c9d8');
    const cache = new Map();
    const colour = (pid, bi, lit, fq, spc, wk) => {
      const key = ((((pid * 5 + bi) * 2 + lit) * 5 + fq) * 4 + spc) * 3 + wk;
      let c = cache.get(key); if (c) return c;
      const R = pid < P0 ? t.pals[pid] : EX[pid - P0], r = R[bi].slice();
      if (spc === 1) for (let k = 0; k < 3; k++) r[k] = R[4][k] + (kc[k] - R[4][k]) * 0.55;
      else if (spc === 2) for (let k = 0; k < 3; k++) r[k] = R[4][k] + (255 - R[4][k]) * 0.6;
      else if (spc === 3) for (let k = 0; k < 3; k++) r[k] += (sc[k] - r[k]) * 0.30;
      if (grade) {
        const u = bi / 4, ta = (sky.aa || 0) * (1 - u) * (1 - (sky.amb || 0) * 0.35), tk = (sky.ka || 0) * u * (lit ? 1 : 0.3), wd = wk === 2 ? 0.30 : wk === 1 ? 0.12 : 0;
        for (let k = 0; k < 3; k++) {
          r[k] += (ac[k] - r[k]) * ta; r[k] += (kc[k] - r[k]) * tk; r[k] *= 1 - wd;
          if (sky.wa) r[k] += (wc[k] - r[k]) * sky.wa;
          if (fq) r[k] += (fc[k] - r[k]) * fq * 0.17;
        }
      }
      c = [clamp(Math.round(r[0]), 0, 255), clamp(Math.round(r[1]), 0, 255), clamp(Math.round(r[2]), 0, 255)];
      cache.set(key, c); return c;
    };
    const mixC = (a, b, f) => [Math.round(a[0] + (b[0] - a[0]) * f), Math.round(a[1] + (b[1] - a[1]) * f), Math.round(a[2] + (b[2] - a[2]) * f)];
    // the sun's direction on the floor, for tips and seams
    let lx = L[0], ly = L[1]; const ll = Math.hypot(lx, ly);
    if (ll < 0.18) { lx = -0.6; ly = -0.8; } else { lx /= ll; ly /= ll; }
    const ax = lx > 0.38 ? -1 : lx < -0.38 ? 1 : 0, ay = ly > 0.38 ? -1 : ly < -0.38 ? 1 : 0;
    const only = o.only || null, NI = only ? only.length : W * Hh;
    // tips: per mark, the texel furthest toward the sun (inside the region)
    const tipV = new Map();
    if (!only) for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) {
      const i = G.ix(X0 + x, Y0 + y), k = t.mark[i]; if (!k) continue;
      const v = (X0 + x) * lx + (Y0 + y) * ly, q = tipV.get(k); if (!q || v > q[0]) tipV.set(k, [v, i]);
    }
    const tipOf = o._tips || tipV; o._tips = tipOf;
    const Rv = [0, 0, 0];
    for (let q = 0; q < NI; q++) {
      const li = only ? only[q] : q, x = li % W, y = (li / W) | 0, X = X0 + x, Y = Y0 + y, i = G.ix(X, Y);
      const c = G.cls[i], nX = G.nx[i], nY = G.ny[i], nZ = G.nz[i], ao = G.ao[i], lvl = lv ? lv[li] : 0;
      const far = G.far ? G.far(Y) : 0, fq = fog > 0.01 ? Math.round(clamp(fog * (0.85 + 0.3 * far), 0, 1) * 4) : 0;
      // light (A, B, D)
      const nl = nX * L[0] + nY * L[1] + nZ * L[2];
      const vis = sunOn && nl > 0 && lvl < 3 && !(occ && occ[i]) ? sunVis(G, X, Y, L) * (lvl === 2 ? 0.45 : 1) : 0;
      const sun = kD * sunI * (nl > 0 ? Math.pow(nl, 1.25) : 0) * vis;
      const E = (kS * skyI * (0.30 + 0.70 * ao) * (skyv ? skyv[i] : 1) * (0.45 + 0.55 * nZ) * (lvl >= 1 ? 0.78 : 1) + sun) * expo;
      let db = grade ? bandOff(E, true) : (ao < 0.7 ? -1 : 0);
      if (grade && lvl === 1 && sunI < 0.3) db -= 1;
      const lit = sun > 0.22 ? 1 : 0;
      // water: the sea below the tide + swash, permanent water, puddles (G)
      let wetT = wet0, isW = c === CL.WATER, depth = elev ? 1 : 0.2, foam = 0, sheen = 0, shore = 0;
      if (elev && tide != null) {
        const e = elev[i], sw = swashAt(X, fi, sky), front = tide + sw.s;
        if (e < front) {
          isW = true; depth = front - e;
          const sl = G.slope ? Math.min(1, G.slope[i] * 6) : 0, fe = G.fetch ? G.fetch[i] : 1, fw = 0.012 + (0.02 + (G.slope ? G.slope[i] : 0)) * (1.1 + 2.4 * wind) * (0.4 + 0.6 * fe), ph2 = TAU * fi / LOOP;
          if (depth < fw) { const lace = vn(X / 3.1 + 1.6 * Math.cos(ph2), Y / 1.6 + 1.6 * Math.sin(ph2), sw.back ? 68 : 61); if (lace > 0.28 + 0.5 * depth / fw) foam = 1; }
          else if (depth < 0.1 + 0.12 * wind && sl < 0.5 && fe > 0.3) { const st = vn(X / 5.5 + 2 * Math.cos(ph2), Y / 1.3 + 2 * Math.sin(ph2), 67); if (st > 0.87 - 0.1 * wind) foam = 2; }
        }
        else {
          if (e < tide + sw.A * 1.1) { shore = 0.95; if (sw.back || e < front + 0.035) sheen = 1; }
          else if (e < tideHigh) shore = 0.55 + 0.4 * clamp((tideHigh - e) / 0.4, 0, 1);
          wetT = Math.max(wetT, shore);
        }
      }
      if (!isW && wetT > 0.25 && G.pond[i] > 0.18 && isPud(G, X, Y, i, c, wetT)) { isW = true; depth = 0.05; }
      let pid, bi, spc = 0, wk = 0, rgb;
      if (isW) {
        // waves: a swell running to the shore on the loop, a chop on top, both with the wind
        const fetch = G.fetch ? G.fetch[i] : 1;
        const amp = (0.16 + 0.84 * wind) * clamp(depth / 0.8, 0.12, 1) * (0.2 + 0.8 * fetch), ph = TAU * fi / LOOP;
        const q1 = Y * 0.62 + 1.6 * Math.sin(X * 0.043 + Y * 0.017) + 3.1 * vn(X / 58, Y / 21, 13) - ph * sdir;
        const q2 = X * 0.37 + Y * 0.81 + 2.2 * vn(X / 17, Y / 9, 31) + 2 * ph;
        const cw = Math.sin(q1), dcy = Math.cos(q1) * 0.62, dcx = Math.cos(q1) * 0.07 + 0.35 * wind * Math.cos(q2) * 0.37;
        let wx = -amp * dcx, wy = -amp * (dcy + 0.35 * wind * Math.cos(q2) * 0.81), wz = 1; const wl = Math.hypot(wx, wy, wz); wx /= wl; wy /= wl; wz /= wl;
        const nv = wx * VIEW[0] + wy * VIEW[1] + wz * VIEW[2]; Rv[0] = 2 * nv * wx - VIEW[0]; Rv[1] = 2 * nv * wy - VIEW[1]; Rv[2] = 2 * nv * wz - VIEW[2];
        /* crests broken into dashes by a mask that circles through noise once per loop, so it tiles in time */
        const mk = vn(X / 7 + 2.5 * Math.cos(ph), Y / 2.5 + 2.5 * Math.sin(ph), 47), ak = clamp(amp / 0.45, 0.15, 1.4);
        const crest = (cw + 0.45 * wind * Math.sin(q2)) * ak;
        bi = crest > 0.8 && mk > 0.5 ? 3 : crest < -0.85 && mk < 0.42 ? 1 : 2;
        bi = clamp(bi + (db <= -2 ? -1 : 0), 0, 4);
        pid = depth < 0.6 ? PWS : PW;
        if (sunOn) {
          const sp = Rv[0] * L[0] + Rv[1] * L[1] + Rv[2] * L[2];
          if (sunI > 0.08 && sp > 0.955 - 0.05 * wind && hash2(X, Y, 11 + fi) < 0.55) { bi = 4; spc = 2; }
          else if (sunI > 0.08 && sp > 0.86 && crest > 0.5 && mk > 0.5 && hash2(X, Y, 17 + fi) < 0.35) bi = 4;
        }
        if (wind > 0.55 && fetch > 0.5 && crest > 0.95 && mk > 0.62 && hash2(X >> 1, Y, 23 + fi) < (wind - 0.55) * 1.1) { pid = PF; bi = 3; spc = 0; }
        if (rain > 0.04) {                                    // rain rings, one drop per 7×7 cell per loop
          const cx = Math.floor(X / 7), cy = Math.floor(Y / 7), h = hash2(cx, cy, 41);
          if (h < rain * 0.9) {
            const ox = cx * 7 + 1 + hash2(cx, cy, 42) * 5, oy = cy * 7 + 1 + hash2(cx, cy, 43) * 5, age = (fi + Math.floor(hash2(cx, cy, 44) * LOOP)) % LOOP, r = age * 0.45;
            if (r < 3.2 && Math.abs(Math.hypot((X - ox) * 0.8, Y - oy) - r) < 0.55) { bi = Math.min(4, bi + 1); spc = 0; }
          }
        }
        if (foam) { pid = PF; bi = foam === 2 ? 2 + (hash2(X, Y, 31 + fi) < 0.3 ? 1 : 0) : hash2(X, Y, 29 + fi) < 0.6 ? 3 : 4; bi = clamp(bi + (db <= -2 ? -1 : 0), 0, 4); spc = 0; }
        rgb = colour(pid, bi, lit, fq, spc, 0);
        if (depth < 0.16 && !foam) {                          // the shallows: the bottom through three steps of water
          const p0 = t.pal[i], b0 = clamp(G.band0[i] + db, 0, 4), gnd = colour(p0, b0, lit, fq, 0, 2);
          rgb = mixC(gnd, rgb, depth < 0.04 ? 0.3 : depth < 0.09 ? 0.5 : 0.7);
        }
      } else {
        pid = t.pal[i]; bi = G.band0[i] + db;
        // live tip and seam (C)
        const k = t.mark[i];
        if (k) {
          let seam = false;
          if (ax) { const j = G.ix(X + ax, Y); if (t.mark[j] !== k && G.H[i] - G.H[j] > 0.2) seam = true; }
          if (!seam && ay) { const j = G.ix(X, Y + ay); if (t.mark[j] !== k && G.H[i] - G.H[j] > 0.2) seam = true; }
          if (seam && bi >= 1) bi -= 1;
          else if (lit && G.proud[i] > 0.12) { const tp = tipOf.get(k); if (tp && tp[1] === i) bi += 1; }
        }
        // wet (E)
        const C = CLASSES[c], wp = wetT * C.por;
        wk = wp > 0.21 ? 2 : wp > 0.06 ? 1 : 0;
        if (wetT > 0.3 && C.gloss > 0.3) {
          if (lit && bi >= 3 && hash2(k || i, 3, 17) < wetT * C.gloss * 0.8) spc = 2;
          else if (nZ > 0.9 && vn(X / 7, Y / 5, 19) > 1 - wetT * C.gloss * 0.42) spc = 3;
        }
        if (sheen && !spc) spc = 3;
        // snow (F)
        if (snow > 0.02 && C.hold > 0 && shore < 0.5) {
          const e = snowE(G, X, Y, i, lvl), thr = snow * 1.3 - 0.12;
          if (e < thr) {
            pid = PS; bi = clamp(3 + db + (bi - G.band0[i] > 0 ? 1 : 0), 0, 4); wk = 0; spc = 0;
            if (lit && nZ > 0.96 && hash2(X, Y, 37) < 0.006) { bi = 4; spc = 2; }
          } else if (e < thr + 0.05) wk = Math.max(wk, 1);
        }
        rgb = colour(pid, clamp(bi, 0, 4), lit, fq, spc, wk);
      }
      const o4 = li * 4; out[o4] = rgb[0]; out[o4 + 1] = rgb[1]; out[o4 + 2] = rgb[2]; out[o4 + 3] = t.a ? t.a[i] : 255;
    }
    return out;
  }

  /* region-local indices whose pixels change with the frame: water, the swash run, puddles in rain */
  function dynamic(G, sky, o) {
    o = o || {};
    const X0 = o.x0 | 0, Y0 = o.y0 | 0, W = o.w || G.n, Hh = o.h || G.m, wet = sky.wet || 0, rain = sky.rain || 0, tide = o.tide, list = [];
    const Amax = 0.05 + 0.15 * (sky.wind ? sky.wind.w : 0.2) + 0.04;
    for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) {
      const i = G.ix(X0 + x, Y0 + y); let d = G.cls[i] === CL.WATER;
      if (!d && G.elev && tide != null && G.elev[i] < tide + Amax * 1.2) d = true;
      if (!d && rain > 0.04 && wet > 0.25 && G.pond[i] > 0.18) d = true;
      if (d) list.push(y * W + x);
    }
    return Int32Array.from(list);
  }

  // ---- channel views ---------------------------------------------------------------------------------------
  function view(G, ch, sky, o) {
    o = o || {};
    if (!ch || ch === 'lit') return relight(G, sky, o);
    if (ch === 'unlit') return relight(G, UNLIT, Object.assign({}, o, { tide: null }));
    const X0 = o.x0 | 0, Y0 = o.y0 | 0, W = o.w || G.n, Hh = o.h || G.m, out = new Uint8ClampedArray(W * Hh * 4), L = sky.sunF || [0, 0, 1];
    let h0 = 1e9, h1 = -1e9; if (ch === 'height') for (let i = 0; i < G.N; i++) { if (G.H[i] < h0) h0 = G.H[i]; if (G.H[i] > h1) h1 = G.H[i]; }
    for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) {
      const X = X0 + x, Y = Y0 + y, i = G.ix(X, Y), o4 = (y * W + x) * 4; let r = 128, g = 128, b = 128;
      if (ch === 'normal') { r = (G.nx[i] * 0.5 + 0.5) * 255; g = (-G.ny[i] * 0.5 + 0.5) * 255; b = (G.nz[i] * 0.5 + 0.5) * 255; }
      else if (ch === 'ao') r = g = b = G.ao[i] * (o.skyv ? o.skyv[i] : 1) * 255;
      else if (ch === 'height') r = g = b = (G.H[i] - h0) / Math.max(1e-3, h1 - h0) * 255;
      else if (ch === 'sun') { const lvl = o.lv ? o.lv[y * W + x] : 0, nl = G.nx[i] * L[0] + G.ny[i] * L[1] + G.nz[i] * L[2], v = (sky.sunI > 0.01 && nl > 0 && lvl < 3 && !(o.occ && o.occ[i]) ? sunVis(G, X, Y, L) * (lvl === 2 ? 0.45 : 1) : 0) * Math.max(0, nl); r = 24 + v * 231; g = 22 + v * 200; b = 30 + v * 120 + (lvl === 1 ? 40 : 0); }
      else if (ch === 'class') { const c = h2r(CLASS_COL[G.cls[i]]), s = 0.55 + 0.45 * G.ao[i]; r = c[0] * s; g = c[1] * s; b = c[2] * s; }
      else if (ch === 'wet') { const c = CLASSES[G.cls[i]]; r = c.por * 400; g = c.gloss * 220; b = Math.min(255, G.pond[i] * 300); }
      else if (ch === 'snow') { const e = snowE(G, X, Y, i, o.lv ? o.lv[y * W + x] : 0), k = clamp((e + 0.12) / 1.3, 0, 1), sn = k <= (sky.snow || 0); r = sn ? 238 : 40 + 150 * (1 - k); g = sn ? 244 : 70 + 130 * (1 - k); b = sn ? 244 : 120 + 110 * (1 - k); }
      else if (ch === 'marks') { const k = G.t.mark[i]; if (!k) { r = 26; g = 30; b = 32; } else { r = 60 + 190 * hash2(k, 1, 5); g = 60 + 190 * hash2(k, 2, 5); b = 60 + 190 * hash2(k, 3, 5); } }
      else if (ch === 'light') { if (h1 < h0) for (let j = 0; j < G.N; j++) { if (G.H[j] < h0) h0 = G.H[j]; if (G.H[j] > h1) h1 = G.H[j]; } r = G.ao[i] * 255; g = CLASSES[G.cls[i]].por / 0.36 * 255; b = (G.H[i] - h0) / Math.max(1e-3, h1 - h0) * 255; }
      else if (ch === 'detail') { const k = G.t.mark[i]; r = k & 255; g = (k >> 8) & 255; b = G.cls[i] * 28; }
      out[o4] = r; out[o4 + 1] = g; out[o4 + 2] = b; out[o4 + 3] = 255;
    }
    return out;
  }

  root.TerrainLight6 = { CL, CLASSES, CLASS_COL, CLASS_OF, classify, SNOWR, FOAMR, DEEP, SHOAL, LOOP, UNLIT, VIEW, gbuf, relight, dynamic, view, swashAt, sunVis, bandOff, vn };
})(typeof globalThis !== 'undefined' ? globalThis : window);
