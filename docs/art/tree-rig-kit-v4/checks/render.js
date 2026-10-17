#!/usr/bin/env node
// Tree kit review renders and sample maps, pass 4.1 — Node 18+, no packages. Run from anywhere:
//   node checks/render.js                   every review render → renders/<name>.png
//   node checks/render.js wind-gale-west    just those
//   node checks/render.js maps [Species] [stage]   the weights-pipeline sheets for one species × stage
//                                            (default RedMaple mature) → maps/, and maps/treeMaps4.json
'use strict';
const fs = require('fs'), path = require('path'), zlib = require('zlib'), crypto = require('crypto');
const KIT = path.resolve(__dirname, '..');
for (const f of ['treeIsoRig4.js', 'weatherSky.js', 'treeMaps4.js', 'lib/treeIsoRig3.js', 'lib/pixelLanguage.js', 'lib/pxKit.js', 'lib/harmonyScenes.js', 'checks/renderReview.js'])
  (0, eval)(fs.readFileSync(path.join(KIT, f), 'utf8'));

const CRC = Array.from({ length: 256 }, (_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0; });
const crc = (b) => { let c = 0xffffffff; for (const x of b) c = CRC[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
function png(w, h, rgba) {   // RGBA8, filter 0, one IDAT
  const row = w * 4 + 1, raw = Buffer.alloc(row * h);
  for (let y = 0; y < h; y++) Buffer.from(rgba.buffer, rgba.byteOffset + y * w * 4, w * 4).copy(raw, y * row + 1);
  const chunk = (t, d) => { const n = Buffer.alloc(4), c = Buffer.alloc(4), td = Buffer.concat([Buffer.from(t, 'latin1'), d]); n.writeUInt32BE(d.length); c.writeUInt32BE(crc(td)); return Buffer.concat([n, td, c]); };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}
const write = (rel, img) => { fs.mkdirSync(path.dirname(path.join(KIT, rel)), { recursive: true }); fs.writeFileSync(path.join(KIT, rel), png(img.w, img.h, img.rgba)); console.log(rel, img.w + '×' + img.h); };
const sha = (rel) => crypto.createHash('sha256').update(fs.readFileSync(path.join(KIT, rel))).digest('hex');

const args = process.argv.slice(2);
if (args[0] === 'maps') {
  const M4 = globalThis.TreeMaps4, key = args[1] || 'RedMaple', stage = args[2] || 'mature', files = [];
  for (const season of ['summer', 'autumn', 'winter']) for (const ch of M4.SHEET_MAPS) {
    if (season === 'autumn' && (ch !== 'unlit' || !globalThis.TreeRig4.byKey[key].fall)) continue;   // autumn shares every geometry map; an evergreen's autumn is its summer
    const rel = 'maps/' + key + '_' + stage + '_' + season + '_' + ch + '.png';
    write(rel, M4.sheet(key, stage, season, ch)); files.push(rel);
  }
  const man = M4.manifest();
  const out = Object.assign({ schema: man.schema, generated: new Date().toISOString().slice(0, 10), derivedFromRigSha256: sha('treeIsoRig4.js'), mapsDerivedFromSha256: sha('treeMaps4.js') }, man,
    { sample: { species: key, stage, files, note: 'One species × stage baked as the weights pipeline ships it: summer 7 maps, autumn _unlit only (every other map is summer\'s), winter 7 maps. TreeMaps4.sheet(key, stage, season, map) bakes any other.' } });
  fs.writeFileSync(path.join(KIT, 'maps', 'treeMaps4.json'), JSON.stringify(out, null, 2));
  console.log('maps/treeMaps4.json');
} else {
  for (const n of args.length ? args : globalThis.TREE_REVIEW.NAMES) write('renders/' + n + '.png', globalThis.TREE_REVIEW.render(n));
}
