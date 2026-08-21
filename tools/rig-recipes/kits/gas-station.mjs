// The gas-station kit — 21 exterior pieces plus 2 sales floors = 23 sheets, over TWO globals with
// two different argument orders (StationIso is render(type, dir, opts); StationInterior is
// render(size, dir, opts), and neither throws when handed the other's shape).
import { constant, array, dictionary, cite, citeLine } from '../lib/csharp.mjs';
import { dirForCell } from '../lib/rigHost.mjs';

const KIT = 'Assets/_Project/Art/Editor/GasStationKit.cs';

export default {
  id: 'gas-station',
  baker: 'GasStationSheetBaker',
  outputFolder: 'Assets/_Project/Art/Sprites/GasStation/Iso',

  provenance() {
    const B = 'Assets/_Project/Code/Tools/Editor/RigBaking/GasStationSheetBaker.cs';
    return [
      ['the sheet list (21 pieces + 2 sales floors)', cite(KIT, 'Builds')],
      ['sizes per piece', cite(KIT, 'Sizes')],
      ['the interior sizes', cite(KIT, 'InteriorSizes')],
      ['the 180° fold', `${cite(KIT, 'Fold180Types')}, ${cite(KIT, 'FoldedFacings')}`],
      ['facings', cite(KIT, 'Facings')],
      ['the opts dict', cite(B, 'OptsOf')],
      ['the two argument orders', citeLine(B, /InteriorGlobalName\}\.render/)],
      ['the column solver', `${cite(KIT, 'Columns')} (called at ${citeLine(B, /int cols = GasStationKit\.Columns/)})`],
      ['the import cap', cite(KIT, 'ImportSizeCap')],
    ];
  },

  plans({ catalog }) {
    const convention = catalog['gasStation'].convention;
    const sizes = dictionary(KIT, 'Sizes');
    const interiorSizes = array(KIT, 'InteriorSizes');
    const fold180 = array(KIT, 'Fold180Types');
    const facingsFull = constant(KIT, 'Facings');
    const facingsFolded = constant(KIT, 'FoldedFacings');
    const cap = constant(KIT, 'ImportSizeCap');

    // GasStationSheetBaker.Opts — every option explicit. `grades` is GEOMETRY here: the rig
    // generates hose count, sign rows and apron caps from that list.
    const opts = {
      size: null,
      grades: array(KIT, 'BakedGrades'),
      lit: constant(KIT, 'BakedLit'),
      hoses: 0,
      sides: 0,
      out: constant(KIT, 'BakedOut'),
      style: constant(KIT, 'BakedStyle'),
      open: constant(KIT, 'BakedOpen'),
      span: constant(KIT, 'BakedSpan'),
      bollards: constant(KIT, 'BakedBollards'),
      bin: constant(KIT, 'BakedBin'),
      wear: constant(KIT, 'BakedWear'),
      seed: 11,
      keyline: false,
      elev: 40,
    };

    // GasStationKit.Columns — the widest layout that fits the cap on both axes, preferring an EXACT
    // divisor of the facing count so no sheet ships a blank ninth cell.
    const columns = (facings) => (cellW, cellH) => {
      let uneven = 0;
      for (let cols = facings; cols >= 1; cols--) {
        const rows = Math.ceil(facings / cols);
        if (cols * cellW > cap || rows * cellH > cap) continue;
        if (facings % cols === 0) return cols;
        if (uneven === 0) uneven = cols;
      }
      return uneven;
    };

    const plan = (type, size, interior) => {
      // ⚠️ ALWAYS the EIGHT-facing dir mapping, even for a folded piece: the fold wants dirs 0·1·2·3,
      // one per equivalence class, NOT the decimation 0·2·4·6 that DirForCell(k, 4, …) would give.
      const facings = interior ? facingsFull : (fold180.includes(type) ? facingsFolded : facingsFull);
      const o = { ...opts, size };
      return {
        stem: `${type}_${size}`,
        rigKey: interior ? 'stationInterior' : 'gasStation',
        cellCall: interior ? { fn: 'cell', args: [size, '$opts'] }
                           : { fn: 'cell', args: [type, size, '$opts'] },
        call: interior ? { fn: 'render', args: [size, '$dir', '$opts'], opts: o }
                       : { fn: 'render', args: [type, '$dir', '$opts'], opts: o },
        axes: [{ name: 'facing', bind: 'dir',
                 values: Array.from({ length: facings },
                                    (_, k) => dirForCell(k, facingsFull, convention)) }],
        columns: columns(facings),
        packRule: 'pivotUnionCrop',
      };
    };

    return [
      ...Object.entries(sizes).flatMap(([type, ss]) => ss.map(s => plan(type, s, false))),
      ...interiorSizes.map(s => plan('interior', s, true)),
    ];
  },
};
