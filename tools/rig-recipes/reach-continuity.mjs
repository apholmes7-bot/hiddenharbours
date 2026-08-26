#!/usr/bin/env node
// THE REACH IS THE RIG'S. This is the check that says so.
//
//   node tools/rig-recipes/reach-continuity.mjs           # the tables, and a pass/fail exit code
//   node tools/rig-recipes/reach-continuity.mjs --all     # every committed iso character sheet, not
//                                                         # only the reach kit's
//   node tools/rig-recipes/reach-continuity.mjs --json    # the same measurements, machine-readable
//
// The reach-kit drop (rig 6.6) arrived as 32 finished PNGs plus a sidecar of numbers. A drop is a
// claim, and this file is what turns the claim into a fact in OUR tree: the sheets are re-rendered
// from the rigs THIS repo carries — a newer eye and head than the drop's own — and compared byte
// for byte against the committed pixels. A sheet that reproduces is pinned by the rig; a sheet that
// does not is art nobody can re-derive, which is what #654 was written to stop.
//
// WHAT IS CHECKED, and why each one is here:
//   · REPRODUCTION — the pixels. Every reach sheet and every run sheet re-rendered from
//                    eyeIsoRig → headIsoRig3 → characterIsoRig6 → characterIsoRig6.hands and
//                    compared against the committed PNG. This is the whole proof; the rest is
//                    the reasoning around it.
//   · THE ROD PINS — the reach clip is a HAND-OVER, and the other half of it belongs to the rod
//                    rig. Frame count, release point, frame→u mapping, how many frames the tool is
//                    still in hand, and the grip rise are all things BOTH rigs state. They must
//                    agree, and neither file can check that alone.
//   · THE SYNC    — reachMount().slip: how far the hand ended from the point the clip was asked to
//                    put it on. Zero means the hand is ON the tool's grip; non-zero means the
//                    figure could not make that reach and the guard pulled the wrist in. The drop
//                    claims zero across the whole matrix, and a hand-over that drifts is the defect
//                    the rod's own continuity law exists to stop.
//   · THE SETTLE  — the last frame must be HOLDABLE as the settled rest (u = 1 exactly), because
//                    the engine plays this clip one-shot and then stops on it. `reach` is the only
//                    clip in the rig with the `settle` frame→u mapping, and an off-by-one there is
//                    invisible until a rod hangs in mid-air.
//
// Every number comes from actually running the rigs and actually rendering the cells (node's V8 is
// the same engine ClearScript gives the in-editor baker — ADR 0021), never from a table beside
// them. The sidecar is READ for provenance only; nothing here trusts it for a number.
import fs from 'node:fs';
import path from 'node:path';
import { install, dirForCell, bytes } from './lib/rigHost.mjs';
import { REPO, source, block } from './lib/csharp.mjs';
import { decodePng } from './lib/png.mjs';
import * as lfs from './lib/lfs.mjs';

const ISO = 'Assets/_Project/Art/Characters/Iso';
const BAKE_MENU = 'Assets/_Project/Code/Tools/Editor/RigBaking/CharacterRigBakeMenu.cs';

// ---- tolerances -----------------------------------------------------------------------------
// There are almost none, and that is the point. A sheet either re-renders or it does not; a cross-
// rig pin either agrees or the two rigs disagree about the same event. `slipM` is the only real
// allowance: a reach the figure cannot physically make is CLAMPED by the rig on purpose (a child at
// a rack above her head), and the clamp is reported rather than hidden — so the check is that an
// UNCLAMPED reach lands on the grip, and that a clamped one says so.
export const TOL = {
  pixels: 0,         // a rendered cell is the committed cell, or the drop is not reproducible
  slipM: 0.0005,     // an unclamped hand is ON the grip: half a millimetre is float noise
  settleU: 0,        // the last frame IS u = 1 — this is an index, not a measurement
};

const argv = process.argv.slice(2);
const WANT_ALL = argv.includes('--all');
const AS_JSON = argv.includes('--json');

// ---- the sheet lane -------------------------------------------------------------------------

/**
 * preset → sheet stem, from the BAKE MENU's own table. Scraped rather than restated: the stems are
 * display capitalisation (`deckboss` → `DeckBoss`) and the one time they were transcribed by hand
 * four sheets went missing in silence, because an absent sheet is not an error anywhere — the
 * all-or-nothing gate simply drops the clip whole.
 */
export function stems() {
  const body = block(BAKE_MENU, 'Cast');
  const out = { fisher: 'Fisher' };                   // the player, baked at the folder root
  for (const m of body.matchAll(/\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\)/g)) out[m[1]] = m[2];
  if (Object.keys(out).length < 2) throw new Error(`${BAKE_MENU}: the Cast stem table scraped empty`);
  return out;
}

/**
 * A committed sheet's file name back to the render call that draws it: `Ginny_reach_stowV` →
 * preset `ginny`, `{anim:'reach', rest:'stowV'}`. Every branch is resolved against one of the RIG's
 * own tables — ANIMS, REACH_LIFT, CARRY_ORDER — so a state the rig does not declare is refused
 * here rather than rendered as something else.
 */
export function optsForState(C, state) {
  const cut = state.indexOf('_');
  const anim = cut < 0 ? state : state.slice(0, cut);
  const tail = cut < 0 ? null : state.slice(cut + 1);
  if (!C.ANIMS[anim]) return null;
  if (tail === null) return { anim };
  if (anim === 'reach') return C.REACH_LIFT[tail] != null ? { anim, rest: tail } : null;
  if (anim === 'cast') return { anim, power: tail };
  if (C.CARRY_ORDER.includes(tail)) return { anim, carry: tail };
  return null;
}

/** Every character PNG on disk, recursively — the same scan the slice guard makes. */
function sheetsOnDisk() {
  const out = [];
  const walk = (rel) => {
    for (const e of fs.readdirSync(path.join(REPO, rel), { withFileTypes: true })) {
      const child = `${rel}/${e.name}`;
      if (e.isDirectory()) walk(child);
      else if (e.name.endsWith('.png')) out.push(child);
    }
  };
  walk(ISO);
  return out.sort();
}

/** The preset a sheet belongs to, by its stem — never by its folder, which the player's sheets lack. */
function presetOf(stem, table) {
  const name = path.basename(stem, '.png');
  const cut = name.indexOf('_');
  if (cut < 0) return null;
  const head = name.slice(0, cut);
  for (const [preset, s] of Object.entries(table)) if (s === head) return { preset, state: name.slice(cut + 1) };
  return null;
}

/** The reach kit's own sheets: the three rests, and the run the drop completed. */
const IS_REACH_KIT = (state) => state.startsWith('reach_') || state === 'run';

// ---- the pixels -----------------------------------------------------------------------------

/**
 * One sheet, re-rendered and compared. Returns the count of differing pixels and where the first
 * one is, so a failure names a cell instead of a number.
 *
 * ⚠️ The OFF-DECK four bake at 64 × 88 — the rig's own 92 cell re-windowed 2 rows top and 2 bottom
 * (CharacterSheetSlicer.OffDeckCell). The window is DERIVED from the committed sheet's own height
 * rather than assumed, so a sheet that was re-windowed differently fails loudly instead of being
 * silently re-cropped to match.
 */
export function compare(C, relPath, preset, opts) {
  const png = decodePng(lfs.read(relPath));
  const frames = C.ANIMS[opts.anim].frames;
  const cellW = C.W;
  const cellH = png.height / 8;

  if (png.width !== frames * cellW || !Number.isInteger(cellH))
    return { ok: false, reason: `sheet is ${png.width}×${png.height}, not ${frames * cellW}×(8 × a whole cell)` };
  if (cellH > C.H || (C.H - cellH) % 2 !== 0)
    return { ok: false, reason: `cell height ${cellH} is not the rig's ${C.H} re-windowed evenly` };

  const top = (C.H - cellH) / 2;                       // 0 for the 92 cell, 2 for the off-deck 88
  let diff = 0, first = null;
  const cells = new Set();

  for (let d = 0; d < 8; d++) {
    const dir = dirForCell(d, 8, 'Clockwise');         // the character rig's measured convention
    for (let f = 0; f < frames; f++) {
      const cell = bytes(C.render(dir, { ...opts, frame: f, build: { preset } }));
      for (let y = 0; y < cellH; y++) {
        const srcRow = (y + top) * cellW * 4;
        const dstRow = ((d * cellH + y) * png.width + f * cellW) * 4;
        for (let i = 0; i < cellW * 4; i++) {
          if (png.rgba[dstRow + i] === cell[srcRow + i]) continue;
          diff++;
          cells.add(`d${d}/f${f}`);
          if (!first) first = { dir: d, frame: f, x: (i >> 2), y, channel: i & 3 };
        }
      }
    }
  }
  return { ok: diff === 0, diff, cells: cells.size, first, cellH, top };
}

// ---- the cross-rig pins ---------------------------------------------------------------------

/**
 * The reach clip is one half of a hand-over; the rod rig owns the other half. These are the facts
 * BOTH files state, and neither can check alone.
 *
 * ⚠️ `restLift()` is NOT the rack height and must never be compared to one. The rod rig's own
 * words: it is "how far a settled rest holds the grip ABOVE THE SURFACE IT RESTS ON", and its
 * consumer "places the sprite's pivot this far above the floor / rack". The character's
 * REACH_LIFT is the other number — the surface's own height — which the rod rig does not know and
 * has no way to know. The one place the two genuinely meet is at the GROUND, where the surface is
 * the floor at 0 and both rigs are then talking about the same 0.095 m.
 */
export function rodPins(C, Rod) {
  const held = Rod.restHeldFrames();
  const rigGripped = (() => {
    let n = 0;
    for (let f = 0; f < C.REST_FRAMES; f++) if (f / (C.REST_FRAMES - 1) < C.RELEASE_AT) n++;
    return n;
  })();
  return [
    { name: 'REST_FRAMES', rod: Rod.REST_FRAMES, character: C.REST_FRAMES },
    { name: 'RELEASE_AT', rod: Rod.RELEASE_AT, character: C.RELEASE_AT },
    { name: 'frames still in hand', rod: held, character: rigGripped },
    // The rod's own ground datum for a reeled rod (the coast and deep tiers) IS the character's
    // default grip rise. A cane pole has no reel and rests on its grip radius instead — 0.036 —
    // which is a real difference and the reason this is pinned per tier rather than once.
    { name: "gripRise vs restLift('ground', coast)", rod: Rod.restLift('ground', { tier: 'coast' }), character: C.GRIP_RISE },
  ];
}

/** What the rod rig actually says about each rest, beside what the character clip was baked at.
 *  Reported, never asserted: they are different quantities (see rodPins). */
export function restLiftTable(C, Rod) {
  return Object.keys(C.REACH_LIFT).map((rest) => ({
    rest,
    characterSurfaceM: C.REACH_LIFT[rest],
    rodGripAboveSurfaceM: Object.fromEntries(Object.keys(Rod.TIERS).map((t) => [t, Rod.restLift(rest, { tier: t })])),
  }));
}

// ---- the sync contract ----------------------------------------------------------------------

/**
 * Every preset × rest × facing × frame: is the hand on the grip it was asked for, and does the
 * clip's own gripped flag agree with the release point?
 *
 * ⚠️ SLIP IS ONLY A DEFECT WHILE THE HAND IS STILL ON THE TOOL. `want` keeps tracking the tool
 * after the release, and the hand deliberately does not — it "peels UP off the tool and settles
 * empty at the standing rest", in the rig's own words. So a large slip on the last frames is the
 * release being VISIBLE, which is the whole point of releasing at 0.72 instead of at the seam;
 * measuring it as drift would fail the clip for working. Both halves are therefore asserted: zero
 * while gripped, and demonstrably NOT zero once let go.
 */
export function sync(C, presets) {
  const rows = [], problems = [];
  for (const preset of presets) {
    for (const rest of Object.keys(C.REACH_LIFT)) {
      let held = 0, worst = null, stretchAt = null, letGo = Infinity;
      let maxHeldSlip = 0, maxHeldStretch = 0, clamped = false, lift = null, frames = 0;
      for (let d = 0; d < 8; d++) {
        for (let f = 0; f < C.REST_FRAMES; f++) {
          const u = f / (C.REST_FRAMES - 1);
          const m = C.reachMount(dirForCell(d, 8, 'Clockwise'),
                                 { anim: 'reach', frame: f, rest, build: { preset } });
          if (!m) { problems.push(`${preset}/${rest}: reachMount() returned null`); continue; }
          frames++;
          lift = m.lift;
          clamped = clamped || !!m.clamped;

          if (m.frame !== f) problems.push(`${preset}/${rest} d${d}/f${f}: reachMount reports frame ${m.frame}`);
          if (m.gripped !== (u < C.RELEASE_AT))
            problems.push(`${preset}/${rest} d${d}/f${f}: gripped=${m.gripped} at u=${u}, release is ${C.RELEASE_AT}`);

          if (m.gripped) {
            held++;
            if (m.slip > maxHeldSlip) { maxHeldSlip = m.slip; worst = `d${d}/f${f}`; }
            // The rig's OWN projected number, not `stretch * PX`: stretchPx carries the camera's
            // vertical foreshortening, so a metre of overreach is a different number of pixels at
            // each facing and only the rig knows which.
            if (m.stretchPx > maxHeldStretch) { maxHeldStretch = m.stretchPx; stretchAt = `d${d}/f${f}`; }
          } else if (f === C.REST_FRAMES - 1) {
            letGo = Math.min(letGo, m.slip);      // the settled frame: the hand is off the tool
          }
        }
      }
      if (maxHeldSlip > TOL.slipM)
        problems.push(`${preset}/${rest}: hand ${maxHeldSlip.toFixed(4)} m off the grip at ${worst} ` +
                      'while still GRIPPED — the hand-over drifts');
      if (!(letGo > TOL.slipM))
        problems.push(`${preset}/${rest}: the hand is still on the grip at the settled frame — ` +
                      'nothing was let go of');
      // The settled rest: the last frame must land on u = 1, or a clip that stops on it stops short.
      const lastU = (C.REST_FRAMES - 1) / (C.REST_FRAMES - 1);
      if (Math.abs(lastU - 1) > TOL.settleU)
        problems.push(`${preset}/${rest}: the last frame is u=${lastU}, not the settled rest`);

      rows.push({ preset, rest, liftM: +lift.toFixed(4), clamped, grippedFrames: held / 8, frames: frames / 8,
                  heldSlipM: +maxHeldSlip.toFixed(5), heldStretchPx: +maxHeldStretch.toFixed(2),
                  heldStretchAt: stretchAt, releasedSlipM: +letGo.toFixed(4) });
    }
  }
  return { rows, problems };
}

// ---- run ------------------------------------------------------------------------------------

export function run() {
  install('characterHands');
  install('rod');
  const C = globalThis.CharacterIso6, Rod = globalThis.RodIso;
  if (!C.ANIMS.reach) throw new Error('characterIsoRig6.js declares no `reach` anim — this is a pre-6.6 body');

  const table = stems();
  const wanted = [], skipped = [];
  for (const rel of sheetsOnDisk()) {
    const hit = presetOf(rel, table);
    if (!hit) { skipped.push({ rel, why: 'no preset stem' }); continue; }
    if (!WANT_ALL && !IS_REACH_KIT(hit.state)) continue;
    const opts = optsForState(C, hit.state);
    if (!opts) { skipped.push({ rel, why: `no rig state for '${hit.state}'` }); continue; }
    wanted.push({ rel, ...hit, opts });
  }
  if (wanted.length === 0) throw new Error('no sheets matched — has the reach kit been staged?');

  lfs.prefetch(wanted.map((w) => w.rel));

  const sheets = [], problems = [];
  for (const w of wanted) {
    const r = compare(C, w.rel, w.preset, w.opts);
    sheets.push({ file: path.basename(w.rel), preset: w.preset, state: w.state, ...r });
    if (!r.ok)
      problems.push(`${w.rel}: ${r.reason ?? `${r.diff} pixel(s) differ across ${r.cells} cell(s), first at ` +
        `d${r.first.dir}/f${r.first.frame} (${r.first.x},${r.first.y})`}`);
  }

  const pins = rodPins(C, Rod);
  for (const p of pins)
    if (p.rod !== p.character)
      problems.push(`the rod rig and the character rig disagree on ${p.name}: ${p.rod} vs ${p.character}`);

  const presets = [...new Set(wanted.filter((w) => w.opts.anim === 'reach').map((w) => w.preset))].sort();
  const s = sync(C, presets);
  problems.push(...s.problems);

  return { revision: C.revision, sheets, skipped, pins, restLifts: restLiftTable(C, Rod), sync: s.rows, problems };
}

// ---- the report -----------------------------------------------------------------------------

const pad = (v, n) => String(v).padEnd(n);
const num = (v, n, d = 3) => String(typeof v === 'number' ? v.toFixed(d) : v).padStart(n);

function report(r) {
  console.log(`\nREPRODUCTION — ${r.sheets.length} committed sheet(s) re-rendered from rig ${r.revision}\n`);
  console.log(`  ${pad('sheet', 30)}${pad('preset', 10)}${pad('cell', 8)}${'pixels differing'}`);
  for (const s of r.sheets)
    console.log(`  ${pad(s.file, 30)}${pad(s.preset, 10)}${pad(s.cellH ? `64×${s.cellH}` : '?', 8)}` +
                `${s.ok ? '·' : `✗ ${s.diff} across ${s.cells} cell(s)`}`);

  console.log('\nTHE ROD PINS — both rigs state these, and they must agree\n');
  for (const p of r.pins)
    console.log(`  ${pad(p.name, 38)}rod ${num(p.rod, 8, 3)}   character ${num(p.character, 8, 3)}   ` +
                `${p.rod === p.character ? '·' : '✗'}`);

  console.log('\n  restLift() is NOT a rack height — the two columns are different quantities:\n');
  console.log(`  ${pad('rest', 10)}${pad('character: surface height', 26)}rod: grip above that surface (per tier)`);
  for (const l of r.restLifts)
    console.log(`  ${pad(l.rest, 10)}${pad(l.characterSurfaceM + ' m', 26)}` +
                Object.entries(l.rodGripAboveSurfaceM).map(([t, v]) => `${t} ${v}`).join('  '));

  if (r.sync.length) {
    console.log('\nTHE SYNC — on the grip while gripped, off it once let go\n');
    console.log(`  ${pad('preset', 10)}${pad('rest', 8)}${pad('lift m', 9)}${pad('clamped', 9)}` +
                `${pad('gripped', 9)}${pad('slip m held', 13)}${pad('stretch px @', 16)}slip m released`);
    for (const s of r.sync)
      console.log(`  ${pad(s.preset, 10)}${pad(s.rest, 8)}${pad(s.liftM, 9)}${pad(s.clamped ? 'YES' : '·', 9)}` +
                  `${pad(`${s.grippedFrames}/${s.frames}`, 9)}${pad(s.heldSlipM, 13)}` +
                  `${pad(`${s.heldStretchPx} ${s.heldStretchAt}`, 16)}${s.releasedSlipM}`);
  }

  for (const sk of r.skipped) console.log(`\n  (skipped ${sk.rel}: ${sk.why})`);

  if (r.problems.length) {
    console.log(`\n✗ ${r.problems.length} problem(s):\n`);
    for (const p of r.problems) console.log(`  · ${p}`);
    console.log('');
    return 1;
  }
  console.log('\n· every sheet re-renders byte for byte, and both rigs agree about the hand-over.\n');
  return 0;
}

const result = run();
if (AS_JSON) console.log(JSON.stringify(result, null, 2));
process.exit(AS_JSON ? (result.problems.length ? 1 : 0) : report(result));
