#!/usr/bin/env node
/*
 * check-rig7-followup-combined.cjs — ONE checker over jobs 1-4 of the 2026-09-17 rig 7 follow-up, on the
 * MERGED chain. Every posing gate of the four kits holds at once; the kits' "as-delivered" pins are
 * re-based to the merged bytes and named (REBASE lines) with old -> new.
 *
 *   node check/check-rig7-followup-combined.cjs --main <main rigs> --merged <merged rigs> \
 *        --j1 <kit1 rigs> --j2 <kit2 rigs> --j3 <kit3 rigs> --j4 <kit4 rigs> [--out report.txt]
 *
 * main = the accepted blobs (40f4656f); j1..j4 = each kit's rigs/ as delivered; merged = the landing.
 * Node only, no dependencies. Loads every file UNMODIFIED, the bake's catalog order.
 */
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm'), crypto = require('crypto');
const { loadKit, BASE, FACE } = require('./load-kit.cjs');
const { raster } = require('./face-render.cjs');

const arg = (k) => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : null; };
const DIR = { main: arg('main'), merged: arg('merged'), j1: arg('j1'), j2: arg('j2'), j3: arg('j3'), j4: arg('j4') };
for (const k of Object.keys(DIR)) if (!DIR[k]) throw new Error('missing --' + k);

const TOL = 1e-4, GATE_M = 3.0, FENCE_M = 8.0, MAT_CAP = 16, PPM = 32, ELEV = 40;
const MOUNT = ['mountUp', 'mountDown', 'mountCab', 'mountCabDown'], FIXED = MOUNT.concat(['lift']);
const SHEET = { idle: { frames: 6, ms: 170 }, walk: { frames: 8, ms: 110 } };
const EXPECT = {                           // the landing's bytes (sha256 of the LF file)
  'characterIsoRig6.js': 'e440095542dce0c8c68b65e59a7a62f6f9eab10f98a11745f1a139f34a20bb28',
  'characterIsoRig7.js': 'f2c51c3be2a98b05c08a278e5a8308ead6a3228df0ab45320886cffdc816ceb8',
  'characterFaceStudy.js': '001dbcc01c592294170e83ac43fb640bfda83a6ba08d479b25553bebe941ed99',
};

const lines = []; let pass = 0, fail = 0;
const out = (s) => lines.push(s);
const gate = (ok, msg) => { out((ok ? 'PASS ' : 'FAIL ') + msg); ok ? pass++ : fail++; return ok; };
const note = (m) => out('NOTE ' + m);
const rebase = (name, was, now) => out('REBASE ' + name + '\n         old: ' + was + '\n         new: ' + now);
// sha256 of the LF form: git stores the rigs as LF blobs, and an autocrlf checkout hands this CRLF bytes.
const sha = (p) => crypto.createHash('sha256').update(fs.readFileSync(p, 'utf8').replace(/\r\n/g, '\n'), 'utf8').digest('hex');
const J = (x) => JSON.stringify(x);
const t0 = Date.now();

const ENGINE = fs.readFileSync(path.join(__dirname, 'cast-engine.js'), 'utf8');
function chain(dir, face) {
  const { context } = loadKit({ rigsDir: dir, face });
  if (face) vm.runInContext(ENGINE, context, { filename: 'cast-engine.js' });
  return context;
}
const MAIN = chain(DIR.main, true), M = chain(DIR.merged, true);
const K1 = chain(DIR.j1, false), K2 = chain(DIR.j2, false), K3 = chain(DIR.j3, false);
const A6 = MAIN.CharacterIso6, A7 = MAIN.CharacterIso7, B6 = M.CharacterIso6, B7 = M.CharacterIso7;
const CAST = B6.CAST, ANIMS = Object.keys(B6.ANIMS), FROZEN = ANIMS.filter((a) => FIXED.indexOf(a) < 0);
const den = (K, anim) => { const A = K.ANIMS[anim]; return A.settle ? Math.max(1, A.frames - 1) : A.frames; };
const bOf = (K, preset) => K.resolveBuild({ build: { preset } });
const facesAt = (K, preset, anim, k, carry) => { const b = bOf(K, preset);
  return K.facesOf(K.pose(anim, k / den(K, anim), b, 'short', carry || null, carry ? { carry } : {}), b); };
const maxV = (faces) => { let m = 0; for (const f of faces) for (const v of f.v) m = Math.max(m, Math.hypot(v[0], v[1], v[2])); return m; };
const same = (fa, fb) => J(fa.map((f) => f.v)) === J(fb.map((f) => f.v));

/* ================================================================================================ */
out('== 0. inputs: the merged bytes');
const ALL = BASE.concat(FACE);
gate(ALL.every((n) => fs.existsSync(path.join(DIR.merged, n))) && ALL.every((n) => fs.existsSync(path.join(DIR.main, n))),
  'main and merged both hold all ' + ALL.length + ' chain + face files');
for (const n of ALL) note(n.padEnd(30) + ' main ' + sha(path.join(DIR.main, n)).slice(0, 12) + '  merged ' + sha(path.join(DIR.merged, n)).slice(0, 12));
gate(Object.keys(EXPECT).every((n) => sha(path.join(DIR.merged, n)) === EXPECT[n]),
  'the three edited files carry the landing hashes: rig 6 e4400955, rig 7 f2c51c3b, face study 001dbcc0');
const edited = ALL.filter((n) => sha(path.join(DIR.main, n)) !== sha(path.join(DIR.merged, n)));
gate(J(edited.slice().sort()) === J(Object.keys(EXPECT).sort()),
  'exactly three files differ from main — ' + edited.join(', '));
rebase('job1 "only characterIsoRig7.js was edited"', 'edited set == {rig 7}', 'edited set vs main == {rig 6, rig 7, face study}, each at its landing hash');
rebase('job2 "only characterIsoRig7.js was edited"', 'edited set == {rig 7}', 'same as job1');
rebase('job3 "only characterIsoRig6.js and characterIsoRig7.js were edited"', 'edited set == {rig 6, rig 7} over the 4 chain files', 'same as job1 (the face study is outside job 3\'s 4-file chain)');
rebase('job4 "only characterFaceStudy.js was edited"', 'edited set == {face study} over 9 files', 'same as job1');
const nl = (d, n) => fs.readFileSync(path.join(d, n), 'utf8').split('\n').length;
const d1 = nl(DIR.j1, 'characterIsoRig7.js') - nl(DIR.main, 'characterIsoRig7.js');
const d2 = nl(DIR.j2, 'characterIsoRig7.js') - nl(DIR.main, 'characterIsoRig7.js');
const d3 = nl(DIR.j3, 'characterIsoRig7.js') - nl(DIR.main, 'characterIsoRig7.js');
const dm = nl(DIR.merged, 'characterIsoRig7.js') - nl(DIR.main, 'characterIsoRig7.js');
gate(nl(DIR.merged, 'characterIsoRig6.js') - nl(DIR.main, 'characterIsoRig6.js') === 2 && d3 === 0 && dm === d1 + d2,
  'rig 6 is +2 lines; rig 7 is +' + dm + ' = job1 +' + d1 + ' plus job2 +' + d2 + ', and job 3 adds 0 to it (in-place token)');
rebase('job3 "the edit is 2 new lines in rig 6 and 0 in rig 7"', 'rig 7 line delta vs main == 0', 'rig 7 line delta vs main == job1 delta + job2 delta (' + d1 + ' + ' + d2 + '); job 3\'s own delta still 0');
const t6 = fs.readFileSync(path.join(DIR.merged, 'characterIsoRig6.js'), 'utf8'), t7 = fs.readFileSync(path.join(DIR.merged, 'characterIsoRig7.js'), 'utf8');
gate(t6.indexOf('P.mountP = mnt;') >= 0 && t6.indexOf('P.liftP = ') >= 0 && t6.indexOf('|| P.mountP || P.liftP){') >= 0 &&
     t7.indexOf('|| P.mountP || P.liftP){') >= 0, 'job 3\'s fix is present in both merged rigs');
gate(!A7.carryClips && !A7.additive && typeof B7.carryClips === 'function' && typeof B7.additive === 'function' &&
     typeof B7.goldenDiffRocked === 'function', 'main\'s rig 7 has no carryClips/additive; the merged rig 7 has both APIs');
gate(B7.revision === A7.revision && B6.revision === A6.revision && B7.base === B6.revision,
  'revision strings unbumped: rig 7 ' + B7.revision + ', rig 6 ' + B6.revision + ', CharacterIso7.base == CharacterIso6.revision');

/* ================================================================================================ */
out(''); out('== 1. job 1: the carry-clip table');
const NAMES = B7.carryClipNames();
gate(J(NAMES) === J(['helm_idle', 'helm_walk', 'oars_idle', 'oars_row']), 'CARRY_CLIPS names, in order: ' + NAMES.join(', '));
out('  clip        rides  carry  frames    ms  loop   settle mount   power  parked      pin');
let sheetOk = true, ridesOk = true, rockNull = true, pinOk = true, bonesSame = true;
const idsOf = (K7, p) => K7.skeleton({ preset: p }).map((b) => b.id);
for (const n of NAMES) {
  const c = B7.carryClip(n, { preset: 'fisher' }), C = B7.CARRY_CLIPS[n], w = SHEET[c.rides];
  if (!(w && c.frames === w.frames && c.ms === w.ms)) sheetOk = false;
  if (B6.CARRIES[C.carry].anims.indexOf(C.anim) < 0) ridesOk = false;
  if (c.rock !== null) rockNull = false;
  const ids = idsOf(B7, 'fisher');
  if (!(c.pin.length > 0 && c.pin.every((id) => ids.indexOf(id) >= 0))) pinOk = false;
  out('  ' + n.padEnd(11) + c.rides.padEnd(7) + c.carry.padEnd(7) + String(c.frames).padStart(5) + String(c.ms).padStart(7) + '  ' +
      String(c.loop).padEnd(7) + String(!!c.settle).padEnd(7) + String(c.mount).padEnd(8) + String(c.power).padEnd(7) +
      J(c.parked || []).padEnd(12) + c.pin.join('+'));
}
for (const p of CAST) for (const n of NAMES) if (B7.carryClip(n, { preset: p }).bones.length !== idsOf(B7, p).length) bonesSame = false;
gate(sheetOk, 'frames/ms match FisherIso.asset HelmStance/OarsStance: idle 6f/170ms, walk 8f/110ms');
gate(ridesOk, 'every carry clip rides an anim rig 6 sanctions for that carry (CARRIES[carry].anims)');
gate(rockNull, 'rock is null on every carry clip');
gate(pinOk, 'every pin bone exists in the skeleton');
gate(bonesSame, 'a carry clip carries the full bone list on all ten presets (no bone added)');
let carrySame = true, carryAt = '';
for (const p of CAST) { const a = J(K1.CharacterIso7.carryClips({ preset: p })), b = J(B7.carryClips({ preset: p }));
  if (a !== b) { carrySame = false; carryAt = p; } }
gate(carrySame, 'NEW cross-kit pin: carryClips() on the merged chain is byte-identical to job 1\'s delivered chain, all ten presets' + (carryAt ? ' — differs on ' + carryAt : ''));
const ex = B7.exportBuild({ preset: 'fisher' });
gate(Array.isArray(ex.carryClips) && ex.carryClips.length === 4 && Array.isArray(ex.clips) && ex.clips.length === 35,
  'exportBuild(): carryClips ' + (ex.carryClips || []).length + ', clips ' + (ex.clips || []).length);

/* ================================================================================================ */
out(''); out('== 2. THE GOLDEN RULE on the merged chain');
const golden = (list, carryOf) => { let w = 0, at = '', err = null, n = 0;
  for (const preset of CAST) for (const name of list) {
    const anim = carryOf ? B7.CARRY_CLIPS[name].anim : name, carry = carryOf ? B7.CARRY_CLIPS[name].carry : null;
    for (let k = 0; k < B6.ANIMS[anim].frames; k++) {
      const d = B7.goldenDiffDetail(anim, k / den(B6, anim), { preset }, carry ? { carry } : {}); n++;
      if (d.err) { err = err || (preset + ' ' + name + ' f' + k + ': ' + d.err); continue; }
      if (d.max > w) { w = d.max; at = preset + ' ' + name + ' f' + k; } } }
  return { w, at, err, n }; };
const g35 = golden(ANIMS, false), gC = golden(NAMES, true);
gate(!g35.err && g35.w <= TOL, 'all 35 clips x 10 presets x every frame (' + g35.n + ' frames): goldenDiff max ' + g35.w.toExponential(2) + ' m (' + g35.at + ') <= ' + TOL + (g35.err ? ' ERR ' + g35.err : ''));
gate(!gC.err && gC.w <= TOL, 'the 4 carry clips x 10 presets x every frame (' + gC.n + ' frames) vs facesOf(pose(anim,u,build,{carry})): max ' + gC.w.toExponential(2) + ' m (' + gC.at + ')' + (gC.err ? ' ERR ' + gC.err : ''));

/* ================================================================================================ */
out(''); out('== 3. job 3: mount* and lift under ' + GATE_M + ' m (rig 6 facesOf, merged)');
let worstAll = 0, worstAt = '';
for (const anim of FIXED) { let w = 0, at = '';
  for (const p of CAST) for (let k = 0; k < B6.ANIMS[anim].frames; k++) { const m = maxV(facesAt(B6, p, anim, k)); if (m > w) { w = m; at = p + ' f' + k; } }
  gate(w < GATE_M, anim + ' max |vertex| = ' + w.toFixed(3) + ' m (' + at + ')');
  if (w > worstAll) { worstAll = w; worstAt = anim + ' ' + at; } }
note('worst of mount* + lift across ten presets: ' + worstAll.toFixed(3) + ' m (' + worstAt + ')');
let fd = true, fdAt = '';
for (const p of CAST) for (const anim of FROZEN) for (let k = 0; k < B6.ANIMS[anim].frames; k++)
  if (!same(facesAt(A6, p, anim, k), facesAt(B6, p, anim, k))) { fd = false; fdAt = p + ' ' + anim + ' f' + k; }
gate(fd, 'rig 6 facesOf bit-identical to MAIN on the other ' + FROZEN.length + ' clips x 10 presets x every frame' + (fdAt ? ' — differs at ' + fdAt : ''));
let fm = true, fmAt = '';
for (const p of CAST) for (const anim of FIXED) for (let k = 0; k < B6.ANIMS[anim].frames; k++)
  if (!same(facesAt(K3.CharacterIso6, p, anim, k), facesAt(B6, p, anim, k))) { fm = false; fmAt = p + ' ' + anim + ' f' + k; }
gate(fm, 'rig 6 facesOf bit-identical to JOB 3\'s delivered chain on the 5 fixed clips (the merge moved none of job 3\'s frames)' + (fmAt ? ' — differs at ' + fmAt : ''));

/* ================================================================================================ */
out(''); out('== 4. clip() records: what a bake reads');
let c30 = true, c30At = '', c5 = true, c5At = '', movedRows = [];
for (const p of CAST) for (const anim of ANIMS) {
  const b = J(B7.clip(anim, { preset: p }));
  if (FIXED.indexOf(anim) < 0) { if (J(A7.clip(anim, { preset: p })) !== b) { c30 = false; c30At = p + ' ' + anim; } }
  else if (J(K3.CharacterIso7.clip(anim, { preset: p })) !== b) { c5 = false; c5At = p + ' ' + anim; }
}
gate(c30, 'clip() byte-identical to MAIN on the ' + FROZEN.length + ' untouched clips x 10 presets' + (c30At ? ' — differs at ' + c30At : ''));
gate(c5, 'clip() byte-identical to JOB 3\'s delivered chain on the 5 fixed clips x 10 presets' + (c5At ? ' — differs at ' + c5At : ''));
rebase('job1 "the existing 35 clips are byte-identical on all ten presets"', 'clips() == as-delivered (main) for all 35', '30 clips == main; mountUp/mountDown/mountCab/mountCabDown/lift == job 3 delivered');
rebase('job2 "the existing 35 clips are byte-identical on all ten presets"', 'clips() == as-delivered (main) for all 35', 'same as job1');
// fisher at float32 — what the def stores
const f32 = (x) => Math.fround(x);
out('  fisher, per fixed clip: frames whose float32 keys differ from main (the def stores float32)');
for (const anim of FIXED) {
  const a = A7.clip(anim, { preset: 'fisher' }), b = B7.clip(anim, { preset: 'fisher' }); const fr = [];
  let meta = J([a.frames, a.ms, a.loop, a.settle, a.mount, a.carry, a.power, a.parked]) === J([b.frames, b.ms, b.loop, b.settle, b.mount, b.carry, b.power, b.parked]);
  for (let k = 0; k < b.tracks.length; k++) { const P = a.tracks[k].bones, Q = b.tracks[k].bones; let d = false, dm = 0;
    for (const id of Object.keys(Q)) { for (let i = 0; i < 3; i++) { if (f32(P[id].pos[i]) !== f32(Q[id].pos[i])) d = true; dm = Math.max(dm, Math.abs(P[id].pos[i] - Q[id].pos[i])); }
      for (let i = 0; i < 4; i++) if (f32(P[id].rot[i]) !== f32(Q[id].rot[i])) d = true; }
    if (d) fr.push('f' + k + '(' + dm.toFixed(3) + 'm)'); }
  movedRows.push([anim, fr.length, b.frames, meta]);
  out('    ' + anim.padEnd(13) + fr.length + '/' + b.frames + ' frames  header(frames,ms,loop,settle,mount,carry,power,parked) same: ' + meta + '  ' + fr.join(' '));
}
note('bone key order: ' + (J(A7.clip('idle', { preset: 'fisher' }).bones) === J(B7.clip('idle', { preset: 'fisher' }).bones) ? 'unchanged' : 'CHANGED'));

/* ================================================================================================ */
out(''); out('== 5. job 2: the additive rock on the merged chain (fisher)');
const ROLLS = [-15, -10, -5, 0, 5, 10, 15], PITCHES = ROLLS, COUNTERS = [0, 0.5, 1], RANIMS = ['idle', 'walk', 'balance', 'astride'];
let rw = 0, rAt = null, rErr = null, rows = 0, addSame = true, addAt = '';
for (const roll of ROLLS) for (const pitch of PITCHES) for (const counter of COUNTERS) for (const anim of RANIMS)
  for (const frame of [0, Math.floor(B6.ANIMS[anim].frames / 2)]) {
    const u = frame / den(B6, anim), rock = { roll, pitch, counter };
    const d = B7.goldenDiffRockedDetail(anim, u, { preset: 'fisher' }, rock); rows++;
    if (d.err) { rErr = rErr || J(rock) + ' ' + anim + ' f' + frame + ': ' + d.err; continue; }
    if (d.max > rw) { rw = d.max; rAt = J({ roll, pitch, counter, anim, frame }); }
    if (J(K2.CharacterIso7.additive(rock, { preset: 'fisher' }, anim, u)) !== J(B7.additive(rock, { preset: 'fisher' }, anim, u))) { addSame = false; addAt = J(rock) + ' ' + anim; }
  }
gate(!rErr && rw <= TOL, 'goldenDiffRocked over the brief\'s grid, ' + rows + ' rows: worst ' + rw.toExponential(2) + ' m ' + (rAt || '') + (rErr ? ' ERR ' + rErr : ''));
gate(addSame, 'NEW cross-kit pin: additive() byte-identical to job 2\'s delivered chain on all ' + rows + ' grid rows' + (addAt ? ' — differs at ' + addAt : ''));
{ const ROCK = { roll: 10, pitch: 5, counter: 1 }, touched = new Set();
  const qmul = (a, b) => [a[3]*b[0]+a[0]*b[3]+a[1]*b[2]-a[2]*b[1], a[3]*b[1]-a[0]*b[2]+a[1]*b[3]+a[2]*b[0], a[3]*b[2]+a[0]*b[1]-a[1]*b[0]+a[2]*b[3], a[3]*b[3]-a[0]*b[0]-a[1]*b[1]-a[2]*b[2]];
  const qang = (q) => 2 * Math.acos(Math.max(-1, Math.min(1, Math.abs(q[3])))) * 180 / Math.PI;
  for (const anim of RANIMS) { const p = B7.clip(anim, { preset: 'fisher' }, null, {}), q = B7.clip(anim, { preset: 'fisher' }, null, ROCK);
    for (let k = 0; k < p.tracks.length; k++) { const P = p.tracks[k].bones, Q = q.tracks[k].bones;
      for (const id of Object.keys(P)) { const r = P[id].rot, d = qmul(Q[id].rot, [-r[0], -r[1], -r[2], r[3]]);
        const dp = Math.hypot(P[id].pos[0]-Q[id].pos[0], P[id].pos[1]-Q[id].pos[1], P[id].pos[2]-Q[id].pos[2]);
        if (qang(d) >= 1e-3 || dp >= 1e-6) touched.add(id); } } }
  const legs = [...touched].filter((id) => /^(hip|knee|ankle|foot|pelvis|inseam)/.test(id));
  gate(touched.size === 21 && legs.length === 0, 'the finding reproduces: rock {10,5,1} touches ' + touched.size + ' of 45 bones, legs/pelvis/feet/inseam clean (' + legs.length + ')');
  note('touched: ' + [...touched].join(' ')); }
let ug = true, ugAt = '';
for (const p of CAST) for (const anim of ANIMS) if (K3.CharacterIso7.goldenDiff(anim, 0, { preset: p }) !== B7.goldenDiff(anim, 0, { preset: p })) { ug = false; ugAt = p + ' ' + anim; }
gate(ug, 'the unrocked golden at u=0 is identical to job 3\'s delivered chain on every clip and preset' + (ugAt ? ' — differs at ' + ugAt : ''));
rebase('job2 "the unrocked golden rule is unmoved on every clip and preset"', 'goldenDiff(anim,0) == as-delivered (main)', 'goldenDiff(anim,0) == job 3 delivered (main differs on mount*/lift by design)');

/* ================================================================================================ */
out(''); out('== 6. the pins (brief §3)');
gate(CAST.every((p) => J(idsOf(A7, p)) === J(idsOf(B7, p))) && idsOf(B7, 'fisher').length === 45, '45 bones, ids and order identical to main on all ten presets');
gate(CAST.every((p) => J(A7.skeleton({ preset: p })) === J(B7.skeleton({ preset: p })) && J(A7.skeletonWorld({ preset: p })) === J(B7.skeletonWorld({ preset: p }))),
  'skeleton() and skeletonWorld() byte-identical to main, all ten presets (every bone incl. tool_* and carry_* rest positions)');
let anc = true, ancAt = '', ancN = 0, ancKeys = '';
for (const p of CAST) for (const anim of FROZEN) for (let dir = 0; dir < B6.DIRS; dir++) {
  const a = A6.anchors(dir, { preset: p, anim, frame: 0 }), b = B6.anchors(dir, { preset: p, anim, frame: 0 });
  ancKeys = Object.keys(b).join(','); ancN++;
  if (J(a) !== J(b)) { anc = false; ancAt = p + ' ' + anim + ' dir' + dir; } }
gate(anc, 'rig 6 anchors() identical to main: ' + Object.keys(B6.anchors(0, { preset: 'fisher' })).length + ' named anchors (' + ancKeys + ') x ' + B6.DIRS + ' facings x ' + FROZEN.length + ' clips f0 x 10 presets = ' + ancN + ' calls' + (ancAt ? ' — differs at ' + ancAt : ''));
// The fourteen are CharacterFaceCompositionTests.Anchors (:51-56): skeletonWorld bones gameplay reads.
const ANCHORS14 = ['root', 'pelvis', 'torso', 'neck', 'head', 'hand_L', 'hand_R', 'foot_L', 'foot_R', 'tool_L', 'tool_R', 'carry_L', 'carry_R', 'carry_mid'];
let a14 = true, a14At = '', a14N = 0;
for (const p of CAST) {
  const wa = A7.skeletonWorld({ preset: p }), wb = B7.skeletonWorld({ preset: p });
  for (const id of ANCHORS14) {
    const x = wa.find((s) => s.id === id), y = wb.find((s) => s.id === id); a14N++;
    if (!x || !y || J([x.pos, x.rot]) !== J([y.pos, y.rot])) { a14 = false; a14At = p + ' ' + id + (y ? '' : ' ABSENT'); } } }
gate(a14 && a14N === 140, '14 named anchors (CharacterFaceCompositionTests.Anchors: ' + ANCHORS14.join(',') + ') present and pos+rot identical to main, 10 presets = ' + a14N + ' pins' + (a14At ? ' — differs at ' + a14At : ''));
const consts = (K) => J([K.W, K.H, K.PX, K.DIRS, K.pivot, K.GAIN, K.BIAS, K.LN, K.BAYER, K.CAST, K.BUILDS, K.ANIMS]);
gate(consts(A6) === consts(B6) && ANIMS.length === 35, 'cell (W,H,PX), pivot, DIRS, GAIN, BIAS, LN, BAYER, CAST, BUILDS and ANIMS byte-identical to main — ANIMS ' + ANIMS.length);
gate(CAST.every((p) => J(A6.propsOf(bOf(A6, p))) === J(B6.propsOf(bOf(B6, p)))), 'propsOf() byte-identical to main, all ten presets');
gate(CAST.every((p) => J(A7.bindMesh({ preset: p })) === J(B7.bindMesh({ preset: p }))), 'rig 7 bindMesh() byte-identical to main, all ten presets');

/* ================================================================================================ */
out(''); out('== 7. materials (<= ' + MAT_CAP + ' per COMPOSED preset)');
const MC = MAIN.CharacterFaceComposition, BC = M.CharacterFaceComposition;
const setOf = (faces) => [...new Set(faces.map((f) => f.mat))].sort();
let capOk = true, matSame = true; const mrow = [];
for (const p of CAST) {
  const base = setOf(B6.facesOf(B6.pose('idle', 0, bOf(B6, p), 'short', null, {}), bOf(B6, p))).length;
  const comp = setOf(BC.composed({ preset: p })), compMain = setOf(MC.composed({ preset: p }));
  if (comp.length > MAT_CAP) capOk = false; if (J(comp) !== J(compMain)) matSame = false;
  mrow.push(p + ' base ' + base + ' composed ' + comp.length);
}
gate(capOk, 'every composed preset <= ' + MAT_CAP + ' materials');
gate(matSame, 'composed material sets identical to main, all ten presets');
note(mrow.join(' | '));

/* ================================================================================================ */
out(''); out('== 8. job 4: child noses at ' + PPM + ' px/m; adults bit-identical');
const ageOf = (p) => bOf(B6, p).age || '-';
const CHILD = CAST.filter((p) => ageOf(p) === 'child'), ADULT = CAST.filter((p) => ageOf(p) !== 'child');
gate(J(CHILD) === J(['boy', 'girl']), 'age "child" is exactly boy and girl');
function gameFaces(R, build, faces, angle) {
  const Mt = R.makeMats(build).MATS, LN = R.LN, a = angle * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a);
  const e = ELEV * Math.PI / 180, se = Math.sin(e), ce = Math.cos(e); const mats = {}, res = [];
  faces.forEach((f, i) => { const key = 'face' + i; res.push(Object.assign({}, f, { mat: key })); const m = Mt[f.mat];
    if (!m) { mats[key] = { ramp: ['#ff00ff'], idx: 0 }; return; }
    const v = f.v.map(([x, y, z]) => [x * ca - y * sa, x * sa + y * ca, z]);
    const u = [0, 1, 2].map((k) => v[1][k] - v[0][k]), t = [0, 1, 2].map((k) => v[2][k] - v[0][k]);
    let n = [u[1] * t[2] - u[2] * t[1], u[2] * t[0] - u[0] * t[2], u[0] * t[1] - u[1] * t[0]]; const len = Math.hypot(n[0], n[1], n[2]) || 1; n = n.map((x) => x / len);
    const shade = (d) => d[0] * LN[0] + (-d[1] * se + d[2] * ce) * LN[1] + (d[1] * ce + d[2] * se) * LN[2];
    const b = f.b || 0; let sh = shade(n); if (sh < 0 && b <= -1) sh = shade(n.map((x) => -x)) * 0.9;
    const idx = Math.round(sh * R.GAIN + R.BIAS + b) + (m.off || 0);
    mats[key] = { ramp: m.ramp, idx: Math.max(0, Math.min(m.ramp.length - 1, idx)) }; });
  return { faces: res, mats };
}
const cell = Math.ceil(PPM * 0.76), OPTS = { w: cell, h: cell, cx: cell / 2 + 0.5, cy: cell / 2, scale: PPM, elev: ELEV, outline: true, mode: 'original' };
const headFaces = (ctx, key) => ctx.CharacterHeadStudy.createHead(ctx.CastViewerEngine.create(key).headBuild, [0, 0, 0]);
function nosePixels(ctx, key, dirs) {
  const ch = ctx.CastViewerEngine.create(key), faces = ctx.CharacterHeadStudy.createHead(ch.headBuild, [0, 0, 0]);
  let apex = null; for (const f of faces) for (const v of f.v) if (!apex || v[1] > apex[1]) apex = v;
  const near = (p, q) => Math.hypot(p[0] - q[0], p[1] - q[1], p[2] - q[2]) < 1e-12; const ni = [];
  faces.forEach((f, i) => { if (f.v.length === 3 && f.v.some((v) => near(v, apex))) ni.push(i); });
  const kept = faces.filter((f, i) => ni.indexOf(i) < 0);
  return dirs.map((angle) => { const W = gameFaces(ctx.CharacterIso6, ch.build, faces, angle), C = gameFaces(ctx.CharacterIso6, ch.build, kept, angle);
    const P = raster(W.faces, W.mats, Object.assign({}, OPTS, { angle })), Q = raster(C.faces, C.mats, Object.assign({}, OPTS, { angle }));
    let d = 0; for (let i = 0; i < P.pixels.length; i += 4) if (P.pixels[i] !== Q.pixels[i] || P.pixels[i + 1] !== Q.pixels[i + 1] || P.pixels[i + 2] !== Q.pixels[i + 2] || P.pixels[i + 3] !== Q.pixels[i + 3]) d++;
    return d; }); }
const GATE_DIRS = [0, 45, 90, 270, 315], ALL_DIRS = [0, 45, 90, 135, 180, 225, 270, 315];
for (const key of CHILD) { const before = nosePixels(MAIN, key, GATE_DIRS), after = nosePixels(M, key, GATE_DIRS);
  gate(after.every((n) => n > 0), key + ' resolves a nose at all 5 resolving headings — ' + after.join(',') + ' px (main ' + before.join(',') + ')'); }
let adultHead = true, adultNose = true, adultComposed = true, acAt = '';
for (const key of ADULT) {
  if (J(headFaces(MAIN, key)) !== J(headFaces(M, key))) adultHead = false;
  if (J(nosePixels(MAIN, key, ALL_DIRS)) !== J(nosePixels(M, key, ALL_DIRS))) adultNose = false;
  if (J(MC.composed({ preset: key })) !== J(BC.composed({ preset: key }))) { adultComposed = false; acAt = key; }
}
gate(adultHead, 'the eight adults\' study heads are byte-identical to main');
gate(adultNose, 'the eight adults paint identical nose pixels at all eight headings');
gate(adultComposed, 'the eight adults\' COMPOSED meshes (the bake\'s FaceLayerJs) are byte-identical to main' + (acAt ? ' — differs on ' + acAt : ''));
let childBody = true;
for (const key of CHILD) { if (J(MC.bodyFaces({ preset: key })) !== J(BC.bodyFaces({ preset: key }))) childBody = false; }
gate(childBody, 'boy and girl: every non-head composed face is byte-identical to main (the change is the head only)');
gate(CAST.every((k) => headFaces(MAIN, k).length === headFaces(M, k).length && J(setOf(headFaces(MAIN, k))) === J(setOf(headFaces(M, k)))),
  'head face count and head material set unchanged, all ten presets');
note('deckboss nose pixels (FINDING carried from job 4, not caused by it): main ' + nosePixels(MAIN, 'deckboss', ALL_DIRS).join(',') + ' merged ' + nosePixels(M, 'deckboss', ALL_DIRS).join(','));
rebase('job4 composition chain', 'rig 6 / rig 7 at main bytes', 'rig 6 / rig 7 at the merged bytes; adult composed meshes still byte-identical to main (gate above)');

/* ================================================================================================ */
out(''); out('== 9. the fence, on the keys the def stores (CharacterSkinPose.FrameIsHonest: |local pos| <= ' + FENCE_M + ' m)');
const keyMax = (c) => { let w = 0, wf = -1, wb = ''; c.tracks.forEach((t, k) => { for (const id of Object.keys(t.bones)) { const p = t.bones[id].pos, m = Math.hypot(p[0], p[1], p[2]); if (!(m <= w)) { w = m; wf = k; wb = id; } } }); return { w, wf, wb }; };
let fenceOk = true, fw = 0, fAt = '', mainBad = 0, mainRows = [];
for (const p of CAST) {
  const list = ANIMS.map((a) => [a, B7.clip(a, { preset: p })]).concat(NAMES.map((n) => [n, B7.carryClip(n, { preset: p })]));
  for (const [name, c] of list) { const r = keyMax(c); if (!(r.w <= FENCE_M) || !isFinite(r.w)) fenceOk = false; if (r.w > fw) { fw = r.w; fAt = p + ' ' + name + ' f' + r.wf + ' ' + r.wb; } }
  for (const a of FIXED) { const c = A7.clip(a, { preset: p }); c.tracks.forEach((t, k) => { for (const id of Object.keys(t.bones)) { const q = t.bones[id].pos; if (Math.hypot(q[0], q[1], q[2]) > FENCE_M) { mainBad++; break; } } }); }
}
gate(fenceOk, 'EVERY clip the def would carry (35 + 4 carry) x 10 presets x every frame: every local key within ' + FENCE_M + ' m; worst ' + fw.toFixed(3) + ' m (' + fAt + ')');
note('on MAIN, frames with a key past the fence across mount*/lift x 10 presets: ' + mainBad + ' (the defect job 3 fixes)');
{ const r = []; for (const a of ANIMS.concat(NAMES)) { const c = FIXED.indexOf(a) >= 0 || ANIMS.indexOf(a) >= 0 ? B7.clip(a, { preset: 'fisher' }) : B7.carryClip(a, { preset: 'fisher' }); r.push(a + ' ' + keyMax(c).w.toFixed(2)); }
  note('fisher max |local key| per clip (m): ' + r.join(', ')); }

out('');
out('GATES: ' + pass + ' passed, ' + fail + ' failed' + (fail ? '' : '  (all gates hold)') + '   [' + ((Date.now() - t0) / 1000).toFixed(1) + ' s]');
const text = lines.join('\n') + '\n';
if (arg('out')) fs.writeFileSync(arg('out'), text); else process.stdout.write(text);
process.exit(fail ? 1 : 0);
