/* Hidden Harbours — PIXEL ISO ROCK, PASS THREE.  Supersedes Art/rockIsoRig.js (superellipsoid
   lumps, old dialect) and Art/pxRockIsoRig.js (pass two, polyhedral but untextured by a scene).
   Sits beside Art/pxKit.js rock(), which stays only as the 3–20 px scatter stone.

   WHAT WAS WRONG, LOOKING AT THE PIXELS
   · pass one (rockIsoRig) was a LOAF. Every species was a squashed superellipsoid; `angular` raised
     an exponent, which cannot put a straight edge, a notch or a corner anywhere in a silhouette. Six
     species read as one shape at five squashes, in the old dialect besides — 8 steps, Bayer dither,
     a 1 px keyline — so on the pixel ground it was a sticker.
   · pass two (pxRockIsoRig) replaced the loaf with a DRUM. The plan became a polygon, which was
     right, but its radius varied by only ±13 % and it was extruded straight up, so every form came
     out a near-regular hexagonal prism with an enormous flat cap: a birthday cake at four sizes.
     Baked out, block · slab · knuckle · shelf were the same silhouette. The cap took bed tone per
     texel, so a tilted cap drew a closed CONTOUR RING across itself — a drawn outline, the exact
     sticker read the rewrite was for. Siltstone beds landed on caps as pale blank fields. The pool
     basin was a band-0 hole punched through the shelf.
   · neither shipped a contact. A rock sat ON the ground, and the ground stopped at the rock.

   WHAT PASS THREE CHANGES
   1 · TAPER. A mass is a plan polygon under a BATTER: the plan shrinks with height, so the sides are
       real sloping planes with real corners and the cap is SMALL. Silhouette stops being a drum;
       the mass reads bottom-heavy, the way a rock resting on its own talus does.
   2 · NOTCHES. The plan carries 1–3 vertices pulled hard in (0.42–0.64 r) against a 0.30–0.45
       radius spread, so the outline has bites and re-entrant corners. That is what "not round" is —
       a straight chord where a chunk came off, not a lower exponent.
   3 · LEAN. Shear moves the mass with height, so a block can lean, a fin can stand over, and a
       perched block can overhang its own plinth (with a baked underside shadow).
   4 · THE CAP IS FACETED, NOT STRIPED. One to three cap planes split by a chord, each flat, meeting
       at an arris — and bed tone on a cap is read ONCE at the cap's own height, so the contour rings
       are gone. Bedding shows where bedding shows on a real block: on the sides.
   5 · SIXTEEN FORMS, and they differ in silhouette: erratic · block · perched · slab · fin · wedge ·
       cloven · knuckle · bench · shelf · prisms · scree · cobbles · skerry · spine · apron. 64
       shipped variants, each mirrorable, over five stones and three tides.
   6 · THE SEAT IS PAINTED BY THE TERRAIN KIT ITSELF. skirt(b, {seat, seat2}) opens a region around
       the rock's toe and hands it to PxKit.paintRegion — so the apron is literally grass, sand,
       shingle, silt, talus, rockweed as the kit paints them, with the kit's own marks lapping over
       the rock and the kit's own edge language (cut face, thrown shade, fringe) between TWO
       materials. A rock dropped on a material boundary IS the transition: no cross-fade, no ellipse
       of shadow, no second palette invented here.

   Camera unchanged (ADR-0006/0022): ¾ from the SOUTH, 40° elevation, orthographic, 32 px = 1 m,
   depth × sin40, height × cos40, +y north. Pivot unchanged: BOTTOM-CENTRE GROUND CONTACT.

     PxRockIso2.FORMS · STONES · SEATS
     PxRockIso2.build(form, {variant, stone, tide, dress, seed, mirror, params}) -> b
     PxRockIso2.albedo(b) · unlit(b) · mask(b) · normal(b)      -> RGBA b.w × b.h
     PxRockIso2.skirt(b, {seat, seat2, axis, step, seed})       -> {rgba, blend} on the same cell
     b.anchors — footprint · perch · snags · hazard · pool · weedLine · pivot
   Runs in the run_script bake sandbox and in the browser. */
(function (root) {
  'use strict';
  const P = root.PxLang; if (!P) throw new Error('Art/pixelLanguage.js must be loaded first');
  const { Tile, stamp, sites, ellipse, stencil, hash2, fbmP, clamp, mix, render, packMask, normalView, packBlend } = P;
  const PPU = 32, T = 32, ELEV = 40 * Math.PI / 180, Q = Math.sin(ELEV), HZ = Math.cos(ELEV), TAU = Math.PI * 2;
  const COLD = P.COLD, SEA = '#2a5560', SHALLOW = '#8EA59C';

  // ---- stones -----------------------------------------------------------------------------------
  /* sandstone / till / basalt verbatim from PxCliff.DEF, so a block and the face it fell off are the
     same rock and the same bed sequence. granite and quartzite carry no cliff. */
  const STONES = {
    sandstone: { name: 'Red Sandstone', base: '#a6583b', c: .95, thin: [3, 4], massive: [6, 11], silt: .26,
      bed: 1.00, jsp: [8, 5], spall: .55, socket: .45, lichen: '#97a08b', lich: .18, chip: .5,
      note: 'The red bed in the round. Massive units stand proud and carry the light, laminated packets are recessive and flaggy, the block ends gated joints down a side. Same sequence as the cliff.' },
    till: { name: 'Boulder Clay', base: '#8a5a3e', c: .85, thin: [5, 3], massive: [7, 6], silt: 0,
      bed: .16, jsp: [13, 7], spall: .20, socket: .30, clast: '#9d8b7a', lichen: '#5b6a37', lich: .10, chip: .2,
      note: 'Cemented boulder clay — a matrix lump with clasts standing out of it as lit stones. Almost unbedded; erodes to a blunt nose, never a sheer face.' },
    basalt: { name: 'Basalt', base: '#5a6169', c: 1.00, thin: [4, 3], massive: [9, 7], silt: 0,
      bed: .38, jsp: [5, 3], spall: .30, socket: .50, columnar: 1, lichen: '#c08a3e', lich: .14, chip: .7,
      note: 'Columnar. Identity sits on the column, never the segment: each column its own tone and its own segment cracks. Orange Xanthoria on the sunlit W/SW facets only.' },
    granite: { name: 'Granite', base: '#6f7169', c: .90, thin: [7, 5], massive: [12, 9], silt: 0,
      bed: .08, jsp: [15, 9], spall: .45, socket: .18, fleck: '#b9866a', lichen: '#9fa694', lich: .26, chip: .35, sheet: 2,
      note: 'Ice-dropped. Unbedded — sheeting joints instead, a few long shallow shells off the top — coarse feldspar fleck, the bluntest form set in the kit. Still planes, never a dome.' },
    quartzite: { name: 'Quartzite', base: '#8d8272', c: .90, thin: [4, 4], massive: [8, 8], silt: 0,
      bed: .32, jsp: [6, 4], spall: .60, socket: .22, vein: '#efe7d6', lichen: '#8f9a8c', lich: .20, chip: .9,
      note: 'Pale and brittle. Breaks to sharp shards with bright quartz veins along the facet breaks; the angular form set, and the one that keeps its corners.' },
  };

  // ---- plan polygons ----------------------------------------------------------------------------
  /* a star-shaped plan in polar form. planAt solves the exact polar equation of the CHORD between
     two vertices, so every edge is a straight line, every vertex a corner, and a vertex pulled in
     hard is a NOTCH the silhouette actually shows. */
  function finishPlan(V, sq) {
    V.sort((p, q) => p.a - q.a);
    const pl = { V, sq, n: V.length, rmax: Math.max.apply(null, V.map(v => v.r)), E: [] };
    for (let i = 0; i < pl.n; i++) {
      const A = V[i], B = V[(i + 1) % pl.n];
      const ex = Math.cos(B.a) * B.r - Math.cos(A.a) * A.r, ey = (Math.sin(B.a) * B.r - Math.sin(A.a) * A.r) * sq;
      const L = Math.hypot(ex, ey) || 1;
      pl.E.push({ nx: ey / L, ny: -ex / L });
    }
    return pl;
  }
  /* o = {turn, jit (angle jitter 0..1), rough (radius spread 0..1), notch (deep bites), sq} */
  function mkPlan(n, r, o, seed) {
    const jit = o.jit == null ? .7 : o.jit, rough = o.rough == null ? .32 : o.rough, V = [];
    for (let i = 0; i < n; i++) {
      const a = (o.turn || 0) + i / n * TAU + (hash2(i, 1, seed) - .5) * (TAU / n) * jit;
      V.push({ a: ((a % TAU) + TAU) % TAU, r: r * (1 - rough * hash2(i, 2, seed)) });
    }
    for (let k = 0; k < (o.notch || 0); k++) {
      const i = Math.floor(hash2(k, 5, seed) * n) % n;
      V[i].r *= .42 + .22 * hash2(k, 6, seed);
    }
    return finishPlan(V, o.sq == null ? .8 : o.sq);
  }
  const scalePlan = (pl, k) => finishPlan(pl.V.map(v => ({ a: v.a, r: v.r * k })), pl.sq);
  const flipPlan = (pl) => finishPlan(pl.V.map(v => ({ a: ((Math.PI - v.a) % TAU + TAU) % TAU, r: v.r })), pl.sq);
  function planAt(pl, x, y) {
    const yy = y / pl.sq, RR = Math.hypot(x, yy);
    if (RR > pl.rmax) return null;
    let a = Math.atan2(yy, x); a = ((a % TAU) + TAU) % TAU;
    const V = pl.V, n = pl.n;
    let i = n - 1;
    for (let k = 0; k < n; k++) if (V[k].a <= a) i = k;
    const A = V[i], B = V[(i + 1) % n];
    let aA = A.a, aB = B.a, t = a;
    if (aB <= aA) { aB += TAU; if (t < aA) t += TAU; }
    const den = A.r * Math.sin(t - aA) + B.r * Math.sin(aB - t);
    const rb = den > 1e-5 ? A.r * B.r * Math.sin(aB - aA) / den : Math.max(A.r, B.r);
    if (RR > rb) return null;
    return { t: rb > 0 ? RR / rb : 0, e: i, rb };
  }

  // ---- masses -----------------------------------------------------------------------------------
  /* one mass: a plan polygon under a batter (taper), leaning (shx), capped by 1–3 tilted planes.
       taper   0 = prism, .5 = the top plan is half the base plan  — the batter
       shx     screen-x drift per texel of height                  — the lean
       ax/ay   cap plane tilt                                       — how it came to rest
       splits  [{c,s,off,dax,day,dz}] chords across the cap, each side its own plane
       under   texels of baked underside shadow below the mass base (an overhang) */
  const mass = (o) => Object.assign({ cx: 0, cy: 0, z0: 0, taper: .30, shx: 0, ax: 0, ay: 0, splits: [], cols: 0 }, o);
  const split = (ang, off, dax, day, dz) => ({ c: Math.cos(ang), s: Math.sin(ang), off, dax, day, dz });

  function Geo(w, h) {
    this.w = w; this.h = h; const N = w * h;
    this.f = new Int32Array(N); this.zb = new Float32Array(N).fill(-1e9);
    this.wx = new Float32Array(N); this.wy = new Float32Array(N); this.wz = new Float32Array(N); this.cz = new Float32Array(N);
    this.nx = new Float32Array(N); this.ny = new Float32Array(N); this.nz = new Float32Array(N);
    this.asp = new Int8Array(N); this.cap = new Uint8Array(N); this.und = new Uint8Array(N);
    this.col = new Int16Array(N).fill(-1); this._prev = new Int32Array(w); this._row = new Int32Array(w);
  }

  /* front-to-back heightfield sweep. Each plan sample paints its top texel and then the wall beneath
     it, stopped by the nearer samples already standing in that column — so cost is the plan area, not
     the area times the height. The coverage record is merged once per depth row, never mid-sample:
     updating it as the wall descends made every sheared mass stop after one texel and left a comb of
     holes for the far facets to show through. The z-buffer key is view-axis depth. */
  function rasterMass(geo, B, bi, cxp, baseY, y0) {
    const pl = B.pl, rm = pl.rmax + 1, stp = .5, W = geo.w, Hh = geo.h, BIG = 1 << 28;
    const Hm = B.zt, tp = Math.max(.02, B.taper), shx = B.shx || 0, zg = B.zg == null ? 0 : B.zg;
    const prev = geo._prev, row = geo._row; prev.fill(BIG);
    for (let Yd = -rm * pl.sq; Yd <= rm * pl.sq + stp; Yd += stp) {
      row.fill(BIG);
      for (let X = -rm; X <= rm; X += stp) {
        const hit = planAt(pl, X, Yd); if (!hit) continue;
        let ax = B.ax || 0, ay = B.ay || 0, dz = 0, capId = 0;
        for (let k = 0; k < B.splits.length; k++) {
          const s = B.splits[k];
          if (X * s.c + Yd * s.s > s.off) { ax += s.dax || 0; ay += s.day || 0; dz += s.dz || 0; capId = k + 1; }
        }
        const capZ = B.z0 + Hm + dz + ax * X + ay * Yd;
        const sideZ = B.z0 + Hm * Math.min(1, (1 - hit.t) / tp);
        const isCap = capZ < sideZ ? 1 : 0, zTop = isCap ? capZ : sideZ;
        if (zTop <= B.z0 + .02) continue;
        let nwx, nwy, nwz, lf;
        if (isCap) { nwx = -ax; nwy = -ay; nwz = 1; lf = 1 + capId; }
        else { const E = pl.E[hit.e]; nwx = E.nx; nwy = E.ny; nwz = tp * hit.rb / Math.max(1, Hm); lf = 8 + hit.e; }
        nwz -= shx * nwx;
        const ln = Math.hypot(nwx, nwy, nwz) || 1; nwx /= ln; nwy /= ln; nwz /= ln;
        const vnx = nwx, vny = -(nwy * Q + nwz * HZ), vnz = nwy * (-HZ) + nwz * Q;
        const nl = vnx * P.LIGHT.key[0] + vny * P.LIGHT.key[1] + vnz * P.LIGHT.key[2];
        /* flat tier per facet, the cliff kit's rule: the quantised compass aspect on a side facet
           (W/SW +1 · S 0 · SE/E −1) and a brightness tier on a cap. The albedo's key pass is left to
           the MARK relief only — letting it step whole facets as well double-counted the light and
           dropped every south-facing wall two bands into mud. */
        const asp = isCap ? (nl > .80 ? 2 : nl > .55 ? 1 : 0) : clamp(Math.round(-vnx * 1.35), -1, 1);
        const fid = bi * 64 + lf, Yw = B.cy + Yd, gRow = baseY - Q * (Yw + y0);
        const colId = B.cols ? Math.floor((X + pl.rmax) / (2 * pl.rmax) * B.cols) : -1;
        const capRef = B.z0 + Hm + dz;
        const wx = B.cx + X;
        const paint = (sx, sy, z, cap, und) => {
          if (sx < 0 || sy < 0 || sx >= W || sy >= Hh) return;
          const i = sy * W + sx, key = Q * z - HZ * Yw;
          if (geo.f[i] && geo.zb[i] >= key) return;
          geo.f[i] = fid; geo.zb[i] = key; geo.wx[i] = wx; geo.wy[i] = Yw; geo.wz[i] = z;
          geo.cz[i] = cap ? capRef : z; geo.cap[i] = cap; geo.und[i] = und || 0;
          geo.asp[i] = und ? -1 : asp; geo.col[i] = colId;
          geo.nx[i] = vnx; geo.ny[i] = vny; geo.nz[i] = vnz;
        };
        const syTop = Math.round(gRow - HZ * zTop), sxTop = Math.round(cxp + wx + shx * (zTop - B.z0));
        paint(sxTop, syTop, zTop, isCap, 0);
        if (sxTop >= 0 && sxTop < W && syTop < row[sxTop]) row[sxTop] = syTop;
        const syBot = Math.round(gRow - HZ * zg);
        let sxLast = sxTop;
        for (let sy = syTop + 1; sy <= syBot; sy++) {
          const z = (gRow - sy) / HZ, sx = Math.round(cxp + wx + shx * (z - B.z0));
          if (sx < 0 || sx >= W) continue;
          if (sy >= prev[sx]) break;
          paint(sx, sy, z, 0, 0); sxLast = sx;
          if (sy < row[sx]) row[sx] = sy;
        }
        for (let k = 1; k <= (B.under || 0); k++) {                       // an overhang's own shadow
          const sy = syBot + k; if (sy >= Hh) break;
          if (sxLast < 0 || sxLast >= W || sy >= prev[sxLast]) break;
          paint(sxLast, sy, zg - k / HZ, 0, 1);
          if (sy < row[sxLast]) row[sxLast] = sy;
        }
      }
      for (let i = 0; i < W; i++) if (row[i] < prev[i]) prev[i] = row[i];
    }
  }

  // ---- bed sequence -----------------------------------------------------------------------------
  /* PxCliff's rule keyed off world Z: massive units alternate with laminated packets over one
     periodic 32-texel metre, so brow, toe, platform and boulder slice the same geology. */
  function bedSeq(S, R) {
    const out = []; let z = 0, k = 0, unit = 0;
    const thin = S.thin || [3, 3], mass2 = S.massive || [6, 8];
    while (z < T) {
      const isM = unit % 2 === 0;
      let th = isM ? mass2[0] + Math.floor(hash2(k, 1, R) * mass2[1]) : thin[0] + Math.floor(hash2(k, 2, R) * thin[1]);
      if (T - z < 3) { out[out.length - 1].th += T - z; break; }
      if (T - z - th < 3) th = T - z;
      const r = hash2(k, 3, R);
      out.push({ z0: z, th, massive: isM, tone: isM ? (r < .22 ? 3 : 2) : (r < .40 ? 1 : 2), silt: !isM && hash2(k, 6, R) < (S.silt || 0) });
      z += th; k++; unit++;
    }
    const idx = new Uint8Array(T);
    for (let i = 0; i < out.length; i++) for (let y = out[i].z0; y < out[i].z0 + out[i].th && y < T; y++) idx[y] = i;
    return { beds: out, idx };
  }

  // ---- the sixteen forms ------------------------------------------------------------------------
  const FORMS = {
    erratic: { name: 'Erratic', cell: { w: 56, h: 50 }, stone: 'granite', role: 'mass',
      note: 'Ice-dropped erratic. Blunt, not round: seven or eight facets under a heavy batter, the cap broken in two by one sheeting joint, one shoulder lobe on the bigger ones.',
      variants: [{ seed: 8101, sizeM: 1.05, turn: .3, n: 8 }, { seed: 8102, sizeM: 1.60, turn: 1.7, n: 9 },
        { seed: 8103, sizeM: .70, turn: 2.6, n: 7 }, { seed: 8104, sizeM: 2.15, turn: 4.4, n: 9 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, out = [];
        out.push(mass({ pl: mkPlan(p.n, r, { turn: p.turn, jit: .8, rough: .34, notch: 1, sq: .82 }, R), zt: r * (.56 + hash2(2, 1, R) * .22),
          taper: .14 + hash2(3, 1, R) * .10, ax: (hash2(4, 1, R) - .5) * .34, ay: (hash2(5, 1, R) - .5) * .20,
          splits: [split(1.1 + hash2(6, 1, R) * 1.4, -r * .1, .12, -.08, -r * .05)] }));
        if (p.sizeM > 1.2) { const rr = r * .34, a = 2.0 + hash2(7, 1, R) * 1.6;
          out.push(mass({ pl: mkPlan(6, rr, { turn: a, jit: .8, rough: .38, notch: 1, sq: .8 }, R + 9), cx: Math.cos(a) * r * .72, cy: Math.sin(a) * r * .5,
            zt: rr * (.85 + hash2(8, 1, R) * .4), taper: .40, ax: .18, ay: .1 })); }
        return out;
      } },

    block: { name: 'Fallen Block', cell: { w: 56, h: 54 }, stone: 'sandstone', role: 'mass',
      note: 'A block off the face, landed on a corner. Five or six sheer sides with a slight batter, one hard-tilted cap, a notch where the corner broke, the bedding running THROUGH it at the angle it came to rest. The piece that makes a talus read as talus.',
      variants: [{ seed: 8201, sizeM: 1.15, turn: .5, n: 6, tilt: .30 }, { seed: 8202, sizeM: 1.65, turn: 2.2, n: 5, tilt: .18 },
        { seed: 8203, sizeM: .85, turn: 3.6, n: 6, tilt: .40 }, { seed: 8204, sizeM: 2.10, turn: 5.1, n: 7, tilt: .12 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, a = p.turn * 1.7;
        return [mass({ pl: mkPlan(p.n, r, { turn: p.turn, jit: .95, rough: .40, notch: 2, sq: .76 }, R), zt: r * (.74 + hash2(2, 1, R) * .34),
          taper: .10 + hash2(3, 1, R) * .12, shx: (hash2(4, 1, R) - .5) * .22,
          ax: Math.cos(a) * p.tilt, ay: Math.sin(a) * p.tilt * .7,
          splits: [split(hash2(5, 1, R) * 3, r * .12, -.16, .10, 0)] })];
      } },

    perched: { name: 'Perched Block', cell: { w: 58, h: 62 }, stone: 'granite', role: 'mass',
      note: 'A block left standing on a smaller plinth as the ground washed out from under it — the one form in the kit with a real OVERHANG, with its underside baked dark. The landmark a player navigates by.',
      variants: [{ seed: 8301, sizeM: 1.3, turn: .4 }, { seed: 8302, sizeM: 1.8, turn: 2.1 },
        { seed: 8303, sizeM: 1.0, turn: 3.7 }, { seed: 8304, sizeM: 2.2, turn: 5.3 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, ph = r * (.34 + hash2(1, 1, R) * .18), out = [];
        out.push(mass({ pl: mkPlan(6, r * (.52 + hash2(2, 1, R) * .1), { turn: p.turn + 1, jit: .9, rough: .34, notch: 1, sq: .82 }, R + 3),
          zt: ph, taper: .30, ax: .1, ay: .06 }));
        out.push(mass({ pl: mkPlan(6, r, { turn: p.turn, jit: .9, rough: .36, notch: 2, sq: .80 }, R), z0: ph, zg: ph, under: 3,
          zt: r * (.60 + hash2(4, 1, R) * .28), taper: .16, shx: (hash2(5, 1, R) - .5) * .3,
          ax: (hash2(6, 1, R) - .5) * .5, ay: -.12, splits: [split(2.2, 0, .3, -.1, -r * .05)] }));
        return out;
      } },

    slab: { name: 'Tabular Slab', cell: { w: 74, h: 42 }, stone: 'sandstone', role: 'flat',
      note: 'A plate off a bedding plane: four to six long straight edges, one corner lifted on a chock so it is not a lid on the floor, standable, the bed edges the whole read on the rim.',
      variants: [{ seed: 8401, sizeM: 1.8, turn: .2, n: 5 }, { seed: 8402, sizeM: 2.4, turn: 1.2, n: 6 },
        { seed: 8403, sizeM: 1.3, turn: 2.9, n: 4 }, { seed: 8404, sizeM: 2.1, turn: 4.8, n: 5 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, out = [];
        out.push(mass({ pl: mkPlan(p.n, r, { turn: p.turn, jit: .6, rough: .30, notch: 1, sq: .60 }, R), zt: r * (.15 + hash2(1, 1, R) * .08),
          taper: .12, ax: .09 + hash2(2, 1, R) * .06, ay: .05, splits: [split(.6 + hash2(3, 1, R) * 2, -r * .2, -.12, .08, 0)] }));
        if (hash2(4, 1, R) < .75) { const rr = r * (.40 + hash2(5, 1, R) * .2);
          out.push(mass({ pl: mkPlan(5, rr, { turn: p.turn + 1.1, jit: .9, rough: .42, notch: 1, sq: .58 }, R + 17),
            cx: r * (hash2(6, 1, R) - .5) * .8, cy: -r * .12, z0: r * .13, zg: r * .1, zt: rr * .30, taper: .2, ax: -.14, ay: .07 })); }
        return out;
      } },

    fin: { name: 'Fin', cell: { w: 46, h: 66 }, stone: 'quartzite', role: 'mass',
      note: 'A blade of rock standing on edge and leaning off the vertical — narrow in plan, tall, sheer. The strongest vertical read in the kit and the only one that casts a long silhouette; scatter sparingly or the coast reads as Stonehenge.',
      variants: [{ seed: 8501, sizeM: 1.2, turn: .5, lean: .26 }, { seed: 8502, sizeM: 1.7, turn: 2.3, lean: -.20 },
        { seed: 8503, sizeM: .95, turn: 3.9, lean: .34 }, { seed: 8504, sizeM: 2.1, turn: 5.4, lean: -.14 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, out = [];
        out.push(mass({ pl: mkPlan(5, r, { turn: p.turn, jit: .8, rough: .34, notch: 1, sq: .34 }, R),
          zt: r * (1.25 + hash2(1, 1, R) * .55), taper: .34, shx: p.lean, ax: (hash2(2, 1, R) - .5) * .8, ay: .2,
          splits: [split(1.7, 0, .5, -.2, -r * .12)] }));
        if (hash2(3, 1, R) < .8) { const rr = 2.5 + hash2(4, 1, R) * 3;
          out.push(mass({ pl: mkPlan(5, rr, { turn: hash2(5, 1, R) * 3, jit: 1, rough: .5, notch: 1, sq: .7 }, R + 21),
            cx: (hash2(6, 1, R) - .5) * r * 1.2, cy: -r * .2, zt: rr * .7, taper: .3, ax: .3, ay: .15 })); }
        return out;
      } },

    wedge: { name: 'Wedge', cell: { w: 54, h: 56 }, stone: 'quartzite', role: 'mass',
      note: 'A leaning prism: one sheer face into the key, one long ramp away from it. Reads at the smallest size in the kit because the cap is a single steep plane and the arris runs the whole height.',
      variants: [{ seed: 8601, sizeM: 1.20, turn: .4, n: 5, tilt: .80 }, { seed: 8602, sizeM: 1.70, turn: 2.4, n: 4, tilt: .66 },
        { seed: 8603, sizeM: .90, turn: 3.9, n: 5, tilt: .92 }, { seed: 8604, sizeM: 2.20, turn: 5.4, n: 6, tilt: .58 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, a = p.turn;
        return [mass({ pl: mkPlan(p.n, r, { turn: p.turn, jit: .85, rough: .36, notch: 1, sq: .72 }, R),
          zt: r * (1.05 + hash2(2, 1, R) * .40), taper: .16, shx: (hash2(3, 1, R) - .5) * .18,
          ax: Math.cos(a) * p.tilt, ay: Math.sin(a) * p.tilt * .55 })];
      } },

    cloven: { name: 'Cloven Stack', cell: { w: 62, h: 86 }, stone: 'sandstone', role: 'mass',
      note: 'Landmark scale: one mass split down a joint, the halves sheer on the inside and battered on the outside, rubble at the foot. The cleft is a real gap between two masses, never a painted line.',
      variants: [{ seed: 8701, sizeM: 1.6, turn: .3, gap: 2.2 }, { seed: 8702, sizeM: 2.1, turn: 1.9, gap: 3.4 },
        { seed: 8703, sizeM: 1.3, turn: 3.3, gap: 1.6 }, { seed: 8704, sizeM: 2.5, turn: 5.0, gap: 4.2 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, Hm = r * (1.15 + hash2(1, 1, R) * .45), out = [];
        out.push(mass({ pl: mkPlan(5, r * .62, { turn: p.turn, jit: .7, rough: .28, notch: 1, sq: .84 }, R),
          cx: -r * .32 - p.gap / 2, cy: 1.4, zt: Hm, taper: .28, shx: -.08, ax: .18, ay: .07, splits: [split(2.4, 0, .3, -.1, -r * .08)] }));
        out.push(mass({ pl: mkPlan(5, r * .54, { turn: p.turn + 1.3, jit: .7, rough: .32, notch: 1, sq: .80 }, R + 31),
          cx: r * .30 + p.gap / 2, cy: -1.4, zt: Hm * (.60 + hash2(2, 1, R) * .20), taper: .32, shx: .12, ax: -.26, ay: .06 }));
        for (let i = 0; i < 3; i++) { const a = hash2(i, 5, R) * TAU, rr = 2.2 + hash2(i, 6, R) * 2.8;
          out.push(mass({ pl: mkPlan(5, rr, { turn: a, jit: 1, rough: .5, notch: 1, sq: .74 }, R + 40 + i),
            cx: Math.cos(a) * r * .9, cy: Math.abs(Math.sin(a)) * r * .5 - r * .22, zt: rr * .8, taper: .3, ax: .3, ay: .16 })); }
        return out;
      } },

    knuckle: { name: 'Bedrock Knuckle', cell: { w: 70, h: 38 }, stone: 'sandstone', role: 'seam',
      note: 'THE TRANSITION PIECE. Bedrock breaking through turf or sand: a low broad plate that rises a bed or two and dies back into the ground at its edges. Scatter these along a material boundary with their skirts and the ground stops having a join.',
      variants: [{ seed: 8801, sizeM: 2.2, turn: .6, n: 8 }, { seed: 8802, sizeM: 1.5, turn: 2.0, n: 7 },
        { seed: 8803, sizeM: 2.9, turn: 3.4, n: 9 }, { seed: 8804, sizeM: 1.1, turn: 5.2, n: 6 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, out = [];
        out.push(mass({ pl: mkPlan(p.n, r, { turn: p.turn, jit: 1, rough: .42, notch: 3, sq: .54 }, R),
          zt: r * (.15 + hash2(1, 1, R) * .07), taper: .30, ax: .10, ay: .06,
          splits: [split(.9, -r * .3, -.1, .06, 0), split(2.6, r * .25, .12, -.06, 0)] }));
        const nb = 2;
        for (let i = 0; i < nb; i++) { const a = 1.2 + hash2(i, 7, R) * 3.4, rr = r * (.30 + hash2(i, 8, R) * .18);
          out.push(mass({ pl: mkPlan(5, rr, { turn: a + 1, jit: 1, rough: .44, notch: 1, sq: .56 }, R + 50 + i),
            cx: Math.cos(a) * r * .36, cy: Math.sin(a) * r * .20, z0: r * .10, zg: r * .06,
            zt: rr * (.40 + hash2(i, 9, R) * .26), taper: .38, ax: .16, ay: .09 })); }
        return out;
      } },

    bench: { name: 'Bedrock Bench', cell: { w: 98, h: 46 }, stone: 'sandstone', role: 'seam',
      note: 'A two-course bedrock step, 2–3 m long, straight enough to lay along a level change and hide it: a flat standable top, one lit bench lip, a shadowed riser, and its ends broken so it never reads as masonry.',
      variants: [{ seed: 8901, sizeM: 2.6, turn: .1, n: 5 }, { seed: 8902, sizeM: 3.0, turn: 1.6, n: 6 },
        { seed: 8903, sizeM: 2.0, turn: 3.0, n: 5 }, { seed: 8904, sizeM: 2.8, turn: 4.5, n: 6 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, out = [];
        const low = mass({ pl: mkPlan(p.n, r, { turn: p.turn, jit: .5, rough: .22, notch: 1, sq: .46 }, R),
          zt: r * (.14 + hash2(1, 1, R) * .05), taper: .12, ax: .05, ay: .03 });
        out.push(low);
        out.push(mass({ pl: mkPlan(p.n, r * (.62 + hash2(2, 1, R) * .12), { turn: p.turn + .4, jit: .6, rough: .26, notch: 2, sq: .44 }, R + 7),
          cx: -r * (.10 + hash2(3, 1, R) * .16), cy: r * .10, z0: low.zt, zg: low.zt * .7,
          zt: r * (.16 + hash2(4, 1, R) * .07), taper: .14, ax: .06, ay: .04, splits: [split(1.4, 0, -.08, .05, 0)] }));
        return out;
      } },

    shelf: { name: 'Pool Shelf', cell: { w: 82, h: 46 }, stone: 'sandstone', role: 'flat',
      note: 'A wave-cut shelf with a basin cut into the cap. The basin is an empty damp hollow — a rim lip, a lit inner wall on the key-away side, two bands of shade — and the rect ships in the sidecar, so the shader still owns every drop of water.',
      variants: [{ seed: 9001, sizeM: 2.2, turn: .3, n: 6 }, { seed: 9002, sizeM: 2.8, turn: 1.6, n: 7 },
        { seed: 9003, sizeM: 1.7, turn: 3.1, n: 5 }, { seed: 9004, sizeM: 2.5, turn: 4.6, n: 6 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, out = [];
        const plate = mass({ pl: mkPlan(p.n, r, { turn: p.turn, jit: .7, rough: .30, notch: 2, sq: .56 }, R),
          zt: r * (.20 + hash2(1, 1, R) * .07), taper: .16, ax: .05, ay: .03 });
        out.push(plate);
        for (let i = 0; i < 2; i++) { const a = 2.4 + i * 2.1 + hash2(i, 3, R), rr = r * (.20 + hash2(i, 4, R) * .12);
          out.push(mass({ pl: mkPlan(5, rr, { turn: a, jit: .9, rough: .40, notch: 1, sq: .60 }, R + 60 + i),
            cx: Math.cos(a) * r * .70, cy: Math.sin(a) * r * .40, z0: plate.zt * .55, zg: plate.zt * .4,
            zt: rr * .7, taper: .28, ax: -.16, ay: .08 })); }
        out.pool = { x: (hash2(9, 1, R) - .5) * r * .3, y: (hash2(9, 2, R) - .5) * r * .2,
          rx: r * (.26 + hash2(9, 3, R) * .08), ry: r * .17, depth: plate.zt * .7 };
        return out;
      } },

    prisms: { name: 'Columnar Cluster', cell: { w: 66, h: 62 }, stone: 'basalt', role: 'mass',
      note: 'Broken colonnade: four to seven prisms at their own heights, each a flat cap and its own tone, barely battered. The form basalt should always have had.',
      variants: [{ seed: 9101, sizeM: 1.5, turn: .4, n: 5 }, { seed: 9102, sizeM: 2.0, turn: 1.8, n: 7 },
        { seed: 9103, sizeM: 1.1, turn: 3.2, n: 4 }, { seed: 9104, sizeM: 2.4, turn: 5.0, n: 6 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, out = [], n = p.n;
        for (let i = 0; i < n; i++) {
          const a = p.turn + i / n * TAU + (hash2(i, 1, R) - .5) * .7, d = r * (.16 + hash2(i, 2, R) * .60);
          const rr = r * (.22 + hash2(i, 3, R) * .12);
          out.push(mass({ pl: mkPlan(6, rr, { turn: a * 1.3, jit: .35, rough: .16, sq: .84 }, R + 70 + i),
            cx: Math.cos(a) * d, cy: Math.sin(a) * d * .62, zt: r * (.50 + hash2(i, 4, R) * 1.15),
            taper: .06, cols: 3, ax: (hash2(i, 5, R) - .5) * .20, ay: (hash2(i, 6, R) - .5) * .16 }));
        }
        return out;
      } },

    scree: { name: 'Scree Pile', cell: { w: 78, h: 46 }, stone: 'quartzite', role: 'heap',
      note: 'Angular rubble heaped at the angle of repose: a low mound of fines with six to twelve plates leaning on it and on each other, sharp edges, gaps you can see into. Not a cobble scatter with the roundness turned down.',
      variants: [{ seed: 9201, sizeM: 1.6, turn: .3, n: 8 }, { seed: 9202, sizeM: 2.2, turn: 1.7, n: 12 },
        { seed: 9203, sizeM: 1.1, turn: 3.0, n: 6 }, { seed: 9204, sizeM: 2.6, turn: 4.9, n: 10 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, out = [], n = p.n;
        out.push(mass({ pl: mkPlan(7, r * .86, { turn: p.turn + .5, jit: 1, rough: .40, notch: 2, sq: .52 }, R + 5),
          zt: r * .22, taper: .62, ax: .04, ay: .03 }));
        for (let i = 0; i < n; i++) {
          const u = hash2(i, 1, R), v = hash2(i, 2, R), d = r * (.05 + u * .72);
          const a = p.turn + i * 2.399, rr = 2.6 + v * (r * .26);
          out.push(mass({ pl: mkPlan(4 + (i & 1), rr, { turn: a * 1.7, jit: 1.1, rough: .46, notch: 1, sq: .64 }, R + 80 + i),
            cx: Math.cos(a) * d, cy: Math.sin(a) * d * .55, z0: Math.max(0, r * .18 - d * .16), zg: 0,
            zt: rr * (.52 + hash2(i, 3, R) * .5) * (1 - d / (r * 1.8) * .4), taper: .14,
            shx: (hash2(i, 6, R) - .5) * .5, ax: (hash2(i, 4, R) - .5) * .9, ay: (hash2(i, 5, R) - .5) * .7 }));
        }
        return out;
      } },

    cobbles: { name: 'Cobble Cluster', cell: { w: 58, h: 32 }, stone: 'granite', role: 'heap',
      note: 'Storm cobbles, the filler. Heavily battered five-sided stones at three calibres — two or three tones each, which is all a 6 px stone can carry — with the biggest one riding up on the others.',
      variants: [{ seed: 9301, sizeM: 1.0, turn: .2, n: 7 }, { seed: 9302, sizeM: 1.4, turn: 1.5, n: 11 },
        { seed: 9303, sizeM: .7, turn: 3.1, n: 5 }, { seed: 9304, sizeM: 1.8, turn: 4.7, n: 14 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, out = [], n = p.n;
        for (let i = 0; i < n; i++) {
          const a = p.turn + i * 2.399, d = r * Math.sqrt(hash2(i, 1, R)) * .68;
          const big = i === 0 || hash2(i, 7, R) < .18, rr = (big ? 3.8 : 2.2) + hash2(i, 2, R) * (big ? 3.4 : 2.2);
          out.push(mass({ pl: mkPlan(5 + (i & 1), rr, { turn: a, jit: .9, rough: .36, notch: 1, sq: .74 }, R + 90 + i),
            cx: Math.cos(a) * d, cy: Math.sin(a) * d * .5, z0: big && i === 0 ? 1.2 : 0,
            zt: rr * (.56 + hash2(i, 3, R) * .26), taper: .44,
            ax: (hash2(i, 4, R) - .5) * .5, ay: (hash2(i, 5, R) - .5) * .4 }));
        }
        return out;
      } },

    skerry: { name: 'Skerry', cell: { w: 102, h: 46 }, stone: 'basalt', role: 'water',
      note: 'Awash hazard: two to four heads breaking an IRREGULAR sea plane, so the waterline wanders with the swell instead of guillotining the rock at one screen row. Ships a hazard radius, not a collision mesh.',
      variants: [{ seed: 9401, sizeM: 2.2, turn: .4, n: 3, wl: .30 }, { seed: 9402, sizeM: 3.0, turn: 1.9, n: 4, wl: .42 },
        { seed: 9403, sizeM: 1.6, turn: 3.4, n: 2, wl: .22 }, { seed: 9404, sizeM: 2.6, turn: 5.1, n: 3, wl: .50 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, out = [], n = p.n;
        out.push(mass({ pl: mkPlan(7, r * .95, { turn: p.turn + 1, jit: 1, rough: .34, notch: 2, sq: .5 }, R + 7),
          zt: r * (.34 + hash2(8, 1, R) * .2), taper: .55, ax: .05, ay: .04 }));        // the awash reef the heads stand on
        for (let i = 0; i < n; i++) {
          const tt = n === 1 ? 0 : i / (n - 1) - .5, rr = r * (.26 + hash2(i, 1, R) * .20);
          out.push(mass({ pl: mkPlan(5 + (i & 1), rr, { turn: p.turn + i * 1.7, jit: .9, rough: .38, notch: 1, sq: .66 }, R + 100 + i),
            cx: tt * r * 1.25 + (hash2(i, 2, R) - .5) * r * .18, cy: (hash2(i, 3, R) - .5) * r * .40,
            zt: rr * (1.0 + hash2(i, 4, R) * .9), taper: .26, shx: (hash2(i, 7, R) - .5) * .3,
            ax: (hash2(i, 5, R) - .5) * .6, ay: (hash2(i, 6, R) - .5) * .35 }));
        }
        return out;
      } },

    spine: { name: 'Bedrock Spine', cell: { w: 122, h: 42 }, stone: 'sandstone', role: 'seam',
      note: 'A 3–4 m rib of bedrock, three to five knuckles in a line at their own heights with soil between them. Lay it ALONG a boundary: one sprite covers four metres of seam and the eye reads a geological line instead of a texture join.',
      variants: [{ seed: 9501, sizeM: 3.4, turn: .2, n: 4 }, { seed: 9502, sizeM: 3.8, turn: 1.5, n: 5 },
        { seed: 9503, sizeM: 2.6, turn: 2.9, n: 3 }, { seed: 9504, sizeM: 3.6, turn: 4.4, n: 4 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, out = [], n = p.n;
        for (let i = 0; i < n; i++) {
          const tt = n === 1 ? 0 : i / (n - 1) - .5, rr = r * (.24 + hash2(i, 1, R) * .13);
          out.push(mass({ pl: mkPlan(5 + (i & 1), rr, { turn: p.turn + i * .9, jit: 1, rough: .40, notch: 2, sq: .58 }, R + 110 + i),
            cx: tt * r * 1.5 + (hash2(i, 2, R) - .5) * r * .10, cy: (hash2(i, 3, R) - .5) * r * .22,
            zt: rr * (.38 + hash2(i, 4, R) * .44), taper: .30, ax: .12 - hash2(i, 5, R) * .24, ay: .07 }));
        }
        return out;
      } },

    apron: { name: 'Talus Apron', cell: { w: 116, h: 58 }, stone: 'till', role: 'heap',
      note: 'What a cliff toe actually makes: a fan of fines spreading downslope with its coarse fraction at the head, thinning to single stones at the lip. Butt it against a cliff toe tile and the wall stops standing on the floor like a flat.',
      variants: [{ seed: 9601, sizeM: 3.0, turn: .3, n: 12 }, { seed: 9602, sizeM: 3.6, turn: 1.6, n: 16 },
        { seed: 9603, sizeM: 2.2, turn: 3.1, n: 9 }, { seed: 9604, sizeM: 3.4, turn: 4.7, n: 14 }],
      gen(p, S, R) {
        const r = p.sizeM * PPU / 2, out = [], n = p.n;
        out.push(mass({ pl: mkPlan(8, r, { turn: p.turn, jit: 1, rough: .34, notch: 2, sq: .44 }, R),
          cy: r * .10, zt: r * .26, taper: .72, ax: .06, ay: .16 }));
        for (let i = 0; i < n; i++) {
          const u = hash2(i, 1, R), v = hash2(i, 2, R), a = p.turn + i * 2.399;
          const dx = (u - .5) * r * 1.7, dy = -r * .3 + v * r * .62;
          const rr = 2.0 + (1 - v) * (r * .16) + hash2(i, 8, R) * 2;
          out.push(mass({ pl: mkPlan(4 + (i & 1), rr, { turn: a * 1.7, jit: 1.1, rough: .46, notch: 1, sq: .62 }, R + 120 + i),
            cx: dx, cy: dy, z0: Math.max(0, r * .18 * (1 - Math.abs(dx) / (r * .9)) - (v * r * .2)), zg: 0,
            zt: rr * (.45 + hash2(i, 3, R) * .45), taper: .18, shx: (hash2(i, 6, R) - .5) * .5,
            ax: (hash2(i, 4, R) - .5) * .8, ay: (hash2(i, 5, R) - .5) * .6 }));
        }
        return out;
      } },
  };

  // ---- fit --------------------------------------------------------------------------------------
  function fitMasses(masses, cell) {
    let minX = 1e9, maxX = -1e9, minY = 1e9, maxY = -1e9, maxZ = 0;
    for (const B of masses) {
      const rx = B.pl.rmax, ry = B.pl.rmax * B.pl.sq;
      const zT = B.z0 + B.zt + Math.abs(B.ax || 0) * rx + Math.abs(B.ay || 0) * ry;
      const sh = (B.shx || 0) * zT;
      minX = Math.min(minX, B.cx - rx + Math.min(0, sh)); maxX = Math.max(maxX, B.cx + rx + Math.max(0, sh));
      minY = Math.min(minY, B.cy - ry); maxY = Math.max(maxY, B.cy + ry);
      maxZ = Math.max(maxZ, zT);
    }
    const ox = (minX + maxX) / 2;
    for (const B of masses) B.cx -= ox;
    const k = Math.min(1, (cell.w - 2) / (maxX - minX + 1), (cell.h - 3) / (Q * (maxY - minY) + HZ * maxZ + 2));
    if (k < 1) for (const B of masses) {
      B.cx *= k; B.cy *= k; B.z0 *= k; B.zt *= k; if (B.zg) B.zg *= k; B.pl = scalePlan(B.pl, k);
      for (const s of B.splits) { s.off *= k; if (s.dz) s.dz *= k; }
    }
    if (k < 1 && masses.pool) { const q = masses.pool; q.x *= k; q.y *= k; q.rx *= k; q.ry *= k; q.depth *= k; }
    return { y0: -minY * k, k, topZ: maxZ * k };
  }
  function mirrorMasses(masses) {
    for (const B of masses) {
      B.cx = -B.cx; B.pl = flipPlan(B.pl); B.ax = -(B.ax || 0); B.shx = -(B.shx || 0);
      for (const s of B.splits) { s.c = -s.c; s.dax = -(s.dax || 0); }
    }
    if (masses.pool) masses.pool.x = -masses.pool.x;
  }

  // ---- stamps -----------------------------------------------------------------------------------
  const SP = {
    spall: [stencil(['.oo.', 'oooo', 'oooo', '.oo.']), stencil(['.ooo.', 'ooooo', '.ooo.']), stencil(['oo.', 'ooo', 'oo.'])],
    socket: [stencil(['oo', 'oo']), stencil(['.o.', 'ooo', '.o.']), stencil(['ooo', 'oo.'])],
    lich: [stencil(['.o.', 'ooo', '.o.']), stencil(['oo', '.o']), stencil(['.oo', 'oo.'])],
  };

  // ---- paint ------------------------------------------------------------------------------------
  function buildTile(geo, S, p, R, tide, dress, bs, topZ, pool, cxp, baseY, y0) {
    const W = geo.w, H = geo.h, t = new Tile(W, H), beds = bs.beds, idx = bs.idx;
    const rockBase = tide === 'dry' ? S.base : tide === 'wet' ? mix(mix(S.base, COLD, .26), '#000000', .08) : mix(mix(S.base, COLD, .20), SHALLOW, .14);
    const pal = {
      rock: t.addPal(rockBase, S.c),
      silt: t.addPal(mix('#8e8c78', rockBase, .40), .9),
      lich: t.addPal(S.lichen || '#97a08b', .7),
      weed: t.addPal(tide === 'dry' ? '#4a5628' : '#5d6a2f', .8),
      barn: t.addPal('#cfc3ad', .8),
      wet: t.addPal(mix(rockBase, SEA, .46), .95),
      accent: t.addPal(S.fleck || S.clast || S.vein || rockBase, .9),
    };
    t.a.fill(0); t.pal.fill(pal.rock); t.band.fill(2); t.h.fill(0);
    const shift = new Int8Array(W * H);
    const tideZ = Math.max(3, topZ * (p.wl ? p.wl : .34));
    const waterZ = p.wl ? topZ * p.wl : 0;

    /* 1 — bed tone. A SIDE facet slices the bed sequence texel by texel and gets the lit lip and
       the shadowed recess at a unit boundary; a CAP reads the sequence ONCE at its own height, so
       it stays one flat plane instead of drawing a contour ring across itself. */
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x; if (!geo.f[i]) continue;
      t.a[i] = 255; shift[i] = geo.asp[i];
      if (geo.und[i]) { t.band[i] = 0; t.h[i] = -1; t.lock[i] = 1; continue; }
      const z = geo.cz[i], zi = ((Math.round(z) % T) + T) % T, zu = ((Math.round(z) + 1) % T + T) % T;
      const b = beds[idx[zi]], bA = beds[idx[zu]], bw = S.bed;
      let band = b.massive ? (bw > .5 ? b.tone : 2) : (bw > .5 ? b.tone : 2 - (bw > .2 ? 1 : 0));
      let pl0 = (b.silt && !geo.cap[i]) ? pal.silt : pal.rock, h = 0, lock = 0;
      if (!geo.cap[i] && bw > .12 && bA !== b) {
        if (b.massive && !bA.massive) { if (hash2(x, idx[zi], R + 13) < .72) { band += 1; h += .45; lock = 1; } }
        else if (!b.massive && bA.massive) { band = Math.max(0, band - 2); h -= .5; lock = 1; }
        else if (hash2(x >> 1, idx[zi], R + 14) < .5) band -= 1;
      } else if (!geo.cap[i] && !b.massive && bw > .3 && (Math.round(z) - b.z0) % 2 === 1 && hash2(x, y, R + 17) < .55) band -= 1;
      if (S.columnar && geo.col[i] >= 0) {                                // identity on the column
        band += hash2(geo.col[i], geo.f[i] >> 6, R + 25) < .38 ? 1 : hash2(geo.col[i], 3, R + 26) < .34 ? -1 : 0;
        if (geo.col[i] !== (x > 0 ? geo.col[i - 1] : -9) && !geo.cap[i]) band -= 1;
      }
      t.pal[i] = pl0; t.band[i] = clamp(band, 0, 4); t.h[i] = h; if (lock) t.lock[i] = 1;
    }
    /* 2 — the arris, and the lit cap lip. Where two facets meet along the screen: one lit texel on
       the upper side, one dark on the lower — the single column that says a plane has turned. */
    const fAt = (x, y) => (x < 0 || y < 0 || x >= W || y >= H) ? 0 : geo.f[y * W + x];
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x, f = geo.f[i]; if (!f || t.lock[i]) continue;
      const up = fAt(x, y - 1), lf = fAt(x - 1, y);
      if (geo.cap[i] && (!up || !lf)) { t.band[i] = clamp(t.band[i] + 1, 0, 4); t.h[i] += .5; t.lock[i] = 1; continue; }
      if (up && up !== f) { t.band[i] = clamp(t.band[i] + (geo.cap[i] ? -1 : 1), 0, 4); t.h[i] += geo.cap[i] ? -.3 : .35; }
      else if (lf && lf !== f && geo.asp[i] <= geo.asp[i - 1]) t.band[i] = clamp(t.band[i] - 1, 0, 4);
    }
    /* 3 — joints: gated vertical traces on the side facets only, at the bed's own spacing */
    for (let y = 0; y < H; y++) for (let x = 1; x < W; x++) {
      const i = y * W + x; if (!geo.f[i] || geo.cap[i] || t.lock[i]) continue;
      const zi = ((Math.round(geo.wz[i]) % T) + T) % T, b = beds[idx[zi]];
      if (!b.massive && hash2(idx[zi], 5, R + 20) > .3) continue;         // joints run through the massive units
      const jsp = (b.massive ? S.jsp[0] : S.jsp[0] * .6) + hash2(idx[zi], 1, R) * S.jsp[1];
      const fo = (geo.f[i] & 63) * 3.1;
      if (Math.floor((x + fo) / jsp) !== Math.floor((x - 1 + fo) / jsp) && hash2(Math.floor((x + fo) / jsp), idx[zi], R + 21) < .30) {
        t.band[i] = clamp(t.band[i] - 2, 0, 4); t.h[i] -= .4;
      }
    }
    /* 3b — sheeting joints: on an unbedded stone the only line is the edge of an exfoliation shell —
       a contour at one height with a lit lip under it. Without these, granite is a blank plane. */
    if (S.sheet) {
      const lv = []; for (let k = 0; k < S.sheet; k++) lv.push(topZ * (.32 + .44 * hash2(k, 3, R + 90)));
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        const i = y * W + x; if (!geo.f[i] || geo.und[i] || t.lock[i]) continue;
        for (const L of lv) {
          const d = geo.wz[i] - L + (hash2(x, Math.round(L), R + 93) - .5) * 1.2;
          if (d > -.8 && d <= .8) { t.band[i] = clamp(t.band[i] - 2, 0, 4); t.h[i] -= .5; t.lock[i] = 1; }
          else if (d > .8 && d < 2.1 && hash2(x, 1, R + 92) < .7) { t.band[i] = clamp(t.band[i] + 1, 0, 4); t.h[i] += .3; }
        }
      }
    }
    /* 4 — marks: spalls proud, sockets plucked, chipped arrises, lichen on the dry caps, veins on
       the breaks, fleck and clasts in the matrix. */
    const inRock = (x, y) => x >= 0 && y >= 0 && x < W && y < H && geo.f[y * W + x] > 0 && !geo.und[y * W + x];
    for (const s of sites(W, H, 9, .9, R + 50)) {
      const i = s.y * W + s.x; if (!geo.f[i] || geo.und[i] || t.lock[i]) continue;
      const zi = ((Math.round(geo.wz[i]) % T) + T) % T, b = beds[idx[zi]];
      if (b.massive && s.r < S.spall * (geo.cap[i] ? .35 : .6)) {
        t.beginMark(); stamp(t, SP.spall[Math.floor(s.r2 * SP.spall.length)], s.x, s.y,
          { pal: pal.rock, band: clamp(t.band[i] + 1, 0, 4), seam: 1, tip: 1, h: 1.5, hBase: .8, lock: 1, clip: inRock }); t.endMark();
      } else if (!b.massive && !geo.cap[i] && s.r2 < S.socket * .55 && fbmP(s.u, s.v, 3, 3, R + 51, 2) > .54) {
        t.beginMark(); stamp(t, SP.socket[Math.floor(s.r * SP.socket.length)], s.x, s.y,
          { pal: pal.rock, band: 1, seam: -1, tip: 0, h: -1.2, lock: 1, clip: inRock }); t.endMark();
        if (inRock(s.x, s.y + 1)) t.set(s.x, s.y + 1, pal.rock, 3, -.9, 1);
      }
    }
    /* chipped arris: two or three texels of fresh break where a vertical edge was knocked off */
    for (const s of sites(W, H, 7, .95, R + 55)) {
      const i = s.y * W + s.x; if (!geo.f[i] || geo.und[i] || s.r > S.chip * .5) continue;
      const nf = fAt(s.x + 1, s.y);
      if (!nf || nf === geo.f[i] || s.r2 > .55) continue;
      t.beginMark();
      for (let k = 0; k < 2 + Math.round(s.r2); k++) { const y = s.y + k; if (!inRock(s.x, y)) break;
        t.set(s.x, y, pal.rock, clamp(t.band[i] + 2, 0, 4), .8, 1);
        if (inRock(s.x + 1, y)) t.set(s.x + 1, y, pal.rock, 1, .2, 1); }
      t.endMark();
    }
    /* the quartz vein: ONE sparse trace down the facets that carry one, not a bright texel at every
       facet break — that was pass two's speckle. */
    /* 4b — the cap treatment. A big flat cap is the one place this rig can still die: 50 × 20
       texels of one band is a table top, not a bedding plane. So a cap gets what a plucked bedding
       plane has — shallow slab scars, one band down, the upper-left rim darker and the lower-right
       inner lip lit — plus the odd spall standing proud of it. */
    for (const s of sites(W, H, 11, .95, R + 44)) {
      const i = s.y * W + s.x; if (!geo.f[i] || !geo.cap[i] || t.lock[i] || s.r > .62) continue;
      const w = 4 + Math.round(s.r2 * 5), hh = Math.max(2, Math.round(w * .5)), st2 = ellipse(w, hh);
      t.beginMark();
      for (const q of st2.px) {
        const x = s.x + q[0] - st2.ox, y = s.y + q[1] - st2.oy;
        if (!inRock(x, y) || !geo.cap[y * W + x]) continue;
        const j = y * W + x; if (t.lock[j]) continue;
        const up = q[1] === 0 || q[0] === 0, lowRim = q[1] === st2.h - 1;
        t.pal[j] = pal.rock; t.band[j] = clamp(t.band[j] + (up ? -2 : lowRim ? 1 : -1), 0, 4);
        t.h[j] = (up ? -1.4 : lowRim ? -.3 : -.9); t.lock[j] = 1; t.mark[j] = t.mid;
      }
      t.endMark();
    }
    if (S.vein) for (let y = 0; y < H; y++) for (let x = 1; x < W; x++) {
      const i = y * W + x, f = geo.f[i]; if (!f || geo.und[i] || t.pal[i] !== pal.rock || t.lock[i]) continue;
      if (f === geo.f[i - 1] || hash2(f, 1, R + 31) > .34) continue;
      if ((y + (f & 7) * 3) % 9 > 1) continue;
      t.pal[i] = pal.accent; t.band[i] = 3; t.lock[i] = 1;
      if (inRock(x + 1, y) && hash2(x, y, R + 32) < .6) { const j = i + 1; t.pal[j] = pal.accent; t.band[j] = 2; t.lock[j] = 1; }
    }
    if (S.fleck) for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x; if (!geo.f[i] || geo.und[i] || t.lock[i] || t.band[i] < 2) continue;
      if (hash2(x, y, R + 33) < .014) { t.pal[i] = pal.accent; t.band[i] = 3; }
    }
    if (S.clast) for (const s of sites(W, H, 7, .9, R + 36)) {
      const i = s.y * W + s.x; if (!geo.f[i] || geo.und[i] || s.r > .42) continue;
      t.beginMark(); stamp(t, ellipse(2 + Math.round(s.r2 * 2), 2), s.x, s.y,
        { pal: pal.accent, band: 2, seam: 1, tip: 1, h: .9, hBase: .3, lock: 1, clip: inRock }); t.endMark();
    }
    if (tide === 'dry') for (const s of sites(W, H, 6, .95, R + 40)) {
      const i = s.y * W + s.x; if (!geo.f[i] || geo.und[i] || t.lock[i]) continue;
      const ok = geo.cap[i] || (S.columnar && geo.asp[i] > 0);
      if (!ok || fbmP(s.u, s.v, 4, 4, R + 41, 2) < .56 || s.r > S.lich * 3) continue;
      t.beginMark(); stamp(t, SP.lich[Math.floor(s.r2 * SP.lich.length)], s.x, s.y,
        { pal: pal.lich, band: s.r2 > .6 ? 3 : 2, seam: 0, tip: 0, h: .2, clip: (x, y) => inRock(x, y) && !t.lock[t.i(x, y)] }); t.endMark();
    }
    /* 5 — the tide band: barnacle crust as clustered marks, rockweed as fronds off the line */
    if (dress === 1 || dress === 2) {
      const lo = tideZ * .70, hi = tideZ * 1.55;
      if (dress === 1) for (const s of sites(W, H, 4, .95, R + 60)) {
        const i = s.y * W + s.x; if (!geo.f[i] || geo.und[i] || t.lock[i]) continue;
        const z = geo.wz[i]; if (z < lo || z > hi) continue;
        if (fbmP(s.u, s.v, 7, 7, R + 61, 2) < .48) continue;
        t.beginMark(); stamp(t, hash2(s.x, s.y, R + 62) < .5 ? SP.lich[1] : SP.lich[0], s.x, s.y,
          { pal: pal.barn, band: s.r > .6 ? 3 : 2, seam: 0, tip: 0, h: .35, clip: inRock }); t.endMark();
      }
      if (dress === 2) for (let x = 0; x < W; x++) {
        let line = -1;
        for (let y = 0; y < H; y++) { const i = y * W + x; if (geo.f[i] && !geo.und[i] && geo.wz[i] < tideZ * (.95 + .5 * hash2(x, 3, R + 63))) { line = y; break; } }
        if (line < 0) continue;
        t.beginMark();
        const n = 2 + Math.floor(hash2(x, 1, R + 64) * 5);
        for (let k = 0; k < n; k++) { const y = line + k; if (y >= H) break; const i = y * W + x; if (!geo.f[i]) break; if (t.lock[i]) continue;
          t.pal[i] = pal.weed; t.band[i] = k === 0 ? 2 : hash2(x, y, R + 65) < .3 ? 2 : 1; t.h[i] += .3; t.lock[i] = 1; t.mark[i] = t.mid; }
        if (hash2(x, 7, R + 66) < .34) { let low = -1;
          for (let y = 0; y < H; y++) if (t.pal[y * W + x] === pal.weed && t.a[y * W + x]) low = y;
          if (low > 0) for (let k = 1; k <= 1 + Math.floor(hash2(x, 9, R + 67) * 3); k++) { const y = low + k; if (y >= H || geo.f[y * W + x]) break;
            const i = y * W + x; t.a[i] = 255; t.pal[i] = pal.weed; t.band[i] = 1; t.lock[i] = 1; t.mark[i] = t.mid;
            geo.nx[i] = 0; geo.ny[i] = -.4; geo.nz[i] = .9; } }
        t.endMark();
      }
    }
    /* 6 — the pool basin: a damp hollow with a rim lip and a lit inner wall, never a punched hole */
    let poolRect = null;
    if (pool) {
      let x0 = 1e9, y0p = 1e9, x1 = -1e9, y1 = -1e9;
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        const i = y * W + x; if (!geo.f[i] || !geo.cap[i]) continue;
        const dx = (geo.wx[i] - pool.x) / pool.rx, dy = (geo.wy[i] - pool.y) / pool.ry;
        const w = dx * dx + dy * dy + (hash2(x, y, R + 70) - .5) * .16;
        if (w > 1) continue;
        t.pal[i] = pal.wet; t.band[i] = w < .55 ? 1 : 2; t.h[i] = -1.4 * (1 - w); t.lock[i] = 1;
        if (x < x0) x0 = x; if (y < y0p) y0p = y; if (x > x1) x1 = x; if (y > y1) y1 = y;
      }
      if (x1 >= x0) {
        for (let x = x0; x <= x1; x++) {
          for (let y = y0p; y <= y1; y++) { const i = y * W + x;
            if (t.pal[i] !== pal.wet) continue;
            if (y === y0p || t.pal[i - W] !== pal.wet) { t.band[i] = 0; t.h[i] -= .4; }     // shaded upper rim
            break; }
          for (let y = y1; y >= y0p; y--) { const i = y * W + x;
            if (t.pal[i] !== pal.wet) continue;
            t.band[i] = 3; t.h[i] += .7; break; }                                            // lit inner wall
        }
        poolRect = { x: x0, y: y0p, w: x1 - x0 + 1, h: y1 - y0p + 1, depthM: +(pool.depth / PPU).toFixed(2) };
      }
    }
    /* 7 — awash: clip at an irregular sea plane and glaze the cut */
    if (waterZ > 0) for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x; if (!t.a[i]) continue;
      const swell = waterZ + 1.6 * Math.sin(x * .42 + R * .7) + 1.1 * Math.sin(x * .17 + R * 1.9) + (hash2(x, 1, R + 80) - .5) * 1.4;
      if (geo.wz[i] < swell) { t.a[i] = 0; t.lock[i] = 0; }
      else if (geo.wz[i] < swell + 2.2 && !t.lock[i]) { t.pal[i] = pal.wet; t.band[i] = geo.wz[i] < swell + 1 ? 3 : 2; }
    }
    /* 8 — the contact: one texel of ground shade on the bottom row of each column, so a rock with
       no seat decal still sits instead of hovering. */
    for (let x = 0; x < W; x++) for (let y = H - 1; y >= 0; y--) {
      const i = y * W + x; if (!t.a[i]) continue;
      if (!t.lock[i] && !geo.und[i]) { t.band[i] = clamp(t.band[i] - 1, 0, 4); t.h[i] -= .3; }
      break;
    }
    return { tile: t, shift, pal, tideZ, waterZ, poolRect };
  }

  /* view-space normals: the facet normal nudged by the mark relief. The form is in the facets, so
     the relief only carries spalls, sockets, joints and chips. */
  function normalsOf(geo, t, relief) {
    const W = geo.w, H = geo.h, N = W * H, nx = new Float32Array(N), ny = new Float32Array(N), nz = new Float32Array(N);
    const hAt = (x, y) => (x < 0 || y < 0 || x >= W || y >= H || !t.a[y * W + x]) ? 0 : t.h[y * W + x];
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x;
      if (!t.a[i]) { nz[i] = 1; continue; }
      const hx = hAt(x + 1, y) - hAt(x - 1, y), hy = hAt(x, y + 1) - hAt(x, y - 1);
      let a = geo.nx[i] - hx * .5 * relief, b = geo.ny[i] + hy * .5 * relief, c = geo.nz[i] + .35;
      const L = Math.hypot(a, b, c) || 1; nx[i] = a / L; ny[i] = b / L; nz[i] = c / L;
    }
    return { nx, ny, nz };
  }

  // ---- anchors ----------------------------------------------------------------------------------
  function anchorsFor(geo, t, W, H, cxp, baseY, y0, tideZ, waterZ, poolRect) {
    let minX = 1e9, maxX = -1e9, minY = 1e9, maxY = -1e9, best = -1, bi = -1;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x; if (!t.a[i]) continue;
      if (geo.wx[i] < minX) minX = geo.wx[i]; if (geo.wx[i] > maxX) maxX = geo.wx[i];
      if (geo.wy[i] < minY) minY = geo.wy[i]; if (geo.wy[i] > maxY) maxY = geo.wy[i];
      if (geo.cap[i] && geo.wz[i] > best) { best = geo.wz[i]; bi = i; }
    }
    const px = Math.round(cxp), gy = Math.round(baseY - Q * y0);
    const footprint = { rx: +(Math.max(1, (maxX - minX) / 2) / PPU).toFixed(2), ry: +(Math.max(1, (maxY - minY) / 2) / PPU).toFixed(2), ground: { x: px, y: gy } };
    let perch = null;
    if (bi >= 0) {
      const bx = bi % W, by = (bi / W) | 0; let flat = 0;
      for (let dx = -2; dx <= 2; dx++) for (let dy = -1; dy <= 1; dy++) {
        const X = bx + dx, Y = by + dy; if (X < 0 || Y < 0 || X >= W || Y >= H) continue;
        const j = Y * W + X; if (t.a[j] && geo.cap[j] && Math.abs(geo.wz[j] - best) < 1.4) flat++;
      }
      perch = { x: bx, y: by, zM: +(best / PPU).toFixed(2), flat: flat >= 9 };
    }
    const rim = [];
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x; if (!t.a[i]) continue;
      const open = x === 0 || !t.a[i - 1] || x === W - 1 || !t.a[i + 1] || y === H - 1 || !t.a[i + W];
      if (!open) continue;
      rim.push({ x, y, d: Math.hypot(x - px, (y - gy) / Q), a: Math.atan2((gy - y) / Q, x - px), z: geo.wz[i] });
    }
    rim.sort((a, b) => (b.d - Math.abs(b.z - tideZ) * .55) - (a.d - Math.abs(a.z - tideZ) * .55));
    const snags = [], ad = (u, v) => { const d = Math.abs(u - v) % TAU; return Math.min(d, TAU - d); };
    for (const r of rim) if (snags.every(s => ad(s.a, r.a) > 1.05)) { snags.push(r); if (snags.length === 3) break; }
    return {
      footprint, perch,
      snags: snags.map(s => ({ x: s.x, y: s.y, zM: +(s.z / PPU).toFixed(2) })),
      hazard: waterZ > 0 ? { x: px, y: Math.round(baseY - Q * y0 - HZ * waterZ), rM: +(Math.max(footprint.rx, footprint.ry) * 1.35).toFixed(2) } : null,
      pool: poolRect,
      weedLine: { y: Math.round(baseY - Q * y0 - HZ * tideZ), zM: +(tideZ / PPU).toFixed(2) },
      pivot: { x: px, y: gy },
    };
  }

  // ---- build ------------------------------------------------------------------------------------
  function build(formKey, o) {
    o = o || {};
    const key = FORMS[formKey] ? formKey : 'erratic', F = FORMS[key];
    const vi = clamp(o.variant == null ? 0 : o.variant | 0, 0, F.variants.length - 1);
    const p = Object.assign({}, F.variants[vi], o.params || {});
    if (o.seed != null) p.seed = o.seed | 0;
    const stone = STONES[o.stone] ? o.stone : (F.stone || 'sandstone'), S = STONES[stone];
    const tide = ['dry', 'wet', 'awash'].indexOf(o.tide) >= 0 ? o.tide : (F.role === 'water' ? 'awash' : 'dry');
    const dress = clamp(o.dress == null ? 0 : o.dress | 0, 0, 2), R = p.seed | 0;
    const W = F.cell.w, H = F.cell.h, cxp = (W - 1) / 2, baseY = H - 2;
    const masses = F.gen(p, S, R), pool = masses.pool || null;
    if (o.mirror) mirrorMasses(masses);
    const fit = fitMasses(masses, F.cell);
    masses.sort((a, b) => a.cy - b.cy);
    const geo = new Geo(W, H);
    for (let i = 0; i < masses.length; i++) rasterMass(geo, masses[i], i, cxp, baseY, fit.y0);
    const bs = bedSeq(S, R);
    if (tide === 'awash' && !p.wl) p.wl = .34;
    const built = buildTile(geo, S, p, R, tide, dress, bs, fit.topZ, pool, cxp, baseY, fit.y0);
    const nr = normalsOf(geo, built.tile, 1.4);
    const anchors = anchorsFor(geo, built.tile, W, H, cxp, baseY, fit.y0, built.tideZ, built.waterZ, built.poolRect);
    return { form: key, name: F.name, role: F.role, w: W, h: H, tile: built.tile, shift: built.shift, geo, nr, pal: built.pal,
      stone, tide, dress, variant: vi, mirror: !!o.mirror, params: p, beds: bs.beds, anchors, cxp, baseY, y0: fit.y0,
      topM: +(fit.topZ / PPU).toFixed(2), tideZ: built.tideZ, waterZ: built.waterZ,
      id: key + String.fromCharCode(65 + vi) + (o.mirror ? 'm' : '') };
  }

  const view = (b) => ({ n: b.w, m: b.h, h: b.tile.h, a: b.tile.a, pal: b.tile.pal, pals: b.tile.pals, band: b.tile.band, i: (x, y) => y * b.w + x });
  function albedo(b) {
    const band = new Int8Array(b.tile.band);
    for (let i = 0; i < b.w * b.h; i++) band[i] = clamp(band[i] + b.shift[i], 0, 4);
    return render(Object.assign(view(b), { band }), { lit: 1, relief: 1.3, lo: .40, hi: .74 });
  }
  const unlit = (b) => render(view(b), {});
  const mask = (b) => packMask(b.tile, 1, 1.4, b.nr, [-3, 3]);
  const normal = (b) => normalView(b.tile, 1, null, b.nr);

  // ---- seats: the blend contract ----------------------------------------------------------------
  /* A seat decal is the SAME CELL and the SAME PIVOT as the rock, drawn over it. The apron around
     the rock's toe is opened as a region and handed to PxKit.paintRegion, so it is painted by the
     terrain kit itself — the kit's matrix, the kit's marks, the kit's palettes, at the kit's 1 px
     texel. Over the rock's toe only the kit's MARKS are kept (blades, cobbles, pebbles), never the
     matrix, so the ground laps over the stone instead of drawing a line against it. Two seats give
     a SEAM decal: the frontier between them runs under the rock and is dressed with PxKit.edges'
     own cut face, thrown shade and fringe. */
  const SEATS = ['grass', 'dirt', 'path', 'mud', 'sand', 'shingle', 'ledge', 'marram', 'foreshore', 'silt', 'shelf', 'talus', 'rockweed', 'water'];
  const BURY = { grass: 3, shingle: 3, sand: 2, dirt: 2, mud: 2, path: 2, silt: 2, marram: 3, foreshore: 2, talus: 3, rockweed: 2, shelf: 1, ledge: 1, water: 0 };

  function skirt(b, o) {
    o = typeof o === 'string' ? { seat: o } : (o || {});
    const K = root.PxKit; if (!K) return null;
    const seatA = o.seat || 'sand', seatB = o.seat2 || null, step = o.step == null ? 1 : o.step;
    const W = b.w, H = b.h, A = b.tile.a, R = (b.params.seed | 0) + (o.seed || 0) + 911;
    const t = new Tile(W, H); t.a.fill(0);
    const reach = o.reach || clamp(6 + Math.round((b.topM || 1) * 6), 6, 17), D = K.DETAIL;
    /* the toe: every silhouette texel with nothing under it — the line the ground has to meet */
    const toe = [], topRow = new Int16Array(W).fill(-1);
    for (let x = 0; x < W; x++) for (let y = H - 1; y >= 0; y--) if (A[y * W + x]) { toe.push([x, y]); break; }
    for (let x = 0; x < W; x++) for (let y = 0; y < H; y++) if (A[y * W + x]) { topRow[x] = y; break; }
    if (!toe.length) return null;
    const near = new Float32Array(W * H).fill(9e9);
    for (const [tx, ty] of toe) {
      const rr = reach * (.55 + .9 * fbmP(tx / W, .5, 4, 1, R + 3, 2));
      for (let dy = -reach; dy <= reach + 2; dy++) for (let dx = -reach - 2; dx <= reach + 2; dx++) {
        const x = tx + dx, y = ty + dy; if (x < 0 || y < 0 || x >= W || y >= H) continue;
        const d = Math.hypot(dx * .72, dy * (dy < 0 ? 1.6 : 1)) / Math.max(1, rr);
        const i = y * W + x; if (d < near[i]) near[i] = d;
      }
    }
    const bury = (BURY[seatA] == null ? 2 : BURY[seatA]);
    const onRock = (i) => A[i] > 0;
    const inBand = (x, y) => { const i = y * W + x; return near[i] <= 1 && (!onRock(i) || near[i] <= bury / reach * 1.4); };
    /* the A|B frontier: a wandering line through the pivot, in screen space. axis names which way B
       lies — 'we' east · 'ew' west · 'ns' south · 'sn' north — so the decal's seam can be aimed at the
       level's own material boundary. */
    const axis = o.axis || 'we', vert = axis === 'ns' || axis === 'sn', sgn = (axis === 'ew' || axis === 'sn') ? -1 : 1;
    const sideB = (x, y) => {
      if (!seatB) return false;
      const u = (x - b.cxp) / W, v = (y - (b.baseY - Q * b.y0)) / Math.max(8, H);
      const w = fbmP(x / W, y / H, 3, 3, R + 7, 2) - .5;
      return (vert ? (v + w * .5) : (u + w * .45)) * sgn > 0;
    };
    const reg = new Uint8Array(W * H).fill(255);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (inBand(x, y)) reg[y * W + x] = sideB(x, y) ? 1 : 0;
    const mats = [];
    const paintSeat = (kk, id, key) => {
      if (key === 'water') { mats[id] = waterSeat(t, reg, id, W, H, A, R); return; }
      if (!K.MATS[key]) { mats[id] = K.paintRegion(t, 'sand', step, (x, y) => x >= 0 && y >= 0 && x < W && y < H && reg[y * W + x] === id, R + id * 31, D); return; }
      mats[id] = K.paintRegion(t, key, step, (x, y) => x >= 0 && y >= 0 && x < W && y < H && reg[y * W + x] === id, R + id * 31, D);
    };
    paintSeat(0, 0, seatA);
    if (seatB) paintSeat(1, 1, seatB);
    /* alpha: the whole apron off the rock, and over the rock ONLY the kit's own marks */
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x; if (reg[i] === 255) continue;
      if (onRock(i)) { if (t.mark[i]) t.a[i] = 255; }
      else if (near[i] < .82 || hash2(x, y, R + 11) < (1 - near[i]) / .18) t.a[i] = 255;
    }
    /* the kit's own edge language along the A|B frontier, under the rock */
    if (seatB && mats[0] && mats[1] && mats[0].pals && mats[1].pals) {
      const c = K.makeCtx(t, (x, y) => x >= 0 && y >= 0 && x < W && y < H, { detail: D });
      K.edges(c, reg, mats, R + 77);
      for (let i = 0; i < W * H; i++) if (c.touched[i] && reg[i] !== 255 && !onRock(i)) t.a[i] = 255;
    }
    /* the fringe over the toe: the ground's OWN mark, in the ground's own palette, lapping up onto
       the stone — blades for turf, a cobble riding up for shingle, crumbs for earth, a lit bench lip
       for bedrock on bedrock. This is the texel that decides whether a rock sits IN the ground or ON
       it, and it is the kit's fringe table that says which mark. */
    const pals0 = mats[0] && mats[0].pals ? mats[0].pals : null;
    const FR = (K.FRINGE && K.FRINGE[seatA]) || (seatA === 'water' ? null : 'crumb');
    if (pals0 && FR) {
      const fp = pals0.fringe == null ? pals0.ground : pals0.fringe, fp2 = pals0.fringe2 == null ? fp : pals0.fringe2;
      const rate = FR === 'blade' ? .34 : FR === 'stones' ? .42 : FR === 'lip' ? 1 : .30;
      for (const [tx, ty] of toe) {
        /* a toe is not a wall. Where the column of rock above this texel is TALL, the contact is the
           foot of a sheer face — lapping it with a full-height tick every third column is a picket
           fence, which is exactly what pass three shipped first. So: the fringe clusters on a
           wandering field, thins hard against a face, and the ticks there are short. */
        const colH = topRow[tx] < 0 ? 4 : ty - topRow[tx], tall = colH > 13;
        if (fbmP(tx / W, .5, 5, 1, R + 19, 2) < (tall ? .56 : .34)) continue;
        if (hash2(tx, 1, R + 20) > rate * (tall ? .55 : 1)) continue;
        t.beginMark();
        if (FR === 'blade') {
          const base = ty - (hash2(tx, 7, R + 26) < .34 ? 1 : 0);
          const len = (tall ? 1 : 2) + Math.floor(hash2(tx, 2, R + 21) * (tall ? 2 : bury + 1));
          const lit = len - 1 - (hash2(tx, 6, R + 25) < .45 ? 1 : 0);   // no two neighbours light the same row
          for (let k = 0; k < len; k++) { const y = base - k; if (y < 0) break; const i = y * W + tx;
            t.a[i] = 255; t.pal[i] = fp; t.band[i] = k === lit ? 3 : 2; t.h[i] = .6 + k * .1; t.lock[i] = 1; t.mark[i] = t.mid; }
          if (!tall && hash2(tx, 3, R + 22) < .5 && tx + 1 < W) { const y = base - Math.max(1, len - 2), i = y * W + tx + 1;
            if (y >= 0) { t.a[i] = 255; t.pal[i] = fp; t.band[i] = 3; t.h[i] = .5; t.lock[i] = 1; t.mark[i] = t.mid; } }
        } else if (FR === 'stones') {
          const w = (tall ? 2 : 3) + Math.round(hash2(tx, 2, R + 21) * 2), hh = Math.max(2, w - 1), st2 = ellipse(w, hh);
          const oy = ty - (tall ? 0 : 1), pp = hash2(tx, 4, R + 23) < .7 ? fp : fp2;
          stamp(t, st2, tx, oy, { pal: pp, band: 2, seam: 1, tip: 1, h: w * .45, hBase: 0, lock: 1 });
          for (const q of st2.px) { const X = tx + q[0] - st2.ox, Y = oy + q[1] - st2.oy;
            if (X >= 0 && Y >= 0 && X < W && Y < H) { const i = Y * W + X; t.a[i] = 255; t.mark[i] = t.mid; } }
        } else if (FR === 'lip') {
          const i = ty * W + tx; t.a[i] = 255; t.pal[i] = fp; t.band[i] = 4; t.h[i] = .5; t.lock[i] = 1; t.mark[i] = t.mid;
          if (hash2(tx, 5, R + 24) < .22 && pals0.fringe != null) { const j = (ty - 1) * W + tx;
            if (ty > 0) { t.a[j] = 255; t.pal[j] = pals0.fringe; t.band[j] = 1; t.lock[j] = 1; t.mark[j] = t.mid; } }
        } else {
          for (let k = 0; k < 1 + (!tall && hash2(tx, 2, R + 21) < .4 ? 1 : 0); k++) { const y = ty - k; if (y < 0) break;
            const i = y * W + tx; t.a[i] = 255; t.pal[i] = fp; t.band[i] = k ? 1 : 2; t.h[i] = .3; t.lock[i] = 1; t.mark[i] = t.mid; }
        }
        t.endMark();
      }
    }
    /* the cast shade: the toe line swept down-key, its run set by how tall that column of rock is.
       A stepped region with a torn tail — never a soft ellipse, never a 1-texel outline. */
    if (seatA !== 'water') {
      const LX = .64, LY = .77;
      const dark = (x, y, d) => { if (x < 0 || y < 0 || x >= W || y >= H) return; const i = y * W + x;
        if (!t.a[i] || onRock(i) || t.mark[i]) return; t.band[i] = clamp(t.band[i] - d, 0, 4); t.h[i] -= .2; };
      for (const [tx, ty] of toe) {
        const colH = topRow[tx] < 0 ? 4 : ty - topRow[tx];
        const run = clamp(Math.round(1.5 + colH * .34), 2, reach);
        for (let k = 1; k <= run; k++) {
          const fr = k / run;
          if (fr > .5 && hash2(tx, k, R + 14) < (fr - .5) / .5 * .8) continue;
          const x = tx + Math.round(k * LX), y = ty + Math.round(k * LY), d = k <= 2 ? 2 : 1;
          dark(x, y, d); if (k > 1) dark(x - 1, y, d);
        }
      }
    }
    const bl = (K.BLEND && K.BLEND[seatA]) || { markLead: .5, grain: 5 };
    return { rgba: render(t, { lit: 1, relief: 1.3 * D, lo: .38, hi: .78 }), blend: packBlend(t, 1, { markLead: bl.markLead, grain: bl.grain, seed: R }),
      tile: t, seat: seatA, seat2: seatB, w: W, h: H, reach };
  }
  /* the water seat: a wandering waterline with a wet-dark collar and a foam frontier. No marks, no
     cast shadow — the shader owns the sea; this only says where the rock enters it. */
  function waterSeat(t, reg, id, W, H, A, R) {
    const sea = t.addPal(SEA, .9), foam = t.addPal(SHALLOW, .8);
    for (let x = 0; x < W; x++) {
      let bot = -1; for (let y = H - 1; y >= 0; y--) if (A[y * W + x]) { bot = y; break; }
      const wl = (bot < 0 ? H - 2 : bot) - Math.round(1.4 + 1.5 * Math.sin(x * .38 + R * .5) + 1.1 * Math.sin(x * .13 + R) + (hash2(x, 1, R + 60) - .5) * 1.6);
      for (let y = 0; y < H; y++) {
        const i = y * W + x; if (reg[i] !== id) continue;
        if (A[i]) { if (y >= wl) { t.a[i] = 255; t.pal[i] = sea; t.band[i] = y === wl ? 2 : 1; t.h[i] = .1; t.lock[i] = 1; } continue; }
        if (y < wl - 1) continue;
        t.a[i] = 255; t.pal[i] = sea; t.band[i] = 2; t.h[i] = 0;
        if (y <= wl + 1 && hash2(x, y, R + 61) < .6) { t.pal[i] = foam; t.band[i] = 4; t.lock[i] = 1; }
      }
    }
    return { key: 'water', pals: { ground: sea, fringe: foam } };
  }

  root.PxRockIso2 = { PPU, T, ELEV: 40, Q, HZ, FORMS, STONES, SEATS, BURY,
    FORM_KEYS: Object.keys(FORMS), STONE_KEYS: Object.keys(STONES), TIDES: ['dry', 'wet', 'awash'],
    DRESS: ['bare', 'barnacled', 'weeded'], build, albedo, unlit, mask, normal, skirt, bedSeq, mkPlan, planAt };
})(typeof globalThis !== 'undefined' ? globalThis : window);
