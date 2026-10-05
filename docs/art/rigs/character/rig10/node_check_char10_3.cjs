// Art desk v2: CD's character kit 10.3 on real Node (built-ins only). READ-only on the kit; writes only into OUT.
// The oracle is the rig itself, run here, and none of the kit's tools/kit.js: the golden checks, the writers (exportBuild,
// gameplay, the data files, presets), every render by pixel and by its manifest rgbaSha256, randomBuild by CD's seeds, the
// README's cast table, and the 10-03 send-back's points: the steps between frames (7), the cell unclipped (8), the random
// classes (10), the irises (6), the aim bar (section 4), the frozen copies, and what moved from 10.2 (the staged 10.2 kit).
// Ends with one RESULT: line: PASS means the kit reproduces on real Node; claims it does not bear out are reported, not failed.
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm'), zlib = require('zlib'), crypto = require('crypto');
const K = process.argv[2] || 'C:/hh-gauntlet/art-desk/drops/character-v10.3-kit-2026-10-03/merged/character-v10.3-kit';
const OUT = process.argv[3] || 'C:/hh-gauntlet/art-desk/drops/character-v10.3-kit-2026-10-03/desk';
const K102 = process.env.K102 || 'C:/hh-gauntlet/art-desk/drops/character-v10.2-kit-2026-10-01/merged/character-v10.2-kit';
const ONLY = (process.env.ONLY || '').split(',').filter(Boolean);
const want = (s) => !ONLY.length || ONLY.includes(s);
fs.mkdirSync(OUT, { recursive: true });
const LOG = [];
const log = (...a) => { const s = a.join(' '); LOG.push(s); console.log(s); };
const SUM = {};
const FAIL = [];
const fail = (s) => { FAIL.push(s); log('  FAIL ' + s); };

const sha = (p) => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const shaB = (b) => crypto.createHash('sha256').update(b).digest('hex');
const rd = (p) => JSON.parse(fs.readFileSync(path.join(K, p), 'utf8'));
const ART = ['characterIsoRig10.js', 'characterIsoRig10.poses.js', 'characterIsoRig10.checks.js'];
for (const f of ART) vm.runInThisContext(fs.readFileSync(path.join(K, 'Art', f), 'utf8'), { filename: f });
const C = globalThis.CharacterIso10;
const RIG = sha(path.join(K, 'Art', ART[0])), POSES = sha(path.join(K, 'Art', ART[1])), CHECKS = sha(path.join(K, 'Art', ART[2]));
log('node ' + process.version + ' · ' + C.rig + ' ' + C.revision + ' · rig ' + RIG.slice(0, 8) + ' poses ' + POSES.slice(0, 8) + ' checks ' + CHECKS.slice(0, 8));
const KEYS = C.CAST.slice();
const REV = '10.3';
if (C.revision !== REV) fail('revision ' + C.revision);

// ---------- a deep diff: counts numbers equal, within tol, and different; keeps the first few paths ----------
function ddiff(a, b, tol) {
  const st = { nums: 0, eq: 0, tol: 0, n: 0, maxD: 0, first: [] };
  const push = (p, x, y) => { st.n++; if (st.first.length < 6) st.first.push(p + ': ' + short(x) + ' | ' + short(y)); };
  (function go(x, y, p) {
    if (typeof x === 'number' && typeof y === 'number') {
      st.nums++; if (x === y) { st.eq++; return; }
      const d = Math.abs(x - y); if (d > st.maxD) st.maxD = d;
      if (d <= tol) { st.tol++; return; } push(p, x, y); return;
    }
    if (x === null || y === null || typeof x !== 'object' || typeof y !== 'object') { if (x !== y) push(p, x, y); return; }
    if (Array.isArray(x) !== Array.isArray(y)) { push(p, 'array?', 'array?'); return; }
    if (Array.isArray(x)) {
      if (x.length !== y.length) push(p + '.length', x.length, y.length);
      for (let i = 0; i < Math.min(x.length, y.length); i++) go(x[i], y[i], p + '[' + i + ']');
      return;
    }
    for (const k of new Set([...Object.keys(x), ...Object.keys(y)])) {
      if (!(k in x)) { push(p + '.' + k, '(absent)', y[k]); continue; }
      if (!(k in y)) { push(p + '.' + k, x[k], '(absent)'); continue; }
      go(x[k], y[k], p + '.' + k);
    }
  })(a, b, '$');
  return st;
}
function short(v) { const s = JSON.stringify(v); return s === undefined ? String(v) : s.length > 90 ? s.slice(0, 90) + '…' : s; }
const r7a = (k, v) => typeof v === 'number' ? +v.toFixed(7) : v;
const firstDiff = (a, b) => { const n = Math.min(a.length, b.length); for (let i = 0; i < n; i++) if (a[i] !== b[i]) return i; return n; };
const around = (s, i) => JSON.stringify(s.slice(Math.max(0, i - 30), i + 30));

// ---------- PNG, 8-bit non-interlaced, any colour type ----------
function readPng(file) {
  const buf = fs.readFileSync(file); let off = 8, W, H, bd, ct, il; const idat = []; let plte = null, trns = null;
  while (off < buf.length) {
    const len = buf.readUInt32BE(off), type = buf.toString('latin1', off + 4, off + 8), data = buf.subarray(off + 8, off + 8 + len);
    if (type === 'IHDR') { W = data.readUInt32BE(0); H = data.readUInt32BE(4); bd = data[8]; ct = data[9]; il = data[12]; }
    else if (type === 'PLTE') plte = data; else if (type === 'tRNS') trns = data; else if (type === 'IDAT') idat.push(data); else if (type === 'IEND') break;
    off += 12 + len;
  }
  if (bd !== 8 || il !== 0) throw new Error(file + ': bit depth ' + bd + ' interlace ' + il);
  const ch = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 }[ct], stride = W * ch, raw = zlib.inflateSync(Buffer.concat(idat));
  const px = Buffer.alloc(W * H * ch); let prev = Buffer.alloc(stride);
  for (let y = 0; y < H; y++) {
    const ft = raw[y * (stride + 1)], line = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1)), cur = px.subarray(y * stride, (y + 1) * stride);
    for (let x = 0; x < stride; x++) {
      const a = x >= ch ? cur[x - ch] : 0, b = prev[x], c = x >= ch ? prev[x - ch] : 0; let v = line[x];
      if (ft === 1) v += a; else if (ft === 2) v += b; else if (ft === 3) v += (a + b) >> 1;
      else if (ft === 4) { const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c); v += (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c); }
      else if (ft !== 0) throw new Error(file + ': filter ' + ft);
      cur[x] = v & 255;
    }
    prev = cur;
  }
  const rgba = new Uint8Array(W * H * 4);
  for (let i = 0; i < W * H; i++) {
    let r, g, b2, a2 = 255;
    if (ct === 6) { r = px[i * 4]; g = px[i * 4 + 1]; b2 = px[i * 4 + 2]; a2 = px[i * 4 + 3]; }
    else if (ct === 2) { r = px[i * 3]; g = px[i * 3 + 1]; b2 = px[i * 3 + 2]; }
    else if (ct === 0) { r = g = b2 = px[i]; } else if (ct === 4) { r = g = b2 = px[i * 2]; a2 = px[i * 2 + 1]; }
    else { const q = px[i]; r = plte[q * 3]; g = plte[q * 3 + 1]; b2 = plte[q * 3 + 2]; a2 = trns && q < trns.length ? trns[q] : 255; }
    rgba[i * 4] = r; rgba[i * 4 + 1] = g; rgba[i * 4 + 2] = b2; rgba[i * 4 + 3] = a2;
  }
  return { W, H, ct, rgba };
}
const canon = (rgba) => { const o = new Uint8Array(rgba); for (let i = 0; i < o.length; i += 4) if (!o[i + 3]) o[i] = o[i + 1] = o[i + 2] = 0; return o; };
function pxDiff(a, b) { let n = 0, maxC = 0; for (let i = 0; i < a.length; i += 4) { let d = 0; for (let c = 0; c < 4; c++) d = Math.max(d, Math.abs(a[i + c] - b[i + c])); if (d) { n++; if (d > maxC) maxC = d; } } return { n, maxC }; }
const nn = (img, W, H, s) => { const o = new Uint8Array(W * s * H * s * 4); for (let y = 0; y < H * s; y++) for (let x = 0; x < W * s; x++) { const i = ((y / s | 0) * W + (x / s | 0)) * 4, j = (y * W * s + x) * 4; o[j] = img[i]; o[j + 1] = img[i + 1]; o[j + 2] = img[i + 2]; o[j + 3] = img[i + 3]; } return o; };
function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
const hex = (c) => '#' + [c[0], c[1], c[2]].map(v => v.toString(16).padStart(2, '0')).join('');
// the rig's matrices are column-major (kit.js and the checks read R[0], R[3], R[6] as the first row)
const mV = (R, v) => [R[0] * v[0] + R[3] * v[1] + R[6] * v[2], R[1] * v[0] + R[4] * v[1] + R[7] * v[2], R[2] * v[0] + R[5] * v[1] + R[8] * v[2]];
// the angle between two rotations, well conditioned: 4 atan2(|q - s r|, |q + s r|), s the dot's sign (the double cover). The acos of the dot
// reads up to 0.1 deg between a quaternion and itself once both are rounded to 7 decimals (their norms fall below 1), so it is not used
const ang = (q, r) => { const s = (q[0] * r[0] + q[1] * r[1] + q[2] * r[2] + q[3] * r[3]) < 0 ? -1 : 1; let m = 0, p = 0; for (let i = 0; i < 4; i++) { m += (q[i] - s * r[i]) ** 2; p += (q[i] + s * r[i]) ** 2; } return 4 * Math.atan2(Math.sqrt(m), Math.sqrt(p)) * 180 / Math.PI; };

// ---------- A. the stamps ----------
if (want('stamps')) {
  log('\n== A. stamps');
  const files = ['golden-report.json', 'builds/presets.json', 'renders/manifest.json', 'data/options.v10.json', 'data/shading.v10.json'].concat(KEYS.map(k => 'builds/' + k + '.v10.json'), KEYS.map(k => 'gameplay/characterIsoRig10.' + k + '.gameplay.json'));
  let bad = 0;
  for (const f of files) {
    const j = rd(f);
    if (j.derivedFromRigSha256 !== RIG) { bad++; fail('stamp ' + f + ' rig ' + j.derivedFromRigSha256); }
    if (j.posesDerivedFromRigSha256 !== undefined && j.posesDerivedFromRigSha256 !== POSES) { bad++; fail('stamp ' + f + ' poses'); }
    if (j.checksSha256 !== undefined && j.checksSha256 !== CHECKS) { bad++; fail('stamp ' + f + ' checks'); }
    if (j.revision !== undefined && j.revision !== REV) { bad++; fail('stamp ' + f + ' revision ' + j.revision); }
  }
  log('  ' + files.length + ' stamped files, ' + bad + ' bad stamps');
  SUM.stamps = { files: files.length, bad };
}

// ---------- B. the golden checks ----------
let MINE = {};
if (want('golden') || want('claims')) {
  log('\n== B. golden checks, every CAST build, run to the end on real Node');
  const GR = rd('golden-report.json'); let rowsEq = 0, rowsN = 0, gated = 0, gatedPass = 0; const failures = [], diffs = [], byId = {};
  const t0 = Date.now();
  for (const k of KEYS) {
    const rows = C.runChecks(k); MINE[k] = rows; const sh = GR.builds[k];
    if (!sh) { fail('golden: no shipped build ' + k); continue; }
    const g = rows.filter(r => r.gate), gp = g.filter(r => r.pass);
    gated += g.length; gatedPass += gp.length;
    for (const r of g) if (!r.pass) failures.push(k + ' · ' + r.id + ' · ' + r.value);
    if (sh.gated !== gp.length + ' / ' + g.length) diffs.push(k + ' gated ' + sh.gated + ' vs ' + gp.length + ' / ' + g.length);
    const sz = C.sizes(k), dz = ddiff(JSON.parse(JSON.stringify(sz)), sh.sizes, 0);
    if (dz.n) diffs.push(k + ' sizes: ' + dz.first.join('; '));
    for (let i = 0; i < Math.max(rows.length, sh.checks.length); i++) {
      const a = rows[i], b = sh.checks[i]; rowsN++;
      if (!a || !b) { diffs.push(k + ' row ' + i + ' missing'); continue; }
      const mine = { id: a.id, title: a.title, gate: a.gate, pass: a.pass, value: a.value, detail: a.detail };
      const d = ddiff(JSON.parse(JSON.stringify(mine)), b, 0);
      if (d.n) {
        const f = ['title', 'value', 'detail'].find(x => String(mine[x]) !== String(b[x]));
        const at = f ? firstDiff(String(b[f]), String(mine[f])) : 0;
        (byId[a.id] = byId[a.id] || { builds: 0, field: {}, ex: '' }).builds++; if (f) byId[a.id].field[f] = (byId[a.id].field[f] || 0) + 1;
        if (!byId[a.id].ex && f) byId[a.id].ex = k + ' ' + f + ': kit ' + around(String(b[f]), at) + ' | node ' + around(String(mine[f]), at);
        diffs.push(k + ' ' + a.id);
      } else rowsEq++;
    }
  }
  log('  ' + KEYS.length + ' builds in ' + ((Date.now() - t0) / 1000).toFixed(1) + ' s; gated ' + gatedPass + ' / ' + gated + ' (kit: ' + GR.summary.gatedPass + ' / ' + GR.summary.gated + ')');
  log('  failures: ' + (failures.join(' | ') || 'none') + '   kit: ' + (GR.summary.failures.join(' | ') || 'none'));
  log('  rows equal to the kit\'s: ' + rowsEq + ' / ' + rowsN);
  for (const [id, v] of Object.entries(byId)) log('  differs: ' + id + ' on ' + v.builds + ' builds (' + JSON.stringify(v.field) + '); e.g. ' + v.ex.slice(0, 300));
  const sizeDiffs = diffs.filter(d => / sizes: /.test(d));
  log('  sizes differ on ' + sizeDiffs.length + ' builds' + (sizeDiffs.length ? ': ' + sizeDiffs.slice(0, 3).join(' | ') : ''));
  if (failures.length) fail('golden gates fail on node: ' + failures.join(' | '));
  SUM.golden = { builds: KEYS.length, gated, gatedPass, failures, rowsEq, rowsN, byId, sizeDiffs };
}

// ---------- C/D. the writers: exportBuild and gameplay ----------
if (want('exports')) {
  log('\n== C. builds/<key>.v10.json against exportBuild(key), printed with toFixed(7)');
  let byteA = 0, byteR = 0, structEq = 0, tolEq = 0, worst = 0; const notes = [];
  for (const k of KEYS) {
    const f = 'builds/' + k + '.v10.json', txt = fs.readFileSync(path.join(K, f), 'utf8'), sh = JSON.parse(txt), E = C.exportBuild(k);
    const wrap = { schema: sh.schema, rig: 'characterIsoRig10', exportSymbol: 'CharacterIso10', derivedFromRigSha256: RIG, posesDerivedFromRigSha256: POSES, revision: REV, authoring: sh.authoring };
    const oA = JSON.stringify(Object.assign({}, wrap, E), r7a), oR = C.R7 ? JSON.stringify(Object.assign({}, wrap, E), C.R7) : '';
    if (oA === txt) byteA++; if (oR === txt) byteR++;
    const d = ddiff(JSON.parse(oA), sh, 0), dt = ddiff(JSON.parse(oA), sh, 1.01e-7);
    if (!d.n) structEq++; if (!dt.n) tolEq++; worst = Math.max(worst, d.maxD);
    if (d.n) notes.push(k + ': ' + d.n + ' of ' + d.nums + ' numbers differ (max ' + d.maxD.toExponential(2) + '), ' + dt.n + ' beyond one step of the 7th decimal; ' + d.first.slice(0, 2).join('; '));
  }
  log('  byte for byte: my toFixed(7) ' + byteA + ' / ' + KEYS.length + ', the rig\'s R7 ' + byteR + ' / ' + KEYS.length + '; equal as numbers ' + structEq + ' / ' + KEYS.length + ', within one 7th-decimal step ' + tolEq + ' / ' + KEYS.length + ', worst ' + worst.toExponential(2));
  for (const n of notes.slice(0, 12)) log('  ' + n);
  if (tolEq !== KEYS.length) fail('exports differ beyond one step of the 7th decimal on ' + (KEYS.length - tolEq));
  SUM.exports = { byteToFixed: byteA, byteR7: byteR, structEq, tolEq, n: KEYS.length, worst, notes: notes.slice(0, 30) };

  log('\n== D. gameplay/<key>.gameplay.json against gameplay(key)');
  let gb = 0, gs = 0, gt = 0; const gn = [];
  for (const k of KEYS) {
    const f = 'gameplay/characterIsoRig10.' + k + '.gameplay.json', txt = fs.readFileSync(path.join(K, f), 'utf8'), sh = JSON.parse(txt), G = C.gameplay(k);
    const o7 = JSON.stringify(Object.assign({ derivedFromRigSha256: RIG, posesDerivedFromRigSha256: POSES }, G), r7a);
    if (o7 === txt) gb++;
    const d = ddiff(JSON.parse(o7), sh, 0), dt = ddiff(JSON.parse(o7), sh, 1.01e-4); if (!d.n) gs++; if (!dt.n) gt++;
    if (d.n) gn.push(k + ': ' + d.n + ' differ (max ' + d.maxD.toExponential(2) + '); ' + d.first.slice(0, 3).join('; '));
  }
  log('  byte for byte (toFixed(7)): ' + gb + ' / ' + KEYS.length + '; equal as data ' + gs + ' / ' + KEYS.length + '; within one 4th-decimal step ' + gt + ' / ' + KEYS.length);
  for (const n of gn.slice(0, 12)) log('  ' + n);
  if (gt !== KEYS.length) fail('sidecars differ beyond one 4th-decimal step on ' + (KEYS.length - gt));
  SUM.gameplay = { byte: gb, structEq: gs, tolEq: gt, n: KEYS.length, notes: gn.slice(0, 30) };
}

// ---------- E. the data files and presets ----------
if (want('data')) {
  log('\n== E. data/options.v10.json, data/shading.v10.json, builds/presets.json');
  const O = rd('data/options.v10.json'), S = rd('data/shading.v10.json'), P = rd('builds/presets.json');
  const grouped = Object.fromEntries(Object.entries(JSON.parse(JSON.stringify(C.GARMENTS))).map(([k, g]) => [k, Object.assign({}, g, { group: g.swim ? 'swimwear' : g.top ? 'top' : 'outfit' })]));
  const map = { options: C.OPTIONS, fields: C.FIELDS, garments: grouped, bottoms: C.BOTTOMS, beards: C.BEARDS, hats: C.HATS, sex: C.SEX, frames: C.FRAMES, ages: C.AGES, heads: C.HEADS, eyeShapes: Object.keys(C.EYE_SHAPES), palettes: C.palettes };
  const od = {};
  for (const [k, v] of Object.entries(map)) {
    if (!(k in O)) { od[k] = 'absent in the file'; log('  options.' + k + ': absent'); continue; }
    const d = ddiff(JSON.parse(JSON.stringify(v)), O[k], 0); od[k] = d.n ? d.n + ' differ: ' + d.first.slice(0, 3).join('; ') : 'equal (' + d.nums + ' numbers)';
    if (d.n) log('  options.' + k + ': ' + od[k]);
  }
  log('  options: ' + Object.values(od).filter(v => /^equal/.test(v)).length + ' / ' + Object.keys(od).length + ' tables equal the rig\'s');
  log('  options.randomBuild rules: ' + short((O.randomBuild || {}).rules));
  const sc = C.shadingContract('fisher'), ds = ddiff(JSON.parse(JSON.stringify(sc)), Object.fromEntries(Object.keys(sc).map(k => [k, S[k]])), 0);
  const extra = { dither: C.DITHER, tolerance: C.TOL, ink: { colours: C.INK, roles: C.INK_ROLES, eyeWhite: C.EYE_WHITE }, aim: C.AIM };
  const dx = ddiff(JSON.parse(JSON.stringify(extra)), { dither: S.dither, tolerance: S.tolerance, ink: S.ink, aim: S.aim }, 0);
  log('  shading.v10.json vs shadingContract(fisher): ' + (ds.n ? ds.n + ' differ (max ' + ds.maxD.toExponential(2) + '): ' + ds.first.join('; ') : 'equal (' + ds.nums + ' numbers)') + '; dither, tolerance, ink, aim vs DITHER, TOL, INK, AIM: ' + (dx.n ? dx.n + ' differ: ' + dx.first.join('; ') : 'equal'));
  if (ds.n && ds.maxD > 1e-12) fail('shading.v10.json differs from shadingContract beyond float noise');
  let pe = 0; const pn = [];
  for (const k of KEYS) {
    const b = C.normBuild(k), s = P.builds[k], mine = { label: b.label, set: C.CAST10.includes(k) ? 'core' : 'npc', key: C.buildKey(b), build: b };
    const d = ddiff(JSON.parse(JSON.stringify(mine)), { label: s.label, set: s.set, key: s.key, build: s.build }, 0); if (d.n) pn.push(k + ': ' + d.first.join('; ')); else pe++;
  }
  const lists = ddiff({ cast: C.CAST, core: C.CAST10, npcs: C.NPCS, fields: C.FIELDS }, { cast: P.cast, core: P.core, npcs: P.npcs, fields: P.fields }, 0);
  log('  presets: ' + pe + ' / ' + KEYS.length + ' builds equal normBuild + buildKey; the lists ' + (lists.n ? 'DIFFER ' + lists.first.join('; ') : 'equal'));
  for (const n of pn.slice(0, 8)) log('  ' + n);
  if (pe !== KEYS.length || lists.n) fail('presets.json differs from the rig');
  // the README: presets.json changes only in its stamps and revision
  const p102f = path.join(K102, 'builds/presets.json');
  let vs102 = 'no 10.2 kit';
  if (fs.existsSync(p102f)) { const strip = (j) => { const o = JSON.parse(JSON.stringify(j)); for (const f of ['derivedFromRigSha256', 'posesDerivedFromRigSha256', 'revision', 'checksSha256']) delete o[f]; return o; };
    const d = ddiff(strip(JSON.parse(fs.readFileSync(p102f, 'utf8'))), strip(P), 0); vs102 = d.n ? d.n + ' differ beyond the stamps: ' + d.first.join('; ') : 'the same but for the stamps and revision'; }
  log('  presets.json against 10.2\'s: ' + vs102);
  SUM.data = { options: od, shading: ds.n, shadingMax: ds.maxD, shadingExtra: dx.n, presetsEq: pe, presetsNotes: pn.slice(0, 20), lists: lists.n, presetsVs102: vs102 };
}

// ---------- F. the renders: every image by pixel, by its manifest rgbaSha256, and the irises ----------
if (want('renders')) {
  log('\n== F. every render against render() on real Node, laid out from the manifest\'s words (not kit.js), and its rgbaSha256');
  const M = rd('renders/manifest.json'), LIM = C.checkHelpers.LIMITS;
  const byFile = Object.fromEntries(M.images.map(e => [e.file, e]));
  const cells = (kind, k) => {
    if (kind === 'idle8') return [[0, 1, 2, 3, 4, 5, 6, 7].map(d => ({ clip: 'idle', frame: 0, dir: d }))];
    if (kind === 'walkS') return [[0, 1, 2, 3, 4, 5, 6, 7].map(f => ({ clip: 'walk', frame: f, dir: 4 }))];
    const S = C.evalClip('idle', 0, C.buildOf(k)), f0 = S.I.face;
    if (kind === 'blink') return [2, 3, 4, 5, 6].map(d => C.BLINK.frames.map(e => ({ clip: 'idle', frame: 0, dir: d, face: { eyes: e, brows: f0.brows, mouth: f0.mouth } })));
    const B = S.B, Wh = S.W[B.sk.ix.head], hm = mV(Wh.R, B.D.headMid), e0 = [Wh.p[0] + hm[0], Wh.p[1] + hm[1], Wh.p[2] + hm[2]];
    const row0 = LIM.map(([y, p]) => ({ clip: 'idle', frame: 0, dir: 4, look: { yaw: y, pitch: p }, face: { eyes: f0.eyes, brows: f0.brows, mouth: f0.mouth } }));
    const row1 = [[-50, 0], [-30, 0], [-15, 0], [0, 0], [15, 0], [30, 0], [50, 0], [0, -0.35]].map(([b, dz]) => {
      const T = [e0[0] + 2 * Math.sin(b * Math.PI / 180), e0[1] + 2 * Math.cos(b * Math.PI / 180), e0[2] + dz], L = C.lookAt(S, T);
      return { clip: 'idle', frame: 0, dir: 4, look: { yaw: L.yaw, pitch: L.pitch }, face: { eyes: f0.eyes === 'open' ? L.eyes : f0.eyes, brows: f0.brows, mouth: f0.mouth }, target: { bearing_deg: b, dz_m: dz } }; });
    return [row0, row1];
  };
  const grid = (k, rows) => { const cols = Math.max(...rows.map(r => r.length)), W = C.W * cols, H = C.H * rows.length, o = new Uint8Array(W * H * 4);
    rows.forEach((row, ri) => row.forEach((c, ci) => { const R = C.render(Object.assign({ build: k }, c)); if (R.W !== C.W || R.H !== C.H) throw new Error('cell ' + R.W + 'x' + R.H);
      for (let y = 0; y < C.H; y++) o.set(R.rgba.subarray(y * C.W * 4, (y + 1) * C.W * 4), ((ri * C.H + y) * W + ci * C.W) * 4); }));
    return { W, H, rgba: canon(o) }; };
  const IRIS = Object.entries((C.palettes && C.palettes.EYES) || {}).map(([n, h]) => [n, h.toLowerCase()]);
  const irisPx = {}; for (const [n] of IRIS) irisPx[n] = 0;
  const scanIris = (rgba) => { for (let i = 0; i < rgba.length; i += 4) { if (!rgba[i + 3]) continue; const h = hex([rgba[i], rgba[i + 1], rgba[i + 2]]); for (const [n, c] of IRIS) if (h === c) irisPx[n]++; } };
  let n = 0, eq1 = 0, eq4 = 0, hash1 = 0, hash4 = 0, turnsOk = 0, turnsN = 0; const notes = [];
  for (const k of KEYS) for (const kind of ['idle8', 'walkS', 'blink', 'look']) {
    const rows = cells(kind, k), g = grid(k, rows);
    for (const s of [1, 4]) { n++;
      const file = 'renders/' + s + 'x/' + k + '.' + kind + '.png', e = byFile[file], mine = s === 1 ? g.rgba : nn(g.rgba, g.W, g.H, 4), W = g.W * s, H = g.H * s;
      if (!e) { fail(file + ' not in the manifest'); continue; }
      const p = readPng(path.join(K, file));
      if (p.W !== W || p.H !== H) { fail(file + ' is ' + p.W + 'x' + p.H + ', mine ' + W + 'x' + H); continue; }
      const pc = canon(p.rgba), d = pxDiff(mine, pc);
      if (!d.n) { if (s === 1) eq1++; else eq4++; } else notes.push(file + ': ' + d.n + ' px differ (max ' + d.maxC + ')');
      const hm = shaB(Buffer.from(mine)), hf = shaB(Buffer.from(pc));
      if (hm === e.rgbaSha256 && hf === e.rgbaSha256) { if (s === 1) hash1++; else hash4++; } else notes.push(file + ': rgbaSha256 mine ' + hm.slice(0, 8) + ', the file\'s ' + hf.slice(0, 8) + ', the manifest ' + String(e.rgbaSha256).slice(0, 8));
      if (s === 1) scanIris(pc);
    }
    if (kind === 'look') { const e = byFile['renders/1x/' + k + '.look.png']; if (e && e.turns) rows.forEach((row, ri) => row.forEach((c, ci) => { turnsN++; const t = e.turns[ri] && e.turns[ri][ci];
      if (t && Math.abs(t.look.yaw - c.look.yaw) < 1e-3 && Math.abs(t.look.pitch - c.look.pitch) < 1e-3 && t.eyes === c.face.eyes) turnsOk++; else if (notes.length < 60) notes.push(k + ' look turn ' + ri + '/' + ci + ': mine ' + JSON.stringify([c.look, c.face.eyes]) + ' manifest ' + JSON.stringify(t)); })); }
  }
  // cast.png: every CAST build in CAST order, ten to a row, each at S (dir 4) then SE (dir 3), idle f0, x2
  const crow = []; for (let i = 0; i < KEYS.length; i += 10) crow.push(KEYS.slice(i, i + 10));
  const cw = C.W * 2, chh = C.H * 2, CW = cw * 20, CH = chh * crow.length, cast = new Uint8Array(CW * CH * 4);
  crow.forEach((keys, ri) => keys.forEach((k, bi) => [4, 3].forEach((d, di) => { const R = C.render({ build: k, clip: 'idle', frame: 0, dir: d });
    for (let y = 0; y < chh; y++) for (let x = 0; x < cw; x++) { const a = ((y >> 1) * C.W + (x >> 1)) * 4, o = ((ri * chh + y) * CW + (bi * 2 + di) * cw + x) * 4; cast[o] = R.rgba[a]; cast[o + 1] = R.rgba[a + 1]; cast[o + 2] = R.rgba[a + 2]; cast[o + 3] = R.rgba[a + 3]; } })));
  const cp = readPng(path.join(K, 'renders/cast.png')), cc = canon(cast), cd = cp.W === CW && cp.H === CH ? pxDiff(cc, canon(cp.rgba)) : { n: -1 }, ce = byFile['renders/cast.png'];
  const castHash = ce && shaB(Buffer.from(cc)) === ce.rgbaSha256;
  log('  ' + n + ' strips (30 x 4 kinds x 1x, 4x): 1x equal to render() ' + eq1 + ' / ' + n / 2 + ', 4x ' + eq4 + ' / ' + n / 2 + '; rgbaSha256 as the manifest 1x ' + hash1 + ', 4x ' + hash4);
  log('  look turns (lookAt to the manifest\'s targets, the eyes): ' + turnsOk + ' / ' + turnsN + ' as the manifest lists them');
  log('  cast.png ' + cp.W + 'x' + cp.H + ' (mine ' + CW + 'x' + CH + '): ' + (cd.n === 0 ? 'equal' : cd.n + ' px differ') + '; rgbaSha256 ' + (castHash ? 'as the manifest' : 'NOT as the manifest') + '; transparent pixels ' + (() => { let t = 0; for (let i = 3; i < cp.rgba.length; i += 4) if (!cp.rgba[i]) t++; return t; })());
  log('  manifest images: ' + M.images.length + ', every one with an rgbaSha256: ' + M.images.every(e => /^[0-9a-f]{64}$/.test(e.rgbaSha256)));
  log('  the eye colours (palettes.EYES) drawn in the 120 1x strips: ' + IRIS.map(([nm, c]) => nm + ' ' + c + ' ' + irisPx[nm] + ' px').join(', '));
  for (const s of notes.slice(0, 20)) log('  ' + s);
  if (eq1 + eq4 !== n || hash1 + hash4 !== n || cd.n !== 0 || !castHash) fail('renders: ' + (n - eq1 - eq4) + ' strips differ by pixel, ' + (n - hash1 - hash4) + ' by hash, cast ' + cd.n);
  SUM.renders = { strips: n, eq1, eq4, hash1, hash4, turnsOk, turnsN, cast: { size: [cp.W, cp.H], diff: cd.n, hash: castHash }, irisPx, notes: notes.slice(0, 40) };
}

// ---------- G. randomBuild by CD's seeds: randomBuild(mulberry32(seed)) ----------
const runSome = (b, ids) => { const B = C.buildOf(b), out = []; for (const c of C.CHECKS) { if (ids && !ids.includes(c.id)) continue; const it = c.run(B); let r = it.next(); while (!r.done) r = it.next(); out.push(Object.assign({ id: c.id }, r.value, { gate: c.gate !== false })); } return out; };
if (want('random')) {
  log('\n== G. randomBuild: the rules by 120,000 seeded draws; the gates by CD\'s seeds');
  const N = +(process.env.RN || 120000), rnd = mulberry32(20261003), T = {};
  const inc = (k) => { T[k] = (T[k] || 0) + 1; };
  const grey = { salt: 1, grey: 1, white: 1 }; let manFem = 0, manFemBottom = 0, notNorm = 0, swim = 0;
  for (let i = 0; i < N; i++) {
    const b = C.randomBuild(rnd), G = C.GARMENTS[b.garment], Bt = C.BOTTOMS[b.bottom];
    inc('sex.' + b.sex); inc('age.' + b.age); if (G.swim) swim++;
    if (b.sex === 'm' && G.fem) manFem++; if (b.sex === 'm' && Bt.fem) manFemBottom++;
    if (b.beard !== 'none') inc('beard.' + b.sex + '.' + b.age);
    inc('n.' + b.sex + '.' + b.age); if (grey[b.hair]) inc('greyhair.' + b.age); if (b.hairStyle === 'bald') inc('bald.' + b.sex + '.' + b.age);
    if (b.hat !== 'none') inc('hat');
    const nb = C.normBuild(b); if (C.FIELDS.some(f => nb[f] !== b[f])) notNorm++;
  }
  const pc = (a, b) => (100 * a / b).toFixed(2) + '%';
  const nElder = (T['n.m.elder'] || 0) + (T['n.f.elder'] || 0), adultM = (T['n.m.adult'] || 0) + (T['n.m.elder'] || 0), bAdultM = (T['beard.m.adult'] || 0) + (T['beard.m.elder'] || 0);
  const out = { N, manInFem: manFem, manInFemBottom: manFemBottom, swim: pc(swim, N) + ' (1/12 = 8.33%)', age: ['adult', 'youth', 'elder', 'child'].map(a => a + ' ' + pc(T['age.' + a] || 0, N)).join(', '),
    elderGrey: pc(T['greyhair.elder'] || 0, nElder) + ' (README: 5 in 6 = 83.33%)', nonElderGrey: (T['greyhair.adult'] || 0) + (T['greyhair.youth'] || 0) + (T['greyhair.child'] || 0) + ' (salt allowed on adults)',
    beardOnAdultElderMen: pc(bAdultM, adultM) + ' (3 in 5)', hat: pc(T.hat || 0, N) + ' (3 in 5)', bald: Object.keys(T).filter(k => k.startsWith('bald.')).map(k => k.slice(5) + ' ' + T[k]).join(', '), notNormalized: notNorm };
  for (const [k, v] of Object.entries(out)) log('  ' + k + ': ' + v);
  if (manFem || manFemBottom) fail('randomBuild put a man in a fem garment or bottom');
  const sweep = (from, n, ids) => { const by = {}, bad = []; for (let s = from; s < from + n; s++) { const b = C.randomBuild(mulberry32(s)), rows = runSome(b, ids), f = rows.filter(r => r.gate && !r.pass);
    if (f.length) { bad.push(s); for (const r of f) (by[r.id] = by[r.id] || []).push(s + ' ' + b.sex + '/' + b.age + '/' + b.head + (r.id === 'face' ? ' [' + String(r.value) + ': ' + String(r.detail).replace(/\s+/g, ' ').slice(0, 150) + ']' : r.id === 'look' ? ' [' + String(r.value) + ']' : ' [' + String(r.value) + ']')); } }
    return { bad, by }; };
  const RA = +(process.env.RA || 60), t0 = Date.now(), a = sweep(1, RA, null);
  log('  every gate, seeds 1..' + RA + ': ' + a.bad.length + ' fail (' + ((Date.now() - t0) / 1000).toFixed(0) + ' s); README: 7, all look (5, 14, 17, 18, 21, 33, 59)');
  for (const [id, v] of Object.entries(a.by)) log('    ' + id + ' ' + v.length + ': ' + v.map(x => x.split(' [')[0]).join(', '));
  const RF = +(process.env.RF || 300), t1 = Date.now(), b = sweep(1, RF, ['face', 'light']);
  log('  face and light, seeds 1..' + RF + ': ' + b.bad.length + ' fail (' + ((Date.now() - t1) / 1000).toFixed(0) + ' s); README: 7 of 300 (face 112, 116, 121, 157, 184 mouth; 121, 145 eye at SW; light 189)');
  for (const [id, v] of Object.entries(b.by)) { log('    ' + id + ' ' + v.length + ':'); for (const x of v) log('      ' + x); }
  out.gatesAll = { n: RA, bad: a.bad, by: a.by }; out.faceLight = { n: RF, bad: b.bad, by: b.by };
  SUM.random = out;
}

// ---------- H. the README's claims ----------
if (want('claims')) {
  log('\n== H. the README\'s cast table and coverage claims');
  // the cast table's rows only: section 2's table also starts rows with a backticked name (`revision`)
  const md = fs.readFileSync(path.join(K, 'README.md'), 'utf8').split('\n'), rows = md.filter(l => /^\| `\w+` \|/.test(l) && KEYS.includes(l.split('|')[1].trim().replace(/`/g, '')));
  let ok = 0; const bad = [];
  for (const l of rows) {
    const c = l.split('|').slice(1, -1).map(s => s.trim()), k = c[0].replace(/`/g, ''), B = C.buildOf(k), b = B.b, G = C.GARMENTS[b.garment];
    const body = (b.sex === 'm' ? 'man' : 'woman') + ', ' + b.frame + ', ' + b.age, wear = G.top ? b.garment + ' + ' + b.bottom : b.garment;
    const rowsK = MINE[k] || C.runChecks(k), gp = rowsK.filter(r => r.gate && r.pass).length, gt = rowsK.filter(r => r.gate).length;
    const readme = { label: c[1], body: c[2], wear: c[3], hair: c[4], beard: c[5], hat: c[6], tris: +c[7].replace(/,/g, ''), designed: c[8], idle: c[9], gated: c[10] };
    const got = { label: b.label, body, wear, hair: b.hairStyle, beard: b.beard, hat: b.hat, tris: C.sizes(k).tris, designed: (B.D.headZ + B.D.crown[2]).toFixed(3), idle: (+C.gameplay(k).figure.height_m).toFixed(3), gated: gp + ' / ' + gt };
    const miss = Object.keys(readme).filter(f => String(readme[f]) !== String(got[f]));
    if (miss.length) bad.push(k + ': ' + miss.map(f => f + ' README ' + readme[f] + ' rig ' + got[f]).join('; ')); else ok++;
  }
  log('  README cast rows: ' + rows.length + '; equal to the rig on every column ' + ok);
  for (const s of bad) log('  ' + s);
  const npc = C.NPCS.map(k => C.buildOf(k).b), cov = (f, all) => { const s = new Set(npc.map(b => b[f])); return all.filter(x => !s.has(x)); };
  const claims = { beardsMissingOnNPCs: cov('beard', C.OPTIONS.beard), muttonOn: KEYS.filter(k => C.buildOf(k).b.beard === 'mutton') };
  for (const [k, v] of Object.entries(claims)) log('  ' + k + ': ' + (v.length ? v.join(', ') : 'none'));
  const cell = KEYS.map(k => k + ' ' + ((MINE[k] || []).find(r => r.id === 'cell') || {}).value);
  log('  cell values: ' + cell.join(', '));
  const cf = (MINE.deckboss || []).find(r => r.id === 'cell'); if (cf) log('  cell detail (deck boss): ' + String(cf.detail).slice(0, 500));
  const aim = KEYS.map(k => { const r = (MINE[k] || []).find(x => x.id === 'look'), m = r && /within ([\d.]+)°/.exec(r.detail); return [k, m ? +m[1] : NaN]; });
  log('  aim (the look check, share 1): ' + aim.map(([k, v]) => k + ' ' + v).join(', ') + '; max ' + Math.max(...aim.map(a => a[1])));
  const hz = (MINE.fisher || []).find(r => r.id === 'heights'); if (hz) log('  heights detail (fisher): ' + hz.detail);
  const cl = KEYS.map(k => { const r = (MINE[k] || []).find(x => x.id === 'cell'); return r && /cloth/i.test(r.detail) ? k + ': ' + (/[^.]*cloth[^.]*\./i.exec(r.detail) || [''])[0].trim().slice(0, 200) : null; }).filter(Boolean);
  log('  cloth notes in the cell check: ' + (cl.join(' | ') || 'none'));
  // the text items of the send-back
  const readmeTxt = md.join('\n'), poses4 = fs.readFileSync(path.join(K, 'Art/characterIsoRig10.poses.js'), 'utf8').split('\n')[3], chk = fs.readFileSync(path.join(K, 'Art/characterIsoRig10.checks.js'), 'utf8');
  const heads = md.filter(l => /^## \d+\./.test(l)).map(l => l.slice(3, 40));
  const refs = [...new Set((readmeTxt.match(/`((?:Art|builds|gameplay|data|renders|tools)\/[^`<>*…]+?\.(?:js|json|png|cjs|html))`/g) || []).map(s => s.slice(1, -1)))];
  const missingRefs = refs.filter(r => !fs.existsSync(path.join(K, r)));
  log('  README sections: ' + heads.join(' · '));
  log('  README file references: ' + refs.length + ', absent from the kit: ' + (missingRefs.join(', ') || 'none') + '; mentions of 10.0\'s README: ' + (readmeTxt.match(/10\.0's README|README of 10\.0/g) || []).length);
  log('  poses line 4: ' + JSON.stringify(poses4.trim().slice(0, 120)) + '; characterIsoRig8 named in the poses file: ' + /characterIsoRig8/.test(fs.readFileSync(path.join(K, 'Art/characterIsoRig10.poses.js'), 'utf8')));
  log('  checks text: "1.545" ' + (chk.match(/1\.545/g) || []).length + ', "19 presets" ' + (chk.match(/19 presets/g) || []).length + ', "4–5 px"/"4-5 px" in README ' + (readmeTxt.match(/4[–-]5 px/g) || []).length + ', "mutton" in README ' + (readmeTxt.match(/mutton/g) || []).length);
  SUM.claims = { tableRows: rows.length, tableOk: ok, tableBad: bad, claims, cell, aim, missingRefs, heads };
}

// ---------- I. the steps between frames (the send-back's item 7) ----------
if (want('steps')) {
  log('\n== I. bone steps between adjacent frames, every clip of every build (exportBuild)');
  const ROD = { tool_R_1: 1, tool_R_2: 1, tool_L_1: 1, tool_L_2: 1 }, SADDLE = { mountUp: 1, mountDown: 1 }, BENCH = { mountCab: 1, mountCabDown: 1 };
  const tot = { steps: 0, over90: 0, at180: 0 }; let worst = { a: 0 }, worstBody = { a: 0 }, rod = { a: 0 }, saddle = { a: 0 }, bench = { a: 0 }, nonMount = { a: 0 }; const at180 = [], tools = {}; const fisher = { worst: { a: 0 }, worstBody: { a: 0 } };
  for (const k of KEYS) {
    const E = C.exportBuild(k);
    for (const c of E.clips) { const an = (C.clipDef(c.name) || {}).anim || c.name;
      for (let i = 0; i + 1 < c.tracks.length; i++) for (const bn of c.bones) {
        const a = ang(c.tracks[i].bones[bn].rot, c.tracks[i + 1].bones[bn].rot), w = { a, k, clip: c.name, anim: an, bn, f: i }; tot.steps++; if (a > 90) tot.over90++;
        if (a >= 179.9) { tot.at180++; if (at180.length < 12) at180.push(k + ' ' + c.name + ' ' + bn + ' ' + i + '>' + (i + 1) + ' ' + a.toFixed(1)); }
        if (a > worst.a) worst = w; if (ROD[bn] && a > rod.a) rod = w;
        if (/^tool_/.test(bn) && k === 'fisher' && c.name === 'cast') tools[bn] = Math.max(tools[bn] || 0, a);
        const body = !/^tool_|^carry_/.test(bn);
        if (body && a > worstBody.a) worstBody = w; if (body && SADDLE[an] && a > saddle.a) saddle = w; if (body && BENCH[an] && a > bench.a) bench = w; if (body && !SADDLE[an] && !BENCH[an] && a > nonMount.a) nonMount = w;
        if (k === 'fisher') { if (a > fisher.worst.a) fisher.worst = w; if (body && a > fisher.worstBody.a) fisher.worstBody = w; }
      } }
  }
  const f = (w) => w.a.toFixed(1) + ' (' + w.k + ' ' + w.clip + ' ' + w.bn + ' f' + w.f + '>' + (w.f + 1) + ')';
  log('  all 30: ' + tot.steps + ' steps, ' + tot.over90 + ' over 90 deg, ' + tot.at180 + ' at 179.9 or more' + (at180.length ? ': ' + at180.join(' · ') : ''));
  log('  worst any bone ' + f(worst) + '; the rod\'s bend sockets ' + f(rod) + ' (README: 14.3, the Fisher cast f4>5 tool_L_2)');
  log('  the Fisher\'s cast, worst step per tool bone: ' + Object.entries(tools).map(([b, a]) => b + ' ' + a.toFixed(1)).join(', ') + ' (README: the palm socket 100.3 in f4>5, meant)');
  log('  body joints: worst ' + f(worstBody) + '; saddle mounts ' + f(saddle) + ' (README 120.7, nan and the mender mountDown f7>8 hip_R); bench mounts ' + f(bench) + ' (README 141.6, mountCab f6>7 hip_R); every other clip ' + f(nonMount));
  log('  the Fisher: worst ' + f(fisher.worst) + ', worst body joint ' + f(fisher.worstBody) + ' (README 103.6, mountDown f7>8 foot_R)');
  SUM.steps = { all: tot, at180, worst, rod, tools, worstBody, saddle, bench, nonMount, fisher };
}

// ---------- J. the frozen 10.1 and 10.2 copies: their own symbols, and they leave CharacterIso10 alone ----------
if (want('frozen')) {
  log('\n== J. the frozen copies (the review pages only)');
  const before = globalThis.CharacterIso10; const rows = {};
  for (const [js, poses, sym] of [['characterIsoRig10_1.js', 'characterIsoRig10_1.poses.js', 'CharacterIso10_1'], ['characterIsoRig10_2.js', 'characterIsoRig10_2.poses.js', 'CharacterIso10_2']]) {
    const ctx = vm.createContext({ console });
    for (const f of [js, poses]) vm.runInContext(fs.readFileSync(path.join(K, 'Art', f), 'utf8'), ctx, { filename: f });
    const F = ctx[sym], syms = Object.keys(ctx).filter(k => /^CharacterIso/.test(k));
    for (const f of [js, poses]) vm.runInThisContext(fs.readFileSync(path.join(K, 'Art', f), 'utf8'), { filename: f });
    const kept = globalThis.CharacterIso10 === before && globalThis.CharacterIso10.revision === REV;
    rows[sym] = { symbols: syms, revision: F && F.revision, kept };
    log('  ' + js + ': defines ' + syms.join(',') + ', revision ' + (F && F.revision) + '; loaded after 10.3 in one page, CharacterIso10 ' + (kept ? 'unchanged' : 'CHANGED'));
    if (!kept) fail(js + ' replaced CharacterIso10');
  }
  // is the frozen 10.2 the staged 10.2 kit, renamed? the source with the symbol renamed, and the Fisher rendered by both
  const r102 = path.join(K102, 'Art/characterIsoRig10.js');
  if (fs.existsSync(r102)) {
    // the frozen copy is the 10.2 rig with a header line on top and its global renamed: take both off, then compare line by line
    const a = fs.readFileSync(r102, 'utf8').split('\n'), b0 = fs.readFileSync(path.join(K, 'Art/characterIsoRig10_2.js'), 'utf8').split('\n');
    const head = /^\/\* FROZEN COPY/.test(b0[0]) ? b0[0] : null, renamed = b0.filter(l => l.includes('CharacterIso10_2')).length;
    const b = (head ? b0.slice(1) : b0).map(l => l.split('CharacterIso10_2').join('CharacterIso10'));
    let dl = 0; const ex = []; for (let i = 0; i < Math.max(a.length, b.length); i++) if (a[i] !== b[i]) { dl++; if (ex.length < 3) ex.push((i + 1) + ': ' + String(b[i]).slice(0, 140)); }
    const c102 = vm.createContext({ console }); for (const f of ['characterIsoRig10.js', 'characterIsoRig10.poses.js']) vm.runInContext(fs.readFileSync(path.join(K102, 'Art', f), 'utf8'), c102, { filename: f });
    const F2 = globalThis.CharacterIso10_2, N = c102.CharacterIso10; let px = 0;
    for (let d = 0; d < 8; d++) { const x = N.render({ build: 'fisher', clip: 'idle', frame: 0, dir: d }), y = F2.render({ build: 'fisher', clip: 'idle', frame: 0, dir: d }); px += pxDiff(canon(x.rgba), canon(y.rgba)).n; }
    log('  characterIsoRig10_2.js against the staged 10.2 kit\'s rig: ' + a.length + ' vs ' + b0.length + ' lines; with the header line ' + (head ? 'taken off' : 'absent') + ' and CharacterIso10_2 renamed back (' + renamed + ' line' + (renamed === 1 ? '' : 's') + '), ' + dl + ' differ' + (ex.length ? ' (e.g. ' + ex.join(' · ') + ')' : '') + '; the Fisher idle at 8 facings, pixels differing ' + px);
    rows.vs102 = { lines: [a.length, b0.length], header: head, renamedLines: renamed, differ: dl, ex, fisherPx: px };
  }
  SUM.frozen = rows;
}

// ---------- K. against kit 10.2 (the staged kit): what the game's bake reads, and what moved ----------
if (want('vs102') && fs.existsSync(path.join(K102, 'Art/characterIsoRig10.js'))) {
  log('\n== K. against kit 10.2 (' + K102 + ', rig ' + sha(path.join(K102, 'Art/characterIsoRig10.js')).slice(0, 8) + ')');
  const ctx = vm.createContext({ console });
  for (const f of ['characterIsoRig10.js', 'characterIsoRig10.poses.js', 'characterIsoRig10.checks.js']) vm.runInContext(fs.readFileSync(path.join(K102, 'Art', f), 'utf8'), ctx, { filename: f });
  const N = ctx.CharacterIso10, J = (o) => String(JSON.stringify(o, (k, v) => typeof v === 'function' ? 'fn' : v));
  const isObj = (x) => x && typeof x === 'object' && !Array.isArray(x), isNames = (x) => Array.isArray(x) && x.every(y => typeof y === 'string');
  const rows = {}, put = (name, a, b) => {
    let r;
    if (J(a) === J(b)) r = 'SAME';
    else if (isObj(a) && isObj(b)) r = 'CHANGED at ' + [...new Set([...Object.keys(a), ...Object.keys(b)])].filter(k => J(a[k]) !== J(b[k])).map(k => k + ': ' + String(J(a[k])).slice(0, 70) + ' -> ' + String(J(b[k])).slice(0, 70)).join('; ');
    else if (isNames(a) && isNames(b)) r = 'CHANGED: added ' + (b.filter(x => !a.includes(x)).join(',') || 'none') + '; removed ' + (a.filter(x => !b.includes(x)).join(',') || 'none');
    else r = 'CHANGED: 10.2 ' + String(J(a)).slice(0, 160) + ' -> 10.3 ' + String(J(b)).slice(0, 160);
    rows[name] = r; log('  ' + name + ': ' + r);
  };
  put('cell W, H, pivot', [N.W, N.H, N.pivot], [C.W, C.H, C.pivot]); put('PX, ELEV, order', [N.PX, N.ELEV, N.order], [C.PX, C.ELEV, C.order]);
  put('FACE_SLOTS', N.FACE_SLOTS, C.FACE_SLOTS); put('BLINK', N.BLINK, C.BLINK); put('LOOK', N.LOOK, C.LOOK); put('SHADING', N.SHADING, C.SHADING);
  put('ANIMS', N.ANIMS, C.ANIMS); put('clipNames', N.clipNames(), C.clipNames()); put('SOCKETS', N.SOCKETS, C.SOCKETS); put('HUMANOID', N.HUMANOID, C.HUMANOID);
  put('CONTRACT', N.CONTRACT, C.CONTRACT); put('CARRIES', N.CARRIES, C.CARRIES); put('GROUP_ORDER', N.GROUP_ORDER, C.GROUP_ORDER);
  put('CAST', N.CAST, C.CAST); put('CAST10', N.CAST10, C.CAST10); put('FIELDS', N.FIELDS, C.FIELDS); put('OPTIONS', N.OPTIONS, C.OPTIONS);
  put('GARMENTS', N.GARMENTS, C.GARMENTS); put('BOTTOMS', N.BOTTOMS, C.BOTTOMS); put('BEARDS', N.BEARDS, C.BEARDS); put('HATS', N.HATS, C.HATS); put('HEADS', N.HEADS, C.HEADS);
  put('FRAMES', N.FRAMES, C.FRAMES); put('AGES', N.AGES, C.AGES); put('SEX', N.SEX, C.SEX); put('palettes', N.palettes, C.palettes); put('EYE_SHAPES', N.EYE_SHAPES, C.EYE_SHAPES);
  put('skeleton ids and parents', N.skeleton('fisher').map(b => b.id + '<' + b.parent), C.skeleton('fisher').map(b => b.id + '<' + b.parent));
  put('lookContract', N.lookContract(), C.lookContract());
  rows.lookAtSource = N.lookAt.toString() === C.lookAt.toString() ? 'SAME' : 'CHANGED'; rows.lookESource = N.lookE && C.lookE ? (N.lookE.toString() === C.lookE.toString() ? 'SAME' : 'CHANGED') : 'n/a';
  log('  lookAt() source: ' + rows.lookAtSource + '; lookE() source: ' + rows.lookESource);
  rows.apiOnly102 = Object.keys(N).filter(k => !(k in C)); rows.apiOnly103 = Object.keys(C).filter(k => !(k in N));
  log('  API only in 10.2: ' + (rows.apiOnly102.join(',') || 'none') + ' · only in 10.3: ' + (rows.apiOnly103.join(',') || 'none'));
  // per build: the export by part, every clip's bones, the sidecar's pins, the idle and walk renders
  const R = (o) => JSON.parse(JSON.stringify(o, r7a));
  const clipMoves = {}, keyMoves = {}, tris = [], pinMoves = {}, figMoves = [], idlePx = [], walkPx = [];
  for (const k of KEYS) {
    const a = R(N.exportBuild(k)), b = R(C.exportBuild(k));
    for (const key of new Set([...Object.keys(a), ...Object.keys(b)])) { if (key === 'clips') continue; const d = ddiff(a[key], b[key], 1.01e-7); if (d.n) (keyMoves[key] = keyMoves[key] || []).push(k); }
    const ta = N.sizes(k).tris, tb = C.sizes(k).tris; if (ta !== tb) tris.push(k + ' ' + ta + '->' + tb);
    const ca = Object.fromEntries(a.clips.map(c => [c.name, c])), cb = Object.fromEntries(b.clips.map(c => [c.name, c]));
    for (const name of new Set([...Object.keys(ca), ...Object.keys(cb)])) { const x = ca[name], y = cb[name];
      if (!x || !y) { (clipMoves[name] = clipMoves[name] || { builds: new Set(), bones: {}, frames: 0, maxDeg: 0 }).builds.add(k + (x ? ' (gone)' : ' (new)')); continue; }
      for (let i = 0; i < Math.min(x.tracks.length, y.tracks.length); i++) for (const bn of y.bones) { const p = x.tracks[i].bones[bn], q = y.tracks[i].bones[bn]; if (!p || !q) continue;
        const dr = ang(p.rot, q.rot), dp = Math.hypot(p.pos[0] - q.pos[0], p.pos[1] - q.pos[1], p.pos[2] - q.pos[2]);
        if (dr > 0.01 || dp > 1e-5) { const m = clipMoves[name] = clipMoves[name] || { builds: new Set(), bones: {}, frames: 0, maxDeg: 0 }; m.builds.add(k); m.bones[bn] = (m.bones[bn] || 0) + 1; m.frames++; if (dr > m.maxDeg) m.maxDeg = dr; } }
      if (x.tracks.length !== y.tracks.length) (clipMoves[name] = clipMoves[name] || { builds: new Set(), bones: {}, frames: 0, maxDeg: 0 }).builds.add(k + ' (frames ' + x.tracks.length + '->' + y.tracks.length + ')'); }
    const ga = R(N.gameplay(k)), gb = R(C.gameplay(k));
    const df = ddiff(ga.figure, gb.figure, 1.01e-4); if (df.n) figMoves.push(k + ': ' + df.first.slice(0, 2).join('; '));
    for (const name of Object.keys(gb.clips || {})) { const d = ddiff((ga.clips || {})[name] || {}, gb.clips[name], 1.01e-4); if (d.n) (pinMoves[name] = pinMoves[name] || []).push(k); }
    let ip = []; for (let d = 0; d < 8; d++) { const x = N.render({ build: k, clip: 'idle', frame: 0, dir: d }), y = C.render({ build: k, clip: 'idle', frame: 0, dir: d }), n = pxDiff(canon(x.rgba), canon(y.rgba)).n; if (n) ip.push(C.order[d] + ' ' + n); }
    if (ip.length) idlePx.push(k + ' ' + ip.join('/'));
    let wp = 0; for (let f = 0; f < 8; f++) { const x = N.render({ build: k, clip: 'walk', frame: f, dir: 4 }), y = C.render({ build: k, clip: 'walk', frame: f, dir: 4 }); wp += pxDiff(canon(x.rgba), canon(y.rgba)).n; }
    if (wp) walkPx.push(k + ' ' + wp);
  }
  log('  export parts that moved beyond 1e-7 (not clips): ' + (Object.entries(keyMoves).map(([k, v]) => k + ' on ' + v.length + (v.length <= 6 ? ' (' + v.join(', ') + ')' : '')).join('; ') || 'none'));
  log('  tris that changed: ' + (tris.join(', ') || 'none'));
  log('  clips that moved (a bone by over 0.01 deg or 1e-5 m in any frame):');
  for (const [name, m] of Object.entries(clipMoves).sort()) log('    ' + name + ': ' + m.builds.size + ' builds, ' + m.frames + ' bone-frames, max ' + m.maxDeg.toFixed(1) + ' deg; bones ' + Object.entries(m.bones).sort((p, q) => q[1] - p[1]).slice(0, 8).map(([b, n]) => b + ' ' + n).join(', '));
  log('  sidecar figure moved beyond 1e-4: ' + (figMoves.join(' | ') || 'none'));
  log('  sidecar clips whose pins moved beyond 1e-4: ' + (Object.entries(pinMoves).map(([n, v]) => n + ' ' + v.length).join(', ') || 'none'));
  log('  idle f0 renders that changed (facing px): ' + (idlePx.join(' · ') || 'none'));
  log('  walk at S renders that changed (px over 8 frames): ' + (walkPx.join(' · ') || 'none'));
  rows.keyMoves = keyMoves; rows.tris = tris; rows.clipMoves = Object.fromEntries(Object.entries(clipMoves).map(([n, m]) => [n, { builds: [...m.builds], frames: m.frames, maxDeg: m.maxDeg, bones: m.bones }]));
  rows.figMoves = figMoves; rows.pinMoves = pinMoves; rows.idlePx = idlePx; rows.walkPx = walkPx;
  SUM.vs102 = rows;
}

// ---------- L. the cell, unclipped ----------
// Every frame's posed mesh goes through the rig's own proj, unclipped, against the cell: the spare is the rows or columns left
// between the mesh's extent and the cell's edge (negative: past it). The kit's own check counts drawn pixels, keyline included.
if (want('cellcut')) {
  log('\n== L. the cell, unclipped: the posed mesh through proj against the ' + C.W + ' x ' + C.H + ' cell (every clip but the four mount clips, 8 facings)');
  const SKIP = { mountUp: 1, mountDown: 1, mountCab: 1, mountCabDown: 1 }, LYING = { swim: 1, sleep: 1, tread: 1 };
  const cams = [0, 1, 2, 3, 4, 5, 6, 7].map(d => C.camOf({ dir: d }));
  const worst = (build) => { const B = C.buildOf(build), w = { lying: { o: -99 }, other: { o: -99 }, sleepN: { o: -99 } };
    for (const n of C.clipNames()) { const cd = C.clipDef(n); if (SKIP[cd.anim]) continue;
      for (let k = 0; k < C.ANIMS[cd.anim].frames; k++) { const faces = C.posed(C.evalClip(n, C.uOf(cd.anim, k), B), B);
        for (let d = 0; d < 8; d++) { let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
          for (const f of faces) for (const p of f.v) { const q = C.proj(p, cams[d]); if (q.sx < x0) x0 = q.sx; if (q.sx > x1) x1 = q.sx; if (q.sy < y0) y0 = q.sy; if (q.sy > y1) y1 = q.sy; }
          const o = Math.max(-x0, x1 - C.W, -y0, y1 - C.H), side = o === -x0 ? 'left' : o === x1 - C.W ? 'right' : o === -y0 ? 'top' : 'bottom', slot = LYING[cd.anim] ? w.lying : w.other;
          if (o > slot.o) Object.assign(slot, { o, n, k, d, side });
          if (cd.anim === 'sleep' && d === 0 && y1 - C.H > w.sleepN.o) Object.assign(w.sleepN, { o: y1 - C.H, n, k, d, side: 'bottom' }); } } }
    for (const s of [w.lying, w.other, w.sleepN]) s.at = s.n + ' f' + s.k + ' ' + C.order[s.d] + ' ' + s.side;
    return { h: B.D.heightM, lying: w.lying, other: w.other, sleepN: w.sleepN }; };
  const fmt = (k, s) => k + ' ' + s.at + ' ' + (-s.o).toFixed(2) + ' px spare';
  const rows = {}, past = []; let minL = { o: -99 }, minO = { o: -99 };
  for (const k of KEYS) { const w = worst(k); rows[k] = { heightM: +w.h.toFixed(3), lyingSpare: +(-w.lying.o).toFixed(2), lyingAt: w.lying.at, otherSpare: +(-w.other.o).toFixed(2), otherAt: w.other.at, sleepNSpareBelowFeet: +(-w.sleepN.o).toFixed(2) };
    for (const s of [w.lying, w.other]) if (s.o > 0.5) past.push(fmt(k, s));
    if (w.lying.o > minL.o) minL = Object.assign({}, w.lying, { key: k }); if (w.other.o > minO.o) minO = Object.assign({}, w.other, { key: k }); }
  log('  presets past the cell (over 0.5 px): ' + (past.join(' · ') || 'none'));
  log('  tightest lying clip ' + fmt(minL.key, minL) + '; tightest other clip ' + fmt(minO.key, minO));
  log('  sleep at N, spare below the feet (unclipped mesh): ' + ['deckboss', 'lobsterman', 'fisher'].map(k => k + ' ' + rows[k].sleepNSpareBelowFeet).join(', ') + ' (README, drawn rows: 3, 4, 5)');
  const RC = +(process.env.RC || 60), rr = { n: RC, pastLying: [], pastOther: [], worstLying: null, worstOther: null };
  for (let s = 1; s <= RC; s++) { const b = C.randomBuild(mulberry32(s)), w = worst(b);
    if (w.lying.o > 0.5) rr.pastLying.push(s); if (w.other.o > 0.5) rr.pastOther.push(s);
    if (!rr.worstLying || w.lying.o > rr.worstLying.o) rr.worstLying = { seed: s, o: +w.lying.o.toFixed(2), at: w.lying.at, heightM: +w.h.toFixed(3) };
    if (!rr.worstOther || w.other.o > rr.worstOther.o) rr.worstOther = { seed: s, o: +w.other.o.toFixed(2), at: w.other.at, heightM: +w.h.toFixed(3) }; }
  log('  random builds, seeds 1..' + RC + ': past the cell in a lying clip ' + rr.pastLying.length + (rr.pastLying.length ? ' (' + rr.pastLying.join(',') + ')' : '') + ', in another clip ' + rr.pastOther.length + (rr.pastOther.length ? ' (' + rr.pastOther.join(',') + ')' : '') + '; worst lying ' + JSON.stringify(rr.worstLying) + '; worst other ' + JSON.stringify(rr.worstOther));
  SUM.cellcut = { presets: rows, past, random: rr };
}

// ---------- M. the aim bar (the send-back's section 4), measured here as AIM.measure says ----------
if (want('aim')) {
  log('\n== M. the aim bar: AIM, lookContract().aimBar, the files, and the aim measured here by AIM\'s own words');
  const A = C.AIM, lc = C.lookContract();
  log('  AIM: bar_deg ' + A.bar_deg + ', share ' + A.share + ', distance ' + A.distance_m + ' m, bearings ' + A.bearings_deg.join(',') + ', dz ' + A.dz_m.join(','));
  log('  lookContract().aimBar.deg ' + (lc.aimBar && lc.aimBar.deg) + '; LOOK.headShare ' + C.LOOK.headShare);
  const sideBars = KEYS.map(k => rd('gameplay/characterIsoRig10.' + k + '.gameplay.json').overlays.look.aimBar).map(x => x && x.deg);
  const expBars = KEYS.map(k => ((rd('builds/' + k + '.v10.json').look || {}).aimBar || {}).deg);
  log('  aimBar.deg in the 30 sidecars: ' + [...new Set(sideBars)].join(',') + '; in the 30 build exports (look): ' + [...new Set(expBars)].join(','));
  const measure = (b) => { const B = C.buildOf(b), S = C.evalClip(A.clip, A.frame, B), ix = B.sk.ix, W0 = S.W[ix.head], h0 = mV(W0.R, B.D.headMid), e0 = [W0.p[0] + h0[0], W0.p[1] + h0[1], W0.p[2] + h0[2]];
    let aim = 0, at = '', inside = 0;
    for (const bear of A.bearings_deg) for (const dz of A.dz_m) { const T = [e0[0] + A.distance_m * Math.sin(bear * Math.PI / 180), e0[1] + A.distance_m * Math.cos(bear * Math.PI / 180), e0[2] + dz], L = C.lookAt(S, T, A.share);
      if (Math.abs(L.need.yaw) > C.LOOK.yaw[1] || L.need.pitch < C.LOOK.pitch[0] || L.need.pitch > C.LOOK.pitch[1]) continue; inside++;
      const S2 = C.evalClip(A.clip, A.frame, B, null, null, { yaw: L.yaw, pitch: L.pitch }), Wh = S2.W[ix.head], f = mV(Wh.R, [0, 1, 0]), hh = mV(Wh.R, B.D.headMid), ep = [Wh.p[0] + hh[0], Wh.p[1] + hh[1], Wh.p[2] + hh[2]];
      const d = [T[0] - ep[0], T[1] - ep[1], T[2] - ep[2]], dl = Math.hypot(...d), a = Math.acos(Math.max(-1, Math.min(1, (f[0] * d[0] + f[1] * d[1] + f[2] * d[2]) / dl))) * 180 / Math.PI;
      if (a > aim) { aim = a; at = bear + '° ' + (dz >= 0 ? '+' : '') + dz; } }
    return { aim, at, inside, elder: B.b.age === 'elder' }; };
  const GR = rd('golden-report.json'); const rows = [];
  for (const k of KEYS) { const m = measure(k), r = GR.builds[k].checks.find(x => x.id === 'look'), g = /within ([\d.]+)°/.exec(r.detail); rows.push([k, +m.aim.toFixed(3), m.at, m.inside, g ? +g[1] : NaN, m.elder]); }
  const nonE = rows.filter(r => !r[5]), eld = rows.filter(r => r[5]);
  log('  measured here: the 23 non-elders ' + Math.min(...nonE.map(r => r[1])).toFixed(2) + ' to ' + Math.max(...nonE.map(r => r[1])).toFixed(2) + '°, the ' + eld.length + ' elders ' + Math.min(...eld.map(r => r[1])).toFixed(2) + ' to ' + Math.max(...eld.map(r => r[1])).toFixed(2) + '° (README: 2.82 to 2.98, the seven elders 3.65 to 3.73); targets inside the limits ' + [...new Set(rows.map(r => r[3]))].join(','));
  log('  equal to the golden report\'s printed aim (2 decimals): ' + rows.filter(r => Math.abs(r[1] - r[4]) < 0.006).length + ' / ' + rows.length + '; over the bar: ' + (rows.filter(r => r[1] > A.bar_deg).map(r => r[0]).join(', ') || 'none'));
  log('  per build: ' + rows.map(r => r[0] + ' ' + r[1] + ' (' + r[2] + ')').join(', '));
  const RA = +(process.env.RA || 60); let rw = { aim: 0 };
  for (let s = 1; s <= RA; s++) { const m = measure(C.randomBuild(mulberry32(s))); if (m.aim > rw.aim) rw = Object.assign({ seed: s }, m); }
  log('  random builds, seeds 1..' + RA + ': worst aim ' + rw.aim.toFixed(2) + '° (seed ' + rw.seed + ', ' + rw.at + '; README: 3.71 at most)');
  // the game's view: with LOOK.headShare (the default), the head alone points within this much of a target 2 m away
  const share = (b) => { const B = C.buildOf(b), S = C.evalClip('idle', 0, B), ix = B.sk.ix, W0 = S.W[ix.head], h0 = mV(W0.R, B.D.headMid), e0 = [W0.p[0] + h0[0], W0.p[1] + h0[1], W0.p[2] + h0[2]]; let w = 0;
    for (const bear of [0, 15, 30, 50]) { const T = [e0[0] + 2 * Math.sin(bear * Math.PI / 180), e0[1] + 2 * Math.cos(bear * Math.PI / 180), e0[2]], L = C.lookAt(S, T); w = Math.max(w, Math.abs(L.need.yaw - L.yaw)); } return w; };
  log('  with the default share (' + C.LOOK.headShare + '), the turn the eyes are left to make at bearing 50: the Fisher ' + share('fisher').toFixed(1) + '° (eyes.left/right beyond ' + C.LOOK.eyesBeyond_deg + '°)');
  SUM.aim = { AIM: A, sideBars: [...new Set(sideBars)], expBars: [...new Set(expBars)], rows, randomWorst: rw };
}

const tag = ONLY.length ? '-' + ONLY.join('-') : '';
const RES = 'RESULT: ' + (FAIL.length ? 'FAIL ' + FAIL.length : 'PASS') + ' (' + Object.keys(SUM).join(', ') + ')';
fs.writeFileSync(path.join(OUT, 'node-check' + tag + '.json'), JSON.stringify(Object.assign({ result: RES, failures: FAIL }, SUM), null, 1) + '\n');
LOG.push(RES);
fs.writeFileSync(path.join(OUT, 'node-check' + tag + '.txt'), LOG.join('\n') + '\n');
console.log(RES);
