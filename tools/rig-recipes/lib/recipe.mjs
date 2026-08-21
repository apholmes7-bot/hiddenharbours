// THE RECIPE — one shape, defined once, shared by every baker.
//
// See docs/art/rig-recipe-ledger.md for the field-by-field contract. This module owns three things:
// the deterministic serialisation (which must stay byte-identical to the C# writer's), the
// EXPANSION from the compact axis form to one call per cell, and the assembly of those cells into
// the sheet the recipe claims to reproduce.
import { install, dirForCell, bytes, rigSha256 } from './rigHost.mjs';
import { rigCatalog } from './csharp.mjs';

export const SCHEMA = 'hiddenharbours.rig-recipe/1';

// ---- serialisation ---------------------------------------------------------------------------

/**
 * The recipe's key order — FIXED, so a re-derived ledger diffs against the last one on content and
 * never on ordering. Any key not named here is a schema change, and the writer refuses it rather
 * than appending it somewhere arbitrary.
 */
const KEY_ORDER = {
  $root: ['schema', 'sheet', 'kit', 'baker', 'rig', 'call', 'grid', 'pack', 'sheetSha256'],
  rig: ['key', 'global', 'source', 'sha256', 'convention', 'prerequisites'],
  prerequisite: ['key', 'global', 'source', 'sha256'],
  call: ['fn', 'args', 'opts'],
  grid: ['columns', 'rows', 'order', 'axes'],
  axis: ['name', 'bind', 'values'],
  pack: ['rule', 'nativeW', 'nativeH', 'nativePivotX', 'nativePivotY',
         'cropX', 'cropY', 'cellW', 'cellH', 'pivotX', 'pivotY', 'sheetW', 'sheetH'],
};

function ordered(obj, order) {
  const out = {};
  for (const k of order) if (obj[k] !== undefined) out[k] = obj[k];
  const extra = Object.keys(obj).filter(k => !order.includes(k));
  if (extra.length) throw new Error(`recipe carries unknown key(s): ${extra.join(', ')}`);
  return out;
}

/** The recipe as it is written to disk. `JSON.stringify(_, null, 2)` + a trailing newline. */
export function stringify(recipe) {
  const r = ordered(recipe, KEY_ORDER.$root);
  r.rig = ordered(r.rig, KEY_ORDER.rig);
  r.rig.prerequisites = (r.rig.prerequisites ?? []).map(p => ordered(p, KEY_ORDER.prerequisite));
  r.call = ordered(r.call, KEY_ORDER.call);
  r.grid = ordered(r.grid, KEY_ORDER.grid);
  r.grid.axes = r.grid.axes.map(a => ordered(a, KEY_ORDER.axis));
  r.pack = ordered(r.pack, KEY_ORDER.pack);
  return JSON.stringify(r, null, 2) + '\n';
}

// ---- expansion -------------------------------------------------------------------------------

/**
 * One call per cell, in SHEET order.
 *
 * The axes are an ODOMETER and the FIRST axis is the fastest — that is the whole placement rule.
 * Cell index i = Σ (index_a × stride_a), and the cell lands at `col = i % columns`,
 * `row = ⌊i / columns⌋`. Every baker in the repo packs that way: the fuel kit's facings-then-fills
 * falls out of it, and so does the gas-station kit's single facing axis wrapping into rows.
 */
export function expand(recipe) {
  const { grid, call } = recipe;
  if (grid.order !== 'rowMajor') throw new Error(`unknown grid order '${grid.order}'`);

  const counts = grid.axes.map(a => a.values.length);
  const total = counts.reduce((a, b) => a * b, 1);
  if (total > grid.columns * grid.rows)
    throw new Error(`${total} cells will not fit a ${grid.columns}×${grid.rows} grid`);

  const cells = [];
  for (let i = 0; i < total; i++) {
    let rest = i, dir = null;
    const opts = { ...call.opts };
    const args = [...call.args];
    for (let a = 0; a < grid.axes.length; a++) {
      const axis = grid.axes[a];
      const value = axis.values[rest % counts[a]];
      rest = Math.floor(rest / counts[a]);
      if (axis.bind === 'dir') dir = value;
      else if (axis.bind.startsWith('opt:')) opts[axis.bind.slice(4)] = value;
      else if (axis.bind.startsWith('arg:')) args[Number(axis.bind.slice(4))] = value;
      else throw new Error(`unknown axis bind '${axis.bind}'`);
    }
    cells.push({
      index: i,
      col: i % grid.columns,
      row: Math.floor(i / grid.columns),
      args: args.map(a => (a === '$dir' ? dir : a === '$opts' ? opts : a)),
    });
  }
  return cells;
}

// ---- rendering -------------------------------------------------------------------------------

/** Installs the recipe's rig (and prerequisites) and returns the callable `fn`. */
export function bind(recipe, catalog = rigCatalog()) {
  install(recipe.rig.key, catalog);
  const g = globalThis[recipe.rig.global];
  if (!g) throw new Error(`rig global ${recipe.rig.global} is not installed`);
  const fn = g[recipe.call.fn];
  if (typeof fn !== 'function')
    throw new Error(`${recipe.rig.global}.${recipe.call.fn} is not a function`);
  return (args) => bytes(fn.apply(g, args));
}

/**
 * Renders every cell and blits it into the sheet at the recorded crop.
 *
 * ⚠️ The verifier deliberately does NOT re-derive the crop. `pack` records the rect the BAKE
 * measured; using it makes this a reassembler rather than a second implementation of the crop rule,
 * so a recipe whose crop is wrong FAILS the compare instead of quietly agreeing with itself.
 */
export function render(recipe, catalog = rigCatalog()) {
  const call = bind(recipe, catalog);
  const p = recipe.pack;
  const sheet = Buffer.alloc(p.sheetW * p.sheetH * 4);

  for (const cell of expand(recipe)) {
    const src = call(cell.args);
    if (src.length !== p.nativeW * p.nativeH * 4)
      throw new Error(`cell ${cell.index} came back ${src.length} bytes, expected ` +
                      `${p.nativeW * p.nativeH * 4} for a ${p.nativeW}×${p.nativeH} RGBA buffer`);
    const x0 = cell.col * p.cellW, y0 = cell.row * p.cellH;
    for (let y = 0; y < p.cellH; y++) {
      const sy = p.cropY + y;
      if (sy < 0 || sy >= p.nativeH) continue;
      for (let x = 0; x < p.cellW; x++) {
        const sx = p.cropX + x;
        if (sx < 0 || sx >= p.nativeW) continue;
        const s = (sy * p.nativeW + sx) * 4;
        const d = ((y0 + y) * p.sheetW + x0 + x) * 4;
        sheet[d] = src[s]; sheet[d + 1] = src[s + 1]; sheet[d + 2] = src[s + 2]; sheet[d + 3] = src[s + 3];
      }
    }
  }
  return sheet;
}

// ---- measuring (the BACKFILL half only) -------------------------------------------------------

/** The alpha bounding box of one RGBA buffer, or `null` if it drew nothing. */
export function alphaBounds(rgba, w, h) {
  let x0 = w, y0 = h, x1 = -1, y1 = -1;
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (rgba[(y * w + x) * 4 + 3] !== 0) {
        if (x < x0) x0 = x; if (x > x1) x1 = x;
        if (y < y0) y0 = y; if (y > y1) y1 = y;
      }
    }
  }
  return x1 < x0 ? null : [x0, y0, x1, y1];
}

/**
 * THE CELL RULE — `IsoPropSheetBaker.MeasureCell`, transcribed: the pivot-INCLUSIVE union of the ink
 * bbox across every cell, SEEDED AT THE PIVOT. The seeding is the rule, not a detail: a wall-hung
 * piece whose ink stops above its own pivot would otherwise crop its ground contact outside its own
 * cell, and that does not fail loudly — it just stands in the wrong place.
 *
 * Used only to WRITE a backfilled recipe. Verification never calls it (see `render`).
 */
export function measureCell(cells, w, h, pivotX, pivotY) {
  let left = 0, top = 0, right = 0, bottom = 0, anyInk = false, pivotInsideInk = false;
  for (const cell of cells) {
    const b = alphaBounds(cell, w, h);
    if (!b) continue;
    const [x0, y0, x1, y1] = b;
    anyInk = true;
    left = Math.min(left, x0 - pivotX); right = Math.max(right, x1 - pivotX);
    top = Math.min(top, y0 - pivotY); bottom = Math.max(bottom, y1 - pivotY);
    if (pivotX >= x0 && pivotX <= x1 && pivotY >= y0 && pivotY <= y1) pivotInsideInk = true;
  }
  if (!anyInk) return null;
  return {
    cellW: right - left + 1, cellH: bottom - top + 1,
    pivotX: -left, pivotY: -top,
    cropX: pivotX + left, cropY: pivotY + top,
    pivotInsideInk,
  };
}

export { dirForCell, rigSha256 };
