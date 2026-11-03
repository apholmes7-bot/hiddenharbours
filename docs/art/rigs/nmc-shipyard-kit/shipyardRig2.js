/* Hidden Harbours — SHIPYARD RIG, PASS 2 · THE PARTS (globalThis.ShipyardGeo2).
   Load after the wharf rig pass 2 files (geo, fam, verbs, wharfRig2, kit), marinaRig.js, marinaBoats.js and the hull rigs the
   yard stores (Sloop88Iso, SloopIso, SportFisherIso3, BowriderIso, GoFastIso, CruiserIso, SportSkiffIso).

   Pass 1 (Art/shipyardIsoRig.js, untouched) lit every face once from the fixed upper-left key with ordered dither and a keyline,
   dropped whole yards to 16 / 12 / 8 px per metre, pasted the fleet's own bakes onto its mounts with a mid-hull depth test, and
   stored nothing. Here every part is a module of the family 'syPart' in a wharf pass 2 harbour: rasterised into the same G-buffer
   as the quays and piers, lit by any WeatherSky.at() with the same law (sky × visibility, sun × shadow map, bounce, the band
   ladder, harmony's grade), rained and snowed on by porosity, and lit after dark by its own lamps (MarinaStage). Hulls are faces
   in the same buffer — the face lists MarinaBoats reads, with the underbody kept and the cabin left out, the sloops' masts
   unstepped — so a bunk, a stand pad or a sling touches the hull where it is drawn, and each shades the other.

     rack      dry stack: frames, cross beams and carpeted bunks; bays for the bowrider, the sport skiff and the 31′ express
               cruiser, three or four tiers; each slot published with its limits
     hard      a hull on the hard: keel blocks, jack stands chained along each side or a steel cradle, a ladder at the stern;
               shrink-wrapped in winter
     lift      150 t travel lift: legs on bogies, side girders, a rear cross beam, open ahead for masts and bows, engine house,
               cab, hoist blocks, and slings shaped to the hull's own sections
     forklift  marina forklift: counterweighted chassis, cab, two-stage mast, carpeted forks, a hull on them
     shed      clear-span workshop on steel portal frames, clad, roll-up door; bench, pegboard, shelving, compressor, a 5 t
               bridge crane, high-bay lamps; cutaway 'roof' or 'section' toward the facing it is baked for
     staging · trestles (a mast unstepped) · fence · flood · washPad · ground (concrete, asphalt) · props

   Every fixture a figure uses is built to character v9.2's world fit for every creator body (375) and publishes its verb.   */
(function (root) {
  'use strict';
  const G = root.WharfGeo2, MG = root.MarinaGeo, MB = root.MarinaBoats;
  if (!G || !G.FAMILIES || !G.resolve || !MG || !MB) throw new Error('shipyardRig2.js: load the wharf pass 2 files, marinaRig.js and marinaBoats.js first');
  if (root.ShipyardGeo2) return;
  const { CTX, at, F, box, tube, xform } = G, PI = Math.PI, HP = PI / 2, D2R = PI / 180;
  const r2 = (v) => Math.round(v * 100) / 100, r3 = (v) => Math.round(v * 1000) / 1000, clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const hs = (a, b, c) => { let h = Math.imul(a | 0, 374761393) + Math.imul(b | 0, 668265263) + Math.imul(c | 0, 1440670441) | 0; h ^= h >>> 13; h = Math.imul(h, 1274126177) | 0; h ^= h >>> 16; return (h >>> 0) / 4294967296; };
  const vn = (x, y, s) => { const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy), a = hs(xi, yi, s), b = hs(xi + 1, yi, s), c = hs(xi, yi + 1, s), d = hs(xi + 1, yi + 1, s); return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v; };

  // ============================ CHARACTER v9.2 (every creator body, reports/worldfit.txt — the wharf's numbers) ============
  const FITS = { rail: [0.549, 0.724], rung: [0.211, 0.273], load: [0.718, 0.978], bench: [0.643, 0.853], rest: [0, 1.012], seat: [0.250, 0.291], stepDown: [0.06, 1.30] };
  const REQUESTS = [
    { clip: 'ladderOn / ladderOff', why: 'onto a ladder at its foot, off it at a deck, a staging lift or a cab', now: 'blend 300 ms from idle to ladderDown at the station; the top-out is board at railZ 0.72 with the root advanced 0.60 m' },
    { clip: 'leanLadder', why: 'a yard ladder leans on the transom at 75°; v9.2 climbs a vertical one', now: 'every ladder is built vertical, 0.30 m off the landing edge and lashed at the top' },
    { clip: 'sprayUp', why: 'pressure-washing and rolling antifoul overhead, the tool above the head', now: 'reach at its highest rest, 1.012 m; the wand is the engine’s' },
    { clip: 'crouchWork', why: 'sanding the foot of a keel and hooking the stand chains near the ground', now: 'reach with lift 0.30' },
    { clip: 'cabSteps', why: 'the forklift cab floor is 1.75 m up; mountCab was fitted to a truck’s step', now: 'mountCab from the top step, 1.30 m, blended 400 ms from the ground' },
    { clip: 'carryRemote', why: 'the travel-lift operator walks beside the lift with the remote on a strap', now: 'walk at the lift’s creep, 0.40 m/s; the remote is the engine’s' },
    { clip: 'stepLadder', why: 'a jack-stand screw above 1.012 m under the big hulls', now: 'published as not fitting, with the height' },
  ];
  const fitOf = (p, v) => { const R = FITS[p]; return { param: p, value: r3(v), range: R || null, ok: !!R && v >= R[0] - 1e-9 && v <= R[1] + 1e-9 }; };

  // ============================ MATERIALS (five bands, dark → light; pass 1's ramps where it had them) =====================
  const PAL = {
    galv: ['#6d7276', '#868b8f', '#a0a5a8', '#bbbfc1', '#d6d9da'], steel: ['#2d3438', '#3f484d', '#556066', '#6f7b81', '#8e9aa0'], rack: ['#1b2f45', '#23405e', '#2f5578', '#44709a', '#6690b8'],
    yel: ['#6b5210', '#8f6f14', '#b8921c', '#d9b43a', '#f0d56a'], tyre: ['#141517', '#1d1f22', '#282b2f', '#34383d', '#43484d'], conc: ['#686962', '#7c7d74', '#919287', '#a6a79b', '#bbbcae'],
    asph: ['#26282a', '#323537', '#404346', '#515457', '#65686a'], block: ['#3a2c20', '#493829', '#584534', '#685440', '#79654f'], pine: ['#5f4a31', '#7a6040', '#967852', '#b39468', '#cfb283'],
    corr: ['#5d666b', '#727d83', '#8a959b', '#a2adb2', '#bcc5c9'], red: ['#511d17', '#6b2a1f', '#84392a', '#9c4d3a', '#b3654e'], green: ['#1f3729', '#2a4735', '#375943', '#466c53', '#588066'],
    white: ['#666b68', '#7f847f', '#989c96', '#b1b5ae', '#cbcec6'], glass: ['#1c2a33', '#2a3d49', '#3d5563', '#5a7584', '#8aa6b3'], wrapW: ['#8f9aa3', '#aab4bb', '#c4ccd1', '#dde3e6', '#f3f6f7'],
    wrapB: ['#1d3a66', '#27508a', '#3468ad', '#4d86c8', '#78a8dd'], sling: ['#6a2c0c', '#8c3c12', '#b0531c', '#cf6f2e', '#e8914e'], carpet: ['#1c2024', '#262b30', '#31373d', '#3d444b', '#4b535b'],
    lens: ['#8a826a', '#b3a986', '#d8cfa8', '#eee7c6', '#fffbe8'], amber: ['#7a4a10', '#a0621a', '#c98026', '#e8a13e', '#ffc466'], line: ['#7a6320', '#9b7e28', '#bb9a34', '#d4b24a', '#e8c965'],
    rust: ['#3b2014', '#4f2b19', '#653820', '#7c4829', '#955c35'], poche: ['#15191b', '#1c2124', '#23292d', '#2b3237', '#343c42'], blue: ['#16304d', '#1f416a', '#2b5688', '#3e70a8', '#5a8cc4'],
    orange: ['#6e2a0a', '#933a10', '#b84e18', '#d66b2a', '#ee8c46'], alum: ['#525a5e', '#687175', '#7f888c', '#98a0a3', '#b2b9bb'], ply: ['#5a4630', '#735a3d', '#8e714d', '#a98a61', '#c3a578'],
    tool: ['#2a2d2e', '#3a3e40', '#4d5254', '#63696b', '#7c8284'],
  };
  const PROP = { galv: [.03, .5, .6, 'metal'], steel: [.03, .55, .6, 'metal'], rack: [.03, .45, .7, 'paint'], yel: [.03, .55, .7, 'paint'], tyre: [.02, .25, .8, 'paint'], conc: [.12, .1, 1, 'wood'],
    asph: [.08, .15, 1, 'wood'], block: [.26, .1, 1, 'wood'], pine: [.24, .12, 1, 'wood'], corr: [.02, .5, .4, 'metal'], red: [.03, .4, .5, 'paint'], green: [.03, .4, .5, 'paint'], white: [.03, .4, .5, 'paint'],
    glass: [0, .95, .3, 'glass'], wrapW: [0, .55, .35, 'paint'], wrapB: [0, .55, .35, 'paint'], sling: [.38, .05, .9, 'rope'], carpet: [.38, .05, .9, 'rope'], lens: [0, .9, .2, 'glass'], amber: [0, .9, .2, 'glass'],
    line: [.05, .2, 1, 'paint'], rust: [.1, .2, .9, 'metal'], poche: [.02, .1, 1, 'paint'], blue: [.03, .45, .7, 'paint'], orange: [.03, .45, .7, 'paint'], alum: [.03, .6, .6, 'metal'], ply: [.2, .15, 1, 'wood'], tool: [.03, .4, .6, 'metal'] };
  for (const k in PAL) { const p = PROP[k]; MG.addMat('sy.' + k, PAL[k], { por: p[0], gloss: p[1], hold: p[2], cls: p[3] }); }
  if (MG.EMI) { MG.EMI['sy.lens'] = { k: 1, c: '#fff4d6' }; MG.EMI['sy.amber'] = { k: 1, c: '#ffb044' }; }

  // textures: a band offset per (u, v) in metres — walls (along, z), slabs (x, y)
  const TX = {
    conc: (J, sd) => (u, v) => { const a = ((u % J) + J) % J, b = ((v % J) + J) % J; if (a < 0.035 || b < 0.035) return -1;
      const n = vn(u * 0.55, v * 0.55, sd || 11); if (n < 0.17) return -1; if (hs(Math.floor(u * 11), Math.floor(v * 11), 5) > 0.992) return -1; return n > 0.9 ? 1 : 0; },
    asph: (u, v) => { if (hs(Math.floor(u * 15), Math.floor(v * 15), 7) > 0.955) return 1; return vn(u * 0.5, v * 0.5, 19) < 0.22 ? -1 : 0; },
    corr: (p) => (u) => { const f = (((u % p) + p) % p) / p; return f < 0.22 ? -1 : f > 0.72 ? 1 : 0; },
    corrV: (p) => (u, v) => { const f = (((v % p) + p) % p) / p; return f < 0.22 ? -1 : f > 0.72 ? 1 : 0; },
    wrap: (u, v) => { const n = vn(u * 1.6, v * 2.9, 23); return n < 0.16 ? -1 : n > 0.9 ? 1 : 0; },
    grate: (u) => (Math.floor(u * 22) & 1) ? -1 : 0,
    peg: (u, v) => (Math.floor(u * 12.5) & 1) === 0 && (Math.floor(v * 12.5) & 1) === 0 ? -1 : 0,
  };

  // ============================ GEOMETRY ================================================================================
  const rec = (s, r) => { (s._sy || (s._sy = [])).push(r); return r; };
  function verb(s, v) { v.k = 'verb'; v.fits = (v.fits || []).map(([p, x]) => fitOf(p, x)); v.fitsCreatorBodies = v.fits.every(f => f.ok); return rec(s, v); }
  function bar(out, p0, p1, w, h, mat) {
    const d = [p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]], L = Math.hypot(d[0], d[1], d[2]) || 1, u = d.map(q => q / L);
    let s = [u[1], -u[0], 0]; const sl = Math.hypot(s[0], s[1]); s = sl < 1e-3 ? [1, 0, 0] : [s[0] / sl, s[1] / sl, 0];
    const t = [s[1] * u[2] - s[2] * u[1], s[2] * u[0] - s[0] * u[2], s[0] * u[1] - s[1] * u[0]];
    const C = (p, a, b) => [p[0] + (s[0] * a * w + t[0] * b * h) / 2, p[1] + (s[1] * a * w + t[1] * b * h) / 2, p[2] + (s[2] * a * w + t[2] * b * h) / 2];
    const K = [[-1, -1], [1, -1], [1, 1], [-1, 1]], B0 = K.map(k => C(p0, k[0], k[1])), B1 = K.map(k => C(p1, k[0], k[1]));
    for (let i = 0; i < 4; i++) { const j = (i + 1) % 4; out.push(F([B0[i], B0[j], B1[j], B1[i]], mat, 0)); }
    out.push(F(B0.slice().reverse(), mat, 0)); out.push(F(B1, mat, 0));
  }
  function wheelX(out, x, y, z, R, w, mat, hub) {
    const n = 14, P = (i, sx) => { const t = i / n * 2 * PI; return [x + sx * w / 2, y + Math.cos(t) * R, z + Math.sin(t) * R]; };
    for (let i = 0; i < n; i++) { const j = (i + 1) % n; out.push(F([P(i, -1), P(j, -1), P(j, 1), P(i, 1)], mat, 0)); }
    for (const sx of [-1, 1]) { const v = []; for (let i = 0; i < n; i++) v.push(P(i, sx)); out.push(F(v, mat, 0));
      if (hub) { const h = []; for (let i = 0; i < 8; i++) { const t = i / 8 * 2 * PI; h.push([x + sx * (w / 2 + 0.012), y + Math.cos(t) * R * 0.55, z + Math.sin(t) * R * 0.55]); } out.push(F(h, hub, 0, 0.02)); } }
  }
  const quad = (out, a, b, c, d, mat, uv, tex, db) => out.push(F([a, b, c, d], mat, 0, db || 0, uv || null, tex || null));
  const inFrame = (out, T, fn) => { const tmp = []; fn(tmp); for (const f of tmp) { f.v = f.v.map(T); out.push(f); } };
  const yawOf = (d) => r2(Math.atan2(d[0], d[1]) / D2R);
  const dirYaw = (T, d) => { const a = T([0, 0, 0]), b = T([d[0], d[1], 0]); return yawOf([b[0] - a[0], b[1] - a[1]]); };
  const pt3 = (p) => [r3(p[0]), r3(p[1]), r3(p[2])];

  // a vertical ladder in the plane y (0.45 m between the stiles), rungs every 0.24 from 0.24 up to one rung under the landing zL
  function ladderAt(out, x, y, g, zL, mat) { at('ladder', 0, 1); const rungs = [];
    for (const sx of [-0.225, 0.225]) box(out, x + sx - 0.024, x + sx + 0.024, y - 0.03, y + 0.03, g, zL + 1.0, mat, 0, null, false);
    for (let z = g + 0.24; z <= zL - 0.2 + 1e-6; z += 0.24) { tube(out, [x - 0.2, y, z], [x + 0.2, y, z], 0.016, 5, mat, 0, null, false); rungs.push(r3(z)); }
    return rungs; }
  // the verbs of a ladder: P(dx, dy) → the local point dy metres out from its rung plane, yaw the local yaw facing it
  function climbVerbs(s, id, fixture, P, yaw, g, zL, rungs, note, show) {
    const foot = P(0, -0.275), top = zL - 0.72;
    verb(s, { verb: 'climbUp', id: id + '.up', fixture, stand: pt3([foot[0], foot[1], g]), yaw, note, rungs, landingZ: r3(zL), show,
      seq: [{ join: true, ms: 300, request: 'ladderOn', note: 'idle at the foot to ladderDown u 0' }, { clip: 'ladderDown', loop: true, reverse: true, opts: { rung: 0.24 }, rootMotion: [0, 0, 0.436], until: 'the top station, ' + r2(top) + ' m' },
        { clip: 'board', once: true, opts: { railZ: 0.72 }, advance: { m: 0.6, t: [0.28, 0.88] }, note: 'onto the landing at ' + r2(zL) + ' m' }],
      fits: [['rung', 0.24], ['rail', 0.72]], anim: { clip: 'ladderDown', reverse: true, at: pt3([foot[0], foot[1], g + 0.1]), move: { d: [0, 0, 1], len: r3(Math.max(0.3, top - g - 0.1)), rate: 0.436 } } });
    verb(s, { verb: 'climbDown', id: id + '.down', fixture, stand: pt3([foot[0], foot[1], top]), yaw,
      seq: [{ join: true, ms: 300, request: 'ladderOn', note: 'from idle on the landing, back to the ladder, to ladderDown u 0 at the top station' }, { clip: 'ladderDown', loop: true, opts: { rung: 0.24 }, rootMotion: [0, 0, -0.436], until: 'the foot' }, { join: true, ms: 300, request: 'ladderOff' }],
      fits: [['rung', 0.24], ['rail', 0.72]] });
  }
  function standAt(out, x, y, g, zp, mat) {
    const top = zp - 0.06, collar = g + Math.min(0.95, Math.max(0.35, (zp - g) * 0.42)), nut = Math.max(collar + 0.12, top - 0.45);
    for (let k = 0; k < 3; k++) { const t = k / 3 * 2 * PI + 0.5; tube(out, [x + Math.cos(t) * 0.5, y + Math.sin(t) * 0.5, g], [x + Math.cos(t) * 0.07, y + Math.sin(t) * 0.07, collar], 0.028, 5, mat, 0, null, false); }
    tube(out, [x, y, g + 0.2], [x, y, nut], 0.05, 8, mat, 0, null, true); tube(out, [x, y, nut], [x, y, top], 0.028, 6, 'sy.steel', 0, null, true);
    box(out, x - 0.1, x + 0.1, y - 0.1, y + 0.1, nut - 0.05, nut + 0.03, 'sy.steel', 0, null, false);
    box(out, x - 0.16, x + 0.16, y - 0.16, y + 0.16, top, zp, 'sy.ply', 0, null, false);
    return { nutZ: nut };
  }
  function drum(out, x, y, g, mat) { at('drum', 0, 1); tube(out, [x, y, g], [x, y, g + 0.88], 0.29, 12, mat, 0, null, true); for (const z of [0.29, 0.59]) tube(out, [x, y, g + z - 0.015], [x, y, g + z + 0.015], 0.296, 12, mat, 0, null, false); }

  // ============================ HULLS ON LAND ===========================================================================
  /* The yard's own table: planning weights (t) and where each is stored. Dimensions are the rigs' (and wharf pass 2's). */
  const SHIPS = {
    bowrider: { label: 'bowrider', loa: 5.64, beam: 2.29, draft: 0.40, t: 1.4, store: 'rack' },
    sportSkiff: { label: 'sport skiff', loa: 7.0, beam: 2.54, draft: 0.45, t: 2.3, store: 'rack' },
    cruiser: { label: 'express cruiser 31′', loa: 10.2, beam: 3.05, draft: 0.95, t: 5.3, store: 'rack' },
    goFast: { label: 'go-fast', loa: 11.9, beam: 2.50, draft: 0.80, t: 5.9, store: 'hard' },
    sloop30: { label: 'sloop 30', loa: 9.4, beam: 2.98, draft: 1.90, t: 4.6, keel: true, store: 'cradle' },
    sportFisher: { label: 'sport fisher 53′', loa: 16.2, beam: 5.16, draft: 1.40, t: 29, store: 'hard' },
    sloop88: { label: 'sloop 88', loa: 27.0, beam: 6.70, draft: 4.60, t: 68, keel: true, store: 'hard' },
    skybridge: { label: 'sport fisher 90′', loa: 27.4, beam: 7.32, draft: 1.90, t: 98, store: 'hard' },
  };
  const XT = {   // hulls the marina does not carry
    cruiser: { global: 'CruiserIso', faces: (R) => R.allFaces({}), mats: (R, p) => R.palette({ scheme: p }).mats, paints: (R) => R.schemeIds || [], deckRaw: (R) => R.DECK, boardRaw: (R) => R.DECK },
    sportSkiff: { global: 'SportSkiffIso', faces: (R) => R.allFaces ? R.allFaces() : [], mats: (R, p) => R.palette({ scheme: p }).mats, paints: (R) => R.schemeIds || [], deckRaw: (R) => R.DECK, boardRaw: (R) => R.DECK },
  };
  const TYP = (k) => XT[k] || MB.TYPES[k];
  function hullReady(type) { const T = TYP(type), R = T && root[T.global]; return !!(R && (type !== 'sportSkiff' || R.allFaces)); }
  const LC = new Map();
  function landHull(type, paint) {
    if (!hullReady(type)) return null; const T = TYP(type), R = root[T.global];
    const paints = T.paints(R) || [], p = typeof paint === 'number' ? (paints[paint % Math.max(1, paints.length)] || 'default') : (paints.indexOf(paint) >= 0 ? paint : (paints[0] || 'default')), ck = type + '|' + p;
    if (LC.has(ck)) return LC.get(ck);
    const SH = SHIPS[type] || { label: type, loa: 8, beam: 2.5, t: 3 }, mats = T.mats(R, p), keys = Object.keys(mats), used = {};
    const mk = (name) => { const nm = mats[name] ? name : (mats.paint ? 'paint' : mats.hull ? 'hull' : keys[0]), k = 'b.' + type + '.' + p + '.' + nm;
      if (!used[k]) { const m = mats[nm]; MG.addMat(k, MB.ramp5(m.ramp || m, m.off || 0), MB.PROPS(nm)); used[k] = 1; } return k; };
    const dwl = T.dwl ? T.dwl(R) : 0, deckRaw = T.deckRaw ? T.deckRaw(R) : T.deck(R) + dwl, boardRaw = T.boardRaw ? T.boardRaw(R) : T.board(R) + dwl;
    const zCut = SH.keel ? deckRaw + 2.6 : 1e9, raw = [];
    for (const f of T.faces(R)) { if (!f.v || f.v.length < 3 || f.lv === 'cabin' || f.lip || f.it) continue;
      if (SH.keel) { if (/^(mast|wire|spar|sail|batten|furl)/.test(f.mat || '')) continue; let zm = 1e9; for (const q of f.v) if (q[2] < zm) zm = q[2]; if (zm > zCut) continue; }
      raw.push(f); }
    let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9, z0 = 1e9, z1 = -1e9;
    for (const f of raw) for (const q of f.v) { if (q[0] < x0) x0 = q[0]; if (q[0] > x1) x1 = q[0]; if (q[1] < y0) y0 = q[1]; if (q[1] > y1) y1 = q[1]; if (q[2] < z0) z0 = q[2]; if (q[2] > z1) z1 = q[2]; }
    const L = y1 - y0, yc = (y0 + y1) / 2; let kz = 1e9;
    // the keel: the lowest skin on the centreline over the middle of the hull — drives, props and rudders aft do not count
    for (const f of raw) for (const q of f.v) if (Math.abs(q[0]) < 0.3 && q[1] > y0 + 0.18 * L && q[1] < y1 - 0.22 * L && q[2] < kz) kz = q[2];
    if (kz > 1e8) kz = z0;
    let ky0 = 1e9, ky1 = -1e9; for (const f of raw) for (const q of f.v) if (Math.abs(q[0]) < 0.35 && q[2] < kz + 0.12) { if (q[1] < ky0) ky0 = q[1]; if (q[1] > ky1) ky1 = q[1]; }
    const faces = raw.map(f => ({ v: f.v.map(q => [q[0], q[1] - yc, q[2] - kz]), mat: mk(f.mat), b: f.b || 0, db: f.db || 0 }));
    // the sections, per 0.8 m station: the lowest skin per twentieth of the half-breadth, the top, the half-breadth and its height
    const NY = Math.max(6, Math.round(L / 0.8)), NX = 20, HB = Math.max(-x0, x1, 0.3), bot = new Float32Array(NY * NX).fill(1e9), top = new Float32Array(NY).fill(-1e9), half = new Float32Array(NY), shz = new Float32Array(NY);
    for (const f of faces) for (const q of f.v) { const iy = clamp(Math.floor((q[1] + L / 2) / L * NY), 0, NY - 1), ax = Math.abs(q[0]), ix = clamp(Math.floor(ax / HB * NX), 0, NX - 1), j = iy * NX + ix;
      if (q[2] < bot[j]) bot[j] = q[2]; if (q[2] > top[iy]) top[iy] = q[2]; if (ax > half[iy]) { half[iy] = ax; shz[iy] = q[2]; } }
    for (let iy = 0; iy < NY; iy++) for (let ix = 0; ix < NX; ix++) { const j = iy * NX + ix; if (bot[j] < 1e8) continue; let a = ix - 1, b = ix + 1;
      while (a >= 0 && bot[iy * NX + a] > 1e8) a--; while (b < NX && bot[iy * NX + b] > 1e8) b++;
      bot[j] = a >= 0 && b < NX ? bot[iy * NX + a] + (bot[iy * NX + b] - bot[iy * NX + a]) * (ix - a) / (b - a) : a >= 0 ? bot[iy * NX + a] : b < NX ? bot[iy * NX + b] : 0; }
    const iyOf = (y) => clamp(Math.floor((y + L / 2) / L * NY), 0, NY - 1);
    const B = { type, paint: p, paints, label: SH.label, loa: SH.loa, beam: SH.beam, draft: SH.draft, t: SH.t, keel: !!SH.keel, store: SH.store, rig: T.global, faces, L: r3(L), HB: r3(HB), depth: r3(z1 - kz),
      deck: r3(deckRaw - kz), board: r3(Math.max(0.3, boardRaw - kz)), keelY: [r3((ky0 < 1e8 ? ky0 : y0 + 0.3 * L) - yc), r3((ky1 > -1e8 ? ky1 : y1 - 0.3 * L) - yc)], faceCount: faces.length, stripped: !!SH.keel,
      bottomAt: (x, y) => bot[iyOf(y) * NX + clamp(Math.floor(Math.abs(x) / HB * NX), 0, NX - 1)], halfAt: (y) => Math.max(0.15, half[iyOf(y)]), topAt: (y) => top[iyOf(y)], sheerAt: (y) => shz[iyOf(y)],
      sideAt: (zk, y) => { let m = -1; for (const f of faces) for (const q of f.v) if (Math.abs(q[1] - y) < 0.7 && Math.abs(q[2] - zk) < 0.14 && Math.abs(q[0]) > m) m = Math.abs(q[0]); return m; } };
    LC.set(ck, B); return B;
  }
  // bow toward heading hd (radians in the part's frame, 0 = +x); hull frame x starboard, y bow, z up from the keel
  function hullXf(hd, cx, cy, cz) { const c = Math.cos(hd), s = Math.sin(hd); return (q) => [cx + q[0] * s + q[1] * c, cy - q[0] * c + q[1] * s, cz + q[2]]; }
  function placeHull(out, B, T) { at('boat', 0, 1); for (const f of B.faces) out.push({ v: f.v.map(T), mat: f.mat, b: f.b, db: f.db, uv: null, tex: null, part: 'boat', grp: 0, lay: 1 }); }
  // winter: a heat-shrunk film over a ridge frame, tucked under the rub rail, closed flat at the transom
  function wrapHull(out, B, T, mat) {
    at('wrap', 0, 1); const n = Math.max(6, Math.round(B.L / 1.4)), L = B.L, st = [];
    for (let k = 0; k <= n; k++) { const y = -L / 2 + 0.2 + (L - 0.4) * k / n, hb = B.halfAt(y) + 0.05, zs = B.sheerAt(y) - 0.2, zt = Math.max(B.topAt(y), zs + 0.4) + 0.22; st.push({ y, hb: k === n ? hb * 0.3 : hb, zs, zt }); }
    for (let k = 0; k < n; k++) { const a = st[k], b = st[k + 1];
      for (const sx of [-1, 1]) { const A0 = T([sx * a.hb, a.y, a.zs]), A1 = T([0, a.y, a.zt]), B1 = T([0, b.y, b.zt]), B0 = T([sx * b.hb, b.y, b.zs]);
        out.push(F([A0, A1, B1, B0], mat, 0, 0.01, [[a.y, 0], [a.y, 1], [b.y, 1], [b.y, 0]], TX.wrap));
        out.push(F([T([sx * (a.hb - 0.06), a.y, a.zs - 0.32]), A0, B0, T([sx * (b.hb - 0.06), b.y, b.zs - 0.32])], mat, 0, 0.01)); } }
    const a = st[0]; out.push(F([T([-a.hb, a.y, a.zs]), T([0, a.y, a.zt]), T([a.hb, a.y, a.zs]), T([a.hb - 0.06, a.y, a.zs - 0.32]), T([-a.hb + 0.06, a.y, a.zs - 0.32])], mat, 0, 0.01));
  }
  function hullRec(s, a, B, T0, keelZ, g, store, more) {
    const L = B.L, foot = [[-B.HB, -L / 2], [B.HB, -L / 2], [B.HB, L / 2], [-B.HB, L / 2]].map(([x, y]) => T0([x, y, 0]).slice(0, 2).map(r3));
    return rec(s, Object.assign({ k: 'hull', id: a.id, type: B.type, label: B.label, paint: B.paint, rig: B.rig, loa: B.loa, beam: B.beam, draft: B.draft, t: B.t, store, keelZ: r3(keelZ), ground: g,
      heading: dirYaw(T0, [0, 1]), foot, deckZ: r3(keelZ + B.deck), topZ: r3(keelZ + B.depth), faces: B.faceCount, mastUnstepped: B.stripped }, more || {}));
  }

  // ============================ THE PARTS (local frame; a.g is the ground height) =========================================
  const BUILD = {};
  BUILD.ground = function (out, s, a) { at('ground', 0, 0); const hx = a.w / 2, hy = a.d / 2, z = a.g, t = a.t || 0.15;
    box(out, -hx, hx, -hy, hy, z - t, z, a.surf === 'asph' ? 'sy.asph' : 'sy.conc', 0, a.surf === 'asph' ? TX.asph : TX.conc(a.J || 4.5, a.seed || 11), false);
    for (const L of a.lines || []) { at('lines', 0, 0); quad(out, [L[0], L[1], z + 0.004], [L[2], L[1], z + 0.004], [L[2], L[3], z + 0.004], [L[0], L[3], z + 0.004], 'sy.line', null, null, 0.03); }
    rec(s, { k: 'walk', kind: a.surf === 'asph' ? 'asphalt' : 'concrete', poly: [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]], z: r3(z), surface: a.surf === 'asph' ? 'asphalt' : 'concrete slab' }); };

  BUILD.rack = function (out, s, a) {
    const g = a.g, yf = a.yf, bays = a.bays, W = bays.reduce((n, b) => n + b.w, 0), colTop = a.colTop || 8.9; let x = -W / 2; const frames = [x];
    for (const b of bays) { x += b.w; frames.push(x); }
    at('rack', 0, 1);
    const depthAt = (k) => Math.max(k > 0 ? bays[k - 1].depth : 0, k < bays.length ? bays[k].depth : 0);
    frames.forEach((fx, k) => { const D = depthAt(k); for (const y of [yf, yf - D / 2, yf - D]) { box(out, fx - 0.125, fx + 0.125, y - 0.125, y + 0.125, g, g + colTop, 'sy.rack', 0, null, false); box(out, fx - 0.25, fx + 0.25, y - 0.25, y + 0.25, g, g + 0.03, 'sy.galv', 0, null, false); }
      box(out, fx - 0.09, fx + 0.09, yf - D, yf, g + colTop - 0.3, g + colTop, 'sy.rack', 0, null, false); });
    bays.forEach((b, k) => { const xa = frames[k], xb = frames[k + 1], xc = (xa + xb) / 2, yb = yf - b.depth, bx = b.bunk;
      tube(out, [xa, yb, g + 0.4], [xb, yb, g + colTop - 0.5], 0.035, 5, 'sy.galv', 0, null, false); tube(out, [xb, yb, g + 0.4], [xa, yb, g + colTop - 0.5], 0.035, 5, 'sy.galv', 0, null, false);
      b.tiers.forEach((tz, ti) => { const z = g + tz;
        for (const y of [yf, yf - b.depth / 2, yb]) box(out, xa + 0.125, xb - 0.125, y - 0.1, y + 0.1, z - 0.3, z, 'sy.rack', 0, null, false);
        for (const sx of [-1, 1]) box(out, xc + sx * bx - 0.05, xc + sx * bx + 0.05, yb - 0.1, yf + 0.3, z, z + 0.12, 'sy.carpet', 0, null, false);
        const H = b.hulls && b.hulls[ti], id = 'bay' + k + '.tier' + ti, slot = { k: 'slot', id, bay: k, tier: ti, at: [r3(xc), r3(yf - b.depth / 2)], z: r3(z + 0.12), width: r3(b.w - 0.25), depth: r3(b.depth),
          maxBeam: r3(b.w - 0.55), maxLoa: r3(b.depth + 0.6), cap_t: b.cap, forkZ: r3(z + 0.45), hull: null, face: [[r3(xa), r3(yf)], [r3(xb), r3(yf)]] };
        if (H && H !== 'forks') { const B = landHull(H[0], H[1]); if (B) { const keelZ = z + 0.12 - B.bottomAt(bx, 0), cy = yf - 0.25 - B.L / 2, T = hullXf(-HP, xc, cy, keelZ);
            placeHull(out, B, T); if (a.wrap && B.store === 'rack') wrapHull(out, B, T, (k + ti) & 1 ? 'sy.wrapB' : 'sy.wrapW'); at('rack', 0, 1);
            slot.hull = B.label; hullRec(s, { id: 'rack.' + id }, B, hullXf(-HP, xc, cy, 0), keelZ, g, 'rack', { slot: id, wrap: !!a.wrap }); } }
        if (H === 'forks') slot.hull = 'arriving on the forks';
        rec(s, slot); }); });
    rec(s, { k: 'block', kind: 'rack', poly: [[-W / 2, yf - Math.max(...bays.map(b => b.depth))], [W / 2, yf - Math.max(...bays.map(b => b.depth))], [W / 2, yf], [-W / 2, yf]], z0: g, z1: r3(g + colTop) });
    rec(s, { k: 'rack', width: r3(W), front: yf, bays: bays.length, slots: bays.reduce((n, b) => n + b.tiers.length, 0), colTop: r3(colTop), frames: frames.map(r3) });
    if (a.spot) { const sx = a.spot[0], sy = a.spot[1]; verb(s, { verb: 'spotLoad', id: 'rack.spot', fixture: 'rack front', stand: pt3([sx, sy, g]), yaw: a.spot[2] || 0, show: true,
      seq: [{ clip: 'idle', loop: true, note: 'the spotter at the rack front, watching the hull clear the bunks; hand signals are the engine’s' }], fits: [], anim: { clip: 'idle' } }); }
  };

  BUILD.hard = function (out, s, a) {
    const g = a.g, B = landHull(a.type, a.paint), hd = a.hd != null ? a.hd : -HP;
    if (!B) { rec(s, { k: 'hull', id: a.id, type: a.type, missing: true, label: (SHIPS[a.type] || {}).label || a.type, store: 'hard' }); return; }
    const keelZ = g + (a.lift != null ? a.lift : B.keel ? 0.35 : 0.55), T = hullXf(hd, 0, 0, keelZ), T0 = hullXf(hd, 0, 0, 0), L = B.L, cradle = !!a.cradle;
    const nb = B.keel ? 2 : L > 20 ? 4 : L > 9 ? 3 : 2, ky0 = B.keelY[0], ky1 = B.keelY[1], kl = Math.max(0.6, ky1 - ky0), blocks = [], stands = [];
    inFrame(out, T0, (o) => { at('blocks', 0, 1);
      for (let i = 0; i < nb; i++) { const y = ky0 + 0.3 + (kl - 0.6) * (nb === 1 ? 0.5 : i / (nb - 1)); let z = g, k = 0;
        while (z < keelZ - 0.01) { const t = Math.min(0.2, keelZ - z); if (k & 1) box(o, -0.14, 0.14, y - 0.55, y + 0.55, z, z + t, 'sy.block', 0, G.sawnTex(), false); else box(o, -0.6, 0.6, y - 0.14, y + 0.14, z, z + t, 'sy.block', 0, G.sawnTex(), false); z += t; k++; }
        blocks.push({ at: T0([0, y, 0]).slice(0, 2).map(r3), top: r3(keelZ) }); } });
    const fr = L > 20 ? [-0.34, -0.12, 0.12, 0.34] : L > 12 ? [-0.3, 0, 0.3] : L > 6.5 ? [-0.24, 0.24] : [0];
    if (!cradle) inFrame(out, T0, (o) => { at('stand', 0, 1);
      for (const f of fr) { const y = f * L; for (const sx of [-1, 1]) { const x = sx * B.halfAt(y) * 0.58, zp = keelZ + B.bottomAt(x, y); if (zp - g < 0.45) continue;
        const q = standAt(o, x + sx * 0.12, y, g, zp, a.standMat || (B.keel ? 'sy.blue' : 'sy.galv')); stands.push({ x: x + sx * 0.12, y, sx, zp, nutZ: q.nutZ }); } }
      at('chain', 0, 1); for (const sx of [-1, 1]) { const S = stands.filter(q => q.sx === sx).sort((p, q) => p.y - q.y);
        for (let i = 0; i + 1 < S.length; i++) MG.sagLine(o, [S[i].x + sx * 0.3, S[i].y, g + 0.3], [S[i + 1].x + sx * 0.3, S[i + 1].y, g + 0.3], 0.12, 8, 'sy.galv', 0.01); } });
    else inFrame(out, T0, (o) => { at('cradle', 0, 1); const yl = L * 0.3;
      for (const sx of [-1, 1]) box(o, sx * 0.85 - 0.1, sx * 0.85 + 0.1, -yl, yl, g, g + 0.2, 'sy.rust', 0, null, false);
      for (const y of [-yl, 0, yl]) box(o, -0.95, 0.95, y - 0.1, y + 0.1, g + 0.2, g + 0.34, 'sy.rust', 0, null, false);
      for (const f of [-0.24, 0.24]) for (const sx of [-1, 1]) { const y = f * L, x = sx * B.halfAt(y) * 0.6, zp = keelZ + B.bottomAt(x, y);
        bar(o, [sx * 0.85, y, g + 0.2], [x + sx * 0.05, y, zp - 0.3], 0.12, 0.12, 'sy.rust'); tube(o, [x + sx * 0.05, y, zp - 0.32], [x + sx * 0.05, y, zp - 0.05], 0.03, 6, 'sy.steel', 0, null, true);
        box(o, x - 0.15, x + 0.15, y - 0.2, y + 0.2, zp - 0.05, zp, 'sy.carpet', 0, null, false); stands.push({ x: x + sx * 0.05, y, sx, zp, nutZ: zp - 0.3, cradle: true }); }
      for (const sx of [-1, 1]) bar(o, [sx * 0.85, -yl, g + 0.2], [sx * 0.85, -yl * 0.2, g + 1.1], 0.08, 0.08, 'sy.rust'); });
    placeHull(out, B, T);
    if (a.wrap) wrapHull(out, B, T, a.wrap === 'blue' ? 'sy.wrapB' : 'sy.wrapW');
    let lad = null;
    if (a.ladder !== false && !a.wrap) { const ly = -L / 2 - 0.3, lx = a.ladderX != null ? a.ladderX : -Math.min(0.9, B.HB * 0.35), zL = keelZ + B.board; let rungs = [];
      inFrame(out, T0, (o) => { rungs = ladderAt(o, lx, ly, g, zL, 'sy.alum'); });
      climbVerbs(s, a.id + '.ladder', 'boat ladder at the stern', (dx, dy) => T0([lx + dx, ly + dy, 0]), dirYaw(T0, [0, 1]), g, zL, rungs, 'aboard over the ' + (B.keel ? 'swim platform' : 'transom') + ' of the ' + B.label, a.showClimb);
      lad = { at: T0([lx, ly, 0]).slice(0, 2).map(r3), landingZ: r3(zL), rungs: rungs.length }; }
    // sand and antifoul where the skin is 1.05 m over the ground (reach 0.95 + the grip's 0.095): the fin, on a keel boat
    const zk = g + 1.05 - keelZ, ys = B.keel ? [(ky0 + ky1) / 2] : [-0.18 * L, 0.18 * L];
    ys.forEach((y, i) => { const xs = B.sideAt(zk, y); if (xs < 0) return; const p = T0([xs + 0.42, y, 0]);
      verb(s, { verb: 'sandBottom', also: ['antifoul'], id: a.id + '.sand' + i, fixture: B.keel ? 'keel fin' : 'hull bottom', stand: pt3([p[0], p[1], g]), yaw: dirYaw(T0, [-1, 0]), show: i === 0 && a.showSand !== false,
        seq: [{ clip: 'reach', loop: true, opts: { lift: 0.95 }, note: 'the sander or the roller on the skin at 1.05 m' }], fits: [['rest', 0.95]], anim: { clip: 'reach', opts: { lift: 0.95 } } }); });
    stands.forEach((q, i) => { const p = T0([q.x + q.sx * 0.6, q.y, 0]), lift = q.nutZ - g;
      verb(s, { verb: 'setStand', id: a.id + '.stand' + i, fixture: q.cradle ? 'cradle pad screw' : 'jack stand screw', stand: pt3([p[0], p[1], g]), yaw: dirYaw(T0, [-q.sx, 0]),
        seq: [{ clip: 'reach', loop: true, opts: { lift: r3(Math.min(lift, 1.012)) }, note: 'the bar through the screw at ' + r2(lift) + ' m', request: lift > 1.012 ? 'stepLadder' : undefined }], fits: [['rest', lift]] }); });
    if (!cradle && stands.length > 2) for (const sx of [-1, 1]) { const S = stands.filter(q => q.sx === sx); if (S.length < 2) continue; const p = T0([S[0].x + sx * 0.75, (S[0].y + S[1].y) / 2, 0]);
      verb(s, { verb: 'chainStands', id: a.id + '.chain' + (sx < 0 ? 'P' : 'S'), fixture: 'stand chain', stand: pt3([p[0], p[1], g]), yaw: dirYaw(T0, [-sx, 0]), seq: [{ clip: 'reach', loop: true, opts: { lift: 0.3 }, note: 'the chain hooks on the stand legs at 0.30 m', request: 'crouchWork' }], fits: [['rest', 0.3]] }); }
    hullRec(s, a, B, T0, keelZ, g, cradle ? 'cradle' : 'hard', { wrap: a.wrap || null, blocks, stands: stands.map(q => ({ at: T0([q.x, q.y, 0]).slice(0, 2).map(r3), padZ: r3(q.zp), nutZ: r3(q.nutZ) })), ladder: lad });
    rec(s, { k: 'block', kind: 'hull on the hard', poly: [[-B.HB - 0.7, -L / 2 - 0.4], [B.HB + 0.7, -L / 2 - 0.4], [B.HB + 0.7, L / 2], [-B.HB - 0.7, L / 2]].map(([x, y]) => T0([x, y, 0]).slice(0, 2).map(r3)), z0: g, z1: r3(keelZ + B.depth) });
  };

  BUILD.lift = function (out, s, a) {
    const g = a.g, tw = a.tw || 6.0, wb = a.wb || 5.8, hg = a.hg || 9.2; at('lift', 0, 1);
    for (const sx of [-1, 1]) { const x = sx * tw;
      box(out, x - 0.45, x + 0.45, -wb - 0.9, wb + 0.9, g + hg, g + hg + 1.5, 'sy.yel', 0, null, false);
      for (const sy of [-1, 1]) { const y = sy * wb;
        box(out, x - 0.42, x + 0.42, y - 1.35, y + 1.35, g + 0.72, g + 1.55, 'sy.yel', 0, null, false);
        for (const dy of [-0.78, 0.78]) wheelX(out, x, y + dy, g + 0.72, 0.72, 0.55, 'sy.tyre', 'sy.steel');
        box(out, x - 0.4, x + 0.4, y - 0.5, y + 0.5, g + 1.55, g + hg, 'sy.yel', 0, null, false);
        bar(out, [x, y - sy * 0.5, g + hg - 2.3], [x, y - sy * 2.6, g + hg], 0.32, 0.32, 'sy.yel'); } }
    box(out, -tw - 0.45, tw + 0.45, -wb - 0.9, -wb + 0.2, g + hg + 0.2, g + hg + 1.5, 'sy.yel', 0, null, false);
    box(out, -tw - 2.1, -tw - 0.45, -wb - 1.1, -wb + 1.6, g + 1.9, g + 3.9, 'sy.yel', 0, null, false);
    quad(out, [-tw - 2.11, -wb - 0.8, g + 2.2], [-tw - 2.11, -wb + 1.3, g + 2.2], [-tw - 2.11, -wb + 1.3, g + 3.5], [-tw - 2.11, -wb - 0.8, g + 3.5], 'sy.steel', [[0, 0], [2.1, 0], [2.1, 1.3], [0, 1.3]], TX.corr(0.08), 0.02);
    tube(out, [-tw - 1.2, -wb + 1.2, g + 3.9], [-tw - 1.2, -wb + 1.2, g + 4.9], 0.08, 8, 'sy.steel', 0, null, true);
    box(out, -tw - 1.95, -tw - 0.45, wb - 0.9, wb + 0.9, g + 2.3, g + 4.3, 'sy.glass', 0, null, false); box(out, -tw - 2.05, -tw - 0.4, wb - 1.0, wb + 1.0, g + 4.3, g + 4.5, 'sy.yel', 0, null, false);
    box(out, -tw - 1.95, -tw - 0.45, wb - 0.9, wb + 0.9, g + 1.9, g + 2.3, 'sy.yel', 0, null, false);
    at('beacon', 0, 1); box(out, -tw - 1.3, -tw - 1.1, wb - 0.1, wb + 0.1, g + 4.5, g + 4.72, 'sy.amber', 0, null, false); MG.lampAt(s, -tw - 1.2, wb, g + 4.8, 4, 0.35, '#ffb044');
    const H = a.hull && landHull(a.hull.type, a.hull.paint); let hullR = null, slings = [];
    if (H) { const keelZ = a.hull.keelZ, cy = a.hull.cy || 0, T = hullXf(HP, 0, cy, keelZ), T0 = hullXf(HP, 0, cy, 0); placeHull(out, H, T);
      const sy = a.slingY || [-H.L * 0.2, H.L * 0.22]; at('sling', 0, 1);
      for (const ys of sy) { const yh = ys - cy, hb = H.halfAt(yh) + 0.04, zs = keelZ + H.sheerAt(yh), eye = zs + 0.9, ex = hb + 0.3, pts = [[-ex, eye], [-hb, zs - 0.25]];
        for (let i = 0; i <= 12; i++) { const x = -hb + 2 * hb * i / 12; pts.push([x, keelZ + H.bottomAt(x, yh) - 0.04]); }
        pts.push([hb, zs - 0.25], [ex, eye]);
        for (let i = 0; i + 1 < pts.length; i++) { const p = pts[i], q = pts[i + 1]; quad(out, [p[0], ys - 0.18, p[1]], [q[0], ys - 0.18, q[1]], [q[0], ys + 0.18, q[1]], [p[0], ys + 0.18, p[1]], 'sy.sling', null, null, 0.015); }
        at('hoist', 0, 1);
        for (const sx of [-1, 1]) { const bx = sx * (tw - 0.25); box(out, bx - 0.3, bx + 0.3, ys - 0.4, ys + 0.4, g + hg - 0.75, g + hg, 'sy.steel', 0, null, false);
          for (const d of [-0.14, 0.14]) tube(out, [bx, ys + d, g + hg - 0.75], [sx * ex, ys + d, eye], 0.02, 4, 'sy.galv', 0, null, false);
          box(out, sx * ex - 0.12, sx * ex + 0.12, ys - 0.22, ys + 0.22, eye - 0.1, eye + 0.18, 'sy.steel', 0, null, false); }
        at('sling', 0, 1); slings.push({ y: r3(ys), eyeZ: r3(eye), under: r3(keelZ - 0.04) }); }
      hullR = hullRec(s, { id: 'lift.hull' }, H, T0, keelZ, g, 'slings', { slings });
      // pressure-wash her as she comes out: from the west runway, the wand at its highest rest
      if (a.wash) verb(s, { verb: 'pressureWash', id: 'lift.wash', fixture: 'hull in the slings', stand: pt3([-tw + 0.9, a.wash, g]), yaw: 90, show: true,
        seq: [{ clip: 'reach', loop: true, opts: { lift: 1.012 }, note: 'the lance up at the bottom as she drips', request: 'sprayUp' }], fits: [['rest', 1.012]], anim: { clip: 'reach', opts: { lift: 1.012 } } }); }
    verb(s, { verb: 'driveLift', id: 'lift.drive', fixture: 'travel lift remote', stand: pt3([tw - 0.6, -wb - 2.4, g]), yaw: 0, show: true,
      seq: [{ clip: 'walk', loop: true, speed: 0.4, note: 'the lift creeps at 0.40 m/s; the operator walks the east runway behind it', request: 'carryRemote' }], fits: [], anim: { clip: 'walk', move: { d: [0, 1, 0], len: 1.6, rate: 0.4 } } });
    const clear = r3(2 * tw - 0.8);
    rec(s, { k: 'lift', capacity_t: a.cap || 150, clearWidth: clear, underBeamZ: r3(hg - 0.75), track: r3(2 * tw), wheelbase: r3(2 * wb), maxBeam: r3(clear - 1.6), maxLoa: 32, openEnd: '+y (masts and bows pass)', creep_mps: 0.4, hull: hullR ? hullR.label : null, slings });
    for (const sx of [-1, 1]) for (const sy of [-1, 1]) rec(s, { k: 'block', kind: 'lift leg', poly: [[sx * tw - 0.45, sy * wb - 1.4], [sx * tw + 0.45, sy * wb - 1.4], [sx * tw + 0.45, sy * wb + 1.4], [sx * tw - 0.45, sy * wb + 1.4]], z0: g, z1: r3(g + hg) });
  };

  BUILD.forklift = function (out, s, a) {
    const g = a.g, fz = a.forkZ, FL = a.forkLen || 5.6, mast = Math.max(7.8, fz - g + 2.8); at('forklift', 0, 1);
    box(out, -1.5, 1.5, -4.2, 1.2, g + 0.55, g + 1.75, 'sy.yel', 0, null, false); box(out, -1.55, 1.55, -4.45, -3.0, g + 0.5, g + 2.35, 'sy.yel', 0, null, false);
    quad(out, [-1.56, -4.2, g + 0.9], [1.56, -4.2, g + 0.9], [1.56, -4.2, g + 1.3], [-1.56, -4.2, g + 1.3], 'sy.steel', [[0, 0], [3.1, 0], [3.1, 0.4], [0, 0.4]], TX.corr(0.1), 0.02);
    for (const sx of [-1, 1]) { wheelX(out, sx * 1.25, 0.3, g + 0.78, 0.78, 0.72, 'sy.tyre', 'sy.yel'); wheelX(out, sx * 1.15, -3.2, g + 0.64, 0.64, 0.5, 'sy.tyre', 'sy.yel'); }
    box(out, -1.5, -0.25, -1.9, 0.1, g + 1.75, g + 3.55, 'sy.glass', 0, null, false); box(out, -1.58, -0.17, -1.98, 0.18, g + 3.55, g + 3.7, 'sy.yel', 0, null, false);
    for (const [x, y] of [[-1.5, -1.9], [-0.25, -1.9], [-1.5, 0.1], [-0.25, 0.1]]) box(out, x - 0.04, x + 0.04, y - 0.04, y + 0.04, g + 1.75, g + 3.55, 'sy.yel', 0, null, false);
    for (const z of [g + 0.95, g + 1.3]) box(out, -1.9, -1.5, -0.9, -0.5, z - 0.04, z, 'sy.steel', 0, null, false);
    at('beacon', 0, 1); box(out, -0.95, -0.75, -1.0, -0.8, g + 3.7, g + 3.9, 'sy.amber', 0, null, false); MG.lampAt(s, -0.85, -0.9, g + 4.0, 4, 0.35, '#ffb044');
    at('mast', 0, 1);
    for (const sx of [-1, 1]) { box(out, sx * 0.85 - 0.11, sx * 0.85 + 0.11, 1.35, 1.65, g + 0.45, g + mast, 'sy.yel', 0, null, false);
      box(out, sx * 0.62 - 0.08, sx * 0.62 + 0.08, 1.45, 1.7, Math.max(g + 0.5, fz - 1.6), fz + 2.0, 'sy.yel', 0, null, false);
      tube(out, [sx * 0.95, 0.0, g + 1.6], [sx * 0.95, 1.35, g + 2.4], 0.07, 8, 'sy.galv', 0, null, true); }
    box(out, -1.0, 1.0, 1.35, 1.65, g + mast - 0.25, g + mast, 'sy.yel', 0, null, false);
    box(out, -1.15, 1.15, 1.7, 1.9, fz - 1.1, fz + 0.4, 'sy.steel', 0, null, false);
    for (const sx of [-1, 1]) { const x = sx * 0.6; box(out, x - 0.1, x + 0.1, 1.9, 1.9 + FL, fz - 0.17, fz, 'sy.steel', 0, null, false); box(out, x - 0.09, x + 0.09, 2.2, 1.9 + FL - 0.2, fz, fz + 0.03, 'sy.carpet', 0, null, false);
      box(out, x - 0.1, x + 0.1, 1.9, 2.05, fz, fz + 1.0, 'sy.steel', 0, null, false); }
    const H = a.load && landHull(a.load.type, a.load.paint); let hr = null;
    if (H) { const keelZ = fz + 0.03 - H.bottomAt(0.6, 0), cy = 2.1 + H.L / 2, T = hullXf(HP, 0, cy, keelZ); placeHull(out, H, T); hr = hullRec(s, { id: 'forks.hull' }, H, hullXf(HP, 0, cy, 0), keelZ, g, 'forks'); }
    const seat = [-0.87, -0.9, g + 1.75];
    verb(s, { verb: 'driveForklift', id: 'forklift.drive', fixture: 'forklift cab seat', stand: pt3([-1.95, -0.9, g]), yaw: 90, seat: pt3(seat), cabFloorZ: r3(g + 1.75),
      seq: [{ clip: 'mountCab', once: true, note: 'up the two steps and into the cab from the left', request: 'cabSteps' }, { clip: 'drive', loop: true, opts: { seatZ: 0.27 }, note: 'the seat 0.27 m over the cab floor' }, { clip: 'mountCabDown', once: true }],
      fits: [['seat', 0.27]] });
    rec(s, { k: 'forklift', capacity_t: a.cap || 9.0, loadCentre_m: 3.0, forkLen: FL, forkZ: r3(fz), liftTo: r3(g + 11.0), belowGrade_m: 2.4, mastTop: r3(g + mast), load: hr ? hr.label : null, turning_m: 12.5 });
    rec(s, { k: 'block', kind: 'forklift', poly: [[-1.6, -4.5], [1.6, -4.5], [1.6, 1.9 + FL], [-1.6, 1.9 + FL]], z0: g, z1: r3(g + mast) });
  };

  // the workshop: steel portal frames, clad walls and roof, a roll-up door at +y; the near walls cut at cutH toward the facing
  BUILD.shed = function (out, s, a) {
    const g = a.g, S = a.S || 14, Ln = a.Ln || 22, He = a.He || 8.4, Hr = a.Hr || 9.6, hx = S / 2, hy = Ln / 2, cut = a.cut || 'none', cutH = a.cutH || 1.35, dw = a.doorW || 11, dh = a.doorH || 7.9;
    const clad = a.colour === 'corr' ? 'sy.corr' : a.colour === 'green' ? 'sy.green' : 'sy.red', th = (a.cutDir || 0) * PI / 4, fw = [Math.sin(th), Math.cos(th)], rot = a.rot || 0;
    const near = (n) => { const c = Math.cos(rot), sn = Math.sin(rot), w = [n[0] * c - n[1] * sn, n[0] * sn + n[1] * c]; return cut === 'section' && w[0] * fw[0] + w[1] * fw[1] < -0.25; };
    at('ground', 0, 0); box(out, -hx, hx, -hy, hy, g - 0.15, g, 'sy.conc', 0, TX.conc(4.0, 23), false);
    rec(s, { k: 'walk', kind: 'shed floor', poly: [[-hx + 0.2, -hy + 0.2], [hx - 0.2, -hy + 0.2], [hx - 0.2, hy - 0.2], [-hx + 0.2, hy - 0.2]], z: r3(g), surface: 'power-floated concrete' });
    // walls as rectangles in (u along, z) with openings left out; a cut wall is thick, capped in poché
    const walls = [
      { id: 'west', o: [-hx, hy], d: [0, -1], n: [-1, 0], L: Ln, rects: [[0, Ln, 0, He]], gable: false, win: [[2, 6], [8.5, 12.5], [15, 19]] },
      { id: 'east', o: [hx, -hy], d: [0, 1], n: [1, 0], L: Ln, rects: [[0, 3.6, 0, He], [3.6, 4.6, 2.2, He], [4.6, Ln, 0, He]], gable: false, win: [[6, 10], [12.5, 16.5]] },
      { id: 'south', o: [-hx, -hy], d: [1, 0], n: [0, -1], L: S, rects: [[0, S, 0, He]], gable: true, win: [] },
      { id: 'north', o: [hx, hy], d: [-1, 0], n: [0, 1], L: S, rects: [[0, hx - dw / 2, 0, He], [hx + dw / 2, S, 0, He], [hx - dw / 2, hx + dw / 2, dh, He]], gable: true, win: [] },
    ];
    const cuts = [];
    for (const Wl of walls) { const isNear = near(Wl.n), P = (u, z, off) => [Wl.o[0] + Wl.d[0] * u - Wl.n[0] * (off || 0), Wl.o[1] + Wl.d[1] * u - Wl.n[1] * (off || 0), g + z];
      at('wall', 0, isNear ? 1 : 0);
      for (const [u0, u1, z0r, z1r] of Wl.rects) { const z0 = z0r, z1 = isNear ? Math.min(z1r, cutH) : z1r; if (z1 <= z0 + 0.01) continue;
        quad(out, P(u0, z0), P(u1, z0), P(u1, z1), P(u0, z1), clad, [[u0, z0], [u1, z0], [u1, z1], [u0, z1]], TX.corr(0.2));
        if (isNear) { quad(out, P(u0, z0, 0.12), P(u1, z0, 0.12), P(u1, z1, 0.12), P(u0, z1, 0.12), 'sy.white', null, null); if (z1 === cutH) quad(out, P(u0, z1), P(u1, z1), P(u1, z1, 0.12), P(u0, z1, 0.12), 'sy.poche', null, null, 0.02); } }
      if (isNear) cuts.push(Wl.id);
      if (Wl.gable && !isNear && cut !== 'roof' && cut !== 'section') out.push(F([P(0, He), P(Wl.L, He), P(Wl.L / 2, Hr)], clad, 0, 0, [[0, He], [Wl.L, He], [Wl.L / 2, Hr]], TX.corr(0.2)));
      if (Wl.gable && !isNear && (cut === 'roof' || cut === 'section')) out.push(F([P(0, He), P(Wl.L, He), P(Wl.L / 2, Hr)], clad, 0, 0, [[0, He], [Wl.L, He], [Wl.L / 2, Hr]], TX.corr(0.2)));
      at('window', 0, 0); for (const [u0, u1] of Wl.win) if (!isNear || cutH > 3.2) quad(out, P(u0, 2.3, -0.01), P(u1, 2.3, -0.01), P(u1, 3.2, -0.01), P(u0, 3.2, -0.01), 'sy.glass', null, null, 0.03); }
    // the frames: five portals; the near columns cut with their wall, the rafters and roof gone in a cutaway
    at('frame', 0, 1);
    for (let k = 0; k <= 4; k++) { const y = -hy + 0.2 + (Ln - 0.4) * k / 4;
      for (const sx of [-1, 1]) { const top = near([sx, 0]) ? cutH : He; box(out, sx * (hx - 0.2) - 0.15, sx * (hx - 0.2) + 0.15, y - 0.12, y + 0.12, g, g + top, 'sy.steel', 0, null, false); }
      if (cut === 'none') for (const sx of [-1, 1]) bar(out, [sx * (hx - 0.2), y, g + He - 0.1], [0, y, g + Hr - 0.35], 0.22, 0.45, 'sy.steel'); }
    if (cut === 'none') { at('roof', 0, 1);
      for (const sx of [-1, 1]) { const a0 = [sx * (hx + 0.3), -hy - 0.3, g + He - 0.06], a1 = [sx * (hx + 0.3), hy + 0.3, g + He - 0.06], b1 = [0, hy + 0.3, g + Hr], b0 = [0, -hy - 0.3, g + Hr];
        out.push(F([a0, a1, b1, b0], 'sy.corr', 0, 0, [[0, -hy], [0, hy], [1, hy], [1, -hy]], TX.corrV(0.25)));
        for (const yk of [-hy + 3, 0.5, hy - 5.5]) { const t0 = 0.35, t1 = 0.7, P = (t, y) => [a0[0] + (b0[0] - a0[0]) * t, y, a0[2] + (b0[2] - a0[2]) * t + 0.02]; out.push(F([P(t0, yk), P(t0, yk + 2.5), P(t1, yk + 2.5), P(t1, yk)], 'sy.glass', 0, 0.02)); } } }
    // the roll-up door, rolled up, and the lamp over it
    at('door', 0, 1); box(out, -dw / 2 - 0.25, dw / 2 + 0.25, hy + 0.05, hy + 0.55, g + dh, g + dh + 0.55, 'sy.galv', 0, null, false);
    for (const sx of [-1, 1]) box(out, sx * (dw / 2 + 0.05) - 0.08, sx * (dw / 2 + 0.05) + 0.08, hy, hy + 0.12, g, g + dh, 'sy.galv', 0, null, false);
    box(out, -0.3, 0.3, hy + 0.05, hy + 0.35, g + dh + 0.65, g + dh + 0.9, 'sy.steel', 0, null, false); quad(out, [-0.25, hy + 0.36, g + dh + 0.66], [0.25, hy + 0.36, g + dh + 0.66], [0.25, hy + 0.36, g + dh + 0.8], [-0.25, hy + 0.36, g + dh + 0.8], 'sy.lens', null, null, 0.02);
    MG.lampAt(s, 0, hy + 0.6, g + dh + 0.6, 11, 0.85, '#ffe6bd');
    // high-bay lamps
    at('lamp', 0, 1); for (const [lx, ly] of [[-3.5, -hy / 2], [3.5, -hy / 2], [-3.5, hy / 2], [3.5, hy / 2]]) { tube(out, [lx, ly, g + He - 0.2], [lx, ly, g + He - 0.75], 0.01, 4, 'sy.steel', 0, null, false);
      tube(out, [lx, ly, g + He - 0.95], [lx, ly, g + He - 0.75], 0.24, 10, 'sy.galv', 0, null, true); quad(out, [lx - 0.17, ly - 0.17, g + He - 0.96], [lx + 0.17, ly - 0.17, g + He - 0.96], [lx + 0.17, ly + 0.17, g + He - 0.96], [lx - 0.17, ly + 0.17, g + He - 0.96], 'sy.lens', null, null, 0.02);
      MG.lampAt(s, lx, ly, g + He - 1.0, 12, 0.9, '#fff2d8'); }
    // the fit-out: bench and pegboard on the west wall, shelving, compressor, welding cart, drums, the bridge crane
    const bx0 = -hx + 0.25, bx1 = -hx + 1.0, by0 = 1.5, by1 = 6.5; at('bench', 0, 1);
    for (const [lx, ly] of [[bx0 + 0.06, by0 + 0.06], [bx1 - 0.06, by0 + 0.06], [bx0 + 0.06, by1 - 0.06], [bx1 - 0.06, by1 - 0.06]]) box(out, lx - 0.04, lx + 0.04, ly - 0.04, ly + 0.04, g, g + 0.76, 'sy.steel', 0, null, true);
    box(out, bx0, bx1, by0, by1, g + 0.76, g + 0.80, 'sy.ply', 0, G.plankTex(0.3), false); box(out, bx0 + 0.05, bx1 - 0.05, by0 + 0.05, by1 - 0.05, g + 0.18, g + 0.21, 'sy.ply', 0, null, false);
    for (const [ly, w, h] of [[2.2, 0.35, 0.22], [3.4, 0.5, 0.12], [5.6, 0.3, 0.3]]) box(out, bx0 + 0.15, bx0 + 0.15 + w * 0.8, ly, ly + w, g + 0.80, g + 0.80 + h, ly > 5 ? 'sy.red' : 'sy.tool', 0, null, false);
    if (!near([-1, 0]) || cutH > 2.4) { quad(out, [-hx + 0.05, by0, g + 1.05], [-hx + 0.05, by1, g + 1.05], [-hx + 0.05, by1, g + 2.3], [-hx + 0.05, by0, g + 2.3], 'sy.ply', [[by0, 1.05], [by1, 1.05], [by1, 2.3], [by0, 2.3]], TX.peg, 0.02);
      for (let i = 0; i < 9; i++) { const ly = by0 + 0.35 + i * 0.52, z = g + 1.3 + (i % 3) * 0.3; box(out, -hx + 0.06, -hx + 0.1, ly - 0.03, ly + 0.03, z, z + 0.22 + (i & 1) * 0.12, 'sy.tool', 0, null, false); } }
    verb(s, { verb: 'benchWork', also: ['mendGear', 'fitParts'], id: 'shed.bench', fixture: 'workbench', stand: pt3([bx1 + 0.38, (by0 + by1) / 2, g]), yaw: -90, show: true, seq: [{ clip: 'bench', loop: true, opts: { workZ: 0.80 } }], fits: [['bench', 0.80]], anim: { clip: 'bench', opts: { workZ: 0.80 } } });
    const sx0 = -hx + 0.2, sx1 = -hx + 0.7, sy0 = -8.5, sy1 = -4.5; at('shelves', 0, 1);
    for (const [lx, ly] of [[sx0, sy0], [sx1, sy0], [sx0, sy1], [sx1, sy1]]) box(out, lx - 0.03, lx + 0.03, ly - 0.03, ly + 0.03, g, g + 2.2, 'sy.galv', 0, null, false);
    for (const z of [0.35, 0.85, 1.35, 1.85]) { box(out, sx0, sx1, sy0, sy1, g + z - 0.03, g + z, 'sy.galv', 0, null, false); for (let i = 0; i < 5; i++) if (hs(i, z * 10, 3) > 0.3) box(out, sx0 + 0.05, sx1 - 0.05, sy0 + 0.1 + i * 0.78, sy0 + 0.6 + i * 0.78, g + z, g + z + 0.25 + hs(i, z * 10, 5) * 0.12, hs(i, z * 10, 7) > 0.5 ? 'sy.blue' : 'sy.ply', 0, null, false); }
    verb(s, { verb: 'fetchParts', also: ['stowParts'], id: 'shed.shelves', fixture: 'parts shelving', stand: pt3([sx1 + 0.42, (sy0 + sy1) / 2, g]), yaw: -90, show: true, seq: [{ clip: 'lift', once: true, opts: { workZ: 0.85 } }, { clip: 'place', once: true, opts: { workZ: 0.85 } }], fits: [['load', 0.85]], anim: { clip: 'lift', opts: { workZ: 0.85 } } });
    at('compressor', 0, 1); tube(out, [hx - 1.6, -hy + 1.0, g + 0.45], [hx - 0.4, -hy + 1.0, g + 0.45], 0.32, 10, 'sy.red', 0, null, true); box(out, hx - 1.4, hx - 0.7, -hy + 0.72, -hy + 1.28, g + 0.77, g + 1.2, 'sy.tool', 0, null, false);
    at('cart', 0, 1); box(out, hx - 1.6, hx - 0.9, 6.2, 7.0, g + 0.2, g + 0.95, 'sy.blue', 0, null, false); for (const d of [0, 0.22]) tube(out, [hx - 1.2 + d, 7.15, g + 0.2], [hx - 1.2 + d, 7.15, g + 1.45], 0.1, 8, d ? 'sy.green' : 'sy.red', 0, null, true);
    for (const [x, y, m] of [[hx - 0.7, -3.4, 'sy.blue'], [hx - 0.7, -2.75, 'sy.red'], [hx - 1.35, -3.1, 'sy.blue']]) drum(out, x, y, g, m);
    const cz = g + (a.craneZ || He - 1.3), cy = a.craneY != null ? a.craneY : 6.5, hxx = a.hookX != null ? a.hookX : -3.2;
    if (cut !== 'section') { at('crane', 0, 1);
      for (const sx of [-1, 1]) box(out, sx * (hx - 0.6) - 0.15, sx * (hx - 0.6) + 0.15, -hy + 0.3, hy - 0.3, cz - 0.45, cz, 'sy.yel', 0, null, false);
      box(out, -hx + 0.5, hx - 0.5, cy - 0.3, cy + 0.3, cz, cz + 0.65, 'sy.yel', 0, null, false); for (const sx of [-1, 1]) box(out, sx * (hx - 0.6) - 0.25, sx * (hx - 0.6) + 0.25, cy - 1.0, cy + 1.0, cz - 0.1, cz + 0.35, 'sy.yel', 0, null, false);
      box(out, hxx - 0.35, hxx + 0.35, cy - 0.4, cy + 0.4, cz - 0.6, cz, 'sy.steel', 0, null, false); tube(out, [hxx, cy, cz - 0.6], [hxx, cy, g + 3.4], 0.025, 4, 'sy.steel', 0, null, false);
      box(out, hxx - 0.12, hxx + 0.12, cy - 0.1, cy + 0.1, g + 3.1, g + 3.4, 'sy.yel', 0, null, false); tube(out, [hxx + 0.3, cy, cz - 0.6], [hxx + 0.3, cy, g + 1.2], 0.008, 3, 'sy.tool', 0, null, false);
      box(out, hxx + 0.24, hxx + 0.36, cy - 0.06, cy + 0.06, g + 0.95, g + 1.2, 'sy.yel', 0, null, false); }
    verb(s, { verb: 'hoistPendant', id: 'shed.crane', fixture: '5 t bridge crane pendant', stand: pt3([hxx + 0.3, cy - 0.45, g]), yaw: 0, seq: [{ clip: 'reach', loop: true, opts: { lift: 0.95 }, note: 'the pendant hangs to 1.05 m; up, down, east, west on its buttons' }], fits: [['rest', 0.95]] });
    rec(s, { k: 'shed', span: S, length: Ln, eave: r3(He), ridge: r3(Hr), floorZ: r3(g), door: { width: dw, height: dh, open: true, kind: 'roll-up', wall: 'north (+y)' }, personnelDoor: { wall: 'east', width: 1.0, height: 2.2 }, cutaway: cut, cutH, cutWalls: cuts, crane: { capacity_t: 5, railZ: r3(cz), hookMinZ: r3(g + 0.3), drawn: cut !== 'section' } });
    for (const Wl of walls) { const a0 = [Wl.o[0], Wl.o[1]], a1 = [Wl.o[0] + Wl.d[0] * Wl.L, Wl.o[1] + Wl.d[1] * Wl.L]; rec(s, { k: 'wall', id: Wl.id, a: a0.map(r3), b: a1.map(r3), gaps: Wl.id === 'north' ? [[r3(hx - dw / 2), r3(hx + dw / 2)]] : Wl.id === 'east' ? [[3.6, 4.6]] : [] }); }
    verb(s, { verb: 'walkIn', id: 'shed.door', fixture: 'roll-up door', stand: pt3([1.2, hy + 2.0, g]), yaw: 180, seq: [{ clip: 'walk', loop: true, note: 'through the roll-up door onto the shed floor, level with the apron' }], fits: [], show: true, anim: { clip: 'walk', move: { d: [0, -1, 0], len: 5.5, rate: 0.72 } } });
  };

  BUILD.staging = function (out, s, a) {
    const g = a.g, L = a.len || 6, w = a.w || 1.2, lev = a.levels || [2.0, 4.0], top = lev[lev.length - 1]; at('staging', 0, 1);
    const ys = []; for (let y = 0; y <= L + 1e-6; y += L / Math.max(1, Math.ceil(L / 2.5))) ys.push(y);
    for (const y of ys) for (const x of [0, w]) tube(out, [x, y, g], [x, y, g + top + 1.05], 0.024, 5, 'sy.galv', 0, null, false);
    for (const lz of lev) { for (const y of ys) tube(out, [0, y, g + lz - 0.06], [w, y, g + lz - 0.06], 0.02, 4, 'sy.galv', 0, null, false);
      for (const x of [0, w]) tube(out, [x, 0, g + lz - 0.06], [x, L, g + lz - 0.06], 0.02, 4, 'sy.galv', 0, null, false);
      box(out, 0.03, w - 0.03, 0, L, g + lz - 0.05, g + lz, 'sy.pine', 0, G.plankTex(0.23), false);
      for (const dz of [0.5, 1.0]) tube(out, [w, 0, g + lz + dz], [w, L, g + lz + dz], 0.02, 4, 'sy.galv', 0, null, false);
      box(out, w - 0.02, w, 0, L, g + lz, g + lz + 0.15, 'sy.pine', 0, null, false);
      rec(s, { k: 'walk', kind: 'staging lift', poly: [[0.05, 0], [w - 0.05, 0], [w - 0.05, L], [0.05, L]], z: r3(g + lz), surface: 'scaffold boards' });
      verb(s, { verb: 'workStaging', also: ['sandTopsides', 'paintTopsides'], id: (a.id || 'staging') + '.lift' + lev.indexOf(lz), fixture: 'staging lift at ' + r2(lz) + ' m', stand: pt3([0.35, L / 2, g + lz]), yaw: -90, show: lz === lev[0],
        seq: [{ clip: 'reach', loop: true, opts: { lift: 0.95 }, note: 'the topsides, a reach from the inner edge' }], fits: [['rest', 0.95]], anim: { clip: 'reach', opts: { lift: 0.95 } } }); }
    let rungs = []; const lx = w + 0.3; { const tmp = []; rungs = ladderAt(tmp, 0, 0, g, g + lev[0], 'sy.galv'); xform(tmp, HP, lx, 0.5, 0); for (const f of tmp) out.push(f); }
    climbVerbs(s, (a.id || 'staging') + '.ladder', 'staging ladder', (dx, dy) => [lx - dy, 0.5 + dx, 0], -90, g, g + lev[0], rungs, 'onto the first lift', false);
  };

  BUILD.trestles = function (out, s, a) {
    const g = a.g, L = a.len || 30, top = 0.80, n = Math.max(2, Math.round(L / 6) + 1); at('trestle', 0, 1);
    for (let i = 0; i < n; i++) { const x = -L / 2 + 0.6 + (L - 1.2) * i / (n - 1); box(out, x - 0.06, x + 0.06, -0.5, 0.5, g + top - 0.12, g + top, 'sy.block', 0, G.sawnTex(), false);
      for (const sy of [-1, 1]) for (const sx of [-1, 1]) bar(out, [x, sy * 0.42, g + top - 0.12], [x + sx * 0.35, sy * 0.55, g], 0.07, 0.07, 'sy.block'); }
    if (a.mast) { at('mast', 0, 1); box(out, -L / 2, L / 2, -0.17, 0.17, g + top, g + top + 0.22, 'sy.alum', 0, null, false);
      for (let i = 0; i < 3; i++) box(out, -L / 2 + 6 + i * 6, -L / 2 + 8.2 + i * 6 - i * 0.4, 0.22, 0.34, g + top, g + top + 0.06, 'sy.alum', 0, null, false); }
    rec(s, { k: 'fixture', id: 'trestles', what: 'mast trestles, tops at 0.80', fit: fitOf('bench', top) });
    for (const f of [-0.25, 0.25]) verb(s, { verb: 'mastWork', also: ['rewire', 'serviceSpreaders'], id: 'trestles.' + (f < 0 ? 'A' : 'B'), fixture: 'unstepped mast on trestles', stand: pt3([f * L, -0.62, g]), yaw: 0, show: f < 0,
      seq: [{ clip: 'bench', loop: true, opts: { workZ: 0.80 }, note: 'the mast lies at bench height' }], fits: [['bench', top]], anim: { clip: 'bench', opts: { workZ: 0.80 } } });
    rec(s, { k: 'block', kind: 'mast on trestles', poly: [[-L / 2, -0.6], [L / 2, -0.6], [L / 2, 0.6], [-L / 2, 0.6]], z0: g, z1: r3(g + 1.1) });
  };

  BUILD.fence = function (out, s, a) {
    const g = a.g, pts = a.pts, mat = a.mat || 'sy.green', gate = a.gate || null; let s0 = 0; at('fence', 0, 1);
    for (let i = 0; i + 1 < pts.length; i++) { const [x0, y0] = pts[i], [x1, y1] = pts[i + 1], L = Math.hypot(x1 - x0, y1 - y0), ux = (x1 - x0) / L, uy = (y1 - y0) / L, P = (t, z) => [x0 + ux * t, y0 + uy * t, g + z];
      const inGate = (t) => gate && s0 + t > gate[0] && s0 + t < gate[1];
      for (let t = 0; t <= L + 1e-6; t += L / Math.max(1, Math.ceil(L / 2.5))) if (!inGate(t) || Math.abs(s0 + t - gate[0]) < 0.01 || Math.abs(s0 + t - gate[1]) < 0.01) tube(out, P(t, 0), P(t, 2.1), 0.045, 6, mat, 0, null, true);
      const segs = []; let a0 = 0; if (gate && gate[1] > s0 && gate[0] < s0 + L) { segs.push([0, Math.max(0, gate[0] - s0)]); a0 = Math.min(L, gate[1] - s0); } segs.push([a0, L]);
      for (const [t0, t1] of segs) { if (t1 - t0 < 0.05) continue; for (const z of [0.3, 1.85]) bar(out, P(t0, z), P(t1, z), 0.04, 0.04, mat);
        for (let t = t0 + 0.09; t < t1; t += 0.18) { const c = P(t, 0); box(out, c[0] - 0.03 * Math.abs(ux) - 0.006 * Math.abs(uy), c[0] + 0.03 * Math.abs(ux) + 0.006 * Math.abs(uy), c[1] - 0.03 * Math.abs(uy) - 0.006 * Math.abs(ux), c[1] + 0.03 * Math.abs(uy) + 0.006 * Math.abs(ux), g + 0.05, g + 2.0, mat, 0, null, false); } }
      rec(s, { k: 'fenceRun', a: [x0, y0], b: [x1, y1], height: 2.0 }); s0 += L; }
    if (gate) rec(s, { k: 'gate', span: gate, width: r3(gate[1] - gate[0]), open: true });
  };

  BUILD.flood = function (out, s, a) {
    const g = a.g, h = a.h || 9, arm = 0.75; at('pole', 0, 1); tube(out, [0, 0, g], [0, 0, g + h], 0.1, 8, 'sy.galv', 0, null, true); box(out, -0.3, 0.3, -0.3, 0.3, g, g + 0.08, 'sy.conc', 0, null, false);
    bar(out, [-arm, 0, g + h - 0.2], [arm, 0, g + h - 0.2], 0.08, 0.08, 'sy.galv');
    for (const sx of [-1, 1]) { const x = sx * arm; at('flood', 0, 1); box(out, x - 0.25, x + 0.25, -0.18, 0.18, g + h - 0.62, g + h - 0.3, 'sy.steel', 0, null, false);
      quad(out, [x - 0.22, -0.15, g + h - 0.63], [x + 0.22, -0.15, g + h - 0.63], [x + 0.22, 0.15, g + h - 0.63], [x - 0.22, 0.15, g + h - 0.63], 'sy.lens', null, null, 0.02); }
    MG.lampAt(s, 0, 0, g + h - 0.7, a.r || 19, 1.0, '#fff4dc');
  };

  BUILD.washPad = function (out, s, a) {
    const g = a.g, hx = a.w / 2, hy = a.d / 2; at('ground', 0, 0); box(out, -hx, hx, -hy, hy, g - 0.05, g + 0.02, 'sy.conc', 0, TX.conc(2.2, 31), false);
    quad(out, [-hx + 0.4, -0.15, g + 0.024], [hx - 0.4, -0.15, g + 0.024], [hx - 0.4, 0.15, g + 0.024], [-hx + 0.4, 0.15, g + 0.024], 'sy.steel', [[-hx, 0], [hx, 0], [hx, 1], [-hx, 1]], TX.grate, 0.03);
    for (const [x0, x1, y0, y1] of [[-hx, hx, -hy, -hy + 0.15], [-hx, hx, hy - 0.15, hy], [-hx, -hx + 0.15, -hy, hy], [hx - 0.15, hx, -hy, hy]]) box(out, x0, x1, y0, y1, g + 0.02, g + 0.14, 'sy.conc', 0, null, false);
    const ux = a.unit ? a.unit[0] : -hx - 1.2, uy = a.unit ? a.unit[1] : 0; at('washer', 0, 1); box(out, ux - 0.35, ux + 0.35, uy - 0.55, uy + 0.55, g + 0.25, g + 1.1, 'sy.red', 0, null, false);
    for (const sy of [-1, 1]) wheelX(out, ux, uy + sy * 0.4, g + 0.22, 0.22, 0.12, 'sy.tyre', null);
    tube(out, [ux - 1.2, uy + 0.8, g], [ux - 1.2, uy + 0.8, g + 1.5], 0.62, 12, 'sy.green', 0, null, true);
    MG.sagLine(out, [ux + 0.3, uy + 0.5, g + 0.6], [ux + 2.4, uy + 1.8, g + 0.05], 0.1, 8, 'sy.tyre', 0.014);
    rec(s, { k: 'walk', kind: 'wash-down pad', poly: [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]], z: r3(g + 0.02), surface: 'bunded concrete, to the separator', drains: true });
    rec(s, { k: 'fixture', id: 'washer', what: 'pressure washer and oil–water separator', at: [r3(ux), r3(uy), r3(g)] });
  };

  BUILD.props = function (out, s, a) {
    const g = a.g;
    for (const it of a.items || []) { const x = it.x, y = it.y;
      if (it.kind === 'drums') for (let i = 0; i < (it.n || 3); i++) drum(out, x + (i % 2) * 0.62, y + Math.floor(i / 2) * 0.62, g, i & 1 ? 'sy.red' : 'sy.blue');
      else if (it.kind === 'blocking') { at('blocks', 0, 1); for (let k = 0; k < (it.n || 5); k++) for (let j = 0; j < 3; j++) (k & 1) ? box(out, x - 0.6, x + 0.6, y - 0.6 + j * 0.45, y - 0.36 + j * 0.45, g + k * 0.2, g + k * 0.2 + 0.2, 'sy.block', 0, G.sawnTex(), false) : box(out, x - 0.6 + j * 0.45, x - 0.36 + j * 0.45, y - 0.6, y + 0.6, g + k * 0.2, g + k * 0.2 + 0.2, 'sy.block', 0, G.sawnTex(), false); }
      else if (it.kind === 'stands') { at('stand', 0, 1); for (let i = 0; i < (it.n || 4); i++) standAt(out, x + i * 1.15, y, g, g + (it.h || 2.1), it.mat || 'sy.blue'); }
      else if (it.kind === 'cart') { at('cart', 0, 1); box(out, x - 0.3, x + 0.3, y - 0.5, y + 0.5, g + 0.25, g + 0.9, 'sy.red', 0, null, false); for (const sy of [-1, 1]) wheelX(out, x, y + sy * 0.38, g + 0.14, 0.14, 0.08, 'sy.tyre', null); }
      rec(s, { k: 'block', kind: it.kind, poly: [[x - 0.7, y - 0.7], [x + 0.7 + (it.kind === 'stands' ? (it.n || 4) * 1.15 : 0.6), y - 0.7], [x + 0.7 + (it.kind === 'stands' ? (it.n || 4) * 1.15 : 0.6), y + 0.7], [x - 0.7, y + 0.7]], z0: g, z1: g + 1.2 }); }
  };

  // ============================ THE FAMILY ==============================================================================
  G.FAMILIES.syPart = { label: 'shipyard part', note: 'one part of a yard (rack, hull on the hard, travel lift, forklift, shed, staging, trestles, fence, flood, wash pad, ground, props), built in the harbour’s G-buffer',
    kind: 'yard', faces: [], dims: { bays: [1, 1, 1], bayLen: [0.2, 400, 4], width: [0.2, 400, 4] },
    build(out, s) { s._sy = []; s._lamps = []; const B = BUILD[s.part]; if (B) B(out, s, Object.assign({ g: 3.0 }, s.args || {})); CTX.grp = 0; } };

  root.ShipyardGeo2 = { FITS, REQUESTS, PAL, TX, SHIPS, XT, BUILD, landHull, hullReady, hullXf, fitOf, bar, wheelX, standAt, ladderAt, cache: LC };
})(typeof globalThis !== 'undefined' ? globalThis : window);
