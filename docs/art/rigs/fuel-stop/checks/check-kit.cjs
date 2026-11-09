'use strict';
const assert = require('node:assert/strict');
const {NM, ctx} = require('./write-today.cjs');
assert.equal(NM.KEYS.length, 17);
assert.equal(new Set(NM.KEYS.map(k => NM.PIECES[k].id)).size, 17);
for (const k of NM.KEYS) {
  const a = ctx.FuelStop.stations(k, {}), b = NM.verbs(k, {});
  a.forEach((v, i) => {
    assert.deepEqual(Array.from(b[i].at), [-v.at[0], -v.at[1], v.at[2] || 0]);
    assert.deepEqual(Array.from(b[i].stand), [-v.stand[0], -v.stand[1], v.stand[2] || 0]);
    assert.equal(b[i].id, v.id);
  });
}
// The authored composition: front sidewalk and wheel stops stay south of the store.
const fs = require('node:fs');
const yaml = fs.readFileSync('Assets/_Project/Data/KeyScenes/KeyScene_nmc_fuel_stop.asset', 'utf8');
assert.match(yaml, /At: \{x: -165\.6, y: 132\.3\}/);
assert.match(yaml, /At: \{x: -164\.1, y: 130\.35\}/);
// Apply the same pi turn and dir-4 transform used by the Def placement.
const toWorld4 = (origin, local) => [origin[0] - local[0], origin[1] - local[1]];
assert.deepEqual(toWorld4([-155, 139], NM.turn([0, -6.4, 0])), [-155, 132.6]);
assert.deepEqual(toWorld4([-155, 139], NM.turn([-9.1, -8.65, 0])), [-164.1, 130.35]);
assert.equal(NM.verbs('pylon', {}).at(-1).id, 'prices_read');
assert.equal(NM.verbs('propane', {}).at(-1).id, 'buy_propane');
assert.equal(NM.verbs('iceBox', {}).at(-1).id, 'buy_ice');
console.log('PASS: 17 kinds; every station and standing point turned by pi; ruled service anchors; composition frame');
