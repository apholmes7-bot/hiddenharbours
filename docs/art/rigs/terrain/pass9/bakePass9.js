/* Hidden Harbours — terrain pass 9, PR 2: bake the ground's maps from the kit, through its public API only.

     node docs/art/rigs/terrain/pass9/bakePass9.js <outDir> [--light tl6|tl5]

   For every material in ../materials.json whose "px" is true, at each ladder step (materials.json's
   ladder.steps, which are the kit's steps 0, 1 and 2):

     b = PxKit8.build(key, step, {pass: 8})          the tile the pass 9 scenes paint with
     G = TerrainLight6.gbuf(b.tile, {relief: b.relief})

     <Name><Step>.png         the albedo: TerrainLight6.view(G, 'unlit'), the rig's bytes
     <Name><Step>_normal.png  TerrainLight6.view(G, 'normal') in RGB; A = the pond depth
     <Name><Step>_light.png   TerrainLight6.view(G, 'light') in RGB; A = pal * 8 + band0
     <Name><Step>_detail.png  TerrainLight6.view(G, 'detail') in RGB; A = the tip bits
     TerrainRelight.json      per tile: its palettes, its height and pond ranges, and each map's sha256

   RGB is the engine contract's, byte for byte. The alphas carry what TerrainLight6.relight reads from the
   tile besides the contract's maps, so a shader can relight from the maps alone:
     _normal A  G.pond, the depth of the texel below its 21-texel mean (relight's puddle test reads it),
                over the tile's largest: round(pond / pondMax * 255).
     _light A   the texel's palette (t.pal, below 16) times 8, plus its authored band (G.band0, 0 to 4).
     _detail A  bit d is set when the texel is its mark's tip toward the floor direction TIP_DIRS[d]
                (relight's own argmax, read back from its o._tips) and it stands proud (G.proud > 0.12),
                which is relight's test for adding a band to a lit tip.
   Each sha256 is over the raw RGBA, rows top-down as the PNG stores them (Unity's GetPixels32 is
   bottom-up). --light tl5 bakes through TerrainLight5 instead; the two must give the same bytes.   */
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm'), zlib = require('zlib'), crypto = require('crypto');

const HERE = __dirname;
const LOAD = ['lib/pixelLanguage.js', 'lib/pxKit.js', 'lib/pxKit2.js', 'lib/weatherSky.js', 'lib/terrainLight4.js', 'lib/pxKit8.js', 'terrainLight5.js', 'terrainLight6.js'];
// floor space: x east, y south. Directions 0 to 7 run clockwise from east, 45 degrees apart.
const R = Math.SQRT1_2;
const TIP_DIRS = [[1, 0], [R, R], [0, 1], [-R, R], [-1, 0], [-R, -R], [0, -1], [R, -R]];
const PROUD = 0.12;                 // relight: `lit && G.proud[i] > 0.12` adds the tip's band

const args = process.argv.slice(2);
const outDir = args.find(a => !a.startsWith('--'));
const lightArg = args.includes('--light') ? args[args.indexOf('--light') + 1] : 'tl6';
if (!outDir || !['tl5', 'tl6'].includes(lightArg)) { console.error('usage: node bakePass9.js <outDir> [--light tl6|tl5]'); process.exit(2); }

const rigSha256 = {};
for (const f of LOAD) {
  const file = path.join(HERE, f), src = fs.readFileSync(file);
  rigSha256[f] = crypto.createHash('sha256').update(src).digest('hex');
  vm.runInThisContext(src.toString('utf8'), { filename: file });
}
const K8 = globalThis.PxKit8, TL = lightArg === 'tl5' ? globalThis.TerrainLight5 : globalThis.TerrainLight6;
if (!K8 || !TL) throw new Error('the kit did not load');

const manifest = JSON.parse(fs.readFileSync(path.join(HERE, '..', 'materials.json'), 'utf8'));
const STEPS = manifest.ladder.steps, SIZE = 256;

// ---- PNG, 8-bit RGBA, filter 0 ------------------------------------------------------------------------
const CRC = new Uint32Array(256);
for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? (0xedb88320 ^ (c >>> 1)) >>> 0 : c >>> 1; CRC[n] = c; }
function crc32(buf) { let c = 0xffffffff; for (let i = 0; i < buf.length; i++) c = (CRC[(c ^ buf[i]) & 255] ^ (c >>> 8)) >>> 0; return (c ^ 0xffffffff) >>> 0; }
function chunk(type, data) {
  const head = Buffer.alloc(4), tail = Buffer.alloc(4), td = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  head.writeUInt32BE(data.length); tail.writeUInt32BE(crc32(td)); return Buffer.concat([head, td, tail]);
}
function png(rgba, w, h) {
  const row = w * 4 + 1, raw = Buffer.alloc(row * h);
  for (let y = 0; y < h; y++) Buffer.from(rgba.buffer, rgba.byteOffset + y * w * 4, w * 4).copy(raw, y * row + 1);
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}
const sha = (a) => crypto.createHash('sha256').update(Buffer.from(a.buffer, a.byteOffset, a.byteLength)).digest('hex');
const hex = (c) => '#' + c.map(v => v.toString(16).padStart(2, '0')).join('');

// ---- the bake -----------------------------------------------------------------------------------------
fs.mkdirSync(outDir, { recursive: true });
const tiles = [];
for (const m of manifest.materials) {
  if (!m.px) continue;
  if (!K8.MATS[m.key]) throw new Error(`materials.json: ${m.key} has "px": true but the kit has no such material`);
  for (let step = 0; step < STEPS.length; step++) {
    const name = m.name + STEPS[step], b = K8.build(m.key, step, { pass: 8 }), t = b.tile;
    if (t.n !== SIZE || t.m !== SIZE || m.size !== SIZE) throw new Error(`${name}: ${t.n}x${t.m}, materials.json says ${m.size}`);
    const G = TL.gbuf(t, { relief: b.relief }), N = G.N;
    const P = t.pals.length;
    if (P > 16) throw new Error(`${name}: ${P} palettes, the light map's alpha holds 16`);
    for (const pal of t.pals) for (const c of pal) for (const v of c) if (!(Number.isInteger(v) && v >= 0 && v <= 255)) throw new Error(`${name}: a palette colour is not a byte`);

    const albedo = TL.view(G, 'unlit', TL.UNLIT), normal = TL.view(G, 'normal', TL.UNLIT);
    const light = TL.view(G, 'light', TL.UNLIT), detail = TL.view(G, 'detail', TL.UNLIT);
    // the tips, as relight finds them: one relight per direction, with the sun off (the tips do not depend on it)
    const tipOf = TIP_DIRS.map(([dx, dy]) => { const o = {}; TL.relight(G, { sunF: [dx, dy, 1], sunI: 0, grade: false }, o); return o._tips; });
    let h0 = Infinity, h1 = -Infinity, pondMax = 0, water = 0, tipTexels = 0;
    for (let i = 0; i < N; i++) if (G.pond[i] > pondMax) pondMax = G.pond[i];
    const pondScale = Math.max(1e-3, pondMax);
    for (let i = 0; i < N; i++) {
      if (G.H[i] < h0) h0 = G.H[i];
      if (G.H[i] > h1) h1 = G.H[i];
      const k = t.mark[i], pal = t.pal[i], band = G.band0[i];
      if (k > 0xffff) throw new Error(`${name}: mark ${k} does not fit the detail map's two bytes`);
      if (pal >= P || band > 4) throw new Error(`${name}: texel ${i} has palette ${pal}, band ${band}`);
      if (albedo[i * 4 + 3] !== 255) throw new Error(`${name}: the albedo is not opaque`);
      if (G.cls[i] === TL.CL.WATER) water++;
      normal[i * 4 + 3] = G.pond[i] / pondScale * 255;     // the clamped array rounds, as view() does
      light[i * 4 + 3] = pal * 8 + band;
      let bits = 0;
      if (k && G.proud[i] > PROUD) for (let d = 0; d < 8; d++) { const tp = tipOf[d].get(k); if (tp && tp[1] === i) bits |= 1 << d; }
      detail[i * 4 + 3] = bits; if (bits) tipTexels++;
    }
    const maps = { albedo, normal, light, detail }, row = {
      name, key: m.key, step, palettes: [].concat(...t.pals.map(p => p.map(hex))),
      heightMin: h0, heightMax: h1, heightRange: Math.max(1e-3, h1 - h0), pondMax: pondScale, sha256: {},
    };
    for (const [kind, a] of Object.entries(maps)) {
      fs.writeFileSync(path.join(outDir, name + (kind === 'albedo' ? '' : '_' + kind) + '.png'), png(a, SIZE, SIZE));
      row.sha256[kind] = sha(a);
    }
    tiles.push(row);
    console.log(`${name.padEnd(16)} palettes ${String(P).padStart(2)}  marks ${String(new Set(t.mark).size - 1).padStart(5)}  tips ${String(tipTexels).padStart(5)}  water ${String(water).padStart(5)}  height ${h0.toFixed(3)}..${h1.toFixed(3)}`);
  }
}
const out = {
  kit: 'Hidden Harbours terrain relight bake', version: 1, light: lightArg === 'tl5' ? 'terrainLight5.js' : 'terrainLight6.js',
  driver: 'bakePass9.js', rigSha256, size: SIZE, ladder: STEPS,
  rows: 'every sha256 is over raw RGBA, rows top-down as the PNG stores them',
  alpha: { normal: 'G.pond / pondMax * 255', light: 'pal * 8 + band0', detail: 'bit d: the texel is its mark\'s tip toward tipDirections[d] and G.proud > ' + PROUD },
  tipDirections: TIP_DIRS, tiles,
};
fs.writeFileSync(path.join(outDir, 'TerrainRelight.json'), JSON.stringify(out, null, 1) + '\n');
console.log(`${tiles.length} tiles, ${tiles.length * 4} maps -> ${outDir}`);
