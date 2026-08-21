// S0 spike — WHAT IS THE EXTERIOR HALF OF ADR 0038's INTERIOR SWAP ON A MESH HULL?
//
// BoatInterior (#622) swaps two layers: the exterior's interior-facing draw goes off and the
// baked interior sheet comes on. The pilotable fleet draws as MESH FACETS (ADR 0022) and the
// house is geometry, so there is nothing obvious to hand the component as the exterior half.
// This harness MEASURES the candidates on one hull instead of arguing about them.
//
// It runs the rig files VERBATIM in node's V8 — the same engine ClearScript gives the bakers —
// so every number below is the rig's own rasteriser, not a re-implementation of it.
//
//   node docs/art/proofs/_boatInteriorExteriorHalfProof.mjs <repoRoot> [outDir]
//
// Node 22. No dependencies. Deterministic: the rigs carry no time or randomness on these paths.
// With <outDir> it also writes two proof sheets; without, it prints the measurements only.
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';

const ROOT = process.argv[2] ?? '.';
const OUT = process.argv[3] ?? null;
const rigs = (...p) => path.join(ROOT, 'docs/art/rigs', ...p);

// ---- loading a rig, with its own private globals ------------------------------------------
// The rigs publish through `root.<Global> = { … }`. Some symbols a bake needs are closed over
// and never exported, so RigMeshExtractor WIDENS that literal; this does the same thing for the
// same reason, and nothing else about the file is touched.
function load(file, into = {}, widen = null) {
  let src = fs.readFileSync(file, 'utf8');
  if (widen) {
    const at = src.indexOf(widen.marker);
    if (at < 0) throw new Error(`no export literal '${widen.marker}' in ${file}`);
    src = src.slice(0, at + widen.marker.length) + ' ' + widen.syms.map(s => `${s},`).join(' ')
        + src.slice(at + widen.marker.length);
  }
  if (widen?.transform) src = widen.transform(src);
  new Function('globalThis', 'window', 'module', 'exports', src)(into, into, { exports: {} }, {});
  return into;
}

// ---- MAIN's lobster rig, with each face tagged by the SOURCE LINE that pushed it -----------
// This is the ground truth for "does the face list carry an identifiable house group?" — it is
// recovered by instrumenting the rig, which is precisely the point: the grouping exists in the
// JS and is GONE by the time the extractor has read `Global.F`.
const HOUSE_SRC_LO = 335, HOUSE_SRC_HI = 380;      // the `// ---- WHEELHOUSE`/`EXTENDED HARDTOP` block
const SINKS = new Set(['211', '212', '213', '214', '215', '216']);   // face()/boxF()/tubeF() wrappers
const tagFaces = src => src
  .replace('const F = [];',
    'const F = []; const __p = F.push.bind(F); F.push = function(){' +
    ' const st = new Error().stack.split("\\n"), s = [];' +
    ' for (let k = 1; k < st.length; k++) { const m = st[k].match(/<anonymous>:(\\d+):/); if (m) s.push(m[1]); }' +
    // ONCE per face: swapping the face list re-pushes the same objects, and a recorder that
    // re-stamped would relabel every face with the swap's own call site.
    ' for (const a of arguments) if (a && typeof a === "object" && a.__line === undefined) a.__line = s.join(">");' +
    ' return __p.apply(null, arguments); };')
  // expose the frontmost-face index and the view depth per pixel (the depth-plane experiment)
  .replace('const dep=new Float32Array(PW*PH);',
           'const dep=new Float32Array(PW*PH); const own=new Int32Array(PW*PH).fill(-1);')
  .replace('zbuf[i]=deff; dep[i]=d;', 'zbuf[i]=deff; dep[i]=d; own[i]=__fi;')
  .replace('for(const f of faces){', 'let __fi=-1; for(const f of faces){ __fi++;')
  .replace('const out=new Array(PW*PH).fill(null);',
           'const out=new Array(PW*PH).fill(null); out.__dep=dep; out.__own=own;');

const EXT = load(rigs('lobsterBoatIsoRig.js'), {},
  { marker: 'root.LobsterBoatIso = {', syms: ['F', 'projVert', 'camBasis', '_paint'], transform: tagFaces }
).LobsterBoatIso;

const siteOf = f => { const c = String(f.__line).split('>'); return Number(c.find(s => !SINKS.has(s)) ?? c.at(-1)); };
const isHouse = f => { const s = siteOf(f); return s >= HOUSE_SRC_LO && s <= HOUSE_SRC_HI; };

// ---- the KIT's rig (publishes HOUSE/loft/DOOR; main's does NOT) + the interior renderer -----
const KIT = {};
load(rigs('boat-interiors-kit/hull-rigs/lobsterBoatIsoRig.js'), KIT);
load(rigs('boat-interiors-kit/boatInteriorRig.js'), KIT);
const HOUSE = KIT.LobsterBoatIso.HOUSE, BI = KIT.BoatInterior;
const CAR = JSON.parse(fs.readFileSync(rigs('boat-interiors-kit/lobsterBoatIsoRig.interior.json'), 'utf8'));

// ---- pixel helpers ------------------------------------------------------------------------
const W = EXT.W, H = EXT.H;
const alpha = px => { const m = new Uint8Array(px.length / 4); for (let i = 0; i < m.length; i++) m[i] = px[i * 4 + 3] > 0 ? 1 : 0; return m; };
const count = m => { let n = 0; for (const v of m) n += v; return n; };
const both = (a, b) => { let n = 0; for (let i = 0; i < a.length; i++) if (a[i] && b[i]) n++; return n; };
const only = (a, b) => { let n = 0; for (let i = 0; i < a.length; i++) if (a[i] && !b[i]) n++; return n; };
const recol = (p, q) => { let n = 0; for (let i = 0; i < p.length; i += 4) if (p[i+3] && q[i+3] && (p[i]!==q[i]||p[i+1]!==q[i+1]||p[i+2]!==q[i+2])) n++; return n; };
const over = (t, b) => { const o = new Uint8ClampedArray(b.length);
  for (let i = 0; i < o.length; i += 4) { const s = t[i+3] > 0 ? t : b; o[i]=s[i]; o[i+1]=s[i+1]; o[i+2]=s[i+2]; o[i+3]=s[i+3]; } return o; };

const FULL = EXT.F.slice();
function renderWith(faces, dir) {                     // swap the face list, render, put it back
  EXT.F.length = 0; for (const f of faces) EXT.F.push(f);
  const px = EXT.render(dir, {});
  EXT.F.length = 0; for (const f of FULL) EXT.F.push(f);
  return px;
}
const DIRL = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
const row = (...c) => console.log('  ' + c.map(([v, w]) => String(v).padEnd(w)).join(' '));

// ============================================================================================
console.log(`\n=== 0 · WHAT THE FACE LIST CARRIES ===`);
const truth = FULL.filter(isHouse), rest = FULL.filter(f => !isHouse(f));
const hist = a => { const o = {}; for (const f of a) o[f.mat] = (o[f.mat] ?? 0) + 1; return o; };
console.log(`  faces ${FULL.length}   fields per face: ${Object.keys(FULL[0]).filter(k => k !== '__line').join(', ')}`);
console.log(`  materials over the WHOLE hull: ${JSON.stringify(hist(FULL))}`);
console.log(`  ground-truth house set (rig source lines ${HOUSE_SRC_LO}-${HOUSE_SRC_HI}): ${truth.length} faces ${JSON.stringify(hist(truth))}`);
{
  const ex = (a, i) => [Math.min(...a.flatMap(f => f.v.map(v => v[i]))), Math.max(...a.flatMap(f => f.v.map(v => v[i])))];
  const f2 = r => `[${r[0].toFixed(2)}, ${r[1].toFixed(2)}]`;
  console.log(`  drawn house extent   x ${f2(ex(truth,0))}  y ${f2(ex(truth,1))}  z ${f2(ex(truth,2))}`);
  console.log(`  published HOUSE      x [-${HOUSE.hxAft}, ${HOUSE.hxAft}]  y [${HOUSE.yAft}, ${HOUSE.yFwd}]  z [${HOUSE.soleZ}, ${HOUSE.roofZ}]`);
  const cream = FULL.filter(f => f.mat === 'cream');
  console.log(`  'cream' (the house paint) spans x ${f2(ex(cream,0))} y ${f2(ex(cream,1))} over ${cream.length} faces — it is the TOPSIDES too`);
}

// ============================================================================================
console.log(`\n=== 1 · CAN THE PUBLISHED HOUSE ENVELOPE NAME THE SUBSET? ===`);
const hxAt = y => { const t = Math.max(0, Math.min(1, (y - HOUSE.yAft) / (HOUSE.yFwd - HOUSE.yAft)));
                    return HOUSE.hxAft + (HOUSE.hxFwd - HOUSE.hxAft) * t; };
const inHouse = ([x, y, z], tol) =>
  y >= HOUSE.yAft - tol && y <= HOUSE.yFwd + tol && z >= HOUSE.soleZ - tol && z <= HOUSE.roofZ + tol && Math.abs(x) <= hxAt(y) + tol;
const truthSet = new Set(truth);
row([ 'tol', 7], ['picked', 7], ['TP', 5], ['FP', 5], ['FN', 5], ['precision', 10], ['recall', 6]);
let bestTol = 0, bestF = -1;
for (const tol of [0, 0.05, 0.12, 0.25, 0.40]) {
  let picked = 0, tp = 0;
  for (const f of FULL) if (f.v.every(v => inHouse(v, tol))) { picked++; if (truthSet.has(f)) tp++; }
  const p = picked ? tp / picked : 0, r = tp / truth.length, f1 = p + r ? 2*p*r/(p+r) : 0;
  if (f1 > bestF) { bestF = f1; bestTol = tol; }
  row([tol.toFixed(2), 7], [picked, 7], [tp, 5], [picked - tp, 5], [truth.length - tp, 5], [p.toFixed(2), 10], [r.toFixed(2), 6]);
}
console.log(`  best F1 at tol ${bestTol.toFixed(2)} m`);
const ruleRest = FULL.filter(f => !f.v.every(v => inHouse(v, bestTol)));

// ============================================================================================
console.log(`\n=== 2 · IS THE INTERIOR SHEET EVER CO-VISIBLE, OR IS IT WHOLLY BEHIND HER? ===`);
console.log('  E = exterior px · Hp = px the house owns · I = interior sheet px');
row(['lvl', 7], ['dir', 4], ['E', 7], ['Hp', 7], ['I', 7], ['I&Hp', 7], ['I&(E-Hp)', 9], ['I-E', 5], ['Hp-I', 7]);
for (const level of CAR.cell.levels.map(l => l === 'house' ? 'house' : l)) {
  for (let dir = 0; dir < 8; dir++) {
    const e = EXT.render(dir, {}), c = renderWith(rest, dir), i = BI.render(dir, { hull: 'lobster', level });
    const mE = alpha(e), mI = alpha(i);
    const mHp = new Uint8Array(mE.length);
    for (let p = 0; p < mE.length; p++)
      mHp[p] = (e[p*4+3] > 0 && (c[p*4+3] === 0 || e[p*4] !== c[p*4] || e[p*4+1] !== c[p*4+1] || e[p*4+2] !== c[p*4+2])) ? 1 : 0;
    let iRest = 0; for (let p = 0; p < mI.length; p++) if (mI[p] && mE[p] && !mHp[p]) iRest++;
    row([level, 7], [DIRL[dir], 4], [count(mE), 7], [count(mHp), 7], [count(mI), 7],
        [both(mI, mHp), 7], [iRest, 9], [only(mI, mE), 5], [only(mHp, mI), 7]);
  }
}

// ============================================================================================
console.log(`\n=== 3 · WHAT A MIS-CULL COSTS, IN PIXELS ===`);
row(['dir', 4], ['truth-cull+sheet', 17], ['rule-cull+sheet', 16], ['differing', 10], ['% of truth', 10]);
for (const dir of [0, 2, 4, 6]) {
  const a = over(BI.render(dir, { hull: 'lobster', level: 'house' }), renderWith(rest, dir));
  const b = over(BI.render(dir, { hull: 'lobster', level: 'house' }), renderWith(ruleRest, dir));
  const mA = alpha(a), mB = alpha(b);
  let d = 0; for (let p = 0; p < mA.length; p++) if (mA[p] !== mB[p]) d++;
  d += recol(a, b);
  row([DIRL[dir], 4], [count(mA), 17], [count(mB), 16], [d, 10], [((d / count(mA)) * 100).toFixed(1) + '%', 10]);
}

// ============================================================================================
console.log(`\n=== 4 · DOES THE EXISTING PER-PIXEL DECK SPLIT ISOLATE THE HOUSE? ===`);
console.log('  The facet shader already cuts on `i.wpos.z < _DeckOccupant[k].x` — one depth PLANE.');
const houseIdx = new Set(); FULL.forEach((f, i) => { if (isHouse(f)) houseIdx.add(i); });
const poly = CAR.WALKABLE.house_sole.polygon;
const REFS = {
  'house sole centroid': [poly.reduce((s, q) => s + q[0], 0) / poly.length,
                          poly.reduce((s, q) => s + q[1], 0) / poly.length, CAR.WALKABLE.house_sole.z],
  'door sill': CAR.THRESHOLD.threshold_point,
};
for (const [name, P] of Object.entries(REFS)) {
  console.log(`  --- plane at the ${name} (${P.map(n => (+n).toFixed(2)).join(', ')}) ---`);
  row(['dir', 4], ['cut px', 8], ['Hp', 7], ['TP', 7], ['collateral', 11], ['FN', 6], ['precision', 10], ['recall', 6]);
  for (const dir of [0, 2, 4, 6]) {
    const B = EXT.camBasis({ dir });
    const refD = EXT.projVert(P[0], P[1], P[2], B, null).d;
    const out = EXT._paint(FULL, { dir }, true, null, undefined);
    let cut = 0, hp = 0, tp = 0;
    for (let i = 0; i < out.__own.length; i++) {
      if (out.__own[i] < 0) continue;
      const h = houseIdx.has(out.__own[i]); if (h) hp++;
      if (out.__dep[i] < refD) { cut++; if (h) tp++; }
    }
    row([DIRL[dir], 4], [cut, 8], [hp, 7], [tp, 7], [cut - tp, 11], [hp - tp, 6],
        [(cut ? tp / cut : 0).toFixed(2), 10], [(hp ? tp / hp : 0).toFixed(2), 6]);
  }
}

// ============================================================================================
console.log(`\n=== 5 · THE SAME QUESTION ON TWO SHIPS (3 levels each) ===`);
for (const [file, sym, key] of [['sideDraggerIsoRig.js', 'SideDraggerIso', 'dragger'],
                                ['sternTrawlerIsoRig.js', 'SternTrawlerIso', 'trawler']]) {
  const k = {}; load(rigs('boat-interiors-kit/hull-rigs/' + file), k); load(rigs('boat-interiors-kit/boatInteriorRig.js'), k);
  const M = load(rigs(file), {}, { marker: `root.${sym} = {`, syms: ['F'] })[sym];
  console.log(`  ${key}: ${M.F.length} faces, materials ${JSON.stringify(hist(M.F))}`);
  for (const level of k.BoatInterior.dims({ hull: key }).levels) {
    let line = `    ${level.padEnd(7)}`;
    for (const dir of [0, 2, 4]) {
      const mE = alpha(M.render(dir, {})), mI = alpha(k.BoatInterior.render(dir, { hull: key, level }));
      line += `  ${DIRL[dir]}: I=${String(count(mI)).padStart(6)} I&E=${String(both(mI, mE)).padStart(6)} I-E=${String(only(mI, mE)).padStart(4)}`;
    }
    console.log(line);
  }
}

// ---- proof sheets (optional) ---------------------------------------------------------------
if (OUT) {
  const CRC = (() => { const t = new Int32Array(256);
    for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c; } return t; })();
  const crc32 = b => { let c = -1; for (const x of b) c = CRC[(c ^ x) & 0xff] ^ (c >>> 8); return (c ^ -1) >>> 0; };
  const chunk = (ty, d) => { const l = Buffer.alloc(4); l.writeUInt32BE(d.length);
    const td = Buffer.concat([Buffer.from(ty, 'ascii'), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc32(td));
    return Buffer.concat([l, td, c]); };
  const png = (p, px, w, h) => { const raw = Buffer.alloc((w * 4 + 1) * h);
    for (let y = 0; y < h; y++) { raw[y * (w * 4 + 1)] = 0;
      for (let x = 0; x < w * 4; x++) raw[y * (w * 4 + 1) + 1 + x] = px[y * w * 4 + x]; }
    const ih = Buffer.alloc(13); ih.writeUInt32BE(w, 0); ih.writeUInt32BE(h, 4); ih[8] = 8; ih[9] = 6;
    fs.writeFileSync(p, Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]), chunk('IHDR', ih),
      chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))])); };
  const sheet = (name, dirs, cells) => {
    const cols = cells.length, buf = new Uint8ClampedArray(W * cols * H * dirs.length * 4);
    dirs.forEach((dir, r) => cells.forEach((make, c) => { const px = make(dir);
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const s = (y * W + x) * 4, d = (((r * H) + y) * (W * cols) + (c * W + x)) * 4;
        buf[d] = px[s]; buf[d+1] = px[s+1]; buf[d+2] = px[s+2]; buf[d+3] = px[s+3]; } }));
    png(path.join(OUT, name), buf, W * cols, H * dirs.length);
    console.log(`  wrote ${name}  ${W * cols}x${H * dirs.length}`);
  };
  const hs = dir => BI.render(dir, { hull: 'lobster', level: 'house' });
  const cu = dir => BI.render(dir, { hull: 'lobster', level: 'cuddy' });
  console.log('\n=== proof sheets (rows N, E, S) ===');
  sheet('boat-interior-exterior-half-A.png', [0, 2, 4],
        [d => EXT.render(d, {}), d => renderWith(rest, d), hs,
         d => over(hs(d), renderWith(rest, d)), d => over(hs(d), EXT.render(d, {}))]);
  console.log('   A columns: exterior | house culled | interior sheet | culled+sheet | sheet over intact');
  sheet('boat-interior-exterior-half-B.png', [0, 2, 4],
        [d => over(hs(d), renderWith(rest, d)), d => over(hs(d), renderWith(ruleRest, d)),
         d => over(cu(d), renderWith(rest, d)), d => over(cu(d), EXT.render(d, {}))]);
  console.log('   B columns: truth-cull+house | RULE-cull+house | truth-cull+CUDDY | exterior+CUDDY');
}
