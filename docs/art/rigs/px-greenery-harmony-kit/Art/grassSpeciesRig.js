/* Art/grassSpeciesRig.js — Hidden Harbours · grass SPECIES (STATIC sprites)
 *
 * Companion to grassTuftRig.js. Same contract, different plants.
 *   · rooted ON the bottom edge, blades grow upward, height = how far they climb
 *   · every lit pixel 8-connected down to the bottom edge, hard alpha, no soil/shadow/outline
 *   · one PNG per variant
 *
 * WHAT'S NEW — two things.
 *
 * 1. PER-SPECIES RAMP, TINTED IN PARALLEL. Each species gets its own five colours, but they
 *    are not invented: they are the base grass ramp with the hue rotated and the saturation
 *    scaled, LIGHTNESS UNTOUCHED. So every species shares one value structure — the thing
 *    that actually carries readability at 32 px — and the runtime lush→straw tint multiplies
 *    over all of them identically. Sedge stays yellower than rush at every point on the knob.
 *
 * 2. BEZIER BLADES. A tuft blade is a line that leans. A sedge blade arches over and comes
 *    back down; saltmeadow hay lies almost flat. So a blade here is a cubic Bezier walked at
 *    sub-pixel steps, and thickness is stamped across the local tangent — horizontally where
 *    the blade is steep, vertically where it has laid over — so a 2 px blade stays 2 px wide
 *    all the way round the arch instead of fattening as it turns.
 */
(function (global) {
  'use strict';

  const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
  const lerp = (a, b, t) => a + (b - a) * t;

  function hash(a, b, c) {
    let h = Math.imul((a | 0) + 374761393, 668265263) ^ Math.imul((b | 0) + 2246822519, 374761393) ^ Math.imul((c | 0) + 3266489917, 2654435761);
    h = Math.imul(h ^ (h >>> 15), 2246822519);
    h ^= h >>> 13;
    return ((h >>> 0) % 100003) / 100003;
  }

  /* ── ramps ─────────────────────────────────────────────────────────────────────────────── */

  const BASE = ['#283a22', '#3a542a', '#567834', '#7ca248', '#aac660'];

  function toHSL(hex) {
    const r = parseInt(hex.slice(1, 3), 16) / 255, g = parseInt(hex.slice(3, 5), 16) / 255, b = parseInt(hex.slice(5, 7), 16) / 255;
    const mx = Math.max(r, g, b), mn = Math.min(r, g, b), d = mx - mn, l = (mx + mn) / 2;
    let h = 0, s = 0;
    if (d > 1e-6) {
      s = d / (1 - Math.abs(2 * l - 1));
      h = mx === r ? ((g - b) / d + (g < b ? 6 : 0)) : mx === g ? (b - r) / d + 2 : (r - g) / d + 4;
      h *= 60;
    }
    return [h, s, l];
  }

  function toHex(h, s, l) {
    h = ((h % 360) + 360) % 360; s = clamp(s, 0, 1); l = clamp(l, 0, 1);
    const c = (1 - Math.abs(2 * l - 1)) * s, x = c * (1 - Math.abs(((h / 60) % 2) - 1)), m = l - c / 2;
    const seg = Math.floor(h / 60) % 6;
    const rgb = [[c, x, 0], [x, c, 0], [0, c, x], [0, x, c], [x, 0, c], [c, 0, x]][seg];
    return '#' + rgb.map(v => Math.round((v + m) * 255).toString(16).padStart(2, '0')).join('');
  }

  const BASE_HSL = BASE.map(toHSL);

  /* hue rotation + saturation scale at FIXED lightness — this is the whole ramp system */
  function ramp(hueShift, satMul) {
    return BASE_HSL.map(([h, s, l]) => toHex(h + hueShift, s * satMul, l));
  }

  const SPECIES = {
    cattail:    { label: 'Cattail',          latin: 'Typha latifolia',      hue: 30,  sat: 0.72, biome: 'freshwater marsh' },
    rush:       { label: 'Soft rush',        latin: 'Juncus effusus',       hue: 38,  sat: 0.88, biome: 'wet edge, ditch' },
    sedge:      { label: 'Tussock sedge',    latin: 'Carex stricta',        hue: -9,  sat: 1.05, biome: 'wet meadow' },
    saltmeadow: { label: 'Saltmeadow hay',   latin: 'Spartina patens',      hue: -24, sat: 0.72, biome: 'salt marsh' },
    timothy:    { label: 'Timothy',          latin: 'Phleum pratense',      hue: 9,   sat: 0.60, biome: 'hay meadow' },
    grass:      { label: 'Meadow grass',     latin: '—',                    hue: 0,   sat: 1.00, biome: 'everywhere' }
  };
  Object.keys(SPECIES).forEach(k => { SPECIES[k].ramp = ramp(SPECIES[k].hue, SPECIES[k].sat); });

  /* the one deliberate off-ramp accent: the cattail spike. Built the SAME way — base ramp,
     hue swung to ochre, saturation cut — so the tint knob still moves it with everything else. */
  const SPIKE = ramp(-72, 0.95);

  /* ── buffer ────────────────────────────────────────────────────────────────────────────── */

  function Buf(w, h) {
    const buf = new Uint8Array(w * h);      // 0 = empty, 1–5 = species ramp, 6–10 = spike ramp
    return {
      w, h, buf,
      put(x, y, v) { if (x < 0 || y < 0 || x >= w || y >= h || v < 1) return; buf[y * w + x] = v | 0; },
      get(x, y) { return (x < 0 || y < 0 || x >= w || y >= h) ? 0 : buf[y * w + x]; }
    };
  }

  function interp(a, u) {
    if (!a || a.length === 1) return a ? a[0] : 1;
    const t = clamp(u, 0, 1) * (a.length - 1);
    const i = Math.min(a.length - 2, Math.floor(t));
    return lerp(a[i], a[i + 1], t - i);
  }

  function bez(p0, p1, p2, p3, u) {
    const m = 1 - u, a = m * m * m, b = 3 * m * m * u, c = 3 * m * u * u, d = u * u * u;
    return [a * p0[0] + b * p1[0] + c * p2[0] + d * p3[0], a * p0[1] + b * p1[1] + c * p2[1] + d * p3[1]];
  }

  /* ── heads ─────────────────────────────────────────────────────────────────────────────── */

  /* Drawn DOWNWARD from a given row so nothing is ever added above the declared silhouette. */

  function cattailSpike(c, x, topY) {          // the brown sausage: 3 wide, 13 tall, capped
    const H = 13;
    for (let d = 0; d < H; d++) {
      const y = topY + d;
      const narrow = d === 0 || d === H - 1;
      c.put(x, y, d < 2 ? 8 : 7);                                   // lit face
      if (!narrow) { c.put(x - 1, y, 6); c.put(x + 1, y, d < 3 ? 8 : 7); }
      if (!narrow && d > 2 && hash(x, y, 91) < 0.22) c.put(x + 1, y, 9);   // a few catchlights
    }
  }

  function timothySpike(c, x, topY) {          // narrow green cylinder, stippled — stays on ramp
    const H = 14;
    for (let d = 0; d < H; d++) {
      const y = topY + d, edge = d < 1 || d > H - 2;
      c.put(x, y, d < 3 ? 5 : 4);
      if (!edge) c.put(x + 1, y, hash(x, y, 17) < 0.4 ? 5 : 3);
      if (!edge && hash(x, y, 23) < 0.3) c.put(x - 1, y, 3);
    }
  }

  function rushSpray(c, x, y, sign) {          // small lateral flower spray, tight to the stem
    for (let i = 0; i < 7; i++) {
      const dx = sign * (1 + Math.round(hash(i, x, 5) * 3));
      const dy = Math.round((hash(i, y, 9) - 0.35) * 5);
      c.put(x + dx, y + dy, i % 3 === 0 ? 5 : 4);
      c.put(x + Math.round(dx / 2), y + Math.round(dy / 2), 3);   // keep it attached
    }
  }

  function sedgeCatkin(c, x, y, sign) {        // small nodding catkin at the arch tip
    for (let d = 0; d < 5; d++) { c.put(x, y + d, d < 2 ? 4 : 3); c.put(x + sign, y + d, 2); }
  }

  /* ── blade ─────────────────────────────────────────────────────────────────────────────── */

  function stroke(c, b, cx, spanW) {
    const baseY = c.h - 1;
    const P0 = [b.rx, baseY];
    const P1 = [b.rx + b.bow, baseY - b.len * 0.50];
    const P2 = [b.rx + (b.tx - b.rx) * 0.55 + b.bow * 0.45, baseY - b.len * 0.98];
    const P3 = [b.tx, baseY - b.len + b.fall];

    const N = Math.max(24, Math.round((b.len + Math.abs(b.tx - b.rx)) * 4));
    const pts = [];
    let arc = 0;
    for (let i = 0; i <= N; i++) {
      const p = bez(P0, P1, P2, P3, i / N);
      if (i) arc += Math.hypot(p[0] - pts[i - 1][0], p[1] - pts[i - 1][1]);
      p[2] = arc;
      pts.push(p);
    }
    const L = arc;
    if (L < 2) return;

    /* value runs from the TIP back down the arc, so a short blade still gets the whole ramp */
    const s = clamp(L / 24, 0.5, 1.7);
    const T = [5.5 * s, 9.5 * s, 13.5 * s, 17.5 * s];
    const shade = hash(b.id, 0, 31) < 0.32 ? -1 : (hash(b.id, 0, 29) < 0.46 ? 1 : 0);

    let prev = null, sprayed = !b.spray;
    for (let i = 0; i <= N; i++) {
      const p = pts[i];
      const xf = p[0] + (hash(b.id, i >> 2, 7) - 0.5) * 0.55;
      const xi = Math.round(xf), yi = Math.round(p[1]);
      if (prev && xi === prev[0] && yi === prev[1]) continue;

      const u = p[2] / L, d = L - p[2];                             // d = distance from the tip
      const idx = (d < T[0] ? 5 : d < T[1] ? 4 : d < T[2] ? 3 : d < T[3] ? 2 : 1) + (d > 2 ? shade : 0);

      const t0 = pts[Math.min(N, i + 1)], t1 = pts[Math.max(0, i - 1)];
      const dx = t0[0] - t1[0], dy = t0[1] - t1[1];
      const steep = Math.abs(dy) >= Math.abs(dx);                   // stamp across the tangent
      const w = Math.max(1, Math.round(lerp(b.t0, b.t1, Math.pow(u, 1.5))));
      const out = steep ? ((xi >= cx) ? 1 : -1) : 1;

      for (let k = 0; k < w; k++) {
        const px = steep ? xi - out * k : xi;
        const py = steep ? yi : yi + k;                             // laid blades shade underneath
        let v = idx - (k > 0 ? 1 : 0);
        /* shadow is a fixed band off the ground, not a fraction of each blade, so a tall stem
           keeps its midtones instead of collapsing into the root mass */
        const centr = 1 - Math.min(1, Math.abs(px - cx) / (spanW * 0.42));
        const dk = centr * clamp(1 - (baseY - py) / 9.5, 0, 1) * 1.7;
        v -= Math.floor(dk) + (hash(px, py, 53) < (dk % 1) ? 1 : 0);
        c.put(px, py, clamp(v, 1, 5));
      }

      if (prev) {                                                   // keep the stroke unbroken
        let ax = prev[0], ay = prev[1];
        while (ax !== xi || ay !== yi) {
          if (ax !== xi) ax += xi > ax ? 1 : -1;
          if (ay !== yi) ay += yi > ay ? 1 : -1;
          c.put(ax, ay, clamp(idx - 1, 1, 5));
        }
      }
      if (!sprayed && u > 0.6) { rushSpray(c, xi, yi, xi >= cx ? 1 : -1); sprayed = true; }
      prev = [xi, yi];
    }

    const tipX = Math.round(P3[0]), tipY = Math.round(P3[1]);
    if (b.head === 'cattail') cattailSpike(c, tipX, tipY + 5);
    if (b.head === 'timothy') timothySpike(c, tipX, tipY);
    if (b.head === 'sedge') sedgeCatkin(c, tipX, tipY, tipX >= cx ? 1 : -1);
  }

  function vnoise(x, s) {
    const i = Math.floor(x), f = x - i, u = f * f * (3 - 2 * f);
    return lerp(hash(i, 0, s), hash(i + 1, 0, s), u);
  }

  /* The root mass is a MOUND, not a slab: height follows how many feet land on each column. */
  function rootMound(c, roots, rows, bias, seed) {
    if (rows <= 0 || !roots.length) return;
    const w = c.w, baseY = c.h - 1, dens = new Float64Array(w);
    for (const r of roots) for (let x = 0; x < w; x++) { const d = (x - r) / 1.7; dens[x] += Math.exp(-d * d); }
    let mx = 0; for (let x = 0; x < w; x++) mx = Math.max(mx, dens[x]);
    if (mx <= 0) return;
    const lo = Math.min(...roots), hi = Math.max(...roots);
    const mid = (lo + hi) / 2, half = Math.max(4, (hi - lo) / 2), thr = rows > 1 ? 0.17 : 0.46;
    for (let x = 0; x < w; x++) {
      let f = dens[x] / mx;
      f *= 1 + (bias || 0) * ((x - mid) / half);
      f *= 0.74 + 0.52 * vnoise(x / 5.5, seed + 5);
      if (f <= thr) continue;
      let hgt = clamp(Math.round(1 + (rows - 1) * Math.pow(clamp(f, 0, 1), 0.75) + (hash(x, 3, seed + 71) - 0.5) * 1.15), 1, rows);
      for (let r = 0; r < hgt; r++) c.put(x, baseY - r, (r === hgt - 1 && hgt > 1 && hash(x, r, 17) < 0.34) ? 2 : 1);
    }
  }

  /* ── render ────────────────────────────────────────────────────────────────────────────── */

  function render(spec) {
    const c = Buf(spec.w, spec.h), baseY = spec.h - 1, seed = spec.seed || 1;
    const blades = [];
    let id = 0;
    for (const g of spec.groups) {
      for (let i = 0; i < g.n; i++) {
        const u = g.n === 1 ? 0.5 : i / (g.n - 1);
        const jit = g.jit == null ? 0.16 : g.jit;
        const len = clamp(Math.round(g.maxLen * interp(g.lenProf, u) * (1 + (hash(i, 1, seed + id) - 0.5) * jit)), 3, g.maxLen);
        blades.push({
          id: seed * 13 + (id++),
          rx: lerp(g.root[0], g.root[1], u) + (hash(i, 2, seed) - 0.5) * (g.rootJit == null ? 1.1 : g.rootJit),
          tx: lerp(g.tip[0], g.tip[1], u) + (hash(i, 3, seed) - 0.5) * (g.tipJit == null ? 2.0 : g.tipJit),
          len,
          fall: lerp(g.fall[0], g.fall[1], u) * (0.8 + 0.4 * hash(i, 4, seed)),
          bow: lerp(g.bow[0], g.bow[1], u),
          t0: g.thick[0], t1: g.thick[1],
          head: g.head, spray: g.spray
        });
      }
    }
    const roots = blades.map(b => b.rx);
    const cx = (Math.min(...roots) + Math.max(...roots)) / 2;
    const spanW = Math.max(6, Math.max(...blades.map(b => b.tx)) - Math.min(...blades.map(b => b.tx)));
    blades.sort((a, b) => (b.len + b.fall) - (a.len + a.fall));    // tall behind, short in front
    for (const b of blades) stroke(c, b, cx, spanW);
    rootMound(c, roots, spec.rootRows == null ? 3 : spec.rootRows, spec.baseBias || 0, seed);
    return c;
  }

  /* ── the set ───────────────────────────────────────────────────────────────────────────── */
  /* stiff = how much of the shader's bend this sprite should take (1 = a normal tuft). A
     cattail stem is a rigid pole; hay is already laid over and shouldn't flap. One number,
     same shader, no opt-outs. */

  const VARIANTS = [
    { name: 'Cattail', sp: 'cattail', cls: 'emergent', note: 'four sword leaves, one spike', w: 32, h: 64, seed: 11, rootRows: 3, stiff: 0.35,
      groups: [
        { n: 5, root: [12, 20], tip: [4, 28], maxLen: 54, lenProf: [0.74, 0.95, 1, 0.9, 0.7], fall: [3, 8], bow: [-3, 4], thick: [3, 1] },
        { n: 1, root: [16, 16], tip: [17, 17], maxLen: 57, lenProf: [1], fall: [0, 0], bow: [1, 1], thick: [2, 1], head: 'cattail', jit: 0 }
      ] },
    { name: 'CattailPair', sp: 'cattail', cls: 'emergent', note: 'two spikes, leaves raked right', w: 32, h: 64, seed: 23, rootRows: 3, baseBias: 0.25, stiff: 0.35,
      groups: [
        { n: 5, root: [11, 21], tip: [7, 30], maxLen: 50, lenProf: [0.7, 0.92, 1, 0.86, 0.66], fall: [4, 10], bow: [0, 6], thick: [3, 1] },
        { n: 2, root: [14, 19], tip: [12, 21], maxLen: 56, lenProf: [1, 0.87], fall: [0, 0], bow: [-1, 2], thick: [2, 1], head: 'cattail', jit: 0.05 }
      ] },

    { name: 'Rush', sp: 'rush', cls: 'rush', note: 'stiff leafless spray', w: 32, h: 48, seed: 41, rootRows: 3, stiff: 0.5,
      groups: [
        { n: 17, root: [13, 19], tip: [4, 28], maxLen: 42, lenProf: [0.66, 0.88, 1, 0.94, 0.82, 0.62], fall: [1, 3], bow: [-2, 2], thick: [1, 1], rootJit: 0.8 },
        { n: 2, root: [15, 17], tip: [11, 21], maxLen: 38, lenProf: [1, 0.9], fall: [1, 1], bow: [-1, 1], thick: [1, 1], spray: true }
      ] },
    { name: 'RushLean', sp: 'rush', cls: 'rush', note: 'whole spray raked, shorter', w: 32, h: 48, seed: 53, rootRows: 3, baseBias: 0.3, stiff: 0.5,
      groups: [
        { n: 15, root: [12, 18], tip: [7, 30], maxLen: 36, lenProf: [0.7, 0.9, 1, 0.9, 0.7], fall: [2, 5], bow: [1, 5], thick: [1, 1], rootJit: 0.8 },
        { n: 2, root: [14, 17], tip: [16, 24], maxLen: 33, lenProf: [1, 0.88], fall: [2, 2], bow: [2, 3], thick: [1, 1], spray: true }
      ] },

    { name: 'Sedge', sp: 'sedge', cls: 'tussock', note: 'fountain, blades arch over', w: 32, h: 48, seed: 71, rootRows: 5, stiff: 0.85,
      groups: [
        { n: 13, root: [12, 20], tip: [0, 32], maxLen: 45, lenProf: [0.52, 0.8, 1, 0.86, 0.66, 0.5], fall: [9, 16], bow: [-5, 5], thick: [2, 1], jit: 0.26 },
        { n: 2, root: [15, 17], tip: [5, 27], maxLen: 43, lenProf: [1, 0.92], fall: [13, 13], bow: [-4, 4], thick: [1, 1], head: 'sedge' }
      ] },
    { name: 'SedgeLow', sp: 'sedge', cls: 'tussock', note: '32×32 — same fountain, half height', w: 32, h: 32, seed: 83, rootRows: 4, stiff: 0.85,
      groups: [
        { n: 13, root: [12, 20], tip: [2, 30], maxLen: 26, lenProf: [0.62, 0.88, 1, 0.9, 0.7, 0.56], fall: [7, 13], bow: [-4, 4], thick: [2, 1] }
      ] },

    { name: 'Saltmeadow', sp: 'saltmeadow', cls: 'mat', note: '64×32 — the cowlick, two whorls', w: 64, h: 32, seed: 101, rootRows: 1, stiff: 0.3,
      groups: [
        { n: 9, root: [10, 28], tip: [-12, 12], maxLen: 21, lenProf: [0.7, 1, 0.86, 0.66], fall: [13, 18], bow: [-14, -8], thick: [2, 1] },
        { n: 9, root: [36, 54], tip: [52, 76], maxLen: 21, lenProf: [0.68, 0.9, 1, 0.72], fall: [13, 18], bow: [8, 14], thick: [2, 1] },
        { n: 6, root: [24, 40], tip: [14, 50], maxLen: 24, lenProf: [0.82, 1, 0.82], fall: [9, 14], bow: [-5, 5], thick: [2, 1] }
      ] },
    { name: 'SaltmeadowSwirl', sp: 'saltmeadow', cls: 'mat', note: '64×32 — one whorl, all combed left', w: 64, h: 32, seed: 113, rootRows: 1, baseBias: -0.25, stiff: 0.3,
      groups: [
        { n: 15, root: [8, 58], tip: [-14, 34], maxLen: 20, lenProf: [0.66, 0.9, 1, 0.84, 0.96, 0.7], fall: [12, 17], bow: [-15, -9], thick: [2, 1] },
        { n: 7, root: [20, 54], tip: [4, 40], maxLen: 25, lenProf: [0.7, 1, 0.78, 0.9], fall: [10, 15], bow: [-9, -4], thick: [2, 1] }
      ] },

    { name: 'Timothy', sp: 'timothy', cls: 'culm', note: 'bare culms, cylindrical heads', w: 32, h: 48, seed: 131, rootRows: 3, stiff: 0.6,
      groups: [
        { n: 8, root: [11, 21], tip: [4, 28], maxLen: 19, lenProf: [0.66, 0.9, 1, 0.86, 0.64], fall: [3, 7], bow: [-4, 4], thick: [2, 1] },
        { n: 2, root: [14, 19], tip: [12, 22], maxLen: 44, lenProf: [1, 0.86], fall: [0, 1], bow: [-1, 2], thick: [1, 1], head: 'timothy', jit: 0.04 }
      ] },
    { name: 'TimothyLean', sp: 'timothy', cls: 'culm', note: 'three culms nodding downwind', w: 32, h: 48, seed: 149, rootRows: 3, baseBias: 0.3, stiff: 0.6,
      groups: [
        { n: 8, root: [10, 20], tip: [6, 30], maxLen: 17, lenProf: [0.7, 0.94, 1, 0.8, 0.62], fall: [3, 8], bow: [0, 6], thick: [2, 1] },
        { n: 3, root: [13, 19], tip: [18, 27], maxLen: 40, lenProf: [1, 0.9, 0.78], fall: [1, 3], bow: [2, 5], thick: [1, 1], head: 'timothy', jit: 0.05 }
      ] }
  ];

  /* ── out ───────────────────────────────────────────────────────────────────────────────── */

  function toRGBA(c, spKey) {
    const cols = (SPECIES[spKey] || SPECIES.grass).ramp.concat(SPIKE)
      .map(h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)]);
    const out = new Uint8ClampedArray(c.w * c.h * 4);
    for (let i = 0; i < c.buf.length; i++) {
      const v = c.buf[i];
      if (!v) continue;                                            // hard alpha: 0 or 255
      const col = cols[Math.min(v, 10) - 1];
      out[i * 4] = col[0]; out[i * 4 + 1] = col[1]; out[i * 4 + 2] = col[2]; out[i * 4 + 3] = 255;
    }
    return out;
  }

  function validate(c, spec) {
    const w = c.w, h = c.h, seen = new Uint8Array(w * h), stack = [];
    let lit = 0, rooted = 0, topRow = h;
    for (let x = 0; x < w; x++) if (c.get(x, h - 1)) { stack.push(x + (h - 1) * w); seen[x + (h - 1) * w] = 1; rooted++; }
    for (let i = 0; i < c.buf.length; i++) if (c.buf[i]) { lit++; const y = (i / w) | 0; if (y < topRow) topRow = y; }
    let reached = 0;
    while (stack.length) {
      const p = stack.pop(); reached++;
      const px = p % w, py = (p / w) | 0;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
        const nx = px + dx, ny = py + dy;
        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
        const n = nx + ny * w;
        if (seen[n] || !c.buf[n]) continue;
        seen[n] = 1; stack.push(n);
      }
    }
    let off = 0;
    for (let i = 0; i < c.buf.length; i++) if (c.buf[i] > 5) off++;
    return { name: spec.name, lit, rooted, detached: lit - reached, topRow, climb: h - topRow, offRamp: off };
  }

  global.GrassSpeciesRig = { SPECIES, SPIKE, ramp, VARIANTS, render, toRGBA, validate, hash };
})(typeof window !== 'undefined' ? window : globalThis);
