// The fuel storage & dispensing kit — 8 vessels × 21 sizes × 4 grades = 84 sheets.
//
// Every fact below is SCRAPED from the baker's own source, not retyped beside it:
// FuelStorageKit states the axes and the tunables, FuelSheetBaker states the call. The two
// transcribed lines are the sheet-key rule and the grid, and the byte-compare is what proves them.
import { constant, array, dictionary } from '../lib/csharp.mjs';
import { dirForCell } from '../lib/rigHost.mjs';

const KIT = 'Assets/_Project/Art/Editor/FuelStorageKit.cs';
const RIG_KEY = 'fuel';

export default {
  id: 'fuel-storage',
  baker: 'FuelSheetBaker',
  outputFolder: 'Assets/_Project/Art/Sprites/FuelStorage/Iso',

  plans({ catalog }) {
    const convention = catalog['fuel'].convention;
    const sizes = dictionary(KIT, 'Sizes');
    const grades = array(KIT, 'Grades');
    const fills = array(KIT, 'Fills');
    const carryTypes = array(KIT, 'CarryTypes');
    const holdsNothing = array(KIT, 'HoldsNothing');
    const wear = constant(KIT, 'BakedWear');
    const carryFacings = constant(KIT, 'CarryFacings');
    const storeFacings = constant(KIT, 'StoreFacings');
    const bakeCarryAtRest = constant(KIT, 'BakeCarryTypesAtRest');

    const out = [];
    // FuelStorageKit.Builds: every type × size × grade, in the table's own order.
    for (const [type, typeSizes] of Object.entries(sizes)) {
      const carry = carryTypes.includes(type);
      const facings = carry ? carryFacings : storeFacings;
      const rest = carry && bakeCarryAtRest;
      const cellFills = holdsNothing.includes(type) ? [0] : fills;

      for (const size of typeSizes) {
        for (const grade of grades) {
          // FuelSheetBaker.Opts — spelled out in full on every call and in the baker's own key order.
          // ⚠️ An omitted `wear` is not "the default": it selects a phantom fourth state that shares
          // a geometry-cache key with 'working', so the picture depends on what rendered first.
          const opts = { size, fuel: grade, fill: cellFills[0], wear };
          if (rest) opts.rest = true;

          out.push({
            stem: `${type}_${size}_${grade}`,
            rigKey: RIG_KEY,
            cellCall: { fn: 'cell', args: [type, size, '$opts'] },
            call: { fn: 'render', args: [type, '$dir', '$opts'], opts },
            axes: [
              { name: 'facing', bind: 'dir',
                values: Array.from({ length: facings }, (_, k) => dirForCell(k, facings, convention)) },
              { name: 'fill', bind: 'opt:fill', values: cellFills },
            ],
            // Columns are facings, rows are fills — this kit's own layout.
            columns: () => facings,
            packRule: 'pivotUnionCrop',
          });
        }
      }
    }
    return out;
  },
};
