# Kitchens, bathrooms and missing buildings: checked inventory and build plan

Snapshot: `3a05aadf68e525de937104d926cd030854fd48c7`, 2026-11-19. This document supersedes the counts in the
[earlier placement plan](kitchens-bathrooms-placement-plan.md) and the [building inventory](nmc-buildings-inventory.md)
for this snapshot. Their proposed coordinates remain proposals.

The requirement is a kitchen and a bathroom in every residence or building that plausibly needs them.
Use north walls first, including the north side of a room partition. The later wall direction also allows
east/west fixtures and a readable south fixture; those are exceptions to record, not permission to infer a
facing from a position. The [wall plan](appliance-wall-plan.md#L1) preserves the wording and frame conversion.
This inventory applies the requirement across homes, shops, public buildings and workplaces. Layout and
appearance decisions remain for the building and art roles.

## 1. Inventory

### Counting and proof

Six current region scenes were checked as serialized text, together with their referenced prefabs:
StPeters, NineMileCreek, Cumberland, RicePoint, EastWater and SouthWater. Both native components and
`PrefabInstance.m_SourcePrefab` references were resolved. A source asset or rig alone does not prove a placement.
The retired prototype scenes are excluded from the population and all work below.

An **interior** needs a room and an indoor footprint, rather than just a component with that name.
**Seamless** below means the existing sprite room and footprint path, or its greybox equivalent; it does not
claim a playtest. A **rigged source** means a room exists in rig data but has not been installed as a dwelling.
ADR [0036](../adr/0036-interior-levels-as-layers.md) keeps storeys on one footprint with a real rise and stair;
ADR [0060](../adr/0060-rigged-dwelling-interiors.md) is **Proposed**, so its mesh dwelling path is not a shipped feature.

**Kitchen now** counts a bound cooking fixture, not a hearth, coffee display, room label or greybox block.
**Bathroom now** counts bound toilet/basin/bathing fixtures; a public toilet can have a different kit from a home.
An em dash for a wall means there is no installed fixture to measure. Compass readings refer to ground north,
not image up or the room's raw model `+y`. A source link in a row proves its identity/kind and placement state;
the component census and Def register below establish the fixture cells. Proposed rows have no placed fixture
even when their rig already draws one. Buildings and dwelling units are counted separately.

### Current interiors: every room-bearing building

| Region / site identity | Kind | Interior / levels | Kitchen now; wall | Bathroom now; wall | Evidence |
| --- | --- | --- | --- | --- | --- |
| St Peters / `IslandShops/generalStore` | shop | seamless; 1 layer | None; — | None; — | [scene](../../Assets/_Project/Scenes/StPeters.unity#L4240) |
| St Peters / `IslandVillage/redSaltbox` | home | seamless; 1 layer | None; — | None; — | [scene](../../Assets/_Project/Scenes/StPeters.unity#L19864) |
| St Peters / `building.stp_ginny_cottage` (lodging) | home | seamless; 2 layers | None; — | Toilet + sink + shower L0; S / E / S. Extra sink L1; E; privacy partition absent | [scene](../../Assets/_Project/Scenes/StPeters.unity#L20094) |
| St Peters / `IslandShops/postOffice` | shop | seamless; 1 layer | None; — | None; — | [scene](../../Assets/_Project/Scenes/StPeters.unity#L43323) |
| St Peters / `IslandVillage/school` | public | seamless; 1 layer | None; — | None; — | [scene](../../Assets/_Project/Scenes/StPeters.unity#L46608) |
| St Peters / `IslandVillage/sageCottage` | home | seamless; 1 layer | None; — | None; — | [scene](../../Assets/_Project/Scenes/StPeters.unity#L74285) |
| St Peters / `IslandVillage/whiteFarmhouse` | home | seamless; 1 layer | None; — | None; — | [scene](../../Assets/_Project/Scenes/StPeters.unity#L90720) |
| St Peters / `IslandVillage/creamSaltbox` | home | seamless; 2 layers | None; — | None; — | [scene](../../Assets/_Project/Scenes/StPeters.unity#L140182) |
| St Peters / `IslandVillage/harbourGable` | home | seamless; 2 layers | None; — | None; — | [scene](../../Assets/_Project/Scenes/StPeters.unity#L140968) |
| St Peters / `IslandVillage/whiteGable` | home | seamless; 2 layers | None; — | None; — | [scene](../../Assets/_Project/Scenes/StPeters.unity#L141754) |
| NMC / `NmcFishPlant` | plant | seamless greybox; 1 layer; staff rooms closed | None; — | None; — | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L438076); [prefab](../../Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab#L1728) |
| NMC / `NmcCommunityHall` | public | seamless greybox; 1 layer; staff rooms closed | None; — | None; — | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L438177); [prefab](../../Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcCommunityHall.prefab#L596) |
| NMC / `NmcBoatGearWholesaler` | shop | seamless greybox; 1 layer; trade floor | Microwave marker only; N | Toilet + sink markers only; NE / N | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L438243); [prefab](../../Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcBoatGearWholesaler.prefab#L669) |
| NMC / `NmcFishingGearWholesaler` | shop | seamless greybox; 1 layer; trade floor | Microwave marker only; E | Toilet + sink markers only; NE / N | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L438309); [prefab](../../Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishingGearWholesaler.prefab#L3688) |

There are **14** room-bearing buildings: St Peters **10** (7 homes, 2 shops, 1 school), NMC **4**
(1 plant, 2 shops, 1 hall including the gym). All 14 have **zero installed kitchens**. Only the lodging
has a bathroom fixture set: **13** have no functional bathroom. Its privacy partition is still missing.
The wholesalers contribute **six unbound markers**, not two kitchens and two working bathrooms.

The fuel canopy is a separate **one-component false positive**: its footprint spans the canopy,
`_facings: 1`, `_groundDepthScale: 1`, zero wall thickness and no doorway. It is a see-through device,
not a C-store interior ([scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L352909)).
The store piece exists beside it, but no installed room is proved by the unused `interior_sStore` prefab.

### Installed fixtures and Def register

| Instance / region | Level / wall | Prefab binding | Placement proof |
| --- | --- | --- | --- |
| `fixture.st_peters.ginny_cottage.washstand` | L1 / E | `washstation.sink` [binding](../../Assets/_Project/Prefabs/Washrooms/WashStand.prefab#L167) | [scene](../../Assets/_Project/Scenes/StPeters.unity#L161608) |
| `fixture.st_peters.ginny_cottage.toilet` | L0 / S | `washstation.toilet` [binding](../../Assets/_Project/Prefabs/Washrooms/Toilet.prefab#L75) | [scene](../../Assets/_Project/Scenes/StPeters.unity#L161674) |
| `fixture.st_peters.ginny_cottage.bathroom_sink` | L0 / E | `washstation.sink` [binding](../../Assets/_Project/Prefabs/Washrooms/WashStand.prefab#L167) | [scene](../../Assets/_Project/Scenes/StPeters.unity#L161740) |
| `fixture.st_peters.ginny_cottage.shower` | L0 / S | `washstation.shower` [binding](../../Assets/_Project/Prefabs/Washrooms/Shower.prefab#L222) | [scene](../../Assets/_Project/Scenes/StPeters.unity#L161806) |
| `fixture.nmc.wharf.shower` | outside / no room wall | `washstation.shower` [binding](../../Assets/_Project/Prefabs/Washrooms/WharfShower.prefab#L167) | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L437978) |

Exact installed total: **5 fixture instances**, comprising **2 basins, 2 showers, 1 toilet**;
4 are indoors at the lodging and 1 shower is outdoors on the NMC wharf. **0** cooking fixtures,
**0** baths and **0** outhouses are installed in the six region scenes. A toilet is a `SitPoint`,
not a wash action. The walls above follow the serialized positions and the
[room-frame conversion](appliance-wall-plan.md#L52); no new visual judgment was made.

| Def identity | Committed data | Prefab / use |
| --- | --- | --- |
| `cooking_station.prep_counter` | [Assets/_Project/Data/Resources/CookingStations/PrepCounter.asset](../../Assets/_Project/Data/Resources/CookingStations/PrepCounter.asset#L15) | [PrepCounter](../../Assets/_Project/Prefabs/Props/Kitchen/PrepCounter.prefab#L1); none placed |
| `cooking_station.stove` | [Assets/_Project/Data/Resources/CookingStations/Stove.asset](../../Assets/_Project/Data/Resources/CookingStations/Stove.asset#L15) | [Stove](../../Assets/_Project/Prefabs/Props/Kitchen/Stove.prefab#L1); none placed |
| `cooking_station.microwave` | [Assets/_Project/Data/Resources/CookingStations/Microwave.asset](../../Assets/_Project/Data/Resources/CookingStations/Microwave.asset#L15) | [Microwave](../../Assets/_Project/Prefabs/Props/Kitchen/Microwave.prefab#L1); none placed |
| `washstation.sink` | [Assets/_Project/Data/Resources/WashStations/Sink.asset](../../Assets/_Project/Data/Resources/WashStations/Sink.asset#L15) | [WashStand](../../Assets/_Project/Prefabs/Washrooms/WashStand.prefab#L1) |
| `washstation.shower` | [Assets/_Project/Data/Resources/WashStations/Shower.asset](../../Assets/_Project/Data/Resources/WashStations/Shower.asset#L15) | [Shower](../../Assets/_Project/Prefabs/Washrooms/Shower.prefab#L1) |
| `washstation.bath` | [Assets/_Project/Data/Resources/WashStations/Bath.asset](../../Assets/_Project/Data/Resources/WashStations/Bath.asset#L15) | [Bath](../../Assets/_Project/Prefabs/Washrooms/Bath.prefab#L1) |
| `washstation.toilet` | [Assets/_Project/Data/Resources/WashStations/Toilet.asset](../../Assets/_Project/Data/Resources/WashStations/Toilet.asset#L15) | [Toilet](../../Assets/_Project/Prefabs/Washrooms/Toilet.prefab#L1) |
| `washroom.outhouse` | [Outhouse.asset](../../Assets/_Project/Data/Resources/Washrooms/Outhouse.asset#L15) | Prefab reference is populated; not placed |

All **7** standard indoor station types already have Defs (3 cooking, 4 bathroom), and the outhouse type
has its prefab reference. The outstanding issue is generally placement/binding, not seven new Defs.
`washstation.sea` also exists but is not an indoor bathroom. Soap exists as
[`supply.soap_bar`](../../Assets/_Project/Data/Resources/Catalog/Supplies/SoapBar.asset#L15).
Food stores exist as `kitchen_storage.fridge`, `.pantry`, `.pantry_locker`, `.cooler`,
`.camper_fridge`, `.camper_pantry` in
[`Data/Resources/Kitchen/Storage`](../../Assets/_Project/Data/Resources/Kitchen/Storage).
Catch/gear storage has its own Defs and site keys in
[`HomeStoragePlan`](../../Assets/_Project/Code/App/Editor/HomeStoragePlan.cs#L71).
Those records do not prove that a room, pantry or cooker is stood. The one current land catch freezer
is outside the lodging ([scene](../../Assets/_Project/Scenes/StPeters.unity#L491)).

### Existing shells and unbuilt St Peters residences

No row in this table has an installed kitchen or bathroom; both walls are **—**. Existing shells have no
room-bearing interior; the two shop flats and keeper dwelling have none yet. These are not added to the
14-interior total. Scene paths are identities where no stable building/unit id has been authored.

| Region / id or scene site | Kind | Interior now / source | Kitchen / bathroom data to bind | Evidence |
| --- | --- | --- | --- | --- |
| St Peters / `home.ginny_lot_camper` | home | none; shell, HomeDoor refuses entry; Clipper room rig exists | prep, stove; galley sink; wet-bath sink/toilet/shower; fridge/pantry/freezer | [scene](../../Assets/_Project/Scenes/StPeters.unity#L31964) |
| St Peters / `generalStore` upper flat (unit id absent) | home | none; shop ground room only | full home kit | [scene](../../Assets/_Project/Scenes/StPeters.unity#L4240) |
| St Peters / `postOffice` upper flat (unit id absent) | home | none; shop ground room only | full home kit | [scene](../../Assets/_Project/Scenes/StPeters.unity#L43323) |
| NMC / `GeneralStore` | shop | none; legacy exterior / gear counter | staff microwave, toilet, sink | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L41294) |
| NMC / `CreekHouses/redSaltbox` | home | none; legacy flavour shell | full home kit | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L26030) |
| NMC / `CreekHouses/sageCottage` | home | none; legacy flavour shell | full home kit | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L222900) |
| NMC / `structure.nmc_harbourmaster` | public | none; set-piece exterior | staff microwave + toilet/sink | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L374207) |
| NMC / `structure.nmc_fish_market` | shop | none; set-piece exterior | staff microwave + toilet/sink | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L374395) |
| NMC / `structure.nmc_restaurant` | shop | none; set-piece exterior | commercial prep/stove + customer toilet/sink | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L374579) |
| NMC / `structure.nmc_cold_store` | plant | none; set-piece exterior | staff microwave + toilet/sink + shower | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L376362) |
| NMC / `station.nmc_store` | shop | none; C-store exterior, canopy cutaway only | staff microwave + customer toilet/sink | [scene](../../Assets/_Project/Scenes/NineMileCreek.unity#L353145) |
| St Peters / `station.stp_ne_light` | home/public | none; keeper dwelling source, key scene not placed | full home kit | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_stp_ne_light.asset#L22) |

The Clipper's [room sidecar](../art/rigs/camper-iso-kit/camperInteriorRig.clipper.interior.json) has 14 interaction
anchors. Its head explicitly names sink/toilet/shower Defs, its stores name storage Defs, but `stove` and `prep`
do not carry a station Def id. `galley_sink` uses `wash_dishes`, which is not the same verb as washing the body.
The [small-shed sidecar](../art/rigs/small-shed-kit/smallShedRig.lot_shed.gameplay.json) is gear storage, not a second
home or bathroom. Neither source proves an installed dwelling. See the [proposed dwelling contract](../adr/0060-rigged-dwelling-interiors.md#L167).
The shop flats are planned by the [placement plan](kitchens-bathrooms-placement-plan.md#L103); they have no unit
ids or upper room references. Do not count the ground-floor room as their kitchen.

### NMC: every unplaced building and camper in key-scene data

Every row below is **none yet** for its installed interior, kitchen and bathroom; kitchen/bath walls are **—**.
The cited `KeySceneDef` has `Placed: 0`. The source/piece column records reusable exterior or room source, not
availability in play. The east-block lot already carries the boat-gear wholesaler: integrate that one site,
rather than adding a second building on it. The new table includes mixed-use blocks and mobile residences,
which the earlier count omitted. K = full home kitchen; B = home bathroom; M = staff microwave;
T/S = toilet and sink; C = commercial prep and stove; shared = shared campsite facilities pending the fit decision.

| Id | Kind / use | Source piece | Required kitchen / bath | Proof |
| --- | --- | --- | --- | --- |
| `camper.nmc_camp_a5` | home (seasonal camper) | `boxCamper` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L23) |
| `camper.nmc_camp_a6` | home (seasonal camper) | `CamperIso` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L340) |
| `camper.nmc_camp_a7` | home (seasonal camper) | `fifthWheel` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L615) |
| `camper.nmc_camp_a8` | home (seasonal camper) | `boxCamper` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L883) |
| `camper.nmc_camp_a9` | home (seasonal camper) | `popup` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L1158) |
| `camper.nmc_camp_a10` | home (seasonal camper) | `CamperIso` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L1426) |
| `camper.nmc_camp_a11` | home (seasonal camper) | `boxCamper` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L1706) |
| `camper.nmc_camp_a20` | home (seasonal camper) | `boxCamper` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L2238) |
| `camper.nmc_camp_a21` | home (seasonal camper) | `fifthWheel` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L2468) |
| `camper.nmc_camp_a22` | home (seasonal camper) | `boxCamper` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L2695) |
| `camper.nmc_camp_a23` | home (seasonal camper) | `popup` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L2966) |
| `camper.nmc_camp_a24` | home (seasonal camper) | `CamperIso` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L3193) |
| `camper.nmc_camp_a25` | home (seasonal camper) | `boxCamper` | galley by size / shared or wet bath; decision Q5 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L3428) |
| `structure.nmc_camp_comfort_a` | public wash block | `comfortStation` | staff M if staffed / T/S + showers | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L3657) |
| `structure.nmc_camp_pool` | public / pool house | `poolArea` | staff M if staffed / changing T/S + shower | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L7832) |
| `structure.nmc_main_street_outfitter` | shop / upper flats where declared | `msBlock` | M + each flat K / T/S + each flat B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L22) |
| `structure.nmc_main_street_flowers` | shop / upper flats where declared | `msBlock` | M + each flat K / T/S + each flat B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L153) |
| `structure.nmc_main_street_antiques` | shop / upper flats where declared | `msBlock` | M + each flat K / T/S + each flat B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L226) |
| `structure.nmc_main_street_west_block` | shop / upper flats where declared | `msBlock` | M + each flat K / T/S + each flat B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L299) |
| `structure.nmc_main_street_corner_block` | shop / upper flats where declared | `msBlock` | bakery C; other bays M; flats K / T/S + flat B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L376) |
| `structure.nmc_main_street_gallery` | shop / upper flats where declared | `msBlock` | M + each flat K / T/S + each flat B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L462) |
| `structure.nmc_main_street_pharmacy` | shop / upper flats where declared | `msBlock` | M + each flat K / T/S + each flat B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L539) |
| `structure.nmc_main_street_wine` | shop / upper flats where declared | `msBlock` | M + each flat K / T/S + each flat B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L614) |
| `structure.nmc_main_street_visitor` | public visitor centre | `msBlock` | M / public T/S | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L685) |
| `structure.nmc_main_street_post_office` | public post office | `msBlock` | M / T/S | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L765) |
| `structure.nmc_main_street_east_corner` | shop / upper flats where declared | `msBlock` | M + each flat K / T/S + each flat B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L840) |
| `structure.nmc_main_street_east_block` | shop / upper flats; wholesaler site overlay | `msBlock` | M + each flat K / T/S + each flat B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L920) |
| `structure.nmc_main_street_east_decker` | home (unmapped flats) | `msBlock` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L997) |
| `structure.nmc_main_street_east_captains` | home (unmapped flats) | `msBlock` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L1075) |
| `structure.nmc_main_street_east_row` | home (unmapped flats) | `msBlock` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L1152) |
| `structure.nmc_restaurant_bar` | shop (food trade) | `ManorIso` | C / customer T/S | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L1228) |
| `structure.nmc_chandlery` | shop | `chandlery` | M / T/S | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L5138) |
| `structure.nmc_waterfront_house_1` | home | `creekHouse` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_north_creek.asset#L22) |
| `structure.nmc_waterfront_house_2` | home | `creekHouse` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_north_creek.asset#L227) |
| `structure.nmc_waterfront_house_3` | home | `creekHouse` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_north_creek.asset#L312) |
| `structure.nmc_waterfront_house_4` | home | `creekHouse` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_north_creek.asset#L395) |
| `structure.nmc_waterfront_house_5` | home | `creekHouse` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_north_creek.asset#L478) |
| `structure.nmc_waterfront_house_6` | home | `creekHouse` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_north_creek.asset#L553) |
| `structure.nmc_waterfront_house_7` | home | `creekHouse` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_north_creek.asset#L625) |
| `structure.nmc_plaza_grocery` | shop (food trade) | `grocery` | C / customer T/S | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_plaza.asset#L22) |
| `structure.nmc_plaza_lumber` | shop | `lumber` | M / T/S | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_plaza.asset#L139) |
| `prop.nmc_yard_gatehouse` | shop | `yardOffice` | M / T/S | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_plaza.asset#L1057) |
| `structure.nmc_plaza_cannery` | home (multi-unit) | `WalkupIso` | K / B per unit; expanded below | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_plaza.asset#L3335) |
| `structure.nmc_plaza_walkup` | home (multi-unit) | `WalkupIso` | K / B per unit; expanded below | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_plaza.asset#L3400) |
| `structure.nmc_plaza_terrace` | home (multi-unit) | `RowhouseIso` | K / B per unit; expanded below | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_plaza.asset#L3465) |
| `structure.nmc_shore_lane_6` | home | `saltbox` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_shore_lane.asset#L22) |
| `structure.nmc_shore_lane_7` | home | `summerCottage` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_shore_lane.asset#L177) |
| `structure.nmc_shore_lane_8` | home | `gardenCape` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_shore_lane.asset#L275) |
| `structure.nmc_south_shore_house_1` | home | `estateHouse` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_south_shore.asset#L22) |
| `structure.nmc_south_shore_house_2` | home | `estateHouse` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_south_shore.asset#L250) |
| `structure.nmc_south_shore_house_3` | home | `estateHouse` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_south_shore.asset#L414) |
| `structure.nmc_south_shore_house_4` | home | `estateHouse` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_south_shore.asset#L513) |
| `structure.nmc_south_shore_house_5` | home | `estateHouse` | K / B | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_south_shore.asset#L613) |
| `structure.nmc_south_shore_house_6` | home; use of “pool house” unresolved | `estateHouse` | K / B provisionally; settle Q6 | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_south_shore.asset#L712) |

This is **41 building/site records** and **13 camper records**. One building record is the already-occupied east-block lot, so it is not a fresh exterior. These are source records, not a total of finished homes.

### Every known plaza dwelling unit

The three multi-unit records above contain **20** separately identified source units. They are expanded here so
one bathroom for an entire block cannot pass. Each qualified identity below is a source address, **not a new
global Def id**. All have **none yet** for a placed rigged interior, kitchen, bathroom and compass wall.
The local rig has cooking, dish-washing, fridge, toilet and vanity anchors; some also have bathing anchors.
`INTERACT` supplies actions/verbs/hosts but no `defId` or station field. Unit `baths` is a design count and
does not prove a usable shower/bath. Map every anchor before placing; do not manufacture missing anchors from that count.

| Building / source unit | Kind | Room source / declared baths | Kitchen / bath fixtures as source data | Evidence |
| --- | --- | --- | --- | --- |
| `structure.nmc_plaza_cannery/unit_1` | home | rigged source; 2 declared | cook / dish-sink / fridge; toilet / vanity; 2 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.cannery8Loft.gameplay.json#L124) |
| `structure.nmc_plaza_cannery/unit_2` | home | rigged source; 2 declared | cook / dish-sink / fridge; toilet / vanity; 2 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.cannery8Loft.gameplay.json#L189) |
| `structure.nmc_plaza_cannery/unit_3` | home | rigged source; 2 declared | cook / dish-sink / fridge; toilet / vanity; 2 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.cannery8Loft.gameplay.json#L254) |
| `structure.nmc_plaza_cannery/unit_4` | home | rigged source; 2 declared | cook / dish-sink / fridge; toilet / vanity; 2 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.cannery8Loft.gameplay.json#L319) |
| `structure.nmc_plaza_cannery/unit_5` | home | rigged source; 2 declared | cook / dish-sink / fridge; toilet / vanity; 2 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.cannery8Loft.gameplay.json#L384) |
| `structure.nmc_plaza_cannery/unit_6` | home | rigged source; 2 declared | cook / dish-sink / fridge; toilet / vanity; 2 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.cannery8Loft.gameplay.json#L455) |
| `structure.nmc_plaza_cannery/unit_7` | home | rigged source; 2 declared | cook / dish-sink / fridge; toilet / vanity; 2 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.cannery8Loft.gameplay.json#L526) |
| `structure.nmc_plaza_cannery/unit_8` | home | rigged source; 2 declared | cook / dish-sink / fridge; toilet / vanity; 2 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.cannery8Loft.gameplay.json#L597) |
| `structure.nmc_plaza_walkup/unit_1` | home | rigged source; 1 declared | cook / dish-sink / fridge; toilet / vanity; 1 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.walkup8.gameplay.json#L124) |
| `structure.nmc_plaza_walkup/unit_2` | home | rigged source; 1 declared | cook / dish-sink / fridge; toilet / vanity; 1 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.walkup8.gameplay.json#L187) |
| `structure.nmc_plaza_walkup/unit_3` | home | rigged source; 1 declared | cook / dish-sink / fridge; toilet / vanity; 1 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.walkup8.gameplay.json#L250) |
| `structure.nmc_plaza_walkup/unit_4` | home | rigged source; 1 declared | cook / dish-sink / fridge; toilet / vanity; 1 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.walkup8.gameplay.json#L313) |
| `structure.nmc_plaza_walkup/unit_5` | home | rigged source; 1 declared | cook / dish-sink / fridge; toilet / vanity; 1 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.walkup8.gameplay.json#L376) |
| `structure.nmc_plaza_walkup/unit_6` | home | rigged source; 1 declared | cook / dish-sink / fridge; toilet / vanity; 1 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.walkup8.gameplay.json#L439) |
| `structure.nmc_plaza_walkup/unit_7` | home | rigged source; 1 declared | cook / dish-sink / fridge; toilet / vanity; 1 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.walkup8.gameplay.json#L502) |
| `structure.nmc_plaza_walkup/unit_8` | home | rigged source; 1 declared | cook / dish-sink / fridge; toilet / vanity; 1 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.walkup8.gameplay.json#L565) |
| `structure.nmc_plaza_terrace/unit_1` | home | rigged source; 2 declared | cook / dish-sink / fridge; toilet / vanity; 3 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/rowhouse/gameplay/rowhouseIsoRig.coastalRow4.gameplay.json#L82) |
| `structure.nmc_plaza_terrace/unit_2` | home | rigged source; 2 declared | cook / dish-sink / fridge; toilet / vanity; 3 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/rowhouse/gameplay/rowhouseIsoRig.coastalRow4.gameplay.json#L136) |
| `structure.nmc_plaza_terrace/unit_3` | home | rigged source; 2 declared | cook / dish-sink / fridge; toilet / vanity; 3 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/rowhouse/gameplay/rowhouseIsoRig.coastalRow4.gameplay.json#L191) |
| `structure.nmc_plaza_terrace/unit_4` | home | rigged source; 2 declared | cook / dish-sink / fridge; toilet / vanity; 3 bathe anchors | [unit](../../docs/art/rigs/multiunit-kit/rowhouse/gameplay/rowhouseIsoRig.coastalRow4.gameplay.json#L246) |

The main-street blocks also declare upper floors and flats in the
[main-street rig](../art/rigs/main-street/nmcMainStreet.js#L6) and each key-scene row's `fl`/`bays` options.
Those are exterior floors/windows, not a residence roster. **The count and ids of those flats remain unknown**:
do not infer a unit per window, shop bay or storey. The 20-unit total above excludes them and the two unbuilt
St Peters shop flats. The three-decker, captain flats and east terrace need a source-unit map too; the main-street
record is not currently bound to a multi-unit room sidecar. Stable site ids must survive that reconciliation.

### Cumberland: every building record in the ground plan

All rows are **none yet** for a built interior, kitchen, bathroom and compass wall. Door bearings in the
ground plan are not fixture bearings. Barns, sheds and the stage are retained as explicit exceptions.

| Id | Kind | Required kitchen / bathroom | Proof |
| --- | --- | --- | --- |
| `structure.cmb_float_bar` | shop / hospitality | commercial prep + stove / customer T/S; use marina toilets | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L714) |
| `structure.cmb_float_stage` | outbuilding / stage | none; shared facilities | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L721) |
| `structure.cmb_marina_office` | public/shop | staff microwave / T/S + showers | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L1241) |
| `structure.cmb_marina_shed` | outbuilding / stage | none; shared facilities | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L1246) |
| `structure.cmb_store` | shop / hospitality | commercial prep + stove / customer T/S | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L1806) |
| `structure.cmb_hall` | public | prep + microwave / T/S | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L1828) |
| `structure.cmb_church` | public | kitchen need unresolved / T/S | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L1850) |
| `structure.cmb_house_1` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L1872) |
| `structure.cmb_house_2` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L1893) |
| `structure.cmb_house_3` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L1914) |
| `structure.cmb_house_4` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L1935) |
| `structure.cmb_house_5` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L1956) |
| `structure.cmb_route_house_1` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L1979) |
| `structure.cmb_route_house_2` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L2000) |
| `structure.cmb_south_farmhouse` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L2023) |
| `structure.cmb_south_farm_barn` | outbuilding / stage | none; shared facilities | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L2044) |
| `structure.cmb_north_farmhouse` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L2065) |
| `structure.cmb_north_farm_barn` | outbuilding / stage | none; shared facilities | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L2086) |
| `structure.cmb_resort_lodge` | shop / hospitality | commercial prep + stove / customer T/S | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L2110) |
| `structure.cmb_resort_cottage_1` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L2132) |
| `structure.cmb_resort_cottage_2` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L2153) |
| `structure.cmb_resort_cottage_3` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L2174) |
| `structure.cmb_resort_cottage_4` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L2195) |
| `structure.cmb_clubhouse` | shop / hospitality | commercial prep + stove / customer T/S | [ground record](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json#L2262) |

Cumberland has **24 unique structure records**, of which **20** need their own kitchen/bathroom assessment;
4 are non-residential exceptions (2 barns, the marina shed, the float stage). There are **13** individual
residential building records, excluding rooms within the lodge. Both for-sale homes are in this table.

### Rice Point: every residential, work and staged building record

All rows are **none yet** for a built interior, kitchen, bathroom and fixture wall. Later stages remain
planned, not open to the player. Reopening the wharf house as a bunkhouse is a use change at the same site.

| Id / site | Kind / stage | Required kitchen / bathroom | Proof |
| --- | --- | --- | --- |
| `structure.rpt_strand_house` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L4497) |
| `structure.rpt_cove_house` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L4521) |
| `structure.rpt_brook_house` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L4543) |
| `structure.rpt_marsh_cottage` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L4565) |
| `structure.rpt_wharf_house` | home; boarded today, bunkhouse stage 2 | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L4588) |
| `structure.rpt_point_house` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L4614) |
| `structure.rpt_back_shore_camp` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L4638) |
| `structure.rpt_farmhouse` | home | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L4662) |
| `structure.rpt_farm_barn` | outbuilding | none; farmhouse facilities | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L4684) |
| `structure.rpt_s3_staff_house_1` | home; stage 3 | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L4709) |
| `structure.rpt_s3_staff_house_2` | home; stage 3 | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L4732) |
| `structure.rpt_s3_skipper_house` | home; stage 3 | full home kitchen / toilet, sink, shower or bath | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L4755) |
| `shed.rpt_store` | shop/office; reopens stage 1 | staff microwave / T/S | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L1185) |
| `shed.rpt_head_shed` | outbuilding; rebuilt stage 1 | none; shared facilities | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L1196) |
| `shed.rpt_s2_bait_shed` | outbuilding; stage 2 | none unless it becomes a staffed trade; shared facilities | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L2932) |
| `shed.rpt_s3_cold_store` | plant/workplace; stage 3 | staff microwave / T/S + shower | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L3673) |
| `railway.rpt_s3_railway` | winch-house site; stage 3; separate building id absent | none; shared facilities | [ground record](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json#L3673) |

Rice Point has **17 building/site records** in this assessment (12 structures, 4 sheds and the railway's
winch-house site): **11 residential sites**, 2 other workplaces needing facilities, and 4 non-residential
exceptions. The farmhouse's description mentions a north kitchen ell; that is a proposed exterior feature,
not an installed kitchen. The point-house lot explicitly says `faces: S`, resolving the older wall plan's
uncertain door bearing; a future interior must still verify its actual rig frame.

### Remaining regions, non-residential pieces and identity traps

| Region or population | Checked state / treatment | Proof |
| --- | --- | --- |
| EastWater, SouthWater | No room-bearing building components, HomeDoor, cooking or wash fixture instances in either scene | [EastWater scene](../../Assets/_Project/Scenes/EastWater.unity), [SouthWater scene](../../Assets/_Project/Scenes/SouthWater.unity) |
| WestWater | RegionDef names a scene that is absent from the committed region-scene set; no authored building inventory to place | [RegionDef](../../Assets/_Project/Data/Regions/WestWater.asset#L15) |
| Coddle Cove | Home cottage is a canon plan, but its RegionDef still targets the retired prototype; no current-region cottage is established by this audit | [RegionDef](../../Assets/_Project/Data/Regions/CoddleCove.asset#L15), [region canon](../vision-and-pillars.md#L79) |
| Other named future regions | No additional committed region scenes or building-instance records in this snapshot. Named port/yard ambitions are design scope, not a list of placed residences | [region canon](../vision-and-pillars.md#L79), [world plan](../design/world-map-plan.md) |
| St Peters cannery | Derelict now; restored variant is the same site and must get staff kitchen + T/S + shower when it opens, not a second counted building | [cannery rows](../../Assets/_Project/Data/KeyScenes/KeyScene_stp_cannery.asset#L22) |
| St Peters storage outbuildings | Lodging's woodshed/net store/lean-to, east-end net loft/net shed/ice house, landing bait store: no private kitchen/bath by current rule | [plot](../../Assets/_Project/Data/KeyScenes/KeyScene_stp_ginnys_plot.asset#L124), [east end](../../Assets/_Project/Data/KeyScenes/KeyScene_stp_east_end.asset#L23), [landing](../../Assets/_Project/Data/KeyScenes/KeyScene_stp_landing.asset#L76) |
| NMC fleet/yard stores | Eight fleet sheds, bait shed, trap store and boat-yard shed are work/storage exceptions; the cold store remains an assessed workplace | [wharf source](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.asset#L1429), [earlier rule](kitchens-bathrooms-placement-plan.md#L160) |
| NMC excluded key-scene pieces | Tennis court, detached garage, garden/lumber sheds, square kiosk, bandstand and bus shelter are not indoor residences. Keep shared wash access in the district plan | [campground](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L7791), [north creek](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_north_creek.asset#L699), [shore lane](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_shore_lane.asset#L849), [plaza](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_plaza.asset#L896), [main street](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L1462) |
| NMC campground tents | Tent-only serviced pitch A19 and 17 tent pitches are outdoor accommodation, not 18 indoor bathrooms; comfort station must serve them | [camp layout options](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L74) |
| NMC old town lots | `HouseNorth`, `HouseNorthWest`, `HouseSouth` and `ParishHall` are transform-only site markers at their roots, not four extra interiors. The hall greybox occupies the parish site | [house marker](../../Assets/_Project/Scenes/NineMileCreek.unity#L288530), [hall marker](../../Assets/_Project/Scenes/NineMileCreek.unity#L158658), [lot plan](../design/nine-mile-creek-mainland.md#L130) |
| NMC relocated farmhouse / Mill Road house | Explicit planned household sites need an identity reconciliation with the old lots before they can be added to the unique-building count. The south-lot farmhouse is moved, not copied | [key-scene decision 8](../design/nmc-key-scenes/README.md#L190), [named missing sites](nmc-buildings-inventory.md#L55) |
| NMC older over-trade homes | The population plan proposes homes over the store, tavern and chandlery, plus harbour-office quarters. None has a unit id, installed room, kitchen, bathroom or fixture wall. Reconcile these four proposals with the newer mixed-use roster, rather than count four additional houses | [population housing table](../design/settlement-population.md#L337) |

The eight purchasable HomeDefs resolve to **one current interior** (St Peters red saltbox), **one camper
shell**, and **six unbuilt regional houses** (three NMC, two Cumberland, one Rice Point). The latter are
already listed above; the HomeDefs do not add eight more buildings. See
[`Data/Homes`](../../Assets/_Project/Data/Homes) and [HomeDef.BuildingKey](../../Assets/_Project/Code/Economy/HomeDef.cs#L109).

**Totals are deliberately separate:** 14 current interiors; 5 installed washing/sitting fixtures;
0 kitchens; 1 bathroom fixture set; 6 wholesaler markers; 20 explicitly enumerated unplaced plaza units;
13 unplaced campers; 24 Cumberland and 17 Rice Point building/site records. The NMC source-table count is
given above. These totals must not be added: sources overlap current shells, source units belong inside
building records, and mixed-use flats and unresolved lots still lack a complete roster. A single exact
worldwide dwelling total would invent information. Completing that roster is slice D1 below.

## 2. Gaps and the smallest closing change

Each group below refers to the individual identities in section 1; it does not permit one shared home kitchen
or one bathroom per apartment block. **Missing** means absent from the snapshot, **planned** means only a
source row exists, and **unbound** means geometry exists but no gameplay station is attached. All scene work
uses the named refresh/patch path, one writer per scene. Moving a fixture, partition or furniture changes its
appearance and belongs to the building/art roles. No such work is performed by this audit.

| Gap / exact affected set | Missing or wrong now | Smallest closing change | Owning role / proof |
| --- | --- | --- | --- |
| G1: lodging cottage | No kitchen; ground toilet and shower are on S, basin and upstairs basin E; bathroom lacks its closet | Bind prep/stove/microwave on N; select a north-backed bathroom layout; add the closet with a usable approach. Keep its fixture ids and the existing upstairs basin distinct | world-content; art-director for layout, art-pipeline for presentation. [plan rows](../../Assets/_Project/Code/App/Editor/RoomFixturePlan.cs#L178), [wall options](appliance-wall-plan.md#L177) |
| G2: `redSaltbox` | No kitchen or bathroom. Planned toilet/shower face the south-side problem; no fixture facing is encoded | Use the N kitchen run and a north/east bathroom; move only conflicting site furniture, or use the documented E/W fallback if selected. Bind all six existing Def types | world-content + art roles. [rows](../../Assets/_Project/Code/App/Editor/RoomFixturePlan.cs#L230), [candidate/fallback](appliance-wall-plan.md#L147) |
| G3: village `sageCottage` | No kitchen/bathroom; planned kitchen W, basin N, shower E, toilet at NE corner; position alone does not choose a backing wall | Install a site-specific home kit and closet, explicitly orient toilet toward the room, separate reaches; prefer N kitchen where the source layout can support it | world-content + art roles. [template](../../Assets/_Project/Code/App/Editor/RoomFixturePlan.cs#L227), [walls](appliance-wall-plan.md#L248) |
| G4: `whiteFarmhouse` | No kitchen/bathroom; planned toilet and basin back onto SE, kitchen NW and bath NE | Keep the full kit with bath, put toilet/basin against the room's NE wall or NW partition, and prove the real ground bearing rather than treating model back as N | world-content + art roles. [farm rows](../../Assets/_Project/Code/App/Editor/RoomFixturePlan.cs#L166), [bearing](appliance-wall-plan.md#L67) |
| G5: `harbourGable`, `creamSaltbox`, `whiteGable` separately | No kitchen or bathroom; upstairs room references exist but no usable stair fixtures are stood | One house per PR: connect its stair first, then the ground kitchen and upper bathroom on the same footprint. Put tall shower pieces on a gable that has full height; keep actual storey rise | world-content + art roles. [rows](../../Assets/_Project/Code/App/Editor/RoomFixturePlan.cs#L236), [stair gap](kitchens-bathrooms-placement-plan.md#L100), [knee walls](appliance-wall-plan.md#L272) |
| G6: St Peters `generalStore`, `postOffice` separately | No staff kitchenette or T/S; no kitchen/bath for either unbuilt upper flat | One shop per PR: staff stations against a north-backed room boundary, with a back-room layout that clears baked shelves. Build the upstairs home in its own later slice, not by counting the staff microwave twice | world-content + art roles; economy-sim for residence gates. [shop layouts](kitchens-bathrooms-placement-plan.md#L103), [upper references](../../Assets/_Project/Scenes/StPeters.unity#L4240) |
| G7: St Peters `school` | No kitchenette, indoor bathroom or outdoor outhouse | Prep + microwave for staff; one outhouse and reachable hand-wash basin outside, with a safe route. No full domestic shower kit in the classroom | world-content + art roles. [school rule](../../Assets/_Project/Code/App/Editor/RoomFixturePlan.cs#L245), [outhouse Def](../../Assets/_Project/Data/Resources/Washrooms/Outhouse.asset#L15) |
| G8: NMC fish plant | Staff wash/staff room are closed zones with no fixtures; no lunchroom kitchen | Bind microwave, toilets/basins/shower against north sides of the staff rooms, with actual partition/backing and access. Coordinate opening with the workplace slice, not an invisible inaccessible set of stations | world-content + art roles; economy-sim for access/jobs. [zones](../../Assets/_Project/Code/App/Editor/NmcBuildingShells.cs#L175), [north-side proposal](appliance-wall-plan.md#L238) |
| G9: NMC community hall / gym | Closed kitchen and washroom zones, no fixtures; gym is a room and equipment markers, not a second building | Open an accessible hall kitchenette (prep + microwave) and T/S; settle changing/shower provision for the gym. Model minus-x is N at this site's bearing, so fit to its north partitions | world-content + art roles; gameplay-systems for workouts later. [hall frame/zones](../../Assets/_Project/Code/App/Editor/NmcBuildingShells.cs#L250), [gym brief](nmc-gym.md) |
| G10: both NMC wholesalers separately | Six appliance markers have no CookingStationFixture, WashStationPoint or SitPoint, hence no attached Def | Replace/bind each microwave, toilet and basin to the existing Def and a unique site fixture key. Boat-gear M/S are N, toilet NE; fishing-gear M is E, toilet NE, basin N. Explicitly set backing/facing and verify staff-room reach | world-content; art roles for visible fixture replacement. [boat markers](../../Assets/_Project/Code/App/Editor/NmcBuildingShells.cs#L353), [fishing markers](../../Assets/_Project/Code/App/Editor/NmcBuildingShells.cs#L419), [Def map](../../Assets/_Project/Code/App/Editor/RoomFixturePlan.cs#L53) |
| G11: NMC C-store `station.nmc_store` | No room; canopy cutaway was miscounted as a shop interior in the old plan; no microwave or T/S | Stand the store's room first; make the back-room/washroom fit in the rig's own frame, keeping cooler/coffee displays clear, then bind staff microwave and customer T/S with north-backed anchors | world-content + art roles. [store/canopy distinction](../../Assets/_Project/Code/App/Editor/StationInteriorPlacement.cs#L29), [space conflict](kitchens-bathrooms-placement-plan.md#L107) |
| G12: NMC harbour office, fish market, restaurant, cold store, general-store stand-in | No installed interiors or stations | One existing site per PR: install its room and complete its smallest appropriate staff/customer kit together. Restaurant gets commercial prep/stove; cold store gets dirty-work shower. Use existing site ids and replace a stand-in at parity | world-content + art roles; economy-sim for trade access. [shell inventory](nmc-buildings-inventory.md#L40), [scene identities](../../Assets/_Project/Scenes/NineMileCreek.unity#L374172) |
| G13: two `CreekHouses` flavour shells | No interiors, kitchen or bathroom | Reconcile each legacy site with the named household/lot plan, then install one full home kit per retained residence. Do not duplicate them into the seven new waterfront houses without an identity decision | world-content. [existing shells](../../Assets/_Project/Scenes/NineMileCreek.unity#L26030), [household move](../design/nmc-key-scenes/README.md#L190) |
| G14: St Peters camper | Room rig exists but entry is refused; stove/prep lack explicit station Def; dish-wash verb is unbound; no placed kitchen/bath | Resolve ADR 0060 first; map stove/prep to existing cooking Defs and head pieces to existing wash/sit Defs, qualify storage by site, then install the full dwelling with its wet bath and test the actual bearing. Keep dish washing descriptive until its own gameplay contract exists | lead-architect for contract; editor engineering, world-content, art roles for installation. [room anchors](../art/rigs/camper-iso-kit/camperInteriorRig.clipper.interior.json), [door state](../../Assets/_Project/Scenes/StPeters.unity#L31964) |
| G15: keeper dwelling and both St Peters upper flats | No installed rooms or full home kit; flat ids absent | Give each site/unit a stable identity, then build its room and full home kit together; separate flat storage/fixture keys from the shop below. The light tower is not a second home | world-content + art roles; economy-sim for flat deeds if offered. [keeper rows](../../Assets/_Project/Data/KeyScenes/KeyScene_stp_ne_light.asset#L22), [flat plans](kitchens-bathrooms-placement-plan.md#L103) |
| G16: all NMC unplaced source rows in section 1, except the east-block overlay | No installed rooms or facilities; mixed-use flats have no roster | Use the kit stated per row; one building per PR, every shop bay and dwelling unit separately supplied. Add kitchen/bath anchors when its first interior is built. Map public changing facilities and campsite shared facilities explicitly | world-content + art roles; data mapping before placement. [main street](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L20), [campground](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L20) |
| G17: each of the 20 expanded plaza units | No runtime room or anchor-to-Def map; `baths` and bathing anchors measure different things; no compass-wall proof | Add a per-source-unit anchor manifest and Def binding, preserving the unit roster; reconcile declared bathrooms with actual toilet/basin/tub/shower anchors. Derive N/E/W in the actual site frame before baking/placement, never from local coordinates alone | editor engineering + world-content; art-director for geometry/anchors. [walk-up anchors](../art/rigs/multiunit-kit/walkup/gameplay/walkupIsoRig.walkup8.gameplay.json), [binding contract](../adr/0060-rigged-dwelling-interiors.md#L167) |
| G18: Cumberland's 20 assessed buildings, including 13 residences | Ground records only; no rooms, kitchens/baths or fixture walls | Author one room and complete appropriate kit per building, keep church kitchen an explicit decision, use marina washrooms for the float bar. Guest-cottage kitchens are per cottage, not one at the lodge | world-content + art roles. [ground plan](../../Assets/_Project/Data/Terrain/CumberlandGround/plan.json), [facility rules](kitchens-bathrooms-placement-plan.md#L130) |
| G19: Rice Point's 11 residences, old store and stage-3 cold store | Ground/stage plans only; no rooms or kitchens/baths | Per current phase, one site per PR with its full kit. Stage-2 bunkhouse reuses the wharf-house identity; later crew/skipper houses stay later-stage data until enabled | world-content + art roles; economy-sim for stage gates. [ground/stages](../../Assets/_Project/Data/Terrain/RicePointGround/plan.json) |
| G20: all placed and planned fixtures | The plan Row has no backing wall or facing; a position test cannot guarantee north-facing readability | Introduce a plan/rig backing-side and a site-bearing conversion, with a named exception list and validator. Keep the standard doorway/overlap/reach assertions, changing only the obsolete fixed-right-closet assumption when the layout truly moves | editor engineering + world-content; qa-test for rule migration. [Row fields](../../Assets/_Project/Code/App/Editor/RoomFixturePlan.cs#L80), [facing gap](appliance-wall-plan.md#L114) |

### Appliances with no Def versus appliances with no binding

| Source object | Existing data / missing fact | Smallest fix and owner |
| --- | --- | --- |
| Standard stove, prep, microwave, basin, toilet, shower, bath | All seven station Defs exist. The unplaced prefabs are not missing content identities | Bind existing ids; world-content. [Def map](../../Assets/_Project/Code/App/Editor/RoomFixturePlan.cs#L53) |
| Both wholesalers' `StaffMicrowave`, `Toilet`, `Sink` blocks | No station component or Def reference on the six objects, although the matching Defs exist | Attach/use the standard fixture prefab with a unique site key; world-content/art roles. [marker definitions](../../Assets/_Project/Code/App/Editor/NmcBuildingShells.cs#L353) |
| Clipper `stove`, `prep` | Actions exist but there is no explicit cooking Def id in the anchor. The rates cannot come from the sidecar note | Bind `cooking_station.stove` and `.prep_counter`; editor engineering/world-content. [sidecar](../art/rigs/camper-iso-kit/camperInteriorRig.clipper.interior.json) |
| Clipper `galley_sink`; plaza `wetSink` | `wash_dishes` is an action, not an explicit Def reference or implemented indoor washing contract | State whether this is a decorative dish sink or a body-wash basin; reuse `washstation.sink` only for the latter. A separate dish-cleaning mechanic is a later design, not a new Def invented here; lead-architect/gameplay-systems |
| Plaza `range`, `vanity`, `toilet`, `shower`, `fridge` hosts, in every unit | Action/verb/host records lack station Def fields. Existing stove/sink/toilet/shower/storage types cover most objects; the fridge's `storage` verb does not itself select food storage | Manifest maps cook→stove, vanity→sink, toilet→sit+toilet Def, bathing→correct bath/shower Def, fridge→food storage Def. Audit the actual host before binding; editor engineering/world-content, economy-sim review |
| Room hearths, shop stove art, coffee/hot-case displays, plant machine blocks | Visual source features have no demonstrated gameplay cooking binding. The house room is not a kitchen merely because it has a hearth | Keep heating/trade/decor roles; only declare a kitchen after binding the proper cooking station. No new general-purpose appliance Def is justified by these alone; world-content/economy-sim. [shop source](../art/rigs/shop-building-kit/shopInteriorRig.js#L116) |

No dangling committed station Def id was found among the **five placed wash/sit instances**. This is a check
of their actual `_stationId` values against the committed Def assets, not a claim that every decorative
appliance in all art sources is interactive. The explicit missing-Def cases are currently **missing bindings
or missing verb semantics**, not an evidenced absence of the seven standard Def types.

## 3. Proposed and missing NMC buildings

The words “missing building” in an older document are not evidence that it is still absent. The following
first reconciles the specifically requested additions with this snapshot, then lists what still lacks a
building. These are building/content prerequisites, not permission to implement later-phase commerce.
Existing building ids, unit ids and seller ids remain stable; an unsited proposal does not receive an invented id.

### Specifically requested additions already present as shells

| Building / function | Snapshot state and source | Data needed before its next build slice |
| --- | --- | --- |
| Gym | Exists as the hall's back room, not a freestanding missing gym. [brief](nmc-gym.md#L1), [hall recipe](../../Assets/_Project/Code/App/Editor/NmcBuildingShells.cs#L250), [placed prefab](../../Assets/_Project/Scenes/NineMileCreek.unity#L438177) | Map each equipment marker to its existing GymEquipmentDef and activity; reach, opening-hour and membership/access records; changing/wash facilities. No second hall or gym site |
| Fish plant | Existing walkable public-route greybox at the documented site A; staff rooms remain closed. [site](../design/nmc-fish-plant-siting.md), [21-zone brief](../design/nmc-fish-plant-key-scene-brief.md), [placed prefab](../../Assets/_Project/Scenes/NineMileCreek.unity#L438076) | Reconcile the documented `structure.nmc_fish_plant` with the placed shell site; zone/fixture manifest, staff access and wash/lunchroom bindings; key-scene registration; truck-court/dock/support geometry and physical goods routes. Current shell is not the rig-kit bake |
| Community/parish hall | Existing greybox on the parish lot, with gym, closed kitchen and washroom. [inventory](nmc-buildings-inventory.md#L18) | Keep one site identity, resolve the walk ending at the ungated north rail, then kitchenette and washroom anchors facing their north partitions |
| Boat/electronics wholesaler | Existing greybox on the east-block lot, `seller.nmc_boat_gear_wholesaler`. [seller Def](../../Assets/_Project/Data/Resources/Sellers/nmc_boat_gear_wholesaler.asset#L15), [placed prefab](../../Assets/_Project/Scenes/NineMileCreek.unity#L438243) | Keep the east-block alias/upper-home plan and current seller routing; turn the three staff markers into bound fixtures. Current seller/counter state must be read from the scene, not the older “no vendor” table in the seller audit |
| Fishing-gear wholesaler | Existing greybox west of the lumber yard, `seller.nmc_fishing_gear_wholesaler`. [seller Def](../../Assets/_Project/Data/Resources/Sellers/nmc_fishing_gear_wholesaler.asset#L15), [placed prefab](../../Assets/_Project/Scenes/NineMileCreek.unity#L438309) | Bind its three staff fixtures; connect the documented public approach to the south door; preserve keeper/offer ids while changing the building presentation |

These five functions occupy **four** new greybox buildings. Real art, usable appliances and access remain
separate gaps; repeating the shell-addition PR would duplicate content. The two existing wholesalers are
not a boatyard, vehicle dealer, or industrial vehicle yard.

### Buildings and residential sites that are still absent

Every key-scene identity in the 41-row NMC building table in section 1 is individually sourced there.
The east-block row is an integration of an occupied site; the other **40** are unplaced building/site
records. The 13 camper records are a separate mobile-residence population. The table below states the
first data each group needs, including plans outside that packaged set.

| Missing building or set (expand each id in section 1) | Source / current absence | Data needed first |
| --- | --- | --- |
| Car and truck dealer | Explicit addition, no building/brief/site. [missing-building inventory](nmc-buildings-inventory.md#L61); existing [`seller.nmc_vehicle_dealer`](../../Assets/_Project/Data/Resources/Sellers/nmc_vehicle_dealer.asset#L15) and vehicle offers prove intended trade, not a shop | Site/footprint/road access brief, showroom versus handover/storage bays, seller/keeper and vehicle-purchase contract, staff microwave/T/S manifest. Keep this separate from marine wholesale |
| Industrial vehicle seller and repair premises | Explicit addition with no building/site. [inventory](nmc-buildings-inventory.md#L62), [`seller.nmc_industrial_yard`](../../Assets/_Project/Data/Resources/Sellers/nmc_industrial_yard.asset#L15) | Yard/site brief, supported display/repair/delivery spaces, offer-to-seller map, lawful carrier/support and access envelope, workshop staff facilities. No unsupported vehicle or teleport delivery |
| Water Street outfitter, flowers, antiques, west block, corner block, gallery, pharmacy, wine shop, visitor centre, post office, east corner, east block, three-decker, captain flats and east terrace; restaurant/bar | Sixteen building rows. East block is already occupied by the wholesaler shell; all source rows remain `Placed: 0`. [main-street data](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L20) | Bay-to-trade and floor-to-unit roster; existing-site aliases, especially east block; stable per-unit fixtures/stores; commercial bakery/restaurant versus staff-kitchen split; public visitor toilets; doors, stairs, reach and source-wall bearings |
| Standalone chandlery | Still an unplaced set-piece building, separate from the legacy counter/stand-in. [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L5138), [old lot purpose](../design/nine-mile-creek-mainland.md#L130) | Decide which site/counter it replaces; bind the existing seller without a duplicate shop; staff facilities and any retained upstairs home roster |
| Grocery, lumber store and lumber-yard gatehouse | Three unplaced workplace records. [plaza data](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_plaza.asset#L22) | Per-shop staff/customer kit, food-prep distinction at grocery, stock/receiving doors and public path; seller ids and real storage anchors before enabling trade |
| Cannery lofts, walk-up block and terrace | Three building records containing 8 + 8 + 4 known units. [plaza rows](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_plaza.asset#L3335), [unit kit](../art/rigs/multiunit-kit/README.md#L54) | Per-unit manifests and site-qualified keys, common access/storeys, actual bathing-anchor coverage and full kitchen/bath bindings; separate the residential loft building from the processing plant |
| Seven north-creek homes, three Shore Lane homes, six south-shore estate records | 16 building/site records with no placed rooms; include the three for-sale NMC homes. [north creek](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_north_creek.asset#L22), [Shore Lane](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_shore_lane.asset#L22), [south shore](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_south_shore.asset#L22) | Map each site to household/deed where applicable; choose room source and actual ground bearing; full home kit per residence. Treat the sixth estate as a home by its `estateHouse` kind until Q6 settles whether “pool house” instead means a changing facility |
| Campground comfort station and pool/pool house | Two building/site records, both absent. [comfort row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L3657), [pool row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L7832) | Public access, toilets/basins/showers, privacy and fixture count, approach routes; reconcile rig id `structure.nmc_camp_comfort` with placed-source identity `structure.nmc_camp_comfort_a` before creating a duplicate Def |
| Thirteen campsite campers | Individual identities in section 1, still not placed. [campground data](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_campground.asset#L20) | Per-variant galley/wet-bath capability and shared-facility assignment. Two pop-ups must not be given an invented full bathroom; all residents need a real route to the comfort station |
| Relocated south-lot farmhouse | Planned at (-200, -8), no established building at the new site. [decision 8](../design/nmc-key-scenes/README.md#L194) | Preserve the household/lot identity, identify the source shell it replaces, deed/fixture keys and room kit. Move once, do not add another farmhouse |
| House on Mill Road and older north/north-west house lots | Named missing/legacy plans without a reconciled current building roster. [inventory](nmc-buildings-inventory.md#L55), [old lots](../design/settlement-population.md#L337) | Reconcile exact lot ids and whether retained, retired or replaced by packaged housing before counting/building. The north-lot “no placement” ruling must survive |
| Homes over store, tavern and chandlery; harbour-office quarters | Four older residential proposals, no unit identities or rooms. [population proposal](../design/settlement-population.md#L343) | Map to the newer mixed-use blocks and existing harbour office; record retained unit ids, independent kitchens/baths and stairs. Do not silently count these as four extra houses |
| Auction house | NMC full-town and auction work name it; no building/site record found. [M2-13](../../backlog/backlog.md#L200), [M2-20](../../backlog/backlog.md#L216) | Scope/site brief, relationship to existing fish buyer, seller/consignment data and Core transaction contract, public/staff rooms and staff kitchenette/T/S. Remains M2 design until scheduled |
| Additional riverside or roadside housing for crew | Older population proposal calls for more dwellings, with count/fit unsettled; newer key-scene households supersede part of it. [housing proposals](../design/settlement-population.md#L365) | Reconciled resident-to-unit roster and measured lot capacity; only then propose extra sites. Do not add the old approximate fifteen to the seven packaged creek homes |

The grouped rows above account for the **41** source building/site records: 16 main-street + 1 chandlery
+ 3 plaza workplaces + 3 plaza housing blocks + 16 creek/shore/estate records + 2 campsite facilities = 41.
The **east-block overlay is counted once in the 16** and the estate called a pool house is already in the
16 creek/shore/estate records. Only the other 40 source sites are fresh exterior candidates.

### Unsited or unapproved business proposals: record, do not assign to NMC by inference

| Named proposal | Source | First data / decision |
| --- | --- | --- |
| Pleasure-boat dealer, yacht broker, ship broker | [seller-audit follow-ups](../design/economy-item-sellers.md#L168), [wholesaler exclusions](../economy/nmc-wholesalers-stock.md#L129) | Whether each exists in NMC at all; which phase/port, site, trade, supported display/handover and staff facilities. Hull sales remain distinct from the electronics wholesaler |
| Golf pro shop, watch seller | [seller-audit follow-ups](../design/economy-item-sellers.md#L172) | Standalone building versus a counter in an existing appropriate business; region and phase. The golf facility may belong to Cumberland, so do not invent an NMC shop |
| Bay hotel, lobster restaurant, other outlet premises | [plant's six outlet assets](../design/nmc-fish-plant-key-scene-brief.md#L227), [bay-hotel data](../../Assets/_Project/Data/FishSupplyChain/bay_hotel.asset) | Map each outlet to an actual regional building or an explicitly off-region customer; bounded receiving storage and a real delivery route. An outlet id is not proof of another NMC hotel or restaurant |

Outdoor missing key-scene pieces (bridge/boardwalks, falls, landing, weather face, court, roads, tents and
outhouses) remain in the [key-scene inventory](nmc-buildings-inventory.md#L66). They are dependencies and
district wash-access locations, not additional indoor buildings. Barns, fleet sheds and similar storage
exceptions stay exceptions unless their use changes to a staffed trade. No proposed count in this section
authorizes a new later-phase market, production, housing or logistics system.

## 4. Build order: ready prerequisites, then one site per PR

This is a dependency order, not a proposal for one large build. Take a slice only after its prerequisites
are present, so no started scene PR waits on another scene writer or an undecided room contract.
Independent data/brief slices can be prepared while a different region is occupied. For every “repeat” row,
each named site or unit is its **own PR**, never the whole row in one PR. Site ids and persisted fixture/store
keys survive replacement. New identities use addition records; the frozen identity baseline is not extended.

The present snapshot still labels ADR 0060 Proposed. An
acceptance change is open (PR #1327); read the merged decision
before scheduling its dependent work. A storage placement change (PR #1318)
also owns lodging/camper placement, and a
Cumberland/Rice Point plan (PR #1288) is open.
Reconcile those results rather than creating another owner for their files. The
older kitchen audit (PR #1235) must be read against this dated
snapshot, especially the canopy and new shells. This audit edits none of those documents or scenes.

### Data and contracts: no visible placement

| Slice / one-PR boundary | Ready after | Result and owning role | Acceptance proof |
| --- | --- | --- | --- |
| D1a: current-site alias ledger | this inventory | Document aliases for lodging, two flavour homes, hall, east block/wholesaler, plant and comfort station. Separate placement ids, Def ids, HomeDef keys and unit addresses. world-content | Each alias resolves to one retained site; no double-counted building, no new save key merely because art changes |
| D1b: main-street dwelling roster | D1a; Q7 | One district roster, linking every shop bay, floor and retained over-trade home to a definite unit or an explicit non-residential use. world-content + economy-sim | Exact unit count and ids; the old lot proposals are either mapped or expressly excluded; no unit count inferred from windows |
| D1c: plaza unit manifest | D1a | Manifest for the existing 8 + 8 + 4 units and their fixture anchors; no new rooms. world-content + editor engineering | All 20 source units appear exactly once; declared bathrooms reconciled with real anchors, including `tub` as distinct from `shower` |
| D1d: campsite capability map | Q5; existing 13 camper rows | One per-variant table of galley/bathing capability and shared-facility assignment. world-content | All 13 campers plus the tent populations have a specified bathroom destination; no invented bathroom in a pop-up |
| D2: backing-side / bearing contract | Q1; D1a | Add a plan-facing field and pure conversion to ground compass; named exceptions with reasons. This prepares data and does not place or turn anything. editor engineering + world-content | Wrong south wall, changed site bearing, mirrored door frame and unknown backing side each fail a focused rule case; retain doorway/overlap/clearance invariants |
| D3a: fixture verb/Def manifest | D1c; accepted room contract | Bind the current cooking, wash/sit and food-store Defs to anchors. Cover the camper and one representative source per family; no decorative verb silently becomes a feature. editor engineering, lead-architect review | Unknown verb/Def, duplicate site key and fridge mapped to generic gear storage refuse; exact type/Def agreement |
| D3b: dwelling foundations | accepted ADR 0060 and its existing prerequisites | Follow the ADR's separate Core-contract, bake, presentation and interior-seam PR boundaries. lead-architect, art-pipeline, editor engineering, world-content | Existing sprite rooms preserve behavior until migration; one inside test, true floor/door, real storey rise, no duplicate shell/room draw. Each asset/presentation PR has its own later editor evidence |
| D4a: road-vehicle dealer brief | this inventory; Q8 | One site/room/yard brief for the existing vehicle seller. world-content + economy-sim | Measured access/display/handover space, seller identity and staff facilities; no purchase or repair system added by the brief |
| D4b: industrial-yard brief | this inventory; Q8 | Separate site/repair-yard brief. world-content + economy-sim | Supported vehicle positions, routes and staff wash/lunch space; no shared ambiguous dealer identity |
| D4c: missing-lot reconciliation | D1a; latest regional plan | One document mapping farmhouse move, Mill Road house and retained old town sites. world-content | The south-lot household moves once; north-lot no-placement ruling preserved; no counting a marker as a house |
| D4d: auction/outlet/broker siting decisions | phase scheduling; Q9 | Separate brief per approved business; leave unsited proposals as data/design. world-content + economy-sim | Region, purpose, existing-business reuse and phase named before any building is proposed |

D3b is an umbrella for the ADR's already-separated PRs, not one combined PR. D2, D4a and D4b do not depend
on a mesh bake. D1c does not wait for a model to be drawn. No save-format or ground change is included in
any of these preparation slices; a demonstrated need for one becomes a separately scoped decision.

### St Peters: serialize all scene writes

| Slice / one-PR boundary, in order | Ready after | Minimum completed experience | Roles / proof |
| --- | --- | --- | --- |
| S1: lodging room source/layout | D2; Q2/Q4; storage change reconciled | One source plan for N-backed kitchen and bathroom, with site-specific furniture override where needed | world-content + art-director; anchor/route/doorway proof before standing it |
| S2: lodging fit-out | S1; correct accepted room path; fixture presentation ready | Prep/stove/microwave and private usable ground bathroom, existing upstairs basin preserved | world-content + art-pipeline; later editor walk/reach and north-wall evidence. No new sprite fit-out over a mesh room |
| S3: camper room/prefab walk-in | D3a/D3b; D1a; existing camper/storage work reconciled | Its complete galley, wet bath and site storage installed together, entry really opens | world-content + art roles; walk/sleep/wash/cook/store evidence, reload existing storage keys, physical supported goods |
| S4: red saltbox source and fit-out | D2; Q3/Q4; correct room path | One purchased home with reachable kitchen and private bathroom; furniture moved only as selected | world-content + art roles; kitchen/bath backing, doorway and deed access; preserve the home/fixture identities |
| S5: village sage cottage | S4 scene write finished; D2 | Full kit and privacy with separated fixture approaches | world-content + art roles; one-home evidence and exact fixture count |
| S6: white farmhouse | S5 scene write finished; diagonal bearing resolved | Full home kitchen and bath, no unrecorded SE fixture exception | world-content + art roles; actual ground-wall proof and access |
| S7: general store | S6 finished; stockroom source ready | Staff prep/microwave and T/S without blocking shelves or trade | world-content + art roles; room-scale walk/reach; upstairs flat remains a separate residence |
| S8: post office | S7 finished; sorting-room source ready | Staff microwave and T/S clear of the mail/stair route | world-content + art roles; off-centre doorway preserved |
| S9: school | S8 finished; outhouse path ready | Staff kitchenette, outdoor toilet and hand washing | world-content + art roles; classroom and public route remain usable |
| S10a/b/c: harbour gable, cream saltbox, white gable | previous scene PR finished; own upper source ready | One house per PR with stair, ground kitchen, upper bathroom and correct full-height shower location | world-content + art roles; actual stair use, one-footprint level test, no through-floor fixture interaction |
| S11: each retained shop flat, then keeper dwelling | own unit/site manifest and source ready; previous scene PR finished | Full private home kit per unit; tower is not another residence | world-content + art roles; qualified fixture/storage keys, stairs and access independently proved |
| S12: cannery restoration facilities | restoration phase scheduled | Staff lunch/wash facilities come with the restored workplace | world-content + economy-sim/art roles; derelict state does not gain a phantom working kitchen |

If the accepted dwelling sequence changes which home proves the mesh path first, reorder S3/S4 to match
that decision; do not build a second room system to keep this table's numbering. Source plans may be ready
before scene slots, but every placed room must use the approved presentation path for that building.

### NMC: a separate serial scene queue

| Slice / one-PR boundary, in order | Ready after | Minimum completed experience | Roles / proof |
| --- | --- | --- | --- |
| N1: boat-gear wholesaler staff fixtures | D1a/D2; fixture presentation ready | Bind its three markers, preserve seller and east-block site | world-content + art roles; exact three fixtures, N/NE backing and staff access |
| N2: fishing-gear wholesaler staff fixtures | N1 scene write finished; public approach defined | Bind its three markers and a reachable public approach, as separate road/scene work if needed | world-content + art roles; exact three fixtures, E/N backing, door route |
| N3: community hall facilities | N2 finished; Q4/Q10; walk/gate decision complete | Accessible kitchenette and T/S, gym wash/changing provision as chosen | world-content + art roles; real route through the west gate, usable private fixtures |
| N4: fish-plant staff facilities | N3 finished; staff access/job phase ready | Actual north-backed lunch/wash spaces, coordinated with its room access | world-content + economy-sim/art roles; no inaccessible “completed” bathroom or unbound work-zone labels |
| N5: C-store interior and facilities | N4 finished; store source/back room ready | A store room distinct from canopy, microwave and customer washroom | world-content + art roles; source-frame-to-site proof, no cooler/door blockage |
| N6a/b/c/d: fish market, restaurant, harbour office, cold store | previous scene PR finished; own room source ready | One building per PR, appropriate staff/customer kit, dirty-work shower at cold store | world-content + art roles; every promised station is placed, bound and reachable |
| N7: dealer, then industrial yard | D4a/D4b approved and scheduled; previous scene PR finished | One complete premises per PR including staff facilities, without silently enabling an unfinished sale/repair feature | world-content + economy-sim/art roles; supported display/handover and door routes |
| N8: retained household sites and mixed-use buildings | D1a/D1b/D4c; own source/ground/access ready | One building per PR, separate kit for every shop bay and resident unit; integrate east-block shell once | world-content + art roles; source-to-site identity and every unit's kitchen/bath count |
| N9: plaza housing | D1c/D3a/D3b; own access/source ready; previous scene PR finished | One building shell/common access PR followed by one complete unit per PR, or a whole small building only if still a single small slice | world-content + art roles; no claiming occupied housing before every intended unit is finished; 8/8/4-unit roster remains exact |
| N10: campsite comfort station, then pool facilities | D1d; fixture numbers/routes settled; previous scene PR finished | Shared washing works before campers and tent accommodation are populated | world-content + art roles; each pitch has an actual public route to toilets/basins/showers |
| N11: campsite camper families | N10; own capability/room source ready | One variant/pitch per PR; galley/wet bath where it fits, assigned shared access otherwise | world-content + art roles; all 13 identities accounted for, no invented pop-up washroom |
| N12: future auction/extra housing/unsited businesses | D4d and phase gate | Only individually approved sites, built with their facilities | world-content + economy-sim/art roles; no unapproved location or later-phase system |

Plant court, road access, gym workout behavior and seller handover code remain separate owning-role slices.
They may be prepared without editing this scene, then consumed before the dependent building PR starts.
Catch and goods remain loaded, carried and unloaded by hand; a fixture or store key cannot move them remotely.

### Other regions and common acceptance

For Cumberland, follow the open regional plan (PR #1288)
after it is reconciled: first **one currently offered home** as the room/Def/deed pilot, then one building
per PR in its own regional queue. Put marina public washrooms before the float bar relies on them; put the
lodge's receiving/staff spaces before opening guest services. Every guest cottage is a separate kitchen/bath
site. Church kitchen provision is Q11. Storage outbuildings keep their documented shared-facility exception.

For Rice Point, use a separate regional queue: one offered home first, then each current residence/store
in its scheduled phase. Later-stage buildings remain later-stage data. Stage 2 reopens the **same** wharf house
as a bunkhouse; stage 3 adds each crew/skipper home and cold-store staff facilities individually. Do not build
the winch house's facilities as if it were a residence. No retired prototype scene receives work.

A placement PR is complete only when its exact fixture/unit count, Def bindings and distinct site keys are
checked; the correct ground bearing and every exception are recorded; doors, approach, privacy and stairs
work at actual body size; washing/cooking and deed access use existing rules; stores retain their existing
save keys; and appropriate editor evidence shows the result. Off-floor fixtures must not interact through
ceilings. A changed rule test moves to the new rule with an equally precise assertion, never a weaker count.
Visual acceptance belongs to the building/art roles. This document's text audit is not that acceptance.

### Recorded choices still needed before the dependent slice

These questions are for the plan record; no answer is assumed to have been given by this audit. The stated
working preference lets independent preparation continue, and does not approve a look or a new building.

1. **Q1: Must every appliance be on strict compass N, or is north-first with recorded E/W and readable-S exceptions the rule?** Choices: north-first with explicit exceptions (working preference, follows the later wording); strict N. Apply the resulting policy to every assessed building, not only deeded homes.
2. **Q2: Which lodging bathroom layout should be used?** Choices: north-east closet with site-specific furniture change (working preference); keep current closet and explicitly justify each remaining S fixture; re-plan within the existing closet. A building/art review must verify the approach.
3. **Q3: Should the red-saltbox bathroom move to the north-east, or use the documented E/W fallback?** Choices: north-east with bed/chest move (working preference); E/W fallback with no furniture move.
4. **Q4: How should privacy partitions be presented without hiding the fixtures?** Choices: cut/low partitions (working preference); collider-only interim partition; full-height walls with the accepted cutaway. This is a visible source decision.
5. **Q5: Which campground variants get their own bathroom?** Choices: per-variant capability plus shared comfort-station access for all, including the two pop-ups (working preference); private wet rooms in every enclosed camper that can physically fit one; shared bathrooms only. Galleys are still assessed separately per variant.
6. **Q6: Is south-shore estate 6 a private home with a pool, or a separate pool/changing facility?** Choices: private home with full home kit (working preference from `estateHouse`); staffed pool facility with changing-room kit. Its name alone does not settle its use.
7. **Q7: Should the four older over-trade/harbour-quarter proposals be mapped into the new mixed-use roster or retained as additional units?** Choices: reconcile into current sites first (working preference); retain specified additional units with explicit identities and space. Do not count both versions automatically.
8. **Q8: Should the vehicle dealer and industrial yard be separate sites or a shared site with distinct premises and sellers?** Choices: separate briefs/sites (working preference); one measured site with separate public/work areas and seller identities. No location is chosen here.
9. **Q9: Which unsited auction/broker/hotel/shop proposals are actually NMC buildings?** Choices: keep them unsited and later-phase until individually located (working preference); approve named premises in NMC; assign named premises to another region. Existing outlet ids alone are insufficient.
10. **Q10: What washing provision does the hall gym need?** Choices: hall T/S plus a shared shower/changing space (working preference); T/S only; separate changing/shower rooms with explicit capacity. Fit is measured before installation.
11. **Q11: Does Cumberland's church need its own kitchenette or only a washroom with shared hall catering?** Choices: T/S with hall catering (working preference); small church kitchenette plus T/S. This does not change the requirement for a hall kitchen.

## Verification of this audit

The proof is serialized source and data, not an editor run. The six scene census resolves referenced prefabs,
separates the canopy from room-bearing interiors, enumerates every installed wash/sit instance and checks its
station Def. Key-scene source tables include set-piece buildings as well as `Kind: building`, campers as well
as houses, and every known plaza unit. Ground-plan ids are deduplicated across repeated JSON references.
Relative evidence targets, stated counts, source-unit coverage and the document-only change scope were checked.
Unity and gameplay tests were not run; no appearance, reach-in-play or visual parity claim is made.

what the player sees: The current game is unchanged; this is a plan only.
the proof: Dated source-linked inventory, exact fixture and unit counts, documented gaps and dependency checks.
the cost: One document; no asset, scene, prefab, code, metadata or save change.
