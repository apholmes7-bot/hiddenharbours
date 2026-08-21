// The dooryard kit's boundary family — ten pieces, one dressing each, eight facings on one row.
//
// The build table is a C# array of `new Build(...)` rows, so this kit reads it with its own row
// pattern rather than the generic array scraper. Anything the pattern cannot parse throws: a short
// table would bake a short kit, in silence, and that is this repo's most-repeated failure.
import { constant, source, cite, citeLine } from '../lib/csharp.mjs';
import { dirForCell } from '../lib/rigHost.mjs';

const KIT = 'Assets/_Project/Art/Editor/YardKit.cs';
const RIG_KEY = 'yardIso';

const ROW = /new Build\(\s*"([^"]+)"\s*,\s*Role\.(\w+)\s*,\s*(null|"[^"]*")\s*,\s*variant:\s*([^,]+),\s*len:\s*([^,]+),\s*kept:\s*([^,]+),\s*seed:\s*([^,]+),/g;

function builds() {
  const runLen = constant(KIT, 'RunLen');
  const fixedLen = constant(KIT, 'FixedLen');
  const num = (t) => {
    const s = t.trim();
    if (s === 'RunLen') return runLen;
    if (s === 'FixedLen') return fixedLen;
    const n = Number(s.replace(/[fdmFDM]$/, ''));
    if (Number.isNaN(n)) throw new Error(`YardKit build row: '${t}' is not a number`);
    return n;
  };
  const out = [];
  const src = source(KIT);
  let m;
  ROW.lastIndex = 0;
  while ((m = ROW.exec(src))) {
    const [, piece, , paint, variant, len, kept, seed] = m;
    out.push({ piece, paint: paint === 'null' ? null : paint.slice(1, -1),
               variant: num(variant), len: num(len), kept: num(kept), seed: num(seed) });
  }
  if (out.length === 0) throw new Error('YardKit.Builds scraped empty');
  return out;
}

export default {
  id: 'yard',
  baker: 'YardSheetBaker',
  outputFolder: 'Assets/_Project/Art/Sprites/Yard',

  provenance() {
    const B = 'Assets/_Project/Code/Tools/Editor/RigBaking/YardSheetBaker.cs';
    return [
      ['the build table', cite(KIT, 'Builds')],
      ['the run / fixed len dials', `${cite(KIT, 'RunLen')}, ${cite(KIT, 'FixedLen')}`],
      ['facings', cite(KIT, 'Facings')],
      ['the dressing every sheet bakes at', cite(KIT, 'BakedPhase')],
      ['the options expression', cite(KIT, 'OptionsJs')],
      ['the opts dict, checked against it at bake time', cite(B, 'AssertOptionsAgree')],
      ['the grid (cols = facings, one row)', citeLine(B, /int cols = facings, rows = 1/)],
    ];
  },

  plans({ catalog }) {
    const convention = catalog[RIG_KEY].convention;
    const facings = constant(KIT, 'Facings');
    const prefix = constant(KIT, 'StemPrefix');
    const phase = constant(KIT, 'BakedPhase');
    const snow = constant(KIT, 'BakedSnow');
    const weather = constant(KIT, 'BakedWeather');
    const night = constant(KIT, 'BakedNight');
    const compose = constant(KIT, 'BakedCompose');
    const elev = constant(KIT, 'Elevation');

    return builds().map(b => ({
      stem: `${prefix}${b.piece}`,
      rigKey: RIG_KEY,
      cellCall: null,                          // this rig publishes the W/H/pivot triple
      // YardKit.OptionsJs, in its own key order. ⚠️ Omitting a key is not "no value": resolve()
      // substitutes its own, and for `len` that default is 0.4 on a run piece rather than 0.
      call: { fn: 'render', args: [b.piece, '$dir', '$opts'],
              opts: { paint: b.paint, variant: b.variant, len: b.len, kept: b.kept,
                      phase, snow, weather, night, seed: b.seed, compose, elev } },
      axes: [{ name: 'facing', bind: 'dir',
               values: Array.from({ length: facings }, (_, k) => dirForCell(k, facings, convention)) }],
      columns: () => facings,                  // columns are facings, one row
      packRule: 'pivotUnionCrop',
    }));
  },
};
