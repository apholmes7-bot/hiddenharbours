# Wharf fender kit

Fender strings and the Nine Mile Creek ladder, drawn by recipe at 32 px/m and 40 degrees like every
other metric rig.

## What a string is

One vertical **fender pile**: a creosoted timber 0.25 m wide and 0.15 m deep, bolted to the face from
the deck lip down to half a metre under spring low. A galvanised chain runs down its face from a head
plate under the lip, and 0.60 m tyres are lashed flat against it across the **rub band**, from spring
low water to spring high water plus a metre of freeboard (capped just under the deck). Pitch is at
most 1.6 diameters, so the band is never bare for more than half a tyre.

Everything below spring high water is drawn wet; everything below spring low plus 0.7 m carries weed.
No water is painted: the scene cuts each picture at the live tide with `TidalFaceWaterline`, so the
lower tyres go under at high water and come out at low.

The string stands 0.35 m proud of the face, inside the 0.40 m a hull keeps from the wall when it
lies alongside (`MAX_STANDOUT`).

## Pieces (`recipes.json`)

| id | deck (m) | for |
|----|----------|-----|
| `prop.wharf_fender_string_stp` | 5.354004 | St Peters pier, south face |
| `prop.wharf_fender_string_nmc` | 3.4 | Nine Mile Creek quay faces |
| `prop.wharf_ladder_nmc` | 3.4, 6.2 long | Nine Mile Creek wharf ladders (the St Peters dock-props ladder recipe) |

Water bounds are the region's tide (`TideMeanLevel` +/- `TideAmplitude`); `check.cjs` fails if they
drift apart.

## Where they go

Placement is data: one `WharfFenderRunDef` per face under `Assets/_Project/Data/Regions/Fenders/`
names the hanging x positions, the lip and the art. The scene steps read it; nothing here places.

## Bake

`HiddenHarbours.Tools.RigBaking.WharfFenderBaker.BakeFromCommandLine` (batchmode, with
`-wharfFenderResult <file>`) renders every piece in all eight facings, writes the sheets and height
maps under `Assets/_Project/Art/Sprites/Wharf/Fenders`, and builds one `SetPieceDef` per piece.

`node docs/art/rigs/wharf-fender-kit/check.cjs` from the repository root checks the contract.
