'use strict';
/* check-kit.cjs — the Nine Mile Creek set pieces kit. Node 18+, built-ins only (fs, path, vm, crypto).
     node nmc-set-pieces-kit/checks/check-kit.cjs
   Loads lib/weatherSky.js, lib/coastalPass.js and nmcSetPieces.js into one vm context, in that order, and checks every
   piece at every facing through the kit's own rasteriser. One PASS or FAIL line per check, then one RESULT line.        */
const fs = require('fs'), path = require('path'), vm = require('vm'), crypto = require('crypto');
const KIT = path.resolve(__dirname, '..');
const ctx = vm.createContext({ console });
for (const f of ['lib/weatherSky.js', 'lib/coastalPass.js', 'nmcSetPieces.js']) vm.runInContext(fs.readFileSync(path.join(KIT, f), 'utf8'), ctx, { filename: f });
const SP = ctx.NmcSetPieces, WS = ctx.WeatherSky;
const { IMPORT_SIZE_CAP } = require('./write-today.cjs');
const today = JSON.parse(fs.readFileSync(path.resolve(KIT, '../../../../Assets/_Project/Art/Sprites/NineMileCreek/SetPieces/nmcSetPieces.today.json'), 'utf8'));
let n = 0, bad = 0; const row = (ok, id, msg) => { n++; if (!ok) bad++; console.log((ok ? 'PASS' : 'FAIL') + '  ' + id + (msg ? '  ' + msg : '')); };
const sha = (a) => crypto.createHash('sha256').update(Buffer.from(a.buffer, a.byteOffset, a.byteLength)).digest('hex');
const SKY = WS.at({ time: 14, cloud: 0.1, wind: 0.3 }), NIGHT = WS.at({ time: 22.5, cloud: 0.05 });
const KEY = [0x10, 0x1d, 0x21];
// Delivered stamps, pinned per key. Only these historical stamps and the final LF may differ.
const SIDECAR_STAMPS = {
  barStake: 'NmcSetPieces 1.3.0', bench: 'NmcSetPieces 1.3.0',
  boardwalk: 'NmcSetPieces 1.2.0', boardwalkLamp: 'NmcSetPieces 1.2.0',
  campTent: 'NmcSetPieces 1.3.0', cascade: 'NmcSetPieces 1.3.0',
  chandlery: 'NmcSetPieces 1.2.0', comfortStation: 'NmcSetPieces 1.3.0',
  creekDock: 'NmcSetPieces 1.4.0', crossingBoard: 'NmcSetPieces 1.3.0',
  fallsDeck: 'NmcSetPieces 1.3.0', fireRing: 'NmcSetPieces 1.3.0',
  guardRail: 'NmcSetPieces 1.3.0', gullySteps: 'NmcSetPieces 1.4.0',
  lifeRingPost: 'NmcSetPieces 1.3.0', picnicTable: 'NmcSetPieces 1.3.0',
  riverDeck: 'NmcSetPieces 1.2.0', riverWall: 'NmcSetPieces 1.2.0',
  routeBridge: 'NmcSetPieces 1.2.0', signBoard: 'NmcSetPieces 1.3.0',
  siteLights: 'NmcSetPieces 1.3.0', sitePost: 'NmcSetPieces 1.3.0',
  wallStairs: 'NmcSetPieces 1.2.0',
};
const LF_ONLY = ['creekDock', 'gullySteps'];
const POLYGON_PINS = {
  guardRail: { id: 'prop.nmc_guard_rail', line: 884, scene: 'keyscene.nmc_the_falls' },
  signBoard: { id: 'prop.nmc_sign_board', line: 896, scene: 'keyscene.nmc_the_falls' },
  barStake: { id: 'prop.nmc_bar_stake', line: 919, scene: 'keyscene.nmc_landing' },
  lifeRingPost: { id: 'prop.nmc_life_ring', line: 925, scene: 'keyscene.nmc_landing' },
  sitePost: { id: 'prop.nmc_site_post', line: 969, scene: 'keyscene.nmc_campground' },
};
const sourceLines = fs.readFileSync(path.join(KIT, 'nmcSetPieces.js'), 'utf8').split(/\r?\n/);
row(Object.keys(POLYGON_PINS).sort().join(',') === 'barStake,guardRail,lifeRingPost,signBoard,sitePost',
    'kit.polygon_exceptions', 'five named ground-mounted walk-solid declarations');
const finalLf = s => s.endsWith('\n') ? s.slice(0, -1) : s;
row(Object.values(SIDECAR_STAMPS).filter(s => s === 'NmcSetPieces 1.2.0').length === 7 &&
    Object.values(SIDECAR_STAMPS).filter(s => s === 'NmcSetPieces 1.3.0').length === 14 &&
    Object.keys(SIDECAR_STAMPS).filter(k => SIDECAR_STAMPS[k] === 'NmcSetPieces 1.4.0').sort().join(',') === LF_ONLY.slice().sort().join(','),
    'kit.sidecar_exceptions', '7 historical 1.2.0, 14 historical 1.3.0, two final-LF-only keys');
row(SP.VERSION === '1.4.0', 'kit.version', SP.VERSION);
row(today.bake.importSizeCap === IMPORT_SIZE_CAP, 'kit.sheet_cap', 'today and the writer declare the same sheet/import cap');
function sheetPlan(piece) {
  let cellW = 0, cellH = 0;
  const variants = [piece, ...piece.variants];
  for (const variant of variants) for (let frame = 0; frame < today.bake.facings; frame++) {
    const dir = (today.bake.facings - frame) % today.bake.facings;
    const r = SP.render(piece.key, dir, Object.assign({sky: SKY, lit: today.bake.lit, outline: today.bake.outline}, variant.options));
    let x0 = r.w, y0 = r.h, x1 = -1, y1 = -1;
    for (let y = 0; y < r.h; y++) for (let x = 0; x < r.w; x++) if (r.rgba[(y * r.w + x) * 4 + 3]) {
      x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y);
    }
    cellW = Math.max(cellW, Math.max(x1, r.px) - Math.min(x0, r.px) + 1);
    cellH = Math.max(cellH, Math.max(y1, r.py) - Math.min(y0, r.py) + 1);
  }
  const cells = today.bake.facings * variants.length;
  for (let cols = cells; cols >= 1; cols--) if (cells % cols === 0) {
    const rows = cells / cols;
    if (cols * cellW <= IMPORT_SIZE_CAP && rows * cellH <= IMPORT_SIZE_CAP)
      return {cells, cellW, cellH, cols, rows, w: cols * cellW, h: rows * cellH};
  }
  return null;
}
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
  // Plan every piece this intake actually bakes, including all state/colour variants. Later phases only check native cells here.
  const baked = today.pieces.find(p => p.key === key);
  if (baked) {
    const plan = sheetPlan(baked);
    row(plan !== null, key + '.sheet', plan ? plan.cells + ' cells of ' + plan.cellW + 'x' + plan.cellH + ' -> ' + plan.cols + 'x' + plan.rows + ' = ' + plan.w + 'x' + plan.h + ' under ' + IMPORT_SIZE_CAP : 'no divisor grid under the sheet/import cap');
  }
  row(!diff.length, key + '.repeats', diff.length ? 'differs at dir ' + diff.join(',') : 'byte-identical twice at all 8');
  row(!ring.length, key + '.ringless', ring.length ? 'keyline colour at dir ' + ring.join(',') : 'no #101d21 keyline by default (ADR 0031)');
  const ab = SP.render(key, 4, { sky: SKY, outline: true }); let kp = 0; for (let i = 0; i < ab.rgba.length; i += 4) if (ab.rgba[i + 3] && ab.rgba[i] === KEY[0] && ab.rgba[i + 1] === KEY[1] && ab.rgba[i + 2] === KEY[2]) kp++;
  row(kp > 0, key + '.keyline_ab', kp + ' keyline px with {outline:true} (the positive control)');
  // The delivered record may retain its pinned historical stamp. Compare every other byte exactly.
  const file = path.join(KIT, 'gameplay', key + '.gameplay.json'), want = JSON.stringify(SP.gameplay(key), null, 1);
  if (Object.prototype.hasOwnProperty.call(SIDECAR_STAMPS, key)) {
    let same = false;
    if (fs.existsSync(file)) {
      const delivered = finalLf(fs.readFileSync(file, 'utf8')), stamp = SIDECAR_STAMPS[key];
      const historical = stamp !== 'NmcSetPieces 1.4.0';
      const normalized = historical ? delivered.replace(' "generatedBy": "' + stamp + '",', ' "generatedBy": "NmcSetPieces 1.4.0",') : delivered;
      same = JSON.parse(delivered).generatedBy === stamp && normalized === want &&
        (historical ? delivered !== want : LF_ONLY.includes(key));
    }
    row(same, key + '.sidecar', path.relative(KIT, file) + ' · pinned stamp and final LF only');
  } else row(!fs.existsSync(file), key + '.inline_gameplay', 'wharf gameplay is checked at the today options');
  // A ground-mounted walk-solid piece blocks. Five delivered empty declarations are pinned by name.
  const gp = SP.gameplay(key), area = (pts) => Math.abs(pts.reduce((a, p, i) => { const q = pts[(i + 1) % pts.length]; return a + p[0] * q[1] - q[0] * p[1]; }, 0)) / 2;
  const pin = POLYGON_PINS[key], blocks = gp.mount === 'ground' && gp.walk === 'solid';
  let cok = (!blocks || gp.colliders.length > 0) && gp.colliders.every(c => area(c.pts) > 0.01);
  if (pin) {
    const delivered = JSON.parse(fs.readFileSync(file, 'utf8'));
    cok = P.id === pin.id && gp.id === pin.id && delivered.id === pin.id &&
      gp.scene === pin.scene && delivered.scene === pin.scene &&
      sourceLines[pin.line - 1].includes('PIECES.' + key + ' =') &&
      Array.isArray(P.footprints) && P.footprints.length === 0 && gp.colliders.length === 0 &&
      delivered.colliders.length === 0 && gp.mount === 'ground' && delivered.mount === 'ground' &&
      gp.walk === 'solid' && delivered.walk === 'solid' &&
      JSON.stringify(gp.colliders) === JSON.stringify(delivered.colliders);
  }
  row(cok, key + '.collider', 'mount ' + gp.mount + ' · walk ' + gp.walk + ' · ' + gp.colliders.length + ' polygon(s)' + (pin ? ' · pinned empty declaration at source line ' + pin.line : ''));
  // This kit also declares words by tag alone. Exported corners and points remain checked when present.
  const W = (gp.anchors.words || []); let wok = W.every(w => w.id && typeof w.text === 'string' &&
    (!w.corners || w.corners.length === 4) && (!w.at || w.at.length === 3));
  const B0 = SP.built(key, {}); for (const w of W) if (!B0.b.faces.some(f => f.tag === w.id)) wok = false;
  row(wok, key + '.words', W.length ? W.map(w => w.id).join(', ') : 'no words');
  // light at night: emitters glow only when lit
}
row(fs.readdirSync(path.join(KIT, 'gameplay')).filter(f => f.endsWith('.gameplay.json')).sort().join(',') ===
    Object.keys(SIDECAR_STAMPS).map(k => k + '.gameplay.json').sort().join(','), 'kit.sidecars', 'exactly the 23 named later-phase pieces');
for (const p of today.pieces) {
  row(JSON.stringify(SP.gameplay(p.key, p.options)) === JSON.stringify(p.gameplay), p.key + '.today', 'gameplay is the kit at scene options');
  for (const v of p.variants) row(JSON.stringify(SP.gameplay(p.key, v.options)) === JSON.stringify(v.gameplay), p.key + '.' + v.name, 'variant gameplay is the kit');
}
const crane = today.pieces.find(p => p.key === 'wharfCrane'), light = today.pieces.find(p => p.key === 'harbourLight');
row(crane.gameplay.anchors.state === 'working' && crane.variants[0].gameplay.anchors.state === 'stowed', 'wharfCrane.states', 'working and stowed');
row(light.gameplay.anchors.colour === 'green' && light.variants[0].gameplay.anchors.colour === 'red', 'harbourLight.colours', 'green west and red east');
for (const key of ['officeTrailer', 'fishMarket', 'restaurant', 'harbourLight']) {
  const p = today.pieces.find(p => p.key === key), a = SP.render(key, 4, Object.assign({sky:NIGHT,lit:0},p.options)), b = SP.render(key, 4, Object.assign({sky:NIGHT,lit:1},p.options));
  row(sha(a.rgba) !== sha(b.rgba), key + '.night', 'emitters respond to lit');
}
console.log(bad ? 'RESULT: FAIL - ' + bad + ' of ' + n + ' checks failed' : 'RESULT: PASS - all ' + n + ' checks pass');
process.exitCode = bad ? 1 : 0;
