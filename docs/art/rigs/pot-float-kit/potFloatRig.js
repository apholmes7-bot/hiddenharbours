/* POT FLOAT RIG. The fishing floats that mark a fisher's gear on the water, lathed on the SAME
   turntable as the navigation buoys (deck-loop-kit/Art/isoSolid.js): 32 px = 1 m, elev 40,
   flat-facet shading from the fixed upper-LEFT key, 4x4 Bayer dither between 5-step ramps,
   depth-edge darkening, no AA, binary alpha, RINGLESS (ADR 0031).

   Authored by the art-pipeline lane as a NEW rig on the shared turntable; it edits no existing
   rig. It replaces the flat, hand-outlined 24x48 buoyRig.js float on the water.

   TRUE SIZE, NOT A TOY. A Nova Scotia pot float is roughly 17 cm across and 40 cm long with a
   stick through it, so at 32 px/m the bullet float is ~6 px wide. It is made good at that size,
   not made big.

   Three shapes, one facing each (the bodies are lathed, so a facing only turns the rope eye):
     bullet  the classic spindle pot float with a wooden stick and a painted tip
     ball    a hard round float with a moulded top ear (crab, whelk, the inshore pots)
     spar    a high-flyer: a collar float on a tall pole with a small flag, marking a trawl end

   THE PIVOT IS THE STILL WATERLINE (model z = 0), exactly as the nav kit: everything above is
   freeboard, everything below is the wet body baked dry; the engine's shader cuts and tints it.

   COLOUR IS DATA. The two paint coats are baked in KEY ramps (BAKED.body, BAKED.band) and the
   engine swaps each key ramp for the owner's ramp at draw time (the ADR 0029 ramp swap in the
   float's own shader), so one sheet serves every owner. The ramp arithmetic below is the nav
   kit's (rampOf over the 'working' wear dim) and is mirrored exactly by the engine's
   PotFloatPaint; a test holds the two together.

   Exposes globalThis.PotFloat = { W, H, cx, cy, SHAPES, META, BAKED, FIXED, paintRamp,
     render(shape, dir, opts), sheet(opts) }.  opts = { body:'#rrggbb', band:'#rrggbb', keyline } */
(function (root) {
  const K = root.IsoSolid;
  const S = 32;

  // One cell for every shape, so the sheet is a plain row and every pivot sits on one row.
  const W = 22, H = 64, cx = 10, cy = 46;
  const SEG = 14;

  const SHAPES = ['bullet', 'ball', 'spar'];

  // ---- colour --------------------------------------------------------------------------------
  // The nav kit's shift/rampOf, verbatim in arithmetic: a coat is dimmed by the 'working' wear
  // (x0.92, 16% toward its own luminance) and then laddered into five steps.
  const hx = (n) => Math.max(0, Math.min(255, Math.round(n))).toString(16).padStart(2, '0');
  function shift(hex, mul, lift, desat) {
    const [r, g, b] = K.hex2rgb(hex);
    const l = 0.299 * r + 0.587 * g + 0.114 * b;
    const m = (c) => c + (l - c) * (desat || 0);
    return '#' + hx(m(r) * mul + lift) + hx(m(g) * mul + lift) + hx(m(b) * mul + lift);
  }
  const rampOf = (hex) => [shift(hex, 0.42, -6, 0), shift(hex, 0.62, -2, 0), hex,
                           shift(hex, 1.14, 10, 0), shift(hex, 1.30, 26, 0)];
  const WORN = { mul: 0.92, desat: 0.16 };
  const paintRamp = (hex) => rampOf(shift(hex, WORN.mul, 0, WORN.desat));

  // The key coats the sheet is baked in: the player's float as it ships (fleet gold over a rust
  // band, the old painted float's two colours). Any owner swaps both at draw time.
  const BAKED = { body: '#e0b13a', band: '#9c4a3c' };

  // Coats that never change hands. Each ramp is distinct from the others and from any paint key
  // (the swap is an exact colour match, so a shared entry would recolour the wrong part).
  const FIXED = {
    STICK: ['#33291d', '#54432f', '#77603f', '#9c825c', '#bba37c'],   // weathered spruce
    ROPE:  ['#4f3b22', '#735b3a', '#9c845d', '#c3b28a', '#dcd0ad'],   // manila, wet below
    GROW:  rampOf('#38492f'),                                          // the nav kit's growth
    DARK:  ['#0d1114', '#161b1f', '#222a2f', '#313b41', '#455158'],   // the nav kit's DARK
    SHADE: rampOf('#1f3a47'),                                          // the float's own shadow on the water
  };

  // The painted coats sit a little UNDER the turntable's mid-tone, so a small lathed body reads its
  // form (shadow side, base, one lit column) instead of resting on its highlight. Same ramps, a
  // lower start on them; the cap and shoulder biases below still lift what faces the key.
  const TONE = -0.55;
  const tone = (o) => Object.assign({}, o || {}, { b: ((o && o.b) || 0) + TONE,
    capB: (o && o.capB != null) ? o.capB + TONE : undefined });

  // ---- geometry (metres, z up from the still waterline) ------------------------------------------
  const push = (F, a) => { for (const f of a) F.push(f); };
  const tube = (z0, z1, r0, r1, mat, o) => K.tube(0, 0, z0, z1, r0, r1, (o && o.seg) || SEG, mat, o || {});

  // A sphere as stacked frusta from the bottom pole to the top pole: mat(i) picks each band's coat.
  function sphere(F, zc, R, n, matOf, b) {
    for (let i = 0; i < n; i++) {
      const a0 = Math.PI * (1 - i / n), a1 = Math.PI * (1 - (i + 1) / n);
      const z0 = zc + R * Math.cos(a0), z1 = zc + R * Math.cos(a1);
      const r0 = Math.max(0.004, R * Math.sin(a0)), r1 = Math.max(0.004, R * Math.sin(a1));
      push(F, tube(z0, z1, r0, r1, matOf(i, n), { b: b || 0 }));
    }
  }

  // The pot warp's eye: a small closed loop of rope, tied on below the body.
  function eye(F, z, r) {
    const n = 6, ring = [];
    for (let i = 0; i <= n; i++) {
      const a = (i / n) * Math.PI * 2;
      ring.push([Math.cos(a) * r, 0.01, z - r + Math.sin(a) * r]);
    }
    for (let i = 0; i < n; i++) push(F, K.bar(ring[i], ring[i + 1], 0.011, 'ROPE', 0.2, 0.01));
  }

  // Marine growth and the wet line, the nav kit's band: a hair proud of the hull at the waterline.
  const growth = (F, r) => push(F, tube(-0.035, 0.008, r * 1.03, r * 1.03, 'GROW', { b: -0.35, db: -0.02 }));

  // The float's shadow on the water, the small cousin of the nav marks' dark wet mass: a thin dark
  // disc at the surface, a little wider than the body and set toward the camera, so it reads below and
  // around the float. Kept tight (a wet ring, not a skirt). It sits at the float's draft (META.draft),
  // the lowest the engine ever lets a trough take the waterline, plus half the curved line's near-side
  // drop (ground squash sin 40 x waterRadius): so the deepest line, centre column included, still lands
  // on this wet ring, never on the bare stick or pole under the body (which drew a stray pale tick there).
  const SHADOW_SPREAD = 1.2, SHADOW_TOWARD = 0.3, SHADOW_ARC = 0.5 * Math.sin(40 * Math.PI / 180);
  const shadow = (F, r, m) => {
    const depth = m.draft + SHADOW_ARC * m.waterRadius;
    push(F, K.tube(0, -r * SHADOW_TOWARD, -depth - 0.004, -depth, r * SHADOW_SPREAD,
                   r * SHADOW_SPREAD, SEG, 'SHADE', { caps: 'top', b: -0.2, capB: -0.2 }));
  };

  const META = {
    // waterRadius = the body's radius where it meets still water (the engine's curved waterline);
    // height/width are the painted extents in metres, for the scale line-up; draft/freeboard are the
    // most a wave may move the engine's waterline below/above still water: down toward the foot but never
    // to the rope eye, up to the shoulder band but never over the crown (a crest washes, never swamps).
    bullet: { name: 'Spindle pot float with stick', waterRadius: 0.084, bodyLength: 0.40, bodyDiameter: 0.17, draft: 0.08, freeboard: 0.17 },
    ball:   { name: 'Round hard float',             waterRadius: 0.1149, bodyLength: 0.28, bodyDiameter: 0.28, draft: 0.05, freeboard: 0.12 },
    spar:   { name: 'High-flyer spar marker',       waterRadius: 0.104, bodyLength: 0.44, bodyDiameter: 0.21, draft: 0.09, freeboard: 0.20 },
  };

  // Foam floats ride HIGH: about a quarter of the body is under still water.
  function bullet() {
    const F = [];
    // the stick: through the float, a painted tip, the eye at its foot
    push(F, tube(-0.22, 0.62, 0.017, 0.016, 'STICK', { seg: 8, b: 0.2 }));
    push(F, tube(0.62, 0.70, 0.017, 0.015, 'BAND', tone({ seg: 8, caps: 'top', b: 0.35, capB: 0.6 })));
    eye(F, -0.22, 0.035);
    // the spindle body: tapered foot, straight barrel in the body coat, a band on the shoulder
    push(F, tube(-0.10, -0.06, 0.030, 0.070, 'BODY', tone({ caps: 'bottom', b: -0.1 })));
    push(F, tube(-0.06, 0.00, 0.070, 0.084, 'BODY', tone()));
    push(F, tube(0.00, 0.17, 0.084, 0.085, 'BODY', tone()));
    push(F, tube(0.17, 0.215, 0.085, 0.079, 'BAND', tone({ b: 0.1 })));
    push(F, tube(0.215, 0.265, 0.079, 0.055, 'BODY', tone({ b: 0.2 })));
    push(F, tube(0.265, 0.30, 0.055, 0.020, 'BODY', tone({ caps: 'top', b: 0.3, capB: 0.6 })));
    growth(F, 0.084);
    shadow(F, 0.084, META.bullet);
    return F;
  }

  function ball() {
    const F = [];
    const R = 0.14, zc = 0.08;
    // one band of the band coat just under the crown: a two-tone hard float
    sphere(F, zc, R, 10, (i, n) => (i === n - 3 ? 'BAND' : 'BODY'), TONE);
    // the moulded ear on the crown and the eye the warp ties to, under water
    push(F, tube(zc + R - 0.01, zc + R + 0.03, 0.030, 0.026, 'BODY', tone({ seg: 10, caps: 'top', b: 0.3, capB: 0.6 })));
    push(F, tube(zc - R - 0.02, zc - R + 0.01, 0.024, 0.028, 'DARK', { seg: 10, caps: 'bottom', b: 0.2 }));
    eye(F, zc - R - 0.02, 0.035);
    growth(F, Math.sqrt(R * R - zc * zc));
    shadow(F, Math.sqrt(R * R - zc * zc), META.ball);
    return F;
  }

  function spar() {
    const F = [];
    // the pole, a counterweight at its foot, and the flag at its head
    push(F, tube(-0.50, 1.52, 0.020, 0.016, 'STICK', { seg: 8, b: 0.2 }));
    push(F, tube(-0.54, -0.44, 0.034, 0.034, 'DARK', { seg: 10, caps: 'both', b: 0.1 }));
    push(F, tube(1.52, 1.56, 0.016, 0.010, 'BODY', tone({ seg: 8, caps: 'top', b: 0.4, capB: 0.6 })));
    // the flag: a thin panel off the pole, body coat with a band stripe
    push(F, K.box([0.15, 0, 1.43], [0.13, 0.006, 0.045], 'BODY', 0.35 + TONE, 0.01));
    push(F, K.box([0.15, 0, 1.36], [0.13, 0.006, 0.025], 'BAND', 0.35 + TONE, 0.01));
    // the collar float on the pole: spindle, band on the shoulder
    push(F, tube(-0.11, -0.06, 0.035, 0.085, 'BODY', tone({ caps: 'bottom', b: -0.1 })));
    push(F, tube(-0.06, 0.00, 0.085, 0.104, 'BODY', tone()));
    push(F, tube(0.00, 0.20, 0.104, 0.105, 'BODY', tone()));
    push(F, tube(0.20, 0.25, 0.105, 0.097, 'BAND', tone({ b: 0.1 })));
    push(F, tube(0.25, 0.30, 0.097, 0.060, 'BODY', tone({ b: 0.2 })));
    push(F, tube(0.30, 0.33, 0.060, 0.022, 'BODY', tone({ caps: 'top', b: 0.3, capB: 0.6 })));
    eye(F, -0.54, 0.035);
    growth(F, 0.104);
    shadow(F, 0.104, META.spar);
    return F;
  }
  const BUILD = { bullet, ball, spar };
  const _faces = {};

  function render(shape, dir, opts) {
    opts = opts || {};
    const id = BUILD[shape] ? shape : 'bullet';
    const F = _faces[id] || (_faces[id] = BUILD[id]());
    const MATS = {
      BODY: { ramp: paintRamp(opts.body || BAKED.body) },
      BAND: { ramp: paintRamp(opts.band || BAKED.band) },
      STICK: { ramp: FIXED.STICK }, ROPE: { ramp: FIXED.ROPE },
      GROW: { ramp: FIXED.GROW }, DARK: { ramp: FIXED.DARK }, SHADE: { ramp: FIXED.SHADE },
    };
    const keyline = opts.keyline != null ? opts.keyline : (opts.outline != null ? opts.outline : undefined);
    return K.paint(F, { W, H, cx, cy, dir: dir || 0, elev: opts.elev, keyline, MATS });
  }

  // The sheet: one cell per shape, left to right in SHAPES order, facing 0. RGBA, (W*3) x H.
  function sheet(opts) {
    const SW = W * SHAPES.length, out = new Uint8ClampedArray(SW * H * 4);
    SHAPES.forEach((s, k) => {
      const c = render(s, 0, opts);
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        const i = (y * W + x) * 4, j = (y * SW + k * W + x) * 4;
        out[j] = c[i]; out[j + 1] = c[i + 1]; out[j + 2] = c[i + 2]; out[j + 3] = c[i + 3];
      }
    });
    return out;
  }

  root.PotFloat = { W, H, cx, cy, PX_PER_M: S, SHAPES, META, BAKED, FIXED, WORN, paintRamp, render, sheet };
})(typeof globalThis !== 'undefined' ? globalThis : window);
