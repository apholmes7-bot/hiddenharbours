#!/usr/bin/env node
// ⭐ THE PROOF. Reads the COMMITTED `<stem>.recipe.json` files — nothing else — runs the rigs they
// name in a standalone V8 (node, no Unity, no ClearScript), reassembles the sheet each one claims to
// produce, and byte-compares it against the committed PNG.
//
//   node tools/rig-recipes/verify-ledger.mjs
//   node tools/rig-recipes/verify-ledger.mjs Assets/_Project/Art/Sprites/FuelStorage/Iso
//
// A recipe that reproduces its sheet IS the recipe. One that does not is a lie about how the art was
// made, and this exits non-zero on the first kind of lie it finds.
//
// ⚠️ What is compared is RGBA PIXEL BYTES, not PNG container bytes. Unity's `EncodeToPNG` produced
// the committed containers and nothing outside Unity reproduces its zlib window and filter choices —
// nor should it, because what a recipe has to reproduce is the IMAGE.
import fs from 'node:fs';
import path from 'node:path';
import { REPO, rigCatalog } from './lib/csharp.mjs';
import { decodePng } from './lib/png.mjs';
import * as lfs from './lib/lfs.mjs';
import { SCHEMA, render, stringify } from './lib/recipe.mjs';
import { rigSha256 } from './lib/rigHost.mjs';

const SEARCH_ROOTS = ['Assets/_Project/Art'];

export function findRecipes(root) {
  const out = [];
  (function walk(dir) {
    for (const e of fs.readdirSync(path.join(REPO, dir), { withFileTypes: true })) {
      const rel = `${dir}/${e.name}`;
      if (e.isDirectory()) walk(rel);
      else if (e.name.endsWith('.recipe.json')) out.push(rel);
    }
  })(root);
  return out.sort();
}

/** The catalog's transitive prerequisite closure for a rig key, in load order. */
function closure(catalog, key, seen = new Set([key]), out = []) {
  for (const dep of catalog[key].prerequisites) {
    if (seen.has(dep)) continue;
    seen.add(dep);
    closure(catalog, dep, seen, out);
    out.push(dep);
  }
  return out;
}

/** Everything a recipe promises that is checkable without rendering. */
function audit(recipePath, recipe, catalog) {
  const fail = [];
  if (recipe.schema !== SCHEMA) fail.push(`schema is '${recipe.schema}', expected '${SCHEMA}'`);

  // The file on disk must be exactly what the writer would emit — one shape, one serialisation, so
  // a hand-edited recipe cannot drift from what a re-bake would write.
  const canonical = stringify(JSON.parse(JSON.stringify(recipe)));
  if (canonical !== fs.readFileSync(path.join(REPO, recipePath), 'utf8'))
    fail.push('the file is not in the ledger\'s canonical serialisation (key order or formatting)');

  // The rig bytes are PINNED. A rig edited since the bake cannot be trusted to reproduce it, and
  // saying so is the whole reason the hash is in the file.
  // ⚠️ The prerequisite list is audited against the CATALOG, not left to the render to expose. A
  // missing prerequisite does not throw once another kit in the same process has already installed
  // that global — the baker's own install is idempotent for exactly the same reason — so a recipe
  // that under-declares its dependencies would render correctly here and wrongly in a fresh host.
  if (catalog?.[recipe.rig.key]) {
    const want = closure(catalog, recipe.rig.key).join(' → ');
    const got = recipe.rig.prerequisites.map(p => p.key).join(' → ');
    if (want !== got) fail.push(`prerequisites are [${got}], the catalog's closure is [${want}]`);
    if (catalog[recipe.rig.key].source !== recipe.rig.source)
      fail.push(`the catalog now loads '${recipe.rig.key}' from ${catalog[recipe.rig.key].source}`);
  }

  const rigs = [recipe.rig, ...recipe.rig.prerequisites];
  for (const r of rigs) {
    if (!fs.existsSync(path.join(REPO, r.source))) { fail.push(`rig source missing: ${r.source}`); continue; }
    const got = rigSha256(r.source);
    if (got !== r.sha256) fail.push(`${r.source} now hashes ${got.slice(0, 12)}…, recipe pins ${r.sha256.slice(0, 12)}…`);
  }

  const sheetPath = `${path.dirname(recipePath)}/${recipe.sheet}`;
  if (!fs.existsSync(path.join(REPO, sheetPath))) fail.push(`no sheet at ${sheetPath}`);
  else if (lfs.contentSha256(sheetPath) !== recipe.sheetSha256)
    fail.push(`${recipe.sheet} has changed since this recipe was written`);

  return { sheetPath, fail };
}

export function verify(recipePath, catalog = rigCatalog()) {
  const recipe = JSON.parse(fs.readFileSync(path.join(REPO, recipePath), 'utf8'));
  const { sheetPath, fail } = audit(recipePath, recipe, catalog);
  if (fail.length) return { ok: false, why: fail.join('; '), sheetPath };

  const sheet = render(recipe, catalog);
  const png = decodePng(lfs.read(sheetPath));
  if (png.width !== recipe.pack.sheetW || png.height !== recipe.pack.sheetH)
    return { ok: false, sheetPath,
             why: `sheet is ${png.width}×${png.height}, recipe renders ` +
                  `${recipe.pack.sheetW}×${recipe.pack.sheetH}` };

  let diff = 0;
  for (let i = 0; i < sheet.length; i++) if (sheet[i] !== png.rgba[i]) diff++;
  return diff === 0 ? { ok: true, sheetPath }
    : { ok: false, sheetPath,
        why: `${diff} of ${sheet.length} RGBA bytes differ (${(100 * diff / sheet.length).toFixed(3)}%)` };
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const roots = process.argv.slice(2).filter(a => !a.startsWith('--'));
  const verbose = process.argv.includes('--verbose');
  const recipes = (roots.length ? roots : SEARCH_ROOTS).flatMap(findRecipes);

  if (!recipes.length) { console.log('no *.recipe.json found'); process.exit(0); }
  lfs.prefetch(recipes.map(r => r.replace(/\.recipe\.json$/, '.png')));

  const catalog = rigCatalog();
  const byKit = new Map();
  for (const rel of recipes) {
    const kit = JSON.parse(fs.readFileSync(path.join(REPO, rel), 'utf8')).kit ?? '(unknown)';
    let v;
    try { v = verify(rel, catalog); } catch (e) { v = { ok: false, why: e.message }; }
    if (!byKit.has(kit)) byKit.set(kit, { pass: 0, fail: [] });
    const b = byKit.get(kit);
    if (v.ok) b.pass++; else b.fail.push(`${rel}: ${v.why}`);
    if (verbose) console.log(`${v.ok ? '✓' : '✗'} ${rel}${v.ok ? '' : ' — ' + v.why}`);
  }

  let bad = 0;
  for (const [kit, b] of [...byKit].sort()) {
    const total = b.pass + b.fail.length;
    console.log(`${b.fail.length ? '❌' : '✅'} ${kit}: ${b.pass}/${total} recipes reproduce their sheet byte-for-byte`);
    for (const f of b.fail.slice(0, 8)) console.log(`     ✗ ${f}`);
    if (b.fail.length > 8) console.log(`     … and ${b.fail.length - 8} more`);
    bad += b.fail.length;
  }
  console.log(`\n${recipes.length - bad} of ${recipes.length} committed recipes reproduce their sheet.`);
  if (bad) process.exitCode = 1;
}
