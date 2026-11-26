// The light station's yard pieces, drawn from numbers: the fog bell on its frame, the keeper's clothesline,
// a picket fence kit and a plank path kit.
//
//   node tools/rig-recipes/stp-light-pieces.mjs                 writes the pictures and the contract (never a meta)
//   node tools/rig-recipes/stp-light-pieces.mjs --check         exits 1 if the committed pictures or contract drifted
//   node tools/rig-recipes/stp-light-pieces.mjs --plates <dir>  writes a 4x review sheet of every piece (no Unity)
//
// Every picture is drawn about its GROUND POINT and padded so that point is the picture's exact centre: the
// importer's default pivot (centre) is then the ground point, and no meta needs a hand-set pivot. Unity writes
// the metas on first import.
//
// A run piece (picket panel, gate, plank) is drawn once per screen direction, every DIR_STEP degrees from 0
// (east) to 90 (north) on screen; a run going the other way is the same picture flipped in x. Its plan length
// is fixed, so a steep run (foreshortened by the camera) draws shorter on screen.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { encodePng } from './lib/png.mjs';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const OUT = 'Assets/_Project/Art/Sprites/StPeters/LightPieces';
const CONTRACT = OUT + '/LightPieces.json';

const PPM = 32;                                   // pixels a metre, the game's
const ELEV = 40 * Math.PI / 180;                  // the camera's elevation
const LIFT = Math.cos(ELEV);                      // picture rise per metre of height
const DEPTH = Math.sin(ELEV);                     // picture rise per metre of ground away from the camera
const DIR_STEP = 10;                              // degrees on screen between run directions
const DIRS = Array.from({ length: 90 / DIR_STEP + 1 }, (_, i) => i * DIR_STEP);

// Plan sizes (metres) the pictures are drawn at; the contract carries them to the Defs.
// A picket is square (width = depth) so it shows a face and a side at every run direction.
const PICKET = { plan: 1.5, height: 0.9, pickets: 10, width: 0.08, rails: [0.24, 0.64], rail: 0.04, railHeight: 0.08 };
const GATE = { plan: 1.0, height: 0.9, pickets: 6 };
const POST = { width: 0.13, height: 1.05 };
const PLANK = { plan: 1.0, width: 0.7, board: 0.2, bend: 0.9, end: 0.6 };
// The frame's beam tops out at the key scene's perch height for its gull (3.12 m over the ground).
const BELL = { half: [0.8, 0.4], plinth: 0.25, frame: 2.8, span: 1.25 };
// A drying line: posts 2 m (a head over a 1.8 m keeper), 6 m of line between them.
const LINE = { span: 6.0, post: 2.0 };
// A kitchen-garden bed: dug soil in a plank edging, rows of greens across it.
const BED = { half: [0.6, 2.0], edge: 0.1, board: 0.15, rows: 6, perRow: 3, plant: 0.3 };

// The palette: the station's whites and reds, the footbridge's timber, stone greys, a bronze bell.
const WHITE = [[216, 216, 208], [192, 196, 188], [164, 172, 164], [140, 148, 144], [116, 124, 124]];
const WOOD = [[157, 144, 124], [141, 128, 109], [126, 114, 97], [111, 100, 85], [96, 86, 73]];
const GAP = [62, 54, 45];
const EDGE = [[52, 46, 39], [76, 67, 56]];
const RED = [[180, 100, 84], [144, 72, 60], [120, 60, 52], [104, 64, 52]];
const STONE = [[150, 158, 156], [140, 152, 152], [116, 124, 124], [88, 96, 100], [64, 70, 76]];
const BRONZE = [[204, 170, 104], [168, 130, 74], [122, 92, 52], [74, 58, 40]];
const VERDI = [92, 128, 112];
const SOIL = [[104, 80, 60], [88, 66, 50], [72, 54, 42], [58, 44, 34]];
const GREEN = [[132, 160, 92], [104, 136, 76], [80, 112, 62], [60, 88, 52]];
const ROPE = [[176, 160, 122], [128, 112, 84]];
const CLOTH = {
  cream: [[208, 212, 204], [180, 184, 176], [152, 156, 150]],
  navy: [[76, 88, 112], [60, 72, 96], [44, 52, 72]],
  red: [[164, 84, 70], [144, 72, 60], [112, 56, 48]],
  teal: [[80, 108, 110], [62, 90, 94], [52, 76, 80]],
};

function hash(x, y, s) {
  let h = (x * 374761393 + y * 668265263 + s * 2147483647) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

/** A picture drawn about its ground point (0, 0), x right, y DOWN, in pixels; later draws cover earlier. */
function sketch() {
  const px = new Map();
  const set = (x, y, c) => { px.set(`${Math.round(x)},${Math.round(y)}`, c); };
  const get = (x, y) => px.get(`${Math.round(x)},${Math.round(y)}`);
  const line = (x0, y0, x1, y1, c) => {
    const n = Math.max(1, Math.ceil(Math.max(Math.abs(x1 - x0), Math.abs(y1 - y0))));
    for (let i = 0; i <= n; i++) set(x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n, typeof c === 'function' ? c(i / n) : c);
  };
  const rect = (x0, y0, x1, y1, c) => { for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) set(x, y, typeof c === 'function' ? c(x, y) : c); };
  return { px, set, get, line, rect };
}

/** Pads a sketch so its ground point is the exact centre of an even-sized picture. */
function pack(s) {
  let ax = 1, ay = 1;
  for (const k of s.px.keys()) {
    const [x, y] = k.split(',').map(Number);
    ax = Math.max(ax, x < 0 ? -x : x + 1);
    ay = Math.max(ay, y < 0 ? -y : y + 1);
  }
  const w = 2 * ax, h = 2 * ay, rgba = Buffer.alloc(w * h * 4);
  for (const [k, c] of s.px) {
    const [x, y] = k.split(',').map(Number);
    const i = ((y + ay) * w + (x + ax)) * 4;
    rgba[i] = c[0]; rgba[i + 1] = c[1]; rgba[i + 2] = c[2]; rgba[i + 3] = 255;
  }
  return { w, h, rgba };
}

/** A run's screen direction (degrees) as a plan direction and the screen vector, in pixels, of one plan metre. */
function runVector(deg) {
  const t = deg * Math.PI / 180;
  const phi = deg === 90 ? Math.PI / 2 : Math.atan(Math.tan(t) / DEPTH);
  return { phi, sx: Math.cos(phi) * PPM, sy: -Math.sin(phi) * DEPTH * PPM };
}

// ---------------------------------------------------------------------------------------------------------
//  the picket fence
// ---------------------------------------------------------------------------------------------------------

/**
 * A tiny box tracer: the fence is built of real boxes (square pickets with a stepped cap, rails, a brace) and every
 * pixel is the nearest box face along the game camera's ray, shaded by that face's facing. So a run reads as the same
 * fence at every direction: square pickets show a face and a side, and overlap the way solid things overlap. The
 * pickets are square (as wide as they are deep) so a run going straight away from the camera still shows each one as
 * a lit post head stacked over the next, not a one-pixel line.
 */
const VIEW = [0, Math.cos(ELEV), -Math.sin(ELEV)];          // the camera's ray: north and down
const SUN = (() => { const v = [-0.62, -0.38, 0.69]; const n = Math.hypot(...v); return v.map((x) => x / n); })();

/** A box: centre (plan x, y, height z), half sizes along the run (a), across it (b) and up (c), turned phi in plan. */
function box(cx, cy, cz, a, b, c, phi, paint, key) {
  return { cx, cy, cz, a, b, c, u: [Math.cos(phi), Math.sin(phi)], w: [-Math.sin(phi), Math.cos(phi)], paint, key };
}

/** Where the ray through screen pixel (x, y) (y down, ground point at 0, 0) first meets a box, and the face it meets. */
function trace(boxes, x, y) {
  const X = (x + 0.5) / PPM, Y0 = -(y + 0.5) / (DEPTH * PPM);
  let best = null;
  for (const bx of boxes) {
    const o = [X - bx.cx, Y0 - bx.cy, -bx.cz];
    const lo = [o[0] * bx.u[0] + o[1] * bx.u[1], o[0] * bx.w[0] + o[1] * bx.w[1], o[2]];
    const ld = [VIEW[0] * bx.u[0] + VIEW[1] * bx.u[1], VIEW[0] * bx.w[0] + VIEW[1] * bx.w[1], VIEW[2]];
    const half = [bx.a, bx.b, bx.c];
    let t0 = -Infinity, t1 = Infinity, axis = -1, sign = 0;
    let miss = false;
    for (let k = 0; k < 3; k++) {
      if (Math.abs(ld[k]) < 1e-9) { if (Math.abs(lo[k]) > half[k]) { miss = true; break; } continue; }
      let ta = (-half[k] - lo[k]) / ld[k], tb = (half[k] - lo[k]) / ld[k];
      let s = -1;
      if (ta > tb) { const q = ta; ta = tb; tb = q; s = 1; }
      if (ta > t0) { t0 = ta; axis = k; sign = s; }
      if (tb < t1) t1 = tb;
      if (t0 > t1) { miss = true; break; }
    }
    if (miss || axis < 0) continue;
    if (!best || t0 < best.t) {
      // The face's normal in the world: +/- the run, +/- across it, or up.
      const n = axis === 0 ? [bx.u[0] * sign, bx.u[1] * sign, 0] : axis === 1 ? [bx.w[0] * sign, bx.w[1] * sign, 0] : [0, 0, sign];
      best = { t: t0, n, bx, z: lo[2] + ld[2] * t0 + bx.cz };
    }
  }
  return best;
}

/** A face's tone 0 (lit) .. 4 (deep shade) from how squarely it takes the sun; a top always reads lightest. */
function toneOf(n) {
  if (n[2] > 0.5) return 0;
  const lam = n[0] * SUN[0] + n[1] * SUN[1] + n[2] * SUN[2];
  return lam > 0.35 ? 1 : lam > 0.05 ? 2 : lam > -0.3 ? 3 : 4;
}

/** Draws the boxes into a sketch, then a dark contact pixel under each box's foot where it meets open ground. */
function render(s, boxes, reach) {
  const R = Math.ceil(reach * PPM) + 2;
  const hit = new Map();
  for (let y = -R * 2; y <= R; y++) for (let x = -R; x <= R; x++) {
    const h = trace(boxes, x, y);
    if (!h) continue;
    hit.set(`${x},${y}`, h);
    s.set(x, y, h.bx.paint(toneOf(h.n), x, y, h));
  }
  for (const [k, h] of hit) {
    const [x, y] = k.split(',').map(Number);
    if (h.z < 0.04 && h.bx.cz - h.bx.c < 0.01 && !hit.has(`${x},${y + 1}`)) s.set(x, y + 1, EDGE[1]);
  }
}

function paintOf(state, seed) {
  if (state === 'fresh') return (tone) => WHITE[Math.min(tone, 4)];
  // Weathered: the white has flaked to grey timber in small patches, more low down where the rain splashes.
  const share = state === 'broken' ? 0.2 : 0.12;
  return (tone, x, y, h) => {
    const low = h && h.z < 0.3 ? 0.08 : 0;
    const flake = hash(Math.floor(x / 2), Math.floor(y / 3), seed) < share + low;
    const t = Math.min(tone + (state === 'broken' ? 1 : 0), 4);
    return flake ? WOOD[Math.min(t + 1, 4)] : [WHITE[t][0] - 6, WHITE[t][1] - 2, WHITE[t][2] - 6];
  };
}

/** The pickets, rails and (on a gate) brace of one bay, as boxes about the bay's ground centre. */
function picketBoxes(phi, plan, count, height, paint, opts = {}) {
  const P = PICKET, boxes = [];
  const u = [Math.cos(phi), Math.sin(phi)], w = [-Math.sin(phi), Math.cos(phi)];
  const at = (along, across) => [u[0] * along + w[0] * across, u[1] * along + w[1] * across];
  const ph = P.width / 2, cap = P.width / 4;
  // The rails sit on the far side of the pickets (the inside of the fence), so they show between them.
  const railOff = ph + P.rail / 2;
  for (const r of P.rails) {
    const parts = opts.snapTop && r === P.rails[1] ? [[-0.5, -0.12], [0.16, 0.5]] : [[-0.5, 0.5]];
    for (const [a, b] of parts) {
      const [x, y] = at(plan * (a + b) / 2, railOff);
      boxes.push(box(x, y, r, plan * (b - a) / 2, P.rail / 2, P.railHeight / 2, phi, paint, 'rail'));
    }
  }
  if (opts.brace) {
    // A gate's brace, from the low rail at the hinge to the high rail at the latch, as a run of short blocks.
    const n = 10;
    for (let k = 0; k < n; k++) {
      const t = (k + 0.5) / n;
      const [x, y] = at(plan * (t - 0.5), railOff);
      boxes.push(box(x, y, P.rails[0] + (P.rails[1] - P.rails[0]) * t, plan / n / 2, P.rail / 2, P.railHeight / 2, phi, paint, 'brace'));
    }
  }
  for (let i = 0; i < count; i++) {
    if (opts.skip && opts.skip.includes(i)) continue;
    const t = (i + 0.5) / count;
    let h = height - (opts.ragged ? Math.floor(hash(i, 7, opts.seed) * 3) * 0.02 : 0);
    if (opts.crown) h += 0.06 * Math.sin(Math.PI * t);
    const [x, y] = at(plan * (t - 0.5), 0);
    boxes.push(box(x, y, h / 2, ph, ph, h / 2, phi, paint, 'picket'));
    boxes.push(box(x, y, h + cap / 2, cap, cap, cap / 2, phi, paint, 'cap'));
  }
  if (opts.fallen) {
    // One picket lies in the grass in front of the gap.
    const [x, y] = at(plan * -0.05, -0.32);
    boxes.push(box(x, y, ph, height * 0.45, ph, ph, phi + 0.35, paint, 'fallen'));
  }
  return boxes;
}

function picketPanel(deg, state) {
  const s = sketch(), v = runVector(deg), seed = deg * 7 + (state === 'fresh' ? 1 : state === 'weathered' ? 2 : 3);
  const opts = { seed, ragged: state !== 'fresh' };
  if (state === 'weathered') opts.skip = [6];
  if (state === 'broken') { opts.skip = [4, 5]; opts.snapTop = true; opts.fallen = true; }
  render(s, picketBoxes(v.phi, PICKET.plan, PICKET.pickets, PICKET.height, paintOf(state, seed), opts), PICKET.plan);
  return s;
}

function picketGate(deg) {
  const s = sketch(), v = runVector(deg);
  render(s, picketBoxes(v.phi, GATE.plan, GATE.pickets, GATE.height, paintOf('fresh'), { brace: true, crown: true }), GATE.plan);
  return s;
}

function post(kind) {
  const s = sketch(), w = Math.round(POST.width * PPM), h = Math.round(POST.height * LIFT * PPM);
  const x0 = -Math.floor(w / 2);
  for (let y = 0; y < h; y++) for (let i = 0; i < w; i++) s.set(x0 + i, -y, WHITE[i === 0 ? 0 : i === w - 1 ? 3 : i === w - 2 ? 2 : 1]);
  // Its top face, seen from above, and the cap.
  for (let i = 0; i < w; i++) s.set(x0 + i, -h, WHITE[0]);
  if (kind === 'corner') {
    for (let i = 0; i < w; i++) s.set(x0 + i, -h - 1, WHITE[i < w / 2 ? 1 : 2]);
    for (let i = 1; i < w - 1; i++) s.set(x0 + i, -h - 2, WHITE[i < w / 2 ? 0 : 2]);
  } else {
    // An end post carries a turned ball finial.
    s.set(0, -h - 1, WHITE[3]); s.set(-1, -h - 1, WHITE[3]);
    for (let i = -2; i <= 1; i++) { s.set(i, -h - 2, WHITE[i < 0 ? 0 : 2]); s.set(i, -h - 3, WHITE[i < 0 ? 0 : 1]); }
    s.set(-1, -h - 4, WHITE[0]); s.set(0, -h - 4, WHITE[1]);
  }
  s.set(x0, 0, WHITE[3]); s.set(x0 + w - 1, 0, WHITE[4]);
  return s;
}

// ---------------------------------------------------------------------------------------------------------
//  the plank path
// ---------------------------------------------------------------------------------------------------------

/** Boards laid across the walk, flat on the ground, with a dark edge where the near side lifts off the grass. */
function planks(deg, kind) {
  const s = sketch(), v = runVector(deg);
  const len = kind === 'bend' ? PLANK.bend : kind === 'end' ? PLANK.end : PLANK.plan;
  const half = (kind === 'bend' ? PLANK.bend : PLANK.width) / 2;
  const ux = Math.cos(v.phi), uy = Math.sin(v.phi);            // along the walk, in plan
  const wx = -uy, wy = ux;                                     // across it
  const R = Math.ceil((len + 2 * half) * PPM);
  const inside = (x, y) => {
    const px = x / PPM, py = -y / (DEPTH * PPM);               // screen pixel to plan metres
    const u = px * ux + py * uy, w = px * wx + py * wy;
    return { ok: Math.abs(u) <= len / 2 && Math.abs(w) <= half, u, w };
  };
  const seed = deg * 13 + (kind === 'bend' ? 5 : kind === 'end' ? 9 : 1);
  for (let y = -R; y <= R; y++) for (let x = -R; x <= R; x++) {
    const p = inside(x, y);
    if (!p.ok) {
      if (inside(x, y - 1).ok) s.set(x, y, EDGE[hash(x, y, seed) < 0.5 ? 0 : 1]);
      continue;
    }
    const board = Math.floor((p.u + len / 2) / PLANK.board);
    const f = (p.u + len / 2) - board * PLANK.board;
    if (f < 0.75 / PPM || f > PLANK.board - 0.45 / PPM) { s.set(x, y, GAP); continue; }
    // Each board its own grey-brown, a grain along it, the ends worn darker, a nail at each joist.
    let tone = 1 + Math.floor(hash(board, 3, seed) * 3);
    if (hash(Math.round(p.w * 9), board, seed) < 0.18) tone += 1;
    if (Math.abs(p.w) > half - 0.6 / PPM) tone = Math.min(4, tone + 1);
    if (Math.abs(Math.abs(p.w) - half * 0.7) < 0.5 / PPM && Math.abs(f - PLANK.board / 2) < 0.6 / PPM) { s.set(x, y, EDGE[1]); continue; }
    s.set(x, y, WOOD[Math.min(4, tone)]);
  }
  if (kind === 'end') {
    // The walk ends on a flat stone set in the turf beyond the last board.
    const cx = ux * (len / 2 + 0.28) * PPM, cy = -uy * (len / 2 + 0.28) * DEPTH * PPM;
    const rx = 0.32 * PPM, ry = 0.24 * DEPTH * PPM;
    for (let y = Math.floor(cy - ry) - 1; y <= Math.ceil(cy + ry) + 1; y++) for (let x = Math.floor(cx - rx) - 1; x <= Math.ceil(cx + rx) + 1; x++) {
      const d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2;
      if (d <= 1) s.set(x, y, STONE[d > 0.7 ? 2 : (y - cy) < -ry * 0.3 ? 0 : 1]);
      else if (d <= 1.35 && y > cy) s.set(x, y, STONE[4]);
    }
  }
  return s;
}

// ---------------------------------------------------------------------------------------------------------
//  the fog bell on its frame
// ---------------------------------------------------------------------------------------------------------

function fogBell() {
  const s = sketch();
  const hx = Math.round(BELL.half[0] * PPM), hd = Math.round(BELL.half[1] * DEPTH * PPM), ph = Math.round(BELL.plinth * LIFT * PPM);
  // The plinth: a dressed-stone block, its top seen from above, its south face in shade, mortar courses.
  s.rect(-hx, -hd - ph, hx - 1, hd - ph, (x, y) => STONE[hash(x, y, 41) < 0.15 ? 0 : 1]);
  s.rect(-hx, hd - ph + 1, hx - 1, hd, (x, y) => (y - (hd - ph + 1)) === 2 || ((x + hx + (y > hd - 2 ? 5 : 0)) % 11 === 0) ? STONE[4] : STONE[hash(x, y, 43) < 0.2 ? 2 : 3]);
  s.line(-hx, -hd - ph, hx - 1, -hd - ph, STONE[0]);
  const sx = Math.round(BELL.span / 2 * PPM), base = -ph, fh = Math.round(BELL.frame * LIFT * PPM), top = base - fh;
  // Two white-washed posts, lit on the west.
  for (const px of [-sx, sx]) for (let y = base; y >= top; y--) {
    s.set(px - 2, y, WHITE[0]); s.set(px - 1, y, WHITE[1]); s.set(px, y, WHITE[1]); s.set(px + 1, y, WHITE[3]);
  }
  // Knee braces.
  s.line(-sx + 2, top + 10, -sx + 10, top + 3, WHITE[2]);
  s.line(sx - 3, top + 10, sx - 11, top + 3, WHITE[3]);
  // The beam and its little red roof.
  s.rect(-sx - 4, top - 1, sx + 3, top + 2, (x, y) => WHITE[y === top - 1 ? 0 : y === top + 2 ? 3 : 1]);
  for (let r = 0; r < 5; r++) {
    const y = top - 2 - r, w = sx + 6 - r * 2;
    for (let x = -w; x < w; x++) s.set(x, y, RED[r === 0 ? 3 : x < -w + 2 || r === 4 ? 0 : x > w - 3 ? 2 : 1]);
  }
  s.line(-sx - 6, top - 2, sx + 5, top - 2, RED[3]);
  // The yoke and the bell: crown, waist, flare, a dark lip, a verdigris streak, the clapper showing.
  s.rect(-3, top + 3, 2, top + 4, EDGE[1]);
  const bh = Math.round(0.62 * LIFT * PPM), b0 = top + 5;
  for (let r = 0; r < bh; r++) {
    const t = r / (bh - 1);
    const w = Math.round(3 + 2 * t + 3.5 * t * t * t);
    for (let x = -w; x < w; x++) {
      const k = (x + w) / (2 * w);
      let c = k < 0.2 ? BRONZE[0] : k < 0.62 ? BRONZE[1] : k < 0.86 ? BRONZE[2] : BRONZE[3];
      if (r === 0) c = BRONZE[2];
      if (r === bh - 1) c = BRONZE[3];
      if (r === bh - 3) c = k < 0.5 ? BRONZE[0] : BRONZE[2];
      if (x === Math.round(w * 0.35) && r > 2 && r < bh - 3 && hash(r, 1, 7) < 0.7) c = VERDI;
      s.set(x, b0 + r, c);
    }
  }
  s.set(-1, b0 + bh, EDGE[0]); s.set(0, b0 + bh, EDGE[0]); s.set(0, b0 + bh + 1, BRONZE[3]);
  // The lanyard from the clapper to a cleat on the east post.
  s.line(1, b0 + bh + 1, sx - 2, base - 14, (t) => ROPE[t < 0.5 ? 0 : 1]);
  s.line(sx - 2, base - 14, sx - 2, base - 10, ROPE[1]);
  s.set(sx - 3, base - 12, EDGE[0]); s.set(sx - 1, base - 12, EDGE[0]);
  return s;
}

// ---------------------------------------------------------------------------------------------------------
//  the clothesline
// ---------------------------------------------------------------------------------------------------------

/**
 * A garment hung by its top edge at (cx, lineY), drawn at true size: a metre of cloth hanging is LIFT * PPM pixels
 * tall (it hangs in the vertical), a metre across is PPM pixels.
 */
function garment(s, cx, lineY, kind) {
  const C = kind.colour, V = LIFT * PPM, U = PPM;
  const shade = (x0, x1, x) => x <= x0 ? 0 : x >= x1 ? 2 : 1;
  const put = (x, y, c) => s.set(cx + x, lineY + 1 + y, c);
  if (kind.shape === 'shirt') {
    // 0.6 m across the shoulders, 0.75 m long, sleeves hanging 0.3 m.
    const sh = Math.round(0.3 * U), bw = Math.round(0.22 * U), h = Math.round(0.75 * V), sl = Math.round(0.3 * V);
    for (let y = 0; y < h; y++) {
      const w = y < sl ? sh : bw;
      for (let x = -w; x < w; x++) put(x, y, C[y === h - 1 ? 2 : (y >= sl && Math.abs(x) === bw - 1 && x > 0) ? 2 : shade(-w, w - 1, x)]);
    }
    for (let y = 1; y < h - 2; y += 4) put(0, y, C[2]);
    put(-2, 1, C[2]); put(1, 1, C[2]);
  } else if (kind.shape === 'trousers') {
    // 0.4 m at the waist, 1.0 m long, the legs parting a third of the way down.
    const w = Math.round(0.2 * U), h = Math.round(1.0 * V), fork = Math.round(0.32 * V);
    for (let y = 0; y < h; y++) for (let x = -w; x < w; x++) {
      if (y > fork && (x === -1 || x === 0)) continue;
      put(x, y, C[y === 0 ? 0 : y === h - 1 ? 2 : shade(-w, w - 1, x)]);
    }
  } else if (kind.shape === 'sheet') {
    // A sheet folded over the line: 1.3 m across, 0.9 m hanging, a red stripe near one edge.
    const w = Math.round(0.65 * U), h = Math.round(0.9 * V);
    for (let y = 0; y < h; y++) for (let x = -w; x < w; x++) {
      const hem = y === h - 1 || (y === h - 2 && (x + w) % 6 === 0);
      const stripe = x === w - 7 || x === w - 6;
      const drop = x > w / 3 ? 1 : 0;
      put(x, y + drop, stripe ? CLOTH.red[x === w - 7 ? 1 : 2] : C[hem ? 2 : shade(-w, w - 2, x)]);
    }
  } else if (kind.shape === 'jumper') {
    // A heavy jumper pegged by its hem: 0.55 m wide, sleeves to 0.75 m across, 0.6 m long.
    const w = Math.round(0.28 * U), sw = Math.round(0.38 * U), h = Math.round(0.6 * V), sl = Math.round(0.28 * V);
    for (let y = 0; y < h; y++) {
      const ww = y < sl ? sw : w;
      for (let x = -ww; x < ww; x++) put(x, y, C[(y === h - 1 || (y === sl && Math.abs(x) >= w)) ? 2 : (y % 3 === 1 && x % 2 === 0) ? 1 : shade(-ww, ww - 1, x)]);
    }
  } else if (kind.shape === 'towel') {
    // A towel, 0.5 m by 0.7 m, two bands across it.
    const w = Math.round(0.25 * U), h = Math.round(0.7 * V);
    for (let y = 0; y < h; y++) for (let x = -w; x < w; x++) {
      const band = (y > h * 0.7 && y < h * 0.7 + 2) || (y > h * 0.8 && y < h * 0.8 + 2);
      put(x, y, band ? CLOTH.navy[1] : C[y === h - 1 ? 2 : shade(-w, w - 1, x)]);
    }
  } else if (kind.shape === 'socks') {
    // A pair of socks, 0.4 m long, the feet turned out.
    const h = Math.round(0.4 * V);
    for (const dx of [-4, 3]) {
      for (let y = 0; y < h; y++) { put(dx, y, C[0]); put(dx + 1, y, C[1]); put(dx + 2, y, C[2]); }
      const toe = dx < 0 ? -1 : 1;
      for (let k = 0; k < 3; k++) { put(dx + 1 + toe * k, h, C[1]); put(dx + 1 + toe * k, h + 1, C[2]); }
    }
  }
  // The pegs.
  s.set(cx - 3, lineY, WOOD[0]); s.set(cx - 3, lineY - 1, WOOD[1]); s.set(cx + 2, lineY, WOOD[0]); s.set(cx + 2, lineY - 1, WOOD[1]);
}

function clothesline() {
  const s = sketch();
  const hx = Math.round(LINE.span / 2 * PPM), ph = Math.round(LINE.post * LIFT * PPM);
  // Two grey timber posts, each with a short cross-arm and a brace to the ground.
  for (const px of [-hx, hx]) {
    for (let y = 0; y <= ph; y++) { s.set(px - 2, -y, WOOD[0]); s.set(px - 1, -y, WOOD[1]); s.set(px, -y, WOOD[2]); s.set(px + 1, -y, WOOD[4]); }
    s.rect(px - 6, -ph - 1, px + 5, -ph + 1, (x, y) => WOOD[y === -ph - 1 ? 0 : y === -ph + 1 ? 4 : 2]);
    s.set(px - 1, -ph - 2, WOOD[0]); s.set(px, -ph - 2, WOOD[1]);
    const dir = px < 0 ? 1 : -1;
    s.line(px + dir * 2, -18, px + dir * 10, 0, WOOD[3]);
    s.set(px - 2, 1, EDGE[1]); s.set(px - 1, 1, EDGE[1]); s.set(px, 1, EDGE[1]); s.set(px + 1, 1, EDGE[1]);
  }
  // The line, sagging a hand's breadth, and what hangs on it.
  const sag = Math.round(0.25 * LIFT * PPM);
  const lineY = (x) => -ph + 1 + Math.round(sag * (1 - (x / hx) ** 2));
  for (let x = -hx; x <= hx; x++) s.set(x, lineY(x), ROPE[x < 0 ? 0 : 1]);
  const kit = [
    { at: -0.8, shape: 'shirt', colour: CLOTH.cream },
    { at: -0.58, shape: 'trousers', colour: CLOTH.navy },
    { at: -0.27, shape: 'sheet', colour: CLOTH.cream },
    { at: 0.1, shape: 'towel', colour: CLOTH.red },
    { at: 0.33, shape: 'jumper', colour: CLOTH.teal },
    { at: 0.55, shape: 'socks', colour: CLOTH.red },
    { at: 0.76, shape: 'shirt', colour: CLOTH.navy },
  ];
  for (const k of kit) { const cx = Math.round(k.at * hx); garment(s, cx, lineY(cx), k); }
  return s;
}

// ---------------------------------------------------------------------------------------------------------
//  the kitchen-garden bed
// ---------------------------------------------------------------------------------------------------------

/** A dug bed in a plank edging, seen from above, its south board's face showing, rows of greens across it. */
function gardenBed() {
  const s = sketch();
  const hx = Math.round(BED.half[0] * PPM), hd = Math.round(BED.half[1] * DEPTH * PPM);
  const ex = Math.max(2, Math.round(BED.edge * PPM)), ey = Math.max(2, Math.round(BED.edge * DEPTH * PPM));
  const bf = Math.round(BED.board * LIFT * PPM);
  // The soil, raked into ridges along the rows; the edging boards round it; the south board's face below.
  const rowGap = (2 * hd - 2 * ey) / BED.rows;
  const lift = -bf;                                           // the bed's top stands a board's height up
  for (let y = -hd; y <= hd; y++) for (let x = -hx; x < hx; x++) {
    const edge = x < -hx + ex || x >= hx - ex || y < -hd + ey || y > hd - ey;
    let c;
    if (edge) c = WOOD[(y < -hd + ey || x < -hx + ex) ? 1 : 2];
    else {
      const f = ((y + hd - ey) % rowGap) / rowGap;
      c = SOIL[f < 0.25 ? 3 : f < 0.5 ? 2 : hash(x, y, 61) < 0.2 ? 0 : 1];
    }
    s.set(x, y + lift, c);
  }
  for (let y = 0; y < bf; y++) for (let x = -hx; x < hx; x++) s.set(x, hd + lift + 1 + y, WOOD[y === 0 ? 2 : x < -hx + 2 ? 3 : 4]);
  for (let x = -hx; x < hx; x++) s.set(x, hd + lift + bf + 1, EDGE[1]);
  // The greens: round heads in rows, the far rows first so the near ones stand over them.
  const pw = Math.round(BED.plant * PPM / 2), ph = Math.round(BED.plant * LIFT * PPM);
  for (let r = 0; r < BED.rows; r++) {
    const ry = Math.round(-hd + ey + rowGap * (r + 0.5)) + lift;
    for (let k = 0; k < BED.perRow; k++) {
      const cx = Math.round(-hx + ex + (2 * hx - 2 * ex) * (k + 0.5) / BED.perRow) + (r % 2 ? 1 : -1);
      const size = 0.8 + 0.2 * hash(r, k, 67);
      const rx = Math.max(2, Math.round(pw * size)), ry2 = Math.max(2, Math.round(ph * size / 2));
      for (let y = -2 * ry2; y <= 1; y++) for (let x = -rx; x <= rx; x++) {
        const d = (x / rx) ** 2 + ((y + ry2) / ry2) ** 2;
        if (d > 1) continue;
        const lit = x < 0 && y < -ry2;
        const leaf = hash(x + cx, y + ry, 71) < 0.18;
        s.set(cx + x, ry + y, GREEN[lit ? 0 : d > 0.7 ? 3 : leaf ? 2 : 1]);
      }
      s.set(cx - rx + 1, ry + 1, SOIL[3]); s.set(cx + rx - 1, ry + 1, SOIL[3]);
    }
  }
  return s;
}

// ---------------------------------------------------------------------------------------------------------
//  the set
// ---------------------------------------------------------------------------------------------------------

const r4 = (n) => Math.round(n * 1e4) / 1e4;
const dirName = (d) => 'a' + String(d).padStart(2, '0');

function build() {
  const files = {}, pieces = [];
  const add = (file, s) => { const p = pack(s); files[file] = p; return { sprite: file, widthPx: p.w, heightPx: p.h }; };
  const runs = (name, draw, plan, extra = {}) => {
    const sprites = DIRS.map((d) => add(`${name}/${name}_${dirName(d)}.png`, draw(d)).sprite);
    pieces.push({ name, kind: 'run', directionStepDegrees: DIR_STEP, sprites, planLengthMetres: plan, ...extra });
  };
  for (const state of ['fresh', 'weathered', 'broken'])
    runs(`PicketPanel_${state}`, (d) => picketPanel(d, state), PICKET.plan, { layer: 'raised', heightMetres: PICKET.height });
  runs('PicketGate', (d) => picketGate(d), GATE.plan, { layer: 'raised', heightMetres: GATE.height });
  runs('Plank', (d) => planks(d, 'straight'), PLANK.plan, { layer: 'floor', widthMetres: PLANK.width });
  runs('PlankBend', (d) => planks(d, 'bend'), PLANK.bend, { layer: 'floor', widthMetres: PLANK.bend });
  runs('PlankEnd', (d) => planks(d, 'end'), PLANK.end, { layer: 'floor', widthMetres: PLANK.width });
  for (const kind of ['corner', 'end']) {
    const name = kind === 'corner' ? 'PicketCorner' : 'PicketEnd';
    pieces.push({ name, kind: 'single', layer: 'raised', ...add(`${name}.png`, post(kind)), footprintHalfMetres: { x: r4(POST.width / 2), y: r4(POST.width / 2 * DEPTH) } });
  }
  pieces.push({
    name: 'FogBell', kind: 'single', layer: 'raised', ...add('FogBell.png', fogBell()), heightMetres: r4(BELL.plinth + BELL.frame),
    footprintHalfMetres: { x: BELL.half[0], y: r4(BELL.half[1] * DEPTH) },
  });
  pieces.push({
    name: 'Clothesline', kind: 'single', layer: 'raised', ...add('Clothesline.png', clothesline()), heightMetres: LINE.post,
    postsAtMetres: [-LINE.span / 2, LINE.span / 2], postHalfMetres: { x: 0.08, y: r4(0.08 * DEPTH) },
  });
  pieces.push({
    name: 'GardenBed', kind: 'single', layer: 'raised', ...add('GardenBed.png', gardenBed()), heightMetres: BED.board,
    footprintHalfMetres: { x: BED.half[0], y: r4(BED.half[1] * DEPTH) },
  });
  const contract = {
    schema: 'stp-light-pieces@1',
    recipe: 'tools/rig-recipes/stp-light-pieces.mjs',
    pixelsPerMetre: PPM, elevationDeg: 40, pivot: 'the ground point is every picture\'s centre',
    directionsDegrees: DIRS, runFlipsX: 'a run whose screen direction falls is the rising picture flipped in x',
    pieces,
  };
  return { files, contract: JSON.stringify(contract, null, 2) + '\n' };
}

/** A 4x review sheet: every picture on a neutral ground, with its ground point marked. */
function plates(built, dir) {
  const names = Object.keys(built.files), Z = 4, pad = 6;
  const groups = {};
  for (const n of names) (groups[n.includes('/') ? n.split('/')[0] : 'singles'] ??= []).push(n);
  for (const [g, list] of Object.entries(groups)) {
    const cells = list.map((n) => built.files[n]);
    const W = cells.reduce((a, c) => a + (c.w + pad) * Z, pad * Z), H = Math.max(...cells.map((c) => c.h)) * Z + pad * 2 * Z;
    const out = Buffer.alloc(W * H * 4);
    for (let i = 0; i < W * H; i++) { const k = ((i % W) >> 4) + ((Math.floor(i / W)) >> 4); out[i * 4] = k % 2 ? 118 : 126; out[i * 4 + 1] = k % 2 ? 132 : 140; out[i * 4 + 2] = k % 2 ? 104 : 110; out[i * 4 + 3] = 255; }
    let ox = pad * Z;
    for (const c of cells) {
      const oy = pad * Z;
      for (let y = 0; y < c.h; y++) for (let x = 0; x < c.w; x++) {
        const s = (y * c.w + x) * 4; if (!c.rgba[s + 3]) continue;
        for (let dy = 0; dy < Z; dy++) for (let dx = 0; dx < Z; dx++) { const d = ((oy + y * Z + dy) * W + ox + x * Z + dx) * 4; c.rgba.copy(out, d, s, s + 4); }
      }
      const gx = ox + c.w / 2 * Z, gy = oy + c.h / 2 * Z;
      for (let k = -3; k <= 3; k++) for (const [x, y] of [[gx + k, gy], [gx, gy + k]]) { const d = (y * W + x) * 4; out[d] = 255; out[d + 1] = 40; out[d + 2] = 200; }
      ox += (c.w + pad) * Z;
    }
    fs.writeFileSync(path.join(dir, `LightPieces_${g}_x4.png`), encodePng(out, W, H));
  }
}

const args = process.argv.slice(2);
const built = build();
if (args[0] === '--plates') {
  const dir = args[1]; fs.mkdirSync(dir, { recursive: true });
  plates(built, dir);
  console.log('plates written to', dir);
} else if (args[0] === '--check') {
  const bad = [];
  for (const [f, v] of Object.entries(built.files)) {
    const p = path.join(REPO, OUT, f);
    if (!fs.existsSync(p) || Buffer.compare(fs.readFileSync(p), encodePng(v.rgba, v.w, v.h)) !== 0) bad.push(f);
  }
  const c = path.join(REPO, CONTRACT);
  if (!fs.existsSync(c) || fs.readFileSync(c, 'utf8').replace(/\r\n/g, '\n') !== built.contract) bad.push('LightPieces.json');
  if (bad.length) { console.error('drifted from the recipe:\n  ' + bad.join('\n  ')); process.exit(1); }
  console.log('light pieces: the committed pictures and contract are the recipe\'s.');
} else {
  for (const [f, v] of Object.entries(built.files)) {
    const p = path.join(REPO, OUT, f);
    fs.mkdirSync(path.dirname(p), { recursive: true });
    fs.writeFileSync(p, encodePng(v.rgba, v.w, v.h));
  }
  fs.writeFileSync(path.join(REPO, CONTRACT), built.contract);
  console.log(`light pieces: ${Object.keys(built.files).length} pictures and the contract written to ${OUT}`);
}
