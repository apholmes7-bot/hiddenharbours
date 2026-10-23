/* Hidden Harbours — ROADS v4 · the review scenes.  Four places on the pass-9 floor, 520 × 450 px (16 × 14 m),
   each a route table the way the municipal plan keeps one (§2.1: one polyline table, three consumers):

     stpeters    St Peters · the school lane      lane (ruts, grass crown) · footpath · alley · stepping stones ·
                                                  a plank walk over the sedge. No kerb, no paving (CAMERA-FIRST 12).
     route91     Nine Mile Creek · Route 91       asphalt route on a bend: centre line double where it curves,
                                                  edge lines, shoulders, the ditch (plan §6.2 #5), a gravel drive
                                                  over it at the culvert spot, a shell walk to the lot.
     wharfroad   Nine Mile Creek · Wharf Road     paved street (ruled 09-29), its shoulder and verge, a boardwalk over the
                                                  dune, a sand trail through the marram, a shell walk.
     finnigans   Finnigan's Landing · Water St    the city (plan §5): kerbs, gutters, slab and brick sidewalks, a
                                                  T with 2.2 m kerb returns, crosswalks, a stop bar, a sett lane.

   RoadScenes4.build(key, {routes}) -> C = {tile, zone, zones, zi, w, h, relief, far, net, pz, PZ, pzi}.
   The floor is PxKit8.floor over the scene's zones AFTER RoadKit4.zones has written the soft routes in, then
   RoadKit4.paint. Plant zones (pz) keep planting off the paving: road · meadow · edge · verge · ditch · bank ·
   sedge · dune · beach · yard.                                                                              */
(function (root) {
  'use strict';
  const P = root.PxLang, K = root.PxKit, K8 = root.PxKit8, R4 = root.RoadKit4, TL = root.TerrainLight5;
  if (!P || !K || !K8 || !R4 || !TL) throw new Error('roadScenes4: the pass-9 kit and roadPathRig4 must load first');
  const { clamp } = P, MATS = K.MATS, vn = TL.vn, RI = R4.RI;
  const fb = (x, y, s) => 0.55 * vn(x, y, s) + 0.3 * vn(x * 2.1 + 5.3, y * 2.1 + 1.7, s + 1) + 0.15 * vn(x * 4.3 + 2.1, y * 4.3 + 7.9, s + 2);
  const W = 520, Hh = 450;
  const idx = (Z) => { const zi = {}; Z.forEach((k, i) => { zi[k] = i; }); return zi; };
  const PZ = ['road', 'meadow', 'edge', 'verge', 'ditch', 'bank', 'sedge', 'dune', 'beach', 'yard'];

  const SCENES = {
    stpeters: {
      name: 'St Peters · the school lane', region: 'St Peters', seed: 61,
      note: 'The island’s one street is a lane: two wheel ruts with mud in their bottoms, a grass crown the kit’s own edge painter frays, a worn footpath down to the slip, a faint alley behind the saltbox, stepping stones to a dooryard and a plank walk over the sedge. No kerb and no paving, per CAMERA-FIRST rule 12.',
      zones: ['grass', 'dirt', 'sedge', 'path'],
      terrain(x, y) {
        if (Math.hypot((x - 455) / 54, (y - 214) / 30) - 1 - 0.5 * (fb(x / 20, y / 16, 3) - 0.5) < 0) return 'sedge';
        if (Math.hypot((x - 150) / 66, (y - 24) / 32) - 1 - 0.5 * (fb(x / 18, y / 14, 4) - 0.5) < 0) return 'dirt';
        return 'grass';
      },
      routes: [
        { id: 'stpeters.school_lane', cls: 'lane', pts: [[-30, 424], [90, 376], [205, 322], [300, 252], [372, 170], [428, 90], [466, -30]] },
        { id: 'stpeters.slip_path', cls: 'footpath', w: 1.1, pts: [[262, 290], [238, 350], [262, 408], [248, 480]] },
        { id: 'stpeters.saltbox_alley', cls: 'alley', pts: [[246, 292], [200, 270], [140, 262], [80, 252], [-20, 246]] },
        { id: 'stpeters.hollow_path', cls: 'footpath', w: 0.9, pts: [[372, 216], [410, 212], [455, 211], [500, 209], [548, 204]] },
        { id: 'stpeters.plank_crossing', cls: 'walk', surf: 'planks', w: 0.66, pts: [[408, 212], [455, 211], [502, 209]] },
        { id: 'stpeters.saltbox_walk', cls: 'walk', surf: 'stones', w: 0.9, pts: [[190, 318], [176, 250], [168, 170], [158, 84], [150, 30]] },
      ],
      trees: [['WhiteBirch', 330, 146, 'young', 1], ['RedSpruce', 60, 170, 'young', 3], ['BalsamFir', 40, 300, 'sapling', 2], ['RedMaple', 480, 446, 'sapling', 0]],
      adds: [['Lupin', 'meadow', 16, { gap: 12 }], ['Lupin', 'edge', 5, { gap: 12 }], ['OxeyeDaisy', 'meadow', 12, { gap: 9 }], ['Buttercup', ['meadow', 'edge'], 10, { gap: 10 }],
        ['Timothy', 'meadow', 12, { gap: 10 }], ['MeadowGrass', ['meadow', 'edge'], 18, { gap: 10 }], ['QueenAnne', 'meadow', 6, { gap: 10 }], ['Fireweed', 'meadow', 4, { gap: 11 }],
        ['Goldenrod', 'meadow', 5, { gap: 11 }], ['SoftRush', 'sedge', 6, { gap: 10 }], ['BlueFlag', 'sedge', 4, { gap: 9 }], ['TussockSedge', 'sedge', 3, { gap: 14 }],
        ['Raspberry', 'yard', 2, { gap: 30 }], ['WildRose', 'meadow', 3, { gap: 28 }]],
    },
    route91: {
      name: 'Nine Mile Creek · Route 91', region: 'Nine Mile Creek', seed: 73,
      note: 'Route 91 through a bend: the centre line goes double where the radius drops under 22 m and dashes where it straightens, white edge lines follow the carriage edge, gravel shoulders, and the ditch the plan asks for, a shallow V with sedge and rush in its bottom that holds water after rain. A gravel drive fills the ditch where it crosses (the culvert spot); a crushed-shell walk runs to the lot.',
      zones: ['grass', 'dirt', 'sedge', 'path'],
      terrain(x, y) { return Math.hypot((x - 480) / 76, (y - 474) / 42) - 1 - 0.4 * (fb(x / 16, y / 12, 5) - 0.5) < 0 ? 'dirt' : 'grass'; },
      routes: [
        { id: 'nmc.route_91', cls: 'route', pts: [[-40, 262], [96, 250], [206, 222], [288, 166], [334, 86], [350, -40]] },
        { id: 'nmc.lot_drive', cls: 'lane', surf: 'gravel', w: 3.0, crown: 0, ruts: null, zone: 'path', wear: 0.3, snow: 'plough', pts: [[300, 214], [368, 272], [418, 350], [440, 480]] },
        { id: 'nmc.lot_walk', cls: 'walk', surf: 'shell', w: 1.3, pts: [[426, 384], [380, 402], [338, 434], [322, 480]] },
      ],
      trees: [['RedMaple', 498, 150, 'young', 0], ['WhiteBirch', 40, 66, 'sapling', 1], ['BalsamFir', 60, 446, 'young', 2], ['TremblingAspen', 506, 318, 'sapling', 3]],
      adds: [['Lupin', 'meadow', 10, { gap: 12 }], ['QueenAnne', ['meadow', 'edge'], 8, { gap: 10 }], ['Goldenrod', 'meadow', 8, { gap: 11 }], ['Fireweed', 'meadow', 6, { gap: 11 }],
        ['OxeyeDaisy', ['meadow', 'bank'], 8, { gap: 9 }], ['Timothy', ['meadow', 'bank'], 12, { gap: 10 }], ['MeadowGrass', ['meadow', 'bank', 'edge'], 16, { gap: 10 }],
        ['SoftRush', 'ditch', 12, { gap: 9 }], ['BlueFlag', 'ditch', 6, { gap: 9 }], ['Cattail', 'ditch', 3, { gap: 14 }], ['WildRose', 'meadow', 3, { gap: 26 }], ['StaghornSumac', 'meadow', 1, { gap: 50 }]],
    },
    wharfroad: {
      name: 'Nine Mile Creek · Wharf Road', region: 'Nine Mile Creek', seed: 89,
      note: 'Wharf Road in gravel: wheel tracks packed pale, loose windrows between them and at the edges, washboard across the tracks that only low sun finds, potholes that fill. A boardwalk stands 0.35 m over the dune on creosote piles, its planks square to its curve; a sand trail runs through the marram and a shell walk goes down to the lot.',
      zones: ['grass', 'marram', 'sand', 'dirt', 'path'],
      terrain(x, y) {
        const b1 = 100 + 14 * Math.sin(x / 60) + 10 * (fb(x / 40, 1.5, 6) - 0.5) * 2, b2 = 214 + 10 * Math.sin(x / 77 + 1) + 8 * (fb(x / 36, 4.5, 7) - 0.5) * 2;
        return y < b1 ? 'sand' : y < b2 ? 'marram' : 'grass';
      },
      routes: [
        { id: 'nmc.wharf_road', cls: 'paved', pts: [[-40, 312], [110, 298], [250, 316], [390, 302], [560, 278]], bands: [{ role: 'shoulder', w: 0.5 }, { role: 'verge', w: 0.4, z: 0, z1: 0 }] },
        { id: 'nmc.dune_boardwalk', cls: 'boardwalk', pts: [[250, 226], [240, 190], [258, 150], [292, 116], [312, 80], [318, 46]] },
        { id: 'nmc.dune_trail', cls: 'footpath', zone: 'sand', w: 1.0, pts: [[430, 222], [440, 180], [424, 140], [436, 90], [444, 40]] },
        { id: 'nmc.fish_market_walk', cls: 'walk', surf: 'shell', w: 1.4, pts: [[150, 392], [146, 430], [156, 480]] },
      ],
      trees: [['BalsamFir', 40, 446, 'sapling', 2], ['WhiteBirch', 470, 446, 'sapling', 1], ['RedSpruce', 330, 446, 'sapling', 0]],
      adds: [['MarramGrass', 'dune', 26, { gap: 13 }], ['BeachPea', 'dune', 4, { gap: 24 }], ['Bayberry', 'dune', 2, { gap: 40 }], ['SweetFern', 'dune', 2, { gap: 26 }],
        ['WildRose', ['edge', 'meadow'], 4, { gap: 26 }], ['Goldenrod', 'meadow', 5, { gap: 11 }], ['MeadowGrass', ['meadow', 'edge', 'verge'], 12, { gap: 10 }],
        ['Timothy', 'meadow', 6, { gap: 10 }], ['OxeyeDaisy', 'meadow', 5, { gap: 9 }], ['Lupin', 'meadow', 4, { gap: 12 }]],
    },
    finnigans: {
      name: 'Finnigan’s Landing · Water Street', region: 'the city', seed: 97,
      note: 'The first place with kerbs, as the plan keeps them (§5.2): granite kerbstones, a concrete gutter pan that holds the rain, slab sidewalks whose joints fan round the bends, brick on Chapel Street. The T meets in 2.2 m kerb returns the sidewalk wraps; crosswalks and a stop bar sit off the junction the rig finds, and a sett lane drops the kerb it crosses.',
      zones: ['grass', 'dirt'],
      terrain() { return 'grass'; },
      routes: [
        { id: 'finnigans.water_street', cls: 'town', crosswalk: 'a', pts: [[-40, 200], [120, 192], [260, 204], [400, 198], [560, 184]] },
        { id: 'finnigans.chapel_street', cls: 'town', w: 5.2, walkSurf: 'brick', crosswalk: 'a', stop: true, pts: [[334, 490], [338, 410], [324, 330], [304, 262], [296, 200]] },
        { id: 'finnigans.rope_walk', cls: 'lane', surf: 'setts', w: 3.0, zone: null, crown: 0, ruts: null, snow: 'plough', pts: [[96, 250], [80, 330], [58, 400], [44, 480]] },
      ],
      trees: [['RedMaple', 190, 446, 'young', 0], ['RedOak', 470, 446, 'sapling', 3], ['WhiteBirch', 440, 30, 'sapling', 1]],
      adds: [['OxeyeDaisy', 'meadow', 6, { gap: 10 }], ['Buttercup', 'meadow', 8, { gap: 10 }], ['MeadowGrass', ['meadow', 'verge'], 10, { gap: 11 }], ['Timothy', 'meadow', 4, { gap: 11 }], ['Lupin', 'meadow', 3, { gap: 12 }], ['WildRose', 'meadow', 2, { gap: 30 }]],
    },
  };

  function paintFloor(Z, zone, sd) {
    const zi = idx(Z), N = W * Hh, seen = new Uint8Array(Z.length);
    for (let j = 0; j < N; j++) seen[zone[j]] = 1;
    const defs = Z.filter((k, i) => seen[i] && MATS[k]).map(k => ({ key: k, step: 1, seed: (MATS[k].seed || 1) + sd * 13, R: (x, y) => x >= 0 && y >= 0 && x < W && y < Hh && zone[y * W + x] === zi[k] }));
    return K8.floor(W, Hh, defs, sd + 5);
  }
  function dilate(M, w, h, r) {
    const T = new Uint8Array(w * h), O = new Uint8Array(w * h);
    for (let y = 0; y < h; y++) { const o = y * w; let c = 0; for (let x = 0; x < Math.min(w, r); x++) c += M[o + x]; for (let x = 0; x < w; x++) { if (x + r < w) c += M[o + x + r]; if (x - r - 1 >= 0) c -= M[o + x - r - 1]; T[o + x] = c > 0 ? 1 : 0; } }
    for (let x = 0; x < w; x++) { let c = 0; for (let y = 0; y < Math.min(h, r); y++) c += T[y * w + x]; for (let y = 0; y < h; y++) { if (y + r < h) c += T[(y + r) * w + x]; if (y - r - 1 >= 0) c -= T[(y - r - 1) * w + x]; O[y * w + x] = c > 0 ? 1 : 0; } }
    return O;
  }
  function build(key, o) {
    o = o || {}; const S = SCENES[key] || SCENES.stpeters, N = W * Hh, sd = S.seed, t0 = Date.now();
    const net = R4.network(o.routes || S.routes, { w: W, h: Hh, seed: sd });
    const Z = S.zones, zi = idx(Z), zone = new Uint8Array(N);
    for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) zone[y * W + x] = zi[S.terrain(x, y)] | 0;
    R4.zones(net, zone, zi);
    const fl = paintFloor(Z, zone, sd);
    R4.paint(fl.tile, net);
    const pzi = idx(PZ), pz = new Uint8Array(N), road = new Uint8Array(N);
    for (let i = 0; i < N; i++) { const r = net.role[i]; road[i] = r && r !== RI.verge && r !== RI.ditch ? 1 : 0; }
    const near = dilate(road, W, Hh, 8);
    for (let i = 0; i < N; i++) {
      const r = net.role[i]; let k;
      if (road[i]) k = 'road'; else if (r === RI.verge) k = 'verge';
      else if (r === RI.ditch) k = Math.abs(net.A[i] / net.BW[i] - 0.5) < 0.22 ? 'ditch' : 'bank';
      else if (near[i]) k = 'edge';
      else { const z = Z[zone[i]]; k = z === 'sedge' || z === 'mud' ? 'sedge' : z === 'marram' ? 'dune' : z === 'sand' ? 'beach' : z === 'dirt' || z === 'path' ? 'yard' : 'meadow'; }
      pz[i] = pzi[k];
    }
    return { key, def: S, tile: fl.tile, reg: fl.reg, mats: fl.mats, zone, zones: Z, zi, w: W, h: Hh, relief: 1.2 * K.DETAIL, far: (y) => clamp(1 - y / Hh, 0, 1), net, pz, PZ, pzi, ms: Date.now() - t0 };
  }

  root.RoadScenes4 = { W, H: Hh, SCENES, PZ, build };
})(typeof globalThis !== 'undefined' ? globalThis : window);
