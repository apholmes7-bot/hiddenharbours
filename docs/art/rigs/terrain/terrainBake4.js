/* terrainBake4.js — pass five: the reef beds. Load AFTER terrainBake.js, 2 and 3.

   Four biogenic beds and one edge strip. Everything a shellfish bed builds is a
   REEF in the ecological sense — the animals are the substrate — so all four are
   plan-view ground materials on the same three-step intensity ladder, and the
   ladder is how far the bed has built:

     Musselbed   512  16 m   blue mussel on a soft flat
     Oysterreef  512  16 m   American oyster, cemented into clusters
     Eelgrass    256   8 m   Zostera meadow on muddy sand — the soft-bottom bed
     Irishmoss   256   8 m   Chondrus turf on red cobble — the hard-bottom bed
     Reefedge    256x128     RGBA decal: the margin, its scour moat and rubble apron

   SCALE IS THE WHOLE PROBLEM HERE. At 32 px/m a mussel is two texels long and an
   oyster three. None of these can be drawn shell by shell; what reads is the
   GRAIN (2-4 texel flecks sharing a lie), the CLUMPING (0.5-2 m hummocks and
   clusters) and the GAPS (mud the bed has not closed over).

   And one rule this pass paid for twice: COVER IS A PER-SHELL DECISION, NOT AN
   ALPHA. Gating a bed with a smooth noise field and using it as tint opacity
   gives a translucent wash of shell colour over mud — which is exactly what mud
   with nothing on it looks like. Decide per Worley cell whether a shell is
   there, draw it opaque, and let the gaps be gaps.

   Same hard rules as every other pass: no key light, no cast shadow, no water,
   no pools, no wetness. A drained pan on a mussel bed is baked as MUD — the
   engine puts the water in.
*/
globalThis.TB4 = (function () {
  const T = globalThis.TB, T2 = globalThis.TB2, T3 = globalThis.TB3;
  if (!T || !T2 || !T3) throw new Error('Art/terrainBake.js, 2 and 3 must be loaded first');
  const hash = T.hash, fbm = T.fbm, fbmA = T.fbmA, rfbm = T.rfbm;
  const worley = T.worley, worleyA = T.worleyA, blades = T.blades;
  const ss = T.ss, ramp = T.ramp, mkRamp = T.mkRamp, P = T.P, Q = T2.Q, R3 = T3.R;
  const set = T.set, tint = T.tint, mul = T.mul, OUT = T.OUT;
  const mix = T.mix, cl = T.cl, fr = T.fr, pk = T.pk;
  const C = Math.cos, PI = Math.PI;

  /* ------------------------------------------------------------------ palettes
     Sampled off the reference photography and pushed to run the full width of
     the grade-safe window. The two shellfish ramps are deliberately far apart in
     hue — a mussel bed is blue-black and an oyster reef is khaki-olive — because
     that difference is the only thing separating them at a hundred texels. */
  const R = {
    /* Mytilus edulis: navy-black periostracum going grey-lilac where it wears
       off at the umbo, and a genuinely pale top, or a closed bed comes out as a
       hole in the terrain. */
    mussel: mkRamp([[0, '#111419'], [.16, '#1a202a'], [.34, '#252c39'], [.52, '#363b49'], [.70, '#4d4f5a'], [.86, '#706d74'], [1, '#9a9599']]),
    /* older shells with the periostracum eroded off — bronze and dull gold */
    musselOld: mkRamp([[0, '#1d1710'], [.3, '#3b2d1a'], [.58, '#5d472a'], [.8, '#82653e'], [1, '#a98a5c']]),
    /* the bed's own floor: anoxic mud and pseudofaeces bound up with byssus.
       Black-brown, never grey — a grey floor makes the bed read as gravel. */
    bedMud: mkRamp([[0, '#131210'], [.24, '#221f18'], [.46, '#342d22'], [.68, '#473d2e'], [.86, '#5b4e3c'], [1, '#72624c']]),
    /* dead valves and shell hash — the cultch. Bleached, faintly blue. */
    cultch: mkRamp([[0, '#565349'], [.32, '#807a6c'], [.62, '#a49c8b'], [.85, '#c5bca7'], [1, '#e0d6be']]),
    /* Crassostrea virginica with the film of diatom and green algae that a living
       reef always carries. Khaki-olive — a white oyster reef is a museum shell. */
    oyster: mkRamp([[0, '#242419'], [.16, '#3d3c24'], [.34, '#585534'], [.52, '#746e45'], [.70, '#918860'], [.85, '#b0a67e'], [1, '#d2c8a2']]),
    /* fresh break and the growing lip — chalk with no film on it yet */
    oysterFresh: mkRamp([[0, '#6f6a58'], [.36, '#9d9680'], [.7, '#c2bba1'], [1, '#e6dfc3']]),
    /* Zostera marina: spring green in the new blade, olive in the old one */
    zostera: mkRamp([[0, '#162114'], [.2, '#233419'], [.4, '#334b20'], [.58, '#466228'], [.76, '#5d7b34'], [.9, '#7a9548'], [1, '#9caf66']]),
    /* epiphyte crust and last year's blades, going straw */
    zosOld: mkRamp([[0, '#3b311c'], [.4, '#675833'], [.72, '#8f7d4f'], [1, '#b4a372']]),
    /* Chondrus crispus: dark wine-purple in the shade going olive-bronze where
       the sun has bleached it, and that spread IS the material */
    moss: mkRamp([[0, '#140a11'], [.18, '#23101a'], [.36, '#351722'], [.52, '#472028'], [.68, '#5d2f2e'], [.84, '#7a4839'], [1, '#9c6c4c']]),
    mossBleach: mkRamp([[0, '#3a3418'], [.4, '#6a5f2c'], [.72, '#928149'], [1, '#b7a26a']])
  };

  /* ================================================================ MUSSELBED
     Blue mussel on a soft flat: a crust of byssus-bound shells standing on end,
     hummocked into low mounds, with drainage channels of black mud between.
       Lo  spat and scattered clumps on a mud floor
       Mid a closed bed
       Hi  thick and hummocked, mud mounded into it, dead shell in the troughs
     Shell lie is per-PATCH, selected between two lattices by a low-frequency
     region test — the same trick that keeps Ripple from weaving. Blending the
     two lattices instead of selecting gives a plaid. */
  function musselbed(s, t, sd, px, py, st) {
    const big = fbm(s, t, 2, sd + 1, 3);
    /* hummocks: 1-3 m mounds of bed. Low-frequency SIGNAL, so this material is
       mean-flattened only lightly. */
    const hum = fbm(s, t, 5, sd + 3, 4) * .62 + fbm(s, t, 9, sd + 4, 3) * .38;
    /* channels the bed never closed over, where the flat drains */
    const chan = ss(.76, .97, rfbm(s, t, 3, sd + 5, 4)) * pk([1.25, 1.0, .80], st);

    /* ---- floor: anoxic mud, pelletal at grain scale ---- */
    const mv = cl(.44 + (fbm(s, t, 10, sd + 20, 3) - .5) * .42 + (fbm(s, t, 70, sd + 21, 2) - .5) * .26
      + (big - .5) * .24 - chan * .14, 0, 1);
    set(ramp(R.bedMud, mv));
    let h = .32 + mv * .10 + (hum - .5) * pk([.10, .26, .42], st) - chan * .24;

    /* ---- the shells ----
       Two anisotropic lattices, 4 x 2 texels at the base step. worleyA with its
       arguments swapped gives the same statistics rotated 90 degrees, and both
       wrap, so a patch boundary shows only as a change of lie — which is what a
       shear boundary in a real bed looks like. */
    const cN = fbm(s, t, 3, sd + 10, 4) * .58 + fbm(s, t, 8, sd + 11, 3) * .42;
    /* the fraction of cells that carry a shell. Clumped, and never a partial
       alpha: a shell is there or it is not. */
    const frac = cl(pk([.46, .93, 1.02], st) * (.30 + .82 * ss(.34, .66, cN)) * (1 - chan * .92), 0, 1);
    const CX = pk([168, 132, 108], st), CY = pk([330, 264, 216], st);
    const alongS = fbm(s, t, 2, sd + 30, 2) < .5;
    const w = alongS ? worleyA(s, t, CX, CY, sd + 31, .92) : worleyA(t, s, CX, CY, sd + 32, .92);
    const wid = w.id, wd1 = w.d1, wgap = w.d2 - w.d1;
    const here = fr(wid * 7.13) < frac;
    /* the shell FILLS its cell — a bed is packed. Leave it as a small ellipse in
       the middle of the cell and 512 texels of it read as spilled rice. */
    const shell = here ? ss(.95, .22, wd1) : 0;
    if (shell > .02) {
      const iv = fr(wid * 11.7), iv2 = fr(wid * 53.1);
      /* value inside one shell: dark at the margin, lifting toward the crown, and
       kept in the bottom half of the ramp — the cavity pass is what brings the
       crowns up. Nothing is outlined. */
      const body = cl(.14 + iv * .36 + (1 - ss(.50, .06, wd1)) * .12 + (fbm(s, t, 170, sd + 33, 2) - .5) * .14, 0, 1);
      let c;
      if (iv2 < pk([.40, .30, .22], st)) c = ramp(R.musselOld, cl(body * .90 + .06, 0, 1));  /* worn to bronze */
      else if (iv2 > .975) c = ramp(R.cultch, cl(body * .60 + .14, 0, 1));                   /* a dead valve in the bed */
      else c = ramp(R.mussel, body);
      tint(c[0], c[1], c[2], shell);
      h += shell * (.30 + iv * .14) - ss(.05, 0, wgap) * .12 * shell;

      /* barnacle and spat on the older shells. Sparse and dull: bright dots one
         texel across are the fastest way to turn any material into a starfield. */
      const bw = worley(s, t, 300, sd + 34, 1);
      if (fr(bw.id * 7.9) < pk([.050, .026, .015], st) && bw.d1 < .30) {
        const k = ss(.30, .08, bw.d1) * shell;
        const bc = ramp(R3.barn, .18 + fr(bw.id * 23.3) * .34);
        tint(bc[0], bc[1], bc[2], k * .50); h += k * .10;
      }
    } else {
      /* gravel and half-buried dead valves on the open floor — what a bed starts
         on, and what is left where one has been ripped out */
      const gv = worley(s, t, 150, sd + 22, 1);
      const gid = fr(gv.id * 17.3);
      if (gid < pk([.16, .10, .07], st) && gv.d1 < .32) {
        const k = ss(.32, .10, gv.d1);
        const c2 = ramp(gid < .05 ? R.cultch : P.stoneGrey, .14 + fr(gid * 41.7) * .40);
        tint(c2[0], c2[1], c2[2], k * .70); h += k * .12;
      }
    }
    /* gut weed and Ulva in the damp seams between clumps, never on the crowns */
    const ug = ss(.60, .90, fbm(s, t, 14, sd + 35, 3)) * ss(.52, .24, h) * pk([.35, .70, .95], st);
    if (ug > .01) { const uc = ramp(R3.ulva, .20 + fbm(s, t, 40, sd + 36, 2) * .44); tint(uc[0], uc[1], uc[2], cl(ug * .34, 0, 1)); }

    /* Hi: mud thrown up into the bed and mounded around the clumps. A thick bed
       traps its own sediment, and that is what makes an old one read as a mound
       rather than as a scatter of shells. */
    if (st === 2) {
      const dr = ss(.46, .86, fbm(s, t, 7, sd + 40, 3)) * ss(.60, .28, h);
      if (dr > .01) { const dc = ramp(R.bedMud, .16 + fbm(s, t, 30, sd + 41, 2) * .32); tint(dc[0], dc[1], dc[2], dr * .52); }
    }
    mul(.972 + .056 * hash(px, py, sd + 90));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* =============================================================== OYSTERREEF
     American oyster. Unlike mussel, oyster CEMENTS: the reef is a field of
     knuckled clusters half a metre across, with mud channels winding between
     them, and the clusters merge into a pavement as the reef builds.
       Lo  singles and cultch scattered on the mud
       Mid a working reef — discrete clusters, open channels
       Hi  a closed reef, clusters merged, channels choked with mud
     Cluster shape comes from a Worley CELL, not a radius test around the site: a
     cemented clump packs against its neighbour, it does not sit in a puddle of
     space. (Rule 9 from the shoreline pass, applied to something alive.) */
  function oysterreef(s, t, sd, px, py, st) {
    const big = fbm(s, t, 2, sd + 1, 3), big2 = fbm(s, t, 4, sd + 2, 3);
    /* the channels are the character of a real reef: they wind, they do not scatter */
    const chan = ss(.72, .96, rfbm(s, t, 3, sd + 3, 4)) * pk([1.3, 1.0, .70], st);

    /* ---- floor: grey-brown mud with cultch trodden into it ---- */
    const fv = cl(.34 + (fbm(s, t, 9, sd + 10, 3) - .5) * .44 + (fbm(s, t, 64, sd + 11, 2) - .5) * .24 + (big - .5) * .24, 0, 1);
    set(ramp(Q.silt, fv));
    let h = .30 + fv * .12 - chan * .20;

    /* ---- the reef crust ----
       An oyster reef at 32 px/m is not a scatter of clumps on mud: it is a
       continuous knuckled crust with mud channels wound through it. So the
       CLUSTER is height and a little value, and presence is decided per SHELL,
       the same way the mussel bed decides it. Gating presence on the cluster
       cell instead gives angular 15-texel lumps — which is Talus, in khaki. */
    const cN = fbm(s, t, 3, sd + 21, 3) * .60 + fbm(s, t, 7, sd + 22, 3) * .40;
    const frac = cl(pk([.36, 1.00, 1.18], st) * (.38 + .86 * ss(.30, .66, cN)) * (1 - chan * .92), 0, 1);
    const kw = worley(s, t, pk([46, 34, 26], st), sd + 20, 1);   /* 11 / 15 / 20 texel clusters */
    const mound = ss(.72, .10, kw.d1) * pk([.55, .85, 1.0], st);

    /* two calibres of valve: a coarse lattice of full-grown oysters and a fine
       one of spat and broken shell packed between them. One calibre alone comes
       out as peas. */
    const wc = worleyA(s, t, 86, 74, sd + 26, .95);
    const coarse = fr(wc.id * 3.71) < pk([.34, .46, .56], st);
    const w = coarse ? wc : worleyA(s, t, 158, 136, sd + 23, .95);
    const sid = fr(w.id * 29.3), sd1 = w.d1, sgap = w.d2 - w.d1;
    const shellK = (fr(w.id * 5.17) < frac) ? ss(.92, .24, sd1) : 0;

    if (shellK > .02) {
      /* 3-4 texels, no fluting. A flute at this scale is a sub-texel line, and a
         sub-texel line aliases into stitching. The valve reads by its own value
         and by the cavity pass, and that is enough. */
      const fresh = fr(w.id * 71.3) < pk([.06, .09, .13], st);
      const vBase = cl(.13 + sid * .40 + mound * .14 + (fbm(s, t, 130, sd + 24, 2) - .5) * .14
        + (1 - ss(.50, .06, sd1)) * .10, 0, 1);
      const sc = ramp(fresh ? R.oysterFresh : R.oyster, vBase);
      tint(sc[0], sc[1], sc[2], shellK);
      h = mix(h, cl(.36 + mound * .34 + sid * .12 + (1 - ss(.50, .06, sd1)) * .14 - ss(.08, 0, sgap) * .22, 0, 1), shellK);
      /* the film of algae that makes a living reef khaki. A broad noise wash,
         never a cell field — tie it to the shells and every valve gets outlined. */
      const film = ss(.36, .76, fbm(s, t, 6, sd + 25, 3)) * shellK * pk([.55, .85, 1.0], st);
      if (film > .01) { const fc = ramp(R.oyster, .16 + big2 * .24); tint(fc[0], fc[1], fc[2], film * .34); }
    } else {
      /* cultch: dead valves and hash worked into the mud between the clusters */
      const hv = worley(s, t, 130, sd + 12, 1);
      const hid = fr(hv.id * 13.1);
      if (hid < pk([.30, .20, .12], st) && hv.d1 < .32) {
        const k = ss(.32, .10, hv.d1);
        const cc = ramp(R.cultch, .14 + fr(hid * 37.1) * .42);
        tint(cc[0], cc[1], cc[2], k * .72); h += k * .10;
      }
    }

    /* mud drape thrown into the reef, heaviest at Hi and always in the low ground */
    const drape = ss(.44, .84, fbm(s, t, 8, sd + 30, 3)) * ss(.56, .26, h) * pk([.45, .70, 1.05], st);
    if (drape > .01) { const dc = ramp(Q.silt, .14 + fbm(s, t, 26, sd + 31, 2) * .32); tint(dc[0], dc[1], dc[2], cl(drape * .44, 0, 1)); }
    /* a little rockweed hanging on the reef crest, the way it does on anything
       hard left standing on a flat */
    const wd = ss(.74, .95, fbm(s, t, 11, sd + 32, 3)) * ss(.46, .76, h) * pk([.30, .70, 1.0], st);
    if (wd > .01) { const wc = ramp(R3.fucus, .20 + fbm(s, t, 34, sd + 33, 2) * .42); tint(wc[0], wc[1], wc[2], cl(wd * .36, 0, 1)); }

    mul(.972 + .056 * hash(px, py, sd + 90));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ================================================================= EELGRASS
     Zostera marina over muddy sand: the soft-bottom bed, and the one that reads
     as a MEADOW rather than as a crust. A blade is 6 mm wide — a fifth of a
     texel — so the material is carried by anisotropic noise lying one way per
     patch, with bunched shoots drawn explicitly on top for legibility.
       Lo  sparse shoots on open sand
       Mid a closed meadow
       Hi  deep meadow, epiphyte crusted, loose blades drifted into it */
  function eelgrass(s, t, sd, px, py, st) {
    const big = fbm(s, t, 2, sd + 1, 3), big2 = fbm(s, t, 5, sd + 6, 3);
    /* substrate: Island sand gone muddy under the meadow. The ripple is heavily
       warped, gated and small in amplitude — an even train edge to edge is the
       single fastest way to make a flat read as corduroy (rule 7). */
    const warp = 13 * (fbm(s, t, 3, sd + 2, 3) - .5) + 4.5 * (fbm(s, t, 7, sd + 3, 2) - .5);
    const rip = .5 + .5 * C(2 * PI * (2 * s + 11 * t) + warp);
    const ripG = ss(.44, .78, fbm(s, t, 4, sd + 4, 3)) * pk([.9, .6, .35], st);
    const sv = cl(.40 + (big - .5) * .28 + (fbm(s, t, 60, sd + 5, 2) - .5) * .20 + (rip - .5) * .16 * ripG, 0, 1);
    set(ramp(R3.fsand, sv));
    /* organic silt in the meadow floor — a clean pink sand under a closed bed is
       the giveaway that nothing has been growing there */
    { const mc = ramp(Q.silt, .20 + big2 * .32); tint(mc[0], mc[1], mc[2], pk([.28, .48, .62], st)); }
    let h = .40 + (rip - .5) * .08 * ripG + (big - .5) * .10;

    /* lie: three anisotropies selected by region, so the meadow is never combed
       in one direction across a whole chunk */
    const zone = fbm(s, t, 2, sd + 10, 2);
    const dir = zone < .38 ? 0 : zone < .72 ? 1 : 2;
    const lie = dir === 0 ? fbmA(s, t, 16, 92, sd + 11, 3)
      : dir === 1 ? fbmA(s, t, 92, 16, sd + 12, 3)
        : fbmA(s + t, t - s, 18, 84, sd + 13, 3);
    /* fine striation ALONG the lie — what turns a smear of green into ribbon */
    const stri = dir === 0 ? fbmA(s, t, 34, 150, sd + 21, 2)
      : dir === 1 ? fbmA(s, t, 150, 34, sd + 22, 2)
        : fbmA(s + t, t - s, 38, 140, sd + 23, 2);

    const cN = fbm(s, t, 3, sd + 14, 4) * .60 + fbm(s, t, 7, sd + 15, 3) * .40;
    const cover = cl(pk([.18, .72, 1.06], st) * (.36 + .78 * ss(.30, .64, cN)), 0, 1);

    if (cover > .02) {
      /* the mat: blade-noise, not blades — and a near-BINARY threshold on it,
         because a half-opaque wash of green over sand is what sand with nothing
         on it looks like. Ribbons with edges first, value spread inside them
         second. Cover moves the threshold, it does not fade the ribbons. */
      const thr = .66 - cover * .30;
      const mat = cl(ss(thr, thr + .035, lie), 0, 1);
      if (mat > .01) {
        const age = fbm(s, t, 4, sd + 16, 3);
        const v = cl(.14 + lie * .44 + (stri - .5) * .30 + (fbm(s, t, 90, sd + 17, 2) - .5) * .14, 0, 1);
        const gc = ramp(R.zostera, v);
        tint(gc[0], gc[1], gc[2], mat * .96);
        /* epiphyte crust on the older blades — a dulling of the green, not a
           separate substance sitting on top of it */
        const ep = ss(.50, .84, age) * mat * pk([.30, .60, .95], st);
        if (ep > .01) { const ec = ramp(R.zosOld, .18 + v * .5); tint(ec[0], ec[1], ec[2], ep * .46); }
        h += mat * .22;
      }
      /* explicit shoots: bunches, not single blades, never below a texel wide, and
         the lean has to AGREE with the lie of the mat — upright strokes on a mat
         that runs across them is the fastest way to turn a meadow into a lawn */
      const wind = dir === 0 ? 3.0 : dir === 1 ? .20 : 1.5;
      const bl = blades(s, t, 22, sd + 18, cl(cover * pk([.45, .60, .70], st), 0, 1), .45, .65, .13, wind);
      if (bl.v > .02) {
        const k = cl(bl.v * 1.2, 0, 1);
        const bc = ramp(R.zostera, cl(.28 + fr(bl.id * 9.7) * .34 + (1 - bl.q) * .12, 0, 1));
        tint(bc[0], bc[1], bc[2], k * .78); h += k * .18;
      }
    }
    /* Hi: loose cut blades drifted over the meadow, straw-coloured and flat */
    if (st === 2) {
      const dr = blades(s, t, 15, sd + 19, .30, .40, .55, .12, -1.4);
      if (dr.v > .03) { const dc = ramp(R.zosOld, .28 + fr(dr.id * 5.3) * .44); tint(dc[0], dc[1], dc[2], cl(dr.v, 0, 1) * .66); h += dr.v * .06; }
    }
    mul(.974 + .052 * hash(px, py, sd + 90));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ================================================================ IRISHMOSS
     Chondrus crispus on red cobble — the hard-bottom bed, and the one colour on
     this coast that is neither olive nor red-brown. Cushions of branched frond
     10-30 cm across, wine-purple in the shade and bleached olive-bronze on the
     crowns, with barnacled cobble showing wherever the turf has not closed.
       Lo  scattered cushions on barnacled cobble
       Mid a closed turf
       Hi  a deep dark drape, cobble hidden, loose moss in the seams */
  function irishmoss(s, t, sd, px, py, st) {
    const big = fbm(s, t, 3, sd + 1, 3);
    /* cobble floor: red stone with a good share of cold grey erratics, and the
       whole floor held DOWN in value — a red cobble under a red-purple turf is
       one material, and the tile stops reading as a bed of anything. */
    const cw = worleyA(s, t, 40, 34, sd + 2, .95);
    const cid = fr(cw.id * 23.7), cd1 = cw.d1, cgap = cw.d2 - cw.d1;
    const stv = cl(.16 + cid * .24 + (1 - ss(.62, .05, cd1)) * .10 + (fbm(s, t, 70, sd + 3, 2) - .5) * .14 + (big - .5) * .12, 0, 1);
    set(ramp(cid < .26 ? P.stoneGrey : R3.ledgeLow, stv));
    let h = .34 + stv * .16 - ss(.09, 0, cgap) * .20;
    const bn = ss(.60, .92, fbm(s, t, 22, sd + 4, 3)) * ss(.30, .62, stv);
    if (bn > .01) { const bc = ramp(R3.barn, .18 + fbm(s, t, 90, sd + 5, 2) * .40); tint(bc[0], bc[1], bc[2], bn * .28); h += bn * .06; }

    /* cushions. A cushion is a DOME with fronds inside it; ring it with anything
       and the tile becomes a field of doughnuts (rule 6). */
    const cN = fbm(s, t, 3, sd + 10, 4) * .58 + fbm(s, t, 8, sd + 11, 3) * .42;
    const frac = cl(pk([.34, .96, 1.08], st) * (.30 + .84 * ss(.30, .66, cN)), 0, 1);
    const KN = pk([34, 26, 22], st);
    const kw = worley(s, t, KN, sd + 12, .95);
    const kid = fr(kw.id * 31.1), kd1 = kw.d1;
    const cush = (fr(kid * 3.77) < frac) ? ss(pk([.56, .74, .88], st), .06, kd1) : 0;
    if (cush > .02) {
      /* frond texture: ridged noise at cushion scale reads as dichotomous
         branching without a single branch being drawn */
      const f1 = rfbm(s, t, 46, sd + 13, 3), f2 = fbm(s, t, 110, sd + 14, 2);
      const shade = cl(.05 + ss(.60, .05, kd1) * .20 + f1 * .22 + (f2 - .5) * .16 + kid * .10, 0, 1);
      const mc = ramp(R.moss, shade);
      tint(mc[0], mc[1], mc[2], cush * .97);
      /* sun-bleached crowns: the olive-bronze that separates a Chondrus bed from
         everything else on the shore. Kept LOW — push it and the turf turns to
         rust and stops being the one purple thing on the coast. */
      const bleach = ss(.62, .94, fbm(s, t, 5, sd + 15, 3) * .66 + ss(.60, .05, kd1) * .34) * pk([.85, .60, .35], st);
      if (bleach > .02) { const bc2 = ramp(R.mossBleach, cl(.14 + shade * .70, 0, 1)); tint(bc2[0], bc2[1], bc2[2], cl(bleach * cush * .22, 0, 1)); }
      h = mix(h, cl(.44 + ss(.60, .05, kd1) * .28 + f1 * .18, 0, 1), cush);
    }
    /* Hi: loose torn moss lying in the seams between cushions */
    if (st === 2) {
      const lo = ss(.60, .90, fbmA(s, t, 40, 14, sd + 16, 3)) * ss(.50, .22, h);
      if (lo > .01) { const lc = ramp(R.moss, .12 + fbm(s, t, 60, sd + 17, 2) * .28); tint(lc[0], lc[1], lc[2], lo * .58); h += lo * .05; }
    }
    mul(.972 + .056 * hash(px, py, sd + 90));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ================================================================ REEF EDGE
     The margin of a bed as an RGBA decal: s runs along the margin and tiles, t
     crosses it and does not. t = 0 is ON the bed, t = 1 is out on the open flat.
     Three things happen across a margin and no noise cross-fade will draw any of
     them: the bed breaks into detached outliers, the current scours a MOAT at
     its foot, and the moat throws a rubble apron of dead valves beyond.

     The bed side is left almost entirely TRANSPARENT — whatever bed material is
     painted there shows through, which is the whole point of an edge strip. The
     decal only draws what the bed and the flat cannot draw for themselves.
       Lo  a soft margin, the bed just thinning out
       Mid a clear rim, a moat, an apron of loose shell
       Hi  an eroding margin — undercut rim, deep moat, outliers well out */
  function reefedge(s, t, sd, px, py, st) {
    const LINE = .38;
    /* the margin wanders, and both of its boundaries are modulated along s: a
       band of even width is a painted stripe and reads as one (rule 12) */
    const wob = (fbmA(s, t, 5, 2, sd + 1, 3) - .5) * .17 + (fbmA(s, t, 13, 2, sd + 2, 2) - .5) * .07;
    const d = t - LINE - wob;                       /* < 0 on the bed, > 0 off it */
    const grain = fbm(s, t, 60, sd + 3, 2), big = fbmA(s, t, 4, 2, sd + 4, 3);

    /* an edge strip must write a colour at every texel even where it is fully
       transparent — a stale carry-over from the previous texel fringes every
       soft boundary once the decal is composited */
    set(ramp(Q.silt, cl(.30 + (grain - .5) * .22 + big * .26, 0, 1)));
    let a = 0, h = .42;

    /* ---- outliers: clumps of bed thrown clear of the margin ---- */
    const clump = fbmA(s, t, 9, 4, sd + 5, 3) * .60 + fbmA(s, t, 20, 7, sd + 6, 2) * .40;
    const reach = pk([.05, .13, .26], st);
    const outK = ss(-.02, .04, d) * ss(reach, reach * .15, d) * ss(pk([.66, .60, .54], st), pk([.80, .74, .68], st), clump);
    if (outK > .02) {
      const sw = worleyA(s, t, 130, 108, sd + 7, .95);
      const sid = fr(sw.id * 27.7);
      const v = cl(.30 + sid * .44 + (1 - ss(.52, .06, sw.d1)) * .14 + (grain - .5) * .16, 0, 1);
      const c = ramp(sid < .40 ? R.mussel : R.oyster, v);
      tint(c[0], c[1], c[2], 1);
      h = .52 + sid * .12 + (1 - ss(.52, .06, sw.d1)) * .16;
      a = cl(outK * 1.4, 0, 1);
    }
    /* ---- the rim: the bed's outer edge stands proud, and is undercut below ---- */
    const rimK = T3.band(d, -.055, .015, .045) * ss(.30, .62, clump) * pk([.45, .90, 1.0], st);
    if (rimK > .02) {
      const rw = worleyA(s, t, 120, 100, sd + 10, .95);
      const rid = fr(rw.id * 31.7);
      const rv = cl(.28 + rid * .44 + (grain - .5) * .16, 0, 1);
      const rc = ramp(rid < .34 ? R.cultch : R.mussel, rv);
      tint(rc[0], rc[1], rc[2], cl(rimK * 1.3, 0, 1));
      h = mix(h, .60 + rid * .16, cl(rimK * 1.2, 0, 1));
      a = Math.max(a, cl(rimK * 1.35, 0, 1));
    }
    /* ---- the moat: swept clean and floored on the flat's own sediment. It is
       dark because it is LOW — nothing in here is painted as shadow ---- */
    const moatW = pk([.09, .15, .21], st);
    const moatK = T3.band(d, .025, .025 + moatW, .05) * pk([.55, .85, 1.0], st);
    if (moatK > .01) {
      const mv = cl(.24 + (grain - .5) * .18 + big * .24, 0, 1);
      const mc = ramp(Q.silt, mv);
      tint(mc[0], mc[1], mc[2], moatK * .70);
      h = mix(h, cl(.16 + mv * .12, 0, 1), moatK);
      a = Math.max(a, moatK * .62);
    }
    /* ---- the apron: dead valves thrown out of the moat, thinning outward ---- */
    const apZ = ss(.03, .12, d) * ss(pk([.30, .48, .70], st), .04, d);
    if (apZ > .02) {
      const rw = worleyA(s, t, 110, 96, sd + 8, 1);
      const rid = fr(rw.id * 19.3);
      if (rid < cl(pk([.22, .34, .48], st) * apZ * 1.9, 0, 1) && rw.d1 < .34) {
        const k = ss(.34, .08, rw.d1);
        const cc = ramp(R.cultch, .20 + fr(rid * 43.1) * .48);
        tint(cc[0], cc[1], cc[2], k);
        h += k * .16;
        a = Math.max(a, k * .95);
      }
    }
    /* wrack caught against the rim on the bed side */
    const wr = ss(.78, .95, fbmA(s, t, 16, 5, sd + 9, 3)) * T3.band(d, -.14, -.01, .05) * pk([.30, .70, 1.0], st);
    if (wr > .02) {
      const wc = ramp(R3.fucus, .18 + grain * .40);
      tint(wc[0], wc[1], wc[2], cl(wr * .9, 0, 1)); h += wr * .06;
      a = Math.max(a, cl(wr * .85, 0, 1));
    }

    mul(.972 + .056 * hash(px, py, sd + 90));
    OUT.a = cl(a * ss(0, .05, t) * ss(1.0, .92, t), 0, 1);
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ------------------------------------------------------------------ registry
     Beds are high-relief materials for their size, so the cavity term carries
     more of them than it does on a flat; and Musselbed and Oysterreef are
     mean-flattened lightly, because their hummock and cluster structure IS
     low-frequency signal — flattening it away removes the reef. */
  const MATS = { musselbed, oysterreef, eelgrass, irishmoss };
  const CFG = {
    musselbed: { cav: .38, crown: .20, r: 3, lo: 16, hi: 214, flat: .54, flatR: .30 },
    oysterreef: { cav: .46, crown: .28, r: 3, lo: 20, hi: 232, flat: .52, flatR: .30 },
    eelgrass: { cav: .28, crown: .13, r: 3, lo: 18, hi: 216, flat: .78, flatR: .26 },
    irishmoss: { cav: .38, crown: .18, r: 3, lo: 16, hi: 218, flat: .72, flatR: .26 }
  };
  const SPEC = {
    musselbed: { N: 512, m: 16, seed: 21221 },
    oysterreef: { N: 512, m: 16, seed: 22307 },
    eelgrass: { N: 256, m: 8, seed: 23417 },
    irishmoss: { N: 256, m: 8, seed: 24509 }
  };
  Object.assign(T.MATS, MATS);
  Object.assign(T.CFG, CFG);
  Object.assign(T.SPEC, SPEC);

  const EDGES = { reefedge };
  const ECFG = { reefedge: { cav: .40, crown: .22, r: 3, lo: 20, hi: 228 } };
  const ESPEC = { reefedge: { W: 256, H: 128, mS: 8, mT: 4, seed: 25601 } };
  Object.assign(T3.EDGES, EDGES);
  Object.assign(T3.ECFG, ECFG);
  Object.assign(T3.ESPEC, ESPEC);

  return { MATS, CFG, SPEC, EDGES, ECFG, ESPEC, R };
})();
