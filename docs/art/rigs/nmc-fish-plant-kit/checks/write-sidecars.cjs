'use strict';
/* write-sidecars.cjs: writes gameplay/<key>.gameplay.json for every piece and sources.json. Node 18+, built-ins only.
     node docs/art/rigs/nmc-fish-plant-kit/checks/write-sidecars.cjs
   A sidecar is the kit's own gameplay(key) at the piece's defaults, plus derivedFromRigSha256: the SHA-256 of
   nmcFishPlant.js with LF line endings. Re-run it in the same change as any edit to the rig. */
const fs = require('fs'), path = require('path'), vm = require('vm'), crypto = require('crypto');
const KIT = path.resolve(__dirname, '..'), LIBKIT = 'nmc-set-pieces-kit', LIBS = ['lib/weatherSky.js', 'lib/coastalPass.js'], RIG = 'nmcFishPlant.js';
const lf = (file) => fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
const shaLf = (file) => crypto.createHash('sha256').update(Buffer.from(lf(file))).digest('hex');
function load(extra) { const ctx = vm.createContext({ console });
  for (const f of LIBS) vm.runInContext(fs.readFileSync(path.join(KIT, '..', LIBKIT, f), 'utf8'), ctx, { filename: f });
  for (const f of extra || []) vm.runInContext(fs.readFileSync(path.join(KIT, '..', LIBKIT, f), 'utf8'), ctx, { filename: f });
  vm.runInContext(fs.readFileSync(path.join(KIT, RIG), 'utf8'), ctx, { filename: RIG }); return ctx; }
function sidecar(NF, key, rigSha) { const g = NF.gameplay(key), out = {}; for (const k of Object.keys(g)) { out[k] = g[k]; if (k === 'generatedBy') out.derivedFromRigSha256 = rigSha; } return out; }
function sources() { return { schema: 'hidden-harbours/nmc-fish-plant-sources@1',
  note: 'This kit carries no copy of the light: it loads the set pieces kit’s, in this order, then its own rig. Hashes are SHA-256 over LF line endings.',
  load: LIBS.map(f => ({ file: '../' + LIBKIT + '/' + f, sha256Lf: shaLf(path.join(KIT, '..', LIBKIT, f)) })).concat([{ file: RIG, sha256Lf: shaLf(path.join(KIT, RIG)) }]) }; }
if (require.main === module) { const NF = load().NmcFishPlant, rigSha = shaLf(path.join(KIT, RIG)); fs.mkdirSync(path.join(KIT, 'gameplay'), { recursive: true });
  for (const key of NF.KEYS) fs.writeFileSync(path.join(KIT, 'gameplay', key + '.gameplay.json'), JSON.stringify(sidecar(NF, key, rigSha), null, 1) + '\n');
  fs.writeFileSync(path.join(KIT, 'sources.json'), JSON.stringify(sources(), null, 1) + '\n');
  console.log('WROTE ' + NF.KEYS.length + ' sidecars and sources.json at rig ' + rigSha.slice(0, 12)); }
module.exports = { KIT, LIBKIT, LIBS, RIG, load, sidecar, sources, shaLf, lf };
