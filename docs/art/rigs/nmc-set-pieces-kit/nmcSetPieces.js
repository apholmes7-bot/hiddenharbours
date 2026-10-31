/* Hidden Harbours — NINE MILE CREEK SET PIECES (globalThis.NmcSetPieces). The wharf's own pieces, 2026-09-28, for
   keyscene.nmc_wharf. v1.1 (the scene): the harbourmaster's office (a box travel trailer towed in and set up on blocks, with
   the licence counter), the wharf crane at the unloading berth, now playable (CRANE, LOADS, craneState), the harbour lights on
   the two breakwater heads, the restaurant on the north row and the fish market beside the office.
   v1.2 (2026-09-30, the key scenes, part 2): the Route 91 bridge, the river wall, the town boardwalks, their lamps, the
   restaurant and bar's river deck, the wall stairs and the chandlery (the town bridge), and the pieces the other scenes add.
   Every 1.1 piece is unchanged.
   v1.4 (2026-09-30, part 2c): the north creek's docks (creekDock) and the weather face's gully steps (gullySteps). Every 1.1 to 1.3
   piece is unchanged.
   v1.3 (2026-09-30, part 2b): the Falls' water and fishing deck, guard rails, benches, sign boards, the crossing board, the bar's
   stakes, a life ring post, the campground's comfort station, fire rings, tents, site posts, picnic tables and site lights.
   Every 1.1 and 1.2 piece is unchanged.

   Built the way the St Peters set pieces are (stPetersSetPieces.js, the same API): every piece is a face model lit by
   CoastalPass.light (sky x visibility, sun x shadow map, bounce, the band ladder, the sky's grade; rain, fog, snow by
   material). No dither, no keyline. Colour is ramps only. Load coastalPass.js (and weatherSky.js) first. Plain script.

   CAMERA 32 px = 1 m, 40 deg; dir 0-7 in 45 deg steps; local +Y is the show face (dir 4: it faces S, toward the camera).
   PIVOT the projection of the local origin, the piece's ground point. WORDS ARE DATA: every board the game letters is blank,
   tagged 'words.<id>', its corners in anchors().words. EMITTERS: faces with em set glow with o.lit (0-1).               */
(function (root) {
  'use strict';
  const VERSION = '1.4.0', PPU = 32, D2R = Math.PI / 180, SE = Math.sin(40 * D2R), CE = Math.cos(40 * D2R);
  const LT = () => { const c = root.CoastalPass; if (!c || !c.light) throw new Error('NmcSetPieces needs coastalPass.js (CoastalPass.light) loaded first'); return c.light; };
  // ---- ramps, dark -> light, six steps. CoastalPass knows wood, glass, trim, stone, iron by name and keeps their weather
  const RAMPS = {
    siding: ['#5c615e', '#7a7f7a', '#979b95', '#b3b6ae', '#cbccc2', '#dedfd4'],     // white aluminium siding, a few summers old
    orange: ['#5b2a0f', '#7d3a13', '#a14f1b', '#c16827', '#d88338', '#e69e52'],     // the stripe
    brown: ['#2b1b12', '#3c2619', '#4f3323', '#63412c', '#775238', '#8b6446'],      // the stripe's lower band, the skirting
    tan: ['#5d4a28', '#786034', '#957a43', '#b09454', '#c7ab67', '#d8bf7d'],        // the pinstripe
    trim: ['#565b58', '#737872', '#90948d', '#adb0a7', '#c7c9bf', '#dcddd2'],
    alum: ['#474d50', '#5d6468', '#767d80', '#8f9699', '#a8aeb0', '#bfc4c5'],
    glass: ['#0f191e', '#15232a', '#1d2f38', '#293e48', '#394f5a', '#4e656e'],
    wood: ['#2c2823', '#3d3730', '#524a40', '#6a6153', '#837966', '#9d937e'],
    ptwood: ['#3a3322', '#4f462e', '#665b3b', '#7d7049', '#948659', '#aa9b6b'],     // pressure-treated steps, greyed
    iron: ['#19262b', '#26383d', '#3a4d50', '#516566', '#687d79', '#899790'],
    rust: ['#2b1c15', '#43271a', '#5d331f', '#774325', '#90562e', '#a66a3c'],
    tyre: ['#111213', '#191b1d', '#222527', '#2c2f32', '#373b3e', '#43474a'],
    conc: ['#51524c', '#686962', '#7f8077', '#96978c', '#acada0', '#bfbfb2'],
    galv: ['#4b5155', '#61686c', '#7a8185', '#94999d', '#adb2b5', '#c4c8ca'],
    yellow: ['#473710', '#685115', '#8c6d1b', '#ad8925', '#c9a334', '#dcb94f'],     // crane yellow, chalked by salt
    hose: ['#101112', '#17191a', '#1f2123', '#282b2d', '#323537', '#3e4143'],
    chrome: ['#3a4144', '#566064', '#76838a', '#98a5ab', '#bcc6ca', '#dde3e5'],
    tote: ['#0d2136', '#142f4c', '#1d3f65', '#28517f', '#35659a', '#4b7db4'],       // fish totes, blue HDPE
    propane: ['#5b605f', '#777c7a', '#939794', '#aeb1ad', '#c6c8c3', '#d9dad5'],
    red: ['#3a1b19', '#53251f', '#6e3226', '#894130', '#a2553d', '#b96b51'],
    green: ['#132a1d', '#1b3a29', '#244c35', '#2f6043', '#3b7452', '#4b8963'],
    board: ['#55503e', '#6d674f', '#877f62', '#a09776', '#b9ae8c', '#cfc3a2'],
    amber: ['#5c3a1a', '#93602b', '#c88b3d', '#e6b35a', '#f6d68b', '#fff0c2'],      // emitters
    lampR: ['#4a1010', '#7a1a18', '#a82a22', '#d0443a', '#ea6a5c', '#fa9a8a'],
    lampG: ['#0c3a22', '#146034', '#1e8a4a', '#34ae64', '#5ccc86', '#96e8b0'],
    lampW: ['#5c5a4a', '#8e8a6c', '#bcb690', '#ddd7b0', '#f2edcf', '#fffbea'],
    butter: ['#6a5a33', '#8a7642', '#a99254', '#c4ac68', '#d9c27f', '#e8d59a'],     // the restaurant's clapboard, buttermilk yellow
    roofAsph: ['#1e2124', '#2a2e31', '#373b3e', '#44494b', '#52575a', '#62676a'],   // asphalt shingles
    shake: ['#3e3a33', '#524d44', '#686256', '#7e7768', '#948c7b', '#a9a08d'],      // cedar shakes gone silver
    navy: ['#0f1a2a', '#16253a', '#1f324d', '#2a4262', '#375478', '#48688e'],
    canvasB: ['#10283d', '#173856', '#214a6f', '#2d5d88', '#3c719f', '#5287b5'],   // the market's awning
    canvasG: ['#15291f', '#1d392b', '#274b38', '#325e46', '#3f7255', '#528667'],   // the takeout's awning
    chalk: ['#141816', '#1b201d', '#232925', '#2c332e', '#363e38', '#424b44'],
    trapwire: ['#2c3a1c', '#3e5125', '#526a2f', '#68833a', '#809b47', '#9bb35b'],   // vinyl-coated trap wire
    flagR: ['#4a0e10', '#6e1416', '#951c1e', '#b72628', '#d23536', '#e45250'],
    // 1.2.0, the key scenes: granite and its weed band, the bridge's asphalt and paint, brick pavers, railway green, cast iron, bronze, flowers
    granite: ['#3e3e41', '#54524e', '#6a6762', '#817d76', '#99948b', '#b1aba0'],
    graniteWet: ['#252925', '#333730', '#42463b', '#525646', '#636752', '#75795f'],
    asphalt: ['#1d2024', '#272a2e', '#313539', '#3c4044', '#484c50', '#55595d'],
    lineY: ['#6a5412', '#8a6c17', '#a9851e', '#c49d29', '#d8b23d', '#e6c457'],
    lineW: ['#8a8c89', '#a3a5a1', '#babcb7', '#cfd0cb', '#dfe0db', '#ededea'],
    brick: ['#46211a', '#5c2c22', '#73392b', '#8a4735', '#a05741', '#b3694f'],
    railGreen: ['#0f201a', '#152c23', '#1c392d', '#244837', '#2e5843', '#3a6a51'],
    cast: ['#16181a', '#1e2124', '#272b2e', '#31363a', '#3c4246', '#495055'],
    bronze: ['#3a2a14', '#4e391b', '#634922', '#795a2b', '#8f6c36', '#a47f44'],
    leaf: ['#1a2e14', '#23401a', '#2d5221', '#386428', '#447632', '#52883d'],
    flowerR: ['#5a0f1c', '#7d1627', '#a11f33', '#c22c41', '#dc4254', '#ec6068'],
    flowerY: ['#6b4d08', '#8f680c', '#b38612', '#d1a31d', '#e6bd33', '#f1d25a'],
    rope: ['#3f3322', '#54442c', '#6a5737', '#806a44', '#967e53', '#ab9264'],
    // 1.3.0: white water and the river's lip, charcoal, flame
    foam: ['#6f8c93', '#8eaeb4', '#b0cccf', '#cfe2e2', '#e6f1ef', '#f6fbfa'],
    waterLip: ['#2d4a52', '#3a5c63', '#4a6f75', '#5d8387', '#739799', '#8aabab'],
    charcoal: ['#141312', '#1d1b19', '#272421', '#322e2a', '#3e3934', '#4b453f'],
    flame: ['#7a2a06', '#a83c08', '#d2590d', '#ec7e1c', '#f7a53a', '#fcd070'],
    redstone: ['#3f2319', '#57301f', '#6d3f2c', '#8a4f36', '#a86444', '#c07a57'],
    redstoneWet: ['#28150f', '#391f16', '#4c2a1e', '#623727', '#784532', '#8e5540'],
  };
  const mats = (keys) => { const M = {}; for (const k of keys) M[k] = { ramp: RAMPS[k].slice() }; return M; };
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  // ---- geometry helpers (the St Peters kit's, verbatim in behaviour) ---------------------------------------------------------
  function F(out, v, mat, b, tag, em, tx) { const f = { v, mat, b: b || 0, db: 0, tag: tag || null, em: em || null }; if (tx) { f.uv = tx.uv; f.tex = tx.tex; } out.push(f); }
  const TEX = {
    hribs: (u, v) => ((v / 0.115) % 1 + 1) % 1 < 0.3 ? -1 : 0,                       // aluminium siding, horizontal
    ribs: (u) => ((u / 0.16) % 1 + 1) % 1 < 0.45 ? 0 : -1,
    boards: (u, v) => ((v / 0.2) % 1 + 1) % 1 < 0.16 ? -1 : 0,
    treads: (u) => ((u / 0.14) % 1 + 1) % 1 < 0.12 ? -1 : 0,
    louvre: (u, v) => ((v / 0.09) % 1 + 1) % 1 < 0.28 ? -1 : 0,                      // jalousie glass
    grate: (u, v) => (Math.floor(u / 0.05) + Math.floor(v / 0.05)) & 1 ? -1 : 0,
    clap: (u, v) => ((v / 0.14) % 1 + 1) % 1 < 0.22 ? -1 : 0,                         // clapboard, 14 cm to the weather
    shakes: (u, v) => { const r = Math.floor(v / 0.2), o = ((r * 0.618034) % 1) * 0.16; return ((v / 0.2) % 1 + 1) % 1 < 0.15 || ((((u + o) / 0.16) % 1) + 1) % 1 < 0.1 ? -1 : 0; },   // cedar shakes, offset course by course
    shingles: (u, v) => { const r = Math.floor(v / 0.2); return ((v / 0.2) % 1 + 1) % 1 < 0.2 || (((u / 0.34) + (r % 2) * 0.5) % 1 + 1) % 1 < 0.06 ? -1 : 0; },
    wire: (u, v) => ((u / 0.05) % 1 + 1) % 1 < 0.5 && ((v / 0.05) % 1 + 1) % 1 < 0.5 ? 0 : -1,
    // 1.2.0: coursed ashlar (0.42 m courses, 0.9 m blocks, broken joints, a lighter block now and then); brick pavers in running bond
    ashlar: (u, v) => { const row = Math.floor(v / 0.42), fv = v - row * 0.42; if (fv < 0.045) return -1; const uu = u + ((row & 1) ? 0.45 : 0), col = Math.floor(uu / 0.9), fu = uu - col * 0.9; if (fu < 0.045) return -1; return (((col * 73856093) ^ (row * 19349663)) >>> 0) % 7 === 0 ? 1 : 0; },
    pavers: (u, v) => { const row = Math.floor(v / 0.1); return ((v / 0.1) % 1 + 1) % 1 < 0.14 || ((((u / 0.2) + (row & 1) * 0.5) % 1) + 1) % 1 < 0.1 ? -1 : 0; },
    // 1.3.0: sandstone bedding: beds about 0.2 m, a lighter bed now and then, a joint every metre or so
    bedding: (u, v) => { const row = Math.floor(v / 0.2), fv = v - row * 0.2; if (fv < 0.035) return -1; const col = Math.floor(u / 1.1 + row * 0.37), fu = u / 1.1 + row * 0.37 - col; if (fu < 0.03) return -1; let h = Math.imul(row + 7, 374761393) ^ Math.imul(col + 3, 668265263); h = Math.imul(h ^ (h >>> 13), 1274126177); return ((h ^ (h >>> 16)) >>> 0) % 6 === 0 ? 1 : 0; },
  };
  // walls take their siding lines from the height, so the courses run level round a building and across its gables
  function FTz(out, v, mat, b, tag, name, em) { F(out, v, mat, b, tag, em || null, { uv: v.map(p => [p[0] + p[1], p[2]]), tex: TEX[name] }); }
  function boxT(out, x0, x1, y0, y1, z0, z1, mat, tag, tex) {
    FT(out, [[x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1]], mat, -0.15, tag, tex); FT(out, [[x1, y1, z0], [x0, y1, z0], [x0, y1, z1], [x1, y1, z1]], mat, 0.15, tag, tex);
    FT(out, [[x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1]], mat, 0, tag, tex); FT(out, [[x0, y1, z0], [x0, y0, z0], [x0, y0, z1], [x0, y1, z1]], mat, -0.3, tag, tex);
    FT(out, [[x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]], mat, 0.1, tag, tex); }
  function tq(v, name) { const A = v[0], B = v[1], D = v[v.length - 1], du = [B[0] - A[0], B[1] - A[1], B[2] - A[2]], dv = [D[0] - A[0], D[1] - A[1], D[2] - A[2]], Lu = Math.hypot(du[0], du[1], du[2]) || 1, Lv = Math.hypot(dv[0], dv[1], dv[2]) || 1;
    const uv = v.map(p => { const q = [p[0] - A[0], p[1] - A[1], p[2] - A[2]]; return [(q[0] * du[0] + q[1] * du[1] + q[2] * du[2]) / Lu, (q[0] * dv[0] + q[1] * dv[1] + q[2] * dv[2]) / Lv]; }); return { uv, tex: TEX[name] }; }
  function FT(out, v, mat, b, tag, name, em) { F(out, v, mat, b, tag, em || null, tq(v, name)); }
  function box(out, x0, x1, y0, y1, z0, z1, mat, tag, o) {
    o = o || {};
    if (!o.noFront) F(out, [[x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1]], mat, -0.15, o.tagFront || tag, o.em);
    if (!o.noBack) F(out, [[x1, y1, z0], [x0, y1, z0], [x0, y1, z1], [x1, y1, z1]], mat, 0.15, o.tagBack || tag, o.em);
    F(out, [[x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1]], mat, 0, o.tagSide || tag, o.em);
    F(out, [[x0, y1, z0], [x0, y0, z0], [x0, y0, z1], [x0, y1, z1]], mat, -0.3, o.tagSide || tag, o.em);
    if (!o.noTop) F(out, [[x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]], mat, 0.1, o.tagTop || tag, o.em);
  }
  function stick(out, A, B, w, mat, tag) {
    const d = [B[0] - A[0], B[1] - A[1], B[2] - A[2]], L = Math.hypot(d[0], d[1], d[2]) || 1, u = [d[0] / L, d[1] / L, d[2] / L];
    const ref = Math.abs(u[2]) < 0.9 ? [0, 0, 1] : [1, 0, 0], cr = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    let p = cr(u, ref); const pl = Math.hypot(p[0], p[1], p[2]); p = p.map(v => v / pl * w / 2); let q = cr(u, p); const ql = Math.hypot(q[0], q[1], q[2]); q = q.map(v => v / ql * w / 2);
    const C = (P, sp, sq) => [P[0] + p[0] * sp + q[0] * sq, P[1] + p[1] * sp + q[1] * sq, P[2] + p[2] * sp + q[2] * sq], R = [[1, 1], [-1, 1], [-1, -1], [1, -1]];
    for (let k = 0; k < 4; k++) { const a = R[k], b = R[(k + 1) % 4]; F(out, [C(A, a[0], a[1]), C(A, b[0], b[1]), C(B, b[0], b[1]), C(B, a[0], a[1])], mat, [0, -0.15, -0.3, 0.1][k], tag); }
    F(out, R.map(r => C(B, r[0], r[1])), mat, 0.1, tag); F(out, R.slice().reverse().map(r => C(A, r[0], r[1])), mat, -0.2, tag);
  }
  function cyl(out, cx, cy, z0, z1, r0, r1, n, mat, tag, noTop, em) {
    for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI, b = (k + 1.5) / n * 2 * Math.PI;
      F(out, [[cx + r0 * Math.cos(a), cy + r0 * Math.sin(a), z0], [cx + r0 * Math.cos(b), cy + r0 * Math.sin(b), z0], [cx + r1 * Math.cos(b), cy + r1 * Math.sin(b), z1], [cx + r1 * Math.cos(a), cy + r1 * Math.sin(a), z1]], mat, Math.sin(a) < 0 ? -0.1 : 0.05, tag, em); }
    if (!noTop) { const v = []; for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI; v.push([cx + r1 * Math.cos(a), cy + r1 * Math.sin(a), z1]); } F(out, v, mat, 0.1, tag, em); }
  }
  // a horizontal cylinder along x (a propane bottle lying, a hose) or along y
  function cylX(out, x0, x1, cy, cz, r, n, mat, tag) { for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI, b = (k + 1.5) / n * 2 * Math.PI;
      F(out, [[x0, cy + r * Math.cos(a), cz + r * Math.sin(a)], [x0, cy + r * Math.cos(b), cz + r * Math.sin(b)], [x1, cy + r * Math.cos(b), cz + r * Math.sin(b)], [x1, cy + r * Math.cos(a), cz + r * Math.sin(a)]], mat, Math.sin(a) > 0 ? 0.05 : -0.2, tag); } }
  function cylY(out, cx, y0, y1, cz, r, n, mat, tag) { for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI, b = (k + 1.5) / n * 2 * Math.PI;
      F(out, [[cx + r * Math.cos(a), y0, cz + r * Math.sin(a)], [cx + r * Math.cos(b), y0, cz + r * Math.sin(b)], [cx + r * Math.cos(b), y1, cz + r * Math.sin(b)], [cx + r * Math.cos(a), y1, cz + r * Math.sin(a)]], mat, Math.sin(a) > 0 ? 0.05 : -0.2, tag);
      } const v0 = [], v1 = []; for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI; v0.push([cx + r * Math.cos(a), y0, cz + r * Math.sin(a)]); v1.push([cx + r * Math.cos(a), y1, cz + r * Math.sin(a)]); }
    F(out, v0.reverse(), mat, -0.1, tag); F(out, v1, mat, 0.15, tag); }
  // a flat panel on the plane y = yp (facing +y when s = 1), x0..x1, z0..z1: decals, windows, doors, stripes
  function panelY(out, yp, s, x0, x1, z0, z1, mat, b, tag, tex, em) { const v = s > 0 ? [[x1, yp, z0], [x0, yp, z0], [x0, yp, z1], [x1, yp, z1]] : [[x0, yp, z0], [x1, yp, z0], [x1, yp, z1], [x0, yp, z1]];
    if (tex) FT(out, v, mat, b, tag, tex, em); else F(out, v, mat, b, tag, em); }
  function panelX(out, xp, s, y0, y1, z0, z1, mat, b, tag, tex, em) { const v = s > 0 ? [[xp, y0, z0], [xp, y1, z0], [xp, y1, z1], [xp, y0, z1]] : [[xp, y1, z0], [xp, y0, z0], [xp, y0, z1], [xp, y1, z1]];
    if (tex) FT(out, v, mat, b, tag, tex, em); else F(out, v, mat, b, tag, em); }

  // =====================================================================================================================
  //  THE PIECES
  // =====================================================================================================================
  const PIECES = {};

  /* THE WHARF OFFICE (structure.nmc_wharf_office) — the harbour authority's office is a travel trailer, towed in one spring
     and never towed out: a 1970s box trailer, white aluminium siding, a brown band under an orange one and a tan pinstripe
     round the body, jalousie windows, the door on the curb side under a porch light, wooden steps, stabilizer jacks down on
     blocks, twin propane bottles on the A-frame, an air conditioner on the roof. Not the St Peters camper (camperIsoRig.js,
     the riveted monocoque): square, flat-sided, flat-roofed, a gentle radius at the front. The hitch points to local -x; the
     door and the sign are on the +Y (show) face. The sign board is blank: the game letters "WHARF OFFICE". */
  PIECES.officeTrailer = {
    id: 'structure.nmc_wharf_office', name: 'Wharf office (box trailer)', defType: 'SetPieceDef', scene: 'keyscene.nmc_wharf',
    layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-4.05, -1.22], [4.12, -1.22], [4.12, 1.22], [-4.05, 1.22]], [[-5.75, -0.9], [-4.05, -0.9], [-4.05, 0.9], [-5.75, 0.9]], [[0.2, 1.22], [2.35, 1.22], [2.35, 2.5], [0.2, 2.5]]],
    defaults: { text: 'WHARF OFFICE', doorX: 1.05, stripe: 'orange', lights: true, role: 'wharfOffice', flag: true },
    build(o) {
      const out = [], X0 = -4.05, X1 = 4.12, hy = 1.22, zF = 0.66, zT = 3.02, R = 0.34, yS = hy + 0.004;   // body, the front radius
      // ---- the body: siding, the front radius, the flat roof with a slight crown
      const prof = [[X0 + R, zT], [X0 + R * 0.3, zT - R * 0.3], [X0, zT - R]], xs = [X0, X1];
      for (const s of [-1, 1]) { const y = s * hy;   // the long sides
        FT(out, s > 0 ? [[X1, y, zF], [X0, y, zF], [X0, y, zT - R], [X1, y, zT - R]] : [[X0, y, zF], [X1, y, zF], [X1, y, zT - R], [X0, y, zT - R]], 'siding', s > 0 ? 0.1 : -0.1, 'body.side', 'hribs');
        FT(out, s > 0 ? [[X1, y, zT - R], [X0 + R, y, zT - R], [X0 + R, y, zT], [X1, y, zT]] : [[X0 + R, y, zT - R], [X1, y, zT - R], [X1, y, zT], [X0 + R, y, zT]], 'siding', s > 0 ? 0.1 : -0.1, 'body.side', 'hribs');
        // the corner wedge under the radius
        F(out, s > 0 ? [[X0 + R, y, zT - R], [X0, y, zT - R], [X0 + R * 0.3, y, zT - R * 0.3], [X0 + R, y, zT]] : [[X0, y, zT - R], [X0 + R, y, zT - R], [X0 + R, y, zT], [X0 + R * 0.3, y, zT - R * 0.3]], 'siding', 0, 'body.side'); }
      FT(out, [[X0, -hy, zF], [X0, hy, zF], [X0, hy, zT - R], [X0, -hy, zT - R]], 'siding', -0.25, 'body.front', 'hribs');
      F(out, [[X0, -hy, zT - R], [X0, hy, zT - R], [X0 + R * 0.3, hy, zT - R * 0.3], [X0 + R * 0.3, -hy, zT - R * 0.3]], 'siding', -0.05, 'body.radius');
      F(out, [[X0 + R * 0.3, -hy, zT - R * 0.3], [X0 + R * 0.3, hy, zT - R * 0.3], [X0 + R, hy, zT], [X0 + R, -hy, zT]], 'siding', 0.05, 'body.radius');
      FT(out, [[X1, hy, zF], [X1, -hy, zF], [X1, -hy, zT], [X1, hy, zT]], 'siding', 0, 'body.rear', 'hribs');
      F(out, [[X0 + R, -hy, zT], [X1, -hy, zT], [X1, 0, zT + 0.05], [X0 + R, 0, zT + 0.05]], 'siding', 0.12, 'roof'); F(out, [[X0 + R, 0, zT + 0.05], [X1, 0, zT + 0.05], [X1, hy, zT], [X0 + R, hy, zT]], 'siding', 0.18, 'roof');
      F(out, [[X0, -hy, zF], [X1, -hy, zF], [X1, hy, zF], [X0, hy, zF]].reverse(), 'alum', -0.6, 'body.floor');
      // ---- the stripe: brown under orange, a tan pinstripe over, round the sides, the front and the rear
      // the bands run straight from the rear to 1.7 m short of the front, then sweep up 0.42 m to the front (the 1970s flourish)
      const XS = X0 + 1.7, RISE = 0.42, BANDS = [[1.20, 1.44, 'brown'], [1.44, 1.66, o.stripe === 'brown' ? 'brown' : 'orange'], [1.70, 1.75, 'tan']];
      for (const [z0, z1, m] of BANDS) { for (const s of [-1, 1]) { const y = s * (hy + 0.006); panelY(out, y, s, XS, X1, z0, z1, m, s > 0 ? 0.05 : -0.15, 'stripe');
          F(out, s > 0 ? [[XS, y, z0], [X0, y, z0 + RISE], [X0, y, z1 + RISE], [XS, y, z1]] : [[X0, y, z0 + RISE], [XS, y, z0], [XS, y, z1], [X0, y, z1 + RISE]], m, s > 0 ? 0.05 : -0.15, 'stripe.sweep'); }
        panelX(out, X0 - 0.006, -1, -hy, hy, z0 + RISE, z1 + RISE, m, -0.25, 'stripe'); panelX(out, X1 + 0.006, 1, -hy, hy, z0, z1, m, 0, 'stripe'); }
      // ---- windows: aluminium frames, jalousie glass; the big front window wraps the front
      const win = (s, x0, x1, z0, z1, id) => { const y = s * (hy + 0.012); panelY(out, y, s, x0 - 0.05, x1 + 0.05, z0 - 0.05, z1 + 0.05, 'alum', 0.1, 'window.frame');
        panelY(out, s * (hy + 0.018), s, x0, x1, z0, z1, 'glass', s > 0 ? 0.25 : 0, 'window.' + id, 'louvre', 'win'); };
      const HM = o.role === 'harbourmaster'; win(1, -3.25, -2.15, 1.86, 2.52, 'curbFwd'); if (!HM) win(1, 2.2, 3.35, 1.86, 2.52, 'curbAft'); win(-1, -2.9, -1.8, 1.86, 2.52, 'roadFwd'); win(-1, 0.6, 1.5, 1.86, 2.52, 'roadMid'); win(-1, 2.6, 3.5, 1.86, 2.52, 'roadAft');
      panelX(out, X0 - 0.012, -1, -0.95, 0.95, 1.86, 2.5, 'alum', -0.2, 'window.frame'); panelX(out, X0 - 0.018, -1, -0.9, 0.9, 1.9, 2.46, 'glass', -0.1, 'window.front', 'louvre', 'win');
      panelX(out, X1 + 0.012, 1, -0.5, 0.5, 1.9, 2.4, 'alum', 0, 'window.frame'); panelX(out, X1 + 0.018, 1, -0.45, 0.45, 1.94, 2.36, 'glass', 0.1, 'window.rear', 'louvre', 'win');
      // ---- the door on the curb side, its porch light, the sign board beside it
      const dx = o.doorX, dw = 0.34, yD = hy + 0.014;
      panelY(out, yD, 1, dx - dw - 0.04, dx + dw + 0.04, zF + 0.02, zF + 1.93, 'alum', 0.1, 'door.frame');
      panelY(out, yD + 0.006, 1, dx - dw, dx + dw, zF + 0.05, zF + 1.88, 'siding', 0.2, 'door.leaf');
      panelY(out, yD + 0.01, 1, dx - dw + 0.1, dx + dw - 0.1, zF + 1.3, zF + 1.72, 'glass', 0.3, 'door.window', null, 'win');
      box(out, dx + dw - 0.1, dx + dw - 0.04, yD + 0.01, yD + 0.05, zF + 0.95, zF + 1.0, 'chrome', 'door.handle');
      box(out, dx - 0.09, dx + 0.09, yD, yD + 0.1, zF + 2.02, zF + 2.14, 'alum', 'porch.light'); box(out, dx - 0.07, dx + 0.07, yD + 0.02, yD + 0.12, zF + 1.96, zF + 2.03, 'lampW', 'porch.lens', { em: 'lamp' });
      const sx0 = -1.95, sx1 = -0.55, sz0 = 1.92, sz1 = 2.44;
      box(out, sx0 - 0.03, sx1 + 0.03, yD, yD + 0.03, sz0 - 0.03, sz1 + 0.03, 'wood', 'sign.board', { noBack: false });
      F(out, [[sx1, yD + 0.034, sz0], [sx0, yD + 0.034, sz0], [sx0, yD + 0.034, sz1], [sx1, yD + 0.034, sz1]], 'board', 0.25, HM ? 'words.nmc_harbourmaster' : 'words.wharf_office');
      // ---- the roof: an air conditioner, a vent, the clearance lights (amber, forward)
      box(out, -0.45, 0.45, -0.42, 0.42, zT + 0.04, zT + 0.4, 'siding', 'roof.ac'); FT(out, [[0.45, 0.42, zT + 0.08], [-0.45, 0.42, zT + 0.08], [-0.45, 0.42, zT + 0.34], [0.45, 0.42, zT + 0.34]], 'alum', 0.25, 'roof.ac.grille', 'louvre');
      box(out, 2.6, 2.95, -0.2, 0.15, zT + 0.04, zT + 0.16, 'alum', 'roof.vent'); box(out, -2.3, -1.95, 0.25, 0.6, zT + 0.04, zT + 0.13, 'alum', 'roof.vent');
      if (o.lights) for (const y of [-0.8, -0.1, 0.1, 0.8]) box(out, X0 + R * 0.35 - 0.04, X0 + R * 0.35 + 0.02, y - 0.05, y + 0.05, zT - R * 0.25, zT - R * 0.25 + 0.06, 'amber', 'clearance', { em: 'lamp' });
      for (const s of [-1, 1]) box(out, X1, X1 + 0.03, s * 0.95 - 0.08, s * 0.95 + 0.08, 0.98, 1.12, 'lampR', 'tail');
      // ---- the chassis: frame rails, the axle and its wheels, the bumper, the jacks down on blocks
      for (const s of [-1, 1]) box(out, X0 + 0.1, X1 - 0.1, s * 0.78 - 0.05, s * 0.78 + 0.05, zF - 0.16, zF, 'iron', 'frame.rail');
      box(out, X1 - 0.05, X1 + 0.1, -1.1, 1.1, 0.55, 0.66, 'iron', 'bumper');
      for (const s of [-1, 1]) { const wy = s * 0.98; cylY(out, 0.65, wy - 0.1, wy + 0.1, 0.36, 0.36, 12, 'tyre', 'wheel'); cylY(out, 0.65, wy + s * 0.1 - 0.002, wy + s * 0.1 + 0.002, 0.36, 0.17, 8, 'chrome', 'hub');
        F(out, s > 0 ? [[1.25, s * hy, zF], [0.05, s * hy, zF], [0.05, s * hy, zF + 0.22], [1.25, s * hy, zF + 0.22]] : [[0.05, s * hy, zF], [1.25, s * hy, zF], [1.25, s * hy, zF + 0.22], [0.05, s * hy, zF + 0.22]], 'siding', 0, 'fender'); }
      // the skirt: brown-painted plywood under the body, the wheel left showing
      for (const s of [-1, 1]) { const y = s * (hy - 0.03); for (const [a, b] of [[X0 + 0.05, -0.2], [1.5, X1 - 0.05]]) panelY(out, y, s, a, b, 0.0, zF - 0.02, 'brown', s > 0 ? -0.1 : -0.3, 'skirt', 'boards'); }
      panelX(out, X1 - 0.08, 1, -hy + 0.05, hy - 0.05, 0.0, 0.54, 'brown', -0.2, 'skirt', 'boards');
      for (const [x, y] of [[X0 + 0.35, -0.95], [X0 + 0.35, 0.95], [X1 - 0.35, -0.95], [X1 - 0.35, 0.95]]) { box(out, x - 0.16, x + 0.16, y - 0.12, y + 0.12, 0, 0.12, 'wood', 'jack.block'); stick(out, [x, y, 0.12], [x, y, zF - 0.14], 0.07, 'iron', 'jack'); }
      // ---- the A-frame: tubes to the coupler, the jack post on its block, twin propane bottles, the battery box
      for (const s of [-1, 1]) stick(out, [X0 + 0.15, s * 0.78, zF - 0.1], [-5.6, s * 0.06, zF - 0.1], 0.09, 'iron', 'tongue');
      box(out, -5.72, -5.5, -0.07, 0.07, zF - 0.16, zF - 0.02, 'iron', 'coupler'); stick(out, [-5.25, 0, zF - 0.02], [-5.25, 0, 0.1], 0.08, 'iron', 'tongue.jack');
      box(out, -5.4, -5.1, -0.15, 0.15, 0, 0.1, 'wood', 'tongue.block'); stick(out, [-5.25, 0, zF + 0.05], [-5.25, 0.12, zF + 0.2], 0.03, 'iron', 'jack.crank');
      for (const y of [-0.34, 0.34]) { cyl(out, -4.62, y, zF - 0.06, zF + 0.72, 0.18, 0.18, 10, 'propane', 'propane'); cyl(out, -4.62, y, zF + 0.72, zF + 0.82, 0.18, 0.1, 10, 'propane', 'propane'); cyl(out, -4.62, y, zF + 0.82, zF + 0.9, 0.05, 0.05, 6, 'chrome', 'valve'); }
      box(out, -5.1, -4.86, -0.2, 0.2, zF - 0.06, zF + 0.22, 'tyre', 'battery');
      // ---- the steps: a pressure-treated landing, two treads, a pipe rail, blocks under the stringers
      const yA = hy + 0.02, yB = hy + 1.15, xl = dx - 0.55, xr = dx + 0.95;
      box(out, xl, xr, yA, yB, zF - 0.28, zF - 0.2, 'ptwood', 'steps.landing'); FT(out, [[xl, yA, zF - 0.2], [xr, yA, zF - 0.2], [xr, yB, zF - 0.2], [xl, yB, zF - 0.2]], 'ptwood', 0.1, 'steps.landing', 'treads');
      for (const [x, y] of [[xl + 0.08, yB - 0.08], [xr - 0.08, yB - 0.08]]) box(out, x - 0.05, x + 0.05, y - 0.05, y + 0.05, 0, zF - 0.28, 'ptwood', 'steps.post');
      for (let k = 0; k < 2; k++) { const y0 = yB + k * 0.28, z = zF - 0.2 - (k + 1) * 0.2; box(out, xl + 0.1, xr - 0.1, y0, y0 + 0.28, z - 0.04, z, 'ptwood', 'steps.tread'); }
      for (const x of [xl + 0.12, xr - 0.12]) F(out, [[x, yB, zF - 0.28], [x, yB + 0.6, 0.0], [x, yB + 0.6, 0.06], [x, yB, zF - 0.2]], 'ptwood', -0.2, 'steps.stringer');
      stick(out, [xr - 0.05, yA + 0.1, zF - 0.2], [xr - 0.05, yA + 0.1, zF + 0.72], 0.045, 'galv', 'steps.rail'); stick(out, [xr - 0.05, yB + 0.5, 0.05], [xr - 0.05, yB + 0.5, 0.95], 0.045, 'galv', 'steps.rail');
      stick(out, [xr - 0.05, yA + 0.1, zF + 0.72], [xr - 0.05, yB + 0.5, 0.95], 0.045, 'galv', 'steps.rail');
      // ---- the harbourmaster's: the licence counter aft of the door (a sliding window, a fold-down shelf on brackets, a hood over it),
      //      the licence board under it, the drop box by the steps, the flag on the front corner. Where you buy the cod licence.
      const HMW = [];
      if (HM) { const wx0 = 2.05, wx1 = 3.55, wz0 = 1.52, wz1 = 2.46, yW = hy + 0.012;
        panelY(out, yW, 1, wx0 - 0.07, wx1 + 0.07, wz0 - 0.07, wz1 + 0.07, 'alum', 0.1, 'counter.frame');
        panelY(out, yW + 0.006, 1, wx0, (wx0 + wx1) / 2 + 0.05, wz0, wz1, 'glass', 0.25, 'counter.glass', null, 'win');
        panelY(out, yW + 0.004, 1, (wx0 + wx1) / 2 + 0.05, wx1, wz0, wz1, 'glass', 0.05, 'counter.open', null, 'win');
        panelY(out, yW + 0.009, 1, (wx0 + wx1) / 2 + 0.02, (wx0 + wx1) / 2 + 0.08, wz0, wz1, 'alum', 0.2, 'counter.frame');
        box(out, wx0 - 0.08, wx1 + 0.08, yW, yW + 0.4, wz0 - 0.1, wz0 - 0.05, 'ptwood', 'counter.shelf');
        for (const x of [wx0 + 0.15, wx1 - 0.15]) stick(out, [x, yW + 0.02, wz0 - 0.42], [x, yW + 0.36, wz0 - 0.1], 0.03, 'galv', 'counter.bracket');
        F(out, [[wx0 - 0.15, yW, wz1 + 0.22], [wx1 + 0.15, yW, wz1 + 0.22], [wx1 + 0.15, yW + 0.55, wz1 + 0.04], [wx0 - 0.15, yW + 0.55, wz1 + 0.04]], 'alum', 0.15, 'counter.hood');
        for (const x of [wx0 - 0.15, wx1 + 0.15]) F(out, x > 2.8 ? [[x, yW, wz1 + 0.04], [x, yW + 0.55, wz1 + 0.04], [x, yW, wz1 + 0.22]] : [[x, yW + 0.55, wz1 + 0.04], [x, yW, wz1 + 0.04], [x, yW, wz1 + 0.22]], 'alum', 0, 'counter.hood');
        const bx0 = 2.12, bx1 = 3.48, bz0 = 0.86, bz1 = 1.16; box(out, bx0 - 0.03, bx1 + 0.03, yW, yW + 0.03, bz0 - 0.03, bz1 + 0.03, 'wood', 'licence.board');
        F(out, [[bx1, yW + 0.034, bz0], [bx0, yW + 0.034, bz0], [bx0, yW + 0.034, bz1], [bx1, yW + 0.034, bz1]], 'board', 0.25, 'words.nmc_licences');
        HMW.push({ id: 'words.nmc_licences', text: 'LICENCES \u00b7 BERTHS \u00b7 WHARFAGE', corners: [[bx0, yW + 0.034, bz0], [bx1, yW + 0.034, bz0], [bx1, yW + 0.034, bz1], [bx0, yW + 0.034, bz1]], note: 'under the counter: what the harbourmaster sells' });
        stick(out, [0.05, yB + 0.95, 0], [0.05, yB + 0.95, 0.95], 0.06, 'ptwood', 'dropbox.post'); box(out, -0.14, 0.24, yB + 0.8, yB + 1.1, 0.95, 1.36, 'alum', 'dropbox'); box(out, -0.1, 0.2, yB + 1.1, yB + 1.12, 1.2, 1.24, 'iron', 'dropbox.slot');
        if (o.flag) { const fx = X0 + 0.22, A = [fx, hy + 0.05, 2.3], B = [fx, hy + 0.95, 3.3], Lf = Math.hypot(B[1] - A[1], B[2] - A[2]), ud = [0, (B[1] - A[1]) / Lf, (B[2] - A[2]) / Lf];
          stick(out, A, B, 0.035, 'galv', 'flag.staff'); const H0 = B, H1 = [fx, B[1] - 0.46 * ud[1], B[2] - 0.46 * ud[2]], fl = 0.92;
          const q = (a, b, m, tg) => F(out, [[H1[0] - a, H1[1], H1[2]], [H1[0] - b, H1[1], H1[2]], [H0[0] - b, H0[1], H0[2]], [H0[0] - a, H0[1], H0[2]]], m, 0.2, tg);
          q(0, fl * 0.25, 'flagR', 'flag'); q(fl * 0.25, fl * 0.75, 'trim', 'flag'); q(fl * 0.75, fl, 'flagR', 'flag');
          const mx = fl * 0.5, mk = 0.085, lz0 = H1[2] + (H0[2] - H1[2]) * 0.32, lz1 = H1[2] + (H0[2] - H1[2]) * 0.68, ly0 = H1[1] + (H0[1] - H1[1]) * 0.32, ly1 = H1[1] + (H0[1] - H1[1]) * 0.68;
          F(out, [[fx - mx + mk, ly0 + 0.004, lz0], [fx - mx - mk, ly0 + 0.004, lz0], [fx - mx - mk, ly1 + 0.004, lz1], [fx - mx + mk, ly1 + 0.004, lz1]], 'flagR', 0.25, 'flag.leaf'); } }
      return { faces: out, mats: mats(['siding', 'orange', 'brown', 'tan', 'alum', 'glass', 'wood', 'ptwood', 'iron', 'tyre', 'chrome', 'propane', 'board', 'galv', 'amber', 'lampR', 'lampW', 'trim', 'flagR']),
        anchors: { role: o.role, counter: HM ? [2.8, yD + 0.8, 0] : null, vendor: HM ? { seller: 'seller.nmc_harbourmaster', at: [2.8, 0.3, 1.5], sells: 'the cod licence (LicenseVendor on this counter; the game\u2019s LicenseDef)' } : null,
          flag: HM && o.flag ? [X0 + 0.22, hy + 0.95, 3.3] : null, dropBox: HM ? [0.05, yB + 0.95, 1.2] : null,
          words: HMW.concat([{ id: HM ? 'words.nmc_harbourmaster' : 'words.wharf_office', text: o.text, corners: [[sx0, yD + 0.034, sz0], [sx1, yD + 0.034, sz0], [sx1, yD + 0.034, sz1], [sx0, yD + 0.034, sz1]], note: 'the office\u2019s sign, lettered by the game' }]),
          door: [dx, yB + 0.62, 0], interact: [dx + 0.2, yB + 0.9, 0], porchLight: [dx, yD + 0.1, zF + 1.99], hitch: [-5.62, 0, zF - 0.1] } };
    },
    notes: 'Stand it with its curb side (the door) to the yard. The hitch is the game\u2019s if it is ever towed: anchors().hitch. The porch light and the windows glow with o.lit while the office is open after dark.',
  };

  /* THE WHARF CRANE (structure.nmc_wharf_crane) — the unloading berth's hydraulic knuckle-boom crane on a pedestal at the quay
     edge: a concrete plinth ringed by yellow posts, the slewing column, a main boom and a jib, their rams, the control stand on
     the landward side, the wire and hook. It replaces the winch (the owner, 09-28) and it is PLAYABLE: it slews all the way
     round, reaches 2.2 to 5.6 m, and hoists from over the quay to 7 m under its deck. The rig draws any pose the game asks for:
       o.state 'stowed' | 'working'; o.slew deg, clockwise from local +Y seen from above; o.reach m, the hook's radius from the
       column; o.hookZ m, the hook's eye over the quay deck; o.load 'none' | 'totes' | 'traps' | 'bait', hung from the eye.
     CRANE is the envelope the game drives it within, LOADS what it lifts, craneState(o) the pose (the hook, the tip, the boom
     angles), anchors() the same for the pose drawn. The fixed base and the control stand never turn; the upper works do.
     Local +Y is out over the water; the plinth sits 2.2 m inside the face. */
  const CRANE = { pin: [0.05, 2.85], Lm: 2.975, Lj: 4.14, column: [0, 0, 1.52], operator: [0, -1.18, 0.26],
    reach: { min: 2.2, max: 5.6, rateMS: 0.45 }, tipZ: { near: 1.9, far: 4.95, farFrom: 5.0 },
    slew: { continuous: true, rateDegS: 20, stowedDeg: -90 }, hoist: { max: 3.6, min: -7.0, upMS: 0.4, downMS: 0.55, belowTip: 0.95, slowNearM: 0.3, slowMS: 0.15 },
    stowed: { luffDeg: 72, jibDeg: -94, hookZ: 1.35 }, swlKg: 1000, latchM: 0.35, landM: 0.05, swing: { damping: 0.6, maxDeg: 12, fromSlewDegS2: 0.35 } };
  const LOADS = {
    none: null,
    totes: { label: 'two fish totes, stacked: the landing', size: [1.12, 0.72, 1.04], kg: 190, lift: 0.4, seat: 'deck or pad' },
    traps: { label: 'a sling of four traps: setting day', size: [1.24, 1.26, 0.9], kg: 150, lift: 0.4, seat: 'pad or deck' },
    bait: { label: 'a bait barrel', size: [0.6, 0.6, 0.9], kg: 120, lift: 0.3, seat: 'pad or deck' },
  };
  const CRANE_STATES = [
    { id: 'stowed', means: 'jib folded down beside the column, the hook in its cradle, slewed along the quay (-90). Nobody at the stand.' },
    { id: 'idle', means: 'working pose, the hook up at the top of its travel, nothing on it. The player is at the stand.' },
    { id: 'reaching', means: 'the player is slewing, reaching or hoisting with nothing on the hook.' },
    { id: 'hooked', means: 'the eye within latchM of a load\u2019s lift point and latched (E). The load still sits.' },
    { id: 'lifting', means: 'a latched load clear of every surface; it swings, damped, never more than swing.maxDeg.' },
    { id: 'landing', means: 'the load\u2019s base within hoist.slowNearM of a surface (the pad, a deck, a truck bed): lowering slows to hoist.slowMS.' },
    { id: 'set', means: 'the load\u2019s base within landM of a surface: it sits, and E unlatches.' },
  ];
  function craneTipZ(R) { const t = CRANE.tipZ; return R >= t.farFrom ? t.far : t.near + (t.far - t.near) * (R - CRANE.reach.min) / (t.farFrom - CRANE.reach.min); }
  function craneIK(R, zt) { const A = CRANE.pin, Lm = CRANE.Lm, Lj = CRANE.Lj, dr = R - A[0], dz = zt - A[1], d = clamp(Math.hypot(dr, dz), Math.abs(Lm - Lj) + 0.02, Lm + Lj - 0.02);
    const ph = Math.atan2(dz, dr), al = Math.acos(clamp((Lm * Lm + d * d - Lj * Lj) / (2 * Lm * d), -1, 1)), lu = ph + al;
    const K = [A[0] + Lm * Math.cos(lu), A[1] + Lm * Math.sin(lu)], T = [A[0] + d * Math.cos(ph), A[1] + d * Math.sin(ph)];
    return { K, T, luffDeg: lu / D2R, jibDeg: Math.atan2(T[1] - K[1], T[0] - K[0]) / D2R }; }
  function craneState(o) {
    o = o || {}; const stow = o.state === 'stowed', A = CRANE.pin; let K, T, luffDeg, jibDeg;
    if (stow) { const S = CRANE.stowed; luffDeg = S.luffDeg; jibDeg = S.jibDeg; K = [A[0] + CRANE.Lm * Math.cos(luffDeg * D2R), A[1] + CRANE.Lm * Math.sin(luffDeg * D2R)];
      T = [K[0] + CRANE.Lj * Math.cos(jibDeg * D2R), K[1] + CRANE.Lj * Math.sin(jibDeg * D2R)]; }
    else { const R = clamp(o.reach != null ? +o.reach : 5.35, CRANE.reach.min, CRANE.reach.max), ik = craneIK(R, craneTipZ(R)); K = ik.K; T = ik.T; luffDeg = ik.luffDeg; jibDeg = ik.jibDeg; }
    const slew = o.slew != null ? +o.slew : stow ? CRANE.slew.stowedDeg : 0, s = Math.sin(slew * D2R), c = Math.cos(slew * D2R);
    const hookZ = stow ? CRANE.stowed.hookZ : clamp(o.hookZ != null ? +o.hookZ : 0.6, CRANE.hoist.min, Math.min(CRANE.hoist.max, T[1] - CRANE.hoist.belowTip));
    const load = stow ? 'none' : (LOADS[o.load] ? o.load : 'none'), r = (v) => +v.toFixed(3), Ld = LOADS[load];
    return { state: stow ? 'stowed' : 'working', slew, s, c, K, T, luffDeg: r(luffDeg), jibDeg: r(jibDeg), reach: r(T[0]), hookZ: r(hookZ), load,
      hook: [r(T[0] * s), r(T[0] * c), r(hookZ)], tip: [r(T[0] * s), r(T[0] * c), r(T[1])], loadBase: Ld ? [r(T[0] * s), r(T[0] * c), r(hookZ - Ld.lift - Ld.size[2])] : null }; }
  PIECES.wharfCrane = {
    id: 'structure.nmc_wharf_crane', name: 'Wharf crane (knuckle boom)', defType: 'SetPieceDef', scene: 'keyscene.nmc_wharf',
    layer: 'raised', walk: 'solid', mount: 'quay',
    footprints: [[[-0.95, -0.95], [0.95, -0.95], [0.95, 0.95], [-0.95, 0.95]], [[-0.55, -1.38], [0.55, -1.38], [0.55, -0.95], [-0.55, -0.95]]],
    defaults: { state: 'working', slew: 0, reach: 5.35, hookZ: 0.6, load: 'totes' },
    build(o) {
      const out = [], up = [], cs = craneState(o), A3 = [0, CRANE.pin[0], CRANE.pin[1]], K3 = [0, cs.K[0], cs.K[1]], T3 = [0, cs.T[0], cs.T[1]];
      const L = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
      // the fixed base: the plinth, its posts, the pedestal, the slewing ring; the control stand on the landward side (it never turns)
      box(out, -0.8, 0.8, -0.8, 0.8, 0, 0.22, 'conc', 'plinth');
      for (const [x, y] of [[-0.95, -0.95], [0.95, -0.95], [-0.95, 0.95], [0.95, 0.95]]) { cyl(out, x, y, 0, 1.0, 0.07, 0.07, 8, 'yellow', 'post'); box(out, x - 0.075, x + 0.075, y - 0.075, y + 0.075, 0.78, 0.86, 'iron', 'post.band'); }
      cyl(out, 0, 0, 0.22, 1.3, 0.4, 0.34, 14, 'yellow', 'pedestal'); cyl(out, 0, 0, 1.3, 1.52, 0.52, 0.52, 16, 'iron', 'slewRing');
      box(out, -0.55, 0.55, -1.35, -1.0, 0.22, 0.26, 'galv', 'controls.grate'); box(out, -0.42, 0.42, -1.02, -0.86, 0.26, 1.05, 'iron', 'controls.stand');
      box(out, -0.4, 0.4, -1.0, -0.78, 1.05, 1.22, 'yellow', 'controls.valves'); for (let i = 0; i < 4; i++) stick(out, [-0.27 + i * 0.18, -0.95, 1.22], [-0.27 + i * 0.18, -1.08, 1.5], 0.025, 'iron', 'controls.lever');
      // the upper works, built along local +Y and turned by the slew
      box(up, -0.3, 0.3, -0.34, 0.3, 1.52, 2.95, 'yellow', 'column'); box(up, -0.36, 0.36, -0.5, -0.3, 1.6, 2.2, 'iron', 'column.motor');
      stick(up, A3, K3, 0.38, 'yellow', 'boom.main'); stick(up, K3, T3, 0.3, 'yellow', 'boom.jib'); box(up, -0.24, 0.24, K3[1] - 0.25, K3[1] + 0.25, K3[2] - 0.25, K3[2] + 0.25, 'iron', 'knuckle');
      const rb = [0, 0.22, 1.9], rt = L(A3, K3, 0.62); stick(up, rb, rt, 0.16, 'chrome', 'ram.main'); stick(up, rb, L(rb, rt, 0.55), 0.21, 'iron', 'ram.main.barrel');
      const jb = L(A3, K3, 0.8), jt = L(K3, T3, 0.3), jb2 = [0, jb[1], jb[2] + 0.3], jt2 = [0, jt[1], jt[2] + 0.26];
      stick(up, jb2, jt2, 0.12, 'chrome', 'ram.jib'); stick(up, jb2, L(jb2, jt2, 0.5), 0.16, 'iron', 'ram.jib.barrel');
      for (const sx of [-1, 1]) stick(up, [sx * 0.17, 0.3, 2.6], [sx * 0.17, K3[1] - 0.15, K3[2] - 0.28], 0.035, 'hose', 'hose');
      box(up, -0.12, 0.12, T3[1] - 0.12, T3[1] + 0.2, T3[2] - 0.28, T3[2] + 0.1, 'iron', 'jib.head');
      // the wire, the hook block, the hook, the sling and the load, all under the eye (hookZ)
      const hz = cs.hookZ, hy = T3[1];
      if (cs.state === 'stowed') box(up, -0.16, 0.16, hy - 0.16, hy + 0.16, hz - 0.08, hz + 0.02, 'iron', 'hook.cradle');
      stick(up, [0, hy, T3[2] - 0.3], [0, hy, hz + 0.22], 0.025, 'iron', 'wire'); box(up, -0.09, 0.09, hy - 0.09, hy + 0.09, hz + 0.02, hz + 0.22, 'yellow', 'hook.block');
      stick(up, [0, hy, hz + 0.02], [0, hy, hz - 0.06], 0.03, 'iron', 'hook');
      const Ld = LOADS[cs.load];
      if (Ld) { const lx = Ld.size[0], ly = Ld.size[1], lh = Ld.size[2], top = hz - Ld.lift, bot = top - lh;
        for (const [sx, sy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) stick(up, [0, hy, hz - 0.06], [sx * lx * 0.42, hy + sy * ly * 0.42, top + 0.02], 0.022, 'orange', 'sling');
        if (cs.load === 'totes') { box(up, -lx / 2, lx / 2, hy - ly / 2, hy + ly / 2, bot + lh / 2 + 0.01, top, 'tote', 'load.tote'); box(up, -lx / 2, lx / 2, hy - ly / 2, hy + ly / 2, bot, bot + lh / 2 - 0.01, 'tote', 'load.tote'); }
        else if (cs.load === 'traps') { for (let i = 0; i < 2; i++) for (let k = 0; k < 2; k++) boxT(up, -lx / 2 + i * lx / 2 + 0.02, -lx / 2 + (i + 1) * lx / 2 - 0.02, hy - ly / 2 + 0.02, hy + ly / 2 - 0.02,
            bot + k * lh / 2 + 0.01, bot + (k + 1) * lh / 2 - 0.01, 'trapwire', 'load.trap', 'wire'); }
        else if (cs.load === 'bait') { cyl(up, 0, hy, bot, top, 0.3, 0.3, 12, 'tote', 'load.barrel'); } }
      const s = cs.s, c = cs.c; for (const f of up) { f.v = f.v.map(p => [p[0] * c + p[1] * s, -p[0] * s + p[1] * c, p[2]]); out.push(f); }
      return { faces: out, mats: mats(['conc', 'yellow', 'iron', 'chrome', 'hose', 'tote', 'orange', 'galv', 'trapwire']),
        anchors: { hook: cs.hook, tip: cs.tip, loadBase: cs.loadBase, operator: CRANE.operator.slice(), column: CRANE.column.slice(), state: cs.state, slewDeg: cs.slew, reachM: cs.reach, hookZ: cs.hookZ,
          luffDeg: cs.luffDeg, jibDeg: cs.jibDeg, load: cs.load, note: 'hook = [reach sin(slew), reach cos(slew), hookZ] in the piece frame; the stand never turns' } };
    },
    extra: (s) => ({ crane: { envelope: CRANE, loads: LOADS, states: CRANE_STATES, pose: (({ s: _s, c: _c, K, T, ...p }) => p)(craneState(s)),
      controls: { take: 'E at the operator point (anchors.operator), from the landward side', slew: 'A / D, left stick X', reach: 'W / S, left stick Y', hoist: 'R up / F down; RT up / LT down',
        latch: 'E, when the eye is within envelope.latchM of a load\u2019s lift point', release: 'E, when the load is set', leave: 'Esc / B, or walk off the grating' },
      camera: { size: [20, 11.25], centre: 'midway between the column and the hook, the same angle; eases in over 0.6 s', marker: 'a ring on the surface under the hook (the drop line), because a hook\u2019s height is hard to read from above' },
      proposal: 'NOTES.md \u00a74: the crane\u2019s gameplay, for the owner to rule' } }),
    notes: 'Mount on the quay with local +Y out over the water, the plinth 2.2 m inside the face, so the hook reaches a boat alongside and the landing pad behind. Playable: see gameplay().crane and NOTES \u00a74.',
  };

  /* THE HARBOUR LIGHTS (structure.nmc_harbour_light) — a galvanised lattice tower on a cast block on each breakwater head,
     a square daymark, a lantern: red on the east head, green on the west (red right returning, entering from the south). */
  PIECES.harbourLight = {
    id: 'structure.nmc_harbour_light', name: 'Harbour light', defType: 'SetPieceDef', scene: 'keyscene.nmc_wharf',
    layer: 'raised', walk: 'solid', mount: 'breakwater',
    footprints: [[[-0.9, -0.9], [0.9, -0.9], [0.9, 0.9], [-0.9, 0.9]]],
    defaults: { colour: 'red', height: 4.6 },
    build(o) {
      const out = [], H = +o.height || 4.6, red = o.colour !== 'green', day = red ? 'red' : 'green', lamp = red ? 'lampR' : 'lampG';
      box(out, -0.9, 0.9, -0.9, 0.9, 0, 0.7, 'conc', 'block');
      const b = 0.62, t = 0.2, legs = [[-1, -1], [1, -1], [1, 1], [-1, 1]];
      for (const [sx, sy] of legs) stick(out, [sx * b, sy * b, 0.7], [sx * t, sy * t, 0.7 + H], 0.07, 'galv', 'leg');
      for (let lv = 0; lv < 3; lv++) { const z0 = 0.7 + H * lv / 3, z1 = 0.7 + H * (lv + 1) / 3, w0 = b + (t - b) * lv / 3, w1 = b + (t - b) * (lv + 1) / 3;
        for (let k = 0; k < 4; k++) { const A = legs[k], B = legs[(k + 1) % 4]; stick(out, [A[0] * w0, A[1] * w0, z0], [B[0] * w1, B[1] * w1, z1], 0.035, 'galv', 'brace'); stick(out, [A[0] * w1, A[1] * w1, z1], [B[0] * w1, B[1] * w1, z1], 0.035, 'galv', 'brace'); } }
      const zt = 0.7 + H;
      box(out, -0.42, 0.42, -0.42, 0.42, zt, zt + 0.06, 'galv', 'platform'); for (const [sx, sy] of legs) stick(out, [sx * 0.4, sy * 0.4, zt], [sx * 0.4, sy * 0.4, zt + 0.55], 0.03, 'galv', 'rail');
      for (let k = 0; k < 4; k++) { const A = legs[k], B = legs[(k + 1) % 4]; stick(out, [A[0] * 0.4, A[1] * 0.4, zt + 0.55], [B[0] * 0.4, B[1] * 0.4, zt + 0.55], 0.03, 'galv', 'rail'); }
      cyl(out, 0, 0, zt + 0.06, zt + 0.18, 0.16, 0.16, 10, 'iron', 'lantern.base'); cyl(out, 0, 0, zt + 0.18, zt + 0.5, 0.13, 0.13, 10, lamp, 'lantern.lens', true, 'lamp'); cyl(out, 0, 0, zt + 0.5, zt + 0.58, 0.16, 0.05, 10, 'iron', 'lantern.cap');
      // the daymark: a square board on the tower's +Y face (toward the approach), a white border
      const zd = 0.7 + H * 0.55, yd = t + (b - t) * 0.45 + 0.06;
      box(out, -0.52, 0.52, yd, yd + 0.04, zd - 0.52, zd + 0.52, 'trim', 'daymark.border'); panelY(out, yd + 0.042, 1, -0.44, 0.44, zd - 0.44, zd + 0.44, day, 0.2, 'daymark');
      for (let z = 0.95; z < zt - 0.1; z += 0.3) stick(out, [-0.16, -b * 0.8 + (z / zt) * 0.3, z], [0.16, -b * 0.8 + (z / zt) * 0.3, z], 0.025, 'galv', 'ladder');
      return { faces: out, mats: mats(['conc', 'galv', 'iron', 'trim', day, lamp]), anchors: { lamp: [0, 0, zt + 0.34], colour: red ? 'red' : 'green', note: 'flashes are the game\u2019s: the lens emits with o.lit' } };
    },
    notes: 'Stand on the head block of a breakwater arm, the daymark to the approach. Red on the east (starboard entering), green on the west.',
  };

  /* the buildings' helpers: a gable-roofed box (walls, gables, roof planes with their overhang, fascia), a window, a picnic table */
  function gable(out, g) {
    const x0 = g.x0, x1 = g.x1, y0 = g.y0, y1 = g.y1, zw = g.zw, zr = g.zw + g.rise, yc = (y0 + y1) / 2, hyb = (y1 - y0) / 2, ov = g.ov, sl = g.rise / hyb, tag = g.tag;
    FTz(out, [[x0, y0, 0], [x1, y0, 0], [x1, y0, zw], [x0, y0, zw]], g.wall, -0.12, tag + '.wall', g.tex); FTz(out, [[x1, y1, 0], [x0, y1, 0], [x0, y1, zw], [x1, y1, zw]], g.wall, 0.1, tag + '.wall', g.tex);
    FTz(out, [[x1, y0, 0], [x1, y1, 0], [x1, y1, zw], [x1, y0, zw]], g.wall, 0.02, tag + '.wall', g.tex); FTz(out, [[x0, y1, 0], [x0, y0, 0], [x0, y0, zw], [x0, y1, zw]], g.wall, -0.2, tag + '.wall', g.tex);
    FTz(out, [[x1, y0, zw], [x1, y1, zw], [x1, yc, zr]], g.wall, 0.04, tag + '.gable', g.tex); FTz(out, [[x0, y1, zw], [x0, y0, zw], [x0, yc, zr]], g.wall, -0.18, tag + '.gable', g.tex);
    const ez = zw - ov * sl, xa = x0 - ov * 0.7, xb = x1 + ov * 0.7;
    FT(out, [[xa, y0 - ov, ez], [xb, y0 - ov, ez], [xb, yc, zr], [xa, yc, zr]], g.roof, -0.05, tag + '.roof', g.roofTex || 'shingles');
    FT(out, [[xb, y1 + ov, ez], [xa, y1 + ov, ez], [xa, yc, zr], [xb, yc, zr]], g.roof, 0.12, tag + '.roof', g.roofTex || 'shingles');
    for (const [a, b] of [[y0 - ov, yc], [yc, y1 + ov]]) for (const x of [xa, xb]) { const za = a < yc ? ez : zr, zb = a < yc ? zr : ez;
      F(out, x === xb ? [[x, a, za - 0.18], [x, b, zb - 0.18], [x, b, zb], [x, a, za]] : [[x, b, zb - 0.18], [x, a, za - 0.18], [x, a, za], [x, b, zb]], g.trim, x === xb ? 0.05 : -0.15, tag + '.barge'); }
    for (const s of [-1, 1]) { const y = s > 0 ? y1 + ov : y0 - ov; panelY(out, y, s, xa, xb, ez - 0.18, ez, g.trim, s > 0 ? 0.1 : -0.1, tag + '.fascia'); }
    for (const [x, y] of [[x0, y0], [x1, y0], [x0, y1], [x1, y1]]) box(out, x - 0.07, x + 0.07, y - 0.07, y + 0.07, 0, zw, g.trim, tag + '.corner');
    return { ez, zr, xa, xb };
  }
  function winY(out, y, s, x0, x1, z0, z1, id, trim, mull) { panelY(out, y + s * 0.01, s, x0 - 0.08, x1 + 0.08, z0 - 0.1, z1 + 0.08, trim || 'trim', 0.1, 'window.trim');
    panelY(out, y + s * 0.02, s, x0, x1, z0, z1, 'glass', s > 0 ? 0.25 : 0.05, 'window.' + id, null, 'win');
    if (mull) for (let k = 1; k < mull; k++) { const x = x0 + (x1 - x0) * k / mull; panelY(out, y + s * 0.025, s, x - 0.03, x + 0.03, z0, z1, trim || 'trim', 0.1, 'window.mullion'); }
    box(out, x0 - 0.12, x1 + 0.12, s > 0 ? y : y - 0.1, s > 0 ? y + 0.1 : y, z0 - 0.14, z0 - 0.08, trim || 'trim', 'window.sill'); }
  function winX(out, x, s, y0, y1, z0, z1, id, trim) { panelX(out, x + s * 0.01, s, y0 - 0.08, y1 + 0.08, z0 - 0.1, z1 + 0.08, trim || 'trim', 0, 'window.trim');
    panelX(out, x + s * 0.02, s, y0, y1, z0, z1, 'glass', 0.05, 'window.' + id, null, 'win'); }
  function picnic(out, x, y, rot, tag) { const r = rot ? 1 : 0, P = (a, b) => r ? [y + b, a] : [x + a, y + b];   // 1.8 m long, along x (or along y when rot)
    const bx = (ax0, ax1, by0, by1, z0, z1, m, tg) => { const p = r ? [x + by0, x + by1, y + ax0, y + ax1] : [x + ax0, x + ax1, y + by0, y + by1]; box(out, p[0], p[1], p[2], p[3], z0, z1, m, tg); };
    bx(-0.9, 0.9, -0.36, 0.36, 0.72, 0.77, 'wood', tag + '.top'); for (const s of [-1, 1]) bx(-0.9, 0.9, s * 0.62 - 0.13, s * 0.62 + 0.13, 0.42, 0.46, 'wood', tag + '.bench');
    for (const a of [-0.62, 0.62]) bx(a - 0.04, a + 0.04, -0.72, 0.72, 0.0, 0.42, 'ptwood', tag + '.frame'), bx(a - 0.04, a + 0.04, -0.2, 0.2, 0.42, 0.72, 'ptwood', tag + '.frame'); return P; }

  /* THE RESTAURANT (structure.nmc_restaurant) — the wharf's seafood restaurant on the north row, facing the creek (the owner,
     09-28): a storey and a half of buttermilk clapboard under an asphalt roof, 16 x 9 m. Its show face (local +Y) is the creek
     side: four big windows and the deck, which wraps round the east end so the camera sees its tables. The yard side (local -Y,
     the side the camera sees when it faces the creek) has the door under a porch hood, the takeout window with its counter and
     awning, the menu board, the name board over the eave, the kitchen hood and the stovepipe. Words are data: the name is
     the owner's to give (words.nmc_restaurant), the menu is the game's (words.nmc_restaurant_menu). */
  PIECES.restaurant = {
    id: 'structure.nmc_restaurant', name: 'The restaurant', defType: 'SetPieceDef', scene: 'keyscene.nmc_wharf',
    layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-8, -4.5], [8, -4.5], [8, 4.5], [-8, 4.5]], [[-8, 4.5], [11.6, 4.5], [11.6, 14.0], [-8, 14.0]], [[8, -3.2], [11.6, -3.2], [11.6, 4.5], [8, 4.5]]],
    defaults: { text: 'RESTAURANT', paint: 'butter', lights: true, deck: true, patio: 5.4 },
    build(o) {
      const out = [], X0 = -8, X1 = 8, Y0 = -4.5, Y1 = 4.5, ZW = 3.4, wall = o.paint === 'red' ? 'red' : 'butter';
      box(out, X0 - 0.05, X1 + 0.05, Y0 - 0.05, Y1 + 0.05, -0.3, 0.02, 'conc', 'foundation', { noTop: true });
      const G = gable(out, { x0: X0, x1: X1, y0: Y0, y1: Y1, zw: ZW, rise: 3.2, ov: 0.45, wall, tex: 'clap', roof: 'roofAsph', trim: 'trim', tag: 'body' });
      // ---- the yard side (-Y): the door and its porch hood, windows, the takeout, the menu, the bench, the name board over the eave
      const yS = Y0; panelY(out, yS - 0.01, -1, -1.05, 0.15, 0, 2.2, 'trim', -0.1, 'door.frame'); panelY(out, yS - 0.02, -1, -0.95, 0.05, 0.02, 2.12, 'green', -0.05, 'door.leaf');
      panelY(out, yS - 0.03, -1, -0.8, -0.1, 1.2, 1.95, 'glass', 0.05, 'door.window', null, 'win');
      const pz = 2.55; F(out, [[1.0, yS, pz + 0.55], [-1.9, yS, pz + 0.55], [-1.9, yS - 1.35, pz], [1.0, yS - 1.35, pz]], 'roofAsph', -0.02, 'porch.roof');
      for (const x of [-1.9, 1.0]) F(out, x > 0 ? [[x, yS - 1.35, pz], [x, yS, pz], [x, yS, pz + 0.55]] : [[x, yS, pz], [x, yS - 1.35, pz], [x, yS, pz + 0.55]], 'trim', 0, 'porch.side');
      for (const x of [-1.8, 0.9]) box(out, x - 0.06, x + 0.06, yS - 1.3, yS - 1.18, 0, pz, 'trim', 'porch.post');
      box(out, -0.52, -0.38, yS - 0.12, yS, 2.28, 2.42, 'iron', 'porch.lamp'); box(out, -0.5, -0.4, yS - 0.16, yS - 0.1, 2.2, 2.3, 'lampW', 'porch.lens', { em: 'lamp' });
      winY(out, yS, -1, -6.4, -5.0, 1.0, 2.3, 'yardW1', 'trim', 2); winY(out, yS, -1, -4.1, -2.7, 1.0, 2.3, 'yardW2', 'trim', 2);
      const tx0 = 2.2, tx1 = 4.7; winY(out, yS, -1, tx0, tx1, 1.12, 2.2, 'takeout', 'trim', 2);
      box(out, tx0 - 0.15, tx1 + 0.15, yS - 0.45, yS, 1.0, 1.06, 'ptwood', 'takeout.counter'); for (const x of [tx0 + 0.2, tx1 - 0.2]) stick(out, [x, yS - 0.03, 0.62], [x, yS - 0.4, 1.0], 0.03, 'galv', 'takeout.bracket');
      F(out, [[tx1 + 0.35, yS, 2.72], [tx0 - 0.35, yS, 2.72], [tx0 - 0.35, yS - 1.0, 2.36], [tx1 + 0.35, yS - 1.0, 2.36]], 'canvasG', -0.05, 'takeout.awning');
      panelY(out, yS - 1.0, -1, tx0 - 0.35, tx1 + 0.35, 2.2, 2.36, 'canvasG', -0.15, 'takeout.valance');
      const mx0 = 5.15, mx1 = 6.35, mz0 = 1.15, mz1 = 2.15; box(out, mx0 - 0.04, mx1 + 0.04, yS - 0.04, yS, mz0 - 0.04, mz1 + 0.04, 'wood', 'menu.board');
      F(out, [[mx0, yS - 0.044, mz0], [mx1, yS - 0.044, mz0], [mx1, yS - 0.044, mz1], [mx0, yS - 0.044, mz1]], 'chalk', -0.1, 'words.nmc_restaurant_menu');
      box(out, -3.6, -2.0, yS - 0.55, yS - 0.2, 0.42, 0.47, 'wood', 'bench'); for (const x of [-3.45, -2.15]) box(out, x - 0.04, x + 0.04, yS - 0.52, yS - 0.23, 0, 0.42, 'iron', 'bench.leg');
      // the name board stands on the eave over the door, lit by two gooseneck lamps
      const nx0 = -3.6, nx1 = 3.6, ny = Y0 - 0.45 - 0.05, nz0 = G.ez + 0.02, nz1 = G.ez + 0.72;
      box(out, nx0 - 0.05, nx1 + 0.05, ny - 0.06, ny, nz0 - 0.05, nz1 + 0.05, 'trim', 'name.board');
      F(out, [[nx0, ny - 0.064, nz0], [nx1, ny - 0.064, nz0], [nx1, ny - 0.064, nz1], [nx0, ny - 0.064, nz1]], 'board', -0.05, 'words.nmc_restaurant');
      for (const x of [nx0 + 1.0, nx1 - 1.0]) { stick(out, [x, ny, nz1 + 0.05], [x, ny - 0.45, nz1 + 0.3], 0.03, 'iron', 'name.lamp'); box(out, x - 0.1, x + 0.1, ny - 0.55, ny - 0.4, nz1 + 0.18, nz1 + 0.3, 'iron', 'name.lamp'); box(out, x - 0.08, x + 0.08, ny - 0.53, ny - 0.42, nz1 + 0.15, nz1 + 0.18, 'lampW', 'name.lens', { em: 'lamp' }); }
      // the roof: the kitchen hood on the yard slope, the stovepipe
      const hz = (x, y) => G.ez + (G.zr - G.ez) * ((y - (Y0 - 0.45)) / (0 - (Y0 - 0.45)));
      box(out, 4.3, 5.3, -2.4, -1.5, hz(0, -2.4) - 0.1, hz(0, -1.5) + 0.55, 'galv', 'roof.hood'); box(out, 4.2, 5.4, -2.5, -1.4, hz(0, -1.5) + 0.55, hz(0, -1.5) + 0.65, 'galv', 'roof.hood.cap');
      cyl(out, -6.0, -1.2, hz(0, -1.2) - 0.1, hz(0, -1.2) + 1.1, 0.12, 0.12, 8, 'iron', 'roof.stovepipe'); cyl(out, -6.0, -1.2, hz(0, -1.2) + 1.1, hz(0, -1.2) + 1.2, 0.22, 0.05, 8, 'iron', 'roof.stovepipe.cap');
      // propane at the west end, a grease bin
      for (const y of [-2.6, -1.9]) { cyl(out, X0 - 0.45, y, 0, 1.1, 0.19, 0.19, 10, 'propane', 'propane'); cyl(out, X0 - 0.45, y, 1.1, 1.22, 0.19, 0.08, 10, 'propane', 'propane'); }
      box(out, X0 - 0.9, X0 - 0.1, 1.2, 2.2, 0, 0.95, 'navy', 'grease.bin');
      // ---- the creek side (+Y): four big windows and the deck door
      for (const [a, b, id] of [[-6.8, -4.6, 'creek1'], [-3.4, -1.2, 'creek2'], [0.6, 2.8, 'creek3'], [4.0, 6.2, 'creek4']]) winY(out, Y1, 1, a, b, 0.95, 2.6, id, 'trim', 3);
      panelY(out, Y1 + 0.01, 1, 6.7, 7.7, 0.55, 2.7, 'trim', 0.1, 'deckDoor.frame'); panelY(out, Y1 + 0.02, 1, 6.8, 7.6, 0.57, 2.62, 'glass', 0.2, 'deckDoor', null, 'win');
      winX(out, X1, 1, -2.4, -0.6, 1.0, 2.4, 'east1'); winX(out, X1, 1, 1.0, 2.8, 1.0, 2.4, 'east2');
      if (o.deck) { const DZ = 0.55, YR = 8.6 + (+o.patio || 0), dN = [X0, 11.6, Y1, YR], dE = [X1, 11.6, -3.2, Y1];
        for (const [a, b, c, d] of [dN, dE]) { box(out, a, b, c, d, DZ - 0.12, DZ - 0.02, 'wood', 'deck.frame', { noTop: true }); FT(out, [[a, c, DZ], [b, c, DZ], [b, d, DZ], [a, d, DZ]], 'wood', 0.12, 'deck.floor', 'treads'); }
        const posts = []; for (let x = X0; x <= 11.6 + 1e-6; x += 2.45) posts.push([Math.min(x, 11.6), YR]); for (let y = YR - 2.3; y >= -3.2 - 1e-6; y -= 2.3) posts.push([11.6, Math.max(y, -3.2)]); for (let y = YR - 2.3; y > Y1 + 0.5; y -= 2.3) posts.push([X0, y]); posts.push([11.6, -3.2]);
        for (const [x, y] of posts) { box(out, x - 0.07, x + 0.07, y - 0.07, y + 0.07, -0.3, DZ + 1.0, 'ptwood', 'deck.post'); }
        box(out, X0, 11.6, YR - 0.05, YR + 0.05, DZ + 0.95, DZ + 1.02, 'ptwood', 'deck.rail'); box(out, 11.55, 11.65, -3.2, YR, DZ + 0.95, DZ + 1.02, 'ptwood', 'deck.rail'); box(out, X0 - 0.05, X0 + 0.05, Y1, YR, DZ + 0.95, DZ + 1.02, 'ptwood', 'deck.rail');
        for (let x = X0 + 0.4; x < 11.6; x += 0.62) box(out, x - 0.025, x + 0.025, YR - 0.02, YR + 0.02, DZ, DZ + 0.95, 'ptwood', 'deck.baluster');
        for (let y = -2.8; y < YR; y += 0.62) box(out, 11.58, 11.62, y - 0.025, y + 0.025, DZ, DZ + 0.95, 'ptwood', 'deck.baluster');
        for (let y = Y1 + 0.4; y < YR; y += 0.62) box(out, X0 - 0.02, X0 + 0.02, y - 0.025, y + 0.025, DZ, DZ + 0.95, 'ptwood', 'deck.baluster');
        box(out, 8.4, 10.2, -4.25, -3.2, 0, DZ - 0.2, 'ptwood', 'deck.steps'); FT(out, [[8.4, -4.25, DZ - 0.2], [10.2, -4.25, DZ - 0.2], [10.2, -3.2, DZ - 0.2], [8.4, -3.2, DZ - 0.2]], 'ptwood', 0.1, 'deck.steps', 'treads');
        const tq = (x, y, r) => { const t0 = out.length; picnic(out, x, y, r, 'deck.table'); for (let i = t0; i < out.length; i++) out[i].v = out[i].v.map(p => [p[0], p[1], p[2] + DZ]); };
        tq(9.8, -1.6, 1); tq(9.8, 1.6, 1); tq(-5.2, 6.6, 0); tq(-1.0, 6.6, 0); tq(3.2, 6.6, 0); tq(9.6, 6.6, 0);
        // the rear patio: round tables under market umbrellas, four chairs each, looking out over the water behind
        const UMB = ['canvasG', 'canvasB', 'flagR', 'canvasG'];
        if (YR > 10) for (const [tx, ty, k] of [[-5.6, 10.3, 0], [-1.7, 11.8, 1], [2.2, 10.3, 2], [6.1, 11.8, 3], [9.9, 10.3, 0], [-5.6, 13.0, 1], [2.2, 13.0, 3], [9.9, 13.0, 2]]) { if (ty > YR - 0.9) continue;
          const z = DZ; cyl(out, tx, ty, z, z + 0.72, 0.06, 0.06, 6, 'iron', 'patio.table'); cyl(out, tx, ty, z + 0.72, z + 0.76, 0.46, 0.46, 12, 'trim', 'patio.table');
          for (const [cx, cy] of [[0.7, 0], [-0.7, 0], [0, 0.7], [0, -0.7]]) { const x = tx + cx, y = ty + cy, bx = Math.sign(cx), by = Math.sign(cy);
            box(out, x - 0.21, x + 0.21, y - 0.21, y + 0.21, z + 0.42, z + 0.47, 'green', 'patio.chair'); box(out, x - 0.03, x + 0.03, y - 0.03, y + 0.03, z, z + 0.42, 'iron', 'patio.chair');
            if (bx) box(out, x + bx * 0.19 - 0.02, x + bx * 0.19 + 0.02, y - 0.21, y + 0.21, z + 0.47, z + 0.86, 'green', 'patio.chair'); else box(out, x - 0.21, x + 0.21, y + by * 0.19 - 0.02, y + by * 0.19 + 0.02, z + 0.47, z + 0.86, 'green', 'patio.chair'); }
          stick(out, [tx, ty, z + 0.76], [tx, ty, z + 2.55], 0.04, 'trim', 'patio.umbrella.pole'); const R = 1.32, zr = z + 2.12, za = z + 2.58, m = UMB[k % UMB.length];
          for (let s = 0; s < 8; s++) { const a0 = (s / 8) * 2 * Math.PI, a1 = ((s + 1) / 8) * 2 * Math.PI; F(out, [[tx + R * Math.cos(a0), ty + R * Math.sin(a0), zr], [tx + R * Math.cos(a1), ty + R * Math.sin(a1), zr], [tx, ty, za]], s % 2 ? m : 'trim', 0.1 + 0.06 * Math.cos(a0 + 2.4), 'patio.umbrella'); } }
        if (o.lights) { const poles = [[X0 + 0.1, YR - 0.1], [11.5, YR - 0.1], [11.5, -3.1]]; for (const [x, y] of poles) stick(out, [x, y, DZ + 1.0], [x, y, DZ + 3.1], 0.05, 'ptwood', 'lights.pole');
          const span = (A, B, n) => { for (let k = 0; k <= n; k++) { const t = k / n, sag = 0.35 * Math.sin(Math.PI * t), x = A[0] + (B[0] - A[0]) * t, y = A[1] + (B[1] - A[1]) * t, z = DZ + 3.05 - sag;
            box(out, x - 0.045, x + 0.045, y - 0.045, y + 0.045, z - 0.1, z - 0.02, 'amber', 'lights.bulb', { em: 'lamp' }); if (k) { const t2 = (k - 1) / n; stick(out, [A[0] + (B[0] - A[0]) * t2, A[1] + (B[1] - A[1]) * t2, DZ + 3.05 - 0.35 * Math.sin(Math.PI * t2)], [x, y, z], 0.012, 'iron', 'lights.wire'); } } };
          span(poles[0], poles[1], 26); span(poles[1], poles[2], 14); span([X1 - 0.1, -3.1, 0], poles[2], 5); } }
      return { faces: out, mats: mats(['butter', 'red', 'roofAsph', 'trim', 'glass', 'green', 'canvasG', 'wood', 'ptwood', 'galv', 'iron', 'conc', 'chalk', 'board', 'propane', 'navy', 'amber', 'lampW']),
        anchors: { door: [-0.45, Y0 - 1.6, 0], takeout: [3.45, Y0 - 0.9, 0], deckDoor: [7.2, Y1 + 0.6, 0.55], deckSteps: [9.3, -4.4, 0], deckZ: 0.55, patio: o.deck ? { box: [X0, 11.6, 8.6, 8.6 + (+o.patio || 0)], z: 0.55, tables: 8, seats: 32, umbrellas: 8, note: 'the rear patio over the water: the deck extended north' } : null,
          words: [{ id: 'words.nmc_restaurant', text: o.text, corners: [[nx0, ny - 0.064, nz0], [nx1, ny - 0.064, nz0], [nx1, ny - 0.064, nz1], [nx0, ny - 0.064, nz1]], note: 'the name board over the door; the owner names the place' },
            { id: 'words.nmc_restaurant_menu', text: 'CHOWDER \u00b7 FISH & CHIPS \u00b7 LOBSTER ROLLS', corners: [[mx0, yS - 0.044, mz0], [mx1, yS - 0.044, mz0], [mx1, yS - 0.044, mz1], [mx0, yS - 0.044, mz1]], note: 'the takeout\u2019s chalk menu; the game may bind it to the day\u2019s catch' }] } };
    },
    notes: 'Stand it with local +Y to the creek (dir 0 on the north row), so the camera sees the yard side: the door, the takeout, the name board, and the deck round the east end. Windows, the porch lamp, the name lamps and the deck lights glow with o.lit.',
  };

  /* THE FISH MARKET (structure.nmc_fish_market) — beside the harbourmaster's office: a one-room shed of silvered cedar shakes
     under a ribbed metal roof, 6.4 x 5.2 m, its long side to the yard: a wide sliding window over a steel counter under a blue
     awning, the door, the name board over the awning, a chalk board by the door (the day's prices, bound by the game), a chest
     of ice at the west end, a hose on its reel, a lamp over the door. */
  PIECES.fishMarket = {
    id: 'structure.nmc_fish_market', name: 'The fish market', defType: 'SetPieceDef', scene: 'keyscene.nmc_wharf',
    layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-3.2, -2.6], [3.2, -2.6], [3.2, 2.6], [-3.2, 2.6]], [[-4.45, -1.2], [-3.2, -1.2], [-3.2, 0.6], [-4.45, 0.6]]],
    defaults: { text: 'FISH MARKET', board: true },
    build(o) {
      const out = [], X0 = -3.2, X1 = 3.2, Y0 = -2.6, Y1 = 2.6, ZW = 2.7;
      box(out, X0 - 0.05, X1 + 0.05, Y0 - 0.05, Y1 + 0.05, -0.2, 0.05, 'conc', 'slab', { noTop: true });
      const G = gable(out, { x0: X0, x1: X1, y0: Y0, y1: Y1, zw: ZW, rise: 1.5, ov: 0.35, wall: 'shake', tex: 'shakes', roof: 'galv', roofTex: 'ribs', trim: 'trim', tag: 'body' });
      const yF = Y1, wx0 = -2.7, wx1 = 0.5, wz0 = 1.0, wz1 = 2.05;
      panelY(out, yF + 0.01, 1, wx0 - 0.1, wx1 + 0.1, wz0 - 0.1, wz1 + 0.08, 'trim', 0.1, 'window.trim');
      panelY(out, yF + 0.02, 1, wx0, (wx0 + wx1) / 2, wz0, wz1, 'glass', 0.25, 'window.counter', null, 'win'); panelY(out, yF + 0.018, 1, (wx0 + wx1) / 2, wx1, wz0, wz1, 'glass', 0.05, 'window.open', null, 'win');
      box(out, wx0 - 0.15, wx1 + 0.15, yF, yF + 0.48, wz0 - 0.08, wz0 - 0.02, 'chrome', 'counter'); for (const x of [wx0 + 0.2, wx1 - 0.2]) stick(out, [x, yF + 0.03, 0.55], [x, yF + 0.44, wz0 - 0.08], 0.03, 'galv', 'counter.bracket');
      F(out, [[wx1 + 0.3, yF, 2.28], [wx0 - 0.3, yF, 2.28], [wx0 - 0.3, yF + 0.95, 1.98], [wx1 + 0.3, yF + 0.95, 1.98]].reverse(), 'canvasB', 0.15, 'awning');
      panelY(out, yF + 0.95, 1, wx0 - 0.3, wx1 + 0.3, 1.84, 1.98, 'canvasB', 0.05, 'awning.valance');
      const sx0 = -2.55, sx1 = 0.35, sz0 = 2.3, sz1 = 2.62; box(out, sx0 - 0.04, sx1 + 0.04, yF, yF + 0.04, sz0 - 0.04, sz1 + 0.04, 'wood', 'sign.board');
      F(out, [[sx1, yF + 0.044, sz0], [sx0, yF + 0.044, sz0], [sx0, yF + 0.044, sz1], [sx1, yF + 0.044, sz1]], 'board', 0.25, 'words.nmc_fish_market');
      panelY(out, yF + 0.01, 1, 1.05, 2.05, 0, 2.1, 'trim', 0.1, 'door.frame'); panelY(out, yF + 0.02, 1, 1.13, 1.97, 0.02, 2.03, 'navy', 0.2, 'door.leaf'); box(out, 1.84, 1.9, yF + 0.02, yF + 0.07, 0.95, 1.05, 'chrome', 'door.handle');
      box(out, 1.45, 1.65, yF, yF + 0.12, 2.2, 2.32, 'iron', 'door.lamp'); box(out, 1.47, 1.63, yF + 0.04, yF + 0.16, 2.12, 2.2, 'lampW', 'door.lens', { em: 'lamp' });
      const W = [];
      if (o.board) { const bx = 2.65, by = Y1 + 0.75, bh = 0.95, sp = 0.28;
        F(out, [[bx + 0.3, by + sp, 0], [bx - 0.3, by + sp, 0], [bx - 0.3, by, bh], [bx + 0.3, by, bh]], 'wood', 0.2, 'aboard.frame'); F(out, [[bx - 0.3, by - sp, 0], [bx + 0.3, by - sp, 0], [bx + 0.3, by, bh], [bx - 0.3, by, bh]], 'wood', -0.1, 'aboard.frame');
        const C = [[bx + 0.24, by + sp * 0.88 + 0.012, 0.11], [bx - 0.24, by + sp * 0.88 + 0.012, 0.11], [bx - 0.24, by + sp * 0.1 + 0.012, bh - 0.09], [bx + 0.24, by + sp * 0.1 + 0.012, bh - 0.09]];
        F(out, C, 'chalk', 0.2, 'words.nmc_market_board');
        W.push({ id: 'words.nmc_market_board', text: 'TODAY', bind: 'market.today', format: ['TODAY', '{species1} {price1}', '{species2} {price2}'], corners: [C[1], C[0], C[3], C[2]], note: 'the day\u2019s prices, from the market the buyer reads' }); }
      box(out, -4.45, -3.3, -1.2, 0.6, 0, 0.88, 'siding', 'icechest'); box(out, -4.5, -3.25, -1.25, 0.65, 0.88, 0.96, 'siding', 'icechest.lid');
      cylX(out, X1 + 0.02, X1 + 0.2, -0.8, 1.1, 0.3, 10, 'iron', 'hose.reel'); cylX(out, X1 + 0.06, X1 + 0.16, -0.8, 1.1, 0.26, 10, 'hose', 'hose');
      for (let k = 0; k < 2; k++) box(out, 2.35, 3.05, 1.55 - 0.02, 2.05, k * 0.36, k * 0.36 + 0.34, 'tote', 'totes');
      return { faces: out, mats: mats(['shake', 'trim', 'galv', 'glass', 'chrome', 'canvasB', 'wood', 'board', 'navy', 'iron', 'lampW', 'chalk', 'siding', 'hose', 'tote', 'conc']),
        anchors: { counter: [-1.1, Y1 + 0.85, 0], door: [1.55, Y1 + 0.7, 0], iceChest: [-3.9, -0.3, 0.96],
          words: [{ id: 'words.nmc_fish_market', text: o.text, corners: [[sx0, yF + 0.044, sz0], [sx1, yF + 0.044, sz0], [sx1, yF + 0.044, sz1], [sx0, yF + 0.044, sz1]], note: 'the name board over the awning' }].concat(W) } };
    },
    notes: 'Stand it with local +Y to the yard (dir 4), beside the harbourmaster\u2019s office. The window, the door lamp glow with o.lit while it is open.',
  };

  /* THE CRANE'S LANDING PAD (prop.nmc_crane_pad) — a 2.4 m square painted yellow on the apron deck, hatched at its corners: where
     the crane sets a load down and picks one up (LOADS' seat 'pad'). A decal, drawn 1 cm proud of the deck. */
  PIECES.landingPad = {
    id: 'prop.nmc_crane_pad', name: 'Crane landing pad', defType: 'SetPieceDef', scene: 'keyscene.nmc_wharf', layer: 'floor', walk: 'on', mount: 'quay',
    footprints: [], defaults: { size: 2.4 },
    build(o) { const out = [], h = (+o.size || 2.4) / 2, w = 0.12, z0 = 0.004, z1 = 0.014;
      box(out, -h, h, -h, -h + w, z0, z1, 'yellow', 'pad.edge', { noBack: true }); box(out, -h, h, h - w, h, z0, z1, 'yellow', 'pad.edge', { noBack: true });
      box(out, -h, -h + w, -h + w, h - w, z0, z1, 'yellow', 'pad.edge'); box(out, h - w, h, -h + w, h - w, z0, z1, 'yellow', 'pad.edge');
      for (const [sx, sy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) for (let k = 0; k < 3; k++) { const d = 0.22 + k * 0.2, v = [[sx * (h - d), sy * (h - w), z1], [sx * (h - w), sy * (h - w), z1], [sx * (h - w), sy * (h - d), z1]], cz = (v[1][0] - v[0][0]) * (v[2][1] - v[0][1]) - (v[1][1] - v[0][1]) * (v[2][0] - v[0][0]); F(out, cz < 0 ? v.reverse() : v, 'yellow', 0.1, 'pad.hatch'); }
      return { faces: out, mats: mats(['yellow']), anchors: { seat: [0, 0, 0.014], size: [2 * h, 2 * h] } }; },
    notes: 'Paint it on the apron deck inside the crane\u2019s reach, behind the berth. anchors().seat is where a load sits.',
  };

  // =====================================================================================================================
  //  1.2.0: THE KEY SCENES' PIECES (Nine Mile Creek, part 2). Structural pieces (the bridge, the wall, the boardwalks, the
  //  river deck) carry their heights in metres above mean sea level: stand them at z 0. The rest stand on their ground point.
  // =====================================================================================================================
  function turn(out, t0, deg, cx, cy) { if (!deg) return; const a = deg * D2R, c = Math.cos(a), s = Math.sin(a);
    for (let i = t0; i < out.length; i++) out[i].v = out[i].v.map(p => [cx + (p[0] - cx) * c - (p[1] - cy) * s, cy + (p[0] - cx) * s + (p[1] - cy) * c, p[2]]); }
  // a lantern: a cast base plate, the glass (the emitter), corner bars, a pyramid roof and a finial; the glass centred on (x, y, z)
  function lantern(out, x, y, z, s, tag) { const h = 0.13 * s, g = 0.2 * s;
    box(out, x - h - 0.03, x + h + 0.03, y - h - 0.03, y + h + 0.03, z - g - 0.05, z - g, 'cast', tag + '.base');
    box(out, x - h, x + h, y - h, y + h, z - g, z + g, 'lampW', tag + '.glass', { em: 'lamp' });
    for (const [a, b] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) box(out, x + a * h - 0.015, x + a * h + 0.015, y + b * h - 0.016, y + b * h + 0.014, z - g, z + g, 'cast', tag + '.bar');
    const r = h + 0.06, zt = z + g, za = z + g + 0.2 * s, A = [[x - r, y - r], [x + r, y - r], [x + r, y + r], [x - r, y + r]];
    for (let k = 0; k < 4; k++) { const a = A[k], b = A[(k + 1) % 4]; F(out, [[a[0], a[1], zt], [b[0], b[1], zt], [x, y, za]], 'cast', [-0.1, 0.05, 0.12, -0.2][k], tag + '.roof'); }
    F(out, A.slice().reverse().map(p => [p[0], p[1], zt]), 'cast', -0.3, tag + '.roof');
    cyl(out, x, y, za - 0.02, za + 0.12 * s, 0.025, 0.01, 6, 'cast', tag + '.finial'); }
  // a hanging basket of petunias and trailing ivy, its rim at z
  function basket(out, x, y, z, tag) { cyl(out, x, y, z - 0.2, z, 0.1, 0.22, 8, 'leaf', tag + '.basket');
    for (const [dx, dy, m] of [[0.14, 0.05, 'flowerR'], [-0.1, 0.12, 'flowerY'], [0.02, -0.15, 'flowerR'], [-0.16, -0.06, 'flowerR'], [0.12, -0.12, 'flowerY'], [0, 0.17, 'flowerR'], [-0.03, 0.01, 'flowerR']])
      box(out, x + dx - 0.05, x + dx + 0.05, y + dy - 0.05, y + dy + 0.05, z - 0.03, z + 0.07, m, tag + '.flowers');
    for (const [dx, dy] of [[0.21, 0], [-0.2, 0.05], [0.05, 0.21], [0.02, -0.22]]) box(out, x + dx - 0.04, x + dx + 0.04, y + dy - 0.04, y + dy + 0.04, z - 0.34, z - 0.08, 'leaf', tag + '.trail'); }
  // a railing from A to B (plan points) on z0: posts, bars, balusters
  function railRun(out, A, B, z0, h, o) { o = o || {}; const L = Math.hypot(B[0] - A[0], B[1] - A[1]) || 1, ux = (B[0] - A[0]) / L, uy = (B[1] - A[1]) / L, mat = o.mat || 'cast', tag = o.tag || 'rail';
    const at = (s) => [A[0] + ux * s, A[1] + uy * s], n = Math.max(1, Math.round(L / (o.post || 2.0))), w = o.pw || 0.06;
    for (let k = 0; k <= n; k++) { const [x, y] = at(L * k / n); box(out, x - w, x + w, y - w, y + w, z0, z0 + h, mat, tag + '.post'); }
    for (const zz of (o.rails || [h - 0.04, h * 0.45])) stick(out, [A[0], A[1], z0 + zz], [B[0], B[1], z0 + zz], o.rw || 0.05, mat, tag + '.bar');
    if (o.bal) { const m = Math.max(2, Math.round(L / o.bal)); for (let k = 1; k < m; k++) { const [x, y] = at(L * k / m); stick(out, [x, y, z0 + 0.06], [x, y, z0 + h - 0.06], o.bw || 0.022, mat, tag + '.baluster'); } } }
  // a round cafe table, four chairs, a market umbrella; floor at z
  function umbrellaTable(out, tx, ty, z, m, tag) {
    cyl(out, tx, ty, z, z + 0.72, 0.05, 0.05, 6, 'cast', tag + '.table'); cyl(out, tx, ty, z + 0.72, z + 0.76, 0.42, 0.42, 12, 'trim', tag + '.table');
    for (const [cx, cy] of [[0.66, 0], [-0.66, 0], [0, 0.66], [0, -0.66]]) { const x = tx + cx, y = ty + cy, bx = Math.sign(cx), by = Math.sign(cy);
      box(out, x - 0.2, x + 0.2, y - 0.2, y + 0.2, z + 0.42, z + 0.47, 'railGreen', tag + '.chair'); box(out, x - 0.03, x + 0.03, y - 0.03, y + 0.03, z, z + 0.42, 'cast', tag + '.chair');
      if (bx) box(out, x + bx * 0.18 - 0.02, x + bx * 0.18 + 0.02, y - 0.2, y + 0.2, z + 0.47, z + 0.84, 'railGreen', tag + '.chair'); else box(out, x - 0.2, x + 0.2, y + by * 0.18 - 0.02, y + by * 0.18 + 0.02, z + 0.47, z + 0.84, 'railGreen', tag + '.chair'); }
    stick(out, [tx, ty, z + 0.76], [tx, ty, z + 2.5], 0.04, 'trim', tag + '.umbrella.pole'); const R = 1.25, zr = z + 2.08, za = z + 2.52;
    for (let s = 0; s < 8; s++) { const a0 = (s / 8) * 2 * Math.PI, a1 = ((s + 1) / 8) * 2 * Math.PI; F(out, [[tx + R * Math.cos(a0), ty + R * Math.sin(a0), zr], [tx + R * Math.cos(a1), ty + R * Math.sin(a1), zr], [tx, ty, za]], s % 2 ? m : 'trim', 0.1 + 0.06 * Math.cos(a0 + 2.4), tag + '.umbrella'); } }
  // a string of lights from A to B (plan points, top z at each end), sagging; bulbs glow with o.lit
  function lightString(out, A, B, za, zb, n, sag, tag) { let prev = null;
    for (let k = 0; k <= n; k++) { const t = k / n, x = A[0] + (B[0] - A[0]) * t, y = A[1] + (B[1] - A[1]) * t, z = za + (zb - za) * t - sag * Math.sin(Math.PI * t);
      if (k && k < n) box(out, x - 0.045, x + 0.045, y - 0.045, y + 0.045, z - 0.11, z - 0.03, 'amber', tag + '.bulb', { em: 'lamp' });
      if (prev) stick(out, prev, [x, y, z], 0.012, 'cast', tag + '.wire'); prev = [x, y, z]; } }

  /* THE ROUTE 91 BRIDGE (structure.nmc_route91_bridge) — the owner's commission of 08-20: the bridge where the through road
     crosses the town river gets its own artwork. Three spans on two granite piers, the end spans over the boardwalks and the
     middle one over the channel, a steel deck painted railway green, granite kerbs and parapets with an iron railing, brick
     sidewalks, a granite newel at each corner with a lantern and a bronze name plate (blank: the owner names it), four lamp
     standards over the piers, each with a cross-arm, two lanterns and a basket of petunias, and a clearance gauge on each pier's
     east end. Local +y runs north along the bridge (stand it at dir 0, z 0); heights are metres above mean sea level. The camera
     looks along the bridge, so what it sees is the deck, the lamps, the newels, the piers' cutwaters in the water and the shadow. */
  PIECES.routeBridge = {
    id: 'structure.nmc_route91_bridge', name: 'The Route 91 bridge', defType: 'SetPieceDef', scene: 'keyscene.nmc_town_bridge',
    layer: 'raised', walk: 'deck', mount: 'abutments',
    footprints: [], defaults: { len: 36, width: 9.5, deck: 6.0, soffit: 5.3, piers: [-7.2, 4.3], pierBed: -1.2, pierLen: 11.5, road: 6.6, lamps: true, baskets: true, gauge: true },
    build(o) {
      const out = [], hL = o.len / 2, hw = o.width / 2, D = o.deck, S = o.soffit, hr = o.road / 2, kb = hr + 0.15, pw = hw - 0.45;
      // the deck: a concrete slab on five steel girders (their faces run along the view; kept for the shadow and the colliders)
      box(out, -hw, hw, -hL, hL, D - 0.27, D - 0.02, 'conc', 'deck.slab', { noTop: true });
      for (const x of [-hw + 0.25, -2.4, 0, 2.4, hw - 0.25]) box(out, x - 0.18, x + 0.18, -hL, hL, S, D - 0.27, 'railGreen', 'deck.girder', { noTop: true });
      // the roadway: asphalt, a double yellow centre line, white edge lines
      F(out, [[-hr, -hL, D], [hr, -hL, D], [hr, hL, D], [-hr, hL, D]], 'asphalt', 0.1, 'deck.road');
      for (const x of [-0.17, 0.07]) F(out, [[x, -hL, D + 0.004], [x + 0.1, -hL, D + 0.004], [x + 0.1, hL, D + 0.004], [x, hL, D + 0.004]], 'lineY', 0.12, 'deck.centreLine');
      for (const a of [-hr + 0.15, hr - 0.27]) F(out, [[a, -hL, D + 0.004], [a + 0.12, -hL, D + 0.004], [a + 0.12, hL, D + 0.004], [a, hL, D + 0.004]], 'lineW', 0.12, 'deck.edgeLine');
      for (const s of [-1, 1]) {
        box(out, s < 0 ? -kb : hr, s < 0 ? -hr : kb, -hL, hL, D - 0.02, D + 0.15, 'granite', 'deck.kerb');
        boxT(out, s < 0 ? -pw : kb, s < 0 ? -kb : pw, -hL, hL, D - 0.02, D + 0.14, 'brick', 'deck.sidewalk', 'pavers');
        boxT(out, s < 0 ? -hw : pw, s < 0 ? -pw : hw, -hL, hL, D - 0.02, D + 0.5, 'granite', 'deck.parapet', 'ashlar');
        railRun(out, [s * (hw - 0.22), -hL + 0.95], [s * (hw - 0.22), hL - 0.95], D + 0.5, 0.64, { post: 1.8, mat: 'railGreen', rails: [0.6, 0.3, 0.08], tag: 'deck.railing' }); }
      // the newels: granite, capped, a lantern on each, a bronze plate on the face toward the approach
      const NWh = 0.48, words = [];
      for (const sy of [-1, 1]) for (const sx of [-1, 1]) { const x = sx * (hw - 0.24), y = sy * (hL - 0.5), tg = 'newel.' + (sy < 0 ? 'S' : 'N') + (sx < 0 ? 'W' : 'E');
        boxT(out, x - NWh, x + NWh, y - NWh, y + NWh, D - 0.02, D + 1.5, 'granite', tg, 'ashlar');
        box(out, x - NWh - 0.08, x + NWh + 0.08, y - NWh - 0.08, y + NWh + 0.08, D + 1.5, D + 1.68, 'granite', tg + '.cap');
        box(out, x - 0.2, x + 0.2, y - 0.2, y + 0.2, D + 1.68, D + 1.86, 'cast', tg + '.plinth');
        lantern(out, x, y, D + 2.16, 1.25, tg + '.lantern');
        const yp = y + sy * (NWh + 0.012), wid = (sx < 0 ? 'words.nmc_bridge_name' : 'words.nmc_bridge_year') + (sy < 0 ? '' : '.north');
        panelY(out, yp, sy, x - 0.3, x + 0.3, D + 0.7, D + 1.2, 'bronze', 0.15, wid);
        words.push({ id: wid, text: '', corners: [[x - 0.3, yp, D + 0.7], [x + 0.3, yp, D + 0.7], [x + 0.3, yp, D + 1.2], [x - 0.3, yp, D + 1.2]], note: sx < 0 ? 'the bridge\u2019s name: the owner names it; blank in pictures' : 'the year it was built: the owner\u2019s to give; blank in pictures' }); }
      // the lamp standards over the piers: a cross-arm east-west (the camera sees it), a lantern hung at each end, a basket
      const lamps = [];
      if (o.lamps) for (const yp of o.piers) for (const sx of [-1, 1]) { const x = sx * (hw - 0.22), tg = 'lamp.' + (yp < 0 ? 'S' : 'N') + (sx < 0 ? 'W' : 'E');
        box(out, x - 0.21, x + 0.21, yp - 0.21, yp + 0.21, D + 0.5, D + 0.98, 'cast', tg + '.base');
        cyl(out, x, yp, D + 0.98, D + 4.62, 0.085, 0.06, 8, 'cast', tg + '.column'); cyl(out, x, yp, D + 1.62, D + 1.72, 0.12, 0.12, 8, 'cast', tg + '.collar');
        stick(out, [x - 0.62, yp, D + 4.46], [x + 0.62, yp, D + 4.46], 0.06, 'cast', tg + '.arm'); cyl(out, x, yp, D + 4.62, D + 4.84, 0.05, 0.015, 6, 'cast', tg + '.finial');
        for (const dx of [-0.54, 0.54]) { stick(out, [x + dx, yp, D + 4.46], [x + dx, yp, D + 4.34], 0.025, 'cast', tg + '.hanger'); lantern(out, x + dx, yp, D + 4.08, 0.9, tg + '.lantern'); lamps.push([+(x + dx).toFixed(2), yp, +(D + 4.08).toFixed(2)]); }
        if (o.baskets) { stick(out, [x, yp, D + 3.46], [x + sx * 0.56, yp, D + 3.46], 0.03, 'cast', tg + '.bracket'); basket(out, x + sx * 0.56, yp, D + 3.3, tg); } }
      // the piers: granite ashlar, weed-dark below the springs, a pointed cutwater at each end, a projecting cap
      const pl = o.pierLen / 2, pt = 0.6, capZ = S - 0.02, NOSE = 1.1;
      for (const yp of o.piers) { const tg = 'pier.' + (yp < 0 ? 'S' : 'N');
        for (const [z0, z1, m] of [[o.pierBed, 1.9, 'graniteWet'], [1.9, capZ - 0.3, 'granite']]) {
          boxT(out, -pl, pl, yp - pt, yp + pt, z0, z1, m, tg, 'ashlar');
          for (const sx of [-1, 1]) { const P1 = [sx * pl, yp - pt], P2 = [sx * (pl + NOSE), yp], P3 = [sx * pl, yp + pt], q = (A, B) => [[A[0], A[1], z0], [B[0], B[1], z0], [B[0], B[1], z1], [A[0], A[1], z1]];
            if (sx > 0) { FT(out, q(P1, P2), m, 0.05, tg + '.cutwater', 'ashlar'); FT(out, q(P2, P3), m, 0.1, tg + '.cutwater', 'ashlar'); F(out, [P1, P2, P3].map(p => [p[0], p[1], z1]), m, 0.1, tg + '.cutwater'); }
            else { FT(out, q(P2, P1), m, -0.15, tg + '.cutwater', 'ashlar'); FT(out, q(P3, P2), m, -0.1, tg + '.cutwater', 'ashlar'); F(out, [P1, P3, P2].map(p => [p[0], p[1], z1]), m, 0.1, tg + '.cutwater'); } } }
        box(out, -pl - 1.2, pl + 1.2, yp - pt - 0.14, yp + pt + 0.14, capZ - 0.3, capZ, 'granite', tg + '.cap');
        if (o.gauge) { const gx0 = pl - 0.62, gx1 = pl - 0.34, gy = yp - pt - 0.012, z0 = 0.0, z1 = capZ - 0.42; panelY(out, gy, -1, gx0, gx1, z0, z1, 'lineW', 0.2, 'gauge.nmc_bridge_clearance');
          for (let z = z0 + 0.5; z < z1; z += 0.5) panelY(out, gy - 0.004, -1, gx0 + ((Math.round(z * 2) % 2) ? 0.12 : 0), gx1, z - 0.025, z + 0.025, 'cast', 0, 'gauge.tick'); } }
      // the abutments, under the deck's ends
      for (const sy of [-1, 1]) boxT(out, -hw - 0.7, hw + 0.7, sy < 0 ? -hL - 1.2 : hL, sy < 0 ? -hL : hL + 1.2, 2.2, D - 0.27, 'granite', 'abutment.' + (sy < 0 ? 'S' : 'N'), 'ashlar');
      const ys = [-hL].concat(o.piers, [hL]), SP = []; for (let k = 0; k + 1 < ys.length; k++) SP.push({ from: ys[k], to: ys[k + 1], clearM: +(ys[k + 1] - ys[k] - (k > 0 ? pt : 0) - (k + 2 < ys.length ? pt : 0)).toFixed(2) });
      return { faces: out, mats: mats(['conc', 'railGreen', 'asphalt', 'lineY', 'lineW', 'granite', 'graniteWet', 'brick', 'cast', 'lampW', 'bronze', 'leaf', 'flowerR', 'flowerY']),
        anchors: { deck: { z: D, box: [-hw, hw, -hL, hL], roadway: [-hr, hr], sidewalks: [[-pw, -kb], [kb, pw]], note: 'the roadway carries Route 91; the sidewalks are walkable; the parapets block' },
          soffit: S, spans: SP, piers: o.piers.map(y => ({ y, x: [-pl - NOSE, pl + NOSE], thickness: 2 * pt, bed: o.pierBed, cap: capZ })),
          clearance: { rule: 'soffit - tide (a boat passes if its air draft is under it)', atSpringHigh: +(S - 2.2).toFixed(2), atMean: S, gauge: 'gauge.nmc_bridge_clearance, on each pier\u2019s east end, south face' },
          lamps, newels: [[-(hw - 0.24), -(hL - 0.5)], [hw - 0.24, -(hL - 0.5)], [-(hw - 0.24), hL - 0.5], [hw - 0.24, hL - 0.5]], words } };
    },
    extra: (s) => ({ bridge: { spanM: s.len, spans: 3, deckZ: s.deck, soffitZ: s.soffit, clearanceAtSpringHighM: +(s.soffit - 2.2).toFixed(2), roadwayM: s.road, widthM: s.width, piersY: s.piers,
      boats: 'small craft pass under the middle span; the end spans cover the town boardwalks (2.3 m over their +3.0 decks)', drive: 'Route 91 (road.nmc.route_91) on the roadway', walk: 'both sidewalks; the parapets and railings block',
      lamps: 'four standards over the piers, two lanterns each, and a lantern on each newel, lit dusk to dawn' } }),
    notes: 'Stand it at dir 0 with z 0 at the bridge\u2019s centre on Route 91 (-176.8, 205): local +y is north. The names on the plates are the owner\u2019s (words.nmc_bridge_name, words.nmc_bridge_year), blank in pictures.',
  };

  /* THE RIVER WALL (structure.nmc_river_wall) — coursed granite from the north bench (+2.7) up to Water Street (+6.0), a cap
     course and a railing painted like the bridge's: the town's north bank. A segment: local +x along the wall, the face on
     local y = 0 toward -y (the river); heights above mean sea level (stand it at z 0), turned by o.yaw. */
  PIECES.riverWall = {
    id: 'structure.nmc_river_wall', name: 'The river wall (granite, under Water Street)', defType: 'SetPieceDef', scene: 'keyscene.nmc_town_bridge',
    layer: 'raised', walk: 'solid', mount: 'bench',
    footprints: [], defaults: { len: 8, foot: 2.7, top: 6.0, cap: 0.2, rail: true, thick: 0.9, weep: 3.2, yaw: 0 },
    build(o) { const out = [], hx = o.len / 2 + 0.03, z0 = o.foot - 0.35, zt = o.top, T = o.thick;
      boxT(out, -hx, hx, 0, T, z0, zt, 'granite', 'wall.face', 'ashlar');
      box(out, -hx - 0.01, hx + 0.01, -0.12, T + 0.05, zt, zt + o.cap, 'granite', 'wall.cap');
      panelY(out, -0.005, -1, -hx, hx, z0, o.foot + 0.24, 'graniteWet', -0.1, 'wall.damp');
      for (let x = -o.len / 2 + o.weep / 2; x < o.len / 2; x += o.weep) panelY(out, -0.01, -1, x - 0.07, x + 0.07, o.foot + 0.38, o.foot + 0.5, 'cast', -0.2, 'wall.weep');
      if (o.rail) railRun(out, [-o.len / 2, 0.3], [o.len / 2, 0.3], zt + o.cap, 1.0, { post: 2.0, mat: 'railGreen', rails: [0.96, 0.5], tag: 'wall.railing' });
      turn(out, 0, o.yaw, 0, 0);
      return { faces: out, mats: mats(['granite', 'graniteWet', 'cast', 'railGreen']), anchors: { face: 0, top: zt, cap: +(zt + o.cap).toFixed(2), foot: o.foot, len: o.len } }; },
    notes: 'Heights above mean sea level: stand it at z 0. Segments follow the north bench\u2019s back edge; the ground behind is laid to +6.0 (the scene\u2019s ground.laid).',
  };

  /* A TOWN BOARDWALK (structure.nmc_town_boardwalk) — a deck at +3.0 on timber piles over the river's upper bank: 2.4 m wide, the
     planks across it, a pile pair every 2.4 m with a cap beam, X braces on the river side, a railing on the river side (o.rail -1
     for local -y, +1 for +y) and a kerb log on the land side. A segment: local +x along it; o.ground gives the bank's height at
     even stations along it (the piles stand in it). Heights above mean sea level: stand it at z 0, turned by o.yaw. */
  PIECES.boardwalk = {
    id: 'structure.nmc_town_boardwalk', name: 'Town boardwalk, on piles', defType: 'SetPieceDef', scene: 'keyscene.nmc_town_bridge',
    layer: 'raised', walk: 'deck', mount: 'piles',
    footprints: [], defaults: { len: 12, width: 2.4, deck: 3.0, ground: [0.8, 0.8], rail: -1, yaw: 0, pile: 2.4, ext: 0.12 },
    build(o) { const out = [], L = o.len, hx = L / 2 + o.ext, hy = o.width / 2, D = o.deck, G = o.ground;
      const gAt = (x) => { const t = clamp((x + L / 2) / L, 0, 1) * (G.length - 1), k = Math.min(G.length - 2, Math.floor(t)); return G[k] + (G[k + 1] - G[k]) * (t - k); };
      FT(out, [[-hx, -hy, D], [hx, -hy, D], [hx, hy, D], [-hx, hy, D]], 'wood', 0.12, 'deck.floor', 'treads');
      for (const s of [-1, 1]) box(out, -hx, hx, s * hy - 0.06, s * hy + 0.06, D - 0.32, D - 0.01, 'ptwood', 'deck.stringer');
      const n = Math.max(1, Math.round(L / o.pile)), rs = o.rail || -1;
      for (let k = 0; k <= n; k++) { const x = -L / 2 + L * k / n;
        for (const s of [-1, 1]) cyl(out, x, s * (hy - 0.14), gAt(x) - 0.4, D - 0.32, 0.13, 0.13, 8, 'ptwood', 'deck.pile');
        box(out, x - 0.09, x + 0.09, -hy - 0.05, hy + 0.05, D - 0.44, D - 0.32, 'ptwood', 'deck.cap');
        if (k < n) { const x2 = -L / 2 + L * (k + 1) / n, ys = rs * (hy - 0.02), gz = Math.max(gAt(x), gAt(x2)) + 0.25;
          if (D - 0.5 - gz > 0.7) { stick(out, [x, ys, gz], [x2, ys, D - 0.5], 0.06, 'ptwood', 'deck.brace'); stick(out, [x2, ys, gz], [x, ys, D - 0.5], 0.06, 'ptwood', 'deck.brace'); } } }
      if (o.rail) railRun(out, [-L / 2 - o.ext, o.rail * (hy - 0.07)], [L / 2 + o.ext, o.rail * (hy - 0.07)], D, 1.05, { post: 2.0, mat: 'ptwood', rails: [1.0, 0.55], pw: 0.05, rw: 0.06, tag: 'deck.rail' });
      box(out, -hx, hx, -rs * (hy - 0.08) - 0.07, -rs * (hy - 0.08) + 0.07, D, D + 0.12, 'ptwood', 'deck.kerb');
      turn(out, 0, o.yaw, 0, 0);
      return { faces: out, mats: mats(['wood', 'ptwood']), anchors: { deck: D, width: o.width, len: L, rail: o.rail } }; },
    notes: 'Heights above mean sea level: stand it at z 0. One segment per straight run of the plan\u2019s boardwalk line; the deck is walkable, the rail blocks.',
  };

  /* THE BOARDWALK LAMP (prop.nmc_boardwalk_lamp) — a cast-iron standard with a lantern, and a basket of petunias on a bracket. */
  PIECES.boardwalkLamp = {
    id: 'prop.nmc_boardwalk_lamp', name: 'Boardwalk lamp', defType: 'SetPieceDef', scene: 'keyscene.nmc_town_bridge',
    layer: 'raised', walk: 'solid', mount: 'deck', footprints: [[[-0.2, -0.2], [0.2, -0.2], [0.2, 0.2], [-0.2, 0.2]]],
    defaults: { h: 3.4, basket: true },
    build(o) { const out = [], h = o.h;
      box(out, -0.2, 0.2, -0.2, 0.2, 0, 0.06, 'cast', 'lamp.plinth'); box(out, -0.16, 0.16, -0.16, 0.16, 0.06, 0.4, 'cast', 'lamp.base');
      cyl(out, 0, 0, 0.4, h - 0.26, 0.065, 0.05, 8, 'cast', 'lamp.column'); cyl(out, 0, 0, 1.1, 1.18, 0.09, 0.09, 8, 'cast', 'lamp.collar');
      lantern(out, 0, 0, h, 1.0, 'lamp.lantern');
      if (o.basket) { stick(out, [0, 0, h - 0.9], [0.56, 0, h - 0.9], 0.03, 'cast', 'lamp.bracket'); stick(out, [0.06, 0, h - 1.18], [0.46, 0, h - 0.92], 0.02, 'cast', 'lamp.bracket'); basket(out, 0.53, 0, h - 1.04, 'lamp'); }
      return { faces: out, mats: mats(['cast', 'lampW', 'leaf', 'flowerR', 'flowerY']), anchors: { light: [0, 0, h], note: 'the lantern, lit dusk to dawn' } }; },
    notes: 'Stand it on a deck (z = the deck) by the rail. The lantern glows with o.lit; the scene gives it a light pool.',
  };

  /* THE RIVER DECK (structure.nmc_river_deck) — the restaurant and bar's deck on the river wall: level with Water Street (+6.0),
     over the north bench on posts, its back on the wall's cap; a railing with balusters on three sides, five tables under market
     umbrellas, string lights on four poles, and stairs down to the bench along the wall at its west end. Local +x along the wall,
     the deck from y = -depth (the river side) to y = 0 (the wall's face); heights above mean sea level (z 0), turned by o.yaw. */
  PIECES.riverDeck = {
    id: 'structure.nmc_river_deck', name: 'The restaurant and bar\u2019s river deck', defType: 'SetPieceDef', scene: 'keyscene.nmc_town_bridge',
    layer: 'raised', walk: 'deck', mount: 'wall',
    footprints: [], defaults: { len: 16, depth: 5.0, deck: 6.0, bench: 2.7, tables: 5, lights: true, stairs: true, yaw: 0 },
    build(o) { const out = [], hx = o.len / 2, y0 = -o.depth, D = o.deck, B = o.bench;
      FT(out, [[-hx, y0, D], [hx, y0, D], [hx, 0, D], [-hx, 0, D]], 'wood', 0.12, 'deck.floor', 'treads');
      box(out, -hx, hx, y0, 0, D - 0.3, D - 0.01, 'ptwood', 'deck.frame', { noTop: true });
      const nx = 6; for (let k = 0; k <= nx; k++) { const x = -hx + 0.2 + (o.len - 0.4) * k / nx; for (const y of [y0 + 0.2, y0 * 0.45]) box(out, x - 0.1, x + 0.1, y - 0.1, y + 0.1, B - 0.3, D - 0.3, 'ptwood', 'deck.post');
        if (k < nx) { const x2 = -hx + 0.2 + (o.len - 0.4) * (k + 1) / nx; stick(out, [x, y0 + 0.12, D - 0.4], [(x + x2) / 2, y0 + 0.12, D - 1.4], 0.07, 'ptwood', 'deck.knee'); stick(out, [x2, y0 + 0.12, D - 0.4], [(x + x2) / 2, y0 + 0.12, D - 1.4], 0.07, 'ptwood', 'deck.knee'); } }
      railRun(out, [-hx, y0 + 0.05], [hx, y0 + 0.05], D, 1.05, { post: 2.0, mat: 'trim', bal: 0.15, bw: 0.03, rails: [1.0, 0.1], pw: 0.05, tag: 'deck.rail' });
      railRun(out, [hx - 0.05, y0], [hx - 0.05, -0.1], D, 1.05, { post: 2.0, mat: 'trim', bal: 0.15, bw: 0.03, rails: [1.0, 0.1], pw: 0.05, tag: 'deck.rail' });
      if (!o.stairs) railRun(out, [-hx + 0.05, y0], [-hx + 0.05, -0.1], D, 1.05, { post: 2.0, mat: 'trim', bal: 0.15, bw: 0.03, rails: [1.0, 0.1], pw: 0.05, tag: 'deck.rail' });
      const UMB = ['canvasG', 'flagR', 'canvasB', 'canvasG', 'flagR'];
      for (let k = 0; k < o.tables; k++) umbrellaTable(out, -hx + o.len * (k + 0.5) / o.tables, y0 * 0.52, D, UMB[k % UMB.length], 'deck.table' + k);
      if (o.lights) { const P = [[-hx + 0.1, y0 + 0.1], [-hx / 3, y0 + 0.1], [hx / 3, y0 + 0.1], [hx - 0.1, y0 + 0.1]];
        for (const [x, y] of P) stick(out, [x, y, D + 1.0], [x, y, D + 3.0], 0.05, 'ptwood', 'lights.pole');
        for (let k = 0; k + 1 < P.length; k++) lightString(out, P[k], P[k + 1], D + 2.95, D + 2.95, 16, 0.32, 'lights'); }
      let foot = null;
      if (o.stairs) { const n = Math.round((D - B) / 0.185), rz = (D - B) / n, g = 0.27, w = 1.2;
        for (let k = 0; k < n; k++) { const z1 = D - k * rz, x1 = -hx - k * g; box(out, x1 - g, x1, -w, -0.02, B - 0.1, z1 - rz, 'ptwood', 'stairs.step'); }
        stick(out, [-hx, -w + 0.05, D + 0.95], [-hx - n * g, -w + 0.05, B + 0.95], 0.05, 'trim', 'stairs.rail');
        for (let k = 0; k <= n; k += 4) { const x = -hx - k * g, z = D - k * rz; box(out, x - 0.04, x + 0.04, -w + 0.01, -w + 0.09, z - 0.2, z + 0.95, 'trim', 'stairs.post'); }
        foot = [+(-hx - n * g).toFixed(2), -0.6, B]; }
      turn(out, 0, o.yaw, 0, 0);
      return { faces: out, mats: mats(['wood', 'ptwood', 'trim', 'cast', 'railGreen', 'canvasG', 'canvasB', 'flagR', 'amber']),
        anchors: { deck: D, box: [-hx, hx, y0, 0], tables: o.tables, seats: o.tables * 4, stairsFoot: foot, note: 'walkable deck; the railing blocks; the stairs reach the bench, the boardwalk\u2019s landing beside them' } }; },
    notes: 'Heights above mean sea level: stand it at z 0 with its back (y = 0) on the wall\u2019s face. The bulbs glow with o.lit.',
  };

  /* THE WALL STAIRS (structure.nmc_wall_stairs) — a granite flight from the bench up the river wall to Water Street, its risers
     toward the river, stepped cheeks and an iron handrail each side. The flight runs from local y = -run (the bench) to y = 0 (the
     wall's face). Stand it on the bench (z = the bench) at the wall's face, turned by o.yaw. */
  PIECES.wallStairs = {
    id: 'structure.nmc_wall_stairs', name: 'Stairs up the river wall', defType: 'SetPieceDef', scene: 'keyscene.nmc_town_bridge',
    layer: 'raised', walk: 'stairs', mount: 'wall',
    footprints: [], defaults: { rise: 3.3, width: 1.5, going: 0.28, yaw: 0 },
    build(o) { const out = [], n = Math.round(o.rise / 0.185), rz = o.rise / n, hw = o.width / 2, run = n * o.going;
      for (let k = 0; k < n; k++) { const y0 = -run + k * o.going; box(out, -hw, hw, y0, 0.05, -0.15, (k + 1) * rz, 'granite', 'stairs.step'); }
      for (const s of [-1, 1]) { for (let k = 0; k < n; k += 2) { const y0 = -run + k * o.going; box(out, s < 0 ? -hw - 0.3 : hw, s < 0 ? -hw : hw + 0.3, y0, 0.05, -0.15, Math.min(o.rise, (k + 2) * rz) + 0.3, 'granite', 'stairs.cheek'); }
        stick(out, [s * (hw - 0.06), -run + 0.1, 0.95], [s * (hw - 0.06), -0.05, o.rise + 0.95], 0.045, 'railGreen', 'stairs.rail');
        for (const t of [0.08, 0.5, 0.92]) { const y = -run + run * t, z = o.rise * t; stick(out, [s * (hw - 0.06), y, z], [s * (hw - 0.06), y, z + 0.95], 0.035, 'railGreen', 'stairs.post'); } }
      turn(out, 0, o.yaw, 0, 0);
      return { faces: out, mats: mats(['granite', 'railGreen']), anchors: { foot: [0, -run, 0], head: [0, 0, o.rise], steps: n, riser: +rz.toFixed(3), going: o.going } }; },
    notes: 'Stand it on the bench at the wall\u2019s face (z = the bench). Walkable as stairs from foot to head.',
  };

  /* THE CHANDLERY (structure.nmc_chandlery) — the rod vendor, moved onto Water Street over the river it sells gear for: a
     cedar-shake shop, 9 x 7 m, its eaves to the street; two shop windows either side of a half-glazed door, a navy fascia board
     (words.nmc_chandlery, blank), a hanging sign on an iron bracket (words.nmc_chandlery_sign, blank), two loft windows, and out
     front a rack of oars, a coil of rope, buoys on the wall, a bench and a lamp over the door. */
  PIECES.chandlery = {
    id: 'structure.nmc_chandlery', name: 'The chandlery (the rod vendor)', defType: 'SetPieceDef', scene: 'keyscene.nmc_town_bridge',
    layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-4.5, -3.5], [4.5, -3.5], [4.5, 3.5], [-4.5, 3.5]]], defaults: { text: 'CHANDLERY', lights: true },
    build(o) { const out = [], X0 = -4.5, X1 = 4.5, Y0 = -3.5, Y1 = 3.5, ZW = 4.8;
      box(out, X0 - 0.05, X1 + 0.05, Y0 - 0.05, Y1 + 0.05, -0.3, 0.12, 'conc', 'foundation', { noTop: true });
      gable(out, { x0: X0, x1: X1, y0: Y0, y1: Y1, zw: ZW, rise: 2.4, ov: 0.35, wall: 'shake', tex: 'shakes', roof: 'roofAsph', trim: 'trim', tag: 'body' });
      const yS = Y1; winY(out, yS, 1, -3.9, -1.35, 0.55, 2.5, 'shopW', 'navy', 3); winY(out, yS, 1, 1.35, 3.9, 0.55, 2.5, 'shopE', 'navy', 3);
      panelY(out, yS + 0.01, 1, -0.75, 0.75, 0, 2.45, 'navy', 0.1, 'door.frame'); panelY(out, yS + 0.02, 1, -0.62, 0.62, 0.02, 2.35, 'navy', 0.2, 'door.leaf'); panelY(out, yS + 0.03, 1, -0.44, 0.44, 1.2, 2.15, 'glass', 0.25, 'door.window', null, 'win');
      box(out, -4.2, 4.2, yS, yS + 0.08, 2.72, 3.3, 'navy', 'fascia'); const FC = [[3.95, yS + 0.084, 2.8], [-3.95, yS + 0.084, 2.8], [-3.95, yS + 0.084, 3.22], [3.95, yS + 0.084, 3.22]]; F(out, FC, 'board', 0.25, 'words.nmc_chandlery');
      winY(out, yS, 1, -2.7, -1.3, 3.55, 4.45, 'loftW', 'trim', 2); winY(out, yS, 1, 1.3, 2.7, 3.55, 4.45, 'loftE', 'trim', 2);
      stick(out, [X1 - 0.3, yS + 0.02, 3.55], [X1 - 0.3, yS + 1.2, 3.55], 0.05, 'iron', 'sign.bracket'); stick(out, [X1 - 0.3, yS + 0.02, 3.15], [X1 - 0.3, yS + 0.95, 3.53], 0.03, 'iron', 'sign.bracket');
      box(out, X1 - 0.34, X1 - 0.26, yS + 0.36, yS + 1.14, 2.72, 3.46, 'board', 'words.nmc_chandlery_sign');
      for (let k = 0; k < 6; k++) stick(out, [-4.32 + k * 0.15, yS + 0.36, 0], [-4.36 + k * 0.15, yS + 0.12, 2.15], 0.05, 'wood', 'oars');
      box(out, -4.48, -3.5, yS + 0.08, yS + 0.46, 0.92, 0.98, 'wood', 'oars.rail');
      cyl(out, 3.4, yS + 0.75, 0, 0.26, 0.38, 0.38, 10, 'rope', 'rope.coil'); cyl(out, 3.4, yS + 0.75, 0.26, 0.27, 0.16, 0.16, 8, 'rope', 'rope.coil');
      for (const [x, z, m] of [[4.25, 2.2, 'orange'], [4.25, 1.55, 'yellow'], [3.95, 1.9, 'flagR']]) { cyl(out, x, yS + 0.2, z - 0.28, z, 0.13, 0.13, 8, m, 'buoys'); stick(out, [x, yS + 0.2, z], [x, yS + 0.02, z + 0.35], 0.02, 'rope', 'buoys'); }
      box(out, -0.12, 0.12, yS, yS + 0.18, 2.55, 2.68, 'iron', 'door.lamp'); box(out, -0.09, 0.09, yS + 0.05, yS + 0.22, 2.44, 2.55, 'lampW', 'door.lens', { em: 'lamp' });
      box(out, 1.45, 3.1, yS + 0.55, yS + 0.9, 0.42, 0.47, 'wood', 'bench'); for (const x of [1.6, 2.95]) box(out, x - 0.04, x + 0.04, yS + 0.58, yS + 0.87, 0, 0.42, 'iron', 'bench.leg');
      return { faces: out, mats: mats(['shake', 'trim', 'roofAsph', 'navy', 'glass', 'board', 'iron', 'wood', 'rope', 'orange', 'yellow', 'flagR', 'lampW', 'conc']),
        anchors: { door: [0, Y1 + 1.2, 0], counter: [0, 1.2, 0], words: [{ id: 'words.nmc_chandlery', text: o.text, corners: FC.slice().reverse(), note: 'the fascia over the windows; blank in pictures' }, { id: 'words.nmc_chandlery_sign', text: '', note: 'the hanging sign; the owner\u2019s to letter, blank in pictures' }] } }; },
    notes: 'Stand it with local +Y to Water Street (dir 4 on the street\u2019s north side). The windows and the door lamp glow with o.lit while it is open or after dusk.',
  };

  // =====================================================================================================================
  //  1.3.0: PART 2B'S PIECES — the Falls (the water on the ledges, the fishing deck), the lookout and the landing (rails, benches,
  //  boards, the bar's stakes, a life ring), the campground (the comfort station, fire rings, tents, site posts, tables, lights)
  // =====================================================================================================================
  const r3 = (v) => Math.round(v * 1000) / 1000;
  const hsh13 = (a, b) => { let h = Math.imul(a | 0, 374761393) + Math.imul(b | 0, 668265263); h = Math.imul(h ^ (h >>> 13), 1274126177); return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };

  /* THE FALLS (structure.nmc_the_falls_water) — the head of tide: three steps of red sandstone across the river, and the white water
     on them. build() is the rock the water leaves dry: at each lip the blocks it parts round (o.islands[k], as fractions of the
     channel from its south bank) and a shelf at each bank's toe, bedded, their faces to the view, so the steps read as a stair at the
     banks. paint() is the water, pixel by pixel as the camera sees it: the glassy tongue before each lip, the lip's white edge, the
     curtain, the boil at its foot and the foam trailing down the tread and the pool. The game draws its own water and foam; the
     painter is the look asked of them. Local +x downstream (the river runs within 7 degrees of east there: the piece does not turn);
     o.lips[k] = [x, run]: lip k and its curtain's run; o.levels[k] the water above lip k, the last the pool's; o.edges
     [[x, yS, yN], ...] the channel's banks in local metres (the scene's still water). Stand it at z 0 at the Falls. */
  const vnz = (x, y, s) => { const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
    const a = hsh13(xi * 7 + s, yi), b = hsh13(xi * 7 + 7 + s, yi), c = hsh13(xi * 7 + s, yi + 1), d = hsh13(xi * 7 + 7 + s, yi + 1);
    return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v; };
  const fallsEdge = (o, lx) => { const E = o.edges; if (lx <= E[0][0]) return [E[0][1], E[0][2]];
    for (let k = 0; k + 1 < E.length; k++) { const a = E[k], b = E[k + 1]; if (lx <= b[0]) { const t = (lx - a[0]) / ((b[0] - a[0]) || 1); return [a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t]; } }
    const l = E[E.length - 1]; return [l[1], l[2]]; };
  // lip k's line across the channel: bowed downstream in the middle, where the water has worn it, and ragged (f: 0 south bank, 1 north)
  const lipOff = (o, k, f) => 0.3 * (1 - (2 * f - 1) * (2 * f - 1)) + 0.34 * (vnz(f * 5.3 + k * 11.7, 0.7 + k, (o.seed | 0) + 17) - 0.5);
  // the water's surface at lx and f: on the tread above lip k (k 0: the upper reach), in the curtain of lip k, or in the pool
  function fallsZ(o, lx, f) { const L = o.lips, Z = o.levels, ff = f == null ? 0.5 : f;
    for (let k = 0; k < L.length; k++) { const xl = L[k][0] + lipOff(o, k, ff), d = L[k][1];
      if (lx < xl) { const foot = k ? L[k - 1][0] + lipOff(o, k - 1, ff) + L[k - 1][1] : -1e9, s = lx - foot, t = clamp((lx - (xl - 0.8)) / 0.8, 0, 1);
        return { z: Z[k] - 0.08 * t * t + (s < 0.8 ? 0.05 * (1 - s / 0.8) : 0), seg: k ? 'tread' : 'reach', k, s: k ? s : 1e9, toLip: xl - lx }; }
      if (lx < xl + d) { const t = (lx - xl) / d; return { z: Z[k] - 0.08 - (Z[k] - 0.08 - Z[k + 1]) * Math.pow(t, 1.6), seg: 'curtain', k, t }; } }
    const xl = L[L.length - 1][0] + lipOff(o, L.length - 1, ff), d = L[L.length - 1][1], s = lx - (xl + d); return { z: Z[Z.length - 1] + (s < 0.8 ? 0.05 * (1 - s / 0.8) : 0), seg: 'pool', k: L.length, s, toLip: 1e9 }; }
  // a stone: an irregular prism, n corners round an ellipse with their radii jittered, its top tipped a little, its sides bedded
  function stone(out, cx, cy, rx, ry, z0, z1, mat, tag, seed, n) { n = n || 7; const Pp = [];
    for (let i = 0; i < n; i++) { const a = (i + 0.35 * hsh13(seed, i)) / n * 2 * Math.PI, rr = 0.76 + 0.24 * hsh13(seed + 5, i); Pp.push([cx + rx * rr * Math.cos(a), cy + ry * rr * Math.sin(a)]); }
    const tip = (hsh13(seed, 99) - 0.5) * 0.14, tz = (p) => z1 - tip * (p[0] - cx) / rx - 0.05 * (p[1] - cy) / ry;
    F(out, Pp.map(p => [p[0], p[1], tz(p)]), mat, 0.1, tag + '.top');
    for (let i = 0; i < n; i++) { const A = Pp[i], B = Pp[(i + 1) % n]; FT(out, [[A[0], A[1], z0], [B[0], B[1], z0], [B[0], B[1], tz(B)], [A[0], A[1], tz(A)]], mat, 0, tag, 'bedding'); } }
  PIECES.cascade = {
    id: 'structure.nmc_the_falls_water', name: 'The Falls: three sandstone steps in white water', defType: 'SetPieceDef', scene: 'keyscene.nmc_the_falls',
    layer: 'water', walk: 'the blocks and shelves are rock, standable; the water is not walkable', mount: 'river bed', footprints: [],
    defaults: { lips: [[-2.8, 0.5], [-0.8, 0.45], [1.2, 0.55]], levels: [2.9, 2.15, 1.35, 0.58], islands: [[[0, 0.12], [0.4, 0.52], [0.87, 1]], [[0, 0.07], [0.23, 0.31], [0.63, 0.77]], [[0.46, 0.55], [0.93, 1]]],
      edges: [[-6, -2.5, 2.5], [9, -4.5, 5]], x0: -5.5, x1: 8.5, seed: 29 },
    build(o) { const out = [], L = o.lips, Z = o.levels, blocks = [], shelves = [];
      for (let k = 0; k < L.length; k++) { const xl = L[k][0], [yS, yN] = fallsEdge(o, xl), W = yN - yS, zt = Z[k] + 0.12, zb = Z[k + 1] - 0.3;
        // the rock the lip parts round: a stone or two in each dry stretch, the bank ones reaching into the bank
        o.islands[k].forEach(([fa, fb], j) => { const f = (Math.max(0, fa) + Math.min(1, fb)) / 2, off = lipOff(o, k, f), cx = xl + off - 0.36, ya = fa <= 0 ? yS - 0.9 : yS + fa * W, yb = fb >= 1 ? yN + 0.9 : yS + fb * W, ry = Math.max(0.28, (yb - ya) / 2 + 0.06), sd = 300 + k * 37 + j * 7;
          stone(out, cx, (ya + yb) / 2, 0.5, ry, zb, zt, 'redstoneWet', 'falls.block', sd, ry > 0.7 ? 9 : 7); if (ry > 0.75) stone(out, cx - 0.55, (ya + yb) / 2 + 0.2 * (hsh13(sd, 3) - 0.5), 0.42, ry * 0.7, zb, zt + 0.08, 'redstone', 'falls.block', sd + 1, 7);
          blocks.push({ lip: k, at: [r3(cx), r3((ya + yb) / 2)], r: [0.5, r3(ry)], top: r3(zt) }); });
        // a shelf at each bank's toe above the lip, stepping down lip by lip: the stair the camera sees at the banks
        for (const s of [-1, 1]) { const yb = s < 0 ? yS - 0.55 : yN + 0.6, top = Z[k] + (s < 0 ? 0.22 : 0.3), sd = 500 + k * 13 + (s < 0 ? 0 : 5), off = lipOff(o, k, s < 0 ? 0 : 1);
          stone(out, xl + off - 0.9, yb, 0.95, 0.75, zb, top, 'redstone', 'falls.shelf', sd, 8); stone(out, xl + off - 1.9, yb + s * 0.35, 0.6, 0.55, zb, top + 0.12, 'redstone', 'falls.shelf', sd + 2, 7);
          shelves.push({ lip: k, bank: s < 0 ? 'south' : 'north', at: [r3(xl + off - 0.9), r3(yb)], r: [0.95, 0.75], top: r3(top) }); } }
      return { faces: out, mats: mats(['redstone', 'redstoneWet']), anchors: { lip: [L[0][0], 0, Z[0]], foot: [L[L.length - 1][0] + L[L.length - 1][1], 0, Z[Z.length - 1]], levels: Z, lips: L, blocks, shelves,
        note: 'the blocks and shelves are rock (walkable, fishable from); the sound point sits at the foot (scene.json sound[])' } }; },
    // the water, painted: { w, h, px, py, rgba, Z } at 32 px to the metre, the pivot at the piece's origin, Z the water's height
    paint(o) { const X0 = o.x0, X1 = o.x1, Zs = o.levels, E = o.edges; let yLo = 1e9, yHi = -1e9; for (const e of E) { yLo = Math.min(yLo, e[1]); yHi = Math.max(yHi, e[2]); }
      const U0 = Math.floor((yLo * SE + Math.min(...Zs) * CE - 0.3) * PPU) / PPU, U1 = Math.ceil((yHi * SE + Math.max(...Zs) * CE + 0.3) * PPU) / PPU;
      const w = Math.round((X1 - X0) * PPU), h = Math.round((U1 - U0) * PPU), rgba = new Uint8ClampedArray(w * h * 4), Zb = new Float32Array(w * h).fill(NaN), sd = o.seed | 0;
      // only the white water and the light on the water: the still water under it is the ground's (the scene's still-water tint)
      const C = { hi: [243, 247, 244], mid: [216, 228, 226], sh: [182, 199, 200], aer: [160, 181, 178], glint: [196, 210, 206], lip: [232, 240, 238] };
      for (let c = 0; c < w; c++) { const lx = X0 + (c + 0.5) / PPU, [yS, yN] = fallsEdge(o, lx), Wd = Math.max(0.5, yN - yS);
        for (let r = 0; r < h; r++) { const u = U1 - (r + 0.5) / PPU; let g = fallsZ(o, lx, 0.5), ly = (u - g.z * CE) / SE, f = (ly - yS) / Wd;
          for (let it = 0; it < 2; it++) { g = fallsZ(o, lx, clamp(f, 0, 1)); ly = (u - g.z * CE) / SE; f = (ly - yS) / Wd; }
          if (f < 0 || f > 1) continue;
          let hole = false; for (let k = 0; k < o.lips.length && !hole; k++) { const xl = o.lips[k][0] + lipOff(o, k, f), d = o.lips[k][1]; if (lx < xl - 0.75 || lx > xl + d * 0.8) continue; const ext = 0.12 * CE / SE / Wd;
            for (const [fa, fb] of o.islands[k]) if (f >= fa - 0.004 && f <= fb + ext) { hole = true; break; } }
          if (hole) continue;
          let col = null; const n = 0.6 * vnz(lx * 3.1, ly * 3.7, sd + 3) + 0.4 * vnz(lx * 7.3, ly * 8.1, sd + 4), edge = Math.min(f, 1 - f);
          if (g.seg === 'curtain') { const st = vnz(ly * 15, g.t * 3, sd + 2); col = g.t < 0.12 ? (vnz(ly * 9, 0.5, sd + 1) > 0.6 ? C.glint : C.lip) : st > 0.66 ? C.sh : st > 0.38 ? C.mid : C.hi; }
          else { const s = g.s, pool = g.seg === 'pool'; let D = 0;
            if (s < 1e8) { const Lb = 1.15 + 0.9 * vnz(ly * 1.9, 0.3, sd + 5); D = Math.max(0, 1 - s / Lb) * 1.5;
              const tr = 0.7 * vnz(lx * 0.5, ly * 2.8, sd + 6) + 0.3 * vnz(lx * 1.4, ly * 5.2, sd + 10), taper = clamp((X1 - lx) / 2.5, 0, 1), fade = Math.exp(-s / (pool ? 5.0 : 2.2)) * taper * taper; D = Math.max(D, (tr - 0.4) * 2.6 * fade);
              if (edge < 0.1) D = Math.max(D, 0.62 * Math.exp(-s / (pool ? 6 : 2.5)) * (1 - edge / 0.1) * clamp((X1 - lx) / 2.5, 0, 1)); }
            D += (n - 0.5) * 0.6 * (s < 1e8 ? clamp((X1 - lx) / 1.5, 0, 1) : 1);
            if (g.toLip < 0.1) col = C.lip;
            else if (D > 0.64) col = n > 0.66 ? C.sh : C.hi; else if (D > 0.5) col = C.mid; else if (D > 0.37) col = C.aer;
            else if (g.toLip < 0.9) { const q = (((ly * 2.4 + vnz(lx * 2, ly * 2, sd + 7) * 0.7) % 1) + 1) % 1; if (q < 0.16 && vnz(lx * 4, ly * 4, sd + 8) > 0.35) col = C.glint; } }
          if (!col) continue; const i = r * w + c; rgba[i * 4] = col[0]; rgba[i * 4 + 1] = col[1]; rgba[i * 4 + 2] = col[2]; rgba[i * 4 + 3] = 255; Zb[i] = g.z; } }
      return { w, h, px: Math.round(-X0 * PPU), py: Math.round(U1 * PPU), rgba, Z: Zb }; },
    extra: (s) => ({ falls: { levels: s.levels, lips: s.lips, dropM: +(s.levels[0] - s.levels[s.levels.length - 1]).toFixed(2), fishing: 'the pool below the third lip, from the Falls deck and the south shelves; trout and salmon lie in the boils', sound: 'the falls: loudest on the ebb, when the pool stands clear of the sea' } }),
    notes: 'Stand it at z 0 at the Falls (-275.5, 197.3). The rock is a collider; the water is the game\u2019s. NmcSetPieces.paint(\u2019cascade\u2019, o) paints the water for pictures.',
  };

  /* THE FALLS DECK (structure.nmc_falls_deck) — a fishing deck over the Falls pool at +3.4 on timber piles: a railing on three sides
     (the fourth open to the boardwalk), a bench along the back, a rod rack, a life ring on a post and a notice board (blank). Local
     +x along the deck from the boardwalk (open at -x), the river on local -y (o.side -1) or +y (o.side 1, mirrored); heights above mean sea level (z 0), turned by o.yaw. */
  PIECES.fallsDeck = {
    id: 'structure.nmc_falls_deck', name: 'The Falls deck', defType: 'SetPieceDef', scene: 'keyscene.nmc_the_falls',
    layer: 'raised', walk: 'deck', mount: 'piles', footprints: [], defaults: { len: 7.2, width: 3.0, deck: 3.4, ground: [1.2, 0.2], yaw: 0, side: -1 },
    build(o) { const out = [], hx = o.len / 2, hy = o.width / 2, D = o.deck, G = o.ground, gAt = (x) => { const t = clamp((x + hx) / o.len, 0, 1) * (G.length - 1), k = Math.min(G.length - 2, Math.floor(t)); return G[k] + (G[k + 1] - G[k]) * (t - k); };
      FT(out, [[-hx, -hy, D], [hx, -hy, D], [hx, hy, D], [-hx, hy, D]], 'wood', 0.12, 'deck.floor', 'treads');
      box(out, -hx, hx, -hy, hy, D - 0.32, D - 0.01, 'ptwood', 'deck.frame', { noTop: true });
      for (let k = 0; k <= 3; k++) { const x = -hx + 0.15 + (o.len - 0.3) * k / 3; for (const s of [-1, 1]) cyl(out, x, s * (hy - 0.15), gAt(x) - 0.4, D - 0.32, 0.14, 0.14, 8, 'ptwood', 'deck.pile'); }
      for (let k = 0; k < 3; k++) { const x0 = -hx + 0.15 + (o.len - 0.3) * k / 3, x1 = -hx + 0.15 + (o.len - 0.3) * (k + 1) / 3, gz = Math.max(gAt(x0), gAt(x1)) + 0.25; if (D - 0.5 - gz > 0.6) { stick(out, [x0, -hy + 0.02, gz], [x1, -hy + 0.02, D - 0.5], 0.06, 'ptwood', 'deck.brace'); stick(out, [x1, -hy + 0.02, gz], [x0, -hy + 0.02, D - 0.5], 0.06, 'ptwood', 'deck.brace'); } }
      railRun(out, [-hx + 0.05, -hy + 0.06], [hx - 0.06, -hy + 0.06], D, 1.05, { post: 1.8, mat: 'ptwood', bal: 0.16, bw: 0.035, rails: [1.0, 0.1], pw: 0.05, tag: 'deck.rail' });
      railRun(out, [hx - 0.06, -hy + 0.06], [hx - 0.06, hy - 0.06], D, 1.05, { post: 1.5, mat: 'ptwood', bal: 0.16, bw: 0.035, rails: [1.0, 0.1], pw: 0.05, tag: 'deck.rail' });
      railRun(out, [-hx + 0.05, hy - 0.06], [hx - 0.06, hy - 0.06], D, 1.05, { post: 1.8, mat: 'ptwood', rails: [1.0, 0.5], pw: 0.05, tag: 'deck.rail' });
      box(out, -hx + 1.0, hx - 0.8, hy - 0.62, hy - 0.2, D + 0.42, D + 0.47, 'wood', 'bench'); for (const x of [-hx + 1.2, hx - 1.0]) box(out, x - 0.04, x + 0.04, hy - 0.58, hy - 0.24, D, D + 0.42, 'cast', 'bench.leg');
      box(out, hx - 0.7, hx - 0.2, -hy + 0.3, -hy + 0.42, D + 0.6, D + 0.66, 'ptwood', 'rack'); for (let k = 0; k < 4; k++) stick(out, [hx - 0.65 + k * 0.13, -hy + 0.36, D + 0.02], [hx - 0.63 + k * 0.13, -hy + 0.2, D + 2.2], 0.02, 'cast', 'rack.rod');
      box(out, -hx + 0.25, -hx + 0.35, -hy + 0.1, -hy + 0.2, D, D + 1.5, 'ptwood', 'lifering.post'); cylY(out, -hx + 0.3, -hy - 0.03, -hy + 0.05, D + 1.05, 0.3, 14, 'orange', 'lifering');
      box(out, -hx + 0.4, -hx + 1.3, hy - 0.12, hy - 0.04, D + 0.9, D + 1.6, 'board', 'words.nmc_falls_notice');
      if (o.side > 0) for (const f of out) f.v = f.v.map(p => [p[0], -p[1], p[2]]).reverse();   // the river on local +y: mirrored
      turn(out, 0, o.yaw, 0, 0);
      return { faces: out, mats: mats(['wood', 'ptwood', 'cast', 'orange', 'board']), anchors: { deck: D, fishFrom: [0, -hy - 0.4, D], note: 'fish from the river rail: the Falls pool below', words: [{ id: 'words.nmc_falls_notice', text: '', note: 'the fishing notice; the game\u2019s rules for the Falls, blank in pictures' }] } }; },
    notes: 'Heights above mean sea level: stand it at z 0 on its line off the north boardwalk\u2019s west end.',
  };

  /* A TIMBER GUARD RAIL (prop.nmc_guard_rail) — posts every 2 m and two rails, 1.0 m high: the lookout's edge. Local +x along it. */
  PIECES.guardRail = { id: 'prop.nmc_guard_rail', name: 'Guard rail (timber)', defType: 'SetPieceDef', scene: 'keyscene.nmc_the_falls', layer: 'raised', walk: 'solid', mount: 'ground', footprints: [],
    defaults: { len: 8, yaw: 0 }, build(o) { const out = []; railRun(out, [-o.len / 2, 0], [o.len / 2, 0], 0, 1.0, { post: 2.0, mat: 'ptwood', rails: [0.95, 0.5], pw: 0.07, rw: 0.08, tag: 'rail' }); turn(out, 0, o.yaw, 0, 0);
      return { faces: out, mats: mats(['ptwood']), anchors: { len: o.len } }; }, notes: 'Stand it on the ground (z = the ground), turned by o.yaw.' };

  /* A BENCH (prop.nmc_bench) — slatted timber on cast legs, 1.8 m, facing local +Y. */
  PIECES.bench = { id: 'prop.nmc_bench', name: 'Bench', defType: 'SetPieceDef', scene: 'keyscene.nmc_the_falls', layer: 'raised', walk: 'solid', mount: 'ground', footprints: [[[-0.9, -0.3], [0.9, -0.3], [0.9, 0.3], [-0.9, 0.3]]],
    defaults: {}, build() { const out = []; for (let k = 0; k < 3; k++) box(out, -0.9, 0.9, -0.24 + k * 0.16, -0.12 + k * 0.16, 0.42, 0.46, 'wood', 'seat');
      for (let k = 0; k < 2; k++) F(out, [[-0.9, -0.28, 0.55 + k * 0.17], [0.9, -0.28, 0.55 + k * 0.17], [0.9, -0.3, 0.68 + k * 0.17], [-0.9, -0.3, 0.68 + k * 0.17]], 'wood', 0.05, 'back');
      for (const x of [-0.75, 0.75]) { box(out, x - 0.04, x + 0.04, -0.26, 0.22, 0, 0.42, 'cast', 'leg'); box(out, x - 0.03, x + 0.03, -0.32, -0.26, 0.4, 0.9, 'cast', 'leg'); }
      return { faces: out, mats: mats(['wood', 'cast']), anchors: { sit: [0, 0.05, 0.44] } }; }, notes: 'Stand it facing the view (local +Y).' };

  /* A SIGN BOARD (prop.nmc_sign_board) — a board on two posts under a little shingled roof; its words are the game's (blank). */
  PIECES.signBoard = { id: 'prop.nmc_sign_board', name: 'Sign board', defType: 'SetPieceDef', scene: 'keyscene.nmc_the_falls', layer: 'raised', walk: 'solid', mount: 'ground', footprints: [],
    defaults: { w: 1.4, h: 0.9, words: 'words.nmc_sign' }, build(o) { const out = [], hw = o.w / 2, z0 = 0.9, z1 = z0 + o.h;
      for (const x of [-hw + 0.08, hw - 0.08]) box(out, x - 0.06, x + 0.06, -0.06, 0.06, 0, z1 + 0.25, 'ptwood', 'post');
      box(out, -hw, hw, 0.02, 0.08, z0, z1, 'ptwood', 'board'); F(out, [[hw - 0.06, 0.084, z0 + 0.06], [-hw + 0.06, 0.084, z0 + 0.06], [-hw + 0.06, 0.084, z1 - 0.06], [hw - 0.06, 0.084, z1 - 0.06]], 'board', 0.2, o.words);
      F(out, [[hw + 0.12, 0.3, z1 + 0.14], [-hw - 0.12, 0.3, z1 + 0.14], [-hw - 0.12, -0.05, z1 + 0.34], [hw + 0.12, -0.05, z1 + 0.34]], 'roofAsph', 0.1, 'roof');
      return { faces: out, mats: mats(['ptwood', 'board', 'roofAsph']), anchors: { words: [{ id: o.words, text: '', note: 'blank in pictures' }] } }; }, notes: 'Stand it with its face (local +Y) to the path.' };

  /* THE CROSSING BOARD (structure.nmc_crossing_board) — at the head of the bar crossing: a roofed board on two posts, the crossing's
     notice on the left and the day's tides on the right (words.nmc_crossing_notice and words.nmc_crossing_tides, bound to the game's
     tide table), a bell on a bracket for fog. */
  PIECES.crossingBoard = { id: 'structure.nmc_crossing_board', name: 'The crossing board', defType: 'SetPieceDef', scene: 'keyscene.nmc_landing', layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-1.4, -0.2], [1.4, -0.2], [1.4, 0.2], [-1.4, 0.2]]], defaults: {}, build() { const out = [], hw = 1.3, z0 = 0.8, z1 = 2.15;
      for (const x of [-hw, hw]) box(out, x - 0.08, x + 0.08, -0.08, 0.08, 0, z1 + 0.35, 'ptwood', 'post');
      box(out, -hw, hw, 0.02, 0.1, z0, z1, 'navy', 'board'); const P1 = [[-0.08, 0.104, z0 + 0.08], [-hw + 0.1, 0.104, z0 + 0.08], [-hw + 0.1, 0.104, z1 - 0.08], [-0.08, 0.104, z1 - 0.08]], P2 = [[hw - 0.1, 0.104, z0 + 0.08], [0.08, 0.104, z0 + 0.08], [0.08, 0.104, z1 - 0.08], [hw - 0.1, 0.104, z1 - 0.08]];
      F(out, P1, 'board', 0.2, 'words.nmc_crossing_notice'); F(out, P2, 'chalk', 0.2, 'words.nmc_crossing_tides');
      F(out, [[hw + 0.25, 0.45, z1 + 0.2], [-hw - 0.25, 0.45, z1 + 0.2], [-hw - 0.25, -0.1, z1 + 0.5], [hw + 0.25, -0.1, z1 + 0.5]], 'roofAsph', 0.1, 'roof');
      stick(out, [hw, 0, z1], [hw + 0.45, 0.1, z1], 0.04, 'cast', 'bell.bracket'); cyl(out, hw + 0.45, 0.1, z1 - 0.34, z1 - 0.05, 0.14, 0.07, 10, 'bronze', 'bell');
      return { faces: out, mats: mats(['ptwood', 'navy', 'board', 'chalk', 'roofAsph', 'cast', 'bronze']), anchors: { bell: [hw + 0.45, 0.1, z1 - 0.2], read: [0, 1.1, 0],
        words: [{ id: 'words.nmc_crossing_notice', text: '', note: 'the crossing\u2019s notice: cross on the ebb, return before the flood; blank in pictures' }, { id: 'words.nmc_crossing_tides', text: '', bind: 'tide.today', note: 'today\u2019s low and high waters from the game\u2019s tide table' }] } }; },
    notes: 'Stand it at the head of the crossing with its face (local +Y) to the land, where walkers read it before they go down.' };

  /* A BAR STAKE (prop.nmc_bar_stake) — a spruce pole driven into the bar with a red and white top and a reflector: they mark the
     crossing's line across the flats every dozen metres. */
  PIECES.barStake = { id: 'prop.nmc_bar_stake', name: 'Bar stake', defType: 'SetPieceDef', scene: 'keyscene.nmc_landing', layer: 'raised', walk: 'solid', mount: 'ground', footprints: [],
    defaults: { h: 2.6 }, build(o) { const out = [], h = o.h; cyl(out, 0, 0, -0.4, h, 0.06, 0.045, 6, 'ptwood', 'pole'); cyl(out, 0, 0, h - 0.6, h - 0.3, 0.052, 0.05, 6, 'flagR', 'band'); cyl(out, 0, 0, h - 0.3, h, 0.05, 0.045, 6, 'lineW', 'band');
      box(out, -0.05, 0.05, -0.07, -0.05, h - 0.9, h - 0.75, 'yellow', 'reflector'); return { faces: out, mats: mats(['ptwood', 'flagR', 'lineW', 'yellow']), anchors: { top: [0, 0, h] } }; },
    notes: 'Stand them on the crossing\u2019s line (path.nmc_bar_walk), one every 12 m.' };

  /* A LIFE RING POST (prop.nmc_life_ring) — a post with an orange life ring and its line in a box. */
  PIECES.lifeRingPost = { id: 'prop.nmc_life_ring', name: 'Life ring post', defType: 'SetPieceDef', scene: 'keyscene.nmc_landing', layer: 'raised', walk: 'solid', mount: 'ground', footprints: [],
    defaults: {}, build() { const out = []; box(out, -0.07, 0.07, -0.07, 0.07, 0, 1.7, 'ptwood', 'post'); box(out, -0.45, 0.45, 0.07, 0.2, 0.95, 1.65, 'flagR', 'box');
      cylY(out, 0, 0.2, 0.28, 1.28, 0.32, 14, 'orange', 'ring'); return { faces: out, mats: mats(['ptwood', 'flagR', 'orange']), anchors: { take: [0, 0.8, 0] } }; }, notes: 'Face (local +Y) to the path.' };

  /* THE COMFORT STATION (structure.nmc_camp_comfort) — Loop A's washrooms: an 8 x 6 m building in cedar shakes, a gable roof, two
     doors under a porch (words.nmc_comfort_men / _women, blank), louvred vents, a lamp over each door, a drinking fountain, a bench. */
  PIECES.comfortStation = { id: 'structure.nmc_camp_comfort', name: 'The comfort station', defType: 'SetPieceDef', scene: 'keyscene.nmc_campground', layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-4, -3], [4, -3], [4, 3], [-4, 3]]], defaults: { lights: true }, build(o) { const out = [], X0 = -4, X1 = 4, Y0 = -3, Y1 = 3, ZW = 2.9;
      box(out, X0 - 0.05, X1 + 0.05, Y0 - 0.05, Y1 + 0.05, -0.3, 0.15, 'conc', 'slab', { noTop: true });
      gable(out, { x0: X0, x1: X1, y0: Y0, y1: Y1, zw: ZW, rise: 1.7, ov: 0.4, wall: 'shake', tex: 'shakes', roof: 'roofAsph', trim: 'trim', tag: 'body' });
      const yS = Y1, words = []; for (const [x, id] of [[-2.0, 'words.nmc_comfort_men'], [2.0, 'words.nmc_comfort_women']]) {
        panelY(out, yS + 0.01, 1, x - 0.55, x + 0.55, 0.15, 2.3, 'trim', 0.1, 'door.frame'); panelY(out, yS + 0.02, 1, x - 0.45, x + 0.45, 0.17, 2.2, 'green', 0.2, 'door.leaf');
        const C = [[x + 0.3, yS + 0.03, 2.35], [x - 0.3, yS + 0.03, 2.35], [x - 0.3, yS + 0.03, 2.6], [x + 0.3, yS + 0.03, 2.6]]; F(out, C, 'board', 0.2, id); words.push({ id, text: '', note: 'the door\u2019s sign, blank in pictures' });
        box(out, x - 0.08, x + 0.08, yS, yS + 0.16, 2.66, 2.8, 'cast', 'door.lamp'); box(out, x - 0.06, x + 0.06, yS + 0.04, yS + 0.2, 2.56, 2.66, 'lampW', 'door.lens', { em: 'lamp' }); }
      for (const x of [-3.3, 0, 3.3]) panelY(out, yS + 0.015, 1, x - 0.35, x + 0.35, 2.3, 2.6, 'alum', 0.05, 'vent', 'louvre');
      F(out, [[3.2, yS, 2.9], [-3.2, yS, 2.9], [-3.2, yS + 1.3, 2.6], [3.2, yS + 1.3, 2.6]].reverse(), 'roofAsph', 0.12, 'porch.roof'); for (const x of [-3.1, 3.1]) box(out, x - 0.06, x + 0.06, yS + 1.18, yS + 1.3, 0, 2.6, 'trim', 'porch.post');
      box(out, 0.2, 0.55, yS + 0.3, yS + 0.62, 0, 0.9, 'galv', 'fountain'); box(out, -1.2, -0.5, yS + 0.25, yS + 0.6, 0, 1.0, 'green', 'bin');
      return { faces: out, mats: mats(['conc', 'shake', 'roofAsph', 'trim', 'green', 'board', 'cast', 'lampW', 'alum', 'galv']), anchors: { doors: [[-2.0, Y1 + 1.0, 0], [2.0, Y1 + 1.0, 0]], fountain: [0.38, Y1 + 0.9, 0], words } }; },
    notes: 'Stand it with its doors (local +Y) to the loop road.' };

  /* A FIRE RING (prop.nmc_fire_ring) — a steel ring 0.9 m across set in a circle of beach stones, split logs in it; lit (o.lit) it
     carries a fire whose flames glow with the lamp rule, and the scene gives it an orange light pool. */
  PIECES.fireRing = { id: 'prop.nmc_fire_ring', name: 'Fire ring', defType: 'SetPieceDef', scene: 'keyscene.nmc_campground', layer: 'floor', walk: 'solid', mount: 'ground',
    footprints: [[[-0.6, -0.6], [0.6, -0.6], [0.6, 0.6], [-0.6, 0.6]]], defaults: { lit: true }, build(o) { const out = [];
      for (let k = 0; k < 9; k++) { const a = k / 9 * 2 * Math.PI, x = 0.62 * Math.cos(a), y = 0.62 * Math.sin(a); box(out, x - 0.12, x + 0.12, y - 0.1, y + 0.1, 0, 0.14, 'granite', 'stones'); }
      cyl(out, 0, 0, 0, 0.32, 0.46, 0.46, 16, 'cast', 'ring', true); cyl(out, 0, 0, 0.02, 0.05, 0.44, 0.44, 16, 'charcoal', 'ash');
      for (let k = 0; k < 4; k++) { const a = k / 4 * Math.PI + 0.3; stick(out, [0.3 * Math.cos(a), 0.3 * Math.sin(a), 0.06], [-0.28 * Math.cos(a), -0.28 * Math.sin(a), 0.2], 0.09, 'wood', 'logs'); }
      // the fire: nine tongues in two rings, the tall ones in the middle, a bed of embers under them
      if (o.lit) { cyl(out, 0, 0, 0.05, 0.12, 0.3, 0.26, 12, 'flame', 'embers', false, 'lamp');
        for (let k = 0; k < 9; k++) { const ring = k < 3 ? 0.06 : 0.2, a = k / (k < 3 ? 3 : 6) * 2 * Math.PI + (k < 3 ? 0 : 0.4), x = ring * Math.cos(a), y = ring * Math.sin(a), h = (k < 3 ? 0.62 : 0.36) + 0.22 * hsh13(k, 3), w = k < 3 ? 0.12 : 0.09;
          F(out, [[x - w, y, 0.14], [x + w, y, 0.14], [x * 0.4, y * 0.4, 0.14 + h]], k % 2 ? 'flame' : 'amber', 0.3, 'flames', 'lamp'); F(out, [[x, y - w, 0.14], [x, y + w, 0.14], [x * 0.4, y * 0.4, 0.14 + h * 0.92]], k % 3 ? 'flame' : 'amber', 0.2, 'flames', 'lamp'); } }
      return { faces: out, mats: mats(['granite', 'cast', 'charcoal', 'wood', 'flame', 'amber']), anchors: { fire: [0, 0, 0.4], sitRadius: 1.6 } }; },
    notes: 'The flames glow with o.lit (the lamp rule: dusk to midnight at an occupied site).' };

  /* A DOME TENT (prop.nmc_camp_tent) — four hoops, a fly in o.colour, the door (local +Y) half open. */
  PIECES.campTent = { id: 'prop.nmc_camp_tent', name: 'Dome tent', defType: 'SetPieceDef', scene: 'keyscene.nmc_campground', layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-1.2, -1.05], [1.2, -1.05], [1.2, 1.05], [-1.2, 1.05]]], defaults: { colour: 'canvasG' }, build(o) { const out = [], rx = 1.2, ry = 1.05, H = 1.3, N = 8, M = 4;
      const P = (i, j) => { const a = i / N * 2 * Math.PI, e = j / M * Math.PI / 2; return [rx * Math.cos(a) * Math.cos(e), ry * Math.sin(a) * Math.cos(e), H * Math.sin(e)]; };
      for (let i = 0; i < N; i++) for (let j = 0; j < M; j++) { const a = P(i, j), b = P(i + 1, j), c = P(i + 1, j + 1), d = P(i, j + 1); F(out, j + 1 < M ? [a, b, c, d] : [a, b, c], ((i + j) % 2) ? o.colour : 'trim', 0.1 * Math.cos((i + 0.5) / N * 2 * Math.PI - 2.2), 'tent.fly'); }
      F(out, [[-0.35, ry + 0.02, 0], [0.35, ry + 0.02, 0], [0, ry * 0.7 + 0.02, 0.95]], 'charcoal', 0, 'tent.door');
      return { faces: out, mats: mats(['canvasG', 'canvasB', 'flagR', 'orange', 'yellow', 'trim', 'charcoal']), anchors: { door: [0, ry + 0.5, 0] } }; },
    notes: 'Stand it on a tent pad, door (local +Y) to the fire.' };

  /* A SITE POST (prop.nmc_site_post) — a serviced site's pedestal: 30 A and 15 A outlets, a hose bib, the site's number (words). */
  PIECES.sitePost = { id: 'prop.nmc_site_post', name: 'Site post (power and water)', defType: 'SetPieceDef', scene: 'keyscene.nmc_campground', layer: 'raised', walk: 'solid', mount: 'ground', footprints: [],
    defaults: { words: 'words.nmc_camp_site' }, build(o) { const out = []; box(out, -0.16, 0.16, -0.12, 0.12, 0, 1.1, 'alum', 'post'); box(out, -0.14, 0.14, 0.12, 0.16, 0.6, 0.95, 'navy', 'box');
      box(out, 0.16, 0.24, -0.04, 0.04, 0.35, 0.42, 'galv', 'bib'); F(out, [[0.1, 0.162, 0.98], [-0.1, 0.162, 0.98], [-0.1, 0.162, 1.07], [0.1, 0.162, 1.07]], 'lineW', 0.2, o.words);
      return { faces: out, mats: mats(['alum', 'navy', 'galv', 'lineW']), anchors: { plug: [0, 0.2, 0.8], words: [{ id: o.words, text: '', note: 'the site\u2019s number; blank in pictures' }] } }; }, notes: 'At the pad\u2019s road end, face (local +Y) to the road.' };

  /* A PICNIC TABLE (prop.nmc_picnic_table) — the kit's picnic table on its own. */
  PIECES.picnicTable = { id: 'prop.nmc_picnic_table', name: 'Picnic table', defType: 'SetPieceDef', scene: 'keyscene.nmc_campground', layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-0.9, -0.8], [0.9, -0.8], [0.9, 0.8], [-0.9, 0.8]]], defaults: { rot: 0 }, build(o) { const out = []; picnic(out, 0, 0, o.rot, 'table'); return { faces: out, mats: mats(['wood', 'iron', 'galv', 'ptwood']), anchors: { seats: 6 } }; }, notes: '' };

  /* SITE LIGHTS (prop.nmc_site_lights) — a string of bulbs from the camper's awning rail to a pole: o.len long, o.h high. */
  PIECES.siteLights = { id: 'prop.nmc_site_lights', name: 'Site lights', defType: 'SetPieceDef', scene: 'keyscene.nmc_campground', layer: 'raised', walk: 'none', mount: 'ground', footprints: [],
    defaults: { len: 4.5, h: 2.3, yaw: 0 }, build(o) { const out = []; stick(out, [o.len, 0, 0], [o.len, 0, o.h + 0.1], 0.04, 'ptwood', 'pole'); lightString(out, [0, 0], [o.len, 0], o.h, o.h, 12, 0.22, 'lights'); turn(out, 0, o.yaw, 0, 0);
      return { faces: out, mats: mats(['ptwood', 'cast', 'amber']), anchors: { len: o.len } }; }, notes: 'The bulbs glow with o.lit.' };

  // =====================================================================================================================
  //  1.4.0: THE KEY SCENES' PIECES, PART 2C (the north creek, the weather face). The 1.1 to 1.3 pieces are unchanged.
  // =====================================================================================================================
  // an Adirondack chair, its seat facing local +x, at (x, y) on a floor at z
  function adirondack(out, x, y, z, mat, tag) {
    for (const s of [-1, 1]) { box(out, x - 0.34, x + 0.36, y + s * 0.33 - 0.03, y + s * 0.33 + 0.03, z, z + 0.36, mat, tag + '.leg'); box(out, x - 0.3, x + 0.42, y + s * 0.36 - 0.06, y + s * 0.36 + 0.06, z + 0.56, z + 0.6, mat, tag + '.arm'); }
    F(out, [[x + 0.36, y - 0.3, z + 0.38], [x + 0.36, y + 0.3, z + 0.38], [x - 0.24, y + 0.3, z + 0.3], [x - 0.24, y - 0.3, z + 0.3]], mat, 0.15, tag + '.seat');
    for (let k = 0; k < 5; k++) { const yy = y - 0.24 + k * 0.12, h = 0.62 + 0.1 * (1 - Math.abs(k - 2) / 2); F(out, [[x - 0.24, yy - 0.045, z + 0.3], [x - 0.24, yy + 0.045, z + 0.3], [x - 0.5, yy + 0.045, z + 0.3 + h], [x - 0.5, yy - 0.045, z + 0.3 + h]], mat, 0.05, tag + '.back'); } }

  /* A DOCK ON THE NORTH CREEK (structure.nmc_creek_dock) — a fixed timber dock off a waterfront lot: a deck at +2.8 (dry at the spring
     high, +2.2), 1.5 m wide, on post pairs every 1.75 m with cap beams and X braces where the mud lies deep enough, a bumper along the
     outer end, a ladder down its +y side at the end, two galvanised cleats, and (o.chairs) Adirondack chairs at the end looking out.
     Local +x from the shore end (x 0) to the outer end (x len); o.ground gives the bank's height at even stations along it (the posts
     stand in it). Heights above mean sea level: stand it at z 0 at the dock's middle, turned by o.yaw. */
  PIECES.creekDock = {
    id: 'structure.nmc_creek_dock', name: 'A dock on the north creek', defType: 'SetPieceDef', scene: 'keyscene.nmc_north_creek',
    layer: 'raised', walk: 'deck', mount: 'posts', footprints: [],
    defaults: { len: 7, width: 1.5, deck: 2.8, ground: [2.4, 1.6, 0.8, 0.2], yaw: 0, ladder: true, cleats: 2, chairs: 0, chairMat: 'red', bay: 1.75 },
    build(o) { const out = [], L = o.len, h = L / 2, hy = o.width / 2, D = o.deck, G = o.ground;
      const gAt = (x) => { const t = clamp((x + h) / L, 0, 1) * (G.length - 1), k = Math.min(G.length - 2, Math.floor(t)); return G[k] + (G[k + 1] - G[k]) * (t - k); };
      FT(out, [[-h, -hy, D], [h, -hy, D], [h, hy, D], [-h, hy, D]], 'wood', 0.12, 'dock.deck', 'treads');
      for (const s of [-1, 1]) box(out, -h, h, s * hy - 0.07, s * hy + 0.02 * s, D - 0.26, D - 0.01, 'ptwood', 'dock.stringer');
      const n = Math.max(1, Math.round(L / o.bay));
      for (let k = 0; k <= n; k++) { const x = -h + 0.12 + (L - 0.24) * k / n, g = gAt(x);
        for (const s of [-1, 1]) cyl(out, x, s * (hy - 0.1), g - 0.5, D - 0.02, 0.11, 0.11, 8, 'ptwood', 'dock.post');
        box(out, x - 0.08, x + 0.08, -hy - 0.06, hy + 0.06, D - 0.4, D - 0.26, 'ptwood', 'dock.cap');
        if (k < n) { const x2 = -h + 0.12 + (L - 0.24) * (k + 1) / n, gz = Math.max(g, gAt(x2)) + 0.2;
          if (D - 0.5 - gz > 0.6) for (const s of [-1, 1]) { stick(out, [x, s * hy, gz], [x2, s * hy, D - 0.45], 0.05, 'ptwood', 'dock.brace'); stick(out, [x2, s * hy, gz], [x, s * hy, D - 0.45], 0.05, 'ptwood', 'dock.brace'); } } }
      box(out, h - 0.02, h + 0.06, -hy - 0.04, hy + 0.04, D - 0.34, D + 0.02, 'ptwood', 'dock.bumper');
      if (o.ladder) { const lx = h - 0.45, ly = hy + 0.06, g = gAt(h); for (const dx of [-0.21, 0.21]) stick(out, [lx + dx, ly, D + 0.8], [lx + dx, ly + 0.04, g + 0.1], 0.035, 'galv', 'dock.ladder');
        for (let z = D - 0.3; z > g + 0.25; z -= 0.3) box(out, lx - 0.21, lx + 0.21, ly - 0.015, ly + 0.03, z - 0.02, z + 0.02, 'galv', 'dock.rung'); }
      for (let c = 0; c < o.cleats; c++) { const x = h - 0.5 - c * 2.6, s = c % 2 ? -1 : 1; box(out, x - 0.16, x + 0.16, s * (hy - 0.14) - 0.04, s * (hy - 0.14) + 0.04, D, D + 0.07, 'galv', 'dock.cleat'); }
      for (let c = 0; c < o.chairs; c++) adirondack(out, h - 0.9, (c - (o.chairs - 1) / 2) * 0.75, D, o.chairMat, 'dock.chair' + c);
      turn(out, 0, o.yaw, 0, 0);
      return { faces: out, mats: mats(['wood', 'ptwood', 'galv', o.chairMat]), anchors: { deck: D, len: L, width: o.width, end: [h, 0, D], shore: [-h, 0, D], ladder: o.ladder ? [h - 0.45, hy + 0.06] : null,
        note: 'walkable deck, dry at the spring high; the ladder reaches the mud; boats tie at the cleats' } }; },
    extra: (s) => ({ dock: { deckZ: s.deck, lenM: s.len, dryAtSpringHighM: +(s.deck - 2.2).toFixed(2), fishing: 'from the end on the flood: smelt, eel, rock crab (stream.nmc_north_creek)', boats: 'a dory or a canoe ties at the cleats; at the ebb it lies on the mud' } }),
    notes: 'Heights above mean sea level: stand it at z 0 at the dock\u2019s middle. North bank docks point south (yaw -90), south bank docks north (yaw 90).',
  };

  /* THE GULLY STEPS (structure.nmc_gully_steps) — the weather face's way down: a timber flight on two stringers down the broken bluff
     into the cove, treads of pressure-treated plank, a rope rail on posts each side, a landing at the head. The flight runs from local
     x 0 (the head, z = rise) to x = run (the foot, z 0). Stand it at the foot's ground, turned by o.yaw. */
  PIECES.gullySteps = {
    id: 'structure.nmc_gully_steps', name: 'The gully steps (timber, down the bluff into the cove)', defType: 'SetPieceDef', scene: 'keyscene.nmc_weather_face',
    layer: 'raised', walk: 'stairs', mount: 'slope', footprints: [],
    defaults: { run: 7.0, rise: 6.4, width: 1.1, yaw: 0, landing: 1.2 },
    build(o) { const out = [], n = Math.max(2, Math.round(o.rise / 0.22)), rz = o.rise / n, go = o.run / n, hw = o.width / 2, R = o.rise, LD = o.landing;
      box(out, -LD, 0, -hw - 0.05, hw + 0.05, R - 0.12, R, 'ptwood', 'steps.landing'); for (const s of [-1, 1]) cyl(out, -LD + 0.1, s * hw, R - 1.4, R - 0.12, 0.08, 0.08, 6, 'ptwood', 'steps.post');
      for (let k = 0; k < n; k++) { const x0 = k * go, z = R - (k + 1) * rz; box(out, x0, x0 + go + 0.04, -hw, hw, z - 0.05, z, 'ptwood', 'steps.tread'); }
      for (const s of [-1, 1]) { stick(out, [0, s * (hw + 0.04), R - 0.1], [o.run, s * (hw + 0.04), -0.1], 0.07, 'ptwood', 'steps.stringer');
        const m = Math.max(2, Math.round(o.run / 1.6)); let prev = null;
        for (let k = 0; k <= m; k++) { const x = -LD + 0.1 + (o.run + LD - 0.2) * k / m, zg = x < 0 ? R : R * (1 - x / o.run), top = zg + 0.95; cyl(out, x, s * (hw + 0.1), zg - 0.3, top, 0.045, 0.045, 6, 'ptwood', 'steps.railPost');
          if (prev) stick(out, prev, [x, s * (hw + 0.1), top - 0.05], 0.022, 'rope', 'steps.rope'); prev = [x, s * (hw + 0.1), top - 0.05]; } }
      turn(out, 0, o.yaw, 0, 0);
      return { faces: out, mats: mats(['ptwood', 'rope']), anchors: { head: [0, 0, R], foot: [o.run, 0, 0], steps: n, riser: +rz.toFixed(3), going: +go.toFixed(3), note: 'stairs from the bluff\u2019s top to the ledges; the rope rails hold' } }; },
    extra: (s) => ({ steps: { rise: s.rise, run: s.run, tide: 'the ledges below are bared from about -1.4 on the ebb; the steps are the only way back up (the game warns on the flood)' } }),
    notes: 'Stand it at the foot\u2019s ground; its head meets the bluff\u2019s top.',
  };

  // =====================================================================================================================
  //  THE CAMERA, THE CELL, THE LIGHT (the St Peters kit's)
  // =====================================================================================================================
  const MODELS = new Map();
  function spec(key, o) { const P = PIECES[key]; if (!P) throw new Error('NmcSetPieces: no piece "' + key + '"'); return Object.assign({}, P.defaults, o || {}); }
  function geomKey(key, o) { const s = spec(key, o), g = {}; for (const k of Object.keys(PIECES[key].defaults)) g[k] = s[k]; return key + '|' + JSON.stringify(g); }
  function built(key, o) { const gk = geomKey(key, o); let B = MODELS.get(gk); if (B) return B;
    const s = spec(key, o), b = PIECES[key].build(s), mdl = LT().model(b.faces, b.mats, { aoRes: 12, sunRes: 24 });
    B = { key, s, b, mdl }; MODELS.set(gk, B); if (MODELS.size > 64) MODELS.delete(MODELS.keys().next().value); return B; }
  function basis(dir) { const a = ((dir | 0) % 8) * Math.PI / 4; return { ct: Math.cos(a), st: Math.sin(a), se: SE, ce: CE, S: PPU }; }
  function projRaw(p, B) { const xr = p[0] * B.ct - p[1] * B.st, yr = p[0] * B.st + p[1] * B.ct; return [xr * B.S, -(yr * B.se + p[2] * B.ce) * B.S, yr * B.ce - p[2] * B.se]; }
  function cellOf(B0, dir) { const B = basis(dir); let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
    for (const f of B0.b.faces) for (const p of f.v) { const q = projRaw(p, B); if (q[0] < x0) x0 = q[0]; if (q[0] > x1) x1 = q[0]; if (q[1] < y0) y0 = q[1]; if (q[1] > y1) y1 = q[1]; }
    const pad = 3, ox = Math.ceil(-x0) + pad, oy = Math.ceil(-y0) + pad; return { w: Math.ceil(x1 - x0) + 2 * pad + 1, h: Math.ceil(y1 - y0) + 2 * pad + 1, px: ox, py: oy, C: Object.assign({}, B, { ox, oy }) }; }
  function frame(key, dir, o) { const B0 = built(key, o), c = cellOf(B0, dir), fr = LT().frame(B0.mdl, c.C, c.w, c.h); fr.cell = c; fr.piece = key; fr.dir = dir | 0; fr.built = B0; if (!fr.C) fr.C = c.C; return fr; }
  function skyOf(o) { return LT().skyOf(o || {}); }
  function relight(fr, sky, o) { o = o || {}; const lit = o.lit != null ? +o.lit : 0; return LT().relight(fr, sky || skyOf(o), { outline: !!o.outline, lights: o.lights || [], emit: () => lit }); }
  function render(key, dir, o) { o = o || {}; const fr = frame(key, dir, o); return { w: fr.W, h: fr.H, px: fr.cell.px, py: fr.cell.py, rgba: relight(fr, o.sky || skyOf(o), o), frame: fr }; }
  function castShadow(fr, sky) { return LT().castShadow(fr, sky, { z: 0 }); }
  function view(fr, ch, sky) { return LT().view(fr, ch, sky); }
  const NAMES = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  function facings() { const out = []; for (let d = 0; d < 8; d++) { const B = basis(d), wx = -B.st, wy = B.ct, deg = (Math.atan2(wx, wy) / D2R + 360) % 360;
      out.push({ dir: d, showFaces: NAMES[Math.round(deg / 45) % 8], bearingDeg: +deg.toFixed(1), cameraSees: wy < -0.5 ? 'the show face' : wy < 0.2 ? 'the show face, edge-on or at a diagonal' : 'its back' }); } return out; }
  function anchors(key, dir, o) { const B0 = built(key, o), c = cellOf(B0, dir), toPx = (p) => { const q = projRaw(p, c.C); return [+(c.px + q[0]).toFixed(1), +(c.py + q[1]).toFixed(1)]; };
    const a = JSON.parse(JSON.stringify(B0.b.anchors || {})); a.px = {}; for (const k of Object.keys(a)) { const v = a[k]; if (Array.isArray(v) && v.length === 3 && typeof v[0] === 'number') a.px[k] = toPx(v); }
    if (a.words) for (const w of a.words) { if (w.corners) w.cornersPx = w.corners.map(toPx); }
    return a; }
  function footprint(key) { return (PIECES[key].footprints || []).map(q => q.map(p => p.slice())); }
  function gameplay(key, o) {
    const P = PIECES[key], s = spec(key, o), B0 = built(key, o);
    let zmax = 0, x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9; for (const f of B0.b.faces) for (const p of f.v) { zmax = Math.max(zmax, p[2]); x0 = Math.min(x0, p[0]); x1 = Math.max(x1, p[0]); y0 = Math.min(y0, p[1]); y1 = Math.max(y1, p[1]); }
    const cells = []; for (let d = 0; d < 8; d++) { const c = cellOf(B0, d); cells.push({ dir: d, w: c.w, h: c.h, pivot: [c.px, c.py] }); }
    const r = (v) => +v.toFixed(3);
    return { schema: 'hidden-harbours/nmc-set-piece@1', generatedBy: 'NmcSetPieces ' + VERSION, key, id: P.id, name: P.name, defType: P.defType, scene: P.scene,
      units: 'metres, the piece\u2019s own frame: +x right, +y the show face, +z up from its ground point (the pivot)',
      bounds: { x: [r(x0), r(x1)], y: [r(y0), r(y1)], z: [0, r(zmax)] }, layer: P.layer, walk: P.walk, mount: P.mount,
      colliders: footprint(key).map(pts => ({ type: 'polygon', blocks: 'walk', pts })), facings: facings(), cells, anchors: anchors(key, 4, o), options: s,
      light: /lamp/.test(JSON.stringify(B0.b.faces.map(f => f.em))) ? { emitters: ['lamp', 'win'], when: 'o.lit, from the game\u2019s lamp rule (dusk to dawn; the office while open)' } : null, notes: P.notes, ...(P.extra ? P.extra(s) : {}) };
  }
  // a piece painted pixel by pixel (the Falls' water): { w, h, px, py, rgba, Z }
  function paint(key, o) { const P = PIECES[key]; if (!P || !P.paint) throw new Error('NmcSetPieces: ' + key + ' has no painter'); return P.paint(spec(key, o)); }
  const API = { VERSION, PPU, RAMPS, PIECES, KEYS: Object.keys(PIECES), CRANE, LOADS, CRANE_STATES, craneState, craneIK, craneTipZ, spec, built, basis, projRaw, cellOf, frame, relight, render, castShadow, view, facings, anchors, footprint, gameplay, skyOf, NAMES, paint, fallsZ, lipOff };
  root.NmcSetPieces = API;
  if (typeof module !== 'undefined' && module.exports) module.exports = API;
})(typeof globalThis !== 'undefined' ? globalThis : this);
