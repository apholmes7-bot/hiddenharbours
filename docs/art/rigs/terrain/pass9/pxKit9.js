/* Hidden Harbours — PIXEL KIT 9 (pixel terrain pass 9).  The coast re-drawn, and four more places.

   Pass 8's coast was one transect in stripes: six bands, each the width of the screen with a gentle
   wobble, and one elliptical red ledge in the sea whose edge was a single step in elevation — so the
   tide met it on one contour and TerrainLight4 drew a perfect dashed white ring round it. What is new:

     · THE LEDGE is a domain-warped outline in two octaves, its top in benches, its edge a ramp; its
       weed is zoned by elevation (Irish moss low, rockweed mid-tide, bare rock above), so the zones
       follow the rock and move with the tide. A grey shelf skerry stands off to the east.
     · THE BEACH has cusps (horns seaward, bays between), a shingle band that pinches out and comes
       back, a runnel along the foreshore with its drainage gap, and boulders on the lower shore.
     · TALUS gathers at the ledge foot.
     · FOUR MORE SCENES on the same floor and light: a blueberry barren with granite and a bog pond,
       a salt marsh with its creeks and pans, a roadside meadow with a tumbled wall and a ditch, and an
       alder swale with a brook and a fen pool. Each returns what coast() returns — tile, zone, elev,
       fetch, far — plus water ({tide} or {level}) and a planting recipe the viewer places from.

     PxKit9.SCENES[key] = {name, note, build(o) -> C, water, recipe(C) -> {trees, adds}}
   Needs PxLang, PxKit, PxKit8 (and through it TerrainLight4).                                          */
(function (root) {
  'use strict';
  const P = root.PxLang, K = root.PxKit, K8 = root.PxKit8;
  if (!P || !K || !K8) throw new Error('pxKit9: pixelLanguage, pxKit and pxKit8 must load first');
  const { hash2, fbmP, clamp } = P, MATS = K.MATS, makeCtx = K.makeCtx, TIDE_M = K8.TIDE_M, MHW = K8.MHW;
  const vn = (x, y, s) => { const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy); const a = hash2(xi, yi, s), b = hash2(xi + 1, yi, s), c = hash2(xi, yi + 1, s), d = hash2(xi + 1, yi + 1, s); return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v; };
  const fb = (x, y, s) => 0.55 * vn(x, y, s) + 0.3 * vn(x * 2.1 + 5.3, y * 2.1 + 1.7, s + 1) + 0.15 * vn(x * 4.3 + 2.1, y * 4.3 + 7.9, s + 2);
  const sm = (a, b, x) => { const t = clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };
  const idx = (Z) => { const zi = {}; Z.forEach((k, i) => zi[k] = i); return zi; };
  const segD = (x, y, pts) => { let d = 1e9; for (let k = 0; k < pts.length - 1; k++) { const [ax, ay] = pts[k], [bx, by] = pts[k + 1], dx = bx - ax, dy = by - ay, tt = clamp(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy), 0, 1); d = Math.min(d, Math.hypot(x - ax - dx * tt, y - ay - dy * tt)); } return d; };
  const lerpP = (y, pts) => { for (let k = 0; k < pts.length - 1; k++) if (y <= pts[k + 1][0]) { const f = (y - pts[k][0]) / Math.max(1e-3, pts[k + 1][0] - pts[k][0]); return pts[k][1] + (pts[k + 1][1] - pts[k][1]) * clamp(f, 0, 1); } return pts[pts.length - 1][1]; };

  /* zones → one floor through PxKit8.floor (the kit's own region painter and edge painter) */
  function paint(W, Hh, Z, zone, sd, steps) {
    const zi = idx(Z), N = W * Hh, seen = new Uint8Array(Z.length);
    for (let j = 0; j < N; j++) seen[zone[j]] = 1;
    const defs = Z.filter((k, i) => seen[i]).map(k => ({ key: k, step: steps && steps[k] != null ? steps[k] : 1, seed: (MATS[k].seed || 1) + sd * 13, R: (x, y) => x >= 0 && y >= 0 && x < W && y < Hh && zone[y * W + x] === zi[k] }));
    return K8.floor(W, Hh, defs, sd + 5);
  }
  function finish(C, fl, elev) { const t = fl.tile; for (let i = 0; i < elev.length; i++) elev[i] += t.h[i] * 0.01; return Object.assign(C, { tile: t, reg: fl.reg, mats: fl.mats, relief: 1.2 * K.DETAIL, far: (y) => clamp(1 - y / C.h, 0, 1) }); }

  // ---- the coast, re-drawn ------------------------------------------------------------------------------------
  function coast(o) {
    o = o || {};
    const W = o.w || 520, Hh = o.h || 450, sd = o.seed || 7, N = W * Hh;
    const wob = (v, s, cells, span) => (fbmP(v / span, 0.5, cells, 1, sd + s, 3) - 0.5) * 2;
    const cusp = x => 7 * Math.pow(Math.abs(Math.sin((x / 50 + 0.12 * wob(x, 31, 4, W)) * Math.PI)), 0.55) - 4;
    const b1 = x => 40 + 15 * wob(x, 1, 5, W), b2 = x => 124 + 20 * wob(x, 2, 4, W) + 6 * wob(x, 22, 9, W);
    const b3 = x => 186 + 12 * wob(x, 3, 5, W) - cusp(x), swd = x => 13 + 13 * wob(x, 4, 3, W), b4 = x => b3(x) + Math.max(0, swd(x));
    const b5 = x => b3(x) + Math.max(8, swd(x)) + 56 + 16 * wob(x, 5, 4, W) + cusp(x) * 0.5, b6 = x => b5(x) + 52 + 18 * wob(x, 6, 4, W);
    const run = x => b2(x) + (b3(x) - b2(x)) * 0.56 + 3 * wob(x, 23, 6, W);
    const ledgeQ = (x, y) => { const u = (x - 128) / 104, v = (y - 88) / 44; return u * u + v * v - 1 - 0.84 * (fb(x / 38, y / 26, sd + 70) - 0.5) - 0.4 * (fb(x / 12, y / 9, sd + 73) - 0.5); };
    const skerQ = (x, y) => { const u = (x - 334) / 40, v = (y - 150) / 15; return u * u + v * v - 1 - 1.0 * (fb(x / 16, y / 12, sd + 76) - 0.5); };
    const marshQ = (x, y) => { const u = (x - 488) / 128, v = (y - 236) / 84; return u * u + v * v - 1 - 0.75 * wob(x * 0.7 + y * 1.3, 11, 9, W + Hh) - 0.35 * wob(x * 1.9 - y, 21, 17, W + Hh); };
    const creekX = y => 462 + 16 * Math.sin(y / 27) + 6 * wob(y, 12, 4, Hh), creekHalf = y => 5 + 2 * (y - 150) / 180;
    const pathPts = [[88, Hh + 4], [104, 410], [150, 372], [196, 340], [226, 300], [248, 262], [262, 226]];
    const BO = []; for (let k = 0; k < 18; k++) BO.push([16 + hash2(k, 1, sd + 80) * 488, 118 + hash2(k, 2, sd + 80) * 74, 2.5 + hash2(k, 3, sd + 80) * 4.5]);
    const Z = ['eelgrass', 'ripple', 'irishmoss', 'rockweed', 'ledge', 'foreshore', 'shingle', 'sand', 'marram', 'grass', 'path', 'dirt', 'marsh', 'silt', 'musselbed', 'sedge', 'talus', 'shelf'], zi = idx(Z);
    const zone = new Uint8Array(N), elev = new Float32Array(N), fetch = new Float32Array(N).fill(1);
    for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x, B1 = b1(x), B2 = b2(x), B3 = b3(x), B4 = b4(x), B5 = b5(x), B6 = b6(x);
      let e = lerpP(y, [[0, -1.6], [B1, -0.25], [B2, 1.0], [B3, 2.55], [Math.max(B4, B3 + 8), 3.0], [B5, 4.2], [B6, 5.4], [Hh, 6.2]]), z;
      z = y < B1 ? 'eelgrass' : y < B2 ? 'ripple' : y < B3 ? 'foreshore' : y < B4 ? 'shingle' : y < B5 ? 'sand' : y < B6 ? 'marram' : 'grass';
      if (y > B2 && y < B3) { const d = Math.abs(y - run(x)), hw = 4 + 2 * wob(x, 24, 5, W); if (d < hw && !(x > 318 && x < 336)) { e -= 0.17 * (1 - d / hw); if (d < hw * 0.55) z = 'silt'; } }
      for (const [bx, by, br] of BO) { const dx = (x - bx) / br, dy = (y - by) / (br * 0.72); if (dx * dx + dy * dy < 1 && (z === 'foreshore' || z === 'ripple')) { z = 'talus'; e += 0.4 * (1 - dx * dx - dy * dy); } }
      const lq = ledgeQ(x, y);
      if (lq < 0 && y > B1 - 18 && y < B2 + 14) {
        const k = sm(0, 0.3, -lq), bench = Math.floor(clamp(-lq, 0, 0.99) * 3.4) * 0.14;
        e += 0.3 + 0.8 * k + bench + 0.1 * (fb(x / 6, y / 5, sd + 74) - 0.5);
        z = e < 0.7 + 0.35 * (fb(x / 9, y / 7, sd + 75) - 0.5) ? 'irishmoss' : e < 1.95 + 0.45 * (fb(x / 11, y / 8, sd + 77) - 0.5) ? 'rockweed' : 'ledge';
      } else if (lq < 0.2 && y > B1 && y < B2 + 10 && fb(x / 5, y / 4, sd + 78) > 0.56) { z = 'talus'; e += 0.18; }
      const sq = skerQ(x, y);
      if (sq < 0) { e = Math.max(e, 1.1 + 1.2 * sm(0, 0.4, -sq)); z = e < 1.85 ? 'rockweed' : 'shelf'; }
      if (marshQ(x, y) < 0 && y > 150) {
        e = Math.max(e, 3.45 + 0.4 * clamp((y - 150) / 120, 0, 1) + 0.12 * wob(x, 15, 6, W)); z = 'marsh'; fetch[i] = 0.2;
        const cx = creekX(y), hw = creekHalf(y);
        if (Math.abs(x - cx) < hw) { e = 0.55 + 1.9 * clamp((y - 150) / 190, 0, 1) - 0.25 * (1 - Math.abs(x - cx) / hw); z = 'silt'; }
      }
      if (x > 430 && y > 106 && y < 152 + 6 * wob(x, 16, 4, W) && y > B2 - 10) { const d = Math.hypot((x - 470) / 50, (y - 132) / 22); if (d < 1 + 0.25 * wob(x + y, 17, 5, W)) { z = 'musselbed'; e = Math.min(e, 0.9 + 0.3 * d); } }
      if (Math.hypot((x - 468) / 62, (y - 352) / 26) < 1 + 0.3 * wob(x * 1.3 + y, 18, 6, W + Hh) && y > B6 - 20) z = 'sedge';
      if (y > 240 && segD(x, y, pathPts) < 6.5 + 1.8 * wob(x + y, 19, 6, W + Hh)) { z = y < B5 - 6 ? 'sand' : 'path'; if (z === 'path') e -= 0.05; }
      if (Math.hypot((x - 72) / 1.35, y - 430) < 22 + 12 * wob(x * 1.7 + y, 20, 9, W + Hh)) z = 'dirt';
      zone[i] = zi[z]; elev[i] = e;
    }
    const fl = paint(W, Hh, Z, zone, sd, o.steps || { ripple: 0 }), t = fl.tile, inb = (x, y) => x >= 0 && y >= 0 && x < W && y < Hh;
    const wc = makeCtx(t, (x, y) => inb(x, y) && Math.abs(elev[y * W + x] - MHW) < 0.03 + 0.02 * hash2(x >> 3, 1, sd) && (zone[y * W + x] === zi.sand || zone[y * W + x] === zi.shingle), { detail: K.DETAIL });
    K.wrack(wc, sd + 40);
    return finish({ zone, zones: Z, zi, elev, fetch, w: W, h: Hh, TIDE_M, MHW, path: pathPts, creekX, marshQ, ledgeQ, bounds: { b1, b2, b3, b4, b5, b6 } }, fl, elev);
  }

  // ---- a blueberry barren: granite, a footpath, a bog pond --------------------------------------------------------
  function barren(o) {
    o = o || {}; const W = 520, Hh = 450, sd = o.seed || 21, N = W * Hh, LV = 0.8;
    const Z = ['grass', 'shelf', 'talus', 'path', 'dirt', 'sedge', 'mud', 'silt'], zi = idx(Z), zone = new Uint8Array(N), elev = new Float32Array(N), fetch = new Float32Array(N).fill(0.12);
    const pathPts = [[58, Hh + 4], [96, 402], [150, 356], [212, 322], [258, 268], [296, 222], [318, 186], [300, 150], [252, 118], [226, 70], [238, -4]];
    for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x; let e = 1.6 + 0.3 * (fb(x / 90, y / 70, sd) - 0.5), z = 'grass';
      const g = fb(x / 64, y / 46, sd + 3) + 0.25 * (fb(x / 17, y / 13, sd + 4) - 0.5);
      const wh = Math.hypot((x - 132) / 92, (y - 196) / 40) - 1 - 0.7 * (fb(x / 30, y / 22, sd + 5) - 0.5);
      if (g > 0.69 || wh < 0) { z = 'shelf'; e += 0.25 + 0.4 * sm(0.69, 0.82, g) + (wh < 0 ? 0.35 * sm(0, 0.4, -wh) : 0); }
      else if ((g > 0.63 || wh < 0.16) && fb(x / 5, y / 4, sd + 6) > 0.57) { z = 'talus'; e += 0.14; }
      else if (fb(x / 30, y / 24, sd + 7) > 0.76) z = 'dirt';
      const pq = Math.hypot((x - 410) / 74, (y - 112) / 36) - 1 - 0.6 * (fb(x / 22, y / 16, sd + 8) - 0.5);
      if (pq < 0.5) { e = Math.min(e, LV + 0.1 + 0.7 * sm(0.1, 0.5, pq)); z = pq < 0.14 ? 'mud' : 'sedge'; if (pq < 0) { e = LV - 0.08 - 0.55 * sm(0, 0.5, -pq); z = 'silt'; } }
      if (segD(x, y, pathPts) < 4 + 1.4 * (fb(x / 9, y / 9, sd + 9) - 0.5) * 2 && z !== 'silt') { if (z !== 'shelf') z = 'path'; e -= 0.03; }
      zone[i] = zi[z]; elev[i] = e;
    }
    return finish({ zone, zones: Z, zi, elev, fetch, w: W, h: Hh, path: pathPts }, paint(W, Hh, Z, zone, sd), elev);
  }

  // ---- a salt marsh: creeks, pans, a dune ------------------------------------------------------------------------------
  function marsh(o) {
    o = o || {}; const W = 520, Hh = 450, sd = o.seed || 33, N = W * Hh;
    const wob = (v, s, cells, span) => (fbmP(v / span, 0.5, cells, 1, sd + s, 3) - 0.5) * 2;
    const Z = ['ripple', 'silt', 'mud', 'marsh', 'sedge', 'grass', 'sand', 'marram', 'musselbed'], zi = idx(Z), zone = new Uint8Array(N), elev = new Float32Array(N), fetch = new Float32Array(N).fill(0.25);
    const cx = y => 250 + 66 * Math.sin(y / 58) + 22 * wob(y, 1, 4, Hh), chw = y => Math.max(3.5, 15 - 11 * y / 400);
    const t1 = x => 206 + 26 * Math.sin(x / 41) + 10 * wob(x, 2, 4, W), t2 = x => 304 + 18 * Math.sin(x / 33 + 1) + 8 * wob(x, 3, 4, W);
    const sea = x => 44 + 12 * wob(x, 4, 5, W), up = x => 392 + 16 * wob(x, 5, 4, W);
    for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x; let e = 3.35 + 0.16 * (fb(x / 40, y / 30, sd) - 0.5), z = 'marsh';
      const S = sea(x), U = up(x);
      if (y < S) { z = 'ripple'; e = -0.6 + 1.4 * y / S; fetch[i] = 1; }
      else if (y > U) { z = 'grass'; e = 4.4 + 0.4 * sm(U, U + 40, y); if (x < 190 + 20 * wob(y, 6, 3, Hh)) { z = y < U + 10 ? 'sand' : 'marram'; e += 0.3; } }
      else {
        if (y < S + 22) { e = 1.4 + 1.9 * (y - S) / 22; z = e < 2.6 ? 'silt' : 'marsh'; }
        if (fb(x / 24, y / 17, sd + 7) > 0.72 && y > S + 30) { z = 'silt'; e = 3.05; }
        const cands = [[Math.abs(x - cx(y)), chw(y), 0.35 + 1.7 * clamp(y / 400, 0, 1)]];
        if (x > cx(206) - 4 && x < 500) cands.push([Math.abs(y - t1(x)), 5 - 2.5 * (x - cx(206)) / 300, 1.6 + 0.9 * (x - cx(206)) / 300]);
        if (x < cx(304) + 4 && x > 30) cands.push([Math.abs(y - t2(x)), 4.5 - 2.5 * (cx(304) - x) / 280, 1.7 + 0.8 * (cx(304) - x) / 280]);
        for (const [d, hw, bed] of cands) { if (d < hw) { e = Math.min(e, bed - 0.3 * (1 - d / hw)); z = 'mud'; } else if (d < hw + 5 && z !== 'mud') { e = Math.min(e, bed + 0.9 + 0.5 * (d - hw) / 5); z = 'silt'; } }
      }
      if (Math.hypot((x - cx(S)) / 44, (y - S - 6) / 12) < 1 + 0.3 * wob(x + y, 8, 5, W) && y > S - 6) { z = 'musselbed'; e = Math.min(e, 0.9); }
      if (Math.hypot((x - 452) / 50, (y - 408) / 22) < 1 + 0.3 * wob(x * 1.3 + y, 9, 6, W) && y > U - 6) { z = 'sedge'; e = Math.min(e, 4.1); }
      zone[i] = zi[z]; elev[i] = e;
    }
    return finish({ zone, zones: Z, zi, elev, fetch, w: W, h: Hh, TIDE_M, MHW, creekX: cx }, paint(W, Hh, Z, zone, sd), elev);
  }

  // ---- a roadside meadow: gravel road, ruts and puddles, a tumbled wall, a ditch ------------------------------------------------
  function meadow(o) {
    o = o || {}; const W = 520, Hh = 450, sd = o.seed || 45, N = W * Hh;
    const Z = ['grass', 'path', 'dirt', 'mud', 'talus', 'sedge'], zi = idx(Z), zone = new Uint8Array(N), elev = new Float32Array(N), fetch = new Float32Array(N).fill(0.1);
    const rc = x => 292 - 0.3 * (x - 260) + 16 * Math.sin(x / 88), wallY = x => 126 + 10 * Math.sin(x / 70) + 5 * (fb(x / 30, 3, sd + 3) - 0.5);
    for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x; let e = 2.0 + 0.25 * (fb(x / 80, y / 60, sd) - 0.5), z = 'grass';
      const d = y - rc(x), ad = Math.abs(d);
      if (ad < 11) { z = 'path'; e -= 0.04; if (Math.abs(ad - 5.5) < 1.6) { z = 'dirt'; e -= 0.06; if (fb(x / 12, y / 5, sd + 4) > 0.64) { z = 'mud'; e -= 0.1; } } }
      else if (ad < 14 + 1.5 * (fb(x / 8, y / 8, sd + 5) - 0.5) * 2) { z = 'dirt'; }
      else if (d < -14 && d > -21) { z = 'sedge'; e -= 0.18 * (1 - Math.abs(d + 17.5) / 3.5); }
      const wy = wallY(x);
      if (Math.abs(y - wy) < 3.2 + 1.5 * (fb(x / 6, y / 6, sd + 6) - 0.5) * 2 && x < 360 && !(x > 150 && x < 172)) { z = 'talus'; e += 0.4; }
      else if (x < 360 && Math.abs(y - wy) < 7 && fb(x / 4, y / 4, sd + 7) > 0.7) { z = 'talus'; e += 0.15; }
      zone[i] = zi[z]; elev[i] = e;
    }
    return finish({ zone, zones: Z, zi, elev, fetch, w: W, h: Hh, roadY: rc, wallY }, paint(W, Hh, Z, zone, sd), elev);
  }

  // ---- an alder swale: a brook, a fen pool, a cart track --------------------------------------------------------------------------
  function swale(o) {
    o = o || {}; const W = 520, Hh = 450, sd = o.seed || 57, N = W * Hh, LV = 0.8;
    const Z = ['grass', 'sedge', 'mud', 'silt', 'dirt', 'path'], zi = idx(Z), zone = new Uint8Array(N), elev = new Float32Array(N), fetch = new Float32Array(N).fill(0.1);
    const bx = y => 170 + 58 * Math.sin(y / 66 + 0.6) + 18 * (fb(y / 40, 1.5, sd + 1) - 0.5) * 2;
    for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x, dB = Math.abs(x - bx(y)), pq = Math.hypot((x - 318) / 82, (y - 262) / 40) - 1 - 0.6 * (fb(x / 24, y / 18, sd + 2) - 0.5);
      const wd = Math.min(dB - 4.5, pq * 40);
      let e = 1.0 + 0.9 * sm(8, 120, wd) + 0.2 * (fb(x / 50, y / 40, sd) - 0.5), z = wd < 3 ? 'mud' : wd < 46 + 16 * (fb(x / 30, y / 24, sd + 3) - 0.5) ? 'sedge' : 'grass';
      if (wd < 0) { e = LV - 0.1 - 0.35 * sm(0, 10, -wd); z = 'silt'; }
      if (Math.abs(y - (40 + 8 * Math.sin(x / 60))) < 5 && x > 60) { z = y % 9 < 3 && Math.abs(y - (40 + 8 * Math.sin(x / 60))) < 3 ? 'dirt' : 'path'; e = Math.max(e, 1.4); }
      if (z === 'sedge' && fb(x / 7, y / 6, sd + 4) > 0.7) { e -= 0.12; z = 'mud'; }
      zone[i] = zi[z]; elev[i] = e;
    }
    return finish({ zone, zones: Z, zi, elev, fetch, w: W, h: Hh, brookX: bx }, paint(W, Hh, Z, zone, sd), elev);
  }

  // ---- planting recipes: trees at fixed spots, plants by zone (rejection-sampled and spaced by the viewer) ----------------------
  const RECIPES = {
    coast: (C) => ({
      trees: [['WhiteBirch', 486, 447, 'young', 1], ['BalsamFir', 30, 446, 'sapling', 2], ['RedSpruce', 404, 440, 'sapling', 0]],
      adds: [['SpeckledAlder', 'sedge', 1, { gap: 40, stage: 'young' }], ['RedOsierDogwood', 'grass', 1, { gap: 34, test: (x, y) => x > 330 && y > 380 }], ['WinterberryHolly', 'sedge', 1, { gap: 30, stage: 'young' }],
        ['TussockSedge', 'sedge', 3, { gap: 18 }], ['SoftRush', 'sedge', 3, { gap: 14 }], ['BlueFlag', 'sedge', 3, { gap: 10 }], ['SweetGale', 'sedge', 2, { gap: 22 }],
        ['Bayberry', 'marram', 2, { gap: 44 }], ['SweetFern', 'marram', 2, { gap: 26 }], ['BeachPea', 'marram', 3, { gap: 30 }], ['MarramGrass', 'marram', 16, { gap: 15 }],
        ['MarramGrass', 'sand', 3, { gap: 16, test: (x, y) => C.elev[y * C.w + x] > 3.8 }],
        ['WildRose', 'grass', 3, { gap: 30, test: (x, y) => y < 380 }], ['Raspberry', 'grass', 2, { gap: 34, test: (x, y) => y < 390 }],
        ['LowbushBlueberry', 'grass', 4, { gap: 26, test: (x, y) => x < 200 && y > 380 }], ['CommonJuniper', 'grass', 2, { gap: 34, test: (x, y) => x < 260 }],
        ['SheepLaurel', 'grass', 1, { gap: 24 }], ['StaghornSumac', 'grass', 1, { gap: 46, test: (x, y) => x > 250 && x < 380 && y > 395 }],
        ['Lupin', 'grass', 7, { gap: 14 }], ['OxeyeDaisy', 'grass', 6, { gap: 10 }], ['Buttercup', 'grass', 5, { gap: 12 }], ['QueenAnne', 'grass', 3, { gap: 12 }],
        ['Fireweed', 'grass', 3, { gap: 12 }], ['Goldenrod', 'grass', 3, { gap: 12 }], ['MeadowGrass', 'grass', 8, { gap: 13 }], ['Timothy', 'grass', 6, { gap: 12 }],
        ['SaltmeadowHay', 'marsh', 8, { gap: 20 }], ['BlackRush', 'marsh', 6, { gap: 15 }], ['Cattail', 'marsh', 2, { gap: 22, test: (x, y) => y > 270 }],
        ['Cordgrass', 'marsh', 5, { gap: 20, test: (x, y) => Math.abs(x - C.creekX(y)) < 18 }], ['Glasswort', 'silt', 3, { gap: 10 }],
        ['KnottedWrack', 'rockweed', 7, { gap: 20 }], ['Bladderwrack', 'rockweed', 5, { gap: 18 }], ['SeaLettuce', 'rockweed', 2, { gap: 18 }], ['SeaLettuce', 'foreshore', 3, { gap: 18, test: (x, y) => C.elev[y * C.w + x] < 1.6 }],
        ['Bladderwrack', 'talus', 3, { gap: 14 }], ['IrishMoss', 'irishmoss', 5, { gap: 20 }], ['SugarKelp', 'irishmoss', 2, { gap: 40 }], ['Eelgrass', 'eelgrass', 6, { gap: 28 }]],
    }),
    barren: (C) => ({
      trees: [['WhitePine', 70, 446, 'young', 1], ['BlackSpruce', 470, 170, 'young', 0], ['Tamarack', 330, 150, 'sapling', 2], ['RedSpruce', 500, 446, 'pole', 3], ['WhiteBirch', 290, 446, 'sapling', 0], ['BlackSpruce', 360, 92, 'sapling', 1]],
      adds: [['LowbushBlueberry', 'grass', 46, { gap: 16 }], ['SheepLaurel', 'grass', 8, { gap: 20 }], ['Rhodora', 'sedge', 4, { gap: 22 }], ['Rhodora', 'grass', 3, { gap: 22 }],
        ['BlackHuckleberry', 'grass', 7, { gap: 22 }], ['CommonJuniper', 'grass', 6, { gap: 30, test: (x, y) => C.zone[y * C.w + x + 6] === C.zi.shelf || C.zone[y * C.w + x - 6] === C.zi.shelf }],
        ['CommonJuniper', 'grass', 3, { gap: 30 }], ['SweetFern', 'grass', 6, { gap: 24 }], ['Bayberry', 'grass', 2, { gap: 50 }], ['Leatherleaf', 'sedge', 5, { gap: 20 }],
        ['TussockSedge', 'sedge', 6, { gap: 16 }], ['SoftRush', 'mud', 4, { gap: 12 }], ['SweetGale', 'sedge', 3, { gap: 22 }], ['MeadowGrass', 'grass', 10, { gap: 14 }],
        ['Fireweed', 'grass', 5, { gap: 12 }], ['Goldenrod', 'grass', 4, { gap: 12 }], ['LadySlipper', 'grass', 3, { gap: 12, test: (x, y) => y > 400 }], ['BlueFlag', 'mud', 3, { gap: 10 }]],
    }),
    marsh: (C) => ({
      trees: [['RedSpruce', 60, 446, 'young', 0], ['BalsamFir', 470, 446, 'young', 2], ['WhiteBirch', 380, 446, 'sapling', 1]],
      adds: [['SaltmeadowHay', 'marsh', 34, { gap: 16 }], ['BlackRush', 'marsh', 14, { gap: 13, test: (x, y) => y > 250 }], ['Cordgrass', 'silt', 22, { gap: 12 }],
        ['Glasswort', 'silt', 14, { gap: 9 }], ['Threesquare', 'marsh', 6, { gap: 14, test: (x, y) => y > 300 }], ['Cattail', 'sedge', 5, { gap: 14 }], ['SoftRush', 'sedge', 3, { gap: 12 }],
        ['SeaLettuce', 'mud', 5, { gap: 14 }], ['KnottedWrack', 'musselbed', 4, { gap: 16 }], ['Bladderwrack', 'silt', 3, { gap: 16, test: (x, y) => y < 110 }],
        ['MarramGrass', 'marram', 12, { gap: 14 }], ['BeachPea', 'marram', 3, { gap: 26 }], ['Bayberry', 'grass', 2, { gap: 44 }], ['SweetGale', 'grass', 2, { gap: 24 }],
        ['Goldenrod', 'grass', 5, { gap: 12 }], ['MeadowGrass', 'grass', 6, { gap: 13 }], ['WildRose', 'grass', 2, { gap: 30 }], ['Eelgrass', 'ripple', 4, { gap: 26 }]],
    }),
    meadow: (C) => ({
      trees: [['RedMaple', 470, 446, 'young', 0], ['WhiteBirch', 36, 446, 'young', 2], ['TremblingAspen', 250, 446, 'sapling', 1], ['RedOak', 420, 150, 'sapling', 3], ['BalsamFir', 500, 250, 'sapling', 0]],
      adds: [['Lupin', 'grass', 18, { gap: 11, test: (x, y) => Math.abs(y - C.roadY(x)) < 60 }], ['Lupin', 'grass', 6, { gap: 12 }], ['OxeyeDaisy', 'grass', 14, { gap: 9 }], ['Buttercup', 'grass', 12, { gap: 10 }],
        ['QueenAnne', 'grass', 10, { gap: 10, test: (x, y) => Math.abs(y - C.roadY(x)) < 50 }], ['Fireweed', 'grass', 7, { gap: 11 }], ['Goldenrod', 'grass', 9, { gap: 11 }],
        ['Timothy', 'grass', 14, { gap: 10 }], ['MeadowGrass', 'grass', 16, { gap: 11 }], ['SoftRush', 'sedge', 6, { gap: 10 }], ['BlueFlag', 'sedge', 3, { gap: 10 }],
        ['WildRose', 'grass', 5, { gap: 26, test: (x, y) => Math.abs(y - C.wallY(x)) < 22 && x < 380 }], ['Raspberry', 'grass', 5, { gap: 28, test: (x, y) => Math.abs(y - C.wallY(x)) < 30 }],
        ['StaghornSumac', 'grass', 2, { gap: 60, test: (x, y) => y > 330 }], ['Serviceberry', 'grass', 1, { gap: 50, test: (x, y) => y < 110 }], ['RedElderberry', 'grass', 1, { gap: 50, test: (x, y) => y < 120 }],
        ['BeakedHazelnut', 'grass', 1, { gap: 50, test: (x, y) => y < 110 }], ['WildRaisin', 'grass', 1, { gap: 50 }], ['CommonJuniper', 'grass', 2, { gap: 30 }]],
    }),
    swale: (C) => ({
      trees: [['BlackSpruce', 470, 446, 'young', 1], ['Tamarack', 60, 446, 'young', 0], ['WhiteCedar', 505, 180, 'sapling', 2], ['RedMaple', 300, 446, 'sapling', 3], ['BlackSpruce', 30, 170, 'sapling', 2]],
      adds: [['SpeckledAlder', 'sedge', 5, { gap: 46 }], ['PussyWillow', 'sedge', 2, { gap: 44 }], ['RedOsierDogwood', 'sedge', 3, { gap: 34 }], ['Meadowsweet', 'sedge', 6, { gap: 18 }],
        ['Steeplebush', 'sedge', 5, { gap: 18 }], ['SweetGale', 'sedge', 5, { gap: 20 }], ['Leatherleaf', 'sedge', 4, { gap: 20 }], ['WinterberryHolly', 'sedge', 2, { gap: 30 }],
        ['BlueFlag', 'mud', 8, { gap: 9 }], ['SoftRush', 'sedge', 10, { gap: 11 }], ['TussockSedge', 'sedge', 10, { gap: 14 }], ['Cattail', 'mud', 7, { gap: 11 }],
        ['LadySlipper', 'grass', 3, { gap: 12 }], ['MeadowGrass', 'grass', 8, { gap: 12 }], ['Timothy', 'grass', 5, { gap: 12 }], ['Fireweed', 'grass', 4, { gap: 12 }], ['Goldenrod', 'grass', 4, { gap: 12 }]],
    }),
  };
  const SCENES = {
    coast: { name: 'Harbour shore', note: 'The pass-8 transect re-drawn: a warped, benched ledge zoned by the tide, a grey skerry, cusps, a runnel, boulders, talus at the ledge foot; the marsh, the path and the sedge hollow as before.', build: coast, water: { tide: true }, recipe: RECIPES.coast },
    barren: { name: 'Blueberry barren', note: 'Granite shelf breaking through heath: blueberry, laurel, rhodora, huckleberry and juniper by the rock, a footpath, a bog pond with leatherleaf and sedge, pine and spruce at the edges.', build: barren, water: { level: 0.8 }, recipe: RECIPES.barren },
    marsh: { name: 'Salt marsh', note: 'A creek and two tributaries through high marsh, bare banks where cordgrass and glasswort stand, salt pans that hold rain, a mussel bed at the mouth, a dune and upland at the foot. The tide floods it.', build: marsh, water: { tide: true }, recipe: RECIPES.marsh },
    meadow: { name: 'Roadside meadow', note: 'A gravel road with ruts that puddle, a ditch of rush and flag, a tumbled field wall with rose and raspberry, and the roadside flowers: lupin, daisy, buttercup, lace, fireweed, goldenrod.', build: meadow, water: null, recipe: RECIPES.meadow },
    swale: { name: 'Alder swale', note: 'A brook opening into a fen pool, sedge ground to either side with alder, willow, dogwood, meadowsweet, steeplebush, sweet gale and winterberry, flag and cattail at the water, a cart track on the rise.', build: swale, water: { level: 0.8 }, recipe: RECIPES.swale },
  };

  root.PxKit9 = { SCENES, RECIPES, coast, barren, marsh, meadow, swale, fb, vn };
})(typeof globalThis !== 'undefined' ? globalThis : window);
