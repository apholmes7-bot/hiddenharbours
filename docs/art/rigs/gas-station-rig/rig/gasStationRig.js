/* Hidden Harbours — GAS STATION rig: the forecourt, built on the FuelIso grade table (ADR-0006 bake
   on IsoSolid). Same fixed 3/4 turntable as the fleet / character / fuel kit: 45deg steps, elev 40deg,
   upper-left key, ordered dither, depth-edge darkening, NO AA, 32 px = 1 m, RINGLESS per ADR-0031.

   THE ONE RULE OF THIS KIT — a station is a LIST OF GRADE POSITIONS, not a building. Every hose on
   the forecourt is one entry of (grade · flow · reach), and everything the player can see is generated
   from that list: the valance stripes, the boot cups, the price rows on the sign, the fill caps in the
   apron. A station that sells three grades cannot draw four bands, because the bands ARE the list.
   Pass `grades:['gas','diesel','mixed']` once and the whole forecourt agrees with itself.

   GRADES COME FROM ONE TABLE. FuelIso owns them — gas red · diesel amber · mixed blue · oil green —
   and this rig imports them BY REFERENCE, adding only STOVE OIL (ivory). So the pump band and the
   jerry can standing under it can never disagree about what red means. That is why fuelRig.js is a
   hard dependency and not a soft one.

   SEVEN VARIETIES, and they are different MACHINES rather than sizes of one:
     sKerb     one hose, a post, 38 L/min      — the pump outside a general store
     sVintage  one hose, globe and clock dial   — pre-electronic, still turning over
     sDock     two hoses on a low pedestal      — the fuel float; hose mast, long reach
     sTwin     two hoses, two lanes             — the small road station
     sMpd      up to four hoses, two lanes      — the whole grade list on one plinth
     sTruck    two hoses, 130 L/min, reels      — high-flow diesel for the boats and the rigs
     sCardlock one hose, unattended terminal    — the co-op card pump, no attendant, no canopy
   AMOUNT is `hoses` (1..maxHose, one per grade position) and `sides` (1 = one lane, 2 = both).
   CONFIGURATION is the island: 1..4 slots, any variety in any slot, plus canopy and sign.

   FILLING IS A REACH PROBLEM, NOT A PIXEL PROBLEM. `serve()` returns the hose as a polyline between
   the dispenser's hose mount and the vessel's fill mount, in canvas pixels, with the true ground
   distance in metres and whether that is inside the variety's reach. Slack grows as the pull
   shortens, so a nozzle held close hangs a deep bight and one held at full stretch goes taut.

   PIVOT: ground centre of the footprint for every piece, so a forecourt is just placements in metres.
   Exposes globalThis.StationIso. */
(function (root) {
  const IS = root.IsoSolid;
  if (!IS) throw new Error('StationIso requires IsoSolid — load Art/isoSolid.js first');
  const FU = root.FuelIso;
  if (!FU) throw new Error('StationIso requires FuelIso — load Art/fuelRig.js first (ONE grade table)');
  const { S, DEG, box, bar, tube, camBasis, proj, paint, mulberry } = IS;
  const DEFAULT_ELEV = 40, KEY = '#101a19';
  const rotX = (a) => { const c = Math.cos(a), s = Math.sin(a); return (p) => [p[0], p[1] * c - p[2] * s, p[1] * s + p[2] * c]; };
  const move = IS.move, chain = IS.chain, rotY = IS.rotY;

  // ---- ramps ---------------------------------------------------------------
  const R = FU.ramps;
  const STEEL = R.STEEL, GALV = R.GALV, GRAPH = R.GRAPH, RUST = R.RUST, BRASS = R.BRASS,
        CONC = R.CONC, WHITE = R.WHITE, DARK = R.DARK, GLASS = R.GLASS;
  /* Enamel and concrete get SEVEN steps where every other ramp gets five. IsoSolid normalises ramp
     position by length, so a longer ramp dithers in finer increments — and the two biggest flat faces
     in this kit (a canopy deck, an island top) are exactly where a 5-step ordered dither turns into
     visible checkerboard. Same brightness, quarter the noise. */
  const ENAM  = ['#4c5251', '#5f6564', '#727877', '#868c8a', '#9aa09d', '#b2b8b4', '#cbd1cc'];
  const CONC7 = ['#343734', '#464a46', '#585c57', '#6b6f68', '#7f8379', '#95998d', '#adb1a4'];
  const ROOF  = ['#242a2e', '#2e353a', '#394146', '#454e53', '#525c61', '#606b70', '#6f7b80'];   // canopy deck from above
  const CHROME = ['#3f4a50', '#63727a', '#93a4ac', '#c0ced4', '#e6f0f3'];   // trim, card slots, swivels
  const ASPH   = ['#15191b', '#1e2325', '#272d2f', '#31383a', '#3d4446'];   // apron
  const YEL    = ['#4b3a0d', '#6e5613', '#96791f', '#bb9d35', '#d9bd58'];   // curb paint, bollard bands
  const GLOW   = ['#5c4f1c', '#8d7a2c', '#c0a944', '#e2d17c', '#f7edbe'];   // canopy soffit, sign field, globe
  const SEG    = ['#13191c', '#1f2c33', '#33505f', '#57869e', '#8fc3da'];   // counter digits, price digits

  /* Grade table: FuelIso's four by reference + one addition. Stove oil is the harbour's fifth grade
     and it is deliberately the only ACHROMATIC code in the set — a kerosene pump has to be legible
     next to red, amber, blue and green without stealing a hue any of them owns. */
  const GRADES = Object.assign({}, FU.FUELS, {
    kero: {
      label: 'STOVE OIL', short: 'KERO', dens: 0.800,
      code: ['#575a52', '#787b70', '#9a9d8f', '#bcbfaf', '#dcdfcd'],
      pale: ['#6b6d64', '#8a8c81', '#a8ab9d', '#c4c7b7', '#dee1d1'],
      deep: ['#3b3d36', '#54564c', '#6f7163', '#8b8d7c', '#a7a996'],
      liq:  ['#5c5b4c', '#807e6a', '#a4a189', '#c5c2a8', '#e2dfc6'], hex: '#a8ab9d',
    },
  });
  const GRADE_ORDER = FU.FUEL_ORDER.concat(['kero']);
  /* Cents per litre. The sign, the counter and the ticket all read this one table, so a price change
     is one number and not four sprites. Bulk oil is priced per litre off a reel, not per jug. */
  const PRICE = { gas: 172.9, diesel: 189.4, mixed: 224.9, oil: 612.0, kero: 154.9 };

  function matsFor(grades, lit) {
    const G = (grades && grades.length ? grades : ['gas']).map((k) => GRADES[k] || GRADES.gas);
    const M = {
      steel: { ramp: STEEL }, galv: { ramp: GALV }, graph: { ramp: GRAPH }, rust: { ramp: RUST },
      brass: { ramp: BRASS }, conc: { ramp: CONC7 }, white: { ramp: ENAM }, dark: { ramp: DARK },
      glass: { ramp: GLASS }, chrome: { ramp: CHROME }, asph: { ramp: ASPH }, yel: { ramp: YEL },
      roof: { ramp: ROOF },
      lamp: lit ? { ramp: GLOW, off: 3 } : { ramp: WHITE, off: -1 },
      win: lit ? { ramp: GLOW, off: 1 } : { ramp: GLASS },
      seg: lit ? { ramp: SEG, off: 3 } : { ramp: SEG },
      panel: lit ? { ramp: WHITE, off: 2 } : { ramp: WHITE },
    };
    for (let i = 0; i < 4; i++) {
      const g = G[Math.min(i, G.length - 1)];
      M['g' + i] = { ramp: g.code };
      M['l' + i] = { ramp: g.liq, off: 1 };
    }
    return M;
  }

  // ---- the table -----------------------------------------------------------
  const TYPES = {
    dispenser: {
      label: 'DISPENSER', order: 1,
      gloss: 'Seven machines, not seven sizes. Hose count is the grade list; sides is how many lanes it serves.',
      sizes: {
        sKerb:    { fam: 'post',  w: 0.32, d: 0.32, h: 1.46, maxHose: 1, maxSides: 1, reach: 3.2, lpm: 38,  tare: 110, label: 'KERB POST' },
        sVintage: { fam: 'globe', w: 0.46, d: 0.42, h: 1.66, maxHose: 1, maxSides: 1, reach: 3.6, lpm: 30,  tare: 190, label: 'GLOBE-TOP' },
        sDock:    { fam: 'ped',   w: 0.48, d: 0.36, h: 1.12, maxHose: 2, maxSides: 1, reach: 4.5, lpm: 45,  tare: 120, label: 'DOCK PEDESTAL' },
        sTwin:    { fam: 'slab',  w: 0.64, d: 0.42, h: 1.74, maxHose: 2, maxSides: 2, reach: 4.0, lpm: 40,  tare: 180, label: 'TWIN SLAB' },
        sMpd:     { fam: 'slab',  w: 0.98, d: 0.48, h: 1.94, maxHose: 4, maxSides: 2, reach: 4.6, lpm: 40,  tare: 260, label: 'MULTI-PRODUCT' },
        sTruck:   { fam: 'brick', w: 1.08, d: 0.56, h: 1.60, maxHose: 2, maxSides: 2, reach: 6.2, lpm: 130, tare: 340, label: 'HIGH-FLOW' },
        sLock:    { fam: 'lock',  w: 0.42, d: 0.36, h: 1.70, maxHose: 1, maxSides: 1, reach: 5.0, lpm: 80,  tare: 150, label: 'CARDLOCK' },
      },
    },
    island: {
      label: 'PUMP ISLAND', order: 2,
      gloss: 'Concrete curb on a 3.3 m slot pitch. Slots are mounts, not pictures — put any variety in any one.',
      sizes: {
        s1: { slots: 1, label: '1 SLOT' }, s2: { slots: 2, label: '2 SLOTS' },
        s3: { slots: 3, label: '3 SLOTS' }, s4: { slots: 4, label: '4 SLOTS' },
      },
    },
    canopy: {
      label: 'CANOPY', order: 3,
      gloss: 'Bays follow the island slots and posts land on the curb. Fascia carries the house colour; the lean-to is what a harbour actually builds.',
      sizes: {
        sB2: { bays: 2, label: '2 BAYS' }, sB3: { bays: 3, label: '3 BAYS' }, sB4: { bays: 4, label: '4 BAYS' },
      },
    },
    sign: {
      label: 'PRICE SIGN', order: 4,
      gloss: 'One row per grade in service, in that grade\u2019s colour. The sign is the grade list seen from the road.',
      sizes: {
        sPylon:  { poleH: 4.10, pw: 2.10, ph: 2.45, label: 'PYLON' },
        sTotem:  { poleH: 0.85, pw: 1.55, ph: 1.60, label: 'TOTEM' },
        sAframe: { poleH: 0,    pw: 0.86, ph: 0.98, label: 'A-FRAME' },
      },
    },
    fillport: {
      label: 'FILL POINT', order: 6,
      gloss: 'The supply side of the same list: a grade-coded cap in the apron for every tank under it, and a vent riser per tank.',
      sizes: {
        sCluster: { pad: 1.72, label: 'FILL CLUSTER' },
        sVent:    { h: 3.10, label: 'VENT RISERS' },
      },
    },
    store: {
      label: 'STOREFRONT', order: 5,
      gloss: 'The modern box: dark base, glazed front wall, illuminated band, entry tower proud of the parapet, plant on the roof. The pay booth is the same grammar at a quarter of the size.',
      sizes: {
        sKiosk: { w: 4.20, d: 3.40, h: 3.05, kiosk: true, wall: 0.16, clear: 2.55, label: 'PAY BOOTH' },
        sStore: { w: 11.60, d: 8.20, h: 4.35, kiosk: false, wall: 0.20, clear: 3.05, label: 'C-STORE' },
      },
    },
  };
  const TYPE_ORDER = Object.keys(TYPES).sort((a, b) => TYPES[a].order - TYPES[b].order);
  const sizesOf = (t) => Object.keys(TYPES[t].sizes);
  function resolveSize(t, s) { const k = sizesOf(t); return (s && TYPES[t].sizes[s]) ? s : k[0]; }

  /* One resolved spec drives geometry, the measure cache and the mount table. Nothing downstream
     reads opts directly, so a new caller cannot invent a state the cell was not measured for. */
  function resolve(type, size, o) {
    o = o || {};
    const Z = TYPES[type].sizes[size];
    const gl = (o.grades && o.grades.length) ? o.grades.length : 0;
    const n = type === 'dispenser'
      ? Math.max(1, Math.min(Z.maxHose, o.hoses || gl || 1))
      : Math.max(1, Math.min(4, o.hoses || gl || 3));
    return {
      n,
      sides: type === 'dispenser' ? Math.max(1, Math.min(Z.maxSides, o.sides || Z.maxSides)) : 1,
      out: o.out != null ? o.out : -1,               // which hose is off the hook (boot reads empty)
      style: o.style === 'tin' ? 'tin' : 'fascia',
      open: Math.max(0, Math.min(1, o.open != null ? o.open : 0)),   // sliding entry, 0 = shut (default)
      span: Math.max(1, Math.min(2, o.span || 1)),   // island rows a canopy covers
      bollards: o.bollards !== false,
      bin: !!o.bin,
      wear: o.wear || 'working',
      seed: o.seed || 11,
    };
  }
  const skey = (type, size, s) => [type, size, s.n, s.sides, s.out, s.style, s.span, s.bollards ? 1 : 0, s.bin ? 1 : 0, s.wear, Math.round(s.open * 8)].join('|');

  // ---- shared parts --------------------------------------------------------
  const vdisc = (F, x, y, z, r, mat, b, db) =>
    F.push(...tube(0, 0, 0, 0, r, r, 12, mat, { caps: 'both', xf: chain(rotX(Math.PI / 2), move(x, y, z)), b, db }));

  /* The counter face. At 32 px/m a dispenser is 60 px tall and its money window is 4 px, so digits
     are not a thing that can be drawn here: the window is a dark recess with three bright blocks in
     it, and the actual READ is the row of grade chips under it — one chip per position, in that
     position's colour. Count the chips and you know what the machine sells. */
  function counter(F, sy, hw, hd, z, n) {
    const y = sy * (hd + 0.006);
    F.push(...box([0, y, z], [hw * 0.78, 0.010, 0.082], 'dark', -0.35, -0.03));
    for (let i = 0; i < 3; i++)
      F.push(...box([(-1 + i) * hw * 0.42, y + 0.006, z + 0.014], [hw * 0.14, 0.008, 0.028], 'seg', 0.3, -0.06));
    F.push(...box([0, y + 0.006, z - 0.052], [hw * 0.56, 0.008, 0.014], 'panel', 0.4, -0.06));
    for (let i = 0; i < n; i++) {
      const w = (hw * 1.34) / n, cx = -hw * 0.67 + w * (i + 0.5);
      F.push(...box([cx, y + 0.009, z - 0.112], [w * 0.38, 0.008, 0.024], 'g' + i, 0.45, -0.07));
    }
  }
  /* A nozzle boot in the grade's own colour, with the nozzle holstered in it unless this hose is the
     one that is out. An empty boot is how the sprite says "someone is fuelling right now". */
  function boot(F, sx, x, z, i, out) {
    F.push(...box([x, 0, z], [0.020, 0.058, 0.070], 'g' + i, 0.3, -0.04));
    F.push(...box([x + sx * 0.014, 0, z + 0.012], [0.012, 0.040, 0.046], 'dark', -0.6, -0.07));
    if (out !== i) {
      F.push(...box([x + sx * 0.026, 0, z + 0.020], [0.020, 0.026, 0.038], 'graph', 0.15, -0.09));
      F.push(...bar([x + sx * 0.026, 0, z + 0.052], [x + sx * 0.030, 0.010, z + 0.098], [0.013, 0.013], 'steel', 0.35, -0.10));
      F.push(...box([x + sx * 0.026, 0, z - 0.020], [0.014, 0.014, 0.026], 'g' + i, 0.5, -0.10));
    }
  }
  const bootZ = (H, i, n) => 0.46 + (H * 0.62 - 0.46) * (n === 1 ? 0.5 : i / (n - 1));

  function pad(F, r, h, mat) { F.push(...tube(0, 0, -0.01, h, r, r * 0.96, 14, mat || 'conc', { caps: 'top', b: -0.05, capB: 0.10 })); }

  // ---- dispenser families --------------------------------------------------
  function geoPost(Z, s) {
    const F = [], hw = Z.w / 2, hd = Z.d / 2, H = Z.h, neck = H * 0.60;
    pad(F, hw + 0.13, 0.085);
    F.push(...box([0, 0, (0.085 + neck) / 2], [hw, hd, (neck - 0.085) / 2], 'white', 0, 0, null, 'shell'));
    F.push(...box([0, 0, neck + 0.020], [hw + 0.016, hd + 0.016, 0.030], 'g0', 0.4, -0.03));            // grade collar
    F.push(...box([0, 0, (neck + 0.05 + H - 0.05) / 2], [hw * 0.98, hd * 0.98, (H - 0.10 - neck) / 2], 'white', 0.08, 0, null, 'shell'));
    F.push(...box([0, 0, H - 0.032], [hw + 0.022, hd + 0.022, 0.034], 'g0', 0.5, -0.04));               // cap band
    F.push(...box([0, 0, H + 0.010], [hw * 0.70, hd * 0.70, 0.014], 'lamp', 0.85, -0.06));              // shop light
    for (const sy of [1, -1]) {                                                                          // the dial IS the counter here
      vdisc(F, 0, sy * (hd + 0.010), H * 0.80, hw * 0.84, 'panel', 0.35, -0.04);
      F.push(...box([0, sy * (hd + 0.020), H * 0.80], [hw * 0.10, 0.008, hw * 0.60], 'g0', 0.6, -0.07));
      F.push(...box([0, sy * (hd + 0.014), H * 0.66], [hw * 0.52, 0.008, 0.020], 'seg', 0.35, -0.06));
    }
    boot(F, 1, hw + 0.014, 0.78, 0, s.out);
    F.push(...bar([hw + 0.004, 0, H * 0.86], [hw + 0.15, 0, H * 0.90], [0.020, 0.020], 'steel', 0.3, -0.04));
    return { F, mounts: { hA0: [hw + 0.17, 0, H * 0.90], bA0: [hw + 0.07, 0, 0.83], top: [0, 0, H + 0.03] } };
  }

  function geoGlobe(Z, s) {
    const F = [], hw = Z.w / 2, hd = Z.d / 2, H = Z.h, bodyT = H * 0.66;
    pad(F, hw + 0.15, 0.075);
    F.push(...box([0, 0, (0.075 + 0.16) / 2], [hw + 0.012, hd + 0.012, 0.045], 'chrome', 0.3, -0.02));   // chrome skirt
    F.push(...box([0, 0, (0.16 + bodyT) / 2], [hw, hd, (bodyT - 0.16) / 2], 'white', 0, 0, null, 'shell'));
    F.push(...box([0, 0, H * 0.42], [hw + 0.010, hd + 0.010, 0.026], 'chrome', 0.45, -0.03));            // waist band
    for (const sy of [1, -1]) {                                                                           // the clock face
      vdisc(F, 0, sy * (hd + 0.008), H * 0.545, hw * 0.80, 'panel', 0.4, -0.04);
      vdisc(F, 0, sy * (hd + 0.016), H * 0.545, hw * 0.66, 'white', 0.15, -0.06);
      F.push(...box([hw * 0.20, sy * (hd + 0.022), H * 0.575], [hw * 0.30, 0.008, 0.014], 'g0', 0.7, -0.08));
      F.push(...box([0, sy * (hd + 0.014), H * 0.30], [hw * 0.62, 0.008, 0.030], 'g0', 0.4, -0.05));      // grade plate
    }
    F.push(...box([0, 0, (bodyT + H * 0.74) / 2], [hw * 0.82, hd * 0.86, (H * 0.74 - bodyT) / 2], 'g0', 0.35, -0.02));   // head
    F.push(...tube(0, 0, H * 0.74, H * 0.86, hw * 0.44, hw * 0.44, 12, 'glass', { caps: 'none', b: -0.3, db: -0.03 }));  // sight cylinder
    F.push(...tube(0, 0, H * 0.745, H * 0.845, hw * 0.34, hw * 0.34, 12, 'l0', { caps: 'top', b: 0.5, capB: 0.8, db: -0.05 }));
    F.push(...tube(0, 0, H * 0.86, H * 0.885, hw * 0.50, hw * 0.50, 12, 'chrome', { caps: 'none', b: 0.5, db: -0.04 }));
    F.push(...tube(0, 0, H * 0.885, H * 0.955, 0.070, 0.140, 12, 'lamp', { caps: 'none', b: 0.2, db: -0.02 }));           // globe
    F.push(...tube(0, 0, H * 0.955, H + 0.055, 0.140, 0.055, 12, 'lamp', { caps: 'top', b: 0.5, capB: 0.8, db: -0.02 }));
    boot(F, 1, hw + 0.014, H * 0.44, 0, s.out);
    F.push(...bar([hw * 0.5, 0, bodyT], [hw + 0.16, 0, H * 0.70], [0.019, 0.019], 'chrome', 0.35, -0.05));                // swan neck
    return { F, mounts: { hA0: [hw + 0.18, 0, H * 0.70], bA0: [hw + 0.07, 0, H * 0.48], top: [0, 0, H + 0.09] } };
  }

  function geoPed(Z, s) {
    const F = [], hw = Z.w / 2, hd = Z.d / 2, H = Z.h, n = s.n;
    F.push(...box([0, 0, 0.045], [hw + 0.13, hd + 0.13, 0.045], 'conc', -0.05, 0));
    F.push(...box([0, 0, 0.10 + (H - 0.10) * 0.16], [hw, hd, (H - 0.10) * 0.16], 'graph', -0.1, 0));
    F.push(...box([0, 0, 0.10 + (H - 0.10) * 0.62], [hw, hd, (H - 0.10) * 0.32], 'white', 0.05, 0, null, 'shell'));
    F.push(...box([0, 0, H - 0.046], [hw + 0.020, hd + 0.020, 0.046], 'g0', 0.45, -0.03));
    if (n > 1) F.push(...box([hw * 0.30, 0, H - 0.046], [hw * 0.34, hd + 0.024, 0.048], 'g1', 0.45, -0.05));
    F.push(...box([0, 0, H + 0.012], [hw * 0.86, hd * 0.82, 0.014], 'lamp', 0.8, -0.05));
    counter(F, 1, hw, hd, 0.10 + (H - 0.10) * 0.70, n);
    counter(F, -1, hw, hd, 0.10 + (H - 0.10) * 0.70, n);
    const mastX = hw + 0.010, mastZ = H + 0.30;
    F.push(...bar([mastX - 0.02, 0, H - 0.06], [mastX + 0.02, 0, mastZ], [0.026, 0.026], 'steel', 0.25, -0.03));
    F.push(...bar([mastX + 0.02, 0, mastZ], [mastX + 0.16, 0, mastZ + 0.02], [0.020, 0.020], 'steel', 0.35, -0.04));
    F.push(...box([mastX + 0.17, 0, mastZ + 0.02], [0.024, 0.026, 0.024], 'chrome', 0.5, -0.06));
    const mounts = { top: [0, 0, H + 0.04] };
    for (let i = 0; i < n; i++) {
      const sx = i === 0 ? 1 : -1, x = sx * (hw + 0.014), z = 0.10 + (H - 0.10) * 0.50;
      boot(F, sx, x, z, i, s.out);
      mounts['h' + (i === 0 ? 'A0' : 'B0')] = [mastX + 0.19, 0, mastZ + 0.01];
      mounts['b' + (i === 0 ? 'A0' : 'B0')] = [x + sx * 0.05, 0, z + 0.05];
    }
    F.push(...box([-hw - 0.012, 0, 0.10 + (H - 0.10) * 0.22], [0.014, 0.046, 0.058], 'graph', 0.15, -0.04));
    return { F, mounts };
  }

  function geoSlab(Z, s) {
    const F = [], hw = Z.w / 2, hd = Z.d / 2, H = Z.h, n = s.n;
    F.push(...box([0, 0, 0.05], [hw + 0.14, hd + 0.14, 0.05], 'conc', -0.05, 0));                        // plinth
    F.push(...box([0, 0, 0.10 + (H - 0.10) * 0.11], [hw, hd, (H - 0.10) * 0.11], 'graph', -0.1, 0));     // sump cabinet
    F.push(...box([0, 0, 0.10 + (H - 0.10) * 0.55], [hw, hd, (H - 0.10) * 0.34], 'white', 0.04, 0, null, 'shell'));
    /* The valance is divided ACROSS the front, one segment per grade position. It is the only part of
       the machine legible at a screen away, so it carries the whole list rather than one house band. */
    const vz = H - 0.085;
    for (let i = 0; i < n; i++) {
      const w = (hw * 2) / n, cx = -hw + w * (i + 0.5);
      F.push(...box([cx, 0, vz], [w / 2 - 0.004, hd + 0.022, 0.085], 'g' + i, 0.45, -0.03));
    }
    F.push(...box([0, 0, H + 0.010], [hw * 0.88, hd * 0.84, 0.014], 'lamp', 0.85, -0.05));               // topper light
    for (let side = 0; side < s.sides; side++) {
      const sy = side ? -1 : 1;
      counter(F, sy, hw, hd, 0.10 + (H - 0.10) * 0.66, n);
      F.push(...box([hw * 0.52, sy * (hd + 0.008), 0.10 + (H - 0.10) * 0.40], [hw * 0.30, 0.012, 0.090], 'graph', 0.1, -0.03));   // keypad
      F.push(...box([hw * 0.52, sy * (hd + 0.016), 0.10 + (H - 0.10) * 0.40], [hw * 0.22, 0.008, 0.062], 'dark', -0.45, -0.06));
      F.push(...box([-hw * 0.56, sy * (hd + 0.012), 0.10 + (H - 0.10) * 0.44], [hw * 0.06, 0.008, 0.036], 'chrome', 0.55, -0.06)); // card slot
    }
    const mounts = { top: [0, 0, H + 0.04] };
    for (let side = 0; side < s.sides; side++) {
      const sx = side ? -1 : 1, tag = side ? 'B' : 'A';
      F.push(...bar([sx * (hw - 0.02), 0, H - 0.16], [sx * (hw + 0.16), 0, H - 0.11], [0.020, 0.020], 'steel', 0.3, -0.05));      // hose guide
      for (let i = 0; i < n; i++) {
        const z = bootZ(H, i, n), x = sx * (hw + 0.014);
        boot(F, sx, x, z, i, side === 0 ? s.out : -1);
        mounts['h' + tag + i] = [sx * (hw + 0.10), 0, z + 0.075];
        mounts['b' + tag + i] = [x + sx * 0.05, 0, z + 0.05];
      }
    }
    return { F, mounts };
  }

  function geoBrick(Z, s) {
    const F = [], hw = Z.w / 2, hd = Z.d / 2, H = Z.h, n = s.n;
    F.push(...box([0, 0, 0.06], [hw + 0.16, hd + 0.16, 0.06], 'conc', -0.05, 0));
    F.push(...box([0, 0, 0.12 + (H - 0.12) * 0.20], [hw, hd, (H - 0.12) * 0.20], 'graph', -0.1, 0));
    F.push(...box([0, 0, 0.12 + (H - 0.12) * 0.62], [hw, hd, (H - 0.12) * 0.36], 'white', 0.04, 0, null, 'shell'));
    F.push(...box([0, 0, H - 0.075], [hw + 0.022, hd + 0.022, 0.075], 'g0', 0.45, -0.03));
    if (n > 1) F.push(...box([-hw * 0.42, 0, H - 0.075], [hw * 0.40, hd + 0.026, 0.077], 'g1', 0.45, -0.05));
    F.push(...box([0, 0, H + 0.012], [hw * 0.60, hd * 0.80, 0.016], 'lamp', 0.85, -0.05));
    for (let side = 0; side < s.sides; side++) counter(F, side ? -1 : 1, hw * 0.72, hd, H * 0.62, n);
    for (const sy of [-1, 1]) F.push(...bar([-hw - 0.10, sy * (hd + 0.10), 0.30], [hw + 0.10, sy * (hd + 0.10), 0.30], [0.052, 0.052], 'yel', 0.2, -0.02));  // bump rail
    for (const sy of [-1, 1]) for (const sx of [-1, 1])
      F.push(...bar([sx * (hw + 0.10), sy * (hd + 0.10), 0.0], [sx * (hw + 0.10), sy * (hd + 0.10), 0.34], [0.040, 0.040], 'yel', 0.1, -0.02));
    const mounts = { top: [0, 0, H + 0.05] };
    /* High-flow hose is 25 mm and 8 m long — it lives on a REEL, and the reel is the silhouette that
       tells a player this pump fills a boat, not a car. */
    for (let side = 0; side < s.sides; side++) {
      const sx = side ? -1 : 1, tag = side ? 'B' : 'A';
      for (let i = 0; i < n; i++) {
        const z = 0.86 + i * 0.34, x = sx * (hw + 0.10);
        F.push(...tube(0, 0, -0.055, 0.055, 0.155, 0.155, 12, 'graph', { caps: 'both', xf: chain(rotY(Math.PI / 2), move(x, 0, z)), b: 0.1, db: -0.04 }));
        F.push(...tube(0, 0, -0.070, 0.070, 0.070, 0.070, 10, 'steel', { caps: 'both', xf: chain(rotY(Math.PI / 2), move(x, 0, z)), b: 0.35, db: -0.06 }));
        F.push(...bar([sx * (hw - 0.01), 0, z], [x, 0, z], [0.030, 0.030], 'steel', 0.2, -0.03));
        mounts['h' + tag + i] = [x + sx * 0.06, -0.14, z - 0.10];
        mounts['b' + tag + i] = [x + sx * 0.06, -0.14, z - 0.10];
      }
    }
    return { F, mounts };
  }

  function geoLock(Z, s) {
    const F = [], hw = Z.w / 2, hd = Z.d / 2, H = Z.h, colT = H * 0.68;
    pad(F, hw + 0.16, 0.10);
    F.push(...box([0, 0, (0.10 + colT) / 2], [hw, hd, (colT - 0.10) / 2], 'graph', 0, 0, null, 'shell'));
    F.push(...box([0, 0, colT + 0.026], [hw + 0.014, hd + 0.014, 0.030], 'g0', 0.45, -0.03));
    F.push(...box([0, -0.03, (colT + 0.06 + H) / 2], [hw * 0.98, hd * 0.90, (H - colT - 0.06) / 2], 'white', 0.06, 0, null, 'shell'));  // terminal head
    F.push(...box([0, -(hd + 0.02), H * 0.90], [hw * 0.72, 0.014, 0.075], 'dark', -0.4, -0.04));                                        // screen
    F.push(...box([0, -(hd + 0.028), H * 0.90], [hw * 0.60, 0.008, 0.055], 'seg', 0.35, -0.07));
    F.push(...box([hw * 0.42, -(hd + 0.024), H * 0.79], [hw * 0.24, 0.008, 0.012], 'chrome', 0.6, -0.07));                               // card slot
    F.push(...box([-hw * 0.34, -(hd + 0.022), H * 0.78], [hw * 0.34, 0.008, 0.045], 'graph', 0.1, -0.06));                               // keypad
    F.push(...box([0, -0.10, H + 0.024], [hw * 1.12, hd * 1.05, 0.024], 'galv', 0.5, -0.04));                                            // rain hood
    F.push(...box([0, -0.10, H + 0.056], [hw * 0.30, hd * 0.30, 0.020], 'lamp', 0.9, -0.06));
    boot(F, 1, hw + 0.014, H * 0.42, 0, s.out);
    F.push(...bar([hw * 0.4, 0, colT - 0.06], [hw + 0.20, 0.10, colT + 0.10], [0.021, 0.021], 'steel', 0.3, -0.05));                     // swing arm
    return { F, mounts: { hA0: [hw + 0.22, 0.12, colT + 0.10], bA0: [hw + 0.07, 0, H * 0.46], top: [0, 0, H + 0.08] } };
  }

  // ---- island / canopy / sign / fill point ---------------------------------
  const SLOT_PITCH = 3.30;
  const islandLen = (slots) => slots * SLOT_PITCH + 1.15;
  const slotXs = (slots) => Array.from({ length: slots }, (_, i) => (i - (slots - 1) / 2) * SLOT_PITCH);

  function geoIsland(Z, s) {
    const F = [], len = islandLen(Z.slots), hl = len / 2, hw = 0.575, h = 0.165;
    F.push(...box([0, 0, h / 2], [hl, hw, h / 2], 'conc', 0, 0, null, 'shell'));
    for (const sx of [-1, 1]) F.push(...tube(sx * hl, 0, 0, h, hw, hw, 14, 'conc', { caps: 'top', b: 0, capB: 0.25, tag: 'shell' }));
    F.push(...box([0, 0, h], [hl - 0.05, hw - 0.05, 0.016], 'conc', 0.28, -0.02));                       // trowelled top
    F.push(...box([0, 0, h * 0.58], [hl + 0.008, hw + 0.008, 0.028], 'yel', 0.35, -0.02));               // painted curb line
    for (const x of slotXs(Z.slots)) {
      F.push(...box([x, 0, h + 0.012], [0.34, 0.30, 0.012], 'graph', -0.2, -0.04));                      // bolt-down plate
      F.push(...box([x, 0, h + 0.020], [0.30, 0.26, 0.006], 'steel', 0.1, -0.06));
    }
    if (s.bollards) for (const sx of [-1, 1]) {
      const x = sx * (hl - 0.28);
      F.push(...tube(x, 0, h, h + 0.98, 0.105, 0.100, 10, 'g0', { caps: 'top', b: 0.15, capB: 0.5, db: -0.03 }));
      for (const z of [h + 0.62, h + 0.80]) F.push(...tube(x, 0, z, z + 0.075, 0.112, 0.112, 10, 'white', { caps: 'none', b: 0.5, db: -0.05 }));
    }
    if (s.bin) {
      const x = -(hl - 0.95);
      F.push(...box([x, 0, h + 0.29], [0.20, 0.20, 0.29], 'graph', 0.05, -0.02, null, 'shell'));
      F.push(...box([x, 0, h + 0.60], [0.22, 0.22, 0.022], 'g0', 0.4, -0.05));
      F.push(...box([x, 0, h + 0.585], [0.10, 0.15, 0.030], 'dark', -0.7, -0.07));
      F.push(...bar([x + 0.20, 0.05, h + 0.02], [x + 0.26, 0.05, h + 0.92], [0.016, 0.016], 'steel', 0.3, -0.05));   // squeegee
      F.push(...box([x + 0.265, 0.05, h + 0.94], [0.020, 0.075, 0.030], 'yel', 0.5, -0.07));
    }
    const mounts = { deck: [0, 0, h] };
    slotXs(Z.slots).forEach((x, i) => { mounts['slot' + i] = [x, 0, h]; });
    return { F, mounts };
  }

  function geoCanopy(Z, s) {
    const F = [], bays = Z.bays, len = bays * SLOT_PITCH + 2.30, hl = len / 2;
    const wid = s.span > 1 ? (s.span - 1) * 7.40 + 5.80 : 5.80, hwid = wid / 2;
    const clear = 4.55, deck = 0.17, fas = 0.62;
    const rowY = Array.from({ length: s.span }, (_, r) => (r - (s.span - 1) / 2) * 7.40);
    for (const sx of [-1, 1]) for (const y of rowY) {                                                   // posts land on the island curb
      const x = sx * (hl - 1.15);
      F.push(...box([x, y, 0.055], [0.34, 0.34, 0.055], 'conc', -0.05, 0));
      F.push(...box([x, y, (0.11 + clear) / 2], [0.135, 0.135, (clear - 0.11) / 2], 'white', 0.05, 0, null, 'shell'));
      F.push(...box([x, y, 0.42], [0.150, 0.150, 0.30], 'g0', 0.25, -0.03));                            // kick collar
      F.push(...box([x, y, clear - 0.06], [0.185, 0.185, 0.075], 'white', 0.4, -0.03));                 // capital
    }
    if (s.style === 'fascia') {
      /* The deck is GALVANISED SHEET, not enamel. A 12 x 6 m plane of near-white at 40deg elevation is
         the brightest thing on the map and it flattens everything under it — the bright element on a
         canopy has to be the fascia band, which is the part carrying the house colour. */
      F.push(...box([0, 0, clear + deck / 2], [hl, hwid, deck / 2], 'roof', 0, 0, null, 'shell'));
      for (let i = -Math.floor((hl - 0.20) / 1.10); i <= Math.floor((hl - 0.20) / 1.10); i++)          // standing seams
        F.push(...bar([i * 1.10, -hwid, clear + deck + 0.008], [i * 1.10, hwid, clear + deck + 0.008], [0.034, 0.014], 'roof', 0.55, -0.04, 'shell'));
      for (const sx of [-1, 1])                                                    // roof drains at the ends
        F.push(...tube(sx * (hl - 0.55), 0, clear + deck - 0.02, clear + deck + 0.030, 0.085, 0.070, 8, 'graph', { caps: 'top', b: 0.2, db: -0.05 }));
      for (const sy of [-1, 1]) {
        F.push(...box([0, sy * (hwid - 0.05), clear + deck + fas / 2], [hl, 0.10, fas / 2], 'white', 0.25, -0.02, null, 'shell'));
        F.push(...box([0, sy * (hwid + 0.03), clear + deck + fas * 0.34], [hl * 0.99, 0.045, 0.115], 'g0', 0.5, -0.05));
      }
      for (const sx of [-1, 1]) {
        F.push(...box([sx * (hl - 0.05), 0, clear + deck + fas / 2], [0.10, hwid, fas / 2], 'white', 0.25, -0.02, null, 'shell'));
        F.push(...box([sx * (hl + 0.03), 0, clear + deck + fas * 0.34], [0.045, hwid * 0.99, 0.115], 'g0', 0.5, -0.05));
      }
      for (const y of rowY) for (let i = 0; i < bays; i++) {                                            // soffit strips
        const x = (i - (bays - 1) / 2) * SLOT_PITCH;
        F.push(...box([x, y, clear - 0.020], [1.05, 0.30, 0.022], 'lamp', 0.9, -0.05));
        F.push(...box([x, y, clear - 0.012], [1.12, 0.36, 0.014], 'white', 0.5, -0.03));
      }
    } else {
      /* The lean-to: sheet steel on purlins, pitched to the road. A harbour builds this, not a
         moulded fascia, and the corrugation is what sells it — ridge bars, not a texture. */
      const zLo = clear - 0.10, zHi = clear + 1.05;
      F.push(...bar([0, -hwid, zLo], [0, hwid, zHi], [hl, 0.035], 'roof', 0.25, 0, 'shell'));
      for (let i = -6; i <= 6; i++)
        F.push(...bar([i * (hl / 6.4), -hwid, zLo + 0.055], [i * (hl / 6.4), hwid, zHi + 0.055], [0.028, 0.020], 'roof', 0.65, -0.04));
      for (const y of rowY) F.push(...bar([-hl, y, zLo + (zHi - zLo) * (y + hwid) / wid - 0.075], [hl, y, zLo + (zHi - zLo) * (y + hwid) / wid - 0.075], [0.060, 0.055], 'steel', -0.15, 0));
      F.push(...bar([-hl, -hwid, zLo - 0.02], [hl, -hwid, zLo - 0.02], [0.070, 0.065], 'g0', 0.35, -0.03));   // gutter fascia
      for (const y of rowY) for (let i = 0; i < bays; i++) {
        const x = (i - (bays - 1) / 2) * SLOT_PITCH;
        F.push(...bar([x, y, zLo + 0.30], [x, y, zLo + 0.02], [0.022, 0.022], 'steel', 0.2, -0.04));
        F.push(...tube(x, y, zLo - 0.10, zLo + 0.02, 0.115, 0.075, 10, 'lamp', { caps: 'bottom', b: 0.8, capB: 1.0, db: -0.06 }));
      }
    }
    const mounts = { deck: [0, 0, clear] };
    rowY.forEach((y, r) => { mounts['row' + r] = [0, y, 0]; });
    return { F, mounts };
  }

  function geoSign(Z, s) {
    const F = [], n = s.n, pw = Z.pw / 2, ph = Z.ph / 2;
    if (Z.poleH > 0) {
      const z1 = Z.poleH + Z.ph;
      F.push(...tube(0, 0, -0.02, 0.16, 0.42, 0.38, 12, 'conc', { caps: 'top', b: -0.05, capB: 0.1 }));
      F.push(...box([0, 0, (0.16 + Z.poleH + 0.10) / 2], [0.135, 0.115, (Z.poleH - 0.06) / 2], 'steel', 0.05, 0, null, 'shell'));
      const cz = Z.poleH + ph;
      F.push(...box([0, 0, cz], [pw, 0.085, ph], 'white', 0.1, 0, null, 'shell'));
      F.push(...box([0, 0, z1 - 0.30], [pw + 0.020, 0.105, 0.30], 'g0', 0.45, -0.03));                  // header
      for (const sy of [-1, 1]) F.push(...box([0, sy * 0.10, z1 - 0.30], [pw * 0.80, 0.010, 0.115], 'panel', 0.75, -0.06));
      for (let i = 0; i < n; i++) {
        const rz = z1 - 0.62 - i * ((Z.ph - 0.42) / Math.max(1, n)) - 0.12;
        for (const sy of [-1, 1]) {
          F.push(...box([-pw * 0.66, sy * 0.098, rz], [pw * 0.26, 0.012, 0.115], 'g' + i, 0.5, -0.05));  // grade chip
          F.push(...box([pw * 0.24, sy * 0.098, rz], [pw * 0.66, 0.012, 0.135], 'dark', -0.4, -0.05));   // price window
          for (let d = 0; d < 3; d++)
            F.push(...box([pw * (-0.24 + d * 0.48), sy * 0.108, rz], [pw * 0.15, 0.008, 0.098], 'seg', 0.4, -0.08));
        }
      }
      return { F, mounts: { head: [0, 0, z1 - 0.15], foot: [0, 0, 0.16] } };
    }
    /* A-frame: two boards leaning into each other, one row, kerbside. The cheapest way a station
       announces a price change, and the only sign here that a player could knock over. */
    for (const sy of [-1, 1]) {
      F.push(...bar([0, sy * 0.30, 0.02], [0, sy * 0.05, Z.ph], [pw, 0.022], 'white', 0.15, 0, 'shell'));
      F.push(...box([-pw * 0.62, sy * 0.28, Z.ph * 0.68], [pw * 0.30, 0.012, 0.115], 'g0', 0.5, -0.05));
      F.push(...box([pw * 0.26, sy * 0.28, Z.ph * 0.66], [pw * 0.62, 0.012, 0.150], 'dark', -0.4, -0.05));
      for (let d = 0; d < 3; d++)
        F.push(...box([pw * (-0.22 + d * 0.46), sy * 0.30, Z.ph * 0.66], [pw * 0.14, 0.008, 0.110], 'seg', 0.4, -0.08));
    }
    F.push(...bar([-pw * 0.7, -0.16, Z.ph * 0.42], [-pw * 0.7, 0.16, Z.ph * 0.42], [0.016, 0.016], 'steel', 0.2, -0.05));
    return { F, mounts: { head: [0, 0, Z.ph], foot: [0, 0, 0.03] } };
  }

  function geoFill(Z, s) {
    const F = [], n = s.n;
    if (Z.pad) {
      const hp = Z.pad / 2;
      F.push(...box([0, 0, 0.035], [hp, hp, 0.035], 'conc', 0, 0, null, 'shell'));
      F.push(...box([0, 0, 0.058], [hp - 0.05, hp - 0.05, 0.014], 'asph', -0.3, -0.02));
      F.push(...box([0, 0, 0.048], [hp + 0.006, hp + 0.006, 0.020], 'yel', 0.3, -0.02));
      const cols = Math.min(2, n), rows = Math.ceil(n / cols);
      for (let i = 0; i < n; i++) {
        const cx = (i % cols - (cols - 1) / 2) * (hp * 0.92), cy = (Math.floor(i / cols) - (rows - 1) / 2) * (hp * 0.92);
        F.push(...tube(cx, cy, 0.056, 0.080, 0.235, 0.225, 12, 'steel', { caps: 'none', b: 0.2, db: -0.04 }));
        F.push(...tube(cx, cy, 0.078, 0.092, 0.200, 0.190, 12, 'g' + i, { caps: 'top', b: 0.35, capB: 0.7, db: -0.06 }));
        F.push(...box([cx + 0.13, cy, 0.096], [0.045, 0.030, 0.016], 'brass', 0.6, -0.09));
      }
      return { F, mounts: { cap0: [0, 0, 0.10] } };
    }
    const H = Z.h;
    F.push(...box([0, 0, 0.05], [0.28 + n * 0.14, 0.26, 0.05], 'conc', -0.05, 0));
    for (let i = 0; i < n; i++) {
      const x = (i - (n - 1) / 2) * 0.28;
      F.push(...bar([x, 0, 0.08], [x, 0, H], [0.036, 0.036], 'galv', 0.15, 0, 'shell'));
      F.push(...tube(x, 0, H, H + 0.075, 0.070, 0.110, 10, 'galv', { caps: 'top', b: 0.4, capB: 0.7, db: -0.04 }));   // mushroom vent
      F.push(...box([x, -0.055, H - 0.34], [0.052, 0.020, 0.075], 'g' + i, 0.5, -0.05));                              // grade tag
    }
    F.push(...bar([-(n - 1) * 0.14 - 0.06, 0, H * 0.55], [(n - 1) * 0.14 + 0.06, 0, H * 0.55], [0.024, 0.024], 'galv', 0.3, -0.04));
    return { F, mounts: { top: [0, 0, H + 0.10] } };
  }

  // ---- the storefront ------------------------------------------------------
  /* PUBLISHED SHELL. Everything the interior rig needs to build the room is derived here and nowhere
     else — wall thickness, floor and ceiling height, the entry opening and its travel, the glazing
     runs. Two rigs, one geometry: the room cannot drift from the box it is inside. */
  function shell(size) {
    size = resolveSize('store', size);
    const Z = TYPES.store.sizes[size], hw = Z.w / 2, hd = Z.d / 2, fz = 0.18, k = Z.kiosk;
    const doorW = k ? 0.94 : 2.40, doorH = k ? 2.10 : 2.37, doorC = k ? hw - 0.92 : 0.30, pil = k ? 0.26 : 0.38;
    const svcCx = k ? -hw + 0.86 : -hw + 1.30, svcW = k ? 0.84 : 1.02, svcStoop = k ? 0.72 : 0.92;
    const gz0 = fz + 0.10, gz1 = fz + (k ? 1.12 : 2.19);
    const runs = k ? [[-hw + pil, doorC - doorW / 2 - 0.09]]
                   : [[-hw + pil, doorC - doorW / 2 - 0.12], [doorC + doorW / 2 + 0.12, hw - pil]];
    return { size, label: Z.label, w: Z.w, d: Z.d, h: Z.h, kiosk: k, wall: Z.wall, pil,
      floorZ: fz, clear: Z.clear, ceilZ: fz + Z.clear, hw, hd,
      inner: { hw: +(hw - Z.wall).toFixed(3), hd: +(hd - Z.wall).toFixed(3) },
      frontY: hd, backY: -hd, roofZ: Z.h - (k ? 0.22 : 0.44),
      door: { cx: doorC, w: doorW, h: doorH, y: hd, leaf: doorW / 2, travel: doorW / 2 + 0.06, kind: 'slide_bipart' },
      glaze: { z0: gz0, z1: gz1, runs, inset: 0.07, mull: 1.34 },
      band: { z0: Z.h - (k ? 0.80 : 1.38), z1: Z.h - (k ? 0.22 : 0.44) },
      tower: k ? null : { cx: doorC, halfW: 1.62, proud: 0.34, top: Z.h + 0.72 },
      /* Back of house. The front is the shopfront and the back is the working face: one outward swing
         leaf onto a stoop, and — on the store only — the fixed ladder that makes the roof reachable.
         A pay booth gets the door and no ladder: nobody climbs a 3 m box to look at one condenser. */
      svc: { cx: svcCx, w: svcW, h: 2.10, y: -hd, kind: 'swing_single_out', hinge: '-x', stoop: svcStoop },
      /* Beside the door, not at the far corner: the stoop that serves the door is the ladder's landing,
         and a crew carrying a filter up to the plant walks out of one and onto the other. */
      ladder: k ? null : { x: +(svcCx + svcW / 2 + 0.80).toFixed(2), y: -(hd + 0.26), w: 0.46, base: 0.12, top: Z.h + 0.64, cageFrom: 2.30 },
      /* Inside face of the parapet: what a body can actually stand on up there. */
      roofWalk: k ? null : { inset: 0.16, z: Z.h - 0.44, parapet: 0.44 },
    };
  }

  function geoStore(size, s) {
    const B = shell(size), F = [], hw = B.hw, hd = B.hd, h = B.h, fz = B.floorZ, wt = B.wall;
    const rz = B.roofZ, k = B.kiosk, G = B.glaze;
    F.push(...box([0, 0, fz / 2], [hw + 0.10, hd + 0.10, fz / 2], 'conc', -0.05, 0, null, 'shell'));           // slab
    const wlk = k ? 0.95 : 1.65;
    F.push(...box([0, hd + 0.10 + wlk / 2, 0.058], [hw + 0.58, wlk / 2, 0.058], 'conc', -0.18, 0));            // walk
    F.push(...box([0, hd + 0.10 + wlk, 0.052], [hw + 0.58, 0.035, 0.052], 'yel', 0.30, -0.03));                // painted edge
    F.push(...box([0, -hd + wt / 2, (fz + rz) / 2], [hw, wt / 2, (rz - fz) / 2], 'white', 0, 0, null, 'shell'));
    for (const sx of [-1, 1])
      F.push(...box([sx * (hw - wt / 2), 0, (fz + rz) / 2], [wt / 2, hd, (rz - fz) / 2], 'white', 0.02, 0, null, 'shell'));
    /* Dark base course on every outside face. A modern box is a white band over a dark band, and the
       dark one is what keeps a 4 m wall from reading as a blank sheet at 32 px/m. */
    const bb = 0.86;
    F.push(...box([0, -hd - 0.014, fz + bb / 2], [hw + 0.014, 0.014, bb / 2], 'graph', 0.05, -0.02));
    for (const sx of [-1, 1])
      F.push(...box([sx * (hw + 0.014), 0, fz + bb / 2], [0.014, hd + 0.014, bb / 2], 'graph', 0.05, -0.02));
    // ---- front wall: pilasters, glazing, entry, spandrel ----
    const jL = B.door.cx - B.door.w / 2 - 0.12, jR = B.door.cx + B.door.w / 2 + 0.12;
    const solid = (x0, x1, z0, z1, mat, b) => F.push(...box([(x0 + x1) / 2, hd - wt / 2, (z0 + z1) / 2], [(x1 - x0) / 2, wt / 2, (z1 - z0) / 2], mat || 'white', b || 0.03, 0, null, 'shell'));
    solid(-hw, -hw + B.pil, fz, B.band.z0);
    solid(hw - B.pil, hw, fz, B.band.z0);
    if (!k) { solid(jL, B.door.cx - B.door.w / 2, fz, B.band.z0); solid(B.door.cx + B.door.w / 2, jR, fz, B.band.z0); }
    solid(k ? jL : B.door.cx - B.door.w / 2, k ? hw - B.pil : B.door.cx + B.door.w / 2, fz + B.door.h, B.band.z0);   // door header
    G.runs.forEach(([x0, x1]) => {
      const cx = (x0 + x1) / 2, w = (x1 - x0) / 2;
      solid(x0, x1, G.z1 + 0.10, B.band.z0);                                                                    // spandrel
      F.push(...box([cx, hd - G.inset, (G.z0 + G.z1) / 2], [w, 0.030, (G.z1 - G.z0) / 2], 'win', 0.10, 0.02));   // glass
      F.push(...box([cx, hd - 0.025, G.z1 + 0.055], [w, 0.048, 0.055], 'chrome', 0.50, -0.03));                  // head rail
      F.push(...box([cx, hd - 0.025, G.z0 - 0.055], [w, 0.048, 0.055], 'chrome', 0.30, -0.03));                  // base rail
      const n = Math.max(1, Math.round((x1 - x0) / G.mull));
      for (let i = 1; i < n; i++)
        F.push(...box([x0 + (x1 - x0) * i / n, hd - 0.030, (G.z0 + G.z1) / 2], [0.034, 0.044, (G.z1 - G.z0) / 2], 'chrome', 0.45, -0.04));
    });
    // ---- the entry: two leaves, bipart slide, shut by default ----
    /* No masking plane behind the entry. The interior rig owns what is behind that opening, so a
       shell painted over an interior in the same cell shows the room through the door — which is the
       whole point of the two sprites sharing a cell. */
    const trav = B.door.travel * s.open, lw = B.door.leaf / 2;
    for (const sx of [-1, 1]) {
      const cx = B.door.cx + sx * (lw + trav);
      F.push(...box([cx, hd - 0.10, fz + B.door.h / 2], [lw, 0.026, B.door.h / 2 - 0.02], 'win', 0.20, -0.02));
      F.push(...box([cx + sx * (lw - 0.03), hd - 0.10, fz + B.door.h / 2], [0.032, 0.034, B.door.h / 2], 'chrome', 0.55, -0.05));  // leading stile
      F.push(...box([cx, hd - 0.10, fz + B.door.h - 0.045], [lw, 0.032, 0.045], 'chrome', 0.5, -0.05));
      F.push(...box([cx, hd - 0.10, fz + 0.05], [lw, 0.032, 0.05], 'chrome', 0.3, -0.05));
    }
    F.push(...box([B.door.cx, hd + 0.52, 0.070], [1.20, 0.52, 0.014], 'graph', -0.25, -0.04));                   // mat
    // ---- the illuminated band, and the tower that steps out of it ----
    const bz = (B.band.z0 + B.band.z1) / 2, bh = (B.band.z1 - B.band.z0) / 2;
    F.push(...box([0, hd + 0.035, bz], [hw + 0.035, 0.055, bh], 'g0', 0.45, -0.02));
    F.push(...box([0, hd + 0.082, bz], [hw * 0.66, 0.014, bh - 0.075], 'lamp', 0.85, -0.05));
    for (let i = 0; i < (k ? 3 : 5); i++)                                                                        // word blocks, not letters
      F.push(...box([(-((k ? 3 : 5) - 1) / 2 + i) * hw * 0.24, hd + 0.094, bz], [hw * 0.075, 0.010, bh * 0.42], 'graph', -0.3, -0.08));
    for (const sx of [-1, 1]) {
      F.push(...box([sx * (hw + 0.035), hd * 0.45, bz], [0.055, hd * 0.55, bh], 'g0', 0.35, -0.02));
      F.push(...box([sx * (hw + 0.075), hd * 0.45, bz], [0.012, hd * 0.42, bh - 0.075], 'lamp', 0.70, -0.05));
    }
    if (B.tower) {
      /* The entry element is a PORTAL, not a slab: two piers and a header standing 0.34 m proud, with
         the doors recessed inside them. A solid tower across the front would bury the one thing the
         building has to show a player — the way in. */
      const T = B.tower, pw = 0.42, hz0 = fz + B.door.h + 0.10;
      for (const sx of [-1, 1]) {
        const cx = T.cx + sx * (T.halfW - pw / 2);
        F.push(...box([cx, hd + T.proud / 2, (fz + T.top) / 2], [pw / 2, T.proud / 2 + 0.02, (T.top - fz) / 2], 'white', 0.10, -0.03, null, 'shell'));
        F.push(...box([cx, hd + T.proud / 2, fz + 0.43], [pw / 2 + 0.012, T.proud / 2 + 0.03, 0.43], 'graph', 0.05, -0.05));
      }
      F.push(...box([T.cx, hd + T.proud / 2, (hz0 + T.top) / 2], [T.halfW, T.proud / 2 + 0.02, (T.top - hz0) / 2], 'white', 0.12, -0.03, null, 'shell'));
      F.push(...box([T.cx, hd + T.proud / 2, T.top - 0.09], [T.halfW + 0.05, T.proud / 2 + 0.055, 0.090], 'galv', 0.55, -0.05));                    // cap
      F.push(...box([T.cx, hd + T.proud + 0.030, (hz0 + T.top - 0.34) / 2], [T.halfW - 0.62, 0.030, (T.top - 0.34 - hz0) / 2], 'g0', 0.5, -0.06));
      F.push(...box([T.cx, hd + T.proud + 0.060, (hz0 + T.top - 0.34) / 2], [T.halfW - 0.80, 0.014, (T.top - 0.44 - hz0) / 2], 'lamp', 0.9, -0.09));
      for (let i = 0; i < 4; i++)                                                                                                                    // word blocks
        F.push(...box([T.cx + (-1.5 + i) * 0.34, hd + T.proud + 0.072, (hz0 + T.top - 0.34) / 2 + 0.10], [0.105, 0.010, 0.24], 'graph', -0.3, -0.11));
      F.push(...box([T.cx, hd + T.proud + 0.020, hz0 - 0.055], [T.halfW - 0.05, 0.030, 0.055], 'graph', -0.2, -0.05));                              // soffit line
    }
    // ---- back of house: service door, stoop, fixed ladder ----
    /* The one face with no glass on it. A steel leaf, a kick plate, a stoop wide enough to set a
       carton down on, and the ladder standing 0.26 m off the wall with its cage starting at 2.30 m —
       which is the whole reason the roof upstairs is allowed to say `walkable: true`. */
    const V = B.svc;
    F.push(...box([V.cx, -hd - V.stoop / 2, 0.062], [V.w / 2 + 0.34, V.stoop / 2, 0.062], 'conc', -0.15, 0));
    F.push(...box([V.cx, -hd - 0.028, fz + V.h / 2 + 0.05], [V.w / 2 + 0.075, 0.028, V.h / 2 + 0.075], 'graph', -0.12, -0.02));
    F.push(...box([V.cx, -hd - 0.056, fz + V.h / 2], [V.w / 2, 0.028, V.h / 2], 'steel', 0.05, -0.03));
    F.push(...box([V.cx, -hd - 0.082, fz + 0.21], [V.w / 2 - 0.035, 0.014, 0.21], 'galv', 0.32, -0.05));
    F.push(...box([V.cx + V.w / 2 - 0.11, -hd - 0.092, fz + 1.04], [0.060, 0.016, 0.028], 'chrome', 0.60, -0.06));
    F.push(...box([V.cx, -hd - 0.10, fz + V.h + 0.20], [0.105, 0.080, 0.060], 'graph', 0.05, -0.03));
    F.push(...box([V.cx, -hd - 0.135, fz + V.h + 0.15], [0.078, 0.048, 0.020], 'lamp', 0.85, -0.07));
    const LD = B.ladder;
    if (LD) {
      for (const sx of [-1, 1])
        F.push(...bar([LD.x + sx * LD.w / 2, LD.y, LD.base], [LD.x + sx * LD.w / 2, LD.y, LD.top], [0.030, 0.030], 'galv', 0.38, -0.03));
      for (let z = LD.base + 0.30; z < LD.top - 0.34; z += 0.30)
        F.push(...bar([LD.x - LD.w / 2, LD.y, z], [LD.x + LD.w / 2, LD.y, z], [0.019, 0.019], 'galv', 0.48, -0.04));
      const cy = LD.y - 0.36, ch = LD.w / 2 + 0.11;
      for (let z = LD.cageFrom; z < h; z += 0.44) {
        F.push(...bar([LD.x - ch, cy, z], [LD.x + ch, cy, z], [0.017, 0.017], 'galv', 0.42, -0.04));
        for (const sx of [-1, 1]) F.push(...bar([LD.x + sx * ch, cy, z], [LD.x + sx * ch, LD.y, z], [0.017, 0.017], 'galv', 0.30, -0.05));
      }
      for (const sx of [-1, 1]) {
        F.push(...bar([LD.x + sx * ch, cy, LD.cageFrom], [LD.x + sx * ch, cy, h], [0.015, 0.015], 'galv', 0.34, -0.04));
        F.push(...bar([LD.x + sx * LD.w / 2, LD.y, LD.top], [LD.x + sx * LD.w / 2, LD.y + 0.46, LD.top], [0.026, 0.026], 'galv', 0.52, -0.03));
      }
    }
    // ---- roof: deck, parapet, plant ----
    F.push(...box([0, 0, rz - 0.05], [hw - 0.03, hd - 0.03, 0.05], 'roof', 0.05, 0, null, 'shell'));
    const pp = k ? 0.22 : 0.44;
    for (const sy of [-1, 1]) {
      F.push(...box([0, sy * (hd - 0.08), rz + pp / 2], [hw, 0.08, pp / 2], 'white', 0.18, -0.02, null, 'shell'));
      F.push(...box([0, sy * (hd - 0.08), rz + pp], [hw + 0.03, 0.105, 0.028], 'galv', 0.55, -0.04));
    }
    for (const sx of [-1, 1]) {
      F.push(...box([sx * (hw - 0.08), 0, rz + pp / 2], [0.08, hd - 0.16, pp / 2], 'white', 0.18, -0.02, null, 'shell'));
      F.push(...box([sx * (hw - 0.08), 0, rz + pp], [0.105, hd - 0.13, 0.028], 'galv', 0.55, -0.04));
    }
    const rtu = (x, y, w2, d2, hh) => {
      F.push(...box([x, y, rz + 0.055], [w2 + 0.06, d2 + 0.06, 0.055], 'graph', -0.2, -0.02));                   // curb
      F.push(...box([x, y, rz + 0.11 + hh / 2], [w2, d2, hh / 2], 'galv', 0.10, -0.03, null, 'shell'));
      F.push(...box([x, y, rz + 0.11 + hh], [w2 * 0.86, d2 * 0.86, 0.030], 'galv', 0.45, -0.05));
      for (let i = -2; i <= 2; i++) F.push(...box([x + i * w2 * 0.30, y, rz + 0.11 + hh + 0.02], [w2 * 0.07, d2 * 0.80, 0.022], 'graph', 0.2, -0.07));
    };
    if (k) { rtu(0, -hd * 0.35, 0.42, 0.34, 0.30); }
    else {
      rtu(-hw * 0.44, -hd * 0.34, 0.74, 0.55, 0.38);
      rtu(hw * 0.30, -hd * 0.42, 0.62, 0.48, 0.34);
      F.push(...tube(hw * 0.66, hd * 0.30, rz, rz + 0.52, 0.075, 0.075, 8, 'galv', { caps: 'top', b: 0.35, db: -0.04 }));
      F.push(...tube(hw * 0.66, hd * 0.30, rz + 0.52, rz + 0.60, 0.115, 0.100, 8, 'galv', { caps: 'top', b: 0.7, db: -0.06 }));
    }
    // ---- what stands against the side wall ----
    const mounts = { door: [B.door.cx, hd + 0.10, fz], sign: [0, hd + 0.09, bz], roof: [0, 0, rz + pp],
      svc: [B.svc.cx, -hd - 0.06, fz], ladder: B.ladder ? [B.ladder.x, B.ladder.y, B.ladder.base] : null };
    if (!k) {
      const ix = hw + 0.44;
      F.push(...box([ix, -hd * 0.10, fz + 0.72], [0.42, 0.62, 0.72], 'white', 0.05, -0.02, null, 'shell'));       // ice merchandiser
      F.push(...box([ix + 0.44, -hd * 0.10, fz + 0.84], [0.018, 0.50, 0.48], 'win', 0.35, -0.05));
      F.push(...box([ix, -hd * 0.10, fz + 1.50], [0.44, 0.64, 0.075], 'g0', 0.5, -0.04));
      const cx = hw + 0.46, cy = -hd * 0.66;
      for (const sy of [-1, 1]) F.push(...bar([cx - 0.40, cy + sy * 0.42, fz], [cx + 0.40, cy + sy * 0.42, fz], [0.028, 0.028], 'galv', 0.2, -0.02));
      for (let i = 0; i <= 4; i++) F.push(...bar([cx - 0.40 + i * 0.20, cy - 0.42, fz + 0.02], [cx - 0.40 + i * 0.20, cy - 0.42, fz + 1.55], [0.024, 0.024], 'galv', 0.35, -0.03));
      for (let i = 0; i <= 3; i++) F.push(...bar([cx - 0.40, cy - 0.42, fz + 0.10 + i * 0.48], [cx + 0.40, cy - 0.42, fz + 0.10 + i * 0.48], [0.022, 0.022], 'galv', 0.3, -0.03));
      F.push(...box([cx, cy, fz + 1.62], [0.42, 0.44, 0.045], 'galv', 0.5, -0.04));
      F.push(...box([cx + 0.42, cy, fz + 1.20], [0.014, 0.20, 0.14], 'g0', 0.55, -0.06));                          // placard
      for (let i = 0; i < 3; i++) F.push(...tube(cx - 0.22 + i * 0.22, cy + 0.10, fz + 0.02, fz + 0.62, 0.115, 0.105, 10, 'g0', { caps: 'top', b: 0.15, capB: 0.4, db: -0.05 }));
      for (const sx of [-1, 1]) {                                                                                 // bollards at the glass
        F.push(...tube(sx * hw * 0.62, hd + 0.95, 0, 0.92, 0.100, 0.095, 10, 'g0', { caps: 'top', b: 0.15, capB: 0.45, db: -0.03 }));
        F.push(...tube(sx * hw * 0.62, hd + 0.95, 0.60, 0.68, 0.108, 0.108, 10, 'white', { caps: 'none', b: 0.5, db: -0.05 }));
      }
      F.push(...box([B.door.cx + 1.85, hd + 0.62, 0.070 + 0.28], [0.22, 0.22, 0.28], 'graph', 0.05, -0.03, null, 'shell'));   // bin
      F.push(...box([B.door.cx + 1.85, hd + 0.62, 0.070 + 0.58], [0.24, 0.24, 0.022], 'g0', 0.4, -0.05));
      mounts.ice = [ix + 0.50, -hd * 0.10, fz + 0.90];
      mounts.propane = [cx + 0.48, cy, fz + 0.80];
      mounts.bin = [B.door.cx + 1.85, hd + 0.86, 0.40];
    }
    return { F, mounts, B };
  }

  /* The entry, in cell pixels and in metres: threshold, both leading edges and whether a body fits.
     A bipart slider has no swept arc — the collider is the leaf itself, so there is no keep_clear. */
  function doorMount(size, dir, opts) {
    opts = opts || {}; size = resolveSize('store', size);
    const B = shell(size), s = resolve('store', size, opts), M = measure('store', size, opts);
    const cam = camBasis({ dir, elev: opts.elev != null ? opts.elev : DEFAULT_ELEV });
    const P = (p) => { const v = proj(p[0], p[1], p[2], cam, M.cx, M.cy); return { x: +v.sx.toFixed(1), y: +v.sy.toFixed(1), m: p.map((q) => +q.toFixed(3)) }; };
    const gap = B.door.w * s.open;
    return { kind: B.door.kind, open: s.open, clearW: +gap.toFixed(2), clear: gap >= 0.80,
      threshold: P([B.door.cx, B.door.y + 0.06, B.floorZ]),
      leadL: P([B.door.cx - gap / 2, B.door.y, B.floorZ]),
      leadR: P([B.door.cx + gap / 2, B.door.y, B.floorZ]),
      leafTravel: +B.door.travel.toFixed(2), clearAt: +(0.80 / B.door.w).toFixed(2) };
  }

  // ---- assembly ------------------------------------------------------------
  const GCACHE = new Map();
  function geo(type, size, opts) {
    const Z = TYPES[type].sizes[size], s = resolve(type, size, opts);
    const key = skey(type, size, s);
    if (GCACHE.has(key)) return GCACHE.get(key);
    let G;
    if (type === 'dispenser') {
      G = Z.fam === 'post' ? geoPost(Z, s) : Z.fam === 'globe' ? geoGlobe(Z, s)
        : Z.fam === 'ped' ? geoPed(Z, s) : Z.fam === 'slab' ? geoSlab(Z, s)
        : Z.fam === 'brick' ? geoBrick(Z, s) : geoLock(Z, s);
    } else if (type === 'island') G = geoIsland(Z, s);
    else if (type === 'canopy') G = geoCanopy(Z, s);
    else if (type === 'sign') G = geoSign(Z, s);
    else if (type === 'store') G = geoStore(size, s);
    else G = geoFill(Z, s);
    /* IsoSolid compares `depth - db` and the SMALLER value wins, so a face is pulled proud by a
       POSITIVE db. Everything above is authored the intuitive way round — negative reads as "in
       front" — so the sign is flipped once, here, exactly as the fuel rig does it. */
    G.F = G.F.map((f) => (f.db ? Object.assign({}, f, { db: -f.db }) : f));
    G.F = wearPass(G.F, s, type, size);
    G.spec = s; G.dims = Z;
    if (GCACHE.size > 320) GCACHE.clear();
    GCACHE.set(key, G);
    return G;
  }
  /* Wear on a forecourt is not speckle either: enamel chalks, the concrete greys, and on a derelict
     the steel that was never painted — mast, posts, vent risers — streaks rust down from its
     fixings. The lamps are a separate axis: `lit` is a material, not a wear state, because a working
     station at night and a dead one at noon are different sprites. */
  function wearPass(F, s, type, size) {
    if (s.wear === 'fresh') return F;
    const dull = s.wear === 'derelict' ? -0.5 : -0.13;
    const rnd = mulberry(type.length * 613 + size.length * 79 + (s.wear === 'derelict' ? 5 : 2));
    return F.map((f) => {
      if (f.tag !== 'shell') return f;
      if (s.wear === 'derelict' && rnd() < 0.14) return Object.assign({}, f, { mat: 'rust', b: (f.b || 0) - 0.15 });
      return Object.assign({}, f, { b: (f.b || 0) + dull });
    });
  }
  const xform = (F, fn) => F.map((f) => ({ v: f.v.map(fn), mat: f.mat, b: f.b, db: f.db, tag: f.tag }));

  // ---- cells ---------------------------------------------------------------
  const MCACHE = new Map();
  function measure(type, size, opts) {
    const s = resolve(type, size, opts), key = skey(type, size, s);
    if (MCACHE.has(key)) return MCACHE.get(key);
    const G = geo(type, size, opts);
    let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
    for (let d = 0; d < 8; d++) {
      const B = camBasis({ dir: d, elev: DEFAULT_ELEV });
      for (const f of G.F) for (const v of f.v) {
        const p = proj(v[0], v[1], v[2], B, 0, 0);
        if (p.sx < x0) x0 = p.sx; if (p.sx > x1) x1 = p.sx;
        if (p.sy < y0) y0 = p.sy; if (p.sy > y1) y1 = p.sy;
      }
    }
    const pad2 = 2;
    const M = { W: Math.ceil(x1 - x0) + pad2 * 2, H: Math.ceil(y1 - y0) + pad2 * 2,
                cx: Math.round(-x0 + pad2), cy: Math.round(-y0 + pad2), mounts: G.mounts, spec: s };
    MCACHE.set(key, M);
    return M;
  }
  function cell(type, size, opts) {
    size = resolveSize(type, size);
    const M = measure(type, size, opts);
    return { W: M.W, H: M.H, cx: M.cx, cy: M.cy, pivot: { x: M.cx, y: M.cy } };
  }
  /* Where a mount lands in cell pixels at a facing: a hose outlet (hA0..hB3), a nozzle boot
     (bA0..bB3), an island slot (slot0..slot3), a canopy row, a sign head. */
  function mount(type, dir, name, opts) {
    opts = opts || {};
    const size = resolveSize(type, opts.size);
    const M = measure(type, size, opts);
    const pt = (M.mounts || {})[name];
    if (!pt) return null;
    const B = camBasis({ dir, elev: opts.elev != null ? opts.elev : DEFAULT_ELEV });
    const v = proj(pt[0], pt[1], pt[2], B, M.cx, M.cy);
    return { x: v.sx, y: v.sy, depth: v.d, m: pt.slice() };
  }
  const hoseMount = (dir, i, side, opts) => mount('dispenser', dir, 'h' + (side === 1 ? 'B' : 'A') + i, opts);
  const reachOf = (size) => (TYPES.dispenser.sizes[resolveSize('dispenser', size)] || {}).reach || 4;
  const flowOf = (size) => (TYPES.dispenser.sizes[resolveSize('dispenser', size)] || {}).lpm || 40;

  // ---- render --------------------------------------------------------------
  function render(type, dir, opts) {
    opts = opts || {};
    if (!TYPES[type]) throw new Error('StationIso: unknown type ' + type);
    const size = resolveSize(type, opts.size);
    const M = measure(type, size, opts), G = geo(type, size, opts);
    let F = G.F;
    if (opts.yaw) F = xform(F, IS.rotZ(opts.yaw * DEG));
    return paint(F, {
      W: M.W, H: M.H, cx: M.cx, cy: M.cy, dir, elev: opts.elev != null ? opts.elev : DEFAULT_ELEV,
      MATS: matsFor(opts.grades, opts.lit), keyline: opts.keyline, key: opts.key || KEY,
    });
  }
  function sheet(type, opts) {
    opts = opts || {};
    const size = resolveSize(type, opts.size), c = cell(type, size, opts), out = [];
    for (let d = 0; d < 8; d++) out.push(render(type, d, Object.assign({}, opts, { size })));
    return { cells: out, W: c.W, H: c.H, cx: c.cx, cy: c.cy, dirs: 8, order: ORDER.slice() };
  }

  // ---- the forecourt -------------------------------------------------------
  /* A station is bigger than any cell, so it is a PLACEMENT LIST in metres, not a sprite. Ground
     coordinates, one pivot per piece, painter-ordered by the same depth the rasteriser uses. */
  const LAYER = { fillport: 0, island: 1, dispenser: 2, sign: 2, store: 2, canopy: 3 };
  function project(x, y, z, dir, elev) {
    const B = camBasis({ dir, elev: elev != null ? elev : DEFAULT_ELEV });
    const p = proj(x, y, z || 0, B, 0, 0);
    return { x: p.sx, y: p.sy, depth: p.d };
  }
  function plan(cfg) {
    cfg = cfg || {};
    const grades = cfg.grades || ['gas', 'diesel', 'mixed'];
    const rows = (cfg.rows && cfg.rows.length ? cfg.rows : [[{ size: 'sMpd' }]]);
    const pieces = [];
    let maxSlots = 0;
    rows.forEach((row, r) => {
      const y = (r - (rows.length - 1) / 2) * 7.40;
      const slots = Math.max(1, Math.min(4, row.length));
      maxSlots = Math.max(maxSlots, slots);
      pieces.push({ type: 'island', size: 's' + slots, x: 0, y,
        opts: { grades, bollards: cfg.bollards !== false, bin: r === 0 && cfg.bin !== false, wear: cfg.wear, lit: cfg.lit } });
      slotXs(slots).forEach((x, i) => {
        const d = row[i] || {};
        const gr = d.grades || grades;
        pieces.push({ type: 'dispenser', size: d.size || 'sTwin', x, y,
          opts: { grades: gr, hoses: d.hoses || gr.length, sides: d.sides, out: d.out, wear: cfg.wear, lit: cfg.lit } });
      });
    });
    const hl = islandLen(maxSlots) / 2;
    /* A canopy that only covers one island is not a compromise — it is what a harbour builds, and it
       keeps the near lane open so the camera can see under the roof instead of at it. */
    if (cfg.canopy) {
      const span = Math.max(1, Math.min(2, cfg.canopySpan || (rows.length > 1 ? 2 : 1)));
      pieces.push({ type: 'canopy', size: 'sB' + Math.max(2, Math.min(4, maxSlots)),
        x: 0, y: span === 1 && rows.length > 1 ? -(rows.length - 1) / 2 * 7.40 : 0,
        opts: { grades, style: cfg.canopy === 'tin' ? 'tin' : 'fascia', span, wear: cfg.wear, lit: cfg.lit } });
    }
    /* Sign and fill points sit on opposite sides on purpose: +y is the ROAD (the sign is read from
       there), -y is the BUILDING side, and the tanker fill cluster belongs back there with the store
       rather than across the traffic path. */
    const roadY = rows.length * 3.9 + 1.6, backY = -(rows.length * 3.9 + 3.6);
    if (cfg.sign) pieces.push({ type: 'sign', size: cfg.sign, x: -(hl + 2.4), y: roadY,
      opts: { grades, hoses: grades.length, wear: cfg.wear, lit: cfg.lit } });
    let storeD = 0;
    if (cfg.store) {
      const B = shell(cfg.store); storeD = B.d;
      pieces.push({ type: 'store', size: cfg.store, x: cfg.storeX != null ? cfg.storeX : 0, y: backY - B.hd,
        opts: { grades, open: cfg.open, wear: cfg.wear, lit: cfg.lit } });
    }
    if (cfg.fill !== false) {
      pieces.push({ type: 'fillport', size: 'sCluster', x: hl + 0.6, y: backY + 1.0,
        opts: { grades, hoses: grades.length, wear: cfg.wear, lit: cfg.lit } });
      pieces.push({ type: 'fillport', size: 'sVent', x: hl + 2.9, y: backY + 0.2,
        opts: { grades, hoses: grades.length, wear: cfg.wear, lit: cfg.lit } });
    }
    const ext = { x0: -hl - 4.0, x1: hl + 4.2, y0: backY - storeD - 1.2, y1: roadY + 1.5 };
    return { pieces, extent: ext, grades, slots: maxSlots, rows: rows.length,
             lanes: rows.map((_, r) => (r - (rows.length - 1) / 2) * 7.40) };
  }
  /* Painter order: layer first (a canopy is always over its own island), then far-to-near on the
     SAME depth the rasteriser uses inside a cell, so a station never disagrees with itself. */
  function sortPieces(pieces, dir, elev) {
    return pieces.slice().map((p, i) => {
      const d = project(p.x, p.y, 0, dir, elev).depth;
      return { p, i, d, l: LAYER[p.type] != null ? LAYER[p.type] : 2 };
    }).sort((a, b) => (a.l - b.l) || (b.d - a.d) || (a.i - b.i)).map((e) => e.p);
  }

  // ---- filling -------------------------------------------------------------
  /* The hose. `from`/`to` are METRE points on the forecourt; the returned polyline is in canvas
     pixels (origin + zoom applied), so a caller draws it with one stroke. Slack grows as the pull
     shortens — a nozzle held close hangs a deep bight, one at full stretch goes taut — and `inReach`
     is honest about the metres, not about the pixels. */
  function serve(from, to, opts) {
    opts = opts || {};
    const dir = opts.dir || 0, elev = opts.elev, zoom = opts.zoom || 1, o = opts.origin || { x: 0, y: 0 };
    const a = project(from.x, from.y, from.z || 0, dir, elev), b = project(to.x, to.y, to.z || 0, dir, elev);
    const A = { x: o.x + a.x * zoom, y: o.y + a.y * zoom }, B = { x: o.x + b.x * zoom, y: o.y + b.y * zoom };
    const reach = opts.reach || reachOf(opts.size);
    const m = Math.hypot(to.x - from.x, to.y - from.y, (to.z || 0) - (from.z || 0));
    const taut = Math.max(0, Math.min(1, m / reach));
    const pts = FU.hosePath(A, B, { seg: opts.seg || 26, slack: 0.10 + 0.46 * (1 - taut) * (1 - taut) });
    return { pts, metres: +m.toFixed(2), reach, taut: +taut.toFixed(2), inReach: m <= reach + 1e-6, from: A, to: B };
  }
  /* What the counter shows. Litres, money and minutes off ONE table: the grade's price and the
     machine's flow rate. A 205 L drum is five and a half minutes on a kerb post and ninety seconds
     on high-flow, and that difference is the whole reason the truck pump exists. */
  function ticket(size, grade, litres) {
    const g = GRADES[grade] || GRADES.gas, lpm = flowOf(size), L = Math.max(0, litres || 0);
    const cents = (PRICE[grade] != null ? PRICE[grade] : 0) * L;
    return { grade, label: g.label, litres: +L.toFixed(1), lpm,
             price: +(cents / 100).toFixed(2), perL: PRICE[grade], kg: +(L * g.dens).toFixed(1),
             minutes: +(L / lpm).toFixed(2), seconds: Math.round(L / lpm * 60) };
  }
  /* Capacity of anything in the fuel kit, so "fill it" is one call for a jerry can or a bulk tank. */
  function vesselFill(vType, vSize, fromFill, toFill, opts) {
    const T = FU.TYPES[vType]; if (!T) return null;
    const size = FU.resolveSize(vType, vSize), Z = T.sizes[size];
    const a = Math.max(0, Math.min(1, fromFill != null ? fromFill : 0)), b = Math.max(a, Math.min(1, toFill != null ? toFill : 1));
    const litres = Z.L * (b - a);
    const t = ticket((opts && opts.size) || 'sTwin', (opts && opts.grade) || 'gas', litres);
    return Object.assign({ vessel: T.label + ' ' + Z.label, capacity: Z.L, from: a, to: b }, t);
  }

  const ORDER = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];

  // ---- gameplay sidecars ---------------------------------------------------
  /* One block per piece, in METRES, on the piece's own frame: origin at the ground centre of the
     footprint, +x and +y as authored, +z up. Heading-independent, so the azimuth the baker chose does
     not touch these numbers.

     `visible_facings` is COMPUTED with the rasteriser's own camera-facing test rather than a guess:
     a face with outward normal (nx,ny) is camera-facing at dir d when -nx*sin(45d) + ny*cos(45d) < 0.
     `reach_point` is a REQUEST, not a promise (camper precedent) — a ground-level standing spot beside
     the fitting, untested against any walkable polygon.

     Fixture heights carry a `treatment` so a collider author does not have to guess what 0.17 m means:
     flat / step_over / waist_block / wall. */
  const F2 = (n) => +(+n).toFixed(2);
  const P3 = (p) => [F2(p[0]), F2(p[1]), F2(p[2])];
  const treatOf = (h) => (h <= 0.14 ? 'flat' : h <= 0.55 ? 'step_over' : h <= 1.20 ? 'waist_block' : 'wall');
  const facesAt = (nx, ny, d) => (-nx * Math.sin(d * Math.PI / 4) + ny * Math.cos(d * Math.PI / 4)) < -0.05;
  const visFacings = (nx, ny) => { const o = []; for (let d = 0; d < 8; d++) if (facesAt(nx, ny, d)) o.push(ORDER[d]); return o; };
  const rect = (x0, x1, y0, y1) => [[F2(x0), F2(y0)], [F2(x1), F2(y0)], [F2(x1), F2(y1)], [F2(x0), F2(y1)]];

  function gameplayBlock(type, size, opts) {
    opts = opts || {};
    size = resolveSize(type, size);
    const Z = TYPES[type].sizes[size], s = resolve(type, size, opts);
    const grades = (opts.grades && opts.grades.length ? opts.grades : ['gas', 'diesel', 'mixed']).slice(0, 4);
    const gAt = (i) => grades[Math.min(i, grades.length - 1)];
    const c = cell(type, size, opts);
    const out = { type, size, label: Z.label, cell: { W: c.W, H: c.H }, pivot: { x: c.cx, y: c.cy },
                  grades_in_service: grades.slice() };
    const G = geo(type, size, opts);
    if (type === 'dispenser') {
      const hw = Z.w / 2, hd = Z.d / 2;
      out.footprint = { w: Z.w, d: Z.d, h: Z.h };
      out.machine = { family: Z.fam, hoses: s.n, sides: s.sides, reach_m: Z.reach, lpm: Z.lpm, tare_kg: Z.tare };
      out.collider_bbox = [P3([-hw - 0.14, -hd - 0.14, 0]), P3([hw + 0.14, hd + 0.14, Z.h])];
      out.HOSES = []; out.INTERACT = [];
      for (let side = 0; side < s.sides; side++) {
        const tag = side ? 'B' : 'A', sx = side ? -1 : 1;
        for (let i = 0; i < s.n; i++) {
          const h = (G.mounts || {})['h' + tag + i], b = (G.mounts || {})['b' + tag + i];
          if (!h) continue;
          out.HOSES.push({ id: tag.toLowerCase() + i, side: side ? 'b' : 'a', index: i, grade: gAt(i),
            outlet: P3(h), boot: b ? P3(b) : null, reach_m: Z.reach, lpm: Z.lpm,
            curve: 'StationIso.serve(outlet, target) — polyline, slack grows as the pull shortens' });
          out.INTERACT.push({ id: 'nozzle_' + tag.toLowerCase() + i, label: 'HOSE ' + tag + (i + 1) + ' · ' + GRADES[gAt(i)].label,
            action: 'fuel', grade: gAt(i), pos: P3(h),
            reach_point: P3([h[0] + sx * 0.62, h[1] - 0.34, 0]),
            visible_facings: visFacings(sx, 0), level: 'apron' });
        }
      }
      if (Z.fam === 'slab' || Z.fam === 'lock' || Z.fam === 'brick') {
        for (let side = 0; side < s.sides; side++) {
          const sy = side ? -1 : 1;
          out.INTERACT.push({ id: 'pay_' + (side ? 'b' : 'a'), label: 'PAY AT PUMP', action: 'pay',
            pos: P3([Z.fam === 'lock' ? 0 : hw * 0.52, sy * (hd + 0.03), Z.h * (Z.fam === 'lock' ? 0.90 : 0.44)]),
            reach_point: P3([Z.fam === 'lock' ? 0 : hw * 0.52, sy * (hd + 0.75), 0]),
            visible_facings: visFacings(0, sy), level: 'apron' });
        }
      }
      out.BLOCKERS = [{ shape: 'box', footprint: rect(-hw, hw, -hd, hd), height_above_grade_m: Z.h, treatment: 'wall', kind: 'cabinet' },
                      { shape: 'box', footprint: rect(-hw - 0.14, hw + 0.14, -hd - 0.14, hd + 0.14), height_above_grade_m: 0.10, treatment: 'flat', kind: 'plinth' }];
      out._excluded = { WALK: 'a dispenser has no walkable surface', HOSE_PIXELS: 'the hose is a runtime polyline, never baked' };
      return out;
    }
    if (type === 'island') {
      const len = islandLen(Z.slots), hl = len / 2, hw = 0.575, h = 0.165;
      out.footprint = { w: F2(len), d: 1.15, h };
      out.WALK = [{ id: 'island_top', winding: 'ccw_from_above', z: h, surface: 'concrete',
        polygon: rect(-hl + 0.05, hl - 0.05, -hw + 0.05, hw - 0.05),
        step_up_m: h, treatment: treatOf(h),
        note: 'A kerb, not a floor: a body steps up onto it to reach a boot. Slot plates are flush.' }];
      out.SLOTS = slotXs(Z.slots).map((x, i) => ({ id: 'slot' + (i + 1), pos: P3([x, 0, h]), pitch_m: SLOT_PITCH,
        note: 'bolt-down plate — any dispenser variety mounts here at its own pivot' }));
      out.BLOCKERS = [{ shape: 'box', footprint: rect(-hl, hl, -hw, hw), height_above_grade_m: h, treatment: 'step_over', kind: 'kerb' }];
      if (s.bollards) for (const sx of [-1, 1])
        out.BLOCKERS.push({ shape: 'circle', pos: P3([sx * (hl - 0.28), 0, h]), r: 0.11, height_above_grade_m: F2(h + 0.98), treatment: 'wall', kind: 'bollard' });
      if (s.bin) out.BLOCKERS.push({ shape: 'box', footprint: rect(-(hl - 1.15), -(hl - 0.75), -0.20, 0.20), height_above_grade_m: F2(h + 0.62), treatment: 'waist_block', kind: 'bin' });
      out.INTERACT = s.bin ? [{ id: 'bin', label: 'WASTE BIN', action: 'discard', pos: P3([-(hl - 0.95), 0, h + 0.60]),
        reach_point: P3([-(hl - 0.95), 1.05, 0]), visible_facings: ORDER.slice(), level: 'apron' }] : [];
      return out;
    }
    if (type === 'canopy') {
      const len = Z.bays * SLOT_PITCH + 2.30, hl = len / 2;
      const wid = s.span > 1 ? (s.span - 1) * 7.40 + 5.80 : 5.80, hwid = wid / 2;
      const clear = 4.55, deck = 0.17;
      out.footprint = { w: F2(len), d: F2(wid), h: F2(clear + deck + (s.style === 'tin' ? 1.15 : 0.62)) };
      out.CLEAR = { height_m: clear, deck_z: F2(clear + deck), style: s.style, bays: Z.bays, span: s.span,
        covers: rect(-hl, hl, -hwid, hwid),
        note: 'Clear height is to the underside. A 4.55 m soffit passes anything in the vehicle rig; the fascia is the lowest edge and it is outboard of the kerb.' };
      out.POSTS = [];
      const rowY = Array.from({ length: s.span }, (_, r) => F2((r - (s.span - 1) / 2) * 7.40));
      for (const sx of [-1, 1]) for (const y of rowY)
        out.POSTS.push({ pos: P3([sx * (hl - 1.15), y, 0]), size: [0.27, 0.27], footing: [0.68, 0.68],
          note: 'lands on the island kerb' });
      out.BLOCKERS = out.POSTS.map((p, i) => ({ shape: 'box', kind: 'post', footprint: rect(p.pos[0] - 0.135, p.pos[0] + 0.135, p.pos[1] - 0.135, p.pos[1] + 0.135),
        height_above_grade_m: clear, treatment: 'wall' }));
      out.LAMPS = [];
      for (const y of rowY) for (let i = 0; i < Z.bays; i++)
        out.LAMPS.push({ pos: P3([F2((i - (Z.bays - 1) / 2) * SLOT_PITCH), y, clear - 0.02]), kind: s.style === 'tin' ? 'pendant' : 'soffit_panel' });
      out._excluded = { WALKABLE_ROOF: 'no roof access is modelled — no ladder, no hatch, no parapet to stand behind',
        WALK: 'nothing on a canopy is walkable' };
      return out;
    }
    if (type === 'sign') {
      const top = Z.poleH + Z.ph;
      out.footprint = { w: Z.pw, d: Z.poleH > 0 ? 0.30 : 0.62, h: F2(top) };
      out.ROWS = grades.map((k, i) => ({ row: i + 1, grade: k, label: GRADES[k].label, cents_per_L: PRICE[k] != null ? PRICE[k] : null,
        note: 'the row order IS the grade list order' }));
      out.INTERACT = [{ id: 'prices', label: 'PRICE SIGN', action: 'read', pos: P3([0, 0, F2(top - 0.4)]),
        reach_point: P3([0, 1.4, 0]), visible_facings: visFacings(0, 1).concat(visFacings(0, -1)),
        note: 'the panel is double-sided; both faces carry the same rows', level: 'apron' }];
      out.BLOCKERS = Z.poleH > 0
        ? [{ shape: 'box', kind: 'pole', footprint: rect(-0.14, 0.14, -0.12, 0.12), height_above_grade_m: F2(Z.poleH), treatment: 'wall' },
           { shape: 'circle', kind: 'base', pos: P3([0, 0, 0]), r: 0.42, height_above_grade_m: 0.16, treatment: 'step_over' }]
        : [{ shape: 'box', kind: 'a_frame', footprint: rect(-Z.pw / 2, Z.pw / 2, -0.32, 0.32), height_above_grade_m: F2(Z.ph), treatment: 'waist_block',
            note: 'the only sign here a player could knock over' }];
      return out;
    }
    if (type === 'fillport') {
      if (Z.pad) {
        const hp = Z.pad / 2, cols = Math.min(2, s.n), rows = Math.ceil(s.n / cols);
        out.footprint = { w: Z.pad, d: Z.pad, h: 0.10 };
        out.WALK = [{ id: 'fill_pad', winding: 'ccw_from_above', z: 0.09, surface: 'concrete',
          polygon: rect(-hp, hp, -hp, hp), treatment: 'flat', note: 'flush with the apron — drive over it' }];
        out.INTERACT = []; out.CAPS = [];
        for (let i = 0; i < s.n; i++) {
          const cx = F2((i % cols - (cols - 1) / 2) * (hp * 0.92)), cy = F2((Math.floor(i / cols) - (rows - 1) / 2) * (hp * 0.92));
          out.CAPS.push({ id: 'cap_' + grades[Math.min(i, grades.length - 1)], grade: gAt(i), pos: P3([cx, cy, 0.09]), r: 0.20 });
          out.INTERACT.push({ id: 'fill_' + gAt(i), label: 'TANK FILL · ' + GRADES[gAt(i)].label, action: 'tanker_fill', grade: gAt(i),
            pos: P3([cx, cy, 0.09]), reach_point: P3([cx, cy + 1.1, 0]), visible_facings: ORDER.slice(), level: 'apron' });
        }
        out._excluded = { BLOCKERS: 'nothing here stands proud enough to block — the caps are flush by design' };
      } else {
        out.footprint = { w: F2(0.56 + s.n * 0.28), d: 0.52, h: F2(Z.h + 0.10) };
        out.VENTS = Array.from({ length: s.n }, (_, i) => ({ id: 'vent_' + gAt(i), grade: gAt(i),
          pos: P3([F2((i - (s.n - 1) / 2) * 0.28), 0, Z.h + 0.08]), height_m: Z.h,
          note: 'one riser per tank; the tag at the top carries the grade colour' }));
        out.BLOCKERS = [{ shape: 'box', kind: 'risers', footprint: rect(-(0.28 + s.n * 0.14), 0.28 + s.n * 0.14, -0.26, 0.26),
          height_above_grade_m: F2(Z.h), treatment: 'wall' }];
        out._excluded = { INTERACT: 'a vent is not interactable — the fill cluster is' };
      }
      return out;
    }
    // ---- storefront: two rigs write this one -------------------------------
    const B = shell(size), dm = doorMount(size, 3, opts);
    out.footprint = { w: Z.w, d: Z.d, h: Z.h };
    out.collider_bbox = [P3([-B.hw, -B.hd, 0]), P3([B.hw, B.hd + (B.tower ? B.tower.proud : 0), B.tower ? B.tower.top : Z.h])];
    out.SHELL = { wall_m: B.wall, floor_z: B.floorZ, clear_m: B.clear, ceiling_z: F2(B.ceilZ), roof_z: F2(B.roofZ),
      inner: [F2(B.inner.hw * 2), F2(B.inner.hd * 2)], front: '+y', published_as: 'StationIso.shell(size)' };
    out.THRESHOLD = { kind: B.door.kind, clear_w_m: B.door.w, clear_h_m: B.door.h,
      threshold: P3([B.door.cx, B.door.y, B.floorZ]), leaf_w_m: B.door.leaf, travel_m: F2(B.door.travel),
      clear_at_open_fraction: dm.clearAt, sill_step_m: B.floorZ, treatment: treatOf(B.floorZ),
      keep_clear: null,
      note: 'Bipart slider: no swept arc, so there is no keep_clear volume — the collider is the leaf itself, which travels inside the wall line. Doors default SHUT (owner ruling, 2026-08-19).' };
    out.SERVICE_DOOR = { kind: B.svc.kind, clear_w_m: B.svc.w, clear_h_m: B.svc.h, hinge: B.svc.hinge,
      threshold: P3([B.svc.cx, B.svc.y, B.floorZ]), sill_step_m: B.floorZ, treatment: treatOf(B.floorZ),
      keep_clear: { shape: 'box', footprint: rect(B.svc.cx - B.svc.w / 2, B.svc.cx + B.svc.w / 2 + 0.10, -B.hd - B.svc.w - 0.10, -B.hd),
        note: 'a single leaf swinging OUT does sweep — unlike the entry, this one owns a keep_clear' },
      note: 'Back of house. Staff only; it is not a second way in for a customer.' };
    out.WALK = [{ id: 'walkway', winding: 'ccw_from_above', z: 0.116, surface: 'concrete',
      polygon: rect(-B.hw - 0.58, B.hw + 0.58, B.hd + 0.10, B.hd + 0.10 + (B.kiosk ? 0.95 : 1.65)),
      treatment: 'step_over', note: 'the walk outside the glass; the painted edge is its outer limit' },
      { id: 'service_stoop', winding: 'ccw_from_above', z: 0.124, surface: 'concrete',
        polygon: rect(B.svc.cx - B.svc.w / 2 - 0.34, B.ladder ? B.ladder.x + 0.34 : B.svc.cx + B.svc.w / 2 + 0.34, -B.hd - B.svc.stoop, -B.hd),
        treatment: 'step_over', note: B.ladder ? 'the pad outside the service door; it runs on to the ladder foot at its +x end'
          : 'the pad outside the service door' }];
    if (B.roofWalk) {
      const RW = B.roofWalk, ri = RW.inset;
      out.WALK.push({ id: 'roof_deck', winding: 'ccw_from_above', z: F2(B.roofZ), surface: 'membrane',
        polygon: rect(-B.hw + ri, B.hw - ri, -B.hd + ri, B.hd - ri),
        treatment: 'flat', reached_by: 'roof_ladder', fall_edge: 'parapet, ' + RW.parapet.toFixed(2) + ' m — a kerb, not a guard',
        note: 'Inside face of the parapet. Plant and its curbs are published as roof-level blockers, not as holes in this polygon.' });
    }
    out.ROOF = { z: F2(B.roofZ), parapet_z: F2(Z.h), plant: B.kiosk ? 1 : 2, walkable: !!B.ladder,
      access: B.ladder ? { kind: 'fixed_ladder_caged', foot: P3([B.ladder.x, B.ladder.y, B.ladder.base]),
        head: P3([B.ladder.x, B.ladder.y, B.ladder.top]), cage_from_m: B.ladder.cageFrom, standoff_m: 0.26,
        note: 'stringers run past the coping and turn inboard as grab rails — the crest is the step-off' } : null,
      note: B.ladder ? 'Plant sits on curbs on the deck; the deck is walkable and the ladder is how a body gets there.'
                     : 'Plant sits on curbs on the deck. Not walkable and not reachable — a booth gets no ladder.' };
    out.ATTACH = [{ id: 'sign_band', pos: P3([0, B.hd + 0.09, F2((B.band.z0 + B.band.z1) / 2)]), kind: 'illuminated band', grade_colour: gAt(0) }];
    if (!B.kiosk) {
      out.ATTACH.push({ id: 'ice', pos: P3([B.hw + 0.94, -B.hd * 0.10, B.floorZ + 0.90]), kind: 'ice merchandiser' });
      out.ATTACH.push({ id: 'propane', pos: P3([B.hw + 0.94, -B.hd * 0.66, B.floorZ + 0.80]), kind: 'propane exchange cage' });
      out.INTERACT = [
        { id: 'ice_box', label: 'ICE', action: 'buy_ice', pos: P3([B.hw + 0.88, -B.hd * 0.10, B.floorZ + 0.90]),
          reach_point: P3([B.hw + 1.55, -B.hd * 0.10, 0]), visible_facings: visFacings(1, 0), level: 'apron' },
        { id: 'propane_cage', label: 'PROPANE EXCHANGE', action: 'buy_propane', pos: P3([B.hw + 0.88, -B.hd * 0.66, B.floorZ + 0.80]),
          reach_point: P3([B.hw + 1.55, -B.hd * 0.66, 0]), visible_facings: visFacings(1, 0), level: 'apron' },
      ];
      out.BLOCKERS = [{ shape: 'box', kind: 'building', footprint: rect(-B.hw, B.hw, -B.hd, B.hd), height_above_grade_m: Z.h, treatment: 'wall' },
        { shape: 'box', kind: 'ice', footprint: rect(B.hw + 0.02, B.hw + 0.86, -B.hd * 0.10 - 0.62, -B.hd * 0.10 + 0.62), height_above_grade_m: F2(B.floorZ + 1.44), treatment: 'wall' },
        { shape: 'box', kind: 'propane_cage', footprint: rect(B.hw + 0.06, B.hw + 0.86, -B.hd * 0.66 - 0.44, -B.hd * 0.66 + 0.44), height_above_grade_m: F2(B.floorZ + 1.66), treatment: 'wall' }];
    } else {
      out.INTERACT = [];
      out.BLOCKERS = [{ shape: 'box', kind: 'building', footprint: rect(-B.hw, B.hw, -B.hd, B.hd), height_above_grade_m: Z.h, treatment: 'wall' }];
    }
    out.INTERACT.push({ id: 'service_door', label: 'SERVICE DOOR', action: 'staff_door',
      pos: P3([B.svc.cx, -B.hd - 0.06, B.floorZ + 1.04]),
      reach_point: P3([B.svc.cx - B.svc.w / 2 - 0.30, -B.hd - 0.55, 0.124]),
      visible_facings: visFacings(0, -1), level: 'service_stoop',
      note: 'hinge side, clear of the leaf\u2019s own keep_clear \u2014 a body does not stand in the swing of the door it is opening' });
    if (B.ladder) {
      out.INTERACT.push({ id: 'roof_ladder', label: 'ROOF LADDER', action: 'climb',
        pos: P3([B.ladder.x, B.ladder.y, 1.20]),
        reach_point: P3([B.ladder.x, B.ladder.y - 0.58, 0.124]),
        lands_at: P3([B.ladder.x, -B.hd + B.roofWalk.inset + 0.30, B.roofZ]),
        visible_facings: visFacings(0, -1), level: 'service_stoop',
        note: 'climb is a two-surface move: stoop at the foot, roof_deck at the head' });
      out.BLOCKERS.push({ shape: 'box', kind: 'ladder_cage',
        footprint: rect(B.ladder.x - B.ladder.w / 2 - 0.11, B.ladder.x + B.ladder.w / 2 + 0.11, B.ladder.y - 0.36, -B.hd),
        height_above_grade_m: F2(Z.h), treatment: 'wall' });
      out.BLOCKERS.push({ shape: 'box', kind: 'plant', level: 'roof',
        footprint: rect(-B.hw * 0.44 - 0.80, -B.hw * 0.44 + 0.80, -B.hd * 0.34 - 0.61, -B.hd * 0.34 + 0.61),
        height_above_grade_m: 0.49, treatment: 'waist_block', note: 'heights on the roof are above the DECK, not the ground' });
      out.BLOCKERS.push({ shape: 'box', kind: 'plant', level: 'roof',
        footprint: rect(B.hw * 0.30 - 0.68, B.hw * 0.30 + 0.68, -B.hd * 0.42 - 0.54, -B.hd * 0.42 + 0.54),
        height_above_grade_m: 0.45, treatment: 'waist_block' });
      out.BLOCKERS.push({ shape: 'circle', kind: 'flue', level: 'roof',
        pos: P3([B.hw * 0.66, B.hd * 0.30, 0]), r: 0.12, height_above_grade_m: 0.60, treatment: 'waist_block' });
    }
    const SI = root.StationInterior;
    if (SI) {
      const sec = SI.gameplaySections(size);
      out.SOLE = sec.SOLE;
      out.INTERACT = out.INTERACT.concat(sec.INTERACT);
      out.interior = { rig: 'Art/stationInteriorRig.js', exposes: 'globalThis.StationInterior',
        cell: 'identical to this piece — StationInterior paints into StationIso.cell("store", size)',
        sectionCut: 'per facing; a wall whose outside is camera-facing is not drawn',
        rock: 'none — a building ashore does not move' };
    } else {
      out._confirm = { SOLE: 'stationInteriorRig.js was not loaded when this block was generated — the sales floor and its interactables are missing. Load it and re-run.' };
    }
    out._excluded = Object.assign(B.ladder ? {} : { WALKABLE_ROOF: 'a pay booth gets no ladder, so its deck stays unreachable' },
      { GLAZING_SEE_THROUGH: 'the shell\u2019s glass is opaque by design; the interior is its own sprite in the same cell',
        ROOF_HATCH: 'none — the ladder crests the parapet, which is the cheaper and more common detail' });
    return out;
  }

  /* ---- reach audit ---------------------------------------------------------
     First pass shipped reach_point as a REQUEST: a plausible standing spot, untested. This makes it a
     TESTED one. Every interactable is checked against its OWN piece — the surface it claims to stand
     on, the blockers at that level, and how far a standing body leans. A point inside a blocker is
     marched straight out along the fitting→point line in 6 cm steps until it clears; a point that
     cannot clear inside arm’s reach is published as null with a reason, because a wrong standing spot
     costs a collider author more than a missing one. Nothing here guesses at neighbours: a pump on an
     island does not know the island is under it, so the audit is per piece and says so. */
  const BODY_R = 0.22;
  const REACH = { grasp: 1.20, read: 1.90, high: 2.10, low: -0.20, step: 0.06 };
  const armFor = (a) => (a === 'read' || a === 'pay' ? REACH.read : REACH.grasp);
  function inPoly(p, poly) {
    let c = false;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
      const a = poly[i], b = poly[j];
      if (((a[1] > p[1]) !== (b[1] > p[1])) && (p[0] < (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0])) c = !c;
    }
    return c;
  }
  /* Footprints in this kit are axis-aligned rectangles, so a body of radius r is tested by inflating
     the bbox rather than by offsetting the polygon. */
  const inflated = (poly, r) => {
    const xs = poly.map((p) => p[0]), ys = poly.map((p) => p[1]);
    const x0 = Math.min(...xs) - r, x1 = Math.max(...xs) + r, y0 = Math.min(...ys) - r, y1 = Math.max(...ys) + r;
    return [[x0, y0], [x1, y0], [x1, y1], [x0, y1]];
  };
  const hits = (p, b, r) => {
    const h = b.height_above_grade_m != null ? b.height_above_grade_m : b.height_above_sole_m;
    if (h != null && h <= 0.14) return false;                       // flat enough to stand on
    if (b.shape === 'circle' && b.pos) return Math.hypot(p[0] - b.pos[0], p[1] - b.pos[1]) < b.r + r;
    return b.footprint ? inPoly(p, r > 0 ? inflated(b.footprint, r) : b.footprint) : false;
  };
  function auditReach(out) {
    const items = out.INTERACT || [];
    if (!items.length) return out;
    const roofPolys = (out.WALK || []).filter((w) => w.id === 'roof_deck');
    const interiorSolids = ((out.SOLE || [])[0] || {})._notes || [];
    const sole = ((out.SOLE || [])[0] || {}).polygon;
    const tally = { checked: 0, ok: 0, moved: 0, failed: 0 };
    for (const it of items) {
      const req = it.reach_point;
      if (!req) continue;
      tally.checked++;
      const onRoof = it.level === 'roof_deck';
      const inside = it.level === 'sales_floor';
      const solids = inside ? interiorSolids
        : (out.BLOCKERS || []).filter((b) => (b.level === 'roof') === onRoof);
      const arm = armFor(it.action);
      /* A body stands right up AGAINST the thing it is using. The fixture the fitting sits on is its
         host, and a host is tested at its true footprint rather than inflated by the body — otherwise
         a cooler door can never be opened by anyone. Everything else keeps the 0.22 m standoff. */
      const isHost = (b) => !!(b.footprint && inPoly(it.pos, b.footprint));
      let dx = req[0] - it.pos[0], dy = req[1] - it.pos[1], L = Math.hypot(dx, dy);
      if (L < 1e-6) { dx = 0; dy = 1; L = 1e-6; } else { dx /= L; dy /= L; }
      /* Try the direction the rig asked for first. If every step of it is inside something, try the
         same line the other way, then the four axes: a standing spot facing INTO the cooler it serves
         is a sign error, not a dead fitting, and flipping it is the honest repair. */
      const dirs = [{ d: [dx, dy], tag: 'as_asked', from: L }, { d: [-dx, -dy], tag: 'flipped', from: BODY_R },
        { d: [0, 1], tag: 'relocated', from: BODY_R }, { d: [0, -1], tag: 'relocated', from: BODY_R },
        { d: [1, 0], tag: 'relocated', from: BODY_R }, { d: [-1, 0], tag: 'relocated', from: BODY_R }];
      let got = null;
      for (const c of dirs) {
        for (let t = Math.max(c.from, BODY_R); t <= arm + 1e-6; t += REACH.step) {
          const p = [it.pos[0] + c.d[0] * t, it.pos[1] + c.d[1] * t, req[2]];
          const surfaceOK = inside ? (!sole || inPoly(p, sole))
            : onRoof ? roofPolys.some((w) => inPoly(p, w.polygon)) : true;
          if (surfaceOK && !solids.some((b) => hits(p, b, isHost(b) ? 0 : BODY_R))) {
            got = { p: P3(p), dist: F2(t), tag: c.tag, moved: F2(Math.hypot(p[0] - req[0], p[1] - req[1])) };
            break;
          }
        }
        if (got) break;
      }
      const lift = F2(it.pos[2] - req[2]);
      /* A sign is READ, not touched. Line of sight is the whole requirement, so the vertical test is
         only run on the actions where a hand has to arrive. */
      const handed = it.action !== 'read';
      if (!got) {
        it.reach_point = null;
        it.reach = { tested: true, verdict: 'no_clear_spot', body_r_m: BODY_R, arm_m: arm,
          reason: 'every point between the fitting and arm\u2019s reach is inside a blocker on this piece' };
        tally.failed++;
      } else {
        it.reach_point = got.p;
        const far = handed && (lift > REACH.high || lift < REACH.low);
        it.reach = { tested: true, verdict: far ? 'out_of_vertical_reach' : got.tag !== 'as_asked' ? got.tag : got.moved > 0.005 ? 'moved' : 'ok',
          on: it.level, dist_m: got.dist, moved_m: got.moved, lift_m: lift, body_r_m: BODY_R, arm_m: arm };
        if (got.tag === 'flipped') it.reach.reason = 'the requested side faced into the fixture; the standing spot is on the other side of the fitting';
        if (got.tag === 'relocated') it.reach.reason = 'neither side of the requested line cleared; the spot was taken to the nearest open axis';
        if (!handed) it.reach.note = 'read at distance — line of sight, not arm\u2019s length, so no vertical test';
        if (far) { it.reach.reason = 'the fitting is ' + lift + ' m above the standing spot; a standing body reaches ' + REACH.high + ' m'; tally.failed++; }
        else if (got.tag !== 'as_asked' || got.moved > 0.005) tally.moved++; else tally.ok++;
      }
    }
    out.REACH_AUDIT = Object.assign({ body_r_m: BODY_R, arm_grasp_m: REACH.grasp, arm_read_m: REACH.read,
      vertical_reach_m: REACH.high, scope: 'this piece only — a dispenser is not told the island under it' }, tally);
    return out;
  }

  function gameplay(type, size, opts) { return auditReach(gameplayBlock(type, size, opts)); }

  /* The whole kit in one file: every type, every size, on one contract. This is what a sidecar for the
     station IS — there is no single "station" object to describe, only the pieces a forecourt is
     assembled from, plus the plan that places them. */
  function gameplayAll(opts) {
    opts = opts || {};
    const grades = opts.grades || ['gas', 'diesel', 'mixed'];
    const out = {
      rig: 'Art/gasStationRig.js', exportSymbol: 'globalThis.StationIso',
      interiorRig: 'Art/stationInteriorRig.js', gradeRig: 'Art/fuelRig.js',
      contract: {
        unit: '32 px = 1 m', camera: '3/4, 45deg steps, elev 40deg (shared with the fleet, houses, characters, fuel kit)',
        origin: 'ground-centre of each piece\u2019s own footprint',
        axes: '+x, +y as authored per piece; +z up. Storefront front is +y.',
        headingIndependent: true, keyline: false,
        oneRule: 'A station is a LIST OF GRADE POSITIONS. Valance stripes, boot cups, sign rows and apron fill caps are all generated from it — nothing is drawn twice.',
        grades: 'imported by reference from FuelIso; stove oil is added here',
        hose: 'never baked — StationIso.serve() returns a polyline plus honest metres',
        placement: 'StationIso.plan(cfg) lays a forecourt out in metres; sortPieces() orders it far-to-near on the rasteriser\u2019s own depth',
        visibleFacings: 'computed: -nx*sin(45d) + ny*cos(45d) < 0',
        reachPoint: 'TESTED, not requested: every point is checked against its own piece\u2019s blockers and its claimed surface, marched clear if it fails, and published as null with a reason if nothing inside arm\u2019s reach works. Cross-piece placement is still the caller\u2019s problem.',
      },
      grades: {}, price: Object.assign({}, PRICE), slotPitch: SLOT_PITCH, pieces: [],
    };
    for (const k of GRADE_ORDER) out.grades[k] = { label: GRADES[k].label, dens: GRADES[k].dens, hex: GRADES[k].hex, cents_per_L: PRICE[k] };
    for (const t of TYPE_ORDER) for (const sz of sizesOf(t))
      out.pieces.push(gameplay(t, sz, Object.assign({}, opts, { grades })));
    const roll = { checked: 0, ok: 0, moved: 0, failed: 0 };
    for (const p of out.pieces) if (p.REACH_AUDIT) for (const k of Object.keys(roll)) roll[k] += p.REACH_AUDIT[k] || 0;
    out.reach_audit = Object.assign({ body_r_m: BODY_R, arm_grasp_m: REACH.grasp, arm_read_m: REACH.read, vertical_reach_m: REACH.high }, roll,
      { note: 'Every interactable in the kit was run through StationIso.auditReach at bake time. `moved` means the first-pass request sat inside a blocker and was marched clear; `failed` means it is published as null with a reason.' });
    return out;
  }

  function contract() {
    const out = { S, elev: DEFAULT_ELEV, dirs: ORDER.slice(), keyline: false, slotPitch: SLOT_PITCH,
                  grades: {}, price: Object.assign({}, PRICE), types: {} };
    for (const k of GRADE_ORDER) out.grades[k] = { label: GRADES[k].label, dens: GRADES[k].dens, hex: GRADES[k].hex };
    for (const t of TYPE_ORDER) {
      out.types[t] = { label: TYPES[t].label, sizes: {} };
      for (const s of sizesOf(t)) {
        const Z = TYPES[t].sizes[s], c = cell(t, s, { grades: ['gas', 'diesel', 'mixed'] });
        const e = { label: Z.label, cell: { W: c.W, H: c.H }, pivot: { x: c.cx, y: c.cy } };
        if (Z.maxHose) { e.hoses = Z.maxHose; e.sides = Z.maxSides; e.reach = Z.reach; e.lpm = Z.lpm; e.fam = Z.fam; }
        if (Z.slots) { e.slots = Z.slots; e.length = +islandLen(Z.slots).toFixed(2); }
        if (Z.bays) e.bays = Z.bays;
        if (Z.clear) { const B = shell(s); e.footprint = [Z.w, Z.d]; e.parapet = Z.h; e.clear = Z.clear; e.door = B.door.w; }
        out.types[t].sizes[s] = e;
      }
    }
    return out;
  }

  root.StationIso = {
    S, DEG, defaultElev: DEFAULT_ELEV, KEY, DIRS: 8, order: ORDER,
    TYPES, TYPE_ORDER, GRADES, GRADE_ORDER, PRICE, SLOT_PITCH, sizesOf, resolveSize,
    ramps: { CHROME, ASPH, YEL, GLOW, SEG },
    islandLen, slotXs, cell, mount, hoseMount, reachOf, flowOf, render, sheet,
    shell, doorMount,
    plan, sortPieces, project, serve, ticket, vesselFill, contract, gameplay, gameplayAll, auditReach,
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
