#!/usr/bin/env node
'use strict';
// Phase A only. Read the checked-in kit; write evidence outside the kit and Assets.
const fs = require('node:fs'), path = require('node:path'), vm = require('node:vm');
const crypto = require('node:crypto'), zlib = require('node:zlib'), assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const kit = path.join(root, 'docs/art/rigs/character/rig10');
const out = path.join(root, 'artifacts/pixel-look-a');
fs.mkdirSync(out, { recursive: true });
const sha = b => crypto.createHash('sha256').update(b).digest('hex');
for (const f of ['characterIsoRig10.js', 'characterIsoRig10.poses.js', 'characterIsoRig10.checks.js'])
  vm.runInThisContext(fs.readFileSync(path.join(kit, 'Art', f), 'utf8'), { filename: f });
const C = globalThis.CharacterIso10, K = require(path.join(kit, 'tools/kit.js'));
const manifest = JSON.parse(fs.readFileSync(path.join(kit, 'renders/manifest.json')));
const byFile = new Map(manifest.images.map(x => [x.file, x]));
assert.equal(C.PX, 32);
assert.equal(C.CAST.length, 30);
// Explicit px:32 control, through the kit's own renderPlan, before any measurement.
const explicit32 = Object.assign({}, C, { render: o => C.render({ ...o, px: 32 }) });
let controls = 0;
for (const p of K.renderPlan(explicit32).filter(p => p.scale === 1)) {
  const r = p.make(), m = byFile.get(p.file);
  assert.equal(r.W, m.width); assert.equal(r.H, m.height);
  assert.equal(sha(K.canon(r.rgba)), m.rgbaSha256, p.file);
  const shipped = K.decodePNG(fs.readFileSync(path.join(kit, p.file)), z => zlib.inflateSync(z));
  assert.equal(sha(K.canon(shipped.rgba)), m.rgbaSha256, 'shipped ' + p.file);
  controls++;
}
assert.equal(controls, 120);
console.log('CONTROL PASS: 120/120 explicit px:32 renderPlan RGBA hashes and shipped PNGs');
const nearest = (r, s) => {
  const W = r.W * s, H = r.H * s, rgba = new Uint8Array(W * H * 4);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const i = (Math.floor(y / s) * r.W + Math.floor(x / s)) * 4;
    rgba.set(r.rgba.subarray(i, i + 4), (y * W + x) * 4);
  }
  return { W, H, rgba };
};
// Keep literal INK palette retention separate from depth-contour retention.
const inkRGB = new Set(C.INK.map(x => parseInt(x.slice(1), 16)));
const isInk = (r, i) => r.rgba[i * 4 + 3] && inkRGB.has((r.rgba[i * 4] << 16) | (r.rgba[i * 4 + 1] << 8) | r.rgba[i * 4 + 2]);
const isContour = (r, i) => r.mat[i] >= 0 && r.step[i] < r.lit[i];
function compare(lo, hi, s) {
  assert.equal(hi.W, lo.W * s); assert.equal(hi.H, lo.H * s);
  const n = { union: 0, reference: 0, different: 0, differentReference: 0, ink: 0, inkKept: 0, contour: 0, contourKept: 0 };
  for (let y = 0; y < hi.H; y++) for (let x = 0; x < hi.W; x++) {
    const a = Math.floor(y / s) * lo.W + Math.floor(x / s), b = y * hi.W + x;
    const aa = lo.rgba[a * 4 + 3], ba = hi.rgba[b * 4 + 3];
    if (!aa && !ba) continue;
    n.union++; if (aa) n.reference++;
    let diff = false;
    for (let c = 0; c < 4; c++) if (lo.rgba[a * 4 + c] !== hi.rgba[b * 4 + c]) diff = true;
    if (diff) { n.different++; if (aa) n.differentReference++; }
    if (isInk(lo, a)) { n.ink++; if (isInk(hi, b)) n.inkKept++; }
    if (isContour(lo, a)) { n.contour++; if (isContour(hi, b)) n.contourKept++; }
  }
  return n;
}
const rows = [], sums = {};
const t0 = Date.now();
for (const build of C.CAST) {
  for (const kind of ['idle8', 'walkS']) for (const cell of K.cellsOf(C, kind, build).flat()) {
    const lo = C.render({ build, ...cell, px: 32 });
    for (const s of [3, 4, 5, 6]) {
      const hi = C.render({ build, ...cell, px: 32 * s });
      const n = compare(lo, hi, s);
      rows.push({ build, kind, ...cell, scale: s, px: 32 * s, ...n });
      for (const key of [String(s), kind + ':' + s]) {
        sums[key] ||= Object.fromEntries(Object.keys(n).map(k => [k, 0]));
        for (const k of Object.keys(n)) sums[key][k] += n[k];
      }
    }
  }
  console.log('measured ' + build + ' (' + Math.round((Date.now() - t0) / 1000) + 's)');
}
assert.equal(rows.length, 1920);
const summary = Object.entries(sums).map(([key, n]) => ({ key, ...n,
  differentPct: 100 * n.different / n.union,
  differentReferencePct: 100 * n.differentReference / n.reference,
  inkKeptPct: n.ink ? 100 * n.inkKept / n.ink : null,
  contourKeptPct: n.contour ? 100 * n.contourKept / n.contour : null }));
const cost = [1, 2, 3, 4, 5, 6, 8].map(s => {
  const W = Math.ceil(1920 / s), H = Math.ceil(1080 / s), pixels = W * H;
  return { step: s, W, H, pixels, screenPct: 100 * pixels / (1920 * 1080), attachments24Bytes: pixels * 24 };
});
const evidence = { revision: C.revision, node: process.version, rigSha256: sha(fs.readFileSync(path.join(kit, 'Art/characterIsoRig10.js'))),
  controls, comparisons: rows.length, elapsedSeconds: (Date.now() - t0) / 1000,
  definitions: { different: 'Exact RGBA mismatch / union of nontransparent reference and high-resolution pixels; reference-only denominator also reported.',
    ink: 'Exact exported INK[0] or INK[1] RGB, alpha nonzero. Retention is same-location high-resolution INK coverage / nearest-enlarged reference INK coverage.',
    contour: 'step < lit in the rig renderer; same-location retention of the inner depth-break contour, reported separately.',
    weighting: 'Sum integer counts before division. No background dilution; no threshold, alignment search, or resampling of high-resolution output.',
    caveat: 'Rig 10.3 has no dither. These are reference-renderer mechanism measurements, not engine parity or GPU timing.' }, summary, cost, rows };
fs.writeFileSync(path.join(out, 'measurements.json'), JSON.stringify(evidence, null, 2) + '\n');
fs.writeFileSync(path.join(out, 'measurements.csv'), Object.keys(rows[0]).join(',') + '\n' + rows.map(r => Object.values(r).join(',')).join('\n') + '\n');
// Five builds, two states each. PNG strips remain local; HTML supplies unambiguous labels.
const chosen = C.CAST.slice(0, 5), labels = [];
const cw = C.W * 4, ch = C.H * 4, W = cw * 4, H = ch * chosen.length;
const rgba = new Uint8Array(W * H * 4);
chosen.forEach((build, row) => {
  const cells = [{ clip: 'idle', frame: 0, dir: 3 }, { clip: 'walk', frame: 3, dir: 4 }];
  cells.forEach((cell, state) => {
    const images = [nearest(C.render({ build, ...cell, px: 32 }), 4), C.render({ build, ...cell, px: 128 })];
    images.forEach((r, side) => {
      for (let y = 0; y < ch; y++) rgba.set(r.rgba.subarray(y * cw * 4, (y + 1) * cw * 4), ((row * ch + y) * W + (state * 2 + side) * cw) * 4);
    });
  });
  labels.push(build);
});
fs.writeFileSync(path.join(out, 'contact-sheet.png'), K.pngBytes(W, H, zlib.deflateSync(K.scanlines(W, H, rgba))));
fs.writeFileSync(path.join(out, 'contact-sheet.html'), '<!doctype html><meta charset="utf-8"><title>Figure pixel target — rig 10.3 mechanism</title><style>body{background:#d4d5cf;color:#17201f;font:16px system-ui}img{image-rendering:pixelated;max-width:none}p{max-width:90ch}</style><h1>Rig 10.3 — mechanism only</h1><p>Rows, top to bottom: ' + labels.join(', ') + '.</p><p>Columns: idle frame 0 SE, 32 px/m enlarged ×4; same at 128 px/m; walk frame 3 S, 32 px/m enlarged ×4; same at 128 px/m. All default contour and head snap, no keyline, no sky or rock. No engine capture.</p><img src="contact-sheet.png" width="' + W + '" height="' + H + '">');
console.log(JSON.stringify({ controls, comparisons: rows.length, summary: summary.filter(x => !x.key.includes(':')), cost }, null, 2));
