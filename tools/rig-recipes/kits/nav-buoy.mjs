// The nav-buoy kit — ten shipping marks × five hull diameters = 50 sheets, eight facings on one row.
//
// ⭐ TWO THINGS HERE ARE NOT LIKE THE OTHER KITS, and both move pixels:
//   · the cell is measured WEAR-INVARIANT — the union runs over all three wear states and only
//     'working' ships, so a per-sheet ink union would give a tighter rect than what shipped and a
//     placed mark would hop as it weathered;
//   · the native pivot is TRUNCATED (`(int)`), not rounded, unlike every other baker in the repo.
import { constant, array, source, cite, citeLine } from '../lib/csharp.mjs';
import { dirForCell } from '../lib/rigHost.mjs';
import fs from 'node:fs';
import path from 'node:path';
import { REPO } from '../lib/csharp.mjs';

const KIT = 'Assets/_Project/Code/Tools/Editor/RigBaking/NavBuoyKit.cs';
const RIG_KEY_RE = /RigKey\s*=\s*"([^"]+)"/;

export default {
  id: 'nav-buoy',
  baker: 'NavBuoySheetBaker',
  outputFolder: 'Assets/_Project/Art/Sprites/NavBuoys/Iso',

  provenance() {
    const B = 'Assets/_Project/Code/Tools/Editor/RigBaking/NavBuoySheetBaker.cs';
    return [
      ['the shipping marks', cite(KIT, 'BakeTypes')],
      ['the hull diameters', cite(KIT, 'Sizes')],
      ['facings', cite(KIT, 'Facings')],
      ['the shipped wear and lamp state', `${cite(KIT, 'BakeWear')}, ${cite(KIT, 'BakeLit')}`],
      ['the WEAR-INVARIANT cell', cite(KIT, 'MeasureCell')],
      ['the opts dict', cite(KIT, 'OptsOf')],
      ['the TRUNCATED native pivot', citeLine(B, /int npx = \(int\)/)],
      ['the sheet plan', 'Assets/_Project/Art/Sprites/NavBuoys/Iso/navBuoyKit.contract.json'],
    ];
  },

  plans({ catalog }) {
    const rigKey = RIG_KEY_RE.exec(source(KIT))[1];
    const convention = catalog[rigKey].convention;
    const types = array(KIT, 'BakeTypes');
    const sizes = array(KIT, 'Sizes');
    const wears = array(KIT, 'WearStates');
    const facings = constant(KIT, 'Facings');
    const wear = constant(KIT, 'BakeWear');
    const lit = constant(KIT, 'BakeLit');

    // The committed contract carries the sheet plan; packing is never re-derived here.
    const contract = JSON.parse(fs.readFileSync(
      path.join(REPO, 'Assets/_Project/Art/Sprites/NavBuoys/Iso/navBuoyKit.contract.json'), 'utf8'));
    const cellOf = (type, size) => {
      const c = contract.cells.find(x => x.key === `${type}|${size}`);
      if (!c) throw new Error(`navBuoyKit.contract.json carries no cell '${type}|${size}'`);
      return c;
    };

    const dirs = Array.from({ length: facings }, (_, k) => dirForCell(k, facings, convention));

    return types.flatMap(type => sizes.map(size => ({
      stem: `NavBuoy_${type}_${size}`,
      rigKey,
      pivotRound: 'trunc',
      cellCall: { fn: 'cell', args: [type, size] },
      call: { fn: 'render', args: [type, '$dir', '$opts'], opts: { size, wear, lit } },
      axes: [{ name: 'facing', bind: 'dir', values: dirs }],
      // ⭐ The crop unions over the wear axis the sheet does not carry.
      cropAxes: [
        { name: 'facing', bind: 'dir', values: dirs },
        { name: 'wear', bind: 'opt:wear', values: wears },
      ],
      columns: () => cellOf(type, size).cols,
      packRule: 'pivotUnionCropOverWear',
    })));
  },
};
