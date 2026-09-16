/* Hidden Harbours — PIXEL CLIFF FACE RIG.  The big tileable cliff FACE textures re-authored in the
   pixel language (Art/pixelLanguage.js), so a 9 m wall reads like the ground at its foot and the
   trees on its brow: hue-true bands cut from one rock colour, cold shadow / warm key, no dither, no
   outline, everything on a 2 px texel — the same texel as the ground materials.

   The one thing that does NOT change is the geometry. Everything v10 added to Art/cliffRig.js is
   still here and still honest:

     FORM      ribs on an alternating lattice, clefts gated into the re-entrants, benches periodic
               in t. Same field, same seed, same metres.
     BATTER    'wall' 90° · 'steep' 76° · 'ramp' 62° · 'bank' 48°. The key is rotated into the
               tipped frame, bedding spacing goes as 1/sin, weathering follows.
     SHADOW    the horizon march along the key over the form field — the biggest depth cue there is.
     profile() the same displacement in METRES, so the wall still bends and the silhouette, the
               baked shadow and the normal map remain the same shape.

   What changes is how the form is DRAWN. A gradient is not pixel art, so the form does not shade
   continuously — it is quantised into five TIERS, each a whole palette cut from the rock's own hue
   (dark tiers toward the cold ambient, light tiers toward the warm key), and the tier boundary is
   torn by a fine field and biased per bed so it steps at a bed contact instead of drifting through
   one. A buttress is therefore a few big flat shapes with hard edges between them, which is how a
   pixel artist draws a buttress, and the cast shadow is a hard-edged region, which is how a pixel
   artist draws a shadow. The five tiers × five bands are one hue-true ramp: ~12 colours land in a
   typical sheet.

   Aspect is the pixel pack's aspect, not a second sun: one band of incidence (W +1 · SW/S 0 ·
   SE/E −1), the windward dust and the lee moss/seep, and the `read` gain that thickens joints and
   lips on the oblique faces. All directional shading — tiers, key banding, cast shadow — is the
   pixel language's own upper-left key, exactly as the ground and the sprites are lit.

     PxCliffFace.face(rock, aspect, step, {slope, W, H, seed})  -> b
     PxCliffFace.channels(b) -> {'', _unlit, _mask, _normal}  RGBA W×H
     PxCliffFace.profile(rock, aspect, {slope})                -> {disp, W, H, metres}
     PxCliffFace.brow(aspect, step, {slope})                   -> RGBA 384×128 decal
     PxCliffFace.toe(aspect, step, {feature})                  -> RGBA 384×128 decal
   Runs in the run_script bake sandbox and in the browser. */
(function (root) {
  'use strict';
  const P = root.PxLang; if (!P) throw new Error('Art/pixelLanguage.js must be loaded first');
  const { Tile, stamp, sites, clean, hash2, fbmP, vnP, stencil, ellipse, clamp } = P;
  const FL = Math.floor, ABS = Math.abs;
  const lerp = (a, b, t) => a + (b - a) * t;
  const sm = t => t * t * (3 - 2 * t);
  const ss = (a, b, x) => { const t = clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };
  const pk = (arr, st) => arr[clamp(st, 0, 2)];
  const pv = (u, v, cx, cy, s) => vnP(u, v, Math.max(1, Math.round(cx)), Math.max(1, Math.round(cy)), s);
  const fb = (u, v, cx, cy, s, oct) => fbmP(u, v, Math.max(1, Math.round(cx)), Math.max(1, Math.round(cy)), s, oct || 3);

  const PPU = 32;                 /* px per metre */
  const RELIEF_M = 1.15;          /* plan depth of the full form range, metres — as cliffRig */
  const SLOPES = { wall: 90, steep: 76, ramp: 62, bank: 48 };
  const ROCKS = ['sandstone', 'till', 'basalt'];
  const ASPECTS = ['W', 'SW', 'S', 'SE', 'E'];

  /* the pixel pack's aspect table: one band of incidence, the weathering, and the read gain.
     No second light — the key is the pixel language's own. */
  const ASPECT = {
    W:  { shift: 1, dust: .30, lee: 0, read: 1.30, xan: .85, cool: .08 },
    SW: { shift: 1, dust: .18, lee: 0, read: 1.08, xan: 1.00, cool: .16 },
    S:  { shift: 0, dust: .06, lee: .2, read: 1.00, xan: .70, cool: .30 },
    SE: { shift: -1, dust: 0, lee: .6, read: 1.10, xan: .22, cool: .66 },
    E:  { shift: -1, dust: 0, lee: 1, read: 1.32, xan: .06, cool: .84 },
  };

  /* ---------------------------------------------------------------------------------- the rocks */
  const DEF = {
    sandstone: { base: '#a6583b', c: 1.0, thin: [2, 3], massive: [4, 8], jsp: [17, 13, 9, 6], silt: .26,
      note: 'The red bed. Massive units and laminated packets, one periodic sequence down the tile; a massive bed\'s top row is a lit lip and the packet under it lies in its shade; joints wander and are gated along their length; spalls stand proud and sockets are plucked, both sparse; pale siltstone beds broken along s; honeycomb and open joints up the ladder.' },
    till: { base: '#8a5a3e', c: .85,
      note: 'Red boulder clay — the dirt material stood on end. Rill gullies at two calibres, each a dark trace with its lit right wall, gated along their length; slump benches with a lit tread and the shade under it, dying out along s; clasts as lit lumps; a dried pale crust on the interfluves; turf mats sliding down the face at Hi.' },
    basalt: { base: '#5a6169', c: 1.0,
      note: 'Grey colonnade. Columns ~0.45 m, each carrying its own tone (identity on the column, never the segment), segment cracks at their own heights, a rubble entablature under the brow with a wandering boundary, and orange Xanthoria on the sunlit W/SW faces only — the aspect tell that does the work on a rock with no hue to spare.' },
  };

  /* ------------------------------------------------------------------------------------- batter */
  const SL = { deg: 90, th: 0, tf: 0, sin: 1, cos: 0 };
  function setSlope(v) {
    let d = v == null ? 90 : (typeof v === 'string' ? (SLOPES[v] || 90) : v);
    d = clamp(d, 30, 90);
    const rad = d * Math.PI / 180, th = (90 - d) * Math.PI / 180;
    SL.deg = d; SL.th = th; SL.tf = clamp(th / (Math.PI / 4), 0, 1.5);
    SL.sin = Math.sin(rad); SL.cos = Math.cos(rad);
    return SL;
  }
  /* the key, rotated into the tipped frame — a bank really does catch a high sun that a wall misses */
  function keyFor() {
    const K = P.LIGHT.key, ct = Math.cos(SL.th), st = Math.sin(SL.th);
    return [K[0], K[1] * ct + K[2] * st, -K[1] * st + K[2] * ct];
  }

  /* --------------------------------------------------------------------------------------- form
     Ported from Art/cliffRig.js unchanged in shape: an alternating rib lattice (value noise does
     not span its own range and comes out as a constant offset — that was the v9 bug), clefts gated
     into the re-entrants only, and three benches periodic in t, each dying out along its length.

     PARITY, and why the form does not run on the pixel language's noise.
     `profile()` is a geometry CONTRACT with the v10 kit: same field, same seed, same metres, so a
     coast can be re-skinned without moving a collider. That contract is not kept by copying the
     parameters — it is kept by computing the same numbers. PxLang's hash2 mixes the seed with a
     different constant (1442695041) than cliffRig's ih (1274126177), so a parameter-perfect port
     onto hash2 still lands every rib, cleft and bench somewhere else: the same rules, a different
     wall, and each rock's seed pointing at another rock's field. So the form field — joints(),
     form(), and nothing else — runs on cliffRig's own ih and pv, verbatim. Everything BELOW the
     form (beds, sockets, columns, lichen, tiers, bands) stays on the pixel language, which is the
     half that is meant to look different. */
  function ih(i, j, s) {
    let h = Math.imul(i | 0, 374761393) + Math.imul(j | 0, 668265263) + Math.imul(s | 0, 1274126177);
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
  }
  function pvF(u, v, cx, cy, s) {
    const x = u * cx, y = v * cy, xi = FL(x), yi = FL(y), xf = x - xi, yf = y - yi;
    const a0 = ((xi % cx) + cx) % cx, b0 = ((yi % cy) + cy) % cy;
    const a1 = (a0 + 1) % cx, b1 = (b0 + 1) % cy, su = sm(xf), sv = sm(yf);
    const p00 = ih(a0, b0, s), p10 = ih(a1, b0, s), p01 = ih(a0, b1, s), p11 = ih(a1, b1, s);
    return lerp(lerp(p00, p10, su), lerp(p01, p11, su), sv);
  }
  const JT = { d: 0, id: 0, side: 0, open: 0 };
  function joints(u, v, cells, s, wander) {
    let bd = 9, bid = 0, bsd = 0;
    const x = u * cells, xi = FL(x);
    for (let k = xi - 1; k <= xi + 1; k++) {
      const kk = ((k % cells) + cells) % cells;
      const jit = (ih(kk, 0, s) - .5) * .62;
      const w = (pvF(.5, v, 1, 4, s + kk * 131) - .5) * wander + (pvF(.5, v, 1, 12, s + kk * 37) - .5) * wander * .34;
      const cx = (k + .5 + jit) / cells + w, d = ABS(u - cx);
      if (d < bd) { bd = d; bid = kk; bsd = u < cx ? -1 : 1; }
    }
    JT.d = bd; JT.id = bid; JT.side = bsd; JT.open = ih(bid, 9, s); return JT;
  }
  const FM = { h: 0, bench: 0, cleft: 0 };
  let FK = 1;                                      /* form amplitude — 0 gives the flat wall back */
  function setForm(k) { FK = k == null ? 1 : k; }
  function form(u, v, R) {
    const uu = u + (pvF(.5, v, 1, 3, R + 310) - .5) * .11;
    const NR = 6, xr = uu * NR, ci = FL(xr), rf = sm(xr - ci);
    const amp1 = i => { const k = ((i % NR) + NR) % NR; return (k % 2 ? 1 : -1) * (.34 + ih(k, 320, R) * .66); };
    const rib = clamp(lerp(amp1(ci), amp1(ci + 1), rf) * 1.55, -1, 1);
    const N2 = 14, x2 = uu * N2 + .37, c2 = FL(x2), r2f = sm(x2 - c2);
    const amp2 = i => { const k = ((i % N2) + N2) % N2; return (k % 2 ? 1 : -1) * (.20 + ih(k, 321, R) * .80); };
    /* third octave: 17 cells at 0.22 amplitude — cliffRig's `rib3 * 0.11` on a ±1 field. It is the
       coarse unevenness that stops the six ribs reading as a moulding; a 9-cell/0.10 stand-in
       widens the envelope and deepens the toe. */
    const rib3 = (pvF(uu, v, 17, 3, R + 308) - .5) * 2;
    const raw = (rib * .62 + lerp(amp2(c2), amp2(c2 + 1), r2f) * .26) * .55 + rib3 * .11;
    const vprof = ss(0, .16, v) * (1 - ss(.80, 1.0, v) * .35) * (1 - SL.tf * .32);
    let out = raw * vprof;
    /* cleft width carries the joint's own openness — a fixed width is five identical chimneys */
    const cj = joints(uu, v, 5, R + 303, .045), cw = .10 + cj.open * .12;
    const cleft = (1 - ss(cw * .4, cw * 1.7, cj.d * 5)) * ss(.06, .34, -raw) * ss(.24, .60, ih(cj.id, 307, R));
    out -= cleft * .62 * vprof;
    const bv = v * 3 + (pvF(uu, .5, 3, 1, R + 304) - .5) * .85, bi = FL(bv), bf = bv - bi, b3 = ((bi % 3) + 3) % 3;
    const alive = ss(.26, .70, pvF(uu, .5, 5, 1, R + 309 + b3 * 37));
    const back = (ih(b3, 306, R) - .5) * .52 * (.35 + SL.tf * .85) * (.30 + alive * .70);
    const lip = (1 - ss(0, .14, bf)) * ss(.30, .72, ih(b3, 305, R)) * alive;
    out += back + lip * .24;
    FM.h = out * FK; FM.bench = lip; FM.cleft = cleft;
    return FM.h;
  }
  /* the form field depends only on (seed, batter, size) — fifteen bakes of one rock share it */
  const FCACHE = {};
  function formField(R, n, m) {
    const key = R + '|' + SL.deg + '|' + n + 'x' + m + '|' + FK;
    if (FCACHE[key]) return FCACHE[key];
    const N = n * m, h = new Float32Array(N), bn = new Float32Array(N), cl = new Float32Array(N);
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x; form((x + .5) / n, (y + .5) / m, R);
      h[i] = FM.h; bn[i] = FM.bench; cl[i] = FM.cleft;
    }
    const mb = blur(h, n, m, 10, 7);
    const e = { h, bn, cl, mb };
    const ks = Object.keys(FCACHE); if (ks.length > 8) delete FCACHE[ks[0]];
    return (FCACHE[key] = e);
  }
  /* separable box blur — wraps in s, clamps in t */
  function blur(src, n, m, rx, ry) {
    const N = n * m, t1 = new Float32Array(N), out = new Float32Array(N);
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      let s = 0; for (let k = -rx; k <= rx; k++) s += src[y * n + (((x + k) % n) + n) % n];
      t1[y * n + x] = s / (2 * rx + 1);
    }
    for (let x = 0; x < n; x++) for (let y = 0; y < m; y++) {
      let s = 0; for (let k = -ry; k <= ry; k++) s += t1[clamp(y + k, 0, m - 1) * n + x];
      out[y * n + x] = s / (2 * ry + 1);
    }
    return out;
  }

  /* --------------------------------------------------------------------------------- the tiers
     Six palettes cut from one rock colour. The wall sits at 0; a rib crown is +1, a flank or a
     gully −1, a cast shadow another one or two down, and the aspect's incidence moves the whole
     sheet one step. −3 exists so a shaded E wall still has somewhere to put its own shadows. */
  const TIERS = [-3, -2, -1, 0, 1, 2];
  const T0 = 3;                                    /* index of tier 0 */
  function tierBase(base, k) {
    if (k < 0) return P.mix(P.mix(base, '#000000', .135 * -k), P.COLD, .095 * -k);
    if (k > 0) return P.mix(P.mix(base, '#ffffff', .055 * k), P.WARM, .125 * k);
    return base;
  }

  /* ------------------------------------------------------------------------------------ marks */
  const HOLLOW = [stencil(['.oo.', 'oooo', '.oo.']), stencil(['ooo', 'ooo']), stencil(['.ooo.', 'ooooo', 'ooooo', '.ooo.'])];
  const stones = {};
  const stone = (w, h) => stones[w + 'x' + h] || (stones[w + 'x' + h] = ellipse(w, h));

  /* ================================================================================== sandstone */
  function bedList(c) {
    const { m, R, def } = c, out = [], S = 1 / SL.sin;      /* beds down a batter are 1/sin apart */
    let y = 0, k = 0, unit = -1, left = 0;
    while (y < m) {
      if (left <= 0) { unit++; left = 1 + FL(hash2(k, 2, R) * (unit % 2 ? 3.4 : 2.2)); }
      const massive = unit % 2 === 0, rg = massive ? def.massive : def.thin;
      let th = Math.max(1, Math.round((rg[0] + FL(hash2(k, 1, R) * (rg[1] - rg[0] + 1))) * S));
      if (m - y - th < 2) th = m - y;
      const r = hash2(k, 3, R);
      out.push({ y0: y, th, massive, unit,
        tone: massive ? (r < .22 ? 3 : 2) : (r < .42 ? 1 : 2),
        prot: hash2(k, 4, R) < .13 ? (hash2(k, 5, R) < .5 ? 1 : -1) : 0,
        silt: !massive && hash2(k, 6, R) < def.silt,
        jsp: massive ? def.jsp[0] + hash2(k, 7, R) * def.jsp[1] : def.jsp[2] + hash2(k, 8, R) * def.jsp[3] });
      y += th; k++; left--;
    }
    return out;
  }
  function bedField(c, beds) {
    const { n, m, R } = c, at = new Int16Array(n * m);
    const warp = (x, k) => Math.round(1.3 * Math.sin(x / n * Math.PI * 2 * (1 + k % 3) + k * 1.3) + (fb(x / n, k / beds.length, 3, 1, R + 11, 2) - .5) * 2.6);
    for (let x = 0; x < n; x++) for (let k = 0; k < beds.length; k++) {
      const a = beds[k].y0 + warp(x, k), e = k + 1 < beds.length ? beds[k + 1].y0 + warp(x, k + 1) : m + warp(x, 0);
      for (let y = a; y < e; y++) at[(((y % m) + m) % m) * n + x] = k;
    }
    return at;
  }
  function paintSandstone(c) {
    const { t, n, m, R, st, A, pal, def } = c;
    const beds = bedList(c), bedAt = bedField(c, beds), rd = A.read > 1.2 ? 1 : 0;
    const prot = (x, k) => {
      const b = beds[k], blk = FL(x / b.jsp), r = hash2(blk, k + 60, R);
      return b.prot + (r < pk([.05, .09, .14], st) ? 1 : r > 1 - pk([.04, .08, .13], st) ? -1 : 0);
    };
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const k = bedAt[y * n + x], b = beds[k], p = prot(x, k);
      const pl = b.silt && fb(x / n, y / m, 5, 1, R + 12 + k, 2) > .42 ? pal.silt : pal.rock;
      const stain = fb(x / n, y / m, 2, 3, R + 15, 2);
      const bd = b.tone + (p > 0 ? 1 : 0) + (stain > .70 ? -1 : 0);
      t.set(x, y, pl, bd, p * 1.5 + (b.tone - 2) * .35 + (b.massive ? .5 : 0));
      const kUp = bedAt[((y - 1 + m) % m) * n + x];
      if (kUp !== k) {
        const pUp = prot(x, kUp);
        if (b.massive && !beds[kUp].massive || p > pUp) {                       /* a lit lip */
          t.set(x, y, pl, clamp(bd + 1, 0, 4), p * 1.5 + .8, 1);
          if (rd && hash2(x, k, R + 13) < .55) t.set(x, y + 1, pl, clamp(bd + 1, 0, 4), p * 1.5 + .6, 1);
        } else if (!b.massive && beds[kUp].massive || p < pUp) {                /* under the lip */
          t.set(x, y, pl, 0, p * 1.5 - .6, 1);
          if ((rd || hash2(x, y, R + 14) < .45)) t.set(x, y + 1, pl, clamp(bd - 1, 0, 4), p * 1.5 - .2, 1);
        } else if (hash2(x >> 1, k, R + 14) < .5) t.set(x, y, pl, clamp(bd - 1, 0, 4), p * 1.5 - .15, 1);
      } else if (!b.massive && (y - b.y0) % 2 === 1 && fb(x / n, y / m, 3, 1, R + 16, 2) > .46 && hash2(x, y, R + 17) < .55) {
        t.set(x, y, pl, clamp(bd - 1, 0, 4), t.h[t.i(x, y)] - .1);              /* flaggy parting */
      }
      const pR = prot(x + 1, k);
      if (p > pR) t.set(x + 1, y, pal.rock, 0, pR * 1.5, 1);
      else if (p < pR) t.set(x, y, pl, clamp(bd - 1, 0, 4), p * 1.5, 1);
    }
    /* joints: per bed, wandering traces gated along their length; a lit wall on the open ones at Hi */
    const gate = pk([.56, .48, .40], st);
    for (let k = 0; k < beds.length; k++) {
      const b = beds[k], nJ = Math.max(1, Math.round(n / b.jsp));
      for (let j = 0; j < nJ; j++) {
        let x = Math.round((j + hash2(k, j + 40, R)) * n / nJ);
        for (let yy = b.y0 - 1; yy < b.y0 + b.th + 1; yy++) {
          if (hash2(x, yy, R + 41) < .12) x += hash2(x, yy, R + 42) < .5 ? -1 : 1;
          const y = ((yy % m) + m) % m, xw = ((x % n) + n) % n;
          if (fb(xw / n, y / m, 4, 4, R + 43, 2) < gate) continue;
          t.set(xw, y, pal.rock, 0, t.h[y * n + xw] - 1.3, 1);
          if (rd) t.set(xw + 1, y, pal.rock, 0, t.h[t.i(xw + 1, y)] - 1.1, 1);
          if (st === 2 && hash2(xw >> 1, y >> 2, R + 44) < .5) t.set(xw + 1 + rd, y, pal.rock, 3, t.h[t.i(xw + 1 + rd, y)], 1);
        }
      }
    }
    /* spalls stand proud, sockets are plucked — the reference wall is a mosaic of sockets */
    for (const s of sites(n, m, pk([13, 10, 8], st), .9, R + 50)) {
      const b = beds[bedAt[s.y * n + s.x]]; if (!b.massive || s.r > pk([.30, .44, .56], st)) continue;
      const w = 4 + Math.round(s.r2 * 5), h = 2 + Math.round(s.r2 * 3);
      if (hash2(s.x, s.y, R + 51) < .55) stamp(t, stone(w, h), s.x, s.y, { pal: pal.rock, band: b.tone + (hash2(s.x, s.y, R + 52) < .4 ? 1 : 0), seam: 1, tip: 1, h: 1.6, hBase: .5, lock: 1 });
      else { stamp(t, stone(w, h), s.x, s.y, { pal: pal.rock, band: 1, seam: -2, tip: -1, h: -1.3, hBase: .5, lock: 1 }); t.set(s.x, s.y + (h >> 1), pal.rock, 3, -1.0, 1); }
    }
    /* honeycomb in the laminated packets, up the ladder */
    if (st >= 1) for (const s of sites(n, m, pk([16, 11, 7], st), .9, R + 55, s => s.r < pk([0, .42, .58], st))) {
      if (beds[bedAt[s.y * n + s.x]].massive) continue;
      const hs = HOLLOW[FL(s.r2 * HOLLOW.length)];
      stamp(t, hs, s.x, s.y, { pal: pal.rock, band: 0, seam: -1, tip: 0, h: -1.6, lock: 1 });
      t.set(s.x + (hs.w >> 1) - 1, s.y + (hs.h >> 1) - 1, pal.rock, 3, -1.2, 1);
    }
    /* weathering: dust crowns on the windward lips, seep and moss under the lee joints */
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x;
      if (A.dust && t.band[i] >= 3 && t.pal[i] === pal.rock && hash2(x >> 1, y, R + 80) < A.dust * .5) t.set(x, y, pal.dust, 3, undefined, 1);
      if (A.lee && t.band[i] === 0 && hash2(x, y, R + 81) < A.lee * .16) {
        const len = 2 + FL(hash2(x, y, R + 82) * 6);
        for (let d = 1; d <= len; d++) { const j = t.i(x, y + d); if (t.pal[j] !== pal.rock) break; t.set(x, y + d, pal.moss, d < len / 2 ? 1 : 2, t.h[j], 1); }
      }
    }
    return bedAt;
  }

  /* ======================================================================================= till */
  function paintTill(c) {
    const { t, n, m, R, st, A, pal } = c;
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const cr = fb(x / n, y / m, 14, 11, R + 20, 2), rag = fb(x / n, y / m, 30, 24, R + 22, 2);
      t.set(x, y, pal.rock, cr > .66 && rag > .46 ? 3 : cr < .30 && rag < .56 ? 1 : 2, cr > .66 ? .3 : 0);
    }
    /* rills at two calibres — long in t, wandering, gated, each with its lit right wall. A rill
       that runs the whole 9 m is a flute in masonry, so every trace has a life along its length. */
    for (const cal of [0, 1]) {
      const sp = cal ? pk([17, 14, 12], st) : pk([8, 7, 6], st), nJ = Math.max(1, Math.round(n / sp));
      const gate = cal ? pk([.60, .50, .40], st) : pk([.72, .64, .56], st);
      for (let j = 0; j < nJ; j++) {
        const x0 = (j + (hash2(j, cal * 7 + 1, R + 30) - .5) * 1.5) * n / nJ;   /* spacing is not a ruler */
        const drift = (hash2(j, cal * 7 + 2, R + 30) - .5) * (cal ? 16 : 9);    /* they converge as they run */
        const v0 = hash2(j, cal * 7 + 3, R + 30) * .55;
        const v1 = v0 + .30 + hash2(j, cal * 7 + 4, R + 30) * .70;
        const dark = hash2(j, cal * 7 + 5, R + 30) < .45 ? 1 : 0;
        for (let y = 0; y < m; y++) {
          const v = y / m, env = ss(v0 - .05, v0 + .12, v) * (1 - ss(v1 - .14, v1 + .03, v));
          if (env < .38) continue;
          const sj = x0 + drift * v + (fb(j / nJ, v, 1, 3, R + 31 + j * 7 + cal * 50, 2) - .5) * (cal ? 7 : 4.5);
          const x = ((Math.round(sj) % n) + n) % n, i = y * n + x;
          if (fb(x / n, v, 2, 3, R + 32 + cal, 2) < gate) continue;
          t.set(x, y, pal.rock, cal ? (dark ? 0 : 1) : 1, t.h[i] - (cal ? 1.4 : .7), 1);
          if (cal) { t.set(x + 1, y, pal.rock, 3, t.h[t.i(x + 1, y)] + .2, 1); if (st >= 1 && dark) t.set(x - 1, y, pal.rock, 1, t.h[t.i(x - 1, y)] - .4, 1); }
        }
      }
    }
    /* slump benches: a lit tread with the shade under it, dying out along s */
    const nb = pk([1, 2, 3], st);
    for (let q = 0; q < nb; q++) {
      const yb = Math.round((q + .5 + (hash2(q, 3, R) - .5) * .6) * m / nb);
      for (let x = 0; x < n; x++) {
        if (fb(x / n, q / nb, 4, 1, R + 40 + q, 2) < pk([.62, .52, .44], st)) continue;
        const y = yb + Math.round((fb(x / n, q / nb, 3, 1, R + 41 + q, 3) - .5) * 9 + (fb(x / n, q / nb, 9, 1, R + 42 + q, 2) - .5) * 3);
        t.set(x, y, pal.rock, 4, t.h[t.i(x, y)] + 1.2, 1);
        t.set(x, y - 1, pal.rock, 3, t.h[t.i(x, y - 1)] + .6, 1);
        t.set(x, y + 1, pal.rock, 0, t.h[t.i(x, y + 1)] - .9, 1);
        if (st === 2) t.set(x, y + 2, pal.rock, 1, t.h[t.i(x, y + 2)] - .4, 1);
      }
    }
    /* hollows and small slumped scars — the clay is not a fluted column */
    for (const s of sites(n, m, pk([22, 17, 13], st), .9, R + 45, s => s.r < pk([.30, .45, .60], st))) {
      const hs = HOLLOW[FL(s.r2 * HOLLOW.length)];
      stamp(t, hs, s.x, s.y, { pal: pal.rock, band: 0, seam: -1, tip: 0, h: -1.5, lock: 1 });
      t.set(s.x + (hs.w >> 1) - 1, s.y + (hs.h >> 1) - 1, pal.rock, 3, -1.1, 1);
    }
    /* clasts, and the turf mats that slide down a failing bank */
    for (const s of sites(n, m, pk([19, 16, 14], st), .9, R + 50, s => s.r < .34)) {
      const w = 2 + Math.round(s.r2 * 2);
      stamp(t, stone(w + 1, w), s.x, s.y, { pal: pal.peb, band: 2, seam: 1, tip: 1, h: 1.0, lock: 1 });
    }
    if (st === 2) for (const s of sites(n, m, 30, .9, R + 60, s => s.r < .45 && s.v < .5)) {
      const w = 5 + Math.round(s.r2 * 7), h = 4 + Math.round(s.r * 8);
      stamp(t, stone(w, h), s.x, s.y, { pal: pal.turf, band: 2, seam: 1, tip: 1, h: 1.4, lock: 1 });
      for (let d = 0; d < 3; d++) t.set(s.x + d - 1, s.y + (h >> 1) + 1, pal.rock, 0, -.6, 1);
    }
    if (A.dust) for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      if (t.band[y * n + x] >= 3 && hash2(x >> 1, y, R + 80) < A.dust * .35) t.set(x, y, pal.dust, 3, undefined, 1);
    }
    return null;
  }

  /* ===================================================================================== basalt */
  function paintBasalt(c) {
    const { t, n, m, R, st, A, pal } = c;
    const cw = 7, nc = Math.max(2, Math.round(n / cw)), w = n / nc;
    const colOf = x => FL((((x % n) + n) % n) / w);
    const tone = j => { const r = hash2(j, 10, R); return r < .18 ? 1 : r > .84 ? 3 : 2; };
    const nseg = j => 2 + FL(hash2(j, 20, R) * 3), segOff = j => FL(hash2(j, 21, R) * m);
    const segAt = (j, y) => { const L = m / nseg(j), q = ((y + segOff(j)) % m + m) % m, i = FL(q / L); return { i, r: q - i * L }; };
    const segProt = (j, i) => { const r = hash2(j * 31 + i, 22, R); return r < pk([.08, .13, .19], st) ? 1 : r > 1 - pk([.05, .10, .16], st) ? -1 : 0; };
    /* the entablature: a rubble zone under the brow with a wandering lower boundary */
    const ent = (x, y) => y / m < .11 + (fb(x / n, .5, 3, 1, R + 61, 2) - .5) * .10;
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      if (ent(x, y) || (y / m < .24 && fb(x / n, y / m, 4, 3, R + 60, 2) > .52 + y / m * 1.4)) {
        const r = hash2(x >> 2, y >> 1, R + 62);
        t.set(x, y, pal.rock, r < .28 ? 1 : r > .82 ? 3 : 2, (r - .5) * 1.8);
        continue;
      }
      const j = colOf(x), sg = segAt(j, y), p = segProt(j, sg.i), tn = tone(j);
      const bd = tn + (p > 0 ? 1 : 0), h = (tn - 2) * .5 + p * 1.3;
      t.set(x, y, pal.rock, bd, h);
      if (sg.r < 1) {
        const pUp = segProt(j, (sg.i + nseg(j) - 1) % nseg(j));
        if (p > pUp) t.set(x, y, pal.rock, clamp(bd + 1, 0, 4), h + .5, 1);
        else if (p < pUp) t.set(x, y, pal.rock, 0, h - .5, 1);
        else if (hash2(x >> 1, sg.i, R + 64) < .6) t.set(x, y, pal.rock, Math.max(0, tn - 1), h - .35, 1);
      }
      if (colOf(x - 1) !== j) {
        t.set(x, y, pal.rock, 0, h - 1.2, 1);
        if (A.read > 1.2) t.set(x + 1, y, pal.rock, Math.max(0, tn - 1), h - .6, 1);
        else if (st === 2 && hash2(x, y >> 1, R + 63) < .5) t.set(x + 1, y, pal.rock, clamp(tn + 1, 0, 4), undefined, 1);
      }
      if (A.xan > .4 && sg.r <= 2 && fb(x / n, y / m, 11, 9, R + 66, 2) > .58 && hash2(x, y, R + 65) < A.xan * pk([.20, .28, .36], st)) t.set(x, y, pal.xan, 3, undefined, 1);
    }
    /* spalled columns up the ladder: a socket with one lit inner wall */
    if (st >= 1) for (const s of sites(n, m, pk([20, 14, 10], st), .9, R + 70, s => s.r < pk([0, .40, .58], st))) {
      const hs = HOLLOW[FL(s.r2 * HOLLOW.length)];
      stamp(t, hs, s.x, s.y, { pal: pal.rock, band: 0, seam: -1, tip: 0, h: -1.7, lock: 1 });
      t.set(s.x + (hs.w >> 1) - 1, s.y + (hs.h >> 1) - 1, pal.rock, 3, -1.3, 1);
    }
    return null;
  }
  DEF.sandstone.paint = paintSandstone; DEF.till.paint = paintTill; DEF.basalt.paint = paintBasalt;

  /* ------------------------------------------------------------------------------------ batter
     A wall shrugs its debris off; a slope keeps it. Colluvium collects in the form's hollows, on
     the benches and in the clefts — never as an even film, and never as a clean band along a bench,
     which is a painted stripe — and plants get a foothold in it. It is the same rule for all three
     rocks, applied after the geology so the batter reads as weathering rather than as a tint. */
  function slopeWeather(c, FF) {
    if (SL.tf < .002) return;
    const { t, n, m, R, st, pal, A } = c, tf = SL.tf;
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x, u = (x + .5) / n, v = (y + .5) / m;
      const dep = ss(.16, .80, fb(u, v, 7, 5, R + 320, 2)) * (.30 + ss(.05, .95, v) * .70);
      const hollow = ss(.55, .18, FF.h[i] * 2 + .5);
      const patchy = ss(.22, .78, fb(u, v, 15, 10, R + 322, 2));
      const g = clamp((dep * (.35 + hollow * .85) + FF.bn[i] * .55 * patchy + FF.cl[i] * .30) * tf, 0, 1);
      if (g > .46) t.set(x, y, pal.grit, g > .74 ? 2 : 1, t.h[i] + .3);
    }
    for (const s of sites(n, m, Math.round(14 - tf * 5), .9, R + 330, s => s.r < (.18 + A.lee * .22) * tf)) {
      if (t.pal[t.i(s.x, s.y)] !== pal.grit) continue;
      const w = 2 + FL(s.r2 * 3);
      stamp(t, stone(w + 1, w), s.x, s.y, { pal: pal.turf, band: s.r2 > .6 ? 3 : 2, seam: 1, tip: 1, h: .9, lock: 1 });
    }
  }

  /* ======================================================================================= face */
  function face(rock, aspect, step, o) {
    o = o || {};
    const def = DEF[rock]; if (!def) throw new Error('PxCliffFace: no rock ' + rock);
    const A = ASPECT[aspect] || ASPECT.S, st = clamp(step | 0, 0, 2);
    const z = o.z || 2, W = o.W || 384, H = o.H || 288, n = W / z, m = H / z;
    const R = o.seed || (ROCKS.indexOf(rock) * 1013 + 7703);
    setSlope(o.slope);
    const t = new Tile(n, m);
    const tiers = TIERS.map(k => t.addPal(tierBase(def.base, k), def.c));
    const pal = {
      rock: tiers[T0],
      silt: t.addPal('#8e8c78', .9), moss: t.addPal('#4a5a38', .8), dust: t.addPal('#c9a686', .7),
      peb: t.addPal('#8f7d6a', .8), turf: t.addPal('#44582e', .75), grit: t.addPal('#7a6149', .8),
      xan: t.addRamp(['#5a4210', '#8a6a1e', '#b58f2a', '#d8b043', '#e8c860']),
    };
    t.fill(pal.rock, 2, 0);
    const FF = formField(R, n, m);
    const c = { t, n, m, z, R, st, A, def, pal, tiers };
    def.paint(c);
    slopeWeather(c, FF);
    clean(t, 1, 3);

    /* ---- geometry: the form field, its own normal, the macro AO and the cast shadow ---- */
    const mf = FF.h, mb = FF.mb, N = n * m;
    const L = keyFor();
    const mrel = RELIEF_M * (PPU / z) * .45;              /* form metres → slope on the texel grid */
    const relief = 1.8 * (o.relief == null ? 1 : o.relief);
    const det = blur(t.h, n, m, 2, 2);                     /* the cavity reference for the detail */
    const nx = new Float32Array(N), ny = new Float32Array(N), nz = new Float32Array(N);
    const fnl = new Float32Array(N), cav = new Float32Array(N), mao = new Float32Array(N);
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x, xm = (x - 1 + n) % n, xp = (x + 1) % n, ym = (y - 1 + m) % m, yp = (y + 1) % m;
      const ds = (t.h[y * n + xp] - t.h[y * n + xm]) * .5 * relief + (mf[y * n + xp] - mf[y * n + xm]) * .5 * mrel;
      const dt = (t.h[yp * n + x] - t.h[ym * n + x]) * .5 * relief + (mf[yp * n + x] - mf[ym * n + x]) * .5 * mrel;
      let a = -ds, b = -dt; const il = 1 / Math.sqrt(a * a + b * b + 1);
      nx[i] = a * il; ny[i] = b * il; nz[i] = il;
      /* the form's own normal — broad, so the tiers are big shapes and not a contour map */
      const fs = (mf[y * n + xp] - mf[y * n + xm]) * .5 * mrel, ft = (mf[yp * n + x] - mf[ym * n + x]) * .5 * mrel;
      const fl = 1 / Math.sqrt(fs * fs + ft * ft + 1);
      fnl[i] = (-fs * fl) * L[0] + (-ft * fl) * L[1] + fl * L[2];
      cav[i] = clamp((t.h[i] - det[i]) * .55 + .5, 0, 1);
      mao[i] = clamp((mf[i] - mb[i]) * 2.2 + .55, 0, 1);
    }
    /* the cast shadow: a horizon march along the key over the form field only */
    const shd = new Float32Array(N), dpx = RELIEF_M * (PPU / z);
    {
      const lh = Math.hypot(L[0], L[1]) || 1e-4, ux = L[0] / lh, uy = L[1] / lh, rate = L[2] / lh;
      const KS = [1, 2, 4, 6, 9, 13, 18, 24, 32];
      for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
        const i = y * n + x, d0 = mf[i] * dpx; let s = 0;
        for (let q = 0; q < KS.length; q++) {
          const k = KS[q];
          const sx = (((x + Math.round(k * ux)) % n) + n) % n, sy = clamp(y + Math.round(k * uy), 0, m - 1);
          const over = mf[sy * n + sx] * dpx - (d0 + k * rate);
          if (over > 0) { const p = clamp(over / 2.0, 0, 1); if (p > s) s = p; }
        }
        shd[i] = s;
      }
    }
    /* ---- the tiers: quantised form, torn along its boundary and stepped at the bed contacts ---- */
    /* ---- the tiers: the form drawn at the block scale ----
       A gradient is not pixel art and neither is a contour map, so the form is not sampled per
       texel. It is sampled once per CELL of about 6 × 5 texels (37 × 31 cm) — cell rows staggered,
       cell edges torn by a fine field — and the whole cell takes that one tier. A buttress comes
       out as a mosaic of flat blocks with hard edges, which is how a pixel artist draws one, and
       the cast shadow comes out with a stepped edge, which is how a pixel artist draws that. */
    const flat = L[2];                                     /* what a plane answers, so flat wall = tier 0 */
    const tierA = new Int8Array(N), tierU = new Int8Array(N), edge = new Int8Array(N);
    const CS = 8, CT = 6;
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x;
      const rag = (fb(x / n, y / m, 30, 22, R + 906, 2) - .5) * 1.8;
      const ox = (hash2(0, FL(y / CT), R + 903) - .5) * 6 + rag;
      const oy = (hash2(FL(x / CS), 0, R + 904) - .5) * 5 + rag;
      const cxi = FL((x + ox) / CS), cyi = FL((y + oy) / CT);
      const sx = (((Math.round((cxi + .5) * CS - ox)) % n) + n) % n;
      const sy = clamp(Math.round((cyi + .5) * CT - oy), 0, m - 1);
      const j = sy * n + sx;
      const q = (fnl[j] - flat) * 3.4 + (mao[j] - .55) * .40 + (hash2(cxi, cyi, R + 905) - .5) * .22;
      let a = q > .40 ? 1 : q < -.40 ? -1 : 0;
      const s = shd[j];
      if (s > .85) a -= 2; else if (s > .45) a -= 1;
      tierA[i] = clamp(a + A.shift, -3, 2);
      const u = (mao[j] - .55) * 1.6 + (hash2(cxi, cyi, R + 907) - .5) * .30;
      tierU[i] = clamp(u > .40 ? 1 : u < -.40 ? -1 : 0, -3, 2);
    }
    /* Where a tier meets a LOWER one across a locally straight edge, one lit texel on the near
       side: the arris. Gated on the run being straight (the three texels above and below step the
       same way) — a ragged perimeter lit texel by texel is fizz, not an edge. */
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x, a = tierA[i], xp = (x + 1) % n;
      if (a <= tierA[y * n + xp]) continue;
      const up = ((y - 1 + m) % m) * n, dn = ((y + 1) % m) * n;
      if (tierA[up + x] > tierA[up + xp] && tierA[dn + x] > tierA[dn + xp]) edge[i] = 1;
    }
    let h0 = 1e9, h1 = -1e9;
    for (let i = 0; i < N; i++) { const h = t.h[i] * .1 + mf[i] * 3; if (h < h0) h0 = h; if (h > h1) h1 = h; }
    return { tile: t, n, m, z, W, H, rock, aspect, step: st, A, tiers, pal, nx, ny, nz, cav, mao, shd, tierA, tierU, edge, mf, relief, slope: SL.deg, hRange: [h0, h1], seed: R };
  }

  /* ---------------------------------------------------------------------------------- channels */
  function channels(b, opt) {
    opt = opt || {};
    const { tile: t, n, m, z, W, H, A, tiers, tierA, tierU, edge, nx, ny, nz, cav, shd, mf } = b;
    const N = n * m, KEY = keyFor(), lo = .40, hi = .74;
    const alb = new Uint8ClampedArray(W * H * 4), unl = new Uint8ClampedArray(W * H * 4);
    const msk = new Uint8ClampedArray(W * H * 4), nrm = new Uint8ClampedArray(W * H * 4);
    /* the index channel is OPT-IN (channels(b, {index:true})) so the standing bake's file count
       does not move. It is what the palette-shift relight path needs: a colour-only unlit texture
       cannot be band-shifted without an inverse lookup, so we publish the (row, band) the texel
       was cut from and let the engine's own sun step the index instead of multiplying the pixel. */
    const wantIdx = !!opt.index, ROW0 = ROCKS.indexOf(b.rock) * 6;
    const idx = wantIdx ? new Uint8ClampedArray(W * H * 4) : null;
    const hs = b.hRange[1] > b.hRange[0] ? 255 / (b.hRange[1] - b.hRange[0]) : 0;
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x, isRock = t.pal[i] === tiers[T0];
      const ndl = nx[i] * KEY[0] + ny[i] * KEY[1] + nz[i] * KEY[2];
      const sh = shd[i];
      /* albedo — the fixed-key ART: the tier carries the form and the aspect, one band the detail */
      let ba = t.band[i] + (isRock ? 0 : tierA[i]) + edge[i];
      if (sh < .45) { if (ndl > hi) ba++; else if (ndl < lo) ba--; } else ba--;
      const pa = isRock ? tiers[clamp(tierA[i] + T0, 0, 5)] : t.pal[i];
      const ca = t.pals[pa][clamp(ba, 0, 4)];
      /* unlit — the authored bands plus the non-directional cavity only */
      let bu = t.band[i] + (isRock ? 0 : tierU[i]);
      if (cav[i] > .80) bu++; else if (cav[i] < .22) bu--;
      const pu = isRock ? tiers[clamp(tierU[i] + T0, 0, 5)] : t.pal[i];
      const cu = t.pals[pu][clamp(bu, 0, 4)];
      /* index — R LUT row of the UNLIT state · G band 0..4 · B tier-shiftable · A coverage.
         Rows 0..17 are the three rocks' six tiers; 18..24 the shared accessory palettes
         (silt moss dust peb turf grit xanthoria), which take a band step but NEVER a tier step —
         lichen does not go the colour of shadowed rock. Raw indices: import with sRGB off. */
      let ir = 0, ib = 0;
      if (wantIdx) {
        ir = isRock ? ROW0 + clamp(tierU[i] + T0, 0, 5) : 18 + clamp(pu - 6, 0, 6);
        ib = clamp(bu, 0, 4);
      }
      /* mask — R key N·L with the cast shadow in it, G sky, B height, A coverage */
      const sky = clamp(.5 + SL.tf * .30 + ny[i] * -.45, 0, 1) * (.30 + cav[i] * .55);
      const mr = clamp(ndl, 0, 1) * (1 - sh * .78) * 255, mg = sky * 255;
      const mbv = clamp((t.h[i] * .1 + mf[i] * 3 - b.hRange[0]) * hs, 0, 255);
      const cavT = clamp(cav[i] * (.52 + b.mao[i] * .48), 0, 1);
      const rx = (nx[i] * .5 + .5) * 255, ry = (-ny[i] * .5 + .5) * 255, rz = (nz[i] * .5 + .5) * 255, ra = cavT * 255;
      for (let dy = 0; dy < z; dy++) for (let dx = 0; dx < z; dx++) {
        const o = ((y * z + dy) * W + x * z + dx) * 4;
        alb[o] = ca[0]; alb[o + 1] = ca[1]; alb[o + 2] = ca[2]; alb[o + 3] = 255;
        unl[o] = cu[0]; unl[o + 1] = cu[1]; unl[o + 2] = cu[2]; unl[o + 3] = 255;
        msk[o] = mr; msk[o + 1] = mg; msk[o + 2] = mbv; msk[o + 3] = 255;
        nrm[o] = rx; nrm[o + 1] = ry; nrm[o + 2] = rz; nrm[o + 3] = ra;
        if (wantIdx) { idx[o] = ir; idx[o + 1] = ib; idx[o + 2] = isRock ? 255 : 0; idx[o + 3] = 255; }
      }
    }
    const out = { '': alb, _unlit: unl, _mask: msk, _normal: nrm };
    if (wantIdx) out._index = idx;
    return out;
  }

  /* ------------------------------------------------------------------------------- palette LUT
     Every colour the face bake can emit, as data — the table the palette-shift relight path
     indexes with the _index channel. 25 rows × 5 bands; rows 0..17 rock tiers (rock × 6),
     18..24 the accessory palettes in the order face() registers them. */
  const ACC = [['silt', '#8e8c78', .9], ['moss', '#4a5a38', .8], ['dust', '#c9a686', .7],
    ['peb', '#8f7d6a', .8], ['turf', '#44582e', .75], ['grit', '#7a6149', .8]];
  const XAN = ['#5a4210', '#8a6a1e', '#b58f2a', '#d8b043', '#e8c860'];
  function paletteLUT() {
    const rows = [];
    for (const r of ROCKS) for (const k of TIERS)
      rows.push({ row: rows.length, rock: r, tier: k, kind: 'rock', bands: P.bands(tierBase(DEF[r].base, k), DEF[r].c) });
    for (const a of ACC) rows.push({ row: rows.length, id: a[0], kind: 'accessory', bands: P.bands(a[1], a[2]) });
    rows.push({ row: rows.length, id: 'xan', kind: 'accessory', bands: XAN.slice() });
    return rows;
  }

  /* ---------------------------------------------------------------------------------- profile
     The same form field as PLAN DISPLACEMENT IN METRES, continuous — the geometry stays smooth
     while the texture is quantised, which is exactly the division of labour we want. */
  function profile(rock, aspect, o) {
    o = o || {};
    const W = o.W || 384, H = o.H || 288;
    setSlope(o.slope);
    const R = o.seed || (ROCKS.indexOf(rock) * 1013 + 7703), disp = new Float32Array(W * H);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) disp[y * W + x] = form((x + .5) / W, (y + .5) / H, R) * RELIEF_M;
    return { disp, W, H, metres: RELIEF_M, slope: SL.deg, pxm: PPU };
  }

  /* ------------------------------------------------------------------------------ brow decal
     RGBA, s along and tiling, t down and clamped, brow line at t = 0.42. The turf is the ground
     material's own colour at its own texel; the shade the mat throws is flat and quantised (three
     alpha levels, never a gradient) so one strip still serves all three rocks. */
  function brow(aspect, step, o) {
    o = o || {};
    const z = 2, W = o.W || 384, H = o.H || 128, n = W / z, m = H / z;
    const A = ASPECT[aspect] || ASPECT.S, st = clamp(step | 0, 0, 2), R = o.seed || 4421;
    setSlope(o.slope);
    const t = new Tile(n, m); t.a.fill(0);
    const turf = t.addPal('#44582e', .75), soil = t.addPal('#6d4a35', .8), straw = t.addPal('#b39c5c', .7);
    const shade = t.addRamp(['#160d0a', '#20130e', '#2a1a14', '#35231a', '#412d22']);
    const anchor = .42 * m;
    const alpha = new Uint8Array(n * m);
    for (let x = 0; x < n; x++) {
      const u = (x + .5) / n;
      const wob = (fb(u, .5, 4, 1, R + 1, 3) - .5) * .16 + (fb(u, .5, 13, 1, R + 2, 2) - .5) * .055;
      const lip = Math.round(anchor + wob * m);
      const over = Math.round((2.2 + A.lee * 2.0 + st * 1.6) * (.6 + fb(u, .5, 7, 1, R + 3, 2) * .8));
      const gash = fb(u, .5, 6, 1, R + 4, 3) > (.78 - st * .30);
      /* the mat: turf at the ground's own 2 px texel, its cut soil face, then the lip row */
      for (let y = 0; y < lip; y++) {
        if (gash && y > lip - 5) { const r = hash2(x, y, R + 9); if (r < .5 + st * .2) continue; }
        const X = x >> 1, Y = y >> 1, tick = hash2(X, Y, R + 5) < .17, above = hash2(X, Y - 1, R + 5) < .17;
        if (y < lip - 3) t.set(x, y, turf, tick ? 4 : above ? 2 : 3, tick ? 1.6 : 1.0, 1);
        else if (y < lip - 1) t.set(x, y, soil, hash2(x, y, R + 6) < .3 ? 1 : 2, .6, 1);
        else t.set(x, y, soil, 1, .3, 1);
        alpha[y * n + x] = 255;
      }
      /* hanging mat: it projects past the rock, so it is opaque turf below the lip line */
      for (let y = lip; y < lip + over; y++) {
        if (gash) break;
        const X = x >> 1, Y = y >> 1;
        t.set(x, y, y < lip + over - 1 ? turf : soil, y < lip + over - 1 ? (hash2(X, Y, R + 5) < .17 ? 3 : 2) : 1, .8, 1);
        alpha[y * n + x] = 255;
      }
      /* the undercut it throws: two flat alpha levels, the near band solid, the far one half, and
         both boundaries torn along s. A band of even width is a painted stripe. */
      const deep = Math.round((4.0 + A.lee * 1.8 + st * 1.4) * (.55 + fb(u, .5, 5, 1, R + 7, 2) * .9));
      for (let d = 0; d < deep + 4; d++) {
        const y = lip + (gash ? 0 : over) + d; if (y >= m) break;
        const near = d < deep * .55;
        if (!near && fb(u, d / (deep + 4), 9, 2, R + 8, 2) < .34 + d / (deep + 4) * .5) continue;
        t.set(x, y, shade, near ? 1 : 2, -.6, 1);
        alpha[y * n + x] = near ? 200 : 128;
      }
      /* clods and straw on a failing brow */
      if (st === 2 && hash2(x >> 2, 3, R + 10) < .18) {
        const y = lip + over + 3 + FL(hash2(x, 4, R + 10) * 8);
        if (y < m - 2) { stamp(t, stone(3, 2), x, y, { pal: soil, band: 2, seam: 1, tip: 1, h: 1.0, lock: 1 });
          for (let dx = -1; dx <= 1; dx++) for (let dy = -1; dy <= 1; dy++) alpha[clamp(y + dy, 0, m - 1) * n + ((x + dx + n) % n)] = 255; }
      }
      if (st === 0 && hash2(x >> 1, 5, R + 11) < .22) { const y = lip - 1; t.set(x, y, straw, 3, 1.2, 1); alpha[y * n + x] = 255; }
    }
    return decal(t, alpha, n, m, z, W, H);
  }

  /* ------------------------------------------------------------------------------- toe decal
     The three things neither the face nor a plan-view talus can draw: the sea-cut undercut notch,
     the salt-bleached basal beds — the pale cream band in every low-tide photograph — and the
     debris lying against the foot.  feature: 'notch' | 'cave' | 'slump'. */
  function toe(aspect, step, o) {
    o = o || {};
    const z = 2, W = o.W || 384, H = o.H || 128, n = W / z, m = H / z;
    const A = ASPECT[aspect] || ASPECT.S, st = clamp(step | 0, 0, 2), R = o.seed || 5527;
    const feat = o.feature || 'notch';
    const t = new Tile(n, m); t.a.fill(0);
    const bleach = t.addPal('#c9b8a0', .8), shade = t.addRamp(['#160d0a', '#20130e', '#2a1a14', '#35231a', '#412d22']);
    const grit = t.addPal('#7a6552', .85), wrack = t.addPal('#4a4432', .8);
    const alpha = new Uint8Array(n * m);
    const put = (x, y, p, b, h, a) => { if (y < 0 || y >= m) return; t.set(x, y, p, b, h, 1); alpha[y * n + ((x % n) + n) % n] = a == null ? 255 : a; };
    for (let x = 0; x < n; x++) {
      const u = (x + .5) / n;
      /* The bleach is BEDDED, not a wash. Whole beds go pale cream and whole beds do not, the
         chance rising as you go down, and the strip is transparent wherever a bed stays red — so
         the pale band reads as the wall's own bedding, salt-stained, and not as a slab of paint. */
      const top = Math.round(m * (.44 - st * .05) + (fb(u, .5, 3, 1, R + 1, 3) - .5) * m * .18 + (fb(u, .5, 11, 1, R + 2, 2) - .5) * m * .06);
      const notchY = Math.round(m * (feat === 'slump' ? 1.2 : .82) + (fb(u, .5, 4, 1, R + 3, 2) - .5) * m * .08);
      const depth = feat === 'cave' ? 8 : 3;
      /* a soft cliff has no salt-bleached beds and no notch at all — it has an apron of its own
         debris banked against the foot, and that is the whole difference at a till toe */
      if (feat === 'slump') {
        const ap = Math.round(m * .52 + (fb(u, .5, 3, 1, R + 15, 3) - .5) * m * .30 + (fb(u, .5, 10, 1, R + 16, 2) - .5) * m * .10);
        for (let y = Math.max(0, ap); y < m; y++) {
          const v = (y - ap) / Math.max(1, m - ap);
          if (hash2(x >> 1, y, R + 17) > .30 + v * .82) continue;
          put(x, y, grit, y === ap || hash2(x, y, R + 18) < .18 ? 3 : hash2(x, y, R + 19) < .3 ? 1 : 2, .3, v > .35 ? 255 : 190);
        }
        continue;
      }
      const sag = (fb(u, .5, 4, 1, R + 9, 3) - .5) * 9 + (fb(u, .5, 13, 1, R + 10, 2) - .5) * 3.4;
      for (let y = Math.max(0, top); y < notchY; y++) {
        const v = (y - top) / Math.max(1, notchY - top);
        const yb = Math.round(y + sag), k = FL(yb / 4);
        /* the bed's own chance, modulated ALONG the bed, so the bleach comes and goes down the
           shore; and it is a WASH at one flat alpha, not a repaint — the wall keeps its own beds,
           joints and sockets showing through, which is what the low-tide photographs actually show */
        const r = hash2(k, 1, R + 4) * .62 + fb(u, k / 12, 3, 1, R + 11 + (k & 7), 2) * .64;
        if (r > .10 + ss(.05, 1.0, v) * 1.18) continue;
        const pale = fb(u, y / m, 6, 4, R + 14, 2) > .58;
        put(x, y, bleach, pale ? 2 : 1, .1, v > .74 && hash2(k, 2, R + 13) < .55 ? 190 : 132);
      }
      for (let y = Math.max(0, notchY); y < m; y++) {
        if (y < notchY + depth) put(x, y, shade, y < notchY + depth * .55 ? 1 : 2, -1.4, y < notchY + depth * .55 ? 214 : 168);
        else if (y === notchY + depth) put(x, y, bleach, 3, .9, 220);       /* the lit lower lip */
        else if (hash2(x >> 1, y >> 1, R + 6) < .30) put(x, y, grit, hash2(x, y, R + 7) < .3 ? 1 : 2, .3);
      }
    }
    /* debris against the foot, and a wrack line on the lee faces */
    for (const s of sites(n, m, pk([12, 10, 8], st), .9, R + 20, s => s.v > .80 && s.r < .6)) {
      const w = 2 + Math.round(s.r2 * 4);
      stamp(t, stone(w + 1, w), s.x, s.y, { pal: grit, band: 2, seam: 1, tip: 1, h: 1.2, lock: 1 });
      for (let dy = -w; dy <= w; dy++) for (let dx = -w - 1; dx <= w + 1; dx++) {
        const xx = ((s.x + dx) % n + n) % n, yy = s.y + dy;
        if (yy >= 0 && yy < m && Math.hypot(dx / (w + 1), dy / w) <= 1) alpha[yy * n + xx] = 255;
      }
    }
    if (A.lee > .4) for (let x = 0; x < n; x++) {
      const y = m - 3 - FL(fb((x + .5) / n, .5, 6, 1, R + 30, 2) * 4);
      if (hash2(x, y, R + 31) < .45) { t.set(x, y, wrack, hash2(x, y, R + 32) < .4 ? 3 : 2, .4, 1); alpha[y * n + x] = 255; }
    }
    return decal(t, alpha, n, m, z, W, H);
  }
  /* a decal's four channels are one RGBA — these are fixed-sun by nature (their darks are cast
     shadow and occlusion, not N·L, so a normal map cannot relight them) */
  function decal(t, alpha, n, m, z, W, H) {
    const nr = P.normals(t, 1.6), out = new Uint8ClampedArray(W * H * 4), KEY = P.LIGHT.key;
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x, a = alpha[i]; if (!a) continue;
      const ndl = nr.nx[i] * KEY[0] + nr.ny[i] * KEY[1] + nr.nz[i] * KEY[2];
      let b = t.band[i]; if (ndl > .74) b++; else if (ndl < .40) b--;
      const c = t.pals[t.pal[i]][clamp(b, 0, 4)];
      for (let dy = 0; dy < z; dy++) for (let dx = 0; dx < z; dx++) {
        const o = ((y * z + dy) * W + x * z + dx) * 4;
        out[o] = c[0]; out[o + 1] = c[1]; out[o + 2] = c[2]; out[o + 3] = a;
      }
    }
    return { data: out, W, H };
  }

  root.PxCliffFace = { PPU, RELIEF_M, SLOPES, ROCKS, ASPECTS, ASPECT, DEF, TIERS, T0, SL,
    setSlope, setForm, form, formField, face, channels, profile, brow, toe, tierBase,
    keyFor, paletteLUT, ACC, XAN };
})(typeof globalThis !== 'undefined' ? globalThis : window);
