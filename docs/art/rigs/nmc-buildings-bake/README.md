# Nine Mile Creek building bakes: the fish plant and the community hall

Exterior and walk-in sprites for the two greybox shells that `NmcBuildingShells` (PR 1276) places. This folder only
bakes them. It writes no prefab, no scene and no Def. Wiring them onto the shells is a small later step, done after
PR 1276 merges, and is described below.

## Files

| File | What it is |
|---|---|
| `../nmc-fish-plant-kit/nmcFishPlant.js` | The fish plant model (#1203), baked exactly as drawn. |
| `../nmc-community-hall-kit/nmcCommunityHall.js` | The parish hall model (new). It draws through the plant kit's rasteriser and light, using the set pieces kit's ramps. |
| `bake.cjs` | Writes the sprites below, their `.meta` files and one sidecar per building. A re-bake keeps the GUIDs and sprite ids. |
| `contact-sheet.cjs` | The review sheet: 1x and 2x, roof on and roof hidden, beside the wharf cold store. It also draws massing proposals for the two wholesalers. These are review props only, not kit pieces. |

Run from the repository root (Node 18+, built-ins only; the sheet also needs Chrome):

```
node docs/art/rigs/nmc-buildings-bake/bake.cjs
node docs/art/rigs/nmc-buildings-bake/contact-sheet.cjs <outDir> <path to the real NmcWharf_cold_store.png>
```

Output, in `Assets/_Project/Art/Sprites/Buildings/NineMileCreek/`:

| Sprite | Size (px) | Pivot (px from top-left) | Goes on |
|---|---|---|---|
| `NmcFishPlant_exterior` | 1653 x 788 | 785, 471 | the fish plant's `Shell` |
| `NmcFishPlant_cutaway` | 1549 x 619 | 773, 319 | the fish plant's `Interior` |
| `NmcCommunityHall_exterior` | 463 x 581 | 250, 386 | the hall's `Shell` |
| `NmcCommunityHall_cutaway` | 447 x 423 | 250, 228 | the hall's `Interior` |

Import settings: PPU 32, point filter, no compression, no mipmaps. Each sprite's pivot is the building's centre on the
ground, which is the prefab root, so it sits at local (0, 0). Both buildings are baked under the sky every Nine Mile
Creek sheet uses (14:00, cloud 0.1, wind 0.3), at camera dir 4.

## Mapping onto PR 1276's shells

Both prefabs are `Assets/_Project/Prefabs/Buildings/NineMileCreek/<Name>.prefab`. Each root carries a
`BuildingInterior` whose `_shell` is the `Shell` child's SpriteRenderer and whose room is the `Interior` child's.
`BuildingInterior` hides the shell while the player is inside (ADR 0036) and shows the room in its place.

For each building:

1. **`Shell`**
   - Set the SpriteRenderer's sprite to `<Name>_exterior`. Reset its colour to white; the greybox tints it.
   - Set localPosition to (0, 0, 0) and localScale to (1, 1, 1); the greybox stretches `Square.png`.
   - Set `YSortSprite.SortPivotYOffset` to the sidecar's `sortPivotYOffsetUnits`. That is the south wall's foot, where
     the greybox sorts today: fish plant -9.000, hall -5.785.
   - Add `SpriteShadow`. Its pivot is the building centre, as on the wharf buildings.
2. **`Interior`**
   - Set the sprite to `<Name>_cutaway`, colour white, position (0, 0, 0), scale (1, 1, 1).
   - Keep `sortingOrder = InteriorCatalog.RoomSortingOrder`.
   - The `Furniture` zone tints overlap the art's floors. Turn the `Zone_*` renderers off, or delete them.
   - Keep the greybox blocks (machines, gym equipment): the cutaway draws floors and partitions only.
3. **Leave alone:** footprint, colliders (`BuildWalls`, `InnerWalls`), door position, `Outside` set pieces, scene
   patch. Every footprint, door and zone in the art was drawn from the shell's numbers:

| | Fish plant | Community hall |
|---|---|---|
| Centre (world) | (-265, -40) | (-153, 62.5) |
| Footprint | 48 m east-west x 28 m north-south | 12 m east-west x 18 m north-south (the shell's facing 2) |
| Door | South wall, world (-245, -49). The half-glazed door under the red porch roof, with the blank name board on the eave above it. | West wall, 0.5 m north of centre, world (-159, 62.82). Under the gabled entry hood, with the stoop, two steps and the door lamp. Square on to the lot's west gate. |
| Landing / back side | East gable: intake I1 and I2 roller doors and the outbound door under the canopy; the ice plant penthouse and chute over the east end | The gym in the north-east, with its doorway at 5 m north in the back wall. The kitchen and washroom stay closed. |

The best place to make this change is a step in `NmcBuildingShells.BuildPrefab` that loads the sprites by path, so a
prefab rebuild keeps them.

## Known limits

- At this camera, east and west walls are seen edge on. The plant's intake doors and the hall's front door read only
  from what sticks out of the wall.
- The roof-hidden sprites are floor plans, not furnished rooms. The plant's south doors are still drawn shut in its cut.
- The cut walls sit on the room's sorting order, so a partition never draws over the player.
