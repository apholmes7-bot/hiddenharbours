# Nine Mile Creek: buildings and key-scene pieces, planned vs placed

Counted 2026-11-16 against `main` at 32dfdecdf3, then updated for this PR (the fish plant and the community hall
with its gym, both greybox shells). Read with `nine-mile-creek-mainland.md`, `nine-mile-creek-wharf.md`,
`docs/design/nmc-fish-plant-key-scene-brief.md` and `docs/world/nmc-gym.md`.

**Headline.** Before this PR Nine Mile Creek had no building you could walk into. Its one `BuildingInterior` was
the fuel canopy's see-through (#1266), which is a cutaway, not a room. Two of the twelve packaged key scenes are
placed (the wharf; the fuel stop, still in progress #1194). Ten are not placed (`Placed: 0`). Three buildings
ruled since the package (the fish plant, 11-12; the car and truck dealer, 11-15; the two wholesalers, 11-16)
are outside it.

**This PR adds two buildings.** Both are walkable greybox shells using the seamless-interior rule (ADR 0036):

- **The fish plant** at site A, centre (-265, -40). It is 48 x 28 m with the brief's 21 zones laid out. You go in
  by the public door on the south wall (-245, -49). The public route goes reception, receiving aisle, delivery edge,
  intake I1. A rail closes off the production floor.
- **The community hall** on the parish lot, centre (-153, 62.5), inside the lot's post-and-rail fence. It is
  18 m north to south by 12 m deep. Its door is on the west wall at (-159, 62.8), 2.5 m inside and square on to the
  lot's only gate (-161.5, 62.8). Behind the front hall are:
  - the gym, 8 x 6 m, with seven equipment markers, each sized from its `GymEquipmentDef`;
  - a kitchen and a washroom, closed for now, per the owner's 11-16 ask that halls have them.

  The hall is turned a quarter (room facing 2 of 8) so the door faces the gate.
  - **Found on the way, not fixed here:** the walk `walk.nmc_parish_hall` comes down from Wharf Road and ends at the
    lot's north rail, which has no gate. The yard step owes either a gate there or a re-routed walk to the west gate.

Both are scene changes made by the named text-patch steps `20-fish-plant` and `21-community-hall-gym` in
`NmcBuildingShells.cs`. Neither step opens or re-saves the scene.

## Table A: buildings

| Building | Purpose | Source of the ask | Scene state | Kit / art that could build it | Proposed spot |
|---|---|---|---|---|---|
| **Fish plant** `structure.nmc_fish_plant` | Truck-fed processing: sells to the grocery, restaurants and export | Ruling 11-12 (key scene, site A); `nmc-fish-plant-siting.md`, `-truck-court.md`, `-key-scene-brief.md`, `fish-plant-supply-chain.md` | **Placed as a greybox shell (this PR)**: walkable public route, working floor laid out, staff and cold rooms behind closed thresholds. Logic is on main (#1127). | `docs/art/rigs/nmc-fish-plant-kit/` (#1203, rig JS only, **not baked**); outside it uses existing fuel-stop and landing set pieces | (-265,-40); wall x[-289,-241]; door (-245,-49) |
| **Gym** | Strength and cardio training | Ruling 11-15; `docs/world/nmc-gym.md` | **Placed as the back room of the community hall (this PR)**: floor and equipment markers, no workouts | No gym art; greybox blocks sized from `Data/Resources/GymEquipment/*` | the hall's back room, its north-east corner |
| **Community / parish hall** | Hall; hosts the gym | `nine-mile-creek-mainland.md` §1.3; `nmc-gym.md` | **Placed as a greybox shell (this PR)**: front hall, gym, kitchen and washroom (closed) | houses-kit manor / shop-building-kit for real art | parish lot (-153,62.5), 18 x 12 m, inside the yard rail; door (-159,62.8) facing the lot's west gate |
| **Marine electronics and boat-gear wholesaler** (new, 11-16) | Sells the Loran and all current boat electronics. Not the boatyard, which sells hulls and engine upgrades. | Owner ruling 11-16 (seat relay) | **Placed as a greybox shell (step 22)**: shop floor with the trade counter, an electronics case and shelf markers; stock room with a staff microwave; staff washroom (toilet and sink markers). Seller identity `seller.nmc_boat_gear_wholesaler` (no keeper, no offers yet). The Loran is still offered by Marguerite (PR 1269) as a placeholder until the owner rules on the stock proposal. | shop-building-kit (`chandlery` room pattern) for real art | The main street key scene's east block lot on Water Street, centre (-148.4,237.5), 12 x 9 m; door (-148.4,233) onto the frontage walk. It stands in for the east block's ground floor until the key scene is placed. |
| **Fishing-gear wholesaler** (new, 11-16) | Bulk fishing gear: rope, traps, nets, line | Owner ruling 11-16 (seat relay) | **Placed as a greybox shell (step 23)**: trade floor with the counter and shelf markers, bulk display (pots, net bales, rope), staff room with a staff microwave, staff washroom (toilet and sink markers), warehouse with pallet racking. Seller identity `seller.nmc_fishing_gear_wholesaler` (no keeper, no offers yet). | wharf-building-kit-v2 `netShed`/warehouse for real art | West of the lumber yard's fence, centre (-301,125), 18 x 14 m; door (-296,132) on the north wall. No walk reaches the door yet. |
| Harbour office `structure.nmc_harbourmaster` | Cod licence, vendor | `nine-mile-creek-wharf.md` §3 | Exterior only | wharf-building-kit-v2 / houses-kit manor | (86.4,112.6) |
| Fish market `structure.nmc_fish_market` | Catch counter | `nine-mile-creek-wharf.md`; NineMileCreekShops.cs | Exterior only | shop-building-kit `fishMarket` (has an interior) | (91.2,127.6) |
| Wharf restaurant `structure.nmc_restaurant` | Restaurant | NineMileCreekShops.cs; settlement population | Exterior only | shop-building-kit `restaurant` | (168,136) |
| Cold store `structure.nmc_cold_store` | Ice and cold store | `nine-mile-creek-wharf.md` §3 | Exterior only | wharf-building-kit-v2 `iceHouse` | (64,99.2) |
| Bait shed, trap store | Wharf trades | `nine-mile-creek-wharf.md` §3 | Exterior only | wharf-building-kit-v2 netShed/redShed | (136.5,131.2) / (152,131.4) |
| 8 fleet sheds | Fishers' premises | KeyScene_nmc_wharf | Exterior only | wharf-building-kit-v2 | rows at y 101.8 and y 131 |
| Boat-yard shed `structure.nmc_yard_shed` | Shipwright: hull sales, engine upgrades | wharf; H6 #1023 | Exterior only | nmc-shipyard-kit | (199,131.4) |
| Fuel stop store and canopy | Gas bar | KeyScene_nmc_fuel_stop | Placed (#1194, in progress); canopy see-through; the store has no room | `Prefabs/GasStation/interior_sStore.prefab` | store (-155,139) |
| Legacy GeneralStore / CreekFuel | Old loop | Retire (10-31) | Present, legacy | to retire | n/a |
| Chandlery `structure.nmc_chandlery` | Rod at the counter | KeyScene_nmc_main_street | Missing | nmc-set-pieces-kit / shop-building-kit `chandlery` | (-160,237.26) |
| Restaurant and bar `structure.nmc_restaurant_bar` | Bar | KeyScene_nmc_main_street | Missing | houses-kit manor; shop-building-kit `tavern` | (-226,241.26) |
| 13 Water Street storefronts and the post office | Shops | KeyScene_nmc_main_street | Missing | main-street kit (#1200) | lots x -262..-123, y 236..275 |
| Plaza grocery | Grocery; fish-plant outlet | KeyScene_nmc_plaza | Missing | plaza-stores nmcStores.js | (-213,120.5) |
| Lumber store, shed, gatehouse | Building supplies | KeyScene_nmc_plaza | Missing | nmcStores | (-244,121) / (-279,111) |
| Plaza homes (cannery loft, walk-up, terrace) | Housing | KeyScene_nmc_plaza | Missing | multiunit-kit | (-271,25), (-241,28), (-202,33) |
| Farmhouse (Dan) | Household | key-scenes README decision 8 | Missing at its new spot | houses-kit | (-200,-8) |
| House on Mill Road | Household | GroundMasks lots | Missing | houses-kit | (-264.5,152) |
| 7 creek houses (and a garage), including the waterfront one for sale | Households, home buying | KeyScene_nmc_north_creek; ruling 11-15 | Missing | north-creek nmcCreekHouses.js | lots x -72..6, y 216..264 |
| 6 south-shore estates (with pools), including one for sale | Households | KeyScene_nmc_south_shore | Missing | south-shore nmcShoreEstates.js | y -95..-148 |
| 3 Shore Lane saltboxes, including one for sale | Households | KeyScene_nmc_shore_lane | Missing | kit file unknown | (-196,-160)..(-150,-161) |
| Campground comfort station, tennis court, pool | Campground | KeyScene_nmc_campground; washrooms plan §3-4 | Missing | nmc-set-pieces-kit comfortStation; camper-iso-kit | (-6,-66) |
| Car and truck dealer | Buy vehicles (key scene) | Ruling 11-15 (no doc yet) | Missing | unknown; needs a brief | none yet |
| Industrial vehicle seller and repair | Work vehicles | Ruling 11-15 | Missing | unknown | none yet |
| Outhouses | Toilets | washrooms plan §4 | Missing | not authored | campground, falls, north creek, weather face |
| Wharf shower | Rinse | washrooms plan §3 | Open PR 1273 | `Prefabs/Washrooms/WharfShower.prefab` (in that PR) | (90.2,108.2) |

## Table B: key scenes

| Key scene | What it is | Required pieces | Present | Missing | Kit to supply |
|---|---|---|---|---|---|
| wharf | Working wharf, 134 rows | Office, market, restaurant, cold store, sheds, crane, yard, berths, floats, trucks, fleet | All placed | All interiors; the crane's night pose is unverified | shop-building-kit rooms; wharf-building-kit-v2 interiors |
| fuel_stop | Gas bar, 30 rows | Store, canopy, pylon, pumps, lights | Placed (in progress), canopy cutaway | Store interior; plates; retiring CreekFuel | interior_sStore.prefab |
| **fish plant** (13th) | Inland truck-fed plant, site A | 48x28 m shell, ground-floor interior, court, 12 m gate, Route 91 spur, dock, kerb, bollards, condenser, totes, yard lights, staff parking, 19-person roster | Logic, docs, rig kit; **this PR: shell, interior zones, public route, door crossing, two yard lights, intake ice box and totes, byproduct skip, four staff wheel stops (existing art)** | Court paving, gate, spur to Route 91, dock, kerb and bollards, condenser, trucks, Defs and registration, NPC roster, real art | nmc-fish-plant-kit (bake needed); road and vehicle lanes for the court |
| town_bridge | Bridge, walls, boardwalks, lamps, 53 rows | Masonry, courses, boardwalks, lamps | Defs only (#1103) | All placement | nmc-set-pieces-kit |
| the_falls | Cascade, deck, lookout | Cascade, deck, rail, bench | None | All, plus an outhouse | nmc-set-pieces-kit |
| main_street | Water Street, 91 rows | Blocks, bar, chandlery, square, fountain, bandstand, cars | None | All, plus moving the rod sale | main-street kit, set pieces, shop-building-kit |
| plaza | Grocery and lumber plaza, homes, 122 rows | Grocery, lumber store, homes, forklift | None | All | nmcStores, multiunit-kit |
| north_creek | 7 houses and dories | Houses, docks, dories | None | All | nmcCreekHouses |
| landing | The landing, 25 rows | (detail not read) | None | All | nmc-set-pieces-kit |
| weather_face | Bluff, gully steps | Steps | None | All, plus an outhouse | set pieces, cliff-face-kit |
| south_shore | 6 estates | Estates, pools | None | All | nmcShoreEstates |
| shore_lane | 3 saltboxes | Saltboxes | None | All | unknown |
| campground | 14 pitches, 178 rows | Comfort station, campers, fire rings | None | All, plus a shower block | camper-iso-kit, set pieces |
| car/truck dealer | Dealer (ruled 11-15) | No brief yet | None | All, plus a brief | unknown |

## Proposed order for what is left

1. **The fish plant's court** (road and vehicle lanes): paving, the 12 m gate at (-215,-40), the spur to Route 91
   at (-190,-40), dock, kerb and bollards, condenser. This finishes the key scene the owner named first.
2. **Marine electronics and boat-gear wholesaler** (ruled 11-16). The greybox shell and the seller identity are
   placed (step 22). Still needed:
   - a keeper NPC def;
   - the Loran and the other current electronics moved off Marguerite's placeholder offer (PR 1269) onto this
     counter, once the owner has ruled on the stock and price proposal.
3. **Fishing-gear wholesaler** (ruled 11-16). The greybox shell and the seller identity are placed (step 23).
   Still needed: a keeper, the gear offers moved off their current placeholders once the owner rules, and a walk
   or drive from the lumber yard's out-drive to its north door.
4. **Fish plant jobs and the gym's workouts** (economy and gameplay lanes). The rooms exist now. The plant's
   staff rooms and cold stores open when its jobs do.
5. **Real art for both shells**: bake the nmc-fish-plant-kit (#1203); give the hall a houses-kit or
   shop-building-kit exterior. The steps keep the footprints, so only the prefab art changes.
6. **Car and truck dealer**: a brief first (ruled 11-15, no doc yet), then the shell.
7. **main_street and plaza** key-scene placement (the chandlery, the bar, the grocery, the lumber store).
8. Wharf building interiors (the fish market first, because the shop-building-kit already has its room), then
   the fuel stop's store.
9. The housing key scenes (north_creek, south_shore, shore_lane), then the campground, the falls and the
   weather face.

## Follow-ups logged by this PR (not built here)

- Gym workouts and fees; fish plant jobs, roster NPCs and routines.
- Fish plant court: paving, gate, spur, trucks, staff parking surface.
- Interior lighting for both shells. The greybox does not light.
- Real art: the fish-plant kit bake, and the hall exterior.
- A dealer brief.
- The parish lot's walk (`walk.nmc_parish_hall`) ends at the lot's north rail, which has no gate. The only gate
  is in the west rail. The yard or roads lane owes either a gate at the walk's end or a walk to the west gate.
- The two wholesalers: keepers, and moving their offers off placeholders once the owner rules on the stock
  proposal. The shells and seller identities are placed (steps 22 and 23).
- A walk or drive to the fishing-gear wholesaler's north door, from the lumber yard's out-drive (roads lane).
- When the main street key scene is placed, its east block takes the boat-gear wholesaler's shell rather than
  doubling it.
