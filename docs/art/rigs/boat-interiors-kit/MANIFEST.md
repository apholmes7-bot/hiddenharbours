# Boat interiors — handoff manifest

Generated 2026-08-19. Every file below is regenerated from the current rig sources; the two sha
fields on each sidecar (`derivedFromRigSha256` = interior rig, `hullRigSha256` = the exterior loft
it was measured against) are the only provenance you need to check.

## What to read first
- `README.md` — the brief §1–§7 walkthrough: contract, schema, exclusions, what shipped per hull.
- `boatInteriorRig.js` — THE interior renderer. Everything else is generated from it.

## Layout
    boatInteriorRig.js                     interior renderer (single source; sha in README §1)
    README.md                              program readme (brief §1–§7)
    MANIFEST.md                            this file
    <hullStem>.interior.json          ×9   per-hull interior sidecars
    lobsterBoatVariants/*.interior.json ×18 per-variant interior sidecars
    hull-rigs/*.js                    ×9   exterior rigs (published HOUSE/loft/DOOR live here)
                                           ⚠ capeIslanderIsoRig.js is OUR branch — do NOT adopt it;
                                             see README Corrections 3 (two divergent pass-2 branches)
    gameplay/*.gameplay.json          ×9   hull gameplay sidecars, interiors merged in
    gameplay/lobsterBoatVariants/     ×18  variant gameplay sidecars, interiors merged in

## Hull coverage (10 rig families, 27 hull instances)
| hull | door | interior levels | routes up |
|---|---|---|---|
| Lobster 12 m | slide, aft | house + cuddy | — (washboards) |
| Cape Islander ⚠ | slide, aft | house + cuddy | — |
| Side dragger 25 m | hinge 110°, fwd | bridge/house/below | boat-deck ladder |
| Stern trawler 34 m | hinge 110°, fwd | bridge/house/below | boat-deck ladder |
| Stern trawler Mk II 38 m | hinge 110°, fwd | bridge/house/below | boat-deck ladder |
| Coastal packet 60 m | hinge 110°, fwd | bridge/house/below | boat-deck ladder |
| Tanker 110 m (16 px/m) | hinge 110°, aft (poop) | bridge/house/below | 2 break ladders + boat deck |
| 53′ Convertible | slide, aft (glass) | helm deck + salon + below | interior stair + mezz→flybridge ladder |
| 90′ Skybridge | slide ×2 (salon + skylounge) | skylounge + salon + below | interior stair + mezz→aft deck→coaming |
| Lobster variants ×18 | slide, aft | house + cuddy | — |

## Consuming the sidecars
Sections are additive against the hull's existing gameplay file: DECK entries replace by `id`,
THRESHOLD/STAIRS/LADDER/INTERACT replace wholesale, `_excluded` merges. **INTERACT carries object
interactables only** — stair routes are declared in STAIRS, which carries each companionway's own
`mechanism: InteriorStair`; they are not INTERACT entries (ruled 2026-08-19, see README Corrections). `_supersedes` carries the
previous hull-rig sha so a stale merge is detectable. Door animation is an 8-frame baked cue
(`doorOpen` = k/7, ~70 ms/frame), reversed on exit; interiors ride the hull's `rock(i)` on the same
camera basis, so exterior and interior sheets stay in register mid-wave.
