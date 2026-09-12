/* Hidden Harbours — PIXEL LANGUAGE.  The one vocabulary every pixel-first ground, cliff and rock
   bake speaks, so a shingle beach, a sandstone face and a boulder shade like the trees do.

   Borrowed verbatim from the tree rig (TreeRig3): the cold ambient + one warm key (COLD/WARM), the
   fixed upper-left KEY and back RIM vectors, the mask pack (R key light · G rim · B depth · A cover),
   the view-space normal pack, "no AA · no dither · binary alpha · ringless".

   What is new here is a TILE: a logical texel grid (n × m) carrying, per texel, a palette id, a band
   (0 dp · 1 sh · 2 mid · 3 hi · 4 key), a height, and a lock bit for authored marks the cleanup pass
   must not eat. Palettes are HUE-TRUE: each material keeps its own base colour and the bands are cut
   from it — darker bands cooled toward COLD, lighter bands warmed toward WARM — so the whole ground
   shares one temperature language without sharing one hue.

   Texel size is per material: a tile built at n and rendered at texel z ships as (n·z) px; the mask
   and normal are upscaled by the same z so all three channels sit on the same texel grid.

     PxLang.bands(base, c)             -> [dp, sh, mid, hi, key] hex (c = contrast, ground ≈ 0.8)
     new PxLang.Tile(n, m)             -> tile with pal/band/h/lock/a buffers, wrap-safe set/get
     PxLang.stamp(tile, stencil, x, y, o) o = {pal, band, h, seam, tip, lock, clip(x,y)}
     PxLang.sites(n, m, pitch, jit, seed) -> jittered lattice, sorted lower-last (paint order)
     PxLang.clean(tile, passes, keep)  -> 3×3 mode filter on (pal, band), locked texels immovable
     PxLang.normals(tile, relief)      -> {nx, ny, nz}   (y down in texture space)
     PxLang.render(tile, o)            -> RGBA at texel z; o.lit bands the key light into the albedo
     PxLang.packMask(tile, z)          -> RGBA  R key N·L · G rim · B height · A coverage
     PxLang.normalView(tile, z)        -> RGBA  R = x · G = y-up · B = out-of-surface
     PxLang.packBlend(tile, z, o)      -> RGBA  R coverage order · G kit height · B mark id · A 255

   MARKS.  Every authored mark (a cobble, a tussock, a puddle, a burrow) is stamped under one mark
   id, recorded per texel in tile.mark. A paint tool needs that: a material arriving at half weight
   must show half its marks WHOLE, never every mark at half opacity. beginMark/endMark bracket a
   mark; texels painted outside a bracket are matrix (id 0).

   HEIGHT is shared. packMask normalises height per tile (it is a lighting channel), but blending
   two materials against each other needs one ruler — packBlend puts height on H_KIT, the kit-wide
   texel range, so a cobble is taller than silt in absolute terms and a shader can say so.
   Runs in the run_script bake sandbox and in the browser. */
(function (root) {
  'use strict';
  const PPU = 32;
  const COLD = '#1d3b4a', WARM = '#e8b06a';
  const H_KIT = [-3, 9];                 // the kit's shared height ruler, in texels of the 1 px grid
  const nrm = (v) => { const L = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / L, v[1] / L, v[2] / L]; };
  const LIGHT = { key: nrm([-0.55, -0.66, 0.52]), rim: nrm([0.48, -0.28, -0.83]) };   // = TreeRig3.LIGHT
  function setKey(v) { LIGHT.key = nrm(v); }

  // ---- colour ----------------------------------------------------------------------------------
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r) => '#' + r.map(v => clamp(Math.round(v), 0, 255).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2r(a), B = h2r(b); return r2h([0, 1, 2].map(i => A[i] + (B[i] - A[i]) * t)); };
  const lum = (h) => { const c = h2r(h); return .299 * c[0] + .587 * c[1] + .114 * c[2]; };
  /* five bands cut from one base. dp and sh go toward black then toward the cold ambient; hi and
     key go toward the warm key. c scales the whole spread: 1 is the tree rig's own contrast, 0.8
     is right for a ground the sprites have to stand out against. */
  function bands(base, c) {
    c = c == null ? 1 : c;
    return [
      mix(mix(base, '#000000', .50 * c), COLD, .30 * c),
      mix(mix(base, '#000000', .24 * c), COLD, .16 * c),
      base,
      mix(base, WARM, .16 * c),
      mix(mix(base, '#ffffff', .12 * c), WARM, .30 * c),
    ];
  }

  // ---- hashing + periodic noise (everything wraps on the tile) -----------------------------------
  function hash2(x, y, s) { let h = (Math.imul(x | 0, 374761393) + Math.imul(y | 0, 668265263) + Math.imul(s | 0, 1442695041)) | 0; h ^= h >>> 13; h = Math.imul(h, 1274126177) | 0; h ^= h >>> 16; return (h >>> 0) / 4294967296; }
  const smooth = (t) => t * t * (3 - 2 * t);
  /* value noise on a lattice of `cx × cy` cells over u,v ∈ [0,1) — wraps by construction */
  function vnP(u, v, cx, cy, s) {
    const X = u * cx, Y = v * cy, xi = Math.floor(X), yi = Math.floor(Y), fx = smooth(X - xi), fy = smooth(Y - yi);
    const w = (a, b) => hash2(((a % cx) + cx) % cx, ((b % cy) + cy) % cy, s);
    const a = w(xi, yi), b = w(xi + 1, yi), c = w(xi, yi + 1), d = w(xi + 1, yi + 1);
    return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy;
  }
  function fbmP(u, v, cx, cy, s, oct) {
    let sum = 0, amp = 1, nr = 0; oct = oct || 3;
    for (let i = 0; i < oct; i++) { sum += amp * vnP(u, v, cx, cy, s + i * 131); nr += amp; amp *= .5; cx *= 2; cy *= 2; }
    return sum / nr;
  }
  /* periodic Worley: nearest and second-nearest site on a jittered cx×cy lattice */
  function worleyP(u, v, cx, cy, s, jit) {
    jit = jit == null ? 1 : jit;
    const X = u * cx, Y = v * cy, xi = Math.floor(X), yi = Math.floor(Y);
    let d1 = 9, d2 = 9, id = 0, sx = 0, sy = 0;
    for (let oy = -1; oy <= 1; oy++) for (let ox = -1; ox <= 1; ox++) {
      const gx = xi + ox, gy = yi + oy, wx = ((gx % cx) + cx) % cx, wy = ((gy % cy) + cy) % cy;
      const px = gx + .5 + (hash2(wx, wy, s) - .5) * jit, py = gy + .5 + (hash2(wx, wy, s + 7) - .5) * jit;
      const d = Math.hypot(X - px, Y - py);
      if (d < d1) { d2 = d1; d1 = d; id = wy * cx + wx + 1; sx = px; sy = py; } else if (d < d2) d2 = d;
    }
    return { d1, d2, id, sx: sx / cx, sy: sy / cy };
  }

  // ---- stencils --------------------------------------------------------------------------------
  function stencil(rows) {
    const h = rows.length, w = rows[0].length, px = [], has = new Set();
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (rows[y][x] !== '.') { px.push([x, y]); has.add(y * 64 + x); }
    const on = (x, y) => x >= 0 && y >= 0 && x < w && y < h && has.has(y * 64 + x);
    for (const p of px) { const [x, y] = p; p[2] = !on(x + 1, y) || !on(x, y + 1); p[3] = !on(x - 1, y) && !on(x, y - 1); }
    return { w, h, px, ox: w >> 1, oy: h >> 1 };
  }
  const mirror = (rows) => rows.map(r => r.split('').reverse().join(''));
  /* a filled ellipse w × h as a stencil — the cobble, the clod, the pebble */
  function ellipse(w, h) {
    const rows = [];
    for (let y = 0; y < h; y++) { let r = ''; for (let x = 0; x < w; x++) { const u = (x + .5) / w * 2 - 1, v = (y + .5) / h * 2 - 1; r += (u * u + v * v <= 1.02) ? 'o' : '.'; } rows.push(r); }
    return stencil(rows);
  }

  // ---- the tile ---------------------------------------------------------------------------------
  function Tile(n, m) {
    this.n = n; this.m = m || n; const N = this.n * this.m;
    this.pal = new Uint8Array(N); this.band = new Int8Array(N).fill(2); this.h = new Float32Array(N);
    this.lock = new Uint8Array(N); this.a = new Uint8Array(N).fill(255);
    this.mark = new Uint16Array(N); this.markSeq = 0; this.mid = 0;
    this.pals = [];
  }
  /* bracket one authored mark, so every texel it paints shares an id and a paint tool can bring the
     whole mark in at once. Nesting is not allowed — endMark always returns to matrix. */
  Tile.prototype.beginMark = function () { this.mid = (this.markSeq = (this.markSeq + 1) & 0xffff) || 1; return this.mid; };
  Tile.prototype.endMark = function () { this.mid = 0; };
  Tile.prototype.i = function (x, y) { const n = this.n, m = this.m; return (((y % m) + m) % m) * n + (((x % n) + n) % n); };
  Tile.prototype.set = function (x, y, pal, band, h, lock) {
    const i = this.i(x, y); if (this.lock[i] && !lock) return;
    this.pal[i] = pal; this.band[i] = clamp(band, 0, 4) | 0; if (h != null) this.h[i] = h; if (lock) this.lock[i] = 1;
    this.mark[i] = this.mid;
  };
  Tile.prototype.shift = function (x, y, d, lock) { const i = this.i(x, y); if (this.lock[i] && !lock) return; this.band[i] = clamp(this.band[i] + d, 0, 4); if (lock) this.lock[i] = 1; };
  Tile.prototype.addH = function (x, y, d) { this.h[this.i(x, y)] += d; };
  Tile.prototype.fill = function (pal, band, h) { this.pal.fill(pal); this.band.fill(band); this.h.fill(h || 0); };
  Tile.prototype.addPal = function (base, c) { this.pals.push(bands(base, c).map(h2r)); return this.pals.length - 1; };
  Tile.prototype.addRamp = function (hexes) { this.pals.push(hexes.map(h2r)); return this.pals.length - 1; };

  /* paint a stencil, the tree way: one flat band per stamp, the down/right seam one band darker,
     the key-ward (upper-left) tip one band lighter. h adds a dome of height so the normal map sees
     the stamp as a lump. Lower stamps paint over upper ones (sort sites by y first). */
  function stamp(tile, st, x, y, o) {
    o = o || {};
    const pal = o.pal | 0, band = o.band == null ? 2 : o.band, seam = o.seam == null ? 1 : o.seam, tip = o.tip == null ? 1 : o.tip;
    const hh = o.h || 0, lock = !!o.lock, rx = st.w / 2, ry = st.h / 2;
    for (const p of st.px) {
      let b = band;
      if (seam && p[2]) b -= seam; else if (tip && p[3]) b += tip;
      const px = x + p[0] - st.ox, py = y + p[1] - st.oy;
      if (o.clip && !o.clip(px, py)) continue;
      let dh = 0;
      if (hh) { const u = (p[0] + .5 - rx) / rx, v = (p[1] + .5 - ry) / ry; dh = hh * Math.sqrt(Math.max(0, 1 - (u * u + v * v) * .9)); }
      const i = tile.i(px, py);
      if (tile.lock[i] && !lock) continue;
      tile.pal[i] = pal; tile.band[i] = clamp(b, 0, 4); if (hh) tile.h[i] = (o.hBase == null ? tile.h[i] : o.hBase) + dh; if (lock) tile.lock[i] = 1;
      tile.mark[i] = tile.mid;
    }
  }

  /* jittered lattice over the tile, returned in paint order (upper first, lower last) */
  function sites(n, m, pitch, jit, seed, fn) {
    const out = [], cols = Math.max(1, Math.round(n / pitch)), rows = Math.max(1, Math.round(m / pitch));
    const pw = n / cols, ph = m / rows;
    for (let gy = 0; gy < rows; gy++) for (let gx = 0; gx < cols; gx++) {
      const r1 = hash2(gx, gy, seed), r2 = hash2(gx, gy, seed + 7), r3 = hash2(gx, gy, seed + 13), r4 = hash2(gx, gy, seed + 19);
      const x = Math.round(gx * pw + pw / 2 + (r1 - .5) * pw * jit), y = Math.round(gy * ph + ph / 2 + (r2 - .5) * ph * jit);
      const s = { x: ((x % n) + n) % n, y: ((y % m) + m) % m, u: (((x % n) + n) % n) / n, v: (((y % m) + m) % m) / m, r: r3, r2: r4, gx, gy };
      if (!fn || fn(s)) out.push(s);
    }
    out.sort((a, b) => a.y - b.y || a.x - b.x);
    return out;
  }

  /* 3×3 mode filter on the (pal, band) pair. A texel keeps its value if at least `keep` of its nine
     neighbours agree; otherwise it takes the neighbourhood's commonest value. Locked texels never
     change but still count. This is what turns per-texel noise into clusters. */
  function clean(tile, passes, keep) {
    const n = tile.n, m = tile.m; keep = keep == null ? 4 : keep; passes = passes == null ? 1 : passes;
    for (let p = 0; p < passes; p++) {
      const pal = new Uint8Array(tile.pal), band = new Int8Array(tile.band);
      const keys = new Int32Array(9), cnt = new Int32Array(9);
      for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
        const i = y * n + x; if (tile.lock[i]) continue;
        let k = 0;
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
          const j = (((y + dy) % m + m) % m) * n + (((x + dx) % n + n) % n), key = pal[j] * 16 + band[j] + 4;
          let f = -1; for (let q = 0; q < k; q++) if (keys[q] === key) { f = q; break; }
          if (f < 0) { keys[k] = key; cnt[k] = 1; k++; } else cnt[f]++;
        }
        const me = pal[i] * 16 + band[i] + 4;
        let mine = 0, best = 0; for (let q = 0; q < k; q++) { if (keys[q] === me) mine = cnt[q]; if (cnt[q] > cnt[best]) best = q; }
        if (mine < keep) { tile.pal[i] = (keys[best] >> 4); tile.band[i] = (keys[best] & 15) - 4; }
      }
    }
  }

  // ---- lighting -----------------------------------------------------------------------------------
  /* texture-space normals from the height field (height in texels, y down). relief scales the slope. */
  function normals(tile, relief) {
    const n = tile.n, m = tile.m, N = n * m, s = relief == null ? 1 : relief;
    const nx = new Float32Array(N), ny = new Float32Array(N), nz = new Float32Array(N), H = tile.h;
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x;
      const hx = H[y * n + (x + 1) % n] - H[y * n + (x + n - 1) % n], hy = H[((y + 1) % m) * n + x] - H[((y + m - 1) % m) * n + x];
      const a = -hx * .5 * s, b = -hy * .5 * s, L = Math.hypot(a, b, 1);
      nx[i] = a / L; ny[i] = b / L; nz[i] = 1 / L;
    }
    return { nx, ny, nz };
  }
  const dot3 = (nr, i, v) => nr.nx[i] * v[0] + nr.ny[i] * v[1] + nr.nz[i] * v[2];

  /* the albedo. o.lit bands the fixed key into it: N·L above o.hi steps the texel up a band, below
     o.lo steps it down — never more than one band, which is what keeps it pixel art and not a
     lightmap. o.z is the texel size. Returns RGBA. */
  function render(tile, o) {
    o = o || {};
    const n = tile.n, m = tile.m, z = o.z || 1, W = n * z, H = m * z, out = new Uint8ClampedArray(W * H * 4);
    const nr = o.lit ? (o.normals || normals(tile, o.relief)) : null, lo = o.lo == null ? .40 : o.lo, hi = o.hi == null ? .74 : o.hi;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = ((y / z) | 0) * n + ((x / z) | 0), o4 = (y * W + x) * 4;
      let b = tile.band[i];
      if (nr) { const l = dot3(nr, i, LIGHT.key); if (l > hi) b++; else if (l < lo) b--; }
      const c = tile.pals[tile.pal[i]][clamp(b, 0, 4)];
      out[o4] = c[0]; out[o4 + 1] = c[1]; out[o4 + 2] = c[2]; out[o4 + 3] = tile.a[i];
    }
    return out;
  }
  /* mask pack — same channels as TreeRig3.packMask: R key light (N·L at the fixed key), G back rim,
     B height (0 = the tile's lowest texel), A coverage */
  /* nr: optional precomputed normals (a rig with real facets — the iso cliff — hands in view-space ones);
     hRange: optional [h0, h1] so a family of tiles shares one height scale */
  function packMask(tile, z, relief, nr, hRange) {
    z = z || 1; const n = tile.n, m = tile.m, W = n * z, H = m * z, out = new Uint8ClampedArray(W * H * 4); nr = nr || normals(tile, relief);
    let h0 = 1e9, h1 = -1e9;
    if (hRange) { h0 = hRange[0]; h1 = hRange[1]; } else for (let i = 0; i < n * m; i++) { if (tile.h[i] < h0) h0 = tile.h[i]; if (tile.h[i] > h1) h1 = tile.h[i]; }
    const hs = h1 > h0 ? 255 / (h1 - h0) : 0;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = ((y / z) | 0) * n + ((x / z) | 0), o4 = (y * W + x) * 4;
      out[o4] = clamp(dot3(nr, i, LIGHT.key), 0, 1) * 255; out[o4 + 1] = clamp(dot3(nr, i, LIGHT.rim), 0, 1) * 255;
      out[o4 + 2] = clamp((tile.h[i] - h0) * hs, 0, 255); out[o4 + 3] = tile.a[i];
    }
    return out;
  }
  /* normal pack — R = x, G = y-UP, B = out of the surface (toward the camera for a plan tile, out
     of the wall for a face), same encoding as TreeRig3.normalView */
  function normalView(tile, z, relief, nr) {
    z = z || 1; const n = tile.n, m = tile.m, W = n * z, H = m * z, out = new Uint8ClampedArray(W * H * 4); nr = nr || normals(tile, relief);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = ((y / z) | 0) * n + ((x / z) | 0), o4 = (y * W + x) * 4;
      out[o4] = (nr.nx[i] * .5 + .5) * 255; out[o4 + 1] = (-nr.ny[i] * .5 + .5) * 255; out[o4 + 2] = (nr.nz[i] * .5 + .5) * 255; out[o4 + 3] = tile.a[i];
    }
    return out;
  }
  /* blend pack — what a paint tool needs to lay this material over another WITHOUT averaging two
     palettes (averaging is what made pass two's cross-fades mud, and it invents off-palette colour).

       R  coverage order 1–255.  As the splat weight w rises, a texel belongs to this material once
          w·255 ≥ R. Every texel of one authored mark carries ONE value, so marks arrive whole. The
          order is organised by a single wrapping field, so a half-painted patch is a blotch with a
          wandering frontier, not a dither screen. o.markLead says which arrives first: 1 = the marks
          lead (a few cobbles land on the sand before the grit does), 0 = the matrix leads (silt
          closes over the flat and its burrows come last), 0.5 = no preference.
       G  height on H_KIT, the kit's shared ruler — comparable between materials, unlike _mask's B.
       B  mark id hash (0 = matrix), so a tool can stagger marks or take whole marks only.
       A  255. A material at full weight covers.                                                    */
  function packBlend(tile, z, o) {
    o = o || {}; z = z || 1;
    const n = tile.n, m = tile.m, N = n * m, W = n * z, H = m * z, out = new Uint8ClampedArray(W * H * 4);
    const lead = o.markLead == null ? .5 : o.markLead, cells = o.grain || 5, sd = (o.seed || 0) + 401;
    const cx = Math.max(1, Math.round(cells)), cy = Math.max(1, Math.round(cells * m / n));
    const fld = new Float32Array(N);
    let f0 = 1e9, f1 = -1e9;
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) { const v = fbmP((x + .5) / n, (y + .5) / m, cx, cy, sd, 3); fld[y * n + x] = v; if (v < f0) f0 = v; if (v > f1) f1 = v; }
    const fs = f1 > f0 ? 1 / (f1 - f0) : 0, fN = i => (fld[i] - f0) * fs;
    /* per mark: its tallest texel (tall marks earn an earlier slot) and where it sits in the field */
    const mhi = new Map(), msum = new Map(), mcnt = new Map();
    for (let i = 0; i < N; i++) {
      const k = tile.mark[i]; if (!k) continue;
      const h = tile.h[i]; if (!(mhi.get(k) >= h)) mhi.set(k, h);
      msum.set(k, (msum.get(k) || 0) + fN(i)); mcnt.set(k, (mcnt.get(k) || 0) + 1);
    }
    let h0 = 1e9, h1 = -1e9;
    for (const v of mhi.values()) { if (v < h0) h0 = v; if (v > h1) h1 = v; }
    const hs = h1 > h0 ? 1 / (h1 - h0) : 0;
    const mkLo = 8 + (1 - lead) * 32, mkHi = 200 + (1 - lead) * 55, mxLo = 8 + lead * 32, mxHi = 200 + lead * 55;
    const ordM = new Map();
    for (const k of mhi.keys()) {
      const f = msum.get(k) / mcnt.get(k), tall = (mhi.get(k) - h0) * hs;
      const u = clamp(.62 * f + .26 * (1 - tall) + .12 * hash2(k, 17, sd), 0, 1);
      ordM.set(k, clamp(Math.round(mkLo + u * (mkHi - mkLo)), 1, 255));
    }
    const hk = 255 / (H_KIT[1] - H_KIT[0]);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const tx = (x / z) | 0, ty = (y / z) | 0, i = ty * n + tx, o4 = (y * W + x) * 4, k = tile.mark[i];
      let r;
      if (k) r = ordM.get(k);
      else { const u = clamp(.80 * fN(i) + .20 * hash2(tx, ty, sd + 9), 0, 1); r = clamp(Math.round(mxLo + u * (mxHi - mxLo)), 1, 255); }
      out[o4] = r;
      out[o4 + 1] = clamp((tile.h[i] - H_KIT[0]) * hk, 0, 255);
      out[o4 + 2] = k ? 1 + Math.floor(hash2(k, 23, sd) * 254) : 0;
      out[o4 + 3] = 255;
    }
    return out;
  }
  function toCanvas(rgba, W, H, createCanvas) {
    const cv = createCanvas(W, H); cv.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(rgba), W, H), 0, 0); return cv;
  }

  root.PxLang = { PPU, COLD, WARM, H_KIT, LIGHT, setKey, clamp, h2r, r2h, mix, lum, bands, hash2, vnP, fbmP, worleyP, smooth,
    stencil, mirror, ellipse, Tile, stamp, sites, clean, normals, render, packMask, packBlend, normalView, toCanvas };
})(typeof globalThis !== 'undefined' ? globalThis : window);
