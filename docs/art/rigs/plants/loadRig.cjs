'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm'), crypto = require('crypto');
for (const [file, sha] of [
    ['plantIsoRig5.js', '1c71a9b2eecc28b395da4ce2a932ab150c2f96ec82880774a9afc66824483833'],
    ['afPieces.js', '9f46015cd644965c6c358d5c61715746379bc69aa55edb0696ff80a1aeae5fe8']
]) {
    const bytes = fs.readFileSync(path.join(__dirname, file));
    if (crypto.createHash('sha256').update(bytes).digest('hex') !== sha) throw Error('Plant source hash mismatch: ' + file);
    vm.runInThisContext(bytes.toString('utf8'), { filename: file });
}
globalThis.PlantDormant5 = JSON.parse(fs.readFileSync(path.join(__dirname, 'dormant-states.json'), 'utf8'));
vm.runInThisContext(fs.readFileSync(path.join(__dirname, 'bakePlants.js'), 'utf8'), { filename: 'bakePlants.js' });
module.exports = { rig: globalThis.PlantRig5, bake: globalThis.PlantBake5 };
