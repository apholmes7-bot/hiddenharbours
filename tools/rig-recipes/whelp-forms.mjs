// whelp-forms.mjs: the Harbour Stack and the three Whelps, drawn from their shapes.
//
// The lookout's scene gives each of these a centre, a size and a height (the stack: an oval 3.2 by 2.6
// across, its top 6.2 m up, walls battered at 80 degrees, on a plinth; the Whelps: three low ledges).
// This builds each as a field of heights, looks at it the way the game's camera does (a metre of height
// lifts a thing 0.766 of a metre up the picture), and paints it in the South Arm cliff's own stone: red
// sandstone laid in beds, darker in its joints, turf on the stack's crown, weed and barnacle where the
// tide works. No outline is drawn.
//
// For each form it writes three pictures, pixel for pixel over one another: the stone dry, the stone
// wet, and the height of every pixel above the form's floor (red = height / range). The game cuts the
// pictures where the sea stands, so the water climbs the stone along the stone's own shape.
// Forms.json beside them carries the numbers the placement reads: floor, range, pivot, the foot a boat
// cannot pass and where the form sorts.
//
//   node tools/rig-recipes/whelp-forms.mjs            write the pictures and the contract
//   node tools/rig-recipes/whelp-forms.mjs --check    exit 1 if the committed files drifted
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { encodePng } from './lib/png.mjs';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const OUT = 'Assets/_Project/Art/Sprites/StPeters/Whelps';
const CONTRACT = OUT + '/Forms.json';
const SPRITE_META = 'Assets/_Project/Art/Sprites/StPeters/SetPieces/stpLandingDeck.png.meta';
const HEIGHT_META = 'Assets/_Project/Art/Sprites/StPeters/SetPieces/stpLandingDeck_height.png.meta';

const PPM = 32;                 // pixels a metre, the game's
const LIFT = 0.766;             // picture rise per metre of height, the camera's
const STEP = 1 / (PPM * 2);     // the march across the ground, half a pixel
const FLOOR = -1.6;             // every form is drawn down to here, below the seabed it stands on

// ---- the cliff's stone, read off the South Arm's own face ----
const RED = [[92, 44, 32], [128, 68, 40], [160, 80, 48], [178, 98, 58], [196, 122, 82]];
const RUST = [[80, 40, 30], [106, 62, 40], [136, 76, 46], [158, 90, 54], [176, 104, 66]];
const JOINT = [[44, 30, 24], [58, 42, 34], [72, 54, 40], [88, 64, 46], [104, 76, 54]];
const PALE = [[112, 72, 50], [150, 96, 66], [184, 122, 86], [204, 146, 106], [220, 170, 130]];
const TURF = [[44, 60, 36], [60, 78, 40], [80, 96, 40], [98, 110, 52], [114, 116, 64]];
const WEED = [[36, 30, 20], [52, 42, 24], [70, 56, 28], [88, 72, 36], [104, 90, 46]];
const BARNACLE = [168, 160, 138];
// The beds from the floor up: how thick, and which stone. The pattern repeats.
const BEDS = [[0.42, RED], [0.10, JOINT], [0.55, RUST], [0.30, RED], [0.08, JOINT], [0.38, PALE], [0.62, RED], [0.12, JOINT], [0.46, RUST]];
const BEDS_TOTAL = BEDS.reduce((s, b) => s + b[0], 0);

function hash(x, y, s) {
  let h = (Math.imul(x | 0, 374761393) + Math.imul(y | 0, 668265263) + Math.imul(s | 0, 2147483647)) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}
function noise(x, y, s) {
  const x0 = Math.floor(x), y0 = Math.floor(y), fx = x - x0, fy = y - y0;
  const u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
  const a = hash(x0, y0, s), b = hash(x0 + 1, y0, s), c = hash(x0, y0 + 1, s), d = hash(x0 + 1, y0 + 1, s);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}
const smooth = (t) => { t = Math.max(0, Math.min(1, t)); return t * t * (3 - 2 * t); };

// ---- the shapes ----
// Each returns the height at a point of the form's own ground (metres from its centre), or null off it.
function stackShape(f) {
  const rot = f.rot * Math.PI / 180, c = Math.cos(rot), s = Math.sin(rot);
  const mean = (f.radii[0] + f.radii[1]) / 2, fall = Math.tan(f.batter * Math.PI / 180);
  return (x, y) => {
    const u = x * c + y * s, v = -x * s + y * c;
    const a = Math.atan2(v / f.radii[1], u / f.radii[0]);
    const lobes = 1 + 0.10 * Math.sin(2 * a + 0.6) + 0.07 * Math.sin(3 * a + 1.9) + 0.02 * Math.sin(7 * a + 2.3);
    const cleft = noise(a * 2.6 + 11, 0.5, 83);                                  // a few upright clefts down the wall
    const wob = lobes - 0.10 * smooth((cleft - 0.70) / 0.2);
    const e = (Math.hypot(u / f.radii[0], v / f.radii[1]) / wob - 1) * mean;      // metres outside the foot
    const rd = Math.hypot(x, y);
    const straight = -e * fall;                                                  // the battered wall, from the foot up
    let z, crown = false;
    if (straight >= f.top) { z = f.top + 0.10 * noise(x * 1.7, y * 1.7, 5) + 0.12 * smooth((straight - f.top) / 4); crown = true; }
    else {
      const lean = 0.7 * (noise(a * 1.4 + 3, 0.5, 91) - 0.5);                      // the beds dip round the stack
      const stepped = Math.round((straight + lean) / 1.35) * 1.35 - lean;          // and stand out as ledges
      z = straight + (stepped - straight) * 0.38 + 0.16 * (noise(a * 4, straight * 1.3, 9) - 0.5);
      z = Math.min(z, f.top);
    }
    const rim = f.plinth.radius * (0.78 + 0.30 * noise(a * 1.9 + 7, 0.5, 61) + 0.08 * Math.sin(5 * a + 0.8));
    const inner = mean * 0.95;
    let t = smooth(1 - (rd - inner) / Math.max(0.5, rim - inner));
    t = Math.round(t * 4) / 4 * 0.65 + t * 0.35;                                 // the plinth steps down to the sea floor
    const skirt = f.plinth.z + (f.plinthTop - f.plinth.z) * t + 0.14 * (noise(x * 1.5, y * 1.5, 3) - 0.5);
    const zp = rd <= rim ? skirt : skirt - (rd - rim) * 3.5;
    z = Math.max(z, zp);
    return z >= FLOOR ? { z, crown } : null;
  };
}
function ledgeShape(f) {
  return (x, y) => {
    const a = Math.atan2(y, x), rd = Math.hypot(x, y);
    const wob = 1 + 0.16 * Math.sin(2 * a + f.seed) + 0.10 * Math.sin(5 * a + 1.7 * f.seed) + 0.06 * Math.sin(9 * a + 0.3 * f.seed);
    const d = rd / (f.radius * wob) + 0.10 * (noise(x * 1.2, y * 1.2, 70 + f.seed) - 0.5);   // 1 at the ledge's rim
    if (d > 1.25) return null;
    const rise = f.top - FLOOR;
    let t = 1 - smooth((d - 0.72) / 0.5);                                        // flat-topped, stepped to the rim
    t = Math.round(t * 3) / 3 * 0.6 + t * 0.4;
    let z = FLOOR + rise * t + 0.07 * (noise(x * 2.1, y * 2.1, f.seed) - 0.5);
    for (const k of f.crags) {
      const q = Math.hypot((x - k.x) / k.r, (y - k.y) / (k.r * 0.8));
      if (q < 1) {
        const up = k.h * (1 - q * q);
        z = Math.max(z, f.top + Math.round(up / 0.22) * 0.22 * 0.55 + up * 0.45 + 0.05 * (noise(x * 4, y * 4, 21) - 0.5));
      }
    }
    return z >= FLOOR ? { z, crown: false } : null;
  };
}

const FORMS = [
  { id: 'form.stp_harbour_stack', kind: 'stack', radii: [3.2, 2.6], rot: 20, top: 6.2, batter: 80,
    plinth: { radius: 5.5, z: -1.2 }, plinthInner: 3.6, plinthTop: 0.15, range: 8.4, turf: true, reach: 5.9, footAt: 0.0,
    pieces: [{ name: 'Body', stem: 'HarbourStack', from: -0.1 }, { name: 'SeabedPlinth', stem: 'HarbourStackPlinth', below: -0.1 }] },
  { id: 'form.stp_whelp_1', pieces: [{ name: 'SeabedLedge', stem: 'Whelp1' }], kind: 'ledge', radius: 2.8, top: -1.04, seed: 1, range: 2, reach: 4.7, footAt: -1.0,
    crags: [{ x: -0.6, y: 0.9, r: 0.9, h: 0.75 }, { x: 1.5, y: 0.2, r: 0.7, h: 0.55 }, { x: -1.8, y: -0.2, r: 0.6, h: 0.4 }] },
  { id: 'form.stp_whelp_2', pieces: [{ name: 'SeabedLedge', stem: 'Whelp2' }], kind: 'ledge', radius: 2.2, top: -1.10, seed: 2, range: 2, reach: 3.8, footAt: -1.0,
    crags: [{ x: 0.2, y: 0.5, r: 0.85, h: 0.7 }, { x: -1.2, y: 0.1, r: 0.55, h: 0.45 }] },
  { id: 'form.stp_whelp_3', pieces: [{ name: 'SeabedLedge', stem: 'Whelp3' }], kind: 'ledge', radius: 2.0, top: -0.35, seed: 3, range: 2, reach: 3.5, footAt: -1.0,
    crags: [{ x: 0.5, y: 0.6, r: 0.7, h: 0.5 }, { x: -0.9, y: 0.7, r: 0.5, h: 0.35 }] },
];

// ---- the paint ----
function bedOf(z, x, y) {
  let h = ((z - FLOOR + 0.10 * (noise(x * 0.7, y * 0.7, 31) - 0.5) + 0.02 * x) % BEDS_TOTAL + BEDS_TOTAL) % BEDS_TOTAL;
  for (const b of BEDS) { if (h < b[0]) return b[1]; h -= b[0]; }
  return RED;
}
function paint(f, p, px, py) {
  // p: { x, y, z, steep, nx, ny, crown }
  const grit = hash(px, py, 7);
  let light = 0.46 + 0.50 * (-0.52 * p.nx - 0.34 * p.ny + 0.78 * p.nz);          // lit from the left and the front
  if (p.steep) light -= 0.07; else light += 0.10;
  // Tide-worked rock is darker than the dry wall, and its flats are pitted and pooled, never one flat tone.
  if (p.z < 0.3) light += -0.22 + 0.50 * (noise(p.x * 2.3, p.y * 2.3, 37) - 0.5) + 0.24 * (noise(p.x * 6.1, p.y * 6.1, 43) - 0.5);
  const crack = noise(p.x * 3.1 + p.y * 1.3, p.z * 0.35, 41);                    // upright joints in the face
  if (p.steep && crack > 0.72) light -= 0.22;
  let tone = Math.max(0, Math.min(4, Math.floor(light * 5 + (grit - 0.5) * 0.9)));
  let ramp = bedOf(p.z, p.x, p.y);
  if (f.turf) {
    const lip = f.top - p.z;                                                    // the turf, and what hangs from its edge
    if (p.crown || (!p.steep && lip < 0.35) || (p.steep && lip < 0.18 + 0.30 * noise(p.x * 3.3, p.y * 3.3, 13))) {
      ramp = TURF;
      tone = Math.max(0, Math.min(4, Math.floor((p.steep ? 0.25 : 0.52) * 5 + noise(p.x * 2.6, p.y * 2.6, 17) * 2.2 - 0.8 + (grit - 0.5))));
    }
  }
  // Where the tide works: weed thick low down and on anything flat, thinning upward; a pale line of barnacles above it.
  const weedTop = f.kind === 'stack' ? -0.15 : f.top + 0.45;
  const patch = noise(p.x * 1.6, p.y * 1.6, 23) * 0.65 + noise(p.x * 4.2, p.y * 4.2, 29) * 0.35;
  const w = f.kind === 'stack'
    ? smooth((weedTop - p.z) / 1.0) * (p.steep ? 0.7 : 0.8) + (patch - 0.5) * 1.3
    : (p.z < weedTop ? (p.steep ? 0.30 : 0.48) : 0) + (patch - 0.5) * 1.4;
  if (ramp !== TURF && w > 0.5) { ramp = WEED; tone = Math.max(0, Math.min(4, tone + (grit > 0.8 ? 1 : 0))); }
  let rgb = ramp[tone];
  if (ramp !== WEED && ramp !== TURF && p.z > weedTop - 0.25 && p.z < weedTop + 0.45 && hash(px, py, 51) > 0.86) rgb = BARNACLE;
  return rgb;
}
const wet = (c) => [Math.round(c[0] * 0.62), Math.round(c[1] * 0.60), Math.round(c[2] * 0.66)];

// ---- the camera ----
function draw(f) {
  const shape = f.kind === 'stack' ? stackShape(f) : ledgeShape(f);
  const R = f.reach;
  const W = Math.ceil(R * 2 * PPM), sLo = Math.floor((-R + LIFT * FLOOR) * PPM) - 2;
  const sHi = Math.ceil((R + LIFT * (f.kind === 'stack' ? f.top + 0.5 : f.top + 1.2)) * PPM) + 2;
  const H = sHi - sLo;
  const dry = Buffer.alloc(W * H * 4), wetb = Buffer.alloc(W * H * 4), hgt = Buffer.alloc(W * H * 4);
  const zs = new Float32Array(W * H).fill(-99);
  let tallest = FLOOR;
  const grad = (x, y) => {
    const e = 0.06, a = shape(x - e, y), b = shape(x + e, y), c = shape(x, y - e), d = shape(x, y + e), m = shape(x, y);
    const z = m ? m.z : FLOOR, za = a ? a.z : z - 0.4, zb = b ? b.z : z - 0.4, zc = c ? c.z : z - 0.4, zd = d ? d.z : z - 0.4;
    const gx = (zb - za) / (2 * e), gy = (zd - zc) / (2 * e), n = Math.hypot(gx, gy, 1);
    return { nx: -gx / n, ny: -gy / n, nz: 1 / n, steep: Math.hypot(gx, gy) > 1.6 };
  };
  for (let px = 0; px < W; px++) {
    const x = (px + 0.5) / PPM - R;
    let horizon = -Infinity, zAtHorizon = FLOOR, started = false;
    for (let y = -R; y <= R; y += STEP) {
      const m = shape(x, y);
      if (!m) continue;
      const s = (y + LIFT * m.z) * PPM;
      if (!started) { horizon = (y + LIFT * FLOOR) * PPM; zAtHorizon = FLOOR; started = true; }
      if (s <= horizon) continue;
      const g = grad(x, y);
      const from = Math.ceil(horizon - 0.5), to = Math.floor(s - 0.5);
      for (let r = from; r <= to; r++) {
        const t = s - horizon > 1e-6 ? (r + 0.5 - horizon) / (s - horizon) : 1;
        const z = zAtHorizon + (m.z - zAtHorizon) * Math.max(0, Math.min(1, t));
        const row = sHi - 1 - (r - sLo) - 0;                                     // picture rows run down
        const py = H - 1 - (r - sLo);
        if (py < 0 || py >= H) continue;
        const steep = g.steep || (to - from) > 2;
        const rgb = paint(f, { x, y, z, steep, nx: g.nx, ny: g.ny, nz: g.nz, crown: m.crown && !steep }, px, r);
        const i = (py * W + px) * 4, wv = wet(rgb);
        dry[i] = rgb[0]; dry[i + 1] = rgb[1]; dry[i + 2] = rgb[2]; dry[i + 3] = 255;
        wetb[i] = wv[0]; wetb[i + 1] = wv[1]; wetb[i + 2] = wv[2]; wetb[i + 3] = 255;
        const code = Math.max(0, Math.min(255, Math.round((z - FLOOR) / f.range * 255)));
        hgt[i] = code; hgt[i + 1] = code; hgt[i + 2] = code; hgt[i + 3] = 255;
        zs[py * W + px] = z;
        if (z > tallest) tallest = z;
        void row;
      }
      horizon = s; zAtHorizon = m.z;
    }
  }
  // The foot a boat cannot pass: where the form stands at the foot height, as a ring of points about its centre.
  const foot = [];
  for (let k = 0; k < 20; k++) {
    const a = k / 20 * Math.PI * 2;
    let rr = R;
    for (let q = 0.2; q <= R; q += 0.05) { const m = shape(Math.cos(a) * q, Math.sin(a) * q); if (!m || m.z < f.footAt) { rr = q; break; } }
    foot.push([Math.round(Math.cos(a) * rr * 100) / 100, Math.round(Math.sin(a) * rr * 100) / 100]);
  }
  const front = Math.min(...foot.map((p) => p[1]));
  // The pieces: a form may be drawn as more than one picture set (what stands above the sea, and the seabed
  // part under it), each with a pivot at its own lowest pixel so a shadow is thrown from its foot.
  const centreRow = H - 1 - (0 - sLo);                                           // the picture row of the centre's ground
  const pieces = f.pieces.map((pc) => {
    const keep = (z) => z > -90 && (pc.from === undefined || z >= pc.from) && (pc.below === undefined || z < pc.below);
    const cut = (src) => { const o = Buffer.alloc(src.length); for (let k = 0; k < W * H; k++) if (keep(zs[k])) src.copy(o, k * 4, k * 4, k * 4 + 4); return o; };
    let lowest = 0;
    for (let k = 0; k < W * H; k++) if (keep(zs[k])) lowest = Math.max(lowest, Math.floor(k / W));
    const whole = pc.from === undefined;                                         // a seabed piece keeps the centre as its pivot
    const pivotRow = whole ? centreRow + 0.5 : lowest + 1;                       // rows run down; the pivot is a row's lower edge
    return {
      name: pc.name, stem: pc.stem, dry: cut(dry), wet: cut(wetb), hgt: cut(hgt),
      pivot: [0.5, Math.round((H - pivotRow) / H * 1e6) / 1e6],
      offsetY: Math.round((centreRow + 0.5 - pivotRow) / PPM * 1e4) / 1e4,       // the pivot's place from the centre, metres
    };
  });
  return {
    W, H, pieces,
    entry: {
      id: f.id, kind: f.kind,
      floorMetres: FLOOR, rangeMetres: f.range, tallestMetres: Math.round(tallest * 100) / 100,
      pixels: [W, H], sortYOffset: Math.round(front * 100) / 100,
      foot: foot.flat(),                                                         // x0, y0, x1, y1 ... about the centre
      pieces: pieces.map((q) => ({ name: q.name, stem: q.stem, pivot: q.pivot, offsetY: q.offsetY })),
    },
  };
}

function metaFor(file, template, pivot) {
  const guid = crypto.createHash('md5').update('whelp-forms/' + file).digest('hex');
  let t = fs.readFileSync(path.join(REPO, template), 'utf8').replace(/\r\n/g, '\n').replace(/^guid: .*$/m, 'guid: ' + guid);
  if (pivot) t = t.replace(/spritePivot: \{[^}]*\}/, `spritePivot: {x: ${pivot[0]}, y: ${pivot[1]}}`);
  return t;
}

const check = process.argv.includes('--check');
const files = [];
const entries = [];
for (const f of FORMS) {
  const d = draw(f);
  entries.push(d.entry);
  for (const q of d.pieces) {
    files.push([q.stem + '.png', encodePng(q.dry, d.W, d.H), SPRITE_META, q.pivot]);
    files.push([q.stem + '_wet.png', encodePng(q.wet, d.W, d.H), SPRITE_META, q.pivot]);
    files.push([q.stem + '_height.png', encodePng(q.hgt, d.W, d.H), HEIGHT_META, null]);
  }
}
const contract = JSON.stringify({ pixelsPerMetre: PPM, liftPerMetre: LIFT, forms: entries }, null, 1) + '\n';
let drift = 0;
for (const [name, png, template, pivot] of files) {
  const p = path.join(REPO, OUT, name);
  if (check) { if (!fs.existsSync(p) || !fs.readFileSync(p).equals(png)) { console.error('drifted: ' + name); drift++; } continue; }
  fs.writeFileSync(p, png);
  fs.writeFileSync(p + '.meta', metaFor(name, template, pivot));
}
if (check) {
  if (!fs.existsSync(path.join(REPO, CONTRACT)) || fs.readFileSync(path.join(REPO, CONTRACT), 'utf8').replace(/\r\n/g, '\n') !== contract) { console.error('drifted: Forms.json'); drift++; }
  process.exit(drift ? 1 : 0);
}
fs.writeFileSync(path.join(REPO, CONTRACT), contract);
if (!fs.existsSync(path.join(REPO, CONTRACT) + '.meta')) {
  const guid = crypto.createHash('md5').update('whelp-forms/Forms.json').digest('hex');
  fs.writeFileSync(path.join(REPO, CONTRACT) + '.meta', `fileFormatVersion: 2\nguid: ${guid}\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
}
for (const e of entries) console.log(`${e.id}: ${e.pixels.join('x')} px, tallest ${e.tallestMetres} m, ${e.pieces.map((q) => q.name + ' ' + q.offsetY).join(', ')}`);
