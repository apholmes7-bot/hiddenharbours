// The camper kit — two lengths × two roles = 4 sheets, on the swing axis.
//
// Two things here are not like the other kits, and both are load-bearing:
//   · the AWNING is READ from the rig for a `rest` sheet (`resolve({variant}).awning`) and forced
//     off for an `enter` one, because the Clipper's default awning stands over its own door and
//     hides the cue it exists to show;
//   · both roles of a variant share ONE crop, so the two sheets slice into identical rects.
import { constant, array } from '../lib/csharp.mjs';
import { dirForCell } from '../lib/rigHost.mjs';

const KIT = 'Assets/_Project/Art/Editor/CamperKit.cs';
const RIG_KEY = 'camper';

export default {
  id: 'camper',
  baker: 'CamperSheetBaker',
  outputFolder: 'Assets/_Project/Art/Sprites/Camper/Iso',

  plans({ catalog, rig }) {
    const convention = catalog[RIG_KEY].convention;
    const variants = array(KIT, 'Variants');
    const facings = constant(KIT, 'Facings');
    const cueFrames = constant(KIT, 'CueFrames');
    const restFrames = constant(KIT, 'RestFrames');
    const weather = constant(KIT, 'BakedWeather');
    const paintJs = constant(KIT, 'BakedPaint');       // the literal spliced into the call: "null"
    const paint = paintJs === 'null' ? null : paintJs;

    const g = rig(RIG_KEY);

    const out = [];
    for (const variant of variants) {
      for (const role of ['rest', 'enter']) {
        const frames = role === 'enter' ? cueFrames : restFrames;
        // Only `enter` decides the awning; `rest` defers to the rig, so a drop that re-fits a
        // variant re-bakes correctly with nobody editing CamperKit.
        const awning = role === 'enter' ? false : !!g.resolve({ variant }).awning;

        // CamperSheetBaker.OptsJs, in the baker's own key order.
        const opts = {
          variant, paint, weather, swing: 0, awning,
          winAwn: false, vents: true, rack: false, propane: true, jacks: true,
          chocks: true, hookups: true, step: true, night: false, outline: false,
        };

        out.push({
          stem: `camper_${variant}_${role}`,
          rigKey: RIG_KEY,
          cellCall: null,                       // this rig publishes the W/H/pivot triple
          cropGroup: variant,                   // ⭐ one crop per VARIANT, over both of its roles
          call: { fn: 'render', args: ['$dir', '$opts'], opts },
          axes: [
            { name: 'swing', bind: 'opt:swing',
              values: Array.from({ length: frames }, (_, f) => (frames === 1 ? 0 : f / (frames - 1))) },
            { name: 'facing', bind: 'dir',
              values: Array.from({ length: facings }, (_, k) => dirForCell(k, facings, convention)) },
          ],
          // Columns are the swing axis and rows are the facings — the obvious layout puts the
          // Clipper at 8 × 322 = 2576 px wide, over the kit's import cap, before a cue frame.
          columns: () => frames,
          packRule: 'pivotUnionCrop',
        });
      }
    }
    return out;
  },
};
