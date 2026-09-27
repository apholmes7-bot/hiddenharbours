/* Hidden Harbours — PIXEL KIT (pass three: one floor, one language).

   What the screenshots said. Sprites are authored pixel art — flat stamps, a dark seam, a lit tip —
   and the floor under them was still a field: soft octave noise with 1-texel grain on top. Two
   pictures on one screen. Nothing on the ground lived at the scale the eye reads a field by (0.3–3 m),
   so the ground was empty before the sprites arrived and camouflage after; the boundaries were
   cross-fades and torn alpha; sand blew out white; a ripple pass laid corduroy across the dirt and
   the tile period with it. This pass is the trees' language brought down to the floor:

     · ONE MARK.  Every material is a quiet flat body plus LUMPS — an ellipse one flat band, its
       key-ward tip a band up, its down/right seam a band down, a cap toward the light on the big
       ones, and the shade it throws on the ground. Tussock, cobble, clod, pebble, fieldstone: the
       same lump at different sizes in different palettes. 4–10 texels, so it reads at 1×.
     · DENSITY, NEVER VALUE.  Variation across a tile is where the marks crowd, steered by a slow
       field. No value zones — a value blob is a camouflage pattern and a visible period.
     · NO DIRECTIONAL FEATURE IN A WRAPPING TILE.  Ripples are short broken crescents; ruts do not
       exist in a tile. Corduroy was the loudest tell in the screenshots.
     · A VALUE LADDER FOR THE WHOLE FLOOR.  sand › path › ledge › dirt › grass › shingle › mud, so
       the materials read apart from across the room, and a path is a light line to follow through
       darker grass — not a brown plank on olive.
     · EDGES ARE AUTHORED.  Where two materials meet, the higher one has a cut face (a dark row on
       its south and east sides), throws a shade on the lower one, and hangs a fringe over it —
       blades, crumbs, spilled stones. Baked as strips in four orientations and painted live into the
       kit scene along any curve. Never a cross-fade.
     · SPRITES IN THE SAME HAND.  Boulders, bushes and flowers are built from the same lump and the
       same stencil stamps as the trees' leaves, ringless, 1 px texel, bottom-centre pivot.

   Texels: pass six drops the floor and the edge strips to a 1 px texel — the sprites' texel — so the
   ground stops reading one step coarser than everything standing on it. Tile sizes and the whole
   engine contract are unchanged (256 px = 8 m); what doubles is the texel grid inside them, from 128
   to 256. Every mark size and site pitch in a paint function is authored in DETAIL-1 texels and
   multiplied up by the context, so a cobble stays the same 12 cm across and gets four times the
   texels to be a cobble with. Heights stay in physical units and `relief` scales instead, which keeps
   one height ruler across the kit (PxLang.H_KIT) for the blend channel to publish.
   Key upper-left as TreeRig3 (the bible's top-of-frame ruling is still open — every bake here is one
   constant, PxLang.setKey, away from it).

     PxKit.DETAIL                         texels per authored texel (2 = the 1 px floor)
     PxKit.GROUND                         the palette (mid bands)
     PxKit.MATS · PxKit.build(key, step)  -> {tile, z, relief, lo, hi}       grass dirt path mud sand shingle ledge
     PxKit.BLEND                          per-material paint-tool response (markLead, grain, bias)
     PxKit.paintRegion(t, key, step, R)   paint a material into a region of any tile (the scene)
     PxKit.edges(ctx, reg, mats, seed)    the seam painter over a region map
     PxKit.strip(a, b, orient, step)      an edge strip N|S|E|W, 256×24 / 24×256 px, alpha
     PxKit.rock(kind, size, seed) · bush(species, variant) · flower(species, tier, seed) */
(function (root) {
  'use strict';
  const P = root.PxLang; if (!P) throw new Error('Art/pixelLanguage.js must be loaded first');
  const { Tile, stamp, sites, hash2, fbmP, worleyP, stencil, ellipse, clamp } = P;
  const STEPS = ['_Lo', '', '_Hi'];
  const DETAIL = 2;                        // texels per authored texel — 2 puts the floor on a 1 px texel
  const pk = (arr, st) => arr[clamp(st, 0, 2)];
  const ALL = () => true;

  // ---- the floor's palette — one value ladder ------------------------------------------------------
  const GROUND = {
    grass: '#485d2f', grassDry: '#5a5f2c', grassDamp: '#3f5e38', soil: '#77523a', straw: '#b7a066', stone: '#7d7b76',
    buttercup: '#e6c53c', clover: '#e8e2d0', selfheal: '#9a7cc4',
    dirt: '#8b5c40', peb: '#a29181', moss: '#5b6a37',
    path: '#a07a56', gravel: '#8e8477',
    mud: '#5a4433', puddle: '#7c8a88', crust: '#7a6450',
    sand: '#c9ab80', damp: '#a88b67',
    grit: '#57504a', grey: '#7d7b76', tan: '#9b866d', red: '#8d5a46', dark: '#4c4844',
    ledge: '#a25c40', weed: '#5d6a2f', cob: '#8a837c',
    water: '#3d6470',
  };

  // ---- helpers -------------------------------------------------------------------------------------
  const ell = {}; const E = (w, h) => { const k = w + 'x' + h; return ell[k] || (ell[k] = ellipse(Math.max(1, w | 0), Math.max(1, h | 0))); };
  /* a painting context over a tile, optionally restricted to a region R(x,y). Feature scales are in
     texels of a 128-texel tile AT DETAIL 1, so a material looks the same in a 256 px tile, in the
     768 px scene, and at either detail. o.detail multiplies mark sizes and site pitches; c.cx/c.cy
     turn an authored cell count into this tile's lattice; c.px turns an authored texel count into
     this tile's texels; c.dot paints one authored texel as a D × D block with a lit tip. */
  function makeCtx(t, R, o) {
    R = R || ALL;
    const n = t.n, m = t.m, D = Math.max(1, Math.round((o && o.detail) || 1)), B = 128 * D;
    const c = {
      t, R, n, m, D,
      px: v => Math.max(1, Math.round(v * D)),
      cx: k => Math.max(1, Math.round(k * n / B)),
      cy: k => Math.max(1, Math.round(k * m / B)),
      F: (x, y, cells, s, oct) => fbmP((x + .5) / n, (y + .5) / m, Math.max(1, Math.round(cells * n / B)), Math.max(1, Math.round(cells * m / B)), s, oct || 2),
      deep: (x, y, d) => { d = Math.max(1, Math.round(d * D)); return R(x, y) && R(x - d, y) && R(x + d, y) && R(x, y - d) && R(x, y + d) && R(x - d, y - d) && R(x + d, y + d) && R(x - d, y + d) && R(x + d, y - d); },
      put: (x, y, pal, band, h, lock) => { if (R(x, y)) t.set(x, y, pal, band, h, lock); },
      dot: (x, y, pal, band, h, lock) => {
        if (D === 1) { if (R(x, y)) t.set(x, y, pal, band, h, lock); return; }
        const own = t.mid === 0; if (own) t.beginMark();
        for (let dy = 0; dy < D; dy++) for (let dx = 0; dx < D; dx++) {
          const b = band + (dx === 0 && dy === 0 ? 1 : dx === D - 1 && dy === D - 1 ? -1 : 0);
          if (R(x + dx, y + dy)) t.set(x + dx, y + dy, pal, b, h, lock);
        }
        if (own) t.endMark();
      },
      fill: (pal, band, h) => { for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) if (R(x, y)) { const i = y * n + x; t.pal[i] = pal; t.band[i] = band; t.h[i] = h || 0; t.mark[i] = 0; } },
      sites: (pitch, jit, seed, fn) => sites(n, m, pitch * D, jit, seed, s => R(s.x, s.y) && (!fn || fn(s))),
    };
    return c;
  }
  /* the shade a mark throws: the ground texel goes down to `band` — only the ground, never another mark */
  function shade(c, x, y, s) { if (!c.R(x, y)) return; const t = c.t, i = t.i(x, y); if (t.lock[i] || t.pal[i] !== s.pal) return; t.band[i] = Math.min(t.band[i], s.band == null ? 1 : s.band); }
  /* THE lump. o = {pal, band, h, hBase, lock, cap, seam, tip, shade:{pal,band}}. w/h are authored
     texels; the context scales them. Height stays physical — relief carries the detail. */
  function lump(c, x, y, w, h, o) {
    const t = c.t, D = c.D, W = c.px(w), Hh = c.px(h), st = E(W, Hh), band = o.band == null ? 2 : o.band;
    t.beginMark();
    stamp(t, st, x, y, { pal: o.pal, band, seam: o.seam == null ? 1 : o.seam, tip: o.tip == null ? 1 : o.tip, h: o.h == null ? w * .35 : o.h, hBase: o.hBase, lock: o.lock, clip: c.R });
    if (o.cap !== false && W >= 4 * D && Hh >= 3 * D) {
      stamp(t, E(W - 2 * D, Hh - 2 * D), x - D, y - D, { pal: o.pal, band: band + 1, seam: 0, tip: 0, lock: o.lock, clip: c.R });
      /* what the finer texel buys: a second crown inside the cap, so a stone is a body with a
         shoulder and a top instead of one flat ellipse with a lit rim */
      if (D > 1 && W >= 6 * D) stamp(t, E(W - 4 * D, Hh - 4 * D), x - 2 * D, y - 2 * D, { pal: o.pal, band: band + 2, seam: 0, tip: 0, lock: o.lock, clip: c.R });
    }
    if (o.shade) {
      const low = new Map(); let rx = -1, ry = 0;
      for (const p of st.px) { const q = low.get(p[0]); if (q == null || p[1] > q) low.set(p[0], p[1]); if (p[0] > rx || (p[0] === rx && p[1] > ry)) { rx = p[0]; ry = p[1]; } }
      for (const [cx, cy] of low) for (let k = 1; k <= D; k++) shade(c, x + cx - st.ox, y + cy - st.oy + k, k === 1 ? o.shade : { pal: o.shade.pal, band: (o.shade.band == null ? 1 : o.shade.band) + 1 });
      for (let k = 1; k <= D; k++) shade(c, x + rx - st.ox + k, y + ry - st.oy, o.shade);
    }
    t.endMark();
  }
  /* a torn flat shape — a plate that stands a hair proud or a worn hollow. Rim pixels on the
     key-ward diagonal take o.ul, on the far diagonal o.dr; o.hole punches it, o.tear frays it. */
  function patch(c, x, y, w, h, o) {
    const { t, R, D } = c, W = c.px(w), Hh = c.px(h), st = E(W, Hh), rag = o.rag || 1, rw = W / 2, rh = Hh / 2, tear = o.tear == null ? 1.5 : o.tear;
    const tf = (px, py, s) => hash2(Math.floor(px / D), Math.floor(py / D), s);   // the torn rim keeps its physical grain
    t.beginMark();
    for (const p of st.px) {
      const px = x + p[0] - st.ox, py = y + p[1] - st.oy; if (!R(px, py)) continue;
      const u = (p[0] + .5 - rw) / rw, v = (p[1] + .5 - rh) / rh, r = u * u + v * v;
      if (r > .5 && tf(px, py, rag) < (r - .5) * tear) continue;
      if (o.hole && hash2(px, py, rag + 1) < o.hole) continue;
      const i = t.i(px, py); if (t.lock[i] && !o.lock) continue;
      let b = o.band;
      if (r > .45 && u + v < -.4) { if (o.ul != null) b = o.ul; }
      else if (r > .45 && u + v > .4) { if (o.dr != null) b = o.dr; }
      else if (o.fleck && hash2(px, py, rag + 2) < o.fleck[1]) b = o.fleck[0];
      t.pal[i] = o.pal; t.band[i] = clamp(b, 0, 4); t.h[i] = o.h || 0; t.mark[i] = t.mid; if (o.lock) t.lock[i] = 1;
    }
    t.endMark();
  }
  /* a hollow: footprint, scour pan, puddle scar — dark, its upper-left corner darkest, its lower-right inner wall lit */
  function hollow(c, x, y, w, h, o) {
    const { t, R, D } = c, st = E(c.px(w), c.px(h)), dh = o.dh == null ? -.8 : o.dh;
    let bx = -1, by = -1;
    t.beginMark();
    for (const p of st.px) {
      const px = x + p[0] - st.ox, py = y + p[1] - st.oy; if (!R(px, py)) continue;
      const i = t.i(px, py); if (t.lock[i]) continue;
      t.pal[i] = o.pal; t.band[i] = 1; t.h[i] = (o.hBase == null ? t.h[i] : o.hBase) + dh; t.mark[i] = t.mid;
      if (p[1] > by || (p[1] === by && p[0] > bx)) { by = p[1]; bx = p[0]; }
    }
    const ul = st.px[0]; c.put(x + ul[0] - st.ox, y + ul[1] - st.oy, o.pal, 0);
    if (bx >= 0) { const lx = x + bx - st.ox, ly = y + by - st.oy; c.put(lx, ly, o.pal, o.gleam ? 4 : 3); for (let k = 1; k <= D; k++) if (w >= 4) c.put(lx - k, ly, o.pal, 3); }
    if (o.weed && w >= 5) c.put(x, y, o.weed, 2, undefined, 1);
    t.endMark();
  }
  /* a puddle: flat sky-coloured water, the bank's shadow on its upper-left edge, one glint */
  function puddle(c, x, y, w, h, o) {
    const { t, R, D } = c, W = c.px(w), Hh = c.px(h), st = E(W, Hh), rw = W / 2, rh = Hh / 2;
    t.beginMark();
    for (const p of st.px) {
      const px = x + p[0] - st.ox, py = y + p[1] - st.oy; if (!R(px, py)) continue;
      const u = (p[0] + .5 - rw) / rw, v = (p[1] + .5 - rh) / rh, r = u * u + v * v;
      if (r > .6 && hash2(Math.floor(px / D), Math.floor(py / D), o.rag) < (r - .6) * 1.4) continue;
      const i = t.i(px, py);
      if (r > .5 && u + v < -.5) { t.pal[i] = o.bank; t.band[i] = 0; } else { t.pal[i] = o.pal; t.band[i] = 3; }
      t.h[i] = 0; t.lock[i] = 1; t.mark[i] = t.mid;
    }
    const gx = x - Math.max(1, W >> 2), gy = y - (Hh >> 2);
    if (R(gx, gy)) { const i = c.t.i(gx, gy); if (c.t.pal[i] === o.pal) c.t.band[i] = 4; }
    t.endMark();
  }
  /* a scuff: a short dash that wanders a texel. len is authored texels; it wanders on the same
     physical pitch at any detail, so a scuff is a scuff and not a staircase. */
  function dash(c, x, y, len, pal, band, sd) {
    const D = c.D, L = c.px(len); let yy = y;
    c.t.beginMark();
    for (let k = 0; k < L; k++) { if (k > 0 && k % D === 0 && hash2(x + k, y, sd) < .18) yy += hash2(x + k, y, sd + 1) < .5 ? -1 : 1; c.put(x + k, yy, pal, band, -.3); }
    c.t.endMark();
  }

  /* a tussock: a fountain of blades, not a cushion. Tips a band up (the centre one two), bases a band
     down, the shade row under it. w 5 or 7 texels. */
  const TUSS = [stencil(['.o.o.', 'ooooo', '.ooo.']), stencil(['.o..o', 'oooo.', '.ooo.']), stencil(['o..o.', '.oooo', '.ooo.']), stencil(['o.o.o.o', '.ooooo.', '..ooo..']), stencil(['..o.o..', '.ooooo.', 'ooooooo', '.ooooo.']), stencil(['.o.o..o', 'oooooo.', '.ooooo.'])];
  function tussock(c, x, y, big, pal, sd, shadePal) {
    const t = c.t, D = c.D;
    t.beginMark();
    if (D === 1) {
      const st = TUSS[big ? 3 + Math.floor(hash2(x, y, sd) * 3) : Math.floor(hash2(x, y, sd) * 3)];
      for (const p of st.px) {
        const px = x + p[0] - st.ox, py = y + p[1] - st.oy; if (!c.R(px, py)) continue;
        const top = p[1] === 0, mid = p[1] === 1, centre = Math.abs(p[0] - (st.w >> 1)) <= 0;
        t.set(px, py, pal, top ? (centre ? 4 : 3) : mid ? 3 : (hash2(px, py, sd + 1) < .5 ? 2 : 1), top ? 1.1 : mid ? .8 : .4, 1);
      }
      for (let dx = -(st.w >> 1) + 1; dx <= (st.w >> 1) - 1; dx++) shade(c, x + dx, y + st.h - st.oy, { pal: shadePal, band: 1 });
      shade(c, x + (st.w >> 1), y + st.h - st.oy - 1, { pal: shadePal, band: 1 });
    } else {
      /* at a 1 px texel the fountain is drawn blade by blade instead of stamped from a 5-texel
         stencil: one blade per texel column, each with its own length and its own lean out of the
         crown, tips a band up and the middle tips two. This is the mark the finer grid was for. */
      const W = c.px(big ? 7 : 5), L = c.px(big ? 3.5 : 2.5), half = W >> 1, base = y + 1;
      for (let k = 0; k < W; k++) {
        const bx = x + k - half, u = half ? (k - half) / half : 0;
        const ln = Math.max(2, L - Math.round(hash2(bx, y, sd + k) * L * .4) - Math.round(Math.abs(u) * L * .3));
        for (let r = 0; r < ln; r++) {
          const px = bx + Math.round(u * r * .5 + (hash2(bx, y + r, sd + 5) - .5) * .9), py = base - r;
          if (!c.R(px, py)) continue;
          const top = r === ln - 1;
          t.set(px, py, pal, top ? (Math.abs(u) < .3 ? 4 : 3) : r > ln * .45 ? 3 : (hash2(px, py, sd + 1) < .5 ? 2 : 1), .3 + r * .3, 1);
        }
      }
      for (let dx = -half + 1; dx <= half - 1; dx++) shade(c, x + dx, base + 1, { pal: shadePal, band: 1 });
      for (let k = 0; k < D; k++) shade(c, x + half + k, base, { pal: shadePal, band: 1 });
    }
    t.endMark();
  }

  // ---- the seven floor materials ----------------------------------------------------------------------
  /* paint(c, step, seed) -> the palette handles the edge painter needs: ground (its body), fringe (what it hangs over the neighbour) */
  const MATS = {
    grass: { z: 2, relief: 1.2, lo: .36, hi: .78, seed: 3070, base: GROUND.grass,
      note: 'The sward. One flat mid green; tussocks — the lump in turf, 5–9 texels — crowd in drifts steered by a slow field; a nap of blade ticks each with its shadow texel; worn hollows where the soil shows, torn and holed, their upper-left wall in shade; flowers as single warm texels in one-species drifts; a fieldstone. Lo is grazed and worn to soil; Hi is rank, with seed-heads on the big tussocks and the flowers thick.',
      paint(c, st, sd) {
        const t = c.t, turf = t.addPal(GROUND.grass, .75), dry = t.addPal(GROUND.grassDry, .75), damp = t.addPal(GROUND.grassDamp, .75), soil = t.addPal(GROUND.soil, .8), straw = t.addPal(GROUND.straw, .7), stone = t.addPal(GROUND.stone, .9);
        const fl = [t.addPal(GROUND.buttercup, .8), t.addPal(GROUND.clover, .6), t.addPal(GROUND.selfheal, .8)];
        /* hue drifts at one value: the sward's marks go straw-green where the field is high, blue-green where it is low; the body never changes */
        const hue = (x, y) => { const f = c.F(x, y, 2, sd + 5); return f > .62 ? dry : f < .36 ? damp : turf; };
        c.fill(turf, 2, 0);
        for (const s of c.sites(28, .9, sd + 1, s => s.r < pk([.6, .28, .08], st) && c.deep(s.x, s.y, 7)))
          patch(c, s.x, s.y, 5 + Math.round(s.r2 * 8), 3 + Math.round(hash2(s.x, s.y, sd) * 4), { pal: soil, band: 2, ul: 1, dr: 2, fleck: [3, .14], hole: .07, rag: sd + 2, h: -.5 });
        for (const s of c.sites(pk([11, 8.5, 7], st), .95, sd + 10)) {
          const d = c.F(s.x, s.y, 4, sd + 11); if (s.r > (d - .22) * 1.7) continue;
          if (t.pal[t.i(s.x, s.y)] === soil) continue;
          const big = s.r2 > .62;
          tussock(c, s.x, s.y, big, hue(s.x, s.y), sd + 13, turf);
          if (st === 2 && big && hash2(s.x, s.y, sd + 12) < .5) { c.put(s.x - 1, s.y - 3, straw, 3, 1.2, 1); c.put(s.x - 1, s.y - 2, straw, 2, 1.0, 1); c.put(s.x + 2, s.y - 2, straw, 3, 1.2, 1); }
        }
        for (const s of c.sites(pk([4.6, 3.8, 3.2], st), .9, sd + 20, s => s.r < .42)) {
          const i = t.i(s.x, s.y); if (t.pal[i] !== turf || t.lock[i]) continue;
          const p = hue(s.x, s.y);
          c.put(s.x, s.y, p, 3, .6); if (s.r2 > .55) c.put(s.x, s.y - 1, p, s.r2 > .93 ? 4 : 3, .8);
          shade(c, s.x, s.y + 1, { pal: turf, band: 1 });
        }
        /* clover rosettes: a darker 3-texel plus, the second mark a lawn has */
        for (const s of c.sites(pk([16, 12, 10], st), .95, sd + 25, s => s.r < .3 && c.deep(s.x, s.y, 2))) { const i = t.i(s.x, s.y); if (t.pal[i] !== turf || t.lock[i]) continue; const d = c.D; t.beginMark(); c.put(s.x, s.y, damp, 2, .3, 1); c.put(s.x - d, s.y, damp, 1, .2, 1); c.put(s.x + d, s.y, damp, 2, .2, 1); c.put(s.x, s.y - d, damp, 3, .3, 1); c.put(s.x, s.y + d, damp, 1, .1, 1); if (d > 1) { c.put(s.x, s.y - 1, damp, 3, .3, 1); c.put(s.x - 1, s.y, damp, 2, .2, 1); } t.endMark(); }
        const fd = pk([0, .012, .03], st);
        if (fd) for (const s of c.sites(3, .95, sd + 30)) {
          const d = c.F(s.x, s.y, 3, sd + 31), p = (d - .55) / .45; if (p <= 0 || s.r > p * fd * 12) continue;
          if (t.pal[t.i(s.x, s.y)] === soil) continue;
          const k = Math.floor(hash2(s.x >> 5, s.y >> 5, sd + 32) * 3);
          c.dot(s.x, s.y, fl[k], 4, .8, 1); shade(c, s.x, s.y + c.D, { pal: turf, band: 1 });
        }
        for (const s of c.sites(44, .9, sd + 40, s => s.r < pk([.4, .25, .12], st) && c.deep(s.x, s.y, 3))) lump(c, s.x, s.y, 3, 2, { pal: stone, band: 2, h: .8, cap: false, shade: { pal: turf, band: 1 }, lock: 1 });
        return { ground: turf, fringe: turf };
      } },

    dirt: { z: 2, relief: 1.2, lo: .38, hi: .78, seed: 10110, base: GROUND.dirt,
      note: 'Trodden red earth — the yard, the bare field. Big pale crust plates, torn and holed, a band up (the packed dry skin); footprints and puddle scars as dark hollows with a lit lower-right wall; pebbles pressed flush; scuff dashes. No dark patches — the blobs are gone. Lo carries stubble and moss; Hi is churned into clods.',
      paint(c, st, sd) {
        const t = c.t, earth = t.addPal(GROUND.dirt, .8), peb = t.addPal(GROUND.peb, .8), straw = t.addPal(GROUND.straw, .7), moss = t.addPal(GROUND.moss, .7);
        c.fill(earth, 2, 0);
        for (const s of c.sites(24, .9, sd + 1, s => s.r < pk([.45, .55, .3], st)))
          patch(c, s.x, s.y, 8 + Math.round(s.r2 * 10), 5 + Math.round(hash2(s.x, s.y, sd) * 6), { pal: earth, band: 3, ul: 3, dr: 2, hole: .12, tear: 1.2, rag: sd + 2, h: .3 });
        for (const s of c.sites(13, .9, sd + 10, s => s.r < pk([.35, .45, .55], st) && c.deep(s.x, s.y, 3))) hollow(c, s.x, s.y, 3 + Math.round(s.r2 * 2), 2 + Math.round(s.r2 * 1.4), { pal: earth });
        for (const s of c.sites(pk([11, 9, 10], st), .9, sd + 20, s => s.r < .4 && c.deep(s.x, s.y, 2))) {
          if (s.r2 < .5) lump(c, s.x, s.y, 2, 2, { pal: earth, band: 3, h: .5, cap: false, seam: 0, shade: { pal: earth, band: 1 }, lock: 1 });          // a dry crumb
          else lump(c, s.x, s.y, 2 + (s.r2 > .85 ? 1 : 0), 2, { pal: peb, band: s.r2 > .95 ? 3 : 2, h: .7, cap: false, shade: { pal: earth, band: 1 }, lock: 1 });
        }
        for (const s of c.sites(15, .9, sd + 30, s => s.r < pk([.3, .5, .6], st))) dash(c, s.x, s.y, 3 + Math.round(s.r2 * 4), earth, 1, sd + 31);
        if (st === 0) for (const s of c.sites(6, .9, sd + 40, s => s.r < .35)) { if (s.r2 < .5) { c.put(s.x, s.y, straw, 3, .6, 1); c.put(s.x, s.y - 1, straw, 2, .8, 1); } else if (s.r2 < .72) lump(c, s.x, s.y, 3, 2, { pal: moss, band: 2, h: .5, cap: false, seam: 0, lock: 1 }); }
        if (st === 2) for (const s of c.sites(5.5, .9, sd + 50, s => s.r < .55)) lump(c, s.x, s.y, 2 + Math.round(s.r2 * 2), 2 + (s.r2 > .5 ? 1 : 0), { pal: earth, band: s.r2 > .75 ? 3 : 2, h: 1.0, cap: false, shade: { pal: earth, band: 1 } });
        return { ground: earth, fringe: earth };
      } },

    path: { z: 2, relief: 1.0, lo: .38, hi: .80, seed: 4410, base: GROUND.path,
      note: 'The track. A step lighter than dirt and two lighter than grass, so a path is a line you can follow from across the screen. Polished pale plates where feet and tyres have packed it, fine gravel as its grain, a few hollows and scuffs. Lo is after rain — damp dark patches, puddle scars with a glint; Hi is bone dry with straw blown across it.',
      paint(c, st, sd) {
        const t = c.t, earth = t.addPal(GROUND.path, .75), dk = t.addPal(GROUND.dirt, .8), grav = t.addPal(GROUND.gravel, .85), straw = t.addPal(GROUND.straw, .7);
        c.fill(earth, 2, 0);
        for (const s of c.sites(22, .9, sd + 1, s => s.r < pk([.3, .5, .6], st))) patch(c, s.x, s.y, 10 + Math.round(s.r2 * 12), 5 + Math.round(hash2(s.x, s.y, sd) * 5), { pal: earth, band: 3, ul: 3, dr: 2, hole: .1, tear: 1.1, rag: sd + 2, h: .2 });
        if (st === 0) {
          for (const s of c.sites(26, .9, sd + 5, s => s.r < .5)) patch(c, s.x, s.y, 6 + Math.round(s.r2 * 8), 4 + Math.round(hash2(s.x, s.y, sd) * 3), { pal: dk, band: 2, ul: 1, dr: 2, hole: .08, rag: sd + 6, h: -.3 });
          for (const s of c.sites(18, .9, sd + 7, s => s.r < .4 && c.deep(s.x, s.y, 3))) hollow(c, s.x, s.y, 4 + Math.round(s.r2 * 3), 2 + Math.round(s.r2 * 2), { pal: dk, gleam: 1 });
        }
        for (const s of c.sites(pk([5.2, 4.4, 4], st), .95, sd + 20, s => s.r < .3)) {
          if (t.lock[t.i(s.x, s.y)]) continue;
          if (s.r2 < .85) { c.put(s.x, s.y, grav, s.r2 < .2 ? 3 : 2, .5, 1); shade(c, s.x, s.y + 1, { pal: earth, band: 1 }); }
          else lump(c, s.x, s.y, 2, 2, { pal: grav, band: 2, h: .6, cap: false, shade: { pal: earth, band: 1 }, lock: 1 });
        }
        for (const s of c.sites(20, .9, sd + 30, s => s.r < .3 && c.deep(s.x, s.y, 3))) hollow(c, s.x, s.y, 3 + Math.round(s.r2 * 2), 2, { pal: earth });
        for (const s of c.sites(16, .9, sd + 40, s => s.r < .4)) dash(c, s.x, s.y, 4 + Math.round(s.r2 * 5), earth, 1, sd + 41);
        if (st === 2) for (const s of c.sites(9, .9, sd + 50, s => s.r < .3)) { c.put(s.x, s.y, straw, 3, .5, 1); if (s.r2 < .5) c.put(s.x + 1, s.y, straw, 2, .5, 1); }
        return { ground: earth, fringe: earth };
      } },

    mud: { z: 2, relief: 1.3, lo: .38, hi: .78, seed: 8080, base: GROUND.mud,
      note: 'Wet ground — the low corner of the yard, the creek margin, the flat behind the beach. Dark and cool; puddles as flat sky-coloured pools with the bank\'s shadow on their upper-left edge and one glint; boot and hoof prints as dark hollows with a lit rim; straw litter. Lo is drying — a cracked skin of Worley plates, pale crust curling on them; Hi is churned and flooded.',
      paint(c, st, sd) {
        const t = c.t, mud = t.addPal(GROUND.mud, .85), pool = t.addPal(GROUND.puddle, .7), straw = t.addPal(GROUND.straw, .7), crust = t.addPal(GROUND.crust, .7);
        c.fill(mud, 2, 0);
        if (st === 0) {
          const cx = c.cx(9), cy = c.cy(9);
          for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
            if (!c.R(x, y)) continue;
            const w = worleyP((x + .5) / c.n, (y + .5) / c.m, cx, cy, sd + 1, .9);
            if (w.d2 - w.d1 < .09 && c.F(x, y, 6, sd + 2) > .46) t.set(x, y, mud, 0, -.6);
            else if (hash2(w.id, 1, sd + 3) < .4 && w.d1 < .4) t.set(x, y, crust, hash2(w.id, 2, sd + 3) < .35 ? 3 : 2, .3);
          }
        }
        for (const s of c.sites(pk([30, 24, 18], st), .9, sd + 10, s => s.r < pk([.35, .55, .7], st) && c.deep(s.x, s.y, 8))) {
          const w = 5 + Math.round(s.r2 * pk([5, 8, 11], st)), h = 3 + Math.round(hash2(s.x, s.y, sd) * (w * .5));
          puddle(c, s.x, s.y, w, h, { pal: pool, bank: mud, rag: sd + 11 });
        }
        for (const s of c.sites(pk([14, 9, 11], st), .9, sd + 20, s => s.r < .55 && c.deep(s.x, s.y, 3))) hollow(c, s.x, s.y, 2 + Math.round(s.r2 * 2), 2 + (s.r2 > .6 ? 1 : 0), { pal: mud });
        if (st === 2) for (const s of c.sites(6, .9, sd + 30, s => s.r < .5)) lump(c, s.x, s.y, 2 + Math.round(s.r2 * 2), 2 + (s.r2 > .5 ? 1 : 0), { pal: mud, band: s.r2 > .7 ? 3 : 2, h: 1.0, cap: false, shade: { pal: mud, band: 1 } });
        for (const s of c.sites(12, .9, sd + 40, s => s.r < .3)) { if (t.lock[t.i(s.x, s.y)]) continue; c.put(s.x, s.y, straw, 3, .5, 1); if (s.r2 < .5) c.put(s.x + 1, s.y, straw, 2, .5, 1); }
        return { ground: mud, fringe: mud };
      } },

    sand: { z: 2, relief: 1.0, lo: .40, hi: .80, seed: 5510, base: GROUND.sand,
      note: 'Flat ochre, brought down off white so the sprites still own the top of the value range. Grain steered by a slow field (density, not value), shell grit as a bright texel with its shade, a dark pebble now and then. Lo is damp — a film of sheen where the tide left, pale where it is drying; Hi is dry and wind-worked, the ripple as short broken crescents, never rows.',
      paint(c, st, sd) {
        const t = c.t, sand = t.addPal(st === 0 ? GROUND.damp : GROUND.sand, .6), shell = t.addRamp(['#8f7a62', '#b39d80', '#e6d9bd', '#f3ead4', '#fbf6e8']), peb = t.addPal(GROUND.dark, .8), dry = t.addPal(GROUND.sand, .6);
        c.fill(sand, 2, 0);
        if (st === 0) {
          for (const s of c.sites(30, .9, sd + 1, s => s.r < .45)) patch(c, s.x, s.y, 6 + Math.round(s.r2 * 8), 3 + Math.round(hash2(s.x, s.y, sd) * 3), { pal: sand, band: 3, ul: 3, dr: 3, hole: .2, tear: 1.6, rag: sd + 2 });
          for (const s of c.sites(34, .9, sd + 3, s => s.r < .3)) patch(c, s.x, s.y, 8 + Math.round(s.r2 * 10), 4 + Math.round(hash2(s.x, s.y, sd) * 4), { pal: dry, band: 2, ul: 2, dr: 2, hole: .25, tear: 1.8, rag: sd + 4 });
        }
        const g = pk([.02, .028, .034], st);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue; const i = t.i(x, y); if (t.lock[i]) continue;
          const f = c.F(x, y, 5, sd) - .5, r = hash2(x, y, sd + 5), pl = g * .5 * clamp(1 + f * 2.6, .1, 2), pd = g * .5 * clamp(1 - f * 2.6, .1, 2);
          if (r < pl) { t.band[i] = 3; t.h[i] = .25; } else if (r < pl + pd) { t.band[i] = 1; t.h[i] = -.25; }
        }
        for (const s of c.sites(11, .95, sd + 10, s => s.r < pk([.12, .22, .28], st))) { c.dot(s.x, s.y, shell, s.r2 < .15 ? 4 : 3, .5, 1); shade(c, s.x, s.y + c.D, { pal: sand, band: 1 }); }
        for (const s of c.sites(24, .9, sd + 20, s => s.r < pk([.35, .22, .18], st) && c.deep(s.x, s.y, 2))) lump(c, s.x, s.y, 2, 2, { pal: peb, band: 2, h: .6, cap: false, shade: { pal: sand, band: 1 }, lock: 1 });
        if (st === 2) for (const s of c.sites(9, .9, sd + 30, s => s.r < .55)) { const len = c.px(3 + Math.round(s.r2 * 5)), amp = 1.2 * c.D; t.beginMark(); for (let k = 0; k < len; k++) { const a = Math.round(Math.sin((k + .5) / len * Math.PI) * amp), x = s.x + k - (len >> 1); c.put(x, s.y - a, sand, 3, .4); for (let q = 1; q <= c.D; q++) c.put(x, s.y - a + q, sand, 1, -.3); } t.endMark(); }
        return { ground: sand, fringe: sand };
      } },

    shingle: { z: 2, relief: 1.6, lo: .40, hi: .76, seed: 2110, base: GROUND.grit,
      note: 'Cobbles you can count. Each is the lump — flat body, lit tip, dark seam, a cap on the big ones, a dark grit shadow under its foot — on a fine-gravel matrix. Half as many stones as pass two and twice the size, so they read as stones and not as static; the sorting field crowds them into a berm and thins them to a lag. Lo is pea gravel; Hi is cobble lag with big stones standing proud.',
      paint(c, st, sd) {
        const t = c.t, grit = t.addPal(GROUND.grit, .8), grey = t.addPal(GROUND.grey, .9), tan = t.addPal(GROUND.tan, .9), red = t.addPal(GROUND.red, .9), dark = t.addPal(GROUND.dark, .9);
        c.fill(grit, 1, 0);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) { if (!c.R(x, y)) continue; const r = hash2(x, y, sd); if (r < .10) t.set(x, y, grit, 2, .2); else if (r > .95) t.set(x, y, grit, 0, -.2); }
        for (const s of c.sites(pk([3.4, 4.4, 5.4], st), .8, sd + 20)) {
          const so = c.F(s.x, s.y, 3, sd + 21); if (s.r > .4 + so * .65) continue;
          let w = pk([2, 3, 4], st) + Math.round((so - .35) * pk([2, 4, 6], st) + s.r2 * pk([1, 2, 3], st));
          if (st === 2 && s.r2 > .94) w += 3;
          w = clamp(w, 2, 10); const h = Math.max(2, Math.round(w * (.6 + hash2(s.x, s.y, sd) * .25)));
          if (!c.deep(s.x, s.y, (w >> 1) + 1)) continue;
          const pal = s.r2 < .6 ? grey : s.r2 < .82 ? tan : s.r2 < .94 ? red : dark;
          lump(c, s.x, s.y, w, h, { pal, band: s.r < .1 ? 3 : 2, h: w * .45, hBase: 0, shade: { pal: grit, band: 0 }, cap: w >= 5 });
        }
        return { ground: grit, fringe: grey, fringe2: tan };
      } },

    ledge: { z: 2, relief: 1.4, lo: .40, hi: .76, seed: 1630, base: GROUND.ledge,
      note: 'The red wave-cut platform as slab benches — fewer, bigger, mostly one level, so it is a floor and not a mosaic. A riser away from the light is a two-texel shadow; toward it, one lit texel. Joints only where two benches at one level meet, gated hard. Scour pans and weed arrive on the ladder.',
      paint(c, st, sd) {
        const t = c.t, rock = t.addPal(GROUND.ledge, .9), weed = t.addPal(GROUND.weed, .8), cob = t.addPal(GROUND.cob, .9);
        const n = c.n, m = c.m, lvl = new Int8Array(n * m).fill(1), id = new Int32Array(n * m), cells = pk([5, 6, 7], st), cx = c.cx(cells), cy = c.cy(cells), D = c.D;
        for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
          const u = (x + .5) / n, v = (y + .5) / m, wu = u + (c.F(x, y, 4, sd + 1) - .5) * .12, wv = v + (c.F(x, y, 4, sd + 2) - .5) * .12;
          const w = worleyP(wu, wv, cx, cy, sd, .9), i = y * n + x, r = hash2(w.id, 1, sd);
          id[i] = w.id; lvl[i] = st === 0 ? (r < .82 ? 1 : 2) : r < .12 ? 0 : r < .76 ? 1 : 2;
        }
        for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
          if (!c.R(x, y)) continue; const i = y * n + x, L = lvl[i], lam = c.F(x, y, 6, sd + 4);
          t.set(x, y, rock, L === 2 ? 3 : L === 0 ? 1 : 2, L * 2.2 + (lam > .7 ? .3 : 0));
          if (lam > .76 && hash2(x, y, sd + 5) < .5) t.shift(x, y, -1);
        }
        const at = (x, y) => c.R(x, y) ? lvl[t.i(x, y)] : 1;
        for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
          if (!c.R(x, y)) continue; const i = y * n + x;
          const L = at(x, y), dR = L - at(x + 1, y), dD = L - at(x, y + 1), dL = L - at(x - 1, y), dU = L - at(x, y - 1);
          if (dR > 0 || dD > 0) t.shift(x, y, 1, 1);
          if (dR > 0) for (let k = 1; k <= (dR > 1 ? 2 * D : D); k++) c.put(x + k, y, rock, 0, undefined, 1);
          if (dD > 0) for (let k = 1; k <= (dD > 1 ? 2 * D : D); k++) c.put(x, y + k, rock, 0, undefined, 1);
          if (dL < 0 || dU < 0) t.shift(x, y, 1, 1);
          const jg = c.F(x, y, 5, sd + 7), jt = pk([.84, .74, .68], st);
          if (L === at(x + 1, y) && c.R(x + 1, y) && id[i] !== id[t.i(x + 1, y)] && jg > jt) { const wd = st >= 1 && hash2(x >> 2, y >> 2, sd + 8) < pk([0, .3, .45], st); c.put(x, y, wd ? weed : rock, wd ? 1 : 1, undefined, 1); }
          if (L === at(x, y + 1) && c.R(x, y + 1) && id[i] !== id[t.i(x, y + 1)] && jg > jt) c.put(x, y, rock, 1, undefined, 1);
        }
        if (st >= 1) for (const s of c.sites(22, .9, sd + 30, s => s.r < pk([0, .35, .55], st) && c.deep(s.x, s.y, 5))) hollow(c, s.x, s.y, 5 + Math.round(s.r2 * 3), 3 + Math.round(s.r2 * 1.5), { pal: rock, hBase: t.h[t.i(s.x, s.y)], dh: -1.4, weed: st === 2 ? weed : null });
        if (st === 2) for (const s of c.sites(12, .9, sd + 40, s => s.r < .3 && c.deep(s.x, s.y, 2))) lump(c, s.x, s.y, 3, 2, { pal: cob, band: 2, h: .9, cap: false, shade: { pal: rock, band: 1 } });
        return { ground: rock, fringe: rock };
      } },
  };
  const ORDER = ['grass', 'dirt', 'path', 'mud', 'sand', 'shingle', 'ledge'];

  /* ---- how a material behaves under a PAINT TOOL ---------------------------------------------------
     A splat weight is not an opacity. Averaging two banded palettes invents colour that is in neither
     of them, which is exactly what pass two's cross-fades did; a paint tool has to CHOOSE per texel.
     PxLang.packBlend bakes that choice order into the _blend channel, and these are its knobs:

       markLead  which arrives first as the weight rises. 1 = the marks (a few cobbles land on the
                 sand long before the grit does, which is how a shingle beach actually encroaches).
                 0 = the matrix (silt closes over the flat as a surface and its burrows come last).
       grain     cells across the tile for the field the coverage grows along — low is broad blotches,
                 high is a fine wandering frontier.
       bias      texels added to this material's kit height when it competes with another at equal
                 weight. It is the RANK table's argument in height terms: what stands proud wins the
                 texel, so a stone keeps its whole silhouette against sand instead of being nibbled. */
  const BLEND = {
    _default: { markLead: .5, grain: 5, bias: 0 },
    grass: { markLead: .85, grain: 4, bias: .6 },
    dirt: { markLead: .30, grain: 5, bias: 0 },
    path: { markLead: .25, grain: 6, bias: -.2 },
    mud: { markLead: .20, grain: 4, bias: -.8 },
    sand: { markLead: .15, grain: 5, bias: -.4 },
    shingle: { markLead: 1, grain: 3, bias: 1.2 },
    ledge: { markLead: .5, grain: 3, bias: 1.6 },
  };

  function build(key, step, o) {
    const M = MATS[key]; if (!M) throw new Error('PxKit: no material ' + key);
    const D = Math.max(1, Math.round((o && o.detail) || DETAIL)), N = (o && o.N) || 128 * D;
    const t = new Tile(N, N), c = makeCtx(t, null, { detail: D });
    const pals = M.paint(c, step | 0, (o && o.seed) || M.seed);
    return { tile: t, z: Math.max(1, M.z / D), relief: M.relief * D, lo: M.lo, hi: M.hi, pals, detail: D, blend: BLEND[key] || BLEND._default };
  }
  function paintRegion(t, key, step, R, seed, detail) { const M = MATS[key], c = makeCtx(t, R, { detail: detail || DETAIL }); return { key, pals: M.paint(c, step | 0, seed || M.seed) }; }

  // ---- edges: who overhangs whom, and what it hangs ------------------------------------------------------
  const RANK = { grass: 0, ledge: 1, shingle: 2, dirt: 3, path: 3, mud: 5, sand: 6 };
  const FRINGE = { grass: 'blade', ledge: 'lip', shingle: 'stones', dirt: 'crumb', path: 'crumb', mud: 'crumb' };
  /* reg[i] = index into mats (255 = none); mats[k] = {key, pals}. Pass 1: the higher material's cut face
     (a dark row on its S and E sides), the shade it throws on the lower one (two rows for a rock lip),
     a broken lit rim where its edge faces the light. Pass 2: the fringe — blades lying over the edge,
     crumbs, spilled stones. Every texel touched is marked in c.touched (the strip bake keeps those). */
  function edges(c, reg, mats, sd) {
    const { t, n, m } = c, touched = c.touched = new Uint8Array(n * m);
    const T = (x, y) => { touched[t.i(x, y)] = 1; };
    const regAt = (x, y) => (x < 0 || y < 0 || x >= n || y >= m) ? 255 : reg[y * n + x];
    const D4 = [[1, 0], [0, 1], [-1, 0], [0, -1]];
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const a = reg[y * n + x]; if (a === 255) continue; const A = mats[a];
      for (const [dx, dy] of D4) {
        const b = regAt(x + dx, y + dy); if (b === 255 || b === a) continue; const B = mats[b];
        if (RANK[A.key] == null || RANK[B.key] == null || RANK[A.key] >= RANK[B.key]) continue;
        const i = t.i(x, y), lip = FRINGE[A.key] === 'lip', D = c.D;
        if (dx > 0 || dy > 0) {
          for (let k = 0; k < D; k++) {                                       // the cut face, D texels into A
            const ax = x - dx * k, ay = y - dy * k; if (regAt(ax, ay) !== a) break;
            const j = t.i(ax, ay); if (t.lock[j]) continue;
            t.pal[j] = A.pals.ground; t.band[j] = Math.min(t.band[j], k === 0 ? 1 : 2); T(ax, ay);
          }
          for (let k = 1; k <= (lip ? 2 * D : D); k++) {                      // the shade it throws on B
            const bx = x + dx * k, by = y + dy * k; if (regAt(bx, by) !== b) break;
            const j = t.i(bx, by); if (t.lock[j]) continue;
            t.pal[j] = B.pals.ground; t.band[j] = Math.min(t.band[j], k <= D ? 1 : 2); T(bx, by);
          }
        } else if (!t.lock[i] && hash2(Math.floor(x / D), Math.floor(y / D), sd) < (lip ? .8 : .4)) { t.band[i] = Math.min(4, Math.max(t.band[i], 3)); T(x, y); }
      }
    }
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const a = reg[y * n + x]; if (a === 255) continue; const A = mats[a], fr = FRINGE[A.key]; if (!fr || fr === 'lip') continue;
      for (const [dx, dy] of D4) {
        const b = regAt(x + dx, y + dy); if (b === 255 || b === a) continue; const B = mats[b];
        if (RANK[A.key] == null || RANK[B.key] == null || RANK[A.key] >= RANK[B.key]) continue;
        const r = hash2(x, y, sd + 3 + dx * 5 + dy * 7), D = c.D;
        if (fr === 'blade') {
          if (r > .5 / D) continue;
          const horiz = dy === 0, bx = x + dx, by = y + dy; if (regAt(bx, by) !== b) continue;
          const len = c.px(1 + (hash2(x, y, sd + 9) < .5 ? 1 : 0));
          let k = 0;
          for (; k < len; k++) {
            const px = bx + (horiz ? dx * k : 0), py = by + (horiz ? 0 : dy * k); if (regAt(px, py) !== b) break;
            const j = t.i(px, py); if (t.lock[j]) break;
            t.pal[j] = A.pals.fringe; t.band[j] = k < D ? 3 : 2; t.h[j] = .6; t.lock[j] = 1; T(px, py);
          }
          if (dy > 0) { const sy = by + k; if (regAt(bx, sy) === b) { const j = t.i(bx, sy); if (!t.lock[j]) { t.band[j] = Math.min(t.band[j], 1); T(bx, sy); } } }
        } else if (fr === 'crumb') {
          if (r > .22 / D) continue;
          const far = c.px(r * D < .08 ? 2 : 1), bx = x + dx * far, by = y + dy * far; if (regAt(bx, by) !== b) continue;
          lumpB(c, bx, by, r * D < .12 ? 2 : 1, r * D < .12 ? 2 : 1, A.pals.fringe, b, reg, T, B.pals.ground);
        } else if (fr === 'stones') {
          if (r > .35 / D) continue;
          const dist = c.px(1 + Math.floor(hash2(x, y, sd + 13) * 4)), bx = x + dx * dist, by = y + dy * dist; if (regAt(bx, by) !== b) continue;
          lumpB(c, bx, by, 2 + (hash2(x, y, sd + 14) < .35 ? 1 : 0), 2, hash2(x, y, sd + 15) < .7 ? A.pals.fringe : (A.pals.fringe2 == null ? A.pals.fringe : A.pals.fringe2), b, reg, T, B.pals.ground);
        }
      }
    }
  }
  function lumpB(c, x, y, w, h, pal, b, reg, T, shadePal) {
    const sub = Object.assign({}, c, { R: (px, py) => px >= 0 && py >= 0 && px < c.n && py < c.m && reg[py * c.n + px] === b });
    lump(sub, x, y, w, h, { pal, band: 2, h: .7, cap: false, lock: 1, shade: { pal: shadePal, band: 1 } });
    const st = E(c.px(w), c.px(h)); for (const p of st.px) { const px = x + p[0] - st.ox, py = y + p[1] - st.oy; if (sub.R(px, py)) T(px, py); if (sub.R(px, py + 1)) T(px, py + 1); if (sub.R(px + 1, py)) T(px + 1, py); }
  }
  /* an edge strip: material A over material B along a wandering line. orient N = A above · S = A below ·
     W = A left · E = A right. A's side ships opaque (lay the strip with its A rows inside the A tile);
     B's side is alpha except the marks. 128 × 12 texels = 256 × 24 px. */
  function strip(aKey, bKey, orient, st, sd, detail) {
    const D = Math.max(1, Math.round(detail || DETAIL)), L = 128 * D, Wd = 12 * D, horiz = orient === 'N' || orient === 'S';
    const n = horiz ? L : Wd, m = horiz ? Wd : L, t = new Tile(n, m), reg = new Uint8Array(n * m);
    sd = sd || 0;
    const line = s => D * 5 + Math.round(D * (1.3 * Math.sin(s / L * Math.PI * 4 + sd) + (fbmP(s / L, .5, 4, 1, sd + 1, 2) - .5) * 3.4));
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const s = horiz ? x : y, d = horiz ? y : x, ln = line(s);
      reg[y * n + x] = (orient === 'N' || orient === 'W' ? d < ln : d >= Wd - ln) ? 0 : 1;
    }
    const mats = [aKey, bKey].map((key, k) => paintRegion(t, key, st, (x, y) => x >= 0 && y >= 0 && x < n && y < m && reg[y * n + x] === k, MATS[key].seed + sd * 3, D));
    const c = makeCtx(t, null, { detail: D }); edges(c, reg, mats, sd + 77);
    for (let i = 0; i < n * m; i++) t.a[i] = (reg[i] === 0 || c.touched[i]) ? 255 : 0;
    return { tile: t, z: Math.max(1, 2 / D), relief: 1.2 * D, lo: .38, hi: .78, detail: D };
  }
  const PAIRS = [['grass', 'dirt'], ['grass', 'path'], ['grass', 'sand'], ['grass', 'shingle'], ['grass', 'mud'], ['dirt', 'sand'], ['shingle', 'sand'], ['ledge', 'sand']];
  /* corner pieces, 24 × 24 texels = 48 px. kind 'o' = outer: A fills the named quadrant (its corner points
     inward); kind 'i' = inner: B fills the named quadrant, A everything else. Region built in its final
     orientation, so the seam painter's screen-space light is right without rotating anything. */
  function corner(aKey, bKey, kind, quad, st, sd, detail) {
    const D = Math.max(1, Math.round(detail || DETAIL)), n = 24 * D, t = new Tile(n, n), reg = new Uint8Array(n * n); sd = sd || 0;
    const wd = v => 12 * D + Math.round(D * (1.2 * Math.sin(v / n * Math.PI * 2 + sd) + (fbmP(v / n, .5, 2, 1, sd + 1, 2) - .5) * 3));
    for (let y = 0; y < n; y++) for (let x = 0; x < n; x++) {
      const qx = quad[1] === 'W' ? x : n - 1 - x, qy = quad[0] === 'N' ? y : n - 1 - y;      // into the named quadrant's frame
      const inQ = qx < wd(qy) && qy < wd(qx);
      reg[y * n + x] = kind === 'o' ? (inQ ? 0 : 1) : (inQ ? 1 : 0);
    }
    const mats = [aKey, bKey].map((key, k) => paintRegion(t, key, st, (x, y) => x >= 0 && y >= 0 && x < n && y < n && reg[y * n + x] === k, MATS[key].seed + sd * 5 + 7, D));
    const c = makeCtx(t, null, { detail: D }); edges(c, reg, mats, sd + 91);
    for (let i = 0; i < n * n; i++) t.a[i] = (reg[i] === 0 || c.touched[i]) ? 255 : 0;
    return { tile: t, z: Math.max(1, 2 / D), relief: 1.2 * D, lo: .38, hi: .78, detail: D };
  }
  const QUADS = ['NW', 'NE', 'SE', 'SW'];

  /* the wrack line: what the last high tide left along the sand — weed ticks, straw, a shell, a pebble,
     strewn a texel or two either side of the line. Paints onto texels where R is true. */
  function wrack(c, sd) {
    const t = c.t, weed = t.addPal('#4b4a2c', .8), straw = t.addPal(GROUND.straw, .7), shell = t.addRamp(['#8f7a62', '#b39d80', '#e6d9bd', '#f3ead4', '#fbf6e8']), peb = t.addPal(GROUND.dark, .8);
    for (const s of c.sites(2.2, .9, sd, s => s.r < .32)) {
      const r = s.r2, i = t.i(s.x, s.y), D = c.D; if (t.lock[i]) continue;
      if (r < .5) { const len = c.px(1 + Math.floor(hash2(s.x, s.y, sd + 1) * 3)), horiz = hash2(s.x, s.y, sd + 2) < .6; t.beginMark(); for (let k = 0; k < len; k++) { const px = s.x + (horiz ? k : 0), py = s.y + (horiz ? 0 : k); if (!c.R(px, py)) break; t.set(px, py, weed, k < D ? 2 : 1, .4, 1); } t.endMark(); }
      else if (r < .75) { c.dot(s.x, s.y, straw, 3, .4, 1); if (hash2(s.x, s.y, sd + 3) < .5) c.dot(s.x + D, s.y, straw, 2, .4, 1); }
      else if (r < .88) c.dot(s.x, s.y, shell, 3, .5, 1);
      else lump(c, s.x, s.y, 2, 2, { pal: peb, band: 2, h: .6, cap: false, lock: 1 });
    }
    return { weed, straw, shell, peb };
  }
  /* a wrack strip, 128 × 12 texels: the line runs along s (horizontal for N, vertical for W); alpha except the marks */
  function wrackStrip(orient, sd, detail) {
    const D = Math.max(1, Math.round(detail || DETAIL)), L = 128 * D, Wd = 12 * D, horiz = orient !== 'W', n = horiz ? L : Wd, m = horiz ? Wd : L, t = new Tile(n, m); sd = sd || 0;
    const line = s => D * 6 + Math.round(D * (1.5 * Math.sin(s / L * Math.PI * 4 + sd) + (fbmP(s / L, .5, 3, 1, sd + 1, 2) - .5) * 4));
    const R = (x, y) => { if (x < 0 || y < 0 || x >= n || y >= m) return false; const s = horiz ? x : y, d = horiz ? y : x; return Math.abs(d - line(s)) <= D + (hash2(Math.floor(s / D), 3, sd) < .3 ? D : 0); };
    const c = makeCtx(t, R, { detail: D }); t.a.fill(0); wrack(c, sd + 40);
    for (let i = 0; i < n * m; i++) t.a[i] = t.lock[i] ? 255 : 0;
    return { tile: t, z: Math.max(1, 2 / D), relief: 1.2 * D, lo: .38, hi: .78, detail: D };
  }

  // ---- rocks: the lump at boulder size, seen from the ¾ camera --------------------------------------------
  const CE = Math.cos(40 * Math.PI / 180), SEL = Math.sin(40 * Math.PI / 180);
  const ROCKS = {
    granite: { base: '#7d7c78', c: .95, e: 2.2, tall: .6, depth: .8, flecks: .045, note: 'ice-dropped erratic — rounded, unbedded, feldspar flecks' },
    sandstone: { base: '#a25c40', c: .95, e: 3.4, tall: .38, depth: .85, beds: 1, note: 'the red bed as a slab — flat-topped, laminated down its face' },
    basalt: { base: '#4f555c', c: 1.0, e: 5.0, tall: .62, depth: .7, joints: 1, plateau: .55, note: 'a fallen column — angular, a flat top, vertical joints' },
  };
  /* silhouette = the top ellipse (the footprint lifted by the rock's height × cos 40°) plus the south
     wall down to the footprint's south contour. Height field: a dome on the top, a slope down the
     wall toward the camera — so the same banded key that lights a cobble lights a boulder: tip and
     cap toward the upper-left, the wall in shade, the contact row dark. */
  function rock(kind, size, seed) {
    const K = ROCKS[kind] || ROCKS.granite, r1 = hash2(seed, 1, 3), r2 = hash2(seed, 2, 3), r3 = hash2(seed, 3, 3);
    const rx = size / 2, rd = size * K.depth * SEL / 2 * (.85 + r1 * .3), hz = size * K.tall * CE * (.8 + r2 * .4), e = K.e * (.85 + r3 * .3);
    const W = size + 2, ch = Math.ceil(rd * 2 + hz) + 3, t = new Tile(W, ch); t.a.fill(0);
    const pal = t.addPal(K.base, K.c), fl = K.flecks ? t.addRamp(['#3a3a3a', '#5a5a58', '#8a8a86', '#c8c6bf', '#e8e6df']) : -1;
    const cx = W / 2, cyF = ch - 1.5, cyT = cyF - hz;                  // footprint centre row, top-ellipse centre row
    const ey = x => rd * Math.pow(Math.max(0, 1 - Math.pow(Math.abs(x) / rx, e)), 1 / e);
    const plat = K.plateau || 0;
    for (let y = 0; y < ch; y++) for (let x = 0; x < W; x++) {
      const X = x + .5 - cx, top = cyT - ey(X), bot = cyF + ey(X); if (Math.abs(X) >= rx || y + .5 < top || y + .5 > bot) continue;
      const i = y * W + x; t.a[i] = 255; t.pal[i] = pal; t.band[i] = 2;
      const Y = y + .5 - cyT, q = Math.pow(Math.abs(X) / rx, e) + Math.pow(Math.abs(Y) / Math.max(.5, rd), e);
      if (q <= 1) {                                                      // on the top
        let d = Math.pow(1 - q, 1 / e); if (plat) d = Math.min(d, plat) / plat;
        t.h[i] = hz + d * size * .22 + (fbmP(x / W, y / ch, 3, 3, seed, 2) - .5) * size * .06;
      } else {                                                           // on the wall
        const f = (y + .5 - (cyT + ey(X))) / Math.max(1, cyF + ey(X) - (cyT + ey(X)));
        t.h[i] = hz * (1 - f);
        if (K.beds && ((y - Math.floor(cyT)) % 4) < 2 && hash2(x >> 2, y >> 2, seed + 5) < .7) t.band[i] = 1;
        if (K.joints && hash2(Math.floor((x + Math.floor(r1 * 3)) / 4), 1, seed + 6) < .45 && (x + Math.floor(r1 * 3)) % 4 === 0) t.band[i] = 1;
      }
      if (y === Math.floor(bot - .5)) { t.band[i] = 0; t.h[i] = 0; }         // the contact row
    }
    if (fl >= 0) for (let i = 0; i < W * ch; i++) if (t.a[i] && hash2(i % W, (i / W) | 0, seed + 7) < K.flecks) { t.pal[i] = fl; t.band[i] = hash2(i % W, (i / W) | 0, seed + 8) < .7 ? 4 : 1; t.lock[i] = 1; }
    return { tile: t, z: 1, relief: 1.1, lo: .40, hi: .74, pivot: [W / 2, ch - 1], w: W, h: ch };
  }

  // ---- bushes: a basket of leaf stamps on a shell, lower over upper, the dark between the leaves -------------
  const SCALE = .8, M2PX = 32 * SCALE;
  const SHRUBS = {
    alder: { base: '#4a6a38', c: .95, hM: 1.9, wM: 1.9, form: 'round', grain: 'leaf', stems: 5, stem: '#4a3a2c', skirt: .18, note: 'speckled alder — the swale shrub, a round basket on many stems' },
    bayberry: { base: '#66784f', c: .85, hM: 1.2, wM: 1.7, form: 'flat', grain: 'coin', stems: 4, stem: '#5a4a3c', skirt: .85, fleck: ['#c6cfc2', .05], note: 'bayberry — grey-green, wide and low, waxy grey berries' },
    wildrose: { base: '#4f7040', c: .95, hM: 1.3, wM: 1.5, form: 'round', grain: 'leaf', stems: 6, stem: '#5a4030', skirt: .62, fleck: ['#e08ab0', .08], note: 'wild rose — pink singles presented on the lit side' },
    blueberry: { base: '#4a6636', c: .9, hM: .72, wM: 1.45, form: 'flat', grain: 'coin', stems: 3, stem: '#6a3a30', skirt: .95, fleck: ['#4a5a9a', .05], note: 'lowbush blueberry — knee-high, red twigs, blue fruit' },
    juniper: { base: '#3d5a48', c: 1.0, hM: .7, wM: 2.3, form: 'sprawl', grain: 'scale', stems: 2, stem: '#4a3a2c', skirt: 1, note: 'common juniper — a sprawling mat of blue-green scales' },
  };
  const BST = {
    leaf: [stencil(['.oo.', 'oooo', 'oooo', '.oo.']), stencil(['.ooo.', 'ooooo', '.ooo.']), stencil(['ooo', 'ooo', '.o.'])],
    coin: [stencil(['.o.', 'ooo', '.o.']), stencil(['oo', 'oo']), stencil(['.oo.', 'oooo', '.oo.'])],
    scale: [stencil(['o.o', 'ooo', '.o.']), stencil(['.o.o.', 'ooooo', '.ooo.']), stencil(['o.o.o', '.ooo.', '..o..'])],
  };
  /* THE SHELL.  Two to four lobes, never one ellipse. This is the defect pass six shipped: `round`
     and `flat` both resolved to a single centred dome, so alder and wild rose had the same outline,
     and all four variants of a species had it too — a hedgerow was one silhouette repeated down the
     row. Lobes sit on a crown line, their radii driven by which one leads (the asymmetry is the
     whole read), and the rim is torn by a per-lobe angular wobble instead of swept clean. */
  function shellOf(B, sd, cx, baseY, hp, wp, W) {
    const n = B.form === 'sprawl' ? 3 + (hash2(sd, 70, 1) < .45 ? 1 : 0) : 2 + (hash2(sd, 70, 1) < .40 ? 1 : 0);
    const crown = B.form === 'round' ? .58 : B.form === 'flat' ? .50 : .46;
    /* lobes MUST overlap. Spread is held under the sum of two adjacent radii, or the low forms come
       apart into two blobs with a bare stem between them — which is what the first fix shipped. */
    const spread = B.form === 'sprawl' ? .72 : B.form === 'flat' ? .60 : .48;
    const leadAt = (hash2(sd, 71, 1) - .5) * 1.5, lobes = [];
    for (let k = 0; k < n; k++) {
      const u = n === 1 ? 0 : (k / (n - 1)) * 2 - 1, j = i => hash2(sd, 80 + k * 9 + i, 1);
      const lead = 1 - Math.abs(u - leadAt) * .26;
      lobes.push({
        x: cx + u * wp * spread * .5 + (j(2) - .5) * wp * .07,
        y: baseY - hp * crown * (.80 + .30 * lead) + (j(3) - .5) * hp * .12,
        rx: Math.max(4, wp * (.34 + .13 * j(0)) * lead), ry: Math.max(4, hp * (.32 + .14 * j(1)) * lead),
        p: j(4) * 6.3, q: j(5) * 6.3,
      });
    }
    /* OVERLAP IS NOT OPTIONAL. Pass seven set a spread under the sum of two radii and called it
       done — but centres exactly a radius-sum apart touch at ONE texel, and the rim wobble then
       tears that join open. Every adjacent pair is pulled to 0.72 of its radius sum, which is a
       join no wobble can break. */
    lobes.sort((a, b) => a.x - b.x);
    for (let k = 1; k < lobes.length; k++) {
      const a = lobes[k - 1], b = lobes[k], d = b.x - a.x, want = (a.rx + b.rx) * .72;
      if (d > want) { const pull = (d - want) / 2; a.x += pull; b.x -= pull; }
    }
    /* THE SKIRT LOBE. Every bush in pass seven was a canopy floating over bare stems — a lollipop
       tree at 1/20 scale, not a shrub. A bayberry, a blueberry and a juniper carry leaf to the
       ground; an alder mostly doesn't. `skirt` is that per species: a low wide lobe sitting on the
       base line, which the canopy pass fills and which hides the stems behind it. */
    const sk = B.skirt == null ? .5 : B.skirt;
    if (sk > .05) {
      const ry = Math.max(3, hp * (.16 + .14 * sk));
      lobes.push({
        x: cx + (hash2(sd, 90, 1) - .5) * wp * .10, y: baseY - ry * .62,
        rx: Math.max(5, wp * (.30 + .26 * sk)), ry,
        p: hash2(sd, 91, 1) * 6.3, q: hash2(sd, 92, 1) * 6.3, skirt: 1,
      });
    }
    /* fit the shell to the cell: a sprawling juniper is wider than its 64 px cell, and pass six let
       it run off both walls */
    let lo = 1e9, hi = -1e9; for (const d of lobes) { lo = Math.min(lo, d.x - d.rx * 1.18); hi = Math.max(hi, d.x + d.rx * 1.18); }
    const room = W - 3, span = hi - lo;
    if (span > room) { const s = room / span; for (const d of lobes) { d.x = cx + (d.x - cx) * s; d.rx *= s; } }
    return lobes;
  }
  function bush(key, variant) {
    const B = SHRUBS[key] || SHRUBS.alder, sd = 900 + variant * 131 + key.length * 7;
    const W = 64, Hc = 64, t = new Tile(W, Hc); t.a.fill(0);
    const pal = t.addPal(B.base, B.c), wood = t.addPal(B.stem, .9), fl = B.fleck ? t.addPal(B.fleck[0], .8) : -1;
    const hp = B.hM * M2PX * (.9 + hash2(sd, 1, 1) * .2), wp = Math.min(W * .80, B.wM * M2PX * (.9 + hash2(sd, 2, 1) * .2));
    const baseY = Hc - 2, cx = W / 2;
    const lobes = shellOf(B, sd, cx, baseY, hp, wp, W);
    const wob = (a, d) => 1 + .12 * Math.sin(a * 3 + d.p) + .08 * Math.sin(a * 5 - d.q) + .05 * Math.sin(a * 8 + d.p + d.q);
    /* the crown must not be cut by the cell: pass six let a full-height alder lose its top row. */
    let top = 1e9; for (const d of lobes) top = Math.min(top, d.y - d.ry * 1.22);
    if (top < 1) for (const d of lobes) d.y += (1 - top);
    const inDome = (x, y) => {
      let best = null;
      for (const d of lobes) {
        const u = (x + .5 - d.x) / d.rx, v = (y + .5 - d.y) / d.ry, w = wob(Math.atan2(v, u), d), q = (u * u + v * v) / (w * w);
        if (q <= 1 && (!best || q < best.q)) best = { q, u, v };
      }
      return best;
    };
    const mark = (x, y) => { if (x >= 0 && y >= 0 && x < W && y < Hc) t.a[y * W + x] = 255; };
    /* STEMS.  Pass six ran every stem as the same parabola out of the same point at one width —
       chopsticks under a dome. A stem now leaves its own place on a root plate, tapers as it climbs,
       aims at the lobe above it, and most of them fork once on the way. */
    const wBase = hp > 30 ? 2 : 1;
    const limb = (x0, y0, x1, y1, w0, s) => {
      const L = Math.max(2, Math.round(Math.hypot(x1 - x0, y1 - y0)));
      for (let k = 0; k <= L; k++) {
        const f = k / L, e = f * f * (3 - 2 * f);
        const xx = Math.round(x0 + (x1 - x0) * e + (hash2(s, k, 2) - .5) * 1.7), yy = Math.round(y0 + (y1 - y0) * f);
        const w = Math.max(1, Math.round(w0 * (1 - f * .7)));
        for (let d = 0; d < w; d++) { const px = xx + d - (w >> 1); if (px < 0 || px >= W || yy < 0 || yy >= Hc) continue; t.set(px, yy, wood, d === 0 ? 2 : 1, .3, 1); mark(px, yy); }
      }
    };
    for (let k = 0; k < B.stems; k++) {
      const f = B.stems === 1 ? .5 : k / (B.stems - 1), jx = hash2(sd, 20 + k, 1), jy = hash2(sd, 30 + k, 1), jf = hash2(sd, 40 + k, 1);
      const x0 = Math.round(cx + (f - .5) * wp * .14 + (jx - .5) * 2.4);
      const lb = lobes[Math.min(lobes.length - 1, Math.floor(f * lobes.length * .999))];
      const tx = Math.round(lb.x + (jf - .5) * lb.rx * .8), ty = Math.round(lb.y + (jy * .5 + .15) * lb.ry);
      const midY = Math.round(baseY - (baseY - ty) * (.44 + jy * .22)), midX = Math.round(x0 + (tx - x0) * .34);
      limb(x0, baseY, midX, midY, wBase, sd + k * 11);
      limb(midX, midY, tx, ty, Math.max(1, wBase - 1), sd + k * 11 + 5);
      if (jf < .55) limb(midX, midY, Math.round(midX + (hash2(sd, 50 + k, 1) - .5) * wp * .40), Math.round(ty + hash2(sd, 60 + k, 1) * lb.ry * .4), 1, sd + k * 11 + 9);
    }
    const G = BST[B.grain], pitch = (B.grain === 'leaf' ? 3.2 : 2.6) * clamp(.62 + hp / 90, .62, 1.05);
    for (const s of sites(W, Hc, pitch, .8, sd + 50, s => { const q = inDome(s.x, s.y); return q && (q.q > .2 || hash2(s.x, s.y, sd) < .35); })) {
      const q = inDome(s.x, s.y), nz = Math.sqrt(Math.max(0, 1 - q.q)), l = -q.u * .55 - q.v * .66 + nz * .52;
      let b = l > .62 ? 3 : l > .28 ? 2 : l > 0 ? 1 : 0; if (l > .8 && hash2(s.x, s.y, sd + 1) < .5) b = 4;
      if (hash2(s.x, s.y, sd + 2) < .15) b += hash2(s.x, s.y, sd + 3) < .5 ? -1 : 1;
      const st = G[Math.floor(s.r2 * G.length)];
      stamp(t, st, s.x, s.y, { pal, band: clamp(b, 0, 4), seam: 1, tip: 1, h: 1 + nz * 4, hBase: 0 });
      for (const p of st.px) mark(s.x + p[0] - st.ox, s.y + p[1] - st.oy);
    }
    /* the dark between the leaves: an empty texel inside the shell with three painted neighbours is inside the bush */
    const a0 = new Uint8Array(t.a);
    for (let y = 1; y < Hc - 1; y++) for (let x = 1; x < W - 1; x++) {
      const i = y * W + x; if (a0[i] || !inDome(x, y)) continue;
      const nb = a0[i - 1] + a0[i + 1] + a0[i - W] + a0[i + W]; if (nb < 3 * 255) continue;
      t.a[i] = 255; t.pal[i] = pal; t.band[i] = 0; t.h[i] = .5;
    }
    if (fl >= 0) for (const s of sites(W, Hc, 2, .9, sd + 60)) { const q = inDome(s.x, s.y); if (!q || !t.a[s.y * W + s.x]) continue; const l = -q.u * .55 - q.v * .66 + Math.sqrt(Math.max(0, 1 - q.q)) * .52; if (l < .3 || s.r > B.fleck[1] * 6) continue; t.set(s.x, s.y, fl, 4, undefined, 1); }
    /* the skirt: the bottom rows of the canopy go down a band, so the bush has a dark underside
       instead of glowing where it meets its own shadow */
    for (let x = 0; x < W; x++) { let lo = -1; for (let y = 0; y < Hc; y++) if (t.a[y * W + x] && t.pal[y * W + x] === pal) lo = y; if (lo < 0) continue; for (let k = 0; k < 2; k++) { const i = (lo - k) * W + x; if (lo - k >= 0 && t.a[i] && t.pal[i] === pal) t.band[i] = clamp(t.band[i] - (k ? 1 : 2), 0, 4); } }
    let wl = W, wr = 0; for (let x = 0; x < W; x++) for (let y = 0; y < Hc; y++) if (t.a[y * W + x]) { if (x < wl) wl = x; if (x > wr) wr = x; break; }
    return { tile: t, z: 1, relief: 1.6, lo: .40, hi: .74, lit: 0, pivot: [cx, baseY + 1], w: W, h: Hc,
      shadow: { rx: Math.max(3, (wr - wl) * .42), ry: Math.max(2, (wr - wl) * .17) } };
  }

  // ---- flowers: a stem, two leaf ticks, a head — single · clump · patch, ringless ------------------------------
  const FLOWERS = {
    buttercup: { col: '#e6c53c', dk: '#b08a20', h: 14, head: 'disc', note: 'buttercup — a 3 px disc with a dark centre' },
    daisy: { col: '#f0ece0', dk: '#e0b030', h: 18, head: 'disc', note: 'oxeye daisy — white disc, yellow centre' },
    lupin: { col: '#7b5fa8', dk: '#5a4088', tip: '#b39ad0', h: 26, head: 'spike', note: 'lupin — a purple spike, paler at the tip' },
    fireweed: { col: '#c9578f', dk: '#9a3a6a', tip: '#e79ac0', h: 30, head: 'spike', note: 'fireweed — magenta spike, the tall one' },
    queenanne: { col: '#efece4', dk: '#c9c4b4', h: 24, head: 'umbel', note: 'Queen Anne\'s lace — a flat lacy umbel on stalklets' },
  };
  const TIERS = { single: [48, 40], clump: [48, 40], patch: [48, 34] };
  /* one plant. Clipped to the cell — pass six wrote through Tile.i, which WRAPS, so a patch plant
     near the rim reappeared as debris down the opposite edge of the cell. Leaf reach scales with
     height, so a 5 px seedling is not given a 3 px leaf. */
  function flowerPlant(t, pal, S, x0, baseY, h, sd, scale, W, Hc) {
    h = Math.max(3, Math.round(h * (scale || 1)));
    const lean = Math.round((hash2(sd, 1, 4) - .5) * 4 * Math.min(1, h / 12));
    const on = (px, py, p, b, hh) => { if (px < 0 || py < 0 || px >= W || py >= Hc) return; t.set(px, py, p, b, hh == null ? 1.5 : hh, 1); t.a[py * W + px] = 255; };
    let x = x0;
    for (let s = 0; s <= h; s++) { x = x0 + Math.round(lean * (s / h) * (s / h)); on(x, baseY - s, pal.leaf, s < h * .3 ? 1 : 2, .4); }
    const reach = h < 8 ? 1 : h < 15 ? 2 : 3;
    for (const [f, dir] of [[.32, -1], [.58, 1]]) {
      const yy = baseY - Math.round(h * f), xx = x0 + Math.round(lean * f * f);
      for (let k = 1; k <= reach; k++) on(xx + dir * k, yy - (k === reach ? 1 : 0), pal.leaf, k === reach ? 3 : 2, .6);
    }
    const hx = x, hy = baseY - h;
    if (S.head === 'disc') {
      if (h < 8) { on(hx, hy, pal.col, 4); on(hx + 1, hy, pal.col, 3); on(hx, hy + 1, pal.dk, 2); }
      else { on(hx, hy - 1, pal.col, 4); on(hx - 1, hy, pal.col, 3); on(hx + 1, hy, pal.col, 3); on(hx, hy + 1, pal.col, 2); on(hx, hy, pal.dk, 2); }
    } else if (S.head === 'spike') {
      const len = Math.max(2, Math.round(h * .38));
      for (let k = 0; k < len; k++) { const yy = hy + k, xx = hx + (k % 2 ? -1 : 1); on(hx, yy, pal.col, 2, 1.2); on(xx, yy, k < 2 ? pal.tip : pal.col, k < 2 ? 4 : (k % 2 ? 2 : 3), 1.2); }
    } else {
      if (h < 12) { for (const [dx, dy, b] of [[-1, -1, 4], [0, -1, 4], [1, -1, 3], [-1, 0, 3], [0, 0, 3], [1, 0, 2]]) on(hx + dx, hy + dy, pal.col, b); }
      else { for (const [dx, dy, b] of [[-1, -2, 4], [0, -2, 4], [1, -2, 3], [-2, -1, 3], [-1, -1, 3], [0, -1, 3], [1, -1, 3], [2, -1, 3], [-3, 0, 3], [-1, 0, 2], [1, 0, 3], [3, 0, 2]]) on(hx + dx, hy + dy, pal.col, b); for (const dx of [-2, 0, 2]) on(hx + dx, hy + 1, pal.leaf, 1, .5); }
    }
  }
  function flower(key, tier, seed) {
    const S = FLOWERS[key] || FLOWERS.buttercup, [W, Hc] = TIERS[tier] || TIERS.single, t = new Tile(W, Hc); t.a.fill(0);
    const pal = { leaf: t.addPal('#4c7a45', .9), col: t.addPal(S.col, .8), dk: t.addPal(S.dk, .8), tip: t.addPal(S.tip || S.col, .6) };
    const baseY = Hc - 2, cx = W >> 1, sd = 500 + seed * 37 + key.length;
    let spread = 6;
    if (tier === 'single') flowerPlant(t, pal, S, cx, baseY, S.h, sd, 1, W, Hc);
    else if (tier === 'clump') {
      const k = 3 + Math.floor(hash2(sd, 2, 4) * 3), plants = [];
      for (let i = 0; i < k; i++) plants.push({ x: cx + Math.round((hash2(sd, 10 + i, 4) - .5) * 20), y: baseY + Math.round((hash2(sd, 20 + i, 4) - .5) * 5), sc: .65 + hash2(sd, 30 + i, 4) * .35 });
      plants.sort((a, b) => a.y - b.y).forEach((p, i) => flowerPlant(t, pal, S, p.x, Math.min(baseY + 1, p.y), S.h, sd + i, p.sc, W, Hc));
      spread = 13;
    } else {
      /* PATCH.  Pass six scattered single coloured texels with 1–3 px stalks on an ellipse: at the
         1 px texel that is confetti on the grass, and the species was unreadable — a lupin patch and
         a daisy patch differed only in hue. A patch is now a STAND: short plants of the same build
         as the single tier, on a density field that crowds the middle and thins to stragglers at the
         rim, taller toward the back so the drift has depth. */
      const plants = [];
      for (let i = 0; i < 30; i++) {
        const u = hash2(sd, 40 + i, 4), v = hash2(sd, 60 + i, 4), g = hash2(sd, 100 + i, 4);
        const x = Math.round(cx + (u - .5) * W * .84), d = Math.abs(x - cx) / (W * .42);
        if (g < d * d * .8) continue;                                  // the rim thins out
        const y = Math.round(baseY - v * 10);
        plants.push({ x, y, sc: (.30 + .24 * hash2(sd, 80 + i, 4)) * (1 - d * .20) + (baseY - y) * .012 });
      }
      plants.sort((a, b) => a.y - b.y).forEach((p, i) => flowerPlant(t, pal, S, p.x, p.y, S.h, sd + i * 3, p.sc, W, Hc));
      spread = 20;
    }
    return { tile: t, z: 1, relief: 1.2, lo: .40, hi: .74, lit: 0, pivot: [cx, baseY + 1], w: W, h: Hc,
      shadow: { rx: spread, ry: Math.max(2, spread * .34) } };
  }

  /* the contact shade a sprite throws on the floor it stands on. The kit's sprites had none — a bush
     and a flower patch both floated, which is the loudest defect in a composed scene. Paints into a
     FLOOR context at the sprite's pivot: an ellipse offset away from the key, ground only, never
     over another mark, two steps so the core is darker than the penumbra. */
  function groundShade(c, x, y, rx, ry, o) {
    o = o || {}; const t = c.t, D = c.D;
    const RX = Math.max(1, rx * D), RY = Math.max(1, ry * D), dep = o.depth == null ? 1 : o.depth;
    const cxp = x + Math.round((o.dx == null ? .45 : o.dx) * RX), cyp = y + Math.round((o.dy == null ? .30 : o.dy) * RY);
    for (let dy = -Math.ceil(RY) - 1; dy <= Math.ceil(RY) + 1; dy++) for (let dx = -Math.ceil(RX) - 1; dx <= Math.ceil(RX) + 1; dx++) {
      const u = dx / RX, v = dy / RY, q = u * u + v * v; if (q > 1.02) continue;
      const px = cxp + dx, py = cyp + dy; if (!c.R(px, py)) continue;
      const i = t.i(px, py); if (t.lock[i]) continue;
      t.band[i] = clamp(t.band[i] - (q < .45 ? 2 * dep : dep), 0, 4);
    }
  }

  root.PxKit = { GROUND, STEPS, DETAIL, MATS, ORDER, BLEND, RANK, FRINGE, PAIRS, QUADS, ROCKS, SHRUBS, FLOWERS, TIERS, makeCtx, lump, patch, hollow, puddle, dash, shade, tussock, build, paintRegion, edges, strip, corner, wrack, wrackStrip, rock, bush, flower, groundShade };
})(typeof globalThis !== 'undefined' ? globalThis : window);
