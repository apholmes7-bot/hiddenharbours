/* tools/check-numbers.cjs — the numbers per preset: painted materials, distinct ramps, vertices, bones, and the animations
   each build covers.
     node tools/check-numbers.cjs      writes reports/numbers.{json,txt}
   Animation families (the game's list) and the clips that cover them:
     walk  walk (+ walk+buckets, walk+tray, walk+pot, helm_walk, oars_row)     run   run (+ run+buckets, run+tray)
     dig   dig                                                                  fish  hold, cast, castBack, castRelease, bite, strike, reel, land
     swim  swim, tread                                                          sleep sleep
     sit / drive  drive: the only seated clip (pelvis on the cab seat, hands on the wheel, feet on the floor). There is no
                  stand-alone sit; "sit" is drive.
     ride  astride (seated on the saddle), astrideStand (standing on the pegs)
     mount mountUp, mountDown (saddle), mountCab, mountCabDown (bench seat)
   "Reach" is the worst limb shortfall over the family's clips at the default world contracts, in mm (ride and mount also at
   CONTRACT.saddleFit, where the golden suite gates them); a build makes its
   targets when it is <= 5 mm (the golden suite's contact gate). Where it is larger the clip clamps to reach and the sidecar's
   pins say so (ikShort). The fits per fixture are reports/worldfit.txt. */
'use strict';
(function () {
function run(C, H) {
  const rigSha = H.sha256('Art/characterIsoRig9.js'), poseSha = H.sha256('Art/characterIsoRig9.poses.js'), problems = [];
  const FAM = { walk:['walk','walk+buckets','walk+tray','walk+pot','helm_walk','oars_row'], run:['run','run+buckets','run+tray'], dig:['dig'],
    fish:['hold','cast','castBack','castRelease','bite','strike','reel','land'], swim:['swim','tread'], sleep:['sleep'], 'sit/drive':['drive'],
    ride:['astride','astrideStand'], mount:['mountUp','mountDown','mountCab','mountCabDown'] };
  const names = C.clipNames();
  for (const list of Object.values(FAM)) for (const n of list) if (names.indexOf(n) < 0) problems.push('no clip ' + n);
  const FIT = C.saddleOpts(C.CONTRACT.saddleFit.scale, C.CONTRACT.saddleFit.reachX);
  const shortOf = (B, clips, opts) => { let m = 0, at = null;
    for (const n of clips) { const cd = C.clipDef(n), A = C.ANIMS[cd.anim];
      for (let k = 0; k < A.frames; k++) { const S = C.evalClip(n, C.uOf(cd.anim, k), B, opts); for (const l of ['footL','footR','handL','handR']) { const v = S.err[l] || 0; if (v > m) { m = v; at = n + ' f' + k + ' ' + l; } } } }
    return { mm:Math.round(m * 1000), at }; };
  const presets = {};
  for (const p of C.CAST) { const B = C.buildOf(p), F = B.mesh.faces, sz = C.sizes(p), sh = C.shadingContract(p).materials;
    const mats = {}; for (const f of F) mats[f.mat] = 1; const matList = Object.keys(mats).sort();
    const rampSet = {}, fixed = []; for (const m of matList) { if (sh[m].fixed) fixed.push(m); else rampSet[JSON.stringify(sh[m].ramp)] = 1; }
    const pos = {}; for (const f of F) f.v.forEach((v, k) => { pos[v.map((x) => x.toFixed(9)).join(',') + '/' + JSON.stringify(f.bone[k])] = 1; });
    const kinds = { deform:0, socket:0, root:0 }; for (const b of B.sk.bones) kinds[b.kind]++;
    const fam = {};
    for (const [f, list] of Object.entries(FAM)) { const s = shortOf(B, list), frames = list.reduce((a, n) => a + C.ANIMS[C.clipDef(n).anim].frames, 0);
      const row = { clips:list, frames, reach_mm:s.mm, makesTargets:s.mm <= 5 }; if (s.mm > 5) row.worstAt = s.at;
      if (f === 'walk' || f === 'run') row.speed_mps = C.evalClip(f, 0, B).I.meta.speed;
      if (f === 'sit/drive') row.sit = 'drive';
      if (f === 'ride' || f === 'mount') { const s2 = shortOf(B, list, FIT); row.fitted = { saddle:'the default scaled to ' + C.CONTRACT.saddleFit.scale + ', idle spot ' + C.CONTRACT.saddleFit.reachX + ' m', reach_mm:s2.mm, makesTargets:s2.mm <= 5 }; if (s2.mm > 5) row.fitted.worstAt = s2.at; }
      fam[f] = row; }
    presets[p] = { label:C.BUILDS[p].label, paintedMaterials:matList.length, distinctRamps:Object.keys(rampSet).length, fixedColours:fixed.length,
      vertices:sz.verts, uniqueVertices:Object.keys(pos).length, faces:sz.faces, tris:sz.tris, bones:sz.bones, deformBones:kinds.deform, socketBones:kinds.socket,
      clips:sz.clipCount, frames:sz.frames, crown_m:+(B.D.heightM).toFixed(3), families:fam };
    if (sz.bones !== 28 || sz.clipCount !== 53) problems.push(p + ': ' + sz.bones + ' bones, ' + sz.clipCount + ' clips'); }
  const json = { tool:'check-numbers', rig:rigSha, poses:poseSha, ok:problems.length === 0, problems, families:FAM,
    notes:{ vertices:'unwelded: one per face corner (flat shading keeps faces unwelded); uniqueVertices welds equal position + equal skin weights',
      bones:'28 = root + 17 deform + 10 sockets, the same in every build', sit:'drive is the only seated clip; there is no separate sit',
      reach:'worst limb shortfall at the default world contracts, mm; <= 5 makes its targets', fitted:'ride and mount also at CONTRACT.saddleFit, the saddle they are gated at' }, presets };
  const pad = (s, n) => (String(s) + '                        ').slice(0, n), L = [];
  L.push('check-numbers — per preset', 'rig ' + rigSha + '  poses ' + poseSha, '');
  L.push('preset      painted ramps fixed  verts (unique)  faces  tris  bones  clips/frames');
  for (const [p, x] of Object.entries(presets)) L.push(pad(p, 12) + pad(x.paintedMaterials, 8) + pad(x.distinctRamps, 6) + pad(x.fixedColours, 7) + pad(x.vertices + ' (' + x.uniqueVertices + ')', 16) + pad(x.faces, 7) + pad(x.tris, 6) + pad(x.bones, 7) + x.clips + '/' + x.frames);
  L.push('', 'Animations (reach mm at the default world contracts; <= 5 makes every target):');
  L.push(pad('preset', 12) + Object.keys(FAM).map((f) => pad(f, 11)).join(''));
  for (const [p, x] of Object.entries(presets)) L.push(pad(p, 12) + Object.values(x.families).map((r) => pad((r.makesTargets ? 'yes' : 'short') + ' ' + r.reach_mm, 11)).join(''));
  L.push('', 'Ride and mount at the fitted saddle (the default scaled to ' + C.CONTRACT.saddleFit.scale + ', idle spot ' + C.CONTRACT.saddleFit.reachX + ' m beside it), reach mm:');
  for (const [p, x] of Object.entries(presets)) L.push('  ' + pad(p, 10) + 'ride ' + pad(x.families.ride.fitted.reach_mm, 5) + 'mount ' + x.families.mount.fitted.reach_mm);
  L.push('', 'Every preset has every family: the same 53 clips, frames and ms. "sit" is drive. Walk / run speeds (m/s):');
  for (const [p, x] of Object.entries(presets)) L.push('  ' + pad(p, 10) + x.families.walk.speed_mps + ' / ' + x.families.run.speed_mps);
  L.push('', problems.length ? 'PROBLEMS:\n  ' + problems.join('\n  ') : 'OK — no problems.', '');
  return { name:'numbers', ok:json.ok, json, text:L.join('\n') };
}
if (typeof module !== 'undefined' && module.exports) { module.exports = run; if (require.main === module) require('./_run.cjs').main(run); }
else (globalThis.HHTools = globalThis.HHTools || {}).numbers = run;
})();
