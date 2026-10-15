'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm'), crypto = require('crypto');
for (const [file, sha] of [
    ['plantIsoRig5.js', 'a80c485993a1a05930849298d688572864deaa873d6aa62435135bfc3ff0059b'],
    ['afPieces.js', '9f46015cd644965c6c358d5c61715746379bc69aa55edb0696ff80a1aeae5fe8']
]) {
    const bytes = fs.readFileSync(path.join(__dirname, file));
    if (crypto.createHash('sha256').update(bytes).digest('hex') !== sha) throw Error('Plant source hash mismatch: ' + file);
    vm.runInThisContext(bytes.toString('utf8'), { filename: file });
}
globalThis.PlantDormant5 = JSON.parse(fs.readFileSync(path.join(__dirname, 'dormant-states.json'), 'utf8'));
vm.runInThisContext(fs.readFileSync(path.join(__dirname, 'bakePlants.js'), 'utf8'), { filename: 'bakePlants.js' });
module.exports = { rig: globalThis.PlantRig5, bake: globalThis.PlantBake5 };
