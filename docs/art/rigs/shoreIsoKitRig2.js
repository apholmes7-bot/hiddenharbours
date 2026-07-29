/* Hidden Harbours — ISO SHORELINE tile kit v8 (PEI red-sandstone coast).
   Same contract as v7 (ADR 0006/0022 boat-bake projection, ADR 0010/0012/0023 water contract:
   the shader owns ALL water, so ZERO water pixels are baked here). What changed is the
   PIXEL CRAFT — v7's weakness was per-pixel white-noise texture, no ambient occlusion and
   no cast shadow, brick-grid strata, and axis-aligned terrain-type edges.

   v8 fixes, in both styles:
   • CLUSTERED VALUE NOISE — domain-warped fbm drives patches at 6–20 px; grain is a
     sub-feature of a patch, never the whole texture. Grass/marram render as 2 px BLADES on
     a jittered lattice, sand as pebble/shell clusters, not salt-and-pepper.
   • OCCLUSION IS BAKED — cap lips carry an AO band, talus a contact shade, and every
     landform ships a matching `contact()` overlay so it seats on the ground it stands on.
   • SEDIMENTARY STRATA — beds 6–10 px with an internal top-lit/base-dark gradient, wobbling
     on fbm, cut by columnar joints that persist across several beds (relief-lit: dark crack +
     lit west arris). No brick grid.
   • ORGANIC FRINGES — reach 5–16 px on fbm, with detached patches and erosion at the tip.
   • ROCK PROPS MOVED OUT — v7's stack()/boulder() are deleted. Every rock in a shoreline scene
     is now a Rock Iso sprite (Erratic · Outcrop · PoolLedge · Skerry · Cloven · Cobble), which
     is a real iso volume with anchors, not a painted lump. Palettes are shared verbatim.

   TWO STYLES — the A/B. Geometry, grid, pivots and API are identical; only the shading law
   and ramps differ, so a map authored against one drops straight onto the other.
     'nat'  naturalist  — 10-step ramps, Bayer-dithered band edges, fine grain, soft AO.
     'gfx'  graphic     — 5–7-step ramps, HARD band edges (no dither), coarse grain, unified
                          near-black keyline on every landform silhouette. Reads cleaner at
                          gameplay zoom over a busy shader sea.

   Grid: square 32×32, 32 px = 1 m. Cliff/dune bands are 32 px (~1.3 m of face at 40°).
   All noise is a pure function of GLOBAL pixel coords → seamless, never repeats.

   API (globalThis.ShoreIso2) — every call takes {style:'nat'|'gfx'}:
     GROUND = ['grass','marram','sand','ripple','shingle','shelf']
     ground(mat,{gx,gy,seed,style})            -> {buf,w:32,h:32} opaque
     fringe(mat,piece,{gx,gy,seed,style})      -> transparent overlay (12 autotile pieces)
     cliff(piece,{band,gy,gx,seed,feature,style})
     column(piece,rows,{seed,feature,style})
     dune(piece,{seed,style})
     contact(piece,{style})                    -> transparent contact-shadow overlay
     rock props → RockIso.render(species, {variant,dress,stone}, 'dry'|'wet'|'awash')
*/
(function (root) {
  const TILE = 32;
  const hex2rgb = h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const mixc = (a, b, t) => {
    const A = Array.isArray(a) ? a : hex2rgb(a), B = Array.isArray(b) ? b : hex2rgb(b);
    return [A[0] + (B[0] - A[0]) * t, A[1] + (B[1] - A[1]) * t, A[2] + (B[2] - A[2]) * t];
  };
  const BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]];
  const B8 = [[0,32,8,40,2,34,10,42],[48,16,56,24,50,18,58,26],[12,44,4,36,14,46,6,38],[60,28,52,20,62,30,54,22],
              [3,35,11,43,1,33,9,41],[51,19,59,27,49,17,57,25],[15,47,7,39,13,45,5,37],[63,31,55,23,61,29,53,21]];

  function hash(x, y, s) {
    let h = (x * 374761393 + y * 668265263 + ((s | 0)) * 1274126177) | 0;
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
  }
  function vnoise(x, y, s) {
    const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi;
    const u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
    const a = hash(xi, yi, s), b = hash(xi + 1, yi, s), c = hash(xi, yi + 1, s), d = hash(xi + 1, yi + 1, s);
    const ab = a + (b - a) * u, cd = c + (d - c) * u;
    return ab + (cd - ab) * v;
  }
  function fbm(x, y, s, oct) {
    let v = 0, amp = 0.5, f = 1, tot = 0;
    for (let i = 0; i < (oct || 3); i++) { v += vnoise(x * f, y * f, s + i * 911) * amp; tot += amp; amp *= 0.5; f *= 2; }
    return v / tot;
  }

  // jittered-lattice cells → irregular slab/cobble polygons (no brick grid anywhere)
  function vor(wx, wy, s0, scale) {
    const gx = Math.floor(wx / scale), gy = Math.floor(wy / scale);
    let f1 = 1e9, f2 = 1e9, bi = 0, bj = 0, bx = 0, by = 0;
    for (let j = -1; j <= 1; j++) for (let i = -1; i <= 1; i++) {
      const ix = gx + i, iy = gy + j;
      const cx = (ix + 0.14 + hash(ix, iy, s0) * 0.72) * scale;
      const cy = (iy + 0.14 + hash(ix, iy, s0 + 1) * 0.72) * scale;
      const dx = wx - cx, dy = wy - cy, d = Math.sqrt(dx * dx + dy * dy);
      if (d < f1) { f2 = f1; f1 = d; bi = ix; bj = iy; bx = cx; by = cy; }
      else if (d < f2) f2 = d;
    }
    return { i: bi, j: bj, cx: bx, cy: by, edge: f2 - f1 };
  }

  // ============================== STYLES =======================================================
  const STYLES = {
    nat: {
      name: 'nat', dither: true, grain: 1, ao: 1, keyline: 0.55, hardKey: false, chunk: 1,
      rock:  ['#33150e','#4a1e14','#6b2c1c','#87301f','#a04528','#bd6038','#d17c4c','#e0a06e'],
      soil:  ['#231409','#3a2113','#54301b','#6f4023','#8a5430'],
      sand:  ['#6f4529','#8f5c35','#b07846','#cb995e','#e0b87d','#efd5a3','#faeccc'],
      grass: ['#12220f','#1d3315','#2a481c','#3a5f23','#4c782c','#619439','#7bad4c','#99c667'],
      wet:   ['#301810','#472115','#5f2d1a','#7a3d22','#95522e','#af6b3e','#c78a55'],
      straw: ['#7d6634','#a5893f','#c7ab5b'],
      moss: '#4c782c', key: '#171009', cool: '#1a2230',
      depthShallow: '#8EA59C', depthDeep: '#0F2227', foam: '#eaf3ee'
    },
    gfx: {
      name: 'gfx', dither: false, grain: 0.12, ao: 1.15, keyline: 1, hardKey: true, chunk: 1.5,
      rock:  ['#241019','#48201d','#7c2f22','#ab4a2a','#d1703f','#efa771'],
      soil:  ['#241610','#452718','#71401f','#9a5c2e'],
      sand:  ['#6a4436','#9a6440','#c78d55','#e6b87d','#fbe3b0'],
      grass: ['#101f16','#1d3520','#2e5426','#437a2c','#66a63c','#93cc5c'],
      wet:   ['#2a1719','#4e2820','#78412a','#a2603a','#c78b57'],
      straw: ['#7d6234','#bfa054'],
      moss: '#437a2c', key: '#120d0c', cool: '#1d2b3a',
      depthShallow: '#8EA59C', depthDeep: '#0F2227', foam: '#eaf3ee'
    }
  };
  const styleOf = s => STYLES[s] || STYLES.nat;

  function dpick(wx, wy, a, b, t) { if (t <= 0) return a; if (t >= 1) return b; return ((BAYER[wy & 3][wx & 3] + 0.5) / 16 < t) ? b : a; }
  function rampAt(R, f, wx, wy, dith) {
    f = Math.max(0, Math.min(R.length - 1, f));
    if (!dith) return R[Math.round(f)];
    const i = Math.floor(f), fr = f - i, hi = R[Math.min(R.length - 1, i + 1)];
    if (fr < 0.34) return R[i];
    if (fr > 0.66) return hi;
    return ((BAYER[wy & 3][wx & 3] + 0.5) / 16 < (fr - 0.34) / 0.32) ? hi : R[i];
  }
  function mkBuf(w, h) {
    const buf = new Uint8ClampedArray(w * h * 4);
    const put = (x, y, c, a) => {
      if (x < 0 || x >= w || y < 0 || y >= h) return;
      const rgb = Array.isArray(c) ? c : hex2rgb(c); const i = (y * w + x) * 4;
      buf[i] = rgb[0]; buf[i + 1] = rgb[1]; buf[i + 2] = rgb[2]; buf[i + 3] = (a == null ? 255 : a);
    };
    const get = (x, y) => { const i = (y * w + x) * 4; return [buf[i], buf[i + 1], buf[i + 2], buf[i + 3]]; };
    return { buf, put, get, w, h };
  }

  // ============================== GROUND MATERIALS =============================================
  function groundColor(mat, wx, wy, sd, ST) {
    sd = sd | 0; ST = ST || STYLES.nat;

    if (mat === 'grass') {
      const G = ST.grass, N = G.length - 1;
      const w = (fbm(wx * 0.07, wy * 0.07, sd + 11, 2) - 0.5) * 7;
      const p = fbm((wx + w) * 0.115, (wy + w) * 0.115, sd + 2, 3);
      const m = fbm(wx * 0.3, wy * 0.3, sd + 5, 2);
      let f = N * 0.28 + p * N * 0.52 + (m - 0.5) * N * 0.27;
      let c = rampAt(G, f, wx, wy, ST.dither);
      const cw = Math.round(3 * ST.chunk), ch = Math.round(5 * ST.chunk);
      const cx = Math.floor(wx / cw), cy = Math.floor(wy / ch), lx = wx - cx * cw, ly = wy - cy * ch;
      const r1 = hash(cx, cy, sd + 7);
      if (r1 < 0.58) {
        const bx = Math.floor(hash(cx, cy, sd + 8) * cw), by = Math.floor(hash(cx, cy, sd + 9) * (ch - 1));
        if (lx === bx && ly >= by && ly <= by + Math.round(ST.chunk * 1.6)) c = rampAt(G, Math.min(N, f + N * 0.26), wx, wy, ST.dither);
      } else if (r1 > 0.87) {
        const bx = Math.floor(hash(cx, cy, sd + 18) * cw), by = Math.floor(hash(cx, cy, sd + 19) * (ch - 1));
        if (lx === bx && ly === by) c = rampAt(G, Math.max(0, f - N * 0.24), wx, wy, ST.dither);
      }
      if (fbm(wx * 0.1 + 31, wy * 0.1, sd + 12, 2) > 0.70 && hash(wx, wy, sd + 13) < 0.45) c = ST.straw[1];
      if (fbm(wx * 0.075, wy * 0.075 + 17, sd + 14, 2) > 0.885) c = rampAt(ST.soil, 1.4 + hash(wx, wy, sd + 15) * 2.2, wx, wy, ST.dither);
      return c;
    }

    if (mat === 'marram') {
      let c = mixc(groundColor('sand', wx, wy, sd + 91, ST), ST.grass[1], 0.14);
      const dens = Math.pow(Math.max(0, (fbm(wx * 0.055, wy * 0.055, sd + 41, 3) - 0.30) / 0.70), 0.85);
      const cw = Math.round(2 * ST.chunk), ch = Math.round(4 * ST.chunk);
      const cx = Math.floor(wx / cw), cy = Math.floor(wy / ch), lx = wx - cx * cw, ly = wy - cy * ch;
      if (hash(cx, cy, sd + 42) < dens * 1.6) {
        const bx = Math.floor(hash(cx, cy, sd + 43) * cw), by = Math.floor(hash(cx, cy, sd + 44) * 2);
        const lean = Math.round(hash(cx, cy, sd + 47) * 2) - 1;
        const dark = hash(cx, cy, sd + 45) < 0.30;
        const straw = hash(cx, cy, sd + 46) < 0.40;
        if (ly >= by && lx === ((bx + (ly > by + 1 ? 0 : lean)) % cw + cw) % cw) {
          c = dark ? ST.grass[2] : (straw ? ST.straw[ST.straw.length - 2] : ST.grass[4]);
          if (ly === by) c = dark ? ST.grass[3] : ST.grass[ST.grass.length - 3];
        }
      }
      return c;
    }

    if (mat === 'sand') {
      const S = ST.sand, N = S.length - 1;
      const u = fbm(wx * 0.032, wy * 0.048, sd + 21, 3);
      const wp = fbm(wx * 0.018, wy * 0.018, sd + 25, 2);
      const rip = Math.sin(wx * 0.12 + wy * 0.055 + wp * 10) * 0.5 + 0.5;
      let f = N * 0.60 + (u - 0.5) * N * 0.55 + rip * N * 0.10;
      const g = fbm(wx * 0.42, wy * 0.42, sd + 23, 2);
      if (g > 0.71) f += N * 0.14; else if (g < 0.27) f -= N * 0.12;
      f += (hash(wx, wy, sd + 24) - 0.5) * N * 0.13 * ST.grain;
      let c = rampAt(S, f, wx, wy, ST.dither);
      if (hash(wx, wy, sd + 26) < 0.010) c = S[N];
      if (fbm(wx * 0.19 + 7, wy * 0.19, sd + 28, 1) > 0.865 && hash(wx, wy, sd + 29) < 0.4)
        c = rampAt(ST.rock, 3.2 + hash(wx, wy, sd + 30) * 2.4, wx, wy, ST.dither);
      if (hash(wx, wy, sd + 27) < 0.005) c = ST.moss;
      return c;
    }

    if (mat === 'ripple') {
      const W = ST.wet, N = W.length - 1;
      const warp = (fbm(wx * 0.022, wy * 0.022, sd + 51, 2) - 0.5) * 7;
      const bend = Math.sin(wx * 0.055 + (fbm(wx * 0.015, 0, sd + 52, 2) - 0.5) * 5) * 2.0;
      const phase = wy + warp + bend;
      const per = 4, ph = ((phase % per) + per) % per;
      let f = N * 0.86 + (fbm(wx * 0.035, wy * 0.035, sd + 53, 3) - 0.5) * N * 0.46;
      const amp = Math.max(0, fbm(wx * 0.04, wy * 0.04, sd + 56, 2) * 2.0 - 0.35);  // ripples fade in and out
      if (ph < 1) f += N * 0.16 * amp; else if (ph > per - 1.2) f -= N * 0.15 * amp;
      f += (hash(wx, wy, sd + 54) - 0.5) * N * 0.11 * ST.grain;
      let c = rampAt(W, f, wx, wy, ST.dither);
      if (fbm(wx * 0.2 + 3, wy * 0.2, sd + 57, 1) > 0.87 && hash(wx, wy, sd + 58) < 0.4)
        c = rampAt(ST.rock, 3 + hash(wx, wy, sd + 59) * 2, wx, wy, ST.dither);       // pebble
      if (hash(wx, wy, sd + 55) < 0.004) c = ST.moss;
      return c;
    }

    if (mat === 'shingle') {
      const R = ST.rock, N = R.length - 1;
      const ux = wx + (fbm(wx * 0.05, wy * 0.05, sd + 66, 2) - 0.5) * 4;
      const uy = wy * 1.7 + (fbm(wx * 0.05 + 9, wy * 0.05, sd + 67, 2) - 0.5) * 4;   // squashed = flat plates
      const v = vor(ux, uy, sd + 61, 5);
      const id = hash(v.i, v.j, sd + 62);
      if (id < 0.42) return groundColor('sand', wx, wy, sd + 77, ST);
      let f = N * 0.20 + id * N * 0.38 + (fbm(wx * 0.3, wy * 0.3, sd + 68, 1) - 0.5) * N * 0.09;
      const rx = ux - v.cx, ry = uy - v.cy;
      if (v.edge < 0.95) f += (rx + ry < 0) ? N * 0.26 : -N * 0.30; // NW lip lit, SE gap dark
      else if (v.edge < 1.9 && rx + ry > 0) f -= N * 0.09;
      f += (hash(wx, wy, sd + 69) - 0.5) * 0.5 * ST.grain;
      return rampAt(R, f, wx, wy, ST.dither);
    }

    if (mat === 'shelf') {
      const R = ST.rock, N = R.length - 1;
      const ux = wx + (fbm(wx * 0.035, wy * 0.035, sd + 72, 2) - 0.5) * 7;
      const uy = wy + (fbm(wx * 0.035 + 5, wy * 0.035, sd + 73, 2) - 0.5) * 7;
      const v = vor(ux, uy, sd + 71, 14);
      let f = N * 0.42 + (hash(v.i, v.j, sd + 74) - 0.5) * N * 0.22
            + (fbm(wx * 0.12, wy * 0.12, sd + 75, 2) - 0.5) * N * 0.20;
      if (v.edge < 0.5) f -= N * 0.37;                             // joint crack
      else if (v.edge < 1.6 && (ux - v.cx) + (uy - v.cy) < 0) f += N * 0.13;  // lit joint shoulder
      f += (hash(wx, wy, sd + 76) - 0.5) * 0.55 * ST.grain;
      let c = rampAt(R, f, wx, wy, ST.dither);
      if (v.edge < 0.95 && fbm(wx * 0.07, wy * 0.07, sd + 78, 2) > 0.80 && hash(wx, wy, sd + 79) < 0.6) c = ST.moss;
      return c;
    }
    return ST.sand[3];
  }

  function ground(mat, opts) {
    opts = opts || {}; const ST = styleOf(opts.style);
    const gx = (opts.gx | 0) * TILE, gy = (opts.gy | 0) * TILE, sd = opts.seed | 0;
    const c = mkBuf(TILE, TILE);
    for (let y = 0; y < TILE; y++) for (let x = 0; x < TILE; x++) c.put(x, y, groundColor(mat, gx + x, gy + y, sd, ST));
    return c;
  }

  // ============================== TERRAIN-TYPE FRINGES =========================================
  const FRINGE_PIECES = ['edN','edE','edS','edW','coNE','coSE','coSW','coNW','inNE','inSE','inSW','inNW'];
  function fringe(mat, piece, opts) {
    opts = opts || {}; const ST = styleOf(opts.style);
    const sd = opts.seed | 0, gx0 = (opts.gx | 0) * TILE, gy0 = (opts.gy | 0) * TILE;
    const c = mkBuf(TILE, TILE);
    const soil = mat === 'grass' || mat === 'marram';
    const N = piece.includes('N'), E = piece.includes('E'), W = piece.includes('W');
    const inner = piece.startsWith('in'), edge = piece.startsWith('ed');
    const SK = { n: 5, e: 6, s: 7, w: 8 };
    function reach(side, p) {
      const k = sd * 13 + SK[side];
      return 5 + fbm(p * 0.075, k * 0.37, k, 3) * 11 + (fbm(p * 0.33, 0, k + 41, 2) - 0.5) * 3.4;
    }
    function raw(x, y) {
      const wx = gx0 + x, wy = gy0 + y;
      if (edge) {
        if (piece === 'edN') return { d: y, r: reach('n', wx) };
        if (piece === 'edS') return { d: TILE - 1 - y, r: reach('s', wx) };
        if (piece === 'edW') return { d: x, r: reach('w', wy) };
        return { d: TILE - 1 - x, r: reach('e', wy) };
      }
      const dy = N ? y : (TILE - 1 - y), dx = E ? (TILE - 1 - x) : x;
      const rn = reach(N ? 'n' : 's', wx), re = reach(E ? 'e' : 'w', wy);
      if (inner) { const a = rn - dy, b = re - dx; return { d: 0, r: Math.max(a, b) > 0 ? 1 : -1, direct: Math.max(a, b) > 0 }; }
      const q = (dy * dy) / (rn * rn + 1e-3) + (dx * dx) / (re * re + 1e-3);
      return { d: 0, r: 0, direct: q < 1, soft: (1 - Math.sqrt(q)) * Math.min(rn, re) };
    }
    function covered(x, y) {
      const wx = gx0 + x, wy = gy0 + y;
      const o = raw(x, y);
      let inside, margin;
      if (o.direct !== undefined) { inside = o.direct; margin = o.soft != null ? o.soft : (inside ? 3 : -3); }
      else { inside = o.d < o.r; margin = o.r - o.d; }
      const n = fbm(wx * 0.28, wy * 0.28, sd + 90, 2);
      if (inside && margin < 3 && n < 0.31) return false;      // erode the tongue tip
      if (!inside && margin > -4.5 && n > 0.70) return true;   // detached patches
      return inside;
    }
    for (let y = 0; y < TILE; y++) for (let x = 0; x < TILE; x++) {
      if (!covered(x, y)) continue;
      c.put(x, y, groundColor(mat, gx0 + x, gy0 + y, sd, ST));
    }
    if (soil) { // sod lip: soil under-shadow wherever the mat's lowest pixel meets the tile below
      for (let x = 0; x < TILE; x++) {
        let low = -1;
        for (let y = TILE - 1; y >= 0; y--) { if (c.buf[(y * TILE + x) * 4 + 3]) { low = y; break; } }
        if (low >= 0 && low < TILE - 1) {
          c.put(x, low + 1, ST.soil[1], 235);
          if (hash(gx0 + x, 0, sd + 83) < 0.45) c.put(x, low + 2, ST.soil[2], 150);
        }
      }
    }
    return c;
  }

  // ============================== SEDIMENTARY STRATA ===========================================
  function strata(x, gy, sd, ST) {
    const R = ST.rock, N = R.length - 1;
    const wob = (fbm(x * 0.030, 0, sd + 31, 3) - 0.5) * 8 + (fbm(x * 0.11, 0, sd + 32, 2) - 0.5) * 2.6;
    const Y = gy + wob;
    const grp = Math.floor(Y / 34);
    const bedH = 7 + Math.floor(hash(0, grp, sd + 33) * 7);
    const bed = Math.floor(Y / bedH), ly = ((Y % bedH) + bedH) % bedH;
    let f = N * 0.44 + (hash(0, bed, sd + 34) - 0.5) * N * 0.42;
    const t = ly / (bedH - 1);
    f += (1 - t) * N * 0.11 - t * N * 0.08;
    const bstr = hash(0, bed, sd + 44);
    if (ly < 1 && bstr > 0.28) f -= N * (0.12 + bstr * 0.19);  // bedding contact — not every bed
    if (ly > bedH - 1.6 && bstr > 0.5) f += N * 0.12;          // sunlit ledge lip
    const jgrp = Math.floor(Y / 58);
    const jw = 17 + Math.floor(hash(0, jgrp, sd + 36) * 16);
    const joff = Math.floor(hash(1, jgrp, sd + 37) * jw) + Math.round((fbm(0, Y * 0.045, sd + 38, 2) - 0.5) * 7);
    const jcol = Math.floor((x + joff) / jw), jx = (((x + joff) % jw) + jw) % jw;
    if (hash(jcol, jgrp, sd + 43) < 0.42) {                   // sparse columnar joint, relief-lit
      if (jx === 0) f -= N * 0.30; else if (jx === 1) f += N * 0.11;
    }
    f += (fbm(x * 0.13, gy * 0.010, sd + 39, 2) - 0.5) * N * 0.24; // rain flutes
    const stain = fbm(x * 0.055, gy * 0.004, sd + 55, 2);     // dark seep stains running the face
    if (stain > 0.63) f -= (stain - 0.63) * N * 0.74;
    f += (hash(x, gy, sd + 40) - 0.5) * N * 0.09 * ST.grain;
    return rampAt(R, f, x, gy, ST.dither);
  }
  function facetShade(c, facing, wx, wy, ST) {
    if (facing === 'w') return mixc(c, ST.rock[ST.rock.length - 2], ST.dither ? 0.34 : 0.28);
    if (facing === 'e') return mixc(c, ST.cool, ST.dither ? 0.38 : 0.44);
    return c;
  }
  function talus(x, y, sd, ST) {
    const R = ST.rock, N = R.length - 1;
    const lean = Math.round(Math.sin(y * 0.4 + sd * 1.3) * 2);
    const rowH = 2 + Math.round(hash(0, y >> 2, sd + 47));
    const ry = Math.floor(y / rowH), off = Math.round(hash(0, ry, sd + 45) * 12);
    const slabW = 5 + Math.round(hash(ry, 0, sd + 46) * 5);
    const key = x + off + lean, sx = Math.floor(key / slabW), lxp = ((key % slabW) + slabW) % slabW;
    const rr = hash(sx, ry, sd + 40);
    if (rr < 0.14) return rampAt(ST.sand, (ST.sand.length - 1) * (0.24 + hash(x, y, sd + 43) * 0.26), x, y, ST.dither);
    let f = N * 0.16 + rr * N * 0.42;
    let c = rampAt(R, f, x, y, ST.dither);
    const ly = ((y % rowH) + rowH) % rowH;
    if (ly === 0 || lxp === 0) c = rampAt(R, Math.min(N, f + N * 0.22), x, y, ST.dither);
    else if (ly === rowH - 1) c = rampAt(R, Math.max(0, f - N * 0.34), x, y, ST.dither);
    if (lxp === slabW - 1) c = rampAt(R, Math.max(0, f - N * 0.26), x, y, ST.dither);
    return c;
  }

  // ============================== CLIFF ========================================================
  const CLIFF_PIECES = ['faceS','cornSW','cornSE','sideW','sideE','innSW','innSE','diagSW','diagSE'];
  function cliffGeom(piece, x, y, sd) {
    const FW = 9;
    switch (piece) {
      case 'faceS': return { on: true, facing: 's' };
      case 'cornSW': { const lim = FW + Math.round(Math.sin(y * 0.25 + sd) * 1.2);
        return { on: true, facing: x < lim ? 'w' : 's', arris: x === lim, sil: x === 0 }; }
      case 'cornSE': { const lim = TILE - 1 - FW - Math.round(Math.sin(y * 0.25 + sd) * 1.2);
        return { on: true, facing: x > lim ? 'e' : 's', arris: x === lim, sil: x === TILE - 1 }; }
      case 'sideW': { const lim = 6 + Math.round(Math.sin(y * 0.25 + sd) * 1.2);
        return { on: true, facing: x < lim ? 'w' : 'cap', arris: x === lim, sil: x === 0 }; }
      case 'sideE': { const lim = TILE - 7 - Math.round(Math.sin(y * 0.25 + sd) * 1.2);
        return { on: true, facing: x > lim ? 'e' : 'cap', arris: x === lim, sil: x === TILE - 1 }; }
      case 'innSW': return { on: true, facing: 's', fold: x < 7 ? (7 - x) / 7 : 0 };
      case 'innSE': return { on: true, facing: 's', fold: x > TILE - 8 ? (x - (TILE - 8)) / 7 : 0 };
      case 'diagSW': { const lim = Math.round((y / (TILE - 1)) * 26) + 2;
        return { on: x >= lim - 2, facing: x < lim + FW - 4 ? 'w' : 's', arris: Math.abs(x - (lim + FW - 4)) < 1, sil: x < lim }; }
      case 'diagSE': { const lim = TILE - 3 - Math.round((y / (TILE - 1)) * 26);
        return { on: x <= lim + 2, facing: x > lim - FW + 4 ? 'e' : 's', arris: Math.abs(x - (lim - FW + 4)) < 1, sil: x > lim }; }
    }
    return { on: true, facing: 's' };
  }

  function cliff(piece, opts) {
    opts = opts || {}; const ST = styleOf(opts.style);
    const band = opts.band || 'mid', sd = opts.seed | 0, gx0 = (opts.gx | 0) * TILE;
    const gy0 = opts.gy != null ? (opts.gy | 0) : (band === 'cap' ? 0 : band === 'mid' ? 32 : 64);
    const feature = opts.feature || null;
    const c = mkBuf(TILE, TILE), capH = 10;
    // talus heap profile for the toe band — an actual silhouette, not a full-width band
    const heapTop = new Array(TILE);
    for (let x = 0; x < TILE; x++) heapTop[x] = Math.round(17 + (fbm((gx0 + x) * 0.055, 0, sd + 65, 3) - 0.5) * 11);

    for (let y = 0; y < TILE; y++) for (let x = 0; x < TILE; x++) {
      const g = cliffGeom(piece, x, y, sd); if (!g.on) continue;
      const gy = gy0 + y, wx = gx0 + x;
      const isCap = g.facing === 'cap';
      if (isCap && band !== 'cap') continue;
      let px, lipY = -99;
      if (band === 'cap' && (isCap || (y < capH + 4 && g.facing === 's'))) {
        if (isCap) px = groundColor('grass', wx, gy, sd, ST);
        else {
          let fr = capH - 1 - Math.round(fbm(wx * 0.14, 0, sd + 66, 2) * 4);
          if (fbm(wx * 0.09, 7, sd + 67, 2) > 0.74) fr -= 4;         // slump gully notch
          else if (fbm(wx * 0.09, 21, sd + 68, 2) > 0.76) fr += 3;   // grass tongue hangs lower
          fr = Math.max(1, fr); lipY = fr;
          if (y > fr) px = strata(wx, gy, sd, ST);
          else {
            px = groundColor('grass', wx, gy, sd, ST);
            if (y >= fr - 1) px = mixc(px, ST.key, 0.35);
          }
        }
      } else {
        px = strata(wx, gy, sd, ST);
        const lift = band === 'cap' ? 0.12 : band === 'mid' ? 0 : -0.14;   // sun catches the crown
        px = lift > 0 ? mixc(px, ST.rock[ST.rock.length - 1], lift) : (lift < 0 ? mixc(px, ST.key, -lift) : px);
      }

      // AO under the cap lip — the single biggest missing cue in v7
      if (band === 'cap' && !isCap) {
        const fr = lipY >= 0 ? lipY : capH;
        const d = y - fr;
        if (d > 0 && d <= 4) {
          const amt = (1 - (d - 1) / 4) * 0.5 * ST.ao;
          px = ST.dither ? dpick(wx, gy, px, mixc(px, ST.key, 0.75), amt + 0.15) : mixc(px, ST.key, amt);
        }
      }
      if (band === 'toe' && !isCap && y >= heapTop[x]) px = talus(wx, y + gy0, sd, ST);
      if (band === 'toe' && !isCap && y >= heapTop[x] - 2 && y < heapTop[x])
        px = mixc(px, ST.key, 0.4 * ST.ao);                       // heap contact shade on the face
      if (g.facing === 'w' && !isCap) px = facetShade(px, 'w', wx, gy, ST);
      if (g.facing === 'e' && !isCap) px = facetShade(px, 'e', wx, gy, ST);
      if (g.fold) px = ST.dither ? dpick(wx, gy, px, mixc(px, ST.key, 0.5), g.fold * 0.85) : mixc(px, ST.key, g.fold * 0.45);
      if (g.arris) px = mixc(px, ST.rock[ST.rock.length - 3], 0.42);
      if (g.sil) px = mixc(px, ST.key, ST.hardKey ? 0.75 : 0.5);
      if (band === 'toe' && y >= TILE - 2) px = mixc(px, ST.key, (y === TILE - 1 ? 0.5 : 0.25) * ST.ao);
      c.put(x, y, px);
    }
    if (band === 'toe' && feature === 'cave' && (piece === 'faceS' || piece === 'innSW' || piece === 'innSE')) carveCave(c, sd, ST);
    if (band !== 'toe') {  // clinging shrubs
      const nT = band === 'cap' ? 4 : 3;
      for (let t = 0; t < nT; t++) {
        const sx = 2 + Math.floor(hash(t, band === 'cap' ? 1 : 2, sd + 30) * 28);
        const sy = (band === 'cap' ? 14 : 3) + Math.floor(hash(t, 9, sd + 31) * 15);
        const g = cliffGeom(piece, sx, sy, sd);
        if (g.on && g.facing !== 'cap') {
          c.put(sx, sy, ST.grass[3]); c.put(sx, sy - 1, ST.grass[5]);
          if (hash(t, 5, sd + 32) < 0.6) { c.put(sx + 1, sy, ST.grass[2]); c.put(sx, sy + 1, ST.grass[1]); }
        }
      }
    }
    if (ST.hardKey) edgeKey(c, ST, 0.8);
    return c;
  }

  function carveCave(c, sd, ST) {
    const R = ST.rock;
    const cx = 15 + Math.round((hash(0, 0, sd + 70) - 0.5) * 6);
    const halfW = 7 + Math.round(hash(2, 0, sd + 72) * 2);
    const archTop = 10 + Math.round(hash(1, 0, sd + 71) * 2), floorY = 31;
    for (let x = cx - halfW; x <= cx + halfW; x++) {
      if (x < 0 || x >= TILE) continue;
      const nx = (x - cx) / halfW;
      const top = archTop + Math.round((1 - Math.sqrt(Math.max(0, 1 - nx * nx))) * 7);
      for (let y = top; y <= floorY; y++) {
        const depth = 1 - Math.abs(nx), vy = (y - top) / (floorY - top + 0.001);
        c.put(x, y, mixc(R[2], ST.key, Math.min(0.94, 0.86 - 0.24 * vy * (0.4 + 0.6 * depth))));
      }
      c.put(x, top - 1, (nx < -0.15 && nx > -0.9) ? R[R.length - 3] : R[1]);
    }
    for (let b = 0; b < 4; b++) {
      const bx = cx - halfW + 1 + Math.round(hash(b, 3, sd + 73) * (halfW * 2 - 2));
      const bw = 2 + Math.round(hash(b, 4, sd + 74)), bh = 2 + Math.round(hash(b, 5, sd + 75));
      for (let yy = floorY - bh + 1; yy <= floorY; yy++) for (let xx = bx; xx < bx + bw; xx++)
        c.put(xx, yy, yy === floorY - bh + 1 ? R[R.length - 4] : R[2 + (b & 1)]);
    }
  }

  function column(piece, rows, opts) {
    opts = opts || {}; const sd = opts.seed | 0;
    const H = rows * TILE, out = mkBuf(TILE, H);
    for (let r = 0; r < rows; r++) {
      const band = r === 0 ? 'cap' : (r === rows - 1 ? 'toe' : 'mid');
      const cell = cliff(piece, { band, gy: r * TILE, seed: sd, style: opts.style, feature: (band === 'toe' ? opts.feature : null) });
      for (let y = 0; y < TILE; y++) for (let x = 0; x < TILE; x++) {
        const i = (y * TILE + x) * 4;
        if (cell.buf[i + 3]) { const d = ((r * TILE + y) * TILE + x) * 4;
          out.buf[d] = cell.buf[i]; out.buf[d + 1] = cell.buf[i + 1]; out.buf[d + 2] = cell.buf[i + 2]; out.buf[d + 3] = cell.buf[i + 3]; }
      }
    }
    return out;
  }

  // ============================== DUNE =========================================================
  function dune(piece, opts) {
    opts = opts || {}; const ST = styleOf(opts.style);
    const sd = opts.seed | 0, gx0 = (opts.gx | 0) * TILE;
    const band = opts.band || 'cap', lower = band === 'toe';
    const c = mkBuf(TILE, TILE), S = ST.sand, N = S.length - 1, capH = 14;
    for (let y = 0; y < TILE; y++) for (let x = 0; x < TILE; x++) {
      const g = cliffGeom(piece, x, y, sd); if (!g.on) continue;
      const wx = gx0 + x;
      const isCap = g.facing === 'cap';
      if (isCap && lower) continue;                       // the plateau tile above already drew it
      const crest = lower ? -1 : capH - Math.round(fbm(wx * 0.09, 0, sd + 81, 2) * 6);
      let px;
      if (!lower && (isCap || y < crest)) px = groundColor('marram', wx, y, sd, ST);
      else {
        const yAbs = lower ? y + TILE : y;
        const tt = Math.min(1, Math.max(0, (yAbs - 8) / (TILE * 2 - 12)));
        let f = N * 0.86 - Math.pow(tt, 0.9) * N * 0.56 + (fbm(wx * 0.07, yAbs * 0.05, sd + 82, 2) - 0.5) * N * 0.19;
        px = rampAt(S, f, wx, yAbs, ST.dither);
        const streak = fbm(wx * 0.24, yAbs * 0.035, sd + 84, 2);      // wind streaks run down the slope
        if (streak > 0.70) px = rampAt(S, Math.max(0, f - N * 0.19), wx, yAbs, ST.dither);
        else if (streak < 0.22) px = rampAt(S, Math.min(N, f + N * 0.14), wx, yAbs, ST.dither);
        const d = y - crest;
        if (!lower && d < 5) px = mixc(px, ST.key, (0.50 - d * 0.09) * ST.ao);   // AO under the marram lip
        if (lower && y >= TILE - 13) px = mixc(px, ST.cool, 0.18 * ST.ao);       // cool bounce in the foot
        if (lower) { const fy = TILE - 6 + Math.round(Math.sin(wx * 0.28 + sd) * 1.8);
          if (y >= fy) px = mixc(px, ST.key, Math.min(0.42, (y - fy + 1) * 0.09) * ST.ao); }
      }
      if (g.facing === 'w' && !isCap) px = mixc(px, S[N], 0.28);
      if (g.facing === 'e' && !isCap) px = mixc(px, ST.cool, 0.34);
      if (g.fold) px = mixc(px, ST.key, g.fold * 0.34);
      if (g.sil) px = mixc(px, ST.key, ST.hardKey ? 0.6 : 0.35);
      c.put(x, y, px);
    }
    if (!lower) for (let t = 0; t < 6; t++) {   // marram tufts slumping down the face
      const sx = 2 + Math.floor(hash(t, 4, sd + 30) * 28);
      const sy = capH + 1 + Math.floor(hash(t, 9, sd + 31) * 13);
      const g = cliffGeom(piece, sx, sy, sd);
      if (g.on && g.facing !== 'cap') {
        c.put(sx, sy, ST.grass[3]); c.put(sx, sy - 1, ST.grass[4]); c.put(sx, sy - 2, ST.grass[2]);
        if (hash(t, 2, sd + 33) < 0.5) { c.put(sx + 1, sy - 1, ST.straw[1]); c.put(sx + 1, sy, ST.grass[2]); }
      }
    }
    if (ST.hardKey) edgeKey(c, ST, 0.7);
    return c;
  }

  // ============================== CONTACT SHADOW OVERLAYS ======================================
  // Transparent overlay stamped on the GROUND tile adjacent to a landform, so nothing floats.
  // Sun is upper-left → shadow falls S and E. pieces: 'n','ne','e','nw','w' (side the form is on).
  const CONTACT_PIECES = ['n', 'ne', 'e', 'nw', 'w'];
  function contact(piece, opts) {
    opts = opts || {}; const ST = styleOf(opts.style);
    const c = mkBuf(TILE, TILE), K = hex2rgb(ST.key);
    const len = opts.len || 9;
    for (let y = 0; y < TILE; y++) for (let x = 0; x < TILE; x++) {
      let d = 99;
      if (piece.includes('n')) d = Math.min(d, y);
      if (piece.includes('w')) d = Math.min(d, x);
      if (piece.includes('e')) d = Math.min(d, TILE - 1 - x);
      if (d >= len) continue;
      const t = 1 - d / len;
      const a = t * t * 0.62 * ST.ao;
      const thr = (B8[y & 7][x & 7] + 0.5) / 64;
      if (thr < a) c.put(x, y, K, Math.round(200 * Math.min(1, 0.45 + a)));
    }
    return c;
  }

  // ============================== ROCK PROPS ===================================================
  // Deliberately NOT in this kit. Sea stacks, skerries, outcrops, erratics, tide-pool ledges and
  // cobble scatters all come from the Rock Iso kit (Art/rockIsoRig.js), which supersedes v7's
  // ShoreIso.stack/boulder: it builds real superellipsoid volumes with view-axis depth sorting,
  // facetised planes, bedding that wraps the mass, and per-variant anchors (footprint / perch /
  // snags / hazard / pool). ShoreIso2 shares its palette verbatim so the two composite seamlessly.

  function keyline(c, ST) {
    const { buf, w, h } = c, K = hex2rgb(ST.key);
    const t = ST.hardKey ? 0.82 : 0.62;
    const solid = (x, y) => x >= 0 && x < w && y >= 0 && y < h && buf[(y * w + x) * 4 + 3] > 0;
    const marks = [];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      if (!buf[(y * w + x) * 4 + 3]) continue;
      if (!solid(x - 1, y) || !solid(x + 1, y) || !solid(x, y - 1) || !solid(x, y + 1)) marks.push(y * w + x);
    }
    for (const p of marks) { const i = p * 4;
      buf[i] = buf[i] * (1 - t) + K[0] * t; buf[i + 1] = buf[i + 1] * (1 - t) + K[1] * t; buf[i + 2] = buf[i + 2] * (1 - t) + K[2] * t; }
  }
  function edgeKey(c, ST, t) { // darken the outer 1px column of an opaque tile edge that is a silhouette
    const { buf, w, h } = c, K = hex2rgb(ST.key);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = (y * w + x) * 4; if (!buf[i + 3]) continue;
      const openL = x === 0 ? false : !buf[(y * w + x - 1) * 4 + 3];
      const openR = x === w - 1 ? false : !buf[(y * w + x + 1) * 4 + 3];
      const openU = y === 0 ? false : !buf[((y - 1) * w + x) * 4 + 3];
      const openD = y === h - 1 ? false : !buf[((y + 1) * w + x) * 4 + 3];
      if (openL || openR || openU || openD) {
        buf[i] = buf[i] * (1 - t) + K[0] * t; buf[i + 1] = buf[i + 1] * (1 - t) + K[1] * t; buf[i + 2] = buf[i + 2] * (1 - t) + K[2] * t;
      }
    }
  }

  root.ShoreIso2 = {
    TILE, STYLES, styleOf,
    GROUND: ['grass', 'marram', 'sand', 'ripple', 'shingle', 'shelf'],
    FRINGE_PIECES, CLIFF_PIECES, CONTACT_PIECES, ROCK_PROPS: 'Art/rockIsoRig.js (RockIso)',
    ground, groundColor, fringe, cliff, column, dune, contact,
    strata, talus, hash, fbm, vnoise, mkBuf, dpick, rampAt, mixc, hex2rgb, B8
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
