// The plank footbridge over the Gap brook, drawn from numbers.
//
//   node tools/rig-recipes/gap-footbridge.mjs           writes the pictures, their metas and the contract
//   node tools/rig-recipes/gap-footbridge.mjs --check   exits 1 if the committed files are not what this draws
//
// Two pictures: the deck, which lies on the ground and is walked on, with a stone sill under each end and its shade
// on the water; and a low rail, stood along both long sides, the far one behind a walker and the near one in front.
// Weathered grey-brown plank, laid across the walk. The span and the width are the
// bridge Def's; the contract carries them so the scene step can refuse a Def the pictures were not drawn for.

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

function deck() {
  const dw = Math.round(SPAN * PPM), dh = Math.round(WIDTH * PPM);
  const w = dw + 2, top = 1 + OVER, h = top + dh + FACE + SHADE + 1;
  const c = canvas(w, h);
  // The shade on the water first, under everything: soft, longest under the middle of the span.
  for (let x = SILL; x < dw - SILL; x++)
  for (let y = 0; y < SHADE; y++) {
    const a = 120 - y * 14 - (hash(x, y, 13) > 0.6 ? 10 : 0);
    if (a > 0) c.set(1 + x, top + dh + FACE + y, [16, 22, 26], a);
  }
  const planks = Math.ceil(dw / PLANK);
  for (let n = 0; n < planks; n++) {
    const tone = 1 + Math.floor(hash(n, 0, 11) * 3);            // each plank its own grey
    const warm = hash(n, 1, 11) > 0.8;
    const short = hash(n, 2, 11) > 0.7 ? 1 : 0;                 // some planks a pixel short at the far side
    for (let x = n * PLANK; x < Math.min(dw, (n + 1) * PLANK); x++)
    for (let y = short; y < dh; y++) {
      const seam = x === n * PLANK;
      let col = TOP[tone];
      if (warm) col = WARM;
      if (hash(x, y, 5) > 0.93) col = TOP[Math.max(0, tone - 1)];   // grain
      if (hash(x >> 1, y, 7) > 0.97) col = TOP[Math.min(4, tone + 1)];
      if (y === dh - 1) col = TOP[4];                               // the near lip catches the light
      if (seam) col = GAP;
      c.set(1 + x, top + y, col);
    }
    for (const y of [3, dh - 5]) c.set(1 + n * PLANK + 3, top + y, NAIL); // nailed to the two stringers
  }
  for (let x = 0; x < dw; x++)
  for (let y = 0; y < FACE; y++) {
    const seam = x % PLANK === 0;
    c.set(1 + x, top + dh + y, seam ? EDGE[0] : EDGE[y === 0 ? 2 : 1]);
  }
  // A stone sill under each end, a little wider than the planks: the bridge sits on the banks.
  for (const x0 of [0, dw - SILL])
  for (let x = 0; x < SILL; x++)
  for (let y = -OVER; y < dh + FACE + OVER; y++) {
    const end = x0 === 0 ? x === 0 : x === SILL - 1;
    if (y >= 0 && y < dh && !end) continue;                         // the planks lie on it
    let col = STONE[2];
    if (y < 0 || y === dh) col = STONE[3];                           // its top, far and near
    if (y > dh) col = STONE[1];                                      // its near face
    if (y === dh + FACE + OVER - 1) col = STONE[0];
    if (hash(x0 + x, y + 9, 17) > 0.86) col = STONE[1];
    c.set(1 + x0 + x, top + y, col);
  }
  // The pivot is the middle of the planks, so the picture sits on its footprint.
  const pivot = [(1 + dw / 2) / w, (h - (top + dh / 2)) / h];
  return { name: 'GapFootbridgeDeck', c, pivot };
}

function rail() {
  const len = Math.round((SPAN - 2 * RAIL_INSET) * PPM), tall = Math.round(RAIL_HEIGHT * PPM * LIFT);
  const w = len + 2, h = tall + 2;
  const c = canvas(w, h);
  const posts = [0, Math.round(len / 2) - 1, len - 3];
  for (const y of [1, Math.round(tall * 0.55)])
  for (let x = 0; x < len; x++) {
    c.set(1 + x, 1 + y, TOP[3]);
    c.set(1 + x, 2 + y, TOP[hash(x, y, 3) > 0.85 ? 0 : 1]);
  }
  for (const p of posts)
  for (let y = 0; y < tall; y++) {
    c.set(1 + p, 1 + y, TOP[3]);
    c.set(2 + p, 1 + y, TOP[hash(p, y, 9) > 0.9 ? 1 : 2]);
    c.set(3 + p, 1 + y, EDGE[1]);
  }
  for (const p of posts) { c.set(1 + p, 1, TOP[4]); c.set(2 + p, 1, TOP[4]); }
  const pivot = [(1 + len / 2) / w, 1 / h];
  return { name: 'GapFootbridgeRail', c, pivot };
}

function metaFor(file, pivot) {
  const guid = crypto.createHash('md5').update('gap-footbridge/' + file).digest('hex');
  return fs.readFileSync(path.join(REPO, SPRITE_META), 'utf8').replace(/\r\n/g, '\n').replace(/^guid: .*$/m, 'guid: ' + guid)
    .replace(/spritePivot: \{[^}]*\}/, `spritePivot: {x: ${pivot[0]}, y: ${pivot[1]}}`);
}

const check = process.argv.includes('--check');
const pictures = [deck(), rail()];
const contract = JSON.stringify({
  pixelsPerMetre: PPM, spanMetres: SPAN, widthMetres: WIDTH, railInsetMetres: RAIL_INSET, railHeightMetres: RAIL_HEIGHT,
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
