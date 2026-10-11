'use strict';
require('./loadRig.cjs');
const methods = ['frame', 'sheet', 'model', 'cellOf'];
let cells = 0;
for (const s of PlantDormant5.states) for (const variant of s.variants) {
    if (s.state !== 'DORMANT' || !PlantRig5.byKey[s.species]) throw Error('Invalid dormant data');
    const o = { stage: s.stage, season: s.season, variant, frame: 0, wind: { w: 0, gust: 0, dir: 1 } };
    for (const method of methods) {
        let threw = false;
        try { PlantRig5[method](s.species, o); } catch (e) {
            if (!(e instanceof RangeError)) throw e;
            threw = true;
        }
        if (!threw) throw Error('Rig now draws dormant cell: ' + [method, s.species, s.stage, s.season, variant].join('/'));
        const original = PlantRig5[method];
        PlantRig5[method] = () => { throw Error('Dormant guard entered rig'); };
        try {
            if (PlantBake5[method](s.species, o) !== null) throw Error('Dormant cell must draw nothing');
        } finally { PlantRig5[method] = original; }
    }
    cells++;
}
if (cells !== 64 || PlantDormant5.states.length !== 16) throw Error('Dormant count changed');
for (const method of methods) {
    let threw = false;
    try { PlantBake5[method]('MissingPlantKey', { stage: 'mature', season: 'summer', variant: 0 }); } catch (e) { threw = true; }
    if (!threw) throw Error('Unknown key must not use fallback');
}
console.log('PASS: 16 dormant states, 64 cells, four call paths guarded; unknown key rejected on every path');
