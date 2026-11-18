# Appliance walls: which wall each kitchen and bathroom piece backs onto

Owner ask, 2026-11-17, verbatim: "For purchasable homes or busiensses that the player can purchase or work at, i
would like to migrate kitechens baths to the north walls, this potentially allows better reads of the appliances, on
the south wall its sometimes harder to tell, bathtubs or tolilets might read ok on east and west walls, same with
kitchen appliances. south walls dont have to be avoided entirely as long as they read as their objects and not just
boring boxes".

This is a survey and plan. It moves nothing and edits no scene or prefab. It reads:
- main at `2f84412d50`
- open #1293 (bathrooms R1, `feat/bathrooms-r1` at `0466fbdd9e`), which places Ginny's ground-floor bathroom
- open #1292 (the two NMC wholesalers, `feat/nmc-wholesalers-greybox` at `21a21435f7`)

It builds on [kitchens-bathrooms-placement-plan.md](kitchens-bathrooms-placement-plan.md) (the R0 plan and its
`RoomFixturePlan` table), [washrooms-plan.md](washrooms-plan.md), the home-buying plan of 2026-11-15 and
[nmc-buildings-inventory.md](nmc-buildings-inventory.md). A separate lane (#1284, household fixture sprites at all 8
facings) is detailing the pieces so they read from every wall. This plan decides which wall each piece stands on.

## 0. The short version

- **The player can buy 8 homes today, as data** (`Data/Homes`). Only one of them has an inside: the **red saltbox**
  in St Peters. The camper and the six NMC, Cumberland and Rice Point homes have no interior yet.
- **He cannot be hired anywhere or buy a business yet.** No job or business-purchase system exists on main (buying
  a building is M2-42). The workplaces planned for him are the NMC **fish plant** (its staff rooms open "when its
  jobs do") and Ginny's farm plot, which is outdoors.
- **Placed now:** Ginny's ground-floor toilet, basin and shower (#1293 pilot), the upstairs washstand at Ginny's
  (main), and greybox markers in the two wholesalers (#1292). **No kitchen piece is placed anywhere yet.**
- **What sits on the south wall now or in the plan:**
  - Ginny's toilet and shower (#1293, placed)
  - the red saltbox's planned shower, and its toilet in the south-west corner (R0 table, not placed)
  - the white farmhouse's planned toilet and basin, on its south-east wall (R0 table, not placed)

  Every other planned piece already backs onto a north, east or west wall.
- **Kitchens barely move.** Ginny's and the red saltbox's counter runs are already on the north wall. Every other
  planned run is on a west wall, which the ask accepts. In the south-door houses the north wall is the hearth gable,
  with windows either side, so a run does not fit there.
- **Recommended moves:**
  1. **Red saltbox** (the one St Peters home for sale): put its bathroom in the north-east corner, with the toilet
     and basin on the north wall and the shower on the east wall. Rose's bed and sea chest move to the free south
     wall. Nothing is placed there yet, so this changes plan rows and two pieces of furniture.
  2. **Ginny's cottage** (his lodging; the seat decides whether it counts, Q1): mirror the closet to the north-east
     corner, with the toilet and basin on the north wall and the shower on the east gable. That means dropping the
     unused ground-floor bed prop she inherits from the village cottage. Keeping #1293's layout and leaving the read
     to the detailing lane is the zero-cost fallback.
  3. **Every row gets a "backs onto" wall**, so the 8-facing sprites from #1284 turn each piece's back to its wall.
     Rows carry no facing today.
  4. **A test keeps the rule.** Every buy-or-work row backs onto N, E or W unless it is on a named list with a
     reason.
- **Order:** everything waits for #1293 to merge, because it owns `RoomFixturePlan`, `InteriorFixturePlacement` and
  `StPeters.unity`. #1292 needs no change.

## 1. How a wall is read

### 1.1 From the plan frame to the compass

`RoomFixturePlan` rows are in the plan frame:
- **+y toward the front door**, −y toward the hearth (back) wall
- **+x to the left as you come in**
- the origin is the floor centre

#1293 found that a footprint whose door is in its −y wall holds the plan half a turn round
(`InteriorFixturePlacement.ModelPoint`). After that, `InteriorFootprint.ModelToWorld` turns the plan by
−45° × facing. The camera looks north, so world +y is north.

For each walk-in room in St Peters (read from the committed scene's `BuildingInterior` fields):

| Room | `_facing` | Door on plan +y | Door wall | Plan +x (left wall) | Plan −x (right wall) | Plan −y (back, hearth) |
|---|---|---|---|---|---|---|
| Ginny's cottage | 2 | 0 | **W** | **N** | S | E |
| Red saltbox | 2 | 0 | **W** | **N** | S | E |
| White farmhouse | 1 | 0 | **SW** | NW | SE | NE |
| Village sage cottage, school | 4 | 1 | **S** | W | E | **N** |
| Harbour gable, cream saltbox, white gable | 4 | 1 | **S** | W | E | **N** |
| General store, post office (shop frame, street door +y) | 4 | 1 | **S** | W | E | **N** |

The derivation was checked against #1293's placed toilet. Plan (−2.775, −1.475) lands at local
(1.475, −1.784), the scene's own number, which is 2.775 m south of the room centre.

### 1.2 Why the south wall reads worst

The room bake is an open dollhouse: "the two walls whose outward face points at the camera are dropped"
(`interiorIsoRig.js`). At the orthogonal facings that is the south wall, and at the diagonals the south-east and
south-west walls. A piece against a dropped wall has nothing drawn behind it, and the camera sees its back. A toilet
seen from behind is a tank, and a stove is a box. Against the north wall the camera sees the piece's front. Against
an east or west wall it sees the piece's side, which reads for tubs, toilets and counters.

### 1.3 Three rules that follow

1. **A partition on a piece's north side reads as a north wall.** A closet in the south half of a room has its own
   partition along its north edge. A toilet or basin against that partition faces the camera, with a wall behind it.
2. **A closet in the north half of a room hides its pieces behind its own south partition, if that partition is
   drawn.** A wall 2.4 m high covers about 2.9 m of floor behind it on screen. Partitions today are colliders only
   (`UpperLevelPlan.Partitions`; nothing draws them), so this does not bite yet. It bites the moment R2's
   ground-floor partitions are drawn full height (Q4).
3. **A row along an east or west wall lines up front to back on screen.** One metre along the wall is 0.64 m of
   screen height, so neighbours overlap. #1293 already found this in the village cottage's standard closet: its
   toilet and basin are 0.81 m apart. Keep pieces on an east or west wall at least 0.9 m apart (#1293's spacing rule
   for Ginny's), or put one piece per wall run. A row along the north wall spreads across the screen and has no such
   limit.

### 1.4 Windows, doors and the hearth (from the rig, confirmed in each editor look)

- **Long side walls carry the sash run:** round((Ln / 2.4) × (0.5 + winD)) windows, spaced evenly. Each is 0.82 m
  wide, with its sill 1.0 m above the floor.
- **Gable walls carry a pair of windows** at ±0.42 × half-width. On the hearth gable, nothing is drawn within 1.15 m
  of centre, because the chimney breast stands there.
- **Pieces under 1.0 m tall may stand under a sash:** toilet, basin, bath, prep counter, the stove body. A basin under
  a window is the ordinary arrangement.
- **Tall pieces stand between sashes:** a shower cubicle, a stove with a pipe or hood, and the microwave if its
  cabinet puts it above the sill.
- **The doorway lane stays clear,** as the tests already require: |x| < door half-width + 0.5 m, within 2 m of the
  door wall.

### 1.5 Rows have no facing yet

`RoomFixturePlan.Row` carries a position and no facing. The placement step writes rotation zero, and the #1293
prefabs are greybox. #1284 bakes each piece at the kit's 8 facings, and turning is a sub-sprite swap
(`InteriorCatalog.ConfigureProp`). So the first code change this plan needs is a `BackTo` field on each row. It
names a plan-frame side (+x, −x, −y or +y), and the placement step turns it into the sprite cell whose back faces
that wall. The toilet's `SitPoint` should seat the player facing away from the same wall.

## 2. The buildings in the ask

| # | Building | Why it is in the set | Interior today | Region scene | Its door wall |
|---|---|---|---|---|---|
| A1 | **Red saltbox**, `home.st_peters_red_saltbox`, 6,000 | For sale (Rose moves over the post office) | Yes, furnished; kitchen and bathroom planned (R2), none placed | StPeters | W |
| A2 | Camper on Ginny's lot, `home.ginny_lot_camper`, 900 | For sale today | No (`_interiorStood: 0`) | StPeters | n/a until stood |
| A3 | NMC Shore Lane saltbox, `home.nmc_shore_lane_saltbox` on `structure.nmc_shore_lane_6`, 5,000 | For sale | Not stood (`Placed: 0`) | NineMileCreek | S if its kit follows the house convention (`Dir: 4`); unverified |
| A4 | NMC house on the creek, `home.nmc_waterfront_house` on `structure.nmc_waterfront_house_3`, 9,000 | For sale | Not stood | NineMileCreek | S, as A3 (`Dir: 4`); unverified |
| A5 | NMC south shore house, `home.nmc_south_shore_estate` on `structure.nmc_south_shore_house_1`, 18,000 | For sale | Not stood | NineMileCreek | S, as A3 (`Dir: 4`); unverified |
| A6 | Cumberland two-storey, `home.cumberland_two_storey` on `structure.cmb_house_4`, 7,500 | For sale | Not drawn | Cumberland | **N** (`"faces": "N"`, 10 m E–W by 8 m) |
| A7 | Cumberland bungalow, `home.cumberland_route_bungalow` on `structure.cmb_route_house_1`, 4,500 | For sale | Not drawn | Cumberland | **W** (`"faces": "W"`, 8 m E–W by 10 m) |
| A8 | Rice Point point house, `home.rice_point_point_house` on `structure.rpt_point_house`, 8,000 | For sale | Not drawn | RicePoint | "its front to the ledges"; no compass in the plan data |
| W1 | **NMC fish plant**, `structure.nmc_fish_plant` | The planned workplace (fish plant jobs, economy lane) | Greybox shell on main; `StaffEntranceWash` and `StaffRoom` are zones with no fixtures | NineMileCreek | S |
| W2 | St Peters cannery | `notice.cannery_for_sale` hints at a business to buy later | Derelict, "gets nothing now" (R0 plan) | StPeters | n/a |
| B1 | **Ginny's cottage** | Not for sale and not a workplace, but it is his lodging and the only room with a bathroom placed (Q1) | Yes; #1293 places its bathroom | StPeters | W |

Out of the ask and listed only so the rule can be widened in one step if the seat wants: the village sage cottage,
the white farmhouse, the three gables, the general store, the post office, the school, the two wholesalers, the NMC
community hall and the fuel-stop C-store (section 3.5).

## 3. Every piece: wall now, target wall

"Now" is main plus #1293 for placed pieces, and the R0 `RoomFixturePlan` row for planned ones. Coordinates are in
the plan frame (section 1.1).

### 3.1 A1, the red saltbox (6.96 × 8.68 m; door W; plan +x = N, −x = S, −y = E)

Furniture (`StPetersInteriors.FurnishingsFor("redSaltbox")`):
- the bed (2.35, −2.30) and the sea chest (2.35, −0.75) on the north wall, east half
- the table and two chairs, centre-south
- the dresser (−2.85, −2.20) on the south wall, east half

The room key is the red saltbox's own, so its furniture can change without touching any other house.

| Piece | Now (R0 row) | Wall now | Target | How |
|---|---|---|---|---|
| Prep | (2.88, 1.74) | N | **N, keep** | Move the stove between sashes if the look shows one over it (section 4) |
| Stove | (2.88, 2.74) | N | **N, keep** | as above |
| Micro | (2.88, 3.59) | N | **N, keep** | |
| Toilet | (−2.83, 3.69), south-west corner | S/W corner | **N** | The closet moves to the north-east, along the north wall: x 1.2..3.48, y −4.34..−1.24. x 1.2 is the doorway-lane limit the test holds (door 1.4 m, so 0.7 + 0.5). Toilet about (2.83, −2.45), backing onto N |
| Basin | (−2.03, 3.79) | W (the door wall) | **N** | About (2.97, −1.55), backing onto N, 0.91 m from the toilet |
| Shower | (−2.73, 2.74) | **S** | **E** | Against the east gable, about (2.35, −3.59), x 1.9..2.8. That keeps it clear of the gable window (x 1.05..1.87), 1.24 m from the toilet, and reachable from the aisle (x 1.35..1.9 in front of it) |
| Closet door | y 2.34..3.14 in the x = −1.58 partition | | in the x = 1.2 partition (the closet's south side), about y −2.4..−1.6, facing the table | The closet's south side is a partition, so rule 1.3.2 applies (Q4) |
| Bed | (2.35, −2.30) | N | **S**, about (−2.85, 2.6), along y, pillow +y as now | Takes the corner the old closet held, clear of the doorway lane (|x| ≥ 2.35) |
| Sea chest | (2.35, −0.75) | N | at the foot of the bed, about (−2.85, 1.2), or left where it is | It clears the new closet either way |

The numbers are a candidate. The R2 editor look sets them, and `RoomFixturePlanTests` must pass on them.

**Fallback (R-1), if the seat wants no furniture move.** Keep the south-west closet:
- the basin stays on the west wall
- the toilet turns to back onto the west wall
- the shower moves from the south wall onto the closet's east partition (y = 2.14)

Every piece then backs onto W or E, which the ask accepts, and only rows change.

### 3.2 B1, Ginny's cottage (6.6 × 8.05 m; door W; plan +x = N, −x = S, −y = E)

Ground furniture is the village cottage's table (the room key is shared):
- the bed prop (2.35, −1.50) and the sea chest (2.30, 0.40) on the north wall
- the dresser (−2.75, −0.50) on the south wall
- the stair foot (−2.50, 0.90) on the south wall

| Piece | Now | Wall now | Target | How |
|---|---|---|---|---|
| Prep, Stove, Micro | R0 rows (2.70, 1.425 / 2.425 / 3.275) | N | **N, keep** | R2 places them |
| Toilet | (−2.775, −1.475), **placed by #1293** | **S** | **N** (G-c), or a south face against the closet's north partition (G-b) | Options below |
| Shower | (−2.575, −2.85), **placed by #1293** | **S** | **E** (the back gable) | G-c; under G-b only if it fits |
| Basin | (−1.925, −3.50), **placed by #1293** | E | **N** (G-c), or E kept | On the east wall it overlaps the gable window span (x −1.80..−0.98) by about 0.18 m. That is fine under the sill, but a mirror would clash |
| Upstairs washstand | footprint frame (0.45, 3.55), level 1, main | E | E, keep | #1293's open note says it stands in Ginny's room, not the player's. That is a separate fix |

**Options for the bathroom (Q2):**
- **G-a, keep #1293's layout.** The detailing lane makes the toilet and shower read from behind. No move.
- **G-b, try it on the look: re-plan inside the same closet.** Turn the toilet to face south against the closet's
  own north partition, east of the closet door, so the camera sees its front with a wall behind it. The closet
  allows little more than that:
  - its inside is 1.6 m (north to south) by 2.475 m
  - the partition cannot move north, because x −1.2 is the doorway-lane limit the test holds and it is at −1.25
  - the east gable's window (x −1.80..−0.98) keeps a tall shower at x ≤ −1.80
  - the dresser (footprint y −1.0..0) rules out a door in the west partition
  - the player's collider (radius 0.35 m) needs an aisle about 0.7 m wide

  On paper, a south-facing toilet plus the shower and basin on the east wall leaves no walkable aisle to all three.
  So G-b probably turns the toilet only and keeps the shower where it is. The look decides.
- **G-c, recommended if Q1 says yes: move the closet to the north-east corner**, mirroring the current one (x
  1.2..3.3, y −4.025..−1.1).
  - The basin and toilet go on the north wall. They are low, so the north sashes (estimated at plan y −3.20..−2.38
    and −1.34..−0.52) do not matter.
  - The shower goes on the east gable, standing off the north wall clear of the gable window (x 0.98..1.80).
  - The door is in the closet's south partition, facing the table.
  - The stair foot (−2.50, 0.90) and the kitchen run (y 1.0..3.7) are untouched.

  The cost: Ginny's ground floor must drop the village cottage's bed prop at (2.35, −1.50), which nobody uses
  (Ginny sleeps upstairs). That needs a Ginny-only ground furniture override in `StPetersInteriors`, which does not
  exist yet, because the room key is shared with the village cottage.

### 3.3 A2 to A8, the homes not stood yet (a rule, not coordinates)

When a region lane stands one of these interiors, it applies this rule in the same PR. The rule is to be added to
section 1.4 of the kitchens plan and to the Cumberland and Rice Point buildings plan (#1288) when each merges.
1. **Door on the south wall (A3, A4, A5 if their kits match the house convention; A8 if its front faces south):**
   - The back wall is north. If the kit draws a hearth there, its corners are about 1.2 m each in a cottage-sized
     room.
   - The bathroom goes in a north corner, with the toilet and basin on the north wall.
   - The kitchen goes on the north wall if a run of 2.4 m or more is free beside the hearth. Otherwise it goes on the
     west or east long wall, between sashes for the tall pieces.
   - The estate house (A5) is the largest. It should get its run on the north wall.
2. **Door on the north wall (A6, the Cumberland two-storey):**
   - The kitchen flanks the door on the north wall, clear of the doorway lane (about ±1.2 m, 2 m deep).
   - The bathroom goes on the east or west wall.
   - Upstairs bathrooms go on a gable wall (section 4, knee walls).
3. **Door on the west wall (A7, the bungalow):** the north wall is the 8 m side wall. Use the red saltbox plan:
   the counter run in its west half and the bathroom in the north-east corner.
4. **Camper (A2):** a long, narrow room. Put the galley on whichever long side faces north once the camper's facing
   is known, and the wet room at one end (east or west).
5. **Any closet in the north half** depends on Q4 (rule 1.3.2).

### 3.4 W1, the fish plant (for when its staff rooms open)

`StaffEntranceWash` (model x −14..−4) and `StaffRoom` (−4..6) are on the south row, y −14..−8. The plant faces 0,
so model +y is north. Their outer wall (y −14) is the dropped south wall. The fitting lane should stand the staff
toilets, basins, showers and lunchroom microwave against those rooms' north side at y −8, which reads as a north
wall (rule 1.3.1). The shell has no partition there yet, only the zone edge, so the fitting lane adds one or uses
pieces with drawn backs.

### 3.5 Outside the ask (for reference; no move proposed unless the seat widens it)

| Building | Pieces (R0 rows unless marked) | Walls | If the rule is widened |
|---|---|---|---|
| Village sage cottage (door S) | Kitchen run; toilet in the NE corner, basin N, shower E | Kitchen W; bathroom NE | Turn the toilet to back onto N. Spread the shower and toilet to 0.9 m or more (rule 1.3.3) |
| White farmhouse (door SW, diagonal) | Kitchen; bath on the back wall; toilet and basin on the right wall | Kitchen **NW** (far, good); bath **NE** (far, good); toilet and basin **SE** (dropped) | Move the toilet and basin onto the NE wall beside the bath, or onto the closet's NW partition |
| Harbour gable, cream saltbox, white gable (door S) | Kitchen ground W; upstairs toilet NE corner, basin N, shower E | W; NE / N / E | As the sage cottage. The upstairs shower on E clashes with the knee wall (section 4) |
| General store, post office | Staff kitchenette and washroom on the stockroom or sorting-room back wall | N | None |
| School | Kitchenette at (2.58, 1.215 / 2.215) | W | None |
| NMC boat-gear wholesaler (#1292, markers) | Microwave y 3.7..4.2, toilet in the NE corner, sink y 3.7..4.2 (back wall y 4.5) | N, NE, N | None |
| NMC fishing-gear wholesaler (#1292, markers) | Microwave x 8.3..8.8 (east wall x 9), toilet in the NE corner, sink y 6.2..6.7 (north wall y 7) | E, NE, N | None |
| NMC community hall (main) | Kitchen and washroom zones, closed, no fixtures. Facing 2, so model −x is north | Each room has a partition on its north side (`GymKitchenWall`, `KitchenWashroomWall`) | Fit against those partitions (rule 1.3.1) |
| NMC fuel-stop C-store | Staff microwave planned "≈ (4.9, −3.55) in the piece's own frame" | Not computed (piece frame; N1 look) | Decide on the N1 look |

## 4. Clashes each move causes, and the fix

| Move | Clash | Fix |
|---|---|---|
| Red saltbox closet to the north-east | Rose's bed and sea chest stand there | Move the bed to the south-wall corner the old closet held (along y, pillow +y as now, never diagonal: the bed-pillow tests). Move the chest to its foot or leave it |
| Red saltbox closet to the north-east | North-wall sashes. Estimate: 4 per long wall at plan y ≈ ±1.0 and ±3.0 (spans about ±0.6..1.4 and ±2.6..3.4), sill 1.0 m | The toilet and basin go under a sash (fine). The shower at y −4.04..−3.14 stands 0.38 m off the north wall, but on screen it can still cover the corner of the sash at y −3.44..−2.62. The look checks the real sash and shifts the shower east, or narrows it to 0.8 m. A basin mirror goes on the partition, not under a window |
| Red saltbox closet to the north-east | The east gable's window pair at x ±1.05..1.87, and the hearth within x ±1.15 | The shower stands at x 1.9..2.8, clear of both. The closet itself starts at x 1.2, inside the window span, but only low pieces or nothing stand there |
| Red saltbox closet to the north-east | `RoomFixturePlanTests` asserts every closet is on the right-hand side (`closet.Area.xMax <= -(halfDoor + 0.5)`) | Change that assertion to the doorway-lane distance on whichever side the closet is (`min(|xMin|, |xMax|)`) |
| Red saltbox kitchen (unchanged) | The stove at y 2.74 may stand under the sash at about y 3.0 | If it has a pipe or hood, swap it with the prep counter, so the counter goes under the window and the stove between sashes |
| Ginny G-b | A south-facing toilet takes the floor the player walks in on; the partition cannot move north (doorway-lane limit x −1.2); the east gable window keeps a tall shower at x ≤ −1.80 | Turn the toilet only. Walk it with the collider on the look |
| Ginny G-c | The ground bed prop holds the north-east corner, and it is shared with the village cottage | A Ginny-only ground furniture override: a new mechanism in `StPetersInteriors` |
| Ginny G-c | North sashes over the toilet and basin; the east gable window | Low pieces under the sashes are fine. The shower stands off the north wall at x ≥ 1.9, clear of the gable window (x 0.98..1.80) |
| Any upstairs bathroom (gables) | Upper storeys have knee walls on the long sides, under the sloped roof | Showers and other tall pieces upstairs stand against a gable end wall (N in these houses), not the E or W knee wall. Toilets and basins can stay low on a knee wall |
| Any closet in the north half of a room | A drawn full-height south partition hides the closet's pieces (rule 1.3.2) | Q4: draw closet partitions as a low cut, or keep them collider-only |
| Turning any piece | Rows have no facing (section 1.5) | Add `BackTo` to the row, and add the cell choice to the placement step. The detailing lane's prefabs need a facing switch |

## 5. Order of work (one writer per scene)

| Step | What | Scene | Files | After | Editor look |
|---|---|---|---|---|---|
| 0 | **This plan** | none | this doc | nothing | no |
| 1 | Rows gain `BackTo` and the placement step picks the facing cell. Re-plan the red saltbox (R-2 or R-1) and Ginny (G-b or G-a). Generalise the closet-side assertion. Add the rule guard test (section 6). Update the kitchens plan's section 1.1 rows | none | `RoomFixturePlan.cs`, `InteriorFixturePlacement.cs`, `RoomFixturePlanTests.cs`, `InteriorFixturePlacementTests.cs`, the kitchens plan doc | **#1293 merged** (it writes all four code files and that doc), and the facing switch from the #1284 prefabs | no |
| 2 | **Fold into R2:** re-apply Ginny's bathroom patch with the new rows, place the red saltbox's kitchen and north-east bathroom, and move its bed and chest | **StPeters** | the placement patch; the village pass patch for the furniture move; `StPetersInteriors.cs` (red saltbox furniture) | step 1; #1293 merged | **yes** (windows, hearth, the route in) |
| 3 | R3 and R4 as planned. If the seat widens the rule: the toilet facings and spacing, and the upstairs shower on a gable | StPeters | as R3 and R4 | R2 merged | yes |
| 4 | Nothing to move | NineMileCreek | none | #1292 merges as it is (its markers are N, E and NE) | no |
| 5 | The fish plant's staff fixtures on the rooms' north side | NineMileCreek | `NmcBuildingShells.cs` or the job lane's own step | when the jobs lane opens those rooms | light |
| 6 | The homes not stood yet (A2 to A8) apply rule 3.3 in the PR that stands each interior | per region | per region | each region's interior lane | yes |

Scenes the open PRs touch:
- #1293 writes `StPeters.unity`
- #1292 writes `NineMileCreek.unity`
- #1286 (NMC exteriors) changes only a bake README, as listed today

Both #1292 and #1293 edit the same prefab list in `RegionSeamAuditTests`, so whichever merges second takes a small
rebase. No step here may run beside another StPeters writer.

## 6. Tests the moves touch

- **`RoomFixturePlanTests`** (main):
  - `EveryStationStandsInsideItsRoom_AndOutOfTheDoorway`, `NoStationSitsOnTheShippedFurniture` and
    `NoTwoStationsOverlap` run on every re-planned row. They read the red saltbox's furniture, so the bed move must
    land in the same PR as the rows.
  - `EveryHomeHasAKitchenAndABathroom_AndBathroomPiecesAreInTheirCloset`.
  - `EveryClosetClearsTheFurniture_TheDoorway_AndHasItsDoorInItsInnerWall` hard-codes the right-hand side and must
    be generalised for a north-east closet in a west-door room.
  - `TheCountsMatchThePlanDoc` stays at 44 rows and 7 closets. Moves add no rows.
- **`InteriorFixturePlacementTests`** (#1293):
  - `TheCommittedSceneHoldsItsFixtures_ARePlanIsEmpty` fails on any row move until the scene is re-patched, so the
    row change and the patch land together (step 2).
  - `EachInteriorFixtureLandsOnItsPlannedPoint` and `APlanRowKeepsItsDistanceFromTheDoor_WhicheverWallTheFootprintsDoorIsIn`.
  - `GinnysBathroomPiecesStandAtLeastNinetyCentimetresApart` holds the 0.9 m rule.
  - `R1PlacesGinnysBathroom_AndNothingElse` is rewritten by R2 anyway.
  - `ThePilotIdsAreThePlansIds` and `EachInteractIdIsInItsSceneOnce` stay green **only if ids never change**. A move
    keeps `fixture.st_peters.<building>.<slot>`, because ids are append-only and the cook job keys on them.
- **New guard test** (step 1): every row whose building is in the buy-or-work set (and Ginny's, if Q1 says yes) has
  a `BackTo` that resolves to N, E or W through its footprint. The exception is a short named list, each entry with
  a reason ("reads from the south, checked on look X").
- **`StPetersInteriorsTests`**: the furniture bounds, `TheFurnishingsDoNotAllPileUpOnOnePoint` and the bed-pillow
  tests (a bed may not stand diagonal or turned away from its declared pillow end), all for the red saltbox bed
  move.
- **`StPetersVillageRefreshTests`**: the village pass must re-plan to nothing on the committed scene, so a furniture
  change is applied through the village pass in the same PR. **`BuildingInteriorStanderOracleTests`** compares
  furniture by name and room coordinate in memory, so it follows the table.
- **`RegionSeamAuditTests.ReferencedPrefabs_CannotHideAdditionalTravelComponents`**: unchanged by a move. It changes
  only when a prefab is referenced for the first time (the kitchen prefabs and `Bath` in R2).
- **`SceneWeightGuardTests`**: no measurable change from moves.
- **`SitPointTests`**: no positions, so unaffected. The seat pose follows `BackTo` once rows carry it.
- **Tree pins:** no EditMode test by that name exists. The scene-equals-plan tests above are the nearest. Interior
  moves stand inside walls and do not touch the woods clearing (`StPetersVillage.RequiredClearingRadius`).

## 7. Open questions for the seat

1. **Does Ginny's cottage count?** It is not for sale or a workplace, but it is his lodging and the only room with a
   bathroom placed. Default: **yes**.
2. **Ginny's bathroom: G-a (keep, detailing only), G-b (turn the toilet in the same closet) or G-c (north-east,
   drop the unused ground bed prop)?** Default: **G-c** if Q1 says yes, otherwise G-a. Also: fold it into #1293
   before merge, or into R2 after? Default: **R2**, per this brief.
3. **Red saltbox: R-2 (north-east bathroom, Rose's bed to the south wall) or R-1 (rows only, everything on W or E)?**
   Default: **R-2**, because it is the one St Peters home the player buys.
4. **Will R2's closet partitions be drawn, and how high?** A full-height drawn partition hides any closet in the
   north half of a room. Default: **a low cut** (like the dollhouse's dropped walls), or collider-only until art
   says otherwise.
5. **Widen the rule to every home and shop in section 3.5?** Default: **no**. Only the farmhouse's south-east
   toilet and basin would really change. Flag it for R2's look.
6. **Who builds the facing switch on the fixture prefabs:** the detailing lane (#1284) or the placement lane in
   step 1? Step 1 cannot turn pieces without it.
7. **The NMC homes' kits (`NmcShoreLane.saltbox`, `NmcCreekHouses.creekHouse`, `NmcShoreEstates.estateHouse`):** is
   `Dir: 4` a south door, as in the St Peters house kit? This is unverified here. Rule 3.3 depends on it.
