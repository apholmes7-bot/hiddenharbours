/* tools/check-bodies.cjs — THE GOLDEN SUITE ON EVERY BODY (9.2: new). runChecks on:
     the 375 creator bodies and the 125 child bodies (youth, adult, elder, child x shape x height x weight) in the Fisher's outfit, and
     the 24 extreme bodies (youth, adult, elder x height +-2 x shape 0, 1 x weight +-2) in the outfit with the most triangles and in the
     outfit that paints the most materials.
     node tools/check-bodies.cjs [--threads N]     writes reports/bodies.{json,txt}; runs on N worker threads (default: cores - 1),
                                                     about 6 s of one core per body, 548 bodies
   A body passes when every gated check passes (budget is a report since 9.2). The report lists every body with its checks. */
'use strict';
(function () {
const MOST_TRIS = { garment:'overalls', bottom:'trousers', hat:'hood', hairStyle:'crop', beard:'long', eyeShape:'lidded' };
const MOST_COLOURS = { garment:'apron', bottom:'skirt', hat:'watchcap', hairStyle:'crop', beard:'stubble', eyeShape:'lidded' };
function jobsOf(C) { const O = C.OPTIONS, J = [];
  for (const age of ['youth','adult','elder','child']) for (const shape of O.shape) for (const height of O.height) for (const weight of O.weight)
    J.push({ id:[age, 'h' + height, 's' + shape, 'w' + weight].join(' '), set:age === 'child' ? 'child' : 'creator', outfit:'fisher', body:{ age, shape, height, weight } });
  for (const [name, o] of [['most triangles', MOST_TRIS], ['most colours', MOST_COLOURS]])
    for (const age of ['youth','adult','elder']) for (const height of [-2, 2]) for (const shape of [0, 1]) for (const weight of [-2, 2])
      J.push({ id:[age, 'h' + height, 's' + shape, 'w' + weight].join(' ') + ' · ' + name, set:'extreme', outfit:name, body:Object.assign({ age, shape, height, weight }, o) });
  return J; }
function work(C, j) { const b = Object.assign({}, C.BUILDS.fisher, { preset:null, label:'body' }, j.body), B = C.buildOf(b), t0 = Date.now(), rows = C.runChecks(B.key);
  return { key:B.key, tris:C.sizes(B.key).tris, ms:Date.now() - t0, checks:rows.map((r) => { const o = { id:r.id, pass:!!r.pass, gate:r.gate !== false, value:r.value }; if (!r.pass) o.detail = r.detail; return o; }) }; }
function run(C, H) {
  if (!C.runChecks) throw new Error('check-bodies: load Art/characterIsoRig9.checks.js');
  const rig = H.sha256('Art/characterIsoRig9.js'), poses = H.sha256('Art/characterIsoRig9.poses.js'), problems = [], J = jobsOf(C);
  const res = J.map((j) => H.cache ? H.cache('body:' + j.id, () => work(C, j)) : work(C, j));
  const ids = res[0].checks.map((c) => c.id), sets = {}, failing = [];
  J.forEach((j, i) => { const r = res[i], s = sets[j.set] || (sets[j.set] = { bodies:0, passed:0, byCheck:{} }); s.bodies++;
    const bad = r.checks.filter((c) => c.gate && !c.pass); if (!bad.length) s.passed++; else failing.push(j.id + ': ' + bad.map((c) => c.id + ' ' + c.value).join(', '));
    for (const c of r.checks) { const e = s.byCheck[c.id] || (s.byCheck[c.id] = { pass:0, of:0 }); e.of++; if (c.pass) e.pass++; } });
  if (failing.length) problems.push(failing.length + ' bodies fail a gated check: ' + failing.slice(0, 6).join(' | '));
  const tris = J.map((j, i) => ({ id:j.id, tris:res[i].tris })).sort((a, b) => b.tris - a.tris);
  const json = { tool:'check-bodies', rig, poses, ok:!problems.length, problems, checks:ids, outfits:{ fisher:C.BUILDS.fisher, 'most triangles':MOST_TRIS, 'most colours':MOST_COLOURS }, sets,
    worstTris:tris[0], bodies:J.map((j, i) => ({ id:j.id, set:j.set, outfit:j.outfit, key:res[i].key, tris:res[i].tris, passed:res[i].checks.filter((c) => c.gate && !c.pass).length === 0, checks:res[i].checks })) };
  const pad = (s, n) => (String(s) + '                                        ').slice(0, n), L = ['check-bodies — runChecks on every body', 'rig ' + rig + '  poses ' + poses, ''];
  for (const [k, s] of Object.entries(sets)) L.push(pad(k, 10) + s.passed + ' / ' + s.bodies + ' bodies pass every gated check   ' + Object.entries(s.byCheck).filter(([, e]) => e.pass < e.of).map(([id, e]) => id + ' ' + e.pass + '/' + e.of).join('  '));
  L.push('', 'Most triangles: ' + tris[0].tris + ' (' + tris[0].id + ').', '', 'Per body (' + ids.join(', ') + '):');
  J.forEach((j, i) => { const r = res[i]; L.push(pad(j.id, 44) + pad(r.tris + ' tris', 10) + (r.checks.filter((c) => c.gate && !c.pass).length ? 'FAIL ' : 'pass ') + r.checks.map((c) => c.id + '=' + c.value + (c.pass ? '' : '!')).join(' · ')); });
  L.push('', problems.length ? 'PROBLEMS:\n  ' + problems.join('\n  ') : 'OK — no problems.', '');
  return { name:'bodies', ok:!problems.length, json, text:L.join('\n') };
}
const ART = ['Art/characterIsoRig9.js', 'Art/characterIsoRig9.poses.js', 'Art/characterIsoRig9.checks.js'];
if (typeof module !== 'undefined' && module.exports) { module.exports = run;
  const wt = require('worker_threads'), path = require('path'), fs = require('fs'), vm = require('vm');
  if (!wt.isMainThread && wt.workerData && wt.workerData.checkBodies) { for (const f of ART) vm.runInThisContext(fs.readFileSync(path.resolve(__dirname, '..', f), 'utf8'), { filename:f });
    const C = globalThis.CharacterIso9; wt.parentPort.on('message', (j) => { if (!j) process.exit(0); wt.parentPort.postMessage({ id:j.id, r:work(C, j) }); }); }
  else if (require.main === module) { const R = require('./_run.cjs'), C = R.load(['Art/characterIsoRig9.checks.js']), J = jobsOf(C), cache = {}, argv = process.argv.slice(2);
    const ti = argv.indexOf('--threads'), n = Math.max(1, ti >= 0 ? +argv[ti + 1] : Math.max(1, require('os').cpus().length - 1)); let next = 0, done = 0;
    const finish = () => { const H = Object.assign({}, R.H, { cache:(k, fn) => (k in cache ? cache[k] : (cache[k] = fn())) }); R.main(() => run(C, H), ['Art/characterIsoRig9.checks.js']); };
    for (let w = 0; w < n; w++) { const wk = new wt.Worker(__filename, { workerData:{ checkBodies:true } });
      const feed = () => { if (next < J.length) wk.postMessage(J[next++]); else wk.postMessage(null); };
      wk.on('message', (m) => { cache['body:' + m.id] = m.r; done++; if (done % 50 === 0) process.stderr.write('check-bodies: ' + done + ' / ' + J.length + '\n'); if (done === J.length) finish(); feed(); }); feed(); } } }
else { const T = (globalThis.HHTools = globalThis.HHTools || {}); T.bodies = run; T.bodiesJobs = jobsOf; T.bodiesWork = work; }
})();
