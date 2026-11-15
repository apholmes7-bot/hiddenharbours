# Double-crested cormorant

| Rig | Global | Sidecar | Sheet |
|---|---|---|---|
| `cormorantIsoRig.js` | `CormorantIso` | `gameplay/cormorantIsoRig.gameplay.json` | `birds/sheets/Cormorant.png` (2688 x 512: 42 columns x 8 facings, cell 64 x 64, pivot 32, 46) |

It draws 0.82 m bill to tail and a 1.20 m wingspan (the brief: 0.70 to 0.90 m, 1.14 to 1.23 m). Breeding
adult: black, bronze-brown upperwing coverts, orange-yellow throat skin, dusky hooked bill, aquamarine eye,
black legs. Proposed species id: `bird.double_crested_cormorant`.

## Its own body, not a gull preset

- Its neck is a chain bent by two angles.
- Its legs are resolved in the contact frame, so they stay plumb under a body that stands nearly upright.
- Its tail bends at the root, to prop on the rock.

## States

`fly, glide, dive, surface, splash, float, patter, land, stand, dry, perch, takeoff`.

- New beside the gull: `surface`, `patter`, `dry`. Not drawn: `swoop, preen, peck, walk`.
- `dry` is the one that makes it a cormorant: upright on a rock or piling, wings held half open.
- `dive` starts from `float`, not from the air. Between `dive` and `surface` the bird is not drawn: `under`
  is a state with no frames, and the sidecar's `UNDER` section gives the spell and the distance.
- It cannot jump off the water: `float -> patter -> fly`, and `patter` is a loop the game holds.
- `float` rides low: the body centre is under the surface and the wet bake tints what is below it.
- No `flock()`: the sidecar's `GROUP` section gives its company as numbers.
