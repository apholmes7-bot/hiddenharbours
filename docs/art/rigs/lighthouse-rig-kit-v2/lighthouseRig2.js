/* Hidden Harbours — LIGHT STATION, pass 2 (lighthouseRig2.js → globalThis.LightStation2), 2026-09-27. Needs stationLight2.js.
   Pass 1 (Art/lighthouseIsoRig.js → LighthouseIso) is untouched: its seven families, presets, palettes and builder surface carry over.
   LIGHT. frame() rasterises a G-buffer through the fleet's camera (orthographic, 40°, 45° steps, 32 px = 1 m) and relight() lights it
   from any WeatherSky.at() sky with StationLight2 (the houses' law plus the wharves' water terms). No dither, no keyline.
   INSIDE. The shaft is a wall with a thickness and real openings: the door, windows set where the stair passes the show side, watch
   windows at the landing. An open-well spiral cantilevers from the wall up to the watch landing, a ladder rises through a hatch to the
   lantern floor, the lens turns on its pedestal and the clockwork weight hangs in the well. cutaway:'section' with climb (0–1 along
   route()) or standZ cuts the tower at the feet + sill (0.9 m) on the side toward the camera and at the feet + 1.95 m behind: cladding
   out, finish in, a pale cap on every cut wall, the door's jambs as stubs with a mat inside. The cut moves up a riser (a rung on the
   ladder) at a time, so it grows as the player climbs.
   CHARACTER v9.2. Risers ≤ 0.19, goings ≥ 0.26 on the walk line, 2.0 m over every tread and the landing, the door 1.0 × 2.05, rungs
   0.24 with the top-out board at 0.72, the lamp at a reach of 0.95, the log at a bench of 0.80, the oil at a load of 0.85. A shaft too
   narrow for the spiral stops it lower and carries on by ladder; a base too narrow to clear the door with the first turn is widened.
   NIGHT. The light is exhibited from dusk (or in fog) by its character; lamp(o, t) gives the turning panels, the beams and the flash
   toward the camera, paintLens() recolours the lens for that moment. The watch, the dwelling and the fog-signal house light by the
   keeper's schedule; lights() hands the game every lamp.
   API  frame(dir, o) · relight(fr, sky, o) · castShadow · shadeAt · contact · view · lamp · lensPixels · paintLens · resolve · route ·
        posAt · cutOf · anchors · project · placement · stations · checks · gameplay · lightsOn                                      */
(function (root) {
  'use strict';
  const L = root.StationLight2; if (!L) throw new Error('lighthouseRig2.js: load stationLight2.js first');
  const PX = 32, S = 32, DEG = Math.PI / 180, TAU = Math.PI * 2, CE = Math.cos(40 * DEG), SE = Math.sin(40 * DEG);
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v, r3 = (v) => Math.round(v * 1000) / 1000, r2 = (v) => Math.round(v * 100) / 100;
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r) => '#' + r.map(v => clamp(Math.round(v), 0, 255).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2r(a), B = h2r(b); return r2h([0, 1, 2].map(i => A[i] + (B[i] - A[i]) * t)); };
  const desat = (hex, t) => { const [r, g, b] = h2r(hex), l = 0.3 * r + 0.59 * g + 0.11 * b; return r2h([r + (l - r) * t, g + (l - g) * t, b + (l - b) * t]); };
  const wrapA = (a) => Math.atan2(Math.sin(a), Math.cos(a));
  const NAMES = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }

  // ---- palettes (pass 1's KTC ramps, dark → light) ------------------------------------------------------------------------
  const BODY = { white: ['#8c928c', '#a6aaa2', '#bfc2b9', '#d5d8cf', '#e7e9e0', '#f3f4ec'], cream: ['#8a6f3c', '#a6884b', '#c2a35f', '#d8bd7c', '#e9d59d', '#f5e7c1'],
    red: ['#4a130f', '#671b14', '#88271c', '#a33124', '#bd4230', '#d25a42'], galv: ['#464d51', '#5a6267', '#727c81', '#8c979c', '#a6b1b5', '#c2cccf'],
    sandstone: ['#5a2f24', '#6f3c2c', '#874e39', '#9e6248', '#b47a5d', '#c79778'], ironGrey: ['#2c3238', '#3a4249', '#4b555d', '#5f6a73', '#74808a', '#8b97a1'] };
  const MARK = { red: ['#4a130f', '#6b1d15', '#8d2a1e', '#a83526', '#c04433', '#d55c45'], black: ['#14161a', '#1c1f24', '#262a30', '#33383f', '#43494f', '#555c63'],
    green: ['#0f2c22', '#17402f', '#20553d', '#2b6c4c', '#37845d', '#489c70'], white: BODY.white };
  const TRIM = ['#9aa09a', '#b4b8b0', '#ccd0c7', '#e0e2da', '#eef0e8', '#f8f9f2'], CAPR = ['#b9bdb4', '#cdd0c7', '#dcdfd6', '#e8eae2', '#f2f3ec', '#fbfbf6'];
  const ROOFS = { grey: ['#23262b', '#2e333a', '#3c424a', '#4c535c', '#5d6570', '#6f7883'], metal: ['#424d52', '#556065', '#6c7c81', '#88999e', '#a4babe', '#c0d4d7'] };
  const STONE = ['#33343a', '#42444b', '#54575d', '#666a70', '#7a7e84', '#8e9299'], BRICK = ['#3a201a', '#552b20', '#6e3728', '#874634', '#a05743', '#b96b55'];
  const WOOD = ['#4f3a24', '#63492d', '#785a39', '#8f7049', '#a6875d', '#bd9f74'], STEEL = ['#2a2f33', '#3c454b', '#525c63', '#6d777e', '#889298', '#a2acb1'];
  const RUST = ['#3a1c10', '#4f2614', '#6a3620', '#84462b', '#9c5b3a', '#b2724c'], IRON = ['#1c2622', '#26332e', '#32423c', '#40534b', '#50655c', '#62786e'];
  const BRASS = ['#4a3514', '#6b4d1c', '#8f6a28', '#b58c3a', '#d4ad55', '#ecd08a'], LENS = ['#3c5552', '#557571', '#739791', '#98bab2', '#c3dcd4', '#e9f6f1'];
  const GLASS = ['#1f2f35', '#2b3c42', '#374c53', '#456068', '#54747c', '#6a8d94'], DOOR = ['#1a2c31', '#20343a', '#2c464d', '#3a5c64', '#4a747d', '#5c8f99'];
  const FINISH = { plaster: ['#8a8f88', '#a3a79f', '#bcc0b7', '#d2d5cc', '#e3e5dc', '#f0f1ea'], boards: ['#7c6d52', '#958567', '#ad9d7d', '#c5b595', '#d9cbad', '#e9dec6'], steel: ['#3f4a45', '#52605a', '#66756e', '#7b8b83', '#909f97', '#a6b4ab'] };
  const FLAGS = ['#4a4a48', '#5c5b57', '#6f6d67', '#838078', '#97938a', '#aba69c'], MATR = ['#3a2a1a', '#4d3a24', '#634c2f', '#7a5f3b', '#927349', '#aa8858'], OIL = ['#3d1410', '#5a1c15', '#7a271c', '#9a3324', '#b8452f', '#cf5c43'];
  const KEY = '#1a1c22';

  // ---- families and presets (pass 1's, with the light's character) --------------------------------------------------------
  const FAMILIES = {
    tapered: { label: 'Octagonal tapered', n: 8, clad: 'shingle', body: 'white', daymark: 'stripeRed', lantern: 'glazed', cap: 'cone', gallery: 'pipe', plinth: 'concrete', windows: 2, ladder: false, hT: [7.2, 6.4], rB: [1.42, 0.62], taper: 0.66, lanH: 1.85, note: 'Timber “pepper-shaker” — the harbour light' },
    conical: { label: 'Conical masonry', n: 24, clad: 'plaster', body: 'white', daymark: 'bandsRed', lantern: 'glazed', cap: 'dome', gallery: 'pipe', plinth: 'rock', windows: 3, ladder: false, hT: [11.5, 8.5], rB: [2.05, 1.05], taper: 0.60, lanH: 2.15, entasis: 0.82, corbel: true, note: 'Masonry coast tower, entasis profile' },
    pyramid: { label: 'Square pyramidal', n: 4, clad: 'clapboard', body: 'white', daymark: 'stripeRed', lantern: 'glazed', cap: 'cone', gallery: 'picket', plinth: 'concrete', windows: 1, ladder: false, hT: [5.4, 4.4], rB: [1.62, 0.72], taper: 0.66, lanH: 1.55, note: 'Timber range / harbour light' },
    pier: { label: 'Pier-head light', n: 8, clad: 'steelPlate', body: 'red', daymark: 'plain', lantern: 'drum', cap: 'flat', gallery: 'pipe', plinth: 'crib', windows: 1, ladder: true, hT: [3.3, 2.5], rB: [1.12, 0.48], taper: 0.86, lanH: 1.3, note: 'Breakwater light on a timber crib' },
    schoolhouse: { label: 'Integral dwelling', n: 4, clad: 'clapboard', body: 'white', daymark: 'plain', lantern: 'glazed', cap: 'cone', gallery: 'picket', plinth: 'concrete', windows: 1, ladder: false, hT: [6.6, 2.4], rB: [1.05, 0.28], taper: 0.80, lanH: 1.5, dwelling: true, note: 'Tower through the keeper’s roof' },
    skeleton: { label: 'Steel skeleton', n: 12, clad: 'corrugated', body: 'galv', daymark: 'plain', lantern: 'drum', cap: 'flat', gallery: 'lattice', plinth: 'concrete', windows: 0, ladder: true, hT: [9.5, 9.5], rB: [0.92, 0.30], taper: 1.0, lanH: 1.45, legs: true, note: 'Splayed lattice + stair barrel' },
    caisson: { label: 'Caisson spark-plug', n: 24, clad: 'steelPlate', body: 'white', daymark: 'redTop', lantern: 'glazed', cap: 'dome', gallery: 'pipe', plinth: 'caisson', windows: 2, ladder: true, hT: [5.2, 3.4], rB: [1.78, 0.48], taper: 0.90, lanH: 1.7, note: 'Offshore tower on a steel caisson' },
  };
  const PRESETS = {
    harbourLight: { family: 'tapered', size: 0.22, body: 'white', clad: 'shingle', daymark: 'stripeRed', weather: 0.42, attach: 'none', char: 'Fl W 6s' },
    coastTower: { family: 'conical', size: 0.82, body: 'white', clad: 'plaster', daymark: 'bandsRed', weather: 0.22, attach: 'station', plinth: 'rock', char: 'Fl(2) W 15s' },
    rangeLight: { family: 'pyramid', size: 0.35, body: 'white', clad: 'clapboard', daymark: 'stripeRed', weather: 0.3, attach: 'none', char: 'Oc R 4s' },
    pierHead: { family: 'pier', size: 0.4, body: 'red', clad: 'steelPlate', daymark: 'plain', weather: 0.55, attach: 'none', char: 'Fl R 4s' },
    keeperLight: { family: 'schoolhouse', size: 0.5, body: 'white', clad: 'clapboard', daymark: 'plain', weather: 0.28, attach: 'shed', char: 'Iso W 6s' },
    latticeTower: { family: 'skeleton', size: 0.55, body: 'galv', clad: 'corrugated', daymark: 'plain', weather: 0.4, attach: 'none', char: 'Fl W 10s' },
    sparkPlug: { family: 'caisson', size: 0.5, body: 'white', clad: 'steelPlate', daymark: 'redTop', weather: 0.5, attach: 'none', char: 'Fl(3) W 20s' },
  };
  const DAYMARKS = ['plain', 'stripeRed', 'bandsRed', 'redTop', 'checker', 'spiral'], CLADS = ['shingle', 'clapboard', 'plaster', 'stoneBlock', 'steelPlate', 'corrugated'];
  const LANTERNS = ['glazed', 'drum', 'open'], CAPS = ['cone', 'dome', 'flat'], GALLERIES = ['none', 'pipe', 'picket', 'lattice'], PLINTHS = ['none', 'concrete', 'rock', 'crib', 'caisson'];
  /* the light's character: a turning optic (rot: 2 bullseye sectors, grp flashes a group, a group every per s) or a lamp that
     occults / flashes behind a fixed lens */
  const CHARS = {
    'F W': { kind: 'fixed', col: 'W' }, 'F R': { kind: 'fixed', col: 'R' }, 'Fl W 6s': { kind: 'rot', col: 'W', per: 6, grp: 1 }, 'Fl W 10s': { kind: 'rot', col: 'W', per: 10, grp: 1 },
    'Fl(2) W 15s': { kind: 'rot', col: 'W', per: 15, grp: 2 }, 'Fl(3) W 20s': { kind: 'rot', col: 'W', per: 20, grp: 3 }, 'Iso W 6s': { kind: 'occ', col: 'W', per: 6, on: [[0, 3]] },
    'Oc R 4s': { kind: 'occ', col: 'R', per: 4, on: [[0, 3]] }, 'Fl R 4s': { kind: 'flash', col: 'R', per: 4, on: [[0, 0.5]] }, 'Fl G 4s': { kind: 'flash', col: 'G', per: 4, on: [[0, 0.5]] },
    'Q G': { kind: 'flash', col: 'G', per: 1, on: [[0, 0.3]] } };
  const COLRAMP = { W: 'lamp', R: 'red', G: 'green' }, BEAMC = { W: '#f6d684', R: '#ff8a70', G: '#8cf0a8' };
  // v9.2, every creator body (export/character-v9.2-import-kit/reports/worldfit.txt); ladders as WharfRig2 / WharfBuilding2
  const CHAR92 = { rig: 'characterIsoRig9.js', revision: '9.2', radiusMax: 0.229, footprintMax: [0.458, 0.370], fits: { bench: [0.643, 0.853], knife: [0.598, 0.760], load: [0.718, 0.978], rest: [0, 1.012], rail: [0.549, 0.724], rung: [0.211, 0.273], stepDown: [0.06, 1.30], stepUp: [0.549, 0.724], stepUpNoGrip: [0.06, 1.30] },
    rung: 0.24, standoff: 0.275, ladderRate: 0.436, topOut: 0.72, topAdvance: 0.60, walk: 0.727, clearDoor: 0.758, headroom: 2.0, spiral: { riserMax: 0.19, goingMin: 0.26, widthMin: 0.64, riseTurnMin: 2.08 } };
  const REQUESTS = [
    { clip: 'stairWalk', why: 'v9.2 walk plants on a level; the spiral is 0.17–0.19 m risers on a curving walk line', now: 'walk at 0.727 m/s along route(), root z linear tread to tread and yaw along the walk line: the feet float at most ± riser / 2 (≈ 2.3 px). Exact for every creator body instead: boardDown per riser (reversed going up)' },
    { clip: 'ladderOn / ladderOff', why: 'the watch ladder has a top-out through the hatch', now: 'up: board at railZ 0.72 from the top station, the root advanced 0.60 m onto the lantern floor. Down: blend 300 ms from idle at the hatch to ladderDown u 0' },
    { clip: 'crank', why: 'the clockwork is wound by a crank', now: 'haul at the crank (the stroke is in reach for every body)' },
    { clip: 'wipe', why: 'the keeper polishes the lantern glass and the lens', now: 'reach at 0.95 (rest 0–1.012), held' } ];
  const SCHEDULES = { keeper: { house: [[5.0, 7.5], [16.5, 23.0]], watch: true }, automated: { house: [], watch: false }, derelict: { house: [], watch: false, dark: true } };

  // ---- faces ---------------------------------------------------------------------------------------------------------------
  let CTX = { grp: 'tower', tag: '', ind: 0, obj: 0 };
  const withCtx = (o, fn) => { const p = CTX; CTX = Object.assign({}, CTX, o); try { fn(); } finally { CTX = p; } };
  function F(v, mat, b, o) { o = o || {}; return { v, mat, b: b || 0, db: o.db || 0, uv: o.uv || null, tex: o.tex || null, tag: o.tag != null ? o.tag : CTX.tag, em: o.em || null, emc: o.emc || null,
    occ: o.occ !== false, ao: o.ao != null ? o.ao : o.occ !== false, ind: o.ind != null ? !!o.ind : !!CTX.ind, grp: o.grp || CTX.grp, obj: o.obj != null ? !!o.obj : !!CTX.obj, glaze: o.glaze || null, fire: !!o.fire, cutDone: !!o.cutDone }; }
  const ringPt = (a, r, ox, oy) => [(ox || 0) + Math.cos(a) * r, (oy || 0) + Math.sin(a) * r];
  function boxS(out, x0, x1, y0, y1, z0, z1, mat, b, o) { b = b || 0;
    out.push(F([[x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1]], mat, b, o), F([[x1, y1, z0], [x0, y1, z0], [x0, y1, z1], [x1, y1, z1]], mat, b, o),
      F([[x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1]], mat, b, o), F([[x0, y1, z0], [x0, y0, z0], [x0, y0, z1], [x0, y1, z1]], mat, b, o), F([[x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]], mat, b + 1, o)); }
  function bar3(out, p0, p1, r0, mat, b, o) { const r = Math.max(0.03, r0), d = [p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]], Ln = Math.hypot(d[0], d[1], d[2]) || 1, u = d.map(c => c / Ln), t = Math.abs(u[2]) < 0.88 ? [0, 0, 1] : [1, 0, 0];
    let n1 = [u[1] * t[2] - u[2] * t[1], u[2] * t[0] - u[0] * t[2], u[0] * t[1] - u[1] * t[0]]; const m = Math.hypot(n1[0], n1[1], n1[2]) || 1; n1 = n1.map(c => c / m); const n2 = [u[1] * n1[2] - u[2] * n1[1], u[2] * n1[0] - u[0] * n1[2], u[0] * n1[1] - u[1] * n1[0]];
    const cor = (p, a, c) => [p[0] + (n1[0] * a + n2[0] * c) * r, p[1] + (n1[1] * a + n2[1] * c) * r, p[2] + (n1[2] * a + n2[2] * c) * r], sg = [[-1, -1], [1, -1], [1, 1], [-1, 1]], A = sg.map(s => cor(p0, s[0], s[1])), B = sg.map(s => cor(p1, s[0], s[1])), oo = Object.assign({ ao: false }, o);
    for (let i = 0; i < 4; i++) out.push(F([A[i], A[(i + 1) % 4], B[(i + 1) % 4], B[i]], mat, b || 0, oo)); out.push(F([B[0], B[1], B[2], B[3]], mat, (b || 0), oo)); }
  function frustum(out, z0, z1, r0, r1, n, rot, matOf, o) { o = o || {}; const ox = o.ox || 0, oy = o.oy || 0;
    for (let i = 0; i < n; i++) { const a0 = rot + TAU * i / n, a1 = rot + TAU * (i + 1) / n, p00 = ringPt(a0, r0, ox, oy), p10 = ringPt(a1, r0, ox, oy), p01 = ringPt(a0, r1, ox, oy), p11 = ringPt(a1, r1, ox, oy), mat = typeof matOf === 'function' ? matOf(i, (a0 + a1) / 2) : matOf;
      out.push(F([[p00[0], p00[1], z0], [p10[0], p10[1], z0], [p11[0], p11[1], z1], [p01[0], p01[1], z1]], mat, o.b || 0, o)); } }
  function polyTop(out, z, r, n, rot, mat, b, o) { o = o || {}; const pts = []; for (let i = 0; i < n; i++) { const p = ringPt(rot + TAU * i / n, r, o.ox, o.oy); pts.push([p[0], p[1], z]); } out.push(F(pts, mat, b || 0, o)); }
  function coneTop(out, z0, z1, r, n, rot, mat, b, o) { o = o || {}; for (let i = 0; i < n; i++) { const p0 = ringPt(rot + TAU * i / n, r, o.ox, o.oy), p1 = ringPt(rot + TAU * (i + 1) / n, r, o.ox, o.oy); out.push(F([[p0[0], p0[1], z0], [p1[0], p1[1], z0], [o.ox || 0, o.oy || 0, z1]], mat, b || 0, o)); } }
  function domeTop(out, z0, hgt, r, n0, rot, mat, o) { const K = 4, cut = 0.8, n = Math.min(n0, 16);
    for (let k = 0; k < K; k++) { const t0 = cut * k / K, t1 = cut * (k + 1) / K; frustum(out, z0 + hgt * Math.sin(t0 * Math.PI / 2), z0 + hgt * Math.sin(t1 * Math.PI / 2), r * Math.cos(t0 * Math.PI / 2), r * Math.cos(t1 * Math.PI / 2), n, rot, mat, o); }
    coneTop(out, z0 + hgt * Math.sin(cut * Math.PI / 2), z0 + hgt, r * Math.cos(cut * Math.PI / 2), n, rot, mat, 0, o); }
  function hoop(out, z, r, n, rot, th, mat, b, o) { o = o || {}; for (let i = 0; i < n; i++) { const p0 = ringPt(rot + TAU * i / n, r, o.ox, o.oy), p1 = ringPt(rot + TAU * (i + 1) / n, r, o.ox, o.oy); bar3(out, [p0[0], p0[1], z], [p1[0], p1[1], z], th, mat, b || 0, o); } }
  /* a flat annular sector at z (a tread, a landing, a floor ring), cut into ≤ 11° pieces */
  function ann(out, z, a0, a1, r0, r1, mat, b, o) { const k = Math.max(1, Math.ceil((a1 - a0) / (TAU / 32)));
    for (let i = 0; i < k; i++) { const p = a0 + (a1 - a0) * i / k, q = a0 + (a1 - a0) * (i + 1) / k, A = ringPt(p, r0), B = ringPt(q, r0), C = ringPt(q, r1), D = ringPt(p, r1);
      const uv = o && o.tex ? [[A[0], A[1]], [B[0], B[1]], [C[0], C[1]], [D[0], D[1]]] : null; out.push(F(r0 < 1e-3 ? [[0, 0, z], [C[0], C[1], z], [D[0], D[1], z]] : [[A[0], A[1], z], [B[0], B[1], z], [C[0], C[1], z], [D[0], D[1], z]], mat, b || 0, Object.assign({}, o, { uv: uv && r0 < 1e-3 ? [[0, 0], uv[2], uv[3]] : uv }))); } }
  /* a disc at z with an annular-sector hole (the hatch) */
  function disc(out, z, R, mat, b, o, hole) { const n = 32;
    for (let i = 0; i < n; i++) { const a0 = TAU * i / n, a1 = TAU * (i + 1) / n;
      if (hole) { const lo = Math.max(a0, wrapTo(hole.a0, a0)), hi = Math.min(a1, wrapTo(hole.a0, a0) + (hole.a1 - hole.a0)); if (hi > lo + 1e-4) {
        if (lo > a0) ann(out, z, a0, lo, 0, R, mat, b, o); if (hi < a1) ann(out, z, hi, a1, 0, R, mat, b, o); ann(out, z, lo, hi, 0, hole.r0, mat, b, o); if (hole.r1 < R) ann(out, z, lo, hi, hole.r1, R, mat, b, o); continue; } }
      ann(out, z, a0, a1, 0, R, mat, b, o); } }
  const wrapTo = (a, ref) => { let v = a; while (v < ref - Math.PI) v += TAU; while (v > ref + Math.PI) v -= TAU; return v; };

  // ---- cladding and floor textures (the houses' scales: 0.18 m shingle courses, 0.20 m lap) -------------------------------
  function tex(kind) {
    if (kind === 'clapboard') { const P = 0.20; return (u, v) => { const f = ((v % P) + P) % P; return f < 0.045 ? -2 : f > P - 0.035 ? 1 : 0; }; }
    if (kind === 'shingle') { const CO = 0.18, SW = 0.15; return (u, v) => { const row = Math.floor(v / CO), f = ((v % CO) + CO) % CO, su = (((u + (row & 1) * 0.5 * SW) % SW) + SW) % SW; return f < 0.035 ? -2 : su < 0.025 ? -1 : f > CO - 0.035 ? 1 : 0; }; }
    if (kind === 'stoneBlock') { const CO = 0.40, BW = 0.82; return (u, v) => { const row = Math.floor(v / CO), f = ((v % CO) + CO) % CO, off = (row & 1) * 0.5 * BW, su = (((u + off) % BW) + BW) % BW;
      if (f < 0.045 || su < 0.045) return -2; if (f > CO - 0.06) return 1; return ((row * 7 + Math.floor((u + off) / BW) * 13) % 5 === 0) ? -1 : 0; }; }
    if (kind === 'steelPlate') { const PH = 1.2, PW = 1.8; return (u, v) => { const f = ((v % PH) + PH) % PH, su = ((u % PW) + PW) % PW; return f < 0.04 ? -2 : su < 0.04 ? -1 : 0; }; }
    if (kind === 'corrugated') { const P = 0.125; return (u) => { const f = (((u % P) + P) % P) / P; return f < 0.2 ? -1 : f < 0.55 ? 0 : f < 0.8 ? 1 : 0; }; }
    if (kind === 'plaster') return (u, v) => { const h = Math.sin(u * 11.3 + v * 3.1) + Math.sin(u * 4.7 - v * 7.9); return h > 1.6 ? 1 : h < -1.65 ? -1 : 0; };
    if (kind === 'boards') { const P = 0.15; return (u) => (((u % P) + P) % P) < 0.02 ? -1 : 0; }
    if (kind === 'flags') return (u, v) => { const fu = ((u % 0.55) + 0.55) % 0.55, fv = (((v + (Math.floor(u / 0.55) & 1) * 0.2) % 0.45) + 0.45) % 0.45; return fu < 0.03 || fv < 0.03 ? -2 : 0; };
    if (kind === 'planks') { const P = 0.16; return (u, v) => (((v % P) + P) % P) < 0.02 ? -1 : 0; }
    if (kind === 'chequer') return (u, v) => ((Math.floor(u / 0.1) + Math.floor(v / 0.1)) & 1) ? 1 : 0;
    return null;
  }

  // ---- the builder surface → a resolved station ---------------------------------------------------------------------------
  function expand(o) { o = o || {}; const p = o.preset && PRESETS[o.preset]; if (!p) return o; const f = FAMILIES[p.family] || {}, A = p.attach || 'none', has = (k) => A === k || A === 'station';
    const q = Object.assign({ family: p.family, clad: f.clad, body: f.body, daymark: f.daymark, lantern: f.lantern, cap: f.cap, gallery: f.gallery, plinth: f.plinth, windows: f.windows, ladder: !!f.ladder,
      dwelling: has('dwelling'), shed: has('shed'), oilHouse: has('oilHouse') }, p); delete q.attach; return Object.assign(q, o); }
  const RES = new Map();
  function resolve(o0) {
    const o = expand(o0), key = geoKey(o); if (RES.has(key)) return RES.get(key);
    const famKey = FAMILIES[o.family] ? o.family : 'tapered', f = FAMILIES[famKey], g = (k, d) => o[k] != null ? o[k] : f[k] != null ? f[k] : d, size = clamp(o.size != null ? +o.size : 0.4, 0, 1);
    const b = { family: famKey, fam: f, size, n: f.n, rot: Math.PI / f.n, clad: g('clad', 'shingle'), body: o.body || f.body || 'white', mark: o.mark || 'red', daymark: g('daymark', 'plain'), lantern: g('lantern', 'glazed'),
      cap: g('cap', 'cone'), gallery: g('gallery', 'pipe'), plinth: g('plinth', 'concrete'), windows: clamp(g('windows', 2) | 0, 0, 4), ladder: !!g('ladder', false), weather: o.weather != null ? +o.weather : 0.25,
      dwelling: !!o.dwelling, shed: !!o.shed, oilHouse: !!o.oilHouse, char: CHARS[o.char] ? o.char : 'Fl W 6s', doorOpen: clamp(+o.doorOpen || 0, 0, 1), wound: o.wound != null ? clamp(+o.wound, 0, 1) : 0.6, oy: 0, grewB: 0 };
    b.hT = f.hT[0] + size * f.hT[1]; b.rB = f.rB[0] + size * f.rB[1]; b.rT = b.rB * f.taper;
    b.zBase = b.plinth === 'none' ? 0 : b.plinth === 'concrete' ? 0.34 : b.plinth === 'rock' ? 0.95 : b.plinth === 'crib' ? 1.25 : 2.35 + size * 1.15;
    if (f.legs) { b.rB = b.rT = Math.max(1.2, 1.0 + 0.3 * size); }                                 // the stair barrel is the shaft, wide enough for the spiral
    if (f.dwelling) { b.dwH = 3.5 + size * 0.7; b.dwWd = 6.2 + size * 1.6; b.dwLn = 7.4 + size * 2.2; b.zBase = 0.5; b.oy = 1.5 - b.dwLn / 2 + 0.15; b.rB = Math.max(b.rB, 1.82); b.rT = Math.max(b.rT, 1.74); }
    const mason = b.clad === 'plaster' || b.clad === 'stoneBlock';
    b.T = (z) => mason ? 0.60 - 0.22 * clamp((z - b.zBase) / b.hT, 0, 1) : b.clad === 'corrugated' ? 0.07 : b.clad === 'steelPlate' ? 0.12 : 0.16;
    const ent = f.entasis || 1; b.rAt = (z) => { const t = clamp((z - b.zBase) / (b.hT || 1), 0, 1); return b.rB + (b.rT - b.rB) * Math.pow(t, ent); };
    b.ap = (z) => b.rAt(z) * Math.cos(Math.PI / b.n); b.rIn = (z) => b.ap(z) - b.T(z);
    b.zTop = b.zBase + b.hT;
    if (f.dwelling) { const ridge = 0.5 + b.dwH + (b.dwWd / 2) * 0.95; b.zTop = Math.max(b.zTop, ridge + 2.0); b.hT = b.zTop - b.zBase; }
    b.zDeck = b.zTop + 0.34; b.lanN = b.n <= 8 ? b.n : f.legs ? 8 : 10;
    b.rLan = Math.max(b.lanN === 4 ? 0.62 : b.lanN === 8 ? 1.1 : 1.36, b.rT * 0.9);                     // the gallery door (≥ 0.76 clear) fits one facet
    b.rGal = Math.max(b.rT + (f.corbel ? 0.72 : 0.52), b.rLan + 0.7);
    b.lanH = Math.max(f.lanH + 0.6, 2.3); b.parH = b.rLan < 1.3 ? 0.6 : 0.85; b.zLanTop = b.zDeck + b.lanH; b.glzTop = b.zLanTop - 0.2; b.rCap = b.rLan + 0.17;
    b.capH = b.cap === 'dome' ? b.rCap * 0.62 : b.cap === 'flat' ? 0.22 : b.rCap * 1.22; b.zApex = b.zLanTop + 0.16 + b.capH; b.railH = 1.02;
    const rl = clamp(0.3 * b.rLan + 0.05, 0.24, 0.8), step = b.rLan >= 1.6, hl = clamp(rl * 1.5, 0.45, 1.3), focal = b.zDeck + (step ? 0.3 : 0) + 0.95;
    b.lens = { rl, hl, step, focal, z0: focal - hl / 2, z1: focal + hl / 2, standZ: b.zDeck + (step ? 0.3 : 0) }; b.focal = focal;
    b.zC = Math.min(b.zBase + 0.85, b.zBase + b.hT * 0.16);
    const dm = b.daymark; b.NB = dm === 'bandsRed' ? 5 : dm === 'redTop' ? 2 : dm === 'checker' ? 4 : dm === 'spiral' ? 8 : 1;
    const COLS = b.n <= 4 ? 4 : 8, colOf = (a) => { let v = (a - b.rot) % TAU; if (v < 0) v += TAU; return Math.floor(v / TAU * COLS) % COLS; };
    b.markAt = (k, a) => dm === 'bandsRed' ? (k % 2 === 1 ? 'mark' : 'body') : dm === 'redTop' ? (k === 1 ? 'mark' : 'body') : dm === 'checker' ? (((colOf(a) + k) % 2 === 0) ? 'mark' : 'body')
      : dm === 'spiral' ? ((((colOf(a) - k) % COLS + COLS) % COLS) < COLS / 2 ? 'mark' : 'body') : 'body';
    b.nSteps = (b.plinth === 'concrete' || b.plinth === 'rock' || f.dwelling) && b.zBase > 0.06 ? Math.ceil(b.zBase / 0.18 - 1e-9) : 0;
    for (let it = 0; it < 30; it++) { b.stair = stairOf(b); if (b.stair.kind !== 'helix' || b.stair.doorClear >= 0) break; b.rB += 0.05; b.grewB = r2(b.grewB + 0.05); }
    b.ops = shaftOps(b);
    RES.set(key, b); if (RES.size > 40) RES.delete(RES.keys().next().value); return b;
  }

  /* THE SPIRAL. Treads cantilever from the wall, w wide, open to a well; each takes g on the walk line (the tread's middle), so the
     stair turns faster as the shaft narrows and every turn rises at least 2.08 m. It climbs to the watch landing, 2.3 m under the
     lantern floor, or lower where the walk line would drop under 0.62 m; a ladder goes on from there. */
  function stairOf(b) {
    const zb = b.zBase, w = clamp(0.64 + 0.2 * b.size, 0.64, 0.86), aD = -Math.PI / 2, dw = 1.0, dh = 2.05, rIn = b.rIn;
    // every turn clears 2.08 m, and so does the landing's 1.3 m of walk line over the last turn: rise a turn ≥ 2.08 / (1 − landing / turn)
    const need = (rw) => 2.13 / (1 - Math.min(0.8, (1.3 / rw) / TAU)), okAt = (z, rs) => { const rw = rIn(z) - w / 2; return rw >= 0.5 && rIn(z) - w >= 0.18 && rs * TAU * rw / 0.26 >= need(rw); };
    let zL = b.zTop - 2.3, n = 1, riser = 0.19; for (;;) { n = Math.max(1, Math.ceil((zL - zb) / 0.19 - 1e-9)); riser = (zL - zb) / n; if (zL <= zb + 1.2 || okAt(zL, riser)) break; zL -= 0.05; }
    const lad = (z0, a) => { const rr = Math.max(0.5, Math.min(rIn(b.zTop) - 0.12, b.rLan - 0.2)), rungs = [], r0 = Math.max(0.16, rr - 0.72), r1 = rr + 0.1, ha = 0.45 / ((r0 + r1) / 2); for (let z = z0 + CHAR92.rung; z < b.zDeck - 0.05; z += CHAR92.rung) rungs.push(r3(z));
      return { a, r: rr, z0, z1: b.zDeck, rungs, hatch: { a0: a - ha, a1: a + ha, r0, r1 } }; };
    if (zL - zb < 1.2) return { kind: 'ladder', zL: zb, w: 0, n: 0, riser: 0, going: 0, treads: [], doorClear: 1, headroomMin: null, ladder: lad(zb, Math.PI / 2) };
    const rw = (z) => rIn(z) - w / 2, g = clamp(riser * TAU * rw(zL) / need(rw(zL)), 0.26, 0.32);
    const hd = (dw / 2 + 0.16) / rIn(zb), a0 = aD + hd + 0.04, T = []; let a = a0;
    for (let k = 1; k <= n; k++) { const z = zb + k * riser, da = g / rw(z); T.push({ k, z, a0: a, a1: a + da, r0: rIn(z) - w, r1: rIn(z) }); a += da; }
    const dL = 1.3 / rw(zL), landing = { a0: a, a1: a + dL, r0: 0, r1: rIn(zL) }, ladder = lad(zL, a + dL - 0.4 / rw(zL));
    landing.r0 = Math.max(0.14, Math.min(rIn(zL) - w, ladder.r - 0.62));
    // headroom: over each tread, the first tread (or the landing) a turn on
    let hmin = 99; const over = T.concat([{ z: zL, a0: landing.a0, a1: landing.a1 }]);
    for (const t of T) { const am = (t.a0 + t.a1) / 2 + TAU; const up = over.find(q => q.a0 <= am && q.a1 >= am); if (up) hmin = Math.min(hmin, up.z - 0.05 - t.z); }
    // the door: the first turn must pass over it clear of the head
    const up = T.find(q => q.a1 >= aD - hd + TAU && q.a0 <= aD + hd + TAU), doorClear = up ? r3(up.z - 0.30 - (zb + dh + 0.1)) : 1;
    return { kind: 'helix', w, n, riser, going: g, treads: T, a0, aEnd: a, zL, landing, ladder, headroomMin: r3(hmin), doorClear, turns: r2((a - a0) / TAU), rptMin: r3(riser * TAU * rw(zL) / g), pitchDeg: r2(Math.atan(riser / g) / DEG) };
  }
  /* the shaft's openings: the door, windows where the stair passes the show side (sill 0.95 over the tread), watch windows */
  function shaftOps(b) {
    const L = [{ a: -Math.PI / 2, w: 1.0, z0: b.zBase, z1: b.zBase + 2.05, kind: 'door', room: 'tower', id: 'door.main' }], st = b.stair, snap = (a) => b.n <= 8 ? Math.round((a - b.rot - Math.PI / b.n) / (TAU / b.n)) * (TAU / b.n) + b.rot + Math.PI / b.n : a;
    if (st.kind === 'helix' && b.windows > 0) { const cand = []; for (let j = 0; j < 12; j++) { const aim = -Math.PI / 2 + (j % 2 ? 0.8 : -0.8); let best = null, bd = 9;
        for (const t of st.treads) { const am = (t.a0 + t.a1) / 2; if (am < st.a0 + j * TAU || am >= st.a0 + (j + 1) * TAU) continue; const d = Math.abs(wrapA(am - aim)); if (d < bd) { bd = d; best = t; } }
        if (best && bd < 0.6 && best.z > b.zBase + 1.7 && best.z + 0.95 + 0.92 < st.zL + 0.2) cand.push({ a: snap(-Math.PI / 2 + (j % 2 ? 0.8 : -0.8)), z0: best.z + 0.95 }); }
      const pick = []; for (let i = 0; i < b.windows && cand.length; i++) pick.push(cand[Math.min(cand.length - 1, Math.round((i + 0.5) * cand.length / b.windows - 0.5))]);
      for (const c of pick) if (!L.some(q => q.kind === 'window' && Math.abs(q.z0 - c.z0) < 0.5)) L.push({ a: c.a, w: 0.62, z0: c.z0, z1: c.z0 + 0.92, kind: 'window', room: 'tower' }); }
    if (st.kind === 'ladder' && b.windows > 0 && b.hT > 2.8) L.push({ a: snap(-Math.PI / 2 + 0.8), w: 0.56, z0: b.zBase + 1.25, z1: b.zBase + 2.05, kind: 'window', room: 'tower' });
    if (st.kind === 'helix') { const z0 = st.zL + 0.95; if (z0 + 0.9 < b.zTop - 0.25) { L.push({ a: snap(-Math.PI / 2 + 0.8), w: 0.62, z0, z1: z0 + 0.9, kind: 'window', room: 'watch' });
        const lm = (st.landing.a0 + st.landing.a1) / 2; if (Math.abs(wrapA(lm - (-Math.PI / 2 + 0.8))) > 1.2 && Math.abs(wrapA(lm - st.ladder.a)) > 0.5) L.push({ a: snap(lm - 0.3), w: 0.62, z0, z1: z0 + 0.9, kind: 'window', room: 'watch' }); } }
    return L;
  }
  /* map openings (angle a, width w) onto facet am of an n-gon at apothem ap */
  function facetOps(ops, am, ap, hwAt, n) { const res = [];
    for (const o of ops) { const da = wrapA(o.a - am); if (Math.abs(da) > Math.PI / n + o.w / (2 * ap) + 0.05) continue; const sc = ap * Math.tan(clamp(da, -1.3, 1.3)), s0 = sc - o.w / 2, s1 = sc + o.w / 2, hw = hwAt(o.z0) - 0.03;
      const q0 = Math.max(s0, -hw), q1 = Math.min(s1, hw); if (q1 - q0 < 0.05) continue; res.push(Object.assign({}, o, { s0: q0, s1: q1, jl: q0 === s0, jr: q1 === s1 })); }
    return res; }

  /* A WALL FACET: outer and inner shells, openings with reveals, paint regions, a cap where the cut takes it. W = { C(z) outer-face
     centre, t tangent, n outward, hw(z) / hwi(z) outer / inner half width, T(z) thickness, z0, z1, cut, ops [{s0,s1,z0,z1,jl,jr}], zB, sB,
     paint(s, z) → mat, texOf(mat), texIn, inMat, capMat, revMat, u0 } */
  function facet(out, W) {
    const zb = W.z0, zt = W.cut != null ? Math.min(W.z1, W.cut) : W.z1; if (zt <= zb + 1e-3) return null;
    const ops = (W.ops || []).filter(o => o.z0 < zt - 1e-3).map(o => Object.assign({}, o, { zc: Math.min(o.z1, zt) }));
    const zs = [zb, zt], add = (z) => { if (z > zb + 1e-3 && z < zt - 1e-3) zs.push(z); }; ops.forEach(o => { add(o.z0); add(o.zc); }); (W.zB || []).forEach(add);
    zs.sort((a, b) => a - b); const Z = zs.filter((z, i) => !i || z - zs[i - 1] > 1e-3);
    const Po = (s, z) => { const c = W.C(z); return [c[0] + W.t[0] * s, c[1] + W.t[1] * s, z]; }, Pi = (s, z) => { const c = W.C(z), T = W.T(z); return [c[0] - W.n[0] * T + W.t[0] * s, c[1] - W.n[1] * T + W.t[1] * s, z]; };
    const E = (s, z, inr) => s === -Infinity ? -(inr ? W.hwi(z) : W.hw(z)) : s === Infinity ? (inr ? W.hwi(z) : W.hw(z)) : s, CD = { cutDone: true };
    let top = null;
    for (let k = 0; k + 1 < Z.length; k++) { const za = Z[k], zc = Z[k + 1], zm = (za + zc) / 2; let segs = [[-Infinity, Infinity]];
      for (const o of ops) if (o.z0 <= zm && o.zc >= zm) { const nx = []; for (const [a, b] of segs) { if (o.s1 <= a || o.s0 >= b) { nx.push([a, b]); continue; } if (o.s0 > a) nx.push([a, o.s0]); if (o.s1 < b) nx.push([o.s1, b]); } segs = nx; }
      top = segs;
      for (const [s0, s1] of segs) { const cuts = [s0]; for (const q of (W.sB || [])) if (q > s0 && q < s1) cuts.push(q); cuts.push(s1);
        for (let j = 0; j + 1 < cuts.length; j++) { const a = cuts[j], c = cuts[j + 1], mat = W.paint ? W.paint((E(a, zm) + E(c, zm)) / 2, zm) : W.mat, tx = W.texOf ? W.texOf(mat) : null;
          const pa0 = E(a, za), pb0 = E(c, za), pb1 = E(c, zc), pa1 = E(a, zc), qa0 = E(a, za, 1), qb0 = E(c, za, 1), qb1 = E(c, zc, 1), qa1 = E(a, zc, 1);
          out.push(F([Po(pa0, za), Po(pb0, za), Po(pb1, zc), Po(pa1, zc)], mat, 0, Object.assign({ uv: tx ? [[W.u0 + pa0, za], [W.u0 + pb0, za], [W.u0 + pb1, zc], [W.u0 + pa1, zc]] : null, tex: tx, tag: 'wall.out' }, CD)));
          out.push(F([Pi(qa0, za), Pi(qb0, za), Pi(qb1, zc), Pi(qa1, zc)], W.inMat, 0, Object.assign({ uv: W.texIn ? [[W.u0 + qa0, za], [W.u0 + qb0, za], [W.u0 + qb1, zc], [W.u0 + qa1, zc]] : null, tex: W.texIn, ind: 1, tag: 'wall.in' }, CD))); } } }
    if (W.cut != null && W.cut < W.z1 - 1e-3 && top) for (const [a, c] of top) out.push(F([Po(E(a, zt), zt), Po(E(c, zt), zt), Pi(E(c, zt, 1), zt), Pi(E(a, zt, 1), zt)], W.capMat, 1, Object.assign({ tag: 'cut.cap' }, CD)));
    for (const o of ops) { const z0 = Math.max(o.z0, zb), z1 = o.zc, jamb = (s) => out.push(F([Po(s, z0), Pi(s, z0), Pi(s, z1), Po(s, z1)], W.revMat, 0, Object.assign({ tag: 'reveal' }, CD)));
      if (o.jl !== false) jamb(o.s0); if (o.jr !== false) jamb(o.s1);
      if (o.z0 > zb + 1e-3) out.push(F([Po(o.s0, o.z0), Po(o.s1, o.z0), Pi(o.s1, o.z0), Pi(o.s0, o.z0)], W.revMat, 1, Object.assign({ tag: 'reveal' }, CD)));
      if (o.z1 <= zt - 1e-3) out.push(F([Po(o.s0, o.z1), Po(o.s1, o.z1), Pi(o.s1, o.z1), Pi(o.s0, o.z1)], W.revMat, -1, Object.assign({ tag: 'reveal' }, CD))); }
    return { zt, ops };
  }
  /* glass, casings, sills, the door leaf, the threshold; where the cut takes the door head, the leaf goes and a mat lies inside */
  function dress(out, W, R, b) {
    if (!R) return; const zt = R.zt, CD = { cutDone: true };
    const Pm = (s, z, d) => { const c = W.C(z); return [c[0] - W.n[0] * d + W.t[0] * s, c[1] - W.n[1] * d + W.t[1] * s, z]; };
    const proud = (s0, s1, z0, z1, d, mat, bb, tag) => { z1 = Math.min(z1, zt); if (z1 <= z0 + 1e-3) return; out.push(F([Pm(s0, z0, -d), Pm(s1, z0, -d), Pm(s1, z1, -d), Pm(s0, z1, -d)], mat, bb, Object.assign({ tag, db: 0.02, ao: false }, CD))); };
    const plate = (s0, s1, z, d0, d1, mat, bb, tag, o) => out.push(F([Pm(s0, z, d0), Pm(s1, z, d0), Pm(s1, z, d1), Pm(s0, z, d1)], mat, bb, Object.assign({ tag }, CD, o)));
    for (const o of R.ops) { const T = W.T(o.z0), casing = W.casing, zc = Math.min(o.z1, zt);
      if (o.kind === 'window') {
        if (zc > o.z0 + 0.02) { const dg = T * 0.4, mid = (o.s0 + o.s1) / 2, zm = (o.z0 + o.z1) / 2;
          out.push(F([Pm(o.s0, o.z0, dg), Pm(o.s1, o.z0, dg), Pm(o.s1, zc, dg), Pm(o.s0, zc, dg)], 'glass', 0, Object.assign({ em: 'win:' + o.room, occ: false, tag: 'glass' }, CD)));
          if (o.jl !== false && o.jr !== false) out.push(F([Pm(mid - .03, o.z0, dg - .01), Pm(mid + .03, o.z0, dg - .01), Pm(mid + .03, zc, dg - .01), Pm(mid - .03, zc, dg - .01)], W.revMat, 1, Object.assign({ occ: false, ao: false, tag: 'muntin' }, CD)));
          if (zm < zc) out.push(F([Pm(o.s0, zm - .03, dg - .01), Pm(o.s1, zm - .03, dg - .01), Pm(o.s1, zm + .03, dg - .01), Pm(o.s0, zm + .03, dg - .01)], W.revMat, 1, Object.assign({ occ: false, ao: false, tag: 'muntin' }, CD))); }
        if (casing) { if (o.jl !== false) proud(o.s0 - .09, o.s0, o.z0, o.z1 + .09, .025, 'trim', 1, 'casing'); if (o.jr !== false) proud(o.s1, o.s1 + .09, o.z0, o.z1 + .09, .025, 'trim', 1, 'casing');
          proud(o.s0 - (o.jl !== false ? .09 : 0), o.s1 + (o.jr !== false ? .09 : 0), o.z1, o.z1 + .09, .025, 'trim', 1, 'casing'); }
        if (zt > o.z0) { proud(o.s0 - .1, o.s1 + .1, o.z0 - .07, o.z0, .06, casing ? 'trim' : 'stone', 0, 'sill'); plate(o.s0 - .1, o.s1 + .1, o.z0, -.06, 0, casing ? 'trim' : 'stone', 1, 'sill'); }
      } else if (o.kind === 'door') {
        const cutDoor = W.cut != null && zt < o.z1 - 0.05, dwid = o.s1 - o.s0;
        if (!cutDoor) { const u = b.doorOpen, ph = u * 1.55, hs = o.s0, e = (d, s) => Pm(s, 0, d);
          if (u < 0.02) { out.push(F([Pm(o.s0, o.z0, .07), Pm(o.s1, o.z0, .07), Pm(o.s1, o.z1, .07), Pm(o.s0, o.z1, .07)], 'door', 0, Object.assign({ tag: 'door.leaf' }, CD)));
            for (const [z0, z1] of [[o.z0 + .18, o.z0 + .9], [o.z0 + 1.05, o.z1 - .16]]) out.push(F([Pm(o.s0 + .13, z0, .065), Pm(o.s1 - .13, z0, .065), Pm(o.s1 - .13, z1, .065), Pm(o.s0 + .13, z1, .065)], 'door', -1, Object.assign({ tag: 'door.leaf', db: 0.01 }, CD)));
            proud(o.s1 - .2, o.s1 - .14, o.z0 + .98, o.z0 + 1.04, -.05, 'brass', 1, 'door.knob'); }
          else { const tip = (z) => { const c = W.C(z); return [c[0] - W.n[0] * (.07 + dwid * Math.sin(ph)) + W.t[0] * (hs + dwid * Math.cos(ph)), c[1] - W.n[1] * (.07 + dwid * Math.sin(ph)) + W.t[1] * (hs + dwid * Math.cos(ph)), z]; };
            out.push(F([Pm(hs, o.z0, .07), tip(o.z0), tip(o.z1), Pm(hs, o.z1, .07)], 'door', 0, Object.assign({ tag: 'door.leaf', ind: 1 }, CD))); } }
        else plate(o.s0 + .06, o.s1 - .06, o.z0 + 0.012, T + 0.06, T + 0.62, 'mat', 0, 'entry.mat', { ind: 1 });
        if (casing) { if (o.jl !== false) proud(o.s0 - .1, o.s0, o.z0, o.z1 + .1, .025, 'trim', 1, 'casing'); if (o.jr !== false) proud(o.s1, o.s1 + .1, o.z0, o.z1 + .1, .025, 'trim', 1, 'casing'); proud(o.s0 - .1, o.s1 + .1, o.z1, o.z1 + .1, .025, 'trim', 1, 'casing'); }
        plate(o.s0 - .12, o.s1 + .12, o.z0 + 0.01, -0.34, T, 'stone', 1, 'entry.threshold');
      } }
  }

  // ---- the tower ---------------------------------------------------------------------------------------------------------
  function shaft(out, b, cut, cam) {
    const n = b.n, cosn = Math.cos(Math.PI / n), sinn = Math.sin(Math.PI / n), tann = Math.tan(Math.PI / n), mason = b.clad === 'plaster' || b.clad === 'stoneBlock', chord = 2 * b.rB * sinn;
    const zB = [b.zC]; for (let k = 1; k < b.NB; k++) zB.push(b.zBase + b.hT * k / b.NB); if (b.fam.entasis) for (let z = b.zBase + 1.6; z < b.zTop; z += 1.6) zB.push(z);
    const sw = Math.min(chord * 0.5, b.rB * 0.92), stripes = b.daymark === 'stripeRed' ? [{ a: -Math.PI / 2, w: sw, z0: b.zC }, { a: Math.PI / 2, w: sw, z0: b.zC }] : [];
    const cladT = tex(b.clad), stoneT = tex('stoneBlock'), inT = mason ? tex('plaster') : b.clad === 'corrugated' || b.clad === 'steelPlate' ? null : tex('boards');
    for (let i = 0; i < n; i++) { const am = b.rot + TAU * (i + 0.5) / n, c = Math.cos(am), s = Math.sin(am), near = -(c * cam.st + s * cam.ct) > 0.02;
      const hwAt = (z) => b.rAt(z) * sinn, ops = facetOps(b.ops, am, b.ap(b.zBase), hwAt, n), stp = facetOps(stripes, am, b.ap(b.zBase), hwAt, n), sB = []; stp.forEach(q => sB.push(q.s0, q.s1));
      const W = { C: (z) => { const ap = b.ap(z); return [c * ap, s * ap]; }, t: [-s, c], n: [c, s], hw: hwAt, hwi: (z) => (b.ap(z) - b.T(z)) * tann, T: b.T, z0: b.zBase, z1: b.zTop,
        cut: cut ? (near ? cut.zc : cut.zf) : null, ops, zB, sB, u0: i * chord, casing: !mason && b.clad !== 'steelPlate' && b.clad !== 'corrugated',
        paint: (sm, zm) => zm < b.zC ? 'stone' : stp.some(q => sm > q.s0 && sm < q.s1) ? 'mark' : b.markAt(Math.min(b.NB - 1, Math.max(0, Math.floor((zm - b.zBase) / b.hT * b.NB))), am),
        texOf: (m) => m === 'stone' ? stoneT : cladT, texIn: inT, inMat: 'finish', capMat: 'cap', revMat: mason ? 'finish' : 'trim' };
      dress(out, W, facet(out, W), b); }
    // string course under the gallery
    frustum(out, b.zTop - 0.34, b.zTop - 0.22, b.rAt(b.zTop) + 0.07, b.rAt(b.zTop) + 0.07, n, b.rot, 'trim', { b: 1, tag: 'string' });
    // entry steps up to the door
    if (b.nSteps) { const ro = b.ap(b.zBase), rs = b.zBase / b.nSteps; for (let k = 0; k < b.nSteps; k++) { const y1 = -(ro + 0.34 + (b.nSteps - 1 - k) * 0.28); boxS(out, -0.75, 0.75, y1 - 0.28, y1, 0, rs * (k + 1), 'stone', 0, { tag: 'entry.steps', grp: 'base' }); } }
  }
  function interior(out, b) {
    const st = b.stair, zb = b.zBase, mason = b.clad === 'plaster' || b.clad === 'stoneBlock', tm = b.clad === 'shingle' || b.clad === 'clapboard' ? 'wood' : 'iron';
    withCtx({ ind: 1, tag: 'floor' }, () => disc(out, zb + 0.005, b.rIn(zb) + 0.03, mason ? 'flags' : 'planks', 0, { tex: tex(mason ? 'flags' : 'planks') }));
    // the goods where the camera looks: oil at a load (0.85) left of the door, the log at a bench (0.80) across the floor
    withCtx({ ind: 1, obj: 1, tag: 'goods.oil' }, () => { const a = -Math.PI / 2 - 0.95, r = b.rIn(zb) - 0.2, c = Math.cos(a), s = Math.sin(a), t = [-s, c], P = (u, v) => [c * (r + v) + t[0] * u, s * (r + v) + t[1] * u];
      const q = [P(-0.42, -0.16), P(0.42, -0.16), P(0.42, 0.14), P(-0.42, 0.14)], xs = q.map(p => p[0]), ys = q.map(p => p[1]);
      boxS(out, Math.min(...xs), Math.max(...xs), Math.min(...ys), Math.max(...ys), zb + 0.78, zb + 0.85, 'wood', 0);
      for (const u of [-0.25, 0, 0.25]) { const p = P(u, 0); boxS(out, p[0] - 0.08, p[0] + 0.08, p[1] - 0.08, p[1] + 0.08, zb + 0.85, zb + 1.13, 'oil', 0); } });
    if (st.kind === 'helix') withCtx({ ind: 1, obj: 1, tag: 'goods.log' }, () => { const a = Math.PI / 2 + 0.2, r = b.rIn(zb) - 0.3, p = ringPt(a, r); boxS(out, p[0] - 0.4, p[0] + 0.4, p[1] - 0.22, p[1] + 0.22, zb + 0.74, zb + 0.80, 'wood', 0);
      boxS(out, p[0] - 0.36, p[0] - 0.3, p[1] - 0.18, p[1] + 0.18, zb, zb + 0.74, 'wood', -1); boxS(out, p[0] + 0.3, p[0] + 0.36, p[1] - 0.18, p[1] + 0.18, zb, zb + 0.74, 'wood', -1); boxS(out, p[0] - 0.12, p[0] + 0.14, p[1] - 0.1, p[1] + 0.08, zb + 0.80, zb + 0.84, 'mark', 1); });
    withCtx({ ind: 1, tag: 'stair' }, () => {
      if (st.kind === 'helix') { let prev = null;
        for (const t of st.treads) { ann(out, t.z, t.a0, t.a1, t.r0, t.r1, tm, 0, { tag: 'stair.tread' });
          const n0 = ringPt(t.a0, t.r0), n1 = ringPt(t.a0, t.r1); out.push(F([[n0[0], n0[1], t.z - 0.05], [n1[0], n1[1], t.z - 0.05], [n1[0], n1[1], t.z], [n0[0], n0[1], t.z]], tm, 1, { tag: 'stair.nosing' }));
          const s0 = ringPt(t.a0, t.r0 - 0.012), s1 = ringPt(t.a1, t.r0 - 0.012); out.push(F([[s0[0], s0[1], t.z - 0.3], [s1[0], s1[1], t.z - 0.3 + 0.02], [s1[0], s1[1], t.z], [s0[0], s0[1], t.z]], 'iron', 0, { tag: 'stair.stringer' }));
          const am = (t.a0 + t.a1) / 2, bp = ringPt(am, t.r0 + 0.04); bar3(out, [bp[0], bp[1], t.z], [bp[0], bp[1], t.z + 0.9], 0.016, 'iron', 0, { tag: 'stair.baluster' });
          if (prev) bar3(out, prev, [bp[0], bp[1], t.z + 0.9], 0.024, 'brass', 1, { tag: 'stair.rail' }); prev = [bp[0], bp[1], t.z + 0.9]; }
        const La = st.landing; ann(out, st.zL, La.a0, La.a1, La.r0, La.r1, tm, 0, { tag: 'landing' });
        let pr = null; for (let a = La.a0; a <= La.a1 - 0.62 / st.ladder.r + 1e-3; a += 0.3 / Math.max(0.3, La.r0 + 0.04)) { const p = ringPt(a, La.r0 + 0.04); bar3(out, [p[0], p[1], st.zL], [p[0], p[1], st.zL + 1.0], 0.016, 'iron', 0, { tag: 'landing.rail' }); if (pr) bar3(out, pr, [p[0], p[1], st.zL + 1.0], 0.024, 'brass', 1, { tag: 'landing.rail' }); pr = [p[0], p[1], st.zL + 1.0]; } }
      const Ld = st.ladder, c = Math.cos(Ld.a), s = Math.sin(Ld.a), t = [-s, c], P = (u, z) => [c * Ld.r + t[0] * u, s * Ld.r + t[1] * u, z];
      for (const u of [-0.225, 0.225]) bar3(out, P(u, Ld.z0), P(u, Ld.z1 + 1.0), 0.03, 'iron', 0, { tag: 'ladder.stile' });
      for (const z of Ld.rungs) bar3(out, P(-0.225, z), P(0.225, z), 0.018, 'iron', 1, { tag: 'ladder.rung' }); });
    // the clockwork weight in the well
    withCtx({ ind: 1, tag: 'clockwork' }, () => { const top = b.zDeck, zw = zb + 0.6 + (st.zL - zb - 1.2) * b.wound; bar3(out, [0, 0, Math.max(zb + 0.4, zw + 0.5)], [0, 0, top], 0.012, 'steel', 0, { tag: 'clockwork.chain' });
      if (st.kind === 'helix') withCtx({ obj: 1 }, () => boxS(out, -0.1, 0.1, -0.1, 0.1, zw, zw + 0.5, 'iron', 0, { tag: 'clockwork.weight' })); });
  }
  function lanternFloor(out, b) {
    const L = b.stair.ladder, h = L.hatch;
    withCtx({ ind: 1, tag: 'lantern.floor' }, () => disc(out, b.zDeck, b.rLan - 0.02, 'iron', 0, { tex: tex('chequer') }, h));
    withCtx({ ind: 0, tag: 'gallery.deck' }, () => ann(out, b.zDeck, 0, TAU, b.rLan - 0.02, b.rGal, 'deck', 0));
    const ls = b.lens, zd = b.zDeck;
    withCtx({ ind: 1, obj: 1, tag: 'lens' }, () => {
      frustum(out, zd, ls.z0 - 0.05, 0.14, 0.12, 8, 0, 'iron', { tag: 'lens.pedestal' }); polyTop(out, ls.z0 - 0.05, 0.2, 8, 0, 'brass', 1, { tag: 'lens.pedestal' });
      frustum(out, zd + 0.3, zd + 0.72, 0.24, 0.24, 8, Math.PI / 8, 'brass', { tag: 'clockwork.box' }); polyTop(out, zd + 0.72, 0.24, 8, Math.PI / 8, 'brass', 1, { tag: 'clockwork.box' });
      bar3(out, [0.24, 0, zd + 0.55], [0.44, 0, zd + 0.55], 0.02, 'iron', 1, { tag: 'clockwork.crank' }); bar3(out, [0.44, 0, zd + 0.55], [0.44, 0, zd + 0.4], 0.02, 'iron', 1, { tag: 'clockwork.crank' });
      if (ls.step) { ann(out, zd + 0.3, 0, TAU, ls.rl + 0.08, ls.rl + 0.6, 'iron', 0, { tag: 'lens.step', tex: tex('chequer') }); frustum(out, zd, zd + 0.3, ls.rl + 0.6, ls.rl + 0.6, 16, 0, 'iron', { b: -1, tag: 'lens.step' }); }
      const col = COLRAMP[CHARS[b.char].col] || 'lamp';
      for (let k = 0; k < 8; k++) { const a0 = TAU * k / 8, a1 = TAU * (k + 1) / 8, p0 = ringPt(a0, ls.rl), p1 = ringPt(a1, ls.rl);
        out.push(F([[p0[0], p0[1], ls.z0], [p1[0], p1[1], ls.z0], [p1[0], p1[1], ls.z1], [p0[0], p0[1], ls.z1]], 'lens', k % 2 ? 0 : 1, { em: 'lens:' + k, emc: col, occ: false, tag: 'lens.panel', uv: [[0, 0], [1, 0], [1, 1], [0, 1]], tex: (u, v) => (Math.abs(u - 0.5) < 0.18 && Math.abs(v - 0.5) < 0.2) ? 1 : (Math.floor(v * 6) % 2 ? -1 : 0) })); }
      for (const z of [ls.z0, (ls.z0 + ls.z1) / 2, ls.z1]) hoop(out, z, ls.rl + 0.02, 8, 0, 0.022, 'brass', 1, { tag: 'lens.frame' });
      coneTop(out, ls.z1, ls.z1 + 0.18, ls.rl + 0.02, 8, 0, 'brass', 1, { tag: 'lens.frame' }); });
  }
  function gallery(out, b) {
    const n = b.n < 8 ? 8 : Math.min(b.n, 16), rot = b.rot, ns = Math.min(n, 12);
    withCtx({ tag: 'gallery' }, () => {
      if (b.fam.corbel) for (let i = 0; i < n; i++) { const a = rot + TAU * (i + 0.5) / n, pi = ringPt(a, b.rT + 0.06), po = ringPt(a, b.rGal - 0.06); bar3(out, [pi[0], pi[1], b.zTop - 0.56], [po[0], po[1], b.zTop + 0.02], 0.10, 'trim', 0); }
      frustum(out, b.zTop, b.zDeck, b.rGal, b.rGal, n, rot, 'trim', { b: -1 });
      if (b.gallery === 'none') return;
      const posts = b.gallery === 'picket' ? Math.min(n * 2, 16) : ns;
      for (let i = 0; i < posts; i++) { const p = ringPt(rot + TAU * i / posts, b.rGal - 0.09); bar3(out, [p[0], p[1], b.zDeck], [p[0], p[1], b.zDeck + b.railH], 0.045, 'steel', 0); }
      for (const fr of (b.gallery === 'pipe' ? [1.0, 0.52] : [1.0, 0.62, 0.24])) hoop(out, b.zDeck + b.railH * fr, b.rGal - 0.09, ns, rot, 0.034, 'steel', 1);
      if (b.gallery === 'lattice') for (let i = 0; i < ns; i++) { const p0 = ringPt(rot + TAU * i / ns, b.rGal - 0.09), p1 = ringPt(rot + TAU * (i + 1) / ns, b.rGal - 0.09); bar3(out, [p0[0], p0[1], b.zDeck + 0.1], [p1[0], p1[1], b.zDeck + b.railH * 0.9], 0.024, 'steel', 0); } });
  }
  function lantern(out, b) {
    const n = b.lanN, rot = Math.PI / n, r = b.rLan, zd = b.zDeck, zp = zd + b.parH, zg = b.glzTop, dw = Math.min(0.82, 2 * r * Math.sin(Math.PI / n) * 0.92), aDo = -Math.PI / 2;
    withCtx({ tag: 'lantern' }, () => {
      frustum(out, zd, zp, r + 0.05, r + 0.03, n, rot, 'body', { tag: 'lantern.parapet' }); frustum(out, zd, zp, r - 0.05, r - 0.05, n, rot, 'finish', { ind: 1, tag: 'lantern.parapet' });
      ann(out, zp, 0, TAU, r - 0.05, r + 0.05, 'body', 1, { tag: 'lantern.parapet' });
      // the door onto the gallery: a leaf on the facet facing the show side, iron below and glazed above
      const fi = Math.round((aDo - rot) / (TAU / n) - 0.5), am = rot + TAU * (fi + 0.5) / n, c = Math.cos(am), s = Math.sin(am), ap = (r + 0.04) * Math.cos(Math.PI / n), t = [-s, c], P = (u, z, d) => [c * (ap + d) + t[0] * u, s * (ap + d) + t[1] * u, z];
      out.push(F([P(-dw / 2, zd, 0.01), P(dw / 2, zd, 0.01), P(dw / 2, zp, 0.01), P(-dw / 2, zp, 0.01)], 'door', 0, { tag: 'lantern.door', db: 0.02 }));
      for (const u of [-dw / 2, dw / 2]) bar3(out, P(u, zd, 0.02), P(u, zd + 2.0, 0.02), 0.03, 'trim', 1, { tag: 'lantern.door' }); bar3(out, P(-dw / 2, zd + 2.0, 0.02), P(dw / 2, zd + 2.0, 0.02), 0.03, 'trim', 1, { tag: 'lantern.door' });
      if (b.lantern === 'glazed' || b.lantern === 'drum') {
        for (let i = 0; i < n; i++) { const a0 = rot + TAU * i / n, a1 = rot + TAU * (i + 1) / n, p0 = ringPt(a0, r), p1 = ringPt(a1, r), m = (a0 + a1) / 2;
          out.push(F([[p0[0], p0[1], zp], [p1[0], p1[1], zp], [p1[0], p1[1], zg], [p0[0], p0[1], zg]], 'glass', 1, { occ: false, ao: false, glaze: [Math.cos(m), Math.sin(m)], tag: 'lantern.glaze', em: 'glaze', emc: COLRAMP[CHARS[b.char].col] })); }
        for (let i = 0; i < n; i++) { const p = ringPt(rot + TAU * i / n, r + 0.015); bar3(out, [p[0], p[1], zp], [p[0], p[1], zg], 0.042, 'trim', 1, { tag: 'lantern.astragal' }); }
        hoop(out, (zp + zg) / 2, r + 0.015, n, rot, 0.03, 'trim', 1, { tag: 'lantern.astragal' });
      } else for (let i = 0; i < 4; i++) { const p = ringPt(rot + TAU * (i + 0.5) / 4, r * 0.8); bar3(out, [p[0], p[1], zp], [p[0], p[1], zg], 0.05, 'steel', 0); }
      frustum(out, zg, b.zLanTop, r + 0.06, r + 0.10, n, rot, 'mark', { b: 1 });
      if (b.cap === 'cone') { coneTop(out, b.zLanTop, b.zLanTop + b.capH, b.rCap, n, rot, 'mark', 0); frustum(out, b.zLanTop - 0.06, b.zLanTop + 0.06, b.rCap, b.rCap * 0.98, n, rot, 'mark', { b: -1 }); }
      else if (b.cap === 'dome') { domeTop(out, b.zLanTop, b.capH, b.rCap, n, rot, 'mark', {}); frustum(out, b.zLanTop - 0.08, b.zLanTop + 0.05, b.rCap, b.rCap, n, rot, 'mark', { b: -1 }); }
      else { frustum(out, b.zLanTop, b.zLanTop + b.capH, b.rCap, b.rCap * 0.94, n, rot, 'steel', {}); polyTop(out, b.zLanTop + b.capH, b.rCap * 0.94, n, rot, 'steel', 1); }
      const zv = b.zLanTop + b.capH, rv = Math.max(0.12, r * 0.19); domeTop(out, zv, rv * 1.5, rv, 8, rot, 'mark', {});
      bar3(out, [0, 0, zv + rv * 1.5], [0, 0, zv + rv * 1.5 + 0.62], 0.03, 'steel', 1); bar3(out, [-0.16, 0, zv + rv * 1.5 + 0.42], [0.16, 0, zv + rv * 1.5 + 0.42], 0.026, 'steel', 1); });
  }
  function plinth(out, b) {
    withCtx({ grp: 'base', tag: 'plinth' }, () => { const n = b.n < 8 ? 8 : b.n, rot = b.rot;
      if (b.plinth === 'concrete') { frustum(out, 0, b.zBase, b.rB + 0.52, b.rB + 0.40, n, rot, 'stone', { tex: tex('stoneBlock') }); polyTop(out, b.zBase - 0.004, b.rB + 0.40, n, rot, 'stone', 1); }
      else if (b.plinth === 'rock') { const rnd = mulberry32(91 + ((b.size * 137) | 0));
        for (let k = 0; k < 3; k++) { const rr = b.rB + 1.55 - k * 0.42, z1 = b.zBase * (0.45 + 0.28 * k), pts = []; for (let i = 0; i < 9; i++) pts.push([rot + TAU * i / 9, rr * (0.80 + rnd() * 0.45)]);
          for (let i = 0; i < 9; i++) { const [a0, q0] = pts[i], [a1, q1] = pts[(i + 1) % 9], p0 = ringPt(a0, q0), p1 = ringPt(a1, q1); out.push(F([[p0[0], p0[1], 0], [p1[0], p1[1], 0], [p1[0], p1[1], z1], [p0[0], p0[1], z1]], 'stone', k % 2 ? -1 : 0, { uv: [[0, 0], [1.4, 0], [1.4, z1], [0, z1]], tex: tex('stoneBlock') })); }
          out.push(F(pts.map(([a, q]) => { const p = ringPt(a, q); return [p[0], p[1], z1 - 0.004 * (2 - k)]; }), 'stone', 1)); } }
      else if (b.plinth === 'crib') { const hw = b.rB + 0.85; boxS(out, -hw, hw, -hw, hw, 0, b.zBase, 'wood', 0, { tex: tex('clapboard') });
        for (const sx of [-1, 1]) for (const sy of [-1, 1]) bar3(out, [sx * hw, sy * hw, -0.1], [sx * hw, sy * hw, b.zBase + 0.16], 0.11, 'wood', 0); }
      else if (b.plinth === 'caisson') { const rC = b.rB * 1.62; frustum(out, 0, b.zBase - 0.30, rC, rC * 0.97, 24, rot, 'steel', { tex: tex('steelPlate') }); frustum(out, b.zBase - 0.30, b.zBase - 0.06, rC * 1.06, rC * 1.02, 24, rot, 'mark', { b: 1 });
        frustum(out, b.zBase - 0.06, b.zBase, rC * 0.99, b.rB + 0.42, 24, rot, 'steel', { b: -1 }); ann(out, b.zBase - 0.004, 0, TAU, 0, rC * 0.99, 'deck', 0);
        hoop(out, b.zBase + 0.92, rC * 0.99, 24, rot, 0.04, 'steel', 1); for (let i = 0; i < 12; i++) { const p = ringPt(rot + TAU * i / 12, rC * 0.99); bar3(out, [p[0], p[1], b.zBase], [p[0], p[1], b.zBase + 0.96], 0.04, 'steel', 0); } } });
    if (b.fam.legs) withCtx({ tag: 'lattice' }, () => { const rB = b.rB + 2.05, rT = b.rT + 0.72, z0 = 0.12, z1 = b.zTop, lp = (i, t) => { const p = ringPt(Math.PI / 4 + TAU * i / 4, rB + (rT - rB) * t); return [p[0], p[1], z0 + (z1 - z0) * t]; };
      for (let i = 0; i < 4; i++) bar3(out, lp(i, 0), lp(i, 1), 0.11, 'steel', 0); const nb = Math.max(3, Math.round((z1 - z0) / 1.85));
      for (let k = 0; k <= nb; k++) for (let i = 0; i < 4; i++) bar3(out, lp(i, k / nb), lp((i + 1) % 4, k / nb), 0.05, 'steel', 0);
      for (let k = 0; k < nb; k++) for (let i = 0; i < 4; i++) { const up = (k + i) % 2 === 0; bar3(out, lp(i, up ? k / nb : (k + 1) / nb), lp((i + 1) % 4, up ? (k + 1) / nb : k / nb), 0.04, 'steel', 0); }
      for (let i = 0; i < 4; i++) { const p = lp(i, 0); boxS(out, p[0] - 0.42, p[0] + 0.42, p[1] - 0.42, p[1] + 0.42, 0, 0.34, 'stone', 0, { grp: 'base' }); } });
    if (b.ladder) withCtx({ tag: 'ladder.outside' }, () => { const a = Math.PI / 2, c = Math.cos(a), s = Math.sin(a), t = [-s, c], P = (u, z) => { const r = b.ap(z) + 0.2; return [c * r + t[0] * u, s * r + t[1] * u, z]; };
      for (const u of [-0.225, 0.225]) bar3(out, P(u, b.zBase + 0.2), P(u, b.zTop + 0.1), 0.032, 'steel', 0);
      for (let z = b.zBase + 0.44; z < b.zTop; z += CHAR92.rung) bar3(out, P(-0.225, z), P(0.225, z), 0.018, 'steel', 1); });
  }
  // ---- station buildings (pass 1's, lit live, windows as emitters) ------------------------------------------------------
  function gableHouse(out, ox, oy, Wd, Ln, fZ, wallH, pitch, mat, siTex, roofKey, o) {
    o = o || {}; const hw = Wd / 2, y0 = oy - Ln / 2, y1 = oy + Ln / 2, eave = fZ + wallH, ridge = eave + hw * pitch, ov = 0.3, W = (x0, a0, x1, a1) => out.push(F([[x0, a0, fZ], [x1, a1, fZ], [x1, a1, eave], [x0, a0, eave]], mat, 0, { uv: [[0, fZ], [Math.hypot(x1 - x0, a1 - a0), fZ], [Math.hypot(x1 - x0, a1 - a0), eave], [0, eave]], tex: siTex }));
    boxS(out, ox - hw - 0.08, ox + hw + 0.08, y0 - 0.08, y1 + 0.08, 0, fZ, 'stone', -1); W(ox - hw, y0, ox - hw, y1); W(ox + hw, y1, ox + hw, y0); W(ox + hw, y0, ox - hw, y0); W(ox - hw, y1, ox + hw, y1);
    for (const yv of [y0, y1]) out.push(F([[ox - hw, yv, eave], [ox + hw, yv, eave], [ox, yv, ridge]], mat, 0, { uv: [[0, eave], [Wd, eave], [hw, ridge]], tex: siTex }));
    const yA = y0 - ov, yB = y1 + ov, rT = tex('clapboard');
    out.push(F([[ox - hw - ov, yA, eave], [ox - hw - ov, yB, eave], [ox, yB, ridge], [ox, yA, ridge]], roofKey, 0, { uv: [[0, 0], [Ln, 0], [Ln, hw * 1.4], [0, hw * 1.4]], tex: rT, tag: 'roof' }));
    out.push(F([[ox + hw + ov, yB, eave], [ox + hw + ov, yA, eave], [ox, yA, ridge], [ox, yB, ridge]], roofKey, 0, { uv: [[0, 0], [Ln, 0], [Ln, hw * 1.4], [0, hw * 1.4]], tex: rT, tag: 'roof' }));
    for (const yv of [yA, yB]) for (const sg of [-1, 1]) out.push(F([[ox + sg * (hw + ov), yv, eave], [ox, yv, ridge], [ox, yv, ridge - 0.19], [ox + sg * (hw + ov), yv, eave - 0.19]], 'trim', 1));
    if (o.chimney) { const cw = 0.42; boxS(out, ox - cw / 2, ox + cw / 2, oy + Ln * 0.22 - cw / 2, oy + Ln * 0.22 + cw / 2, eave, ridge + 0.95, 'brick', 0); }
    return { hw, y0, y1, eave, ridge };
  }
  function hWin(out, axis, v, sg, c, z, w, h, room) { const e = 0.03 * sg, P = (a0, a1, z0, z1, mat, bb, o) => { const pts = axis === 'x' ? [[v + e, a0, z0], [v + e, a1, z0], [v + e, a1, z1], [v + e, a0, z1]] : [[a0, v + e, z0], [a1, v + e, z0], [a1, v + e, z1], [a0, v + e, z1]]; out.push(F(pts, mat, bb, Object.assign({ db: 0.06 }, o))); };
    P(c - w / 2 - 0.09, c + w / 2 + 0.09, z - 0.03, z + h + 0.09, 'trim', 1); P(c - w / 2, c + w / 2, z, z + h, 'glass', 0, { em: 'win:' + room, occ: false, db: 0.1 }); P(c - 0.028, c + 0.028, z, z + h, 'trim', 1, { db: 0.14 }); P(c - w / 2, c + w / 2, z + h / 2 - 0.028, z + h / 2 + 0.028, 'trim', 1, { db: 0.14 }); }
  function hDoor(out, axis, v, sg, c, z0, w, h, lamp) { const e = 0.03 * sg, P = (a0, a1, zz0, zz1, mat, bb, o) => { const pts = axis === 'x' ? [[v + e, a0, zz0], [v + e, a1, zz0], [v + e, a1, zz1], [v + e, a0, zz1]] : [[a0, v + e, zz0], [a1, v + e, zz0], [a1, v + e, zz1], [a0, v + e, zz1]]; out.push(F(pts, mat, bb, Object.assign({ db: 0.06 }, o))); };
    P(c - w / 2 - 0.1, c + w / 2 + 0.1, z0, z0 + h + 0.1, 'trim', 1); P(c - w / 2, c + w / 2, z0, z0 + h, 'door', 0, { db: 0.1, tag: 'door' }); P(c - w / 2 + 0.13, c + w / 2 - 0.13, z0 + h * 0.55, z0 + h - 0.14, 'glass', 0, { db: 0.12, em: 'win:' + lamp, occ: false });
    if (lamp) { const lz = z0 + h + 0.3, lc = c + w / 2 + 0.28, q = axis === 'x' ? [v + sg * 0.12, lc] : [lc, v + sg * 0.12]; boxS(out, q[0] - 0.07, q[0] + 0.07, q[1] - 0.07, q[1] + 0.07, lz, lz + 0.22, 'dark', 0, { em: 'lamp:door', emc: 'warm', tag: 'lamp.door', occ: false }); } }
  function stationBuildings(out, b, siTex) {
    const off = b.rB + 3.1;
    withCtx({ grp: 'station', tag: 'dwelling' }, () => { if (b.dwelling && !b.fam.dwelling) { const ox = -off - 1.5, oy = 0.9, Wd = 6.4, Ln = 8.2, fZ = 0.5; gableHouse(out, ox, oy, Wd, Ln, fZ, 3.7, 0.95, 'body', siTex, 'roof', { chimney: true });
      hDoor(out, 'y', oy - Ln / 2, -1, ox + 1.2, fZ, 1.0, 2.05, 'dwelling'); boxS(out, ox + 0.6, ox + 1.8, oy - Ln / 2 - 0.75, oy - Ln / 2, 0, fZ, 'wood', 0);
      hWin(out, 'y', oy - Ln / 2, -1, ox - 1.5, fZ + 1.0, 0.8, 1.15, 'dwelling'); hWin(out, 'y', oy - Ln / 2, -1, ox, fZ + 2.3 + 1.0, 0.7, 0.9, 'dwelling');
      hWin(out, 'x', ox + Wd / 2, 1, oy + 2.1, fZ + 1.0, 0.8, 1.15, 'dwelling'); hWin(out, 'x', ox - Wd / 2, -1, oy - 1.6, fZ + 1.0, 0.8, 1.15, 'dwelling'); hWin(out, 'x', ox - Wd / 2, -1, oy + 2.1, fZ + 1.0, 0.8, 1.15, 'dwelling'); } });
    withCtx({ grp: 'station', tag: 'shed' }, () => { if (b.shed) { const ox = off + 1.1, oy = -1.6, Wd = 3.9, Ln = 4.9, fZ = 0.4; gableHouse(out, ox, oy, Wd, Ln, fZ, 2.85, 0.62, 'body', siTex, 'roofM', {});
      hDoor(out, 'y', oy - Ln / 2, -1, ox + 0.9, fZ, 0.95, 2.0, 'shed'); hWin(out, 'x', ox - Wd / 2, -1, oy, fZ + 1.0, 0.75, 1.0, 'shed');
      const hz = fZ + 2.4, hy = oy - Ln / 2 - 0.1, hx = ox - 0.7; boxS(out, hx - 0.3, hx + 0.3, hy - 0.55, hy, hz - 0.3, hz + 0.3, 'steel', 0); boxS(out, hx - 0.46, hx + 0.46, hy - 0.92, hy - 0.55, hz - 0.46, hz + 0.46, 'steel', 0); boxS(out, hx - 0.62, hx + 0.62, hy - 1.12, hy - 0.92, hz - 0.62, hz + 0.62, 'mark', 0); } });
    withCtx({ grp: 'station', tag: 'oilHouse' }, () => { if (b.oilHouse) { const ox = -off + 0.4, oy = -4.6, Wd = 2.5, Ln = 2.9, fZ = 0.34; gableHouse(out, ox, oy, Wd, Ln, fZ, 2.1, 0.5, 'brick', tex('stoneBlock'), 'roofM', {}); hDoor(out, 'x', ox + Wd / 2, 1, oy, fZ, 0.85, 1.85, null); } });
  }

  // ---- the cut ----------------------------------------------------------------------------------------------------------
  function clipZ(f, zmax) { const v = f.v, uv = f.uv, n = v.length; let allIn = true, allOut = true; for (const p of v) { if (p[2] > zmax + 1e-6) allIn = false; else allOut = false; }
    if (allIn) return f; if (allOut) return null; const ov = [], ou = uv ? [] : null;
    for (let i = 0; i < n; i++) { const a = v[i], c = v[(i + 1) % n], ia = a[2] <= zmax + 1e-6, ic = c[2] <= zmax + 1e-6; if (ia) { ov.push(a); if (ou) ou.push(uv[i]); }
      if (ia !== ic) { const t = (zmax - a[2]) / (c[2] - a[2]); ov.push([a[0] + (c[0] - a[0]) * t, a[1] + (c[1] - a[1]) * t, zmax]); if (ou) { const ua = uv[i], ub = uv[(i + 1) % n]; ou.push([ua[0] + (ub[0] - ua[0]) * t, ua[1] + (ub[1] - ua[1]) * t]); } } }
    return ov.length < 3 ? null : Object.assign({}, f, { v: ov, uv: ou }); }
  function cutOf(b, o) {
    if (!o || o.cutaway !== 'section') return null; const st = b.stair, zb = b.zBase;
    let z = o.standZ != null ? +o.standZ : o.climb != null ? posAt(b, +o.climb).z : zb, p = o.climb != null ? posAt(b, +o.climb) : null;
    if (p && !p.inside) return null; if (z < zb - 0.05) return null;
    let zq; if (z >= b.zDeck - 0.02) zq = z >= b.lens.standZ - 0.01 && b.lens.step && p && p.seg === 'lamp' ? b.lens.standZ : b.zDeck;
    else if (st.kind === 'helix' && z <= st.zL + 1e-3) zq = zb + Math.floor((z - zb) / st.riser + 1e-6) * st.riser;
    else zq = st.ladder.z0 + Math.floor((z - st.ladder.z0) / CHAR92.rung + 1e-6) * CHAR92.rung;
    const sill = o.cutH != null ? +o.cutH : 0.9, head = o.headH != null ? +o.headH : 1.95;
    return { grp: 'tower', zFeet: r3(zq), zc: zq + sill, zf: zq + head, sill, head, key: zq.toFixed(3) + '|' + sill + '|' + head };
  }
  function applyCut(faces, cut, cam, oy) { if (!cut) return faces; const out = [];
    for (const f of faces) { if (f.cutDone || f.grp !== 'tower') { out.push(f); continue; }
      let zmin = 1e9, cx = 0, cy = 0; for (const p of f.v) { if (p[2] < zmin) zmin = p[2]; cx += p[0]; cy += p[1]; } cx /= f.v.length; cy /= f.v.length;
      if (f.obj) { if (zmin <= cut.zf) out.push(f); continue; }
      const near = cx * cam.st + (cy - oy) * cam.ct < -0.02, g = clipZ(f, near ? cut.zc : cut.zf); if (g) out.push(g); }
    return out; }

  // ---- assemble ---------------------------------------------------------------------------------------------------------
  function build(b, cut, cam) {
    const out = [], siTex = tex(b.clad);
    plinth(out, b);
    if (b.fam.dwelling) withCtx({ grp: 'tower', tag: 'dwelling' }, () => { const g = gableHouse(out, 0, 1.5, b.dwWd, b.dwLn, 0.5, b.dwH, 0.95, 'body', siTex, 'roof', { chimney: true });
      hDoor(out, 'x', b.dwWd / 2, 1, 1.5, 0.5, 1.0, 2.05, 'dwelling'); boxS(out, b.dwWd / 2, b.dwWd / 2 + 0.75, 0.8, 2.2, 0, 0.5, 'wood', 0);
      hWin(out, 'x', b.dwWd / 2, 1, 4.1, 1.5, 0.8, 1.15, 'dwelling'); hWin(out, 'x', -b.dwWd / 2, -1, 0.2, 1.5, 0.8, 1.15, 'dwelling'); hWin(out, 'x', -b.dwWd / 2, -1, 3.9, 1.5, 0.8, 1.15, 'dwelling');
      hWin(out, 'y', 1.5 + b.dwLn / 2, 1, -1.5, 1.5, 0.8, 1.15, 'dwelling'); hWin(out, 'y', 1.5 + b.dwLn / 2, 1, 1.5, 1.5, 0.8, 1.15, 'dwelling'); });
    const tw = []; shaft(tw, b, cut, cam); interior(tw, b); lanternFloor(tw, b); gallery(tw, b); lantern(tw, b);
    if (b.oy) { for (const f of tw) f.v = f.v.map(p => [p[0], p[1] + b.oy, p[2]]); }
    for (const f of tw) out.push(f);
    stationBuildings(out, b, siTex);
    return applyCut(out, cut, cam, b.oy);
  }
  function makeMats(b) {
    const wx = b.weather, W = (r) => r.map(c => mix(desat(c, wx * 0.5), '#6f6a5f', wx * 0.24)), St = (r) => r.map(c => mix(c, RUST[3], wx * 0.3)), Ro = (r) => r.map(c => mix(c, '#5f6a52', wx * 0.18)), Wd = (r) => r.map(c => mix(c, '#8a8172', wx * 0.42));
    const body = Array.isArray(b.body) ? b.body : (BODY[b.body] || BODY.white), mark = Array.isArray(b.mark) ? b.mark : (MARK[b.mark] || MARK.red), metal = b.body === 'galv' || b.body === 'ironGrey', mason = b.clad === 'plaster' || b.clad === 'stoneBlock';
    const P = (ramp, por, gloss, hold) => ({ ramp, por, gloss, hold });
    return { body: P(metal ? St(body) : W(body), metal ? .02 : .22, metal ? .5 : .06, .3), mark: P(W(mark), .2, .1, .5), trim: P(TRIM.map(c => desat(c, wx * .32)), .12, .12, .8), cap: P(CAPR, .1, .1, .9),
      deck: P(W(TRIM).map(c => mix(c, '#6f746c', .25)), .2, .2, .95), finish: P(mason ? FINISH.plaster : b.clad === 'corrugated' || b.clad === 'steelPlate' ? FINISH.steel : FINISH.boards, .1, .05, 0),
      flags: P(FLAGS, .3, .1, .9), planks: P(Wd(WOOD), .36, .08, 1), stone: P(W(STONE), .32, .1, .9), brick: P(Ro(BRICK), .3, .08, .9), wood: P(Wd(WOOD), .36, .08, 1),
      steel: P(St(STEEL), .02, .6, .6), iron: P(IRON, .02, .5, .5), brass: P(BRASS, 0, .7, .3), roof: P(Ro(ROOFS.grey), .12, .55, 1), roofM: P(Ro(ROOFS.metal), .02, .6, .4),
      door: P(DOOR, .08, .3, .2), glass: P(GLASS, 0, 1, 0), lens: P(LENS, 0, 1, 0), mat: P(MATR, .3, 0, 0), oil: P(OIL, .02, .4, .2), dark: P([KEY, KEY, '#2a2c30', '#3a3c40', '#4a4c50'], 0, 0, 0) };
  }

  // ---- frames -----------------------------------------------------------------------------------------------------------
  const MODELS = new Map(), CROPS = new Map(), NOGEO = { sky: 1, time: 1, cloud: 1, rain: 1, fog: 1, snow: 1, wind: 1, gust: 1, outline: 1, occupancy: 1, tide: 1, frame: 1, t: 1, night: 1, climb: 1, standZ: 1, cutaway: 1, cutH: 1, headH: 1, elev: 1 };
  const geoKey = (o) => JSON.stringify(Object.keys(o).sort().filter(k => !NOGEO[k] && typeof o[k] !== 'function').map(k => [k, o[k]]));
  const camOf = (dir) => { const th = dir * Math.PI / 4; return { dir, ct: Math.cos(th), st: Math.sin(th) }; };
  function cropOf(o, dir) { o = expand(o || {}); const gk = geoKey(o), key = gk + '|' + dir; let c = CROPS.get(key); if (c) return c; const b = resolve(o), cam = camOf(dir), faces = build(b, null, cam);
    let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9; for (const f of faces) for (const p of f.v) { const xr = p[0] * cam.ct - p[1] * cam.st, yr = p[0] * cam.st + p[1] * cam.ct, X = xr * S, Y = -(yr * SE + p[2] * CE) * S; if (X < x0) x0 = X; if (X > x1) x1 = X; if (Y < y0) y0 = Y; if (Y > y1) y1 = Y; }
    c = { x0: Math.floor(x0) - 6, y0: Math.floor(y0) - 6 }; c.w = Math.ceil(x1) + 7 - c.x0; c.h = Math.ceil(y1) + 7 - c.y0; CROPS.set(key, c); if (CROPS.size > 80) CROPS.delete(CROPS.keys().next().value); return c; }
  function modelOf(o, dir) { const b = resolve(o), cut = cutOf(b, o), cam = camOf(dir), key = geoKey(o) + '|' + (cut ? dir + '|' + cut.key : '-'); let M = MODELS.get(key);
    if (!M) { M = L.model(build(b, cut, cam), makeMats(b), { aoRes: b.hT > 14 ? 9 : 11 }); M.b = b; M.cut = cut; MODELS.set(key, M); if (MODELS.size > 14) MODELS.delete(MODELS.keys().next().value); } return M; }
  function frame(dir, o0, fo) {
    const o = expand(o0 || {}); dir = (((dir | 0) % 8) + 8) % 8; const M = modelOf(o, dir), cr = cropOf(o, dir), cam = camOf(dir), tc = [-cam.st, -cam.ct];
    const C = { ct: cam.ct, st: cam.st, se: SE, ce: CE, S, ox: -cr.x0, oy: -cr.y0 };
    const only = (f) => !f.src.glaze || (f.src.glaze[0] * tc[0] + f.src.glaze[1] * tc[1]) < 0.12;         // the near panes are not drawn: the lens shows through
    const fr = L.frame(M, C, cr.w, cr.h, Object.assign({ only }, fo || {}));
    fr.dir = dir; fr.opts = o; fr.b = M.b; fr.cut = M.cut; fr.w = cr.w; fr.h = cr.h; fr.px = C.ox; fr.py = C.oy; fr.pivot = { x: C.ox, y: C.oy }; return fr;
  }
  // ---- night: the keeper, the light, the lamps ---------------------------------------------------------------------------
  function lightsOn(o, sky) { o = expand(o || {}); const b = resolve(o); sky = sky || L.skyOf(o); const need = L.lampNeed(sky), t = sky.time != null ? ((+sky.time % 24) + 24) % 24 : 14, occ = o.occupancy || 'keeper', S2 = SCHEDULES[occ] || SCHEDULES.keeper;
    const exhibit = S2.dark ? 0 : (need > 0.22 || (sky.fog || 0) > 0.55) ? 1 : 0, home = S2.house.some(([a, c]) => t >= a && t < c), watch = S2.watch && exhibit;
    return { need: r3(need), time: r3(t), occupancy: occ, exhibit, watch: !!watch, windows: { tower: watch ? 0.45 * need : 0, watch: watch ? need : 0, dwelling: home ? need : 0, shed: S2.dark ? 0 : (sky.fog || 0) > 0.45 ? Math.max(0.5, need) : 0 },
      doorLamp: home && need > 0.05 ? need : 0, lens: exhibit ? 0.55 : 0 }; }
  function lightRig(fr, sky, o) { const b = fr.b, Ls = lightsOn(Object.assign({}, fr.opts, o), sky), ls = b.lens, oy = b.oy, cc = CHARS[b.char].col, lc = cc === 'R' ? '#ff9a80' : cc === 'G' ? '#9af0b0' : '#ffe2a0';
    const emit = (name) => name.indexOf('win:') === 0 ? (Ls.windows[name.slice(4)] || 0) : name.indexOf('lens:') === 0 ? Ls.lens : name === 'glaze' ? 0 : name === 'lamp:door' ? Ls.doorLamp : 0;
    const lights = []; if (Ls.exhibit) lights.push({ p: [0, oy, ls.focal], c: lc, I: 0.95, r: b.rLan + 1.6 });
    if (Ls.watch && b.stair.kind === 'helix') { const La = b.stair.landing, m = (La.a0 + La.a1) / 2, p = ringPt(m, (La.r0 + La.r1) / 2); lights.push({ p: [p[0], p[1] + oy, b.stair.zL + 1.3], c: '#ffc774', I: 0.7, r: 2.6 }); }
    return { Ls, emit, lights }; }
  function relight(fr, sky, o) { o = o || {}; sky = sky || L.skyOf(fr.opts); const R = lightRig(fr, sky, o);
    return L.relight(fr, sky, Object.assign({ emit: R.emit, lights: R.lights, indoor: fr.cut ? [0.35, 1.55] : null, tide: fr.opts.tide != null ? fr.opts.tide : null, wind: sky.wind }, o)); }
  /* the light at time t (s): the lamp level, each of the 8 panels' level, the beams (model angles) and the flash toward the camera */
  function lamp(o, t, dir, sky) { o = expand(o || {}); const b = resolve(o), ch = CHARS[b.char], Ls = lightsOn(o, sky), cam = camOf(dir || 0), ac = Math.atan2(-cam.ct, -cam.st), panels = new Float32Array(8), beams = [];
    let level = Ls.exhibit ? 1 : 0, flash = 0;
    if (level && ch.kind === 'rot') { const Tr = ch.per * 2, rot = TAU * t / Tr; for (const side of [0, Math.PI]) for (let j = 0; j < ch.grp; j++) { const a = rot + side + (j - (ch.grp - 1) / 2) * 0.24; beams.push({ a, I: 1 }); flash = Math.max(flash, Math.pow(Math.max(0, Math.cos(a - ac)), 60)); } }
    else if (level && (ch.kind === 'occ' || ch.kind === 'flash')) { const p = ((t % ch.per) + ch.per) % ch.per; level = ch.on.some(([a, c]) => p >= a && p < c) ? 1 : 0; flash = level * (ch.kind === 'flash' ? 1 : 0.35); }
    else if (level) flash = 0.3;
    for (let k = 0; k < 8; k++) { const pa = TAU * (k + 0.5) / 8; panels[k] = level ? (ch.kind === 'rot' ? 0.5 + 0.5 * flash * Math.max(0, Math.cos(pa - ac)) : 0.35 + 0.65 * level) : 0; }
    return { lit: Ls.exhibit, level, flash, beams, panels, col: ch.col, ramp: COLRAMP[ch.col] || 'lamp', beamC: BEAMC[ch.col] || BEAMC.W, focal: b.focal, oy: b.oy, rLan: b.rLan, char: b.char }; }
  function lensPixels(fr) { if (fr._lens) return fr._lens; const gb = fr.gb, EM = fr.mdl.EM, map = EM.map(n => n.indexOf('lens:') === 0 ? +n.slice(5) : -1), out = [];
    for (let i = 0; i < fr.W * fr.H; i++) if (gb.a[i] && gb.em[i] && map[gb.em[i]] >= 0) out.push(i, map[gb.em[i]], gb.sb[i]); return (fr._lens = Int32Array.from(out)); }
  function paintLens(px, fr, lp) { const P = lensPixels(fr), R = L.EMR[lp.ramp] || L.EMR.lamp; if (!lp.lit) return px;
    for (let k = 0; k < P.length; k += 3) { const i = P[k], el = lp.panels[P[k + 1]], sb = P[k + 2]; if (el < 0.04) continue; const c = R[clamp(1 + Math.round(el * 2.6) + sb + (sb > 0 ? 1 : 0), 0, 5)]; px[i * 4] = c[0]; px[i * 4 + 1] = c[1]; px[i * 4 + 2] = c[2]; }
    return px; }
  const castShadow = (fr, sky, o) => L.castShadow(fr, sky, o), shadeAt = (fr, sky, x, y, z) => L.shadeAt(fr, sky, x, y, z), contact = (fr, t) => L.contact(fr, t);
  const view = (fr, ch, sky, o) => (!ch || ch === 'lit') ? relight(fr, sky, o) : L.view(fr, ch, sky, o);
  function project(dir, p) { const cam = camOf(dir), xr = p[0] * cam.ct - p[1] * cam.st, yr = p[0] * cam.st + p[1] * cam.ct; return { x: xr * S, y: -(yr * SE + (p[2] || 0) * CE) * S }; }

  // ---- the route a keeper takes, and where they are on it --------------------------------------------------------------
  function route(o) { const b = o && o.fam ? o : resolve(o || {}); if (b._route) return b._route; const st = b.stair, oy = b.oy, P = [], add = (x, y, z, seg, clip) => P.push({ x, y: y + oy, z, seg, clip });
    const ro = b.ap(b.zBase), ri = b.rIn(b.zBase);
    add(0, -(ro + 1.7 + b.nSteps * 0.28), 0, 'approach', 'walk'); if (b.nSteps) add(0, -(ro + 0.34 + b.nSteps * 0.28), 0, 'steps', 'walk');
    add(0, -(ro + 0.2), b.zBase, 'door', 'walk'); add(0, -(ri - 0.5), b.zBase, 'inside', 'walk');
    if (st.kind === 'helix') { const t0 = st.treads[0], rw0 = (t0.r0 + t0.r1) / 2; add(Math.cos(t0.a0 - 0.2) * rw0, Math.sin(t0.a0 - 0.2) * rw0, b.zBase, 'floor', 'walk');
      for (const t of st.treads) { const am = (t.a0 + t.a1) / 2, rw = (t.r0 + t.r1) / 2; add(Math.cos(am) * rw, Math.sin(am) * rw, t.z, 'stair', 'walk'); }
      const La = st.landing, lm = (La.a0 + La.a1) / 2 - 0.1, rl = (La.r0 + La.r1) / 2; add(Math.cos(lm) * rl, Math.sin(lm) * rl, st.zL, 'landing', 'walk'); }
    const Ld = st.ladder, rf = Ld.r - CHAR92.standoff, cf = Math.cos(Ld.a), sf = Math.sin(Ld.a);
    add(cf * rf, sf * rf, Ld.z0, 'foot', 'walk'); add(cf * rf, sf * rf, b.zDeck - CHAR92.topOut, 'ladder', 'ladderDown');
    add(cf * (rf - CHAR92.topAdvance), sf * (rf - CHAR92.topAdvance), b.zDeck, 'topout', 'board');
    const sa = -Math.PI / 2 + 0.55, rs = b.lens.rl + (b.lens.step ? 0.4 : 0.34); if (b.lens.step) add(Math.cos(sa) * (b.lens.rl + 0.85), Math.sin(sa) * (b.lens.rl + 0.85), b.zDeck, 'step', 'walk');
    add(Math.cos(sa) * rs, Math.sin(sa) * rs, b.lens.standZ, 'lamp', b.lens.step ? 'boardDown' : 'walk');
    let T = 0; P[0].t = 0; for (let i = 1; i < P.length; i++) { const a = P[i - 1], c = P[i], dh = Math.hypot(c.x - a.x, c.y - a.y), dz = Math.abs(c.z - a.z);
      const dt = c.clip === 'ladderDown' ? dz / CHAR92.ladderRate : c.clip === 'board' ? 0.9 : c.clip === 'boardDown' ? 0.57 : Math.max(dh, dz * 1.4) / CHAR92.walk; T += Math.max(0.05, dt); c.t = T; }
    for (let i = 0; i < P.length; i++) { const c = P[i], n = P[Math.min(P.length - 1, i + 1)], p = P[Math.max(0, i - 1)]; c.yaw = (c.seg === 'ladder' || c.seg === 'foot') ? Math.atan2(cf, sf) / DEG : Math.atan2(n.x - p.x, n.y - p.y) / DEG; }
    return (b._route = { pts: P, T, door: P.findIndex(q => q.seg === 'door') }); }
  function posAt(o, u) { const b = o && o.fam ? o : resolve(o || {}), R = route(b), P = R.pts, t = clamp(u, 0, 1) * R.T; let i = 1; while (i < P.length - 1 && P[i].t < t) i++;
    const a = P[i - 1], c = P[i], f = c.t > a.t ? clamp((t - a.t) / (c.t - a.t), 0, 1) : 1, lp = (p, q) => p + (q - p) * f, dy = (((c.yaw - a.yaw) % 360) + 540) % 360 - 180;
    return { x: lp(a.x, c.x), y: lp(a.y, c.y), z: lp(a.z, c.z), yaw: a.yaw + dy * Math.min(1, f * 3), clip: c.clip, seg: c.seg, inside: i > R.door, t, T: R.T, reverse: c.clip === 'ladderDown', u: clamp(u, 0, 1) }; }

  // ---- camera-first: facings, anchors, stations ------------------------------------------------------------------------
  const toWorld = (p, dir) => { const a = dir * Math.PI / 4; return [p[0] * Math.cos(a) - p[1] * Math.sin(a), p[0] * Math.sin(a) + p[1] * Math.cos(a)]; };
  const compass = (v) => NAMES[((Math.round(Math.atan2(v[0], v[1]) / (Math.PI / 4)) % 8) + 8) % 8];
  function placement(o) { const list = []; for (let d = 0; d < 8; d++) { const w = toWorld([0, -1], d); if (w[1] > 0.3) continue; list.push({ dir: d, doorFaces: compass(w), via: w[1] < -0.3 ? 'door' : 'side' }); }
    list.sort((a, b) => (a.via === 'door' ? 0 : 1) - (b.via === 'door' ? 0 : 1) || Math.abs(toWorld([0, -1], a.dir)[0]) - Math.abs(toWorld([0, -1], b.dir)[0]) || a.dir - b.dir); return { show: '-Y', door: '-Y', facings: list }; }
  function anchors(dir, o) { const b = resolve(o || {}), st = b.stair, oy = b.oy, pt = (x, y, z) => { const q = project(dir, [x, y + oy, z]), w = toWorld([x, y + oy], dir); return { x: r2(q.x), y: r2(q.y), m: [r3(x), r3(y + oy), r3(z)], w: [r3(w[0]), r3(w[1])] }; };
    const ro = b.ap(b.zBase), La = st.landing, lm = La ? (La.a0 + La.a1) / 2 : 0, h = st.ladder.hatch, hm = (h.a0 + h.a1) / 2;
    return { lamp: pt(0, 0, b.focal), gallery: pt(0, 0, b.zDeck), apex: pt(0, 0, b.zApex), door: pt(0, -ro, b.zBase + 1.0), threshold: pt(0, -ro, b.zBase), approach: pt(0, -(ro + 1.7 + b.nSteps * 0.28), 0),
      landing: st.kind === 'helix' ? pt(Math.cos(lm) * (La.r0 + La.r1) / 2, Math.sin(lm) * (La.r0 + La.r1) / 2, st.zL) : null, hatch: pt(Math.cos(hm) * (h.r0 + h.r1) / 2, Math.sin(hm) * (h.r0 + h.r1) / 2, b.zDeck),
      focal: r2(b.focal), height: r2(b.zApex), hT: r2(b.hT), Wd: r2(b.rB * 2), rGal: r2(b.rGal), doorFaces: compass(toWorld([0, -1], dir)), label: b.fam.label, char: b.char }; }
  function stations(o) { const b = resolve(o || {}), st = b.stair, F2 = CHAR92.fits, fit = (k, z) => z >= F2[k][0] - 1e-9 && z <= F2[k][1] + 1e-9, out = [], ro = b.ap(b.zBase), ri = b.rIn(b.zBase), oy = b.oy;
    out.push({ id: 'door', kind: 'door', verb: 'enter', clip: 'walk', at: [0, -ro + oy, b.zBase], clearWidth: 1.0, clearHeight: 2.05, fits: 1.0 >= CHAR92.clearDoor && 2.05 >= CHAR92.headroom, opens: 'one leaf swings in 90°' });
    if (b.nSteps) out.push({ id: 'entry.steps', kind: 'steps', verb: 'climbSteps', n: b.nSteps, riser: r3(b.zBase / b.nSteps), going: 0.28, clip: 'boardDown', reverse: true, opts: { railZ: r3(b.zBase / b.nSteps) }, fits: b.zBase / b.nSteps <= CHAR92.fits.stepUpNoGrip[1] });
    if (!b.nSteps && b.zBase > 0.06) out.push({ id: 'entry.boat', kind: 'note', verb: 'boardFromBoat', note: 'the ' + b.plinth + ' deck is ' + r2(b.zBase) + ' m up: land by the outside ladder or from a boat', fits: !!b.ladder });
    const shelf = ringPt(-Math.PI / 2 - 0.95, ri - 0.7); out.push({ id: 'oil', kind: 'fixture', verb: 'fetchOil', clip: 'lift', also: ['place'], fixture: 'load', fixtureZ: 0.85, opts: { workZ: 0.85 }, stand: [r3(shelf[0]), r3(shelf[1] + oy)], standZ: b.zBase, fits: fit('load', 0.85) });
    if (st.kind === 'helix') { const d = ringPt(Math.PI / 2 + 0.2, ri - 0.85); out.push({ id: 'log', kind: 'fixture', verb: 'writeLog', clip: 'bench', fixture: 'bench', fixtureZ: 0.80, opts: { workZ: 0.80 }, stand: [r3(d[0]), r3(d[1] + oy)], standZ: b.zBase, fits: fit('bench', 0.80) });
      out.push({ id: 'spiral', kind: 'stair', verb: 'climbStair', also: ['descendStair'], clip: 'walk', n: st.n, riser: r3(st.riser), going: r3(st.going), width: r3(st.w), turns: st.turns, pitchDeg: st.pitchDeg, headroomMin: st.headroomMin, riseTurnMin: st.rptMin,
        from: b.zBase, to: r3(st.zL), seq: [{ clip: 'walk', loop: true, rootMotion: 'route(): along the walk line, z tread to tread, 0.727 m/s', request: 'stairWalk' }],
        exact: { up: [{ clip: 'boardDown', reverse: true, opts: { railZ: r3(st.riser) }, repeat: st.n }] }, fits: st.riser <= CHAR92.spiral.riserMax + 1e-9 && st.going >= CHAR92.spiral.goingMin - 1e-9 && st.w >= CHAR92.spiral.widthMin - 1e-9 && st.headroomMin >= CHAR92.headroom - 1e-9 && st.doorClear >= 0 }); }
    const Ld = st.ladder, h = Ld.hatch, hw = (h.a1 - h.a0) * (h.r0 + h.r1) / 2, hd = h.r1 - h.r0;
    out.push({ id: 'ladder', kind: 'ladder', verb: 'climbUp', also: ['climbDown'], clip: 'ladderDown', opts: { rung: CHAR92.rung }, rungs: Ld.rungs.length, rung: CHAR92.rung, from: r3(Ld.z0), to: r3(Ld.z1), standoff: CHAR92.standoff, hatch: [r2(hw), r2(hd)],
      seq: { up: [{ clip: 'ladderDown', loop: true, reverse: true, rootMotion: { d: [0, 0, 1], rate: CHAR92.ladderRate }, until: 'the feet at ' + r3(b.zDeck - CHAR92.topOut) }, { clip: 'board', once: true, opts: { railZ: CHAR92.topOut }, advance: CHAR92.topAdvance, request: 'ladderOff' }] },
      fits: fit('rung', CHAR92.rung) && fit('rail', CHAR92.topOut) && hw >= CHAR92.footprintMax[0] + 0.12 && hd >= CHAR92.footprintMax[1] + 0.12 });
    out.push({ id: 'lamp', kind: 'fixture', verb: 'trimLamp', clip: 'reach', fixture: 'rest', fixtureZ: 0.95, opts: { lift: 0.95 }, standZ: r3(b.lens.standZ), step: b.lens.step ? 0.3 : 0, fits: fit('rest', 0.95), note: b.lens.step ? 'onto the service step (0.30): boardDown reversed' : '' });
    out.push({ id: 'clockwork', kind: 'fixture', verb: 'windClock', clip: 'haul', fixtureZ: 0.55, standZ: r3(b.zDeck), request: 'crank', fits: true });
    out.push({ id: 'glass', kind: 'fixture', verb: 'cleanGlass', clip: 'reach', fixture: 'rest', fixtureZ: 0.95, opts: { lift: 0.95 }, standZ: r3(b.zDeck), request: 'wipe', fits: fit('rest', 0.95) });
    const ldw = Math.min(0.82, 2 * b.rLan * Math.sin(Math.PI / b.lanN) * 0.92); out.push({ id: 'lantern.door', kind: 'door', verb: 'toGallery', clip: 'walk', clearWidth: r2(ldw), clearHeight: 2.0, fits: ldw >= CHAR92.clearDoor && b.lanH >= 2.1 });
    out.push({ id: 'gallery', kind: 'look', verb: 'watch', clip: 'idle', standZ: r3(b.zDeck), railH: b.railH, width: r2(b.rGal - b.rLan), fits: b.rGal - b.rLan >= 0.6 });
    if (b.ladder) out.push({ id: 'ladder.outside', kind: 'ladder', verb: 'climbUp', clip: 'ladderDown', opts: { rung: CHAR92.rung }, from: r3(b.zBase), to: r3(b.zTop), fits: true, note: 'service ladder to the gallery' });
    return { character: CHAR92, stations: out, requests: REQUESTS }; }
  function checks(o) { const b = resolve(o || {}), st = b.stair, rows = [], row = (id, ok, v) => rows.push({ id, ok: !!ok, v });
    if (st.kind === 'helix') { row('spiral', st.riser <= 0.19 + 1e-9 && st.going >= 0.26 - 1e-9 && st.w >= 0.64 - 1e-9, st.n + ' risers of ' + r3(st.riser) + ' · going ' + r3(st.going) + ' on the walk line · ' + r2(st.w) + ' wide · ' + st.turns + ' turns · ' + st.pitchDeg + '°');
      row('headroom', st.headroomMin >= 2.0, 'least clear height over a tread ' + st.headroomMin + ' m · every turn rises ≥ ' + st.rptMin + ' m'); row('door.return', st.doorClear >= 0, 'the first turn passes ' + st.doorClear + ' m clear over the door head' + (b.grewB ? ' (base widened ' + b.grewB + ' m to clear it)' : ''));
      row('landing', true, 'the spiral lands at ' + r2(st.zL) + ' m' + (st.zL < b.zTop - 2.31 ? ' (the shaft narrows: a ' + r2(b.zDeck - st.zL) + ' m ladder carries on)' : ', 2.3 m under the lantern floor')); }
    else row('spiral', true, 'no spiral: the ' + r2(b.hT) + ' m shaft is climbed by its ladder');
    const sl = stations(o).stations; for (const q of sl) if (q.fits === false) row('station.' + q.id, false, q.verb + ' does not fit every creator body');
    row('stations', sl.every(q => q.fits !== false), sl.length + ' stations · ' + sl.filter(q => q.fits !== false).length + ' fit every creator body');
    return { ok: rows.every(r => r.ok), rows }; }
  function gameplay(o) { const b = resolve(o || {}), st = b.stair, S2 = stations(o), oy = b.oy, R = route(o);
    return { schema: 'hidden-harbours/light-station-gameplay@1', rig: 'lighthouseRig2.js', exportSymbol: 'LightStation2', revision: '2.0', generated: 'LightStation2.gameplay() off the live model — regenerate, do not edit',
      frame: { units: 'metres', axes: '+x right of the door, +y away from it (the door is on −y), +z up from the ground', origin: 'the ground under the tower (the pivot)', scale_px_per_m: PX, elev_deg: 40, dirs: 8 },
      build: { family: b.family, size: b.size, hT: r2(b.hT), rB: r2(b.rB), rT: r2(b.rT), zBase: r2(b.zBase), zDeck: r2(b.zDeck), apex: r2(b.zApex), focal: r2(b.focal), char: b.char, grewB: b.grewB },
      PLACEMENT: placement(o), WALK: [{ id: 'ground', z: r3(b.zBase), disc: [0, r3(oy), r3(b.rIn(b.zBase))] }].concat(st.kind === 'helix' ? [{ id: 'spiral', treads: st.treads.map(t => [r3(t.a0), r3(t.a1), r3(t.r0), r3(t.r1), r3(t.z)]), center: [0, r3(oy)] }, { id: 'landing', z: r3(st.zL), sector: [r3(st.landing.a0), r3(st.landing.a1), r3(st.landing.r0), r3(st.landing.r1)] }] : [])
        .concat([{ id: 'lantern', z: r3(b.zDeck), disc: [0, r3(oy), r3(b.rLan - 0.1)], holes: [{ hatch: [r3(st.ladder.hatch.a0), r3(st.ladder.hatch.a1), r3(st.ladder.hatch.r0), r3(st.ladder.hatch.r1)] }, { lens: r3(b.lens.rl + (b.lens.step ? 0.6 : 0.1)) }] }, { id: 'gallery', z: r3(b.zDeck), ring: [r3(b.rLan + 0.05), r3(b.rGal - 0.12)] }]),
      ROUTE: R.pts.map(p => ({ seg: p.seg, clip: p.clip, m: [r3(p.x), r3(p.y), r3(p.z)], yaw: r2(p.yaw), t: r2(p.t) })), STATIONS: S2.stations,
      CUTAWAY: { mode: 'cutaway:"section" with climb (0–1 along ROUTE) or standZ', sill: 0.9, head: 1.95, step: st.kind === 'helix' ? r3(st.riser) + ' m on the spiral, ' + CHAR92.rung + ' on the ladder' : CHAR92.rung + ' on the ladder', note: 'walls toward the camera stand to feet + sill, walls behind to feet + head, everything over the head goes; the cut face is capped' },
      LIGHT: { char: b.char, colour: CHARS[b.char].col, focal_m: r2(b.focal), note: 'lamp(o, t, dir) gives the panels, beams and flash; the glow and beams are the engine’s overlay' },
      CHAR: CHAR92, CHAR_REQUESTS: REQUESTS }; }

  root.LightStation2 = { pass: 2, revision: '2.0', PX, DIRS: 8, order: NAMES, FAMILIES, PRESETS, DAYMARKS, CLADS, LANTERNS, CAPS, GALLERIES, PLINTHS, BODY, MARK, TRIM, STONE, STEEL, CHARS, SCHEDULES, CHAR: CHAR92, REQUESTS,
    expand, resolve, cutOf, route, posAt, frame, relight, castShadow, shadeAt, contact, view, lamp, lensPixels, paintLens, lightsOn, project, anchors, placement, stations, checks, gameplay, cropOf, tex, makeMats };
})(typeof globalThis !== 'undefined' ? globalThis : window);
