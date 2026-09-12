/* Hidden Harbours — PIXEL KIT, PASS FIVE: the shore comes across.

   The terrain material kit (v3, 20 materials) and the pixel kit (pass four, 7) were two kits. The
   pixel kit had the language — one flat body, lumps, density never value, authored edges — and the
   old kit had the SUBJECT: the intertidal. Everything below high water was missing, and five of the
   seven had come across thinner than they went in. This pass merges them.

   WHAT THE OLD KIT HAD THAT PASS FOUR DROPPED (re-baked here, same filenames):
     · Ledge was one red palette at three bands — one bed with dents in it. The old kit stacked THREE
       beds, each its own ramp: strip a bench and the rock underneath is a different colour, not a
       darker one. Restored, plus barnacle on the longest-dry crowns and weed in the joints.
     · Sand had shell grit but no shell HASH (a fragment is 2–3 texels, curved) and no heavy mineral.
       The garnet-and-magnetite dark winnowed into the lows is why a PEI flat reads striped and not
       orange, and it was the single most recognisable thing in the old sand.
     · Shingle had no barnacle high in the berm and no wet-dark stones at the toe — a beach that is
       all one wetness has no tide in it.
     · Grass lost the straw drift and the moss; Dirt lost its pebble lag.

   THE FOURTEEN THAT WERE NEVER BUILT — the whole tide, from the dune down:
     marram · foreshore · ripple · silt · shelf · talus · bank · marsh · sedge · rockweed ·
     musselbed · oysterreef · eelgrass · irishmoss

   The old kit's hard-won rules carry over verbatim, because they are about pictures, not noise:
   a shell FILLS its cell (a small ellipse in the middle of a cell is spilled rice); cover is a
   per-object decision, never an alpha (a gated wash of shell colour over mud is mud); a canopy
   needs gaps; talus tessellates and shingle scatters; a blade leans the way its mat runs; never
   ring a feature you mean to fill; a ripple field has to clear in places; anisotropic cell counts,
   never a sheared lattice.

   Registers into the pass-three tables — PxKit.MATS · RANK · FRINGE — so build, paintRegion, edges,
   strip, corner and the whole bake path work on the new materials with no changes.

     PxKit2.ORDER    the fourteen, in ladder order
     PxKit2.REDONE   the five re-baked with their detail back
     PxKit2.PAIRS    eight new edge pairs (the dune, the strand, the flat, the weed line,
                     the cliff toe, the bed margin, the creek)                                  */
(function (root) {
  'use strict';
  const P = root.PxLang, K = root.PxKit;
  if (!P || !K) throw new Error('Art/pixelLanguage.js and Art/pxKit.js must be loaded first');
  const { hash2, fbmP, worleyP, clamp } = P;
  const { lump, patch, hollow, shade, dash, tussock, MATS, GROUND, RANK, FRINGE } = K;
  const pk = (a, st) => a[clamp(st, 0, 2)];

  /* the shore palette — the old kit's ramps read at their mid, kept on the value ladder */
  const SHORE = {
    ripple: '#8a6448', mineral: '#3a2b24', foreshore: '#a87b5d', runnel: '#7d543f',
    silt: '#5c503f', siltCrust: '#9d9382', anox: '#242219',
    shelfA: '#7a7264', shelfB: '#6e6558', shelfC: '#61594e',
    ledgeA: '#a86444', ledgeB: '#8a4f36', ledgeC: '#6d3f2c',
    talus: '#6d4433', till: '#5e3b2a', lichen: '#868a72', fucus: '#534926',
    marram: '#6c8558', marramDry: '#a89573', dune: '#bd9877',
    spartina: '#5a6633', thatch: '#736748', pan: '#9b9884',
    sedge: '#546333', peat: '#30251a', rush: '#3d5029', moss: '#5e733f',
    bank: '#5e3b2a', bankFresh: '#8d5c44',
    asco: '#6f5f26', ulva: '#4e7a2c', barn: '#98928a', rock: '#6d4433',
    mussel: '#363b49', musselOld: '#5d472a', bedMud: '#342d22', cultch: '#a49c8b',
    oyster: '#746e45', oysterFresh: '#9d9680',
    zostera: '#466228', zosOld: '#675833', siltSand: '#9a7659',
    chondrus: '#5a2a30', chondrusSun: '#6a5f2c', cobble: '#8a837c',
    shell: '#c5bca7',
  };

  // ---- helpers the shore needs -----------------------------------------------------------------------
  /* a cell map on a periodic Worley lattice. `aniso` stretches the cells by using MORE rows, never by
     shearing the lattice. rim(x,y) gives the lump's two packing marks: -1 where the right or lower
     neighbour is another cell (the seam, a band down), +1 where the left or upper one is (the lit lip). */
  function cellMap(c, count, sd, jit, aniso) {
    const n = c.n, m = c.m;
    const cx = c.cx(count), cy = c.cy(count * (aniso || 1));
    const id = new Int32Array(n * m), d1 = new Float32Array(n * m), d2 = new Float32Array(n * m);
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const w = worleyP((x + .5) / n, (y + .5) / m, cx, cy, sd, jit == null ? .9 : jit), i = y * n + x;
      id[i] = w.id; d1[i] = w.d1; d2[i] = w.d2;
    }
    const at = (x, y) => id[(((y % m) + m) % m) * n + (((x % n) + n) % n)];
    return {
      id, d1, d2, at,
      rim(x, y) { const me = at(x, y); if (at(x + 1, y) !== me || at(x, y + 1) !== me) return -1; if (at(x - 1, y) !== me || at(x, y - 1) !== me) return 1; return 0; },
      r(x, y, k) { return hash2(at(x, y), k || 0, sd + 3); },
      d(x, y) { return d1[(((y % m) + m) % m) * n + (((x % n) + n) % n)]; },
    };
  }
  /* one blade: a texel wide, leaning `lean` per row, tip a band up, base a band down. A blade stroke
     has to lean the way its mat runs — upright strokes over a mat that runs across them is a lawn.
     len is authored texels; at a finer detail the blade is longer in texels and no wider, which is
     what a 1 px texel is for. */
  function blade(c, x, y, len, lean, pal, tip, sd) {
    const L = c.px(len);
    for (let r = 0; r < L; r++) {
      const px = x + Math.round(lean * r / c.D + (hash2(x, y + r, sd) - .5) * .9), py = y - r;
      c.put(px, py, pal, r === L - 1 ? tip : r > L * .5 ? 3 : 2, .3 + r * .35 / c.D, 1);
    }
  }
  /* a fan of blades from one crown */
  function fan(c, x, y, o) {
    const nb = c.px(o.n), sd = o.sd || 0, D = c.D;
    c.t.beginMark();
    for (let k = 0; k < nb; k++) {
      const bx = x + k - (nb >> 1), ln = Math.max(2, o.len - Math.round(hash2(bx, y, sd + k) * (o.jit == null ? 2 : o.jit)));
      blade(c, bx, y, ln, o.lean + (hash2(bx, y, sd + 3) - .5) * (o.spread == null ? .4 : o.spread), o.pal, o.tip == null ? 4 : o.tip, sd + k * 7);
    }
    c.t.endMark();
    if (o.shade != null) for (let k = -D; k <= D; k++) shade(c, x + k, y + D, { pal: o.shade, band: 1 });
  }
  /* a RIPPLE TRAIN. A ripple field is the one place a wrapping tile may carry a direction, because a
     flat is directional and takes no chunk offset — but it has to be a train and not corduroy, so:
     crests sit on a periodic pitch, each wanders on a field periodic in x (which is what makes it
     tile), and each is BROKEN where a slow field says the train has been planed off. The gaps are
     what make it a ripple field instead of a ribbed panel. Profile across one wavelength: stoss lit,
     crest brightest, lee dark, heavy mineral winnowed into the trough. */
  function train(c, o) {
    const D = c.D, rows = Math.max(2, Math.round(c.m / (o.pitch * D))), cells = Math.max(2, c.cx(3));
    const prof = [[-3, 2, .2], [-2, 3, .45], [-1, 4, .6], [0, 3, .4], [1, 1, -.3], [2, 1, -.25]];
    for (let k = 0; k < rows; k++) {
      const y0 = k * c.m / rows;
      for (let x = 0; x < c.n; x++) {
        const yy = Math.round(y0 + (fbmP((x + .5) / c.n, .5, cells, 1, o.sd + k * 37, 2) - .5) * o.amp * D);
        if (c.F(x, yy, 2.5, o.sd + 7) < o.clear) continue;                                        // the field clears
        if (fbmP((x + .5) / c.n, .5, cells * 5, 1, o.sd + 101 + k * 13, 2) < o.brk) continue;      // and every crest terminates
        for (const [dy, b, hh] of prof) for (let q = 0; q < D; q++) c.put(x, yy + dy * D + q, o.pal, b, hh);
        if (o.dark != null && c.F(x, yy, 2, o.sd + 21) < .44 && hash2(x, yy, o.sd + 13) < o.min) for (let q = 0; q < D; q++) c.put(x, yy + 3 * D + q, o.dark, 2, -.3, 1);
      }
    }
  }
  /* a shell fragment: 2–3 authored texels, curved, with its shade. Not a bright dot — a dot is grit. */
  function hash_shell(c, x, y, pal, ground, sd) {
    const D = c.D, w = 2 + (hash2(x, y, sd) < .4 ? 1 : 0), up = hash2(x, y, sd + 1) < .5;
    c.t.beginMark();
    for (let k = 0; k < w; k++) c.dot(x + k * D, y - (k === 1 && up ? D : 0), pal, k === 0 ? 4 : 3, .5, 1);
    c.t.endMark();
    shade(c, x, y + D, { pal: ground, band: 1 });
  }
  /* a wandering line that TILES: the wander is a periodic field of v, so the top and bottom agree */
  const wander = (c, v, cells, amp, sd) => Math.round((fbmP(.5, v, 1, Math.max(2, c.cy(cells)), sd, 2) - .5) * amp * c.D);

  /* a bedded rock pavement. Worley benches at three levels and each level is its OWN bed palette —
     strip a bench and what shows is a different rock, not a darker one. A riser away from the light
     throws two texels of shadow, toward it one lit texel; joints only where two benches at one level
     meet, gated hard. This is the pass-four ledge with the old kit's three stacked beds put back. */
  function pavement(c, st, sd, o) {
    const t = c.t, n = c.n, m = c.m, B = o.beds, D = c.D, lvl = new Int8Array(n * m), id = new Int32Array(n * m);
    const cnt = pk(o.cells, st), cx = c.cx(cnt), cy = c.cy(cnt);
    const th = pk(o.levels, st);
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      const u = (x + .5) / n, v = (y + .5) / m;
      const w = worleyP(u + (c.F(x, y, 4, sd + 1) - .5) * .05, v + (c.F(x, y, 4, sd + 2) - .5) * .05, cx, cy, sd, .9);
      const i = y * n + x, r = hash2(w.id, 1, sd);
      id[i] = w.id; lvl[i] = r < th[0] ? 0 : r < th[1] ? 1 : 2;
    }
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      if (!c.R(x, y)) continue;
      const i = y * n + x, L = lvl[i], lam = c.F(x, y, 6, sd + 4);
      t.set(x, y, B[L], 2, L * 2.2 + (lam > .7 ? .3 : 0));
      /* a stain drifts ALONG a bed, one band at most, and it is solid: the top few percent of a noise
         field is thin and dendritic, so stippling inside it draws twigs on the rock. */
      if (lam > .82) t.shift(x, y, -1);
    }
    const at = (x, y) => c.R(x, y) ? lvl[t.i(x, y)] : 1;
    for (let y = 0; y < m; y++) for (let x = 0; x < n; x++) {
      if (!c.R(x, y)) continue;
      const i = y * n + x, L = at(x, y), dR = L - at(x + 1, y), dD = L - at(x, y + 1);
      if (dR > 0 || dD > 0) t.shift(x, y, 1, 1);
      if (dR > 0) for (let k = 1; k <= (dR > 1 ? 2 * D : D); k++) c.put(x + k, y, B[at(x + k, y)], 0, undefined, 1);
      if (dD > 0) for (let k = 1; k <= (dD > 1 ? 2 * D : D); k++) c.put(x, y + k, B[at(x, y + k)], 0, undefined, 1);
      if (L - at(x - 1, y) < 0 || L - at(x, y - 1) < 0) t.shift(x, y, 1, 1);
      /* joints are LINES, not a net: the decision is per pair of benches, so a joint either runs its
         whole length or is not there at all. A per-texel gate leaves Y-shaped fragments, and a full
         Voronoi net reads as cracked glaze. */
      const jp = pk(o.jointGate || [.34, .46, .54], st);
      const pair = (a, b) => hash2(Math.min(a, b), Math.max(a, b) + 7, sd + 17) < jp;
      if (L === at(x + 1, y) && c.R(x + 1, y) && id[i] !== id[t.i(x + 1, y)] && pair(id[i], id[t.i(x + 1, y)])) o.joint(x, y, L);
      if (L === at(x, y + 1) && c.R(x, y + 1) && id[i] !== id[t.i(x, y + 1)] && pair(id[i], id[t.i(x, y + 1)])) o.joint(x, y, L);
    }
    return lvl;
  }

  // ---- the fourteen ----------------------------------------------------------------------------------
  const NEW = {
    marram: { z: 2, relief: 1.2, lo: .38, hi: .78, seed: 7710, base: SHORE.marram,
      note: 'Marram on the dune — the one upland material that is mostly sand. Every blade in the stand combs the same way, because the wind that built the dune only blows one way; that lean is the material and it is why marram takes no chunk offset. Open pink sand between the crowns with the wind\'s lineation on it, dead thatch collecting under a closed hummock. Lo is pioneer sprigs on bare sand; Hi is a closed hummock, heavy thatch, the sand gone.',
      paint(c, st, sd) {
        const t = c.t, sand = t.addPal(SHORE.dune, .6), green = t.addPal(SHORE.marram, .8), dry = t.addPal(SHORE.marramDry, .7), thatch = t.addPal(SHORE.thatch, .7);
        c.fill(sand, 2, 0);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue; const i = t.i(x, y); const f = c.F(x, y, 5, sd) - .5, r = hash2(x, y, sd + 2);
          if (r < .022 * clamp(1 + f * 2.4, .1, 2)) { t.band[i] = 3; t.h[i] = .2; } else if (r > 1 - .022 * clamp(1 - f * 2.4, .1, 2)) { t.band[i] = 1; t.h[i] = -.2; }
        }
        if (st === 2) for (const s of c.sites(9, .9, sd + 5, s => s.r < .55)) dash(c, s.x, s.y, 4 + Math.round(s.r2 * 5), thatch, 2, sd + 6);
        for (const s of c.sites(pk([11, 7.5, 6], st), .9, sd + 10)) {
          const d = c.F(s.x, s.y, 3, sd + 11); if (s.r > pk([.30, .72, 1.1], st) * (d + .3)) continue;
          fan(c, s.x, s.y, { n: 3 + (s.r2 > .62 ? 2 : 0), len: pk([4, 6, 7], st), jit: 3, lean: .55, spread: .45, pal: s.r2 > .8 ? dry : green, sd: sd + 13 + s.gx, shade: sand });
        }
        return { ground: sand, fringe: green };
      } },

    foreshore: { z: 2, relief: 1.1, lo: .40, hi: .80, seed: 6620, base: SHORE.foreshore,
      note: 'The low-tide flat, DRY albedo — lighter and less red than the photographs, which are all of wet sand. Run the shader\'s wet band over it and it lands where it should; bake the wet in and the tide freezes. A working wave-ripple field with shell hash and a gravel lag in the troughs. Lo is planed — firm quiet sand, runnels only; Hi is megaripple, the lag heavy.',
      paint(c, st, sd) {
        const t = c.t, sand = t.addPal(SHORE.foreshore, .65), dark = t.addPal(SHORE.mineral, .8), shell = t.addPal(SHORE.shell, .5), grav = t.addPal(GROUND.grey, .85), run = t.addPal(SHORE.runnel, .7);
        c.fill(sand, 2, 0);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue; const i = t.i(x, y); const f = c.F(x, y, 4, sd) - .5, r = hash2(x, y, sd + 2);
          if (r < .014 * clamp(1 + f * 2.4, .1, 2)) { t.band[i] = 3; t.h[i] = .2; } else if (r > 1 - .014 * clamp(1 - f * 2.4, .1, 2)) { t.band[i] = 1; t.h[i] = -.2; }
        }
        if (st === 0) for (const s of c.sites(20, .9, sd + 5, s => s.r < .5)) { const len = 5 + Math.round(s.r2 * 7); dash(c, s.x, s.y, len, run, 1, sd + 6); dash(c, s.x, s.y - 1, len, sand, 3, sd + 7); }
        train(c, { pitch: pk([12, 8, 11], st), amp: pk([3, 4, 6], st), clear: pk([.62, .40, .34], st), brk: pk([.48, .34, .3], st), min: pk([.1, .2, .34], st), pal: sand, dark: dark, sd: sd + 10 });
        for (const s of c.sites(18, .95, sd + 20, s => s.r < pk([.14, .2, .26], st) && c.deep(s.x, s.y, 2))) hash_shell(c, s.x, s.y, shell, sand, sd + 21);
        if (st === 2) for (const s of c.sites(9, .9, sd + 30, s => s.r < .55 && c.deep(s.x, s.y, 2))) lump(c, s.x, s.y, 2, 2, { pal: grav, band: 2, h: .6, cap: false, shade: { pal: sand, band: 1 }, lock: 1 });
        return { ground: sand, fringe: sand };
      } },

    ripple: { z: 2, relief: 1.1, lo: .40, hi: .78, seed: 6210, base: SHORE.ripple,
      note: 'The inner flat, wetter and finer than the foreshore. The dark is heavy mineral — garnet and magnetite winnowed into the troughs — and it is why a PEI flat reads striped rather than orange; it never washes over the whole tile. Lo is relict, all but planed off; Hi is storm-built with a gravel lag in the troughs.',
      paint(c, st, sd) {
        const t = c.t, wet = t.addPal(SHORE.ripple, .7), min = t.addPal(SHORE.mineral, .8), grav = t.addPal(GROUND.grey, .85), shell = t.addPal(SHORE.shell, .5);
        c.fill(wet, 2, 0);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue; const i = t.i(x, y); const f = c.F(x, y, 4, sd) - .5, r = hash2(x, y, sd + 2);
          if (r < .016 * clamp(1 + f * 2.4, .1, 2)) { t.band[i] = 3; t.h[i] = .2; } else if (r > 1 - .016 * clamp(1 - f * 2.4, .1, 2)) { t.band[i] = 1; t.h[i] = -.2; }
        }
        train(c, { pitch: pk([10, 6.5, 9], st), amp: pk([2.5, 3, 5], st), clear: pk([.58, .32, .28], st), brk: pk([.44, .26, .24], st), min: pk([.24, .38, .5], st), pal: wet, dark: min, sd: sd + 10 });
        for (const s of c.sites(8, .9, sd + 20, s => s.r < pk([.1, .16, .22], st))) {
          if (t.lock[t.i(s.x, s.y)]) continue; const len = c.px(1 + Math.floor(s.r2 * 3));
          t.beginMark();
          for (let k = 0; k < len; k++) for (let q = 0; q < c.D; q++) c.put(s.x + k, s.y + q, min, 2, -.3, 1);
          t.endMark();
        }
        if (st === 2) for (const s of c.sites(11, .9, sd + 30, s => s.r < .5 && c.deep(s.x, s.y, 2))) lump(c, s.x, s.y, 2, 2, { pal: grav, band: 2, h: .6, cap: false, shade: { pal: wet, band: 1 }, lock: 1 });
        for (const s of c.sites(22, .95, sd + 40, s => s.r < pk([.08, .12, .16], st) && c.deep(s.x, s.y, 2))) hash_shell(c, s.x, s.y, shell, wet, sd + 41);
        return { ground: wet, fringe: wet };
      } },

    silt: { z: 2, relief: 1.2, lo: .38, hi: .76, seed: 4820, base: SHORE.silt,
      note: 'Estuary silt — warm grey-brown, carrying the red beds it was eroded from; a green-grey silt reads as army canvas. Burrows are holes with their spoil thrown on one side, never rings; worm casts are coiled lumps. Lo is a soft fresh sheet barely drained; Hi is crusted and cracked into curled plates.',
      paint(c, st, sd) {
        const t = c.t, mud = t.addPal(SHORE.silt, .8), crust = t.addPal(SHORE.siltCrust, .7), dark = t.addPal(SHORE.anox, .9);
        c.fill(mud, 2, 0);
        /* the 0.3–3 m read: broad drained crust plates, torn, a band up. Without them the flat is an
           even field and the burrows on it are pepper. */
        for (const s of c.sites(pk([30, 22, 26], st), .9, sd + 1, s => s.r < pk([.35, .6, .4], st)))
          patch(c, s.x, s.y, 9 + Math.round(s.r2 * 11), 5 + Math.round(hash2(s.x, s.y, sd) * 6), { pal: st === 0 ? mud : crust, band: st === 0 ? 3 : 2, ul: 3, dr: 1, hole: .14, tear: 1.4, rag: sd + 2, h: .3 });
        /* drainage runnels — short, wandering, dying out; a silt flat drains in threads, not in rows */
        for (const s of c.sites(pk([26, 30, 40], st), .9, sd + 5, s => s.r < .5)) {
          const D = c.D, len = c.px(6 + Math.round(s.r2 * 10)); let yy = s.y;
          t.beginMark();
          for (let k = 0; k < len; k++) { if (k && k % D === 0 && hash2(s.x + k, s.y, sd + 6) < .3) yy += hash2(s.x + k, s.y, sd + 7) < .5 ? -1 : 1; for (let q = 0; q < D; q++) c.put(s.x + k, yy + q, dark, 1, -.7); c.put(s.x + k, yy - 1, mud, 3, .3); }
          t.endMark();
        }
        if (st === 2) {
          const CM = cellMap(c, 10, sd + 3, .9);
          for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
            if (!c.R(x, y)) continue;
            if (CM.rim(x, y) < 0 && c.F(x, y, 6, sd + 4) > .42) { t.set(x, y, mud, 0, -.7); continue; }
            if (CM.r(x, y, 1) < .5) t.set(x, y, crust, CM.rim(x, y) > 0 ? 3 : 2, .3);
          }
        }
        /* a burrow is a hole with its spoil thrown on one side — never a ring */
        for (const s of c.sites(pk([13, 8, 11], st), .9, sd + 10, s => s.r < pk([.3, .7, .5], st) && c.deep(s.x, s.y, 3))) {
          const D = c.D;
          t.beginMark();
          c.dot(s.x, s.y, dark, 0, -1.4, 1); c.dot(s.x + D, s.y, dark, 0, -1.2, 1);
          if (s.r2 > .4) c.dot(s.x, s.y + D, dark, 1, -1, 1);
          c.dot(s.x + 2 * D, s.y + D, crust, 3, .6, 1); c.dot(s.x + D, s.y + 2 * D, crust, 2, .5, 1);
          t.endMark();
        }
        for (const s of c.sites(pk([22, 14, 18], st), .9, sd + 20, s => s.r < .45 && c.deep(s.x, s.y, 2))) lump(c, s.x, s.y, 2 + (s.r2 > .65 ? 1 : 0), 2, { pal: crust, band: 2, h: .8, cap: false, shade: { pal: mud, band: 1 }, lock: 1 });
        return { ground: mud, fringe: mud };
      } },

    shelf: { z: 2, relief: 1.4, lo: .40, hi: .76, seed: 3320, base: SHORE.shelfA,
      note: 'The grey wave-cut pavement, three stacked beds. Pluck a slab and the bed under it is a different rock, not a darker one — that is the whole read, and it is what pass four\'s single palette lost. Barnacle crusts the longest-dry crowns; the joints hold weed where water sits. Lo is intact pavement; Hi is stripped to the third bed with cobble on the benches.',
      paint(c, st, sd) {
        const t = c.t, B = [t.addPal(SHORE.shelfC, .85), t.addPal(SHORE.shelfB, .85), t.addPal(SHORE.shelfA, .85)];
        const weed = t.addPal(SHORE.fucus, .8), barn = t.addPal(SHORE.barn, .6), cob = t.addPal(SHORE.cobble, .9);
        const lvl = pavement(c, st, sd, { beds: B, cells: [6, 7, 8], levels: [[0, .90], [.10, .74], [.22, .66]], jointGate: [.42, .5, .56], joint: (x, y, L) => { const wd = st === 2 && hash2(x >> 2, y >> 2, sd + 8) < .3; c.put(x, y, wd ? weed : B[L], wd ? 2 : 1, undefined, 1); } });
        for (const s of c.sites(2.6, .9, sd + 20)) {
          const i = t.i(s.x, s.y); if (lvl[i] !== 2 || t.lock[i]) continue;
          if (s.r > pk([.8, .55, .34], st) * c.F(s.x, s.y, 4, sd + 21) * 2) continue;
          c.dot(s.x, s.y, barn, s.r2 < .4 ? 4 : 3, .4, 1);
        }
        if (st >= 1) for (const s of c.sites(22, .9, sd + 30, s => s.r < pk([0, .35, .55], st) && c.deep(s.x, s.y, 5))) hollow(c, s.x, s.y, 5 + Math.round(s.r2 * 3), 3 + Math.round(s.r2 * 1.5), { pal: B[lvl[t.i(s.x, s.y)]], hBase: t.h[t.i(s.x, s.y)], dh: -1.4, weed: st === 2 ? weed : null });
        if (st === 2) for (const s of c.sites(12, .9, sd + 40, s => s.r < .35 && c.deep(s.x, s.y, 2))) lump(c, s.x, s.y, 3, 2, { pal: cob, band: 2, h: .9, cap: false, shade: { pal: B[1], band: 1 } });
        return { ground: B[2], fringe: B[2] };
      } },

    talus: { z: 2, relief: 1.7, lo: .40, hi: .74, seed: 2420, base: SHORE.talus,
      note: 'The apron at the cliff toe — what a plan view gets at the foot of a cliff, never the face material. Talus TESSELLATES where shingle scatters: the blocks are Voronoi cells inset by a texel, so the joint between two blocks is one dark line and the blocks pack. A freshly fallen block is lighter than a settled one; lichen greys the long-settled uppers; the interstices stay damp and dark. Lo is a scatter of slabs on a gravel floor; Hi is a deep chaotic blockfield.',
      paint(c, st, sd) {
        const t = c.t, floor = t.addPal(SHORE.till, .85), grav = t.addPal(GROUND.grit, .8), rock = t.addPal(SHORE.talus, .9), fresh = t.addPal(SHORE.bankFresh, .9), lich = t.addPal(SHORE.lichen, .55), damp = t.addPal(SHORE.fucus, .7);
        c.fill(floor, 2, 0);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) { if (!c.R(x, y)) continue; const r = hash2(x, y, sd); if (r < .13) t.set(x, y, grav, 2, .2); else if (r > .93) t.set(x, y, floor, 1, -.2); }
        const CM = cellMap(c, pk([8, 7, 5.5], st), sd + 10, .95), cover = pk([.34, .82, 1], st);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue;
          if (CM.r(x, y, 1) > cover) { if (hash2(x, y, sd + 12) < .3) t.set(x, y, damp, 1, -.4); continue; }
          const rim = CM.rim(x, y);
          if (rim < 0) { t.set(x, y, floor, 0, -.5); continue; }
          const grain = CM.r(x, y, 2) < .34 ? 1 : 0, wear = CM.r(x, y, 3) < .2 ? -1 : 0;
          const isFresh = CM.r(x, y, 4) < pk([.28, .45, .68], st);
          t.set(x, y, isFresh ? fresh : rock, clamp(2 + grain + wear + (rim > 0 ? 1 : 0), 0, 4), 1.5 + CM.r(x, y, 5) * .9);
        }
        for (const s of c.sites(4, .9, sd + 30, s => s.r < pk([.5, .38, .22], st))) { const i = t.i(s.x, s.y); if (t.pal[i] === rock && t.band[i] >= 2) { t.pal[i] = lich; t.band[i] = 3; t.lock[i] = 1; } }
        return { ground: rock, fringe: rock };
      } },

    bank: { z: 2, relief: 1.7, lo: .40, hi: .74, seed: 9120, base: SHORE.bank, face: true,
      note: 'The soft cliff — unconsolidated red till. A FACE material: s runs along the cliff, t runs DOWN it, and the rills run down t. That is the one place in the kit where a directional feature is right, because a face has a direction; rotate the UVs and the geology is nonsense. Every rill wanders on a field that is periodic in t, so it still tiles. Lo is firm and faintly rilled; Hi is failing — deep gullies, clods at the toe, turf hanging over the top.',
      paint(c, st, sd) {
        const t = c.t, till = t.addPal(SHORE.bank, .85), fresh = t.addPal(SHORE.bankFresh, .85), turf = t.addPal(GROUND.grass, .75), stone = t.addPal(GROUND.stone, .9);
        c.fill(till, 2, 0);
        /* slump scars and dried crust plates — the 0.3–3 m read, so the face is not only rills */
        for (const s of c.sites(pk([24, 20, 18], st), .9, sd + 50, s => s.r < pk([.35, .5, .6], st)))
          patch(c, s.x, s.y, 8 + Math.round(s.r2 * 12), 6 + Math.round(hash2(s.x, s.y, sd) * 8), { pal: hash2(s.x, s.y, sd + 51) < .4 ? fresh : till, band: 3, ul: 3, dr: 1, hole: .12, tear: 1.3, rag: sd + 52, h: .3 });
        /* the rills: FEW, and each one a gully with a lit shoulder and a shadowed wall, gated on a slow
           field so it dies out and starts again down the face. Five rills across eight metres, not
           fifty — a face of even stripes is corduroy, the one tell this kit will not ship. */
        const cols = Math.max(2, Math.round(c.n / pk([26, 18, 13], st)));
        for (let k = 0; k < cols; k++) {
          const x0 = k * c.n / cols + hash2(k, 1, sd) * 5, deep = hash2(k, 2, sd) < pk([.14, .4, .7], st);
          for (let y = 0; y < c.m; y++) {
            const x = Math.round(x0 + wander(c, (y + .5) / c.m, deep ? 2 : 3, deep ? 11 : 6, sd + 2 + k * 17));
            if (c.F(x, y, 2, sd + 4 + k) < pk([.5, .38, .24], st)) continue;
            c.put(x, y, till, 0, -1.3); c.put(x - 1, y, till, 3, .5);
            if (deep) { c.put(x + 1, y, till, 0, -1.1); c.put(x + 2, y, till, 1, -.5); c.put(x - 2, y, till, 2, .2); }
          }
        }
        for (const s of c.sites(pk([16, 11, 8], st), .9, sd + 10, s => s.r < .5)) lump(c, s.x, s.y, 2 + Math.round(s.r2 * 2), 2, { pal: hash2(s.x, s.y, sd + 11) < .3 ? fresh : till, band: 3, h: .9, cap: false, shade: { pal: till, band: 1 }, lock: 1 });
        for (const s of c.sites(30, .9, sd + 20, s => s.r < .35)) lump(c, s.x, s.y, 3, 2, { pal: stone, band: 2, h: .8, cap: false, shade: { pal: till, band: 1 }, lock: 1 });
        if (st === 2) for (let x = 0; x < c.n; x++) {
          const h = c.px(3) + Math.round((fbmP((x + .5) / c.n, .5, Math.max(2, c.cx(3)), 1, sd + 30, 2) - .5) * 6 * c.D);
          for (let y = 0; y < h; y++) c.put(x, y, turf, y === h - 1 ? 1 : y < 2 * c.D ? 3 : 2, .8, 1);
        }
        return { ground: till, fringe: till };
      } },

    marsh: { z: 2, relief: 1.2, lo: .38, hi: .76, seed: 8420, base: SHORE.spartina,
      note: 'Cordgrass on anoxic creek mud. Spartina is yellower, duller and stiffer than the upland sward, so the two never read as the same green. Lo is pioneer sprigs on open black mud; base is a closed sward; Hi is high marsh — dead thatch lying over, drift wrack, and evaporite salt pans where the water stood and left.',
      paint(c, st, sd) {
        const t = c.t, mud = t.addPal(SHORE.anox, .85), grass = t.addPal(SHORE.spartina, .8), thatch = t.addPal(SHORE.thatch, .7), pan = t.addPal(SHORE.pan, .5);
        c.fill(mud, 2, 0);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) { if (!c.R(x, y)) continue; if (hash2(x, y, sd) < .1) t.shift(x, y, 1); else if (hash2(x, y, sd + 1) < .08) t.shift(x, y, -1); }
        if (st === 2) for (const s of c.sites(30, .9, sd + 5, s => s.r < .4 && c.deep(s.x, s.y, 6))) patch(c, s.x, s.y, 8 + Math.round(s.r2 * 9), 5 + Math.round(hash2(s.x, s.y, sd) * 5), { pal: pan, band: 2, ul: 3, dr: 1, hole: .12, tear: 1.4, rag: sd + 6, lock: 1 });
        if (st === 2) for (const s of c.sites(7, .9, sd + 8, s => s.r < .6)) dash(c, s.x, s.y, 4 + Math.round(s.r2 * 6), thatch, 2, sd + 9);
        for (const s of c.sites(pk([9, 5.2, 6], st), .9, sd + 10)) {
          const d = c.F(s.x, s.y, 3, sd + 11); if (s.r > pk([.26, 1, .8], st) * (d + .35)) continue;
          fan(c, s.x, s.y, { n: 3, len: pk([4, 6, 5], st), jit: 2, lean: .18, spread: .55, pal: grass, sd: sd + 13 + s.gx, shade: mud });
        }
        return { ground: st === 0 ? mud : grass, fringe: grass };
      } },

    sedge: { z: 2, relief: 1.3, lo: .38, hi: .76, seed: 7150, base: SHORE.sedge,
      note: 'The freshwater edge — sedge tussocks on peat, with moss between. The tussock is the kit\'s own fountain of blades in a duller, yellower green; the ground under it is black peat, not soil. Lo is a short sedge lawn with the peat and moss showing; Hi is rank tussocks with heavy thatch and rush standing over them.',
      paint(c, st, sd) {
        const t = c.t, peat = t.addPal(SHORE.peat, .85), sedge = t.addPal(SHORE.sedge, .8), moss = t.addPal(SHORE.moss, .75), rush = t.addPal(SHORE.rush, .8), thatch = t.addPal(SHORE.thatch, .7);
        c.fill(peat, 2, 0);
        for (const s of c.sites(pk([4, 5, 7], st), .9, sd + 1)) {
          const d = c.F(s.x, s.y, 3, sd + 2); if (s.r > pk([1, .8, .5], st) * (d + .3)) continue;
          lump(c, s.x, s.y, 3 + Math.round(s.r2 * 2), 2 + (s.r2 > .5 ? 1 : 0), { pal: moss, band: 2, h: .5, cap: false, seam: 0, shade: { pal: peat, band: 1 }, lock: 1 });
        }
        if (st === 2) for (const s of c.sites(8, .9, sd + 5, s => s.r < .5)) dash(c, s.x, s.y, 4 + Math.round(s.r2 * 5), thatch, 2, sd + 6);
        for (const s of c.sites(pk([10, 7.5, 6.5], st), .95, sd + 10)) {
          const d = c.F(s.x, s.y, 4, sd + 11); if (s.r > (d - .18) * pk([1.3, 1.8, 2.2], st)) continue;
          tussock(c, s.x, s.y, s.r2 > pk([.85, .6, .4], st), sedge, sd + 13, peat);
        }
        if (st === 2) for (const s of c.sites(11, .9, sd + 20, s => s.r < .5)) fan(c, s.x, s.y, { n: 2, len: 6, jit: 2, lean: .12, spread: .7, pal: rush, tip: 3, sd: sd + 21 + s.gx });
        return { ground: st === 0 ? peat : sedge, fringe: sedge };
      } },

    rockweed: { z: 2, relief: 1.4, lo: .38, hi: .76, seed: 5240, base: SHORE.asco,
      note: 'Ascophyllum on the exposed intertidal — olive going gold, the signature colour of a ledge at low water and nothing else in the kit is near it. The fronds are straps that all lie the way the last ebb laid them, drawn lower over upper, and the canopy KEEPS ITS GAPS: barnacled rock shows through, because a weed that fills every texel is a green floor. Lo is barnacled rock with scattered tufts; Hi is a deep drape with bladders and Ulva in the wet.',
      paint(c, st, sd) {
        const t = c.t, rock = t.addPal(SHORE.rock, .9), barn = t.addPal(SHORE.barn, .6), weed = t.addPal(SHORE.asco, .85), ulva = t.addPal(SHORE.ulva, .8);
        c.fill(rock, 2, 0);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) { if (!c.R(x, y)) continue; const r = hash2(x, y, sd); if (r < .1) t.shift(x, y, 1); else if (r > .93) t.shift(x, y, -1); }
        for (const s of c.sites(3, .9, sd + 5, s => s.r < .34 * (c.F(s.x, s.y, 4, sd + 6) + .4))) c.put(s.x, s.y, barn, s.r2 < .35 ? 4 : 3, .4, 1);
        const cover = pk([.3, .78, 1.05], st), lean = .5;
        for (const s of c.sites(pk([7, 4.4, 3.6], st), .9, sd + 10)) {
          const g = c.F(s.x, s.y, 3, sd + 11); if (s.r > cover * (g + .28)) continue;
          const len = 4 + Math.round(s.r2 * pk([2, 4, 6], st));
          for (let k = 0; k < 2 + (s.r2 > .6 ? 1 : 0); k++) blade(c, s.x + k - 1, s.y, len, lean + (hash2(s.x, k, sd + 12) - .5) * .5, weed, 3, sd + 13 + k * 5);
          if (st === 2 && s.r2 > .55) { const bx = s.x + Math.round(lean * len * .6), by = s.y - Math.round(len * .6); lump(c, bx, by, 2, 2, { pal: weed, band: 4, h: 1.4, cap: false, seam: 0, lock: 1 }); }
        }
        if (st === 2) for (const s of c.sites(12, .9, sd + 30, s => s.r < .45)) lump(c, s.x, s.y, 3, 2, { pal: ulva, band: 3, h: .5, cap: false, seam: 0, lock: 1 });
        return { ground: st === 0 ? rock : weed, fringe: weed };
      } },

    musselbed: { z: 2, relief: 1.5, lo: .38, hi: .74, seed: 1840, base: SHORE.mussel,
      note: 'The animals ARE the substrate, which is why a bed is a ground material and not scatter — at this scale a mussel is two texels. Every shell FILLS its cell: left as a small ellipse in the middle of one, a packed bed comes out as spilled rice. The lie is anisotropic, from a lattice with more rows than columns — never a sheared one. The floor is black-brown anoxic mud; a grey floor makes the bed read as gravel, so painting a bed thin over clean silt will never look like a thin bed — use the ladder. Lo is spat and scattered clumps; Hi is thick and hummocked with dead shell in the troughs.',
      paint(c, st, sd) {
        const t = c.t, floor = t.addPal(SHORE.bedMud, .85), sh = t.addPal(SHORE.mussel, .9), old = t.addPal(SHORE.musselOld, .8), cultch = t.addPal(SHORE.cultch, .6);
        c.fill(floor, 2, 0);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) { if (!c.R(x, y)) continue; if (hash2(x, y, sd) < .08) t.shift(x, y, -1); }
        const CM = cellMap(c, pk([16, 19, 21], st), sd + 10, .85, 2.4), cover = pk([.3, .88, 1], st);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue;
          const hum = c.F(x, y, 3, sd + 11);
          if (CM.r(x, y, 1) > cover * (st === 0 ? hum + .3 : 1)) continue;
          const rim = CM.rim(x, y), o = CM.r(x, y, 2) < .18;
          let b = 2 + (rim > 0 ? 1 : 0) + (rim < 0 ? -1 : 0) + (CM.r(x, y, 3) < .25 ? 1 : 0);
          if (st === 2) b += hum > .62 ? 1 : hum < .34 ? -1 : 0;
          t.set(x, y, o ? old : sh, clamp(b, 0, 4), 1.1 + (st === 2 ? (hum - .5) * 1.4 : 0) + CM.r(x, y, 4) * .5, 1);
        }
        for (const s of c.sites(pk([26, 20, 12], st), .9, sd + 30, s => s.r < pk([.3, .35, .6], st))) hash_shell(c, s.x, s.y, cultch, floor, sd + 31);
        return { ground: st === 0 ? floor : sh, fringe: sh, fringe2: cultch };
      } },

    oysterreef: { z: 2, relief: 1.6, lo: .38, hi: .74, seed: 1450, base: SHORE.oyster,
      note: 'Crassostrea in clusters, cemented and standing on edge, with the film of diatom and green algae a living reef always carries — a white oyster reef is a museum shell. The clusters are decided per cluster and drawn opaque, and the gaps between them are the channels. Lo is singles and cultch scattered on the mud; base is a working reef with the channels open; Hi is closed, the channels choked with mud.',
      paint(c, st, sd) {
        const t = c.t, floor = t.addPal(SHORE.bedMud, .85), sh = t.addPal(SHORE.oyster, .85), lip = t.addPal(SHORE.oysterFresh, .55), cultch = t.addPal(SHORE.cultch, .6);
        c.fill(floor, 2, 0);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) { if (!c.R(x, y)) continue; if (hash2(x, y, sd) < .09) t.shift(x, y, -1); }
        const CL = cellMap(c, pk([7, 9, 8], st), sd + 10, .95), SHc = cellMap(c, 26, sd + 20, .8, 1.7);
        const cover = pk([.22, .62, .85], st);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue;
          if (CL.r(x, y, 1) > cover) continue;
          if (CL.d(x, y) > pk([.26, .34, .42], st)) continue;
          const rim = SHc.rim(x, y);
          if (rim < 0 && SHc.r(x, y, 5) < .7) { t.set(x, y, floor, 1, .4, 1); continue; }
          const b = 2 + (SHc.r(x, y, 2) < .3 ? 1 : 0) - (SHc.r(x, y, 3) < .22 ? 1 : 0);
          t.set(x, y, rim > 0 && SHc.r(x, y, 4) < .45 ? lip : sh, clamp(b + (rim > 0 ? 1 : 0), 0, 4), 1.4 + SHc.r(x, y, 6) * .8, 1);
        }
        for (const s of c.sites(pk([9, 16, 22], st), .9, sd + 30, s => s.r < pk([.7, .4, .25], st))) hash_shell(c, s.x, s.y, cultch, floor, sd + 31);
        return { ground: st === 0 ? floor : sh, fringe: sh, fringe2: cultch };
      } },

    eelgrass: { z: 2, relief: 1.2, lo: .38, hi: .76, seed: 2960, base: SHORE.zostera,
      note: 'Zostera on muddy sand, subtidal into the channel. The blades are long lax ribbons and every one of them lies the way the current last ran; the substrate is deliberately silted, because a clean sand floor under a thin bed reads as sand with green marks on it. Lo is sparse shoots on open sand; Hi is deep and epiphyte-crusted with last year\'s cut blades drifted in.',
      paint(c, st, sd) {
        const t = c.t, sand = t.addPal(SHORE.siltSand, .7), silt = t.addPal(SHORE.silt, .8), green = t.addPal(SHORE.zostera, .85), oldb = t.addPal(SHORE.zosOld, .7);
        c.fill(sand, 1, 0);
        for (const s of c.sites(14, .9, sd + 1, s => s.r < .75)) patch(c, s.x, s.y, 8 + Math.round(s.r2 * 11), 5 + Math.round(hash2(s.x, s.y, sd) * 5), { pal: silt, band: 2, ul: 1, dr: 2, hole: .18, tear: 1.5, rag: sd + 2 });
        if (st === 2) for (const s of c.sites(10, .9, sd + 5, s => s.r < .5)) blade(c, s.x, s.y, 5 + Math.round(s.r2 * 4), .9, oldb, 3, sd + 6);
        const lean = .75;
        for (const s of c.sites(pk([9, 5, 3.8], st), .9, sd + 10)) {
          const d = c.F(s.x, s.y, 3, sd + 11); if (s.r > pk([.26, .8, 1.1], st) * (d + .3)) continue;
          const n = 2 + (s.r2 > .5 ? 1 : 0) + (s.r2 > .85 ? 1 : 0);
          for (let k = 0; k < n; k++) blade(c, s.x + k - 1, s.y, pk([5, 7, 9], st) - Math.round(hash2(s.x, k, sd + 12) * 3), lean + (hash2(s.x, k, sd + 13) - .5) * .35, k === n - 1 && st === 2 ? oldb : green, 4, sd + 14 + k * 5);
        }
        return { ground: st === 0 ? sand : green, fringe: green };
      } },

    irishmoss: { z: 2, relief: 1.3, lo: .36, hi: .74, seed: 3680, base: SHORE.chondrus,
      note: 'Chondrus on red cobble — dark wine-purple in the shade going olive-bronze where the sun has bleached it, and that SPREAD is the material, not a tint on it. The cushions fill their cells; the cobble under them is held down in value so a thin turf still reads as turf on rock. Lo is scattered cushions on barnacled cobble; Hi is a deep drape with the cobble hidden.',
      paint(c, st, sd) {
        const t = c.t, cob = t.addPal(SHORE.cobble, .9), red = t.addPal(SHORE.ledgeC, .9), barn = t.addPal(SHORE.barn, .55), moss = t.addPal(SHORE.chondrus, .95), sun = t.addPal(SHORE.chondrusSun, .8);
        c.fill(red, 1, 0);
        for (const s of c.sites(4.2, .85, sd + 1)) {
          const w = 3 + Math.round(s.r2 * 3); if (!c.deep(s.x, s.y, (w >> 1) + 1)) continue;
          lump(c, s.x, s.y, w, Math.max(2, Math.round(w * .7)), { pal: s.r < .45 ? cob : red, band: s.r < .45 ? 1 : 2, h: w * .4, hBase: 0, cap: false, shade: { pal: red, band: 0 } });
        }
        for (const s of c.sites(3.4, .9, sd + 5, s => s.r < pk([.4, .22, .1], st))) c.put(s.x, s.y, barn, 3, .4, 1);
        const CM = cellMap(c, pk([13, 11, 9], st), sd + 10, .9), cover = pk([.34, .8, 1.05], st);
        for (let y = 0; y < c.m; y++) for (let x = 0; x < c.n; x++) {
          if (!c.R(x, y)) continue;
          const g = c.F(x, y, 3, sd + 11); if (CM.r(x, y, 1) > cover * (g + .35)) continue;
          const rim = CM.rim(x, y), bleach = c.F(x, y, 4, sd + 12) > .58;
          t.set(x, y, bleach ? sun : moss, clamp(2 + (rim > 0 ? 1 : 0) + (rim < 0 ? -1 : 0) + (CM.r(x, y, 2) < .3 ? 1 : 0), 0, 4), 1.2 + CM.r(x, y, 3) * .6, 1);
        }
        return { ground: st === 0 ? red : moss, fringe: moss };
      } },
  };

  // ---- the five, with the detail the old kit had -------------------------------------------------------
  /* wrap, never rewrite: pass four's paint runs first and then the marks it dropped go on top */
  function after(key, fn) {
    const M = MATS[key], base = M.paint;
    M.paint = function (c, st, sd) { const pals = base.call(M, c, st, sd); fn(c, st, sd, pals); return pals; };
    M.note = M.note + ' — PASS FIVE: ' + fn.note;
  }

  const sandDetail = (c, st, sd, pals) => {
    const t = c.t, min = t.addPal(SHORE.mineral, .8), shell = t.addPal(SHORE.shell, .5);
    /* heavy mineral in the lows — the reason a red-bed flat reads striped and not orange */
    for (const s of c.sites(6, .9, sd + 60, s => s.r < pk([.3, .18, .12], st))) {
      const f = c.F(s.x, s.y, 5, sd); if (f > .46) continue;
      const len = 1 + Math.floor(s.r2 * 3);
      for (let k = 0; k < len; k++) { const i = t.i(s.x + k, s.y); if (!c.R(s.x + k, s.y) || t.lock[i]) continue; c.put(s.x + k, s.y, min, 2, -.3, 1); }
    }
    /* shell HASH — a fragment is 2–3 texels and curved; a single bright texel is grit */
    for (const s of c.sites(20, .95, sd + 70, s => s.r < pk([.18, .3, .42], st) && c.deep(s.x, s.y, 2))) { if (t.lock[t.i(s.x, s.y)]) continue; hash_shell(c, s.x, s.y, shell, pals.ground, sd + 71); }
  };
  sandDetail.note = 'shell hash (2–3 texels, curved, with its shade) and heavy mineral winnowed into the lows.';

  const grassDetail = (c, st, sd, pals) => {
    const t = c.t, straw = t.addPal(GROUND.straw, .7), moss = t.addPal(SHORE.moss, .75);
    /* the old kit\'s straw drift: dry blade ticks where the slow field runs high, at one value */
    for (const s of c.sites(pk([7, 6, 5], st), .9, sd + 60, s => s.r < pk([.5, .34, .5], st))) {
      if (c.F(s.x, s.y, 2, sd + 5) < .58) continue; const i = t.i(s.x, s.y); if (t.lock[i] || t.pal[i] !== pals.ground) continue;
      c.put(s.x, s.y, straw, 3, .6, 1); if (s.r2 > .55) c.put(s.x, s.y - 1, straw, 2, .8, 1);
    }
    /* moss cushions in the damp hollows */
    for (const s of c.sites(pk([14, 18, 24], st), .9, sd + 70, s => s.r < .35 && c.deep(s.x, s.y, 3))) {
      if (c.F(s.x, s.y, 2, sd + 5) > .40) continue; if (t.lock[t.i(s.x, s.y)]) continue;
      lump(c, s.x, s.y, 3, 2, { pal: moss, band: 2, h: .5, cap: false, seam: 0, shade: { pal: pals.ground, band: 1 }, lock: 1 });
    }
  };
  grassDetail.note = 'the straw drift and the moss cushions in the damp hollows.';

  const dirtDetail = (c, st, sd, pals) => {
    const t = c.t, peb = t.addPal(GROUND.peb, .8), grey = t.addPal(GROUND.stone, .9);
    /* pebble lag: where the fines have blown out, the stones are left crowded and flush */
    for (const s of c.sites(pk([9, 5.5, 6], st), .9, sd + 60, s => s.r < pk([.2, .55, .35], st) && c.deep(s.x, s.y, 2))) {
      if (c.F(s.x, s.y, 3, sd + 61) < .52) continue; if (t.lock[t.i(s.x, s.y)]) continue;
      lump(c, s.x, s.y, 2 + (s.r2 > .8 ? 1 : 0), 2, { pal: s.r2 > .6 ? grey : peb, band: 2, h: .6, cap: false, shade: { pal: pals.ground, band: 1 }, lock: 1 });
    }
  };
  dirtDetail.note = 'the pebble lag where the fines have blown out.';

  const shingleDetail = (c, st, sd, pals) => {
    const t = c.t, barn = t.addPal(SHORE.barn, .55), wet = t.addPal(SHORE.mineral, .7);
    /* barnacle high in the berm where the stones are long dry; the toe stones are wet and dark.
       A beach that is all one wetness has no tide in it. */
    for (const s of c.sites(3, .9, sd + 60)) {
      const i = t.i(s.x, s.y), f = c.F(s.x, s.y, 2, sd + 61);
      if (!c.R(s.x, s.y) || t.pal[i] === pals.ground) continue;
      if (f > .58 && s.r < .3 && t.band[i] >= 2) { t.pal[i] = barn; t.band[i] = s.r2 < .4 ? 4 : 3; t.lock[i] = 1; }
      else if (f < .38 && s.r < .5) { t.band[i] = Math.max(0, t.band[i] - 1); if (s.r2 < .25) { t.pal[i] = wet; t.band[i] = 2; t.lock[i] = 1; } }
    }
  };
  shingleDetail.note = 'barnacle on the long-dry stones high in the berm, and the wet dark stones at the toe.';

  /* the ledge, re-baked on three beds. Pass four ran one red palette at three bands, which reads as
     one bed with dents in it; the old kit stacked three, and the difference is the whole material. */
  MATS.ledge = Object.assign({}, MATS.ledge, {
    note: 'The red wave-cut platform as slab benches — fewer, bigger, mostly one level, so it is a floor and not a mosaic. PASS FIVE: three stacked beds, each its own palette, so a plucked bench shows a different rock and not a darker one; barnacle on the longest-dry crowns; weed in the joints and the scour pans.',
    paint(c, st, sd) {
      const t = c.t, B = [t.addPal(SHORE.ledgeC, .9), t.addPal(SHORE.ledgeB, .9), t.addPal(SHORE.ledgeA, .9)];
      const weed = t.addPal(GROUND.weed, .8), cob = t.addPal(GROUND.cob, .9), barn = t.addPal(SHORE.barn, .55);
      const lvl = pavement(c, st, sd, { beds: B, cells: [5, 6, 7], levels: [[0, .82], [.12, .76], [.24, .70]], jointGate: [.34, .46, .54], joint: (x, y, L) => { const wd = st >= 1 && hash2(x >> 2, y >> 2, sd + 8) < pk([0, .3, .45], st); c.put(x, y, wd ? weed : B[L], 1, undefined, 1); } });
      for (const s of c.sites(3.4, .9, sd + 20)) {
        const i = t.i(s.x, s.y); if (lvl[i] !== 2 || t.lock[i]) continue;
        if (s.r > pk([.44, .3, .18], st) * c.F(s.x, s.y, 4, sd + 21) * 2) continue;
        c.put(s.x, s.y, barn, s.r2 < .4 ? 4 : 3, .4, 1);
      }
      if (st >= 1) for (const s of c.sites(22, .9, sd + 30, s => s.r < pk([0, .35, .55], st) && c.deep(s.x, s.y, 5))) hollow(c, s.x, s.y, 5 + Math.round(s.r2 * 3), 3 + Math.round(s.r2 * 1.5), { pal: B[lvl[t.i(s.x, s.y)]], hBase: t.h[t.i(s.x, s.y)], dh: -1.4, weed: st === 2 ? weed : null });
      if (st === 2) for (const s of c.sites(12, .9, sd + 40, s => s.r < .3 && c.deep(s.x, s.y, 2))) lump(c, s.x, s.y, 3, 2, { pal: cob, band: 2, h: .9, cap: false, shade: { pal: B[1], band: 1 } });
      return { ground: B[2], fringe: B[2] };
    },
  });

  after('sand', sandDetail); after('grass', grassDetail); after('dirt', dirtDetail); after('shingle', shingleDetail);

  // ---- registration ------------------------------------------------------------------------------------
  for (const k in NEW) MATS[k] = NEW[k];
  /* who overhangs whom, in the tidal frame: the higher ground cuts its face, throws its shade and
     hangs its fringe over the lower. The old kit's five named strips fall out of this table. */
  Object.assign(RANK, { sedge: 0, marram: .5, shelf: 1, irishmoss: 1.2, talus: 1.5, rockweed: 2.2, marsh: 4, musselbed: 4.4, oysterreef: 4.4, eelgrass: 4.6, silt: 5.5, foreshore: 6.5, ripple: 7 });
  Object.assign(FRINGE, { sedge: 'blade', marram: 'blade', marsh: 'blade', eelgrass: 'blade', rockweed: 'blade', irishmoss: 'blade', shelf: 'lip', talus: 'stones', musselbed: 'stones', oysterreef: 'stones', silt: 'crumb', foreshore: 'crumb', ripple: 'crumb' });

  /* and how each behaves under the paint tool — the RANK table said WHO overhangs whom along an
     authored seam; markLead / bias say the same thing texel by texel as a splat weight rises, which
     is the only form a paint tool can use. The two agree: what stands proud carries the higher bias. */
  Object.assign(K.BLEND, {
    marram: { markLead: .85, grain: 4, bias: .4 }, foreshore: { markLead: .15, grain: 5, bias: -.3 },
    ripple: { markLead: .10, grain: 6, bias: -.5 }, silt: { markLead: .12, grain: 4, bias: -.9 },
    shelf: { markLead: .5, grain: 3, bias: 1.5 }, talus: { markLead: 1, grain: 3, bias: 1.8 },
    bank: { markLead: .4, grain: 5, bias: 1.0 }, marsh: { markLead: .85, grain: 4, bias: .3 },
    sedge: { markLead: .9, grain: 4, bias: .5 }, rockweed: { markLead: .7, grain: 4, bias: .2 },
    musselbed: { markLead: 1, grain: 3, bias: .9 }, oysterreef: { markLead: 1, grain: 3, bias: 1.0 },
    eelgrass: { markLead: .75, grain: 4, bias: 0 }, irishmoss: { markLead: .75, grain: 5, bias: .2 },
  });

  const ORDER = ['marram', 'foreshore', 'ripple', 'silt', 'shelf', 'talus', 'bank', 'marsh', 'sedge', 'rockweed', 'musselbed', 'oysterreef', 'eelgrass', 'irishmoss'];
  const REDONE = ['grass', 'dirt', 'sand', 'shingle', 'ledge'];
  /* the eight lines a shore is actually made of: the dune back and front, the strand, the flat,
     the weed line, the cliff toe (the strip the old kit's README called the obvious sixth), the
     bed margin, the creek */
  const PAIRS = [['grass', 'marram'], ['marram', 'sand'], ['sand', 'foreshore'], ['foreshore', 'ripple'], ['ledge', 'rockweed'], ['talus', 'shelf'], ['musselbed', 'silt'], ['marsh', 'silt']];

  root.PxKit2 = { SHORE, ORDER, REDONE, PAIRS, cellMap, blade, fan, train, hash_shell, pavement, wander };
})(typeof globalThis !== 'undefined' ? globalThis : window);
