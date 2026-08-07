# Hidden Harbours — ISO Rig Pack

Four parametric rigs, one shared camera. Each folder is self-contained: a plain `.js` rig with no
imports and no build step, its own README, and machine-readable catalogue / contract data.

| folder | rig | global | contents |
|---|---|---|---|
| `wharf-kit-iso/` | `wharfIsoRig.js` | `WharfIso` | 7 wharf structure families, 17 presets, tide model, gameplay contract |
| `wharf-decor-iso/` | `wharfDecorRig.js` | `WharfDecor` | 61 pieces of wharf gear and dressing, 7 categories |
| `utility-iso/` | `utilityIsoRig.js` | `UtilityIso` | 42 village services, 6 categories, runtime spans |
| `shoreline-finds-iso/` | `shoreFindsRig.js` | `ShoreFinds` | 36 beachcombing finds from 19 forms, 3 states |

## The shared contract

Everything in the pack goes through the same projection, so anything from any rig can sit on the same
tile as anything else and be in scale with the fleet, the buildings and the characters.

| | |
|---|---|
| scale | 32 px = 1 m |
| camera | ADR-0006 turntable, ¾ from the south, elev 40°, orthographic |
| facings | `N NE E SE S SW W NW` — 8 facings out of one model |
| light | fixed upper-LEFT key, flat-facet, z-buffered, ordered dither, per-face uv texture, depth-edge darkening, no AA |
| keyline | **retired** — all four carry `KEYLINE_DEFAULT = false` (ADR 0031); `{outline:true}` restores the ring |
| pivot | ground contact — footprint centre for the props and structures, `sit` for the finds |
| alpha | binary |

Nothing here bakes water. The wharf rig hands out a `wet` mask instead and the shader decides.

Two keyline colours, still declared on purpose even though the ring no longer bakes: the structures
and props use the cold harbour keyline `#1a1c22`; the shore finds use a warm sand keyline `#231d14`,
because they sit on beach, not on planking. The colours stay exported and stay in each rig's
contract so the A/B arm and the archived sheets remain describable — ADR 0031 gates the ring, it
does not delete it. Depth-edge darkening is the separate INTERIOR rule and is untouched.

## Sheets

The prop rigs (`WharfDecor`, `UtilityIso`) render onto a **fixed sheet** with a fixed pivot — 420 × 520
at (210, 420) and 440 × 620 at (220, 520) — sized for the tallest member of each family. Crop to the
ink bbox when you pack, and carry the pivot with the crop.

`WharfIso` and `ShoreFinds` return **tight cells** and report the pivot per bake: `px, py` for the
wharf, `cellOf(key)` for the finds.

## Load them together

    <script src="wharf-kit-iso/wharfIsoRig.js"></script>
    <script src="wharf-decor-iso/wharfDecorRig.js"></script>
    <script src="utility-iso/utilityIsoRig.js"></script>
    <script src="shoreline-finds-iso/shoreFindsRig.js"></script>

No order dependency, no shared state, no globals beyond the four rig objects.

## How they divide the waterfront

- **structure** — the deck, piles, cribs, floats, gangways, slipways, riprap, and the fittings that
  belong to a berth (cleats, bollards, rails, ladders, fenders): `wharf-kit-iso`.
- **dressing** — the gear, handling kit, lifting tackle, safety boards, drying flakes and clutter that
  sit *on* that deck: `wharf-decor-iso`.
- **services** — power, light, water, sewer, fuel, telecom, for the wharf and the village behind it:
  `utility-iso`.
- **shore** — what the tide leaves on the sand below the wharf: `shoreline-finds-iso`.

Viewer pages, in-project: `Wharf Kit Iso.dc.html` · `Wharf Decor Iso.dc.html` · `Utility Iso.dc.html` ·
`Shoreline Finds Iso.dc.html`.
