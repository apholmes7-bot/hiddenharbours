/* NINE MILE CREEK, KEY SCENES PART 2C (hh-nmc-key-scenes/fuel-stop/, 2026-09-30): the wharf return's copy with one change, call 7 as
   leaned (ruled 09-30): the store's goods board reads "COFFEE  ICE  BAIT" (the groceries go to the plaza's grocery, the bait to the rivers
   and the wharf). The three name boards stay blank (the name is open). Same file name, same global (FuelStop).
   The wharf return's header follows. */
/* NINE MILE CREEK COPY (hh-nmc-wharf-return/fuel-stop/, 2026-09-28, keyscene.nmc_fuel_stop). The owner placed pass 6's stop at
   Nine Mile Creek and left its name open (09-28), so the station's three name boards draw blank here: BRAND below. The
   store's "GROCERY  COFFEE  ICE" board stays (the General Store folds into it). Everything else is pass 6's rig as it stands,
   same file name, same global (FuelStop). Pass 6's own file (hh-st-peters-key-scenes-return/pass6/) is untouched.
   St Peters pass 6: THE BAR ROAD STOP (globalThis.FuelStop). The fuel stop at the mainland head of the bar road, built the
   way HouseIso and Outbuilding build a house: one parametric model per option set, drawn through the fixed camera
   (orthographic, 40 deg, looking north, 32 px = 1 m) on CoastalPass.light, relit from any WeatherSky.at() sky. No dither, no
   keyline. It replaces the IsoSolid forecourt (Art/gasStationRig.js, baked upper-left key) for the St Peters scenes; the
   grade table and the prices are the old rig's, read by reference when StationIso is loaded.

   Authored in plan metres as the scene stands it (e east, n north, z up, facing 4: the show face and the door to the
   south). Model coordinates are the house rigs' own (+Y the show face): P(e, n, z) = [-e, -n, z].

   NEON. CoastalPass lights every emitter with its warm ramp. Signs here are not warm, so an emitter named neon:<colour>,
   led:<colour> or glow:<colour> is lit as its material by the rig pass and then recoloured by FuelStop.relight from its own
   ramp (NEON): a neon letter or tube is a pixel whose structure band is +1 (the font is a texture on the sign face, so every
   letter pixel is exact at 3, 2 or 1 px per font pixel), its halo the two pixels round it. frame().neonPx lists the lit
   pixels, so a scene can bloom them onto the ground and the sky round the sign.

   SECTION. cutaway:'section' is the house rigs' sill cut: walls facing the camera drop to cutH above the floor (default
   0.9), the rest stand to the eave, the roof, parapet, sign band and entry canopy come off, every cut takes a pale cap,
   partitions are cut at the same height, the door leaf goes and its threshold stays. The store is furnished only in the
   section (a sales floor you can read: coolers, gondolas, coffee bar, checkout and back-bar, ATM, freezer, stockroom with
   the service door, washrooms). deck:'cut' does the same for a canopy: the deck comes off and the perimeter beams with their
   neon stay, so every pump reads.

   Kinds: store . canopy (three islands, N-S lanes) . truck (the semi lane: master + satellite high-flow and DEF, 5.5 m
   clear) . pylon (brand, prices, OPEN 24/7) . iceBox . propane . air . vacuum . dumpster . bench . firewood . lampPole .
   barSign . stopSign . picnic . wheelStop . bin

   FuelStop.frame(kind, dir, o)  -> CoastalPass frame, canvas fitted to the model, pivot fr.C.ox/oy; fr.neonPx
   FuelStop.relight(fr, sky, {need})  -> RGBA; FuelStop.levels(need); castShadow; view; stations(kind, o); footprint(kind) */
(function (root) {
  'use strict';
  const BRAND = { store: '', canopy: '', pylon: '', words: { store: 'words.nmc_station_store', canopy: 'words.nmc_station_canopy', pylon: 'words.nmc_station_pylon' } };   // the name is open: blank boards
  const S = 32, DEG = Math.PI / 180, ELEV = 40, CE40 = Math.cos(40 * DEG);
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const md = (a, m) => a - Math.floor(a / m) * m;
  const hsh = (a, b, s) => { let h = Math.imul(a | 0, 374761393) + Math.imul(b | 0, 668265263) + Math.imul(s | 0, 1274126177) | 0; h ^= h >>> 13; h = Math.imul(h, 1103515245) | 0; h ^= h >>> 16; return (h >>> 0) / 4294967296; };
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r, g, b) => '#' + [r, g, b].map(n => clamp(Math.round(n), 0, 255).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2r(a), B = h2r(b); return r2h(A[0] + (B[0] - A[0]) * t, A[1] + (B[1] - A[1]) * t, A[2] + (B[2] - A[2]) * t); };

  // ---- ramps: the houses' and the yard's where they exist (HouseIso, Outbuilding), the forecourt's own beside them ----------
  const RAMP = {
    body: ['#4a524f', '#5d6662', '#727b76', '#88918b', '#a0a8a1', '#b8bfb7'], trim: ['#9aa09a', '#b4b8b0', '#ccd0c7', '#e0e2da', '#eef0e8', '#f8f9f2'],
    white: ['#8c928c', '#a6aaa2', '#bfc2b9', '#d5d8cf', '#e7e9e0', '#f3f4ec'], band: ['#461914', '#5e231b', '#772e24', '#903b2e', '#a74b3b', '#bc5e4c'],
    sign: ['#141619', '#1c1f23', '#262a2f', '#30353b', '#e8e2d6', '#fbf8f1'], signG: ['#0d2a1c', '#133a27', '#1b4d34', '#26643f', '#dfe8e0', '#f3f7f2'],
    signR: ['#3c0b0d', '#5a1013', '#7d171a', '#a01f22', '#f1e6e2', '#fbf6f3'], roof: ['#3a3d3f', '#474a4c', '#56595a', '#666969', '#787a79', '#8b8d8b'],
    deck: ['#6f7478', '#878c90', '#a0a5a8', '#b8bcbe', '#cdd0d1', '#dfe1e1'], enamel: ['#7c8286', '#959b9e', '#afb4b6', '#c8cccd', '#dcdfe0', '#eceeee'],
    glass: ['#27373d', '#33474d', '#40585f', '#50696f', '#62797e', '#7a9095'], glassHi: ['#6f8b90', '#86a2a6', '#a1bcbf', '#bcd4d6', '#cfe6e8', '#e2f2f3'],
    frame: ['#1f1c19', '#2a2622', '#36312c', '#433d37', '#524b44', '#625a52'], conc: ['#4f4f4a', '#63635d', '#787871', '#8d8d85', '#a3a39a', '#b8b8ae'],
    yel: ['#5a4610', '#7a5f16', '#9d7b1f', '#c09a2c', '#d9b441', '#ead066'], steel: ['#565b5f', '#6d7276', '#868b8f', '#a0a5a8', '#bbbfc1', '#d6d9da'],
    dark: ['#0e1114', '#151a1e', '#1d2429', '#252d33', '#2e373e', '#38434b'], rubber: ['#0f1011', '#17191a', '#202224', '#2a2d2f', '#35393b', '#414547'],
    red: ['#4a0f10', '#6b1618', '#8f2022', '#b02b2c', '#c9403d', '#dc5c55'], blue: ['#14283d', '#1c3753', '#27496b', '#335d84', '#44739d', '#5a8ab4'],
    amber: ['#5a3408', '#7c4a0c', '#a06414', '#c2801f', '#d99d38', '#e8b85a'], ivory: ['#575a52', '#787b70', '#9a9d8f', '#bcbfaf', '#dcdfcd', '#ecefe0'],
    wood: ['#3f3a33', '#514a40', '#645b4f', '#786e60', '#8c8172', '#a19585'], cord: ['#5a3f22', '#71512d', '#8a653a', '#a37b4b', '#ba925f', '#cda977'],
    bark: ['#2c241c', '#3a2f24', '#4a3c2e', '#5b4a39', '#6d5a46', '#806b55'], green: ['#1f3328', '#294234', '#345242', '#416452', '#517864', '#648c77'],
    tile: ['#6d6a62', '#838077', '#99968c', '#aeab9f', '#c2bfb2', '#d4d1c4'], cap: ['#8e8778', '#a79f8e', '#bdb5a2', '#d0c8b4', '#dfd8c5', '#ebe5d4'],
    shelf: ['#5d605f', '#747776', '#8b8e8c', '#a2a5a2', '#b8bbb7', '#cdd0cb'], tube: ['#6e5a5d', '#8a7478', '#a68f93', '#c2abae', '#dccacc', '#efe4e5'],
    stone: ['#3b3d3a', '#4c4f4a', '#5f625b', '#74776d', '#8a8d81', '#a1a396'], soil: ['#241a12', '#31241a', '#3f2f22', '#4e3b2b', '#5e4936', '#6f5843'],
    prodR: ['#5a1a16', '#7a241d', '#9a3126', '#b8432f', '#cc5b3e', '#dc7852'], prodB: ['#1b2a4a', '#243a63', '#2f4c7d', '#3d6096', '#5176ad', '#6a8fc2'],
    prodY: ['#6e5316', '#8c6b1f', '#a9852c', '#c49f3e', '#d9b85a', '#e8cd7e'], prodG: ['#1f3a2c', '#29493a', '#35594a', '#436b5a', '#56806c', '#6d9582'],
  };
  const NEON = {
    red: ['#3a0610', '#7a0c1e', '#c8142e', '#ff3348', '#ff8a96', '#ffe3e6'], teal: ['#04302f', '#07615d', '#0fa39a', '#2ee6d6', '#8ff7ee', '#e6fffb'],
    blue: ['#071a3d', '#0c347a', '#1a5ad1', '#3f8cff', '#9cc4ff', '#e8f1ff'], amber: ['#3a1e04', '#7a4208', '#c86f10', '#ffa228', '#ffd08a', '#fff3dd'],
    white: ['#1c2530', '#3b4a5c', '#7d8ea3', '#bfcde0', '#e4ecf7', '#ffffff'], cool: ['#1f2a33', '#40525f', '#7f98a8', '#bcd2de', '#e2eef4', '#fbfdff'],
    led: ['#2a0404', '#5e0808', '#a31010', '#f02020', '#ff7070', '#ffd0d0'],
  };
  const NEONRGB = {}; for (const k in NEON) NEONRGB[k] = NEON[k].map(h2r);

  // ---- the grade table (the old rig's, by reference) and the board's prices ----------------------------------------------------
  const GRADES = { reg: { label: 'REGULAR', short: 'REG', mat: 'red' }, sup: { label: 'SUPREME', short: 'SUP', mat: 'blue' }, dsl: { label: 'DIESEL', short: 'DSL', mat: 'amber' },
    kero: { label: 'STOVE OIL', short: 'KERO', mat: 'ivory' }, def: { label: 'DIESEL EXHAUST FLUID', short: 'DEF', mat: 'blue' } };
  const PRICE = { reg: 172.9, sup: 199.9, dsl: 189.4, kero: 154.9 };
  if (root.StationIso && root.StationIso.PRICE) { const P0 = root.StationIso.PRICE; if (P0.gas) PRICE.reg = P0.gas; if (P0.diesel) PRICE.dsl = P0.diesel; if (P0.kero) PRICE.kero = P0.kero; }

  // ---- a 5 x 7 font, laid as a texture on a sign face: one font pixel is exactly 3, 2 or 1 screen px on a south face ----------
  const FONT = {}, G = (c, rows) => { FONT[c] = rows.map(r => parseInt(r, 2)); };
  G('A', ['01110', '10001', '10001', '11111', '10001', '10001', '10001']); G('B', ['11110', '10001', '10001', '11110', '10001', '10001', '11110']);
  G('C', ['01110', '10001', '10000', '10000', '10000', '10001', '01110']); G('D', ['11110', '10001', '10001', '10001', '10001', '10001', '11110']);
  G('E', ['11111', '10000', '10000', '11110', '10000', '10000', '11111']); G('F', ['11111', '10000', '10000', '11110', '10000', '10000', '10000']);
  G('G', ['01110', '10001', '10000', '10111', '10001', '10001', '01111']); G('H', ['10001', '10001', '10001', '11111', '10001', '10001', '10001']);
  G('I', ['01110', '00100', '00100', '00100', '00100', '00100', '01110']); G('K', ['10001', '10010', '10100', '11000', '10100', '10010', '10001']);
  G('L', ['10000', '10000', '10000', '10000', '10000', '10000', '11111']); G('M', ['10001', '11011', '10101', '10101', '10001', '10001', '10001']);
  G('N', ['10001', '11001', '10101', '10011', '10001', '10001', '10001']); G('O', ['01110', '10001', '10001', '10001', '10001', '10001', '01110']);
  G('P', ['11110', '10001', '10001', '11110', '10000', '10000', '10000']); G('R', ['11110', '10001', '10001', '11110', '10100', '10010', '10001']);
  G('S', ['01111', '10000', '10000', '01110', '00001', '00001', '11110']); G('T', ['11111', '00100', '00100', '00100', '00100', '00100', '00100']);
  G('U', ['10001', '10001', '10001', '10001', '10001', '10001', '01110']); G('V', ['10001', '10001', '10001', '10001', '10001', '01010', '00100']);
  G('W', ['10001', '10001', '10001', '10101', '10101', '10101', '01010']); G('Y', ['10001', '10001', '01010', '00100', '00100', '00100', '00100']);
  G('X', ['10001', '10001', '01010', '00100', '01010', '10001', '10001']);
  G('0', ['01110', '10001', '10011', '10101', '11001', '10001', '01110']); G('1', ['00100', '01100', '00100', '00100', '00100', '00100', '01110']);
  G('2', ['01110', '10001', '00001', '00010', '00100', '01000', '11111']); G('3', ['11111', '00010', '00100', '00010', '00001', '10001', '01110']);
  G('4', ['00010', '00110', '01010', '10010', '11111', '00010', '00010']); G('5', ['11111', '10000', '11110', '00001', '00001', '10001', '01110']);
  G('6', ['00110', '01000', '10000', '11110', '10001', '10001', '01110']); G('7', ['11111', '00001', '00010', '00100', '01000', '01000', '01000']);
  G('8', ['01110', '10001', '10001', '01110', '10001', '10001', '01110']); G('9', ['01110', '10001', '10001', '01111', '00001', '00010', '01100']);
  G('/', ['00001', '00010', '00010', '00100', '01000', '01000', '10000']); G('.', ['00000', '00000', '00000', '00000', '00000', '01100', '01100']);
  G('-', ['00000', '00000', '00000', '11111', '00000', '00000', '00000']); G('$', ['00100', '01111', '10100', '01110', '00101', '11110', '00100']);
  G('>', ['01000', '00100', '00010', '00001', '00010', '00100', '01000']); G(' ', ['00000', '00000', '00000', '00000', '00000', '00000', '00000']);
  /* text on a face whose uv is (e along it, z up it): +1 on a letter pixel, -1 between. uC the text's centre, v0 its foot. */
  function textTex(str, uC, v0, scale) {
    const cw = scale / S, ch = scale / (S * CE40), n = str.length, us = uC - (n * 6 - 1) * cw / 2;
    return (u, v) => { const c = Math.floor((u - us) / cw), r = 6 - Math.floor((v - v0) / ch); if (c < 0 || r < 0 || r > 6 || c >= n * 6) return -1;
      const k = (c / 6) | 0, cc = c - k * 6; if (cc === 5) return -1; const g = FONT[str[k]]; return g && ((g[r] >> (4 - cc)) & 1) ? 1 : -1; }; }
  const textW = (str, scale) => (str.length * 6 - 1) * scale / S, textH = (scale) => 7 * scale / (S * CE40);

  const TEX = {
    clap: (u, v) => md(v, 0.2) < 0.034 ? -1 : 0,
    seam: (u, v) => { const j = md(u, 0.45); return j < 0.03 ? -1 : j < 0.06 ? 1 : 0; },
    tile: (u, v) => ((Math.floor(u / 0.6) + Math.floor(v / 0.6)) & 1) ? -1 : 0,
    deck: (u, v) => md(u, 3.15) < 0.05 || md(v, 2.0) < 0.04 ? -1 : 0,
    membrane: (u, v) => md(u, 1.2) < 0.03 ? -1 : 0,
    grille: (u, v) => md(u, 0.1) < 0.025 || md(v, 0.1) < 0.025 ? 0 : -3,
    stripes: (u, v) => md(u + v, 0.5) < 0.25 ? 0 : -3,
    boards: (u, v) => { const i = Math.floor(u / 0.21); if (md(u, 0.21) < 0.018) return -1; return hsh(i, 3, 11) < 0.22 ? -1 : 0; },
    rubble: (u, v) => { const row = Math.floor(v / 0.16), uu = u + (row & 1) * 0.17; if (md(v, 0.16) < 0.03 || md(uu, 0.34) < 0.03) return -2; return hsh(Math.floor(uu / 0.34), row, 23) < 0.3 ? -1 : 0; },
    endgrain: (u, v) => { const c = 0.14, i = Math.floor(u / c), j = Math.floor((v + (i & 1) * 0.07) / c), du = md(u, c) / c - 0.5, dv = md(v + (i & 1) * 0.07, c) / c - 0.5;
      if (du * du + dv * dv > 0.2) return -2; const k = hsh(i, j, 29); return k < 0.25 ? -1 : k > 0.8 ? 1 : 0; },
    facings: (u, v) => md(u, 0.16) < 0.02 || md(v, 0.34) < 0.04 ? -2 : (hsh(Math.floor(u / 0.16), Math.floor(v / 0.34), 41) < 0.3 ? 1 : 0),
    doors: (u, v) => md(u, 1.06) < 0.06 ? -3 : 0,
  };

  // ---- builders, in plan metres (e, n, z) ----------------------------------------------------------------------------------------
  const P = (e, n, z) => [-e, -n, z];
  function F(C, pts, mat, o) { o = o || {}; const f = { v: pts.map(p => P(p[0], p[1], p[2])), mat, b: o.b || 0, db: o.db || 0, uv: o.uv || null, tex: o.tex || null }; if (o.tag) f.tag = o.tag; if (o.em) f.em = o.em; C.out.push(f); return f; }
  /* a box; o.s / o.nf / o.ef / o.wf / o.t override one face ({mat, tex, em, b}); uv: south (e, z), north (-e, z), east (n, z), west (-n, z), top (e, n) */
  function box(C, e0, e1, n0, n1, z0, z1, mat, o) {
    o = o || {}; const base = { mat, tag: o.tag, b: o.b || 0, em: o.em, tex: o.tex || null };
    const put = (pts, uv, ov) => { const f = Object.assign({}, base, ov || {}); F(C, pts, f.mat, { tag: f.tag, b: f.b, em: f.em, tex: f.tex, uv: f.tex ? uv : null }); };
    if (!o.noS) put([[e0, n0, z0], [e1, n0, z0], [e1, n0, z1], [e0, n0, z1]], [[e0, z0], [e1, z0], [e1, z1], [e0, z1]], o.s);
    if (!o.noN) put([[e1, n1, z0], [e0, n1, z0], [e0, n1, z1], [e1, n1, z1]], [[-e1, z0], [-e0, z0], [-e0, z1], [-e1, z1]], o.nf);
    if (!o.noE) put([[e1, n0, z0], [e1, n1, z0], [e1, n1, z1], [e1, n0, z1]], [[n0, z0], [n1, z0], [n1, z1], [n0, z1]], o.ef);
    if (!o.noW) put([[e0, n1, z0], [e0, n0, z0], [e0, n0, z1], [e0, n1, z1]], [[-n1, z0], [-n0, z0], [-n0, z1], [-n1, z1]], o.wf);
    if (!o.noTop) put([[e0, n0, z1], [e1, n0, z1], [e1, n1, z1], [e0, n1, z1]], [[e0, n0], [e1, n0], [e1, n1], [e0, n1]], Object.assign({ mat: o.top || mat, tex: o.topTex || null, b: o.topB != null ? o.topB : (o.b || 0) }, o.t || {}));
    if (o.bot) put([[e0, n1, z0], [e1, n1, z0], [e1, n0, z0], [e0, n0, z0]], null, { tex: null }); }
  /* a flat sign plane facing south at n, e0..e1, z0..z1, a hair proud (text laid on it) */
  function plate(C, e0, e1, n, z0, z1, mat, o) { o = o || {}; return F(C, [[e0, n, z0], [e1, n, z0], [e1, n, z1], [e0, n, z1]], mat, { tag: o.tag, em: o.em, b: o.b || 0, tex: o.tex || null, uv: o.tex ? [[e0, z0], [e1, z0], [e1, z1], [e0, z1]] : null }); }
  const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]], crs = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const nrm = (a) => { const m = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / m, a[1] / m, a[2] / m]; };
  function stick(C, a, b, t, mat, o) { o = o || {}; const u = nrm(sub(b, a)), p = Math.abs(u[2]) < 0.9 ? [0, 0, 1] : [1, 0, 0], v1 = nrm(crs(u, p)), v2 = crs(u, v1), h = t / 2;
    const c = (Q, s1, s2) => [Q[0] + (v1[0] * s1 + v2[0] * s2) * h, Q[1] + (v1[1] * s1 + v2[1] * s2) * h, Q[2] + (v1[2] * s1 + v2[2] * s2) * h];
    const A = [c(a, -1, -1), c(a, 1, -1), c(a, 1, 1), c(a, -1, 1)], B2 = [c(b, -1, -1), c(b, 1, -1), c(b, 1, 1), c(b, -1, 1)];
    for (let k = 0; k < 4; k++) { const k2 = (k + 1) % 4; F(C, [A[k], A[k2], B2[k2], B2[k]], mat, { tag: o.tag, b: o.b || 0, em: o.em }); }
    F(C, B2, mat, { tag: o.tag, b: o.b || 0, em: o.em }); F(C, A, mat, { tag: o.tag, b: o.b || 0, em: o.em }); }
  function cyl(C, e, n, r, z0, z1, k, mat, o) { o = o || {}; const Q = []; for (let i = 0; i < k; i++) { const a = (i + 0.5) / k * 2 * Math.PI; Q.push([e + Math.cos(a) * r, n + Math.sin(a) * r]); }
    const pr = 2 * Math.PI * r / k;
    for (let i = 0; i < k; i++) { const a = Q[i], b = Q[(i + 1) % k]; F(C, [[a[0], a[1], z0], [b[0], b[1], z0], [b[0], b[1], z1], [a[0], a[1], z1]], mat, { tag: o.tag, b: o.b || 0, em: o.em, tex: o.tex || null, uv: o.tex ? [[i * pr, z0], [(i + 1) * pr, z0], [(i + 1) * pr, z1], [i * pr, z1]] : null }); }
    if (!o.noTop) F(C, Q.map(p => [p[0], p[1], z1]), o.top || mat, { tag: o.tag, b: o.topB != null ? o.topB : (o.b || 0) }); }

  // ---- walls with openings and the sill cut (the outbuilding rig's, in plan metres) --------------------------------------------
  function clipAx(Q, ax, v, keepGreater) { const out = []; for (let i = 0; i < Q.length; i++) { const a = Q[i], b = Q[(i + 1) % Q.length], ia = keepGreater ? a[ax] >= v - 1e-9 : a[ax] <= v + 1e-9, ib = keepGreater ? b[ax] >= v - 1e-9 : b[ax] <= v + 1e-9;
    if (ia) out.push(a); if (ia !== ib) { const t = (v - a[ax]) / (b[ax] - a[ax]), q = [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]; q[ax] = v; out.push(q); } } return out; }
  const area2 = (Q) => { let s = 0; for (let i = 0; i < Q.length; i++) { const a = Q[i], b = Q[(i + 1) % Q.length]; s += a[0] * b[1] - b[0] * a[1]; } return Math.abs(s) / 2; };
  function pieces(outline, holes) {
    const us = [...new Set(holes.flatMap(h => [h.u0, h.u1]))].sort((a, b) => a - b), umin = Math.min(...outline.map(p => p[0])), umax = Math.max(...outline.map(p => p[0]));
    const cuts = [umin, ...us.filter(u => u > umin + 1e-6 && u < umax - 1e-6), umax], res = [];
    for (let i = 0; i + 1 < cuts.length; i++) { const ua = cuts[i], ub = cuts[i + 1]; const Q = clipAx(clipAx(outline, 0, ua, true), 0, ub, false); if (Q.length < 3 || area2(Q) < 1e-6) continue;
      const hs = holes.filter(h => h.u0 <= ua + 1e-6 && h.u1 >= ub - 1e-6).sort((a, b) => a.z0 - b.z0);
      if (!hs.length) { res.push(Q); continue; }
      let zlo = -1e9; for (const h of hs) { const R = clipAx(clipAx(Q, 1, zlo, true), 1, h.z0, false); if (R.length >= 3 && area2(R) > 1e-6) res.push(R); zlo = h.z1; }
      const R = clipAx(Q, 1, zlo, true); if (R.length >= 3 && area2(R) > 1e-6) res.push(R); }
    return res; }
  /* the world-north share of a plan normal at the frame's facing: < -0.2 faces the camera (it drops in a section) */
  const faceN = (C, o) => { const a = C.dir * Math.PI / 4, nx = -o[0], ny = -o[1]; return nx * Math.sin(a) + ny * Math.cos(a); };
  const dropped = (C, o) => C.s.cut && faceN(C, o) < -0.2;
  /* w: {a:[e,n], b:[e,n], out:[e,n] outward, z0, top, eave, holes:[{u0,u1,z0,z1,walk}], mat, tex, inMat, t, tag} */
  function wall(C, w) {
    const t = w.t || 0.2, holes = w.holes || [], L = Math.hypot(w.b[0] - w.a[0], w.b[1] - w.a[1]), d = [(w.b[0] - w.a[0]) / L, (w.b[1] - w.a[1]) / L], no = w.out, z0 = w.z0 || 0;
    const M = (u, z, off) => [w.a[0] + d[0] * u + no[0] * off, w.a[1] + d[1] * u + no[1] * off, z];
    let outline = [[0, z0], [L, z0], [L, w.top], [0, w.top]], capZ = null; const drop = dropped(C, no);
    if (C.s.cut) { capZ = drop ? C.floor + C.s.cutH : w.eave; outline = clipAx(outline, 1, capZ, false); }
    for (const Q of pieces(outline, holes)) {
      F(C, Q.map(p => M(p[0], p[1], 0)), w.mat || 'body', { uv: Q.map(p => [p[0], p[1]]), tex: w.tex, tag: w.tag });
      F(C, Q.map(p => M(p[0], p[1], -t)), w.inMat || 'white', { tag: 'wall.inside' }); }
    for (const h of holes) { const z1 = capZ != null ? Math.min(h.z1, capZ) : h.z1; if (z1 <= h.z0 + 1e-6) continue;
      const q = (u0, za, u1, zb) => F(C, [M(u0, za, 0), M(u1, zb, 0), M(u1, zb, -t), M(u0, za, -t)], 'white', { tag: w.tag, b: -1 });
      q(h.u0, h.z0, h.u0, z1); q(h.u1, h.z0, h.u1, z1); if (h.z0 > z0 + 0.01) q(h.u0, h.z0, h.u1, h.z0); if (capZ == null || h.z1 < capZ - 1e-6) q(h.u0, h.z1, h.u1, h.z1); }
    if (capZ != null) {
      for (let i = 0; i < outline.length; i++) { const a = outline[i], b = outline[(i + 1) % outline.length];
        if (Math.abs(a[0] - b[0]) < 1e-6) continue;
        if (Math.abs(a[1] - capZ) < 1e-6 && Math.abs(b[1] - capZ) < 1e-6) { let segs = [[Math.min(a[0], b[0]), Math.max(a[0], b[0])]];
          for (const h of holes) if (h.z0 < capZ - 1e-6 && h.z1 > capZ + 1e-6) segs = segs.flatMap(([s0, s1]) => (h.u1 <= s0 || h.u0 >= s1) ? [[s0, s1]] : [[s0, h.u0], [h.u1, s1]].filter(q => q[1] - q[0] > 1e-3));
          for (const [s0, s1] of segs) F(C, [M(s0, capZ, 0.012), M(s1, capZ, 0.012), M(s1, capZ, -t), M(s0, capZ, -t)], 'cap', { tag: 'cut.cap', b: 1 }); } } }
    return { M, L, drop, capZ, t };
  }
  function fbox(C, M, u0, u1, z0, z1, o0, o1, mat, o) { o = o || {};
    const c = [M(u0, z0, o0), M(u1, z0, o0), M(u1, z1, o0), M(u0, z1, o0), M(u0, z0, o1), M(u1, z0, o1), M(u1, z1, o1), M(u0, z1, o1)], q = { tag: o.tag, b: o.b || 0, em: o.em };
    F(C, [c[4], c[5], c[6], c[7]], mat, Object.assign({}, q, { tex: o.tex || null, uv: o.tex ? [[u0, z0], [u1, z0], [u1, z1], [u0, z1]] : null }));
    F(C, [c[0], c[1], c[5], c[4]], mat, q); F(C, [c[3], c[2], c[6], c[7]], o.topMat || mat, q); F(C, [c[0], c[3], c[7], c[4]], mat, q); F(C, [c[1], c[2], c[6], c[5]], mat, q); }
  /* shopfront glazing in an opening: glass on its own emitter, aluminium mullions every bay, a head and a sill rail */
  function glazing(C, Wf, h, o) { o = o || {}; if (Wf.capZ != null && h.z0 >= Wf.capZ - 1e-6) return; const zt = Wf.capZ != null ? Math.min(h.z1, Wf.capZ) : h.z1, em = o.em || 'win:' + (C.win++);
    F(C, [Wf.M(h.u0, h.z0, -0.05), Wf.M(h.u1, h.z0, -0.05), Wf.M(h.u1, zt, -0.05), Wf.M(h.u0, zt, -0.05)], 'glass', { tag: o.tag || 'show.glazing', em });
    if (zt > h.z0 + 0.5) F(C, [Wf.M(h.u0 + 0.2, zt - 0.35, -0.045), Wf.M(h.u0 + 0.75, zt - 0.35, -0.045), Wf.M(h.u0 + 0.45, zt - 0.08, -0.045)], 'glassHi', { tag: o.tag || 'show.glazing', em });
    const n = Math.max(1, Math.round((h.u1 - h.u0) / (o.bay || 1.55)));
    for (let k = 0; k <= n; k++) { const u = h.u0 + (h.u1 - h.u0) * k / n; fbox(C, Wf.M, u - 0.04, u + 0.04, h.z0, zt, -0.06, 0.02, 'frame', { tag: 'show.frame' }); }
    fbox(C, Wf.M, h.u0, h.u1, h.z0, h.z0 + 0.08, -0.06, 0.03, 'frame', { tag: 'show.frame' });
    if (Wf.capZ == null || h.z1 <= Wf.capZ) fbox(C, Wf.M, h.u0, h.u1, h.z1 - 0.08, h.z1, -0.06, 0.03, 'frame', { tag: 'show.frame' });
    if (o.transom && (Wf.capZ == null || o.transom < Wf.capZ)) fbox(C, Wf.M, h.u0, h.u1, o.transom - 0.04, o.transom + 0.04, -0.06, 0.02, 'frame', { tag: 'show.frame' }); }
  /* neon tube: a thin proud box whose face is the tube (band +1 = the lit core) */
  const tube = (C, e0, e1, n, z, col, o) => box(C, e0, e1, n - (o && o.d || 0.035), n, z - 0.03, z + 0.03, 'tube', { tag: 'neon.tube', em: 'neon:' + col, b: 1, noN: true });
  const tubeN = (C, e, n0, n1, z, col) => box(C, e - 0.02, e + 0.02, n0, n1, z - 0.03, z + 0.03, 'tube', { tag: 'neon.tube', em: 'neon:' + col, b: 1 });
  const lamp = (C, e, n, z, c, I, r) => C.lamps.push({ p: P(e, n, z), c, I, r });

  // ---- the kinds ----------------------------------------------------------------------------------------------------------------
  const B = {};
  /* THE STORE. 18 x 12 m, clapboard in harbour grey under a red standing-seam band, a glazed front with a bipart sliding door
     under an entry canopy, the name in neon on a raised sign, OPEN 24/7 in the west window, rooftop plant behind the parapet,
     the service door, the meter and the tank vents at the back. */
  B.store = function (C) {
    const s = C.s, hw = 9, hl = 6, fz = C.floor = 0.15, eave = 4.2, par = 4.6, t = 0.2;
    C.dims = { w: 2 * hw, d: 2 * hl, h: 5.6 };
    box(C, -hw - 0.06, hw + 0.06, -hl - 0.06, hl + 0.06, 0, fz, 'conc', { tag: 'wall.plinth', noTop: true });
    // the walls: the front (south) with its shopfront, the sides, the back with the service door
    const gW = { u0: 0.8, u1: 7.0, z0: 0.6, z1: 2.9, walk: false }, dr = { u0: 7.9, u1: 10.1, z0: fz, z1: 2.9 }, gE = { u0: 11.0, u1: 17.2, z0: 0.6, z1: 2.9, walk: false };
    const Wf = wall(C, { a: [-hw, -hl], b: [hw, -hl], out: [0, -1], z0: 0, top: par, eave, holes: [gW, dr, gE], tex: TEX.clap, t, tag: 'show.front' });
    const svc = { u0: 1.4, u1: 2.4, z0: fz, z1: 2.25 };
    const Wb = wall(C, { a: [hw, hl], b: [-hw, hl], out: [0, 1], z0: 0, top: par, eave, holes: [{ u0: 15.6, u1: 16.6, z0: fz, z1: 2.25 }], tex: TEX.clap, t, tag: 'wall.back' });
    const We = wall(C, { a: [hw, -hl], b: [hw, hl], out: [1, 0], z0: 0, top: par, eave, holes: [{ u0: 3.2, u1: 4.6, z0: 1.6, z1: 2.6, walk: false }], tex: TEX.clap, t, tag: 'wall.side' });
    const Ww = wall(C, { a: [-hw, hl], b: [-hw, -hl], out: [-1, 0], z0: 0, top: par, eave, holes: [{ u0: 7.4, u1: 8.8, z0: 1.6, z1: 2.6, walk: false }], tex: TEX.clap, t, tag: 'wall.side' });
    void svc;
    glazing(C, Wf, gW, { tag: 'show.glazing' }); glazing(C, Wf, gE, { tag: 'show.glazing' });
    glazing(C, We, { u0: 3.2, u1: 4.6, z0: 1.6, z1: 2.6 }, { bay: 0.7 }); glazing(C, Ww, { u0: 7.4, u1: 8.8, z0: 1.6, z1: 2.6 }, { bay: 0.7 });
    // corner boards and the frieze
    for (const [e, n] of [[-hw, -hl], [hw, -hl], [hw, hl], [-hw, hl]]) { const zt = s.cut ? (n < 0 ? fz + s.cutH : eave) : par; box(C, e - 0.1, e + 0.1, n - 0.1, n + 0.1, 0.02, zt, 'trim', { tag: 'wall.trim', noTop: !!s.cut }); }
    // the door: a bipart slider in an aluminium frame, a transom over it; in a section the leaves go and the threshold stays
    if (!Wf.drop) {
      const op = s.doorOpen * 1.02, zt = 2.45;
      for (const sg of [-1, 1]) { const a0 = sg < 0 ? -1.08 - op : 0.01 + op, a1 = a0 + 1.07;
        box(C, a0, a1, -hl + 0.02, -hl + 0.07, fz + 0.01, zt, 'glass', { tag: 'door.leaf', em: 'win:door', s: { mat: 'glass', em: 'win:door' } });
        for (const [b0, b1, c0, c1] of [[a0, a1, fz, fz + 0.12], [a0, a1, zt - 0.08, zt], [a0, a0 + 0.07, fz, zt], [a1 - 0.07, a1, fz, zt]]) box(C, b0, b1, -hl + 0.005, -hl + 0.075, c0, c1, 'frame', { tag: 'door.leaf' });
        box(C, sg < 0 ? a1 - 0.2 : a0 + 0.12, sg < 0 ? a1 - 0.12 : a0 + 0.2, -hl - 0.005, -hl + 0.005, 1.0, 1.2, 'steel', { tag: 'door.leaf' }); }
      F(C, [[-1.1, -hl - 0.04, zt], [1.1, -hl - 0.04, zt], [1.1, -hl - 0.04, 2.9], [-1.1, -hl - 0.04, 2.9]], 'glass', { tag: 'door.transom', em: 'win:door' });
      box(C, -1.12, 1.12, -hl - 0.07, -hl + 0.02, zt - 0.02, zt + 0.06, 'frame', { tag: 'door.frame' });
    } else F(C, [[-1.1, -hl + 0.03, fz + 0.005], [1.1, -hl + 0.03, fz + 0.005], [1.1, -hl - t, fz + 0.005], [-1.1, -hl - t, fz + 0.005]], 'frame', { tag: 'entry.threshold' });
    for (const e of [-1.18, 1.18]) box(C, e - 0.07, e + 0.07, -hl - 0.07, -hl + 0.02, fz, s.cut ? fz + s.cutH : 2.9, 'frame', { tag: 'door.frame', noTop: !!s.cut });
    // the entry canopy on two posts, its fascia red; OPEN 24/7 in the west window
    if (!s.cut) {
      box(C, -2.4, 2.4, -hl - 2.1, -hl, 3.0, 3.28, 'band', { tag: 'entry.canopy', tex: TEX.seam, top: 'roof', topTex: TEX.membrane });
      box(C, -2.44, 2.44, -hl - 2.14, -hl - 2.08, 2.98, 3.32, 'trim', { tag: 'entry.canopy', noN: true });
      for (const e of [-2.2, 2.2]) box(C, e - 0.07, e + 0.07, -hl - 1.97, -hl - 1.83, 0.125, 3.0, 'frame', { tag: 'entry.post' });
      for (const e of [-1.2, 0, 1.2]) box(C, e - 0.18, e + 0.18, -hl - 1.5, -hl - 1.2, 2.96, 3.0, 'enamel', { tag: 'entry.light', em: 'glow:white', b: 1 });
      lamp(C, 0, -hl - 1.2, 2.8, '#fff4e2', 0.8, 3.6);
      const e0 = -6.3, z0 = 1.62, sc = 2, w1 = textW('OPEN', sc), w2 = textW('24/7', sc);
      box(C, e0 - 0.12, e0 + w1 + w2 + 0.36, -hl - 0.03, -hl - 0.015, z0 - 0.12, z0 + textH(sc) + 0.12, 'sign', { tag: 'show.open', noN: true, s: { mat: 'sign', b: -1 } });
      plate(C, e0, e0 + w1, -hl - 0.036, z0, z0 + textH(sc), 'signR', { tag: 'show.open', em: 'neon:red', tex: textTex('OPEN', e0 + w1 / 2, z0, sc) });
      plate(C, e0 + w1 + 0.24, e0 + w1 + 0.24 + w2, -hl - 0.036, z0, z0 + textH(sc), 'sign', { tag: 'show.open', em: 'neon:blue', tex: textTex('24/7', e0 + w1 + 0.24 + w2 / 2, z0, sc) });
      for (const z of [z0 - 0.09, z0 + textH(sc) + 0.09]) tube(C, e0 - 0.1, e0 + w1 + w2 + 0.34, -hl - 0.036, z, 'blue', { d: 0.012 });
    } else for (const e of [-2.2, 2.2]) { box(C, e - 0.07, e + 0.07, -hl - 1.97, -hl - 1.83, 0.125, fz + s.cutH, 'frame', { tag: 'entry.post', noTop: true }); box(C, e - 0.075, e + 0.075, -hl - 1.975, -hl - 1.825, fz + s.cutH, fz + s.cutH + 0.01, 'cap', { tag: 'cut.cap' }); }
    // bollards before the glazing (the forecourt's rule: nothing drives into the front)
    for (const e of [-7.6, -4.4, 4.4, 7.6]) { cyl(C, e, -hl - 0.55, 0.1, 0.125, 1.05, 8, 'yel', { tag: 'show.bollard' }); cyl(C, e, -hl - 0.55, 0.104, 0.7, 0.8, 8, 'dark', { tag: 'show.bollard', noTop: true }); }
    if (!s.cut) {
      // the sign band, the raised name sign, the parapet coping, the roof and its plant
      fbox(C, Wf.M, 0, 2 * hw, 3.05, par, 0, 0.06, 'band', { tag: 'show.band', tex: TEX.seam });
      for (const [q, L] of [[We, 2 * hl], [Ww, 2 * hl], [Wb, 2 * hw]]) fbox(C, q.M, 0, L, 3.35, par, 0, 0.05, 'band', { tag: 'wall.band', tex: TEX.seam });
      box(C, -hw - 0.08, hw + 0.08, -hl - 0.08, -hl + 0.12, par, par + 0.08, 'trim', { tag: 'roof.coping' }); box(C, -hw - 0.08, hw + 0.08, hl - 0.12, hl + 0.08, par, par + 0.08, 'trim', { tag: 'roof.coping' });
      box(C, -hw - 0.08, -hw + 0.12, -hl, hl, par, par + 0.08, 'trim', { tag: 'roof.coping' }); box(C, hw - 0.12, hw + 0.08, -hl, hl, par, par + 0.08, 'trim', { tag: 'roof.coping' });
      F(C, [[-hw + t, -hl + t, eave], [hw - t, -hl + t, eave], [hw - t, hl - t, eave], [-hw + t, hl - t, eave]], 'roof', { tag: 'roof', tex: TEX.membrane, uv: [[-hw, -hl], [hw, -hl], [hw, hl], [-hw, hl]] });
      for (const [q, L] of [[Wf, 2 * hw], [Wb, 2 * hw], [We, 2 * hl], [Ww, 2 * hl]]) F(C, [q.M(0, eave, -t), q.M(L, eave, -t), q.M(L, par, -t), q.M(0, par, -t)], 'roof', { tag: 'roof.parapet', b: -1 });
      box(C, -5.2, -3.0, 1.2, 2.8, eave, eave + 1.25, 'steel', { tag: 'roof.plant', s: { mat: 'steel', tex: TEX.grille } }); cyl(C, -4.1, 2.0, 0.42, eave + 1.25, eave + 1.32, 10, 'dark', { tag: 'roof.plant' });
      box(C, 2.6, 4.2, 2.4, 3.6, eave, eave + 1.0, 'steel', { tag: 'roof.plant', s: { mat: 'steel', tex: TEX.grille } }); cyl(C, 6.4, 3.4, 0.28, eave, eave + 0.9, 8, 'steel', { tag: 'roof.plant' });
      box(C, 6.1, 6.7, 3.1, 3.7, eave + 0.9, eave + 1.0, 'steel', { tag: 'roof.plant' });
      const nS = -hl - 0.16, sz0 = 3.22, sz1 = 5.32, sc = 3, l1 = BRAND.store, l2 = 'COFFEE  ICE  BAIT', z1 = 4.34, z2 = 3.46;
      box(C, -4.75, 4.75, nS, -hl + 0.1, sz0, sz1, 'sign', { tag: 'show.sign', s: { mat: 'sign', b: -1 }, top: 'trim' });
      if (l1) plate(C, -4.5, 4.5, nS - 0.005, z1, z1 + textH(sc), 'signR', { tag: 'show.sign', em: 'neon:red', tex: textTex(l1, 0, z1, sc) });
      plate(C, -4.5, 4.5, nS - 0.005, z2, z2 + textH(2), 'sign', { tag: 'show.sign', em: 'neon:teal', tex: textTex(l2, 0, z2, 2) });
      for (const z of [sz0 + 0.08, sz1 - 0.08]) tube(C, -4.62, 4.62, nS - 0.005, z, 'red', { d: 0.03 });
      for (const e of [-4.62, 4.62]) box(C, e - 0.03, e + 0.03, nS - 0.035, nS - 0.005, sz0 + 0.08, sz1 - 0.08, 'tube', { tag: 'neon.tube', em: 'neon:red', b: 1, noN: true });
    }
    // round the back: the service door, the meter, the tank vents up the wall
    if (!Wb.drop || !s.cut) { box(C, -7.62, -6.58, hl - 0.02, hl + 0.05, fz, 2.25, 'steel', { tag: 'service.door', noN: false });
      box(C, 4.2, 4.6, hl, hl + 0.18, 1.2, 1.75, 'steel', { tag: 'wall.meter' }); }
    for (const [k, e] of [[0, 6.3], [1, 6.6], [2, 6.9]]) { const zt = s.cut ? eave : par + 0.9 + k * 0.05; cyl(C, e, hl + 0.14, 0.045, 0.1, zt, 6, 'steel', { tag: 'wall.vent' }); if (!s.cut) cyl(C, e, hl + 0.14, 0.075, zt, zt + 0.12, 6, 'dark', { tag: 'wall.vent' }); }
    // THE SALES FLOOR (the section only)
    if (s.cut) {
      const ix0 = -hw + t, ix1 = hw - t, iy0 = -hl + t, iy1 = hl - t, ph = fz + s.cutH;
      F(C, [[ix0, iy0, fz], [ix1, iy0, fz], [ix1, iy1, fz], [ix0, iy1, fz]], 'tile', { tag: 'store.floor', tex: TEX.tile, uv: [[ix0, iy0], [ix1, iy0], [ix1, iy1], [ix0, iy1]] });
      const part = (e0, e1, n0, n1) => { box(C, e0, e1, n0, n1, fz, ph, 'white', { tag: 'store.partition', noTop: true }); box(C, e0 - 0.012, e1 + 0.012, n0 - 0.012, n1 + 0.012, ph, ph + 0.012, 'cap', { tag: 'cut.cap' }); };
      part(-hw + t, -5.4, 2.35, 2.47); part(-4.4, -3.4, 2.35, 2.47); part(-3.52, -3.4, 2.47, hl - t);        // the stockroom, NW
      part(5.5, 6.4, 2.35, 2.47); part(7.3, hw - t, 2.35, 2.47); part(5.5, 5.62, 2.47, hl - t);             // the washrooms, NE
      part(7.3, 7.42, 2.47, hl - t);
      // the cooler wall: eight glass doors, lit from inside
      box(C, -3.3, 5.3, 4.95, 5.75, fz, 2.3, 'shelf', { tag: 'store.cooler', s: { mat: 'glass', em: 'glow:cool', tex: TEX.doors }, top: 'shelf' });
      for (let k = 0; k <= 8; k++) { const e = -3.3 + k * 1.075; box(C, e - 0.03, e + 0.03, 4.9, 4.97, fz + 0.1, 2.25, 'frame', { tag: 'store.cooler' }); }
      box(C, -3.3, 5.3, 4.9, 4.98, 2.3, 2.62, 'band', { tag: 'store.cooler', s: { mat: 'signR', tex: textTex('COLD DRINKS', 1.0, 2.36, 1) } });
      // two gondolas N-S, product on every face and on the top shelf, end caps to the door
      const PM = ['prodR', 'prodB', 'prodY', 'prodG'];
      for (const [gi, ge] of [[0, -6.1], [1, -3.9]]) {
        box(C, ge - 0.45, ge + 0.45, -3.4, 1.6, fz, fz + 0.12, 'dark', { tag: 'store.gondola' });
        box(C, ge - 0.06, ge + 0.06, -3.4, 1.6, fz, 1.55, 'shelf', { tag: 'store.gondola' });
        for (let row = 0; row < 3; row++) for (let seg = 0; seg < 5; seg++) { const z = fz + 0.14 + row * 0.44, n0 = -3.35 + seg * 0.99, mt = PM[(gi + row + seg) & 3];
          for (const sg of [-1, 1]) box(C, sg < 0 ? ge - 0.44 : ge + 0.06, sg < 0 ? ge - 0.06 : ge + 0.44, n0, n0 + 0.95, z, z + 0.34, mt, { tag: 'store.product', tex: TEX.facings, top: 'shelf' }); }
        for (let seg = 0; seg < 5; seg++) box(C, ge - 0.4, ge + 0.4, -3.3 + seg * 0.99, -3.3 + seg * 0.99 + 0.85, 1.55, 1.72, PM[(gi + seg + 1) & 3], { tag: 'store.product', tex: TEX.facings });
        box(C, ge - 0.45, ge + 0.45, -3.85, -3.4, fz, 1.2, 'shelf', { tag: 'store.endcap', s: { mat: PM[(gi + 2) & 3], tex: TEX.facings } }); }
      // the coffee bar on the west wall
      box(C, -hw + t, -8.15, -4.6, 1.0, fz, 0.95, 'wood', { tag: 'store.coffee', top: 'trim' });
      for (const [k, n] of [[0, -3.9], [1, -2.9], [2, -1.9]]) { box(C, -8.75, -8.3, n - 0.2, n + 0.2, 0.95, 1.6, 'dark', { tag: 'store.coffee', s: { mat: k === 1 ? 'red' : 'dark' } }); box(C, -8.7, -8.35, n - 0.15, n + 0.15, 1.6, 1.68, 'steel', { tag: 'store.coffee' }); }
      for (const n of [-0.6, -0.2, 0.3]) cyl(C, -8.45, n, 0.07, 0.95, 1.3, 7, 'white', { tag: 'store.cups' });
      // the checkout: counter N-S by the door, register, lottery case, roller grill; the back-bar on the east wall; a stool
      box(C, 3.2, 4.0, -4.9, -0.8, fz, 1.02, 'wood', { tag: 'store.counter', top: 'trim', s: { mat: 'band' } });
      box(C, 3.35, 3.85, -4.4, -4.0, 1.02, 1.3, 'dark', { tag: 'store.register' }); box(C, 3.3, 3.9, -3.6, -2.7, 1.02, 1.25, 'glass', { tag: 'store.lottery', em: 'glow:cool' });
      box(C, 3.3, 3.9, -2.2, -1.2, 1.02, 1.38, 'glass', { tag: 'store.grill', em: 'win:grill' });
      box(C, hw - t - 0.45, hw - t, -4.9, 1.8, fz, 2.1, 'shelf', { tag: 'store.backbar', wf: { mat: 'prodB', tex: TEX.facings } });
      cyl(C, 6.0, -2.6, 0.2, fz, 0.7, 8, 'dark', { tag: 'store.stool', top: 'red' });
      // the ATM by the door, the ice-cream chest, a stack of cases in the stockroom, a desk, the washroom fittings
      box(C, -2.0, -1.4, -hl + t, -hl + t + 0.5, fz, 1.62, 'steel', { tag: 'store.atm', t: { mat: 'dark' }, nf: { mat: 'glass', em: 'glow:cool' } });
      box(C, 0.7, 2.3, 1.3, 2.1, fz, 0.88, 'white', { tag: 'store.freezer', top: 'glass', t: { mat: 'glass', em: 'glow:cool' } });
      for (let k = 0; k < 3; k++) box(C, -8.7 + k * 0.62, -8.15 + k * 0.62, 4.2, 5.7, fz, fz + 0.5 + (k & 1) * 0.42, 'cord', { tag: 'store.stock' });
      box(C, -6.2, -5.0, 3.0, 3.7, fz, 0.76, 'wood', { tag: 'store.desk' }); box(C, -8.75, -8.15, 2.7, 3.6, fz, 1.8, 'steel', { tag: 'store.rack' });
      box(C, 8.1, 8.6, 4.7, 5.5, fz, 0.45, 'white', { tag: 'store.wc' }); box(C, 5.9, 6.4, 5.3, 5.7, 0.75, 0.9, 'white', { tag: 'store.basin' });
      box(C, -7.62, -6.58, hl - t - 0.02, hl - t + 0.02, fz, fz + 0.005, 'frame', { tag: 'service.threshold' });
      // the lights over the floor, and the room the sun only enters by the windows
      for (const e of [-6.2, -2.2, 1.8, 5.6]) for (const n of [-3.2, 0.6]) lamp(C, e, n, 3.0, '#fff1d8', 0.7, 5.2);
      C.indoor = [-hw + 0.1, hw - 0.1, -hl + 0.1, hl - 0.1, 0.35, 0.55];
    }
    C.door = { e: 0, n: -hl, z: fz, width: 2.2, height: 2.45, facing: 'S' };
    C.st.push({ id: 'door', verb: 'enter', at: [0, -hl, fz], stand: [0, -hl - 1.6] }, { id: 'serviceDoor', verb: 'enter', at: [-7.1, hl, fz], stand: [-7.1, hl + 1.4] },
      { id: 'counter', verb: 'pay', at: [3.2, -2.8, 1.02], stand: [2.5, -2.8] }, { id: 'coffee', verb: 'pourCoffee', at: [-8.3, -2.9, 0.95], stand: [-7.6, -2.9] },
      { id: 'cooler', verb: 'takeDrink', at: [1.0, 4.95, 1.2], stand: [1.0, 4.2] }, { id: 'atm', verb: 'cash', at: [-1.7, -hl + 0.7, 1.2], stand: [-1.7, -hl + 1.3] });
  };
  /* one multi-product dispenser across an island: the ends carry the grade chips, the sides the boots and hoses */
  function mpd(C, e, n, grades, o) { o = o || {}; const z0 = 0.18, w = o.w || 0.26, l = o.l || 0.55, top = o.top || 1.62, hdr = o.hdr || 'GAS', col = o.neon || 'red';
    box(C, e - w - 0.05, e + w + 0.05, n - l - 0.06, n + l + 0.06, z0, z0 + 0.12, 'conc', { tag: 'pump.plinth' });
    box(C, e - w, e + w, n - l, n + l, z0 + 0.12, top, 'enamel', { tag: 'pump.body', s: { mat: 'enamel', b: 0 } });
    grades.forEach((g, k) => { const m = (GRADES[g] || GRADES.reg).mat; box(C, e - w + 0.06, e + w - 0.06, n - l - 0.025, n - l, 0.5 + k * 0.2, 0.66 + k * 0.2, m, { tag: 'pump.grade', noN: true }); });
    for (const sg of [-1, 1]) { const x = e + sg * (w + 0.005);
      box(C, sg < 0 ? x - 0.02 : x, sg < 0 ? x : x + 0.02, n - 0.32, n + 0.32, 1.08, 1.42, 'glass', { tag: 'pump.display', em: 'glow:cool' });
      grades.forEach((g, k) => { const m = (GRADES[g] || GRADES.reg).mat, nn = n - l + 0.22 + k * ((2 * l - 0.44) / Math.max(1, grades.length - 1 || 1)), xb = e + sg * (w + 0.07);
        box(C, xb - 0.05, xb + 0.05, nn - 0.05, nn + 0.05, 0.86, 1.02, m, { tag: 'pump.boot' });
        stick(C, [xb, nn, 1.0], [xb + sg * 0.1, nn, 0.84], 0.05, 'dark', { tag: 'pump.nozzle' });
        stick(C, [e + sg * w, nn, top - 0.1], [xb + sg * 0.16, nn, 0.52], 0.035, 'rubber', { tag: 'pump.hose' });
        stick(C, [xb + sg * 0.16, nn, 0.52], [xb + sg * 0.05, nn, 0.86], 0.035, 'rubber', { tag: 'pump.hose' });
        C.st.push({ id: 'nozzle.' + g + '.' + (sg < 0 ? 'W' : 'E') + '.' + C.st.length, verb: 'fuel', grade: g, at: [xb, nn, 0.95], stand: [e + sg * (w + 0.75), nn], lpm: o.lpm || 40, reach: o.reach || 4.6 }); }); }
    box(C, e - w - 0.04, e + w + 0.04, n - l - 0.04, n + l + 0.04, top, top + 0.36, 'band', { tag: 'pump.header', s: { mat: 'signR', em: 'neon:' + col, tex: textTex(hdr, e, top + 0.06, 1) } }); }
  /* an island: curb, painted noses, bollards, a column to the deck, bin and squeegee, the dispenser */
  function island(C, e, n0, n1, deckZ, o) { o = o || {};
    box(C, e - 0.6, e + 0.6, n0, n1, 0, 0.18, 'conc', { tag: 'island.curb', top: 'conc' });
    box(C, e - 0.61, e + 0.61, n0 - 0.01, n0 + 0.32, 0, 0.19, 'yel', { tag: 'island.nose' }); box(C, e - 0.61, e + 0.61, n1 - 0.32, n1 + 0.01, 0, 0.19, 'yel', { tag: 'island.nose' });
    for (const nb of [n0 - 0.18, n1 + 0.18]) for (const eb of [e - 0.34, e + 0.34]) { cyl(C, eb, nb, 0.1, 0, 1.1, 8, 'yel', { tag: 'island.bollard' }); cyl(C, eb, nb, 0.104, 0.62, 0.72, 8, 'dark', { tag: 'island.bollard', noTop: true }); }
    const nc = o.col != null ? o.col : n1 - 1.0;
    box(C, e - 0.3, e + 0.3, nc - 0.3, nc + 0.3, 0.18, 0.62, 'conc', { tag: 'canopy.column' });
    box(C, e - 0.2, e + 0.2, nc - 0.2, nc + 0.2, 0.62, deckZ, 'sign', { tag: 'canopy.column', s: { mat: 'enamel' } });
    cyl(C, e + 0.12, nc - 0.28, 0.07, 0.95, 1.4, 7, 'red', { tag: 'island.extinguisher' });
    box(C, e - 0.16, e + 0.02, nc - 0.25, nc - 0.2, 1.1, 1.36, 'white', { tag: 'island.towels' });
    cyl(C, e, nc - 0.9, 0.2, 0.18, 1.0, 9, 'green', { tag: 'island.bin' }); cyl(C, e, nc - 0.9, 0.22, 1.0, 1.06, 9, 'dark', { tag: 'island.bin' });
    cyl(C, e + 0.36, nc - 0.9, 0.12, 0.62, 0.92, 8, 'blue', { tag: 'island.squeegee' }); stick(C, [e + 0.36, nc - 0.9, 0.18], [e + 0.36, nc - 0.9, 0.62], 0.04, 'steel', { tag: 'island.squeegee' });
    stick(C, [e + 0.34, nc - 0.86, 0.85], [e + 0.42, nc - 0.98, 1.3], 0.025, 'dark', { tag: 'island.squeegee' }); }
  /* a canopy deck: fascia on every side, a membrane top, a soffit that throws the shade, neon along the fascia, the brand */
  function deck(C, e0, e1, n0, n1, z0, z1, col, word, o) { o = o || {};
    if (C.s.deck === 'cut') {
      for (const [a, b, c, d] of [[e0, e1, n0, n0 + 0.25], [e0, e1, n1 - 0.25, n1], [e0, e0 + 0.25, n0, n1], [e1 - 0.25, e1, n0, n1]]) box(C, a, b, c, d, z0, z0 + 0.35, 'enamel', { tag: 'canopy.beam', bot: true });
      tube(C, e0, e1, n0, z0 + 0.1, col); tube(C, e0, e1, n0, z0 + 0.27, col); return; }
    box(C, e0, e1, n0, n1, z0, z1, 'enamel', { tag: 'canopy.fascia', top: 'deck', topTex: TEX.deck, bot: true });
    box(C, e0 + 0.18, e1 - 0.18, n0 + 0.18, n1 - 0.18, z1, z1 + 0.06, 'deck', { tag: 'canopy.roof', noTop: true });
    for (const z of [z0 + 0.05, z1 - 0.05]) { tube(C, e0, e1, n0, z, col); box(C, e0, e1, n1, n1 + 0.035, z - 0.03, z + 0.03, 'tube', { tag: 'neon.tube', em: 'neon:' + col, b: 1, noS: true });
      tubeN(C, e0 - 0.02, n0, n1, z, col); tubeN(C, e1 + 0.02, n0, n1, z, col); }
    if (word) { const sc = 3, w = textW(word, sc) + 0.6, zt = z0 + (z1 - z0 - textH(sc)) / 2;
      plate(C, -w / 2, w / 2, n0 - 0.01, z0 + 0.12, z1 - 0.12, 'signR', { tag: 'canopy.brand', em: 'neon:' + col, tex: textTex(word, 0, zt, sc) }); } }
  /* THE CAR CANOPY. Three islands at 8 m, the lanes between them run north and south; each dispenser stands in the southern
     half of its island, inside the band the fascia does not hide (a head 1.94 m up is seen to 3.64 m behind a 5.0 m fascia). */
  B.canopy = function (C) {
    const hw = 12.6, hl = 4.0, z0 = 5.0, z1 = 6.1; C.dims = { w: 2 * hw, d: 2 * hl, h: z1 + 0.1 };
    for (const [k, e] of [[0, -8], [1, 0], [2, 8]]) { island(C, e, -3.0, 3.0, z0, { col: 2.1 }); mpd(C, e, -1.25, ['reg', 'sup', 'dsl'], { hdr: 'GAS' }); }
    // the stove-oil post at the west island's foot, for the island's heaters (jugs filled at the kerb)
    box(C, -8.17, -7.83, -2.72, -2.38, 0.18, 1.46, 'ivory', { tag: 'pump.kero' }); box(C, -8.2, -7.8, -2.75, -2.35, 1.46, 1.6, 'signR', { tag: 'pump.kero', em: 'neon:amber', b: 1 });
    stick(C, [-7.82, -2.55, 1.2], [-7.6, -2.55, 0.9], 0.04, 'dark', { tag: 'pump.nozzle' }); stick(C, [-7.83, -2.55, 1.4], [-7.52, -2.55, 0.5], 0.03, 'rubber', { tag: 'pump.hose' });
    C.st.push({ id: 'nozzle.kero', verb: 'fuel', grade: 'kero', at: [-7.6, -2.55, 0.9], stand: [-7.0, -2.55], lpm: 30, reach: 3.2 });
    deck(C, -hw, hw, -hl, hl, z0, z1, 'red', BRAND.canopy);
    for (const e of [-10.6, -5.7, -2.3, 2.3, 5.7, 10.6]) for (const n of [-2.0, 1.8]) lamp(C, e, n, z0 - 0.1, '#e8efff', 0.8, 5.8);
    C.clear = z0;
  };
  /* THE SEMI LANE. A 4.6 m lane between a master island (high-flow diesel and DEF) and a satellite island (the second saddle
     tank), under a 5.5 m deck with a clearance bar hung before it; teal neon, DIESEL on the fascia. */
  B.truck = function (C) {
    const hw = 4.6, hl = 4.6, z0 = 5.5, z1 = 6.6; C.dims = { w: 2 * hw, d: 2 * hl, h: z1 + 0.1 };
    island(C, -2.9, -5.0, 5.0, z0, { col: 3.6 }); island(C, 2.9, -5.0, 5.0, z0, { col: 3.6 });
    mpd(C, -2.9, -1.3, ['dsl', 'dsl'], { hdr: 'DSL', neon: 'teal', w: 0.32, l: 0.72, top: 1.78, lpm: 130, reach: 6.2 });
    mpd(C, -2.9, -3.55, ['def'], { hdr: 'DEF', neon: 'blue', w: 0.22, l: 0.3, top: 1.5, lpm: 20, reach: 4.0 });
    box(C, 2.65, 3.15, -1.75, -0.85, 0.3, 1.34, 'enamel', { tag: 'pump.satellite', s: { mat: 'amber' } });
    box(C, 2.53, 2.63, -1.35, -1.25, 0.86, 1.02, 'amber', { tag: 'pump.boot' }); stick(C, [2.58, -1.3, 1.0], [2.44, -1.3, 0.84], 0.06, 'dark', { tag: 'pump.nozzle' });
    stick(C, [2.65, -1.3, 1.25], [2.38, -1.3, 0.5], 0.05, 'rubber', { tag: 'pump.hose' });
    C.st.push({ id: 'nozzle.satellite', verb: 'fuel', grade: 'dsl', at: [2.44, -1.3, 0.9], stand: [1.8, -1.3], lpm: 130, reach: 6.2, note: 'fills the passenger-side saddle tank while the master runs' });
    deck(C, -hw, hw, -hl, hl, z0, z1, 'teal', 'DIESEL');
    if (C.s.deck !== 'cut') { for (const e of [-2.2, 2.2]) stick(C, [e, -hl - 0.3, z0], [e, -hl - 0.3, 5.3], 0.025, 'steel', { tag: 'canopy.clearance' });
      box(C, -2.35, 2.35, -hl - 0.36, -hl - 0.24, 5.2, 5.3, 'yel', { tag: 'canopy.clearance', tex: TEX.stripes, s: { tex: TEX.stripes } }); }
    for (const e of [-1.2, 1.2]) for (const n of [-2.6, 0.6, 3.4]) lamp(C, e, n, z0 - 0.1, '#e8efff', 0.85, 6.2);
    C.clear = z0;
  };
  /* THE PYLON. The brand in neon, the three prices in LED, OPEN 24/7 in red and blue, on two legs in a stone planter. */
  B.pylon = function (C) {
    const hw = 1.8, n0 = -0.3, n1 = 0.3; C.dims = { w: 2 * hw, d: 0.6, h: 7.6 };
    box(C, -2.0, 2.0, -0.7, 0.7, 0, 0.6, 'stone', { tag: 'pylon.planter', tex: TEX.rubble, top: 'soil' });
    for (const e of [-1.05, 1.05]) box(C, e - 0.13, e + 0.13, -0.13, 0.13, 0.6, 3.45, 'sign', { tag: 'pylon.leg' });
    box(C, -hw, hw, n0, n1, 3.4, 7.6, 'sign', { tag: 'pylon.cabinet', s: { mat: 'sign', b: -1 }, top: 'trim' });
    box(C, -hw - 0.05, hw + 0.05, n0 - 0.05, n1 + 0.05, 7.52, 7.62, 'band', { tag: 'pylon.cap' });
    const zb = 6.52; if (BRAND.pylon) plate(C, -hw + 0.12, hw - 0.12, n0 - 0.01, 6.38, 7.44, 'signR', { tag: 'pylon.brand', em: 'neon:red', tex: textTex(BRAND.pylon, 0, zb, 2) });
    const rows = [['REG', PRICE.reg], ['SUP', PRICE.sup], ['DSL', PRICE.dsl]];
    rows.forEach(([k, p], i) => { const z = 5.66 - i * 0.62, str = k + ' ' + (Math.round(p * 10) / 10).toFixed(1);
      plate(C, -hw + 0.1, hw - 0.1, n0 - 0.01, z - 0.02, z + textH(2) + 0.02, 'sign', { tag: 'pylon.price', em: 'led:led', tex: textTex(str, 0, z, 2) }); });
    const z = 3.56, w1 = textW('OPEN', 2), w2 = textW('24/7', 2), g = 0.375, e0 = -(w1 + g + w2) / 2;
    plate(C, e0 - 0.05, e0 + w1 + 0.05, n0 - 0.01, z - 0.04, z + textH(2) + 0.04, 'signR', { tag: 'pylon.open', em: 'neon:red', tex: textTex('OPEN', e0 + w1 / 2, z, 2) });
    plate(C, e0 + w1 + g - 0.05, e0 + w1 + g + w2 + 0.05, n0 - 0.01, z - 0.04, z + textH(2) + 0.04, 'sign', { tag: 'pylon.open', em: 'neon:blue', tex: textTex('24/7', e0 + w1 + g + w2 / 2, z, 2) });
    for (const zz of [3.47, 4.36, 6.3]) tube(C, -hw + 0.06, hw - 0.06, n0 - 0.012, zz, zz > 6 ? 'red' : 'blue', { d: 0.012 });
  };
  // ---- the forecourt's dressing ---------------------------------------------------------------------------------------------
  B.iceBox = function (C) { C.dims = { w: 1.6, d: 0.85, h: 1.35 };
    box(C, -0.8, 0.8, -0.42, 0.42, 0, 1.3, 'white', { tag: 'prop.ice', top: 'white', s: { mat: 'blue', em: 'neon:blue', tex: textTex('ICE', 0, 0.55, 2) } });
    box(C, -0.82, 0.82, -0.44, 0.44, 1.3, 1.36, 'blue', { tag: 'prop.ice' }); for (const e of [-0.4, 0.4]) box(C, e - 0.1, e + 0.1, -0.47, -0.42, 1.0, 1.06, 'steel', { tag: 'prop.ice' }); };
  B.propane = function (C) { C.dims = { w: 1.7, d: 0.9, h: 1.75 };
    box(C, -0.85, 0.85, -0.45, 0.45, 0, 1.55, 'steel', { tag: 'prop.propane', tex: TEX.grille, top: 'steel', topTex: TEX.grille });
    for (const e of [-0.5, 0, 0.5]) { cyl(C, e, -0.18, 0.16, 0.04, 0.5, 9, 'white', { tag: 'prop.tank' }); cyl(C, e, 0.18, 0.16, 0.04, 0.5, 9, 'white', { tag: 'prop.tank' }); }
    box(C, -0.85, 0.85, -0.47, -0.43, 1.55, 1.8, 'red', { tag: 'prop.propane', s: { mat: 'signR', tex: textTex('PROPANE', 0, 1.6, 1) } }); };
  B.air = function (C) { C.dims = { w: 0.6, d: 0.5, h: 1.45 };
    box(C, -0.25, 0.25, -0.12, 0.12, 0, 0.1, 'conc', { tag: 'prop.air' });
    box(C, -0.28, 0.28, -0.22, 0.22, 0.1, 1.38, 'red', { tag: 'prop.air', s: { mat: 'signR', tex: textTex('AIR', 0, 1.02, 1) } });
    box(C, -0.3, 0.3, -0.24, 0.24, 1.38, 1.45, 'dark', { tag: 'prop.air' }); cyl(C, 0.36, 0, 0.14, 0.4, 0.62, 10, 'rubber', { tag: 'prop.hose' }); };
  B.vacuum = function (C) { C.dims = { w: 0.8, d: 0.8, h: 2.7 };
    cyl(C, 0, 0, 0.34, 0, 1.05, 10, 'blue', { tag: 'prop.vacuum', top: 'dark' }); stick(C, [0, 0, 1.05], [0, 0, 2.6], 0.08, 'steel', { tag: 'prop.vacuum' });
    stick(C, [0, 0, 2.6], [0.9, -0.2, 2.55], 0.06, 'steel', { tag: 'prop.vacuum' }); stick(C, [0.9, -0.2, 2.55], [0.95, -0.25, 0.9], 0.05, 'rubber', { tag: 'prop.hose' }); };
  B.dumpster = function (C) { C.dims = { w: 3.8, d: 2.6, h: 1.9 };
    box(C, -1.9, 1.9, 1.15, 1.3, 0, 1.85, 'wood', { tag: 'prop.enclosure', tex: TEX.boards });
    box(C, -1.9, -1.75, -1.3, 1.15, 0, 1.85, 'wood', { tag: 'prop.enclosure', tex: TEX.boards }); box(C, 1.75, 1.9, -1.3, 1.15, 0, 1.85, 'wood', { tag: 'prop.enclosure', tex: TEX.boards });
    box(C, -1.75, -0.7, -1.32, -1.2, 0.05, 1.8, 'wood', { tag: 'prop.gate', tex: TEX.boards });
    box(C, -0.95, 1.05, -0.2, 1.05, 0, 1.2, 'green', { tag: 'prop.bin' }); F(C, [[-1.0, -0.25, 1.2], [1.1, -0.25, 1.2], [1.1, 1.08, 1.34], [-1.0, 1.08, 1.34]], 'dark', { tag: 'prop.bin' }); };
  B.bench = function (C) { C.dims = { w: 1.7, d: 0.6, h: 0.9 };
    for (const e of [-0.7, 0.7]) box(C, e - 0.04, e + 0.04, -0.25, 0.25, 0, 0.44, 'dark', { tag: 'prop.bench' });
    box(C, -0.82, 0.82, -0.25, 0.22, 0.44, 0.5, 'wood', { tag: 'prop.bench', tex: TEX.boards }); box(C, -0.82, 0.82, 0.2, 0.26, 0.5, 0.88, 'wood', { tag: 'prop.bench' }); };
  B.firewood = function (C) { C.dims = { w: 1.9, d: 0.9, h: 1.7 };
    for (const e of [-0.9, 0.9]) box(C, e - 0.04, e + 0.04, -0.4, 0.4, 0, 1.5, 'wood', { tag: 'prop.rack' });
    for (let r = 0; r < 3; r++) for (let k = 0; k < 4; k++) { const e = -0.66 + k * 0.44, z = 0.05 + r * 0.42; box(C, e - 0.2, e + 0.2, -0.36, 0.36, z, z + 0.38, 'bark', { tag: 'prop.firewood', s: { mat: 'cord', tex: TEX.endgrain }, top: 'bark' }); }
    F(C, [[-1.0, -0.55, 1.5], [1.0, -0.55, 1.5], [1.0, 0.5, 1.7], [-1.0, 0.5, 1.7]], 'roof', { tag: 'prop.rackRoof' });
    plate(C, -0.8, 0.8, -0.56, 1.28, 1.48, 'signR', { tag: 'prop.sign', tex: textTex('FIREWOOD', 0, 1.31, 1) }); };
  B.lampPole = function (C) { C.dims = { w: 0.8, d: 2.0, h: 8.1 };
    cyl(C, 0, 0, 0.32, 0, 0.55, 10, 'conc', { tag: 'pole.base' }); box(C, -0.09, 0.09, -0.09, 0.09, 0.55, 8.0, 'steel', { tag: 'pole' });
    box(C, -0.05, 0.05, -1.3, 0, 7.82, 7.9, 'steel', { tag: 'pole.arm' });
    box(C, -0.34, 0.34, -1.78, -1.05, 7.62, 7.84, 'steel', { tag: 'pole.head', s: { mat: 'enamel', em: 'glow:white', b: 1 }, bot: true }); };
  B.barSign = function (C) { C.dims = { w: 2.9, d: 0.3, h: 2.6 };
    for (const e of [-1.2, 1.2]) box(C, e - 0.05, e + 0.05, -0.05, 0.05, 0, 2.55, 'steel', { tag: 'sign.post' });
    box(C, -1.4, 1.4, -0.08, -0.04, 1.3, 2.55, 'signG', { tag: 'sign.board', s: { mat: 'signG', b: 0 } });
    const L = [['ST PETERS', 2.18], ['BAR CROSSING', 1.86], ['ON FOOT AT LOW TIDE', 1.54]];
    for (const [t, z] of L) plate(C, -1.36, 1.36, -0.085, z - 0.03, z + textH(1) + 0.02, 'signG', { tag: 'sign.text', tex: textTex(t, 0, z, 1) });
    box(C, -0.7, 0.7, -0.08, -0.04, 0.72, 1.18, 'yel', { tag: 'sign.board', s: { mat: 'yel' } });
    plate(C, -0.66, 0.66, -0.085, 0.82, 0.82 + textH(1), 'yel', { tag: 'sign.text', tex: textTex('NO TRUCKS', 0, 0.82, 1) }); };
  B.stopSign = function (C) { C.dims = { w: 0.8, d: 0.2, h: 2.4 };
    box(C, -0.04, 0.04, -0.04, 0.04, 0, 1.95, 'steel', { tag: 'sign.post' }); const pts = [], r = 0.38, zc = 1.95;
    for (let k = 0; k < 8; k++) { const a = (k + 0.5) / 8 * 2 * Math.PI; pts.push([Math.cos(a) * r, -0.06, zc + Math.sin(a) * r]); }
    F(C, pts, 'signR', { tag: 'sign.stop', tex: textTex('STOP', 0, zc - 0.14, 1), uv: pts.map(p => [p[0], p[2]]) }); };
  B.picnic = function (C) { C.dims = { w: 1.9, d: 1.6, h: 0.8 };
    box(C, -0.9, 0.9, -0.38, 0.38, 0.72, 0.78, 'wood', { tag: 'prop.table', tex: TEX.boards });
    for (const n of [-0.66, 0.66]) box(C, -0.9, 0.9, n - 0.14, n + 0.14, 0.42, 0.47, 'wood', { tag: 'prop.table' });
    for (const e of [-0.7, 0.7]) { stick(C, [e, -0.75, 0], [e, 0, 0.72], 0.06, 'wood', { tag: 'prop.table' }); stick(C, [e, 0.75, 0], [e, 0, 0.72], 0.06, 'wood', { tag: 'prop.table' }); } };
  B.wheelStop = function (C) { C.dims = { w: 1.8, d: 0.22, h: 0.12 }; box(C, -0.9, 0.9, -0.1, 0.1, 0, 0.12, 'conc', { tag: 'prop.wheelStop', top: 'yel' }); };
  B.bin = function (C) { C.dims = { w: 0.6, d: 0.6, h: 1.0 }; cyl(C, 0, 0, 0.26, 0, 0.92, 10, 'green', { tag: 'prop.bin' }); cyl(C, 0, 0, 0.29, 0.92, 1.0, 10, 'dark', { tag: 'prop.bin' }); };

  const KINDS = {
    store: { label: 'The store', foot: [-9.2, 9.2, -8.2, 6.4] }, canopy: { label: 'The car canopy', foot: [-12.8, 12.8, -4.1, 4.1] },
    truck: { label: 'The semi lane', foot: [-4.8, 4.8, -5.2, 5.2] }, pylon: { label: 'The price sign', foot: [-2.0, 2.0, -0.7, 0.7] },
    iceBox: { label: 'Ice', foot: [-0.85, 0.85, -0.45, 0.45] }, propane: { label: 'Propane exchange', foot: [-0.9, 0.9, -0.5, 0.5] },
    air: { label: 'Air', foot: [-0.35, 0.5, -0.25, 0.25] }, vacuum: { label: 'Vacuum', foot: [-0.4, 1.0, -0.4, 0.4] },
    dumpster: { label: 'Dumpster', foot: [-1.95, 1.95, -1.35, 1.35] }, bench: { label: 'Bench', foot: [-0.85, 0.85, -0.3, 0.3] },
    firewood: { label: 'Firewood', foot: [-1.0, 1.0, -0.56, 0.5] }, lampPole: { label: 'Lot light', foot: [-0.35, 0.35, -0.35, 0.35] },
    barSign: { label: 'The bar road sign', foot: [-1.4, 1.4, -0.1, 0.1] }, stopSign: { label: 'Stop', foot: [-0.1, 0.1, -0.1, 0.1] },
    picnic: { label: 'Picnic table', foot: [-0.95, 0.95, -0.8, 0.8] }, wheelStop: { label: 'Wheel stop', foot: [-0.9, 0.9, -0.1, 0.1] }, bin: { label: 'Bin', foot: [-0.3, 0.3, -0.3, 0.3] },
  };
  function resolve(kind, o) { o = o || {}; return { kind, cut: o.cutaway === 'section', cutH: o.cutH != null ? +o.cutH : 0.9, deck: o.deck === 'cut' ? 'cut' : 'full',
    doorOpen: clamp(+(o.doorOpen || 0), 0, 1), weather: clamp(o.weather != null ? +o.weather : 0.25, 0, 1) }; }
  function mats(s) { const M = {}, keep = { glass: 1, glassHi: 1, tube: 1, sign: 1, signR: 1, signG: 1, dark: 1 };
    for (const k in RAMP) M[k] = { ramp: keep[k] ? RAMP[k] : RAMP[k].map(c => mix(c, '#5f584c', s.weather * 0.12)) }; return M; }
  function assemble(kind, opts, dir) { const s = resolve(kind, opts), C = { s, out: [], lamps: [], st: [], dir: dir | 0, floor: 0, indoor: null, win: 0 }; (B[kind] || B.bin)(C); return C; }
  const MODELS = new Map();
  function camBasis(dir, elev) { const th = dir * Math.PI / 4, e = (elev != null ? elev : ELEV) * DEG; return { ct: Math.cos(th), st: Math.sin(th), se: Math.sin(e), ce: Math.cos(e) }; }
  function liveModel(kind, opts, dir) { const CP = root.CoastalPass; if (!CP || !CP.light) throw new Error('FuelStop needs CoastalPass (coastalPass.js) loaded first');
    const s = resolve(kind, opts), key = kind + '|' + JSON.stringify(s) + '|' + (s.cut ? dir | 0 : 'x'); let m = MODELS.get(key); if (m) return m;
    const C = assemble(kind, opts, dir); m = CP.light.model(C.out, mats(s)); m.kind = kind; m.s = s; m.lamps = C.lamps; m.st = C.st; m.indoor = C.indoor; m.dims = C.dims; m.door = C.door || null;
    m.EK = m.EM.map(nm => { const q = /^(neon|led|glow):(\w+)/.exec(nm || ''); return q ? { type: q[1], col: q[2] } : null; });
    if (MODELS.size > 40) MODELS.delete(MODELS.keys().next().value); MODELS.set(key, m); return m; }
  /* the frame, its canvas fitted to what the model draws at this facing */
  function frame(kind, dir, opts) { opts = opts || {}; const m = liveModel(kind, opts, dir), Bc = camBasis(dir, opts.elev);
    let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
    for (const f of m.faces) for (const p of f.v) { const xr = p[0] * Bc.ct - p[1] * Bc.st, yr = p[0] * Bc.st + p[1] * Bc.ct, sx = xr * S, sy = -(yr * Bc.se + p[2] * Bc.ce) * S;
      if (sx < x0) x0 = sx; if (sx > x1) x1 = sx; if (sy < y0) y0 = sy; if (sy > y1) y1 = sy; }
    const pad = 6, ox = Math.ceil(-x0) + pad, oy = Math.ceil(-y0) + pad, W = Math.ceil(x1 - x0) + 2 * pad + 2, H = Math.ceil(y1 - y0) + 2 * pad + 2;
    const fr = root.CoastalPass.light.frame(m, { ct: Bc.ct, st: Bc.st, se: Bc.se, ce: Bc.ce, S, ox, oy }, W, H);
    fr.kind = kind; fr.dir = dir | 0; fr.s = m.s;
    const gb = fr.gb, N = W * H, np = [];
    for (let i = 0; i < N; i++) { if (!gb.a[i]) continue; const e = gb.em[i]; if (!e) continue; const q = m.EK[e]; if (q && (q.type === 'neon' || q.type === 'led') && gb.sb[i] >= 1) np.push(i); }
    fr.neonPx = Int32Array.from(np); return fr; }
  /* how much of each light burns for how dark it is (a 24-hour stop: the store is always lit inside) */
  function levels(need) { const k = clamp((need - 0.15) / 0.45, 0, 1);
    return { k, win: k > 0.02 ? 0.3 + 0.7 * k : 0, glow: k, cool: 0.78, neon: k, led: 0.6 + 0.4 * k, lamps: k, inside: 0.55 + 0.45 * k }; }
  function relight(fr, sky, o) { o = o || {}; const CPL = root.CoastalPass.light; sky = sky || CPL.REF_SKY;
    const m = fr.mdl, n0 = o.need != null ? +o.need : CPL.lampNeed(sky), need = isFinite(n0) ? n0 : 0, L = levels(need), cut = fr.s && fr.s.cut;
    const lights = (m.lamps || []).map(l => ({ p: l.p, c: l.c, I: l.I * (cut && l.c === '#fff1d8' ? L.inside : L.lamps), r: l.r })).filter(l => l.I > 0.01);
    const px = CPL.relight(fr, sky, { emit: (nm) => nm && nm.startsWith('win') ? L.win : 0, lights, indoor: cut ? m.indoor : null, outline: !!o.outline });
    // the signs: recolour every neon, LED and glow emitter from its own ramp at its level; a halo round every lit letter
    const gb = fr.gb, W = fr.W, H = fr.H, N = W * H, core = new Uint8Array(N), ck = new Uint8Array(N), CK = Object.keys(NEONRGB);
    for (let i = 0; i < N; i++) { if (!gb.a[i]) continue; const e = gb.em[i]; if (!e) continue; const q = m.EK[e]; if (!q) continue;
      const R = NEONRGB[q.col] || NEONRGB.white, lv = q.type === 'neon' ? L.neon : q.type === 'led' ? L.led : q.col === 'cool' ? L.cool : L.glow; if (lv < 0.04) continue; const a = i * 4, sb = gb.sb[i];
      if (q.type === 'glow') { const c = R[clamp(1 + Math.round(lv * 2.6) + sb, 0, 5)]; px[a] = c[0]; px[a + 1] = c[1]; px[a + 2] = c[2]; continue; }
      if (sb >= 1) { const c = R[lv > 0.62 ? 5 : lv > 0.3 ? 4 : 3]; px[a] = c[0]; px[a + 1] = c[1]; px[a + 2] = c[2]; core[i] = 1; ck[i] = CK.indexOf(q.col); }
      else { const c = R[0], t = 0.5 * lv; px[a] = px[a] + (c[0] - px[a]) * t; px[a + 1] = px[a + 1] + (c[1] - px[a + 1]) * t; px[a + 2] = px[a + 2] + (c[2] - px[a + 2]) * t; } }
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const i = y * W + x; if (!core[i]) continue; const R = NEONRGB[CK[ck[i]]], lv = L.neon;
      for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) { const X = x + dx, Y = y + dy; if (X < 0 || Y < 0 || X >= W || Y >= H) continue; const j = Y * W + X; if (core[j] || !gb.a[j]) continue;
        const d = Math.max(Math.abs(dx), Math.abs(dy)), t = (d === 1 ? 0.42 : 0.18) * lv, c = R[3], a = j * 4; px[a] = px[a] + (c[0] - px[a]) * t; px[a + 1] = px[a + 1] + (c[1] - px[a + 1]) * t; px[a + 2] = px[a + 2] + (c[2] - px[a + 2]) * t; } }
    return px; }
  function castShadow(fr, sky, o) { return root.CoastalPass.light.castShadow(fr, sky || root.CoastalPass.light.REF_SKY, o); }
  function view(fr, ch, sky, o) { return (!ch || ch === 'lit') ? relight(fr, sky, o) : root.CoastalPass.light.view(fr, ch, sky, o); }
  function render(kind, dir, o) { o = o || {}; const fr = frame(kind, dir, o), CPL = root.CoastalPass.light, sky = CPL.skyOf(o); return { w: fr.W, h: fr.H, px: fr.C.ox, py: fr.C.oy, rgba: relight(fr, sky, o) }; }
  /* the verbs, in plan metres from the pivot (the scene turns them into world metres) */
  function stations(kind, o) { const m = liveModel(kind, o || {}, 4); return m.st.map(q => Object.assign({}, q)); }
  const footprint = (kind) => (KINDS[kind] || KINDS.bin).foot.slice();
  const dims = (kind, o) => Object.assign({}, liveModel(kind, o || {}, 4).dims);

  root.FuelStop = { S, KINDS, GRADES, PRICE, NEON, RAMP, FONT, resolve, frame, relight, render, castShadow, view, levels, stations, footprint, dims, textTex, textW, textH, list: () => Object.keys(KINDS) };
})(typeof globalThis !== 'undefined' ? globalThis : this);
