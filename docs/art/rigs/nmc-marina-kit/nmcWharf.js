/* Nine Mile Creek, the wharf: the scene (keyscene.nmc_wharf), one scene for StpPano (globalThis.NmcWharf).

   Units: region metres (the ground map's frame, NineMileCreekSeabed.asset): x east, y north, world centre (0, 0); z metres
   above mean sea level (the tide is St Peters' own, mean 0, amplitude 2.2). The wharf kits count height from chart datum,
   the spring low: kit z = z + 2.2.

   THE IDEA. Every job the wharf does is laid along south faces the camera sees whole: from the west, the boat ramp and the
   working float, the crane apron and its unloading berth with the cold store behind it, the fleet along the old crib wall and
   its concrete extension, each boat in front of her own shed, then the rec float and the boat yard's lift slip at the east
   end. Two armour-stone arms close the basin and leave a mouth to the south, a light on each head, so the harbour's door is in
   the foreground; the scene return pushes them 28 m further south so a boat has 30 m of clear water round the fingers' ends.
   The yard's back row holds the sheds, the restaurant on its own terrace over the creek, and the harbourmaster's office with
   the fish market across the main road from it. The main road runs straight from the turning pad to the boat yard's gate, the
   cars park beside the upper row of sheds, and the restaurant has a truck turn at its door.                                                                                                 */
(function (root) {
  'use strict';
  const PPM = 32, D2R = Math.PI / 180, SE = Math.sin(40 * D2R), CE = Math.cos(40 * D2R), PI = Math.PI, HP = PI / 2, KZ = 2.2;
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v, lerp = (a, b, t) => a + (b - a) * t, r3 = (v) => Math.round(v * 1000) / 1000;
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
  const rectD = (x, y, x0, x1, y0, y1) => Math.max(x0 - x, x - x1, y0 - y, y - y1);   // < 0 inside

  // =====================================================================================================================
  //  THE LAYOUT (the numbers the builders take; NOTES lists them old and new)
  // =====================================================================================================================
  const DECK = 3.4, YARD = 3.4, LAND = 6.0;
  const L = {
    deck: DECK,
    craneApron: { x0: 50, x1: 78, face: 76, back: 88 },                    // concrete, yellow curb: the unloading berth on its south face
    fleetW: { x0: 78, x1: 111.6, face: 86, back: 96 },                     // the old crib wall, concrete cap
    fleetE: { x0: 111.6, x1: 162, face: 86, back: 96 },                    // its concrete extension
    eastQuay: { x0: 162, x1: 184, face: 86, back: 96 },                    // the rec float's gate
    yardFront: { x0: 184, x1: 212, face: 86, back: 96 },                   // the boat yard's quay, the lift slip's piers
    liftPiers: [192, 204], liftPierLen: 19.2,
    // made ground, +3.4, crowned 0.25 m: the apron, the yard, its north-west extension (the turning circle), the restaurant's terrace (new)
    yard: [[47, 78, 76, 96], [42, 212, 86, 138], [42, 104, 86, 146], [156, 184, 136, 152]],
    // the arms' crest lines, crest +3.6 (the kit's granite armour); the core under them +2.4 at 3.2 m out, then 1:1.1 to the bed
    armW: [[38, 2], [116, 2]], armE: [[212, 86], [212, 20], [158, -6]],
    lights: { west: [115.2, 2.0, 'green'], east: [158.6, -5.6, 'red'] },
    mouth: [[116, 2], [158, -6]],
    // the dredging: rectangles [x0, x1, y0, y1, bed] with 1:5 sides and ragged edges; channels (polylines, half-width, bed); pockets (ellipses)
    dredge: [[79, 184, 74, 86, -4.4], [52, 78, 64, 76, -4.2], [86, 166, 12, 80, -4.4], [36, 78, 30, 66, -3.4]],   // ... the ramp's toe and the working float: 1.2 m at spring low beyond the toe   // the wall trench, the unloading berth, the basin, the ramp and the working float
    channels: [{ id: 'approach', pts: [[137, -40], [137, -6], [136, 14]], hw: 11, bed: -4.4 }, { id: 'crane', pts: [[90, 40], [80, 52], [70, 63], [62, 69]], hw: 7, bed: -4.4 },
      { id: 'east', pts: [[164, 44], [180, 56], [198, 66]], hw: 6, bed: -4.4 }],
    pockets: [[164, 192, 60, 74, -3.2], [192, 204, 62, 86, -4.4]],        // the rec float, the lift slip (it hauls the Cape Islander at any tide)
    fingers: [{ id: 'fingerA', x: 109.7 }, { id: 'fingerB', x: 142.3 }], fingerLen: 27.2, fingerSouth: 43.9,
    turn: { c: [62, 125], r: 15 },                                          // the semis' turning circle, clear of everything
    plot: [80.6, 92.4, 96.8, 106.6],                                       // berth 0's shed lot, left empty to build on
    ramp: { x: 44, w: 5, head: 86, mid: 66, toe: 46, zHead: 3.4, zMid: 0.4, zToe: -2.6 },   // two slipway modules, 15 %
    clearance: { min: 30, x: [96, 156], need: -4.1 },                     // clear water south of the fingers' ends for the design boat
    workingFloat: { x: [48, 72], y: [58.635, 61.035], note: 'where WharfRig2Kit.gangwayFrom puts it off the apron (the bake logs it)' },
    crane: { at: [72.0, 78.2], pad: [67.6, 81.2], padSize: 2.4, deck: [69.5, 73.4] },   // deck: Yvette's cockpit, under the hook
    office: { at: [86.4, 112.6] }, market: { at: [91.2, 127.6], box: [88.0, 95.7, 124.2, 130.2] },   // north of the main road, facing it and the office across it
    mainRoad: { pts: [[76, 120], [190, 120]], w: 7.5, gate: [184.4, 115, 124] },          // straight from the turning pad to the boat yard's gate
    parking: [{ id: 'yard.parking_w', box: [96.5, 106.0, 124.1, 133.1] }, { id: 'yard.parking_e', box: [141.2, 146.8, 123.5, 132.5] }],   // beside the upper row of sheds
    truckTurn: { id: 'yard.restaurant_turn', box: [160.5, 176.5, 118.0, 131.0] },        // off the main road, at the restaurant's door: a box truck turns here
    cars: [['buyer_truck', 'VehicleIso', 98.2, 128.4, 4], ['lot_e', 'VehicleIso', 101.0, 128.4, 4], ['lot_a', 'VehicleIso', 103.8, 128.4, 4], ['lot_b', 'VanIso', 142.6, 128.0, 4], ['lot_c', 'VehicleIso', 145.4, 128.0, 4], ['delivery', 'VanIso', 171.2, 127.8, 6]],
    restaurant: { at: [168, 136], box: [160, 176, 131.5, 140.5], deck: [160, 179.6, 140.5, 150.0], patio: [160, 179.6, 144.6, 150.0] },
    shorePath: [[60, -150], [56, -130], [50, -110], [44, -88], [38.5, -64], [35.5, -40], [34, -20], [34.2, -8], [35.2, 1.0], [34, 8], [32.8, 18], [32.5, 40], [33, 64], [34, 88], [36, 104], [40, 112]],
    basinBed: -1.6, seaBed: -5.6,
  };
  // the fleet at the wall, west to east: the register (Data/Boats/Owners) and its sheds (m of frontage the register asks)
  const FLEET = [
    { id: 'player', berth: 0, slot: [78.6, 93.8], type: null, shed: null, note: 'kept for the player' },
    { id: 'leo', owner: 'Leo Arsenault', berth: 1, slot: [93.8, 108.2], type: 'lobStdHardtopNth', paint: 'harbour', hd: 0, shed: ['gambrelBarn', 101.0, 1.0, 10] },
    { id: 'marie', owner: 'Marie Gallant', berth: 2, slot: [111.2, 126.4], type: 'capeIslander', paint: 'sage-green', hd: PI, shed: ['redShed', 118.8, 0.85, 9] },
    { id: 'yvette', owner: 'Yvette Doiron', berth: 3, slot: [126.4, 140.8], type: 'lobStdOpenFundy', paint: 'spruce', hd: 0, shed: ['netShed', 133.6, 0.9, 8], atCrane: true },
    { id: 'ross', owner: 'Ross MacDonald', berth: 4, slot: [143.8, 154.8], type: 'lobInHardtopNfld', paint: 'gelcoat', hd: PI, shed: ['tealShack', 149.3, 0.9, 8] },
    { id: 'hughie', owner: 'Hughie Campbell', berth: 5, slot: [154.8, 165.8], type: 'lobInOpenNth', paint: 'harbour', hd: 0, shed: ['netShed', 160.3, 0.55, 6] },
  ];
  const FLOAT_FLEET = [
    { id: 'celeste', owner: 'Celeste Bernard', rig: 'DoryIso', opts: { build: 'motor' }, loa: 4.5, beam: 1.5, float: 0.2, shed: ['tealShack', 110.5, 0.35] },
    { id: 'alma', owner: 'Alma Boudreau', rig: 'ConsoleIso', opts: {}, loa: 7.0, beam: 2.3, float: 0.35, shed: ['netShed', 118.5, 0.35] },
    { id: 'dan', owner: 'Dan Peters', rig: 'PuntIso', opts: {}, loa: 5.2, beam: 1.58, float: 0.22, shed: ['tealShack', 125.6, 0.15] },
  ];
  const REC = [['bowrider', 'summer-navy'], ['sportSkiff', null], ['bowrider', 'cranberry']];
  // the two floating fingers: four boats each, two a side, bows to the wall, north first. Finger A's west side holds the design
  // boat (a Cape Islander, the register's biggest hull) in its south berth, so her track goes round the finger's end
  const FINGER_FLEET = [
    [['lobInHardtopNfld', 'spruce', 'w'], ['capeIslander', 'banks-white', 'w'], ['lobStdHardtopNth', 'gelcoat', 'e'], ['lobInOpenNth', 'gelcoat', 'e']],
    [['lobStdOpenFundy', 'harbour', 'w'], ['lobInHardtopNfld', 'gelcoat', 'w'], ['lobStdHardtopFundy', 'spruce', 'e'], ['capeIslander', 'sage-green', 'e']],
  ];
  // the design boat and her track in and out (waypoints; the track is the smooth curve through them, sampled every metre)
  const DESIGN = { type: 'capeIslander', loa: 12.8, beam: 4.4, draught: 1.4, berth: 'fingerA west, the south berth', at: [106.26, 54.02], heading: 'north' };
  const TRACK = {
    in: [[137, -45], [137, -10], [136.2, 5], [131, 16.5], [121, 23.5], [108, 27], [98.5, 31.5], [94.5, 38], [96.5, 44], [100.8, 47.6], [104.6, 50.6], [106.26, 54.02]],
    outAstern: [[106.26, 54.02], [106.1, 46], [106.0, 38.5]],
    out: [[106.0, 38.5], [108.2, 31.5], [115, 25.5], [126, 21], [133.5, 13], [136.6, 2], [137, -10], [137, -45]],
  };
  function curve(wp, step) { const out = [], P = wp, n = P.length; if (n < 2) return P.slice();
    for (let k = 0; k + 1 < n; k++) { const p0 = P[Math.max(0, k - 1)], p1 = P[k], p2 = P[k + 1], p3 = P[Math.min(n - 1, k + 2)], Ls = Math.hypot(p2[0] - p1[0], p2[1] - p1[1]), m = Math.max(1, Math.ceil(Ls / (step || 1)));
      for (let j = 0; j < m; j++) { const t = j / m, t2 = t * t, t3 = t2 * t, f = (a, b, c, d) => 0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3); out.push([f(p0[0], p1[0], p2[0], p3[0]), f(p0[1], p1[1], p2[1], p3[1])]); } }
    out.push(P[n - 1].slice()); return out; }

  // =====================================================================================================================
  //  THE GROUND (numbers: outlines, heights, slopes)
  // =====================================================================================================================
  const faceY = (x) => x >= 50 && x < 78 ? 76 : x >= 78 && x <= 212 ? 86 : null;   // the quay faces: vertical
  function yardD(x, y) { let d = 1e9; for (const r of L.yard) d = Math.min(d, rectD(x, y, r[0], r[1], r[2], r[3])); return d; }
  function yardZ(x, y) { return YARD + 0.25 * Math.sin(PI * clamp((y - 86) / 52, 0, 1)) + 0.04 * (vn(x / 7, y / 7, 3) - 0.5); }
  function inHarbour(x, y) {   // north of the west arm and the mouth, west of the east arm and its south-west leg, south of the quays
    if (y > 88 || x > 212 || x < 38) return false;
    if (x <= L.mouth[0][0]) return y > L.armW[0][1];
    const a = L.armE[1], b = L.armE[2]; if (x >= b[0] && (b[0] - a[0]) * (y - a[1]) - (b[1] - a[1]) * (x - a[0]) > 0) return false;
    const m0 = L.mouth[0], m1 = L.mouth[1]; return (m1[0] - m0[0]) * (y - m0[1]) - (m1[1] - m0[1]) * (x - m0[0]) > 0; }
  function harbourEdge(x, y) {   // metres inside the harbour's outline (for the blend into its bed)
    const m0 = L.mouth[0], m1 = L.mouth[1], a = L.armE[1], b = L.armE[2];
    const dm = ((m1[0] - m0[0]) * (y - m0[1]) - (m1[1] - m0[1]) * (x - m0[0])) / Math.hypot(m1[0] - m0[0], m1[1] - m0[1]);
    const de = -((b[0] - a[0]) * (y - a[1]) - (b[1] - a[1]) * (x - a[0])) / Math.hypot(b[0] - a[0], b[1] - a[1]);
    return Math.min(212 - x, x < 60 ? 99 : x - 38, x <= m0[0] ? y - m0[1] : 99, x > m0[0] && x < b[0] ? dm : 99, x >= b[0] ? de : 99); }
  const creekY = (x) => 148.5 + 3.2 * Math.sin(x / 17) + 1.4 * Math.sin(x / 6.3) + 13 * ss(146, 160, x) * ss(196, 184, x);   // the terrace pushes the low-water channel north
  // the sea bed with no made ground: the basin, its dredging, the approach, the north flats
  function seabed(x, y) {
    let e = L.seaBed + 1.4 * ss(60, 30, x) + 0.35 * (fb(x / 18, y / 18, 11) - 0.5);
    if (y > 136) { const n = ss(136, 146, y); e = lerp(e, 0.2 - 0.9 * ss(142, 166, y) + 0.3 * (fb(x / 9, y / 7, 13) - 0.5), n); }   // the north flats, the creek mouth
    const crk = Math.abs(y - creekY(x)); if (y > 140) e = Math.min(e, lerp(-1.7, e, ss(1.6, 4.2, crk)));                            // Nine Mile Creek's channel
    if (inHarbour(x, y)) { const k = ss(0, 3, harbourEdge(x, y)); e = lerp(e, L.basinBed + 0.9 * (fb(x / 14, y / 11, 17) - 0.5) + 0.25 * (vn(x / 4, y / 3, 19) - 0.5), k); }
    const cut = (d, bed) => { d += 2.2 * (fb(x / 9, y / 7, 29) - 0.5); if (d < 0) e = Math.min(e, bed + 0.3 * (vn(x / 3, y / 3, 31) - 0.5)); else if (d < 14) e = Math.min(e, bed + d / 5); };
    for (const r of L.dredge) cut(rectD(x, y, r[0], r[1], r[2], r[3]), r[4]);
    for (const r of L.pockets) { const cx = (r[0] + r[1]) / 2, cy = (r[2] + r[3]) / 2, ax = (r[1] - r[0]) / 2, ay = (r[3] - r[2]) / 2; cut((Math.hypot((x - cx) / ax, (y - cy) / ay) - 1) * Math.min(ax, ay), r[4]); }
    for (const C of L.channels) cut(nearSeg(C.pts, x, y).d - C.hw, C.bed);
    return e;
  }
  // the arms: a mound under the kit's armour (the stones to +3.6), +2.4 at 3.2 m from the crest line, then 1:1.1 to the bed
  function armCore(x, y) { let z = -9;
    for (const Q of [L.armW, L.armE]) { const n = nearSeg(Q, x, y), rag = 1.6 * (fb(x / 5.5, y / 5.5, 27) - 0.5); z = Math.max(z, 2.4 - Math.max(0, n.d - 3.2 + rag) / 1.1 + 0.25 * (vn(x / 3, y / 3, 21) - 0.5)); }
    return z; }
  // the mainland: the fields at +6, a red bank down to a cobble toe at the basin's west side
  function mainland(x, y) { const xb = 36.5 + 1.6 * (vn(0.5, y / 9, 23) - 0.5); if (x > xb + 12) return -9;
    return x < xb ? LAND + 0.25 * (fb(x / 12, y / 12, 25) - 0.5) : LAND - (x - xb) * 0.95; }
  function rampZ(y) { const R = L.ramp; if (y >= R.head) return R.zHead; if (y >= R.mid) return lerp(R.zMid, R.zHead, (y - R.mid) / (R.head - R.mid));
    const g = (R.zMid - R.zToe) / (R.mid - R.toe); return R.zMid - (R.mid - y) * g; }
  function groundAt(x, y) {
    let e = Math.max(seabed(x, y), armCore(x, y), mainland(x, y));
    const d = yardD(x, y), fy = faceY(x);
    if (d <= 0) e = Math.max(e, yardZ(x, y));
    else if (!(fy != null && y < fy && y > fy - 30 && x > 50.5 && x < 211.5)) e = Math.max(e, yardZ(x, y) - d / 1.5);   // revetments; the quays are vertical
    // Wharf Road comes down off the fields onto the yard at its west end
    if (x < 48 && y > 104 && y < 132) e = Math.max(e, lerp(LAND, YARD + 0.1, ss(30, 48, x)) - Math.max(0, Math.abs(y - 118) - 6) * 0.5);
    // the boat ramp: its corridor cut and filled to the slab's underside (0.3 m under the ramp), the sides at 1:1.2
    const R = L.ramp, dx = Math.abs(x - R.x) - R.w / 2;
    if (dx < 7 && y > R.toe - 8 && y < R.head + 1) { const zr = rampZ(y) - 0.3, k = ss(R.toe - 8, R.toe - 2, y);
      const want = dx <= 0 ? zr : clamp(e, zr - dx * 1.2, zr + dx * 1.2); e = lerp(e, want, k); }
    return e;
  }
  const kitBed = (x, y) => seabed(x, y) + KZ;   // what the wharf kit stands its piles and faces on

  // =====================================================================================================================
  //  THE HARBOURS (WharfRig2Kit / MarinaKit / ShipyardGeo2), each its own sprite, origin (OX, 0), kit metres
  // =====================================================================================================================
  const H = {};
  // the berths each fixed face publishes (its ladders at the berths' middles, tyres at the quarters, lines at the ends and a spring)
  const WALL_BERTHS = FLEET.map(f => { const m = (f.slot[0] + f.slot[1]) / 2; return { id: 'berth' + f.berth, x: m, fenders: [f.slot[0] + 2.6, f.slot[1] - 2.6], lines: [f.slot[0] + 0.9, m + 2.0, f.slot[1] - 0.9] }; })
    .concat([{ id: 'east', x: 180.2, fenders: [178, 182.4], lines: [183.2] }]);
  const CRANE_BERTHS = [{ id: 'unloadW', x: 55.5, fenders: [58.2, 63.4], lines: [52.8] }, { id: 'unloadE', x: 76.6, fenders: [66.8, 73.0], lines: [77.4, 64.6] }];
  const LADDERS = WALL_BERTHS.map(b => ({ id: 'ladder.nmc_' + b.id, x: b.x, face: 86 })).concat(CRANE_BERTHS.map(b => ({ id: 'ladder.nmc_' + b.id, x: b.x, face: 76 })));
  function planned(q, list) { const cx = (q.x0 + q.x1) / 2, hx = (q.x1 - q.x0) / 2, lx = (X) => r3(cx - X), inside = (X) => X > q.x0 + 0.45 && X < q.x1 - 0.45;
    return { water: { list: list.filter(b => inside(b.x)).map(b => ({ id: b.id, x: lx(b.x), cls: 'B', fenderX: b.fenders.filter(inside).map(lx), lines: b.lines.filter(inside).map(lx) })), faceLen: r3(2 * hx), kind: 'quay', utilisation: 1, segments: [], dry: [] } }; }
  function defineHarbours() {
    const K = root.WharfRig2Kit, MK = root.MarinaKit, MB = root.MarinaBoats, MG = root.MarinaGeo; if (!K) return;
    const TR = 4.4, KD = DECK + KZ;
    const quay = (id, key, q, OX, o) => ({ id, key, at: [r3((q.x0 + q.x1) / 2 - OX), (q.face + q.back) / 2], rot: PI,
      o: Object.assign({ bays: Math.round((q.x1 - q.x0) / 4.2), bayLen: r3((q.x1 - q.x0) / Math.round((q.x1 - q.x0) / 4.2)), width: q.back - q.face, deckZ: KD, tideRange: TR }, o || {}) });
    const hullBox = (type, paint) => MB && MB.loaded(type) ? MB.build(type, paint) : null;
    const alongside = (type, paint, x, faceY0, hd, off) => { const B = hullBox(type, paint); if (!B) return null;
      const cy = hd === 0 ? faceY0 - off + B.bbox.x0 : faceY0 - off - B.bbox.x1; return { B, at: [x, r3(cy)] }; };
    // ---- 1. the west: the boat ramp, the crane apron and its ladders, the working float and its gangway, Yvette unloading
    { const OX = 64, q = quay('apron', 'concreteQuay', L.craneApron, OX, { curb: 'yellow', bays: 7, bayLen: 4.0, width: 12, fixtures: [], berthPlanned: planned(L.craneApron, CRANE_BERTHS) });
      const gw = K.gangwayFrom(q, 'water6', { freeboard: 0.45, floatGrp: 1 }), hw = 1.2;
      const fl = { id: 'float', key: 'float', grp: 1, rot: PI, at: [r3(gw.edgeAt[0] + 6.0), r3(gw.edgeAt[1] - hw)], o: { hull: 'timber', bays: 8, bayLen: 3.0, width: 2.4, freeboard: 0.45, tideRange: TR, faces: ['water'], open: { shore: [[-7.0, -5.0]] }, fixtures: [] } };
      const Rm = L.ramp, up = Rm.head - Rm.mid, lo = Rm.mid - Rm.toe;
      const ramps = [{ id: 'rampUpper', key: 'slipway', at: [r3(Rm.x - OX), r3((Rm.head + Rm.mid) / 2)], rot: -HP, o: { run: up, width: Rm.w, deckZ: Rm.zHead + KZ, toeZ: Rm.zMid + KZ, tideRange: TR } },
        { id: 'rampLower', key: 'slipway', at: [r3(Rm.x - OX), r3((Rm.mid + Rm.toe) / 2)], rot: -HP, o: { run: lo, width: Rm.w, deckZ: Rm.zMid + KZ, toeZ: Rm.zToe + KZ, tideRange: TR } }];
      const boats = []; const Y = FLEET.find(f => f.atCrane), a = Y && alongside(Y.type, Y.paint, 67 - OX, L.craneApron.face, PI, 0.6);   // bow west, as she came in: her cockpit under the hook
      if (a) boats.push({ id: 'unload.' + Y.id, type: Y.type, paint: Y.paint, at: a.at, heading: PI, mooring: 'alongside', dock: 'apron', boardAt: [a.at[0], 0] });
      H.west = { OX, gw, fl, boats, floatSouth: fl.at[1] - hw, floatX: [r3(fl.at[0] + OX - 12), r3(fl.at[0] + OX + 12)] };
      MK.defineMarina('nmc_west', { label: 'NMC \u00b7 the ramp, the crane apron and the working float', tideRange: TR, bed: (x, y) => kitBed(x + OX, y), road: () => false, fetch: () => 0.25,
        layout: () => ({ mods: [q, fl].concat(ramps), gangs: [gw], boats }) }); }
    // ---- 2. the fleet wall: the old crib and its concrete extension, its ladders, the fleet alongside, the two fingers
    { const OX = 120, loc = (q, X) => r3((q.x0 + q.x1) / 2 - X), gA = L.fingers[0].x, gB = L.fingers[1].x;
      const w = quay('crib', 'logCrib', L.fleetW, OX, { cap: 'concrete', curb: 'wood', bays: 8, bayLen: 4.2, width: 10, ends: { nx: 'join' }, open: { water: [[loc(L.fleetW, gA) - 1.2, loc(L.fleetW, gA) + 1.2]] }, berthPlanned: planned(L.fleetW, WALL_BERTHS) }),
        e = quay('conc', 'concreteQuay', L.fleetE, OX, { curb: 'wood', bays: 12, bayLen: 4.2, width: 10, ends: { px: 'join' }, open: { water: [[loc(L.fleetE, gB) - 1.2, loc(L.fleetE, gB) + 1.2]] }, berthPlanned: planned(L.fleetE, WALL_BERTHS) }), boats = [], fmods = [], gangs = [];
      const hang = (q, mod, X, grp, id) => { const hx = (q.x1 - q.x0) / 2, bl = mod.o.bayLen, lx = loc(q, X), i = Math.max(0, Math.min(mod.o.bays, Math.round((lx + hx) / bl))), sl = r3(lx - (-hx + i * bl));
        return K.gangwayFrom(mod, 'water' + i, { slide: sl, freeboard: 0.45, floatGrp: grp, id }); };
      H.fingerBoats = [];
      L.fingers.forEach((fg, k) => { const grp = k + 1, gw = hang(k ? L.fleetE : L.fleetW, k ? e : w, fg.x, grp, 'gangway' + k), len = L.fingerLen, hw = 1.2;
        gangs.push(gw); const cy = r3(gw.edgeAt[1] - len / 2);
        fmods.push({ id: fg.id, key: 'float', grp, rot: -HP, at: [r3(gw.edgeAt[0]), cy], o: { hull: 'timber', bays: 8, bayLen: r3(len / 8), width: 2.4, freeboard: 0.45, tideRange: TR, faces: ['water', 'shore'], guideFace: 'water', fixtures: [] } });
        const sides = { w: gw.edgeAt[1] - 0.6, e: gw.edgeAt[1] - 0.6 };
        for (const [type, paint, side] of FINGER_FLEET[k]) { const B = hullBox(type, paint); if (!B) continue; const Lh = B.bbox.y1 - B.bbox.y0, hd = HP;
          const cx = side === 'w' ? gw.edgeAt[0] - hw - 0.14 - B.bbox.x1 : gw.edgeAt[0] + hw + 0.14 - B.bbox.x0, top = sides[side], cyB = r3(top - B.bbox.y1);
          boats.push({ id: fg.id + '.' + side + boats.length, type, paint, at: [r3(cx), cyB], heading: hd, mooring: 'alongside', dock: fg.id, boardAt: [r3(gw.edgeAt[0]), cyB] });
          H.fingerBoats.push({ finger: k, side, type, x: r3(cx + OX), y0: r3(top - Lh), y1: r3(top), beam: r3(B.bbox.x1 - B.bbox.x0) }); sides[side] = top - Lh - 1.4; } });
      for (const f of FLEET) { if (!f.type || f.atCrane || f.slot[0] > 162) continue; const a = alongside(f.type, f.paint, (f.slot[0] + f.slot[1]) / 2 - OX, 86, f.hd, 0.6);
        if (a) boats.push({ id: 'berth' + f.berth + '.' + f.id, type: f.type, paint: f.paint, at: a.at, heading: f.hd, mooring: 'alongside', dock: 'wall', boardAt: [a.at[0], 86] }); }
      H.fleet = { OX, boats, gangs };
      MK.defineMarina('nmc_fleet', { label: 'NMC \u00b7 the fleet wall and the two fingers', tideRange: TR, bed: (x, y) => kitBed(x + OX, y), road: () => false, fetch: () => 0.25, layout: () => ({ mods: [w, e].concat(fmods), gangs, boats }) }); }
    // ---- 3. the east: the rec float off its gate, its ladder
    { const OX = 173, q = quay('east', 'concreteQuay', L.eastQuay, OX, { curb: 'wood', bays: 5, bayLen: 4.4, width: 10, berthPlanned: planned(L.eastQuay, WALL_BERTHS), marina: { items: [{ kind: 'gate', id: 'gate', x: -2.2, y: 3.6, rot: PI }, { kind: 'lampPost', x: -4.9, y: 3.8, rot: 0 }], cleats: [] } });
      const gw = K.gangwayFrom(q, 'water1', { freeboard: (MG && MG.FREEBOARD && MG.FREEBOARD.composite) || 0.48, floatGrp: 1 }), hw = 1.25, fbd = (MG && MG.FREEBOARD && MG.FREEBOARD.composite) || 0.48;
      const lights = [-8.5, -2.5, 3.5, 9.5].map((x, k) => ({ kind: 'dockLight', x, y: (k % 2 ? 1 : -1) * (hw - 0.2), rot: 0 }));
      const items = lights.concat([-6.8, 0.4, 7.4].map((x, k) => ({ kind: 'pedestal2', id: 'ped' + k, x, y: hw - 0.3, rot: PI, serves: ['rec' + k] })));
      const fl = { id: 'recFloat', key: 'float', grp: 1, rot: PI, at: [r3(gw.edgeAt[0] - 4.2), r3(gw.edgeAt[1] - hw)], o: { hull: 'composite', bays: 7, bayLen: 3.0, width: 2.5, freeboard: fbd, tideRange: TR, faces: ['water'], guideFace: 'shore', open: { shore: [[3.4, 5.0]] }, fixtures: [], marina: { conduits: ['water', 'shore'], items, cleats: [] } } };
      const boats = []; let x = fl.at[0] + 10.2; const fs = fl.at[1] - hw;
      REC.forEach(([t, p], k) => { const B = hullBox(t, p); if (!B) return; const Lh = B.bbox.y1 - B.bbox.y0, cx = x - B.bbox.y1; boats.push({ id: 'rec' + k, type: t, paint: p || undefined, at: [r3(cx), r3(fs - 0.15 + B.bbox.x0)], heading: 0, mooring: 'alongside', dock: 'recFloat', boardAt: [r3(cx), fs] }); x -= Lh + 1.3; });
      H.east = { OX, gw, fl, boats, lights: lights.map(l => [r3(fl.at[0] + OX - l.x), r3(fl.at[1] - l.y)]) };
      MK.defineMarina('nmc_east', { label: 'NMC \u00b7 the rec float', tideRange: TR, bed: (x, y) => kitBed(x + OX, y), road: () => false, fetch: () => 0.25, layout: () => ({ mods: [q, fl], gangs: [gw], boats }) }); }
    // ---- 4. the boat yard: its quay, the lift slip's runway piers, the lift with a boat in her slings, the hard, the shed
    { const OX = 198, g = DECK + KZ, SY = root.ShipyardGeo2;
      const part = (id, p, x, y, rot, args, Lp, Wp) => ({ id, key: 'syPart', at: [r3(x - OX), y], rot: rot || 0, o: { part: p, args: Object.assign({ g }, args), bayLen: Lp || 4, width: Wp || 4, faces: [], fixtures: [], deckZ: g, tideRange: TR } });
      const q = quay('yardFront', 'concreteQuay', L.yardFront, OX, { curb: 'none', bays: 7, bayLen: 4.0, width: 10, open: { water: [[-7.6, -4.4], [4.4, 7.6]] }, fixtures: [] });
      const pier = (id, x) => ({ id, key: 'steelPier', at: [r3(x - OX), r3(86 - L.liftPierLen / 2)], rot: -HP, o: { bays: 6, bayLen: 3.2, width: 2.6, faces: [], fixtures: [], deckZ: g, rail: 'none', curb: 'none', tideRange: TR, ends: { nx: 'join' } } });
      const mods = [q, pier('pierW', L.liftPiers[0]), pier('pierE', L.liftPiers[1])];
      if (SY) { const c = (L.liftPiers[0] + L.liftPiers[1]) / 2;
        mods.push(part('lift', 'lift', c, 75.4, PI, { tw: 6.0, wb: 5.8, hg: 7.8, hull: { type: 'capeIslander', paint: 'banks-white', keelZ: g - 1.0, cy: 0 }, slingY: [-3.6, 3.6], wash: -3.0 }, 13, 13));
        mods.push(part('slab', 'ground', 198, 111, 0, { w: 27.5, d: 30, surf: 'conc', J: 5.0, seed: 13 }, 27.5, 30));
        mods.push(part('hardA', 'hard', 191.5, 103.5, 0, { id: 'hardA', type: 'lobStdHardtopFundy', paint: 'gelcoat', hd: 0 }, 13, 5));
        mods.push(part('hardB', 'hard', 204.0, 113.0, 0, { id: 'hardB', type: 'sportSkiff', paint: 0, hd: 0 }, 9, 4));
        mods.push(part('hardC', 'hard', 208.2, 121.8, 0, { id: 'hardC', type: 'bowrider', paint: 'harbour-white', hd: PI }, 7, 3));
        mods.push(part('shed', 'shed', 199, 131.4, PI, { S: 11, Ln: 11, He: 5.4, Hr: 6.6, colour: 'red', rot: 0, doorW: 7, doorH: 5.0, craneY: 4.2, hookX: -2.0 }, 11, 11));
        mods.push(part('floodW', 'flood', 186, 97.6, 0, { h: 8 }, 1.6, 0.6), part('floodE', 'flood', 210, 124, 0, { h: 8 }, 1.6, 0.6));
        mods.push(part('fence', 'fence', 198, 0, 0, { pts: [[184.4, 98], [184.4, 136]], gate: [17, 26] }, 38, 0.3));   // the gate on the main road
        mods.push(part('props', 'props', 198, 0, 0, { items: [{ kind: 'stands', x: 208, y: 104, n: 4 }, { kind: 'blocking', x: 207.5, y: 118.5, n: 5 }, { kind: 'drums', x: 186.8, y: 127.2, n: 3 }] }, 1, 1)); }
      H.yard = { OX };
      K.define('nmc_yard', { label: 'NMC \u00b7 Harris & Sons boat yard', tideRange: TR, bed: (x, y) => { const X = x + OX; return yardD(X, y) < 0 && y > 86.2 ? g - 0.15 : kitBed(X, y); }, road: () => false, layout: () => ({ mods, gangs: [], lines: [] }) }); }
    // ---- 5. the arms: granite armour on a mound, joined modules
    { const arm = (id, OX, pts) => { const mods = []; let n = 0;
        for (let k = 0; k + 1 < pts.length; k++) { const a = pts[k], b = pts[k + 1], Ls = Math.hypot(b[0] - a[0], b[1] - a[1]), m = Math.ceil(Ls / 36), rot = Math.atan2(b[1] - a[1], b[0] - a[0]);
          for (let j = 0; j < m; j++) { const t = (j + 0.5) / m, bays = Math.max(4, Math.round(Ls / m / 3.6)), bl = r3(Ls / m / bays);
            mods.push({ id: id + (n++), key: 'breakwater', at: [r3(a[0] + (b[0] - a[0]) * t - OX), r3(a[1] + (b[1] - a[1]) * t)], rot: r3(rot), o: { bays, bayLen: bl, width: 8, deckZ: 3.6 + KZ, tideRange: TR, variant: n % 4, ends: { nx: 'join', px: 'join' } } }); } }
        mods[0].o.ends = { nx: 'free', px: 'join' }; mods[mods.length - 1].o.ends = { nx: 'join', px: 'free' }; return mods; };
      H.armW = { OX: 77 }; K.define('nmc_armW', { label: 'NMC \u00b7 the west arm', tideRange: TR, bed: (x, y) => kitBed(x + 77, y), road: () => false, layout: () => ({ mods: arm('w', 77, L.armW), gangs: [], lines: [] }) });
      H.armE = { OX: 190 }; K.define('nmc_armE', { label: 'NMC \u00b7 the east arm', tideRange: TR, bed: (x, y) => kitBed(x + 190, y), road: () => false, layout: () => ({ mods: arm('e', 190, L.armE), gangs: [], lines: [] }) }); }
  }

  // =====================================================================================================================
  //  ZONES, ROADS, FLORA
  // =====================================================================================================================
  const ZONES = ['grass', 'dirt', 'path', 'bank', 'shingle', 'sedge', 'foreshore', 'mud', 'ripple', 'rockweed', 'sand', 'eelgrass', 'silt', 'talus', 'marram', 'marsh'];
  const ZI = {}; ZONES.forEach((k, i) => { ZI[k] = i; });
  function zone(c) {
    const x = c.wx, y = c.wy, e = c.e, nz = vn(x * 2.3, y * 2.3, 43), yd = yardD(x, y);
    if (yd < -0.3 && e > 3.0) { const rut = vn(x / 3.2, y / 1.1, 47), ph = fb(x / 2.4, y / 2.4, 45); return rut > 0.8 || ph < 0.13 ? ZI.dirt : ZI.path; }   // the yard: packed red gravel, ruts
    if (Math.abs(x - L.ramp.x) < L.ramp.w / 2 + 3 && y > L.ramp.toe - 6 && y < L.ramp.head && e > -3) return e > 3.2 ? ZI.bank : ZI.talus;               // the ramp's cutting and its fill
    if (yd < 2.8 && e > 0.4 && yd > -0.3) return e > 2.6 && nz > 0.55 ? ZI.grass : ZI.talus;                                                       // the revetments
    const arm = Math.min(nearSeg(L.armW, x, y).d, nearSeg(L.armE, x, y).d); if (arm < 12 && e > -2.5 && e < 3.2) return e < 1.2 && e > -1.4 ? ZI.rockweed : ZI.shingle;
    if (e > 5.0) return nz > 0.5 ? ZI.grass : ZI.marsh;
    if (x < 50 && e > 1.4) return ZI.bank;
    if (x < 50 && e > -0.6) return vn(x / 2, y / 2, 51) > 0.5 ? ZI.shingle : ZI.foreshore;
    if (y > 136) { if (e > 1.2) return nz > 0.55 ? ZI.marram : ZI.sand; if (e > 0.4) return vn(x / 3, y / 2.5, 53) > 0.45 ? ZI.sedge : ZI.sand;
      if (e > -0.4) return vn(x / 4, y / 3, 55) > 0.6 ? ZI.ripple : ZI.mud; return vn(x / 5, y / 4, 57) > 0.55 ? ZI.eelgrass : ZI.silt; }
    if (inHarbour(x, y)) return vn(x / 6, y / 5, 59) > 0.66 ? ZI.ripple : e > -2.4 && vn(x / 9, y / 8, 60) > 0.5 ? ZI.mud : ZI.silt;
    return vn(x / 7, y / 6, 61) > 0.55 ? ZI.eelgrass : ZI.sand;
  }
  // RoadKit4 routes: Wharf Road coming in, the truck court, berth 0's pad, the parking, the lane to the boat yard, the shore path
  const RT = (id, cls, w, pts, o) => Object.assign({ id, cls, w, pts }, o || {});
  const GRAV = { surf: 'gravel', bands: [], ends: 'flat', marks: {}, wear: 0.62 };
  const inFrame = (pts) => pts.filter(p => p[1] > -34 && p[1] < 120);
  function clipY(pts, y0, y1) { const out = [], lerpP = (a, b, y) => { const t = (y - a[1]) / (b[1] - a[1]); return [r3(a[0] + (b[0] - a[0]) * t), y]; };   // a polyline cut to y0 <= y <= y1 (y rising along it)
    for (let k = 0; k + 1 < pts.length; k++) { const a = pts[k], b = pts[k + 1]; if (b[1] < y0 || a[1] > y1) continue;
      if (!out.length) out.push(a[1] < y0 ? lerpP(a, b, y0) : a.slice()); out.push(b[1] > y1 ? lerpP(a, b, y1) : b.slice()); if (b[1] > y1) break; } return out; }
  const NETS = [
    { id: 'court', box: [40, 82, 106, 144], routes: [RT('road.nmc.wharf_road', 'street', 6.0, [[40, 118], [50, 118]], { bands: [{ role: 'shoulder', w: 0.5 }], wear: 0.55 }), RT('yard.turn', 'street', 31, [[46.5, 125], [78, 125]], Object.assign({}, GRAV, { wear: 0.7 }))] },
    { id: 'plot', box: [79, 94, 95, 108], routes: [RT('plot.berth0', 'town', 9.8, [[80.6, 101.7], [92.4, 101.7]], { surf: 'slab', bands: [], ends: 'flat', marks: {}, wear: 0.08 })] },
    { id: 'yard', box: [74, 192, 112, 136], routes: [RT('yard.main_road', 'street', 7.5, [[76, 120], [190, 120]], Object.assign({}, GRAV, { wear: 0.66 })),
      RT('yard.restaurant_turn', 'street', 13, [[160.5, 124.5], [176.5, 124.5]], Object.assign({}, GRAV, { wear: 0.5 })),
      RT('yard.parking_w', 'street', 9, [[96.5, 128.6], [106.0, 128.6]], Object.assign({}, GRAV, { wear: 0.35 })), RT('yard.parking_e', 'street', 5.6, [[144, 123.5], [144, 132.5]], Object.assign({}, GRAV, { wear: 0.35 }))] },
    { id: 'rampHead', box: [40, 54, 84, 100], routes: [RT('yard.to_ramp', 'street', 5.2, [[44, 86.2], [44.6, 92], [47.5, 97.5], [50.5, 99.5]], Object.assign({}, GRAV, { wear: 0.5 }))] },
  ].concat([[-34, 2], [0, 40], [38, 80], [78, 116]].map(([y0, y1], k) => { const pts = clipY(L.shorePath, y0 - 3, y1 + 3), xs = pts.map(p => p[0]);   // the shore path, in four short boxes, painted in bands
    return { id: 'shore' + k, box: [Math.floor(Math.min(...xs) - 3), Math.ceil(Math.max(...xs) + 3), y0 - 3.5, y1 + 3.5], band: [y0, y1], minE: 2.8, soft: true, routes: [RT('path.nmc_shore_path', 'footpath', 1.3, pts, { wear: 0.5 })] }; }));
  async function afterPaint(P) {
    const RK = root.RoadKit4; P.roadLog = []; if (!RK) { (P.errors || (P.errors = [])).push('NmcWharf: RoadKit4 is not loaded'); return; }
    const t = P.tile, W = P.W, PH = P.PH, E = P.E, E0 = Float32Array.from(E), paved = P._paved = new Uint8Array(P.N), cache = new Map(), live = t.live || (t.live = []);
    const addPal = (base, c) => { const k = base + '|' + c; let p = cache.get(k); if (p == null) { p = t.addPal(base, c); cache.set(k, p); } return p; };
    let mk = 0;
    for (const spec of NETS) {
      const t0 = Date.now(), [bx0, bx1, by0, by1] = spec.box, gw = Math.ceil((bx1 - bx0) * PPM), gh = Math.ceil((by1 - by0) * PPM), n = gw * gh, minE = spec.minE != null ? spec.minE : 2.8;
      const routes = spec.routes.map(r => Object.assign({}, r, { pts: r.pts.map(p => [(p[0] - bx0) * PPM, (by1 - p[1]) * PPM].concat(p.length > 2 ? [p[2]] : [])) }));
      const net = RK.network(routes, { w: gw, h: gh, seed: 71 + spec.id.length * 7 });
      for (const g of net.groups) g.F1 = g.F2 = g.R1 = g.R2 = g.D = g.S = g.TX = g.TY = g.E = null;
      const pt = { n: gw, m: gh, pal: new Uint8Array(n), band: new Int8Array(n).fill(2), h: new Float32Array(n), mark: new Uint16Array(n), lock: new Uint8Array(n), a: new Uint8Array(n).fill(255),
        pals: t.pals, live, mid: 0, markSeq: 0, addPal, addRamp: (hx) => t.addRamp(hx), beginMark() { this.mid = (this.markSeq = (this.markSeq + 1) & 0xffff) || 1; return this.mid; }, endMark() { this.mid = 0; },
        i(x, y) { return y * gw + x; }, set(x, y, p, b, h, l) { const i = y * gw + x; this.pal[i] = p; this.band[i] = b; if (h != null) this.h[i] = h; this.mark[i] = this.mid; if (l) this.lock[i] = 1; } };
      RK.paint(pt, net);
      const c0 = clamp(P.col(bx0), 0, W - 1), c1 = clamp(P.col(bx1), 0, W - 1), r0 = clamp(P.row(by1), 0, PH - 1), r1 = clamp(P.row(by0), 0, PH - 1); let cnt = 0;
      for (let r = r0; r <= r1; r++) { const Y = Math.floor((by1 - P.wy(r)) * PPM); if (Y < 0 || Y >= gh) continue; if (spec.band && (P.wy(r) < spec.band[0] || P.wy(r) >= spec.band[1])) continue;
        for (let c = c0; c <= c1; c++) { const X = Math.floor((P.wx(c) - bx0) * PPM); if (X < 0 || X >= gw) continue; const j = Y * gw + X, role = net.role[j]; if (!role || (!pt.lock[j] && !spec.soft)) continue;
          const i = r * W + c; if (E0[i] < minE) continue;
          t.pal[i] = pt.pal[j]; t.band[i] = pt.band[j]; t.h[i] = pt.h[j]; t.mark[i] = pt.mark[j] ? 20000 + ((pt.mark[j] + mk) % 45000) : 0; t.lock[i] = 1;
          E[i] = E0[i] + net.zr[j] + net.zg[j]; paved[i] = role === 1 ? 1 : 2; cnt++; } }
      mk += 7919; P.roadLog.push(spec.id + ' ' + gw + 'x' + gh + ' ' + cnt + ' texels ' + (Date.now() - t0) + ' ms'); if (typeof console !== 'undefined') console.log('[nmc] net ' + P.roadLog[P.roadLog.length - 1]);
      await new Promise(r => setTimeout(r, 0)); }
  }
  // habitats: the yard's edges (goldenrod, lupins, rugosa, grass), the revetment tops, the bank, the fields, the north flats
  const BUILT = [];
  function builtD(x, y) { let d = 9; for (const b of BUILT) { const q = rectD(x, y, b[0], b[1], b[2], b[3]); if (q < d) d = q; } return d; }
  function sample(x, y, out) { out[0] = groundAt(x, y); out[1] = builtD(x, y); }
  function habitat(c) {
    const P = c.pano, i = c.i, x = c.x, y = c.y, z = c.zone, e = c.e;
    if (P._paved && P._paved[i]) return null; if (P.CH[0][i] < 0.8) return null;
    const yd = yardD(x, y);
    if (z === 'rockweed') return Math.min(nearSeg(L.armW, x, y).d, nearSeg(L.armE, x, y).d) < 13 ? null : 'rockweed'; if (z === 'eelgrass') return 'eelgrass'; if (z === 'marram') return 'dune'; if (z === 'sedge') return 'fringe';
    if (z === 'bank') return 'bankFace'; if (z === 'mud' && y > 136) return e > 0.05 && e < 0.9 ? 'glasswort' : null;
    if (yd < -0.3) { if (yd > -2.6 || z === 'grass') return 'edge'; return vn(x / 2.2, y / 2.2, 63) > 0.8 ? 'weeds' : null; }
    if (z === 'talus' && e > 2.4) return 'edge';
    if (z === 'grass' || z === 'marsh') return e > 5 ? 'meadow' : 'edge';
    return null;
  }
  const HERBS = {
    meadow: [['MeadowGrass', 26], ['Timothy', 16], ['Lupin', 14], ['Goldenrod', 9], ['OxeyeDaisy', 8], ['QueenAnne', 6], ['Buttercup', 6], ['Fireweed', 3], ['WildRose', 2], ['Raspberry', 1]],
    edge: [['MeadowGrass', 22], ['Timothy', 10], ['Goldenrod', 12], ['Lupin', 9], ['WildRose', 7], ['QueenAnne', 5], ['BeachPea', 4], ['Bayberry', 2], ['OxeyeDaisy', 4]],
    weeds: [['MeadowGrass', 14], ['Timothy', 6], ['OxeyeDaisy', 3], ['Buttercup', 2]],
    bankFace: [['MeadowGrass', 16], ['Bayberry', 8], ['WildRose', 8], ['Goldenrod', 6], ['BeachPea', 5], ['CommonJuniper', 3], ['SaltmeadowHay', 4]],
    dune: [['Marram', 40], ['BeachPea', 8], ['SeaRocket', 4], ['WildRose', 3]],
    fringe: [['Cordgrass', 50], ['SaltmeadowHay', 20], ['BlackRush', 12], ['Glasswort', 6], ['Threesquare', 6]],
    glasswort: [['Glasswort', 10], ['Cordgrass', 3]], rockweed: [['KnottedWrack', 50], ['Bladderwrack', 36]], eelgrass: [['Eelgrass', 70]],
  };
  const DENS = { meadow: 0.42, edge: 0.34, weeds: 0.06, bankFace: 0.3, dune: 0.4, fringe: 0.5, glasswort: 0.1, rockweed: 0.14, eelgrass: 0.06 };
  const TALL = { Serviceberry: 0.95, BeakedHazelnut: 0.9, WildRaisin: 0.85, Bayberry: 0.7, Cattail: 0.45 }, MAT = { LowbushBlueberry: 0.55, CommonJuniper: 0.6, BeachPea: 0.4 };
  const SHRUB = new Set(['LowbushBlueberry', 'SheepLaurel', 'CommonJuniper', 'Raspberry', 'WildRose', 'Serviceberry', 'BeakedHazelnut', 'WildRaisin', 'Bayberry', 'SweetFern']);
  const WEED = new Set(['KnottedWrack', 'Bladderwrack', 'Eelgrass']), FLOWER = new Set(['Lupin', 'Fireweed', 'QueenAnne', 'Goldenrod', 'OxeyeDaisy', 'BlueFlag', 'Buttercup', 'BeachPea']);
  const plant = (key, x, y, rnd, o) => { const stage = (o && o.stage) || (rnd() < 0.3 ? 'young' : 'mature'), variant = Math.floor(rnd() * 4);
    return { id: 'plant.' + key, kind: 'plant', key, stage, variant, at: [x, y], rad: TALL[key] || MAT[key] || (SHRUB.has(key) ? 0.62 : 0.3), cat: WEED.has(key) ? 'weed' : SHRUB.has(key) ? 'shrub' : 'herb',
      family: WEED.has(key) ? 'weed' : SHRUB.has(key) ? 'shrub' : FLOWER.has(key) ? 'flower' : 'grass', waterDep: WEED.has(key), fkey: key + ':' + stage + ':' + variant, occM: 3 }; };
  function herbPick(c) { const h = habitat(c); if (!h) return null; let d = DENS[h], list = HERBS[h]; const x = c.x, y = c.y;
    if (h === 'meadow' || h === 'edge' || h === 'bankFace') { const Lu = vn(x / 6.5, y / 5, 101), Go = vn(x / 5.5, y / 4.5, 103);
      list = list.map(([k, w]) => [k, w * (k === 'Lupin' ? (Lu > 0.58 ? 7 : Lu < 0.4 ? 0.15 : 1) : k === 'Goldenrod' ? (Go > 0.62 ? 4 : 0.6) : 1)]); }
    if (c.rnd() > d) return null; const k = pickW(list, c.rnd()); if (root.PlantRig5 && root.PlantRig5.SPECIES && !root.PlantRig5.SPECIES[k] && root.PlantRig5.has && !root.PlantRig5.has(k)) return null; return plant(k, x, y, c.rnd); }
  const TUFTS = [['MeadowGrass', 6], ['Timothy', 2.5], ['OxeyeDaisy', 1], ['Buttercup', 1]], TUFT_H = new Set(['meadow', 'edge', 'bankFace']);
  function tuftPick(c) { const h = habitat(c); if (!TUFT_H.has(h) || c.rnd() > 0.28) return null; const p = plant(pickW(TUFTS, c.rnd()), c.x, c.y, c.rnd); p.rad = 0.22; return p; }
  const TRAD = { mature: 1.75, pole: 1.3, young: 0.95, sapling: 0.6 };
  const tree = (key, stage, x, y, variant) => ({ id: 'tree.' + key, kind: 'tree', family: 'tree', cat: 'tree', key, stage, variant: variant | 0, at: [x, y], rad: TRAD[stage], fkey: key + ':' + stage + ':' + (variant | 0) });
  const rock = (form, x, y, o) => { o = o || {}; const variant = o.variant | 0, mirror = !!o.mirror, stone = o.stone || 'granite', tide = o.tide || 'dry', dress = o.dress | 0, seat = o.seat || 'grass';
    return { id: 'rock.' + form, kind: 'rock', family: 'rock', cat: 'rock', form, variant, mirror, stone, tide, dress, seat, at: [x, y], rad: form === 'shelf' ? 1.5 : form === 'cobbles' ? 0.8 : 1.05, fkey: [form, variant, mirror ? 1 : 0, stone, tide, dress, seat].join(':') }; };
  const x2stone = (x, y) => (Math.min(nearSeg(L.armW, x, y).d, nearSeg(L.armE, x, y).d) < 12 ? 'granite' : 'sandstone');
  function rockPick(c) { const P = c.pano, r = c.rnd, z = c.zone, e = c.e, i = c.i; if ((P._paved && P._paved[i]) || P.CH[0][i] < 1.0) return null;
    let form, p = 0;
    if (z === 'shingle' || z === 'foreshore') { p = 0.04; form = pickW([['cobbles', 70], ['slab', 15], ['block', 10], ['erratic', 5]], r()); }
    else if (z === 'talus' && e > -1.5 && e < 3.2) { p = 0.05; form = pickW([['block', 45], ['cobbles', 30], ['slab', 25]], r()); }
    else if (z === 'rockweed') { p = 0.06; form = pickW([['cobbles', 50], ['knuckle', 20], ['slab', 15]], r()); }
    if (!form || r() > p) return null;
    const tide = e < 0.6 ? 'wet' : 'dry', dress = e < 0.2 ? 2 : e < 1.2 ? 1 : 0, seat = root.PxRockIso2 && root.PxRockIso2.SEATS && root.PxRockIso2.SEATS.indexOf(z) >= 0 ? z : 'grass';
    return rock(form, c.x, c.y, { variant: Math.floor(r() * 4), mirror: r() < 0.5, tide, dress, seat, stone: x2stone(c.x, c.y) }); }
  const SPACING = (a, b) => { if (a === 'piece' || b === 'piece') return 1; if (a === 'tree' && b === 'tree') return 1; if (a === 'tree' || b === 'tree') return 0.3;
    if (a === 'rock' && b === 'rock') return 0.85; if (a === 'rock' || b === 'rock') return 0.55; if (a === 'shrub' && b === 'shrub') return 0.85; if (a === 'shrub' || b === 'shrub') return 0.6;
    if (a === 'weed' || b === 'weed') return 0.5; return 0.72; };

  // =====================================================================================================================
  //  THE PIECES, AND THE LIGHTS
  // =====================================================================================================================
  // the crane's pose per board (the day's landing; stowed after dark): see NmcSetPieces.craneState
  const CRANE_POSE = { day: { state: 'working', slew: 27.5, reach: 5.41, hookZ: -1.06, load: 'totes' }, night: { state: 'stowed' } };   // day: two totes coming up out of Yvette's cockpit
  const LAMP = { warm: '#ffd6a0', sodium: '#ffc47a', cool: '#e4ecff', red: '#ff4a3c', green: '#4cff8a' };
  function pieces(P) {
    if (!H.west) defineHarbours();
    const out = [], g = (x, y) => { const i = P.texOfW(x, y); return i >= 0 ? P.E[i] : groundAt(x, y); };
    const add = (o) => { o.dir = o.dir | 0; if (o.z == null) o.z = g(o.at[0], o.at[1]); if (o.clear == null) o.clear = 0.6; if (!o.fkey) o.fkey = (o.kit || o.kind) + ':' + (o.piece || o.rig || '') + ':' + JSON.stringify(o.opts || {}) + ':' + o.dir; out.push(o); return o; };
    const lamp = (id, x, y, dz, r, I, c, z) => add({ id: 'light.nmc_' + id, kind: 'nmcLight', at: [x, y], z: z != null ? z : g(x, y), dir: 0, opts: {}, family: 'light', clear: 0, occM: 0, fkey: 'light',
      light: { pool: true, when: 'dusk', at: [0, 0, dz], reachM: r, intensity: I, colour: c } });
    // the harbours (sortY north of what stands on their decks)
    const hb = (id, harbour, OX, sortY, wh) => add({ id: 'harbour.' + id, kind: 'harbour', harbour, at: [OX, 0], z: -KZ, sortY, tideDep: true, family: 'wharf', clear: 0, fkey: 'harbour:' + harbour, wharf: wh, occM: 0 });
    hb('west', 'nmc_west', H.west.OX, 88.4, { x0: 38, len: 42, yc: 66, width: 44 });
    hb('fleet', 'nmc_fleet', H.fleet.OX, 96.4, { x0: 76, len: 88, yc: 88, width: 14 });
    hb('east', 'nmc_east', H.east.OX, 96.4, { x0: 160, len: 26, yc: 78, width: 22 });
    hb('yard', 'nmc_yard', H.yard.OX, 146.5, { x0: 182, len: 32, yc: 112, width: 70 });
    hb('armW', 'nmc_armW', H.armW.OX, 6.5, { x0: 34, len: 86, yc: 2, width: 10 });
    hb('armE', 'nmc_armE', H.armE.OX, 88.5, { x0: 150, len: 66, yc: 40, width: 96 });
    // the new pieces: the crane and its pad, the harbourmaster's office, the fish market, the restaurant, the lights
    // the crane at work by day (variant 'today'), stowed after dark (variant 'night'): the same piece in two of its poses
    add({ id: 'structure.nmc_wharf_crane', kind: 'nmcPiece', piece: 'wharfCrane', at: L.crane.at.slice(), z: DECK, dir: 4, opts: CRANE_POSE.day, family: 'set piece', shadowBox: 7, sortY: L.crane.at[1], occM: 60, variants: ['today'] });
    add({ id: 'structure.nmc_wharf_crane', kind: 'nmcPiece', piece: 'wharfCrane', at: L.crane.at.slice(), z: DECK, dir: 4, opts: CRANE_POSE.night, family: 'set piece', shadowBox: 7, sortY: L.crane.at[1], occM: 60, variants: ['night'] });
    add({ id: 'prop.nmc_crane_pad', kind: 'nmcPiece', piece: 'landingPad', at: L.crane.pad.slice(), z: DECK, dir: 4, opts: { size: L.crane.padSize }, family: 'set piece', shadowBox: 0, sortY: L.crane.pad[1] + 1.6, occM: 0, clear: 0 });
    add({ id: 'structure.nmc_harbourmaster', kind: 'nmcPiece', piece: 'officeTrailer', at: L.office.at.slice(), dir: 4, opts: { role: 'harbourmaster', text: 'HARBOURMASTER' }, family: 'set piece', shadowBox: 7, emits: true, occM: 60 });
    add({ id: 'structure.nmc_fish_market', kind: 'nmcPiece', piece: 'fishMarket', at: L.market.at.slice(), dir: 4, opts: {}, family: 'set piece', shadowBox: 7, emits: true, occM: 60 });
    add({ id: 'structure.nmc_restaurant', kind: 'nmcPiece', piece: 'restaurant', at: L.restaurant.at.slice(), dir: 0, opts: {}, family: 'set piece', shadowBox: 11, emits: true, occM: 120 });
    for (const k of ['west', 'east']) { const [x, y, c] = L.lights[k]; add({ id: 'structure.nmc_light_' + k, kind: 'nmcPiece', piece: 'harbourLight', at: [x, y], z: 3.6, dir: 4, opts: { colour: c }, family: 'set piece', shadowBox: 4, emits: true, occM: 40 });
      lamp('head_' + k, x, y - 1.2, 5.9, 7.5, 0.45, c === 'red' ? LAMP.red : LAMP.green, 3.6); }
    // the buildings: every boat her shed, behind her berth; the cold store at the crane apron; the bait shed and the trap store
    const B = (id, preset, x, y, dir, opts, sb) => add({ id: 'structure.nmc_' + id, kind: 'building', piece: preset, at: [x, y], dir, opts: Object.assign({ preset }, opts || {}), family: 'building', shadowBox: sb || 7, clear: 0, occM: 120 });
    for (const f of FLEET) if (f.shed) B('shed_' + f.id, f.shed[0], f.shed[1], 101.8, 4, { size: f.shed[2], weather: 0.55 + 0.1 * (f.berth % 3) });
    for (const f of FLOAT_FLEET) B('shed_' + f.id, f.shed[0], f.shed[1], 131.0, 4, { size: f.shed[2], weather: 0.6 });
    B('bait_shed', 'iceHouse', 136.5, 131.2, 4, { weather: 0.7 }); B('trap_store', 'gambrelBarn', 152.0, 131.4, 4, { size: 0.9, weather: 0.6 });
    B('cold_store', 'fishPlant', 64.0, 99.2, 4, { size: 0.9, boom: false, weather: 0.35, occupancy: 'working' }, 12);
    // the fleet's small craft at the working float (their own rigs' render; the float's south face)
    let fx = H.west.fl.at[0] + H.west.OX - 10.8;
    for (const f of FLOAT_FLEET) { const cx = fx + f.loa / 2, cy = H.west.floatSouth - 0.25 - f.beam / 2;
      add({ id: 'boat.nmc_' + f.id, kind: 'flat', rig: f.rig, at: [cx, cy], dir: 2, opts: f.opts, float: f.float, family: 'boat', sortY: cy, clear: 0, occM: 20 }); fx += f.loa + 1.4; }
    // the road fleet: the buyer's truck by the market and the parking, a reefer coming round the circle, pickups at the berths and in the lot
    const V = (id, rig, x, y, dir, opts) => add({ id: 'vehicle.nmc_' + id, kind: 'flat', rig, at: [x, y], dir, opts: opts || {}, family: 'vehicle', clear: 1.4, occM: 40 });
    for (const [id, rig, x, y, dir] of L.cars) V(id, rig, x, y, dir, {}); V('pickup_leo', 'VehicleIso', 99.5, 91.2, 2, {}); V('pickup_ross', 'VehicleIso', 141.5, 91.6, 6, {}); V('pickup_marie', 'VehicleIso', 121.0, 91.0, 2, {});
    V('ramp_pickup', 'VehicleIso', 47.2, 95.2, 0, {});
    V('reefer_trailer', 'TrailerIso', 56.0, 133.0, 6, { body: 'reefer53' }); V('reefer_tractor', 'AeroSemiIso', 65.575, 133.0, 6, {});
    // the fuel stop's own dressing where the wharf needs it (pass 6 rig): the dumpster, the picnic table and the ice box, the lot lights
    const FS = (id, piece, x, y, dir, sb) => add({ id: 'prop.nmc_' + id, kind: 'fuel', piece, at: [x, y], dir: dir == null ? 4 : dir, opts: {}, family: 'station', cat: 'piece', shadowBox: sb || 3, clear: 0.6, occM: 14, fkey: 'fuel:' + piece + ':{}:' + (dir == null ? 4 : dir) });
    if (root.FuelStop) { FS('dumpster', 'dumpster', 92.2, 133.8, 4, 3.5); FS('picnic', 'picnic', 93.9, 112.4, 4); FS('ice', 'iceBox', 79.4, 111.8, 4); FS('bin', 'bin', 92.8, 109.2, 4); FS('picnic_r', 'picnic', 157.2, 142.8, 4);
      for (const [id, x, y, c] of [['court', 44.6, 108.6, LAMP.sodium], ['lot', 124.0, 107.6, LAMP.cool], ['quay_w', 78.4, 95.8, LAMP.sodium], ['quay_e', 147.8, 95.6, LAMP.sodium], ['ramp', 48.6, 88.6, LAMP.sodium], ['lot_e', 164.0, 107.4, LAMP.cool]]) {
        FS('light_' + id, 'lampPole', x, y, 4, 8); lamp('pole_' + id, x, y - 1.3, 6.6, 10.5, 0.72, c); } }
    // the lamps of the pieces themselves (their faces glow with the lamp need; these are the pools they throw)
    lamp('office_porch', 85.35, 114.4, 2.5, 3.6, 0.8, LAMP.warm); lamp('office_counter', 83.6, 110.9, 1.6, 2.8, 0.55, LAMP.warm);
    lamp('market_door', 89.65, 124.6, 2.3, 3.4, 0.8, LAMP.warm); lamp('market_window', 92.3, 124.4, 1.4, 3.0, 0.5, LAMP.warm);
    lamp('rest_porch', 168.45, 130.8, 2.4, 3.8, 0.85, LAMP.warm); lamp('rest_takeout', 164.55, 130.9, 1.6, 3.4, 0.6, LAMP.warm); lamp('rest_name', 168.0, 130.7, 4.0, 5.0, 0.4, LAMP.warm);
    for (const [x, y] of [[177.6, 134.5], [177.6, 138.7], [178.4, 143.3], [171.0, 143.3], [163.0, 143.3]]) lamp('rest_deck_' + x, x, y, 3.2, 4.2, 0.5, '#ffc98a', DECK + 0.55);
    lamp('cold_dock', 64.0, 108.6, 3.6, 7.5, 0.7, LAMP.cool); lamp('yard_flood_w', 186.0, 96.8, 7.6, 11, 0.7, LAMP.cool); lamp('yard_flood_e', 210.0, 123.2, 7.6, 11, 0.65, LAMP.cool);
    for (const [x, y] of H.east.lights) lamp('dock_' + x, x, y, 1.2, 3.2, 0.55, LAMP.warm, 0.5);
    // the traps ashore for the summer: stacks higher than you, in blocks by the court, by the boat yard, by the lot
    const TR = (id, x0, y0, nx, ny, v) => { for (let j = 0; j < ny; j++) for (let i = 0; i < nx; i++) add({ id: 'prop.nmc_traps_' + id + '_' + j + '_' + i, kind: 'decor', kit: 'wharfDecor', piece: 'trapStack', at: [x0 + i * 1.32, y0 - j * 1.12], dir: 4, opts: { variant: (i * 3 + j + v) % 4, weather: 0.45 + 0.1 * ((i + j) % 3) }, family: 'dressing', clear: 0.3, sortY: y0 - j * 1.12 }); };
    TR('court', 82.0, 144.0, 9, 2, 0); TR('yardW', 174.8, 110.5, 4, 5, 2); TR('lot', 106.4, 109.6, 2, 2, 3); TR('ramp', 43.6, 100.8, 2, 2, 1);
    // gear: traps between the sheds and on the quay, totes at the crane and on its pad, buoys, the damaged dory for sale in the yard
    const D = (id, piece, x, y, dir, opts) => add({ id: 'prop.nmc_' + id, kind: 'decor', kit: 'wharfDecor', piece, at: [x, y], dir: dir == null ? 4 : dir, opts: opts || {}, family: 'dressing', clear: 0.5 });
    [[94.6, 99.0], [95.3, 100.4], [108.0, 99.2], [108.7, 100.6], [124.2, 99.0], [139.6, 99.4], [140.3, 100.8], [154.4, 99.2], [165.2, 99.6], [165.9, 101.0], [110.2, 99.4], [110.9, 100.8]].forEach(([x, y], k) => D('traps_' + k, 'trapStack', x, y, 4, { variant: k % 4, weather: 0.5 }));
    [[84.0, 94.2], [127.5, 94.6], [150.5, 94.4]].forEach(([x, y], k) => D('quay_traps_' + k, 'trapStack', x, y, 4, { variant: (k + 2) % 4 }));
    [[74.6, 84.6], [76.0, 84.9], [58.5, 85.4], [63.6, 83.2]].forEach(([x, y], k) => D('totes_' + k, 'toteStack', x, y, 4, { variant: k % 3 }));
    D('buoys_leo', 'buoyRack', 104.0, 97.6, 4); D('buoys_marie', 'buoyRack', 119.4, 97.6, 4); D('nets_a', 'netPile', 113.0, 98.2, 4); D('ice_crane', 'iceChest', 62.0, 86.2, 4); D('baskets', 'bushelBaskets', 134.4, 98.2, 4);
    D('barrow', 'wheelbarrow', 146.4, 98.6, 5); D('ring_w', 'ringStation', 80.6, 87.0, 4); D('ring_e', 'ringStation', 158.8, 87.0, 4); D('ring_ramp', 'ringStation', 47.6, 86.8, 4); D('notice', 'noticeBoard', 81.2, 110.2, 4);
    add({ id: 'boat.nmc_damaged_dory', kind: 'decor', kit: 'dory', at: [179.6, 126.4], dir: 2, opts: { build: 'motor' }, family: 'boat', clear: 1.0, z: 3.42 });
    // gulls
    const GL = (id, x, y, dz, dir) => add({ id: 'gull.nmc_' + id, kind: 'decor', kit: 'seagull', piece: 'perch', at: [x, y], z: g(x, y) + dz, dir, opts: { anim: 'perch', frame: 0 }, family: 'gull', clear: 0.3, fkey: 'gull:' + dir });
    GL('crane', 72.0, 78.5, 6.9, 5); GL('shed', 116.2, 104.4, 5.3, 3); GL('light', 158.6, -5.4, 5.9, 4); GL('arm', 60.0, 1.6, 0.6, 2); GL('flats', 96.0, 141.4, 0.2, 6); GL('restaurant', 166.0, 136.1, 6.55, 3); GL('market', 91.6, 127.6, 4.15, 5);
    return out;
  }
  function fixedFlora() {
    const T = (key, stage, x, y, v) => tree(key, stage, x, y, v);
    return [T('WhiteSpruce', 'mature', 44.5, 138.5, 0), T('BalsamFir', 'young', 43.2, 132.6, 1), T('WhiteBirch', 'pole', 44.8, 102.8, 0),
      T('WhiteSpruce', 'pole', 29.8, 52.0, 1), T('BalsamFir', 'young', 30.2, 30.5, 0), T('WhiteBirch', 'young', 30.6, 74.0, 1), T('WhiteSpruce', 'young', 29.6, 12.0, 0)].filter(t => !root.TreeRig4 || !root.TreeRig4.SPECIES || root.TreeRig4.SPECIES[t.key]);
  }
  (function () { const pad = (x0, x1, y0, y1) => BUILT.push([x0, x1, y0, y1]);
    for (const f of FLEET) if (f.shed) pad(f.shed[1] - 5.5, f.shed[1] + 5.5, 96, 108); for (const f of FLOAT_FLEET) pad(f.shed[1] - 4, f.shed[1] + 4, 126, 137);
    pad(130, 158, 126, 137); pad(52, 77, 90, 108); pad(80, 104, 107, 116); pad(87, 107, 123.5, 135.5); pad(141, 147, 123.5, 133); pad(182, 212, 96, 138); pad(44, 100, 103, 127); pad(158, 182, 100, 124);
    pad(50, 78, 76, 88.4); pad(78, 212, 86, 96.4); pad(46, 80, 108, 142); pad(79, 94, 95, 116); pad(158, 181, 117.5, 146.5); pad(40, 49, 44, 100); })();

  // ---- places (labels): ground point, metres above the ground --------------------------------------------------------
  const PLACES = [
    { id: 'crane', label: 'The crane', at: [72.0, 78.2, 7.2] }, { id: 'pad', label: 'The crane\u2019s landing pad', at: [67.6, 81.2, 0.4] }, { id: 'unload', label: 'The unloading berth', at: [66.5, 73.2, 2.4] },
    { id: 'cold', label: 'Cold storage', at: [64, 99.2, 7] }, { id: 'court', label: 'The turning circle', at: [62, 125, 0.2] }, { id: 'plot', label: 'Berth 0\u2019s lot, to build on', at: [86.5, 101.7, 0.3] },
    { id: 'fingerA', label: 'Finger A', at: [109.7, 57, 0.8] }, { id: 'fingerB', label: 'Finger B', at: [142.3, 57, 0.8] }, { id: 'float', label: 'The working float', at: [60, 60, 0.6] },
    { id: 'ramp', label: 'The boat ramp', at: [44, 64, 0.6] }, { id: 'fleet', label: 'The fleet at the wall', at: [120, 84, 3.4] }, { id: 'sheds', label: 'A shed for every boat', at: [131, 101.8, 6.5] },
    { id: 'office', label: 'The harbourmaster', at: [86.4, 112.6, 4.2] }, { id: 'market', label: 'The fish market', at: [91.2, 127.6, 4.6] }, { id: 'buyer', label: 'The buyer\u2019s truck', at: [98.2, 128.4, 2.6] }, { id: 'road', label: 'The main road, to the boat yard', at: [134, 120, 0.2] },
    { id: 'parking', label: 'Parking', at: [104.6, 131.6, 0.2] }, { id: 'turn', label: 'The truck turn', at: [168.5, 121.2, 0.2] }, { id: 'restaurant', label: 'The restaurant', at: [168, 136, 7.4] }, { id: 'patio', label: 'The rear patio, over the water', at: [168, 147.5, 3.2] }, { id: 'rec', label: 'The rec float', at: [176, 69, 1.2] },
    { id: 'yard', label: 'Harris & Sons boat yard', at: [199, 112, 5] }, { id: 'lift', label: 'The lift slip', at: [198, 76, 7.4] }, { id: 'basin', label: 'Clear water: 30 m', at: [128, 28, 0.2] },
    { id: 'mouth', label: 'The mouth', at: [137, -3, 0.2] }, { id: 'lightW', label: 'Green light', at: L.lights.west.slice(0, 2).concat([9]) }, { id: 'lightE', label: 'Red light', at: L.lights.east.slice(0, 2).concat([9]) },
    { id: 'player', label: 'Berth 0, kept for you', at: [86.2, 83.4, 0.4] }, { id: 'road', label: 'Wharf Road, to the town', at: [44, 118, 0.2] }, { id: 'path', label: 'The shore path, from the landing', at: [34, 20, 0.2] },
    { id: 'creek', label: 'Nine Mile Creek', at: [120, 147, 0.2] },
  ];
  const FRAME = { x0: 28, x1: 214, uTop: 104, uBot: -14, padBelow: 8, yMin: -34, yMax: 172 };
  const FRAMES = [
    { id: 'wharf.wide', kind: 'wide', centre: [121, 69.984], z: 0, size: [186, 118], note: 'the whole wharf from the south: the arms and the mouth in front, the basin, the fleet, the yard, the restaurant and the boat yard' },
    { id: 'wharf.wide_arrival', kind: 'wide', centre: [50, 16], z: 2, size: [32, 18], note: 'the establishing view as the shore path tops the bank at the west arm\u2019s root: the camera eased out to twice the walk (a proposal), across the basin' },
    { id: 'wharf.of_arrival', kind: 'on-foot', centre: [40, 10], z: 5, size: [16, 9], note: 'on foot, off the shore path at the west arm\u2019s root' },
    { id: 'wharf.of_harbourmaster', kind: 'on-foot', centre: [86, 112], z: 3.4, size: [16, 9], note: 'the harbourmaster\u2019s counter: where the cod licence is bought' },
    { id: 'wharf.of_yard', kind: 'on-foot', centre: [95.5, 124], z: 3.4, size: [16, 9], note: 'the yard: the fish market off the main road, the buyer\u2019s truck in the parking beside it' },
    { id: 'wharf.of_crane', kind: 'on-foot', centre: [70.5, 76], z: 1.2, size: [16, 9], note: 'the crane at work: two totes coming up out of Yvette\u2019s cockpit, the pad behind, the ladders' },
    { id: 'wharf.of_float', kind: 'on-foot', centre: [54, 59], z: 0.5, size: [16, 9], note: 'the working float, its gangway, the small craft and the boat ramp beside it' },
  ];
  const boxOf = (f) => { const u = 0.643 * f.centre[1] + 0.766 * f.z; return { x: Math.round((f.centre[0] - f.size[0] / 2 - FRAME.x0) * PPM), y: Math.round((FRAME.uTop - u - f.size[1] / 2) * PPM), w: Math.round(f.size[0] * PPM), h: Math.round(f.size[1] * PPM) }; };

  root.NmcWharf = { id: 'nmc_wharf', name: 'Nine Mile Creek: the wharf', frame: FRAME, seed: 83, relief: 2.4, channels: 2, nearest: {},
    fetch: () => 0.22, zones: ZONES, sample, zone, groundAt, cliffs: null, pieces, fixedFlora, spacing: SPACING, places: PLACES, afterPaint, FRAMES, boxOf,
    scatter: [{ name: 'rocks', pitch: 1.4, pick: rockPick }, { name: 'plants', pitch: 0.62, pick: herbPick }, { name: 'tufts', pitch: 0.55, pick: tuftPick }],
    L, FLEET, FLOAT_FLEET, REC, FINGER_FLEET, DESIGN, TRACK, curve, BUILT, H, LADDERS, WALL_BERTHS, CRANE_BERTHS, CRANE_POSE, LAMP, defineHarbours, seabed, yardD, yardZ, armCore, kitBed, rampZ, creekY, inHarbour, NETS };
})(typeof globalThis !== 'undefined' ? globalThis : this);
