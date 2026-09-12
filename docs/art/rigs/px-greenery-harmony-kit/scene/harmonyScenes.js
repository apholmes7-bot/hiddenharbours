/* Hidden Harbours — HARMONY SCENES.  Four habitats composed from the shipped rigs, relit four ways
   off one bake.

   WHY A COMPOSITOR AND NOT FOUR DRAWINGS.  The point of the page is that the greenery, the pixel
   terrain pack and the cliff face rig share one light. So a scene is not painted per lighting
   scenario — it is baked ONCE into geometry-and-albedo buffers (palette id · band · normal · height
   · sprite flag), and each scenario is a pass over those buffers with a different key vector. If a
   bush looks wrong at dusk, the bush's normals are wrong, not its dusk painting.

   THE BUFFERS (one texel = one screen pixel at z = 1; the kit's floor detail 2 is on that grid):
     pal/band   the authored, UNLIT colour — palette index into S.pals, band 0-4
     nx,ny,nz   texture-space normal (y down), from each contributor's own height field
     h          height in texels, shared ruler: floors ~0-3, cliff form ~0-40
     spr        1 = sprite pixel, 0 = floor. Cast shadows only land on the floor.

   THE RELIGHT, per scenario:
     1. terrain self-shadow — a horizon march over h along the key's ground track. Low sun = long
        shadow off the cliff's own form field, for free.
     2. sprite cast shadow — every opaque sprite texel is projected onto the ground plane by its
        height above its own pivot times the scenario's shear. This is the 2.5D shadow; a height
        march cannot produce it, because a bush is not relief in the floor.
     3. N·L, quantised to five steps BEFORE grading, then one band step up or down. Five steps ×
        five bands is a finite palette, so the output is still flat pixel art and not a lightmap.

   No AA · no dither · binary alpha · ringless. */
(function (root) {
  'use strict';
  const P = root.PxLang, K = root.PxKit, CF = root.PxCliffFace, SP = root.ShorePlants;
  if (!P || !K) throw new Error('harmonyScenes: pixelLanguage.js + pxKit.js must load first');
  const { Tile, normals, hash2, fbmP, clamp, h2r, mix } = P;

  /* ------------------------------------------------------------------ the four lighting scenarios
     L      key vector in texture space (x right, y DOWN, z out of the floor)
     sx,sy  ground shear per texel of sprite height — where a sprite's shadow is thrown
     kc/ac  key and ambient colour · ka/aa how far the grade pulls · rim the back light  */
  const LIGHTS = [
    { key: 'dawn', name: 'Low dawn', time: '05:40',
      note: 'Sun barely off the water in the east. Shadows run west, nearly flat and very long; the fill is the cold sky, so everything out of the key goes blue before it goes dark.',
      L: [0.80, -0.30, 0.28], sx: -1.55, sy: 0.44, kc: '#f7b183', ac: '#2b3f66', ka: 0.30, aa: 0.42, tgt: 0.54,
      rim: [-0.62, -0.24, -0.74], rc: '#8fb6d8', ra: 0.26, amb: 0.52, wash: '#3d4f80', wa: 0.20 },
    { key: 'noon', name: 'Noon', time: '12:20',
      note: 'The authored key — upper-left, steep. This is the reference every sheet in the kit was painted against; the other three are the same texels under a different sun.',
      L: [-0.55, -0.66, 0.52], sx: 0.48, sy: 0.44, kc: '#fff0cf', ac: '#1d3b4a', ka: 0.16, aa: 0.30, tgt: 0.62,
      rim: [0.48, -0.28, -0.83], rc: '#bcd6e2', ra: 0.14, amb: 0.62, wash: '#fff4dd', wa: 0.03 },
    { key: 'golden', name: 'Golden hour', time: '19:05',
      note: 'Low from the west. Long shadows east, and the key is warm enough to push the greens amber where it lands — the turf and the sandstone come within a value of each other.',
      L: [-0.82, -0.26, 0.24], sx: 1.70, sy: 0.40, kc: '#ffae55', ac: '#3a3358', ka: 0.34, aa: 0.40, tgt: 0.56,
      rim: [0.66, -0.22, -0.72], rc: '#ffc98a', ra: 0.30, amb: 0.50, wash: '#ff9a44', wa: 0.17 },
    { key: 'dusk', name: 'Dusk', time: '21:15',
      note: 'The key is spent — dim, low and warm; the sky does most of the lighting, so shadows are blue rather than black and the rim is the strongest light on any vertical face.',
      L: [-0.48, -0.30, 0.26], sx: 1.05, sy: 0.36, kc: '#d98a5a', ac: '#22304e', ka: 0.20, aa: 0.62, tgt: 0.38,
      rim: [0.58, -0.30, -0.76], rc: '#9fc0e8', ra: 0.42, amb: 0.44, wash: '#25355c', wa: 0.34 },
  ];

  // ------------------------------------------------------------------------------ the scene buffer
  function Scene(n, m) {
    const N = n * m;
    this.n = n; this.m = m;
    this.pal = new Uint16Array(N); this.band = new Int8Array(N).fill(2);
    this.nx = new Float32Array(N); this.ny = new Float32Array(N); this.nz = new Float32Array(N).fill(1);
    this.h = new Float32Array(N); this.a = new Uint8Array(N); this.spr = new Uint8Array(N);
    this.pals = []; this.sprites = [];
  }
  Scene.prototype.addPals = function (list) { const off = this.pals.length; for (const p of list) this.pals.push(p); return off; };

  /* blit any PxLang tile. sprite:1 records the pixel as sprite (no cast shadow lands on it) and
     keeps the source tile in the shadow list so the relight can project it. */
  function blitTile(S, T, ox, oy, o) {
    o = o || {};
    const off = S.addPals(T.pals), nr = o.nr || normals(T, o.relief == null ? 1 : o.relief);
    const hs = o.hScale == null ? 1 : o.hScale, hb = o.hBias || 0, spr = o.sprite ? 1 : 0;
    for (let y = 0; y < T.m; y++) for (let x = 0; x < T.n; x++) {
      const i = y * T.n + x; if (!T.a[i]) continue;
      const px = ox + x, py = oy + y; if (px < 0 || py < 0 || px >= S.n || py >= S.m) continue;
      const j = py * S.n + px;
      S.pal[j] = T.pal[i] + off; S.band[j] = T.band[i]; S.a[j] = 255; S.spr[j] = spr;
      S.nx[j] = nr.nx[i]; S.ny[j] = nr.ny[i]; S.nz[j] = nr.nz[i];
      S.h[j] = T.h[i] * hs + hb;
    }
  }
  /* a sprite: bottom-centre pivot, y-sorted by the caller, registered for cast shadow */
  function place(S, sp, x, y, o) {
    o = o || {};
    const T = sp.tile, ox = Math.round(x - sp.pivot[0]), oy = Math.round(y - sp.pivot[1]);
    blitTile(S, T, ox, oy, { relief: sp.relief, sprite: 1, hScale: 0.25, hBias: o.hBias || 0 });
    S.sprites.push({ T, ox, oy, pv: sp.pivot, soft: o.soft == null ? 1 : o.soft });
  }
  /* a sprite that arrives as raw RGBA + normals (the shore plant rig ships lit pixels, not a tile).
     NOTE its pivot is an OBJECT {x,y}, not the [x,y] pair the kit's sprites use. */
  function placeRGBA(S, res, x, y, o) {
    o = o || {};
    const pv = [res.pivot.x, res.pivot.y];
    const w = res.w, hgt = res.h, rgba = res.rgba, nv = SP.normalView(res);
    const ox = Math.round(x - pv[0]), oy = Math.round(y - pv[1]);
    /* one palette per plant: the plant's own pixels become band-2 entries of a 5-band ramp cut from
       each colour, so the relight can step them like everything else */
    const seen = new Map();
    const idx = new Int32Array(w * hgt).fill(-1);
    const bnd = new Uint8Array(w * hgt).fill(2);
    /* DE-RING. The shore rig outlines every sprite with KEYLINE #101d21; the pixel kit is ringless
       by rule. Composited as shipped, a rockweed is a black-edged sticker lying on a floor that has
       no black in it anywhere — the single loudest disagreement between the two families. The ring
       is not deleted (that would open the silhouette); it is re-cut as band 0 of the plant's own
       dominant colour, which is what a ringless dark edge is. */
    const hist = new Map();
    for (let i = 0; i < w * hgt; i++) {
      if (rgba[i * 4 + 3] < 128) continue;
      const r = rgba[i * 4], g = rgba[i * 4 + 1], b = rgba[i * 4 + 2];
      if (0.299 * r + 0.587 * g + 0.114 * b < 46) continue;
      const k = (r << 16) | (g << 8) | b;
      hist.set(k, (hist.get(k) || 0) + 1);
    }
    let dom = 0, domN = -1;
    for (const [k, c] of hist) if (c > domN) { domN = c; dom = k; }
    const domPal = S.pals.length;
    S.pals.push(P.bands('#' + dom.toString(16).padStart(6, '0'), 0.62).map(h2r));
    for (let i = 0; i < w * hgt; i++) {
      if (rgba[i * 4 + 3] < 128) continue;
      const r = rgba[i * 4], g = rgba[i * 4 + 1], b = rgba[i * 4 + 2];
      if (0.299 * r + 0.587 * g + 0.114 * b < 46) { idx[i] = domPal; bnd[i] = 0; continue; }
      const k = (r << 16) | (g << 8) | b;
      let p = seen.get(k);
      if (p == null) { p = S.pals.length; S.pals.push(P.bands('#' + k.toString(16).padStart(6, '0'), 0.62).map(h2r)); seen.set(k, p); }
      idx[i] = p;
    }
    const T = { n: w, m: hgt, a: new Uint8Array(w * hgt), pivot: pv };
    for (let y = 0; y < hgt; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (idx[i] < 0) continue;
      T.a[i] = 255;
      const px = ox + x, py = oy + y; if (px < 0 || py < 0 || px >= S.n || py >= S.m) continue;
      const j = py * S.n + px;
      S.pal[j] = idx[i]; S.band[j] = bnd[i]; S.a[j] = 255; S.spr[j] = 1;
      S.nx[j] = nv[i * 4] / 127.5 - 1; S.ny[j] = -(nv[i * 4 + 1] / 127.5 - 1); S.nz[j] = nv[i * 4 + 2] / 127.5 - 1;
      S.h[j] = 0.4;
    }
    S.sprites.push({ T, ox, oy, pv, soft: o.soft == null ? 1 : o.soft });
  }

  /* the cliff face: geometry only. Its tier system carries the form, its `unl` branch carries the
     colour with no key in it, and its form field goes into the scene height so OUR march throws the
     macro shadow — the rig's own baked one would be stuck at noon. */
  function blitCliff(S, b, ox, oy, hBias) {
    const { tile: t, n, m, tierU, cav, nx, ny, nz, mf } = b, T0 = 3, rockPal = b.tiers[T0];
    const off = S.addPals(t.pals);
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x, px = ox + x, py = oy + y;
      if (px < 0 || py < 0 || px >= S.n || py >= S.m) continue;
      const j = py * S.n + px, isRock = t.pal[i] === rockPal;
      let bu = t.band[i] + (isRock ? 0 : tierU[i]);
      if (cav[i] > 0.80) bu++; else if (cav[i] < 0.22) bu--;
      const pu = isRock ? b.tiers[clamp(tierU[i] + T0, 0, 5)] : t.pal[i];
      S.pal[j] = pu + off; S.band[j] = clamp(bu, 0, 4); S.a[j] = 255; S.spr[j] = 0;
      S.nx[j] = nx[i]; S.ny[j] = ny[i]; S.nz[j] = nz[i];
      S.h[j] = (hBias || 0) + t.h[i] * 0.35 + mf[i] * 26;
    }
  }

  // -------------------------------------------------------------------------------- floor painting
  /* paint a set of PxKit materials into one tile by region predicate, then run the kit's own edge
     painter between them so the seams are the shipped ones */
  function floor(S, n, m, defs, sd) {
    const t = new Tile(n, m), reg = new Uint8Array(n * m).fill(255), mats = [];
    defs.forEach((d, k) => {
      mats.push(K.paintRegion(t, d.key, d.step | 0, d.R, d.seed, 2));
      for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) if (d.R(x, y)) reg[y * n + x] = k;
    });
    K.edges(K.makeCtx(t, null, { detail: 2 }), reg, mats, sd || 11);
    return { tile: t, reg, mats };
  }
  /* flat water — the kit has no water material; a cool plane with a ripple nap and a lit crest.
     Painted straight into the floor tile so it shares the scene's one palette list. */
  function water(t, R, sd) {
    const deep = t.addPal('#2d4e5c', 0.85), mid = t.addPal('#3d6470', 0.8), fm = t.addPal('#9fb6b4', 0.7);
    for (let y = 0; y < t.m; y++) for (let x = 0; x < t.n; x++) {
      if (!R(x, y)) continue;
      /* ripple LINES, not blobs: a low-frequency field only bends a row-wise wave. A plain fbm at
         this aspect gave big soft clouds that read as sky, not sea. */
      const f = fbmP(x / t.n, y / t.m, 7, 2, sd, 3);
      const rip = Math.sin(y * 1.55 + f * 6.5 + Math.sin(x / 14) * 1.3);
      t.set(x, y, rip > 0.30 ? mid : deep, rip > 0.80 ? 3 : rip > 0.05 ? 2 : 1, 0);
      if (rip > 0.94 && hash2(x, y, sd + 3) < 0.45) t.set(x, y, fm, 4, 0.2);
    }
  }

  // ------------------------------------------------------------------------------------- relighting
  const SHSTEP = [1, 2, 3, 5, 7, 10, 14, 19, 25, 32, 40, 50];
  function terrainShadow(S, L) {
    const n = S.n, m = S.m, out = new Float32Array(n * m);
    const lh = Math.hypot(L[0], L[1]) || 1e-4, ux = L[0] / lh, uy = L[1] / lh, rate = L[2] / lh;
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const i = y * n + x, d0 = S.h[i]; let s = 0;
      for (let q = 0; q < SHSTEP.length; q++) {
        const k = SHSTEP[q], sx = x + Math.round(k * ux), sy = y + Math.round(k * uy);
        if (sx < 0 || sy < 0 || sx >= n || sy >= m) break;
        const over = S.h[sy * n + sx] - (d0 + k * rate);
        if (over > 0) { const p = clamp(over / 3.0, 0, 1); if (p > s) s = p; }
      }
      out[i] = s;
    }
    return out;
  }
  /* project every sprite texel onto the ground by its height above its own pivot */
  function castShadow(S, lt) {
    const n = S.n, m = S.m, out = new Float32Array(n * m);
    for (const s of S.sprites) {
      const T = s.T, pvy = s.pv[1];
      for (let y = 0; y < T.m; y++) for (let x = 0; x < T.n; x++) {
        if (!T.a[y * T.n + x]) continue;
        const hgt = pvy - y; if (hgt <= 0) continue;
        const gx = Math.round(s.ox + x + hgt * lt.sx), gy = Math.round(s.oy + pvy + hgt * lt.sy - hgt * 0.06);
        if (gx < 0 || gy < 0 || gx >= n || gy >= m) continue;
        out[gy * n + gx] = 1;
      }
    }
    /* CLOSE IT UP. A per-texel projection of a leafy canopy lands as a stipple — at a low sun the
       shear spreads the same 30 rows over 60 columns, so every shadow came out a scribble of holes
       instead of a shape. Two fill passes (a hole with three shadowed neighbours is inside the
       shadow), then one texel of penumbra around the closed silhouette. */
    for (let p = 0; p < 2; p++) {
      const a0 = new Float32Array(out);
      for (let y = 1; y < m - 1; y++) for (let x = 1; x < n - 1; x++) {
        const i = y * n + x; if (a0[i] >= 1) continue;
        if (a0[i - 1] + a0[i + 1] + a0[i - n] + a0[i + n] >= 3) out[i] = 1;
      }
    }
    const soft = new Float32Array(out);
    for (let y = 1; y < m - 1; y++) for (let x = 1; x < n - 1; x++) {
      const i = y * n + x; if (out[i]) continue;
      if (out[i - 1] + out[i + 1] + out[i - n] + out[i + n] >= 2) soft[i] = 0.5;
    }
    return soft;
  }
  function relight(S, lt, z) {
    const n = S.n, m = S.m, W = n * (z || 1), H = m * (z || 1), out = new Uint8ClampedArray(W * H * 4);
    const L = lt.L, R = lt.rim, ts = terrainShadow(S, L), cs = castShadow(S, lt);
    const kc = h2r(lt.kc), ac = h2r(lt.ac), rc = h2r(lt.rc), wc = h2r(lt.wash || '#ffffff'), wa = lt.wa || 0;
    /* EXPOSURE. A flat floor facing the camera has N = (0,0,1), so its N·L is just the key's z —
       0.24 at golden hour against 0.52 at noon. Ungraded, every low sun renders the whole frame two
       bands down and the scene is mud. tgt is where a flat floor should sit; the scenario is
       exposed to it, so relief and cast shadow still carry the difference and the COLOUR carries
       the hour. */
    const expo = (lt.tgt || 0.6) / Math.max(0.05, L[2]);
    const cache = new Map();
    const px = new Uint8ClampedArray(n * m * 4);
    for (let i = 0; i < n * m; i++) {
      if (!S.a[i]) continue;
      const nl = clamp((S.nx[i] * L[0] + S.ny[i] * L[1] + S.nz[i] * L[2]) * expo, 0, 1);
      const sh = Math.max(ts[i], S.spr[i] ? 0 : cs[i]);
      const q = Math.round(clamp(nl * (1 - 0.52 * sh), 0, 1) * 4);
      const rl = clamp(S.nx[i] * R[0] + S.ny[i] * R[1] + S.nz[i] * R[2], 0, 1);
      const rq = (rl > 0.60 && q < 2) ? 1 : 0;
      const sp = S.spr[i];
      const ck = (((S.pal[i] * 5 + S.band[i]) * 5 + q) * 2 + rq) * 2 + sp;
      let c = cache.get(ck);
      if (c === undefined) {
        const u = q / 4, ramp = S.pals[S.pal[i]];
        /* a sprite's authored bands ALREADY carry its form — the leaf stamps were banded off the
           lobe normal when the sheet was built. Stepping it again by the full range double-shades
           the canopy into a smear, which is what the first relight did. Sprites get ±1. */
        const db = sp ? (q >= 4 ? 1 : q >= 2 ? 0 : -1)
          : (q >= 4 ? 1 : q >= 2 ? 0 : q >= 1 ? -1 : -2);
        const base = ramp[clamp(S.band[i] + db, 0, 4)];
        let r = base[0], g = base[1], b = base[2];
        const ta = lt.aa * (1 - u) * (1 - lt.amb * 0.35), tk = lt.ka * u;
        r += (ac[0] - r) * ta; g += (ac[1] - g) * ta; b += (ac[2] - b) * ta;
        r += (kc[0] - r) * tk; g += (kc[1] - g) * tk; b += (kc[2] - b) * tk;
        if (rq) { r += (rc[0] - r) * lt.ra; g += (rc[1] - g) * lt.ra; b += (rc[2] - b) * lt.ra; }
        /* the WASH: one flat pull of the whole frame toward the hour's colour, on top of the
           lit/shadow grade. Without it every scenario exposes its flat ground to the same q and the
           four renders come out near-identical — the grade alone only separates the extremes. */
        if (wa) { r += (wc[0] - r) * wa; g += (wc[1] - g) * wa; b += (wc[2] - b) * wa; }
        c = [r, g, b]; cache.set(ck, c);
      }
      px[i * 4] = c[0]; px[i * 4 + 1] = c[1]; px[i * 4 + 2] = c[2]; px[i * 4 + 3] = 255;
    }
    if ((z || 1) === 1) return px;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = (((y / z) | 0) * n + ((x / z) | 0)) * 4, o = (y * W + x) * 4;
      out[o] = px[i]; out[o + 1] = px[i + 1]; out[o + 2] = px[i + 2]; out[o + 3] = px[i + 3];
    }
    return out;
  }
  /* the two channels the page puts under the scenes, so the light maps are visible as maps */
  function normalPNG(S, z) {
    const n = S.n, m = S.m, W = n * z, H = m * z, out = new Uint8ClampedArray(W * H * 4);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = ((y / z) | 0) * n + ((x / z) | 0), o = (y * W + x) * 4;
      out[o] = (S.nx[i] * 0.5 + 0.5) * 255; out[o + 1] = (-S.ny[i] * 0.5 + 0.5) * 255;
      out[o + 2] = (S.nz[i] * 0.5 + 0.5) * 255; out[o + 3] = S.a[i] ? 255 : 0;
    }
    return out;
  }
  function heightPNG(S, z) {
    const n = S.n, m = S.m, W = n * z, H = m * z, out = new Uint8ClampedArray(W * H * 4);
    let h0 = 1e9, h1 = -1e9;
    for (let i = 0; i < n * m; i++) { if (S.h[i] < h0) h0 = S.h[i]; if (S.h[i] > h1) h1 = S.h[i]; }
    const hs = h1 > h0 ? 255 / (h1 - h0) : 0;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = ((y / z) | 0) * n + ((x / z) | 0), o = (y * W + x) * 4, v = (S.h[i] - h0) * hs;
      out[o] = v; out[o + 1] = v; out[o + 2] = v; out[o + 3] = S.a[i] ? 255 : 0;
    }
    return out;
  }

  // ------------------------------------------------------------------------------------ the scenes
  const N = 320, M = 200;
  const wig = (x, a, c, s, base) => base + Math.round(a * (fbmP(x / N, 0.5, c, 1, s, 3) - 0.5) * 2);

  const SCENES = {
    /* ------------------------------------------------------------------------------- 1 cliff brow */
    cliffbrow: {
      name: 'Cliff brow', sub: 'sandstone face · turf lip · the greenery crowding the edge',
      note: 'The turf runs to the drop and stops. Bayberry and juniper take the lip because nothing taller survives the salt; the flowers sit back where the sod is deeper. The face below is the cliff rig at its own texel, and the toe is the terrain pack\'s shingle with the kit\'s boulders on it.',
      build() {
        const S = new Scene(N, M), lip = x => wig(x, 9, 3, 41, 104);
        const f = floor(S, N, M, [
          { key: 'grass', step: 1, R: (x, y) => y < lip(x), seed: 3070 },
          { key: 'shingle', step: 1, R: (x, y) => y >= 172 && y < 191, seed: 2110 },
          { key: 'ledge', step: 1, R: (x, y) => y >= 191, seed: 1630 },
        ], 21);
        water(f.tile, (x, y) => y >= 191 && y > 192 + 3 * Math.sin(x / 22), 77);
        blitTile(S, f.tile, 0, 0, { relief: 1.3 });
        const cliff = CF.face('sandstone', 'S', 1, { z: 1, W: N, H: 74, seed: 7703, slope: 'steep' });
        blitCliff(S, cliff, 0, 100, 2);
        /* the lip: the sod overhangs its own face — a few rows of turf carried past the brow and a
           dark root row under them. Without it the grass stops dead on one row like a cut. Painted
           column by column into ONE tile, then blitted once. */
        const top = 96, TH = 16, tf = new Tile(N, TH); tf.a.fill(0);
        const turf = tf.addPal('#485d2f', 0.75), root = tf.addPal('#5a4130', 0.8);
        for (let x = 0; x < N; x++) {
          const L = lip(x) - top, d = 3 + Math.round(2.4 * fbmP(x / N, 0.2, 14, 1, 61, 3));
          for (let k = 0; k < d; k++) {
            const r = L + k; if (r < 0 || r >= TH) continue;
            const i = r * N + x;
            tf.a[i] = 255; tf.pal[i] = turf; tf.band[i] = k === 0 ? 3 : k >= d - 1 ? 0 : 2; tf.h[i] = 2.4 - k * 0.5;
          }
          if (hash2(x, 1, 61) < 0.34) { const r = L + d; if (r >= 0 && r < TH) { const i = r * N + x; tf.a[i] = 255; tf.pal[i] = root; tf.band[i] = 1; tf.h[i] = 0.4; } }
        }
        blitTile(S, tf, 0, top, {});
        const put = [];
        [['bayberry', 0, 38, 92], ['juniper', 1, 96, 98], ['bayberry', 2, 154, 88], ['wildrose', 0, 214, 96], ['juniper', 3, 270, 94], ['blueberry', 1, 302, 80]]
          .forEach(([k, v, x, y]) => put.push({ y, f: () => place(S, K.bush(k, v), x, y) }));
        [['daisy', 'patch', 3, 62, 62], ['buttercup', 'clump', 1, 124, 50], ['daisy', 'clump', 5, 190, 66], ['lupin', 'single', 2, 246, 54], ['buttercup', 'patch', 7, 294, 60], ['queenanne', 'single', 4, 16, 72]]
          .forEach(([k, t, s, x, y]) => put.push({ y, f: () => place(S, K.flower(k, t, s), x, y) }));
        [['sandstone', 22, 3, 40, 186], ['granite', 15, 5, 120, 182], ['sandstone', 28, 9, 232, 188], ['granite', 12, 2, 286, 179]]
          .forEach(([k, sz, sd, x, y]) => put.push({ y, f: () => place(S, K.rock(k, sz, sd), x, y) }));
        put.sort((a, b) => a.y - b.y).forEach(p => p.f());
        return S;
      },
    },

    /* ------------------------------------------------------------------------------ 2 dune & beach */
    dune: {
      name: 'Dune & beach', sub: 'marram · wrack line · rose and bayberry back from the sand',
      note: 'Foreground is the dune: marram clumps from the shore rig, wild rose and bayberry from the kit, beach pea running flat over the sand. The wrack line is the kit\'s own scatter, and it is the thing that tells you where the last high water reached.',
      build() {
        const S = new Scene(N, M), crest = x => wig(x, 13, 3, 77, 104);
        const f = floor(S, N, M, [
          { key: 'sand', step: 1, R: (x, y) => y >= 26 && y < crest(x), seed: 5510 },
          { key: 'grass', step: 2, R: (x, y) => y >= crest(x), seed: 3071 },
        ], 33);
        water(f.tile, (x, y) => y < 26 + 3 * Math.sin(x / 17), 51);
        /* the wrack line: the kit's strew, on the sand only, along a wandering row */
        const wl = x => 52 + Math.round(4 * Math.sin(x / 26) + 4 * (fbmP(x / N, 0.7, 5, 1, 12, 3) - 0.5) * 2);
        K.wrack(K.makeCtx(f.tile, (x, y) => Math.abs(y - wl(x)) <= 3 && y > 28, { detail: 2 }), 9);
        blitTile(S, f.tile, 0, 0, { relief: 1.15 });
        const put = [];
        [['wildrose', 1, 40, 150], ['bayberry', 3, 116, 168], ['wildrose', 2, 210, 158], ['bayberry', 0, 286, 144], ['blueberry', 2, 158, 136], ['blueberry', 0, 252, 192]]
          .forEach(([k, v, x, y]) => put.push({ y, f: () => place(S, K.bush(k, v), x, y) }));
        [['daisy', 'clump', 2, 78, 188], ['buttercup', 'patch', 4, 236, 178], ['daisy', 'single', 8, 310, 132]]
          .forEach(([k, t, s, x, y]) => put.push({ y, f: () => place(S, K.flower(k, t, s), x, y) }));
        if (SP) {
          [['MarramGrass', 0, 24, 126], ['MarramGrass', 2, 68, 116], ['MarramGrass', 1, 132, 130],
          ['MarramGrass', 3, 186, 118], ['MarramGrass', 0, 248, 128], ['MarramGrass', 2, 298, 112],
          ['MarramGrass', 1, 96, 146], ['MarramGrass', 3, 220, 142],
          ['BeachPea', 1, 100, 88], ['BeachPea', 0, 224, 82], ['SweetFern', 1, 150, 196]]
            .forEach(([k, v, x, y]) => put.push({ y, f: () => placeRGBA(S, SP.render(k, { variant: v, season: 'summer', tide: 0.15, stage: 'full' }), x, y) }));
        }
        [['granite', 13, 4, 58, 70], ['granite', 9, 7, 272, 64], ['sandstone', 16, 1, 172, 42]]
          .forEach(([k, sz, sd, x, y]) => put.push({ y, f: () => place(S, K.rock(k, sz, sd), x, y) }));
        put.sort((a, b) => a.y - b.y).forEach(p => p.f());
        return S;
      },
    },

    /* ------------------------------------------------------------------------------- 3 low-tide flat */
    lowtide: {
      name: 'Low-tide flat', sub: 'dead low · rockweed on the ledge, cordgrass in the creek mud',
      note: 'Tide at dead low, so the shore rig has every plant drained: the wracks have collapsed onto their rock in folds and gone glossy-dark, while the marsh plants at the back — which never went under — stand up dry. Same species, same seeds; the tide number is doing all of it.',
      build() {
        const S = new Scene(N, M), edge = x => wig(x, 8, 4, 13, 34);
        const f = floor(S, N, M, [
          { key: 'ledge', step: 1, R: (x, y) => y >= edge(x) && y < 104 + 6 * Math.sin(x / 31), seed: 1631 },
          { key: 'shingle', step: 0, R: (x, y) => y >= 104 + 6 * Math.sin(x / 31) && y < 146, seed: 2111 },
          { key: 'mud', step: 1, R: (x, y) => y >= 146, seed: 8080 },
        ], 44);
        water(f.tile, (x, y) => y < edge(x), 91);
        blitTile(S, f.tile, 0, 0, { relief: 1.35 });
        const put = [];
        if (SP) {
          [['SugarKelp', 0, 0, 30, 52], ['KnottedWrack', 1, 0, 74, 66], ['Bladderwrack', 2, 0, 128, 58],
          ['IrishMoss', 0, 0, 170, 74], ['KnottedWrack', 3, 0, 214, 62], ['SeaLettuce', 1, 0, 256, 70],
          ['Bladderwrack', 0, 0, 298, 56], ['Eelgrass', 2, 0, 52, 92], ['IrishMoss', 3, 0, 234, 96],
          ['Cordgrass', 0, 0, 44, 174], ['Cordgrass', 2, 0, 96, 188], ['Glasswort', 1, 0, 150, 168],
          ['Cordgrass', 3, 0, 198, 182], ['BlackRush', 0, 0, 252, 172], ['Glasswort', 2, 0, 292, 190],
          ['Threesquare', 1, 0, 126, 196], ['SaltmeadowHay', 0, 0, 316, 164]]
            .forEach(([k, v, td, x, y]) => put.push({ y, f: () => placeRGBA(S, SP.render(k, { variant: v, season: 'summer', tide: td, stage: 'full' }), x, y) }));
        }
        [['basalt', 20, 6, 100, 124], ['granite', 14, 8, 188, 132], ['basalt', 16, 3, 272, 120], ['granite', 10, 11, 26, 128]]
          .forEach(([k, sz, sd, x, y]) => put.push({ y, f: () => place(S, K.rock(k, sz, sd), x, y) }));
        put.sort((a, b) => a.y - b.y).forEach(p => p.f());
        return S;
      },
    },

    /* ------------------------------------------------------------------------------ 4 lane & hedgerow */
    lane: {
      name: 'Lane & hedgerow', sub: 'cart track · rank grass · lupin and daisy on the verges',
      note: 'The one scene with no shore in it, and the hardest test of the greenery on its own: a hedgerow of alder on the high side, drifts of lupin and daisy along both verges, and nothing but turf to hold them apart. The path is the terrain pack; the kit\'s edge painter hangs the blades over it.',
      build() {
        const S = new Scene(N, M);
        const mid = y => 96 + Math.round((y - 100) * 0.62 + 14 * Math.sin(y / 38) + 7 * (fbmP(0.3, y / M, 1, 4, 29, 3) - 0.5) * 2);
        const halfw = y => 15 + Math.round(y * 0.055);
        const onPath = (x, y) => Math.abs(x - mid(y)) < halfw(y);
        const f = floor(S, N, M, [
          { key: 'grass', step: 2, R: (x, y) => !onPath(x, y), seed: 3072 },
          { key: 'path', step: 1, R: onPath, seed: 4410 },
        ], 55);
        blitTile(S, f.tile, 0, 0, { relief: 1.2 });
        const put = [];
        [['alder', 0, 34, 72], ['alder', 2, 78, 64], ['alder', 1, 16, 96], ['alder', 3, 58, 108],
        ['wildrose', 1, 100, 88], ['alder', 0, 22, 130], ['blueberry', 3, 92, 124]]
          .forEach(([k, v, x, y]) => put.push({ y, f: () => place(S, K.bush(k, v), x, y) }));
        [['lupin', 'patch', 1, 132, 150], ['daisy', 'patch', 4, 214, 128], ['buttercup', 'clump', 6, 178, 106],
        ['fireweed', 'clump', 2, 262, 158], ['daisy', 'clump', 9, 296, 118], ['lupin', 'clump', 5, 246, 96],
        ['queenanne', 'clump', 3, 156, 178], ['buttercup', 'patch', 8, 292, 186], ['daisy', 'patch', 7, 60, 178],
        ['fireweed', 'single', 4, 118, 74], ['queenanne', 'single', 6, 208, 76]]
          .forEach(([k, t, s, x, y]) => put.push({ y, f: () => place(S, K.flower(k, t, s), x, y) }));
        put.sort((a, b) => a.y - b.y).forEach(p => p.f());
        return S;
      },
    },
  };

  root.Harmony = { LIGHTS, SCENES, Scene, blitTile, blitCliff, place, placeRGBA, floor, water, relight, normalPNG, heightPNG, terrainShadow, castShadow, N, M };
})(typeof globalThis !== 'undefined' ? globalThis : window);
