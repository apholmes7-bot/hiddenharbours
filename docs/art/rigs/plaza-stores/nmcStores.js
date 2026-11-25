/* NINE MILE CREEK, KEY SCENES PART 2E (hh-nmc-key-scenes/plaza-stores/, 2026-10-02): THE PLAZA'S TWO STORES (globalThis.NmcStores).
   The owner's full pass on the grocery and the lumber store (chat, 10-02, two reference photos): new exteriors with more charm, each
   store's own props, a lot without wheel stops, loading docks a semi backs up to, and interiors: dock, stock room, staff room, a full
   sales floor with stocked shelving and cashier lanes. Built the way FuelStop builds the gas bar (fuel-stop/fuelStopRig.js): one
   parametric model per option set, in plan metres (e east, n north, z up; the show face south), drawn through the fixed camera
   (orthographic, 40 deg, looking north, 32 px = 1 m) on CoastalPass.light and relit from any WeatherSky.at() sky. No dither, no keyline.
   The words are trade words (GROCERY, LUMBER, HARDWARE); the stores' and the plaza's names stay open (call 35).

   SECTION. cutaway:'section' is the house rigs' sill cut: walls facing the camera drop to cutH above the floor (default 0.9), the rest
   stand to the eave, the roof, parapets, signs and canopies come off, every cut takes a pale cap, partitions cut at the same height.
   Each store is furnished only in the section.

   Kinds (this file): grocery . lumber (the building centre) . dock (a depressed truck well with its seals, bumpers and lights).
   The props (nmcStoresProps.js) add themselves to NmcStores.B and NmcStores.KINDS.

   NmcStores.frame(kind, dir, o) -> CoastalPass frame (pivot fr.C.ox/oy = the plan origin on the ground); relight(fr, sky, {need});
   render(kind, dir, o); castShadow; stations(kind, o); footprint(kind); dims(kind, o); lib (the builders, for the props file). */
(function (root) {
  'use strict';
  const S = 32, DEG = Math.PI / 180, ELEV = 40, CE40 = Math.cos(40 * DEG);
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v, md = (a, m) => a - Math.floor(a / m) * m, fl = Math.floor;
  const hsh = (a, b, s) => { let h = Math.imul(a | 0, 374761393) + Math.imul(b | 0, 668265263) + Math.imul(s | 0, 1274126177) | 0; h ^= h >>> 13; h = Math.imul(h, 1103515245) | 0; h ^= h >>> 16; return (h >>> 0) / 4294967296; };
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r, g, b) => '#' + [r, g, b].map(n => clamp(Math.round(n), 0, 255).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2r(a), B = h2r(b); return r2h(A[0] + (B[0] - A[0]) * t, A[1] + (B[1] - A[1]) * t, A[2] + (B[2] - A[2]) * t); };

  // ---- ramps, dark to light ------------------------------------------------------------------------------------------------------
  const RAMP = {
    brick: ['#4a1d14', '#622619', '#7b3120', '#93402b', '#a95338', '#bc6a4c'], cream: ['#7d725d', '#988c73', '#b2a68a', '#c9bea1', '#ddd3b8', '#ece4cc'],
    stucco: ['#8a8170', '#a59b88', '#bfb6a1', '#d6ceb9', '#e8e1cd', '#f4efdf'], green: ['#0f2a1c', '#163a27', '#1e4c33', '#285f41', '#347452', '#428a64'],
    seam: ['#13301f', '#1a3f29', '#235134', '#2d6441', '#3a7a50', '#4a9061'], glulam: ['#4a2e15', '#61401e', '#7b5329', '#956836', '#ad7f47', '#c3985e'],
    white: ['#7f857f', '#9aa09a', '#b5bab3', '#ccd0c9', '#dfe2dc', '#eef0ea'], block: ['#4f4b45', '#625d56', '#777169', '#8c857c', '#a19a90', '#b5aea3'],
    conc: ['#4f4f4a', '#63635d', '#787871', '#8d8d85', '#a3a39a', '#b8b8ae'], concF: ['#5d5e5a', '#6f706b', '#82837d', '#959690', '#a8a9a3', '#babbb5'],
    roof: ['#3a3d3f', '#474a4c', '#56595a', '#666969', '#787a79', '#8b8d8b'], frame: ['#1f1c19', '#2a2622', '#36312c', '#433d37', '#524b44', '#625a52'],
    glass: ['#27373d', '#33474d', '#40585f', '#50696f', '#62797e', '#7a9095'], glassHi: ['#6f8b90', '#86a2a6', '#a1bcbf', '#bcd4d6', '#cfe6e8', '#e2f2f3'],
    steel: ['#565b5f', '#6d7276', '#868b8f', '#a0a5a8', '#bbbfc1', '#d6d9da'], dark: ['#0e1114', '#151a1e', '#1d2429', '#252d33', '#2e373e', '#38434b'],
    rubber: ['#0f1011', '#17191a', '#202224', '#2a2d2f', '#35393b', '#414547'], yel: ['#5a4610', '#7a5f16', '#9d7b1f', '#c09a2c', '#d9b441', '#ead066'],
    red: ['#4a0f10', '#6b1618', '#8f2022', '#b02b2c', '#c9403d', '#dc5c55'], blue: ['#14283d', '#1c3753', '#27496b', '#335d84', '#44739d', '#5a8ab4'],
    tile: ['#7a786f', '#8e8b82', '#a29f95', '#b5b2a7', '#c7c4b9', '#d7d4ca'], cap: ['#8e8778', '#a79f8e', '#bdb5a2', '#d0c8b4', '#dfd8c5', '#ebe5d4'],
    shelf: ['#5d605f', '#747776', '#8b8e8c', '#a2a5a2', '#b8bbb7', '#cdd0cb'], wood: ['#4b3420', '#5f432a', '#745335', '#896442', '#9e7651', '#b28a63'],
    cord: ['#5a3f22', '#71512d', '#8a653a', '#a37b4b', '#ba925f', '#cda977'], enamel: ['#7c8286', '#959b9e', '#afb4b6', '#c8cccd', '#dcdfe0', '#eceeee'],
    prodR: ['#5a1a16', '#7a241d', '#9a3126', '#b8432f', '#cc5b3e', '#dc7852'], prodB: ['#1b2a4a', '#243a63', '#2f4c7d', '#3d6096', '#5176ad', '#6a8fc2'],
    prodY: ['#6e5316', '#8c6b1f', '#a9852c', '#c49f3e', '#d9b85a', '#e8cd7e'], prodG: ['#1f3a2c', '#29493a', '#35594a', '#436b5a', '#56806c', '#6d9582'],
    prodO: ['#5c2a0e', '#7a3812', '#9a4a18', '#b85e22', '#cf7631', '#e09048'], prodW: ['#7a7c76', '#94968f', '#adafa8', '#c4c6bf', '#d8d9d3', '#e8e9e4'],
    prodP: ['#2e1d3d', '#3e2852', '#4f3468', '#62427f', '#775596', '#8d6aab'], prodBr: ['#4a2c12', '#61391a', '#7a4a24', '#93602f', '#aa773e', '#bf8f52'],
    leaf: ['#1d3318', '#274420', '#32562a', '#3f6a34', '#4e7f40', '#62944f'], pink: ['#5c1a35', '#7d2448', '#a0315e', '#c04476', '#d8608e', '#e883a8'],
    soil: ['#241a12', '#31241a', '#3f2f22', '#4e3b2b', '#5e4936', '#6f5843'], lumber: ['#6b5133', '#856641', '#a07d52', '#b99566', '#cfad7e', '#e1c597'],
    meat: ['#5a1418', '#781c22', '#97272c', '#b03a3a', '#c55650', '#d6766c'], fence: ['#14301f', '#1b3d28', '#244d32', '#2e5e3d', '#3a704a', '#4a8559'],
  };
  const NEON = {
    red: ['#3a0610', '#7a0c1e', '#c8142e', '#ff3348', '#ff8a96', '#ffe3e6'], green: ['#062a14', '#0b5a2a', '#139448', '#2ed36e', '#8ff0b2', '#e6fff0'],
    blue: ['#071a3d', '#0c347a', '#1a5ad1', '#3f8cff', '#9cc4ff', '#e8f1ff'], amber: ['#3a1e04', '#7a4208', '#c86f10', '#ffa228', '#ffd08a', '#fff3dd'],
    white: ['#1c2530', '#3b4a5c', '#7d8ea3', '#bfcde0', '#e4ecf7', '#ffffff'], cool: ['#1f2a33', '#40525f', '#7f98a8', '#bcd2de', '#e2eef4', '#fbfdff'],
    cream: ['#2e2414', '#5e4a28', '#a88f5c', '#f0dcae', '#fbefd2', '#fffaf0'], led: ['#2a0404', '#5e0808', '#a31010', '#f02020', '#ff7070', '#ffd0d0'],
  };
  const NEONRGB = {}; for (const k in NEON) NEONRGB[k] = NEON[k].map(h2r);

  // ---- a 5 x 7 font: on a sign face (a texture) or as channel letters (geometry, one box a run of lit pixels) -------------------------
  const FONT = {}, G = (c, rows) => { FONT[c] = rows.map(r => parseInt(r, 2)); };
  [['A', '01110 10001 10001 11111 10001 10001 10001'], ['B', '11110 10001 10001 11110 10001 10001 11110'], ['C', '01110 10001 10000 10000 10000 10001 01110'],
    ['D', '11110 10001 10001 10001 10001 10001 11110'], ['E', '11111 10000 10000 11110 10000 10000 11111'], ['F', '11111 10000 10000 11110 10000 10000 10000'],
    ['G', '01110 10001 10000 10111 10001 10001 01111'], ['H', '10001 10001 10001 11111 10001 10001 10001'], ['I', '01110 00100 00100 00100 00100 00100 01110'],
    ['J', '00111 00010 00010 00010 00010 10010 01100'], ['K', '10001 10010 10100 11000 10100 10010 10001'], ['L', '10000 10000 10000 10000 10000 10000 11111'],
    ['M', '10001 11011 10101 10101 10001 10001 10001'], ['N', '10001 11001 10101 10011 10001 10001 10001'], ['O', '01110 10001 10001 10001 10001 10001 01110'],
    ['P', '11110 10001 10001 11110 10000 10000 10000'], ['Q', '01110 10001 10001 10001 10101 10010 01101'], ['R', '11110 10001 10001 11110 10100 10010 10001'],
    ['S', '01111 10000 10000 01110 00001 00001 11110'], ['T', '11111 00100 00100 00100 00100 00100 00100'], ['U', '10001 10001 10001 10001 10001 10001 01110'],
    ['V', '10001 10001 10001 10001 10001 01010 00100'], ['W', '10001 10001 10001 10101 10101 10101 01010'], ['X', '10001 10001 01010 00100 01010 10001 10001'],
    ['Y', '10001 10001 01010 00100 00100 00100 00100'], ['Z', '11111 00001 00010 00100 01000 10000 11111'], ['0', '01110 10001 10011 10101 11001 10001 01110'],
    ['1', '00100 01100 00100 00100 00100 00100 01110'], ['2', '01110 10001 00001 00010 00100 01000 11111'], ['3', '11111 00010 00100 00010 00001 10001 01110'],
    ['4', '00010 00110 01010 10010 11111 00010 00010'], ['5', '11111 10000 11110 00001 00001 10001 01110'], ['6', '00110 01000 10000 11110 10001 10001 01110'],
    ['7', '11111 00001 00010 00100 01000 01000 01000'], ['8', '01110 10001 10001 01110 10001 10001 01110'], ['9', '01110 10001 10001 01111 00001 00010 01100'],
    ['&', '01100 10010 10100 01000 10101 10010 01101'], ['/', '00001 00010 00010 00100 01000 01000 10000'], ['.', '00000 00000 00000 00000 00000 01100 01100'],
    ['-', '00000 00000 00000 11111 00000 00000 00000'], ['$', '00100 01111 10100 01110 00101 11110 00100'], [',', '00000 00000 00000 00000 01100 00100 01000'],
    ["'", '01100 00100 01000 00000 00000 00000 00000'], ['!', '00100 00100 00100 00100 00100 00000 00100'], [':', '00000 01100 01100 00000 01100 01100 00000'],
    ['+', '00000 00100 00100 11111 00100 00100 00000'], ['>', '01000 00100 00010 00001 00010 00100 01000'], [' ', '00000 00000 00000 00000 00000 00000 00000']].forEach(([c, r]) => G(c, r.split(' ')));
  /* text on a face whose uv is (e along it, z up it): +1 on a letter pixel, -1 between. uC the text's centre, v0 its foot */
  function textTex(str, uC, v0, scale) {
    const cw = scale / S, ch = scale / (S * CE40), n = str.length, us = uC - (n * 6 - 1) * cw / 2;
    return (u, v) => { const c = fl((u - us) / cw), r = 6 - fl((v - v0) / ch); if (c < 0 || r < 0 || r > 6 || c >= n * 6) return -1;
      const k = (c / 6) | 0, cc = c - k * 6; if (cc === 5) return -1; const g = FONT[str[k]]; return g && ((g[r] >> (4 - cc)) & 1) ? 1 : -1; }; }
  const textW = (str, scale) => (str.length * 6 - 1) * scale / S, textH = (scale) => 7 * scale / (S * CE40);

  const TEX = {
    brick: (u, v) => { const ch = 0.24, cw = 0.5, row = fl(v / ch), uu = u + (row & 1) * cw / 2; if (md(v, ch) < 0.034 || md(uu, cw) < 0.034) return -1; const k = hsh(fl(uu / cw), row, 7); return k < 0.16 ? -1 : k > 0.93 ? 1 : 0; },
    band: (u, v) => md(u, 1.5) < 0.03 ? -1 : 0,
    seam: (u, v) => { const j = md(u, 0.45); return j < 0.03 ? -1 : j < 0.06 ? 1 : 0; },
    rib: (u, v) => { const j = md(u, 0.3); return j < 0.04 ? -1 : j < 0.07 ? 1 : 0; },
    split: (u, v) => { const row = fl(v / 0.2), uu = u + (row & 1) * 0.2; if (md(v, 0.2) < 0.025 || md(uu, 0.4) < 0.025) return -2; return hsh(fl(uu * 25), fl(v * 25), 13) < 0.3 ? -1 : 0; },
    stucco: (u, v) => hsh(fl(u * 30), fl(v * 30), 17) < 0.12 ? -1 : 0,
    tile: (u, v) => (md(u, 0.6) < 0.03 || md(v, 0.6) < 0.03) ? -1 : (((fl(u / 0.6) * 7 + fl(v / 0.6) * 3) % 11) === 0 ? 1 : 0),
    concF: (u, v) => (md(u, 4) < 0.03 || md(v, 4) < 0.03) ? -1 : (hsh(fl(u * 6), fl(v * 6), 19) < 0.06 ? -1 : 0),
    walk: (u, v) => (md(u, 1.5) < 0.03 || md(v, 1.5) < 0.03) ? -1 : 0,
    membrane: (u, v) => md(u, 1.2) < 0.03 ? -1 : 0,
    grille: (u, v) => md(u, 0.1) < 0.025 || md(v, 0.1) < 0.025 ? 0 : -3,
    boards: (u, v) => { const i = fl(u / 0.21); if (md(u, 0.21) < 0.018) return -1; return hsh(i, 3, 11) < 0.22 ? -1 : 0; },
    endgrain: (u, v) => { const c = 0.14, i = fl(u / c), j = fl((v + (i & 1) * 0.07) / c), du = md(u, c) / c - 0.5, dv = md(v + (i & 1) * 0.07, c) / c - 0.5;
      if (du * du + dv * dv > 0.2) return -2; const k = hsh(i, j, 29); return k < 0.25 ? -1 : k > 0.8 ? 1 : 0; },
    lumberEnd: (u, v) => (md(u, 0.1) < 0.012 || md(v, 0.05) < 0.008) ? -2 : (hsh(fl(u / 0.1), fl(v / 0.05), 31) < 0.3 ? -1 : 0),
    box: (u, v) => md(u, 0.19) < 0.025 || md(v, 0.3) < 0.03 ? -2 : (hsh(fl(u / 0.19), fl(v / 0.3), 41) < 0.3 ? 1 : 0),
    can: (u, v) => md(u, 0.11) < 0.022 ? -2 : (md(v, 0.16) > 0.125 ? 1 : (md(v, 0.16) < 0.03 ? -1 : 0)),
    bottle: (u, v) => md(u, 0.12) < 0.035 ? -2 : (md(v, 0.33) > 0.24 ? -1 : (md(v, 0.33) < 0.06 ? 1 : 0)),
    bag: (u, v) => md(u, 0.24) < 0.03 ? -2 : (md(v, 0.3) > 0.22 ? 1 : (hsh(fl(u / 0.24), fl(v / 0.3), 43) < 0.25 ? -1 : 0)),
    produce: (u, v) => { const c = 0.09, du = md(u, c) / c - 0.5, dv = md(v, c) / c - 0.5; if (du * du + dv * dv > 0.22) return -2; const k = hsh(fl(u / c), fl(v / c), 47); return k < 0.25 ? -1 : k > 0.75 ? 1 : 0; },
    peg: (u, v) => (md(u, 0.1) < 0.025 && md(v, 0.1) < 0.025) ? -2 : 0,
    doors: (u, v) => md(u, 0.76) < 0.05 ? -3 : (md(v, 0.38) < 0.03 ? -1 : 0),
    dock: (u, v) => md(v, 0.6) < 0.035 ? -1 : (md(v, 0.6) > 0.56 ? 1 : 0),
    slats: (u, v) => md(u, 0.09) < 0.015 ? -1 : 0,
    tools: (u, v) => { const c = 0.22, i = fl(u / c), j = fl(v / 0.3); if (md(u, c) < 0.05 || md(v, 0.3) < 0.07) return -2; const k = hsh(i, j, 53); return k < 0.3 ? -1 : k > 0.7 ? 1 : 0; },
    paintCans: (u, v) => md(u, 0.17) < 0.02 ? -2 : (md(v, 0.22) > 0.17 ? 1 : (md(v, 0.22) < 0.03 ? -2 : 0)),
    chips: (u, v) => (md(u, 0.08) < 0.012 || md(v, 0.1) < 0.012) ? -2 : ((fl(u / 0.08) + fl(v / 0.1)) % 3) - 1,
    hatch: (u, v) => md(u + v, 0.5) < 0.25 ? 0 : -3,
  };

  // ---- builders, in plan metres (e, n, z) ---------------------------------------------------------------------------------------------
  const P = (e, n, z) => [-e, -n, z];
  function F(C, pts, mat, o) { o = o || {}; const f = { v: pts.map(p => P(p[0], p[1], p[2])), mat, b: o.b || 0, db: 0, uv: o.uv || null, tex: o.tex || null }; if (o.tag) f.tag = o.tag; if (o.em) f.em = o.em; C.out.push(f); return f; }
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
  function plate(C, e0, e1, n, z0, z1, mat, o) { o = o || {}; return F(C, [[e0, n, z0], [e1, n, z0], [e1, n, z1], [e0, n, z1]], mat, { tag: o.tag, em: o.em, b: o.b || 0, tex: o.tex || null, uv: o.tex ? [[e0, z0], [e1, z0], [e1, z1], [e0, z1]] : null }); }
  const flat = (C, e0, e1, n0, n1, z, mat, o) => { o = o || {}; return F(C, [[e0, n0, z], [e1, n0, z], [e1, n1, z], [e0, n1, z]], mat, { tag: o.tag, em: o.em, b: o.b || 0, tex: o.tex || null, uv: o.tex ? [[e0, n0], [e1, n0], [e1, n1], [e0, n1]] : null }); };
  const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]], crs = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const nrm = (a) => { const m = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / m, a[1] / m, a[2] / m]; };
  function stick(C, a, b, t, mat, o) { o = o || {}; const u = nrm(sub(b, a)), p = Math.abs(u[2]) < 0.9 ? [0, 0, 1] : [1, 0, 0], v1 = nrm(crs(u, p)), v2 = crs(u, v1), h = t / 2;
    const c = (Q, s1, s2) => [Q[0] + (v1[0] * s1 + v2[0] * s2) * h, Q[1] + (v1[1] * s1 + v2[1] * s2) * h, Q[2] + (v1[2] * s1 + v2[2] * s2) * h];
    const A = [c(a, -1, -1), c(a, 1, -1), c(a, 1, 1), c(a, -1, 1)], B2 = [c(b, -1, -1), c(b, 1, -1), c(b, 1, 1), c(b, -1, 1)];
    for (let k = 0; k < 4; k++) { const k2 = (k + 1) % 4; F(C, [A[k], A[k2], B2[k2], B2[k]], mat, { tag: o.tag, b: o.b || 0, em: o.em }); }
    F(C, B2, mat, { tag: o.tag, b: o.b || 0, em: o.em }); if (!o.noA) F(C, A, mat, { tag: o.tag, b: o.b || 0, em: o.em }); }
  function cyl(C, e, n, r, z0, z1, k, mat, o) { o = o || {}; const Q = []; for (let i = 0; i < k; i++) { const a = (i + 0.5) / k * 2 * Math.PI; Q.push([e + Math.cos(a) * r, n + Math.sin(a) * r]); }
    for (let i = 0; i < k; i++) { const a = Q[i], b = Q[(i + 1) % k]; F(C, [[a[0], a[1], z0], [b[0], b[1], z0], [b[0], b[1], z1], [a[0], a[1], z1]], mat, { tag: o.tag, b: o.b || 0, em: o.em }); }
    if (!o.noTop) F(C, Q.map(p => [p[0], p[1], z1]), o.top || mat, { tag: o.tag, b: o.topB != null ? o.topB : (o.b || 0), em: o.topEm }); }
  /* channel letters: each run of lit font pixels one box, proud of the face at n by o.d, centred on eC, its foot at z0 */
  function letters(C, str, eC, n, z0, scale, mat, o) { o = o || {}; const cw = scale / S, ch = scale / (S * CE40), d = o.d || 0.1, e0 = eC - textW(str, scale) / 2;
    for (let k = 0; k < str.length; k++) { const g = FONT[str[k]]; if (!g) continue;
      for (let r = 0; r < 7; r++) { let c = 0; while (c < 5) { if (!((g[r] >> (4 - c)) & 1)) { c++; continue; } let c1 = c; while (c1 < 5 && ((g[r] >> (4 - c1)) & 1)) c1++;
        const ea = e0 + (k * 6 + c) * cw, eb = e0 + (k * 6 + c1) * cw, z = z0 + (6 - r) * ch; box(C, ea, eb, n - d, n, z, z + ch, mat, { tag: o.tag || 'sign.letter', em: o.em, noN: true, b: o.b || 0 }); c = c1; } } } }

  // ---- walls with openings and the sill cut (the outbuilding rig's, in plan metres) ----------------------------------------------------
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
  const faceN = (C, o) => { const a = C.dir * Math.PI / 4, nx = -o[0], ny = -o[1]; return nx * Math.sin(a) + ny * Math.cos(a); };
  const dropped = (C, o) => C.s.cut && faceN(C, o) < -0.2;
  /* w: {a:[e,n], b:[e,n], out:[e,n] outward, z0, top, eave, holes:[{u0,u1,z0,z1}], mat, tex, inMat, t, tag, gable:[uPeak, zPeak]} */
  function wall(C, w) {
    const t = w.t || 0.3, holes = w.holes || [], L = Math.hypot(w.b[0] - w.a[0], w.b[1] - w.a[1]), d = [(w.b[0] - w.a[0]) / L, (w.b[1] - w.a[1]) / L], no = w.out, z0 = w.z0 || 0;
    const M = (u, z, off) => [w.a[0] + d[0] * u + no[0] * off, w.a[1] + d[1] * u + no[1] * off, z];
    let outline = w.gable ? [[0, z0], [L, z0], [L, w.top], [w.gable[0], w.gable[1]], [0, w.top]] : [[0, z0], [L, z0], [L, w.top], [0, w.top]], capZ = null; const drop = dropped(C, no);
    if (C.s.cut) { capZ = drop ? C.floor + C.s.cutH : Math.min(w.eave, w.top); outline = clipAx(outline, 1, capZ, false); }
    for (const Q of pieces(outline, holes)) {
      F(C, Q.map(p => M(p[0], p[1], 0)), w.mat || 'brick', { uv: Q.map(p => [p[0], p[1]]), tex: w.tex, tag: w.tag });
      F(C, Q.map(p => M(p[0], p[1], -t)), w.inMat || 'white', { tag: 'wall.inside', tex: w.inTex || null, uv: w.inTex ? Q.map(p => [p[0], p[1]]) : null }); }
    for (const h of holes) { const z1 = capZ != null ? Math.min(h.z1, capZ) : h.z1; if (z1 <= h.z0 + 1e-6) continue;
      const q = (u0, za, u1, zb) => F(C, [M(u0, za, 0), M(u1, zb, 0), M(u1, zb, -t), M(u0, za, -t)], w.revMat || 'cream', { tag: w.tag, b: -1 });
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
  function glazing(C, Wf, h, o) { o = o || {}; if (Wf.capZ != null && h.z0 >= Wf.capZ - 1e-6) return; const zt = Wf.capZ != null ? Math.min(h.z1, Wf.capZ) : h.z1, em = o.em || 'win:' + (C.win++);
    F(C, [Wf.M(h.u0, h.z0, -0.08), Wf.M(h.u1, h.z0, -0.08), Wf.M(h.u1, zt, -0.08), Wf.M(h.u0, zt, -0.08)], 'glass', { tag: o.tag || 'show.glazing', em });
    if (zt > h.z0 + 0.6) for (let u = h.u0 + 0.3; u < h.u1 - 0.9; u += 2.6) F(C, [Wf.M(u, zt - 0.5, -0.075), Wf.M(u + 0.6, zt - 0.5, -0.075), Wf.M(u + 0.3, zt - 0.14, -0.075)], 'glassHi', { tag: o.tag || 'show.glazing', em });
    const n = Math.max(1, Math.round((h.u1 - h.u0) / (o.bay || 1.5)));
    for (let k = 0; k <= n; k++) { const u = h.u0 + (h.u1 - h.u0) * k / n; fbox(C, Wf.M, u - 0.045, u + 0.045, h.z0, zt, -0.09, 0.02, 'frame', { tag: 'show.frame' }); }
    fbox(C, Wf.M, h.u0, h.u1, h.z0, h.z0 + 0.09, -0.09, 0.03, 'frame', { tag: 'show.frame' });
    if (Wf.capZ == null || h.z1 <= Wf.capZ) fbox(C, Wf.M, h.u0, h.u1, h.z1 - 0.09, h.z1, -0.09, 0.03, 'frame', { tag: 'show.frame' });
    if (o.transom && (Wf.capZ == null || o.transom < Wf.capZ)) fbox(C, Wf.M, h.u0, h.u1, o.transom - 0.045, o.transom + 0.045, -0.09, 0.02, 'frame', { tag: 'show.frame' }); }
  const lamp = (C, e, n, z, c, I, r) => C.lamps.push({ p: P(e, n, z), c, I, r });
  /* a bipart sliding door pair in a south face at n, centred on e, w wide, the leaves glass in frames; o.open 0..1 */
  function slider(C, e, n, w, fz, zt, o) { o = o || {}; const hw = w / 2, op = (o.open || 0) * hw * 0.95;
    for (const sg of [-1, 1]) { const a0 = sg < 0 ? e - hw - op : e + 0.01 + op, a1 = a0 + hw - 0.01;
      box(C, a0, a1, n - 0.03, n + 0.02, fz + 0.01, zt, 'glass', { tag: 'door.leaf', em: 'win:door', noN: true });
      for (const [b0, b1, c0, c1] of [[a0, a1, fz, fz + 0.12], [a0, a1, zt - 0.08, zt], [a0, a0 + 0.07, fz, zt], [a1 - 0.07, a1, fz, zt]]) box(C, b0, b1, n - 0.045, n + 0.03, c0, c1, 'frame', { tag: 'door.leaf' }); }
    box(C, e - hw - 0.08, e + hw + 0.08, n - 0.06, n + 0.03, zt, zt + 0.12, 'frame', { tag: 'door.header' }); }

  // ---- a gondola run along e (E-W): kick, back, shelves, product; its show face south (sides 'S', 'N' or 'SN') -----------------------
  const PMATS = ['prodR', 'prodB', 'prodY', 'prodG', 'prodO', 'prodW', 'prodP', 'prodBr'], PTEX = ['box', 'can', 'bottle', 'bag', 'box', 'can'];
  function gondola(C, e0, e1, n0, n1, fz, o) { o = o || {}; const h = o.h || 1.8, lv = o.levels || 5, seg = o.seg || 1.2, sd = o.seed || 1, nc = (n0 + n1) / 2, back = o.back || 'shelf';
    box(C, e0, e1, n0 + 0.08, n1 - 0.08, fz, fz + 0.12, 'dark', { tag: 'store.kick', noTop: true });
    box(C, e0, e1, nc - 0.04, nc + 0.04, fz, fz + h, back, { tag: 'store.gondola', tex: o.backTex || TEX.peg, s: { mat: back, tex: o.backTex || TEX.peg } });
    for (let k = 0; e0 + k * seg < e1 - 0.05; k++) { const a = e0 + k * seg, b = Math.min(e1, a + seg);
      box(C, a - 0.02, a + 0.02, n0, n1, fz, fz + h, 'shelf', { tag: 'store.upright' });
      for (let l = 0; l < lv; l++) { const z = fz + 0.14 + l * (h - 0.25) / lv, zt = z + (h - 0.25) / lv * 0.78, pm = o.mats ? o.mats[(k + l * 3 + sd) % o.mats.length] : PMATS[(hsh(k, l, sd) * PMATS.length) | 0], tx = o.tex ? TEX[o.tex] : TEX[PTEX[(hsh(k + 7, l, sd + 3) * PTEX.length) | 0]];
        if (/S/.test(o.sides || 'S')) { box(C, a + 0.03, b - 0.03, n0, nc - 0.05, z - 0.03, z, 'shelf', { tag: 'store.shelf', noN: true }); box(C, a + 0.05, b - 0.05, n0 + 0.02, nc - 0.06, z, zt, pm, { tag: 'store.product', tex: tx, s: { mat: pm, tex: tx }, noN: true, noE: true, noW: true }); }
        if (/N/.test(o.sides || 'S') && l === lv - 1) box(C, a + 0.05, b - 0.05, nc + 0.06, n1 - 0.02, z, zt, PMATS[(hsh(k, l + 9, sd) * PMATS.length) | 0], { tag: 'store.product', noS: true, noE: true, noW: true, noN: true }); } }
    box(C, e1 - 0.02, e1 + 0.02, n0, n1, fz, fz + h, 'shelf', { tag: 'store.upright' });
    if (o.cap) for (const [ec, sg] of [[e0, -1], [e1, 1]]) { const pm = PMATS[(hsh(ec * 10, 5, sd) * PMATS.length) | 0];
      box(C, sg < 0 ? ec - 0.55 : ec, sg < 0 ? ec : ec + 0.55, n0 + 0.05, n1 - 0.05, fz, fz + 1.3, 'shelf', { tag: 'store.endcap', ef: sg > 0 ? { mat: pm, tex: TEX.box } : undefined, wf: sg < 0 ? { mat: pm, tex: TEX.box } : undefined, s: { mat: pm, tex: TEX.box }, top: pm }); } }
  /* a reach-in cooler or freezer bank along e, its glass doors facing south at n0, lit from inside */
  function coolerBank(C, e0, e1, n0, n1, fz, o) { o = o || {}; const h = o.h || 2.2;
    box(C, e0, e1, n0, n1, fz, fz + h, 'enamel', { tag: 'store.cooler', s: { mat: 'glass', em: 'glow:cool', tex: TEX.doors }, top: 'enamel' });
    for (let e = e0; e <= e1 + 1e-6; e += 0.76) box(C, e - 0.03, e + 0.03, n0 - 0.05, n0 + 0.01, fz + 0.1, fz + h - 0.08, 'frame', { tag: 'store.cooler', noN: true });
    for (let k = 0; e0 + k * 0.76 < e1 - 0.1; k++) for (let l = 0; l < 4; l++) { const z = fz + 0.25 + l * 0.46; box(C, e0 + k * 0.76 + 0.08, e0 + (k + 1) * 0.76 - 0.08, n0 + 0.03, n0 + 0.3, z, z + 0.3, o.mats ? o.mats[(k + l) % o.mats.length] : PMATS[(hsh(k, l, 77) * 8) | 0], { tag: 'store.product', tex: TEX.bottle, s: { mat: o.mats ? o.mats[(k + l) % o.mats.length] : 'prodW', tex: o.tex ? TEX[o.tex] : TEX.bottle, em: 'glow:cool' }, noN: true, noE: true, noW: true, noTop: true }); }
    box(C, e0, e1, n0 - 0.06, n0 + 0.02, fz + h, fz + h + 0.32, o.band || 'green', { tag: 'store.cooler', s: { mat: o.band || 'green', tex: o.label ? textTex(o.label, (e0 + e1) / 2, fz + h + 0.06, 1) : null } }); }
  /* a checkout lane, its belt running N-S at e, the customer walking south; lane number lit on a pole */
  function checkout(C, e, n0, n1, fz, num, o) { o = o || {};
    box(C, e - 0.45, e + 0.45, n0, n1, fz, fz + 0.86, 'wood', { tag: 'store.checkout', top: 'shelf', s: { mat: 'enamel' } });
    box(C, e - 0.32, e + 0.12, n0 + 1.1, n1 - 0.1, fz + 0.86, fz + 0.9, 'dark', { tag: 'store.belt' });
    box(C, e - 0.4, e + 0.4, n0 + 0.55, n0 + 1.0, fz + 0.86, fz + 0.92, 'glass', { tag: 'store.scanner', em: 'glow:cool' });
    box(C, e + 0.18, e + 0.42, n0 + 0.35, n0 + 0.6, fz + 0.86, fz + 1.28, 'dark', { tag: 'store.register', s: { mat: 'glass', em: 'glow:cool' } });
    box(C, e - 0.45, e + 0.45, n0 - 0.6, n0, fz, fz + 0.8, 'shelf', { tag: 'store.bagging', top: 'steel' });
    box(C, e + 0.52, e + 0.86, n1 - 1.4, n1 - 0.2, fz, fz + 1.2, 'shelf', { tag: 'store.impulse', s: { mat: PMATS[num % 8], tex: TEX.box } });
    box(C, e - 0.03, e + 0.03, n1 - 0.25, n1 - 0.19, fz + 0.86, fz + 2.15, 'steel', { tag: 'store.lanepole' });
    box(C, e - 0.2, e + 0.2, n1 - 0.3, n1 - 0.24, fz + 2.15, fz + 2.5, 'dark', { tag: 'store.lanelight', s: { mat: 'green', em: 'neon:green', tex: textTex(String(num), e, fz + 2.22, 2) }, noN: true });
    if (o.st) C.st.push({ id: 'checkout' + num, verb: 'pay', at: [e, (n0 + n1) / 2, fz + 0.86], stand: [e - 0.95, (n0 + n1) / 2] }); }

  const B = {};
  /* ================================================================================================================================
     THE GROCERY. 30 x 24 m of terracotta brick under cream precast bands and a cream coping; a brick entry tower 9 m wide that steps
     out 1 m and rises to a gable at 10 m, a cream panel on it with GROCERY in green channel letters, a green canopy band over the
     two door pairs; plate glass in bronze frames either side between brick piers, a green plate on each wing (OPEN 7 DAYS, BAKERY
     DELI); a walk 4 m deep with a yellow kerb. Round the back: two dock doors in their wells (the dock kind), a man door, the compactor.
     Inside (section): vestibule, four lanes, the service desk, produce, deli and bakery, meat and dairy cases along the back, a
     freezer bank, three gondola runs; behind the partition the stock room with its two docks and racking, the walk-in cooler and
     freezer, the staff room, the office and the washroom. ============================================================================ */
  B.grocery = function (C) {
    const s = C.s, hw = 15, hl = 12, fz = C.floor = 0.15, eave = 6.4, par = 7.0, t = 0.3, cut = s.cut, ph = fz + s.cutH;
    const wk = s.o.walk || 4, dkS = s.o.dock || null, mirror = !!(dkS && dkS.side === 'E'); C.dims = { w: 2 * hw, d: 2 * hl, h: 10.1, walk: wk };   // 10-06: o.dock, the side dock (dockWing)
    box(C, -hw - 0.06, hw + 0.06, -hl - 0.06, hl + 0.06, 0, fz, 'conc', { tag: 'wall.plinth', noTop: true });
    box(C, -hw - 0.5, hw + 0.5, -hl - wk, -hl, 0, fz, 'conc', { tag: 'walk', topTex: TEX.walk, s: { mat: 'yel' } });
    // the front: wings of plate glass between brick piers; the tower's mouth in the middle
    const wl = [{ u0: 0.9, u1: 5.05, z0: 0.6, z1: 3.1 }, { u0: 5.75, u1: 10.0, z0: 0.6, z1: 3.1 }], mouth = { u0: 11.2, u1: 18.8, z0: fz, z1: 3.1 }, wr = [{ u0: 20.0, u1: 24.25, z0: 0.6, z1: 3.1 }, { u0: 24.95, u1: 29.1, z0: 0.6, z1: 3.1 }];
    const Wf = wall(C, { a: [-hw, -hl], b: [hw, -hl], out: [0, -1], top: par, eave, holes: wl.concat([mouth], wr), tex: TEX.brick, t, tag: 'show.front' });
    for (const h of wl.concat(wr)) glazing(C, Wf, h, { bay: 1.4, transom: 2.55 });
    // the back: two dock doors, a man door, the compactor's chute (u runs west from e +15)
    const dk = [{ u0: 24.125, u1: 26.875, z0: fz, z1: fz + 3.05 }, { u0: 20.125, u1: 22.875, z0: fz, z1: fz + 3.05 }], md2 = { u0: 17.05, u1: 17.95, z0: fz, z1: 2.25 }, chute = { u0: 11.0, u1: 13.0, z0: 0.95, z1: 2.15 };
    const Wb = wall(C, { a: [hw, hl], b: [-hw, hl], out: [0, 1], top: par, eave, holes: (dkS ? [] : dk).concat([md2, chute]), tex: TEX.brick, t, tag: 'wall.back', inMat: 'block', inTex: TEX.split });
    const We = wall(C, { a: [hw, -hl], b: [hw, hl], out: [1, 0], top: par, eave, holes: mirror ? [{ u0: hl + 7.5, u1: hl + 11.3, z0: fz, z1: 3.3 }] : [], tex: TEX.brick, t, tag: 'wall.side' });
    const Ww = wall(C, { a: [-hw, hl], b: [-hw, -hl], out: [-1, 0], top: par, eave, holes: dkS && !mirror ? [{ u0: 0.7, u1: 4.5, z0: fz, z1: 3.3 }] : [], tex: TEX.brick, t, tag: 'wall.side' });
    if (dkS) dockWing(C, hw, Object.assign({ n1: hl, mat: 'brick', tex: TEX.brick, band: 'cream', coping: 'cream', stock: 'cord' }, dkS));
    for (const h of dkS ? [] : dk) { const e = hw - (h.u0 + h.u1) / 2, z1 = h.z1; box(C, e - 1.36, e + 1.36, hl - t + 0.04, hl - t + 0.1, fz, z1, 'enamel', { tag: 'dock.door', tex: TEX.dock, nf: { mat: 'enamel', tex: TEX.dock } });
      for (const zz of [fz + 1.75]) box(C, e - 1.2, e + 1.2, hl - t + 0.02, hl - t + 0.04, zz, zz + 0.28, 'glass', { tag: 'dock.window' });
      flat(C, e - 1.1, e + 1.1, hl - t - 2.4, hl - t, fz + 0.012, 'steel', { tag: 'dock.leveler', tex: TEX.grille }); for (const ee of [e - 1.15, e + 1.12]) flat(C, ee, ee + 0.05, hl - t - 2.4, hl - t, fz + 0.014, 'yel', { tag: 'dock.edge' }); }
    box(C, hw - 17.95, hw - 17.05, hl - t + 0.05, hl - t + 0.1, fz, 2.25, 'steel', { tag: 'door.service' });
    // brick piers, the bands, the coping
    const capZ = cut ? ph : par + 0.06;
    for (const e of [-15, -9.6, 9.6, 15]) { const mid = Math.abs(e) < 10, zt = cut ? ph : mid ? 3.3 : par + 0.06; box(C, e - 0.35, e + 0.35, -hl - 0.2, -hl + 0.02, 0, zt, 'brick', { tag: 'show.pier', tex: TEX.brick, s: { mat: 'brick', tex: TEX.brick }, noTop: cut });
      if (!cut) { box(C, e - 0.42, e + 0.42, -hl - 0.26, -hl + 0.02, 3.25, 3.75, 'cream', { tag: 'show.pierCap' }); if (!mid) box(C, e - 0.4, e + 0.4, -hl - 0.24, -hl + 0.02, par, par + 0.14, 'cream', { tag: 'show.pierCap' }); }
      else box(C, e - 0.355, e + 0.355, -hl - 0.205, -hl + 0.025, ph, ph + 0.012, 'cap', { tag: 'cut.cap' }); }
    void capZ;
    if (!cut) {
      for (const [q, L] of [[Wf, 2 * hw], [We, 2 * hl], [Ww, 2 * hl], [Wb, 2 * hw]]) { fbox(C, q.M, 0, L, 3.3, 3.7, 0, 0.08, 'cream', { tag: 'show.band', tex: TEX.band }); fbox(C, q.M, 0, L, 5.55, 5.75, 0, 0.06, 'cream', { tag: 'show.band', tex: TEX.band }); }
      box(C, -hw - 0.1, hw + 0.1, -hl - 0.1, -hl + 0.2, par, par + 0.1, 'cream', { tag: 'roof.coping' }); box(C, -hw - 0.1, hw + 0.1, hl - 0.2, hl + 0.1, par, par + 0.1, 'cream', { tag: 'roof.coping' });
      box(C, -hw - 0.1, -hw + 0.2, -hl, hl, par, par + 0.1, 'cream', { tag: 'roof.coping' }); box(C, hw - 0.2, hw + 0.1, -hl, hl, par, par + 0.1, 'cream', { tag: 'roof.coping' });
      F(C, [[-hw + t, -hl + t, eave], [hw - t, -hl + t, eave], [hw - t, hl - t, eave], [-hw + t, hl - t, eave]], 'roof', { tag: 'roof', tex: TEX.membrane, uv: [[-hw, -hl], [hw, -hl], [hw, hl], [-hw, hl]] });
      for (const [q, L] of [[Wf, 2 * hw], [Wb, 2 * hw], [We, 2 * hl], [Ww, 2 * hl]]) F(C, [q.M(0, eave, -t), q.M(L, eave, -t), q.M(L, par, -t), q.M(0, par, -t)], 'brick', { tag: 'roof.parapet', b: -1 });
      // the wings' plates: green, cream words, a cream frame
      for (const [e0, e1, str] of [[-14.1, -5.5, 'OPEN 7 DAYS'], [5.5, 14.1, 'BAKERY  DELI']]) { box(C, e0, e1, -hl - 0.16, -hl + 0.01, 4.0, 5.3, 'cream', { tag: 'show.plate', noN: true });
        plate(C, e0 + 0.08, e1 - 0.08, -hl - 0.165, 4.08, 5.22, 'green', { tag: 'show.plate', em: 'neon:cream', tex: textTex(str, (e0 + e1) / 2, 4.22, 3) }); }
      // rooftop plant: four units, the refrigeration condensers, a hatch, the vents
      for (const [e, n] of [[-10, 3], [-4, 7], [3, 4], [9, 8]]) { box(C, e - 1.1, e + 1.1, n - 0.8, n + 0.8, eave, eave + 1.15, 'steel', { tag: 'roof.unit', s: { mat: 'steel', tex: TEX.grille }, top: 'steel' }); cyl(C, e + 0.4, n, 0.32, eave + 1.15, eave + 1.2, 8, 'dark', { tag: 'roof.unit' }); }
      box(C, -1.5, 3.5, 9.0, 10.6, eave, eave + 1.0, 'steel', { tag: 'roof.condenser', s: { mat: 'steel', tex: TEX.grille } }); for (const e of [-0.6, 0.9, 2.4]) cyl(C, e, 9.8, 0.42, eave + 1.0, eave + 1.06, 10, 'dark', { tag: 'roof.fan' });
      box(C, 11.0, 12.0, 1.0, 2.0, eave, eave + 0.5, 'steel', { tag: 'roof.hatch' }); for (const e of [-13, -12.6]) cyl(C, e, 9.5, 0.08, eave, eave + 0.9, 6, 'steel', { tag: 'roof.vent' });
    }
    // THE TOWER: steps out 1 m (n -13), 9 m wide, its back at n -9.5; a gable to 10.0, a green standing-seam roof; the doors
    const tz = 8.2, tp = 10.0, tn0 = -hl - 1.0, tn1 = -hl + 2.5, te = 4.5;
    const Tf = wall(C, { a: [-te, tn0], b: [te, tn0], out: [0, -1], top: tz, eave: tz, gable: [te, tp], holes: [{ u0: 1.25, u1: 4.15, z0: fz, z1: 3.1 }, { u0: 4.85, u1: 7.75, z0: fz, z1: 3.1 }], tex: TEX.brick, t, tag: 'show.tower' });
    wall(C, { a: [te, tn0], b: [te, tn1], out: [1, 0], top: tz, eave: tz, tex: TEX.brick, t, tag: 'show.tower' }); wall(C, { a: [-te, tn1], b: [-te, tn0], out: [-1, 0], top: tz, eave: tz, tex: TEX.brick, t, tag: 'show.tower' });
    if (!cut) { wall(C, { a: [te, tn1], b: [-te, tn1], out: [0, 1], z0: eave, top: tz, eave: tz, gable: [te, tp], tex: TEX.brick, t, tag: 'show.tower' });
      for (const sg of [-1, 1]) { F(C, [[0, tn0 - 0.2, tp + 0.08], [0, tn1 + 0.1, tp + 0.08], [sg * (te + 0.25), tn1 + 0.1, tz - 0.12], [sg * (te + 0.25), tn0 - 0.2, tz - 0.12]].map(p => sg > 0 ? p : p), 'seam', { tag: 'roof.tower', tex: TEX.seam, uv: [[0, 0], [3.8, 0], [3.8, 5], [0, 5]] });
        stick(C, [0, tn0 - 0.12, tp + 0.02], [sg * (te + 0.2), tn0 - 0.12, tz - 0.1], 0.26, 'cream', { tag: 'show.coping' }); }
      box(C, -te - 0.12, te + 0.12, tn0 - 0.18, tn0 + 0.02, tz - 0.25, tz + 0.02, 'cream', { tag: 'show.band' }); box(C, -te - 0.1, te + 0.1, tn0 - 0.14, tn0 + 0.02, 3.3, 3.7, 'cream', { tag: 'show.band', noTop: false });
      // the cream panel and GROCERY; a round cream vent in the gable; the green canopy band and its lights
      box(C, -4.0, 4.0, tn0 - 0.12, tn0 + 0.01, 4.25, 6.45, 'stucco', { tag: 'show.panel', tex: TEX.stucco, s: { mat: 'stucco', tex: TEX.stucco }, noN: true });
      letters(C, 'GROCERY', 0, tn0 - 0.12, 4.62, 5, 'green', { d: 0.12, em: 'neon:green', tag: 'show.sign' });
      cyl(C, 0, tn0 - 0.06, 0.55, 8.55, 8.62, 14, 'cream', { tag: 'show.oculus' }); 
      box(C, -4.4, 4.4, tn0 - 0.9, tn0, 3.12, 3.72, 'green', { tag: 'show.canopy', top: 'roof' }); for (const e of [-3, -1, 1, 3]) box(C, e - 0.2, e + 0.2, tn0 - 0.7, tn0 - 0.4, 3.08, 3.12, 'enamel', { tag: 'show.light', em: 'glow:white', b: 1 });
      lamp(C, 0, tn0 - 0.6, 3.0, '#fff4e2', 0.7, 3.6); }
    if (!Tf.drop) { slider(C, -1.55, tn0, 2.9, fz, 2.55, { open: s.doorOpen }); slider(C, 1.55, tn0, 2.9, fz, 2.55, { open: s.doorOpen });
      for (const e of [-1.55, 1.55]) F(C, [[e - 1.45, tn0 - 0.02, 2.67], [e + 1.45, tn0 - 0.02, 2.67], [e + 1.45, tn0 - 0.02, 3.08], [e - 1.45, tn0 - 0.02, 3.08]], 'glass', { tag: 'door.transom', em: 'win:door' }); }
    else for (const e of [-1.55, 1.55]) flat(C, e - 1.45, e + 1.45, tn0 - 0.02, tn0 + t, fz + 0.005, 'frame', { tag: 'entry.threshold' });
    // bollards before the doors, yellow; the vestibule's inner doors
    for (const e of [-3.9, -2.3, 2.3, 3.9]) { cyl(C, e, tn0 - 1.4, 0.11, fz, cut ? Math.min(1.05, ph) : 1.05, 8, 'yel', { tag: 'show.bollard' }); }
    // ---- THE SECTION: the floors, the partitions, the furniture, the lights --------------------------------------------------------
    if (cut) {
      const ix0 = -hw + t, ix1 = hw - t, iy0 = -hl + t, iy1 = hl - t, pn = 7.0;
      F(C, [[ix0, iy0, fz], [ix1, iy0, fz], [ix1, pn, fz], [ix0, pn, fz]], 'tile', { tag: 'store.floor', tex: TEX.tile, uv: [[ix0, iy0], [ix1, iy0], [ix1, pn], [ix0, pn]] });
      F(C, [[-te + t, tn0 + t, fz], [te - t, tn0 + t, fz], [te - t, iy0, fz], [-te + t, iy0, fz]], 'tile', { tag: 'store.floor', tex: TEX.tile, uv: [[-te, tn0], [te, tn0], [te, iy0], [-te, iy0]] });
      F(C, [[ix0, pn, fz], [ix1, pn, fz], [ix1, iy1, fz], [ix0, iy1, fz]], 'concF', { tag: 'store.floorBack', tex: TEX.concF, uv: [[ix0, pn], [ix1, pn], [ix1, iy1], [ix0, iy1]] });
      const part = (e0, e1, n0, n1) => { box(C, e0, e1, n0, n1, fz, ph, 'white', { tag: 'store.partition', noTop: true }); box(C, e0 - 0.012, e1 + 0.012, n0 - 0.012, n1 + 0.012, ph, ph + 0.012, 'cap', { tag: 'cut.cap' }); };
      // the partition between the floor and the back, two staff doors; the vestibule's inner wall
      part(ix0, -13.4, pn, pn + 0.15); part(-11.8, 0.6, pn, pn + 0.15); part(2.4, ix1, pn, pn + 0.15);
      part(-te + t, -3.4, iy0 + 2.3, iy0 + 2.42); part(3.4, te - t, iy0 + 2.3, iy0 + 2.42);
      // rooms at the back: stock room (W), walk-in cooler and freezer, staff room, office, washroom (E); 10-06: mirrored with the side dock east,
      // the stock room by its wing (the receiving), the staff rooms west
      const backRooms = (box, part, plate, stick) => {
      part(4.0, 4.15, pn + 0.15, iy1); part(7.4, 7.55, pn + 0.15, iy1); part(10.2, 10.35, pn + 0.15, iy1); part(10.35, ix1, 9.1, 9.25);
      part(12.6, 12.75, pn + 0.15, 9.1);
      // walk-ins: insulated boxes with their doors
      for (const [e0, e1, nm, mat] of [[4.15, 7.4, 'COOLER', 'prodW'], [7.55, 10.2, 'FREEZER', 'blue']]) { box(C, e0 + 0.1, e1 - 0.1, pn + 0.35, iy1 - 0.1, fz, fz + 2.4, 'enamel', { tag: 'store.walkin', s: { mat: 'enamel', tex: TEX.seam }, top: 'white' });
        box(C, (e0 + e1) / 2 - 0.5, (e0 + e1) / 2 + 0.5, pn + 0.3, pn + 0.36, fz, fz + 2.1, mat, { tag: 'store.walkinDoor', noN: true }); plate(C, e0 + 0.3, e1 - 0.3, pn + 0.29, fz + 2.15, fz + 2.15 + textH(1), 'dark', { tag: 'store.sign', tex: textTex(nm, (e0 + e1) / 2, fz + 2.15, 1) }); }
      // the stock room: racking along the west wall and the partition, pallets staged by the docks, a pallet jack, the baler, carts
      for (const [e0, e1, n0, n1] of (dkS ? [[ix0 + 2.5, ix0 + 8.5, iy1 - 1.15, iy1 - 0.1]] : [[ix0, ix0 + 1.1, pn + 0.4, iy1 - 0.1]]).concat([[-3.8, 3.6, pn + 0.25, pn + 1.3]])) { const ns = n0, ne = n1;
        for (let l = 0; l < 3; l++) { const z = fz + l * 1.25; box(C, e0, e1, ns, ne, z + 1.1, z + 1.18, 'prodO', { tag: 'store.rackBeam' });
          for (let e = e0 + 0.05; e < e1 - 0.5; e += 1.25) box(C, e, Math.min(e + 1.15, e1 - 0.05), ns + 0.05, ne - 0.05, z + (l ? 1.18 : 0), z + (l ? 1.18 : 0) + 0.12, 'cord', { tag: 'store.pallet', tex: TEX.boards }); 
          for (let e = e0 + 0.05; e < e1 - 0.5; e += 1.25) box(C, e + 0.05, Math.min(e + 1.1, e1 - 0.1), ns + 0.1, ne - 0.1, z + (l ? 1.3 : 0.12), z + (l ? 1.3 : 0.12) + 0.85 - (hsh(e * 7, l, 3) < 0.3 ? 0.35 : 0), 'cord', { tag: 'store.cases', tex: TEX.box, s: { mat: 'cord', tex: TEX.box } }); }
        for (const e of [e0, e1]) box(C, e - 0.04, e + 0.04, ns, ne, fz, fz + 3.8, 'blue', { tag: 'store.rackPost' }); }
      for (const [e, n] of [[-12.2, 8.0], [-8.5, 8.2], [-4.6, 9.4]]) { box(C, e - 0.55, e + 0.55, n - 0.6, n + 0.6, fz, fz + 0.14, 'cord', { tag: 'store.pallet', tex: TEX.boards }); box(C, e - 0.5, e + 0.5, n - 0.55, n + 0.55, fz + 0.14, fz + 1.0 + hsh(e * 3, n, 5) * 0.5, 'cord', { tag: 'store.cases', tex: TEX.box, s: { mat: 'cord', tex: TEX.box } }); }
      const jn = dkS ? -1.0 : 0; box(C, -8.8 + (dkS ? 2.8 : 0), -8.3 + (dkS ? 2.8 : 0), 9.6 + jn, 10.9 + jn, fz, fz + 0.1, 'red', { tag: 'store.jack' }); stick(C, [-8.55 + (dkS ? 2.8 : 0), 9.6 + jn, fz + 0.1], [-8.55 + (dkS ? 2.8 : 0), 9.3 + jn, fz + 1.05], 0.06, 'red', { tag: 'store.jack' });
      box(C, 1.2, 3.4, iy1 - 1.4, iy1 - 0.1, fz, fz + 2.5, 'green', { tag: 'store.baler', s: { mat: 'green', tex: textTex('BALER', 2.3, fz + 1.9, 1) }, top: 'steel' });
      for (const e of [-2.0, -1.2]) { box(C, e - 0.3, e + 0.3, 9.4, 10.6, fz + 0.15, fz + 0.2, 'steel', { tag: 'store.uboat' }); box(C, e - 0.28, e + 0.28, 8.05, 9.15, fz + 0.2, fz + 0.9, 'cord', { tag: 'store.cases', tex: TEX.box }); }
      if (!dkS) plate(C, -9.6, -7.4, iy1 - 0.02, fz + 3.4, fz + 2.4 + textH(1) + 0.06, 'yel', { tag: 'store.sign', tex: textTex('RECEIVING', -8.5, fz + 3.43, 1) });
      // the staff room: a table and four chairs, lockers, a counter with the sink, fridge and microwave, a sofa, the board
      box(C, 11.6, 13.6, 9.9, 10.9, fz, fz + 0.74, 'wood', { tag: 'staff.table', top: 'cream' });
      for (const [e, n] of [[11.9, 9.6], [13.3, 9.6], [11.9, 11.2], [13.3, 11.2]]) box(C, e - 0.2, e + 0.2, n - 0.2, n + 0.2, fz, fz + 0.45, 'red', { tag: 'staff.chair' });
      for (let k = 0; k < 4; k++) box(C, 10.45 + k * 0.42, 10.85 + k * 0.42, iy1 - 0.45, iy1 - 0.05, fz, fz + 1.85, k & 1 ? 'blue' : 'steel', { tag: 'staff.locker', s: { mat: k & 1 ? 'blue' : 'steel', tex: TEX.slats } });
      box(C, ix1 - 0.6, ix1, 9.4, iy1 - 0.1, fz, fz + 0.92, 'wood', { tag: 'staff.counter', top: 'cream' }); box(C, ix1 - 0.55, ix1 - 0.05, 9.45, 9.95, fz, fz + 1.75, 'white', { tag: 'staff.fridge' });
      box(C, ix1 - 0.5, ix1 - 0.1, 10.5, 10.9, fz + 0.92, fz + 1.2, 'dark', { tag: 'staff.microwave' });
      // the office and the washroom
      box(C, 10.6, 12.2, 7.4, 8.1, fz, fz + 0.75, 'wood', { tag: 'office.desk', top: 'wood' }); box(C, 11.0, 11.5, 7.6, 7.9, fz + 0.75, fz + 1.1, 'dark', { tag: 'office.screen', s: { mat: 'glass', em: 'glow:cool' } }); box(C, 10.45, 10.85, 8.3, 8.9, fz, fz + 1.5, 'steel', { tag: 'office.files' });
      box(C, 13.8, 14.3, 7.4, 8.1, fz, fz + 0.45, 'white', { tag: 'wc.toilet' }); box(C, ix1 - 0.5, ix1, 8.3, 8.9, fz + 0.75, fz + 0.9, 'white', { tag: 'wc.basin' });
      };
      if (mirror) backRooms(mbox, (a, b, c, d) => part(-b, -a, c, d), mplate, mstick); else backRooms(box, part, plate, stick);
      // ---- the sales floor
      // the vestibule: carts nested along its west side
      for (let k = 0; k < 6; k++) { const e = -3.6 + k * 0.14; box(C, e, e + 0.5, tn0 + 0.55, tn0 + 1.45, fz + 0.15, fz + 0.95, 'steel', { tag: 'store.cart', tex: TEX.grille, top: 'steel', topTex: TEX.grille }); }
      // four lanes (W of the vestibule), the service desk (E), the customer area between
      [[-13.0, 4], [-10.6, 3], [-8.2, 2], [-5.8, 1]].forEach(([e, k]) => checkout(C, e, -11.2, -7.6, fz, k, { st: true }));
      box(C, 5.2, 9.6, -11.0, -10.2, fz, fz + 1.05, 'wood', { tag: 'store.service', top: 'cream', s: { mat: 'green' } }); box(C, 8.8, 9.6, -10.2, -8.4, fz, fz + 1.05, 'wood', { tag: 'store.service', top: 'cream', ef: { mat: 'green' } });
      box(C, 5.6, 7.4, -hl + t + 0.05, -hl + t + 0.55, fz, fz + 2.0, 'shelf', { tag: 'store.backbar', s: { mat: 'prodB', tex: TEX.box } });
      box(C, 7.6, 8.4, -hl + t + 0.05, -hl + t + 0.55, fz, fz + 1.6, 'dark', { tag: 'store.lottery', s: { mat: 'glass', em: 'glow:cool' } });
      plate(C, 5.2, 9.6, -11.0 - 0.01, fz + 1.12, fz + 1.12 + textH(1) + 0.04, 'green', { tag: 'store.sign', tex: textTex('SERVICE', 7.4, fz + 1.14, 1) });
      // produce: four tables, a wet rack along the east wall, a banana stand
      const PR = [['prodR', 'produce'], ['prodO', 'produce'], ['leaf', 'produce'], ['prodY', 'produce'], ['prodG', 'produce'], ['red', 'produce'], ['prodBr', 'produce'], ['leaf', 'produce']];
      [[5.0, -6.0], [8.4, -6.0], [5.0, -3.4], [8.4, -3.4]].forEach(([e, n], k) => { box(C, e - 1.3, e + 1.3, n - 0.6, n + 0.6, fz, fz + 0.62, 'wood', { tag: 'produce.table', tex: TEX.boards, s: { mat: 'wood', tex: TEX.boards } });
        for (let j = 0; j < 4; j++) { const [m, tx] = PR[(k * 2 + j) % PR.length], e0 = e - 1.25 + j * 0.62; F(C, [[e0, n - 0.55, fz + 0.66], [e0 + 0.58, n - 0.55, fz + 0.66], [e0 + 0.58, n + 0.55, fz + 0.92], [e0, n + 0.55, fz + 0.92]], m, { tag: 'produce', tex: TEX[tx], uv: [[e0, 0], [e0 + 0.58, 0], [e0 + 0.58, 1.2], [e0, 1.2]] }); } });
      box(C, ix1 - 0.9, ix1, -9.4, 0.2, fz, fz + 2.0, 'enamel', { tag: 'produce.wetrack', wf: { mat: 'leaf', tex: TEX.produce, em: 'glow:cool' }, top: 'green' });
      for (let k = 0; k < 6; k++) { const n0 = -9.3 + k * 1.58; F(C, [[ix1 - 0.9, n0, fz + 0.5], [ix1 - 0.9, n0 + 1.5, fz + 0.5], [ix1 - 0.3, n0 + 1.5, fz + 1.4], [ix1 - 0.3, n0, fz + 1.4]], ['leaf', 'prodG', 'prodO', 'leaf', 'prodR', 'prodY'][k], { tag: 'produce', tex: TEX.produce, uv: [[n0, 0], [n0 + 1.5, 0], [n0 + 1.5, 1], [n0, 1]] }); }
      plate(C, ix1 - 0.95, ix1 - 0.94, -9.4, fz + 2.05, fz + 2.4, 'green', { tag: 'store.sign' });
      [[5.0, -0.8], [8.4, -0.8]].forEach(([e, n], k) => { box(C, e - 1.3, e + 1.3, n - 0.6, n + 0.6, fz, fz + 0.62, 'wood', { tag: 'produce.table', tex: TEX.boards, s: { mat: 'wood', tex: TEX.boards } });
        for (let j = 0; j < 4; j++) { const m = ['prodY', 'leaf', 'prodR', 'prodO', 'prodG', 'prodBr', 'red', 'leaf'][(k * 4 + j) % 8], e0 = e - 1.25 + j * 0.62; F(C, [[e0, n - 0.55, fz + 0.66], [e0 + 0.58, n - 0.55, fz + 0.66], [e0 + 0.58, n + 0.55, fz + 0.92], [e0, n + 0.55, fz + 0.92]], m, { tag: 'produce', tex: TEX.produce, uv: [[e0, 0], [e0 + 0.58, 0], [e0 + 0.58, 1.2], [e0, 1.2]] }); } });
      plate(C, 4.0, 9.4, -6.62, fz + 2.55, fz + 2.55 + textH(1) + 0.06, 'leaf', { tag: 'store.sign', tex: textTex('FRESH PRODUCE', 6.7, fz + 2.58, 1) });
      for (const [e, n, m] of [[1.0, -6.4, 'prodB'], [1.0, -4.4, 'red'], [1.0, -2.4, 'prodY'], [-0.2, 4.6, 'prodO']]) { box(C, e - 0.6, e + 0.6, n - 0.6, n + 0.6, fz, fz + 0.14, 'cord', { tag: 'store.pallet', tex: TEX.boards });
        box(C, e - 0.56, e + 0.56, n - 0.56, n + 0.56, fz + 0.14, fz + 1.15 + hsh(e * 9, n * 3, 4) * 0.35, m, { tag: 'store.palletDisplay', tex: TEX.box, s: { mat: m, tex: TEX.box } }); box(C, e - 0.3, e + 0.3, n - 0.62, n - 0.58, fz + 1.2, fz + 1.5, 'prodY', { tag: 'store.saleTag', s: { mat: 'prodY', tex: textTex('SALE', e, fz + 1.26, 1) } }); }
      // deli and bakery along the east wall's north part and the partition
      box(C, 9.6, 13.6, 1.6, 2.5, fz, fz + 1.25, 'enamel', { tag: 'deli.case', s: { mat: 'glass', em: 'glow:cool' }, top: 'glass' });
      for (let k = 0; k < 5; k++) box(C, 9.75 + k * 0.76, 10.35 + k * 0.76, 1.7, 2.3, fz + 0.55, fz + 0.85, ['meat', 'prodY', 'prodW', 'prodO', 'meat'][k], { tag: 'deli.food', tex: TEX.produce, noN: true });
      box(C, 13.6, ix1, 1.6, 5.6, fz, fz + 1.25, 'enamel', { tag: 'deli.case', wf: { mat: 'glass', em: 'glow:cool' }, top: 'glass' });
      plate(C, 9.6, 13.6, 1.59, fz + 1.3, fz + 1.3 + textH(1) + 0.04, 'green', { tag: 'store.sign', tex: textTex('DELI', 11.6, fz + 1.32, 1) });
      box(C, 9.8, 13.4, 4.8, 5.6, fz, fz + 0.95, 'wood', { tag: 'deli.backcounter', top: 'steel' }); box(C, 10.2, 11.4, 5.0, 5.5, fz + 0.95, fz + 1.4, 'steel', { tag: 'deli.slicer' });
      for (const [e0, e1] of [[4.6, 6.6], [6.9, 8.9]]) { box(C, e0, e1, 5.9, 6.75, fz, fz + 1.75, 'wood', { tag: 'bakery.rack' }); for (let l = 0; l < 4; l++) box(C, e0 + 0.05, e1 - 0.05, 5.92, 6.4, fz + 0.15 + l * 0.42, fz + 0.4 + l * 0.42, 'prodBr', { tag: 'bakery.bread', tex: TEX.bag, s: { mat: 'prodBr', tex: TEX.bag }, noN: true }); }
      plate(C, 4.6, 8.9, 5.88, fz + 1.8, fz + 1.8 + textH(1) + 0.04, 'prodBr', { tag: 'store.sign', tex: textTex('BAKERY', 6.75, fz + 1.82, 1) });
      // the back wall of the floor: dairy (W) and meat (E) cases against the partition
      coolerBank(C, ix0 + 0.1, -2.0, pn - 0.85, pn - 0.05, fz, { label: 'DAIRY', band: 'blue', mats: ['prodW', 'prodW', 'prodB', 'prodY', 'prodW', 'prodR'] });
      box(C, -1.6, 4.2, pn - 0.95, pn - 0.05, fz, fz + 1.9, 'enamel', { tag: 'meat.case', s: { mat: 'dark' } });
      for (let l = 0; l < 3; l++) box(C, -1.5, 4.1, pn - 0.9 + l * 0.12, pn - 0.15, fz + 0.4 + l * 0.42, fz + 0.62 + l * 0.42, 'meat', { tag: 'meat.packs', tex: TEX.box, s: { mat: 'meat', tex: TEX.box, em: 'glow:cool' }, noN: true });
      box(C, -1.6, 4.2, pn - 1.0, pn - 0.9, fz + 1.9, fz + 2.2, 'red', { tag: 'meat.band', s: { mat: 'red', tex: textTex('MEAT', 1.3, fz + 1.95, 1) } });
      // the freezer bank (doors south) and two coffin cases
      coolerBank(C, -4.6, 2.2, 2.6, 3.4, fz, { label: 'FROZEN', band: 'blue', mats: ['prodB', 'prodW', 'prodR', 'prodB', 'prodY'], tex: 'box' });
      for (const e of [-9.0, -5.6]) { box(C, e - 1.6, e + 1.6, 2.5, 3.4, fz, fz + 0.9, 'white', { tag: 'store.coffin', top: 'glass', t: { mat: 'glass', em: 'glow:cool' } });
        for (let k = 0; k < 6; k++) flat(C, e - 1.5 + k * 0.5, e - 1.06 + k * 0.5, 2.6, 3.3, fz + 0.82, PMATS[(k + 2) % 8], { tag: 'store.frozen', tex: TEX.box }); }
      // three gondola runs, E-W, shelves on both faces, end caps
      for (const [n0, k] of [[-5.2, 0], [-2.2, 1], [0.8, 2]]) gondola(C, -13.4, -0.8, n0, n0 + 1.1, fz, { seed: 11 + k * 5, sides: 'SN', cap: true, h: 1.85, levels: 5 });
      // the aisle signs hung over the runs
      for (const [n, a] of [[-3.8, '1'], [-0.8, '2'], [2.0, '3']]) { box(C, -7.6, -6.4, n - 0.02, n + 0.02, fz + 2.55, fz + 2.95, 'green', { tag: 'store.aisleSign', s: { mat: 'green', tex: textTex('AISLE ' + a, -7.0, fz + 2.62, 1) } }); }
      // carts parked by the lanes, a mop bucket, the flowers' bucket stand by the door
      box(C, 3.4, 4.6, -11.1, -10.5, fz, fz + 0.9, 'wood', { tag: 'store.flowers', top: 'pink' }); for (let k = 0; k < 5; k++) cyl(C, 3.55 + k * 0.24, -10.8, 0.1, fz + 0.9, fz + 1.25, 6, ['pink', 'prodY', 'red', 'prodW', 'prodP'][k], { tag: 'store.flowers' });
      // the lights over the floor and the back
      for (let e = -12; e <= 12.1; e += 4) for (const n of [-9, -5, -1, 3]) lamp(C, e, n, 3.6, '#fff1d8', 0.26, 4.8);
      for (const [e, n] of [[-11, 9.5], [-6, 9.5], [-1, 9.5], [12, 10], [5.8, 9.5], [8.9, 9.5], [11.4, 8], [13.7, 8]]) lamp(C, mirror ? -e : e, n, 3.2, '#fff1d8', 0.24, 4.0);
      C.indoor = [-(hw - 0.1) - (mirror ? dkS.w : 0), hw - 0.1 + (dkS && !mirror ? dkS.w : 0), -(hl - 0.1), hl + 1.2, 0.12, 0.85];
    }
    C.door = { e: 0, n: tn0, z: fz, width: 6.0, height: 2.55, facing: 'S' };
    if (!dkS) C.st.push({ id: 'dock1', verb: 'unload', at: [hw - 25.5, hl, fz], stand: [hw - 25.5, hl - 1.6] }, { id: 'dock2', verb: 'unload', at: [hw - 21.5, hl, fz], stand: [hw - 21.5, hl - 1.6] });
    C.st.push({ id: 'doorIn', verb: 'enter', at: [-1.55, tn0, fz], stand: [-1.55, tn0 - 1.6] }, { id: 'doorOut', verb: 'exit', at: [1.55, tn0, fz], stand: [1.55, tn0 + 1.2] },
      { id: 'serviceDoor', verb: 'enter', at: [hw - 17.5, hl, fz], stand: [hw - 17.5, hl + 1.2] }, { id: 'service', verb: 'ask', at: [7.4, -10.2, 1.05], stand: [7.4, -9.4] },
      { id: 'staffRoom', verb: 'rest', at: [12.6, 10.4, fz], stand: [12.6, 9.2] }, { id: 'deli', verb: 'order', at: [11.6, 1.6, 1.2], stand: [11.6, 0.8] });
  };

  /* ================================================================================================================================
     THE LUMBER STORE (the building centre). 24 x 22 m: split-face block to 1.2 m, white ribbed panel above, a green fascia at the top;
     a green standing-seam gable 9 m wide that steps out 1.2 m and rises to 10.2 m, LUMBER in cream channel letters on it and HARDWARE
     in the peak; a timber truss canopy on round concrete columns along the whole front, its roof green standing seam; ENTRANCE and
     EXIT ONLY door pairs in the gable. Round the back: a dock door in its well and a grade door for the forklift.
     Inside (section): the doors and three lanes, the contractor desk, the paint counter and its wall, three tall gondola runs of
     hardware, millwork on the west wall; behind the partition the receiving room with its dock and racking, the staff room, the
     office and the washroom. ============================================================================================================ */
  B.lumber = function (C) {
    const s = C.s, hw = 12, hl = 11, fz = C.floor = 0.15, eave = 6.0, par = 6.6, t = 0.3, cut = s.cut, ph = fz + s.cutH;
    const wk = s.o.walk || 4, dkS = s.o.dock || null; C.dims = { w: 2 * hw, d: 2 * hl, h: 10.3, walk: wk };   // 10-06: o.dock, the side dock (dockWing)
    box(C, -hw - 0.06, hw + 0.06, -hl - 0.06, hl + 0.06, 0, fz, 'conc', { tag: 'wall.plinth', noTop: true });
    box(C, -hw - 0.5, hw + 0.5, -hl - wk, -hl, 0, fz, 'conc', { tag: 'walk', topTex: TEX.walk, s: { mat: 'yel' } });
    const gW = [{ u0: 0.8, u1: 6.4, z0: 1.2, z1: 3.4 }], gE = [{ u0: 17.6, u1: 23.2, z0: 1.2, z1: 3.4 }];
    const Wf = wall(C, { a: [-hw, -hl], b: [hw, -hl], out: [0, -1], top: par, eave, holes: gW.concat([{ u0: 7.5, u1: 16.5, z0: fz, z1: 3.4 }], gE), mat: 'white', tex: TEX.rib, t, tag: 'show.front' });
    for (const h of gW.concat(gE)) glazing(C, Wf, h, { bay: 1.4 });
    const dkU = { u0: 16.625, u1: 19.375, z0: fz, z1: fz + 3.05 }, grU = { u0: 11.2, u1: 14.8, z0: 0, z1: 3.6 }, mdU = { u0: 7.55, u1: 8.45, z0: fz, z1: 2.25 };
    const Wb = wall(C, { a: [hw, hl], b: [-hw, hl], out: [0, 1], top: par, eave, holes: dkS ? [mdU] : [dkU, grU, mdU], mat: 'white', tex: TEX.rib, t, tag: 'wall.back', inMat: 'block', inTex: TEX.split });
    const We = wall(C, { a: [hw, -hl], b: [hw, hl], out: [1, 0], top: par, eave, holes: dkS && dkS.side === 'E' ? [{ u0: hl + 5.6, u1: hl + 10.4, z0: fz, z1: 3.3 }] : [], mat: 'white', tex: TEX.rib, t, tag: 'wall.side' });
    const Ww = wall(C, { a: [-hw, hl], b: [-hw, -hl], out: [-1, 0], top: par, eave, holes: dkS && dkS.side !== 'E' ? [{ u0: 0.6, u1: 5.4, z0: fz, z1: 3.3 }] : [], mat: 'white', tex: TEX.rib, t, tag: 'wall.side' });
    const eDk = hw - 18, eGr = hw - 13;
    if (dkS) dockWing(C, hw, Object.assign({ n1: hl, mat: 'white', tex: TEX.rib, base: 1.2, fascia: 'seam', coping: 'seam', driveMat: 'seam', stock: 'lumber' }, dkS));
    else { box(C, eDk - 1.36, eDk + 1.36, hl - t + 0.04, hl - t + 0.1, fz, fz + 3.05, 'enamel', { tag: 'dock.door', tex: TEX.dock, nf: { mat: 'enamel', tex: TEX.dock } });
      flat(C, eDk - 1.1, eDk + 1.1, hl - t - 2.4, hl - t, fz + 0.012, 'steel', { tag: 'dock.leveler', tex: TEX.grille });
      box(C, eGr - 1.78, eGr + 1.78, hl - t + 0.04, hl - t + 0.1, 0.02, 3.6, 'seam', { tag: 'door.grade', tex: TEX.dock, nf: { mat: 'seam', tex: TEX.dock } }); }
    box(C, hw - 8.45, hw - 7.55, hl - t + 0.05, hl - t + 0.1, fz, 2.25, 'steel', { tag: 'door.service' });
    // the block base on every face, the fascia, the coping
    for (const [q, L] of [[Wf, 2 * hw], [We, 2 * hl], [Ww, 2 * hl], [Wb, 2 * hw]]) { const zt = cut && q.drop ? Math.min(1.2, ph) : 1.2;
      const holesQ = q === Wf ? [[7.5, 16.5]] : q === Wb ? (dkS ? [[mdU.u0, mdU.u1]] : [[dkU.u0, dkU.u1], [grU.u0, grU.u1], [mdU.u0, mdU.u1]]) : q === Ww && dkS && dkS.side !== 'E' ? [[0.6, 5.4]] : []; let segs = [[0, L]];
      for (const [a, b] of holesQ) segs = segs.flatMap(([s0, s1]) => (b <= s0 || a >= s1) ? [[s0, s1]] : [[s0, a], [b, s1]].filter(p => p[1] - p[0] > 1e-3));
      for (const [s0, s1] of segs) fbox(C, q.M, s0, s1, 0, zt, 0, 0.06, 'block', { tag: 'wall.base', tex: TEX.split }); }
    if (!cut) {
      for (const [q, L] of [[Wf, 2 * hw], [We, 2 * hl], [Ww, 2 * hl], [Wb, 2 * hw]]) fbox(C, q.M, 0, L, eave - 0.05, par, 0, 0.1, 'seam', { tag: 'show.fascia', tex: TEX.seam });
      box(C, -hw - 0.12, hw + 0.12, -hl - 0.12, -hl + 0.2, par, par + 0.08, 'seam', { tag: 'roof.coping' }); box(C, -hw - 0.12, hw + 0.12, hl - 0.2, hl + 0.12, par, par + 0.08, 'seam', { tag: 'roof.coping' });
      box(C, -hw - 0.12, -hw + 0.2, -hl, hl, par, par + 0.08, 'seam', { tag: 'roof.coping' }); box(C, hw - 0.2, hw + 0.12, -hl, hl, par, par + 0.08, 'seam', { tag: 'roof.coping' });
      F(C, [[-hw + t, -hl + t, eave], [hw - t, -hl + t, eave], [hw - t, hl - t, eave], [-hw + t, hl - t, eave]], 'roof', { tag: 'roof', tex: TEX.membrane, uv: [[-hw, -hl], [hw, -hl], [hw, hl], [-hw, hl]] });
      for (const [q, L] of [[Wf, 2 * hw], [Wb, 2 * hw], [We, 2 * hl], [Ww, 2 * hl]]) F(C, [q.M(0, eave, -t), q.M(L, eave, -t), q.M(L, par, -t), q.M(0, par, -t)], 'white', { tag: 'roof.parapet', b: -1 });
      for (const [e, n] of [[-7, 4], [1, 7], [7, 2]]) { box(C, e - 1.0, e + 1.0, n - 0.75, n + 0.75, eave, eave + 1.05, 'steel', { tag: 'roof.unit', s: { mat: 'steel', tex: TEX.grille } }); cyl(C, e + 0.35, n, 0.3, eave + 1.05, eave + 1.1, 8, 'dark', { tag: 'roof.unit' }); }
      for (const e of [-10, -9.6, 9.8]) cyl(C, e, 9.5, 0.08, eave, eave + 0.9, 6, 'steel', { tag: 'roof.vent' });
    }
    // THE GABLE: steps out 1.2 m (n -12.2), 9 m wide, back at n -8; the doors in its face; standing seam above the doors
    const gz = 6.6, gp = 10.2, gn0 = -hl - 1.2, gn1 = -hl + 3.0, ge = 4.5;
    const Gf = wall(C, { a: [-ge, gn0], b: [ge, gn0], out: [0, -1], top: gz, eave: gz, gable: [ge, gp], holes: [{ u0: 0.65, u1: 3.95, z0: fz, z1: 3.4 }, { u0: 5.05, u1: 8.35, z0: fz, z1: 3.4 }], mat: 'seam', tex: TEX.seam, t, tag: 'show.gable' });
    wall(C, { a: [ge, gn0], b: [ge, gn1], out: [1, 0], top: gz, eave: gz, mat: 'seam', tex: TEX.seam, t, tag: 'show.gable' }); wall(C, { a: [-ge, gn1], b: [-ge, gn0], out: [-1, 0], top: gz, eave: gz, mat: 'seam', tex: TEX.seam, t, tag: 'show.gable' });
    if (!cut) { wall(C, { a: [ge, gn1], b: [-ge, gn1], out: [0, 1], z0: eave, top: gz, eave: gz, gable: [ge, gp], mat: 'seam', tex: TEX.seam, t, tag: 'show.gable' });
      for (const sg of [-1, 1]) F(C, [[0, gn0 - 0.3, gp + 0.1], [0, gn1 + 0.1, gp + 0.1], [sg * (ge + 0.35), gn1 + 0.1, gz - 0.2], [sg * (ge + 0.35), gn0 - 0.3, gz - 0.2]], 'seam', { tag: 'roof.gable', tex: TEX.seam, uv: [[0, 0], [4.5, 0], [4.5, 5], [0, 5]] });
      for (const sg of [-1, 1]) stick(C, [0, gn0 - 0.28, gp + 0.06], [sg * (ge + 0.3), gn0 - 0.28, gz - 0.18], 0.2, 'cream', { tag: 'show.barge' });
      letters(C, 'LUMBER', 0, gn0, 4.75, 5, 'cream', { d: 0.12, em: 'neon:cream', tag: 'show.sign' });
      letters(C, 'HARDWARE', 0, gn0, 7.15, 2, 'cream', { d: 0.08, em: 'neon:cream', tag: 'show.sign' }); }
    if (!Gf.drop) { slider(C, -2.3, gn0, 3.1, fz, 2.6, { open: s.doorOpen }); slider(C, 2.3, gn0, 3.1, fz, 2.6, { open: s.doorOpen });
      for (const e of [-2.3, 2.3]) F(C, [[e - 1.55, gn0 - 0.02, 2.72], [e + 1.55, gn0 - 0.02, 2.72], [e + 1.55, gn0 - 0.02, 3.35], [e - 1.55, gn0 - 0.02, 3.35]], 'glass', { tag: 'door.transom', em: 'win:door' });
      plate(C, -3.4, -1.2, gn0 - 0.05, 2.78, 2.78 + textH(1) + 0.04, 'green', { tag: 'door.sign', tex: textTex('ENTRANCE', -2.3, 2.8, 1) });
      plate(C, 1.1, 3.5, gn0 - 0.05, 2.78, 2.78 + textH(1) + 0.04, 'red', { tag: 'door.sign', tex: textTex('EXIT ONLY', 2.3, 2.8, 1) }); }
    else for (const e of [-2.3, 2.3]) flat(C, e - 1.55, e + 1.55, gn0 - 0.02, gn0 + t, fz + 0.005, 'frame', { tag: 'entry.threshold' });
    // THE TRUSS CANOPY: two timber chords and a Warren web along the front, round concrete columns, a standing-seam roof
    const cn = -hl - 2.8;
    for (const e of [-hw, -7.2, 7.2, hw]) { cyl(C, e, cn + 0.05, 0.3, fz, cut ? Math.min(3.5, ph) : 3.5, 12, 'conc', { tag: 'canopy.column', noTop: cut }); if (cut) cyl(C, e, cn + 0.05, 0.305, ph, ph + 0.012, 12, 'cap', { tag: 'cut.cap' }); }
    if (!cut) {
      box(C, -hw - 0.2, hw + 0.2, cn - 0.14, cn + 0.14, 4.62, 4.86, 'glulam', { tag: 'canopy.truss' }); box(C, -hw - 0.2, hw + 0.2, cn - 0.14, cn + 0.14, 3.5, 3.74, 'glulam', { tag: 'canopy.truss' });
      for (let k = 0, e = -hw; e < hw - 0.1; k++, e += 1.2) { stick(C, [e, cn, k & 1 ? 4.62 : 3.74], [e + 1.2, cn, k & 1 ? 3.74 : 4.62], 0.16, 'glulam', { tag: 'canopy.web' }); if (!(k % 5)) box(C, e - 0.07, e + 0.07, cn - 0.1, cn + 0.1, 3.74, 4.62, 'glulam', { tag: 'canopy.post' }); }
      for (let e = -hw + 0.6; e < hw; e += 2.4) box(C, e - 0.07, e + 0.07, cn, -hl, 4.36, 4.5, 'glulam', { tag: 'canopy.joist' });
      F(C, [[-hw - 0.2, cn + 0.12, 4.52], [hw + 0.2, cn + 0.12, 4.52], [hw + 0.2, -hl, 4.8], [-hw - 0.2, -hl, 4.8]], 'seam', { tag: 'canopy.roof', tex: TEX.seam, uv: [[-hw, 0], [hw, 0], [hw, 2.8], [-hw, 2.8]] });
      for (const e of [-9, -4.8, 0, 4.8, 9]) { box(C, e - 0.2, e + 0.2, cn + 0.9, cn + 1.3, 4.3, 4.34, 'enamel', { tag: 'canopy.light', em: 'glow:white', b: 1 }); lamp(C, e, cn + 1.1, 4.2, '#fff4e2', 0.5, 3.6); }
    }
    // ---- THE SECTION ----------------------------------------------------------------------------------------------------------------
    if (cut) {
      const ix0 = -hw + t, ix1 = hw - t, iy0 = -hl + t, iy1 = hl - t, pn = 5.0;
      F(C, [[ix0, iy0, fz], [ix1, iy0, fz], [ix1, pn, fz], [ix0, pn, fz]], 'concF', { tag: 'store.floor', tex: TEX.concF, uv: [[ix0, iy0], [ix1, iy0], [ix1, pn], [ix0, pn]] });
      F(C, [[-ge + t, gn0 + t, fz], [ge - t, gn0 + t, fz], [ge - t, iy0, fz], [-ge + t, iy0, fz]], 'concF', { tag: 'store.floor', tex: TEX.concF, uv: [[-ge, gn0], [ge, gn0], [ge, iy0], [-ge, iy0]] });
      F(C, [[ix0, pn, fz], [ix1, pn, fz], [ix1, iy1, fz], [ix0, iy1, fz]], 'concF', { tag: 'store.floorBack', tex: TEX.concF, uv: [[ix0, pn], [ix1, pn], [ix1, iy1], [ix0, iy1]] });
      const part = (e0, e1, n0, n1) => { box(C, e0, e1, n0, n1, fz, ph, 'white', { tag: 'store.partition', noTop: true }); box(C, e0 - 0.012, e1 + 0.012, n0 - 0.012, n1 + 0.012, ph, ph + 0.012, 'cap', { tag: 'cut.cap' }); };
      part(ix0, -9.6, pn, pn + 0.15); part(-7.4, 2.6, pn, pn + 0.15); part(4.4, ix1, pn, pn + 0.15);
      part(4.0, 4.15, pn + 0.15, iy1); part(8.0, 8.15, pn + 0.15, iy1); part(8.15, ix1, 7.9, 8.05);
      // receiving: tall racks of stock on pallets, lumber and sheet goods in a cantilever rack, a pallet jack, a forklift's parking box
      for (const [e0, e1, n0, n1] of (dkS ? [[ix0 + 0.2, ix0 + 6.2, iy1 - 1.2, iy1 - 0.1]] : [[ix0, ix0 + 1.1, pn + 0.4, iy1 - 0.2]]).concat([[-9.0, -3.2, pn + 0.3, pn + 1.4]])) {
        for (let l = 0; l < 3; l++) { const z = fz + l * 1.35; box(C, e0, e1, n0, n1, z + 1.2, z + 1.28, 'prodO', { tag: 'store.rackBeam' });
          for (let e = e0 + 0.05; e < e1 - 0.5; e += 1.3) { box(C, e, Math.min(e + 1.2, e1 - 0.05), n0 + 0.05, n1 - 0.05, z + (l ? 1.28 : 0), z + (l ? 1.28 : 0) + 0.12, 'cord', { tag: 'store.pallet', tex: TEX.boards });
            box(C, e + 0.05, Math.min(e + 1.15, e1 - 0.1), n0 + 0.1, n1 - 0.1, z + (l ? 1.4 : 0.12), z + (l ? 1.4 : 0.12) + 0.9 - (hsh(e * 5, l, 9) < 0.35 ? 0.4 : 0), hsh(e * 3, l, 2) < 0.5 ? 'cord' : 'prodO', { tag: 'store.stock', tex: TEX.box, s: { mat: 'cord', tex: TEX.box } }); } }
        for (const e of [e0, e1]) box(C, e - 0.04, e + 0.04, n0, n1, fz, fz + 4.2, 'blue', { tag: 'store.rackPost' }); }
      for (let l = 0; l < 4; l++) box(C, -2.6, 2.8, iy1 - 1.0, iy1 - 0.1, fz + 0.1 + l * 0.32, fz + 0.36 + l * 0.32, l & 1 ? 'lumber' : 'cord', { tag: 'store.sheetgoods', s: { mat: 'lumber', tex: TEX.lumberEnd } });
      box(C, -6.0, -4.9, 7.8, 9.1, fz, fz + 0.12, 'red', { tag: 'store.jack' }); if (!dkS) flat(C, eGr - 1.8, eGr + 1.8, iy1 - 3.6, iy1, fz + 0.01, 'yel', { tag: 'store.lane', tex: TEX.hatch });
      if (!dkS) plate(C, -9.6, -7.0, iy1 - 0.02, fz + 2.5, fz + 2.5 + textH(1) + 0.06, 'yel', { tag: 'store.sign', tex: textTex('RECEIVING', -8.3, fz + 2.53, 1) });
      // the staff room, the office, the washroom
      box(C, 5.3, 7.1, 8.2, 9.2, fz, fz + 0.74, 'wood', { tag: 'staff.table', top: 'cream' }); for (const [e, n] of [[5.6, 7.9], [6.8, 7.9], [5.6, 9.5], [6.8, 9.5]]) box(C, e - 0.2, e + 0.2, n - 0.2, n + 0.2, fz, fz + 0.45, 'blue', { tag: 'staff.chair' });
      for (let k = 0; k < 5; k++) box(C, 4.3 + k * 0.42, 4.7 + k * 0.42, iy1 - 0.45, iy1 - 0.05, fz, fz + 1.85, k & 1 ? 'red' : 'steel', { tag: 'staff.locker', s: { mat: k & 1 ? 'red' : 'steel', tex: TEX.slats } });
      box(C, 7.3, 7.9, pn + 0.3, pn + 2.4, fz, fz + 0.92, 'wood', { tag: 'staff.counter', top: 'cream' }); box(C, 7.35, 7.85, pn + 2.5, pn + 3.0, fz, fz + 1.75, 'white', { tag: 'staff.fridge' });
      box(C, 8.6, 10.4, pn + 0.4, pn + 1.1, fz, fz + 0.75, 'wood', { tag: 'office.desk' }); box(C, 9.0, 9.5, pn + 0.55, pn + 0.85, fz + 0.75, fz + 1.1, 'dark', { tag: 'office.screen', s: { mat: 'glass', em: 'glow:cool' } });
      box(C, 10.6, 11.1, 8.4, 9.0, fz, fz + 0.45, 'white', { tag: 'wc.toilet' }); box(C, ix1 - 0.5, ix1, 9.6, 10.2, fz + 0.75, fz + 0.9, 'white', { tag: 'wc.basin' });
      // ---- the sales floor
      [[5.6, 3], [7.8, 2], [10.0, 1]].forEach(([e, k]) => checkout(C, e, -10.4, -7.2, fz, k, { st: true }));
      box(C, -11.2, -6.6, -10.4, -9.6, fz, fz + 1.05, 'wood', { tag: 'store.desk', top: 'cream', s: { mat: 'seam' } }); box(C, -11.2, -10.4, -9.6, -7.8, fz, fz + 1.05, 'wood', { tag: 'store.desk', top: 'cream', wf: { mat: 'seam' } });
      plate(C, -11.2, -6.6, -10.41, fz + 1.12, fz + 1.12 + textH(1) + 0.04, 'seam', { tag: 'store.sign', tex: textTex('CONTRACTOR DESK', -8.9, fz + 1.14, 1) });
      box(C, -8.4, -7.2, -hl + t + 0.05, -hl + t + 0.6, fz, fz + 1.9, 'shelf', { tag: 'store.keys', s: { mat: 'steel', tex: TEX.peg } });
      // the paint department along the east wall: the counter, the tinting machine, the can wall, the chip wall
      box(C, 6.4, 9.6, -2.6, -1.8, fz, fz + 1.0, 'wood', { tag: 'paint.counter', top: 'cream', s: { mat: 'prodB' } }); box(C, 8.6, 9.3, -1.7, -1.1, fz, fz + 1.4, 'white', { tag: 'paint.tinter' });
      box(C, ix1 - 0.6, ix1, -5.6, 3.8, fz, fz + 2.2, 'shelf', { tag: 'paint.wall', wf: { mat: 'white', tex: TEX.paintCans } });
      box(C, 6.0, 9.8, 3.6, 4.0, fz, fz + 2.0, 'white', { tag: 'paint.chips', s: { mat: 'white', tex: TEX.chips } });
      for (let k = 0; k < 4; k++) box(C, 6.2 + k * 0.85, 6.9 + k * 0.85, -0.9, -0.3, fz, fz + 0.5 + (k & 1) * 0.3, ['white', 'prodB', 'white', 'prodR'][k], { tag: 'paint.cans', tex: TEX.paintCans, s: { mat: ['white', 'prodB', 'white', 'prodR'][k], tex: TEX.paintCans } });
      plate(C, 6.4, 9.6, -2.61, fz + 1.05, fz + 1.05 + textH(1) + 0.04, 'prodB', { tag: 'store.sign', tex: textTex('PAINT', 8.0, fz + 1.07, 1) });
      // three tall gondola runs of hardware, E-W; tools on pegboard, bins, boxes
      const HM = ['red', 'steel', 'prodY', 'prodB', 'dark', 'prodO', 'prodG', 'yel'];
      for (const [n0, k, nm] of [[-6.0, 0, 'TOOLS'], [-3.0, 1, 'PLUMBING'], [0.0, 2, 'ELECTRICAL']]) { gondola(C, -10.6, 4.4, n0, n0 + 1.0, fz, { seed: 21 + k * 7, sides: 'SN', cap: true, h: 2.2, levels: 5, mats: HM, tex: k === 0 ? 'tools' : null, back: 'shelf' });
        box(C, -4.0, -2.0, n0 + 0.48, n0 + 0.52, fz + 2.6, fz + 3.0, 'seam', { tag: 'store.aisleSign', s: { mat: 'seam', tex: textTex(nm, -3.0, fz + 2.67, 1) } }); }
      // millwork on the west wall: doors on display, mouldings in a rack
      for (let k = 0; k < 4; k++) box(C, ix0, ix0 + 0.08, -6.4 + k * 1.1, -5.5 + k * 1.1, fz, fz + 2.05, ['wood', 'white', 'wood', 'white'][k], { tag: 'millwork.door', ef: { mat: ['wood', 'white', 'wood', 'white'][k], tex: TEX.boards } });
      box(C, ix0, ix0 + 0.5, -1.4, 3.6, fz, fz + 1.2, 'wood', { tag: 'millwork.rack', ef: { mat: 'lumber', tex: TEX.lumberEnd } });
      // a tool island by the doors, an ice-melt and BBQ-fuel stack, carts
      box(C, -5.6, -3.4, -8.6, -7.6, fz, fz + 1.1, 'shelf', { tag: 'store.island', s: { mat: 'red', tex: TEX.box }, top: 'yel' });
      for (let k = 0; k < 4; k++) { const e = -1.0 + k * 0.14; box(C, e, e + 0.5, -10.2, -9.2, fz + 0.15, fz + 0.95, 'steel', { tag: 'store.cart', tex: TEX.grille, top: 'steel', topTex: TEX.grille }); }
      for (let e = -10; e <= 10.1; e += 4) for (const n of [-8.5, -4.5, -0.5, 3]) lamp(C, e, n, 3.8, '#fff1d8', 0.26, 4.8);
      for (const [e, n] of [[-8, 8.5], [-1, 8.5], [6, 8.5], [10, 6.5], [10, 9.5]]) lamp(C, e, n, 3.2, '#fff1d8', 0.24, 4.0);
      C.indoor = [-(hw - 0.1) - (dkS && dkS.side === 'E' ? dkS.w : 0), hw - 0.1 + (dkS && dkS.side !== 'E' ? dkS.w : 0), -(hl - 0.1), hl + 1.4, 0.12, 0.85];
    }
    C.door = { e: 0, n: gn0, z: fz, width: 6.6, height: 2.6, facing: 'S' };
    if (!dkS) C.st.push({ id: 'dock', verb: 'unload', at: [eDk, hl, fz], stand: [eDk, hl - 1.6] }, { id: 'gradeDoor', verb: 'drive', at: [eGr, hl, 0], stand: [eGr, hl + 2] });
    C.st.push({ id: 'doorIn', verb: 'enter', at: [-2.3, gn0, fz], stand: [-2.3, gn0 - 1.6] }, { id: 'doorOut', verb: 'exit', at: [2.3, gn0, fz], stand: [2.3, gn0 + 1.2] },
      { id: 'desk', verb: 'order', at: [-8.9, -9.6, 1.05], stand: [-8.9, -8.8] }, { id: 'paint', verb: 'tint', at: [8.0, -1.8, 1.0], stand: [8.0, -3.2] }, { id: 'staffRoom', verb: 'rest', at: [6.2, 8.7, fz], stand: [6.2, 7.4] });
  };

  /* ================================================================================================================================
     THE DOCK: a truck well behind a store, its origin on the wall's outer face (n 0) at the middle of its doors, the well running
     north. The floor stands level 1.1 m down for 4 m by the doors, then ramps up to the yard over o.len; concrete retaining walls
     each side with a yellow pipe rail; at the wall a black seal and two bumpers round each door, a lamp on a swing arm. A semi's
     trailer bed (1.25 m) meets the store's floor (0.15 m) at the door. o.doors: e offsets; o.compactor: e of the compactor, or none. */
  B.dock = function (C) {
    const s = C.s, o = s.o, doors = o.doors || [0], len = o.len || 18, dz = o.depth || 1.1, lvl = 4, e0 = Math.min(...doors) - 2.4, e1 = Math.max(...doors) + 2.4;
    C.dims = { w: e1 - e0 + 0.6, d: len, h: 3.8 };
    const zAt = (n) => n <= lvl ? -dz : -dz + dz * (n - lvl) / (len - lvl);
    for (let n = 0; n < len - 1e-6; n += 1) { const n2 = Math.min(len, n + 1); F(C, [[e0, n, zAt(n)], [e1, n, zAt(n)], [e1, n2, zAt(n2)], [e0, n2, zAt(n2)]], 'conc', { tag: 'dock.floor', tex: TEX.walk, uv: [[e0, n], [e1, n], [e1, n2], [e0, n2]] }); }
    flat(C, e0, e1, 0.9, 1.1, -dz + 0.006, 'dark', { tag: 'dock.drain', tex: TEX.grille });
    for (const [ea, eb, sg] of [[e0 - 0.3, e0, -1], [e1, e1 + 0.3, 1]]) {
      for (let n = 0; n < len - 1e-6; n += 1) { const n2 = Math.min(len, n + 1), ein = sg < 0 ? eb : ea;
        F(C, [[ein, n, zAt(n)], [ein, n2, zAt(n2)], [ein, n2, 0.15], [ein, n, 0.15]].map(p => sg < 0 ? p : p), 'conc', { tag: 'dock.wall' }); }
      box(C, ea, eb, 0, len, 0, 0.15, 'conc', { tag: 'dock.wallTop', top: 'conc' });
      for (let n = 0.4; n < len - 0.5; n += 2) cyl(C, (ea + eb) / 2, n, 0.04, 0.15, 1.1, 6, 'yel', { tag: 'dock.rail' });
      stick(C, [(ea + eb) / 2, 0.4, 1.1], [(ea + eb) / 2, len - 0.6, 0.8 * 0 + 1.1], 0.07, 'yel', { tag: 'dock.rail' });
      stick(C, [(ea + eb) / 2, 0.4, 0.62], [(ea + eb) / 2, len - 0.6, 0.62], 0.05, 'yel', { tag: 'dock.rail' }); }
    for (const e of doors) {
      for (const sg of [-1, 1]) box(C, e + sg * 1.55 - 0.18, e + sg * 1.55 + 0.18, 0, 0.55, -0.4, 3.3, 'rubber', { tag: 'dock.seal' });
      box(C, e - 1.73, e + 1.73, 0, 0.55, 3.3, 3.7, 'rubber', { tag: 'dock.seal' });
      for (const sg of [-1, 1]) box(C, e + sg * 1.0 - 0.15, e + sg * 1.0 + 0.15, 0, 0.12, -0.1, 0.3, 'rubber', { tag: 'dock.bumper' });
      box(C, e - 1.4, e + 1.4, 0, 0.05, -dz, 0.15, 'conc', { tag: 'dock.face', noTop: true });
      stick(C, [e + 2.0, 0.05, 2.6], [e + 2.0, 0.9, 2.6], 0.05, 'steel', { tag: 'dock.light' }); box(C, e + 1.85, e + 2.15, 0.85, 1.1, 2.45, 2.65, 'yel', { tag: 'dock.light', em: 'glow:white' });
      box(C, e + 1.9, e + 2.25, 0.02, 0.14, 1.2, 1.6, 'steel', { tag: 'dock.controls', s: { mat: 'steel' }, t: { mat: 'red' } }); }
    if (o.compactor != null) { const e = o.compactor; box(C, e - 1.2, e + 1.2, 0.05, 3.7, 0, 1.9, 'green', { tag: 'dock.compactor', top: 'steel', s: { mat: 'green', tex: textTex('COMPACTOR', e, 1.3, 1) } });
      box(C, e - 1.0, e + 1.0, 0, 0.3, 0.8, 2.2, 'steel', { tag: 'dock.chute' }); }
    C.st.push(...doors.map((e, k) => ({ id: 'bay' + (k + 1), verb: 'dock', at: [e, 0, 0.15], stand: [e, len], truck: 'back in from the yard; the trailer stands in the well, its doors at the seal' })));
  };

  /* ================================================================================================================================
     THE SIDE DOCK (the east side and plaza pass, 10-06): a receiving wing on a store's side at the back, its doors in its south face,
     to the camera, at the store's floor, so a truck backs up to them on the level. Each truck door has a dock lift flush in the ground
     before it (the pallets ride it between the trailer's bed and the floor), a seal, two bumpers and a bay light (red: taken, green:
     free), its number beside it; a drive-in door (kind 'drive') takes the forklift or a pickup. A steel canopy over the doors, lit, and
     a man door. Inside (section): receiving, racking along the outer wall, pallets staged, a pallet jack, the RECEIVING sign; the wing
     opens into the store's back. d: { side 'E'|'W', w, n0 (the dock face), n1, doors [{ e, kind 'lift'|'drive', taken }], man (e),
     par, eave, mat, tex, band, base (block base height), fascia, coping, driveMat, stock }. Store-local metres. */
  function dockWing(C, hw, d) {
    const s = C.s, cut = s.cut, fz = C.floor, t = 0.3, ph = fz + s.cutH, E = d.side === 'E', eA = E ? hw : -hw - d.w, eB = E ? hw + d.w : -hw, n0 = d.n0, n1 = d.n1;
    const par = d.par || 5.6, eave = d.eave || 5.0, mat = d.mat || 'brick', tex = d.tex || TEX.brick, L = eB - eA, D = n1 - n0, mid = (eA + eB) / 2;
    const holes = d.doors.map(q => q.kind === 'drive' ? { u0: q.e - eA - 1.8, u1: q.e - eA + 1.8, z0: 0.02, z1: 4.2 } : { u0: q.e - eA - 1.6, u1: q.e - eA + 1.6, z0: fz, z1: fz + 3.6 });
    if (d.man != null) holes.push({ u0: d.man - eA - 0.45, u1: d.man - eA + 0.45, z0: fz, z1: 2.25 });
    box(C, eA - (E ? 0 : 0.06), eB + (E ? 0.06 : 0), n0 - 0.06, n1 + 0.06, 0, fz, 'conc', { tag: 'wall.plinth', noTop: true });
    const Ws = wall(C, { a: [eA, n0], b: [eB, n0], out: [0, -1], top: par, eave, holes, mat, tex, t, tag: 'dock.face' });
    const Wo = E ? wall(C, { a: [eB, n0], b: [eB, n1], out: [1, 0], top: par, eave, mat, tex, t, tag: 'wall.side' }) : wall(C, { a: [eA, n1], b: [eA, n0], out: [-1, 0], top: par, eave, mat, tex, t, tag: 'wall.side' });
    const Wn = wall(C, { a: [eB, n1], b: [eA, n1], out: [0, 1], top: par, eave, mat, tex, t, tag: 'wall.back', inMat: 'block', inTex: TEX.split });
    const walls = [[Ws, L], [Wo, D], [Wn, L]];
    if (d.base) for (const [q, LL] of walls) { const zt = cut && q.drop ? Math.min(d.base, ph) : d.base, hs = q === Ws ? holes.map(h => [h.u0, h.u1]) : []; let segs = [[0, LL]];
      for (const [a, b] of hs) segs = segs.flatMap(([s0, s1]) => (b <= s0 || a >= s1) ? [[s0, s1]] : [[s0, a], [b, s1]].filter(p => p[1] - p[0] > 1e-3));
      for (const [s0, s1] of segs) fbox(C, q.M, s0, s1, 0, zt, 0, 0.06, 'block', { tag: 'wall.base', tex: TEX.split }); }
    if (!cut) {
      if (d.band) for (const [q, LL] of walls) fbox(C, q.M, 0, LL, 4.3, 4.55, 0, 0.08, d.band, { tag: 'show.band', tex: TEX.band });
      if (d.fascia) for (const [q, LL] of walls) fbox(C, q.M, 0, LL, eave - 0.05, par, 0, 0.1, d.fascia, { tag: 'show.fascia', tex: TEX.seam });
      const cp = d.coping || 'cream', x0 = eA - (E ? 0 : 0.1), x1 = eB + (E ? 0.1 : 0);
      box(C, x0, x1, n0 - 0.1, n0 + 0.2, par, par + 0.1, cp, { tag: 'roof.coping' }); box(C, x0, x1, n1 - 0.2, n1 + 0.1, par, par + 0.1, cp, { tag: 'roof.coping' });
      if (E) box(C, eB - 0.2, eB + 0.1, n0, n1, par, par + 0.1, cp, { tag: 'roof.coping' }); else box(C, eA - 0.1, eA + 0.2, n0, n1, par, par + 0.1, cp, { tag: 'roof.coping' });
      const r0 = E ? eA : eA + t, r1 = E ? eB - t : eB;
      F(C, [[r0, n0 + t, eave], [r1, n0 + t, eave], [r1, n1 - t, eave], [r0, n1 - t, eave]], 'roof', { tag: 'roof', tex: TEX.membrane, uv: [[r0, n0], [r1, n0], [r1, n1], [r0, n1]] });
      for (const [q, LL] of walls) F(C, [q.M(0, eave, -t), q.M(LL, eave, -t), q.M(LL, par, -t), q.M(0, par, -t)], mat, { tag: 'roof.parapet', b: -1 });
      box(C, mid - 0.9, mid + 0.9, n0 + D * 0.62 - 0.7, n0 + D * 0.62 + 0.7, eave, eave + 1.0, 'steel', { tag: 'roof.unit', s: { mat: 'steel', tex: TEX.grille }, top: 'steel' }); cyl(C, mid + 0.35, n0 + D * 0.62, 0.3, eave + 1.0, eave + 1.05, 8, 'dark', { tag: 'roof.unit' });
      // the canopy over the doors, on its tie rods, lit underneath
      box(C, eA + (E ? 0 : 0.2), eB - (E ? 0.2 : 0), n0 - 1.8, n0, 4.75, 4.95, 'steel', { tag: 'dock.canopy', top: 'roof' });
      for (const e of [eA + (E ? 1.0 : 1.2), eB - (E ? 1.2 : 1.0)]) stick(C, [e, n0 - 0.02, 5.45], [e, n0 - 1.7, 4.97], 0.05, 'steel', { tag: 'dock.canopy' }); }
    d.doors.forEach((q, k) => { const e = q.e, drive = q.kind === 'drive', hwD = drive ? 1.8 : 1.6, z0 = drive ? 0.02 : fz, z1 = drive ? 4.2 : fz + 3.6, zt = cut ? Math.min(z1, ph) : z1, dm = drive ? (d.driveMat || 'seam') : 'enamel';
      box(C, e - hwD + 0.04, e + hwD - 0.04, n0 + 0.2, n0 + 0.26, z0, zt, dm, { tag: drive ? 'door.drive' : 'dock.door', tex: TEX.dock, s: { mat: dm, tex: TEX.dock } });
      if (!drive && zt > fz + 2.1) box(C, e - 1.3, e + 1.3, n0 + 0.17, n0 + 0.2, fz + 1.8, fz + 2.08, 'glass', { tag: 'dock.window' });
      if (!drive) {   // the dock lift, flush in the ground: its pit's frame, its platform, its hatched edges; the bumpers; the seal
        flat(C, e - 1.55, e + 1.55, n0 - 3.3, n0, 0.006, 'dark', { tag: 'dock.liftPit' }); flat(C, e - 1.4, e + 1.4, n0 - 3.15, n0 - 0.05, 0.012, 'steel', { tag: 'dock.lift', tex: TEX.grille });
        for (const [a, b, c, dd] of [[e - 1.4, e + 1.4, n0 - 3.15, n0 - 2.92], [e - 1.4, e - 1.18, n0 - 3.15, n0 - 0.05], [e + 1.18, e + 1.4, n0 - 3.15, n0 - 0.05]]) flat(C, a, b, c, dd, 0.016, 'yel', { tag: 'dock.liftEdge', tex: TEX.hatch });
        for (const sg of [-1, 1]) box(C, e + sg * 1.25 - 0.16, e + sg * 1.25 + 0.16, n0 - 0.14, n0, 0.95, 1.42, 'rubber', { tag: 'dock.bumper' });
        if (!cut) { for (const sg of [-1, 1]) box(C, e + sg * 1.8 - 0.17, e + sg * 1.8 + 0.17, n0 - 0.45, n0, 0.55, fz + 3.85, 'rubber', { tag: 'dock.seal' });
          box(C, e - 1.97, e + 1.97, n0 - 0.45, n0, fz + 3.85, fz + 4.2, 'rubber', { tag: 'dock.seal' }); } }
      else {   // the drive-in door: a hatched lane before it, its bollards
        flat(C, e - 1.8, e + 1.8, n0 - 4.0, n0 - 0.02, 0.012, 'yel', { tag: 'dock.lane', tex: TEX.hatch });
        for (const sg of [-1, 1]) cyl(C, e + sg * 2.1, n0 - 0.32, 0.12, 0, cut ? Math.min(1.1, ph) : 1.1, 8, 'yel', { tag: 'dock.bollard' }); }
      // the bay light (red: taken, green: free) and the bay's number, west of the door
      const lx = drive ? e - hwD - 0.45 : e - 2.35;
      box(C, lx - 0.16, lx + 0.16, n0 - 0.12, n0, 1.9, 2.62, 'dark', { tag: 'dock.bayLight' });
      box(C, lx - 0.1, lx + 0.1, n0 - 0.14, n0 - 0.12, 2.36, 2.54, 'red', { tag: 'dock.bayLight', em: q.taken ? 'led:red' : undefined });
      box(C, lx - 0.1, lx + 0.1, n0 - 0.14, n0 - 0.12, 2.0, 2.18, 'green', { tag: 'dock.bayLight', em: q.taken ? undefined : 'led:green' });
      if (!cut) { plate(C, lx - 0.3, lx + 0.3, n0 - 0.02, 2.8, 2.8 + textH(2) + 0.06, 'yel', { tag: 'dock.number', tex: textTex(String(k + 1), lx, 2.83, 2) });
        box(C, e - 0.3, e + 0.3, n0 - 1.25, n0 - 0.85, 4.7, 4.75, 'enamel', { tag: 'dock.light', em: 'glow:white', b: 1 }); lamp(C, e, n0 - 1.4, 4.4, '#fff4e2', 0.6, 4.4); }
      C.st.push({ id: 'bay' + (k + 1), verb: drive ? 'driveIn' : 'dock', at: [e, n0, fz], stand: [e, n0 - 3.3], truck: drive ? 'drive in, or back in, at grade' : 'back up to the door from the south, on the level; the dock lift rides between the trailer\u2019s bed and the floor' }); });
    if (d.man != null) { box(C, d.man - 0.43, d.man + 0.43, n0 + 0.05, n0 + 0.1, fz, cut ? Math.min(2.25, ph) : 2.25, 'steel', { tag: 'door.service' }); box(C, d.man - 0.6, d.man + 0.6, n0 - 0.5, n0, 0, fz, 'conc', { tag: 'door.step' }); }
    if (cut) {   // receiving, inside
      const i0 = E ? eA : eA + t, i1 = E ? eB - t : eB, j0 = n0 + t, j1 = n1 - t, ra = E ? i1 - 1.1 : i0, rb = E ? i1 : i0 + 1.1, st = d.stock || 'cord';
      F(C, [[i0, j0, fz], [i1, j0, fz], [i1, j1, fz], [i0, j1, fz]], 'concF', { tag: 'store.floorBack', tex: TEX.concF, uv: [[i0, j0], [i1, j0], [i1, j1], [i0, j1]] });
      for (let l = 0; l < 3; l++) { const z = fz + l * 1.3; for (const sgn of [-1, 1]) box(C, ra, rb, (sgn < 0 ? j0 + 2.6 : j1 - 0.25) - 0.04, (sgn < 0 ? j0 + 2.6 : j1 - 0.25) + 0.04, z + 1.15, z + 1.23, 'prodO', { tag: 'store.rackBeam' });
        for (let n = j0 + 2.65; n < j1 - 0.9; n += 1.3) { const n2 = Math.min(n + 1.2, j1 - 0.3); box(C, ra + 0.05, rb - 0.05, n, n2, z + (l ? 1.23 : 0), z + (l ? 1.23 : 0) + 0.12, 'cord', { tag: 'store.pallet', tex: TEX.boards });
          box(C, ra + 0.1, rb - 0.1, n + 0.05, n2 - 0.05, z + (l ? 1.35 : 0.12), z + (l ? 1.35 : 0.12) + 0.85 - (hsh(n * 7, l, 5) < 0.3 ? 0.35 : 0), st, { tag: 'store.cases', tex: TEX.box, s: { mat: st, tex: TEX.box } }); } }
      for (const n of [j0 + 2.6, j1 - 0.25]) box(C, ra, rb, n - 0.04, n + 0.04, fz, fz + 4.0, 'blue', { tag: 'store.rackPost' });
      d.doors.forEach((q, k) => { if (q.kind === 'drive') { flat(C, q.e - 1.8, q.e + 1.8, j0, j0 + 4.2, fz + 0.01, 'yel', { tag: 'store.lane', tex: TEX.hatch }); return; }
        const n = j0 + 1.9 + (k & 1) * 0.3; box(C, q.e - 0.6, q.e + 0.6, n - 0.6, n + 0.6, fz, fz + 0.14, 'cord', { tag: 'store.pallet', tex: TEX.boards });
        box(C, q.e - 0.55, q.e + 0.55, n - 0.55, n + 0.55, fz + 0.14, fz + 1.05 + hsh(q.e * 3, k, 7) * 0.45, st, { tag: 'store.cases', tex: TEX.box, s: { mat: st, tex: TEX.box } }); });
      const jx = E ? i0 + 1.6 : i1 - 2.1; box(C, jx, jx + 0.5, j1 - 2.4, j1 - 1.1, fz, fz + 0.1, 'red', { tag: 'store.jack' }); stick(C, [jx + 0.25, j1 - 2.4, fz + 0.1], [jx + 0.25, j1 - 2.7, fz + 1.05], 0.06, 'red', { tag: 'store.jack' });
      plate(C, mid - 1.3, mid + 1.3, j1 - 0.02, fz + 2.6, fz + 2.6 + textH(1) + 0.06, 'yel', { tag: 'store.sign', tex: textTex('RECEIVING', mid, fz + 2.63, 1) });
      for (const n of [j0 + 2.0, j1 - 2.0]) lamp(C, mid, n, 3.6, '#fff1d8', 0.26, 4.6); }
    C.dims.wing = { side: d.side, e0: eA, e1: eB, n0, n1, doors: d.doors.map(q => Object.assign({}, q)) };
  }
  // the grocery's back rooms mirrored (the stock room by an east wing): a box, a plate and a stick reflected through e 0, their words kept reading
  const mirO = (o, a, b) => { if (!o) return o; const q = Object.assign({}, o), sh = a + b, tr = (f) => f && f.tex ? Object.assign({}, f, { tex: ((tx) => (u, v) => tx(u + sh, v))(f.tex) }) : f;
    if (q.s) q.s = tr(q.s); const ef = q.ef, wf = q.wf; q.ef = wf; q.wf = ef; return q; };
  const mbox = (C, e0, e1, n0, n1, z0, z1, mat, o) => box(C, -e1, -e0, n0, n1, z0, z1, mat, mirO(o, e0, e1));
  const mplate = (C, e0, e1, n, z0, z1, mat, o) => plate(C, -e1, -e0, n, z0, z1, mat, o && o.tex ? Object.assign({}, o, { tex: ((tx) => (u, v) => tx(u + e0 + e1, v))(o.tex) }) : o);
  const mstick = (C, a, b, t, mat, o) => stick(C, [-a[0], a[1], a[2]], [-b[0], b[1], b[2]], t, mat, o);

  const KINDS = {
    grocery: { label: 'The grocery', foot: [-15.6, 15.6, -16.2, 12.2] }, lumber: { label: 'The lumber store', foot: [-12.6, 12.6, -15.2, 11.2] },
    dock: { label: 'A truck well', foot: [-6, 6, 0, 18] },
  };
  function resolve(kind, o) { o = o || {}; return { kind, cut: o.cutaway === 'section', cutH: o.cutH != null ? +o.cutH : 0.9, doorOpen: clamp(+(o.doorOpen || 0), 0, 1), weather: clamp(o.weather != null ? +o.weather : 0.25, 0, 1),
    o: Object.assign({}, o, { cutaway: undefined, cutH: undefined, doorOpen: undefined, weather: undefined, lit: undefined, need: undefined }) }; }
  function mats(s) { const M = {}, keep = { glass: 1, glassHi: 1, dark: 1, rubber: 1 };
    for (const k in RAMP) M[k] = { ramp: keep[k] ? RAMP[k] : RAMP[k].map(c => mix(c, '#5f584c', s.weather * 0.12)) }; return M; }
  function assemble(kind, opts, dir) { const s = resolve(kind, opts), C = { s, out: [], lamps: [], st: [], dir: dir | 0, floor: 0, indoor: null, win: 0 }; (B[kind] || B.dock)(C); return C; }
  const MODELS = new Map();
  function camBasis(dir, elev) { const th = dir * Math.PI / 4, e = (elev != null ? elev : ELEV) * DEG; return { ct: Math.cos(th), st: Math.sin(th), se: Math.sin(e), ce: Math.cos(e) }; }
  function liveModel(kind, opts, dir) { const CP = root.CoastalPass; if (!CP || !CP.light) throw new Error('NmcStores needs CoastalPass (coastalPass.js) loaded first');
    const s = resolve(kind, opts), key = kind + '|' + JSON.stringify(s) + '|' + (s.cut ? dir | 0 : 'x'); let m = MODELS.get(key); if (m) return m;
    const C = assemble(kind, opts, dir); m = CP.light.model(C.out, mats(s), { aoRes: s.cut ? 8 : 10 }); m.kind = kind; m.s = s; m.lamps = C.lamps; m.st = C.st; m.indoor = C.indoor; m.dims = C.dims; m.door = C.door || null;
    m.EK = m.EM.map(nm => { const q = /^(neon|led|glow):(\w+)/.exec(nm || ''); return q ? { type: q[1], col: q[2] } : null; });
    if (MODELS.size > 40) MODELS.delete(MODELS.keys().next().value); MODELS.set(key, m); return m; }
  function frame(kind, dir, opts) { opts = opts || {}; const m = liveModel(kind, opts, dir), Bc = camBasis(dir, opts.elev);
    let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
    for (const f of m.faces) for (const p of f.v) { const xr = p[0] * Bc.ct - p[1] * Bc.st, yr = p[0] * Bc.st + p[1] * Bc.ct, sx = xr * S, sy = -(yr * Bc.se + p[2] * Bc.ce) * S;
      if (sx < x0) x0 = sx; if (sx > x1) x1 = sx; if (sy < y0) y0 = sy; if (sy > y1) y1 = sy; }
    const pad = 6, ox = Math.ceil(-x0) + pad, oy = Math.ceil(-y0) + pad, W = Math.ceil(x1 - x0) + 2 * pad + 2, H = Math.ceil(y1 - y0) + 2 * pad + 2;
    const fr = root.CoastalPass.light.frame(m, { ct: Bc.ct, st: Bc.st, se: Bc.se, ce: Bc.ce, S, ox, oy }, W, H);
    fr.kind = kind; fr.dir = dir | 0; fr.s = m.s; return fr; }
  /* a store's lights: inside always on (it is open), signs and windows by the dark */
  function levels(need) { const k = clamp((need - 0.15) / 0.45, 0, 1);
    return { k, win: k > 0.02 ? 0.35 + 0.65 * k : 0, glow: k, cool: 0.78, neon: k, led: 0.6 + 0.4 * k, lamps: k, inside: 0.6 + 0.4 * k }; }
  function relight(fr, sky, o) { o = o || {}; const CPL = root.CoastalPass.light; sky = sky || CPL.REF_SKY;
    const m = fr.mdl, n0 = o.need != null ? +o.need : CPL.lampNeed(sky), need = isFinite(n0) ? n0 : 0, L = levels(need), cut = fr.s && fr.s.cut;
    const lights = (m.lamps || []).map(l => ({ p: l.p, c: l.c, I: l.I * (l.c === '#fff1d8' ? L.inside : L.lamps), r: l.r })).filter(l => l.I > 0.01);
    const px = CPL.relight(fr, sky, { emit: (nm) => nm && nm.startsWith('win') ? L.win : 0, lights, indoor: cut ? m.indoor : null, outline: !!o.outline });
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
  function render(kind, dir, o) { o = o || {}; const fr = frame(kind, dir, o), CPL = root.CoastalPass.light, sky = o.sky || CPL.skyOf(o); return { w: fr.W, h: fr.H, px: fr.C.ox, py: fr.C.oy, rgba: relight(fr, sky, o), fr }; }
  function stations(kind, o) { const m = liveModel(kind, o || {}, 4); return m.st.map(q => Object.assign({}, q)); }
  const footprint = (kind) => (KINDS[kind] || KINDS.dock).foot.slice();
  const dims = (kind, o) => Object.assign({}, liveModel(kind, o || {}, 4).dims);
  const lib = { S, CE40, clamp, md, hsh, mix, TEX, PMATS, FONT, textTex, textW, textH, P, F, box, plate, flat, stick, cyl, letters, wall, fbox, glazing, lamp, slider, gondola, coolerBank, checkout };
  root.NmcStores = { revision: '1.1 (10-06: the side docks)', dockWing, S, KINDS, B, RAMP, NEON, FONT, lib, resolve, frame, relight, render, castShadow, levels, stations, footprint, dims, textTex, textW, textH, list: () => Object.keys(KINDS) };
})(typeof globalThis !== 'undefined' ? globalThis : this);
