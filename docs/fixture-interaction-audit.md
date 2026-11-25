# Room fixtures: verbs, footprints and reach

Static audit of main `f1b4ab2f59f417eda06f2239f7dee25d4c52ad23`, 2026-11-22.
This document changes no behaviour or assets. The companion [reach proposal](fixture-wall-reach-proposal.md) is a design for a later change.

## Scope and how to read the tables

The inventory covers every prefab in `Assets/_Project/Prefabs/Props/Kitchen`,
`Props/Storage` and `Washrooms`; every asset of the six corresponding Def types
(`CookingStationDef`, `KitchenStorageDef`, `StowageDef`, `FreezerDef`,
`WashStationDef`, `WashroomDef`); the five house furniture entries; the camper's
room anchors; and the boat interior Defs containing cabin furniture. Def discovery
used their script GUIDs across all project assets, not just filename keywords.
Outdoor scenery, vehicles, gym equipment, loose goods, lamps and merchant stock
are not domestic room fixtures. Shop counter bindings are distinguished below
from the furniture they stand beside.

**NO VERB** means no player interaction implementation is attached/built, even
when the data names an intended action. A refusal from an implemented verb is
still a verb. **NO SOLID** means no enabled non-trigger collider on the prefab;
it does not mean that a surrounding building has no walls. **NO WALL CHECK**
means the interaction path does not test the segment from actor to fixture
against a wall. These are separate findings.

Prefab sizes below are local XY metres at unit root scale. They include children;
the rack/locker colliders are on their unit-scale roots, not their scaled art.
Runtime changes are called out explicitly. Furniture ground width/depth is not
screen-space XY: the house projection rotates it and squashes depth by
`sin(40 degrees)` (about 0.642788). Scene placement, enabled parents, physics
layers and the player's movement controller can change actual access. No editor,
playtest, collision simulation, screenshot or visual acceptance was run.

## Standalone prefab inventory

Paths in this table are relative to `Assets/_Project/Prefabs/`. Def paths and ids
are expanded in the Def inventory. All offered verbs in this table are **NO WALL
CHECK**, through path **R** (shared resolver) or **L** (legacy freezer) below.

| Prefab | Def id | Player verb and offering component | Solid footprint, saved -> runtime | Reach / wall gate |
|---|---|---|---|---|
| `Props/Kitchen/Stove.prefab` | `cooking_station.stove` | Cook on the stove — `CookingStationFixture` | **NO SOLID**, no collider; remains absent | 1 m / **R: NO WALL CHECK** |
| `Props/Kitchen/PrepCounter.prefab` | `cooking_station.prep_counter` | Prepare food — `CookingStationFixture` | **NO SOLID**, no collider; remains absent | 1 m / **R: NO WALL CHECK** |
| `Props/Kitchen/Microwave.prefab` | `cooking_station.microwave` | Use the microwave — `CookingStationFixture` | **NO SOLID**, no collider; remains absent | 1 m / **R: NO WALL CHECK** |
| `Props/Kitchen/Fridge.prefab` | `kitchen_storage.fridge` | **NO VERB** — `KitchenStoreFixture` only binds data | `BoxCollider2D` 0.7 x 0.43709555 -> fitted box, ground 0.7 x 0.68 | N/A |
| `Props/Kitchen/KitchenSink.prefab` | `washstation.sink` | Wash up at the sink / Stop washing — `WashStationPoint` | `BoxCollider2D` 1 x 0.38567257 -> fitted box, ground 1 x 0.6 | 1.2 m / **R: NO WALL CHECK** |
| `Props/Kitchen/GinnyFreezer.prefab` | `freezer.ginny_freezer` | F exchanges bucket catch — `GinnyFreezer.Update`; **no shared verb/popup label** | **NO SOLID**, no collider; `ChestFreezerArt` adds none | 4 m / **L: NO WALL CHECK** |
| `Props/Kitchen/CamperFreezer.prefab` | `freezer.camper_freezer` | Use the freezer — `CamperFreezer` | **NO SOLID**, no collider; `ChestFreezerArt` adds none | 1.2 m / **R: NO WALL CHECK** |
| `Props/Storage/GinnyRodRack.prefab` | `stowage.ginny_rod_rack` | Stow it in the rod rack / Take one from the rod rack — `StowageRack` | `BoxCollider2D` 0.9 x 0.3, offset (0,0) | 1.2 m / **R: NO WALL CHECK** |
| `Props/Storage/GinnyGearLocker.prefab` | `stowage.ginny_gear_locker` | Stow it in the gear locker / Take one from the gear locker — `StowageRack` | `BoxCollider2D` 0.6 x 0.6, offset (0,0) | 1.2 m / **R: NO WALL CHECK** |
| `Props/Storage/CamperShoreLead.prefab` | None | **NO VERB** — no interaction component | **NO SOLID**, no collider; cable decoration | N/A |
| `Washrooms/Bath.prefab` | `washstation.bath` | Have a bath / Stop washing — `WashStationPoint` | Two `CircleCollider2D`, r=0.34, offsets (+/-0.42,0) -> disabled, one fitted box, ground 1.6 x 0.78 | 1.3 m / **R: NO WALL CHECK** |
| `Washrooms/Toilet.prefab` | `washstation.toilet` | Sit down / Get up — `SitPoint` | `CircleCollider2D` r=0.2, offset (0,0) -> disabled, one fitted box, ground 0.46 x 0.7 | 1.4 m / **R: NO WALL CHECK** |
| `Washrooms/WashStand.prefab` | `washstation.sink` | Wash up at the basin / Stop washing — `WashStationPoint` | `CircleCollider2D` r=0.3, offset (0,0.5) -> disabled, one fitted box, ground 0.58 x 0.44 | 1.2 m / **R: NO WALL CHECK** |
| `Washrooms/Shower.prefab` | `washstation.shower` | Take a shower / Stop washing — `WashStationPoint` | **NO SOLID**, no collider; step-in shower intentionally retains this | 1.1 m / **R: NO WALL CHECK** |
| `Washrooms/WharfShower.prefab` | `washstation.shower` | Rinse off under the shower / Stop washing — `WashStationPoint` | `CircleCollider2D` r=0.35, offset (0,0.5), retained; not a fitted box | 1.3 m / **R: NO WALL CHECK** |
| `Washrooms/Outhouse.prefab` | `washroom.outhouse` and `washstation.toilet` | Sit down / Get up — `SitPoint` | `PolygonCollider2D`, four-point rectangle 1.1 x 0.77134514 at cell 0; `OuthouseArt` turns its footprint with the cell | 0.7 m from door offer offset (0,-0.6106482) / **R: NO WALL CHECK** |

The cooking footprint dimensions in `ApplianceSheets` are **layout data, not
colliders**. `ApplianceArt.EnsureStop` (lines 314-327) only replaces a pre-existing
solid for non-standing poses. It deliberately leaves pieces with no solid
walk-through, and returns immediately for the two standing shower poses.
`ApplianceArt.Settle` (130-137) calls this before fitting.

For a fitted appliance box at cell angle theta, its world XY width is
`|w cos(theta)| + |d sin(theta)|`; its height is
`(|w sin(theta)| + |d cos(theta)|) * sin(40 degrees)`. Its centre is the
projected footprint centre shifted down by `FixtureStopDropMetres` (0.7 on this
main); local size/offset divide by lossy scale. Thus the cell-0 runtime boxes are
bath 1.6 x 0.501374, toilet 0.46 x 0.449951, basin 0.58 x 0.282827,
fridge 0.7 x 0.437096, sink 1 x 0.385673, each centred at (0,-0.7) on a unit root.
See `World/Washrooms/ApplianceFacing.cs:69-81`, `ApplianceArt.cs:177-184,303-327`
and `ApplianceSheets.cs:121-157`, under `Assets/_Project/Code/`.

The fridge's component explicitly says it carries "no verb, no store key and no
state" (`World/Kitchen/KitchenStoreFixture.cs:11-15`). Door animation capability
is not a player action. The freezer, rack and cooker availability checks only
require a Def; ownership/fullness/ingredients can refuse later. Those checks do
not provide wall protection.

## House furniture assembled from the room kit

There are **no standalone prefabs or furniture Def ids** for these five kit
entries. The source is `Assets/_Project/Art/Sprites/Interiors/Interiors.json`
(`props`, lines 950-1155), `Art/Editor/InteriorKit.cs:577-610` and
`Code/App/Editor/StPetersInteriors.cs`. The latter's `Furnish` creates one
non-trigger `PolygonCollider2D` per prop (610-615), using `InteriorFootprint.PropQuad`
(360-366) and the declared ground dimensions. Root rotation/projection determines
the actual quad; these are not axis-aligned boxes at every facing.

| Piece / prefab | Def id | Verb and component | Solid ground footprint | Wall gate |
|---|---|---|---|---|
| `bed` as PlayerBed; built object | None | Sleep — `InteriorBed`; player's bed can rest | Polygon quad 1.5 x 2.05 m | 1.4 m / **R: NO WALL CHECK** |
| `bed` as OwnerBed; built object | None | Sleep — `InteriorBed`; answers with ownership refusal | Polygon quad 1.5 x 2.05 m | 1.4 m / **R: NO WALL CHECK** |
| `bed` with `InteriorFixture.None`; built object | None | **NO VERB** — no `InteriorBed` | Polygon quad 1.5 x 2.05 m | N/A |
| `table`; built object, including school desks | None | **NO VERB** — no interaction component | Polygon quad 2.315 x 0.78 m | N/A |
| `chair`; built object | None | **NO VERB** — no `SitPoint` | Polygon quad 0.44 x 0.42 m | N/A |
| `dresser` as Wardrobe; built object | None | Open the wardrobe — `InteriorWardrobe` | Polygon quad 1 x 0.5 m | 1.2 m / **R: NO WALL CHECK** |
| `dresser` with `InteriorFixture.None`; built object | None | **NO VERB** | Polygon quad 1 x 0.5 m | N/A |
| `seaChest` with `InteriorFixture.None`; built object | None | **NO VERB** — not a storage container | Polygon quad 0.9 x 0.5 m | N/A |
| `seaChest` standing in for StairUp / StairDown | None | Go up / Go down — `InteriorStair` | Polygon quad 0.9 x 0.5 m | 1.2 m / **R: NO WALL CHECK**; current-storey gate also applies |
| Settee | None in house kit | **NO VERB**; no house-kit entry or standalone prefab found | No house-kit footprint to instantiate; camper entry below | N/A |

`Fit` returns immediately for `InteriorFixture.None` (808). It only adds the
bed/wardrobe/stair components for explicit roles (814-847). In the committed
`StPeters.unity` furniture objects, all 37 kit pieces have a solid polygon:
5 beds (2 with `InteriorBed`, 3 without), 6 tables, 13 chairs, 6 dressers
(1 with `InteriorWardrobe`), and 7 sea chests (2 with `InteriorStair`). This
component count is a static scene read, not a claim that every object is active
at the same time. Examples of the functional ids are
`fixture.ginnyCottage.bed_player`, `.bed_owner`, and `.wardrobe`.

### Counter bindings are not the counter's furniture footprint

`GeneralStoreCounter` in `StPeters.unity` binds `GearShop`, `BaitShop`,
`SupplyShop`, `InstrumentShop`, `LicenseVendor`, `Market`, `FishBuyer` and
`WharfSellPoint`. `NmcBoatGearWholesalerCounter` binds three `InstrumentShop`s;
`NmcFishingGearWholesalerCounter` binds `GearShop`; `NmcWharfMarketCounter`
binds `WharfSellPoint`, `FishBuyer`, `Market`. None is a standalone fixture prefab
or has an attached solid collider or `IInteractable` verb. They are **NO direct
fixture VERB / NO attached SOLID** counter binding objects, not evidence that
buying/selling is unavailable: their transaction APIs are driven by the merchant
conversation/catalogue. Stock/seller Defs are not furniture Defs.
`BuildingInteriorStander.StandShop` builds shell walls but no separate furniture
(`furnitureCount: 0`, line 360); a drawn counter is not thereby a solid counter.
The conversation gate is path **C** below. No merchant transaction or dialogue
implementation is changed or accepted by this audit.

## Complete fixture Def inventory

The following table is an asset inventory. A Def cannot offer a verb or carry a
Unity collider by itself; "none" means no standalone fixture binding found, not
that the Def should be deleted. Prefab names refer to the table above. For bare
data there is **NO offered VERB / no physical footprint** until a fixture is built.

| Asset under `Assets/_Project/` | Def id | Binding, offered verb/component and footprint |
|---|---|---|
| [Microwave.asset](../Assets/_Project/Data/Resources/CookingStations/Microwave.asset) (`Data/Resources/CookingStations`) | `cooking_station.microwave` | Microwave; Use the microwave / CookingStationFixture; NO SOLID; R |
| [PrepCounter.asset](../Assets/_Project/Data/Resources/CookingStations/PrepCounter.asset) (`Data/Resources/CookingStations`) | `cooking_station.prep_counter` | PrepCounter; Prepare food / CookingStationFixture; NO SOLID; R |
| [Stove.asset](../Assets/_Project/Data/Resources/CookingStations/Stove.asset) (`Data/Resources/CookingStations`) | `cooking_station.stove` | Stove; Cook on the stove / CookingStationFixture; NO SOLID; R |
| [CamperFridge.asset](../Assets/_Project/Data/Resources/Kitchen/Storage/CamperFridge.asset) (`Data/Resources/Kitchen/Storage`) | `kitchen_storage.camper_fridge` | None; **NO VERB / no standalone solid footprint**. Data/anchor only; see camper or cabin inventory where referenced. |
| [CamperPantry.asset](../Assets/_Project/Data/Resources/Kitchen/Storage/CamperPantry.asset) (`Data/Resources/Kitchen/Storage`) | `kitchen_storage.camper_pantry` | None; **NO VERB / no standalone solid footprint**. Data/anchor only; see camper or cabin inventory where referenced. |
| [Cooler.asset](../Assets/_Project/Data/Resources/Kitchen/Storage/Cooler.asset) (`Data/Resources/Kitchen/Storage`) | `kitchen_storage.cooler` | None; **NO VERB / no standalone solid footprint**. Data/anchor only; see camper or cabin inventory where referenced. |
| [Fridge.asset](../Assets/_Project/Data/Resources/Kitchen/Storage/Fridge.asset) (`Data/Resources/Kitchen/Storage`) | `kitchen_storage.fridge` | Fridge; NO VERB / KitchenStoreFixture; fitted box (see prefab row) |
| [Pantry.asset](../Assets/_Project/Data/Resources/Kitchen/Storage/Pantry.asset) (`Data/Resources/Kitchen/Storage`) | `kitchen_storage.pantry` | None; **NO VERB / no standalone solid footprint**. Data/anchor only; see camper or cabin inventory where referenced. |
| [PantryLocker.asset](../Assets/_Project/Data/Resources/Kitchen/Storage/PantryLocker.asset) (`Data/Resources/Kitchen/Storage`) | `kitchen_storage.pantry_locker` | None; **NO VERB / no standalone solid footprint**. Data/anchor only; see camper or cabin inventory where referenced. |
| [CamperCatchFreezer.asset](../Assets/_Project/Data/Stowage/CamperCatchFreezer.asset) (`Data/Stowage`) | `freezer.camper_freezer` | CamperFreezer; Use the freezer / CamperFreezer; NO SOLID; R |
| [CamperGearLocker.asset](../Assets/_Project/Data/Stowage/CamperGearLocker.asset) (`Data/Stowage`) | `stowage.camper_gear_locker` | None; **NO VERB / no standalone solid footprint**. Data/anchor only; see camper or cabin inventory where referenced. |
| [CamperRodRack.asset](../Assets/_Project/Data/Stowage/CamperRodRack.asset) (`Data/Stowage`) | `stowage.camper_rod_rack` | None; **NO VERB / no standalone solid footprint**. Data/anchor only; see camper or cabin inventory where referenced. |
| [GinnyCatchFreezer.asset](../Assets/_Project/Data/Stowage/GinnyCatchFreezer.asset) (`Data/Stowage`) | `freezer.ginny_freezer` | GinnyFreezer; bucket exchange / GinnyFreezer; NO SOLID; L |
| [GinnyGearLocker.asset](../Assets/_Project/Data/Stowage/GinnyGearLocker.asset) (`Data/Stowage`) | `stowage.ginny_gear_locker` | GinnyGearLocker; stow/take / StowageRack; box 0.6 x 0.6; R |
| [GinnyRodRack.asset](../Assets/_Project/Data/Stowage/GinnyRodRack.asset) (`Data/Stowage`) | `stowage.ginny_rod_rack` | GinnyRodRack; stow/take / StowageRack; box 0.9 x 0.3; R |
| [RodRack.asset](../Assets/_Project/Data/Stowage/RodRack.asset) (`Data/Stowage`) | `stowage.rod_rack` | None; **NO VERB / no standalone solid footprint**. Data/anchor only; see camper or cabin inventory where referenced. |
| [ShedGearLocker.asset](../Assets/_Project/Data/Stowage/ShedGearLocker.asset) (`Data/Stowage`) | `stowage.shed_gear_locker` | None; **NO VERB / no standalone solid footprint**. Data/anchor only; see camper or cabin inventory where referenced. |
| [ShedRodRack.asset](../Assets/_Project/Data/Stowage/ShedRodRack.asset) (`Data/Stowage`) | `stowage.shed_rod_rack` | None; **NO VERB / no standalone solid footprint**. Data/anchor only; see camper or cabin inventory where referenced. |
| [Bath.asset](../Assets/_Project/Data/Resources/WashStations/Bath.asset) (`Data/Resources/WashStations`) | `washstation.bath` | Bath; bath/stop / WashStationPoint; fitted box (see prefab row); R |
| [Sea.asset](../Assets/_Project/Data/Resources/WashStations/Sea.asset) (`Data/Resources/WashStations`) | `washstation.sea` | No room prefab; outdoor washing station data, included to close the type inventory |
| [Shower.asset](../Assets/_Project/Data/Resources/WashStations/Shower.asset) (`Data/Resources/WashStations`) | `washstation.shower` | Shower and WharfShower; shower/stop / WashStationPoint; none or circle (see prefab rows); R |
| [Sink.asset](../Assets/_Project/Data/Resources/WashStations/Sink.asset) (`Data/Resources/WashStations`) | `washstation.sink` | KitchenSink and WashStand; wash/stop / WashStationPoint; fitted boxes (see prefab rows); R |
| [Toilet.asset](../Assets/_Project/Data/Resources/WashStations/Toilet.asset) (`Data/Resources/WashStations`) | `washstation.toilet` | Toilet and Outhouse; sit/get up / SitPoint; fitted box or polygon (see prefab rows); R |
| [Outhouse.asset](../Assets/_Project/Data/Resources/Washrooms/Outhouse.asset) (`Data/Resources/Washrooms`) | `washroom.outhouse` | Outhouse; sit/get up / SitPoint; polygon 1.1 x 0.77134514 at cell 0; R |

## Camper room anchors

`Assets/_Project/Data/Dwellings/Meshes/CamperClipperDwellingMesh.asset`
has id `dwelling.camper_clipper`. These anchors (754-844) are **data declarations,
not verbs offered by components**. `BuildingInterior` reads the dwelling room
shape and validates its anchors; there is no runtime dispatch of their `Verb`
fields in `BuildingInterior`, `DwellingInteriorRules` or `BuildingInteriorStander`.
The committed camper home still serializes `_interiorStood: 0` in `StPeters.unity`.
Each anchor below has `Footprint: []`: **no individual solid collider/size is
declared by the anchor**. The dwelling outline is not a substitute for furniture
collision. Separate freezer/rack prefabs must not be mistaken for installed camper
fixtures just because their Def ids agree.

| Anchor (no prefab) | Def id if any | Declared action, **not an offered verb** | Footprint / wall gate |
|---|---|---|---|
| `bunk` | None | `sleep_save`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |
| `galley_sink` | None | `wash_dishes`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |
| `stove` | None | `cook`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |
| `prep` | None | `prep`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |
| `fridge` | `kitchen_storage.camper_fridge` | `food_storage`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |
| `pantry` | `kitchen_storage.camper_pantry` | `food_storage`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |
| `freezer` | `freezer.camper_freezer` | `freeze_catch`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |
| `gear_locker` | `stowage.camper_gear_locker` | `storage`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |
| `rod_rack` | `stowage.camper_rod_rack` | `storage`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |
| `settee` | None | `sit`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |
| `head_basin` | `washstation.sink` | `wash`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |
| `head_toilet` | `washstation.toilet` | `toilet`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |
| `head_shower` | `washstation.shower` | `bathe`; **NO VERB**, no offering component | Empty; **NO individual SOLID**; wall gate N/A |

## Cabin furniture embedded in boat interior Defs

These have no standalone room-fixture prefab. The table includes sleep, cook,
prep, storage, food_storage, sit, toilet, wash_dishes, table and dine anchors;
helm, boarding, deck work and door/engine controls are outside the room-fixture
inventory. `BoatInteriorInstaller.BuildBunks`
(269-288) instantiates `BoatBunk` for each `sleep` anchor: it offers **Sleep**, with
the configured bunk reach (1 m on this main) and a below-deck/current-level availability gate, but
**R: NO WALL CHECK**. The installer explicitly leaves kitchen storage, seats and
tables uninstantiated (247-260); cook/prep/storage/wash anchors also have no
corresponding fixture construction in this installer. An anchor's action word
is not proof of an offered verb.

Unlike house furniture, cabins use logical obstruction polygons in hull-local
metres, not one `Collider2D` per piece. The table records the linked
`BunkObstruction` / `FurnitureObstruction` bounds when present; an AABB is a
measurement of that polygon, not a replacement for it. Unlinked anchors have
**no individual solid footprint proven** by the anchor; the room can still have
other obstructions. This does not claim that a player walks through every bunk.
Each asset below is under `Assets/_Project/Data/Boats/Interiors/`.

| Def asset / id (no fixture prefab) | Anchor | Offered verb / component, or declaration only | Individual solid / logical obstruction, hull-local metres |
|---|---|---|---|
| [CapeIslanderIso](../Assets/_Project/Data/Boats/Interiors/CapeIslanderIso.asset) / `interior.cape_islander_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [CapeIslanderIso](../Assets/_Project/Data/Boats/Interiors/CapeIslanderIso.asset) / `interior.cape_islander_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [CapeIslanderIso](../Assets/_Project/Data/Boats/Interiors/CapeIslanderIso.asset) / `interior.cape_islander_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 1.900; walk-blocking |
| [CapeIslanderIso](../Assets/_Project/Data/Boats/Interiors/CapeIslanderIso.asset) / `interior.cape_islander_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [CapeIslanderIso](../Assets/_Project/Data/Boats/Interiors/CapeIslanderIso.asset) / `interior.cape_islander_iso` | `bunk_starboard` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 1.900; walk-blocking |
| [CapeIslanderIso](../Assets/_Project/Data/Boats/Interiors/CapeIslanderIso.asset) / `interior.cape_islander_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/helm_seat` polygon, 4 vertices, AABB 0.480 x 0.480; walk-blocking |
| [CapeIslanderIso](../Assets/_Project/Data/Boats/Interiors/CapeIslanderIso.asset) / `interior.cape_islander_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [CapeIslanderIso](../Assets/_Project/Data/Boats/Interiors/CapeIslanderIso.asset) / `interior.cape_islander_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [CoastalPacketIso](../Assets/_Project/Data/Boats/Interiors/CoastalPacketIso.asset) / `interior.coastal_packet_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [CoastalPacketIso](../Assets/_Project/Data/Boats/Interiors/CoastalPacketIso.asset) / `interior.coastal_packet_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [CoastalPacketIso](../Assets/_Project/Data/Boats/Interiors/CoastalPacketIso.asset) / `interior.coastal_packet_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `below_sole/bunk` polygon, 4 vertices, AABB 1.100 x 2.100; walk-blocking |
| [CoastalPacketIso](../Assets/_Project/Data/Boats/Interiors/CoastalPacketIso.asset) / `interior.coastal_packet_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [CoastalPacketIso](../Assets/_Project/Data/Boats/Interiors/CoastalPacketIso.asset) / `interior.coastal_packet_iso` | `bunk_starboard` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `below_sole/bunk_stbd` polygon, 4 vertices, AABB 1.100 x 2.100; walk-blocking |
| [CoastalPacketIso](../Assets/_Project/Data/Boats/Interiors/CoastalPacketIso.asset) / `interior.coastal_packet_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 1.350 x 0.500; walk-blocking |
| [CoastalPacketIso](../Assets/_Project/Data/Boats/Interiors/CoastalPacketIso.asset) / `interior.coastal_packet_iso` | `table` | **NO VERB**, no component; declares `table` | No Collider2D; `house_sole/table_port` polygon, 4 vertices, AABB 1.000 x 0.900; walk-blocking |
| [CoastalPacketIso](../Assets/_Project/Data/Boats/Interiors/CoastalPacketIso.asset) / `interior.coastal_packet_iso` | `pantry` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [CoastalPacketIso](../Assets/_Project/Data/Boats/Interiors/CoastalPacketIso.asset) / `interior.coastal_packet_iso` | `fridge` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.fridge` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bed_guest_aft_port` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bed_guest_aft_stbd` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bed_vip_port` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bed_vip_stbd` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bed_crew_port` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bed_crew_stbd` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bed_master` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bed_captain` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bench_laundry` | **NO VERB**, no component; declares `wash_dishes` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bench_crew_pantry` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `chop_galley` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bench_galley` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bench_sky_bar` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpeditionYacht120Iso](../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) / `interior.expedition_yacht120_iso` | `bench_fly_bar` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpressCruiserIso](../Assets/_Project/Data/Boats/Interiors/ExpressCruiserIso.asset) / `interior.express_cruiser_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpressCruiserIso](../Assets/_Project/Data/Boats/Interiors/ExpressCruiserIso.asset) / `interior.express_cruiser_iso` | `bunk_aft` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpressCruiserIso](../Assets/_Project/Data/Boats/Interiors/ExpressCruiserIso.asset) / `interior.express_cruiser_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpressCruiserIso](../Assets/_Project/Data/Boats/Interiors/ExpressCruiserIso.asset) / `interior.express_cruiser_iso` | `sink` | **NO VERB**, no component; declares `wash_dishes` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpressCruiserIso](../Assets/_Project/Data/Boats/Interiors/ExpressCruiserIso.asset) / `interior.express_cruiser_iso` | `head` | **NO VERB**, no component; declares `toilet` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [ExpressCruiserIso](../Assets/_Project/Data/Boats/Interiors/ExpressCruiserIso.asset) / `interior.express_cruiser_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [GoFastIso](../Assets/_Project/Data/Boats/Interiors/GoFastIso.asset) / `interior.go_fast_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [GoFastIso](../Assets/_Project/Data/Boats/Interiors/GoFastIso.asset) / `interior.go_fast_iso` | `cooler` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [GoFastIso](../Assets/_Project/Data/Boats/Interiors/GoFastIso.asset) / `interior.go_fast_iso` | `head` | **NO VERB**, no component; declares `toilet` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [GoFastIso](../Assets/_Project/Data/Boats/Interiors/GoFastIso.asset) / `interior.go_fast_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterBoatIso](../Assets/_Project/Data/Boats/Interiors/LobsterBoatIso.asset) / `interior.lobster_boat_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterBoatIso](../Assets/_Project/Data/Boats/Interiors/LobsterBoatIso.asset) / `interior.lobster_boat_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterBoatIso](../Assets/_Project/Data/Boats/Interiors/LobsterBoatIso.asset) / `interior.lobster_boat_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 1.160; walk-blocking |
| [LobsterBoatIso](../Assets/_Project/Data/Boats/Interiors/LobsterBoatIso.asset) / `interior.lobster_boat_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterBoatIso](../Assets/_Project/Data/Boats/Interiors/LobsterBoatIso.asset) / `interior.lobster_boat_iso` | `bunk_starboard` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 1.160; walk-blocking |
| [LobsterBoatIso](../Assets/_Project/Data/Boats/Interiors/LobsterBoatIso.asset) / `interior.lobster_boat_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.720 x 0.380; walk-blocking |
| [LobsterBoatIso](../Assets/_Project/Data/Boats/Interiors/LobsterBoatIso.asset) / `interior.lobster_boat_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterBoatIso](../Assets/_Project/Data/Boats/Interiors/LobsterBoatIso.asset) / `interior.lobster_boat_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopFundyIso.asset) / `interior.lobster_inshore_hardtop_fundy_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopFundyIso.asset) / `interior.lobster_inshore_hardtop_fundy_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopFundyIso.asset) / `interior.lobster_inshore_hardtop_fundy_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.540; walk-blocking |
| [LobsterInshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopFundyIso.asset) / `interior.lobster_inshore_hardtop_fundy_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopFundyIso.asset) / `interior.lobster_inshore_hardtop_fundy_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterInshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopFundyIso.asset) / `interior.lobster_inshore_hardtop_fundy_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterInshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopFundyIso.asset) / `interior.lobster_inshore_hardtop_fundy_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopFundyIso.asset) / `interior.lobster_inshore_hardtop_fundy_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNewfoundlandIso.asset) / `interior.lobster_inshore_hardtop_newfoundland_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNewfoundlandIso.asset) / `interior.lobster_inshore_hardtop_newfoundland_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNewfoundlandIso.asset) / `interior.lobster_inshore_hardtop_newfoundland_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.433; walk-blocking |
| [LobsterInshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNewfoundlandIso.asset) / `interior.lobster_inshore_hardtop_newfoundland_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNewfoundlandIso.asset) / `interior.lobster_inshore_hardtop_newfoundland_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterInshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNewfoundlandIso.asset) / `interior.lobster_inshore_hardtop_newfoundland_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterInshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNewfoundlandIso.asset) / `interior.lobster_inshore_hardtop_newfoundland_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNewfoundlandIso.asset) / `interior.lobster_inshore_hardtop_newfoundland_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNorthumberlandIso.asset) / `interior.lobster_inshore_hardtop_northumberland_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNorthumberlandIso.asset) / `interior.lobster_inshore_hardtop_northumberland_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNorthumberlandIso.asset) / `interior.lobster_inshore_hardtop_northumberland_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.353; walk-blocking |
| [LobsterInshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNorthumberlandIso.asset) / `interior.lobster_inshore_hardtop_northumberland_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNorthumberlandIso.asset) / `interior.lobster_inshore_hardtop_northumberland_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterInshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNorthumberlandIso.asset) / `interior.lobster_inshore_hardtop_northumberland_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterInshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNorthumberlandIso.asset) / `interior.lobster_inshore_hardtop_northumberland_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreHardtopNorthumberlandIso.asset) / `interior.lobster_inshore_hardtop_northumberland_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenFundyIso.asset) / `interior.lobster_inshore_open_fundy_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenFundyIso.asset) / `interior.lobster_inshore_open_fundy_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenFundyIso.asset) / `interior.lobster_inshore_open_fundy_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.540; walk-blocking |
| [LobsterInshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenFundyIso.asset) / `interior.lobster_inshore_open_fundy_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenFundyIso.asset) / `interior.lobster_inshore_open_fundy_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterInshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenFundyIso.asset) / `interior.lobster_inshore_open_fundy_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterInshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenFundyIso.asset) / `interior.lobster_inshore_open_fundy_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenFundyIso.asset) / `interior.lobster_inshore_open_fundy_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNewfoundlandIso.asset) / `interior.lobster_inshore_open_newfoundland_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNewfoundlandIso.asset) / `interior.lobster_inshore_open_newfoundland_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNewfoundlandIso.asset) / `interior.lobster_inshore_open_newfoundland_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.433; walk-blocking |
| [LobsterInshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNewfoundlandIso.asset) / `interior.lobster_inshore_open_newfoundland_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNewfoundlandIso.asset) / `interior.lobster_inshore_open_newfoundland_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterInshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNewfoundlandIso.asset) / `interior.lobster_inshore_open_newfoundland_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterInshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNewfoundlandIso.asset) / `interior.lobster_inshore_open_newfoundland_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNewfoundlandIso.asset) / `interior.lobster_inshore_open_newfoundland_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNorthumberlandIso.asset) / `interior.lobster_inshore_open_northumberland_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNorthumberlandIso.asset) / `interior.lobster_inshore_open_northumberland_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNorthumberlandIso.asset) / `interior.lobster_inshore_open_northumberland_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.353; walk-blocking |
| [LobsterInshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNorthumberlandIso.asset) / `interior.lobster_inshore_open_northumberland_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNorthumberlandIso.asset) / `interior.lobster_inshore_open_northumberland_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterInshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNorthumberlandIso.asset) / `interior.lobster_inshore_open_northumberland_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterInshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNorthumberlandIso.asset) / `interior.lobster_inshore_open_northumberland_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterInshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterInshoreOpenNorthumberlandIso.asset) / `interior.lobster_inshore_open_northumberland_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopFundyIso.asset) / `interior.lobster_offshore_hardtop_fundy_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopFundyIso.asset) / `interior.lobster_offshore_hardtop_fundy_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopFundyIso.asset) / `interior.lobster_offshore_hardtop_fundy_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 1.154; walk-blocking |
| [LobsterOffshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopFundyIso.asset) / `interior.lobster_offshore_hardtop_fundy_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopFundyIso.asset) / `interior.lobster_offshore_hardtop_fundy_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterOffshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopFundyIso.asset) / `interior.lobster_offshore_hardtop_fundy_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterOffshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopFundyIso.asset) / `interior.lobster_offshore_hardtop_fundy_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopFundyIso.asset) / `interior.lobster_offshore_hardtop_fundy_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNewfoundlandIso.asset) / `interior.lobster_offshore_hardtop_newfoundland_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNewfoundlandIso.asset) / `interior.lobster_offshore_hardtop_newfoundland_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNewfoundlandIso.asset) / `interior.lobster_offshore_hardtop_newfoundland_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.973; walk-blocking |
| [LobsterOffshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNewfoundlandIso.asset) / `interior.lobster_offshore_hardtop_newfoundland_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNewfoundlandIso.asset) / `interior.lobster_offshore_hardtop_newfoundland_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 1.130; walk-blocking |
| [LobsterOffshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNewfoundlandIso.asset) / `interior.lobster_offshore_hardtop_newfoundland_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 1.130; walk-blocking |
| [LobsterOffshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNewfoundlandIso.asset) / `interior.lobster_offshore_hardtop_newfoundland_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNewfoundlandIso.asset) / `interior.lobster_offshore_hardtop_newfoundland_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNorthumberlandIso.asset) / `interior.lobster_offshore_hardtop_northumberland_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNorthumberlandIso.asset) / `interior.lobster_offshore_hardtop_northumberland_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNorthumberlandIso.asset) / `interior.lobster_offshore_hardtop_northumberland_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.837; walk-blocking |
| [LobsterOffshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNorthumberlandIso.asset) / `interior.lobster_offshore_hardtop_northumberland_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNorthumberlandIso.asset) / `interior.lobster_offshore_hardtop_northumberland_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 1.380; walk-blocking |
| [LobsterOffshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNorthumberlandIso.asset) / `interior.lobster_offshore_hardtop_northumberland_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 1.380; walk-blocking |
| [LobsterOffshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNorthumberlandIso.asset) / `interior.lobster_offshore_hardtop_northumberland_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreHardtopNorthumberlandIso.asset) / `interior.lobster_offshore_hardtop_northumberland_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenFundyIso.asset) / `interior.lobster_offshore_open_fundy_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenFundyIso.asset) / `interior.lobster_offshore_open_fundy_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenFundyIso.asset) / `interior.lobster_offshore_open_fundy_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 1.154; walk-blocking |
| [LobsterOffshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenFundyIso.asset) / `interior.lobster_offshore_open_fundy_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenFundyIso.asset) / `interior.lobster_offshore_open_fundy_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterOffshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenFundyIso.asset) / `interior.lobster_offshore_open_fundy_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.820; walk-blocking |
| [LobsterOffshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenFundyIso.asset) / `interior.lobster_offshore_open_fundy_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenFundyIso.asset) / `interior.lobster_offshore_open_fundy_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNewfoundlandIso.asset) / `interior.lobster_offshore_open_newfoundland_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNewfoundlandIso.asset) / `interior.lobster_offshore_open_newfoundland_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNewfoundlandIso.asset) / `interior.lobster_offshore_open_newfoundland_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.973; walk-blocking |
| [LobsterOffshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNewfoundlandIso.asset) / `interior.lobster_offshore_open_newfoundland_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNewfoundlandIso.asset) / `interior.lobster_offshore_open_newfoundland_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 1.130; walk-blocking |
| [LobsterOffshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNewfoundlandIso.asset) / `interior.lobster_offshore_open_newfoundland_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 1.130; walk-blocking |
| [LobsterOffshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNewfoundlandIso.asset) / `interior.lobster_offshore_open_newfoundland_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNewfoundlandIso.asset) / `interior.lobster_offshore_open_newfoundland_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNorthumberlandIso.asset) / `interior.lobster_offshore_open_northumberland_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNorthumberlandIso.asset) / `interior.lobster_offshore_open_northumberland_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNorthumberlandIso.asset) / `interior.lobster_offshore_open_northumberland_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.837; walk-blocking |
| [LobsterOffshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNorthumberlandIso.asset) / `interior.lobster_offshore_open_northumberland_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNorthumberlandIso.asset) / `interior.lobster_offshore_open_northumberland_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 1.380; walk-blocking |
| [LobsterOffshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNorthumberlandIso.asset) / `interior.lobster_offshore_open_northumberland_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 1.380; walk-blocking |
| [LobsterOffshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNorthumberlandIso.asset) / `interior.lobster_offshore_open_northumberland_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterOffshoreOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterOffshoreOpenNorthumberlandIso.asset) / `interior.lobster_offshore_open_northumberland_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopFundyIso.asset) / `interior.lobster_standard_hardtop_fundy_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopFundyIso.asset) / `interior.lobster_standard_hardtop_fundy_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopFundyIso.asset) / `interior.lobster_standard_hardtop_fundy_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.888; walk-blocking |
| [LobsterStandardHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopFundyIso.asset) / `interior.lobster_standard_hardtop_fundy_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopFundyIso.asset) / `interior.lobster_standard_hardtop_fundy_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.830; walk-blocking |
| [LobsterStandardHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopFundyIso.asset) / `interior.lobster_standard_hardtop_fundy_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.830; walk-blocking |
| [LobsterStandardHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopFundyIso.asset) / `interior.lobster_standard_hardtop_fundy_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopFundyIso.asset) / `interior.lobster_standard_hardtop_fundy_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNewfoundlandIso.asset) / `interior.lobster_standard_hardtop_newfoundland_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNewfoundlandIso.asset) / `interior.lobster_standard_hardtop_newfoundland_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNewfoundlandIso.asset) / `interior.lobster_standard_hardtop_newfoundland_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.739; walk-blocking |
| [LobsterStandardHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNewfoundlandIso.asset) / `interior.lobster_standard_hardtop_newfoundland_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNewfoundlandIso.asset) / `interior.lobster_standard_hardtop_newfoundland_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.930; walk-blocking |
| [LobsterStandardHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNewfoundlandIso.asset) / `interior.lobster_standard_hardtop_newfoundland_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.930; walk-blocking |
| [LobsterStandardHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNewfoundlandIso.asset) / `interior.lobster_standard_hardtop_newfoundland_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNewfoundlandIso.asset) / `interior.lobster_standard_hardtop_newfoundland_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNorthumberlandIso.asset) / `interior.lobster_standard_hardtop_northumberland_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNorthumberlandIso.asset) / `interior.lobster_standard_hardtop_northumberland_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNorthumberlandIso.asset) / `interior.lobster_standard_hardtop_northumberland_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.627; walk-blocking |
| [LobsterStandardHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNorthumberlandIso.asset) / `interior.lobster_standard_hardtop_northumberland_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNorthumberlandIso.asset) / `interior.lobster_standard_hardtop_northumberland_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 1.130; walk-blocking |
| [LobsterStandardHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNorthumberlandIso.asset) / `interior.lobster_standard_hardtop_northumberland_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 1.130; walk-blocking |
| [LobsterStandardHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNorthumberlandIso.asset) / `interior.lobster_standard_hardtop_northumberland_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardHardtopNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardHardtopNorthumberlandIso.asset) / `interior.lobster_standard_hardtop_northumberland_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenFundyIso.asset) / `interior.lobster_standard_open_fundy_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenFundyIso.asset) / `interior.lobster_standard_open_fundy_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenFundyIso.asset) / `interior.lobster_standard_open_fundy_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.888; walk-blocking |
| [LobsterStandardOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenFundyIso.asset) / `interior.lobster_standard_open_fundy_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenFundyIso.asset) / `interior.lobster_standard_open_fundy_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.830; walk-blocking |
| [LobsterStandardOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenFundyIso.asset) / `interior.lobster_standard_open_fundy_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.830; walk-blocking |
| [LobsterStandardOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenFundyIso.asset) / `interior.lobster_standard_open_fundy_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenFundyIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenFundyIso.asset) / `interior.lobster_standard_open_fundy_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNewfoundlandIso.asset) / `interior.lobster_standard_open_newfoundland_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNewfoundlandIso.asset) / `interior.lobster_standard_open_newfoundland_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNewfoundlandIso.asset) / `interior.lobster_standard_open_newfoundland_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.739; walk-blocking |
| [LobsterStandardOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNewfoundlandIso.asset) / `interior.lobster_standard_open_newfoundland_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNewfoundlandIso.asset) / `interior.lobster_standard_open_newfoundland_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.930; walk-blocking |
| [LobsterStandardOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNewfoundlandIso.asset) / `interior.lobster_standard_open_newfoundland_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 0.930; walk-blocking |
| [LobsterStandardOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNewfoundlandIso.asset) / `interior.lobster_standard_open_newfoundland_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenNewfoundlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNewfoundlandIso.asset) / `interior.lobster_standard_open_newfoundland_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNorthumberlandIso.asset) / `interior.lobster_standard_open_northumberland_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNorthumberlandIso.asset) / `interior.lobster_standard_open_northumberland_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNorthumberlandIso.asset) / `interior.lobster_standard_open_northumberland_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `cuddy_sole/v_berth` polygon, 4 vertices, AABB 1.800 x 0.627; walk-blocking |
| [LobsterStandardOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNorthumberlandIso.asset) / `interior.lobster_standard_open_northumberland_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNorthumberlandIso.asset) / `interior.lobster_standard_open_northumberland_iso` | `bunk_bench` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 1.130; walk-blocking |
| [LobsterStandardOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNorthumberlandIso.asset) / `interior.lobster_standard_open_northumberland_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 0.470 x 1.130; walk-blocking |
| [LobsterStandardOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNorthumberlandIso.asset) / `interior.lobster_standard_open_northumberland_iso` | `pantry_locker` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry_locker` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [LobsterStandardOpenNorthumberlandIso](../Assets/_Project/Data/Boats/Interiors/LobsterStandardOpenNorthumberlandIso.asset) / `interior.lobster_standard_open_northumberland_iso` | `cooler` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.cooler` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sink` | **NO VERB**, no component; declares `wash_dishes` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `toilet_master_bath` | **NO VERB**, no component; declares `toilet` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `toilet_guest_head` | **NO VERB**, no component; declares `toilet` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `toilet_vip_head` | **NO VERB**, no component; declares `toilet` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `toilet_crew_head` | **NO VERB**, no component; declares `toilet` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `dine_port` | **NO VERB**, no component; declares `dine` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `dine_stbd` | **NO VERB**, no component; declares `dine` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sleep_master` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sleep_guest_out` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sleep_guest_in` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sleep_vip` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sleep_crew_lower` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sleep_crew_upper` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_sofa_port` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_sofa_stbd` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_chair_aft` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_chair_mid` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_chair_fwd` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_settee` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_bench` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_armchair_port_aft` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_armchair_port_fwd` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_armchair_stbd_aft` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_armchair_stbd_fwd` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_lounger_port` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_lounger_stbd` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_master_settee` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [MotorYacht84Iso](../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) / `interior.motor_yacht84_iso` | `sit_crew_settee` | **NO VERB**, no component; declares `sit` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SideDraggerIso](../Assets/_Project/Data/Boats/Interiors/SideDraggerIso.asset) / `interior.side_dragger_iso` | `berth_aft` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SideDraggerIso](../Assets/_Project/Data/Boats/Interiors/SideDraggerIso.asset) / `interior.side_dragger_iso` | `berth_mid_aft` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SideDraggerIso](../Assets/_Project/Data/Boats/Interiors/SideDraggerIso.asset) / `interior.side_dragger_iso` | `berth_mid_fwd` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SideDraggerIso](../Assets/_Project/Data/Boats/Interiors/SideDraggerIso.asset) / `interior.side_dragger_iso` | `berth_fwd` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SideDraggerIso](../Assets/_Project/Data/Boats/Interiors/SideDraggerIso.asset) / `interior.side_dragger_iso` | `galley` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherConvertibleIso](../Assets/_Project/Data/Boats/Interiors/SportFisherConvertibleIso.asset) / `interior.sport_fisher_convertible_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherConvertibleIso](../Assets/_Project/Data/Boats/Interiors/SportFisherConvertibleIso.asset) / `interior.sport_fisher_convertible_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherConvertibleIso](../Assets/_Project/Data/Boats/Interiors/SportFisherConvertibleIso.asset) / `interior.sport_fisher_convertible_iso` | `locker_below` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherConvertibleIso](../Assets/_Project/Data/Boats/Interiors/SportFisherConvertibleIso.asset) / `interior.sport_fisher_convertible_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherConvertibleIso](../Assets/_Project/Data/Boats/Interiors/SportFisherConvertibleIso.asset) / `interior.sport_fisher_convertible_iso` | `bunk_starboard` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherConvertibleIso](../Assets/_Project/Data/Boats/Interiors/SportFisherConvertibleIso.asset) / `interior.sport_fisher_convertible_iso` | `bunk_vip` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherConvertibleIso](../Assets/_Project/Data/Boats/Interiors/SportFisherConvertibleIso.asset) / `interior.sport_fisher_convertible_iso` | `tub_port` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherConvertibleIso](../Assets/_Project/Data/Boats/Interiors/SportFisherConvertibleIso.asset) / `interior.sport_fisher_convertible_iso` | `tub_starboard` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherConvertibleIso](../Assets/_Project/Data/Boats/Interiors/SportFisherConvertibleIso.asset) / `interior.sport_fisher_convertible_iso` | `rods_port` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherConvertibleIso](../Assets/_Project/Data/Boats/Interiors/SportFisherConvertibleIso.asset) / `interior.sport_fisher_convertible_iso` | `rods_starboard` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherSkybridgeIso](../Assets/_Project/Data/Boats/Interiors/SportFisherSkybridgeIso.asset) / `interior.sport_fisher_skybridge_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherSkybridgeIso](../Assets/_Project/Data/Boats/Interiors/SportFisherSkybridgeIso.asset) / `interior.sport_fisher_skybridge_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherSkybridgeIso](../Assets/_Project/Data/Boats/Interiors/SportFisherSkybridgeIso.asset) / `interior.sport_fisher_skybridge_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherSkybridgeIso](../Assets/_Project/Data/Boats/Interiors/SportFisherSkybridgeIso.asset) / `interior.sport_fisher_skybridge_iso` | `bunk_port` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherSkybridgeIso](../Assets/_Project/Data/Boats/Interiors/SportFisherSkybridgeIso.asset) / `interior.sport_fisher_skybridge_iso` | `bunk_starboard` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherSkybridgeIso](../Assets/_Project/Data/Boats/Interiors/SportFisherSkybridgeIso.asset) / `interior.sport_fisher_skybridge_iso` | `bunk_vip` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherSkybridgeIso](../Assets/_Project/Data/Boats/Interiors/SportFisherSkybridgeIso.asset) / `interior.sport_fisher_skybridge_iso` | `head` | **NO VERB**, no component; declares `toilet` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherSkybridgeIso](../Assets/_Project/Data/Boats/Interiors/SportFisherSkybridgeIso.asset) / `interior.sport_fisher_skybridge_iso` | `tub_port` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherSkybridgeIso](../Assets/_Project/Data/Boats/Interiors/SportFisherSkybridgeIso.asset) / `interior.sport_fisher_skybridge_iso` | `tub_starboard` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherSkybridgeIso](../Assets/_Project/Data/Boats/Interiors/SportFisherSkybridgeIso.asset) / `interior.sport_fisher_skybridge_iso` | `rods_port` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SportFisherSkybridgeIso](../Assets/_Project/Data/Boats/Interiors/SportFisherSkybridgeIso.asset) / `interior.sport_fisher_skybridge_iso` | `rods_starboard` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SternTrawlerIso](../Assets/_Project/Data/Boats/Interiors/SternTrawlerIso.asset) / `interior.stern_trawler_iso` | `berth_aft_port` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SternTrawlerIso](../Assets/_Project/Data/Boats/Interiors/SternTrawlerIso.asset) / `interior.stern_trawler_iso` | `berth_aft_stbd` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SternTrawlerIso](../Assets/_Project/Data/Boats/Interiors/SternTrawlerIso.asset) / `interior.stern_trawler_iso` | `berth_fwd_port` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SternTrawlerIso](../Assets/_Project/Data/Boats/Interiors/SternTrawlerIso.asset) / `interior.stern_trawler_iso` | `berth_fwd_stbd` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SternTrawlerIso](../Assets/_Project/Data/Boats/Interiors/SternTrawlerIso.asset) / `interior.stern_trawler_iso` | `galley` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SternTrawlerMk2Iso](../Assets/_Project/Data/Boats/Interiors/SternTrawlerMk2Iso.asset) / `interior.stern_trawler_mk2_iso` | `berth_aft_port` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SternTrawlerMk2Iso](../Assets/_Project/Data/Boats/Interiors/SternTrawlerMk2Iso.asset) / `interior.stern_trawler_mk2_iso` | `berth_aft_stbd` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SternTrawlerMk2Iso](../Assets/_Project/Data/Boats/Interiors/SternTrawlerMk2Iso.asset) / `interior.stern_trawler_mk2_iso` | `berth_fwd_port` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SternTrawlerMk2Iso](../Assets/_Project/Data/Boats/Interiors/SternTrawlerMk2Iso.asset) / `interior.stern_trawler_mk2_iso` | `berth_fwd_stbd` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No individual Collider2D; no linked obstruction/size proven by anchor |
| [SternTrawlerMk2Iso](../Assets/_Project/Data/Boats/Interiors/SternTrawlerMk2Iso.asset) / `interior.stern_trawler_mk2_iso` | `galley` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [TankerIso](../Assets/_Project/Data/Boats/Interiors/TankerIso.asset) / `interior.tanker_iso` | `stove` | **NO VERB**, no component; declares `cook` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [TankerIso](../Assets/_Project/Data/Boats/Interiors/TankerIso.asset) / `interior.tanker_iso` | `locker` | **NO VERB**, no component; declares `storage` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [TankerIso](../Assets/_Project/Data/Boats/Interiors/TankerIso.asset) / `interior.tanker_iso` | `bunk` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `below_sole/bunk` polygon, 4 vertices, AABB 1.200 x 2.200; walk-blocking |
| [TankerIso](../Assets/_Project/Data/Boats/Interiors/TankerIso.asset) / `interior.tanker_iso` | `prep` | **NO VERB**, no component; declares `prep` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [TankerIso](../Assets/_Project/Data/Boats/Interiors/TankerIso.asset) / `interior.tanker_iso` | `bunk_starboard` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `below_sole/bunk_stbd` polygon, 4 vertices, AABB 1.200 x 2.200; walk-blocking |
| [TankerIso](../Assets/_Project/Data/Boats/Interiors/TankerIso.asset) / `interior.tanker_iso` | `bunk_forward` | Sleep / `BoatBunk`; **R: NO WALL CHECK** | No Collider2D; `below_sole/bunk_port` polygon, 4 vertices, AABB 1.200 x 2.200; walk-blocking |
| [TankerIso](../Assets/_Project/Data/Boats/Interiors/TankerIso.asset) / `interior.tanker_iso` | `seat` | **NO VERB**, no component; declares `sit` | No Collider2D; `house_sole/bench` polygon, 4 vertices, AABB 1.500 x 0.500; walk-blocking |
| [TankerIso](../Assets/_Project/Data/Boats/Interiors/TankerIso.asset) / `interior.tanker_iso` | `table` | **NO VERB**, no component; declares `table` | No Collider2D; `house_sole/table_port` polygon, 4 vertices, AABB 1.200 x 1.100; walk-blocking |
| [TankerIso](../Assets/_Project/Data/Boats/Interiors/TankerIso.asset) / `interior.tanker_iso` | `pantry` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.pantry` | No individual Collider2D; no linked obstruction/size proven by anchor |
| [TankerIso](../Assets/_Project/Data/Boats/Interiors/TankerIso.asset) / `interior.tanker_iso` | `fridge` | **NO VERB**, no component; declares `food_storage`; `kitchen_storage.fridge` | No individual Collider2D; no linked obstruction/size proven by anchor |

## Where reach is decided (quoted source)

Paths here are relative to `Assets/_Project/Code/`; line numbers refer only to
the pinned revision. The registry-based prefab verbs, both functional house
beds, the wardrobe, stairs and boat bunks above all go through **R**. No audited
fixture adds a wall query in its own `IsAvailable` or `Interact` method.

### R — shared fixture press, chooser and preview

`Core/Interaction/InteractResolver.Qualifies`, lines 117-132:

```csharp
if ((c.Contexts & actor.Context) == 0) return false;
// ... candidate availability ...
if (!c.IsAvailable) return false;
// ... horizontal reach ...
Vector2 delta = c.WorldPosition - actor.Position;
sqr = delta.sqrMagnitude;
float reach = Mathf.Max(0f, c.ReachMeters);
if (sqr > reach * reach) return false;
// ... facing ...
if (c.RequiresFacing && !InteractArc.Contains(delta, facing, cosHalfArc)) return false;
return true;
```

The omitted lines are comments/blank lines, not additional conditions.
`Player/ControlSwitcher.TryInteractCandidate` (1195) calls `InteractVerb.TryPress`.
That calls `BuildFor`, then `InteractChoiceList.Build`, which calls `Qualifies`
(172). `TryPerform` resolves directly through the same predicate.
`PublishCandidate` calls `TryResolveDefault`, and free-bunk substitution calls
`Qualifies` again in `FreeMateFor` (392). Thus selection, labels and chooser rows
share the same distance-only obstruction behaviour.

`Core/Interaction/InteractChooser.StillWorkable`, line 198:

```csharp
return InteractResolver.Qualifies(c, actor, actor.Facing, InteractArc.CosHalfArc(arc), out _);
```

It rechecks registration and occupation first, but does not add a wall test.
Finally `InteractVerb.Perform`, lines 105-110, dispatches without a spatial check:

```csharp
if (target == null) return;
if (target is INeedsGated gated && NeedsGate.TryRefuse(gated.NeedsRequirement)) return;

if (action >= 0 && target is IMultiActionInteractable multi) multi.Interact(actor, action);
else target.Interact(actor);
EventBus.Publish(new InteractPerformed(target.Id));
```

`InteractionGate` is a modal-input gate, not an architectural-wall gate.
`WashStationPoint`/`SitPoint` walking-away checks and `WardrobePicker.PlayerHasLeft`
also use distance; neither repairs the initial press. Ownership, occupation and
storey checks are meaningful restrictions but do not detect a wall on the same floor.

### L — the separate freezer key

`Player/GinnyFreezer.Update`, lines 179-182:

```csharp
var kb = Keyboard.current;
if (kb == null || !kb[_interactKey].wasPressedThisFrame) return;
if (!_reach.CanInteract(transform.position)) return;
Exchange();
```

`Economy/StallReach.CanInteract`, lines 69-70:

```csharp
Transform p = ResolvePlayer();
return p != null && StallGate.CanInteract(_onFoot, p.position, stallPosition, _range);
```

`Economy/StallGate`, lines 19-24:

```csharp
public static bool InRange(Vector2 player, Vector2 stall, float range)
    => Vector2.Distance(player, stall) <= range;

public static bool CanInteract(bool onFoot, Vector2 player, Vector2 stall, float range)
    => onFoot && InRange(player, stall, range);
```

This path never enters `InteractResolver`; fixing R alone would leave this
freezer's four-metre through-wall reach unchanged.

### C — merchant conversation access near counter bindings

`World/WorldInteractor.FindNearest`, lines 390-398:

```csharp
if (it == null || !it.isActiveAndEnabled) continue;
Vector2 delta = (Vector2)it.transform.position - p;
float sq = delta.sqrMagnitude;
if (sq > bestSq) continue;
// ... facing comment ...
if (!InteractArc.Contains(delta, facing, cosHalfArc)) continue;
```

This is **NO WALL CHECK** for active conversation targets, including a merchant
through whom a counter is used. Hidden/inactive NPCs are correctly rejected;
that does not imply an active merchant behind a partition is rejected.

## Existing coverage found, and limits

Before proposing any new test, the search found these existing suites under
`Assets/Tests/`: `EditMode/InteractVerbTests.cs` (inclusive reach, context,
availability and ranking), `InteractArcTests.cs`, `InteractChooserTests.cs`,
`InteractOfferTests.cs`, `Economy/StallGateTests.cs`,
`Washrooms/ApplianceWalkUpSortingTests.cs`, `Washrooms/WashStationPointTests.cs`,
`Washrooms/SitPointTests.cs`, `Kitchen/KitchenApplianceArtTests.cs`,
`Food/CookingStationContentTests.cs`, `StPetersInteriorsTests.cs`,
`InteriorFixturePlacementTests.cs`, `RigBaking/BoatInteriorKitchenTests.cs`,
`Kitchen/DiningAndSeatedRestTests.cs`, plus `PlayMode/InteractVerbPlayTests.cs`
and `InteractPopupPlayTests.cs`. No existing wall-rejection rule was found in the
audited resolver/fixture paths. These suites were read/searched, **not run**;
none was added, edited or weakened. The proposal extends them where appropriate.

## Ten gaps a player would notice first

Ranking is a design judgment from the source, not a measured playtest result.

1. **The fridge offers nothing.** A cabinet with food-storage data and door states
   has no open/store/take verb (`KitchenStoreFixture`).
2. **Presses reach through room walls.** Sleep, wardrobe, washing, sitting,
   cooking and stowing all pass R without an obstruction test; the chooser does too.
3. **The stove has no solid stop.** Its cooking verb works within 1 m, but neither
   its prefab nor runtime appliance setup gives it a collider.
4. **The prep counter and microwave have no solid stop.** The counter is a work
   surface without a movement footprint; a microwave also needs its supporting
   surface considered rather than blindly giving its entire picture a collider.
5. **Both catch-freezer prefabs have no solid footprint.** Their contents and
   capacity behaviours do not supply a physical box.
6. **The cottage freezer uses a separate, four-metre F-key action.** It has no
   shared verb label and would retain through-wall access after an R-only fix.
7. **House chairs cannot be sat on.** All 13 committed kit chairs are solid decor;
   a usable toilet seat does not establish usable ordinary seating.
8. **Some beds are silent.** Three kit beds have no sleep action or ownership
   refusal, while two otherwise similar beds do answer the press.
9. **Tables, dressers and sea chests promise more than they do.** Tables cannot
   support a dining action; ordinary dressers/chests offer no storage action.
   One dresser is a wardrobe and two chests are stairs, so shape alone is unreliable.
10. **Settees and other camper/cabin kitchen pieces remain declarations.** The
    camper settee's `sit` anchor and cabin food-storage/seat/table anchors do not
    instantiate usable fixtures. The camper's interior is also not stood. This
    ranks below the immediately present room problems; the missing shower solid
    is deliberately not ranked as a bug because stepping into it is intended.
