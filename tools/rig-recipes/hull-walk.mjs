#!/usr/bin/env node
// THE HULL WALKS, WRITTEN BY THE RIGS (ADR 0048 §8 B; the spec is docs/art/hull-layout-spec.md).
//
//   node tools/rig-recipes/hull-walk.mjs                  # derive every walk, compare with the file; writes nothing
//   node tools/rig-recipes/hull-walk.mjs --write          # write docs/art/rigs/gameplay/<rig stem>.<hull id>.walk.json
//   node tools/rig-recipes/hull-walk.mjs --fit            # also print the character fit, fixture by fixture
//   node tools/rig-recipes/hull-walk.mjs --hull bowrider  # one hull
//
// A WALK IS THE RIG'S OWN. `byId(id).walk()` returns every field of the sidecar: the areas, the climbs, the house,
// the anchors, interact, cleats, washboards, `_excluded`, rock and the room's fitCheck. This script adds only what a
// rig cannot know about itself, and never edits a number the rig wrote:
//   - `derivedFromRigSha256`: the sha256 of the rig's text with its line ends as LF (a CRLF checkout hashes the same);
//   - `authoring`: how the file was made;
//   - `qa.character`: the fit to character rig 10.3, every clip frame of every one of its thirty builds, held against
//     this hull's own fixtures as its walk publishes them (the helm, the berths, every covered floor, the door, the
//     washboards, the steps, the ladder, the seats). Nothing in it is a typed-in character number.
// It refuses a character rig that is not revision 10.3 and a rig whose walk names another hull.
//
// The fit is the 10-03 writer's (Art/_recreationGameplay.js, the CHARACTER 10.2 FIT), carried to 10.3 and fed from
// the walk instead of from a shim of the rig: the same clips, the same pivots and the same pass marks.
//
// No dependencies, no network, nothing to install: Node 16 or later. The rigs run in Node's own V8, read verbatim.
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import crypto from 'node:crypto';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
export const SCHEMA = 'hidden-harbours/hull-walk@1';
export const WRITER = 'tools/rig-recipes/hull-walk.mjs';
export const BOATS = [
  { rig: 'goFastIsoRig.js', sym: 'GoFastIso', hull: 'go_fast' },
  { rig: 'bowriderIsoRig.js', sym: 'BowriderIso', hull: 'bowrider' },
  { rig: 'cruiserIsoRig.js', sym: 'CruiserIso', hull: 'express_cruiser' },
];
export const CHARACTER = {
  rig: 'docs/art/rigs/character/rig10/Art/characterIsoRig10.js',
  poses: 'docs/art/rigs/character/rig10/Art/characterIsoRig10.poses.js',
  sym: 'CharacterIso10', revision: '10.3',
};

const r3 = (v) => Math.round(v * 1000) / 1000, r4 = (v) => Math.round(v * 10000) / 10000;

export const lfText = (file) => fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
export const lfSha = (file) => crypto.createHash('sha256').update(lfText(file), 'utf8').digest('hex');
export const rigPath = (boat) => path.join(ROOT, 'docs/art/rigs', boat.rig);
export const walkPath = (boat) => path.join(ROOT, 'docs/art/rigs/gameplay', boat.rig.replace(/\.js$/, '') + '.' + boat.hull + '.walk.json');
export const serialize = (w) => JSON.stringify(w, null, 1) + '\n';

export function loadRig(boat) {
  const ctx = {}; vm.createContext(ctx);
  vm.runInContext(lfText(rigPath(boat)), ctx, { filename: boat.rig });
  const G = ctx[boat.sym];
  if (!G) throw new Error(boat.rig + ': no global ' + boat.sym);
  const H = typeof G.byId === 'function' ? G.byId(boat.hull) : null;
  if (!H) throw new Error(boat.rig + ': byId(' + boat.hull + ') returned nothing');
  return { G, H };
}

export function loadCharacter() {
  const ctx = { console }; ctx.globalThis = ctx; ctx.window = ctx; vm.createContext(ctx);
  for (const f of [CHARACTER.rig, CHARACTER.poses]) vm.runInContext(lfText(path.join(ROOT, f)), ctx, { filename: path.basename(f) });
  const C = ctx[CHARACTER.sym];
  if (!C || C.revision !== CHARACTER.revision) throw new Error('hull walk: the fit is for character rig ' + CHARACTER.revision + ' (got ' + (C && C.revision) + ')');
  return C;
}

/* ===================================== geometry ===================================== */
const pip = (pt, poly) => { let c = false; for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) { const a = poly[i], b = poly[j];
  if (((a[1] > pt[1]) !== (b[1] > pt[1])) && (pt[0] < (b[0] - a[0]) * (pt[1] - a[1]) / ((b[1] - a[1]) || 1e-12) + a[0])) c = !c; } return c; };
const segD = (p, a, b) => { const dx = b[0] - a[0], dy = b[1] - a[1], L = dx * dx + dy * dy; let t = L ? ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / L : 0;
  t = Math.max(0, Math.min(1, t)); return Math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy); };
const edgeD = (p, poly) => { let d = Infinity; for (let i = 0; i < poly.length; i++) d = Math.min(d, segD(p, poly[i], poly[(i + 1) % poly.length])); return d; };
// where the line y = const crosses a polygon: its lowest x (side -1) or highest (side +1)
const edgeX = (poly, y, side) => { let best = side < 0 ? Infinity : -Infinity;
  for (let i = 0; i < poly.length; i++) { const a = poly[i], b = poly[(i + 1) % poly.length];
    if ((a[1] - y) * (b[1] - y) > 0 || a[1] === b[1]) continue; const x = a[0] + (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]);
    best = side < 0 ? Math.min(best, x) : Math.max(best, x); } return best; };
// a walk area's z at (x, y): flat, or its ramp, held beyond a0 / a1 (the walk's own rule)
export const areaZ = (a, x, y) => { if (a.z != null) return a.z; const R = a.ramp, v = R.axis === 'x' ? x : y;
  const t = Math.max(0, Math.min(1, (v - R.a0) / ((R.a1 - R.a0) || 1e-12))); return R.z0 + (R.z1 - R.z0) * t; };
export const onArea = (a, x, y) => pip([x, y], a.poly) && !(a.holes || []).some((h) => pip([x, y], h));

/* ===================================== the character 10.3 fit ===================================== */
export function builds(C) {
  return C.CAST.map((k) => { const P = C.pinsOf(C.evalClip('idle', 0, k, {})), col = C.colliderOf(k);
    return { key: k, crown: r4(P.head[2]), radius: col.collision_radius_m }; });
}
// the distance from a point to a wheel rim (circle: centre c, unit normal n, radius R)
function rimMiss(p, c, n, R) { const q = [p[0] - c[0], p[1] - c[1], p[2] - c[2]], h = q[0] * n[0] + q[1] * n[1] + q[2] * n[2];
  const ip = [q[0] - h * n[0], q[1] - h * n[1], q[2] - h * n[2]]; return Math.hypot(h, Math.hypot(ip[0], ip[1], ip[2]) - R); }
// the helm: seated (drive, its world opts computed from the seat and the hub) or standing (helm_idle at the pilot's
// spot). A build fits when both hands land within 0.04 m of the boat's rim on every frame and the clip's own wheel
// (drive) or the hands' midpoint (helm_idle) sits within 0.02 m of the boat's hub
function helmFit(C, B, h, standing) {
  const rk = h.rakeDeg * Math.PI / 180, n = [0, -Math.cos(rk), Math.sin(rk)];
  const piv = standing ? [h.pilot.x, h.pilot.y, h.floorZ] : [h.seat.x, h.seat.y + 0.02, h.floorZ];
  const req = standing ? null : { seatZ: r4(h.seat.z - h.floorZ), wheelZ: r4(h.hub.z - h.floorZ), wheelY: r4(h.hub.y - piv[1]) };
  const clip = standing ? 'helm_idle' : 'drive', nF = C.ANIMS[standing ? 'idle' : 'drive'].frames;
  const c = [h.hub.x - piv[0], h.hub.y - piv[1], h.hub.z - piv[2]];
  const per = {}; let okN = 0, posN = 0, clamped = false, worst = 0, worstHub = 0, worstIk = 0, tilt = null;
  const grip = { z: [Infinity, -Infinity], y: [Infinity, -Infinity] };      // where each build holds its hands (helm_idle)
  for (const b of B) { let m = 0, ik = 0, cl = false, ho = 0;
    for (let k = 0; k < nF; k++) { const P = C.pinsOf(C.evalClip(clip, k / nF, b.key, req || {}));
      m = Math.max(m, rimMiss(P.handL, c, n, h.rim), rimMiss(P.handR, c, n, h.rim));
      if (P.ikShort) ik = Math.max(ik, Math.max.apply(null, Object.values(P.ikShort)));
      if (P.wheel) { if (P.wheel.clamped) cl = true; if (P.wheel.tiltDeg != null) tilt = P.wheel.tiltDeg; const w = P.wheel.wheelMid;
        ho = Math.max(ho, Math.hypot(w[0] - c[0], w[1] - c[1], w[2] - c[2])); }
      else { ho = Math.max(ho, Math.hypot(P.mid[0] - c[0], P.mid[1] - c[1], P.mid[2] - c[2]));
        grip.z = [Math.min(grip.z[0], P.mid[2]), Math.max(grip.z[1], P.mid[2])]; grip.y = [Math.min(grip.y[0], P.mid[1]), Math.max(grip.y[1], P.mid[1])]; } }
    const posOk = ho <= 0.02, ok = m <= 0.04 && posOk; if (ok) okN++; if (posOk) posN++; if (cl) clamped = true;
    worst = Math.max(worst, m); worstHub = Math.max(worstHub, ho); worstIk = Math.max(worstIk, ik);
    per[b.key] = { grip_miss_m: r3(m), hub_offset_m: r3(ho), ikShort_m: r3(ik), clamped: cl, ok }; }
  return { clip, piv: piv.map(r3), req, per, okN, posN, clamped, tilt, worst: r3(worst), worstHub: r3(worstHub), worstIk: r3(worstIk),
    grip: standing ? { over_floor_m: grip.z.map(r3), ahead_m: grip.y.map(r3) } : null };
}
// the drive clip's world opts: the span of each (the others at the clip's contract) that it takes without clamping
export function driveRange(C) {
  const d = C.CONTRACT.drive, out = {};
  for (const key of ['seatZ', 'wheelZ', 'wheelY']) { const ok = [];
    for (let v = 0; v <= 1.6 + 1e-9; v += 0.01) { const o = Object.assign({}, d); o[key] = r3(v);
      if (!C.pinsOf(C.evalClip('drive', 0, C.CAST[0], o)).wheel.clamped) ok.push(r3(v)); }
    out[key] = ok.length ? [Math.min.apply(null, ok), Math.max.apply(null, ok)] : null; }
  return out;
}
// a berth: every pin of every sleep frame (hands, feet and 0.12 m of toe, hip, crown, mid) inside the mattress outline
// and 0.10 m under the deckhead. One pivot for every build: the tallest head 0.12 m inside the pillow end
function berthFit(C, B, br, floorZ) {
  const athwart = br.kind === 'midberth', f = athwart ? [-1, 0] : [0, -1], right = [f[1], -f[0]];
  const bedZ = r4(br.topZ - floorZ), poly = br.poly, ys = poly.map((p) => p[1]);
  const y0 = Math.min.apply(null, ys), y1 = Math.max.apply(null, ys), ym = (y0 + y1) / 2;
  let headMax = 0; const lying = {};
  for (const b of B) { const pins = []; for (let k = 0; k < C.ANIMS.sleep.frames; k++) pins.push(C.pinsOf(C.evalClip('sleep', k / C.ANIMS.sleep.frames, b.key, { bedZ })));
    lying[b.key] = pins; headMax = Math.max(headMax, Math.max.apply(null, pins.map((Q) => Q.head[1]))); }
  const piv = athwart ? [edgeX(poly, ym, -1) + 0.12 + headMax, ym, floorZ] : [0, y0 + 0.12 + headMax, floorZ];
  const toBoat = (p) => [piv[0] + p[0] * right[0] + p[1] * f[0], piv[1] + p[0] * right[1] + p[1] * f[1], piv[2] + p[2]];
  const per = {}; let okN = 0;
  for (const b of B) { let inside = true, margin = Infinity, zTop = 0;
    for (const Q of lying[b.key]) { const toe = (p) => [p[0], p[1] - 0.12, p[2]];
      for (const p of [Q.head, Q.hip, Q.handL, Q.handR, Q.footL, Q.footR, Q.mid, toe(Q.footL), toe(Q.footR)]) { const w = toBoat(p);
        const inP = pip([w[0], w[1]], poly); if (!inP) inside = false; margin = Math.min(margin, inP ? edgeD([w[0], w[1]], poly) : -edgeD([w[0], w[1]], poly));
        zTop = Math.max(zTop, w[2]); } }
    const clear = r3(br.deckheadZ - zTop), ok = inside && clear >= 0.10; if (ok) okN++;
    per[b.key] = { inside, edge_margin_m: r3(margin), clear_above_body_m: clear, ok }; }
  return { piv: piv.map(r3), bedZ, per, okN, length: r3(athwart ? edgeX(poly, ym, 1) - edgeX(poly, ym, -1) : y1 - y0) };
}
// the step heights the board / boardDown clips reach (railZ swept; a height is in range when no build's arms fall
// more than 0.01 m short on any frame)
const BOARD_SWEEP = [0.06, 0.10, 0.15, 0.20, 0.25, 0.30, 0.35, 0.40, 0.45, 0.50, 0.55, 0.60, 0.65, 0.70, 0.75, 0.80, 0.85, 0.90, 0.95, 1.00, 1.10, 1.20, 1.30];
export function boardRange(C) {
  const res = {};
  for (const clip of ['board', 'boardDown']) { const nF = C.ANIMS[clip].frames, ok = [];
    for (const z of BOARD_SWEEP) { let worst = 0;
      for (const b of C.CAST) for (let k = 0; k < nF; k++) { const P = C.pinsOf(C.evalClip(clip, C.uOf(clip, k), b, { railZ: z }));
        if (P.ikShort) worst = Math.max(worst, Math.max.apply(null, Object.values(P.ikShort))); }
      if (worst <= 0.01) ok.push(z); }
    res[clip] = ok.length ? [Math.min.apply(null, ok), Math.max.apply(null, ok)] : null; }
  return res;
}
// the steps between areas the walker crosses. Two areas meet where a point of one's outline (or hole) lies on the
// other or within 0.03 m of its edge; the walker crosses where the heights there differ least, so the step is the
// smallest difference along the meeting. Under 0.06 m it is flush (walk); from stepTol up the areas do not connect
// (a deck over a cabin sole); between, it is a step
const nearArea = (q, x, y) => onArea(q, x, y) || edgeD([x, y], q.poly) <= 0.03 || (q.holes || []).some((h) => edgeD([x, y], h) <= 0.03);
function steps(W) {
  const A = W.walk.areas, out = [];
  for (let i = 0; i < A.length; i++) for (let j = i + 1; j < A.length; j++) { const a = A[i], b = A[j]; let dz = Infinity, at = null;
    for (const [p, q] of [[a, b], [b, a]]) for (const ring of [p.poly].concat(p.holes || [])) for (let e = 0; e < ring.length; e++) {
      const u = ring[e], v = ring[(e + 1) % ring.length];
      for (let s = 0; s <= 8; s++) { const x = u[0] + (v[0] - u[0]) * s / 8, y = u[1] + (v[1] - u[1]) * s / 8;
        if (!nearArea(q, x, y)) continue;
        const d = Math.abs(areaZ(p, x, y) - areaZ(q, x, y)); if (d < dz) { dz = d; at = [r3(x), r3(y)]; } } }
    if (at && dz >= 0.06 && dz < W.walk.stepTol) out.push({ from: a.id, to: b.id, rise_m: r3(dz), at, up: areaZ(a, at[0], at[1]) < areaZ(b, at[0], at[1]) }); }
  return out;
}
// the narrowest width of an area across its ramp's axis (a climb's clear width is that plus the walker's diameter)
function narrowest(a) {
  const ax = a.ramp ? a.ramp.axis : 'y', o = ax === 'y' ? 1 : 0, vs = a.poly.map((p) => p[o]);
  const lo = Math.min.apply(null, vs), hi = Math.max.apply(null, vs); let w = Infinity;
  for (let k = 1; k < 40; k++) { const v = lo + (hi - lo) * k / 40; let mn = Infinity, mx = -Infinity;
    for (let i = 0; i < a.poly.length; i++) { const p = a.poly[i], q = a.poly[(i + 1) % a.poly.length];
      if ((p[o] - v) * (q[o] - v) > 0 || p[o] === q[o]) continue; const c = p[1 - o] + (q[1 - o] - p[1 - o]) * (v - p[o]) / (q[o] - p[o]);
      mn = Math.min(mn, c); mx = Math.max(mx, c); }
    if (mx > mn) w = Math.min(w, mx - mn); }
  return r3(w);
}

// every fixture a figure uses on this hull, fitted for the thirty builds
export function fit(G, W, C) {
  const B = builds(C), N = B.length, FX = [];
  const names = (pred) => B.filter(pred).map((b) => b.key);
  const push = (o, okKeys) => { o.builds_ok = okKeys.length; o.of = N;
    if (okKeys.length > 0 && okKeys.length <= N / 2) o.fit = okKeys.slice(); else if (okKeys.length > N / 2 && okKeys.length < N) o.failing = names((b) => !okKeys.includes(b.key));
    FX.push(o); return o; };
  const okOf = (per) => Object.keys(per).filter((k) => per[k].ok);
  const sole = (lv) => (W.house && W.house.decks && W.house.decks[lv]) ? W.house.decks[lv].soleZ : null;
  const allWaits = new Set();
  // ---- the helm
  const an = W.anchors || {};
  if (an.helm && an.wheel && G.WHEEL_GEO) {
    const h = { hub: an.wheel, seat: an.helm, pilot: an.pilot, floorZ: an.pilot.z, rim: G.WHEEL_GEO.rad, rakeDeg: G.WHEEL_GEO.rake };
    const S = helmFit(C, B, h, false), DR = driveRange(C);
    push({ id: 'helm_seated', what: 'seated at the wheel', clip: 'drive', world_opts: S.req, range: DR, clamped: S.clamped,
      wheel: { hub: [an.wheel.x, an.wheel.y, an.wheel.z], rim_m: h.rim, rake_deg: h.rakeDeg, clip_tilt_deg: S.tilt },
      worst_grip_miss_m: S.worst, worst_hub_offset_m: S.worstHub, worst_ikShort_m: S.worstIk,
      note: S.okN === N ? 'every build’s hands land on the rim (within 0.04 m) on every frame, the clip’s wheel on the hub' :
        S.posN < N ? 'the opts clamp: the clip poses its wheel up to ' + S.worstHub + ' m from the hub' :
        'the clip’s wheel sits on the hub; the hands miss the rim by up to ' + S.worst + ' m (its rim tilts ' + S.tilt + '°, this wheel rakes ' + h.rakeDeg + '°)' }, okOf(S.per));
    if (S.okN < N && S.posN === N) allWaits.add('drive.tiltDeg');
    const T = helmFit(C, B, h, true), hubZ = r3(an.wheel.z - h.floorZ), hubY = r3(an.wheel.y - an.pilot.y), seatBack = r3(an.wheel.y - an.helm.y);
    const why = T.okN === N ? null : 'Why not: helm_idle takes no wheel opts, so each build holds its hands where its own arms put them: ' + T.grip.over_floor_m.join(' to ') +
      ' m over the floor and ' + T.grip.ahead_m.join(' to ') + ' m ahead of its feet. One wheel is within 0.02 m of a few of those grips at most. This wheel is set for ' +
      'drive (seated, 30 of 30), which takes it no higher than ' + DR.wheelZ[1] + ' m over the floor (this hub: ' + hubZ + ' m), and its seat is ' + seatBack +
      ' m behind the hub, so a figure standing at the reach helm_idle holds would stand in the seat. At the rig’s pilot spot (' + hubY + ' m behind the hub) the hands’ midpoint misses the hub by up to ' +
      T.worstHub + ' m. Standing waits on helm_idle taking wheelZ / wheelY (as drive does) and a helm with standing room in front of its seat.';
    push({ id: 'helm_standing', what: 'standing at the wheel', clip: 'helm_idle', pivot: T.piv, worst_grip_miss_m: T.worst, worst_hands_to_hub_m: T.worstHub,
      hub_over_floor_m: hubZ, hub_ahead_of_pilot_m: hubY, grip_by_build: T.grip,
      waitsOn: T.okN === N ? undefined : ['helm_idle.wheelOpts'],
      note: T.okN === N ? 'every build’s hands land on the rim from the pilot’s spot' : why }, okOf(T.per));
    if (T.okN < N) allWaits.add('helm_idle.wheelOpts');
  }
  // ---- the berths
  for (const br of (an.berths || [])) {
    const floorZ = sole('below'), F = berthFit(C, B, br, floorZ);
    push({ id: br.id, what: (br.kind === 'midberth' ? 'the mid-cabin berth' : 'the V-berth') + ', asleep', clip: 'sleep', pivot: F.piv, world_opts: { bedZ: F.bedZ },
      length_m: F.length, deckhead_z: br.deckheadZ, note: 'every pin of every frame inside the mattress outline, the body 0.10 m under the deckhead' }, okOf(F.per));
  }
  // ---- the walk itself: every area is the walker's centre region at its radius
  push({ id: 'walk_areas', what: 'walk every area (' + W.walk.areas.length + ' of them) at the walker’s radius ' + W.walk.radius + ' m', clip: 'walk',
    note: 'each outline is inset by the walker’s radius, the widest 10.3 build’s collision radius or more: every build’s shoulders fit wherever the walker goes. ' +
      'Headroom is the stand fixtures’; steps are the step fixtures’.' }, names((b) => b.radius <= W.walk.radius));
  // ---- every covered floor: stand up (crown + 0.02 under the deckhead). builds_ok: anywhere on it (its lowest
  // deckhead, headroom); stands_somewhere: under its highest (headroomMax)
  const stand = (a, excluded) => { const hmax = a.headroomMax != null ? a.headroomMax : a.headroom;
    push({ id: 'stand_' + a.id, what: 'stand up on ' + a.id + (excluded ? ' (excluded: ' + (a.waitsOn || []).join(', ') + ')' : ''), clip: 'idle',
      headroom_m: a.headroom, headroomMax_m: hmax, stands_somewhere: names((b) => b.crown + 0.02 <= hmax).length,
      waitsOn: a.waitsOn }, names((b) => b.crown + 0.02 <= a.headroom)); };
  for (const a of W.walk.areas) if (a.headroom != null && !a.kind) stand(a, false);
  for (const x of (W._excluded || [])) if (x.headroom != null) stand(x, true);
  // ---- the door(s): through upright (shoulders 2 x collision radius, crown + 0.02)
  const doors = (W.house && (W.house.doors || (W.house.door ? [W.house.door] : []))) || [];
  for (const d of doors) { const w = r3(d.x1 - d.x0), hgt = r3(d.z1 - (d.sillZ != null ? d.sillZ : d.z0)), dx = (W._excluded || []).find((x) => x.id === d.id);
    const waits = (dx && dx.waitsOn) || (W.walk.areas.find((a) => a.id === d.id) || {}).waitsOn;
    push({ id: 'through_' + (d.id || d.face || 'door'), what: 'through the ' + (d.id || 'house') + ' doorway upright (' + d.kind + ' door, ' + w + ' x ' + hgt + ' m)', clip: 'walk',
      clear_width_m: w, clear_height_m: hgt, too_wide: names((b) => 2 * b.radius > w).length, must_duck: names((b) => b.crown + 0.02 > hgt).length, waitsOn: waits },
      names((b) => 2 * b.radius <= w && b.crown + 0.02 <= hgt)); }
  if (G.GATE && G.GATE.x1 != null) { const w = r3(G.GATE.x1 - G.GATE.x0);
    push({ id: 'transom_door', what: 'through the transom door (open above)', clip: 'walk', clear_width_m: w }, names((b) => 2 * b.radius <= w)); }
  // ---- washboards: walked by the washboard's own rule (the shoulders against the clear width)
  for (const wb of (W.washboards || [])) { const w = wb.pinch_m != null ? wb.pinch_m : wb.width_m;
    push({ id: 'washboard_' + wb.side, what: 'along the ' + wb.side + ' washboard' + (wb.pinch_m != null ? ', at its pinch' : ''), clip: 'walk', clear_width_m: w }, names((b) => 2 * b.radius <= w)); }
  // ---- the excluded standing surfaces that publish their gaps: what the walker's single radius leaves out
  for (const x of (W._excluded || [])) if (x.gaps_m) {
    const g = x.gaps_m, need = Math.min.apply(null, Object.values(g).filter((v) => v >= 0.3));
    push({ id: 'between_' + x.id, what: 'stand in ' + x.id + ' between its fittings (the narrowest gap a figure stands in: ' + need + ' m)', clip: 'walk',
      clear_width_m: need, walker_radius_m: W.walk.radius, waitsOn: x.waitsOn }, names((b) => 2 * b.radius <= need)); }
  // ---- climbs and stairs (areas with a kind): walked as their ramp; the treads' feet wait on stair clips. A build
  // goes up or down upright when its shoulders fit the flight's narrowest clear width and its crown clears the
  // flight's headroom (the soffit over its top, where it has one)
  for (const a of W.walk.areas) if (a.kind) {
    const w = a.waitsOn || (a.kind === 'stair' ? ['stairDown', 'stairUp'] : ['ladderOn']), cw = r3(narrowest(a) + 2 * W.walk.radius);
    push({ id: 'climb_' + a.id, what: a.id + ' (a ' + a.kind + '), walked as its ramp ' + a.ramp.z0 + ' → ' + a.ramp.z1 + ' m, upright', clip: 'walk',
      clear_width_m: cw, headroom_m: a.headroom != null ? a.headroom : null, ramp: a.ramp, waitsOn: w,
      note: 'the ramp carries the root; the feet on the treads wait on ' + w.filter((v) => /^stair|^ladder/.test(v)).join(' / ') +
        (a.headroom != null ? '; under the soffit (' + a.headroom + ' m over the top tread) every build taller than ' + r3(a.headroom - 0.02) + ' m waits on duck' : '') },
      names((b) => 2 * b.radius <= cw && (a.headroom == null || b.crown + 0.02 <= a.headroom)));
    w.forEach((v) => allWaits.add(v)); }
  // ---- steps between areas (the walker's stepTol; the board / boardDown clips where one is to be played)
  const BR = boardRange(C);
  for (const s of steps(W)) { const m = s.rise_m;
    let clip, range;
    if (s.up && BR.board && m >= BR.board[0] && m <= BR.board[1]) { clip = 'board'; range = BR.board; }
    else { clip = s.up ? 'boardDown (reversed)' : 'boardDown'; range = BR.boardDown; }
    const ok = m <= W.walk.stepTol && range && m >= range[0] && m <= range[1];
    push({ id: 'step_' + s.from + '_' + s.to, what: 'step ' + (s.up ? 'up' : 'down') + ' ' + s.from + ' → ' + s.to + ' (' + m + ' m)', clip, world_opts: { railZ: m },
      range_m: range, within_stepTol: m <= W.walk.stepTol }, ok ? B.map((b) => b.key) : []); }
  // ---- the swim ladder (ladderDown, rung 0.30 m; reversed to climb aboard)
  if (an.ladder) { const ok = [];
    for (const b of B) { let ik = 0; for (let k = 0; k < C.ANIMS.ladderDown.frames; k++) { const Q = C.pinsOf(C.evalClip('ladderDown', k / C.ANIMS.ladderDown.frames, b.key, { rung: 0.30 }));
        if (Q.ikShort) ik = Math.max(ik, Math.max.apply(null, Object.values(Q.ikShort))); } if (ik <= 0.01) ok.push(b.key); }
    push({ id: 'swim_ladder', what: 'down the swim ladder (reversed to climb aboard)', clip: 'ladderDown', world_opts: { rung: 0.30 },
      note: 'rungs at 0.30 m (the rig draws the ladder stowed); the top-out onto the platform waits on ladderOn', waitsOn: ['ladderOn'] }, ok);
    allWaits.add('ladderOn'); }
  else for (const x of (W._excluded || [])) if ((x.waitsOn || []).includes('ladderOn') && x.z != null) {
    push({ id: 'aboard_from_the_water', what: 'come aboard from the water (onto ' + x.id + ', ' + r3(x.z - W.DWL) + ' m above the waterline; no ladder)', clip: null,
      waitsOn: ['ladderOn'], note: 'no ladder on this hull: a swimmer cannot climb back aboard (see _excluded ' + x.id + ')' }, []);
    allWaits.add('ladderOn'); }
  // ---- the seats: no sit clip in 10.3
  const seats = (W._excluded || []).filter((x) => x.of === 'seat' && (x.waitsOn || []).includes('sit')).map((x) => x.id);
  if (seats.length) push({ id: 'seats', what: 'sit as a passenger (' + seats.join(', ') + ')', clip: null, waitsOn: ['sit'],
    note: '10.3 has no sit clip: drive is its only seated clip, and its hands hold a wheel' }, []);
  // ---- every clip the walk says it waits on
  const each = (o) => { for (const w of (o.waitsOn || [])) allWaits.add(w); };
  (W._excluded || []).forEach(each); (W.interact || []).forEach(each); W.walk.areas.forEach(each);
  return {
    rig: path.basename(CHARACTER.rig), exportSymbol: CHARACTER.sym, revision: C.revision,
    derivedFromRigSha256: lfSha(path.join(ROOT, CHARACTER.rig)), posesDerivedFromRigSha256: lfSha(path.join(ROOT, CHARACTER.poses)),
    builds: N, crown_m: [Math.min.apply(null, B.map((b) => b.crown)), Math.max.apply(null, B.map((b) => b.crown))],
    collision_radius_m: [Math.min.apply(null, B.map((b) => b.radius)), Math.max.apply(null, B.map((b) => b.radius))],
    board_range_m: BR.board, boardDown_range_m: BR.boardDown,
    method: 'every frame of each clip for all thirty builds (evalClip + pinsOf), world opts from the walk; hands vs the wheel rim, the lying body vs the berth outline and deckhead, crowns vs headroom, shoulders (2 x collision radius) vs clear widths',
    fixtures: FX, waitsOn: Array.from(allWaits).sort(),
  };
}

/* ===================================== the walk ===================================== */
const FIRST = ['schema', 'rig', 'exportSymbol', 'hull', 'label', 'kind', 'derivedFromRigSha256', 'authoring'];
export function deriveWalk(boat, C) {
  const { G, H } = loadRig(boat), raw = H.walk();
  if (raw.schema !== SCHEMA) throw new Error(boat.rig + ': walk schema ' + raw.schema);
  if (raw.hull !== boat.hull || raw.rig !== boat.rig || raw.exportSymbol !== boat.sym) throw new Error(boat.rig + ': its walk names ' + raw.rig + ' / ' + raw.exportSymbol + ' / ' + raw.hull);
  const w = {};
  for (const k of FIRST) if (k in raw) w[k] = raw[k];
  w.derivedFromRigSha256 = lfSha(rigPath(boat));
  w.authoring = 'Written by the rig: ' + boat.sym + ".byId('" + boat.hull + "').walk(), every number its own (mm), stamped with the rig's LF sha256 and fitted to character rig " +
    CHARACTER.revision + ' (qa.character) by ' + WRITER + '. Regenerate with node ' + WRITER + ' --write; never edit by hand.';
  for (const k of Object.keys(raw)) if (!(k in w)) w[k] = raw[k];
  w.qa = Object.assign({}, raw.qa || {}, { character: fit(G, raw, C) });
  return JSON.parse(JSON.stringify(w));
}

function main(argv) {
  const write = argv.includes('--write'), showFit = argv.includes('--fit'), hi = argv.indexOf('--hull'), only = hi >= 0 ? argv[hi + 1] : null;
  const C = loadCharacter(); let stale = 0;
  for (const boat of BOATS.filter((b) => !only || b.hull === only)) {
    const w = deriveWalk(boat, C), file = walkPath(boat), text = serialize(w), rel = path.relative(ROOT, file).replace(/\\/g, '/');
    const old = fs.existsSync(file) ? lfText(file) : null, same = old === text;
    if (write && !same) { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, text); }
    console.log((same ? 'same    ' : write ? 'WROTE   ' : old == null ? 'MISSING ' : 'STALE   ') + rel + '  (' + Buffer.byteLength(text) + ' B, rig ' + w.derivedFromRigSha256.slice(0, 12) + ')');
    if (!same && !write) stale++;
    if (showFit) for (const f of w.qa.character.fixtures)
      console.log('   ' + String(f.builds_ok).padStart(2) + '/' + f.of + '  ' + f.id.padEnd(34) + ' ' + (f.clip || '-').padEnd(20) + ' ' + f.what +
        (f.fit ? '  fit: ' + f.fit.join(' ') : '') + (f.failing ? '  failing: ' + f.failing.join(' ') : '') + (f.waitsOn ? '  waits on ' + f.waitsOn.join(', ') : ''));
  }
  if (stale) { console.log(stale + ' walk(s) differ from their rig: run with --write'); process.exitCode = 1; }
}
if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) main(process.argv.slice(2));
