'use strict';
const fs = require('fs'), vm = require('vm'), path = require('path');
const dir = __dirname;
vm.runInThisContext(fs.readFileSync(path.join(dir, 'plantIsoRig5.js'), 'utf8'));
globalThis.PlantDormant5 = JSON.parse(fs.readFileSync(path.join(dir, 'dormant-states.json'), 'utf8'));
vm.runInThisContext(fs.readFileSync(path.join(dir, 'bakePlants.js'), 'utf8'));
let cells = 0;
for (const s of PlantDormant5.states) for (const variant of s.variants) {
    if (s.state !== 'DORMANT' || !PlantRig5.byKey[s.species]) throw Error('Invalid dormant data');
    const o = { stage: s.stage, season: s.season, variant, frame: 0, wind: { w: 0, gust: 0, dir: 1 } };
    let threw = false;
    try { PlantRig5.frame(s.species, o); } catch (e) {
        if (!(e instanceof RangeError)) throw e;
        threw = true;
    }
    if (!threw) throw Error('Rig now draws dormant cell: ' + [s.species, s.stage, s.season, variant].join('/'));
    if (PlantBake5.frame(s.species, o) !== null) throw Error('Dormant cell must draw nothing');
    cells++;
}
if (cells !== 64 || PlantDormant5.states.length !== 16) throw Error('Dormant count changed');
const missing = JSON.parse(fs.readFileSync(path.join(dir, 'unresolved-species.json'), 'utf8'));
for (const s of missing.species) {
    if (PlantRig5.byKey[s.key]) throw Error('Unresolved key now has art: ' + s.key);
    let threw = false;
    try { PlantBake5.frame(s.key, { stage: 'mature', season: 'summer', variant: 0 }); } catch (e) { threw = true; }
    if (!threw) throw Error('Unknown key must not use fallback');
}
console.log('PASS: 16 dormant states, 64 variants; two unresolved keys rejected');
