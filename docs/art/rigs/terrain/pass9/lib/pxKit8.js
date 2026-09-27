/* Hidden Harbours — PIXEL KIT, PASS EIGHT: the floor on the trees' light, and the detail it was missing.

   Pass seven's floor was authored for one sun. Every mark carried its light in its bands — an
   upper-left tip, a down-right seam, a cap offset toward the key, a painted shade under it — so the
   moment TreeRig4 put the sun anywhere else, the ground contradicted the trees standing on it. And
   seen at 1× under the new sky, several materials were thin or wrong:

     grass     a flat olive body with tussocks on it and salmon ellipses for worn ground: the sward
               had no nap at the scale the eye reads a lawn by, and the bare patches read as stains.
     dirt/path pebbles with a plus-shaped cap read as sparkles; hollows were black holes with a lit
               lip painted on; a track had no grain.
     sand      almost empty: a few dark dashes. No heavy-mineral, no hash, no tracks.
     ripple    straight horizontal steps with dashes in them — the corduroy pass three banned —
     foreshore and the same on the flat.
     talus     a Voronoi net of flat plates with dark outlines: a tiled floor, not scree.
     mussel    blue-grey flagstones; oyster reef as round medallions; Irish moss as maroon glaze.
     rockweed  diagonal olive hatching on rock.
     marsh     one tussock stamp on a grid; sedge as lily pads; silt as grey mould.

   What this pass does:
     · STRUCTURE-ONLY BANDS.  Every re-authored material paints identity (which palette, which band
       of it the material IS) and HEIGHT — no light. TerrainLight4 lights the height live: the lit
       side, the seam away from the sun, the tip toward it, the shadow a cobble throws at 19:00.
       Materials not re-authored here (shingle, eelgrass, bank) are un-baked by TerrainLight4.
     · SURFACE CLASS per palette, registered here, so rain and snow know sand from granite.
     · MORE DETAIL where it was thin, all of it height the light can find: a blade-tick nap and
       rosettes and clover drifts in the sward, boot prints and pressed gravel, heavy-mineral wisps,
       shell hash and gull tracks on sand, sinuous broken ripples with water in the troughs,
       blocks with shoulders in the talus, individual mussels and oysters, fronds with bladders,
       lugworm casts, laminated benches with pits that hold the rain.
     · A COAST.  coast() builds the whole transect a scene needs — subtidal, ledge and weed, ripple
       flat, foreshore, berm, beach and wrack, dune, meadow and path, a marsh with its creek — with
       an elevation field in metres, so TerrainLight4 can run the tide and the swash over it.

     PxKit8.MATS (= PxKit.MATS, re-authored in place; the pass-seven paint kept as paint7)
     PxKit8.build(key, step, {pass}) · floor(W, H, defs, sd) · coast(o) · CHANGED · NOTES          */
(function (root) {
  'use strict';
  const P = root.PxLang, K = root.PxKit, K2 = root.PxKit2, TL = root.TerrainLight4;
  if (!P || !K || !K2 || !TL) throw new Error('pxKit8: pixelLanguage, pxKit, pxKit2 and terrainLight4 must load first');
  const { Tile, stamp, stencil, ellipse, hash2, fbmP, worleyP, clamp, r2h } = P;
  const { MATS, GROUND: G, makeCtx } = K, S = K2.SHORE, CL = TL.CL;
  const pk = (a, st) => a[clamp(st, 0, 2)];
  const SHELL_R = ['#8f7a62', '#b39d80', '#e6d9bd', '#f3ead4', '#fbf6e8'];

  // ---- classes: every base the kit paints with ------------------------------------------------------
  const REG = [
    [CL.VEG, [G.grass, G.grassDry, G.grassDamp, G.buttercup, G.clover, G.selfheal, G.moss, S.marram, S.spartina, S.sedge, S.rush, S.moss, S.zostera, S.zosOld]],
    [CL.SOIL, [G.soil, G.dirt, G.path, G.crust, S.till, S.bank, S.bankFresh]],
    [CL.SAND, [G.sand, G.damp, S.ripple, S.foreshore, S.runnel, S.dune, S.siltSand, S.mineral]],
    [CL.ROCK, [G.stone, G.peb, G.gravel, G.grit, G.grey, G.tan, G.red, G.dark, G.ledge, G.cob, S.shelfA, S.shelfB, S.shelfC, S.ledgeA, S.ledgeB, S.ledgeC, S.talus, S.rock, S.cobble, S.lichen]],
    [CL.MUD, [G.mud, S.silt, S.siltCrust, S.anox, S.bedMud, S.peat, S.pan]],
    [CL.WATER, [G.puddle, G.water, '#2d4e5c', '#3d6470', '#56666a']],
    [CL.WEED, [G.weed, S.fucus, S.asco, S.ulva, S.chondrus, S.chondrusSun, '#4b4a2c', '#6b3348']],
    [CL.SHELL, [S.barn, S.cultch, S.oyster, S.oysterFresh, S.shell, S.mussel, S.musselOld, SHELL_R[2]]],
    [CL.LITTER, [G.straw, S.thatch, S.marramDry]],
  ];
  for (const [c, list] of REG) for (const h of list) if (h) TL.CLASS_OF.set(h.toLowerCase(), c);
  function pal(t, base, c, cls) { const p = t.addPal(base, c); TL.CLASS_OF.set(r2h(t.pals[p][2]), cls); (t.live || (t.live = []))[p] = 1; return p; }
  function ramp(t, hexes, cls) { const p = t.addRamp(hexes); TL.CLASS_OF.set(hexes[2].toLowerCase(), cls); (t.live || (t.live = []))[p] = 1; return p; }

  // ---- marks, structure only: the light is TerrainLight4's ----------------------------------------------
  const ELL = {}; const E = (w, h) => { w = Math.max(1, w | 0); h = Math.max(1, h | 0); const k = w + 'x' + h; return ELL[k] || (ELL[k] = ellipse(w, h)); };
  /* a stone, a clod, a cushion: one flat band and a dome of height. No cap, no tip, no seam — the
     plus-shaped cap on a 4-texel pebble is what made pass seven's dirt sparkle. */
  function pebble(c, x, y, w, h, o) {
    const t = c.t, st = E(c.px(w), c.px(h));
    t.beginMark();
    stamp(t, st, x, y, { pal: o.pal, band: o.band == null ? 2 : o.band, seam: 0, tip: 0, h: o.h == null ? Math.max(0.5, w * 0.4) : o.h, hBase: o.hBase == null ? 0 : o.hBase, lock: o.lock == null ? 1 : o.lock, clip: c.R });
    t.endMark();
  }
  /* a hollow: a print, a pit, a pan. Its walls are drawn by the light (the lit one is the one facing the sun) */
  function print(c, x, y, w, h, o) {
    const t = c.t, st = E(c.px(w), c.px(h)), dh = o.dh == null ? -0.7 : o.dh, rw = st.w / 2, rh = st.h / 2;
    t.beginMark();
    for (const p of st.px) {
      const px = x + p[0] - st.ox, py = y + p[1] - st.oy; if (!c.R(px, py)) continue;
      const i = t.i(px, py); if (t.lock[i]) continue;
      const u = (p[0] + 0.5 - rw) / rw, v = (p[1] + 0.5 - rh) / rh;
      t.pal[i] = o.pal; t.band[i] = o.band == null ? 1 : o.band; t.h[i] = (o.hBase == null ? 0 : o.hBase) + dh * Math.sqrt(Math.max(0, 1 - (u * u + v * v) * 0.9)); t.mark[i] = t.mid;
    }
    t.endMark();
  }
  /* a fountain of blades, one per texel column, bases dark and tips pale (that is the plant, not the light) */
  function tuft(c, x, y, o) {
    const t = c.t, W = Math.max(2, c.px(o.w)), L = Math.max(2, c.px(o.len)), half = W >> 1, sd = o.seed || 0, sp = o.spread == null ? 0.55 : o.spread;
    t.beginMark();
    for (let k = 0; k < W; k++) {
      const bx = x + k - half, u = half ? (k - half) / half : 0;
      const ln = Math.max(2, L - Math.round(hash2(bx, y, sd + k) * L * 0.45) - Math.round(Math.abs(u) * L * 0.35)), lean = (o.lean || 0) + u * sp;
      const p = o.pal2 != null && hash2(bx, y, sd + 3) < (o.mixP == null ? 0.3 : o.mixP) ? o.pal2 : o.pal;
      for (let r = 0; r < ln; r++) {
        const px = bx + Math.round(lean * r + (hash2(bx, y + r, sd + 5) - 0.5) * 0.8), py = y - r;
        if (!c.R(px, py)) continue;
        t.set(px, py, p, r === ln - 1 ? 3 : r > ln * 0.5 ? 2 : (hash2(px, py, sd + 1) < 0.5 ? 2 : 1), 0.3 + r * 0.32, 1);
      }
      if (o.head != null && ln >= L * 0.8 && hash2(bx, y, sd + 9) < (o.headP || 0.2)) {
        const px = bx + Math.round(lean * ln), py = y - ln;
        for (let q = 0; q < 2; q++) if (c.R(px, py - q)) t.set(px, py - q, o.head, 3, 0.3 + (ln + q) * 0.32, 1);
      }
    }
    t.endMark();
  }
  /* the grain of a surface: single texels a band up or down, their density steered by a slow field */
  function grain(c, p, g, sd, hh) {
    const t = c.t;
    for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
      if (!c.R(x, y)) continue; const i = t.i(x, y); if (t.lock[i] || t.pal[i] !== p) continue;
      const f = c.F(x, y, 5, sd) - 0.5, r = hash2(x, y, sd + 1);
      if (r < g * clamp(1 + f * 2.4, 0.2, 2)) { t.band[i] = 3; t.h[i] += hh; } else if (r > 1 - g * clamp(1 - f * 2.4, 0.2, 2)) { t.band[i] = 1; t.h[i] -= hh; }
    }
  }
  /* bedded rock as benches. Each level its own bed palette; laminations run across a bench on its own
     strike; joints either run the whole length where two benches at one level meet or not at all.
     No riser is painted — its height is, and the light finds it for the hour. */
  function benches(c, st, sd, B, o) {
    const t = c.t, n = c.n, m = c.m, cells = pk(o.cells, st), cx = c.cx(cells), cy = c.cy(cells), lvl = new Int8Array(n * m), id = new Int32Array(n * m), th = o.levels;
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      if (!c.R(x, y)) continue;
      const u = (x + 0.5) / n, v = (y + 0.5) / m, w = worleyP(u + (c.F(x, y, 4, sd + 1) - 0.5) * 0.1, v + (c.F(x, y, 4, sd + 2) - 0.5) * 0.1, cx, cy, sd, 0.9);
      const i = y * n + x, r = hash2(w.id, 1, sd); id[i] = w.id; lvl[i] = r < th[0] ? 0 : r < th[1] ? 1 : 2;
      const a = hash2(w.id, 2, sd) * Math.PI, s = (x * Math.cos(a) + y * Math.sin(a)) / c.D, lam = (((Math.floor(s) % 4) + 4) % 4) === 0 && c.F(x, y, 5, sd + 4) > 0.55;
      const ti = t.i(x, y); t.pal[ti] = B[lvl[i]]; t.band[ti] = lam ? 1 : 2; t.h[ti] = lvl[i] * o.step + (lam ? -0.08 : 0); t.mark[ti] = (w.id % 60000) + 1; t.lock[ti] = 0;
    }
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      if (!c.R(x, y)) continue; const i = y * n + x, L = lvl[i];
      for (const [dx, dy] of [[1, 0], [0, 1]]) {
        const X = x + dx, Y = y + dy; if (X >= n || Y >= m || !c.R(X, Y)) continue; const j = Y * n + X;
        if (id[j] === id[i] || lvl[j] !== L) continue;
        if (hash2(Math.min(id[i], id[j]), Math.max(id[i], id[j]) + 7, sd + 17) > pk(o.joint, st)) continue;
        const ti = t.i(x, y); t.band[ti] = 1; t.h[ti] = L * o.step - 0.18; t.mark[ti] = 0;
        if (o.weed != null && hash2(x >> 1, y >> 1, sd + 8) < pk(o.weedP, st)) t.pal[ti] = o.weed;
      }
    }
    return { lvl, id };
  }
  /* a ripple field, evaluated per texel so crests are continuous: a periodic pitch bent by two fields,
     planed off where a slow field says, each crest broken along its length. Stoss long, lee short;
     the crest drier and a band paler; heavy mineral and, if asked, a film of water in the trough. */
  function rippleField(c, sd, o) {
    const t = c.t, n = c.n, m = c.m, D = c.D, rows = Math.max(3, Math.round(m / (o.pitch * D))), lam = m / rows, bc = Math.max(2, c.cx(o.brkCells || 10));
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      if (!c.R(x, y)) continue;
      const warp = (c.F(x, y, 2, sd + 1) - 0.5) * o.amp * D + (c.F(x, y, 6, sd + 2) - 0.5) * o.amp * 0.45 * D;
      const q = (y + warp) / lam, row = Math.floor(q), s = q - row;
      const flat = c.F(x, y, 2.5, sd + 3) < o.clear || fbmP((x + 0.5) / n, (((row % rows) + rows) % rows + 0.5) / rows, bc, rows, sd + 5, 2) < o.brk;
      let hh = 0, b = 2;
      if (!flat) { hh = (s < 0.7 ? s / 0.7 : (1 - s) / 0.3) - 0.5; hh *= o.h; b = s > 0.6 && s < 0.75 ? 3 : (s < 0.1 || s > 0.95) ? 1 : 2; }
      t.set(x, y, o.pal, b, hh);
      if (!flat && (s < 0.1 || s > 0.95) && hash2(x, y, sd + 7) < o.min) t.set(x, y, o.minPal, 2, hh - 0.1);
      if (o.water != null && !flat && (s < 0.05 || s > 0.975) && c.F(x, y, 3, sd + 8) > o.wet) t.set(x, y, o.water, 2, hh - 0.25);
    }
  }
  const GULL = [[0, 0], [1, 0], [2, 0], [1, -1], [2, -2], [1, 1], [2, 2]];
  const COIL = [[[0, 0], [1, 0], [1, -1], [0, -1], [-1, 0], [-1, 1], [0, 1], [1, 1], [2, 1]], [[0, 0], [1, 0], [2, 0], [2, -1], [1, -1], [0, 1], [1, 1], [-1, 1]]];
  const MUS = [stencil(['.ooo', 'ooo.']), stencil(['ooo.', '.ooo']), stencil(['.o', 'oo', 'oo', 'o.']), stencil(['..oo', '.oo.', 'oo..']), stencil(['oo..', '.oo.', '..oo'])];
  const OYS = [stencil(['.ooo.', 'ooooo', '.oo..']), stencil(['.oo', 'ooo', 'ooo', 'oo.', '.o.']), stencil(['..ooo', '.oooo', 'ooo..', 'oo...']), stencil(['ooo..', 'oooo.', '..ooo'])];
  const BLOB = [stencil(['.o.', 'ooo', 'oo.']), stencil(['oo', 'oo', '.o']), stencil(['.oo', 'ooo', '.o.']), stencil(['ooo', '.oo'])];

  // ---- the re-authored materials ------------------------------------------------------------------------------
  const NEW = {
    grass: {
      note: 'The sward on a 1 px nap: blade ticks everywhere, two texels at most, density steered by a slow field and never value; the dark between blades; tussocks as fountains in drifts, seed-heads at Hi; plantain and dandelion rosettes lying flat; clover drifts in a bluer green with white heads; buttercup, self-heal and hawkweed in one-species drifts; worn ground where the nap thins and the soil shows in torn 2 × 2 flecks, not as a salmon ellipse.',
      paint(c, st, sd) {
        const t = c.t, turf = pal(t, G.grass, 0.75, CL.VEG), dry = pal(t, G.grassDry, 0.75, CL.VEG), damp = pal(t, G.grassDamp, 0.75, CL.VEG), soil = pal(t, G.soil, 0.8, CL.SOIL);
        const straw = pal(t, G.straw, 0.7, CL.LITTER), stone = pal(t, G.stone, 0.9, CL.ROCK), clov = pal(t, '#4b6e41', 0.75, CL.VEG);
        const fl = [pal(t, G.buttercup, 0.8, CL.VEG), pal(t, G.clover, 0.6, CL.VEG), pal(t, G.selfheal, 0.8, CL.VEG), pal(t, '#d9803a', 0.8, CL.VEG)];
        const hue = (x, y) => { const f = c.F(x, y, 2, sd + 5); return f > 0.6 ? dry : f < 0.38 ? damp : turf; };
        const wT = pk([0.56, 0.68, 0.8], st), worn = (x, y) => clamp((c.F(x, y, 3, sd + 2) - wT) / 0.14, 0, 1);
        c.fill(turf, 2, 0);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue; const w = worn(x, y);
          if (w > 0.15 && hash2(x >> 1, y >> 1, sd + 3) < (w - 0.15) * 0.55) t.set(x, y, soil, hash2(x >> 1, y >> 1, sd + 4) < 0.3 ? 1 : 2, -0.3);
        }
        const dens = pk([0.09, 0.14, 0.18], st);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue; const i = t.i(x, y); if (t.lock[i]) continue;
          const d = dens * clamp(0.5 + c.F(x, y, 4, sd + 7), 0.3, 1.4) * (1 - worn(x, y) * 0.8), r = hash2(x, y, sd + 8);
          if (r < d) { const p = hue(x, y), L = 1 + (hash2(x, y, sd + 9) < 0.5 ? 1 : 0) + (st === 2 && hash2(x, y, sd + 10) < 0.35 ? 1 : 0); for (let k = 0; k < L; k++) c.put(x, y - k, p, k === L - 1 ? 3 : 2, 0.2 + k * 0.22); }
          else if (r > 1 - d * 0.5 && t.pal[i] === turf) t.band[i] = 1;
        }
        for (const s of c.sites(pk([12, 9, 7.5], st), 0.95, sd + 10)) {
          const d = c.F(s.x, s.y, 4, sd + 11); if (s.r > (d - 0.2) * 1.6 || worn(s.x, s.y) > 0.5) continue;
          const big = s.r2 > 0.6, p = hue(s.x, s.y);
          tuft(c, s.x, s.y, { w: big ? 6 : 4, len: big ? 3.6 : 2.4, pal: p, pal2: p === turf ? dry : turf, seed: sd + 13 + s.gx, head: st === 2 && big ? straw : null, headP: 0.3 });
        }
        for (const s of c.sites(18, 0.95, sd + 25, s => s.r < pk([0.45, 0.35, 0.2], st) && c.deep(s.x, s.y, 3))) {
          const nL = 5 + Math.floor(s.r2 * 3), a0 = s.r * 6.28;
          t.beginMark();
          for (let k = 0; k < nL; k++) { const a = a0 + k * 6.283 / nL, L = c.px(1.6 + hash2(s.x, k, sd) * 1.2); for (let r = 1; r <= L; r++) c.put(s.x + Math.round(Math.cos(a) * r), s.y + Math.round(Math.sin(a) * r * 0.7), clov, r === L ? 2 : 1, 0.25, 1); }
          c.put(s.x, s.y, clov, 1, 0.2, 1); t.endMark();
        }
        for (const s of c.sites(3.2, 0.95, sd + 27)) {
          const f = c.F(s.x, s.y, 3, sd + 28); if (f < 0.6 || s.r > (f - 0.6) * 3) continue;
          const i = t.i(s.x, s.y); if (t.lock[i]) continue;
          t.beginMark(); c.put(s.x, s.y, clov, 3, 0.35, 1); c.put(s.x + 1, s.y, clov, 2, 0.3, 1); c.put(s.x, s.y + 1, clov, 2, 0.3, 1); t.endMark();
          if (st > 0 && s.r2 < 0.12) { t.beginMark(); c.put(s.x, s.y - 1, fl[1], 3, 0.55, 1); c.put(s.x + 1, s.y - 1, fl[1], 2, 0.5, 1); t.endMark(); }
        }
        const fd = pk([0.004, 0.014, 0.03], st), FK = [0, 2, 3, 0];
        for (const s of c.sites(3, 0.95, sd + 30)) {
          const d = c.F(s.x, s.y, 3, sd + 31), p = (d - 0.56) / 0.44; if (p <= 0 || s.r > p * fd * 14) continue;
          const i = t.i(s.x, s.y); if (t.lock[i] || t.pal[i] === soil) continue;
          const k = fl[FK[Math.floor(hash2(s.x >> 5, s.y >> 5, sd + 32) * 4)]];
          t.beginMark(); c.put(s.x, s.y, damp, 1, 0.5, 1); c.put(s.x, s.y - 1, k, 3, 0.9, 1); if (hash2(s.x, s.y, sd + 33) < 0.5) c.put(s.x + 1, s.y - 2, k, 3, 1.0, 1); else c.put(s.x, s.y - 2, k, 3, 1.0, 1); t.endMark();
        }
        for (const s of c.sites(44, 0.9, sd + 40, s => s.r < pk([0.4, 0.25, 0.12], st) && c.deep(s.x, s.y, 3))) pebble(c, s.x, s.y, 2.5 + s.r2 * 1.5, 1.8, { pal: stone, h: 1.0 });
        return { ground: turf, fringe: turf };
      } },

    dirt: {
      note: 'Trodden red earth. A crumb-and-pit grain, faint pale crust plates, boot prints in pairs (hollows — the light draws the wall that faces the sun), pebbles as domes with no painted cap, scuffs. Lo carries straw and moss cushions; Hi is churned to clods.',
      paint(c, st, sd) {
        const t = c.t, earth = pal(t, G.dirt, 0.8, CL.SOIL), peb = pal(t, G.peb, 0.8, CL.ROCK), grey = pal(t, G.stone, 0.9, CL.ROCK), straw = pal(t, G.straw, 0.7, CL.LITTER), moss = pal(t, G.moss, 0.7, CL.VEG);
        c.fill(earth, 2, 0); grain(c, earth, 0.045, sd + 2, 0.18);
        for (const s of c.sites(26, 0.9, sd + 1, s => s.r < pk([0.35, 0.45, 0.25], st))) K.patch(c, s.x, s.y, 8 + Math.round(s.r2 * 10), 5 + Math.round(hash2(s.x, s.y, sd) * 6), { pal: earth, band: 3, ul: 3, dr: 3, hole: 0.14, tear: 1.3, rag: sd + 2, h: 0.12 });
        for (const s of c.sites(20, 0.9, sd + 10, s => s.r < pk([0.5, 0.4, 0.25], st) && c.deep(s.x, s.y, 5))) {
          print(c, s.x, s.y, 2, 3.2, { pal: earth, dh: -0.6 });
          if (s.r2 > 0.3) print(c, s.x + c.px(3), s.y + c.px(s.r2 > 0.65 ? 3 : -3), 2, 3.2, { pal: earth, dh: -0.6 });
        }
        for (const s of c.sites(pk([9, 8, 9], st), 0.9, sd + 20, s => s.r < 0.45 && c.deep(s.x, s.y, 2))) pebble(c, s.x, s.y, 1.5 + s.r2 * 1.5, 1.2 + s.r2, { pal: s.r2 < 0.6 ? peb : grey, band: s.r2 > 0.9 ? 3 : 2, h: 0.7 });
        for (const s of c.sites(15, 0.9, sd + 30, s => s.r < pk([0.3, 0.5, 0.6], st))) K.dash(c, s.x, s.y, 3 + Math.round(s.r2 * 4), earth, 1, sd + 31);
        if (st === 0) for (const s of c.sites(6, 0.9, sd + 40, s => s.r < 0.3)) { if (s.r2 < 0.5) { t.beginMark(); c.put(s.x, s.y, straw, 3, 0.5, 1); c.put(s.x + 1, s.y - 1, straw, 2, 0.6, 1); t.endMark(); } else if (s.r2 < 0.7) pebble(c, s.x, s.y, 2.5, 1.6, { pal: moss, h: 0.5 }); }
        if (st === 2) for (const s of c.sites(5.5, 0.9, sd + 50, s => s.r < 0.5)) pebble(c, s.x, s.y, 2 + s.r2 * 2, 1.6 + s.r2, { pal: earth, h: 1.1, lock: 0 });
        return { ground: earth, fringe: earth };
      } },

    path: {
      note: 'The track is its gravel: single stones and pairs across the whole surface, a few bigger ones pressed in, pale polished plates where feet pack it, scuffs, and shallow hollows that fill in the rain. Lo is after rain (dark damp patches, more hollows); Hi is bone dry with straw blown across.',
      paint(c, st, sd) {
        const t = c.t, earth = pal(t, G.path, 0.75, CL.SOIL), dk = pal(t, G.dirt, 0.8, CL.SOIL), grav = pal(t, G.gravel, 0.85, CL.ROCK), grav2 = pal(t, '#b0a38e', 0.8, CL.ROCK), straw = pal(t, G.straw, 0.7, CL.LITTER);
        c.fill(earth, 2, 0); grain(c, earth, 0.022, sd + 2, 0.15);
        for (const s of c.sites(22, 0.9, sd + 1, s => s.r < pk([0.3, 0.5, 0.6], st))) K.patch(c, s.x, s.y, 10 + Math.round(s.r2 * 12), 5 + Math.round(hash2(s.x, s.y, sd) * 5), { pal: earth, band: 3, ul: 3, dr: 3, hole: 0.12, tear: 1.1, rag: sd + 3, h: 0.1 });
        if (st === 0) for (const s of c.sites(26, 0.9, sd + 5, s => s.r < 0.5)) K.patch(c, s.x, s.y, 6 + Math.round(s.r2 * 8), 4 + Math.round(hash2(s.x, s.y, sd) * 3), { pal: dk, band: 2, ul: 2, dr: 2, hole: 0.08, rag: sd + 6, h: -0.25 });
        const gd = pk([0.026, 0.034, 0.04], st);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue; const i = t.i(x, y); if (t.lock[i]) continue;
          if (hash2(x, y, sd + 7) < gd * (0.5 + c.F(x, y, 4, sd + 8))) { const p = hash2(x, y, sd + 9) < 0.6 ? grav : grav2; t.beginMark(); c.put(x, y, p, 2, 0.35, 1); if (hash2(x, y, sd + 10) < 0.35) c.put(x + 1, y, p, 2, 0.3, 1); t.endMark(); }
        }
        for (const s of c.sites(10, 0.9, sd + 20, s => s.r < 0.3 && c.deep(s.x, s.y, 2))) pebble(c, s.x, s.y, 1.6 + s.r2 * 1.2, 1.3, { pal: grav, h: 0.6 });
        for (const s of c.sites(pk([16, 22, 30], st), 0.9, sd + 30, s => s.r < pk([0.6, 0.4, 0.3], st) && c.deep(s.x, s.y, 4))) print(c, s.x, s.y, 4 + s.r2 * 5, 2.5 + s.r2 * 2, { pal: st === 0 ? dk : earth, dh: -0.9 });
        for (const s of c.sites(16, 0.9, sd + 40, s => s.r < 0.4)) K.dash(c, s.x, s.y, 4 + Math.round(s.r2 * 5), earth, 1, sd + 41);
        if (st === 2) for (const s of c.sites(9, 0.9, sd + 50, s => s.r < 0.3)) { c.put(s.x, s.y, straw, 3, 0.4, 1); if (s.r2 < 0.5) c.put(s.x + 1, s.y, straw, 2, 0.4, 1); }
        return { ground: earth, fringe: earth };
      } },

    mud: {
      note: 'Wet ground. Puddles are hollows full of water — the relight gives them the sky, the wind and the rain; boot and hoof prints; straw litter. Lo is drying to a cracked skin whose plates curl (each plate a mark, so it takes its own tip and seam); Hi is churned.',
      paint(c, st, sd) {
        const t = c.t, mud = pal(t, G.mud, 0.85, CL.MUD), pool = pal(t, G.puddle, 0.7, CL.WATER), straw = pal(t, G.straw, 0.7, CL.LITTER), crust = pal(t, G.crust, 0.7, CL.SOIL);
        c.fill(mud, 2, 0); grain(c, mud, 0.035, sd + 4, 0.15);
        if (st === 0) {
          const cx = c.cx(9), cy = c.cy(9);
          for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
            if (!c.R(x, y)) continue;
            const w = worleyP((x + 0.5) / c.n, (y + 0.5) / c.m, cx, cy, sd + 1, 0.9);
            if (w.d2 - w.d1 < 0.08 && c.F(x, y, 6, sd + 2) > 0.44) t.set(x, y, mud, 0, -0.7);
            else if (hash2(w.id, 1, sd + 3) < 0.45 && w.d1 < 0.42) { t.set(x, y, crust, 2, 0.25 + (0.42 - w.d1) * 0.8); t.mark[t.i(x, y)] = (w.id % 60000) + 1; }
          }
        }
        for (const s of c.sites(pk([30, 24, 18], st), 0.9, sd + 10, s => s.r < pk([0.35, 0.55, 0.7], st) && c.deep(s.x, s.y, 8))) {
          const w = 5 + s.r2 * pk([5, 8, 11], st), h = 3 + hash2(s.x, s.y, sd) * w * 0.5, W = c.px(w), Hh = c.px(h), stn = E(W, Hh);
          t.beginMark();
          for (const p of stn.px) {
            const px = s.x + p[0] - stn.ox, py = s.y + p[1] - stn.oy; if (!c.R(px, py)) continue;
            const u = (p[0] + 0.5 - W / 2) / (W / 2), v = (p[1] + 0.5 - Hh / 2) / (Hh / 2), r = u * u + v * v;
            if (r > 0.62 && hash2(px >> 1, py >> 1, sd + 11) < (r - 0.62) * 1.6) continue;
            const i = t.i(px, py); t.pal[i] = r > 0.55 ? mud : pool; t.band[i] = r > 0.55 ? 1 : 2; t.h[i] = -1.0 * Math.sqrt(Math.max(0, 1 - r)) - 0.2; t.lock[i] = 1; t.mark[i] = t.mid;
          }
          t.endMark();
        }
        for (const s of c.sites(pk([14, 9, 11], st), 0.9, sd + 20, s => s.r < 0.55 && c.deep(s.x, s.y, 3))) print(c, s.x, s.y, 2 + s.r2 * 1.5, 2.6 + s.r2, { pal: mud, band: 2, dh: -0.45 });
        if (st === 2) for (const s of c.sites(6, 0.9, sd + 30, s => s.r < 0.5)) pebble(c, s.x, s.y, 2 + s.r2 * 2, 1.6 + s.r2, { pal: mud, h: 1.1, lock: 0 });
        for (const s of c.sites(12, 0.9, sd + 40, s => s.r < 0.3)) { if (t.lock[t.i(s.x, s.y)]) continue; c.put(s.x, s.y, straw, 3, 0.4, 1); if (s.r2 < 0.5) c.put(s.x + 1, s.y, straw, 2, 0.4, 1); }
        return { ground: mud, fringe: mud };
      } },

    sand: {
      note: 'Ochre sand with a grain at last: a band up and down on single texels, density on a slow field; heavy mineral winnowed into dark wisps; shell hash as curved fragments pearly at one end; a pebble now and then; gull tracks wandering in pairs of three-toed prints. Lo is damp with pale drying patches; Hi is dry, rippled by the wind in broken crescents, wrack bits blown up it.',
      paint(c, st, sd) {
        const t = c.t, sand = pal(t, st === 0 ? G.damp : G.sand, 0.6, CL.SAND), dry = pal(t, G.sand, 0.6, CL.SAND), shell = ramp(t, SHELL_R, CL.SHELL), min = pal(t, S.mineral, 0.8, CL.SAND), peb = pal(t, G.dark, 0.8, CL.ROCK), weed = pal(t, '#4b4a2c', 0.8, CL.WEED);
        c.fill(sand, 2, 0); grain(c, sand, pk([0.03, 0.036, 0.04], st), sd + 5, 0.15);
        if (st === 0) for (const s of c.sites(34, 0.9, sd + 3, s => s.r < 0.35)) K.patch(c, s.x, s.y, 8 + Math.round(s.r2 * 10), 4 + Math.round(hash2(s.x, s.y, sd) * 4), { pal: dry, band: 2, ul: 2, dr: 2, hole: 0.25, tear: 1.8, rag: sd + 4 });
        for (const s of c.sites(pk([18, 24, 40], st), 0.9, sd + 8, s => s.r < 0.7)) {
          const L = c.px(8 + s.r2 * 14), a = (c.F(s.x, s.y, 2, sd + 9) - 0.5) * 1.4;
          t.beginMark();
          for (let k = 0; k < L; k++) { if (hash2(s.x + k, s.y, sd + 10) > 0.55) continue; const x = s.x + Math.round(Math.cos(a) * k), y = s.y + Math.round(Math.sin(a) * k + Math.sin(k * 0.3 + s.r * 6) * 1.2); c.put(x, y, min, hash2(x, y, sd + 11) < 0.3 ? 1 : 2, -0.1); }
          t.endMark();
        }
        for (const s of c.sites(pk([9, 8, 7], st), 0.95, sd + 12, s => s.r < pk([0.18, 0.26, 0.3], st))) { const up = s.r2 < 0.5, w = 2 + (s.r2 > 0.7 ? 1 : 0); t.beginMark(); for (let k = 0; k < w; k++) c.put(s.x + k, s.y - (k === 1 && up ? 1 : 0), shell, k === 0 ? 4 : 3, 0.35, 1); t.endMark(); }
        for (const s of c.sites(26, 0.9, sd + 20, s => s.r < pk([0.3, 0.2, 0.15], st) && c.deep(s.x, s.y, 2))) pebble(c, s.x, s.y, 1.5 + s.r2, 1.3, { pal: peb, h: 0.6 });
        if (st < 2) for (const s of c.sites(64, 0.8, sd + 30, s => s.r < 0.55)) {
          let x = s.x, y = s.y, a = -1.57 + (s.r2 - 0.5) * 1.2;
          for (let k = 0; k < 9 + Math.floor(s.r * 8); k++) {
            a += (hash2(k, s.gx, sd + 31) - 0.5) * 0.5; x += Math.round(Math.cos(a) * 5); y += Math.round(Math.sin(a) * 5);
            const sx = k % 2 ? 2 : -2, px = x + Math.round(-Math.sin(a) * sx), py = y + Math.round(Math.cos(a) * sx);
            t.beginMark(); for (const [f, q] of GULL) c.put(px + Math.round(Math.cos(a) * f - Math.sin(a) * q), py + Math.round(Math.sin(a) * f + Math.cos(a) * q), sand, 1, -0.25); t.endMark();
          }
        }
        if (st === 2) {
          for (const s of c.sites(9, 0.9, sd + 40, s => s.r < 0.55)) { const len = c.px(3 + Math.round(s.r2 * 5)), amp = 1.2 * c.D; t.beginMark(); for (let k = 0; k < len; k++) { const a = Math.round(Math.sin((k + 0.5) / len * Math.PI) * amp), x = s.x + k - (len >> 1); c.put(x, s.y - a, sand, 3, 0.45); for (let q = 1; q <= c.D; q++) c.put(x, s.y - a + q, sand, 2, 0.2 - q * 0.25); } t.endMark(); }
          for (const s of c.sites(14, 0.9, sd + 50, s => s.r < 0.25)) { const L = c.px(1 + s.r2 * 2); t.beginMark(); for (let k = 0; k < L; k++) c.put(s.x + k, s.y, weed, k ? 1 : 2, 0.3, 1); t.endMark(); }
        }
        return { ground: sand, fringe: sand };
      } },

    ledge: {
      note: 'The red platform as benches at three levels, each level its own bed; laminations across each bench on its own strike; joints only where two benches of one level meet, whole or not at all. No riser is painted: its height is, and the light draws it for the hour. Pits that hold the rain, barnacle specks, weed in the joints and loose cobbles on the ladder.',
      paint(c, st, sd) {
        const t = c.t, rock = pal(t, G.ledge, 0.9, CL.ROCK), rock2 = pal(t, S.ledgeB, 0.9, CL.ROCK), rock3 = pal(t, S.ledgeC, 0.9, CL.ROCK), weed = pal(t, G.weed, 0.8, CL.WEED), barn = pal(t, S.barn, 0.8, CL.SHELL), cob = pal(t, G.cob, 0.9, CL.ROCK);
        benches(c, st, sd, [rock3, rock2, rock], { cells: [6, 7, 8], levels: [0.14, 0.74], step: 2.4, joint: [0.30, 0.42, 0.52], weed, weedP: [0, 0.25, 0.45] });
        if (st >= 1) for (const s of c.sites(18, 0.9, sd + 30, s => s.r < pk([0, 0.4, 0.6], st) && c.deep(s.x, s.y, 5))) print(c, s.x, s.y, 3 + s.r2 * 3, 2 + s.r2 * 1.5, { pal: t.pal[t.i(s.x, s.y)], hBase: t.h[t.i(s.x, s.y)], dh: -1.1 });
        if (st >= 1) for (const s of c.sites(2.6, 0.95, sd + 35, s => c.F(s.x, s.y, 6, sd + 36) > pk([1, 0.64, 0.58], st))) { const i = t.i(s.x, s.y); if (t.lock[i]) continue; c.put(s.x, s.y, barn, s.r < 0.3 ? 3 : 2, t.h[i] + 0.25, 1); }
        if (st === 2) for (const s of c.sites(12, 0.9, sd + 40, s => s.r < 0.3 && c.deep(s.x, s.y, 2))) pebble(c, s.x, s.y, 3, 2, { pal: cob, h: 0.9, hBase: t.h[t.i(s.x, s.y)] });
        return { ground: rock, fringe: rock };
      } },

    foreshore: {
      note: 'The flat between the berm and the ripple zone. Low sinuous ripples on a long pitch, bent by two fields, planed off in broad patches and broken along every crest — a field, not a panel. Heavy mineral in the troughs, shell hash on the flat; at Lo the troughs still hold a film of water.',
      paint(c, st, sd) {
        const t = c.t, fs = pal(t, S.foreshore, 0.7, CL.SAND), min = pal(t, S.mineral, 0.8, CL.SAND), shell = ramp(t, SHELL_R, CL.SHELL), wat = pal(t, '#3d6470', 0.8, CL.WATER);
        rippleField(c, sd, { pal: fs, pitch: pk([9, 8, 7], st), amp: 11, h: 1.1, clear: pk([0.44, 0.38, 0.32], st), brk: 0.42, brkCells: 22, minPal: min, min: 0.35, water: st === 0 ? wat : null, wet: 0.55 });
        grain(c, fs, 0.02, sd + 6, 0.1);
        for (const s of c.sites(10, 0.95, sd + 12, s => s.r < 0.22)) { t.beginMark(); c.put(s.x, s.y, shell, 4, 0.3, 1); c.put(s.x + 1, s.y, shell, 3, 0.3, 1); t.endMark(); }
        return { ground: fs, fringe: fs };
      } },

    ripple: {
      note: 'The low-water ripple zone: short sinuous crests on a tight pitch, stoss long and lee short, the crest drier and paler, heavy mineral and a film of water in the troughs (more of it at Lo, just uncovered). The one wrapping tile allowed a direction — a train, never corduroy.',
      paint(c, st, sd) {
        const t = c.t, rp = pal(t, S.ripple, 0.75, CL.SAND), min = pal(t, S.mineral, 0.8, CL.SAND), wat = pal(t, '#3d6470', 0.8, CL.WATER), shell = ramp(t, SHELL_R, CL.SHELL);
        rippleField(c, sd, { pal: rp, pitch: pk([6, 5.5, 5], st), amp: 9, h: 1.4, clear: pk([0.32, 0.27, 0.22], st), brk: 0.36, brkCells: 26, minPal: min, min: 0.45, water: wat, wet: pk([0.42, 0.56, 0.66], st) });
        for (const s of c.sites(12, 0.95, sd + 12, s => s.r < 0.2)) { t.beginMark(); c.put(s.x, s.y, shell, 4, 0.3, 1); c.put(s.x + 1, s.y - (s.r2 < 0.5 ? 1 : 0), shell, 3, 0.3, 1); t.endMark(); }
        return { ground: rp, fringe: rp };
      } },

    talus: {
      note: 'Scree as blocks: each Worley cell a block with a flat top and rounded shoulders, its own rock (red talus, the lower bed, grey shelf, dark till), fines in the gaps, lichen on the tops that have stayed put. Each block is a mark, so the light gives it a lit face, a seam and a shadow for the hour — no outline is painted.',
      paint(c, st, sd) {
        const t = c.t, B = [pal(t, S.talus, 0.9, CL.ROCK), pal(t, S.ledgeB, 0.9, CL.ROCK), pal(t, '#7a5242', 0.9, CL.ROCK), pal(t, '#6a625a', 0.9, CL.ROCK)], fines = pal(t, S.till, 0.8, CL.SOIL), lich = pal(t, S.lichen, 0.7, CL.ROCK);
        const cnt = pk([8, 10, 12], st), cx = c.cx(cnt), cy = c.cy(cnt);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue;
          const w = worleyP((x + 0.5) / c.n + (c.F(x, y, 6, sd + 1) - 0.5) * 0.03, (y + 0.5) / c.m + (c.F(x, y, 6, sd + 2) - 0.5) * 0.03, cx, cy, sd, 0.95), e = w.d2 - w.d1, i = t.i(x, y);
          if (e < 0.07) { t.set(x, y, fines, hash2(x, y, sd + 1) < 0.25 ? 3 : 2, -0.1); continue; }
          const r = hash2(w.id, 1, sd), p = B[r < 0.5 ? 0 : r < 0.78 ? 1 : r < 0.94 ? 2 : 3], top = clamp((e - 0.07) * 7, 0, 1), hh = (1.0 + 1.8 * hash2(w.id, 2, sd)) * Math.sqrt(top);
          t.set(x, y, p, 2, hh); t.mark[i] = (w.id % 60000) + 1;
          if (top > 0.6 && c.F(x, y, 9, sd + 3) > 0.62 && hash2(w.id, 3, sd) < 0.5) { t.set(x, y, lich, 3, hh + 0.05); t.mark[i] = (w.id % 60000) + 1; }
        }
        return { ground: fines, fringe: B[0] };
      } },

    musselbed: {
      note: 'Mussels you can count: dense clumps of shells three and four texels long in five orientations, each a mark with its own glint and dark edge, old shells pale among them, broken shell in the mud between clumps, a barnacle riding here and there, fucus bits at Hi.',
      paint(c, st, sd) {
        const t = c.t, mud = pal(t, S.bedMud, 0.8, CL.MUD), mus = pal(t, S.mussel, 0.95, CL.SHELL), old = pal(t, S.musselOld, 0.85, CL.SHELL), sh = ramp(t, SHELL_R, CL.SHELL), barn = pal(t, S.barn, 0.8, CL.SHELL), fuc = pal(t, S.fucus, 0.8, CL.WEED);
        c.fill(mud, 2, 0); grain(c, mud, 0.03, sd + 1, 0.12);
        const cover = pk([0.40, 0.55, 0.72], st);
        for (const s of c.sites(1.6, 0.95, sd + 3)) {
          const f = c.F(s.x, s.y, 4, sd + 4) * 0.75 + c.F(s.x, s.y, 12, sd + 5) * 0.25; if (f < 1 - cover) continue;
          t.beginMark(); stamp(t, MUS[Math.floor(s.r * MUS.length)], s.x, s.y, { pal: s.r2 < 0.12 ? old : mus, band: s.r2 > 0.8 ? 1 : 2, seam: 0, tip: 0, h: 0.7, hBase: 0.3 * f, clip: c.R }); t.endMark();
          if (s.r2 > 0.96) c.put(s.x, s.y - 1, barn, 3, 1.1, 1);
        }
        for (const s of c.sites(7, 0.9, sd + 8, s => s.r < 0.3)) { if (t.pal[t.i(s.x, s.y)] !== mud) continue; t.beginMark(); c.put(s.x, s.y, sh, 3, 0.25, 1); if (s.r2 < 0.5) c.put(s.x + 1, s.y, sh, 2, 0.2, 1); t.endMark(); }
        if (st === 2) for (const s of c.sites(10, 0.9, sd + 9, s => s.r < 0.35)) { const L = c.px(2 + s.r2 * 3); t.beginMark(); for (let k = 0; k < L; k++) c.put(s.x + (k % 3 === 2 ? 1 : 0), s.y + k, fuc, 2, 0.9, 1); t.endMark(); }
        return { ground: mud, fringe: mud };
      } },

    oysterreef: {
      note: 'Oysters as clusters of irregular elongated shells, grey-olive with fresh pale ones, each a mark; cultch (broken shell) between the clusters; the mud between them as the ground.',
      paint(c, st, sd) {
        const t = c.t, mud = pal(t, S.bedMud, 0.8, CL.MUD), oy = pal(t, S.oyster, 0.9, CL.SHELL), fresh = pal(t, S.oysterFresh, 0.8, CL.SHELL), cul = ramp(t, SHELL_R, CL.SHELL);
        c.fill(mud, 2, 0); grain(c, mud, 0.03, sd + 1, 0.12);
        const cover = pk([0.30, 0.40, 0.52], st);
        for (const s of c.sites(2.2, 0.95, sd + 3)) {
          const f = c.F(s.x, s.y, 2.5, sd + 4) * 0.85 + c.F(s.x, s.y, 10, sd + 5) * 0.15; if (f < 1 - cover) continue;
          t.beginMark(); stamp(t, OYS[Math.floor(s.r * OYS.length)], s.x, s.y, { pal: s.r2 < 0.2 ? fresh : oy, band: 2, seam: 0, tip: 0, h: 0.8, hBase: 0.4 * f, clip: c.R }); t.endMark();
        }
        for (const s of c.sites(3.5, 0.9, sd + 8, s => s.r < 0.45)) { if (t.pal[t.i(s.x, s.y)] !== mud) continue; c.put(s.x, s.y, cul, s.r2 < 0.4 ? 3 : 2, 0.2, 1); }
        return { ground: mud, fringe: mud };
      } },

    rockweed: {
      note: 'Drained Ascophyllum and Fucus lying down the rock face toward the water: fronds as strands that wander as they fall, a texel or two wide, knotted wrack with a bladder every few texels standing proud, the rock showing between with barnacles on it. Each frond is a mark — the wet sheen and the glint come from the relight.',
      paint(c, st, sd) {
        const t = c.t, rock = pal(t, S.rock, 0.9, CL.ROCK), rock2 = pal(t, S.ledgeC, 0.9, CL.ROCK), asco = pal(t, '#5e5122', 0.85, CL.WEED), fuc = pal(t, '#463d20', 0.85, CL.WEED), barn = pal(t, S.barn, 0.8, CL.SHELL);
        c.fill(rock, 2, 0.2);
        const cx = c.cx(5), cy = c.cy(5);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue; const w = worleyP((x + 0.5) / c.n, (y + 0.5) / c.m, cx, cy, sd + 1, 0.9);
          if (w.d2 - w.d1 < 0.05) t.set(x, y, rock2, 1, -0.2); else if (hash2(w.id, 1, sd) < 0.3) t.set(x, y, rock2, 2, 0.2);
        }
        if (st < 2) for (const s of c.sites(2.2, 0.95, sd + 20, s => s.r < 0.35)) { if (t.pal[t.i(s.x, s.y)] !== rock) continue; c.put(s.x, s.y, barn, 3, 0.45, 1); }
        const cover = pk([0.45, 0.62, 0.8], st);
        for (const s of c.sites(4.2, 0.95, sd + 10, s => c.F(s.x, s.y, 2.5, sd + 11) > 1 - cover)) {
          const isF = s.r2 < 0.35, p = isF ? fuc : asco, L = c.px(8 + s.r * 10), wp = isF ? 4 : 3;
          let x = s.x, dx = (s.r - 0.5) * 1.6;
          t.beginMark();
          for (let k = 0; k < L; k++) {
            dx = clamp(dx + (hash2(s.x, k, sd + 12) - 0.5) * 0.5, -1.4, 1.4); x += dx * 0.7;
            const X = Math.round(x), y = s.y + k, hh = 0.8 + 0.25 * Math.sin(k * 0.5 + s.r * 5), wq = wp - (k > L * 0.75 ? 1 : 0) - (k > L * 0.9 ? 1 : 0);
            for (let q = 0; q < wq; q++) c.put(X + q, y, p, q === wq - 1 && hash2(X, y, sd + 13) < 0.5 ? 1 : 2, hh, 1);
            if (!isF && k % 5 === 2) c.put(X + wq, y, p, 3, hh + 0.5, 1);
          }
          t.endMark();
        }
        return { ground: rock, fringe: rock };
      } },

    marsh: {
      note: 'High marsh: a dense sod of Spartina tussocks in two greens with thatch among them, thatch combed by the tide lying across the sod, and salt pans — flat pale hollows that hold water after rain.',
      paint(c, st, sd) {
        const t = c.t, sod = pal(t, S.spartina, 0.8, CL.VEG), sod2 = pal(t, '#667336', 0.8, CL.VEG), thatch = pal(t, S.thatch, 0.7, CL.LITTER), pan = pal(t, S.pan, 0.6, CL.MUD);
        c.fill(sod, 1, 0);
        for (const s of c.sites(4, 0.9, sd + 1, s => s.r < pk([0.35, 0.45, 0.55], st))) { const L = c.px(2 + s.r2 * 4); t.beginMark(); for (let k = 0; k < L; k++) c.put(s.x + k, s.y + (k > L / 2 ? 1 : 0), thatch, hash2(s.x, k, sd) < 0.3 ? 3 : 2, 0.2); t.endMark(); }
        for (const s of c.sites(34, 0.9, sd + 5, s => s.r < pk([0.2, 0.35, 0.5], st) && c.deep(s.x, s.y, 10))) K.patch(c, s.x, s.y, 8 + s.r2 * 10, 4 + s.r2 * 5, { pal: pan, band: 2, ul: 2, dr: 2, hole: 0.05, tear: 1.3, rag: sd + 6, h: -0.9 });
        for (const s of c.sites(pk([4.5, 3.8, 3.2], st), 0.95, sd + 10)) { if (t.pal[t.i(s.x, s.y)] === pan) continue; tuft(c, s.x, s.y, { w: 2 + s.r2 * 2.5, len: 1.6 + s.r * 1.6, pal: s.r2 > 0.6 ? sod2 : sod, pal2: thatch, mixP: 0.12, seed: sd + s.gx * 3 + s.gy, spread: 0.7, lean: 0.25 }); }
        return { ground: sod, fringe: sod };
      } },

    sedge: {
      note: 'Tussock sedge: hummocks with a fountain of arching blades on each, straw in the crowns at Hi, dark peat between with water standing in it on the ladder. Pass seven drew lily pads.',
      paint(c, st, sd) {
        const t = c.t, peat = pal(t, S.peat, 0.8, CL.MUD), sedge = pal(t, S.sedge, 0.8, CL.VEG), base = pal(t, S.rush, 0.8, CL.VEG), straw = pal(t, S.thatch, 0.7, CL.LITTER), pool = pal(t, '#56666a', 0.7, CL.WATER);
        c.fill(peat, 2, -0.4);
        if (st > 0) for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) { if (!c.R(x, y)) continue; if (c.F(x, y, 3, sd + 1) < pk([0, 0.3, 0.38], st)) t.set(x, y, pool, 2, -0.7); }
        for (const s of c.sites(3, 0.95, sd + 2, s => s.r < 0.4)) { const i = t.i(s.x, s.y); if (t.pal[i] !== peat) continue; c.put(s.x, s.y, sedge, 2, 0.1); c.put(s.x, s.y - 1, sedge, 3, 0.3); }
        for (const s of c.sites(pk([12, 10, 9], st), 0.95, sd + 10)) {
          pebble(c, s.x, s.y, 4 + s.r2 * 3, 3 + s.r2 * 2, { pal: base, band: 1, h: 1.6, lock: 0 });
          tuft(c, s.x, s.y - 1, { w: 5 + s.r2 * 3, len: 3 + s.r * 2, pal: sedge, pal2: straw, mixP: st === 2 ? 0.35 : 0.15, spread: 0.9, seed: sd + s.gx * 5 + s.gy });
        }
        return { ground: peat, fringe: sedge };
      } },

    marram: {
      note: 'The dune: marram crowns in drifts, each on the little drift of sand it has caught, every blade combed the one way the wind builds the dune; green and dry blades mixed; the sand between with a grain and the wind\'s lineation; thatch at Hi.',
      paint(c, st, sd) {
        const t = c.t, sand = pal(t, S.dune, 0.6, CL.SAND), green = pal(t, S.marram, 0.8, CL.VEG), dry = pal(t, S.marramDry, 0.7, CL.LITTER), thatch = pal(t, S.thatch, 0.7, CL.LITTER);
        c.fill(sand, 2, 0); grain(c, sand, 0.03, sd + 2, 0.15);
        for (const s of c.sites(12, 0.9, sd + 3, s => s.r < 0.5)) K.dash(c, s.x, s.y, 5 + Math.round(s.r2 * 8), sand, 3, sd + 4);
        for (const s of c.sites(pk([11, 7.5, 6], st), 0.9, sd + 10)) {
          const d = c.F(s.x, s.y, 3, sd + 11); if (s.r > d * 1.5 - 0.2) continue;
          pebble(c, s.x, s.y + 1, 3 + s.r2 * 3, 1.6 + s.r2, { pal: sand, h: 1.0, lock: 0 });
          tuft(c, s.x, s.y, { w: 3 + s.r2 * 3, len: 3 + s.r * 2.5, pal: green, pal2: dry, mixP: st === 2 ? 0.4 : 0.22, lean: 0.45, spread: 0.5, seed: sd + s.gx * 7 + s.gy });
        }
        if (st === 2) for (const s of c.sites(9, 0.9, sd + 5, s => s.r < 0.55)) K.dash(c, s.x, s.y, 4 + Math.round(s.r2 * 5), thatch, 2, sd + 6);
        return { ground: sand, fringe: sand };
      } },

    silt: {
      note: 'Soft grey silt with a sheen (it is mud to the relight: glossy in rain, never dusty), a faint grain, lugworm casts — a coil and the pit of its head shaft beside it — a drying crust at Lo and shell hash at Hi. Pass seven painted grey mould.',
      paint(c, st, sd) {
        const t = c.t, silt = pal(t, S.silt, 0.7, CL.MUD), crust = pal(t, S.siltCrust, 0.6, CL.MUD), anox = pal(t, S.anox, 0.8, CL.MUD), sh = ramp(t, SHELL_R, CL.SHELL);
        c.fill(silt, 2, 0); grain(c, silt, 0.025, sd + 1, 0.1);
        if (st === 0) for (const s of c.sites(30, 0.9, sd + 2, s => s.r < 0.4)) K.patch(c, s.x, s.y, 8 + s.r2 * 10, 5 + s.r2 * 4, { pal: crust, band: 2, ul: 2, dr: 2, hole: 0.15, tear: 1.6, rag: sd + 3, h: 0.2 });
        for (const s of c.sites(pk([16, 12, 10], st), 0.9, sd + 5, s => s.r < 0.7 && c.deep(s.x, s.y, 3))) {
          t.beginMark(); for (const [u, v] of COIL[Math.floor(s.r2 * COIL.length)]) c.put(s.x + u, s.y + v, silt, 3, 0.55, 1); t.endMark();
          print(c, s.x + c.px(2), s.y - c.px(1), 1, 1, { pal: anox, band: 0, dh: -0.5 });
        }
        if (st === 2) for (const s of c.sites(9, 0.9, sd + 8, s => s.r < 0.3)) c.put(s.x, s.y, sh, 3, 0.25, 1);
        return { ground: silt, fringe: silt };
      } },

    shelf: {
      note: 'The grey platform as laminated benches in three grey beds, barnacle crusts in patches, periwinkles as small dark domes, weed in the joints on the ladder.',
      paint(c, st, sd) {
        const t = c.t, a = pal(t, S.shelfA, 0.85, CL.ROCK), b = pal(t, S.shelfB, 0.85, CL.ROCK), cc = pal(t, S.shelfC, 0.85, CL.ROCK), barn = pal(t, S.barn, 0.8, CL.SHELL), wink = pal(t, '#3e3834', 0.85, CL.SHELL), weed = pal(t, S.fucus, 0.8, CL.WEED);
        benches(c, st, sd, [cc, b, a], { cells: [5, 6, 7], levels: [0.2, 0.7], step: 1.8, joint: [0.35, 0.45, 0.55], weed, weedP: [0.1, 0.3, 0.5] });
        for (const s of c.sites(2.2, 0.95, sd + 20, s => c.F(s.x, s.y, 5, sd + 21) > pk([0.66, 0.58, 0.54], st))) { const i = t.i(s.x, s.y); c.put(s.x, s.y, barn, s.r < 0.3 ? 3 : 2, t.h[i] + 0.25, 1); }
        for (const s of c.sites(7, 0.9, sd + 25, s => s.r < pk([0.3, 0.45, 0.55], st))) pebble(c, s.x, s.y, 1.2, 1.2, { pal: wink, h: 0.6, hBase: t.h[t.i(s.x, s.y)] });
        return { ground: b, fringe: a };
      } },

    irishmoss: {
      note: 'A turf of dark red-purple cushions over cobbles: small bushy blobs, each a mark, in two reds, bleached olive-yellow on the crowns that dry at Lo; cobbles showing through the gaps.',
      paint(c, st, sd) {
        const t = c.t, rock = pal(t, S.cobble, 0.9, CL.ROCK), ch = pal(t, '#4e2430', 0.85, CL.WEED), ch2 = pal(t, '#5c2a3e', 0.85, CL.WEED), sun = pal(t, S.chondrusSun, 0.8, CL.WEED);
        c.fill(rock, 1, 0);
        for (const s of c.sites(7, 0.9, sd + 1)) pebble(c, s.x, s.y, 3 + s.r2 * 2, 2 + s.r2, { pal: rock, h: 1.2, lock: 0 });
        const cover = pk([0.5, 0.6, 0.72], st);
        for (const s of c.sites(1.6, 0.95, sd + 3)) {
          const f = c.F(s.x, s.y, 3, sd + 4) * 0.8 + c.F(s.x, s.y, 9, sd + 5) * 0.2; if (f < 1 - cover) continue;
          const p = st === 0 && f > 0.72 && s.r2 < 0.6 ? sun : s.r2 < 0.5 ? ch : ch2;
          t.beginMark(); stamp(t, BLOB[Math.floor(s.r * BLOB.length)], s.x, s.y, { pal: p, band: 2, seam: 0, tip: 0, h: 0.8, hBase: 0.6 + 0.4 * f, clip: c.R }); t.endMark();
        }
        return { ground: rock, fringe: ch };
      } },
  };
  const CHANGED = Object.keys(NEW), KEEP = Object.keys(MATS).filter(k => !NEW[k]);
  for (const k of CHANGED) { const M = MATS[k]; if (!M) continue; MATS[k] = Object.assign({}, M, { paint: NEW[k].paint, note: NEW[k].note, paint7: M.paint7 || M.paint, note7: M.note7 || M.note, live: 1 }); }
  const NOTES = {}; for (const k of Object.keys(MATS)) NOTES[k] = { now: MATS[k].note, before: MATS[k].note7 || MATS[k].note, changed: !!NEW[k] };

  /* build one tile; {pass: 7} paints it the way pass seven did (for the A/B) */
  function build(key, step, o) {
    o = o || {}; const M = MATS[key]; if (!M) throw new Error('PxKit8: no material ' + key);
    const D = K.DETAIL, N = o.N || 128 * D, t = new Tile(N, N), c = makeCtx(t, null, { detail: D });
    const fn = o.pass === 7 && M.paint7 ? M.paint7 : M.paint, pals = fn(c, step | 0, o.seed || M.seed);
    return { tile: t, relief: M.relief * D, pals, key, step, pass: o.pass === 7 ? 7 : 8 };
  }
  /* paint materials into one tile by region, then the kit's own edge painter between them */
  function floor(W, Hh, defs, sd) {
    const t = new Tile(W, Hh), reg = new Uint8Array(W * Hh).fill(255), mats = [];
    defs.forEach((d, k) => { mats.push(K.paintRegion(t, d.key, d.step | 0, d.R, d.seed, K.DETAIL)); for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) if (d.R(x, y)) reg[y * W + x] = k; });
    K.edges(makeCtx(t, null, { detail: K.DETAIL }), reg, mats, sd || 11);
    return { tile: t, reg, mats };
  }

  // ---- the coast: one transect with its elevation ---------------------------------------------------------------
  /* North is up the screen and so is the sea. Metres above chart datum; the tide runs 0 – 4 m over it
     (TIDE_M, the shore plant rig's harbour range), mean high water at 3.55 leaves the wrack line. */
  const TIDE_M = 4.0, MHW = 3.55;
  function coast(o) {
    o = o || {};
    const W = o.w || 520, Hh = o.h || 450, sd = o.seed || 7, N = W * Hh;
    const wob = (v, s, cells, span) => (fbmP(v / span, 0.5, cells, 1, sd + s, 3) - 0.5) * 2;
    const b1 = x => 40 + 15 * wob(x, 1, 5, W), b2 = x => 124 + 20 * wob(x, 2, 4, W), b3 = x => 182 + 12 * wob(x, 3, 5, W), b4 = x => b3(x) + 16 + 6 * wob(x, 4, 6, W);
    const b5 = x => b4(x) + 60 + 16 * wob(x, 5, 4, W), b6 = x => b5(x) + 52 + 18 * wob(x, 6, 4, W);
    const ledgeQ = (x, y) => { const u = (x - 128) / 100, v = (y - 90) / 42; return u * u + v * v - 1 - 0.3 * wob(x + y * 1.7, 9, 5, W); };
    const marshQ = (x, y) => { const u = (x - 488) / 128, v = (y - 236) / 84; return u * u + v * v - 1 - 0.75 * wob(x * 0.7 + y * 1.3, 11, 9, W + Hh) - 0.35 * wob(x * 1.9 - y, 21, 17, W + Hh); };
    const marshX = y => 378 + 16 * wob(y, 11, 3, Hh), creekX = y => 462 + 16 * Math.sin(y / 27) + 6 * wob(y, 12, 4, Hh), creekHalf = y => 5 + 2 * (y - 150) / 180;
    const pathPts = [[88, Hh + 4], [104, 410], [150, 372], [196, 340], [226, 300], [248, 262], [262, 226]];
    const segD = (x, y) => { let d = 1e9; for (let k = 0; k < pathPts.length - 1; k++) { const [ax, ay] = pathPts[k], [bx, by] = pathPts[k + 1], dx = bx - ax, dy = by - ay, tt = clamp(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy), 0, 1); d = Math.min(d, Math.hypot(x - ax - dx * tt, y - ay - dy * tt)); } return d; };
    const Z = ['eelgrass', 'ripple', 'irishmoss', 'rockweed', 'ledge', 'foreshore', 'shingle', 'sand', 'marram', 'grass', 'path', 'dirt', 'marsh', 'silt', 'musselbed', 'sedge'];
    const zi = {}; Z.forEach((k, i) => zi[k] = i);
    const zone = new Uint8Array(N), elev = new Float32Array(N), fetch = new Float32Array(N).fill(1);
    const lerpP = (y, pts) => { for (let k = 0; k < pts.length - 1; k++) if (y <= pts[k + 1][0]) { const f = (y - pts[k][0]) / Math.max(1e-3, pts[k + 1][0] - pts[k][0]); return pts[k][1] + (pts[k + 1][1] - pts[k][1]) * clamp(f, 0, 1); } return pts[pts.length - 1][1]; };
    for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x, B1 = b1(x), B2 = b2(x), B3 = b3(x), B4 = b4(x), B5 = b5(x), B6 = b6(x);
      let e = lerpP(y, [[0, -1.6], [B1, -0.25], [B2, 1.0], [B3, 2.55], [B4, 3.0], [B5, 4.2], [B6, 5.4], [Hh, 6.2]]), z;
      z = y < B1 ? 'eelgrass' : y < B2 ? 'ripple' : y < B3 ? 'foreshore' : y < B4 ? 'shingle' : y < B5 ? 'sand' : y < B6 ? 'marram' : 'grass';
      const lq = ledgeQ(x, y);
      if (lq < 0 && y > B1 - 14 && y < B2 + 12) { e += 0.7 + Math.min(0.6, -lq * 0.9); z = y < 70 + 6 * wob(x, 13, 4, W) ? 'irishmoss' : y < 96 + 8 * wob(x, 14, 4, W) ? 'rockweed' : 'ledge'; }
      if (marshQ(x, y) < 0 && y > 150) {
        e = Math.max(e, 3.45 + 0.4 * clamp((y - 150) / 120, 0, 1) + 0.12 * wob(x, 15, 6, W)); z = 'marsh'; fetch[i] = 0.2;
        const cx = creekX(y), hw = creekHalf(y);
        if (Math.abs(x - cx) < hw) { e = 0.55 + 1.9 * clamp((y - 150) / 190, 0, 1) - 0.25 * (1 - Math.abs(x - cx) / hw); z = 'silt'; }
      }
      if (x > 430 && y > 106 && y < 152 + 6 * wob(x, 16, 4, W) && y > B2 - 10) { const d = Math.hypot((x - 470) / 50, (y - 132) / 22); if (d < 1 + 0.25 * wob(x + y, 17, 5, W)) { z = 'musselbed'; e = Math.min(e, 0.9 + 0.3 * d); } }
      if (Math.hypot((x - 468) / 62, (y - 352) / 26) < 1 + 0.3 * wob(x * 1.3 + y, 18, 6, W + Hh) && y > B6 - 20) z = 'sedge';
      if (y > 240 && segD(x, y) < 6.5 + 1.8 * wob(x + y, 19, 6, W + Hh)) { z = y < B5 - 6 ? 'sand' : 'path'; if (z === 'path') e -= 0.05; }
      if (Math.hypot((x - 72) / 1.35, y - 430) < 22 + 12 * wob(x * 1.7 + y, 20, 9, W + Hh)) z = 'dirt';
      zone[i] = zi[z]; elev[i] = e;
    }
    const STEP = o.steps || { eelgrass: 1, ripple: 0, irishmoss: 1, rockweed: 1, ledge: 1, foreshore: 1, shingle: 1, sand: 1, marram: 1, grass: 1, path: 1, dirt: 1, marsh: 1, silt: 1, musselbed: 1, sedge: 1 };
    const inb = (x, y) => x >= 0 && y >= 0 && x < W && y < Hh;
    const used = Z.filter((k, i) => { for (let j = 0; j < N; j += 7) if (zone[j] === i) return true; return false; });
    const defs = used.map(k => ({ key: k, step: STEP[k] == null ? 1 : STEP[k], seed: (MATS[k].seed || 1) + sd * 13, R: (x, y) => inb(x, y) && zone[y * W + x] === zi[k] }));
    const fl = floor(W, Hh, defs, sd + 5), t = fl.tile;
    /* the wrack line: the last high water's strand, weed and straw and shell along one contour */
    const wc = makeCtx(t, (x, y) => inb(x, y) && Math.abs(elev[y * W + x] - MHW) < 0.03 + 0.02 * hash2(x >> 3, 1, sd) && (zone[y * W + x] === zi.sand || zone[y * W + x] === zi.shingle), { detail: K.DETAIL });
    K.wrack(wc, sd + 40);
    for (let i = 0; i < N; i++) elev[i] += t.h[i] * 0.01;
    return { tile: t, reg: fl.reg, mats: fl.mats, zone, zones: Z, zi, elev, fetch, w: W, h: Hh, TIDE_M, MHW, relief: 1.2 * K.DETAIL,
      far: (y) => clamp(1 - y / Hh, 0, 1), path: pathPts, creekX, marshQ, ledgeQ, bounds: { b1, b2, b3, b4, b5, b6 } };
  }

  root.PxKit8 = { MATS, CHANGED, KEEP, NOTES, SHELL_R, TIDE_M, MHW, pal, ramp, pebble, print, tuft, grain, benches, rippleField, build, floor, coast };
})(typeof globalThis !== 'undefined' ? globalThis : window);
