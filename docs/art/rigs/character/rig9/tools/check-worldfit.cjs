/* tools/check-worldfit.cjs — WORLD FIT per build: the collider and footprint, and every world fixture (rail, ladder, hauler,
   bench, cutting surface, load height, tool rest, cab seat and wheel, saddle, mount) fitted to the build. 9.2: the mount is fitted by saddle scale.
     node tools/check-worldfit.cjs          fit the ten presets and the 500 bodies of the reach space (~1-2 min), compare with the
                                            committed data/world-fit.v9.json, write reports/worldfit.{json,txt}
     node tools/check-worldfit.cjs --write  also rewrite data/world-fit.v9.json
   Uses Art/characterIsoRig9.fit.js (CharacterIso9.worldFit; the rules are in its header). The reach space is every age x shape
   x height x weight (4 x 5 x 5 x 5): those four fields set every bone length, the shoulder span and the foot, and nothing else
   moves a reach (head, hair, clothes and colours do not). The creator's bodies are the youth, adult and elder ones (375); the
   125 child bodies are fitted too, for the presets and for anyone generating children. */
'use strict';
(function () {
function run(C, H) {
  const O = C.OPTIONS, FISH = C.BUILDS.fisher, OUT = 'data/world-fit.v9.json', problems = [];
  const rigSha = H.sha256('Art/characterIsoRig9.js'), poseSha = H.sha256('Art/characterIsoRig9.poses.js'), fitSha = H.sha256('Art/characterIsoRig9.fit.js');
  if (!C.worldFit) throw new Error('check-worldfit: Art/characterIsoRig9.fit.js is not loaded');
  const fitOf = (b) => { const key = typeof b === 'string' ? b : C.buildKey(C.normBuild(b)); return H.cache ? H.cache('fit:' + key, () => C.worldFit(b)) : C.worldFit(b); };
  const presets = {}; for (const p of C.CAST) presets[p] = Object.assign({ label:C.BUILDS[p].label, body:{ age:C.BUILDS[p].age, shape:C.BUILDS[p].shape, height:C.BUILDS[p].height, weight:C.BUILDS[p].weight } }, fitOf(p));
  const rows = [];
  for (const age of O.age) for (const shape of O.shape) for (const height of O.height) for (const weight of O.weight) {
    const b = Object.assign({}, FISH, { preset:null, label:'body', age, shape, height, weight }), f = fitOf(b);
    rows.push(Object.assign({ age, shape, height, weight, creator: age !== 'child' }, f)); }
  const FX = C.FIXTURES, ids = Object.keys(FX);
  /* intervals: intersection over a set of builds */
  const inter = (A, B) => { const o = []; for (const [a0, a1] of A) for (const [b0, b1] of B) { const lo = Math.max(a0, b0), hi = Math.min(a1, b1); if (lo <= hi + 1e-9) o.push([lo, hi]); } return o; };
  const common = (list, get) => list.reduce((acc, r) => acc == null ? get(r) : inter(acc, get(r)), null) || [];
  const sets = { creator: rows.filter((r) => r.creator), children: rows.filter((r) => !r.creator), presets: Object.values(presets) };
  const shortList = (list, id) => list.filter((r) => !r.fixtures[id].fits);
  const summary = {};
  for (const id of ids) { const F = FX[id], s = { label:F.label, clips:F.clips };
    s.presetsShort = Object.entries(presets).filter(([, r]) => !r.fixtures[id].fits).map(([p, r]) => ({ preset:p, short_mm:r.fixtures[id].short_mm, at:r.fixtures[id].at }));
    s.creatorShort = shortList(sets.creator, id).length; s.childrenShort = shortList(sets.children, id).length;
    if (F.param) { s.param = F.param; s.default = F.def;
      for (const [k, list] of Object.entries(sets)) s['fitsAll_' + k] = common(list, (r) => r.fixtures[id].fit);
      if (F.sheave) for (const [k, list] of Object.entries(sets)) s['sheaveScale_fitsAll_' + k] = common(list, (r) => r.fixtures[id].sheave.scale_fit); }
    else if (id === 'wheel') { for (const [k, list] of Object.entries(sets)) { s['seatZ_fitsAll_' + k] = common(list, (r) => r.fixtures[id].seatZ_fit);
        s['wheelMoved_' + k] = list.filter((r) => !r.fixtures[id].wheel.default).length; } }
    else if (id === 'saddle') { for (const [k, list] of Object.entries(sets)) for (const q of ['pegZ','gripY','scale']) s[q + '_fitsAll_' + k] = common(list, (r) => r.fixtures[id][q + '_fit']); }
    else if (id === 'mount') { for (const [k, list] of Object.entries(sets)) { s['scale_fitsAll_' + k] = common(list, (r) => r.fixtures[id].scale_fit); s['saddleFit_' + k] = list.filter((r) => r.fixtures[id].saddleFit.fits).length + ' / ' + list.length; const v = list.map((r) => r.fixtures[id].short_mm); s['short_mm_' + k] = [Math.min(...v), Math.max(...v)]; } }
    else { for (const [k, list] of Object.entries(sets)) { const v = list.map((r) => r.fixtures[id].short_mm); s['short_mm_' + k] = [Math.min(...v), Math.max(...v)]; } }
    summary[id] = s; }
  const colR = (list) => { const r = list.map((x) => x.collider.collision_radius_m), fx = list.map((x) => x.collider.footprint_m[0]), fy = list.map((x) => x.collider.footprint_m[1]);
    return { collision_radius_m:[Math.min(...r), Math.max(...r)], footprint_x_m:[Math.min(...fx), Math.max(...fx)], footprint_y_m:[Math.min(...fy), Math.max(...fy)] }; };
  summary.collider = { presets:colR(sets.presets), creator:colR(sets.creator), children:colR(sets.children) };
  if (presets.fisher.collider.collision_radius_m !== 0.2 || presets.fisher.collider.footprint_m[0] !== 0.4 || presets.fisher.collider.footprint_m[1] !== 0.36) problems.push('the Fisher does not reproduce 0.20 / 0.40 x 0.36');
  const fixtures = {}; for (const [id, F] of Object.entries(FX)) { const o = { label:F.label, clips:F.clips }; for (const k of ['param','params','def','range','step','also','named','reportOnly']) if (F[k] != null) o[k === 'def' ? 'default' : k] = F[k]; fixtures[id] = o; }
  const data = { schema:'hidden-harbours/character-worldfit@1', rig:'characterIsoRig9.js', exportSymbol:'CharacterIso9', derivedFromRigSha256:rigSha, posesDerivedFromRigSha256:poseSha,
    fitModule:'Art/characterIsoRig9.fit.js', fitModuleSha256:fitSha, revision:C.revision,
    authoring:'Generated by tools/check-worldfit.cjs with CharacterIso9.worldFit(). Do not hand-edit: node tools/check-worldfit.cjs --write',
    units:'metres; shortfalls in mm; intervals [lo, hi] in metres, rounded to the mm', gate_m:C.FIT_GATE,
    collider:{ collision_radius_m:'shoulder_R bind.pos x (the shoulder joint\'s distance from the centreline)', footprint_m:'[2 x collision_radius_m, 2 x 0.180 m x footK] (x across, y fore and aft)',
      fisher:'0.20 and 0.40 x 0.36, the constants in every committed sidecar', measured:'bind mesh half-widths [x, y]: the whole mesh, and the body without hair, hat, face marks, nose, ears and beard' },
    fixtures, presets,
    space:{ fields:['age','shape','height','weight'], others:'the Fisher\'s (they move no reach)', creator:'age youth, adult or elder (the creator never makes a child)', rows }, summary };
  const text = JSON.stringify(data) + '\n', committed = H.exists(OUT) ? H.text(OUT) : null, same = committed === text;
  if (!problems.length && !same) problems.push(OUT + (committed == null ? ' is missing' : ' differs from the regenerated file'));

  /* ---- the report ---- */
  const L = [], pad = (s, n) => (String(s) + '                              ').slice(0, n), fmtI = (a) => a && a.length ? a.map(([x, y]) => x.toFixed(3) + '–' + y.toFixed(3)).join(', ') : 'none';
  const sgn = (v) => (v > 0 ? '+' : '') + v;
  const compact = (list) => { const out = [];
    for (const age of O.age) { const A = list.filter((r) => r.age === age); if (!A.length) continue; const per = [];
      if (A.length === 125) { out.push(age + ' all'); continue; }
      for (const h of O.height) { const Hh = A.filter((r) => r.height === h); if (!Hh.length) continue; if (Hh.length === 25) { per.push('h' + sgn(h) + ' all'); continue; }
        const sh = []; for (const s of O.shape) { const S = Hh.filter((r) => r.shape === s); if (!S.length) continue; sh.push('s' + s + (S.length === 5 ? '' : ' w' + S.map((r) => sgn(r.weight)).join(','))); }
        per.push('h' + sgn(h) + ' ' + sh.join('; ')); }
      out.push(age + ' (' + A.length + '/125): ' + per.join(' | ')); }
    return out; };
  L.push('check-worldfit — collider, footprint and world fixtures, fitted per build', 'rig ' + rigSha, 'poses ' + poseSha + '  fit module ' + fitSha, '');
  L.push('A build fits a fixture when every limb in every frame of its clips makes its target to within 5 mm.', 'Bodies: 10 presets + 500 (age x shape x height x weight); creator = the 375 youth, adult and elder bodies.', '');
  L.push('COLLIDER  collision_radius_m = shoulder joint half-span; footprint_m = [2 x that, 2 x 0.180 x footK]  (Fisher 0.20, 0.40 x 0.36)');
  L.push('  preset      radius  footprint      measured half-width x/y (whole · body)');
  for (const [p, r] of Object.entries(presets)) { const c = r.collider; L.push('  ' + pad(p, 12) + pad(c.collision_radius_m.toFixed(3), 8) + pad(c.footprint_m.map((x) => x.toFixed(3)).join(' x '), 15) + c.measured.bind_half_width_m.join(' / ') + ' · ' + c.measured.body_half_width_m.join(' / ')); }
  const cs = summary.collider; L.push('  creator bodies: radius ' + cs.creator.collision_radius_m.join('–') + ', footprint ' + cs.creator.footprint_x_m.join('–') + ' x ' + cs.creator.footprint_y_m.join('–') + '; children: radius ' + cs.children.collision_radius_m.join('–'), '');
  for (const id of ids) { const F = FX[id], s = summary[id];
    L.push(id.toUpperCase() + ' — ' + F.label + '  [' + F.clips.join(', ') + ']');
    if (F.param) L.push('  ' + F.param + ' default ' + F.def + ', range ' + F.range.join('–'));
    L.push('  presets short at the default: ' + (s.presetsShort.length ? s.presetsShort.map((x) => x.preset + ' ' + x.short_mm + ' mm').join(', ') : 'none'));
    L.push('  creator bodies short: ' + s.creatorShort + ' / 375; child bodies short: ' + s.childrenShort + ' / 125');
    const cr = shortList(sets.creator, id); if (cr.length && cr.length < 375) for (const line of compact(cr)) L.push('    ' + line);
    if (F.param) { L.push('  fits every creator body: ' + fmtI(s.fitsAll_creator) + '; every preset: ' + fmtI(s.fitsAll_presets) + '; every child: ' + fmtI(s.fitsAll_children));
      if (F.sheave) L.push('  sheave offset scale (toward the body) that fits at workZ ' + F.def + ': every creator body ' + fmtI(s.sheaveScale_fitsAll_creator) + '; every preset ' + fmtI(s.sheaveScale_fitsAll_presets));
      L.push('  per preset (' + F.param + ' that fits' + (F.sheave ? '; sheave scale that fits at ' + F.def + ', or at the closest height' : '') + '):'); for (const [p, r] of Object.entries(presets)) { const x = r.fixtures[id], sv = x.sheave;
        const shv = !sv ? '' : '   sheave ' + (sv.scale_fit.length ? fmtI(sv.scale_fit) : 'none at ' + sv.workZ + '; at ' + sv.closest.workZ + ': ' + (sv.closest.scale_fit.length ? fmtI(sv.closest.scale_fit) : 'none (best ' + sv.closest.best.short_mm + ' mm at ' + sv.closest.best.at + ')'));
        L.push('    ' + pad(p, 10) + pad((x.fits ? 'fits ' : 'short ' + x.short_mm + ' mm') , 14) + (x.fit.length ? fmtI(x.fit) : 'nothing in range (best ' + x.best.short_mm + ' mm at ' + x.best.at + ')') + shv); } }
    else if (id === 'wheel') { L.push('  seat heights where the feet reach, every creator body: ' + fmtI(s.seatZ_fitsAll_creator) + '; every preset: ' + fmtI(s.seatZ_fitsAll_presets));
      L.push('  per preset (feet / hands mm at the default; seat that fits the feet; wheel at that seat):');
      for (const [p, r] of Object.entries(presets)) { const x = r.fixtures[id]; L.push('    ' + pad(p, 10) + pad(x.feet_mm + ' / ' + x.hands_mm, 11) + pad(x.seatZ_fit.length ? fmtI(x.seatZ_fit) : 'none (best ' + x.seatZ_best.short_mm + ' mm at ' + x.seatZ_best.at + ')', 30) + 'wheel ' + (x.wheel.default ? 'default' : x.wheel.wheelZ + ' / ' + x.wheel.wheelY + (x.wheel.fits ? '' : ' (short ' + x.wheel.short_mm + ' mm)'))); } }
    else if (id === 'saddle') { L.push('  every creator body: pegs z ' + fmtI(s.pegZ_fitsAll_creator) + ', grips y ' + fmtI(s.gripY_fitsAll_creator) + ', whole-saddle scale ' + fmtI(s.scale_fitsAll_creator));
      L.push('  per preset (feet / hands mm at the default; pegs z; grips y; scale):');
      for (const [p, r] of Object.entries(presets)) { const x = r.fixtures[id]; L.push('    ' + pad(p, 10) + pad(x.feet_mm + ' / ' + x.hands_mm, 11) + pad(fmtI(x.pegZ_fit), 16) + pad(fmtI(x.gripY_fit), 18) + fmtI(x.scale_fit)); } }
    else { L.push('  shortfall, creator bodies: ' + s.short_mm_creator.join('–') + ' mm; presets: ' + s.short_mm_presets.join('–') + ' mm; at the default saddle (its seat is out of reach from the ground); fitted by saddle scale: every creator body ' + fmtI(s.scale_fitsAll_creator) + ', every preset ' + fmtI(s.scale_fitsAll_presets) + ', every child ' + fmtI(s.scale_fitsAll_children) + '; at CONTRACT.saddleFit (' + C.CONTRACT.saddleFit.scale + '): creator ' + s.saddleFit_creator + ', children ' + s.saddleFit_children);
      for (const [p, r] of Object.entries(presets)) L.push('    ' + pad(p, 10) + Object.entries(r.fixtures[id].clips).map(([n, v]) => n + ' ' + v).join(' · ')); }
    L.push(''); }
  L.push(OUT + ': ' + (same ? 'regenerated, byte-identical to the committed file' : committed == null ? 'MISSING (run with --write)' : 'DIFFERS from the regenerated file'));
  L.push(problems.length ? 'PROBLEMS:\n  ' + problems.join('\n  ') : 'OK — no problems.', '');
  const json = { tool:'check-worldfit', rig:rigSha, poses:poseSha, fitModule:fitSha, ok:problems.length === 0, problems, dataFile:OUT, dataIdentical:same, bodies:rows.length, summary };
  return { name:'worldfit', ok:json.ok, json, text:L.join('\n'), data:{ [OUT]:text } };
}
if (typeof module !== 'undefined' && module.exports) { module.exports = run; if (require.main === module) require('./_run.cjs').main(run, ['Art/characterIsoRig9.fit.js']); }
else (globalThis.HHTools = globalThis.HHTools || {}).worldfit = run;
})();
