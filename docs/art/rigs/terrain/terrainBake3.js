/* terrainBake3.js — the shoreline pass. Load Art/terrainBake.js then
   Art/terrainBake2.js first; this registers into the same TB tables, so
   TB.bake('foreshore', step, createCanvas) works exactly as before.

   FOUR NEW PLAN-VIEW MATERIALS
     foreshore  512x512 = 16 x 16 m   red rippled low-tide sand flat
     talus      512x512 = 16 x 16 m   angular red sandstone block apron
     ledge      256x256 =  8 x  8 m   red wave-cut rock platform
     rockweed   256x256 =  8 x  8 m   Ascophyllum / Fucus canopy over rock

   FOUR EDGE STRIPS — a new kind of asset.
     turf · scarp · wrack · weedline    256 x 128 = 8 m along x 4 m across

   An edge strip is an RGBA DECAL, not a tile. s runs ALONG the shore and tiles;
   t runs ACROSS the boundary and does NOT — t = 0 is the landward/upper side,
   t = 1 the seaward/lower side. Alpha falls to zero at both ends, so one strip
   lays over whatever two materials happen to meet there. Laid along a spline,
   this is what puts a real line on the coast instead of a noise cross-fade.

   Same contract as the earlier passes for the tiles: exactly periodic; intrinsic
   colour, grain and a non-directional cavity/crown term only. No key light, no
   cast shadow, no water, no wet or damp bands. The edge strips break exactly one
   rule — alpha — and nothing else.
*/
globalThis.TB3 = (function () {
  const T = globalThis.TB, T2 = globalThis.TB2;
  if (!T || !T2) throw new Error('Art/terrainBake.js and Art/terrainBake2.js must be loaded first');
  const hash = T.hash, fbm = T.fbm, fbmA = T.fbmA, rfbm = T.rfbm;
  const worley = T.worley, worleyA = T.worleyA, blades = T.blades;
  const ss = T.ss, ramp = T.ramp, mkRamp = T.mkRamp, P = T.P, Q = T2.Q, ridA = T2.ridA;
  const set = T.set, tint = T.tint, mul = T.mul, OUT = T.OUT;
  const mix = T.mix, cl = T.cl, fr = T.fr, pk = T.pk;
  const F = Math.floor, C = Math.cos, SN = Math.sin, SQ = Math.sqrt, PI = Math.PI;

  /* 1 inside [a,b], feathered by f on both sides. The whole edge vocabulary is
     built out of this — a coastline is a stack of bands. */
  const band = (x, a, b, f) => ss(a - f, a, x) * ss(b + f, b, x);

  /* Worley that also hands back the offset from the winning site, so a cell can
     be shaded by direction — which is how an angular block gets facets without
     baking a light direction into it. */
  const WB = { d1: 0, d2: 0, id: 0, dx: 0, dy: 0 };
  function worleyB(s, t, cx, cy, sd, jit) {
    jit = jit === undefined ? 1 : jit;
    const x = s * cx, y = t * cy, xi = F(x), yi = F(y);
    let d1 = 9e9, d2 = 9e9, id = 0, bx = 0, by = 0;
    for (let oy = -1; oy <= 1; oy++) for (let ox = -1; ox <= 1; ox++) {
      const gx = xi + ox, gy = yi + oy;
      const wx = ((gx % cx) + cx) % cx, wy = ((gy % cy) + cy) % cy;
      const jx = gx + .5 + (hash(wx, wy, sd) - .5) * jit;
      const jy = gy + .5 + (hash(wx, wy, sd + 9173) - .5) * jit;
      const dx = x - jx, dy = y - jy, d = SQ(dx * dx + dy * dy);
      if (d < d1) { d2 = d1; d1 = d; id = hash(wx, wy, sd + 551); bx = dx; by = dy; }
      else if (d < d2) d2 = d;
    }
    WB.d1 = d1; WB.d2 = d2; WB.id = id; WB.dx = bx; WB.dy = by; return WB;
  }

  /* ------------------------------------------------------------------ palettes
     Everything here is sampled off the PEI shoreline photography. The ramps run
     the whole way — black hollow to pale dry crown — because a ramp that only
     uses its middle reads as a flat wall however much detail sits on it. */
  const R = {
    /* lower foreshore sand: the same mineral as the dune, but coarser and with
       more of the red heavy fraction left in it. This is the DRY albedo — the
       engine's wet band is what turns it into the colour of the photographs. */
    fsand: mkRamp([[0, '#48302a'], [.16, '#5f4033'], [.34, '#7d543f'], [.52, '#9c6f52'], [.70, '#b98d6b'], [.86, '#d2aa88'], [1, '#e6c5a6']]),
    /* garnet and magnetite winnowed into the ripple troughs */
    fdark: mkRamp([[0, '#241813'], [.5, '#432c21'], [1, '#66452f']]),
    /* Ascophyllum — olive going to gold. The signature colour of a PEI ledge at
       low water, and nothing else in the set is anywhere near it. */
    asco: mkRamp([[0, '#2a2611'], [.18, '#3d3616'], [.36, '#55491d'], [.54, '#6f5f26'], [.72, '#8b7830'], [.86, '#a49040'], [1, '#c0af5e']]),
    /* Fucus — shorter, darker, browner; also does duty as dried wrack */
    fucus: mkRamp([[0, '#221f14'], [.32, '#39321a'], [.62, '#534926'], [1, '#786b39']]),
    ulva: mkRamp([[0, '#2c4a1e'], [.5, '#4e7a2c'], [1, '#82b04d']]),
    barn: mkRamp([[0, '#6b6760'], [.4, '#908b80'], [.72, '#b2ac9f'], [1, '#d4cebe']]),
    mussel: mkRamp([[0, '#15181e'], [.5, '#2a323e'], [1, '#4c5668']]),
    lichen: mkRamp([[0, '#5d6152'], [.5, '#868a72'], [1, '#adb094']]),
    /* three stacked beds of the wave-cut platform. Red where Shelf is grey — the
       two are kept as separate materials so a region can pick one or the other. */
    ledgeTop: mkRamp([[0, '#2c1a12'], [.16, '#43281c'], [.34, '#5e3b28'], [.52, '#7b5238'], [.70, '#966b4c'], [.86, '#b08962'], [1, '#c9a67f']]),
    ledgeMid: mkRamp([[0, '#251310'], [.16, '#3d1e17'], [.34, '#582d20'], [.52, '#75432d'], [.70, '#935c3f'], [.86, '#b07a56'], [1, '#c99a75']]),
    ledgeLow: mkRamp([[0, '#1e0f0c'], [.16, '#331812'], [.34, '#4a2519'], [.52, '#633523'], [.70, '#7e4a30'], [.86, '#9a6442'], [1, '#b5825c']]),
    /* bleached driftwood in the strand line */
    drift: mkRamp([[0, '#5a5348'], [.4, '#867d6d'], [.72, '#a89d8a'], [1, '#c8bda8']])
  };
  const LEDGE_BED = [R.ledgeTop, R.ledgeMid, R.ledgeLow];

  /* ================================================================= FORESHORE
     The wide low-tide sand flat: wave ripple trains in red-brown sand, drained
     by shallow runnels, worked over by lugworms.
       Lo  planed almost flat — firm quiet sand, runnels the only structure
       Mid a working wave-ripple field
       Hi  long-wavelength megaripple, shell and gravel lag in the troughs
     Wave numbers are integers so the trains tile, three near-parallel regimes are
     SELECTED by region rather than summed, and crests break along their length —
     the same three rules that stopped Ripple reading as woven cloth. */
  function foreshore(s, t, sd, px, py, st) {
    const broad = fbm(s, t, 2, sd + 1, 3), broad2 = fbm(s, t, 4, sd + 2, 3);
    const w1 = fbm(s, t, 3, sd + 10, 3) - .5, w2 = fbm(s, t, 7, sd + 11, 3) - .5, w3 = fbm(s, t, 17, sd + 12, 2) - .5;

    /* Hi is a LONGER wave, not a shorter one — storm and spring-tide flats build
       megaripple. Lo is the same field almost planed off. Wave numbers are kept
       low enough that a crest is fifteen texels: pack forty crests into the tile
       and the whole thing reads as corduroy however much it is warped. */
    const WA = pk([[3, 26], [4, 34], [3, 22]], st);
    const WB = pk([[1, 24], [2, 31], [1, 20]], st);
    const WC = pk([[5, 29], [6, 38], [4, 25]], st);
    const ex = pk([1.00, 1.34, 1.68], st);

    const warp = 26.0 * w1 + 9.0 * w2 + 2.4 * w3;
    const rA = Math.pow(.5 + .5 * C(2 * PI * (WA[0] * s + WA[1] * t) + warp), ex);
    const rB = Math.pow(.5 + .5 * C(2 * PI * (WB[0] * s + WB[1] * t) + warp * .88 + 1.9 * w2), ex * .94);
    const rC = Math.pow(.5 + .5 * C(2 * PI * (WC[0] * s + WC[1] * t) + warp * .76 + 2.6 * w1), ex * 1.08);
    const z1 = fbm(s, t, 2, sd + 80, 2), z2 = fbm(s, t, 3, sd + 81, 3);
    let rid = mix(rA, rB, ss(.46, .55, z1));
    rid = mix(rid, rC, ss(.64, .73, z2));

    /* the field has to CLEAR in places. A ripple train that runs edge to edge at
       an even amplitude is the single thing that makes a flat read as fabric;
       real ones die out onto smooth sheets and pick up again. */
    const gate = pk([.62, .36, .28], st);
    const gateN = fbm(s, t, 3, sd + 20, 3) * .62 + fbm(s, t, 6, sd + 21, 2) * .38;
    const amp = pk([.34, 1.00, 1.20], st) * (.08 + .92 * ss(gate, gate + .26, gateN));
    /* crests break and resume ALONG their length, so the break field is stretched
       ALONG the crest — stretch it across and the tile bands into vertical
       panels of silk instead of breaking into ripple segments */
    const brkF = pk([.14, .22, .34], st);
    const brk = brkF + (1 - brkF) * ss(.30, .74, fbmA(s, t, 5, 30, sd + 70, 3));
    let h = rid * amp * brk * .74 + broad * .14 + broad2 * .10;

    /* runnels: the flat drains, and the drainage is what carries the low step */
    const run = ss(.80, .965, rfbm(s, t, 3, sd + 33, 4)) * pk([1.25, 1.0, .85], st);
    const run2 = ss(.87, .995, rfbm(s, t, 7, sd + 34, 3)) * pk([1.2, 1.0, .9], st);
    h -= run * .20 + run2 * .10;
    h = cl(h + (fbm(s, t, 80, sd + 90, 2) - .5) * .06, 0, 1);

    const comp = fbm(s, t, 6, sd + 40, 3), gr = fbm(s, t, 110, sd + 91, 2), gr2 = fbm(s, t, 220, sd + 92, 2);
    /* most of the ripple's read comes from the cavity pass, not from ramping the
       height straight into value — do that and wet sand comes out as watered silk */
    set(ramp(R.fsand, cl(h * .34 + (comp - .5) * .30 + (broad - .5) * .22 + (gr - .5) * .20 + (gr2 - .5) * .10 + .42, 0, 1)));
    /* the red is heavy mineral concentrated in the troughs, not a wash over the
       whole tile — that is why a PEI flat reads striped rather than orange */
    const hm = ss(.55, .05, h) * ss(.40, .78, fbm(s, t, 9, sd + 41, 3));
    { const d = ramp(R.fdark, .20 + comp * .50); tint(d[0], d[1], d[2], hm * pk([.20, .30, .38], st)); }
    tint(104, 86, 72, run * .24 + run2 * .13);
    mul(.955 + .09 * hash(px, py, sd + 99));

    /* lugworm: a coiled spoil cast with a small feeding pit alongside it */
    const cz = ss(.36, .70, fbm(s, t, 4, sd + 120, 3)) * pk([.55, 1.0, 1.25], st);
    if (cz > .04) {
      const cw = worley(s, t, 60, sd + 121, 1);
      if (cw.id < .22 * cz && cw.d1 < .26) {
        const k = ss(.26, .06, cw.d1);
        const cc = ramp(R.fsand, .30 + fr(cw.id * 7.7) * .24);
        tint(cc[0] * .94, cc[1] * .92, cc[2] * .90, k * .85);
        mul(1 + (.5 + .5 * C(2 * PI * cw.d1 * 46 + fr(cw.id * 31.1) * 6.3) - .5) * .18 * k);
        h += k * .30;
      } else if (cw.id > 1 - .10 * cz && cw.d1 < .17) {
        const k = ss(.17, .04, cw.d1); tint(60, 42, 32, k * .55); h -= k * .36;
      }
    }
    /* shell hash — flecks, dull, settled low */
    const sg = worley(s, t, 120, sd + 56, 1);
    if (sg.id < pk([.018, .034, .068], st) && sg.d1 < .26) {
      const k = ss(.26, .08, sg.d1); tint(212, 200, 182, k * .52); h += k * .11;
    }
    /* Hi: gravel winnowed into the troughs, exactly where the water sits longest */
    if (st === 2) {
      const gv = worley(s, t, 84, sd + 57, 1);
      if (gv.id < .13 && gv.d1 < .38) {
        const k = ss(.38, .14, gv.d1) * ss(.48, .20, h);
        const gc = ramp(P.stoneGrey, .24 + fr(gv.id * 31.7) * .34);
        tint(gc[0], gc[1], gc[2], k * .72); h += k * .24;
      }
    }
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ===================================================================== LEDGE
     The red wave-cut platform: the same bedded mass as Shelf, but red, and cut by
     the sea rather than by weather. Scour pans are the difference — broad shallow
     hollows ground into the beds, holding weed, with bevelled rims.
       Lo  intact bevelled pavement, pans shallow
       Mid dissected — benches on the second bed, pans deep and weeded
       Hi  stripped to the third bed, cobble on the benches */
  function ledge(s, t, sd, px, py, st) {
    const iv = st * .5;
    const u = s + 2 * t;
    const fold = (fbm(s, t, 2, sd + 31, 3) - .5) * 3.2 + (fbm(s, t, 5, sd + 32, 3) - .5) * 1.0;
    const bp0 = 6 * u + fold;
    const bmod = i => ((i % 6) + 6) % 6;

    const js = s + (fbm(s, t, 9, sd + 51, 2) - .5) * .13;
    const jt = t + (fbm(s, t, 9, sd + 52, 2) - .5) * .11;
    const w = worleyA(js, jt, 3, 3, sd + 1, .92);
    const wid = w.id, wd1 = w.d1, wd2 = w.d2;
    const w2 = worleyA(js, jt, 7, 6, sd + 4, .88);
    const blockB = (fr(wid * 13.77) - .5) * .84 + (fr(w2.id * 29.3) - .5) * .16;

    /* the benches are only partly slab-shaped. Weight the joint pattern in lightly:
       lean on it and the platform comes out as a floor of terracotta tiles. */
    const dE = rfbm(s, t, 3, sd + 60, 3) * .50 + fbm(s, t, 7, sd + 61, 2) * .26 + blockB * .28
      + (hash(bmod(F(bp0)), 91, sd) - .5) * .07 + (fbm(s, t, 20, sd + 62, 2) - .5) * .06;
    const th1 = mix(.56, .38, iv), th2 = mix(.80, .56, iv);
    const lvl = dE > th2 ? 2 : dE > th1 ? 1 : 0;
    const riser = Math.max(ss(.030, 0, Math.abs(dE - th1)), ss(.030, 0, Math.abs(dE - th2)));

    const bp = bp0 + lvl * 2.63;
    const tone = hash(bmod(F(bp)), 23, sd);
    const toneAmp = lvl > 0 ? .20 : .12;
    const lam = .5 + .5 * SN(2 * PI * (18 * u) + fold * 3);
    const lam2 = .5 + .5 * SN(2 * PI * (30 * u) + fold * 5);
    const lamM = ss(.34, .74, fbm(s, t, 6, sd + 35, 3)) * (lvl > 0 ? 1.0 : .20) * (.72 + .6 * riser);

    /* hold the joint width above a texel and fade it with opacity — a joint
       modulated toward zero width aliases into dotted stitching */
    const jw = (.032 + .020 * fbm(s, t, 10, sd + 2, 2)) * pk([.74, 1.0, 1.26], st);
    const jVis = ss(pk([.50, .34, .20], st), pk([.88, .76, .60], st),
      fbm(s, t, 4, sd + 70, 3) * .74 + fbm(s, t, 11, sd + 71, 2) * .26);
    const joint = ss(jw, jw * .30, wd2 - wd1) * jVis;
    const joint2 = ss(jw * .74, jw * .22, w2.d2 - w2.d1) * jVis * .62;

    const weathBig = fbm(s, t, 3, sd + 25, 3), weath = fbm(s, t, 11, sd + 5, 4), fine = fbm(s, t, 44, sd + 15, 2);
    const slabV = (fr(wid * 19.3) - .5) * .09;

    /* scour pans — the wave-cut signature. Broad, shallow, smooth-floored. */
    const pw = worleyA(s + (fbm(s, t, 4, sd + 80, 2) - .5) * .22, t + (fbm(s, t, 4, sd + 81, 2) - .5) * .22, 5, 5, sd + 82, 1);
    const pan = cl(1 - pw.d1 / pk([.40, .52, .62], st), 0, 1) * ss(.40, .64, fr(pw.id * 7.3));

    const v = cl(.16 + slabV + (tone - .5) * toneAmp + weathBig * .24 + weath * .28 + fine * .12
      + ((lam - .5) * .22 + (lam2 - .5) * .10) * lamM - pan * .20 - lvl * .06, 0, 1);
    set(ramp(LEDGE_BED[lvl], v));

    let h = cl(.46 + slabV + ((lam - .5) * .12 + (lam2 - .5) * .05) * lamM + weath * .18 + fine * .06
      - pan * .26 - joint * .40 - joint2 * .22, 0, 1);
    h -= lvl * .30;
    tint(34, 22, 18, cl(joint * .62 + joint2 * .34, 0, 1));

    /* cobble left on the benches, carrying the colour of the bed above */
    if (lvl > 0) {
      const rb = worley(s, t, 100, sd + 8, 1);
      const rbz = ss(.30, .70, fbm(s, t, 8, sd + 63, 3)) * (.35 + .9 * riser);
      if (rb.id < .30 * rbz && rb.d1 < .34) {
        const k = ss(.34, .12, rb.d1);
        const rc = ramp(LEDGE_BED[lvl - 1], cl(.22 + fr(rb.id * 9.1) * .40, 0, 1));
        tint(rc[0], rc[1], rc[2], k * .80); h += k * .16;
      }
    }
    /* weed only where water sits: the joints and the pan floors, never the crowns */
    const wd = cl(joint * .85 + joint2 * .40 + pan * ss(.40, .82, fbm(s, t, 7, sd + 6, 3)), 0, 1) * pk([.55, .90, 1.20], st);
    if (wd > .01) { const wc = ramp(R.fucus, .18 + weath * .50); tint(wc[0], wc[1], wc[2], cl(wd * .62, 0, 1)); }
    /* barnacle on the highest, longest-dry crowns only */
    if (lvl === 0) {
      const bz = ss(.58, .86, fbm(s, t, 14, sd + 9, 3)) * ss(.46, .76, v) * pk([1.2, 1.0, .72], st);
      if (bz > .01) {
        const bw = worley(s, t, 100, sd + 10, 1);
        const bk = fr(bw.id * 13.3) < .55 ? ss(.34, .10, bw.d1) * bz : 0;
        if (bk > .005) { const bc = ramp(R.barn, .30 + fr(bw.id * 29.7) * .50); tint(bc[0], bc[1], bc[2], bk * .55); h += bk * .10; }
      }
    }
    mul(.976 + .048 * hash(px, py, sd + 95));
    { const gw = worley(s, t, 128, sd + 96, 1); mul(.966 + .068 * fr(gw.id * 17.3)); }
    OUT.h = h;
    return OUT;
  }

  /* ================================================================== ROCKWEED
     The canopy on the lower platform. Fronds LIE DOWN, all one way per patch —
     that is what a rockweed bed looks like from above at low water, and standing
     them up turns it into grass.
       Lo  barnacled rock with scattered tufts
       Mid closed olive canopy, rock in the gaps
       Hi  deep drape, bladders showing, Ulva in the runnels, mussel in the gullies */
  function rockweed(s, t, sd, px, py, st) {
    /* substrate: knobbly red rock with hollows where the water lies. NOISE ONLY —
       a Worley d2−d1 net under a canopy turns the whole tile into cracked glaze,
       because the weed then fills the polygons and outlines them. */
    const rk = fbm(s, t, 5, sd + 1, 4), rk2 = fbm(s, t, 13, sd + 21, 3), rkf = fbm(s, t, 46, sd + 2, 2);
    const hollow = ss(.58, .94, rfbm(s, t, 4, sd + 3, 3));
    set(ramp(Q.rock, cl(.18 + rk * .30 + rk2 * .16 + rkf * .12 - hollow * .12, 0, 1)));
    let h = .34 + rk * .22 + rk2 * .10 + rkf * .06 - hollow * .26;

    const bz = ss(.34, .70, fbm(s, t, 5, sd + 4, 3)) * pk([1.15, .70, .35], st);
    if (bz > .02) {
      const bw = worleyA(s, t, 110, 110, sd + 5, 1);
      if (fr(bw.id * 13.3) < .60) {
        const k = ss(.34, .10, bw.d1) * bz;
        const bc = ramp(R.barn, .28 + fr(bw.id * 29.7) * .50);
        tint(bc[0], bc[1], bc[2], k * .60); h += k * .09;
      }
    }
    const mz = hollow * ss(.44, .78, fbm(s, t, 6, sd + 6, 3)) * pk([.35, .75, 1.10], st);
    if (mz > .02) {
      const mw = worleyA(s, t, 150, 110, sd + 7, .9);
      const k = ss(.42, .14, mw.d1) * mz;
      const mc = ramp(R.mussel, .18 + fr(mw.id * 17.9) * .60);
      tint(mc[0], mc[1], mc[2], cl(k * .90, 0, 1)); h += k * .08;
    }

    /* canopy density: clumped by noise, not by cells. The rock showing through
       the gaps is what makes it a weed BED rather than a shag carpet. */
    const cN = fbm(s, t, 3, sd + 8, 3) * .48 + fbm(s, t, 7, sd + 22, 2) * .32 + fbm(s, t, 16, sd + 23, 2) * .20;
    const cover = cl(ss(pk([.64, .44, .32], st), pk([.86, .64, .52], st), cN) * (1 - hollow * .28), 0, 1);
    if (cover > .02) {
      /* a frond-noise mat first. Without it the explicit strokes read as loose
         worms dropped on bare rock instead of as a bed. */
      const dirSel = ss(.42, .58, fbm(s, t, 2, sd + 14, 2));
      const bsx = s + (fbm(s, t, 4, sd + 11, 2) - .5) * .05;
      const fn = mix(fbmA(bsx, t, 22, 84, sd + 12, 2), fbmA(bsx + t, t, 22, 84, sd + 13, 2), dirSel) * .64
        + fbmA(bsx, t, 40, 150, sd + 20, 1) * .36;
      const mat = ss(.34, .70, fn);
      const wc = ramp(R.asco, cl(.14 + mat * .46 + cN * .22 + rkf * .12, 0, 1));
      tint(wc[0], wc[1], wc[2], cl(cover * (.52 + .46 * mat), 0, 1));
      h += cover * mat * .24;

      const lean = 1.5 + (fbm(s, t, 3, sd + 10, 2) - .5) * 2.6;
      const b1 = blades(s, t, pk([26, 22, 18], st), sd + 15, cover * pk([.85, 1.15, 1.5], st),
        pk([.9, 1.2, 1.5], st), pk([.8, 1.1, 1.4], st), .22, lean);
      if (b1.v > .02) {
        const fc = ramp(R.asco, cl(.20 + b1.q * .42 + fr(b1.id * 11.3) * .26, 0, 1));
        tint(fc[0], fc[1], fc[2], cl(b1.v * 1.15, 0, 1));
        h += b1.v * .30;
        /* air bladders — a brighter node every so often along a frond */
        const nod = .5 + .5 * C(2 * PI * b1.q * pk([3, 4, 5], st) + fr(b1.id * 7.1) * 6.3);
        if (nod > .80 && b1.v > .45) mul(1 + (nod - .80) * .95);
      }
      const b2 = blades(s, t, 30, sd + 16, cover * pk([.5, .8, 1.05], st), .8, 1.0, .17, -lean * .74);
      if (b2.v > .02) {
        const fc2 = ramp(R.fucus, cl(.18 + b2.q * .46 + fr(b2.id * 5.3) * .20, 0, 1));
        tint(fc2[0], fc2[1], fc2[2], cl(b2.v * 1.0, 0, 1)); h += b2.v * .20;
      }
    }
    const uz = ss(.62, .88, fbm(s, t, 9, sd + 17, 3)) * pk([.10, .40, .82], st) * (.40 + .60 * hollow);
    if (uz > .02) {
      const uc = ramp(R.ulva, .22 + fbm(s, t, 30, sd + 18, 2) * .56);
      tint(uc[0], uc[1], uc[2], cl(uz * .70, 0, 1)); h += uz * .05;
    }
    mul(.972 + .056 * hash(px, py, sd + 19));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ===================================================================== TALUS
     The block apron at the foot of a sandstone cliff. Angular, not rounded —
     Shingle is what the sea has finished with, this is what has just arrived.
       Lo  a scatter of fallen slabs on a gravel floor
       Mid a closed apron
       Hi  a deep chaotic blockfield, big blocks, black interstices
     Facets are ALBEDO, not incident light: a fresh fracture is a different colour
     from a weathered top, and the cavity pass supplies the rest. */
  function talus(s, t, sd, px, py, st) {
    const g1 = fbm(s, t, 60, sd + 1, 3), g2 = fbm(s, t, 16, sd + 2, 3);
    set(ramp(Q.till, cl(.16 + g1 * .24 + g2 * .18, 0, 1)));
    let h = .08 + g1 * .09 + g2 * .05;
    {
      const gw = worley(s, t, 200, sd + 3, 1);
      if (gw.id < .34 && gw.d1 < .30) {
        const k = ss(.30, .10, gw.d1);
        const gc = ramp(Q.rock, .20 + fr(gw.id * 19.7) * .34);
        tint(gc[0], gc[1], gc[2], k * .68); h += k * .08;
      }
    }
    /* Blocks TESSELLATE — a collapsed face packs, it does not scatter. Take the
       Voronoi cell and inset it and you get angular slabs that fit together with
       a black gap between; a radius test around the site gives round pebbles,
       which is Shingle, not talus. Two metres down to half a metre. */
    const CELL = [[28, 26], [15, 13], [8, 7]];
    const GAPW = [.16, .13, .11];
    const SEEDO = [0, 311, 622];
    const HIGH = [.30, .48, .66];
    const G = pk([[.30, .16, .06], [.58, .52, .34], [.80, .82, .72]], st);
    const apron = fbmA(s, t, 3, 4, sd + 7, 2);
    for (let i = 0; i < 3; i++) {
      const g = G[i] * (.40 + 1.0 * ss(.26, .76, apron)); if (g < .02) continue;
      const ws = s + (fbm(s, t, 10, sd + 8 + i, 2) - .5) * .05;
      const wt = t + (fbm(s, t, 10, sd + 18 + i, 2) - .5) * .05;
      const w = worleyB(ws, wt, CELL[i][0], CELL[i][1], sd + SEEDO[i], .96);
      if (fr(w.id * 47.3) > g) continue;
      const gw2 = GAPW[i] * (.7 + .6 * fr(w.id * 3.3));
      const inset = ss(gw2 * .35, gw2 * 1.5, w.d2 - w.d1);
      if (inset < .03) continue;
      const k = ss(0, .20, inset);

      const nf = 3 + ((fr(w.id * 3.7) * 3) | 0);
      const fi = F((Math.atan2(w.dy, w.dx) / (2 * PI) + .5) * nf + fr(w.id * 13.1) * nf);
      const fh = hash(fi, 0, sd + SEEDO[i] + 77);
      const fresh = ss(.35, .85, fr(w.id * 23.3)) * pk([.30, .55, .80], st);
      const vv = cl(.22 + fr(w.id * 7.13) * .40 + (fh - .5) * .34, 0, 1);
      const cb = ramp(Q.rock, vv).slice();
      const cf = ramp(Q.fresh, cl(vv + .08, 0, 1));
      tint(mix(cb[0], cf[0], fresh * .55), mix(cb[1], cf[1], fresh * .55), mix(cb[2], cf[2], fresh * .55), k);
      /* bedding carried on the block, in the block's own attitude. Kept low — a
         strong stripe across every face turns the apron into a pile of woodchips. */
      const th = fr(w.id * 5.1) * 6.283;
      mul(1 + (.5 + .5 * SN(2 * PI * (w.dx * SN(th) + w.dy * C(th)) * 3.4) - .5) * .09 * k);
      /* red dust film — heavier on the older blocks, so they sit IN the apron */
      tint(122, 84, 58, ((1 - fresh) * .18 + ss(.30, .78, g1) * .09) * k);
      mul(1 + (fbm(s, t, 240, sd + 9, 2) - .5) * .12 * k);
      h = Math.max(h, (HIGH[i] + fr(w.id * 31.7) * .11) * ss(0, .34, inset));
    }
    /* lichen on the long-settled upper blocks; damp dark deep in the interstices */
    const li = ss(.60, .88, fbm(s, t, 15, sd + 30, 3)) * ss(.42, .70, h) * pk([1.15, 1.0, .70], st);
    if (li > .01) { const lc = ramp(R.lichen, .24 + g1 * .46); tint(lc[0], lc[1], lc[2], li * .22); }
    const dmp = ss(.24, .10, h) * ss(.56, .86, fbm(s, t, 6, sd + 31, 3)) * pk([.5, .8, 1.0], st);
    if (dmp > .01) { const dc = ramp(R.fucus, .08 + g2 * .30); tint(dc[0], dc[1], dc[2], dmp * .30); }
    mul(.974 + .050 * hash(px, py, sd + 71));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ====================================================================== EDGES
     s along the shore (tiles), t across the boundary (does not).
     t = 0 landward / upper, t = 1 seaward / lower. OUT.a is coverage. */

  /* ------------------------------------------------------------------- TURF LIP
     Grass sward breaking over a cliff or bank top. The brow wanders, bites back
     into the field where a block has gone, and hangs over below.
       Lo  a stable vegetated brow, barely a break
       Mid a clean sod lip with tension cracks behind it
       Hi  actively failing — big gashes, turf mats hanging, clods on the face */
  function edgeTurf(s, t, sd, px, py, st) {
    const wob = (fbmA(s, t, 4, 2, sd + 1, 3) - .5) * .26 + (fbmA(s, t, 10, 4, sd + 2, 2) - .5) * .09;
    const bite = ss(.60, .92, fbmA(s, t, 5, 2, sd + 3, 2)) * pk([.02, .06, .12], st);
    const e = .40 + wob * pk([.55, 1.0, 1.35], st) + bite;
    const d = t - e;
    /* the underside of the lip is where a sod edge is irregular — a uniform-width
       band of soil reads as a painted stripe, which is exactly what it is */
    const under = pk([.048, .078, .118], st) + (fbmA(s, t, 7, 2, sd + 40, 3) - .5) * .075 + (fbmA(s, t, 17, 4, sd + 41, 2) - .5) * .025;
    const fine = fbm(s, t, 70, sd + 9, 2), gt = fbm(s, t, 10, sd + 4, 3), gm = fbm(s, t, 26, sd + 5, 3);

    const gDry = ss(-.20, -.01, d);
    set(ramp(P.grass, cl(.28 + gt * .30 + gm * .12 - gDry * .12 + (fine - .5) * .14, 0, 1)));
    let h = .52 + gt * .14 + gm * .06;
    { const sc = ramp(P.straw, .20 + gm * .50); tint(sc[0], sc[1], sc[2], gDry * .22 + ss(.60, .86, gm) * .16); }
    /* the sward's own blades, and a fringe of them hanging over the brow. Left
       ungated they run all the way down the face as vertical scratches. */
    const gb = blades(s, t, 40, sd + 6, .55, 1.4, 1.6, .13, .20);
    const gbZ = ss(.055, -.02, d);
    if (gb.v * gbZ > .02) {
      const bc = ramp(P.grass, cl(.36 + gb.q * .30 + fr(gb.id * 7.7) * .18, 0, 1));
      tint(bc[0], bc[1], bc[2], cl(gb.v * gbZ * .65, 0, 1)); h += gb.v * gbZ * .12;
    }
    /* tension gash — the sod is parting a metre back from the brow */
    const gash = band(d, -.13, -.05, .035) * ss(.50, .80, fbmA(s, t, 3, 1, sd + 7, 3)) * pk([.12, .60, 1.0], st);
    if (gash > .01) { const sc = ramp(Q.till, .22 + gm * .34); tint(sc[0], sc[1], sc[2], gash * .70); h -= gash * .32; }

    /* the lip in section: turf crown, dark root mat, raw soil under it. Kept on
       the soil ramp rather than the till ramp — full till chroma here reads as a
       chocolate stripe laid across the picture. */
    const lip = band(d, -.015, under, .028);
    let lipK = 0;
    if (lip > .01) {
      const rootT = ss(-.012, .028, d), soilT = ss(under * .45, under, d);
      const so = ramp(P.soil, cl(.30 + gm * .28 - rootT * .12, 0, 1));
      const tc = ramp(Q.till, cl(.30 + gm * .30, 0, 1));
      lipK = lip * cl(rootT * 1.4, 0, 1) * pk([.62, 1.0, 1.0], st);
      tint(mix(so[0], tc[0], soilT * .62), mix(so[1], tc[1], soilT * .62), mix(so[2], tc[2], soilT * .62), lipK);
      const rt = ss(.55, .85, fbmA(s, t, 120, 26, sd + 8, 2)) * band(d, .0, under * .9, .02);
      if (rt > .01) tint(170, 156, 128, rt * .40);
      h = mix(h, .34 - soilT * .16, lipK);
    }
    /* hanging turf mats: only where a patch has actually slumped, so most of the
       face below the lip stays transparent and shows the real cliff material */
    const matZ = ss(.62, .90, fbmA(s, t, 4, 2, sd + 13, 3)) * pk([.20, .70, 1.10], st);
    const mats = ss(.62, .88, fbmA(s, t, 9, 5, sd + 10, 3)) * ss(.34, under * .8, d) * matZ;
    if (mats > .02) {
      const mc = ramp(P.grass, cl(.20 + gm * .24, 0, 1));
      const dc = ramp(P.straw, cl(.16 + gm * .30, 0, 1));
      const dead = ss(.08, .30, d);
      tint(mix(mc[0], dc[0], dead), mix(mc[1], dc[1], dead), mix(mc[2], dc[2], dead), cl(mats * .92, 0, 1));
      h = mix(h, .58 + gm * .14, cl(mats * .90, 0, 1));
    }
    /* roots trailing out from under the mats only, never across the open face */
    const roots = ss(.76, .96, fbmA(s, t, 90, 10, sd + 11, 2)) * band(d, under * .6, .30, .06)
      * ss(.44, .78, fbmA(s, t, 5, 2, sd + 12, 3)) * pk([.25, .70, 1.05], st);
    if (roots > .01) { tint(96, 74, 54, roots * .62); h += roots * .09; }
    /* clods and turf blocks caught on the way down. Angular and rare: a Worley
       radius test alone gives a field of perfect polka dots. */
    const clod = worleyB(s, t, 20, 10, sd + 14, .95);
    let clodK = 0;
    const cz = band(d, .12, .82, .14) * ss(.52, .84, fbmA(s, t, 4, 2, sd + 15, 3)) * pk([.18, .55, 1.0], st);
    if (cz > .04 && fr(clod.id * 17.3) < .30 * cz) {
      const nf = 4 + ((fr(clod.id * 3.1) * 3) | 0);
      const ang = Math.atan2(clod.dy, clod.dx);
      const rr = .30 * (.55 + .55 * fr(clod.id * 9.7)) * (.80 + .30 * SN(nf * ang + fr(clod.id * 5.9) * 6.28));
      if (clod.d1 < rr) {
        clodK = ss(rr, rr * .72, clod.d1) * .92 * cz;
        const cc = fr(clod.id * 7.9) < .45 ? ramp(P.grass, .20 + fr(clod.id * 3.3) * .22)
          : ramp(Q.till, .26 + fr(clod.id * 3.3) * .30);
        tint(cc[0], cc[1], cc[2], clodK); h = mix(h, .62, clodK);
      }
    }
    mul(.974 + .050 * hash(px, py, sd + 19));
    const aSward = ss(0, .16, t) * ss(.02, -.045, d);
    OUT.a = cl(Math.max(aSward, lipK, gash * .85, cl(mats * .95, 0, 1), roots * .75, clodK) * ss(1.0, .88, t), 0, 1);
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* --------------------------------------------------------------------- SCARP
     The low erosion scarp cut into a coastal field — turf brow, raw rilled till
     face, slump fan spreading onto the beach. The whole edge is two to three
     metres of drop, so it fits inside the strip with the fan.
       Lo  an old scarp, grassing over
       Mid a working scarp with a fan at the toe
       Hi  actively failing — deep gullies, big slump blocks, driftwood at the foot */
  function edgeScarp(s, t, sd, px, py, st) {
    const wob = (fbmA(s, t, 3, 2, sd + 1, 3) - .5) * .20 + (fbmA(s, t, 8, 3, sd + 2, 2) - .5) * .07;
    const e = .24 + wob * pk([.55, 1.0, 1.3], st);
    const toeD = pk([.24, .34, .46], st) + (fbmA(s, t, 4, 2, sd + 3, 2) - .5) * .12;
    const d = t - e, dt = d - toeD;
    const fine = fbm(s, t, 70, sd + 9, 2), gm = fbm(s, t, 24, sd + 5, 3);

    /* the field above: turf with bare red ground showing through it */
    const bareN = ss(.44, .78, fbm(s, t, 6, sd + 6, 3)) * pk([.35, .62, .82], st);
    const gc = ramp(P.grass, cl(.28 + fbm(s, t, 10, sd + 4, 3) * .30 + (fine - .5) * .12, 0, 1)).slice();
    const dc = ramp(Q.till, cl(.30 + gm * .32, 0, 1));
    set([mix(gc[0], dc[0], bareN), mix(gc[1], dc[1], bareN), mix(gc[2], dc[2], bareN)]);
    let h = .52 + gm * .10;
    const gb = blades(s, t, 38, sd + 7, .48 * (1 - bareN * .8), 1.3, 1.5, .13, .18);
    if (gb.v > .02) {
      const bc = ramp(P.grass, cl(.34 + gb.q * .30 + fr(gb.id * 7.7) * .18, 0, 1));
      tint(bc[0], bc[1], bc[2], cl(gb.v * .62, 0, 1)); h += gb.v * .10;
    }
    /* overhanging brow */
    const lip = band(d, -.03, .05, .03);
    if (lip > .01) {
      const so = ramp(P.soil, cl(.26 + gm * .30, 0, 1));
      tint(so[0], so[1], so[2], lip * .85); h = mix(h, .36, lip * .85);
      const rt = ss(.58, .88, fbmA(s, t, 110, 22, sd + 8, 2)) * band(d, .0, .06, .02);
      if (rt > .01) tint(172, 156, 126, rt * .45);
    }
    /* the raw till face, rilled DOWN it — but only in patches. Rills that run the
       full height everywhere turn the scarp into a palisade fence; a real face is
       mostly slumped ground with a few incised gullies in it. */
    const face = band(d, .03, toeD - .01, .035);
    let relief = 0;
    if (face > .01) {
      const gz = ss(.38, .78, fbmA(s, t, 6, 2, sd + 25, 3));
      const r1 = ss(.52, .94, ridA(s, t, 64, 6, sd + 20, 4));
      const r2 = ss(.62, .97, ridA(s, t, 128, 10, sd + 21, 3));
      const r3 = ss(.70, .99, ridA(s, t, 26, 4, sd + 22, 3));
      relief = (r1 * .34 + r2 * .20 + r3 * .46) * gz * pk([.30, .80, 1.25], st);
      /* horizontal slump benches — the till comes away in sheets, and the ledges
         left behind are what break the vertical grain */
      const bench = (fbmA(s, t, 2, 9, sd + 26, 3) - .5) * .30 + (fbmA(s, t, 3, 18, sd + 27, 2) - .5) * .14;
      const fc = ramp(Q.till, cl(.30 + gm * .22 + fine * .10 + bench - relief * .20, 0, 1));
      tint(fc[0], fc[1], fc[2], face);
      h = mix(h, cl(.54 - relief * .30 + bench * .8 + gm * .08, 0, 1), face);
      /* stones and clods standing out of the face, dusted red so they sit in it */
      const sw = worleyA(s, t, 60, 30, sd + 23, .95);
      if (fr(sw.id * 19.7) < .16 && sw.d1 < .34) {
        const k = ss(.34, .12, sw.d1) * face;
        const sc = ramp(Q.rock, .24 + fr(sw.id * 7.3) * .34);
        tint(sc[0], sc[1], sc[2], k * .80); tint(126, 88, 62, k * .22); h += k * .16;
      }
      /* roots trailing out of the top of the face */
      const rz = ss(.70, .94, fbmA(s, t, 90, 12, sd + 24, 2)) * band(d, .03, .16, .05);
      if (rz > .01) { tint(98, 76, 54, rz * .6); h += rz * .08; }
    }
    /* the slump fan at the toe: loose till in lobes, turf blocks riding on it */
    const fanReach = pk([.10, .20, .32], st) + (fbmA(s, t, 5, 2, sd + 30, 2) - .5) * .14;
    const fan = band(dt, .0, fanReach, .055);
    if (fan > .01) {
      const lobe = fbmA(s, t, 7, 4, sd + 31, 3);
      const fc = ramp(Q.till, cl(.34 + lobe * .26 + fine * .12, 0, 1));
      tint(fc[0], fc[1], fc[2], fan * .95);
      h = mix(h, .40 + lobe * .16, fan * .95);
      const bw = worleyA(s, t, 22, 12, sd + 32, .95);
      if (fr(bw.id * 13.9) < .26 * pk([.3, .8, 1.2], st) && bw.d1 < .38) {
        const k = ss(.38, .14, bw.d1) * fan;
        const bc = fr(bw.id * 5.7) < .5 ? ramp(P.grass, .20 + fr(bw.id * 3.1) * .20)
          : ramp(Q.rock, .24 + fr(bw.id * 3.1) * .30);
        tint(bc[0], bc[1], bc[2], k * .90); h = mix(h, .60, k * .9);
      }
      /* Hi: bleached sticks washed against the foot of the scarp */
      if (st === 2) {
        const dwv = blades(s, t, 9, sd + 33, fan * .28, .9, 1.3, .18, 2.9);
        if (dwv.v > .04) {
          const wc = ramp(R.drift, cl(.26 + fr(dwv.id * 7.7) * .42, 0, 1));
          tint(wc[0], wc[1], wc[2], cl(dwv.v * .95, 0, 1)); h += dwv.v * .18;
        }
      }
    }
    mul(.974 + .050 * hash(px, py, sd + 19));
    const aField = ss(0, .16, t) * ss(.02, -.05, d);
    OUT.a = cl(Math.max(aField, lip * .9, face, fan * .95) * ss(1.0, .90, t), 0, 1);
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* --------------------------------------------------------------------- WRACK
     The strand line. Dried rockweed rope, eelgrass ribbon, shell, sticks, and a
     smear of fine organic staining either side of it.
       Lo  a faint scatter of flecks
       Mid one continuous rope
       Hi  a storm line — two or three berms, heavy weed, driftwood
     Logs are props, not texture; only chips and sticks live in here. */
  function edgeWrack(s, t, sd, px, py, st) {
    const wob = (fbmA(s, t, 3, 1, sd + 1, 3) - .5) * .28 + (fbmA(s, t, 9, 2, sd + 2, 2) - .5) * .09;
    const e = .46 + wob;
    const d = t - e;
    const wide = pk([.035, .075, .130], st);
    let line = band(d, -wide, wide, .05);
    if (st > 0) {
      const o2 = -(pk([0, .15, .21], st)) + (fbmA(s, t, 4, 1, sd + 3, 2) - .5) * .09;
      line = Math.max(line, band(d - o2, -wide * .48, wide * .48, .045) * pk([0, .55, .82], st));
    }
    if (st === 2) {
      const o3 = .19 + (fbmA(s, t, 5, 2, sd + 4, 2) - .5) * .09;
      line = Math.max(line, band(d - o3, -wide * .38, wide * .38, .045) * .62);
    }
    const fine = fbm(s, t, 80, sd + 9, 2);
    set(ramp(P.sand, cl(.34 + fbm(s, t, 8, sd + 5, 3) * .24 + fine * .16, 0, 1)));
    let h = .38 + fine * .08, a = 0;

    const smear = band(d, -wide * 2.0, wide * 2.2, .08) * ss(.46, .82, fbmA(s, t, 5, 2, sd + 6, 3)) * pk([.7, 1.0, 1.2], st);
    if (smear > .01) { tint(72, 60, 44, smear * .40); a = Math.max(a, smear * .20); }

    /* the rope itself has to have BODY. Fibre is value inside a solid ribbon, not
       coverage — drive coverage with it and the strand line comes out as dry-brush
       scratches with the beach showing through. */
    const clumpy = ss(.26, .80, fbmA(s, t, 11, 4, sd + 16, 3));
    const body = cl(line * (.30 + 1.05 * clumpy), 0, 1);
    if (body > .01) {
      const fib = fbmA(s, t, 70, 22, sd + 8, 3) * .66 + fbmA(s, t, 140, 40, sd + 17, 2) * .34;
      const lt = ramp(Q.wrack, cl(.02 + fib * .38, 0, 1));
      const dk = ramp(R.fucus, cl(.04 + fib * .40, 0, 1));
      const k = ss(.54, .90, fib);
      tint(mix(dk[0], lt[0], k), mix(dk[1], lt[1], k), mix(dk[2], lt[2], k), body);
      h += body * (.14 + fib * .16); a = Math.max(a, body);
    }
    /* eelgrass ribbons — short and broad. Long thin ones at this lean become
       forty-texel scratches that run right out of the line. */
    const eg = blades(s, t, 20, sd + 10, body * pk([.35, .70, .95], st), .55, .70, .30, 2.4);
    if (eg.v > .02) {
      const ec = ramp(P.straw, cl(.24 + eg.q * .34 + fr(eg.id * 7.3) * .22, 0, 1));
      const k = cl(eg.v * .95, 0, 1);
      tint(ec[0], ec[1], ec[2], k); h += eg.v * .14; a = Math.max(a, k);
    }
    /* shell fragments and small chips of wood */
    const sw = worleyA(s, t, 130, 66, sd + 11, .95);
    if (fr(sw.id * 23.1) < .22 * line && sw.d1 < .30) {
      const k = ss(.30, .10, sw.d1) * line;
      const cc = fr(sw.id * 3.9) < .58 ? ramp(P.stoneGrey, .58 + fr(sw.id * 11.1) * .38)
        : ramp(R.drift, .30 + fr(sw.id * 11.1) * .50);
      tint(cc[0], cc[1], cc[2], k * .90); h += k * .16; a = Math.max(a, k * .9);
    }
    if (st === 2) {
      const dwv = blades(s, t, 8, sd + 12, line * .34, .7, 1.0, .34, 2.8);
      if (dwv.v > .04) {
        const wc = ramp(R.drift, cl(.28 + fr(dwv.id * 7.7) * .48, 0, 1));
        const k = cl(dwv.v * 1.1, 0, 1);
        tint(wc[0], wc[1], wc[2], k); h += dwv.v * .22; a = Math.max(a, k);
      }
    }
    mul(.972 + .054 * hash(px, py, sd + 19));
    OUT.a = cl(a * ss(0, .08, t) * ss(1.0, .92, t), 0, 1);
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ------------------------------------------------------------------ WEEDLINE
     The upper limit of the weed on a rock platform: dry lichened rock, a black
     Verrucaria band, a white barnacle band, then the canopy closing below. The
     sharpest natural line on the whole coast, and the one that tells the player
     where the tide gets to.
       Lo  a high, thin, hard line — an exposed headland
       Mid the normal zonation
       Hi  broad and ragged — a sheltered cove, weed running high, Ulva in the wet */
  function edgeWeedline(s, t, sd, px, py, st) {
    const wob = (fbmA(s, t, 3, 1, sd + 1, 3) - .5) * .24 + (fbmA(s, t, 8, 3, sd + 2, 2) - .5) * .09;
    const e = .34 + wob * pk([.45, 1.0, 1.45], st);
    const d = t - e;
    const rk = fbm(s, t, 9, sd + 3, 4), rkf = fbm(s, t, 44, sd + 4, 2);
    const cw0 = worleyA(s, t, 7, 4, sd + 5, .9);
    const gully = ss(.14, .02, cw0.d2 - cw0.d1);
    set(ramp(Q.rock, cl(.22 + rk * .32 + rkf * .14 - gully * .14, 0, 1)));
    let h = .40 + rk * .20 - gully * .20, a = 0;

    /* above: crustose lichen going to a black band right at the splash limit.
       Both are TINTS on whatever rock is underneath — push the alpha up here and
       the decal lays its own slab of rock over the platform. */
    const dry = ss(.04, -.18, d);
    if (dry > .01) {
      const li = ss(.52, .84, fbm(s, t, 16, sd + 6, 3)) * dry;
      const lc = ramp(R.lichen, .26 + rkf * .50);
      tint(lc[0], lc[1], lc[2], li * .42);
      a = Math.max(a, li * .24);
    }
    const vb = band(d, -.20, -.08, .05) * ss(.42, .78, fbm(s, t, 20, sd + 7, 3)) * pk([1.1, 1.0, .8], st);
    if (vb > .01) { tint(30, 28, 24, vb * .55); a = Math.max(a, vb * .42); }

    /* the barnacle band — a genuine white line, and the reason the edge reads */
    const bb = band(d, pk([-.030, -.045, -.070], st), pk([.030, .045, .070], st), .028) * pk([1.0, .92, .68], st);
    if (bb > .01) {
      tint(150, 142, 128, bb * .10);
      a = Math.max(a, bb * .14);
      const bw = worleyA(s, t, 120, 60, sd + 8, 1);
      if (fr(bw.id * 13.3) < .70) {
        const bk = ss(.40, .10, bw.d1) * bb;
        if (bk > .005) {
          const bc = ramp(R.barn, .38 + fr(bw.id * 29.7) * .52);
          tint(bc[0], bc[1], bc[2], bk * .95); h += bk * .12; a = Math.max(a, bk * .98);
        }
      }
    }
    /* below: fucus tufts thickening into the canopy, handing off to Rockweed */
    const clumpN = ss(.30, .72, fbmA(s, t, 6, 3, sd + 30, 3));
    const wz = cl(ss(-.02, .34, d) * (.15 + 1.05 * clumpN) * pk([.62, 1.0, 1.22], st), 0, 1);
    if (wz > .02) {
      const dirSel = ss(.42, .58, fbm(s, t, 2, sd + 14, 2));
      const bsx = s + (fbmA(s, t, 4, 2, sd + 11, 2) - .5) * .05;
      const fn = mix(fbmA(bsx, t, 26, 48, sd + 12, 2), fbmA(bsx + t, t, 26, 48, sd + 13, 2), dirSel) * .64
        + fbmA(bsx, t, 46, 86, sd + 20, 1) * .36;
      const mat = ss(.40, .74, fn) * wz;
      const wc = ramp(R.asco, cl(.16 + mat * .44 + fbm(s, t, 6, sd + 15, 3) * .22, 0, 1));
      tint(wc[0], wc[1], wc[2], cl(mat * .92, 0, 1));
      h += mat * .22; a = Math.max(a, cl(mat * .95, 0, 1));
      const lean = 1.5 + (fbm(s, t, 3, sd + 10, 2) - .5) * 2.4;
      const b1 = blades(s, t, 22, sd + 16, wz * pk([.9, 1.4, 1.8], st), 1.1, 1.3, .19, lean);
      if (b1.v > .02) {
        const fc = ramp(R.asco, cl(.20 + b1.q * .42 + fr(b1.id * 11.3) * .24, 0, 1));
        const k = cl(b1.v * 1.1, 0, 1);
        tint(fc[0], fc[1], fc[2], k); h += b1.v * .26; a = Math.max(a, k);
      }
      const b2 = blades(s, t, 28, sd + 17, wz * .9, .7, .9, .16, -lean * .7);
      if (b2.v > .02) {
        const fc2 = ramp(R.fucus, cl(.18 + b2.q * .46 + fr(b2.id * 5.3) * .20, 0, 1));
        const k = cl(b2.v * .95, 0, 1);
        tint(fc2[0], fc2[1], fc2[2], k); h += b2.v * .18; a = Math.max(a, k);
      }
      if (st === 2) {
        const uz = ss(.60, .88, fbm(s, t, 11, sd + 18, 3)) * wz * (.4 + .6 * gully);
        if (uz > .02) { const uc = ramp(R.ulva, .24 + rkf * .52); tint(uc[0], uc[1], uc[2], cl(uz * .68, 0, 1)); a = Math.max(a, uz * .7); }
      }
    }
    mul(.972 + .056 * hash(px, py, sd + 19));
    OUT.a = cl(a * ss(0, .10, t) * ss(1.0, .78, t), 0, 1);
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* -------------------------------------------------------------------- baking */
  const MATS = { foreshore, ledge, rockweed, talus };
  const CFG = {
    foreshore: { cav: .32, crown: .15, r: 3, lo: 28, hi: 224, flat: .62, flatR: .32 },
    ledge: { cav: .30, crown: .26, r: 5, lo: 22, hi: 222, flat: .48, flatR: .38 },
    rockweed: { cav: .28, crown: .14, r: 3, lo: 20, hi: 214, flat: .80, flatR: .26 },
    talus: { cav: .40, crown: .22, r: 4, lo: 20, hi: 228, flat: .60, flatR: .28 }
  };
  const SPEC = {
    foreshore: { N: 512, m: 16, seed: 13411 },
    talus: { N: 512, m: 16, seed: 14503 },
    ledge: { N: 256, m: 8, seed: 15607 },
    rockweed: { N: 256, m: 8, seed: 16703 }
  };
  Object.assign(T.MATS, MATS);
  Object.assign(T.CFG, CFG);
  Object.assign(T.SPEC, SPEC);

  const EDGES = { turf: edgeTurf, scarp: edgeScarp, wrack: edgeWrack, weedline: edgeWeedline };
  const ECFG = {
    turf: { cav: .30, crown: .15, r: 3, lo: 22, hi: 220 },
    scarp: { cav: .32, crown: .17, r: 3, lo: 22, hi: 218 },
    wrack: { cav: .30, crown: .16, r: 3, lo: 24, hi: 226 },
    weedline: { cav: .28, crown: .15, r: 3, lo: 20, hi: 222 }
  };
  /* 256 x 128 = 8 m along shore x 4 m across, at the same 32 px/m */
  const ESPEC = {
    turf: { W: 256, H: 128, mS: 8, mT: 4, seed: 17807 },
    scarp: { W: 256, H: 128, mS: 8, mT: 4, seed: 18911 },
    wrack: { W: 256, H: 128, mS: 8, mT: 4, seed: 19013 },
    weedline: { W: 256, H: 128, mS: 8, mT: 4, seed: 20117 }
  };

  function blurWrapR(src, W, H, r) {
    const rx = Math.min(r, (W >> 1) - 1), ry = Math.min(r, (H >> 1) - 1);
    const tmp = new Float32Array(W * H), dst = new Float32Array(W * H);
    const ix = 1 / (2 * rx + 1), iy = 1 / (2 * ry + 1);
    for (let y = 0; y < H; y++) {
      const row = y * W; let acc = 0;
      for (let i = -rx; i <= rx; i++) acc += src[row + (((i % W) + W) % W)];
      for (let x = 0; x < W; x++) { tmp[row + x] = acc * ix; acc += src[row + (x + rx + 1) % W] - src[row + ((((x - rx) % W) + W) % W)]; }
    }
    for (let x = 0; x < W; x++) {
      let acc = 0;
      for (let i = -ry; i <= ry; i++) acc += tmp[(((i % H) + H) % H) * W + x];
      for (let y = 0; y < H; y++) { dst[y * W + x] = acc * iy; acc += tmp[((y + ry + 1) % H) * W + x] - tmp[((((y - ry) % H) + H) % H) * W + x]; }
    }
    return dst;
  }

  /* Edges get the cavity/crown pass and the luminance clamp, but no mean-flatten:
     an edge is meant to be a low-frequency event, and levelling it away is the
     one thing that would destroy it. */
  function bakeEdge(name, step, createCanvas) {
    const fn = EDGES[name], cfg = ECFG[name], sp = ESPEC[name];
    const W = sp.W, H = sp.H, seed = sp.seed, st = step | 0, n = W * H;
    const Hf = new Float32Array(n), Rf = new Float32Array(n), Gf = new Float32Array(n), Bf = new Float32Array(n), Af = new Float32Array(n);
    for (let y = 0; y < H; y++) {
      const t = (y + .5) / H;
      for (let x = 0; x < W; x++) {
        OUT.a = 1;
        const o = fn((x + .5) / W, t, seed, x, y, st), i = y * W + x;
        Hf[i] = o.h; Rf[i] = o.r; Gf[i] = o.g; Bf[i] = o.b; Af[i] = cl(o.a, 0, 1);
      }
    }
    const soft = blurWrapR(Hf, W, H, cfg.r);
    for (let i = 0; i < n; i++) {
      const dd = Hf[i] - soft[i];
      const k = dd < 0 ? 1 + dd * 2.6 * cfg.cav : 1 + dd * 2.2 * cfg.crown;
      Rf[i] *= k; Gf[i] *= k; Bf[i] *= k;
    }
    const ca = createCanvas(W, H), ctx = ca.getContext('2d'), im = ctx.createImageData(W, H), D = im.data;
    for (let i = 0; i < n; i++) {
      let r = Rf[i], g = Gf[i], b = Bf[i];
      const l = .299 * r + .587 * g + .114 * b;
      if (l > 1e-3) { const tl = cl(l, cfg.lo, cfg.hi); if (tl !== l) { const k = tl / l; r *= k; g *= k; b *= k; } }
      const o = i * 4;
      D[o] = cl(r, 0, 255) | 0; D[o + 1] = cl(g, 0, 255) | 0; D[o + 2] = cl(b, 0, 255) | 0;
      D[o + 3] = cl(Af[i] * 255, 0, 255) | 0;
    }
    ctx.putImageData(im, 0, 0);
    return { albedo: ca, W: W, H: H };
  }

  return { MATS, CFG, SPEC, EDGES, ECFG, ESPEC, bakeEdge, blurWrapR, worleyB, band, R };
})();
