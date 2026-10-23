# HH-road-path-kit-v4-2026-09-30 — road kit v4

Part 3 of `briefs/BRIEF-2026-09-29-nmc-key-scenes.md` (design-desk): the owner put it in on 09-29 ("Yes part 3 add on"), and the
game's road work for Nine Mile Creek waits on it (09-28: "wait for road rig 4"). The gas bar loads `roadPathRig4.js` from this kit.
It comes after part 1 (`HH-nmc-ground-part-1-2026-09-30/`) and before part 2.

## Files
    README.md                         this: every change from v3, old and new
    roadPathRig4.js                   the kit -> globalThis.RoadKit4 (plain JS, no build step)
    roadScenes4.js                    the four review scenes -> globalThis.RoadScenes4
    lib/pixelLanguage.js pxKit.js pxKit2.js weatherSky.js terrainLight4.js pxKit8.js terrainLight5.js
                                      what the kit paints and lights with, from pixel-terrain-kit-v9, unmodified (the terrain lane's pass 9 PR 2 bytes)
    gameplay/roadPathRig4.classes.gameplay.json      every class's sidecar (one sample route each)
    gameplay/roadScenes4.<scene>.gameplay.json       each review scene's sidecar: stpeters, route91, wharfroad, finnigans
    renders/*.png                     road kit v4 in the game's framing, from part 1's views (TerrainLight6, 32 px/m)
    tools/writeGameplay.cjs           Node 18: writes every sidecar from the kit
    check-road-kit-v4.cjs             the checker (Node 18, built-ins only; last line RESULT:)
    SHA256SUMS.txt

## v3's names stay
v3's files keep their names and the globals they install, byte for byte, and this kit does not touch them: the game's
`docs/art/rigs/road-path-kit-v3/roadPathRig3.js` (sha256 `d45e9ac657eb42daf5dc22f61068bf0ff1f0ee65152d6a4ca827a77ef0a5ee3c`,
`globalThis.RoadKit3`), `road.contract.json`, `IMPORT.md` and the `RoadIso3_*_new_blob47.png` atlases the RoadsV3 tilemaps read. v4 is a
new file with a new global, `roadPathRig4.js` -> `RoadKit4`, and `roadScenes4.js` -> `RoadScenes4`.

**What the game's bake reads.** Its road bake reads v3 (the blob-47 atlases through `RoadKitV3` and `RoadKitContract`), and nothing of v4
until a consumer change: v4 paints routes into the terrain's own floor, so it needs the terrain-chunk path (the four maps below), not a
tilemap. v4 has no `const RIG = {` / `id:spec.id,` anchor and no `matsFor('gelcoat').MATS`: it is not a mesh rig.

## Every change from v3, old and new
| | v3 (2026-08, in the game) | v4 |
|---|---|---|
| file, global | `roadPathRig3.js`, `RoadKit3` | `roadPathRig4.js`, `RoadKit4` (v3's kept, untouched) |
| what it draws on | its own 32 × 64 px blob-47 cells, blitted over the grass (head 10 rows, skirt 42) | the pass 9 floor itself: a route is painted into the terrain tile it crosses |
| geometry | a 1 m grid; a mask per cell (47 blobs); `renderFree` a coverage brush | smoothed polylines (centripetal Catmull–Rom, resampled every 3 px); every texel has s (along), d (across), e (from the edge) |
| junctions | tile masks; `abut` edges share a kerb face | a round union per group: kerb returns are circular fillets (2.2 m in town), bands wrap them |
| light | baked once, a fixed upper-left key, 6-step ramps, a 4 × 4 Bayer dither | structure only (palette, band, height, mark); TerrainLight5 relights it live with the ground; no dither, binary alpha |
| faces | fixed shade, a baked alpha contact band | a face G-buffer with its own normals, relit by the same relight; contact from sky visibility |
| patterns | phased on world metres (`gy` north) | laid in (s, d), so planks, bricks, setts and dashes follow the curve |
| surfaces | 11: dirt, oiledDirt, gravel, shell, sand, concrete, apron, asphalt, cobble, brick, boardwalk | trodden, lane, gravel, shoulder, asphalt, slab, kerb, gutter, brick, setts, shell, stones, planks, deck (`SURF`), each with a foot sound and a grip; the soft ones are the terrain kit's own materials |
| wear | 3 states: new, worn, cracked | a wear number per route (0..1): tracks, washboard, potholes, patches, paint worn off |
| profiles → classes | `path` flush, `sidewalk` +125 mm on a kerb run, `lane` edge-lined, `road2` centre-lined | the municipal plan's classes (§2.2), below |
| heights | 24.5 px = 1 m; the sidewalk +0.125; a 50 mm camber; the quay apron's 16 px face (0.65 m) | 24.5 px = 1 m (unchanged); kerb +0.14, walk +0.125; camber 5–7 cm per class; route +0.04; boardwalk deck +0.35 |
| weather | none | wet by class; water in ruts, potholes, gutters and ditches; rain rings; snow by shape; plough, shovel, trodden |
| palette | 6-step ramps, harbour-master ramps | PxLang hue-true 5 bands via `PxKit8.pal`, classes registered for rain and snow |
| API | `render(surface, opts)`, `renderFree`, `renderGround`, `gameplay(surface, opts)`, `BLOB47`, `canon`, `fromMask`, `topPoly`, `PAL`, `MATS`; constants `TILE 32`, `CELL_H 64`, `HEAD 10`, `SKIRT 42`, `S 32`, `ZS 24.5` | `network(routes, {w, h, seed})`, `zones`, `paint`, `patchG`, `faces`, `relightFaces`, `composite`, `weather`, `gameplay(net, {scene})`, `lift`, `spline`, `roleView`, `kapAt`, `unionRound`; `CLASSES`, `SURF`, `PALS`, `ROLES`; constants `PPM 32`, `ZS 24.5` |
| gameplay sidecar | walk / drive / kerb-blocker per surface, at v3's heights | `hidden-harbours/road-gameplay@1` per route: centreline in metres (0.5 m steps), class and lane class, surface, foot sound, grip, section bands with heights, kerb and deck data, junction and crossing intervals, stop bars, where water stands, how snow is cleared |
| engine contract | 12 × 4 atlases of 32 × 64 cells, blitted in two passes (N→S, then skirts) | the floor ships as the terrain's four maps with the routes in (`_unlit _normal _light _detail`, TerrainLight5's encodings); faces as the same four maps on the screen; relight both; import point-filtered, uncompressed, no mips at 1:1, sRGB off on `_normal _light _detail` |

### The classes (new in v4; `paved` new in this issue)
| class | carriage | bands outward | where |
|---|---|---|---|
| footpath | 1.2 m trodden earth, sunk 2.5 cm, width wanders | — | St Peters, Nine Mile Creek (the shore path, the gully path, the anglers' trail) |
| alley | 1.0 m, patchy | — | St Peters (leaves only) |
| walk | 1.3 m shell · stones · planks (+0.07) | — | St Peters (stones, planks); Nine Mile Creek (shell) |
| lane | 2.6 m earth, ruts at 0.66 m, a grass crown | — | St Peters; every yard and drive; Nine Mile Creek's creek lanes, Shore Lane, the campground's loops (gravel) |
| street | 5.0 m gravel, 7 cm camber, tracks, washboard, potholes | shoulder 0.45 · ditch 1.4 (V 28 cm) | Nine Mile Creek: the bar road, Mill Road. **Was: "Wharf Road, the bar road".** |
| **paved** | **5.6 m asphalt at +0.04, 5 cm camber, auto centre line, edge lines** | **shoulder 0.5 · ditch 1.4 (V 30 cm)** | **Nine Mile Creek: Wharf Road (ruled paved 09-29), Water Street. New.** Group `route`, so it meets Route 91 in a round union |
| route | 5.8 m asphalt at +0.04, auto centre line, edge lines | shoulder 0.55 · ditch 1.45 (V 34 cm) | Nine Mile Creek (Route 91) |
| town | 5.6 m asphalt, dashed centre, fillet 2.2 m | gutter 0.30 · kerb 0.16 at +0.14 · walk 1.5 at +0.125 · verge 0.7 ramp | Finnigan's Landing only |
| boardwalk | 1.5 m deck at +0.35 on piles, flat ends | — | Nine Mile Creek: the dunes, the marsh crossings; the town's river boardwalks (their deck at +3.0 over a +2.7 bench) |

### Changed in this issue (2026-09-30), old and new
| what | 2026-09-26 (as the gas bar loaded it) | 2026-09-30 |
|---|---|---|
| `CLASSES.paved` | — | new (above) |
| `CLASSES.street.where` | "Nine Mile Creek (Wharf Road, the bar road)" | "Nine Mile Creek (the bar road, Mill Road)" |
| review scene `wharfroad`: `nmc.wharf_road` | `cls: 'street'` (gravel) | `cls: 'paved'` (asphalt) |
| the header line | "(road / path kit v4, 2026-09-26)" | "(road / path kit v4, 2026-09-26; the paved street added 2026-09-30)" |
Nothing else moved: every other class, surface and band draws as before, so the gas bar's frames stand (it draws Wharf Road as `route`).

## A route
    { id: 'nmc.wharf_road', cls: 'paved', w: 5.6, pts: [[x, y], ...] }
Scene px, 32 px = 1 m, y down = south. A point's optional third value is its width in metres. Any class field can be overridden per route
(`surf`, `w`, `bands`, `wear`, `marks`, `lift`, `zone`, `crown`, `ruts`, `snow`, `ends: 'flat'`, `crossings: [s metres]`).

## Pipeline
    net = RoadKit4.network(routes, {w, h, seed}); RoadKit4.zones(net, zone, zi); fl = PxKit8.floor(w, h, defs, seed); RoadKit4.paint(fl.tile, net)
    G = TerrainLight5.gbuf(fl.tile, {relief, wrap: false, far}); RoadKit4.patchG(G, net); RoadKit4.faces(net, fl.tile, G)
    per sky: lit = TerrainLight5.relight(G, sky, {lv, frame}); RoadKit4.weather(G, net, sky, lit, {lv, frame}); fl = RoadKit4.relightFaces(net, sky, {lv, frame}); out = RoadKit4.composite(net, lit, fl)
Part 1's views paint the plan's roads with it this way inside the St Peters panorama (`afterPaint`), on TerrainLight6.

## Renders
`renders/` holds three of part 1's views, which paint Nine Mile Creek's roads with this kit on the pass 9 floor, lit by TerrainLight6:
Route 91 at the north edge (`route`, with its centre and edge lines), the bar road and the shore path at the landing (`street`,
`footpath`), and the town's south boardwalk (`boardwalk`). The four review scenes render in `Road Path Iso v4.dc.html` in the project.

## Still open
- The game's RoadsV3 tilemaps read blob-47 cells; v4 needs the terrain-chunk path. A consumer change.
- Sections are symmetric per route. A group's round union keeps two routes per texel; a third at one meeting resolves by distance.
- The plough clears the carriageway but builds no windrow height. Sprites shade the foot of a face, not the face.
- Flat utility lids still need plan decals in utilityIsoRig (v3's open debt).
- A bridge is not a road class: Route 91's bridge over the town river is its own artwork (commissioned 08-20); the kit paints up to its abutments.
