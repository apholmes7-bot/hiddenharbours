/* tools/check-look.cjs — LOOK AT: the gaze states and the head turn (9.2: new). Runs the golden suite's gaze and look checks over a wide set.
     node tools/check-look.cjs            writes reports/look.{json,txt} (~1 min)
   gaze  the 168 bodies (child, youth, adult, elder x 7 heads x shape 0, 0.5, 1 x weight -2, +2) x every eye shape (504 builds), idle f0 at
         16 facings: eyes.left and eyes.right each move the pupil (the ink and iris pixels) against eyes.open, and differ from each other,
         at every facing where the eyes show — at least 1 px at 32 px/m, or the pupil hidden where the one eye in profile turns away
   turn  the Fisher's body x every hat x every hair style (104 builds), and every garment (13, with the hood), idle f0 and walk f2 at 16
         facings, the head turned to yaw +-60, pitch -15 / +20 and the four corners: no scalp in the hair region, no neck through the hood,
         no see-through at the neck, collar or hood that the unturned pose does not have; and the aim lands (lookAt() then the turn) */
'use strict';
(function () {
function jobsOf(C) { const O = C.OPTIONS, J = [];
  for (const age of ['child','youth','adult','elder']) for (const head of O.head) for (const shape of [0, 0.5, 1]) for (const weight of [-2, 2]) for (const eyeShape of O.eyeShape) J.push({ id:'gaze:' + [age, head, shape, weight, eyeShape].join('/'), check:'gaze', build:{ age, head, shape, weight, eyeShape, height:0 } });
  for (const hat of O.hat) for (const hairStyle of O.hairStyle) J.push({ id:'turn:' + hat + '/' + hairStyle, check:'look', build:{ hat, hairStyle } });
  for (const garment of O.garment) J.push({ id:'turn:' + garment + '/hood', check:'look', build:{ garment, hat:'hood', hairStyle:'long' } });
  return J; }
function work(C, j) { const B = C.buildOf(Object.assign({}, C.BUILDS.fisher, { preset:null, label:'look' }, j.build)), ch = C.CHECKS.find((c) => c.id === j.check), it = ch.run(B); let r = it.next(); while (!r.done) r = it.next();
  const v = r.value; return { pass:v.pass, value:v.value, detail:v.detail, bad:(v.rows || []).filter((x) => !x.ok).map((x) => x.label + ' ' + x.value) }; }
function run(C, H) {
  if (!C.CHECKS) throw new Error('check-look: load Art/characterIsoRig9.checks.js');
  const rig = H.sha256('Art/characterIsoRig9.js'), poses = H.sha256('Art/characterIsoRig9.poses.js'), problems = [];
  const J = jobsOf(C), res = J.map((j) => H.cache ? H.cache('look:' + j.id, () => work(C, j)) : work(C, j));
  const g = { builds:0, fail:[] }, t = { builds:0, fail:[], aim:0 };
  J.forEach((j, i) => { const r = res[i], s = j.check === 'gaze' ? g : t; s.builds++; if (!r.pass) s.fail.push(j.id + ': ' + (r.bad.length ? r.bad.slice(0, 3).join(', ') : r.value));
    if (j.check === 'look') { const m = /within ([0-9.]+)°/.exec(r.detail); if (m) t.aim = Math.max(t.aim, +m[1]); } });
  if (g.fail.length) problems.push('gaze: ' + g.fail.length + ' builds: ' + g.fail.slice(0, 5).join(' | '));
  if (t.fail.length) problems.push('turn: ' + t.fail.length + ' builds: ' + t.fail.slice(0, 5).join(' | '));
  const json = { tool:'check-look', rig, poses, ok:!problems.length, problems, look:C.lookContract(), gaze:{ builds:g.builds, failing:g.fail }, turn:{ builds:t.builds, failing:t.fail, aimWithin_deg:t.aim }, rows:J.map((j, i) => ({ id:j.id, pass:res[i].pass, value:res[i].value })) };
  const LK = C.LOOK, L = ['check-look — the gaze states and the head turn', 'rig ' + rig + '  poses ' + poses, '',
    'Controls: bones ' + LK.bones.join(' + ') + ' (split ' + LK.split.neck + ' / ' + LK.split.head + '), yaw ' + LK.yaw.join('..') + ' deg, pitch ' + LK.pitch.join('..') + ' deg (+ is down); eyes.left / eyes.right in place of eyes.open.',
    'gaze: ' + g.builds + ' builds (168 bodies x 3 eye shapes), 16 facings: ' + (g.builds - g.fail.length) + ' move the pupils both ways at every facing that shows the eyes.',
    'turn: ' + t.builds + ' builds (every hat x every hair style on the Fisher, every garment with the hood), 16 facings x 8 limits x idle and walk: ' + (t.builds - t.fail.length) + ' open no gap. Aim within ' + t.aim.toFixed(2) + ' deg for targets inside the limits.',
    '', problems.length ? 'PROBLEMS:\n  ' + problems.join('\n  ') : 'OK — no problems.', ''];
  return { name:'look', ok:!problems.length, json, text:L.join('\n') };
}
if (typeof module !== 'undefined' && module.exports) { module.exports = run; if (require.main === module) require('./_run.cjs').main(run, ['Art/characterIsoRig9.checks.js']); }
else { const T = (globalThis.HHTools = globalThis.HHTools || {}); T.look = run; T.lookJobs = jobsOf; T.lookWork = work; }
})();
