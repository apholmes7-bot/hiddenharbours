/* tools/_run.cjs — the Node harness every checker uses (Node 16+, no packages).
   Loads the rig into this process with vm.runInThisContext, so it registers globalThis.CharacterIso9 exactly as a browser
   <script> does; hands the checker the API and a small file helper; writes reports/<name>.json and reports/<name>.txt.
   A checker that generates a data file compares it with the committed one and says so in its report; `--write` rewrites it. */
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm'), crypto = require('crypto'), zlib = require('zlib');
const ROOT = path.resolve(__dirname, '..'), abs = (p) => path.resolve(ROOT, p);
const byName = (a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0);
function list(dir) { const out = [];
  (function rec(d) { for (const e of fs.readdirSync(abs(d || '.'), { withFileTypes: true }).sort(byName)) {
    const p = d ? d + '/' + e.name : e.name; if (e.isDirectory()) { out.push(p + '/'); rec(p); } else out.push(p); } })((dir || '').replace(/\/+$/, ''));
  return out; }
const H = {
  root: ROOT, argv: process.argv.slice(2),
  exists: (p) => fs.existsSync(abs(p)),
  text: (p) => fs.readFileSync(abs(p), 'utf8'),
  bytes: (p) => new Uint8Array(fs.readFileSync(abs(p))),
  sha256: (p) => crypto.createHash('sha256').update(fs.readFileSync(abs(p))).digest('hex'),
  sha256Bytes: (u8) => crypto.createHash('sha256').update(u8).digest('hex'),
  inflate: (u8) => new Uint8Array(zlib.inflateSync(u8)),
  list
};
function load(extra) {
  for (const f of ['Art/characterIsoRig9.js', 'Art/characterIsoRig9.poses.js'].concat(extra || []))
    vm.runInThisContext(fs.readFileSync(abs(f), 'utf8'), { filename: f });
  return globalThis.CharacterIso9;
}
function main(run, extra) {
  const C = load(extra), r = run(C, H);
  fs.mkdirSync(abs('reports'), { recursive: true });
  if (r.json) fs.writeFileSync(abs('reports/' + r.name + '.json'), JSON.stringify(r.json, null, 2) + '\n');
  if (r.text) fs.writeFileSync(abs('reports/' + r.name + '.txt'), r.text);
  if (r.data && H.argv.includes('--write'))
    for (const [p, s] of Object.entries(r.data)) { fs.mkdirSync(path.dirname(abs(p)), { recursive: true }); fs.writeFileSync(abs(p), s); }
  process.stdout.write(r.text || '');
  process.exitCode = r.ok ? 0 : 1;
}
module.exports = { main, load, H };
