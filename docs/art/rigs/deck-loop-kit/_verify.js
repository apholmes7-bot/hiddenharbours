/* Measures the deck-loop-kit rigs in a bare V8 host and checks the committed contract against
   them. No DOM, no host API, no deps (ADR 0021 §5). Run from the repo root:

       node docs/art/rigs/deck-loop-kit/_verify.js           # check the contract + the traps
       node docs/art/rigs/deck-loop-kit/_verify.js --emit    # re-emit the contract from the rigs

   Exits non-zero on any failure, so it is usable as a gate.

   WHY THIS EXISTS. Same reason as iso-rig-pack/_verify.js (#452): the contract under
   Assets/_Project/Art/Sprites/ is the ORACLE a baker asserts against, and the rule behind each
   number has to be MEASURED, never declared. Getting the rule wrong does not throw — it silently
   produces a cell that disagrees with the oracle on every key.

   THE THREE THINGS THIS FILE EXISTS TO PIN DOWN, all of which a README got to have an opinion on:

   1. HANDEDNESS. The kit's drop README declares "8 facings, N NE E SE S SW W NW, dir 0..7, CW".
      The repo's boats are the opposite (CounterClockwise), and docs/art/rigs/README.md is explicit
      that a declared order has shipped wrong five times. So it is measured here against the repo's
      OWN registered-CCW reference (iso-rig-pack/utility-iso), in one harness, one sign convention:

        utility-iso (registered CounterClockwise) : +X ground bearing steps +45 deg/dir
        deck-loop-kit (IsoSolid turntable)        : +X ground bearing steps -45 deg/dir

      Opposite signs, so the kit is CLOCKWISE and the drop README is RIGHT — which is the one
      outcome nobody should assume. A baker that applies the fleet's CCW correction to this kit
      mirrors all eight cells of every piece.

      Note the -46.75 deg/step figure in the iso-rig-pack contracts is the SCREEN mean of an
      alternating quantity (see that pack's VERIFICATION.md §6). This kit's screen mean is
      -46.75 too, with the opposite ground-plane sign — which is exactly why the screen mean must
      not be used as the handedness test. The ground-plane bearing is, and it needs the depth
      un-squash (/sin(elev)) before any angle is read off a projected point.

   2. THE CELL RULE. Every rig here returns a FIXED W x H buffer with a fixed integer pivot, so the
      rule is the wharfDecor/utilityIso one: pivot-INCLUSIVE union of the INK bbox across all
      facings, seeded at the pivot so a piece whose ink does not span its own pivot still contains
      it. It is recovered by measurement below, not assumed.

   3. WHAT IS ACTUALLY DIRECTIONAL. Every piece is byte-distinct at all eight facings, so a hash
      says "8 real facings" for all of them. Pixel agreement against dir 0 says something more
      useful: `baitbin` is a barrel (98.6%) and all eight buoys are spars (98.8-99.0%) — those rows
      turn barely at all, and a packer may collapse them. `haulerstation` is the opposite extreme
      (71.9%): it is the most strongly directional piece in the kit, which is the one you would
      most regret mirroring. `crabCone` looks rotationally symmetric in silhouette but measures
      92.7%, so it is NOT in the collapsible group despite the shape. None of this is in any
      README — it only exists if you count rendered pixels.
*/
'use strict';
const fs = require('fs'), vm = require('vm'), path = require('path'), crypto = require('crypto');

const ROOT = path.resolve(__dirname, '../../../..');
const KIT  = path.join(ROOT, 'docs/art/rigs/deck-loop-kit');
const RIGS = path.join(ROOT, 'docs/art/rigs');
const CONTRACT = path.join(ROOT, 'Assets/_Project/Art/Sprites/DeckGear/deckLoopKit.contract.json');

let failures = 0;
const ok   = (m) => console.log('  ok    ' + m);
const fail = (m) => { failures++; console.log('  FAIL  ' + m); };
const note = (m) => console.log('  --    ' + m);

// ---- host -----------------------------------------------------------------
// Load order is load-bearing: isoSolid first (every rig lathes against it), then the character
// chain (body delegates head delegates eye), then the kit. A missing prerequisite does not throw
// here — it renders the WRONG thing silently — so each global is asserted after the load.
const KIT_ART = ['isoSolid.js', 'deckGearRig.js', 'trapIsoRig.js', 'trapFaunaRig.js',
                 'trayIsoRig.js', 'buoyIsoRig.js'];
const TOP_CHAIN = ['eyeIsoRig.js', 'headIsoRig3.js', 'characterIsoRig6.js'];

function newCtx() {
  const ctx = { console, Math, JSON, Uint8ClampedArray, Uint8Array, Uint32Array, Int32Array,
                Float64Array, Float32Array, Array, Object, Number, String, Boolean, isNaN,
                isFinite, parseInt, parseFloat, Error, TypeError, RangeError, Map, Set };
  ctx.globalThis = ctx; ctx.window = ctx; ctx.self = ctx;
  return vm.createContext(ctx), ctx;
}
function loadKit() {
  const ctx = newCtx();
  for (const f of KIT_ART) vm.runInContext(fs.readFileSync(path.join(KIT, 'Art', f), 'utf8'), ctx, { filename: f });
  for (const f of TOP_CHAIN) vm.runInContext(fs.readFileSync(path.join(RIGS, f), 'utf8'), ctx, { filename: f });
  return ctx;
}

// ---- geometry -------------------------------------------------------------
function inkBox(buf, W, H) {
  let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++)
    if (buf[(y * W + x) * 4 + 3] > 0) {
      if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
    }
  return x1 < x0 ? null : { x0, y0, x1, y1 };
}
// The rule: pivot-INCLUSIVE union of the ink bbox over all facings on the fixed buffer.
function cellOf(render, dirs, W, H, px, py) {
  let x0 = px, y0 = py, x1 = px, y1 = py;                    // seeded AT the pivot
  for (let d = 0; d < dirs; d++) {
    const b = inkBox(render(d), W, H);
    if (!b) continue;
    x0 = Math.min(x0, b.x0); y0 = Math.min(y0, b.y0);
    x1 = Math.max(x1, b.x1); y1 = Math.max(y1, b.y1);
  }
  return { cellW: x1 - x0 + 1, cellH: y1 - y0 + 1, pivotX: px - x0, pivotY: py - y0 };
}
const sha = (b) => crypto.createHash('sha256')
  .update(Buffer.from(b.buffer ? new Uint8Array(b.buffer, b.byteOffset, b.byteLength) : b)).digest('hex');

// ---- the families ---------------------------------------------------------
// `keys` is the artwork axis; `dirs` is measured, never taken from a nativeDirs field (#452:
// install reported 0 facings for three of four packs — take facings from the CONTRACT).
function families(g) {
  return [
    { key: 'deckGear', global: 'DeckGear', rig: 'deckGearRig.js', dirs: 8,
      keys: g.DeckGear.KINDS, render: (k, d) => g.DeckGear.render(k, d, {}),
      W: g.DeckGear.W, H: g.DeckGear.H, px: g.DeckGear.pivot.x, py: g.DeckGear.pivot.y },
    { key: 'trap', global: 'TrapIso', rig: 'trapIsoRig.js', dirs: 8,
      keys: g.TrapIso.BUILDS, render: (k, d) => g.TrapIso.render(k, d, {}),
      W: g.TrapIso.W, H: g.TrapIso.H, px: g.TrapIso.pivot.x, py: g.TrapIso.pivot.y },
    { key: 'tray', global: 'FishTray2', rig: 'trayIsoRig.js', dirs: 8,
      keys: ['tray'], render: (k, d) => g.FishTray2.render(d, {}),
      W: g.FishTray2.W, H: g.FishTray2.H, px: g.FishTray2.pivot.x, py: g.FishTray2.pivot.y },
    { key: 'buoy', global: 'BuoyIso', rig: 'buoyIsoRig.js', dirs: 8,
      keys: g.BuoyIso.FLEET, render: (k, d) => g.BuoyIso.render(k, d, {}),
      W: g.BuoyIso.W, H: g.BuoyIso.H, px: g.BuoyIso.pivot.x, py: g.BuoyIso.pivot.y },
    // TrapFauna.render(kind, opts) takes NO dir — the catch inside a pot is not directional.
    // Absence of a facing axis is data, so it is recorded as dirs:1 rather than omitted.
    { key: 'trapFauna', global: 'TrapFauna', rig: 'trapFaunaRig.js', dirs: 1,
      keys: g.TrapFauna.KINDS, render: (k) => g.TrapFauna.render(k, {}),
      W: g.TrapFauna.W, H: g.TrapFauna.H, px: g.TrapFauna.pivot.x, py: g.TrapFauna.pivot.y },
  ];
}

// ---- measurement ----------------------------------------------------------
function measure(g) {
  const K = g.IsoSolid, elev = K.DEFAULT_ELEV, se = Math.sin(elev * Math.PI / 180);
  const P = (dir, v) => { const B = K.camBasis({ dir, elev }); return K.proj(v[0], v[1], v[2], B, 0, 0); };
  const bearing = (dir, v) => { const o = P(dir, [0,0,0]), p = P(dir, v);
    return Math.atan2(-(p.sy - o.sy) / se, p.sx - o.sx) * 180 / Math.PI; };   // un-squashed
  const bx = []; for (let d = 0; d < 8; d++) bx.push(bearing(d, [1,0,0]));
  const steps = []; for (let i = 1; i < 8; i++) {
    let d = bx[i] - bx[i-1]; while (d > 180) d -= 360; while (d <= -180) d += 360; steps.push(+d.toFixed(6)); }
  const uniq = [...new Set(steps)];

  const out = { elev, depthScale: se, groundStepDeg: uniq.length === 1 ? uniq[0] : uniq,
                convention: uniq.length === 1 && uniq[0] < 0 ? 'clockwise' : 'counterClockwise',
                families: [] };
  for (const f of families(g)) {
    const cells = [];
    for (const k of f.keys) {
      const c = cellOf((d) => f.render(k, d), f.dirs, f.W, f.H, f.px, f.py);
      // per-artwork facing data: how many of the `dirs` cells are actually distinct art?
      const seen = new Map();
      for (let d = 0; d < f.dirs; d++) { const h = sha(f.render(k, d)); seen.set(h, (seen.get(h) || 0) + 1); }
      // Exact-hash distinctness is a blunt instrument: ordered dither makes a barrel's eight cells
      // byte-distinct while they are the same artwork. Mean pixel agreement against dir 0 is the
      // measure a packer can act on (collapse a near-symmetric row), so record that too.
      let selfSim = null;
      if (f.dirs === 8) {
        const a = f.render(k, 0); let same = 0, tot = 0;
        for (let d = 1; d < 8; d++) { const b = f.render(k, d);
          for (let i = 0; i < a.length; i += 4) { tot++;
            if (a[i]===b[i] && a[i+1]===b[i+1] && a[i+2]===b[i+2] && a[i+3]===b[i+3]) same++; } }
        selfSim = +(100 * same / tot).toFixed(2);
      }
      // mirror: is cell d the horizontal mirror of cell (dirs-d)%dirs?
      let mirrorPct = null;
      if (f.dirs === 8) {
        let same = 0, tot = 0;
        for (let d = 1; d < 8; d++) {
          const a = f.render(k, d), b = f.render(k, (8 - d) % 8);
          for (let y = 0; y < f.H; y++) for (let x = 0; x < f.W; x++) {
            const i = (y*f.W + x)*4, j = (y*f.W + (f.W-1-x))*4; tot++;
            if (a[i]===b[j] && a[i+1]===b[j+1] && a[i+2]===b[j+2] && a[i+3]===b[j+3]) same++;
          }
        }
        mirrorPct = +(100 * same / tot).toFixed(2);
      }
      cells.push({ key: k, ...c, distinctFacings: seen.size, selfSimilarityPct: selfSim,
                   mirrorAgreementPct: mirrorPct });
    }
    out.families.push({ key: f.key, global: f.global, rig: f.rig, facings: f.dirs,
                        nativeW: f.W, nativeH: f.H, nativePivotX: f.px, nativePivotY: f.py, cells });
  }
  return out;
}

function buildContract(g, m) {
  return {
    kit: 'deck-loop-kit', version: 1,
    generated: 'measured from docs/art/rigs/deck-loop-kit/Art/*.js in a standalone V8 host at rig defaults',
    projection: {
      ppu: 32, elev: m.elev, depthScale: m.depthScale, antialias: false, alpha: 'binary',
      pivot: 'ground under the centre of the footprint',
      cellRule: 'pivot-INCLUSIVE union of the INK bbox across all facings on the fixed nativeW x nativeH ' +
                'buffer, seeded at the pivot. Same rule as wharfDecorRig / utilityIsoRig.',
      keyline: 'ringless per ADR 0031; {keyline:true} — or {outline:true}, the name the rest of the ' +
               'repo uses — is kept live on every kind as the A/B',
      keylineDefault: false,
    },
    azimuth: {
      convention: m.convention,
      measured: `+X ground-plane bearing steps ${m.groundStepDeg} deg per dir (depth un-squashed by ` +
                `/sin(${m.elev}deg)). The repo's registered-CounterClockwise reference ` +
                `(iso-rig-pack/utility-iso) steps +45 in this same harness, so this kit is the OPPOSITE ` +
                `handedness to the fleet's boats. The kit's drop README declares CW and is CORRECT.`,
      order: ['N','NE','E','SE','S','SW','W','NW'],
      warning: 'Do NOT apply the fleet CounterClockwise correction to this kit — it mirrors all eight cells.',
    },
    families: m.families,
  };
}

// ---- run ------------------------------------------------------------------
const g = loadKit();
for (const [nm, want] of [['IsoSolid',1],['DeckGear',1],['TrapIso',1],['TrapFauna',1],['TrapCatch',1],
                          ['FishTray2',1],['BuoyIso',1],['CharacterIso6',1],['HeadIso3',1],['EyeIso',1]])
  if (!g[nm]) { fail(`${nm} did not install — a missing prerequisite renders the wrong thing silently`); }

const m = measure(g);

if (process.argv.includes('--emit')) {
  fs.mkdirSync(path.dirname(CONTRACT), { recursive: true });
  fs.writeFileSync(CONTRACT, JSON.stringify(buildContract(g, m), null, 2) + '\n');
  console.log('wrote ' + path.relative(ROOT, CONTRACT));
  process.exit(0);
}

console.log('[1] handedness — measured against the repo\'s own registered-CCW reference');
{
  const ctx2 = newCtx();
  vm.runInContext(fs.readFileSync(path.join(RIGS, 'iso-rig-pack/utility-iso/utilityIsoRig.js'), 'utf8'), ctx2);
  const U = ctx2.UtilityIso, se = Math.sin(U.defaultElev * Math.PI / 180);
  const ub = []; for (let d = 0; d < 8; d++) {
    const o = U.project(d, [0,0,0], U.defaultElev), p = U.project(d, [1,0,0], U.defaultElev);
    ub.push(Math.atan2(-(p.y - o.y) / se, p.x - o.x) * 180 / Math.PI); }
  let us = ub[1] - ub[0]; while (us > 180) us -= 360; while (us <= -180) us += 360;
  (us > 0 ? ok : fail)(`reference utility-iso steps ${us.toFixed(0)} deg/dir (registered CounterClockwise)`);
  (m.groundStepDeg < 0 ? ok : fail)(`deck-loop-kit steps ${m.groundStepDeg} deg/dir => ${m.convention}`);
  (Math.sign(us) !== Math.sign(m.groundStepDeg) ? ok : fail)(
    'the two are OPPOSITE handedness — the fleet CCW correction must not be applied to this kit');
}

console.log('\n[2] the committed contract still matches the rigs');
if (!fs.existsSync(CONTRACT)) { fail('contract missing — run with --emit'); }
else {
  const C = JSON.parse(fs.readFileSync(CONTRACT, 'utf8'));
  (C.azimuth.convention === m.convention ? ok : fail)(`convention ${C.azimuth.convention}`);
  (Math.abs(C.projection.depthScale - m.depthScale) < 1e-12 ? ok : fail)(
    `depthScale ${C.projection.depthScale} = sin(${m.elev}deg)`);
  let n = 0, bad = 0;
  for (const fam of m.families) {
    const cf = C.families.find(x => x.key === fam.key);
    if (!cf) { fail(`family ${fam.key} absent from the contract`); continue; }
    if (cf.facings !== fam.facings) fail(`${fam.key}: facings ${cf.facings} != measured ${fam.facings}`);
    for (const cell of fam.cells) {
      const cc = cf.cells.find(x => x.key === cell.key); n++;
      if (!cc) { bad++; fail(`${fam.key}.${cell.key} absent`); continue; }
      for (const k of ['cellW','cellH','pivotX','pivotY','distinctFacings'])
        if (cc[k] !== cell[k]) { bad++; fail(`${fam.key}.${cell.key}.${k} ${cc[k]} != measured ${cell[k]}`); }
    }
  }
  (bad === 0 ? ok : fail)(`${n - bad}/${n} committed cells reproduce exactly from the live rigs`);
}

console.log('\n[3] traps a README cannot tell you');
{
  // 3a. what actually turns. A packer can collapse a rotationally-symmetric row from 8 cells to 1.
  const flat = [];
  for (const fam of m.families) for (const c of fam.cells)
    if (fam.facings === 8 && c.selfSimilarityPct >= 97) flat.push(`${fam.key}.${c.key}(${c.selfSimilarityPct}%)`);
  note(`near-rotationally-symmetric (>=97% agreement with dir 0): ${flat.join(' ') || '(none)'}`);
  note('  -> every piece is byte-distinct at all 8 facings (ordered dither), so a packer must use');
  note('     selfSimilarityPct, not a hash, to decide whether a row can collapse.');

  // 3b. mirror is per-artwork, never a family property.
  for (const fam of m.families) {
    if (fam.facings !== 8) continue;
    const r = fam.cells.map(c => `${c.key}:${c.mirrorAgreementPct}%`);
    note(`${fam.key} mirror-vs-(8-d): ${r.join(' ')}`);
  }
  const anyExactMirror = m.families.some(f => f.cells.some(c => c.mirrorAgreementPct === 100));
  (!anyExactMirror ? ok : fail)('no piece is an exact horizontal mirror pair — all 8 facings are real art');

  // 3c. TrapCatch is DOM-bound; only TrapFauna is bakeable headlessly.
  (typeof g.TrapCatch === 'object' ? ok : fail)('TrapCatch installs');
  // Use a kind TrapCatch owns ('urchin' is in TrapFauna.KINDS) so the call reaches its canvas path.
  // A kind it does NOT own delegates to CatchKit and returns null when CatchKit is absent, which
  // would make this trap silently "pass" — the exact shape of mistake this file exists to catch.
  let domBound = false;
  try { g.TrapCatch.item('urchin', {}); }
  catch (e) { domBound = /document|canvas|ImageData/.test(String(e && e.message)); }
  (domBound ? ok : fail)('TrapCatch.item() is DOM-bound (document/canvas) — bake via TrapFauna.render, not the facade');
  (g.TrapCatch.item('lobster', {}) === null ? ok : fail)(
    'TrapCatch delegates unknown kinds to CatchKit and returns null when it is absent — ' +
    'load catchKit.js + crustaceanRig.js beside it or the mixes come up empty, silently');

  // 3c-bis. …and the BAKEABLE half refuses those same kinds rather than substituting one. Before
  // the refusal landed, every unowned kind fell through to `DRAW[kind]||drawUrchin` and rendered a
  // byte-identical urchin — worse than TrapCatch's silent null, because a sheet of plausible
  // urchins is not a failure anyone reads as one.
  {
    let refused = 0;
    const DELEGATED = ['lobster', 'crab', 'jonah', 'short', 'nonsense'];
    for (const kind of DELEGATED) {
      try { g.TrapFauna.render(kind, {}); }
      catch (e) { if (/not one of this rig's kinds/.test(String(e && e.message))) refused++; }
    }
    (refused === DELEGATED.length ? ok : fail)(
      `TrapFauna.render refuses all ${DELEGATED.length} delegated/unknown kinds loudly ` +
      `(${refused} refused) — a bake cannot come up urchin-in-silence`);
    let drewAll = 0;
    for (const k of g.TrapFauna.KINDS) { try { g.TrapFauna.render(k, {}); drewAll++; } catch (e) {} }
    (drewAll === g.TrapFauna.KINDS.length ? ok : fail)(
      `…and still draws all ${g.TrapFauna.KINDS.length} kinds it DOES own — the refusal is a guard, ` +
      'not a rig that has stopped rendering');
  }

  // 3d. the work height is a world metre and DeckGear.station() is the only source of it.
  const st = g.DeckGear.station('haulerstation', 0, {});
  (st && st.workZ > 0 && st.turn === 4 ? ok : fail)(
    `haulerstation station(): workZ=${st && st.workZ} m, turn=${st && st.turn} (the one station whose operator faces OUTBOARD)`);
  (st && st.sheaveLocal ? ok : fail)(`sheave local = ${JSON.stringify(st && st.sheaveLocal)} m`);

  // 3e. per-hull trap capacity is DATA in the rig, not a game constant.
  const caps = g.TrapIso.CAPS;
  (caps && caps.lobsterBoat && caps.capeIslander ? ok : fail)(
    `TrapIso.CAPS carries per-hull stack limits: ${Object.keys(caps).map(k=>`${k} deck${caps[k].deck}/wb${caps[k].washboard}`).join(', ')}`);
}

console.log('\n[4] the KEYLINE_DEFAULT gate — what a bake mechanically refuses without');
{
  // IsoPackContract.AssertKeylineGated probes `<Global>.KEYLINE_DEFAULT` on the RIG's own global,
  // not on the turntable, and refuses the whole family when it is absent. The shipyard kit landed
  // that way and could not bake until #477 added one. All five of this kit's globals were in the
  // same state — and, unlike the shipyard's, their DEFAULT render was already ringless, because
  // each rig passed an explicit `keyline:false` to the shared pass. So the gap here was the
  // exported constant, not the behaviour: a missing declaration, mechanically indistinguishable
  // from ringed art.
  for (const nm of ['DeckGear', 'TrapIso', 'FishTray2', 'BuoyIso', 'TrapFauna']) {
    const v = g[nm] && g[nm].KEYLINE_DEFAULT;
    (typeof v !== 'undefined' && v === false ? ok : fail)(
      `${nm}.KEYLINE_DEFAULT === false (the bake gate reads the RIG's global, not IsoSolid's)`);
  }
  // The ring must still be reachable, or "0 ring pixels" would also pass on a renderer that had
  // simply stopped drawing. Both arms are required — the #477 shape.
  const ARMS = [
    { nm: 'deckGear', r: (o) => g.DeckGear.render('haulerstation', 0, o) },
    { nm: 'trap',     r: (o) => g.TrapIso.render('wood', 0, o) },
    { nm: 'tray',     r: (o) => g.FishTray2.render(0, o) },
    { nm: 'buoy',     r: (o) => g.BuoyIso.render('LobsterBoat', 0, o) },
  ];
  const opaque = (a) => { let n = 0; for (let i = 3; i < a.length; i += 4) if (a[i] !== 0) n++; return n; };
  for (const arm of ARMS) {
    const off = arm.r({}), on = arm.r({ keyline: true });
    (opaque(on) > opaque(off) ? ok : fail)(
      `${arm.nm}: the ring is a GATE, not a renderer that stopped — ${opaque(off)} px default, ` +
      `${opaque(on)} px with {keyline:true}`);
    // pure ring deletion: every pixel the ring adds was TRANSPARENT by default, so switching it
    // off cannot change a painted pixel of any piece.
    let painted = 0;
    for (let i = 0; i < off.length; i += 4)
      if (off[i+3] !== 0 && !(off[i]===on[i] && off[i+1]===on[i+1] && off[i+2]===on[i+2] && off[i+3]===on[i+3]))
        painted++;
    (painted === 0 ? ok : fail)(`${arm.nm}: pure ring deletion — ${painted} painted px differ`);
    // …and {outline:true}, the name #463/#477 use, must reach the SAME pass. It used to be
    // ignored here, so an A/B driven with the repo-standard name came back silently ringless.
    const alias = arm.r({ outline: true });
    let same = alias.length === on.length;
    for (let i = 0; same && i < alias.length; i++) if (alias[i] !== on[i]) same = false;
    (same ? ok : fail)(`${arm.nm}: {outline:true} is a live alias of {keyline:true}, byte for byte`);
  }
  // trapFauna has no ring pass at all — it is a flat 2D plotter. Forcing the arm must be a no-op,
  // which is what makes its exported `false` an honest statement rather than a copied one.
  {
    const off = g.TrapFauna.render('urchin', {}), on = g.TrapFauna.render('urchin', { keyline: true });
    let diff = 0; for (let i = 0; i < off.length; i++) if (off[i] !== on[i]) diff++;
    (diff === 0 ? ok : fail)('trapFauna is ringless BY CONSTRUCTION — no ring pass exists to gate');
  }
}

console.log('\n[5] determinism — two cold V8 hosts must agree byte-for-byte');
{
  const a = loadKit(), b = loadKit();
  let n = 0, diff = 0;
  for (const fam of families(a)) {
    const fb = families(b).find(x => x.key === fam.key);
    for (const k of fam.keys) for (let d = 0; d < fam.dirs; d++) {
      n++; if (sha(fam.render(k, d)) !== sha(fb.render(k, d))) diff++;
    }
  }
  (diff === 0 ? ok : fail)(`${n - diff}/${n} renders byte-identical across two cold hosts`);
}

console.log(failures ? `\n${failures} FAILED` : '\nall checks passed');
process.exit(failures ? 1 : 0);
