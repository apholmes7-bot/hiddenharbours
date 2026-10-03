// Art desk v2: CD's character kit 10.2 on real Node (built-ins only). READ-only on the kit; writes only into OUT.
// The oracle is the rig itself, run here: the golden checks, every writer (exportBuild, gameplay, the data files,
// presets), the renders by pixel, randomBuild by seeded draws, the README's cast table, and the 09-30 send-back's
// measurable points (the look-at's aim, the steps between frames), and the cell measured unclipped (section L).
// Ends with one RESULT: line: PASS means the kit reproduces on real Node; the README's claims it does not bear out are
// reported in H, G and L, not failed.
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm'), zlib = require('zlib'), crypto = require('crypto');
const K = process.argv[2] || 'C:/hh-gauntlet/art-desk/drops/character-v10.2-kit-2026-10-01/merged/character-v10.2-kit';
const OUT = process.argv[3] || 'C:/hh-gauntlet/art-desk/drops/character-v10.2-kit-2026-10-01/desk';
const ONLY = (process.env.ONLY || '').split(',').filter(Boolean);
const want = (s) => !ONLY.length || ONLY.includes(s);
fs.mkdirSync(OUT, { recursive: true });
const LOG = [];
const log = (...a) => { const s = a.join(' '); LOG.push(s); console.log(s); };
const SUM = {};
const FAIL = [];
const fail = (s) => { FAIL.push(s); log('  FAIL ' + s); };

const sha = (p) => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const rd = (p) => JSON.parse(fs.readFileSync(path.join(K, p), 'utf8'));
const ART = ['characterIsoRig10.js', 'characterIsoRig10.poses.js', 'characterIsoRig10.checks.js'];
for (const f of ART) vm.runInThisContext(fs.readFileSync(path.join(K, 'Art', f), 'utf8'), { filename: f });
const C = globalThis.CharacterIso10;
const RIG = sha(path.join(K, 'Art', ART[0])), POSES = sha(path.join(K, 'Art', ART[1])), CHECKS = sha(path.join(K, 'Art', ART[2]));
log('node ' + process.version + ' · ' + C.rig + ' ' + C.revision + ' · rig ' + RIG.slice(0, 8) + ' poses ' + POSES.slice(0, 8) + ' checks ' + CHECKS.slice(0, 8));
const KEYS = C.CAST.slice();

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
function short(v) { const s = typeof v === 'string' ? JSON.stringify(v) : JSON.stringify(v); return s === undefined ? String(v) : s.length > 90 ? s.slice(0, 90) + '…' : s; }
const r7a = (k, v) => typeof v === 'number' ? +v.toFixed(7) : v;
const r7b = (k, v) => typeof v === 'number' ? Math.round(v * 1e7) / 1e7 : v;

// ---------- PNG, 8-bit non-interlaced, any colour type; and a writer for diff plates ----------
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
const CRC = (() => { const t = new Int32Array(256); for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c; } return t; })();
function crc32(b) { let c = -1; for (let i = 0; i < b.length; i++) c = CRC[(c ^ b[i]) & 255] ^ (c >>> 8); return (c ^ -1) >>> 0; }
function writePng(file, W, H, rgba) {
  const raw = Buffer.alloc((W * 4 + 1) * H);
  for (let y = 0; y < H; y++) { raw[y * (W * 4 + 1)] = 0; Buffer.from(rgba.buffer, rgba.byteOffset + y * W * 4, W * 4).copy(raw, y * (W * 4 + 1) + 1); }
  const chunk = (t, d) => { const l = Buffer.alloc(4); l.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t, 'latin1'), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc32(td)); return Buffer.concat([l, td, c]); };
  const ih = Buffer.alloc(13); ih.writeUInt32BE(W, 0); ih.writeUInt32BE(H, 4); ih[8] = 8; ih[9] = 6; ih[10] = 0; ih[11] = 0; ih[12] = 0;
  fs.writeFileSync(file, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ih), chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]));
}
function pxDiff(a, b) { // RGBA buffers of the same size
  let n = 0, vis = 0, maxC = 0;
  for (let i = 0; i < a.length; i += 4) {
    let d = 0; for (let c = 0; c < 4; c++) d = Math.max(d, Math.abs(a[i + c] - b[i + c]));
    if (d) { n++; if (a[i + 3] || b[i + 3]) vis++; if (d > maxC) maxC = d; }
  }
  return { n, vis, maxC };
}
const nn = (img, W, H, s) => { const o = new Uint8Array(W * s * H * s * 4); for (let y = 0; y < H * s; y++) for (let x = 0; x < W * s; x++) { const i = ((y / s | 0) * W + (x / s | 0)) * 4, j = (y * W * s + x) * 4; o[j] = img[i]; o[j + 1] = img[i + 1]; o[j + 2] = img[i + 2]; o[j + 3] = img[i + 3]; } return o; };

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
    if (j.revision !== undefined && j.revision !== '10.2') { bad++; fail('stamp ' + f + ' revision ' + j.revision); }
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
    const sz = C.sizes(k), dz = ddiff(sz, sh.sizes, 0);
    if (dz.n) diffs.push(k + ' sizes: ' + dz.first.join('; '));
    if (sh.label !== C.buildOf(k).b.label) diffs.push(k + ' label ' + sh.label);
    for (let i = 0; i < Math.max(rows.length, sh.checks.length); i++) {
      const a = rows[i], b = sh.checks[i]; rowsN++;
      if (!a || !b) { diffs.push(k + ' row ' + i + ' missing'); continue; }
      const mine = { id: a.id, title: a.title, gate: a.gate, pass: a.pass, value: a.value, detail: a.detail };
      if (a.rows !== undefined && b.rows !== undefined) mine.rows = a.rows;
      const d = ddiff(JSON.parse(JSON.stringify(mine)), b, 0);
      if (d.n) {
        const mask = (v) => String(v).replace(/-?\d+(\.\d+)?(e[-+]?\d+)?/g, '#'), numsOnly = ['title', 'value', 'detail'].every(f => mask(mine[f]) === mask(b[f])) && mine.pass === b.pass && mine.gate === b.gate;
        (byId[a.id] = byId[a.id] || { builds: 0, numbersOnly: 0, ex: '' }).builds++; if (numsOnly) byId[a.id].numbersOnly++;
        if (!byId[a.id].ex) byId[a.id].ex = k + ': ' + d.first.slice(0, 2).join('; ');
        diffs.push(k + ' ' + a.id + ': ' + d.first.join('; '));
      } else rowsEq++;
    }
  }
  log('  ' + KEYS.length + ' builds in ' + ((Date.now() - t0) / 1000).toFixed(1) + ' s; gated ' + gatedPass + ' / ' + gated + ' (kit: ' + GR.summary.gatedPass + ' / ' + GR.summary.gated + ')');
  log('  failures: ' + (failures.join(' | ') || 'none') + '   kit: ' + GR.summary.failures.join(' | '));
  log('  rows equal to the kit\'s: ' + rowsEq + ' / ' + rowsN);
  for (const [id, v] of Object.entries(byId)) log('  differs: ' + id + ' on ' + v.builds + ' builds, numbers only on ' + v.numbersOnly + '; e.g. ' + v.ex.slice(0, 260));
  const sizeDiffs = diffs.filter(d => / sizes: /.test(d));
  log('  sizes differ on ' + sizeDiffs.length + ' builds: ' + sizeDiffs.slice(0, 3).join(' | '));
  SUM.golden = { builds: KEYS.length, gated, gatedPass, failures, rowsEq, rowsN, diffs: diffs.length, byId, sizeDiffs, diffList: diffs.slice(0, 80) };
}

// ---------- C/D. the writers: exportBuild and gameplay ----------
if (want('exports')) {
  log('\n== C. builds/<key>.v10.json against exportBuild(key)');
  let byteA = 0, byteB = 0, structEq = 0, worst = 0; const notes = [];
  for (const k of KEYS) {
    const f = 'builds/' + k + '.v10.json', txt = fs.readFileSync(path.join(K, f), 'utf8'), sh = JSON.parse(txt), E = C.exportBuild(k);
    const wrap = { schema: sh.schema, rig: 'characterIsoRig10', exportSymbol: 'CharacterIso10', derivedFromRigSha256: RIG, posesDerivedFromRigSha256: POSES, revision: '10.2', authoring: sh.authoring };
    const oA = JSON.stringify(Object.assign({}, wrap, E), r7a), oB = JSON.stringify(Object.assign({}, wrap, E), r7b);
    if (oA === txt) byteA++; if (oB === txt) byteB++;
    const d = ddiff(JSON.parse(oA), sh, 0), dt = ddiff(JSON.parse(oA), sh, 1.5e-7);
    if (!d.n) structEq++; worst = Math.max(worst, d.maxD);
    if (d.n) notes.push(k + ': ' + d.n + ' of ' + d.nums + ' numbers differ at 0 (max ' + d.maxD.toExponential(2) + '), ' + dt.n + ' beyond 1.5e-7; ' + d.first.slice(0, 2).join('; '));
    if (k === KEYS[0]) log('  bytes: mine ' + oA.length + ' vs the kit\'s ' + txt.length);
  }
  log('  byte for byte: toFixed(7) ' + byteA + ' / ' + KEYS.length + ', round(1e7) ' + byteB + ' / ' + KEYS.length + '; equal as numbers ' + structEq + ' / ' + KEYS.length + ', worst ' + worst.toExponential(2));
  for (const n of notes.slice(0, 12)) log('  ' + n);
  SUM.exports = { byteToFixed: byteA, byteRound: byteB, structEq, n: KEYS.length, worst, notes: notes.slice(0, 30) };

  log('\n== D. gameplay/<key>.gameplay.json against gameplay(key)');
  let gb = 0, gr = 0, gs = 0; const gn = [];
  for (const k of KEYS) {
    const f = 'gameplay/characterIsoRig10.' + k + '.gameplay.json', txt = fs.readFileSync(path.join(K, f), 'utf8'), sh = JSON.parse(txt), G = C.gameplay(k);
    const o = JSON.stringify(Object.assign({ derivedFromRigSha256: RIG, posesDerivedFromRigSha256: POSES }, G));
    const o7 = JSON.stringify(Object.assign({ derivedFromRigSha256: RIG, posesDerivedFromRigSha256: POSES }, G), r7a);
    if (o === txt) gb++; if (o7 === txt) gr++;
    const d = ddiff(JSON.parse(o), sh, 0); if (!d.n) gs++; else gn.push(k + ': ' + d.n + ' differ (max ' + d.maxD.toExponential(2) + '); ' + d.first.slice(0, 3).join('; '));
  }
  log('  byte for byte: ' + gb + ' / ' + KEYS.length + ' (rounded 7: ' + gr + '); equal as data ' + gs + ' / ' + KEYS.length);
  for (const n of gn.slice(0, 12)) log('  ' + n);
  SUM.gameplay = { byte: gb, byteR7: gr, structEq: gs, n: KEYS.length, notes: gn.slice(0, 30) };
}

// ---------- E. the data files and presets ----------
if (want('data')) {
  log('\n== E. data/options.v10.json, data/shading.v10.json, builds/presets.json');
  const O = rd('data/options.v10.json'), S = rd('data/shading.v10.json'), P = rd('builds/presets.json');
  // The file gives each garment a group the rig's table does not carry (the README: "garments with their group"); the rule
  // read here is swim -> swimwear, top -> top, else outfit. eyeShapes is the list of EYE_SHAPES' keys (OPTIONS.eyeShape).
  const grouped = Object.fromEntries(Object.entries(C.GARMENTS).map(([k, g]) => [k, Object.assign({}, g, { group: g.swim ? 'swimwear' : g.top ? 'top' : 'outfit' })]));
  const map = { options: C.OPTIONS, fields: C.FIELDS, garments: grouped, bottoms: C.BOTTOMS, beards: C.BEARDS, hats: C.HATS, sex: C.SEX, frames: C.FRAMES, ages: C.AGES, heads: C.HEADS, eyeShapes: Object.keys(C.EYE_SHAPES), palettes: C.palettes };
  const od = {};
  for (const [k, v] of Object.entries(map)) {
    if (!(k in O)) { od[k] = 'absent in the file'; continue; }
    const d = ddiff(JSON.parse(JSON.stringify(v)), O[k], 0); od[k] = d.n ? d.n + ' differ: ' + d.first.slice(0, 3).join('; ') : 'equal (' + d.nums + ' numbers)';
    log('  options.' + k + ': ' + od[k]);
  }
  log('  options.randomBuild (the rules as written): ' + short(O.randomBuild));
  const sc = C.shadingContract('fisher'), ds = ddiff(JSON.parse(JSON.stringify(sc)), Object.fromEntries(Object.keys(sc).map(k => [k, S[k]])), 0);
  log('  shading.v10.json vs shadingContract(fisher): ' + (ds.n ? ds.n + ' differ: ' + ds.first.join('; ') : 'equal (' + ds.nums + ' numbers)') + '; extra keys ' + Object.keys(S).filter(k => !(k in sc)).join(','));
  let pe = 0; const pn = [];
  for (const k of KEYS) {
    const b = C.buildOf(k).b, s = P.builds[k], mine = { label: b.label, set: C.CAST10.includes(k) ? 'core' : 'npc', key: C.buildKey(b), build: b };
    const theirs = { label: s.label, set: s.set, key: s.key, build: s.build };
    const d = ddiff(mine, theirs, 0); if (d.n) pn.push(k + ': ' + d.first.join('; ')); else pe++;
  }
  const lists = ddiff({ cast: C.CAST, core: C.CAST10, npcs: C.NPCS, fields: C.FIELDS }, { cast: P.cast, core: P.core, npcs: P.npcs, fields: P.fields }, 0);
  log('  presets: ' + pe + ' / ' + KEYS.length + ' builds equal normBuild + buildKey; the lists ' + (lists.n ? 'DIFFER ' + lists.first.join('; ') : 'equal'));
  for (const n of pn.slice(0, 8)) log('  ' + n);
  log('  presets.builds keys on one: ' + Object.keys(P.builds.fisher).join(','));
  SUM.data = { options: od, shading: ds.n, presetsEq: pe, presetsNotes: pn.slice(0, 20), lists: lists.n };
}

// ---------- F. the renders by pixel ----------
if (want('renders')) {
  log('\n== F. renders/1x and 4x against render() on real Node');
  const M = rd('renders/manifest.json'), DIRS = C.order;
  const strips = {
    idle8: DIRS.map((d, i) => ({ clip: M.strips.idle8.clip, frame: M.strips.idle8.frame, dir: i })),
    walkS: Array.from({ length: M.strips.walkS.frames }, (_, f) => ({ clip: M.strips.walkS.clip, frame: f, dir: DIRS.indexOf(M.strips.walkS.dir) })),
  };
  let eq1 = 0, eq4 = 0, nn4 = 0, n = 0; const notes = [], DD = path.join(OUT, 'render-diffs');
  for (const k of KEYS) for (const [sname, cells] of Object.entries(strips)) {
    n++; const W = C.W * cells.length, H = C.H, mine = new Uint8Array(W * H * 4);
    cells.forEach((c, i) => { const r = C.render(Object.assign({ build: k }, c)); if (r.W !== C.W || r.H !== C.H) throw new Error('cell ' + r.W + 'x' + r.H);
      for (let y = 0; y < H; y++) mine.set(r.rgba.subarray(y * r.W * 4, (y + 1) * r.W * 4), (y * W + i * C.W) * 4); });
    const p1 = readPng(path.join(K, 'renders/1x', k + '.' + sname + '.png')), p4 = readPng(path.join(K, 'renders/4x', k + '.' + sname + '.png'));
    if (p1.W !== W || p1.H !== H) { fail(k + '.' + sname + ' 1x is ' + p1.W + 'x' + p1.H); continue; }
    const d1 = pxDiff(mine, p1.rgba); if (!d1.n) eq1++; else {
      notes.push(k + '.' + sname + ' 1x: ' + d1.n + ' px differ (' + d1.vis + ' visible, max ' + d1.maxC + ')');
      fs.mkdirSync(DD, { recursive: true }); const z = new Uint8Array(W * H * 4);
      for (let i = 0; i < z.length; i += 4) { let d = 0; for (let c = 0; c < 4; c++) d = Math.max(d, Math.abs(mine[i + c] - p1.rgba[i + c])); if (d) { z[i] = 255; z[i + 3] = 255; } else { z[i] = z[i + 1] = z[i + 2] = mine[i] >> 2; z[i + 3] = mine[i + 3]; } }
      writePng(path.join(DD, k + '.' + sname + '.diff.png'), W, H, z); writePng(path.join(DD, k + '.' + sname + '.mine.png'), W, H, mine);
    }
    const up = nn(p1.rgba, W, H, 4), d4 = pxDiff(up, p4.rgba); if (!d4.n) nn4++; else notes.push(k + '.' + sname + ' 4x is not the kit\'s 1x at x4: ' + d4.n + ' px');
    const d4m = pxDiff(nn(mine, W, H, 4), p4.rgba); if (!d4m.n) eq4++;
  }
  log('  ' + n + ' strips: 1x equal to render() ' + eq1 + ' / ' + n + '; 4x = the kit\'s 1x at x4 nearest ' + nn4 + ' / ' + n + '; 4x = mine at x4 ' + eq4 + ' / ' + n);
  for (const s of notes.slice(0, 20)) log('  ' + s);
  // the cast sheet: decoded and sized; laid out below if the layout reads
  const cs = readPng(path.join(K, 'renders/cast.png'));
  log('  cast.png ' + cs.W + 'x' + cs.H + ' colour type ' + cs.ct);
  SUM.renders = { strips: n, eq1, nn4, eq4, notes: notes.slice(0, 40), cast: [cs.W, cs.H] };
}

// ---------- G. randomBuild by seeded draws ----------
function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
if (want('random')) {
  log('\n== G. randomBuild, seeded (mulberry32)');
  const N = +(process.env.RN || 120000), rnd = mulberry32(20261001), T = {};
  const inc = (k) => { T[k] = (T[k] || 0) + 1; };
  const grey = { salt: 1, grey: 1, white: 1 }; let manFem = 0, manFemBottom = 0, notNorm = 0, swim = 0; const youthBeards = new Set(), seenBeardAges = new Set();
  for (let i = 0; i < N; i++) {
    const b = C.randomBuild(rnd), G = C.GARMENTS[b.garment], Bt = C.BOTTOMS[b.bottom];
    inc('sex.' + b.sex); inc('age.' + b.age); if (G.swim) swim++;
    if (b.sex === 'm' && G.fem) manFem++; if (b.sex === 'm' && Bt.fem) manFemBottom++;
    if (b.beard !== 'none') { seenBeardAges.add(b.sex + '/' + b.age); inc('beard.' + b.sex + '.' + b.age); }
    if (b.sex === 'm' && b.age === 'youth') youthBeards.add(b.beard);
    inc('n.' + b.sex + '.' + b.age); if (grey[b.hair]) inc('greyhair.' + b.age + (b.hair === 'salt' ? '.salt' : '')); if (b.hairStyle === 'bald') inc('bald.' + b.sex + '.' + b.age);
    if (b.hat !== 'none') inc('hat'); if (!FR(b)) inc('badframe');
    const nb = C.normBuild(b); if (C.FIELDS.some(f => nb[f] !== b[f])) notNorm++;
  }
  function FR(b) { return Object.keys(C.FRAMES[b.sex]).includes(b.frame); }
  const pc = (a, b) => (100 * a / b).toFixed(2) + '%';
  const adultM = (T['n.m.adult'] || 0) + (T['n.m.elder'] || 0), bAdultM = (T['beard.m.adult'] || 0) + (T['beard.m.elder'] || 0);
  const nElder = (T['n.m.elder'] || 0) + (T['n.f.elder'] || 0), nAdult = (T['n.m.adult'] || 0) + (T['n.f.adult'] || 0);
  const out = {
    N, men: T['sex.m'], women: T['sex.f'], manInFem: manFem, manInFemBottom: manFemBottom, swim: pc(swim, N) + ' (1/12 = 8.33%)',
    age: ['adult', 'youth', 'elder', 'child'].map(a => a + ' ' + pc(T['age.' + a] || 0, N)).join(', '),
    beardOnAdultElderMen: pc(bAdultM, adultM) + ' (3 in 5 = 60%)', beardAgesSeen: [...seenBeardAges].sort().join(','), youthManBeards: [...youthBeards].sort().join(','),
    elderGrey: pc((T['greyhair.elder'] || 0) + (T['greyhair.elder.salt'] || 0), nElder) + ' (README: 3 in 4)', adultSalt: pc(T['greyhair.adult.salt'] || 0, nAdult), adultGreyWhite: T['greyhair.adult'] || 0,
    youthOrChildGrey: (T['greyhair.youth'] || 0) + (T['greyhair.child'] || 0) + (T['greyhair.youth.salt'] || 0) + (T['greyhair.child.salt'] || 0),
    bald: Object.keys(T).filter(k => k.startsWith('bald.')).map(k => k.slice(5) + ' ' + T[k]).join(', '), hat: pc(T.hat || 0, N) + ' (3 in 5 = 60%)', badFrame: T.badframe || 0, notNormalized: notNorm,
  };
  for (const [k, v] of Object.entries(out)) log('  ' + k + ': ' + v);
  if (manFem || manFemBottom) fail('randomBuild put a man in a fem garment or bottom');
  // a few random builds through the golden suite
  const RC = +(process.env.RC || 24), r2 = mulberry32(7), rf = [];
  // the failing gates tallied from their full detail (the lines below cut it at 360 characters): for the face, the states that
  // read as the default at S, those that do at the diagonals, and the builds whose face reads at under 8 of 8 facings
  const ty = { byCheck: {}, faceAtS: {}, faceAtDiagonals: {}, faceUnder8: 0, lookPx: {}, failing: { f: 0, m: 0 }, of: { f: 0, m: 0 }, failingByAge: {}, ofByAge: {} };
  const tally = (o, k) => { o[k] = (o[k] || 0) + 1; };
  for (let i = 0; i < RC; i++) { const b = C.randomBuild(r2), rows = C.runChecks(b), bad = rows.filter(r => r.gate && !r.pass);
    tally(ty.of, b.sex); tally(ty.ofByAge, b.age);
    if (!bad.length) continue;
    tally(ty.failing, b.sex); tally(ty.failingByAge, b.age);
    for (const r of bad) { tally(ty.byCheck, r.id);
      if (r.id === 'face') { const s = /except ((?:[a-z]+\.[a-z]+(?:, )?)+)/.exec(r.detail), g = /read the same as the default: ((?:[a-z]+\.[a-z]+ [NSEW]+(?:, )?)+)/.exec(r.detail);
        if (s) for (const x of s[1].split(', ')) tally(ty.faceAtS, x); if (g) for (const x of g[1].split(', ')) tally(ty.faceAtDiagonals, x); if (!/^8 \/ 8/.test(r.value)) ty.faceUnder8++; }
      if (r.id === 'look') { const m = /beyond the unturned pose: (\d+) px/.exec(r.detail); tally(ty.lookPx, m ? m[1] + ' px' : '?'); } }
    rf.push(b.sex + ' ' + b.frame + ' ' + b.age + ' h' + b.height + ' w' + b.weight + ' ' + b.head + ' ' + b.garment + '/' + b.bottom + ' ' + b.hairStyle + ' ' + b.beard + ' ' + b.hat + ': ' + bad.map(r => r.id + ' ' + r.value + ' [' + String(r.detail).slice(0, 360) + ']').join(' || ')); }
  log('  ' + RC + ' random builds through the 20 checks: ' + (RC - rf.length) + ' pass every gate');
  log('  the failing gates, tallied: ' + JSON.stringify(ty));
  for (const s of rf) log('    ' + s);
  out.randomChecks = { n: RC, failing: rf, tally: ty };
  SUM.random = out;
}

// ---------- H. the README's claims ----------
if (want('claims')) {
  log('\n== H. the README\'s cast table and coverage claims');
  const md = fs.readFileSync(path.join(K, 'README.md'), 'utf8').split('\n'), rows = md.filter(l => /^\| `\w+` \|/.test(l));
  const P = rd('builds/presets.json'); let ok = 0; const bad = [];
  for (const l of rows) {
    const c = l.split('|').slice(1, -1).map(s => s.trim()), k = c[0].replace(/`/g, ''), B = C.buildOf(k), b = B.b, G = C.GARMENTS[b.garment];
    const sexW = b.sex === 'm' ? 'man' : 'woman', body = sexW + ', ' + b.frame + ', ' + b.age, wear = G.top ? b.garment + ' + ' + b.bottom : b.garment;
    const E = C.exportBuild(k); let top = -1; for (const f of E.bindMesh) for (const v of f.v) top = Math.max(top, v[2]);
    const designed = B.D.headZ + B.D.crown[2], rowsK = MINE[k] || C.runChecks(k), gp = rowsK.filter(r => r.gate && r.pass).length, gt = rowsK.filter(r => r.gate).length;
    const readme = { label: c[1], body: c[2], wear: c[3], hair: c[4], beard: c[5], hat: c[6], tris: +c[7].replace(/,/g, ''), height: c[8], gated: c[9] };
    const got = { label: b.label, body, wear, hair: b.hairStyle, beard: b.beard, hat: b.hat, tris: C.sizes(k).tris, height: designed.toFixed(2), gated: gp + '/' + gt };
    const miss = Object.keys(readme).filter(f => String(readme[f]) !== String(got[f]));
    if (miss.length) bad.push(k + ': ' + miss.map(f => f + ' README ' + readme[f] + ' rig ' + got[f]).join('; ') + ' [designed ' + designed.toFixed(3) + ', bind top ' + top.toFixed(3) + ', idle crown ' + C.gameplay(k).figure.height_m + ']');
    else ok++;
  }
  log('  README cast rows: ' + rows.length + '; equal to the rig on every column ' + ok);
  for (const s of bad) log('  ' + s);
  const npc = C.NPCS.map(k => C.buildOf(k).b), cov = (f, all) => { const s = new Set(npc.map(b => b[f])); return all.filter(x => !s.has(x)); };
  const frames = []; for (const s of ['m', 'f']) for (const fr of Object.keys(C.FRAMES[s])) frames.push(s + ':' + fr);
  const fs2 = new Set(npc.map(b => b.sex + ':' + b.frame)), swimN = npc.filter(b => C.GARMENTS[b.garment].swim).map(b => b.label + ' ' + b.garment);
  const claims = { beardsMissing: cov('beard', C.OPTIONS.beard), stylesMissing: cov('hairStyle', C.OPTIONS.hairStyle), skinsMissing: cov('skin', C.OPTIONS.skin), framesMissing: frames.filter(x => !fs2.has(x)), swimwear: swimN };
  for (const [k, v] of Object.entries(claims)) log('  NPCs ' + k + ': ' + (v.length ? v.join(', ') : 'none'));
  const cell = KEYS.map(k => (MINE[k] || []).find(r => r.id === 'cell')).filter(Boolean).map(r => r.value);
  log('  cell values: ' + [...new Set(cell)].join(' | ') + '; under 4 px: ' + KEYS.filter(k => { const r = (MINE[k] || []).find(x => x.id === 'cell'); return r && parseInt(r.value) < 4; }).map(k => k + ' ' + (MINE[k].find(x => x.id === 'cell').value)).join(', '));
  log('  aim (look detail, share 1): ' + KEYS.map(k => { const r = (MINE[k] || []).find(x => x.id === 'look'), m = r && /within ([\d.]+)°/.exec(r.detail); return k + ' ' + (m ? m[1] : '?'); }).join(', '));
  const look = KEYS.map(k => k + ' ' + ((MINE[k] || []).find(r => r.id === 'look') || {}).value);
  log('  look values: ' + look.join(', '));
  const lk = (MINE.fisher || []).find(r => r.id === 'look'); if (lk) log('  look detail (fisher): ' + lk.detail);
  const hz = (MINE.fisher || []).find(r => r.id === 'heights'); if (hz) log('  heights detail (fisher): ' + hz.detail);
  const fc = (MINE.girl || []).find(r => r.id === 'face'); if (fc) log('  face (girl): ' + fc.value + ' — ' + fc.detail);
  const bd = (MINE.fisher || []).find(r => r.id === 'budget'); if (bd) log('  budget (fisher, reported): ' + bd.value + ' — ' + String(bd.detail).slice(0, 300));
  SUM.claims = { tableRows: rows.length, tableOk: ok, tableBad: bad, npc: claims, cell: [...new Set(cell)], look };
}

// ---------- I. the steps between frames (the 09-30 send-back, 5.2) ----------
if (want('steps')) {
  log('\n== I. bone steps between adjacent frames, every clip of every build (exportBuild)');
  const ang = (q, r) => { const d = Math.min(1, Math.abs(q[0] * r[0] + q[1] * r[1] + q[2] * r[2] + q[3] * r[3])); return 2 * Math.acos(d) * 180 / Math.PI; };
  const tot = { steps: 0, over90: 0 }; let worst = { a: 0 }, worstBody = { a: 0 }; const fisher = { steps: 0, over90: 0, worst: { a: 0 }, list: {} };
  for (const k of KEYS) {
    const E = C.exportBuild(k);
    for (const c of E.clips) for (let i = 0; i + 1 < c.tracks.length; i++) for (const bn of c.bones) {
      const a = ang(c.tracks[i].bones[bn].rot, c.tracks[i + 1].bones[bn].rot); tot.steps++; if (a > 90) tot.over90++;
      if (a > worst.a) worst = { a, k, clip: c.name, bn, f: i };
      if (!/^tool_|^carry_/.test(bn) && a > worstBody.a) worstBody = { a, k, clip: c.name, bn, f: i };
      if (k === 'fisher') { fisher.steps++; if (a > 90) { fisher.over90++; const key = c.name + ':' + bn; fisher.list[key] = (fisher.list[key] || 0) + 1; } if (a > fisher.worst.a) fisher.worst = { a, clip: c.name, bn, f: i }; }
    }
  }
  const castF = C.exportBuild('fisher').clips.find(c => c.name === 'cast');
  const castSteps = castF ? castF.tracks.slice(0, -1).map((t, i) => i + '>' + (i + 1) + ' R1 ' + ang(t.bones.tool_R_1.rot, castF.tracks[i + 1].bones.tool_R_1.rot).toFixed(1) + ' L1 ' + ang(t.bones.tool_L_1.rot, castF.tracks[i + 1].bones.tool_L_1.rot).toFixed(1)) : [];
  log('  all 30: ' + tot.steps + ' steps, ' + tot.over90 + ' over 90 deg; worst ' + worst.a.toFixed(1) + ' (' + worst.k + ' ' + worst.clip + ' ' + worst.bn + ' ' + worst.f + '>' + (worst.f + 1) + '); worst body joint ' + worstBody.a.toFixed(1) + ' (' + worstBody.k + ' ' + worstBody.clip + ' ' + worstBody.bn + ' ' + worstBody.f + '>' + (worstBody.f + 1) + ')');
  log('  the fisher: ' + fisher.steps + ' steps, ' + fisher.over90 + ' over 90; worst ' + fisher.worst.a.toFixed(1) + ' (' + fisher.worst.clip + ' ' + fisher.worst.bn + ' ' + fisher.worst.f + '>' + (fisher.worst.f + 1) + ')');
  log('  the fisher, over 90 by clip:bone: ' + Object.entries(fisher.list).sort((a, b) => b[1] - a[1]).slice(0, 14).map(([k, v]) => k + ' ' + v).join(', '));
  log('  the fisher\'s cast, the rod sockets: ' + castSteps.join(' · '));
  SUM.steps = { all: tot, worst, worstBody, fisher: { steps: fisher.steps, over90: fisher.over90, worst: fisher.worst }, cast: castSteps };
}

// ---------- J. the frozen 10.1 copy: its own symbol, and it leaves CharacterIso10 alone ----------
if (want('frozen')) {
  log('\n== J. Art/characterIsoRig10_1.js (frozen 10.1, the review page only)');
  const ctx = vm.createContext({ console });
  for (const f of ['characterIsoRig10_1.js', 'characterIsoRig10_1.poses.js']) vm.runInContext(fs.readFileSync(path.join(K, 'Art', f), 'utf8'), ctx, { filename: f });
  const F = ctx.CharacterIso10_1, syms = Object.keys(ctx).filter(k => /^CharacterIso/.test(k));
  const before = globalThis.CharacterIso10;
  for (const f of ['characterIsoRig10_1.js', 'characterIsoRig10_1.poses.js']) vm.runInThisContext(fs.readFileSync(path.join(K, 'Art', f), 'utf8'), { filename: f });
  const kept = globalThis.CharacterIso10 === before && globalThis.CharacterIso10.revision === '10.2';
  log('  symbols it defines: ' + syms.join(',') + '; revision ' + (F && F.revision) + '; loaded after 10.2 in one page, CharacterIso10 ' + (kept ? 'unchanged (10.2)' : 'CHANGED'));
  if (!kept) fail('the 10.1 copy replaced CharacterIso10');
  SUM.frozen = { symbols: syms, revision: F && F.revision, keeps10_2: kept };
}

// ---------- K. against rig 9.2 on main (the 09-30 send-back's section 1: what the game reads, keep these) ----------
const R9 = process.env.R9 || 'C:/hh-gauntlet/main-read/docs/art/rigs/character/rig9/Art';
if (want('vs92') && fs.existsSync(path.join(R9, 'characterIsoRig9.js'))) {
  log('\n== K. against rig 9.2 (' + R9 + ', rig ' + sha(path.join(R9, 'characterIsoRig9.js')).slice(0, 8) + ')');
  const ctx = vm.createContext({ console });
  for (const f of ['characterIsoRig9.js', 'characterIsoRig9.poses.js', 'characterIsoRig9.checks.js']) if (fs.existsSync(path.join(R9, f))) vm.runInContext(fs.readFileSync(path.join(R9, f), 'utf8'), ctx, { filename: f });
  const N = ctx.CharacterIso9, J = (o) => String(JSON.stringify(o, (k, v) => typeof v === 'function' ? 'fn' : v));
  // A changed table names its changed keys; a changed list of names, what it added and removed.
  const isObj = (x) => x && typeof x === 'object' && !Array.isArray(x), isNames = (x) => Array.isArray(x) && x.every(y => typeof y === 'string');
  const rows = {}, put = (name, a, b) => {
    let r;
    if (J(a) === J(b)) r = 'SAME';
    else if (isObj(a) && isObj(b)) r = 'CHANGED at ' + [...new Set([...Object.keys(a), ...Object.keys(b)])].filter(k => J(a[k]) !== J(b[k]))
      .map(k => k + ': ' + String(J(a[k])).slice(0, 60) + ' -> ' + String(J(b[k])).slice(0, 60)).join('; ');
    else if (isNames(a) && isNames(b)) r = 'CHANGED: added ' + (b.filter(x => !a.includes(x)).join(',') || 'none') + '; removed ' + (a.filter(x => !b.includes(x)).join(',') || 'none');
    else r = 'CHANGED: 9.2 ' + String(J(a)).slice(0, 160) + ' -> 10.2 ' + String(J(b)).slice(0, 160);
    rows[name] = r; log('  ' + name + ': ' + r);
  };
  put('cell W, H, pivot', [N.W, N.H, N.pivot], [C.W, C.H, C.pivot]); put('PX, ELEV, order', [N.PX, N.ELEV, N.order], [C.PX, C.ELEV, C.order]);
  put('FACE_SLOTS', N.FACE_SLOTS, C.FACE_SLOTS); put('BLINK', N.BLINK, C.BLINK); put('LOOK', N.LOOK, C.LOOK); put('SHADING', N.SHADING, C.SHADING);
  put('ANIMS', N.ANIMS, C.ANIMS); put('clipNames', N.clipNames(), C.clipNames()); put('SOCKETS', N.SOCKETS, C.SOCKETS); put('HUMANOID', N.HUMANOID, C.HUMANOID);
  put('CONTRACT', N.CONTRACT, C.CONTRACT); put('CARRIES', N.CARRIES, C.CARRIES); put('GROUP_ORDER', N.GROUP_ORDER, C.GROUP_ORDER);
  put('the ten presets (9.2 CAST, 10.2 CAST10)', N.CAST, C.CAST10); put('FIELDS', N.FIELDS, C.FIELDS);
  put('skeleton ids and parents', N.skeleton('fisher').map(b => b.id + '<' + b.parent), C.skeleton('fisher').map(b => b.id + '<' + b.parent));
  rows.apiOnly92 = Object.keys(N).filter(k => !(k in C)); rows.apiOnly10 = Object.keys(C).filter(k => !(k in N));
  log('  API only in 9.2: ' + rows.apiOnly92.join(',') + ' · only in 10.2: ' + rows.apiOnly10.join(','));
  const g9 = N.gameplay('fisher').figure, g10 = C.gameplay('fisher').figure;
  rows.fisher = { height92: g9.height_m, height10: g10.height_m, radius92: g9.collision_radius_m, radius10: g10.collision_radius_m, irises: N.palettes && N.palettes.EYES ? N.palettes.EYES.sea + ' -> ' + C.palettes.EYES.sea : C.palettes.EYES.sea };
  log('  the fisher in idle: crown ' + g9.height_m + ' -> ' + g10.height_m + ' m, collider radius ' + g9.collision_radius_m + ' -> ' + g10.collision_radius_m + ' m; irises ' + rows.fisher.irises);
  const ang = (q, r) => { const d = Math.min(1, Math.abs(q[0] * r[0] + q[1] * r[1] + q[2] * r[2] + q[3] * r[3])); return 2 * Math.acos(d) * 180 / Math.PI; };
  const steps = (R, keys) => { let n = 0, o = 0, ob = 0, wb = { a: 0 }; for (const k of keys) { const E = R.exportBuild(k); for (const c of E.clips) for (let i = 0; i + 1 < c.tracks.length; i++) for (const bn of c.bones) {
    const a = ang(c.tracks[i].bones[bn].rot, c.tracks[i + 1].bones[bn].rot); n++; if (a > 90) { o++; if (!/^tool_|^carry_/.test(bn)) { ob++; if (a > wb.a) wb = { a, k, clip: c.name, bn, f: i }; } } } } return { n, o, ob, wb }; };
  const s9 = steps(N, N.CAST), s10 = steps(C, C.CAST10);
  const fmt = (s) => s.n + ' steps, ' + s.o + ' over 90 deg (body joints ' + s.ob + ', worst ' + s.wb.a.toFixed(1) + ' ' + s.wb.clip + ' ' + s.wb.bn + ' ' + s.wb.f + '>' + (s.wb.f + 1) + ')';
  log('  steps between frames, the ten presets: 9.2 ' + fmt(s9) + ' · 10.2 ' + fmt(s10));
  rows.steps = { r92: s9, r102: s10 };
  SUM.vs92 = rows;
}

// ---------- L. the cell, unclipped ----------
// The kit's cell check measures inside the canvas, so a figure that runs past the edge reads 0 px, not cut. Here every frame's
// posed mesh goes through the rig's own proj, unclipped, against the cell; where it runs past the top or bottom by over 0.5 px,
// the frame is rendered again moved 4 px by camOf's heave, and the covered pixels beyond the old edge are the pixels cut.
// Reported, not failed: the kit's check gates none of these (the lying clips on a build over 1.545 m, the four mount clips).
if (want('cellcut')) {
  log('\n== L. the cell, unclipped: the posed mesh through proj against the ' + C.W + ' x ' + C.H + ' cell (every clip but the four mount clips, 8 facings)');
  const SKIP = { mountUp: 1, mountDown: 1, mountCab: 1, mountCabDown: 1 }, LYING = { swim: 1, sleep: 1, tread: 1 }, U = 4;
  const cams = [0, 1, 2, 3, 4, 5, 6, 7].map(d => C.camOf({ dir: d }));
  const worst = (build) => { const B = C.buildOf(build), w = { lying: { o: -99 }, other: { o: -99 } };
    for (const n of C.clipNames()) { const cd = C.clipDef(n); if (SKIP[cd.anim]) continue;
      for (let k = 0; k < C.ANIMS[cd.anim].frames; k++) { const faces = C.posed(C.evalClip(n, C.uOf(cd.anim, k), B), B);
        for (let d = 0; d < 8; d++) { let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
          for (const f of faces) for (const p of f.v) { const q = C.proj(p, cams[d]); if (q.sx < x0) x0 = q.sx; if (q.sx > x1) x1 = q.sx; if (q.sy < y0) y0 = q.sy; if (q.sy > y1) y1 = q.sy; }
          const o = Math.max(-x0, x1 - C.W, -y0, y1 - C.H), side = o === -x0 ? 'left' : o === x1 - C.W ? 'right' : o === -y0 ? 'top' : 'bottom', slot = LYING[cd.anim] ? w.lying : w.other;
          if (o > slot.o) Object.assign(slot, { o, n, k, d, side }); } } }
    for (const s of [w.lying, w.other]) { s.at = s.n + ' f' + s.k + ' ' + C.order[s.d] + ' ' + s.side; s.cut = null;
      if (s.o > 0.5 && (s.side === 'bottom' || s.side === 'top')) { const R = C.render({ build, clip: s.n, frame: s.k, dir: s.d, heave: s.side === 'bottom' ? U : -U }); s.cut = 0;
        for (let y = s.side === 'bottom' ? R.H - U : 0, y1 = s.side === 'bottom' ? R.H : U; y < y1; y++) for (let x = 0; x < R.W; x++) if (R.rgba[(y * R.W + x) * 4 + 3] > 0) s.cut++; } }
    return { h: B.D.heightM, lying: w.lying, other: w.other }; };
  const fmt = (k, s) => k + ' ' + s.at + ' ' + s.o.toFixed(2) + ' px' + (s.cut === null ? '' : ', ' + s.cut + ' px cut');
  const rows = {}, past = [], touch = [];
  for (const k of KEYS) { const w = worst(k); rows[k] = { heightM: +w.h.toFixed(3), lying: w.lying, other: w.other };
    for (const s of [w.lying, w.other]) { if (s.o > 0.5) past.push(fmt(k, s)); else if (s.o > 0) touch.push(fmt(k, s)); } }
  log('  presets past the cell (over 0.5 px): ' + (past.join(' · ') || 'none'));
  log('  presets at its edge (0 to 0.5 px over, a pixel centre not reached): ' + (touch.join(' · ') || 'none'));
  const tallest = KEYS.filter(k => rows[k].heightM > 1.545).length;
  log('  the lying clips go ungated on ' + tallest + ' of ' + KEYS.length + ' presets (heightM over 1.545 m, 9.2\'s Fisher)');
  const RC = +(process.env.RC || 24), r3 = mulberry32(7), rr = { n: RC, pastLying: 0, pastOther: 0, worstLying: null, worstOther: null };
  for (let i = 0; i < RC; i++) { const b = C.randomBuild(r3), w = worst(b);
    if (w.lying.o > 0.5) rr.pastLying++; if (w.other.o > 0.5) rr.pastOther++;
    if (!rr.worstLying || w.lying.o > rr.worstLying.o) rr.worstLying = { o: +w.lying.o.toFixed(2), at: w.lying.at, cut: w.lying.cut, heightM: +w.h.toFixed(3) };
    if (!rr.worstOther || w.other.o > rr.worstOther.o) rr.worstOther = { o: +w.other.o.toFixed(2), at: w.other.at, cut: w.other.cut, heightM: +w.h.toFixed(3) }; }
  log('  ' + RC + ' random builds (the same as G): past the cell in a lying clip ' + rr.pastLying + ', in another clip ' + rr.pastOther + '; worst lying ' + JSON.stringify(rr.worstLying) + '; worst other ' + JSON.stringify(rr.worstOther));
  SUM.cellcut = { presets: rows, random: rr };
}

const tag = ONLY.length ? '-' + ONLY.join('-') : '';
// The RESULT line goes into both files too, so a copied log carries its own verdict.
const RES = 'RESULT: ' + (FAIL.length ? 'FAIL ' + FAIL.length : 'PASS') + ' (' + Object.keys(SUM).join(', ') + ')';
fs.writeFileSync(path.join(OUT, 'node-check' + tag + '.json'), JSON.stringify(Object.assign({ result: RES, failures: FAIL }, SUM), null, 1) + '\n');
LOG.push(RES);
fs.writeFileSync(path.join(OUT, 'node-check' + tag + '.txt'), LOG.join('\n') + '\n');
console.log(RES);
