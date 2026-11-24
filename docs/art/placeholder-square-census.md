# Placeholder square census

Baseline: `c626cea10d1cb496df41cca2f9ab2f580ad47b28` (2026-11-23).

Scanned all 149 `.prefab` files under `Assets/_Project` as YAML text. **Seven prefabs contain 117 explicit SpriteRenderer references** to `Assets/_Project/Art/Sprites/Square.png`, GUID `0c1634d5861312443ac89b42a97625b6`.

Since the first census at `f1b4ab2`, main added `Assets/_Project/Prefabs/Dwellings/CamperClipperDwelling.prefab`. It has no Square reference. The seven-prefab list and all 117 renderer rows below are unchanged.

This records existing content debt; it does not accept a look or authorize more square placeholders. No asset was changed and no scene was rendered.

## Scope and maintenance

- Every row joins a SpriteRenderer (`!u!212`) to its GameObject and Transform by fileID, regardless of YAML document order. Renderer IDs disambiguate repeated object names.
- Tint is the serialized `m_Color` RGBA value. Scale is the same object's serialized `m_LocalScale` XYZ, not accumulated parent/world scale or visible dimensions. Values retain their source precision.
- Disabled renderers and inactive objects are included: they still carry the reference. Runtime reassignment and scene overrides are not measured.
- This is a census of explicit references in each prefab file, not expanded nested/variant inheritance. A new explicit `m_Sprite` override referencing Square fails the guard for deliberate resolution, rather than disappearing from the scan.
- `PlaceholderSquareCensusTests` holds the seven-prefab allowlist and reads the rows below as the per-renderer baseline. A new prefab or renderer fails. When a renderer stops using Square, **delete its row**. When the last one goes, **delete the prefab line from `KnownSquarePrefabs`** and remove its section/summary row here.
- For an intentional object name, tint or local-scale change, update the corresponding row after reviewing the change. Do not add placeholders to make the test green.

## Prefabs

| Prefab | Square renderers |
| --- | ---: |
| `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcBoatGearWholesaler.prefab` | 16 |
| `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcCommunityHall.prefab` | 22 |
| `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab` | 50 |
| `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishingGearWholesaler.prefab` | 24 |
| `Assets/_Project/Prefabs/Props/Storage/CamperShoreLead.prefab` | 3 |
| `Assets/_Project/Prefabs/Props/Storage/GinnyGearLocker.prefab` | 1 |
| `Assets/_Project/Prefabs/Props/Storage/GinnyRodRack.prefab` | 1 |

## Renderer rows

### `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcBoatGearWholesaler.prefab`

| Renderer fileID | Object name | Tint RGBA | Local scale XYZ |
| --- | --- | --- | --- |
| 2618399048685872483 | BackWallEast | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 9.4, y: 1.7645525, z: 1} |
| 6070911562005069556 | BackWallWest | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 11.8, y: 1.7645525, z: 1} |
| 8223387647860988626 | DoorStep | {r: 0.3, g: 0.3, b: 0.32, a: 1} | {x: 2.8, y: 1.2855754, z: 1} |
| 3880670097960207906 | ElectronicsCase | {r: 0.62, g: 0.78, b: 0.82, a: 1} | {x: 6.3999996, y: 2.4812942, z: 1} |
| 2713882331244760869 | Interior | {r: 0.8, g: 0.77, b: 0.7, a: 1} | {x: 24, y: 11.570176, z: 1} |
| 3443998285198223625 | NorthWallFace | {r: 0.6, g: 0.67, b: 0.74, a: 1} | {x: 24, y: 4.0626855, z: 1} |
| 4002036160097974045 | ShelfGondola | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 1.6000004, y: 4.8692837, z: 1} |
| 201697677304615530 | Shell | {r: 0.6, g: 0.67, b: 0.74, a: 1} | {x: 24, y: 22.294798, z: 1} |
| 8272235501631889335 | StockPallet | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 2.8000002, y: 3.0747793, z: 1} |
| 2192634288928542523 | StockShelving | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 9.4, y: 4.1419406, z: 1} |
| 3586615791120519512 | TradeCounter | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 5.2000003, y: 2.2514806, z: 1} |
| 1550770456983379035 | WallShelfWest | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 1, y: 8.849266, z: 1} |
| 1160489697257163432 | WashroomWall | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.5999999, y: 3.2429638, z: 1} |
| 2070437347853515749 | Zone_ShopFloor | {r: 0.8, g: 0.77, b: 0.7, a: 1} | {x: 24, y: 7.327778, z: 1} |
| 7733633093412484615 | Zone_StaffWashroom | {r: 0.6, g: 0.68, b: 0.78, a: 1} | {x: 4.8, y: 4.242398, z: 1} |
| 8443580675395337129 | Zone_StockRoom | {r: 0.68, g: 0.64, b: 0.55, a: 1} | {x: 19.2, y: 4.242398, z: 1} |

### `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcCommunityHall.prefab`

| Renderer fileID | Object name | Tint RGBA | Local scale XYZ |
| --- | --- | --- | --- |
| 7389743444338569386 | BackWallEnd | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.59999985, y: 4.3357024, z: 1} |
| 3778371769516306703 | BackWallMiddle | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.59999985, y: 3.9500308, z: 1} |
| 1216806961526625175 | BackWallNorth | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.5999998, y: 5.6212773, z: 1} |
| 8468121234480545449 | BackWallSouth | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.5999994, y: 9.349446, z: 1} |
| 8573551116451310831 | DoorStep | {r: 0.3, g: 0.3, b: 0.32, a: 1} | {x: 2, y: 1.7998054, z: 1} |
| 1716117928528422878 | GymBench | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 0.79999983, y: 3.2605906, z: 1} |
| 4562312955268947901 | GymKitchenWall | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 11.7, y: 1.7645526, z: 1} |
| 8322856856588834340 | HallStage | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 8, y: 3.7475185, z: 1} |
| 9185104858532816669 | Interior | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 24.000002, y: 23.140352, z: 1} |
| 5626314013553859062 | KitchenWashroomWall | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 11.700001, y: 1.7645535, z: 1} |
| 948495333882303855 | Shell | {r: 0.78, g: 0.72, b: 0.62, a: 1} | {x: 24.000002, y: 30.800797, z: 1} |
| 784569706041752624 | Zone_FrontHall | {r: 0.8, g: 0.77, b: 0.7, a: 1} | {x: 11.999998, y: 23.140352, z: 1} |
| 4733436728831797777 | Zone_Gym | {r: 0.7, g: 0.66, b: 0.6, a: 1} | {x: 11.999999, y: 10.284601, z: 1} |
| 8363080353042196965 | Zone_Kitchen | {r: 0.55, g: 0.62, b: 0.6, a: 1} | {x: 12, y: 6.4278765, z: 1} |
| 4599378568647697715 | Zone_Washroom | {r: 0.6, g: 0.68, b: 0.78, a: 1} | {x: 11.999999, y: 6.4278755, z: 1} |
| 3412512821665127493 | gym_equipment.bench_press | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 4, y: 2.0516205, z: 1} |
| 739523500381217006 | gym_equipment.grip_trainer | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 1.9999999, y: 2.664455, z: 1} |
| 8334646408446695287 | gym_equipment.pull_up_bar | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 1.9999998, y: 4.8093796, z: 1} |
| 8618639559350878005 | gym_equipment.rowing_machine | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 6, y: 2.2048287, z: 1} |
| 7835466295187029648 | gym_equipment.skipping_rope | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 2, y: 1.3621798, z: 1} |
| 2285363962650125835 | gym_equipment.squat_rack | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 4.0000005, y: 5.9417467, z: 1} |
| 2347734893766794088 | gym_equipment.treadmill | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 3.9999998, y: 3.2772903, z: 1} |

### `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab`

| Renderer fileID | Object name | Tint RGBA | Local scale XYZ |
| --- | --- | --- | --- |
| 3965781039780425417 | CanningConveyor | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 24, y: 2.8176641, z: 1} |
| 407889897892073694 | ColdStoreDoorA | {r: 0.3, g: 0.3, b: 0.32, a: 1} | {x: 0.5999985, y: 6.762394, z: 1} |
| 7494703892916933131 | ColdStoreDoorB | {r: 0.3, g: 0.3, b: 0.32, a: 1} | {x: 0.5999985, y: 6.762394, z: 1} |
| 1944801372206881465 | ColdStoreWall | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.5999985, y: 27.73662, z: 1} |
| 2860819194485899626 | DeliveryEdgeRail | {r: 0.85, g: 0.7, b: 0.2, a: 1} | {x: 0.5999985, y: 14.54105, z: 1} |
| 5090604240282840050 | DispatchPallets | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 16, y: 4.4096565, z: 1} |
| 1903441142509735340 | DoorStep | {r: 0.3, g: 0.3, b: 0.32, a: 1} | {x: 2.800003, y: 1.285574, z: 1} |
| 6420372219635204209 | DryStoreRacks | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 24, y: 5.6053762, z: 1} |
| 7299440088763908973 | Filler | {r: 0.4, g: 0.42, b: 0.45, a: 1} | {x: 6, y: 6.584534, z: 1} |
| 1841617583090726179 | FilletTableA | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 24, y: 2.9215698, z: 1} |
| 6447776041966647135 | FilletTableB | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 24, y: 2.9215698, z: 1} |
| 3236908180549252331 | GradingTable | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 12, y: 2.9215703, z: 1} |
| 2183987990841079353 | IntakeScale | {r: 0.4, g: 0.42, b: 0.45, a: 1} | {x: 4, y: 3.1839862, z: 1} |
| 1947298915873887853 | IntakeTotes | {r: 0.25, g: 0.45, b: 0.7, a: 1} | {x: 3, y: 3.1786852, z: 1} |
| 2379873953262691716 | Interior | {r: 0.62, g: 0.63, b: 0.63, a: 1} | {x: 96, y: 35.996105, z: 1} |
| 3360208153927791607 | OfficeCounter | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 1.4000015, y: 5.465419, z: 1} |
| 1093289573295555318 | OfficeWall | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.5999985, y: 6.521181, z: 1} |
| 5393332582201460072 | OfficeWestWallNorth | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.6000004, y: 4.142867, z: 1} |
| 3600091528420070713 | OfficeWestWallSouth | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.6000004, y: 4.335703, z: 1} |
| 9109465696936027174 | PackingTableA | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 8, y: 3.3072429, z: 1} |
| 7866174225364084817 | PackingTableB | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 8, y: 3.3072429, z: 1} |
| 1078513864442261556 | ReceptionBench | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 4, y: 1.3322277, z: 1} |
| 6371140212240009322 | Retort | {r: 0.4, g: 0.42, b: 0.45, a: 1} | {x: 6, y: 7.197369, z: 1} |
| 2316759123955711468 | ReturnsRail | {r: 0.85, g: 0.7, b: 0.2, a: 1} | {x: 15.700001, y: 2.0709705, z: 1} |
| 6782901030354319712 | Shell | {r: 0.72, g: 0.74, b: 0.76, a: 1} | {x: 96, y: 45.188637, z: 1} |
| 8617307760597031280 | StaffRoomWestWallNorth | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.6000004, y: 4.142867, z: 1} |
| 3965860303970977298 | StaffRoomWestWallSouth | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.6000004, y: 4.335703, z: 1} |
| 2860431140340403264 | StaffRowWall | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 60, y: 1.7645521, z: 1} |
| 1481131701134342059 | StaffWashWestWall | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.5999985, y: 8.899495, z: 1} |
| 1625842356780668731 | Zone_CanningLine | {r: 0.55, g: 0.62, b: 0.6, a: 1} | {x: 32, y: 12.855751, z: 1} |
| 9159030749824882608 | Zone_DeliveryEdge | {r: 0.8, g: 0.77, b: 0.7, a: 1} | {x: 4, y: 12.855751, z: 1} |
| 901326057444365837 | Zone_DispatchStaging | {r: 0.68, g: 0.64, b: 0.55, a: 1} | {x: 24, y: 5.1423016, z: 1} |
| 4951698408672683128 | Zone_DryStore | {r: 0.68, g: 0.64, b: 0.55, a: 1} | {x: 32, y: 5.1423016, z: 1} |
| 4545699681365135204 | Zone_FilletLine | {r: 0.55, g: 0.62, b: 0.6, a: 1} | {x: 32, y: 10.284601, z: 1} |
| 4410875283941787782 | Zone_FinishedColdStore | {r: 0.6, g: 0.68, b: 0.78, a: 1} | {x: 28, y: 10.284601, z: 1} |
| 4046543824928012653 | Zone_IntakeI1 | {r: 0.8, g: 0.77, b: 0.7, a: 1} | {x: 12, y: 5.1423006, z: 1} |
| 3962724597899278701 | Zone_IntakeI2 | {r: 0.62, g: 0.63, b: 0.63, a: 1} | {x: 12, y: 5.1423, z: 1} |
| 1557478547533969094 | Zone_LobsterCell | {r: 0.6, g: 0.68, b: 0.78, a: 1} | {x: 28, y: 5.1423, z: 1} |
| 7966192486589647002 | Zone_Office | {r: 0.7, g: 0.66, b: 0.6, a: 1} | {x: 20, y: 7.7134514, z: 1} |
| 8286144444548690478 | Zone_OutboundDock | {r: 0.62, g: 0.63, b: 0.63, a: 1} | {x: 12, y: 5.1423016, z: 1} |
| 7520149861881018620 | Zone_Packing | {r: 0.55, g: 0.62, b: 0.6, a: 1} | {x: 24, y: 10.284601, z: 1} |
| 2023085979929883138 | Zone_PlantServices | {r: 0.5, g: 0.5, b: 0.52, a: 1} | {x: 20, y: 7.7134514, z: 1} |
| 815464153775814190 | Zone_RawChilledBuffer | {r: 0.6, g: 0.68, b: 0.78, a: 1} | {x: 28, y: 7.713451, z: 1} |
| 4411138036123833131 | Zone_ReceivingAisle | {r: 0.8, g: 0.77, b: 0.7, a: 1} | {x: 12, y: 7.713451, z: 1} |
| 7741397824037436368 | Zone_Reception | {r: 0.8, g: 0.77, b: 0.7, a: 1} | {x: 16, y: 7.7134514, z: 1} |
| 2790124294653816884 | Zone_Refrigeration | {r: 0.5, g: 0.5, b: 0.52, a: 1} | {x: 28, y: 5.1423016, z: 1} |
| 1224862977984875585 | Zone_ReturnsWash | {r: 0.55, g: 0.62, b: 0.6, a: 1} | {x: 12, y: 5.1423006, z: 1} |
| 536756657175690762 | Zone_StaffEntranceWash | {r: 0.7, g: 0.66, b: 0.6, a: 1} | {x: 20, y: 7.7134514, z: 1} |
| 4431859451482574411 | Zone_StaffRoom | {r: 0.7, g: 0.66, b: 0.6, a: 1} | {x: 20, y: 7.7134514, z: 1} |
| 1929948195302007281 | Zone_WeighAndGrade | {r: 0.55, g: 0.62, b: 0.6, a: 1} | {x: 20, y: 12.855751, z: 1} |

### `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishingGearWholesaler.prefab`

| Renderer fileID | Object name | Tint RGBA | Local scale XYZ |
| --- | --- | --- | --- |
| 1015893364989555222 | BackWallEast | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 12.6, y: 1.7645524, z: 1} |
| 7053706542001052066 | BackWallWest | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 20.6, y: 1.7645524, z: 1} |
| 6043640157007274232 | DoorStep | {r: 0.3, g: 0.3, b: 0.32, a: 1} | {x: 2.8000002, y: 1.2855749, z: 1} |
| 6173661329888678113 | FloorPallets | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 8, y: 4.716075, z: 1} |
| 5229649928033577464 | Interior | {r: 0.62, g: 0.63, b: 0.63, a: 1} | {x: 36, y: 17.998053, z: 1} |
| 6599410194719937378 | NetBales | {r: 0.4, g: 0.42, b: 0.45, a: 1} | {x: 3.1999998, y: 3.6383116, z: 1} |
| 8790302569345168544 | NorthWallFace | {r: 0.7, g: 0.66, b: 0.56, a: 1} | {x: 36, y: 4.062686, z: 1} |
| 6958464374102258231 | PalletRackNorth | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 20.6, y: 6.188259, z: 1} |
| 2355078660607428516 | PalletRackWest | {r: 0.78, g: 0.8, b: 0.82, a: 1} | {x: 2.4000006, y: 12.87325, z: 1} |
| 4344203944262331641 | PotStack | {r: 0.36, g: 0.52, b: 0.38, a: 1} | {x: 4, y: 5.32891, z: 1} |
| 1077490831408201967 | RopeCoils | {r: 0.82, g: 0.72, b: 0.4, a: 1} | {x: 2.3999999, y: 2.7683616, z: 1} |
| 6084162704334131669 | Shell | {r: 0.7, g: 0.66, b: 0.56, a: 1} | {x: 36, y: 27.190586, z: 1} |
| 3863469990452846498 | StaffWallNorth | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.6000004, y: 7.8067555, z: 1} |
| 8171721683289372206 | StaffWallSouth | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 0.6000004, y: 2.6001763, z: 1} |
| 6384056944966096359 | TackleGondola | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 1.6, y: 5.6652803, z: 1} |
| 5666925445040185136 | TradeCounter | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 9, y: 2.3800383, z: 1} |
| 8272459828999257428 | WallShelfEast | {r: 0.55, g: 0.4, b: 0.26, a: 1} | {x: 1, y: 10.312702, z: 1} |
| 3717443078162433155 | WashroomWallEast | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 2.1999998, y: 1.7645526, z: 1} |
| 2570395819222387240 | WashroomWallWest | {r: 0.58, g: 0.58, b: 0.6, a: 1} | {x: 3.6999998, y: 1.7645526, z: 1} |
| 6697266851148396560 | Zone_BulkDisplay | {r: 0.8, g: 0.77, b: 0.7, a: 1} | {x: 16, y: 8.356238, z: 1} |
| 5949057195811748385 | Zone_StaffRoom | {r: 0.7, g: 0.66, b: 0.6, a: 1} | {x: 9, y: 5.1423006, z: 1} |
| 1417832802356649729 | Zone_StaffWashroom | {r: 0.6, g: 0.68, b: 0.78, a: 1} | {x: 9, y: 4.499513, z: 1} |
| 4944318350860775463 | Zone_TradeFloor | {r: 0.8, g: 0.77, b: 0.7, a: 1} | {x: 20, y: 8.356238, z: 1} |
| 7901471877688425768 | Zone_Warehouse | {r: 0.68, g: 0.64, b: 0.55, a: 1} | {x: 27, y: 9.641814, z: 1} |

### `Assets/_Project/Prefabs/Props/Storage/CamperShoreLead.prefab`

| Renderer fileID | Object name | Tint RGBA | Local scale XYZ |
| --- | --- | --- | --- |
| 4895106647942165336 | Run0 | {r: 0.86, g: 0.7, b: 0.16, a: 1} | {x: 11.195862, y: 0.08, z: 1} |
| 6414638449624088705 | Run1 | {r: 0.86, g: 0.7, b: 0.16, a: 1} | {x: 2.7920017, y: 0.08, z: 1} |
| 8231050914704565214 | Run2 | {r: 0.86, g: 0.7, b: 0.16, a: 1} | {x: 2.6900005, y: 0.08, z: 1} |

### `Assets/_Project/Prefabs/Props/Storage/GinnyGearLocker.prefab`

| Renderer fileID | Object name | Tint RGBA | Local scale XYZ |
| --- | --- | --- | --- |
| 4673724462723122298 | Art | {r: 0.42, g: 0.5, b: 0.46, a: 1} | {x: 0.6, y: 1.8, z: 1} |

### `Assets/_Project/Prefabs/Props/Storage/GinnyRodRack.prefab`

| Renderer fileID | Object name | Tint RGBA | Local scale XYZ |
| --- | --- | --- | --- |
| 7072931822352091966 | Art | {r: 0.55, g: 0.4, b: 0.25, a: 1} | {x: 0.9, y: 1.4, z: 1} |
