# Hidden Harbours — Wharf Rig, PASS 2 — weather-lit wharves, piers and floats, berths for the fleet, modules that join, character v9.2 fixtures

    wharfRig2.js            the rig → globalThis.WharfRig2: frame() G-buffer, relight(), castShadow(), contact(), view(), sheet()
    wharfRig2.geo.js        the parts → globalThis.WharfGeo2: ramps, the tidal frame, fittings, the fleet, the berth packer
    wharfRig2.fam.js        the families, presets, spec resolution, fittings laid out from the berths, sockets (adds to WharfGeo2)
    wharfRig2.verbs.js      fixtures and verbs for character v9.2, the boarding-step planner → globalThis.WharfVerbs2
    wharfRig2.kit.js        harbours, define() / snap() / gangwayFrom(), the gameplay sidecar → globalThis.WharfRig2Kit
    wharfStage2.js          the viewer's compositor (floor, water shader, structure, figures): the reference for the engine's
    wharfPlates2.js         the viewer's plates (gameplay overlay, berth plans)
    gameplay/               wharfRig2.<preset | harbour-id>.gameplay.json × 29, schema hidden-harbours/wharf-gameplay@2
    maps/                   sprites.json (cell, pivot, sheet: 25 presets × 8 facings) + the engine maps for one fixed and one floating module
    renders/                six review renders
    checks/                 wharfChecks.js (the checks), run.js (Node runner), out/ (their output)
    Wharf Rig Pass 2.dc.html  the viewer; opens from this folder (support.js, lib/)
    lib/                    the viewer's other scripts: pixel terrain (PxLang, PxKit, PxKit2, PxKit8), WeatherSky, TerrainLight4/5/6, character v9.2
    SHA256SUMS.txt          every file in the kit except itself and checks/out/sums.txt

Load order: `wharfRig2.geo.js`, `wharfRig2.fam.js`, `wharfRig2.verbs.js`, `wharfRig2.js`, `wharfRig2.kit.js`. Plain scripts, no imports, no build step.
The rig needs nothing else; `relight()` takes any `WeatherSky.at()` sky. Pass 1 (`export/wharf-iso-rig-kit/`, `Art/wharfIsoRig.js`) is untouched.

## What was weak in pass 1, and what changed

| | pass 1 | pass 2 |
|---|---|---|
| light | every face ordered-dithered once from the upper-left key; no self-shadow, so the piles under a deck were as bright as the deck | a G-buffer lit by `WeatherSky` with the cliffs' and trees' law: sky × visibility from nine occlusion maps, sun × N·L × a shadow map, ground bounce, the band ladder, harmony's grade |
| floats | rocked on their own 8-frame sine | heave, roll and pitch are the waterplane average of the water shader's own three components; a main float and its fingers are one group; guide piles, anchor and hinge never move; the gangway is re-solved every frame |
| water | painted, or ignored | zero water pixels; every pixel knows its height, so the shader draws the sea over whatever is under the tide; `contact()` gives the waterline for the foam lace |
| weather | none | cloud, rain (porosity darkens, lit tops glint), fog (height-heavy), snow (one byte per pixel), the tide's splash line, a sun rim when back-lit, water dapple on shaded faces |
| parameters | style axes only | the trees': variant 0–3, season, stage (new · seasoned · weathered · derelict), wind, 16-frame loop, plus tide, sea state and facing |
| berths | stopped at an 18 m dragger, sized off the deepest water under the module | 22 hulls from the dory to the gas tanker in five classes; berths only where the water is, the deep end to the biggest hulls, drying berths published as drying |
| modules | sat side by side | ends join (end wall, rail return and a pile row drop), sides open spans, decks meet flush; `define()`, `snap()`, `gangwayFrom()` build a harbour from sockets |
| figures | fittings drawn to fit a cell | every ladder, cleat, step and table built to a height every v9.2 creator body fits, each with its verb |
| gangways | slope from deck and water only | 1:3 at chart datum (the length was the span, not the ramp: 19.3° at datum; now drop / sin, rounded up); the toe's margin on its float published at both extremes |
| dolphins | moored nothing | bollards and panel fenders published as ties and fenders; the tanker berth takes the dolphins' lines |

## Parameters

`frame(key, o)`, `gameplay(key, o)` and `sheet(key, o)` take the trees' parameters and the structure's own. `key` is a family, a preset or `harbour:<id>`.

| option | values | |
|---|---|---|
| `variant` | 0–3 | seeds the derelict deck's missing boards, the icicles, the weed fronds, the fill and the stone |
| `season` | summer · autumn · winter | growth colours; winter adds ice collars at 0.62 R, icicles on deck edges, spray rime R − 0.3 … R + 0.9 |
| `stage` / `age` | new 0.05 · seasoned 0.35 · weathered 0.65 · derelict 0.95 / 0–1 | colour and geometry: growth arrives with age; a derelict deck loses boards (published as HOLES) and a third of its rail runs |
| `wind` | {w 0–1, gust, dir ±1} | the sea state under the floats, the splash and wash lines; `WeatherSky.at().wind` |
| `frame` | 0–15 | the loop (LOOP 16); fixed modules only change in the water-bounce dapple |
| `tide` | metres above chart datum | continuous; the tidal frame is fixed, the water moves through it |
| `sea` · `shelter` · `waveDeg` | calm · chop · working · rough · swell · 0–1 · degrees | a named sea instead of the wind's; shelter 0.35 of open water by default |
| `dir` · `ppu` | 0–7 · 32 | the shared ¾ camera, 40°, 45° steps, 32 px = 1 m |

The structure's own: `bays`, `bayLen`, `width`, `run`, `tideRange` (0.3–14 m), `clearance`, `deckZ`, `freeboard`, `bed` (a number or `bed(x, y)`), `faces`, `ends`, `open`, `fittings`, `fixtures`, `berths` (`'auto'`, a hull id, a list of ids, a class letter), `rail`, `curb` and the style axes below.

## Families and presets

| family | what it is |
|---|---|
| `quay` | quay — solid face on fill: mass concrete, driven steel or timber sheet, or granite ashlar — the deep-water berth |
| `pier` | timber pile pier — piles → caps → stringers → planking, X-braced bents; open, sheeted or on steel piles; plank or concrete deck |
| `crib` | stone-filled crib wharf — drift-pinned log crib on rubble ballast, plank or cast cap — the Atlantic Canada vernacular |
| `float` | floating dock — rides the tide and the sea on guide piles: timber + foam, HDPE cubes, concrete pontoon or poly drums |
| `finger` | finger float — the slip between two boats: a narrow float pinned to a main float, one pile at its tip |
| `gangway` | gangway — aluminium ramp, fixed length, hinged on the wharf, its toe rolling on the float — re-solved every frame |
| `slipway` | slipway — ribbed concrete ramp into the water; the tide decides how much of it launches |
| `riprap` | armour-stone edge — graded rock: revetment, breakwater mound or rock-filled sheet-pile cell |
| `dolphin` | dolphin — timber pile cluster, or steel piles under a cast cap: breasting (panel fender) or mooring (bollard) for the big hulls |
| `catwalk` | catwalk — steel truss walkway on grating, rails 1.0 / 0.5 m — spans dolphin to dolphin without piles |

| preset | family | set |
|---|---|---|
| `lowPier` | pier | clearance 0.6, bays 4, bayLen 2.6, width 3, brace false, capIron false |
| `tallPier` | pier | bays 5, bayLen 2.8, width 4.2 |
| `sheetedPier` | pier | struct sheeted, bays 5, width 4.6 |
| `steelPier` | pier | struct steelPile, deck concrete, bays 5, width 6, curb yellow, rail pipe |
| `concreteQuay` | quay | face concrete, bays 6, bayLen 3, width 8 |
| `torbayQuay` | quay | face steelSheet, curb yellow, bays 6, bayLen 3, width 7 |
| `timberQuay` | quay | face timberSheet, bays 5, bayLen 3, width 6.5 |
| `blockQuay` | quay | face block, bays 5, bayLen 3, width 8, curb none |
| `logCrib` | crib | cap plank |
| `cappedCrib` | crib | cap concrete, curb yellow |
| `timberFloat` | float | hull timber |
| `plasticFloat` | float | hull plastic, width 2.5, curb yellow |
| `concreteFloat` | float | hull concrete, width 3, bays 4 |
| `drumFloat` | float | hull drum, bays 2 |
| `finger` | finger | bays 2 |
| `gangway` | gangway | defaults |
| `slipway` | slipway | defaults |
| `graniteEdge` | riprap | stone granite, mound revetment |
| `redEdge` | riprap | stone sandstone, mound revetment |
| `breakwater` | riprap | stone granite, mound breakwater, width 6, bays 6 |
| `sheetCell` | riprap | mound sheetCell, width 5, bays 5 |
| `timberDolphin` | dolphin | style timber, width 1.4 |
| `breastingDolphin` | dolphin | style steel, role breasting, width 5 |
| `mooringDolphin` | dolphin | style steel, role mooring, width 3.6 |
| `catwalk` | catwalk | run 14 |

## Light: the G-buffer and the sky

`frame()` rasterises a module (or a harbour, as one model) to a G-buffer: material and structure band, face normal, model position, view depth, part, layer (0 the floor layer, 1 raised) and float group; `sv` sky visibility (nine directions, the zenith and a ring at 32°, against occlusion maps of the static structure and each float group in its own rest frame) and `snow`. `relight(frame, sky)` lights it:

    E = 0.36·skyI·(0.30 + 0.70·sv)·(0.45 + 0.55·max(0, n.z))  +  sunI·(n·L)^1.25·shadow  +  bounce·(0.5 − 0.5·n.z)(0.5 + 0.5·sv)  +  water dapple
    band = +1 (E ≥ 0.72) · 0 (≥ 0.24) · −1 (≥ 0.095) · −2 (≥ 0.04) · −3; one band down across a depth break of 0.16 m

then wet (rain by porosity, the tide's splash line at tide + 0.06 + 0.12·wind and damp to tide + 0.40, glints on lit wet tops), snow, the sun rim when back-lit, height-heavy fog and harmony's grade. `castShadow(frame, sky, {z})` gives levels 1 canopy (sky straight down blocked; stays under overcast) · 2 partial · 3 full on the water or the ground, for TerrainLight6's `lv`; `shadeAt()` answers one point.

`view(frame, ch, sky)` returns any channel: `lit unlit normal ao sun height depth parts mat wet snow sub light detail layer`. See `renders/channels-tallPier.png`.

### Engine maps (`maps/`)

`tallPier_d1_*.png` is one fixed module at facing 1 (one frame; cell 421 × 386, pivot 210, 212); `timberFloat_d1_*_sheet.png` is a float's 16-frame loop as 4 × 4 (cell 272 × 336, pivot 135, 171; sheet 1088 × 1344), wind 0.6.

| map | channels |
|---|---|
| `_lit` | `REF_SKY` (afternoon, sun az 290° el 55°), for engines without live light |
| `_unlit` | structure bands under a flat sky |
| `_normal` | world normal: R east · G north · B up, one per face |
| `_light` | R sky visibility · G porosity (× 255 / 0.4) · B height above −3 m over the tide range + 3.5 m |
| `_height` | RG = round((z + 10) × 1000): millimetres above −10 m. The water shader's input: `under = tide − z` per pixel, at any tide |
| `_snow` | the snow byte (below) |
| `_detail` | RG face id + 1 (16-bit) · B part index × 9 |
| `_layer` | R 255 raised (y-sorts by its base) / 0 floor layer (draws under a figure) · G float group × 40 |

A fixed module is one frame per facing: nothing in it moves but the water, and the water is the shader's. A floating module is the loop: every map per frame, the snow byte and sky visibility riding with it. Import as the trees': point filter, no compression, no mips at 1:1, sRGB off on everything but `_lit` and `_unlit`.

## Water: the floats sit on the water shader

**Zero water pixels are baked.** A pixel whose height is under the tide is the shader's: the first 0.04 · 0.09 · 0.16 m of water take it in three steps (0.3 · 0.5 · 0.7 of the water), deeper is water. `contact(frame)` marks the waterline (2 on the line, 1 beside it) for the foam lace (TerrainLight6's `FOAMR`); the viewer's stage does exactly this (`wharfStage2.js`).

A float's motion is the water's: three loop-periodic components (1, 2 and 1 cycles per 16 frames) running toward the shore, heights from the wind (shelter 0.35) or a named sea, and the float takes their waterplane average over its own rectangle (sinc for heave, the first moment for roll and pitch; heave × 0.9, tilt × 0.7). Pinned floats are one group: a main float and its fingers rock as one. Guide piles, the anchor block and the gangway hinge are fixed; the hoops, chain and the gangway's toe follow the float, and the gangway is re-solved from the hinge to the rocked deck every frame, its toe rolling. `rockFor(frame, group, yawDeg)` gives a figure standing on a float its `{ roll, pitch }` for `CharacterIso9.render`. See `renders/float-rock-frames-1-5-9-13.png`.

## Snow: one byte per pixel

`frame.snow` is TreeMaps4's contract: `snowed = round(cover × 254) >= byte`, 255 never. Never: faces steeper than 72° (n.z < 0.3), sky visibility under 0.18, growth (barnacle, weed), and everything under the wash line (tide + 0.25 + 0.2·wind). The byte is keyed on the rest position, so it rides a float. `checks/out/snow.txt` checks the nevers on six cases, rest and rocked; `renders/harbour-fishing-snow.png` is the harbour at 80 % after snowfall.

## Berths for the fleet

| class |  | max LOA | end | side | ukc | line | moor | fender | faces |
|---|---|---:|---:|---:|---:|---|---|---|---|
| A | small craft | 8 | 0.6 | 0.45 | 0.3 | 12–16 mm | cleat · ring | foam | finger · float · pier · crib · slipway |
| B | inshore | 15 | 1.2 | 0.6 | 0.5 | 16–22 mm | cleat | tyre · foam | float · pier · crib · quay |
| C | midshore | 28 | 2 | 1 | 0.6 | 22–28 mm | bollard · cleat | tyre · fender pile | pier · crib · quay |
| D | large | 60 | 4 | 1.5 | 0.8 | 32–40 mm | bollard | panel | quay |
| E | terminal | 120 | 10 | 2 | 1.5 | 40 mm + wires | mooring dolphin | breasting dolphin panel | jetty |

| hull | class | LOA | beam | draft | slot | depth afloat | berths at | moors on · fender |
|---|---|---:|---:|---:|---:|---:|---|---|
| Otter 8×8 amphib | A | 3.1 | 1.56 | 0.52 | 4.3 | 0.82 | finger · float · pier · crib · slipway | cleat · ring · foam |
| dory | A | 4.5 | 1.5 | 0.26 | 5.7 | 0.56 | finger · float · pier · crib · slipway | cleat · ring · foam |
| punt | A | 5.2 | 1.58 | 0.28 | 6.4 | 0.58 | finger · float · pier · crib · slipway | cleat · ring · foam |
| bowrider | A | 5.64 | 2.29 | 0.4 | 6.84 | 0.7 | finger · float · pier · crib · slipway | cleat · ring · foam |
| 6.5 m FRC | A | 6.85 | 2.5 | 0.4 | 8.05 | 0.7 | finger · float · pier · crib · slipway | cleat · ring · foam |
| console skiff | A | 7 | 2.3 | 0.42 | 8.2 | 0.72 | finger · float · pier · crib · slipway | cleat · ring · foam |
| sport skiff | A | 7 | 2.54 | 0.45 | 8.2 | 0.75 | finger · float · pier · crib · slipway | cleat · ring · foam |
| 7.3 m Hurricane | A | 7.47 | 2.6 | 0.45 | 8.67 | 0.75 | finger · float · pier · crib · slipway | cleat · ring · foam |
| lobster boat, inshore | B | 8.6 | 3.55 | 0.95 | 11 | 1.45 | float · pier · crib · quay | cleat · tyre · foam |
| sloop 30 | B | 9.4 | 2.98 | 1.9 | 11.8 | 2.4 | float · pier · crib · quay | cleat · tyre · foam |
| go-fast | B | 11.9 | 2.5 | 0.8 | 14.3 | 1.3 | float · pier · crib · quay | cleat · tyre · foam |
| lobster boat | B | 12 | 4.44 | 1.35 | 14.4 | 1.85 | float · pier · crib · quay | cleat · tyre · foam |
| Cape Islander | B | 12.8 | 4.4 | 1.4 | 15.2 | 1.9 | float · pier · crib · quay | cleat · tyre · foam |
| lobster boat, offshore | B | 14.6 | 5.05 | 1.55 | 17 | 2.05 | float · pier · crib · quay | cleat · tyre · foam |
| sport fisher, convertible | C | 16.2 | 5.16 | 1.4 | 20.2 | 2 | pier · crib · quay | bollard · cleat · tyre · fender pile |
| side dragger | C | 25 | 7 | 2.55 | 29 | 3.15 | pier · crib · quay | bollard · cleat · tyre · fender pile |
| sloop 88 | C | 27 | 6.7 | 4.6 | 31 | 5.2 | pier · crib · quay | bollard · cleat · tyre · fender pile |
| sport fisher, skybridge | C | 27.4 | 7.32 | 1.9 | 31.4 | 2.5 | pier · crib · quay | bollard · cleat · tyre · fender pile |
| stern trawler | D | 38 | 9 | 3.9 | 46 | 4.7 | quay | bollard · panel |
| stern trawler Mk II | D | 38 | 9 | 3.9 | 46 | 4.7 | quay | bollard · panel |
| coastal packet | D | 60 | 10.4 | 4.2 | 68 | 5 | quay | bollard · panel |
| gas tanker | E | 110 | 17.4 | 6.2 | 130 | 7.7 | jetty | mooring dolphin · breasting dolphin panel |

**Packing.** A face is packed with the best mix: the most length used, then the biggest hulls (the sum of LOA²); two mixes within 12 % of the face count as equal, so deep water goes to the boats that need it. A named hull or list (`berths: ['packet', 'trawler', 'dragger']`) is laid out as given.

**Where the water is.** A face keeps berths only where the bed 1.5 m off it is at least 1.0 m under highest water, and is cut where the depth crosses what each class needs afloat at datum (0.56 · 1.30 · 2.00 · 4.70 m), pieces under 3 m joining their shallower neighbour. Each piece takes hulls afloat at datum, else hulls that float for part of the tide (a drying berth), else none. Each berth publishes `bedUnder` (the shallowest bed under its hull), `afloatAtDatum`, `dries`, `floatsFrom` (the tide it floats from), and at this tide `afloatNow` and `aground`; a drying hull sits on the bed in BOARD. Every berth gets a ladder at mid-berth (fixed faces), fenders at the quarters, lines at the bow, stern and springs, and lists its `ties` and `ladder`.

| harbour | berth | hull | class | bed under hull | at chart datum | ties | ladder |
|---|---|---|---|---:|---|---:|---|
| harbour:fishing | crib.water.berth0 | 7.3 m Hurricane | A | 0.57 | dries, floats from 1.32 | 4 | yes |
| harbour:fishing | pier.water.berth0 | console skiff | A | 0.52 | dries, floats from 1.24 | 4 | yes |
| harbour:fishing | pier.water.berth1 | Cape Islander | B | -2.59 | afloat | 4 | yes |
| harbour:fishing | float.water.berth0 | 7.3 m Hurricane | A | -1.79 | afloat | 4 | — |
| harbour:fishing | finger0.water.berth0 | console skiff | A | -1.27 | afloat | 3 | — |
| harbour:fishing | finger1.water.berth0 | console skiff | A | -2.69 | afloat | 3 | — |
| harbour:fishing | finger2.water.berth0 | console skiff | A | -3.7 | afloat | 3 | — |
| harbour:cove | pier.water.berth0 | bowrider | A | -0.03 | dries, floats from 0.67 | 3 | yes |
| harbour:cove | finger0.water.berth0 | console skiff | A | -2.6 | afloat | 3 | — |
| harbour:cove | finger1.water.berth0 | console skiff | A | -1.31 | afloat | 3 | — |
| harbour:commercial | quay0.water.berth0 | coastal packet | D | -5.6 | afloat | 3 | yes |
| harbour:commercial | quay1.water.berth1 | stern trawler | D | -5.6 | afloat | 2 | yes |
| harbour:commercial | quay2.water.berth2 | side dragger | C | -5.6 | afloat | 4 | yes |
| harbour:jetty | platform.water.berth0 | gas tanker | E | -9 | afloat | 4 | yes |

## Modules that join

Every module has sockets (`sockOf(m)`, SOCKETS in the sidecar): `nx` and `px` at its ends and one per bay boundary on each side, at deck height, with an outward normal. An end marked `join` drops its end wall, rail return and first pile row; an `open` span in a side breaks the bull rail and keeps berths and fixtures clear. Deck texture phase runs from each module's −x end on 0.2 m planks, and every bay length is a multiple of 0.2 m, so a chain reads as one deck. A harbour builds as one model: one shadow, one sea.

    const K = WharfRig2Kit;
    const head = { id: 'head', key: 'crib', at: [-6, -1], rot: 0, o: { bays: 2, bayLen: 3.2, width: 5, faces: ['water'], tideRange: 1.2 } };
    const pier = K.snap(head, 'water1', { id: 'pier', key: 'pier', o: { bays: 3, width: 3 } }, 'nx');   // at, rot, ends, open, deckZ set
    const gw   = K.gangwayFrom(pier, 'shore2', { freeboard: 0.32 });       // { hinge, u, L, land, edgeAt, reach }
    const fl   = { id: 'float', key: 'float', grp: 1, rot: Math.atan2(-gw.u[0], gw.u[1]), at: /* near edge on gw.edgeAt */, o: { hull: 'plastic', freeboard: 0.32 } };
    const f0   = K.snap(fl, 'water0', { id: 'finger0', key: 'finger' }, 'nx', { slide: 0.6 });  // freeboard and group from the float
    K.define('myCove', { tideRange: 1.2, bed: (x, y) => …, mods: [head, pier, fl, f0], gangs: [gw] });  // then 'harbour:myCove' anywhere

`snap(a, sa, b, sb, {slide})` sets b facing a with the sockets meeting, marks the join on both, gives b a's tide range and deck height (fixed) or freeboard and group (float), and refuses a float against a fixed deck (that is a gangway). `gangwayFrom()` takes 1:3 at datum onto the float's freeboard and says where the float's near edge goes (`edgeAt`, the toe 0.5 m inside it at datum). `harbour:cove` is built this way in `wharfRig2.kit.js`; `checks/out/joins.txt` checks every join in every harbour.

| key |  | walk | climb | tie | berths | fixtures | verbs |
|---|---|---|---|---|---|---|---|
| `harbour:fishing` | crib, 30 m pile pier, gangway, float with three fingers, slipway, revetment; 1.8 m tide | 9 | 4 | 25 | 7 | 13 | 92 |
| `harbour:cove` | built with define() + snap() + gangwayFrom(); PEI 1.2 m tide | 8 | 2 | 9 | 3 | 11 | 46 |
| `harbour:commercial` | three 48 m steel-sheet quays joined: packet, stern trawler, side dragger | 3 | 3 | 12 | 3 | 9 | 45 |
| `harbour:jetty` | tanker platform, breasting and mooring dolphins, catwalks | 9 | 1 | 4 | 1 | 6 | 20 |

## Character v9.2

Every fixture a figure uses is built to a height at which every creator body (375) makes every limb target of its clip within 5 mm (`export/character-v9.2-import-kit/reports/worldfit.txt`, rig `02df29ec…`, poses `42179e44…`), and publishes its verb: where to stand, which way to face, the clips in order with their opts, and what the engine does between them.

| fixture | built to | every creator body fits |
|---|---|---|
| ladder · rung spacing | 0.24 | 0.211–0.273 |
| ladder · top-out: board at three rungs | 0.72 | 0.549–0.724 |
| boarding · step down · boardDown | ≤ 1.30 | 0.06–1.3 |
| boarding · step up · board, hand on the edge | 0.549–0.724 | 0.549–0.724 |
| boarding · lower step up · boardDown backward | < 0.549 | 0.06–1.3 |
| cleat · reach to the horn | 0.1 | 0–1.012 |
| bollard · reach to the head | 0.555 | 0–1.012 |
| fish-cleaning table · knife | 0.72 | 0.598–0.76 |
| fish-cleaning table · bench | 0.72 | 0.643–0.853 |
| dock box · load | 0.85 | 0.718–0.978 |
| wharf davit + hauler · warp | 0.52 | 0.4–0.632 |
| life-ring post · rest | 0.95 | 0–1.012 |
| service pedestal · rest | 0.95 | 0–1.012 |
| boarding step · rail | 0.6 | 0.549–0.724 |
| gangway · slope at chart datum | 18.43 | 0–18.43 |

**Boarding, measured.** The world fit gives 0.549–0.724 m for `board` and `boardDown` together. Measured separately with the rig itself (`evalClip`, the same 5 mm gate) over all 375 creator bodies: `boardDown` grips nothing and fits every body at every railZ in its clamp, 0.06–1.30 m; `board` puts the left hand on the edge it steps onto and fits every body only in 0.549–0.724 m (per body from 0.387–0.549 up to 0.724–1.117: below, the tall adults cannot reach down to the edge; above, the short youths cannot reach up). A float berth is a 0.3–0.45 m step, so pass 2's first cut, which boarded with `board` at the true step, failed every preset stepping off a boat. `WharfVerbs2.stepPlan(dz)` now gives:

| step | clips |
|---|---|
| under 0.06 m | walk |
| down, to 1.30 m | `boardDown` at the step |
| up, 0.549–0.724 m | `board` at the step, the hand on the edge |
| up, under 0.549 m | `boardDown` played backward (the same frames reversed, so the same fit): no grip. `stepUp` is asked of the rig |
| up onto a hull, 0.724–1.324 m | the boarding step (0.60, `board`), then the rest by the rule above |
| up off a hull, 0.724–1.30 m | `boardDown` backward; the berth's ladder is offered as `alt` |
| more than 1.30 m | the berth's ladder (`climbDown` / `climbUp`), else no way at this tide |

Every BOARD entry carries `aboard` and `ashore`, and every step at every berth in every sidecar, at five tides, fits every creator body (`checks/out/fits.txt`).

**Ladders.** Rungs 0.24 m (pass 1's 0.30 left 50 of 375 short), 0.45 m between the stiles, the top rung one rung under the deck, the bottom rung 1.0 m under chart datum for a swimmer, grab hoops 1.0 m over the deck and a 0.75 m gap in the bull rail. The figure faces the ladder with its root 0.275 m out from the rung plane and `ladderDown` loops with root motion ±0.436 m/s; the top station is three rungs down, and the top-out is `board` at railZ 0.72 with the root advanced 0.60 m onto the deck. CLIMB gives every rung's height, which are dry at this tide, and whether a swimmer can reach the bottom one.

**Gangways.** 1:3 at chart datum, clear width 1.10 m, rails 1.0 and 0.5 m, treads 0.30 m, walkable to 24°; WALK gives the slope at this tide, the toe, and `toeOnFloat_m` at datum and highest water.

**Verbs in the fishing harbour:** `throwRing` ×3 · `gut` ×2 · `bait` ×2 · `stow` ×3 · `fetch` ×3 · `fish` ×4 · `haulTrap` ×1 · `climbDown` ×3 · `climbUp` ×3 · `swimIn` ×1 · `swimOut` ×1 · `stepAboard` ×7 · `stepAshore` ×7 · `tieUp` ×25 · `castOff` ×25 · `walkGangway` ×2. Each is `{ verb, stand [x, y, z], yaw, rides, seq: [{ clip, once | loop, reverse, opts, rootMotion, advance, until, note, request }], fits }`; `rides` is the float group a figure stands on (take `rockFor` each frame).

**Asked of the character rig** (`CHAR_REQUESTS` in every sidecar):

- **ladderOn / ladderOff** — v9.2 has the ladder loop and no way on or off it at the top. Until then: down: blend 300 ms from idle at the deck spot (0.55 m inboard, back to the water) to ladderDown u 0 at the top station (root 0.435 m out from the face, z = deck − 0.72). Up: board at railZ 0.72, exact for every creator body, with the root advanced 0.60 m onto the deck over the drive (t 0.28–0.88)..
- **ladderToHull** — stepping back off a ladder onto a boat alongside. Until then: stop the loop with the lower foot at the sole, blend 250 ms to idle on the hull, then turn.
- **walkSlope** — gangways (18.4° comfortable, 24° steep) and slipways (to 15°): v9.2 walk plants on a level. Until then: walk at 0.727 m/s along the slope, root z linear hinge → toe; at 18° the feet float at most ±1.5 px.
- **board travel** — board and boardDown end at idle over the start xy; their horizontal travel is not published. Until then: advance the root to the landing over the drive (t 0.28–0.88); the kit gives the landing.
- **treadToLadder** — a swimmer taking the bottom rung. Until then: blend 300 ms from tread at the ladder root to ladderDown reversed.
- **stepUp** — board grips the edge it steps onto, and below 0.549 m the tallest creator bodies cannot reach it: a boat’s sole up onto a float is 0.3–0.5 m. Until then: boardDown played backward, which fits every creator body from 0.06 to 1.30 m (measured on all 375).

## Gameplay sidecar

`WharfRig2Kit.gameplay(key, o)` → `hidden-harbours/wharf-gameplay@2`, metres in the model frame (a module: +x along it, +y its water face; a harbour: +x east, +y north to the sea), +z up from chart datum, 32 px = 1 m. The committed files are at mid tide, frame 0 (modules facing 1, harbours facing 0); anything that moves with the water (BOARD, CLIMB.atTide, BERTHS.afloatNow, FLOAT) is for that tide, and `gameplay()` writes it for any other. Generated, never edited.

Sections: `SPRITE` · `WALK` · `HOLES` · `BLOCKERS` · `CLIMB` · `TIE` · `BOARD` · `BERTHS` · `FENDERS` · `FLOAT` · `SOCKETS` · `WATER` · `SNOW` · `SHADOW` · `SORT` · `CHAR` · `FIXTURES` · `VERBS` · `CHAR_FIT` · `CHAR_REQUESTS`. WALK (decks, the slipway's slope, the gangway, what rides a float) · HOLES (a derelict deck's missing boards) · BLOCKERS (bollards, rails, guide piles, fixtures) · CLIMB · TIE (every cleat, bollard and ring: stand, yaw, `reach` opts, then `haul`) · BOARD · BERTHS · FENDERS · FLOAT (the 16-frame rock per group) · SOCKETS · WATER · SNOW · SHADOW · SORT · CHAR (the v9.2 numbers built to) · FIXTURES · VERBS · CHAR_FIT · CHAR_REQUESTS.

## Sprites

`maps/sprites.json`: cell, pivot and sheet for every preset at every facing. The pivot is the projection of the model origin (the module centre at chart datum): blit at `screen(origin) − pivot`. Every fixed module's cell and every floating module's 4 × 4 loop is within 2048 at every facing (`checks/out/sprites.txt`). Harbours are scenes, not sprites.

## Checks

`node checks/run.js` (Node 18+, no packages) runs them all and rewrites `checks/out/`; `--write` first regenerates the sidecars and `maps/sprites.json`. `checks/wharfChecks.js` also runs in a page or sandbox. I ran them in a browser engine; your Node run is the confirmation, and if a last digit differs there, `--write` regenerates.

| check | what it does | result |
|---|---|---|
| `sidecars` | each committed sidecar is byte for byte what `gameplay()` writes today | 29 of 29 identical |
| `fits` | every ladder, tie, boarding step, fixture and verb against the v9.2 fit, at 0, 25, 50, 75 and 100 % of the tide range | 150 climbs · 750 ties · 200 boardings · 430 fixtures · 2,790 verbs, 0 unfit |
| `gangways` | toe ≥ 0.3 m inside the float deck at both extremes; slope at datum | fishing 7.5 m, toe 1.04 / 1.40 m; cove 6.0 m, 0.50 / 0.76 m |
| `joins` | every joined end meets another module's deck edge within 3 cm at the same deck height or freeboard and group | 10 of 10 |
| `berths` | no hull overlaps another hull, a module or a gangway; every berth floats at some tide | 14 berths, 0 problems |
| `snow` | the nevers, and the cover fractions | 6 cases, 0 problems |
| `sprites` | every preset × facing within 2048; `maps/sprites.json` reproduced | 200 of 200 |
| `sums` | `SHA256SUMS.txt` against every file | — |

## Review renders

    harbour-fishing-1400.png          the fishing harbour, 14:00 clear, mid tide, summer weathered, figures off
    harbour-fishing-snow.png          winter, 10:00 after snowfall, snow cover 80 %, low water
    harbour-cove-golden.png           the Cove wharf, golden hour, autumn, seasoned
    float-rock-frames-1-5-9-13.png    a timber float on the water shader in a 0.85 wind: four frames of the loop
    channels-tallPier.png             one module: lit, unlit, normal, sky AO, sun, under water, snow map, lit in snow, layers
    berths.png                        the berth plans to scale, each face at its own scale with a bar

## Use

    const sky = WeatherSky.at({ time: 16.5, cloud: 0.1, rain: 0, fog: 0, snow: 0, wind: 0.3, gust: 0.4, dir: 1 });
    const fr  = WharfRig2.frame('timberFloat', { dir: 1, tide: 0.9, frame: f, wind: sky.wind, season: 'summer', stage: 'weathered', variant: 0 });
    const rgba = WharfRig2.relight(fr, sky);          // fr.w × fr.h, pivot fr.px, fr.py
    const sh  = WharfRig2.castShadow(fr, sky);        // { x0, y0, w, h, lv }: 1 canopy · 2 partial · 3 full, on the water
    const lace = WharfRig2.contact(fr);               // the waterline for the foam
    const gp  = WharfRig2Kit.gameplay('harbour:fishing', { dir: 0, tide: 0.9 });
    const rock = WharfRig2.rockFor(fr, 1, 90);        // { roll, pitch } for a figure on float group 1 facing +x

## Still open

- The viewer's figures are painted by v9.2's own fixed key, not relit by the sky.
- No hull sits in a berth in the viewer; berths are published and drawn as footprints (GAMEPLAY overlay).
- The commercial quay (144 m) and the tanker jetty are too big for a live stage at 32 px/m; they are in the sidecars and the berth plans.
- Ladder on and off, the step from a ladder to a hull, walking a slope, the boards' horizontal travel and a low step up are blends or reversed clips until the character rig has the clips asked for above.
- A fixed module baked as one frame loses the water-bounce dapple on shaded faces near the water (`relight`'s one per-frame term); do it in the shader from `_height` and the loop phase.
- Relighting is per pixel on the CPU; the viewer's first loop of a harbour takes several seconds.
