'use strict';
/* check-kit.cjs: the fish plant kit's intake check, on the set pieces kit's model. Node 18+, built-ins only. No Unity.
     node docs/art/rigs/nmc-fish-plant-kit/checks/check-kit.cjs
   Loads the light and the rig into one vm context and checks every piece through the kit's own rasteriser, then checks
   the kit against the brief's numbers (docs/design/nmc-fish-plant-key-scene-brief.md, nmc-fish-plant-truck-court.md)
   and against the plant's Def on main. One PASS or FAIL line per check, then one RESULT line. */
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const W = require('./write-sidecars.cjs'), KIT = W.KIT, ROOT = path.resolve(KIT, '../../../..');
const ctx = W.load(['nmcSetPieces.js']), NF = ctx.NmcFishPlant, SP = ctx.NmcSetPieces, WS = ctx.WeatherSky;
let n = 0, bad = 0; const row = (ok, id, msg) => { n++; if (!ok) bad++; console.log((ok ? 'PASS' : 'FAIL') + '  ' + id + (msg ? '  ' + msg : '')); };
const sha = (a) => crypto.createHash('sha256').update(Buffer.from(a.buffer, a.byteOffset, a.byteLength)).digest('hex');
const SKY = WS.at({ time: 14, cloud: 0.1, wind: 0.3 }), NIGHT = WS.at({ time: 22.5, cloud: 0.05 });
const KEY = [0x10, 0x1d, 0x21], CELL_CAP = 2048, SHEET_CAP = 4096, near = (a, b) => Math.abs(a - b) < 1e-6;
const area = (pts) => Math.abs(pts.reduce((a, p, i) => { const q = pts[(i + 1) % pts.length]; return a + p[0] * q[1] - q[0] * p[1]; }, 0)) / 2;
const rigSha = W.shaLf(path.join(KIT, W.RIG));

row(NF.VERSION === '1.0.0', 'kit.version', NF.VERSION);
row(NF.KEYS.join(',') === 'fishPlant,plantDock,plantGate,courtBollard,courtKerb,plantCondenser,toteStack,yardLight', 'kit.pieces', NF.KEYS.length + ' pieces');
row(NF.PPU === SP.PPU && JSON.stringify(NF.facings()) === JSON.stringify(SP.facings()), 'kit.camera', 'the set pieces kit’s scale (' + NF.PPU + ' px = 1 m) and facings');
const want = W.sources(), have = fs.existsSync(path.join(KIT, 'sources.json')) ? JSON.parse(fs.readFileSync(path.join(KIT, 'sources.json'), 'utf8')) : null;
row(JSON.stringify(want) === JSON.stringify(have), 'kit.sources', 'sources.json pins the two light scripts and the rig as they stand');
// ramps: six steps, dark to light; a ramp this kit shares with the set pieces kit carries that kit's values
let rok = true, shared = 0, own = []; for (const [k, r] of Object.entries(NF.RAMPS)) { if (r.length !== 6) rok = false; const L = r.map(h => { const v = [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16)); return 0.2126 * v[0] + 0.7152 * v[1] + 0.0722 * v[2]; }); for (let i = 1; i < 6; i++) if (L[i] < L[i - 1]) rok = false;
  if (SP.RAMPS[k]) { shared++; if (SP.RAMPS[k].join() !== r.join()) rok = false; } else own.push(k); }
row(rok, 'kit.ramps', Object.keys(NF.RAMPS).length + ' ramps, six steps each, dark to light; ' + shared + ' shared with the set pieces kit unchanged; own: ' + own.join(', '));

for (const key of NF.KEYS) {
  const P = NF.PIECES[key], dirs = P.bake ? P.bake.facings : [0, 1, 2, 3, 4, 5, 6, 7], variants = [{}].concat((P.bake && P.bake.variants) || []);
  const B0 = NF.built(key, {}); let big = [], empty = [], diff = [], ring = [];
  for (let d = 0; d < 8; d++) { const c = NF.cellOf(B0, d); if (c.w > CELL_CAP || c.h > CELL_CAP) big.push(d + ':' + c.w + 'x' + c.h); }
  let cellW = 0, cellH = 0;
  for (const d of dirs) for (const v of variants) { const r1 = NF.render(key, d, Object.assign({ sky: SKY }, v)), r2 = NF.render(key, d, Object.assign({ sky: SKY }, v));
    let px = 0; for (let i = 3; i < r1.rgba.length; i += 4) if (r1.rgba[i]) px++; if (!px) empty.push(d); if (sha(r1.rgba) !== sha(r2.rgba)) diff.push(d);
    for (let i = 0; i < r1.rgba.length; i += 4) if (r1.rgba[i + 3] && r1.rgba[i] === KEY[0] && r1.rgba[i + 1] === KEY[1] && r1.rgba[i + 2] === KEY[2]) { ring.push(d); break; }
    cellW = Math.max(cellW, r1.w); cellH = Math.max(cellH, r1.h); }
  row(!empty.length, key + '.draws', empty.length ? 'empty at dir ' + empty.join(',') : 'bake facings ' + dirs.join(',') + (variants.length > 1 ? ' x ' + variants.length + ' variants' : ''));
  row(!big.length, key + '.cell', big.length ? big.join(' ') : 'every facing’s cell within ' + CELL_CAP);
  const cells = dirs.length * variants.length; let plan = null; for (let cols = cells; cols >= 1 && !plan; cols--) if (cells % cols === 0 && cols * cellW <= SHEET_CAP && (cells / cols) * cellH <= SHEET_CAP) plan = cols + 'x' + cells / cols + ' = ' + cols * cellW + 'x' + (cells / cols) * cellH;
  row(!!plan, key + '.sheet', plan ? cells + ' cells of ' + cellW + 'x' + cellH + ' -> ' + plan + ' under ' + SHEET_CAP : 'no grid under the sheet cap');
  row(!diff.length, key + '.repeats', diff.length ? 'differs at dir ' + diff.join(',') : 'byte-identical twice');
  row(!ring.length, key + '.ringless', ring.length ? 'keyline colour at dir ' + ring.join(',') : 'no #101d21 keyline by default (ADR 0031)');
  const ab = NF.render(key, dirs[0], { sky: SKY, outline: true }); let kp = 0; for (let i = 0; i < ab.rgba.length; i += 4) if (ab.rgba[i + 3] && ab.rgba[i] === KEY[0] && ab.rgba[i + 1] === KEY[1] && ab.rgba[i + 2] === KEY[2]) kp++;
  row(kp > 0, key + '.keyline_ab', kp + ' keyline px with {outline:true} (the positive control)');
  const file = path.join(KIT, 'gameplay', key + '.gameplay.json'), gp = NF.gameplay(key);
  row(fs.existsSync(file) && W.lf(file) === JSON.stringify(W.sidecar(NF, key, rigSha), null, 1) + '\n', key + '.sidecar', 'gameplay/' + key + '.gameplay.json is the kit’s output and carries the rig’s hash');
  const blocks = gp.mount === 'ground' && gp.walk === 'solid';
  row((!blocks || gp.colliders.length > 0) && gp.colliders.every(c => area(c.pts) > 0.01), key + '.collider', 'layer ' + gp.layer + ' · walk ' + gp.walk + ' · ' + gp.colliders.length + ' polygon(s)');
  const Wd = gp.anchors.words || []; row(Wd.every(w => w.id && typeof w.text === 'string' && w.text === '' && w.corners && w.corners.length === 4 && B0.b.faces.some(f => f.tag === w.id)) && B0.b.faces.filter(f => /^words\./.test(f.tag || '')).every(f => Wd.some(w => w.id === f.tag)), key + '.words', Wd.length ? Wd.map(w => w.id).join(', ') + ' (all blank)' : 'no words');
  const L = gp.anchors.lights || []; if (gp.light) { const a = NF.render(key, dirs[0], { sky: NIGHT, lit: 0 }), b = NF.render(key, dirs[0], { sky: NIGHT, lit: 1 });
    row(sha(a.rgba) !== sha(b.rgba) && L.length > 0 && L.every(l => l.id && l.at.length === 3 && l.atPx.length === 2), key + '.night', 'emitters ' + gp.light.emitters.join(', ') + ' respond to lit; ' + L.length + ' light anchor(s)'); }
  else row(L.length === 0, key + '.night', 'no emitters, no light anchors');
}
row(fs.readdirSync(path.join(KIT, 'gameplay')).sort().join(',') === NF.KEYS.map(k => k + '.gameplay.json').sort().join(','), 'kit.sidecars', 'exactly one sidecar a piece');

// ---- the brief's numbers
const S = NF.SITE, plant = NF.gameplay('fishPlant'), dock = NF.gameplay('plantDock'), gate = NF.gameplay('plantGate'), wxy = (g, k) => g.site.anchors[k].world, eq2 = (p, q) => near(p[0], q[0]) && near(p[1], q[1]);
const fp = plant.site.colliders[0], xs = fp.map(p => p[0]), ys = fp.map(p => p[1]);
row(near(area(plant.colliders[0].pts), 1344) && Math.min(...xs) === -289 && Math.max(...xs) === -241 && Math.min(...ys) === -54 && Math.max(...ys) === -26, 'brief.footprint', '48 x 28 m = 1,344 m2 at world x [-289, -241], y [-54, -26]');
let zsum = 0, zok = NF.ZONES.length === 20; for (const z of NF.ZONES) { zsum += (z[3] - z[2]) * (z[5] - z[4]); if (z[2] < -48 || z[3] > 0 || z[4] < -14 || z[5] > 14) zok = false; }
for (let i = 0; i < NF.ZONES.length; i++) for (let j = i + 1; j < NF.ZONES.length; j++) { const a = NF.ZONES[i], b = NF.ZONES[j]; if (Math.min(a[3], b[3]) - Math.max(a[2], b[2]) > 1e-9 && Math.min(a[5], b[5]) - Math.max(a[4], b[4]) > 1e-9) zok = false; }
row(zok && zsum === 1344, 'brief.programme', '20 rooms, no overlap, ' + zsum + ' m2 (the whole footprint)');
row(eq2(plant.site.pivot.world, [-265, -40]) && eq2(plant.site.anchors.door.brief, [-4, -15.2]) && eq2(plant.site.anchors.staffDoor.brief, [-33, -15.2]), 'brief.doors', 'public door at brief (-4, -14), staff at (-33, -14); the anchors stand 1.2 m outside');
const D = (id) => NF.DOORS.find(d => d.id === id);
row(D('intake1').c === 0 && D('intake1').w === 4 && D('intake2').c === 8 && D('intake2').w === 4 && D('outbound').c === 12 && eq2(plant.site.anchors.intake1.brief, [0, 0]) && eq2(plant.site.anchors.intake2.brief, [0, 8]) && eq2(plant.site.anchors.outbound.brief, [0, 12]), 'brief.openings', 'I1 4 m at v 0, I2 4 m at v 8, outbound at v 12, all on the east face');
row(eq2(wxy(plant, 'truckStop1'), [-240.5, -40]) && eq2(wxy(plant, 'truckStop2'), [-240.5, -32]) && eq2(plant.site.anchors.truckAxle1.brief, [2.5, 0]) && eq2(plant.site.anchors.truckAxle2.brief, [2.5, 8]), 'brief.truck_stops', 'rear bumpers at world (-240.5, -40) and (-240.5, -32), rear axles at brief u 2.5');
row(['truckStop1', 'truckStop2', 'truckAxle1', 'truckAxle2'].every(k => eq2(wxy(plant, k), wxy(dock, k))) && eq2(dock.site.pivot.world, [-241, -40]) && eq2(dock.site.anchors.truckNose1.brief, [8.5, 0]), 'brief.dock_agrees', 'the dock apron and the plant give the same truck points; the bays hold the 8 m planning truck');
const gc = gate.site.colliders, inOpening = gc.filter(c => { const u = c.map(p => p[0] + 241), v = c.map(p => p[1] + 40); return Math.max(...u) > 25.9 && Math.min(...u) < 26.1 && Math.max(...v) > -5.99 && Math.min(...v) < 5.99 && Math.min(...u) > 25.5; });
row(eq2(gate.site.pivot.world, [-215, -40]) && gate.anchors.clear[0] === 12 && inOpening.length === 0 && NF.gameplay('plantGate', { open: false }).anchors.state === 'shut', 'brief.gate', '12 m clear at world (-215, -40); open, nothing stands in brief v [-6, 6] at u 26');
const cut = NF.built('fishPlant', { cutaway: true }); let zc = 0; for (const f of cut.b.faces) for (const p of f.v) zc = Math.max(zc, p[2]);
row(zc <= 1.21 && NF.OPENINGS.length === 6 && cut.b.faces.filter(f => /^floor\./.test(f.tag || '')).length === 20, 'brief.cutaway', 'cut at ' + zc.toFixed(2) + ' m, 20 floors, 6 proposed openings on the named route');
// ---- the logic on main: the plant's Def carries no geometry; the kit names the same plant and region
const def = fs.readFileSync(path.join(ROOT, 'Assets/_Project/Data/FishSupplyChain/NineMileCreekFishPlant.asset'), 'utf8'), defCs = fs.readFileSync(path.join(ROOT, 'Assets/_Project/Code/Economy/SupplyChain/FishPlantDef.cs'), 'utf8');
row(new RegExp('^\\s*Id: ' + S.plantDef.replace('.', '\\.') + '\\s*$', 'm').test(def) && new RegExp('^\\s*RegionId: ' + S.region.replace('.', '\\.') + '\\s*$', 'm').test(def), 'logic.plant_def', S.plantDef + ' in ' + S.region);
row(!/Vector[23]|Bounds|Rect\b|Transform|Door|Dock/.test(defCs), 'logic.no_geometry', 'FishPlantDef holds no position, door or dock field: the brief is the only geometry to agree with');
console.log(bad ? 'RESULT: FAIL - ' + bad + ' of ' + n + ' checks failed' : 'RESULT: PASS - all ' + n + ' checks pass');
process.exitCode = bad ? 1 : 0;
