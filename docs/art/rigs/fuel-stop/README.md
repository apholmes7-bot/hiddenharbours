# Fuel stop export and sheets

The four delivered files are pinned unchanged by FuelStopKitIntakeTests. The H9 sibling
`fuelStopGameplay.js` adds the 17-kind export contract. It reads the rig's station and
footprint APIs; anchors and standing points turn by pi into SetPieceDef's model frame.
The authored composition at dir 4 therefore remains unchanged.

Run from the repository root, with Node 24:

```
node docs/art/rigs/fuel-stop/checks/check-kit.cjs
node docs/art/rigs/fuel-stop/checks/write-today.cjs Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/fuelStop.today.json
node docs/art/rigs/fuel-stop/checks/bake-headless.cjs Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/fuelStop.today.json <output-directory>
```

The driver serves the repository on loopback and launches headless Chrome with its own profile.
`H9_BROWSER` can select another Chromium executable. The page calls FuelStop.render directly;
the rig supplies its own pixel font. Canvas only assembles and encodes the returned RGBA.
Frames include their pivots and ink, in clockwise order (rig dirs 0,7,6,5,4,3,2,1), packed
under the existing 4096 import cap. The contract uses SetPieceSheetSlicer's field names.

One Def represents a kind. The seven shared kinds have stable prop ids; row ids identify
instances. barSign has no row and is retained as an unused kind. No Data asset or scene is
written by these scripts. The today writer refreshes gameplay sidecars with the source hash.

Phase B imports and slices the sheets and builds Defs on the existing route. It also authors
door and island collision geometry: the exported footprint is a bounding polygon, not a
walk blocker for a canopy lane or an enterable store. Empty colliders must not be mistaken
for completed scene collision. Light metadata identifies emitters; the three-state play
proof and runtime lighting remain Phase B work. The named scene patch consumes
NineMileCreekFuelStopPlan's validated 39 placements. The two gull perches use their own route.

The ruled service anchors are prices_read at the pylon base, buy_propane at the propane row,
and buy_ice at the ice box. SUPREME serves mixed; KERO serves stove_oil. DEF is drawn only.
No new stock, fee or price is introduced here.
