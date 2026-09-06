/* Hidden Harbours — ATV pack kit writer (art side).  globalThis.ATV_KIT

   Bakes the sheets, measures every painted bbox, and writes the contract for export/vehicle-rig-pack/atv-pack/
   OFF THE LIVE RIG — no number in the contract is typed. Run from the project's script sandbox one stage at a
   time (each stage is a few hundred renders):

     (0,eval)(await readFile('Art/atvIsoRig.js'));
     (0,eval)(await readFile('Art/_atvKit.js'));
     const io = { readFile, saveFile, log, createCanvas, dir:'export/vehicle-rig-pack/atv-pack/' };
     await ATV_KIT(Object.assign({ stage:'dirtbike' }, io));   // sheets + _measure.dirtbike.json
     await ATV_KIT(Object.assign({ stage:'trike'    }, io));
     await ATV_KIT(Object.assign({ stage:'quad'     }, io));
     await ATV_KIT(Object.assign({ stage:'contract' }, io));   // folds the three measure files into atvPack.contract.json

   The contract stage hashes the rig bytes it read (D1: the stamp and the file cannot come apart) and REFUSES to
   write if the sidecar in Art/gameplay/ is stamped with a different hash — regenerate the sidecar from the
   builder page first. The _measure.*.json files are scratch; delete them once the contract is written. */
(function (root) {
  const ORDER = ['N','NE','E','SE','S','SW','W','NW'];
  const NAMES  = { dirtbike:'Enduro250', trike:'Trike200', quad:'UtilityQuad' };
  const BUILDS = { dirtbike:'enduro', trike:'beachTrike', quad:'wharfQuad' };     // the preset each body is measured and sheeted in
  const CUE_DIR = { roll:6, turn:3, bounce:6, steer:4, park:3 };                  // W, SE, W, S, SE — the facing each cue reads at
  const CUE_WHY = { roll:'W — side on, both wheels read', turn:'SE — nose quarter, street side: steer, yaw and (bike) lean all read',
                    bounce:'W — side on, the pitch reads', steer:'S — nose to camera, the front wheel(s) turning read', park:'SE — the street side, where the stand is' };

  async function sha256Hex(text) {
    const d = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
    return Array.from(new Uint8Array(d)).map(b => b.toString(16).padStart(2, '0')).join('');
  }

  root.ATV_KIT = async function (io) {
    const { readFile, saveFile, log, dir, stage } = io;
    const R = root.AtvIso; if (!R) throw new Error('ATV_KIT: load Art/atvIsoRig.js first');
    const W = R.W, H = R.H, PV = R.pivot, DEG = Math.PI / 180;
    const mk = io.createCanvas || ((w, h) => { const c = document.createElement('canvas'); c.width = w; c.height = h; return c; });
    const bbox = (p) => { let x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1, n = 0;
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (p[(y * W + x) * 4 + 3]) { n++; if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
      return { x0, y0, x1, y1, n }; };
    const diff = (a, b) => { let d = 0, al = 0;
      for (let i = 0; i < W * H; i++) { const p = a[i * 4 + 3] > 0, q = b[i * 4 + 3] > 0; if (p !== q) al++;
        if (p && q && (a[i * 4] !== b[i * 4] || a[i * 4 + 1] !== b[i * 4 + 1] || a[i * 4 + 2] !== b[i * 4 + 2])) d++; }
      return { d, al }; };
    const colours = (p) => { const s = new Set(); for (let i = 0; i < p.length; i += 4) if (p[i + 3]) s.add((p[i] << 16) | (p[i + 1] << 8) | p[i + 2]); return s.size; };
    const arr = (b) => [b.x0, b.y0, b.x1, b.y1];
    const union = (bs) => { const u = { x0: 1e9, y0: 1e9, x1: -1, y1: -1 };
      for (const b of bs) { if (!b.n) continue; u.x0 = Math.min(u.x0, b.x0); u.y0 = Math.min(u.y0, b.y0); u.x1 = Math.max(u.x1, b.x1); u.y1 = Math.max(u.y1, b.y1); } return u; };
    const size = (u) => [u.x1 - u.x0 + 1, u.y1 - u.y0 + 1];
    const r1 = (v) => +v.toFixed(1), r3 = (v) => +v.toFixed(3);

    // ---------------- one body: sheets + measurements ----------------
    if (NAMES[stage]) {
      const body = stage, name = NAMES[body], bike = body === 'dirtbike', trike = body === 'trike', quad = body === 'quad';
      const base = Object.assign({ body }, R.PRESETS[BUILDS[body]]);
      const ridden = bike ? { stand: 0 } : {};
      const bake = (d, o) => R.render(d, Object.assign({}, base, o || {}));
      const perFacing = (o) => { const out = {}, bs = []; for (let d = 0; d < 8; d++) { const b = bbox(bake(d, o)); out[ORDER[d]] = arr(b); bs.push(b); } return { out, u: union(bs), bs }; };
      const M = { body, label: R.BODIES[body].label, kind: R.BODIES[body].kind, sheet_stem: name, measured_with: base, painted_bbox: { _units: 'cell px, [x0,y0,x1,y1] inclusive, measured off the alpha channel' } };

      // states
      const states = bike ? { at_rest: {}, ridden: { stand: 0 }, plate: { lamp: false } }
                   : trike ? { at_rest: {}, bare: { rackR: false } }
                   : { at_rest: {}, bare: { rackF: false, rackR: false, hitch: false, winch: false }, winch: { winch: true } };
      const everything = [];
      for (const k of Object.keys(states)) { const r = perFacing(states[k]);
        M.painted_bbox[k] = r.out; M.painted_bbox['union_' + k] = arr(r.u); everything.push(...r.bs);
        if (k === 'at_rest') { M.painted_px_at_rest = r.bs.map(b => b.n); M.union_at_rest_size = size(r.u); } }
      M.painted_bbox._state_note = bike ? 'at_rest IS PARKED (stand 1, leaning 12deg to the street side, lamp). ridden = stand 0, upright. plate = lamp:false, the MX number plate.'
        : trike ? 'at_rest = rear rack fitted (the default). bare = rackR:false.' : 'at_rest = the wharfQuad build: both racks + hitch + winch. bare = nothing fitted. winch = the default build (racks + hitch) plus the winch.';

      // pose sweeps (ridden for the bike)
      const sweeps = { over_yaw_sweep: [{ yaw: 22.5 }, { yaw: -22.5 }], over_steer: [{ steer: 1 }, { steer: -1 }],
        over_suspension: [{ susF: 1, susR: 1 }, { susF: -1, susR: -1 }, { susF: 1, susR: -1 }, { susF: -1, susR: 1 }] };
      if (bike) sweeps.over_lean = [{ lean: 1 }, { lean: -1 }];
      let inCell = true;
      for (const k of Object.keys(sweeps)) { const bs = [];
        for (const p of sweeps[k]) for (let d = 0; d < 8; d++) { const b = bbox(bake(d, Object.assign({}, ridden, p))); bs.push(b); if (b.x0 < 0 || b.y0 < 0 || b.x1 >= W || b.y1 >= H) inCell = false; }
        M.painted_bbox[k] = arr(union(bs)); everything.push(...bs); }
      const ue = union(everything);
      M.painted_bbox.union_everything = arr(ue);
      M.painted_bbox.union_everything_size = size(ue);
      M.painted_bbox.all_poses_inside_cell = inCell;
      M.painted_bbox.crop_note = 'pack from union_everything to hold every fitting, pose, yaw step' + (bike ? ', the lean and the parked attitude' : '') + ' in one atlas cell; union_at_rest is the tight default build.';

      // roll seam at W
      { const a = bake(6, Object.assign({ roll: 0 }, ridden)), b = bake(6, Object.assign({ roll: 1 }, ridden)), m = bake(6, Object.assign({ roll: 0.5 }, ridden));
        const s = diff(a, b), mv = diff(a, m), n = bbox(a).n, P = R.SPECS[body];
        M.roll_seam = { facing: 'W', painted_px: n, roll1_vs_roll0: { colour_px: s.d, alpha_px: s.al, pct_of_painted: r3(100 * s.d / n) }, roll05_vs_roll0: { colour_px: mv.d, alpha_px: mv.al },
          distance_per_rev_m: R.BODIES[body].distancePerRev, rear_lugs_per_rev: Math.round(2 * Math.PI * R.BODIES[body].rearWheelR / 0.155),
          verdict: (s.d === 0 && s.al === 0) ? 'bit-identical — the loop closes exactly' : 'seamless, not bit-exact' };
        if (!quad) { // the front wheel turns rR/rF revolutions per rear revolution, so it does not close with the rear; measure the seam it leaves and prove the arithmetic
          const ratio = P.rR / P.rF, c = diff(a, bake(6, Object.assign({ roll: 1, rollF: 1 - ratio }, ridden)));
          M.roll_seam.front_wheel = { rev_per_rear_rev: r3(ratio), short_of_closing_deg: r3((1 - ratio) * 360), spoked: bike,
            closed_by_rollF: { value: r3(1 - ratio), colour_px: c.d, alpha_px: c.al, note: 'roll:1 with rollF = 1 - rR/rF added brings the front round to its start: the seam is the front wheel and nothing else' + (c.d === 0 && c.al === 0 ? ' (bit-identical)' : '') } }; } }

      // cues move
      M.cue_motion = {};
      for (const cue of R.cuesFor(body)) { const cyc = !!R.CYCLIC[cue], d = CUE_DIR[cue];
        const a = bake(d, Object.assign({}, ridden, R.CUES[cue](0))), b = bake(d, Object.assign({}, ridden, R.CUES[cue](cyc ? 0.25 : 1)));
        const df = diff(a, b); M.cue_motion[cue] = { facing: ORDER[d], why: CUE_WHY[cue], cyclic: cyc, compared: cyc ? 't=0 vs t=0.25' : 't=0 vs t=1', colour_px: df.d, alpha_px: df.al }; }

      // suspension: the body moves, the wheels stay grounded
      { const rest = bbox(bake(6, ridden)), comp = bbox(bake(6, Object.assign({ susF: 1, susR: 1 }, ridden))), ext = bbox(bake(6, Object.assign({ susF: -1, susR: -1 }, ridden)));
        M.suspension = { facing: 'W', bottom_row: { rest: rest.y1, compressed: comp.y1, extended: ext.y1 }, top_row: { rest: rest.y0, compressed: comp.y0, extended: ext.y0 },
          travel_m: { front: R.SPECS[body].TF, rear: R.SPECS[body].TR } };
        if (trike) { const z = diff(bake(6, {}), bake(6, { susR: 1 })); M.suspension.rear_rigid = { susR1_vs_susR0_colour_px: z.d, alpha_px: z.al, note: 'susR is accepted and ignored — the rear axle is rigid' }; } }

      // steering facts
      if (quad) { const a = R.steer.angles(1, R.SPECS.quad), P = R.SPECS.quad, wb = R.BODIES.quad.wheelbase;
        M.steering = { mechanism: 'bars about a vertical stem; the front pair yaw about their own kingpins, Ackermann-split', bars_max_deg: P.barsMax, inner_max_deg: r3(a.L), outer_max_deg: r3(a.R),
          turning_at_full_lock: { inner_wheel_radius_m: r3(wb / Math.tan(P.steerMax * DEG)), centreline_radius_m: r3(wb / Math.tan(P.steerMax * DEG) + P.wheelX) },
          full_lock_differs_px: diff(bake(4, { steer: 1 }), bake(4, { steer: -1 })).d }; }
      else { const P = R.SPECS[body], wb = R.BODIES[body].wheelbase;
        M.steering = { mechanism: 'the WHOLE front assembly (wheel, fork, fender, lamp, bars) turns about the raked steering axis through the head', rake_deg: P.rake, max_deg: P.steerMax,
          turning_at_full_lock: { radius_m: r3(wb / Math.tan(P.steerMax * DEG)), note: 'geometric, no lean, no slip' },
          full_lock_differs_px: diff(bake(4, Object.assign({ steer: 1 }, ridden)), bake(4, Object.assign({ steer: -1 }, ridden))).d }; }

      // the bike's stand and lean
      if (bike) { const P = R.SPECS.dirtbike, A1 = R.anchors(3, base), A0 = R.anchors(3, Object.assign({}, base, { stand: 0 }));
        M.stand = { default: 1, lean_deg_at_1: P.standLean, tip_world_at_1: A1.standTip.m, tip_z_error_m: Math.abs(A1.standTip.m[2]), tip_stowed_at_0: A0.standTip.m, side: 'street (-x)',
          parked_vs_ridden_px: (() => { const z = diff(bake(3, {}), bake(3, { stand: 0 })); return { colour_px: z.d, alpha_px: z.al }; })() };
        const cL = bbox(bake(4, { stand: 0, lean: 1 })), cR = bbox(bake(4, { stand: 0, lean: -1 })), c0 = bbox(bake(4, { stand: 0 }));
        M.lean = { max_deg: P.leanMax, leanDeg_at_stand1: R.dims(base).leanDeg, leanDeg_lean1_stand0: R.dims({ body, stand: 0, lean: 1 }).leanDeg, leanDeg_lean1_stand1: R.dims({ body, stand: 1, lean: 1 }).leanDeg,
          ground_row_at_S: { upright: c0.y1, lean_curb: cL.y1, lean_street: cR.y1 }, note: 'the contact line is the axis, so the ground row does not move with lean' }; }

      // anchors by facing
      const anch = (o) => { const out = {}; for (let d = 0; d < 8; d++) { const A = R.anchors(d, Object.assign({}, base, o || {})), f = {};
        for (const k of Object.keys(A)) { const v = A[k]; if (v && typeof v === 'object' && 'x' in v) f[k] = { x: r1(v.x), y: r1(v.y), m: v.m }; } out[ORDER[d]] = f; } return out; };
      M.anchors = { _units: '{x,y} in cell px for that facing, m = [x,y,z] in model metres, already posed', measured_with: base, by_facing: anch() };
      if (bike) { M.anchors._note = 'by_facing is PARKED (stand 1, 12deg lean) — the default bake. by_facing_ridden is stand 0, the state a rider mounts into; the character rig wants these.'; M.anchors.by_facing_ridden = anch({ stand: 0 }); }
      else M.anchors._note = 'at rest, every fitting the preset carries. Grips turn with steer and every point rides the suspension — read anchors(dir, opts) live for a posed machine.';
      { const A = R.anchors(4, Object.assign({}, base, ridden)), B = R.anchors(4, Object.assign({}, base, ridden, { steer: 1 }));
        M.anchors.grips_turn_with_steer_px = r1(Math.hypot(B.gripL.x - A.gripL.x, B.gripL.y - A.gripL.y));
        M.anchors.seat_holds_under_steer_px = r1(Math.hypot(B.seat.x - A.seat.x, B.seat.y - A.seat.y)); }

      M.distinct_colours_at_rest = { W: colours(bake(6)), SE: colours(bake(3)) };

      // ---- sheets ----
      const sheet = async (file, cells, cols, meta) => { cols = cols || cells.length; const rows = Math.ceil(cells.length / cols);
        const cv = mk(W * cols, H * rows), c = cv.getContext('2d');
        cells.forEach((px, i) => c.putImageData(new ImageData(px, W, H), (i % cols) * W, Math.floor(i / cols) * H));
        await saveFile(dir + file, cv);
        return Object.assign({ file, cells: cells.length, layout: rows > 1 ? cols + ' x ' + rows : '1 row', grid: [W * cols, H * rows] }, meta); };
      const dirs = (o) => { const out = []; for (let d = 0; d < 8; d++) out.push(bake(d, o)); return out; };
      const strip = (cue, n, o) => R.frames(CUE_DIR[cue], n, Object.assign({}, base, o || {}), cue);
      const paintDesc = base.paint + ', weather ' + base.weather;
      const S = [];
      S.push(await sheet(name + '_' + base.paint + '_8dir.png', dirs(), 8, { pose: bike ? 'PARKED at rest — ' + BUILDS[body] + ' (' + paintDesc + ', lamp, stand 1: leaning 12deg onto the side stand)' : 'at rest — ' + BUILDS[body] + ' (' + paintDesc + (trike ? ', rear rack' : ', both racks + hitch + winch') + ')', order: ORDER.slice() }));
      if (bike) S.push(await sheet(name + '_' + base.paint + '_ridden_8dir.png', dirs({ stand: 0 }), 8, { pose: 'RIDDEN at rest — stand 0, upright. The state a rider mounts into; pool THIS for a moving bike, not the default.', order: ORDER.slice() }));
      if (quad) S.push(await sheet(name + '_' + base.paint + '_bare_8dir.png', dirs(states.bare), 8, { pose: 'bare — no racks, no hitch, no winch (' + paintDesc + ')', order: ORDER.slice() }));
      S.push(await sheet(name + '_' + base.paint + '_roll_W.png', strip('roll', 10), 10, { pose: 'one rear-wheel revolution at W, cyclic (frame 10 wraps to frame 0)' + (bike ? '; stand 0' : '') }));
      S.push(await sheet(name + '_' + base.paint + '_turn_SE.png', strip('turn', 8), 8, { pose: 'turn cue at SE, cyclic: steer sin(2pi t), yaw 14deg with it' + (bike ? ', lean -0.55 into the turn' : '') + ', rolling' + (bike ? ', stand 0' : '') }));
      S.push(await sheet(name + '_' + base.paint + '_bounce_W.png', strip('bounce', 8), 8, { pose: 'bounce cue at W, cyclic: susF/susR sin sweep 0.8 out of phase, rolling' + (trike ? ' — the rear does not move (rigid axle)' : '') + (bike ? ', stand 0' : '') }));
      S.push(await sheet(name + '_' + base.paint + '_steer_S.png', strip('steer', 8, ridden), 8, { pose: 'steer cue at S: full RIGHT lock -> full LEFT lock, 8 inclusive' + (bike ? '; baked at stand 0 — steering is a ridden act' : '') }));
      if (bike) { S.push(await sheet(name + '_' + base.paint + '_park_SE.png', strip('park', 8, { lean: 0 }), 8, { pose: 'park cue at SE: stand 0 -> 1, upright -> 12deg onto the stand, 8 inclusive. Run reversed to kick it up.' }));
        const mx = Object.assign({ body }, R.PRESETS.mxPlate);
        S.push(await sheet(name + '_' + mx.paint + '_plate_8dir.png', ORDER.map((_, d) => R.render(d, mx)), 8, { pose: 'mxPlate preset — ' + mx.paint + ', weather ' + mx.weather + ', lamp:false (white MX number plate at the lamp station), parked', order: ORDER.slice() })); }
      const paints = Object.keys(R.BODY);
      S.push(await sheet(name + '_paints_SE.png', paints.map(p => bake(3, { paint: p, weather: 0.22 })), 5, { pose: 'the ten harbour paints at SE, weather 0.22' + (bike ? ', parked' : ''), order: paints }));
      M.sheets = S;
      await saveFile(dir + '_measure.' + body + '.json', JSON.stringify(M, null, 2));
      log(body, 'sheets', S.length, 'union at rest', M.union_at_rest_size.join('x'), 'everything', M.painted_bbox.union_everything_size.join('x'), 'in cell', inCell, 'roll seam', JSON.stringify(M.roll_seam.roll1_vs_roll0), 'colours', JSON.stringify(M.distinct_colours_at_rest));
      return M;
    }

    // ---------------- the contract ----------------
    if (stage === 'contract') {
      const src = await readFile(R.RIG_URL), sha = await sha256Hex(src);
      const side = JSON.parse(await readFile('Art/gameplay/atvIsoRig.atvPack.gameplay.json'));
      if (side.derivedFromRigSha256 !== sha) throw new Error('ATV_KIT: the sidecar is stamped ' + side.derivedFromRigSha256.slice(0, 12) + '… but the rig hashes ' + sha.slice(0, 12) + '… — regenerate the sidecar from ATV Pack Iso.dc.html before writing a contract');
      const M = {}; for (const b of Object.keys(NAMES)) M[b] = JSON.parse(await readFile(dir + '_measure.' + b + '.json'));
      const SP = R.SPECS, B = R.BODIES;
      const dimsOf = (b) => Object.assign({}, B[b], { travel_m: { front: SP[b].TF, rear: SP[b].TR }, mass_kg_estimate: SP[b].mass, y_extent_m: [SP[b].yMin, SP[b].yMax] });
      const C = {
        kit: 'vehicle-rig-pack/atv-pack', rig: 'atvIsoRig.js', exportSymbol: 'AtvIso', rigSha256: sha,
        bodies: Object.keys(NAMES), label: 'ATV Set: Enduro 250, Trike 200, Utility Quad 4x4', kind: 'saddle_vehicles',
        bake: {
          scale_px_per_m: R.PX, cell: { W, H }, pivot: { x: PV.x, y: PV.y }, dirs: 8, order: ORDER.slice(),
          camera: { elev_deg: R.defaultElev, step_deg: 45, projection: 'flat-facet 3/4, z-buffered, ordered dither (4x4 Bayer), depth-edge darkening, no AA, binary alpha' },
          keyline: { default: false, adr: 'ADR-0031 ringless', opt_in: '{outline:true}' },
          shading: { gain: 3.1, bias: 2.55, edge: 0.16, key: 'fixed upper-LEFT' },
          one_cell: 'one 256 x 192 cell for all three bodies; model z=0 projects to the pivot row in all 8 facings at every pose — leaned, parked, on full bump',
          no_rider: 'THE BAKE CARRIES NO RIDER. The character rig mounts on anchors(dir, opts): seat, gripL/gripR, pegL/pegR, already posed through steer, suspension and lean.',
          parked_default: 'the dirtbike DEFAULTS TO stand 1 — she bakes parked, leaning 12deg to the street side. Every rolling cue sets stand 0. See per_body.dirtbike.painted_bbox.ridden.',
          facing_note: 'dir 0 (N) shows the TAIL: the machine points away. dir 4 (S) shows the nose. Street side (-x) reads at NE E SE; curb side at SW W NW. Exhaust curb side, side stand street side.'
        },
        paints: Object.keys(R.BODY), presets: R.PRESETS,
        cues: { list: Object.keys(R.CUES), cyclic: Object.keys(R.CYCLIC).filter(k => R.CYCLIC[k]), per_body: { dirtbike: R.cuesFor('dirtbike'), trike: R.cuesFor('trike'), quad: R.cuesFor('quad') },
          frames_baked: { roll: 10, turn: 8, bounce: 8, steer: 8, park: 8 }, t_rule: 'cyclic cues sample t = i/n (frame n wraps to 0); steer and park sample t = i/(n-1), both ends inclusive',
          stand_rule: 'roll, turn and bounce set stand 0 themselves; steer does not — the steer sheet is baked with stand 0 passed in' },
        articulation: {
          roll: { params: ['roll', 'rollF', 'rollR'], units: 'revolutions of the REAR wheel', distance_per_rev_m: { dirtbike: B.dirtbike.distancePerRev, trike: B.trike.distancePerRev, quad: B.quad.distancePerRev },
            front_scaling: 'bike and trike: the front wheel is a different radius and turns rR/rF times as fast, so both tread the same road', cyclic: 'exact — the lug pitch is fitted to a whole number of lugs per revolution; see per_body.*.roll_seam' },
          steer: { param: 'steer', range: [-1, 1], sense: '+1 = full LEFT (nose swings toward -x) — the dually\'s convention',
            dirtbike: { mechanism: 'whole front assembly about the raked head axis', rake_deg: SP.dirtbike.rake, max_deg: SP.dirtbike.steerMax, turning_radius_m: M.dirtbike.steering.turning_at_full_lock.radius_m },
            trike: { mechanism: 'whole front assembly about the raked head axis', rake_deg: SP.trike.rake, max_deg: SP.trike.steerMax, turning_radius_m: M.trike.steering.turning_at_full_lock.radius_m },
            quad: { mechanism: 'bars about a vertical stem; front pair yaw about their own kingpins, Ackermann', bars_max_deg: SP.quad.barsMax, inner_max_deg: M.quad.steering.inner_max_deg, outer_max_deg: M.quad.steering.outer_max_deg, turning: M.quad.steering.turning_at_full_lock },
            coupling: 'steer, yaw and lean are published together and NOT coupled by the rig; the turn cue couples them for the sheet only' },
          suspension: { params: ['susF', 'susR'], range: [-1, 1], travel_m: { dirtbike: { front: SP.dirtbike.TF, rear: SP.dirtbike.TR }, trike: { front: SP.trike.TF, rear: SP.trike.TR }, quad: { front: SP.quad.TF, rear: SP.quad.TR } },
            rule: 'the BODY moves and pitches, the wheels stay on the ground: dz(y) linear between the axles, applied to every sprung polygon and every SADDLE anchor', trike_rear: 'RIGID (travel 0). susR is accepted and ignored; the balloon tires are the springs.' },
          lean: { bodies: ['dirtbike'], param: 'lean', range: [-1, 1], max_deg: SP.dirtbike.leanMax, axis: 'the tyre contact line — world y at x=0, z=0 — so the ground row never moves', sense: '+1 leans toward the CURB (+x); cornering leans INTO the turn: steer +1 pairs with lean -1',
            stacks_with_stand: 'leanDeg = lean*' + SP.dirtbike.leanMax + ' - stand*' + SP.dirtbike.standLean + '; dims().leanDeg publishes the sum and the rider rolls with it' },
          stand: { bodies: ['dirtbike'], param: 'stand', range: [0, 1], default: 1, side: 'street (-x)', lean_deg_at_1: SP.dirtbike.standLean, hinge: SP.dirtbike.standPivot, tip_world_at_1: M.dirtbike.stand.tip_world_at_1,
            rule: 'she BAKES PARKED. A dirtbike does not stand upright unridden; the roll/turn/bounce cues set stand 0 and a rider owns the upright state.' },
          yaw: { param: 'yaw', units: 'degrees', range: [-45, 45], what: 'heading between the facings. The model turns about z under the fixed key before projection, so the shading is rebaked, not rotated; 45deg at a facing is bit-identical to the next facing.', pivot: 'unaffected at any yaw' },
          parts: { dirtbike: { lamp: true, _alt: 'lamp:false = MX number plate at the same station, no light at night' }, trike: { rackR: true }, quad: { rackF: true, rackR: true, hitch: true, winch: false } },
          night: { param: 'night', effect: 'headlamps swap to the glow ramp and spill one pixel; tail lamps do not glow' },
          weather: { param: 'weather', range: [0, 1], default: 0.32, effect: 'greys and grimes the plastics, rusts the running gear, speckles' },
          ramp_budget: '11 ramps at most on any build of any body; the fleet cap is 16'
        },
        per_body: {}
      };
      for (const b of Object.keys(NAMES)) { const m = M[b];
        C.per_body[b] = Object.assign({ label: m.label, kind: m.kind, sheet_stem: m.sheet_stem, dims: dimsOf(b), measured_with: m.measured_with, painted_bbox: m.painted_bbox, painted_px_at_rest: m.painted_px_at_rest, union_at_rest_size: m.union_at_rest_size,
          roll_seam: m.roll_seam, cue_motion: m.cue_motion, suspension: m.suspension, steering: m.steering }, m.stand ? { stand: m.stand, lean: m.lean } : {}, { distinct_colours_at_rest: m.distinct_colours_at_rest, anchors: m.anchors, sheets: m.sheets }); }
      C.sidecar = 'atvIsoRig.atvPack.gameplay.json';
      C.sidecar_note = 'one file, three bodies keyed under bodies.{dirtbike,trike,quad}; byte-identical to Art/gameplay/ in the art workspace and stamped with the same rigSha256 as above';
      await saveFile(dir + 'atvPack.contract.json', JSON.stringify(C, null, 2));
      log('contract written; rig sha', sha, '; sheets', Object.keys(NAMES).map(b => b + ' ' + M[b].sheets.length).join(', '));
      return C;
    }
    throw new Error('ATV_KIT: unknown stage ' + stage);
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
