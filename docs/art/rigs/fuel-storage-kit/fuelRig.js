/* Hidden Harbours — FUEL rig: carry vessels + storage/dispensing plant (ADR-0006 bake on IsoSolid).
   Same fixed 3/4 turntable as the fleet / character / deck gear: 45deg steps, elev 40deg, upper-left
   key, ordered dither, depth-edge darkening, NO AA, 32 px = 1 m, RINGLESS per ADR-0031.

   THE ONE RULE OF THIS KIT — every vessel that holds fuel reads its LEVEL from outside, and it reads
   its GRADE from colour. Three read mechanisms, chosen by what the vessel is actually made of:
     'body'  translucent HDPE — the liquid darkens the whole wall below the surface (jug, jerry, tote)
     'tube'  opaque steel — an external sight glass on a graduated backing plate (drum, skid tank)
     'board' big steel — a float-and-tape gauge board with a travelling pointer (bulk tank)
   Gauges are MIRRORED on the two long sides. A single gauge is hidden on 3 of 8 facings, and a level
   you cannot see from the north is not a level. That is a game-readability call, stated on purpose.

   FOUR GRADES: gas (red) · diesel (amber) · mixed 50:1 premix (blue) · oil (green). Body colour is the
   grade at ALL fills — the pale ramp is dyed plastic, the deep ramp is the same plastic with fuel
   behind it — so an empty red can is still visibly the gas can.

   CARRY  jug · jerry · nozzle — pivotCarry is the GRIP. One per hand: pin to CharacterIso.carry()
          handL/handR and pass opts.swing = carry().swingL/swingR (RADIANS).
   STORE  drum · tote · skid · bulk · pump — pivot is the BASE CENTRE on the ground plane.

   fill is a VOLUME fraction 0..1. The rig solves the surface height itself, so a horizontal-cylinder
   skid tank at fill 0.25 shows its true 40% depth, not a quarter of the way up.

   Exposes globalThis.FuelIso. */
(function (root) {
  const IS = root.IsoSolid;
  if (!IS) throw new Error('FuelIso requires IsoSolid — load Art/isoSolid.js first');
  const { S, DEG, box, bar, tube, prism, rotY, move, chain, camBasis, proj, paint, mulberry } = IS;
  const DEFAULT_ELEV = 40, KEY = '#101a19';
  const rotX = (a) => { const c = Math.cos(a), s = Math.sin(a); return (p) => [p[0], p[1] * c - p[2] * s, p[1] * s + p[2] * c]; };

  // ---- ramps ---------------------------------------------------------------
  const STEEL = ['#3a4148', '#565f66', '#7a858c', '#9fabb1', '#c3ced2'];   // galvanised / bare
  const GALV  = ['#434a4e', '#61686c', '#848b8f', '#a6aeb1', '#c9d0d2'];   // tote cage, brackets
  const GRAPH = ['#101317', '#1d2127', '#2b323a', '#3d454e', '#525c63'];   // hose, grips, boots
  const RUST  = ['#2b1a12', '#42281a', '#5a3823', '#754a2e', '#91603c'];
  const BRASS = ['#463514', '#68501e', '#8b6d2c', '#ac8e46', '#cbb072'];   // fittings, valves, nozzle
  const CONC  = ['#3a3d3a', '#535753', '#6f736c', '#8b8f85', '#a8aca0'];   // pad, berm
  const WHITE = ['#565c5b', '#767c7a', '#949a97', '#b1b7b3', '#ced4cf'];   // painted tank, gauge board
  const GLASS = ['#141c20', '#1e282d', '#2a373d', '#38484f', '#4a5d65'];   // empty sight glass
  const DARK  = ['#0c1114', '#141b1f', '#1d262b', '#273238', '#333f46'];   // recesses, digit windows

  /* Four grades. code = the saturated colour that never lies about what is in the vessel (caps,
     handles, grade bands, gauge pointers). pale = dyed HDPE with air behind it. deep = the same wall
     with fuel behind it. liq = raw liquid in a glass tube, brightest of the three. */
  const FUELS = {
    gas: {
      label: 'GASOLINE', short: 'GAS', dens: 0.745,
      code: ['#4a1410', '#701f18', '#932f22', '#b04a36', '#c9704f'],
      pale: ['#6b5551', '#8b7269', '#a99083', '#c4ae9f', '#dccdbe'],
      deep: ['#2a0c09', '#40140e', '#5c2015', '#7c3020', '#9c4630'],
      liq:  ['#571a12', '#82291b', '#ad3f28', '#cd6446', '#e69068'], hex: '#b2402c',
    },
    diesel: {
      label: 'DIESEL', short: 'DSL', dens: 0.840,
      code: ['#463509', '#684f12', '#8a6c1f', '#a98a36', '#c4a95e'],
      pale: ['#655e42', '#877f59', '#a79e73', '#c3bc94', '#dcd8bb'],
      deep: ['#241905', '#382708', '#513a10', '#6d5019', '#8a6a2c'],
      liq:  ['#553d0c', '#7f5f15', '#a68325', '#c8a844', '#e4cd77'], hex: '#ad8a28',
    },
    mixed: {
      label: 'MIXED 50:1', short: 'MIX', dens: 0.752,
      code: ['#0e2b39', '#164257', '#215d77', '#317f9b', '#4aa0bb'],
      pale: ['#465a62', '#61787f', '#7f989e', '#9db4b9', '#bfd0d3'],
      deep: ['#08202e', '#0f3145', '#184a62', '#256880', '#358aa2'],
      liq:  ['#0f2f3c', '#16485a', '#22687c', '#31899d', '#4aacbd'], hex: '#286f8c',
    },
    oil: {
      label: 'MOTOR OIL', short: 'OIL', dens: 0.885,
      code: ['#112118', '#1a3123', '#264631', '#356043', '#4c7d59'],
      pale: ['#4d5449', '#6a725f', '#88907a', '#a5ad95', '#c2c9b0'],
      deep: ['#1d1408', '#30220c', '#472f12', '#61441c', '#7e5c2b'],
      liq:  ['#2a1a09', '#432a0d', '#5f3d12', '#80571d', '#a2762f'], hex: '#2d573a',
    },
  };
  const FUEL_ORDER = ['gas', 'diesel', 'mixed', 'oil'];

  /* shell decides what the body IS: 'hdpe' takes the dyed-plastic pale ramp and shows its level
     through the wall; 'steel' takes the painted-grade ramp and reads off a gauge instead. */
  const SHELL_DEFAULT = { jug: 'hdpe', jerry: 'hdpe', tote: 'hdpe', nozzle: 'steel', drum: 'steel', skid: 'steel', bulk: 'steel', pump: 'steel' };
  function matsFor(fuel, shell, grade2) {
    const F = FUELS[fuel] || FUELS.gas, G = FUELS[grade2] || F;
    const bodyRamp = shell === 'steel' ? F.code : F.pale;
    return {
      steel: { ramp: STEEL }, galv: { ramp: GALV }, graph: { ramp: GRAPH }, rust: { ramp: RUST },
      brass: { ramp: BRASS }, conc: { ramp: CONC }, white: { ramp: WHITE }, glass: { ramp: GLASS },
      dark: { ramp: DARK },
      body: { ramp: bodyRamp },                  // the shell above the surface
      wet:  { ramp: F.deep },                    // the same wall with fuel behind it
      men:  { ramp: F.liq, off: 2 },             // meniscus — the 1 px surface line
      liq:  { ramp: F.liq, off: 1 },             // liquid in a sight glass
      code: { ramp: F.code },                    // caps, handles, grade bands, pointers
      code2: { ramp: G.code },                   // second grade on a twin dispenser
      liq2: { ramp: G.liq, off: 1 },
    };
  }

  // ---- volume -> surface height (this is why fill is a volume, not a height) ----
  function segFrac(y) { const t = 2 * Math.acos(Math.max(-1, Math.min(1, 1 - 2 * y))); return (t - Math.sin(t)) / (2 * Math.PI); }
  function heightFrac(shape, v) {
    v = Math.max(0, Math.min(1, v));
    if (shape !== 'hcyl') return v;
    let lo = 0, hi = 1;
    for (let i = 0; i < 34; i++) { const m = (lo + hi) / 2; if (segFrac(m) < v) lo = m; else hi = m; }
    return (lo + hi) / 2;
  }

  // ---- shells that show their level ---------------------------------------
  /* A translucent wall, split at the surface. Below is `wet`, above is `body`, and the seam gets a
     bright 0.024 m band — sub-pixel on purpose, so the dither resolves it to the 1 px waterline that
     does all the reading work at 32 px/m. */
  function shellBox(c, h, zf, b, db, tag) {
    const z0 = c[2] - h[2], z1 = c[2] + h[2], zc = Math.max(z0, Math.min(z1, zf)), F = [];
    if (zc > z0 + 0.002) F.push(...box([c[0], c[1], (z0 + zc) / 2], [h[0], h[1], (zc - z0) / 2], 'wet', b, db, null, tag));
    if (z1 > zc + 0.002) F.push(...box([c[0], c[1], (zc + z1) / 2], [h[0], h[1], (z1 - zc) / 2], 'body', b, db, null, tag));
    if (zc > z0 + 0.006 && zc < z1 - 0.004)
      F.push(...box([c[0], c[1], zc], [h[0] + 0.004, h[1] + 0.004, 0.012], 'men', 0.1, (db || 0) - 0.03, null, tag));
    return F;
  }
  /* External sight glass on a graduated backing plate. n = axis: 'y' puts it on a long side, 'x' on
     an end. side = +1/-1. Returns the tube, the plate, quarter ticks and both end fittings. */
  function sightGlass(px, py, z0, z1, zf, n, side, w, F, so) {
    so = so || 0;
    const t = w * 0.42, out = n === 'y' ? [0, side, 0] : [side, 0, 0];
    const P = (dz, hz) => [px + out[0] * (so + 0.012), py + out[1] * (so + 0.012), dz + hz];
    const hx = n === 'y' ? w : t, hy = n === 'y' ? t : w;
    F.push(...box(P((z0 + z1) / 2, 0), [hx, hy, (z1 - z0) / 2 + 0.03], 'white', -0.15, 0));   // backing plate
    for (const bz of [z0 + 0.02, z1 - 0.02])                                                  // standoff brackets
      F.push(...bar([px + out[0] * 0.002, py + out[1] * 0.002, bz], [px + out[0] * (so + 0.014), py + out[1] * (so + 0.014), bz], [w * 0.30, 0.016], 'steel', 0.1, 0));
    const gx = px + out[0] * (so + 0.030), gy = py + out[1] * (so + 0.030);
    const gh = n === 'y' ? w * 0.46 : t * 0.42, gw = n === 'y' ? t * 0.42 : w * 0.46;
    const zc = Math.max(z0, Math.min(z1, zf));
    if (zc > z0 + 0.004) F.push(...box([gx, gy, (z0 + zc) / 2], [gh, gw, (zc - z0) / 2], 'liq', 0.55, -0.05));
    if (z1 > zc + 0.004) F.push(...box([gx, gy, (zc + z1) / 2], [gh, gw, (z1 - zc) / 2], 'glass', -0.4, -0.05));
    if (zc > z0 + 0.01 && zc < z1 - 0.01)
      F.push(...box([gx, gy, zc], [gh + 0.006, gw + 0.006, 0.013], 'men', 0.6, -0.08));
    for (let i = 1; i <= 3; i++) {                                                              // quarter ticks
      const tz = z0 + (z1 - z0) * i / 4, lw = i === 2 ? 1.4 : 0.9;
      const tx = n === 'y' ? w * 0.92 : t * 0.5, ty = n === 'y' ? t * 0.5 : w * 0.92;
      F.push(...box([px + out[0] * (so + 0.021), py + out[1] * (so + 0.021), tz], [tx * lw * 0.6, ty * lw * 0.6, 0.010], 'graph', -0.5, -0.035));
    }
    for (const z of [z0, z1])                                                                    // gland fittings
      F.push(...box([gx, gy, z], [gh + 0.014, gw + 0.014, 0.026], 'brass', 0.3, -0.06));
  }
  /* Float-and-tape gauge board: a painted scale up the tank side with a pointer riding the level.
     This is the industrial read — you check a bulk tank from the far side of the yard. */
  function gaugeBoard(px, py, z0, z1, zf, side, w, F) {
    F.push(...box([px, py + side * 0.030, (z0 + z1) / 2], [w, 0.026, (z1 - z0) / 2], 'white', 0.05, -0.02));
    F.push(...box([px, py + side * 0.046, (z0 + z1) / 2], [w * 0.16, 0.014, (z1 - z0) / 2], 'dark', -0.2, -0.05));  // tape slot
    for (let i = 0; i <= 10; i++) {
      const tz = z0 + (z1 - z0) * i / 10, maj = i % 5 === 0;
      F.push(...box([px + w * (maj ? 0.44 : 0.60), py + side * 0.046, tz], [w * (maj ? 0.34 : 0.18), 0.012, 0.016], 'graph', -0.45, -0.05));
    }
    const zc = Math.max(z0, Math.min(z1, zf));
    F.push(...box([px, py + side * 0.062, zc], [w * 0.86, 0.020, 0.045], 'code', 0.75, -0.09));      // pointer
    F.push(...box([px, py + side * 0.070, zc], [w * 0.30, 0.014, 0.020], 'white', 0.9, -0.12));      // pointer flag
  }
  const disc = (cx, cy, z, r, mat, b, db, F, xf) => F.push(...tube(cx, cy, z - 0.004, z + 0.004, r, r, 12, mat, { caps: 'both', b, db, xf }));

  /* Wear on painted steel is RUN-OFF, not speckle: it starts at a fitting and streaks down. These
     are small boxes standing 4 mm proud of the shell, so a 20-segment lathe never loses a whole
     facet to rust the way a buoy hull can — at 32 px/m one facet of a drum is a third of the drum. */
  function streakCyl(F, r, z0, z1, count, seed, wear) {
    if (!wear || wear === 'fresh') return;
    const n = wear === 'derelict' ? count : Math.max(1, Math.round(count * 0.42)), rnd = mulberry(seed);
    for (let i = 0; i < n; i++) {
      const a = rnd() * Math.PI * 2, t0 = z0 + (z1 - z0) * (0.18 + rnd() * 0.55);
      const len = (z1 - z0) * (0.10 + rnd() * 0.30), w = Math.max(0.016, r * 0.055);
      F.push(...bar([Math.cos(a) * (r + 0.005), Math.sin(a) * (r + 0.005), Math.min(z1, t0 + len)],
                    [Math.cos(a) * (r + 0.005), Math.sin(a) * (r + 0.005), t0], [w, w], 'rust', -0.2, -0.03));
    }
  }
  function streakHCyl(F, r, cz, L, count, seed, wear) {
    if (!wear || wear === 'fresh') return;
    const n = wear === 'derelict' ? count : Math.max(1, Math.round(count * 0.42)), rnd = mulberry(seed);
    for (let i = 0; i < n; i++) {
      const a = (rnd() - 0.5) * 2.2, x = (rnd() - 0.5) * L * 0.78, len = L * (0.05 + rnd() * 0.09);
      F.push(...box([x, Math.cos(a) * (r + 0.005), cz + Math.sin(a) * (r + 0.005)],
                    [len, Math.max(0.014, r * 0.05), Math.max(0.014, r * 0.05)], 'rust', -0.2, -0.03));
    }
  }

  // ---- the table -----------------------------------------------------------
  const TYPES = {
    jug: {
      kind: 'carry', label: 'OIL JUG', read: 'body', shape: 'box', order: 1,
      gloss: 'Moulded HDPE bottle, grade-coloured cap and shoulder band. The wall is the gauge.',
      sizes: {
        s1:  { L: 1,  w: 0.108, d: 0.078, h: 0.220, tare: 0.07, handle: false, label: '1 L' },
        s4:  { L: 4,  w: 0.180, d: 0.120, h: 0.298, tare: 0.24, handle: true,  label: '4 L' },
        s10: { L: 10, w: 0.252, d: 0.172, h: 0.362, tare: 0.55, handle: true,  label: '10 L' },
      },
    },
    jerry: {
      kind: 'carry', label: 'JERRY CAN', read: 'body', shape: 'box', order: 2,
      gloss: 'Three handles: one man, two men, or hand it across a gunwale. Plastic reads its own level; the steel can carries a welded sight glass instead.',
      sizes: {
        s5:  { L: 5,  w: 0.238, d: 0.128, h: 0.292, tare: 0.80, label: '5 L' },
        s10: { L: 10, w: 0.292, d: 0.152, h: 0.372, tare: 1.30, label: '10 L' },
        s20: { L: 20, w: 0.348, d: 0.168, h: 0.470, tare: 2.20, label: '20 L' },
        s25: { L: 25, w: 0.372, d: 0.188, h: 0.502, tare: 2.70, label: '25 L' },
      },
    },
    nozzle: {
      kind: 'carry', label: 'FUEL NOZZLE', read: 'none', shape: 'box', order: 3,
      gloss: 'Cast body, trigger and hold-open latch, hose whip off the back. The hose is a runtime polyline, not pixels.',
      sizes: {
        sAuto: { L: 0, len: 0.255, tare: 1.4, hose: 4.5, label: 'AUTO' },
        sHigh: { L: 0, len: 0.310, tare: 2.6, hose: 6.0, label: 'HIGH-FLOW' },
      },
    },
    drum: {
      kind: 'store', label: 'STEEL DRUM', read: 'tube', shape: 'vcyl', order: 4,
      gloss: 'Painted steel, two rolling hoops, two bungs. Level comes off a strap-on sight glass — one on each side, because a drum is round and a player is not always north of it.',
      sizes: {
        s60:  { L: 60,  dia: 0.400, h: 0.610, tare: 8,  label: '60 L' },
        s205: { L: 205, dia: 0.585, h: 0.880, tare: 18, label: '205 L' },
      },
    },
    tote: {
      kind: 'store', label: 'IBC TOTE', read: 'body', shape: 'box', order: 5,
      gloss: 'The whole bottle is the gauge — a caged tote is the one fuel vessel in the world that never needed a gauge fitted.',
      sizes: {
        s600:  { L: 600,  w: 1.00, d: 0.80, h: 0.94, tare: 48, label: '600 L' },
        s1000: { L: 1000, w: 1.20, d: 1.00, h: 1.17, tare: 62, label: '1000 L' },
      },
    },
    skid: {
      kind: 'store', label: 'SKID TANK', read: 'tube', shape: 'hcyl', order: 6,
      gloss: 'Horizontal cylinder on saddle legs — the wharf day tank. Volume is not linear in depth here, and the sight glass tells the truth about that.',
      sizes: {
        s500:  { L: 500,  dia: 0.76, len: 1.24, leg: 0.34, tare: 90,  label: '500 L' },
        s1200: { L: 1200, dia: 1.00, len: 1.66, leg: 0.40, tare: 170, label: '1200 L' },
        s2500: { L: 2500, dia: 1.26, len: 2.14, leg: 0.46, tare: 310, label: '2500 L' },
      },
    },
    bulk: {
      kind: 'store', label: 'BULK TANK', read: 'board', shape: 'vcyl', order: 7,
      gloss: 'The commercial farm: pad, berm, caged ladder, grade band at the shoulder, float-and-tape board you can read from the fuel dock.',
      sizes: {
        s10k: { L: 10000, dia: 2.60, h: 2.05, tare: 1400, label: '10 kL' },
        s25k: { L: 25000, dia: 3.20, h: 3.15, tare: 3100, label: '25 kL' },
        s50k: { L: 50000, dia: 3.60, h: 4.95, tare: 5400, label: '50 kL' },
      },
    },
    pump: {
      kind: 'store', label: 'DISPENSER', read: 'none', shape: 'box', order: 8,
      gloss: 'The pedestal on the fuel float: counter face, grade band, hose mast and nozzle boot. Twin serves two grades off one plinth.',
      sizes: {
        sSingle: { L: 0, w: 0.46, d: 0.34, h: 1.06, tare: 95,  twin: false, label: 'SINGLE' },
        sTwin:   { L: 0, w: 0.62, d: 0.38, h: 1.12, tare: 140, twin: true,  label: 'TWIN' },
      },
    },
  };
  const TYPE_ORDER = Object.keys(TYPES).sort((a, b) => TYPES[a].order - TYPES[b].order);
  const sizesOf = (t) => Object.keys(TYPES[t].sizes);
  function resolveSize(t, s) { const k = sizesOf(t); return (s && TYPES[t].sizes[s]) ? s : k[Math.min(k.length - 1, 1)]; }

  // ---- geometry ------------------------------------------------------------
  function geoJug(Z, zf, o) {
    const F = [], hw = Z.w / 2, hd = Z.d / 2, H = Z.h;
    const bodyTop = H * 0.70, shZ = H * 0.865, neck = H * 0.925;
    F.push(...box([0, 0, 0.010], [hw * 0.92, hd * 0.92, 0.010], 'body', -0.6, 0, null, 'shell'));          // foot
    F.push(...shellBox([0, 0, (0.018 + bodyTop) / 2], [hw, hd, (bodyTop - 0.018) / 2], zf, 0, 0, 'shell'));
    F.push(...shellBox([0, 0, (bodyTop + shZ) / 2], [hw * 0.74, hd * 0.78, (shZ - bodyTop) / 2], zf, 0.12, 0, 'shell'));  // shoulder
    F.push(...box([0, 0, bodyTop + (shZ - bodyTop) * 0.30], [hw * 0.78, hd * 0.82, 0.014], 'code', 0.35, -0.02));        // grade band
    F.push(...box([0, 0, (shZ + neck) / 2], [hw * 0.30, hd * 0.42, (neck - shZ) / 2], 'body', 0.2, 0, null, 'shell'));    // neck
    F.push(...box([0, 0, (neck + H) / 2], [hw * 0.38, hd * 0.50, (H - neck) / 2], 'code', 0.4, -0.02));                   // cap
    F.push(...box([0, 0, H], [hw * 0.30, hd * 0.40, 0.010], 'code', 0.75, -0.04));                                        // cap crown
    let grip = [0, 0, H * 0.90];
    if (Z.handle) {
      const y = -(hd + 0.030), z0 = H * 0.44, z1 = bodyTop + (shZ - bodyTop) * 0.55, r = 0.016;
      F.push(...bar([0, y, z0], [0, y, z1], [hw * 0.30, r], 'body', 0.15, -0.02, 'shell'));
      F.push(...bar([0, -hd + 0.004, z1], [0, y, z1], [hw * 0.30, r], 'body', 0.3, -0.02, 'shell'));
      F.push(...bar([0, -hd + 0.004, z0], [0, y, z0], [hw * 0.26, r * 0.9], 'body', -0.1, -0.02, 'shell'));
      grip = [0, y, (z0 + z1) / 2];
    }
    return { F, grip, mounts: { cap: [0, 0, H + 0.02], spout: [0, 0, H + 0.02] } };
  }

  function geoJerry(Z, zf, o) {
    const F = [], hw = Z.w / 2, hd = Z.d / 2, H = Z.h, steel = o.shell === 'steel';
    const capT = H - 0.030;
    F.push(...box([0, 0, 0.012], [hw * 0.94, hd * 0.90, 0.012], steel ? 'steel' : 'body', -0.55, 0, null, 'shell'));  // rolled foot
    if (steel) F.push(...box([0, 0, (0.022 + capT) / 2], [hw, hd, (capT - 0.022) / 2], 'body', 0, 0, null, 'shell'));
    else F.push(...shellBox([0, 0, (0.022 + capT) / 2], [hw, hd, (capT - 0.022) / 2], zf, 0, 0, 'shell'));
    F.push(...box([0, 0, (capT + H) / 2], [hw * 0.90, hd * 0.86, (H - capT) / 2], 'code', 0.30, 0));   // chamfered top, grade-coloured
    /* No X emboss. A real can has one; at 22 px tall it is four bright diagonals across exactly the
       band the level line has to cross, and the gauge loses. Form carries the can instead. */
    for (const sy of [-1, 1])
      F.push(...box([0, sy * (hd + 0.005), H * 0.90], [hw * 0.92, hd * 0.05, 0.011], 'code', 0.15, 0.02));   // shoulder rib, above full
    const hr = 0.020, hz = H + hr + 0.006;                                                                        // three handles
    for (const sx of [-1, 0, 1]) {
      const x = sx * hw * 0.56;
      F.push(...bar([x, -hd * 0.72, hz], [x, hd * 0.72, hz], [hr * 0.85, hr], 'code', 0.35, -0.03));
      for (const sy of [-1, 1]) F.push(...bar([x, sy * hd * 0.72, hz], [x, sy * hd * 0.86, H - 0.010], [hr * 0.8, hr * 0.8], 'code', 0.1, -0.03));
    }
    const spx = hw * 0.58;                                                                                        // spout + cam cap
    F.push(...bar([spx, 0, H - 0.006], [spx + 0.030, hd * 0.30, H + 0.052], [0.028, 0.028], steel ? 'steel' : 'body', 0.25, -0.04, 'shell'));
    F.push(...box([spx + 0.036, hd * 0.34, H + 0.062], [0.032, 0.030, 0.014], 'code', 0.7, -0.06));
    if (steel) { sightGlass(hw, 0, 0.06, capT - 0.03, zf, 'x', 1, 0.048, F, 0.006); sightGlass(-hw, 0, 0.06, capT - 0.03, zf, 'x', -1, 0.048, F, 0.006); }
    return { F, grip: [0, 0, hz], mounts: { spout: [spx + 0.05, hd * 0.4, H + 0.07], cap: [spx, 0, H + 0.06] } };
  }

  function geoNozzle(Z, o) {
    const F = [], L = Z.len, k = L / 0.255, flow = !!o.flow;
    F.push(...bar([0, -0.090 * k, -0.028 * k], [0, -0.030 * k, 0.030 * k], [0.024 * k, 0.030 * k], 'graph', 0.05, 0));   // hand grip
    F.push(...box([0, 0.010 * k, 0.036 * k], [0.030 * k, 0.062 * k, 0.038 * k], 'code', 0.15, 0));                        // cast body
    F.push(...box([0, 0.010 * k, 0.074 * k], [0.024 * k, 0.050 * k, 0.008 * k], 'brass', 0.55, -0.02));                   // top rib
    F.push(...bar([0, -0.070 * k, -0.038 * k], [0, 0.030 * k, -0.010 * k], [0.010 * k, 0.011 * k], 'brass', 0.3, -0.02)); // trigger guard
    F.push(...bar([0, -0.052 * k, -0.006 * k], [0, -0.016 * k, -0.012 * k], [0.011 * k, 0.010 * k], flow ? 'code' : 'brass', flow ? 0.8 : 0.1, -0.04)); // trigger
    F.push(...box([0.020 * k, -0.058 * k, 0.006 * k], [0.012 * k, 0.016 * k, 0.007 * k], 'brass', 0.5, -0.05));           // hold-open latch
    F.push(...bar([0, 0.062 * k, 0.046 * k], [0, 0.132 * k, 0.028 * k], [0.019 * k, 0.019 * k], 'steel', 0.25, -0.01));   // spout, first bend
    F.push(...bar([0, 0.128 * k, 0.030 * k], [0, 0.186 * k, -0.006 * k], [0.017 * k, 0.017 * k], 'steel', 0.35, -0.01));  // spout, second
    F.push(...tube(0, 0.078 * k, 0.030 * k, 0.062 * k, 0.034 * k, 0.026 * k, 10, 'graph', { xf: rotX(-0.35), caps: 'none', b: -0.1, db: -0.03 })); // splash cone
    F.push(...box([0, 0.186 * k, -0.008 * k], [0.019 * k, 0.012 * k, 0.019 * k], 'brass', 0.45, -0.05));                  // tip ferrule
    F.push(...bar([0, -0.100 * k, -0.024 * k], [0, -0.168 * k, -0.062 * k], [0.023 * k, 0.023 * k], 'graph', -0.05, 0));  // hose whip
    F.push(...box([0, -0.104 * k, -0.026 * k], [0.026 * k, 0.012 * k, 0.026 * k], 'code', 0.4, -0.04));                   // grade collar
    return { F, grip: [0, -0.060 * k, 0.006 * k], mounts: { tip: [0, 0.196 * k, -0.014 * k], hose: [0, -0.172 * k, -0.066 * k] } };
  }

  function geoDrum(Z, zf, o) {
    const F = [], r = Z.dia / 2, H = Z.h, seg = 18;
    F.push(...tube(0, 0, 0.026, H - 0.026, r, r, seg, 'body', { caps: 'none', b: 0, tag: 'shell' }));
    for (const z of [0.014, H - 0.014]) F.push(...tube(0, 0, z - 0.014, z + 0.014, r + 0.012, r + 0.012, seg, 'steel', { caps: 'none', b: 0.5, db: -0.02 })); // chimes
    for (const f of [0.34, 0.64]) F.push(...tube(0, 0, H * f - 0.020, H * f + 0.020, r + 0.014, r + 0.014, seg, 'steel', { caps: 'none', b: 0.45, db: -0.02 })); // rolling hoops
    disc(0, 0, H, r, 'body', 0.3, 0, F); disc(0, 0, 0.002, r, 'steel', -0.7, 0, F);
    F.push(...tube(0, 0, H, H + 0.016, r * 0.52, r * 0.52, 10, 'body', { caps: 'top', b: -0.35, capB: -0.5, db: -0.02 }));                                    // head sink
    F.push(...tube(r * 0.62, 0, H + 0.004, H + 0.026, 0.036, 0.036, 8, 'brass', { caps: 'top', b: 0.5, db: -0.05 }));                                          // large bung
    F.push(...tube(-r * 0.62, 0.02, H + 0.004, H + 0.020, 0.022, 0.022, 8, 'brass', { caps: 'top', b: 0.4, db: -0.05 }));                                      // vent bung
    F.push(...tube(0, 0, H * 0.86, H * 0.90, r + 0.004, r + 0.004, seg, 'code', { caps: 'none', b: 0.55, db: -0.03 }));                                        // grade band
    const gz0 = 0.07, gz1 = H - 0.06;
    sightGlass(0, r, gz0, gz1, zf, 'y', 1, 0.055, F, 0.022);
    sightGlass(0, -r, gz0, gz1, zf, 'y', -1, 0.055, F, 0.022);
    streakCyl(F, r, 0.10, H - 0.05, 6, 4211, o.wear);
    const mounts = { bung: [r * 0.62, 0, H + 0.03] };
    if (o.pump) {                                                                                                                                              // rotary hand pump
      const px = r * 0.62, top = H + 0.40;
      F.push(...bar([px, 0, H + 0.02], [px, 0, top], [0.026, 0.026], 'steel', 0.2, -0.03));
      F.push(...box([px, 0, top + 0.020], [0.052, 0.046, 0.028], 'code', 0.4, -0.04));
      F.push(...bar([px, 0, top + 0.030], [px + 0.14, 0.02, top + 0.070], [0.014, 0.014], 'steel', 0.35, -0.05));
      F.push(...box([px + 0.152, 0.024, top + 0.076], [0.014, 0.030, 0.014], 'graph', 0.3, -0.06));
      F.push(...bar([px, 0, top + 0.006], [px - 0.10, 0.05, top - 0.05], [0.020, 0.020], 'steel', 0.25, -0.05));
      mounts.spout = [px - 0.12, 0.06, top - 0.07];
    }
    return { F, grip: null, mounts };
  }

  function geoTote(Z, zf, o) {
    const F = [], hw = Z.w / 2, hd = Z.d / 2, palH = 0.145, bH = Z.h - palH, z0 = palH + 0.012, z1 = palH + bH;
    for (const sx of [-1, 0, 1]) F.push(...bar([sx * hw * 0.80, -hd, 0.055], [sx * hw * 0.80, hd, 0.055], [0.030, 0.055], 'galv', -0.15, 0));   // pallet runners
    F.push(...box([0, 0, palH - 0.026], [hw, hd, 0.026], 'galv', 0.15, 0));                                                                     // pallet deck
    F.push(...box([0, 0, palH - 0.006], [hw - 0.020, hd - 0.020, 0.010], 'galv', -0.35, -0.02));                                                // drip lip
    F.push(...shellBox([0, 0, (z0 + z1) / 2], [hw - 0.030, hd - 0.030, (z1 - z0) / 2], zf, 0, 0, 'shell'));
    /* The cage is DRAWN THIN. A real tote has seven wires to a face; at 32 px/m that is a 4 px pitch
       of 1 px bars and the bottle behind it stops reading, which defeats the one vessel in the kit
       that never needed a gauge. Four courses and three stiles say "caged tote" and keep the level. */
    const nv = 3, nh = Math.max(3, Math.min(5, Math.round(bH / 0.30)));                                                                                     // the cage
    for (let i = 0; i <= nh; i++) {
      const z = z0 + (z1 - z0) * i / nh;
      F.push(...bar([-hw, -hd, z], [hw, -hd, z], [0.019, 0.017], 'galv', 0.25, -0.05));
      F.push(...bar([-hw, hd, z], [hw, hd, z], [0.019, 0.017], 'galv', 0.25, -0.05));
      F.push(...bar([-hw, -hd, z], [-hw, hd, z], [0.017, 0.019], 'galv', 0.25, -0.05));
      F.push(...bar([hw, -hd, z], [hw, hd, z], [0.017, 0.019], 'galv', 0.25, -0.05));
    }
    for (let i = 0; i <= nv; i++) {
      const x = -hw + 2 * hw * i / nv, y = -hd + 2 * hd * i / nv;
      F.push(...bar([x, -hd, z0], [x, -hd, z1], [0.017, 0.017], 'galv', 0.3, -0.06));
      F.push(...bar([x, hd, z0], [x, hd, z1], [0.017, 0.017], 'galv', 0.3, -0.06));
      F.push(...bar([-hw, y, z0], [-hw, y, z1], [0.017, 0.017], 'galv', 0.3, -0.06));
      F.push(...bar([hw, y, z0], [hw, y, z1], [0.017, 0.017], 'galv', 0.3, -0.06));
    }
    for (const sx of [-1, 1]) for (const sy of [-1, 1])                                                                                          // corner posts
      F.push(...bar([sx * hw, sy * hd, palH - 0.02], [sx * hw, sy * hd, z1 + 0.012], [0.026, 0.026], 'galv', 0.4, -0.07));
    F.push(...bar([-hw, -hd, z1 + 0.012], [hw, -hd, z1 + 0.012], [0.024, 0.022], 'galv', 0.45, -0.07));
    F.push(...bar([-hw, hd, z1 + 0.012], [hw, hd, z1 + 0.012], [0.024, 0.022], 'galv', 0.45, -0.07));
    F.push(...bar([-hw, -hd, z1 + 0.012], [-hw, hd, z1 + 0.012], [0.022, 0.024], 'galv', 0.45, -0.07));
    F.push(...bar([hw, -hd, z1 + 0.012], [hw, hd, z1 + 0.012], [0.022, 0.024], 'galv', 0.45, -0.07));
    F.push(...tube(0, 0, z1, z1 + 0.055, 0.115, 0.098, 12, 'code', { caps: 'top', b: 0.4, capB: 0.75, db: -0.05 }));                             // fill cap
    F.push(...tube(0, 0, z1 + 0.055, z1 + 0.070, 0.104, 0.104, 12, 'code', { caps: 'top', b: 0.9, capB: 1.0, db: -0.07 }));
    const vy = hd + 0.010, vz = z0 + 0.048;                                                                                                      // discharge valve
    F.push(...box([0, vy, vz], [0.062, 0.048, 0.048], 'graph', 0.15, -0.06));
    F.push(...bar([0, vy + 0.02, vz], [0, vy + 0.10, vz], [0.032, 0.032], 'code', 0.35, -0.08));
    F.push(...box([0.010, vy + 0.11, vz], [0.030, 0.014, 0.030], 'code', 0.7, -0.10));
    F.push(...bar([-0.060, vy + 0.03, vz + 0.010], [0.075, vy + 0.03, vz + 0.028], [0.013, 0.013], 'code', 0.6, -0.10));                          // lever
    return { F, grip: null, mounts: { valve: [0, vy + 0.13, vz], cap: [0, 0, z1 + 0.09] } };
  }

  function geoSkid(Z, zf, o) {
    const F = [], r = Z.dia / 2, L = Z.len, legH = Z.leg, cz = legH + r, seg = 20;
    const lay = chain(rotY(Math.PI / 2), move(0, 0, cz));
    F.push(...tube(0, 0, -L / 2, L / 2, r, r, seg, 'body', { caps: 'both', xf: lay, b: 0, capB: 0.1, tag: 'shell' }));
    for (const sx of [-1, 1]) {                                                                                        // saddle legs
      const x = sx * L * 0.30;
      for (const sy of [-1, 1]) {
        F.push(...bar([x, sy * r * 0.62, cz - r * 0.55], [x, sy * (r + 0.10), 0.03], [0.030, 0.026], 'steel', 0.1, 0));
        F.push(...bar([x - 0.05, sy * (r + 0.10), 0.03], [x + 0.05, sy * (r + 0.10), 0.03], [0.058, 0.030], 'steel', 0.2, 0));
      }
      F.push(...bar([x, -(r + 0.10), 0.075], [x, r + 0.10, 0.075], [0.020, 0.020], 'steel', -0.1, -0.02));             // cross tie
      F.push(...tube(0, 0, x - 0.030, x + 0.030, r + 0.012, r + 0.012, seg, 'steel', { caps: 'none', xf: lay, b: 0.4, db: -0.02 })); // saddle band
    }
    for (const sy of [-1, 1]) F.push(...bar([-L * 0.36, sy * (r + 0.10), 0.028], [L * 0.36, sy * (r + 0.10), 0.028], [0.026, 0.022], 'steel', -0.2, 0)); // skid rails
    F.push(...tube(0, 0, -L / 2 - 0.006, -L / 2 + 0.070, r + 0.008, r + 0.008, seg, 'code', { caps: 'none', xf: lay, b: 0.5, db: -0.03 })); // grade band, far end
    F.push(...tube(0, 0, L / 2 - 0.070, L / 2 + 0.006, r + 0.008, r + 0.008, seg, 'code', { caps: 'none', xf: lay, b: 0.5, db: -0.03 }));
    const tz0 = cz - r + 0.045, tz1 = cz + r - 0.045;
    sightGlass(-L * 0.06, r, tz0, tz1, zf, 'y', 1, 0.060, F, 0.048);
    sightGlass(-L * 0.06, -r, tz0, tz1, zf, 'y', -1, 0.060, F, 0.048);
    streakHCyl(F, r, cz, L, 7, 7717, o.wear);
    F.push(...tube(0, 0, cz + r - 0.010, cz + r + 0.052, 0.070, 0.062, 12, 'code', { caps: 'top', b: 0.45, capB: 0.8, db: -0.05 }));  // fill cap
    F.push(...tube(-L * 0.30, 0, cz + r * 0.92, cz + r + 0.048, 0.026, 0.026, 8, 'brass', { caps: 'top', b: 0.5, db: -0.05 }));       // vent
    const ox = L / 2 + 0.012, oz = cz - r * 0.45;                                                                                     // outlet + valve
    F.push(...bar([L / 2 - 0.02, 0, oz], [ox + 0.070, 0, oz], [0.028, 0.028], 'steel', 0.2, -0.04));
    F.push(...box([ox + 0.086, 0, oz], [0.030, 0.038, 0.038], 'code', 0.4, -0.06));
    F.push(...tube(ox + 0.086, 0, oz + 0.036, oz + 0.052, 0.046, 0.046, 10, 'code', { caps: 'top', b: 0.75, db: -0.08 }));            // hand wheel
    F.push(...tube(-L / 2 - 0.014, 0, cz + 0.02, cz + 0.02, 0.088, 0.088, 12, 'white', { caps: 'both', xf: rotY(Math.PI / 2), b: 0.5, db: -0.05 }));  // end dial
    return { F, grip: null, mounts: { outlet: [ox + 0.13, 0, oz], cap: [0, 0, cz + r + 0.08] } };
  }

  function geoBulk(Z, zf, o) {
    const F = [], r = Z.dia / 2, H = Z.h, seg = 26, pad = r * 1.62;
    F.push(...tube(0, 0, -0.02, 0.075, pad, pad, 20, 'conc', { caps: 'top', b: -0.15, capB: -0.05 }));                                 // pad
    if (o.berm !== false) {                                                                                                            // containment berm
      F.push(...tube(0, 0, 0.05, 0.05 + Math.min(0.62, H * 0.17), pad, pad, 20, 'conc', { caps: 'none', b: 0.15 }));
      F.push(...tube(0, 0, 0.05, 0.05 + Math.min(0.62, H * 0.17), pad - 0.10, pad - 0.10, 20, 'conc', { caps: 'none', b: -0.75 }));
      F.push(...tube(0, 0, 0.05 + Math.min(0.62, H * 0.17) - 0.03, 0.05 + Math.min(0.62, H * 0.17), pad, pad - 0.10, 20, 'conc', { caps: 'none', b: 0.6, db: -0.02 }));
    }
    F.push(...tube(0, 0, 0.075, 0.19, r + 0.05, r + 0.05, seg, 'steel', { caps: 'none', b: 0.2 }));                                     // skirt / anchor ring
    F.push(...tube(0, 0, 0.19, H, r, r, seg, 'white', { caps: 'none', b: 0, tag: 'shell' }));
    for (const f of [0.33, 0.66]) F.push(...tube(0, 0, H * f - 0.022, H * f + 0.022, r + 0.010, r + 0.010, seg, 'white', { caps: 'none', b: 0.4, db: -0.02 })); // weld course lifts
    F.push(...tube(0, 0, H * 0.775, H * 0.865, r + 0.008, r + 0.008, seg, 'code', { caps: 'none', b: 0.45, db: -0.03 }));               // grade band
    F.push(...tube(0, 0, H, H + r * 0.20, r, r * 0.30, seg, 'white', { caps: 'top', b: 0.35, capB: 0.55, db: -0.01 }));                 // cone roof
    F.push(...tube(0, 0, H + r * 0.20, H + r * 0.20 + 0.26, 0.075, 0.062, 10, 'steel', { caps: 'top', b: 0.5, db: -0.03 }));            // vent stack
    F.push(...tube(0, 0, H + r * 0.20 + 0.26, H + r * 0.20 + 0.32, 0.105, 0.105, 10, 'steel', { caps: 'top', b: 0.8, db: -0.05 }));
    F.push(...tube(r * 0.46, r * 0.10, H + r * 0.10, H + r * 0.14, 0.130, 0.130, 10, 'steel', { caps: 'top', b: 0.55, db: -0.04 }));    // manway
    const lx = -r, rung = 0.30;                                                                                                         // caged ladder, west face
    for (const sy of [-1, 1]) F.push(...bar([lx - 0.10, sy * 0.19, 0.16], [lx - 0.10, sy * 0.19, H + r * 0.16], [0.021, 0.021], 'steel', 0.35, -0.05));
    for (let z = 0.30; z < H + r * 0.14; z += rung) F.push(...bar([lx - 0.10, -0.19, z], [lx - 0.10, 0.19, z], [0.014, 0.014], 'steel', 0.25, -0.06));
    for (let z = 1.05; z < H + r * 0.10; z += 0.62) {                                                                                    // ladder cage hoops
      F.push(...bar([lx - 0.36, -0.30, z], [lx - 0.36, 0.30, z], [0.015, 0.015], 'steel', 0.3, -0.06));
      for (const sy of [-1, 1]) F.push(...bar([lx - 0.36, sy * 0.30, z], [lx - 0.02, sy * 0.24, z], [0.015, 0.015], 'steel', 0.3, -0.06));
    }
    for (let i = 0; i < 10; i++) {                                                                                                        // roof rail
      const a = i / 10 * Math.PI * 2, b2 = (i + 1) / 10 * Math.PI * 2, rr = r * 0.86, rz = H + r * 0.20 * (1 - 0.86 / 1) + 0.02;
      const zz = H + r * 0.20 * 0.14;
      F.push(...bar([Math.cos(a) * rr, Math.sin(a) * rr, zz], [Math.cos(a) * rr, Math.sin(a) * rr, zz + 0.42], [0.017, 0.017], 'steel', 0.35, -0.05));
      F.push(...bar([Math.cos(a) * rr, Math.sin(a) * rr, zz + 0.40], [Math.cos(b2) * rr, Math.sin(b2) * rr, zz + 0.40], [0.015, 0.015], 'steel', 0.4, -0.05));
    }
    const gz0 = 0.32, gz1 = H - 0.14;
    gaugeBoard(0, r, gz0, gz1, zf, 1, Math.max(0.18, r * 0.16), F);
    gaugeBoard(0, -r, gz0, gz1, zf, -1, Math.max(0.18, r * 0.16), F);
    streakCyl(F, r, 0.30, H - 0.10, 9, 3313, o.wear);
    const my = r + 0.02;                                                                                                                  // bottom manifold
    F.push(...bar([r * 0.32, my - 0.10, 0.30], [r * 0.32, my + 0.24, 0.30], [0.038, 0.038], 'steel', 0.25, -0.04));
    F.push(...box([r * 0.32, my + 0.26, 0.30], [0.045, 0.040, 0.048], 'code', 0.4, -0.06));
    F.push(...tube(r * 0.32, my + 0.26, 0.352, 0.372, 0.072, 0.072, 10, 'code', { caps: 'top', b: 0.8, db: -0.08 }));
    F.push(...bar([r * 0.32, my - 0.02, 0.30], [r * 0.32, my - 0.02, 0.86], [0.030, 0.030], 'steel', 0.2, -0.04));
    F.push(...box([-r * 0.30, my + 0.10, 0.44], [0.20, 0.13, 0.44], 'white', 0.1, -0.03));                                                // fill cabinet
    F.push(...box([-r * 0.30, my + 0.23, 0.44], [0.17, 0.02, 0.38], 'dark', -0.45, -0.06));
    F.push(...box([-r * 0.30, my + 0.24, 0.74], [0.15, 0.014, 0.045], 'code', 0.6, -0.08));
    return { F, grip: null, mounts: { fill: [-r * 0.30, my + 0.28, 0.62], outlet: [r * 0.32, my + 0.32, 0.30], ladder: [lx - 0.10, 0, 0.30] } };
  }

  function geoPump(Z, o) {
    const F = [], hw = Z.w / 2, hd = Z.d / 2, H = Z.h, twin = Z.twin;
    F.push(...box([0, 0, 0.045], [hw + 0.13, hd + 0.13, 0.045], 'conc', -0.05, 0));                                    // plinth
    F.push(...box([0, 0, 0.10 + (H - 0.10) / 2 * 0.30], [hw, hd, (H - 0.10) * 0.15], 'graph', -0.1, 0));               // lower cabinet
    F.push(...box([0, 0, 0.10 + (H - 0.10) * 0.62], [hw, hd, (H - 0.10) * 0.32], 'white', 0.05, 0, null, 'shell'));    // upper body
    F.push(...box([0, 0, H - 0.048], [hw + 0.020, hd + 0.020, 0.048], 'code', 0.45, -0.03));                            // grade crown
    if (twin) F.push(...box([0, 0, H - 0.048], [hw * 0.34, hd + 0.024, 0.050], 'code2', 0.45, -0.05));
    F.push(...box([0, 0, H + 0.014], [hw * 0.86, hd * 0.82, 0.014], 'white', 0.8, -0.05));                              // canopy light
    for (const sy of [-1, 1]) {                                                                                          // counter faces
      const y = sy * (hd + 0.006), fz = 0.10 + (H - 0.10) * 0.72;
      F.push(...box([0, y, fz], [hw * 0.80, 0.010, 0.088], 'dark', -0.35, -0.03));
      for (let i = 0; i < 4; i++) F.push(...box([(-1.5 + i) * hw * 0.36, y + 0.006, fz + 0.014], [hw * 0.14, 0.008, 0.032], 'white', 0.35, -0.06));
      F.push(...box([0, y + 0.006, fz - 0.058], [hw * 0.52, 0.008, 0.018], 'code', 0.55, -0.06));
      F.push(...box([0, y, 0.10 + (H - 0.10) * 0.34], [hw * 0.86, 0.008, 0.030], 'code', 0.35, -0.04));                  // grade stripe
    }
    const mastX = hw + 0.010, mastZ = H + 0.30;                                                                          // hose mast
    F.push(...bar([mastX - 0.02, 0, H - 0.06], [mastX + 0.02, 0, mastZ], [0.026, 0.026], 'steel', 0.25, -0.03));
    F.push(...bar([mastX + 0.02, 0, mastZ], [mastX + 0.16, 0, mastZ + 0.02], [0.020, 0.020], 'steel', 0.35, -0.04));
    F.push(...box([mastX + 0.17, 0, mastZ + 0.02], [0.024, 0.026, 0.024], 'code', 0.5, -0.06));
    const boots = twin ? [1, -1] : [1];
    const mounts = { hose: [mastX + 0.19, 0, mastZ + 0.01] };
    boots.forEach((s, i) => {                                                                                             // nozzle boot
      const bx = s * (hw + 0.014), bz = 0.10 + (H - 0.10) * 0.50;
      F.push(...box([bx, 0, bz], [0.020, 0.062, 0.075], i ? 'code2' : 'code', 0.3, -0.04));
      F.push(...box([bx + s * 0.014, 0, bz + 0.012], [0.012, 0.042, 0.048], 'dark', -0.6, -0.07));
      mounts[i ? 'boot2' : 'boot'] = [bx + s * 0.05, 0, bz + 0.05];
    });
    F.push(...box([-hw - 0.012, 0, 0.10 + (H - 0.10) * 0.22], [0.014, 0.046, 0.058], 'graph', 0.15, -0.04));              // filter bowl
    return { F, grip: null, mounts };
  }

  // ---- assembly ------------------------------------------------------------
  const GCACHE = new Map();
  function geo(type, size, o) {
    const T = TYPES[type], Z = T.sizes[size];
    const fill = Math.max(0, Math.min(1, o.fill != null ? o.fill : 0.6));
    const q = Math.round(fill * 96) / 96;
    const shell = o.shell || SHELL_DEFAULT[type];
    const key = [type, size, q, o.wear || 'working', shell, o.pump ? 1 : 0, o.berm === false ? 0 : 1, o.flow ? 1 : 0].join('|');
    if (GCACHE.has(key)) return GCACHE.get(key);
    const hf = heightFrac(T.shape, q);
    let G;
    if (type === 'jug') G = geoJug(Z, 0.022 + (Z.h * 0.70 - 0.022) * hf * 1.06, o);
    else if (type === 'jerry') G = geoJerry(Z, 0.022 + (Z.h - 0.052) * hf * 0.98, Object.assign({}, o, { shell }));
    else if (type === 'nozzle') G = geoNozzle(Z, o);
    else if (type === 'drum') G = geoDrum(Z, 0.070 + (Z.h - 0.130) * hf, o);
    else if (type === 'tote') G = geoTote(Z, 0.157 + (Z.h - 0.145 - 0.062) * hf, o);
    else if (type === 'skid') G = geoSkid(Z, Z.leg + Z.dia * hf, o);
    else if (type === 'bulk') G = geoBulk(Z, 0.32 + (Z.h - 0.46) * hf, o);
    else G = geoPump(Z, o);
    /* IsoSolid compares `depth - db` and the SMALLER value wins, so a face is pulled proud by a
       POSITIVE db. Everything above is authored the intuitive way round — negative reads as "in
       front" — so the sign is flipped once, here, rather than eighty times up there. */
    G.F = G.F.map((f) => (f.db ? Object.assign({}, f, { db: -f.db }) : f));
    G.F = wearPass(G.F, o.wear || 'working', type, size);
    G.dims = Z; G.hf = hf; G.fill = q;
    if (GCACHE.size > 480) GCACHE.clear();
    GCACHE.set(key, G);
    return G;
  }
  /* Steel weathers by run-off (streakCyl/streakHCyl, above). What is left here is the flat part of
     it: paint dulls, and on a derelict the trim goes. Plastic never rusts — a scuffed jerry can just
     goes chalky, which is a bias step, not a material swap. */
  function wearPass(F, wear, type, size) {
    if (wear === 'fresh') return F;
    const plastic = SHELL_DEFAULT[type] === 'hdpe';
    const dull = wear === 'derelict' ? -0.55 : -0.14;
    const p = 0;
    const rnd = mulberry(type.length * 977 + size.length * 131 + (wear === 'derelict' ? 7 : 3));
    return F.map((f) => {
      if (f.tag !== 'shell') return f;
      const r = rnd();
      if (p && r < p) return Object.assign({}, f, { mat: 'rust', b: (f.b || 0) - 0.2 });
      return Object.assign({}, f, { b: (f.b || 0) + dull });
    });
  }
  const xform = (F, fn) => F.map((f) => ({ v: f.v.map(fn), mat: f.mat, b: f.b, db: f.db, tag: f.tag }));

  // ---- cells ---------------------------------------------------------------
  const MCACHE = new Map();
  const TILT = 15 * DEG;                        // carry allowance: a swinging can never leaves its quad
  function measure(type, size, mode, ex) {
    ex = ex || {};
    const key = [type, size, mode, ex.pump ? 1 : 0, ex.berm === false ? 0 : 1, ex.shell || SHELL_DEFAULT[type]].join('|');
    if (MCACHE.has(key)) return MCACHE.get(key);
    const T = TYPES[type];
    const G = geo(type, size, { fill: 1, wear: 'working', pump: ex.pump, berm: ex.berm, shell: ex.shell });
    const base = mode === 'carry' && G.grip ? xform(G.F, move(-G.grip[0], -G.grip[1], -G.grip[2])) : G.F;
    const sets = mode === 'carry' && G.grip
      ? [base, xform(base, rotX(TILT)), xform(base, rotX(-TILT)), xform(base, rotY(TILT * 0.6)), xform(base, rotY(-TILT * 0.6))]
      : [base];
    let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
    for (let d = 0; d < 8; d++) {
      const B = camBasis({ dir: d, elev: DEFAULT_ELEV });
      for (const FS of sets) for (const f of FS) for (const v of f.v) {
        const p = proj(v[0], v[1], v[2], B, 0, 0);
        if (p.sx < x0) x0 = p.sx; if (p.sx > x1) x1 = p.sx;
        if (p.sy < y0) y0 = p.sy; if (p.sy > y1) y1 = p.sy;
      }
    }
    const pad = 2;
    const W = Math.ceil(x1 - x0) + pad * 2, H = Math.ceil(y1 - y0) + pad * 2;
    const M = { W, H, cx: Math.round(-x0 + pad), cy: Math.round(-y0 + pad), mode,
                kind: T.kind, grip: G.grip, mounts: G.mounts };
    MCACHE.set(key, M);
    return M;
  }
  const modeOf = (type, opts) => (TYPES[type].kind === 'carry' && !(opts && opts.rest)) ? 'carry' : 'rest';
  function cell(type, size, opts) {
    size = resolveSize(type, size); opts = opts || {};
    const M = measure(type, size, modeOf(type, opts), opts);
    return { W: M.W, H: M.H, cx: M.cx, cy: M.cy, pivot: { x: M.cx, y: M.cy }, mode: M.mode };
  }
  /* Where a mount point lands in cell pixels at a given facing — the boot a nozzle hangs in, the
     valve a jug fills under, the spout that pours. */
  function mount(type, dir, name, opts) {
    opts = opts || {}; const size = resolveSize(type, opts.size);
    const M = measure(type, size, modeOf(type, opts), opts);
    const G = geo(type, size, opts);
    const pt = (G.mounts || {})[name] || (name === 'grip' ? G.grip : null);
    if (!pt) return null;
    let p = pt.slice();
    if (M.mode === 'carry' && G.grip) {
      p = move(-G.grip[0], -G.grip[1], -G.grip[2])(p);
      if (opts.tilt) p = rotY(opts.tilt)(p);
      if (opts.swing) p = rotX(opts.swing)(p);
    } else if (opts.roll || opts.pitch) {
      if (opts.roll) p = rotY(opts.roll * DEG)(p);
      if (opts.pitch) p = rotX(opts.pitch * DEG)(p);
    }
    const B = camBasis({ dir, elev: opts.elev != null ? opts.elev : DEFAULT_ELEV });
    const v = proj(p[0], p[1], p[2], B, M.cx, M.cy);
    return { x: v.sx, y: v.sy, depth: v.d };
  }

  // ---- render --------------------------------------------------------------
  function render(type, dir, opts) {
    opts = opts || {};
    if (!TYPES[type]) throw new Error('FuelIso: unknown type ' + type);
    const size = resolveSize(type, opts.size);
    const M = measure(type, size, modeOf(type, opts), opts);
    const G = geo(type, size, opts);
    let F = G.F;
    if (M.mode === 'carry' && G.grip) {
      const fns = [move(-G.grip[0], -G.grip[1], -G.grip[2])];
      if (opts.tilt) fns.push(rotY(opts.tilt));
      if (opts.swing) fns.push(rotX(opts.swing));
      F = xform(F, chain(...fns));
    } else if (opts.roll || opts.pitch) {
      const fns = [];
      if (opts.roll) fns.push(rotY(opts.roll * DEG));
      if (opts.pitch) fns.push(rotX(opts.pitch * DEG));
      F = xform(F, chain(...fns));
    }
    return paint(F, {
      W: M.W, H: M.H, cx: M.cx, cy: M.cy, dir, elev: opts.elev != null ? opts.elev : DEFAULT_ELEV,
      MATS: matsFor(opts.fuel || 'gas', opts.shell || SHELL_DEFAULT[type], opts.grade2),
      keyline: opts.keyline, key: opts.key || KEY,
    });
  }
  function sheet(type, opts) {
    opts = opts || {};
    const size = resolveSize(type, opts.size);
    const c = cell(type, size, opts), out = [];
    for (let d = 0; d < 8; d++) out.push(render(type, d, Object.assign({}, opts, { size })));
    return { cells: out, W: c.W, H: c.H, cx: c.cx, cy: c.cy, dirs: 8, order: ORDER.slice() };
  }

  // ---- the gauge, as numbers ----------------------------------------------
  /* What the vessel is actually holding, and where its surface sits on the sprite. `readY` is the
     cell row the level line lands on at `dir` — hang a HUD tick off it, or check your own bake. */
  function level(type, size, fill, opts) {
    opts = opts || {}; size = resolveSize(type, size);
    const T = TYPES[type], Z = T.sizes[size], f = Math.max(0, Math.min(1, fill != null ? fill : 0));
    const fuel = FUELS[opts.fuel || 'gas'];
    const litres = Z.L * f, hf = heightFrac(T.shape, f);
    const kg = Z.tare + litres * fuel.dens;
    const R = { type, size, capacity: Z.L, litres: +litres.toFixed(1), fill: f, heightFrac: +hf.toFixed(3),
                kg: +kg.toFixed(2), tare: Z.tare, mech: T.read, label: Z.label, shape: T.shape };
    if (T.read !== 'none' && opts.dir != null) {
      const M = measure(type, size, modeOf(type, opts), opts);
      const G = geo(type, size, Object.assign({ fill: f }, opts));
      const zf = { jug: 0.022 + (Z.h * 0.70 - 0.022) * hf * 1.06, jerry: 0.022 + (Z.h - 0.052) * hf * 0.98,
                   drum: 0.070 + (Z.h - 0.130) * hf, tote: 0.157 + (Z.h - 0.145 - 0.062) * hf,
                   skid: Z.leg + Z.dia * hf, bulk: 0.32 + (Z.h - 0.46) * hf }[type];
      if (zf != null) {
        let p = [0, 0, zf];
        if (M.mode === 'carry' && G.grip) p = move(-G.grip[0], -G.grip[1], -G.grip[2])(p);
        const B = camBasis({ dir: opts.dir, elev: DEFAULT_ELEV });
        R.readY = +proj(p[0], p[1], p[2], B, M.cx, M.cy).sy.toFixed(1);
      }
    }
    return R;
  }
  /* A hose is a catenary between two cell-space points, and 4.5 m of it is 144 px — longer than any
     cell here. So it is a polyline you draw, not pixels you blit. `t` runs 0..1 along it. */
  function hosePath(from, to, opts) {
    opts = opts || {};
    const seg = opts.seg || 22, slack = opts.slack != null ? opts.slack : 0.32;
    const dx = to.x - from.x, dy = to.y - from.y, span = Math.hypot(dx, dy);
    const sag = span * slack + (opts.sagPx || 0);
    const pts = [];
    for (let i = 0; i <= seg; i++) {
      const t = i / seg, k = 4 * t * (1 - t);
      pts.push({ x: from.x + dx * t, y: from.y + dy * t + sag * k, t,
                 w: 2 + (opts.thick || 0), shade: 1 - 0.35 * Math.abs(0.5 - t) * 2 });
    }
    return pts;
  }

  const ORDER = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  function contract() {
    const out = { S, elev: DEFAULT_ELEV, dirs: ORDER.slice(), keyline: false, fuels: {}, types: {} };
    for (const k of FUEL_ORDER) out.fuels[k] = { label: FUELS[k].label, dens: FUELS[k].dens, hex: FUELS[k].hex };
    for (const t of TYPE_ORDER) {
      const T = TYPES[t];
      out.types[t] = { label: T.label, kind: T.kind, read: T.read, shape: T.shape, sizes: {} };
      for (const s of sizesOf(t)) {
        const c = cell(t, s), Z = T.sizes[s];
        const e = { label: Z.label, litres: Z.L, tare: Z.tare, cell: { W: c.W, H: c.H }, pivot: { x: c.cx, y: c.cy }, mode: c.mode };
        if (T.kind === 'carry') { const r = cell(t, s, { rest: true }); e.rest = { cell: { W: r.W, H: r.H }, pivot: { x: r.cx, y: r.cy } }; }
        if (Z.L) e.fullKg = +(Z.tare + Z.L * 0.84).toFixed(1);
        out.types[t].sizes[s] = e;
      }
    }
    return out;
  }

  root.FuelIso = {
    S, DEG, defaultElev: DEFAULT_ELEV, KEY, DIRS: 8, order: ORDER,
    TYPES, TYPE_ORDER, FUELS, FUEL_ORDER, SHELL_DEFAULT, sizesOf, resolveSize,
    ramps: { STEEL, GALV, GRAPH, RUST, BRASS, CONC, WHITE, GLASS, DARK },
    cell, mount, render, sheet, level, hosePath, heightFrac, contract,
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
