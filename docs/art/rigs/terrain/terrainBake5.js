/* terrainBake5.js — pass six: the mown lawn. Load AFTER terrainBake.js, 2, 3 and 4.

   One material. A dooryard lawn is not Grass at a low intensity, and the reason is
   worth writing down because it is the whole justification for spending a slot:

     THE KIT'S LADDER COUPLES COVERAGE TO POSITION. A painted channel's value is
     BOTH the blend weight against the height-derived bands AND the position on
     that material's ladder. Grass's _Lo IS "grazed, trodden thin turf" — but you
     can only reach it by painting the channel LOW, which is also how you ask for
     LESS grass. Paint it low and you get sparse rank meadow with the wild band
     showing through; paint it high and you get the _Hi step, which is rank meadow
     WITH SEED HEADS. There is no way to say "a lot of very short grass".

   So Lawn gets its own slot, and — this is the design decision — its ladder runs
   the other way round from the wear ladders:

     _Lo   neglected   moss, bare scuffs, tussocks starting, the odd seed head
     base  ordinary    kept, a bit of clover and wear, unevenly cut
     _Hi   crisp       dense, fine, even, cut tips catching the light

   MORE MATERIAL MEANS A BETTER LAWN, so the coupling stops fighting the look and
   starts carrying it: a yard painted 1.0 is a full-weight crisp lawn, and a yard
   painted ~0.55 is a half-weight ordinary one with the wild grass band showing
   through the other half — which is exactly what a kept-but-rough dooryard is.
   Two tiers of care out of one number, with nothing needing a second channel.

   WHAT SELLS "MOWN" AT 32 px/m is not blade length — a 3 cm blade is one texel.
   It is three things, in this order:
     1. UNIFORMITY. A lawn has almost no low-frequency variation. The clump and
        tuft fields that give Grass its sweep are turned right down.
     2. CUT TIPS. A mown sward is speckled with pale, slightly straw cut ends
        where the mower took the blade off square. This is the signature, and it
        is high-frequency, so it survives the mip chain as a lightening rather
        than washing out to flat colour.
     3. NO SEED HEADS. Grass's _Hi grows them; a lawn never does. Their absence is
        read instantly even when nothing else is.

   Same hard rules as every other pass: no key light, no cast shadow, no water, no
   pools, no wetness. Mower STRIPES are not baked here either — a baked stripe
   would run the same way on every lawn on the island. They are drawn in the
   shader from each yard polygon's own long axis.
*/
globalThis.TB5 = (function () {
  const T = globalThis.TB, T2 = globalThis.TB2, T3 = globalThis.TB3, T4 = globalThis.TB4;
  if (!T || !T2 || !T3 || !T4) throw new Error('terrainBake.js, 2, 3 and 4 must be loaded first');
  const hash = T.hash, fbm = T.fbm, fbmA = T.fbmA, worley = T.worley, blades = T.blades;
  const ss = T.ss, ramp = T.ramp, mkRamp = T.mkRamp, P = T.P;
  const set = T.set, tint = T.tint, mul = T.mul, OUT = T.OUT;
  const cl = T.cl, fr = T.fr, pk = T.pk;

  /* ------------------------------------------------------------------ palettes
     Held deliberately close to P.grass — a lawn on this island is the same
     species kept short, not a different plant, and a lawn that reads as a
     different HUE from the meadow it borders looks like astroturf. What changes
     is the SPREAD: this ramp is narrower and a touch cooler and fresher, because
     cutting removes the bleached tips and the seed heads that widen Grass's. */
  const R = {
    turf: mkRamp([[0, '#202d17'], [.18, '#2c3b1f'], [.36, '#3a4c2a'], [.54, '#485c36'], [.72, '#586e42'], [.88, '#6a8250'], [1, '#7d9662']]),
    /* the pale square end a mower leaves. Straw-green, never white — a white tip
       reads as frost. */
    cutTip: mkRamp([[0, '#4a5c33'], [.4, '#5b6d3e'], [.72, '#6d804b'], [1, '#7f9159']]),
    /* Trifolium repens: rounder, flatter, a shade bluer and lighter than turf. */
    clover: mkRamp([[0, '#2b3d24'], [.45, '#3c5232'], [.78, '#4f6742'], [1, '#627c53']]),
    /* the moss that takes a neglected lawn in the shade — yellow-green, flat,
       no blade structure at all. */
    moss: mkRamp([[0, '#232d15'], [.4, '#33401d'], [.72, '#465428'], [1, '#5b6a36']])
  };

  /* -------------------------------------------------------------------- lawn */
  function lawn(s, t, sd, px, py, st) {
    /* 1. UNIFORMITY. Grass leans on a 9-cell clump field for its sweep; a lawn
          has almost none, and what little it has shrinks as the care goes up. A
          mown sward that still has visible clumping has not been mown. */
    const cw = worley(s, t, 26, sd + 1, .95);
    const clump = cl(1 - cw.d1 / .70, 0, 1) * pk([.55, .30, .16], st);
    const base = fbm(s, t, 6, sd + 2, 3);
    const mid = fbm(s, t, 19, sd + 3, 3);
    const fine = fbm(s, t, 52, sd + 4, 2);
    const grain = fbm(s, t, 96, sd + 5, 2);

    /* ⚠ UNIFORM IS NOT FEATURELESS, and the first cut of this material got that
       wrong. Damping every scale at once produced flat green paint: measurably a
       lawn (low-frequency sigma 0.74 against Grass's 4.19) and visibly nothing at
       all. A real mown sward carries plenty of contrast at the scale of a few
       BLADES; what mowing takes away is the metre-scale blotching. So the swing
       below damps only the LOW frequencies, and the fine and grain terms above
       are given real weight to put the texture back. */
    const swing = pk([.30, .20, .14], st);

    let h = clump * .16 + base * .10 + mid * .14 + fine * .16 + grain * .10;

    const ct = .44 + (base - .5) * swing + (mid - .5) * (swing * .62)
             + (fine - .5) * .30 + (grain - .5) * .17
             + clump * .10 + pk([-.05, .00, .08], st);
    set(ramp(R.turf, cl(ct, 0, 1)));

    /* 2. MOSS — a neglected lawn's real tell, in the damp low-frequency dips.
          Gone by the crisp step. */
    const mossF = ss(.56, .86, fbm(s, t, 5, sd + 6, 3)) * pk([.72, .26, .06], st);
    if (mossF > .01) {
      const mc = ramp(R.moss, .22 + mid * .55);
      tint(mc[0], mc[1], mc[2], mossF * .80);
      h -= mossF * .05;                       /* moss sits below the sward line */
    }

    /* 3. CLOVER — rounder leaves in small colonies. An ordinary lawn has plenty,
          a kept one a trace, a neglected one is losing it to the moss. */
    const cv = worley(s, t, 34, sd + 7, .95);
    const clover = cl(1 - cv.d1 / .46, 0, 1) * ss(.48, .78, fbm(s, t, 11, sd + 8, 2))
                 * pk([.30, .50, .14], st);
    if (clover > .02) {
      const cc = ramp(R.clover, .30 + fr(cv.id * 5.3) * .5);
      tint(cc[0], cc[1], cc[2], cl(clover * .72, 0, 1));
      h += clover * .05;
    }

    /* 4. WEAR — scuffed ground where a lawn is walked or has never taken. Heavy
          at neglected, a trace at ordinary, absent when crisp. */
    const wear = ss(pk([.52, .78, .96], st), pk([.78, .96, 1.04], st), fbm(s, t, 15, sd + 9, 3))
               * (1 - clover * .5) * (.55 + .45 * grain);
    if (wear > .01) {
      const bc = ramp(P.soil, .24 + mid * .44);
      tint(bc[0], bc[1], bc[2], wear * pk([.62, .34, .16], st));
      h -= wear * .10;
    }

    /* 5. THE SWARD ITSELF — short, dense and standing up: more cells than Grass
          (finer blades), higher density (closed turf), about half the length, and
          little wind lean, because mown grass is not combed.
          ⚠ THE CELL COUNT IS A RESOLUTION BUDGET, NOT A STYLE KNOB. The first cut
          asked for 78–104 cells across a 256 px tile — 2.5 px per cell, with the
          blade a fifth of that — so every blade landed inside one texel and the
          whole sward vanished. Grass sits at 38–52 (≈5–7 px); a lawn can go finer
          but not below about FOUR pixels a cell or there is nothing to see. */
    const gb = blades(s, t, pk([44, 52, 66], st), sd + 61, pk([.56, .74, .90], st),
      pk([.42, .38, .34], st), pk([.45, .36, .28], st), .20, pk([.12, .08, .05], st));
    if (gb.v > .02) {
      const bc = ramp(R.turf, cl(.30 + gb.q * .34 + fr(gb.id * 7.7) * .22, 0, 1));
      tint(bc[0], bc[1], bc[2], cl(gb.v * pk([.62, .72, .82], st), 0, 1));
      h += gb.v * .13;

      /* ⭐ 6. THE CUT TIPS. The pale square end the mower left, on the top
            fraction of a blade only. This is the signature of the whole material
            — it is why a crisp lawn reads as CUT rather than merely short — and
            it is deliberately high-frequency so it survives the mip chain as a
            lightening instead of washing out to a flat pale colour. A neglected
            lawn has grown its tips out again, so it barely gets any. */
      const tipQ = ss(pk([.74, .58, .44], st), 1.0, gb.q);
      const tip = gb.v * tipQ * pk([.10, .30, .46], st);
      if (tip > .01) {
        /* ⚠ The tip follows the BLADE's own roll, not a fresh one, or every cut
           end lands on the same value and the sward speckles white. The first cut
           of this ran the ramp to #bcc78e at alpha .86 on a one-texel spot, which
           reads as dandruff — a cut end is a shade paler than the blade, not a
           different colour. */
        const tc = ramp(R.cutTip, .22 + fr(gb.id * 11.3) * .44);
        tint(tc[0], tc[1], tc[2], cl(tip, 0, 1));
        h += tip * .03;
      }
    }

    /* 7. NEGLECT ONLY: the first tussocks, and the odd seed head that got away.
          ⚠ The crisp and ordinary steps get NONE — a mown lawn does not grow seed
          heads, and their absence is what the eye reads first. Putting a trace on
          "ordinary" to make it look natural would undo the whole material. */
    if (st === 0) {
      const ts = blades(s, t, 30, sd + 63, .16, .9, .8, .11, .20);
      if (ts.v > .02) {
        const sc = ramp(P.grass, .34 + fr(ts.id * 13.1) * .30);
        tint(sc[0], sc[1], sc[2], cl(ts.v * .62, 0, 1));
        h += ts.v * .10;
        if (ts.q > .80) {
          const hc = ramp(P.straw, .34 + fr(ts.id * 17.7) * .34);
          tint(hc[0], hc[1], hc[2], cl(ts.v * ss(.80, .97, ts.q) * .70, 0, 1));
        }
      }
    }

    mul(.982 + .036 * hash(px, py, sd + 91));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ------------------------------------------------------------------ registry
     A lawn is the FLATTEST material in the kit, so the cavity and crown terms are
     the lowest here — a mown sward with Grass's relief reads as pasture. `flat`
     is pushed near the top of its range for the same reason: whatever
     low-frequency mean survives is exactly what a lawn does not have, and the
     shader's hashed per-chunk offset is allowed on it (the tile is isotropic;
     nothing directional is baked in — the mower stripes are drawn live). */
  const MATS = { lawn };
  const CFG = { lawn: { cav: .24, crown: .13, r: 3, lo: 30, hi: 224, flat: .86, flatR: .22 } };
  const SPEC = { lawn: { N: 256, m: 8, seed: 26611 } };

  Object.assign(T.MATS, MATS);
  Object.assign(T.CFG, CFG);
  Object.assign(T.SPEC, SPEC);

  return { MATS, CFG, SPEC, R };
})();
