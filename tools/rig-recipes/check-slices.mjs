#!/usr/bin/env node
// A THIRD check on the ledger, independent of both the rigs and the sheet pixels.
//
//   node tools/rig-recipes/check-slices.mjs
//   node tools/rig-recipes/check-slices.mjs --verbose Assets/_Project/Art/Sprites/Yard
//
// `verify-ledger.mjs` proves a recipe by rendering it and comparing pixels — the real proof, but it
// needs the rigs and it needs the sheet's LFS bytes. This one needs NEITHER. It reads the committed
// `<stem>.png.meta`, which carries the SLICER's own view of the sheet — one sprite rect per cell,
// plus the normalised pivot — and holds the recipe to it:
//
//   · one rect per cell the recipe's axes produce;
//   · every rect is exactly `pack.cellW × pack.cellH`;
//   · the rects span exactly `pack.sheetW × pack.sheetH`;
//   · the normalised pivot is ADR 0026's `(pivotX/cellW, (cellH − pivotY)/cellH)` of the recipe's.
//
// Two independently-committed artefacts describing one sheet, made to agree. It cannot tell you the
// PIXELS are right — only the pixel compare does that — but it runs anywhere, and a recipe that
// disagrees with the slicer beside it is wrong whatever the pixels say.
import fs from 'node:fs';
import path from 'node:path';
import { REPO } from './lib/csharp.mjs';
import { SCHEMA } from './lib/recipe.mjs';
import { findRecipes } from './verify-ledger.mjs';

const SEARCH_ROOTS = ['Assets/_Project/Art'];

/** ADR 0026: a rig pivot is a CONTINUOUS coordinate from the cell's top-left, so the y term is
 *  `(H − pivotY)/H` and NOT `(H − 1 − pivotY)/H`. Getting it upside-down is easy and silent. */
const normalised = (pivotX, pivotY, w, h) => [pivotX / w, (h - pivotY) / h];

/** The `sprites:` block of a TextureImporter meta — name, rect and pivot per slice. */
export function slicesOf(metaPath) {
  const text = fs.readFileSync(path.join(REPO, metaPath), 'utf8');
  const at = text.indexOf('\n    sprites:');
  if (at < 0) return null;                        // not sliced (or sliced as a single sprite)

  const out = [];
  const re = /name:\s*(\S+)\s*\n\s*rect:\s*\n(?:\s*serializedVersion:.*\n)?\s*x:\s*(-?\d+)\s*\n\s*y:\s*(-?\d+)\s*\n\s*width:\s*(\d+)\s*\n\s*height:\s*(\d+)[\s\S]*?pivot:\s*\{x:\s*([-\d.eE]+),\s*y:\s*([-\d.eE]+)\}/g;
  let m;
  while ((m = re.exec(text.slice(at)))) {
    out.push({ name: m[1], x: +m[2], y: +m[3], w: +m[4], h: +m[5],
               pivotX: +m[6], pivotY: +m[7] });
  }
  return out.length ? out : null;
}

export function checkSlices(recipePath) {
  const r = JSON.parse(fs.readFileSync(path.join(REPO, recipePath), 'utf8'));
  if (r.schema !== SCHEMA) return { ok: false, why: `schema is '${r.schema}'` };

  const metaPath = `${path.dirname(recipePath)}/${r.sheet}.meta`;
  if (!fs.existsSync(path.join(REPO, metaPath))) return { skipped: 'no import meta beside the sheet' };
  const slices = slicesOf(metaPath);
  if (!slices) return { skipped: 'the sheet is not sliced into sprites' };

  const p = r.pack;
  const cells = r.grid.axes.reduce((n, a) => n * a.values.length, 1);
  const fail = [];

  if (slices.length !== cells)
    fail.push(`the slicer cut ${slices.length} sprite(s); the recipe's axes make ${cells} cell(s)`);

  const odd = slices.filter(s => s.w !== p.cellW || s.h !== p.cellH);
  if (odd.length)
    fail.push(`${odd.length} rect(s) are not ${p.cellW}×${p.cellH} (e.g. ${odd[0].name} is ${odd[0].w}×${odd[0].h})`);

  const spanW = Math.max(...slices.map(s => s.x + s.w));
  const spanH = Math.max(...slices.map(s => s.y + s.h));
  if (spanW !== p.sheetW || spanH !== p.sheetH)
    fail.push(`the rects span ${spanW}×${spanH}; the recipe packs ${p.sheetW}×${p.sheetH}`);

  // The pivot is stored to float precision, so compare within a rounding tolerance rather than
  // exactly — but a tolerance loose enough to hide a whole pixel would hide the bug this catches.
  const [wantX, wantY] = normalised(p.pivotX, p.pivotY, p.cellW, p.cellH);
  const tol = 0.5 / Math.max(p.cellW, p.cellH);
  const badPivot = slices.filter(s => Math.abs(s.pivotX - wantX) > tol || Math.abs(s.pivotY - wantY) > tol);
  if (badPivot.length)
    fail.push(`${badPivot.length} sprite pivot(s) disagree: ${badPivot[0].name} is ` +
              `(${badPivot[0].pivotX}, ${badPivot[0].pivotY}), the recipe's (${p.pivotX},${p.pivotY}) ` +
              `normalises to (${wantX.toFixed(6)}, ${wantY.toFixed(6)})`);

  return fail.length ? { ok: false, why: fail.join('; ') } : { ok: true, cells: slices.length };
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const roots = process.argv.slice(2).filter(a => !a.startsWith('--'));
  const verbose = process.argv.includes('--verbose');
  const recipes = (roots.length ? roots : SEARCH_ROOTS).flatMap(findRecipes);

  const byKit = new Map();
  for (const rel of recipes) {
    const kit = JSON.parse(fs.readFileSync(path.join(REPO, rel), 'utf8')).kit ?? '(unknown)';
    let v;
    try { v = checkSlices(rel); } catch (e) { v = { ok: false, why: e.message }; }
    if (!byKit.has(kit)) byKit.set(kit, { pass: 0, skip: [], fail: [] });
    const b = byKit.get(kit);
    if (v.skipped) b.skip.push(`${rel}: ${v.skipped}`);
    else if (v.ok) b.pass++;
    else b.fail.push(`${rel}: ${v.why}`);
    if (verbose) console.log(`${v.skipped ? '–' : v.ok ? '✓' : '✗'} ${rel}${v.ok ? '' : ' — ' + (v.why ?? v.skipped)}`);
  }

  let bad = 0;
  for (const [kit, b] of [...byKit].sort()) {
    const total = b.pass + b.skip.length + b.fail.length;
    const skipped = b.skip.length ? `, ${b.skip.length} not sliced` : '';
    console.log(`${b.fail.length ? '❌' : '✅'} ${kit}: ${b.pass}/${total} recipes agree with the ` +
                `slicer's own rects${skipped}`);
    for (const f of b.fail.slice(0, 8)) console.log(`     ✗ ${f}`);
    if (b.fail.length > 8) console.log(`     … and ${b.fail.length - 8} more`);
    bad += b.fail.length;
  }
  console.log(`\n${recipes.length - bad} of ${recipes.length} recipes agree with their committed ` +
              'import meta. (This needs no rig and no LFS object — it is not the pixel proof.)');
  if (bad) process.exitCode = 1;
}
