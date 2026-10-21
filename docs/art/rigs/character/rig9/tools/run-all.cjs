/* tools/run-all.cjs — every checker in this folder, one after another, each in its own Node process (Node 16+, no packages).
     node tools/run-all.cjs [rig7-kit] [--write] [--bodies]
   rig7-kit goes to compare-rig7 only (default ../skinned-character-kit); --write goes to every checker that generates a file.
   Each checker writes reports/<name>.{json,txt}. Exit code 0 only when every checker ends "OK — no problems."
   The golden suite is separate: load Art/characterIsoRig9.checks.js after the rig and the pose library and call
   CharacterIso9.runChecks(preset), or press RUN CAST in Character v9.dc.html. golden-report.json is its run. */
'use strict';
const { spawnSync } = require('child_process'), path = require('path');
const TOOLS = ['export-builds', 'check-options', 'check-colours', 'check-numbers', 'check-shading', 'check-worldfit', 'check-renders', 'compare-rig7', 'check-hair', 'check-look']
  .concat(process.argv.includes('--bodies') ? ['check-bodies'] : []);   // check-bodies: 548 bodies, ~6 s of one core each, on worker threads
const args = process.argv.slice(2).filter((a) => a !== '--bodies'), flags = args.filter((a) => a.startsWith('--')), failed = [];
for (const t of TOOLS) {
  process.stdout.write('\n=== ' + t + ' ===\n');
  const r = spawnSync(process.execPath, [path.join(__dirname, t + '.cjs')].concat(t === 'compare-rig7' ? args : flags), { stdio:'inherit' });
  if (r.status !== 0) failed.push(t);
}
process.stdout.write('\n' + (failed.length ? 'FAILED: ' + failed.join(', ') : 'all ' + TOOLS.length + ' checkers OK') + '\n');
process.exitCode = failed.length ? 1 : 0;
