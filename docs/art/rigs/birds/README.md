# Bird rigs: the birds after the herring gull

Each bird here is:

- a rig source in `docs/art/rigs/` (one file, one global);
- a sidecar in `docs/art/rigs/gameplay/`, declaring `hidden-harbours/creature-gameplay@1`;
- a sheet and its contract in `sheets/`;
- one `<name>.bird.json` in this folder, which is how the build finds it;
- one `<name>.md` in this folder: what that bird is, its states, and what is not verified.

The herring gull (`seagullIsoRig.js`) is not touched by anything in this folder.

## Build and check (no Unity)

```
node docs/art/rigs/birds/tools/build.cjs                 # check; the last line is RESULT: PASS or RESULT: FAIL
node docs/art/rigs/birds/tools/build.cjs --write         # regenerate sidecars, sheets and contracts, then check
node docs/art/rigs/birds/tools/build.cjs --plates <dir>  # also write the preview plates
```

Node 18 or later, built-ins only. For every bird the check holds: the sidecar and the contract are what the
rig generates now; the rig is LF; every state has frames, motion, a way in and a way out; every cell holds a
bird and none is clipped by its cell; the open wing measures the declared span.

The sheet is drawn by the rig's own `render()`, which is the call the in-engine baker makes. The control:
`--gull-rgba <file>` draws the herring gull on main through this same route, and that output matched the
shipped `Seagull.png` pixel for pixel (3072 x 512, 0 differing pixels) when this kit was made.

## What every bird here shares with the gull

The turntable (8 facings, 45 degree steps, CLOCKWISE, elevation 40), 32 px to the metre at strict world
scale, a pivot that is a contact point, the painter (ordered dither, depth-edge darkening, no keyline), the
wet bake under `waterZ`, the sheet layout (row = facing, column = `sheetOrder()`), and the shape of the
export (`W, H, pivot, BODY, ANIMS, AORDER, MOTION, POSE, MATS, resolve, render, anchors, project,
sheetOrder, dirOf, gameplayGeometry`). State names are the gull's where the slot means the same thing.

## What none of them has yet

- **No skinned export** (`skeleton`, `bindMesh`, `clips`). They ship on the sprite-sheet route the gull is on.
- **No game side.** There is no baker, slicer, reader or species asset for a second bird. The sheet in
  `sheets/` is the art, at the path this lane owns; the game lane copies or re-bakes it to where the game
  reads sprites.
- **No field-guide citation.** Sizes are inside the brief's ranges and marks are from general knowledge of
  each species. The brief asks for title, edition and page; that is still owed.
