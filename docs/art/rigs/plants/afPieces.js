/* Hidden Harbours — THE ALDER FALL'S PIECES (globalThis.StpAfPieces), pass 6, 2026-09-27. What the fall on Alder Run needs that
   the kits do not draw: the fallen alder across the gully (one grey stem snapped off its stool, bare and mossy), and the tin cup
   left on the flat stone. Face models lit by StationLight2, as Ginny's pieces are (pass 5). Ramps only, no dither, no keyline.
   Also two plants the swale kit lacks, added to PlantRig5's table at load (the rig is not edited): ostrich fern, on the gully's
   walls, and marsh marigold, at the pool's edge (yellow in spring).
   LOCAL FRAME as StpGinnyPieces: +x right, -y the show side; dir 0 faces S, each dir turns 45 deg CCW.
   API  frame(key, dir, o) · relight(fr, sky, o) · castShadow · shadeAt · PIECES · RAMPS                                     */
(function (root) {
  'use strict';
  const L = () => { const l = root.StationLight2; if (!l) throw new Error('StpAfPieces: load stationLight2.js first'); return l; };
  const S = 32, D2R = Math.PI / 180, SE = Math.sin(40 * D2R), CE = Math.cos(40 * D2R), TAU = Math.PI * 2;
  const RAMPS = {
    alder: ['#2f2c29', '#403c38', '#534e48', '#68625a', '#7e776d', '#948c80'], wood: ['#6b5a3e', '#84704f', '#9c8661', '#b39b73', '#c7af86', '#d8c29a'],
    moss: ['#1f2d14', '#2b3f1a', '#39521f', '#4a6726', '#5d7d2f', '#72933a'], tin: ['#3e4446', '#586064', '#788185', '#9aa3a6', '#bfc6c7', '#e2e6e5'],
    earth: ['#2a1d15', '#3a281c', '#4c3424', '#5f412c', '#735035', '#88603f'] };
  const M = (ramp, por, gloss, hold) => ({ ramp, por, gloss, hold });
  const MATS = { alder: M(RAMPS.alder, .35, .02, .4), wood: M(RAMPS.wood, .4, .01, .4), moss: M(RAMPS.moss, .5, .01, .3), tin: M(RAMPS.tin, .01, .65, .8), earth: M(RAMPS.earth, .5, .01, .3) };
  const F = (v, mat, b, o) => Object.assign({ v, mat, b: b || 0 }, o || {});
  function bar(out, p0, p1, r, mat, b, r1) { const d = [p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]], Ln = Math.hypot(d[0], d[1], d[2]) || 1, u = d.map(c => c / Ln), t = Math.abs(u[2]) < 0.88 ? [0, 0, 1] : [1, 0, 0];
    let n1 = [u[1] * t[2] - u[2] * t[1], u[2] * t[0] - u[0] * t[2], u[0] * t[1] - u[1] * t[0]]; const m = Math.hypot(n1[0], n1[1], n1[2]) || 1; n1 = n1.map(c => c / m);
    const n2 = [u[1] * n1[2] - u[2] * n1[1], u[2] * n1[0] - u[0] * n1[2], u[0] * n1[1] - u[1] * n1[0]], rb = r1 == null ? r : r1;
    const sg = [[-1, -1], [1, -1], [1, 1], [-1, 1]], A = sg.map(s => [0, 1, 2].map(k => p0[k] + (n1[k] * s[0] + n2[k] * s[1]) * r)), B = sg.map(s => [0, 1, 2].map(k => p1[k] + (n1[k] * s[0] + n2[k] * s[1]) * rb));
    for (let i = 0; i < 4; i++) out.push(F([A[i], A[(i + 1) % 4], B[(i + 1) % 4], B[i]], mat, b, { ao: false })); out.push(F([B[0], B[1], B[2], B[3]], mat, b, { ao: false })); out.push(F([A[3], A[2], A[1], A[0]], mat, b, { ao: false })); }
  function frustum(out, z0, z1, r0, r1, n, mat, b, cx, cy, o) { cx = cx || 0; cy = cy || 0;
    for (let i = 0; i < n; i++) { const a0 = TAU * i / n, a1 = TAU * (i + 1) / n, p = (a, r, z) => [cx + Math.cos(a) * r, cy + Math.sin(a) * r, z];
      out.push(F([p(a0, r0, z0), p(a1, r0, z0), p(a1, r1, z1), p(a0, r1, z1)], mat, b, o)); } }
  function disc(out, cx, cy, z, r, n, mat, b) { const v = []; for (let i = 0; i < n; i++) { const a = TAU * i / n; v.push([cx + Math.cos(a) * r, cy + Math.sin(a) * r, z]); } out.push(F(v, mat, b)); }
  const hsh = (k) => { let h = Math.imul(k ^ 0x9e3779b9, 2654435761); h ^= h >>> 15; h = Math.imul(h, 2246822519); h ^= h >>> 13; return (h >>> 0) / 4294967296; };

  const PIECES = {
    /* the stem lies WSW-ENE across the gully, its snapped foot on the east shoulder by the stool it came off, its crown on the
       west one; local z 0 is the gully floor under its middle, so the ends rest at +zW and +zE */
    fallenAlder: { defaults: { len: 3.6, dy: 0.32, zW: 0.92, zE: 1.0, sag: 0.1, r: 0.1 }, build(s) { const o = [], hl = s.len / 2;
      const at = (t) => { const x = -hl + s.len * t, y = -s.dy + 2 * s.dy * t + 0.1 * Math.sin(t * Math.PI * 1.3), z = s.zW + (s.zE - s.zW) * t - s.sag * Math.sin(t * Math.PI); return [x, y, z]; };
      const N = 7; for (let k = 0; k < N; k++) { const a = at(k / N), b = at((k + 1) / N), r0 = s.r * (0.72 + 0.28 * k / N), r1 = s.r * (0.72 + 0.28 * (k + 1) / N);
        bar(o, [a[0], a[1], a[2] + r0], [b[0], b[1], b[2] + r1], r0, 'alder', k % 2 ? 0 : -1, r1);
        if (k > 0 && k < N - 1 && hsh(k * 7 + 3) > 0.35) { const m = at((k + 0.5) / N), w = 0.1 + 0.12 * hsh(k * 11); bar(o, [m[0] - w, m[1], m[2] + r0 * 2 + 0.005], [m[0] + w, m[1] + 0.02, m[2] + r0 * 2 + 0.005], 0.028, 'moss', 1); } }
      const foot = at(1); bar(o, [foot[0] - 0.02, foot[1], foot[2] + s.r], [foot[0] + 0.07, foot[1] + 0.01, foot[2] + s.r * 1.2], s.r * 1.02, 'wood', 1, s.r * 0.6);
      bar(o, [foot[0] + 0.05, foot[1] - 0.03, foot[2] + s.r * 1.4], [foot[0] + 0.16, foot[1] - 0.02, foot[2] + s.r * 2.3], 0.018, 'wood', 1);
      // the crown: forked stems and twigs off the west end, bare
      const tw = [[0.06, -0.34, 0.25, 0.75], [0.14, 0.32, 0.34, 0.62], [0.24, -0.1, 0.5, 0.55], [0.33, 0.4, 0.2, 0.46], [0.2, -0.5, 0.02, 0.5], [0.45, -0.25, 0.45, 0.38]];
      tw.forEach((q, k) => { const b0 = at(q[0]), dir = [-Math.cos(q[1]), Math.sin(q[1]) * 0.9, q[2]], Ln = q[3], e = [b0[0] + dir[0] * Ln, b0[1] + dir[1] * Ln, b0[2] + s.r + dir[2] * Ln];
        bar(o, [b0[0], b0[1], b0[2] + s.r], e, 0.034, 'alder', k % 2 ? 0 : 1, 0.02);
        for (let j = 0; j < 3; j++) { const a2 = q[1] + (j - 1) * 0.7, f = [e[0] - Math.cos(a2) * 0.24, e[1] + Math.sin(a2) * 0.24, e[2] + 0.08 + 0.07 * j]; bar(o, e, f, 0.016, 'alder', 1, 0.01); } });
      for (let k = 1; k < 6; k++) { const m = at(k / 6 + 0.04), h = 0.12 + 0.1 * hsh(k * 5 + 1); if (hsh(k * 13) < 0.45) continue; bar(o, [m[0], m[1], m[2] + s.r * 1.8], [m[0] + 0.04, m[1] - 0.02, m[2] + s.r * 1.8 + h], 0.018, 'alder', 1, 0.01); }
      return o; } },
    tinCup: { defaults: {}, build() { const o = [];
      frustum(o, 0, 0.085, 0.04, 0.046, 10, 'tin', 0); disc(o, 0, 0, 0.083, 0.043, 10, 'tin', -1);
      for (let k = 0; k < 5; k++) { const a0 = -Math.PI / 2 + k * Math.PI / 5, a1 = a0 + Math.PI / 5, p = (a) => [0.046 + 0.028 * Math.cos(a) + 0.028, 0, 0.042 + 0.028 * Math.sin(a)]; bar(o, p(a0), p(a1), 0.006, 'tin', 1); }
      return o; } },
  };

  const MODELS = new Map();
  function built(key, o) { const P = PIECES[key]; if (!P) throw new Error('StpAfPieces: no piece "' + key + '"'); const s = Object.assign({}, P.defaults, o || {}), gk = key + '|' + JSON.stringify(s);
    let B = MODELS.get(gk); if (B) return B; const faces = P.build(s); B = { key, s, faces, mdl: L().model(faces, MATS, { aoRes: 12 }) }; MODELS.set(gk, B); if (MODELS.size > 40) MODELS.delete(MODELS.keys().next().value); return B; }
  function frame(key, dir, o) { const B = built(key, o), a = ((dir | 0) % 8) * Math.PI / 4, ct = Math.cos(a), st = Math.sin(a); let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
    for (const f of B.faces) for (const p of f.v) { const xr = p[0] * ct - p[1] * st, yr = p[0] * st + p[1] * ct, X = xr * S, Y = -(yr * SE + p[2] * CE) * S; if (X < x0) x0 = X; if (X > x1) x1 = X; if (Y < y0) y0 = Y; if (Y > y1) y1 = Y; }
    const pad = 3, ox = Math.ceil(-x0) + pad, oy = Math.ceil(-y0) + pad, W = Math.ceil(x1 - x0) + 2 * pad + 1, H = Math.ceil(y1 - y0) + 2 * pad + 1, C = { ct, st, se: SE, ce: CE, S, ox, oy };
    const fr = L().frame(B.mdl, C, W, H); fr.piece = key; fr.dir = dir | 0; fr.opts = B.s; fr.w = W; fr.h = H; fr.px = ox; fr.py = oy; return fr; }
  function relight(fr, sky, o) { o = o || {}; return L().relight(fr, sky, Object.assign({ emit: () => 0, lights: [], wind: sky && sky.wind }, o)); }

  /* the two plants, in PlantRig5's own species format (the swale's ostrich fern: a vase of arching fronds; marsh marigold: glossy
     round leaves, yellow cups in spring) */
  const PR = root.PlantRig5;
  if (PR && PR.byKey && !PR.byKey.OstrichFern) {
    const WS = PR.byKey.SweetFern.wind, WH = PR.byKey.Buttercup.wind;
    const add = (sp) => { sp.trans = sp.trans == null ? 0.8 : sp.trans; PR.byKey[sp.key] = sp; if (PR.SPECIES && PR.SPECIES.indexOf(sp) < 0) PR.SPECIES.push(sp); };
    add({ key: 'OstrichFern', name: 'Ostrich Fern', latin: 'Matteuccia struthiopteris', fam: 'shrub', hab: 'swale', zone: 4.65, habit: 'woody', h: 36, w: 50, fol: '#5f8f3c', fall: '#b98a3e', bark: '#5a7a38', wind: WS, trans: 0.9,
      p: { form: 'clump', stems: 12, spread: 0.72, arch: 0.7, forks: 0, grain: 'fern', leafDen: 1.1, leafFrom: 0.1, mr: 2.0, cover: 1.2 } });
    add({ key: 'MarshMarigold', name: 'Marsh Marigold', latin: 'Caltha palustris', fam: 'flower', hab: 'wet', zone: 4.65, habit: 'herb', h: 15, w: 30, fol: '#3e6d34', bloomC: '#f4c418', clover: '#f4c418', wind: WH,
      p: { head: 'cup', basal: 'lobed', bloomS: ['spring'] } });
  }

  root.StpAfPieces = { revision: '1.0', PIECES, RAMPS, MATS, frame, relight, castShadow: (fr, sky, o) => L().castShadow(fr, sky, o), shadeAt: (fr, sky, x, y, z) => L().shadeAt(fr, sky, x, y, z) };
})(typeof globalThis !== 'undefined' ? globalThis : this);
