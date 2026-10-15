'use strict';
const fs = require('fs'), path = require('path'), assert = require('assert');
Object.defineProperty(globalThis, 'StationLight2', { get() { throw Error('Piece lighting must stay unused'); }, configurable: true });
const { rig: R, bake: B } = require('./loadRig.cjs');
const keys = ['OstrichFern', 'MarshMarigold'];
assert.deepStrictEqual(R.SPECIES.slice(48).map(s => s.key), keys);
const cat = JSON.parse(fs.readFileSync(path.join(__dirname, 'catalog.json'), 'utf8'));
const rows = [], output = process.argv[2];
if (output) fs.mkdirSync(output, { recursive: true });
for (const key of keys) {
    const sp = cat.species.find(s => s.key === key);
    assert(sp && sp.cells.length === 48 && sp.dormant.length === 0);
    for (const stage of R.STAGE_KEYS) for (const season of R.SEASONS) for (let variant = 0; variant < 4; variant++) {
        const o = { stage, season, variant, frame: 4, wind: { w: .45, gust: .2, dir: 1 } };
        const model = B.model(key, o), cell = B.cellOf(key, o), frame = B.frame(key, o), sheet = B.sheet(key, o, 'lit');
        assert(model && cell && frame && sheet);
        assert.strictEqual(cell.w, frame.v.w); assert.strictEqual(cell.h, frame.v.h);
        assert.deepStrictEqual(sheet.cell, [cell.w, cell.h]);
        assert.strictEqual(sheet.frames, 16);
        const rgba = R.view(frame, 'lit');
        let opaque = 0; for (let i = 3; i < rgba.length; i += 4) if (rgba[i]) opaque++;
        assert(opaque > 0, key + '/' + stage + '/' + season + '/' + variant);
        const file = [key, stage, season, variant].join('-') + '.rgba';
        if (output) fs.writeFileSync(path.join(output, file), rgba);
        rows.push({ key, stage, season, variant, w: cell.w, h: cell.h, opaque, file });
    }
    R.clearCache();
}
assert.strictEqual(rows.length, 96);
assert.deepStrictEqual(JSON.parse(fs.readFileSync(path.join(__dirname, 'unresolved-species.json'), 'utf8')).species, []);
if (output) fs.writeFileSync(path.join(output, 'cells.json'), JSON.stringify(rows, null, 2) + '\n');
console.log('PASS: both added keys, 24 states, 96 cells, four call paths, zero throws; piece lighting unused');
