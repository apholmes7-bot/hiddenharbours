# Hidden Harbours — Shoreline Finds ISO Rig

Beachcombing decor and pickups: what the tide leaves on the sand. **36 finds from 19 parametric
forms**, in 6 categories, 3 states, 8 lie angles, 3 variants — all generated. Successor to the baked
`Art/Sprites/Shore/Finds/*.png` pack (12 finds), same subjects, nothing hand-drawn.

    shoreFindsRig.js   the rig (plain script, no imports, no build step)

Exposes `globalThis.ShoreFinds`. Viewer page (in-project): `Shoreline Finds Iso.dc.html`.

## Contract

| | |
|---|---|
| scale | 32 px = 1 m |
| camera | ¾ iso from the south, elev 40°; ground foreshorten **Q = 0.72** baked into every shape |
| keyline | 1 px soft `#231d14` — a warm shore keyline, darker than the finds panel ground |
| alpha | binary, no AA |
| cell | tight per find; `cellOf(key)` reports `w, h` and the pivot |
| pivot | `sit` — the ground-contact centre |

These lie **flat**, so there is no facing, only a lie angle: 8 canonical steps (`N NE E SE S SW W NW`
= the object's long axis in the ground plane) so a scatter never repeats. The rotation happens in the
**generator** — ground plane first, then projected — never as a pixel rotate. No resampling, no mush.

## Read scale

A periwinkle is 2.5 cm and a drift log is 1.1 m. Drawn linearly at 32 px/m one of them is 1 px. So
every find declares its **true size in cm** and the pack derives the drawn size from one rule:

    drawn px = 4.7 · cm^0.62      (compressive)

A clam lands on 18 px (the old baked pack drew 18), a periwinkle on 8 px instead of 2, a drift log on
87 px. Relative sizes stay honest and nothing falls under the readability floor. Both numbers — true
and drawn — ship in `report`.

## Forms, not drawings

19 generators cover the 36 finds: `valve · fanValve · spiral · disc · star · carapace · claw · rod ·
root · feather · pouch · shard · bottle · coil · mesh · ball · cluster · cobble · can`.

Each builds a **height field first** — z per pixel — and is lit from that field, so ribs, whorls,
domes and splits are lighting rather than drawn lines. Change a row in `FINDS` and the shading follows.

## States and axes

States are ramp transforms; the structure underneath is identical.

| state | where | look |
|---|---|---|
| `wet` | the tideline | darker, cool cast, a sky glint |
| `dry` | upper beach | as-quarried, matte, sand mottle |
| `bleached` | old wrack, dune | pulled toward bone `#e9e6df`, desaturated |

Axes: `variant` 0–2 (seeded) · `wear` 0–1 (chipped silhouette, softened relief) · `sand` 0–1 (grains
clinging in the contact shadow and along the lee).

## Use the rig

    <script src="shoreFindsRig.js"></script>

    ShoreFinds.render(key, dir, opts)   // opts: { variant:0..2, state:'wet'|'dry'|'bleached',
                                        //         wear:0..1, sand:0..1 }
    // -> { w, h, rgba, anchors, params, report }

    ShoreFinds.list()                   // -> the 36 keys
    ShoreFinds.cellOf(key)              // -> cell size, pivot, long axis, reach
    ShoreFinds.rampsOf(key, state)      // -> the resolved colour ramps
    ShoreFinds.FINDS / CATS / STATES / DIRS / PPU / Q / KEYLINE

Runs in the bake sandbox and in the browser.

### Anchors as data

| anchor | what it is |
|---|---|
| `sit` | ground-contact centre — the pivot |
| `pick` | hand grab point, the crown |
| `catch` | 2–3 outer points where weed and rope snag |

## Gameplay data

Every find carries `trueCm`, `zone`, `rarity` and `stack` — the pile cap, i.e. how many read as a
heap before the shape goes to mush. Zones: `tide` (below the wrack line) · `wrack` (the wrack line
itself) · `upper` (upper beach) · `dune`.

## The 36 finds

`px` is the drawn long axis; `cell` is the measured sprite cell.

| find | cat | true | px | cell | zone | rarity | stack |
|---|---|---|---|---|---|---|---|
| Soft-shell Clam | shell | 9 cm | 18 | 30 × 29 | tide | common | 6 |
| Quahog | shell | 8 cm | 17 | 29 × 28 | tide | common | 5 |
| Blue Mussel | shell | 6 cm | 14 | 25 × 25 | tide | common | 9 |
| Scallop Shell | shell | 9 cm | 18 | 31 × 30 | wrack | often | 4 |
| Oyster Shell | shell | 11 cm | 21 | 33 × 34 | tide | often | 4 |
| Razor Shell | shell | 13 cm | 23 | 36 × 32 | tide | often | 5 |
| Jingle Shell | shell | 4 cm | 11 | 21 × 19 | wrack | often | 8 |
| Periwinkle | shell | 2.5 cm | 8 | 18 × 21 | tide | common | 12 |
| Dogwhelk | shell | 3.5 cm | 10 | 21 × 26 | tide | often | 8 |
| Moon Snail | shell | 6 cm | 14 | 26 × 28 | wrack | scarce | 3 |
| Sand Dollar | remains | 7 cm | 16 | 27 × 24 | wrack | scarce | 4 |
| Green Urchin | remains | 5 cm | 13 | 23 × 25 | tide | scarce | 4 |
| Starfish | remains | 15 cm | 25 | 40 × 36 | tide | scarce | 2 |
| Brittle Star | remains | 13 cm | 23 | 37 × 31 | tide | rare | 2 |
| Crab Moult | remains | 12 cm | 22 | 47 × 44 | wrack | often | 3 |
| Green Crab Shell | remains | 7 cm | 16 | 36 × 34 | wrack | common | 5 |
| Lobster Claw | remains | 19 cm | 29 | 47 × 47 | wrack | scarce | 2 |
| Driftwood | wood | 110 cm | 87 | 128 × 121 | wrack | common | 2 |
| Drift Branch | wood | 70 cm | 65 | 99 × 84 | wrack | common | 3 |
| Drift Root | wood | 55 cm | 56 | 105 × 101 | upper | often | 2 |
| Drift Plank | wood | 85 cm | 74 | 110 × 90 | upper | often | 3 |
| Weathered Bone | wood | 28 cm | 37 | 59 × 54 | upper | scarce | 3 |
| Gull Feather | wood | 20 cm | 30 | 46 × 37 | upper | common | 6 |
| Mermaid's Purse | case | 9 cm | 18 | 34 × 30 | wrack | scarce | 3 |
| Whelk Egg Mass | case | 11 cm | 21 | 39 × 38 | wrack | scarce | 2 |
| Barnacle Cluster | case | 9 cm | 18 | 35 × 33 | tide | common | 4 |
| Wave Cobble | stone | 14 cm | 24 | 37 × 42 | tide | common | 4 |
| Pebble Scatter | stone | 26 cm | 35 | 61 × 53 | tide | common | 3 |
| Sea Coal | stone | 12 cm | 22 | 41 × 37 | wrack | often | 4 |
| Sea Glass | flotsam | 3 cm | 9 | 19 × 20 | wrack | often | 10 |
| Glass Bottle | flotsam | 26 cm | 35 | 57 × 55 | upper | often | 2 |
| Rope Scrap | flotsam | 24 cm | 34 | 66 × 58 | wrack | common | 3 |
| Net Scrap | flotsam | 28 cm | 37 | 53 × 43 | wrack | often | 3 |
| Net Float | flotsam | 11 cm | 21 | 33 × 41 | wrack | often | 4 |
| Trap Lath | flotsam | 62 cm | 61 | 92 × 74 | wrack | common | 4 |
| Rusted Can | flotsam | 12 cm | 22 | 37 × 40 | upper | often | 3 |

## Files

- `shoreFindsRig.js` — the rig. No dependencies.
- `shoreFindsRig.catalogue.json` — the 36 finds with form, true and drawn size, cell, pivot, zone,
  rarity and stack cap, serialised out of the live rig so it cannot drift from the bake.
