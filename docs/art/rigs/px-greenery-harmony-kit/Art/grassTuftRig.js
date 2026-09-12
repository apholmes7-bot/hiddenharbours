/* Art/grassTuftRig.js — Hidden Harbours · grass tuft varieties (STATIC sprites)
 *
 * These sprites are bent at runtime by the wind/footstep vertex shader, so the art
 * obeys the shader's contract:
 *   · every blade is rooted ON the bottom edge of the canvas and grows upward
 *   · a variant's HEIGHT is how far its blades climb (bend ramps 0 at bottom → max at top)
 *   · every lit pixel is 8-connected down to the bottom edge — nothing detaches when sheared
 *   · hard alpha only, and strictly the five-colour blade ramp (a runtime tint knob slides
 *     whole fields lush → straw, and the shared ramp is what keeps that coherent)
 *   · no soil, no shadow, no outline pixels — the terrain layer underneath is the ground
 *
 * Reference: Art/Sprites/GrassTuft.png (the shipping tuft) — 1 px blade strokes, value
 * running dark at the root to light at the tip, tuft interior a step darker than its edge,
 * and a solid dark root mass in the bottom rows where the feet merge.
 */
(function (global) {
  'use strict';

  const RAMP = ['#283a22', '#3a542a', '#567834', '#7ca248', '#aac660'];
  const RGB = RAMP.map(h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)]);
  const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);

  function hash(a, b, c) {
    let h = Math.imul((a | 0) + 374761393, 668265263) ^ Math.imul((b | 0) + 2246822519, 374761393) ^ Math.imul((c | 0) + 3266489917, 2654435761);
    h = Math.imul(h ^ (h >>> 15), 2246822519);
    h ^= h >>> 13;
    return ((h >>> 0) % 100003) / 100003;
  }

  function Buf(w, h) {
    const buf = new Uint8Array(w * h);
    return {
      w: w, h: h, buf: buf,
      put(x, y, v) { if (x < 0 || y < 0 || x >= w || y >= h || v < 1) return; buf[y * w + x] = clamp(v | 0, 1, 5); },
      get(x, y) { return (x < 0 || y < 0 || x >= w || y >= h) ? 0 : buf[y * w + x]; }
    };
  }

  /* a seed head: a small club thickening the top of a stalk. Drawn DOWNWARD from the tip so
     it never adds pixels above the declared silhouette height. */
  function drawHead(c, x, tipY, outSign) {
    const prof = [[0, 5], [1, 5], [2, 5], [3, 4], [4, 4], [5, 3]];
    for (let i = 0; i < prof.length; i++) {
      const dy = prof[i][0], v = prof[i][1];
      c.put(x, tipY + dy, v);
      if (dy > 0 && dy < 5) c.put(x - outSign, tipY + dy, clamp(v - 1, 1, 5));
    }
  }

  function drawBlade(c, b, cx, spanW) {
    const baseY = b.baseY, tipY = b.tipY, L = baseY - tipY;
    if (L < 1) return;
    /* value runs from the TIP down, not from the canvas top: a short blade still gets the
       whole ramp, just compressed. s calibrates the reference tuft (L≈27) to its own steps. */
    const s = clamp(L / 24, 0.5, 1.6);
    const T = [5.5 * s, 9.5 * s, 13.5 * s, 17.5 * s];
    const curve = b.curve || 2.1;
    const thick = b.thick == null ? 2 : b.thick;
    const outSign = (b.rootX >= cx) ? 1 : -1;
    /* every blade sits a step lighter or darker than its neighbours, so a packed tuft still
       reads blade-by-blade instead of as one smooth mass */
    const shade = hash(b.id, 0, 31) < 0.34 ? -1 : (hash(b.id, 0, 29) < 0.48 ? 1 : 0);
    let prev = null;
    for (let y = baseY; y >= tipY; y--) {
      const t = (baseY - y) / L;                                  // 0 at the root, 1 at the tip
      let xf = b.rootX + (b.tipX - b.rootX) * Math.pow(t, curve);  // lean lands near the tip, root stays put
      xf += (hash(b.id, y, 7) - 0.5) * 0.7;
      const xi = Math.round(xf);
      const d = y - tipY;
      const idx = (d < T[0] ? 5 : d < T[1] ? 4 : d < T[2] ? 3 : d < T[3] ? 2 : 1) + (d > 2 ? shade : 0);
      let w = 1;
      if (thick >= 2 && t < 0.32) w = 2;
      if (thick >= 3 && t < 0.18) w = 3;
      for (let k = 0; k < w; k++) {
        const px = xi - outSign * k;                 // k=0 is the outward-facing (lit) column
        let v = idx - (k > 0 ? 1 : 0);
        /* shadow is a band a fixed height off the ground, not a fraction of each blade — so a
           tall blade keeps its midtones instead of collapsing into the base like a trunk */
        const centr = 1 - Math.min(1, Math.abs(px - cx) / (spanW * 0.40));
        const dk = centr * clamp(1 - (baseY - y) / 9.5, 0, 1) * 1.7;
        v -= Math.floor(dk) + (hash(px, y, 53) < (dk % 1) ? 1 : 0);   // dithered, never a hard step
        c.put(px, y, clamp(v, 1, 5));
      }
      if (prev !== null && Math.abs(xi - prev) > 1) {           // keep the stroke unbroken
        const st = xi < prev ? 1 : -1;
        for (let fx = xi + st; fx !== prev; fx += st) c.put(fx, y, clamp(idx - 1, 1, 5));
      }
      prev = xi;
    }
    if (b.head) drawHead(c, Math.round(b.rootX + (b.tipX - b.rootX)), tipY, outSign);
  }

  function vnoise(x, s) {
    const i = Math.floor(x), f = x - i, u = f * f * (3 - 2 * f);
    return hash(i, 0, s) * (1 - u) + hash(i + 1, 0, s) * u;
  }

  /* The root mass is a MOUND, not a slab: its height follows how many feet land on each
     column, tilts off-centre with baseBias, and carries a low-frequency lump so a wide clump
     doesn't bottom out flat. Blades are already drawn down to the bottom row, so the mound
     can thin to nothing at the flanks without lifting the tuft off the edge. */
  function rootMound(c, roots, rows, bias, seed) {
    if (rows <= 0) return;
    const w = c.w, baseY = c.h - 1, dens = new Float64Array(w);
    for (let i = 0; i < roots.length; i++) {
      const r = roots[i];
      for (let x = 0; x < w; x++) { const d = (x - r) / 1.7; dens[x] += Math.exp(-d * d); }
    }
    let mx = 0;
    for (let x = 0; x < w; x++) if (dens[x] > mx) mx = dens[x];
    if (mx <= 0) return;
    let lo = Infinity, hi = -Infinity;
    for (let i = 0; i < roots.length; i++) { lo = Math.min(lo, roots[i]); hi = Math.max(hi, roots[i]); }
    const mid = (lo + hi) / 2, half = Math.max(4, (hi - lo) / 2);
    const thr = rows > 1 ? 0.17 : 0.46;
    for (let x = 0; x < w; x++) {
      let f = dens[x] / mx;
      f *= 1 + (bias || 0) * ((x - mid) / half);
      f *= 0.74 + 0.52 * vnoise(x / 5.5, seed + 5);
      if (f <= thr) continue;
      let hgt = Math.round(1 + (rows - 1) * Math.pow(clamp(f, 0, 1), 0.75) + (hash(x, 3, seed + 71) - 0.5) * 1.15);
      hgt = clamp(hgt, 1, rows);
      for (let r = 0; r < hgt; r++)
        c.put(x, baseY - r, (r === hgt - 1 && hgt > 1 && hash(x, r, 17) < 0.34) ? 2 : 1);
    }
  }

  function interp(a, u) {
    if (a.length === 1) return a[0];
    const t = clamp(u, 0, 1) * (a.length - 1);
    const i = Math.min(a.length - 2, Math.floor(t)), f = t - i;
    return a[i] * (1 - f) + a[i + 1] * f;
  }

  /* a variant is a blade COUNT, a root span and a length profile across that span — the profile
     is the silhouette. Roots pack at ~1.5 px so blades merge into a body near the ground and
     separate into strokes near the tips, the way the shipping tuft does. */
  function expand(spec) {
    const n = spec.n, seed = spec.seed, jit = spec.jit == null ? 0.30 : spec.jit;
    const heads = (spec.heads || []).map(hp => Math.round(hp * (n - 1)));
    const k = spec.baseK == null ? 0.55 : spec.baseK;
    const cx = (spec.span[0] + spec.span[1]) / 2;
    const root = [], tip = [], len = [];
    for (let i = 0; i < n; i++) {
      const u = n === 1 ? 0.5 : i / (n - 1);
      let f = interp(spec.prof, u) * (1 + (hash(i, 1, seed) - 0.5) * jit);
      if (heads.indexOf(i) >= 0) f = Math.max(f, 0.93);
      const spread = spec.span[0] + (spec.span[1] - spec.span[0]) * u + (hash(i, 2, seed) - 0.5) * 1.2;
      const lean = spec.leanL + (spec.leanR - spec.leanL) * u + (hash(i, 3, seed) - 0.5) * 2.0;
      /* the FOOT converges toward the tuft centre while the tip keeps the full spread, so the
         tuft fans upward off a narrow base instead of standing on a slab as wide as its crown */
      root.push(cx + (spread - cx) * k);
      tip.push(spread + lean);
      len.push(clamp(Math.round(spec.maxLen * Math.min(1, f)), 3, spec.maxLen));
    }
    return { root, tip, len, headIdx: heads };
  }

  function render(spec) {
    const c = Buf(spec.w, spec.h);
    const baseY = spec.h - 1;
    const ex = expand(spec);
    const roots = ex.root;
    let lo = Infinity, hi = -Infinity;
    for (let i = 0; i < roots.length; i++) { lo = Math.min(lo, roots[i]); hi = Math.max(hi, roots[i]); }
    const cx = (spec.span[0] + spec.span[1]) / 2, spanW = Math.max(6, spec.span[1] - spec.span[0]);
    const blades = roots.map((rx, i) => ({
      id: (spec.seed || 1) * 13 + i,
      rootX: rx, tipX: ex.tip[i], tipY: baseY - ex.len[i], baseY,
      curve: spec.curve, thick: spec.thick, len: ex.len[i],
      head: ex.headIdx.indexOf(i) >= 0
    }));
    blades.sort((a, b) => b.len - a.len);       // tall blades behind, short front blades over them
    for (let i = 0; i < blades.length; i++) drawBlade(c, blades[i], cx, spanW);
    rootMound(c, roots, spec.rootRows == null ? 3 : spec.rootRows, spec.baseBias || 0, spec.seed || 1);
    return c;
  }

  /* ── the set ──────────────────────────────────────────────────────────────────────────────
     Footprint and density are held CONSTANT within a height class — grass is uniform stuff.
     What varies is where the crest sits, which way the tuft leans, and where it parts. maxLen
     = how far the longest blade climbs. short 15 · medium 22 · tall 29 · fringe 8–11 · clump
     ≤16 (low and rounded: it bends as ONE unit) · marram 42–45. baseBias tilts the root mound. */
  const VARIANTS = [
    { name: 'Short2', cls: 'short', note: 'even fan, crest centred', w: 32, h: 32, seed: 11, rootRows: 3, thick: 2, curve: 2.0, jit: 0.18,
      n: 11, span: [8, 24], maxLen: 15, baseK: 0.85, prof: [0.68, 0.92, 1, 0.8, 0.96, 0.7], leanL: -5, leanR: 6 },
    { name: 'Short3', cls: 'short', note: 'soft parting down the middle', w: 32, h: 32, seed: 23, rootRows: 3, thick: 2, curve: 2.1, jit: 0.18,
      n: 11, span: [8, 24], maxLen: 15, baseK: 0.85, prof: [0.9, 1, 0.66, 0.5, 0.72, 1, 0.86], leanL: -5, leanR: 5, baseBias: -0.2 },
    { name: 'Short4', cls: 'short', note: 'combed right, crest off-centre', w: 32, h: 32, seed: 37, rootRows: 3, thick: 2, curve: 2.15, jit: 0.18,
      n: 11, span: [8, 24], maxLen: 15, baseK: 0.85, prof: [0.6, 0.78, 0.94, 1, 0.9, 0.72], leanL: 1, leanR: 7, baseBias: 0.35 },

    { name: 'Medium2', cls: 'medium', note: 'dome, crest centred', w: 32, h: 32, seed: 51, rootRows: 3, thick: 2, curve: 2.45, jit: 0.18,
      n: 12, span: [8, 24], maxLen: 22, baseK: 0.85, prof: [0.6, 0.84, 1, 0.88, 0.74, 0.58], leanL: -5, leanR: 5 },
    { name: 'Medium3', cls: 'medium', note: 'wind-combed left', w: 32, h: 32, seed: 67, rootRows: 3, thick: 2, curve: 2.2, jit: 0.18,
      n: 12, span: [8, 24], maxLen: 22, baseK: 0.85, prof: [0.62, 0.86, 1, 0.92, 0.76, 0.6], leanL: -2, leanR: -8, baseBias: -0.3 },
    { name: 'Medium4', cls: 'medium', note: 'parted, tall on both flanks', w: 32, h: 32, seed: 79, rootRows: 3, thick: 2, curve: 2.1, jit: 0.18,
      n: 12, span: [8, 24], maxLen: 22, baseK: 0.85, prof: [0.95, 1, 0.7, 0.56, 0.74, 1, 0.9], leanL: -5, leanR: 5 },

    { name: 'Tall2', cls: 'tall', note: 'twin leaders over a full body', w: 32, h: 32, seed: 91, rootRows: 3, thick: 2, curve: 2.45, jit: 0.18,
      n: 12, span: [9, 23], maxLen: 29, baseK: 0.82, prof: [0.5, 0.72, 1, 0.74, 0.68, 0.97, 0.7, 0.52], leanL: -5, leanR: 6 },
    { name: 'Tall3', cls: 'tall', note: 'whole tuft raked right', w: 32, h: 32, seed: 103, rootRows: 3, thick: 2, curve: 2.3, jit: 0.18,
      n: 12, span: [9, 23], maxLen: 29, baseK: 0.82, prof: [0.55, 0.78, 0.96, 1, 0.88, 0.68], leanL: 2, leanR: 8, baseBias: 0.3 },

    { name: 'Clump', cls: 'clump', note: 'broad low mound, three soft crests', w: 64, h: 32, seed: 131, rootRows: 4, thick: 2, curve: 2.0, jit: 0.18,
      n: 24, span: [4, 59], maxLen: 16, baseK: 0.95, prof: [0.5, 0.78, 0.97, 0.84, 1, 0.86, 0.96, 0.76, 0.52], leanL: -7, leanR: 7 },
    { name: 'ClumpBroad', cls: 'clump', note: 'flatter, crest right of centre', w: 64, h: 32, seed: 149, rootRows: 4, thick: 2, curve: 2.1, jit: 0.18,
      n: 24, span: [3, 60], maxLen: 16, baseK: 0.95, prof: [0.46, 0.72, 0.9, 1, 0.8, 0.66, 0.86, 1, 0.88, 0.62, 0.44], leanL: -8, leanR: 8, baseBias: 0.2 },

    { name: 'Fringe', cls: 'fringe', note: 'six thin blades — feathers a grass/ground boundary', w: 32, h: 32, seed: 163, rootRows: 1, thick: 1, curve: 2.2, jit: 0.16,
      n: 6, span: [11, 22], maxLen: 11, baseK: 0.9, prof: [0.7, 1, 0.82, 0.6], leanL: -3, leanR: 4 },
    { name: 'Fringe2', cls: 'fringe', note: 'five blades, one leader, sits left', w: 32, h: 32, seed: 179, rootRows: 1, thick: 1, curve: 2.0, jit: 0.16,
      n: 5, span: [9, 19], maxLen: 11, baseK: 0.9, prof: [1, 0.72, 0.52], leanL: -4, leanR: 3 },
    { name: 'Fringe3', cls: 'fringe', note: 'seven stubs, wide spaced — the thinnest step', w: 32, h: 32, seed: 191, rootRows: 1, thick: 1, curve: 2.1, jit: 0.16,
      n: 7, span: [6, 26], maxLen: 8, baseK: 0.92, prof: [0.6, 0.85, 0.55, 0.78, 0.6], leanL: -3, leanR: 4 },

    { name: 'Marram', cls: 'marram', note: 'dune marram — tall, wispy, splayed', w: 32, h: 48, seed: 211, rootRows: 4, thick: 1, curve: 2.45, jit: 0.18,
      n: 11, span: [10, 22], maxLen: 45, baseK: 0.78, prof: [0.62, 0.86, 1, 0.9, 0.74, 0.58], leanL: -9, leanR: 9 },
    { name: 'MarramLean', cls: 'marram', note: 'dune marram raked by the onshore wind', w: 32, h: 48, seed: 227, rootRows: 4, thick: 1, curve: 2.2, jit: 0.18,
      n: 11, span: [10, 22], maxLen: 45, baseK: 0.78, prof: [0.56, 0.82, 1, 0.88, 0.72, 0.56], leanL: 4, leanR: 13, baseBias: 0.3 },

    { name: 'SeedHead', cls: 'medium', note: 'late season — three seeding stalks over a tuft', w: 32, h: 32, seed: 241, rootRows: 3, thick: 2, curve: 2.1, jit: 0.18,
      n: 12, span: [8, 24], maxLen: 25, baseK: 0.85, prof: [0.6, 0.84, 1, 0.78, 0.95, 0.6], leanL: -5, leanR: 6, heads: [0.32, 0.6, 0.82] },
    { name: 'SeedHeadTall', cls: 'marram', note: 'late season, nodding stalks', w: 32, h: 48, seed: 257, rootRows: 4, thick: 1, curve: 2.8, jit: 0.18,
      n: 13, span: [9, 23], maxLen: 42, baseK: 0.78, prof: [0.56, 0.8, 1, 0.74, 0.94, 0.56], leanL: -7, leanR: 8, heads: [0.3, 0.58, 0.82] }
  ];

  function toRGBA(c) {
    const out = new Uint8ClampedArray(c.w * c.h * 4);
    for (let i = 0; i < c.buf.length; i++) {
      const v = c.buf[i];
      if (!v) continue;                       // hard alpha: 0 or 255, never between
      const col = RGB[v - 1];
      out[i * 4] = col[0]; out[i * 4 + 1] = col[1]; out[i * 4 + 2] = col[2]; out[i * 4 + 3] = 255;
    }
    return out;
  }

  /* the shader's preconditions, asserted */
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
    return { name: spec.name, lit, rooted, detached: lit - reached, topRow, climb: h - topRow, coverage: +(lit / (w * h)).toFixed(3) };
  }

  global.GrassTuftRig = { RAMP, RGB, VARIANTS, render, toRGBA, validate, hash };
})(typeof window !== 'undefined' ? window : globalThis);
