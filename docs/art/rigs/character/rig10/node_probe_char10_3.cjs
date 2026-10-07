// Art desk v2: the probes behind the desk's kit 10.3 input, beside node_check_char10_3.cjs. Real Node 18 or later, built-ins
// only, READ-only on both kits; writes OUT/probe.txt. Usage: node node_probe_char10_3.cjs [K] [OUT], K102 in the environment.
// 1. the eye colours: which material draws each pixel of an eye colour, and whether any pixel is drawn in the 'iris' material;
// 2. bindMesh, 10.2 -> 10.3, on all 30: the fields that moved, by part and group; the look block's keys;
// 3. the rod's bend sockets in the eight fishing clips the README names, on all 30: against 10.2, and the worst frame step;
// 4. the mount clips' worst body-joint step, per build: the README's saddle bar and the three children;
// 5. reach f3 at NE past the cell's bottom on random seeds 26 and 50: what reaches it, the pixels cut, the kit's own cell check.
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm');
const K = process.argv[2] || 'C:/hh-gauntlet/art-desk/drops/character-v10.3-kit-2026-10-03/merged/character-v10.3-kit';
const OUT = process.argv[3] || 'C:/hh-gauntlet/art-desk/drops/character-v10.3-kit-2026-10-03/desk';
const K102 = process.env.K102 || 'C:/hh-gauntlet/art-desk/drops/character-v10.2-kit-2026-10-01/merged/character-v10.2-kit';
const LOG = []; const log = (...a) => { const s = a.join(' '); LOG.push(s); console.log(s); };
for (const f of ['characterIsoRig10.js', 'characterIsoRig10.poses.js', 'characterIsoRig10.checks.js']) vm.runInThisContext(fs.readFileSync(path.join(K, 'Art', f), 'utf8'), { filename: f });
const C = globalThis.CharacterIso10;
const ctx = vm.createContext({ console });
for (const f of ['characterIsoRig10.js', 'characterIsoRig10.poses.js']) vm.runInContext(fs.readFileSync(path.join(K102, 'Art', f), 'utf8'), ctx, { filename: f });
const N = ctx.CharacterIso10;
log('node ' + process.version + ' · CharacterIso10 ' + C.revision + ' against ' + N.revision + ' (' + K102 + ')');
function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
const hex = (r) => (i) => '#' + [r[i], r[i + 1], r[i + 2]].map(v => v.toString(16).padStart(2, '0')).join('');
// the angle between two rotations, well conditioned: 4 atan2(|q - s r|, |q + s r|), s the dot's sign (the double cover)
const ang = (q, r) => { const s = (q[0] * r[0] + q[1] * r[1] + q[2] * r[2] + q[3] * r[3]) < 0 ? -1 : 1; let m = 0, p = 0; for (let i = 0; i < 4; i++) { m += (q[i] - s * r[i]) ** 2; p += (q[i] + s * r[i]) ** 2; } return 4 * Math.atan2(Math.sqrt(m), Math.sqrt(p)) * 180 / Math.PI; };

// 1. the eye colours
{
  log('\n== 1. the eye colours, by the material that draws them');
  const EY = Object.entries(C.palettes.EYES).map(([n, h]) => [n, h.toLowerCase()]);
  const byMat = {}, irisPx = {}; let cells = 0, drawn = 0;
  for (const k of C.CAST) {
    const S = C.evalClip('idle', 0, C.buildOf(k)), f0 = S.I.face, list = [];
    for (let d = 0; d < 8; d++) list.push({ clip: 'idle', frame: 0, dir: d });
    for (let d = 2; d <= 6; d++) for (const e of C.BLINK.frames) list.push({ clip: 'idle', frame: 0, dir: d, face: { eyes: e, brows: f0.brows, mouth: f0.mouth } });
    for (const [y, p] of C.checkHelpers.LIMITS) list.push({ clip: 'idle', frame: 0, dir: 4, look: { yaw: y, pitch: p } });
    for (let f = 0; f < 8; f++) list.push({ clip: 'walk', frame: f, dir: 4 });
    for (const c of list) { const R = C.render(Object.assign({ build: k }, c)); cells++; const hx = hex(R.rgba);
      for (let i = 0; i < R.W * R.H; i++) { if (!R.rgba[i * 4 + 3]) continue; drawn++; const h = hx(i * 4), fi = R.face[i], F = fi >= 0 ? R.faces[fi] : null, mat = F ? F.mat : '?';
        if (mat === 'iris') irisPx[k] = (irisPx[k] || 0) + 1;
        for (const [n, c] of EY) if (h === c) { const key = n + ' ' + c + ' <- ' + mat + (F && F.group ? ' (' + F.group + ')' : ''); byMat[key] = (byMat[key] || 0) + 1; } } }
  }
  log('  ' + cells + ' renders of the 30 (idle at 8 facings, the blink states at E to W, the 8 look limits, walk at S), ' + drawn + ' drawn pixels');
  log('  pixels drawn in the iris material: ' + (Object.keys(irisPx).length ? JSON.stringify(irisPx) : 'none'));
  log('  pixels in an eye colour, by the material that drew them:');
  for (const [k, v] of Object.entries(byMat).sort((a, b) => b[1] - a[1])) log('    ' + k + ': ' + v);
  log('  INK_ROLES.iris: ' + C.INK_ROLES.iris);
  log('  DITHER.used ' + C.DITHER.used + ': ' + C.DITHER.note);
}

// 2. bindMesh, 10.2 -> 10.3, on all 30
{
  log('\n== 2. bindMesh, 10.2 -> 10.3, on all 30');
  const agg = {}, counts = [];
  for (const k of C.CAST) { const A = N.exportBuild(k).bindMesh, B = C.exportBuild(k).bindMesh;
    if (A.length !== B.length) { counts.push(k + ' ' + A.length + '->' + B.length); continue; }
    for (let i = 0; i < A.length; i++) for (const f of Object.keys(B[i])) { if (JSON.stringify(A[i][f]) === JSON.stringify(B[i][f])) continue;
      const key = f + ' on ' + B[i].part + '/' + B[i].mat + (B[i].group ? ' (' + B[i].group + ')' : ''), s = agg[key] = agg[key] || { builds: new Set(), faces: 0, ex: '' };
      s.builds.add(k); s.faces++; if (!s.ex) s.ex = JSON.stringify(A[i][f]) + ' -> ' + JSON.stringify(B[i][f]); } }
  log('  face counts that changed (their faces not compared): ' + (counts.join(', ') || 'none'));
  for (const [k, s] of Object.entries(agg).sort((p, q) => q[1].faces - p[1].faces)) log('  ' + k + ': ' + s.builds.size + ' builds, ' + s.faces + ' faces, e.g. ' + s.ex);
  const la = Object.keys(N.exportBuild('fisher').look), lb = Object.keys(C.exportBuild('fisher').look);
  log('  the look block: only in 10.3 ' + lb.filter(x => !la.includes(x)).join(',') + '; only in 10.2 ' + (la.filter(x => !lb.includes(x)).join(',') || 'none'));
}

// 3. the rod's bend sockets
{
  log('\n== 3. the rod\'s bend sockets (tool_L_1, tool_L_2, tool_R_1, tool_R_2), on all 30');
  const SOCK = ['tool_L_1', 'tool_L_2', 'tool_R_1', 'tool_R_2'];
  for (const name of ['hold', 'cast', 'castBack', 'castRelease', 'bite', 'strike', 'reel', 'land']) { let dv = 0, s2 = 0, s3 = 0; const moved = new Set();
    for (const k of C.CAST) { const x = N.exportBuild(k).clips.find(c => c.name === name), y = C.exportBuild(k).clips.find(c => c.name === name);
      for (let i = 0; i < y.tracks.length; i++) { for (const bn of SOCK) { const d = ang(x.tracks[i].bones[bn].rot, y.tracks[i].bones[bn].rot); if (d > 0.01) moved.add(k); dv = Math.max(dv, d); }
        if (i + 1 < y.tracks.length) for (const bn of SOCK) { s2 = Math.max(s2, ang(x.tracks[i].bones[bn].rot, x.tracks[i + 1].bones[bn].rot)); s3 = Math.max(s3, ang(y.tracks[i].bones[bn].rot, y.tracks[i + 1].bones[bn].rot)); } } }
    log('  ' + name + ': moved from 10.2 on ' + moved.size + ' builds, by at most ' + dv.toFixed(2) + ' deg; the worst step between frames 10.2 ' + s2.toFixed(1) + ', 10.3 ' + s3.toFixed(1)); }
}

// 4. the mount clips
{
  log('\n== 4. the mount clips\' worst body-joint step, per build (the README: the saddle mounts 120.7 at most, the bench 141.6)');
  const BODY = /^(pelvis|spine|chest|back|neck|head|shoulder|elbow|hand|hip|knee|foot|toe)/, rows = [];
  for (const k of C.CAST) { const e = C.exportBuild(k), age = C.buildOf(k).b.age;
    for (const name of ['mountUp', 'mountDown', 'mountCab', 'mountCabDown']) { const c = e.clips.find(x => x.name === name); if (!c) continue; let w = { d: 0 };
      for (let i = 0; i + 1 < c.tracks.length; i++) for (const bn of c.bones) { if (!BODY.test(bn)) continue; const d = ang(c.tracks[i].bones[bn].rot, c.tracks[i + 1].bones[bn].rot); if (d > w.d) w = { d, bn, i }; }
      rows.push({ k, age, name, d: w.d, at: 'f' + w.i + '>' + (w.i + 1) + ' ' + w.bn }); } }
  for (const name of ['mountUp', 'mountDown', 'mountCab', 'mountCabDown']) { const r = rows.filter(x => x.name === name).sort((a, b) => b.d - a.d), grown = r.filter(x => x.age !== 'child');
    log('  ' + name + ': the children ' + r.filter(x => x.age === 'child').map(x => x.k + ' ' + x.d.toFixed(1) + ' (' + x.at + ')').join(', ') + '; the rest at most ' + grown[0].d.toFixed(1) + ' (' + grown[0].k + ' ' + grown[0].at + ')'); }
  const g = JSON.parse(fs.readFileSync(path.join(K, 'gameplay', 'characterIsoRig10.girl.gameplay.json'), 'utf8')).clips;
  log('  the girl\'s sidecar carries ' + Object.keys(g).filter(n => /^mount/.test(n)).map(n => n + ' (' + g[n].mount + ')').join(', '));
}

// 5. the reach clip past the bottom on two random builds
{
  log('\n== 5. reach f3 at NE on random seeds 26 and 50 (unclipped, the desk\'s section L: 0.89 and 0.83 px past the bottom)');
  const cams = [0, 1, 2, 3, 4, 5, 6, 7].map(d => C.camOf({ dir: d }));
  const ext = (B, n, k, d) => { const cd = C.clipDef(n), faces = C.posed(C.evalClip(n, C.uOf(cd.anim, k), B), B); let y1 = -1e9; for (const f of faces) for (const p of f.v) { const q = C.proj(p, cams[d]); if (q.sy > y1) y1 = q.sy; } return y1 - C.H; };
  for (const s of [26, 50]) { const b = C.randomBuild(mulberry32(s)), B = C.buildOf(b);
    let w = { o: -99 }; for (const n of C.clipNames()) { const cd = C.clipDef(n); if (/^mount/.test(cd.anim)) continue; for (let k = 0; k < C.ANIMS[cd.anim].frames; k++) for (let d = 0; d < 8; d++) { const o = ext(B, n, k, d); if (o > w.o) w = { o, n, k, d }; } }
    const fs2 = C.posed(C.evalClip(w.n, C.uOf(C.clipDef(w.n).anim, w.k), B), B), parts = {};
    for (const f of fs2) for (const p of f.v) if (C.proj(p, cams[w.d]).sy > C.H - 1) { parts[f.part + '/' + f.mat] = 1; break; }
    const R = C.render({ build: b, clip: w.n, frame: w.k, dir: w.d, heave: 4 }); let cut = 0; for (let y = R.H - 4; y < R.H; y++) for (let x = 0; x < R.W; x++) if (R.rgba[(y * R.W + x) * 4 + 3] > 0) cut++;
    const cell = C.CHECKS.find(c => c.id === 'cell'), it = cell.run(C.buildOf(b)); let r = it.next(); while (!r.done) r = it.next();
    log('  seed ' + s + ' (' + b.sex + ' ' + b.frame + ' ' + b.age + ', ' + b.garment + '): ' + w.n + ' f' + w.k + ' ' + C.order[w.d] + ', ' + w.o.toFixed(2) + ' px past the bottom; reaching the bottom row: ' + Object.keys(parts).join(', ') + '; pixels cut ' + cut + ' (drawn 4 px higher); the kit\'s cell check: ' + (r.value.pass ? 'PASS ' : 'FAIL ') + r.value.value + ' — ' + String(r.value.detail).slice(0, 260));
  }
}
fs.writeFileSync(path.join(OUT, 'probe.txt'), LOG.join('\n') + '\n');
