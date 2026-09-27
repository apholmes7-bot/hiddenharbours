/* tools/compare-rig7.cjs — v9 against rig 7 (characterIsoRig7.js rev 7.1) on the Fisher, from the files both kits ship.
     node tools/compare-rig7.cjs [rig7-kit]      writes reports/rig7.{json,txt}; add --write to regenerate samples/
   rig7-kit is the folder that holds rig 7's samples/ and golden-report.json (default ../skinned-character-kit, i.e. the two
   kits unzipped side by side). Rig 7 is not loaded: only its four exports are read.
     rig 7   samples/fisher.skeleton.json, samples/fisher.skinned.json, samples/fisher.walk.clip.json, golden-report.json
     v9      builds/fisher.v9.json, golden-report.json, and the rig (to regenerate v9's two samples)
   v9's samples are cut from builds/fisher.v9.json in rig 7's shapes, so the pairs diff directly:
     samples/fisher.skeleton.v9.json    = .skeleton, indent 1, as rig 7's fisher.skeleton.json
     samples/fisher.walk.clip.v9.json   = the walk clip, compact, as rig 7's fisher.walk.clip.json
   They are compared byte for byte with that cut; a fresh skeleton('fisher') and clip('walk', 'fisher') from the rig are compared
   number by number (gate 1e-9: a JavaScript engine may differ from the one that wrote the build in the last bit).
   The bone map (MAP below) is the design; this tool checks it covers both skeletons and measures it. */
'use strict';
(function () {
const MAP = { root:['root', 'same'], pelvis:['pelvis', 'same'],
  torso:['spine chest', 'split in two: spine (pelvis up to the waist ring) and chest (the waist ring up); the waist ring blends them 0.5 / 0.5'],
  neck:['neck', 'same'], neck_tip:[null, 'folded into neck: head is its child at a constant length'],
  head:['head', 'same; rig 7\'s sits 0.10 m above neck_tip (the head centre), v9\'s at the top of the neck'],
  inseam_top:[null, 'removed: v9 draws no inseam panel, so nothing is parked'], inseam_bot:[null, 'removed (as inseam_top)'],
  carry_mid:['carry_mid', 'same socket; parent torso -> chest'],
  apron_hem:[null, 'removed (not on the Fisher): the apron is rigid rings on chest, spine and pelvis'],
  skirt_hem:[null, 'removed (not on the Fisher): the skirt is rigid on pelvis'] };
for (const s of ['L', 'R']) Object.assign(MAP, {
  ['hip_' + s]:['hip_' + s, 'same'], ['hip_' + s + '_tip']:[null, 'folded into hip_' + s + ': knee_' + s + ' is its child at a constant length'],
  ['knee_' + s]:['knee_' + s, 'same'], ['knee_' + s + '_tip']:[null, 'folded into knee_' + s + ': foot_' + s + ' is its child at a constant length'],
  ['ankle_' + s]:[null, 'removed: the boot shaft is rings on knee_' + s + ' (part boot_' + s + ')'], ['ankle_' + s + '_tip']:[null, 'removed (as ankle_' + s + ')'],
  ['ankle_' + s + '_cuff']:[null, 'removed: the boot cuff is a ring on knee_' + s], ['foot_' + s]:['foot_' + s, 'same'],
  ['shoulder_' + s]:['shoulder_' + s, 'same; rig 7 put it at the drawn tube top (0.098 m under rig 6\'s joint), v9 at the joint'],
  ['shoulder_' + s + '_tip']:[null, 'folded into shoulder_' + s + ': elbow_' + s + ' is its child at a constant length'],
  ['elbow_' + s]:['elbow_' + s, 'same'], ['elbow_' + s + '_tip']:[null, 'folded into elbow_' + s + ': hand_' + s + ' is its child at a constant length'],
  ['wrist_' + s]:[null, 'removed: the cuff is a ring on elbow_' + s + ' (part fore_' + s + ')'], ['hand_' + s]:['hand_' + s, 'same'],
  ['tool_' + s]:['tool_' + s, 'same socket: +y along the tool'], ['tool_' + s + '_1']:['tool_' + s + '_1', 'same socket (0.40 of the tool length)'],
  ['tool_' + s + '_2']:['tool_' + s + '_2', 'same socket (0.70 of the tool length)'], ['carry_' + s]:['carry_' + s, 'same socket'] });
const V9_NEW = { back:'new socket on chest: the slung rod' };
const EQ = { eyes:(f) => (f.eyesClosed || f.lid >= 1) ? 'shut' : f.lid > 0 ? 'half' : f.brow < 0 ? 'wide' : 'open',
  brows:(f) => f.brow < 0 ? 'up' : f.brow > 0 ? 'knit' : 'flat',
  mouth:(f) => ({ neutral:'flat', oh:'open', grit:'grit', grin:'smile' })[f.mouth] || '?' };
const EQ_TEXT = [
  'eyes   eyesClosed, or lid 1 -> shut · 0 < lid < 1 -> half · lid 0 with brow -1 -> wide · otherwise open',
  'brows  brow -1 -> up · 0 -> flat · +1 -> knit',
  'mouth  neutral -> flat · oh -> open · grit -> grit · grin -> smile',
  'gaze   no v9 group: the eye marks do not move (dropped)' ];
function run(C, H) {
  const problems = [], L = [], rigSha = H.sha256('Art/characterIsoRig9.js'), poseSha = H.sha256('Art/characterIsoRig9.poses.js');
  const dir = (H.argv.find((a) => !a.startsWith('--')) || '../skinned-character-kit').replace(/\/+$/, '');
  const F7 = { skeleton:dir + '/samples/fisher.skeleton.json', skinned:dir + '/samples/fisher.skinned.json', walk:dir + '/samples/fisher.walk.clip.json', golden:dir + '/golden-report.json' };
  for (const p of Object.values(F7)) if (!H.exists(p)) throw new Error('compare-rig7: no ' + p + ' — pass the folder that holds rig 7\'s samples/ and golden-report.json');
  const sk7 = JSON.parse(H.text(F7.skeleton)), s7 = JSON.parse(H.text(F7.skinned)), w7 = JSON.parse(H.text(F7.walk)), g7 = JSON.parse(H.text(F7.golden));
  const b9 = JSON.parse(H.text('builds/fisher.v9.json')), g9 = JSON.parse(H.text('golden-report.json'));
  if (s7.rig !== 'characterIsoRig7' || s7.revision !== '7.1') problems.push('fisher.skinned.json is ' + s7.rig + ' ' + s7.revision + ', not characterIsoRig7 7.1');
  if (b9.derivedFromRigSha256 !== rigSha || b9.posesDerivedFromRigSha256 !== poseSha) problems.push('builds/fisher.v9.json stamps do not match the rig / pose library on disk');
  const inputs = { rig7:{}, v9:{} };
  for (const [k, p] of Object.entries(F7)) inputs.rig7[p.slice(dir.length + 1)] = H.sha256(p);
  for (const p of ['builds/fisher.v9.json', 'golden-report.json']) inputs.v9[p] = H.sha256(p);
  const r3 = (x) => Math.round(x * 1000) / 1000, mm = (a, b) => Math.round(Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2]) * 10000) / 10;
  const pad = (s, n) => { s = String(s); return s.length >= n ? s + ' ' : s + ' '.repeat(n - s.length); };
  const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
  /* ---- samples ---- */
  const skel9 = b9.skeleton, walk9 = b9.clips.find((c) => c.name === 'walk');
  const SAMPLES = { 'samples/fisher.skeleton.v9.json':JSON.stringify(skel9, null, 1), 'samples/fisher.walk.clip.v9.json':JSON.stringify(walk9) }, samples = {};
  for (const [p, t] of Object.entries(SAMPLES)) { const on = H.exists(p) ? H.text(p) : null;
    samples[p] = on == null ? 'not on disk' : on === t ? 'byte-identical to the cut' : 'differs from the cut'; if (on !== t && !H.argv.includes('--write')) problems.push(p + ' ' + samples[p] + ' (node tools/compare-rig7.cjs --write)'); }
  function numDiff(a, b) { const r = { numbers:0, differ:0, max:0, structure:0 };
    (function rec(x, y) { if (typeof x === 'number' && typeof y === 'number') { r.numbers++; const d = Math.abs(x - y); if (d > 0) r.differ++; if (d > r.max) r.max = d; }
      else if (x && y && typeof x === 'object' && typeof y === 'object') { const kx = Object.keys(x), ky = Object.keys(y); if (kx.join() !== ky.join()) r.structure++; for (const k of kx) if (k in y) rec(x[k], y[k]); }
      else if (x !== y) r.structure++; })(a, b); return r; }
  const fresh = { skeleton:numDiff(C.skeleton('fisher'), skel9), walk:numDiff(C.clip('walk', 'fisher'), walk9) };
  for (const [k, d] of Object.entries(fresh)) if (d.structure || d.max > 1e-9) problems.push('fresh ' + k + ' from the rig differs from builds/fisher.v9.json: ' + JSON.stringify(d));
  /* ---- skeleton ---- */
  const n7 = sk7.map((b) => b.id), n9 = b9.skeleton.map((b) => b.id), W7 = {}, B9 = {};
  if (!same(sk7, s7.skeleton)) problems.push('rig 7 fisher.skeleton.json is not fisher.skinned.json .skeleton');
  for (const b of s7.skeletonWorld) W7[b.id] = b; for (const b of b9.skeleton) B9[b.id] = b;
  const rows = [], hit = {};
  for (const b of sk7) { const m = MAP[b.id]; if (!m) { problems.push('rig 7 bone ' + b.id + ' is not in the map'); continue; }
    const to = m[0] ? m[0].split(' ') : [], d = to.map((t) => { if (!B9[t]) { problems.push('map target ' + t + ' is not a v9 bone'); return null; } hit[t] = 1; return mm(W7[b.id].pos, B9[t].bind.pos); });
    rows.push({ rig7:b.id, parent7:b.parent < 0 ? null : n7[b.parent], v9:m[0], parent9:to.length === 1 && B9[to[0]] && B9[to[0]].parent >= 0 ? n9[B9[to[0]].parent] : null,
      how:m[1], bind7:W7[b.id].pos.map(r3), bind9:to.map((t) => B9[t] ? B9[t].bind.pos.map(r3) : null), bindDelta_mm:d }); }
  for (const id of n9) if (!hit[id] && !V9_NEW[id]) problems.push('v9 bone ' + id + ' has no rig 7 source and is not listed as new');
  const counts = { rig7:n7.length, v9:n9.length, kept:rows.filter((r) => r.v9 && r.v9.indexOf(' ') < 0).length, split:rows.filter((r) => r.v9 && r.v9.indexOf(' ') > 0).length,
    folded:rows.filter((r) => !r.v9 && /^folded/.test(r.how)).length, removed:rows.filter((r) => !r.v9 && /^removed/.test(r.how)).length, v9New:Object.keys(V9_NEW).length };
  const nonIdRest7 = sk7.filter((b) => Math.abs(b.rest.rot[3]) < 1 - 1e-12).length, nonIdRest9 = b9.skeleton.filter((b) => !same(b.rest.rot, [0, 0, 0, 1]) || !same(b.bind.rot, [0, 0, 0, 1])).length;
  let fk = 0; const wp = []; b9.skeleton.forEach((b, i) => { const p = b.parent < 0 ? [0, 0, 0] : wp[b.parent]; wp[i] = [p[0] + b.rest.pos[0], p[1] + b.rest.pos[1], p[2] + b.rest.pos[2]];
    fk = Math.max(fk, Math.hypot(wp[i][0] - b.bind.pos[0], wp[i][1] - b.bind.pos[1], wp[i][2] - b.bind.pos[2])); });
  /* ---- bind mesh ---- */
  function meshStats(mesh, names) { const inf = {}, parts = {}, blend = {}, bset = {}; let verts = 0;
    for (const f of mesh) { const pm = (parts[f.part] = parts[f.part] || {}); pm[f.mat] = (pm[f.mat] || 0) + 1; bset[f.b || 0] = 1;
      for (const vb of f.bone) { verts++; inf[vb.length] = (inf[vb.length] || 0) + 1;
        if (vb.length > 1) { const k = f.part + ': ' + vb.map(([i, w]) => names[i] + ' ' + r3(w)).join(' + '); blend[k] = (blend[k] || 0) + 1; } } }
    return { faces:mesh.length, tris:mesh.reduce((s, f) => s + f.v.length - 2, 0), verts, influences:inf, blended:blend, parts,
      fields:[...new Set(mesh.flatMap((f) => Object.keys(f)))], materials:[...new Set(mesh.map((f) => f.mat))].sort(), bValues:Object.keys(bset).map(Number).sort((a, b) => a - b),
      bytes:JSON.stringify(mesh).length }; }
  const m7 = meshStats(s7.bindMesh, n7), m9 = meshStats(b9.bindMesh, n9);
  /* ---- clips ---- */
  const c7 = {}; for (const c of s7.clips) c7[c.name] = c;
  if (!same(w7, c7.walk)) problems.push('rig 7 fisher.walk.clip.json is not the walk clip in fisher.skinned.json');
  const common = b9.clips.filter((c) => c7[c.name]), onlyV9 = b9.clips.filter((c) => !c7[c.name]).map((c) => c.name), onlyR7 = s7.clips.filter((c) => !b9.clips.some((d) => d.name === c.name)).map((c) => c.name);
  const hdr = [], uDiff = []; for (const c of common) { const r = c7[c.name];
    for (const k of ['frames', 'ms', 'loop', 'settle', 'mount']) if (r[k] !== c[k]) hdr.push(c.name + ' ' + k + ' ' + r[k] + ' -> ' + c[k]);
    c.tracks.forEach((t, k) => { if (t.u !== r.tracks[k].u || t.ms !== r.tracks[k].ms) uDiff.push(c.name + ' f' + k); }); }
  const keysOf = (list) => [...new Set(list.flatMap((o) => Object.keys(o || {})))];
  const T7 = s7.clips.flatMap((c) => c.tracks), T9 = b9.clips.flatMap((c) => c.tracks);
  const fmt = { clip:{ rig7:keysOf(s7.clips), v9:keysOf(b9.clips) }, track:{ rig7:keysOf(T7), v9:keysOf(T9) },
    bone:{ rig7:keysOf(T7.flatMap((t) => Object.values(t.bones))), v9:keysOf(T9.flatMap((t) => Object.values(t.bones))) },
    face:{ rig7:keysOf(T7.map((t) => t.face)), v9:keysOf(T9.map((t) => t.face)) }, tool:{ rig7:keysOf(T7.map((t) => t.tool)), v9:keysOf(T9.map((t) => t.tool)) } };
  const diffKeys = (a, b) => ({ removed:a.filter((k) => b.indexOf(k) < 0), added:b.filter((k) => a.indexOf(k) < 0) });
  const movesPos = (clip, restOf) => clip.bones.filter((id) => clip.tracks.some((t) => t.bones[id].pos.some((x, j) => Math.abs(x - restOf(id)[j]) > 1e-9)));
  const R7 = {}; for (const b of sk7) R7[b.id] = b.rest.pos;
  const walkPos = { rig7:movesPos(w7, (id) => R7[id]), v9:movesPos(walk9, (id) => B9[id].rest.pos) };
  const rotStep = (c) => { let m = 0, at = null; for (let k = 1; k < c.tracks.length; k++) for (const id of c.bones) { const a = c.tracks[k - 1].bones[id].rot, b = c.tracks[k].bones[id].rot;
    const d = 2 * Math.acos(Math.min(1, Math.abs(a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3]))) * 180 / Math.PI; if (d > m) { m = d; at = id + ' f' + (k - 1) + '-' + k; } } return { deg:Math.round(m * 10) / 10, at }; };
  const walkStep = { rig7:rotStep(w7), v9:rotStep(walk9) };
  /* ---- face ---- */
  const seen = { lid:{}, gaze:{}, brow:{}, mouth:{}, eyesClosed:0 }, agree = { eyes:0, brows:0, mouth:0, all:0 }, xt = {}, diffs = {}; let frames = 0;
  for (const c of common) c.tracks.forEach((t, k) => { const f = c7[c.name].tracks[k].face, g = t.face; frames++;
    seen.lid[f.lid] = 1; seen.gaze[f.gaze.join(',')] = 1; seen.brow[f.brow] = 1; seen.mouth[f.mouth] = 1; if (f.eyesClosed) seen.eyesClosed++;
    let all = true; for (const s of ['eyes', 'brows', 'mouth']) { const e = EQ[s](f); if (e === g[s]) agree[s]++; else { all = false; (diffs[c.name] = diffs[c.name] || []).push('f' + k + ' ' + s + ' ' + e + '->' + g[s]); } }
    if (all) agree.all++;
    const key = 'lid ' + f.lid + (f.eyesClosed ? ' closed' : '') + ' · brow ' + f.brow + ' · ' + f.mouth + (f.gaze[0] || f.gaze[1] ? ' · gaze ' + f.gaze.join(',') : '') + '  ->  ' + g.eyes + ' / ' + g.brows + ' / ' + g.mouth;
    (xt[key] = xt[key] || []).push(c.name + ' f' + k); });
  const groups = Object.entries(b9.faceGroups).map(([g, r]) => ({ group:g, first:r[0], count:r[1], bones:[...new Set(b9.bindMesh.slice(r[0], r[0] + r[1]).flatMap((f) => f.bone.flat().map(([i]) => n9[i])))],
    mats:[...new Set(b9.bindMesh.slice(r[0], r[0] + r[1]).map((f) => f.mat))] }));
  const groupFaces = groups.reduce((s, g) => s + g.count, 0), firstGroup = Math.min(...groups.map((g) => g.first));
  if (firstGroup + groupFaces !== b9.bindMesh.length) problems.push('face groups are not the contiguous tail of the bind mesh');
  /* ---- materials ---- */
  const sh = b9.shading, painted = m9.materials, tbl = sh.materials;
  const matFmt = { rig7:'per face: mat (a rig 6 material name), b, db. No material table in the export: ramps, gains and biases are rig 6 code (makeMats, SPAN, GAIN 3.0, BIAS, LN, BAYER).',
    v9:'per face: mat, b, db (+ group, minT, side). shading.materials[mat] = { ramp, gain, bias, lo, hi, off } or { color, fixed:true }; shading = the globals (' + Object.keys(sh).filter((k) => k !== 'materials').join(', ') + ').' };
  const tblKinds = { ramp:Object.keys(tbl).filter((k) => !tbl[k].fixed).length, fixed:Object.keys(tbl).filter((k) => tbl[k].fixed).length };
  /* ---- golden ---- */
  const G7 = { builds:g7.builds.length, rows:g7.builds[0].rows.length, frames:g7.builds.reduce((s, b) => s + b.frames, 0), tol:g7.tol, worst:Math.max(...g7.builds.map((b) => b.worst)),
    worstAt:g7.builds.reduce((a, b) => (b.worst > a.worst ? b : a)).build, ok:g7.builds.every((b) => b.ok), keys:{ top:Object.keys(g7), build:Object.keys(g7.builds[0]), row:Object.keys(g7.builds[0].rows[0]) } };
  const gb = Object.values(g9.builds), G9 = { builds:gb.length, checks:gb[0].checks.length, passed:gb.reduce((s, b) => s + b.passed, 0), of:gb.reduce((s, b) => s + b.of, 0),
    ids:gb[0].checks.map((c) => c.id + (c.gate === false ? ' (report)' : '')), roundtrip:Object.fromEntries(Object.entries(g9.builds).map(([p, b]) => [p, b.checks.find((c) => c.id === 'roundtrip').value])),
    keys:{ top:Object.keys(g9), build:Object.keys(gb[0]), check:Object.keys(gb[0].checks[0]) } };
  /* ---- sizes ---- */
  const bytes = (x) => JSON.stringify(x).length;
  const sizes = { rig7:{ bones:n7.length, faces:m7.faces, tris:m7.tris, verts:m7.verts, clips:s7.clips.length, frames:T7.length, skeletonBytes:bytes(s7.skeleton), skeletonWorldBytes:bytes(s7.skeletonWorld), bindMeshBytes:m7.bytes, clipBytes:bytes(s7.clips), fileBytes:H.text(F7.skinned).length },
    v9:{ bones:n9.length, faces:m9.faces, tris:m9.tris, verts:m9.verts, clips:b9.clips.length, frames:T9.length, skeletonBytes:bytes(b9.skeleton), skeletonWorldBytes:0, bindMeshBytes:m9.bytes, clipBytes:bytes(b9.clips), fileBytes:H.text('builds/fisher.v9.json').length } };
  /* ---- text ---- */
  L.push('compare-rig7 — v9 (characterIsoRig9 9.1) against rig 7 (characterIsoRig7 7.1), the Fisher', 'rig ' + rigSha + '  poses ' + poseSha, '');
  L.push('rig 7 files (' + dir + '):'); for (const [p, h] of Object.entries(inputs.rig7)) L.push('  ' + pad(p, 31) + h);
  L.push('v9 files:'); for (const [p, h] of Object.entries(inputs.v9)) L.push('  ' + pad(p, 31) + h);
  L.push('', 'SKELETON  ' + n7.length + ' bones -> ' + n9.length + ': ' + counts.kept + ' kept, 1 split in two, ' + counts.folded + ' tips folded, ' + counts.removed + ' removed, ' + counts.v9New + ' new (bind delta = rig 7 skeletonWorld pos to v9 bind.pos, mm)');
  L.push('  ' + pad('rig 7', 15) + pad('v9', 13) + pad('bind delta', 12) + 'how');
  for (const r of rows) L.push('  ' + pad(r.rig7, 15) + pad(r.v9 || '—', 13) + pad(r.bindDelta_mm.length ? r.bindDelta_mm.join(' / ') : '', 12) + r.how);
  for (const [id, how] of Object.entries(V9_NEW)) L.push('  ' + pad('—', 15) + pad(id, 13) + pad('', 12) + how);
  L.push('  on other rig 7 builds: apron_hem, skirt_hem (44–46 bones by garment). v9: 28 on every build (root + 17 deform + 10 sockets).');
  L.push('  bind delta compares two different binds: rig 7\'s is idle f0 of rig 6\'s Fisher (limbs bent, tubes at drawn points); v9\'s is the figure standing straight, arms down.');
  L.push('  rest rotations: rig 7 ' + nonIdRest7 + ' of ' + n7.length + ' bones are not identity (bind = idle f0); v9 ' + nonIdRest9 + ' of ' + n9.length + ' (every bind frame is identity).');
  L.push('  skeletonWorld: rig 7 exports it (' + s7.skeletonWorld.length + ' x { id, pos, rot, rotEuler }, the bindposes\' inverses); v9 does not: bind.pos is the joint in the figure frame, rotation identity,');
  L.push('    so bindpose = translate(-bind.pos). Checked: bind.pos = the sum of rest.pos along the parents to ' + fk.toExponential(2) + ' m on all ' + n9.length + ' bones.');
  L.push('', 'BIND MESH  faces ' + m7.faces + ' -> ' + m9.faces + ' · tris ' + m7.tris + ' -> ' + m9.tris + ' · verts ' + m7.verts + ' -> ' + m9.verts + ' (unwelded) · bytes ' + m7.bytes + ' -> ' + m9.bytes);
  L.push('  influences per vertex: rig 7 ' + JSON.stringify(m7.influences) + ' · v9 ' + JSON.stringify(m9.influences));
  L.push('  blended: rig 7 ' + Object.entries(m7.blended).map(([k, v]) => k + ' (' + v + ')').join('; ')); L.push('           v9 ' + Object.entries(m9.blended).map(([k, v]) => k + ' (' + v + ')').join('; '));
  L.push('  face fields: rig 7 ' + m7.fields.join(', ') + ' · v9 adds ' + diffKeys(m7.fields, m9.fields).added.join(', '));
  L.push('  b (band offset): rig 7 ' + m7.bValues.length + ' distinct values, ' + r3(m7.bValues[0]) + ' .. ' + r3(m7.bValues[m7.bValues.length - 1]) + ' (continuous) · v9 ' + m9.bValues.join(', '));
  L.push('  parts, materials (faces):');
  for (const p of [...new Set(Object.keys(m7.parts).concat(Object.keys(m9.parts)))]) { const a = m7.parts[p], b = m9.parts[p], f = (x) => x ? Object.entries(x).map(([m, n]) => m + ' ' + n).join(', ') : '—';
    L.push('    ' + pad(p, 12) + pad(f(a), 44) + '| ' + f(b)); }
  L.push('', 'FACE  rig 7: track.face = { lid, gaze, brow, mouth, eyesClosed }, a HeadIso raster stamp on the head. v9: ' + groups.length + ' face groups (' + groupFaces + ' faces, bind-mesh faces ' + firstGroup + '..' + (firstGroup + groupFaces - 1) + '), all on head at weight 1; track.face = { eyes, brows, mouth }.');
  for (const g of groups) L.push('  ' + pad(g.group, 12) + pad('faces ' + g.first + ' +' + g.count, 16) + g.mats.join(' '));
  L.push('  rig 7 values on the 35 shared clips (' + frames + ' frames): lid ' + Object.keys(seen.lid).join(' ') + ' · brow ' + Object.keys(seen.brow).join(' ') + ' · mouth ' + Object.keys(seen.mouth).join(' ') + ' · gaze ' + Object.keys(seen.gaze).join(' | ') + ' · eyesClosed on ' + seen.eyesClosed + ' frames');
  L.push('  equivalents (rig 7 value -> v9 state):'); for (const t of EQ_TEXT) L.push('    ' + t);
  L.push('  v9 authors its own face per clip frame (the pose library). Frames where it equals the equivalent: eyes ' + agree.eyes + ', brows ' + agree.brows + ', mouth ' + agree.mouth + ', all three ' + agree.all + ' of ' + frames + '.');
  L.push('  cross-tab (rig 7 face  ->  v9 eyes / brows / mouth: frames):');
  for (const [k, v] of Object.entries(xt).sort((a, b) => b[1].length - a[1].length)) L.push('    ' + pad(v.length, 4) + k + '   e.g. ' + v.slice(0, 3).join(', '));
  L.push('  frames that differ from the equivalent, by clip:'); for (const [c, v] of Object.entries(diffs)) L.push('    ' + pad(c, 13) + v.join(' · '));
  L.push('', 'CLIPS  rig 7 ' + s7.clips.length + ' · v9 ' + b9.clips.length + ' (' + common.length + ' shared, ' + onlyV9.length + ' new' + (onlyR7.length ? ', rig 7 only: ' + onlyR7.join(' ') : '') + ')');
  L.push('  shared: frames, ms, loop, settle identical; u and ms per track ' + (uDiff.length ? 'differ on ' + uDiff.length : 'identical on every frame') + '; mount label: ' + (hdr.length ? hdr.join(' · ') : 'identical'));
  L.push('  new in v9: ' + onlyV9.join(' '));
  L.push('  rig 7 made carries and the three long-rod casts through opts (clip(anim, build, null, { carry | power })); v9 names the carries as clips and has no long-rod cast.');
  for (const [k, v] of Object.entries(fmt)) { const d = diffKeys(v.rig7, v.v9); L.push('  ' + pad(k, 6) + 'rig 7 { ' + v.rig7.join(', ') + ' } -> v9 { ' + v.v9.join(', ') + ' }' + (d.removed.length || d.added.length ? '  (removed ' + (d.removed.join(', ') || '—') + '; added ' + (d.added.join(', ') || '—') + ')' : '')); }
  L.push('  walk: bones whose local pos moves off rest: rig 7 ' + walkPos.rig7.length + ' (' + walkPos.rig7.join(' ') + ') · v9 ' + walkPos.v9.length + ' (' + walkPos.v9.join(' ') + ')');
  L.push('  walk: largest frame-to-frame turn of one bone: rig 7 ' + walkStep.rig7.deg + ' deg (' + walkStep.rig7.at + ', a tube roll seam) · v9 ' + walkStep.v9.deg + ' deg (' + walkStep.v9.at + ')');
  L.push('', 'MATERIALS  rig 7 ' + matFmt.rig7, '           v9 ' + matFmt.v9);
  L.push('  Fisher: rig 7 paints ' + m7.materials.length + ' (' + m7.materials.join(' ') + '); v9 paints ' + painted.length + ' (' + painted.join(' ') + ')');
  L.push('  v9 shading.materials lists every material the rig defines, painted or not: ' + Object.keys(tbl).length + ' (' + tblKinds.ramp + ' ramp, ' + tblKinds.fixed + ' fixed).');
  L.push('', 'GOLDEN REPORT  rig 7: the export against rig 6\'s own meshes, vertex by vertex: ' + G7.builds + ' builds x ' + G7.rows + ' rows, ' + G7.frames + ' frames, tol ' + G7.tol + ' m, worst ' + G7.worst.toExponential(2) + ' m (' + G7.worstAt + '), all ok: ' + G7.ok);
  L.push('    keys: { ' + G7.keys.top.join(', ') + ' } · builds[] { ' + G7.keys.build.join(', ') + ' } · rows[] { ' + G7.keys.row.join(', ') + ' }');
  L.push('  v9: runChecks, ' + G9.checks + ' checks x ' + G9.builds + ' presets, ' + G9.passed + ' / ' + G9.of + ' passed; no reference rig. The round trip (clip -> quaternion -> FK -> skin = the solver) is rig 7\'s rule on v9 itself:');
  L.push('    ' + Object.entries(G9.roundtrip).map(([p, v]) => p + ' ' + v).join(' · '));
  L.push('    checks: ' + G9.ids.join(', '));
  L.push('    keys: { ' + G9.keys.top.join(', ') + ' } · builds.<preset> { ' + G9.keys.build.join(', ') + ' } · checks[] { ' + G9.keys.check.join(', ') + ' }');
  L.push('', 'SIZES (JSON bytes)          rig 7        v9');
  for (const k of Object.keys(sizes.rig7)) L.push('  ' + pad(k, 26) + pad(sizes.rig7[k], 13) + sizes.v9[k]);
  L.push('', 'SAMPLES  cut from builds/fisher.v9.json: ' + Object.entries(samples).map(([p, s]) => p + ' ' + s).join(' · '));
  for (const [k, d] of Object.entries(fresh)) L.push('  fresh ' + pad(k, 9) + 'from the rig against the committed file: ' + d.numbers + ' numbers, ' + d.differ + ' differ, largest ' + d.max.toExponential(2) + (d.structure ? ', ' + d.structure + ' structural' : ', same structure'));
  L.push('', problems.length ? 'PROBLEMS:\n  ' + problems.join('\n  ') : 'OK — no problems.', '');
  const json = { tool:'compare-rig7', rig:rigSha, poses:poseSha, ok:problems.length === 0, problems, inputs, skeleton:{ counts, map:rows, v9New:V9_NEW, nonIdentityRest:{ rig7:nonIdRest7, v9:nonIdRest9 }, fkFromRest_m:fk },
    mesh:{ rig7:m7, v9:m9 }, face:{ equivalents:EQ_TEXT, seen, agree, frames, crossTab:xt, differs:diffs, groups },
    clips:{ shared:common.length, onlyV9, onlyR7, headerDiffs:hdr, uDiffs:uDiff, format:fmt, walkLocalPosMoves:walkPos, walkLargestTurn:walkStep }, materials:{ format:matFmt, rig7:m7.materials, v9:painted, table:tblKinds },
    golden:{ rig7:G7, v9:G9 }, sizes, samples, freshAgainstCommitted:fresh };
  return { name:'rig7', ok:json.ok, json, text:L.join('\n'), data:SAMPLES };
}
if (typeof module !== 'undefined' && module.exports) { module.exports = run; if (require.main === module) require('./_run.cjs').main(run); }
else (globalThis.HHTools = globalThis.HHTools || {}).rig7 = run;
})();
