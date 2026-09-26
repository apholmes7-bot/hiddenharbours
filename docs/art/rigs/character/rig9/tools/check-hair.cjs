/* tools/check-hair.cjs — THE HAIR REGION: no skull skin where the hair is, for every hair style, head, age and body, with and without
   every hat (9.2: new).
     node tools/check-hair.cjs            writes reports/hair.{json,txt} (~2 min)
   The skin that is meant to show: the face and the forehead below the style's front hairline, the temples in front of the ears below
   the temple line, the ears, and the nape below the style's nape line (CharacterIso9.hairline(build) gives the lines; the rig's THE HAIR
   REGION explains them). Everything else of the skull is the hair region, and a skull pixel there must not be skin.
   1  mesh: every head geometry (age class x shape x weight x head, 525) x every style: no skull face painted skin inside the region
   2  renders, idle f0, 16 facings: the 168 bodies you rendered (child, youth, adult, elder x 7 heads x shape 0, 0.5, 1 x weight -2, +2)
      x every style, no hat; and 28 bodies (4 ages x 7 heads, shape and weight at their extremes) x every style x every hat
   3  renders, 16 facings, every style x every hat, on two bodies (adult heart s0 w+2, child wide s1 w-2): walk f0-f7, the most-tipped
      frame of every clip that tips the head more than 5 deg from idle, and idle f0 with the head turned to each look-at limit
   A pixel counts when it is drawn by a skull face in a skin material and its point on the head lies inside the region by more than
   1.5 mm (the golden hair check's classifier, CharacterIso9.checkHelpers). */
'use strict';
(function () {
const STYLES = ['crop','mop','bob','long','bun','ponytail','buzz','bald'];
function jobsOf(C) { const O = C.OPTIONS, J = [];
  for (const age of ['child','youth','adult','elder']) for (const head of O.head) for (const shape of [0, 0.5, 1]) for (const weight of [-2, 2]) J.push({ id:'b168:' + [age, head, shape, weight].join('/'), tier:2, body:{ age, head, shape, weight }, hats:['none'] });
  ['child','youth','adult','elder'].forEach((age, a) => O.head.forEach((head, h) => J.push({ id:'b28:' + age + '/' + head, tier:2, body:{ age, head, shape:(a + h) % 2 ? 0 : 1, weight:(a + h) % 2 ? 2 : -2 }, hats:O.hat.filter((x) => x !== 'none') })));
  for (const body of [{ age:'adult', head:'heart', shape:0, weight:2 }, { age:'child', head:'wide', shape:1, weight:-2 }]) for (const hat of O.hat) J.push({ id:'tip:' + body.age + '/' + body.head + '/' + hat, tier:3, body, hats:[hat] });
  return J; }
function work(C, j) { const H8 = C.checkHelpers, O = C.OPTIONS, out = { hair:0, at:null, renders:0, most:{ face:0, temple:0, ear:0, nape:0, scalp:0 } };
  const base = Object.assign({}, C.BUILDS.fisher, { preset:null, label:'hair', height:0, beard:'none' }, j.body);
  const tally = (R, B, HL, cache, lab) => { const c = H8.skullCount(R, B, HL, cache); out.renders++; if (c.hair > out.hair) { out.hair = c.hair; out.at = lab; } for (const z of Object.keys(out.most)) out.most[z] = Math.max(out.most[z], c[z]); };
  for (const hat of j.hats) for (const hs of STYLES) { const B = C.buildOf(Object.assign({}, base, { hat, hairStyle:hs })), HL = C.hairline(B.key), cache = {}, lab = hs + '/' + hat;
    const frames = [['idle', 0, null]];
    if (j.tier === 3) { for (let f = 0; f < 8; f++) frames.push(['walk', f, null]); for (const [y, p] of H8.LIMITS) frames.push(['idle', 0, { yaw:y, pitch:p }]);
      const up = (S) => { const R = S.W[B.sk.ix.head].R; return [R[6], R[7], R[8]]; }, u0 = up(C.evalClip('idle', 0, B)), ang = (a) => Math.acos(Math.max(-1, Math.min(1, a[0] * u0[0] + a[1] * u0[1] + a[2] * u0[2]))) * 180 / Math.PI;
      for (const n of C.clipNames()) { const cd = C.clipDef(n), A = C.ANIMS[cd.anim]; let best = -1, bk = 0; for (let k = 0; k < A.frames; k++) { const a = ang(up(C.evalClip(n, C.uOf(cd.anim, k), B))); if (a > best) { best = a; bk = k; } } if (best > 5) frames.push([n, bk, null]); } }
    for (const [clip, frame, look] of frames) for (let k = 0; k < 16; k++) tally(C.render({ clip, frame, yaw:k * 22.5, build:B.key, look, keyline:false, edges:false }), B, HL, cache, lab + ' ' + clip + ' f' + frame + (look ? ' turned ' + look.yaw + '/' + look.pitch : '') + ' at ' + (k * 22.5) + ' deg'); }
  return out; }
function meshProof(C) { const H8 = C.checkHelpers, O = C.OPTIONS; let builds = 0, bad = 0, first = null;
  for (const age of ['child','youth','adult']) for (const shape of O.shape) for (const weight of O.weight) for (const head of O.head) for (const hs of STYLES) {
    const B = C.buildOf(Object.assign({}, C.BUILDS.fisher, { preset:null, label:'hair', age, shape, weight, head, hairStyle:hs, hat:'none', beard:'none' })), HL = C.hairline(B.key), nb = B.mesh.groups['eyes.open'][0]; builds++;
    for (let fi = 0; fi < nb; fi++) { const f = B.mesh.faces[fi]; if (f.part !== 'head' || !(f.mat === 'skin' || f.mat === 'skinD' || f.mat === 'stub')) continue; const side = H8.skullSide(f);
      for (const p of f.v.concat([f.v.reduce((a, q) => [a[0] + q[0] / f.v.length, a[1] + q[1] / f.v.length, a[2] + q[2] / f.v.length], [0, 0, 0])])) if (H8.strictlyHair(B, HL, side, p)) { bad++; if (!first) first = hs + ' ' + age + ' ' + head + ' s' + shape + ' w' + weight; break; } } }
  return { builds, bad, first }; }
function run(C, H) {
  if (!C.checkHelpers || !C.hairline) throw new Error('check-hair: load Art/characterIsoRig9.checks.js (the golden helpers)');
  const rig = H.sha256('Art/characterIsoRig9.js'), poses = H.sha256('Art/characterIsoRig9.poses.js'), problems = [];
  const proof = H.cache ? H.cache('hair:proof', () => meshProof(C)) : meshProof(C);
  const J = jobsOf(C), res = J.map((j) => H.cache ? H.cache('hair:' + j.id, () => work(C, j)) : work(C, j));
  let renders = 0, hair = 0, at = null; const most = { face:0, temple:0, ear:0, nape:0, scalp:0 }, tiers = {};
  J.forEach((j, i) => { const r = res[i], t = tiers[j.tier] || (tiers[j.tier] = { renders:0, hair:0 }); renders += r.renders; t.renders += r.renders; t.hair = Math.max(t.hair, r.hair);
    if (r.hair > hair) { hair = r.hair; at = j.id + ' ' + r.at; } for (const z of Object.keys(most)) most[z] = Math.max(most[z], r.most[z]); });
  if (proof.bad) problems.push(proof.bad + ' skull faces painted skin inside the hair region (first: ' + proof.first + ')');
  if (hair) problems.push('skull skin inside the hair region: ' + hair + ' px at ' + at);
  const lines = {}; for (const hs of STYLES) { const HL = C.hairline(Object.assign({}, C.BUILDS.fisher, { preset:null, hairStyle:hs })); lines[hs] = HL; }
  const json = { tool:'check-hair', rig, poses, ok:!problems.length, problems, meshProof:proof, renders, skullSkinInHair_px:hair, worstAt:at, tiers, mostMeantToShow_px:most, hairlines_fisherHead:lines,
    meantToShow:{ face:'the front and its corners below the front hairline F and the corner line C (the forehead included)', temple:'a side plane in front of the ear (y > ' + C.EAR.front + ' m) below the temple line T', ear:'the side plane between the ear\'s front and back edges, below its top E', nape:'the back, and a side plane behind the ear (y < ' + C.EAR.back + ' m), below the nape line N' } };
  const L = ['check-hair — skull skin where the hair is', 'rig ' + rig + '  poses ' + poses, '',
    '1 mesh: ' + proof.builds + ' head geometries x styles: ' + proof.bad + ' skull faces painted skin inside the hair region.',
    '2 idle f0 at 16 facings: the 168 bodies x 8 styles, no hat; 28 bodies x 8 styles x 12 hats: ' + (tiers[2] ? tiers[2].renders : 0) + ' renders, most skull skin in the hair region ' + (tiers[2] ? tiers[2].hair : 0) + ' px.',
    '3 two bodies x 8 styles x 13 hats x 16 facings, walk f0-f7, every clip that tips the head (its most-tipped frame) and the look-at limits: ' + (tiers[3] ? tiers[3].renders : 0) + ' renders, most ' + (tiers[3] ? tiers[3].hair : 0) + ' px.',
    '', 'Skull skin in the hair region, all ' + renders + ' renders: ' + hair + ' px' + (at ? ' (' + at + ')' : '') + '.',
    'The skin meant to show, most in one render: face and forehead ' + most.face + ' px, temples ' + most.temple + ', ears ' + most.ear + ', nape ' + most.nape + '. Painted scalp (the hair-coloured skull under the shell) showing: at most ' + most.scalp + ' px.',
    '', 'Hair lines (canonical head z) on the Fisher\'s head: F front, C corners, T temples, E top of the ear, N nape:'];
  for (const [hs, HL] of Object.entries(lines)) L.push('  ' + (hs + '         ').slice(0, 10) + (HL.horseshoe ? 'bald: the horseshoe at the back, ' + HL.horseshoe.join('-') : 'F ' + HL.F.toFixed(3) + '  C ' + HL.C + '  T ' + HL.T + '  E ' + HL.E.toFixed(3) + '  N ' + HL.N + '  (' + HL.mat + ')'));
  L.push('', problems.length ? 'PROBLEMS:\n  ' + problems.join('\n  ') : 'OK — no problems.', '');
  return { name:'hair', ok:!problems.length, json, text:L.join('\n') };
}
if (typeof module !== 'undefined' && module.exports) { module.exports = run; if (require.main === module) require('./_run.cjs').main(run, ['Art/characterIsoRig9.checks.js']); }
else { const T = (globalThis.HHTools = globalThis.HHTools || {}); T.hair = run; T.hairJobs = jobsOf; T.hairWork = work; T.hairProof = meshProof; }
})();
