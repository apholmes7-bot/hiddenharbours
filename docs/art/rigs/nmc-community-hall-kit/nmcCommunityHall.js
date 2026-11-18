/* NINE MILE CREEK COMMUNITY HALL KIT (globalThis.NmcCommunityHall), 1.0.0, 2026-11-16.
   The parish hall on the town lot (centre world (-153, 62.5)), with the gym in its back room: an 18 x 12 m one-storey
   hall, its long side north to south, its door on the WEST wall facing the lot's only gate (-161.5, 62.8). Drawn to the
   greybox shell's footprint (NmcBuildingShells.CommunityHall: 18 m north to south, 12 m west to east, door 0.5 m north
   of centre) so the art swaps onto the shell without moving a collider.

   Built the way the Nine Mile Creek fish plant kit is (nmc-fish-plant-kit/nmcFishPlant.js), and drawn THROUGH it: this
   kit has no rasteriser of its own. Load weatherSky.js, coastalPass.js (the set pieces kit's lib copies) and
   nmcFishPlant.js first, then this file, then call NmcCommunityHall.install(NmcFishPlant): that registers one piece,
   'communityHall', and every render, frame and anchor call goes through the plant kit's API (same light, same camera,
   same 32 px = 1 m). Colour is ramps only; every ramp is the set pieces kit's (white paint, railway green trim, asphalt
   shingles, brick, concrete), so the hall sits beside the restaurant and the chandlery without a new colour.

   THE FRAME. The plant kit's: CAMERA 32 px = 1 m, 40 deg, dir 4 shows local +Y. The hall stands at dir 4 with its pivot
   on the building's centre, so local x = -(world east offset), local y = -(world north offset): local +Y is the SOUTH
   gable (the face the camera sees), local +X is the WEST wall with the door. At this camera a west wall is seen edge on,
   so the way in reads from what stands out of it: a gabled entry hood on two posts, the steps and the door lamp.

   WORDS ARE DATA: the board under the south gable and the notice board on the porch are blank, tagged 'words.<id>'.
   o.cutaway true lifts the roof and cuts the walls at 1.1 m: the front hall, the gym, the kitchen and the washroom as
   floor, the partitions with the gym's doorway and the front door left open (the kitchen and washroom stay closed). */
(function (root) {
  'use strict';
  const VERSION = '1.0.0';
  const fr1 = (x, p) => ((x / p) % 1 + 1) % 1;
  // ---- ramps: the set pieces kit's values, verbatim (white paint is its 'siding')
  const RAMPS = {
    paint: ['#5c615e', '#7a7f7a', '#979b95', '#b3b6ae', '#cbccc2', '#dedfd4'],
    trim: ['#565b58', '#737872', '#90948d', '#adb0a7', '#c7c9bf', '#dcddd2'],
    green: ['#132a1d', '#1b3a29', '#244c35', '#2f6043', '#3b7452', '#4b8963'],
    roofAsph: ['#1e2124', '#2a2e31', '#373b3e', '#44494b', '#52575a', '#62676a'],
    glass: ['#0f191e', '#15232a', '#1d2f38', '#293e48', '#394f5a', '#4e656e'],
    conc: ['#51524c', '#686962', '#7f8077', '#96978c', '#acada0', '#bfbfb2'],
    brick: ['#46211a', '#5c2c22', '#73392b', '#8a4735', '#a05741', '#b3694f'],
    ptwood: ['#3a3322', '#4f462e', '#665b3b', '#7d7049', '#948659', '#aa9b6b'],
    wood: ['#2c2823', '#3d3730', '#524a40', '#6a6153', '#837966', '#9d937e'],
    board: ['#55503e', '#6d674f', '#877f62', '#a09776', '#b9ae8c', '#cfc3a2'],
    iron: ['#19262b', '#26383d', '#3a4d50', '#516566', '#687d79', '#899790'],
    galv: ['#4b5155', '#61686c', '#7a8185', '#94999d', '#adb2b5', '#c4c8ca'],
    lampW: ['#5c5a4a', '#8e8a6c', '#bcb690', '#ddd7b0', '#f2edcf', '#fffbea'],
    cast: ['#16181a', '#1e2124', '#272b2e', '#31363a', '#3c4246', '#495055'],
    // the cutaway's floors: the hall's maple (the plant kit's lino family), the gym's rubber, the kitchen's lino, the washroom's tile
    maple: ['#4f3f27', '#6a5433', '#866b41', '#a08250', '#b89961', '#cdae74'],
    rubber: ['#1b1f1d', '#232826', '#2c3230', '#363d3a', '#414945', '#4d5651'],
    lino: ['#4d4a3c', '#646050', '#7c7765', '#948e7a', '#aba58f', '#bfb9a3'],
    tile: ['#5f6566', '#7a8182', '#959c9c', '#afb5b4', '#c6cbc9', '#d9dddb'],
  };
  // ---- textures: the set pieces kit's clapboard and shingles, and floors for the cutaway
  const TEX = {
    clap: (u, v) => fr1(v, 0.14) < 0.22 ? -1 : 0,
    shingles: (u, v) => { const r = Math.floor(v / 0.2); return fr1(v, 0.2) < 0.2 || fr1(u / 0.34 + (r % 2) * 0.5, 1) < 0.06 ? -1 : 0; },
    louvre: (u, v) => fr1(v, 0.09) < 0.28 ? -1 : 0,
    brick: (u, v) => { const row = Math.floor(v / 0.075); return fr1(v, 0.075) < 0.2 || fr1(u + (row & 1) * 0.1, 0.2) < 0.1 ? -1 : 0; },
    strip: (u) => fr1(u, 0.09) < 0.12 ? -1 : 0,
    chequer: (u, v) => (Math.floor(u / 0.3) + Math.floor(v / 0.3)) & 1 ? -1 : 0,
    tile: (u, v) => fr1(u, 0.3) < 0.06 || fr1(v, 0.3) < 0.06 ? -1 : 0,
    boards: (u, v) => fr1(v, 0.2) < 0.16 ? -1 : 0,
  };
  // ---- the plan, local metres (see THE FRAME). Walls x [-6, 6] (east, west), y [-9, 9] (north, south).
  const PL = { X0: -6, X1: 6, Y0: -9, Y1: 9, ZF: 0.5, ZE: 3.9, ZR: 7.6, OV: 0.45, OG: 0.4, CUT: 1.1,
    door: { y: -0.5, w: 1.4, h: 2.3 },                                   // the west door: 0.5 m north of centre, the shell's doorway width
    porch: { x: 7.4, y0: -1.75, y1: 0.75, z: 2.75, rise: 0.9 },          // the entry hood: 1.4 m out from the wall, open sides
    steps: { x: 7.7 },                                                   // the stoop and two steps, 1.7 m out (0.8 m short of the gate)
  };
  PL.SL = (PL.ZR - PL.ZE) / PL.X1;                                       // the roof's rise per metre out from the ridge
  // the shell's zones, in this frame (NmcBuildingShells.CommunityHall's model frame turned to the world, then to local)
  const ZONES = [
    ['frontHall', 'Front hall', 0, 6, -9, 9, 'maple', 'strip'],
    ['gym', 'Gym', -6, 0, -9, -1, 'rubber', null],
    ['kitchen', 'Kitchen (closed for now)', -6, 0, -1, 4, 'lino', 'chequer'],
    ['washroom', 'Washroom (closed for now)', -6, 0, 4, 9, 'tile', 'tile'],
  ];

  function install(NF) {
    if (!NF || !NF.helpers) throw new Error('NmcCommunityHall.install needs NmcFishPlant (load nmcFishPlant.js first)');
    const { F, box, stick, cyl, panelY } = NF.helpers;
    const M = (keys) => { const o = {}; for (const k of keys) o[k] = { ramp: RAMPS[k].slice() }; return o; };
    // a wall or roof plane with this kit's texture: walls take their lines from the height (u along the wall, v up)
    const Fw = (out, v, mat, b, tag, tex, em) => F(out, v, mat, b, tag, em || null, { uv: v.map(p => [p[0] + p[1], p[2]]), tex: TEX[tex] });
    const Fr = (out, v, mat, b, tag, tex) => F(out, v, mat, b, tag, null, { uv: v.map(p => [p[1], Math.abs(p[0]) * 1.28]), tex: TEX[tex] });
    const Ff = (out, v, mat, b, tag, tex) => F(out, v, mat, b, tag, null, tex ? { uv: v.map(p => [p[0], p[1]]), tex: TEX[tex] } : null);
    // a window on the south gable (faces +y): trim, glass that glows, a meeting rail, a sill
    function winS(out, y, x0, x1, z0, z1, id) {
      panelY(out, y + 0.01, 1, x0 - 0.1, x1 + 0.1, z0 - 0.1, z1 + 0.12, 'trim', 0.1, 'window.trim');
      panelY(out, y + 0.02, 1, x0, x1, z0, z1, 'glass', 0.25, 'window.' + id, null, 'win');
      const zm = (z0 + z1) / 2; panelY(out, y + 0.025, 1, x0, x1, zm - 0.03, zm + 0.03, 'trim', 0.1, 'window.rail');
      panelY(out, y + 0.025, 1, (x0 + x1) / 2 - 0.025, (x0 + x1) / 2 + 0.025, z0, z1, 'trim', 0.1, 'window.mullion');
      box(out, x0 - 0.14, x1 + 0.14, y, y + 0.1, z0 - 0.14, z0 - 0.08, 'trim', 'window.sill');
    }

    const piece = {
      id: 'structure.nmc_community_hall', name: 'The community hall', defType: 'SetPieceDef', scene: null,
      layer: 'raised', walk: 'solid', mount: 'ground',
      footprints: [[[PL.X0, PL.Y0], [PL.X1, PL.Y0], [PL.X1, PL.Y1], [PL.X0, PL.Y1]]],
      defaults: { text: '', lights: true, cutaway: false },
      bake: { facings: [4], note: 'one facing: the hall never turns' },
      build(o) {
        const out = [], words = [], cut = !!o.cutaway, { X0, X1, Y0, Y1, ZF, ZE, ZR, OV, OG, CUT } = PL;
        const top = (z) => cut ? Math.min(z, CUT) : z;
        // ---- the foundation: a poured wall showing half a metre, the floor's height
        box(out, X0 - 0.05, X1 + 0.05, Y0 - 0.05, Y1 + 0.05, -0.2, ZF, 'conc', 'foundation', { noTop: true });
        // ---- the walls: white clapboard on the foundation. North and south (gables), east and west (eaves)
        Fw(out, [[X1, Y1, ZF], [X0, Y1, ZF], [X0, Y1, top(ZE)], [X1, Y1, top(ZE)]], 'paint', 0.1, 'wall.south', 'clap');
        Fw(out, [[X0, Y0, ZF], [X1, Y0, ZF], [X1, Y0, top(ZE)], [X0, Y0, top(ZE)]], 'paint', -0.12, 'wall.north', 'clap');
        Fw(out, [[X0, Y1, ZF], [X0, Y0, ZF], [X0, Y0, top(ZE)], [X0, Y1, top(ZE)]], 'paint', 0.02, 'wall.east', 'clap');
        Fw(out, [[X1, Y0, ZF], [X1, Y1, ZF], [X1, Y1, top(ZE)], [X1, Y0, top(ZE)]], 'paint', -0.1, 'wall.west', 'clap');
        if (!cut) {
          Fw(out, [[X1, Y1, ZE], [X0, Y1, ZE], [0, Y1, ZR]], 'paint', 0.1, 'gable.south', 'clap');
          Fw(out, [[X0, Y0, ZE], [X1, Y0, ZE], [0, Y0, ZR]], 'paint', -0.12, 'gable.north', 'clap');
          // corner boards, railway green, and a water table along the foot of the south wall
          for (const x of [X0, X1]) for (const y of [Y0, Y1]) box(out, x - 0.09, x + 0.09, y - 0.09, y + 0.09, ZF - 0.05, ZE, 'green', 'corner');
          box(out, X0 - 0.06, X1 + 0.06, Y1, Y1 + 0.05, ZF, ZF + 0.18, 'green', 'water.table');
        }

        // ---- THE SOUTH GABLE (the show face): three tall sashes, a fanlight louvre in the peak, the name board
        if (!cut) {
          for (const [x, id] of [[3.6, 'southW'], [0, 'southM'], [-3.6, 'southE']]) winS(out, Y1, x - 0.55, x + 0.55, 1.25, 3.15, id);
          // the peak's louvred vent: a pointed board with slats, the hall's one ornament
          panelY(out, Y1 + 0.02, 1, -0.75, 0.75, 5.55, 6.55, 'green', 0.1, 'gable.vent.frame');
          F(out, [[0.62, Y1 + 0.03, 5.65], [-0.62, Y1 + 0.03, 5.65], [-0.62, Y1 + 0.03, 6.4], [0.62, Y1 + 0.03, 6.4]], 'trim', 0.1, 'gable.vent', null, { uv: [[0, 5.65], [1.2, 5.65], [1.2, 6.4], [0, 6.4]], tex: TEX.louvre });
          F(out, [[0.75, Y1 + 0.02, 6.55], [-0.75, Y1 + 0.02, 6.55], [0, Y1 + 0.02, 7.15]], 'green', 0.1, 'gable.vent.hood');
          // the board under the vent (blank: the game letters it)
          const NB = [[1.9, Y1 + 0.07, 4.2], [-1.9, Y1 + 0.07, 4.2], [-1.9, Y1 + 0.07, 4.85], [1.9, Y1 + 0.07, 4.85]];
          box(out, -2.0, 2.0, Y1, Y1 + 0.06, 4.12, 4.93, 'green', 'sign.frame'); F(out, NB.slice(), 'board', 0.25, 'words.nmc_community_hall');
          words.push({ id: 'words.nmc_community_hall', text: o.text || '', corners: NB.slice().reverse(), note: 'the board under the south gable’s vent; blank in pictures' });
          // the eaves' barge boards and fascia along the gables' rakes (railway green)
          const ez = ZE - OV * PL.SL, xa = X0 - OV, xb = X1 + OV;
          for (const y of [Y0 - OG, Y1 + OG]) { const s = y > 0 ? 1 : -1;
            for (const [xA, xB] of [[xa, 0], [xb, 0]]) F(out, [[xA, y, ez - 0.22], [xB, y, ZR - 0.22], [xB, y, ZR], [xA, y, ez]], 'green', s > 0 ? 0.1 : -0.1, 'barge'); }
        }

        // ---- THE WEST WALL (the way in): the door, the entry hood on two posts, the stoop and steps, the lamp, the notice board
        const D = PL.door, P = PL.porch, yd0 = D.y - D.w / 2, yd1 = D.y + D.w / 2;
        // the door (seen edge on from the camera; it is still drawn, for the other facings and the review)
        F(out, [[X1 + 0.02, yd0 - 0.12, ZF], [X1 + 0.02, yd1 + 0.12, ZF], [X1 + 0.02, yd1 + 0.12, ZF + top(D.h + 0.12)], [X1 + 0.02, yd0 - 0.12, ZF + top(D.h + 0.12)]], 'trim', 0, 'door.frame');
        F(out, [[X1 + 0.03, yd0, ZF], [X1 + 0.03, yd1, ZF], [X1 + 0.03, yd1, ZF + top(D.h)], [X1 + 0.03, yd0, ZF + top(D.h)]], 'green', 0, 'door.leaf');
        // the stoop: a concrete landing at floor height, two steps down to the path
        box(out, X1, X1 + 1.1, P.y0 + 0.1, P.y1 - 0.1, 0, ZF, 'conc', 'stoop');
        box(out, X1 + 1.1, X1 + 1.4, P.y0 + 0.25, P.y1 - 0.25, 0, ZF * 0.66, 'conc', 'step');
        box(out, X1 + 1.4, PL.steps.x, P.y0 + 0.25, P.y1 - 0.25, 0, ZF * 0.33, 'conc', 'step');
        if (!cut) {
          // two posts, a beam, and a little gable roof with its ridge running out from the wall (its south slope faces the camera)
          for (const y of [P.y0 + 0.12, P.y1 - 0.12]) box(out, P.x - 0.16, P.x, y - 0.08, y + 0.08, ZF, ZF + P.z - 0.5, 'trim', 'porch.post');
          box(out, X1, P.x + 0.05, P.y0, P.y1, ZF + P.z - 0.5, ZF + P.z - 0.32, 'trim', 'porch.beam');
          const zb = ZF + P.z - 0.32, zt = zb + P.rise, ym = (P.y0 + P.y1) / 2, xo = P.x + 0.2;
          F(out, [[X1, P.y1 + 0.25, zb], [xo, P.y1 + 0.25, zb], [xo, ym, zt], [X1, ym, zt]], 'roofAsph', 0.12, 'porch.roof', null, { uv: [[0, 0], [1.6, 0], [1.6, 1.6], [0, 1.6]], tex: TEX.shingles });
          F(out, [[xo, P.y0 - 0.25, zb], [X1, P.y0 - 0.25, zb], [X1, ym, zt], [xo, ym, zt]], 'roofAsph', -0.05, 'porch.roof', null, { uv: [[0, 0], [1.6, 0], [1.6, 1.6], [0, 1.6]], tex: TEX.shingles });
          F(out, [[xo, P.y1 + 0.25, zb], [xo, P.y0 - 0.25, zb], [xo, ym, zt]], 'paint', -0.1, 'porch.gable', null, { uv: [[0, zb], [2.9, zb], [1.45, zt]], tex: TEX.clap });
          for (const s of [-1, 1]) { const ye = s > 0 ? P.y1 + 0.25 : P.y0 - 0.25; F(out, [[xo, ye, zb - 0.16], [xo, ym, zt - 0.16], [xo, ym, zt], [xo, ye, zb]], 'green', 0, 'porch.barge'); }
          panelY(out, P.y1 + 0.25, 1, X1, xo, zb - 0.16, zb, 'green', 0.1, 'porch.fascia');
          // the door lamp under the hood (it glows with o.lit), the notice board on the wall south of the door (blank)
          box(out, X1, X1 + 0.24, D.y - 0.15, D.y + 0.15, ZF + 2.62, ZF + 2.72, 'iron', 'lamp.door.hood');
          box(out, X1 + 0.04, X1 + 0.2, D.y - 0.1, D.y + 0.1, ZF + 2.48, ZF + 2.62, 'lampW', 'lamp.door.lens', { em: 'lamp' });
          const NBw = [[X1 + 0.05, 1.05, ZF + 1.15], [X1 + 0.05, 1.95, ZF + 1.15], [X1 + 0.05, 1.95, ZF + 1.95], [X1 + 0.05, 1.05, ZF + 1.95]];
          box(out, X1, X1 + 0.04, 0.98, 2.02, ZF + 1.08, ZF + 2.02, 'wood', 'notice.frame'); F(out, NBw.slice(), 'board', 0, 'words.nmc_community_hall_notice');
          words.push({ id: 'words.nmc_community_hall_notice', text: '', corners: NBw.slice(), note: 'the notice board beside the door (hall bookings, the gym’s hours); the game’s to letter' });
          // a bench on the south side of the stoop and a galvanised bin
          box(out, X1 + 0.3, X1 + 0.7, P.y1 + 0.5, P.y1 + 2.1, 0.42, 0.48, 'wood', 'bench'); for (const y of [P.y1 + 0.65, P.y1 + 1.95]) box(out, X1 + 0.34, X1 + 0.66, y - 0.04, y + 0.04, 0, 0.42, 'iron', 'bench.leg');
          cyl(out, X1 + 0.6, P.y0 - 0.9, 0, 0.75, 0.27, 0.29, 10, 'galv', 'bin'); cyl(out, X1 + 0.6, P.y0 - 0.9, 0.75, 0.8, 0.31, 0.31, 10, 'galv', 'bin.lid');
          // the east wall's gym windows and the west wall's hall windows (edge on from the camera; drawn for completeness)
          for (const y of [-7.2, -4.8, -2.4]) F(out, [[X0 - 0.02, y - 0.5, 1.4], [X0 - 0.02, y + 0.5, 1.4], [X0 - 0.02, y + 0.5, 3.2], [X0 - 0.02, y - 0.5, 3.2]], 'glass', 0, 'window.gym', 'win');
          for (const y of [-6.5, -3.4, 3.0, 6.0]) F(out, [[X1 + 0.02, y - 0.5, 1.4], [X1 + 0.02, y + 0.5, 1.4], [X1 + 0.02, y + 0.5, 3.2], [X1 + 0.02, y - 0.5, 3.2]], 'glass', 0, 'window.hall', 'win');
        }

        if (!cut) {
          // ---- THE ROOF: two asphalt slopes (east and west), the ridge cap, the vent cupola, the kitchen's brick chimney
          const ez = ZE - OV * PL.SL, xa = X0 - OV, xb = X1 + OV, ya = Y0 - OG, yb = Y1 + OG;
          Fr(out, [[xb, ya, ez], [xb, yb, ez], [0, yb, ZR], [0, ya, ZR]], 'roofAsph', -0.05, 'roof.west', 'shingles');
          Fr(out, [[xa, yb, ez], [xa, ya, ez], [0, ya, ZR], [0, yb, ZR]], 'roofAsph', 0.0, 'roof.east', 'shingles');
          box(out, -0.18, 0.18, ya - 0.02, yb + 0.02, ZR - 0.08, ZR + 0.08, 'roofAsph', 'ridge.cap');
          // the ventilator cupola on the ridge, a third of the way from the south gable: louvred, a pyramid cap
          { const cy = 3.0, h = 0.6, zb = ZR - 0.25, zt = ZR + 1.05;
            for (const [v, b, tg] of [[[[h, cy + h, zb], [-h, cy + h, zb], [-h, cy + h, zt], [h, cy + h, zt]], 0.1, 's'], [[[-h, cy - h, zb], [h, cy - h, zb], [h, cy - h, zt], [-h, cy - h, zt]], -0.1, 'n'],
              [[[h, cy - h, zb], [h, cy + h, zb], [h, cy + h, zt], [h, cy - h, zt]], 0, 'w'], [[[-h, cy + h, zb], [-h, cy - h, zb], [-h, cy - h, zt], [-h, cy + h, zt]], 0, 'e']])
              F(out, v, 'paint', b, 'cupola.' + tg, null, { uv: v.map(p => [p[0] + p[1], p[2]]), tex: TEX.louvre });
            for (const [x, y] of [[h, cy + h], [-h, cy + h], [h, cy - h], [-h, cy - h]]) box(out, x - 0.06, x + 0.06, y - 0.06, y + 0.06, zb, zt, 'green', 'cupola.post');
            const c = h + 0.15, zc = zt + 0.6;
            F(out, [[c, cy + c, zt], [-c, cy + c, zt], [0, cy, zc]], 'green', 0.12, 'cupola.cap'); F(out, [[-c, cy - c, zt], [c, cy - c, zt], [0, cy, zc]], 'green', -0.1, 'cupola.cap');
            F(out, [[c, cy - c, zt], [c, cy + c, zt], [0, cy, zc]], 'green', 0, 'cupola.cap'); F(out, [[-c, cy + c, zt], [-c, cy - c, zt], [0, cy, zc]], 'green', 0, 'cupola.cap');
            stick(out, [0, cy, zc], [0, cy, zc + 0.5], 0.04, 'iron', 'cupola.finial'); }
          // the kitchen's chimney on the east slope (over the kitchen, local y [-1, 4])
          { const x0 = -3.3, x1 = -2.5, y0 = 0.8, y1 = 1.6, zb = ZR - PL.SL * 3.3 - 0.1, zt = ZR + 0.7;
            for (const [v, b] of [[[[x1, y1, zb], [x0, y1, zb], [x0, y1, zt], [x1, y1, zt]], 0.1], [[[x0, y0, zb], [x1, y0, zb], [x1, y0, zt], [x0, y0, zt]], -0.1],
              [[[x1, y0, zb], [x1, y1, zb], [x1, y1, zt], [x1, y0, zt]], 0], [[[x0, y1, zb], [x0, y0, zb], [x0, y0, zt], [x0, y1, zt]], 0]])
              F(out, v, 'brick', b, 'chimney', null, { uv: v.map(p => [p[0] + p[1], p[2]]), tex: TEX.brick });
            box(out, x0 - 0.06, x1 + 0.06, y0 - 0.06, y1 + 0.06, zt, zt + 0.1, 'conc', 'chimney.cap');
            box(out, (x0 + x1) / 2 - 0.12, (x0 + x1) / 2 + 0.12, (y0 + y1) / 2 - 0.12, (y0 + y1) / 2 + 0.12, zt + 0.1, zt + 0.3, 'cast', 'chimney.pot'); }
          // the gym's exhaust vent on the east slope over the gym, and a plumbing vent over the washroom
          cyl(out, -2.2, -5.0, ZR - PL.SL * 2.2 - 0.05, ZR - PL.SL * 2.2 + 0.4, 0.26, 0.26, 10, 'galv', 'vent.gym');
          cyl(out, -2.2, -5.0, ZR - PL.SL * 2.2 + 0.4, ZR - PL.SL * 2.2 + 0.5, 0.36, 0.1, 10, 'galv', 'vent.gym.cap');
          cyl(out, -3.8, 6.4, ZR - PL.SL * 3.8 - 0.05, ZR - PL.SL * 3.8 + 0.5, 0.06, 0.06, 6, 'cast', 'vent.soil');
          // gutters along both eaves, two downpipes on the south corners
          for (const x of [xa, xb]) box(out, x - 0.08, x + 0.08, ya, yb, ez - 0.14, ez, 'galv', 'gutter');
          for (const x of [X0 + 0.2, X1 - 0.2]) { const xg = x < 0 ? xa : xb; stick(out, [xg, Y1 + 0.3, ez - 0.14], [x, Y1 + 0.1, ez - 0.5], 0.08, 'galv', 'downpipe'); stick(out, [x, Y1 + 0.1, ez - 0.5], [x, Y1 + 0.1, 0.05], 0.08, 'galv', 'downpipe'); }
        } else {
          // ---- THE CUTAWAY: the shell's four rooms as floor, the partitions at the cut height, the stage, the outer walls' cut tops
          for (const [key, , x0, x1, y0, y1, mat, tex] of ZONES) Ff(out, [[x0, y0, ZF + 0.01], [x1, y0, ZF + 0.01], [x1, y1, ZF + 0.01], [x0, y1, ZF + 0.01]], mat, 0, 'floor.' + key, tex);
          const wall = (x0, x1, y0, y1) => box(out, x0, x1, y0, y1, ZF, ZF + CUT, 'trim', 'partition');
          // the back wall (x 0) with the gym's doorway (centred y -5, 1.4 m), the gym-kitchen wall (y -1), the kitchen-washroom wall (y 4)
          wall(-0.15, 0.15, -9, -5.7); wall(-0.15, 0.15, -4.3, 9); wall(-6, -0.15, -1.15, -0.85); wall(-6, -0.15, 3.85, 4.15);
          // the stage at the south end of the front hall: a low platform with two steps
          box(out, 1, 5, 6.5, 8.7, ZF, ZF + 0.6, 'maple', 'stage'); box(out, 1, 5, 6.2, 6.5, ZF, ZF + 0.3, 'maple', 'stage.step');
          panelY(out, 8.7, -1, 1, 5, ZF + 0.6, ZF + 0.62, 'wood', 0, 'stage.edge');
          // the outer walls' cut tops, the front door left open in the west wall
          const cz = ZF + CUT;
          for (const [x0, x1, y0, y1] of [[X0, X1, Y0, Y0 + 0.2], [X0, X1, Y1 - 0.2, Y1], [X0, X0 + 0.2, Y0, Y1], [X1 - 0.2, X1, Y0, yd0], [X1 - 0.2, X1, yd1, Y1]])
            Ff(out, [[x0, y0, cz], [x1, y0, cz], [x1, y1, cz], [x0, y1, cz]], 'cast', 0, 'wall.cut', null);
        }

        // ---- the anchors: local metres. The door stands 1.2 m outside its threshold, on the ground.
        const A = { door: [X1 + 1.2, D.y, 0], threshold: [X1, D.y, ZF], gymDoor: [0, -5, ZF], stage: [3, 7.6, ZF + 0.6],
          lights: cut ? [] : [{ id: 'light.hall_door', at: [X1 + 0.12, D.y, ZF + 2.55], kind: 'door lamp under the entry hood' }],
          words, note: 'local x = -(world east), local y = -(world north); the pivot is the shell’s centre, world (-153, 62.5)' };
        const mats = M([...new Set(out.map(f => f.mat))]);
        return { faces: out, mats, anchors: A };
      },
      notes: 'Stand it at dir 4 with its pivot on the building’s centre, world (-153, 62.5): the show face is the south gable; the door is on the west wall, square on to the lot’s west gate. Windows and the door lamp glow with o.lit. o.cutaway is the walk-in floor plan (the Interior sprite), not furnished art: the gym’s equipment stays the shell’s Def-sized markers.',
      extra() { return { programme: { source: 'NmcBuildingShells.CommunityHall (PR 1276), docs/world/nmc-gym.md', floors: 1, grossM2: 216,
        zones: ZONES.map(z => ({ key: z[0], name: z[1], local: { x: [z[2], z[3]], y: [z[4], z[5]] }, m2: (z[3] - z[2]) * (z[5] - z[4]) })) },
        doors: [{ id: 'front', wall: 'W', local: [PL.X1, PL.door.y, 0], width: PL.door.w, world: [-159, 62.5 + 0.5 * Math.sin(40 * Math.PI / 180)] }] }; },
    };
    NF.PIECES.communityHall = piece;
    return 'communityHall';
  }

  const API = { VERSION, RAMPS, TEX, PL, ZONES, install };
  root.NmcCommunityHall = API;
  if (typeof module !== 'undefined' && module.exports) module.exports = API;
})(typeof globalThis !== 'undefined' ? globalThis : this);
