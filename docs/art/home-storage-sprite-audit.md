# Home storage sprite audit

Baseline: `1abca5974ce3d16a3257777797bb2547c9ad8d53`.
The rack and locker have **no dedicated sprite to redraw in place**: both reference
the same tinted `Square.png` slice. Replacing that image would also repaint the
shore-power lead and unrelated building fixtures. This audit changes no art.

Paths below are relative to `Assets/_Project/`: **art** = `Art/`, **props** =
`Prefabs/Props/`, **data** = `Data/`. Sizes are PNG canvas / individual sprite cell
in pixels; pivots are normalized from the cell's lower left. Every measured sprite
uses 32 pixels per metre. A dash means no independent sprite or prefab was found.

| Piece | Source and placement | Canvas / cell; pivot | Stand-in and contents |
|---|---|---|---|
| Cottage rod rack (2 rods) | props `Storage/GinnyRodRack.prefab`; art `Sprites/Square.png`; data `Stowage/GinnyRodRack.asset` | 16×16 / 16×16; centre alignment, effective (0.5, 0.5) | Brown rectangle, shared source; one state. Stowed rods are hidden, with no filled-state sprite. |
| Cottage gear locker (6 pieces) | props `Storage/GinnyGearLocker.prefab`; the same `Square.png`; data `Stowage/GinnyGearLocker.asset` | 16×16 / 16×16; same pivot | Green-grey rectangle, shared source; no open/closed states or visible contents. |
| Camper rod rack / locker (4 / 10) | data `Stowage/CamperRodRack.asset`, `CamperGearLocker.asset`; anchors in `Dwellings/Meshes/CamperClipperDwellingMesh.asset` | — | No independent PNG or storage prefab. Plan rows are unpositioned; mesh anchors do not constitute replaceable sprite states. |
| Small-shed rod rack / locker (8 / 12) | data `Stowage/ShedRodRack.asset`, `ShedGearLocker.asset`; unpositioned home-storage rows | — | No separate PNG or prefab. No third shed-storage sprite to replace. |
| Outdoor chest freezer (20 units) | props `Kitchen/GinnyFreezer.prefab`; art `Resources/Appliances/Appliance_chestFreezer.png` | 512×546 / 64×78; (0.5, 17/78) | Existing detailed art; 8 facings × 7 rows: closed, two lid steps, open empty/some/full, near rim. Actual catch is drawn inside when open. |
| Larger camper freezer (40 units) | props `Kitchen/CamperFreezer.prefab`; art `Resources/Appliances/Appliance_chestFreezerLarge.png` | 608×595 / 76×85; (0.5, 20/85) | Existing detailed art, same 56-cell layout. A prefab and a mesh anchor exist; the home-storage plan row is unpositioned. |
| Household fridge | props `Kitchen/Fridge.prefab`; art `Resources/Appliances/Appliance_fridge.png` | 624×344 / 78×86; (0.5, 21/86) | Existing detailed art, 32 cells; not a grey storage stand-in. |
| Camper fridge / pantry | data `Resources/Kitchen/Storage/CamperFridge.asset`, `CamperPantry.asset`; named camper mesh anchors | — | No independent camper storage PNG or prefab; do not treat the generic fridge sheet as a camper-specific replacement. |
| Sea chest | art `Sprites/Interiors/Props/InteriorProp_seaChest.png`; placed by `StPetersInteriors.FurnishingsFor` / `UpperLevelFor` | 288×35 / 36×35; (0.5, 12/35) | Existing detailed art, 8 facings, no contents states. Some uses are stair markers, not catch or gear containers. |
| Dresser / wardrobe | art `Sprites/Interiors/Props/InteriorProp_dresser.png`; placed by the same furnishing routines | 320×49 / 40×49; (0.5, 13/49) | Existing detailed dresser art, 8 facings. The upstairs wardrobe explicitly borrows it (facing 6); this is an additional **shape stand-in**, not a separate grey PNG. No fill or door states. |
| Pantry / pantry locker / cooler | data `Resources/Kitchen/Storage/{Pantry,PantryLocker,Cooler}.asset` | — | Definitions alone are not source art. No separate home tackle-shelf, bait-cooler or shed-bench sprite/prefab found. |

## Source checks and implications

- Both cottage prefabs reference GUID `0c1634d5861312443ac89b42a97625b6`,
  fileID `7482667652216324306` (`Square_0`). The slice serializes a (0, 0)
  pivot but uses alignment 0 (centre); the importer-level pivot is (0.5, 0.5).
- `HomeStoragePlacement.cs:162–197` deliberately constructs both from that same
  image. The rack's art scale is (0.9, 1.4), local height 0.7; the locker's is
  (0.6, 1.8), local height 0.9. Since the square is only 0.5 m wide/high,
  serialized bounds imply 0.45×0.70 m and 0.30×0.90 m art respectively, with
  lower edges 0.35 m and 0.45 m above their roots before any parent transforms.
  This is an arithmetic finding, not a verified in-game placement measurement.
- `StowageRack.cs:160–161` and `:300–305` deactivate stored instances. There is
  no contents renderer on either prefab. Painting rods permanently into the
  empty rack would misrepresent its contents.
- `ChestFreezerSheet.cs:44–59` defines the freezer states;
  `FreezerContentsPresenter.cs:13–17` describes the separate catch layer and rim.
  Existing freezer and dresser art are the comparison references; their palettes
  and pixels are unchanged. All inspected source PNGs are present LFS objects.
- `StPetersInteriors.cs:360–366` explicitly uses the dresser as the wardrobe.
  Redrawing that shared image as a wardrobe would change other rooms' dressers.
  This is the remaining storage stand-in; tackle shelf, bait cooler and shed
  bench searches did not identify another dedicated home-storage image.

The rack, locker and wardrobe therefore need dedicated sprite assets plus an
explicitly scoped reference/placement change before a safe replacement can ship.
The wardrobe also overlaps the active cottage fit-out (draft #1392). A pixel-only
replacement preserving the current source references cannot isolate any of these
three changes. Keep them parked rather than repainting their shared sources.

The audit contact sheets show unchanged before/after panels at 1× and 4× nearest
neighbour on a sample of the committed cottage floor, beside the freezer and
dresser. They reconstruct serialized sprite scale and tint, with integer rounding;
they are not engine plates or proof of lighting, camera scale or final placement.
The freezer is an outdoor reference, included indoors only for comparison. The
rack and locker still read as plain rectangles, and the wardrobe still reads as a
dresser. The images and their provenance stay in review evidence, outside this PR.
