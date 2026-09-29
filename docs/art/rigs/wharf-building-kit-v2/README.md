# Hidden Harbours — Wharf Buildings, PASS 2 — live light, weather, doors that open, lofts, ladders and stairs, character v9.2 verbs

2026-09-26. The net shed, the storage barn and the fish plant get what the houses got in the village return (phases 1–5) and the wharves got in wharf pass 2. Pass 1 (`Art/wharfBuildingRig.js`, `globalThis.WharfBuilding`, `export/wharf-building-kit/`) is untouched.

    wharfBuildingRig2.geo.js   the geometry → globalThis.WharfBuildingGeo2: types, presets, walls with openings, doors, roofs, interiors, stairs, ladders
    wharfBuildingRig2.js       the rig → globalThis.WharfBuilding2: the light, camera-first API, stations, lights, gameplay(), checks()
    gameplay/                  wharfBuilding2.<preset>.gameplay.json × 7, schema hidden-harbours/wharf-building-gameplay@1
    boards/                    review renders (pass 1 beside pass 2, interiors with v9.2 figures, weather, dereliction)
    checks.txt                 WharfBuilding2.checks() on every preset
    Wharf Building Iso v2.dc.html   the viewer (weather, time, facings, doors, sections, figures, gameplay overlay, channels, sheet); opens from this folder
    lib/                       the viewer's other scripts: interiorPropRig.js and coastalPass.js (hh-village-return houses-kit), weatherSky.js, buildingLifecycleRig.js, character v9.2 (rig, poses)
    support.js                 the viewer's runtime
    SHA256SUMS.txt             every file in the kit except itself

**Load order:** `interiorPropRig.js`, `coastalPass.js` (both from `hh-village-return/houses-kit/Art/`; `lib/` has byte-identical copies for the viewer, so take the houses kit's), then optionally `weatherSky.js` and `buildingLifecycleRig.js`, then `wharfBuildingRig2.geo.js`, `wharfBuildingRig2.js`. Plain scripts, no build step. The canvas and pivot are pass 1's (1200 × 1160, pivot 600, 780, 32 px = 1 m, 40°), so placement code does not change.

## What was wrong in pass 1, and what changed

| | pass 1 | pass 2 |
|---|---|---|
| light | one band per face from a fixed upper-left key, ordered dither, 1 px keyline, no self-shadow | a G-buffer relit from any `WeatherSky.at()` sky on `CoastalPass.light`: sky visibility, sun map, `castShadow()` levels 1–3, no dither, no keyline (`outline:true` for the A/B) |
| weather | a ramp fade, and speckle chosen per screen pixel (it moved between facings) | rain by porosity with glints, height-heavy fog, snow held by material (tin sheds it), a backlit rim, icicles with `season:'winter'`. Moss, rust streaks and peeling paint are model-space patches, so they stay put |
| gambrel | both slopes about 45°: it read as a gable, with unsided gable ends | steep lower slope (2.1 : 1), shallow upper, knee trim, siding to the peak |
| braces, boom | eight squares along a line: dotted at 32 px/m | solid sticks |
| floating parts | stacks 0.6 m over the roof; monitor walls up to 1.2 m clear of the slope; stacks inside the monitor | stacks and pipes set into the roof with flashing, clear of the ridge; the monitor stands on the slope; stacks outside it |
| dock | deck 1.05, floor and bay sills 0.6, so 0.45 m of every bay door was behind the dock; 0.35 m step risers | the plant floor is at dock height, 1.2 m (the eave is unchanged); seven risers of 0.171 on 0.28 goings |
| doors | flat decals | real openings with reveals; `doorOpen` swings the double and plank leaves, slides the barn leaves, rolls up the roll-ups; `bayOpen`, `loftOpen`, `persOpen` |
| interior | none | a net loft over the gear floor, a gear store with a loft, a cutting floor with an office mezzanine; `cutaway:'section'`, `storey:'ground' \| 'loft'` |
| verbs | none | `stations()`: each fixture inside the v9.2 fit, each stand point reached from the approach |
| sign | overlapped the roll-up at small sizes | above the door head, shrunk to fit under the rake and clear of the boom mast, or left off |
| shed-shape peak | the window stood 0.5 m above the wall | placed under the wall's real top, toward its high side |
| cladding | 0.34 × 0.24 m shingles and a 0.30 m lap read as masonry | the houses' scales: 0.18 m courses, 0.20 m lap, 0.30 m battens |
| plant windows | sills inside the cinderblock band | sills above the band; clerestory clear of the bays and the mezzanine |
| night | every pane lit all night | occupancy schedules, lamps with pools, the stove fire |
| placement | none; at facing 3 the plant's dock faced away | `placement()` lists the facing that shows the dock or the buoys first; `mirror:true` for the other diagonal |

## The three types

| type | massing (pass 1's ranges) | inside | outside |
|---|---|---|---|
| `shack` net shed | gable, 3.6–5.0 × 4.5–7.5 m, floor 0.4 | gear floor: bench (mend nets), stove, oilskin pegs; net loft 2.40–2.88 m up a ladder, nets on a pole inside the 2 m headroom strip | gear ramp to wide doors or steps to narrow ones, a stack of traps by the door, buoys on the +X wall, stove pipe, door lamp |
| `storage` barn | gambrel, 5.2–7.4 × 6.5–11 m, floor 0.45 | traps, bait freezer, rope pegs, bench; loft at 2.88 up a 16-riser stair, buoy rack | stone ramp, sliding doors, loft door under a hood with a hoist beam out past the ramp, cupola |
| `processing` fish plant | gable, 7.2–9.8 × 10–16 m, floor 1.2 (dock height) | cutting line, fish boxes, ice bin, scale, hose; office mezzanine at 2.64 up a 14-riser steel stair | apron with a 7-riser stair and pipe rails, the boom over a gap in the rail, truck dock with bumpers and its own stair, roll-up bays, personnel door, wall packs, HVAC, monitor, stacks |

Presets are pass 1's seven, unchanged: `netShed · redShed · tealShack · gambrelBarn · iceHouse · fishPlant · cannery`. New options: `kept`, `plinth` (stone · concrete · sills), `stove`, `goods`, `personnel`, `mirror`, `doorOpen`, `bayOpen`, `loftOpen`, `persOpen`, `cutaway`, `storey`, `cutH`, `season`, `floorH`, `occupancy`, `schedule`, `lamp`, and the sky (`time`, `cloud`, `rain`, `fog`, `snow`, or `sky`).

## Character v9.2

Numbers are every creator body's (`export/character-v9.2-import-kit/reports/worldfit.txt`); the ladder is the wharf rig's.

| fixture | built to | fit |
|---|---|---|
| bench (mend nets, paint buoys) · tally desk | 0.80 | bench 0.643–0.853 |
| cutting table (fillet) | 0.72 | knife 0.598–0.760 |
| trap stack · freezer · scale · fish boxes | 0.78 · 0.85 · 0.85 · 0.94 | load 0.718–0.978 |
| pegs · net pole · buoy rack · stove door · hose · cabinet | 1.0 · 0.95 · 0.95 · 0.35 · 1.0 · 0.95 | rest 0–1.012 |
| loft ladder | rung 0.24, top-out `board` at 0.72, root 0.275 off the rungs | rung 0.211–0.273, rail 0.549–0.724 |
| doors | clear ≥ 0.758, ≥ 2.0 high | the widest creator body + 0.3 |
| stairs | risers ≤ 0.19 inside, ≤ 0.2 outside, goings ≥ 0.26, ≥ 0.9 wide, 2 m headroom over every tread | — |
| hoist, boom | `haul` under the whip | — |
| ice bin | `dig` at the floor behind a 0.3 m board | — |

Every verb is `{ id, kind, verb, clip, opts, fixture, fixtureZ, at, stand, standZ, level, faceYaw, reachable, fits }`; ladders add `rungs`, `top`, and `seq.up` / `seq.down`; stairs add `n`, `riser`, `going`, `pitchDeg`, `top`, `seq` and `exact`; the door adds `clearWidth`, `opens`, `entry` and `path`. Reach is a 0.1 m grid from the approach at radius 0.229, round the wall segments (floor-level openings are gaps), platform edges and fixtures; the loft is its own level, entered from the ladder landing or the stair top, blocked where the roof is under 2 m.

**Asked of the character rig** (`CHAR_REQUESTS`): `stairWalk` (until then, walk with root z along the pitch line, feet within ±2.1–2.3 px; or, exact for every body, `boardDown` per riser), `walkSlope` (the 16–17° gear ramps), `ladderOn / ladderOff`, `sit`, and the fish box as the `tray` carry.

## Night

`SCHEDULES`: fisher 04:00–06:15 and 15:30–20:30, store 06:30–07:30 and 16:00–18:30, plant 05:00–17:30, closed. Windows glow by room (`win:ground`, `win:loft`, `win:office`) while the building is in use after dark; the door lamp burns while in use; the plant's wall packs burn dusk to dawn unless `closed`; the stove fire glows while in use. `occupancy: 'working' | 'away' | 'closed'` overrides the schedule; a decay of abandoned or worse puts everything out. `lights(dir, o)` gives every lamp's level, model point, screen anchor and ground pool.

## Checks (`checks.txt`)

All seven presets pass: every verb fits and is reached, the stairs and ladders meet their numbers, the sign clears the door, every stack stands on the roof, the crop fits the 1200 × 1160 cell, and the door shows 97–100 % at every listed facing on the sheds and barns and 81–91 % on the plants (the apron rails and the boom take the rest).

- **Sheet:** the sheds and barns pack 8 facings under 2048. The fish plant and the cannery need 4096, as pass 1's cannery did (the game already bakes it there).
- **Dereliction:** `BuildingLifecycle` still applies. With a decay state on, the rig builds the single-skinned shell it reads, so the three sheds on Ginny's plot (neglected, ruin, collapsing) and the collapsing cannery render as before.

These ran through this workspace's browser engine. `WharfBuilding2.checks(preset)` is the same code on node.

## To do on node

- Re-bake the 7 presets and the game's lifecycle set, and re-measure the tight cells: they differ from pass 1's (the plant's apron and boom).
- The fish plant's floor moved from 0.6 to 1.2 m to meet the dock, with the eave kept. `floorH: 0.6` gives the old floor if a placed plant needs it.
- Register `WharfBuilding2` in the rig catalog beside `WharfBuilding`. The API matches pass 1's (`render`, `anchors`, `project`, the tables), so the baker can swap the global.

## Open

- The figures in the viewer are lit by one gain from the sky, not relit per pixel. This is the wharf viewer's open item too.
- The shadow falls on the ground only. The building shades itself, but it does not shade a boat or a neighbour.
- The sign board is blank for the game to letter. Its anchor is in `anchors().sign`.
- `pitchHay`-style forking and seated work wait for clips.
