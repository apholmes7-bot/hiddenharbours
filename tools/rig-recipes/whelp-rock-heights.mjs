// whelp-rock-heights.mjs: the height each pixel of an offshore rock stands above its seat.
//
// The shaded rock kit ships colour, a mask and a normal for every rock, and no height. A rock that
// stands in the tide needs one: the sea covers a pixel when the water is above it, and a waterline
// cut straight across the picture is a ruler laid on a rock. This reads the committed colour sheet
// and writes, for each cell, how far each pixel stands above the rock's own lower edge in that
// column, so the water climbs the rock along the line the rock meets the ground on.
//
// The lower edge is smoothed over a few columns (a hanging frond of weed is not the seat). The rise is
// written as drawn; the picture's upper part is the rock's top receding from the camera as well as
// rising, so the contract also carries each variant's tallest drawn rise, and the placement scales it
// to the height the kit's sidecar gives that rock.
//
//   node tools/rig-recipes/whelp-rock-heights.mjs            write the sheets and the contract
//   node tools/rig-recipes/whelp-rock-heights.mjs --check    exit 1 if the committed files drifted
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { decodePng, encodePng } from './lib/png.mjs';
import { withoutTemplateSprites } from './lib/unity-meta.mjs';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const SHEETS = 'Assets/_Project/Art/Sprites/Shore/RockPx/tex';
const OUT = 'Assets/_Project/Art/Sprites/StPeters/Whelps';
const CONTRACT = OUT + '/RockHeights.json';
const META_TEMPLATE = 'Assets/_Project/Art/Sprites/StPeters/SetPieces/stpLandingDeck_height.png.meta';

const STEMS = ['Skerry_sandstone_weeded', 'Knuckle_sandstone_weeded', 'Shelf_sandstone_weeded', 'Slab_sandstone_weeded'];
const COLS = 8, ROWS = 3;                 // the kit's sheet: four variants and their mirrors, three tides
const PIXELS_PER_METRE = 32;              // the game's
const HEIGHT_SCALE = 0.766;               // screen rise per metre of height (the camera's)
const RANGE_METRES = 3;                   // what a full-white pixel means
const SEAT_SMOOTH_COLUMNS = 4;            // the lower edge is the median over this many columns each side

function heightsOf(stem) {
  const src = decodePng(fs.readFileSync(path.join(REPO, SHEETS, stem + '.png')));
  const { width: W, height: H, rgba } = src;
  const cw = W / COLS, ch = H / ROWS;
  if (cw % 1 || ch % 1) throw new Error(`${stem}: ${W}x${H} is not a ${COLS}x${ROWS} sheet`);
  const out = Buffer.alloc(W * H * 4);
  const tallest = new Array(COLS).fill(0);
  for (let cy = 0; cy < ROWS; cy++) for (let cx = 0; cx < COLS; cx++) {
    const x0 = cx * cw, y0 = cy * ch;
    const bottom = new Array(cw).fill(-1);
    for (let x = 0; x < cw; x++)
      for (let y = ch - 1; y >= 0; y--)
        if (rgba[((y0 + y) * W + x0 + x) * 4 + 3] > 0) { bottom[x] = y; break; }
    for (let x = 0; x < cw; x++) {
      if (bottom[x] < 0) continue;
      const near = [];
      for (let k = -SEAT_SMOOTH_COLUMNS; k <= SEAT_SMOOTH_COLUMNS; k++)
        if (x + k >= 0 && x + k < cw && bottom[x + k] >= 0) near.push(bottom[x + k]);
      near.sort((a, b) => a - b);
      const seat = near[near.length >> 1];
      for (let y = 0; y < ch; y++) {
        const at = ((y0 + y) * W + x0 + x) * 4;
        if (rgba[at + 3] === 0) continue;
        const metres = Math.max(0, seat - y) / (PIXELS_PER_METRE * HEIGHT_SCALE) ; tallest[cx] = Math.max(tallest[cx], Math.min(metres, RANGE_METRES));
        const v = Math.max(0, Math.min(255, Math.round(metres / RANGE_METRES * 255)));
        out[at] = out[at + 1] = out[at + 2] = v; out[at + 3] = 255;
      }
    }
  }
  return { png: encodePng(out, W, H), tallest: tallest.map((m) => Math.round(m * 1e4) / 1e4) };
}

function metaFor(file) {
  const guid = crypto.createHash('md5').update('whelp-rock-heights/' + file).digest('hex');
  return withoutTemplateSprites(fs.readFileSync(path.join(REPO, META_TEMPLATE), 'utf8').replace(/\r\n/g, '\n')).replace(/^guid: .*$/m, 'guid: ' + guid);
}

const built = STEMS.map((s) => ({ stem: s, ...heightsOf(s) }));
const files = Object.fromEntries(built.map((b) => [b.stem + '_height.png', b.png]));
const contract = JSON.stringify({
  what: 'height above its seat of each pixel of the offshore rock sheets, red channel',
  rangeMetres: RANGE_METRES, seatSmoothColumns: SEAT_SMOOTH_COLUMNS,
  sheets: built.map((b) => ({ stem: b.stem, height: b.stem + '_height.png', tallestDrawnMetres: b.tallest })),
}, null, 2) + '\n';

if (process.argv[2] === '--check') {
  const bad = [];
  for (const [f, png] of Object.entries(files)) {
    const p = path.join(REPO, OUT, f);
    if (!fs.existsSync(p) || Buffer.compare(fs.readFileSync(p), png) !== 0) bad.push(f);
  }
  const c = path.join(REPO, CONTRACT);
  if (!fs.existsSync(c) || fs.readFileSync(c, 'utf8').replace(/\r\n/g, '\n') !== contract) bad.push('RockHeights.json');
  if (bad.length) { console.error('drifted from the sheets:\n  ' + bad.join('\n  ')); process.exit(1); }
  console.log('whelp rock heights: the committed sheets and contract are the recipe\'s.');
} else {
  fs.mkdirSync(path.join(REPO, OUT), { recursive: true });
  for (const [f, png] of Object.entries(files)) {
    const p = path.join(REPO, OUT, f);
    fs.writeFileSync(p, png);
    if (!fs.existsSync(p + '.meta')) fs.writeFileSync(p + '.meta', metaFor(f));
    else {                                                   // an older meta keeps its GUID, loses the template's sprite
      const was = fs.readFileSync(p + '.meta', 'utf8').replace(/\r\n/g, '\n'), now = withoutTemplateSprites(was);
      if (now !== was) fs.writeFileSync(p + '.meta', now);
    }
  }
  fs.writeFileSync(path.join(REPO, CONTRACT), contract);
  console.log('whelp rock heights written to', OUT);
}
