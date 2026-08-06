/* Verifies the four ISO-rig-pack contracts against the live rigs, in a bare V8 host.
   No DOM, no host API, no deps (ADR 0021 §5). Run from the repo root:

       node docs/art/rigs/iso-rig-pack/_verify.js            # contracts + traps
       node docs/art/rigs/iso-rig-pack/_verify.js --gate     # + the keyline-gate A/B

   Exits non-zero on any failure, so it is usable as a gate.

   WHY THIS EXISTS. The four contracts committed under Assets/_Project/Art/Sprites/ are the
   ORACLE the bakers assert against, but the script that generated them was never committed — so
   the RULE behind each number had to be recovered by measurement before a baker could reimplement
   it. That rule is CELL_RULES below, and it is not the same for all four families. Getting it
   wrong does not throw; it silently produces a cell that disagrees with the oracle on every key.

   THE RULE, per family (recovered, then verified key-by-key — see VERIFICATION.md):

     wharfIso    cell = pivot-aligned union of the rig's RETURNED BUFFER extents over 8 facings,
                 floor/ceil. NOT the ink bbox: measuring ink gives floatSet 751x556 against the
                 committed 757x592, and every one of the 17 keys disagrees.
     wharfDecor  fixed 420x520 buffer -> cell = pivot-INCLUSIVE union of the INK bbox over 8
     utilityIso  fixed 440x620 buffer    facings, seeded at the pivot so a piece whose ink does
                                         not span its own pivot still contains it.
     shoreFinds  cell = cellOf(key) verbatim. It is ANALYTIC (no render, no raster), so it is
                 independent of anything the renderer does, the keyline included.
*/
'use strict';
const fs = require('fs'), vm = require('vm'), path = require('path');

const ROOT = path.resolve(__dirname, '../../../..');
const PACK = path.join(ROOT, 'docs/art/rigs/iso-rig-pack');
const SPR  = path.join(ROOT, 'Assets/_Project/Art/Sprites');

const FAMILIES = [
  { key: 'wharfIso',   global: 'WharfIso',   src: 'wharf-kit-iso/wharfIsoRig.js',
    contract: 'Wharf/Iso/wharfIsoRig.contract.json',        rule: 'bufferUnion' },
  { key: 'wharfDecor', global: 'WharfDecor', src: 'wharf-decor-iso/wharfDecorRig.js',
    contract: 'Wharf/Decor/wharfDecorRig.contract.json',    rule: 'inkUnionFixed' },
  { key: 'utilityIso', global: 'UtilityIso', src: 'utility-iso/utilityIsoRig.js',
    contract: 'Utility/utilityIsoRig.contract.json',        rule: 'inkUnionFixed' },
  { key: 'shoreFinds', global: 'ShoreFinds', src: 'shoreline-finds-iso/shoreFindsRig.js',
    contract: 'Shore/FindsIso/shoreFindsRig.contract.json', rule: 'analytic' },
];

let failures = 0;
const ok   = (m) => console.log('  ok    ' + m);
const fail = (m) => { failures++; console.log('  FAIL  ' + m); };
const note = (m) => console.log('  --    ' + m);

// ---- host -----------------------------------------------------------------
// The ring is a PASS, not a colour: it writes KEY into empty pixels that touch ink. Gating it
// means skipping the pass. Filtering by colour instead would punch holes through every interior
// pixel that legitimately sits at the keyline value (radioMast's lattice is 551 of them).
const RING_PASS = /if\(touch\) out\[i\] *= *KEY;/;
function loadRigs(list, keyline) {
  const ctx = { console, Math, JSON, Uint8ClampedArray, Uint8Array, Float64Array, Array, Object,
                Number, String, Boolean, isNaN, isFinite, parseInt, parseFloat };
  ctx.KEYLINE_DEFAULT = keyline !== false;
  ctx.globalThis = ctx; ctx.window = ctx; ctx.self = ctx;
  vm.createContext(ctx);
  for (const f of list) {
    let src = fs.readFileSync(path.join(PACK, f.src), 'utf8');
    if (keyline === false) {
      // Simulate the art-director's KEYLINE_DEFAULT=false gate ahead of it landing upstream.
      // Once the rigs ship their own gate this transform becomes a no-op and can be dropped.
      src = src.replace(RING_PASS, 'if(touch && KEYLINE_DEFAULT) out[i]=KEY;');
    }
    vm.runInContext(src, ctx, { filename: path.basename(f.src) });
  }
  return ctx;
}
const contractOf = (f) => JSON.parse(fs.readFileSync(path.join(SPR, f.contract), 'utf8'));

// ---- geometry -------------------------------------------------------------
function inkBox(buf, W, H) {
  let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    if (buf[(y * W + x) * 4 + 3] > 0) {
      if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
    }
  }
  return x1 < x0 ? null : { x0, y0, x1, y1 };
}
// wharfIso: the rig sizes its own buffer and reports a FRACTIONAL px,py per bake.
function cellBufferUnion(rig, key, opts) {
  let L = 1e9, R = -1e9, T = 1e9, B = -1e9;
  for (let d = 0; d < 8; d++) {
    const r = rig.render(key, d, opts || {});
    L = Math.min(L, -r.px); R = Math.max(R, r.w - 1 - r.px);
    T = Math.min(T, -r.py); B = Math.max(B, r.h - 1 - r.py);
  }
  return { w: Math.ceil(R) - Math.floor(L) + 1, h: Math.ceil(B) - Math.floor(T) + 1,
           px: -Math.floor(L), py: -Math.floor(T) };
}
// wharfDecor / utilityIso: fixed buffer, fixed pivot, render() returns a BARE RGBA array.
function cellInkUnionFixed(rig, key) {
  const W = rig.W, H = rig.H, px = rig.pivot.x, py = rig.pivot.y;
  let L = 0, R = 0, T = 0, B = 0, inside = false, any = false;   // seeded at 0 => pivot-inclusive
  for (let d = 0; d < 8; d++) {
    const b = inkBox(rig.render(key, d, {}), W, H);
    if (!b) continue;
    any = true;
    L = Math.min(L, b.x0 - px); R = Math.max(R, b.x1 - px);
    T = Math.min(T, b.y0 - py); B = Math.max(B, b.y1 - py);
    if (px >= b.x0 && px <= b.x1 && py >= b.y0 && py <= b.y1) inside = true;
  }
  return any ? { w: R - L + 1, h: B - T + 1, px: -L, py: -T, pivotInsideInk: inside } : null;
}

// ---- 1. contracts reproduce ----------------------------------------------
function verifyContracts(ctx) {
  for (const f of FAMILIES) {
    const rig = ctx[f.global], C = contractOf(f);
    const byKey = {}; for (const e of C.cells) byKey[e.key] = e;
    console.log(`\n[${f.key}] ${C.cells.length} cells, rule=${f.rule}`);
    let cOk = 0, pOk = 0, iOk = 0, n = 0, bad = [];
    const keys = f.rule === 'bufferUnion' ? rig.presets() : rig.list();
    for (const k of keys) {
      n++;
      const e = byKey[k];
      if (!e) { bad.push(`${k}: absent from contract`); continue; }
      let m;
      if (f.rule === 'bufferUnion')        m = cellBufferUnion(rig, k);
      else if (f.rule === 'inkUnionFixed') m = cellInkUnionFixed(rig, k);
      else { const c = rig.cellOf(k); m = { w: c.w, h: c.h, px: c.pvx, py: c.pvy }; }
      if (!m) { bad.push(`${k}: renders no ink`); continue; }
      if (e.cellW === m.w && e.cellH === m.h) cOk++;
      else bad.push(`${k}: cell contract ${e.cellW}x${e.cellH} vs measured ${m.w}x${m.h}`);
      if (e.pivotX === m.px && e.pivotY === m.py) pOk++;
      else bad.push(`${k}: pivot contract ${e.pivotX},${e.pivotY} vs measured ${m.px},${m.py}`);
      if (e.pivotInsideInk === undefined) iOk++;
      else if (e.pivotInsideInk === m.pivotInsideInk) iOk++;
      else bad.push(`${k}: pivotInsideInk contract ${e.pivotInsideInk} vs measured ${m.pivotInsideInk}`);
    }
    (cOk === n ? ok : fail)(`cells   ${cOk}/${n} match the contract`);
    (pOk === n ? ok : fail)(`pivots  ${pOk}/${n} match the contract`);
    (iOk === n ? ok : fail)(`pivotInsideInk ${iOk}/${n} match the contract`);
    for (const b of bad) note(b);
    // sheet packing is self-consistent and covers 8 facings
    let sOk = 0;
    for (const e of C.cells) {
      const s = e.sheet;
      if (s.sheetW === s.cols * e.cellW && s.sheetH === s.rows * e.cellH) sOk++;
    }
    (sOk === C.cells.length ? ok : fail)(
      `sheets  ${sOk}/${C.cells.length} satisfy sheetW=cols*cellW, sheetH=rows*cellH`);
  }
}

// ---- 2. the measured traps ------------------------------------------------
function verifyTraps(ctx) {
  const { WharfIso, WharfDecor, UtilityIso, ShoreFinds } = ctx;

  console.log('\n[trap 1] union cell != naive per-facing maximum');
  for (const k of ['floatSet', 'plasticSet']) {
    const u = cellBufferUnion(WharfIso, k);
    let nw = 0, nh = 0;
    for (let d = 0; d < 8; d++) { const r = WharfIso.render(k, d, {}); nw = Math.max(nw, r.w); nh = Math.max(nh, r.h); }
    (u.w > nw * 1.5 ? ok : fail)(`${k}: union ${u.w}x${u.h} vs naive max ${nw}x${nh} ` +
                                 `(${(u.w / nw).toFixed(2)}x wider) — a cap off the naive number downscales`);
  }

  console.log('\n[trap 2] the wharf cell is PARAMETRIC — a fixed cap is not a fixed guarantee');
  for (const [label, opts] of [['bays: 8', { bays: 8 }], ['tideRange: 14 (Fundy)', { tideRange: 14 }]]) {
    const c = cellBufferUnion(WharfIso, 'sheetedPier', opts);
    (c.w > 700 || c.h > 500 ? ok : fail)(`sheetedPier @ ${label}: cell ${c.w}x${c.h} ` +
      `(defaults ${(() => { const d = cellBufferUnion(WharfIso, 'sheetedPier'); return d.w + 'x' + d.h; })()}) ` +
      `— the native-res assert must read RENDERED cells, never a table`);
  }

  // A texture cap binds on the LONGEST SIDE, so `worstSheet` (worst by AREA) is the wrong field to
  // size headroom off. Every contract therefore also carries `worstSheetByMaxDim`; assert it.
  console.log('\n[trap 2b] packed sheets against each family\'s ruled import cap');
  for (const f of FAMILIES) {
    const C = contractOf(f);
    let worst = { m: 0 };
    for (const e of C.cells) {
      // wharfIso re-measures from the live rig (its cells are parametric); the fixed-sheet families
      // pack from a committed cell, so the contract's own sheet figures are the thing to check.
      const c = f.rule === 'bufferUnion' ? cellBufferUnion(ctx[f.global], e.key)
                                         : { w: e.cellW, h: e.cellH };
      const sw = e.sheet.cols * c.w, sh = e.sheet.rows * c.h, m = Math.max(sw, sh);
      if (m > worst.m) worst = { m, key: e.key, sw, sh };
    }
    const cap = C.importSizeCap, d = C.worstSheetByMaxDim;
    (worst.m <= cap ? ok : fail)(`${f.key}: worst sheet by MAX DIMENSION is ${worst.key} ` +
      `${worst.sw}x${worst.sh} = ${worst.m} px, ${cap - worst.m} px under the ${cap} cap`);
    if (!d) fail(`${f.key}: contract declares no worstSheetByMaxDim — headroom cannot be sized off ` +
                 `worstSheet, which is the worst by AREA`);
    else (d.key === worst.key && d.maxDim === worst.m && d.headroomToCap === cap - worst.m ? ok : fail)(
      `${f.key}: worstSheetByMaxDim reproduces (${d.key} ${d.maxDim} px, ${d.headroomToCap} px headroom)`);
  }

  console.log('\n[trap 3] fireCabinet is wall-hung — its pivot lies outside its own ink');
  {
    const px = WharfDecor.pivot.x, py = WharfDecor.pivot.y;
    let gap = 1e9;
    for (let d = 0; d < 8; d++) {
      const b = inkBox(WharfDecor.render('fireCabinet', d, {}), WharfDecor.W, WharfDecor.H);
      if (b) gap = Math.min(gap, py - b.y1);
    }
    (gap > 0 ? ok : fail)(`fireCabinet ink stops ${gap} px ABOVE the pivot — a crop tight to ink puts ` +
                          `its ground contact outside its own cell`);
    const others = contractOf(FAMILIES[1]).cells.filter(e => e.pivotInsideInk === false).map(e => e.key);
    (others.length === 1 && others[0] === 'fireCabinet' ? ok : fail)(
      `pivotInsideInk=false is exactly [${others.join(', ')}] — assert it per piece, do not assume`);
  }

  console.log('\n[trap 4] ShoreFinds breaks the default loader TWICE');
  (typeof ShoreFinds.DIRS !== 'number' ? ok : fail)(
    `ShoreFinds.DIRS is ${JSON.stringify(ShoreFinds.DIRS)} (typeof "${typeof ShoreFinds.DIRS}") — ` +
    `Install's \`typeof DIRS === 'number'\` reports 0 facings SILENTLY`);
  (ShoreFinds.pivot === undefined ? ok : fail)(
    `ShoreFinds declares no pivot — plain Install throws; load via InstallModule + cellOf(key)`);
  // ... and so, less visibly, do two of the three that are documented as safe:
  for (const g of ['WharfDecor', 'UtilityIso']) {
    if (typeof ctx[g].DIRS !== 'number')
      note(`${g}.DIRS is ${JSON.stringify(ctx[g].DIRS)} — Install reports 0 facings here too, ` +
           `though its contract declares facings: 8. Source facings from the CONTRACT, not nativeDirs.`);
  }

  console.log('\n[trap 5] ground squash is PER RIG — never a shared constant');
  const q = ShoreFinds.Q, iso = Math.sin(WharfDecor.defaultElev * Math.PI / 180);
  (q === 0.72 ? ok : fail)(`ShoreFinds.Q = ${q}`);
  (Math.abs(iso - 0.6427876096865393) < 1e-12 ? ok : fail)(
    `iso rigs squash = sin(${WharfDecor.defaultElev}deg) = ${iso.toFixed(10)}`);
  (q !== iso ? ok : fail)(`the two differ — carrying one across families is the un-squash mistake`);

  console.log('\n[trap 6] azimuth — measured, because a label is not truth');
  for (const [nm, rig] of [['WharfIso', WharfIso], ['WharfDecor', WharfDecor], ['UtilityIso', UtilityIso]]) {
    const e = rig.defaultElev, se = Math.sin(e * Math.PI / 180), gp = [];
    for (let d = 0; d < 8; d++) {
      const o = rig.project(d, [0, 0, 0], e), p = rig.project(d, [1, 0, 0], e);
      gp.push(Math.atan2(-(p.y - o.y) / se, p.x - o.x) * 180 / Math.PI);   // un-squash to the ground plane
    }
    const steps = new Set();
    for (let i = 1; i < 8; i++) { let d = gp[i] - gp[i - 1]; while (d > 180) d -= 360; while (d < -180) d += 360; steps.add(+d.toFixed(6)); }
    (steps.size === 1 && [...steps][0] === 45 ? ok : fail)(`${nm}: facings are exactly ${[...steps].join('/')} deg apart in the ground plane`);
    // the repo's own convention test (BuildingRigAzimuthProbe): a +Y face at the cell labelled 'E'
    const o = rig.project(2, [0, 0, 0], e), face = rig.project(2, [0, 1, 0], e);
    (face.x - o.x < 0 ? ok : fail)(`${nm}: +Y face lands ${(face.x - o.x).toFixed(0)} px (screen-WEST) at order[2]="${rig.order[2]}" => CounterClockwise`);
  }

  console.log('\n[trap 7] utility spans are NEVER baked');
  {
    const withTies = UtilityIso.list().filter(k => UtilityIso.PROPS[k] && UtilityIso.PROPS[k].ties);
    (withTies.length > 0 ? ok : fail)(`${withTies.length} of ${UtilityIso.list().length} pieces export ties()`);
    const t = UtilityIso.PROPS[withTies[0]].ties({ len: 0 });
    const kinds = Object.keys(t);
    (kinds.length > 0 ? ok : fail)(`${withTies[0]}.ties() -> {${kinds.join(', ')}} in METRES — ` +
      `bake the poles, let a runtime consumer draw the catenary; import the ties data beside the sheets`);
  }
}

// ---- 3. the keyline gate A/B ---------------------------------------------
// "Zero ring pixels" ALONE also passes on a renderer that draws nothing. The positive control is
// what makes it evidence: the ring must come BACK when the gate is on.
function verifyGate() {
  console.log('\n[gate] keyline A/B — ringless bake + positive control');
  const on = loadRigs(FAMILIES.slice(0, 3), true), off = loadRigs(FAMILIES.slice(0, 3), false);
  const KEY = '#1a1c22';
  const kc = [1, 3, 5].map(i => parseInt(KEY.substr(i, 2), 16));
  const ring = (buf, W, H) => {
    let n = 0;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = (y * W + x) * 4;
      if (buf[i + 3] === 0 || buf[i] !== kc[0] || buf[i + 1] !== kc[1] || buf[i + 2] !== kc[2]) continue;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = x + dx, ny = y + dy;
        if (nx < 0 || nx >= W || ny < 0 || ny >= H || buf[(ny * W + nx) * 4 + 3] === 0) { n++; break; }
      }
    }
    return n;
  };
  for (const [g, key] of [['WharfDecor', 'trapStack'], ['UtilityIso', 'powerPole'], ['UtilityIso', 'radioMast']]) {
    const A = on[g], B = off[g];
    const rOn = ring(A.render(key, 0, {}), A.W, A.H), rOff = ring(B.render(key, 0, {}), B.W, B.H);
    (rOff === 0 ? ok : fail)(`${g}.${key}: gated bake has ZERO ring pixels`);
    (rOn > 0 ? ok : fail)(`${g}.${key}: ring returns with the gate on (${rOn} px) — positive control`);
  }
  // What the gate costs the committed cells, per family, measured against each family's OWN rule.
  console.log('\n[gate] how many committed cells MOVE when the ring is gated off');
  let moved = 0, total = 0;
  for (const g of ['WharfDecor', 'UtilityIso']) {
    const A = on[g], B = off[g], hist = {};
    let m = 0;
    for (const k of A.list()) {
      total++;
      const a = cellInkUnionFixed(A, k), b = cellInkUnionFixed(B, k);
      const d = `${b.w - a.w},${b.h - a.h}`;
      hist[d] = (hist[d] || 0) + 1;
      if (d !== '0,0') { m++; moved++; }
    }
    note(`${g}: ${m}/${A.list().length} cells move — deltas ${JSON.stringify(hist)}`);
  }
  {
    const A = on.WharfIso, B = off.WharfIso, hist = {};
    let m = 0;
    for (const k of A.presets()) {
      total++;
      const a = cellBufferUnion(A, k), b = cellBufferUnion(B, k);
      const d = `${b.w - a.w},${b.h - a.h}`;
      hist[d] = (hist[d] || 0) + 1;
      if (d !== '0,0') { m++; moved++; }
    }
    note(`WharfIso: ${m}/17 cells move — deltas ${JSON.stringify(hist)}. Its cell comes from the ` +
         `rig's BUFFER, which the geometry sizes before the ring pass runs, so the ring is free here.`);
  }
  {
    const SF = loadRigs([FAMILIES[3]], true).ShoreFinds;
    total += SF.list().length;
    note(`ShoreFinds: 0/${SF.list().length} cells move — cellOf() is analytic (no render, no raster), ` +
         `so no renderer change can reach it.`);
  }
  note(`TOTAL: ${moved} of ${total} committed cell entries move under the gate. ` +
       `Regenerate the contracts in the same slice that takes the gated rigs.`);
}

// ---- run ------------------------------------------------------------------
console.log('ISO rig pack — contract verification (bare V8, no DOM, no deps)');
const ctx = loadRigs(FAMILIES, true);
verifyContracts(ctx);
verifyTraps(ctx);
if (process.argv.includes('--gate')) verifyGate();
console.log(failures === 0 ? '\nPASS — every contract measurement reproduces.'
                           : `\nFAIL — ${failures} check(s) did not reproduce.`);
process.exit(failures === 0 ? 0 : 1);
