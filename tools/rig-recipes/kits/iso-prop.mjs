// The fixed-sheet ISO prop families — wharfDecor (61 pieces of wharf dressing), utilityIso (42
// village services) and the deck-loop kit's five — all baked by ONE parameterised baker because they
// share a buffer shape and a cell rule.
//
// What they do NOT share is the shape of the render() CALL, and that is exactly what
// `IsoPropSheetBaker.Shapes` records: three call shapes across seven families. Guessing it does not
// throw — it renders the rig DEFAULT with no error at all — so it is scraped from the baker, never
// inferred from a family name.
import fs from 'node:fs';
import path from 'node:path';
import { REPO, source, block, cite, citeLine } from '../lib/csharp.mjs';
import { dirForCell } from '../lib/rigHost.mjs';

const BAKER = 'Assets/_Project/Code/Tools/Editor/RigBaking/IsoPropSheetBaker.cs';
const CONTRACT = 'Assets/_Project/Code/Tools/Editor/RigBaking/IsoPackContract.cs';

/** `IsoPropSheetBaker.Shapes` — which families this baker serves, and how each is called. */
function shapes() {
  const body = block(BAKER, 'Shapes');
  const out = {};
  const re = /\[\s*"([^"]+)"\s*\]\s*=\s*new RenderShape\(([^)]*)\)/g;
  let m;
  while ((m = re.exec(body))) {
    const args = m[2];
    out[m[1]] = {
      takesKey: !/takesKey:\s*false/.test(args),
      takesDir: !/takesDir:\s*false/.test(args),
    };
  }
  if (!Object.keys(out).length) throw new Error('IsoPropSheetBaker.Shapes scraped empty');
  return out;
}

/** `IsoPackContract.Registry` — where each family's contract and sheets live. */
function registry() {
  const src = source(CONTRACT);
  const consts = {};
  for (const m of src.matchAll(/const string (\w+)\s*=\s*"([^"]+)";/g)) consts[m[1]] = m[2];
  const literal = (tok) => {
    const t = tok.trim();
    if (/^"/.test(t)) return t.slice(1, -1);
    const sum = /^(\w+)\s*\+\s*"([^"]*)"$/.exec(t);
    if (sum) return (consts[sum[1]] ?? (() => { throw new Error(`unknown const ${sum[1]}`); })()) + sum[2];
    if (consts[t]) return consts[t];
    throw new Error(`IsoPackContract.Registry: cannot resolve '${tok}'`);
  };

  const body = block(CONTRACT, 'Registry');
  const out = {};
  const re = /\[\s*"([^"]+)"\s*\]\s*=\s*new Registration\(([^)]*)\)/g;
  let m;
  while ((m = re.exec(body))) {
    const args = m[2].split(',').map(s => s.trim());
    const contractPath = literal(args[0]);
    const section = args[1] ? literal(args[1]) : null;
    const sheetFolder = args[2] ? literal(args[2]) : contractPath.slice(0, contractPath.lastIndexOf('/'));
    out[m[1]] = { contractPath, section, sheetFolder };
  }
  if (!Object.keys(out).length) throw new Error('IsoPackContract.Registry scraped empty');
  return out;
}

/** The committed contract's own view of a family: its facings and its per-piece sheet plan.
 *  The plan is the ORACLE for packing as much as the cell is for size — never re-derived. */
function contractFor(reg) {
  const json = JSON.parse(fs.readFileSync(path.join(REPO, reg.contractPath), 'utf8'));
  if (!reg.section) return json;
  const fam = (json.families ?? []).find(f => f.key === reg.section);
  if (!fam) throw new Error(`${reg.contractPath} carries no family '${reg.section}'`);
  return fam;
}

export default {
  id: 'iso-prop',
  baker: 'IsoPropSheetBaker',
  // Seven families over five sprite folders — resolved per plan below.
  outputFolder: null,

  provenance() {
    return [
      ['the families and their call shapes', cite(BAKER, 'Shapes')],
      ['each family\'s contract and sprite folder', cite(CONTRACT, 'Registry')],
      ['the render call (opts are {} — rig defaults)', citeLine(BAKER, /EvaluateBytes\(\$"\{g\}\.render/)],
      ['facings and the sheet plan', 'each family\'s committed *.contract.json'],
      ['the grid comes from the plan, never re-derived', cite(CONTRACT, 'GridFor')],
    ];
  },

  plans({ catalog }) {
    const shp = shapes();
    const reg = registry();
    const out = [];

    for (const [family, shape] of Object.entries(shp)) {
      const r = reg[family];
      if (!r) throw new Error(`${family} has a render shape but no contract registration`);
      const c = contractFor(r);
      const convention = catalog[family].convention;
      const facings = c.facings;
      if (!facings) throw new Error(`${family}: the contract declares no facings`);

      for (const cell of c.cells) {
        // The call: render(key, dir, {}) for most, render(dir, {}) for the family that draws exactly
        // one thing, render(key, {}) for the one with no facing axis at all. ⚠️ The opts are EMPTY
        // here, and that is the baker's own call — this family bakes at rig defaults.
        const args = [];
        if (shape.takesKey) args.push(cell.key);
        if (shape.takesDir) args.push('$dir');
        args.push('$opts');

        out.push({
          stem: cell.key,
          sheetFolder: r.sheetFolder,
          rigKey: family,
          cellCall: null,                        // these rigs publish the W/H/pivot triple
          call: { fn: 'render', args, opts: {} },
          axes: shape.takesDir
            ? [{ name: 'facing', bind: 'dir',
                 values: Array.from({ length: facings }, (_, k) => dirForCell(k, facings, convention)) }]
            : [],
          // The grid is the CONTRACT's, not a re-derived one: ChooseGrid would put timberQuay back on
          // one row at 4048 px and quietly undo its ruled repack.
          columns: () => cell.sheet.cols,
          packRule: 'pivotUnionCrop',
        });
      }
    }
    return out;
  },
};
