# Gas station iso rig — export

Pixel-art isometric gas station kit and its gameplay sidecar. Everything here is generated; nothing in
`gameplay/` is hand-edited.

    rig/isoSolid.js              rasteriser — load FIRST
    rig/fuelRig.js               grade table (FuelIso) — gas, diesel, mixed, oil
    rig/gasStationRig.js         StationIso — forecourt, canopies, signs, storefront shell
    rig/stationInteriorRig.js    StationInterior — sales floor, painted into the shell's own cell
    gameplay/gasStationRig.gameplay.json
    gameplay/README.station.md   the sidecar contract, in full
    tools/_sidecarExport.js      the hash stamper (re-bake tooling)

**Load order is isoSolid → fuelRig → gasStationRig → stationInteriorRig.** Each throws if the one before
it is missing, so a wrong order fails loudly rather than drawing something subtly wrong. Plain scripts,
no modules, no build step; each exposes one global.

## Rendering

    StationIso.render(type, size, dir, opts)     // one piece, one facing
    StationIso.cell(type, size, opts)            // quad and pivot for that piece
    StationIso.plan(cfg)                         // lay a forecourt out in metres
    StationIso.sortPieces(list, dir)             // far-to-near on the rasteriser's own depth
    StationIso.serve(outlet, target)             // hose polyline + honest metres (never baked)
    StationIso.ticket(size, grade, litres)       // litres, money, minutes off one price table

32 px = 1 m, 3/4 view, 45° steps, elev 40° — shared with the rest of the set. 21 pieces:
dispenser ×7, island ×4, canopy ×3, sign ×3, store ×2, fillport ×2.

## The sidecar

`gameplay/gasStationRig.gameplay.json`, regenerate with `StationIso.gameplayAll({ grades })`. Metres,
heading-independent, origin at the ground centre of each piece's own footprint, +z up. Sections per
piece: `WALK`, `SOLE`, `INTERACT`, `BLOCKERS`, `HOSES`, `SLOTS`, `CLEAR`, `THRESHOLD`, `SERVICE_DOOR`,
`ROOF`, `CAPS`, `ROWS`, plus `_excluded` naming what is deliberately absent.

Every `reach_point` is **tested**, not requested: 45 checked, 42 as written, 3 repaired,
0 published as null. Body radius 0.22 m, grasp 1.2 m, read 1.9 m, vertical 2.1 m.
Scope is one piece — a dispenser is not told the island is under it, so a laid-out forecourt still needs
a whole-scene pass after `plan()`.

## Stamps

    derivedFromRigSha256          3700747cd2ab7a0ebc40eb31ff88b9e23978266ed447317a5ef55a7ad23c7b5d
    interiorDerivedFromRigSha256  42542b72f66b36a4b9bf8037ce4a8330a17b1f2f6fcb783feec984611139930c

SHA-256 of `rig/gasStationRig.js` and `rig/stationInteriorRig.js` as served. If either file is edited,
re-bake the sidecar rather than editing the JSON — a stamp that no longer matches its rig is the defect
the field exists to catch. `tools/_sidecarExport.js` does the hashing.
