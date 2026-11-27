// The plank footbridge over the Gap brook, drawn from numbers.
//
//   node tools/rig-recipes/gap-footbridge.mjs           writes the pictures, their metas and the contract
//   node tools/rig-recipes/gap-footbridge.mjs --check   exits 1 if the committed files are not what this draws
//
// Two pictures: the deck, which lies on the ground and is walked on, with a stone sill under each end and its shade
// on the water; and a low rail, stood along both long sides, the far one behind a walker and the near one in front.
// Weathered grey-brown plank, laid across the walk. The deck runs square to the brook, so both pictures are drawn
// along that axis. The span, the width and the axis are the bridge Def's; the contract carries them so the scene
// step can refuse a Def the pictures were not drawn for.

import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { encodePng } from './lib/png.mjs';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const OUT = 'Assets/_Project/Art/Sprites/StPeters/Footbridge';
const CONTRACT = OUT + '/Footbridge.json';
const SPRITE_META = 'Assets/_Project/Art/Sprites/StPeters/SetPieces/stpLandingDeck.png.meta';

const PPM = 32;                 // pixels a metre, the game's
const LIFT = 0.766;             // picture rise per metre of height, the camera's
const SPAN = 3.0;               // metres along the walk, bank to bank
const WIDTH = 1.15;             // metres across
const RAIL_INSET = 0.1;         // the rail stops this short of each end
const RAIL_HEIGHT = 0.6;        // metres: a low rail
const FACE = 4;                 // pixels of plank edge seen under the near side
const SHADE = 8;                // pixels of shade on the water under that
const SILL = 7;                 // pixels of stone sill under each end
const OVER = 2;                 // pixels the sill stands out past the planks, far and near
const PLANK = 6;                // pixels a plank
// Which way the deck runs, anticlockwise from the world's +x: square to the Gap brook, which falls two metres south
// for each metre east where the bridge crosses it. The 2:1 line is also the cleanest stair a pixel line makes.
const AXIS = Math.atan2(1, 2);
const AXIS_DEGREES = Math.round(AXIS * 180 / Math.PI * 1e5) / 1e5;
const CA = Math.cos(AXIS), SA = Math.sin(AXIS);

const TOP = [[96, 86, 73], [111, 100, 85], [126, 114, 97], [141, 128, 109], [157, 144, 124]];
const STONE = [[78, 78, 76], [104, 103, 99], [128, 126, 120], [150, 148, 140]];
const WARM = [124, 104, 82];
const GAP = [62, 54, 45];
const EDGE = [[52, 46, 39], [76, 67, 56], [96, 86, 73]];
const NAIL = [58, 51, 43];

function hash(x, y, s) {
  let h = (x * 374761393 + y * 668265263 + s * 2147483647) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

function canvas(w, h) {
  const px = new Uint8Array(w * h * 4);
  const set = (x, y, c, a = 255) => {
    if (x < 0 || y < 0 || x >= w || y >= h) return;
    const i = (y * w + x) * 4; px[i] = c[0]; px[i + 1] = c[1]; px[i + 2] = c[2]; px[i + 3] = a;
  };
  return { px, set, w, h };
}

// The deck's own frame: u along the axis (bank to bank), v across it (landward positive), metres from the middle
// of the planks. A pixel's centre is taken into it from the picture's middle (cx, cy), the ground drawn flat.
function frame(cx, cy) {
  return (X, Y) => {
    const x = (X + 0.5 - cx) / PPM, y = -(Y + 0.5 - cy) / PPM;
    return [x * CA + y * SA, -x * SA + y * CA];
  };
}

function deck() {
  const half = SPAN / 2, across = WIDTH / 2, sill = SILL / PPM, over = OVER / PPM, lip = 1 / PPM;
  // The picture's extent: the sills' corners, then the faces and the shade hanging under the near side.
  const hx = (half + lip) * CA + (across + over) * SA, hy = (half + lip) * SA + (across + over) * CA;
  const cx = Math.ceil(hx * PPM) + 2, cy = Math.ceil(hy * PPM) + 2;
  const w = 2 * cx, h = cy + Math.ceil(hy * PPM) + FACE + SHADE + OVER + 3;
  const c = canvas(w, h);
  const at = frame(cx, cy);
  const inside = (X, Y) => { const [u, v] = at(X, Y); return Math.abs(u) <= half && Math.abs(v) <= across; };
  const onSill = (X, Y) => {
    const [u, v] = at(X, Y);
    return Math.abs(u) >= half - sill && Math.abs(u) <= half + lip && Math.abs(v) <= across + over;
  };
  const plank = (X, Y) => Math.floor((at(X, Y)[0] + half) * PPM / PLANK);
  const seamAt = (X, Y) => inside(X - 1, Y) && plank(X - 1, Y) !== plank(X, Y);
  const above = (X, Y, test, most) => { for (let k = 1; k <= most; k++) if (test(X, Y - k)) return k; return 0; };

  for (let Y = 0; Y < h; Y++)
  for (let X = 0; X < w; X++) {
    if (inside(X, Y)) continue;
    // The shade on the water first, under everything: soft, only under the open middle of the span.
    const k = above(X, Y, inside, FACE + SHADE);
    if (k > FACE && !onSill(X, Y)) {
      const [u] = at(X, Y - k);
      const a = 120 - (k - FACE - 1) * 14 - (hash(X, Y, 13) > 0.6 ? 10 : 0);
      if (Math.abs(u) < half - sill && a > 0) c.set(X, Y, [16, 22, 26], a);
    }
    // The planks' edge seen under the near side (and under the near end, which faces the camera a little).
    if (k >= 1 && k <= FACE) c.set(X, Y, seamAt(X, Y - k) ? EDGE[0] : EDGE[k === 1 ? 2 : 1]);
  }
  // A stone sill under each end, a little wider than the planks: the bridge sits on the banks.
  for (let Y = 0; Y < h; Y++)
  for (let X = 0; X < w; X++) {
    if (inside(X, Y)) continue;
    if (onSill(X, Y)) {
      const [, v] = at(X, Y);
      let col = Math.abs(v) > across ? STONE[3] : STONE[2];          // its top, far and near of the planks
      if (hash(X, Y + 9, 17) > 0.86) col = STONE[1];
      c.set(X, Y, col);
      continue;
    }
    const k = above(X, Y, onSill, FACE + 1);
    if (k) c.set(X, Y, k === FACE + 1 ? STONE[0] : (hash(X, Y + 9, 17) > 0.86 ? STONE[0] : STONE[1]));   // its near face
  }
  // The planks, laid across the walk: each its own grey, a few warm, grain, a seam between each.
  for (let Y = 0; Y < h; Y++)
  for (let X = 0; X < w; X++) {
    if (!inside(X, Y)) continue;
    const n = plank(X, Y), [, v] = at(X, Y);
    const tone = 1 + Math.floor(hash(n, 0, 11) * 3);
    const warm = hash(n, 1, 11) > 0.8;
    const short = hash(n, 2, 11) > 0.7;                                // some planks a pixel short at the far side
    if (short && v > 0 && !inside(X, Y - 1)) continue;
    let col = warm ? WARM : TOP[tone];
    if (hash(X, Y, 5) > 0.93) col = TOP[Math.max(0, tone - 1)];       // grain
    if (hash(X >> 1, Y, 7) > 0.97) col = TOP[Math.min(4, tone + 1)];
    if (v < 0 && !inside(X, Y + 1)) col = TOP[4];                      // the near lip catches the light
    if (seamAt(X, Y)) col = GAP;
    c.set(X, Y, col);
  }
  // Nailed to the two stringers, one nail a plank on each.
  const planks = Math.ceil(SPAN * PPM / PLANK);
  for (let n = 0; n < planks; n++)
  for (const v of [across - 3.5 / PPM, -across + 5.5 / PPM]) {
    const u = -half + (n + 0.5) * PLANK / PPM;
    if (Math.abs(u) > half - 1 / PPM) continue;
    const X = Math.floor(cx + (u * CA - v * SA) * PPM), Y = Math.floor(cy - (u * SA + v * CA) * PPM);
    if (inside(X, Y) && !seamAt(X, Y)) c.set(X, Y, NAIL);
  }
  // The pivot is the middle of the planks, so the picture sits on its footprint.
  return { name: 'GapFootbridgeDeck', c, pivot: [cx / w, (h - cy) / h] };
}

// A low rail stood along one long side: posts at each end and the middle, two bars between, all following the
// deck's axis. Its pivot is the middle of its foot line, which the scene stands on the deck's edge.
function rail() {
  const len = SPAN - 2 * RAIL_INSET, tall = Math.round(RAIL_HEIGHT * PPM * LIFT);
  const hx = (len / 2) * CA * PPM, hy = (len / 2) * SA * PPM;
  const cx = Math.ceil(hx) + 3, cy = Math.ceil(hy) + tall + 2;
  const w = 2 * cx, h = cy + Math.ceil(hy) + 2;
  const c = canvas(w, h);
  const footY = (X) => Math.floor(cy - (X + 0.5 - cx) * SA / CA);
  const onLine = (X) => Math.abs((X + 0.5 - cx) / PPM / CA) <= len / 2;
  const bars = [tall - 2, tall - 1 - Math.round(tall * 0.55)];
  for (let X = 0; X < w; X++) {
    if (!onLine(X)) continue;
    for (const r of bars) {
      c.set(X, footY(X) - r, TOP[3]);
      c.set(X, footY(X) - r + 1, TOP[hash(X, r, 3) > 0.85 ? 0 : 1]);
    }
  }
  for (const u of [-len / 2, 0, len / 2]) {
    const xp = Math.round(cx + u * CA * PPM) + (u < 0 ? 1 : u > 0 ? -2 : -1);
    for (let i = 0; i < 3; i++) {
      const X = xp + i, f = footY(xp + 1);
      for (let r = 0; r < tall; r++) c.set(X, f - r, i === 0 ? TOP[3] : i === 1 ? TOP[hash(xp, r, 9) > 0.9 ? 1 : 2] : EDGE[1]);
      if (i < 2) c.set(X, f - tall + 1, TOP[4]);
    }
  }
  return { name: 'GapFootbridgeRail', c, pivot: [cx / w, (h - cy) / h] };
}

function metaFor(file, pivot) {
  const guid = crypto.createHash('md5').update('gap-footbridge/' + file).digest('hex');
  return fs.readFileSync(path.join(REPO, SPRITE_META), 'utf8').replace(/\r\n/g, '\n').replace(/^guid: .*$/m, 'guid: ' + guid)
    .replace(/spritePivot: \{[^}]*\}/, `spritePivot: {x: ${pivot[0]}, y: ${pivot[1]}}`);
}

const check = process.argv.includes('--check');
const pictures = [deck(), rail()];
const contract = JSON.stringify({
  pixelsPerMetre: PPM, axisDegrees: AXIS_DEGREES, spanMetres: SPAN, widthMetres: WIDTH, railInsetMetres: RAIL_INSET, railHeightMetres: RAIL_HEIGHT,
  deck: pictures[0].name, rail: pictures[1].name,
}, null, 1) + '\n';
let drift = 0;
if (!check) fs.mkdirSync(path.join(REPO, OUT), { recursive: true });
for (const p of pictures) {
  const file = path.join(REPO, OUT, p.name + '.png');
  const png = encodePng(p.c.px, p.c.w, p.c.h);
  if (check) { if (!fs.existsSync(file) || !fs.readFileSync(file).equals(png)) { console.error('drifted: ' + p.name); drift++; } continue; }
  fs.writeFileSync(file, png);
  fs.writeFileSync(file + '.meta', metaFor(p.name + '.png', p.pivot));
  console.log(`${p.name}: ${p.c.w}x${p.c.h} px, pivot ${p.pivot.map((v) => v.toFixed(4)).join(', ')}`);
}
if (check) {
  const at = path.join(REPO, CONTRACT);
  if (!fs.existsSync(at) || fs.readFileSync(at, 'utf8').replace(/\r\n/g, '\n') !== contract) { console.error('drifted: Footbridge.json'); drift++; }
  process.exit(drift ? 1 : 0);
}
fs.writeFileSync(path.join(REPO, CONTRACT), contract);
const textMeta = (name) => `fileFormatVersion: 2\nguid: ${crypto.createHash('md5').update('gap-footbridge/' + name).digest('hex')}\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
if (!fs.existsSync(path.join(REPO, CONTRACT) + '.meta')) fs.writeFileSync(path.join(REPO, CONTRACT) + '.meta', textMeta('Footbridge.json'));
const folderMeta = path.join(REPO, OUT) + '.meta';
if (!fs.existsSync(folderMeta)) fs.writeFileSync(folderMeta, `fileFormatVersion: 2\nguid: ${crypto.createHash('md5').update('gap-footbridge/folder').digest('hex')}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
