# Remaining square-sprite stand-ins

Snapshot: `1abca5974ce3d16a3257777797bb2547c9ad8d53`. Searching every prefab
under `Assets/` for the shared `Square.png` identity
(`0c1634d5861312443ac89b42a97625b6`) finds **seven prefabs and 111 direct
SpriteRenderer references**. Counts include shells, floor zones and walls;
they are serialized references, not a count of visible objects in a room.

The indoor rod rack and gear locker account for one reference each. Dedicated
sprites and isolated wiring are proposed in draft #1408
and #1409. Those drafts
are independent of this snapshot and still need picture review. The remaining
**five prefabs / 109 references** are below, ordered by player readability and
the size of a useful, isolated art change.

| Priority | Prefab | References | What should replace the stand-ins |
|---|---|---:|---|
| 1 | `Props/Storage/CamperShoreLead.prefab` | 3 | The `Run0`, `Run1`, `Run2` yellow strips should become a grounded insulated power cable with readable bends and connected plug ends. Preserve the route and connection points; review contact with the ground along the whole run. |
| 2 | `Buildings/NineMileCreek/NmcFishingGearWholesaler.prefab` | 24 | Start with `RopeCoils`, `NetBales`, `PotStack`, `FloorPallets`, `PalletRackWest`, `PalletRackNorth`, `TackleGondola`, `WallShelfEast` and `TradeCounter`: distinct coils, tied net bales, stacked pots, pallets, stocked racks and a service counter. Handle shell, interior, zone fills, walls and doorstep in the room-art pass. |
| 3 | `Buildings/NineMileCreek/NmcBoatGearWholesaler.prefab` | 16 | Start with `ElectronicsCase`, `StockPallet`, `StockShelving`, `ShelfGondola`, `WallShelfWest` and `TradeCounter`: glass-front marine electronics, boxed stock on a pallet, shelving and a counter. Resolve the remaining shell, floor zones, walls and doorstep with the room art. |
| 4 | `Buildings/NineMileCreek/NmcCommunityHall.prefab` | 22 | Give the seven `gym_equipment.*` props their distinct exercise silhouettes, then the `GymBench` and `HallStage` their own bench and stage art. The seven are pull-up bar, rowing machine, bench press, grip trainer, skipping rope, treadmill and squat rack. Coordinate the shell, zones, partitions and doorstep as a separate room pass. |
| 5 | `Buildings/NineMileCreek/NmcFishPlant.prefab` | 44 | After the active prefab work settles, prioritize `IntakeTotes`, `IntakeScale`, `GradingTable`, `DispatchPallets` and `DryStoreRacks`: totes, a weighing station, grading bench, loaded pallets and stocked racks. Then address fillet/packing tables, canning equipment, cold-store doors, rails, reception and the room surfaces. This prefab belongs to another open change; this note grants no edit scope. |

Prefab paths above are relative to `Assets/_Project/Prefabs/`. These are future
art assignments, not additions to gameplay scope. A floor-zone rectangle is
part of room surfacing, not a storage item; do not commission 109 independent
prop sprites from the raw count.

Keep `Square.png` unchanged. Give each replacement its own image and identity,
sample the accepted art beside its actual placement, and preserve contact,
dimensions and sorting with measured bounds. Each assignment needs its own
before/after picture review. Do not change stored-goods behaviour as part of
the redraw; painted goods must not be presented as live inventory counts.

The wardrobe sheet in draft #1419
is a separate, unwired handoff. It replaces a borrowed dresser later and is
not part of the square-reference count; placement remains with its active
room change.
