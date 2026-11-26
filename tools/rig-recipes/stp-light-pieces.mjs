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
const PICKET = { plan: 1.5, height: 0.85, pickets: 9, width: 0.07, rails: [0.22, 0.62] };
const GATE = { plan: 1.0, height: 0.85, pickets: 6 };
const POST = { width: 0.13, height: 1.0 };
const PLANK = { plan: 1.0, width: 0.7, board: 0.2, bend: 0.9, end: 0.6 };
const BELL = { half: [0.8, 0.4], plinth: 0.25, frame: 2.0, span: 1.25 };
const LINE = { span: 3.6, post: 1.9 };

// The palette: the station's whites and reds, the footbridge's timber, stone greys, a bronze bell.
const WHITE = [[216, 216, 208], [192, 196, 188], [164, 172, 164], [140, 148, 144], [116, 124, 124]];
const WOOD = [[157, 144, 124], [141, 128, 109], [126, 114, 97], [111, 100, 85], [96, 86, 73]];
const GAP = [62, 54, 45];
const EDGE = [[52, 46, 39], [76, 67, 56]];
const RED = [[180, 100, 84], [144, 72, 60], [120, 60, 52], [104, 64, 52]];
const STONE = [[150, 158, 156], [140, 152, 152], [116, 124, 124], [88, 96, 100], [64, 70, 76]];
const BRONZE = [[204, 170, 104], [168, 130, 74], [122, 92, 52], [74, 58, 40]];
const VERDI = [92, 128, 112];
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

/** One picket stood at (bx, by): a lit edge, its face, a shaded edge, a pointed head. */
function picket(s, bx, by, h, faceW, paint, lean = 0) {
  const x0 = Math.round(bx - faceW / 2);
  for (let i = 0; i < faceW; i++) {
    const tone = faceW === 1 ? 2 : i === 0 ? 0 : i === faceW - 1 ? 2 : 1;
    const top = (faceW >= 3 && (i === 0 || i === faceW - 1)) ? 1 : 0;
    for (let y = 0; y < h - top; y++) {
      const shift = lean ? Math.round(lean * y / h) : 0;
      s.set(x0 + i + shift, by - y, paint(tone + (y === 0 ? 1 : 0), x0 + i, by - y));
    }
  }
  if (faceW < 3) for (let y = 1; y < h - 1; y++) s.set(x0 + faceW + (lean ? Math.round(lean * y / h) : 0), by - y, paint(3, x0 + faceW, by - y));
  if (faceW < 3) {
    // Seen end-on, a picket is a lit point over a shadow, so a steep run reads as a ladder of heads, not a pole.
    const tip = by - (h - 1), sh = lean ? Math.round(lean) : 0;
    for (let i = 0; i <= faceW; i++) { s.set(x0 + i + sh, tip, WHITE[0]); s.set(x0 + i + sh, tip + 1, WHITE[4]); }
  }
}

function paintOf(state, seed) {
  if (state === 'fresh') return (tone) => WHITE[Math.min(tone, 4)];
  // Weathered: the white has flaked to grey timber in places, more low down where the rain splashes.
  return (tone, x, y) => {
    const flake = hash(x, Math.floor(y / 4) + Math.floor(hash(x, 0, seed) * 4), seed) < (state === 'broken' ? 0.34 : 0.22);
    return flake ? WOOD[Math.min(tone + 1, 4)] : [WHITE[Math.min(tone + 1, 4)][0] - 6, WHITE[Math.min(tone + 1, 4)][1] - 2, WHITE[Math.min(tone + 1, 4)][2] - 6];
  };
}

function picketPanel(deg, state) {
  const s = sketch(), v = runVector(deg), seed = deg * 7 + (state === 'fresh' ? 1 : state === 'weathered' ? 2 : 3);
  const L = PICKET.plan, H = Math.round(PICKET.height * LIFT * PPM);
  const ax = -v.sx * L / 2, ay = -v.sy * L / 2, bx = v.sx * L / 2, by = v.sy * L / 2;
  const faceW = Math.max(1, Math.round(PICKET.width * PPM * Math.abs(Math.cos(v.phi))) + 1);
  const paint = paintOf(state, seed);
  const railC = state === 'fresh' ? WHITE[3] : WOOD[3];
  // The rails, behind the pickets.
  for (const r of PICKET.rails) {
    const lift = Math.round(r * LIFT * PPM);
    if (state === 'broken' && r === PICKET.rails[1]) {
      // The top rail has snapped in the middle: the left half hangs to the ground.
      s.line(ax, ay - lift, ax + (bx - ax) * 0.42, ay + (by - ay) * 0.42 - lift + 6, railC);
      s.line(ax + (bx - ax) * 0.66, ay + (by - ay) * 0.66 - lift, bx, by - lift, railC);
    } else s.line(ax, ay - lift, bx, by - lift, railC);
  }
  const n = PICKET.pickets, order = [...Array(n).keys()].sort((a, b) => (v.sy * (a - b)));
  for (const i of order) {
    const t = (i + 0.5) / n, px = ax + (bx - ax) * t, py = ay + (by - ay) * t;
    if (state === 'weathered' && i === 6) continue;
    if (state === 'broken' && (i === 3 || i === 4)) continue;
    let h = H, lean = 0;
    if (state !== 'fresh') h -= Math.floor(hash(i, deg, seed) * 3);
    if (state === 'weathered' && i === 2) lean = 2;
    if (state === 'broken' && i === 5) { lean = -5; h -= 3; }
    picket(s, px, py, h, faceW, paint, lean);
  }
  if (state === 'broken') {
    // One picket lies in the grass in front of the gap.
    const gx = ax + (bx - ax) * 0.45, gy = ay + (by - ay) * 0.45 + 4;
    const n2 = Math.hypot(v.sx, v.sy), ux = v.sx / n2, uy = v.sy / n2;
    for (let k = -7; k <= 7; k++) { s.set(gx + ux * k, gy + uy * k, WHITE[2]); s.set(gx + ux * k, gy + uy * k + 1, WOOD[3]); }
    s.set(gx + ux * 8, gy + uy * 8, WHITE[3]);
  }
  return s;
}

function picketGate(deg) {
  const s = sketch(), v = runVector(deg);
  const L = GATE.plan, H = Math.round(GATE.height * LIFT * PPM) - 1;
  const ax = -v.sx * L / 2, ay = -v.sy * L / 2, bx = v.sx * L / 2, by = v.sy * L / 2;
  const faceW = Math.max(1, Math.round(PICKET.width * PPM * Math.abs(Math.cos(v.phi))) + 1);
  const lo = Math.round(PICKET.rails[0] * LIFT * PPM), hi = Math.round(PICKET.rails[1] * LIFT * PPM);
  // A ledged-and-braced leaf: two rails and the brace between them, behind the pickets.
  s.line(ax, ay - lo, bx, by - lo, WHITE[3]);
  s.line(ax, ay - hi, bx, by - hi, WHITE[3]);
  s.line(ax, ay - lo, bx, by - hi, WHITE[4]);
  const n = GATE.pickets, order = [...Array(n).keys()].sort((a, b) => (v.sy * (a - b)));
  for (const i of order) {
    const t = (i + 0.5) / n;
    // The pickets crown towards the middle of the leaf.
    const crown = Math.round(2 * Math.sin(Math.PI * t));
    picket(s, ax + (bx - ax) * t, ay + (by - ay) * t, H + crown, faceW, paintOf('fresh'));
  }
  // The latch: a dark ring on the free end.
  s.set(bx, by - hi + 1, EDGE[0]); s.set(bx + 1, by - hi + 1, EDGE[1]);
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

function garment(s, cx, lineY, kind) {
  const C = kind.colour;
  const shade = (x0, x1, x) => x <= x0 ? 0 : x >= x1 ? 2 : 1;
  if (kind.shape === 'shirt') {
    for (let y = 0; y < 12; y++) {
      const w = y < 4 ? 6 : 4;
      for (let x = -w; x < w; x++) s.set(cx + x, lineY + 1 + y, C[y === 11 ? 2 : shade(-w, w - 1, x)]);
    }
    s.set(cx, lineY + 2, C[2]); s.set(cx, lineY + 5, C[2]); s.set(cx, lineY + 8, C[2]);
  } else if (kind.shape === 'trousers') {
    for (let y = 0; y < 15; y++) for (let x = -4; x < 4; x++) {
      if (y > 4 && (x === -1 || x === 0)) continue;
      s.set(cx + x, lineY + 1 + y, C[y === 0 ? 0 : shade(-4, 3, x)]);
    }
  } else if (kind.shape === 'sheet') {
    for (let y = 0; y < 14; y++) for (let x = -8; x < 8; x++) {
      const hem = y === 13 || (y === 12 && (x + 8) % 5 === 0);
      const stripe = x === 4 || x === 5;
      s.set(cx + x, lineY + 1 + y + (x > 3 ? 1 : 0), stripe ? CLOTH.red[x === 4 ? 1 : 2] : C[hem ? 2 : shade(-8, 6, x)]);
    }
  } else if (kind.shape === 'jumper') {
    for (let y = 0; y < 11; y++) {
      const w = y < 6 ? 7 : 4;
      for (let x = -w; x < w; x++) s.set(cx + x, lineY + 1 + y, C[(y === 10 || (y === 5 && Math.abs(x) > 4)) ? 2 : shade(-w, w - 1, x)]);
    }
  } else if (kind.shape === 'socks') {
    for (const dx of [-3, 2]) for (let y = 0; y < 6; y++) { s.set(cx + dx, lineY + 1 + y, C[0]); s.set(cx + dx + 1, lineY + 1 + y, C[1]); }
    s.set(cx - 2, lineY + 6, C[1]); s.set(cx + 4, lineY + 6, C[2]);
  }
  // The pegs.
  s.set(cx - 2, lineY, WOOD[0]); s.set(cx + 1, lineY, WOOD[0]);
}

function clothesline() {
  const s = sketch();
  const hx = Math.round(LINE.span / 2 * PPM), ph = Math.round(LINE.post * LIFT * PPM);
  // Two grey timber posts, each with a short cross-arm and a brace to the ground.
  for (const px of [-hx, hx]) {
    for (let y = 0; y <= ph; y++) { s.set(px - 1, -y, WOOD[1]); s.set(px, -y, WOOD[2]); s.set(px + 1, -y, WOOD[4]); }
    s.rect(px - 4, -ph - 1, px + 3, -ph, (x, y) => WOOD[y === -ph - 1 ? 0 : 3]);
    s.set(px - 1, -ph - 2, WOOD[0]); s.set(px, -ph - 2, WOOD[1]);
    const dir = px < 0 ? 1 : -1;
    s.line(px + dir * 2, -13, px + dir * 7, 0, WOOD[3]);
  }
  // The line, sagging, and what hangs on it.
  const lineY = (x) => -ph + 1 + Math.round(7 * (1 - (x / hx) ** 2));
  s.line(-hx, -ph + 1, -hx, -ph + 1, ROPE[1]);
  for (let x = -hx + 1; x < hx; x++) s.set(x, lineY(x), ROPE[x < 0 ? 0 : 1]);
  const kit = [
    { at: -0.72, shape: 'shirt', colour: CLOTH.cream },
    { at: -0.42, shape: 'trousers', colour: CLOTH.navy },
    { at: -0.12, shape: 'sheet', colour: CLOTH.cream },
    { at: 0.26, shape: 'jumper', colour: CLOTH.teal },
    { at: 0.5, shape: 'socks', colour: CLOTH.red },
    { at: 0.72, shape: 'shirt', colour: CLOTH.navy },
  ];
  for (const k of kit) { const cx = Math.round(k.at * hx); garment(s, cx, lineY(cx), k); }
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
    name: 'FogBell', kind: 'single', layer: 'raised', ...add('FogBell.png', fogBell()),
    footprintHalfMetres: { x: BELL.half[0], y: r4(BELL.half[1] * DEPTH) },
  });
  pieces.push({
    name: 'Clothesline', kind: 'single', layer: 'raised', ...add('Clothesline.png', clothesline()),
    postsAtMetres: [-LINE.span / 2, LINE.span / 2], postHalfMetres: { x: 0.08, y: r4(0.08 * DEPTH) },
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
