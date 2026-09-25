#!/usr/bin/env node
// Tree kit checks, pass 4.1 — Node 18+, no packages. Run from anywhere:
//   node checks/run.js                 every check, in TREE_CHECKS.ORDER
//   node checks/run.js snow wind       just those
// Writes checks/out/<name>.txt and prints one line per check. `sums` runs last: it verifies SHA256SUMS.txt.
'use strict';
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const KIT = path.resolve(__dirname, '..');
for (const f of ['treeIsoRig4.js', 'weatherSky.js', '_treeGameplay.js', 'treeMaps4.js', 'lib/treeIsoRig3.js', 'checks/treeChecks.js']) (0, eval)(fs.readFileSync(path.join(KIT, f), 'utf8'));
const walk = (d) => fs.readdirSync(path.join(KIT, d), { withFileTypes: true })
  .flatMap(e => e.isDirectory() ? walk(path.join(d, e.name)) : [path.join(d, e.name).split(path.sep).join('/')]);
const io = {
  read: async (f) => fs.readFileSync(path.join(KIT, f), 'utf8'),
  readBytes: async (f) => fs.readFileSync(path.join(KIT, f)),
  list: async () => walk('.'),
  sha256: async (b) => crypto.createHash('sha256').update(b).digest('hex'),
};
(async () => {
  const C = globalThis.TREE_CHECKS, names = process.argv.slice(2).length ? process.argv.slice(2) : C.ORDER;
  fs.mkdirSync(path.join(KIT, 'checks', 'out'), { recursive: true });
  let failed = 0;
  for (const n of names) {
    const r = await C.run(n, io);
    fs.writeFileSync(path.join(KIT, 'checks', 'out', n + '.txt'), C.format(r));
    failed += r.failed;
    console.log(n.padEnd(9), r.failed ? r.failed + ' FAILED' : 'passed');
  }
  process.exitCode = failed ? 1 : 0;
})().catch((e) => { console.error(e); process.exitCode = 2; });
