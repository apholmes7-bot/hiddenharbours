/* Art/cliffRig.js — Hidden Harbours CLIFF FACE & TERRAIN LEDGE rig.
   Near-vertical sections the player never traverses. Their whole job is to say
   WHICH WAY THEY FACE.

   This rig deliberately breaks the terrain kit's "no baked light" contract, and
   that break is the point. A cliff face never rotates relative to the camera:
   its aspect is fixed the moment it is authored. So incident light, shadow
   colour temperature, and aspect-dependent weathering are all BAKED, which is
   what lets a player read a headland's shape at gameplay zoom. Plan-view
   materials keep the old contract; faces do not.

   Ships two consumption paths off one source of truth:
     FACES  — tileable albedo, s ALONG the cliff, t DOWN it. 384x288 = 12 x 9 m.
     LEDGES — 32x32 iso band sprites for 0.3-4 m risers, camera 3/4 from S @40deg.
     BROW   — RGBA decal: the hanging sod lip where plan meets face.
     TOE    — RGBA decal: undercut notch, salt-bleached basal beds, debris.

   API (globalThis.CliffRig)
     ROCKS   = ['sandstone','till','basalt']
     ASPECTS = ['W','SW','S','SE','E']
     face(rock, aspect, step, {W,H,seed})      -> {data,W,H}   opaque
     brow(aspect, step, {rock,W,H,seed})       -> {data,W,H}   RGBA
     toe (aspect, step, {rock,W,H,seed,feature}) -> {data,W,H} RGBA
     ledge(rock, piece, {band,aspect,gx,gy,seed,step}) -> {data,w:32,h:32}
     contact(piece)                            -> {data,w:32,h:32} RGBA
   step is 0 | 1 | 2  (Lo / base / Hi) exactly as terrainBake's ladder.
*/
globalThis.CliffRig = (function () {
  'use strict';

  const F = Math.floor, ABS = Math.abs;
  const clamp = (x, a, b) => x < a ? a : (x > b ? b : x);
  const lerp = (a, b, t) => a + (b - a) * t;
  const sm = t => t * t * (3 - 2 * t);
  const ss = (a, b, x) => { const t = clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };

  /* ---------------------------------------------------------------- noise
     Everything here is EXACTLY PERIODIC on the unit square. Lattice counts are
     integers, octaves double them, so a face tile wraps in s and (for the plain
     faces) in t as well. Strips clamp in t and only need s. */
  function ih(i, j, s) {
    let h = Math.imul(i | 0, 374761393) + Math.imul(j | 0, 668265263) + Math.imul(s | 0, 1274126177);
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
  }
  function pv(u, v, cx, cy, s) {
    const x = u * cx, y = v * cy;
    const xi = F(x), yi = F(y), xf = x - xi, yf = y - yi;
    const a0 = ((xi % cx) + cx) % cx, b0 = ((yi % cy) + cy) % cy;
    const a1 = (a0 + 1) % cx, b1 = (b0 + 1) % cy;
    const su = sm(xf), sv = sm(yf);
    const p00 = ih(a0, b0, s), p10 = ih(a1, b0, s), p01 = ih(a0, b1, s), p11 = ih(a1, b1, s);
    return lerp(lerp(p00, p10, su), lerp(p01, p11, su), sv);
  }
  function fb(u, v, cx, cy, s, oct) {
    let sum = 0, amp = 0.5, tot = 0, f = 1;
    oct = oct || 3;
    for (let i = 0; i < oct; i++) { sum += pv(u, v, cx * f, cy * f, s + i * 977) * amp; tot += amp; amp *= 0.5; f *= 2; }
    return sum / tot;
  }
  /* ridged, anisotropic — narrow across, long along. Flutes, rills, seeps. */
  function rid(u, v, cx, cy, s, oct) {
    let sum = 0, amp = 0.5, tot = 0, f = 1;
    oct = oct || 3;
    for (let i = 0; i < oct; i++) {
      const n = pv(u, v, cx * f, cy * f, s + i * 613);
      sum += (1 - ABS(n * 2 - 1)) * amp; tot += amp; amp *= 0.5; f *= 2;
    }
    return sum / tot;
  }

  /* 1D lattice of vertical joint traces that WANDER as they run down.
     Rule 5 from the terrain kit: joints are lines, not polygons. A 2D d2-d1 net
     reads as cracked glaze. Returns distance in u-units to the nearest trace,
     the trace id, and which side of it we are on. */
  const J = { d: 0, id: 0, side: 0, open: 0 };
  function joints(u, v, cells, s, wander) {
    let bd = 9, bid = 0, bsd = 0, bx = 0;
    const x = u * cells, xi = F(x);
    for (let k = xi - 1; k <= xi + 1; k++) {
      const kk = ((k % cells) + cells) % cells;
      const jit = (ih(kk, 0, s) - 0.5) * 0.62;
      const w = (pv(0.5, v, 1, 4, s + kk * 131) - 0.5) * wander
              + (pv(0.5, v, 1, 12, s + kk * 37) - 0.5) * wander * 0.34;
      const cx = (k + 0.5 + jit) / cells + w;
      const d = ABS(u - cx);
      if (d < bd) { bd = d; bid = kk; bsd = u < cx ? -1 : 1; bx = cx; }
    }
    J.d = bd; J.id = bid; J.side = bsd;
    J.open = ih(bid, 9, s);
    return J;
  }

  /* -------------------------------------------------------------- palettes
     PEI red beds, sampled off the references and off the existing kit so the
     cliff composites with ShorelineIso2 and the terrain materials. Every ramp
     runs the WHOLE way — black hollow to pale dry dust on a crown. A ramp that
     only uses its middle reads as a flat wall no matter what is drawn on it. */
  const RAMPS = {
    sandstone: ['#1c0b07', '#341410', '#4e1e15', '#6d2c1b', '#8d3d22', '#a8512c', '#c06a3a', '#d4884f', '#e3a76c', '#eec594', '#f6e0be'],
    till:      ['#1a0d07', '#301810', '#472317', '#61321d', '#7c4425', '#95582f', '#ac6d3c', '#c1874f', '#d4a36c', '#e4bf93'],
    basalt:    ['#0d1014', '#181d23', '#252c33', '#343d45', '#465059', '#5a656e', '#717c85', '#8b959d', '#a8b0b6', '#c6ccd0'],
    turf:      ['#101c0d', '#1a2c13', '#26401a', '#355622', '#476f2c', '#5b8a37', '#73a447', '#8fbd5e'],
    soil:      ['#170c06', '#2a170c', '#402313', '#57321b', '#6f4425'],
    /* the salt-bleached basal beds — the pale cream band in every low-tide ref */
    bleach:    ['#6b5a4c', '#8b7767', '#a99483', '#c4b1a0', '#dbcbbb', '#ece1d4'],
    /* the grey-green siltstone partings between the red beds */
    band:      ['#38332c', '#4c463c', '#61594c', '#776d5e', '#8d8271', '#a39784'],
    lichen:    ['#5c6152', '#7a8069', '#9aa085', '#b9bda1'],   /* grey-green crustose */
    xanth:     ['#8a6a1e', '#b58f2a', '#d8b043'],               /* orange sunlit lichen */
    moss:      ['#1b2a14', '#2b421d', '#3e5d28'],
    /* colluvium — the crumb and scree that only a SLOPED face can hold. Drier
       and greyer than the parent rock because it is broken and sun-baked. */
    grit:      ['#241610', '#38251b', '#503628', '#6a4b37', '#83614a', '#997a60'],
  };
  function ramp(list, t) {
    t = clamp(t, 0, 0.99999) * (list.length - 1);
    const i = F(t), f = t - i;
    const a = list[i], b = list[Math.min(i + 1, list.length - 1)];
    const ar = parseInt(a.slice(1, 3), 16), ag = parseInt(a.slice(3, 5), 16), ab = parseInt(a.slice(5, 7), 16);
    const br = parseInt(b.slice(1, 3), 16), bg = parseInt(b.slice(3, 5), 16), bb = parseInt(b.slice(5, 7), 16);
    return [ar + (br - ar) * f, ag + (bg - ag) * f, ab + (bb - ab) * f];
  }
  /* prebuilt 256-entry LUTs — the ramp call above is far too slow per pixel */
  const LUT = {};
  for (const k in RAMPS) {
    const a = new Float32Array(768);
    for (let i = 0; i < 256; i++) { const c = ramp(RAMPS[k], i / 255); a[i * 3] = c[0]; a[i * 3 + 1] = c[1]; a[i * 3 + 2] = c[2]; }
    LUT[k] = a;
  }
  const RGB = [0, 0, 0];
  function lut(k, t) {
    const i = clamp(t, 0, 1) * 255, i0 = i | 0, f = i - i0, i1 = i0 < 255 ? i0 + 1 : 255;
    const a = LUT[k];
    RGB[0] = a[i0 * 3] + (a[i1 * 3] - a[i0 * 3]) * f;
    RGB[1] = a[i0 * 3 + 1] + (a[i1 * 3 + 1] - a[i0 * 3 + 1]) * f;
    RGB[2] = a[i0 * 3 + 2] + (a[i1 * 3 + 2] - a[i0 * 3 + 2]) * f;
    return RGB;
  }

  /* ================================================================= ASPECT
     THE LAW OF THIS RIG.

     Camera is 3/4 from the south at 40deg (ADR 0006/0022). Key light upper-left
     of screen — from the WNW, high summer, elevation ~45deg. Five aspects can
     face that camera; N cannot and is not authored.

     What changes between them, and why each one earns its bake:

     inc   incident level. W is square to the key, E is in its own shadow.
     L     the light direction IN FACE SPACE (s right, t down, z out of the
           wall). A lit face is frontally lit, so relief is crisp and small.
           A shadowed face is lit by the SKY DOME — broad, top-down, soft. That
           single swap is most of why the near cliff in the reference reads as
           turned away while the far ones read as facing the sun.
     rel   relief gain. Frontal light flattens, so lit faces need less; the
           shadowed ones need their bed steps pushed or they go to mush.
     cool  how far the shadow pulls toward sky violet-blue. Every recess on a
           PEI cliff at any hour is violet, not grey.
     wind  prevailing NW-W scour: fresh rock, spall, dry pale dust, no plants.
     damp  the lee: seep stain, moss in the joints, crustose lichen, clinging
           vegetation. Wind and damp are near-inverses but not exactly.
     read  line-feature gain for foreshortening. W and E present a fraction of
           their true width on screen, so their joints and bed lips are drawn
           thicker and harder — otherwise they alias away at gameplay zoom.
           This is the whole "read the directions" ask made literal. */
  const ASPECT = {
    W:  { az: 270, inc: 1.00, L: [-0.24, -0.52, 0.82], rel: 0.90, cool: 0.08, warm: 1.00, wind: 1.00, damp: 0.08, read: 1.30, xan: 0.85 },
    SW: { az: 225, inc: 0.90, L: [-0.44, -0.56, 0.70], rel: 1.00, cool: 0.16, warm: 0.92, wind: 0.78, damp: 0.20, read: 1.08, xan: 1.00 },
    S:  { az: 180, inc: 0.72, L: [-0.60, -0.55, 0.58], rel: 1.06, cool: 0.30, warm: 0.72, wind: 0.44, damp: 0.42, read: 1.00, xan: 0.70 },
    SE: { az: 135, inc: 0.44, L: [-0.22, -0.74, 0.64], rel: 1.34, cool: 0.66, warm: 0.30, wind: 0.20, damp: 0.76, read: 1.10, xan: 0.22 },
    E:  { az:  90, inc: 0.34, L: [-0.10, -0.82, 0.56], rel: 1.48, cool: 0.84, warm: 0.16, wind: 0.08, damp: 1.00, read: 1.32, xan: 0.06 },
  };
  const ASPECTS = ['W', 'SW', 'S', 'SE', 'E'];
  const ROCKS = ['sandstone', 'till', 'basalt'];
  const SKY = [0.42, 0.55, 0.86];   /* sky dome fill */
  const SUN = [1.00, 0.89, 0.74];   /* direct key */
  const BOU = [0.72, 0.50, 0.36];   /* warm bounce off the red platform below */

  /* a grey rock cannot take a red rock's shadow temperature — push the same
     violet into basalt and it comes out navy */
  const COOLSCALE = { sandstone: 1, till: 0.9, basalt: 0.34 };
  const O = { h: 0, t: 0, dust: 0, fresh: 0, veg: 0, lich: 0, xan: 0, seep: 0, pale: 0, dark: 0, grit: 0, macro: 0 };

  /* ================================================================== SLOPE
     v9 baked one thing only: a vertical wall. Every face read as a wall from
     every angle because every face WAS one. batter is degrees from horizontal:
     90 is the sea cliff, 48 is a bank you could scramble up. It is not a
     cosmetic parameter — four things change with it and they all have to move
     together or the bake lies about its own geometry:

       1. LIGHT. The plane tips back, so the sun's angle to it changes. L is
          rotated into the tipped frame rather than faked with a multiplier,
          which is why a shaded E bank still catches a high sun.
       2. SKY. A tipped face sees more of the dome, so ambient rises.
       3. BEDDING SPACING. Beds are horizontal in the WORLD. Measured down a
          sloped surface they are 1/sin further apart, so the bake carries
          fewer of them per texture metre. This is the foreshortening tell.
       4. WEATHERING. A wall shrugs debris off; a slope keeps it. Colluvium in
          the hollows, plants with a foothold, water running ON the face
          instead of dripping clear of it, and no undercut lip to speak of. */
  const SLOPES = { wall: 90, steep: 76, ramp: 62, bank: 48 };
  const RELIEF_M = 1.15;  /* plan depth of the full height-field range, metres */
  const MACW = 0.60;      /* how much of the form rides in the combined height field */
  const PXM = 32;
  const SL = { deg: 90, th: 0, tf: 0, sin: 1, cos: 0, vs: 1 };
  function setSlope(v) {
    let d = v == null ? 90 : (typeof v === 'string' ? (SLOPES[v] || 90) : v);
    d = clamp(d, 30, 90);
    const rad = d * Math.PI / 180, th = (90 - d) * Math.PI / 180;
    SL.deg = d; SL.th = th; SL.tf = clamp(th / (Math.PI / 4), 0, 1.5);
    SL.sin = Math.sin(rad); SL.cos = Math.cos(rad);
    SL.vs = SL.sin;
    return SL;
  }

  /* ============================================================ MACRO FORM
     And the other half of the same complaint: a wall that is a PLANE, however
     well its beds are drawn, is a plane. This is the form under the geology —
     the shape you would still see at 60 m.

       RIBS      resistant buttress noses at 3 m and 1.3 m, wandering in plan as
                 they run down, swelling at mid height, dying into brow and toe.
       CLEFTS    where the profile is already deepest a narrow chimney cuts in
                 further. Clefts are what make ribs read as ribs.
       BENCHES   two or three near-horizontal step-backs. The only broad relief
                 on a cliff that is not vertical, and the thing that catches
                 debris and light.

     Big in amplitude, gentle in gradient: form, not noise. It is also the only
     channel the ENGINE needs geometry for — profile() hands the same field back
     in metres so the wall can actually bend, and then the silhouette, the cast
     shadow and the shading all agree. */
  const FM = { h: 0, bench: 0, cleft: 0 };
  let FK = 1;                    /* form amplitude — 0 gives the v9 flat wall back */
  function setForm(k) { FK = k == null ? 1 : k; }
  /* The form field depends only on the ROCK SEED and the BATTER — not on aspect,
     not on the wear step. Fifteen bakes of one rock at one batter therefore share
     it, so it is computed once per (seed, slope) and memoised along with the wide
     blur that the macro AO needs. This is most of the bake time back. */
  const FCACHE = {};
  function formField(R, W, H) {
    const key = R + '|' + SL.deg + '|' + W + 'x' + H + '|' + FK;
    if (FCACHE[key]) return FCACHE[key];
    const N = W * H;
    const h = new Float32Array(N), bn = new Float32Array(N), cl = new Float32Array(N);
    for (let y = 0; y < H; y++) {
      const v = (y + 0.5) / H;
      for (let x = 0; x < W; x++) {
        const i = y * W + x;
        form((x + 0.5) / W, v, R);
        h[i] = FM.h; bn[i] = FM.bench; cl[i] = FM.cleft;
      }
    }
    /* wide separable box blur of the form field, for the macro occlusion term */
    const mb = new Float32Array(N), t2 = new Float32Array(N), rx = 20, ry = 14;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      let s = 0;
      for (let k = -rx; k <= rx; k++) s += h[y * W + (((x + k) % W) + W) % W];
      t2[y * W + x] = s / (2 * rx + 1);
    }
    for (let x = 0; x < W; x++) for (let y = 0; y < H; y++) {
      let s = 0;
      for (let k = -ry; k <= ry; k++) s += t2[clamp(y + k, 0, H - 1) * W + x];
      mb[y * W + x] = s / (2 * ry + 1);
    }
    const e = { h, bn, cl, mb };
    const keys = Object.keys(FCACHE);
    if (keys.length > 6) delete FCACHE[keys[0]];
    FCACHE[key] = e;
    return e;
  }
  function form(u, v, R) {
    const wob = (pv(0.5, v, 1, 3, R + 310) - 0.5) * 0.11;
    const uu = u + wob;
    /* Ribs are built on an explicit ALTERNATING lattice, not on value noise.
       Six cells of value noise do not span their own range — this seed's came
       out entirely above the mean, which is how the first attempt produced a
       constant offset and a flat wall. A cliff in plan alternates: buttress,
       re-entrant, buttress. So alternate the sign per cell and let the hash set
       the amplitude. Stretch and clip afterwards: the clipped plateaus are the
       rib crowns and the gully floors, and the fast run between them is the
       FLANK, where all of the form shading lives. */
    const NR = 6;                       /* 6 cells over 12 m -> 4 m period */
    const xr = uu * NR, ci = F(xr), rf = sm(xr - ci);
    const amp1 = i => { const k = ((i % NR) + NR) % NR; return (k % 2 ? 1 : -1) * (0.34 + ih(k, 320, R) * 0.66); };
    const rib = clamp(lerp(amp1(ci), amp1(ci + 1), rf) * 1.55, -1, 1);
    const N2 = 14;                      /* 0.86 m — the coarse unevenness */
    const x2 = uu * N2 + 0.37, c2 = F(x2), r2f = sm(x2 - c2);
    const amp2 = i => { const k = ((i % N2) + N2) % N2; return (k % 2 ? 1 : -1) * (0.20 + ih(k, 321, R) * 0.80); };
    const rib2 = lerp(amp2(c2), amp2(c2 + 1), r2f);
    const rib3 = (pv(uu, v, 17, 3, R + 308) - 0.5) * 2;
    let raw = (rib * 0.62 + rib2 * 0.26) * 0.55 + rib3 * 0.11;
    const vprof = ss(0, 0.16, v) * (1 - ss(0.80, 1.0, v) * 0.35) * (1 - SL.tf * 0.32);
    let out = raw * vprof;
    /* clefts: narrow, deep, and only ever in a re-entrant */
    const cj = joints(uu, v, 5, R + 303, 0.045);
    const cw = 0.10 + cj.open * 0.12;
    const cleft = (1 - ss(cw * 0.4, cw * 1.7, cj.d * 5))
                * ss(0.06, 0.34, -raw) * ss(0.24, 0.60, ih(cj.id, 307, R));
    out -= cleft * 0.62 * vprof;
    /* benches: three step-backs, periodic in t so the face still tiles. The
       band under each line sits BACK and its tread lip stands proud — a step,
       not a scratch. Deeper on a slope, where the wall really does terrace. */
    const bv = v * 3 + (pv(uu, 0.5, 3, 1, R + 304) - 0.5) * 0.85;
    const bi = F(bv), bf = bv - bi, b3 = ((bi % 3) + 3) % 3;
    /* and a bench DIES OUT along its length. A step of even depth running the
       whole 12 m is a moulding, exactly like an unbroken bed lip. */
    const alive = ss(0.26, 0.70, pv(uu, 0.5, 5, 1, R + 309 + b3 * 37));
    const back = (ih(b3, 306, R) - 0.5) * 0.52 * (0.35 + SL.tf * 0.85) * (0.30 + alive * 0.70);
    const lip = (1 - ss(0, 0.14, bf)) * ss(0.30, 0.72, ih(b3, 305, R)) * alive;
    out += back + lip * 0.24;
    FM.h = out * FK; FM.bench = lip; FM.cleft = cleft;
    return FM.h;
  }

  /* what a slope does to the surface, applied at the end of every field so all
     three rocks answer the batter the same way */
  function slopeWeather(u, v, R, A, st) {
    const tf = SL.tf;
    if (tf < 0.002) { O.grit = 0; return; }
    const dep = ss(0.16, 0.80, fb(u, v, 7, 5, R + 320, 2)) * (0.30 + ss(0.05, 0.95, v) * 0.70);
    /* it collects in the hollows and on the benches, never as an even film — and
       never as a clean band along a bench, which is a painted stripe */
    const hollow = ss(0.66, 0.28, O.h);
    const patchy = ss(0.22, 0.78, fb(u, v, 14, 9, R + 322, 2));
    const g = clamp((dep * (0.35 + hollow * 0.85) + FM.bench * 0.50 * patchy + FM.cleft * 0.30) * tf, 0, 1);
    O.grit = g * 0.92;
    O.h += g * 0.055;
    O.veg = clamp(O.veg + g * (0.22 + A.damp * 0.60) * tf, 0, 1);
    O.dust *= 1 - tf * 0.34;
    O.seep *= 1 + tf * 0.45;
    O.lich = clamp(O.lich * (1 + tf * 0.28), 0, 1);
  }
  /* ============================================================= SANDSTONE
     The hero. Permo-Carboniferous red bed, seen face on.

     Two scales of bedding, which is the single most important thing the
     references say and the thing v8 got wrong: THICK MASSIVE UNITS (1.5-2 m)
     each divided into THIN BEDS (~25 cm / 8 px). v8's 7-13 px beds with no unit
     structure came out as brickwork. Real beds run in packets — a metre of
     laminated flags, then a massive unit, then flags again.

     Per-bed hardness drives relief; the cavity and gradient passes draw the lip
     and the undercut, so no line is ever painted in directly (rule: painting a
     symmetric dark line into a riser is what made an early shelf read as dried
     mud).

     Joints are a 1D lattice of wandering traces with per-trace openness.
     Where a joint crosses a bed, a block can be PLUCKED — a rectangular socket
     with a dark interior and one fresh bright edge. The reference walls are
     mostly sockets. That is the ladder: Lo few, Hi a mosaic. */
  function sandstone(u, v, R, A, st) {
    const rd = A.read;
    const mac = FM.h;

    /* bedding sag — never a ruler line */
    const sag = (fb(u, v, 2, 2, R + 31, 3) - 0.5) * 0.055
              + (fb(u, v, 6, 4, R + 32, 2) - 0.5) * 0.018;
    const vv = v + sag;

    /* ---- two scales of bedding ----
       6 units over 9 m (1.5 m), each cut into ~36 beds over 9 m (25 cm). Unit
       identity is what stops the wall reading as one ladder: a unit is either
       MASSIVE (few, thick, standing proud, blocky) or a LAMINATED PACKET (many
       thin flags, recessive). The reference walls alternate between the two
       every metre or so, and that alternation is most of the silhouette.

       Bed THICKNESS varies — a warp on the bed coordinate compresses some to
       3 px and opens others to 25 px. A stack of identical beds reads as
       decking however well it is shaded. */
    const NB = Math.max(8, Math.round(36 * SL.vs)), NU = 6, BPU = NB / NU;
    const bwarp = (pv(0.5, vv, 1, 9, R + 40) - 0.5) * 2.6 + (pv(0.5, vv, 1, 24, R + 41) - 0.5) * 1.1;
    const bfl = vv * NB + bwarp, bi = F(bfl), bf = bfl - bi;
    const bm = ((bi % NB) + NB) % NB;
    const um = ((F(bm / BPU) % NU) + NU) % NU;
    const uf = (bm % BPU) / BPU;                 /* position within the unit */

    const massive = ss(0.42, 0.72, ih(um, 7, R));
    const uProt = (ih(um, 8, R) - 0.5) * 0.30 + massive * 0.16;   /* whole unit stands out or back */

    /* ---- BLOCKS ----
       The wall is not beds, it is BLOCK ENDS. Every (bed x joint cell) is a
       block with its own protrusion, and the dark line round it falls out of
       the height field — never painted. Master joints persist across the whole
       stack; the block ends inside a bed stagger off a per-bed offset, so the
       wall does not comb into columns. Both are in the photographs. */
    const MJ = 5;
    const mj = joints(u, v, MJ, R + 71, 0.018);
    const mjd = mj.d * MJ;
    const mjw = (0.012 + mj.open * 0.022 * (0.5 + st)) * rd;
    /* a joint dies out and resumes down its length — one that runs the full
       height at an even width is a ruled line, and five of them are a grid */
    const mjLive = ss(0.30, 0.58, pv(0.5, v, 1, 5, R + 82 + mj.id * 53))
                 * (0.45 + mj.open * 0.55);
    const master = (1 - ss(mjw * 0.5, mjw * 1.6, mjd)) * mjLive;

    const BC = 6 + F(ih(um, 12, R) * 5);         /* 6-10 blocks over 12 m = 1.2-2 m */
    const boff = ih(bm, 13, R) * 0.9 + mj.id * 0.17;
    const bx = (u + boff) * BC, bxi = F(bx);
    const blkId = ((bxi % BC) + BC) % BC;
    /* jitter the block boundary so widths vary within a bed */
    const bxc = bxi + 0.5 + (ih(blkId, 19, R + bm) - 0.5) * 0.5;
    const bxf = clamp((bx - bxc) + 0.5, 0, 1);
    const blk = ih(blkId * 97 + bm, 14, R);

    /* The bed is the primary object: a flag that stands proud or sits back as a
       WHOLE, running metres along the cliff. Blocks are secondary and SPARSE —
       most of them sit flush with their bed. Letting every block move is what
       turns a cliff into a brick wall. */
    const bedProt = (ih(bm, 1, R) - 0.45) * lerp(0.34, 0.20, massive);
    const active = ss(0.44, 0.66, ih(blkId * 17 + bm, 18, R));
    let prot = (blk - 0.5) * lerp(0.16, 0.52, massive) * active;

    /* plucked: a block gone entirely. The ladder is how many. Its socket is a
       RECESS with rock in it, not a void — the fresh face inside is brighter
       and redder than the weathered wall, which is what the references show. */
    const pk = ih(blkId * 31 + bm * 7, 3, R + 5);
    const gone = ss(0.82 - st * 0.30, 0.88 - st * 0.30, pk);
    prot -= gone * (0.26 + ih(blkId + bm, 15, R) * 0.14);

    /* block edges: soften toward the boundary so blocks are not slabs */
    const edgeS = 1 - ss(0.30, 0.50, ABS(bxf - 0.5));
    const edgeT = 1 - ss(0.30, 0.50, ABS(bf - 0.5));
    const core = clamp(edgeS * 0.6 + edgeT * 0.6, 0, 1);

    /* internal lamination — only in the laminated packets, dip flips per bed */
    const dip = (ih(bm, 5, R) - 0.5) * 0.9;
    const lamF = 4 + F(ih(bm, 6, R) * 4);
    const lam = (pv(u + dip * bf * 0.1, bf, 3, lamF, R + bm * 13) - 0.5)
              * (1 - massive) * 0.085;

    /* the parting at a UNIT boundary is a real recess, far deeper than a bed */
    const part = (1 - ss(0.0, 0.16, uf)) * (0.5 + ih(um, 16, R) * 0.5);

    /* alveolar honeycomb — salt weathering, sun faces, weak beds only */
    const alv = ss(0.54, 0.82, fb(u, v, 26, 22, R + 44, 2)) * (1 - massive)
              * (0.25 + A.wind * 0.75) * st * 0.8;

    /* vertical erosion flutes on the soft packets */
    const flute = ss(0.60, 0.92, rid(u, v, 20, 2, R + 51, 2)) * (1 - massive)
                * (0.25 + st * 0.5) * ss(0.18, 0.48, fb(u, v, 3, 2, R + 52, 2));

    /* broad relief: the wall bulges and recedes at 3-4 m, so it is never flat.
       Kept small in both channels — as height it reads as drapery, as colour it
       reads as a stain washed over the geology. */
    const bulge = (fb(u, v, 3, 2, R + 80, 3) - 0.5) * 0.10;

    /* fine spall and chip — the high-frequency fracture that says rock rather
       than timber. Without it every bed is an extruded plank. */
    const chip = ss(0.60, 0.86, fb(u, v, 64, 50, R + 42, 2));
    const rough = ss(0.32, 0.70, fb(u, v, 38, 30, R + 43, 2));

    const gr = pv(u, v, 384, 288, R + 90) - 0.5;

    /* ---- height ---- */
    let h = 0.50 + uProt * 0.5 + bedProt + prot * core + lam + bulge;
    h -= master * (0.16 + mj.open * 0.30) * A.rel;
    h -= part * 0.18;
    h -= alv * 0.13;
    h -= flute * 0.11;
    h -= chip * 0.07;
    h += gr * 0.030;
    /* every bed's own top lip stands proud, its foot is undercut. One
       asymmetric term; the gradient pass draws the lip and the shadow. Broken
       along its length by the roughness field — an unbroken lip is a moulding. */
    h += (ss(0.34, 0.03, bf) * 0.9 - ss(0.70, 0.99, bf)) * 0.10 * A.rel * SL.sin * (0.25 + rough * 0.95);

    /* ---- albedo ----
       Colour identity lives on the BED and the UNIT, not the block. Give every
       block its own value and the wall comes out as confetti. */
    let t = 0.46 + (ih(bm, 2, R) - 0.5) * 0.25 + (ih(um, 21, R) - 0.5) * 0.14
          + (blk - 0.5) * 0.05
          + lam * 1.6 + bulge * 0.9
          + (pv(u, v, 24, 20, R + 12) - 0.5) * 0.10
          + massive * 0.05 + gr * 0.055 + mac * 0.10;

    /* dry pale dust on crowns, scoured faces get much more of it. This is what
       carries the top of the ramp — without it the wall stays a brown slab. */
    O.dust = ss(0.50, 0.86, h + prot * 0.4) * (0.22 + A.wind * 0.78) * (0.55 + st * 0.35);
    /* fresh rock in a socket and along an open joint: redder, more saturated */
    O.fresh = gone * 0.85 + master * mj.open * 0.30;
    O.dark = part * 0.30 + alv * 0.30 + gone * 0.18;

    /* seep stains — from a parting, running down, lee faces far more */
    O.seep = ss(0.64, 0.90, rid(u, v, 9, 1, R + 61, 2))
           * ss(0.30, 0.62, fb(u, v, 4, 2, R + 62, 2))
           * (0.10 + A.damp * 0.90) * (0.35 + st * 0.5);

    /* life. Crustose lichen loves the lee; orange Xanthoria the sunlit crowns;
       vegetation only in the joints and only where it is damp. */
    O.lich = ss(0.52, 0.80, fb(u, v, 16, 12, R + 71, 3)) * A.damp * 0.70 * ss(0.35, 0.75, h);
    O.xan = ss(0.64, 0.88, fb(u, v, 20, 15, R + 72, 2)) * A.xan * ss(0.55, 0.88, h) * 0.45;
    O.veg = master * ss(0.50, 0.78, fb(u, v, 6, 5, R + 73, 2)) * A.damp * 0.6;
    /* pale grey-green siltstone bands — every few beds the red bed is not red
       at all, and that is what stops a red cliff reading as one material.
       Broken along its length, or it is a mortar course. */
    O.pale = ss(0.84, 0.94, ih(bm, 22, R)) * 0.55
           * ss(0.24, 0.58, fb(u, v, 5, 2, R + 83, 2));
    O.macro = mac; O.h = h; O.t = t;
    slopeWeather(u, v, R, A, st);
  }

  /* ================================================================== TILL
     The soft cliff — red boulder clay over the rock, or a whole bank of it.
     Its character is RILL GULLIES, three calibres, all long in t, plus slump
     benches cutting across them and turf mats that have slid down the face.
     Never let a cell field carry the wash here: cells draw stones, noise draws
     the mud. */
  function till(u, v, R, A, st) {
    const rd = A.read;
    const mac = FM.h;
    const wob = (fb(u, v, 3, 2, R + 21, 3) - 0.5) * 0.048 + (fb(u, v, 9, 3, R + 22, 2) - 0.5) * 0.014;
    const us = u + wob;

    /* THE GULLIES ARE THE MATERIAL. Built on the same wandering-trace lattice
       as the joints, because that is what draws a LINE. Thresholding ridged
       fbm gives smooth swells, not incisions — an earlier pass lost the whole
       character that way. Three calibres, all long in t, each gated so it runs
       in patches and the bank clears between them: a rill field at even
       amplitude edge to edge reads as corduroy. */
    const g1j = joints(us, v, 9, R + 1, 0.030);
    const g1d = g1j.d * 9, g1w = 0.09 + g1j.open * 0.20, g1o = g1j.open;
    const g1live = ss(0.20, 0.55, pv(0.5, v, 1, 3, R + 51 + g1j.id * 71));
    const G1 = (1 - ss(g1w * 0.35, g1w * 1.5, g1d)) * g1live * (0.55 + st * 0.55) * (1 - SL.tf * 0.34);

    const g2j = joints(us, v, 26, R + 2, 0.014);
    const g2d = g2j.d * 26, g2w = 0.10 + g2j.open * 0.16;
    const g2live = ss(0.24, 0.60, pv(0.5, v, 1, 5, R + 52 + g2j.id * 37));
    const G2 = (1 - ss(g2w * 0.4, g2w * 1.6, g2d)) * g2live * (0.5 + st * 0.5) * (1 - SL.tf * 0.30);

    const G3 = ss(0.60, 0.92, rid(us, v, 62, 6, R + 3, 2)) * 0.5;

    /* slump benches — horizontal, they interrupt the rills and throw the only
       shadow on the face that is not vertical */
    const bn = rid(u, v, 1, Math.max(3, Math.round(7 * SL.vs)), R + 6, 2);
    const bench = ss(0.68, 0.96, bn) * ss(0.3, 0.7, fb(u, v, 4, 2, R + 7, 2)) * (0.35 + st * 0.6)
                * (1 + SL.tf * 1.30);   /* a bank terraces; a wall does not */

    /* clasts: stones in the matrix, on a PERIODIC anisotropic lattice, carried
       mostly in the HEIGHT field with a red dust film over them. Drawn as
       colour they come out as confetti; two earlier passes did exactly that.
       They are a texture on the bank, never the subject of it. */
    const CX = 22, CY = 16;
    const cx = F(u * CX), cy = F(v * CY);
    let stone = 0;
    for (let dj = -1; dj <= 0; dj++) for (let di = -1; di <= 0; di++) {
      const ix = cx + di, iy = cy + dj;
      const kx = ((ix % CX) + CX) % CX, ky = ((iy % CY) + CY) % CY;
      if (ih(kx, ky, R + 10) < 0.80) continue;
      const px = (ix + 0.25 + ih(kx, ky, R + 8) * 0.5) / CX;
      const py = (iy + 0.25 + ih(kx, ky, R + 9) * 0.5) / CY;
      const rx = 0.005 + ih(kx, ky, R + 11) * 0.007;
      const ry = rx * (0.5 + ih(kx, ky, R + 12) * 0.45);
      const d = Math.hypot((u - px) / rx, (v - py) / ry);
      if (d < 1) { const k = Math.sqrt(1 - d * d); if (k > stone) stone = k; }
    }

    /* hanging turf mats — half dead, torn edged, and only at Hi */
    const mat = ss(0.68, 0.82, fb(u, v, 5, 7, R + 13, 3)) * ss(0.12, 0.45, v) * st * 0.9;
    /* grass tongues running DOWN the gullies, straight off the reference */
    const tongue = ss(0.58, 0.88, rid(u, v, 11, 1, R + 14, 2)) * ss(0.02, 0.34, v)
                 * (0.3 + A.damp * 0.7) * (0.3 + st * 0.5);

    /* dried crust on the interfluves — the pale ribs between the gullies */
    const crust = ss(0.35, 0.75, fb(u, v, 11, 8, R + 15, 2)) * (1 - G1) * (1 - G2 * 0.6) * (1 - SL.tf * 0.45);

    const gr = pv(u, v, 384, 288, R + 90) - 0.5;

    let h = 0.60 - G1 * (0.30 + g1o * 0.22) - G2 * 0.15 - G3 * 0.06
          + bench * 0.14 * A.rel + stone * 0.07 + mat * 0.09 + gr * 0.032;

    /* the gully is dark in VALUE as well as in relief. On a soft bank the
       shadow is not enough on its own — wet shaded mud really is darker mud. */
    let t = 0.48 + (pv(u, v, 12, 9, R + 16) - 0.5) * 0.13
          - G1 * 0.24 - G2 * 0.12 - G3 * 0.05 + crust * 0.18
          + stone * 0.03 + gr * 0.06 + mac * 0.09;

    O.dust = (crust * 0.55 + ss(0.62, 0.90, h) * 0.7) * (0.25 + A.wind * 0.75) * (0.35 + st * 0.35);
    O.fresh = (G1 * 0.45 + bench * 0.35) * (0.4 + st * 0.4);
    O.dark = G1 * 0.26 + G2 * 0.10;
    O.seep = ss(0.62, 0.90, rid(u, v, 8, 1, R + 17, 2)) * (0.15 + A.damp * 0.85) * 0.5;
    O.veg = clamp(mat + tongue, 0, 1) * 0.95;
    O.lich = 0;
    O.xan = 0;
    O.pale = 0;
    O.macro = mac; O.h = h; O.t = t;
    slopeWeather(u, v, R, A, st);
  }

  /* ================================================================ BASALT
     The second region — a grey headland. Columnar jointing: a 1D lattice of
     column boundaries running down t, with an entablature zone at the top where
     the columns break into chaotic blocky rubble. Orange Xanthoria on the
     sunlit aspects is the aspect tell that does the most work here, because the
     value range is narrow and grey. */
  function basalt(u, v, R, A, st) {
    const rd = A.read;
    const mac = FM.h;
    /* colonnade below, entablature above — the boundary wanders, it is not a
       ruled horizon. Everything here is periodic in u; scaling u by a
       non-integer is what put hard rectangles in an earlier pass. */
    const ent = ss(0.42 + SL.tf * 0.26, 0.10, v + (fb(u, 0.5, 3, 1, R + 20, 2) - 0.5) * 0.30);

    /* columns: 18 over 12 m = 0.67 m, which is life size for a basalt colonnade */
    const NC = 18;
    const jt = joints(u, v, NC, R + 30, 0.012);
    const jd = jt.d * NC;
    const jw = (0.055 + jt.open * 0.075) * rd;
    const groove = (1 - ss(jw * 0.5, jw * 1.7, jd));

    /* each column breaks into segments at its OWN heights */
    const SEGN = 7;
    const sv = v * SEGN + ih(jt.id, 41, R) * SEGN + (pv(0.5, v, 1, 3, R + 45 + jt.id * 13) - 0.5) * 2.4;
    const si = F(sv), sf = sv - si;
    const sm2 = ((si % SEGN) + SEGN) % SEGN;
    const segLine = (1 - ss(0.03, 0.10, Math.min(sf, 1 - sf))) * (0.25 + st * 0.4)
                  * ss(0.30, 0.62, ih(jt.id * 17 + sm2, 44, R));
    const segProt = (ih(jt.id * 53 + sm2, 42, R) - 0.5) * 0.22;

    /* entablature: chaotic rubble, no columns at all */
    const blk = ss(0.44, 0.70, fb(u, v, 20, 24, R + 32, 2));
    const rub = ss(0.52, 0.78, fb(u, v, 44, 40, R + 33, 2));

    const gr = pv(u, v, 384, 288, R + 90) - 0.5;

    const colH = 0.62 + segProt * (1 - ent) - groove * (0.16 + jt.open * 0.16) * A.rel - segLine * 0.13;
    const entH = 0.50 + blk * 0.24 - rub * 0.10;
    let h = lerp(colH, entH, ent) + gr * 0.028;

    let t = 0.54 + (ih(jt.id, 2, R) - 0.5) * 0.12 * (1 - ent)
          + (pv(u, v, 18, 16, R + 34) - 0.5) * 0.10
          + (ih(jt.id * 53 + sm2, 43, R) - 0.5) * 0.07 * (1 - ent)
          + blk * 0.10 * ent + gr * 0.05 + mac * 0.08;

    O.dust = ss(0.62, 0.92, h) * (0.2 + A.wind * 0.8) * 0.45;
    O.fresh = groove * jt.open * 0.25;
    O.dark = groove * 0.22 + segLine * 0.14;
    O.seep = ss(0.66, 0.92, rid(u, v, 10, 1, R + 35, 2)) * (0.2 + A.damp * 0.8) * 0.45;
    O.lich = ss(0.46, 0.76, fb(u, v, 15, 13, R + 36, 3)) * A.damp * 0.85 * ss(0.35, 0.78, h);
    /* the orange Xanthoria is the aspect tell that does the most work on a grey
       rock — the value range is narrow, so let the lichen carry the direction */
    O.xan = ss(0.50, 0.78, fb(u, v, 19, 16, R + 37, 2)) * A.xan * ss(0.5, 0.86, h) * 0.9;
    O.veg = groove * ss(0.52, 0.80, fb(u, v, 7, 6, R + 38, 2)) * A.damp * 0.45;
    O.pale = 0;
    O.macro = mac; O.h = h; O.t = t;
    slopeWeather(u, v, R, A, st);
  }

  const FIELD = { sandstone, till, basalt };

  /* ============================================================ SHADING PASS
     What makes a face read as facing a direction.

     1. Gradient of the height field gives a face-space normal.
     2. Direct key: N . L at the aspect's incident level, in sun colour.
        On the lit aspects L is frontal, so relief is crisp and local.
        On the shadowed ones the "key" is the sky dome — top-down and broad —
        and the incident level is a quarter of W's. That swap is the read.
     3. Cavity/crown from the height field against its own blur: a
        non-directional occlusion term that survives a day/night grade.
     4. Warm bounce off the red platform below lifts the underside of every
        overhang, which is exactly what the low-tide references show.
     5. Shadow pulls toward sky violet by the aspect's cool coefficient. */
  function shade(rk, W, H, A, st, seed, alpha, mode, opt) {
    opt = opt || {};
    setSlope(opt.slope);
    setForm(opt.form);
    const N = W * H;
    const hf = new Float32Array(N);       /* detail + form, for the cavity term */
    const det = new Float32Array(N);      /* detail only */
    const mf = new Float32Array(N);       /* macro form only — normal, AO, cast shadow */
    const tf = new Float32Array(N);
    const ex = new Float32Array(N * 7);   /* dust fresh veg lich xan seep grit */
    const pf = new Float32Array(N);       /* pale (bleach) */
    const df = new Float32Array(N);       /* dark */
    const fn = FIELD[rk];
    const R = seed;
    const FF = formField(R, W, H);

    for (let y = 0; y < H; y++) {
      const v = (y + 0.5) / H;
      for (let x = 0; x < W; x++) {
        const u = (x + 0.5) / W;
        const i = y * W + x;
        FM.h = FF.h[i]; FM.bench = FF.bn[i]; FM.cleft = FF.cl[i];
        fn(u, v, R, A, st);
        /* detail and form are kept APART. They are shaded at different reliefs:
           the detail multiplier is an artistic 8.4, the form multiplier is the
           real one — RELIEF_M metres over a texel of 1/32 m. Shading a buttress
           at the fine-detail relief is exactly how v9 ended up with a plane. */
        det[i] = O.h; mf[i] = O.macro;
        hf[i] = O.h + O.macro * MACW;
        tf[i] = O.t; pf[i] = O.pale; df[i] = O.dark;
        const e = i * 7;
        ex[e] = O.dust; ex[e + 1] = O.fresh; ex[e + 2] = O.veg;
        ex[e + 3] = O.lich; ex[e + 4] = O.xan; ex[e + 5] = O.seep; ex[e + 6] = O.grit;
      }
    }
    if (opt.post) opt.post(hf, tf, ex, pf, df, W, H, A, st);

    /* ---- the sun, rotated into the tipped frame ----
       A batter of (90 - deg) tips the plane back about the s axis, so instead of
       fudging the incident level we rotate L and let N.L answer honestly. This
       is why a shaded E bank still catches a high sun and a vertical E wall
       does not. */
    const ct = Math.cos(SL.th), stn = Math.sin(SL.th);
    const Lx = A.L[0];
    const Ly = A.L[1] * ct + A.L[2] * stn;
    const Lz = -A.L[1] * stn + A.L[2] * ct;

    /* separable box blur of h for the cavity term (wraps in s, clamps in t) */
    const r = 4, tmp = new Float32Array(N), bl = new Float32Array(N);
    for (let y = 0; y < H; y++) {
      for (let x = 0; x < W; x++) {
        let s = 0;
        for (let k = -r; k <= r; k++) s += hf[y * W + (((x + k) % W) + W) % W];
        tmp[y * W + x] = s / (2 * r + 1);
      }
    }
    for (let x = 0; x < W; x++) {
      for (let y = 0; y < H; y++) {
        let s = 0;
        for (let k = -r; k <= r; k++) s += tmp[clamp(y + k, 0, H - 1) * W + x];
        bl[y * W + x] = s / (2 * r + 1);
      }
    }

    /* ---- macro pass: broad occlusion and a real CAST SHADOW ----
       The fine cavity term above is a local crown/hollow measure and it cannot
       see a buttress. Two things are needed for form to read as form:
         mAO  the macro field against a wide blur of itself — non-directional,
              so it goes in unlit and in normal.A with the cavity.
         shd  a horizon march along L, in image space, over the macro field
              only. A rib in the morning throws a long shadow along the wall; a
              bench under a high sun throws one down it. This is the single
              biggest depth cue and no normal map can fake it — which is also
              why it stays out of unlit and comes back as mask.R. */
    const mb = FF.mb;
    const shd = new Float32Array(N);
    {
      const dpx = RELIEF_M * PXM;                 /* height units -> texels of plan depth */
      const lh = Math.hypot(Lx, Ly) || 1e-4;
      const ux = Lx / lh, uy = Ly / lh;
      const rate = Lz / lh;                       /* texels out of the wall per texel along it */
      const KS = [2, 4, 7, 11, 16, 23, 32, 44, 60];
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        const i = y * W + x, d0 = mf[i] * dpx;
        let sh = 0;
        for (let q = 0; q < KS.length; q++) {
          const k = KS[q];
          const sx = (((x + Math.round(k * ux)) % W) + W) % W;
          const sy = clamp(y + Math.round(k * uy), 0, H - 1);
          const over = mf[sy * W + sx] * dpx - (d0 + k * rate);
          if (over > 0) { const p = clamp(over / 7, 0, 1); if (p > sh) sh = p; }
        }
        shd[i] = sh;
      }
      const t3 = new Float32Array(N), rs = 3;
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        let s = 0;
        for (let k = -rs; k <= rs; k++) s += shd[y * W + (((x + k) % W) + W) % W];
        t3[y * W + x] = s / (2 * rs + 1);
      }
      for (let x = 0; x < W; x++) for (let y = 0; y < H; y++) {
        let s = 0;
        for (let k = -rs; k <= rs; k++) s += t3[clamp(y + k, 0, H - 1) * W + x];
        shd[y * W + x] = s / (2 * rs + 1);
      }
    }

    const out = new Uint8ClampedArray(N * 4);
    /* co-registered shader channels, same convention as TreeRig2:
         mask    R = key light (N.L at this aspect's key) - G = sky occlusion
                 B = depth (the height field) - A = coverage
         normal  TANGENT space, R = s, G = t-up, B = out of the face, A = cavity AO
         unlit   albedo + the non-directional cavity term ONLY. This one obeys the
                 terrain kit's contract exactly and survives any grade or sun.
       For a 24 h cycle the shader wants unlit x its own N.L off the normal map;
       the pre-lit albedo stays as the cheap fixed-sun path. */
    const mask = new Uint8ClampedArray(N * 4);
    const nrm = new Uint8ClampedArray(N * 4);
    const unl = new Uint8ClampedArray(N * 4);
    const relief = 8.4 * A.rel;
    /* the form's own relief: RELIEF_M metres of plan depth across texels of
       1/PXM m, eased off a little so a rib nose does not go black */
    const mrel = RELIEF_M * PXM * 0.45;
    /* a tipped face turns more of itself toward a high sun */
    const inc = clamp(A.inc + SL.tf * (0.95 - A.inc) * 0.50, 0, 1);

    for (let y = 0; y < H; y++) {
      for (let x = 0; x < W; x++) {
        const i = y * W + x;
        const xm = ((x - 1) % W + W) % W, xp = (x + 1) % W;
        const ym = mode === 'wrapT' ? ((y - 1) % H + H) % H : clamp(y - 1, 0, H - 1);
        const yp = mode === 'wrapT' ? (y + 1) % H : clamp(y + 1, 0, H - 1);
        const dhs = (det[y * W + xp] - det[y * W + xm]) * relief
                  + (mf[y * W + xp] - mf[y * W + xm]) * mrel;
        const dht = (det[yp * W + x] - det[ym * W + x]) * relief
                  + (mf[yp * W + x] - mf[ym * W + x]) * mrel;
        /* face-space normal: s right, t down, z out of the wall */
        let nx = -dhs, ny = -dht, nz = 1;
        const il = 1 / Math.sqrt(nx * nx + ny * ny + 1);
        nx *= il; ny *= il; nz *= il;

        const ndl = clamp(nx * Lx + ny * Ly + nz * Lz, 0, 1);
        const cav = clamp((hf[i] - bl[i]) * 4.8 + 0.5, 0, 1);   /* crown 1, hollow 0 */
        /* the same measure at form scale: a rib crown vs a re-entrant */
        const mao = clamp((mf[i] - mb[i]) * 2.2 + 0.55, 0, 1);
        const cavT = cav * (0.52 + mao * 0.48);
        /* sky: a vertical face sees half the dome, more if it tips back */
        const sky = clamp(0.5 + SL.tf * 0.30 + ny * -0.45, 0, 1) * (0.30 + cavT * 0.55);
        /* warm bounce from the platform: only the undersides, and a slope has
           far fewer of them */
        const bnc = clamp(ny * 0.9, 0, 1) * (0.25 + cavT * 0.4) * (0.5 + A.inc * 0.5) * (1 - SL.tf * 0.55);

        /* albedo */
        const e = i * 7;
        let t = tf[i] + ex[e] * 0.42 - ex[e + 1] * 0.10 - df[i] * 0.30;
        let c = lut(rk, t);
        let cr = c[0], cg = c[1], cb = c[2];
        /* pale dry dust desaturates as well as lightens */
        const dl = ex[e] * 0.45;
        const lum = (cr * 0.35 + cg * 0.45 + cb * 0.20);
        cr = lerp(cr, lum * 1.16 + 24, dl); cg = lerp(cg, lum * 1.10 + 22, dl); cb = lerp(cb, lum * 1.02 + 22, dl);
        /* salt-bleached basal beds */
        if (pf[i] > 0) { const b = lut('band', clamp(0.30 + cav * 0.55 + (tf[i] - 0.45) * 0.5, 0, 1)); cr = lerp(cr, b[0], pf[i]); cg = lerp(cg, b[1], pf[i]); cb = lerp(cb, b[2], pf[i]); }
        /* seep stain — darken and cool, never a flat wash */
        const sp = ex[e + 5];
        if (sp > 0) { cr = lerp(cr, cr * 0.42 + 6, sp); cg = lerp(cg, cg * 0.44 + 8, sp); cb = lerp(cb, cb * 0.52 + 14, sp); }
        /* life */
        const lc = ex[e + 3];
        if (lc > 0) { const l = lut('lichen', clamp(0.25 + cav * 0.7, 0, 1)); cr = lerp(cr, l[0], lc * 0.75); cg = lerp(cg, l[1], lc * 0.75); cb = lerp(cb, l[2], lc * 0.75); }
        const xa = ex[e + 4];
        if (xa > 0) { const l = lut('xanth', clamp(0.3 + cav * 0.6, 0, 1)); cr = lerp(cr, l[0], xa * 0.8); cg = lerp(cg, l[1], xa * 0.8); cb = lerp(cb, l[2], xa * 0.8); }
        const vg = ex[e + 2];
        if (vg > 0) { const l = lut(rk === 'basalt' ? 'moss' : 'turf', clamp(0.2 + cav * 0.65, 0, 1)); cr = lerp(cr, l[0], vg * 0.85); cg = lerp(cg, l[1], vg * 0.85); cb = lerp(cb, l[2], vg * 0.85); }
        /* colluvium: only a sloped face holds it */
        const gt = ex[e + 6];
        if (gt > 0) { const g = lut('grit', clamp(0.34 + cav * 0.42 + (tf[i] - 0.48) * 0.35, 0, 1)); cr = lerp(cr, g[0], gt * 0.82); cg = lerp(cg, g[1], gt * 0.82); cb = lerp(cb, g[2], gt * 0.82); }

        /* ---- light ---- */
        const key = ndl * inc * (1 - shd[i] * 0.78);
        const amb = 0.30 + cavT * 0.22;
        const skyK = (0.24 + A.cool * 0.40 * COOLSCALE[rk]);
        let lr = amb + key * SUN[0] * 0.95 + sky * SKY[0] * skyK + bnc * BOU[0] * 0.30;
        let lg = amb + key * SUN[1] * 0.95 + sky * SKY[1] * skyK + bnc * BOU[1] * 0.30;
        let lb = amb + key * SUN[2] * 0.95 + sky * SKY[2] * skyK + bnc * BOU[2] * 0.30;
        /* cavity is non-directional and multiplies everything */
        const cm = 0.46 + cavT * 0.80;
        lr *= cm; lg *= cm; lb *= cm;

        let R8 = cr * lr, G8 = cg * lg, B8 = cb * lb;
        /* warm the lit aspects, cool the shaded ones — the temperature split is
           as much of the direction read as the value split */
        const w = A.warm, cs = COOLSCALE[rk];
        R8 *= lerp(lerp(1, 0.84, cs), 1.10, w); G8 *= lerp(lerp(1, 0.89, cs), 1.01, w); B8 *= lerp(lerp(1, 1.30, cs), 0.86, w);
        /* grade headroom */
        R8 = clamp(R8, 16, 244); G8 = clamp(G8, 14, 242); B8 = clamp(B8, 14, 240);

        const o = i * 4;
        const av = alpha ? alpha(i, x, y, W, H, hf, cav) : 255;
        out[o] = R8; out[o + 1] = G8; out[o + 2] = B8; out[o + 3] = av;

        mask[o] = ndl * (1 - shd[i] * 0.78) * 255; mask[o + 1] = sky * 255;
        mask[o + 2] = clamp(hf[i], 0, 1) * 255; mask[o + 3] = av;
        nrm[o] = (nx * 0.5 + 0.5) * 255; nrm[o + 1] = (-ny * 0.5 + 0.5) * 255;
        nrm[o + 2] = (nz * 0.5 + 0.5) * 255; nrm[o + 3] = cavT * 255;
        /* the unlit channel keeps ONLY the cavity/crown terms, fine and macro,
           which are view- and light-independent and therefore grade-safe */
        const um = 0.72 + cavT * 0.40;
        unl[o] = clamp(cr * um, 16, 244); unl[o + 1] = clamp(cg * um, 14, 242);
        unl[o + 2] = clamp(cb * um, 14, 240); unl[o + 3] = av;
      }
    }
    return { data: out, mask, normal: nrm, unlit: unl, W, H, slope: SL.deg };
  }

  /* ================================================================== FACE */
  function face(rock, aspect, step, o) {
    o = o || {};
    const W = o.W || 384, H = o.H || 288;
    const A = ASPECT[aspect];
    const st = step / 2;
    return shade(rock, W, H, A, st, o.seed || (ROCKS.indexOf(rock) * 1013 + 7703), null, 'wrapT',
                 { slope: o.slope, form: o.form });
  }

  /* =============================================================== PROFILE
     The macro form as PLAN DISPLACEMENT IN METRES, so the wall does not have to
     be a quad. Subdivide the wall along s (0.25 m is plenty), push each vertex
     out along the segment normal by disp[], and the silhouette, the cast shadow
     in the bake and the normal map are all the same shape. Without this the
     depth stops at the texture. */
  function profile(rock, aspect, o) {
    o = o || {};
    const W = o.W || 384, H = o.H || 288;
    setSlope(o.slope);
    const R = o.seed || (ROCKS.indexOf(rock) * 1013 + 7703);
    const disp = new Float32Array(W * H);
    for (let y = 0; y < H; y++) {
      const v = (y + 0.5) / H;
      for (let x = 0; x < W; x++) disp[y * W + x] = form((x + 0.5) / W, v, R) * RELIEF_M;
    }
    return { disp, W, H, metres: RELIEF_M, slope: SL.deg, pxm: PXM };
  }

  /* ================================================================== BROW
     RGBA decal. The lip where the plan-view turf rolls over and HANGS.
     s runs along the cliff and tiles; t runs down and does not — t=0 is the
     last of the plan surface, t=1 is clean face. Alpha falls to zero at both
     ends so one strip serves turf-onto-sandstone, turf-onto-till and
     turf-onto-basalt without a variant. The ladder is how badly it is failing:
     Lo a stable vegetated brow, base a clean sod lip with tension cracks
     behind, Hi a collapsing one — gashes, mats hanging, clods on the face.

     The undercut shade band under the lip is modulated hard along s. A band of
     even width is a painted stripe. */
  function brow(aspect, step, o) {
    o = o || {};
    const W = o.W || 384, H = o.H || 128;
    const A = ASPECT[aspect], st = step / 2, R = o.seed || 4421;
    const out = new Uint8ClampedArray(W * H * 4);
    const anchor = 0.42;

    for (let y = 0; y < H; y++) {
      const v = (y + 0.5) / H;
      for (let x = 0; x < W; x++) {
        const u = (x + 0.5) / W;
        /* the lip line itself — width and BOTH boundaries modulated along s */
        const wob = (fb(u, 0.5, 4, 1, R + 1, 3) - 0.5) * 0.16 + (fb(u, 0.5, 13, 1, R + 2, 2) - 0.5) * 0.055;
        const lip = anchor + wob;
        /* overhang: the turf mat projects past the rock. Lee faces hang further
           because the sward is ranker; windward is wind-pruned and short. */
        const over = (0.05 + A.damp * 0.055 + st * 0.05) * (0.6 + fb(u, 0.5, 7, 1, R + 3, 2) * 0.8);

        /* failure: gashes where the mat has gone */
        const gash = ss(0.62 - st * 0.30, 0.80 - st * 0.30, fb(u, 0.5, 6, 1, R + 4, 3));
        const gashD = gash * (0.05 + st * 0.10);

        const turfTop = 0;
        const turfBot = lip + over - gashD;
        const shadeBot = turfBot + 0.10 + A.damp * 0.03 + fb(u, 0.5, 9, 1, R + 5, 2) * 0.06;

        let a = 0, cr = 0, cg = 0, cb = 0;

        if (v < turfBot) {
          /* --- the sod mat --- */
          const d = (v - turfTop) / Math.max(turfBot - turfTop, 1e-4);
          const blade = fb(u, v, 60, 26, R + 10, 2);
          const clump = fb(u, v, 9, 5, R + 11, 3);
          /* soil profile shows in the last third — roots and red till */
          const soilM = ss(0.55, 0.80, d);
          let t = 0.32 + clump * 0.42 + (blade - 0.5) * 0.30;
          let c = lut('turf', clamp(t, 0, 1));
          cr = c[0]; cg = c[1]; cb = c[2];
          if (soilM > 0) {
            const rt = ss(0.35, 0.75, rid(u, v, 40, 6, R + 12, 2));
            const s2 = lut('soil', clamp(0.25 + rt * 0.55 + (blade - 0.5) * 0.4, 0, 1));
            cr = lerp(cr, s2[0], soilM); cg = lerp(cg, s2[1], soilM); cb = lerp(cb, s2[2], soilM);
          }
          /* wind pruning: the windward brow is browner and shorter */
          if (A.wind > 0.5) { const dry = (A.wind - 0.5) * 0.5; cr = lerp(cr, cr * 1.18 + 18, dry); cg = lerp(cg, cg * 1.05 + 10, dry); cb = lerp(cb, cb * 0.86, dry); }
          /* tension cracks behind the lip */
          const crk = ss(0.72 - st * 0.2, 0.90 - st * 0.2, rid(u, v, 5, 2, R + 13, 2)) * ss(0.55, 0.2, d) * (0.3 + st * 0.7);
          cr *= 1 - crk * 0.6; cg *= 1 - crk * 0.6; cb *= 1 - crk * 0.55;
          /* light: the top of the mat is plan view and catches the sky */
          const li = 0.62 + (1 - d) * 0.42 * (0.55 + A.inc * 0.45) - ss(0.7, 1, d) * 0.22;
          cr *= li; cg *= li; cb *= li;
          cb *= lerp(1.16, 0.90, A.warm); cr *= lerp(0.92, 1.05, A.warm);
          a = 255;
          /* ragged bottom edge */
          if (v > turfBot - 0.045) a = 255 * (1 - ss(turfBot - 0.045, turfBot, v)) * 255 / 255;
          if (gash > 0.5 && v > lip - 0.02) a *= 1 - ss(0.5, 0.72, gash);
        } else if (v < shadeBot) {
          /* --- the undercut shade the mat throws on the rock --- */
          const d = (v - turfBot) / Math.max(shadeBot - turfBot, 1e-4);
          const k = (1 - d) * (1 - d);
          a = 255 * k * (0.80 - A.inc * 0.22);
          cr = 14; cg = 12; cb = 22 + A.cool * 16;
          /* a few root threads and clods hanging into it */
          const cl = ss(0.80 - st * 0.2, 0.94 - st * 0.2, fb(u, v, 22, 9, R + 20, 2)) * (1 - ss(0.1, 0.7, d));
          if (cl > 0) { const s2 = lut('soil', 0.4); cr = lerp(cr, s2[0], cl); cg = lerp(cg, s2[1], cl); cb = lerp(cb, s2[2], cl); a = Math.max(a, 255 * cl); }
        } else {
          /* --- fade out onto whatever face material is underneath --- */
          const d = (v - shadeBot) / Math.max(1 - shadeBot, 1e-4);
          const k = (1 - ss(0, 0.75, d));
          /* soil wash and grass tongues spilling down the face */
          const tg = ss(0.60, 0.86, rid(u, v, 10, 1, R + 21, 2)) * k * (0.3 + A.damp * 0.7) * (0.4 + st * 0.6);
          const wash = k * 0.30 * (0.4 + st * 0.5);
          if (tg > 0.02) { const s2 = lut('turf', clamp(0.2 + fb(u, v, 30, 14, R + 22, 2) * 0.5, 0, 1)); cr = s2[0] * 0.8; cg = s2[1] * 0.8; cb = s2[2] * 0.85; a = 255 * Math.min(tg, 1); }
          else { const s2 = lut('soil', 0.35); cr = s2[0]; cg = s2[1]; cb = s2[2]; a = 255 * wash * 0.7; }
        }
        const i = (y * W + x) * 4;
        out[i] = clamp(cr, 8, 246); out[i + 1] = clamp(cg, 8, 246); out[i + 2] = clamp(cb, 8, 246); out[i + 3] = clamp(a, 0, 255);
      }
    }
    return { data: out, W, H };
  }

  /* =================================================================== TOE
     RGBA decal. Where the face lands. Three things the face texture cannot
     draw for itself and the plan-view Talus cannot either:

       1. the UNDERCUT NOTCH the sea cuts at high-water — a dark recess with a
          hard lit lip over it;
       2. the SALT-BLEACHED BASAL BEDS, the pale cream band in every low-tide
          photograph, which is the single most recognisable thing about a PEI
          sea cliff and which nothing in the kit currently has;
       3. the debris that lies AGAINST the foot, occluding it.

     feature:'notch' | 'cave' | 'slump'. */
  function toe(aspect, step, o) {
    o = o || {};
    const W = o.W || 384, H = o.H || 128;
    const A = ASPECT[aspect], st = step / 2, R = o.seed || 6607;
    const feat = o.feature || 'notch';
    const out = new Uint8ClampedArray(W * H * 4);

    for (let y = 0; y < H; y++) {
      const v = (y + 0.5) / H;
      for (let x = 0; x < W; x++) {
        const u = (x + 0.5) / W;
        let a = 0, cr = 0, cg = 0, cb = 0;

        /* the bleach band: strongest at the base, ragged top edge, and it eats
           UP the joints because salt travels up them */
        const bTop = 0.30 + (fb(u, 0.5, 5, 1, R + 1, 3) - 0.5) * 0.24
                   + ss(0.5, 0.9, fb(u, 0.5, 14, 1, R + 2, 2)) * 0.14;
        const bleach = ss(bTop, bTop + 0.30, v) * (0.55 + st * 0.30) * (0.6 + A.wind * 0.4);

        /* the notch: a horizontal recess cut at high water. It COMES AND GOES
           along the shore — a notch of even depth running the full width is a
           painted stripe, which is exactly what the first pass drew. */
        const nAmp = ss(0.22, 0.62, fb(u, 0.5, 4, 1, R + 12, 2))
                   * (0.5 + ss(0.4, 0.8, fb(u, 0.5, 11, 1, R + 13, 2)) * 0.5);
        let nC = 0.50 + (fb(u, 0.5, 4, 1, R + 3, 2) - 0.5) * 0.12;
        let nH = (0.09 + st * 0.08) * (0.55 + nAmp * 0.75);
        if (feat === 'cave') { const m = ss(0.72, 0.30, ABS(u - 0.5) * 2); nC -= m * 0.16; nH += m * 0.30; }
        if (feat === 'slump') { nH *= 0.25; }
        const notch = (1 - ss(nH * 0.55, nH, ABS(v - nC))) * nAmp * (feat === 'slump' ? 0.35 : 1);

        /* debris against the foot */
        const dTop = 0.70 - st * 0.10 + (fb(u, 0.5, 6, 1, R + 4, 3) - 0.5) * 0.20;
        const cs = 26, cx = F(u * cs), cy = F(v * cs * 0.5);
        const px = (cx + 0.15 + ih(cx, cy, R + 5) * 0.7) / cs;
        const py = (cy + 0.15 + ih(cx, cy, R + 6) * 0.7) / (cs * 0.5);
        const pr = 0.012 + ih(cx, cy, R + 7) * 0.024;
        const pd = Math.hypot(u - px, (v - py) * 1.4);
        const blk = (v > dTop) && (ih(cx, cy, R + 8) > 0.28 - st * 0.16) ? (1 - ss(pr * 0.80, pr, pd)) : 0;

        if (notch > 0.02) {
          /* dark recess, warm bounce on its floor. No painted lip — the bright
           edge above it is a soft falloff, so it survives being tiled. */
          cr = 12 + A.inc * 10; cg = 10 + A.inc * 7; cb = 20 + A.cool * 18;
          a = Math.max(a, 255 * notch * (0.84 - A.inc * 0.14));
        }
        const lipGlow = ss(nC - nH * 2.2, nC - nH * 0.7, v) * (1 - ss(nC - nH * 0.7, nC - nH * 0.2, v))
                      * nAmp * (0.30 + A.inc * 0.55);
        if (bleach > 0.02 && notch < 0.5) {
          /* real bedding in the bleached zone — a flat cream wash is a
             watermark, not a rock. Bed thickness warps like the face's does. */
          const bw = (pv(0.5, v, 1, 7, R + 20) - 0.5) * 2.2;
          const bIdx = F(v * 26 + bw), bId = ((bIdx % 26) + 26) % 26;
          const bedV = ih(bId, 21, R);
          const grain = fb(u, v, 90, 40, R + 9, 2);
          const jointy = ss(0.72, 0.94, rid(u, v, 12, 1, R + 22, 2));
          const l = lut('bleach', clamp(0.22 + bedV * 0.52 + (grain - 0.5) * 0.42 - jointy * 0.3, 0, 1));
          const li = 0.62 + A.inc * 0.44;
          const br = l[0] * li, bg2 = l[1] * li, bb = l[2] * (li * (0.94 + A.cool * 0.16));
          const w = bleach * (1 - notch);
          if (a > 0) { cr = lerp(cr, br, w); cg = lerp(cg, bg2, w); cb = lerp(cb, bb, w); a = Math.max(a, 255 * w); }
          else { cr = br; cg = bg2; cb = bb; a = 255 * w; }
        }
        if (lipGlow > 0.02 && bleach < 0.5) {
          const l = lut('sandstone', 0.62 + A.inc * 0.20);
          cr = lerp(cr, l[0] * (0.75 + A.inc * 0.5), lipGlow); cg = lerp(cg, l[1] * (0.75 + A.inc * 0.5), lipGlow);
          cb = lerp(cb, l[2] * (0.75 + A.inc * 0.45), lipGlow); a = Math.max(a, 255 * lipGlow * 0.8);
        }
        if (blk > 0.02) {
          const l = lut('sandstone', clamp(0.32 + ih(cx, cy, R + 11) * 0.36, 0, 1));
          const li = 0.48 + A.inc * 0.60 + (v - dTop) * 0.25;
          const top = 1 - ss(0, 0.55, (v - (py - pr)) / Math.max(pr * 2, 1e-4));
          cr = l[0] * li * (0.82 + top * 0.42); cg = l[1] * li * (0.82 + top * 0.40); cb = l[2] * li * (0.86 + top * 0.3);
          a = 255;
        }
        /* contact shade where the debris meets the wall */
        if (v > dTop - 0.05 && v < dTop + 0.06 && blk < 0.3) {
          const k = 1 - ss(0, 0.11, ABS(v - dTop - 0.005));
          const s2 = 255 * k * 0.5;
          if (a < s2) { cr = 12; cg = 10; cb = 20 + A.cool * 14; a = s2; }
        }
        const i = (y * W + x) * 4;
        out[i] = clamp(cr, 6, 246); out[i + 1] = clamp(cg, 6, 246); out[i + 2] = clamp(cb, 6, 246); out[i + 3] = clamp(a, 0, 255);
      }
    }
    return { data: out, W, H };
  }

  /* ================================================================= LEDGE
     32x32 iso band sprites for the SMALL stuff — 0.3-4 m risers the player
     walks past: a wave-cut platform edge, a road cut, a field bank, a step in
     the terrain. Same camera as the boat bake: 3/4 from the south at 40deg,
     32 px = 1 m, so a 32 px band is ~1.3 m of face.

     These are drawn AS PIXEL ART, not sampled from the face textures. A 32 px
     crop of a 32 px/m photoreal wall is mush: at this size the bedding has to
     be placed on the pixel grid deliberately, 3-6 px per bed, quantised.

     Every piece is a FACET MAP. A tile is not one plane — a corner shows two,
     and each is shaded with its OWN aspect, so a ledge run that turns west
     changes value and temperature at the corner and the player reads the turn.
     That is the whole point of the kit at this scale.

       0 empty   1 S face   2 W-facing facet   3 E-facing facet   4 plan top
     Pieces: faceS cornSW cornSE innSW innSE sideW sideE diagSW diagSE
             roundSW roundSE notch
     Bands:  brow (the lip) / mid / toe */
  const PIECES = ['faceS', 'cornSW', 'cornSE', 'innSW', 'innSE', 'sideW', 'sideE', 'diagSW', 'diagSE', 'roundSW', 'roundSE', 'notch'];
  const BANDS = ['brow', 'mid', 'toe'];
  /* which aspect each facet code is lit as */
  const FACET_ASPECT = { 1: 'S', 2: 'W', 3: 'E', 5: 'SW', 6: 'SE' };

  function facet(piece, x, y, R) {
    /* a little per-column jitter so no silhouette is a ruler edge */
    const j = (i, s) => F(ih(i, s, R) * 2.6);
    switch (piece) {
      case 'faceS': return 1;
      case 'sideW': return x < 9 + j(y, 1) ? 2 : 0;
      case 'sideE': return x > 22 - j(y, 2) ? 3 : 0;
      case 'cornSW': return x < 7 + j(y, 3) ? 2 : 1;
      case 'cornSE': return x > 24 - j(y, 4) ? 3 : 1;
      case 'innSW': return x > 25 - j(y, 5) ? 3 : 1;
      case 'innSE': return x < 6 + j(y, 6) ? 2 : 1;
      case 'diagSW': { const e = 26 - y * 0.85 + j(y, 7); return x > e ? 0 : (x > e - 7 ? 2 : 1); }
      case 'diagSE': { const e = 6 + y * 0.85 - j(y, 8); return x < e ? 0 : (x < e + 7 ? 3 : 1); }
      /* a rounded nose in an iso kit is not an ellipse — the plan silhouette is
         the same at every band, so an ellipse in y stacks into a slit. What
         rounds a corner here is an INTERMEDIATE facet: S -> SW -> W, each lit
         by its own aspect, so the value steps down in two goes instead of one. */
      case 'roundSW': return x < 5 + j(y, 9) ? 2 : (x < 13 + j(y, 11) ? 5 : 1);
      case 'roundSE': return x > 26 - j(y, 10) ? 3 : (x > 18 - j(y, 12) ? 6 : 1);
      case 'notch': { const dx = ABS(x - 15.5); if (dx < 5) return 0; return x < 15 ? 3 : 2; }
    }
    return 1;
  }

  /* quantise onto an 8-step ramp so ledges sit beside the ShorelineIso2 sheets */
  function qput(a, i, r, g, b) {
    const q = v => Math.round(clamp(v, 0, 255) / 8) * 8;
    a[i] = q(r); a[i + 1] = q(g); a[i + 2] = q(b); a[i + 3] = 255;
  }

  function ledge(rock, piece, o) {
    o = o || {};
    const band = o.band || 'mid';
    const st = (o.step === undefined ? 1 : o.step) / 2;
    const R = (o.seed || 3301) + ROCKS.indexOf(rock) * 137;
    const gx = o.gx || 0, gy = o.gy || 0;
    const T = 32;
    const out = new Uint8ClampedArray(T * T * 4);
    const baseA = ASPECT[o.aspect || 'S'];
    const cs = COOLSCALE[rock];

    /* the face proper starts below the brow lip and ends above the toe wash */
    const top = band === 'brow' ? 11 : 0;
    const bot = band === 'toe' ? 24 : 32;

    for (let y = 0; y < T; y++) {
      const wy = gy * T + y;
      for (let x = 0; x < T; x++) {
        const fc = facet(piece, x, y, R);
        const i = (y * T + x) * 4;
        if (fc === 0) { out[i + 3] = 0; continue; }
        const wx = gx * T + x;
        /* a receding facet is compressed on screen, so its bedding is drawn at
           roughly a third of the width — same law as the face textures' read
           coefficient, made literal on the pixel grid */
        const A = ASPECT[FACET_ASPECT[fc]];
        const sx = fc === 1 ? wx : Math.round(wx * 0.38);

        /* ---- brow band: turf, then the shade it throws ---- */
        if (band === 'brow' && y < top) {
          if (y < 7 + F(ih(wx, 0, R) * 2)) {
            const g = ih(F(wx / 2), F(wy / 2), R + 3);
            const c = lut('turf', clamp(0.30 + g * 0.46 + (6 - y) * 0.03, 0, 1));
            const li = (1.05 - y * 0.035) * (0.72 + A.inc * 0.34);
            qput(out, i, c[0] * li, c[1] * li * 0.99, c[2] * li * (0.95 + A.cool * 0.25));
          } else {
            const k = (top - y) / 4;
            const c = lut(rock, 0.24);
            qput(out, i, c[0] * (0.34 + k * 0.10), c[1] * (0.30 + k * 0.10), c[2] * (0.46 + k * 0.12));
          }
          continue;
        }

        /* ---- bedding, placed on the pixel grid ----
           Blocks first, because each block's beds sit at their OWN height: a
           run whose bed lines cross every joint uninterrupted is a course of
           masonry, and that is what the first pass drew. */
        const jw = 6 + F(ih(F(sx / 9), 15, R) * 6);
        const blockId = F(sx / jw);
        const onJoint = ((sx % jw) === 0) && ih(blockId, 17, R) > 0.24;
        const blockDrop = F(ih(blockId, 22, R) * 3);

        const bandY = wy + (band === 'toe' ? 64 : band === 'mid' ? 32 : 0) + blockDrop;
        /* bed boundaries every 3-6 px, wobbling along the run */
        const wob = F(ih(F(sx / 5), 11, R) * 2.2) + F(ih(F(sx / 13), 12, R) * 2.6);
        let acc = 0, bid = 0;
        while (acc <= bandY + wob) { acc += 4 + F(ih(bid, 13, R) * 5); bid++; }
        const bTop = acc - (4 + F(ih(bid - 1, 13, R) * 5));
        const inBed = (bandY + wob) - bTop;
        const bedH = acc - bTop;
        const bedV = ih(bid, 14, R);
        const plucked = ih(blockId * 7 + bid, 18, R) > (0.86 - st * 0.26);

        let t = 0.44 + (bedV - 0.5) * 0.42 + (ih(blockId + bid * 3, 19, R) - 0.5) * 0.12;
        /* top pixel of a bed catches the light, bottom pixel is the undercut */
        if (inBed === 0) t += 0.27;
        else if (inBed >= bedH - 1) t -= 0.27;
        if (onJoint) t -= 0.32;
        if (plucked) t -= 0.19;
        /* the occasional pale siltstone band — rare, and it BREAKS along the
           run, or it is a mortar course */
        const isBandRock = ih(bid, 20, R) > 0.95 && ih(F(sx / 7) + bid, 21, R) > 0.42;
        if (isBandRock) t = clamp(t * 0.70 + 0.24, 0, 1);

        /* ---- toe band: bleached base and the contact shade ---- */
        let bleach = 0;
        if (band === 'toe' && y >= bot - 8) bleach = clamp((y - (bot - 8)) / 9, 0, 1) * (0.34 + st * 0.22);
        if (band === 'toe' && y >= 29) bleach = 0;

        let c = lut(rock, clamp(t, 0, 1));
        let cr = c[0], cg = c[1], cb = c[2];
        if (isBandRock) { const b = lut('band', clamp(t, 0, 1)); cr = lerp(cr, b[0], 0.38); cg = lerp(cg, b[1], 0.38); cb = lerp(cb, b[2], 0.38); }
        if (bleach > 0) { const b = lut('bleach', clamp(0.25 + bedV * 0.45, 0, 1)); cr = lerp(cr, b[0], bleach); cg = lerp(cg, b[1], bleach); cb = lerp(cb, b[2], bleach); }
        /* the very bottom rows are in the ground's own contact shade */
        if (band === 'toe' && y >= 29) { const k = (y - 28) / 4; cr *= 1 - k * 0.55; cg *= 1 - k * 0.58; cb *= 1 - k * 0.40; }

        const li = 0.46 + A.inc * 0.62;
        cr *= li * lerp(lerp(1, 0.86, cs), 1.08, A.warm);
        cg *= li * lerp(lerp(1, 0.91, cs), 1.00, A.warm);
        cb *= li * lerp(lerp(1, 1.26, cs), 0.88, A.warm) * (1 + A.cool * 0.10 * cs);
        qput(out, i, cr, cg, cb);
      }
    }

    /* the arris. Where two facets meet, one lit pixel on the near side and one
       dark on the far side — that single column is what tells the player a
       corner has turned, and no amount of shading substitutes for it. */
    for (let y = 0; y < T; y++) for (let x = 0; x < T - 1; x++) {
      const a0 = facet(piece, x, y, R), a1 = facet(piece, x + 1, y, R);
      if (a0 === a1 || a0 === 0 || a1 === 0) continue;
      const near = ASPECT[FACET_ASPECT[a0]].inc >= ASPECT[FACET_ASPECT[a1]].inc ? x : x + 1;
      const far = near === x ? x + 1 : x;
      const iN = (y * T + near) * 4, iF = (y * T + far) * 4;
      out[iN] = clamp(out[iN] * 1.16 + 9, 0, 255); out[iN + 1] = clamp(out[iN + 1] * 1.12 + 7, 0, 255); out[iN + 2] = clamp(out[iN + 2] * 1.08 + 5, 0, 255);
      out[iF] *= 0.60; out[iF + 1] *= 0.58; out[iF + 2] *= 0.72;
    }

    /* near-black keyline on the silhouette so it holds over a busy shader sea */
    const al = i => out[i * 4 + 3] === 0;
    for (let y = 0; y < T; y++) for (let x = 0; x < T; x++) {
      const i = y * T + x;
      if (al(i)) continue;
      const edge = (x > 0 && al(i - 1)) || (x < 31 && al(i + 1)) || (y < 31 && al(i + T));
      if (edge) { const o4 = i * 4; out[o4] *= 0.40; out[o4 + 1] *= 0.38; out[o4 + 2] *= 0.52; }
    }
    return { data: out, w: T, h: T };
  }

  /* =============================================================== CONTACT
     AO overlays you stamp on the ground tile a ledge or a cliff stands next
     to. Nothing in v7 was occluded and every landform floated; v8 added these
     for the shoreline kit and a cliff needs its own set, deeper and longer
     because the thing casting them is 10 m tall. */
  const CPIECES = ['faceS', 'cornSW', 'cornSE', 'sideW', 'sideE'];
  function contact(piece, o) {
    o = o || {};
    const T = 32, R = 8821;
    const out = new Uint8ClampedArray(T * T * 4);
    for (let y = 0; y < T; y++) for (let x = 0; x < T; x++) {
      const u = x / 31, v = y / 31;
      let k = 0;
      const rag = (fb(u, v, 6, 3, R, 2) - 0.5) * 0.22;
      if (piece === 'faceS') k = 1 - ss(0, 0.62 + rag, v);
      else if (piece === 'cornSW') k = 1 - ss(0, 0.66 + rag, Math.hypot(u * 0.9, v));
      else if (piece === 'cornSE') k = 1 - ss(0, 0.66 + rag, Math.hypot((1 - u) * 0.9, v));
      else if (piece === 'sideW') k = 1 - ss(0, 0.48 + rag, u);
      else if (piece === 'sideE') k = 1 - ss(0, 0.48 + rag, 1 - u);
      const i = (y * T + x) * 4;
      out[i] = 10; out[i + 1] = 9; out[i + 2] = 26;
      out[i + 3] = clamp(k * k * 205, 0, 255);
    }
    return { data: out, w: T, h: T };
  }

  return { ROCKS, ASPECTS, ASPECT, SLOPES, PIECES, BANDS, CPIECES, RAMPS, face, profile, brow, toe, ledge, contact, _lut: lut, _fb: fb, _form: form, _setSlope: setSlope };
})();
