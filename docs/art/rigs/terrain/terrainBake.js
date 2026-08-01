/* terrainBake.js — seamless tileable terrain albedo baker for Hidden Harbours. v2.
   Target: Unity 6.5, 2D URP, 32 px/m.
     ripple, shingle             512x512 = 16 x 16 m
     grass, marram, sand, shelf  256x256 = 8 x 8 m

   v2 adds an INTENSITY LADDER. Every material bakes at three steps:
     st 0  _Lo   the material barely expressed — thin, worn, relict, intact
     st 1  ---   the working, typical state (keeps the original filename)
     st 2  _Hi   fully expressed — rank, coarse, storm-built, stripped
   The painter paints one 0..1 intensity channel per material; the shader picks the
   bracketing pair and lerps. Steps share seeds, palettes and low-frequency layout,
   so a lerp between neighbours never cross-fades two unrelated images.

   HARD RULES (engine owns these, so they are NOT baked):
     - no directional key light, no cast shadow, no AO from a sun
     - no water, no pools, no wet/damp bands, no specular sheen
     - alpha is always 255

   What IS baked: intrinsic material colour, grain, and a small NON-DIRECTIONAL
   cavity/crown term (crevices darker, crowns lighter) which is view- and
   light-independent and survives a day/night colour grade.

   Every field is exactly periodic, so all output tiles seamlessly. A final
   low-frequency mean-flatten pass evens out the tile's average value so the
   engine's hashed per-chunk UV offsets never show as repeating blotches.
*/
globalThis.TB = (function () {
  const F = Math.floor, C = Math.cos, SN = Math.sin, SQ = Math.sqrt, PI = Math.PI;

  function hash(ix, iy, s) {
    let h = (Math.imul(ix | 0, 374761393) + Math.imul(iy | 0, 668265263) + Math.imul(s | 0, 1442695041)) | 0;
    h = h ^ (h >>> 13); h = Math.imul(h, 1274126177) | 0; h = h ^ (h >>> 16);
    return (h >>> 0) / 4294967296;
  }
  const sm = t => t * t * (3 - 2 * t);
  const cl = (x, a, b) => x < a ? a : x > b ? b : x;
  const mix = (a, b, k) => a + (b - a) * k;
  function ss(a, b, x) { if (a === b) return x < a ? 0 : 1; const t = cl((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }
  const fr = x => x - F(x);
  /* pick from a 3-entry step table */
  const pk = (arr, st) => arr[st];

  function vnA(s, t, cx, cy, sd) {
    const x = s * cx, y = t * cy;
    const xi = F(x), yi = F(y), xf = x - xi, yf = y - yi;
    const x0 = ((xi % cx) + cx) % cx, y0 = ((yi % cy) + cy) % cy;
    const x1 = (x0 + 1) % cx, y1 = (y0 + 1) % cy;
    const u = sm(xf), v = sm(yf);
    const a = hash(x0, y0, sd), b = hash(x1, y0, sd), c = hash(x0, y1, sd), d = hash(x1, y1, sd);
    const i1 = a + (b - a) * u, i2 = c + (d - c) * u;
    return i1 + (i2 - i1) * v;
  }
  function fbmA(s, t, cx, cy, sd, oct, gain) {
    oct = oct || 4; gain = gain === undefined ? 0.5 : gain;
    let sum = 0, amp = 1, nrm = 0, f = 1;
    for (let i = 0; i < oct; i++) { sum += amp * vnA(s, t, cx * f, cy * f, sd + i * 7919); nrm += amp; amp *= gain; f *= 2; }
    return sum / nrm;
  }
  const fbm = (s, t, c, sd, oct, gain) => fbmA(s, t, c, c, sd, oct, gain);
  function rfbm(s, t, c, sd, oct) {
    oct = oct || 4; let sum = 0, amp = 1, nrm = 0, f = 1;
    for (let i = 0; i < oct; i++) { const n = Math.abs(vnA(s, t, c * f, c * f, sd + i * 6151) * 2 - 1); sum += amp * (1 - n); nrm += amp; amp *= 0.5; f *= 2; }
    return sum / nrm;
  }

  const W = { d1: 0, d2: 0, id: 0 };
  /* anisotropic Worley: cx != cy stretches the features. Inputs may be integer
     linear combinations of s,t (e.g. s+2t) — still exactly periodic. */
  function worleyA(s, t, cx, cy, sd, jit) {
    jit = jit === undefined ? 1 : jit;
    const x = s * cx, y = t * cy, xi = F(x), yi = F(y);
    let d1 = 9e9, d2 = 9e9, id = 0;
    for (let oy = -1; oy <= 1; oy++) for (let ox = -1; ox <= 1; ox++) {
      const gx = xi + ox, gy = yi + oy;
      const wx = ((gx % cx) + cx) % cx, wy = ((gy % cy) + cy) % cy;
      const jx = gx + 0.5 + (hash(wx, wy, sd) - 0.5) * jit;
      const jy = gy + 0.5 + (hash(wx, wy, sd + 9173) - 0.5) * jit;
      const dx = jx - x, dy = jy - y, d = SQ(dx * dx + dy * dy);
      if (d < d1) { d2 = d1; d1 = d; id = hash(wx, wy, sd + 551); }
      else if (d < d2) d2 = d;
    }
    W.d1 = d1; W.d2 = d2; W.id = id; return W;
  }
  const worley = (s, t, cells, sd, jit) => worleyA(s, t, cells, cells, sd, jit);

  const hx = h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const mkRamp = l => l.map(([p, h]) => [p, hx(h)]);
  const RC = [0, 0, 0];
  function ramp(st, t) {
    t = cl(t, 0, 1);
    for (let i = 1; i < st.length; i++) {
      if (t <= st[i][0]) {
        const a = st[i - 1], b = st[i], k = (t - a[0]) / Math.max(1e-6, b[0] - a[0]);
        RC[0] = a[1][0] + (b[1][0] - a[1][0]) * k; RC[1] = a[1][1] + (b[1][1] - a[1][1]) * k; RC[2] = a[1][2] + (b[1][2] - a[1][2]) * k;
        return RC;
      }
    }
    const l = st[st.length - 1][1]; RC[0] = l[0]; RC[1] = l[1]; RC[2] = l[2]; return RC;
  }

  /* ------------------------------------------------------------------ palettes */
  const P = {
    ripple: mkRamp([[0, '#2b1f1a'], [.16, '#432e24'], [.34, '#5c402e'], [.52, '#77543b'], [.70, '#8d6a4c'], [.86, '#a3825f'], [1, '#b79878']]),
    rippleSilt: mkRamp([[0, '#2a2421'], [.16, '#42322c'], [.34, '#5a453a'], [.52, '#745a4a'], [.70, '#8d7460'], [.86, '#a68f78'], [1, '#bfab95']]),
    grass: mkRamp([[0, '#1a2412'], [.16, '#26301a'], [.33, '#334020'], [.50, '#45522f'], [.66, '#526237'], [.82, '#667842'], [.93, '#7c8e52'], [1, '#94a06e']]),
    straw: mkRamp([[0, '#8a7748'], [.5, '#b39c60'], [1, '#d4bf86']]),
    soil: mkRamp([[0, '#403427'], [.5, '#6f5c47'], [1, '#96805f']]),
    /* PEI beach and dune sand: pink-tan, not the grey-cream of a granite coast.
       Shared by Sand, the Marram substrate and the Shingle grit matrix, which is
       correct — they are all the same weathered red bed. */
    sand: mkRamp([[0, '#57402f'], [.15, '#755642'], [.33, '#9a7659'], [.50, '#bd9877'], [.68, '#d2b092'], [.84, '#e4c8ab'], [1, '#f3e0c6']]),
    stoneGrey: mkRamp([[0, '#33353a'], [.25, '#4d4f52'], [.5, '#6b6c6c'], [.75, '#8d8d88'], [1, '#b6b5ac']]),
    stoneWarm: mkRamp([[0, '#4c2c1e'], [.28, '#7b452b'], [.55, '#a3663f'], [.78, '#c28858'], [1, '#deb488']]),
    stoneGreen: mkRamp([[0, '#353f39'], [.5, '#5c6d61'], [1, '#8f9d92']]),
    marram: mkRamp([[0, '#333d2c'], [.3, '#4b5f3c'], [.58, '#6c8558'], [.8, '#8ea16f'], [1, '#b8ac82']]),
    marramDry: mkRamp([[0, '#5e5439'], [.4, '#8b7b55'], [.7, '#a89573'], [1, '#c8b795']]),
    /* three stacked beds of the shelf, oldest/lowest last. Value separates them
       first, hue second, so the step reads even under a heavy night grade. */
    shelfTop: mkRamp([[0, '#403c35'], [.22, '#5a5449'], [.45, '#766d5f'], [.68, '#948a78'], [.86, '#b1a691'], [1, '#c9bea9']]),
    shelfMid: mkRamp([[0, '#38332c'], [.22, '#4e463c'], [.45, '#675c4e'], [.68, '#827564'], [.86, '#9d8f7b'], [1, '#b4a692']]),
    shelfLow: mkRamp([[0, '#302e2b'], [.22, '#433f3a'], [.45, '#59544c'], [.68, '#716b60'], [.86, '#8a8375'], [1, '#a29a8b']]),
    weed: mkRamp([[0, '#1c2614'], [.5, '#2f4522'], [1, '#526630']])
  };
  const SHELF_BED = [P.shelfTop, P.shelfMid, P.shelfLow];

  /* explicit blade strokes: tapered segments rising from hashed roots. Returns
     coverage 0..1 in BF.v, position along the blade in BF.q, per-blade id in BF.id. */
  const BF = { v: 0, q: 0, id: 0 };
  function blades(s, t, cells, sd, density, lenMin, lenVar, width, wind) {
    const x = s * cells, y = t * cells, xi = F(x), yi = F(y);
    let best = 0, bq = 0, bid = 0;
    for (let oy = -1; oy <= 3; oy++) for (let ox = -3; ox <= 3; ox++) {
      const gx = xi + ox, gy = yi + oy;
      const wx = ((gx % cells) + cells) % cells, wy = ((gy % cells) + cells) % cells;
      const h3 = hash(wx, wy, sd + 22); if (h3 > density) continue;
      const h1 = hash(wx, wy, sd), h2 = hash(wx, wy, sd + 11), h4 = hash(wx, wy, sd + 33);
      const rx = gx + h1, ry = gy + h2;
      const lean = (h4 - .5) * 1.1 + wind;
      const L = lenMin + h1 * lenVar;
      const dl = 1 / SQ(lean * lean + 1), ux = lean * dl, uy = -dl;
      const ax = x - rx, ay = y - ry;
      let q = ax * ux + ay * uy; if (q < 0) q = 0; else if (q > L) q = L;
      const ex = ax - q * ux, ey = ay - q * uy;
      const d = SQ(ex * ex + ey * ey);
      const wd = width * (1 - .72 * (q / L));
      const v = 1 - d / Math.max(wd, 1e-4);
      if (v > best) { best = v; bq = q / L; bid = h4; }
    }
    BF.v = cl(best, 0, 1); BF.q = bq; BF.id = bid; return BF;
  }

  const OUT = { h: 0, r: 0, g: 0, b: 0 };
  function set(c) { OUT.r = c[0]; OUT.g = c[1]; OUT.b = c[2]; }
  function tint(tr, tg, tb, k) { OUT.r = mix(OUT.r, tr, k); OUT.g = mix(OUT.g, tg, k); OUT.b = mix(OUT.b, tb, k); }
  function mul(k) { OUT.r *= k; OUT.g *= k; OUT.b *= k; }

  /* ==================================================================== RIPPLE
     Iron-red Fundy mud.
       Lo  relict ripple almost planed off — smooth mud sheets, rills, burrows
       Mid working ripple field
       Hi  storm-built, short wavelength, sharp crests, gravel lag in the troughs
     Wave numbers are integers so the trains tile; three regimes are SELECTED by
     region rather than summed, which is what stops it reading as woven cloth. */
  function ripple(s, t, sd, px, py, st) {
    const w1x = fbm(s, t, 2, sd + 10, 3) - .5, w1y = fbm(s, t, 3, sd + 11, 3) - .5;
    const w2 = fbm(s, t, 8, sd + 12, 3) - .5, w3 = fbm(s, t, 16, sd + 13, 2) - .5;
    const broad = fbm(s, t, 3, sd + 1, 3), broad2 = fbm(s, t, 2, sd + 2, 2);

    /* the three regimes are kept NEARLY parallel: a large strike difference makes
       the blend region read as a chevron weave instead of a bifurcating crest */
    const WA = pk([[4, 30], [5, 46], [6, 61]], st);
    const WB = pk([[1, 28], [0, 43], [1, 57]], st);
    const WC = pk([[7, 26], [9, 41], [11, 54]], st);
    const ex = pk([1.05, 1.25, 1.45], st);

    const warp = 22.0 * w1x + 9.0 * w1y + 2.5 * (fbm(s, t, 4, sd + 14, 2) - .5) + 0.9 * w2 + 0.25 * w3;
    const ridA = Math.pow(.5 + .5 * C(2 * PI * (WA[0] * s + WA[1] * t) + warp), ex);
    const ridB = Math.pow(.5 + .5 * C(2 * PI * (WB[0] * s + WB[1] * t) + warp * .86 + 2.2 * w2), ex * .92);
    const ridC = Math.pow(.5 + .5 * C(2 * PI * (WC[0] * s + WC[1] * t) + warp * .74 + 3.0 * w1y), ex * 1.10);
    const zone = fbm(s, t, 2, sd + 80, 2), zone2 = fbm(s, t, 3, sd + 81, 3);
    let rid = mix(ridA, ridB, ss(.470, .545, zone));
    rid = mix(rid, ridC, ss(.655, .725, zone2));

    /* the ripple field waxes and wanes but never clears the tile entirely — large
       empty sheets leave the mean-flatten nothing to work with and the tile goes
       to a flat slab of colour */
    const gate = pk([.52, .34, .24], st);
    const amp = (.34 + .66 * ss(gate, gate + .34, fbm(s, t, 5, sd + 20, 3))) * pk([.46, .92, 1.06], st);
    const brkF = pk([.14, .22, .32], st);
    const brk = brkF + (1 - brkF) * ss(.30, .66, fbm(s, t, 13, sd + 70, 3));

    let h = rid * amp * brk;
    h = h * .78 + broad * .13 + broad2 * .09;

    /* drainage: main rills plus a finer branching set, both with a low levée */
    const rill = ss(.79, .965, rfbm(s, t, 4, sd + 33, 4));
    const rill2 = ss(.86, .995, rfbm(s, t, 9, sd + 34, 3));
    h -= rill * .22 + rill2 * .11;
    h = cl(h + (fbm(s, t, 64, sd + 90, 2) - .5) * .07, 0, 1);

    /* composition: greyer silt in the slacks, red haematite clay on the banks */
    const comp = fbm(s, t, 6, sd + 40, 3);
    const silt = ss(.36, .70, fbm(s, t, 2, sd + 41, 3) * .60 + fbm(s, t, 7, sd + 42, 3) * .40);
    const ct = cl(h * .70 + (comp - .5) * .26 + .22, 0, 1);
    const cc = ramp(P.ripple, ct), c0 = cc[0], c1 = cc[1], c2 = cc[2];
    const cs = ramp(P.rippleSilt, ct);
    set([mix(c0, cs[0], silt), mix(c1, cs[1], silt), mix(c2, cs[2], silt)]);
    mul(.945 + .11 * hash(px, py, sd + 99));
    tint(96, 82, 74, (rill * .30 + rill2 * .16));

    /* Corophium burrow openings — peppered through the damper slacks. Tiny dark
       pinholes with a faint spoil rim; this is what says "tidal mud" at 1:1. */
    const bz = ss(.40, .72, fbm(s, t, 5, sd + 120, 3)) * pk([1.0, .82, .55], st);
    if (bz > .04) {
      const bw = worley(s, t, 168, sd + 121, 1);
      if (bw.id < .16 * bz) {
        if (bw.d1 < .30) { const k = ss(.30, .10, bw.d1) * .8; tint(30, 19, 15, k); h -= k * .38; }
        else if (bw.d1 < .48) { const k = ss(.48, .32, bw.d1) * .16; tint(206, 176, 148, k); }
      }
    }
    /* dried weed strands collected in the troughs */
    const wr = ss(.88, .97, fbm(s, t, 22, sd + 55, 2)) * ss(.42, .18, h);
    tint(64, 62, 46, wr * .55);
    /* shell hash — sparse, dull, settled low. Flecks, not sparkles. */
    const sg = worley(s, t, 104, sd + 56, 1);
    if (sg.id < .020 && sg.d1 < .24) tint(170, 158, 142, ss(.24, .09, sg.d1) * .42 * ss(.52, .24, h));
    /* Hi only: a gravel lag winnowed into the troughs */
    if (st === 2) {
      const gv = worley(s, t, 76, sd + 57, 1);
      if (gv.id < .10 && gv.d1 < .40) {
        const k = ss(.40, .16, gv.d1) * ss(.46, .20, h);
        const gc = ramp(P.stoneGrey, .26 + fr(gv.id * 31.7) * .34);
        tint(gc[0], gc[1], gc[2], k * .78); h += k * .22;
      }
    }
    OUT.h = h;
    return OUT;
  }

  /* =================================================================== SHINGLE
     Storm-sorted cobble beach.
       Lo  pea gravel and coarse grit, occasional stone
       Mid mixed shingle
       Hi  cobble lag, big flat stones sitting proud
     Four calibres painted fine → coarse, gated by elongated storm-sorting bands,
     with the stones themselves flattened (anisotropic cells) like beach cobbles. */
  function shingle(s, t, sd, px, py, st) {
    const ws = s + (fbm(s, t, 20, sd + 61, 2) - .5) * .010, wt = t + (fbm(s, t, 20, sd + 62, 2) - .5) * .010;
    const band = fbmA(s, t, 2, 6, sd + 3, 2) * .74 + fbmA(s, t, 3, 3, sd + 13, 2) * .26;

    /* grit-and-sand matrix showing between the stones */
    const grit = fbm(s, t, 90, sd + 5, 3);
    const gc = ramp(P.sand, .14 + grit * .26);
    set([gc[0] * .78, gc[1] * .77, gc[2] * .74]);
    let h = grit * .14 + band * .06;
    { const mg = hash(px, py, sd + 111); if (mg > .93) mul(1 + (mg - .93) * 1.6); else if (mg < .07) mul(.88 + mg); }

    /* cells alternate their long axis so the stones do not all lie the same way */
    const CELL = [[204, 176], [92, 108], [40, 34], [17, 20]];
    const RAD = [.52, .54, .56, .62];
    const SEED = [0, 313, 626, 939];
    const G = pk([[.90, .58, .14, .00], [.74, .84, .58, .11], [.42, .58, .90, .66]], st);
    const bandMod = [ss(.64, .38, band), 1, ss(.44, .68, band), ss(.56, .82, band)];
    for (let i = 0; i < 4; i++) {
      const g = G[i] * bandMod[i]; if (g < .02) continue;
      const w = worleyA(ws, wt, CELL[i][0], CELL[i][1], sd + SEED[i], .92);
      if (fr(w.id * 47.3) > .08 + .84 * g) continue;
      const r = RAD[i] * (.70 + .52 * fr(w.id * 11.9));
      const dn = w.d1 / r; if (dn >= 1) continue;
      const dome = SQ(cl(1 - dn * dn, 0, 1));
      const fam = fr(w.id * 7.13), v = fr(w.id * 23.7);
      /* heavier dark minerals concentrate in the coarse bands */
      const vv = cl(.30 + v * .34 - (band - .5) * .14, 0, 1);
      let c, ds;
      if (fam < .10) { c = ramp(P.stoneWarm, vv); ds = .62; }
      else if (fam < .16) { c = ramp(P.stoneGreen, vv); ds = .54; }
      else { c = ramp(P.stoneGrey, vv); ds = .12; }
      /* a working cobble beach is slate first, colour second */
      const y = .299 * c[0] + .587 * c[1] + .114 * c[2];
      const k = mix(.86, 1.06, dome);
      OUT.r = mix(c[0], y, ds) * k; OUT.g = mix(c[1], y, ds) * k; OUT.b = mix(c[2], y, ds) * k;
      /* quartz veining and grain on the larger calibres */
      if (i >= 2) {
        if (fr(w.id * 91.7) < .12) tint(190, 190, 184, ss(.34, .56, fbm(s, t, 150, sd + 77, 2)) * .30);
        mul(.965 + .07 * fbm(s, t, 220, sd + 78, 2));
      }
      h = Math.max(h, dome * .70 + .10 + i * .055);
    }
    mul(.975 + .05 * hash(px, py, sd + 71));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ===================================================================== GRASS
     Mixed coastal sward.
       Lo  grazed / trodden thin turf, soil showing, straw heavy
       Mid sward
       Hi  rank meadow — tall tussocks, deep colour, seed heads */
  function grass(s, t, sd, px, py, st) {
    const cw = worley(s, t, 9, sd + 1, .95);
    const clump = cl(1 - cw.d1 / pk([.58, .70, .80], st), 0, 1);
    const cw2 = worley(s, t, 22, sd + 11, .95);
    const tuft = cl(1 - cw2.d1 / pk([.52, .62, .70], st), 0, 1);
    const base = fbm(s, t, 4, sd + 2, 4);
    const mid = fbm(s, t, 14, sd + 3, 3);
    const bs = s + (fbm(s, t, 5, sd + 4, 2) - .5) * .016;
    const bl = fbmA(bs, t, 118, 44, sd + 5, 2);
    const bl2 = fbmA(bs, t, 200, 76, sd + 6, 1);
    const blade = ss(.44, .72, bl) * .70 + ss(.50, .78, bl2) * .30;

    let h = clump * .34 + tuft * .22 + base * .18 + mid * .12 + blade * .18;
    const gap = ss(.30, .04, clump) * ss(.36, .06, tuft);
    const dryP = fbm(s, t, 7, sd + 12, 3);
    const lift = pk([-.06, 0, .05], st);
    const ct = .26 + lift + base * .30 + mid * .13 + clump * .16 + tuft * .11 + blade * .22 - gap * .17;
    set(ramp(P.grass, cl(ct, 0, 1)));
    { const sc = ramp(P.straw, .18 + blade * .5); tint(sc[0], sc[1], sc[2], ss(.46, .78, dryP) * pk([.52, .34, .22], st)); }

    const straw = ss(pk([.58, .70, .80], st), pk([.76, .88, .96], st), fbm(s, t, 4, sd + 7, 3)) * ss(.34, .70, bl);
    if (straw > .01) { const sc = ramp(P.straw, .26 + blade * .5); tint(sc[0], sc[1], sc[2], straw * .50); }
    const bare = ss(pk([.58, .78, .88], st), pk([.76, .92, .99], st), fbm(s, t, 6, sd + 8, 3)) * (1 - clump * .85);
    if (bare > .01) { const bc = ramp(P.soil, .26 + mid * .46); tint(bc[0], bc[1], bc[2], bare * .78); h -= bare * .14; }
    if (bl > .86 && clump > .40) mul(1.08);

    /* explicit blades — legibility at 1:1 comes from these, not from the noise */
    const gb = blades(s, t, pk([52, 44, 38], st), sd + 61, pk([.40, .58, .74], st),
      pk([.7, .9, 1.25], st), pk([.8, 1.1, 1.5], st), .13, .22);
    if (gb.v > .02) {
      const bc = ramp(P.grass, cl(.36 + gb.q * .32 + fr(gb.id * 7.7) * .18, 0, 1));
      tint(bc[0], bc[1], bc[2], cl(gb.v * pk([.54, .64, .74], st), 0, 1));
      h += gb.v * .13;
    }
    /* Hi only: seed heads catching at the top of the tallest blades */
    if (st === 2) {
      const sh = blades(s, t, 30, sd + 63, .18, 1.5, 1.1, .10, .26);
      if (sh.v > .02 && sh.q > .74) {
        const hc = ramp(P.straw, .42 + fr(sh.id * 13.1) * .38);
        tint(hc[0], hc[1], hc[2], cl(sh.v * ss(.74, .95, sh.q) * .85, 0, 1));
        h += sh.v * .10;
      }
    }
    mul(.975 + .05 * hash(px, py, sd + 91));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ==================================================================== MARRAM
     Dune tussock over pale sand.
       Lo  pioneer sprigs on open sand, sand ripple still visible
       Mid stand
       Hi  closed hummock, heavy dead thatch, sand barely showing */
  function marram(s, t, sd, px, py, st) {
    const dry = fbm(s, t, 4, sd + 41, 4), mott = fbm(s, t, 16, sd + 42, 3);
    const lin = rfbm(s, t, 6, sd + 44, 3) * .5 + fbmA(s, t, 6, 30, sd + 43, 2) * .5;
    const fine = fbm(s, t, 50, sd + 45, 2);
    set(ramp(P.sand, cl(.22 + dry * .30 + mott * .24 + (lin - .5) * .28 + fine * .10, 0, 1)));
    let h = lin * .28 + mott * .26 + dry * .3;
    /* Lo: the open sand between sprigs still carries a wind ripple */
    if (st === 0) {
      const wr = .5 + .5 * C(2 * PI * (4 * s + 23 * t) + (fbm(s, t, 3, sd + 46, 2) - .5) * 9);
      const wm = ss(.42, .70, fbm(s, t, 3, sd + 47, 3));
      mul(.955 + .09 * wr * wm); h += wr * wm * .12;
    }
    { const cg = hash(px, py, sd + 19); if (cg > .988) mul(1.16); else if (cg < .012) mul(.92); }

    const stand = ss(pk([.52, .28, .10], st), pk([.80, .62, .40], st), fbm(s, t, 3, sd + 2, 3));
    const clumpN = fbm(s, t, 8, sd + 12, 3);
    const dens = cl(stand * (.45 + .85 * ss(.34, .70, clumpN)), 0, 1);
    if (dens > .02) {
      /* dead thatch collected at the base of an established stand */
      if (st === 2) {
        const th = ss(.40, .76, fbm(s, t, 20, sd + 15, 3)) * dens;
        if (th > .01) { const tc = ramp(P.marramDry, .22 + mott * .40); tint(tc[0], tc[1], tc[2], th * .55); }
      }
      const b1 = blades(s, t, pk([34, 26, 22], st), sd + 6, dens * pk([1.0, 1.35, 1.7], st),
        pk([.9, 1.2, 1.5], st), pk([1.0, 1.3, 1.5], st), .17, .34);
      if (b1.v > .02) {
        const ddry = fr(b1.id * 3.7);
        const live = ramp(P.marram, cl(.14 + b1.q * .54 + fr(b1.id * 11.3) * .28, 0, 1)).slice();
        const dead = ramp(P.marramDry, cl(.18 + b1.q * .58, 0, 1));
        const k = ss(pk([.42, .50, .58], st), pk([.76, .84, .92], st), ddry);
        tint(mix(live[0], dead[0], k), mix(live[1], dead[1], k), mix(live[2], dead[2], k), cl(b1.v * 1.25, 0, 1));
        h += b1.v * .45;
      }
      const b2 = blades(s, t, 40, sd + 26, dens * pk([.6, .95, 1.25], st), .7, .8, .14, .18);
      if (b2.v > .02) {
        const uc = ramp(P.marram, cl(.08 + b2.q * .40 + fr(b2.id * 5.3) * .2, 0, 1));
        tint(uc[0], uc[1], uc[2], cl(b2.v * .95, 0, 1));
        h += b2.v * .22;
      }
    }
    const gr = hash(px, py, sd + 93);
    if (gr > .9962) mul(1.18);
    mul(.98 + .04 * gr);
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ====================================================================== SAND
     Dry upper-beach sand.
       Lo  hardpack — quiet, fine, almost featureless, for wide flats
       Mid dry sand with aeolian lineation
       Hi  coarse and shelly, with a small wind ripple and shell hash */
  function sand(s, t, sd, px, py, st) {
    const broad = fbm(s, t, 2, sd + 1, 3), dry = fbm(s, t, 6, sd + 11, 4);
    const mott = fbm(s, t, 22, sd + 2, 3), fine = fbm(s, t, 74, sd + 4, 2), fine2 = fbm(s, t, 150, sd + 14, 2);
    const lin = rfbm(s, t, 7, sd + 3, 3) * .58 + fbmA(s, t, 8, 22, sd + 13, 2) * .42;
    /* the shear must be a whole number of TILE periods or the field stops
       being periodic in t and the tile seams */
    const lin2 = fbmA(s + t, t, 15, 30, sd + 23, 2);
    const lm = pk([.42, 1.0, 1.15], st), mm = pk([.44, .68, .78], st);
    let h = lin * .28 * lm + lin2 * .12 * lm + mott * .26 * mm + dry * .24 + broad * .06 + fine * .10;
    set(ramp(P.sand, cl(.05 + broad * .10 + dry * .30 + mott * .28 * mm + (lin - .5) * .34 * lm + (lin2 - .5) * .12 * lm + fine * .18 + (fine2 - .5) * .10, 0, 1)));

    /* Hi only: a genuine small wind ripple, integer wave numbers so it tiles */
    if (st === 2) {
      const rw = (fbm(s, t, 3, sd + 31, 2) - .5) * 7 + (fbm(s, t, 8, sd + 32, 2) - .5) * 2;
      const rp = .5 + .5 * C(2 * PI * (3 * s + 22 * t) + rw);
      const rm = ss(.36, .68, fbm(s, t, 4, sd + 33, 3));
      mul(.94 + .12 * rp * rm); h += rp * rm * .20;
    }
    /* heavy-mineral ribbons — soft, low contrast, only loosely directional */
    const hm = ss(.62, .86, fbmA(s, t, 5, 16, sd + 5, 3) * .7 + fbmA(s, t, 3, 7, sd + 15, 2) * .3);
    if (hm > .01) tint(104, 96, 90, hm * pk([.13, .21, .28], st));

    const sh = worley(s, t, pk([72, 64, 46], st), sd + 6, 1);
    const shg = pk([.016, .040, .105], st);
    if (sh.id < shg && sh.d1 < .34) { const k = ss(.34, .12, sh.d1); tint(206, 196, 178, k * .60); h += k * .16; }
    /* coarse grains and comminuted shell — keeps it legible at 1:1 */
    const cg = hash(px, py, sd + 19), ct = pk([.9955, .988, .972], st);
    if (cg > ct) { const kk = (cg - ct) / (1 - ct); mul(1 + kk * pk([.12, .16, .22], st)); }
    else if (cg < 1 - ct) mul(.90 + cg * 6);

    mul(.968 + .064 * hash(px, py, sd + 9));
    OUT.h = cl(h, 0, 1);
    return OUT;
  }

  /* ===================================================================== SHELF
     Bedrock ledge from above, built as a STACK OF BEDS, not a mosaic of pavers.
     Bedding runs one strike across the whole tile (direction 2,-1) and is folded.
     A spall field strips the top bed off in strike-parallel patches, exposing the
     bed below — and in places the bed below that. Each level gets its own palette,
     its own bedding phase and its own grain, and the riser between levels drops
     the height field so the non-directional cavity pass draws the step edge.
       Lo  intact pavement, a few healed joints
       Mid jointed, the second bed showing through in runs
       Hi  heavily stripped, three levels open, rubble on the benches */
  function shelf(s, t, sd, px, py, st) {
    const iv = st * .5;
    const u = s + 2 * t;                      /* across-bedding, integer combo */
    const fold = (fbm(s, t, 2, sd + 31, 3) - .5) * 3.6 + (fbm(s, t, 5, sd + 32, 3) - .5) * 1.1;
    const bp0 = 7 * u + fold;                 /* ~16 px beds at 256 */

    /* which beds are weak: mod-7 keeps the per-bed hash exactly periodic */
    const bedHash = i => { const m = ((i % 7) + 7) % 7; return hash(m, 17, sd); };
    const bedWeak = i => { const m = ((i % 7) + 7) % 7; return hash(m, 91, sd); };

    /* joints first — the pavement is cut into slabs, and whole slabs get plucked,
       so the exposure boundary is part block-shaped and part irregular. Cells are
       kept near-equant: shearing the lattice makes slats too thin to hold a joint. */
    const js = s + (fbm(s, t, 9, sd + 51, 2) - .5) * .14;
    const jt = t + (fbm(s, t, 9, sd + 52, 2) - .5) * .12;
    const w = worleyA(js, jt, 3, 3, sd + 1, .92);
    const w2 = worleyA(js, jt, 7, 6, sd + 4, .88);
    const blockB = (fr(w.id * 13.77) - .5) * .84 + (fr(w2.id * 29.3) - .5) * .16;

    /* spall / strip field: irregular lobes, slab bias, a touch of bed weakness */
    const bi0 = F(bp0);
    const dE = rfbm(s, t, 3, sd + 60, 3) * .46 + fbm(s, t, 7, sd + 61, 2) * .20
      + blockB * .46 + (bedWeak(bi0) - .5) * .06 + (fbm(s, t, 20, sd + 62, 2) - .5) * .06;
    const th1 = mix(.82, .60, iv), th2 = mix(1.10, .88, iv);
    const lvl = dE > th2 ? 2 : dE > th1 ? 1 : 0;
    const riser = Math.max(ss(.024, 0, Math.abs(dE - th1)), ss(.024, 0, Math.abs(dE - th2)));

    /* each exposed bench shows a different part of the stack */
    const bp = bp0 + lvl * 2.63;
    const bi = F(bp), bf = fr(bp);
    const tone = bedHash(bi);
    /* fresh benches show their bedding; the weathered top surface barely does */
    const toneAmp = lvl > 0 ? .21 : .11;
    /* lamination is only really legible on freshly opened rock, not on the
       long-weathered top surface — otherwise the whole tile reads as wood */
    const lam = .5 + .5 * SN(2 * PI * (21 * u) + fold * 3);
    const lamM = ss(.34, .74, fbm(s, t, 6, sd + 35, 3)) * (lvl > 0 ? 1.0 : .16) * (.72 + .6 * riser);
    const lam2 = .5 + .5 * SN(2 * PI * (34 * u) + fold * 5);

    /* joint width must stay above a texel or the line aliases into dotted stitching;
       fade with opacity, never by shrinking the width to nothing */
    const jw = (.034 + .020 * fbm(s, t, 10, sd + 2, 2)) * pk([.72, 1.0, 1.28], st);
    const jVis = ss(pk([.66, .52, .34], st), pk([.98, .90, .76], st), fbm(s, t, 4, sd + 70, 3) * .74 + fbm(s, t, 11, sd + 71, 2) * .26);
    const joint = ss(jw, jw * .30, w.d2 - w.d1) * jVis;

    const weathBig = fbm(s, t, 3, sd + 25, 3);
    const weath = fbm(s, t, 11, sd + 5, 4), fine = fbm(s, t, 44, sd + 15, 2);
    const slabV = (fr(w.id * 19.3) - .5) * .09;
    /* shallow solution pans and pitting on the open pavement */
    const pan = ss(.70, .93, fbm(s, t, 13, sd + 26, 3)) * (lvl === 0 ? 1 : .4);

    /* value inside the exposed bed */
    const v = cl(.15 + slabV + (tone - .5) * toneAmp + weathBig * .26 + weath * .30 + fine * .13
      + ((lam - .5) * .24 + (lam2 - .5) * .11) * lamM - pan * .13 - lvl * .045, 0, 1);
    const cr = ramp(SHELF_BED[lvl], v).slice();
    /* iron staining follows the bedding; strongest on the freshly opened bench */
    const rust = ss(.62, .90, fbm(s, t, 5, sd + 36, 3) * .80 + lam * .20);
    const cwm = ramp(P.stoneWarm, v);
    const rk = rust * (lvl === 1 ? .16 : lvl === 2 ? .09 : .12);
    set([mix(cr[0], cwm[0], rk), mix(cr[1], cwm[1], rk), mix(cr[2], cwm[2], rk)]);

    /* the step itself: the height drop alone feeds the cavity pass, which shades a
       step ASYMMETRICALLY — dark at the foot, bright on the lip. Painting a
       symmetric dark line into the riser instead is what makes a bench read as a
       crack, and turns the whole shelf into crazy paving. */
    let h = cl(.46 + slabV + ((lam - .5) * .13 + (lam2 - .5) * .06) * lamM + weath * .20 + fine * .07 - pan * .12 - joint * .34, 0, 1);
    h -= lvl * .26;
    tint(42, 40, 37, joint * .48);

    /* spall debris left on the benches, carrying the colour of the bed above */
    if (lvl > 0) {
      const rb = worley(s, t, 110, sd + 8, 1);
      const rbz = ss(.30, .70, fbm(s, t, 8, sd + 63, 3)) * (.35 + riser * .9);
      if (rb.id < .30 * rbz && rb.d1 < .34) {
        const k = ss(.34, .12, rb.d1);
        const rc = ramp(SHELF_BED[lvl - 1], cl(.20 + fr(rb.id * 9.1) * .40, 0, 1));
        tint(rc[0], rc[1], rc[2], k * .80); h += k * .16;
      }
    }
    /* weed only where water sits — in the joints, never on the open pavement */
    const wd = joint * ss(.62, .86, fbm(s, t, 7, sd + 6, 3));
    if (wd > .01) { const wc = ramp(P.weed, .18 + weath * .45); tint(wc[0], wc[1], wc[2], cl(wd * .55, 0, 1)); }
    /* lichen crust only on the long-exposed top surface */
    if (lvl === 0) {
      const li = ss(.62, .86, fbm(s, t, 16, sd + 9, 3)) * ss(.44, .74, v) * pk([1.15, 1.0, .8], st);
      if (li > .01) tint(156, 158, 136, li * .26);
    }
    mul(.978 + .045 * hash(px, py, sd + 95));
    /* hard mineral grain — a rock surface at 1:1 is not smooth */
    { const gw = worley(s, t, 128, sd + 96, 1); mul(.965 + .07 * fr(gw.id * 17.3)); }
    OUT.h = h;
    return OUT;
  }

  const MATS = { ripple, shingle, grass, marram, sand, shelf };
  /* cav = non-directional crevice darkening / crown lightening. r = radius in texels.
     lo/hi = luminance clamp so a day/night grade keeps headroom at both ends.
     flat = how hard the tile's own low-frequency mean is levelled. Shelf is levelled
     least, because its layer-to-layer value separation IS the low-frequency signal. */
  const CFG = {
    ripple: { cav: .22, crown: .08, r: 4, lo: 26, hi: 210, flat: .68, flatR: .34 },
    shingle: { cav: .34, crown: .18, r: 3, lo: 24, hi: 232, flat: .82, flatR: .22 },
    grass: { cav: .26, crown: .12, r: 3, lo: 24, hi: 218, flat: .88, flatR: .26 },
    marram: { cav: .22, crown: .11, r: 3, lo: 26, hi: 230, flat: .86, flatR: .26 },
    sand: { cav: .20, crown: .10, r: 4, lo: 34, hi: 236, flat: .66, flatR: .30 },
    shelf: { cav: .30, crown: .28, r: 4, lo: 24, hi: 228, flat: .50, flatR: .38 }
  };

  const SPEC = {
    ripple: { N: 512, m: 16, seed: 1301 },
    shingle: { N: 512, m: 16, seed: 2207 },
    grass: { N: 256, m: 8, seed: 3313 },
    marram: { N: 256, m: 8, seed: 4409 },
    sand: { N: 256, m: 8, seed: 5501 },
    shelf: { N: 256, m: 8, seed: 6607 }
  };
  const STEPS = ['_Lo', '', '_Hi'];

  function blurWrap(src, N, r) {
    r = Math.min(r, (N >> 1) - 1);
    const tmp = new Float32Array(N * N), dst = new Float32Array(N * N), inv = 1 / (2 * r + 1);
    for (let y = 0; y < N; y++) {
      const row = y * N; let acc = 0;
      for (let i = -r; i <= r; i++) acc += src[row + ((i % N) + N) % N];
      for (let x = 0; x < N; x++) { tmp[row + x] = acc * inv; acc += src[row + (x + r + 1) % N] - src[row + (((x - r) % N) + N) % N]; }
    }
    for (let x = 0; x < N; x++) {
      let acc = 0;
      for (let i = -r; i <= r; i++) acc += tmp[(((i % N) + N) % N) * N + x];
      for (let y = 0; y < N; y++) { dst[y * N + x] = acc * inv; acc += tmp[((y + r + 1) % N) * N + x] - tmp[((((y - r) % N) + N) % N) * N + x]; }
    }
    return dst;
  }

  function bake(name, step, createCanvas) {
    const fn = MATS[name], cfg = CFG[name], sp = SPEC[name];
    const N = sp.N, seed = sp.seed, st = step | 0;
    const H = new Float32Array(N * N), R = new Float32Array(N * N), G = new Float32Array(N * N), B = new Float32Array(N * N);
    for (let y = 0; y < N; y++) {
      const t = (y + .5) / N;
      for (let x = 0; x < N; x++) {
        const o = fn((x + .5) / N, t, seed, x, y, st), i = y * N + x;
        H[i] = o.h; R[i] = o.r; G[i] = o.g; B[i] = o.b;
      }
    }
    /* 1. non-directional cavity / crown from the height field */
    const soft = blurWrap(H, N, cfg.r);
    for (let i = 0; i < N * N; i++) {
      const d = H[i] - soft[i];
      const k = d < 0 ? 1 + d * 2.6 * cfg.cav : 1 + d * 2.2 * cfg.crown;
      R[i] *= k; G[i] *= k; B[i] *= k;
    }
    /* 2. flatten the tile's own low-frequency mean so hashed UV offsets in the
          shader can never reveal a repeating light/dark blotch */
    const lum = new Float32Array(N * N);
    for (let i = 0; i < N * N; i++) lum[i] = .299 * R[i] + .587 * G[i] + .114 * B[i];
    const lowF = blurWrap(lum, N, Math.max(8, (N * (cfg.flatR || .25)) | 0));
    let mean = 0; for (let i = 0; i < N * N; i++) mean += lowF[i]; mean /= N * N;
    for (let i = 0; i < N * N; i++) {
      const corr = cl(mix(1, mean / Math.max(lowF[i], 1e-3), cfg.flat), .72, 1.38);
      R[i] *= corr; G[i] *= corr; B[i] *= corr;
    }
    /* 3. hold luminance inside the grade-safe window, preserving hue */
    const ca = createCanvas(N, N), ctx = ca.getContext('2d'), im = ctx.createImageData(N, N), D = im.data;
    for (let i = 0; i < N * N; i++) {
      let r = R[i], g = G[i], b = B[i];
      const l = .299 * r + .587 * g + .114 * b;
      if (l > 1e-3) {
        const tl = cl(l, cfg.lo, cfg.hi);
        if (tl !== l) { const k = tl / l; r *= k; g *= k; b *= k; }
      }
      const o = i * 4;
      D[o] = cl(r, 0, 255) | 0; D[o + 1] = cl(g, 0, 255) | 0; D[o + 2] = cl(b, 0, 255) | 0; D[o + 3] = 255;
    }
    ctx.putImageData(im, 0, 0);
    return { albedo: ca, H: H, N: N };
  }

  return { bake, MATS, CFG, SPEC, STEPS, hash, fbm, fbmA, vnA, rfbm, worley, worleyA,
           blades, ss, ramp, mkRamp, P, blurWrap, OUT, set, tint, mul, mix, cl, fr, sm, pk };
})();
