/* Hidden Harbours — St Peters OUTBUILDINGS (phase 5, 2026-09-26). The woodshed, the tool shed, the gambrel barn, the
   hen house, the dug well and the yard pump, built the way HouseIso builds a house: one parametric model per option set,
   drawn through the fixed camera (orthographic, 40 deg, looking north, 32 px = 1 m) on CoastalPass.light, with no dither
   and no keyline. Every kind has its show face at +Y with its door (or its open bay, its trough, its bucket) on it, so a
   lot stands it at the facing that turns that face to the south.

   Doors work: doorOpen 0..1 swings a hinged leaf out (shed, hen house, loft door) or slides the barn's leaves along
   their track. The interiors are furnished for their verbs (tool rack, potting bench, cordwood, stall, feed bin,
   harness pegs, ladder, roosts). cutaway:'section' drops the camera-facing walls to the sill (cutH above the floor,
   default 0.9), stands the others to the eave, takes the roof off, and caps every cut in pale board.

   Outbuilding.KINDS                          woodshed . shed . barn . henHouse . well . pump
   Outbuilding.render(kind, dir, o)           RGBA W x H on the live light (== renderLive)
   Outbuilding.frame / relight / castShadow / view   the light engine, as HouseIso
   Outbuilding.dims(kind, o)                  {w, d, h}  (h = the highest point, metres)
   Outbuilding.footprint(kind, o)             [{id, x0, x1, y0, y1, walk}]  model metres
   Outbuilding.layout(kind, o)                {show:'+Y', door:{x, y, z, width, height, facing, nrm, kind}}
   Outbuilding.placement(kind, o)             {facings:[{dir, doorFaces, via}]}  door S / SE / SW ('door'), E / W ('side')
   Outbuilding.anchors(kind, dir, o)          door, approach, entryPath, lamp: each {x, y} px, m (model), w (world)
   Outbuilding.stations(kind, o)              the v9.2 verbs: clip, fixture height inside the creator fit, stand point,
                                              reachability from the approach on a 0.1 m grid, round the walls
   Outbuilding.lights(kind, dir, o)           the barn lamp: level, model point, screen anchor, ground pool
   Options: body . siding (clapboard | shingle | board | boards) . roof (slate | asphaltGrey | asphaltBrown | metal |
   metalRed | metalGreen) . trim . doorPaint . size 0..1 . mirror . doorOpen 0..1 . loftOpen 0..1 . cutaway:'section' .
   cutH . weather 0..1 . kept 0..1 . lamp ('dusk' | 'off' | 0..1) . time / sky / night . outline */
(function (root) {
  const PX = 32, S = 32, W = 640, H = 620, cx = 320, groundY = 450, DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  function hsh(a, b, s) { let h = Math.imul(a | 0, 374761393) + Math.imul(b | 0, 668265263) + Math.imul(s | 0, 1274126177) | 0; h ^= h >>> 13; h = Math.imul(h, 1103515245) | 0; h ^= h >>> 16; return (h >>> 0) / 4294967296; }
  const h2 = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r, g, b) => '#' + [r, g, b].map(n => Math.max(0, Math.min(255, Math.round(n))).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2(a), B = h2(b); return r2h(A[0] + (B[0] - A[0]) * t, A[1] + (B[1] - A[1]) * t, A[2] + (B[2] - A[2]) * t); };
  const desat = (c, t) => { const [r, g, b] = h2(c), l = 0.3 * r + 0.59 * g + 0.11 * b; return r2h(r + (l - r) * t, g + (l - g) * t, b + (l - b) * t); };

  // ---- palettes: the houses' ramps (HouseIso.BODY), plus the yard's own ------------------------------------------------
  const BODY = {
    white: ['#8c928c', '#a6aaa2', '#bfc2b9', '#d5d8cf', '#e7e9e0', '#f3f4ec'], cream: ['#8a6f3c', '#a6884b', '#c2a35f', '#d8bd7c', '#e9d59d', '#f5e7c1'],
    red: ['#4a130f', '#671b14', '#88271c', '#a33124', '#bd4230', '#d25a42'], sage: ['#3a4636', '#4a5843', '#5c6b52', '#718063', '#889777', '#a1ae90'],
    blue: ['#33454a', '#43585d', '#556d72', '#6a848a', '#849ea3', '#a3b9bd'], greyShingle: ['#4c463f', '#5d564c', '#6f665a', '#82786a', '#968b7b', '#a99d8c'],
    yellow: ['#6e5316', '#8c6b1f', '#a9852c', '#c49f3e', '#d9b85a', '#e8cd7e'], green: ['#1f3a2c', '#29493a', '#35594a', '#436b5a', '#56806c', '#6d9582'],
    grey: ['#4d5256', '#5f6569', '#737a7e', '#8a9195', '#a2a9ac', '#b9bfc1'], teal: ['#23474a', '#2d5a5c', '#3a6e6f', '#4b8584', '#629b98', '#7db2ad'],
    charcoal: ['#26292c', '#303438', '#3c4145', '#4a5055', '#5a6166', '#6c7378'],
    barnRed: ['#3d1510', '#551d16', '#6f281d', '#893425', '#a04332', '#b55a47'],     // oxide barn paint, browner than the house red
    raw: ['#3d3a34', '#4f4a42', '#625c52', '#766f64', '#8b8478', '#a09a8d'],         // unpainted board, sun-silvered
    whitewash: ['#7b807a', '#959992', '#afb2a9', '#c6c9bf', '#d9dbd1', '#e8e9e0'],
  };
  const TRIM = ['#9aa09a', '#b4b8b0', '#ccd0c7', '#e0e2da', '#eef0e8', '#f8f9f2'];
  const PAINT = {
    white: TRIM, ivory: ['#8f8a78', '#a9a48f', '#c2bca5', '#d6d0b8', '#e6e0c9', '#f2edd9'], join: ['#1c2f27', '#243d33', '#2f4d41', '#3c5f51', '#4d7464', '#638a79'],
    red: BODY.red, blue: ['#1f3140', '#2a4152', '#365366', '#44667b', '#577b91', '#6d92a8'], green: BODY.green, iron: ['#16181b', '#1f2226', '#2a2e33', '#373c42', '#454b52', '#555c64'],
    oak: ['#4f3a24', '#63492d', '#785a39', '#8f7049', '#a6875d', '#bd9f74'], barnRed: BODY.barnRed, raw: BODY.raw,
  };
  const ROOFS = {
    slate: ['#2c3336', '#363e42', '#434c51', '#515b60', '#616c71', '#737e83'], asphaltGrey: ['#23262b', '#2e333a', '#3c424a', '#4c535c', '#5d6570', '#6f7883'],
    asphaltBrown: ['#2a211a', '#3a2e23', '#4c3d2e', '#5f4d3a', '#736046', '#877254'], metal: ['#424d52', '#556065', '#6c7c81', '#88999e', '#a4babe', '#c0d4d7'],
    metalRed: ['#461914', '#5e231b', '#772e24', '#903b2e', '#a74b3b', '#bc5e4c'], metalGreen: ['#1f3328', '#294234', '#345242', '#416452', '#517864', '#648c77'],
  };
  const WOOD = ['#3f3a33', '#514a40', '#645b4f', '#786e60', '#8c8172', '#a19585'];   // weathered post and rail
  const RAMP = {
    wood: WOOD, inside: ['#3a2e22', '#4a3b2c', '#5c4a37', '#6f5a43', '#836c51', '#977f62'], floor: ['#3b3025', '#4b3d2f', '#5d4c3a', '#705c47', '#846e56', '#988066'],
    cap: ['#8e8778', '#a79f8e', '#bdb5a2', '#d0c8b4', '#dfd8c5', '#ebe5d4'], cord: ['#5a3f22', '#71512d', '#8a653a', '#a37b4b', '#ba925f', '#cda977'],
    bark: ['#2c241c', '#3a2f24', '#4a3c2e', '#5b4a39', '#6d5a46', '#806b55'], stone: ['#3b3d3a', '#4c4f4a', '#5f625b', '#74776d', '#8a8d81', '#a1a396'],
    hay: ['#6a5421', '#86692a', '#a28336', '#bb9c47', '#cfb35e', '#dfc97c'], straw: ['#5e4c24', '#78612e', '#917839', '#a88e49', '#bca45d', '#cdb874'],
    water: ['#101c22', '#17282f', '#20363e', '#2b4650', '#385864', '#476b78'], rope: ['#5a4526', '#71592f', '#8a6f3d', '#a2874e', '#b89f66', '#cdb686'],
    galv: ['#565b5f', '#6d7276', '#868b8f', '#a0a5a8', '#bbbfc1', '#d6d9da'], cp_iron: ['#16181b', '#1f2226', '#2a2e33', '#373c42', '#454b52', '#555c64'],
    clay: ['#4f2517', '#6b3620', '#88492b', '#a35f3b', '#bb7852', '#d1936d'], sack: ['#4d4232', '#5f523f', '#72634d', '#86765c', '#9a896d', '#ad9d80'],
    leather: ['#2a1a12', '#3a2419', '#4c3021', '#5f3d2b', '#724b36', '#865b43'], soil: ['#241a12', '#31241a', '#3f2f22', '#4e3b2b', '#5e4936', '#6f5843'],
    dark: ['#0e1114', '#151a1e', '#1d2429', '#252d33', '#2e373e', '#38434b'], glass: ['#27373d', '#33474d', '#40585f', '#50696f', '#62797e', '#7a9095'],
    glassHi: ['#6f8b90', '#86a2a6', '#a1bcbf', '#bcd4d6', '#cfe6e8', '#e2f2f3'], cp_warm: ['#5c3a1a', '#93602b', '#c88b3d', '#e6b35a', '#f6d68b', '#fff0c2'],
  };

  // ---- textures: (u along the face, v up it or up the slope) -> band offset ---------------------------------------------
  const md = (a, m) => a - Math.floor(a / m) * m;
  const TEX = {
    clapboard: (u, v) => md(v, 0.2) < 0.034 ? -1 : 0,
    shingle: (u, v) => { const c = 0.18, row = Math.floor(v / c); if (md(v, c) < 0.028) return -1; const uu = u + (row & 1) * 0.12; if (md(uu, 0.24) < 0.022) return -1; return hsh(Math.floor(uu / 0.24), row, 7) < 0.14 ? -1 : 0; },
    board: (u, v) => { const j = md(u, 0.3); return j < 0.05 ? 1 : j < 0.075 ? -1 : 0; },           // board and batten
    boards: (u, v) => { const i = Math.floor(u / 0.21); if (md(u, 0.21) < 0.018) return -1; return hsh(i, 3, 11) < 0.22 ? -1 : 0; },
    slats: (u, v) => md(u, 0.19) < 0.035 ? -2 : (hsh(Math.floor(u / 0.19), 5, 13) < 0.2 ? -1 : 0),
    planks: (u, v) => { const i = Math.floor(v / 0.16); if (md(v, 0.16) < 0.016) return -1; if (md(u + i * 0.9, 1.8) < 0.02) return -1; return hsh(i, 9, 17) < 0.2 ? -1 : 0; },
    rubble: (u, v) => { const row = Math.floor(v / 0.16), uu = u + (row & 1) * 0.17; if (md(v, 0.16) < 0.03 || md(uu, 0.34) < 0.03) return -2; return hsh(Math.floor(uu / 0.34), row, 23) < 0.3 ? -1 : 0; },
    endgrain: (u, v) => { const c = 0.14, i = Math.floor(u / c), j = Math.floor((v + (i & 1) * 0.07) / c), du = md(u, c) / c - 0.5, dv = md(v + (i & 1) * 0.07, c) / c - 0.5;
      if (du * du + dv * dv > 0.2) return -2; const k = hsh(i, j, 29); return k < 0.25 ? -1 : k > 0.8 ? 1 : 0; },
    bark: (u, v) => md(u * 3.1 + v * 0.7, 0.23) < 0.05 ? -1 : 0,
    hay: (u, v) => { const k = hsh(Math.floor(u * 14), Math.floor(v * 9), 31); return k < 0.25 ? -1 : k > 0.85 ? 1 : 0; },
  };
  const ROOFTEX = {
    slate: (u, v) => { const c = 0.3, row = Math.floor(v / c); if (md(v, c) < 0.035) return -1; const uu = u + (row & 1) * 0.15; if (md(uu, 0.3) < 0.025) return -1; const k = hsh(Math.floor(uu / 0.3), row, 41); return k < 0.1 ? -1 : k > 0.93 ? 1 : 0; },
    asphaltGrey: (u, v) => { const c = 0.25, row = Math.floor(v / c); if (md(v, c) < 0.03) return -1; const uu = u + (row & 1) * 0.17; if (md(v, c) < 0.14 && md(uu, 0.34) < 0.02) return -1; return hsh(Math.floor(uu / 0.34), row, 43) < 0.12 ? -1 : 0; },
    asphaltBrown: (u, v) => { const c = 0.2, row = Math.floor(v / c); if (md(v, c) < 0.03) return -1; const uu = u + hsh(row, 1, 47) * 0.3; const wd = 0.12 + 0.12 * hsh(Math.floor(uu / 0.2), row, 49); if (md(uu, wd) < 0.02) return -1; return hsh(Math.floor(uu / 0.2), row, 51) < 0.2 ? -1 : 0; },
    metal: (u, v) => { const j = md(u, 0.45); return j < 0.03 ? -1 : j < 0.06 ? 1 : 0; },
  };
  ROOFTEX.metalRed = ROOFTEX.metal; ROOFTEX.metalGreen = ROOFTEX.metal;
  const SIDTEX = { clapboard: TEX.clapboard, shingle: TEX.shingle, board: TEX.board, boards: TEX.boards };

  // ---- kinds ----------------------------------------------------------------------------------------------------------
  const KINDS = {
    woodshed: { label: 'Woodshed', note: 'open-fronted lean-to, two bays of cordwood, a chopping block', def: { body: 'raw', siding: 'boards', roof: 'metal' } },
    shed: { label: 'Tool shed', note: 'gable shed, door in the gable, tool rack and potting bench', def: { body: 'white', siding: 'clapboard', roof: 'asphaltBrown', trim: 'white', doorPaint: 'join' } },
    barn: { label: 'Gambrel barn', note: 'small barn: sliding doors, loft door under a hay hood, stall, feed bin, ladder', def: { body: 'barnRed', siding: 'board', roof: 'metal', trim: 'white' } },
    henHouse: { label: 'Hen house', note: 'raised coop: people door, pop hole and ramp, nest box with a lid', def: { body: 'whitewash', siding: 'boards', roof: 'asphaltBrown', trim: 'white', doorPaint: 'red' } },
    well: { label: 'Dug well', note: 'fieldstone curb, windlass and crank under a gabled hood, bucket', def: { roof: 'asphaltBrown' } },
    pump: { label: 'Yard pump', note: 'cast-iron pump on a plank cover, trough under the spout, pail', def: { roof: 'asphaltBrown' } },
  };
  const SIDINGS = ['clapboard', 'shingle', 'board', 'boards'], ROOF_OPTIONS = Object.keys(ROOFS);
  function resolve(kind, o) { o = o || {}; const K = KINDS[kind] || KINDS.shed, d = K.def, g = (k, v) => o[k] != null ? o[k] : (d[k] != null ? d[k] : v);
    return { kind, body: BODY[g('body', 'white')] ? g('body', 'white') : 'white', siding: SIDTEX[g('siding', 'clapboard')] ? g('siding', 'clapboard') : 'clapboard',
      roof: ROOFS[g('roof', 'metal')] ? g('roof', 'metal') : 'metal', trim: g('trim', 'white'), doorPaint: g('doorPaint', null), size: clamp(+g('size', 0.5), 0, 1),
      mirror: !!o.mirror, doorOpen: clamp(+(o.doorOpen || 0), 0, 1), loftOpen: clamp(+(o.loftOpen || 0), 0, 1), cut: o.cutaway === 'section', cutH: o.cutH != null ? +o.cutH : 0.9,
      weather: clamp(o.weather != null ? +o.weather : 0.35, 0, 1), kept: clamp(o.kept != null ? +o.kept : 0.75, 0, 1), seed: o.seed | 0 }; }
  function mats(s) { const wx = s.weather, un = 1 - s.kept;
    const grime = r => r.map(c => mix(desat(c, wx * 0.3), '#5f584c', wx * 0.14)), chalk = r => r.map(c => mix(desat(c, un * 0.4), '#a39b8a', un * 0.2));
    const M = {}; for (const k in RAMP) M[k] = { ramp: grime(RAMP[k]) };
    M.body = { ramp: chalk(grime(BODY[s.body])) }; M.trim = { ramp: chalk(grime(PAINT[s.trim] || TRIM)) };
    M.door = { ramp: chalk(grime(PAINT[s.doorPaint] || BODY[s.body])) }; M.roof = { ramp: grime(ROOFS[s.roof]) };
    M.glass = { ramp: RAMP.glass }; M.glassHi = { ramp: RAMP.glassHi }; M.cp_warm = { ramp: RAMP.cp_warm }; M.dark = { ramp: RAMP.dark };
    return M; }

  // ---- face builders ----------------------------------------------------------------------------------------------------
  function F(out, v, mat, o) { o = o || {}; const f = { v, mat, b: o.b || 0, db: o.db || 0, uv: o.uv || null, tex: o.tex || null }; if (o.tag) f.tag = o.tag; if (o.em) f.em = o.em; out.push(f); return f; }
  function box(out, x0, x1, y0, y1, z0, z1, mat, o) { o = o || {}; const q = { tag: o.tag, b: o.b || 0, tex: o.tex || null };
    F(out, [[x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1]], mat, Object.assign({}, q, { uv: [[x0, z0], [x1, z0], [x1, z1], [x0, z1]] }));
    F(out, [[x1, y1, z0], [x0, y1, z0], [x0, y1, z1], [x1, y1, z1]], mat, Object.assign({}, q, { uv: [[x1, z0], [x0, z0], [x0, z1], [x1, z1]] }));
    F(out, [[x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1]], mat, Object.assign({}, q, { uv: [[y0, z0], [y1, z0], [y1, z1], [y0, z1]] }));
    F(out, [[x0, y1, z0], [x0, y0, z0], [x0, y0, z1], [x0, y1, z1]], mat, Object.assign({}, q, { uv: [[y1, z0], [y0, z0], [y0, z1], [y1, z1]] }));
    if (!o.noTop) F(out, [[x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]], o.topMat || mat, { tag: o.tag, b: o.topB != null ? o.topB : (o.b || 0), tex: o.topTex || null, uv: o.topTex ? [[x0, y0], [x1, y0], [x1, y1], [x0, y1]] : null }); }
  const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]], crs = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const nrm = (a) => { const m = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / m, a[1] / m, a[2] / m]; };
  function stick(out, a, b, t, mat, o) { o = o || {}; const u = nrm(sub(b, a)), p = Math.abs(u[2]) < 0.9 ? [0, 0, 1] : [1, 0, 0], v1 = nrm(crs(u, p)), v2 = crs(u, v1), h = t / 2;
    const c = (P, s1, s2) => [P[0] + (v1[0] * s1 + v2[0] * s2) * h, P[1] + (v1[1] * s1 + v2[1] * s2) * h, P[2] + (v1[2] * s1 + v2[2] * s2) * h];
    const A = [c(a, -1, -1), c(a, 1, -1), c(a, 1, 1), c(a, -1, 1)], B = [c(b, -1, -1), c(b, 1, -1), c(b, 1, 1), c(b, -1, 1)];
    for (let k = 0; k < 4; k++) { const k2 = (k + 1) % 4; F(out, [A[k], A[k2], B[k2], B[k]], mat, { tag: o.tag, b: o.b || 0 }); }
    F(out, B, mat, { tag: o.tag, b: o.b || 0 }); F(out, A, mat, { tag: o.tag, b: o.b || 0 }); }
  function cyl(out, x, y, r, z0, z1, n, mat, o) { o = o || {}; const P = []; for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI; P.push([x + Math.cos(a) * r, y + Math.sin(a) * r]); }
    const pr = 2 * Math.PI * r / n;
    for (let k = 0; k < n; k++) { const a = P[k], b = P[(k + 1) % n]; F(out, [[a[0], a[1], z0], [b[0], b[1], z0], [b[0], b[1], z1], [a[0], a[1], z1]], mat, { tag: o.tag, b: o.b || 0, tex: o.tex || null, uv: [[k * pr, z0], [(k + 1) * pr, z0], [(k + 1) * pr, z1], [k * pr, z1]] }); }
    if (!o.noTop) F(out, P.map(p => [p[0], p[1], z1]), o.topMat || mat, { tag: o.tag, b: o.topB != null ? o.topB : (o.b || 0), tex: o.topTex || null, uv: o.topTex ? P.map(p => [p[0], p[1]]) : null }); }
  function cylX(out, x0, x1, y, z, r, n, mat, o) { o = o || {}; const P = []; for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI; P.push([y + Math.cos(a) * r, z + Math.sin(a) * r]); }
    for (let k = 0; k < n; k++) { const a = P[k], b = P[(k + 1) % n]; F(out, [[x0, a[0], a[1]], [x1, a[0], a[1]], [x1, b[0], b[1]], [x0, b[0], b[1]]], mat, { tag: o.tag, b: o.b || 0 }); }
    F(out, P.map(p => [x0, p[0], p[1]]), o.endMat || mat, { tag: o.tag, b: o.b || 0 }); F(out, P.map(p => [x1, p[0], p[1]]), o.endMat || mat, { tag: o.tag, b: o.b || 0 }); }

  // ---- walls: a convex outline in (u, z) with rectangular openings, outer and inner faces, reveals and caps ---------------
  function clipAx(P, ax, v, keepGreater) { const out = []; for (let i = 0; i < P.length; i++) { const a = P[i], b = P[(i + 1) % P.length], ia = keepGreater ? a[ax] >= v - 1e-9 : a[ax] <= v + 1e-9, ib = keepGreater ? b[ax] >= v - 1e-9 : b[ax] <= v + 1e-9;
    if (ia) out.push(a); if (ia !== ib) { const t = (v - a[ax]) / (b[ax] - a[ax]), q = [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]; q[ax] = v; out.push(q); } } return out; }
  const area2 = (P) => { let s = 0; for (let i = 0; i < P.length; i++) { const a = P[i], b = P[(i + 1) % P.length]; s += a[0] * b[1] - b[0] * a[1]; } return Math.abs(s) / 2; };
  function pieces(outline, holes) {
    const us = [...new Set(holes.flatMap(h => [h.u0, h.u1]))].sort((a, b) => a - b), umin = Math.min(...outline.map(p => p[0])), umax = Math.max(...outline.map(p => p[0]));
    const cuts = [umin, ...us.filter(u => u > umin + 1e-6 && u < umax - 1e-6), umax], res = [];
    for (let i = 0; i + 1 < cuts.length; i++) { const ua = cuts[i], ub = cuts[i + 1]; const P = clipAx(clipAx(outline, 0, ua, true), 0, ub, false); if (P.length < 3 || area2(P) < 1e-6) continue;
      const hs = holes.filter(h => h.u0 <= ua + 1e-6 && h.u1 >= ub - 1e-6).sort((a, b) => a.z0 - b.z0);
      if (!hs.length) { res.push(P); continue; }
      let zlo = -1e9; for (const h of hs) { const Q = clipAx(clipAx(P, 1, zlo, true), 1, h.z0, false); if (Q.length >= 3 && area2(Q) > 1e-6) res.push(Q); zlo = h.z1; }
      const Q = clipAx(P, 1, zlo, true); if (Q.length >= 3 && area2(Q) > 1e-6) res.push(Q); }
    return res; }
  function wallFrame(p0, p1, n) { const L = Math.hypot(p1[0] - p0[0], p1[1] - p0[1]), d = [(p1[0] - p0[0]) / L, (p1[1] - p0[1]) / L];
    return { p0, p1, n, d, L, M: (u, z, off) => [p0[0] + d[0] * u + n[0] * off, p0[1] + d[1] * u + n[1] * off, z] }; }
  function fbox(out, M, u0, u1, z0, z1, o0, o1, mat, o) { o = o || {};
    const c = [M(u0, z0, o0), M(u1, z0, o0), M(u1, z1, o0), M(u0, z1, o0), M(u0, z0, o1), M(u1, z0, o1), M(u1, z1, o1), M(u0, z1, o1)], q = { tag: o.tag, b: o.b || 0, em: o.em };
    F(out, [c[4], c[5], c[6], c[7]], mat, Object.assign({}, q, { tex: o.tex || null, uv: [[u0, z0], [u1, z0], [u1, z1], [u0, z1]] }));
    if (!o.flush) F(out, [c[0], c[1], c[2], c[3]], mat, q);
    F(out, [c[0], c[1], c[5], c[4]], mat, q); F(out, [c[3], c[2], c[6], c[7]], o.topMat || mat, Object.assign({}, q, { b: o.topB != null ? o.topB : (o.b || 0) }));
    F(out, [c[0], c[3], c[7], c[4]], mat, q); F(out, [c[1], c[2], c[6], c[5]], mat, q); }
  function faceWorldN(C, n) { if (C.dir == null) return 0; const nx = C.s.mirror ? -n[0] : n[0], a = C.dir * Math.PI / 4; return nx * Math.sin(a) + n[1] * Math.cos(a); }
  const dropped = (C, n) => C.s.cut && faceWorldN(C, n) < -0.2;
  /* w: {p0, p1, n, outline, holes, mat, tex, inTex, t, tag, eave, z0} -> the wall frame; C.walls keeps the plan segments */
  function wall(C, w) { const out = C.out, Wf = wallFrame(w.p0, w.p1, w.n), t = w.t || 0.1, holes = w.holes || [];
    let outline = w.outline || [[0, w.z0 || 0], [Wf.L, w.z0 || 0], [Wf.L, w.eave], [0, w.eave]], capZ = null;
    const drop = dropped(C, w.n); Wf.drop = drop;
    if (C.s.cut) { capZ = drop ? C.floor + C.s.cutH : (w.eave != null ? w.eave : null); if (capZ != null) outline = clipAx(outline, 1, capZ, false); }
    for (const P of pieces(outline, holes)) {
      F(out, P.map(p => Wf.M(p[0], p[1], 0)), w.mat || 'body', { uv: P.map(p => [p[0], p[1]]), tex: w.tex, tag: w.tag });
      F(out, P.map(p => Wf.M(p[0], p[1], -t)), 'inside', { uv: P.map(p => [p[0], p[1]]), tex: w.inTex || TEX.boards, tag: 'wall.inside' }); }
    for (const h of holes) { const z1 = capZ != null ? Math.min(h.z1, capZ) : h.z1; if (z1 <= h.z0 + 1e-6) continue;
      const q = (u0, z0, u1, zz) => F(out, [Wf.M(u0, z0, 0), Wf.M(u1, zz, 0), Wf.M(u1, zz, -t), Wf.M(u0, z0, -t)], h.rev || 'inside', { tag: w.tag, b: -1 });
      q(h.u0, h.z0, h.u0, z1); q(h.u1, h.z0, h.u1, z1); if (h.z0 > (w.z0 || 0) + 0.01) q(h.u0, h.z0, h.u1, h.z0); if (capZ == null || h.z1 < capZ - 1e-6) q(h.u0, h.z1, h.u1, h.z1); }
    if (capZ != null) { // pale caps along every cut edge: the horizontal cut skips the openings it crosses
      for (let i = 0; i < outline.length; i++) { const a = outline[i], b = outline[(i + 1) % outline.length];
        if (Math.abs(a[0] - b[0]) < 1e-6) continue; const zc = Math.max(a[1], b[1]); if (zc < capZ - 1e-6 && Math.abs(a[1] - b[1]) < 1e-6) continue;
        if (Math.abs(a[1] - capZ) < 1e-6 && Math.abs(b[1] - capZ) < 1e-6) { let segs = [[Math.min(a[0], b[0]), Math.max(a[0], b[0])]];
          for (const h of holes) if (h.z0 < capZ - 1e-6 && h.z1 > capZ + 1e-6) segs = segs.flatMap(([s0, s1]) => (h.u1 <= s0 || h.u0 >= s1) ? [[s0, s1]] : [[s0, h.u0], [h.u1, s1]].filter(q => q[1] - q[0] > 1e-3));
          for (const [s0, s1] of segs) F(out, [Wf.M(s0, capZ, 0.012), Wf.M(s1, capZ, 0.012), Wf.M(s1, capZ, -t), Wf.M(s0, capZ, -t)], 'cap', { tag: 'cut.cap', b: 1 }); }
        else if (Math.max(a[1], b[1]) > (w.z0 || 0) + 0.05 && !(Math.abs(a[1] - (w.z0 || 0)) < 1e-6 && Math.abs(b[1] - (w.z0 || 0)) < 1e-6)) F(out, [Wf.M(a[0], a[1], 0.012), Wf.M(b[0], b[1], 0.012), Wf.M(b[0], b[1], -t), Wf.M(a[0], a[1], -t)], 'cap', { tag: 'cut.cap', b: 1 }); } }
    // the plan segments for reachability: floor-level openings are gaps
    let gaps = holes.filter(h => h.z0 < (C.floor || 0) + 0.35 && h.walk !== false).map(h => [h.u0, h.u1]).sort((a, b) => a[0] - b[0]), u = 0;
    for (const g of gaps) { if (g[0] > u) C.walls.push({ a: Wf.M(u, 0, -t / 2), b: Wf.M(g[0], 0, -t / 2), t }); u = Math.max(u, g[1]); }
    if (u < Wf.L) C.walls.push({ a: Wf.M(u, 0, -t / 2), b: Wf.M(Wf.L, 0, -t / 2), t });
    Wf.capZ = capZ; return Wf; }
  function cornerBoards(C, Wf, z0, z1, mat) { const zz = C.s.cut ? Math.min(z1, Wf.capZ != null ? Wf.capZ : z1) : z1; if (zz <= z0) return;
    fbox(C.out, Wf.M, 0, 0.1, z0, zz, 0, 0.024, mat || 'trim', { tag: 'wall.trim' }); fbox(C.out, Wf.M, Wf.L - 0.1, Wf.L, z0, zz, 0, 0.024, mat || 'trim', { tag: 'wall.trim' }); }
  function casing(C, Wf, h, o) { o = o || {}; const cw = o.w || 0.08, z1 = Wf.capZ != null ? Math.min(h.z1 + cw, Wf.capZ) : h.z1 + cw, tg = o.tag || 'entry.frame';
    if (z1 > h.z0) { fbox(C.out, Wf.M, h.u0 - cw, h.u0, h.z0, z1, 0, 0.026, 'trim', { tag: tg }); fbox(C.out, Wf.M, h.u1, h.u1 + cw, h.z0, z1, 0, 0.026, 'trim', { tag: tg }); }
    if (Wf.capZ == null || h.z1 + cw <= Wf.capZ) fbox(C.out, Wf.M, h.u0 - cw, h.u1 + cw, h.z1, h.z1 + cw, 0, 0.03, 'trim', { tag: tg }); }
  function windowIn(C, Wf, h, o) { o = o || {}; if (Wf.capZ != null && h.z0 >= Wf.capZ - 1e-6) return; const zt = Wf.capZ != null ? Math.min(h.z1, Wf.capZ) : h.z1, out = C.out, em = 'win:' + (C.win++);
    F(out, [Wf.M(h.u0, h.z0, -0.04), Wf.M(h.u1, h.z0, -0.04), Wf.M(h.u1, zt, -0.04), Wf.M(h.u0, zt, -0.04)], 'glass', { tag: 'show.window', em });
    F(out, [Wf.M(h.u0 + (h.u1 - h.u0) * 0.08, zt - 0.14, -0.035), Wf.M(h.u0 + (h.u1 - h.u0) * 0.34, zt - 0.14, -0.035), Wf.M(h.u0 + (h.u1 - h.u0) * 0.22, zt - 0.05, -0.035)], 'glassHi', { tag: 'show.window', em });
    const nv = o.cols || 2, nh = o.rows || 2;
    for (let k = 1; k < nv; k++) { const u = h.u0 + (h.u1 - h.u0) * k / nv; fbox(out, Wf.M, u - 0.018, u + 0.018, h.z0, zt, -0.04, -0.02, 'trim', { tag: 'show.window' }); }
    for (let k = 1; k < nh; k++) { const z = h.z0 + (h.z1 - h.z0) * k / nh; if (z < zt) fbox(out, Wf.M, h.u0, h.u1, z - 0.018, z + 0.018, -0.04, -0.02, 'trim', { tag: 'show.window' }); }
    casing(C, Wf, h, { tag: 'show.window', w: 0.07 }); fbox(out, Wf.M, h.u0 - 0.1, h.u1 + 0.1, h.z0 - 0.05, h.z0, 0, 0.06, 'trim', { tag: 'show.window' }); }
  /* a hinged leaf in the opening: hinge at u = hu, the leaf runs sgn along the wall, swings out by th (rad) */
  function leafFrame(Wf, hu, sgn, th) { const c = Math.cos(th), s = Math.sin(th); return (a, z, b) => Wf.M(hu + sgn * (a * c - b * s), z, a * s + b * c); }
  function ledgeLeaf(C, Wf, h, hingeAt, th, mat, o) { o = o || {}; const out = C.out, sgn = hingeAt === 'u0' ? 1 : -1, hu = hingeAt === 'u0' ? h.u0 : h.u1, wd = h.u1 - h.u0 - 0.02, M = leafFrame(Wf, hu, sgn, th), tg = 'door.leaf';
    const z0 = h.z0 + 0.012, z1 = h.z1 - 0.012; fbox(out, M, 0.01, wd, z0, z1, 0.0, 0.045, mat, { tag: tg, tex: TEX.boards });
    for (const z of o.ledges || [z0 + 0.18, (z0 + z1) / 2 - 0.05, z1 - 0.3]) fbox(out, M, 0.06, wd - 0.05, z, z + 0.11, 0.045, 0.07, mat, { tag: tg, b: 1 });
    if (o.brace !== false) { const L = o.ledges || [z0 + 0.18, (z0 + z1) / 2 - 0.05, z1 - 0.3]; stick(out, M(0.12, L[0] + 0.11, 0.058), M(wd - 0.1, L[1], 0.058), 0.07, mat, { tag: tg, b: 1 }); if (L.length > 2) stick(out, M(0.12, L[1] + 0.11, 0.058), M(wd - 0.1, L[2], 0.058), 0.07, mat, { tag: tg, b: 1 }); }
    fbox(out, M, wd - 0.12, wd - 0.07, (z0 + z1) / 2 - 0.08, (z0 + z1) / 2 + 0.06, 0.045, 0.075, 'cp_iron', { tag: tg });
    for (const z of o.ledges || [z0 + 0.18, z1 - 0.3]) fbox(out, M, 0.01, 0.34, z + 0.03, z + 0.08, 0.07, 0.085, 'cp_iron', { tag: tg }); }
  const DEG100 = 100 * DEG;

  // ---- roofs: gable (ridge along y), gambrel, mono-pitch ------------------------------------------------------------------
  function gableRoof(C, x0, x1, y0, y1, e, r, ov, rk, o) { o = o || {}; if (C.s.cut) return; const out = C.out, xm = (x0 + x1) / 2, hw = (x1 - x0) / 2, sl = (r - e) / hw, ze = e - ov * sl, ya = y0 - rk, yb = y1 + (o.front || 0) + rk, L = Math.hypot(hw + ov, r - ze), tex = ROOFTEX[C.s.roof], tg = 'roof';
    F(out, [[x0 - ov, ya, ze], [x0 - ov, yb, ze], [xm, yb, r], [xm, ya, r]], 'roof', { tag: tg, tex, uv: [[ya, 0], [yb, 0], [yb, L], [ya, L]] });
    F(out, [[x1 + ov, yb, ze], [x1 + ov, ya, ze], [xm, ya, r], [xm, yb, r]], 'roof', { tag: tg, tex, uv: [[-yb, 0], [-ya, 0], [-ya, L], [-yb, L]] });
    const th = o.th || 0.08, tm = o.trim || 'trim';
    for (const x of [x0 - ov, x1 + ov]) F(out, [[x, ya, ze - th], [x, yb, ze - th], [x, yb, ze], [x, ya, ze]], tm, { tag: 'roof.fascia' });
    for (const y of [ya, yb]) { F(out, [[x0 - ov, y, ze - th], [xm, y, r - th], [xm, y, r + 0.01], [x0 - ov, y, ze + 0.01]], tm, { tag: 'roof.rake' }); F(out, [[x1 + ov, y, ze - th], [xm, y, r - th], [xm, y, r + 0.01], [x1 + ov, y, ze + 0.01]], tm, { tag: 'roof.rake' }); }
    box(out, xm - 0.05, xm + 0.05, ya, yb, r - 0.02, r + 0.05, 'roof', { tag: 'roof.ridge', b: 1 }); }
  function monoRoof(C, x0, x1, yF, zF, yB, zB, ovF, ovB, rk, o) { o = o || {}; if (C.s.cut) return; const out = C.out, sl = (zF - zB) / (yF - yB), zf = zF + ovF * sl, zb = zB - ovB * sl, ya = yB - ovB, yb = yF + ovF, xa = x0 - rk, xb = x1 + rk, L = Math.hypot(yb - ya, zf - zb), tex = ROOFTEX[C.s.roof];
    F(out, [[xa, ya, zb], [xb, ya, zb], [xb, yb, zf], [xa, yb, zf]], 'roof', { tag: 'roof', tex, uv: [[xa, 0], [xb, 0], [xb, L], [xa, L]] });
    const th = 0.09, tm = o.trim || 'wood';
    F(out, [[xa, yb, zf - th], [xb, yb, zf - th], [xb, yb, zf], [xa, yb, zf]], tm, { tag: 'roof.fascia' }); F(out, [[xa, ya, zb - th], [xb, ya, zb - th], [xb, ya, zb], [xa, ya, zb]], tm, { tag: 'roof.fascia' });
    for (const x of [xa, xb]) F(out, [[x, ya, zb - th], [x, yb, zf - th], [x, yb, zf + 0.01], [x, ya, zb + 0.01]], tm, { tag: 'roof.rake' }); }

  // ---- the kinds ----------------------------------------------------------------------------------------------------------
  const B = {};
  B.shed = function (C) { const s = C.s, out = C.out, w = 2.4 + 0.6 * s.size, d = 2.8 + 1.0 * s.size, hw = w / 2, hl = d / 2, z0 = 0.06, fz = C.floor = 0.14, e = 2.12, r = e + hw * 0.9, t = 0.09, sid = SIDTEX[s.siding];
    C.dims = { w, d, h: r + 0.1 };
    for (const [x, y] of [[-hw + 0.12, -hl + 0.12], [hw - 0.12, -hl + 0.12], [-hw + 0.12, hl - 0.12], [hw - 0.12, hl - 0.12]]) box(out, x - 0.13, x + 0.13, y - 0.13, y + 0.13, 0, z0 + 0.02, 'stone', { tag: 'wall.pier' });
    F(out, [[-hw + t, -hl + t, fz], [hw - t, -hl + t, fz], [hw - t, hl - t, fz], [-hw + t, hl - t, fz]], 'floor', { tag: 'store.floor', tex: TEX.planks, uv: [[-hw, -hl], [hw, -hl], [hw, hl], [-hw, hl]] });
    const door = { u0: hw - 0.425, u1: hw + 0.425, z0: fz, z1: fz + 1.9 }, win = { u0: hl - 0.34, u1: hl + 0.34, z0: 1.05, z1: 1.72, walk: false };
    const gab = [[0, z0], [w, z0], [w, e], [hw, r], [0, e]];
    const Wf = wall(C, { p0: [-hw, hl], p1: [hw, hl], n: [0, 1], outline: gab, holes: [door], tex: sid, t, tag: 'show.front', eave: e, z0 });
    const Wx = wall(C, { p0: [hw, hl], p1: [hw, -hl], n: [1, 0], holes: [win], tex: sid, t, tag: 'wall.side', eave: e, z0 });
    const Wb = wall(C, { p0: [hw, -hl], p1: [-hw, -hl], n: [0, -1], outline: gab, tex: sid, t, tag: 'wall.back', eave: e, z0 });
    const Wn = wall(C, { p0: [-hw, -hl], p1: [-hw, hl], n: [-1, 0], tex: sid, t, tag: 'wall.side', eave: e, z0 });
    for (const q of [Wf, Wx, Wb, Wn]) cornerBoards(C, q, z0, e);
    casing(C, Wf, door); windowIn(C, Wx, win, { cols: 2, rows: 2 });
    if (!Wf.drop) { ledgeLeaf(C, Wf, door, 'u0', s.doorOpen * DEG100, 'door'); if (!s.cut) { fbox(out, Wf.M, hw - 0.19, hw + 0.19, e + 0.28, e + 0.62, 0, 0.03, 'trim', { tag: 'show.vent' }); for (let k = 0; k < 3; k++) fbox(out, Wf.M, hw - 0.13, hw + 0.13, e + 0.34 + k * 0.09, e + 0.37 + k * 0.09, 0.03, 0.05, 'dark', { tag: 'show.vent' }); } }
    else F(out, [Wf.M(door.u0, fz + 0.005, 0.02), Wf.M(door.u1, fz + 0.005, 0.02), Wf.M(door.u1, fz + 0.005, -t), Wf.M(door.u0, fz + 0.005, -t)], 'trim', { tag: 'entry.threshold' });
    box(out, -0.5, 0.5, hl, hl + 0.42, 0, 0.09, 'stone', { tag: 'entry.steps', topTex: TEX.rubble });
    gableRoof(C, -hw, hw, -hl, hl, e, r, 0.17, 0.15);
    // inside: tool rack on the back wall, potting bench and pot shelf on the -X wall, sacks and a can by the +X wall
    const yb = -hl + t, xw = -hw + t, xe = hw - t;
    box(out, -hw + 0.3, hw - 0.3, yb, yb + 0.04, 1.3, 1.4, 'wood', { tag: 'store.toolRack' });
    const tools = [['spade', -0.6], ['fork', -0.22], ['rake', 0.18], ['hoe', 0.56]].map(([k, f]) => [k, f * (w - 0.6) / 1.9]);
    for (const [k, x] of tools) { const y = yb + 0.07; box(out, x - 0.025, x + 0.025, yb + 0.04, yb + 0.1, 1.33, 1.37, 'wood', { tag: 'store.toolRack' });
      if (k === 'spade') { stick(out, [x, y, 1.42], [x, y, 0.62], 0.036, 'wood', { tag: 'store.tools' }); box(out, x - 0.1, x + 0.1, y - 0.012, y + 0.012, 0.3, 0.62, 'galv', { tag: 'store.tools' }); box(out, x - 0.07, x + 0.07, y - 0.015, y + 0.015, 1.42, 1.5, 'wood', { tag: 'store.tools' }); }
      if (k === 'fork') { stick(out, [x, y, 1.45], [x, y, 0.6], 0.036, 'wood', { tag: 'store.tools' }); box(out, x - 0.09, x + 0.09, y - 0.015, y + 0.015, 0.56, 0.62, 'cp_iron', { tag: 'store.tools' }); for (let q = 0; q < 4; q++) stick(out, [x - 0.075 + q * 0.05, y, 0.57], [x - 0.075 + q * 0.05, y, 0.3], 0.018, 'cp_iron', { tag: 'store.tools' }); }
      if (k === 'rake') { stick(out, [x, y, 0.28], [x, y, 1.62], 0.034, 'wood', { tag: 'store.tools' }); box(out, x - 0.2, x + 0.2, y - 0.02, y + 0.02, 1.62, 1.67, 'cp_iron', { tag: 'store.tools' }); for (let q = 0; q < 7; q++) stick(out, [x - 0.18 + q * 0.06, y + 0.015, 1.62], [x - 0.18 + q * 0.06, y + 0.05, 1.56], 0.014, 'cp_iron', { tag: 'store.tools' }); }
      if (k === 'hoe') { stick(out, [x, y, 0.3], [x, y, 1.66], 0.034, 'wood', { tag: 'store.tools' }); box(out, x - 0.08, x + 0.08, y - 0.01, y + 0.07, 1.64, 1.74, 'galv', { tag: 'store.tools' }); } }
    C.solids.push({ id: 'toolRack', x0: -hw + 0.3, x1: hw - 0.3, y0: yb, y1: yb + 0.14 });
    const bx0 = xw, bx1 = xw + 0.55, by0 = yb + 0.5, by1 = hl - t - 0.95;
    box(out, bx0, bx1, by0, by1, 0.75, 0.8, 'wood', { tag: 'store.bench', topTex: TEX.boards });
    for (const [x, y] of [[bx0 + 0.05, by0 + 0.05], [bx1 - 0.05, by0 + 0.05], [bx0 + 0.05, by1 - 0.05], [bx1 - 0.05, by1 - 0.05]]) box(out, x - 0.03, x + 0.03, y - 0.03, y + 0.03, fz, 0.75, 'wood', { tag: 'store.bench' });
    box(out, bx0, bx1 - 0.04, by0 + 0.03, by1 - 0.03, 0.3, 0.33, 'wood', { tag: 'store.bench' });
    for (let k = 0; k < 3; k++) { const y = by0 + 0.2 + k * (by1 - by0 - 0.4) / 2; cyl(out, bx0 + 0.25, y, 0.075, 0.8, 0.94, 8, 'clay', { tag: 'store.pots', topMat: 'soil' }); }
    box(out, xw, xw + 0.24, by0, by1, 1.46, 1.49, 'wood', { tag: 'store.shelf' });
    for (let k = 0; k < 3; k++) cyl(out, xw + 0.12, by0 + 0.15 + k * 0.3, 0.06, 1.49, 1.64, 8, 'galv', { tag: 'store.shelf' });
    C.solids.push({ id: 'bench', x0: bx0, x1: bx1, y0: by0, y1: by1 });
    box(out, xe - 0.42, xe, yb + 0.1, yb + 0.7, fz, fz + 0.55, 'sack', { tag: 'store.sacks', b: 0 }); box(out, xe - 0.38, xe - 0.02, yb + 0.72, yb + 1.1, fz, fz + 0.42, 'sack', { tag: 'store.sacks', b: -1 });
    cyl(out, xe - 0.22, hl - t - 0.5, 0.1, fz, fz + 0.26, 8, 'galv', { tag: 'store.can' }); stick(out, [xe - 0.14, hl - t - 0.44, fz + 0.12], [xe - 0.02, hl - t - 0.3, fz + 0.3], 0.025, 'galv', { tag: 'store.can' });
    C.solids.push({ id: 'sacks', x0: xe - 0.44, x1: xe, y0: yb + 0.1, y1: yb + 1.1 }, { id: 'can', x0: xe - 0.34, x1: xe - 0.1, y0: hl - t - 0.62, y1: hl - t - 0.38 });
    C.door = { x: 0, y: hl, z: fz, width: 0.85, height: 1.9, facing: '+Y', nrm: [0, 1], kind: 'hinged' };
    C.foot = [{ id: 'shed', x0: -hw - 0.05, x1: hw + 0.05, y0: -hl - 0.05, y1: hl + 0.03, walk: false }, { id: 'step', x0: -0.5, x1: 0.5, y0: hl, y1: hl + 0.42, walk: true }];
    C.st = [
      { id: 'door', verb: 'enter', clip: 'walk', at: [0, hl, fz], stand: [0, hl + 1.5], clearWidth: 0.85 },
      { id: 'toolRack', verb: 'takeTool', clip: 'reach', also: ['stowV'], fixture: 'rest', fixtureZ: 0.95, at: [0, yb + 0.08, fz + 0.95], stand: [0.1, yb + 0.62], standZ: fz, note: 'handles hang with their grips from 0.95 up; the rack rail is at 1.35' },
      { id: 'pottingBench', verb: 'pot', clip: 'bench', also: ['place'], fixture: 'bench', fixtureZ: 0.8 - fz, at: [(bx0 + bx1) / 2, (by0 + by1) / 2, 0.8], stand: [bx1 + 0.42, (by0 + by1) / 2], standZ: fz, note: 'the bench top is a place surface too (load fit)' },
    ]; };
  B.woodshed = function (C) { const s = C.s, out = C.out, w = 2.7 + 0.8 * s.size, d = 1.8, hw = w / 2, hl = d / 2, zF = 2.25, zB = 1.72, t = 0.05, fz = C.floor = 0.1, sid = SIDTEX[s.siding] || TEX.boards;
    C.dims = { w, d, h: zF + 0.2 };
    const zAt = (y) => zB + (zF - zB) * (y + hl) / d;
    const Wb = wall(C, { p0: [hw, -hl], p1: [-hw, -hl], n: [0, -1], tex: sid, t, tag: 'wall.back', eave: zB, z0: 0.06 });
    const Wx = wall(C, { p0: [hw, hl], p1: [hw, -hl], n: [1, 0], outline: [[0, 0.06], [d, 0.06], [d, zB], [0, zF]], tex: TEX.slats, t, tag: 'wall.side', eave: s.cut ? zB : null, z0: 0.06 });
    const Wn = wall(C, { p0: [-hw, -hl], p1: [-hw, hl], n: [-1, 0], outline: [[0, 0.06], [d, 0.06], [d, zF], [0, zB]], tex: TEX.slats, t, tag: 'wall.side', eave: s.cut ? zB : null, z0: 0.06 });
    const front = { n: [0, 1] }, fdrop = dropped(C, front.n), ztop = s.cut ? (fdrop ? fz + s.cutH : zF) : zF;
    for (const x of [-hw + 0.06, 0, hw - 0.06]) box(out, x - 0.06, x + 0.06, hl - 0.12, hl, 0, ztop, 'wood', { tag: 'show.post' });
    for (const x of [-hw + 0.05, hw - 0.05]) box(out, x - 0.05, x + 0.05, -hl, -hl + 0.1, 0, s.cut ? Math.min(zB, Wb.capZ) : zB, 'wood', { tag: 'wall.post' });
    if (!s.cut) box(out, -hw, hw, hl - 0.13, hl, zF - 0.2, zF, 'wood', { tag: 'show.header' });
    for (const y of [-hl + 0.3, hl - 0.4]) box(out, -hw + 0.08, hw - 0.08, y - 0.06, y + 0.06, 0, fz - 0.02, 'wood', { tag: 'store.sleeper' });
    F(out, [[-hw + t, -hl + t, fz], [hw - t, -hl + t, fz], [hw - t, hl - 0.05, fz], [-hw + t, hl - 0.05, fz]], 'floor', { tag: 'store.floor', tex: TEX.planks, uv: [[-hw, -hl], [hw, -hl], [hw, hl], [-hw, hl]] });
    const y0 = -hl + t + 0.04, y1 = hl - 0.34, tops = [1.52, 1.12];
    [[-hw + t + 0.06, -0.09], [0.09, hw - t - 0.06]].forEach(([xa, xb], k) => { const zt = Math.min(tops[k], s.cut ? zAt(y1) - 0.1 : 9);
      F(out, [[xa, y1, fz], [xb, y1, fz], [xb, y1, zt], [xa, y1, zt]], 'cord', { tag: 'show.cordwood', tex: TEX.endgrain, uv: [[xa, fz], [xb, fz], [xb, zt], [xa, zt]] });
      F(out, [[xa, y0, zt], [xb, y0, zt], [xb, y1, zt], [xa, y1, zt]], 'bark', { tag: 'store.cordwood', tex: TEX.bark, uv: [[xa, y0], [xb, y0], [xb, y1], [xa, y1]] });
      for (const x of [xa, xb]) F(out, [[x, y0, fz], [x, y1, fz], [x, y1, zt], [x, y0, zt]], 'cord', { tag: 'store.cordwood', tex: TEX.endgrain, uv: [[y0, fz], [y1, fz], [y1, zt], [y0, zt]], b: -1 });
      C.solids.push({ id: 'cord' + k, x0: xa, x1: xb, y0: y0, y1: y1 }); });
    for (let k = 0; k < 4; k++) { const x = 0.25 + k * 0.22, y = y1 + 0.12 + (k & 1) * 0.06; cylX(out, x - 0.2, x + 0.2, y, fz + 0.06, 0.06, 6, 'bark', { tag: 'store.cordwood', endMat: 'cord' }); }
    monoRoof(C, -hw, hw, hl, zF, -hl, zB, 0.3, 0.15, 0.14);
    const bx = hw - 0.5, by = hl + 1.0; cyl(out, bx, by, 0.25, 0, 0.62, 10, 'bark', { tag: 'yard.chopBlock', topMat: 'cord', tex: TEX.bark });
    box(out, bx - 0.09, bx + 0.05, by - 0.02, by + 0.02, 0.6, 0.7, 'cp_iron', { tag: 'yard.axe' }); stick(out, [bx - 0.02, by, 0.66], [bx + 0.24, by + 0.36, 0.98], 0.035, 'wood', { tag: 'yard.axe' });
    for (let k = 0; k < 9; k++) { const a = hsh(k, 1, 61) * 6.28, rr = 0.38 + hsh(k, 2, 61) * 0.5, x = bx + Math.cos(a) * rr, y = by + Math.sin(a) * rr * 0.8, l = 0.07 + hsh(k, 3, 61) * 0.08; box(out, x - l, x + l, y - 0.025, y + 0.025, 0, 0.018, 'cord', { tag: 'yard.chips' }); }
    C.solids.push({ id: 'block', x0: bx - 0.27, x1: bx + 0.27, y0: by - 0.27, y1: by + 0.27 });
    C.door = { x: 0, y: hl, z: fz, width: w - 0.24, height: zF - 0.2, facing: '+Y', nrm: [0, 1], kind: 'open' };
    C.foot = [{ id: 'woodshed', x0: -hw - 0.05, x1: hw + 0.05, y0: -hl - 0.05, y1: hl, walk: false }, { id: 'chopBlock', x0: bx - 0.3, x1: bx + 0.3, y0: by - 0.3, y1: by + 0.3, walk: false }];
    C.st = [
      { id: 'bay', verb: 'enter', clip: 'walk', at: [0, hl, fz], stand: [-hw / 2, hl + 1.5], clearWidth: hw - 0.12, note: 'open front: two bays between three posts' },
      { id: 'cordwood', verb: 'takeWood', clip: 'lift', also: ['place'], fixture: 'load', fixtureZ: 0.8, at: [-hw / 2, y1, 0.8], stand: [-hw / 2, hl + 0.5], standZ: 0, note: 'splits taken from the face of the full bay at 0.80; the right bay is half burned' },
      { id: 'chopBlock', verb: 'splitWood', clip: 'chop', fixture: 'knife', fixtureZ: 0.62, at: [bx, by, 0.62], stand: [bx - 0.66, by], standZ: 0, note: 'a tall round, so the splitting face is at the v9.2 chop height' },
    ]; };
  B.barn = function (C) { const s = C.s, out = C.out, w = 5.6 + 1.4 * s.size, d = 6.6 + 2.0 * s.size, hw = w / 2, hl = d / 2, zb = C.floor = 0.32, e = 3.1, ks = 0.95, zk = e + 2.0, zr = zk + 1.3, t = 0.12, sid = SIDTEX[s.siding] || TEX.board;
    C.dims = { w, d, h: zr + 1.5 };
    box(out, -hw - 0.06, hw + 0.06, -hl - 0.06, hl + 0.06, 0, zb, 'stone', { tag: 'wall.plinth', tex: TEX.rubble });
    const gam = [[0, zb], [w, zb], [w, e], [w - ks, zk], [hw, zr], [ks, zk], [0, e]];
    const door = { u0: hw - 1.4, u1: hw + 1.4, z0: zb, z1: zb + 2.72 }, loft = { u0: hw - 0.62, u1: hw + 0.62, z0: e + 0.42, z1: e + 1.58, walk: false };
    const wx1 = { u0: d * 0.27 - 0.36, u1: d * 0.27 + 0.36, z0: 1.45, z1: 2.3, walk: false }, wx2 = { u0: d * 0.7 - 0.36, u1: d * 0.7 + 0.36, z0: 1.45, z1: 2.3, walk: false }, wn = { u0: d * 0.5 - 0.36, u1: d * 0.5 + 0.36, z0: 1.45, z1: 2.3, walk: false };
    const Wf = wall(C, { p0: [-hw, hl], p1: [hw, hl], n: [0, 1], outline: gam, holes: [door, loft], tex: sid, t, tag: 'show.front', eave: e, z0: zb });
    const Wx = wall(C, { p0: [hw, hl], p1: [hw, -hl], n: [1, 0], holes: [wx1, wx2], tex: sid, t, tag: 'wall.side', eave: e, z0: zb });
    const Wb = wall(C, { p0: [hw, -hl], p1: [-hw, -hl], n: [0, -1], outline: gam, tex: sid, t, tag: 'wall.back', eave: e, z0: zb });
    const Wn = wall(C, { p0: [-hw, -hl], p1: [-hw, hl], n: [-1, 0], holes: [wn], tex: sid, t, tag: 'wall.side', eave: e, z0: zb });
    for (const q of [Wf, Wx, Wb, Wn]) cornerBoards(C, q, zb, e);
    casing(C, Wf, door, { w: 0.12 }); if (!s.cut) casing(C, Wf, loft, { w: 0.1 });
    for (const [q, h] of [[Wx, wx1], [Wx, wx2], [Wn, wn]]) windowIn(C, q, h, { cols: 3, rows: 2 });
    if (!Wf.drop) { const slide = s.doorOpen * 1.42, lz0 = zb + 0.02, lz1 = zb + 2.78;
      fbox(out, Wf.M, hw - 2.95, hw + 2.95, zb + 2.84, zb + 2.92, 0.02, 0.1, 'cp_iron', { tag: 'entry.track' });
      for (const sg of [-1, 1]) { const ua = sg < 0 ? hw - 1.42 - slide : hw + slide, ub = ua + 1.42;
        fbox(out, Wf.M, ua, ub, lz0, lz1, 0.1, 0.16, 'body', { tag: 'door.leaf', tex: sid });
        for (const [a0, a1, z0, z1] of [[ua, ub, lz0, lz0 + 0.12], [ua, ub, lz1 - 0.12, lz1], [ua, ua + 0.12, lz0, lz1], [ub - 0.12, ub, lz0, lz1], [ua, ub, (lz0 + lz1) / 2 - 0.06, (lz0 + lz1) / 2 + 0.06]]) fbox(out, Wf.M, a0, a1, z0, z1, 0.16, 0.19, 'trim', { tag: 'door.leaf' });
        stick(out, Wf.M(ua + 0.1, lz0 + 0.12, 0.178), Wf.M(ub - 0.1, (lz0 + lz1) / 2 - 0.06, 0.178), 0.09, 'trim', { tag: 'door.leaf' });
        stick(out, Wf.M(ua + 0.1, lz1 - 0.12, 0.178), Wf.M(ub - 0.1, (lz0 + lz1) / 2 + 0.06, 0.178), 0.09, 'trim', { tag: 'door.leaf' });
        for (const a of [ua + 0.3, ub - 0.3]) fbox(out, Wf.M, a - 0.05, a + 0.05, zb + 2.8, zb + 2.95, 0.1, 0.13, 'cp_iron', { tag: 'entry.track' }); }
      if (!s.cut) { const th = s.loftOpen * DEG100; fbox(out, leafFrame(Wf, loft.u0, 1, th), 0.01, loft.u1 - loft.u0 - 0.02, loft.z0 + 0.01, loft.z1 - 0.01, 0, 0.05, 'body', { tag: 'door.loft', tex: sid });
        const LM = leafFrame(Wf, loft.u0, 1, th), lw = loft.u1 - loft.u0 - 0.02; stick(out, LM(0.06, loft.z0 + 0.06, 0.07), LM(lw - 0.06, loft.z1 - 0.06, 0.07), 0.08, 'trim', { tag: 'door.loft' }); stick(out, LM(0.06, loft.z1 - 0.06, 0.07), LM(lw - 0.06, loft.z0 + 0.06, 0.07), 0.08, 'trim', { tag: 'door.loft' }); } }
    else F(out, [Wf.M(door.u0, zb + 0.005, 0.03), Wf.M(door.u1, zb + 0.005, 0.03), Wf.M(door.u1, zb + 0.005, -t), Wf.M(door.u0, zb + 0.005, -t)], 'trim', { tag: 'entry.threshold' });
    // the ramp up to the sill, stone on the earth
    F(out, [[-1.65, hl + 0.06, zb], [1.65, hl + 0.06, zb], [1.65, hl + 1.15, 0.02], [-1.65, hl + 1.15, 0.02]], 'stone', { tag: 'entry.ramp', tex: TEX.rubble, uv: [[-1.65, 0], [1.65, 0], [1.65, 1.2], [-1.65, 1.2]] });
    for (const x of [-1.65, 1.65]) F(out, [[x, hl + 0.06, 0], [x, hl + 0.06, zb], [x, hl + 1.15, 0.02], [x, hl + 1.15, 0]], 'stone', { tag: 'entry.ramp', b: -1 });
    // lamp beside the doors
    const lu = hw + 1.72, lz = zb + 2.3; if (!Wf.drop) { stick(out, Wf.M(lu, lz + 0.12, 0), Wf.M(lu, lz + 0.12, 0.3), 0.03, 'cp_iron', { tag: 'entry.lantern' });
      fbox(out, Wf.M, lu - 0.07, lu + 0.07, lz - 0.14, lz + 0.04, 0.22, 0.36, 'cp_warm', { tag: 'entry.lantern', em: 'lantern' }); fbox(out, Wf.M, lu - 0.09, lu + 0.09, lz + 0.04, lz + 0.1, 0.2, 0.38, 'cp_iron', { tag: 'entry.lantern' }); }
    C.lamp = Wf.M(lu, lz - 0.05, 0.29);
    if (!s.cut) { // gambrel roof with a hay hood over the loft door, the hay beam, a cupola
      const ov = 0.2, rk = 0.2, hood = 0.95, tex = ROOFTEX[s.roof], ya = -hl - rk, yb = hl + rk, yh = yb + hood;
      const P = [[-hw - ov, e - 0.28], [-hw + ks, zk], [0, zr], [hw - ks, zk], [hw + ov, e - 0.28]];
      for (let k = 0; k < 4; k++) { const a = P[k], b = P[k + 1], upper = k === 1 || k === 2, y1 = upper ? yh : yb, L = Math.hypot(b[0] - a[0], b[1] - a[1]);
        const v = k < 2 ? [[a[0], ya, a[1]], [a[0], y1, a[1]], [b[0], y1, b[1]], [b[0], ya, b[1]]] : [[b[0], ya, b[1]], [b[0], y1, b[1]], [a[0], y1, a[1]], [a[0], ya, a[1]]];
        F(out, v, 'roof', { tag: 'roof', tex, uv: k < 2 ? [[ya, 0], [y1, 0], [y1, L], [ya, L]] : [[-ya, 0], [-y1, 0], [-y1, L], [-ya, L]] }); }
      for (const [y, a, b] of [[ya, 0, 4], [yb, 0, 1], [yb, 3, 4], [yh, 1, 3]]) for (let k = a; k < b; k++) { const p = P[k], q = P[k + 1]; F(out, [[p[0], y, p[1] - 0.1], [q[0], y, q[1] - 0.1], [q[0], y, q[1] + 0.01], [p[0], y, p[1] + 0.01]], 'trim', { tag: 'roof.rake' }); }
      F(out, [[-hw + ks, yb, zk], [-hw + ks, yh, zk], [-hw + ks, yh, zk - 0.1], [-hw + ks, yb, zk - 0.1]], 'trim', { tag: 'roof.rake' }); F(out, [[hw - ks, yb, zk], [hw - ks, yh, zk], [hw - ks, yh, zk - 0.1], [hw - ks, yb, zk - 0.1]], 'trim', { tag: 'roof.rake' });
      for (const x of [-hw - ov, hw + ov]) F(out, [[x, ya, e - 0.38], [x, yb, e - 0.38], [x, yb, e - 0.28], [x, ya, e - 0.28]], 'trim', { tag: 'roof.fascia' });
      box(out, -0.06, 0.06, ya, yh, zr - 0.02, zr + 0.06, 'roof', { tag: 'roof.ridge', b: 1 });
      stick(out, [0, hl - 0.4, zr - 0.32], [0, yh - 0.05, zr - 0.32], 0.15, 'wood', { tag: 'show.hayBeam' }); cyl(out, 0, yh - 0.12, 0.07, zr - 0.62, zr - 0.4, 8, 'cp_iron', { tag: 'show.hayBeam' });
      stick(out, [0, yh - 0.12, zr - 0.62], [0, yh - 0.12, e + 1.9], 0.025, 'rope', { tag: 'show.hayBeam' });
      const cz = zr - 0.25; box(out, -0.45, 0.45, -0.45, 0.45, cz, cz + 0.85, 'trim', { tag: 'show.cupola', noTop: true });
      for (const [p0, p1, n] of [[[-0.45, 0.45], [0.45, 0.45], [0, 1]], [[0.45, 0.45], [0.45, -0.45], [1, 0]], [[0.45, -0.45], [-0.45, -0.45], [0, -1]], [[-0.45, -0.45], [-0.45, 0.45], [-1, 0]]]) { const Q = wallFrame(p0, p1, n); fbox(out, Q.M, 0.14, 0.76, cz + 0.28, cz + 0.72, 0, 0.012, 'dark', { tag: 'show.cupola' }); for (let k = 0; k < 4; k++) fbox(out, Q.M, 0.14, 0.76, cz + 0.32 + k * 0.1, cz + 0.36 + k * 0.1, 0.012, 0.04, 'trim', { tag: 'show.cupola' }); }
      const ap = cz + 1.35; for (const [a, b] of [[[-0.55, -0.55], [0.55, -0.55]], [[0.55, -0.55], [0.55, 0.55]], [[0.55, 0.55], [-0.55, 0.55]], [[-0.55, 0.55], [-0.55, -0.55]]]) F(out, [[a[0], a[1], cz + 0.85], [b[0], b[1], cz + 0.85], [0, 0, ap]], 'roof', { tag: 'show.cupola' });
      stick(out, [0, 0, ap], [0, 0, ap + 0.5], 0.025, 'cp_iron', { tag: 'show.vane' }); stick(out, [-0.3, 0, ap + 0.38], [0.3, 0, ap + 0.38], 0.03, 'cp_iron', { tag: 'show.vane' }); F(out, [[0.3, 0, ap + 0.3], [0.42, 0, ap + 0.38], [0.3, 0, ap + 0.46]], 'cp_iron', { tag: 'show.vane' });
      // the loft: floor over the whole barn, hay behind the loft door
      F(out, [[-hw + t, -hl + t, e], [hw - t, -hl + t, e], [hw - t, hl - t, e], [-hw + t, hl - t, e]], 'inside', { tag: 'store.loft', b: -1 });
      box(out, -1.2, 1.2, hl - t - 1.2, hl - t - 0.05, e, e + 1.3, 'hay', { tag: 'store.hay', tex: TEX.hay }); }
    // inside the ground floor
    F(out, [[-hw + t, -hl + t, zb], [hw - t, -hl + t, zb], [hw - t, hl - t, zb], [-hw + t, hl - t, zb]], 'floor', { tag: 'store.floor', tex: TEX.planks, uv: [[-hw, -hl], [hw, -hl], [hw, hl], [-hw, hl]] });
    const sx0 = hw - t - 1.95, sy1 = -hl + t + 2.7, rz = zb + 0.66;
    box(out, sx0, sx0 + 0.06, -hl + t, sy1, zb, zb + 1.4, 'wood', { tag: 'store.stall', tex: TEX.boards });
    box(out, sx0, hw - t, sy1 - 0.05, sy1, rz - 0.05, rz + 0.04, 'wood', { tag: 'store.stall' }); box(out, sx0, hw - t, sy1 - 0.05, sy1, zb + 1.18, zb + 1.26, 'wood', { tag: 'store.stall' });
    box(out, sx0 + 0.15, hw - t - 0.15, -hl + t, -hl + t + 0.45, zb, zb + 0.85, 'wood', { tag: 'store.manger', noTop: true }); F(out, [[sx0 + 0.18, -hl + t + 0.03, zb + 0.78], [hw - t - 0.18, -hl + t + 0.03, zb + 0.78], [hw - t - 0.18, -hl + t + 0.42, zb + 0.78], [sx0 + 0.18, -hl + t + 0.42, zb + 0.78]], 'hay', { tag: 'store.manger', tex: TEX.hay, uv: [[0, 0], [1.6, 0], [1.6, 0.4], [0, 0.4]] });
    C.solids.push({ id: 'stall', x0: sx0, x1: hw - t, y0: -hl + t, y1: sy1 });
    const fb = [-hw + t, -hw + t + 0.62, hl - t - 1.9, hl - t - 0.75]; box(out, fb[0], fb[1], fb[2], fb[3], zb, zb + 0.8, 'wood', { tag: 'store.feedBin', tex: TEX.boards, noTop: true });
    F(out, [[fb[0], fb[2], zb + 0.8], [fb[1], fb[2], zb + 0.86], [fb[1], fb[3], zb + 0.86], [fb[0], fb[3], zb + 0.8]], 'wood', { tag: 'store.feedBin', b: 1 });
    C.solids.push({ id: 'feedBin', x0: fb[0], x1: fb[1], y0: fb[2], y1: fb[3] });
    for (const [y, r] of [[-0.9, 0.24], [0.1, 0.22]]) { const x = -hw + t + 0.07, zc = zb + 0.78; stick(out, [-hw + t, y, zb + 1.0], [-hw + t + 0.16, y, zb + 1.04], 0.04, 'wood', { tag: 'store.harness' });
      for (let k = 0; k < 8; k++) { const a0 = k / 8 * 6.283, a1 = (k + 1) / 8 * 6.283; stick(out, [x, y + Math.cos(a0) * r * 0.7, zc + Math.sin(a0) * r], [x, y + Math.cos(a1) * r * 0.7, zc + Math.sin(a1) * r], 0.07, k < 5 ? 'leather' : 'rope', { tag: 'store.harness' }); } }
    for (let i = 0; i < 2; i++) for (let j = 0; j < 3; j++) { const x0 = -hw + t + 0.02, y0 = -hl + t + 0.02 + i * 0.47, z0 = zb + j * 0.38; box(out, x0, x0 + 0.9, y0, y0 + 0.45, z0, z0 + 0.37, 'hay', { tag: 'store.hay', tex: TEX.hay, topTex: TEX.hay }); }
    C.solids.push({ id: 'bales', x0: -hw + t, x1: -hw + t + 0.94, y0: -hl + t, y1: -hl + t + 0.96 });
    const lx = -0.35, ly = -hl + t + 0.08, ltop = s.cut ? Math.min(e, Wb.capZ || e) : e + 0.25; for (const x of [lx - 0.22, lx + 0.22]) stick(out, [x, ly, zb], [x, ly, ltop], 0.05, 'wood', { tag: 'store.ladder' });
    for (let z = zb + 0.25; z < ltop - 0.05; z += 0.25) stick(out, [lx - 0.22, ly, z], [lx + 0.22, ly, z], 0.035, 'wood', { tag: 'store.ladder' });
    C.solids.push({ id: 'ladder', x0: lx - 0.26, x1: lx + 0.26, y0: -hl + t, y1: ly + 0.1 });
    C.door = { x: 0, y: hl, z: zb, width: 2.8, height: 2.72, facing: '+Y', nrm: [0, 1], kind: 'sliding' };
    C.foot = [{ id: 'barn', x0: -hw - 0.08, x1: hw + 0.08, y0: -hl - 0.08, y1: hl + 0.08, walk: false }, { id: 'ramp', x0: -1.65, x1: 1.65, y0: hl + 0.06, y1: hl + 1.15, walk: true }];
    C.st = [
      { id: 'doors', verb: 'enter', clip: 'walk', at: [0, hl, zb], stand: [0, hl + 1.65], clearWidth: 2.8, note: 'the leaves slide along the track; the ramp runs up to the sill at 0.32' },
      { id: 'feedBin', verb: 'scoopFeed', clip: 'lift', also: ['place'], fixture: 'load', fixtureZ: 0.84, at: [(fb[0] + fb[1]) / 2, (fb[2] + fb[3]) / 2, zb + 0.84], stand: [fb[1] + 0.45, (fb[2] + fb[3]) / 2], standZ: zb },
      { id: 'harness', verb: 'takeHarness', clip: 'reach', fixture: 'rest', fixtureZ: 1.0, at: [-hw + t + 0.1, -0.4, zb + 1.0], stand: [-hw + t + 0.62, -0.4], standZ: zb },
      { id: 'stallRail', verb: 'leanOnRail', clip: 'rail', fixture: 'rail', fixtureZ: 0.66, at: [(sx0 + hw - t) / 2, sy1, rz], stand: [(sx0 + hw - t) / 2, sy1 + 0.45], standZ: zb },
      { id: 'ladder', verb: 'climb', clip: 'rung', fixture: 'rung', fixtureZ: 0.25, at: [lx, ly, zb + 0.25], stand: [lx, ly + 0.55], standZ: zb, note: 'rungs every 0.25 to the loft floor at 3.1' },
    ]; };
  B.henHouse = function (C) { const s = C.s, out = C.out, w = 2.2 + 0.4 * s.size, d = 1.6, hw = w / 2, hl = d / 2, zb = C.floor = 0.3, zF = 2.0, zB = 1.5, t = 0.06, z0 = zb - 0.06, sid = SIDTEX[s.siding] || TEX.boards;
    C.dims = { w, d, h: zF + 0.2 };
    for (const [x, y] of [[-hw + 0.06, -hl + 0.06], [hw - 0.06, -hl + 0.06], [-hw + 0.06, hl - 0.06], [hw - 0.06, hl - 0.06]]) box(out, x - 0.06, x + 0.06, y - 0.06, y + 0.06, 0, z0, 'wood', { tag: 'wall.leg' });
    const door = { u0: 0.14, u1: 0.92, z0: zb, z1: zb + 1.45 }, pop = { u0: w * 0.5, u1: w * 0.5 + 0.28, z0: zb + 0.02, z1: zb + 0.36, walk: false }, win = { u0: w - 0.74, u1: w - 0.2, z0: 1.22, z1: 1.68, walk: false };
    const Wf = wall(C, { p0: [-hw, hl], p1: [hw, hl], n: [0, 1], outline: [[0, z0], [w, z0], [w, zF], [0, zF]], holes: [door, pop, win], tex: sid, t, tag: 'show.front', eave: zF, z0 });
    const Wx = wall(C, { p0: [hw, hl], p1: [hw, -hl], n: [1, 0], outline: [[0, z0], [d, z0], [d, zB], [0, zF]], tex: sid, t, tag: 'wall.side', eave: s.cut ? zB : null, z0 });
    const Wb = wall(C, { p0: [hw, -hl], p1: [-hw, -hl], n: [0, -1], tex: sid, t, tag: 'wall.back', eave: zB, z0 });
    const Wn = wall(C, { p0: [-hw, -hl], p1: [-hw, hl], n: [-1, 0], outline: [[0, z0], [d, z0], [d, zF], [0, zB]], tex: sid, t, tag: 'wall.side', eave: s.cut ? zB : null, z0 });
    for (const q of [Wf, Wx, Wb, Wn]) cornerBoards(C, q, z0, zB);
    casing(C, Wf, door); casing(C, Wf, pop, { w: 0.05 }); windowIn(C, Wf, win, { cols: 2, rows: 1 });
    if (!Wf.drop) ledgeLeaf(C, Wf, door, 'u0', s.doorOpen * DEG100, 'door', { ledges: [zb + 0.14, zb + 1.2] });
    else F(out, [Wf.M(door.u0, zb + 0.005, 0.02), Wf.M(door.u1, zb + 0.005, 0.02), Wf.M(door.u1, zb + 0.005, -t), Wf.M(door.u0, zb + 0.005, -t)], 'trim', { tag: 'entry.threshold' });
    box(out, -hw + 0.18, -hw + 0.88, hl, hl + 0.4, 0, 0.16, 'stone', { tag: 'entry.steps', topTex: TEX.rubble });
    const px0 = -hw + pop.u0 - 0.01, px1 = -hw + pop.u1 + 0.01, ry = hl + 0.9; F(out, [[px0, hl, zb + 0.02], [px1, hl, zb + 0.02], [px1, ry, 0.02], [px0, ry, 0.02]], 'wood', { tag: 'show.ramp', tex: TEX.boards, uv: [[0, 0], [0.3, 0], [0.3, 0.95], [0, 0.95]] });
    for (let k = 1; k < 7; k++) { const f = k / 7, y = hl + (ry - hl) * f, z = zb + 0.02 + (0.02 - zb - 0.02) * f; box(out, px0, px1, y - 0.015, y + 0.015, z, z + 0.025, 'wood', { tag: 'show.ramp', b: 1 }); }
    const nx0 = hw - 0.74, nx1 = hw - 0.16; box(out, nx0, nx1, hl, hl + 0.4, 0.62, 0.98, 'body', { tag: 'show.nestBox', tex: sid, noTop: true });
    F(out, [[nx0 - 0.03, hl, 1.08], [nx1 + 0.03, hl, 1.08], [nx1 + 0.03, hl + 0.44, 0.96], [nx0 - 0.03, hl + 0.44, 0.96]], 'roof', { tag: 'show.nestBox', tex: ROOFTEX[s.roof], uv: [[0, 0], [0.6, 0], [0.6, 0.45], [0, 0.45]] });
    fbox(out, wallFrame([nx0, hl + 0.44], [nx1, hl + 0.44], [0, 1]).M, (nx1 - nx0) / 2 - 0.05, (nx1 - nx0) / 2 + 0.05, 0.9, 0.95, 0, 0.03, 'cp_iron', { tag: 'show.nestBox' });
    monoRoof(C, -hw, hw, hl, zF, -hl, zB, 0.25, 0.15, 0.12, { trim: 'trim' });
    F(out, [[-hw + t, -hl + t, zb], [hw - t, -hl + t, zb], [hw - t, hl - t, zb], [-hw + t, hl - t, zb]], 'straw', { tag: 'store.floor', tex: TEX.hay, uv: [[-hw, -hl], [hw, -hl], [hw, hl], [-hw, hl]] });
    for (const [y, z] of [[-hl + 0.32, zb + 0.55], [-hl + 0.62, zb + 0.8]]) { stick(out, [-hw + t, y, z], [hw - t, y, z], 0.045, 'wood', { tag: 'store.roost' }); }
    for (const x of [-hw + 0.3, hw - 0.3]) stick(out, [x, -hl + t, zb + 0.9], [x, -hl + 0.7, zb], 0.04, 'wood', { tag: 'store.roost' });
    cyl(out, hw - 0.35, 0.1, 0.12, zb, zb + 0.42, 8, 'galv', { tag: 'store.feeder' }); cyl(out, -0.55, -0.42, 0.13, zb, zb + 0.3, 8, 'galv', { tag: 'store.waterer' });
    C.solids.push({ id: 'roost', x0: -hw + t, x1: hw - t, y0: -hl + t, y1: -hl + 0.72 }, { id: 'feeder', x0: hw - 0.5, x1: hw - 0.2, y0: -0.05, y1: 0.25 });
    C.door = { x: -hw + (door.u0 + door.u1) / 2, y: hl, z: zb, width: door.u1 - door.u0, height: 1.45, facing: '+Y', nrm: [0, 1], kind: 'hinged' };
    C.foot = [{ id: 'henHouse', x0: -hw - 0.05, x1: hw + 0.05, y0: -hl - 0.05, y1: hl + 0.02, walk: false }, { id: 'nestBox', x0: nx0 - 0.03, x1: nx1 + 0.03, y0: hl, y1: hl + 0.44, walk: false }, { id: 'ramp', x0: px0, x1: px1, y0: hl, y1: ry, walk: false }, { id: 'step', x0: -hw + 0.18, x1: -hw + 0.88, y0: hl, y1: hl + 0.4, walk: true }];
    C.st = [
      { id: 'door', verb: 'enter', clip: 'walk', at: [C.door.x, hl, zb], stand: [C.door.x, hl + 1.5], clearWidth: door.u1 - door.u0, note: 'a stone step up to the sill at 0.30' },
      { id: 'nestBox', verb: 'collectEggs', clip: 'reach', fixture: 'rest', fixtureZ: 0.96, at: [(nx0 + nx1) / 2, hl + 0.3, 0.96], stand: [(nx0 + nx1) / 2, hl + 0.95], standZ: 0, note: 'lift the lid from outside; the lid edge is at 0.96' },
      { id: 'feeder', verb: 'fillFeeder', clip: 'reach', fixture: 'rest', fixtureZ: 0.42, at: [hw - 0.35, 0.1, zb + 0.42], stand: [hw - 0.95, 0.3], standZ: zb, note: 'the feeder hangs by the east wall; stand beside it, the coop is 1.6 m deep' },
    ]; };
  B.well = function (C) { const s = C.s, out = C.out, ro = 0.72, ri = 0.52, zc = 0.78, n = 14, hl = 0.8; C.floor = 0; C.dims = { w: 2.0, d: 1.6, h: 2.5 };
    const ring = (r) => { const P = []; for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI; P.push([Math.cos(a) * r, Math.sin(a) * r]); } return P; }, Po = ring(ro), Pi = ring(ri), pr = 2 * Math.PI * ro / n;
    for (let k = 0; k < n; k++) { const a = Po[k], b = Po[(k + 1) % n], c = Pi[k], d = Pi[(k + 1) % n];
      F(out, [[a[0], a[1], 0], [b[0], b[1], 0], [b[0], b[1], zc - 0.08], [a[0], a[1], zc - 0.08]], 'stone', { tag: 'yard.well', tex: TEX.rubble, uv: [[k * pr, 0], [(k + 1) * pr, 0], [(k + 1) * pr, zc], [k * pr, zc]] });
      F(out, [[c[0], c[1], 0.2], [d[0], d[1], 0.2], [d[0], d[1], zc], [c[0], c[1], zc]], 'stone', { tag: 'yard.well', b: -1, tex: TEX.rubble, uv: [[k * pr, 0.2], [(k + 1) * pr, 0.2], [(k + 1) * pr, zc], [k * pr, zc]] });
      const a2 = [a[0] * 1.04, a[1] * 1.04], b2 = [b[0] * 1.04, b[1] * 1.04];
      F(out, [[a2[0], a2[1], zc - 0.08], [b2[0], b2[1], zc - 0.08], [b2[0], b2[1], zc], [a2[0], a2[1], zc]], 'stone', { tag: 'yard.well', b: 1 });
      F(out, [[a2[0], a2[1], zc], [b2[0], b2[1], zc], [d[0], d[1], zc], [c[0], c[1], zc]], 'stone', { tag: 'yard.well', b: 1 }); }
    F(out, Pi.map(p => [p[0], p[1], 0.22]), 'water', { tag: 'yard.water' });
    const px = 0.86, pz = 2.02; for (const x of [-px, px]) box(out, x - 0.065, x + 0.065, -0.065, 0.065, 0, pz, 'wood', { tag: 'yard.well' });
    for (const x of [-px, px]) for (const sg of [-1, 1]) stick(out, [x, sg * 0.42, 0.02], [x, sg * 0.06, 0.62], 0.05, 'wood', { tag: 'yard.well' });
    cylX(out, -0.78, 0.78, 0, 1.5, 0.11, 8, 'wood', { tag: 'yard.windlass', endMat: 'cord' }); cylX(out, -0.22, 0.22, 0, 1.5, 0.125, 8, 'rope', { tag: 'yard.windlass' });
    stick(out, [0.8, 0, 1.5], [1.0, 0, 1.5], 0.04, 'cp_iron', { tag: 'yard.crank' }); stick(out, [1.0, 0, 1.5], [1.0, 0, 1.0], 0.035, 'cp_iron', { tag: 'yard.crank' }); stick(out, [1.0, 0, 1.0], [1.15, 0, 1.0], 0.045, 'wood', { tag: 'yard.crank' });
    const by = 0.62; cyl(out, 0, by, 0.15, zc, zc + 0.27, 9, 'wood', { tag: 'yard.bucket', topMat: 'water' }); for (const z of [zc + 0.05, zc + 0.21]) cyl(out, 0, by, 0.155, z, z + 0.03, 9, 'galv', { tag: 'yard.bucket', noTop: true });
    stick(out, [-0.14, by, zc + 0.27], [0, by, zc + 0.42], 0.018, 'cp_iron', { tag: 'yard.bucket' }); stick(out, [0.14, by, zc + 0.27], [0, by, zc + 0.42], 0.018, 'cp_iron', { tag: 'yard.bucket' });
    stick(out, [0, 0.1, 1.44], [0, by, zc + 0.42], 0.022, 'rope', { tag: 'yard.windlass' });
    if (!s.cut) { const e = 2.0, r = 2.42, ov = 0.14, tex = ROOFTEX[s.roof], hx = 1.0, L = Math.hypot(hl + ov, r - e);
      F(out, [[-hx, -hl - ov, e - 0.05], [hx, -hl - ov, e - 0.05], [hx, 0, r], [-hx, 0, r]], 'roof', { tag: 'roof', tex, uv: [[-hx, 0], [hx, 0], [hx, L], [-hx, L]] });
      F(out, [[hx, hl + ov, e - 0.05], [-hx, hl + ov, e - 0.05], [-hx, 0, r], [hx, 0, r]], 'roof', { tag: 'roof', tex, uv: [[hx, 0], [-hx, 0], [-hx, L], [hx, L]] });
      for (const x of [-hx, hx]) { F(out, [[x, -hl - ov, e - 0.13], [x, 0, r - 0.08], [x, 0, r + 0.01], [x, -hl - ov, e - 0.04]], 'wood', { tag: 'roof.rake' }); F(out, [[x, hl + ov, e - 0.13], [x, 0, r - 0.08], [x, 0, r + 0.01], [x, hl + ov, e - 0.04]], 'wood', { tag: 'roof.rake' });
        F(out, [[x * 0.95, -0.5, e - 0.02], [x * 0.95, 0.5, e - 0.02], [x * 0.95, 0, r - 0.05]], 'wood', { tag: 'roof.gable', b: -1 }); }
      stick(out, [-px, 0, pz], [px, 0, pz], 0.1, 'wood', { tag: 'yard.well' }); box(out, -hx, hx, -0.05, 0.05, r - 0.02, r + 0.05, 'roof', { tag: 'roof.ridge', b: 1 }); }
    C.solids.push({ id: 'curb', x0: -ro - 0.05, x1: ro + 0.05, y0: -ro - 0.05, y1: ro + 0.05 }, { id: 'posts', x0: -px - 0.1, x1: px + 0.1, y0: -0.45, y1: 0.45 });
    C.door = { x: 0, y: ro, z: 0, width: 1.0, height: 0, facing: '+Y', nrm: [0, 1], kind: 'none' };
    C.foot = [{ id: 'well', x0: -px - 0.12, x1: px + 0.12, y0: -ro - 0.05, y1: ro + 0.05, walk: false }, { id: 'crank', x0: px, x1: 1.2, y0: -0.1, y1: 0.1, walk: false }];
    C.st = [
      { id: 'bucket', verb: 'drawWater', clip: 'lift', also: ['place'], fixture: 'load', fixtureZ: zc, at: [0, by, zc], stand: [0, ro + 0.55], standZ: 0, note: 'the pail stands on the coping at 0.78 and goes down on the windlass rope' },
      { id: 'crank', verb: 'windBucket', clip: 'reach', fixture: 'rest', fixtureZ: 1.0, at: [1.1, 0, 1.0], stand: [1.1, 0.62], standZ: 0, note: 'the crank handle turns through 1.0 at the bottom of its throw' },
    ]; };
  B.pump = function (C) { const s = C.s, out = C.out; C.floor = 0; C.dims = { w: 1.3, d: 1.6, h: 1.3 };
    const p0 = [-0.6, 0.6, -0.8, 0.3]; box(out, p0[0], p0[1], p0[2], p0[3], 0, 0.14, 'wood', { tag: 'yard.pumpCover', topTex: TEX.boards });
    box(out, -0.12, 0.12, -0.35, -0.11, 0.14, 0.24, 'cp_iron', { tag: 'yard.pump' }); cyl(out, 0, -0.23, 0.075, 0.24, 1.14, 10, 'cp_iron', { tag: 'yard.pump' }); cyl(out, 0, -0.23, 0.095, 1.14, 1.22, 10, 'cp_iron', { tag: 'yard.pump' });
    stick(out, [0, -0.16, 0.92], [0, 0.3, 0.92], 0.06, 'cp_iron', { tag: 'yard.pump' }); stick(out, [0, 0.3, 0.92], [0, 0.34, 0.84], 0.055, 'cp_iron', { tag: 'yard.pump' });
    stick(out, [0, -0.23, 1.2], [0, -0.78, 0.98], 0.045, 'cp_iron', { tag: 'yard.pumpHandle' }); stick(out, [0, -0.2, 1.2], [0, -0.2, 1.32], 0.03, 'cp_iron', { tag: 'yard.pumpHandle' });
    const tr = [-0.52, 0.52, 0.26, 0.72], zt = 0.74; box(out, tr[0], tr[1], tr[2], tr[3], 0, zt, 'wood', { tag: 'yard.trough', tex: TEX.boards, noTop: true });
    F(out, [[tr[0] + 0.04, tr[2] + 0.04, zt - 0.08], [tr[1] - 0.04, tr[2] + 0.04, zt - 0.08], [tr[1] - 0.04, tr[3] - 0.04, zt - 0.08], [tr[0] + 0.04, tr[3] - 0.04, zt - 0.08]], 'water', { tag: 'yard.water' });
    for (const y of [tr[2], tr[3] - 0.04]) box(out, tr[0], tr[1], y, y + 0.04, zt - 0.01, zt + 0.01, 'wood', { tag: 'yard.trough', b: 1 });
    cyl(out, 0.36, -0.52, 0.13, 0.14, 0.42, 9, 'galv', { tag: 'yard.pail', topMat: 'dark' }); stick(out, [0.24, -0.52, 0.42], [0.36, -0.52, 0.56], 0.016, 'cp_iron', { tag: 'yard.pail' }); stick(out, [0.48, -0.52, 0.42], [0.36, -0.52, 0.56], 0.016, 'cp_iron', { tag: 'yard.pail' });
    C.solids.push({ id: 'pump', x0: -0.62, x1: 0.62, y0: -0.82, y1: 0.74 });
    C.door = { x: 0, y: 0.72, z: 0, width: 1.0, height: 0, facing: '+Y', nrm: [0, 1], kind: 'none' };
    C.foot = [{ id: 'pump', x0: -0.62, x1: 0.62, y0: -0.82, y1: 0.74, walk: false }];
    C.st = [
      { id: 'trough', verb: 'fillPail', clip: 'lift', also: ['place'], fixture: 'load', fixtureZ: zt, at: [0, 0.5, zt], stand: [0, 1.2], standZ: 0, note: 'the spout runs into the trough; a pail stands on its rim at 0.74 to fill' },
      { id: 'handle', verb: 'pump', clip: 'reach', fixture: 'rest', fixtureZ: 0.98, at: [0, -0.78, 0.98], stand: [0.1, -1.3], standZ: 0, note: 'the handle end rests at 0.98' },
    ]; };

  // ---- assembly, caches, the light ------------------------------------------------------------------------------------
  const CHAR92 = { radiusMax: 0.229, footprintMax: [0.458, 0.370], fits: { bench: [0.643, 0.853], knife: [0.598, 0.760], load: [0.718, 0.978], rest: [0, 1.012], rail: [0.549, 0.724], rung: [0.211, 0.273] } };
  function assemble(kind, opts, dir) { const s = resolve(kind, opts), C = { s, out: [], walls: [], solids: [], st: [], dir: s.cut ? (dir | 0) : null, win: 0, floor: 0 };
    (B[kind] || B.shed)(C); if (s.mirror) for (const f of C.out) f.v = f.v.map(p => [-p[0], p[1], p[2]]); return C; }
  const GEO = new Map();
  function geo(kind, opts) { const s = resolve(kind, opts), key = kind + '|' + JSON.stringify(Object.assign({}, s, { doorOpen: 0, loftOpen: 0, cut: false, weather: 0, kept: 0 })); let g = GEO.get(key); if (g) return g;
    g = assemble(kind, Object.assign({}, opts, { cutaway: null, doorOpen: 0 }), null); g.out = null; if (GEO.size > 64) GEO.delete(GEO.keys().next().value); GEO.set(key, g); return g; }
  const mx = (s, p) => s.mirror ? [-p[0], p[1]] : p, mxR = (s, q) => s.mirror ? Object.assign({}, q, { x0: -q.x1, x1: -q.x0 }) : q;
  function dims(kind, o) { return Object.assign({}, geo(kind, o).dims); }
  function footprint(kind, o) { const s = resolve(kind, o); return geo(kind, o).foot.map(q => mxR(s, q)); }
  function layout(kind, o) { const s = resolve(kind, o), g = geo(kind, o), d = g.door; return { show: '+Y', door: Object.assign({}, d, { x: s.mirror ? -d.x : d.x }) }; }
  const NAMES = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  const toWorld = (p, dir) => { const a = dir * Math.PI / 4; return [p[0] * Math.cos(a) - p[1] * Math.sin(a), p[0] * Math.sin(a) + p[1] * Math.cos(a)]; };
  const compass = (v) => NAMES[((Math.round(Math.atan2(v[0], v[1]) / (Math.PI / 4)) % 8) + 8) % 8];
  function placement(kind, o) { const L = layout(kind, o), n = L.door.nrm, list = [];
    for (let d = 0; d < 8; d++) { const w = toWorld(n, d); if (w[1] > 0.3) continue; list.push({ dir: d, doorFaces: compass(w), via: w[1] < -0.3 ? 'door' : 'side' }); }
    const rank = (f) => (f.via === 'door' ? 0 : 2) + (f.dir % 2 ? 0 : 1); list.sort((a, b) => rank(a) - rank(b) || a.dir - b.dir);
    return { show: '+Y', entry: '+Y', facings: list }; }
  function camBasis(dir, elev) { const th = dir * Math.PI / 4, e = (elev != null ? elev : DEFAULT_ELEV) * DEG; return { ct: Math.cos(th), st: Math.sin(th), se: Math.sin(e), ce: Math.cos(e) }; }
  function project(dir, p, elev) { const B0 = camBasis(dir, elev), xr = p[0] * B0.ct - p[1] * B0.st, yr = p[0] * B0.st + p[1] * B0.ct; return { x: cx + xr * S, y: groundY - (yr * B0.se + (p[2] || 0) * B0.ce) * S }; }
  const pt = (dir, m, z, elev) => { const q = project(dir, [m[0], m[1], z || 0], elev), w = toWorld(m, dir); return { x: +q.x.toFixed(1), y: +q.y.toFixed(1), m: [+m[0].toFixed(3), +m[1].toFixed(3)], w: [+w[0].toFixed(3), +w[1].toFixed(3)] }; };
  function anchors(kind, dir, o) { o = o || {}; const s = resolve(kind, o), g = geo(kind, o), d = g.door, dp = mx(s, [d.x, d.y]), n = d.nrm, ap = [dp[0] + n[0] * 1.5, dp[1] + n[1] * 1.5 + (kind === 'barn' ? 0.15 : 0)];
    const out = { show: '+Y', door: pt(dir, dp, d.z + Math.min(1, d.height || 0.5), o.elev), threshold: pt(dir, dp, d.z, o.elev), doorFaces: compass(toWorld(n, dir)), approach: pt(dir, ap, 0, o.elev), entryPath: [pt(dir, ap, 0, o.elev), pt(dir, [ap[0], ap[1] + 1.0], 0, o.elev)] };
    if (kind === 'barn') { const L = assemble(kind, o, dir).lamp; if (L) out.lamp = Object.assign(pt(dir, [L[0], L[1]], L[2], o.elev), { z: +L[2].toFixed(3) }); }
    return out; }
  function stations(kind, o) { o = o || {}; const s = resolve(kind, o), g = geo(kind, o), r = CHAR92.radiusMax, d = g.door, ap = [d.x + d.nrm[0] * 1.5, d.y + d.nrm[1] * 1.5];
    const yaw = (v) => +(Math.atan2(v[0], v[1]) * 180 / Math.PI).toFixed(1), fit = (k, z) => { const f = CHAR92.fits[k]; return !!f && z >= f[0] - 1e-9 && z <= f[1] + 1e-9; };
    // reachability: a 0.1 m grid from the approach, round the wall segments (openings are gaps) and the furnishings
    let X0 = 1e9, X1 = -1e9, Y0 = 1e9, Y1 = -1e9; for (const q of g.foot) { X0 = Math.min(X0, q.x0); X1 = Math.max(X1, q.x1); Y0 = Math.min(Y0, q.y0); Y1 = Math.max(Y1, q.y1); }
    X0 -= 2; X1 += 2; Y0 -= 2; Y1 += 2.6; const st = 0.1, nx = Math.ceil((X1 - X0) / st) + 1, ny = Math.ceil((Y1 - Y0) / st) + 1, seen = new Uint8Array(nx * ny);
    const segD = (p, a, b) => { const vx = b[0] - a[0], vy = b[1] - a[1], L2 = vx * vx + vy * vy || 1e-9, t = clamp(((p[0] - a[0]) * vx + (p[1] - a[1]) * vy) / L2, 0, 1); return Math.hypot(p[0] - a[0] - vx * t, p[1] - a[1] - vy * t); };
    const outside = g.foot.filter(q => !q.walk && !['shed', 'barn', 'henHouse', 'woodshed', 'well', 'pump'].includes(q.id));
    const blocked = (x, y) => g.walls.some(w => segD([x, y], w.a, w.b) < r + w.t / 2) || g.solids.some(q => x > q.x0 - r && x < q.x1 + r && y > q.y0 - r && y < q.y1 + r) || outside.some(q => x > q.x0 - r && x < q.x1 + r && y > q.y0 - r && y < q.y1 + r);
    const cell = (p) => [Math.round((p[0] - X0) / st), Math.round((p[1] - Y0) / st)], [si, sj] = cell(ap), Q = [];
    if (!blocked(ap[0], ap[1])) { seen[sj * nx + si] = 1; Q.push(sj * nx + si); }
    for (let q = 0; q < Q.length; q++) { const k = Q[q], i = k % nx, j = (k - i) / nx; for (const [a, b] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const ii = i + a, jj = j + b; if (ii < 0 || jj < 0 || ii >= nx || jj >= ny) continue; const kk = jj * nx + ii; if (seen[kk]) continue; if (blocked(X0 + ii * st, Y0 + jj * st)) continue; seen[kk] = 1; Q.push(kk); } }
    const reach = (p) => { const [i, j] = cell(p); for (let dj = -1; dj <= 1; dj++) for (let di = -1; di <= 1; di++) { const ii = i + di, jj = j + dj; if (ii >= 0 && jj >= 0 && ii < nx && jj < ny && seen[jj * nx + ii]) return true; } return false; };
    const S0 = g.st.map(q => { const v = [q.at[0] - q.stand[0], q.at[1] - q.stand[1]], z = q.fixtureZ;
      const o2 = Object.assign({ standZ: 0 }, q, { faceYaw: yaw(v), reachable: reach(q.stand) }); if (q.fixture) { o2.fit = CHAR92.fits[q.fixture].slice(); o2.fits = fit(q.fixture, z); } if (q.clearWidth != null) o2.fits = q.clearWidth >= CHAR92.footprintMax[0] + 0.3; return o2; });
    if (s.mirror) for (const q of S0) { q.at = [-q.at[0], q.at[1], q.at[2]]; q.stand = [-q.stand[0], q.stand[1]]; q.faceYaw = -q.faceYaw; }
    return { character: Object.assign({ rig: 'characterIsoRig9.js', revision: '9.2' }, CHAR92), approach: mx(s, ap), stations: S0, requests: kind === 'barn' ? [{ verb: 'pitchHay', note: 'v9.2 has no fork clip; the loft door and the hay beam are decor until one lands' }] : [] }; }
  const MODELS = new Map(), SKIP = { sky: 1, time: 1, cloud: 1, rain: 1, fog: 1, snow: 1, wind: 1, outline: 1, elev: 1, night: 1, lamp: 1, occupancy: 1, schedule: 1 };
  function liveModel(kind, opts, dir) { const CP = root.CoastalPass; if (!CP || !CP.light) return null; const s = resolve(kind, opts), o = {};
    for (const k of Object.keys(opts).sort()) if (!SKIP[k] && typeof opts[k] !== 'function') o[k] = opts[k];
    const key = kind + '|' + JSON.stringify(o) + (s.cut ? '|' + (dir | 0) : ''); let m = MODELS.get(key); if (m) return m;
    const C = assemble(kind, opts, dir); m = CP.light.model(C.out, mats(s)); m.kind = kind; m.lamp = C.lamp || null;
    if (MODELS.size > 48) MODELS.delete(MODELS.keys().next().value); MODELS.set(key, m); return m; }
  function frame(kind, dir, opts, fo) { opts = opts || {}; const m = liveModel(kind, opts, dir); if (!m) return null; const B0 = camBasis(dir, opts.elev);
    const fr = root.CoastalPass.light.frame(m, { ct: B0.ct, st: B0.st, se: B0.se, ce: B0.ce, S, ox: cx, oy: groundY }, W, H, fo); fr.dir = dir; fr.kind = kind; fr.opts = opts; return fr; }
  function lampLevel(opts, sky) { const CPL = root.CoastalPass.light, need = CPL.lampNeed(sky), t = sky && sky.time != null ? ((+sky.time % 24) + 24) % 24 : 14, L = opts.lamp;
    if (L === 'off') return 0; if (typeof L === 'number') return clamp(L, 0, 1) * need; return (t >= 5 || t < 0.5) && need > 0.05 ? need : 0; }
  function lightRig(fr, opts, sky) { const m = fr.mdl, lv = m.lamp ? lampLevel(opts, sky) : 0, p = m.lamp ? (resolve(fr.kind, opts).mirror ? [-m.lamp[0], m.lamp[1], m.lamp[2]] : m.lamp) : null;
    return { level: lv, emit: (name) => name === 'lantern' ? lv : 0, lights: p && lv > 0.01 ? [{ p, c: '#ffc774', I: 0.95 * lv, r: 3.4 }] : [] }; }
  function relight(fr, sky, o) { o = o || {}; const CPL = root.CoastalPass.light; sky = sky || CPL.REF_SKY; const L = lightRig(fr, Object.assign({}, fr.opts, o), sky);
    return CPL.relight(fr, sky, Object.assign({ emit: L.emit, lights: L.lights }, o)); }
  function renderLive(kind, dir, opts) { opts = opts || {}; const fr = frame(kind, dir, opts), CPL = root.CoastalPass.light, sky = CPL.skyOf(opts), L = lightRig(fr, opts, sky);
    return CPL.relight(fr, sky, { emit: L.emit, lights: L.lights, outline: !!opts.outline }); }
  function castShadow(fr, sky, o) { return root.CoastalPass.light.castShadow(fr, sky || root.CoastalPass.light.REF_SKY, o); }
  function view(fr, ch, sky, o) { return (!ch || ch === 'lit') ? relight(fr, sky, o) : root.CoastalPass.light.view(fr, ch, sky, o); }
  function lights(kind, dir, o) { o = o || {}; const CPL = root.CoastalPass.light, sky = CPL.skyOf(o), g = assemble(kind, o, dir); if (!g.lamp) return { lamps: [] };
    const s = resolve(kind, o), p = s.mirror ? [-g.lamp[0], g.lamp[1], g.lamp[2]] : g.lamp, lv = lampLevel(o, sky), q = project(dir, p, o.elev), pool = [p[0], p[1] + 1.2];
    return { lamps: [{ id: 'barnLamp', level: +lv.toFixed(3), m: p.map(v => +v.toFixed(3)), screen: { x: +q.x.toFixed(1), y: +q.y.toFixed(1) }, pool: { m: pool.map(v => +v.toFixed(3)), r: 3.4, screen: project(dir, [pool[0], pool[1], 0], o.elev) } }] }; }
  function render(kind, dir, opts) { return renderLive(kind, dir, opts || {}); }

  root.Outbuilding = { W, H, PX, pivot: { x: cx, y: groundY }, defaultElev: DEFAULT_ELEV, order: NAMES, KINDS, BODY, TRIM, ROOFS, PAINT, SIDINGS, ROOF_OPTIONS, CHAR: CHAR92,
    resolve, dims, footprint, layout, placement, anchors, stations, lights, frame, relight, castShadow, view, renderLive, render, project, list: () => Object.keys(KINDS) };
})(typeof globalThis !== 'undefined' ? globalThis : window);
