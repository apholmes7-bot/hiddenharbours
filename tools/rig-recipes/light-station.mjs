// The north-east light station's sprites and their contract, generated from the rig and nothing else.
//
//   node tools/rig-recipes/light-station.mjs                 # write the sprites, their metas (if absent) and the contract
//   node tools/rig-recipes/light-station.mjs --check         # fail if a committed sprite or the contract has drifted
//   node tools/rig-recipes/light-station.mjs --plates <dir>  # write preview plates (no Unity)
//
// The rig is evaluated VERBATIM (ADR 0021 section 5). Its own checks() must pass or nothing is written.
// The station is drawn once, facing 0, under the rig's reference sky, as three sprites that sort on their
// own ground: the tower, the keeper's house with the oil house at its corner, and the shed.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { encodePng } from './lib/png.mjs';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const lf = (rel) => Buffer.from(fs.readFileSync(path.join(REPO, rel)).toString('binary').replace(/\r\n/g, '\n'), 'binary');
const sha = (buf) => crypto.createHash('sha256').update(buf).digest('hex');

const KIT = 'docs/art/rigs/lighthouse-rig-kit-v2';
const SOURCES = ['docs/art/rigs/st-peters-set-pieces-kit/lib/weatherSky.js', `${KIT}/stationLight2.js`, `${KIT}/lighthouseRig2.js`];
const OUT = 'Assets/_Project/Art/Sprites/StPeters/LightStation';
const CONTRACT = `${OUT}/LightStation.json`;
const META_TEMPLATE = 'Assets/_Project/Art/Sprites/Buildings/Shops/ShelfGoods/ShelfBaitBox.png.meta';

// The station the key scene asks for (KeyScene_stp_ne_light: station.stp_ne_light_tower, facing 0).
const STATION = {
  family: 'pyramid', size: 1, clad: 'clapboard', body: 'white', mark: 'red', daymark: 'plain', gallery: 'picket', cap: 'cone',
  dwelling: true, oilHouse: true, shed: true, weather: 0.32, char: 'Fl W 6s', occupancy: 'keeper',
};
const DIR = 0;
const ELEV = 40 * Math.PI / 180;
const DEPTH = Math.sin(ELEV);   // a ground metre away from the camera, in drawn metres
const r4 = (n) => Math.round(n * 1e4) / 1e4;

for (const s of SOURCES) (0, eval)(fs.readFileSync(path.join(REPO, s), 'utf8'));
const G = globalThis.LightStation2, L = globalThis.StationLight2;
if (!G || !L) throw new Error('the light-station rig did not install LightStation2 / StationLight2');
const checks = G.checks(STATION);
if (!checks.ok) { console.error('lighthouseRig2.js fails its own checks:', JSON.stringify(checks)); process.exit(1); }

// The rig's own rule for the lantern's near panes at facing 0 (the lens shows through), kept when a part is cut out.
const nearPanes = (f) => !f.src.glaze || -f.src.glaze[1] < 0.12;
const centreX = (f) => f.v.reduce((s, p) => s + p[0], 0) / f.v.length;
const partOf = (f) => f.src.grp !== 'station' ? 'tower' : centreX(f) < 0 ? 'keepersHouse' : 'shed';

// Where each part stands (metres from the ground under the tower; the door is on -y) and what it stands on.
// The outbuildings' plans are the rig's own (lighthouseRig2.js, the 'station' group); the tower's is its plinth.
const b = G.frame(DIR, STATION).b;
const off = 6.94 - 1.5;   // read back below against the rig's own model
const PARTS = [
  { name: 'tower', at: [0, 0], half: [r4(b.rB * 0.94), r4(b.rB * 0.94)] },
  { name: 'keepersHouse', at: [-off - 1.5, 0.9], half: [3.2, 4.1], also: { name: 'oilHouse', at: [-off + 0.4, -4.6], half: [1.25, 1.45] } },
  { name: 'shed', at: [off + 1.1, -1.6], half: [1.95, 2.45] },
];

function rgba(fr) { const p = G.relight(fr, L.REF_SKY, {}); return Buffer.from(p.buffer, p.byteOffset, p.length); }

function build() {
  const whole = G.frame(DIR, STATION);
  const again = G.frame(DIR, STATION, { only: nearPanes });
  if (Buffer.compare(rgba(whole), rgba(again)) !== 0) throw new Error('the near-pane rule here is not the rig\'s: the whole station differs');
  const W = whole.w, H = whole.h, pivot = whole.pivot;
  const files = {}, parts = [];
  for (const part of PARTS) {
    const fr = G.frame(DIR, STATION, { only: (f) => nearPanes(f) && partOf(f) === part.name });
    const px = rgba(fr);
    let x0 = W, y0 = H, x1 = -1, y1 = -1;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (px[(y * W + x) * 4 + 3]) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
    if (x1 < 0) throw new Error(`the ${part.name} drew nothing`);
    // The sprite's pivot is the ground under the part's own middle, so it sorts where it stands.
    const gx = pivot.x + part.at[0] * G.PX, gy = pivot.y - part.at[1] * DEPTH * G.PX;
    x0 = Math.min(x0, Math.floor(gx)); x1 = Math.max(x1, Math.ceil(gx)); y1 = Math.max(y1, Math.ceil(gy));
    const w = x1 - x0 + 1, h = y1 - y0 + 1, out = Buffer.alloc(w * h * 4);
    for (let y = 0; y < h; y++) px.copy(out, y * w * 4, ((y + y0) * W + x0) * 4, ((y + y0) * W + x0 + w) * 4);
    const file = `LightStation_${part.name}.png`;
    files[file] = { png: encodePng(out, w, h), pivot: { x: r4((gx - x0) / w), y: r4((y1 + 1 - gy) / h) } };
    const plan = (p) => ({ name: p.name, centreMetres: { x: r4(p.at[0] - part.at[0]), y: r4((p.at[1] - part.at[1]) * DEPTH) }, halfMetres: { x: p.half[0], y: r4(p.half[1] * DEPTH) } });
    parts.push({
      name: part.name, sprite: file, widthPx: w, heightPx: h, pivot: files[file].pivot,
      offsetMetres: { x: r4(part.at[0]), y: r4(part.at[1] * DEPTH) },
      footprints: [plan(part)].concat(part.also ? [plan(part.also)] : []),
    });
  }
  const an = G.anchors(DIR, STATION), lamp = G.lamp(STATION, 0, DIR, L.REF_SKY);
  const contract = {
    schema: 'light-station-sprites@1',
    rig: `${KIT}/lighthouseRig2.js`,
    derivedFromRigSha256: Object.fromEntries(SOURCES.map((s) => [path.basename(s), sha(lf(s))])),
    station: STATION, facing: DIR, pixelsPerMetre: G.PX,
    parts,
    lantern: { focalMetres: r4(an.lamp.m[2]), riseMetres: r4(-an.lamp.y / G.PX), galleryRiseMetres: r4(-an.gallery.y / G.PX), apexRiseMetres: r4(-an.apex.y / G.PX) },
    character: { name: lamp.char || STATION.char, flashEverySeconds: G.CHARS[STATION.char].per, beams: 2, turnSeconds: G.CHARS[STATION.char].per * 2 },
  };
  return { files, contract: JSON.stringify(contract, null, 2) + '\n', whole: { px: rgba(whole), w: W, h: H } };
}

function metaFor(file, pivot) {
  const guid = crypto.createHash('md5').update('light-station/' + file).digest('hex');
  let t = fs.readFileSync(path.join(REPO, META_TEMPLATE), 'utf8').replace(/\r\n/g, '\n');
  t = t.replace(/^guid: .*$/m, 'guid: ' + guid)
    .replace(/^  spritePivot: .*$/m, `  spritePivot: {x: ${pivot.x}, y: ${pivot.y}}`)
    .replace(/^    spriteID: .*$/m, '    spriteID: ' + crypto.createHash('md5').update('sprite/' + file).digest('hex'));
  return t;
}

const args = process.argv.slice(2);
const built = build();
if (args[0] === '--plates') {
  const dir = args[1]; fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, 'LightStation_whole.png'), encodePng(built.whole.px, built.whole.w, built.whole.h));
  for (const [f, v] of Object.entries(built.files)) fs.writeFileSync(path.join(dir, f), v.png);
  fs.writeFileSync(path.join(dir, 'LightStation.json'), built.contract);
  console.log('plates written to', dir);
} else if (args[0] === '--check') {
  const bad = [];
  for (const [f, v] of Object.entries(built.files)) {
    const p = path.join(REPO, OUT, f);
    if (!fs.existsSync(p) || Buffer.compare(fs.readFileSync(p), v.png) !== 0) bad.push(f);
    const m = p + '.meta';
    if (!fs.existsSync(m) || !fs.readFileSync(m, 'utf8').includes(`spritePivot: {x: ${v.pivot.x}, y: ${v.pivot.y}}`)) bad.push(f + '.meta (pivot)');
  }
  const c = path.join(REPO, CONTRACT);
  if (!fs.existsSync(c) || fs.readFileSync(c, 'utf8').replace(/\r\n/g, '\n') !== built.contract) bad.push('LightStation.json');
  if (bad.length) { console.error('drifted from the rig:\n  ' + bad.join('\n  ')); process.exit(1); }
  console.log('light station: the committed sprites and contract are the rig\'s.');
} else {
  fs.mkdirSync(path.join(REPO, OUT), { recursive: true });
  for (const [f, v] of Object.entries(built.files)) {
    const p = path.join(REPO, OUT, f);
    fs.writeFileSync(p, v.png);
    if (!fs.existsSync(p + '.meta')) fs.writeFileSync(p + '.meta', metaFor(f, v.pivot));
  }
  fs.writeFileSync(path.join(REPO, CONTRACT), built.contract);
  console.log('light station written to', OUT);
}
