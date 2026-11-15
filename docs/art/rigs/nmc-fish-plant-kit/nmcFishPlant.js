/* NINE MILE CREEK FISH PLANT KIT (globalThis.NmcFishPlant), 1.0.0, 2026-11-14.
   The truck-fed plant at site A on the south farm, drawn to docs/design/nmc-fish-plant-key-scene-brief.md and
   docs/design/nmc-fish-plant-truck-court.md: a 48 x 28 m one-storey shed (1,344 m2), its east loading face, the
   dock apron with the two truck standing positions, the 12 m gate, and the court's small pieces.

   Built the way the Nine Mile Creek set pieces are (nmc-set-pieces-kit/nmcSetPieces.js, the same API and the same
   sidecar schema): every piece is a face model lit by CoastalPass.light. No dither, no keyline. Colour is ramps only;
   every ramp shared with that kit carries that kit's values. Load weatherSky.js and coastalPass.js first (this kit
   reads the set pieces kit's lib/ copies, pinned in sources.json). Plain script.

   CAMERA 32 px = 1 m, 40 deg; dir 0-7 in 45 deg steps; local +Y is the show face (dir 4: it faces S, toward the camera).
   PIVOT the projection of the local origin, the piece's ground point.

   THE SITE FRAME. The brief measures in u = world x + 241 (east), v = world y + 40 (north). The plant stands at dir 4,
   so its local frame is the brief's turned half a circle about the pivot: local x = -(u - pivot u), local y = -(v - pivot v).
   Local +Y (the show face) is the SOUTH wall, with the public and the staff doors; local -X is the EAST loading face.
   Every sidecar carries its anchors in the local frame, in the brief's (u, v) and in world metres at site A.

   WORDS ARE DATA: every board the game letters is blank, tagged 'words.<id>', its corners in anchors().words. The
   business has no name yet (the brief leaves it open). EMITTERS: faces with em set ('win', 'lamp') glow with o.lit (0-1); the roof lights ('rooflight') glow at o.roofLit of it. */
(function (root) {
  'use strict';
  const VERSION = '1.0.0', PPU = 32, D2R = Math.PI / 180, SE = Math.sin(40 * D2R), CE = Math.cos(40 * D2R);
  const LT = () => { const c = root.CoastalPass; if (!c || !c.light) throw new Error('NmcFishPlant needs coastalPass.js (CoastalPass.light) loaded first'); return c.light; };
  // ---- ramps, dark -> light, six steps. Shared names carry the set pieces kit's values; two are this kit's own.
  const RAMPS = {
    siding: ['#5c615e', '#7a7f7a', '#979b95', '#b3b6ae', '#cbccc2', '#dedfd4'],     // white-painted corrugated steel, chalked
    trim: ['#565b58', '#737872', '#90948d', '#adb0a7', '#c7c9bf', '#dcddd2'],
    alum: ['#474d50', '#5d6468', '#767d80', '#8f9699', '#a8aeb0', '#bfc4c5'],
    glass: ['#0f191e', '#15232a', '#1d2f38', '#293e48', '#394f5a', '#4e656e'],
    wood: ['#2c2823', '#3d3730', '#524a40', '#6a6153', '#837966', '#9d937e'],
    iron: ['#19262b', '#26383d', '#3a4d50', '#516566', '#687d79', '#899790'],
    rust: ['#2b1c15', '#43271a', '#5d331f', '#774325', '#90562e', '#a66a3c'],
    tyre: ['#111213', '#191b1d', '#222527', '#2c2f32', '#373b3e', '#43474a'],
    conc: ['#51524c', '#686962', '#7f8077', '#96978c', '#acada0', '#bfbfb2'],
    galv: ['#4b5155', '#61686c', '#7a8185', '#94999d', '#adb2b5', '#c4c8ca'],
    yellow: ['#473710', '#685115', '#8c6d1b', '#ad8925', '#c9a334', '#dcb94f'],
    tote: ['#0d2136', '#142f4c', '#1d3f65', '#28517f', '#35659a', '#4b7db4'],       // fish totes, blue HDPE
    propane: ['#5b605f', '#777c7a', '#939794', '#aeb1ad', '#c6c8c3', '#d9dad5'],    // insulated tubs, grey-white
    red: ['#3a1b19', '#53251f', '#6e3226', '#894130', '#a2553d', '#b96b51'],        // red oxide roof paint, faded
    board: ['#55503e', '#6d674f', '#877f62', '#a09776', '#b9ae8c', '#cfc3a2'],
    lampW: ['#5c5a4a', '#8e8a6c', '#bcb690', '#ddd7b0', '#f2edcf', '#fffbea'],
    shake: ['#3e3a33', '#524d44', '#686256', '#7e7768', '#948c7b', '#a9a08d'],      // cedar shakes gone silver
    navy: ['#0f1a2a', '#16253a', '#1f324d', '#2a4262', '#375478', '#48688e'],
    chalk: ['#141816', '#1b201d', '#232925', '#2c332e', '#363e38', '#424b44'],      // the dark inside an open door
    lineY: ['#6a5412', '#8a6c17', '#a9851e', '#c49d29', '#d8b23d', '#e6c457'],
    cast: ['#16181a', '#1e2124', '#272b2e', '#31363a', '#3c4246', '#495055'],
    orange: ['#5b2a0f', '#7d3a13', '#a14f1b', '#c16827', '#d88338', '#e69e52'],
    // this kit's own: fibreglass roof lights gone straw-green, and lino for the cutaway's office floors
    skylite: ['#4f5a47', '#69755b', '#849071', '#9fab89', '#bac4a2', '#d2d9bc'],
    lino: ['#4d4a3c', '#646050', '#7c7765', '#948e7a', '#aba58f', '#bfb9a3'],
  };
  const mats = (keys) => { const M = {}; for (const k of keys) M[k] = { ramp: RAMPS[k].slice() }; return M; };
  const allMats = (faces) => mats([...new Set(faces.map(f => f.mat))]);
  const fr1 = (x, p) => ((x / p) % 1 + 1) % 1;
  const hash2 = (a, b) => { let h = Math.imul(a + 7, 374761393) ^ Math.imul(b + 3, 668265263); h = Math.imul(h ^ (h >>> 13), 1274126177); return (h ^ (h >>> 16)) >>> 0; };
  // ---- geometry helpers (the set pieces kit's, verbatim in behaviour) --------------------------------------------------
  function F(out, v, mat, b, tag, em, tx) { const f = { v, mat, b: b || 0, db: 0, tag: tag || null, em: em || null }; if (tx) { f.uv = tx.uv; f.tex = tx.tex; } out.push(f); }
  const TEX = {
    ribs: (u) => fr1(u, 0.16) < 0.45 ? 0 : -1,
    boards: (u, v) => fr1(v, 0.2) < 0.16 ? -1 : 0,
    louvre: (u, v) => fr1(v, 0.09) < 0.28 ? -1 : 0,
    grate: (u, v) => (Math.floor(u / 0.05) + Math.floor(v / 0.05)) & 1 ? -1 : 0,
    shakes: (u, v) => { const r = Math.floor(v / 0.2), o = ((r * 0.618034) % 1) * 0.16; return fr1(v, 0.2) < 0.15 || fr1(u + o, 0.16) < 0.1 ? -1 : 0; },
    // corrugated wall sheets, 0.9 m cover, a lap line each sheet and one sheet in nine a shade off (a replaced sheet)
    sheet: (u, v) => { const c = Math.floor(u / 0.9); if (fr1(u, 0.9) < 0.07) return -1; if (fr1(u, 0.3) < 0.1) return -1; return hash2(c, 11) % 17 === 0 ? -1 : 0; },
    // the roof: standing seams at 0.6 m running down the slope, sheets weathered unevenly, rust creeping up from the eave
    seam: (u, v) => { if (fr1(u, 0.6) < 0.09) return -1; const c = Math.floor(u / 0.6), h = hash2(c, 5) % 29; return h < 2 ? -1 : 0; },
    // concrete block, 0.2 m courses, 0.4 m blocks, broken joints
    block: (u, v) => { const row = Math.floor(v / 0.2); return fr1(v, 0.2) < 0.14 || fr1(u + (row & 1) * 0.2, 0.4) < 0.07 ? -1 : 0; },
    // a roller door's slats
    slats: (u, v) => fr1(v, 0.1) < 0.3 ? -1 : 0,
    // cast slab, a joint every 3 m, a stain here and there
    slab: (u, v) => { if (fr1(u, 3) < 0.02 || fr1(v, 3) < 0.02) return -1; const h = hash2(Math.floor(u / 0.75), Math.floor(v / 0.75)) % 37; return h === 0 ? -1 : 0; },
    chequer: (u, v) => (Math.floor((u + v) / 0.07) + Math.floor((u - v) / 0.07)) & 1 ? 0 : -1,
  };
  // walls take their lines from the height, so the courses run level round a building
  function FTz(out, v, mat, b, tag, name, em) { F(out, v, mat, b, tag, em || null, { uv: v.map(p => [p[0] + p[1], p[2]]), tex: TEX[name] }); }
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
  function boxT(out, x0, x1, y0, y1, z0, z1, mat, tag, tex, topTex) {
    FT(out, [[x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1]], mat, -0.15, tag, tex); FT(out, [[x1, y1, z0], [x0, y1, z0], [x0, y1, z1], [x1, y1, z1]], mat, 0.15, tag, tex);
    FT(out, [[x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1]], mat, 0, tag, tex); FT(out, [[x0, y1, z0], [x0, y0, z0], [x0, y0, z1], [x0, y1, z1]], mat, -0.3, tag, tex);
    FT(out, [[x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]], mat, 0.1, tag, topTex || tex); }
  function stick(out, A, B, w, mat, tag, em) {
    const d = [B[0] - A[0], B[1] - A[1], B[2] - A[2]], L = Math.hypot(d[0], d[1], d[2]) || 1, u = [d[0] / L, d[1] / L, d[2] / L];
    const ref = Math.abs(u[2]) < 0.9 ? [0, 0, 1] : [1, 0, 0], cr = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    let p = cr(u, ref); const pl = Math.hypot(p[0], p[1], p[2]); p = p.map(v => v / pl * w / 2); let q = cr(u, p); const ql = Math.hypot(q[0], q[1], q[2]); q = q.map(v => v / ql * w / 2);
    const C = (P, sp, sq) => [P[0] + p[0] * sp + q[0] * sq, P[1] + p[1] * sp + q[1] * sq, P[2] + p[2] * sp + q[2] * sq], R = [[1, 1], [-1, 1], [-1, -1], [1, -1]];
    for (let k = 0; k < 4; k++) { const a = R[k], b = R[(k + 1) % 4]; F(out, [C(A, a[0], a[1]), C(A, b[0], b[1]), C(B, b[0], b[1]), C(B, a[0], a[1])], mat, [0, -0.15, -0.3, 0.1][k], tag, em); }
    F(out, R.map(r => C(B, r[0], r[1])), mat, 0.1, tag, em); F(out, R.slice().reverse().map(r => C(A, r[0], r[1])), mat, -0.2, tag, em);
  }
  function cyl(out, cx, cy, z0, z1, r0, r1, n, mat, tag, noTop, em) {
    for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI, b = (k + 1.5) / n * 2 * Math.PI;
      F(out, [[cx + r0 * Math.cos(a), cy + r0 * Math.sin(a), z0], [cx + r0 * Math.cos(b), cy + r0 * Math.sin(b), z0], [cx + r1 * Math.cos(b), cy + r1 * Math.sin(b), z1], [cx + r1 * Math.cos(a), cy + r1 * Math.sin(a), z1]], mat, Math.sin(a) < 0 ? -0.1 : 0.05, tag, em); }
    if (!noTop) { const v = []; for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI; v.push([cx + r1 * Math.cos(a), cy + r1 * Math.sin(a), z1]); } F(out, v, mat, 0.1, tag, em); }
  }
  // a flat panel on the plane y = yp (facing +y when s = 1) or x = xp (facing +x when s = 1): decals, windows, doors
  function panelY(out, yp, s, x0, x1, z0, z1, mat, b, tag, tex, em) { const v = s > 0 ? [[x1, yp, z0], [x0, yp, z0], [x0, yp, z1], [x1, yp, z1]] : [[x0, yp, z0], [x1, yp, z0], [x1, yp, z1], [x0, yp, z1]];
    if (tex) FT(out, v, mat, b, tag, tex, em); else F(out, v, mat, b, tag, em); }
  function panelX(out, xp, s, y0, y1, z0, z1, mat, b, tag, tex, em) { const v = s > 0 ? [[xp, y0, z0], [xp, y1, z0], [xp, y1, z1], [xp, y0, z1]] : [[xp, y1, z0], [xp, y0, z0], [xp, y0, z1], [xp, y1, z1]];
    if (tex) FT(out, v, mat, b, tag, tex, em); else F(out, v, mat, b, tag, em); }
  function flat(out, x0, x1, y0, y1, z, mat, b, tag, tex, em) { const v = [[x0, y0, z], [x1, y0, z], [x1, y1, z], [x0, y1, z]]; if (tex) FT(out, v, mat, b, tag, tex, em); else F(out, v, mat, b, tag, em); }
  function winY(out, y, s, x0, x1, z0, z1, id, trim, mull) { panelY(out, y + s * 0.01, s, x0 - 0.08, x1 + 0.08, z0 - 0.1, z1 + 0.08, trim || 'trim', 0.1, 'window.trim');
    panelY(out, y + s * 0.02, s, x0, x1, z0, z1, 'glass', s > 0 ? 0.25 : 0.05, 'window.' + id, null, 'win');
    if (mull) for (let k = 1; k < mull; k++) { const x = x0 + (x1 - x0) * k / mull; panelY(out, y + s * 0.025, s, x - 0.03, x + 0.03, z0, z1, trim || 'trim', 0.1, 'window.mullion'); }
    box(out, x0 - 0.12, x1 + 0.12, s > 0 ? y : y - 0.1, s > 0 ? y + 0.1 : y, z0 - 0.14, z0 - 0.08, trim || 'trim', 'window.sill'); }
  // a wall lamp on a south (+y) wall, and one on the east (-x) wall: a steel hood, a lens that emits
  function lampS(out, x, y, z, tag) { box(out, x - 0.16, x + 0.16, y, y + 0.24, z, z + 0.1, 'iron', tag + '.hood'); box(out, x - 0.1, x + 0.1, y + 0.04, y + 0.2, z - 0.12, z, 'lampW', tag + '.lens', { em: 'lamp' }); }
  function lampE(out, x, y, z, tag) { box(out, x - 0.3, x, y - 0.2, y + 0.2, z, z + 0.1, 'iron', tag + '.hood'); box(out, x - 0.26, x - 0.04, y - 0.13, y + 0.13, z - 0.14, z, 'lampW', tag + '.lens', { em: 'lamp' }); }

  // =====================================================================================================================
  //  THE SITE (the brief's numbers, in one place)
  // =====================================================================================================================
  const SITE = {
    name: 'A', region: 'region.nine_mile_creek', plantDef: 'fishplant.nmc_industrial',
    frame: 'u = world x + 241 (east), v = world y + 40 (north), metres',
    origin: [-241, -40], site: { u: [-54, 26], v: [-30, 30] }, building: { u: [-48, 0], v: [-14, 14] }, court: { u: [0, 26], v: [-30, 30] },
    gate: { at: [26, 0], clear: [-6, 6] }, roadDatum: [51, 0], workApron: { u: [0.5, 3.0], v: [-4, 14] },
    truck: { length: 8.0, width: 2.5, rearOverhang: 2.0, note: 'the court study’s ASSUMED planning truck, not a Def' },
    bays: [{ id: 'I1', v: 0, rearBumper: [0.5, 0], rearAxle: [2.5, 0] }, { id: 'I2', v: 8, rearBumper: [0.5, 8], rearAxle: [2.5, 8] }],
  };
  // the brief's programme table (section 1), verbatim rectangles: [key, name, u0, u1, v0, v1, floor, access]
  const ZONES = [
    ['services', 'Plant services', -48, -38, -14, -8, 'conc', 'outside-only service door'],
    ['staffEntrance', 'Staff entrance, changing and wash', -38, -28, -14, -8, 'lino', 'staff'],
    ['staffRoom', 'Staff room', -28, -18, -14, -8, 'lino', 'staff'],
    ['office', 'Office', -18, -8, -14, -8, 'lino', 'public counter side'],
    ['lobby', 'Reception / delivery lobby', -8, 0, -14, -8, 'lino', 'public'],
    ['rawChill', 'Raw chilled buffer', -48, -34, -8, -2, 'alum', 'staff'],
    ['coldStore', 'Finished cold store / freezing cell', -48, -34, -2, 6, 'alum', 'staff'],
    ['lobster', 'Lobster handling cell', -48, -34, 6, 10, 'alum', 'staff'],
    ['freshLine', 'Preparation / fresh fillet line', -34, -18, -8, 0, 'conc', 'staff'],
    ['canning', 'Processing / canning line', -34, -18, 0, 10, 'conc', 'staff'],
    ['weighGrade', 'Weigh and grade', -18, -6, -8, 2, 'conc', 'delivery strip public, work area staff'],
    ['packing', 'Packing', -18, -6, 2, 10, 'conc', 'staff'],
    ['receivingAisle', 'Front receiving aisle', -6, 0, -8, -2, 'conc', 'public delivery route'],
    ['intake1', 'Intake I1', -6, 0, -2, 2, 'conc', 'delivery when the truck is stopped'],
    ['returns', 'Returns / container wash', -6, 0, 2, 6, 'conc', 'staff'],
    ['intake2', 'Intake I2', -6, 0, 6, 10, 'conc', 'staff; shared with dispatch at other times'],
    ['refrigeration', 'Refrigeration / plant equipment', -48, -34, 10, 14, 'conc', 'exterior service access only'],
    ['dryStore', 'Dry store', -34, -18, 10, 14, 'conc', 'staff'],
    ['dispatch', 'Finished dispatch staging', -18, -6, 10, 14, 'conc', 'staff'],
    ['outbound', 'Outbound dock', -6, 0, 10, 14, 'conc', 'staff'],
  ];
  // inside openings on the brief's named player route only (reception, office counter, receiving aisle, the delivery edge of
  // weigh and grade, I1) and the dispatch hand-carry aisle u [-3, -1.5]. PROPOSED, for the cutaway; every other threshold is closed.
  const OPENINGS = [
    { axis: 'u', c: -8, a: -11.6, b: -10.4, id: 'lobby-office (the counter)' }, { axis: 'v', c: -8, a: -3.6, b: -2.4, id: 'lobby-receiving aisle' },
    { axis: 'u', c: -6, a: -5.6, b: -4.4, id: 'receiving aisle-weigh and grade' }, { axis: 'v', c: -2, a: -3.6, b: -2.4, id: 'receiving aisle-I1' },
    { axis: 'u', c: -6, a: -1.5, b: 1.5, id: 'I1-weigh and grade (the delivery strip)' }, { axis: 'v', c: 10, a: -3, b: -1.5, id: 'outbound-I2 (the hand-carry aisle)' },
  ];
  // the outside doors, in the brief's frame: [id, wall, centre along the wall, width, height, kind]
  const DOORS = [
    { id: 'public', wall: 'S', c: -4, w: 1.2, h: 2.2, kind: 'half-glazed', zone: 'lobby', brief: 'public south door (-4,-14)' },
    { id: 'staff', wall: 'S', c: -33, w: 1.1, h: 2.2, kind: 'steel', zone: 'staffEntrance', brief: 'south threshold (-33,-14)' },
    { id: 'service', wall: 'S', c: -43, w: 2.0, h: 2.3, kind: 'steel double', zone: 'services', brief: 'outside-only service door' },
    { id: 'intake1', wall: 'E', c: 0, w: 4.0, h: 3.6, kind: 'roller', zone: 'intake1', brief: '4 m facade opening centred v 0' },
    { id: 'intake2', wall: 'E', c: 8, w: 4.0, h: 3.6, kind: 'roller', zone: 'intake2', brief: '4 m opening centred v 8' },
    { id: 'outbound', wall: 'E', c: 12, w: 2.5, h: 3.0, kind: 'roller', zone: 'outbound', brief: 'door centred v 12 (its width is this kit’s: 2.5 m)' },
    { id: 'refrigeration', wall: 'N', c: -41, w: 1.1, h: 2.2, kind: 'steel', zone: 'refrigeration', brief: 'exterior service access only' },
  ];

  // =====================================================================================================================
  //  THE PIECES
  // =====================================================================================================================
  const PIECES = {};

  /* THE PLANT (structure.nmc_fish_plant). A long low steel shed, 48 x 28 m. The main shed is white corrugated steel on a
     block upstand under a galvanised roof with fibreglass roof lights (they glow at dusk); along the south side an older
     shake-clad lean-to under red oxide roofing holds the lobby, the office and the staff rooms. The east gable is the
     loading face: two 4 m intake roller doors (I1, I2), the outbound door, rubber bumpers and a cantilevered canopy.
     On the roof: ridge vents, two retort stacks over the canning line, the condensers over the refrigeration room, and the
     ice plant's penthouse with its drop spout by the south-east corner, clear of the truck apron.
     o.doors 'closed' | 'open' (the three roller doors). o.cutaway true lifts the roof and cuts the walls at 1.1 m to show
     the brief's twenty rooms as floor and partitions (no machines: that is a later interior pass). */
  const PL = { X0: -24, X1: 24, Y0: -14, YM: 8, Y1: 14, ZE: 5.6, ZR: 8.0, YC: -3, ZL: 3.3, ZLT: 4.5, BASE: 0.9, CUT: 1.1 };
  PL.SL = (PL.ZR - PL.ZE) / (PL.YM - PL.YC);
  const roofZ = (y) => PL.ZR - PL.SL * Math.abs(y - PL.YC);
  const UX = (u) => -(u + 24);   // the brief's u to the plant's local x (the pivot is the building's centre, u -24, v 0); local y = -v
  PIECES.fishPlant = {
    id: 'structure.nmc_fish_plant', name: 'The fish plant', defType: 'SetPieceDef', scene: null,
    layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-24, -14], [24, -14], [24, 14], [-24, 14]]], defaults: { text: '', doors: 'closed', lights: true, cutaway: false },
    pivotBrief: [-24, 0], bake: { facings: [4], note: 'one facing: the plant never turns, and eight would not fit a 4096 sheet' },
    build(o) {
      const out = [], { X0, X1, Y0, YM, Y1, ZE, ZR, YC, ZL, ZLT, BASE, CUT } = PL, cut = !!o.cutaway, open = o.doors === 'open';
      const top = (z) => cut ? Math.min(z, CUT) : z, words = [];
      // ---- the slab and the block upstand
      box(out, X0 - 0.06, X1 + 0.06, Y0 - 0.06, Y1 + 0.06, -0.3, 0.1, 'conc', 'foundation', { noTop: true });
      // ---- the main shed's walls: north, the clerestory strip over the lean-to, the two gables
      const wallN = (z0, z1, mat, tex) => FTz(out, [[X0, Y0, z0], [X1, Y0, z0], [X1, Y0, z1], [X0, Y0, z1]], mat, -0.12, 'main.wall', tex);
      wallN(0, top(BASE), 'conc', 'block'); if (!cut) wallN(BASE, ZE, 'siding', 'sheet');
      if (!cut) FTz(out, [[X1, YM, ZLT - 0.2], [X0, YM, ZLT - 0.2], [X0, YM, ZE], [X1, YM, ZE]], 'siding', 0.1, 'main.wall', 'sheet');
      const gableE = (x, lo, mat, tex, yA, yB, zA, zB, peak) => { const v = [[x, yB, lo], [x, yA, lo], [x, yA, zA]]; if (peak) v.push([x, peak[0], peak[1]]); v.push([x, yB, zB]); FTz(out, v, mat, 0.02, 'main.gable', tex); };
      const gableW = (x, lo, mat, tex, yA, yB, zA, zB, peak) => { const v = [[x, yA, lo], [x, yB, lo], [x, yB, zB]]; if (peak) v.push([x, peak[0], peak[1]]); v.push([x, yA, zA]); FTz(out, v, mat, -0.2, 'main.gable', tex); };
      gableE(X0, 0, 'conc', 'block', Y0, YM, top(BASE), top(BASE)); gableW(X1, 0, 'conc', 'block', Y0, YM, top(BASE), top(BASE));
      if (!cut) { gableE(X0, BASE, 'siding', 'sheet', Y0, YM, ZE, ZE, [YC, ZR]); gableW(X1, BASE, 'siding', 'sheet', Y0, YM, ZE, ZE, [YC, ZR]); }
      // ---- the lean-to: shake walls on a low block course, south and both ends
      const lz = (y) => ZLT - (ZLT - ZL) * (y - YM) / (Y1 - YM);
      FTz(out, [[X1, Y1, 0], [X0, Y1, 0], [X0, Y1, top(0.5)], [X1, Y1, top(0.5)]], 'conc', 0.1, 'lean.wall', 'block');
      FTz(out, [[X1, Y1, 0.5], [X0, Y1, 0.5], [X0, Y1, top(ZL)], [X1, Y1, top(ZL)]], 'shake', 0.1, 'lean.wall', 'shakes');
      FTz(out, [[X0, Y1, 0], [X0, YM, 0], [X0, YM, top(ZLT)], [X0, Y1, top(ZL)]], 'shake', 0.02, 'lean.end', 'shakes');
      FTz(out, [[X1, YM, 0], [X1, Y1, 0], [X1, Y1, top(ZL)], [X1, YM, top(ZLT)]], 'shake', -0.2, 'lean.end', 'shakes');
      if (!cut) for (const x of [X0, X1]) for (const y of [Y0, YM, Y1]) box(out, x - 0.08, x + 0.08, y - 0.08, y + 0.08, 0, y === Y1 ? ZL : y === YM ? ZLT : ZE, 'trim', 'corner');

      // ---- THE SOUTH FACE (the show face): the lean-to's doors and windows, west to east in the brief's u
      const yS = Y1, dS = DOORS.filter(d => d.wall === 'S');
      for (const d of dS) { const xa = UX(d.c + d.w / 2), xb = UX(d.c - d.w / 2), h = top(d.h), mat = d.id === 'service' ? 'alum' : 'navy';
        panelY(out, yS + 0.01, 1, xa - 0.12, xb + 0.12, 0, top(d.h + 0.1), 'trim', 0.1, 'door.' + d.id + '.frame'); panelY(out, yS + 0.02, 1, xa, xb, 0.02, h, mat, 0.2, 'door.' + d.id + '.leaf');
        if (cut) continue;
        if (d.id === 'public') panelY(out, yS + 0.03, 1, xa + 0.2, xb - 0.2, 1.15, 2.0, 'glass', 0.25, 'door.public.window', null, 'win');
        if (d.id === 'service') panelY(out, yS + 0.03, 1, (xa + xb) / 2 - 0.02, (xa + xb) / 2 + 0.02, 0.02, h, 'iron', 0, 'door.service.meeting');
        box(out, xa - 0.5, xb + 0.5, yS, yS + 1.2, 0, 0.1, 'conc', 'door.' + d.id + '.step');
        lampS(out, (xa + xb) / 2, yS, d.h + 0.32, 'lamp.' + d.id);
      }
      if (!cut) {
        // the public door's little porch roof, the lettering board over it (blank), the notice board beside it (blank)
        box(out, UX(-2.9), UX(-5.1), yS, yS + 1.25, 2.66, 2.74, 'red', 'porch.roof'); for (const u of [-3.0, -5.0]) stick(out, [UX(u), yS + 0.04, 2.0], [UX(u), yS + 1.15, 2.66], 0.06, 'iron', 'porch.bracket');
        const ySg = yS + 0.56, FC = [[UX(-7.6), ySg + 0.07, 3.36], [UX(-0.6), ySg + 0.07, 3.36], [UX(-0.6), ySg + 0.07, 3.96], [UX(-7.6), ySg + 0.07, 3.96]];
        box(out, UX(-0.5), UX(-7.7), ySg, ySg + 0.06, 3.28, 4.04, 'navy', 'sign.frame'); F(out, FC.slice(), 'board', 0.25, 'words.nmc_fish_plant');
        for (const u of [-7.2, -4.1, -1.0]) { stick(out, [UX(u), ySg - 0.02, 3.2], [UX(u), ySg - 0.02, 4.0], 0.06, 'iron', 'sign.post'); stick(out, [UX(u), ySg - 0.02, 3.9], [UX(u), ySg - 0.9, 3.42], 0.05, 'iron', 'sign.stay'); }
        words.push({ id: 'words.nmc_fish_plant', text: o.text || '', corners: FC.slice().reverse(), note: 'the board standing on the lean-to\u2019s eave over the public door; the business has no name yet, blank in pictures' });
        const NB = [[UX(-1.9), yS + 0.05, 1.2], [UX(-0.9), yS + 0.05, 1.2], [UX(-0.9), yS + 0.05, 2.0], [UX(-1.9), yS + 0.05, 2.0]];
        box(out, UX(-0.84), UX(-1.96), yS, yS + 0.04, 1.14, 2.06, 'wood', 'notice.frame'); F(out, NB.slice(), 'board', 0.2, 'words.nmc_fish_plant_notice');
        words.push({ id: 'words.nmc_fish_plant_notice', text: '', corners: NB.slice().reverse(), note: 'the delivery hours board by the door; the game’s to letter' });
        // windows: the lobby, three on the office, two on the staff room, a high one on the changing room, a louvre on services
        const win = (u0, u1, z0, z1, id, mull) => winY(out, yS, 1, UX(u1), UX(u0), z0, z1, id, 'trim', mull);
        win(-7.5, -5.7, 1.0, 2.15, 'lobby', 2); win(-17.2, -15.2, 1.0, 2.15, 'officeW', 2); win(-14, -12, 1.0, 2.15, 'officeM', 2); win(-10.8, -8.8, 1.0, 2.15, 'officeE', 2);
        win(-27, -25, 1.0, 2.15, 'staffW', 2); win(-22, -20, 1.0, 2.15, 'staffE', 2); win(-37, -35.4, 1.65, 2.2, 'changing'); win(-30.6, -29.4, 1.65, 2.2, 'wash');
        panelY(out, yS + 0.02, 1, UX(-45.4), UX(-47), 1.5, 2.3, 'alum', 0, 'services.louvre', 'louvre');
        // the eave: fascia, gutter, three downpipes
        const ezS = ZL - 0.4 * (ZLT - ZL) / (Y1 - YM);
        box(out, X0 - 0.3, X1 + 0.3, yS + 0.4, yS + 0.52, ezS - 0.14, ezS, 'galv', 'gutter');
        for (const u of [-47.4, -24, -0.4]) { stick(out, [UX(u), yS + 0.46, ezS - 0.14], [UX(u), yS + 0.08, ezS - 0.5], 0.08, 'galv', 'downpipe'); stick(out, [UX(u), yS + 0.08, ezS - 0.5], [UX(u), yS + 0.08, 0.1], 0.08, 'galv', 'downpipe'); }
        // a bench by the staff door, a galvanised bin by services, a stack of three fish boxes left by the lobby
        box(out, UX(-29.4), UX(-31.2), yS + 0.25, yS + 0.62, 0.4, 0.46, 'wood', 'bench'); for (const u of [-29.6, -31.0]) box(out, UX(u) - 0.04, UX(u) + 0.04, yS + 0.28, yS + 0.6, 0, 0.4, 'iron', 'bench.leg');
        cyl(out, UX(-40.6), yS + 0.5, 0, 0.75, 0.28, 0.3, 10, 'galv', 'bin'); cyl(out, UX(-40.6), yS + 0.5, 0.75, 0.8, 0.32, 0.32, 10, 'galv', 'bin.lid');
        for (let k = 0; k < 3; k++) box(out, UX(-9.2) - 0.4, UX(-9.2) + 0.4, yS + 0.2, yS + 0.72, k * 0.24, k * 0.24 + 0.22, k === 1 ? 'orange' : 'tote', 'boxes');
        // the clerestory over the lean-to: twelve lights along the main shed's south wall
        for (let k = 0; k < 12; k++) { const u = -46 + k * 4; winY(out, YM, 1, UX(u + 1.3), UX(u - 1.3), 4.72, 5.3, 'clerestory' + k, 'trim', 2); }
      }

      // ---- THE EAST FACE (local -x): the loading doors, bumpers, the canopy, the wall lamps, the gable board
      const xE = X0;
      for (const d of DOORS.filter(q => q.wall === 'E')) { const ya = -(d.c + d.w / 2), yb = -(d.c - d.w / 2), h = top(d.h);
        for (const y of [ya - 0.16, yb]) box(out, xE - 0.12, xE, y, y + 0.16, 0, top(d.h + 0.14), 'yellow', 'dock.' + d.id + '.post');
        if (cut) { panelX(out, xE - 0.02, -1, ya, yb, 0.02, 0.06, 'yellow', 0, 'dock.' + d.id + '.sill'); continue; }
        box(out, xE - 0.12, xE, ya - 0.16, yb + 0.16, d.h, d.h + 0.14, 'yellow', 'dock.' + d.id + '.head');
        if (open) { panelX(out, xE - 0.02, -1, ya, yb, 0.02, d.h - 0.55, 'chalk', -0.2, 'dock.' + d.id + '.inside'); panelX(out, xE - 0.04, -1, ya, yb, d.h - 0.55, d.h, 'navy', 0.1, 'dock.' + d.id + '.door', 'slats'); }
        else panelX(out, xE - 0.03, -1, ya, yb, 0.02, d.h, 'navy', 0.1, 'dock.' + d.id + '.door', 'slats');
        if (d.id !== 'outbound') for (const y of [ya - 0.62, yb + 0.22]) box(out, xE - 0.3, xE, y, y + 0.4, 0.55, 1.25, 'tyre', 'dock.' + d.id + '.bumper');
        lampE(out, xE, (ya + yb) / 2, 3.98, 'lamp.' + d.id);
      }
      if (!cut) {
        // the canopy over the work apron: brief v [-3, 14], 3.0 m out (the work apron\u2019s depth), steel sheet on cantilever brackets
        const cy0 = -14, cy1 = 3, cz = 4.3;
        FT(out, [[xE - 3.0, cy0, cz + 0.04], [xE, cy0, cz + 0.3], [xE, cy1, cz + 0.3], [xE - 3.0, cy1, cz + 0.04]], 'galv', 0.1, 'canopy.roof', 'seam');
        F(out, [[xE - 3.0, cy1, cz - 0.1], [xE - 3.0, cy0, cz - 0.1], [xE - 3.0, cy0, cz + 0.04], [xE - 3.0, cy1, cz + 0.04]], 'navy', 0.02, 'canopy.fascia');
        F(out, [[xE, cy1, cz - 0.1], [xE - 3.0, cy1, cz - 0.1], [xE - 3.0, cy1, cz + 0.04], [xE, cy1, cz + 0.3]], 'navy', 0.15, 'canopy.fascia');
        F(out, [[xE - 3.0, cy0, cz - 0.1], [xE, cy0, cz - 0.1], [xE, cy0, cz + 0.3], [xE - 3.0, cy0, cz + 0.04]], 'navy', -0.15, 'canopy.fascia');
        for (const y of [cy0 + 0.1, -10.4, -4, 2.9]) stick(out, [xE, y, 3.75], [xE - 2.8, y, cz - 0.06], 0.08, 'iron', 'canopy.bracket');
        // the gable's lettering board (blank) and the floodlight at the peak
        const GB = [[xE - 0.07, 1.2, 5.75], [xE - 0.07, -7.2, 5.75], [xE - 0.07, -7.2, 6.75], [xE - 0.07, 1.2, 6.75]];
        box(out, xE - 0.06, xE, -7.3, 1.3, 5.68, 6.82, 'navy', 'gable.sign.frame'); F(out, GB.slice(), 'board', 0.05, 'words.nmc_fish_plant_gable');
        words.push({ id: 'words.nmc_fish_plant_gable', text: o.text || '', corners: GB.slice(), note: 'the board on the east gable, toward the gate and Route 91; blank in pictures' });
        lampE(out, xE, YC, 7.15, 'lamp.gable');
        // a louvre high in the west gable
        panelX(out, X1 + 0.02, 1, YC - 0.8, YC + 0.8, 6.2, 7.2, 'alum', 0, 'west.louvre', 'louvre');
        // the north wall: the refrigeration room's door and louvres
        const dn = DOORS.find(q => q.id === 'refrigeration'), xa = UX(dn.c + dn.w / 2), xb = UX(dn.c - dn.w / 2);
        panelY(out, Y0 - 0.01, -1, xa - 0.1, xb + 0.1, 0, dn.h + 0.1, 'trim', -0.1, 'door.refrigeration.frame'); panelY(out, Y0 - 0.02, -1, xa, xb, 0.02, dn.h, 'alum', -0.05, 'door.refrigeration.leaf');
        panelY(out, Y0 - 0.02, -1, UX(-44.4), UX(-47), 1.2, 2.6, 'alum', -0.1, 'refrigeration.louvre', 'louvre'); panelY(out, Y0 - 0.02, -1, UX(-36), UX(-38.6), 1.2, 2.6, 'alum', -0.1, 'refrigeration.louvre', 'louvre');
      } else {
        const dn = DOORS.find(q => q.id === 'refrigeration'); panelY(out, Y0 - 0.02, -1, UX(dn.c + dn.w / 2), UX(dn.c - dn.w / 2), 0.02, CUT, 'alum', -0.05, 'door.refrigeration.leaf');
      }

      if (!cut) {
        // ---- THE ROOFS: the main shed's two slopes, the lean-to's one, the ridge
        const ov = 0.5, xa = X0 - 0.4, xb = X1 + 0.4, ezN = ZE - ov * PL.SL;
        FT(out, [[xa, Y0 - ov, ezN], [xb, Y0 - ov, ezN], [xb, YC, ZR], [xa, YC, ZR]], 'galv', -0.05, 'main.roof', 'seam');
        FT(out, [[xb, YM + 0.3, ZE - 0.3 * PL.SL], [xa, YM + 0.3, ZE - 0.3 * PL.SL], [xa, YC, ZR], [xb, YC, ZR]], 'galv', 0.12, 'main.roof', 'seam');
        const lsl = (ZLT - ZL) / (Y1 - YM), ezS = ZL - 0.4 * lsl;
        FT(out, [[xb, Y1 + 0.4, ezS], [xa, Y1 + 0.4, ezS], [xa, YM, ZLT], [xb, YM, ZLT]], 'red', 0.1, 'lean.roof', 'seam');
        panelY(out, Y1 + 0.4, 1, xa, xb, ezS - 0.16, ezS, 'trim', 0.1, 'lean.fascia'); panelY(out, Y0 - ov, -1, xa, xb, ezN - 0.16, ezN, 'trim', -0.1, 'main.fascia');
        // barge boards on both gables (main and lean-to)
        for (const x of [xa, xb]) { const e = x === xa, bar = (ya, za, yb, zb) => F(out, e ? [[x, yb, zb - 0.2], [x, ya, za - 0.2], [x, ya, za], [x, yb, zb]] : [[x, ya, za - 0.2], [x, yb, zb - 0.2], [x, yb, zb], [x, ya, za]], 'trim', e ? 0.05 : -0.15, 'barge');
          bar(Y0 - ov, ezN, YC, ZR); bar(YC, ZR, YM + 0.3, ZE - 0.3 * PL.SL); bar(YM, ZLT, Y1 + 0.4, ezS); }
        box(out, xa - 0.02, xb + 0.02, YC - 0.28, YC + 0.28, ZR - 0.05, ZR + 0.1, 'galv', 'ridge.cap');
        for (const u of [-42, -33, -24, -15, -6]) { boxT(out, UX(u) - 0.9, UX(u) + 0.9, YC - 0.5, YC + 0.5, ZR + 0.08, ZR + 0.5, 'alum', 'ridge.vent', 'louvre'); box(out, UX(u) - 1.0, UX(u) + 1.0, YC - 0.6, YC + 0.6, ZR + 0.5, ZR + 0.56, 'galv', 'ridge.vent.cap'); }
        // on the lean-to: the staff room\u2019s stove pipe and two soil vents
        { const lzr = (y) => ZLT - lsl * (y - YM); cyl(out, UX(-23), 10, lzr(10) - 0.05, lzr(10) + 0.2, 0.3, 0.3, 8, 'galv', 'stove.flashing'); cyl(out, UX(-23), 10, lzr(10) + 0.2, 5.5, 0.15, 0.15, 8, 'cast', 'stove.pipe', true); cyl(out, UX(-23), 10, 5.5, 5.68, 0.3, 0.05, 8, 'galv', 'stove.cap');
          for (const u of [-34.5, -31]) cyl(out, UX(u), 9.4, lzr(9.4) - 0.05, lzr(9.4) + 0.6, 0.07, 0.07, 6, 'cast', 'soil.vent'); }
        // fibreglass roof lights, six a slope, lifted a finger off the sheet; they glow when the plant is lit
        for (const u of [-44.5, -37.5, -30.5, -20.5, -13.5, -9.5]) { const x0 = UX(u) - 0.6, x1 = UX(u) + 0.6, d = 0.035;
          let ya = 6.6, yb = -1.4; F(out, [[x1, ya, roofZ(ya) + d], [x0, ya, roofZ(ya) + d], [x0, yb, roofZ(yb) + d], [x1, yb, roofZ(yb) + d]], 'skylite', 0.15, 'rooflight', 'rooflight');
          if (u < -36) continue;   // the condensers stand on the north slope's west end
          ya = -12.6; yb = -4.6; F(out, [[x0, ya, roofZ(ya) + d], [x1, ya, roofZ(ya) + d], [x1, yb, roofZ(yb) + d], [x0, yb, roofZ(yb) + d]], 'skylite', 0, 'rooflight', 'rooflight'); }
        // rust at the south eave where the sheets lap, and three patched sheets
        for (const u of [-45.4, -39.7, -27.1, -18.7, -11.5, -3.1]) { const x = UX(u), ya = YM + 0.28, yb = YM - 0.5 - (hash2(Math.round(u * 10), 3) % 5) * 0.22; for (const [dx, w, k] of [[-0.2, 0.05, 0.7], [0.02, 0.07, 0.4], [0.24, 0.04, 0.55]]) { const yc = ya - (ya - yb) * k; F(out, [[x + dx + w, ya, roofZ(ya) + 0.02], [x + dx - w, ya, roofZ(ya) + 0.02], [x + dx - w, yc, roofZ(yc) + 0.02], [x + dx + w, yc, roofZ(yc) + 0.02]], 'rust', -0.35, 'main.roof.rust'); } }
        for (const [u, ya, yb] of [[-25.5, 5.4, 2.2], [-4.6, -11.5, -8.5], [-34.3, -9.8, -7.2]]) { const x = UX(u), s = ya > YC ? 1 : -1; F(out, s > 0 ? [[x + 0.3, ya, roofZ(ya) + 0.02], [x - 0.3, ya, roofZ(ya) + 0.02], [x - 0.3, yb, roofZ(yb) + 0.02], [x + 0.3, yb, roofZ(yb) + 0.02]] : [[x - 0.3, ya, roofZ(ya) + 0.02], [x + 0.3, ya, roofZ(ya) + 0.02], [x + 0.3, yb, roofZ(yb) + 0.02], [x - 0.3, yb, roofZ(yb) + 0.02]], 'alum', 0.15, 'main.roof.patch'); }
        // the canning line's two retort stacks (brief u -30 and -26, v 5), guyed short, rain caps
        for (const u of [-30, -26]) { const x = UX(u), y = -5, zb = roofZ(y) - 0.1; cyl(out, x, y, zb, zb + 0.35, 0.42, 0.42, 10, 'galv', 'stack.flashing'); cyl(out, x, y, zb + 0.35, 10.1, 0.26, 0.26, 10, 'galv', 'stack', true); cyl(out, x, y, 9.2, 9.5, 0.275, 0.275, 10, 'rust', 'stack.band', true); cyl(out, x, y, 10.1, 10.2, 0.26, 0.26, 10, 'cast', 'stack.mouth'); cyl(out, x, y, 10.36, 10.56, 0.48, 0.06, 10, 'galv', 'stack.cap'); for (const a of [0.6, 2.7, 4.8]) stick(out, [x + 0.34 * Math.cos(a), y + 0.34 * Math.sin(a), 10.16], [x + 0.34 * Math.cos(a), y + 0.34 * Math.sin(a), 10.38], 0.04, 'iron', 'stack.cap.leg'); }
        // the condensers over the refrigeration room (brief u [-47, -36], v [9.2, 13.4]): a level steel deck, two units, four fans
        { const xw = UX(-47), xe = UX(-36), ya = -13.4, yb = -9.2, zd = roofZ(yb) + 0.12;
          box(out, xe, xw, ya, yb, zd - 0.1, zd, 'iron', 'condenser.deck'); for (const x of [xe + 0.1, xw - 0.1]) stick(out, [x, ya + 0.1, roofZ(ya + 0.1)], [x, ya + 0.1, zd - 0.1], 0.1, 'iron', 'condenser.leg');
          for (const [ua, ub] of [[-46.6, -42], [-41, -36.4]]) { const x0 = UX(ub), x1 = UX(ua); boxT(out, x0, x1, ya + 0.4, yb - 0.4, zd, zd + 1.3, 'alum', 'condenser.unit', 'louvre', 'ribs'); box(out, x0 - 0.04, x1 + 0.04, ya + 0.36, yb - 0.36, zd + 1.3, zd + 1.36, 'galv', 'condenser.top');
            for (const fx of [0.27, 0.73]) { const cx = x0 + (x1 - x0) * fx, cyy = (ya + yb) / 2; cyl(out, cx, cyy, zd + 1.36, zd + 1.5, 0.82, 0.82, 12, 'galv', 'condenser.fan.ring', true); cyl(out, cx, cyy, zd + 1.36, zd + 1.42, 0.74, 0.74, 12, 'cast', 'condenser.fan'); cyl(out, cx, cyy, zd + 1.42, zd + 1.47, 0.16, 0.16, 8, 'alum', 'condenser.fan.hub'); } }
          stick(out, [xe - 0.3, yb - 0.2, zd + 0.3], [xe - 0.3, yb + 1.6, roofZ(yb + 1.6) + 0.12], 0.12, 'rust', 'condenser.pipe'); stick(out, [xe - 0.55, yb - 0.2, zd + 0.3], [xe - 0.55, yb + 1.6, roofZ(yb + 1.6) + 0.12], 0.09, 'galv', 'condenser.pipe'); }
        // the ice plant's penthouse on the south slope by the east gable (brief u [-7, -2.6], v [-7.2, -3.6]) and its drop spout
        { const x0 = UX(-2.6), x1 = UX(-7), ya = 3.6, yb = 7.2, zb = roofZ(yb) - 0.1, zt = 8.3;
          boxT(out, x0, x1, ya, yb, zb, zt, 'siding', 'ice.house', 'sheet'); FT(out, [[x1 + 0.2, yb + 0.25, zt - 0.02], [x0 - 0.2, yb + 0.25, zt - 0.02], [x0 - 0.2, ya - 0.25, zt + 0.38], [x1 + 0.2, ya - 0.25, zt + 0.38]], 'red', 0.1, 'ice.house.roof', 'seam');
          F(out, [[x0 - 0.2, yb + 0.25, zt - 0.02], [x0 - 0.2, ya - 0.25, zt - 0.02], [x0 - 0.2, ya - 0.25, zt + 0.38]], 'trim', 0.02, 'ice.house.cheek'); F(out, [[x1 + 0.2, ya - 0.25, zt - 0.02], [x1 + 0.2, yb + 0.25, zt - 0.02], [x1 + 0.2, ya - 0.25, zt + 0.38]], 'trim', -0.2, 'ice.house.cheek');
          panelY(out, ya - 0.25, -1, x0 - 0.2, x1 + 0.2, zt - 0.02, zt + 0.38, 'trim', -0.1, 'ice.house.cheek');
          panelY(out, yb + 0.02, 1, x0 + 1.4, x1 - 1.4, zb + 0.9, zb + 1.7, 'alum', 0.1, 'ice.house.louvre', 'louvre');
          const yc = 5.2, A = [x0, yc, 6.95], B = [xE - 1.0, yc, 6.75], C = [xE - 1.6, yc, 3.3];
          stick(out, A, B, 0.5, 'galv', 'ice.chute'); stick(out, B, C, 0.46, 'galv', 'ice.chute'); stick(out, C, [C[0], yc, 2.25], 0.4, 'board', 'ice.chute.sock');
          stick(out, [xE, yc - 0.6, 3.9], [C[0], yc, 3.9], 0.06, 'iron', 'ice.chute.brace'); stick(out, [xE, yc + 0.6, 3.9], [C[0], yc, 3.9], 0.06, 'iron', 'ice.chute.brace'); stick(out, [xE, yc, 6.3], [B[0], yc, 6.5], 0.06, 'iron', 'ice.chute.brace'); }
      } else {
        // ---- THE CUTAWAY: the brief's rooms as floor, and partitions at the cut height with the route's openings left out
        for (const [key, , u0, u1, v0, v1, floor] of ZONES) flat(out, UX(u1), UX(u0), -v1, -v0, 0.11, floor, 0, 'floor.' + key, floor === 'conc' ? 'slab' : null);
        const segs = new Map();
        const add = (axis, c, a, b) => { const k = axis + c; if (!segs.has(k)) segs.set(k, { axis, c, iv: [] }); segs.get(k).iv.push([a, b]); };
        for (const [, , u0, u1, v0, v1] of ZONES) { if (u0 > -48) add('u', u0, v0, v1); if (u1 < 0) add('u', u1, v0, v1); if (v0 > -14) add('v', v0, u0, u1); if (v1 < 14) add('v', v1, u0, u1); }
        for (const s of segs.values()) { const iv = s.iv.sort((p, q) => p[0] - q[0]), merged = []; for (const q of iv) { const m = merged[merged.length - 1]; if (m && q[0] <= m[1]) m[1] = Math.max(m[1], q[1]); else merged.push(q.slice()); }
          const holes = OPENINGS.filter(h => h.axis === s.axis && h.c === s.c).sort((p, q) => p.a - q.a);
          for (const m of merged) { let a = m[0]; const runs = []; for (const h of holes) if (h.a >= m[0] && h.b <= m[1]) { runs.push([a, h.a]); a = h.b; } runs.push([a, m[1]]);
            for (const [p, q] of runs) if (q - p > 0.01) { if (s.axis === 'u') box(out, UX(s.c) - 0.07, UX(s.c) + 0.07, -q, -p, 0.11, CUT, 'trim', 'partition'); else box(out, UX(q), UX(p), -s.c - 0.07, -s.c + 0.07, 0.11, CUT, 'trim', 'partition'); } } }
        // the outer walls' cut tops, so the plan reads as a section
        for (const [x0, x1, y0, y1] of [[X0, X1, Y0, Y0 + 0.2], [X0, X1, Y1 - 0.2, Y1], [X0, X0 + 0.2, Y0, Y1], [X1 - 0.2, X1, Y0, Y1]]) flat(out, x0, x1, y0, y1, CUT, 'cast', 0, 'wall.cut');
      }

      // ---- the anchors: local metres. Doors stand 1.2 m outside their threshold, on the ground.
      const A = { door: [UX(-4), Y1 + 1.2, 0], staffDoor: [UX(-33), Y1 + 1.2, 0], serviceDoor: [UX(-43), Y1 + 1.2, 0], refrigerationDoor: [UX(-41), Y0 - 1.2, 0],
        counter: [UX(-9.5), 11, 0], intake1: [X0, 0, 0], intake2: [X0, -8, 0], outbound: [X0, -12, 0],
        truckStop1: [UX(0.5), 0, 0], truckStop2: [UX(0.5), -8, 0], truckAxle1: [UX(2.5), 0, 0], truckAxle2: [UX(2.5), -8, 0],
        ice: [X0 - 1.6, 5.2, 0], weigh: [UX(-8), 3, 0],
        lights: [
          { id: 'light.public_door', at: [UX(-4), Y1 + 0.12, 2.42], kind: 'door lamp' }, { id: 'light.staff_door', at: [UX(-33), Y1 + 0.12, 2.42], kind: 'door lamp' },
          { id: 'light.service_door', at: [UX(-43), Y1 + 0.12, 2.52], kind: 'door lamp' }, { id: 'light.intake1', at: [X0 - 0.15, 0, 3.86], kind: 'dock lamp, under the canopy' },
          { id: 'light.intake2', at: [X0 - 0.15, -8, 3.86], kind: 'dock lamp, under the canopy' }, { id: 'light.outbound', at: [X0 - 0.15, -12, 3.86], kind: 'dock lamp, under the canopy' },
          { id: 'light.gable_flood', at: [X0 - 0.15, YC, 7.03], kind: 'court floodlight at the east peak' }],
        words, note: 'truckStop is the rear bumper (brief u 0.5), truckAxle the rear axle (u 2.5); the truck’s nose points east, local -x' };
      if (cut) A.lights = [];
      return { faces: out, mats: allMats(out), anchors: A };
    },
    notes: 'Stand it at dir 4 with its pivot on the building’s centre, world (-265, -40): the show face is the south wall. The windows, the roof lights and the lamps glow with o.lit (the lamp rule: dusk to dawn, and while the plant works). The doors on the south wall are drawn shut; a walk-in interior is a later pass (ADR 0036) and o.cutaway is its floor plan, not its art.',
    extra(s) { return { programme: { source: 'docs/design/nmc-fish-plant-key-scene-brief.md section 1', frame: SITE.frame, grossM2: 1344, floors: 1,
        zones: ZONES.map(z => ({ key: z[0], name: z[1], u: [z[2], z[3]], v: [z[4], z[5]], m2: (z[3] - z[2]) * (z[5] - z[4]), access: z[7], local: { x: [UX(z[3]), UX(z[2])], y: [-z[5], -z[4]] } })),
        openings: OPENINGS.map(h => Object.assign({ status: 'PROPOSED by the kit from the brief’s named route' }, h)) },
      doors: DOORS.map(d => Object.assign({}, d, { local: d.wall === 'E' ? [PL.X0, -d.c, 0] : [UX(d.c), d.wall === 'S' ? PL.Y1 : PL.Y0, 0] })) }; },
  };

  /* THE DOCK APRON (structure.nmc_fish_plant_dock). The loading dock outside the east face: a cast slab over the brief's
     work apron and the two standing positions, each bay boxed in yellow for the 8 x 2.5 m planning truck with its rear
     bumper line at u 0.5, a chequer transfer plate across the 0.5 m bumper gap, and short wheel guides inside the work
     apron. Pivot: I1's threshold (brief u 0, v 0). o.plate is the transfer plate's height: the brief leaves the deck height
     to the chosen truck, so the default lies on the slab. */
  PIECES.plantDock = {
    id: 'structure.nmc_fish_plant_dock', name: 'The loading dock apron', defType: 'SetPieceDef', scene: null, layer: 'floor', walk: 'on', mount: 'ground',
    footprints: [], defaults: { plate: 0.03, guides: true, lines: true }, pivotBrief: [0, 0], bake: { facings: [4], note: 'paired with the plant’s east face' },
    build(o) { const out = [], X = (u) => -u, zs = 0.03, zl = 0.036, pz = Math.max(zs, +o.plate || zs);
      flat(out, X(9), X(0), -14.5, 4, zs, 'conc', 0, 'apron.slab', 'slab'); box(out, X(9), X(0), -14.5, 4, 0, zs, 'conc', 'apron.edge', { noTop: true });
      const line = (u0, u1, v0, v1, tag) => flat(out, X(u1), X(u0), -v1, -v0, zl, 'lineY', 0.1, tag);
      for (const b of SITE.bays) { const v = b.v;
        if (o.lines) { line(0.5, 8.5, v - 1.46, v - 1.34, 'bay.' + b.id + '.line'); line(0.5, 8.5, v + 1.34, v + 1.46, 'bay.' + b.id + '.line'); line(8.38, 8.5, v - 1.34, v + 1.34, 'bay.' + b.id + '.line'); line(0.44, 0.56, v - 1.34, v + 1.34, 'bay.' + b.id + '.stop'); }
        flat(out, X(0.62), X(-0.02), -(v + 1.1), -(v - 1.1), pz + 0.02, 'iron', 0.1, 'plate.' + b.id, 'chequer'); box(out, X(0.62), X(-0.02), -(v + 1.1), -(v - 1.1), zs, pz + 0.02, 'iron', 'plate.' + b.id + '.edge', { noTop: true });
        if (o.guides) for (const s of [-1, 1]) { const vv = v + s * 1.62; box(out, X(2.8), X(0.7), -(vv + 0.05), -(vv - 0.05), zs, 0.2, 'yellow', 'guide.' + b.id); } }
      if (o.lines) { line(0.5, 3.0, -4.06, -3.94, 'apron.keepclear'); line(2.94, 3.06, -4, 14, 'apron.keepclear'); line(0.5, 3.0, 13.94, 14.06, 'apron.keepclear'); }
      const A = { apron: [X(4.5), -5.25, zs], truckStop1: [X(0.5), 0, 0], truckAxle1: [X(2.5), 0, 0], truckNose1: [X(8.5), 0, 0], truckStop2: [X(0.5), -8, 0], truckAxle2: [X(2.5), -8, 0], truckNose2: [X(8.5), -8, 0],
        plate1: [X(0.3), 0, pz + 0.02], plate2: [X(0.3), -8, pz + 0.02], outboundThreshold: [0, -12, 0], spotter: [X(3.6), 5, 0],
        note: 'truckStop the rear bumper, truckAxle the rear axle, truckNose the front: the truck stands nose east (local -x). One truck in the court at a time; I2 serves intake and dispatch at different hours.' };
      return { faces: out, mats: allMats(out), anchors: A }; },
    notes: 'Lay it at dir 4 with its pivot on I1’s threshold, world (-241, -40), flush to the plant’s east wall. The yellow keep-clear line is the brief’s work apron, u [0.5, 3.0], v [-4, 14]: closed to walking while a truck moves.',
    extra(s) { return { bays: SITE.bays.map(b => ({ id: b.id, standing: { u: [0.5, 8.5], v: [b.v - 1.25, b.v + 1.25] }, rearBumper: b.rearBumper, rearAxle: b.rearAxle, heading: 'east (+u)' })), workApron: SITE.workApron, truck: SITE.truck, plate: { height: +(+s.plate).toFixed(3), status: 'OPEN: the brief leaves the deck height and liftgate to the chosen truck' } }; },
  };

  /* THE GATE (structure.nmc_fish_plant_gate). Twelve metres clear between its posts (brief u 26, v [-6, 6]): two 6 m chain-link
     leaves on steel posts, a 3 m fence return each side, a lamp on the north post and a blank board on the south return.
     o.open true swings both leaves 90 degrees into the court (west), clear of every swept path in the court study. */
  function chainLink(out, A, B, z0, z1, tag) { const L = Math.hypot(B[0] - A[0], B[1] - A[1]), ux = (B[0] - A[0]) / L, uy = (B[1] - A[1]) / L, P = (s, z) => [A[0] + ux * s, A[1] + uy * s, z], h = z1 - z0;
    for (const z of [z0, z1]) stick(out, P(0, z), P(L, z), 0.06, 'galv', tag + '.rail'); for (const s of [0, L / 2, L]) stick(out, P(s, z0), P(s, z1), 0.06, 'galv', tag + '.stile');
    const step = 0.36; for (let s = -h; s < L; s += step) { const a = Math.max(0, s), b = Math.min(L, s + h); if (b - a > 0.05) { stick(out, P(a, z0 + (a - s)), P(b, z0 + (b - s)), 0.034, 'alum', tag + '.mesh'); stick(out, P(a, z1 - (a - s)), P(b, z1 - (b - s)), 0.034, 'alum', tag + '.mesh'); } } }
  PIECES.plantGate = {
    id: 'structure.nmc_fish_plant_gate', name: 'The plant gate', defType: 'SetPieceDef', scene: null, layer: 'raised', walk: 'solid', mount: 'ground',
    defaults: { open: true, fence: 3, lights: true }, pivotBrief: [26, 0], bake: { facings: [4], variants: [{ open: false }], note: 'open and shut, one facing' },
    footprints(o) { const q = (x0, x1, y0, y1) => [[x0, y0], [x1, y0], [x1, y1], [x0, y1]], f = +o.fence || 0, out = [q(-0.1, 0.1, -6.2, -6), q(-0.1, 0.1, 6, 6.2)];
      if (f) out.push(q(-0.05, 0.05, -6.2 - f, -6.2), q(-0.05, 0.05, 6.2, 6.2 + f));
      if (o.open) out.push(q(0, 6, -6.05, -5.95), q(0, 6, 5.95, 6.05)); else out.push(q(-0.05, 0.05, -6, 6));
      return out; },
    build(o) { const out = [], f = +o.fence || 0, H = 2.0;
      for (const y of [-6.1, 6.1]) { box(out, -0.1, 0.1, y - 0.1, y + 0.1, 0, 2.5, 'galv', 'gate.post'); box(out, -0.13, 0.13, y - 0.13, y + 0.13, 2.5, 2.56, 'iron', 'gate.post.cap'); box(out, -0.3, 0.3, y - 0.3, y + 0.3, 0, 0.08, 'conc', 'gate.post.footing'); }
      // local +x is west (into the court), local +y is south. The north leaf hangs on y = -6, the south on y = +6.
      if (o.open) { chainLink(out, [0.08, -5.98, 0], [6.0, -5.98, 0], 0.12, H, 'gate.leaf.north'); chainLink(out, [0.08, 5.98, 0], [6.0, 5.98, 0], 0.12, H, 'gate.leaf.south'); for (const y of [-5.98, 5.98]) cyl(out, 5.9, y, 0, 0.1, 0.07, 0.07, 6, 'tyre', 'gate.wheel'); }
      else { chainLink(out, [0, -5.98, 0], [0, -0.03, 0], 0.12, H, 'gate.leaf.north'); chainLink(out, [0, 0.03, 0], [0, 5.98, 0], 0.12, H, 'gate.leaf.south'); box(out, -0.07, 0.07, -0.12, 0.12, 0.95, 1.12, 'iron', 'gate.lock'); }
      if (f) { chainLink(out, [0, -6.2 - f, 0], [0, -6.2, 0], 0.06, H, 'fence.north'); chainLink(out, [0, 6.2, 0], [0, 6.2 + f, 0], 0.06, H, 'fence.south'); for (const y of [-6.2 - f, 6.2 + f]) box(out, -0.05, 0.05, y - 0.05, y + 0.05, 0, H + 0.1, 'galv', 'fence.post'); }
      // the lamp on the north post: a swan neck toward the opening
      stick(out, [0, -6.1, 2.5], [0, -6.1, 4.6], 0.09, 'galv', 'gate.lamp.pole'); stick(out, [0, -6.1, 4.6], [0, -5.2, 4.75], 0.07, 'galv', 'gate.lamp.arm'); box(out, -0.16, 0.16, -5.45, -4.95, 4.66, 4.78, 'iron', 'gate.lamp.hood'); box(out, -0.11, 0.11, -5.38, -5.02, 4.56, 4.66, 'lampW', 'gate.lamp.lens', { em: 'lamp' });
      // the board on the south return, toward the road (east, local -x): blank
      const BC = [[-0.09, 8.9, 1.0], [-0.09, 6.7, 1.0], [-0.09, 6.7, 1.75], [-0.09, 8.9, 1.75]], words = [];
      if (f >= 3) { box(out, -0.08, -0.03, 6.62, 8.98, 0.94, 1.81, 'navy', 'gate.sign.frame'); F(out, BC.slice(), 'board', 0.05, 'words.nmc_fish_plant_gate'); words.push({ id: 'words.nmc_fish_plant_gate', text: '', corners: BC.slice(), note: 'the gate board, toward Route 91; the game’s to letter, blank in pictures' }); }
      return { faces: out, mats: allMats(out), anchors: { gate: [0, 0, 0], postNorth: [0, -6.1, 0], postSouth: [0, 6.1, 0], inside: [3, 0, 0], outside: [-3, 0, 0], clear: [12, 2.5],
        lights: [{ id: 'light.gate', at: [0, -5.2, 4.58], kind: 'gate lamp on the north post' }], words, state: o.open ? 'open' : 'shut', note: 'clear is [metres between the posts, post height]; the opening is brief v [-6, 6] at u 26' } }; },
    notes: 'Stand it at dir 4 with its pivot on the gate centre, world (-215, -40). Open, the leaves lie along the court’s v = +6 and v = -6 lines, 6 m into the court (brief u [20, 26]). The court study’s swept paths are unproven against the open leaves: check them in the scene’s vehicle sequence.',
  };

  /* THE COURT'S SMALL PIECES: a steel bollard, a precast kerb length, the condensing unit by the services door,
     a stack of insulated fish tubs or boxes on a pallet, and the yard light on its pole. */
  PIECES.courtBollard = { id: 'prop.nmc_plant_bollard', name: 'Court bollard', defType: 'SetPieceDef', scene: null, layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-0.16, -0.16], [0.16, -0.16], [0.16, 0.16], [-0.16, 0.16]]], defaults: { h: 1.05 },
    build(o) { const out = [], h = +o.h || 1.05; cyl(out, 0, 0, 0, 0.04, 0.2, 0.2, 10, 'conc', 'bollard.footing'); cyl(out, 0, 0, 0.04, h, 0.11, 0.11, 10, 'yellow', 'bollard', true); cyl(out, 0, 0, h - 0.34, h - 0.2, 0.116, 0.116, 10, 'tyre', 'bollard.band', true); cyl(out, 0, 0, h, h + 0.06, 0.11, 0.05, 10, 'yellow', 'bollard.cap');
      cyl(out, 0, 0, 0.04, 0.22, 0.118, 0.114, 10, 'rust', 'bollard.foot', true); return { faces: out, mats: allMats(out), anchors: { top: [0, 0, h + 0.06] } }; },
    notes: 'Concrete-filled steel pipe, chalked yellow, rust at the foot. Stand them at the plant’s corners, either side of each dock door clear of the bay lines, and along the court edge.' };
  PIECES.courtKerb = { id: 'prop.nmc_plant_kerb', name: 'Court kerb', defType: 'SetPieceDef', scene: null, layer: 'floor', walk: 'on', mount: 'ground',
    footprints: [], defaults: { len: 6, paint: false },
    build(o) { const out = [], h = (+o.len || 6) / 2, n = Math.max(1, Math.round(h * 2 / 1.5)); for (let k = 0; k < n; k++) { const x0 = -h + k * 2 * h / n + 0.012, x1 = -h + (k + 1) * 2 * h / n - 0.012; box(out, x0, x1, -0.13, 0.13, 0, 0.16, o.paint && (k & 1) ? 'yellow' : 'conc', 'kerb'); }
      return { faces: out, mats: allMats(out), anchors: { endA: [-h, 0, 0], endB: [h, 0, 0], len: [2 * h, 0.26] } }; },
    notes: 'Precast lengths, 1.5 m units, laid along the court’s edge (the surface itself is the ground lane’s). o.len is the run in metres; o.paint stripes alternate units yellow.' };
  PIECES.plantCondenser = { id: 'prop.nmc_plant_condenser', name: 'Condensing unit', defType: 'SetPieceDef', scene: null, layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-2.2, -0.75], [2.2, -0.75], [2.2, 0.75], [-2.2, 0.75]]], defaults: {},
    build(o) { const out = []; box(out, -2.2, 2.2, -0.75, 0.75, 0, 0.12, 'conc', 'condenser.pad'); boxT(out, -2.0, 0.9, -0.6, 0.6, 0.12, 1.75, 'alum', 'condenser.coil', 'louvre', 'ribs'); box(out, -2.04, 0.94, -0.64, 0.64, 1.75, 1.81, 'galv', 'condenser.top');
      for (const x of [-1.25, 0.15]) { cyl(out, x, 0, 1.81, 1.93, 0.56, 0.56, 12, 'galv', 'condenser.fan.ring', true); cyl(out, x, 0, 1.81, 1.86, 0.5, 0.5, 12, 'cast', 'condenser.fan'); cyl(out, x, 0, 1.86, 1.9, 0.12, 0.12, 8, 'alum', 'condenser.fan.hub'); }
      box(out, 1.0, 2.0, -0.5, 0.5, 0.12, 1.35, 'iron', 'compressor.case'); panelY(out, 0.51, 1, 1.12, 1.88, 0.5, 1.2, 'alum', 0.1, 'compressor.louvre', 'louvre'); box(out, 1.3, 1.7, 0.5, 0.58, 0.3, 0.42, 'rust', 'compressor.valve');
      stick(out, [1.5, -0.5, 1.0], [1.5, -0.95, 1.0], 0.1, 'rust', 'condenser.pipe'); stick(out, [1.75, -0.5, 0.7], [1.75, -0.95, 0.7], 0.07, 'galv', 'condenser.pipe');
      return { faces: out, mats: allMats(out), anchors: { service: [0, 1.6, 0], wall: [0, -0.95, 0] } }; },
    notes: 'Stand it with local -y to a wall (its pipes run that way), show face out. Suggested: outside the refrigeration room’s north wall, or wherever the scene lane has room; the plant’s own condensers are on its roof.' };
  PIECES.toteStack = { id: 'prop.nmc_plant_totes', name: 'Fish tubs and boxes', defType: 'SetPieceDef', scene: null, layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-1.3, -0.6], [1.3, -0.6], [1.3, 0.6], [-1.3, 0.6]]], defaults: { kind: 'tubs', high: 2 },
    build(o) { const out = [], n = Math.max(1, Math.min(4, o.high | 0 || 2));
      if (o.kind === 'boxes') { for (const px of [-0.65, 0.65]) { box(out, px - 0.6, px + 0.6, -0.5, 0.5, 0.1, 0.14, 'wood', 'pallet'); for (const y of [-0.45, 0, 0.45]) box(out, px - 0.6, px + 0.6, y - 0.05, y + 0.05, 0, 0.1, 'wood', 'pallet');
          const lv = px < 0 ? n * 2 : n * 2 - 1; for (let k = 0; k < lv; k++) for (let i = 0; i < 2; i++) for (let j = 0; j < 2; j++) { if (k === lv - 1 && px > 0 && i === 1 && j === 1) continue; const x0 = px - 0.58 + i * 0.59, y0 = -0.48 + j * 0.49, z0 = 0.14 + k * 0.24, m = (hash2(k * 4 + i * 2 + j, px < 0 ? 1 : 2) % 5); box(out, x0, x0 + 0.57, y0, y0 + 0.47, z0, z0 + 0.22, m === 0 ? 'orange' : m === 1 ? 'propane' : 'tote', 'box'); } }
      } else for (const [px, lv] of [[-0.65, n], [0.65, Math.max(1, n - 1)]]) for (let k = 0; k < lv; k++) { const z0 = k * 0.78, m = (k + (px < 0 ? 0 : 1)) & 1 ? 'tote' : 'propane';
          box(out, px - 0.6, px + 0.6, -0.5, 0.5, z0 + 0.1, z0 + 0.72, m, 'tub'); for (const x of [px - 0.52, px + 0.36]) box(out, x, x + 0.16, -0.5, 0.5, z0, z0 + 0.1, m, 'tub.foot'); box(out, px - 0.62, px + 0.62, -0.52, 0.52, z0 + 0.72, z0 + 0.78, k === lv - 1 ? 'trim' : m, 'tub.lid'); }
      return { faces: out, mats: allMats(out), anchors: { hand: [0, 0.9, 0], top: [-0.65, 0, o.kind === 'boxes' ? 0.14 + n * 0.48 : n * 0.78] } }; },
    notes: 'Set dressing only: it holds no stock and shows no fill. o.kind ‘tubs’ (insulated 1.2 x 1.0 m tubs, lidded) or ‘boxes’ (fish boxes on two pallets); o.high 1 to 4.' };
  PIECES.yardLight = { id: 'prop.nmc_plant_yard_light', name: 'Yard light', defType: 'SetPieceDef', scene: null, layer: 'raised', walk: 'solid', mount: 'ground',
    footprints: [[[-0.16, -0.16], [0.16, -0.16], [0.16, 0.16], [-0.16, 0.16]]], defaults: { h: 7.5, lights: true },
    build(o) { const out = [], h = +o.h || 7.5; cyl(out, 0, 0, 0, h, 0.14, 0.1, 8, 'wood', 'pole'); stick(out, [0, 0, h - 0.5], [0, 1.5, h - 0.2], 0.07, 'galv', 'pole.arm'); stick(out, [0, 0, h - 1.3], [0, 0.9, h - 0.38], 0.04, 'iron', 'pole.stay');
      box(out, -0.17, 0.17, 1.2, 1.95, h - 0.26, h - 0.12, 'alum', 'lamp.head'); box(out, -0.14, 0.14, 1.26, 1.89, h - 0.46, h - 0.26, 'lampW', 'lamp.lens', { em: 'lamp' }); box(out, -0.3, 0.3, -0.04, 0.04, h - 1.0, h - 0.9, 'wood', 'pole.crossarm');
      return { faces: out, mats: allMats(out), anchors: { light: [0, 1.58, h - 0.46], lights: [{ id: 'light.yard', at: [0, 1.58, h - 0.46], kind: 'yard light, mercury white' }] } }; },
    notes: 'A utility pole with a single yard lamp on a 1.5 m arm toward local +y. Suggested at the court’s north-east and south-east corners and by the staff parking, arms toward the court.' };

  // =====================================================================================================================
  //  THE CAMERA, THE CELL, THE LIGHT (the set pieces kit's)
  // =====================================================================================================================
  const MODELS = new Map();
  function spec(key, o) { const P = PIECES[key]; if (!P) throw new Error('NmcFishPlant: no piece "' + key + '"'); return Object.assign({}, P.defaults, o || {}); }
  function geomKey(key, o) { const s = spec(key, o), g = {}; for (const k of Object.keys(PIECES[key].defaults)) g[k] = s[k]; return key + '|' + JSON.stringify(g); }
  function built(key, o) { const gk = geomKey(key, o); let B = MODELS.get(gk); if (B) return B;
    const s = spec(key, o), b = PIECES[key].build(s), mdl = LT().model(b.faces, b.mats, { aoRes: 12, sunRes: 24 });
    B = { key, s, b, mdl }; MODELS.set(gk, B); if (MODELS.size > 32) MODELS.delete(MODELS.keys().next().value); return B; }
  function basis(dir) { const a = ((dir | 0) % 8) * Math.PI / 4; return { ct: Math.cos(a), st: Math.sin(a), se: SE, ce: CE, S: PPU }; }
  function projRaw(p, B) { const xr = p[0] * B.ct - p[1] * B.st, yr = p[0] * B.st + p[1] * B.ct; return [xr * B.S, -(yr * B.se + p[2] * B.ce) * B.S, yr * B.ce - p[2] * B.se]; }
  function cellOf(B0, dir) { const B = basis(dir); let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
    for (const f of B0.b.faces) for (const p of f.v) { const q = projRaw(p, B); if (q[0] < x0) x0 = q[0]; if (q[0] > x1) x1 = q[0]; if (q[1] < y0) y0 = q[1]; if (q[1] > y1) y1 = q[1]; }
    const pad = 3, ox = Math.ceil(-x0) + pad, oy = Math.ceil(-y0) + pad; return { w: Math.ceil(x1 - x0) + 2 * pad + 1, h: Math.ceil(y1 - y0) + 2 * pad + 1, px: ox, py: oy, C: Object.assign({}, B, { ox, oy }) }; }
  function frame(key, dir, o) { const B0 = built(key, o), c = cellOf(B0, dir), fr = LT().frame(B0.mdl, c.C, c.w, c.h); fr.cell = c; fr.piece = key; fr.dir = dir | 0; fr.built = B0; if (!fr.C) fr.C = c.C; return fr; }
  function skyOf(o) { return LT().skyOf(o || {}); }
  // the roof lights are their own emitter: they pass the work lights below, so they glow at o.roofLit (default 0.5) of o.lit
  function relight(fr, sky, o) { o = o || {}; const lit = o.lit != null ? +o.lit : 0, roof = lit * (o.roofLit != null ? +o.roofLit : 0.5); return LT().relight(fr, sky || skyOf(o), { outline: !!o.outline, lights: o.lights || [], emit: (name) => name === 'rooflight' ? roof : lit }); }
  function render(key, dir, o) { o = o || {}; const fr = frame(key, dir, o); return { w: fr.W, h: fr.H, px: fr.cell.px, py: fr.cell.py, rgba: relight(fr, o.sky || skyOf(o), o), frame: fr }; }
  function castShadow(fr, sky) { return LT().castShadow(fr, sky, { z: 0 }); }
  function view(fr, ch, sky) { return LT().view(fr, ch, sky); }
  const NAMES = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  function facings() { const out = []; for (let d = 0; d < 8; d++) { const B = basis(d), wx = -B.st, wy = B.ct, deg = (Math.atan2(wx, wy) / D2R + 360) % 360;
      out.push({ dir: d, showFaces: NAMES[Math.round(deg / 45) % 8], bearingDeg: +deg.toFixed(1), cameraSees: wy < -0.5 ? 'the show face' : wy < 0.2 ? 'the show face, edge-on or at a diagonal' : 'its back' }); } return out; }
  function anchors(key, dir, o) { const B0 = built(key, o), c = cellOf(B0, dir), toPx = (p) => { const q = projRaw(p, c.C); return [+(c.px + q[0]).toFixed(1), +(c.py + q[1]).toFixed(1)]; };
    const a = JSON.parse(JSON.stringify(B0.b.anchors || {})); a.px = {}; for (const k of Object.keys(a)) { const v = a[k]; if (Array.isArray(v) && v.length === 3 && typeof v[0] === 'number') a.px[k] = toPx(v); }
    if (a.words) for (const w of a.words) { if (w.corners) w.cornersPx = w.corners.map(toPx); }
    if (a.lights) for (const l of a.lights) l.atPx = toPx(l.at);
    return a; }
  function footprint(key, o) { const fp = PIECES[key].footprints, list = typeof fp === 'function' ? fp(spec(key, o)) : fp || []; return list.map(q => q.map(p => p.slice())); }
  // where a piece's anchors land at site A: it stands at dir 4 on its pivotBrief, so local (x, y) is brief (pu - x, pv - y)
  function siteOf(key, o) { const P = PIECES[key]; if (!P.pivotBrief) return null; const B0 = built(key, o), pu = P.pivotBrief[0], pv = P.pivotBrief[1], r = (v) => +v.toFixed(3);
    const one = (p) => ({ local: p.map(r), brief: [r(pu - p[0]), r(pv - p[1])], world: [r(SITE.origin[0] + pu - p[0]), r(SITE.origin[1] + pv - p[1])], z: r(p[2]) }), A = B0.b.anchors || {}, at = {};
    for (const k of Object.keys(A)) { const v = A[k]; if (Array.isArray(v) && v.length === 3 && typeof v[0] === 'number') at[k] = one(v); }
    for (const l of A.lights || []) at[l.id] = one(l.at);
    return { site: SITE.name, frame: SITE.frame, dir: 4, pivot: { brief: P.pivotBrief.slice(), world: [SITE.origin[0] + pu, SITE.origin[1] + pv] },
      rule: 'brief u = pivot u - local x, brief v = pivot v - local y; world x = u - 241, world y = v - 40', anchors: at,
      colliders: footprint(key, o).map(pts => pts.map(p => [r(SITE.origin[0] + pu - p[0]), r(SITE.origin[1] + pv - p[1])])) }; }
  function gameplay(key, o) {
    const P = PIECES[key], s = spec(key, o), B0 = built(key, o);
    let zmax = 0, x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9; for (const f of B0.b.faces) for (const p of f.v) { zmax = Math.max(zmax, p[2]); x0 = Math.min(x0, p[0]); x1 = Math.max(x1, p[0]); y0 = Math.min(y0, p[1]); y1 = Math.max(y1, p[1]); }
    const cells = []; for (let d = 0; d < 8; d++) { const c = cellOf(B0, d); cells.push({ dir: d, w: c.w, h: c.h, pivot: [c.px, c.py] }); }
    const r = (v) => +v.toFixed(3), site = siteOf(key, o);
    return { schema: 'hidden-harbours/nmc-set-piece@1', generatedBy: 'NmcFishPlant ' + VERSION, key, id: P.id, name: P.name, defType: P.defType, scene: P.scene,
      units: 'metres, the piece’s own frame: +x right, +y the show face, +z up from its ground point (the pivot)',
      bounds: { x: [r(x0), r(x1)], y: [r(y0), r(y1)], z: [0, r(zmax)] }, layer: P.layer, walk: P.walk, mount: P.mount,
      colliders: footprint(key, o).map(pts => ({ type: 'polygon', blocks: 'walk', pts })), facings: facings(), cells, anchors: anchors(key, 4, o), options: s,
      light: B0.b.faces.some(f => f.em) ? { emitters: [...new Set(B0.b.faces.map(f => f.em).filter(Boolean))].sort(), when: 'o.lit, from the game’s lamp rule (dusk to dawn; the plant while it works)' } : null, notes: P.notes,
      ...(P.bake ? { bake: P.bake } : {}), ...(site ? { site } : {}), ...(P.extra ? P.extra(s) : {}) };
  }
  const API = { VERSION, PPU, RAMPS, TEX, PIECES, KEYS: Object.keys(PIECES), SITE, ZONES, OPENINGS, DOORS, PL, roofZ, spec, built, basis, projRaw, cellOf, frame, relight, render, castShadow, view, facings, anchors, footprint, siteOf, gameplay, skyOf, NAMES,
    helpers: { F, FT, FTz, box, boxT, stick, cyl, panelX, panelY, flat, mats, allMats } };
  root.NmcFishPlant = API;
  if (typeof module !== 'undefined' && module.exports) module.exports = API;
})(typeof globalThis !== 'undefined' ? globalThis : this);
