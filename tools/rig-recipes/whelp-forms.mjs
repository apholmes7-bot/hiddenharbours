// whelp-forms.mjs: the Harbour Stack and the three Whelps, drawn from their shapes.
//
// The lookout's scene gives each of these a centre, a size and a height (the stack: an oval 3.2 by 2.6
// across at the sea, its top 6.2 m up, standing on a plinth; the Whelps: three low ledges). Jointed
// sandstone does not weather round. It breaks along its upright joints into blocks and along its beds
// into steps, so each form is built as a crowd of upright blocks, every block with a flat top at its own
// height: tall and narrow at the stack's summit, stepping down and out to the sea, with a few fallen at
// its foot. The camera looks at that the way the game's does (a metre of height lifts a thing 0.766 of
// a metre up the picture), so a block shows its top and the faces that look toward the viewer, each
// face one plane with a sunlit or a shadowed side. The stone is the South Arm cliff's, a good deal
// darker; rough turf holds only the summit. No outline is drawn.
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
import { withoutTemplateSprites } from './lib/unity-meta.mjs';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const OUT = 'Assets/_Project/Art/Sprites/StPeters/Whelps';
const CONTRACT = OUT + '/Forms.json';
const SPRITE_META = 'Assets/_Project/Art/Sprites/StPeters/SetPieces/stpLandingDeck.png.meta';
const HEIGHT_META = 'Assets/_Project/Art/Sprites/StPeters/SetPieces/stpLandingDeck_height.png.meta';

const PPM = 32;                 // pixels a metre, the game's
const LIFT = 0.766;             // picture rise per metre of height, the camera's
const STEP = 1 / (PPM * 2);     // the march across the ground, half a pixel
const FLOOR = -1.6;             // every form is drawn down to here, below the seabed it stands on

// ---- the stone ----
// The stack: the South Arm's red sandstone seen against the light, five tones a bed, dark to lit.
const DEEP = [[38, 22, 22], [56, 30, 26], [78, 40, 30], [102, 54, 36], [130, 72, 46]];
const RUST = [[42, 26, 24], [62, 36, 28], [88, 48, 32], [114, 62, 40], [142, 82, 52]];
const SEAM = [[28, 20, 20], [38, 26, 24], [50, 34, 28], [64, 44, 34], [80, 56, 42]];
const PALE = [[58, 36, 30], [82, 50, 38], [108, 66, 46], [132, 84, 58], [156, 104, 74]];
// The beds from the floor up: how thick, and which stone. The pattern repeats.
const BEDS = [[0.46, DEEP], [0.08, SEAM], [0.58, RUST], [0.34, DEEP], [0.07, SEAM], [0.26, PALE], [0.66, DEEP], [0.10, SEAM], [0.50, RUST]];
const BEDS_TOTAL = BEDS.reduce((s, b) => s + b[0], 0);
const BED_LINE = 0.39;          // the partings drawn across a face, metres apart: half a step of the stack's beds
// The reef the sea covers: the seabed's own browns and greys.
const REEF = [[36, 28, 28], [50, 37, 34], [66, 47, 40], [84, 58, 47], [102, 72, 56]];
const SLATE = [[38, 35, 36], [52, 47, 46], [68, 61, 57], [84, 76, 68], [100, 91, 80]];
const TURF = [[38, 52, 32], [50, 66, 36], [66, 82, 38], [84, 96, 46], [100, 106, 58]];
const WEED = [[32, 28, 20], [46, 38, 24], [60, 50, 28], [76, 62, 34], [90, 78, 42]];
const BARNACLE = [150, 142, 122];

const JOINT = SEAM[0];          // the line between two faces, and under a block where it sits on another
const SUN = [-0.45, -0.28];     // a metre toward the sun across the ground: the left and the front

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
const clampTone = (t) => Math.max(0, Math.min(4, Math.round(t)));

// ---- the blocks ----
// The ground is cut into cells, one seed in each square of a grid and pushed about inside it. A point
// belongs to the nearest seed's block. `build` says what stands on a seed (or nothing); it is asked once.
//
// Broken stone, not cut stone (owner 11 26: "far too smooth ... where the jaggedness and rough edges"). With
// `rough` the edge between two blocks is not a straight bisector: the point is pushed about by two octaves of
// noise before its block is found, so every joint wanders and kinks. And a block's top is chipped along its
// edges: within `chip.width` of a joint, where noise picks it, the top drops by `chip.depth`, so a top edge
// reads as broken lips and notches and the face under it as a ragged silhouette. `keyBase` keeps a second
// field's cells (the rubble) apart from the first's.
function blocks(size, seed, build, rough, keyBase = 0) {
  const made = new Map();
  const cellAt = (i, j) => {
    const key = keyBase + (i + 512) * 1024 + (j + 512);
    let c = made.get(key);
    if (c === undefined) {
      const cx = (i + 0.5 + 0.82 * (hash(i, j, seed) - 0.5)) * size, cy = (j + 0.5 + 0.82 * (hash(i, j, seed + 1) - 0.5)) * size;
      c = { key, i, j, cx, cy, built: build(cx, cy, i, j) };
      made.set(key, c);
    }
    return c;
  };
  return (x, y) => {
    let qx = x, qy = y;
    if (rough) {
      const w = rough.warp;
      qx += w.amp * 2 * (noise(x * w.freq, y * w.freq, seed + 501) - 0.5) + w.fine * 2 * (noise(x * w.fineFreq, y * w.fineFreq, seed + 503) - 0.5);
      qy += w.amp * 2 * (noise(x * w.freq, y * w.freq, seed + 502) - 0.5) + w.fine * 2 * (noise(x * w.fineFreq, y * w.fineFreq, seed + 504) - 0.5);
    }
    const gi = Math.floor(qx / size), gj = Math.floor(qy / size);
    let best = null, bd = Infinity, second = null, sd = Infinity;
    for (let j = gj - 1; j <= gj + 1; j++) for (let i = gi - 1; i <= gi + 1; i++) {
      const c = cellAt(i, j), d = (qx - c.cx) * (qx - c.cx) + (qy - c.cy) * (qy - c.cy);
      if (d < bd) { second = best; sd = bd; bd = d; best = c; } else if (d < sd) { sd = d; second = c; }
    }
    const b = best.built;
    if (!b) return null;
    let z = b.h + b.tx * (x - best.cx) + b.ty * (y - best.cy);                    // a block's top is one plane, a little off level
    let chipped = false;
    if (rough && rough.chip && b.kind !== 'turf' && second) {
      // How far in from the joint: (d2 - d1) / (2 |c2 - c1|) is the distance to the bisector, exactly.
      const gap = Math.hypot(second.cx - best.cx, second.cy - best.cy);
      const edge = gap > 1e-6 ? (sd - bd) / (2 * gap) : Infinity;
      const ch = rough.chip;
      const reach = ch.width * (0.35 + 0.65 * noise(x * ch.freq, y * ch.freq, seed + 511));
      if (edge < reach && noise(x * ch.freq * 0.5, y * ch.freq * 0.5, seed + 513) > ch.gate) {
        z -= ch.depth * (edge < reach * 0.5 ? 2 : 1);
        chipped = true;
      }
    }
    return z >= FLOOR ? { z, cell: best, b, chipped } : null;
  };
}

// ---- the shapes ----
// Each returns what stands at a point of the form's own ground (metres from its centre), or null off it.
function stackShape(f) {
  const rot = f.rot * Math.PI / 180, c = Math.cos(rot), s = Math.sin(rot);
  const ceiling = FLOOR + f.range - 0.1;
  const footOf = (x, y) => {                                                     // 1 where the stack meets the sea
    const u = x * c + y * s, v = -x * s + y * c;
    const a = Math.atan2(v / f.radii[1], u / f.radii[0]);
    const out = 1 + 0.15 * Math.sin(2 * a + 0.6) + 0.11 * Math.sin(3 * a + 1.9) + 0.22 * (noise(a * 2.2 + 5, 0.5, 83) - 0.5);
    return Math.hypot(u / f.radii[0], v / f.radii[1]) / out;
  };
  const main = blocks(f.block, 83, (x, y, i, j) => {
    const r1 = hash(i, j, 97), r2 = hash(i, j, 101), r3 = hash(i, j, 113), r4 = hash(i, j, 127);
    const u = x * c + y * s, v = -x * s + y * c;
    const a = Math.atan2(v / f.radii[1], u / f.radii[0]);
    const out = 1 + 0.15 * Math.sin(2 * a + 0.6) + 0.11 * Math.sin(3 * a + 1.9) + 0.22 * (noise(a * 2.2 + 5, 0.5, 83) - 0.5);
    const foot = Math.hypot(u / f.radii[0], v / f.radii[1]) / out;               // 1 where the stack meets the sea
    const su = f.summit[0] * c + f.summit[1] * s, sv = -f.summit[0] * s + f.summit[1] * c;
    const peak = Math.hypot((u - su) / f.radii[0], (v - sv) / f.radii[1]) / out; // the same, measured from the summit
    const w = Math.max(0, 1 - foot), q = foot * (1 - w) + peak * w;
    const tilt = { tx: 0.16 * (r3 - 0.5), ty: 0.12 * (r4 - 0.5) };               // broad slabs lie near level
    const stone = { kind: 'stone', dip: 0.08 * (r2 - 0.5), bright: r3 > 0.55, ...tilt }; // the beds run on from block to block
    if (peak <= f.crown) {                                                       // the summit: turf, and bare stone through it
      if (r1 > f.bare) return { kind: 'turf', h: f.top + 0.22 * (r2 - 0.5), dip: 0, bright: r3 > 0.5, tx: 0.2 * (r3 - 0.5), ty: 0.15 * (r4 - 0.5) };
      return { ...stone, h: Math.min(ceiling, f.top + 0.10 + 0.40 * r2) };
    }
    let h = q < 1 ? f.top * (1 - Math.pow((q - f.crown) / (1 - f.crown), f.flare)) : -1;
    for (const k of f.shoulders) {                                               // a lower head or two beside the summit
      const d = Math.hypot((x - k.x) / k.r, (y - k.y) / k.r);
      if (d < 1) h = Math.max(h, k.h * (1 - Math.pow(d, 1.3)));
    }
    if (h > 0.15) {
      h += f.rough * (r1 - 0.5) * Math.sin(Math.PI * Math.min(1, h / f.top));     // broken, most of all half way up
      h = Math.round(h / f.bed) * f.bed + 0.12 * (r2 - 0.5);                      // and stepped along its beds
      if (peak < f.crown + 0.08 && r3 > f.spires) h = f.top - 0.2 + 0.5 * r2;      // a few points of stone at the turf's edge
      if (h >= f.top - f.sward && r1 > f.bare && r3 <= f.spires)                  // the turf runs over the whole upper step
        return { kind: 'turf', h: Math.min(ceiling, h), dip: 0, bright: r3 > 0.5, tx: 0.2 * (r3 - 0.5), ty: 0.15 * (r4 - 0.5) };
      return { ...stone, h: Math.max(0.25, Math.min(ceiling, h)) };
    }
    if (foot < f.fallen.reach && r1 > f.fallen.few + (foot - 1) / (f.fallen.reach - 1) * (1 - f.fallen.few))
      return { ...stone, kind: 'fallen', h: 0.15 + f.fallen.tall * r2 * (f.fallen.reach - foot) / (f.fallen.reach - 1) };
    // The plinth: reef slabs stepping down from under the stack to the sea floor.
    const rd = Math.hypot(x, y), rim = f.plinth.radius * (0.74 + 0.36 * noise(a * 1.9 + 7, 0.5, 61));
    if (rd > rim) return null;
    const inner = (f.radii[0] + f.radii[1]) / 2;
    const t = Math.round(smooth(1 - (rd - inner) / Math.max(0.5, rim - inner)) * 4) / 4;
    return { kind: 'slab', grey: r3 > 0.62, shade: r4, dip: 0, bright: false, tx: 0, ty: 0,
      h: f.plinth.z + (f.plinth.top - f.plinth.z) * t + 0.10 * (r2 - 0.5) };
  }, f.broken);
  if (!f.talus) return main;
  // The talus: a scree of small broken blocks fallen against the foot, thickest at the foot and thinning out to
  // sea. A field of its own, finer than the stack's, standing only where it rises above what is already there.
  const T = f.talus;
  const rubble = blocks(T.block, 89, (x, y, i, j) => {
    const r1 = hash(i, j, 131), r2 = hash(i, j, 137), r3 = hash(i, j, 139), r4 = hash(i, j, 149);
    const ft = footOf(x, y);
    if (ft < T.from || ft > T.reach) return null;
    const near = 1 - (ft - T.from) / (T.reach - T.from);                         // 1 at the foot, 0 at the scree's edge
    if (y > T.back * f.radii[1]) return null;                                    // behind the body it would hang in the air
    if (near < 0.5 && r1 > T.cover) return null;                                  // the inner apron is whole, so every block touches it
    return { kind: 'fallen', h: T.low + (T.tall - T.low) * near * (0.4 + 0.6 * r2), dip: 0.08 * (r3 - 0.5), bright: r3 > 0.5,
      tx: T.tilt * (r3 - 0.5), ty: T.tilt * (r4 - 0.5) };
  }, f.broken && { warp: f.broken.warp, chip: T.chip }, 1 << 21);
  return (x, y) => {
    const m = main(x, y), r = rubble(x, y);
    return r && (!m || r.z > m.z) ? r : m;
  };
}
function ledgeShape(f) {
  const rise = f.top - FLOOR;
  return blocks(f.block, 40 + f.seed, (x, y, i, j) => {
    const r2 = hash(i, j, 101 + f.seed), r3 = hash(i, j, 113 + f.seed), r4 = hash(i, j, 127 + f.seed);
    const a = Math.atan2(y, x);
    const wob = 1 + 0.16 * Math.sin(2 * a + f.seed) + 0.10 * Math.sin(5 * a + 1.7 * f.seed) + 0.06 * Math.sin(9 * a + 0.3 * f.seed);
    const d = Math.hypot(x, y) / (f.radius * wob);                               // 1 at the ledge's rim
    if (d > 1.12 + 0.12 * r4) return null;
    const t = Math.round((1 - smooth((d - 0.70) / 0.5)) * 3) / 3;                 // flat-topped, stepped to the rim
    let h = FLOOR + rise * t + 0.06 * (r2 - 0.5), kind = 'slab';
    for (const k of f.crags) {
      const q = Math.hypot((x - k.x) / k.r, (y - k.y) / (k.r * 0.8));
      if (q < 1) { h = Math.max(h, f.top + Math.round(k.h * (1 - q * q) / 0.22) * 0.22 + 0.05 * (r2 - 0.5)); kind = 'crag'; }
    }
    return { kind, grey: r3 > 0.62, shade: r4, dip: 0, bright: r3 > 0.5, tx: 0, ty: 0, h };
  }, f.broken);
}

// How a reef ledge is broken: its joints wander, its slab edges are chipped, its flats are crossed by fissures.
const REEF_ROUGH = { warp: { amp: 0.12, freq: 2.6, fine: 0.04, fineFreq: 8 }, chip: { width: 0.12, freq: 6, gate: 0.45, depth: 0.12 },
  crack: { freq: 2.2, lean: 0.35, width: 0.026, gateFreq: 1.1, gate: 0.52 }, spall: null, seamRun: 5, seamKeep: 0.45, bite: { run: 5, depth: 2 } };

const FORMS = [
  { id: 'tidalform.stp_harbour_stack', kind: 'stack', radii: [3.2, 2.6], rot: 20, top: 6.2, block: 1.05, bed: 0.78,
    summit: [-0.2, 0.15], crown: 0.6, bare: 0.08, flare: 3.0, rough: 0.9, spires: 0.86, sward: 1.0,
    shoulders: [{ x: 1.35, y: -0.55, r: 1.3, h: 3.7 }, { x: -1.5, y: -0.2, r: 0.9, h: 2.2 }],
    fallen: { reach: 1.42, few: 0.2, tall: 0.55 },
    plinth: { radius: 5.5, z: -1.2, top: -0.3 }, range: 8.4, reach: 5.9, footAt: 0.0,
    broken: { warp: { amp: 0.16, freq: 1.8, fine: 0.025, fineFreq: 5 }, chip: { width: 0.18, freq: 5, gate: 0.4, depth: 0.2 },
      crack: { freq: 2.6, lean: 0.35, width: 0.03, gateFreq: 1.3, gate: 0.5 }, spall: { freq: 2.4, gate: 0.66 },
      seamRun: 5, seamKeep: 0.45, bite: { run: 9, depth: 6 } },
    talus: { block: 0.45, from: 0.9, reach: 1.12, cover: 0.6, back: 0.15, low: 0.02, tall: 0.45, tilt: 0.35,
      chip: { width: 0.08, freq: 9, gate: 0.5, depth: 0.1 } },
    pieces: [{ name: 'Body', stem: 'HarbourStack', from: -0.1 }, { name: 'SeabedPlinth', stem: 'HarbourStackPlinth', below: -0.1 }] },
  { id: 'tidalform.stp_whelp_1', pieces: [{ name: 'SeabedLedge', stem: 'Whelp1' }], kind: 'ledge', radius: 2.8, top: -1.04, block: 0.7, seed: 1, broken: REEF_ROUGH, range: 2, reach: 4.7, footAt: -1.0,
    crags: [{ x: -0.6, y: 0.9, r: 0.9, h: 0.75 }, { x: 1.5, y: 0.2, r: 0.7, h: 0.55 }, { x: -1.8, y: -0.2, r: 0.6, h: 0.4 }] },
  { id: 'tidalform.stp_whelp_2', pieces: [{ name: 'SeabedLedge', stem: 'Whelp2' }], kind: 'ledge', radius: 2.2, top: -1.10, block: 0.7, seed: 2, broken: REEF_ROUGH, range: 2, reach: 3.8, footAt: -1.0,
    crags: [{ x: 0.2, y: 0.5, r: 0.85, h: 0.7 }, { x: -1.2, y: 0.1, r: 0.55, h: 0.45 }] },
  { id: 'tidalform.stp_whelp_3', pieces: [{ name: 'SeabedLedge', stem: 'Whelp3' }], kind: 'ledge', radius: 2.0, top: -0.35, block: 0.7, seed: 3, broken: REEF_ROUGH, range: 2, reach: 3.5, footAt: -1.0,
    crags: [{ x: 0.5, y: 0.6, r: 0.7, h: 0.5 }, { x: -0.9, y: 0.7, r: 0.5, h: 0.35 }] },
];

// ---- the paint ----
function bedOf(z) {
  let h = ((z - FLOOR) % BEDS_TOTAL + BEDS_TOTAL) % BEDS_TOTAL;
  for (const b of BEDS) { if (h < b[0]) return b[1]; h -= b[0]; }
  return DEEP;
}
// p: { x, y, z, b (the block), wall, wnx (which way a face looks, -1 left to 1 right), shaded }
function paint(f, p, px, py) {
  const b = p.b, grit = hash(px, py, 7);
  const speck = grit > 0.93 ? 1 : grit < 0.07 ? -1 : 0;
  if (b.kind === 'turf' && (!p.wall || b.h - p.z < 0.14 + 0.16 * hash(px, 0, 13)))
    return TURF[clampTone((p.wall ? 0.6 : 1.4) + 2.4 * noise(p.x * 5.2, p.y * 5.2, 17) - (p.shaded ? 1 : 0) + speck)];
  const reef = b.kind === 'slab' || b.kind === 'crag';
  let ramp = reef ? (b.grey ? SLATE : REEF) : bedOf(p.z + b.dip);
  let tone;
  if (p.wall) tone = p.wnx < -0.3 ? 3 : p.wnx > 0.35 ? 1 : 2;                    // the sunlit side, the front, the shadow side
  else tone = b.bright ? 4 : 3;                                                  // a top takes the sky
  if (p.shaded) tone -= p.wall ? 1 : 2;
  if (reef) tone = (p.wall ? 0 : 1) + Math.floor(b.shade * 2.4) - (p.shaded ? 1 : 0) + (b.kind === 'crag' && !p.wall ? 1 : 0);
  else if (p.z < 0.35) tone -= 1;                                                // where the tide works the stone is darker
  // Weed lies in patches on the reef's flats, and nowhere on the stack: a band of it reads as a drawn line.
  if (reef && !p.wall && noise(p.x * 1.6, p.y * 1.6, 23) * 0.65 + noise(p.x * 4.2, p.y * 4.2, 29) * 0.35 > 0.60)
    return WEED[clampTone(tone + speck)];
  const R = f.broken;
  if (R && b.kind !== 'turf') {
    // Cracks: one-pixel dark fissures where a noise field crosses its middle, gated so each runs part way and
    // stops. Up a face they wander about the upright; across a top they run every way.
    const k = R.crack, cell = ((p.cellKey || 0) % 97) * 0.37;                   // each block's cracks are its own
    const ridge = p.wall ? Math.abs(noise(p.x * k.freq, p.z * k.freq * k.lean + cell, 211) - 0.5)
                         : Math.abs(noise(p.x * k.freq, p.y * k.freq, 223) - 0.5);
    const gate = p.wall ? noise(p.x * k.gateFreq, p.z * k.gateFreq, 227) : noise(p.x * k.gateFreq, p.y * k.gateFreq, 229);
    if (ridge < k.width && gate > k.gate) return (reef ? (b.grey ? SLATE : REEF) : SEAM)[p.shaded || !p.wall ? 0 : 1];
    // Spalls: patches where a skin of stone has flaked off, a band darker than the face about them, their upper
    // edge a shadowed lip.
    const sp = R.spall;
    if (p.wall && sp) {
      const n = noise(p.x * sp.freq, p.z * sp.freq * 1.4, 233) * 0.7 + noise(p.x * sp.freq * 3, p.z * sp.freq * 3, 239) * 0.3;
      if (n > sp.gate) {
        const lip = noise(p.x * sp.freq, (p.z + 1 / PPM / LIFT * 1.5) * sp.freq * 1.4, 233) * 0.7 +
                    noise(p.x * sp.freq * 3, (p.z + 1 / PPM / LIFT * 1.5) * sp.freq * 3, 239) * 0.3 <= sp.gate;
        tone += lip ? -2 : -1;
      }
    }
  }
  if (!reef) {
    // The bedding: a thin dark parting every bed across a face, level from block to block, broken here and there.
    const inBed = ((p.z + b.dip - FLOOR) % BED_LINE + BED_LINE) % BED_LINE;
    if (p.wall && inBed < 0.05 && noise(p.x * 2.4, p.z * 3.1, 37) > 0.22) return SEAM[clampTone(1 + (p.shaded ? -1 : 0))];
    // Fallen blocks at the foot are washed pale by the spray and crusted white; so is the stack's own foot, in patches.
    const washed = b.kind === 'fallen' || (p.z < 0.7 && noise(p.x * 3.3, p.y * 3.3 + p.z * 2, 43) > 0.55 - 0.3 * (0.7 - p.z));
    if (washed) {
      if (hash(px, py, 51) > (b.kind === 'fallen' ? 0.8 : 0.9)) return BARNACLE;
      ramp = PALE; tone += b.kind === 'fallen' ? 1 : 0;
    }
  }
  if (!reef && p.z > -0.1 && p.z < 0.55 && hash(px, py, 51) > 0.93) return BARNACLE;
  return ramp[clampTone(tone + speck)];
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
  const face = new Int32Array(W * H);          // which plane a pixel belongs to: a block's top, or one of its faces
  const part = new Uint8Array(W * H);          // 0 nothing, 1 a top, 2 a face, 3 turf
  const own = new Int32Array(W * H);           // the block a pixel is part of
  let tallest = FLOOR;
  const shadedAt = (x, y, z) => {
    for (let k = 1; k <= 3; k++) { const m = shape(x + SUN[0] * k, y + SUN[1] * k); if (m && m.z - z > 0.42 * k) return true; }
    return false;
  };
  for (let px = 0; px < W; px++) {
    const x = (px + 0.5) / PPM - R;
    let horizon = -Infinity, zAtHorizon = FLOOR, open = true, before = null;
    for (let y = -R; y <= R; y += STEP) {
      const m = shape(x, y);
      if (!m) { open = true; before = null; continue; }
      const s = (y + LIFT * m.z) * PPM;
      if (open) {                                                                // the front of a form, or of a part that stands alone
        const ground = (y + LIFT * FLOOR) * PPM;
        if (ground > horizon) { horizon = ground; zAtHorizon = FLOOR; }
        open = false;
      }
      const last = before;
      before = m;
      if (s <= horizon) continue;
      const from = Math.ceil(horizon - 0.5), to = Math.floor(s - 0.5);
      // A face: the block stands above what is before it (another block, or its own chipped lip).
      const risen = (!last || last.cell !== m.cell || last.chipped !== m.chipped) && to - from >= 1;
      const topPlane = m.chipped ? -(m.cell.key * 2) - 1 : m.cell.key * 2;
      let wnx = 0, plane = topPlane;
      if (risen && last && last.cell === m.cell) {                                 // the chip's own back wall faces the viewer
        plane = ((Math.imul(m.cell.key, 40503) ^ 0x5bd1) << 1) | 1;
      } else if (risen) {
        const dx = last ? last.cell.cx - m.cell.cx : x - m.cell.cx, dy = last ? last.cell.cy - m.cell.cy : y - m.cell.cy;
        wnx = dx / (Math.hypot(dx, dy) || 1);
        plane = ((Math.imul(m.cell.key, 40503) ^ (last ? last.cell.key : 7)) << 1) | 1;
      }
      for (let r = from; r <= to; r++) {
        const py = H - 1 - (r - sLo);                                            // picture rows run down
        if (py < 0 || py >= H) continue;
        const wall = risen && r < to;                                            // the last row is the top's own edge
        const t = s - horizon > 1e-6 ? (r + 0.5 - horizon) / (s - horizon) : 1;
        const z = wall ? zAtHorizon + (m.z - zAtHorizon) * Math.max(0, Math.min(1, t)) : m.z;
        const rgb = paint(f, { x, y, z, b: m.b, wall, wnx, shaded: shadedAt(x, y, z), cellKey: m.cell.key }, px, r);
        const i = (py * W + px) * 4;
        dry[i] = rgb[0]; dry[i + 1] = rgb[1]; dry[i + 2] = rgb[2]; dry[i + 3] = 255;
        const code = Math.max(0, Math.min(255, Math.round((z - FLOOR) / f.range * 255)));
        hgt[i] = code; hgt[i + 1] = code; hgt[i + 2] = code; hgt[i + 3] = 255;
        zs[py * W + px] = z;
        face[py * W + px] = wall ? plane : topPlane;
        part[py * W + px] = m.b.kind === 'turf' && rgb[1] > rgb[0] ? 3 : wall ? 2 : 1;
        own[py * W + px] = m.cell.key;
        if (z > tallest) tallest = z;
      }
      horizon = s; zAtHorizon = m.z;
    }
  }
  // A broken outline: where a face stands against open sky at the form's left or right edge, the edge is bitten
  // back a few pixels in runs a bed or so tall, so a tall wall's silhouette steps in and out along its beds
  // instead of running down one ruled line. in = +1 bites the left edge rightward, -1 the right edge leftward.
  const bite = f.broken && f.broken.bite;
  if (bite) for (const inward of [1, -1]) {
    for (let py = 0; py < H; py++) {
      const band = Math.floor((py + Math.floor(hash(inward, 0, 301) * bite.run)) / bite.run);
      for (let n = 0; n < W; n++) {
        const px = inward > 0 ? n : W - 1 - n, k = py * W + px, out = px - inward;
        if (!part[k]) continue;
        if (part[k] === 2 && !(out >= 0 && out < W && part[py * W + out])) {
          const depth = Math.floor(hash(band, px >> 3, 307 + inward) * (bite.depth + 1));
          for (let d = 0; d < depth; d++) {
            const q = px + inward * d, j = py * W + q;
            if (q < 0 || q >= W || part[j] !== 2) break;
            part[j] = 0; zs[j] = -99; dry.fill(0, j * 4, j * 4 + 4); hgt.fill(0, j * 4, j * 4 + 4);
          }
        }
        break;                                                                     // the outermost pixel of the row only
      }
    }
  }
  // The joints: a dark line where one plane of stone meets another, and under a block where it sits on the
  // one below. Only between stone and stone; the form's own edge against the sea is left as it is.
  const joint = new Uint8Array(W * H);
  for (let py = 0; py < H; py++) for (let px = 1; px < W; px++) {
    const k = py * W + px, l = k - 1;
    if (!part[k] || !part[l] || part[k] === 3 || part[l] === 3 || face[k] === face[l]) continue;
    const faces = part[k] === 2 || part[l] === 2;
    // Two faces of ONE block whose joint wanders (a broken edge) meet in a broken crack, not a ruled line.
    if (faces && f.broken && own[k] === own[l] && hash(px, Math.floor(py / f.broken.seamRun), 73) > f.broken.seamKeep) continue;
    if (faces || (Math.abs(zs[k] - zs[l]) < 0.5 && hash(px, py, 71) > 0.25)) joint[k] = faces ? 2 : 1;
  }
  for (let py = 0; py < H - 1; py++) for (let px = 0; px < W; px++) {
    const k = py * W + px, under = k + W;
    if (part[k] === 2 && part[under] === 1 && face[k] !== face[under]) joint[k] = 2;
  }
  for (let k = 0; k < W * H; k++) {
    if (!joint[k]) continue;
    const c = joint[k] === 2 ? JOINT : SEAM[1];
    dry[k * 4] = c[0]; dry[k * 4 + 1] = c[1]; dry[k * 4 + 2] = c[2];
  }
  for (let k = 0; k < W * H; k++) {
    if (!part[k]) continue;
    const wv = wet([dry[k * 4], dry[k * 4 + 1], dry[k * 4 + 2]]);
    wetb[k * 4] = wv[0]; wetb[k * 4 + 1] = wv[1]; wetb[k * 4 + 2] = wv[2]; wetb[k * 4 + 3] = 255;
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
  let t = withoutTemplateSprites(fs.readFileSync(path.join(REPO, template), 'utf8').replace(/\r\n/g, '\n')).replace(/^guid: .*$/m, 'guid: ' + guid);
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
// --out <dir>: a preview. The pictures and the contract go to that folder and nothing in the project is touched.
const outAt = process.argv.indexOf('--out');
if (outAt > 0 && process.argv[outAt + 1]) {
  const dir = path.resolve(process.argv[outAt + 1]);
  fs.mkdirSync(dir, { recursive: true });
  for (const [name, png] of files) fs.writeFileSync(path.join(dir, name), png);
  fs.writeFileSync(path.join(dir, 'Forms.json'), contract);
  for (const e of entries) console.log(`${e.id}: ${e.pixels.join('x')} px, tallest ${e.tallestMetres} m (preview in ${dir})`);
  process.exit(0);
}
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
