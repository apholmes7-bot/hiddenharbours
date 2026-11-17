/* Parametric CAMPER INTERIOR rig: the room inside camperIsoRig.js's Clipper shell. Same turntable,
   camera and shading recipe as the shell and the fleet: 45deg steps, elev 40deg, flat-facet shading
   from the fixed key, z-buffered, ordered dither, depth-edge darkening, NO AA, ringless (ADR 0031).
   32 px = 1 m. The cell, the pivot and the frame are the SHELL's, so a baked room composites under
   the exterior 1:1.

   THE ROOM MEASURES AGAINST THE SHELL, NEVER BESIDE IT. Every wall, the ceiling and every fixture
   clamp read CamperIso.loft (prof, profAtY, skinHW, interiorHW, stations) and the walkable floor IS
   CamperIso.gameplayGeometry().SOLE. A re-derived profile is how a room drifts off its body; this
   file has none. Load camperIsoRig.js first or this file throws.

   FRAME (the shell's): metres, origin at the ground-centre of the body, +x curb side (the door
   side), +y bow / hitch, +z up. Heights in LAYOUT are ABOVE THE SOLE.

   ON THE LOT the curb faces the cottage (south) and the hitch points east, so rig -x is NORTH. The
   galley, the pantry and the wet bath are on -x: the owner's ruling puts kitchens and baths on the
   north wall. sidecar().LOT carries the mapping and the reserved spot for the lot's small shed.

   CUTAWAY (the boat-cutaway convention): the ceiling is the lid and is always culled; any wall
   segment, partition or tall fixture whose outward plan normal o satisfies o . view > 0.2 is
   sectioned down to a 0.60 m sill. view = (-sin th, -cos th) is the plan direction toward the
   camera. render(dir) is cut by default; {cut:false} draws the closed room.

   Exposes globalThis.CamperInterior = { W,H,PX,DIRS,pivot,order,defaultElev, ROOM, WALKER, LAYOUT,
     HEAD, list(), resolve(opts), fixtures(opts), walkable(opts), interactables(opts), fit(opts),
     lot(opts), cutaway(dir), faces(opts), render(dir,opts), project(dir,p,elev), anchors(dir,opts),
     validate(opts), sidecar(opts) }. */
(function (root) {
  'use strict';
  const C = root.CamperIso;
  if (!C || !C.loft || typeof C.loft.skinHW !== 'function' || typeof C.gameplayGeometry !== 'function')
    throw new Error('camperInteriorRig.js: load camperIsoRig.js first. The room measures against ' +
      'CamperIso.loft and CamperIso.gameplayGeometry and will not re-derive them.');
  const L = C.loft, CELL = L.cell;
  const W = CELL.W, H = CELL.H, cx = CELL.cx, groundY = CELL.groundY, S = CELL.S;
  const GAIN = L.shade.GAIN, BIAS = L.shade.BIAS, EDGE = L.shade.EDGE, LN = L.shade.LN, BAYER = L.shade.BAYER;
  const PX = 32, DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const ORDER = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  const r3 = (v) => +(+v).toFixed(3);

  // ---- room constants (art source; the walker numbers mirror the game's, see WALKER) ----
  const ROOM = {
    headroom: 2.00,      // the shell's interior_headroom_m: the ceiling never sits higher above the sole
    crownGap: 0.10,      // ...nor closer than this under the crown, so the domed ends bring it down
    liner: 0.05,         // liner inset off the skin
    fitInset: 0.06,      // the shell's own fitOut() inset: nothing is authored nearer the skin
    sill: 0.60,          // cutaway: near walls are sectioned to this height above the sole
    nearDot: 0.20,       // o . view above this = a near wall
    // wall band breaks above the sole: sill, window sill, window head, door head, then the ceiling
    bands: [0, 0.60, 0.92, 1.56, 1.72],
  };
  // The player as the game builds him: CircleCollider2D radius 0.35 m (PersistentCoreBuilder;
  // QuayEdgePlan.WalkerFootRadiusMetres). Reach rules are the boat-interior kit's, widened.
  const WALKER = { footRadius: 0.35, reachClear: 0.20, reachSpacing: 0.25, interactReach: 0.25,
    aisleMin: 0.70 };

  // ---- the fit-out. Clipper only: the Bantam has no room yet. x is clamped into the loft by fitX().
  // wall: which liner it stands on (north = -x, south = +x, tail = -y, nose = +y); it drives the
  // cutaway and the north-wall rule. z0 (above the sole) is only for overhead pieces.
  const LAYOUT = {
    clipper: [
      { id: 'rear_bunk', kind: 'berth', wall: 'tail', x0: -1.02, x1: 1.02, y0: -3.06, y1: -1.88, h: 0.52,
        note: 'transverse double across the tail: the bed, and the save' },
      { id: 'head_room', kind: 'room', wall: 'north', x0: -1.02, x1: -0.06, y0: -1.86, y1: -0.84, h: 1.92,
        note: 'wet bath: toilet, corner basin and a shower over one pan floor; sliding door on its +x face' },
      { id: 'galley_counter', kind: 'counter', wall: 'north', x0: -1.02, x1: -0.40, y0: -0.80, y1: 1.62, h: 0.90,
        note: 'sink, two-burner stove, prep run, under-counter fridge, drawers; the window sill sits just above the top' },
      { id: 'pantry', kind: 'tall', wall: 'north', x0: -1.02, x1: -0.40, y0: 1.66, y1: 2.22, h: 1.70,
        note: 'tall pantry locker, forward of the galley window' },
      { id: 'gear_locker', kind: 'tall', wall: 'south', x0: 0.37, x1: 1.02, y0: 0.50, y1: 0.94, h: 1.80,
        note: 'gear locker, about 10 pieces of gear, just inside the door on its hinge side' },
      { id: 'freezer', kind: 'chest', wall: 'south', x0: 0.34, x1: 1.02, y0: 1.00, y1: 2.10, h: 0.86,
        note: 'chest freezer for the catch, 40 units (twice Ginny\'s 20), lid hinged on the wall side, under the curb window' },
      { id: 'rod_rack', kind: 'overhead', wall: 'south', x0: 0.34, x1: 0.62, y0: 0.50, y1: 2.46, z0: 1.84, h: 1.94,
        note: 'four rods on overhead brackets above the locker and the freezer' },
      { id: 'nose_settee', kind: 'seat', wall: 'nose', x0: -1.02, x1: 1.02, y0: 2.50, y1: 3.00, h: 0.44,
        note: 'bench across the nose under the front window' },
    ],
  };
  // galley run along the counter (y spans on the counter top)
  const GALLEY = { sink: [-0.62, -0.24], stove: [-0.16, 0.36], prep: [0.40, 0.70], fridge: [0.74, 1.30], drawers: [1.32, 1.60] };
  // the wet bath, inside head_room: partition thickness, the door gap, and its own fixtures
  const HEAD = {
    wall: 0.04,
    door: { y0: -1.40, y1: -0.88 },
    toilet: { x0: -0.90, x1: -0.48, y0: -1.80, y1: -1.40, h: 0.45 },
    basin: { x0: -0.90, x1: -0.62, y0: -1.18, y1: -0.90, h: 0.82 },
    stand: [-0.31, -1.20],            // the one standing spot: shower, basin and the toilet front
    showerHeadAboveSole: 1.85,
  };

  const VARIANTS = { clipper: { key: 'clipper', shell: 'clipper' } };
  function list() { return Object.keys(VARIANTS); }
  function resolve(opts) {
    opts = opts || {};
    const key = opts.variant || 'clipper';
    if (!VARIANTS[key]) throw new Error('CamperInterior: no room for variant "' + key + '" (have: ' + list().join(', ') + ')');
    const V = C.VARIANTS[VARIANTS[key].shell];
    return { V, key, cut: opts.cut !== false, sole: V.floorZ };
  }

  // ---- loft measurements (all through CamperIso.loft) ----
  function ceilingAt(V, P) { return Math.min(V.floorZ + ROOM.headroom, P.zc + P.hzT - ROOM.crownGap); }
  function fitX(V, it) {
    // a room's walls ARE the liner, so its floor footprint runs to the sole; everything else must
    // fit under the skin over its whole height
    const zb = V.floorZ + (it.z0 || 0), zt = it.kind === 'room' ? zb : V.floorZ + it.h;
    const P0 = L.profAtY(V, it.y0), P1 = L.profAtY(V, it.y1);
    const lim = Math.min(L.skinHW(V, P0, zt, ROOM.fitInset), L.skinHW(V, P1, zt, ROOM.fitInset),
      L.skinHW(V, P0, zb, ROOM.fitInset), L.skinHW(V, P1, zb, ROOM.fitInset));
    return { x0: r3(Math.max(it.x0, -lim)), x1: r3(Math.min(it.x1, lim)) };
  }
  function fixtures(opts) {
    const s = resolve(opts), V = s.V;
    return LAYOUT[s.key].map((it) => {
      const f = fitX(V, it);
      if (f.x1 - f.x0 < 0.14) throw new Error('CamperInterior: ' + it.id + ' is squeezed out by the loft');
      const o = Object.assign({}, it, f);
      o.footprint = [[o.x0, o.y0], [o.x1, o.y0], [o.x1, o.y1], [o.x0, o.y1]];
      return o;
    });
  }
  const fx = (list_, id) => list_.find((f) => f.id === id);
  function treatment(it) {
    if (it.kind === 'overhead') return 'overhead';
    if (it.kind === 'room') return 'room';
    return it.h <= 0.50 ? 'step_over' : (it.h <= 0.95 ? 'waist_block' : 'wall');
  }

  // ---- walkable levels ----
  function headPolygon(V, F) {
    const hr = fx(F, 'head_room'), yi0 = hr.y0 + HEAD.wall, yi1 = hr.y1 - HEAD.wall;
    const xin = -Math.min(L.interiorHW(V, L.profAtY(V, yi0)), L.interiorHW(V, L.profAtY(V, yi1)));
    const xi1 = hr.x1 - HEAD.wall;
    return [[r3(xin), r3(yi0)], [r3(xi1), r3(yi0)], [r3(xi1), r3(yi1)], [r3(xin), r3(yi1)]];
  }
  function walkable(opts) {
    const s = resolve(opts), V = s.V, F = fixtures(opts);
    const shellSole = C.gameplayGeometry({ variant: V.key }).SOLE[0];
    const box = (b) => [[b.x0, b.y0], [b.x1, b.y0], [b.x1, b.y1], [b.x0, b.y1]];
    return {
      saloon: { z: V.floorZ, polygon: shellSole.polygon, source: 'CamperIso.gameplayGeometry({variant:"' + V.key + '"}).SOLE[0].polygon',
        walker: 'foot_circle', foot_radius_m: WALKER.footRadius,
        obstructions: F.map((it) => {
          const o = { id: it.id, kind: it.kind, wall: it.wall, footprint: it.footprint,
            height_above_sole_m: it.h, treatment: treatment(it) };
          if (it.z0) o.bottom_above_sole_m = it.z0;
          return o;
        }) },
      head: { z: V.floorZ, polygon: headPolygon(V, F), walker: 'point_clamp',
        note: 'The wet bath is too small for the 0.35 m foot circle once its fixtures stand in it; walk it the way the boat cabins walk theirs, a point clamped to this polygon. Entered through INNER_DOORS.head_door.',
        obstructions: [
          { id: 'toilet', kind: 'fixture', footprint: box(HEAD.toilet), height_above_sole_m: HEAD.toilet.h, treatment: 'step_over' },
          { id: 'basin', kind: 'fixture', footprint: box(HEAD.basin), height_above_sole_m: HEAD.basin.h, treatment: 'waist_block' }] },
    };
  }

  // ---- interactables ----
  const ALL = ORDER.slice();
  function interactables(opts) {
    const s = resolve(opts), V = s.V, F = fixtures(opts), z = V.floorZ;
    const g = fx(F, 'galley_counter'), top = z + g.h, gx = r3((g.x0 + g.x1) / 2);
    const mid = (a) => r3((a[0] + a[1]) / 2);
    const bunk = fx(F, 'rear_bunk'), fr = fx(F, 'freezer'), gl = fx(F, 'gear_locker'), rr = fx(F, 'rod_rack');
    const pa = fx(F, 'pantry'), se = fx(F, 'nose_settee');
    const D = V.door, P = L.profAtY(V, -1.29), zs = z + HEAD.showerHeadAboveSole;
    const showerX = r3(-L.skinHW(V, P, zs, ROOM.liner) + 0.03);
    const R = (x, y) => [r3(x), r3(y), z];
    const A = (x, y, zz) => [r3(x), r3(y), r3(zz)];
    const it = (id, action, reach, at, level, mech, extra) => Object.assign(
      { id, action, reach_point: reach, at, level, visible_facings: ALL, _note: 'mechanism: ' + mech }, extra || {});
    return [
      it('door', 'step_out', R(0.56, 0.08), A(0.968, (D.y0 + D.y1) / 2, z), 'saloon',
        'the shell door (THRESHOLD in camperIsoRig.clipper.gameplay.json); the reach is the inside landing, where a 0.35 m foot stands clear',
        { door: { clear_width_m: r3(D.y1 - D.y0), landing_centre: [0.56, 0.08], landing_radius_m: WALKER.footRadius } }),
      it('bunk', 'sleep_save', R(0.40, -1.62), A(0, (bunk.y0 + bunk.y1) / 2, z + bunk.h), 'saloon',
        'InteriorBed (sleep, rest + save)',
        { bunk: { level: 'saloon', obstruction: 'rear_bunk', mattress_centre: A(0, (bunk.y0 + bunk.y1) / 2, z + bunk.h),
          pillow_end: A(bunk.x0 + 0.13, (bunk.y0 + bunk.y1) / 2, z + bunk.h), pillow_source: 'pillow on the street (north) side of the transverse double' } }),
      it('galley_sink', 'wash_dishes', R(-0.17, -0.45), A(gx, mid(GALLEY.sink), top), 'saloon', 'galley sink (wash up)', { counter: 'galley_counter' }),
      it('stove', 'cook', R(-0.17, 0.10), A(gx, mid(GALLEY.stove), top), 'saloon', 'camper stove pattern (cook)', { counter: 'galley_counter' }),
      it('prep', 'prep', R(-0.17, 0.55), A(gx, mid(GALLEY.prep), top), 'saloon', 'prep station (prep, then cook at the stove)', { counter: 'galley_counter' }),
      it('fridge', 'food_storage', R(-0.17, 1.02), A(g.x1, mid(GALLEY.fridge), z + 0.45), 'saloon',
        'food store (fridge: cold while there is power; the camper is on shore power)', { counter: 'galley_counter', kitchen_storage: 'kitchen_storage.fridge' }),
      it('pantry', 'food_storage', R(-0.17, 1.94), A(pa.x1, (pa.y0 + pa.y1) / 2, z + 1.0), 'saloon', 'food store (pantry)', { kitchen_storage: 'kitchen_storage.pantry' }),
      it('freezer', 'freeze_catch', R(0.12, 1.55), A((fr.x0 + fr.x1) / 2, (fr.y0 + fr.y1) / 2, z + fr.h), 'saloon',
        'catch freezer (the GinnyFreezer pattern, an IHold), powered from shore_power',
        { capacity_units: 40, capacity_source: 'owner 2026-11-17: bigger than Ginny\'s 20 (seat lean 40); the capacity belongs in a Def when the freezer is wired' }),
      it('gear_locker', 'storage', R(0.15, 0.72), A(gl.x0, (gl.y0 + gl.y1) / 2, z + 1.0), 'saloon', 'gear stowage (a StowageDef rack)',
        { stowage: 'stowage.camper_gear_locker', stowage_status: 'proposed id, no Def yet', capacity: 10 }),
      it('rod_rack', 'storage', R(0.12, 1.95), A((rr.x0 + rr.x1) / 2, (rr.y0 + rr.y1) / 2, z + rr.z0), 'saloon', 'rod stowage (StowageRack)',
        { stowage: 'stowage.rod_rack', capacity: 4 }),
      it('settee', 'sit', R(0, 2.24), A(0, (se.y0 + se.y1) / 2, z + se.h), 'saloon', 'seat (sit: rest short of a bed)',
        { seat: { level: 'saloon', obstruction: 'nose_settee', sit_point: A(0, (se.y0 + se.y1) / 2, z + se.h) } }),
      it('head_basin', 'wash', R(HEAD.stand[0], HEAD.stand[1]), A((HEAD.basin.x0 + HEAD.basin.x1) / 2, (HEAD.basin.y0 + HEAD.basin.y1) / 2, z + HEAD.basin.h), 'head',
        'wash station (washstation.sink)', { washstation: 'washstation.sink' }),
      it('head_toilet', 'toilet', R(HEAD.stand[0], HEAD.stand[1]), A((HEAD.toilet.x0 + HEAD.toilet.x1) / 2, (HEAD.toilet.y0 + HEAD.toilet.y1) / 2, z + HEAD.toilet.h), 'head',
        'wash station (washstation.toilet)', { washstation: 'washstation.toilet',
          seat: { level: 'head', obstruction: 'toilet', sit_point: A((HEAD.toilet.x0 + HEAD.toilet.x1) / 2, (HEAD.toilet.y0 + HEAD.toilet.y1) / 2, z + HEAD.toilet.h) } }),
      it('head_shower', 'bathe', R(HEAD.stand[0], HEAD.stand[1]), A(showerX, -1.29, zs), 'head',
        'wash station (washstation.shower); the whole head floor is the shower pan', { washstation: 'washstation.shower' }),
    ];
  }

  // ---- geometry helpers ----
  function rectDist(a, b) {
    const dx = Math.max(0, a.x0 - b.x1, b.x0 - a.x1), dy = Math.max(0, a.y0 - b.y1, b.y0 - a.y1);
    return Math.hypot(dx, dy);
  }
  function ptRect(p, b) {
    const dx = Math.max(0, b.x0 - p[0], p[0] - b.x1), dy = Math.max(0, b.y0 - p[1], p[1] - b.y1);
    return Math.hypot(dx, dy);
  }
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

  // the inside landing: the door's clear span, one foot diameter deep from the sole edge
  function landingKeepClear(V) {
    const e = L.interiorHW(V, L.profAtY(V, (V.door.y0 + V.door.y1) / 2)), d = 2 * WALKER.footRadius;
    return [[r3(e - d), r3(V.door.y0)], [r3(e), r3(V.door.y0)], [r3(e), r3(V.door.y1)], [r3(e - d), r3(V.door.y1)]];
  }

  // ---- the fit, in numbers (what the PR body quotes) ----
  function fit(opts) {
    const s = resolve(opts), V = s.V, F = fixtures(opts), Wk = walkable(opts);
    const get = (id) => fx(F, id);
    const g = get('galley_counter'), fr = get('freezer'), gl = get('gear_locker'), pa = get('pantry'), hr = get('head_room');
    const sole = Wk.saloon.polygon, hp = Wk.head.polygon;
    const soleHalfAt = (y) => L.interiorHW(V, L.profAtY(V, y));
    // every pair across the aisle (one north, one south) that a person walks between
    const floorFx = F.filter((f) => f.kind !== 'overhead');
    const across = [];
    for (const a of floorFx) for (const b of floorFx) {
      if (a.wall !== 'north' || b.wall !== 'south') continue;
      across.push({ between: [a.id, b.id], clear_m: r3(rectDist(a, b)) });
    }
    across.sort((p, q) => p.clear_m - q.clear_m);
    const D = V.door;
    const fz = { x: r3(fr.x1 - fr.x0), y: r3(fr.y1 - fr.y0), h: fr.h };
    const inner = Math.max(0, fz.x - 0.12) * Math.max(0, fz.y - 0.12) * Math.max(0, fz.h - 0.10);
    const headW = r3(hp[1][0] - hp[0][0]), headL = r3(hp[2][1] - hp[1][1]);
    const st = HEAD.stand;
    const standClear = Math.min(edgeDist(st, hp), ptRect(st, HEAD.toilet), ptRect(st, HEAD.basin));
    return {
      aisle_pairs_m: across,
      aisle_min_m: across.length ? across[0].clear_m : null,
      vestibule_clear_m: r3(soleHalfAt((hr.y0 + hr.y1) / 2) - hr.x1),
      door: { clear_width_m: r3(D.y1 - D.y0), foot_diameter_m: r3(2 * WALKER.footRadius),
        landing_clear_of_fixtures_m: r3(Math.min(...F.filter((f) => f.kind !== 'overhead').map((f) => ptRect([0.56, 0.08], f))) - WALKER.footRadius),
        landing_clear_of_sole_edge_m: r3(edgeDist([0.56, 0.08], sole) - WALKER.footRadius) },
      freezer: { footprint_m: [fz.x, fz.y], height_m: fz.h, inner_volume_m3: r3(inner), inner_volume_cuft: +(inner * 35.3147).toFixed(1),
        capacity_units: 40, ginny_units: 20 },
      gear_locker: { footprint_m: [r3(gl.x1 - gl.x0), r3(gl.y1 - gl.y0)], height_m: gl.h, capacity: 10 },
      rod_rack: { span_m: r3(get('rod_rack').y1 - get('rod_rack').y0), clear_under_m: get('rod_rack').z0, capacity: 4 },
      pantry: { footprint_m: [r3(pa.x1 - pa.x0), r3(pa.y1 - pa.y0)], height_m: pa.h },
      galley: { counter_m: [r3(g.x1 - g.x0), r3(g.y1 - g.y0)], top_above_sole_m: g.h, window_sill_above_sole_m: ROOM.bands[2] },
      wet_bath: { fits: true, clear_m: [headW, headL], area_m2: r3(headW * headL), door_clear_m: r3(HEAD.door.y1 - HEAD.door.y0),
        stand_clear_m: r3(standClear), foot_circle_fits: false,
        verdict: 'A wet bath fits: toilet, corner basin and a shower over one ' + headW + ' x ' + headL + ' m pan, with one standing spot ' + r3(standClear) +
          ' m clear of every fixture. The 0.35 m foot circle does not fit inside, so the head is a point-clamped level like a boat cabin.' },
      headroom_m: ROOM.headroom,
    };
  }

  // ---- the lot (C# StPetersCamperLot is the truth; the test re-derives every number) ----
  const SHED = { tailMetres: 9.0, along: 2.4, across: 1.8 };
  function lot(opts) {
    resolve(opts);
    const rr = Math.hypot(SHED.along, SHED.across) / 2;
    return {
      facing: 2, rig_dir: 6, convention: 'CounterClockwise: rigDir = (8 - facing) % 8',
      compass: { north: '-x', south: '+x (curb side, the door)', east: '+y (hitch)', west: '-y (tail)' },
      world_from_rig: 'world = LotPos + (y, -x)',
      source: 'StPetersCamperLot: Approach is the cottage, due south, so the curb faces south; NoseDirection is east. The north-wall rule therefore means rig -x.',
      shed_reservation: {
        purpose: 'the lot\'s small shed (seat lean: an 8-rod rack and a 12-slot gear store); its rig is another slice',
        centre_rig_m: [0, -SHED.tailMetres], offset: 'LotPos - NoseDirection * ' + SHED.tailMetres + ' (off the tail, west)',
        footprint_m: { along_camper: SHED.along, across_camper: SHED.across, world: SHED.along + ' m east-west x ' + SHED.across + ' m north-south' },
        reserve_radius_m: r3(rr), door_side: 'south: the curb side, toward the cottage and the path, like the camper door',
        csharp: 'StPetersCamperLot.ShedPos / ShedSize / ShedFootprintRadiusMetres / ShedDoorDirection',
      },
    };
  }

  // ---- validation (the generator refuses to write a sidecar that fails it) ----
  function validate(opts) {
    const s = resolve(opts), V = s.V, F = fixtures(opts), Wk = walkable(opts), I = interactables(opts), bad = [];
    const sole = Wk.saloon.polygon, hp = Wk.head.polygon;
    for (const f of F) for (const c of f.footprint) if (!inPoly(c, sole) && edgeDist(c, sole) > 0.02) bad.push(f.id + ' corner off the sole');
    for (let i = 0; i < F.length; i++) for (let j = i + 1; j < F.length; j++) {
      const a = F[i], b = F[j];
      const vsep = (a.kind === 'overhead' || b.kind === 'overhead') &&
        ((a.z0 || 0) >= b.h - 1e-6 || (b.z0 || 0) >= a.h - 1e-6);
      if (!vsep && rectDist(a, b) <= 0 && Math.min(a.x1, b.x1) - Math.max(a.x0, b.x0) > 1e-6 && Math.min(a.y1, b.y1) - Math.max(a.y0, b.y0) > 1e-6)
        bad.push(a.id + ' overlaps ' + b.id);
    }
    const floorFx = F.filter((f) => f.kind !== 'overhead' && f.kind !== 'room');
    const saloonPts = [];
    for (const a of I) {
      const p = [a.reach_point[0], a.reach_point[1]];
      if (a.level === 'saloon') {
        if (!inPoly(p, sole) || edgeDist(p, sole) < WALKER.reachClear) bad.push(a.id + ' reach too near the sole edge');
        for (const f of F) if (f.kind !== 'overhead' && ptRect(p, f) < WALKER.reachClear - 1e-6) bad.push(a.id + ' reach within ' + WALKER.reachClear + ' of ' + f.id);
        const lk = landingKeepClear(V);
        if (a.id !== 'door' && p[0] > lk[0][0] && p[1] > lk[0][1] && p[1] < lk[2][1]) bad.push(a.id + ' reach in the door landing');
        saloonPts.push([a.id, p]);
      } else {
        if (!inPoly(p, hp) || edgeDist(p, hp) < WALKER.reachClear - 1e-6) bad.push(a.id + ' reach off the head floor');
        for (const k of ['toilet', 'basin']) if (ptRect(p, HEAD[k]) < WALKER.reachClear - 1e-6) bad.push(a.id + ' reach within ' + WALKER.reachClear + ' of ' + k);
      }
    }
    for (let i = 0; i < saloonPts.length; i++) for (let j = i + 1; j < saloonPts.length; j++)
      if (Math.hypot(saloonPts[i][1][0] - saloonPts[j][1][0], saloonPts[i][1][1] - saloonPts[j][1][1]) < WALKER.reachSpacing)
        bad.push(saloonPts[i][0] + ' and ' + saloonPts[j][0] + ' closer than ' + WALKER.reachSpacing);
    for (const id of ['head_room', 'galley_counter', 'pantry']) if (fx(F, id).wall !== 'north' || fx(F, id).x1 >= 0) bad.push(id + ' is not on the north (-x) wall');
    const f = fit(opts);
    if (f.aisle_min_m < WALKER.aisleMin) bad.push('aisle ' + f.aisle_min_m + ' < ' + WALKER.aisleMin);
    if (f.door.clear_width_m < 2 * WALKER.footRadius) bad.push('door narrower than the foot');
    if (f.door.landing_clear_of_fixtures_m < 0 || f.door.landing_clear_of_sole_edge_m < 0) bad.push('door landing does not hold the foot');
    void floorFx;
    return bad;
  }

  // =========================== ART ===========================
  function hex2rgb(h) { return [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)]; }
  function rgb2hex(r, g, b) { const q = (n) => Math.max(0, Math.min(255, Math.round(n))).toString(16).padStart(2, '0'); return '#' + q(r) + q(g) + q(b); }
  function mix(a, b, t) { const A = hex2rgb(a), B = hex2rgb(b); return rgb2hex(A[0] + (B[0] - A[0]) * t, A[1] + (B[1] - A[1]) * t, A[2] + (B[2] - A[2]) * t); }
  function desat(hex, t) { const [r, g, b] = hex2rgb(hex); const l = 0.3 * r + 0.59 * g + 0.11 * b; return rgb2hex(r + (l - r) * t, g + (l - g) * t, b + (l - b) * t); }
  // the shell's ramps, no new colours; liner and leaf are the shell's own derivations
  const MATS = {
    wood: C.WOOD, trim: C.TRIM, chrome: C.CHROME, galv: C.GALV, canvas: C.CANVAS, iron: C.IRON,
    cushion: C.BODY.blue, porcelain: C.BODY.white, glass: C.GLASSD,
    liner: C.TRIM.map((c) => mix(desat(c, 0.34), '#4a4f52', 0.44)),
    leaf: C.TRIM.map((c) => mix(desat(c, 0.20), '#cdcfc9', 0.30)),
  };

  function camBasis(dir, elev) { const th = dir * Math.PI / 4, e = (elev != null ? elev : DEFAULT_ELEV) * DEG;
    return { th, ct: Math.cos(th), stt: Math.sin(th), se: Math.sin(e), ce: Math.cos(e) }; }
  function projVert(x, y, z, B) { const xr = x * B.ct - y * B.stt, yr = x * B.stt + y * B.ct;
    return { xr, yr, zr: z, sx: cx + xr * S, sy: groundY - (yr * B.se + z * B.ce) * S, d: (yr * B.ce - z * B.se) }; }
  function normal(a, b, c) { const ux = b.xr - a.xr, uy = b.yr - a.yr, uz = b.zr - a.zr, vx = c.xr - a.xr, vy = c.yr - a.yr, vz = c.zr - a.zr;
    const nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx, m = Math.hypot(nx, ny, nz) || 1; return [nx / m, ny / m, nz / m]; }
  function shadeOf(n, se, ce) { return n[0] * LN[0] + (n[1] * se + n[2] * ce) * LN[1] + (-n[1] * ce + n[2] * se) * LN[2]; }
  function plankTex(u, v) { const p = 0.18, f = ((u % p) + p) % p; if (f < 0.012) return -1.6; return ((Math.floor(u / p) * 7) % 3) * 0.12 - 0.12; }

  // a face: verts, material, brightness bias, plus the cutaway tags
  function face(out, v, mat, b, tag) { out.push(Object.assign({ v, mat, b: b || 0 }, tag || {})); }
  // orient so the model-space normal agrees with hint (rotation keeps handedness, so this survives projection)
  function oriented(v, hint) {
    const a = v[0], b = v[1], c = v[2];
    const u = [b[0] - a[0], b[1] - a[1], b[2] - a[2]], w = [c[0] - a[0], c[1] - a[1], c[2] - a[2]];
    const n = [u[1] * w[2] - u[2] * w[1], u[2] * w[0] - u[0] * w[2], u[0] * w[1] - u[1] * w[0]];
    return (n[0] * hint[0] + n[1] * hint[1] + n[2] * hint[2]) < 0 ? v.slice().reverse() : v;
  }
  function box(out, x0, x1, y0, y1, z0, z1, mat, b, tag, noTop) {
    b = b || 0;
    face(out, [[x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1]], mat, b - 0.30, tag);
    face(out, [[x1, y1, z0], [x0, y1, z0], [x0, y1, z1], [x1, y1, z1]], mat, b + 0.10, tag);
    face(out, [[x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1]], mat, b + 0.18, tag);
    face(out, [[x0, y1, z0], [x0, y0, z0], [x0, y0, z1], [x0, y1, z1]], mat, b - 0.42, tag);
    if (!noTop) face(out, [[x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]], mat, b + 0.34, tag);
  }
  const WALL_O = { north: [-1, 0], south: [1, 0], tail: [0, -1], nose: [0, 1] };
  // a fixture taller than the sill on its wall: split at the sill so the cutaway can section it
  function tallBox(out, it, z, x0, x1, y0, y1, h, mat, b) {
    const o = WALL_O[it.wall];
    if (h <= ROOM.sill) { box(out, x0, x1, y0, y1, z, z + h, mat, b, { seg: 'fx:' + it.id }); return; }
    box(out, x0, x1, y0, y1, z, z + ROOM.sill, mat, b, { seg: 'fx:' + it.id }, true);
    box(out, x0, x1, y0, y1, z + ROOM.sill, z + h, mat, b, { seg: 'fx:' + it.id + ':high', high: true, o });
    face(out, [[x0, y0, z + ROOM.sill], [x1, y0, z + ROOM.sill], [x1, y1, z + ROOM.sill], [x0, y1, z + ROOM.sill]], 'iron', 0.2,
      { seg: 'fx:' + it.id + ':cap', capOf: true, o });
  }

  function faces(opts) {
    const s = resolve(opts), V = s.V, z = V.floorZ, out = [], F = fixtures(opts);
    const US = L.stations(V);
    // floor: the shell's SOLE, planked
    const sole = walkable(opts).saloon.polygon;
    face(out, oriented(sole.map((p) => [p[0], p[1], z]), [0, 0, 1]), 'wood', -0.10, { seg: 'floor', tex: plankTex });
    // liner walls, banded, with the shell's windows as glass and the door as a closed leaf
    const rings = [];
    for (const u of US) { const P = L.prof(V, u); if (L.interiorHW(V, P) <= 0.06) continue; rings.push(P); }
    const winAt = (side, y, zm) => V.win.some((w) => w.kind === 'side' && w.side === side && y > w.y0 && y < w.y1 && zm > w.z0 && zm < w.z1);
    const doorAt = (side, y, zm) => side > 0 && y > V.door.y0 && y < V.door.y1 && zm < V.door.z1;
    for (let i = 0; i + 1 < rings.length; i++) {
      const A = rings[i], B = rings[i + 1], ym = (A.y + B.y) / 2;
      const cA = ceilingAt(V, A), cB = ceilingAt(V, B);
      for (const side of [1, -1]) {
        const wall = side > 0 ? 'south' : 'north';
        const br = ROOM.bands.map((h) => z + h);
        for (let k = 0; k < br.length; k++) {
          const z0A = Math.min(br[k], cA), z0B = Math.min(br[k], cB);
          const z1A = k + 1 < br.length ? Math.min(br[k + 1], cA) : cA, z1B = k + 1 < br.length ? Math.min(br[k + 1], cB) : cB;
          if (z1A - z0A < 1e-3 && z1B - z0B < 1e-3) continue;
          const X = (P, zz) => side * L.skinHW(V, P, zz, ROOM.liner);
          const v = [[X(A, z0A), A.y, z0A], [X(B, z0B), B.y, z0B], [X(B, z1B), B.y, z1B], [X(A, z1A), A.y, z1A]];
          const zm = (z0A + z1A + z0B + z1B) / 4;
          const mat = doorAt(side, ym, zm) ? 'leaf' : (winAt(side, ym, zm) ? 'glass' : 'liner');
          face(out, oriented(v, [-side, 0, 0]), mat, mat === 'glass' ? 0.1 : -0.05,
            { seg: 'wall_' + wall + (k === 0 ? '_low' : '_high'), high: k > 0, o: [side, 0] });
        }
      }
      // ceiling strip
      const hA = L.skinHW(V, A, cA, ROOM.liner), hB = L.skinHW(V, B, cB, ROOM.liner);
      face(out, oriented([[-hA, A.y, cA], [hA, A.y, cA], [hB, B.y, cB], [-hB, B.y, cB]], [0, 0, -1]), 'liner', 0.1, { seg: 'ceiling', lid: true });
    }
    // end caps across the last rings
    for (const [P, end] of [[rings[0], 'tail'], [rings[rings.length - 1], 'nose']]) {
      const oy = end === 'tail' ? -1 : 1, c = ceilingAt(V, P), br = ROOM.bands.map((h) => z + h).filter((q) => q < c).concat([c]);
      for (let k = 0; k + 1 < br.length; k++) {
        const a = L.skinHW(V, P, br[k], ROOM.liner), b = L.skinHW(V, P, br[k + 1], ROOM.liner);
        face(out, oriented([[-a, P.y, br[k]], [a, P.y, br[k]], [b, P.y, br[k + 1]], [-b, P.y, br[k + 1]]], [0, -oy, 0]), 'liner', 0,
          { seg: 'end_' + end + (k === 0 ? '_low' : '_high'), high: k > 0, o: [0, oy] });
      }
    }
    // ---- fixtures ----
    for (const it of F) {
      const t = { seg: 'fx:' + it.id };
      if (it.id === 'rear_bunk') {
        box(out, it.x0, it.x1, it.y0, it.y1, z, z + 0.36, 'wood', -0.2, t);
        box(out, it.x0 + 0.02, it.x1 - 0.02, it.y0 + 0.02, it.y1 - 0.02, z + 0.36, z + it.h, 'cushion', 0, t);
        box(out, it.x0 + 0.06, it.x0 + 0.42, it.y0 + 0.14, it.y1 - 0.14, z + it.h, z + it.h + 0.10, 'porcelain', 0.1, t);
      } else if (it.id === 'galley_counter') {
        box(out, it.x0, it.x1, it.y0, it.y1, z, z + it.h - 0.04, 'wood', -0.1, t);
        box(out, it.x0, it.x1 + 0.02, it.y0, it.y1, z + it.h - 0.04, z + it.h, 'trim', 0, t);
        const top = z + it.h + 0.002, x0 = it.x0 + 0.08, x1 = it.x1 - 0.06;
        face(out, [[x0, GALLEY.sink[0], top], [x1, GALLEY.sink[0], top], [x1, GALLEY.sink[1], top], [x0, GALLEY.sink[1], top]], 'chrome', -0.4, t);
        box(out, it.x0 + 0.03, it.x0 + 0.07, -0.45, -0.41, top, top + 0.24, 'chrome', 0.2, t);
        face(out, [[x0, GALLEY.stove[0], top], [x1, GALLEY.stove[0], top], [x1, GALLEY.stove[1], top], [x0, GALLEY.stove[1], top]], 'iron', 0, t);
        for (const yb of [-0.02, 0.22]) box(out, (x0 + x1) / 2 - 0.08, (x0 + x1) / 2 + 0.08, yb - 0.08, yb + 0.08, top, top + 0.02, 'galv', 0.1, t);
        face(out, [[x0, GALLEY.prep[0], top], [x1, GALLEY.prep[0], top], [x1, GALLEY.prep[1], top], [x0, GALLEY.prep[1], top]], 'wood', 0.3, t);
        const fxf = it.x1 + 0.004;
        face(out, oriented([[fxf, GALLEY.fridge[0], z + 0.06], [fxf, GALLEY.fridge[1], z + 0.06], [fxf, GALLEY.fridge[1], z + 0.82], [fxf, GALLEY.fridge[0], z + 0.82]], [1, 0, 0]), 'porcelain', 0.1, t);
        for (const zd of [0.30, 0.56]) face(out, oriented([[fxf, GALLEY.drawers[0], z + zd], [fxf, GALLEY.drawers[1], z + zd], [fxf, GALLEY.drawers[1], z + zd + 0.02], [fxf, GALLEY.drawers[0], z + zd + 0.02]], [1, 0, 0]), 'iron', 0, t);
      } else if (it.id === 'pantry') {
        tallBox(out, it, z, it.x0, it.x1, it.y0, it.y1, it.h, 'wood', -0.1);
      } else if (it.id === 'gear_locker') {
        tallBox(out, it, z, it.x0, it.x1, it.y0, it.y1, it.h, 'galv', 0);
      } else if (it.id === 'freezer') {
        box(out, it.x0, it.x1, it.y0, it.y1, z, z + it.h - 0.05, 'porcelain', -0.1, t);
        box(out, it.x0 - 0.01, it.x1, it.y0 - 0.01, it.y1 + 0.01, z + it.h - 0.05, z + it.h, 'porcelain', 0.15, t);
        box(out, it.x0 - 0.03, it.x0 - 0.01, (it.y0 + it.y1) / 2 - 0.15, (it.y0 + it.y1) / 2 + 0.15, z + it.h - 0.10, z + it.h - 0.06, 'chrome', 0.2, t);
      } else if (it.id === 'rod_rack') {
        const hi = { seg: 'fx:rod_rack:high', high: true, o: WALL_O.south };
        for (const yb of [it.y0 + 0.10, (it.y0 + it.y1) / 2, it.y1 - 0.10]) box(out, it.x0, it.x1, yb - 0.02, yb + 0.02, z + it.z0, z + it.z0 + 0.03, 'chrome', 0, hi);
        for (let r = 0; r < 4; r++) { const xr = it.x0 + 0.05 + r * (it.x1 - it.x0 - 0.10) / 3;
          box(out, xr - 0.012, xr + 0.012, it.y0, it.y1, z + it.z0 + 0.03, z + it.z0 + 0.055, r % 2 ? 'iron' : 'canvas', 0.1, hi); }
      } else if (it.id === 'nose_settee') {
        box(out, it.x0, it.x1, it.y0, it.y1, z, z + 0.30, 'wood', -0.2, t);
        box(out, it.x0 + 0.02, it.x1 - 0.02, it.y0 + 0.02, it.y1 - 0.02, z + 0.30, z + it.h, 'canvas', 0.05, t);
      } else if (it.id === 'head_room') {
        const w = HEAD.wall, hz = z + it.h, xp0 = it.x1 - w, xp1 = it.x1;
        const part = (x0, x1, y0, y1, o, id) => {
          box(out, x0, x1, y0, y1, z, z + ROOM.sill, 'liner', 0, { seg: 'head_partition_' + id + '_low' }, true);
          box(out, x0, x1, y0, y1, z + ROOM.sill, hz, 'liner', 0, { seg: 'head_partition_' + id + '_high', high: true, o });
          face(out, [[x0, y0, z + ROOM.sill], [x1, y0, z + ROOM.sill], [x1, y1, z + ROOM.sill], [x0, y1, z + ROOM.sill]], 'iron', 0.2, { seg: 'head_partition_' + id + '_cap', capOf: true, o });
        };
        part(xp0, xp1, it.y0, HEAD.door.y0, [1, 0], 'px');                           // +x face, aft of the door
        box(out, xp0, xp1, HEAD.door.y0, it.y1, z + 1.72, hz, 'liner', 0, { seg: 'head_partition_px_lintel', high: true, o: [1, 0] });
        // the transverse partitions follow the liner up the tumblehome in steps, never through it
        const steps = [0, ROOM.sill, 1.10, 1.50, 1.75, it.h];
        for (const [y0, y1, o, id] of [[it.y0, it.y0 + w, [0, -1], 'tail'], [it.y1 - w, it.y1, [0, 1], 'nose']]) {
          const P = L.profAtY(V, (y0 + y1) / 2);
          for (let k = 0; k + 1 < steps.length; k++) {
            const za = z + steps[k], zb = z + steps[k + 1];
            const xl = -Math.min(L.skinHW(V, P, za, ROOM.liner), L.skinHW(V, P, zb, ROOM.liner));
            const hi = k > 0, tag = { seg: 'head_partition_' + id + (hi ? '_high' : '_low') };
            if (hi) Object.assign(tag, { high: true, o });
            box(out, xl, xp1, y0, y1, za, zb, 'liner', 0, tag, k + 2 < steps.length);
          }
          face(out, [[it.x0, y0, z + ROOM.sill], [xp1, y0, z + ROOM.sill], [xp1, y1, z + ROOM.sill], [it.x0, y1, z + ROOM.sill]], 'iron', 0.2, { seg: 'head_partition_' + id + '_cap', capOf: true, o });
        }
        box(out, xp0 - 0.02, xp0, HEAD.door.y0 - 0.50, HEAD.door.y0 - 0.02, z, z + 1.72, 'leaf', 0, { seg: 'head_door_leaf', high: true, o: [1, 0] });  // slid open, aft
        const hp = walkable(opts).head.polygon;
        face(out, [[hp[0][0], hp[0][1], z + 0.012], [hp[1][0], hp[1][1], z + 0.012], [hp[2][0], hp[2][1], z + 0.012], [hp[3][0], hp[3][1], z + 0.012]], 'trim', -0.1, { seg: 'fx:shower_pan' });
        const T = HEAD.toilet, Bn = HEAD.basin;
        box(out, T.x0 + 0.08, T.x1, T.y0 + 0.06, T.y1 - 0.06, z, z + T.h, 'porcelain', 0, { seg: 'fx:toilet' });
        box(out, T.x0, T.x0 + 0.10, T.y0, T.y1, z + 0.30, z + 0.78, 'porcelain', 0.1, { seg: 'fx:toilet' });
        box(out, Bn.x0, Bn.x1, Bn.y0, Bn.y1, z + Bn.h - 0.16, z + Bn.h, 'porcelain', 0.1, { seg: 'fx:basin' });
        const sh = interactables(opts).find((a) => a.id === 'head_shower').at;
        box(out, sh[0] - 0.03, sh[0] + 0.05, sh[1] - 0.04, sh[1] + 0.04, sh[2] - 0.03, sh[2] + 0.03, 'chrome', 0.2, { seg: 'fx:shower_head' });
      }
    }
    return out;
  }

  // ---- cutaway: which faces a facing keeps ----
  function cutaway(dir) {
    const th = dir * Math.PI / 4, view = [-Math.sin(th), -Math.cos(th)];
    const near = (o) => !!o && (o[0] * view[0] + o[1] * view[1]) > ROOM.nearDot;
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
      if (sh < 0) sh = shadeOf([-n[0], -n[1], -n[2]], B.se, B.ce) * 0.9;     // inside faces read from either side
      const ramp = MATS[f.mat] || MATS.liner, fidx = sh * GAIN + BIAS + f.b, tex = f.tex;
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
            if (tex) { const u = w0 * ma[0] + w1 * mb[0] + w2 * mc[0], v = w0 * ma[1] + w1 * mb[1] + w2 * mc[1]; fi += tex(u, v); }
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
    return o;
  }

  // ---- the sidecar ----
  function sidecar(opts) {
    const s = resolve(opts), V = s.V, Wk = walkable(opts), F = fixtures(opts), hr = fx(F, 'head_room');
    return {
      schema: 'camper-interior@1', exportSymbol: 'CamperInterior', rig: 'camperInteriorRig.js', variant: s.key,
      shell_rig: 'camperIsoRig.js', shell_sidecar: 'docs/art/rigs/gameplay/dwellings/camperIsoRig.' + s.key + '.gameplay.json',
      generatedBy: 'CamperInterior.sidecar({variant:"' + s.key + '"}) via node tools/rig-recipes/camper-interior.mjs (--check fails on drift)',
      frame: { units: 'metres', scale_px_per_m: PX, origin: 'ground-centre of the BODY footprint (tongue excluded), the shell\'s',
        axes: '+x curb side (door side), +y bow/hitch, +z up', heading_independent: true },
      cell: { w: W, h: H, pivot: { x: cx, y: groundY }, facings: ORDER, convention: 'CounterClockwise',
        note: 'The shell\'s cell and pivot: a baked room composites under the exterior 1:1.' },
      ROOM: { sole_z_m: V.floorZ, headroom_m: ROOM.headroom, ceiling_z_max_m: r3(V.floorZ + ROOM.headroom), liner_inset_m: ROOM.liner,
        cutaway: { lid: 'ceiling, always culled', sill_above_sole_m: ROOM.sill, near_rule: 'a wall, partition or tall fixture whose outward plan normal o has o . view > ' + ROOM.nearDot + ' is sectioned to the sill; view = (-sin(dir*45deg), -cos(dir*45deg))' } },
      WALKABLE: Wk,
      THRESHOLD: { id: 'entry', shell: 'THRESHOLD in the shell sidecar is the door; this is its inside landing',
        door_clear_width_m: r3(V.door.y1 - V.door.y0), landing_centre: [0.56, 0.08], landing_radius_m: WALKER.footRadius,
        keep_clear_inside: landingKeepClear(V) },
      INNER_DOORS: [{ id: 'head_door', from: 'saloon', to: 'head', kind: 'sliding', clear_width_m: r3(HEAD.door.y1 - HEAD.door.y0),
        span: [[r3(hr.x1 - HEAD.wall / 2), HEAD.door.y0], [r3(hr.x1 - HEAD.wall / 2), HEAD.door.y1]],
        saloon_side: [0.20, -1.14], head_side: HEAD.stand.slice(),
        note: 'The leaf slides aft inside the partition; the saloon walker hands over to the head\'s point clamp at the span.' }],
      INTERACT: interactables(opts),
      mechanism_map: {
        door: 'shell door (step_out)', bunk: 'InteriorBed (sleep, rest + save)', galley_sink: 'galley sink (wash_dishes)',
        stove: 'camper stove pattern (cook)', prep: 'prep station (prep, then cook at the stove)',
        fridge: 'food store (kitchen_storage.fridge)', pantry: 'food store (kitchen_storage.pantry)',
        freezer: 'catch freezer, GinnyFreezer pattern (IHold), 40 units', gear_locker: 'StowageRack (stowage.camper_gear_locker, proposed)',
        rod_rack: 'StowageRack (stowage.rod_rack)', settee: 'seat (sit)',
        head_basin: 'wash station (washstation.sink)', head_toilet: 'wash station (washstation.toilet)', head_shower: 'wash station (washstation.shower)' },
      FIT: fit(opts),
      LOT: lot(opts),
      walker: Object.assign({ source: 'foot = CircleCollider2D radius 0.35 m (PersistentCoreBuilder, QuayEdgePlan.WalkerFootRadiusMetres); reach rules after the boat-interior kit' }, WALKER),
      _excluded: {
        wardrobe: 'No wardrobe: the shell layout\'s wardrobe slot went to the wet bath. Clothes stowage, if wanted, is over-bunk lockers.',
        dinette: 'No dinette or table: the curb wall holds the freezer, the gear locker and the rod rack the owner asked for.',
        second_berth: 'The nose settee is ' + r3(fx(F, 'nose_settee').x1 - fx(F, 'nose_settee').x0) + ' m long: a seat, not a berth.',
      },
      _confirm: {
        shell_layout: 'camperIsoRig.js LAYOUT.clipper (the dark seen-in volumes through the open door, and the shell sidecar\'s SOLE obstructions) still describes the old plan: head on the curb side, dinette, lounge. This room supersedes it; the shell is not edited here (its sha stamps two sidecars). Retire or regenerate it with the bake.',
        coop_sleep: 'One transverse double. The co-op rule (everyone in a separate bunk) is not met by this room alone.',
        freezer_power: 'The freezer and the fridge are powered; the shore_power hookup is on the street side of the shell.',
      },
      _bake_owed: {
        catalog: 'Register camperInteriorRig.js in RigCatalog with camperIsoRig.js as its prerequisite (the room throws without it).',
        mesh: 'Bake through VehicleMeshAssetBaker / the cutaway seam (ADR 0041/0048): ceiling culled, near walls sectioned to the 0.60 m sill per facing, faces tagged by seg.',
        interior_key: 'File the baked room under StPetersCamperLot.InteriorKey ("camper.clipper") so InteriorStood flips.',
        facing: 'The lot parks facing 2, which this counter-clockwise rig draws as dir 6; at dir 6 the south (curb) wall is the near wall.',
        look: 'Owner reviews the contact sheet before the bake lands.',
      },
    };
  }

  root.CamperInterior = { W, H, PX, DIRS: 8, pivot: { x: cx, y: groundY }, order: ORDER, defaultElev: DEFAULT_ELEV,
    ROOM, WALKER, LAYOUT, GALLEY, HEAD, MATS, list, resolve, fixtures, walkable, interactables, fit, lot, validate,
    cutaway, faces, render, project, anchors, sidecar,
    geom: { inPoly, edgeDist, ptRect, rectDist } };
})(typeof globalThis !== 'undefined' ? globalThis : window);
