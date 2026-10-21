#!/usr/bin/env node
/* Hidden Harbours — wharf rig pass 2 · the check runner (Node 18+, no packages).
     node checks/run.js              run every check, rewrite checks/out/*.txt, exit 1 on any problem
     node checks/run.js fits joins   only those checks (sidecars fits gangways joins berths snow sprites sums)
     node checks/run.js --write      first regenerate gameplay/*.json and maps/sprites.json from the rig, then check
   The checks themselves are checks/wharfChecks.js, which also runs in a page or sandbox after the five rig files.           */
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm'), crypto = require('crypto');
const KIT = path.resolve(__dirname, '..'), OUT = path.join(__dirname, 'out');
for (const f of ['wharfRig2.geo.js', 'wharfRig2.fam.js', 'wharfRig2.verbs.js', 'wharfRig2.js', 'wharfRig2.kit.js', 'checks/wharfChecks.js'])
  vm.runInThisContext(fs.readFileSync(path.join(KIT, f), 'utf8'), { filename: f });
const C = globalThis.WharfChecks2, args = process.argv.slice(2), write = args.includes('--write'), only = args.filter(a => !a.startsWith('--'));
const read = (p) => { try { return fs.readFileSync(path.join(KIT, p), 'utf8'); } catch (e) { return null; } };

if (write) {
  fs.mkdirSync(path.join(KIT, 'gameplay'), { recursive: true }); fs.mkdirSync(path.join(KIT, 'maps'), { recursive: true });
  for (const k of C.keys()) fs.writeFileSync(path.join(KIT, 'gameplay', C.fileOf(k)), C.text(C.sidecar(k)));
  fs.writeFileSync(path.join(KIT, 'maps', 'sprites.json'), JSON.stringify(C.ALL.sprites({}, {}).json.table, null, 1) + '\n');
  console.log('wrote', C.keys().length, 'sidecars and maps/sprites.json');
}

fs.mkdirSync(OUT, { recursive: true });
const names = only.filter(n => n !== 'sums'), res = only.length && !names.length ? [] : C.run({ read }, names.length ? { only: names } : {});
for (const r of res) { fs.writeFileSync(path.join(OUT, r.name + '.txt'), r.text); console.log(r.name.padEnd(10), r.ok ? 'OK' : 'PROBLEMS'); }

// SHA256SUMS.txt lists every file in the kit except itself and checks/out/sums.txt (the check of the list)
let sumsOk = true;
if (!only.length || only.includes('sums')) {
  const list = [];
  (function walk(d) { for (const e of fs.readdirSync(path.join(KIT, d || '.'), { withFileTypes: true })) { const p = d ? d + '/' + e.name : e.name;
    if (e.isDirectory()) walk(p); else if (p !== 'SHA256SUMS.txt' && p !== 'checks/out/sums.txt') list.push(p); } })('');
  const want = new Map((read('SHA256SUMS.txt') || '').split('\n').filter(Boolean).map(l => [l.slice(66), l.slice(0, 64)])), L = []; let bad = 0;
  for (const p of list.sort()) { const h = crypto.createHash('sha256').update(fs.readFileSync(path.join(KIT, p))).digest('hex'), w = want.get(p);
    if (w !== h) bad++; L.push((w === h ? 'ok        ' : w ? 'DIFFERS   ' : 'UNLISTED  ') + p); }
  for (const p of want.keys()) if (!list.includes(p)) { bad++; L.push('MISSING   ' + p); }
  sumsOk = !bad;
  fs.writeFileSync(path.join(OUT, 'sums.txt'), 'check sums \u2014 ' + (sumsOk ? 'OK' : 'PROBLEMS') + '\n\n' + L.join('\n') + '\n\n' + list.length + ' files, ' + bad + ' problems\n');
  console.log('sums'.padEnd(10), sumsOk ? 'OK' : 'PROBLEMS');
}
process.exit(res.every(r => r.ok) && sumsOk ? 0 : 1);
