// NMC walls (H3): Nine Mile Creek's cliff walls by real id against G3, and their Defs. Re-runnable (rule 6): every line
// a Def carries is the scene's own text or read off G3 here, never typed in.
//
//   node tools/nmc-walls/derive-walls.cjs --ground-plan <ground/plan.json> [--plan B|A] [--write] [--out <dir>] [--h2 <walls-disposition.csv>]
//
// Each station of each wall, read along its own normal (brow to toe):
//   ON         brow and toe both within 10 cm of G3: kept, the scene's text to the bit;
//   toe lifted the brow ON, and a face the default bake draws (55 deg or steeper, 1 m or more) under it: the toe moves to
//              the face's foot and is read off G3, so long as the builder's law still holds there (1 m, 55 deg);
//   re-lined   else that face's brow and toe both, read off G3, under the same law;
//   banked     (plan B) else the brow ON with the toe off a face the bake does not draw: kept as built;
//   out        else: no wall stands there.
// A wall's standing stations make its pieces, cut where the chunk math would cut them (a piece is one chunk), the first
// keeping the wall's id and the rest taking the next free ones. A wall with none retires: its Def keeps the id and the
// scene's lines, and spawns nothing. A piece follows the live piece whose last station is its first, to the bit.
'use strict';
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const G = require('./g3.cjs'), S = require('./scene.cjs'), FM = require('./facemap.cjs'), F = require('./faces.cjs'), C = require('./chunks.cjs'), D = require('./defs.cjs');

const ROOT = path.resolve(__dirname, '../..');
const SCENE = 'Assets/_Project/Scenes/NineMileCreek.unity';
const GROUND = 'Assets/_Project/Data/Terrain/NineMileCreekGround/NineMileCreekGround_Height.png';
const FOLDER = 'Assets/_Project/Data/Terrain/NineMileCreekWalls';
const G3_PIXELS = '5924a6428d593bc80bc3eabe6c3887b39b05abe6aa5142b7ef9a0d6f9e8b02b0';
const KEEP = 0.10;            // the kept rule: a station within 10 cm of the ground at both ends stands as it is
const WINDOW = 16;            // how far either side of a station's footprint its normal is read for a face (m)
const FIRST_NEW = 86;         // the scene's walls are 000 to 085

const arg = (k, d) => { const i = process.argv.indexOf(k); return i < 0 ? d : process.argv[i + 1]; };
const PLAN = arg('--plan', 'B'), WRITE = process.argv.includes('--write'), OUT = path.resolve(ROOT, arg('--out', 'artifacts/nmc-h3'));
const H2 = arg('--h2', null), GROUND_PLAN = arg('--ground-plan', null);
if (!['A', 'B'].includes(PLAN)) throw new Error('--plan is A or B');
const f = Math.fround, sha = (b) => crypto.createHash('sha256').update(b).digest('hex');
const fx = (x, n = 3) => (x == null ? '' : (+x).toFixed(n));

// ---- the sources ------------------------------------------------------------------------------------------------
const g3 = G.decode(path.join(ROOT, GROUND));
if (g3.pixelsSha256 !== G3_PIXELS) throw new Error(`G3's pixels are ${g3.pixelsSha256}, not the live ground's ${G3_PIXELS}`);
const g = G.field(g3.codes), at = (p) => f(g.at(p[0], p[1]));
const sceneText = fs.readFileSync(path.join(ROOT, SCENE), 'utf8');
const { root, walls } = S.cliffWalls(sceneText);
// The wharf's faces: every steep piece of G3 that QuayWalls' runs touch (the quay's own faces, and what joins them)
const map = FM.steepPieces(g), quayRuns = S.boundaryRuns(sceneText, 'QuayWalls');
const quayPieces = new Set(quayRuns.flatMap(r => [...map.piecesAlong(r)]));
const MEASURED_ON = `G3: NineMileCreekGround_Height.png (pixels sha256 ${G3_PIXELS})`;
const SOURCE_LIST = 'inventory.csv (tools/nmc-walls/derive-walls.cjs, plan ' + PLAN + ')';

// ---- each station against G3 ------------------------------------------------------------------------------------
const P = (b, d, s) => [f(b[0] + d[0] * s), f(b[1] + d[1] * s)];
const lawHolds = (s) => s.drop >= F.MIN_DROP && C.batterDegrees(s) >= F.MIN_BATTER - 1e-3;
const snapBaked = (deg) => { let best = 0; for (const k of F.BAKED) if (Math.abs(deg - F.ANGLES[k]) < Math.abs(deg - F.ANGLES[best])) best = k; return F.BATTERS[best]; };
let formatterChecked = 0;

function stationsOf(w) {
  return w.brow.map((b, i) => {
    const t = w.toe[i], raw = { brow: w.raw.brow[i], toe: w.raw.toe[i], drop: w.raw.drop[i], toeZ: w.raw.toeZ[i] };
    const built = { brow: raw.brow.map(Number).map(f), toe: raw.toe.map(Number).map(f), drop: f(+raw.drop), toeZ: f(+raw.toeZ) };
    for (const v of [...raw.brow, ...raw.toe, raw.drop, raw.toeZ]) { if (D.f32s(+v) !== v) throw new Error(`the float form ${D.f32s(+v)} is not the scene's ${v}`); formatterChecked++; }
    const browZ = f(built.toeZ + built.drop);
    const rb = Math.abs(at(b) - browZ), rt = Math.abs(at(t) - built.toeZ);
    const L = Math.hypot(t[0] - b[0], t[1] - b[1]), d = [(t[0] - b[0]) / L, (t[1] - b[1]) / L];
    let face = null, gap = Infinity;
    for (const fc of F.faces(F.profile(g, b, d, -WINDOW, L + WINDOW))) {
      const gp = Math.max(0, fc.sBrow - L, -fc.sToe);
      if (gp < gap - 1e-9 || (Math.abs(gp - gap) <= 1e-9 && fc.drop > face.drop)) { gap = gp; face = fc; }
    }
    if (face) face.piece = map.pieceAt(b[0] + d[0] * (face.sBrow + face.sToe) / 2, b[1] + d[1] * (face.sBrow + face.sToe) / 2);
    const overlaps = face != null && gap <= L, quay = overlaps && quayPieces.has(face.piece);
    const kit = overlaps && !quay && face.batter >= F.MIN_BATTER;
    const s = { k: i, rb, rt, L, face, gap, overlaps, quay, cls: rb <= KEEP && rt <= KEEP ? 'ON' : quay ? 'QUAY' : kit ? 'KIT' : overlaps ? 'BANK' : 'NONE',
      built: { ...built, browFrom: 'Kept', toeFrom: 'Kept', text: raw, rule: 'kept' } };
    if (s.cls === 'ON') s.stand = s.built;
    else if (kit) {
      if (rb <= KEEP) {                                   // toe lifted: the brow stays, the toe goes to the face's foot
        const toe = P(b, d, face.sToe), toeZ = at(toe), m1 = { brow: built.brow, toe, toeZ, drop: f(browZ - toeZ), browFrom: 'Kept', toeFrom: 'Measured', text: { brow: raw.brow }, rule: 'toe lifted' };
        if (lawHolds(m1)) s.stand = m1;
      }
      if (!s.stand) {                                     // re-lined: the face's brow and toe, both read off G3
        const brow = P(b, d, face.sBrow), toe = P(b, d, face.sToe), bz = at(brow), tz = at(toe);
        const m2 = { brow, toe, toeZ: tz, drop: f(bz - tz), browFrom: 'Measured', toeFrom: 'Measured', text: {}, rule: 're-lined' };
        if (lawHolds(m2)) s.stand = m2;
      }
    }
    s.bankable = PLAN === 'B' && !s.stand && rb <= KEEP;
    s.kind = s.stand ? 'stand' : s.bankable ? 'bank' : 'out';
    return s;
  });
}

// (a face the wharf's runs touch is the quay's: no cliff wall is moved onto it, and none banks against it)
// ---- each wall's pieces -----------------------------------------------------------------------------------------
function piecesOf(w, sts) {
  const segs = [];
  for (let i = 0; i < sts.length; i++) {
    const kind = sts[i].kind;
    if (kind === 'out') continue;
    if (segs.length && segs.at(-1).kind === kind && segs.at(-1).to === i - 1) segs.at(-1).to = i;
    else segs.push({ kind, from: i, to: i });
  }
  // a banked stretch after a standing one starts on that one's last station as built, so the two overlap by a station
  for (let n = 1; n < segs.length; n++)
    if (segs[n].kind === 'bank' && segs[n - 1].kind === 'stand' && segs[n - 1].to === segs[n].from - 1 && sts[segs[n].from - 1].rb <= KEEP) segs[n].from--;
  const pieces = [];
  for (const sg of segs) {
    const list = [];
    for (let i = sg.from; i <= sg.to; i++) list.push({ ...(sg.kind === 'stand' && sts[i].kind === 'stand' ? sts[i].stand : sts[i].built), k: i, kind: sg.kind });
    if (list.length < 2) continue;
    for (const [a, z] of C.cutPieces(list)) if (z - a + 1 >= 2) pieces.push({ kind: sg.kind, stations: list.slice(a, z + 1) });
  }
  return pieces;
}

function statusOf(p) {
  if (p.kind === 'bank') return 'Banked';
  if (p.stations.some(s => s.rule === 're-lined')) return 'Relined';
  if (p.stations.some(s => s.rule === 'toe lifted')) return 'ToeLifted';
  return 'Kept';
}
function batterOf(w, p) {
  if (p.stations.every(s => s.rule === 'kept')) return { batter: w.batter, mixed: '' };
  const votes = {};
  for (const s of p.stations) { const b = snapBaked(C.batterDegrees(s)); votes[b] = (votes[b] || 0) + 1; }
  const order = Object.entries(votes).sort((a, b) => b[1] - a[1] || (a[0] === w.batter ? -1 : b[0] === w.batter ? 1 : 0));
  return { batter: order[0][0], mixed: order.length > 1 ? order.map(([b, n]) => `${b} ${n}`).join(', ') : '' };
}

// ---- the walls ----------------------------------------------------------------------------------------------------
const rows = [], defs = [];
let nextId = FIRST_NEW;
const later = [];       // pieces past a wall's first, which take new ids once every wall has its own
for (const w of walls) {
  const sts = stationsOf(w), pieces = piecesOf(w, sts), id = 'wall.nmc_' + w.realId;
  const counts = {}; for (const s of sts) counts[s.cls] = (counts[s.cls] || 0) + 1;
  const rules = sts.map(s => (s.kind === 'stand' ? s.stand.rule : s.kind));
  let disposition;
  if (!pieces.length) disposition = 'RETIRE';
  else if (pieces.length > 1) disposition = 'SPLIT';
  else {
    const p = pieces[0], whole = p.stations.length === sts.length && p.stations.every(s => s.rule === 'kept');
    disposition = whole ? 'KEEP' : 'RE-LINE';
  }
  const row = { w, sts, pieces, counts, rules, disposition, ids: [] };
  rows.push(row);
  if (!pieces.length) {
    defs.push({ id, name: 'Wall_' + w.realId, status: 'Retired', splitFrom: '', follows: '', rock: 'sandstone', cls: w.cls, aspect: w.aspect,
      batter: w.batter, stations: sts.map(s => ({ ...s.built, k: s.k })), pass9: false, sourceRow: w.order, measuredOn: '', faceNeeded: '',
      wall: w, row, live: false });
    continue;
  }
  pieces.forEach((p, n) => {
    const d = { id: n === 0 ? id : null, name: null, status: statusOf(p), splitFrom: n === 0 ? '' : id, follows: '', rock: 'sandstone',
      cls: w.cls, aspect: w.aspect, ...batterOf(w, p), stations: p.stations, pass9: true, sourceRow: w.order, measuredOn: MEASURED_ON,
      faceNeeded: '', wall: w, row, live: true, piece: n };
    if (n === 0) row.ids.push(id); else later.push(d);
    defs.push(d);
  });
}
for (const d of later) { d.id = 'wall.nmc_' + String(nextId++).padStart(3, '0'); d.row.ids.push(d.id); }
for (const d of defs) {
  d.name = 'Wall_' + d.id.slice(-3);
  for (const s of d.stations) { s.browPass9 = at(s.brow); s.toePass9 = at(s.toe); }
}

// ---- runs: a piece follows the live piece whose last station is its first, to the bit ----------------------------
const live = defs.filter(d => d.live).map(d => ({ d, first: C.samples(toLines(d))[0], last: C.samples(toLines(d)).at(-1) }));
function toLines(d) { return { brow: d.stations.map(s => s.brow), toe: d.stations.map(s => s.toe), drop: d.stations.map(s => s.drop), toeZ: d.stations.map(s => s.toeZ) }; }
for (const a of live) {
  const before = live.filter(b => b !== a && C.sameStation(b.last, a.first));
  if (before.length > 1) throw new Error(`'${a.d.id}' starts on the last station of ${before.map(b => b.d.id).join(' and ')}`);
  if (before.length) a.d.follows = before[0].d.id;
}
const chunkDefs = defs.filter(d => d.live).map(d => ({ id: d.id, live: true, follows: d.follows, ...toLines(d) }));
const chunks = C.chunksOfDefs(chunkDefs);

// ---- the new faces G3 asks for, held (newfaces.cjs) ----------------------------------------------------------------
const NF = require('./newfaces.cjs').heldFaces({ g, at, defs, map, quayPieces, firstId: nextId, groundPlan: GROUND_PLAN });
const held = NF.held;
for (const h of held) { h.live = false; h.pass9 = false; h.measuredOn = MEASURED_ON; h.sourceRow = -1; h.rock = 'sandstone'; defs.push(h); }

// ---- after the plan: each live Def against G3 -------------------------------------------------------------------
function residual(d) {
  let rb = 0, rt = 0, keptRb = 0, keptRt = 0, poly = 0;
  for (const s of d.stations) {
    const ob = Math.abs(at(s.brow) - f(s.toeZ + s.drop)), ot = Math.abs(at(s.toe) - s.toeZ);
    rb = Math.max(rb, ob); rt = Math.max(rt, ot);
  }
  // H2's residual: the brow and toe polylines every 0.25 m against the line's own height
  const line = (pts, zs) => { for (let i = 0; i + 1 < pts.length; i++) { const [ax, ay] = pts[i], [bx, by] = pts[i + 1], n = Math.max(1, Math.ceil(Math.hypot(bx - ax, by - ay) / 0.25));
    for (let k = 0; k <= n; k++) { const t = k / n; poly = Math.max(poly, Math.abs(g.at(ax + (bx - ax) * t, ay + (by - ay) * t) - (zs[i] + (zs[i + 1] - zs[i]) * t))); } } };
  line(d.stations.map(s => s.brow), d.stations.map(s => s.toeZ + s.drop)); line(d.stations.map(s => s.toe), d.stations.map(s => s.toeZ));
  return { rb, rt, poly };
}
const asBuilt = (w) => residual({ stations: w.brow.map((b, i) => ({ brow: b, toe: w.toe[i], drop: w.drop[i], toeZ: w.toeZ[i] })) });

// ---- Why, in words ----------------------------------------------------------------------------------------------
const kRange = (st) => (st.length ? `k${st[0].k}-k${st.at(-1).k}` : '');
const range = (xs, n = 2) => (xs.length ? (Math.min(...xs) === Math.max(...xs) ? fx(xs[0], n) : `${fx(Math.min(...xs), n)}-${fx(Math.max(...xs), n)}`) : '-');
const STEP07 = 'Step 07 of the ground swap (#959) set it inactive; step 11 removes its objects.';
for (const d of defs) {
  if (d.why) continue;
  const w = d.wall, sts = d.row.sts;
  if (d.status === 'Retired') {
    const off = sts.map(s => s.rb), faces = sts.filter(s => s.overlaps).map(s => s.face.batter);
    d.why = `Retired: G3 holds no face here a wall can stand on (its brow ${range(off)} m off the ground` +
      (faces.length ? `, the only face under it ${range(faces, 0)} deg` : ', no face under it') + `). ` + (w.active ? '' : STEP07 + ' ') +
      `Its lines are the scene's at 286e231f, kept as a record; the id is never used again.`;
    continue;
  }
  const st = d.stations, kept = st.filter(s => s.rule === 'kept'), lifted = st.filter(s => s.rule === 'toe lifted'), relined = st.filter(s => s.rule === 're-lined');
  const parts = [];
  if (d.status === 'Banked') {
    const own = st.map(s => sts[s.k]);
    parts.push(`Banked on G3 (plan B): stations ${kRange(st)} as built, the brow on the ground (worst ${fx(Math.max(...own.map(s => s.rb)))} m), the toe ` +
      `${range(own.map(s => s.rt), 1)} m off it, where G3's face is ${own.some(s => s.overlaps) ? range(own.filter(s => s.overlaps).map(s => s.face.batter), 0) + ' deg' : 'out of reach'}, ` +
      `which the default bake does not draw. It stands as built until that face has its own wall`);
  } else {
    if (kept.length) parts.push(`${kept.length === st.length ? 'Kept on G3: every station' : `kept: ${kept.length} stations`} within 10 cm of the ground at both ends`);
    if (lifted.length) parts.push(`toe lifted: ${lifted.map(s => 'k' + s.k).join(' ')} to the foot of G3's face (${range(lifted.map(s => sts[s.k].face.batter), 0)} deg), read off G3`);
    if (relined.length) parts.push(`re-lined: ${relined.map(s => 'k' + s.k).join(' ')} onto G3's face, brow and toe read off G3`);
  }
  if (d.splitFrom) parts.push(`cut from ${d.splitFrom} (its ${kRange(st)})`);
  else if (d.row.pieces.length > 1) parts.push(`its ${kRange(st)}; the rest of the wall is ${d.row.ids.slice(1).join(', ')}`);
  else if (st.length < sts.length) parts.push(`shortened to ${kRange(st)}: ${sts.filter(s => s.kind === 'out').map(s => 'k' + s.k).join(' ')} stand off the ground`);
  if (d.mixed) parts.push(`its stations snap to ${d.mixed}: it wears ${d.batter}`);
  const s = parts.join('; ') + '.';
  d.why = s[0].toUpperCase() + s.slice(1);
}

// ---- the table ----------------------------------------------------------------------------------------------------
const rl = (xs) => { const o = []; for (const x of xs) { if (o.length && o.at(-1)[0] === x) o.at(-1)[1]++; else o.push([x, 1]); } return o.map(([k, n]) => `${k}x${n}`).join(' '); };
let h2 = null;
if (H2) { h2 = new Map(); const L = fs.readFileSync(H2, 'utf8').trim().split(/\r?\n/); const hd = L[0].split(','); for (const l of L.slice(1)) { const c = l.split(','); h2.set(c[hd.indexOf('fileID')], +c[hd.indexOf('maxNewResidual')]); } }
const header = ['realId', 'name', 'active', 'goFileID', 'stations', 'h2Residual', 'h2Csv', 'browOffMax', 'toeOffMax', 'stationClasses', 'faceBatter', 'faceGap',
  'stationRules', 'disposition', 'pieces', 'statuses', 'afterBrowMax', 'afterToeMax', 'afterResidual', 'why'];
const csv = [header.join(',')];
const q = (s) => (/[",]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s);
for (const r of rows) {
  const w = r.w, ab = asBuilt(w), mine = defs.filter(d => d.wall === w && d.live), after = mine.map(residual);
  const ov = r.sts.filter(s => s.face);
  r.after = after; r.asBuilt = ab;
  csv.push([w.realId, w.name, w.active ? 1 : 0, w.ids.go, w.brow.length, fx(ab.poly, 4), h2 ? fx(h2.get(w.ids.surface), 4) : '', fx(Math.max(...r.sts.map(s => s.rb))), fx(Math.max(...r.sts.map(s => s.rt))),
    rl(r.sts.map(s => s.cls)), range(ov.map(s => s.face.batter), 1), range(ov.map(s => s.gap)), rl(r.rules), r.disposition,
    mine.map(d => `${d.id.slice(-3)}[${kRange(d.stations)}]`).join(' '), mine.map(d => d.status).join(' '),
    after.length ? fx(Math.max(...after.map(a => a.rb))) : '', after.length ? fx(Math.max(...after.map(a => a.rt))) : '', after.length ? fx(Math.max(...after.map(a => a.poly)), 4) : '',
    q(defs.find(d => d.id === 'wall.nmc_' + w.realId).why)].join(','));
}
fs.mkdirSync(OUT, { recursive: true });
const csvText = csv.join('\n') + '\n';
fs.writeFileSync(path.join(OUT, 'inventory.csv'), csvText);
const csvSha = sha(csvText);
for (const d of defs) { d.sourceList = SOURCE_LIST; d.sourceListSha256 = csvSha; }

// ---- the Defs ---------------------------------------------------------------------------------------------------
const texts = defs.map(d => ({ d, file: `${FOLDER}/${d.name}.asset`, text: D.assetText(d), meta: D.metaText(D.guidOf(d.id)) }));
if (WRITE) {
  fs.mkdirSync(path.join(ROOT, FOLDER), { recursive: true });
  fs.writeFileSync(path.join(ROOT, FOLDER + '.meta'), D.folderMetaText(D.guidOf('folder:' + FOLDER)));
  for (const t of texts) { fs.writeFileSync(path.join(ROOT, t.file), t.text); fs.writeFileSync(path.join(ROOT, t.file + '.meta'), t.meta); }
}
fs.writeFileSync(path.join(OUT, 'walls.json'), JSON.stringify({ plan: PLAN, groundPixels: G3_PIXELS, scene: sha(sceneText), root,
  defs: defs.map(d => ({ id: d.id, status: d.status, follows: d.follows, splitFrom: d.splitFrom, cls: d.cls, aspect: d.aspect, batter: d.batter, live: d.live,
    file: `${FOLDER}/${d.name}.asset`, assetSha256: sha(D.assetText(d)), stations: d.stations.length })),
  chunks: chunks.map(c => ({ id: c.def.id, along: c.along, rows: c.rows, azimuth: c.azimuth, runIndex: c.runIndex })) }, null, 1));

// ---- the faces G3 holds, piece by piece (newfaces.cjs) -------------------------------------------------------------
const nfHeader = ['piece', 'texels', 'x', 'y', 'z', 'drop', 'slopeMean', 'aspect', 'feature', 'guides', 'stations', 'classes', 'owners', 'held', 'heldIds'];
const nfCsv = [nfHeader.join(',')];
for (const r of NF.pieces) {
  const p = r.piece;
  nfCsv.push([p.id, p.n, `${fx(p.x0, 1)}..${fx(p.x1, 1)}`, `${fx(p.y0, 1)}..${fx(p.y1, 1)}`, `${fx(p.zMin, 2)}..${fx(p.zMax, 2)}`, fx(p.drop, 2), fx(p.slopeMean, 1),
    p.aspect + (p.allowed ? '' : '!'), q([...r.features].join('; ')), r.guides, r.stations, Object.entries(r.classes).map(([k, n]) => `${k} ${n}`).join(' '),
    q([...r.owners].map(o => o.replace('wall.nmc_', '')).join(' ')), r.held.length, r.held.map(id => id.slice(-3)).join(' ')].join(','));
}
fs.writeFileSync(path.join(OUT, 'newfaces.csv'), nfCsv.join('\n') + '\n');

// ---- the summary --------------------------------------------------------------------------------------------------
const by = (k) => rows.filter(r => r.disposition === k).map(r => r.w.realId);
const st = {}; for (const d of defs) st[d.status] = (st[d.status] || 0) + 1;
const out = [];
out.push(`# NMC walls on G3: plan ${PLAN}`, '', `Scene ${SCENE} sha256 ${sha(sceneText)}; ${walls.length} walls under CliffWalls (GameObject ${root.go}); ${walls.filter(w => w.active).length} active.`,
  `G3 pixels sha256 ${g3.pixelsSha256}, file sha256 ${g3.fileSha256}, ${g3.fileBytes} B. The float form matched the scene's text on ${formatterChecked} numbers.`, '');
out.push('## Dispositions', '');
for (const k of ['KEEP', 'RE-LINE', 'SPLIT', 'RETIRE']) out.push(`- ${k} ${by(k).length}: ${by(k).join(' ')}`);
out.push(`- Defs ${defs.length}: ` + Object.entries(st).map(([k, n]) => `${k} ${n}`).join(', ') + `; live ${defs.filter(d => d.live).length}; new ids ${later.map(d => d.id.slice(-3)).join(' ') || 'none'}` +
  (held.length ? `; held new faces ${held.map(d => d.id.slice(-3)).join(' ')}` : ''), '');
out.push('## The faces G3 holds that no wall covers', '', `Neap high ${fx(NF.tide.neapHigh, 2)} m, spring high ${fx(NF.tide.springHigh, 2)} m (the region's tide, the config's neap fraction); the ground plan sha256 ${NF.planSha}.`, '');
for (const r of NF.pieces) if (r.held.length) out.push(`- piece ${r.piece.id} (${[...r.features].join('; ') || 'unnamed'}): ${r.held.length} held, ${r.held[0].slice(-3)} to ${r.held.at(-1).slice(-3)}; ${Object.entries(r.classes).map(([k, n]) => `${k} ${n}`).join(', ')}`);
out.push('');
out.push('## After the plan, still more than 10 cm off', '');
for (const r of rows) for (const d of defs.filter(x => x.wall === r.w && x.live)) { const a = residual(d); if (a.rb > KEEP || a.rt > KEEP) out.push(`- ${d.id.slice(-3)} (${d.status}): brow ${fx(a.rb)} m, toe ${fx(a.rt)} m. ${d.why}`); }
out.push('', '## Runs', '', `${chunks.length} chunks in ${new Set(chunks.map(c => c.runIndex)).size} runs: ` +
  [...new Set(chunks.map(c => c.runIndex))].map(ri => chunks.filter(c => c.runIndex === ri).map(c => c.id.slice(-3)).join('-')).map(s => `[${s}]`).join(' '), '');
const notes = [];
for (const c of chunks) {
  const w = walls.find(x => 'wall.nmc_' + x.realId === c.id); if (!w) continue;
  const ch = []; if (c.along !== f(w.along)) ch.push(`along ${w.along} -> ${D.f32s(c.along)}`); if (c.rows !== f(w.rows)) ch.push(`rows ${w.rows} -> ${D.f32s(c.rows)}`);
  if (c.azimuth !== f(w.azimuth)) ch.push(`azimuth ${w.azimuth} -> ${D.f32s(c.azimuth)}`);
  if (ch.length) notes.push(`- ${c.id.slice(-3)}: ${ch.join(', ')}`);
}
out.push(`Forecast of the patch's along, rows and azimuth changes on the standing walls (${notes.length}):`, '', ...notes, '');
fs.writeFileSync(path.join(OUT, 'summary-plan' + PLAN + '.md'), out.join('\n') + '\n');
console.log(out.slice(0, 14).join('\n'));
console.log(`${WRITE ? 'wrote' : 'would write'} ${texts.length} Defs; inventory.csv sha256 ${csvSha}`);
module.exports = { rows, defs, chunks };
