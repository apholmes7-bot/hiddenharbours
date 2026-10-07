/* Hidden Harbours — TREE MAPS, PASS 4.1: one snow map, wind weights, and the shader that reads them.
   globalThis.TreeMaps4, read off globalThis.TreeRig4 (load treeIsoRig4.js first). Plain JS, no imports.

   The rig's wind is baked: a 16-frame loop per wind level and direction. For every level, direction,
   season and variant that is 34.5 GB of RGBA8 sheets (8.6 GB for one variant). A game that animates in
   a shader needs the REST POSE and a few weights per pixel instead, and a game that shows snow at any
   cover needs one threshold per pixel instead of a sheet per cover. This file reads both off the rig
   without changing it: treeIsoRig4.js keeps its hash, so the gameplay sidecars stay valid.

     rest(key, o)            the rest pose: wind 0, no flutter, no turn-over. Every map below is on it.
                             o = {stage | size, season, variant} as for TreeRig4.frame().
     snowMap(fr)             Uint8Array, a byte per pixel: the snow cover × 254 at which the pixel turns
                             to snow. 1–254 · 255 never (sheltered: sky visibility ≤ 0.2) · 0 outside.
                             Snowed ⇔ round(cover × 254) ≥ byte. It is relight()'s own rule solved for
                             the cover, so it is exact at every cover k/254: 0, 50 % (127), 100 % (254).
     windMaps(fr)            { wind, phase }: RGBA8, opaque, filled outward from the silhouette so a gather
                             can start off it. Stamp-flat on leaves: a leaf moves as one.
                               _wind   R lean     trunk bend (height / H)^1.8         0 foot → 255 top
                                       G sway     limb reach from the stem, r / 1.3    0 → 255
                                       B flutter  255 a leaf, the flutter unit (1 px) · 0 wood and the dark between leaves
                               _phase  R wave     position across the crown, X = (x − cx) / crownR:  (X / 4 + ½) · 255
                                       G play     the mass's own phase offset J ∈ ±0.25 rad:  (J / 0.5 + ½) · 255
                                       B depth    view depth over the rest pose's range, 0 far → 255 near
     constants(key, stage)   what the shader needs beside the maps: px at a gale, bob, flutter rates
     shade(rest, wind, f)    the reference shader: the rest pose moved through the maps, returned as a
                             frame TreeRig4.relight() / view() / castShadow() accept. wind {w, gust, dir}, f 0–15
     view(fr, ch, sky)       'snow' · 'wind' · 'phase' as RGBA; any other channel is TreeRig4.view()
     sheet(key, stage, season, ch, sky)   the four variants at rest side by side: the weights pipeline's sheet
     manifest()              constants for every species × stage and the encodings (maps/treeMaps4.json)

   THE SHADER, for wind w · gust g · dir ±1 · loop phase φ = f / 16, at a rest pixel:
     env   = 1 + 0.9 g sin(2πφ + 0.7)
     trunk = w² bendPx (0.8 + 0.2 env) + 0.42 w bendPx sin(2πφ + 0.3)(0.55 + 0.45 env)
     la    = limbPx w (0.6 + 0.4 env)
     p     = 4πφ − 0.9 dir X + J                          s = G / 255 × 1.3
     field = ( dir (trunk · R/255 + la s^1.2 (sin p + ½)),  −½ la bob s sin(p + 1.1) )
   These are TreeRig4.windAt()'s terms, sampled where the rig samples them: a leaf's centre, a base pixel,
   a point on a limb. A rest pixel moves round(field). A leaf moves round(field + its dither) plus its
   flutter push, one offset for the whole leaf, keyed on its stamp id (the _detail map) by the rig's own
   hash. Gather, per destination D: start at D, step twice S ← D − round(field(S)); of the 25 pixels
   within 2 of S keep those whose own offset lands them on D; the nearer wins (depth, beyond a 3/255
   margin), then a leaf over the rest, then the later stamp. None lands? A foliage pixel under S shows as
   the dark between leaves (a leaf moved off it), as in the rig; wood shows as wood.                   */
(function (root) {
  'use strict';
  const R = root.TreeRig4;
  if (!R) throw new Error('treeMaps4.js: load treeIsoRig4.js first');
  const LOOP = R.LOOP, CE = R.CE, SE = R.SE, UPV = R.UPV, FOL = R.M.FOLIAGE, TAU = Math.PI * 2;
  const X_SPAN = 4, J_SPAN = 0.5, R_MAX = 1.3, MARGIN = 3, SNOW_NEVER = 255;
  const REST_PHASE = 1 / 1600;   // loop frames sit on multiples of 1/16: the rig's frame cache never mixes the two
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const smooth = (e0, e1, x) => { const t = clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); };
  // the rig's two hashes, verbatim (it does not export them): the snow rule and the flutter slots key on them
  function vnoise(x, y, s) { const n = Math.sin(x * 127.1 + y * 311.7 + s * 74.7) * 43758.5453; return n - Math.floor(n); }
  const hsh = (n, k) => { let h = Math.imul((n | 0) ^ Math.imul(k + 1, 0x9e3779b9), 0x85ebca6b); h ^= h >>> 13; h = Math.imul(h, 0xc2b2ae35); h ^= h >>> 16; return (h >>> 0) / 4294967296; };
  const conif = (sp) => sp.form === 'spire' || sp.form === 'pine' || sp.form === 'cedar' || sp.form === 'larch';

  // ---- the rest pose --------------------------------------------------------------------------------
  function rest(key, o) {
    const sp = R.byKey[key]; if (!sp) throw new Error('treeMaps4: unknown species ' + key);
    const sh = sp.shimmer; sp.shimmer = null;   // the aspen trembles in still air; not in its rest pose (restored at once)
    try { return R.frame(key, Object.assign({}, o, { wind: { w: 0, gust: 0, dir: 1 }, phase: REST_PHASE })); }
    finally { sp.shimmer = sh; }
  }

  // ---- snow: relight()'s rule, solved for the cover ----------------------------------------------------
  // relight: snow > 0.02 && up > 1.02 − snow·0.95 + (hs − 0.5)·0.34 && ao > 0.2 && (not the dark between
  // leaves || snow > 0.6). Monotone in snow, so the smallest k with the rule true at snow = k/254 is found
  // by bisection on the rule itself, evaluated in the same floating-point order.
  function snowByte(up, hs, ao, between) {
    if (!(ao > 0.2)) return SNOW_NEVER;
    const ok = (k) => { const snow = k / 254; if (!(snow > 0.02)) return false; const thr = 1.02 - snow * 0.95 + (hs - 0.5) * 0.34; return up > thr && (!between || snow > 0.6); };
    if (!ok(254)) return SNOW_NEVER;
    let lo = 1, hi = 254;
    while (lo < hi) { const m = (lo + hi) >> 1; if (ok(m)) hi = m; else lo = m + 1; }
    return lo;
  }
  function snowMap(fr) {
    if (fr._snow) return fr._snow;
    const v = fr.v, w = v.w, out = new Uint8Array(w * v.h);
    for (let y = 0; y < v.h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i]) continue;
      const s = v.st[i], up = v.nx[i] * UPV[0] + v.ny[i] * UPV[1] + v.nz[i] * UPV[2];
      const hs = s >= 0 ? vnoise(s & 8191, 7, 91) : vnoise(x >> 1, y >> 1, 91);
      out[i] = snowByte(up, hs, v.ao[i], v.mat[i] === FOL && s < 0);
    }
    return (fr._snow = out);
  }

  // ---- wind weights ---------------------------------------------------------------------------------
  function weightsOf(fr) {
    if (fr._ww) return fr._ww;
    const mdl = fr.mdl, g = mdl.g, v = fr.v, w = v.w, h = v.h, N = w * h, cell = mdl.cell, piv = mdl.pivot, cR = Math.max(8, g.crownR);
    const HK = new Float32Array(N), RR = new Float32Array(N), XX = new Float32Array(N), JJ = new Float32Array(N), LF = new Uint8Array(N), DP = new Uint8Array(N), set = new Uint8Array(N);
    const put = (i, x, y, z, m) => {     // windAt()'s own terms at a build-space point
      HK[i] = Math.pow(clamp((g.baseY - y) / g.H, 0, 1), 1.8);
      RR[i] = Math.min(R_MAX, Math.hypot(x - g.cx, z) / cR);
      XX[i] = (x - g.cx) / cR;
      JJ[i] = ((((m == null ? 0 : m) * 0.618034) % 1) - 0.5) * 0.5;
    };
    // a screen point at a part's mean depth, as frame() builds its field grid
    const scr = (i, sx, sy, zc, m) => { const Yr = sy - piv.y; put(i, sx - cell.dx, g.baseY - (SE * zc - CE * Yr), SE * Yr + CE * zc, m); };
    const zr = Math.max(1, mdl.zmax - mdl.zmin), leafAt = new Map();
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i]) continue;
      set[i] = 1; DP[i] = Math.round(clamp((v.z[i] - mdl.zmin) / zr, 0, 1) * 255);
      if (v.mat[i] === FOL) {
        const P = mdl.parts[v.part[i]], s = v.st[i];
        if (!P) { scr(i, x + 0.5, y + 0.5, 0, 900); continue; }
        if (s >= 0) {                                   // a leaf: the field under its centre, for every pixel of it
          LF[i] = 1;
          const j = leafAt.get(s);
          if (j != null) { HK[i] = HK[j]; RR[i] = RR[j]; XX[i] = XX[j]; JJ[i] = JJ[j]; continue; }
          const k = s - P.sb - 1; scr(i, P.scs[k * 2], P.scs[k * 2 + 1], P.zc, P.m); leafAt.set(s, i);
        } else scr(i, x + 0.5, y + 0.5, P.zc, P.m);    // the dark between leaves: per pixel, as frame() warps it
      } else {                                          // wood: the point on its limb piece under the pixel
        const L = mdl.limbs[v.id[i]];
        if (!L) { scr(i, x + 0.5, y + 0.5, 0, 900); continue; }
        const ex = L.sb[0] - L.sa[0], ey = L.sb[1] - L.sa[1], t = clamp(((x + 0.5 - L.sa[0]) * ex + (y + 0.5 - L.sa[1]) * ey) / (ex * ex + ey * ey || 1e-6), 0, 1);
        put(i, L.a[0] + (L.b[0] - L.a[0]) * t, L.a[1] + (L.b[1] - L.a[1]) * t, L.a[2] + (L.b[2] - L.a[2]) * t, L.m);
      }
    }
    // filled outward, nearest first, so a gather that starts off the silhouette still finds the tree
    const q = new Int32Array(N); let qh = 0, qt = 0;
    for (let i = 0; i < N; i++) if (set[i]) q[qt++] = i;
    while (qh < qt) {
      const i = q[qh++], x = i % w, y = (i / w) | 0;
      for (let d = 0; d < 4; d++) {
        const jx = x + (d === 0 ? 1 : d === 1 ? -1 : 0), jy = y + (d === 2 ? 1 : d === 3 ? -1 : 0);
        if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue;
        const j = jy * w + jx; if (set[j]) continue;
        set[j] = 1; HK[j] = HK[i]; RR[j] = RR[i]; XX[j] = XX[i]; JJ[j] = JJ[i]; q[qt++] = j;
      }
    }
    return (fr._ww = { HK, RR, XX, JJ, LF, DP });
  }
  function windMaps(fr) {
    if (fr._wmaps) return fr._wmaps;
    const W = weightsOf(fr), N = fr.v.w * fr.v.h, wind = new Uint8ClampedArray(N * 4), phase = new Uint8ClampedArray(N * 4);
    let clipped = 0;
    for (let i = 0; i < N; i++) {
      const o = i * 4, xn = W.XX[i] / X_SPAN + 0.5;
      if (fr.v.a[i] && (xn < 0 || xn > 1)) clipped++;
      wind[o] = Math.round(W.HK[i] * 255); wind[o + 1] = Math.round(W.RR[i] / R_MAX * 255); wind[o + 2] = W.LF[i] ? 255 : 0; wind[o + 3] = 255;
      phase[o] = Math.round(clamp(xn, 0, 1) * 255); phase[o + 1] = Math.round(clamp(W.JJ[i] / J_SPAN + 0.5, 0, 1) * 255); phase[o + 2] = W.DP[i]; phase[o + 3] = 255;
    }
    return (fr._wmaps = { wind, phase, clipped });
  }
  // what an engine reads back out of the two images
  function decoded(fr) {
    if (fr._wdec) return fr._wdec;
    const m = windMaps(fr), N = fr.v.w * fr.v.h;
    const D = { lean: new Float32Array(N), sway: new Float32Array(N), leaf: new Uint8Array(N), X: new Float32Array(N), J: new Float32Array(N), dep: new Uint8Array(N) };
    for (let i = 0; i < N; i++) {
      const o = i * 4;
      D.lean[i] = m.wind[o] / 255; D.sway[i] = m.wind[o + 1] / 255 * R_MAX; D.leaf[i] = m.wind[o + 2] > 127 ? 1 : 0;
      D.X[i] = (m.phase[o] / 255 - 0.5) * X_SPAN; D.J[i] = (m.phase[o + 1] / 255 - 0.5) * J_SPAN; D.dep[i] = m.phase[o + 2];
    }
    return (fr._wdec = D);
  }

  // ---- constants ------------------------------------------------------------------------------------
  function constants(key, stage) {
    const sp = R.byKey[key]; if (!sp) throw new Error('treeMaps4: unknown species ' + key);
    const t = typeof stage === 'number' ? stage : R.sizeOf({ stage }), wv = R.windOf(sp), H = Math.max(26, sp.worldH * t), cell = R.cellOf(sp, t);
    return { key, stage: R.stageName(t), size: t, cell: [cell.w, cell.h], pivot: [cell.pivotX, cell.pivotY], H,
      bendPx: wv[0] * H, limbPx: wv[1] * H / 300, bob: wv[3], flutter: wv[2], shimmer: sp.shimmer ? sp.shimmer.slice() : null, conifer: conif(sp), loop: LOOP };
  }

  // ---- the reference shader -------------------------------------------------------------------------
  function shade(rf, W, f) {
    W = W || {};
    const mdl = rf.mdl, sp = mdl.sp, v = rf.v, w = v.w, h = v.h, N = w * h, C = constants(mdl.key, mdl.size), Dq = decoded(rf);
    const ww = clamp(+W.w || 0, 0, 1), gust = W.gust == null ? 0.4 : clamp(+W.gust, 0, 1), dir = W.dir < 0 ? -1 : 1;
    const f16 = (((f | 0) % LOOP) + LOOP) % LOOP, ph = f16 / LOOP;
    const env = 1 + gust * 0.9 * Math.sin(TAU * ph + 0.7);
    const trunk = ww * ww * C.bendPx * (0.8 + 0.2 * env) + ww * C.bendPx * 0.42 * Math.sin(TAU * ph + 0.3) * (0.55 + 0.45 * env);
    const la = C.limbPx * ww * (0.6 + 0.4 * env);
    const FX = new Float32Array(N), FY = new Float32Array(N);
    if (ww > 0) for (let i = 0; i < N; i++) {
      const p = 2 * TAU * ph - dir * 0.9 * Dq.X[i] + Dq.J[i];
      FX[i] = dir * (trunk * Dq.lean[i] + la * Math.pow(Dq.sway[i], 1.2) * (Math.sin(p) + 0.5));
      FY[i] = -la * C.bob * 0.5 * Dq.sway[i] * Math.sin(p + 1.1);
    }
    // offsets: per pixel, and per LEAF (dither + flutter + turn-over on the rig's own slots)
    const resp = smooth(0, 0.85, ww), shS = sp.shimmer, wv = R.windOf(sp), conifer = C.conifer;
    const dRate = Math.min(1, (wv[2] * resp * 0.35 + (shS ? shS[0] * shS[1] * 0.03 : 0)) * LOOP / 2.9);
    const tRate = shS ? Math.min(1, shS[0] * (shS[1] + (1 - shS[1]) * resp) * 0.10 * LOOP / 2.9) : 0;
    const nS = mdl.stampN + 1, OX = new Int16Array(N), OY = new Int16Array(N), flut = new Uint8Array(nS), lx = new Int16Array(nS), ly = new Int16Array(nS), seen = new Uint8Array(nS);
    let moved = 0;
    for (let i = 0; i < N; i++) {
      if (!v.a[i]) continue;
      if (!Dq.leaf[i]) { OX[i] = Math.round(FX[i]); OY[i] = Math.round(FY[i]); continue; }
      const gs = v.st[i];
      if (!seen[gs]) {
        seen[gs] = 1;
        let sx = Math.round(FX[i] + (hsh(gs, 8) - 0.5) * 0.8), sy = Math.round(FY[i] + (hsh(gs, 9) - 0.5) * 0.8), mv = false;
        if (dRate > 0) for (let e = 0; e < 2; e++) {
          const s0 = (hsh(gs, 10 + e) * LOOP) | 0;
          if ((f16 - s0 + LOOP) % LOOP >= (hsh(gs, 12 + e) < 0.55 ? 1 : 2)) continue;
          if (hsh(gs, 14 + e) >= dRate * (1 + gust * 0.9 * Math.sin(TAU * s0 / LOOP + 0.7 - dir * Dq.X[i] * 1.3))) continue;
          const r = hsh(gs, 16 + e);
          if (conifer) sx += r < 0.8 ? dir : -dir;
          else if (r < 0.55) sx += dir; else if (r < 0.75) sy -= 1; else if (r < 0.9) { sx += dir; sy -= 1; } else sx -= dir;
          mv = true; moved++; flut[gs] |= 1; break;
        }
        if (tRate > 0) {
          let tn = mv && hsh(gs, 18) < shS[0] * 0.5;
          for (let e = 0; e < 2 && !tn; e++) { const s0 = (hsh(gs, 20 + e) * LOOP) | 0; if ((f16 - s0 + LOOP) % LOOP < (hsh(gs, 22 + e) < 0.5 ? 1 : 2) && hsh(gs, 24 + e) < tRate) tn = true; }
          if (tn) flut[gs] |= 2;
        }
        lx[gs] = sx; ly[gs] = sy;
      }
      OX[i] = lx[gs]; OY[i] = ly[gs];
    }
    // the gather
    const o = { w, h, z: new Float32Array(N).fill(-1e9), nx: new Float32Array(N), ny: new Float32Array(N), nz: new Float32Array(N), mat: new Uint8Array(N), a: new Uint8Array(N),
      id: new Int16Array(N).fill(-1), mid: new Int16Array(N).fill(-1), st: new Int32Array(N).fill(-1), ao: new Float32Array(N), part: new Int16Array(N).fill(-1), r: new Float32Array(N) };
    const bury = new Uint8Array(N), src = new Int32Array(N).fill(-1), dep = Dq.dep, leaf = Dq.leaf;
    const front = (j, b) => {
      const dj = dep[j], db = dep[b];
      if (dj > db + MARGIN) return true; if (db > dj + MARGIN) return false;
      if (leaf[j] !== leaf[b]) return leaf[j] > leaf[b];
      return leaf[j] ? v.st[j] > v.st[b] : dj > db;
    };
    for (let Y = 0; Y < h; Y++) for (let X = 0; X < w; X++) {
      let sx = X, sy = Y;
      for (let it = 0; it < 2; it++) { const j = clamp(sy, 0, h - 1) * w + clamp(sx, 0, w - 1); sx = X - Math.round(FX[j]); sy = Y - Math.round(FY[j]); }
      let best = -1, base = false;
      for (let dy = -2; dy <= 2; dy++) {
        const yy = sy + dy; if (yy < 0 || yy >= h) continue;
        for (let dx = -2; dx <= 2; dx++) {
          const xx = sx + dx; if (xx < 0 || xx >= w) continue;
          const j = yy * w + xx;
          if (!v.a[j] || xx + OX[j] !== X || yy + OY[j] !== Y) continue;
          if (best < 0 || front(j, best)) best = j;
        }
      }
      if (best < 0 && sx >= 0 && sy >= 0 && sx < w && sy < h) { const j = sy * w + sx; if (v.a[j]) { best = j; base = v.mat[j] === FOL; } }
      if (best < 0) continue;
      const i = Y * w + X;
      o.z[i] = v.z[best]; o.nx[i] = v.nx[best]; o.ny[i] = v.ny[best]; o.nz[i] = v.nz[best]; o.mat[i] = v.mat[best]; o.a[i] = 1;
      o.id[i] = v.id[best]; o.mid[i] = v.mid[best]; o.st[i] = base ? -1 : v.st[best]; o.ao[i] = v.ao[best]; o.part[i] = v.part[best]; o.r[i] = v.r[best];
      bury[i] = rf.bury[best]; src[i] = best;
    }
    return { mdl, v: o, D: distField(o.a, w, h), bury, ph, W: { w: ww, gust, dir }, frame: f16, offs: rf.offs, lp: rf.lp, despeckled: 0, flut, moved, shaded: true, src };
  }
  // the rig's chamfer distance-to-edge (3-4) in px, verbatim: relight() reads it for the sun rim
  function distField(a, w, h) {
    const D = new Float32Array(w * h), BIG = 1e6;
    for (let i = 0; i < w * h; i++) D[i] = a[i] ? BIG : 0;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!D[i]) continue; let d = D[i];
      if (y > 0) { d = Math.min(d, D[i - w] + 3); if (x > 0) d = Math.min(d, D[i - w - 1] + 4); if (x < w - 1) d = Math.min(d, D[i - w + 1] + 4); }
      if (x > 0) d = Math.min(d, D[i - 1] + 3);
      D[i] = d;
    }
    for (let y = h - 1; y >= 0; y--) for (let x = w - 1; x >= 0; x--) {
      const i = y * w + x; if (!D[i]) continue; let d = D[i];
      if (y < h - 1) { d = Math.min(d, D[i + w] + 3); if (x > 0) d = Math.min(d, D[i + w - 1] + 4); if (x < w - 1) d = Math.min(d, D[i + w + 1] + 4); }
      if (x < w - 1) d = Math.min(d, D[i + 1] + 3);
      D[i] = d;
    }
    for (let i = 0; i < w * h; i++) D[i] /= 3;
    return D;
  }

  // ---- views, sheets, manifest ------------------------------------------------------------------------
  function view(fr, ch, sky) {
    const N = fr.v.w * fr.v.h;
    if (ch === 'snow') { const s = snowMap(fr), out = new Uint8ClampedArray(N * 4); for (let i = 0; i < N; i++) if (fr.v.a[i]) { out[i * 4] = out[i * 4 + 1] = out[i * 4 + 2] = s[i]; out[i * 4 + 3] = 255; } return out; }
    if (ch === 'wind' || ch === 'phase') return windMaps(fr)[ch].slice();
    return R.view(fr, ch, sky);
  }
  const SHEET_MAPS = ['unlit', 'normal', 'light', 'detail', 'snow', 'wind', 'phase'];
  function sheet(key, stage, season, ch, sky) {
    const frs = [0, 1, 2, 3].map(variant => rest(key, { stage, season, variant }));
    const cw = frs[0].v.w, chh = frs[0].v.h, W = cw * 4, out = new Uint8ClampedArray(W * chh * 4);
    frs.forEach((fr, k) => { const px = view(fr, ch, sky); for (let y = 0; y < chh; y++) out.set(px.subarray(y * cw * 4, (y + 1) * cw * 4), (y * W + k * cw) * 4); });
    return { w: W, h: chh, rgba: out, cell: [cw, chh], pivot: [frs[0].mdl.pivot.x, frs[0].mdl.pivot.y], cols: 4, rows: 1 };
  }
  const r4 = (x) => Math.round(x * 1e4) / 1e4;
  function manifest() {
    const species = {};
    for (const sp of R.SPECIES) {
      const wv = R.windOf(sp), stages = {};
      for (const k of R.STAGE_KEYS) { const c = constants(sp.key, k); stages[k] = { cell: c.cell, pivot: c.pivot, H_px: r4(c.H), bendPx: r4(c.bendPx), limbPx: r4(c.limbPx) }; }
      species[sp.key] = { name: sp.name, conifer: conif(sp), bob: wv[3], flutter: wv[2], shimmer: sp.shimmer ? sp.shimmer.slice() : null, stages };
    }
    return {
      schema: 'hidden-harbours/tree-maps@1', loop: LOOP, sheet: { cols: 4, rows: 1, order: 'variant 1–4, rest pose', maps: SHEET_MAPS },
      encodings: {
        snow: 'byte = cover × 254 at which the pixel turns to snow; 1–254; 255 never; alpha 0 outside. snowed = round(cover × 254) >= byte',
        wind: 'R lean = (height / H)^1.8 × 255 · G sway = r / 1.3 × 255 · B flutter 255 leaf / 0 · A 255 (filled outward)',
        phase: 'R wave = (X / 4 + 0.5) × 255, X = (x − cx) / crownR · G play = (J / 0.5 + 0.5) × 255 · B depth 0 far – 255 near · A 255 (filled outward)',
        stampId: '_detail RG − 1 = the leaf stamp id the flutter slots hash (0 = not a leaf)',
      },
      shader: 'see treeMaps4.js header: field from lean/sway/wave/play; leaves add dither + flutter keyed on the stamp id; gather over 5 × 5 by depth',
      species,
    };
  }

  root.TreeMaps4 = { REST_PHASE, X_SPAN, J_SPAN, R_MAX, MARGIN, SNOW_NEVER, SHEET_MAPS, rest, snowMap, snowByte, windMaps, decoded, constants, shade, view, sheet, manifest, distField, vnoise, hsh };
})(typeof globalThis !== 'undefined' ? globalThis : window);
