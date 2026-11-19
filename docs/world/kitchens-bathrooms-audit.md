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
| `structure.nmc_south_shore_house_6` | public / pool house | `estateHouse` | staff M if staffed / changing T/S + shower | [key-scene row](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_south_shore.asset#L712) |

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

