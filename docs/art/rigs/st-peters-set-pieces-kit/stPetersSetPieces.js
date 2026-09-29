/* Hidden Harbours — ST PETERS SET PIECES (globalThis.StPetersSetPieces). The key scenes' own pieces: each exists at one
   place on the island and nowhere else. 2026-09-27, for keyscene.stp_landing and keyscene.stp_cannery.

   LIGHT. Every piece is a face model lit by CoastalPass.light (coastalPass.js, the houses kit's, byte-identical in lib/):
   sky x visibility, sun x shadow map, bounce, the band ladder, the sky's grade; rain, fog, snow by material. No dither and
   no keyline (ADR 0031); {outline:true} is the A/B arm. Colour is ramps only. Load coastalPass.js (and weatherSky.js for
   skies) before this file. Plain script, no build step. Node: vm context, the same load order.

   CAMERA. The shared 3/4 camera: 32 px = 1 m across, 40 deg. dir 0-7 turns the piece in 45 deg steps. Local +Y is the
   piece's show face. facings(key) gives, measured by projecting the show face's normal, the world direction it faces at
   each dir (dir 4: S, toward the camera). Hand the game the direction; the index rides beside it.

   PIVOT. A cell's pivot is the projection of the local origin: the piece's ground point. Blit at screen(ground) - pivot.

   WORDS ARE DATA. Every board the game letters is blank here, tagged 'words.<id>', and anchors().words gives its four
   corners in cell px per dir, its string id and its default string. No glyph is ever baked.

   TIME. Anything that moves takes o.t (game hours) and o.seed and nothing else: the vane's heading, the smoke. The rates
   are tunables in TUNABLES and in each sidecar.                                                                          */
(function (root) {
  'use strict';
  const VERSION = '1.0.0', PPU = 32, D2R = Math.PI / 180, SE = Math.sin(40 * D2R), CE = Math.cos(40 * D2R);
  const LT = () => { const c = root.CoastalPass; if (!c || !c.light) throw new Error('StPetersSetPieces needs coastalPass.js (CoastalPass.light) loaded first'); return c.light; };
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  // ---- ramps (dark -> light, six steps). Names that CoastalPass.light knows keep its weather (wood, brick, glass, trim, stone, iron)
  const RAMPS = {
    wood: ['#2c2823', '#3d3730', '#524a40', '#6a6153', '#837966', '#9d937e'],       // weathered spruce, silvered
    tar: ['#131716', '#1b211f', '#252c29', '#303834', '#3d4641', '#4e5751'],        // tarred pile below the splash line
    trim: ['#565b58', '#737872', '#90948d', '#adb0a7', '#c7c9bf', '#dcddd2'],       // white paint, a season old
    mark: ['#101413', '#171c1a', '#1f2523', '#28302d', '#323b38', '#3e4844'],       // black paint
    red: ['#3a1b19', '#53251f', '#6e3226', '#894130', '#a2553d', '#b96b51'],        // oxide red
    brick: ['#39211f', '#502b26', '#6a382f', '#84483a', '#9c5c48', '#b3735c'],      // the boiler's brick
    rust: ['#2b1c15', '#43271a', '#5d331f', '#774325', '#90562e', '#a66a3c'],
    iron: ['#19262b', '#26383d', '#3a4d50', '#516566', '#687d79', '#899790'],      // CoastalPass iron
    tin: ['#4a5459', '#5d686d', '#737f84', '#8b979b', '#a3aeb1', '#bcc5c7'],        // corrugated, grey where the paint went
    tinOld: ['#35393a', '#44494a', '#555a5a', '#686c6a', '#7b7e7a', '#8f918b'],     // corrugated, weathered past grey
    glass: ['#0f191e', '#15232a', '#1d2f38', '#293e48', '#394f5a', '#4e656e'],
    weed: ['#1d281b', '#293822', '#37482a', '#475a32', '#596d3b', '#6e8047'],       // wrack and green weed on a pile foot
    barnacle: ['#4a4a44', '#5f5f57', '#77766c', '#8f8d81', '#a7a596', '#bdbaa9'],
    board: ['#55503e', '#6d674f', '#877f62', '#a09776', '#b9ae8c', '#cfc3a2'],       // a lettered board's cream ground
    copper: ['#1e3a33', '#2a4c42', '#386052', '#4a7563', '#5f8a75', '#7aa08b'],     // verdigris
    soot: ['#141516', '#1c1e1f', '#262829', '#303335', '#3c3f41', '#4a4d4f'],
    cinder: ['#2a2521', '#38312b', '#473f36', '#584e42', '#6a5f50', '#7d7160'],     // the rail bed: cinders gone to dirt
    rope: ['#3f3424', '#56472f', '#6e5c3c', '#87714b', '#9e865b', '#b39b6e'],
    plank: ['#33291f', '#473829', '#5d4a35', '#745d42', '#8b7151', '#a18663'],      // the trolley's oak deck
    grass: ['#233a24', '#2f4a2c', '#3c5b33', '#4b6c3b', '#5c7d45', '#6f8e50'],
    lamp: ['#5c3a1a', '#93602b', '#c88b3d', '#e6b35a', '#f6d68b', '#fff0c2'],       // the lantern's glass (an emitter)
  };
  const TUNABLES = {
    vane: { stepsPerTurn: 16, lagMinutes: 0, note: 'heading = the wind it points into, from the game wind model; 16 headings baked' },
    smoke: { rise_mps: 0.9, drift_per_wind: 2.6, puffs: 14, life_h: 0.012, note: 'restored cannery only; a runtime overlay, a function of game time, wind and seed' },
  };
  const mats = (keys) => { const M = {}; for (const k of keys) M[k] = { ramp: RAMPS[k].slice() }; return M; };
  // ---- geometry: faces {v:[[x,y,z]..], mat, b, db, tag, em} in local metres, z up, +Y the show face ---------------------------
  function F(out, v, mat, b, tag, em, tx) { const f = { v, mat, b: b || 0, db: 0, tag: tag || null, em: em || null }; if (tx) { f.uv = tx.uv; f.tex = tx.tex; } out.push(f); }
  // band-bias patterns in a face's own metres: brick courses, corrugated ribs, clapboard
  const TEX = {
    brick: (u, v) => { const c = Math.floor(v / 0.3), f = v / 0.3 - c, j = ((u + (c % 2) * 0.23) / 0.46) % 1; return f < 0.2 ? -1 : (j < 0.07 ? -1 : 0); },
    ribs: (u) => ((u / 0.16) % 1 + 1) % 1 < 0.45 ? 0 : -1,
    boards: (u, v) => ((v / 0.2) % 1 + 1) % 1 < 0.16 ? -1 : 0,
  };
  // uv for a quad: u along the first edge, v along the last; tex by name
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
  // a box turned about z by a (radians) and centred (cx, cy)
  function obox(out, cx, cy, hx, hy, z0, z1, a, mat, tag) {
    const c = Math.cos(a), s = Math.sin(a), P = (x, y, z) => [cx + x * c - y * s, cy + x * s + y * c, z];
    const q = [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]];
    for (let k = 0; k < 4; k++) { const A = q[k], B = q[(k + 1) % 4]; F(out, [P(A[0], A[1], z0), P(B[0], B[1], z0), P(B[0], B[1], z1), P(A[0], A[1], z1)], mat, k === 0 ? -0.15 : k === 2 ? 0.15 : k === 1 ? 0 : -0.3, tag); }
    F(out, q.map(p => P(p[0], p[1], z1)), mat, 0.1, tag);
  }
  // a stick between two points, square section w
  function stick(out, A, B, w, mat, tag) {
    const d = [B[0] - A[0], B[1] - A[1], B[2] - A[2]], L = Math.hypot(d[0], d[1], d[2]) || 1, u = [d[0] / L, d[1] / L, d[2] / L];
    const ref = Math.abs(u[2]) < 0.9 ? [0, 0, 1] : [1, 0, 0], cr = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    let p = cr(u, ref); const pl = Math.hypot(p[0], p[1], p[2]); p = p.map(v => v / pl * w / 2); let q = cr(u, p); const ql = Math.hypot(q[0], q[1], q[2]); q = q.map(v => v / ql * w / 2);
    const C = (P, sp, sq) => [P[0] + p[0] * sp + q[0] * sq, P[1] + p[1] * sp + q[1] * sq, P[2] + p[2] * sp + q[2] * sq], R = [[1, 1], [-1, 1], [-1, -1], [1, -1]];
    for (let k = 0; k < 4; k++) { const a = R[k], b = R[(k + 1) % 4]; F(out, [C(A, a[0], a[1]), C(A, b[0], b[1]), C(B, b[0], b[1]), C(B, a[0], a[1])], mat, [0, -0.15, -0.3, 0.1][k], tag); }
    F(out, R.map(r => C(B, r[0], r[1])), mat, 0.1, tag);
  }
  function cyl(out, cx, cy, z0, z1, r0, r1, n, mat, tag, noTop) {
    for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI, b = (k + 1.5) / n * 2 * Math.PI;
      F(out, [[cx + r0 * Math.cos(a), cy + r0 * Math.sin(a), z0], [cx + r0 * Math.cos(b), cy + r0 * Math.sin(b), z0], [cx + r1 * Math.cos(b), cy + r1 * Math.sin(b), z1], [cx + r1 * Math.cos(a), cy + r1 * Math.sin(a), z1]], mat, Math.sin(a) < 0 ? -0.1 : 0.05, tag); }
    if (!noTop) { const v = []; for (let k = 0; k < n; k++) { const a = (k + 0.5) / n * 2 * Math.PI; v.push([cx + r1 * Math.cos(a), cy + r1 * Math.sin(a), z1]); } F(out, v, mat, 0.1, tag); }
  }
  function flat(out, pts, z, mat, tag, b) { F(out, pts.map(p => [p[0], p[1], z]), mat, b == null ? -0.2 : b, tag); }
  const hash = (a, b, s) => { let h = Math.imul(a | 0, 374761393) + Math.imul(b | 0, 668265263) + Math.imul(s | 0, 1440670441) | 0; h ^= h >>> 13; h = Math.imul(h, 1103515245) | 0; h ^= h >>> 16; return (h >>> 0) / 4294967296; };

  // =====================================================================================================================
  //  THE PIECES
  // =====================================================================================================================
  const PIECES = {};

  /* THE SLIP TIDE BOARD (prop.stp_slip_tide_board) — the Landing. A tarred pile standing in the slip below the pier's
     south lip, where every arrival passes it, carrying a painted gauge in metres over CHART DATUM (the spring low, -2.20 m):
     a levelling staff's black blocks every 0.25 m, a black tick at each half metre, each whole metre in red, a red flag at
     the spring high (4.40 over datum, +2.20). The water in the game is the tide, so the board reads true at every clock
     with no logic of its own; its foot bares only at the bottom of a spring. The numerals are data. */
  PIECES.slipTideBoard = { front: '-y',
    id: 'prop.stp_slip_tide_board', name: 'Slip tide board', defType: 'SetPieceDef', scene: 'keyscene.stp_landing',
    layer: 'raised', walk: 'behind', footprint: [[-0.2, -0.22], [0.2, -0.22], [0.2, 0.2], [-0.2, 0.2]],
    defaults: { foot: -1.0, datum: -2.2, top: 2.6, growth: 1 },
    build(o) {
      const out = [], g = [], foot = o.foot, datum = o.datum, loc = (L) => L - foot, H = loc(o.top) + 0.35;
      // the pile: tarred to the splash line, silvered timber above, a cap board and an iron strap
      const tarTo = Math.max(0.3, loc(0.9));
      box(out, -0.15, 0.15, -0.15, 0.15, 0, tarTo, 'tar', 'pile');
      box(out, -0.15, 0.15, -0.15, 0.15, tarTo, H, 'wood', 'pile');
      box(out, -0.19, 0.19, -0.19, 0.19, H, H + 0.06, 'wood', 'pile.cap');
      box(out, -0.155, 0.155, -0.155, 0.155, H - 0.55, H - 0.5, 'iron', 'pile.strap');
      // growth: weed low, barnacles to mean high water, where the pile spends its time wet
      if (o.growth) { const w1 = Math.max(0.05, loc(-0.4)), b1 = Math.max(w1 + 0.05, loc(1.0));
        box(out, -0.165, 0.165, -0.165, 0.165, 0, w1, 'weed', 'growth'); box(out, -0.162, 0.162, -0.162, 0.162, w1, b1, 'barnacle', 'growth'); }
      // the gauge board on the show face: white, 0.32 wide, from the foot to the top mark
      const y0 = -0.2, x0 = -0.16, x1 = 0.16, zt = loc(o.top), yf = y0 - 0.004;
      box(out, x0, x1, y0, -0.15, 0.02, zt + 0.08, 'trim', 'gauge.face', { noBack: true, tagFront: 'gauge.face' });
      const d0 = Math.ceil((foot + 0.05 - datum) / 0.25) * 0.25, dTop = o.top - datum;
      for (let d = d0; d <= dTop + 1e-6; d += 0.25) {
        const m = Math.round(d * 100) / 100, z = loc(datum + m), whole = Math.abs(m - Math.round(m)) < 1e-6, half = Math.abs(m * 2 - Math.round(m * 2)) < 1e-6;
        if (!half && z + 0.25 <= zt + 0.08) { F(out, [[x0 + 0.01, yf, z], [0.01, yf, z], [0.01, yf, z + 0.125], [x0 + 0.01, yf, z + 0.125]], 'mark', 0, 'gauge.mark');
          F(out, [[0.01, yf, z + 0.125], [x1 - 0.01, yf, z + 0.125], [x1 - 0.01, yf, z + 0.25], [0.01, yf, z + 0.25]], 'mark', 0, 'gauge.mark'); }
        if (half) { const th = whole ? 0.06 : 0.035;
          F(out, [[x0 + 0.005, yf, z - th / 2], [x1 - 0.005, yf, z - th / 2], [x1 - 0.005, yf, z + th / 2], [x0 + 0.005, yf, z + th / 2]], whole ? 'red' : 'mark', 0.2, whole ? 'gauge.metre' : 'gauge.half'); }
        if (half) g.push({ z: +(datum + m).toFixed(2), overDatum: m, at: [whole ? x1 + 0.05 : 0, yf, z], label: whole ? String(Math.round(m)) : null, kind: whole ? 'metre' : 'half' });
      }
      // the spring high: a red flag board standing proud of the gauge's right edge
      const zs = loc(2.2); F(out, [[x1, yf, zs - 0.04], [x1 + 0.16, yf, zs - 0.04], [x1 + 0.16, yf, zs + 0.06], [x1, yf, zs + 0.06]], 'red', 0.2, 'gauge.springhigh');
      g.push({ z: 2.2, overDatum: 4.4, at: [x1 + 0.08, yf, zs + 0.01], label: null, kind: 'spring high' });
      return { faces: out, mats: mats(['wood', 'tar', 'trim', 'mark', 'red', 'iron', 'weed', 'barnacle']),
        anchors: { marks: g, top: [0, 0, H + 0.06], words: g.filter(q => q.label).map(q => ({ id: 'words.gauge_' + q.label, text: q.label, at: q.at, size: 0.12, note: 'the game draws the numeral beside the red metre mark' })) } };
    },
    notes: 'Reads metres over chart datum (-2.20, the spring low): whole metres red with numerals as data, the spring high (+2.20, 4.4) a red flag. opts.foot is the ground it stands on.',
  };

  /* THE HARBOUR VANE (prop.stp_harbour_vane) — the Landing. A timber mast on a crib foot at the slip head, a verdigris cod
     on a spindle at the top. The cod points INTO the wind: the game turns it from its own wind model, so the island's
     front door says which way the weather is coming before anyone does. 16 headings; the arms carry no letters. */
  PIECES.harbourVane = {
    id: 'prop.stp_harbour_vane', name: 'Harbour vane', defType: 'SetPieceDef', scene: 'keyscene.stp_landing',
    layer: 'raised', walk: 'behind', footprint: [[-0.42, -0.42], [0.42, -0.42], [0.42, 0.42], [-0.42, 0.42]],
    defaults: { height: 4.6, windFromDeg: 270 },
    build(o) {
      const out = [], H = o.height;
      // the crib foot: four sills and a box of planks (the island has timber; stone stays off)
      box(out, -0.42, 0.42, -0.42, -0.3, 0, 0.26, 'wood', 'foot'); box(out, -0.42, 0.42, 0.3, 0.42, 0, 0.26, 'wood', 'foot');
      box(out, -0.42, -0.3, -0.3, 0.3, 0, 0.26, 'wood', 'foot'); box(out, 0.3, 0.42, -0.3, 0.3, 0, 0.26, 'wood', 'foot');
      box(out, -0.3, 0.3, -0.3, 0.3, 0.2, 0.3, 'wood', 'foot');
      // the mast, tapering, with two braces to the foot
      stick(out, [0, 0, 0.26], [0, 0, H], 0.16, 'wood', 'mast');
      stick(out, [0.36, 0, 0.26], [0.04, 0, 1.5], 0.07, 'wood', 'mast.brace'); stick(out, [-0.36, 0, 0.26], [-0.04, 0, 1.5], 0.07, 'wood', 'mast.brace');
      box(out, -0.1, 0.1, -0.1, 0.1, H, H + 0.06, 'iron', 'mast.cap');
      // the compass arms (world-fixed: they turn with the piece's facing, so the game places the vane at dir 0 to keep N north)
      const za = H - 0.25; stick(out, [-0.42, 0, za], [0.42, 0, za], 0.035, 'iron', 'arms'); stick(out, [0, -0.42, za], [0, 0.42, za], 0.035, 'iron', 'arms');
      for (const [x, y] of [[0, 0.42], [0.42, 0], [0, -0.42], [-0.42, 0]]) box(out, x - 0.035, x + 0.035, y - 0.035, y + 0.035, za - 0.035, za + 0.035, 'iron', 'arms');
      // the spindle and the cod: it points into the wind (windFromDeg, world compass), so it is built in world directions
      stick(out, [0, 0, H + 0.06], [0, 0, H + 0.5], 0.04, 'iron', 'spindle');
      const a = (90 - o.windFromDeg) * D2R, ux = Math.cos(a), uy = Math.sin(a), px = -uy, py = ux, zc = H + 0.42, L = 0.95;
      const P = (s, t, dz) => [ux * s + px * t, uy * s + py * t, zc + dz];
      // head toward the wind (+s), tail fanned behind: a flat plate in the vertical plane of the heading, both sides drawn
      const body = [[0.48, 0, 0], [0.3, 0, 0.12], [0.02, 0, 0.14], [-0.25, 0, 0.08], [-0.38, 0, 0.02], [-0.5, 0, 0.14], [-0.47, 0, -0.1], [-0.38, 0, -0.02], [-0.25, 0, -0.07], [0.02, 0, -0.12], [0.3, 0, -0.1]];
      const side = (sg) => F(out, (sg > 0 ? body : body.slice().reverse()).map(q => { const p = P(q[0] * L / 0.98, sg * 0.012, q[2]); return p; }), 'copper', sg > 0 ? 0.05 : -0.1, 'vane.cod');
      side(1); side(-1);
      box(out, ux * 0.3 - 0.02, ux * 0.3 + 0.02, uy * 0.3 - 0.02, uy * 0.3 + 0.02, zc - 0.02, zc + 0.02, 'copper', 'vane.eye');
      return { faces: out, mats: mats(['wood', 'iron', 'copper']), anchors: { top: [0, 0, H + 0.6], vaneHeadingDeg: o.windFromDeg, words: [] } };
    },
    notes: 'The cod turns: windFromDeg (world compass, the wind it points into) comes from the game wind model; 16 headings (22.5 deg). Placed at dir 0 so its arms and its cod are true to the compass.',
  };

  /* THE BOILER STACK (structure.stp_cannery_boiler_stack) — the cannery's own silhouette. A brick boiler house with a tin
     lean-to roof, stove in, and a square brick chimney 14 m to its corbelled cap, two iron bands, the top courses gone.
     It stands against the cannery's gable and is the island's tallest thing after the lighthouse: it reads from the sea. */
  PIECES.boilerStack = { front: '-y',
    id: 'structure.stp_cannery_boiler_stack', name: 'Boiler house and stack', defType: 'SetPieceDef', scene: 'keyscene.stp_cannery',
    layer: 'raised', walk: 'behind', footprint: [[-2.1, -1.7], [2.1, -1.7], [2.1, 1.7], [-2.1, 1.7]],
    defaults: { stackH: 14.0, decay: 'derelict', lit: 0 },
    build(o) {
      const out = [], der = o.decay !== 'restored', sx = -1.05, sy = 0.25, H = o.stackH;
      // the boiler house: brick walls 3.0 m, a door in the show face, a small window, a tin lean-to roof sloping to +Y
      const W0 = -2.1, W1 = 2.1, D0 = -1.7, D1 = 1.7, h0 = 3.0, h1 = 2.4;
      FT(out, [[W0, D0, 0], [W1, D0, 0], [W1, D0, h1], [W0, D0, h1]], 'brick', -0.15, 'house.front', 'brick');
      FT(out, [[W1, D1, 0], [W0, D1, 0], [W0, D1, h0], [W1, D1, h0]], 'brick', 0.15, 'house', 'brick');
      FT(out, [[W1, D0, 0], [W1, D1, 0], [W1, D1, h0], [W1, D0, h1]], 'brick', 0, 'house', 'brick');
      FT(out, [[W0, D1, 0], [W0, D0, 0], [W0, D0, h1], [W0, D1, h0]], 'brick', -0.3, 'house', 'brick');
      // the door (iron, rust-streaked) and its lintel; the window's frame
      F(out, [[0.35, D0 - 0.02, 0], [1.35, D0 - 0.02, 0], [1.35, D0 - 0.02, 2.0], [0.35, D0 - 0.02, 2.0]], der ? 'rust' : 'iron', -0.2, 'house.door');
      box(out, 0.25, 1.45, D0 - 0.06, D0, 2.0, 2.14, 'soot', 'house.lintel', { noBack: true });
      F(out, [[-1.6, D0 - 0.02, 1.1], [-0.6, D0 - 0.02, 1.1], [-0.6, D0 - 0.02, 1.9], [-1.6, D0 - 0.02, 1.9]], 'glass', -0.2, 'house.window', der ? null : 'window');
      if (der) { F(out, [[-1.2, D0 - 0.025, 1.1], [-0.95, D0 - 0.025, 1.1], [-0.6, D0 - 0.025, 1.6], [-0.6, D0 - 0.025, 1.9], [-0.85, D0 - 0.025, 1.9]], 'soot', -0.1, 'house.window'); }
      // the roof: corrugated tin; derelict, the east bay has fallen in and hangs from the wall plate
      const zr = (y) => h1 + (h0 - h1) * (y - D0) / (D1 - D0);
      const roofTo = der ? 0.55 : W1;
      FT(out, [[W0 - 0.15, D0 - 0.2, zr(D0) - 0.05], [roofTo, D0 - 0.2, zr(D0) - 0.05], [roofTo, D1 + 0.1, zr(D1) + 0.02], [W0 - 0.15, D1 + 0.1, zr(D1) + 0.02]], der ? 'tinOld' : 'tin', -0.4, 'house.roof', 'ribs');
      if (der) { FT(out, [[roofTo + 0.1, D0, 0.9], [W1 + 0.1, D0 + 0.4, 1.4], [W1 + 0.1, D1, 2.2], [roofTo + 0.1, D1 - 0.3, zr(D1) - 0.4]], 'rust', 0.0, 'house.roof.fallen', 'ribs');
        for (let k = 0; k < 4; k++) stick(out, [0.7 + k * 0.4, D0 + 0.1, h1 - 0.1], [0.75 + k * 0.4, D1 - 0.1, h0 - 0.1 - (k === 2 ? 0.7 : 0)], 0.09, 'wood', 'house.rafter'); }
      // the stack: square brick, 1.3 m at the base tapering to 0.95, a plinth course, two iron bands, a corbelled cap
      const b0 = 0.65, b1 = 0.47, zb = 0, top = der ? H - 0.9 : H, sq = (z0, z1, r0, r1, mat, tag) => {
        const q = (r, z) => [[sx - r, sy - r, z], [sx + r, sy - r, z], [sx + r, sy + r, z], [sx - r, sy + r, z]], A = q(r0, z0), B = q(r1, z1);
        for (let k = 0; k < 4; k++) { const v = [A[k], A[(k + 1) % 4], B[(k + 1) % 4], B[k]]; if (mat === 'brick') FT(out, v, mat, [-0.15, 0, 0.15, -0.3][k], tag, 'brick'); else F(out, v, mat, [-0.15, 0, 0.15, -0.3][k], tag); } };
      const rAt = (z) => b0 + (b1 - b0) * (z / H);
      sq(zb, 1.0, b0 + 0.08, b0 + 0.08, 'brick', 'stack.plinth'); F(out, [[sx - b0 - 0.08, sy - b0 - 0.08, 1.0], [sx + b0 + 0.08, sy - b0 - 0.08, 1.0], [sx + b0 + 0.08, sy + b0 + 0.08, 1.0], [sx - b0 - 0.08, sy + b0 + 0.08, 1.0]], 'brick', 0.1, 'stack.plinth');
      sq(1.0, top, rAt(1.0), rAt(top), 'brick', 'stack');
      for (const zz of [5.5, 10.5]) sq(zz, zz + 0.12, rAt(zz) + 0.025, rAt(zz + 0.12) + 0.025, der ? 'rust' : 'iron', 'stack.band');
      if (!der) { sq(H, H + 0.25, rAt(H) + 0.12, rAt(H) + 0.12, 'brick', 'stack.cap'); F(out, [[sx - rAt(H) - 0.12, sy - rAt(H) - 0.12, H + 0.25], [sx + rAt(H) + 0.12, sy - rAt(H) - 0.12, H + 0.25], [sx + rAt(H) + 0.12, sy + rAt(H) + 0.12, H + 0.25], [sx - rAt(H) - 0.12, sy + rAt(H) + 0.12, H + 0.25]], 'soot', 0.1, 'stack.flue'); }
      else { // the broken top: a ragged last courses, one corner standing higher, soot inside
        const r = rAt(top); for (let k = 0; k < 5; k++) { const x = sx - r + (k + 0.5) * (2 * r / 5), hk = 0.1 + 0.55 * hash(k, 3, 91); box(out, x - r / 5, x + r / 5, sy - r, sy - r + 0.2, top, top + hk, 'brick', 'stack.ragged'); }
        box(out, sx + r - 0.24, sx + r, sy - r, sy + r, top, top + 0.8, 'brick', 'stack.ragged');
        F(out, [[sx - r + 0.12, sy - r + 0.12, top - 0.01], [sx + r - 0.12, sy - r + 0.12, top - 0.01], [sx + r - 0.12, sy + r - 0.12, top - 0.01], [sx - r + 0.12, sy + r - 0.12, top - 0.01]], 'soot', -0.3, 'stack.flue'); }
      return { faces: out, mats: mats(['brick', 'rust', 'iron', 'tin', 'tinOld', 'glass', 'soot', 'wood']),
        anchors: { flue: [sx, sy, der ? top : H + 0.25], door: [0.85, D0 - 0.05, 0], words: [] },
        emitters: der ? [] : ['window'] };
    },
    notes: 'Derelict (default) or restored. Restored, the flue anchor is where the smoke plume (fx.stp_cannery_smoke) rises: a runtime overlay.',
  };

  /* THE TROLLEY LINE (structure.stp_cannery_trolley_line) — 0.6 m gauge rails on sleepers, from the cannery's door to the
     pier root, sunk in the grass: missing sleepers, one rail lifted. A floor piece: walked on, drawn under figures. */
  PIECES.trolleyLine = {
    id: 'structure.stp_cannery_trolley_line', name: 'Trolley line', defType: 'SetPieceDef', scene: 'keyscene.stp_cannery',
    layer: 'floor', walk: 'on', footprint: null,
    defaults: { path: [[0, 0], [6, -2], [12, -4.2]], gauge: 0.6, decay: 'derelict', seed: 7 },
    build(o) {
      const out = [], P = o.path, g = o.gauge / 2, der = o.decay !== 'restored';
      let s = 0;
      for (let k = 0; k + 1 < P.length; k++) {
        const A = P[k], B = P[k + 1], dx = B[0] - A[0], dy = B[1] - A[1], L = Math.hypot(dx, dy), ux = dx / L, uy = dy / L, nx = -uy, ny = ux;
        // the ballast: cinders and dirt under the sleepers, 1.3 m wide, ragged at the edges (the piece's own floor, not a ground change)
        for (let t = 0; t < L; t += 0.5) { const t1 = Math.min(L, t + 0.5), w0 = 0.62 + 0.1 * hash(k, Math.round(t * 2), o.seed + 5), w1 = 0.62 + 0.1 * hash(k, Math.round(t * 2) + 1, o.seed + 5);
          F(out, [[A[0] + ux * t - nx * w0, A[1] + uy * t - ny * w0, 0.005], [A[0] + ux * t1 - nx * w1, A[1] + uy * t1 - ny * w1, 0.005], [A[0] + ux * t1 + nx * w1, A[1] + uy * t1 + ny * w1, 0.005], [A[0] + ux * t + nx * w0, A[1] + uy * t + ny * w0, 0.005]], der ? 'cinder' : 'soot', -0.3, 'ballast'); }
        // sleepers every 0.7 m, some missing, some skewed
        for (let t = 0.2; t < L; t += 0.7, s++) { const miss = der && hash(s, k, o.seed) < 0.18; if (miss) continue;
          const cx = A[0] + ux * t, cy = A[1] + uy * t, sk = der ? (hash(s, 9, o.seed) - 0.5) * 0.25 : 0, a = Math.atan2(ny, nx) + sk;
          obox(out, cx, cy, 0.52, 0.09, -0.02, 0.06, a, 'wood', 'sleeper'); }
        // the rails: two iron bars 0.05 wide, 0.07 high; derelict, the outer rail lifts off its last metres
        for (const side of [-1, 1]) { const ox = nx * g * side, oy = ny * g * side, lift = der && side > 0 && k === P.length - 2;
          const n = Math.max(1, Math.ceil(L / 1.5));
          for (let j = 0; j < n; j++) { const t0 = j / n * L, t1 = (j + 1) / n * L, z0 = lift ? Math.max(0, (t0 - L + 3) * 0.12) : 0, z1 = lift ? Math.max(0, (t1 - L + 3) * 0.12) : 0;
            stick(out, [A[0] + ux * t0 + ox, A[1] + uy * t0 + oy, 0.08 + z0], [A[0] + ux * t1 + ox, A[1] + uy * t1 + oy, 0.08 + z1], 0.05, der ? 'rust' : 'iron', 'rail'); } }
      }
      return { faces: out, mats: mats(['wood', 'rust', 'iron', 'cinder', 'soot']), anchors: { path: P, words: [] } };
    },
    notes: 'A floor piece, walked on; the path is in the piece\'s own metres from its origin (the cannery door end).',
  };

  /* THE TROLLEY (prop.stp_cannery_trolley) — a flat rail trolley, left where it stopped: a rusted frame, an oak deck with
     a board gone, one wheel off the rail, a broken fish box still on it. */
  PIECES.trolley = { front: '-y',
    id: 'prop.stp_cannery_trolley', name: 'Derelict trolley', defType: 'SetPieceDef', scene: 'keyscene.stp_cannery',
    layer: 'raised', walk: 'behind', footprint: [[-0.75, -0.5], [0.75, -0.5], [0.75, 0.5], [-0.75, 0.5]],
    defaults: { decay: 'derelict', load: 'box' },
    build(o) {
      const out = [], der = o.decay !== 'restored', tilt = der ? 0.07 : 0, zAt = (x) => 0.36 + tilt * (x + 0.75) / 1.5;
      // wheels: four iron discs on axles at the rails (0.6 gauge), one dropped off
      for (const [x, y, drop] of [[-0.5, -0.3, 0], [0.5, -0.3, der ? 0.06 : 0], [-0.5, 0.3, 0], [0.5, 0.3, 0]]) {
        const z = 0.17 - drop; cyl(out, x, y, 0, 0, 0, 0, 3, 'iron'); out.pop();
        const pts = []; for (let k = 0; k < 10; k++) { const a = k / 10 * 2 * Math.PI; pts.push([x + 0.17 * Math.cos(a), y, z + 0.17 * Math.sin(a)]); }
        F(out, pts, der ? 'rust' : 'iron', -0.1, 'wheel'); F(out, pts.map(p => [p[0], p[1] + (y < 0 ? 0.05 : -0.05), p[2]]).reverse(), der ? 'rust' : 'iron', 0.05, 'wheel'); }
      stick(out, [-0.5, -0.36, 0.17], [-0.5, 0.36, 0.17], 0.05, 'iron', 'axle'); stick(out, [0.5, -0.36, 0.17], [0.5, 0.36, 0.11], 0.05, 'iron', 'axle');
      // the frame and the deck boards (one missing when derelict)
      stick(out, [-0.75, -0.42, zAt(-0.75) - 0.06], [0.75, -0.42, zAt(0.75) - 0.06], 0.08, der ? 'rust' : 'iron', 'frame');
      stick(out, [-0.75, 0.42, zAt(-0.75) - 0.06], [0.75, 0.42, zAt(0.75) - 0.06], 0.08, der ? 'rust' : 'iron', 'frame');
      for (let k = 0; k < 6; k++) { if (der && k === 2) continue; const x0 = -0.75 + k * 0.25, x1 = x0 + 0.23;
        F(out, [[x0, -0.5, zAt(x0)], [x1, -0.5, zAt(x1)], [x1, 0.5, zAt(x1)], [x0, 0.5, zAt(x0)]], 'plank', k % 2 ? 0.05 : -0.05, 'deck');
        F(out, [[x0, -0.5, zAt(x0) - 0.05], [x1, -0.5, zAt(x1) - 0.05], [x1, -0.5, zAt(x1)], [x0, -0.5, zAt(x0)]], 'plank', -0.2, 'deck'); }
      // the load: a slatted fish box, split
      if (o.load === 'box') { const bx = 0.18, bz = zAt(0.18);
        box(out, bx - 0.36, bx + 0.36, -0.26, 0.26, bz, bz + 0.3, 'wood', 'load');
        if (der) { F(out, [[bx - 0.1, -0.265, bz + 0.05], [bx + 0.05, -0.265, bz + 0.3], [bx - 0.02, -0.265, bz + 0.3], [bx - 0.16, -0.265, bz + 0.09]], 'soot', -0.1, 'load.split'); }
        for (const yy of [-0.12, 0.12]) box(out, bx - 0.37, bx + 0.37, yy - 0.01, yy + 0.01, bz + 0.3, bz + 0.32, 'wood', 'load'); }
      return { faces: out, mats: mats(['iron', 'rust', 'plank', 'wood', 'soot']), anchors: { words: [] } };
    },
    notes: 'Sits on the trolley line; its long axis runs along the rails.',
  };

  /* THE CONVEYOR'S BONES (prop.stp_cannery_conveyor) — an inclined steel frame that fed the cannery's loft door from the
     trolley line: A-frame legs, the side stringers, three rollers left of twenty, a strip of belt hanging. */
  PIECES.conveyor = { front: '-y',
    id: 'prop.stp_cannery_conveyor', name: 'Conveyor frame', defType: 'SetPieceDef', scene: 'keyscene.stp_cannery',
    layer: 'raised', walk: 'under', footprint: [[-3.1, -0.55], [-2.3, -0.55], [-2.3, 0.55], [-3.1, 0.55]],
    footprints: [[[-3.1, -0.55], [-2.3, -0.55], [-2.3, 0.55], [-3.1, 0.55]], [[-0.4, -0.55], [0.4, -0.55], [0.4, 0.55], [-0.4, 0.55]], [[2.3, -0.55], [3.1, -0.55], [3.1, 0.55], [2.3, 0.55]]],
    defaults: { length: 6.2, z0: 0.8, z1: 2.9, decay: 'derelict', seed: 3 },
    build(o) {
      const out = [], der = o.decay !== 'restored', L = o.length, hx = L / 2, zAt = (x) => o.z0 + (o.z1 - o.z0) * (x + hx) / L, m = der ? 'rust' : 'iron', w = 0.36;
      for (const yy of [-w, w]) stick(out, [-hx, yy, zAt(-hx)], [hx, yy, zAt(hx)], 0.1, m, 'stringer');
      // legs: three A-frames with a cross-tie
      for (const x of [-hx + 0.4, 0, hx - 0.4]) { const z = zAt(x) - 0.05;
        stick(out, [x - 0.3, -w - 0.1, 0], [x, -w, z], 0.07, m, 'leg'); stick(out, [x + 0.3, -w - 0.1, 0], [x, -w, z], 0.07, m, 'leg');
        stick(out, [x - 0.3, w + 0.1, 0], [x, w, z], 0.07, m, 'leg'); stick(out, [x + 0.3, w + 0.1, 0], [x, w, z], 0.07, m, 'leg');
        stick(out, [x, -w, z * 0.45], [x, w, z * 0.45], 0.05, m, 'tie'); }
      // rollers: of twenty, three are left (derelict)
      const n = 20; for (let k = 0; k < n; k++) { const keep = !der || [3, 9, 16].includes(k); if (!keep) continue; const x = -hx + (k + 0.5) * L / n;
        stick(out, [x, -w + 0.03, zAt(x) + 0.04], [x, w - 0.03, zAt(x) + 0.04], 0.08, der ? 'iron' : 'tin', 'roller'); }
      // a strip of belt hanging from the top rollers
      if (der) { const x = hx - 1.2, z = zAt(x); F(out, [[x, -0.2, z + 0.02], [x + 0.25, -0.2, z + 0.03], [x + 0.35, -0.12, z - 0.9], [x + 0.12, -0.12, z - 1.1]], 'soot', -0.1, 'belt');
        F(out, [[x + 0.25, -0.2, z + 0.03], [x, -0.2, z + 0.02], [x + 0.12, -0.12, z - 1.1], [x + 0.35, -0.12, z - 0.9]], 'soot', 0.0, 'belt'); }
      else { F(out, [[-hx, -w + 0.04, o.z0 + 0.09], [hx, -w + 0.04, o.z1 + 0.09], [hx, w - 0.04, o.z1 + 0.09], [-hx, w - 0.04, o.z0 + 0.09]], 'soot', 0.05, 'belt'); }
      return { faces: out, mats: mats(['rust', 'iron', 'tin', 'soot']), anchors: { top: [hx, 0, o.z1], foot: [-hx, 0, o.z0], words: [] } };
    },
    notes: 'The legs are three colliders; a walker passes under the frame between them.',
  };

  /* THE FALLEN SIGN (prop.stp_cannery_fallen_sign) — the company's name board, 5 m long, down in the grass where the wind
     put it: one end on its broken post, the other in the grass. Its words are data (the owner will rename the place). */
  PIECES.fallenSign = { front: '-y',
    id: 'prop.stp_cannery_fallen_sign', name: 'Fallen company sign', defType: 'SetPieceDef', scene: 'keyscene.stp_cannery',
    layer: 'raised', walk: 'behind', footprint: [[-2.6, -0.45], [2.6, -0.45], [2.6, 0.45], [-2.6, 0.45]],
    defaults: { length: 5.0, height: 0.95, lean: 0.62, text: 'ST PETERS PACKING CO.' },
    build(o) {
      const out = [], L = o.length, hx = L / 2, h = o.height, a = o.lean, zL = 0.05, zR = 0.72;
      // the board lies tilted back (a rad from vertical toward +Y) with its left end low: its face is the show face
      const P = (x, v) => { const z0 = zL + (zR - zL) * (x + hx) / L; return [x, -0.1 + Math.sin(a) * v, z0 + Math.cos(a) * v]; };   // authored front at -y; built() mirrors it to +y
      F(out, [P(-hx, 0), P(hx, 0), P(hx, h), P(-hx, h)], 'board', -0.1, 'words.cannery_sign');
      // the frame: a dark moulding round the board, the paint gone in patches (model-space, so it stays put)
      const fr = (x0, v0, x1, v1, mat, tag) => F(out, [[...P(x0, v0)].map((q, i) => i === 1 ? q - 0.012 : q), [...P(x1, v0)].map((q, i) => i === 1 ? q - 0.012 : q), [...P(x1, v1)].map((q, i) => i === 1 ? q - 0.012 : q), [...P(x0, v1)].map((q, i) => i === 1 ? q - 0.012 : q)], mat, 0.1, tag);
      fr(-hx, 0, hx, 0.07, 'mark', 'frame'); fr(-hx, h - 0.07, hx, h, 'mark', 'frame'); fr(-hx, 0, -hx + 0.07, h, 'mark', 'frame'); fr(hx - 0.07, 0, hx, h, 'mark', 'frame');
      for (let k = 0; k < 7; k++) { const x = -hx + 0.3 + hash(k, 1, 5) * (L - 0.6), v = 0.12 + hash(k, 2, 5) * (h - 0.3), s = 0.12 + hash(k, 3, 5) * 0.2; fr(x, v, x + s * 1.6, v + s * 0.5, 'wood', 'peel'); }
      // the back and the edge (so the board has thickness), and the broken post it rests on
      F(out, [P(hx, 0).map((q, i) => i === 1 ? q + 0.05 : q), P(-hx, 0).map((q, i) => i === 1 ? q + 0.05 : q), P(-hx, h).map((q, i) => i === 1 ? q + 0.05 : q), P(hx, h).map((q, i) => i === 1 ? q + 0.05 : q)], 'wood', 0.15, 'back');
      F(out, [P(-hx, h), P(hx, h), P(hx, h).map((q, i) => i === 1 ? q + 0.05 : q), P(-hx, h).map((q, i) => i === 1 ? q + 0.05 : q)], 'wood', 0.1, 'edge');
      box(out, hx - 0.55, hx - 0.35, 0.25, 0.45, 0, 0.78, 'wood', 'post'); stick(out, [hx - 0.45, 0.35, 0.78], [hx - 0.1, 0.55, 0.95], 0.14, 'wood', 'post.split');
      box(out, -hx + 0.6, -hx + 0.8, 0.3, 0.5, 0, 0.32, 'wood', 'post');
      const c = [P(-hx + 0.12, 0.12), P(hx - 0.12, 0.12), P(hx - 0.12, h - 0.12), P(-hx + 0.12, h - 0.12)];
      return { faces: out, mats: mats(['board', 'mark', 'wood']), anchors: { words: [{ id: 'words.cannery_sign', text: o.text, corners: c, note: 'the company name, lettered by the game on the board plane; the owner renames places later' }] } };
    },
    notes: 'The board plane\'s four corners are in anchors().words; the game letters it. Default string: ST PETERS PACKING CO.',
  };

  /* THE DOOR NOTICE (prop.stp_cannery_door_notice) — a hasp, a chain and a padlock across the cannery's doors, and a
     printed notice nailed beside them: the later purchase hook. Wall-mounted: it takes the door's facing. Words are data. */
  PIECES.doorNotice = { front: '-y',
    id: 'prop.stp_cannery_door_notice', name: 'Padlock and notice', defType: 'SetPieceDef', scene: 'keyscene.stp_cannery',
    layer: 'raised', walk: 'behind', footprint: null, mount: 'wall',
    defaults: { text: 'NOTICE', body: 'notice.cannery_for_sale', span: 2.2 },
    build(o) {
      const out = [], y = -0.03, sp = o.span / 2, zc = 1.15;
      // the chain: iron links across the two leaves' handles, sagging to the padlock
      const pts = []; for (let k = 0; k <= 12; k++) { const t = k / 12, x = -sp * 0.35 + t * sp * 0.7, z = zc - 0.22 * Math.sin(Math.PI * t); pts.push([x, y - 0.02, z]); }
      for (let k = 0; k + 1 < pts.length; k++) stick(out, pts[k], pts[k + 1], 0.035, 'iron', 'chain');
      box(out, -0.07, 0.07, y - 0.08, y - 0.02, zc - 0.34, zc - 0.2, 'iron', 'padlock'); stick(out, [-0.045, y - 0.05, zc - 0.2], [0.045, y - 0.05, zc - 0.2], 0.02, 'iron', 'padlock');
      for (const x of [-sp * 0.35, sp * 0.35]) box(out, x - 0.05, x + 0.05, y - 0.04, y, zc - 0.07, zc + 0.07, 'rust', 'hasp');
      // the notice: a printed card on a board, nailed at eye height beside the doors; its text is data
      const nx0 = sp + 0.18, nx1 = nx0 + 0.46, nz0 = 1.25, nz1 = 1.85;
      box(out, nx0 - 0.03, nx1 + 0.03, y - 0.03, y, nz0 - 0.03, nz1 + 0.03, 'wood', 'notice.board', { noBack: true });
      F(out, [[nx0, y - 0.034, nz0], [nx1, y - 0.034, nz0], [nx1, y - 0.034, nz1], [nx0, y - 0.034, nz1]], 'trim', 0.1, 'words.cannery_notice');
      F(out, [[nx0 + 0.04, y - 0.036, nz1 - 0.1], [nx1 - 0.04, y - 0.036, nz1 - 0.1], [nx1 - 0.04, y - 0.036, nz1 - 0.06], [nx0 + 0.04, y - 0.036, nz1 - 0.06]], 'red', 0.2, 'notice.rule');
      return { faces: out, mats: mats(['iron', 'rust', 'wood', 'trim', 'red']),
        anchors: { words: [{ id: 'words.cannery_notice', text: o.text, body: o.body, corners: [[nx0, y - 0.034, nz0], [nx1, y - 0.034, nz0], [nx1, y - 0.034, nz1], [nx0, y - 0.034, nz1]], note: 'the purchase hook; its heading and body are strings the game draws' }],
          interact: [nx0 + 0.23, y - 0.6, 0], lock: [0, y - 0.05, zc - 0.27] } };
    },
    notes: 'Mount at the cannery\'s door anchor, at the door\'s own facing. interact is where a player stands to read it.',
  };

  // ---- the smoke (fx.stp_cannery_smoke): the restored cannery's plume, a runtime overlay, not a sprite --------------------
  function smoke(t, wind, seed) {
    const T = TUNABLES.smoke, w = wind || { w: 0.3, dir: 1 }, out = [];
    for (let k = 0; k < T.puffs; k++) {
      const ph = ((t / T.life_h) + k / T.puffs + hash(k, 1, seed || 1) * 0.2) % 1, age = ph * T.life_h * 3600;   // seconds since the puff left the flue
      const up = age * T.rise_mps * (0.8 + 0.4 * hash(k, 2, seed || 1)), dx = age * w.w * (w.dir || 1) * T.drift_per_wind * 0.25, r = 0.35 + age * 0.09;
      out.push({ x: dx + (hash(k, 3, seed || 1) - 0.5) * 0.4, y: 0, z: up, r, a: clamp(1 - ph, 0, 1) * 0.85 });
    }
    return out;
  }

  // =====================================================================================================================
  //  THE CAMERA, THE CELL, THE LIGHT
  // =====================================================================================================================
  const MODELS = new Map();
  function spec(key, o) { const P = PIECES[key]; if (!P) throw new Error('StPetersSetPieces: no piece "' + key + '"'); return Object.assign({}, P.defaults, o || {}); }
  function geomKey(key, o) { const s = spec(key, o), g = {}; for (const k of Object.keys(PIECES[key].defaults)) g[k] = s[k]; return key + '|' + JSON.stringify(g); }
  function mirrorY(v) { if (Array.isArray(v)) { if (v.length === 3 && v.every(q => typeof q === 'number')) return [v[0], -v[1], v[2]]; return v.map(mirrorY); } if (v && typeof v === 'object') { const o = {}; for (const k of Object.keys(v)) o[k] = mirrorY(v[k]); return o; } return v; }
  function built(key, o) { const gk = geomKey(key, o); let B = MODELS.get(gk); if (B) return B;
    const s = spec(key, o), b = PIECES[key].build(s);
    if (PIECES[key].front === '-y') { for (const f of b.faces) f.v = f.v.map(p => [p[0], -p[1], p[2]]); b.anchors = mirrorY(b.anchors || {}); }
    const mdl = LT().model(b.faces, b.mats, { aoRes: 12, sunRes: 24 });
    B = { key, s, b, mdl }; MODELS.set(gk, B); if (MODELS.size > 64) MODELS.delete(MODELS.keys().next().value); return B; }
  function basis(dir) { const a = ((dir | 0) % 8) * Math.PI / 4; return { ct: Math.cos(a), st: Math.sin(a), se: SE, ce: CE, S: PPU }; }
  function projRaw(p, B) { const xr = p[0] * B.ct - p[1] * B.st, yr = p[0] * B.st + p[1] * B.ct; return [xr * B.S, -(yr * B.se + p[2] * B.ce) * B.S, yr * B.ce - p[2] * B.se]; }
  function cellOf(B0, dir) { const B = basis(dir); let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
    for (const f of B0.b.faces) for (const p of f.v) { const q = projRaw(p, B); if (q[0] < x0) x0 = q[0]; if (q[0] > x1) x1 = q[0]; if (q[1] < y0) y0 = q[1]; if (q[1] > y1) y1 = q[1]; }
    const pad = 3, ox = Math.ceil(-x0) + pad, oy = Math.ceil(-y0) + pad; return { w: Math.ceil(x1 - x0) + 2 * pad + 1, h: Math.ceil(y1 - y0) + 2 * pad + 1, px: ox, py: oy, C: Object.assign({}, B, { ox, oy }) }; }
  function frame(key, dir, o) { const B0 = built(key, o), c = cellOf(B0, dir), fr = LT().frame(B0.mdl, c.C, c.w, c.h); fr.cell = c; fr.piece = key; fr.dir = dir | 0; fr.built = B0; return fr; }
  function skyOf(o) { return LT().skyOf(o || {}); }
  /* o: the sky (o.sky, or o.time/cloud/rain/fog/snow with WeatherSky), o.lights [{p model m, c, I, r}], o.lit 0..1 for the
     emitters (windows, the lantern), o.outline for the keyline A/B */
  function relight(fr, sky, o) { o = o || {}; const lit = o.lit != null ? +o.lit : 0; return LT().relight(fr, sky || skyOf(o), { outline: !!o.outline, lights: o.lights || [], emit: () => lit }); }
  function render(key, dir, o) { o = o || {}; const fr = frame(key, dir, o); return { w: fr.W, h: fr.H, px: fr.cell.px, py: fr.cell.py, rgba: relight(fr, o.sky || skyOf(o), o), frame: fr }; }
  function castShadow(fr, sky) { return LT().castShadow(fr, sky, { z: 0 }); }
  function view(fr, ch, sky) { return LT().view(fr, ch, sky); }
  // which way the show face points at each dir, MEASURED by projecting its normal (+Y) onto the ground
  const NAMES = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  function facings() { const out = []; for (let d = 0; d < 8; d++) { const B = basis(d), wx = 0 * B.ct - 1 * B.st, wy = 0 * B.st + 1 * B.ct, deg = (Math.atan2(wx, wy) / D2R + 360) % 360;
      out.push({ dir: d, showFaces: NAMES[Math.round(deg / 45) % 8], bearingDeg: +deg.toFixed(1), cameraSees: wy < -0.5 ? 'the show face' : wy < 0.2 ? 'the show face, edge-on or at a diagonal' : 'its back' }); } return out; }
  function dirFacing(name) { const f = facings().find(q => q.showFaces === name); return f ? f.dir : null; }
  // anchors at a dir: model metres, and cell px
  function anchors(key, dir, o) { const B0 = built(key, o), c = cellOf(B0, dir), toPx = (p) => { const q = projRaw(p, c.C); return [+(c.px + q[0]).toFixed(1), +(c.py + q[1]).toFixed(1)]; };
    const a = JSON.parse(JSON.stringify(B0.b.anchors || {})); a.px = {}; for (const k of Object.keys(a)) { const v = a[k]; if (Array.isArray(v) && v.length === 3 && typeof v[0] === 'number') a.px[k] = toPx(v); }
    if (a.words) for (const w of a.words) { if (w.corners) w.cornersPx = w.corners.map(toPx); if (w.at) w.atPx = toPx(w.at); }
    if (a.marks) for (const m of a.marks) if (m.at) m.px = toPx(m.at);
    return a; }
  function footprintAt(key, o) { const P = PIECES[key], L = P.footprints ? P.footprints : P.footprint ? [P.footprint] : []; return P.front === '-y' ? L.map(q => q.map(p => [p[0], -p[1]])) : L; }
  // ---- the gameplay sidecar (generated, never edited) -------------------------------------------------------------------
  function gameplay(key, o) {
    const P = PIECES[key], s = spec(key, o), B0 = built(key, o);
    let zmax = 0, x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9; for (const f of B0.b.faces) for (const p of f.v) { zmax = Math.max(zmax, p[2]); x0 = Math.min(x0, p[0]); x1 = Math.max(x1, p[0]); y0 = Math.min(y0, p[1]); y1 = Math.max(y1, p[1]); }
    const cells = []; for (let d = 0; d < 8; d++) { const c = cellOf(B0, d); cells.push({ dir: d, w: c.w, h: c.h, pivot: [c.px, c.py] }); }
    return {
      schema: 'hidden-harbours/st-peters-set-piece@1', generatedBy: 'StPetersSetPieces ' + VERSION, key, id: P.id, name: P.name, defType: P.defType, scene: P.scene,
      units: 'metres, the piece\'s own frame: +x right, +y the show face, +z up from its ground point (the pivot)',
      bounds: { x: [+x0.toFixed(3), +x1.toFixed(3)], y: [+y0.toFixed(3), +y1.toFixed(3)], z: [0, +zmax.toFixed(3)] },
      layer: P.layer, walk: P.walk, mount: P.mount || 'ground',
      colliders: footprintAt(key, o).map(pts => ({ type: 'polygon', blocks: 'walk', pts })),
      facings: facings(), cells, anchors: anchors(key, 4, o), options: s,
      light: key === 'boilerStack' ? { emitters: ['window'], when: 'restored only: a runtime overlay (the backlog makes windows and smoke overlays)' } : null,
      animated: key === 'harbourVane' ? { part: 'vane.cod', input: 'windFromDeg', source: 'the game wind model (world seed, game time)', steps: TUNABLES.vane.stepsPerTurn } : null,
      notes: P.notes,
    };
  }
  const API = { VERSION, PPU, RAMPS, TUNABLES, PIECES, KEYS: Object.keys(PIECES), spec, built, basis, projRaw, cellOf, frame, relight, render, castShadow, view, facings, dirFacing, anchors, gameplay, smoke, skyOf, NAMES };
  root.StPetersSetPieces = API;
  if (typeof module !== 'undefined' && module.exports) module.exports = API;
})(typeof globalThis !== 'undefined' ? globalThis : this);
