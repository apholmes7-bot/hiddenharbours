# Bird rigs: the birds after the herring gull

Each bird here is a rig source in `docs/art/rigs/`, a sidecar in `docs/art/rigs/gameplay/`, and a sheet with
its contract in `sheets/`. The herring gull (`seagullIsoRig.js`) is not touched by anything in this folder.

| Bird | Rig | Global | Sidecar | Sheet |
|---|---|---|---|---|
| Double-crested cormorant | `cormorantIsoRig.js` | `CormorantIso` | `gameplay/cormorantIsoRig.gameplay.json` | `birds/sheets/Cormorant.png` (2688 x 512, 42 columns x 8 facings) |

## Build and check (no Unity)

```
node docs/art/rigs/birds/tools/build.cjs                 # check; the last line is RESULT: PASS or RESULT: FAIL
node docs/art/rigs/birds/tools/build.cjs --write         # regenerate sidecars, sheets and contracts, then check
node docs/art/rigs/birds/tools/build.cjs --plates <dir>  # also write the preview plates
```

Node 18 or later, built-ins only. The check holds: the sidecar and the contract are what the rig generates
now; the rig is LF; every state has frames, motion, a way in and a way out; every cell holds a bird and none
is clipped by its cell; the open wing measures the declared span.

The sheet is drawn by the rig's own `render()`, which is the call the in-engine baker makes. The control:
`--gull-rgba <file>` draws the herring gull on main through this same route, and that output matched the
shipped `Seagull.png` pixel for pixel (3072 x 512, 0 differing pixels) when this kit was made.

## What is the same as the gull, and what is not

The same: the turntable (8 facings, 45 degree steps, CLOCKWISE, elevation 40), 32 px to the metre at strict
world scale, the 64 x 64 cell with its pivot at (32, 46) as a contact point, the painter (ordered dither,
depth-edge darkening, no keyline), the wet bake under `waterZ`, the sheet layout (row = facing, column =
`sheetOrder()`), and the shape of the export (`W, H, pivot, BODY, ANIMS, AORDER, MOTION, POSE, MATS, resolve,
render, anchors, project, sheetOrder, dirOf, gameplayGeometry`).

Not the same:

- **The cormorant is its own body, not a gull preset.** Its neck is a chain bent by two angles, its legs are
  resolved in the contact frame so they stay plumb under a body that stands nearly upright, and its tail
  bends at the root to prop on the rock. A gull body has none of the three.
- **Its states.** `fly, glide, dive, surface, splash, float, patter, land, stand, dry, perch, takeoff`. The
  names are the gull's where the slot means the same thing. New: `surface`, `patter`, `dry`. Not drawn:
  `swoop, preen, peck, walk`.
- **`dive` starts from `float`,** not from the air, and between `dive` and `surface` the bird is not drawn.
  The sidecar's `UNDER` section gives the spell and the distance; `under` is a state with no frames.
- **It cannot jump off the water.** `float -> patter -> fly`; `patter` is a loop the game holds.
- **No `flock()`.** The sidecar's `GROUP` section gives its company as numbers.
- **No skinned export** (`skeleton`, `bindMesh`, `clips`). It ships on the sprite-sheet route the gull is on.

## Sizes and marks

The rig draws 0.82 m bill to tail and a 1.20 m wingspan, inside the brief's ranges (0.70 to 0.90 m, 1.14 to
1.23 m). The marks are the breeding adult's: black with bronze-brown upperwing coverts, orange-yellow throat
skin, a dusky hooked bill, an aquamarine eye, black legs. These are from general knowledge of the species and
are NOT yet confirmed against a field guide by title, edition and page, which the brief asks for.

## After this merges

The game side has no baker, slicer or species asset for a second bird yet. The sheet in `sheets/` is the
art, at the path this lane owns; the game lane copies or re-bakes it to where the game reads sprites.
