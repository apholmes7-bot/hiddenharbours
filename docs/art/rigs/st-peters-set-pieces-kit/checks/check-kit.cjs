'use strict';
/* check-kit.cjs — the St Peters set pieces kit. Node 18+, built-ins only (fs, path, vm, crypto).
     node st-peters-set-pieces-kit/checks/check-kit.cjs
   Loads lib/weatherSky.js, lib/coastalPass.js and stPetersSetPieces.js into one vm context, in that order, and checks every
   piece at every facing through the kit's own rasteriser. One PASS or FAIL line per check, then one RESULT line.        */
const fs = require('fs'), path = require('path'), vm = require('vm'), crypto = require('crypto');
const KIT = path.resolve(__dirname, '..');
const ctx = vm.createContext({ console });
for (const f of ['lib/weatherSky.js', 'lib/coastalPass.js', 'stPetersSetPieces.js']) vm.runInContext(fs.readFileSync(path.join(KIT, f), 'utf8'), ctx, { filename: f });
const SP = ctx.StPetersSetPieces, WS = ctx.WeatherSky;
let n = 0, bad = 0; const row = (ok, id, msg) => { n++; if (!ok) bad++; console.log((ok ? 'PASS' : 'FAIL') + '  ' + id + (msg ? '  ' + msg : '')); };
const sha = (a) => crypto.createHash('sha256').update(Buffer.from(a.buffer, a.byteOffset, a.byteLength)).digest('hex');
const SKY = WS.at({ time: 14, cloud: 0.1, wind: 0.3 }), NIGHT = WS.at({ time: 22.5, cloud: 0.05 });
const KEY = [0x10, 0x1d, 0x21];
row(SP.VERSION === '1.0.0', 'kit.version', SP.VERSION);
// the facings table is measured: the show face's normal, projected, at every dir; dir 4 faces the camera (S)
const F = SP.facings(); let fok = F.length === 8 && F[4].showFaces === 'S';
for (const q of F) { const B = SP.basis(q.dir), wx = -B.st, wy = B.ct, deg = (Math.atan2(wx, wy) * 180 / Math.PI + 360) % 360; if (SP.NAMES[Math.round(deg / 45) % 8] !== q.showFaces) fok = false; }
row(fok, 'kit.facings', F.map(q => q.dir + ':' + q.showFaces).join(' '));
// ramps: six steps, dark to light
let rok = true; for (const [k, r] of Object.entries(SP.RAMPS)) { if (r.length !== 6) rok = false; const L = r.map(h => { const v = [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16)); return 0.2126 * v[0] + 0.7152 * v[1] + 0.0722 * v[2]; }); for (let i = 1; i < 6; i++) if (L[i] < L[i - 1]) rok = false; }
row(rok, 'kit.ramps', Object.keys(SP.RAMPS).length + ' ramps, six steps each, dark to light');
for (const key of SP.KEYS) {
  const P = SP.PIECES[key];
  // every facing builds, draws something, fits a 2048 sheet cell, and repeats byte for byte
  let empty = [], big = [], diff = [], ring = [];
  for (let d = 0; d < 8; d++) {
    const r1 = SP.render(key, d, { sky: SKY }), r2 = SP.render(key, d, { sky: SKY });
    let px = 0; for (let i = 3; i < r1.rgba.length; i += 4) if (r1.rgba[i]) px++;
    if (!px) empty.push(d); if (r1.w > 2048 || r1.h > 2048) big.push(d + ':' + r1.w + 'x' + r1.h); if (sha(r1.rgba) !== sha(r2.rgba)) diff.push(d);
    for (let i = 0; i < r1.rgba.length; i += 4) if (r1.rgba[i + 3] && r1.rgba[i] === KEY[0] && r1.rgba[i + 1] === KEY[1] && r1.rgba[i + 2] === KEY[2]) { ring.push(d); break; }
  }
  row(!empty.length, key + '.draws', empty.length ? 'empty at dir ' + empty.join(',') : 'all 8 facings');
  row(!big.length, key + '.cell', big.length ? big.join(' ') : 'every cell within 2048');
  row(!diff.length, key + '.repeats', diff.length ? 'differs at dir ' + diff.join(',') : 'byte-identical twice at all 8');
  row(!ring.length, key + '.ringless', ring.length ? 'keyline colour at dir ' + ring.join(',') : 'no #101d21 keyline by default (ADR 0031)');
  const ab = SP.render(key, 4, { sky: SKY, outline: true }); let kp = 0; for (let i = 0; i < ab.rgba.length; i += 4) if (ab.rgba[i + 3] && ab.rgba[i] === KEY[0] && ab.rgba[i + 1] === KEY[1] && ab.rgba[i + 2] === KEY[2]) kp++;
  row(kp > 0, key + '.keyline_ab', kp + ' keyline px with {outline:true} (the positive control)');
  // the sidecar is exactly what gameplay() writes today
  const file = path.join(KIT, 'gameplay', P.id + '.gameplay.json'), want = JSON.stringify(SP.gameplay(key), null, 1) + '\n';
  row(fs.existsSync(file) && fs.readFileSync(file, 'utf8') === want, key + '.sidecar', path.relative(KIT, file) + (fs.existsSync(file) ? '' : ' missing'));
  // colliders: a raised piece carries a footprint; a floor piece is walked on; a wall piece takes its wall's
  const gp = SP.gameplay(key), area = (pts) => Math.abs(pts.reduce((a, p, i) => { const q = pts[(i + 1) % pts.length]; return a + p[0] * q[1] - q[0] * p[1]; }, 0)) / 2;
  const cok = gp.layer === 'floor' ? gp.walk === 'on' && !gp.colliders.length : gp.mount === 'wall' ? !gp.colliders.length : gp.colliders.length > 0 && gp.colliders.every(c => area(c.pts) > 0.01);
  row(cok, key + '.collider', gp.layer + ' · walk ' + gp.walk + ' · ' + gp.colliders.length + ' polygon(s)');
  // words are data: every lettered board is blank, tagged, and has its string id and default string in anchors()
  const W = (gp.anchors.words || []); let wok = W.every(w => w.id && typeof w.text === 'string' && (w.corners || w.at));
  const B0 = SP.built(key, {}); for (const w of W) if (w.corners && !B0.b.faces.some(f => f.tag === w.id)) wok = false;
  row(wok, key + '.words', W.length ? W.map(w => w.id).join(', ') : 'no words');
  // light at night: emitters glow only when lit
  if (P.id === 'structure.stp_cannery_boiler_stack') { const a = SP.render(key, 5, { sky: NIGHT, decay: 'restored', lit: 0 }), b = SP.render(key, 5, { sky: NIGHT, decay: 'restored', lit: 1 }); row(sha(a.rgba) !== sha(b.rgba), key + '.window', 'the restored window glows when lit, and only then'); }
}
// anything that moves is a function of its inputs: the vane turns with the wind it is given; the smoke with the clock
const v0 = SP.render('harbourVane', 0, { sky: SKY, windFromDeg: 0 }), v1 = SP.render('harbourVane', 0, { sky: SKY, windFromDeg: 90 }), v0b = SP.render('harbourVane', 0, { sky: SKY, windFromDeg: 0 });
row(sha(v0.rgba) !== sha(v1.rgba) && sha(v0.rgba) === sha(v0b.rgba), 'harbourVane.turns', 'windFromDeg 0 and 90 differ; 0 twice is identical');
const s1 = JSON.stringify(SP.smoke(20.5, { w: 0.4, dir: 1 }, 7)), s2 = JSON.stringify(SP.smoke(20.5, { w: 0.4, dir: 1 }, 7)), s3 = JSON.stringify(SP.smoke(20.51, { w: 0.4, dir: 1 }, 7));
row(s1 === s2 && s1 !== s3, 'smoke.time', 'a pure function of (game time, wind, seed)');
// the tide board's marks: chart datum, whole metres red with numerals as data, the spring high flagged
const tb = SP.anchors('slipTideBoard', 4, { foot: -1.69, datum: -2.2, top: 2.6 }), marks = tb.marks || [];
row(marks.some(m => m.kind === 'spring high' && m.z === 2.2) && marks.filter(m => m.label).every(m => Math.abs(m.z + 2.2 - +m.label) < 1e-6), 'slipTideBoard.marks', marks.filter(m => m.label).map(m => m.label + '@' + m.z).join(' ') + ' · spring high +2.20');
console.log(bad ? 'RESULT: FAIL - ' + bad + ' of ' + n + ' checks failed' : 'RESULT: OK - all ' + n + ' checks pass');
process.exitCode = bad ? 1 : 0;
