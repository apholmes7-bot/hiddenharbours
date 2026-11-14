/* NINE MILE CREEK, KEY SCENES PART 2C (hh-nmc-key-scenes/fuel-stop/, 2026-09-30): the gas bar redrawn once for calls 7 and 10 (ruled
   09-30): it stands on part 1's plan texture (setGround), the brook across the top of its frame is gone, and the frame's top edge sits
   on the river valley's rim (uTop 69: Route 91 at the bridge's south abutment); the lane carries on to the boat shed, Mill Road leaves
   Route 91 at the frame's west edge, and part 1's still water draws the river in the top-right corner. Each change is marked 'part 2c'.
   Re-issued 10-01: part 1's truck park shows in the frame's bottom strip as a gravel pad, and the region view's helpers are exported
   (marked 'part 2c (10-01)'); the frame, the pieces and every other line stand as they were.
   The wharf return's header follows. */
/* Nine Mile Creek: the gas station (keyscene.nmc_fuel_stop), one scene for StpPano (globalThis.NmcFuelStop). A copy of St
   Peters pass 6's Bar Road Stop (pass6/fuelStop.js, untouched), re-sited where the owner placed it (the wharf pass brief, §6):
   the stop stands in the north-east corner of the junction where Wharf Road leaves Route 91, its store facing south on the
   north side of Wharf Road. Pass 6's paved road along the bottom of the frame is Wharf Road; Route 91, the region's through
   road, runs north-south up the west side; the water across the top is a stream running east to the barachois behind the
   wharf's spit (a proposal: the owner rules on it here), and the dirt road up the east side is a lane down to a pull-off at the
   stream (a proposal). The store takes the General Store's trade ("GROCERY  COFFEE  ICE"); the station's name is open, so its
   name boards draw blank (fuel-stop/fuelStopRig.js, BRAND).
   Units: site metres, x east and y north, pass 6's own origin kept so the rig's numbers carry over; region metres (the ground
   map's frame) = site + (-174, +87): Wharf Road's centre line (site y 5) is region y 92, Route 91's east shoulder (site x 0) is
   region x -174. z metres above mean sea level: the fields here stand at +6 (the map's top), so the pads are at +6.0.       */
(function (root) {
  'use strict';
  const D2R = Math.PI / 180, PPM = 32;
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v, lerp = (a, b, t) => a + (b - a) * t;
  const ss = (a, b, x) => { const t = clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };
  const hsh = (x, y, s) => { let h = Math.imul(x | 0, 374761393) + Math.imul(y | 0, 668265263) + Math.imul(s | 0, 2147483647); h = Math.imul(h ^ (h >>> 13), 1274126177); h ^= h >>> 16; return (h >>> 0) / 4294967296; };
  const vn = (x, y, s) => { const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
    const a = hsh(xi, yi, s), b = hsh(xi + 1, yi, s), c = hsh(xi, yi + 1, s), d = hsh(xi + 1, yi + 1, s); return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v; };
  const fb = (x, y, s) => 0.55 * vn(x, y, s) + 0.3 * vn(x * 2.1 + 5.3, y * 2.1 + 1.7, s + 1) + 0.15 * vn(x * 4.3 + 2.1, y * 4.3 + 7.9, s + 2);
  const pickW = (list, u) => { let s = 0; for (const q of list) s += q[1]; let a = u * s; for (const q of list) { a -= q[1]; if (a <= 0) return q[0]; } return list[list.length - 1][0]; };
  function nearSeg(Q, x, y) { let b = 1e9, bk = 0, bt = 0;
    for (let k = 0; k + 1 < Q.length; k++) { const ax = Q[k][0], ay = Q[k][1], vx = Q[k + 1][0] - ax, vy = Q[k + 1][1] - ay, L2 = vx * vx + vy * vy;
      let t = L2 ? ((x - ax) * vx + (y - ay) * vy) / L2 : 0; t = t < 0 ? 0 : t > 1 ? 1 : t; const d = Math.hypot(x - ax - vx * t, y - ay - vy * t); if (d < b) { b = d; bk = k; bt = t; } }
    return { d: b, k: bk, t: bt }; }
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];

  // ---- the site, its frame, the roads -----------------------------------------------------------------------------------------
  const OFFSET = [-174, 87], toRegion = (p) => [+(p[0] + OFFSET[0]).toFixed(3), +(p[1] + OFFSET[1]).toFixed(3)];
  const SITE = { name: 'Nine Mile Creek, the junction of Wharf Road and Route 91', origin: 'site metres (pass 6); region = site + (-174, +87)', junction: [-4, 5], junctionRegion: [-178, 92] };
  const FRAME = { x0: -12, x1: 64, uTop: 69, uBot: 0, padBelow: 8, yMin: -12, yMax: 112 };   // part 2c: the top edge on the river valley's rim (Route 91 at the bridge's south abutment, region y 187)
  let EG = null, SG = null, PLAN = null;   // part 2c: part 1's plan texture and still water, in site metres (setGround)
  const PAD_Z = 6.0, STORE = [19, 52], CANOPY = [20, 24], TRUCK = [47, 31], PYLON = [5.2, 11.6];
  const WR = { y: 5.0, hw: 2.9, sh: 0.55, ditch: 1.45, depth: 0.34 };                              // Wharf Road (pass 6's paved road)
  const R91 = { x: -3.45, hw: 2.9, sh: 0.55, ditch: 1.45, depth: 0.34 };                             // Route 91, north-south up the west side
  const LANE = { x: 57.5, y0: 9.9, y1: 58.0, hw: 2.5, sh: 0.45, ditch: 1.4, depth: 0.28, pull: [57.5, 61.2], pullR: 5.2 };   // the lane to the stream (proposal)
  const DRIVES = [[4.6, 35.4], [40.6, 53.4]];
  // part 2c (10-01): part 1's truck park (lot.nmc_truck_park, region (-131, 78), 20.1 x 13.4 m), south of Wharf Road in the frame's bottom strip;
  // no scene draws it yet, so it is a gravel pad, as the region view draws the lots no scene has drawn. setGround reads it from the plan.
  let TPARK = [32.95, 53.05, -15.7, -2.3];
  const tpD = (x, y) => Math.max(TPARK[0] - x, x - TPARK[1], TPARK[2] - y, y - TPARK[3]);
  const EXIT = [[47, 36], [48, 42], [51, 46.5], [55.0, 48.5]];
  const ANGLERS = [[59.4, 64.0], [60.6, 70.5], [61.8, 78.5], [62.6, 88], [63.2, 98]];   // part 2c: the anglers' path from the pull-off down the valley's side to the river
  // THE STREAM (proposal): its line, width, bed and banks as numbers. It runs east across the top of the frame to the barachois.
  const STREAM = { id: 'stream.nmc_junction_brook', pts: [[-22, 91], [-6, 90.2], [14, 88.6], [34, 86.4], [54, 83], [76, 76], [96, 67.4], [116, 59]],
    width: [5.0, 9.0], bed: [3.0, -0.6], bank: 2.0, depthM: [0.35, 0.6], riffles: [[8, 89.1], [30, 86.9], [49, 84.0]], lengthM: 0 };
  { let s = 0; for (let k = 1; k < STREAM.pts.length; k++) s += Math.hypot(STREAM.pts[k][0] - STREAM.pts[k - 1][0], STREAM.pts[k][1] - STREAM.pts[k - 1][1]); STREAM.lengthM = +s.toFixed(1); }
  const DRY_S = { d: 1e9, s: 0, f: 0, w: 0, bed: -9, wl: -9, rif: 0 };
  function streamAt() { return DRY_S; }   // part 2c: the brook is gone (call 10, ruled 09-30); the river valley's rim takes its place
  function streamAt0(x, y) { const n = nearSeg(STREAM.pts, x, y); let s = 0; for (let k = 0; k < n.k; k++) s += Math.hypot(STREAM.pts[k + 1][0] - STREAM.pts[k][0], STREAM.pts[k + 1][1] - STREAM.pts[k][1]);
    s += n.t * Math.hypot(STREAM.pts[n.k + 1][0] - STREAM.pts[n.k][0], STREAM.pts[n.k + 1][1] - STREAM.pts[n.k][1]); const f = clamp(s / STREAM.lengthM, 0, 1);
    const w = lerp(STREAM.width[0], STREAM.width[1], f) * (1 + 0.12 * (vn(s / 6, 3.3, 81) - 0.5)), bed = lerp(STREAM.bed[0], STREAM.bed[1], f) - 0.06 * Math.sin(s / 3.1);
    let rif = 0; for (const r of STREAM.riffles) rif = Math.max(rif, 1 - Math.hypot(x - r[0], y - r[1]) / 3.2);
    const dep = lerp(STREAM.depthM[0], STREAM.depthM[1], f) * (1 - 0.55 * rif), wl = bed + dep; return { d: n.d, s, f, w, bed, wl, rif }; }
  const PADS = [[5, 35, 7.9, 33], [41, 53, 7.9, 38], [35.5, 40.5, 10.5, 19], [2.5, 53.2, 33, 44.2], [30.45, 35.95, 44, 58.5], [7.2, 30.8, 44.1, 46.2], [10, 28, 46, 58]];
  function paveD(x, y) { let d = 9; for (const [a, b, c, e] of PADS) { const q = Math.max(a - x, x - b, c - y, y - e); if (q < d) d = q; }
    const ex = nearSeg(EXIT, x, y).d - 3.75; if (ex < d) d = ex; const tr = Math.hypot(x - LANE.pull[0], y - LANE.pull[1]) - LANE.pullR; if (tr < d) d = tr; return d; }

  // ---- the ground: the fields at +6, the site level on them, the stream's gully across the top ----------------------------------------
  function fields(x, y) { return EG ? EG(x, y) : PAD_Z + 0.22 * (fb(x / 13, y / 13, 3) - 0.5) - 0.2 * ss(0, 1, (y - 96) / 30); }   // part 2c: part 1's plan texture
  function gully(x, y, e) { const S = streamAt(x, y), hw = S.w / 2, over = S.d - hw; if (over > (e - S.bed) * STREAM.bank + 2) return e;
    if (over <= 0) { const q = S.d / hw; return S.bed + 0.18 * q * q + 0.05 * (vn(x * 1.3, y * 1.3, 83) - 0.5); }
    return Math.min(e, S.bed + 0.18 + over / STREAM.bank + 0.12 * (fb(x / 3, y / 3, 85) - 0.5)); }
  const siteK = (x, y) => ss(-3, 2.5, Math.min(x - 0.2, 63.5 - x, y + 12, 71 - y));
  function ditchDip(x, y) { let dip = 0;
    const ay = Math.abs(y - WR.y), a0 = WR.hw + WR.sh;
    if (ay > a0 && ay < a0 + WR.ditch && x > R91.x + R91.hw + 1) { const u = (ay - a0) / WR.ditch; let k = 1; if (y > WR.y) for (const [a, b] of DRIVES) k = Math.min(k, 1 - ss(a - 1.2, a, x) * ss(b + 1.2, b, x));
      if (x > LANE.x - 4.2 && x < LANE.x + 4.2 && y > WR.y) k *= 1 - ss(LANE.x - 4.2, LANE.x - 3, x) * ss(LANE.x + 4.2, LANE.x + 3, x);
      dip = Math.max(dip, WR.depth * Math.pow(Math.sin(Math.PI * u), 0.85) * k); }
    const ax = Math.abs(x - R91.x), b0 = R91.hw + R91.sh;
    if (ax > b0 && ax < b0 + R91.ditch && Math.abs(y - WR.y) > WR.hw + 2.5) { const u = (ax - b0) / R91.ditch; dip = Math.max(dip, R91.depth * Math.pow(Math.sin(Math.PI * u), 0.85) * ss(80, 76, y)); }
    if (y > LANE.y0 && y < LANE.y1) { const bx = Math.abs(x - LANE.x), c0 = LANE.hw + LANE.sh;
      if (bx > c0 && bx < c0 + LANE.ditch) { const u = (bx - c0) / LANE.ditch; let k = ss(LANE.y0, LANE.y0 + 1.5, y) * ss(LANE.y1, LANE.y1 - 3, y); if (x < LANE.x) k *= 1 - ss(43.8, 45.2, y) * ss(53.2, 51.8, y);
        dip = Math.max(dip, LANE.depth * Math.pow(Math.sin(Math.PI * u), 0.85) * k); } }
    return dip; }
  function groundAt(x, y) { const f0 = fields(x, y), k = siteK(x, y) * ss(4.8, 5.5, f0); return lerp(f0, PAD_Z, k) - ditchDip(x, y); }   // part 2c: level at +6.0 on the fields; the valley's side keeps part 1's slope
  /* the soft bands of the roads: 1 carriage, 2 shoulder, 3 ditch bottom, 4 ditch side, 5 verge, 0 off the road */
  function roadSoft(x, y) {
    const ay = Math.abs(y - WR.y); if (x > R91.x - R91.hw - 1) { if (ay <= WR.hw) return 1; if (ay < WR.hw + WR.sh) return 2;
      if (ay < WR.hw + WR.sh + WR.ditch && ditchDip(x, y) > 0.03) { const u = (ay - WR.hw - WR.sh) / WR.ditch; return Math.abs(u - 0.5) < 0.16 + 0.05 * (fb(x / 6, y / 6, 5) - 0.5) ? 3 : 4; }
      if (ay < WR.hw + WR.sh + WR.ditch + 0.8) return 5; }
    const ax = Math.abs(x - R91.x); if (ax <= R91.hw) return 1; if (ax < R91.hw + R91.sh) return 2;
    if (ax < R91.hw + R91.sh + R91.ditch && ditchDip(x, y) > 0.03) return 4; if (ax < R91.hw + R91.sh + R91.ditch + 0.8) return 5;
    if (y > WR.y + WR.hw && y < LANE.y1 + 3) { const bx = Math.abs(x - LANE.x); if (bx <= LANE.hw) return 1; if (bx < LANE.hw + LANE.sh) return 2;
      if (bx < LANE.hw + LANE.sh + LANE.ditch && ditchDip(x, y) > 0.03) { const u = (bx - LANE.hw - LANE.sh) / LANE.ditch; return Math.abs(u - 0.5) < 0.16 + 0.05 * (fb(x / 6, y / 6, 6) - 0.5) ? 3 : 4; }
      if (bx < LANE.hw + LANE.sh + LANE.ditch + 0.8) return 5; }
    return 0; }
  const woods = (x, y) => Math.max(ss(-7.4, -9.6, x + 1.2 * (vn(0.5, y / 6, 71) - 0.5)) * ss(12, 16, Math.abs(y - WR.y)), ss(62.4, 64.2, x - 1.2 * (vn(1.5, y / 6, 73) - 0.5)) * ss(12, 15, y) * ss(72, 64, y),
    ss(59, 62.5, y + 1.5 * (vn(x / 6, 2.5, 75) - 0.5)) * ss(74, 70, y + 1.5 * (vn(x / 7, 4.5, 77) - 0.5)) * ss(0.9, 0.2, gullyK(x, y)));   // part 2c: a 12 m wood behind the store, then the fields to the rim
  const gullyK = (x, y) => clamp((PAD_Z - 0.35 - fields(x, y)) / 1.6, 0, 1);   // part 2c: 1 down the river valley's side, 0 on the fields (was the brook's gully)

  const BUILT = [];
  function builtD(x, y) { let d = 9; for (const b of BUILT) { const q = Math.max(b[0] - x, x - b[1], b[2] - y, y - b[3]); if (q < d) d = q; } return d; }
  function sample(x, y, out) { out[0] = groundAt(x, y); out[1] = builtD(x, y); }

  // ---- zones ------------------------------------------------------------------------------------------------------------------
  const ZONES = ['grass', 'dirt', 'path', 'bank', 'shingle', 'sedge', 'foreshore', 'mud', 'ripple', 'rockweed', 'sand', 'eelgrass'];
  const ZI = {}; ZONES.forEach((k, i) => { ZI[k] = i; });
  function zone(c) {
    const x = c.wx, y = c.wy, e = c.e, nz = vn(x * 2.3, y * 2.3, 43), rs = roadSoft(x, y), pd = paveD(x, y);
    if (rs === 1 || pd < 0.05) return ZI.dirt;
    if (rs === 2) return ZI.path; if (rs === 3) return ZI.sedge;
    if (tpD(x, y) < 0.18 * (nz - 0.5)) return ZI.path;   // part 2c (10-01): the truck park's gravel
    if (pd < 0.55 + 0.35 * (nz - 0.5) && e > PAD_Z - 0.4) return ZI.path;
    { const sw = SG ? SG(x, y) : null; if (sw != null && sw > e + 0.02) return ZI.mud; if (e < 2.45) return vn(x / 2.2, y / 1.8, 89) > 0.5 ? ZI.sedge : ZI.mud; }   // part 2c: the valley's floor (part 1's still water, its mud)
    const S = streamAt(x, y), over = S.d - S.w / 2;
    if (over < 0) return S.rif > 0.2 || vn(x / 1.6, y / 1.6, 87) > 0.55 ? ZI.shingle : ZI.sand;                                   // the bed: gravel, sand in the pools
    if (over < 0.9 + 0.5 * nz) return ZI.mud;                                                                                    // the wet margin
    if (gullyK(x, y) > 0.05 && e < PAD_Z - 0.35) return vn(x / 2.6, y / 2.2, 65) > 0.55 ? ZI.bank : ZI.grass;                  // the gully's sides
    if (y > LANE.pull[1] + 1 && nearSeg(ANGLERS, x, y).d < 0.55 + 0.1 * nz) return ZI.path;   // part 2c: the anglers' path to the river   // the anglers' path down
    if (woods(x, y) > 0.6 && vn(x / 3.3, y / 3.3, 67) > 0.64) return ZI.dirt;
    return ZI.grass;
  }

  // ---- the roads, the pads and the lot: RoadKit4 routes (site metres), each group its own network, painted after the floor ---------
  const RT = (id, cls, w, pts, o) => Object.assign({ id, cls, w, pts }, o || {});
  const SLAB = { surf: 'slab', bands: [], ends: 'flat', marks: {}, wear: 0.14 }, ASPH = { surf: 'asphalt', bands: [], ends: 'flat', marks: {}, wear: 0.3 };
  const NETS = [
    { id: 'lane', box: [52.0, 80.0, 6.0, 72.0], routes: [RT('road.nmc.stream_lane', 'street', 5.0, [[57.5, 7.0], [57.5, 52], [57.5, 58.5, 5.0], [57.5, 61.2, 10.4], [61.0, 64.6, 5.0], [70.0, 67.2], [78.0, 69.2]], { bands: [{ role: 'shoulder', w: 0.45 }], wear: 0.55 })] },   // part 2c: carried on to the boat shed
    { id: 'mill_road', box: [-14, 2, 66, 77], policy: 'road', routes: [RT('road.nmc.mill_road', 'street', 5.0, [[-3.3, 71.0], [-16.0, 71.92]], { bands: [{ role: 'shoulder', w: 0.45 }], wear: 0.5 })] },   // part 2c: part 1's Mill Road leaves Route 91 here
    { id: 'lot', box: [1.5, 54.5, 32.0, 59.5], routes: [RT('lot.front', 'town', 11.2, [[2.5, 38.6], [53.2, 38.6]], ASPH), RT('lot.service', 'town', 5.5, [[33.2, 44.1], [33.2, 58.5]], ASPH)] },
    { id: 'pads', box: [3.5, 57.0, 7.5, 53.5], routes: [8, 14, 20, 26, 32].map((x, k) => RT('pad.car_canopy_' + (k + 1), 'town', 6, [[x, 7.95], [x, 33]], SLAB))
      .concat([RT('pad.tanks', 'town', 5, [[38, 10.5], [38, 19]], SLAB), RT('pad.semi_lane_w', 'town', 6, [[44, 7.95], [44, 38.2]], Object.assign({}, SLAB, { wear: 0.22 })),
        RT('pad.semi_lane_e', 'town', 6, [[50, 7.95], [50, 38.2]], Object.assign({}, SLAB, { wear: 0.22 })), RT('pad.semi_exit', 'town', 7.5, EXIT, Object.assign({}, SLAB, { wear: 0.22 }))]) },
    { id: 'walk', box: [6.5, 31.5, 43.5, 47.0], routes: [RT('walk.store', 'town', 2.1, [[7.2, 45.15], [30.8, 45.15]], { surf: 'slab', lift: 0.125, bands: [{ role: 'kerb', w: 0.16, z: 0.14 }], ends: 'flat', marks: {}, wear: 0.1 })] },
    { id: 'wharf_road', box: [-8, 64, 0.5, 9.5], policy: 'road', routes: [RT('road.nmc.wharf_road', 'route', 5.8, [[-4.0, 5.0], [70, 5.0]], { bands: [{ role: 'shoulder', w: 0.55 }], wear: 0.35 })] },
    { id: 'route_91', box: [-12, 2, -14, 101], policy: 'road', routes: [RT('road.nmc.route_91', 'route', 5.8, [[-3.55, -14], [-3.45, 5.0], [-3.1, 60], [-2.85, 100.2]], { bands: [{ role: 'shoulder', w: 0.55 }], wear: 0.4 })] },   // part 2c: to the bridge's south abutment
  ];
  const STALLS = []; for (let k = 0; k <= 8; k++) STALLS.push(8.6 + k * 2.6);
  const FILLS = [['reg', 36.6], ['sup', 37.6], ['dsl', 38.6], ['kero', 39.6]], FILLCOL = { reg: '#b02b2c', sup: '#335d84', dsl: '#c2801f', kero: '#bcbfaf' };
  async function afterPaint(P) {
    const RK = root.RoadKit4, K8 = root.PxKit8; P.roadLog = [];
    if (!RK || !K8) { (P.errors || (P.errors = [])).push('NmcFuelStop: RoadKit4 is not loaded, the pads are bare'); return; }
    const t = P.tile, W = P.W, PH = P.PH, E = P.E, E0 = Float32Array.from(E), paved = P._paved = new Uint8Array(P.N), cache = new Map(), live = t.live || (t.live = []);
    const addPal = (base, c) => { const k = base + '|' + c; let p = cache.get(k); if (p == null) { p = t.addPal(base, c); cache.set(k, p); } return p; };
    const pal = (base, c, cls) => { const p = addPal(base, c); live[p] = 1; if (root.TerrainLight5 && root.TerrainLight5.CLASS_OF) { const q = t.pals[p][2]; root.TerrainLight5.CLASS_OF.set('#' + q.map(v => v.toString(16).padStart(2, '0')).join(''), cls); } return p; };
    let mk = 0;
    const TF = P.def.frame || FRAME;
    for (const spec of NETS) {
      if (spec.box[1] < TF.x0 - 6 || spec.box[0] > TF.x1 + 6 || (TF.yMin != null && spec.box[3] < TF.yMin) || (TF.yMax != null && spec.box[2] > TF.yMax)) { P.roadLog.push(spec.id + ' outside this tile'); continue; }   // part 2c: a tile paints only the nets that reach it
      const t0 = Date.now(), [bx0, bx1, by0, by1] = spec.box, gw = Math.ceil((bx1 - bx0) * PPM), gh = Math.ceil((by1 - by0) * PPM), n = gw * gh;
      const routes = spec.routes.map(r => Object.assign({}, r, { pts: r.pts.map(p => [(p[0] - bx0) * PPM, (by1 - p[1]) * PPM].concat(p.length > 2 ? [p[2]] : [])) }));
      const net = RK.network(routes, { w: gw, h: gh, seed: (P.def.seed | 0) * 31 + spec.id.length * 7 });
      for (const g of net.groups) g.F1 = g.F2 = g.R1 = g.R2 = g.D = g.S = g.TX = g.TY = g.E = null;
      const pt = { n: gw, m: gh, pal: new Uint8Array(n), band: new Int8Array(n).fill(2), h: new Float32Array(n), mark: new Uint16Array(n), lock: new Uint8Array(n), a: new Uint8Array(n).fill(255),
        pals: t.pals, live, mid: 0, markSeq: 0, addPal, addRamp: (hx) => t.addRamp(hx), beginMark() { this.mid = (this.markSeq = (this.markSeq + 1) & 0xffff) || 1; return this.mid; }, endMark() { this.mid = 0; },
        i(x, y) { return y * gw + x; }, set(x, y, p, b, h, l) { const i = y * gw + x; this.pal[i] = p; this.band[i] = b; if (h != null) this.h[i] = h; this.mark[i] = this.mid; if (l) this.lock[i] = 1; } };
      RK.paint(pt, net);
      const c0 = clamp(P.col(bx0), 0, W - 1), c1 = clamp(P.col(bx1), 0, W - 1), r0 = clamp(P.row(by1), 0, PH - 1), r1 = clamp(P.row(by0), 0, PH - 1); let cnt = 0;
      for (let r = r0; r <= r1; r++) { const Y = Math.floor((by1 - P.wy(r)) * PPM); if (Y < 0 || Y >= gh) continue;
        for (let c = c0; c <= c1; c++) { const X = Math.floor((P.wx(c) - bx0) * PPM); if (X < 0 || X >= gw) continue; const j = Y * gw + X, role = net.role[j]; if (!role || !pt.lock[j]) continue;
          const i = r * W + c; if (spec.policy === 'road' && role !== 1 && paved[i]) continue;
          t.pal[i] = pt.pal[j]; t.band[i] = pt.band[j]; t.h[i] = pt.h[j]; t.mark[i] = pt.mark[j] ? 20000 + ((pt.mark[j] + mk) % 45000) : 0; t.lock[i] = 1;
          E[i] = E0[i] + net.zr[j] + net.zg[j]; paved[i] = role === 1 ? 1 : 2; cnt++; } }
      mk += 7919; P.roadLog.push(spec.id + ' ' + gw + 'x' + gh + ' ' + cnt + ' texels ' + (Date.now() - t0) + ' ms');
      await new Promise(r => setTimeout(r, 0)); }
    const PW = RK.PALS.paintW, white = pal(PW[0], PW[1], PW[2]), tar = pal(RK.PALS.tar[0], RK.PALS.tar[1], RK.PALS.tar[2]);
    const put = (i, p, b, h, m) => { t.pal[i] = p; t.band[i] = b; t.h[i] = h; t.mark[i] = m; t.lock[i] = 1; };
    STALLS.forEach((sx, k) => { const c = P.col(sx), r0 = P.row(44.0), r1 = P.row(38.9); for (let r = r0; r <= r1; r++) for (const cc of [c - 1, c]) { const i = r * W + cc; if (paved[i] === 1) put(i, white, hsh(cc, r, 3) < 0.06 ? 3 : 2, 0.06, 18000 + k); } });
    FILLS.forEach(([g, fx], k) => { const col = pal(FILLCOL[g], 0.6, root.TerrainLight5 && root.TerrainLight5.CL ? root.TerrainLight5.CL.ROCK : 0), fy = 13.4;
      for (let r = P.row(fy + 0.4); r <= P.row(fy - 0.4); r++) for (let c = P.col(fx - 0.4); c <= P.col(fx + 0.4); c++) { const d = Math.hypot(P.wx(c) - fx, P.wy(r) - fy), i = r * W + c; if (d > 0.36) continue; put(i, d > 0.27 ? tar : col, d > 0.3 ? 1 : 2, d > 0.27 ? 0.05 : 0.02, 18100 + k); } });
  }

  // ---- habitats and the scatter ------------------------------------------------------------------------------------------------
  function habitat(c) {
    const P = c.pano, i = c.i, x = c.x, y = c.y, z = c.zone, e = c.e;
    if (P._paved && P._paved[i]) return null; if (P.CH[0][i] < 0.6) return null;
    { const sw = SG ? SG(x, y) : null; if (sw != null && sw > e + 0.03) return null; }   // part 2c: no herbs under the river's still water
    if (z === 'mud') return 'streamside'; if (z === 'bank') return 'bankFace';
    if (z === 'sedge') return 'ditch';
    if (z === 'path' || z === 'shingle' || z === 'sand' || z === 'dirt') return null;
    const rs = roadSoft(x, y); if (rs === 4) return 'ditchSide'; if (rs === 5) return 'verge';
    if (gullyK(x, y) > 0.05) return 'gully';
    if (woods(x, y) > 0.5) return 'woods';
    if (x > 1.5 && x < 9.6 && y > 46.5 && y < 60) return 'lawn';
    if (paveD(x, y) < 1.4) return 'verge';
    return 'meadow';
  }
  const HERBS = {
    meadow: [['MeadowGrass', 26], ['Timothy', 16], ['Lupin', 12], ['Goldenrod', 8], ['OxeyeDaisy', 9], ['QueenAnne', 7], ['Buttercup', 7], ['Fireweed', 4], ['WildRose', 1.5], ['Raspberry', 1]],
    verge: [['MeadowGrass', 30], ['Timothy', 18], ['Buttercup', 8], ['OxeyeDaisy', 8], ['Lupin', 6], ['QueenAnne', 4]],
    lawn: [['MeadowGrass', 14], ['Buttercup', 4], ['OxeyeDaisy', 2]],
    ditch: [['TussockSedge', 16], ['SoftRush', 14], ['BlueFlag', 10], ['Cattail', 6], ['MeadowGrass', 6]],
    ditchSide: [['MeadowGrass', 18], ['Timothy', 8], ['Goldenrod', 5], ['Lupin', 5], ['SoftRush', 3]],
    streamside: [['SoftRush', 16], ['TussockSedge', 14], ['BlueFlag', 10], ['Cattail', 4], ['OstrichFern', 6], ['MeadowGrass', 6]],
    gully: [['MeadowGrass', 16], ['OstrichFern', 8], ['Goldenrod', 6], ['Raspberry', 5], ['WildRaisin', 3], ['Fireweed', 4], ['SoftRush', 3]],
    woods: [['SweetFern', 5], ['BeakedHazelnut', 5], ['WildRaisin', 4], ['Serviceberry', 2], ['LowbushBlueberry', 6], ['SheepLaurel', 2], ['MeadowGrass', 4], ['Raspberry', 3]],
    bankFace: [['MeadowGrass', 16], ['Raspberry', 6], ['WildRose', 6], ['Goldenrod', 6], ['OstrichFern', 5], ['SoftRush', 3]],
  };
  const DENS = { meadow: 0.42, verge: 0.3, lawn: 0.06, ditch: 0.5, ditchSide: 0.34, streamside: 0.5, gully: 0.4, woods: 0.14, bankFace: 0.34 };
  const TALL = { Serviceberry: 0.95, BeakedHazelnut: 0.9, WildRaisin: 0.85, Bayberry: 0.7, Cattail: 0.45, OstrichFern: 0.5 }, MAT = { LowbushBlueberry: 0.55, CommonJuniper: 0.6, BeachPea: 0.4 };
  const SHRUB = new Set(['LowbushBlueberry', 'SheepLaurel', 'CommonJuniper', 'Raspberry', 'WildRose', 'Serviceberry', 'BeakedHazelnut', 'WildRaisin', 'Bayberry', 'SweetFern']);
  const FLOWER = new Set(['Lupin', 'Fireweed', 'QueenAnne', 'Goldenrod', 'OxeyeDaisy', 'BlueFlag', 'Buttercup', 'BeachPea']);
  const plant = (key, x, y, rnd, o) => { const stage = (o && o.stage) || (rnd() < 0.3 ? 'young' : 'mature'), variant = Math.floor(rnd() * 4) & 1;   // part 2c: two variants a species (half the frames a tile bakes)
    return { id: 'plant.' + key, kind: 'plant', key, stage, variant, at: [x, y], rad: TALL[key] || MAT[key] || (SHRUB.has(key) ? 0.62 : 0.3), cat: SHRUB.has(key) ? 'shrub' : 'herb',
      family: SHRUB.has(key) ? 'shrub' : key === 'OstrichFern' ? 'fern' : FLOWER.has(key) ? 'flower' : 'grass', waterDep: false, fkey: key + ':' + stage + ':' + variant, occM: 3 }; };
  function herbPick(c) { const h = habitat(c); if (!h) return null; let d = DENS[h], list = HERBS[h]; const x = c.x, y = c.y;
    if (h === 'meadow' || h === 'verge' || h === 'bankFace') { const Lu = vn(x / 6.5, y / 5, 101), Go = vn(x / 5.5, y / 4.5, 103);
      list = list.map(([k, w]) => [k, w * (k === 'Lupin' ? (Lu > 0.58 ? 7 : Lu < 0.4 ? 0.15 : 1) : k === 'Goldenrod' ? (Go > 0.62 ? 4 : 0.6) : 1)]); }
    if (c.rnd() > d) return null; const k = pickW(list, c.rnd()); if (root.PlantRig5 && root.PlantRig5.SPECIES && !root.PlantRig5.SPECIES[k] && root.PlantRig5.has && !root.PlantRig5.has(k)) return null; return plant(k, x, y, c.rnd); }
  const TUFTS = [['MeadowGrass', 6], ['Timothy', 2.5], ['OxeyeDaisy', 1], ['Buttercup', 1]], TUFT_H = new Set(['meadow', 'verge', 'ditchSide', 'bankFace', 'gully']);
  function tuftPick(c) { const h = habitat(c); if (!TUFT_H.has(h) || c.rnd() > 0.28) return null; const p = plant(pickW(TUFTS, c.rnd()), c.x, c.y, c.rnd); p.rad = 0.22; return p; }
  const TRAD = { mature: 1.75, pole: 1.3, young: 0.95, sapling: 0.6 };
  const tree = (key, stage, x, y, variant) => ({ id: 'tree.' + key, kind: 'tree', family: 'tree', cat: 'tree', key, stage, variant: variant | 0, at: [x, y], rad: TRAD[stage], fkey: key + ':' + stage + ':' + (variant | 0) });
  const WOODS = [['BalsamFir', 30], ['RedSpruce', 28], ['WhiteBirch', 16], ['RedMaple', 12], ['TremblingAspen', 7], ['WhitePine', 5], ['BlackSpruce', 4]], ALDERS = [['WhiteBirch', 30], ['RedMaple', 24], ['BalsamFir', 20], ['TremblingAspen', 10]];
  function treePick(c) { const x = c.x, y = c.y, e = c.e, r = c.rnd, P = c.pano, i = c.i;
    if ((P._paved && P._paved[i]) || P.CH[0][i] < 2.4 || paveD(x, y) < 2.2 || roadSoft(x, y)) return null;
    if (e < 3.2) return null;   // part 2c: no trees on the river's benches and mud
    const gk = gullyK(x, y), w = Math.max(woods(x, y), gk > 0.15 && gk < 0.95 ? 0.55 : 0); if (w < 0.3 || r() > 0.8 * w) return null;
    const u = r(), stage = w > 0.8 ? (u < 0.32 ? 'mature' : u < 0.72 ? 'pole' : u < 0.92 ? 'young' : 'sapling') : (u < 0.15 ? 'pole' : u < 0.6 ? 'young' : 'sapling');
    return tree(pickW(gk > 0.15 ? ALDERS : WOODS, r()), stage, x, y, r() < 0.5 ? 0 : 1); }
  const rock = (form, x, y, o) => { o = o || {}; const variant = o.variant | 0, mirror = !!o.mirror, stone = o.stone || 'sandstone', tide = o.tide || 'dry', dress = o.dress | 0, seat = o.seat || 'grass';
    return { id: 'rock.' + form, kind: 'rock', family: 'rock', cat: 'rock', form, variant, mirror, stone, tide, dress, seat, at: [x, y], rad: form === 'shelf' ? 1.5 : form === 'cobbles' ? 0.8 : 1.05, fkey: [form, variant, mirror ? 1 : 0, stone, tide, dress, seat].join(':') }; };
  function rockPick(c) { const P = c.pano, r = c.rnd, z = c.zone, i = c.i; if ((P._paved && P._paved[i]) || P.CH[0][i] < 1.0) return null;
    let form, p = 0; const S = streamAt(c.x, c.y);
    if (z === 'shingle') { p = 0.05 + 0.08 * S.rif; form = pickW([['cobbles', 60], ['knuckle', 20], ['slab', 20]], r()); }
    else if (z === 'bank') { p = 0.025; form = pickW([['cobbles', 40], ['block', 35], ['slab', 25]], r()); }
    else if (z === 'mud') { p = 0.02; form = 'cobbles'; }
    else if (woods(c.x, c.y) > 0.7) { p = 0.006; form = pickW([['erratic', 60], ['block', 25], ['perched', 15]], r()); }
    if (!form || r() > p) return null;
    const wet = S.d < S.w / 2 + 0.4, seat = root.PxRockIso2 && root.PxRockIso2.SEATS && root.PxRockIso2.SEATS.indexOf(z) >= 0 ? z : 'grass';
    return rock(form, c.x, c.y, { variant: Math.floor(r() * 4), mirror: r() < 0.5, tide: wet ? 'wet' : 'dry', dress: wet ? 1 : 0, seat }); }
  const SPACING = (a, b) => { if (a === 'piece' || b === 'piece') return 1; if (a === 'tree' && b === 'tree') return 1; if (a === 'tree' || b === 'tree') return 0.3;
    if (a === 'rock' && b === 'rock') return 0.85; if (a === 'rock' || b === 'rock') return 0.55; if (a === 'shrub' && b === 'shrub') return 0.85; if (a === 'shrub' || b === 'shrub') return 0.6; return 0.72; };

  // ---- the pieces (pass 6's, less the bar road's sign; the lane gets a sign-less gate post at its pull-off) ------------------------
  const PIECES = [
    ['store', 'store', STORE, { sb: 13, occM: 150 }], ['canopy', 'canopy', CANOPY, { sb: 17, occM: 100 }], ['semi_lane', 'truck', TRUCK, { sb: 11, occM: 110 }],
    ['pylon', 'pylon', PYLON, { sb: 6, occM: 24 }],
    ['propane', 'propane', [8.4, 45.3], { sb: 2.5 }], ['ice', 'iceBox', [10.4, 45.35], { sb: 2.5 }], ['bench', 'bench', [13.0, 45.5], { sb: 2 }],
    ['bin', 'bin', [22.3, 45.55], { sb: 1.5 }], ['firewood', 'firewood', [28.8, 45.3], { sb: 2.5 }],
    ['air', 'air', [31.2, 46.7], { sb: 2 }], ['vacuum', 'vacuum', [31.0, 50.6], { sb: 3 }], ['dumpster', 'dumpster', [33.2, 56.9], { sb: 3.5 }],
    ['picnic', 'picnic', [5.0, 50.5], { sb: 2 }], ['stop', 'stopSign', [54.4, 51.4], { sb: 3 }], ['stop_91', 'stopSign', [1.4, 8.7], { sb: 3 }],
    ['light_w', 'lampPole', [3.6, 31.2], { sb: 8 }], ['light_mid', 'lampPole', [38.0, 31.6], { sb: 8 }], ['light_e', 'lampPole', [54.1, 42.4], { sb: 8 }],
    ['picnic_stream', 'picnic', [61.4, 63.6], { sb: 2 }], ['bin_stream', 'bin', [53.6, 62.4], { sb: 1.5 }],
  ];
  for (let k = 0; k < 8; k++) PIECES.push(['wheel_stop_' + (k + 1), 'wheelStop', [9.9 + k * 2.6, 43.35], { sb: 1 }]);
  (function () { const F = root.FuelStop; for (const [, piece, at] of PIECES) { const f = F ? F.footprint(piece) : [-1, 1, -1, 1]; BUILT.push([at[0] + f[0], at[0] + f[1], at[1] + f[2], at[1] + f[3]]); }
    BUILT.push([43.3, 50.7, 25.6, 36.4]); })();
  function pieces(P) {
    const out = [], g = (x, y) => { const i = P.texOfW(x, y); return i >= 0 ? P.E[i] : groundAt(x, y); };
    for (const [id, piece, at, o] of PIECES) { const opts = o.opts || {};
      out.push({ id: 'fs.' + id, kind: 'fuel', piece, at: at.slice(), z: g(at[0], at[1]), dir: 4, opts, family: piece === 'store' ? 'building' : 'station', cat: 'piece',
        shadowBox: o.sb || 3, clear: piece === 'store' || piece === 'canopy' || piece === 'truck' ? 0 : 0.6, occM: o.occM != null ? o.occM : 14,
        fkey: 'fuel:' + piece + ':' + JSON.stringify(opts) + ':4' }); }
    const GL = (id, x, y, dz, dir) => out.push({ id: 'gull.fs_' + id, kind: 'decor', kit: 'seagull', piece: 'perch', at: [x, y], z: g(x, y) + dz, dir, opts: { anim: 'perch', frame: 0 }, family: 'gull', cat: 'piece', clear: 0.3, fkey: 'gull:' + dir });
    GL('canopy', 27.4, 26.6, 6.16, 5); GL('parapet', 12.6, 46.5, 4.68, 3);
    const TF = P.def.frame || FRAME;   // part 2c: a tile frames only the pieces that can reach it (the margins hold the store, the canopy and the truck whole)
    return out.filter(s => { const ug = 0.643 * s.at[1] + 0.766 * s.z; return s.at[0] > TF.x0 - 14 && s.at[0] < TF.x1 + 14 && ug > TF.uBot - 12 && ug < TF.uTop + 7; });
  }
  function fixedFlora() {
    const T = (key, stage, x, y, v) => tree(key, stage, x, y, v), S = (key, x, y, stage) => { const p = plant(key, x, y, () => 0.5, { stage: stage || 'mature' }); p.variant = 1; p.fkey = key + ':' + p.stage + ':1'; return p; };
    return [T('WhiteBirch', 'pole', 2.6, 55.8, 0), T('WhiteBirch', 'young', 7.8, 60.4, 1), T('RedMaple', 'young', 36.8, 47.5, 0), T('BalsamFir', 'young', 39.2, 55.4, 1),
      S('Lupin', 36.4, 22.8), S('Lupin', 37.1, 23.9), S('Lupin', 39.3, 22.6), S('Lupin', 11.2, 1.4), S('Lupin', 26.8, 1.2), S('WildRose', 40.0, 25.2), S('Goldenrod', 35.8, 24.6)]; }

  // ---- the stream's water (a proposal, drawn over the bed after the sprites: its body by depth, the sky in it, the riffles' white)
  //      and the lights: pass 6's pools and the neon's bloom, following FuelStop.levels ------------------------------------------------
  const L = (x, y, r, I, c) => ({ x, y, r, I, c: h2r(c) }), POOLS = [];
  for (const e of [-10.6, -5.7, -2.3, 2.3, 5.7, 10.6]) for (const n of [-2.0, 1.8]) POOLS.push(L(CANOPY[0] + e, CANOPY[1] + n, 5.8, 0.5, '#dfe8ff'));
  for (const e of [-1.2, 1.2]) for (const n of [-2.6, 0.6, 3.4]) POOLS.push(L(TRUCK[0] + e, TRUCK[1] + n, 6.2, 0.55, '#dfe8ff'));
  for (let x = 9; x <= 31; x += 4.4) POOLS.push(L(x, CANOPY[1] - 4.4, 4.6, 0.2, '#ff3348')); for (const e of [-3, 0, 3]) POOLS.push(L(TRUCK[0] + e, TRUCK[1] - 5.0, 4.2, 0.2, '#2ee6d6'));
  for (let x = 12; x <= 26.5; x += 3.6) POOLS.push(L(x, 44.6, 5.0, 0.38, '#ffd9a0')); POOLS.push(L(STORE[0], 44.6, 3.8, 0.45, '#fff4e2'));
  for (const [x, y] of [[3.6, 31.2], [38.0, 31.6], [54.1, 42.4]]) POOLS.push(L(x, y - 1.4, 10.0, 0.5, '#e9efff'));
  POOLS.push(L(PYLON[0], PYLON[1] - 0.8, 4.2, 0.28, '#ff6a78'));
  function water(P, out, map, sky) {
    const W = P.W, H = P.H, sc = h2r(sky.skyC || '#b4cedf'), ac = h2r(sky.ac || '#1d3b4a'), day = clamp((sky.sunI || 0) * 1.4 + 0.25, 0.2, 1);
    for (let j = 0; j < W * H; j++) { const p = map[j]; if (p < 0) continue; const q = j * 4, a = p * 4; if (out[q] !== P.lit[a] || out[q + 1] !== P.lit[a + 1] || out[q + 2] !== P.lit[a + 2]) continue;
      const wx = P.wx(p % W), wy = P.wy((p / W) | 0); if (wy < 58 || wy > 98) continue; const S = streamAt(wx, wy); if (S.d > S.w / 2 + 0.2) continue;
      const dep = S.wl - P.E[p]; if (dep <= 0.01) continue; const k = clamp(dep / 0.55, 0, 1), rip = vn(wx * 2.4 - S.s * 0.7, wy * 3.6, 91), glint = rip > 0.82 ? 0.35 : 0;
      const body = [lerp(out[q], ac[0], 0.25 + 0.45 * k), lerp(out[q + 1], ac[1], 0.25 + 0.45 * k), lerp(out[q + 2], ac[2] + 18, 0.3 + 0.45 * k)];
      const sk = 0.18 + 0.2 * (1 - k) + glint * day, foam = S.rif > 0.25 && rip > 0.55 ? 0.35 * S.rif : 0;
      for (let c = 0; c < 3; c++) out[q + c] = clamp(lerp(lerp(body[c], sc[c], sk), 236, foam), 0, 255); } }
  function post(P, out, map, T, sky, o) {
    if (SG && root.NmcSceneViews && root.NmcSceneViews.post) root.NmcSceneViews.post(P, { w: P.W, h: P.H, rgba: out, tide: T }, { _E: EG, _S: SG, _plan: PLAN });   // part 2c: part 1's still water (the river), where the brook's tint was
    const F = root.FuelStop; if (!F) return; const Lv = F.levels(o.need || 0); if (Lv.k < 0.02) return;
    const W = P.W, H = P.H, lit = P.lit;
    for (const l of POOLS) { const I = l.I * Lv.k, zc = PAD_Z; if (I < 0.01) continue;
      const x0 = Math.max(0, Math.floor(P.sx(l.x - l.r))), x1 = Math.min(W, Math.ceil(P.sx(l.x + l.r))), y0 = Math.max(0, Math.floor(P.sy(l.y + l.r, zc + 0.3))), y1 = Math.min(H, Math.ceil(P.sy(l.y - l.r, zc - 0.5)));
      for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) { const j = y * W + x, p = map[j]; if (p < 0) continue; const q = j * 4, a = p * 4;
        if (out[q] !== lit[a] || out[q + 1] !== lit[a + 1] || out[q + 2] !== lit[a + 2]) continue;
        const d = Math.hypot(P.wx(p % W) - l.x, P.wy((p / W) | 0) - l.y); if (d >= l.r) continue; const u = 1 - d / l.r, k = I * u * u * 0.72;
        out[q] += (l.c[0] - out[q]) * k; out[q + 1] += (l.c[1] - out[q + 1]) * k; out[q + 2] += (l.c[2] - out[q + 2]) * k; } }
    const R = 8, acc = new Float32Array(W * H), cr = new Float32Array(W * H), cg = new Float32Array(W * H), cb = new Float32Array(W * H), NR = {};
    for (const k in F.NEON) NR[k] = F.NEON[k].map(h2r);
    let x0 = W, x1 = 0, y0 = H, y1 = 0;
    for (const s of P.sprites) { if (s.kind !== 'fuel' || (s.variants && !s.variants.includes(o.variant))) continue; const Fr = s._F; if (!Fr || !Fr.fr || !Fr.fr.neonPx || !Fr.fr.neonPx.length) continue;
      const fr = Fr.fr, ox = Math.round(P.sx(s.at[0])) - Fr.px, oy = Math.round(P.sy(s.at[1], s.z)) - Fr.py, EK = fr.mdl.EK;
      for (let n = 0; n < fr.neonPx.length; n += 3) { const i = fr.neonPx[n], q = EK[fr.gb.em[i]]; if (!q) continue; const c = (NR[q.col] || NR.white)[3], lv = q.type === 'led' ? Lv.led * 0.5 : Lv.neon;
        const sx = ox + i % fr.W, sy = oy + ((i / fr.W) | 0);
        for (let dy = -R; dy <= R; dy++) { const Y = sy + dy; if (Y < 0 || Y >= H) continue; for (let dx = -R; dx <= R; dx++) { const X = sx + dx; if (X < 0 || X >= W) continue;
          const d = Math.hypot(dx, dy); if (d > R) continue; const w = lv * (1 - d / R) * (1 - d / R) * 0.05, j = Y * W + X; acc[j] += w; cr[j] += w * c[0]; cg[j] += w * c[1]; cb[j] += w * c[2];
          if (X < x0) x0 = X; if (X > x1) x1 = X; if (Y < y0) y0 = Y; if (Y > y1) y1 = Y; } } } }
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) { const j = y * W + x, a = acc[j]; if (a < 0.003) continue; const t = Math.min(0.62, 1 - Math.exp(-a * 1.6)), q = j * 4;
      out[q] += (cr[j] / a - out[q]) * t; out[q + 1] += (cg[j] / a - out[q + 1]) * t; out[q + 2] += (cb[j] / a - out[q + 2]) * t; }
  }

  const PLACES = [
    { id: 'store', label: 'The store (the name is open)', at: [19, 53, 5.4] }, { id: 'door', label: 'The door', at: [19, 45.4, 2.3] },
    { id: 'pumps', label: 'The pumps', at: [20, 22.6, 2.0] }, { id: 'semi', label: 'The semi lane', at: [47, 29.6, 2.1] },
    { id: 'pylon', label: 'The price sign', at: [PYLON[0], PYLON[1], 7.7] }, { id: 'ice', label: 'Ice & propane', at: [9.4, 45.4, 1.6] },
    { id: 'wharfroad', label: 'Wharf Road, to the wharf', at: [36, 5.0, 0.1] }, { id: 'route91', label: 'Route 91', at: [-3.45, 60, 0.1] },
    { id: 'junction', label: 'The junction', at: [-3.5, 5, 0.1] }, { id: 'lane', label: 'The river lane (on to the boat shed)', at: [57.5, 30, 0.1] },
    { id: 'pull', label: 'The pull-off above the river', at: [57.5, 61.2, 0.1] }, { id: 'rim', label: 'The river valley\u2019s rim', at: [10, 99, 0.1] },
  ];

  root.NmcFuelStop = { id: 'nmc_fuel_stop', name: 'Nine Mile Creek: the gas station', site: SITE, frame: FRAME, seed: 61, relief: 2.4, channels: 2, nearest: {},
    fetch: () => 0.2, zones: ZONES, sample, zone, groundAt, cliffs: null, pieces, fixedFlora, spacing: SPACING, places: PLACES, post, afterPaint,
    scatter: [{ name: 'trees', pitch: 2.0, pick: treePick }, { name: 'rocks', pitch: 1.4, pick: rockPick }, { name: 'plants', pitch: 0.6, pick: herbPick }, { name: 'tufts', pitch: 0.5, pick: tuftPick }],
    NETS, STORE, CANOPY, TRUCK, PYLON, WR, R91, LANE, STREAM, streamAt, EXIT, PAD_Z, OFFSET, toRegion, roadSoft, paveD, PIECES, ANGLERS, part: '2c',
    woods, gullyK, siteK, builtD, tpD, STALLS, FILLS, FILLCOL, get TPARK() { return TPARK.slice(); },   // part 2c (10-01): for the region view, which lays the gas bar on its own stage
    setGround(E, S, plan) { EG = E; SG = S; PLAN = plan;
      const tp = plan && plan.lots && (plan.lots.working || []).find(l => l.id === 'lot.nmc_truck_park');
      if (tp && tp.at && tp.size) TPARK = [tp.at[0] - OFFSET[0] - tp.size[0] / 2, tp.at[0] - OFFSET[0] + tp.size[0] / 2, tp.at[1] - OFFSET[1] - tp.size[1] / 2, tp.at[1] - OFFSET[1] + tp.size[1] / 2].map(v => Math.round(v * 1000) / 1000); } };
})(typeof globalThis !== 'undefined' ? globalThis : this);
