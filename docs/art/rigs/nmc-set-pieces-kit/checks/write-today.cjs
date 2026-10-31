'use strict';
// Node 24, built-ins only. Reads the delivered scene; gameplay is always written by the kit.
const fs = require('fs'), path = require('path'), vm = require('vm'), crypto = require('crypto');
const KIT = path.resolve(__dirname, '..'), ROOT = path.resolve(KIT, '../../../..');
const SCRIPTS = ['lib/weatherSky.js', 'lib/coastalPass.js', 'nmcSetPieces.js'];
const SCENE = 'hh-nmc-wharf-return/scenes/keyscene.nmc_wharf/scene.json';
const DROP = path.resolve(ROOT, '../../hh-drops/HH-nmc-key-scenes-one-scene-reissue-3-2026-10-07/unzipped/HH-nmc-key-scenes-one-scene-reissue-3-2026-10-07');
const TARGET = path.join(ROOT, 'Assets/_Project/Art/Sprites/NineMileCreek/SetPieces/nmcSetPieces.today.json');
const ctx = vm.createContext({ console });
for (const file of SCRIPTS) vm.runInContext(fs.readFileSync(path.join(KIT, file), 'utf8'), ctx, { filename: file });
const NM = ctx.NmcSetPieces;
const IMPORT_SIZE_CAP = 4096;
const assert = (ok, message) => { if (!ok) throw new Error(message); };
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const equal = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const keys = ['officeTrailer', 'wharfCrane', 'harbourLight', 'restaurant', 'fishMarket', 'landingPad'];
const required = { officeTrailer: ['counter','vendor','door','interact'], fishMarket: ['counter','door','iceChest'], restaurant: ['door','takeout','deckDoor'], landingPad: ['seat'], wharfCrane: ['hook','tip','loadBase','operator','column','state'] };
function writeToday(sceneFile = path.join(DROP, SCENE)) {
  assert(NM.VERSION === '1.4.0', 'kit version changed');
  const bytes = fs.readFileSync(sceneFile), scene = JSON.parse(bytes);
  assert(scene.id === 'keyscene.nmc_wharf', 'wrong scene');
  const rows = scene.pieces.map((entry, index) => ({ entry, index })).filter(x => keys.includes(x.entry.piece));
  assert(rows.length === 8, 'expected six pieces, crane state and light colour');
  const pieces = NM.KEYS.filter(k => keys.includes(k)).map(key => {
    const entries = rows.filter(x => x.entry.piece === key), first = entries[0];
    assert(first, 'missing ' + key);
    const options = first.entry.opts, gameplay = NM.gameplay(key, options);
    for (const name of required[key] || []) assert(gameplay.anchors[name] != null, key + ': missing anchor ' + name);
    if (key === 'fishMarket') assert(gameplay.anchors.words.some(w => w.id === 'words.nmc_market_board' && w.bind === 'market.today'), 'market price board missing');
    const variants = entries.slice(1).map(({entry,index}) => {
      const other = NM.gameplay(key, entry.opts), state = other.anchors.state;
      const changed = [...new Set([...Object.keys(options), ...Object.keys(entry.opts)])].filter(k => !equal(options[k], entry.opts[k]));
      assert(state || changed.length === 1, key + ': stateless variant differs in more than one option');
      if (state) assert(equal(entry.at, first.entry.at) && entry.z === first.entry.z, key + ': state moved its ground point');
      const name = state || changed[0] + '=' + entry.opts[changed[0]];
      assert(equal(Object.keys(gameplay.anchors), Object.keys(other.anchors)), key + ': variant anchor names changed');
      for (const anchor of Object.keys(gameplay.anchors)) assert(other.anchors[anchor] != null || (state && anchor === 'loadBase'), key + ': variant lost ' + anchor);
      for (const field of ['layer','walk','mount','light']) assert(equal(gameplay[field], other[field]), key + ': shared field differs: ' + field);
      for (const field of ['words','marks','path']) assert(equal(gameplay.anchors[field], other.anchors[field]), key + ': shared anchor field differs: ' + field);
      assert(!gameplay.animated, key + ': variants with animated part');
      return { name, sceneEntry: index, options: entry.opts, gameplay: other };
    });
    return {key, id: NM.PIECES[key].id, scene: scene.id, sceneEntry:first.index, options, gameplay, variants};
  });
  const today = {schema:'hidden-harbours/nmc-set-pieces-today@1',
    note:'The six wharf pieces at the delivered scene options. The crane includes working and stowed states; the harbour light includes green and red. Gameplay blocks are the kit output, unchanged.',
    kit:{folder:'docs/art/rigs/nmc-set-pieces-kit',global:'NmcSetPieces',version:NM.VERSION,
      scripts:SCRIPTS.map(file=>({path:file,sha256Lf:sha(Buffer.from(fs.readFileSync(path.join(KIT,file),'utf8').replace(/\r\n/g,'\n')))}))},
    scenes:[{id:scene.id,file:SCENE,bytes:bytes.length,sha256:sha(bytes)}],
    bake:{sky:{time:14,cloud:0.1,wind:0.3},lit:0,outline:false,facings:8,importSizeCap:IMPORT_SIZE_CAP},leftOut:[],pieces};
  fs.mkdirSync(path.dirname(TARGET),{recursive:true});
  fs.writeFileSync(TARGET, JSON.stringify(today,null,1)+'\n');
  return today;
}
if (require.main === module) { const t = writeToday(process.argv[2]); console.log('WROTE today: ' + t.pieces.length + ' pieces, ' + t.pieces.reduce((n,p)=>n+p.variants.length,0) + ' variants'); }
module.exports = { writeToday, NM, SCRIPTS, IMPORT_SIZE_CAP };
