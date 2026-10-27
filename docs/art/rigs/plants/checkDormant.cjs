'use strict';
const assert = require('assert');
require('./loadRig.cjs');
const methods = ['frame', 'sheet', 'model', 'cellOf'];
const bareKeys = ['Lupin', 'LadySlipper', 'Fireweed', 'QueenAnne', 'Goldenrod', 'BlueFlag'];
assert.strictEqual(PlantDormant5.schema, 1);
assert.deepStrictEqual(PlantDormant5.states, bareKeys.map(species => ({
    species, stage: 'seedling', season: 'winter', variants: [0, 1, 2, 3], state: 'DORMANT'
})));
let cells = 0;
for (const s of PlantDormant5.states) for (const variant of s.variants) {
    const o = { stage: s.stage, season: s.season, variant, frame: 0, wind: { w: 0, gust: 0, dir: 1 } };
    for (const method of methods) {
        const original = PlantRig5[method];
        PlantRig5[method] = () => { throw Error('Dormant guard entered rig'); };
        try {
            assert.strictEqual(PlantBake5[method](s.species, o), null, 'Dormant cell must draw nothing');
        } finally { PlantRig5[method] = original; }
    }
    cells++;
}
assert.strictEqual(cells, 24);
let empty = 0, drawn = 0;
for (const sp of PlantRig5.SPECIES) {
    for (const stage of PlantRig5.STAGE_KEYS) for (const season of PlantRig5.SEASONS)
    for (let variant = 0; variant < PlantRig5.VARIANTS; variant++) {
        const o = { stage, season, variant, frame: 0, wind: { w: 0, gust: 0, dir: 1 } };
        const label = [sp.key, stage, season, variant].join('/');
        const bare = bareKeys.includes(sp.key) && stage === 'seedling' && season === 'winter';
        const model = PlantRig5.model(sp.key, o), cell = PlantRig5.cellOf(sp.key, o), frame = PlantRig5.frame(sp.key, o);
        const flag = bare ? true : undefined;
        assert.strictEqual(model.empty, flag, label + ' model.empty');
        assert.strictEqual(cell.empty, flag, label + ' cell.empty');
        assert.strictEqual(frame.mdl.empty, flag, label + ' frame.mdl.empty');
        assert.strictEqual(frame.empty, undefined, label + ' frame has no empty flag');
        assert.strictEqual(frame.v.a.some(a => a !== 0), !bare, label + ' raster');
        if (bare) {
            assert.deepStrictEqual([frame.v.w, frame.v.h, cell.w, cell.h], [10, 6, 10, 6], label);
            empty++;
        } else drawn++;
    }
    PlantRig5.clearCache();
}
assert.strictEqual(empty, 24);
assert.strictEqual(drawn, 2376);
for (const method of methods) for (const api of [PlantRig5, PlantBake5]) {
    assert.throws(() => api[method]('MissingPlantKey', { stage: 'mature', season: 'summer', variant: 0 }),
        error => error instanceof Error && error.message.includes('MissingPlantKey'));
}
console.log('PASS: 6 dormant states, 24 cells, four call paths guarded; exactly 24 empty and 2376 drawn rig cells; unknown key named on every path');
