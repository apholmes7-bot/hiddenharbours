#!/usr/bin/env node
// THE RECIPE LEDGER — derive, verify and (optionally) write `<stem>.recipe.json` beside every sheet
// a rig baked.
//
//   node tools/rig-recipes/bake-ledger.mjs                       # verify every kit, write nothing
//   node tools/rig-recipes/bake-ledger.mjs --write               # write the kits that reproduce
//   node tools/rig-recipes/bake-ledger.mjs --kit camper --verbose
//
// ⭐ THE RULE THAT MAKES THIS LEDGER TRUE RATHER THAN PLAUSIBLE: a recipe is written only when
// running it back through the rigs reproduces the COMMITTED sheet byte for byte. A kit where one
// sheet disagrees is refused WHOLE and named in the report — a ledger that is right about 83 of 84
// sheets is worse than none, because nothing downstream can tell which one is the lie.
//
// Nothing here rebakes. No PNG is written, opened for writing, or touched in any way: the prop-mesh
// bake is not byte-deterministic and a rebake would dirty sheets this lane never looked at.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { REPO, rigCatalog } from './lib/csharp.mjs';
import { decodePng } from './lib/png.mjs';
import * as lfs from './lib/lfs.mjs';
import { install, bytes } from './lib/rigHost.mjs';
import { SCHEMA, stringify, expand, measureCell } from './lib/recipe.mjs';

/** Every kit in the ledger. A kit is here once its plans reproduce its committed sheets. */
export const KITS = ['fuel-storage', 'gas-station', 'camper', 'yard', 'iso-prop', 'nav-buoy'];

const catalog = rigCatalog();

// ---- helpers -----------------------------------------------------------------------------------

/** `Mathf.RoundToInt` — .NET's MidpointRounding.ToEven, NOT JavaScript's round-half-up. */
export function roundToInt(v) {
  const f = Math.floor(v), d = v - f;
  if (d > 0.5) return f + 1;
  if (d < 0.5) return f;
  return f % 2 === 0 ? f : f + 1;
}

/**
 * The rig block: WHICH BYTES drew this sheet. The scene-editor review's headline finding was that
 * nothing in an export recorded that; a recipe that cannot be traced to a rig source is a guess.
 */
function rigBlock(rigKey, rigSha256) {
  const entry = catalog[rigKey];
  const seen = new Set([rigKey]);
  const prerequisites = [];
  (function walk(key) {
    for (const dep of catalog[key].prerequisites) {
      if (seen.has(dep)) continue;
      seen.add(dep);
      walk(dep);
      prerequisites.push({ key: dep, global: catalog[dep].global, source: catalog[dep].source,
                           sha256: rigSha256(catalog[dep].source) });
    }
  })(rigKey);
  return { key: rigKey, global: entry.global, source: entry.source, sha256: rigSha256(entry.source),
           convention: entry.convention, prerequisites };
}

const substitute = (args, opts, dir) =>
  args.map(a => (a === '$opts' ? opts : a === '$dir' ? dir : a));

/** The rig's own cell geometry — from `cell(...)` where a kit has one, from the W/H/pivot triple
 *  otherwise. Never restated by a kit module (ADR 0021 §4). */
function geometry(g, plan) {
  // ⚠️ How a fractional pivot becomes an integer is the BAKER's choice and the two in this repo
  // differ: most round (Mathf.RoundToInt, half-to-EVEN), the nav-buoy baker casts (truncates).
  // Half a pixel of pivot moves the crop rect by a row, so the plan states which, never this file.
  const round = plan.pivotRound === 'trunc' ? Math.trunc : roundToInt;
  if (!plan.cellCall) {
    return { nativeW: g.W | 0, nativeH: g.H | 0,
             nativePivotX: round(g.pivot.x), nativePivotY: round(g.pivot.y) };
  }
  const fn = g[plan.cellCall.fn];
  if (typeof fn !== 'function') throw new Error(`rig has no ${plan.cellCall.fn}()`);
  const c = fn.apply(g, substitute(plan.cellCall.args, plan.call.opts, null));
  return { nativeW: c.W | 0, nativeH: c.H | 0,
           nativePivotX: round(c.cx), nativePivotY: round(c.cy) };
}

/** Renders a plan's cells for a given axis set, in odometer order, at native size. */
function renderCells(g, plan, geo, axes = plan.axes) {
  const skeleton = {
    schema: SCHEMA, sheet: `${plan.stem}.png`, kit: '', baker: '',
    rig: { key: plan.rigKey, global: '', source: '', sha256: '', convention: '', prerequisites: [] },
    call: plan.call,
    grid: { columns: 1, rows: axes.reduce((n, a) => n * a.values.length, 1),
            order: 'rowMajor', axes },
    pack: {}, sheetSha256: '',
  };
  const fn = g[plan.call.fn];
  if (typeof fn !== 'function') throw new Error(`rig has no ${plan.call.fn}()`);
  return expand(skeleton).map((c, i) => {
    const rgba = bytes(fn.apply(g, c.args));
    if (rgba.length !== geo.nativeW * geo.nativeH * 4)
      throw new Error(`cell ${i} came back ${rgba.length} bytes, expected ` +
                      `${geo.nativeW * geo.nativeH * 4} for a ${geo.nativeW}×${geo.nativeH} buffer`);
    return rgba;
  });
}

/** Blits the rendered cells into the sheet at the RECORDED crop — the verifier's own path. */
function assemble(recipe, cells) {
  const p = recipe.pack;
  const sheet = Buffer.alloc(p.sheetW * p.sheetH * 4);
  for (const c of expand(recipe)) {
    const src = cells[c.index];
    for (let y = 0; y < p.cellH; y++) {
      const sy = p.cropY + y;
      if (sy < 0 || sy >= p.nativeH) continue;
      for (let x = 0; x < p.cellW; x++) {
        const sx = p.cropX + x;
        if (sx < 0 || sx >= p.nativeW) continue;
        const s = (sy * p.nativeW + sx) * 4;
        const d = ((c.row * p.cellH + y) * p.sheetW + c.col * p.cellW + x) * 4;
        sheet[d] = src[s]; sheet[d + 1] = src[s + 1]; sheet[d + 2] = src[s + 2]; sheet[d + 3] = src[s + 3];
      }
    }
  }
  return sheet;
}

function compare(assetPath, sheet, pack) {
  const png = decodePng(lfs.read(assetPath));
  if (png.width !== pack.sheetW || png.height !== pack.sheetH)
    return { ok: false, why: `sheet is ${png.width}×${png.height}, recipe renders ${pack.sheetW}×${pack.sheetH}` };
  let diff = 0;
  for (let i = 0; i < sheet.length; i++) if (sheet[i] !== png.rgba[i]) diff++;
  return diff === 0 ? { ok: true }
    : { ok: false, why: `${diff} of ${sheet.length} RGBA bytes differ (${(100 * diff / sheet.length).toFixed(3)}%)` };
}

/** Unity's .meta for a TextAsset, GUID derived from the asset path so a re-derived ledger never
 *  renumbers a file that has not changed. */
function metaFor(assetPath) {
  const guid = crypto.createHash('md5').update(`hiddenharbours.rig-recipe:${assetPath}`).digest('hex');
  return `fileFormatVersion: 2\nguid: ${guid}\nTextScriptImporter:\n  externalObjects: {}\n` +
         `  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
}

// ---- one kit -----------------------------------------------------------------------------------

export async function runKit(id, { write = false, verbose = false } = {}) {
  const kit = (await import(`./kits/${id}.mjs`)).default;
  const { rigSha256 } = await import('./lib/rigHost.mjs');

  const rig = (key) => { install(key, catalog); return globalThis[catalog[key].global]; };
  const plans = kit.plans({ catalog, rig, roundToInt });

  const sheetOf = (p) => `${p.sheetFolder ?? kit.outputFolder}/${p.stem}.png`;
  lfs.prefetch(plans.map(sheetOf));

  // ---- crop groups: several sheets can SHARE one crop --------------------------------------
  // The camper measures one cell per VARIANT across both of its roles, so its rest and enter sheets
  // slice into identical rects and the awning does not shift the pivot between them. Measuring each
  // sheet alone would give two different crops and neither would match what shipped.
  const groups = new Map();
  for (const p of plans) {
    const key = p.cropGroup ?? sheetOf(p);
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(p);
  }

  const results = [];
  for (const [, members] of groups) {
    let rendered;
    try {
      rendered = members.map(plan => {
        const g = rig(plan.rigKey);
        const geo = geometry(g, plan);
        // `cropAxes` is the axis set the CROP was measured over, when it is wider than the sheet's
        // own. The nav-buoy kit unions its cell over all three wear states and ships only one, so a
        // per-sheet ink union would give a tighter rect than what shipped — and a placed mark would
        // hop between wear states, silently.
        return { plan, geo,
                 cells: renderCells(g, plan, geo),
                 cropCells: plan.cropAxes ? renderCells(g, plan, geo, plan.cropAxes) : null };
      });
      const geo = rendered[0].geo;
      for (const r of rendered)
        if (r.geo.nativeW !== geo.nativeW || r.geo.nativeH !== geo.nativeH ||
            r.geo.nativePivotX !== geo.nativePivotX || r.geo.nativePivotY !== geo.nativePivotY)
          throw new Error(`crop group members disagree on the native cell (${r.plan.stem})`);

      const m = measureCell(rendered.flatMap(r => r.cropCells ?? r.cells), geo.nativeW, geo.nativeH,
                            geo.nativePivotX, geo.nativePivotY);
      if (!m) throw new Error('every cell rendered fully transparent');

      for (const { plan, cells } of rendered) {
        const assetPath = sheetOf(plan);
        const columns = typeof plan.columns === 'function' ? plan.columns(m.cellW, m.cellH) : plan.columns;
        if (!columns) throw new Error('no column count packs this cell inside the import cap');
        const rows = Math.ceil(cells.length / columns);
        const recipe = {
          schema: SCHEMA, sheet: `${plan.stem}.png`, kit: kit.id, baker: kit.baker,
          rig: rigBlock(plan.rigKey, rigSha256),
          call: plan.call,
          grid: { columns, rows, order: 'rowMajor', axes: plan.axes },
          pack: {
            rule: plan.packRule, ...geo,
            cropX: m.cropX, cropY: m.cropY,
            cellW: m.cellW, cellH: m.cellH, pivotX: m.pivotX, pivotY: m.pivotY,
            sheetW: columns * m.cellW, sheetH: rows * m.cellH,
          },
          sheetSha256: fs.existsSync(path.join(REPO, assetPath)) ? lfs.contentSha256(assetPath) : '',
        };
        if (!fs.existsSync(path.join(REPO, assetPath))) {
          results.push({ stem: plan.stem, ok: false, why: `no committed sheet at ${assetPath}` });
          continue;
        }
        const verdict = compare(assetPath, assemble(recipe, cells), recipe.pack);
        results.push({ stem: plan.stem, assetPath, recipe, ...verdict });
        if (verbose) console.log(`  ${verdict.ok ? '✓' : '✗'} ${plan.stem}${verdict.ok ? '' : ' — ' + verdict.why}`);
      }
    } catch (e) {
      for (const plan of members) {
        results.push({ stem: plan.stem, ok: false, why: e.message });
        if (verbose) console.log(`  ✗ ${plan.stem} — ${e.message}`);
      }
    }
  }

  const failed = results.filter(r => !r.ok);
  const ok = failed.length === 0;
  console.log(`${ok ? '✅' : '❌'} ${id}: ${results.length - failed.length}/${results.length} ` +
              'sheets reproduce byte-for-byte');
  for (const f of failed.slice(0, 8)) console.log(`     ✗ ${f.stem} — ${f.why}`);
  if (failed.length > 8) console.log(`     … and ${failed.length - 8} more`);

  if (write && ok) {
    for (const r of results) {
      const out = path.join(REPO, r.assetPath.replace(/\.png$/, '.recipe.json'));
      fs.writeFileSync(out, stringify(r.recipe));
      fs.writeFileSync(`${out}.meta`, metaFor(r.assetPath.replace(/\.png$/, '.recipe.json')));
    }
    const folders = [...new Set(results.map(r => path.posix.dirname(r.assetPath)))];
    console.log(`     wrote ${results.length} recipes to ${folders.join(', ')}`);
  } else if (write) {
    console.log('     REFUSED to write: the ledger is committed per kit, and this one does not reproduce whole');
  }

  return { id, total: results.length, failed: failed.length, ok, results };
}

// ---- cli ---------------------------------------------------------------------------------------

if (import.meta.url === `file://${process.argv[1]}`) {
  const argv = process.argv.slice(2);
  const write = argv.includes('--write');
  const verbose = argv.includes('--verbose');
  const only = argv.includes('--kit') ? argv[argv.indexOf('--kit') + 1] : null;

  const report = [];
  for (const id of KITS) {
    if (only && only !== id) continue;
    report.push(await runKit(id, { write, verbose }));
  }
  if (!report.length) { console.error(`no such kit '${only}'. Known: ${KITS.join(', ')}`); process.exit(2); }

  const bad = report.filter(r => !r.ok);
  console.log(`\n${report.reduce((n, r) => n + r.total - r.failed, 0)} of ` +
              `${report.reduce((n, r) => n + r.total, 0)} sheets reproduce across ${report.length} kit(s)`);
  if (bad.length) {
    console.log(`${bad.length} kit(s) refused: ${bad.map(r => r.id).join(', ')}`);
    process.exitCode = 1;
  }
}
