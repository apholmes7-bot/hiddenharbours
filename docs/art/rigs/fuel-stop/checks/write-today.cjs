'use strict';
const fs = require('node:fs'), path = require('node:path'), vm = require('node:vm'), crypto = require('node:crypto');
const ROOT = path.resolve(__dirname, '../../../../..');
const KIT = path.resolve(__dirname, '..');
const scripts = ['docs/art/rigs/nmc-set-pieces-kit/lib/weatherSky.js',
  'docs/art/rigs/nmc-set-pieces-kit/lib/coastalPass.js',
  'docs/art/rigs/fuel-stop/fuelStopRig.js', 'docs/art/rigs/fuel-stop/fuelStopGameplay.js'];
const ctx = vm.createContext({console});
for (const file of scripts) vm.runInContext(fs.readFileSync(path.join(ROOT, file), 'utf8'), ctx, {filename: file});
const NM = ctx.FuelStopGameplay;
const sha = b => crypto.createHash('sha256').update(b).digest('hex');
function writeToday(target) {
  const pieces = NM.KEYS.map(key => ({key, id: NM.PIECES[key].id, scene: NM.PIECES[key].scene,
    options: {}, gameplay: NM.gameplay(key, {}), variants: []}));
  const today = {schema: 'hidden-harbours/fuel-stop-today@1',
    kit: {folder: 'docs/art/rigs/fuel-stop', global: 'FuelStopGameplay', version: NM.VERSION,
      scripts: scripts.map(p => ({path: path.relative(KIT, path.join(ROOT, p)).replaceAll('\\', '/'),
        sha256Lf: sha(Buffer.from(fs.readFileSync(path.join(ROOT, p), 'utf8').replaceAll('\r\n', '\n')))}))},
    bake: {sky: {time: 14, cloud: 0.1, wind: 0.3}, lit: 0, outline: false, facings: 8, importSizeCap: 4096},
    leftOut: ['barSign has no placement row; retained as a kind'], pieces};
  fs.mkdirSync(path.dirname(target), {recursive: true});
  fs.writeFileSync(target, JSON.stringify(today, null, 1) + '\n');
  const sidecars = path.join(KIT, 'gameplay');
  fs.mkdirSync(sidecars, {recursive: true});
  const derivedFromRigSha256 = sha(Buffer.from(fs.readFileSync(path.join(KIT, 'fuelStopRig.js'), 'utf8').replaceAll('\r\n', '\n')));
  for (const p of pieces) fs.writeFileSync(path.join(sidecars, p.key + '.gameplay.json'),
    JSON.stringify({derivedFromRigSha256, gameplay: p.gameplay}, null, 1) + '\n');
  return today;
}
if (require.main === module) {
  const target = process.argv[2];
  if (!target) throw new Error('Pass the output today.json path');
  console.log('WROTE ' + writeToday(path.resolve(target)).pieces.length + ' pieces');
}
module.exports = {writeToday, NM, ctx, ROOT, scripts};
