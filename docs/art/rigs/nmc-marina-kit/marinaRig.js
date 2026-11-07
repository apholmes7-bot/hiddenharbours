/* Hidden Harbours — MARINA RIG · THE PARTS (globalThis.MarinaGeo). Load after the wharf rig pass 2 files (geo, fam, verbs).
   The marina is built on the wharf rig pass 2 and leaves it as it is: with this file loaded, every pass 2 module, harbour and
   sidecar comes out the same. What it adds to WharfGeo2:

     · a fifth float hull, 'composite': anodised aluminium frame, wood-plastic composite boards on 0.145 m, black PE pods,
       a PVC rub rail. Any float or finger takes it, beside pass 2's timber + foam, HDPE cube, concrete pontoon and poly drum
     · the utility run: a marina float carries its fresh-water main and its power conduit along the fascia, and they feed
       pedestals (2 × 32 A + 2 taps + a light dome; a 63 / 125 A 3-phase yacht pedestal), so every berth gets both
     · dock lighting: bollard lights, 4 m lamp posts, pedestal domes and festoon strings, each published as a lamp
     · the security gate: card reader, self-closing mesh leaf, fence panels and anti-climb wings out over the water
     · waste: general and recycling bins, a bottle bank, a bunded waste-oil tank, a bin screen, a sewage pump-out
     · two floating families: 'office' (a dockhouse where the attendants work a counter behind a service hatch) and 'venue'
       (a bar and restaurant under a pavilion, a stage with a lit truss, a dance floor, festoon lights over the tables)

   Every fixture a figure uses is built to character v9.2's world fit (export/character-v9.2-import-kit reports/worldfit.txt,
   every one of the 375 creator bodies within 5 mm) and carries its verb: sockets 0.85 and taps 1.045 (reach), the gate reader
   1.045 (reach), bins 0.93 (toss), the oil tray 0.85 (place), the office counter 0.80 (bench), the hatch shelf 0.95 (reach),
   the bar work top 0.80 (bench) and counter 0.95 (place / reach), tables 0.75 (place), the keyboard 0.80 (bench), the conga 0.72
   (chop), the microphone 1.045 (reach). Seats, the stage step and dancing are asked of the rig (MarinaGeo.REQUESTS).

   Module frame as pass 2: +x along the module, +y its water face, +z up; a float's waterline at z 0. Fixture frame as pass 2:
   the figure's root at (0, 0) facing +y, the fixture ahead of it, placed with rot / x / y in the module.                  */
(function (root) {
  'use strict';
  const G = root.WharfGeo2, V = root.WharfVerbs2;
  if (!G || !G.FAMILIES || !G.resolve || !V) throw new Error('marinaRig.js: load wharfRig2.geo.js, wharfRig2.fam.js and wharfRig2.verbs.js first');
  if (root.MarinaGeo) return;
  const { clamp, hash2, CTX, at, F, wall, slab, box, tube, pipe, beam, decalY, decalZ, xform, cleatAt, bollardAt, foamAt, railRun,
    plankTex, sawnTex, formTex, rustTex, ribTex, zSplit } = G;
  const REACH = V.REACH, GR = REACH.gripRise, WANT = REACH.want, STOW = REACH.rests.stowV;
  const r2 = (v) => Math.round(v * 100) / 100, r3 = (v) => Math.round(v * 1000) / 1000, PI = Math.PI;

  // ============================ MATERIALS =================================================================================
  // six-step ramps, dark → light (makeMats resamples them to the five bands and gives each its tidal frame and its age),
  // and their weather: porosity · gloss · how readily snow stays · class · age law
  const SIX = {
    wpc: ['#3a352f', '#4b443c', '#5d554b', '#71675b', '#867b6d', '#9c9081'], alumF: ['#454c51', '#5a6267', '#71797e', '#899296', '#a3abae', '#bdc4c6'],
    pe: ['#141618', '#1d2023', '#272b2f', '#33383d', '#41474d', '#51585f'], pvc: ['#6b706e', '#838886', '#9ca19e', '#b4b8b4', '#c9ccc7', '#dcded9'],
    navy: ['#141f33', '#1b2a45', '#233758', '#2d466d', '#3a5783', '#4b6a98'], clap: ['#59625f', '#6f7875', '#87908c', '#9fa8a3', '#b7beb9', '#ccd2cd'],
    roofM: ['#1d2124', '#272c30', '#33393e', '#40474d', '#4f575e', '#606970'], win: ['#0e1a1f', '#152429', '#1d3036', '#283e45', '#355057', '#47646b'],
    lamp: ['#6e695c', '#878170', '#a09986', '#b9b29d', '#cfc9b3', '#e2dcc8'], bulb: ['#5c5a52', '#747168', '#8d897e', '#a5a194', '#bcb7a9', '#d2cdbf'],
    blk: ['#0d0e10', '#141619', '#1b1e22', '#24282d', '#2e3339', '#3b4148'], binG: ['#132319', '#1a2f22', '#223d2c', '#2c4c37', '#385d44', '#476f53'],
    binB: ['#122140', '#192c53', '#213967', '#2a477c', '#355791', '#4468a5'], bank: ['#17301f', '#1f3f28', '#285033', '#33633f', '#41764d', '#528a5e'],
    canvas: ['#381616', '#4f1f1d', '#682925', '#83342e', '#9c4238', '#b45446'], danceF: ['#1b1922', '#24212d', '#2e2a39', '#393446', '#454054', '#534d63'],
  };
  const LEDR = ['#15161a', '#1d1f24', '#272a30', '#33373e', '#41464e', '#51575f'];
  for (let k = 0; k < 4; k++) SIX['led' + k] = LEDR;
  const MDEF = { wpc: [0.10, 0.25, 1, 'plastic', 'paint'], alumF: [0.03, 0.70, 0.7, 'metal', 'galv'], pe: [0.02, 0.55, 0.6, 'plastic', null], pvc: [0.03, 0.50, 0.9, 'plastic', 'paint'],
    navy: [0.06, 0.40, 1, 'paint', 'paint'], clap: [0.12, 0.30, 1, 'paint', 'paint'], roofM: [0.02, 0.60, 1, 'metal', 'galv'], win: [0, 0.95, 0.3, 'glass', null],
    lamp: [0, 0.80, 0.5, 'glass', null], bulb: [0, 0.80, 0.4, 'glass', null], blk: [0.10, 0.20, 0.8, 'plastic', null], binG: [0.04, 0.40, 0.9, 'plastic', null],
    binB: [0.04, 0.40, 0.9, 'plastic', null], bank: [0.04, 0.40, 0.9, 'plastic', null], canvas: [0.40, 0.05, 0.9, 'rope', null], danceF: [0.05, 0.45, 1, 'plastic', null] };
  for (let k = 0; k < 4; k++) MDEF['led' + k] = [0, 0.70, 0.5, 'glass', null];
  for (const k in SIX) if (!G.SIX[k]) { G.SIX[k] = SIX[k]; G.MATDEF[k] = MDEF[k]; }
  // what glows after dark: the colour at full night; stage lanterns step through LED on the 16-frame loop, each a quarter out of phase
  const LED = ['#ff4fa3', '#3fd2ff', '#ffb43c', '#a06cff'];
  const EMI = { lamp: { c: '#ffe7ad', k: 1 }, bulb: { c: '#ffd185', k: 1 }, win: { c: '#ffc36a', k: 0.8 } };
  for (let k = 0; k < 4; k++) EMI['led' + k] = { cyc: LED, ph: k, k: 1 };
  // five-band materials that need no tidal frame (the boats' paints), registered by MarinaBoats and merged into every makeMats
  const EXTRA = {};
  function addMat(key, ramp5, p) { if (!EXTRA[key]) EXTRA[key] = Object.assign({ ramp: ramp5.slice(0, 5), por: 0.05, gloss: 0.4, hold: 0.8, cls: 'paint' }, p || {}); return key; }
  const mm0 = G.makeMats;
  G.makeMats = function (s) { const M = mm0(s); for (const k in EXTRA) if (!M[k]) M[k] = Object.assign({ base: k }, EXTRA[k]); return M; };

  // ============================ TEXTURES AND SMALL SHAPES =================================================================
  const clapTex = () => (u, v) => (((v % 0.18) + 0.18) % 0.18) < 0.025 ? -1 : 0;
  const meshTex = () => (u, v) => { const a = ((u % 0.1) + 0.1) % 0.1, b = ((v % 0.1) + 0.1) % 0.1; return a < 0.02 || b < 0.02 ? 0 : -2; };
  const trussTex = () => (u, v) => { const a = (((u + v) % 0.3) + 0.3) % 0.3, b = (((u - v) % 0.3) + 0.3) % 0.3; return a < 0.05 || b < 0.05 ? 0 : -2; };
  const checkTex = (p) => (u, v) => ((Math.floor(u / p) + Math.floor(v / p)) & 1) ? -1 : 0;
  function decalX(out, xv, nx, ys, ye, z0, z1, mat, b, tex, db) { const e = 0.02 * nx, uw = Math.abs(ye - ys), uh = z1 - z0;
    out.push(F([[xv + e, ys, z0], [xv + e, ye, z0], [xv + e, ye, z1], [xv + e, ys, z1]], mat, b || 0, db != null ? db : 0.05, tex ? [[0, 0], [uw, 0], [uw, uh], [0, uh]] : null, tex || null)); }
  function pyramid(out, x, y, z0, rx, ry, h, mat, b) { const P = [[x - rx, y - ry], [x + rx, y - ry], [x + rx, y + ry], [x - rx, y + ry]], ap = [x, y, z0 + h];
    for (let i = 0; i < 4; i++) { const a = P[i], c = P[(i + 1) % 4]; out.push(F([[a[0], a[1], z0], [c[0], c[1], z0], ap], mat, (b || 0) + [-0.2, 0.1, 0.25, -0.05][i])); } }
  // a line that sags between two points; returns the point at t for what hangs on it
  function sagLine(out, a, b, sag, n, mat, r) { const P = (t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t - Math.sin(t * PI) * sag];
    for (let i = 0; i < n; i++) tube(out, P(i / n), P((i + 1) / n), r || 0.012, 4, mat || 'blk', 0, null, false); return P; }
  function lampAt(s, x, y, z, r, i, c, extra) { (s._lamps || (s._lamps = [])).push(Object.assign({ x: r3(x), y: r3(y), z: r3(z), r, i, c }, extra || {})); }
  function record(s, rec) { (s._mfix || (s._mfix = [])).push(rec); return rec; }

  // ============================ FIXTURES (v9.2) ===========================================================================
  const Z_SOCKET = 0.85, Z_TAP = r3(STOW + GR);
  const FIX = {
    pedestal2: { label: 'marina pedestal · 2 × 32 A · 2 taps · light', w: 0.36, d: 0.28, h: 1.27, stand: 0.30, verbs: ['plugShore', 'fillWater'], fit: [['rest', r3(Z_SOCKET - GR)], ['rest', STOW]], power: ['32 A 230 V', '32 A 230 V'], taps: 2 },
    pedestalY: { label: 'yacht pedestal · 63 A + 125 A 3-phase · 2 taps · light', w: 0.56, d: 0.40, h: 1.40, stand: 0.30, verbs: ['plugShore', 'fillWater'], fit: [['rest', r3(Z_SOCKET - GR)], ['rest', STOW]], power: ['63 A 400 V 3-ph', '125 A 400 V 3-ph'], taps: 2 },
    lampPost: { label: 'lamp post · 4 m', w: 0.26, d: 0.26, h: 4.26, stand: 0.02, verbs: [], fit: [] },
    dockLight: { label: 'bollard light · 0.92 m', w: 0.19, d: 0.19, h: 0.92, stand: 0.005, verbs: [], fit: [] },
    gate: { label: 'security gate · card reader · anti-climb wings', w: 0, d: 0, h: 2.1, stand: 0, verbs: ['openGate'], fit: [['rest', STOW]], standOff: [r3(0.78 - WANT[0]), r3(0.23 - WANT[1])] },
    binGeneral: { label: 'waste bin · general', w: 0.62, d: 0.72, h: 0.97, stand: 0.30, verbs: ['binWaste'], fit: [['load', 0.93]] },
    binRecycle: { label: 'waste bin · recycling', w: 0.62, d: 0.72, h: 0.97, stand: 0.30, verbs: ['recycle'], fit: [['load', 0.93]] },
    bottleBank: { label: 'bottle bank', w: 0.9, d: 0.9, h: 1.26, stand: 0.30, verbs: ['recycleGlass'], fit: [['load', 0.93]] },
    oilTank: { label: 'waste-oil tank · bunded', w: 1.0, d: 0.8, h: 1.12, stand: 0.30, verbs: ['oilDrop'], fit: [['load', 0.85]] },
    binScreen: { label: 'bin screen', w: 0, d: 0, h: 1.3, stand: 0, verbs: [], fit: [] },
    pumpOut: { label: 'sewage pump-out', w: 0.68, d: 0.52, h: 1.12, stand: 0.27, verbs: ['pumpOut'], fit: [['rest', STOW]] },
    officeDesk: { label: 'office counter · inside', w: 1.9, d: 0.60, h: 0.80, stand: 0.14, verbs: ['deskWork'], fit: [['bench', 0.80]] },
    serviceHatch: { label: 'office service hatch · shelf', w: 1.6, d: 0.30, h: 0.95, stand: 0.22, verbs: ['checkIn'], fit: [['rest', r3(0.95 - GR)]] },
    barWork: { label: 'bar · work top', w: 1.2, d: 0.45, h: 0.80, stand: 0.14, verbs: ['mixDrinks'], fit: [['bench', 0.80]] },
    barFront: { label: 'bar · counter', w: 1.0, d: 0.40, h: 0.95, stand: 0.22, verbs: ['orderDrink', 'pickUpTray'], fit: [['rest', r3(0.95 - GR)], ['load', 0.95]] },
    kitchenPass: { label: 'kitchen pass', w: 1.1, d: 0.40, h: 0.95, stand: 0.22, verbs: ['collectOrder'], fit: [['load', 0.95]] },
    dineTable: { label: 'dining table · 4 chairs', w: 0.8, d: 0.8, h: 0.75, stand: 0.25, verbs: ['serveTable', 'dine'], fit: [['load', 0.75], ['rest', r3(0.75 - GR)]] },
    highTable: { label: 'standing table · 0.95', w: 0.6, d: 0.6, h: 0.95, stand: 0.20, verbs: ['mingle'], fit: [['rest', r3(0.95 - GR)]] },
    barStool: { label: 'bar stool · seat 0.75', w: 0.36, d: 0.36, h: 0.75, stand: 0.05, verbs: ['perch'], fit: [] },
    micStand: { label: 'microphone on a stand', w: 0, d: 0, h: 1.6, stand: 0.14, verbs: ['sing'], fit: [['rest', STOW]] },
    keysStand: { label: 'keyboard on a stand · 0.80', w: 1.2, d: 0.36, h: 0.80, stand: 0.14, verbs: ['playKeys'], fit: [['bench', 0.80]] },
    conga: { label: 'conga on a stand · 0.72', w: 0.34, d: 0.34, h: 0.72, stand: 0.14, verbs: ['playDrum'], fit: [['knife', 0.72]] },
    danceSpot: { label: 'dance floor', w: 0, d: 0, h: 0, stand: 0, verbs: ['dance'], fit: [] },
  };
  const SEQ = {
    plugShore: () => ({ seq: [{ clip: 'reach', once: true, opts: { lift: r3(Z_SOCKET - GR), want: [WANT[0], WANT[1], Z_SOCKET] }, note: 'the plug into the berth’s socket at 0.85 m' }], note: 'then the cable is walked to the boat’s inlet: UTILITIES gives the run' }),
    fillWater: () => ({ seq: [{ clip: 'reach', once: true, opts: { rest: 'stowV' } }], note: 'the hand is on the tap from 0.62 of the clip; the hose goes to the boat’s fill (UTILITIES)' }),
    openGate: () => ({ seq: [{ clip: 'reach', once: true, opts: { rest: 'stowV' }, note: 'the card to the reader at 1.045 m' }, { clip: 'walk', advance: { m: 1.6 }, note: 'the leaf swings in on its closer; walk through the opening' }], note: 'the leaf shuts itself 4 s after it opens' }),
    binWaste: () => ({ seq: [{ clip: 'toss', once: true, opts: { workZ: 0.93 }, note: 'the bag over the rim at tossRelease 0.52' }], carry: 'walk+buckets', request: 'carryBag' }),
    recycle: () => ({ seq: [{ clip: 'toss', once: true, opts: { workZ: 0.93 } }], carry: 'walk+buckets', request: 'carryBag' }),
    recycleGlass: () => ({ seq: [{ clip: 'toss', once: true, opts: { workZ: 0.93 }, note: 'through the port at 0.93 m' }] }),
    oilDrop: () => ({ seq: [{ clip: 'place', once: true, opts: { workZ: 0.85 }, note: 'the can on the funnel tray' }] }),
    pumpOut: () => ({ seq: [{ clip: 'reach', once: true, opts: { rest: 'stowV' }, note: 'the nozzle off its holster' }, { clip: 'walk', note: 'the hose paid out to the berth, 15 m on the reel' }, { clip: 'reach', once: true, opts: { lift: 0 }, note: 'the nozzle on the deck fitting' }, { clip: 'idle', loop: true, until: 'the holding tank is empty' }] }),
    deskWork: () => ({ seq: [{ clip: 'bench', loop: true, opts: { workZ: 0.80 } }], note: 'the attendant at the counter, facing the hatch' }),
    checkIn: () => ({ seq: [{ clip: 'reach', once: true, opts: { lift: r3(0.95 - GR) }, note: 'papers and the gate card across the shelf' }, { clip: 'idle', loop: true }] }),
    mixDrinks: () => ({ seq: [{ clip: 'bench', loop: true, opts: { workZ: 0.80 } }] }),
    orderDrink: () => ({ seq: [{ clip: 'idle', loop: true }, { clip: 'reach', once: true, opts: { lift: r3(0.95 - GR) }, note: 'the glass off the counter' }] }),
    pickUpTray: () => ({ seq: [{ clip: 'place', once: true, reverse: true, opts: { workZ: 0.95 }, note: 'the tray off the counter' }, { clip: 'walk+tray', note: 'to the table' }] }),
    collectOrder: () => ({ seq: [{ clip: 'place', once: true, reverse: true, opts: { workZ: 0.95 } }, { clip: 'walk+tray' }] }),
    serveTable: () => ({ seq: [{ clip: 'walk+tray' }, { clip: 'place', once: true, opts: { workZ: 0.75 }, note: 'the plates down' }] }),
    dine: () => ({ seq: [{ clip: 'idle', loop: true, request: 'sitChair', note: 'seated at 0.45 m when the rig has it; standing at the table until then' }, { clip: 'reach', once: true, opts: { lift: r3(0.75 - GR) } }] }),
    mingle: () => ({ seq: [{ clip: 'idle', loop: true }, { clip: 'reach', once: true, opts: { lift: r3(0.95 - GR) }, note: 'the glass on the table' }] }),
    perch: () => ({ seq: [{ clip: 'idle', loop: true, request: 'sitStool', note: 'perched at 0.75 m when the rig has it; standing at the bar until then' }] }),
    sing: () => ({ seq: [{ clip: 'reach', once: true, opts: { rest: 'stowV' }, note: 'the hand settles on the microphone at 1.045 m and holds' }], request: 'sing' }),
    playKeys: () => ({ seq: [{ clip: 'bench', loop: true, opts: { workZ: 0.80 } }] }),
    playDrum: () => ({ seq: [{ clip: 'chop', loop: true, opts: { workZ: 0.72 }, note: 'the hands fall on the drum head' }] }),
    dance: () => ({ seq: [{ clip: 'balance', loop: true, note: 'arms out, swaying: the dance until the rig has one' }], request: 'dance' }),
  };
  const REQUESTS = [
    { clip: 'sitChair', why: 'the restaurant’s chairs (seat 0.45 m) and the office chair: v9.2’s only seat is the cab’s drive at 0.25–0.29 m', now: 'diners stand at their table (idle, reach to 0.75); the chairs are published with the seat height' },
    { clip: 'sitStool', why: 'bar stools, seat 0.75 m', now: 'stand at the counter (idle; reach to the 0.95 top)' },
    { clip: 'dance', why: 'the dance floor', now: 'balance, looped: arms out and swaying' },
    { clip: 'sing · strum', why: 'performers: the singer holds the microphone, a guitarist has no pose', now: 'reach settles the hand on the microphone at 1.045 m; the keys are bench at 0.80, the conga chop at 0.72' },
    { clip: 'stairs', why: 'the stage is 0.45 m up three treads; v9.2 walks on a level', now: 'the stage step is boardDown played backward (0.06–1.30 fits every creator body), treads drawn for the eye' },
    { clip: 'carryBag', why: 'rubbish to the bins', now: 'walk+buckets, the pails as the bags' },
  ];

  // ---- the builders (fixture frame, z = the deck) ----
  function pedestal(out, z, g, P, sock) {
    const cx = WANT[0], hw = P.w / 2, y0 = P.stand, y1 = y0 + P.d, hb = P.h - 0.25; at('pedestal', g, 1);
    box(out, cx - hw - 0.03, cx + hw + 0.03, y0 - 0.03, y1 + 0.03, z, z + 0.05, 'alumF', -0.4, null, true);
    box(out, cx - hw, cx + hw, y0, y1, z + 0.05, z + hb, 'pvc', 0, null, true);
    decalY(out, y0, -1, cx - hw, cx + hw, z + hb - 0.10, z + hb, 'navy', 0.1, null, 0.02);
    box(out, cx - hw - 0.015, cx + hw + 0.015, y0 - 0.015, y1 + 0.015, z + hb, z + hb + 0.06, 'navy', 0.25, null, false);
    box(out, cx - hw + 0.05, cx + hw - 0.05, y0 + 0.05, y1 - 0.05, z + hb + 0.06, z + P.h - 0.04, 'lamp', 0.35, null, true);
    box(out, cx - hw, cx + hw, y0, y1, z + P.h - 0.04, z + P.h, 'navy', 0.3, null, false);
    decalY(out, y0, -1, cx - hw * 0.6, cx + hw * 0.6, z + 0.52, z + 0.66, 'win', 0.3, null, 0.02);
    const n = P.power.length; for (let k = 0; k < n; k++) { const x = cx + (k - (n - 1) / 2) * Math.min(0.18, P.w * 0.5); box(out, x - 0.04, x + 0.04, WANT[1], y0, z + Z_SOCKET - 0.05, z + Z_SOCKET + 0.05, sock, 0.25, null, false); }
    for (const sx of [-1, 1]) { const x = cx + sx * (hw - 0.05); tube(out, [x, y0, z + Z_TAP], [x, WANT[1], z + Z_TAP], 0.014, 6, 'galv', 0.3, null, true); pipe(out, x, WANT[1] + 0.03, 0.02, z + Z_TAP + 0.012, z + Z_TAP + 0.05, 'binB', 0.3, 6); }
  }
  function binBody(out, z, g, mat, lid) { at('bin', g, 1); const x0 = -0.30, x1 = 0.30, y0 = 0.30, y1 = 1.0;
    box(out, x0, x1, y0 + 0.05, y1 - 0.05, z, z + 0.08, mat, -0.6, null, true);
    box(out, x0, x1, y0, y1, z + 0.08, z + 0.92, mat, 0, null, true);
    box(out, x0 - 0.02, x1 + 0.02, y0 - 0.03, y1 + 0.02, z + 0.92, z + 0.97, lid || mat, 0.3, null, false);
    box(out, x0 + 0.05, x1 - 0.05, y1, y1 + 0.05, z + 0.86, z + 0.92, mat, 0.1, null, false);
    for (const sx of [-1, 1]) tube(out, [sx * 0.22, y1 - 0.06, z + 0.08], [sx * 0.30, y1 - 0.06, z + 0.08], 0.08, 8, 'rubber', 0, null, true); }
  const BUILD = {
    pedestal2(out, z, g) { pedestal(out, z, g, FIX.pedestal2, 'poly'); },
    pedestalY(out, z, g) { pedestal(out, z, g, FIX.pedestalY, 'red'); },
    lampPost(out, z, g) { at('lamppost', g, 1); const x = 0, y = 0.15;
      pipe(out, x, y, 0.13, z, z + 0.22, 'navy', 0, 10); pipe(out, x, y, 0.05, z + 0.22, z + 3.82, 'navy', 0.1, 8);
      box(out, x - 0.07, x + 0.07, y - 0.07, y + 0.07, z + 3.78, z + 3.84, 'navy', 0.2, null, false);
      box(out, x - 0.12, x + 0.12, y - 0.12, y + 0.12, z + 3.84, z + 4.10, 'lamp', 0.3, null, true);
      for (const [dx, dy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) box(out, x + dx * 0.12 - 0.015, x + dx * 0.12 + 0.015, y + dy * 0.12 - 0.015, y + dy * 0.12 + 0.015, z + 3.84, z + 4.10, 'navy', 0, null, true);
      pyramid(out, x, y, z + 4.10, 0.16, 0.16, 0.16, 'navy', 0.2); },
    dockLight(out, z, g) { at('docklight', g, 1); pipe(out, 0, 0.1, 0.08, z, z + 0.70, 'navy', 0, 10); pipe(out, 0, 0.1, 0.075, z + 0.70, z + 0.86, 'lamp', 0.25, 10); pipe(out, 0, 0.1, 0.095, z + 0.86, z + 0.92, 'navy', 0.3, 10); },
    gate(out, z, g, it) { at('gate', g, 1); const y = 0.30, H = 2.05, op = clamp(it && it.open != null ? +it.open : 0, 0, 1), a = op * PI / 2, ca = Math.cos(a), sa = Math.sin(a);
      for (const x of [-0.64, 0.64]) box(out, x - 0.045, x + 0.045, y - 0.045, y + 0.045, z, z + H + 0.05, 'alumF', 0.1, null, false);
      const P = (u) => [-0.6 + u * ca, y - u * sa], A = P(0), B = P(1.2);                // the leaf hinges on the left post and swings in, onto the quay
      out.push(F([[A[0], A[1], z + 0.06], [B[0], B[1], z + 0.06], [B[0], B[1], z + H - 0.04], [A[0], A[1], z + H - 0.04]], 'alumF', 0.05, 0, [[0, 0], [1.2, 0], [1.2, H], [0, H]], meshTex()));
      for (const zz of [0.06, H / 2, H - 0.04]) tube(out, [A[0], A[1], z + zz], [B[0], B[1], z + zz], 0.022, 6, 'alumF', 0.3, null, true);
      tube(out, [B[0], B[1], z + 0.06], [B[0], B[1], z + H - 0.04], 0.022, 6, 'alumF', 0.3, null, true);
      const S1 = P(0.30), S2 = P(0.90); out.push(F([[S1[0], S1[1] - 0.02, z + 1.30], [S2[0], S2[1] - 0.02, z + 1.30], [S2[0], S2[1] - 0.02, z + 1.62], [S1[0], S1[1] - 0.02, z + 1.62]], 'board', 0.3, 0.03));
      for (const [x0, x1] of [[-1.6, -0.69], [0.69, 1.6]]) { wall(out, x0, y, x1, y, z + 0.04, z + H, 'alumF', meshTex(), 0);
        tube(out, [x0, y, z + H], [x1, y, z + H], 0.024, 6, 'alumF', 0.3, null, true); tube(out, [x0, y, z + 0.04], [x1, y, z + 0.04], 0.024, 6, 'alumF', 0.1, null, true); }
      for (const sx of [-1, 1]) { const x = sx * 1.6; box(out, x - 0.04, x + 0.04, y - 0.04, y + 0.04, z, z + H + 0.05, 'alumF', 0.1, null, false);
        wall(out, x, y, x, y + 1.15, z + 0.04, z + H, 'alumF', meshTex(), sx > 0 ? -0.15 : -0.3);                     // the anti-climb wing, out over the water
        tube(out, [x, y + 1.15, z + 0.04], [x, y + 1.15, z + H], 0.03, 6, 'alumF', 0.2, null, true); tube(out, [x, y, z + H], [x, y + 1.15, z + H], 0.024, 6, 'alumF', 0.3, null, true);
        for (let k = 0; k < 7; k++) { const t = k / 6 * PI / 2; tube(out, [x, y + 1.15, z + H], [x, y + 1.15 + Math.sin(t) * 0.42, z + H + Math.cos(t) * 0.42], 0.012, 4, 'galv', 0.3, null, false); } }
      box(out, 0.74, 0.82, y - 0.06, y, z, z + 1.12, 'navy', 0, null, false);                                                   // the reader post
      box(out, 0.70, 0.86, y - 0.10, y - 0.04, z + 0.98, z + 1.10, 'blk', 0.2, null, false); decalY(out, y - 0.10, -1, 0.75, 0.81, z + 1.03, z + 1.07, 'lamp', 0.3, null, 0.02);
      box(out, 0.58, 0.70, y - 0.08, y + 0.08, z + H + 0.05, z + H + 0.21, 'lamp', 0.3, null, true); box(out, 0.56, 0.72, y - 0.10, y + 0.10, z + H + 0.21, z + H + 0.25, 'navy', 0.3, null, false); },
    binGeneral(out, z, g) { binBody(out, z, g, 'binG'); },
    binRecycle(out, z, g) { binBody(out, z, g, 'binB', 'yel'); },
    bottleBank(out, z, g) { at('bin', g, 1); pipe(out, 0, 0.75, 0.45, z, z + 1.12, 'bank', 0, 12); pipe(out, 0, 0.75, 0.30, z + 1.12, z + 1.26, 'bank', 0.3, 10); box(out, -0.09, 0.09, 0.28, 0.32, z + 0.86, z + 1.00, 'blk', 0.2, null, false); },
    oilTank(out, z, g) { at('oil', g, 1); box(out, -0.5, 0.5, 0.30, 1.10, z, z + 0.24, 'blk', -0.2, null, true);
      box(out, -0.42, 0.42, 0.40, 1.02, z + 0.24, z + 1.05, 'yel', 0, null, false); decalY(out, 0.40, -1, -0.42, 0.42, z + 0.30, z + 0.42, 'blk', 0.1, null, 0.02);
      box(out, -0.24, 0.24, 0.30, 0.42, z + 0.80, z + 0.85, 'galv', 0.2, null, false); pipe(out, 0, 0.5, 0.07, z + 1.05, z + 1.12, 'blk', 0.3, 8); },
    binScreen(out, z, g, it) { at('screen', g, 0); const hw = ((it && it.w) || 3.6) / 2;
      for (const x of [-hw, 0, hw]) box(out, x - 0.05, x + 0.05, -0.05, 0.05, z, z + 1.3, 'wood', 0.1, sawnTex(), false);
      for (let k = 0; k < 9; k++) { const zz = z + 0.1 + k * 0.135; box(out, -hw, hw, -0.02, 0.02, zz, zz + 0.09, 'wood', 0, sawnTex(), false); } },
    pumpOut(out, z, g) { at('pumpout', g, 1); box(out, -0.31, 0.31, 0.30, 0.76, z, z + 1.05, 'yel', 0, null, true); box(out, -0.34, 0.34, 0.27, 0.79, z + 1.05, z + 1.12, 'navy', 0.3, null, false);
      decalY(out, 0.30, -1, -0.2, 0.2, z + 0.62, z + 0.80, 'navy', 0.1, null, 0.02);
      tube(out, [0.31, 0.53, z + 0.70], [0.44, 0.53, z + 0.70], 0.26, 12, 'blk', 0.1, null, true);
      box(out, WANT[0] - 0.05, WANT[0] + 0.05, WANT[1], 0.30, z + Z_TAP - 0.07, z + Z_TAP + 0.05, 'blk', 0.2, null, false); },
  };
  // the lamps each fixture carries, in its own frame
  const LAMPS = {
    pedestal2: [{ x: WANT[0], y: 0.44, z: 1.14, r: 3.0, i: 0.55, c: '#fff0d2' }], pedestalY: [{ x: WANT[0], y: 0.50, z: 1.26, r: 3.4, i: 0.65, c: '#fff0d2' }],
    lampPost: [{ x: 0, y: 0.15, z: 3.97, r: 8.5, i: 1.0, c: '#ffd89c' }], dockLight: [{ x: 0, y: 0.1, z: 0.78, r: 3.6, i: 0.75, c: '#ffe2b0' }],
    gate: [{ x: 0.64, y: 0.30, z: 2.18, r: 5.0, i: 0.9, c: '#ffe7c0' }], pumpOut: [{ x: 0, y: 0.5, z: 1.3, r: 2.0, i: 0.35, c: '#fff0d2' }],
  };
  // place a fixture in a module: build it in its frame, move it, record it (gameplay turns records into FIXTURES and VERBS)
  function place(out, s, g, top, it) {
    const B = BUILD[it.kind] || V.BUILD[it.kind]; if (!B) return null;
    const tmp = [], z = it.z != null ? it.z : top, rot = it.rot || 0, c = Math.cos(rot), sn = Math.sin(rot); CTX.grp = g;
    B(tmp, z, g, it, s); xform(tmp, rot, it.x, it.y, 0); for (const f of tmp) out.push(f);
    const rec = record(s, { id: it.id || (it.kind + ((s._mfix || []).length)), kind: it.kind, x: r3(it.x), y: r3(it.y), rot: r3(rot), z: r3(z), grp: g, serves: it.serves || null, meta: it.meta || null });
    for (const l of (LAMPS[it.kind] || [])) lampAt(s, it.x + l.x * c - l.y * sn, it.y + l.x * sn + l.y * c, z + l.z, l.r, l.i, l.c, { kind: it.kind, fixture: rec.id });
    return rec;
  }

  // ============================ THE COMPOSITE FLOAT ========================================================================
  // aluminium frame, WPC boards on 0.145 m, black PE pods under it, a PVC rub rail. The float's own tail (swim ladder, guide piles
  // and hoops, the anchor block and chain) is pass 2's, taken from its builder so it stays pass 2's.
  const KEEP = { ladder: 1, guide: 1, hoop: 1, anchor: 1 };
  function compositeBody(out, s, T, g) {
    const L = s.L, hx = L / 2, hy = s.W / 2, top = s.freeboard, FT = G.floatFrame(T); CTX.ux = hx; at('float', g, 0);
    const np = Math.max(1, Math.round(L / 1.5)), rows = s.W >= 1.6 ? [-hy + 0.42, hy - 0.42] : [0], pw = s.W >= 1.6 ? 0.62 : Math.min(0.8, s.W - 0.3);
    for (let i = 0; i < np; i++) { const cx = -hx + (L / np) * (i + 0.5), a = cx - (L / np) * 0.42, b = cx + (L / np) * 0.42;
      for (const ry of rows) for (const zz of zSplit(-0.22, 0.22, 'pe', FT, s)) box(out, a, b, ry - pw / 2, ry + pw / 2, zz.a, zz.b, zz.mat, -0.2, null, true); }
    const sides = [[-hx, hy, hx, hy, 0.05], [hx, -hy, -hx, -hy, -0.5]]; if (s.ends.px !== 'join') sides.push([hx, hy, hx, -hy, -0.15]); if (s.ends.nx !== 'join') sides.push([-hx, -hy, -hx, hy, -0.3]);
    for (const zz of zSplit(0.22, top - 0.03, 'alumF', FT, s)) for (const [x0, y0, x1, y1, b] of sides) wall(out, x0, y0, x1, y1, zz.a, zz.b, zz.mat, null, b);
    slab(out, [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]], 0.22, 'alumF', -0.9);
    at('deck', g, 0); slab(out, [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]], top, 'wpc', 0.1, plankTex(0.145));
    at('float', g, 0); for (const sy of [-1, 1]) box(out, -hx, hx, sy > 0 ? hy : -hy - 0.05, sy > 0 ? hy + 0.05 : -hy, top - 0.17, top - 0.06, 'pvc', 0.1, null, false);
    if (s.ends.px !== 'join') box(out, hx, hx + 0.05, -hy, hy, top - 0.17, top - 0.06, 'pvc', 0, null, false);
    if (s.ends.nx !== 'join') box(out, -hx - 0.05, -hx, -hy, hy, top - 0.17, top - 0.06, 'pvc', -0.2, null, false);
    if (s.curb === 'yellow') { at('deck', g, 0); for (const sy of [-1, 1]) decalZ(out, top + 0.003, -hx + 0.02, hx - 0.02, sy > 0 ? hy - 0.10 : -hy + 0.02, sy > 0 ? hy - 0.02 : -hy + 0.10, 'yel', 0.1, null, 0.02); }
    if (s.landing != null) { at('deck', g, 0); decalZ(out, top + 0.006, s.landing - 0.3, s.landing + 1.3, -0.7, 0.7, 'galv', 0, rustTex(), 0.03); }
    at('float', g, 0);
  }
  function withComposite(orig) { return function (out, s, T) { if (s.hull !== 'composite') return orig.call(this, out, s, T);
    const tmp = [], h = s.hull; s.hull = 'timber'; try { orig.call(this, tmp, s, T); } finally { s.hull = h; }
    for (const f of tmp) if (KEEP[f.part]) out.push(f); compositeBody(out, s, T, s.grp || 1); }; }
  G.FAMILIES.float.build = withComposite(G.FAMILIES.float.build); G.FAMILIES.finger.build = withComposite(G.FAMILIES.finger.build);
  const FREEBOARD = { timber: 0.45, plastic: 0.32, composite: 0.48, concrete: 0.50, drum: 0.45 };
  const HULL_LABEL = { timber: 'timber frame on foam billets', plastic: 'HDPE cubes', composite: 'aluminium frame, WPC boards, PE pods', concrete: 'concrete pontoon', drum: 'poly drums' };

  // the utility run along a float's fascia: the fresh-water main (blue) over the power conduit, clipped every 1.5 m
  function conduits(out, s, g, top, sides) { const hx = s.L / 2, hy = s.W / 2; at('utility', g, 0);
    for (const face of sides) { const sy = face === 'water' ? 1 : -1, y = sy * (hy + 0.055), x0 = -hx + (s.ends.nx === 'join' ? 0 : 0.1), x1 = hx - (s.ends.px === 'join' ? 0 : 0.1);
      tube(out, [x0, y, top - 0.21], [x1, y, top - 0.21], 0.034, 6, 'poly', 0.1, null, true);
      tube(out, [x0, y + sy * 0.012, top - 0.29], [x1, y + sy * 0.012, top - 0.29], 0.04, 6, 'pvc', -0.1, null, true);
      for (let x = x0 + 0.4; x < x1; x += 1.5) box(out, x - 0.02, x + 0.02, Math.min(sy * hy, y + sy * 0.05), Math.max(sy * hy, y + sy * 0.05), top - 0.33, top - 0.17, 'galv', 0.1, null, true); } }

  // ============================ THE OFFICE ================================================================================
  // a dockhouse on its own pontoon: windows round, a door and a service hatch in the front (+y), the counter inside the hatch
  // and a shelf outside it, a gable roof in standing-seam metal that overhangs the hatch, rails round the pontoon's free edges
  function officeHouse(out, s, T, g) {
    const hx = s.L / 2, hy = s.W / 2, top = s.freeboard, bx0 = -hx + 0.85, bx1 = hx - 0.85, by0 = -hy + 0.70, fy = hy - 1.15, Hw = 2.45, zt = top + Hw;
    const hx0 = -1.05, hx1 = 0.55, hz0 = top + 0.95, hz1 = top + 2.02, dx0 = 1.25, dx1 = 2.10, OB = 'office';
    CTX.ux = hx; at(OB, g, 0);
    slab(out, [[bx0, by0], [bx1, by0], [bx1, fy], [bx0, fy]], top + 0.015, 'plank', 0, plankTex(0.12));
    const Wl = (xa, ya, xb, yb, za, zb, b) => wall(out, xa, ya, xb, yb, za, zb, 'clap', clapTex(), b || 0);
    for (const [a, b] of [[bx0, hx0], [hx1, dx0], [dx1, bx1]]) Wl(a, fy, b, fy, top, zt, 0.05);
    Wl(hx0, fy, hx1, fy, top, hz0, 0.05); Wl(hx0, fy, hx1, fy, hz1, zt, 0.05); Wl(dx0, fy, dx1, fy, top + 2.05, zt, 0.05);
    Wl(bx1, by0, bx0, by0, top, zt, -0.5); Wl(bx0, by0, bx0, fy, top, zt, -0.3); Wl(bx1, fy, bx1, by0, top, zt, -0.15);
    at(OB, g, 0); wall(out, dx0, fy - 0.05, dx1, fy - 0.05, top, top + 2.05, 'navy', null, 0.1);                                  // the door, shut
    decalY(out, fy - 0.05, 1, dx0 + 0.2, dx1 - 0.2, top + 1.25, top + 1.85, 'win', 0.2, null, 0.02);
    box(out, dx1 - 0.2, dx1 - 0.14, fy - 0.02, fy + 0.02, top + 1.0, top + 1.08, 'galv', 0.3, null, false);
    for (const [a, b] of [[bx0, hx0], [hx1, dx0], [dx1, bx1]]) decalY(out, fy, 1, a, b, top, top + 0.32, 'navy', 0, null, 0.02);
    decalY(out, fy, 1, hx0, hx1, top, top + 0.32, 'navy', 0, null, 0.02);
    decalY(out, fy, 1, bx0 + 0.3, hx0 - 0.28, top + 1.0, top + 2.0, 'win', 0.25, null, 0.03);
    decalY(out, fy, 1, dx1 + 0.12, bx1 - 0.18, top + 1.0, top + 2.0, 'win', 0.25, null, 0.03);
    decalY(out, by0, -1, bx0 + 0.5, bx0 + 2.0, top + 1.0, top + 2.0, 'win', 0.2, null, 0.03); decalY(out, by0, -1, bx1 - 2.0, bx1 - 0.5, top + 1.0, top + 2.0, 'win', 0.2, null, 0.03);
    decalX(out, bx0, -1, by0 + 0.5, fy - 0.5, top + 1.0, top + 2.0, 'win', 0.2, null, 0.03); decalX(out, bx1, 1, by0 + 0.5, fy - 0.5, top + 1.0, top + 2.0, 'win', 0.2, null, 0.03);
    for (const [x, y] of [[bx0, fy], [bx1, fy], [bx0, by0], [bx1, by0]]) box(out, x - 0.05, x + 0.05, y - 0.05, y + 0.05, top, zt, 'pvc', 0.2, null, true);
    for (const x of [hx0, hx1]) box(out, x - 0.04, x + 0.04, fy - 0.04, fy + 0.04, top + 0.9, hz1 + 0.05, 'pvc', 0.25, null, true);
    box(out, hx0 - 0.04, hx1 + 0.04, fy - 0.04, fy + 0.04, hz1, hz1 + 0.06, 'pvc', 0.25, null, true);
    box(out, hx0 - 0.08, hx1 + 0.08, fy, fy + 0.30, top + 0.91, top + 0.95, 'wood', 0.25, null, false);                     // the shelf outside
    for (const x of [hx0 + 0.1, hx1 - 0.1]) beam(out, [x, fy, top + 0.70], [x, fy + 0.26, top + 0.91], 0.02, 'galv', 0, 4);
    decalY(out, fy, 1, hx0 - 0.12, dx0 + 0.25, top + 2.07, top + 2.37, 'navy', 0.2, null, 0.03); decalY(out, fy, 1, hx0 - 0.02, dx0 + 0.15, top + 2.17, top + 2.25, 'pvc', 0.3, null, 0.04);
    // inside: the counter under the hatch with a screen on it, shelves on the back wall, a board
    at('officeIn', g, 0); box(out, hx0 - 0.2, hx1 + 0.2, fy - 0.64, fy - 0.06, top, top + 0.76, 'pvc', -0.2, null, true);
    box(out, hx0 - 0.23, hx1 + 0.23, fy - 0.67, fy - 0.04, top + 0.76, top + 0.80, 'wood', 0.2, null, false);
    box(out, hx0 + 0.25, hx0 + 0.75, fy - 0.50, fy - 0.44, top + 0.80, top + 1.14, 'blk', 0, null, false); decalY(out, fy - 0.44, 1, hx0 + 0.29, hx0 + 0.71, top + 0.86, top + 1.10, 'win', 0.3, null, 0.02);
    box(out, bx0 + 0.4, bx1 - 0.4, by0 + 0.02, by0 + 0.36, top + 1.05, top + 1.95, 'wood', -0.3, null, false);
    decalY(out, by0 + 0.36, 1, bx0 + 0.6, bx0 + 1.8, top + 1.2, top + 1.8, 'board', 0.2, null, 0.02);
    // the roof: gable along x, the long overhang to the front over the hatch
    const yr = (by0 + fy) / 2, ey0 = by0 - 0.45, ey1 = fy + 0.62, ze = zt - 0.02, zr = zt + 1.0, rx0 = bx0 - 0.3, rx1 = bx1 + 0.3;
    const rz = (y) => y >= yr ? ze + (zr - ze) * (ey1 - y) / (ey1 - yr) : ze + (zr - ze) * (y - ey0) / (yr - ey0);
    at('roof', g, 1);
    out.push(F([[rx0, ey1, ze], [rx1, ey1, ze], [rx1, yr, zr], [rx0, yr, zr]], 'roofM', 0.05, 0, [[rx0, 0], [rx1, 0], [rx1, 1], [rx0, 1]], ribTex(0.34)));
    out.push(F([[rx1, ey0, ze], [rx0, ey0, ze], [rx0, yr, zr], [rx1, yr, zr]], 'roofM', 0.05, 0, [[rx1, 0], [rx0, 0], [rx0, 1], [rx1, 1]], ribTex(0.34)));
    for (const bx of [bx0, bx1]) out.push(F([[bx, by0, zt], [bx, by0, rz(by0)], [bx, yr, zr - 0.02], [bx, fy, rz(fy)], [bx, fy, zt]], 'clap', bx < 0 ? -0.3 : -0.15, 0, null, null));
    box(out, rx0, rx1, ey1, ey1 + 0.05, ze - 0.14, ze, 'pvc', 0.2, null, false); box(out, rx0, rx1, ey0 - 0.05, ey0, ze - 0.14, ze, 'pvc', -0.3, null, false);
    box(out, rx0, rx1, yr - 0.05, yr + 0.05, zr - 0.02, zr + 0.05, 'navy', 0.3, null, false);
    tube(out, [bx1 - 0.6, yr, zr], [bx1 - 0.6, yr, zr + 2.4], 0.018, 5, 'pvc', 0.3, null, true);
    // rails round the pontoon's free edges; the joined end stays open
    at('rail', g, 1);
    railRun(out, -hx + 0.1, hy - 0.08, hx - 0.1, hy - 0.08, top, 'pipe'); railRun(out, -hx + 0.1, -hy + 0.08, hx - 0.1, -hy + 0.08, top, 'pipe');
    if (s.ends.px !== 'join') railRun(out, hx - 0.08, -hy + 0.1, hx - 0.08, hy - 0.1, top, 'pipe');
    if (s.ends.nx !== 'join') railRun(out, -hx + 0.08, -hy + 0.1, -hx + 0.08, hy - 0.1, top, 'pipe');
    at(OB, g, 0); box(out, (dx0 + dx1) / 2 - 0.08, (dx0 + dx1) / 2 + 0.08, fy, fy + 0.12, top + 2.12, top + 2.30, 'lamp', 0.3, null, true);
    lampAt(s, (dx0 + dx1) / 2, fy + 0.10, top + 2.2, 4.2, 0.85, '#ffe2b0', { kind: 'doorLamp' });
    lampAt(s, (hx0 + hx1) / 2, (by0 + fy) / 2, top + 2.3, 3.4, 0.8, '#ffe9c4', { kind: 'officeLight' });
    record(s, { id: 'officeDesk', kind: 'officeDesk', x: r3((hx0 + hx1) / 2), y: r3(fy - 0.64 - FIX.officeDesk.stand), rot: 0, z: r3(top), grp: g, meta: { role: 'attendant' } });
    record(s, { id: 'serviceHatch', kind: 'serviceHatch', x: r3((hx0 + hx1) / 2), y: r3(fy + 0.30 + FIX.serviceHatch.stand), rot: r3(PI), z: r3(top), grp: g, meta: { role: 'boater' } });
    s._office = { building: [[bx0, by0], [bx1, by0], [bx1, fy], [bx0, fy]], door: [[dx0, fy], [dx1, fy]], hatch: [[hx0, fy], [hx1, fy]], hatchZ: [r3(hz0 - top), r3(hz1 - top)], walls: r2(Hw), ridge: r2(zr - top), top };
    return s._office;
  }

  // ============================ THE VENUE =================================================================================
  // an 18 × 12 m concrete pontoon: the bar under a pavilion at the back of the west half with the tables in front of it under
  // festoon strings; the stage at the back of the east half, 0.45 m up three treads, a lit truss over it, the dance floor in front
  function venueFit(out, s, T, g) {
    const hx = s.L / 2, hy = s.W / 2, top = s.freeboard, gx = s.gangX != null ? s.gangX : -3, VB = 'venue', rec = (o) => record(s, Object.assign({ z: r3(top), grp: g }, o));
    CTX.ux = hx;
    // ---- the pavilion over the bar ----
    const px0 = -hx + 0.3, px1 = -0.6, py0 = 2.0, py1 = hy - 0.3, rzP = top + 3.05;
    at('pavilion', g, 1);
    for (const x of [px0, (px0 + px1) / 2, px1]) for (const y of [py0, py1]) box(out, x - 0.09, x + 0.09, y - 0.09, y + 0.09, top, rzP, 'wood', 0.1, sawnTex(), false);
    box(out, px0 - 0.35, px1 + 0.35, py0 - 0.55, py1 + 0.35, rzP, rzP + 0.16, 'roofM', 0.2, null, false);
    decalY(out, py0 - 0.55, -1, px0 - 0.35, px1 + 0.35, rzP - 0.22, rzP + 0.16, 'canvas', 0.1, null, 0.02);
    for (let x = px0 - 0.2; x < px1 + 0.2; x += 0.6) out.push(F([[x, py0 - 0.56, rzP - 0.22], [x + 0.6, py0 - 0.56, rzP - 0.22], [x + 0.3, py0 - 0.56, rzP - 0.40]], 'canvas', -0.1, 0.02));
    // ---- the bar: a counter to the front (0.95) with a work shelf behind it (0.80), a back bar ----
    at('bar', g, 1); const cx0 = -7.6, cx1 = -2.0, cyF = 2.55, cyB = 3.0, cyW = 3.42;
    box(out, cx0, cx1, cyF, cyB, top, top + 0.91, 'wood', 0, sawnTex(), false); box(out, cx0 - 0.05, cx1 + 0.05, cyF - 0.06, cyB, top + 0.91, top + 0.95, 'board', 0.3, null, false);
    box(out, cx0, cx1, cyB, cyW, top, top + 0.76, 'pvc', -0.4, null, false); box(out, cx0, cx1, cyB, cyW, top + 0.76, top + 0.80, 'galv', 0.1, null, false);
    for (let x = cx0 + 0.9; x < cx1 - 0.5; x += 1.4) for (const dx of [0, 0.12, 0.24]) tube(out, [x + dx, cyB - 0.08, top + 0.95], [x + dx, cyB - 0.08, top + 1.22], 0.016, 5, 'galv', 0.3, null, true);
    box(out, cx0, cx1, py1 - 0.55, py1 - 0.1, top, top + 0.9, 'wood', -0.3, null, false); box(out, cx0, cx1, py1 - 0.4, py1 - 0.1, top + 1.1, top + 2.2, 'wood', -0.5, null, false);
    for (let x = cx0 + 0.15, k = 0; x < cx1 - 0.1; x += 0.13, k++) { const m = ['bank', 'win', 'binB', 'canvas'][k % 4]; for (const zz of [1.12, 1.62]) box(out, x, x + 0.07, py1 - 0.48, py1 - 0.41, top + zz, top + zz + 0.26, m, 0.3, null, false); }
    decalY(out, py1 - 0.4, -1, cx0 + 1.8, cx0 + 3.6, top + 2.3, top + 2.7, 'led0', 0.3, null, 0.02);                             // the bar's sign
    for (const x of [-6.6, -4.8, -3.0]) { beam(out, [x, (cyF + cyW) / 2, rzP], [x, (cyF + cyW) / 2, top + 2.55], 0.01, 'blk', 0, 4); box(out, x - 0.1, x + 0.1, (cyF + cyW) / 2 - 0.1, (cyF + cyW) / 2 + 0.1, top + 2.38, top + 2.55, 'lamp', 0.3, null, true);
      lampAt(s, x, (cyF + cyW) / 2, top + 2.45, 3.2, 0.7, '#ffd9a0', { kind: 'pendant' }); }
    for (const x of [-7.0, -5.9, -4.8, -3.7, -2.6]) { at('stool', g, 1); pipe(out, x, 2.12, 0.035, top, top + 0.72, 'galv', 0.1, 6); pipe(out, x, 2.12, 0.19, top + 0.70, top + 0.75, 'wood', 0.3, 10); pipe(out, x, 2.12, 0.17, top, top + 0.02, 'galv', 0, 10);
      rec({ id: 'stool' + x, kind: 'barStool', x: x, y: r3(2.12 - FIX.barStool.stand - 0.18), rot: 0 }); }
    // the kitchen box at the west end, its pass on the front (0.95)
    at('kitchen', g, 0); const kx0 = -hx + 0.35, kx1 = -7.75, ky0 = 2.2, ky1 = hy - 0.35;
    for (const [a, b, c2, d2, bb] of [[kx0, ky1, kx0, ky0, -0.3], [kx1, ky0, kx1, ky1, -0.15], [kx1, ky1, kx0, ky1, -0.5]]) wall(out, a, b, c2, d2, top, top + 2.6, 'clap', clapTex(), bb);
    wall(out, kx0, ky0, kx1, ky0, top, top + 0.91, 'clap', clapTex(), 0.05); wall(out, kx0, ky0, kx1, ky0, top + 1.85, top + 2.6, 'clap', clapTex(), 0.05);
    box(out, kx0, kx1, ky0 - 0.30, ky0, top + 0.91, top + 0.95, 'galv', 0.3, null, false); slab(out, [[kx0, ky0], [kx1, ky0], [kx1, ky1], [kx0, ky1]], top + 2.6, 'roofM', 0.1);
    lampAt(s, (kx0 + kx1) / 2, ky0 - 0.2, top + 1.7, 2.4, 0.6, '#ffd9a0', { kind: 'heatLamp' });
    rec({ id: 'kitchenPass', kind: 'kitchenPass', x: r3((kx0 + kx1) / 2), y: r3(ky0 - 0.30 - FIX.kitchenPass.stand), rot: 0, meta: { role: 'waiter' } });
    for (const [k, x] of [[0, -6.0], [1, -3.6]]) rec({ id: 'barWork' + k, kind: 'barWork', x, y: r3(cyW + FIX.barWork.stand), rot: r3(PI), meta: { role: 'bartender' } });
    for (const [k, x] of [[0, -6.5], [1, -4.2], [2, -2.6]]) rec({ id: 'barFront' + k, kind: 'barFront', x, y: r3(cyF - 0.06 - FIX.barFront.stand), rot: 0, meta: { role: 'patron' } });
    // ---- the tables ----
    const TBL = [[-7.2, -3.6], [-4.7, -3.6], [-2.2, -3.6], [-7.2, -0.6], [-4.7, -0.6], [-2.2, -0.6]];
    TBL.forEach(([x, y], k) => { at('table', g, 1); pipe(out, x, y, 0.2, top, top + 0.03, 'galv', 0, 8); pipe(out, x, y, 0.04, top + 0.03, top + 0.72, 'galv', 0.1, 6);
      box(out, x - 0.4, x + 0.4, y - 0.4, y + 0.4, top + 0.72, top + 0.75, 'wood', 0.3, plankTex(0.1), false);
      for (const [dx, dy, bx, by] of [[0, -0.62, 0, -1], [0, 0.62, 0, 1], [-0.62, 0, -1, 0], [0.62, 0, 1, 0]]) { const cx = x + dx, cy = y + dy; at('chair', g, 1);
        for (const [ex, ey] of [[-0.18, -0.18], [0.18, -0.18], [0.18, 0.18], [-0.18, 0.18]]) box(out, cx + ex - 0.02, cx + ex + 0.02, cy + ey - 0.02, cy + ey + 0.02, top, top + 0.43, 'galv', 0, null, true);
        box(out, cx - 0.2, cx + 0.2, cy - 0.2, cy + 0.2, top + 0.43, top + 0.47, 'wood', 0.3, null, false);
        const ox = bx * 0.19, oy = by * 0.19; box(out, cx + ox - (by ? 0.2 : 0.02), cx + ox + (by ? 0.2 : 0.02), cy + oy - (bx ? 0.2 : 0.02), cy + oy + (bx ? 0.2 : 0.02), top + 0.47, top + 0.86, 'wood', 0.1, null, false); }
      rec({ id: 'table' + k, kind: 'dineTable', x, y: r3(y - 0.4 - FIX.dineTable.stand - 0.25), rot: 0, meta: { seats: 4, seatZ: 0.45, role: 'diner · waiter' } }); });
    for (const [k, y] of [[0, -4.4], [1, -2.4], [2, -0.4]]) { const x = 0.35; at('table', g, 1); pipe(out, x, y, 0.2, top, top + 0.03, 'galv', 0, 8); pipe(out, x, y, 0.04, top, top + 0.92, 'galv', 0.1, 6); pipe(out, x, y, 0.32, top + 0.92, top + 0.95, 'wood', 0.3, 10);
      rec({ id: 'high' + k, kind: 'highTable', x: r3(x - 0.3 - FIX.highTable.stand - 0.1), y, rot: r3(-PI / 2), meta: { role: 'patron' } }); }
    // ---- the stage ----
    const sx0 = 2.4, sx1 = hx - 0.6, sy0 = 2.0, sy1 = hy - 0.4, sz = top + 0.45, tz = sz + 4.0;
    at('stage', g, 0); box(out, sx0, sx1, sy0, sy1, top, sz, 'blk', -0.1, null, false); slab(out, [[sx0, sy0], [sx1, sy0], [sx1, sy1], [sx0, sy1]], sz, 'plank', 0.05, plankTex(0.14));
    decalY(out, sy0, -1, sx0, sx1, top + 0.05, sz - 0.05, 'blk', -0.3, null, 0.02);
    for (let k = 0; k < 3; k++) { const x0 = sx0 - 0.9 + k * 0.3; box(out, x0, sx0, sy0 + 0.35, sy0 + 1.35, top, top + 0.15 * (k + 1), 'plank', 0.1 - k * 0.1, plankTex(0.14), false); }
    at('truss', g, 1);
    for (const [x, y] of [[sx0 + 0.15, sy0 + 0.15], [sx1 - 0.15, sy0 + 0.15], [sx0 + 0.15, sy1 - 0.15], [sx1 - 0.15, sy1 - 0.15]]) {
      box(out, x - 0.15, x + 0.15, y - 0.15, y + 0.15, sz, tz, 'alumF', 0.05, trussTex(), true); box(out, x - 0.25, x + 0.25, y - 0.25, y + 0.25, sz, sz + 0.03, 'alumF', 0.2, null, false); }
    for (const y of [sy0 + 0.15, sy1 - 0.15]) box(out, sx0, sx1, y - 0.15, y + 0.15, tz - 0.3, tz, 'alumF', y < 0 || y === sy0 + 0.15 ? 0.1 : -0.2, trussTex(), false);
    for (const x of [sx0 + 0.15, sx1 - 0.15]) box(out, x - 0.15, x + 0.15, sy0, sy1, tz - 0.3, tz, 'alumF', 0, trussTex(), false);
    at('stageLight', g, 1);
    const cans = 6; for (let k = 0; k < cans; k++) { const x = sx0 + 0.7 + (sx1 - sx0 - 1.4) * k / (cans - 1), y = sy0 + 0.15, m = 'led' + (k % 4);
      beam(out, [x, y, tz - 0.3], [x, y, tz - 0.48], 0.012, 'blk', 0, 4); tube(out, [x, y + 0.02, tz - 0.52], [x, y - 0.18, tz - 0.62], 0.10, 8, 'blk', 0.1, null, false);
      pipe(out, x, y - 0.19, 0.085, tz - 0.66, tz - 0.60, m, 0.3, 8);
      lampAt(s, x, sy0 + 1.4, sz + 2.2, 4.4, 1.0, LED[k % 4], { kind: 'stageWash', cyc: LED, ph: k % 4 }); }
    at('stage', g, 0); wall(out, sx0 + 0.3, sy1 - 0.35, sx1 - 0.3, sy1 - 0.35, sz, tz - 0.35, 'blk', plankTex(0.4), -0.2);   // the backdrop
    for (const [a, b] of [[sx0 + 0.05, sx0 + 0.75], [sx1 - 0.75, sx1 - 0.05]]) { at('speaker', g, 1); box(out, a, b, sy0 + 0.1, sy0 + 0.7, sz, sz + 1.5, 'blk', 0, null, false);
      for (const zz of [0.35, 0.95]) decalY(out, sy0 + 0.1, -1, a + 0.1, b - 0.1, sz + zz, sz + zz + 0.4, 'iron', 0.2, null, 0.02); }
    for (const x of [sx0 + 1.6, sx1 - 1.6]) { at('monitor', g, 1); out.push(F([[x - 0.3, sy0 + 0.2, sz], [x + 0.3, sy0 + 0.2, sz], [x + 0.3, sy0 + 0.6, sz + 0.34], [x - 0.3, sy0 + 0.6, sz + 0.34]], 'blk', 0.2)); box(out, x - 0.3, x + 0.3, sy0 + 0.6, sy0 + 0.66, sz, sz + 0.34, 'blk', -0.2, null, false); }
    // the performers' kit: the mic at the front, the keys to one side, the conga to the other
    const micX = (sx0 + sx1) / 2, micY = sy0 + 0.95; at('mic', g, 1);
    { const hx2 = micX - WANT[0], hy2 = micY - WANT[1]; pipe(out, hx2, hy2 - 0.12, 0.13, sz, sz + 0.02, 'blk', 0, 8); tube(out, [hx2, hy2 - 0.12, sz], [hx2, hy2 - 0.12, sz + 0.95], 0.012, 5, 'galv', 0.2, null, false);
      tube(out, [hx2, hy2 - 0.12, sz + 0.95], [hx2, hy2, sz + 1.04], 0.01, 4, 'galv', 0.2, null, false); pipe(out, hx2, hy2, 0.03, sz + 1.02, sz + 1.09, 'blk', 0.3, 6); }
    rec({ id: 'mic', kind: 'micStand', x: r3(micX), y: r3(micY), rot: r3(PI), z: r3(sz), meta: { role: 'singer' } });
    { const kx = sx0 + 1.3, ky = sy0 + 1.9 - FIX.keysStand.stand - FIX.keysStand.d; at('keys', g, 1);
      for (const dx of [-0.45, 0.45]) { beam(out, [kx + dx, ky, sz], [kx + dx, ky + 0.3, sz + 0.76], 0.015, 'galv', 0, 4); beam(out, [kx + dx, ky + 0.3, sz], [kx + dx, ky, sz + 0.76], 0.015, 'galv', 0, 4); }
      box(out, kx - 0.6, kx + 0.6, ky, ky + 0.34, sz + 0.76, sz + 0.80, 'blk', 0.2, null, false); decalZ(out, sz + 0.805, kx - 0.55, kx + 0.55, ky + 0.03, ky + 0.16, 'board', 0.3, checkTex(0.04), 0.02);
      rec({ id: 'keys', kind: 'keysStand', x: r3(kx), y: r3(ky + FIX.keysStand.d + FIX.keysStand.stand), rot: r3(PI), z: r3(sz), meta: { role: 'keys' } }); }
    { const dx = sx1 - 1.4, dy = sy0 + 1.8, cy2 = dy - FIX.conga.stand - 0.17; at('conga', g, 1);
      for (const a of [0, 2.1, 4.2]) beam(out, [dx + Math.cos(a) * 0.22, cy2 + Math.sin(a) * 0.22, sz], [dx, cy2, sz + 0.4], 0.012, 'galv', 0, 4);
      pipe(out, dx, cy2, 0.15, sz + 0.36, sz + 0.69, 'canvas', 0.1, 10); pipe(out, dx, cy2, 0.16, sz + 0.69, sz + 0.72, 'board', 0.35, 10);
      rec({ id: 'conga', kind: 'conga', x: r3(dx), y: r3(dy), rot: r3(PI), z: r3(sz), meta: { role: 'drums' } }); }
    // ---- the dance floor ----
    at('dance', g, 0); const fx0 = 1.3, fx1 = hx - 0.5, fy0 = -hy + 0.8, fy1 = sy0 - 0.35; decalZ(out, top + 0.004, fx0, fx1, fy0, fy1, 'danceF', 0.1, checkTex(0.6), 0.02);
    for (const [k, x, y] of [[0, 3.2, -1.2], [1, 5.0, -2.6], [2, 6.8, -1.0], [3, 4.4, 0.4]]) rec({ id: 'dance' + k, kind: 'danceSpot', x, y, rot: r3(PI), meta: { role: 'dancer' } });
    // ---- festoon strings over the tables and the floor ----
    const poles = [[-hx + 0.25, -hy + 0.25], [-4.6, -hy + 0.25], [-0.6, -hy + 0.25], [4.3, -hy + 0.25], [hx - 0.25, -hy + 0.25], [hx - 0.25, 1.6], [-0.6, 1.6], [-hx + 0.25, 1.6]], pz = top + 3.2;
    at('festoon', g, 1); for (const [x, y] of poles) { pipe(out, x, y, 0.045, top, pz, 'galv', 0.1, 6); pipe(out, x, y, 0.1, top, top + 0.06, 'galv', 0, 8); }
    const runs = [[0, 1], [1, 2], [2, 3], [3, 4], [4, 5], [5, 6], [6, 7], [7, 0], [1, 6], [2, 7], [3, 6], [0, 6]];
    for (const [a, b] of runs) { const A = [poles[a][0], poles[a][1], pz - 0.05], B = [poles[b][0], poles[b][1], pz - 0.05], L = Math.hypot(B[0] - A[0], B[1] - A[1]), P = sagLine(out, A, B, 0.35 + L * 0.02, Math.max(4, Math.round(L / 0.8)), 'blk', 0.008);
      const nb = Math.max(2, Math.round(L / 0.55)); for (let k = 1; k < nb; k++) { const p = P(k / nb); pipe(out, p[0], p[1], 0.03, p[2] - 0.09, p[2] - 0.02, 'bulb', 0.3, 5); }
      const nl = Math.max(1, Math.round(L / 1.9)); for (let k = 0; k < nl; k++) { const p = P((k + 0.5) / nl); lampAt(s, p[0], p[1], p[2] - 0.06, 2.6, 0.5, '#ffd08a', { kind: 'festoon' }); } }
    // ---- rails; the gangway landing and the visitors' side stay open ----
    at('rail', g, 1);
    railRun(out, -hx + 0.1, -hy + 0.08, gx - 1.2, -hy + 0.08, top, 'pipe'); railRun(out, gx + 1.2, -hy + 0.08, 1.4, -hy + 0.08, top, 'pipe');
    railRun(out, -hx + 0.1, hy - 0.08, hx - 0.1, hy - 0.08, top, 'pipe'); railRun(out, -hx + 0.08, -hy + 0.1, -hx + 0.08, hy - 0.1, top, 'pipe');
    railRun(out, hx - 0.08, 1.9, hx - 0.08, hy - 0.1, top, 'pipe');
    s._venue = { top, bar: { counter: [[cx0, cyF], [cx1, cyB]], counterZ: 0.95, workZ: 0.80, back: [[cx0, py1 - 0.55], [cx1, py1 - 0.1]] }, pavilion: [[px0, py0], [px1, py1]], roofZ: r2(rzP - top),
      kitchen: [[kx0, ky0], [kx1, ky1]], tables: TBL.map(([x, y]) => ({ at: [x, y], top: 0.75, seats: 4, seatZ: 0.45 })), stage: { poly: [[sx0, sy0], [sx1, sy0], [sx1, sy1], [sx0, sy1]], deckZ: 0.45, trussZ: r2(tz - top), steps: { at: [sx0 - 0.45, sy0 + 0.85], treads: 3, rise: 0.15 } },
      dance: [[fx0, fy0], [fx1, fy1]], gangX: gx };
    return s._venue;
  }

  // ============================ FAMILIES ==================================================================================
  Object.assign(G.FAMILIES, {
    office: { label: 'marina office', note: 'a floating dockhouse on its own pontoon: windows round, a door and a service hatch with a shelf outside it, the attendants’ counter inside, a gable roof, rails', kind: 'float', faces: [],
      dims: { bays: [1, 3, 2], bayLen: [3.0, 4.0, 3.6], width: [3.6, 6.0, 4.8] },
      build(out, s, T) { const g = s.grp || 1; G.FAMILIES.float.build(out, s, T); officeHouse(out, s, T, g); CTX.grp = 0; } },
    venue: { label: 'floating venue', note: 'an 18 × 12 m pontoon: a bar and restaurant under a pavilion, festoon lights over the tables, a stage with a lit truss, a dance floor', kind: 'float', faces: [],
      dims: { bays: [3, 6, 5], bayLen: [3.0, 4.0, 3.6], width: [8, 14, 12] },
      build(out, s, T) { const g = s.grp || 1; G.FAMILIES.float.build(out, s, T); venueFit(out, s, T, g); CTX.grp = 0; } },
  });
  G.FLOATING.office = 1; G.FLOATING.venue = 1;

  // ============================ THE MARINA FIT ============================================================================
  // s.marina = { conduits: ['water' | 'shore'], items: [{kind, x, y, rot, id, serves, open, w}], cleats: [{x, y, side, berth, lead, type:'cleat'|'bollard'}],
  // fenders: [{x, y, side, berth}] } in module metres. Runs inside addFittings, so cleats and bollards join P and the TIE section.
  function marinaFit(out, s, T, P, g, top) {
    const M = s.marina || {}, FT = s.floating ? G.floatFrame(T) : T; s._mfix = s._mfix || []; s._lamps = s._lamps || []; CTX.grp = g;
    if (M.conduits && s.floating) conduits(out, s, g, top, M.conduits);
    for (const c of (M.cleats || [])) { CTX.grp = g;
      if (c.type === 'bollard') { bollardAt(out, c.x, c.y, top); P.bollards.push({ x: r3(c.x), y: r3(c.y), z: top, side: c.side || 1, face: 'marina', berth: c.berth || null, lead: c.lead || null }); }
      else { cleatAt(out, c.x, c.y, top, c.ang || 0); P.cleats.push({ x: r3(c.x), y: r3(c.y), z: top, side: c.side || 1, face: 'marina', berth: c.berth || null, lead: c.lead || null }); } }
    for (const f of (M.fenders || [])) { CTX.grp = g; foamAt(out, f.x, f.y, f.side || 1, top - 0.20, FT, s); P.foams.push({ x: r3(f.x), y: r3(f.y), side: f.side || 1, face: 'marina', berth: f.berth || null }); }
    for (const it of (M.items || [])) place(out, s, g, top, it);
    CTX.grp = g;
  }
  const af0 = G.addFixtures;
  G.addFixtures = function (out, s, T, P, g, top) { const list = af0 ? (af0(out, s, T, P, g, top) || []) : []; if (s.marina || s._mfix || s._lamps) marinaFit(out, s, T, P, g, top); return list; };

  // ============================ PRESETS (single modules) ==================================================================
  // a lone marina float: a pedestal at each quarter of its berth edge, bollard lights, a dock box and a ring, cleats for two boats
  function floatFit(L, W, hull) { const hx = L / 2, hy = W / 2, e = hy - 0.63, items = [
      { kind: 'pedestal2', x: -hx * 0.5, y: e, rot: 0, id: 'pedestal0' }, { kind: 'pedestal2', x: hx * 0.5, y: e, rot: 0, id: 'pedestal1' },
      { kind: 'dockLight', x: -hx + 0.35, y: -hy + 0.2, rot: 0 }, { kind: 'dockLight', x: 0, y: -hy + 0.2, rot: 0 }, { kind: 'dockLight', x: hx - 0.35, y: -hy + 0.2, rot: 0 },
      { kind: 'dockBox', x: -0.3, y: -hy + 0.95, rot: PI }, { kind: 'ringPost', x: hx - 1.0, y: e + 0.1, rot: 0 }];
    const cleats = [-hx + 0.6, -hx * 0.18, hx * 0.18, hx - 0.6].map((x, k) => ({ x, y: hy - 0.30, side: 1, lead: ['bow', 'spring', 'spring', 'stern'][k] }));
    return { conduits: ['shore'], items, cleats, hull }; }
  const mf = (hull, bays, bayLen, W, extra) => Object.assign({ family: 'float', hull, bays, bayLen, width: W, freeboard: FREEBOARD[hull], faces: [], fixtures: [], swimLadder: true, marina: floatFit(bays * bayLen, W, hull) }, extra || {});
  Object.assign(G.PRESETS, {
    compositeMarina: mf('composite', 4, 3.0, 2.5), cubeMarina: mf('plastic', 4, 3.0, 2.5, { curb: 'yellow' }), timberMarina: mf('timber', 4, 3.0, 2.4), drumMarina: mf('drum', 3, 3.0, 2.4),
    yachtPontoon: { family: 'float', hull: 'concrete', bays: 3, bayLen: 3.6, width: 4.0, freeboard: 0.55, faces: [], fixtures: [], swimLadder: false,
      marina: { conduits: ['shore'], items: [{ kind: 'pedestalY', x: -2.6, y: 1.37, rot: 0, id: 'pedestal0' }, { kind: 'pedestalY', x: 2.6, y: 1.37, rot: 0, id: 'pedestal1' },
        { kind: 'pumpOut', x: 4.2, y: -0.6, rot: 0 }, { kind: 'dockLight', x: -5.0, y: -1.8, rot: 0 }, { kind: 'dockLight', x: 0, y: -1.8, rot: 0 }, { kind: 'dockLight', x: 5.0, y: -1.8, rot: 0 }],
        cleats: [{ x: -4.2, y: 1.45, side: 1, type: 'bollard', lead: 'stern' }, { x: -1.0, y: 1.45, side: 1, type: 'bollard', lead: 'stern' }, { x: 1.0, y: 1.45, side: 1, type: 'bollard', lead: 'stern' }, { x: 4.2, y: 1.45, side: 1, type: 'bollard', lead: 'stern' }] } },
    floatingOffice: { family: 'office', hull: 'concrete', bays: 2, bayLen: 3.6, width: 4.8, freeboard: 0.50, guideFace: 'shore', swimLadder: false,
      marina: { items: [{ kind: 'ringPost', x: 2.9, y: 1.6, rot: 0 }, { kind: 'dockLight', x: -3.35, y: 2.15, rot: 0 }, { kind: 'dockLight', x: 3.35, y: 2.15, rot: 0 }] } },
    floatingVenue: { family: 'venue', hull: 'concrete', bays: 5, bayLen: 3.6, width: 12, freeboard: 0.55, guideFace: 'shore', swimLadder: false, gangX: -3,
      marina: { items: [{ kind: 'dockLight', x: -8.7, y: -5.7, rot: 0 }, { kind: 'dockLight', x: 8.7, y: -5.7, rot: 0 }, { kind: 'ringPost', x: 8.3, y: -1.2, rot: -PI / 2 }],
        cleats: [{ x: 2.6, y: -5.72, side: -1 }, { x: 5.0, y: -5.72, side: -1 }, { x: 7.4, y: -5.72, side: -1 }] } },
    gateQuay: { family: 'quay', face: 'concrete', bays: 3, bayLen: 3.0, width: 5, faces: [], curb: 'none', rail: 'pipe', railSides: ['water'], open: { water: [[-1.65, 1.65]] }, fixtures: [],
      marina: { items: [{ kind: 'gate', x: 0, y: 1.85, rot: 0, id: 'gate' }, { kind: 'lampPost', x: -3.4, y: 1.4, rot: 0 }, { kind: 'lampPost', x: 3.4, y: 1.4, rot: 0 }] } },
    wasteQuay: { family: 'quay', face: 'concrete', bays: 3, bayLen: 3.0, width: 5, faces: [], curb: 'none', rail: 'pipe', railSides: ['water'], fixtures: [],
      marina: { items: [{ kind: 'binScreen', x: -0.6, y: 0.9, rot: 0, w: 4.8 }, { kind: 'binGeneral', x: -2.4, y: -0.25, rot: 0 }, { kind: 'binGeneral', x: -1.7, y: -0.25, rot: 0 },
        { kind: 'binRecycle', x: -1.0, y: -0.25, rot: 0 }, { kind: 'bottleBank', x: 0.0, y: -0.25, rot: 0 }, { kind: 'oilTank', x: 1.3, y: -0.25, rot: 0 }, { kind: 'lampPost', x: 3.6, y: 1.5, rot: 0 }] } },
  });

  root.MarinaGeo = { SIX, EMI, LED, EXTRA, addMat, FIX, SEQ, REQUESTS, BUILD, LAMPS, FREEBOARD, HULL_LABEL, Z_SOCKET, Z_TAP, place, record, lampAt, marinaFit, conduits, compositeBody,
    officeHouse, venueFit, floatFit, decalX, pyramid, sagLine, clapTex, meshTex, trussTex, checkTex };
})(typeof globalThis !== 'undefined' ? globalThis : window);
