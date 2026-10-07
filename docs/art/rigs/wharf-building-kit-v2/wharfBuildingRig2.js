/* Hidden Harbours — WHARF BUILDINGS, pass 2 (wharfBuildingRig2.js → globalThis.WharfBuilding2), 2026-09-26.
   Load order: interiorPropRig.js, coastalPass.js (CoastalPass.light), [buildingLifecycleRig.js], [weatherSky.js],
   wharfBuildingRig2.geo.js, wharfBuildingRig2.js. Pass 1 (Art/wharfBuildingRig.js → WharfBuilding) is untouched;
   render(dir, {classic:true}) hands back pass 1's picture when it is loaded.

   THE LIGHT is not baked. frame() rasterises a G-buffer through the fleet's camera (orthographic, 40°, 45° steps, 32 px
   = 1 m) and relight() lights it from any WeatherSky.at() sky with the trees' / wharves' / houses' law (CoastalPass.light):
   sky visibility from nine occlusion maps, a sun map so eaves, hoods, the monitor and the stacks shade the building, rain
   by porosity with glints on lit wet tops, fog, snow held by material, a backlit rim; castShadow() on the ground (levels
   1 canopy · 2 partial · 3 full). No dither, no keyline (outline:true for the A/B). Frames are cropped to the union of the
   eight facings, so the pivot sits at the same pixel in every facing: fr.pivot, fr.x0 / fr.y0 in the 1200 × 1160 cell.

   API (model metres: +x the dock wall, +y out of the show gable; world [east, north]; screen px in the pass-1 cell)
     render(dir, o) · renderLive · frame(dir, o) · relight(fr, sky, o) · castShadow(fr, sky) · view(fr, ch, sky)
     dims · footprint · layout · placement · anchors(dir, o) · stations(o) · lightsOn(o, sky) · lights(dir, o)
     gameplay(o) → hidden-harbours/wharf-building-gameplay@1 · checks(o) · stepPlan(dz) · project(dir, p, elev)
   Options: pass 1's builder surface (type shape size siding base body roof door windows winDensity cupola loft dock hvac
   stacks vents sign boom weather night elev) plus kept · plinth · stove · goods · personnel · mirror · doorOpen · bayOpen ·
   loftOpen · persOpen · cutaway:'section' · storey:'ground'|'loft' · cutH · season · floorH · time / sky / cloud / rain /
   fog / snow · occupancy · schedule · lamp · phase / decay / burnt (BuildingLifecycle) · classic · outline               */
(function (root) {
  'use strict';
  const G = root.WharfBuildingGeo2; if (!G) throw new Error('wharfBuildingRig2.js: load wharfBuildingRig2.geo.js first');
  const PX = 32, S = 32, W = 1200, H = 1160, cx = 600, groundY = 780, DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const { clamp, r3, resolve, mats, assemble, gableTop } = G, r1 = (v) => Math.round(v * 10) / 10;
  const NAMES = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  // v9.2 world fit, every creator body (export/character-v9.2-import-kit/reports/worldfit.txt); the ladder numbers are WharfRig2's
  const CHAR92 = { rig: 'characterIsoRig9.js', revision: '9.2', radiusMax: 0.229, footprintMax: [0.458, 0.370],
    fits: { bench: [0.643, 0.853], knife: [0.598, 0.760], load: [0.718, 0.978], rest: [0, 1.012], rail: [0.549, 0.724], rung: [0.211, 0.273], stepDown: [0.06, 1.30], stepUp: [0.549, 0.724], stepUpNoGrip: [0.06, 1.30] },
    rung: 0.24, standoff: 0.275, ladderRate: 0.436, topOut: 0.72, topAdvance: 0.60, walk: 0.727, clearDoor: 0.758, headroom: 2.0, stair: { riserMax: 0.19, goingMin: 0.26, widthMin: 0.9, outsideRiserMax: 0.2 } };
  const SCHEDULES = { fisher: [[4.0, 6.25], [15.5, 20.5]], store: [[6.5, 7.5], [16.0, 18.5]], plant: [[5.0, 17.5]], closed: [] };
  const REQUESTS = [
    { clip: 'stairWalk', why: 'v9.2 walk plants on a level; a flight of 0.17–0.19 m risers wants a stepped stride', now: 'walk at 0.727 m/s along the flight, root z linear from the bottom nosing to the top: the feet float at most ± riser / 2 (2.1–2.3 px). Exact for every creator body instead: boardDown per riser (played backward going up), 0.57 s a riser' },
    { clip: 'walkSlope', why: 'the gear ramps at the wide doors are 16–17°', now: 'walk at 0.727 m/s, root z linear from the foot to the sill (as the wharf gangways)' },
    { clip: 'ladderOn / ladderOff', why: 'v9.2 has the ladder loop and no way on or off it at the top', now: 'up: board at railZ 0.72 from the top station (three rungs down), the root advanced 0.60 m onto the loft over t 0.28–0.88. Down: blend 300 ms from idle 0.55 m back from the hatch edge to ladderDown u 0 at the top station' },
    { clip: 'sit', why: 'nets are mended and tallies kept sitting', now: 'the bench clip at the bench height (0.80), standing' },
    { clip: 'carry box', why: 'a fish box goes in two hands', now: 'the tray carry (CARRIES.tray) over walk, idle, board and boardDown' },
  ];
  const DEAD = { abandoned: 1, collapsing: 1, ruin: 1 };

  function camBasis(dir, elev) { const th = dir * Math.PI / 4, e = (elev != null ? elev : DEFAULT_ELEV) * DEG; return { ct: Math.cos(th), st: Math.sin(th), se: Math.sin(e), ce: Math.cos(e) }; }
  function project(dir, p, elev) { const B = camBasis(dir, elev), xr = p[0] * B.ct - p[1] * B.st, yr = p[0] * B.st + p[1] * B.ct; return { x: cx + xr * S, y: groundY - (yr * B.se + (p[2] || 0) * B.ce) * S }; }
  const toWorld = (p, dir) => { const a = dir * Math.PI / 4; return [p[0] * Math.cos(a) - p[1] * Math.sin(a), p[0] * Math.sin(a) + p[1] * Math.cos(a)]; };
  const compass = (v) => NAMES[((Math.round(Math.atan2(v[0], v[1]) / (Math.PI / 4)) % 8) + 8) % 8];

  // ---- geometry for the API: doors shut, no section ---------------------------------------------------------------------
  const GEO = new Map(), NOGEO = { doorOpen: 1, bayOpen: 1, loftOpen: 1, persOpen: 1, cut: 1, weather: 1, kept: 1, season: 1, cutH: 1, storey: 1 };
  function geo(o) { const s = resolve(o), k = {}; for (const key of Object.keys(s)) if (!NOGEO[key]) k[key] = s[key]; const key = JSON.stringify(k); let g = GEO.get(key); if (g) return g;
    g = assemble(Object.assign({}, o, { cutaway: null, doorOpen: 0, bayOpen: 0, loftOpen: 0, persOpen: 0 }), null, false); g.out = null; if (GEO.size > 48) GEO.delete(GEO.keys().next().value); GEO.set(key, g); return g; }
  const mxP = (s, p) => s.mirror ? [-p[0], p[1]].concat(p.length > 2 ? [p[2]] : []) : p.slice();
  const mxR = (s, q) => s.mirror ? Object.assign({}, q, { x0: -q.x1, x1: -q.x0 }) : Object.assign({}, q);

  function dims(o) { const s = resolve(o); return { Wd: r3(s.Wd), Ln: r3(s.Ln), fH: s.fH, eaveZ: r3(s.eaveZ), ridgeZ: r3(s.ridgeZ), loftZ: s.loftZ != null ? r3(s.loftZ) : null, loftKind: s.loftKind, wallH: r3(s.wallH) }; }
  function footprint(o) { const s = resolve(o); return geo(o).foot.map(q => { const r = mxR(s, q); for (const k of ['x0', 'x1', 'y0', 'y1']) r[k] = r3(r[k]); return r; }); }
  function layout(o) { const s = resolve(o), g = geo(o);
    return { show: '+Y', dockSide: s.dock ? (s.mirror ? '-X' : '+X') : null, door: { x: 0, y: r3(s.hl), z: s.fH, width: r3(s.dw), height: r3(s.dh), clearWidth: r3(g.clearW), facing: '+Y', nrm: [0, 1], kind: s.door },
      personnel: s.pers && g.W ? null : null, bays: (g.bays || []).map(b => Object.assign({}, b)), loft: s.loftKind ? { kind: s.loftKind, z: r3(s.loftZ), standHalf: r3(s.standHalf) } : null, sign: g.sign }; }
  function placement(o) { const s = resolve(o), side = [s.mirror ? -1 : 1, 0], list = [];
    for (let d = 0; d < 8; d++) { const w = toWorld([0, 1], d); if (w[1] > 0.3) continue; const ws = toWorld(side, d);
      list.push({ dir: d, doorFaces: compass(w), via: w[1] < -0.3 ? 'door' : 'side', showSide: (s.dock || s.goods) ? (ws[1] < -0.3 ? 'seen' : ws[1] < 0.3 ? 'edge' : 'hidden') : null }); }
    const rank = (f) => (f.via === 'door' ? 0 : 4) + (f.showSide === 'seen' ? 0 : f.showSide === 'edge' ? 1 : f.showSide === 'hidden' ? 2 : 0) + (f.dir % 2 ? 0 : 0.5);
    list.sort((a, b) => rank(a) - rank(b) || a.dir - b.dir);
    return { show: '+Y', entry: '+Y', dockSide: s.dock ? (s.mirror ? '-X' : '+X') : null, facings: list }; }
  function anchors(dir, o) { o = o || {}; const s = resolve(o), g = geo(o), el = o.elev;
    const pt = (p, z) => { const m = s.mirror ? [-p[0], p[1]] : [p[0], p[1]], q = project(dir, [m[0], m[1], z || 0], el), w = toWorld(m, dir); return { x: r1(q.x), y: r1(q.y), m: [r3(m[0]), r3(m[1])], w: [r3(w[0]), r3(w[1])], z: r3(z || 0) }; };
    return { Wd: r3(s.Wd), Ln: r3(s.Ln), stacks: g.stacks.map(q => pt(q, q[2])), door: pt([0, s.hl], s.fH + 1.0), threshold: pt([0, s.hl], s.fH), ridge: pt([0, 0], s.ridgeZ),
      show: '+Y', doorFaces: compass(toWorld([0, 1], dir)), approach: pt(g.approach, 0), entryPath: g.entryPath.map(p => pt(p, p[2] || 0)),
      lamps: g.lamps.map(q => Object.assign(pt(q.p, q.p[2]), { id: q.id, kind: q.kind })), sign: g.sign ? Object.assign(pt([0, g.sign.y], (g.sign.z0 + g.sign.z1) / 2), { w: g.sign.w, h: r3(g.sign.z1 - g.sign.z0) }) : null }; }

  // ---- stepping, reach, stations -----------------------------------------------------------------------------------------
  function stepPlan(dz) { const s = r3(Math.abs(dz)), F = CHAR92.fits, inR = (v, R) => v >= R[0] - 1e-9 && v <= R[1] + 1e-9, P = (how, seq, fits) => ({ dz: r3(dz), how, seq, fits });
    if (s < 0.06) return P('level', [{ clip: 'walk' }], true);
    if (dz < 0) return s <= F.stepDown[1] ? P('step down', [{ clip: 'boardDown', once: true, opts: { railZ: s } }], true) : P('none', [], false);
    if (inR(s, F.stepUp)) return P('step up', [{ clip: 'board', once: true, opts: { railZ: s }, note: 'the left hand takes the edge' }], true);
    if (s <= F.stepUpNoGrip[1]) return P(s < F.stepUp[0] ? 'low step up' : 'high step up', [{ clip: 'boardDown', once: true, reverse: true, opts: { railZ: s }, note: 'boardDown played backward: the same frames, the same fit' }], true);
    return P('none', [], false); }
  function reach(g, s) { const r = CHAR92.radiusMax, st = 0.1; let X0 = -s.hw - 3, X1 = s.hw + 3, Y0 = -s.hl - 3, Y1 = s.hl + 3;
    for (const q of g.foot) { X0 = Math.min(X0, q.x0 - 2); X1 = Math.max(X1, q.x1 + 2); Y0 = Math.min(Y0, q.y0 - 2); Y1 = Math.max(Y1, q.y1 + 2); }
    if (g.approach) { X0 = Math.min(X0, g.approach[0] - 1.5); X1 = Math.max(X1, g.approach[0] + 1.5); Y1 = Math.max(Y1, g.approach[1] + 1.5); }
    const nx = Math.ceil((X1 - X0) / st) + 1, ny = Math.ceil((Y1 - Y0) / st) + 1;
    const segD = (x, y, a, b) => { const vx = b[0] - a[0], vy = b[1] - a[1], L2 = vx * vx + vy * vy || 1e-9, t = clamp(((x - a[0]) * vx + (y - a[1]) * vy) / L2, 0, 1); return Math.hypot(x - a[0] - vx * t, y - a[1] - vy * t); };
    const inRect = (x, y, q, m) => x > q.x0 - m && x < q.x1 + m && y > q.y0 - m && y < q.y1 + m;
    const blk0 = (x, y) => g.walls0.some(w => segD(x, y, w.a, w.b) < r + w.t / 2) || g.segs0.some(w => segD(x, y, w.a, w.b) < r + 0.03) || g.solids0.some(q => inRect(x, y, q, r));
    const L1 = s.loftKind === 'mezz' ? { x0: -s.hw + s.t, x1: s.hw - s.t, y0: -s.hl + s.t, y1: -s.hl + s.t + s.mezzD } : { x0: -s.hw + s.t, x1: s.hw - s.t, y0: -s.hl + s.t, y1: s.hl - s.t };
    const blk1 = (x, y) => !s.loftKind || x < L1.x0 + r || x > L1.x1 - r || y < L1.y0 + r || y > L1.y1 - r || gableTop(s, s.shape === 'shed' ? x : Math.abs(x) + 0.1) - s.loftZ < CHAR92.headroom || g.holes1.some(q => inRect(x, y, q, r)) || g.solids1.some(q => inRect(x, y, q, r));
    const cell = (p) => [Math.round((p[0] - X0) / st), Math.round((p[1] - Y0) / st)];
    function bfs(blk, starts) { const seen = new Uint8Array(nx * ny), Q = [];
      for (const p of starts) { let best = null; const [ci, cj] = cell(p); for (let rr = 0; rr <= 5 && !best; rr++) for (let dj = -rr; dj <= rr && !best; dj++) for (let di = -rr; di <= rr; di++) { const i = ci + di, j = cj + dj; if (i < 0 || j < 0 || i >= nx || j >= ny) continue; if (!blk(X0 + i * st, Y0 + j * st)) { best = [i, j]; break; } }
        if (best) { const k = best[1] * nx + best[0]; if (!seen[k]) { seen[k] = 1; Q.push(k); } } }
      for (let q = 0; q < Q.length; q++) { const k = Q[q], i = k % nx, j = (k - i) / nx; for (const [a, b] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const ii = i + a, jj = j + b; if (ii < 0 || jj < 0 || ii >= nx || jj >= ny) continue; const kk = jj * nx + ii; if (seen[kk]) continue; if (blk(X0 + ii * st, Y0 + jj * st)) continue; seen[kk] = 1; Q.push(kk); } }
      return seen; }
    const near = (seen, p) => { if (!seen) return false; const [i, j] = cell(p); for (let dj = -1; dj <= 1; dj++) for (let di = -1; di <= 1; di++) { const ii = i + di, jj = j + dj; if (ii >= 0 && jj >= 0 && ii < nx && jj < ny && seen[jj * nx + ii]) return true; } return false; };
    const seen0 = bfs(blk0, [g.approach]), at0 = (p) => near(seen0, p);
    const starts1 = g.ladders.filter(L => at0(L.bottom)).map(L => L.land).concat(g.stairs.filter(q => q.levels[1] === 1 && at0(q.landBot)).map(q => q.landTop));
    const seen1 = s.loftKind && starts1.length ? bfs(blk1, starts1) : null;
    return { at: (p, lv) => lv === 1 ? near(seen1, p) : near(seen0, p), grid: { X0, Y0, st, nx, ny, seen0, seen1 } }; }
  const fitIn = (k, z) => { const f = CHAR92.fits[k]; return !!f && z >= f[0] - 1e-9 && z <= f[1] + 1e-9; };
  const yawOf = (v) => r3(Math.atan2(v[0], v[1]) / DEG);
  function optsFor(q) { if (q.clip === 'bench' || q.clip === 'chop' || q.clip === 'lift' || q.clip === 'place') return { workZ: q.fixtureZ }; if (q.clip === 'reach') return { lift: q.fixtureZ }; if (q.clip === 'ladderDown') return { rung: CHAR92.rung }; return {}; }
  function stations(o) { o = o || {}; const s = resolve(o), g = geo(o), R = reach(g, s), out = [];
    const floatPx = (riser) => r1(riser / 2 * Math.cos(40 * DEG) * PX);
    // the main door and the way to it
    const ent = g.entry || {}, pathOK = ent.kind === 'ramp' ? ent.slopeDeg <= 24 : ent.kind === 'steps' ? stepPlan(ent.riser).fits : true;
    const entSeq = ent.kind === 'ramp' ? [{ clip: 'walk', loop: true, rootMotion: 'z linear from the ramp foot to the sill', request: 'walkSlope', slopeDeg: ent.slopeDeg }]
      : ent.kind === 'steps' ? Array.from({ length: ent.n }, () => stepPlan(ent.riser).seq[0]) : [{ verb: 'climbStair', stair: ent.stair }];
    out.push({ id: 'door', kind: 'door', verb: 'enter', also: ['leave'], clip: 'walk', at: [0, s.hl, s.fH], stand: g.approach.slice(0, 2), standZ: 0, level: 0, doorKind: s.door,
      opens: { doubleBarn: 'two leaves swing out 100°', slidingBarn: 'the leaves slide along the track', plank: 'one leaf swings out 100°', rollUp: 'the curtain rolls up into the hood', personnel: 'one leaf swings out 100°' }[s.door],
      clearWidth: r3(g.clearW), clearHeight: r3(s.dh), entry: Object.assign({}, ent), seq: entSeq, path: g.entryPath.map(p => p.map(r3)), fits: g.clearW >= CHAR92.clearDoor && s.dh >= CHAR92.headroom && pathOK });
    if (s.pers && g.W && s.type === 'processing') out.push({ id: 'personnel', kind: 'door', verb: 'enter', also: ['leave'], clip: 'walk', at: [s.dw / 2 + 1.0, s.hl, s.fH], stand: [s.dw / 2 + 1.0, s.hl + 0.9], standZ: s.fH, level: 0, doorKind: 'personnel', clearWidth: 0.9, clearHeight: 2.1, fits: true, note: 'onto the apron beside the roll-up' });
    for (const q of g.st) { const rec = Object.assign({ kind: q.clip === 'haul' ? 'hoist' : 'fixture', standZ: 0, level: 0 }, q); rec.opts = optsFor(q); rec.fits = q.fixture ? fitIn(q.fixture, q.fixtureZ) : true; if (q.fixture) rec.fit = CHAR92.fits[q.fixture].slice(); out.push(rec); }
    for (const L of g.ladders) out.push({ id: L.id, kind: 'ladder', verb: 'climbUp', also: ['climbDown'], clip: 'ladderDown', fixture: 'rung', fixtureZ: CHAR92.rung, fit: CHAR92.fits.rung.slice(), opts: { rung: CHAR92.rung },
      at: [L.x, L.rungY, L.z0 + CHAR92.rung], stand: L.bottom, standZ: L.z0, level: 0, rungs: L.rungs, rung: CHAR92.rung, standoff: CHAR92.standoff, hatch: L.hatch,
      top: { stand: L.land, z: r3(L.zTop), level: 1, station: r3(L.zTop - CHAR92.topOut), root: [L.x, r3(L.rungY + CHAR92.standoff)] },
      seq: { up: [{ clip: 'ladderDown', loop: true, reverse: true, opts: { rung: CHAR92.rung }, rootMotion: { d: [0, 0, 1], rate: CHAR92.ladderRate }, until: 'the feet on the rung ' + r3(L.zTop - CHAR92.topOut) }, { clip: 'board', once: true, opts: { railZ: CHAR92.topOut }, advance: CHAR92.topAdvance + ' m onto the loft over t 0.28–0.88', request: 'ladderOff' }],
        down: [{ clip: 'idle', note: 'at the landing, back to the hatch', request: 'ladderOn' }, { clip: 'ladderDown', loop: true, opts: { rung: CHAR92.rung }, rootMotion: { d: [0, 0, -1], rate: CHAR92.ladderRate }, until: 'the feet on the floor' }] },
      fits: fitIn('rung', CHAR92.rung) && fitIn('rail', CHAR92.topOut) && (L.hatch.x1 - L.hatch.x0) >= 0.9 && (L.hatch.y1 - L.hatch.y0) >= 0.9 });
    for (const q of g.stairs) { const lim = q.outside ? CHAR92.stair.outsideRiserMax : CHAR92.stair.riserMax, up = [-q.dir[0], -q.dir[1]];
      out.push({ id: q.id, kind: 'stair', verb: 'climbStair', also: ['descendStair'], clip: 'walk', n: q.n, riser: r3(q.riser), going: q.going, width: q.width, pitchDeg: r3(q.pitchDeg), outside: !!q.outside,
        at: [r3(q.bottom[0]), r3(q.bottom[1]), q.zBot], stand: q.landBot.map(r3), standZ: q.zBot, level: q.levels[0], faceYaw: yawOf(up),
        top: { stand: q.landTop.map(r3), z: r3(q.zTop), level: q.levels[1] }, headroomOpening: q.opening ? { y0: r3(q.opening.y0), y1: r3(q.opening.y1) } : null,
        seq: [{ clip: 'walk', loop: true, rootMotion: { from: [r3(q.bottom[0]), r3(q.bottom[1]), q.zBot], to: [r3(q.top[0]), r3(q.top[1]), r3(q.zTop)], rate: CHAR92.walk }, request: 'stairWalk', note: 'root z linear along the pitch line; the feet float at most ±' + floatPx(q.riser) + ' px' }],
        exact: { up: [{ clip: 'boardDown', once: true, reverse: true, opts: { railZ: r3(q.riser) }, repeat: q.n, advance: q.going }], down: [{ clip: 'boardDown', once: true, opts: { railZ: r3(q.riser) }, repeat: q.n, advance: q.going }], note: 'one riser per clip: fits every creator body (0.06–1.30)' },
        fits: q.riser <= lim + 1e-9 && q.going >= CHAR92.stair.goingMin - 1e-9 && q.width >= CHAR92.stair.widthMin - 1e-9 }); }
    for (const q of out) { if (q.faceYaw == null && q.at && q.stand) q.faceYaw = yawOf([q.at[0] - q.stand[0], q.at[1] - q.stand[1]]); if (q.kind === 'ladder') q.faceYaw = yawOf([0, -1]);
      q.reachable = R.at(q.stand, q.level || 0); if (q.top) q.topReachable = R.at(q.top.stand, q.top.level || 0);
      q.at = q.at.map(r3); q.stand = q.stand.map(r3); if (q.standZ != null) q.standZ = r3(q.standZ); if (q.fixtureZ != null) q.fixtureZ = r3(q.fixtureZ); }
    if (s.mirror) for (const q of out) { q.at[0] = -q.at[0]; q.stand[0] = -q.stand[0]; q.faceYaw = -q.faceYaw; if (q.top) { q.top.stand = [-q.top.stand[0], q.top.stand[1]]; if (q.top.root) q.top.root = [-q.top.root[0], q.top.root[1]]; } if (q.path) q.path = q.path.map(p => [-p[0], p[1], p[2]]);
      if (q.hatch) q.hatch = { x0: -q.hatch.x1, x1: -q.hatch.x0, y0: q.hatch.y0, y1: q.hatch.y1 }; if (q.seq && q.seq[0] && q.seq[0].rootMotion && q.seq[0].rootMotion.from) { const rm = q.seq[0].rootMotion; rm.from = [-rm.from[0], rm.from[1], rm.from[2]]; rm.to = [-rm.to[0], rm.to[1], rm.to[2]]; } }
    return { character: CHAR92, approach: mxP(s, g.approach).map(r3), stations: out, requests: REQUESTS }; }

  // ---- night: occupancy, emitters, lamps -----------------------------------------------------------------------------------
  function inUse(o, s, t) { const occ = o.occupancy; if (occ === 'working' || occ === 'home') return true; if (occ === 'away' || occ === 'closed' || occ === 'asleep') return false; if (o.decay && DEAD[o.decay]) return false;
    return (SCHEDULES[o.schedule || s.sched] || SCHEDULES.fisher).some(([a, b]) => t >= a && t < b); }
  function lightsOn(o, sky) { o = o || {}; const CPL = root.CoastalPass.light, s = resolve(o); sky = sky || CPL.skyOf(o); const need = CPL.lampNeed(sky), t = sky.time != null ? ((+sky.time % 24) + 24) % 24 : 14, use = inUse(o, s, t), dead = !!(o.decay && DEAD[o.decay]);
    const L = o.lamp, lampLv = (base) => L === 'off' ? 0 : typeof L === 'number' ? clamp(L, 0, 1) * need : base;
    return { need: r3(need), time: r3(t), inUse: use, windows: { ground: use ? need : 0, loft: use && s.loftKind === 'loft' ? need * 0.75 : 0, office: use ? need : 0 },
      lamps: { door: dead ? 0 : lampLv(use && need > 0.05 ? need : 0), pack: dead || (o.schedule || s.sched) === 'closed' ? 0 : lampLv(need > 0.05 ? need : 0) }, fire: use && s.stove && !dead ? 0.85 : 0 }; }
  function lightRig(fr, opts, sky) { const s = fr.s, L = lightsOn(opts, sky), g = fr.mdl.C, mir = (p) => s.mirror ? [-p[0], p[1], p[2]] : p;
    const emit = (name) => { if (name.indexOf('win:') === 0) return L.windows[name.split(':')[1]] || 0; if (name === 'lamp:door') return L.lamps.door; if (name.indexOf('lamp:') === 0) return L.lamps.pack; if (name === 'fire:stove') return L.fire; return 0; };
    const lights = []; for (const q of g.lamps) { const lv = q.kind === 'door' ? L.lamps.door : L.lamps.pack; if (lv > 0.01) lights.push({ p: mir(q.p), c: q.c, I: (q.kind === 'pack' ? 0.6 : 0.95) * lv, r: q.r }); }
    if (L.fire > 0.01 && g.stove) lights.push({ p: mir(g.stove), c: '#ff9a4a', I: 0.45 * L.fire, r: 2.2 });
    return { L, emit, lights }; }
  function lights(dir, o) { o = o || {}; const CPL = root.CoastalPass.light, s = resolve(o), g = geo(o), sky = CPL.skyOf(o), L = lightsOn(o, sky), el = o.elev, M = (p) => s.mirror ? [-p[0], p[1], p[2]] : p;
    const lamps = g.lamps.map(q => { const p = M(q.p), lv = q.kind === 'door' ? L.lamps.door : L.lamps.pack, pool = M([q.pool[0], q.pool[1], 0]), sc = project(dir, p, el), ps = project(dir, pool, el);
      return { id: q.id, kind: q.kind, colour: q.c, level: r3(lv), m: p.map(r3), screen: { x: r1(sc.x), y: r1(sc.y) }, pool: { m: [r3(pool[0]), r3(pool[1])], r: q.r, screen: { x: r1(ps.x), y: r1(ps.y) } } }; });
    return { time: L.time, need: L.need, inUse: L.inUse, schedule: o.schedule || s.sched, windows: L.windows, fire: L.fire, lamps }; }

  // ---- the live light --------------------------------------------------------------------------------------------------------
  const MODELS = new Map(), SKIP = { sky: 1, time: 1, cloud: 1, rain: 1, fog: 1, snow: 1, wind: 1, gust: 1, outline: 1, elev: 1, night: 1, lamp: 1, occupancy: 1, schedule: 1, classic: 1 };
  function liveModel(opts, dir) { const CP = root.CoastalPass; if (!CP || !CP.light) return null; const s = resolve(opts), o = {};
    for (const k of Object.keys(opts).sort()) if (!SKIP[k] && typeof opts[k] !== 'function') o[k] = opts[k];
    const key = JSON.stringify(o) + (s.cut ? '|' + (dir | 0) : ''); let m = MODELS.get(key); if (m) { MODELS.delete(key); MODELS.set(key, m); return m; }
    const LC = root.BuildingLifecycle, lc = !!(LC && LC.active && LC.active(opts)), C = assemble(opts, dir, lc), M = mats(s); let faces = C.out;
    if (lc) { const r = LC.apply(faces, M, { fH: s.fH, eaveZ: s.eaveZ, ridgeZ: s.ridgeZ, weather: s.weather, night: false, Wd: s.Wd, Ln: s.Ln, type: s.type }, opts); faces = r.faces; }
    if (s.mirror) faces = faces.map(f => Object.assign({}, f, { v: f.v.map(p => [-p[0], p[1], p[2]]) }));
    m = CP.light.model(faces, M); const metalRoof = s.roof === 'metalSeam' || s.roof === 'corrugated' || s.roof === 'rusted', WP = G.WPROP;
    for (const a of m.MA) { const p = a.key === 'roof' ? (metalRoof ? WP.roofMetal : WP.roofAsph) : a.key === 'body' ? (s.metalBody ? WP.bodyMetal : WP.body) : WP[a.key]; if (p) { a.por = p[0]; a.gloss = p[1]; a.hold = p[2]; } }
    m.C = C; m.s = s; m.crops = {}; m.lc = lc; if (MODELS.size > 10) MODELS.delete(MODELS.keys().next().value); MODELS.set(key, m); return m; }
  // the crop: every vertex and a 2 m ground margin, projected at all eight facings, so the pivot is one pixel for all eight
  function cropOf(m, elev) { const k = elev == null ? DEFAULT_ELEV : +elev; if (m.crops[k]) return m.crops[k];
    let bx0 = 1e9, bx1 = -1e9, by0 = 1e9, by1 = -1e9; for (const f of m.faces) for (const p of f.v) { if (p[0] < bx0) bx0 = p[0]; if (p[0] > bx1) bx1 = p[0]; if (p[1] < by0) by0 = p[1]; if (p[1] > by1) by1 = p[1]; }
    let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9; const add = (q) => { if (q.x < x0) x0 = q.x; if (q.x > x1) x1 = q.x; if (q.y < y0) y0 = q.y; if (q.y > y1) y1 = q.y; };
    for (let d = 0; d < 8; d++) { const B = camBasis(d, k); for (const f of m.faces) for (const p of f.v) { const xr = p[0] * B.ct - p[1] * B.st, yr = p[0] * B.st + p[1] * B.ct; add({ x: cx + xr * S, y: groundY - (yr * B.se + p[2] * B.ce) * S }); }
      for (const x of [bx0 - 2, bx1 + 2]) for (const y of [by0 - 2, by1 + 2]) add(project(d, [x, y, 0], k)); }
    const c = { x0: Math.floor(x0) - 2, y0: Math.floor(y0) - 2 }; c.w = Math.ceil(x1) + 3 - c.x0; c.h = Math.ceil(y1) + 3 - c.y0; m.crops[k] = c; return c; }
  // the silhouette alone over the eight facings (what the game's baker tight-crops to)
  function tightOf(m, elev) { const k = elev == null ? DEFAULT_ELEV : +elev; let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
    for (let d = 0; d < 8; d++) { const B = camBasis(d, k); for (const f of m.faces) for (const p of f.v) { const xr = p[0] * B.ct - p[1] * B.st, yr = p[0] * B.st + p[1] * B.ct, X = cx + xr * S, Y = groundY - (yr * B.se + p[2] * B.ce) * S; if (X < x0) x0 = X; if (X > x1) x1 = X; if (Y < y0) y0 = Y; if (Y > y1) y1 = Y; } }
    return { x0: Math.floor(x0), y0: Math.floor(y0), w: Math.ceil(x1) - Math.floor(x0) + 1, h: Math.ceil(y1) - Math.floor(y0) + 1 }; }
  function frame(dir, opts, fo) { opts = opts || {}; const m = liveModel(opts, dir); if (!m) return null; const c = cropOf(m, opts.elev), B = camBasis(dir, opts.elev);
    const fr = root.CoastalPass.light.frame(m, { ct: B.ct, st: B.st, se: B.se, ce: B.ce, S, ox: cx - c.x0, oy: groundY - c.y0 }, c.w, c.h, fo);
    fr.dir = dir; fr.opts = opts; fr.x0 = c.x0; fr.y0 = c.y0; fr.pivot = { x: cx - c.x0, y: groundY - c.y0 }; fr.s = m.s; return fr; }
  function relight(fr, sky, o) { o = o || {}; const CPL = root.CoastalPass.light; sky = sky || CPL.skyOf(fr.opts); const R = lightRig(fr, Object.assign({}, fr.opts, o), sky), s = fr.s;
    const indoor = s.cut ? [-(s.hw - s.t), s.hw - s.t, -(s.hl - s.t), s.hl - s.t, 0.35, 1.55] : null;
    return CPL.relight(fr, sky, Object.assign({ emit: R.emit, lights: R.lights, indoor }, o)); }
  function toCell(fr, px) { const out = new Uint8ClampedArray(W * H * 4);
    for (let y = 0; y < fr.H; y++) { const Y = y + fr.y0; if (Y < 0 || Y >= H) continue; for (let x = 0; x < fr.W; x++) { const X = x + fr.x0; if (X < 0 || X >= W) continue; const i = (y * fr.W + x) * 4; if (!px[i + 3]) continue; const j = (Y * W + X) * 4; out[j] = px[i]; out[j + 1] = px[i + 1]; out[j + 2] = px[i + 2]; out[j + 3] = 255; } }
    return out; }
  function renderLive(dir, opts) { opts = opts || {}; const fr = frame(dir, opts), sky = root.CoastalPass.light.skyOf(opts); return toCell(fr, relight(fr, sky, { outline: !!opts.outline })); }
  function render(dir, opts) { opts = (typeof opts === 'number') ? { elev: opts } : (opts || {}); const P1 = root.WharfBuilding;
    if (opts.classic && P1 && P1.render && P1 !== API) return P1.render(dir, opts);
    if (!root.CoastalPass || !root.CoastalPass.light) { if (P1 && P1.render) return P1.render(dir, opts); throw new Error('WharfBuilding2 needs coastalPass.js (CoastalPass.light)'); }
    return renderLive(dir, opts); }
  function castShadow(fr, sky, o) { return root.CoastalPass.light.castShadow(fr, sky || root.CoastalPass.light.REF_SKY, o); }
  function view(fr, ch, sky, o) { return (!ch || ch === 'lit') ? relight(fr, sky, o) : root.CoastalPass.light.view(fr, ch, sky, o); }

  // ---- checks ----------------------------------------------------------------------------------------------------------------
  function doorShare(o, dir) { const oo = Object.assign({}, o, { doorOpen: 0, cutaway: null }), m = liveModel(oo, dir), CPL = root.CoastalPass.light, c = cropOf(m, o.elev), B = camBasis(dir, o.elev), ti = m.TI['door.main'];
    if (ti == null) return 0; const C = { ct: B.ct, st: B.st, se: B.se, ce: B.ce, S, ox: cx - c.x0, oy: groundY - c.y0 };
    const full = CPL.frame(m, C, c.w, c.h, { noSky: true }), solo = CPL.frame(m, C, c.w, c.h, { noSky: true, only: (f) => f.ti === ti }); let a = 0, b = 0;
    for (let i = 0; i < c.w * c.h; i++) if (solo.gb.a[i]) { b++; if (full.gb.a[i] && full.gb.tg[i] === ti) a++; } return b ? a / b : 0; }
  function checks(o, co) { o = o || {}; co = co || {}; const s = resolve(o), g = geo(o), rows = [], row = (id, ok, v, note) => rows.push({ id, ok: !!ok, v, note: note || '' });
    const st = stations(o), bad = st.stations.filter(q => !q.fits || q.reachable === false || q.topReachable === false);
    row('stations', !bad.length, st.stations.length + ' verbs', bad.map(q => q.id + (q.fits ? '' : ' unfit') + (q.reachable === false ? ' unreachable' : '') + (q.topReachable === false ? ' top unreachable' : '')).join(', '));
    for (const q of st.stations.filter(q => q.kind === 'stair')) row('stair.' + q.id, q.fits, q.n + ' risers of ' + q.riser + ' · going ' + q.going + ' · ' + q.width + ' wide · ' + q.pitchDeg + '°');
    for (const q of st.stations.filter(q => q.kind === 'ladder')) row('ladder.' + q.id, q.fits, q.rungs.length + ' rungs at ' + q.rung + ' · top-out board 0.72 · hatch ' + r3(q.hatch.x1 - q.hatch.x0) + ' × ' + r3(q.hatch.y1 - q.hatch.y0));
    const dr = st.stations.find(q => q.id === 'door'); row('door.clear', dr.fits, 'clear ' + dr.clearWidth + ' × ' + dr.clearHeight + ' m · ' + (dr.entry.kind === 'ramp' ? 'ramp ' + dr.entry.slopeDeg + '°' : dr.entry.kind === 'steps' ? dr.entry.n + ' risers of ' + dr.entry.riser : 'apron stair'));
    if (s.sign) row('sign', !!g.sign && g.sign.z0 >= (g.doorTop || 0) + 0.2, g.sign ? 'board ' + g.sign.w + ' m wide from ' + g.sign.z0 + ', door head ' + r3(g.doorTop) : 'no room on the gable: left off');
    const roofOK = g.stacks.every(p => p[2] > s.ridgeZ); row('roof.stacks', roofOK, g.stacks.length + ' stacks and pipes, every one set into the roof and clear of the ridge');
    if (root.CoastalPass && root.CoastalPass.light && co.frames !== false) {
      const m = liveModel(o, 0), c = cropOf(m, o.elev); row('cell', c.x0 >= 0 && c.y0 >= 0 && c.x0 + c.w <= W && c.y0 + c.h <= H, 'crop ' + c.w + ' × ' + c.h + ' at (' + c.x0 + ', ' + c.y0 + ') in the ' + W + ' × ' + H + ' cell');
      let grid = null; const tc = tightOf(m, o.elev); for (let cols = 1; cols <= 8; cols++) { const rws = Math.ceil(8 / cols); if (tc.w * cols <= 2048 && tc.h * rws <= 2048) { grid = cols + ' × ' + rws; break; } }
      rows.push({ id: 'sheet', ok: true, info: true, v: grid ? '8 facings of the tight ' + tc.w + ' × ' + tc.h + ' cell pack ' + grid + ' under 2048' : '8 facings of the tight ' + tc.w + ' × ' + tc.h + ' cell need a 4096 sheet', note: grid ? '' : 'as pass 1\'s cannery (840 × 673): the game bakes it at 4096' });
      for (const f of placement(o).facings.filter(q => q.via === 'door')) { const v = doorShare(o, f.dir); row('door.d' + f.dir, v >= 0.6, Math.round(v * 100) + ' % of the door shows at facing ' + f.dir + ' (door ' + f.doorFaces + ')'); } }
    return { ok: rows.every(r => r.ok), rows }; }
  const tightCell = (o, elev) => tightOf(liveModel(o || {}, 0), elev);

  // ---- the gameplay sidecar ---------------------------------------------------------------------------------------------------
  function gameplay(o) { o = o || {}; const s = resolve(o), g = geo(o), st = stations(o), M = (q) => { const r = mxR(s, q); for (const k of ['x0', 'x1', 'y0', 'y1']) r[k] = r3(r[k]); return r; };
    const walk = [{ id: 'floor', z: s.fH, rect: M({ x0: -s.hw + s.t, x1: s.hw - s.t, y0: -s.hl + s.t, y1: s.hl - s.t }), surface: s.type === 'processing' ? 'concrete' : 'plank' }];
    if (s.loftKind) walk.push({ id: s.loftKind === 'mezz' ? 'mezzanine' : 'loft', z: r3(s.loftZ), rect: M(s.loftKind === 'mezz' ? { x0: -s.hw + s.t, x1: s.hw - s.t, y0: -s.hl + s.t, y1: -s.hl + s.t + s.mezzD } : { x0: -s.hw + s.t, x1: s.hw - s.t, y0: -s.hl + s.t, y1: s.hl - s.t }),
      holes: g.holes1.map(M), headroom: { minM: CHAR92.headroom, standHalfX: r3(s.standHalf) }, surface: s.loftKind === 'mezz' ? 'steel' : 'plank' });
    for (const f of g.foot) if (f.walk) walk.push({ id: f.id, z: f.z != null ? f.z : 0, rect: M(f), surface: /apron|dock/.test(f.id) ? 'concrete' : /ramp|steps/.test(f.id) ? 'plank' : 'concrete' });
    return { schema: 'hidden-harbours/wharf-building-gameplay@1', rig: 'wharfBuildingRig2.geo.js + wharfBuildingRig2.js', exportSymbol: 'WharfBuilding2', revision: '2.0', generated: 'WharfBuilding2.gameplay() off the live model — do not hand-edit, regenerate',
      frame: { units: 'metres', axes: '+x the dock wall, +y out of the show gable (the main door), +z up from the ground', origin: 'the ground centre under the building (the cell pivot)', scale_px_per_m: PX, elev_deg: DEFAULT_ELEV, dirs: 8, cell: [W, H], pivot: [cx, groundY] },
      build: Object.assign(dims(o), { type: s.type, shape: s.shape, size: s.size, door: s.door, mirror: s.mirror, dock: s.dock, preset: o.preset || null }),
      PLACEMENT: placement(o), FOOTPRINT: footprint(o), WALK: walk, BLOCKERS: g.solids0.map(q => Object.assign(M(q), { level: 0 })).concat(g.solids1.map(q => Object.assign(M(q), { level: 1 }))),
      DOORS: st.stations.filter(q => q.kind === 'door'), CLIMB: st.stations.filter(q => q.kind === 'ladder'), STAIRS: st.stations.filter(q => q.kind === 'stair'),
      STATIONS: st.stations.filter(q => q.kind === 'fixture' || q.kind === 'hoist'), APPROACH: st.approach,
      LIGHTS: { schedule: { name: o.schedule || s.sched, hours: SCHEDULES[o.schedule || s.sched] || [] }, lamps: g.lamps.map(q => ({ id: q.id, kind: q.kind, m: mxP(s, q.p).map(r3), pool: { m: mxP(s, q.pool).map(r3), r: q.r }, colour: q.c, burns: q.kind === 'pack' ? 'dusk to dawn' : 'from dusk while in use' })),
        windows: 'emitters win:<ground|loft|office>:n glow with the room while it is in use after dark', fire: s.stove ? { m: mxP(s, g.stove || [0, 0, 0]).map(r3), burns: 'while in use' } : null },
      SMOKE: g.stacks.map(p => mxP(s, p).map(r3)), SIGN: g.sign, CHAR: CHAR92, CHAR_REQUESTS: REQUESTS }; }

  const API = { W, H, PX, DIRS: 8, pivot: { x: cx, y: groundY }, defaultElev: DEFAULT_ELEV, order: NAMES, pass: 2, revision: '2.0',
    TYPES: G.TYPES, PRESETS: G.PRESETS, SHAPES: G.SHAPES, SIDINGS: G.SIDINGS, ROOFS: G.ROOF_KEYS, BODY: G.BODY, TRIM: G.TRIM, DOORS: G.DOORS, WINDOWS: G.WINDOWS, CUPOLAS: G.CUPOLAS, PLINTHS: G.PLINTHS,
    ROOF_RAMPS: G.ROOFS, RAMP: G.RAMP, CHAR: CHAR92, SCHEDULES, REQUESTS,
    resolve, dims, footprint, layout, placement, anchors, stations, stepPlan, lightsOn, lights, gameplay, checks, doorShare,
    frame, relight, castShadow, view, renderLive, render, project, cropOf: (o, elev) => cropOf(liveModel(o || {}, 0), elev), tightCell, liveModel };
  root.WharfBuilding2 = API;
})(typeof globalThis !== 'undefined' ? globalThis : window);
