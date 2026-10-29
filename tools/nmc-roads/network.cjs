// NMC roads (H4): Nine Mile Creek's road network on G3, by id from G3's plan, in road kit v4's classes.
//   node tools/nmc-roads/network.cjs           write the network JSON and print its summary
//   node tools/nmc-roads/network.cjs --check   rebuild it in memory and fail unless the committed file is byte for byte
// Node built-ins only. Every route is the plan's own line, clipped only where a deck or the wharf takes over; every
// class, width, surface and band is the plan's, resolved against the kit's CLASSES (a G3 band string names the kit
// band of that role and overrides its width and surface; a route without bands keeps its class's). The bridges are
// fitted to G3 (their seats on dry ground SEAT metres past the water each side, the deck at the higher seat), and the
// no-ford check refuses a route that stands in water outside a deck. The owner's rulings of 10-24 stand in it too: the
// measured corner flares of flares.json on their roads (Q4), the truck park spur's widened gate (Q5), the planting in
// the ruled routes' swept bodies (Q6), the river lane off the vehicle routes (Q8), the camp brook deck's measured seat
// pads (Q10), one row of stalls a bay (Q12) and the held walks' owners (Q15).
'use strict';
const fs = require('fs'), path = require('path'), crypto = require('crypto'), vm = require('vm');
const planLib = require('./plan.cjs'), groundLib = require('./ground.cjs');

const ROOT = path.resolve(__dirname, '../..');
const KIT = 'docs/art/rigs/road-path-kit-v4';
const KIT_LIBS = ['lib/pixelLanguage.js', 'lib/pxKit.js', 'lib/pxKit2.js', 'lib/weatherSky.js', 'lib/terrainLight4.js', 'lib/pxKit8.js', 'lib/terrainLight5.js', 'roadPathRig4.js'];
const OUT = 'Assets/_Project/Data/Terrain/NineMileCreekRoads/NineMileCreekRoadNetwork.json';
const SCHEMA = 'hidden-harbours/nmc-road-network@1';
const MAINLAND = 'Assets/_Project/Code/App/Editor/NineMileCreekMainland.cs';

// The fit's tunables, written into the file beside what they made.
const TUNABLES = {
  seatMetres: 0.5,        // a deck's seat on dry ground past the water's edge, each end
  sampleMetres: 0.25,     // along a line, and across a carriageway
  joinSlackMetres: 0.25,  // an end within the other line's half width plus this is a junction
  approachMetres: 5,      // a deck's approach, measured for its steepest one-metre grade
  seatToleranceMetres: 0.05, // a deck on its own axis runs on until its bank stands within this of the deck,
  seatBearMetres: 0.1,       // and this beyond, rounded up to the decimetre
  seatReachMetres: 2,        // and no further: a bank that never comes up is a grade, not a seat
};
const EDGE = { west: -380, east: 380, south: -280, north: 280 };
// the base charter's H4: "Supported bridge prefabs, no road ford: Route 91 36 m/deck +6, pond culvert, camp brook deck +5.2"
const BASE_CAMP_BROOK_DECK = 5.2;
// the base charter's H4 "Playground clears Loop A/C" and H12's "Playground at H4 clear site": the plan's (-14, 22) stood
// 1.8 m into Loop A's carriageway and on pitch A1. H4's site is the plan's 14 x 10 moved 16.5 m west, between Loop A's
// west leg and Loop C's stem: chosen, not derived, for a metre over the lot rule's 3 m off the camp roads and 4 m off
// pitch A1, on the level +6 plateau inside the outline. The planner's amendment 2 (10-24) puts the plan's spot here and
// re-sites the pitches round it: a29, the nearest, stands 3 m off. This file measures it; the test holds the measures
const PLAYGROUND_SITE = { id: 'structure.nmc_camp_playground', at: [-30, 18], size: [14, 10] };
// where a route meets ground this network does not lay
const OUTSIDE = [
  { at: [40, 118], what: 'the wharf\'s stub of Wharf Road and its turning pad (step 06, kit v3)' },
  { at: [58, -146], what: 'the bar (path.nmc_bar_walk, bare)' },
];

// ---- the owner's rulings of 10-24 on H4's Phase A report ---------------------------------------------------------------
// Q4 "Lay the measured corner flares": fitted from the swept measure's wheels into FLARES, each on the road it widens.
// A flare takes that road's class, surface and bands, so its kerb, walk, shoulder and edge line follow the flare; a
// lane's is a street's (a lane draws a crown and ruts along its line). Its line stands at most FLARE_LINE_PAST past
// the road's carriage edge, so the kit's junction rule (a second line of the group within 1.2 m) keeps the class's
// centre marks off it, and its carriage reaches FLARE_OVERLAP into the road's at least
const FLARES = 'tools/nmc-roads/flares.json';
const FLARE_SCHEMA = 'hidden-harbours/nmc-road-flares@1';
const FLARE_LINE_PAST_METRES = 1.0, FLARE_OVERLAP_METRES = 0.1;
// Q5: the truck park spur's gate clearance in Wharf Road's south hedge, for the 53-foot pairs; the gate stays undressed
const SPUR_GATE = { id: 'drive.nmc_truck_park_spur', x: [-135.1, -124.9] };
// Q8: the river lane stays as drawn, off the vehicle routes
const OFF_VEHICLES = { 'road.nmc_stream_lane': 'until a vehicle grade limit exists: it climbs 41.7% and its bodies cross into the boat shed lot (the owner, 10-24, Q8)' };
// Q6: the planting pieces a ruled route's swept body hits, each its root, name and place in the scene; step 12
// deletes them (the laydown's follow Q2 and the gas bar lane's Q3, so they stay)
const SWEPT = [
  { root: 'CreekTrees', name: 'RedSpruce_2', at: [-194.03105, 162.35143], by: 'junction road.nmc_route_91 (south of the river bridge) reversed into road.nmc_mill_road' },
  { root: 'CreekHedgerows', name: 'SweetGale_2', at: [-180.3476, 276.19177], by: 'junction road.nmc_route_91_town (the town section, north of the river bridge) forward into road.nmc_north_creek_lane' },
];

const r3 = (v) => Math.round(v * 1000) / 1000;
const sha = (b) => crypto.createHash('sha256').update(b).digest('hex');
const lfSha = (file) => sha(Buffer.from(fs.readFileSync(path.join(ROOT, file), 'utf8').replace(/\r\n/g, '\n'), 'utf8'));

// ---------------------------------------------------------------------------------------------------------------
//  Lines
// ---------------------------------------------------------------------------------------------------------------
function lengthOf(pts) { let L = 0; for (let i = 1; i < pts.length; i++) L += Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]); return L; }
function pointAt(pts, s) {
  for (let i = 1; i < pts.length; i++) {
    const L = Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]);
    if (s <= L || i === pts.length - 1) { const t = L ? Math.max(0, Math.min(1, s / L)) : 0; return [pts[i - 1][0] + (pts[i][0] - pts[i - 1][0]) * t, pts[i - 1][1] + (pts[i][1] - pts[i - 1][1]) * t, (pts[i][0] - pts[i - 1][0]) / (L || 1), (pts[i][1] - pts[i - 1][1]) / (L || 1)]; }
    s -= L;
  }
  return [pts[0][0], pts[0][1], 1, 0];
}
// the arc length of the point on the line nearest p, and how far off it is
function project(pts, p) {
  let best = { s: 0, d: Infinity, at: pts[0] }, s0 = 0;
  for (let i = 1; i < pts.length; i++) {
    const [ax, ay] = pts[i - 1], [bx, by] = pts[i], L = Math.hypot(bx - ax, by - ay);
    const t = L ? Math.max(0, Math.min(1, ((p[0] - ax) * (bx - ax) + (p[1] - ay) * (by - ay)) / (L * L))) : 0;
    const x = ax + (bx - ax) * t, y = ay + (by - ay) * t, d = Math.hypot(p[0] - x, p[1] - y);
    if (d < best.d) best = { s: s0 + t * L, d, at: [x, y] };
    s0 += L;
  }
  return best;
}
// how far apart two carriageways stand at a junction: r's last metre at its end (with its cap where the end is round)
// against q's last segment, up to two metres of it, at its flat end s; each a quad across the carriageway, measured
// exactly (two convex shapes apart are nearest at a vertex of one against an edge of the other)
function footprintGap(r, which, q, s) {
  const quad = (route, s0, s1) => {
    const [ax, ay, aux, auy] = pointAt(route.pts, s0), [bx, by, bux, buy] = pointAt(route.pts, s1), h = route.w / 2;
    return [[ax - auy * h, ay + aux * h], [bx - buy * h, by + bux * h], [bx + buy * h, by - bux * h], [ax + auy * h, ay - aux * h]];
  };
  const Lr = lengthOf(r.pts), Lq = lengthOf(q.pts);
  const lastSegQ = s <= 1e-9 ? Math.hypot(q.pts[1][0] - q.pts[0][0], q.pts[1][1] - q.pts[0][1])
    : Math.hypot(q.pts[q.pts.length - 1][0] - q.pts[q.pts.length - 2][0], q.pts[q.pts.length - 1][1] - q.pts[q.pts.length - 2][1]);
  const reachQ = Math.min(2, lastSegQ, Lq), reachR = Math.min(1, Lr);
  const B = s <= 1e-9 ? quad(q, 0, reachQ) : quad(q, Lq - reachQ, Lq);
  const A = which === 'start' ? quad(r, 0, reachR) : quad(r, Lr - reachR, Lr);
  const segD = (p, u, v) => {
    const dx = v[0] - u[0], dy = v[1] - u[1], L2 = dx * dx + dy * dy;
    const t = L2 ? Math.max(0, Math.min(1, ((p[0] - u[0]) * dx + (p[1] - u[1]) * dy) / L2)) : 0;
    return Math.hypot(p[0] - u[0] - dx * t, p[1] - u[1] - dy * t);
  };
  const inside = (p, P) => { let sgn = 0; for (let i = 0; i < 4; i++) { const u = P[i], v = P[(i + 1) % 4], c = (v[0] - u[0]) * (p[1] - u[1]) - (v[1] - u[1]) * (p[0] - u[0]); if (Math.abs(c) < 1e-12) continue; if (sgn && Math.sign(c) !== sgn) return false; sgn = Math.sign(c); } return true; };
  const toPoly = (p, P) => { if (inside(p, P)) return 0; let m = Infinity; for (let i = 0; i < 4; i++) m = Math.min(m, segD(p, P[i], P[(i + 1) % 4])); return m; };
  let best = Infinity;
  for (const p of A) best = Math.min(best, toPoly(p, B));
  for (const p of B) best = Math.min(best, toPoly(p, A));
  for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++) {
    const [p1, p2, p3, p4] = [A[i], A[(i + 1) % 4], B[j], B[(j + 1) % 4]];
    const d1 = (p4[0] - p3[0]) * (p1[1] - p3[1]) - (p4[1] - p3[1]) * (p1[0] - p3[0]), d2 = (p4[0] - p3[0]) * (p2[1] - p3[1]) - (p4[1] - p3[1]) * (p2[0] - p3[0]);
    const d3 = (p2[0] - p1[0]) * (p3[1] - p1[1]) - (p2[1] - p1[1]) * (p3[0] - p1[0]), d4 = (p2[0] - p1[0]) * (p4[1] - p1[1]) - (p2[1] - p1[1]) * (p4[0] - p1[0]);
    if (d1 * d2 < 0 && d3 * d4 < 0) best = 0;
  }
  if (r.ends !== 'flat') {
    const e = which === 'start' ? r.pts[0] : r.pts[r.pts.length - 1];
    best = Math.min(best, Math.max(0, toPoly(e, B) - r.w / 2));
  }
  return best;
}
// the part of a line between two arc lengths, its own vertices kept
function clip(pts, a, b) {
  const out = [pointAt(pts, a).slice(0, 2)]; let s0 = 0;
  for (let i = 1; i < pts.length; i++) {
    s0 += Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]);
    if (s0 > a + 1e-6 && s0 < b - 1e-6) out.push(pts[i].slice(0, 2));
  }
  out.push(pointAt(pts, b).slice(0, 2));
  return out;
}
function stations(pts, step) { const L = lengthOf(pts), n = Math.max(1, Math.ceil(L / step)), out = []; for (let k = 0; k <= n; k++) out.push(Math.min(L, k * step)); return out; }
// the arc-length runs where test(x, y) holds across a carriageway of half width hw
function runs(pts, hw, test, step) {
  const out = []; let open = null;
  for (const s of stations(pts, step)) {
    const [x, y, ux, uy] = pointAt(pts, s); let hit = false;
    for (let d = -hw; d <= hw + 1e-9 && !hit; d += step) hit = test(x - uy * d, y + ux * d);
    if (hit && !open) open = [s, s]; else if (hit) open[1] = s; else if (open) { out.push(open); open = null; }
  }
  if (open) out.push(open);
  return out;
}

// ---------------------------------------------------------------------------------------------------------------
//  The kit, the plan, the ground
// ---------------------------------------------------------------------------------------------------------------
function loadKit() {
  for (const f of KIT_LIBS) vm.runInThisContext(fs.readFileSync(path.join(ROOT, KIT, f), 'utf8'), { filename: f });
  return globalThis.RoadKit4;
}
function springHighWater() {
  const src = fs.readFileSync(path.join(ROOT, MAINLAND), 'utf8');
  const mean = +src.match(/const float TideMean = ([-\d.]+)f;/)[1], amp = +src.match(/const float TideAmplitude = ([-\d.]+)f;/)[1];
  return mean + amp;
}

// G3's band strings ("gutter 0.3", "walk 2.6 brick") as the kit's band objects of the same role
function bandsOf(RK, cls, strings) {
  if (!strings) return undefined;
  const C = RK.CLASSES[cls];
  return strings.map((s) => {
    const [role, w, surf] = s.split(' ');
    const base = C.bands.find((b) => b.role === role);
    if (!base) throw new Error(`class ${cls} has no ${role} band (${s})`);
    const b = Object.assign({}, base, { w: +w });
    if (surf) { if (!RK.SURF[surf]) throw new Error(`the kit has no surface ${surf} (${s})`); b.surf = surf; }
    return b;
  });
}

function routeOf(RK, o, from, extra = {}) {
  const cls = extra.cls || o.cls;
  if (!RK.CLASSES[cls]) throw new Error(`${o.id}: the kit has no class ${cls}`);
  const r = { id: extra.id || o.id };
  if (extra.part) r.part = extra.part;
  r.from = from;
  r.cls = cls;
  r.w = +(extra.w || o.width || RK.CLASSES[cls].w);
  const surf = extra.surf !== undefined ? extra.surf : o.surface;
  if (surf) { if (!RK.SURF[surf]) throw new Error(`${o.id}: the kit has no surface ${surf}`); if (surf !== RK.CLASSES[cls].surf) r.surf = surf; }
  const bands = extra.bands !== undefined ? extra.bands : bandsOf(RK, cls, o.bands);
  if (bands) r.bands = bands;
  const ends = extra.ends !== undefined ? extra.ends : (typeof o.ends === 'string' ? o.ends : undefined);
  if (ends) r.ends = ends;
  if (OFF_VEHICLES[r.id]) { r.vehicles = false; r.vehiclesWhy = OFF_VEHICLES[r.id]; }
  if (extra.clip) r.clip = extra.clip;
  if (extra.stalls) r.stalls = extra.stalls;
  r.pts = (extra.pts || o.pts).map((p) => [r3(p[0]), r3(p[1])]);
  return r;
}

// ---------------------------------------------------------------------------------------------------------------
//  Bridges: fit a deck to G3 on its route
// ---------------------------------------------------------------------------------------------------------------
function fitDeck(G, high, b, line, hw, opts = {}) {
  const T = TUNABLES, near = project(line, b.centre);
  const wet = runs(line, hw, (x, y) => G.wet(x, y, high), T.sampleMetres)
    .filter((r) => r[1] >= near.s - b.span && r[0] <= near.s + b.span);
  if (!wet.length) throw new Error(`${b.id}: no water under its route near ${b.centre}`);
  const w0 = Math.min(...wet.map((r) => r[0])), w1 = Math.max(...wet.map((r) => r[1]));
  const planA = near.s - b.span / 2, planB = near.s + b.span / 2;
  let sA, sB, how;
  if (opts.deck != null) {
    // a deck whose height the base fixes: each seat where G3's bank first reaches the deck, outward from the water
    const bank = (s, dir) => { for (let d = 0; d < 4 * b.span; d += T.sampleMetres) { const [x, y] = pointAt(line, s + dir * d); if (G.at(x, y) >= opts.deck) return s + dir * d; } throw new Error(`${b.id}: no bank at +${opts.deck}`); };
    sA = bank(w0, -1); sB = bank(w1, +1);
    how = `seats where G3's banks reach the base's +${opts.deck} m deck (the plan's ${b.span} m span is schematic)`;
  } else if (opts.abutments) {
    const s0 = project(line, opts.abutments[0]).s, s1 = project(line, opts.abutments[1]).s;
    sA = Math.min(s0, s1); sB = Math.max(s0, s1);
    how = 'the plan\'s abutments';
  } else if (planA <= w0 - T.seatMetres && planB >= w1 + T.seatMetres) {
    sA = planA; sB = planB; how = 'the plan\'s span, as drawn';
  } else {
    const span = Math.max(b.span, Math.ceil((w1 - w0 + 2 * T.seatMetres) * 2) / 2), mid = (w0 + w1) / 2;
    sA = mid - span / 2; sB = mid + span / 2;
    how = span > b.span ? `lengthened ${b.span} -> ${span} m and centred on the water` : `the plan's ${b.span} m span, centred on the water`;
  }
  const [ax, ay] = pointAt(line, sA), [bx, by] = pointAt(line, sB);
  // the ground a deck end bears on: the highest under its seat, from the seat point SEAT metres back onto the approach
  const seatZ = (s, dir) => { let z = -Infinity; for (let d = 0; d <= T.seatMetres + 1e-9; d += T.sampleMetres) { const [x, y] = pointAt(line, s + dir * d); z = Math.max(z, G.at(x, y)); } return z; };
  const zA = seatZ(sA, -1), zB = seatZ(sB, +1);
  const deck = opts.deck != null ? opts.deck : opts.abutments ? b.deck : Math.max(zA, zB);
  let stillTop = -Infinity, bed = Infinity;
  for (const s of stations(clip(line, sA, sB), T.sampleMetres)) {
    const [x, y] = pointAt(clip(line, sA, sB), s), z = G.at(x, y), w = G.stillZ(x, y);
    bed = Math.min(bed, z); if (!isNaN(w) && w > z) stillTop = Math.max(stillTop, w);
  }
  const water = Math.max(stillTop, high);
  // each approach's steepest grade onto the deck: one-metre windows over APPROACH metres, from behind the seat
  // (the seat itself bears the deck's end: a bench face under it is not the approach)
  const steepest = (s, dir) => {
    let g = 0;
    for (let d = T.seatMetres; d + 1 <= T.seatMetres + T.approachMetres + 1e-9; d += T.sampleMetres) {
      const [x0, y0] = pointAt(line, s + dir * d), [x1, y1] = pointAt(line, s + dir * (d + 1));
      g = Math.max(g, Math.abs(G.at(x1, y1) - G.at(x0, y0)));
    }
    return g * 100;
  };
  // the footprint: the axis-aligned box over the carriageway the deck carries (StandablePlatform takes a Rect)
  const half = (opts.deckWidth || 2 * hw) / 2, corners = [];
  let seatPad = [0, 0];
  if (opts.abutments) {
    // a deck the plan draws on its own axis: its centre line and width, abutment to abutment, each end run on over
    // its bank until G3 under the carriage (both edges and the centre) stands within SEAT_TOLERANCE of the deck, and
    // SEAT_BEAR beyond: the bench a plan abutment stands on can fall short of it, and a wheel would drop off the end
    const ys0 = [Math.min(opts.abutments[0][1], opts.abutments[1][1]), Math.max(opts.abutments[0][1], opts.abutments[1][1])];
    const reach = (y0, dir) => {
      for (let d = 0; d <= T.seatReachMetres + 1e-9; d += 0.05) {
        if ([-hw, 0, hw].every((o) => G.at(b.centre[0] + o, y0 + dir * d) >= b.deck - T.seatToleranceMetres)) return d ? r3(Math.ceil((d + T.seatBearMetres) * 10 - 1e-6) / 10) : 0;
      }
      throw new Error(`${b.id}: no bank within ${T.seatReachMetres} m of the abutment at y ${y0}`);
    };
    seatPad = [reach(ys0[0], -1), reach(ys0[1], +1)];
    corners.push([b.centre[0] - half, ys0[0] - seatPad[0]], [b.centre[0] + half, ys0[1] + seatPad[1]]);
  } else if (opts.deck != null) {
    // a deck the base fixes the height of, seated where its banks reach it on the centre line: each end runs on along
    // its line by the same rule, until G3 under the carriage (both edges and the centre) stands within SEAT_TOLERANCE
    // of the deck, and SEAT_BEAR beyond (the owner's Q10, 10-24: the camp brook deck's measured 1.0 m east, 0.5 m west)
    const reach = (s, dir) => {
      for (let d = 0; d <= T.seatReachMetres + 1e-9; d += 0.05) {
        const [x, y, ux, uy] = pointAt(line, s + dir * d);
        if ([-hw, 0, hw].every((o) => G.at(x - uy * o, y + ux * o) >= opts.deck - T.seatToleranceMetres)) return d ? r3(Math.ceil((d + T.seatBearMetres) * 10 - 1e-6) / 10) : 0;
      }
      throw new Error(`${b.id}: no bank within ${T.seatReachMetres} m of the seat at s ${r3(s)}`);
    };
    seatPad = [reach(sA, -1), reach(sB, +1)];
    const s0 = sA - seatPad[0], s1 = sB + seatPad[1];
    for (let s = s0; ; s = Math.min(s + T.sampleMetres, s1)) { const [x, y, ux, uy] = pointAt(line, s); corners.push([x - uy * half, y + ux * half], [x + uy * half, y - ux * half]); if (s >= s1) break; }
  } else {
    for (let s = sA; s <= sB + 1e-9; s += T.sampleMetres) { const [x, y, ux, uy] = pointAt(line, Math.min(s, sB)); corners.push([x - uy * half, y + ux * half], [x + uy * half, y - ux * half]); }
  }
  const xs = corners.map((c) => c[0]), ys = corners.map((c) => c[1]);
  return {
    sA: r3(sA), sB: r3(sB), span: r3(sB - sA), how,
    a: [r3(ax), r3(ay)], b: [r3(bx), r3(by)],
    deck: r3(deck), deckPlan: b.deck,
    footprint: { x: r3(Math.min(...xs)), y: r3(Math.min(...ys)), w: r3(Math.max(...xs) - Math.min(...xs)), h: r3(Math.max(...ys) - Math.min(...ys)) },
    measures: {
      water: wet.map((r) => [r3(r[0]), r3(r[1])]), planSpan: [r3(planA), r3(planB)],
      planShortOfWater: [r3(Math.max(0, planA - w0)), r3(Math.max(0, w1 - planB))],
      seatGround: [r3(zA), r3(zB)], stepAtSeats: [r3(deck - zA), r3(deck - zB)], seatPad,
      bed: r3(bed), waterTop: r3(water), clearance: r3(deck - water),
      approachGradePct: [r3(steepest(sA, -1)), r3(steepest(sB, +1))],
    },
  };
}

// ---------------------------------------------------------------------------------------------------------------
//  The measured corner flares (the owner's Q4, 10-24)
// ---------------------------------------------------------------------------------------------------------------
// G3's building lots (the 43 of NineMileCreekGroundMasks.BuildingLots: a lot facing east or west stands its size
// turned) and the camp's buildings and pitches, as boxes [x0, x1, y0, y1]; own: the plan path its own drives come from
function groundBoxes(plan) {
  const box = (o) => { const [w, h] = o.faces === 'E' || o.faces === 'W' ? [o.size[1], o.size[0]] : o.size; return [o.at[0] - w / 2, o.at[0] + w / 2, o.at[1] - h / 2, o.at[1] + h / 2]; };
  const out = [];
  for (const k of ['town', 'working', 'south_shore_houses', 'waterfront'])
    plan.lots[k].forEach((l, i) => { if (/^lot\./.test(l.id) && l.at && l.size) out.push({ id: l.id, own: `lots.${k}[${i}]`, box: box(l) }); });
  for (const k of ['buildings', 'pitches']) for (const o of plan.lots.campground[k]) out.push({ id: o.id, own: null, box: box(o) });
  return out;
}

// Each flare of FLARES on the route it names (its id and part), in that route's class, surface and bands (a lane's in
// a street's, without bands). Refused: one whose carriage does not reach into its road's, whose line stands where its
// class would draw centre marks, that paves into a lot, building or pitch other than its own road's lot, or stands in
// water off the decks. A refused flare throws, unless opts.collect (the fitter's), which returns it instead
function layFlares(RK, G, high, plan, routes, bridges, opts) {
  const file = path.join(ROOT, FLARES);
  const src = opts.flares || (fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : { schema: FLARE_SCHEMA, flares: [] });
  if (src.schema !== FLARE_SCHEMA) throw new Error(`${FLARES}: not ${FLARE_SCHEMA}`);
  const boxes = groundBoxes(plan), laid = [], refused = [], ids = new Set();
  const toBox = (B, x, y) => Math.hypot(Math.max(B[0] - x, 0, x - B[1]), Math.max(B[2] - y, 0, y - B[3]));
  const onDeck = (x, y) => bridges.some((b) => { const f = b.footprint; return x >= f.x && x <= f.x + f.w && y >= f.y && y <= f.y + f.h; });
  src.flares.forEach((f, i) => {
    const on = routes.find((r) => r.id === f.on && (r.part || null) === (f.part || null));
    if (!on) throw new Error(`${FLARES} flares[${i}]: no route ${f.on}${f.part ? ' (' + f.part + ')' : ''}`);
    if (!/^flare\.nmc_[a-z0-9_]+$/.test(f.id) || ids.has(f.id)) throw new Error(`${FLARES} flares[${i}]: ${f.id} is not a flare.nmc_ id of its own`);
    ids.add(f.id);
    if (!RK.CLASSES[on.cls].veh) throw new Error(`${f.id}: ${on.id} is a ${on.cls}, which carries no vehicle`);
    if (!(f.pts && f.pts.length >= 2) || !(f.w > 0)) throw new Error(`${f.id}: no line or no width`);
    const cls = on.cls === 'lane' ? 'street' : on.cls, surf = on.surf || RK.CLASSES[on.cls].surf;
    const r = { id: f.id, from: `flares[${i}]`, on: on.id };
    if (on.part) r.onPart = on.part;
    r.cls = cls;
    r.w = r3(f.w);
    if (surf !== RK.CLASSES[cls].surf) r.surf = surf;
    if (on.cls === 'lane') r.bands = []; else if (on.bands) r.bands = on.bands;
    r.for = f.for;
    r.pts = f.pts.map((p) => [r3(p[0]), r3(p[1])]);
    const why = [], past = r.pts.map((p) => project(on.pts, p).d - on.w / 2);
    if (Math.min(...past) > r.w / 2 - FLARE_OVERLAP_METRES) why.push(`its carriage reaches only ${r3(r.w / 2 - Math.min(...past))} m into ${on.id}'s`);
    const marks = RK.CLASSES[cls].marks;
    if (marks && marks.centre && Math.max(...past) > FLARE_LINE_PAST_METRES + 1e-6) why.push(`its line stands ${r3(Math.max(...past))} m past ${on.id}'s carriage edge, where its centre marks would show`);
    const into = {};
    let wet = null;
    for (const s of stations(r.pts, TUNABLES.sampleMetres / 5)) {
      const [x, y, ux, uy] = pointAt(r.pts, s);
      for (const B of boxes) {
        if (B.own && (on.from === B.own || on.from.startsWith(B.own + '.') || on.from.startsWith(B.own + ' '))) continue;
        const d = r.w / 2 - toBox(B.box, x, y);
        if (d > 0) into[B.id] = Math.max(into[B.id] || 0, d);
      }
      for (let o = -r.w / 2; o <= r.w / 2 + 1e-9 && !wet; o += TUNABLES.sampleMetres) {
        const qx = x - uy * o, qy = y + ux * o;
        if (G.wet(qx, qy, high) && !onDeck(qx, qy)) wet = [r3(qx), r3(qy)];
      }
    }
    for (const [id, d] of Object.entries(into)) why.push(`it paves ${r3(d)} m into ${id}`);
    if (wet) why.push(`it stands in water at ${JSON.stringify(wet)}`);
    if (why.length) refused.push({ id: f.id, on: on.id, for: f.for, why }); else laid.push(r);
  });
  if (refused.length && !opts.collect) throw new Error(`${FLARES}: ${refused.length} flares refused\n` + refused.map((x) => `  ${x.id}: ${x.why.join('; ')}`).join('\n'));
  return { laid, refused };
}

// ---------------------------------------------------------------------------------------------------------------
//  The network
// ---------------------------------------------------------------------------------------------------------------
function build(opts = {}) {
  const RK = loadKit(), plan = planLib.read(path.join(ROOT, planLib.ASSET)), src = planLib.source(path.join(ROOT, planLib.ASSET));
  const G = groundLib.load(ROOT), high = springHighWater(), T = TUNABLES;
  const roads = Object.fromEntries(plan.roads.map((r, i) => [r.id, { o: r, from: `roads[${i}]` }]));
  const paths = Object.fromEntries(plan.paths.map((r, i) => [r.id, { o: r, from: `paths[${i}]` }]));
  const boards = Object.fromEntries(plan.boardwalks.map((r, i) => [r.id, { o: r, from: `boardwalks[${i}]` }]));
  const bridgePlan = Object.fromEntries(plan.bridges.map((b) => [b.id, b]));
  const routes = [], bridges = [], held = [], bare = [];
  const add = (r) => { routes.push(r); return r; };
  const hold = (id, from, owner, why) => held.push({ id, from, owner, why });

  // --- Route 91: the route class south of the river bridge, the town section north of it --------------------
  const r91 = roads['road.nmc_route_91'], b91 = bridgePlan['bridge.nmc_route91_river'];
  const fit91 = fitDeck(G, high, b91, r91.o.pts, r91.o.width / 2, { abutments: [b91.abutments.south, b91.abutments.north], deckWidth: b91.width });
  const sSouth = project(r91.o.pts, b91.abutments.south).s;
  add(routeOf(RK, r91.o, r91.from, { part: 'south of the river bridge', pts: clip(r91.o.pts, sSouth, lengthOf(r91.o.pts)), clip: 'starts at the bridge\'s south abutment; the deck carries it over the river' }));
  const town = r91.o.sections[0], townPts = town.pts, sNorth = project(townPts, b91.abutments.north).s;
  add(routeOf(RK, town, `${r91.from}.sections[0]`, { part: 'the town section, north of the river bridge', pts: clip(townPts, sNorth, lengthOf(townPts)), clip: 'starts at the bridge\'s north abutment' }));
  bridges.push(Object.assign({ id: b91.id, on: b91.on, over: b91.over, kind: 'deck', prefab: 'Route91RiverBridge', width: b91.width }, fit91, { why: 'the plan\'s abutments on the 6 m benches; the deck stands at the plan\'s 6 m, the benches\' height, and runs on over each bank to meet it (measures.seatPad)' }));

  // --- Wharf Road, to the wharf's own stub; the pad and the yard road are the wharf's ---------------------------
  const wr = roads['road.nmc_wharf_road'], wEnd = project(wr.o.pts, [40, 118]);
  add(routeOf(RK, wr.o, wr.from, { pts: clip(wr.o.pts, 0, wEnd.s), ends: 'flat', clip: 'ends at (40, 118), where the wharf\'s own stub of Wharf Road takes over' }));
  hold('yard.main_road', `${wr.from}.beyond`, 'the wharf (H5/H6)', 'the wharf\'s yard road, beyond the turning pad');
  hold('pad.nmc_wharf_turning', `${wr.from}.ends`, 'the wharf (H5/H6)', 'the turning pad stays on step 06 in kit v3, the wharf\'s');

  // --- the other roads, whole ------------------------------------------------------------------------------------
  for (const id of ['road.nmc_bar_road', 'road.nmc_water_street', 'road.nmc_mill_road', 'road.nmc_north_creek_lane', 'road.nmc_creek_lane_south',
    'road.nmc_stream_lane', 'road.nmc_camp_road', 'road.nmc_camp_loop_a', 'road.nmc_camp_loop_b', 'road.nmc_camp_loop_c_stem',
    'road.nmc_main_street_lane', 'road.nmc_main_street_east_lane', 'road.nmc_plaza_flats_street', 'road.nmc_plaza_flats_link']) {
    add(routeOf(RK, roads[id].o, roads[id].from));
  }
  const sl = roads['road.nmc_shore_lane'];
  add(routeOf(RK, sl.o, sl.from));
  add(routeOf(RK, { id: sl.o.turn.id, cls: 'street', width: sl.o.turn.width, surface: sl.o.turn.surface, pts: sl.o.turn.pts }, `${sl.from}.turn`, { ends: 'flat' }));

  // --- the camp's brook bridge and pond culvert: fitted, the loops split at them ---------------------------------
  const stem = roads['road.nmc_camp_loop_b_stem'], bb = bridgePlan['bridge.nmc_camp_brook_bridge'];
  // the base charter's H4 fixes this deck at +5.2 ("camp brook deck +5.2"); the culvert's height it leaves to the ground
  const fitB = fitDeck(G, high, bb, stem.o.pts, stem.o.width / 2, { deckWidth: bb.width, deck: BASE_CAMP_BROOK_DECK });
  add(routeOf(RK, stem.o, stem.from, { part: 'east of the brook bridge', pts: clip(stem.o.pts, 0, fitB.sA), clip: 'ends at the brook bridge\'s east seat' }));
  add(routeOf(RK, stem.o, stem.from, { part: 'west of the brook bridge', pts: clip(stem.o.pts, fitB.sB, lengthOf(stem.o.pts)), clip: 'starts at the brook bridge\'s west seat' }));
  bridges.push(Object.assign({ id: bb.id, on: bb.on, over: bb.over, kind: 'deck', prefab: 'CampBrookBridge', width: bb.width }, fitB, { why: 'fitted to G3 (base H4 and the owner, 10-23): the deck at the base\'s +5.2 m, seated where the ravine\'s banks reach it' }));

  const lc = roads['road.nmc_camp_loop_c'], cv = bridgePlan['bridge.nmc_camp_pond_culvert'];
  const fitC = fitDeck(G, high, cv, lc.o.pts, lc.o.width / 2, { deckWidth: cv.width });
  const lcL = lengthOf(lc.o.pts), around = clip(lc.o.pts, fitC.sB, lcL).concat(clip(lc.o.pts, 0, fitC.sA).slice(1));
  add(routeOf(RK, lc.o, lc.from, { part: 'round the loop from the culvert\'s north seat to its south seat', pts: around, clip: 'the loop opened at the pond culvert' }));
  bridges.push(Object.assign({ id: cv.id, on: cv.on, over: cv.over, kind: 'culvert', prefab: 'CampPondCulvert', width: cv.width }, fitC, { why: 'fitted to G3: seats on the dam\'s crest past the outlet, the deck at the higher seat (the base gives the culvert no height)' }));

  // --- footpaths, walks and boardwalks ---------------------------------------------------------------------------
  for (const id of ['path.nmc_shore_path', 'path.nmc_gully_path', 'path.nmc_falls_path', 'path.nmc_river_trail']) add(routeOf(RK, paths[id].o, paths[id].from));
  const bp = paths['path.nmc_camp_brook_path'], fb = bridgePlan['bridge.nmc_camp_footbridge'];
  const fitF = fitDeck(G, high, fb, bp.o.pts, fb.width / 2, { deckWidth: fb.width });
  add(routeOf(RK, bp.o, bp.from, { part: 'north of the footbridge', pts: clip(bp.o.pts, 0, fitF.sA), clip: 'ends at the footbridge\'s north bank' }));
  add(routeOf(RK, bp.o, bp.from, { part: 'south of the footbridge', pts: clip(bp.o.pts, fitF.sB, lengthOf(bp.o.pts)), clip: 'starts at the footbridge\'s south bank' }));
  hold(fb.id, 'bridges', 'H12 (the owner, 10-24, Q15)', `the walk-in sites' footbridge; G3 measures it ${fitF.span} m bank to bank`);
  held[held.length - 1].measured = fitF;
  for (const id of ['boardwalk.nmc_dune_walk_east', 'boardwalk.nmc_dune_walk_west']) add(routeOf(RK, boards[id].o, boards[id].from));
  hold('boardwalk.nmc_town_north', boards['boardwalk.nmc_town_north'].from, 'H7', 'the town boardwalks pass under the bridge\'s end spans: H7\'s, on H4\'s span');
  hold('boardwalk.nmc_town_south', boards['boardwalk.nmc_town_south'].from, 'H7', 'as the north boardwalk');
  hold('boardwalk.nmc_falls_deck', boards['boardwalk.nmc_falls_deck'].from, 'H7', 'the falls deck, H7\'s');
  hold('boardwalk.nmc_marsh_walk', boards['boardwalk.nmc_marsh_walk'].from, 'H7 (the owner, 10-24, Q15)', 'the marsh walk goes with H7\'s boardwalks');
  hold('path.nmc_north_shore_walk', paths['path.nmc_north_shore_walk'].from, 'out of H4 (the owner, 10-24, Q15)', 'optional in the plan, with the optional notch plank that would carry it over the river\'s mouth');
  hold('bridge.nmc_notch_plank', 'bridges', 'out of H4 (the owner, 10-24, Q15)', 'optional in the plan');
  bare.push({ id: 'path.nmc_bar_walk', from: paths['path.nmc_bar_walk'].from, kind: 'tidal_crossing', why: 'the bar is the ground: no surface' });
  bare.push({ id: 'path.nmc_south_strand', from: paths['path.nmc_south_strand'].from, kind: 'beach', why: 'the beach is the ground: no surface' });

  // --- the plaza: its drives and yard, and the public lot's two bays ---------------------------------------------
  const plaza = plan.lots.town[0];
  const seen = new Set();
  plaza.drives.forEach((d, i) => { if (seen.has(d.id)) return; seen.add(d.id); add(routeOf(RK, d, `lots.town[0].drives[${i}]`)); });
  const pk = plaza.parking || (plaza.site && plaza.site.parking);
  if (!pk) throw new Error('the plaza has no parking block');
  // the asphalt runs the lot box's width, x0..x1: its aisles meet the yard's drive inside it; the stalls stand within
  const x0 = pk.box[0], x1 = pk.box[1], s0 = pk.stall.x0, s1 = r3(pk.stall.x0 + pk.stall.w * pk.stall.n);
  const bayS = [Math.min(pk.rowsS[1][0], pk.aisles[1][0]), Math.max(pk.rowsN[1][1], pk.aisles[1][1])];
  const bayN = [Math.min(pk.rowsS[0][0], pk.aisles[0][0]), Math.max(pk.rowsN[0][1], pk.aisles[0][1])];
  // the owner's Q12 (10-24): one row of stalls a bay, the plan's south row (the row the swept measure parks the 2500,
  // the 350 and the van from); the north row's ground is aisle. The plan's cut takes four stalls of the south bay's row
  for (const [part, b, row, cut] of [['the south bay', bayS, pk.rowsS[1], pk.cut], ['the north bay', bayN, pk.rowsS[0], null]]) {
    const [c0, c1] = cut ? cut.match(/\((\d+) to (\d+)\)/).slice(1).map(Number) : [1, 0];
    const n = pk.stall.n - (c1 - c0 + 1);
    add(routeOf(RK, { id: 'lot.nmc_plaza_parking', cls: 'town', surface: 'asphalt', pts: [[x0, (b[0] + b[1]) / 2], [x1, (b[0] + b[1]) / 2]] }, 'lots.town[0] parking', {
      part, w: r3(b[1] - b[0]), bands: [], ends: 'flat', clip: `one row of stalls (the owner, 10-24, Q12) y ${row[0]}..${row[1]} and the aisle y ${row[1]}..${b[1]}, across the lot box's x ${x0}..${x1}; the stalls x ${s0}..${s1}, and kit v4 draws no stall lines`,
      stalls: Object.assign({ row, x: [s0, s1], each: [pk.stall.w, pk.stall.d], n, aisle: [row[1], b[1]] }, cut ? { cut } : {}),
    }));
  }

  // --- the town's sites (site 0 is the park, a lot), the south shore's drives, the camp's walks -------------------
  plan.lots.sites.forEach((s, i) => { if (s.cls && s.pts) add(routeOf(RK, s, `lots.sites[${i}]`)); });
  plan.lots.south_shore_houses.forEach((h, i) => (h.drives || []).forEach((d, j) => add(routeOf(RK, d, `lots.south_shore_houses[${i}].drives[${j}]`))));
  plan.lots.campground.walks.forEach((w, i) => add(routeOf(RK, Object.assign({ cls: 'walk' }, w), `lots.campground.walks[${i}]`)));

  // --- the working lots on their final southern ground (the base's "Move park/laydown to final southern lots"), the
  // spurs re-anchored on the legacy rule (NineMileCreekRoads.ParkSpurRoute: the nearest road to the park's centre;
  // NineMileCreekLaydown.LaydownSpurRoute: the park's centre to the yard's), 5 m of gravel and nothing else, as the
  // legacy painted them; and the parish hall's walk from the north door the plan gives it to Wharf Road's edge ---------
  const [park, lay] = plan.lots.working, roadsOnly = routes.filter((r) => r.from.startsWith('roads['));
  plan.lots.working.forEach((l, i) => add(routeOf(RK, { id: l.id, cls: 'street', surface: 'gravel', pts: [[l.at[0] - l.size[0] / 2, l.at[1]], [l.at[0] + l.size[0] / 2, l.at[1]]] }, `lots.working[${i}]`, {
    w: l.size[1], bands: [], ends: 'flat', clip: `the lot's own gravel, ${l.size[0]} x ${l.size[1]} m round ${JSON.stringify(l.at)}`,
  })));
  const nearestRoad = (p) => roadsOnly.map((r) => Object.assign({ r }, project(r.pts, p))).sort((a, b) => a.d - b.d)[0];
  const mouth = nearestRoad(park.at);
  add(routeOf(RK, { id: 'drive.nmc_truck_park_spur', cls: 'street', surface: 'gravel', pts: [mouth.at, park.at] }, 'lots.working[0].spur', {
    bands: [], ends: 'flat', clip: `the legacy rule: from ${mouth.r.id}, the nearest road, to the park's centre`,
  }));
  add(routeOf(RK, { id: 'drive.nmc_laydown_spur', cls: 'street', surface: 'gravel', pts: [park.at, lay.at] }, 'lots.working[1].spur', {
    bands: [], ends: 'flat', clip: 'the legacy rule: from the park\'s centre to the laydown\'s',
  }));
  const hallAt = plan.lots.town.findIndex((l) => l.id === 'lot.nmc_parish_hall'), hall = plan.lots.town[hallAt];
  if (hall.faces !== 'N') throw new Error('the parish hall no longer faces north: re-derive its walk');
  const door = [hall.at[0], hall.at[1] + hall.size[1] / 2];
  const wharf = routes.find((r) => r.id === 'road.nmc_wharf_road'), toEdge = project(wharf.pts, door);
  if (Math.abs(toEdge.at[0] - door[0]) > 0.01) throw new Error('Wharf Road is not due north of the hall\'s door: re-derive its walk');
  add(routeOf(RK, { id: 'walk.nmc_parish_hall', cls: 'walk', pts: [door, [door[0], r3(toEdge.at[1] - wharf.w / 2)]] }, `lots.town[${hallAt}].walk`, {
    clip: 'the legacy town walk re-anchored: from the hall\'s north door (the plan\'s faces N) due north to Wharf Road\'s edge',
  }));

  // --- ids are unique per part -----------------------------------------------------------------------------------
  const key = new Set();
  for (const r of routes) { const k = r.id + '|' + (r.part || ''); if (key.has(k)) throw new Error('two routes ' + k); key.add(k); }

  // --- the measured corner flares, each on its road (the owner's Q4, 10-24) --------------------------------------
  const { laid: flares, refused: flaresRefused } = layFlares(RK, G, high, plan, routes, bridges, opts);

  // --- no ford: no route or flare stands in water outside a deck -------------------------------------------------
  const fords = [];
  for (const r of routes.concat(flares)) {
    const wet = runs(r.pts, 0, (x, y) => G.wet(x, y, high), T.sampleMetres);
    for (const w of wet) { const [x, y] = pointAt(r.pts, (w[0] + w[1]) / 2); fords.push({ id: r.id, part: r.part, at: [r3(x), r3(y)], length: r3(w[1] - w[0]) }); }
  }

  // --- junctions: each end joins another line, leaves the region, or stops ----------------------------------------
  const ends = [];
  for (const r of routes) {
    for (const [which, p] of [['start', r.pts[0]], ['end', r.pts[r.pts.length - 1]]]) {
      const edge = p[0] <= EDGE.west + 0.5 || p[0] >= EDGE.east - 0.5 || p[1] <= EDGE.south + 0.5 || p[1] >= EDGE.north - 0.5;
      // a round end (the kit's default) reaches half its own width past its last point; a flat end stops at it
      const reach = (r.ends === 'flat' ? 0 : r.w / 2) + T.joinSlackMetres;
      let joins = null;
      for (const q of routes) {
        if (q === r) continue;
        const pr = project(q.pts, p);
        // a flat end stops square at its last point: past it, the other line's carriageway is not there, so the two
        // carriageways' own footprints are measured against each other
        if (q.ends === 'flat' && (pr.s <= 1e-9 || pr.s >= lengthOf(q.pts) - 1e-9) && footprintGap(r, which, q, pr.s) > T.joinSlackMetres) continue;
        if (pr.d <= q.w / 2 + reach && (!joins || pr.d < joins.d)) joins = { id: q.id, part: q.part, d: pr.d };
      }
      const deck = bridges.concat(held.filter((h) => h.measured).map((h) => Object.assign({ id: h.id }, h.measured)))
        .find((b) => Math.hypot(b.a[0] - p[0], b.a[1] - p[1]) <= T.joinSlackMetres || Math.hypot(b.b[0] - p[0], b.b[1] - p[1]) <= T.joinSlackMetres);
      const out = OUTSIDE.find((o) => Math.hypot(o.at[0] - p[0], o.at[1] - p[1]) <= T.joinSlackMetres);
      const kind = deck ? 'deck' : joins ? 'joins' : out ? 'outside' : edge ? 'edge' : 'stops';
      ends.push({ id: r.id, part: r.part, which, at: [r3(p[0]), r3(p[1])], kind, to: deck ? deck.id : joins ? joins.id : out ? out.what : undefined });
    }
  }
  // --- the base's clearances ("Playground clears Loop A/C. North hedge clear x-130..-110; south gates at working
  // drives") and the handoff's call-85 fields and hedge, measured. A gate here is its clearance only: the gates are held
  const clearances = [];
  const swath = (r, step) => {
    const out = [];
    for (const s of stations(r.pts, step)) {
      const [x, y, ux, uy] = pointAt(r.pts, s);
      for (let d = -r.w / 2; d <= r.w / 2 + 1e-9; d += step) out.push([x - uy * d, y + ux * d]);
    }
    return out;
  };
  const inPoly = (poly, x, y) => {
    let c = false;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
      const [ax, ay] = poly[i], [bx, by] = poly[j];
      if ((ay > y) !== (by > y) && x < (bx - ax) * (y - ay) / (by - ay) + ax) c = !c;
    }
    return c;
  };
  // Wharf Road's hedge stands outside its bands; what opens onto the road across the strip from the carriageway's edge
  // to a metre past them, on each side, along the hedge's x -130..-70 (plan) and 10 m either side
  const bandsOut = RK.CLASSES[wharf.cls].bands.reduce((a, b) => a + b.w, 0), lineY = wharf.pts.find((p) => p[0] === -100)[1];
  for (const side of [1, -1]) {
    const opens = [];
    for (const r of routes) {
      if (r === wharf) continue;
      let lo = Infinity, hi = -Infinity;
      for (const [x, y] of swath(r, T.sampleMetres)) {
        const off = side * (y - lineY) - wharf.w / 2;
        if (x >= -140 && x <= -60 && off >= 0 && off <= bandsOut + 1) { lo = Math.min(lo, x); hi = Math.max(hi, x); }
      }
      if (lo <= hi) opens.push({ id: r.id, x: [r3(lo), r3(hi)] });
    }
    if (side < 0) {
      const g = opens.find((o) => o.id === SPUR_GATE.id);
      if (!g || g.x[0] < SPUR_GATE.x[0] || g.x[1] > SPUR_GATE.x[1]) throw new Error(`the ruled spur gate x ${SPUR_GATE.x.join('..')} no longer holds the spur's swath ${g ? g.x.join('..') : '(none)'}`);
      Object.assign(g, { swath: g.x, x: SPUR_GATE.x, why: 'widened for the 53-foot pairs\' swept bodies (the owner, 10-24, Q5): the clearance only; the gate stays undressed' });
    }
    clearances.push(side > 0
      ? { id: 'hedge.nmc_wharf_road', side: 'north', clear: [-130, -110], opens, also: 'the gas bar\'s semi lane, x -131.6..-122.4 (its pads, G3 call 30; painted with the station)', why: 'G3 call 30: the hedge\'s west 20 m runs along the gas bar\'s frontage; keep the north side clear there' }
      : { id: 'hedge.nmc_wharf_road', side: 'south', gates: opens, why: 'the base\'s "south gates at working drives": each is a gate\'s clearance in the south hedge; the gates are held, so no gate is dressed' });
  }
  // hedge.nmc_y96 stops at x -290 (call 85): nothing crosses it, and how near the nearest route comes
  const y96 = plan.lots.hedges.lines.find((h) => h.id === 'hedge.nmc_y96');
  let near96 = { d: Infinity };
  for (const r of routes) for (const [x, y] of swath(r, T.sampleMetres)) { const pr = project(y96.pts, [x, y]); if (pr.d < near96.d) near96 = { id: r.id, d: pr.d, at: [r3(x), r3(y)] }; }
  clearances.push({ id: y96.id, pts: y96.pts, nearest: { id: near96.id, metres: r3(near96.d), at: near96.at }, why: 'call 85: it stops at the yard\'s fence line, x -290' });
  // the call-85 fields: which routes stand in them, and how much
  for (const id of ['field.nmc_west_farm_north', 'field.nmc_river_farm']) {
    const f = plan.lots.fields.find((q) => q.id === id), inside = [];
    for (const r of routes) {
      let n = 0, lo = [Infinity, Infinity], hi = [-Infinity, -Infinity];
      for (const [x, y] of swath(r, T.sampleMetres)) if (inPoly(f.poly, x, y)) { n++; lo = [Math.min(lo[0], x), Math.min(lo[1], y)]; hi = [Math.max(hi[0], x), Math.max(hi[1], y)]; }
      if (n) inside.push({ id: r.id, squareMetres: r3(n * T.sampleMetres * T.sampleMetres), box: [r3(lo[0]), r3(hi[0]), r3(lo[1]), r3(hi[1])] });
    }
    clearances.push({ id, poly: f.poly, inside, why: f.changed });
  }
  // the playground: its box against the camp's roads, pitches and buildings, at the plan's spot and at H4's site
  const cg = plan.lots.campground, pgPlan = cg.buildings.find((b) => b.id === PLAYGROUND_SITE.id);
  const boxOf = (at, size) => [at[0] - size[0] / 2, at[0] + size[0] / 2, at[1] - size[1] / 2, at[1] + size[1] / 2];
  const toBox = (B, x, y) => Math.hypot(Math.max(B[0] - x, 0, x - B[1]), Math.max(B[2] - y, 0, y - B[3]));
  const boxGap = (A, B) => Math.hypot(Math.max(B[0] - A[1], 0, A[0] - B[1]), Math.max(B[2] - A[3], 0, A[2] - B[3]));
  const measureSite = (at, size) => {
    const B = boxOf(at, size), roadsTo = {};
    for (const r of routes.filter((q) => /^road\.nmc_camp_/.test(q.id))) {
      let m = Infinity;
      for (let i = 0; i + 1 < r.pts.length; i++) {
        const [a, b] = [r.pts[i], r.pts[i + 1]], n = Math.max(1, Math.ceil(Math.hypot(b[0] - a[0], b[1] - a[1]) / 0.05));
        for (let k = 0; k <= n; k++) m = Math.min(m, toBox(B, a[0] + (b[0] - a[0]) * k / n, a[1] + (b[1] - a[1]) * k / n));
      }
      const k = r.id + (r.part ? ' (' + r.part + ')' : '');
      roadsTo[k] = r3(m - r.w / 2);
    }
    let pitch = { metres: Infinity }, building = { metres: Infinity };
    for (const p of cg.pitches) { const d = boxGap(B, boxOf(p.at, p.size)); if (d < pitch.metres) pitch = { id: p.id, metres: r3(d) }; }
    for (const b of cg.buildings) if (b !== pgPlan) { const d = boxGap(B, boxOf(b.at, b.size)); if (d < building.metres) building = { id: b.id, metres: r3(d) }; }
    let lo = Infinity, hi = -Infinity, wet = 0;
    for (let x = B[0]; x <= B[1] + 1e-9; x += T.sampleMetres) for (let y = B[2]; y <= B[3] + 1e-9; y += T.sampleMetres) { const z = G.at(x, y); lo = Math.min(lo, z); hi = Math.max(hi, z); if (G.wet(x, y, high)) wet++; }
    const corners = [[B[0], B[2]], [B[1], B[2]], [B[0], B[3]], [B[1], B[3]]].every(([x, y]) => inPoly(cg.outline, x, y));
    return { at, size, box: B, roads: roadsTo, pitch, building, ground: [r3(lo), r3(hi)], wetSamples: wet, inCamp: corners };
  };
  clearances.push({ id: PLAYGROUND_SITE.id, plan: measureSite(pgPlan.at, pgPlan.size), site: measureSite(PLAYGROUND_SITE.at, PLAYGROUND_SITE.size), why: 'the base\'s "Playground clears Loop A/C"; H12 dresses it at this site' });
  clearances.push({ id: 'planting.nmc_swept_bodies', pieces: SWEPT, why: 'the owner\'s Q6 (10-24): step 12 deletes the pieces a ruled route\'s swept body hits, each found by its place' });

  const r91s = routes[0], last = r91s.pts.slice(-2);
  const westEdge = {
    route: r91s.id, at: last[1], from: last[0], cls: r91s.cls, w: r91s.w, surf: RK.CLASSES[r91s.cls].surf,
    lift: RK.CLASSES[r91s.cls].lift, bands: RK.CLASSES[r91s.cls].bands, ground: r3(G.at(last[1][0], last[1][1])),
    bearing: r3(Math.atan2(last[1][1] - last[0][1], last[1][0] - last[0][0]) * 180 / Math.PI),
  };

  const doc = {
    schema: SCHEMA,
    what: 'Nine Mile Creek\'s roads on G3 in road kit v4 (NMC H4), generated by tools/nmc-roads/network.cjs from G3\'s plan as the game holds it. World metres, y north.',
    source: {
      plan: { asset: planLib.ASSET, assetSha256Lf: lfSha(planLib.ASSET), file: src.file, sha256: src.sha256 },
      kit: { dir: KIT, rig: 'roadPathRig4.js', sha256: sha(fs.readFileSync(path.join(ROOT, KIT, 'roadPathRig4.js'))) },
      ground: { height: groundLib.HEIGHT, heightPixelsSha256: G.heightSha256, still: groundLib.STILL, stillPixelsSha256: G.stillSha256 },
      springHighWater: high,
    },
    tunables: TUNABLES,
    routes, flares, bridges, held, bare, clearances,
    junctions: ends,
    westEdge,
    fords,
  };
  if (opts.collect) Object.defineProperty(doc, 'flaresRefused', { value: flaresRefused });
  return doc;
}

// one space indents, and a point or a pair of numbers on one line
function text(doc) { return JSON.stringify(doc, null, 1).replace(/\[\s+(-?[\d.e+-]+),\s+(-?[\d.e+-]+)\s+\]/g, '[$1, $2]') + '\n'; }

function summary(doc) {
  const by = {}; for (const r of doc.routes) (by[r.cls] = by[r.cls] || []).push(r);
  const lines = [`${doc.routes.length} routes on ${new Set(doc.routes.map((r) => r.id)).size} ids:`];
  for (const [c, rs] of Object.entries(by)) lines.push(`  ${c.padEnd(9)} ${rs.length}`);
  lines.push(`${doc.flares.length} corner flares on ${new Set(doc.flares.map((f) => f.on + '|' + (f.onPart || ''))).size} roads`);
  for (const b of doc.bridges) lines.push(`bridge ${b.id}: ${b.how}; seats ${JSON.stringify(b.a)} ${JSON.stringify(b.b)}, span ${b.span}, deck ${b.deck} (plan ${b.deckPlan}), seat steps ${b.measures.stepAtSeats.join(' / ')}, water ${b.measures.waterTop}, clearance ${b.measures.clearance}`);
  for (const h of doc.held) lines.push(`held ${h.id}: ${h.owner}`);
  for (const b of doc.bare) lines.push(`bare ${b.id}: ${b.kind}`);
  const k = {}; for (const e of doc.junctions) k[e.kind] = (k[e.kind] || 0) + 1;
  lines.push('ends: ' + Object.entries(k).map(([a, n]) => `${a} ${n}`).join(', '));
  const w = doc.westEdge;
  lines.push(`west edge: ${w.route} leaves at ${JSON.stringify(w.at)} from ${JSON.stringify(w.from)}, bearing ${w.bearing} deg, ${w.cls} ${w.w} m ${w.surf} +${w.lift}, ground ${w.ground} m`);
  lines.push(doc.fords.length ? `FORDS ${doc.fords.length}: ` + doc.fords.map((f) => `${f.id}${f.part ? ' (' + f.part + ')' : ''} at ${JSON.stringify(f.at)} ${f.length} m`).join('; ') : 'no ford');
  return lines.join('\n');
}

if (require.main === module) {
  const doc = build(), out = path.join(ROOT, OUT), t = text(doc);
  if (process.argv.includes('--check')) {
    const have = fs.existsSync(out) ? fs.readFileSync(out) : Buffer.alloc(0);
    if (Buffer.compare(have, Buffer.from(t, 'utf8')) !== 0) { console.log('RESULT: FAIL the committed network differs from a rebuild'); process.exit(1); }
    console.log('RESULT: PASS the committed network is a rebuild, byte for byte');
  } else {
    fs.mkdirSync(path.dirname(out), { recursive: true });
    fs.writeFileSync(out, t);
    console.log(summary(doc));
    console.log(`wrote ${OUT} (${Buffer.byteLength(t)} bytes, sha256 ${sha(Buffer.from(t, 'utf8'))})`);
  }
}

module.exports = { build, text, summary, groundBoxes, OUT, SCHEMA, FLARES, FLARE_SCHEMA, FLARE_LINE_PAST_METRES, FLARE_OVERLAP_METRES, TUNABLES, lengthOf, pointAt, project, clip, runs, stations };
