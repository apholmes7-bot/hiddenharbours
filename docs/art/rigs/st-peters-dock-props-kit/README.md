# St Peters dock props

Phase A art for the landing, serving the living working coast. One metre is one rig unit; PPU 32, eight facings, elevation 40 degrees. The existing decor palettes, faceted primitives, ordered dither, depth edges and weather law carry through into the new source. The old rigs and shared sheets are unchanged.

`stPetersDockProps.js` exports `StPetersDockProps` (also CommonJS). Load as a plain script or require it on real Node. It has no dependencies. `render(key, dir, options)` returns RGBA; `frame` additionally returns dimensions, a top-left pixel pivot and a per-pixel local height buffer. `geometry`, `bounds`, `project`, `spec` and `ladderContract` expose metric geometry and contacts. Unknown keys and unsupported ladder dimensions refuse.

The two new models are `dockCart` and `ladder`. The cart has a 1.22 × 0.72 m timber deck at 0.34 m, four 0.25 m wheels, two uprights and one fixed full-width end push bar at 0.98 m. The two loaded fish/ice boxes fit on its deck. `{loaded:false}` shows the deck. There is no centre draw handle. Default weather is 0.35; paint, metal, weather, night and outline remain options.

`recipes.json` keeps the three landing prop IDs. Traps and totes reuse the existing immutable `WharfDecor` drawing with the exact board options. Their dedicated recipe sheets must be baked before the proposed consumer patch is applied. A tote variant of 1 does not change this rig's unloaded shell geometry: this is recorded rather than inventing a visible variant. The new art ID is `prop.stp_landing_ladder`; the gameplay fitting retains `wharf.st_peters.ladder_0`.

The ladder takes `height` in metres (0.60–14.00), `top`, `lowWater` and `highWater`. Rung spacing is fixed at 0.30 m, standoff at 0.275 m. Its origin is the hanger pivot at the deck edge. Local +y points out from the pier; +z points up, so the ladder descends through negative z. World heading 180 degrees faces south. Each stile is 0.05 m diameter, the clear rung span is 0.50 m, and rung diameter is 0.035 m. Attachment returns finish at deck height. Brackets, plates and bolts repeat down the face. Wet metal and weed bands derive from the same water levels as the tide board; no water pixels are baked.

The St Peters recipe has top 5.354004 m, height 8.10 m, foot -2.745996 m. Its 27 rung centres run from 5.054004 to -2.745996 m above datum. The region's mean 0 and amplitude 2.20 give the conservative envelope [-2.20, +2.20]. The foot is 0.545996 m below that lower bound. The lowest rung's tube extends a further 0.0175 m. The wet band ends at +2.20; weed is concentrated below -1.50. These are art dimensions, not a traversal implementation.

`ladder.gameplay.json` publishes the rig SHA, top, foot, rung list in local and datum coordinates, standoff, hand/foot lines, body clearance, hanger pivot and preserved fitting ID. The traversal lane owns placement, motions and the dory berth. It must use its live water level and the actual figure contact pins; the sheet is a scale/contact reference, not proof of animated boarding.

The prospective three-row route uses existing `SetPieceDef` assets with keys `landingCart`, `landingTraps` and `landingTotes`; it selects their frames by the exact landing IDs. All other decor keeps the generic lookup. No new Def type or shared importer change is required. The evidence patch is against the specified deck head and is intentionally outside this kit. Future reuse in another region requires an explicit opt-in recipe/consumer choice.

Run from the repository root:

```
node docs/art/rigs/st-peters-dock-props-kit/check.cjs
```

An optional output directory writes `kit-check.json`. Eight named checks cover stable IDs, exact retained recipes, source hashes, rung geometry and water reach, cart supports/scale, all eight facings at three ladder heights, deterministic pixels, sidecar derivation, the regional tide profile and the five LF-normalized file sums. No editor, scene, save or sheet is written by the check.

The Phase A contact sheet compares committed sprite crops, board crops, real Node board-option renders and the proposed art. Owner selection precedes any game bake. Phase B must verify the dedicated sheets with the existing materials at both exact board frames and tides, plus high/low ladder contact plates beside the dory and rig-10 figure. The future consumer must preserve height-aware water clipping; no shared water system is changed by this source-only kit.
