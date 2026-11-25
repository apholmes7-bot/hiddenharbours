/* Parametric SMALL SHED rig: the little clapboard shed that comes with the camper lot on Ginny's
   land (owner 2026-11-17: the camper and a small shed hold more rods and more gear). One building,
   outside and in: a gable roof, red clapboard with white corner boards, a plank door on the south
   eave wall, a small window in the east gable, and inside an upright rack for 8 rods and a 4-bay,
   3-shelf gear store (12 slots), with room for him to stand.

   Same turntable, camera and shading recipe as the fleet: 45deg steps, elev 40deg, flat-facet shading
   from the fixed key, z-buffered, ordered dither, depth-edge darkening, NO AA, ringless (ADR 0031).
   32 px = 1 m. Self-contained: no other rig needs to be loaded first.

   FRAME: metres, origin at the ground-centre of the WALL footprint, +x east (along the camper's long
   axis on the lot), +y north, +z up from the ground. The door is on the -y (south) wall. Heights in
   LAYOUT are ABOVE THE FLOOR, which stands BODY.floorZ above the ground on skids.

   ON THE LOT (C# StPetersCamperLot is the truth; the tests re-derive every number): rig -y is
   ShedDoorDirection (south today), rig +x is its right-handed perpendicular (east today), and the
   shed's plan, eaves included, is exactly the reserved ShedSize, inside ShedFootprintRadiusMetres.

   CUTAWAY (the boat-cutaway and camper-room convention, ADR 0041/0048): the roof is the lid and is
   culled; any wall panel or tall fixture part whose outward plan normal o has o . view > 0.2 is
   sectioned down to a 0.60 m sill above the floor. view = (-sin th, -cos th) is the plan direction
   toward the camera. render(dir) draws the closed shed; render(dir, {cut:true}) the room.

   Exposes globalThis.SmallShed = { W,H,PX,DIRS,pivot,order,defaultElev, BODY, WALKER, LAYOUT,
     STORE, RESERVE, list(), resolve(opts), fixtures(opts), walkable(opts), slots(opts),
     interactables(opts), fit(opts), lot(opts), cutaway(dir), faces(opts), render(dir,opts),
     project(dir,p,elev), anchors(dir,opts), validate(opts), sidecar(opts) }. */
(function (root) {
  'use strict';
  const PX = 32, S = 32;
  const W = 160, H = 176, cx = 80, groundY = 120;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const ORDER = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  const r3 = (v) => +(+v).toFixed(3);

  // ---- the fleet's shading recipe (camperIsoRig.js / houseIsoRig.js) ----
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v = [-0.42, 0.72, 0.52]; const m = Math.hypot(...v); return v.map((c) => c / m); })();
  const BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]].map((r) => r.map((v) => (v + 0.5) / 16));

  // ---- the building (art source) ----
  const BODY = {
    len: 2.20,        // outer wall length, east-west (rig x)
    dep: 1.60,        // outer wall depth, north-south (rig y)
    wall: 0.08,       // clapboard over studs
    baseZ: 0.04,      // the walls stand on corner blocks this high
    floorZ: 0.18,     // floor on skids, above the ground
    plateZ: 2.20,     // wall plate above the ground (outer face)
    pitch: 8 / 12,    // roof rise per run
    eave: 0.10,       // roof overhang past the eave walls (north, south)
    rake: 0.10,       // roof overhang past the gables (east, west)
    roofT: 0.05,      // roof boards and shingles
    rafter: 0.10,     // rafter depth under the roof boards (headroom is measured under it)
    sill: 0.60,       // cutaway: near walls are sectioned to this height above the floor
    nearDot: 0.20,    // o . view above this = a near wall
    // the door, on the south wall: clear span along x, clear height above the floor
    door: { u0: -0.10, u1: 0.66, h: 1.85, leafT: 0.04, hinge: 'west', swing: 'out' },
    // the window, in the east gable: span along y, sill and head above the floor
    window: { u0: -0.24, u1: 0.24, z0: 1.20, z1: 1.62 },
    // the step outside the door: a granite slab
    step: { x0: -0.06, x1: 0.62, y0: -1.08, y1: -0.80, h: 0.09 },
  };
  const XO = BODY.len / 2, YO = BODY.dep / 2;                 // outer half extents
  const XI = r3(XO - BODY.wall), YI = r3(YO - BODY.wall);      // inner half extents

  // The player as the game builds him: CircleCollider2D radius 0.35 m (PersistentCoreBuilder;
  // QuayEdgePlan.WalkerFootRadiusMetres). Reach rules are the boat-interior kit's, as the camper room.
  const WALKER = { footRadius: 0.35, reachClear: 0.20, reachSpacing: 0.25, interactReach: 0.25 };

  // The ground the camper lot reserves for this shed (StPetersCamperLot.ShedSize, along x across y)
  // and its any-facing circle. The plan, eaves included, must fit both.
  const RESERVE = { along: 2.4, across: 1.8 };
  RESERVE.radius = r3(Math.hypot(RESERVE.along, RESERVE.across) / 2);

  // The interior fit-out. wall: which wall it stands against (north = +y, south = -y, east = +x,
  // west = -x); it drives the cutaway.
  const LAYOUT = [
    { id: 'rod_rack', kind: 'rack', wall: 'west', x0: -XI, x1: -0.80, y0: -0.48, y1: 0.48, h: 2.10,
      note: 'eight rods standing upright in a floor rail, held at the top by a notched rail on the west gable, where the roof is highest' },
    { id: 'gear_shelf', kind: 'shelf', wall: 'north', x0: -0.72, x1: XI, y0: 0.38, y1: YI, h: 1.80,
      note: 'open shelving along the north wall: four bays, three shelves, one gear slot per bay per shelf' },
  ];

  // What the stores hold. DATA: the capacities live here and in the stowage Defs (PR 1307, not on main yet); the save rows are
  // the storage-with-memory lane's (save v25), not this rig's.
  const STORE = {
    rod_rack: { stowage: 'stowage.shed_rod_rack', status: 'Def in PR 1307 (storage-with-memory lane), not on main yet',
      accepts: 'Rods', capacity: 8, pitch: 0.12, rodTop: 2.10, minRod: 2.10 },
    gear_shelf: { stowage: 'stowage.shed_gear_locker', status: 'Def in PR 1307 (storage-with-memory lane), not on main yet',
      accepts: 'General', capacity: 12, bays: 4, shelves: [0.30, 0.85, 1.40], board: 0.02, upright: 0.03 },
  };

  // where he stands: inside the door, outside it, and the spot that proves there is room to stand
  const SPOTS = {
    insideLanding: [0.28, -0.36],
    outsideLanding: [0.28, -1.44],
    stand: [0.12, -0.16],
    reach: { rod_rack: [-0.56, 0.00], gear_shelf: [0.30, 0.16] },
  };

  const VARIANTS = { lot_shed: { key: 'lot_shed', body: 'red', note: 'the camper lot\'s shed on Ginny\'s land' } };
  function list() { return Object.keys(VARIANTS); }
  function resolve(opts) {
    opts = opts || {};
    const key = opts.variant || 'lot_shed';
    if (!VARIANTS[key]) throw new Error('SmallShed: no variant "' + key + '" (have: ' + list().join(', ') + ')');
    const door = opts.door === 'open' ? 180 : (typeof opts.door === 'number' ? opts.door : 0);
    return { key, V: VARIANTS[key], cut: opts.cut === true, doorDeg: door };
  }

  // ---- measurements ----
  const sillZ = () => BODY.floorZ + BODY.sill;
  // roof underside above the ground at plan distance |y| from the ridge line
  const roofUnder = (y) => BODY.plateZ + (YO - Math.abs(y)) * BODY.pitch;
  const ridgeZ = () => roofUnder(0);
  const roofVert = () => BODY.roofT * Math.sqrt(1 + BODY.pitch * BODY.pitch);
  const rect = (b) => [[b.x0, b.y0], [b.x1, b.y0], [b.x1, b.y1], [b.x0, b.y1]];

  function fixtures(opts) {
    resolve(opts);
    return LAYOUT.map((it) => { const o = Object.assign({}, it); o.x0 = r3(o.x0); o.x1 = r3(o.x1); o.y0 = r3(o.y0); o.y1 = r3(o.y1); o.footprint = rect(o); return o; });
  }
  const fx = (F, id) => F.find((f) => f.id === id);
  function treatment(it) { return it.h <= 0.50 ? 'step_over' : (it.h <= 0.95 ? 'waist_block' : 'wall'); }

  function floorPolygon() { return [[-XI, -YI], [XI, -YI], [XI, YI], [-XI, YI]]; }

  function walkable(opts) {
    const F = fixtures(opts);
    return {
      floor: { z: BODY.floorZ, polygon: floorPolygon(), walker: 'foot_circle', foot_radius_m: WALKER.footRadius,
        obstructions: F.map((it) => ({ id: it.id, kind: it.kind, wall: it.wall, footprint: it.footprint,
          height_above_floor_m: it.h, treatment: treatment(it) })) },
      ground_outside: { z: 0, note: 'Outside the walls the ground is the scene\'s; the step is a tread, walked over.',
        obstructions: [{ id: 'step', kind: 'tread', footprint: rect(BODY.step), height_above_ground_m: BODY.step.h, treatment: 'step_over' }] },
    };
  }

  // the item positions: where a stowed rod stands and where a piece of gear sits
  function slots(opts) {
    const F = fixtures(opts), rr = fx(F, 'rod_rack'), gs = fx(F, 'gear_shelf');
    const R = STORE.rod_rack, G = STORE.gear_shelf, z = BODY.floorZ;
    const rx = r3((rr.x0 + rr.x1) / 2), y0 = (rr.y0 + rr.y1) / 2 - R.pitch * (R.capacity - 1) / 2;
    const rods = [];
    for (let i = 0; i < R.capacity; i++) rods.push({ slot: i, foot: [rx, r3(y0 + i * R.pitch), r3(z + 0.03)], tip: [rx, r3(y0 + i * R.pitch), r3(z + R.rodTop)] });
    const bay = (gs.x1 - gs.x0) / G.bays, gy = r3((gs.y0 + gs.y1) / 2), gear = [];
    for (let s = 0; s < G.shelves.length; s++) for (let b = 0; b < G.bays; b++)
      gear.push({ slot: s * G.bays + b, shelf: s, bay: b, centre: [r3(gs.x0 + (b + 0.5) * bay), gy, r3(z + G.shelves[s])],
        width_m: r3(bay - G.upright), depth_m: r3(gs.y1 - gs.y0) });
    return { rods, gear };
  }

  // ---- interactables ----
  const ALL = ORDER.slice();
  function interactables(opts) {
    resolve(opts);
    const F = fixtures(opts), z = BODY.floorZ, D = BODY.door, rr = fx(F, 'rod_rack'), gs = fx(F, 'gear_shelf');
    const R = (p, zz) => [r3(p[0]), r3(p[1]), r3(zz)];
    const doorAt = [r3((D.u0 + D.u1) / 2), -YO, z];
    const it = (id, action, reach, at, level, mech, extra) => Object.assign(
      { id, action, reach_point: reach, at, level, visible_facings: ALL, _note: 'mechanism: ' + mech }, extra || {});
    return [
      it('door', 'step_out', R(SPOTS.insideLanding, z), doorAt, 'floor',
        'the plank door (a press moves the leaf); the reach is the inside landing, where a 0.35 m foot stands clear',
        { door: { clear_width_m: r3(D.u1 - D.u0), landing_centre: SPOTS.insideLanding.slice(), landing_radius_m: WALKER.footRadius } }),
      it('door_outside', 'enter', R(SPOTS.outsideLanding, 0), doorAt, 'ground',
        'the same door from outside; the reach is the ground landing in front of the step',
        { door: { clear_width_m: r3(D.u1 - D.u0), landing_centre: SPOTS.outsideLanding.slice(), landing_radius_m: WALKER.footRadius } }),
      it('rod_rack', 'storage', R(SPOTS.reach.rod_rack, z), [r3((rr.x0 + rr.x1) / 2), 0, r3(z + 1.0)], 'floor',
        'rod stowage (StowageRack, a StowageDef that accepts Rods)',
        { stowage: STORE.rod_rack.stowage, stowage_status: STORE.rod_rack.status, accepts: STORE.rod_rack.accepts, capacity: STORE.rod_rack.capacity }),
      it('gear_shelf', 'storage', R(SPOTS.reach.gear_shelf, z), [r3((gs.x0 + gs.x1) / 2), r3(gs.y0), r3(z + 0.85)], 'floor',
        'gear stowage (StowageRack, a StowageDef that accepts General)',
        { stowage: STORE.gear_shelf.stowage, stowage_status: STORE.gear_shelf.status, accepts: STORE.gear_shelf.accepts, capacity: STORE.gear_shelf.capacity }),
    ];
  }

  // ---- geometry helpers ----
  function ptRect(p, b) { const dx = Math.max(0, b.x0 - p[0], p[0] - b.x1), dy = Math.max(0, b.y0 - p[1], p[1] - b.y1); return Math.hypot(dx, dy); }
  function rectDist(a, b) { const dx = Math.max(0, a.x0 - b.x1, b.x0 - a.x1), dy = Math.max(0, a.y0 - b.y1, b.y0 - a.y1); return Math.hypot(dx, dy); }
  function inPoly(p, poly) {
    let c = false;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
      const a = poly[i], b = poly[j];
      if (((a[1] > p[1]) !== (b[1] > p[1])) && (p[0] < (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0])) c = !c;
    }
    return c;
  }
  function edgeDist(p, poly) {
    let m = Infinity;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
      const a = poly[j], b = poly[i], ux = b[0] - a[0], uy = b[1] - a[1], l2 = ux * ux + uy * uy;
      const t = l2 > 0 ? Math.max(0, Math.min(1, ((p[0] - a[0]) * ux + (p[1] - a[1]) * uy) / l2)) : 0;
      m = Math.min(m, Math.hypot(p[0] - a[0] - t * ux, p[1] - a[1] - t * uy));
    }
    return m;
  }
  function segDist(p, a, b) { return edgeDist(p, [a, b]); }

  // the door leaf in plan at a swing angle (0 closed, 180 folded back against the wall, outward)
  function leafPlan(deg) {
    const D = BODY.door, w = D.u1 - D.u0, a = deg * DEG;
    const H0 = [D.u0, -YO], dir = [Math.cos(a), -Math.sin(a)], n = [Math.sin(a), Math.cos(a)];
    const A = H0, B = [H0[0] + dir[0] * w, H0[1] + dir[1] * w];
    return { hinge: H0, dir, n, width: w, quad: [A, B, [B[0] + n[0] * D.leafT, B[1] + n[1] * D.leafT], [A[0] + n[0] * D.leafT, A[1] + n[1] * D.leafT]] };
  }

  // the foot's free floor, flood-filled from the inside landing (what the C# test re-measures)
  function footFill(step) {
    step = step || 0.02;
    const F = fixtures(), poly = floorPolygon(), r = WALKER.footRadius;
    const nx = Math.round(2 * XI / step) + 1, ny = Math.round(2 * YI / step) + 1;
    const at = (i, j) => [-XI + i * step, -YI + j * step];
    const free = (p) => inPoly(p, poly) && edgeDist(p, poly) >= r - 1e-9 && F.every((f) => ptRect(p, f) >= r - 1e-9);
    const si = Math.round((SPOTS.insideLanding[0] + XI) / step), sj = Math.round((SPOTS.insideLanding[1] + YI) / step);
    const seen = new Set(), q = [[si, sj]], pts = [];
    if (!free(at(si, sj))) return { pts, start: false };
    seen.add(si + ',' + sj);
    while (q.length) {
      const [i, j] = q.shift(); pts.push(at(i, j));
      for (const [di, dj] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const a = i + di, b = j + dj, k = a + ',' + b;
        if (a < 0 || b < 0 || a >= nx || b >= ny || seen.has(k) || !free(at(a, b))) continue;
        seen.add(k); q.push([a, b]);
      }
    }
    return { pts, start: true, step };
  }

  // ---- the fit, in numbers (what the PR body quotes) ----
  function fit(opts) {
    resolve(opts);
    const F = fixtures(opts), rr = fx(F, 'rod_rack'), gs = fx(F, 'gear_shelf'), sl = slots(opts);
    const r = WALKER.footRadius, poly = floorPolygon();
    const yRod = Math.max(...sl.rods.map((s) => Math.abs(s.foot[1])));
    const rodHeadroom = roofUnder(yRod) - BODY.rafter - BODY.floorZ;
    const fill = footFill();
    const xs = fill.pts.map((p) => p[0]), ys = fill.pts.map((p) => p[1]);
    const clearOf = (p) => Math.min(edgeDist(p, poly), ...F.map((f) => ptRect(p, f)));
    const leafOpen = leafPlan(180), leafShut = leafPlan(0);
    const out = SPOTS.outsideLanding;
    const leafClear = (L) => Math.min(...L.quad.map((a, i) => segDist(out, a, L.quad[(i + 1) % 4])));
    const plan = planExtent();
    return {
      walls_m: [BODY.len, BODY.dep], plan_with_eaves_m: [r3(plan.x1 - plan.x0), r3(plan.y1 - plan.y0)],
      plan_corner_radius_m: r3(Math.hypot(plan.x1, plan.y1)), reserve: RESERVE,
      floor_clear_m: [r3(2 * XI), r3(2 * YI)], floor_area_m2: r3(4 * XI * YI),
      wall_plate_above_floor_m: r3(BODY.plateZ - BODY.floorZ), ridge_under_above_floor_m: r3(ridgeZ() - BODY.floorZ),
      stand: { centre: SPOTS.stand.slice(), clear_m: r3(clearOf(SPOTS.stand)), foot_radius_m: r,
        free_floor_for_the_foot_m: fill.pts.length ? [r3(Math.max(...xs) - Math.min(...xs)), r3(Math.max(...ys) - Math.min(...ys))] : [0, 0] },
      door: { clear_width_m: r3(BODY.door.u1 - BODY.door.u0), clear_height_m: BODY.door.h, foot_diameter_m: r3(2 * r),
        inside_landing_clear_m: r3(clearOf(SPOTS.insideLanding) - r),
        outside_landing_clear_of_step_m: r3(ptRect(out, BODY.step) - r),
        outside_landing_clear_of_walls_m: r3(ptRect(out, { x0: -XO, x1: XO, y0: -YO, y1: YO }) - r),
        outside_landing_clear_of_leaf_m: { shut: r3(leafClear(leafShut) - r), open: r3(leafClear(leafOpen) - r) },
        swing_sweep_radius_m: r3(leafOpen.width),
        swing_note: 'The leaf swings out and west through the ground in front of the door; at rest (shut, or folded back against the wall) it is clear of the landing.' },
      rod_rack: { capacity: STORE.rod_rack.capacity, slot_pitch_m: STORE.rod_rack.pitch, span_m: r3(rr.y1 - rr.y0),
        headroom_at_outer_slot_m: r3(rodHeadroom), longest_rod_m: STORE.rod_rack.minRod },
      gear_shelf: { capacity: STORE.gear_shelf.capacity, bays: STORE.gear_shelf.bays, shelves: STORE.gear_shelf.shelves.length,
        bay_clear_m: sl.gear[0].width_m, depth_m: r3(gs.y1 - gs.y0), run_m: r3(gs.x1 - gs.x0), height_m: gs.h },
    };
  }

  // the plan's outer extent, eaves and rakes included
  function planExtent() { return { x0: r3(-XO - BODY.rake), x1: r3(XO + BODY.rake), y0: r3(-YO - BODY.eave), y1: r3(YO + BODY.eave) }; }

  // ---- the lot ----
  function lot(opts) {
    resolve(opts);
    return {
      facing: 0, rig_dir: 0, convention: 'CounterClockwise: rigDir = (8 - facing) % 8; facing is the compass point rig +y faces (0 = N)',
      compass: { north: '+y (back wall, the gear shelf)', south: '-y (the door)', east: '+x (the window gable, toward the camper)', west: '-x (the rod rack gable)' },
      world_from_rig: 'world = ShedPos + x * R + y * U, U = -ShedDoorDirection, R = (U.y, -U.x)',
      source: 'StPetersCamperLot.ShedDoorDirection is the camper\'s curb side (south today), so U is north and R is east.',
      reservation: { footprint_m: { along_camper: RESERVE.along, across_camper: RESERVE.across }, reserve_radius_m: RESERVE.radius,
        csharp: 'StPetersCamperLot.ShedPos / ShedSize / ShedFootprintRadiusMetres / ShedDoorDirection' },
    };
  }

  // ---- validation (the generator refuses to write a sidecar that fails it) ----
  function validate(opts) {
    resolve(opts);
    const F = fixtures(opts), I = interactables(opts), poly = floorPolygon(), bad = [], r = WALKER.footRadius;
    const plan = planExtent();
    if (plan.x1 - plan.x0 > RESERVE.along + 1e-6 || plan.y1 - plan.y0 > RESERVE.across + 1e-6) bad.push('plan larger than the reserved ' + RESERVE.along + ' x ' + RESERVE.across);
    if (Math.hypot(plan.x1, plan.y1) > RESERVE.radius + 1e-3) bad.push('plan corner outside the ' + RESERVE.radius + ' m reserve circle');
    for (const f of F) for (const c of f.footprint) if (!inPoly(c, poly) && edgeDist(c, poly) > 0.02) bad.push(f.id + ' corner off the floor');
    for (let i = 0; i < F.length; i++) for (let j = i + 1; j < F.length; j++) if (rectDist(F[i], F[j]) <= 0) bad.push(F[i].id + ' touches ' + F[j].id);
    const pts = [];
    for (const a of I) {
      const p = [a.reach_point[0], a.reach_point[1]];
      if (a.level === 'floor') {
        if (!inPoly(p, poly) || edgeDist(p, poly) < WALKER.reachClear - 1e-6) bad.push(a.id + ' reach too near a wall');
        for (const f of F) if (ptRect(p, f) < WALKER.reachClear - 1e-6) bad.push(a.id + ' reach within ' + WALKER.reachClear + ' of ' + f.id);
        if (a.id !== 'door' && p[0] > BODY.door.u0 && p[0] < BODY.door.u1 && p[1] < -YI + 2 * r) bad.push(a.id + ' reach in the door landing');
        pts.push([a.id, p]);
      } else if (a.level === 'ground') {
        if (ptRect(p, { x0: -XO, x1: XO, y0: -YO, y1: YO }) < r) bad.push(a.id + ' landing overlaps the walls');
        if (ptRect(p, BODY.step) < r - 1e-6) bad.push(a.id + ' landing overlaps the step');
      } else bad.push(a.id + ' names a level this shed does not have');
    }
    for (let i = 0; i < pts.length; i++) for (let j = i + 1; j < pts.length; j++)
      if (Math.hypot(pts[i][1][0] - pts[j][1][0], pts[i][1][1] - pts[j][1][1]) < WALKER.reachSpacing) bad.push(pts[i][0] + ' and ' + pts[j][0] + ' closer than ' + WALKER.reachSpacing);
    const fill = footFill();
    if (!fill.start) bad.push('the foot cannot stand on the inside landing');
    const near = (q) => fill.pts.length ? Math.min(...fill.pts.map((p) => Math.hypot(p[0] - q[0], p[1] - q[1]))) : Infinity;
    for (const a of I) if (a.level === 'floor' && near(a.reach_point) > WALKER.interactReach + fill.step) bad.push('the foot cannot reach ' + a.id + ' from the door');
    if (near(SPOTS.stand) > fill.step) bad.push('the stand spot is not on the foot\'s free floor');
    const ft = fit(opts);
    if (ft.door.clear_width_m < 2 * r) bad.push('door narrower than the foot');
    if (BODY.floorZ + BODY.door.h + 0.08 > BODY.plateZ) bad.push('door head runs into the wall plate');
    if (ft.door.inside_landing_clear_m < -1e-6) bad.push('inside landing does not hold the foot');
    if (ft.door.outside_landing_clear_of_leaf_m.open < 0 || ft.door.outside_landing_clear_of_leaf_m.shut < 0) bad.push('outside landing under the door leaf at rest');
    if (SPOTS.insideLanding[0] - r < BODY.door.u0 - 1e-6 || SPOTS.insideLanding[0] + r > BODY.door.u1 + 1e-6) bad.push('inside landing overhangs the door span');
    if (ft.stand.clear_m < r) bad.push('no room to stand');
    if (ft.rod_rack.headroom_at_outer_slot_m < STORE.rod_rack.minRod) bad.push('rods do not stand under the roof');
    const sl = slots(opts);
    if (sl.rods.length !== STORE.rod_rack.capacity) bad.push('rod slots != capacity');
    if (sl.gear.length !== STORE.gear_shelf.capacity) bad.push('gear slots != capacity');
    const win = BODY.window, gs = fx(F, 'gear_shelf');
    if (win.u1 > gs.y0 - 0.02 && gs.x1 >= XI - 1e-6) bad.push('the window sits behind the gear shelf');
    // the cell holds every facing, door shut and open
    for (const door of [0, 180]) for (let d = 0; d < 8; d++) {
      const B = camBasis(d);
      for (const f of faces({ door })) for (const v of f.v) {
        const p = projVert(v[0], v[1], v[2], B);
        if (p.sx < 1 || p.sx > W - 2 || p.sy < 1 || p.sy > H - 2) { bad.push('dir ' + d + ' door ' + door + ': ' + f.seg + ' leaves the cell'); break; }
      }
    }
    return Array.from(new Set(bad));
  }

  // =========================== ART ===========================
  function hex2rgb(h) { return [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)]; }
  // existing ramps only: houseIsoRig.js (red, greyShingle, trim, wood, stone, asphaltGrey) and
  // camperIsoRig.js (wood as the bare inside boards, iron, chrome, canvas, glass)
  const MATS = {
    clap: ['#4a130f', '#671b14', '#88271c', '#a33124', '#bd4230', '#d25a42'],
    trim: ['#9aa09a', '#b4b8b0', '#ccd0c7', '#e0e2da', '#eef0e8', '#f8f9f2'],
    plank: ['#4c463f', '#5d564c', '#6f665a', '#82786a', '#968b7b', '#a99d8c'],
    floor: ['#4f3a24', '#63492d', '#785a39', '#8f7049', '#a6875d', '#bd9f74'],
    boards: ['#553d23', '#6b4e30', '#7f603b', '#957749', '#ab8d5f', '#c1a578'],
    stone: ['#33343a', '#42444b', '#54575d', '#666a70', '#7a7e84'],
    roof: ['#23262b', '#2e333a', '#3c424a', '#4c535c', '#5d6570', '#6f7883'],
    iron: ['#111216', '#1c1e23', '#2a2d33', '#3a3e46', '#4d525a', '#636970'],
    chrome: ['#4a5157', '#5f696f', '#7b858c', '#98a2a8', '#b6bec2', '#d6dbdd'],
    canvas: ['#4a4536', '#5f5943', '#786f52', '#8f8663', '#a79c77', '#bfb28c'],
    glass: ['#1b262b', '#243238', '#2f4149', '#3d545c', '#5d7b82', '#96b6ba'],
  };

  function camBasis(dir, elev) { const th = dir * Math.PI / 4, e = (elev != null ? elev : DEFAULT_ELEV) * DEG;
    return { th, ct: Math.cos(th), stt: Math.sin(th), se: Math.sin(e), ce: Math.cos(e) }; }
  function projVert(x, y, z, B) { const xr = x * B.ct - y * B.stt, yr = x * B.stt + y * B.ct;
    return { xr, yr, zr: z, sx: cx + xr * S, sy: groundY - (yr * B.se + z * B.ce) * S, d: (yr * B.ce - z * B.se) }; }
  function normal(a, b, c) { const ux = b.xr - a.xr, uy = b.yr - a.yr, uz = b.zr - a.zr, vx = c.xr - a.xr, vy = c.yr - a.yr, vz = c.zr - a.zr;
    const nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx, m = Math.hypot(nx, ny, nz) || 1; return [nx / m, ny / m, nz / m]; }
  function shadeOf(n, se, ce) { return n[0] * LN[0] + (n[1] * se + n[2] * ce) * LN[1] + (-n[1] * ce + n[2] * se) * LN[2]; }

  // textures take the model-space point
  const clapTex = (x, y, z) => { const p = 0.13, f = (((z - BODY.baseZ) % p) + p) % p; return f < 0.02 ? -1.3 : (f / p) * 0.3 - 0.1; };
  const floorTex = (x) => { const p = 0.15, f = ((x % p) + p) % p; return f < 0.012 ? -1.4 : ((Math.floor(x / p) * 7) % 3) * 0.12 - 0.12; };
  const boardTex = (u) => { const p = 0.20, f = ((u % p) + p) % p; return f < 0.012 ? -1.2 : ((Math.floor(u / p) * 5) % 3) * 0.10 - 0.10; };
  const roofTex = (x, y, z) => { const p = 0.075, row = Math.floor(z / p), f = ((z % p) + p) % p;
    if (f < 0.012) return -1.1; const tab = Math.floor(x / 0.33 + (row % 2) * 0.5); return (tab % 2) ? 0.12 : -0.08; };

  function face(out, v, mat, b, tag, tex) { out.push(Object.assign({ v, mat, b: b || 0 }, tex ? { tex } : {}, tag || {})); }
  function oriented(v, hint) {
    const a = v[0], b = v[1], c = v[2];
    const u = [b[0] - a[0], b[1] - a[1], b[2] - a[2]], w = [c[0] - a[0], c[1] - a[1], c[2] - a[2]];
    const n = [u[1] * w[2] - u[2] * w[1], u[2] * w[0] - u[0] * w[2], u[0] * w[1] - u[1] * w[0]];
    return (n[0] * hint[0] + n[1] * hint[1] + n[2] * hint[2]) < 0 ? v.slice().reverse() : v;
  }
  // a vertical prism over a convex plan polygon (counter-clockwise), no bottom face
  function prism(out, poly, z0, z1, mat, b, tag, noTop) {
    b = b || 0;
    for (let i = 0; i < poly.length; i++) {
      const a = poly[i], c = poly[(i + 1) % poly.length];
      face(out, oriented([[a[0], a[1], z0], [c[0], c[1], z0], [c[0], c[1], z1], [a[0], a[1], z1]], [c[1] - a[1], a[0] - c[0], 0]), mat, b + 0.05 * (i % 2 ? 1 : -1), tag);
    }
    if (!noTop) face(out, oriented(poly.map((p) => [p[0], p[1], z1]), [0, 0, 1]), mat, b + 0.25, tag);
  }
  const ccw = (x0, x1, y0, y1) => [[x0, y0], [x1, y0], [x1, y1], [x0, y1]];
  const WALL_O = { north: [0, 1], south: [0, -1], east: [1, 0], west: [-1, 0] };
  // a fixture part that may rise past the sill: split there so the cutaway can section it
  function tallPrism(out, poly, z0, z1, mat, b, id, o) {
    const s = sillZ();
    if (z1 <= s + 1e-6) { prism(out, poly, z0, z1, mat, b, { seg: 'fx:' + id }); return; }
    if (z0 >= s - 1e-6) { prism(out, poly, z0, z1, mat, b, { seg: 'fx:' + id + ':high', high: true, o }); return; }
    prism(out, poly, z0, s, mat, b, { seg: 'fx:' + id }, true);
    prism(out, poly, s, z1, mat, b, { seg: 'fx:' + id + ':high', high: true, o });
    face(out, oriented(poly.map((p) => [p[0], p[1], s]), [0, 0, 1]), 'iron', 0.2, { seg: 'fx:' + id + ':cap', capOf: true, o });
  }

  // ---- walls: panels around the openings, split at the sill, each a slab with clapboard outside ----
  const WALLS = [
    { id: 'south', o: [0, -1], t: [1, 0], d: YO, u0: -XO, u1: XO, gable: false },
    { id: 'north', o: [0, 1], t: [1, 0], d: YO, u0: -XO, u1: XO, gable: false },
    { id: 'east', o: [1, 0], t: [0, 1], d: XO, u0: -YO, u1: YO, gable: true },
    { id: 'west', o: [-1, 0], t: [0, 1], d: XO, u0: -YO, u1: YO, gable: true },
  ];
  function holesOf(w) {
    const z = BODY.floorZ;
    if (w.id === 'south') return [{ u0: BODY.door.u0, u1: BODY.door.u1, z0: z, z1: z + BODY.door.h, kind: 'door' }];
    if (w.id === 'east') return [{ u0: BODY.window.u0, u1: BODY.window.u1, z0: z + BODY.window.z0, z1: z + BODY.window.z1, kind: 'window' }];
    return [];
  }
  function wallFaces(out, w) {
    const T = BODY.wall, sZ = sillZ(), holes = holesOf(w);
    const P = (u, s, z) => [w.t[0] * u + w.o[0] * s, w.t[1] * u + w.o[1] * s, z];
    const top = (u) => (w.gable ? roofUnder(u) : BODY.plateZ);
    const us = [w.u0, w.u1], zs = [BODY.baseZ, sZ, BODY.plateZ];
    if (w.gable) us.push(-YI, YI, 0);
    for (const h of holes) { us.push(h.u0, h.u1); zs.push(h.z0, h.z1); }
    const U = Array.from(new Set(us.map(r3))).sort((a, b) => a - b), Z = Array.from(new Set(zs.map(r3))).sort((a, b) => a - b);
    const tex = (x, y, z) => clapTex(x, y, z);
    const inTex = (x, y) => boardTex(w.t[0] * x + w.t[1] * y);
    for (let i = 0; i + 1 < U.length; i++) {
      const ua = U[i], ub = U[i + 1], um = (ua + ub) / 2;
      // the eave walls run the full length; the gables fill between them below the plate
      const between = !w.gable || Math.abs(um) < YI;
      const cells = [];
      for (let k = 0; k + 1 < Z.length; k++) cells.push([Z[k], Z[k + 1], Z[k + 1], Z[k + 1]]);
      if (w.gable) cells.push([BODY.plateZ, BODY.plateZ, top(ua), top(ub)]);
      for (const [za, zb, ta, tb] of cells) {
        const isGableTop = za === BODY.plateZ && zb === BODY.plateZ;
        if (!isGableTop && !between) continue;
        const zm = isGableTop ? za + 0.01 : (za + zb) / 2;
        if (holes.some((h) => um > h.u0 && um < h.u1 && zm > h.z0 && zm < h.z1)) continue;
        const zta = isGableTop ? ta : zb, ztb = isGableTop ? tb : zb;
        if (zta - za < 1e-4 && ztb - za < 1e-4) continue;
        const high = za >= sZ - 1e-6, tag = high ? { seg: 'wall_' + w.id + '_high', high: true, o: w.o } : { seg: 'wall_' + w.id + '_low' };
        const loop = [[ua, za], [ub, za], [ub, ztb], [ua, zta]];
        const outer = loop.map(([u, z]) => P(u, w.d, z)), inner = loop.map(([u, z]) => P(u, w.d - T, z));
        face(out, oriented(outer, [w.o[0], w.o[1], 0]), 'clap', 0, tag, tex);
        face(out, oriented(inner, [-w.o[0], -w.o[1], 0]), 'boards', -0.15, tag, inTex);
        for (let e = 0; e < 4; e++) {
          const a = loop[e], c = loop[(e + 1) % 4];
          if (Math.abs(a[0] - c[0]) < 1e-6 && Math.abs(a[1] - c[1]) < 1e-6) continue;
          const nu = c[1] - a[1], nz = a[0] - c[0];
          const mat = (Math.abs(nz) > 0 && Math.abs(a[1] - sZ) < 1e-6 && Math.abs(c[1] - sZ) < 1e-6 && !high) ? 'iron' : 'boards';
          face(out, oriented([P(a[0], w.d, a[1]), P(c[0], w.d, c[1]), P(c[0], w.d - T, c[1]), P(a[0], w.d - T, a[1])],
            [w.t[0] * nu, w.t[1] * nu, nz]), mat, 0.1, tag);
        }
      }
    }
  }

  function faces(opts) {
    const s = resolve(opts), out = [], F = fixtures(opts), z = BODY.floorZ, sZ = sillZ();
    // corner blocks and the step
    for (const [x, y] of [[-XO, -YO], [XO, -YO], [XO, YO], [-XO, YO], [0, -YO], [0, YO]])
      prism(out, ccw(Math.max(-XO, x - 0.10), Math.min(XO, x + 0.10), Math.max(-YO, y - 0.10), Math.min(YO, y + 0.10)), 0, BODY.baseZ, 'stone', -0.1, { seg: 'blocks' });
    const st = BODY.step;
    prism(out, ccw(st.x0, st.x1, st.y0, st.y1), 0, st.h, 'stone', 0.05, { seg: 'step' });
    // floor (its edge shows in the door opening)
    face(out, oriented(floorPolygon().map((p) => [p[0], p[1], z]), [0, 0, 1]), 'floor', -0.05, { seg: 'floor' }, (x) => floorTex(x));
    prism(out, ccw(BODY.door.u0, BODY.door.u1, -YO, -YI), BODY.baseZ, z, 'floor', -0.2, { seg: 'threshold' });
    // walls
    for (const w of WALLS) wallFaces(out, w);
    // corner boards (white trim), split at the sill with their walls
    for (const [x, y, ox, oy] of [[-XO, -YO, -1, -1], [XO, -YO, 1, -1], [XO, YO, 1, 1], [-XO, YO, -1, 1]]) {
      const xa = x - ox * 0.09, xb = x + ox * 0.015, ya = y - oy * 0.09, yb = y + oy * 0.015;
      const poly = ccw(Math.min(xa, xb), Math.max(xa, xb), Math.min(ya, yb), Math.max(ya, yb));
      // a corner board belongs to both its walls: it is sectioned when either is near
      prism(out, poly, BODY.baseZ, sZ, 'trim', 0, { seg: 'corner_board' }, true);
      prism(out, poly, sZ, BODY.plateZ, 'trim', 0, { seg: 'corner_board_high', high: true, o: [ox, oy] });
    }
    // door casing (south wall, outside) and the window casing (east gable, outside)
    const D = BODY.door, cas = 0.06, pr = 0.015;
    const casS = (x0, x1, z0, z1, id) => tallPrism(out, ccw(x0, x1, -YO - pr, -YO + 0.01), z0, z1, 'trim', 0.05, id, [0, -1]);
    casS(D.u0 - cas, D.u0, BODY.baseZ + 0.10, z + D.h + cas, 'door_casing_w');
    casS(D.u1, D.u1 + cas, BODY.baseZ + 0.10, z + D.h + cas, 'door_casing_e');
    casS(D.u0 - cas, D.u1 + cas, z + D.h, z + D.h + cas, 'door_casing_head');
    const Wn = BODY.window, wz0 = z + Wn.z0, wz1 = z + Wn.z1;
    const casE = (y0, y1, z0, z1, id) => tallPrism(out, [[XO - 0.01, y0], [XO + pr, y0], [XO + pr, y1], [XO - 0.01, y1]], z0, z1, 'trim', 0.05, id, [1, 0]);
    casE(Wn.u0 - cas, Wn.u0, wz0 - cas, wz1 + cas, 'window_casing_s');
    casE(Wn.u1, Wn.u1 + cas, wz0 - cas, wz1 + cas, 'window_casing_n');
    casE(Wn.u0 - cas, Wn.u1 + cas, wz1, wz1 + cas, 'window_casing_head');
    casE(Wn.u0 - cas, Wn.u1 + cas, wz0 - cas, wz0, 'window_casing_sill');
    face(out, oriented([[XO - 0.04, Wn.u0, wz0], [XO - 0.04, Wn.u1, wz0], [XO - 0.04, Wn.u1, wz1], [XO - 0.04, Wn.u0, wz1]], [1, 0, 0]), 'glass', 0.1, { seg: 'window_glass', high: true, o: [1, 0] });
    tallPrism(out, [[XO - 0.05, -0.012], [XO - 0.02, -0.012], [XO - 0.02, 0.012], [XO - 0.05, 0.012]], wz0, wz1, 'trim', 0.1, 'window_muntin', [1, 0]);
    tallPrism(out, [[XO - 0.05, Wn.u0], [XO - 0.02, Wn.u0], [XO - 0.02, Wn.u1], [XO - 0.05, Wn.u1]], (wz0 + wz1) / 2 - 0.012, (wz0 + wz1) / 2 + 0.012, 'trim', 0.1, 'window_muntin', [1, 0]);
    // the door leaf: vertical planks with three battens on the outside face
    const L = leafPlan(s.doorDeg), lz0 = z + 0.01, lz1 = z + D.h - 0.01;
    const plankTexL = (x, y) => { const u = (x - L.hinge[0]) * L.dir[0] + (y - L.hinge[1]) * L.dir[1]; return boardTex(u + 0.05) * 1.2; };
    const lq = L.quad, lpoly = oriented(lq.map((p) => [p[0], p[1], 0]), [0, 0, 1]).map((p) => [p[0], p[1]]);
    const leafTag = (hi) => (hi ? { seg: 'door_leaf_high', high: true, o: [0, -1] } : { seg: 'door_leaf' });
    const leafPart = (z0, z1, hi) => {
      for (let i = 0; i < lpoly.length; i++) {
        const a = lpoly[i], c = lpoly[(i + 1) % lpoly.length];
        face(out, oriented([[a[0], a[1], z0], [c[0], c[1], z0], [c[0], c[1], z1], [a[0], a[1], z1]], [c[1] - a[1], a[0] - c[0], 0]), 'plank', 0, leafTag(hi), plankTexL);
      }
      face(out, oriented(lpoly.map((p) => [p[0], p[1], z1]), [0, 0, 1]), 'plank', 0.2, leafTag(hi));
    };
    leafPart(lz0, sZ, false); leafPart(sZ, lz1, true);
    for (const zb of [0.22, 0.92, 1.62]) {
      const off = -0.02, A = lq[0], B = lq[1];
      const bq = [A, B, [B[0] + L.n[0] * off, B[1] + L.n[1] * off], [A[0] + L.n[0] * off, A[1] + L.n[1] * off]];
      const bp = oriented(bq.map((p) => [p[0], p[1], 0]), [0, 0, 1]).map((p) => [p[0], p[1]]);
      const z0b = z + zb, hi = z0b >= sZ;
      prism(out, bp, z0b, z0b + 0.10, 'plank', -0.15, leafTag(hi));
    }
    // a latch on the free edge
    const lat = [lq[1][0] - L.dir[0] * 0.08, lq[1][1] - L.dir[1] * 0.08];
    prism(out, ccw(lat[0] - 0.02, lat[0] + 0.02, lat[1] - 0.02, lat[1] + 0.02), z + 0.98, z + 1.04, 'iron', 0.2, { seg: 'door_latch', high: true, o: [0, -1] });
    // the roof: two slabs from the eaves to the ridge, the lid of the cutaway
    const pl = planExtent(), rv = roofVert();
    for (const side of [-1, 1]) {
      const ye = side * (YO + BODY.eave), ze = roofUnder(ye), zr = ridgeZ();
      const lid = { seg: 'roof', lid: true };
      face(out, oriented([[pl.x0, ye, ze + rv], [pl.x1, ye, ze + rv], [pl.x1, 0, zr + rv], [pl.x0, 0, zr + rv]], [0, side * BODY.pitch, 1]), 'roof', 0, lid, roofTex);
      face(out, oriented([[pl.x0, ye, ze], [pl.x1, ye, ze], [pl.x1, 0, zr], [pl.x0, 0, zr]], [0, -side * BODY.pitch, -1]), 'boards', -0.2, lid);
      face(out, oriented([[pl.x0, ye, ze], [pl.x1, ye, ze], [pl.x1, ye, ze + rv], [pl.x0, ye, ze + rv]], [0, side, 0]), 'trim', 0, lid);
      for (const xe of [pl.x0, pl.x1])
        face(out, oriented([[xe, ye, ze], [xe, 0, zr], [xe, 0, zr + rv], [xe, ye, ze + rv]], [Math.sign(xe), 0, 0]), 'trim', 0.05, lid);
    }
    prism(out, ccw(pl.x0, pl.x1, -0.05, 0.05), ridgeZ() + rv - 0.01, ridgeZ() + rv + 0.04, 'roof', 0.3, { seg: 'ridge_cap', lid: true });
    // ---- the fit-out ----
    for (const it of F) {
      const o = WALL_O[it.wall];
      if (it.id === 'rod_rack') {
        prism(out, ccw(it.x0, it.x1, it.y0, it.y1), z, z + 0.07, 'boards', 0.1, { seg: 'fx:rod_rack' });
        tallPrism(out, ccw(it.x0, it.x0 + 0.16, it.y0, it.y1), z + 1.20, z + 1.26, 'boards', 0.1, 'rod_rack_rail', o);
        const sl = slots(opts).rods;
        sl.forEach((r, i) => {
          const [x, y] = r.foot;
          tallPrism(out, ccw(x - 0.016, x + 0.016, y - 0.016, y + 0.016), z + 0.03, z + 0.46, 'canvas', 0.1, 'rod_grip', o);
          tallPrism(out, ccw(x + 0.012, x + 0.06, y - 0.03, y + 0.03), z + 0.40, z + 0.48, 'chrome', 0.15, 'rod_reel', o);
          tallPrism(out, ccw(x - 0.009, x + 0.009, y - 0.009, y + 0.009), z + 0.46, r.tip[2], i % 2 ? 'iron' : 'canvas', 0.05, 'rod_blank', o);
        });
      } else if (it.id === 'gear_shelf') {
        const G = STORE.gear_shelf, bay = (it.x1 - it.x0) / G.bays;
        for (let b = 0; b <= G.bays; b++) {
          const xu = Math.min(it.x1 - G.upright, Math.max(it.x0, it.x0 + b * bay - G.upright / 2));
          tallPrism(out, ccw(xu, xu + G.upright, it.y0, it.y1), z, z + it.h, 'boards', 0.05, 'gear_shelf_upright', o);
        }
        for (const sz of G.shelves) tallPrism(out, ccw(it.x0, it.x1, it.y0, it.y1), z + sz - G.board, z + sz, 'boards', 0.2, 'gear_shelf_board', o);
        tallPrism(out, ccw(it.x0, it.x1, it.y0, it.y1), z + it.h - G.board, z + it.h, 'boards', 0.2, 'gear_shelf_top', o);
      }
    }
    return out;
  }

  // ---- cutaway: which faces a facing keeps ----
  function cutaway(dir) {
    const th = dir * Math.PI / 4, view = [-Math.sin(th), -Math.cos(th)];
    const near = (o) => !!o && (o[0] * view[0] + o[1] * view[1]) > BODY.nearDot;
    return { view: [r3(view[0]) + 0, r3(view[1]) + 0], keep: (f) => {
      if (f.lid) return false;
      if (f.high && near(f.o)) return false;
      if (f.capOf) return near(f.o);
      return true;
    } };
  }

  function paint(fl, B) {
    const N = W * H, zbuf = new Float32Array(N).fill(Infinity), dep = new Float32Array(N);
    const rbuf = new Array(N).fill(null), ibuf = new Int16Array(N);
    for (const f of fl) {
      const rv = f.v.map(([x, y, z]) => projVert(x, y, z, B));
      const n = normal(rv[0], rv[1], rv[2]); let sh = shadeOf(n, B.se, B.ce);
      if (sh < 0) sh = shadeOf([-n[0], -n[1], -n[2]], B.se, B.ce) * 0.9;
      const ramp = MATS[f.mat] || MATS.boards, fidx = sh * GAIN + BIAS + f.b, tex = f.tex;
      for (let t = 1; t + 1 < rv.length; t++) tri(rv[0], rv[t], rv[t + 1], f.v[0], f.v[t], f.v[t + 1]);
      function tri(a, b, c, ma, mb, mc) {
        const minX = Math.max(0, Math.floor(Math.min(a.sx, b.sx, c.sx))), maxX = Math.min(W - 1, Math.ceil(Math.max(a.sx, b.sx, c.sx)));
        const minY = Math.max(0, Math.floor(Math.min(a.sy, b.sy, c.sy))), maxY = Math.min(H - 1, Math.ceil(Math.max(a.sy, b.sy, c.sy)));
        const area = (b.sx - a.sx) * (c.sy - a.sy) - (c.sx - a.sx) * (b.sy - a.sy); if (Math.abs(area) < 1e-6) return;
        for (let y = minY; y <= maxY; y++) for (let x = minX; x <= maxX; x++) {
          const px = x + 0.5, py = y + 0.5;
          const w0 = ((b.sx - px) * (c.sy - py) - (c.sx - px) * (b.sy - py)) / area, w1 = ((c.sx - px) * (a.sy - py) - (a.sx - px) * (c.sy - py)) / area, w2 = 1 - w0 - w1;
          if (w0 < -0.001 || w1 < -0.001 || w2 < -0.001) continue;
          const d = w0 * a.d + w1 * b.d + w2 * c.d, i = y * W + x;
          if (d < zbuf[i]) { zbuf[i] = d; dep[i] = d;
            let fi = fidx;
            if (tex) fi += tex(w0 * ma[0] + w1 * mb[0] + w2 * mc[0], w0 * ma[1] + w1 * mb[1] + w2 * mc[1], w0 * ma[2] + w1 * mb[2] + w2 * mc[2]);
            const base = Math.floor(fi), idx = Math.max(0, Math.min(ramp.length - 1, base + ((fi - base) > BAYER[x & 3][y & 3] ? 1 : 0)));
            rbuf[i] = ramp; ibuf[i] = idx; }
        }
      }
    }
    return { rbuf, ibuf, dep };
  }
  function post(bufs) {
    const { rbuf, ibuf, dep } = bufs, N = W * H, out = new Array(N).fill(null);
    for (let i = 0; i < N; i++) if (rbuf[i]) out[i] = rbuf[i][ibuf[i]];
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const i = y * W + x; if (!rbuf[i]) continue;
      for (const [dx, dy] of [[1, 0], [0, 1]]) { const nx = x + dx, ny = y + dy; if (nx >= W || ny >= H) continue; const j = ny * W + nx; if (!rbuf[j]) continue;
        if (Math.abs(dep[i] - dep[j]) > EDGE) { const far = dep[i] > dep[j] ? i : j; out[far] = rbuf[far][Math.max(0, ibuf[far] - 2)]; } } }
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const i = y * W + x; if (!out[i]) continue; let n = 0;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const nx = x + dx, ny = y + dy; if (nx >= 0 && nx < W && ny >= 0 && ny < H && out[ny * W + nx]) n++; }
      if (n === 0) out[i] = null; }
    const rgba = new Uint8ClampedArray(N * 4);
    for (let i = 0; i < N; i++) { const c = out[i]; if (!c) continue; const [r, g, b] = hex2rgb(c); rgba[i * 4] = r; rgba[i * 4 + 1] = g; rgba[i * 4 + 2] = b; rgba[i * 4 + 3] = 255; }
    return rgba;
  }
  function render(dir, opts) {
    opts = opts || {}; const s = resolve(opts), B = camBasis(dir, opts.elev);
    let fl = faces(opts);
    if (s.cut) { const k = cutaway(dir).keep; fl = fl.filter(k); }
    return post(paint(fl, B));
  }
  function project(dir, p, elev) { const v = projVert(p[0], p[1], p[2], camBasis(dir, elev)); return { x: v.sx, y: v.sy }; }
  function anchors(dir, opts) {
    opts = opts || {}; const o = {};
    for (const a of interactables(opts)) o[a.id] = { reach: project(dir, a.reach_point, opts.elev), at: project(dir, a.at, opts.elev) };
    o.stand = { reach: project(dir, [SPOTS.stand[0], SPOTS.stand[1], BODY.floorZ], opts.elev) };
    return o;
  }

  // ---- the sidecar ----
  function sidecar(opts) {
    const s = resolve(opts), F = fixtures(opts), D = BODY.door, L0 = leafPlan(0), L1 = leafPlan(180);
    const q = (Q) => Q.map((p) => [r3(p[0]), r3(p[1])]);
    return {
      schema: 'small-shed@1', exportSymbol: 'SmallShed', rig: 'smallShedRig.js', variant: s.key,
      generatedBy: 'SmallShed.sidecar({variant:"' + s.key + '"}) via node tools/rig-recipes/small-shed.mjs (--check fails on drift)',
      frame: { units: 'metres', scale_px_per_m: PX, origin: 'ground-centre of the wall footprint',
        axes: '+x east (along the camper on the lot), +y north, +z up from the ground; the door is on -y', heading_independent: true },
      cell: { w: W, h: H, pivot: { x: cx, y: groundY }, facings: ORDER, convention: 'CounterClockwise' },
      BODY: { walls_m: { along_x: BODY.len, across_y: BODY.dep, thickness: BODY.wall }, floor_z_m: BODY.floorZ,
        wall_plate_z_m: BODY.plateZ, ridge_under_z_m: r3(ridgeZ()), roof_pitch: '8/12', eave_m: BODY.eave, rake_m: BODY.rake,
        plan_with_eaves_m: planExtent(),
        cutaway: { lid: 'the roof, always culled', sill_above_floor_m: BODY.sill,
          near_rule: 'a wall panel or tall fixture part whose outward plan normal o has o . view > ' + BODY.nearDot + ' is sectioned to the sill; view = (-sin(dir*45deg), -cos(dir*45deg))' } },
      WALKABLE: walkable(opts),
      THRESHOLD: { id: 'entry', wall: 'south', door_clear_m: { u0: D.u0, u1: D.u1, width: r3(D.u1 - D.u0), height_above_floor: D.h },
        leaf: { hinge: D.hinge, swing: D.swing, hinge_point: q([L0.hinge])[0], width_m: r3(L0.width), thickness_m: D.leafT,
          shut_plan: q(L0.quad), open_plan: q(L1.quad), open_deg: 180,
          note: 'A press moves the leaf (the door ruling): it swings out and west and rests folded back against the wall.' },
        floor_step_m: BODY.floorZ, outside_step: { footprint: rect(BODY.step), top_above_ground_m: BODY.step.h },
        inside_landing: { centre: SPOTS.insideLanding.slice(), radius_m: WALKER.footRadius, z: BODY.floorZ },
        outside_landing: { centre: SPOTS.outsideLanding.slice(), radius_m: WALKER.footRadius, z: 0 },
        keep_clear_inside: [[D.u0, -YI], [D.u1, -YI], [D.u1, r3(-YI + 2 * WALKER.footRadius)], [D.u0, r3(-YI + 2 * WALKER.footRadius)]] },
      STAND: { centre: SPOTS.stand.slice(), z: BODY.floorZ, radius_m: WALKER.footRadius, note: 'a spot inside where the 0.35 m foot stands clear of every wall and fixture: room to stand' },
      INTERACT: interactables(opts),
      STORAGE: {
        rod_rack: Object.assign({ fixture: 'rod_rack', stowage: STORE.rod_rack.stowage, status: STORE.rod_rack.status, accepts: STORE.rod_rack.accepts,
          capacity: STORE.rod_rack.capacity, slot_pitch_m: STORE.rod_rack.pitch }, { slots: slots(opts).rods }),
        gear_shelf: Object.assign({ fixture: 'gear_shelf', stowage: STORE.gear_shelf.stowage, status: STORE.gear_shelf.status, accepts: STORE.gear_shelf.accepts,
          capacity: STORE.gear_shelf.capacity, bays: STORE.gear_shelf.bays, shelves_above_floor_m: STORE.gear_shelf.shelves }, { slots: slots(opts).gear }),
        memory: 'What sits in each slot is saved by the storage-with-memory lane (save v25), keyed by the rack; this rig only places the slots.',
      },
      mechanism_map: { door: 'plank door, inside (step_out)', door_outside: 'plank door, outside (enter)',
        rod_rack: 'StowageRack (stowage.shed_rod_rack): 8 rods', gear_shelf: 'StowageRack (stowage.shed_gear_locker): 12 pieces of gear' },
      FIT: fit(opts),
      LOT: lot(opts),
      walker: Object.assign({ source: 'foot = CircleCollider2D radius 0.35 m (PersistentCoreBuilder, QuayEdgePlan.WalkerFootRadiusMetres); reach rules after the boat-interior kit' }, WALKER),
      _excluded: {
        bench: 'No workbench: the floor is 2.04 x 1.44 m and a bench would take the standing room the owner asked for.',
        freezer: 'The bigger freezer is the camper\'s (40 units); the shed holds rods and gear only.',
        light: 'No lamp: the window lights it by day.',
      },
      _bake_owed: {
        catalog: 'Register smallShedRig.js in RigCatalog (no prerequisites) once the baker has a building-with-cutaway path.',
        mesh: 'Bake through the cutaway seam (ADR 0041/0048): roof culled, near walls sectioned to the 0.60 m sill per facing, faces tagged by seg.',
        placement: 'Stand it at StPetersCamperLot.ShedPos at facing ' + lot().facing + ' (rig dir ' + lot().rig_dir + '); the door faces ShedDoorDirection.',
        look: 'Owner reviews the contact sheet before the bake lands.',
      },
    };
  }

  root.SmallShed = { W, H, PX, DIRS: 8, pivot: { x: cx, y: groundY }, order: ORDER, defaultElev: DEFAULT_ELEV,
    BODY, WALKER, LAYOUT, STORE, RESERVE, SPOTS, MATS, list, resolve, fixtures, walkable, slots, interactables, fit, lot,
    validate, cutaway, faces, render, project, anchors, sidecar, leafPlan, planExtent,
    geom: { inPoly, edgeDist, ptRect, rectDist } };
})(typeof globalThis !== 'undefined' ? globalThis : window);
