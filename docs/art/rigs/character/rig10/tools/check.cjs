#!/usr/bin/env node
/* Hidden Harbours — character kit 10.3: tools/check.cjs, the kit's one checker. Node 18 or later, built-ins only.
     node tools/check.cjs              check the kit as it is (about 3 minutes; the golden suite is most of it)
     node tools/check.cjs --quick      skip the golden suite (sums, data files, renders only)
     node tools/check.cjs --random 60  also run every gate on CharacterIso10.randomBuild(mulberry32(seed)), seeds 1..60 (reported, not gated)
     node tools/check.cjs --write      regenerate every generated file (builds, gameplay, presets, options, shading, golden report,
                                       renders and their manifest), then SHA256SUMS.txt, then check
   It loads Art/characterIsoRig10.js, .poses.js and .checks.js with vm.runInThisContext, as a page's <script> tags do, and makes every
   file from tools/kit.js. The last line it prints starts with RESULT:. Exit code 0 on PASS. */
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm'), crypto = require('crypto'), zlib = require('zlib');
const ROOT = path.resolve(__dirname, '..'), abs = (p) => path.join(ROOT, p), argv = process.argv.slice(2);
const WRITE = argv.includes('--write'), QUICK = argv.includes('--quick'), RI = argv.indexOf('--random'), RANDOM = RI >= 0 ? Math.max(1, parseInt(argv[RI + 1], 10) || 60) : 0;
const sha = (b) => crypto.createHash('sha256').update(b).digest('hex'), shaFile = (p) => sha(fs.readFileSync(abs(p)));
const say = (s) => process.stdout.write(s + '\n'), problems = [], notes = [];
const RIG = ['Art/characterIsoRig10.js', 'Art/characterIsoRig10.poses.js', 'Art/characterIsoRig10.checks.js'];
for (const f of RIG) vm.runInThisContext(fs.readFileSync(abs(f), 'utf8'), { filename: f });
const C = globalThis.CharacterIso10, K = require('./kit.js');
const st = { rig: shaFile(RIG[0]), poses: shaFile(RIG[1]), checks: shaFile(RIG[2]) };
say('character kit check — ' + C.rig + ' ' + C.revision + ', node ' + process.version);
say('rig ' + st.rig + '\nposes ' + st.poses + '\nchecks ' + st.checks);
const t0 = Date.now(), secs = () => ((Date.now() - t0) / 1000).toFixed(0) + ' s';
const writeText = (p, s) => { fs.mkdirSync(path.dirname(abs(p)), { recursive: true }); fs.writeFileSync(abs(p), s); };
const firstDiff = (a, b) => { const n = Math.min(a.length, b.length); for (let i = 0; i < n; i++) if (a[i] !== b[i]) return i; return n; };

/* 1. the data files */
const DATA = [['builds/presets.json', () => K.presetsFile(C, st)], ['data/options.v10.json', () => K.optionsFile(C, st)], ['data/shading.v10.json', () => K.shadingFile(C, st)]];
for (const k of C.CAST) { DATA.push(['builds/' + k + '.v10.json', () => K.buildFile(C, k, st)]); DATA.push(['gameplay/characterIsoRig10.' + k + '.gameplay.json', () => K.gameplayFile(C, k, st)]); }
let dataSame = 0;
for (const [p, make] of DATA) { const s = make();
  if (WRITE) { writeText(p, s); dataSame++; continue; }
  if (!fs.existsSync(abs(p))) { problems.push(p + ': missing'); continue; }
  const f = fs.readFileSync(abs(p), 'utf8'); if (f === s) { dataSame++; continue; }
  const i = firstDiff(f, s); problems.push(p + ': DIFFERS from its regeneration at character ' + i + ' (file ' + JSON.stringify(f.slice(Math.max(0, i - 40), i + 40)) + ', regenerated ' + JSON.stringify(s.slice(Math.max(0, i - 40), i + 40)) + ')'); }
say('data files: ' + dataSame + ' / ' + DATA.length + (WRITE ? ' written' : ' identical to their regeneration') + ' (' + secs() + ')');

/* 2. the renders */
const plan = K.renderPlan(C), entries = []; let rSame = 0, rHash = 0;
const manOld = !WRITE && fs.existsSync(abs('renders/manifest.json')) ? JSON.parse(fs.readFileSync(abs('renders/manifest.json'), 'utf8')) : null;
const byFile = {}; if (manOld) for (const e of manOld.images) byFile[e.file] = e;
for (const p of plan) { const g = p.make(), rgba = K.canon(g.rgba), rh = sha(rgba);
  const e = { file: p.file, kind: p.kind, preset: p.preset, label: p.preset ? C.normBuild(p.preset).label : 'the cast', scale: p.scale, width: g.W, height: g.H, rgbaSha256: rh, pngSha256: null };
  if (p.kind === 'look') e.turns = K.cellsOf(C, 'look', p.preset).map((r) => r.map((c) => Object.assign({ look: c.look, eyes: c.face.eyes }, c.target ? { target: c.target } : {})));
  if (WRITE) { const png = K.pngBytes(g.W, g.H, zlib.deflateSync(Buffer.from(K.scanlines(g.W, g.H, rgba)), { level: 9 })); fs.mkdirSync(path.dirname(abs(p.file)), { recursive: true }); fs.writeFileSync(abs(p.file), png); e.pngSha256 = sha(png); rSame++; rHash++; entries.push(e); continue; }
  const m = byFile[p.file]; if (!m) { problems.push(p.file + ': not in renders/manifest.json'); continue; }
  if (m.rgbaSha256 === rh) rHash++; else problems.push(p.file + ': a fresh render\'s rgbaSha256 differs from the manifest');
  if (!fs.existsSync(abs(p.file))) { problems.push(p.file + ': missing'); continue; }
  const bytes = new Uint8Array(fs.readFileSync(abs(p.file))); let d;
  try { d = K.decodePNG(bytes, (z) => new Uint8Array(zlib.inflateSync(Buffer.from(z)))); } catch (err) { problems.push(p.file + ': ' + err.message); continue; }
  if (d.W !== g.W || d.H !== g.H) { problems.push(p.file + ': ' + d.W + 'x' + d.H + ', a fresh render is ' + g.W + 'x' + g.H); continue; }
  let px = 0; const dr = K.canon(d.rgba); for (let i = 0; i < dr.length; i += 4) if (dr[i] !== rgba[i] || dr[i + 1] !== rgba[i + 1] || dr[i + 2] !== rgba[i + 2] || dr[i + 3] !== rgba[i + 3]) px++;
  if (px) problems.push(p.file + ': ' + px + ' px differ from a fresh render'); else rSame++;
  if (m.pngSha256 !== sha(bytes)) notes.push(p.file + ': pngSha256 is not the file\'s'); }
if (WRITE) writeText('renders/manifest.json', K.manifestFile(C, st, entries));
else if (manOld) { const listed = new Set(plan.map((p) => p.file)); for (const e of manOld.images) if (!listed.has(e.file)) problems.push(e.file + ': in the manifest, not in the render plan');
  for (const k of ['derivedFromRigSha256', 'posesDerivedFromRigSha256']) if (manOld[k] !== (k === 'derivedFromRigSha256' ? st.rig : st.poses)) problems.push('renders/manifest.json: ' + k + ' is not the rig\'s'); }
say('renders: ' + rSame + ' / ' + plan.length + (WRITE ? ' written' : ' pixel-identical to a fresh render') + ', ' + rHash + ' / ' + plan.length + ' rgbaSha256 as the manifest (' + secs() + ')');

/* 3. the golden suite on the thirty */
let gated = 0, gPass = 0;
if (!QUICK) { const results = {};
  for (const k of C.CAST) { results[k] = K.runBuild(C, k); const g = results[k].filter((r) => r.gate); gated += g.length; gPass += g.filter((r) => r.pass).length;
    for (const r of g) if (!r.pass) problems.push('golden: ' + k + ' · ' + r.id + ' fails (' + r.value + ')'); }
  const s = K.goldenFile(C, st, results);
  if (WRITE) writeText('golden-report.json', s);
  else { const f = fs.existsSync(abs('golden-report.json')) ? fs.readFileSync(abs('golden-report.json'), 'utf8') : '';
    if (f !== s) { const F = f ? JSON.parse(f) : { builds: {} }, S = JSON.parse(s); let flags = 0, text = 0;
      for (const k of C.CAST) (S.builds[k].checks).forEach((r, i) => { const o = F.builds[k] && F.builds[k].checks[i]; if (!o || o.pass !== r.pass) flags++; else if (o.value !== r.value || o.detail !== r.detail || o.title !== r.title) { text++; if (text <= 5) notes.push('golden text: ' + k + ' · ' + r.id + ': file "' + o.value + '", now "' + r.value + '"'); } });
      problems.push('golden-report.json: DIFFERS from its regeneration (' + flags + ' pass flags, ' + text + ' rows of printed text)'); } }
  say('golden suite: ' + gPass + ' / ' + gated + ' gated checks pass on ' + C.CAST.length + ' builds (' + secs() + ')'); }

/* 4. SHA256SUMS.txt over every other file */
const files = []; (function walk(d) { for (const e of fs.readdirSync(abs(d || '.'), { withFileTypes: true })) { const p = d ? d + '/' + e.name : e.name; if (e.isDirectory()) walk(p); else if (p !== 'SHA256SUMS.txt') files.push(p); } })('');
files.sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));
if (WRITE) writeText('SHA256SUMS.txt', files.map((p) => shaFile(p) + '  ' + p).join('\n') + '\n');
let sumsOk = 0; const sums = fs.existsSync(abs('SHA256SUMS.txt')) ? fs.readFileSync(abs('SHA256SUMS.txt'), 'utf8').split('\n').filter(Boolean).map((l) => { const m = /^([0-9a-f]{64}) {2}(.+)$/.exec(l); return m ? [m[1], m[2]] : null; }) : [];
const inSums = new Set(); for (const s of sums) { if (!s) { problems.push('SHA256SUMS.txt: a line is not "<sha256>  <path>"'); continue; } inSums.add(s[1]);
  if (!fs.existsSync(abs(s[1]))) problems.push(s[1] + ': in SHA256SUMS.txt, missing'); else if (shaFile(s[1]) !== s[0]) problems.push(s[1] + ': SHA-256 differs from SHA256SUMS.txt'); else sumsOk++; }
for (const p of files) if (!inSums.has(p)) problems.push(p + ': not in SHA256SUMS.txt');
say('sums: ' + sumsOk + ' / ' + files.length + ' files match SHA256SUMS.txt (' + secs() + ')');

/* 5. the random sweep (reported) */
if (RANDOM) { const rows = K.randomSweep(C, RANDOM), bad = rows.filter((r) => r.fails.length), by = {};
  for (const r of bad) for (const f of r.fails) (by[f.id] = by[f.id] || []).push(r.seed);
  say('random builds (mulberry32 seeds 1..' + RANDOM + ', every gate, reported): ' + bad.length + ' / ' + RANDOM + ' fail at least one gate' + (bad.length ? ': ' + Object.entries(by).map(([k, v]) => k + ' ' + v.length + ' (seeds ' + v.join(', ') + ')').join('; ') : '') + ' (' + secs() + ')'); }

for (const n of notes.slice(0, 20)) say('note: ' + n);
for (const p of problems.slice(0, 60)) say('PROBLEM: ' + p);
if (problems.length > 60) say('PROBLEM: … ' + (problems.length - 60) + ' more');
const head = (QUICK ? '' : gPass + '/' + gated + ' gates, ') + dataSame + '/' + DATA.length + ' data files, ' + rSame + '/' + plan.length + ' renders, ' + sumsOk + '/' + files.length + ' sums';
say(problems.length ? 'RESULT: FAIL — ' + problems.length + ' problem' + (problems.length === 1 ? '' : 's') + ' (' + head + ')' : 'RESULT: PASS — ' + head + (WRITE ? ' (written by this run)' : ''));
process.exitCode = problems.length ? 1 : 0;
