# Kitchens and bathrooms: placement plan

Owner ask, 2026-11-16: "Add kitchens, and bathrooms to all residences/buildings that would have them."

This is the plan the placement lanes follow. It places nothing and edits no scene. The exact St Peters
coordinates live as data in `Assets/_Project/Code/App/Editor/RoomFixturePlan.cs`. EditMode
`RoomFixturePlanTests` checks every one against its room: each station must sit inside the floor, out of
the doorway lane, clear of the shipped furniture and of every other station, and each bathroom piece must
sit inside its closet.

Baseline: main `32dfdecdf3`. It builds on four other documents:
- [washrooms-plan.md](washrooms-plan.md) (Defs on main, #1237)
- open #1273 (`WashStationPoint` and the `wash-stations-01` text patch)
- open #1272 (`CookingStationDef`, `CookingStationFixture`, and the `PrepCounter`, `Stove` and
  `Microwave` prefabs)
- open #1235 (the kitchens audit, which this plan follows wherever the two overlap)

## 0. Counts and the PR split

| | Buildings | Kitchens | Bathrooms | Stations |
|---|---|---|---|---|
| **Interiors that exist today** (St Peters 10, NMC fuel stop 1) | 11 | 11 | 10 indoor + 1 outhouse | **57** |
| St Peters house-kit rooms with exact coordinates in `RoomFixturePlan` | 8 | 8 | 7 | 46 |
| St Peters shops (zones; the coordinates need an editor look) | 2 | 2 | 2 | 7 |
| School outhouse (outhouse lane) | (1) | | 1 | 1 |
| NMC fuel-stop C-store (a zone, needs an editor look and a back room) | 1 | 1 | 1 | 3 |
| **Stood shells with no interior yet** (St Peters camper, 8 NMC wharf and village buildings) | 9 | 9 | 9 | 34 (when built) |
| **Data-only buildings, not stood** (NMC town, Cumberland, Rice Point) | 86 | rule in section 1.4 | rule in section 1.4 | not counted |
| Sheds, boathouses, stores, barns, set pieces | 17 stood | none | none | 0 |

"Kitchen" means any cooking station: a full kitchen has prep, stove and microwave, while a kitchenette has
prep and microwave or a microwave alone.

**The PR split.** Only one writer works on a scene at a time. St Peters PRs run in series. NMC runs in
parallel with St Peters.

| # | PR | Scene | Needs first | Editor look |
|---|---|---|---|---|
| R0 | **This PR:** the plan and the `RoomFixturePlan` table, with tests | none | nothing | no |
| R1 | Bathroom fixture prefabs: toilet (a "sit that does nothing" fixture), indoor shower, bath, and the bathroom basin (#1273's `WashStand`, reused). The outhouse prefab. The generic `InteriorFixturePlacement` step (section 3). **Built**, and as a pilot it places Ginny's cottage's ground-floor bathroom (toilet, basin, shower) with no closet partition yet (R2 adds it). | StPeters (pilot only) | #1272 and #1273 merged | one look of the pilot room |
| R2 | St Peters homes A: Ginny's cottage (kitchen and closet; its bathroom fixtures are R1's pilot), the village sage cottage, the red saltbox and the white farmhouse. Kitchens, bathrooms, and the four ground-floor closet partitions. | StPeters | R1 | **yes** (closets and the counter run against the drawn walls, windows and hearth) |
| R3 | St Peters workplaces: the general store, the post office and the school kitchenette, plus the school's outhouse | StPeters | R2 merged | **yes**, and probably a shop stockroom re-bake |
| R4 | St Peters empty homes: the harbour gable, cream saltbox and white gable. Kitchens, a stair to each upper storey, upstairs bathrooms and their partitions. | StPeters | R3 merged | **yes** |
| R5 | St Peters open-air outhouses (section 2.3) | StPeters | R4 merged | light |
| N1 | NMC: the C-store staff microwave and the open-air outhouses (falls, north creek, weather face) | NineMileCreek | R1, and #1273 merged (it writes this scene) | light |
| N2 | NMC: a C-store back room (washroom and staff room), built with the art lane | NineMileCreek | N1 merged | **yes** (shell change) |
| later | Each interior that does not exist yet gets its kitchen and bathroom **in the same PR that builds the interior**, as rows in the same table | per region | that region's interior lane | yes |

R2 to R5 can merge into fewer PRs if the seat prefers. They must never run alongside each other, and none
may run while any other lane holds a StPeters edit.

## 1. Every building

The frame is the room's model metres, the frame `StPetersInteriors.FurnishingsFor` uses:
- the origin is the floor centre
- **+y points toward the front door** and −y toward the hearth
- **+x is to the left as you come in**
- the walls are 0.3 m thick
- "level 1" is the storey above

The stations are:

| Station | Def | Source |
|---|---|---|
| Prep | `cooking_station.prep_counter` | #1272 |
| Stove | `cooking_station.stove` | #1272 |
| Micro | `cooking_station.microwave` | #1272 |
| Sink | `washstation.sink` (basin) | main |
| Shower | `washstation.shower` | main |
| Bath | `washstation.bath` | main |
| Toilet | `washstation.toilet` (a seat, verb `sit`) | main |
| Outhouse | `washroom.outhouse` | main |

**The two templates** (exact numbers in `RoomFixturePlan`):

- **Kitchen run:** along the left side wall (+x) in the front half, at x = W/2 − 0.6. Prep is at
  y = L/2 − 2.6, the stove at L/2 − 1.6, and the microwave on its own short cabinet at L/2 − 0.75. It stays
  clear of the bed or chest in the back half of the left wall, and of the doorway lane.
- **Bathroom closet:** 1.9 × 2.2 m in the back-right corner (−x, −y), clear of the hearth that sits mid
  back wall. It has two new partitions and an 0.8 m door in the partition that faces the room, at
  y = −L/2 + 1.2 to −L/2 + 2.0. Inside:
  - the toilet in the corner at (−W/2 + 0.65, −L/2 + 0.65)
  - the basin beside it at (−W/2 + 1.45, −L/2 + 0.55)
  - the shower in front of the toilet at (−W/2 + 0.75, −L/2 + 1.6)

  Where the room's own furniture already holds that corner, the closet mirrors to the front-right corner.

### 1.1 St Peters: interiors that exist (10)

Each row below needs a **layout change** to make room: the closet's two partitions. Partitions exist today
only on upper storeys (`UpperLevelPlan.Partitions`), so R2 adds a ground-floor list to `StPetersInteriors`.
The gables also need a stair before their upstairs bathroom can be reached.

| Building (scene path) | Who | Kitchen | Bathroom | Stations, model metres | Room? |
|---|---|---|---|---|---|
| Ginny's cottage (`GinnyPlot/sageCottage`), 7.20 × 9.10 m (her own `ginnyCottage` room and shell), 2 storeys | Ginny; the player lodges upstairs | Full | Ground washroom, back-right | **Placed** (kitchen and bathroom). Pass 4, 2026-11-22, after the owner's look (the stairs split the living room from the kitchen; the washroom too shallow; the shower hid the microwave; no landing upstairs): the house is enlarged from 6.60 × 8.05 m to 7.20 × 9.10 m, the least that fits the stairs along an outside wall. The ground floor is three rooms behind solid partitions with colliders (`UpperLevelFor("ginnyCottage").GroundWalls`, a `WallsGround` child of the ground storey's furniture root, so it is switched off upstairs). The living room is the front half (table and two chairs, the painted settee by the door, the stairs). The stairs run along the north wall (x 2.25..3.30, y 0.30..3.15, eleven treads), the foot towards the front door with a clear landing between it and the front wall (y 3.15..4.25, pass 5; pressed at (2.75, 3.70), on the landing), the head at the back partition (pressed at (3.25, −0.20) upstairs); each half lands him at the other. Behind the back partition (y 0..0.30) are the kitchen (north) and the washroom (south), each entered by its own doorway (x −0.10..1.10 and x −2.20..−1.20), with partition D (x −0.60..−0.30) between them. No hearth in her room. Kitchen, all on the north wall: Fridge (2.96, −0.40), Prep (3.00, −1.25), Sink (3.00, −2.25), Stove (3.00, −3.175), Microwave cabinet (3.075, −3.85). Washroom, 2.7 m deep: Shower (−2.575, −3.825) on the back wall, Toilet (−3.075, −2.40) and Basin (−3.025, −1.50) on the south wall, the two recorded south exceptions; nothing stands in front of the shower, and nothing on the back wall north of the partition. Upstairs: a corridor along the north side off the stair head, a partition (x 1.70..2.00) with a door into each bedroom, a solid partition between the bedrooms (y −1.00..−0.70); both beds have their heads on the south wall. The press has no wall check, so the layout keeps every piece out of reach from the rooms beside it (`GinnyCottageRoomsTests`). Closet x −3.6..−0.6, y −4.55..0, door x −2.20..−1.20 in its front partition. Follow-up: the cooking prefabs (stove, prep counter, microwave) carry no solid footprint, so he can walk over their feet. | Grown to 7.20 × 9.10 m (new shell and room bakes). Pass 5: the shell wears the old three-window front and the artboard's details. Pass 6: the artboard's yellow body and sea-green shutters; the room re-baked to three far-wall windows; the smoke rises from the chimney. |
| Village sage cottage (`IslandVillage/sageCottage`), same room | Junior and Basil | Full | Ground closet, back-right | The standard closet: Toilet (−2.65, −3.375), Sink (−1.85, −3.475), Shower (−2.55, −2.425); closet x −3.3..−1.4, y −4.025..−1.825, door y −2.825..−2.025. Its R2 look should consider Ginny's line layout instead (the toilet and basin here are 0.81 m apart and overlap on screen). | Fits; ground partitions |
| Red saltbox (`IslandVillage/redSaltbox`), 6.96 × 8.68 m | Rose (also `home.st_peters_red_saltbox`) | Full | Ground closet, **front-right** (the dresser holds the back-right corner) | Prep (2.88, 1.74), Stove (2.88, 2.74), Micro (2.88, 3.59). Toilet (−2.83, 3.69), Sink (−2.03, 3.79), Shower (−2.73, 2.74). Closet x −3.48..−1.58, y 2.14..4.34, door y 2.34..3.14. | Fits; ground partitions. The editor must check the front-right corner for a drawn window. |
| White farmhouse (`IslandVillage/whiteFarmhouse`), 7.68 × 9.94 m | Eileen boards here; a family house | Full | Ground closet, back-right, 2.3 × 2.6 m, **bath** (a family house) | Prep (3.24, 2.37), Stove (3.24, 3.37), Micro (3.24, 4.22). Bath (−2.64, −4.27) along the back wall, Toilet (−3.19, −3.42), Sink (−3.24, −2.82). Closet x −3.84..−1.54, y −4.97..−2.37, door y −3.57..−2.77. | Fits; ground partitions. When its upper storey is built (the comment in `StPetersInteriors` says the family sleeps up there), a second bathroom can go up; this one stays. |
| Harbour gable (`IslandVillage/harbourGable`), 6.72 × 8.26 m, 2 storeys | Nobody yet | Full, ground | **Upstairs** closet, back-right | Prep (2.76, 1.53), Stove (2.76, 2.53), Micro (2.76, 3.38). Level 1: Toilet (−2.71, −3.48), Sink (−1.91, −3.58), Shower (−2.61, −2.53). Closet x −3.36..−1.46, y −4.13..−1.93. | Both floors are empty. **Needs a stair** (no stair fixture today, so upstairs is unreachable) and upper partitions. The stair must avoid the closet. |
| Cream saltbox (`IslandVillage/creamSaltbox`), 6.84 × 8.47 m, 2 storeys | Nobody yet | Full, ground | Upstairs closet, back-right | Prep (2.82, 1.635), Stove (2.82, 2.635), Micro (2.82, 3.485). Level 1: Toilet (−2.77, −3.585), Sink (−1.97, −3.685), Shower (−2.67, −2.635). Closet x −3.42..−1.52, y −4.235..−2.035. | As for the harbour gable |
| White gable (`IslandVillage/whiteGable`), 6.84 × 8.47 m, 2 storeys | Nobody yet | Full, ground | Upstairs closet, back-right | Same numbers as the cream saltbox | As for the harbour gable |
| General store (`IslandShops/generalStore`), 8 × 10 m; sales floor y −2..5, stockroom y −5..−2 | Marguerite, a 10.5 h counter day | **Staff kitchenette:** Prep and Micro (no stove: her full kitchen belongs in the flat upstairs, which is not stood) | **Staff washroom:** Toilet and Sink | **Zone.** The stockroom's back wall: Prep ≈ (1.2·s, −4.4) and Micro ≈ (2.1·s, −4.4), on the stair side s. Toilet ≈ (−3.25·s, −4.35) and Sink ≈ (−2.45·s, −4.45) in a 1.6 × 1.7 m closet in the opposite back corner. s = +1 if the rig's E side is model +x; the editor must read the sign. | **Needs an editor look, probably a re-bake.** The stockroom's shelves, barrels and stair are baked into the room sprite (rig `stockroom` layout: shelves on both side walls, barrels mid back). The closet displaces the rear stock shelf on its side. |
| Post office (`IslandShops/postOffice`), 7 × 8.5 m, door 1.68 m off centre; sorting room y −4.25..−0.47 | Rose, an 8 h counter day; she buys dinner at the store | **Staff:** Micro on a short counter | **Staff washroom:** Toilet and Sink | **Zone.** The sorting room's back wall, away from the stair (rig W side) and clear of the mail table. | **Editor look.** The same baked-fixture problem as the store: the pigeonholes, stock shelf and crates are drawn in. |
| School (`IslandVillage/school`), 6.36 × 7.63 m | Eileen, an 8 h teaching day | **Staff kitchenette:** Prep (2.58, 1.215), Micro (2.58, 2.215) in the front-left bay. No stove: the hearth heats the room and is not a cooker (the audit says the same). | **None indoors.** A one-room rural school has no room for a closet without breaking the desks. **An outhouse** goes beside it: one `washroom.outhouse` toilet. | Kitchenette rows are exact; the outhouse is the outhouse lane's | Kitchenette fits. Outhouse placed outside. |

### 1.2 Nine Mile Creek: the one interior that exists

| Building | Who | Kitchen | Bathroom | Stations | Room? |
|---|---|---|---|---|---|
| Route 91 fuel stop C-store (`CreekFuel/Route91FuelStop`, room `interior_sStore` 12.46 × 8.82 m; the `BuildingInterior` is 26.28 × 13.79 m because it spans the canopy too) | No staff routine | **Staff:** Micro at the inner end of the coffee run, ≈ (4.9, −3.55) in the piece's own frame (`interior_sStore.asset` blockers). The hot case and coffee are trade fixtures, not a kitchen. | **Customer washroom:** Toilet and Sink | Micro as given; the washroom needs a back room | **Layout change.** The sales floor's back wall is the cooler wall (x −5.4..1.3) and coffee run (2.1..5.35), with only an 0.8 m gap between them. A washroom does not fit on the floor, so N2 adds a back-of-house room behind that gap with the art lane. The piece frame and the `BuildingInterior` frame differ; convert in the editor. |

### 1.3 Stood shells with no interior yet (flagged; stations go in with the interior)

| Building | Scene | Who | Kitchen | Bathroom | Stations when built |
|---|---|---|---|---|---|
| Camper, Clipper 26 (`CamperLot`, `home.ginny_lot_camper`, a `HomeDoor` with `_interiorStood: 0`) | StPeters | Buyable home | Galley: Prep, Stove (two-burner) | Wet room: Toilet, Sink, small Shower | 5 |
| Harbourmaster office (`structure.nmc_harbourmaster`, a trailer) | NMC | The harbourmaster (vendor) | Micro | Toilet, Sink | 3 |
| Fish market (`structure.nmc_fish_market`) | NMC | Shop | Staff Micro, in a clean nook away from the cutting room | Staff Toilet, Sink | 3 |
| Restaurant (`structure.nmc_restaurant`; the shop contract's restaurant room exists but is not stood) | NMC | Cook and server, no roster yet | Commercial: Prep at the drawn prep table, Stove at the drawn oven bank | Customer Toilet, Sink: **not in the rig plan**, so a layout change (a washroom off the bar) | 4 |
| Cold store, piece `fishPlant` (`structure.nmc_cold_store`) | NMC | Plant hands | Lunchroom Micro | Staff Toilet, Sink, **Shower** (dirty work; washrooms plan section 3) | 4 |
| General store stand-in (`GeneralStore`, a teal house sprite with a `GearShop`) | NMC | Claudette | Staff Micro | Staff Toilet, Sink | 3 |
| Flavour house `CreekHouses/redSaltbox` | NMC | Nobody | Full home kit | Full | 6 |
| Flavour house `CreekHouses/sageCottage` | NMC | Nobody | Full home kit | Full | 6 |

The St Peters cannery is derelict ("collapsing") and gets nothing now. Its restored variant would get a
staff washroom with a shower and a lunchroom microwave. The NE light dwelling (not stood) gets the full home
kit when it is.

### 1.4 Data-only buildings, not stood (rules, no coordinates)

None of these has a GameObject in its scene: they are key-scene or ground-plan data. When a region lane
stands one with an interior, it applies these rules and adds the rows.

- **Every dwelling** gets the full home kit: Prep, Stove, Micro, Toilet, Sink, and a Shower (a Bath as
  well in an estate house, a farmhouse, or a two-storey house of four rooms or more). Dwellings include
  houses, cottages, farmhouses, crew and skipper houses, the bunkhouse, summer camps, and **each flat** over
  a shop or in a block.
- **Shops** get a staff Toilet and Sink in the back room, plus a staff Micro.
- **Food trades** get a commercial Prep and Stove plus a customer Toilet and Sink. These are the bakery
  café, deli, restaurant and bar, grocery, resort lodge, clubhouse and float bar. The float bar gets no
  toilet; the marina office serves it.
- **Public washrooms** get several Toilets and Sinks. These are the NMC visitor centre, the campground
  comfort station (plus Showers), the pool house (a changing-room Shower and Toilet) and the Cumberland
  marina office (showers, by its own data).
- **The church and the community hall** get a Toilet and Sink, with a kitchenette (Prep, Micro) in the hall.
- **None:** barns, sheds, stores, the winch house, garages, the lumber shed, the bandstand, the kiosk and
  the bus shelter.

| Region | Data-only buildings |
|---|---|
| NMC | Shore lane 6, 7 and 8 · south shore houses 1 to 6 (6 is a pool house) · waterfront houses 1 to 7 and the waterfront 6 garage · main street outfitter, flowers, antiques, west block, corner block, gallery, pharmacy, wine, visitor centre, post office, east corner, east block, east decker, east captains, east row, restaurant and bar, chandlery, square kiosk, bandstand, bus shelter · plaza grocery, lumber, lumber shed, cannery lofts, walk-up, terrace · campground comfort station A, pool house (45) |
| Cumberland | Store and gas bar, hall, church, houses 1 to 5, route houses 1 and 2, south and north farmhouses and barns, resort lodge, resort cottages 1 to 4, marina office, marina shed, clubhouse, float bar, float stage (24) |
| Rice Point | Strand, cove, brook and marsh houses · wharf house · point house · back-shore camp · farmhouse and barn · stage-3 staff houses 1 and 2 · skipper house · the old store, head shed, stage-2 bait shed, stage-3 cold store, winch house (17) |

**Greywick and Greybox are out of scope by owner ruling** (2026-11-16: "Greywick was a greybox. Its not
used or part of the game."). Neither gets kitchens or bathrooms, and their shells are not counted above.
EastWater and SouthWater are water scenes with no buildings.

### 1.5 No kitchen or bathroom (stood)

These have no kitchen or bathroom because they are workplaces you leave, not rooms you stay in:
- St Peters: Ginny's woodshed, net store and lean-to; the east-end net loft and net shed; the landing bait
  store
- NMC: the eight owners' sheds (Leo, Marie, Yvette, Ross, Hughie, Celeste, Alma, Dan); the bait shed; the
  trap store; the shipyard shed

The wharf shower (#1273) and the outhouses cover the wharf.

## 2. Things the placement PRs must know

### 2.1 Before R2
- **#1272 and #1273 merged.** The kitchen prefabs and `WashStationPoint` come from them.
- **The toilet has no fixture.** `WashStationPoint` refuses `washstation.toilet` by design (it is `sit`,
  not `wash`), and no building-side sit fixture exists on main: `ActionSit` is a boats anchor. R1 adds a
  small World-side sit point: press to sit, press or walk to stand, does nothing. It uses the galley's seat
  pose when that lands.
- **No indoor shower or bath prefab exists.** #1273's `WharfShower` is an open-air pail and post. R1
  builds `Shower` and `Bath` prefabs (placeholder art, `WashStationPoint` with those Defs). It reuses
  `WashStand` as the bathroom basin.
- **The kitchen prefabs have no sprite and no collider.** The planning footprints in `RoomFixturePlan`
  stand in until they do.

### 2.2 Questions for the editor look
- **Which end is the door?** The model frame puts the door at +y. Three buildings serialize
  `_doorOnPlusY: 0` (red saltbox, Ginny's cottage, white farmhouse) while the other house-kit rooms say 1.
  **Answered by the first R1 look: it is not harmless.** The committed scene holds those rooms half a turn
  round: their furniture stands at the negated table coordinates, with the door in the footprint's −y wall.
  The placement step therefore negates a plan row when the footprint's door is at −y
  (`InteriorFixturePlacement.ModelPoint`). Without that, the pilot bathroom stood at the door end beside the
  table. The R2 look should still confirm the counter run is at the end the player enters by.
- **Drawn windows, and the hearth's width,** on the walls the counter run and closets back onto. These come
  from the room sprites, not from data.
- **The shop stockrooms' rig side sign** (E as +x or −x), for the general store and post office zones.

### 2.3 Open-air outhouses
The washrooms plan section 4 lists outhouse spots: St Peters landing and main beach, east end, Ginny's
plot, NE light and Whelps lookout; NMC campground, falls, north creek and weather face.

Two changes, because this plan gives Ginny's cottage a bathroom:
- **Ginny's plot outhouse is dropped.** The cottage's ground-floor bathroom is a few steps away.
- **The NE light and the NMC campground wait until they are stood** (neither is in its scene today).

That leaves:
- **R5:** St Peters landing and main beach, east end, Whelps lookout
- **N1:** NMC falls, north creek, weather face
- **R3:** the school's outhouse

### 2.4 Villagers
When a home's bathroom is placed, the washrooms plan section 5 join applies: add the bathroom as a station
in the region's station table and give the home's residents one `RoutineActivity.Washroom` row. The audit's
meal stops (#1235 section 4) attach to the kitchens and tables the same way. Both are routine changes, so
they get their own PRs after R2 and R3, not inside a placement PR.

## 3. The generic placement step (built in R1)

**R1 notes.** The step is `InteriorFixturePlacement`. It differs from the spec below in three small ways: the builder hook is still called from `StPetersGinnyPlot` (the one building with rows), not from `BuildingInteriorStander`, and moves there when R2 adds rows for buildings the stander stands; one patch is written per building root, holding every level's fixtures; and `WashStationPlacement` keeps only its two prefabs and their ids. The toilet's seat is `SitPoint` (World): it sits the player through Core's `SeatSession`, so a seated stretch is banked as seated rest like any seat, and it washes nothing. After the first look, plan rows are marked `PlanFrame` and negated for a footprint whose door is at −y (section 2.2), and Ginny's bathroom moved to its own layout (section 2 table). The second re-plan was checked on paper against the scene's colliders with the player's collider (radius 0.35 m, 0.7 m below his transform): the route in runs along the north side of the table, then south through the closet door.

**Fixed in the Ginny pass 4: #1273's upstairs washstand.** Its site was (0.45, 3.55) in the footprint frame, which put it in Ginny's back bedroom. It is now a plan-frame site at (0.25, 3.90), against the front wall of the player's room. Ginny's rod rack and gear locker were in the same frame bug (they stood in his doorway); they are plan-frame sites now too, against the bedroom partition.

**Ginny pass 4: the upper storey stands its own outer walls.** The storey above is drawn UpperLevelY (2.51 world units) up the screen, so the ground floor's north wall ran across the upper floor and walled off the south half, where both beds are. The builder now stands a lifted copy of the outer walls in `WallsUpper` and hands the ground floor's `Walls` to the interior, which switches them off while he is upstairs (`BuildingInterior._groundWalls`). The three empty two-storey homes (harbour gable, cream saltbox, white gable) are stood by the village refresh, not this builder, and still have the old walls: the same crossing will matter there once R4 gives them a stair.

**Ginny pass 4: the upper room sheet sorts under him at the north wall.** The sheet is ranked by its floor's far edge raised by the occupant's own pivot-over-feet (`InteriorLevelLayout.UpperRoomSortingOrder(footprint, rise)`, set by the interior when he goes up). Ranked by the bare edge, it tied with him in the north-wall corridor and he vanished on the landing. The cost: anything growing in that strip just north of the house draws over the upstairs far wall. So the seven authored plants the bigger cottage now covered were moved just outside it, and the three in the strip behind it moved 0.4 m north. Their ids are unchanged.

**Ginny pass 5, after the owner's look** ("no floor space in front of the stairs", "the old three window look", "add the missing details from the artboard"):

- **The stair landing.** The flight is shortened to eleven treads (y 0.30..3.15), so between its foot and the front wall there is 1.10 m of open floor as wide as the flight. The foot is pressed from that landing. The head upstairs is unchanged. The painted flight and the upstairs stairwell were redrawn to match. `GinnyCottageRoomsTests.TheFootOfTheStairsHasClearFloorInFrontOfIt_ALandingHeCanStandOn` holds it.
- **The wardrobe upstairs** moved off the corridor partition, where its top drew over the stair rail, to the front wall of his room, between the foot of his bed and the washstand, at (−0.75, 3.98). `UpstairsNothingTallStandsInFrontOfTheStairwell` holds it.
- **Chimney smoke** hides while the shell is cut away to show the rooms (`ChimneySmoke.PlumeShows`), so it no longer drifts across the upstairs room.
- **The front.** The shell stays at the larger size, but its dials keep the old cottage's front: three windows on each eave wall in one row (`winDensity` 0.40 and the house rig's new `upperWindows: false`). The gables keep their upper windows.
- **The artboard's details** (the key scenes' return 3, Ginny's cottage, 2026-09-28): red door, window boxes, the wall bench, the stovepipe, roof patches and the roof ladder. Pass 6 (owner 2026-11-22, "make it yellow to match the artboard"): the artboard's yellow body, sea-green shutters and weathering are taken too, so the shell is the artboard as drawn apart from its size and its one-row front. The bench and window boxes have no collider (the building's footprint is its walls).
- **The room matches the front** (pass 6): the room's winDensity is 0.40 like the shell's, so the far wall inside shows three windows, as the front does outside (VillageReturnIntakeTests.GinnysRoom_PaintsAsManyWindowsAsHerShellShows).
- **The smoke rises from the chimney** (pass 6): the smoke object stands at the shell bake's chimney-top anchor for the facing the scene draws, less the plume's own 0.5 m lift (StPetersGinnyPlot.SmokeLocal, checked against the anchor by GinnyCottageRoomsTests).

**Why it is not built in this PR.** It would generalise #1273's `WashStationPlacement`, which is not
merged. Building it now on main means a second copy of the same ~250 lines of scene-text patching, which
#1273 would then have to be reworked onto. No row could use it yet anyway: none of its prefabs (kitchen,
toilet, shower, bath) are on main. What *can* stand alone is built: the row table and its geometry tests
(`RoomFixturePlan`, `RoomFixturePlanTests`). The step below consumes that table unchanged.

**Files (R1):**
- `Assets/_Project/Code/App/Editor/InteriorFixturePlacement.cs`: the planner, menu items and builder hook
- `Assets/_Project/Code/App/Editor/RoomFixturePlan.cs`: the table (from this PR). Each row gains a
  `Prefab` path from a `Kind → prefab` map in the placement file. R1 also adds the shop and NMC rows once
  their editor looks give coordinates; rows for open-air sites (outhouses) carry world metres and a root
  name, as #1273's open-air `Site` does.
- `Assets/Tests/EditMode/InteriorFixturePlacementTests.cs` (beside `RoomFixturePlanTests`, in the
  EditMode assembly that references App.Editor and Art; the `World` test assembly does not)

**Types:**
- `InteriorFixturePlacement.PrefabFacts`: #1273's, but generalised. It finds the root `MonoBehaviour` that
  serializes an `_id:` field (both `WashStationPoint` and `CookingStationFixture` do) instead of requiring
  `WashStationPoint` by class. It refuses zero matches, and it refuses two.
- `Plan(SceneYaml scene, string scenePath, Func<string, PrefabFacts> prefabs) → List<LayerPatch>`: one
  patch per building root touched, as in #1273's `PlanInterior` and `PlanOpenAir`. It reads the
  building's serialized `BuildingInterior` footprint (`_widthMetres`, `_lengthMetres`, `_facing`,
  `_facings`, `_groundDepthScale`, `_doorOnPlusY`, `_doorAcrossMetres`, `_upperLevelY`) and converts
  through `InteriorFootprint.ModelToWorld`.
- **One named step per scene:** `interior-fixtures-st-peters` and `interior-fixtures-nmc`. Patches go to
  `artifacts/interior-fixtures/<step>-<root>.yaml`. The menu items are "Write the Patches (dry run)" and
  "Apply the Patches"; both refuse an open scene.
- `StandInterior(GameObject buildingGo, string buildingRoot)`: called once from `BuildingInteriorStander`
  after it furnishes a building (not per site, as #1273's call in `StPetersGinnyPlot` is), so a rebuild
  re-stands every row.

**Ids:**
- The interact id is `fixture.<region>.<building_key>.<slot>`, which is `Row.Id`. Examples:
  `fixture.st_peters.ginny_cottage.stove` and `fixture.st_peters.red_saltbox.toilet`.
- `building_key` is **explicit per row**, never derived from the GameObject name. Ginny's cottage and the
  village cottage are both named `sageCottage`.
- The GameObject name is the slot in PascalCase (`Stove`, `BathroomSink`). It is unique under its furniture
  root.
- `IdAllocator` keys are `<step>/<interactId>/PrefabInstance` and `<step>/<interactId>/Transform`.
- The `_id` doubles as the cook job's key (#1272), so it must stay stable. These ids are append-only.

**Folding #1273 in:** the two `WashStationPlacement.Sites` rows become two rows with the same names
(`WashStand`, `WharfShower`) and the same ids. The planner finds an existing instance by prefab GUID and
name, so re-planning after the switch is empty. Then `WashStationPlacement`'s planner is deleted.

**Tests:**
1. **Idempotence.** For each scene with rows, `Plan` on the committed scene text gives patches with zero
   operations, so a placement PR proves its own apply. Separately, on a small synthetic scene: plan,
   apply, then plan again is empty.
2. **One instance per id.** Every row's id appears exactly once across both scenes.
3. **Landing.** The world point of each interior row, mapped back through the footprint, equals `Row.At`
   within 1 mm.
4. **Geometry.** All of `RoomFixturePlanTests` keeps running on the table.
5. **Prefabs.** Every prefab loads, carries exactly one `_id` component, and its Def resolves.

## 4. Not in scope

These are left out, to be logged for later:
- kitchen sinks as a wash station
- fridges, pantries, ice coolers and dining-seat interactions (audit #1235 section 2)
- NPC meal and washroom routine rows
- the farmhouse's upper storey
- the general store's flat
- player-placed furniture
- any save change (none is needed: placed stations are scene content)
