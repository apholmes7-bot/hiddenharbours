/* terrainBake2.js — six more Prince Edward Island terrain materials.
   Load Art/terrainBake.js first. This registers into TB.MATS / TB.CFG / TB.SPEC,
   so TB.bake('sandstone', step, createCanvas) works exactly as for the first six.

     sandstone, bank, silt   512x512 = 16 x 16 m
     dirt, marsh, sedge      256x256 =  8 x  8 m

   SANDSTONE and BANK are FACE materials: s runs along the cliff, t runs DOWN it.
   The other four are plan view like the original set.

   Same contract as pass 2 — exactly periodic; intrinsic colour, grain and a
   non-directional cavity/crown term only. No key light, no cast shadow, no water,
   no wet or damp bands, alpha 255. Three-step intensity ladder per material:
   _Lo barely expressed, base working, _Hi fully expressed.
*/
globalThis.TB2 = (function () {
  const T = globalThis.TB;
  if (!T) throw new Error('Art/terrainBake.js must be loaded first');
  const hash = T.hash, fbm = T.fbm, fbmA = T.fbmA, vnA = T.vnA, rfbm = T.rfbm;
  const worley = T.worley, worleyA = T.worleyA, blades = T.blades;
  const ss = T.ss, ramp = T.ramp, mkRamp = T.mkRamp, P = T.P;
  const set = T.set, tint = T.tint, mul = T.mul, OUT = T.OUT;
  const mix = T.mix, cl = T.cl, fr = T.fr, pk = T.pk;
  const F = Math.floor, C = Math.cos, SN = Math.sin, SQ = Math.sqrt, PI = Math.PI;

  /* anisotropic ridged noise — narrow across, long along. Rills and rivulets.
     cx, cy integer and the octave factor is a power of two, so it stays periodic. */
  function ridA(s, t, cx, cy, sd, oct) {
    oct = oct || 4;
    let sum = 0, amp = 1, nrm = 0, f = 1;
    for (let i = 0; i < oct; i++) {
      const n = Math.abs(vnA(s, t, cx * f, cy * f, sd + i * 6151) * 2 - 1);
      sum += amp * (1 - n); nrm += amp; amp *= .5; f *= 2;
    }
    return sum / nrm;
  }

  /* a vertical joint set: a 1D lattice of joint traces, each jittered inside its
     own interval and wandering horizontally as it runs down the face. Periodic in
     both axes. A 2D Worley net gives polygons instead, which reads as cracked
     glaze rather than as jointing. */
  const VJ = { d: 0, id: 0 };
  function vjoint(s, t, cells, sd, wander) {
    const x = (s + wander) * cells, xi = F(x);
    let best = 9e9, bid = 0;
    for (let o = -1; o <= 1; o++) {
      const gx = xi + o, wx = ((gx % cells) + cells) % cells;
      const d = Math.abs(x - (gx + hash(wx, 5, sd)));
      if (d < best) { best = d; bid = hash(wx, 9, sd); }
    }
    VJ.d = best / cells; VJ.id = bid; return VJ;
  }

  /* ------------------------------------------------------------------ palettes
     PEI red beds: haematite-cemented sandstone and the till derived from it.
     Mid of each ramp sampled off the cliff photography, ends pushed out, and the
     whole thing held inside the grade-safe window by the baker. */
  const Q = {
    /* Permo-Carboniferous sandstone. Liver red, but the ramp has to run the whole
       way from a black hollow to pale dry dust on a crown — a narrow ramp reads as a
       brown wall no matter how much incident is drawn on it. */
    rock: mkRamp([[0, '#201410'], [.14, '#37211a'], [.30, '#4f2f24'], [.46, '#684131'], [.62, '#855841'], [.76, '#9e7359'], [.88, '#b89377'], [1, '#ccb096']]),
    /* a freshly broken face is lighter and less brown than a weathered one */
    fresh: mkRamp([[0, '#33201a'], [.32, '#5f3a2b'], [.62, '#8d5c44'], [1, '#bb9070']]),
    /* grey-green reduction spots and bands — the signature mottle of the red beds.
       Kept almost neutral: any real chroma here reads as mould. */
    reduce: mkRamp([[0, '#565a52'], [.35, '#73776c'], [.7, '#949887'], [1, '#b1b3a2']]),
    /* iron and manganese run-off down the face */
    stain: mkRamp([[0, '#1e1410'], [.5, '#372218'], [1, '#4f3324']]),
    /* unconsolidated red till of the soft banks */
    till: mkRamp([[0, '#1e1109'], [.18, '#331d14'], [.36, '#47291d'], [.54, '#5e3b2a'], [.72, '#785339'], [.88, '#96704f'], [1, '#b8926e']]),
    /* dried crust on a clod crown or a trodden path */
    crust: mkRamp([[0, '#5f4838'], [.5, '#836a50'], [1, '#a68c6d']]),
    /* estuary silt: warm grey-brown, carrying the red beds it was eroded from. A
       green-grey ramp here reads as army canvas. */
    silt: mkRamp([[0, '#211d17'], [.16, '#312a21'], [.34, '#463c2f'], [.52, '#5c503f'], [.70, '#756653'], [.86, '#8f7f6b'], [1, '#a99783']]),
    siltCrust: mkRamp([[0, '#5e5748'], [.4, '#807765'], [.7, '#9d9382'], [1, '#b8ad9a']]),
    /* anoxic black mud in the channel floors and under the marsh mat */
    anox: mkRamp([[0, '#161511'], [.5, '#242219'], [1, '#353023']]),
    peat: mkRamp([[0, '#1c1712'], [.3, '#30251a'], [.6, '#463625'], [1, '#604c34']]),
    /* Spartina — yellower, duller and stiffer than the upland sward, so the two
       never read as the same material */
    spartina: mkRamp([[0, '#2b331a'], [.28, '#3f4a26'], [.55, '#5a6633'], [.78, '#788447'], [1, '#9ba465']]),
    /* dead cordgrass thatch and drift wrack */
    wrack: mkRamp([[0, '#4c4431'], [.45, '#736748'], [1, '#9a8c67']]),
    /* evaporite crust of a high-marsh salt pan */
    pan: mkRamp([[0, '#7b7866'], [.5, '#9b9884'], [1, '#bbb7a2']]),
    sedge: mkRamp([[0, '#25301a'], [.3, '#394723'], [.58, '#546333'], [.8, '#6e7c44'], [1, '#8d975f']]),
    rush: mkRamp([[0, '#1e2918'], [.5, '#344429'], [1, '#52653b']]),
    moss: mkRamp([[0, '#39492b'], [.4, '#5e733f'], [.7, '#85975b'], [1, '#a7b477']]),
    mossRed: mkRamp([[0, '#48291f'], [.5, '#784231'], [1, '#a36849']])
  };

  /* ================================================================= SANDSTONE
     PEI cliff face, seen face-on. One bedded sandstone mass: near-horizontal beds
     of alternating hardness, cross-bedded within, cut into blocks by a vertical
     joint set. Weathering plucks blocks out and hollows the soft beds into
     cavernous alveoli; iron run-off streaks the whole face.
       Lo  fresh face, crisp bedding, little staining — recently collapsed
       Mid weathered — joints open, blocks out, honeycomb in the soft beds
       Hi  badly eroded — deep alveolar hollows, big scars, spall flakes
     The t coefficient on the bedding IS the modulus of the per-bed hash, or bed
     identity would not wrap at the tile edge. 16 beds over 16 m = 1 m beds. */
  function sandstone(s, t, sd, px, py, st) {
    const NB = 16;
    const sag = (fbm(s, t, 2, sd + 31, 3) - .5) * 1.7 + (fbm(s, t, 4, sd + 32, 3) - .5) * .60
      + (fbm(s, t, 12, sd + 33, 2) - .5) * .12;
    const bp = NB * t + sag;
    const bi = F(bp), bf = fr(bp);
    const bmod = i => ((i % NB) + NB) % NB;
    /* two scales of bed identity, so the stack comes out as thick units and thin
       beds rather than a uniform ladder of stripes. Neighbouring beds often share
       a tone and read as one unit. */
    const unit = hash(bmod(F(bp * .5) * 2), 53, sd);
    const tone = mix(unit, hash(bmod(bi), 23, sd), .42);
    const hard = mix(unit, hash(bmod(bi), 71, sd), .50);

    /* the parting at the top of a bed. Only about half the beds have one, and each
       one fades in and out along its length — a hairline groove at every boundary
       reads as ruled paper. Width is held above a texel and faded with opacity. */
    const part = ss(.100, 0, bf) * ss(.34, .78, hash(bmod(bi), 109, sd))
      * ss(.26, .70, fbmA(s, t, 7, 3, sd + 37, 2));

    /* internal lamination: cross-bedded in some beds, planar in others, dip
       flipping per bed. It shows only in PATCHES. A comb running the whole face
       reads as corduroy, and that is the fastest way to stop reading as rock. */
    const dip = tone > .5 ? 1 : -1;
    const xb = .5 + .5 * SN(2 * PI * (72 * s + dip * 18 * t) + sag * 2.2);
    const pl = .5 + .5 * SN(2 * PI * (112 * t) + sag * 7);
    const lam = mix(pl, xb, ss(.36, .70, hash(bmod(bi), 137, sd)));
    const lamM = ss(.62, .92, fbm(s, t, 6, sd + 34, 3) * .58 + fbmA(s, t, 16, 4, sd + 35, 2) * .42)
      * (.35 + .65 * hard);

    /* two vertical joint sets, wandering as they run down. Openness is a per-joint
       property and each trace is broken into runs, so a few fissures show and the
       rest stay healed. */
    const wan = (fbm(s, t, 3, sd + 53, 3) - .5) * .13 + (fbm(s, t, 8, sd + 54, 2) - .5) * .034;
    const runBreak = ss(.34, .66, fbmA(s, t, 2, 6, sd + 72, 2));
    let joint = 0;
    {
      const a = vjoint(s, t, 5, sd + 1, wan);
      const jw = (.0040 + .0018 * fbmA(s, t, 3, 8, sd + 55, 2)) * pk([.85, 1.0, 1.15], st);
      joint = ss(jw, jw * .28, a.d) * ss(.62, .30, fr(a.id * 29.13)) * runBreak * pk([.30, .85, 1.05], st);
      const b = vjoint(s, t, 13, sd + 2, wan * .7);
      const bw = .0030 * pk([.85, 1.0, 1.15], st);
      joint = Math.max(joint, ss(bw, bw * .28, b.d) * ss(.34, .12, fr(b.id * 17.91))
        * ss(.34, .68, fbmA(s, t, 3, 9, sd + 73, 2)) * pk([.20, .70, .95], st));
    }

    /* undercut hollows: irregular recessed patches that follow the weak beds. This
       is the notch reading — the single thing that says "sea cliff" and not "wall" —
       so the boundary is kept as a step, letting the cavity pass draw the rim. */
    let hollow = 0;
    const hz = pk([.05, .45, .95], st);
    {
      const hs = s + (fbm(s, t, 4, sd + 40, 2) - .5) * .22, ht = t + (fbm(s, t, 4, sd + 41, 2) - .5) * .10;
      const hw = worleyA(hs, ht, 5, 4, sd + 42, .95);
      const he = .46 + (fbm(s, t, 14, sd + 43, 2) - .5) * .42;
      if (fr(hw.id * 31.71) < hz * .70 && hw.d1 < he)
        hollow = ss(he, he * .72, hw.d1) * (.30 + .70 * ss(.62, .26, hard)) * hz;
    }

    /* blocky spall scars: whole blocks off the face, bounded by the joint set and
       by bedding, so the outline is angular. These are the only hard edges on the
       material and they are what keep it from reading as watercolour. */
    let blk = 0, blkV = 0;
    {
      const bw = worleyA(s + (fbm(s, t, 6, sd + 46, 2) - .5) * .05, t + (fbm(s, t, 6, sd + 47, 2) - .5) * .03,
        6, 10, sd + 48, .90);
      if (fr(bw.id * 53.17) < pk([.10, .26, .44], st)) { blk = ss(.02, .10, bw.d1); blkV = fr(bw.id * 19.3); }
    }

    const weathBig = fbm(s, t, 3, sd + 25, 3), weath = fbm(s, t, 13, sd + 5, 4);
    const fine = fbm(s, t, 52, sd + 15, 2), grit = fbm(s, t, 180, sd + 16, 2);
    /* bed-to-bed value separation is the big move here; everything else is detail.
       These weights are deliberately wide — a cliff face that only uses the middle
       of its ramp reads as a brown wall no matter how much incident is drawn on it. */
    let v = cl(.10 + (tone - .5) * .44 + weathBig * .26 + weath * .24 + fine * .12 + (grit - .5) * .11
      + (lam - .5) * .075 * lamM - part * .08 - hollow * .12 + (hard - .5) * .07
      + blk * (blkV - .35) * .22, 0, 1);
    const cr = ramp(Q.rock, v).slice();
    /* the Lo step is a recently collapsed face: fresher rock all over, not just in
       the hollows. That, more than the detail, is what separates it from the base. */
    const fk = hollow * .55 + blk * .45 + (st === 0 ? .34 : 0);
    if (fk > .02) { const fc = ramp(Q.fresh, cl(v + .06, 0, 1)); set([mix(cr[0], fc[0], fk), mix(cr[1], fc[1], fk), mix(cr[2], fc[2], fk)]); }
    else set(cr);
    /* case-hardened rind: broad pale patches, the large-scale variety that keeps a
       bedded face from reading as a stack of flat bands */
    const rind = ss(.58, .88, fbm(s, t, 4, sd + 38, 3) * .7 + weathBig * .3) * (1 - hollow);
    if (rind > .02) tint(178, 154, 134, rind * .16);

    let h = cl(.50 + (hard - .5) * .20 + (lam - .5) * .05 * lamM + weath * .14 + fine * .05
      + (grit - .5) * .04 - part * .09 - joint * .34 - hollow * .50 - blk * .30, 0, 1);

    /* cavernous weathering. Alveoli cluster — inside the hollows and in the weak
       beds — rather than peppering the face, and each pit is wide enough that a
       patch of them merges into honeycomb. No painted rim: a bright ring at this
       size reads as a doughnut, and the cavity pass draws the lip for free. */
    const avZ = cl((.34 + hollow * 1.2 + blk * .5) * ss(.62, .26, hard)
      * ss(.46, .82, fbm(s, t, 10, sd + 8, 3)) * pk([.00, .85, 1.25], st), 0, 1);
    if (avZ > .05) {
      const av = worley(s, t, 36, sd + 7, 1);
      const ae = .62 + (fbm(s, t, 70, sd + 39, 2) - .5) * .30;
      if (fr(av.id * 29.1) < .70 * avZ && av.d1 < ae) {
        const k = ss(ae, ae * .32, av.d1) * (.45 + .55 * avZ);
        tint(52, 33, 26, k * .44); h -= k * .52;
      }
    }
    /* grey-green reduction mottles — large irregular blotches with a ragged outline */
    const rz = ss(.54, .88, fbm(s, t, 5, sd + 9, 3));
    if (rz > .05) {
      const rw = worley(s + .31, t + .17, 12, sd + 10, 1);
      const edge = .44 + (fbm(s, t, 22, sd + 36, 2) - .5) * .38;
      if (fr(rw.id * 7.71) < .24 && rw.d1 < edge) {
        const rc = ramp(Q.reduce, cl(v + .12, 0, 1));
        tint(rc[0], rc[1], rc[2], ss(edge, edge * .3, rw.d1) * .34 * rz);
      }
    }
    /* run-off: long vertical streaks, the strongest single incident on the face */
    const runN = fbmA(s, t, 26, 3, sd + 11, 3) * .70 + fbmA(s, t, 10, 2, sd + 12, 2) * .30;
    const run = ss(.54, .88, runN) * pk([.18, .80, 1.10], st);
    if (run > .01) { const sc = ramp(Q.stain, .18 + weath * .5); tint(sc[0], sc[1], sc[2], run * .38); }

    /* Hi only: spall sheets lifting off, fresher rock behind them */
    if (st === 2) {
      const spz = ss(.50, .82, fbm(s, t, 4, sd + 13, 3));
      if (spz > .05) {
        const sp = worleyA(s, t, 11, 8, sd + 14, .9);
        if (fr(sp.id * 17.31) < .34 * spz && sp.d1 < .54) {
          const k = ss(.54, .26, sp.d1);
          const fc = ramp(Q.fresh, cl(v + .12, 0, 1));
          tint(fc[0], fc[1], fc[2], k * .44); h -= k * .12;
        }
      }
    }
    tint(30, 21, 18, joint * .34);
    mul(.974 + .052 * hash(px, py, sd + 95));
    { const gw = worley(s, t, 150, sd + 96, 1); mul(.966 + .068 * fr(gw.id * 17.3)); }
    OUT.h = h;
    return OUT;
  }

  /* ====================================================================== BANK
     The other PEI cliff: an unconsolidated red till bank, actively failing. Seen
     face-on. Rill gullies run down it and converge; clods and stones stand out of
     the face; slipped turf mats ride down on the slump lobes.
       Lo  firm bank, faint rilling, crust mostly intact
       Mid well rilled, clods loose, a mat or two slipped
       Hi  failing — deep gullies, big slump clods, hanging turf, stone lag */
  function bank(s, t, sd, px, py, st) {
    const wob = (fbm(s, t, 3, sd + 21, 3) - .5) * .060 + (fbm(s, t, 9, sd + 22, 2) - .5) * .018;
    const rs = s + wob;
    /* rill gullies. Three calibres, all long in t and narrow in s, and this is the
       whole character of the material — everything else is grain on top of it. */
    const g1 = ridA(rs, t, 5, 2, sd + 1, 4);
    const g2 = ridA(rs + .13, t, 13, 4, sd + 2, 3);
    const g3 = ridA(rs + .41, t, 29, 7, sd + 3, 2);
    const dp = pk([.55, 1.00, 1.35], st);
    const ch = cl((ss(.60, .94, g1) * .50 + ss(.70, .97, g2) * .32 + ss(.78, 1.0, g3) * .18) * dp, 0, 1);

    const broad = fbm(s, t, 3, sd + 4, 3), mott = fbm(s, t, 10, sd + 5, 3);
    const fine = fbm(s, t, 54, sd + 6, 2), fine2 = fbm(s, t, 140, sd + 7, 2);
    let h = .52 + broad * .14 + mott * .10 + fine * .05 - ch * .52;
    let v = cl(.13 + broad * .24 + mott * .20 + fine * .14 + (fine2 - .5) * .10 - ch * .30, 0, 1);
    set(ramp(Q.till, v));
    /* dried crust on the interfluves — a broad wash. Tie it to a cell field and it
       immediately reads as crazed plaster. */
    const cz = ss(.34, .82, (1 - ch) * (.42 + .58 * broad)) * pk([1.0, .80, .52], st);
    if (cz > .02) { const cc = ramp(Q.crust, .22 + mott * .48); tint(cc[0], cc[1], cc[2], cz * .30); }

    /* clods standing out of the face. Sparse on purpose: a dense lump field makes a
       cellular net, which is the same failure as the crust above. */
    const clz = pk([.25, .65, 1.00], st);
    const cw = worleyA(s, t, 18, 15, sd + 8, .95);
    if (fr(cw.id * 17.31) < .38 * clz && cw.d1 < .46) {
      const dn = cw.d1 / .46, dome = SQ(cl(1 - dn * dn, 0, 1));
      const cc = ramp(Q.till, cl(v + .08 + (fr(cw.id * 11.7) - .5) * .16, 0, 1));
      tint(cc[0], cc[1], cc[2], dome * .55);
      mul(mix(.95, 1.05, dome));
      h += dome * .24 * clz;
    }
    /* stones washing out of the till and collecting as a lag in the gullies */
    /* stones washing out of the till. Sizes have to vary hard and the edge has to be
       crisp, or one stone per cell at one size reads as polka dots. They carry a red
       dust film, so they sit IN the bank instead of on it. */
    const stz = pk([.5, .85, 1.15], st);
    const stw = worleyA(s, t, 30, 26, sd + 9, 1);
    const str = (.18 + fr(stw.id * 7.31) * .30) * (1 + (fbm(s, t, 120, sd + 16, 2) - .5) * .5);
    if (fr(stw.id * 47.3) < .055 * stz && stw.d1 < str) {
      const k = ss(str, str * .70, stw.d1);
      const g = ramp(P.stoneGrey, .10 + fr(stw.id * 31.7) * .26);
      const y = .299 * g[0] + .587 * g[1] + .114 * g[2];
      tint(mix(g[0], y, .28) * .98, mix(g[1], y, .28) * .82, mix(g[2], y, .28) * .68, k * .76);
      h += k * .20;
    }
    /* slipped turf mats riding down on the slump lobes. Torn outline, and the green
       is pulled well down — a saturated patch here reads as camouflage. */
    const mz = pk([.06, .20, .38], st);
    if (mz > .03) {
      const ms = s + (fbm(s, t, 6, sd + 12, 2) - .5) * .16, mt = t + (fbm(s, t, 6, sd + 13, 2) - .5) * .10;
      const mw = worleyA(ms, mt, 5, 2, sd + 10, .95);
      const edge = .48 + (fbm(s, t, 18, sd + 14, 2) - .5) * .44;
      const inMat = fr(mw.id * 19.71) < mz ? ss(edge, edge - .18, mw.d1) : 0;
      if (inMat > .02) {
        const g = ramp(P.grass, cl(.16 + mott * .26 + fine * .14, 0, 1));
        const y = .299 * g[0] + .587 * g[1] + .114 * g[2];
        tint(mix(g[0], y, .48) * .84, mix(g[1], y, .48) * .84, mix(g[2], y, .48) * .84, inMat * .90);
        h += inMat * .18;
        /* half of it is dead — a mat that has slipped is a dying mat */
        const dz = ss(.42, .80, fbm(s, t, 13, sd + 15, 3)) * inMat;
        if (dz > .02) { const dc = ramp(P.straw, .06 + fine * .26); tint(dc[0] * .88, dc[1] * .86, dc[2] * .84, dz * .42); }
        const gb = blades(s, t, 40, sd + 11, .60, 1.0, 1.2, .13, .50);
        if (gb.v > .02) {
          const bc = ramp(P.grass, cl(.22 + gb.q * .26 + fr(gb.id * 7.7) * .16, 0, 1));
          const by = .299 * bc[0] + .587 * bc[1] + .114 * bc[2];
          tint(mix(bc[0], by, .40) * .90, mix(bc[1], by, .40) * .90, mix(bc[2], by, .40) * .90, cl(gb.v * inMat * .92, 0, 1));
          h += gb.v * inMat * .12;
        }
        /* torn soil and root hair where the mat has pulled away */
        const ez = ss(edge - .18, edge, mw.d1) * (inMat > .02 ? 1 : 0);
        if (ez > .02) { const sc = ramp(P.soil, .18 + mott * .38); tint(sc[0], sc[1], sc[2], ez * .34); }
      }
    }
    tint(48, 28, 21, ch * .34);
    mul(.972 + .056 * hash(px, py, sd + 91));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ====================================================================== DIRT
     Trodden red earth — a path, a headland turn, a yard. Plan view.
       Lo  a worn line, stubble and moss still holding, soil showing between
       Mid bare compacted earth, pebbles pressed flush, scuffs
       Hi  churned — clods, loose tilth, grooves cut across it */
  function dirt(s, t, sd, px, py, st) {
    const broad = fbm(s, t, 3, sd + 1, 3), mott = fbm(s, t, 12, sd + 2, 3);
    const fine = fbm(s, t, 54, sd + 3, 2), fine2 = fbm(s, t, 120, sd + 4, 2);
    let h = .48 + broad * .14 + mott * .16 + fine * .07;
    let v = cl(.13 + broad * .22 + mott * .26 + fine * .16 + (fine2 - .5) * .10, 0, 1);
    set(ramp(Q.till, v));
    /* pale dried crust on the compacted flats, strongest on the worn base step */
    const cz = ss(.34, .74, broad * .5 + mott * .5) * pk([.85, 1.0, .45], st);
    if (cz > .02) { const cc = ramp(Q.crust, .24 + mott * .48); tint(cc[0], cc[1], cc[2], cz * .40); }

    /* scuffs and drag marks: elongated, low contrast, only loosely aligned */
    const sc = ss(.58, .86, fbmA(s, t, 38, 11, sd + 5, 2) * .72 + fbmA(s, t, 14, 5, sd + 6, 2) * .28)
      * pk([.5, 1.0, .8], st);
    if (sc > .01) { mul(1 - sc * .085); h -= sc * .10; }

    /* pebble lag pressed flush into the surface — flat, not domed, and sparse. One
       stone per cell at one size and one value reads as polka dots. */
    const pz = pk([.5, 1.0, 1.25], st);
    const pw = worley(s, t, 40, sd + 7, 1);
    const pr = (.16 + fr(pw.id * 7.31) * .28) * (1 + (fbm(s, t, 130, sd + 16, 2) - .5) * .5);
    if (fr(pw.id * 43.7) < .075 * pz && pw.d1 < pr) {
      const k = ss(pr, pr * .68, pw.d1);
      const g = ramp(P.stoneGrey, .12 + fr(pw.id * 29.3) * .30);
      const y = .299 * g[0] + .587 * g[1] + .114 * g[2];
      tint(mix(g[0], y, .30) * .98, mix(g[1], y, .30) * .84, mix(g[2], y, .30) * .70, k * .78);
      h += k * .08;
    }
    /* Lo: what is left of the turf — stubble, moss and a little straw */
    if (st === 0) {
      const gz = ss(.30, .70, fbm(s, t, 5, sd + 8, 3));
      if (gz > .03) {
        const mo = ss(.48, .80, fbm(s, t, 12, sd + 9, 3)) * gz;
        if (mo > .02) { const mc = ramp(Q.moss, .16 + mott * .40); tint(mc[0], mc[1], mc[2], mo * .68); h += mo * .07; }
        const gb = blades(s, t, 42, sd + 10, gz * .70, .60, .8, .13, .18);
        if (gb.v > .02) {
          const bc = ramp(P.grass, cl(.20 + gb.q * .28 + fr(gb.id * 7.7) * .16, 0, 1));
          tint(bc[0], bc[1], bc[2], cl(gb.v * .92, 0, 1)); h += gb.v * .11;
        }
        const stw = ss(.60, .90, fbm(s, t, 8, sd + 11, 3)) * gz;
        if (stw > .01) { const sc2 = ramp(P.straw, .18 + fine * .44); tint(sc2[0], sc2[1], sc2[2], stw * .44); }
      }
    }
    /* Hi: churned into clods with loose tilth between, and grooves cut across */
    if (st === 2) {
      const cw = worleyA(s, t, 14, 12, sd + 12, .95);
      if (cw.d1 < .52) {
        const dn = cw.d1 / .52, dome = SQ(cl(1 - dn * dn, 0, 1));
        const cvv = cl(.14 + fr(cw.id * 13.9) * .36 + mott * .22, 0, 1);
        const cc = ramp(Q.till, cvv);
        tint(cc[0], cc[1], cc[2], dome * .70);
        mul(mix(.92, 1.07, dome));
        h += dome * .30;
      }
      const gr = ss(.72, .96, ridA(s + (fbm(s, t, 5, sd + 13, 2) - .5) * .06, t, 5, 3, sd + 14, 3));
      if (gr > .01) { mul(1 - gr * .16); h -= gr * .30; }
      const tl = fbm(s, t, 90, sd + 15, 2);
      mul(.96 + .08 * tl);
    }
    mul(.972 + .056 * hash(px, py, sd + 19));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ====================================================================== SILT
     Estuary creek mud — olive grey, fine, quite unlike the iron-red Fundy ripple.
     Dendritic rivulets drain it; the surface crusts and cracks into curled plates
     as it dries; Corophium and lugworm work it over.
       Lo  soft fresh silt sheet, barely drained, no crust
       Mid drained — rivulet network, casts, patchy crust
       Hi  crusted and cracked into curled plates, mud clasts lying on it */
  function silt(s, t, sd, px, py, st) {
    const broad = fbm(s, t, 2, sd + 1, 3), mid = fbm(s, t, 8, sd + 2, 3);
    const fine = fbm(s, t, 46, sd + 3, 2), grain = fbm(s, t, 170, sd + 4, 2);
    /* drainage: NARROW incised rivulets with a levee shoulder. A wide threshold on
       ridged noise gives broad soft smudges that read as scribble, not as channels. */
    const dA = rfbm(s, t, 5, sd + 5, 3), dB = rfbm(s + .19, t + .37, 12, sd + 6, 2);
    const rA = ss(.80, .93, dA) * pk([.55, 1.0, .85], st);
    const rB = ss(.86, .97, dB) * pk([.40, 1.0, .80], st);
    const riv = cl(rA * .72 + rB * .40, 0, 1);
    const lev = ss(.66, .80, dA) * (1 - rA) * .8 + ss(.74, .86, dB) * (1 - rB) * .5;

    let h = .50 + broad * .08 + mid * .14 + fine * .08 + (grain - .5) * .05 + lev * .10 - riv * .50;
    let v = cl(.04 + broad * .14 + mid * .26 + fine * .18 + (grain - .5) * .13 + lev * .10 - riv * .26, 0, 1);
    set(ramp(Q.silt, v));
    /* pelletal surface. Mud worked over by Corophium is stippled at the grain scale,
       never smooth, and this is most of what makes it read as mud at 1:1. */
    { const pw = worley(s, t, 150, sd + 20, 1); mul(.940 + .118 * fr(pw.id * 23.71)); }
    /* anoxic black in the channel floors and the hollows. Mud that is not dark is
       not mud — it is plaster. */
    const az = cl(riv * .95 + ss(.42, .18, broad * .5 + mid * .5) * .55, 0, 1);
    if (az > .02) { const ac = ramp(Q.anox, .3 + mid * .5); tint(ac[0], ac[1], ac[2], az * .60); }

    /* drying crust and desiccation plates. Plates run about 70 cm; the crack is held
       at 1.2 px and faded with opacity, and the plate edges curl up so the cavity
       pass shades the lip bright and the crack dark. Only ever in patches, and the
       net itself is broken — a continuous polygonal web reads as crackle glaze. */
    const kz = pk([.00, .30, .85], st) * ss(.40, .78, fbm(s, t, 5, sd + 7, 3) * .62 + fbm(s, t, 12, sd + 18, 2) * .38);
    if (kz > .03) {
      const cs = s + (fbm(s, t, 12, sd + 8, 2) - .5) * .020, ct2 = t + (fbm(s, t, 12, sd + 9, 2) - .5) * .020;
      const cw = worleyA(cs, ct2, 22, 21, sd + 10, .92);
      const gap = cw.d2 - cw.d1;
      const netM = ss(.32, .72, fbm(s, t, 26, sd + 19, 2));
      const crack = ss(.055, .012, gap) * kz * netM;
      const lip = ss(.24, .10, gap) * (1 - crack) * kz;
      const pc = ramp(Q.siltCrust, cl(v + .12 + (fr(cw.id * 17.7) - .5) * .24, 0, 1));
      tint(pc[0], pc[1], pc[2], kz * .34 * (1 - crack * .8));
      tint(20, 19, 15, crack * .70);
      h += lip * .20 - crack * .44;
      /* Hi: plates that have lifted clear and lie as clasts on the surface */
      if (st === 2 && fr(cw.id * 61.3) < .09 && cw.d1 < .34) {
        const k = ss(.34, .12, cw.d1);
        const mc = ramp(Q.siltCrust, cl(v + .14, 0, 1));
        tint(mc[0], mc[1], mc[2], k * .36); h += k * .22;
      }
    }
    /* burrow openings: dark pinholes with a low spoil rim. Small and dark — a bright
       raised cast at this size reads as a grub lying on the mud. */
    const wz = ss(.34, .68, fbm(s, t, 6, sd + 11, 3)) * pk([.85, 1.0, .55], st);
    if (wz > .04) {
      const ww = worley(s, t, 140, sd + 12, 1);
      if (fr(ww.id * 37.1) < .20 * wz) {
        if (ww.d1 < .17) { const k = ss(.17, .04, ww.d1); tint(20, 19, 15, k * .74); h -= k * .34; }
        else if (ww.d1 < .36) { const k = ss(.36, .19, ww.d1); const cc = ramp(Q.silt, cl(v + .12, 0, 1)); tint(cc[0], cc[1], cc[2], k * .32); h += k * .14; }
      }
    }
    /* shell hash and stranded weed, both settled low */
    const sg = worley(s, t, 96, sd + 13, 1);
    if (fr(sg.id * 51.7) < .020 && sg.d1 < .26) tint(158, 150, 134, ss(.26, .08, sg.d1) * .42);
    const wd = ss(.86, .97, fbm(s, t, 20, sd + 14, 2)) * ss(.30, .80, riv);
    if (wd > .01) { const wc = ramp(P.weed, .20 + mid * .45); tint(wc[0], wc[1], wc[2], wd * .55); }
    mul(.972 + .056 * hash(px, py, sd + 19));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ===================================================================== MARSH
     Salt marsh: Spartina cordgrass over anoxic mud, cut by creeklets, with bare
     salt pans in the high marsh. Stiff near-vertical blades, yellower than the
     upland sward, and the black substrate reads through wherever it thins.
       Lo  pioneer sprigs on open creek mud
       Mid closed cordgrass sward
       Hi  high marsh — heavy thatch, drift wrack, pans */
  function marsh(s, t, sd, px, py, st) {
    const broad = fbm(s, t, 3, sd + 1, 3), mid = fbm(s, t, 12, sd + 2, 3), fine = fbm(s, t, 60, sd + 3, 2);
    /* creeklets: few, sinuous, and they carry no grass */
    const cr = ss(.88, .995, ridA(s + (fbm(s, t, 3, sd + 4, 2) - .5) * .10, t, 3, 4, sd + 5, 3));
    const cr2 = ss(.93, 1.00, ridA(s + .27, t + .41, 7, 9, sd + 6, 2)) * .7;
    const creek = cl(cr * .7 + cr2 * .3, 0, 1);

    /* the substrate is anoxic mud and it has to stay DARK. Give the marsh a pale
       grey floor and the sward has nothing to read against. */
    let h = .46 + broad * .10 + mid * .12 + fine * .06 - creek * .42;
    set(ramp(Q.anox, cl(.14 + broad * .26 + mid * .32 + fine * .20 - creek * .22, 0, 1)));
    { const sc = ramp(Q.silt, .10 + mid * .34); tint(sc[0], sc[1], sc[2], ss(.36, .74, broad * .5 + mid * .5) * .34); }

    /* salt pans: broad flats of evaporite crust with a ragged outline. A cell field
       here gives round pale blobs, which read as cotton wool. */
    const pan = cl(ss(.58, .86, fbm(s, t, 4, sd + 7, 3) * .62 + fbm(s, t, 11, sd + 17, 2) * .38)
      * pk([.20, .50, 1.15], st) * (1 - creek), 0, 1);
    if (pan > .02) {
      const pc = ramp(Q.pan, cl(.16 + mid * .40 + fine * .22, 0, 1));
      tint(pc[0], pc[1], pc[2], pan * .58); h += pan * .05;
    }

    /* the sward. Stand clumping, then a fine anisotropic blade-noise field, then
       explicit blades — the noise field is what makes it read as cordgrass rather
       than as hairs laid on mud. */
    const stand = ss(pk([.58, .30, .10], st), pk([.86, .64, .38], st), fbm(s, t, 4, sd + 8, 3));
    const cw = worley(s, t, 7, sd + 20, .95);
    const clump = cl(1 - cw.d1 / pk([.48, .66, .80], st), 0, 1);
    const dens = cl(stand * (.40 + .90 * clump) * (1 - creek) * (1 - pan * .88), 0, 1);
    if (dens > .02) {
      const bs = s + (fbm(s, t, 5, sd + 21, 2) - .5) * .014;
      const bl = fbmA(bs, t, 96, 34, sd + 22, 2) * .68 + fbmA(bs, t, 170, 62, sd + 23, 1) * .32;
      const blade = ss(.42, .74, bl);
      const gc = ramp(Q.spartina, cl(.14 + blade * .40 + clump * .20 + mid * .14, 0, 1));
      tint(gc[0], gc[1], gc[2], cl(dens * (.55 + .40 * blade), 0, 1));
      h += dens * blade * .18;
      /* dead thatch collecting under an established stand */
      if (st > 0) {
        const th = ss(.42, .78, fbm(s, t, 18, sd + 9, 3)) * dens * pk([0, .45, 1.0], st);
        if (th > .01) { const tc = ramp(Q.wrack, .16 + fine * .40); tint(tc[0], tc[1], tc[2], th * .50); h += th * .06; }
      }
      /* cordgrass — stiff, so the lean and wind terms stay small */
      const b1 = blades(s, t, pk([30, 24, 20], st), sd + 10, dens * pk([1.1, 1.5, 1.85], st),
        pk([.9, 1.25, 1.55], st), pk([.9, 1.2, 1.4], st), .16, .12);
      if (b1.v > .02) {
        const dd = fr(b1.id * 3.7);
        const live = ramp(Q.spartina, cl(.18 + b1.q * .54 + fr(b1.id * 11.3) * .24, 0, 1)).slice();
        const dead = ramp(Q.wrack, cl(.20 + b1.q * .56, 0, 1));
        const k = ss(pk([.46, .58, .42], st), pk([.80, .92, .76], st), dd);
        tint(mix(live[0], dead[0], k), mix(live[1], dead[1], k), mix(live[2], dead[2], k), cl(b1.v * 1.25, 0, 1));
        h += b1.v * .42;
      }
      const b2 = blades(s, t, 36, sd + 11, dens * pk([.7, 1.1, 1.4], st), .65, .75, .13, .07);
      if (b2.v > .02) {
        const uc = ramp(Q.spartina, cl(.08 + b2.q * .38 + fr(b2.id * 5.3) * .18, 0, 1));
        tint(uc[0], uc[1], uc[2], cl(b2.v * .95, 0, 1));
        h += b2.v * .20;
      }
    }
    /* Hi: drift wrack — flat mats of dead cordgrass stranded by the spring tide */
    if (st === 2) {
      const wz = ss(.62, .88, fbmA(s, t, 5, 20, sd + 12, 3) * .7 + fbm(s, t, 4, sd + 13, 2) * .3);
      if (wz > .02) {
        const wc = ramp(Q.wrack, cl(.24 + fine * .44, 0, 1));
        tint(wc[0], wc[1], wc[2], wz * .78); h += wz * .10;
      }
    }
    mul(.974 + .052 * hash(px, py, sd + 19));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ===================================================================== SEDGE
     Freshwater marshy ground behind the dunes and along the ponds: sedge and rush
     tussocks standing out of peat, with sphagnum in the soaks. Strongly clumped,
     olive rather than yellow-green, and the dark peat gaps are the whole character.
       Lo  short grazed sedge lawn, peat and moss showing through
       Mid tussocks
       Hi  rank tussocks, thatch collars, rush standing over them */
  function sedge(s, t, sd, px, py, st) {
    const broad = fbm(s, t, 3, sd + 1, 3), mid = fbm(s, t, 13, sd + 2, 3), fine = fbm(s, t, 56, sd + 3, 2);
    /* soaks — the low wet hollows where the peat is bare and darkest */
    const soak = ss(.62, .30, fbm(s, t, 5, sd + 4, 3) * .7 + broad * .3);

    let h = .46 + broad * .12 + mid * .12 + fine * .06 - soak * .18;
    set(ramp(Q.peat, cl(.14 + broad * .28 + mid * .32 + fine * .20 - soak * .18, 0, 1)));

    /* sphagnum in the soaks. Broad ragged patches from noise, and which patch is the
       rusty red form is decided at patch scale — per-cell selection gives polka dots. */
    const mp = ss(.54, .84, fbm(s, t, 7, sd + 5, 3) * .60 + fbm(s, t, 17, sd + 6, 2) * .40)
      * (.30 + .70 * soak) * pk([1.2, .95, .65], st);
    if (mp > .02) {
      const redSel = ss(.52, .70, fbm(s, t, 4, sd + 7, 2)) * .7;
      const mg = ramp(Q.moss, cl(.18 + fine * .42 + mid * .20, 0, 1));
      const mr = ramp(Q.mossRed, cl(.18 + fine * .40, 0, 1));
      tint(mix(mg[0], mr[0], redSel), mix(mg[1], mr[1], redSel), mix(mg[2], mr[2], redSel), mp * .72);
      h += mp * .07;
    }
    /* tussocks: 80 cm mounds, each with its own vigour. No painted collar ring — a
       ring of straw around every mound reads as a field of worms. */
    const tw = worley(s, t, 8, sd + 8, .95);
    const tr = pk([.50, .68, .82], st);
    const tuss = cl(1 - tw.d1 / tr, 0, 1);
    const vig = fr(tw.id * 11.9);
    h += tuss * .30 * pk([.5, 1.0, 1.25], st);

    const dens = cl((tuss * .82 + .18) * (.45 + .85 * ss(.30, .70, mid)) * (1 - soak * .50) * pk([.7, 1.0, 1.2], st), 0, 1);
    if (dens > .02) {
      /* fine blade-noise field first: without it the explicit blades read as loose
         needles dropped on peat rather than as standing sedge */
      const bs = s + (fbm(s, t, 5, sd + 12, 2) - .5) * .018;
      const bl = fbmA(bs, t, 88, 30, sd + 13, 2) * .66 + fbmA(bs, t, 156, 56, sd + 14, 1) * .34;
      const blade = ss(.42, .74, bl);
      const gc = ramp(Q.sedge, cl(.12 + blade * .38 + tuss * .24 + vig * .16, 0, 1));
      tint(gc[0], gc[1], gc[2], cl(dens * (.55 + .40 * blade), 0, 1));
      h += dens * blade * .16;
      /* thatch, inside the tussock base rather than ringed around it */
      if (st > 0) {
        const th = ss(.44, .80, fbm(s, t, 20, sd + 15, 3)) * tuss * pk([0, .55, 1.0], st);
        if (th > .01) { const tc = ramp(P.straw, .10 + fine * .34); tint(tc[0] * .90, tc[1] * .88, tc[2] * .84, th * .46); }
      }
      /* the sedge itself, leaning both ways out of the mound */
      const b1 = blades(s, t, pk([32, 26, 22], st), sd + 9, dens * pk([1.0, 1.4, 1.8], st),
        pk([.65, 1.0, 1.4], st), pk([.7, 1.05, 1.35], st), .15, .34);
      if (b1.v > .02) {
        const dd = fr(b1.id * 3.7);
        const live = ramp(Q.sedge, cl(.16 + b1.q * .50 + vig * .20 + fr(b1.id * 11.3) * .16, 0, 1)).slice();
        const dead = ramp(P.straw, cl(.14 + b1.q * .50, 0, 1));
        const k = ss(pk([.52, .60, .50], st), pk([.86, .94, .80], st), dd);
        tint(mix(live[0], dead[0], k), mix(live[1], dead[1], k), mix(live[2], dead[2], k), cl(b1.v * 1.2, 0, 1));
        h += b1.v * .36;
      }
      const b2 = blades(s, t, 34, sd + 16, dens * pk([.7, 1.0, 1.2], st), .6, .8, .13, -.30);
      if (b2.v > .02) {
        const lc = ramp(Q.sedge, cl(.10 + b2.q * .40 + fr(b2.id * 5.3) * .18, 0, 1));
        tint(lc[0], lc[1], lc[2], cl(b2.v * .95, 0, 1));
        h += b2.v * .22;
      }
      /* rush: very upright, narrow, darker, and it overtops the sedge at Hi */
      const rz = dens * pk([.16, .40, .75], st) * ss(.34, .72, fbm(s, t, 7, sd + 10, 3));
      if (rz > .02) {
        const b3 = blades(s, t, 28, sd + 11, rz, pk([.9, 1.3, 1.8], st), pk([.7, .9, 1.2], st), .11, .04);
        if (b3.v > .02) {
          const rc = ramp(Q.rush, cl(.18 + b3.q * .50 + fr(b3.id * 5.3) * .20, 0, 1));
          tint(rc[0], rc[1], rc[2], cl(b3.v * 1.05, 0, 1));
          h += b3.v * .26;
        }
      }
    }
    mul(.974 + .052 * hash(px, py, sd + 19));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  const MATS = { sandstone, bank, dirt, silt, marsh, sedge };
  /* cav/crown as pass 2. The two cliff faces are flattened least: their
     bed-to-bed and crest-to-gully value separation IS low-frequency signal. */
  const CFG = {
    sandstone: { cav: .34, crown: .24, r: 4, lo: 24, hi: 226, flat: .34, flatR: .34 },
    bank: { cav: .32, crown: .17, r: 4, lo: 24, hi: 220, flat: .44, flatR: .32 },
    dirt: { cav: .26, crown: .13, r: 3, lo: 26, hi: 224, flat: .84, flatR: .26 },
    silt: { cav: .28, crown: .15, r: 4, lo: 20, hi: 186, flat: .45, flatR: .32 },
    marsh: { cav: .26, crown: .13, r: 3, lo: 22, hi: 216, flat: .84, flatR: .26 },
    sedge: { cav: .28, crown: .14, r: 3, lo: 22, hi: 214, flat: .84, flatR: .26 }
  };
  const SPEC = {
    sandstone: { N: 512, m: 16, seed: 7703 },
    bank: { N: 512, m: 16, seed: 8809 },
    silt: { N: 512, m: 16, seed: 9901 },
    dirt: { N: 256, m: 8, seed: 10103 },
    marsh: { N: 256, m: 8, seed: 11213 },
    sedge: { N: 256, m: 8, seed: 12301 }
  };
  Object.assign(T.MATS, MATS);
  Object.assign(T.CFG, CFG);
  Object.assign(T.SPEC, SPEC);
  return { MATS, CFG, SPEC, Q, ridA };
})();
