# St Peters, the village: the plan

> - **Status:** PLAN, for the owner's ruling. Session 1 of the village redesign arc. Docs only: no code, no asset, no scene.
> - **Lane:** world-content, with art-pipeline. Charter: `seat/HANDOFF-2026-09-25-st-peters-village-redesign.md`.
> - **Pillars:**
>   - **P3 Living Working Coast:** a village you can read from its street: a school, a store, a post office, six homes, a square to gather in, a garden, and a flake yard where the cannery's hands turn the fish.
>   - **P2 Dory to Dynasty:** the island's work has somewhere to live: the cannery's four hands get homes on the island (settlement-population Q8).
>   - **P5 Cozy but with Teeth:** at dusk the village lights itself a lantern at a time, by the hands of the people who live there. There is still no pole and no wire.
> - **Siblings:** `docs/design/st-peters-terrain-pass-9.md` · `docs/design/municipal-infrastructure.md` · `docs/design/settlement-population.md` · `docs/design/lighting-and-daynight.md` · `docs/design/world-and-regions.md` · ADRs 0003, 0004, 0016, 0019.
> - **The package:** `HH-village-camera-first-2026-09-25.zip` (5,659,241 B, sha256 `073f569e5557eabab70b3e00362c76ce93aa639e7dc4a89ae3876d051d02beae`): the brief the owner sent Claude Design, built at main `981f50a6`.
> - **Base:** main `981f50a6`. Numbers taken from the game cite their file and line. Tables T1 to T13 are generated from the prototype's output, and the prototype's 281 checks all pass (Appendix A).
> - **Units:** world units, as in the package's `sites.json`. x is metres east. y is screen-north, which is metres north × 0.643 (the 40° camera). Lot, yard and path sizes are in ground metres.
> - **Pictures:** 8 PNGs, 1,373,795 B, in LFS under `docs/design/st-peters-village-plan/`, with the plan's JSON (Appendix B).

---

## 0. In plain words

**What you asked for.** First more of a grid, then the grid broken up. A few houses round a square with greenery in the middle. Some houses nearer the shore, some closer together, some on larger lots. Most front doors to the south, so the camera sees them; a few from the east or west, still showing off the house. Clapboard. Six homes, the store and the school. A common ground and a garden for the villagers. Paths with lights and fences. A yard of its own for every home. And every screen a pleasant view.

**What the plan does:**
- **One street, Green Row,** runs east–west across the top of the village, 76 m long. Five buildings stand along its north side, behind one front line, all facing south: the school, the store, the post office and two of the new homes. That is the grid.
- **The square** lies between the street and the green, round the hearth, which stays the middle of the village. It has log benches by the fire pit, a notice board, two benches, flower beds and two young maples. The school, the store and the post office face it from across the street. Rose's red saltbox faces it from the east, across the East Walk. Those are the houses round the square.
- **The west garden** is the square's other half: four garden beds, two benches, a water butt and a potting bench, on public ground.
- **Breaking the grid.** Two homes do not face the street. Rose's red saltbox and Eileen's white farmhouse each sit down a walk of their own, on the island's two largest lots (17.6 × 27.5 m and 14.4 × 23.5 m), facing west. The fronts step back by 0 to 1.7 m, the lots run from 8 to 18 m across, and every home has its own kind of fence.
- **Nearer the shore.** The sage cottage (Junior and Basil) moves up to the Shore Walk above the harbour shore, and a new house, the harbour gable, stands at the head of the Bluff Path.
- **Closer together.** The store, the post office and the new cream saltbox stand shoulder to shoulder, their yards 1 m apart. The white gable and the farmhouse have room round them.
- **Six homes.** Three are today's (the red saltbox, the white farmhouse and the sage cottage). Three are new, all clapboard:
  - the cream saltbox and the harbour gable, with two of the cannery's four hands in each. This answers Q8: they live on the island;
  - the white gable, for a family. Its two children need your ruling that children exist (Q6).

  Marguerite still lives over the store, and Ginny's plot does not change.
- **Doors.** 7 of the 9 doors face south, and the camera sees them. Rose's and Eileen's face west onto their walks. Today's art shows those two doors side-on, so each is marked by a doorstep lantern and a gate. Claude Design's Job 1 can give them a side entry that turns the house's best face to the camera (asked in §10).
- **Yards.** One per home, each with its own fence (pickets, post and rail, wire or stone), its gate on the home's path, and dressing that says whose it is. No fence crosses a path.
- **Paths.** A path goes to every door. Green Row is the street (4 m of dirt); the rest are 1.5 m footpaths. The villagers walk a tree of them, as the game's rule requires. Five links that a routine might like would make a loop, so they stay the player's (decision 2).
- **Lights, within the island's power ruling.** An oil lantern at each of the nine doors, five hurricane lantern posts that named villagers light at dusk, the fire pit on a warm evening, and a battery light in a shed. No street lamps: the island has no poles or wires, and a line of lamps would make it read like the mainland. The busiest on-foot screen holds 3 lit pools, of the 8 the renderer draws.
- **Every screen a scene.** The routes cross 36 on-foot screens, and all 36 are scenes. Four wider views at 14 m show the village whole.
- **Nothing protected moves.** The wharf, the berths, the approach, the arrival route, the bar sightline, the cannery, the cliffs, the woods (no tree is cut) and the five fixed points stay where they are. Six buildings move, by 2.5 to 69 units, and three are new.

**Look at these first:** `plan-map.png` (the whole plan), then `windows.png` (every on-foot screen), then the four wide views.

**One finding needs your word (decision 10).** Standing still at a south-facing gate, the on-foot camera cuts off the top of the door, by 0.2 to 1.4 units. A few steps up the path the door is whole, and while you walk the camera leads you and shows it. The fix, if you want one, is a small upward lean of the camera in the village: gameplay-systems' code, and a tunable.

**The decisions** are in §14. Each has a recommendation.

---

## 1. Today, measured (charter §3.1)

![St Peters village today](st-peters-village-plan/village-today.png)

`village-today.png` is redrawn from the box at main `981f50a6`. A script reads `Assets/_Project/Scenes/StPeters.unity`, and nothing is placed by hand. It marks every building with its facing and whether the camera sees its door, and every item terrain pass 9 froze, by name.

#### T1. Today's buildings, read from the scene at 981f50a6, in the package's order

| id | at | facing (cell) | door | source |
|---|---|---|---|---|
| `school` | (-12, 33) | S (4) | seen | `StPetersBuilder.cs:747` |
| `general_store` | (4, 31) | S (4) | seen | `StPetersBuilder.cs:748` |
| `white_farmhouse` | (21, 26) | SW (5) | seen | `StPetersBuilder.cs:749` |
| `red_saltbox` | (25, 8) | W (6) | edge-on | `StPetersBuilder.cs:750` |
| `sage_cottage` | (10, -20) | N (0) | faces away | `StPetersBuilder.cs:751` |
| `post_office` | (15, 43) | S (4) | seen | `StPetersShops.cs:98` |
| `ginny_cottage` | (84, 28) | W (6) | edge-on | `StPetersGinnyPlot.cs:94` |
| `camper` | (84, 42) | S (4) | seen | `StPetersCamperLot.cs:98`, `:105` |
| `cannery` | (170, 16) | SE (3) | seen | `StPetersCannery.cs:72` |

6 of 9 doors are seen.

- **Where the package differs: the school only.** The package gives its door as SE, from the bearing to the green (150.85°, `4-st-peters-today/sites.json`). The committed scene draws the school at the `_d4` cell, S: the facing `BuildingFacing` chose from the bake's door anchors (`BuildingFacing.cs`). The camera sees the door either way. Every other position and facing agrees with the package.
- **Today's doors:** 6 of 9 are seen. The red saltbox and Ginny's cottage are edge-on (they face W). The sage cottage faces away (N), 20 units south of the green.

**What pass 9 froze** (the package's `4-st-peters-today/terrain-pass-9-frozen.md`), and what this plan does to each item:

| frozen item | where | this plan |
|---|---|---|
| the beach slip, the dredged approach, the dock, the dory berth, the arrival route, the wharf, the sandbar and the bar gut | the harbour and the bar | unchanged |
| the buildings, 8 m round each | school (−12, 33), store (4, 31), farmhouse (21, 26), red saltbox (25, 8), sage cottage (10, −20) (`StPetersBuilder.cs:747-751`) | all five move (T2, §9) |
| the post office | (15, 43), the scene's `IslandShops/postOffice` (`StPetersShops.cs:98`) | moves 7.4 |
| the cannery, 12 m round | (170, 16) | unchanged |
| Ginny's camper and freezer | (84, 42) and (78, 24) | unchanged |
| the hearth and the start spawn | (0, 14) and (5, 0) | unchanged, as are the green (2.5, 7), Ginny's mark (4, 11) and Ned's letter (−2, 10.5) (`StPetersBuilder.cs:685-715`) |
| the wet bucket | (−59, 0) | unchanged |
| the roads, 3 m each side | the slip road and the bar-head road (`StPetersStarterSplat.cs:215-372`) | unchanged; every footprint and yard keeps 3.75 m from them (the least: 3.80 m (`red_saltbox`'s yard)) |
| the yards, each outline + 3 m | the 153 placed objects under `Yards` | the village's yards are redrawn (§5); Ginny's and the camper's are unchanged |
| the passages, the cliff walls, the nav marks and the clam holes | round the island | unchanged |
| the woods, 3 m round each trunk | 259 trees under `IslandWoods` | no trunk is cut; the nearest is 3.38 m (`white_farmhouse`'s yard) |
| the ambient fleet's grounds | x −37.5 to 47.5, y −43 to −21 | unchanged; nothing in the plan stands south of the green |

---

## 2. The grid (charter §3.2)

![The plan](st-peters-village-plan/plan-map.png)

**The lines.** The plan is laid out on a few lines, each on a compass axis. Every position is derived from them and from the bakes' own sizes, so no coordinate is copied (municipal §2.4). The JSON holds them as `lines`:
- **Green Row, the street:** y 28.3, from x −19 to 57; 4 m of dirt (y 27.02 to 29.59).
- **The axis:** x 2.5, the green's own x. The Green step, the Green Walk and the store's door stand on it.
- **The Cross Walk:** y 12.5, from the west garden to the East Walk, broken by the hearth ring's gravel.
- **The East Walk:** x 13.25, the square's east side, from the Cross Walk up to Green Row.
- **North of the street:** the Bluff Path (x −6.25) and the Bluff Walk (x −17.5); the Shore Walk (y 34.5), west to the shore; the Barren Gap (x 41), north past the flake yard to pass 9's barren path.
- **South of the street:** the Farm Walk (x 34), down to the farmhouse; the Garden Walk (x −17.5), down to the west garden's gate.

**The front line.** Every fenced lot on Green Row starts 0.4 m inside the street's north edge; the store's and the post office's forecourts run to the street. Each building stands back from its fence by the least its art allows (its porch and steps, plus 0.5 m of path), then by 0 to 1 m more. So the fronts step a little and the row does not read as a wall (T4's setbacks).

**One clear middle.** The hearth (0, 14) stays the middle of the village, as a fixed point (`StPetersBuilder.cs:685-715`). The square is built round it. The axis runs from the green through the ring to the store's door, so the walk up from the slip or the bar ends looking at the store across the square.

**The classes** (municipal §2.2, `municipal-infrastructure.md:141-149`):
- **Green Row is the island's one street** (`:236`). It keeps its shipped id `route.stpeters.school_lane` (`:243`), and is now drawn: 4 m of dirt.
- **Every other link is a footpath:** 1.5 m of bare ground.
- **No walks:** a walk waits on decision 8.
- **No lanes and no alleys.** The proposed `saltbox_alley` and `sage_alley` (`:251-252`) stay proposals, blocked on municipal §7, and move with their homes.

#### T3. The lanes

| id | name | class | from → to | length (ground m) | status |
|---|---|---|---|---:|---|
| `route.stpeters.school_lane` | Green Row | street | (-19, 28.3) → (57, 28.3) | 76.0 m | shipped id, re-drawn |
| `route.stpeters.green_step` | Green step | footpath | (2.5, 7) → (2.5, 12.5) | 8.6 m | new |
| `route.stpeters.cross_walk` | Cross Walk | footpath | (-8, 12.5) → (-2.6, 12.5) | 5.4 m | new |
| `route.stpeters.cross_walk_east` | Cross Walk (east) | footpath | (2.6, 12.5) → (13.25, 12.5) | 10.7 m | new |
| `route.stpeters.green_walk` | Green Walk | footpath | (2.5, 15.66) → (2.5, 27.02) | 17.7 m | new |
| `route.stpeters.east_walk` | East Walk | footpath | (13.25, 12.02) → (13.25, 27.02) | 23.3 m | new |
| `route.stpeters.farm_walk` | Farm Walk | footpath | (34, 27.02) → (34, 17.9) | 14.2 m | new |
| `route.stpeters.bluff_path` | Bluff Path | footpath | (-6.25, 29.59) → (-6.25, 41.1) | 17.9 m | new |
| `route.stpeters.bluff_walk` | Bluff Walk | footpath | (-17.5, 29.59) → (-17.5, 34.98) | 8.4 m | new |
| `route.stpeters.shore_walk` | Shore Walk | footpath | (-37.41, 34.5) → (-17.5, 34.5) | 19.9 m | new |
| `route.stpeters.garden_walk` | Garden Walk | footpath | (-17.5, 12.5) → (-17.5, 27.02) | 22.6 m | new |
| `route.stpeters.garden_path` | Garden Path | footpath | (-22, 12.5) → (-8, 12.5) | 14.0 m | new |
| `route.stpeters.barren_gap` | Barren Gap | footpath | (41, 29.59) → (41, 43.5) | 21.6 m | new |
| `route.stpeters.plot_path` | Ginny's path | footpath | (57, 28.3) → (76.74, 26.13) | 20.2 m | shipped id, re-drawn |

**The villagers' lanes are a TREE over the grid** (`RoutineLaneTree.cs:14-33`; `RoutineLanes.Validate()`, `RoutineLanes.cs:90-114`):
- It has 23 nodes, rooted at the green.
- The twelve shipped node names are kept (`StPetersRoutines.cs:877-895`; municipal §2.4), and eleven are new.
- Every villager lane runs on a drawn path (the check "every villager lane runs on a drawn path").
- The player walks anywhere.

#### T7. The villagers' lanes: the tree

| node | parent | at | what it is |
|---|---|---|---|
| `green` | — (root) | (2.5, 7) | the root (StPetersBuilder.VillageGreen) |
| `cross_walk` | `green` | (2.5, 12.5) | the Green step meets the Cross Walk, on the ring's gravel |
| `green_walk_foot` | `cross_walk` | (2.5, 15.66) | the ring's north edge, the foot of the Green Walk |
| `lane_store` | `green_walk_foot` | (2.5, 28.3) | the store's approach on Green Row (kept name) |
| `bluff_path_foot` | `lane_store` | (-6.25, 28.3) | the Bluff Path leaves Green Row |
| `lane_harbour_gable` | `bluff_path_foot` | (-6.25, 41.1) | the harbour gable's approach, the Bluff Path's head |
| `lane_school` | `bluff_path_foot` | (-11.4, 28.3) | the school's approach (kept name) |
| `bluff_walk_foot` | `lane_school` | (-17.5, 28.3) | the Bluff Walk leaves Green Row |
| `yard_sage_cottage` | `bluff_walk_foot` | (-21.5, 34.5) | the sage cottage's approach on the Shore Walk (kept name) |
| `east_walk_head` | `lane_store` | (13.25, 28.3) | the East Walk meets Green Row |
| `yard_saltbox` | `east_walk_head` | (13.25, 20.3) | the red saltbox's approach on the East Walk (kept name): Rose's way to the counter |
| `lane_post_office` | `east_walk_head` | (18.18, 28.3) | the post office's approach (kept name) |
| `lane_cream_saltbox` | `lane_post_office` | (28.5, 28.3) | the cream saltbox's approach |
| `farm_walk_head` | `lane_cream_saltbox` | (34, 28.3) | the Farm Walk leaves Green Row |
| `lane_farmhouse` | `farm_walk_head` | (34, 18.9) | the farmhouse's approach on the Farm Walk (kept name) |
| `lane_white_gable` | `farm_walk_head` | (53, 28.3) | the white gable's approach |
| `green_row_east` | `lane_white_gable` | (57, 28.3) | Green Row's east end |
| `yard_ginny_plot` | `green_row_east` | (76.74, 26.13) | StPetersGinnyPlot.Dooryard (unchanged); via (62, 27) |
| `garden_gate` | `cross_walk` | (-17.5, 12.5) | the west garden's gate, where the Garden Path meets the Garden Walk |
| `yard_cottage` | `green` | (4, 11) | Ginny's village mark (4, 11), unchanged |
| `flats_head` | `green` | (-55, 2.5) | unchanged, with its bends (bar road) |
| `slip_head` | `green` | (190, 0) | unchanged, with its bends (slip road) |
| `wharf_head` | `slip_head` | (207.5, 1.5) | unchanged |

**The links that would make a cycle (decision 2).** Five links that a routine might use would close a loop. The plan builds none of them into the villagers' network; each stays a path that only the player walks. None is needed: every home, shop and station is reachable in the tree.

#### T8. The links that would make a cycle (decision 2)

| link | between | by | whose day would use it |
|---|---|---|---|
| east_walk | `yard_saltbox` and `cross_walk` | route.stpeters.east_walk south of the red saltbox, and route.stpeters.cross_walk_east | Rose down to the hearth bench of an evening; in the tree the walk goes by Green Row and the Green Walk |
| garden_walk | `garden_gate` and `bluff_walk_foot` | route.stpeters.garden_walk | the school and the sage cottage down to the garden beds; in the tree they go round by the ring |
| ring | `cross_walk` and `green_walk_foot` | the hearth ring (either side) | nobody: a villager crosses the ring on one side only |
| barren_gap | `lane_cream_saltbox` and barren path | route.stpeters.barren_gap | no routine today (pass 9's barren path is the player's) |
| shore_walk | `yard_sage_cottage` and shore path | route.stpeters.shore_walk, west of the cottage | no routine today (pass 9's shore path is the player's) |

---

## 3. The buildings (charter §3.3)

#### T2. The nine buildings

| id | rig · preset | at | facing (cell) · door | door seen | on · approach | moved (world units) | who |
|---|---|---|---|---|---|---|---|
| `school` | HouseIso · Buildings.json school | (-11.4, 35.42) | S (4) · (-11.4, 32.97) | seen | Green Row · (-11.4, 28.3) | 2.5 from (-12, 33) | Eileen teaches here (station.st_peters.school_desk) |
| `general_store` | Shopfront · harbourStore | (2.5, 35.42) | S (4) · (2.5, 32.21) | seen | Green Row · (2.5, 28.3) | 4.7 from (4, 31) | Marguerite, who lives over it |
| `post_office` | Shopfront · villagePost | (16.5, 35.77) | S (4) · (18.18, 33.03) | seen | Green Row · (18.18, 28.3) | 7.4 from (15, 43) | Rose keeps its counter (station.st_peters.post_office_counter, RoseMacIsaacRoutine.asset:24) |
| `cream_saltbox` | HouseIso · new: cream_saltbox | (28.5, 35.7) | S (4) · (28.5, 32.98) | seen | Green Row · (28.5, 28.3) | new | two of the cannery hands (Q8) |
| `white_gable` | HouseIso · new: white_gable | (53, 35.2) | S (4) · (53, 32.48) | seen | Green Row · (53, 28.3) | new | a family with two school-age children (the school gets its pupils) |
| `red_saltbox` | HouseIso · Buildings.json redSaltbox | (22.56, 19.6) | W (6) · (18.22, 19.6) | edge-on | East Walk · (13.25, 20.3) | 11.8 from (25, 8) | Rose |
| `white_farmhouse` | HouseIso · Buildings.json whiteFarmhouse | (43.43, 18.9) | W (6) · (38.46, 18.9) | edge-on | Farm Walk · (34, 18.9) | 23.5 from (21, 26) | Eileen |
| `sage_cottage` | HouseIso · Buildings.json sageCottage | (-21.5, 41.64) | S (4) · (-21.5, 39.05) | seen | Shore Walk · (-21.5, 34.5) | 69.2 from (10, -20) | Junior and Basil (housemates) |
| `harbour_gable` | HouseIso · new: harbour_gable | (-6.25, 46.12) | S (4) · (-6.25, 43.47) | seen | Bluff Path · (-6.25, 41.1) | new | the other two cannery hands (Q8) |

**Facings:**
- **7 of 9 face S** (cell 4), and the camera sees all seven doors.
- **The red saltbox and the white farmhouse face W** (cell 6), onto the East Walk and the Farm Walk.
  - Their doors are edge-on in today's art, so each is marked: a doorstep lantern 0.67 from the door, and a gate on its walk (the check "edge-on door marked").
  - Windows E2 and F2 hold each door in frame (T9).
  - The owner's words allow this: "some may enter from the east or west but the building will be designed to still showcase its architecture". Job 1's side entry is the art that finishes it (§10).

**The facing is now data.** Today the builder turns each door toward the green (`StPetersVillage.cs:158-171`, through `BuildingFacing.FacingToward`). In this plan the doors face the street or their own walk, not the green. So the facing becomes a field of the building's `LotDef` (§11). That moves the premise of every test that derives a facing (§9).

**Rigs and presets, from today's kits:**
- The school and the three kept homes keep their `Buildings.json` presets (the houses rig, `houseIsoRig.js`).
- The store and the post office keep their shop-kit shells (`harbourStore`, `villagePost`).
- The three new homes are new option sets on the same houses rig, all clapboard (the owner's ruling, 21:10Z):

| new home | shape · siding · body · roof | windows · attic · porch · chimneys | size | for |
|---|---|---|---:|---|
| `cream_saltbox` | saltbox · clapboard · cream · brown asphalt | two-over-two · none · front · 1 | 0.35 | two cannery hands |
| `white_gable` | colonial gable · clapboard · white · brown asphalt | six-over-six · gable · front · 1 | 0.35 | a family |
| `harbour_gable` | plain gable · clapboard · blue · grey asphalt | two-over-two · gable · front · 1 | 0.30 | two cannery hands |

Their exact option literals are in the JSON (`buildings[].optsJs`). Each renders on node from today's rig (the pictures show them).

**Notes:**
- **Every door is on its porch front.** Every building in the plan has a front (or wrap) porch, so its door is on the rig's +Y gable (`houseIsoRig.js:748-750`). #853's R2, the honest door anchor, does not touch this plan.
- **The harbour gable is a home, not a harbourmaster's.** The name says where it stands, above the harbour shore. The game's harbourmaster already works at Nine Mile Creek (the seat's correction to the package).
- **The moves.** Six buildings move. The sage cottage moves furthest (69.2): from south of the green, where its door faced away, to the Shore Walk, where it faces the camera.

### 3.1 The manor (decision 4)

The owner asked for suggestions for its site (21:10Z: "make suggestions for location"; the seat reads it as proposals, not a ruling). The game has no manor art, so a manor would be a new bake from the manor rig in the package's houses kit (#853's). The sizes below are that rig's presets (`manorIsoRig.params.json`); `harbourmaster` is one preset's name, and on St Peters it would be a manor, not a harbourmaster's building. Here are three sites, measured on pass 9's maps the way the plan measures a home:

#### T13. Three sites for a manor, measured on pass 9's maps

| site | door · preset | bar sightline margin | on frozen paint | ground: lowest · fall | biome | reads |
|---|---|---:|---:|---|---|---|
| M1: the barren rise, at the head of the Barren Gap | (41, 44) · mansardManor | +47.78 | 0% | 4.25 m · 2.27 m | Blueberry barren 99%, Roadside meadow 1% | off the +6 m plateau; takes pass 9's blueberry barren |
| M2: the east end of Green Row, on the white gable's lot and 4 m east of it (the manor as one of the six) | (56, 33.91) · harbourmaster | +57.70 | 0% | 5.55 m · 1.10 m | Roadside meadow 68%, Blueberry barren 32% | possible |
| M3: west of the school, at Green Row's west end | (-34, 33.91) · mansardManor | -10.33 | 0% | -0.50 m · 6.50 m | Roadside meadow 66%, Harbour shore 34% | fails the bar sightline (protected); off the +6 m plateau |

- **M2 is the only site that works.** The manor would stand at the east end of Green Row, as the row's end house, in the white gable's place. It would be one of the six, and its household the sixth home's.
- **M1** sits on low ground, off the plateau, and takes pass 9's blueberry barren.
- **M3** breaks the bar sightline, which is protected.

---

## 4. The commons (charter §3.4)

**Sizes and sites** (the JSON's `commons`):
- **The square:** x −8 to 12.5, y 9 to 27.02 (20.5 × 28 m), from the green up to Green Row.
  - The hearth ring (r 3 units, the fire pit in it) stays at (0, 14), the middle of the village.
  - By it are two log benches. North of it, beside the Green Walk, is the notice board.
  - Two benches stand by the Green Walk, with three flower beds and two young red maples at pole stage.
- **The west garden:** x −24 to −8, y 9 to 27.02 (16 × 28 m), west of the square across the Garden Path.
  - Four garden beds, two benches, a water butt and a low potting bench.
  - It is public ground: whoever's day it is works the beds.
- **The flake yard:** x 34 to 48, y 29.59 to 41 (14 × 17.8 m), north of Green Row by the Barren Gap.
  - Four fish flakes, a trap stack and a bench.
  - It is the cannery hands' work ground, and the focal of the street's east end.
- **On the Shore Walk:** a bench and a buoy post, looking out over the harbour shore.
- **Somewhere to gather:** the hearth ring, with its fire pit and log benches, on a hall night or a warm evening.

Every piece is from today's kits: the yard kit's fire pit, benches, island beds, rain barrel, tool lean-to and garden rows; the wharf kit's bench, notice board, cod flakes and trap stack; and the tree kit's red maple. §10 lists what a commons kit would add.

**Stations** (proposed `station.st_peters.*`; V2 ships them with the routines that use them):

#### T5. The commons' stations

| id | at | lane node | whose day |
|---|---|---|---|
| `station.st_peters.hearth_bench_a` | (-2.3, 16.3) | `cross_walk` | Rose's evening sit |
| `station.st_peters.hearth_bench_b` | (-0.3, 17.1) | `cross_walk` | Junior and Basil, after the tide's in |
| `station.st_peters.notice_board` | (4.4, 17.8) | `green_walk_foot` | everyone's morning look; Marguerite pins the notices |
| `station.st_peters.green_bench_a` | (0, 21.9) | `green_walk_foot` | the cannery hands' lunch |
| `station.st_peters.green_bench_b` | (5.2, 21.9) | `green_walk_foot` | Eileen, marking, after school |
| `station.st_peters.garden_bed_a` | (-20.4, 15.3) | `garden_gate` | Eileen's rows (Saturday) |
| `station.st_peters.garden_bed_b` | (-12.5, 20.8) | `garden_gate` | the family's rows (the white gable) |
| `station.st_peters.garden_bench` | (-22.5, 14.2) | `garden_gate` | Ginny, some evenings, before the walk home |
| `station.st_peters.flake_yard` | (39, 30.6) | `lane_cream_saltbox` | the cannery hands, turning the fish (fine days) |

- Each station's lane node is on the tree (T7), and each names whose day uses it (municipal §2.5).
- The green's four slots are unchanged: `green_a` to `green_d`, the centre one Ginny's (`StPetersRoutineContentTests:383`).

---

## 5. Yards (charter §3.5)

#### T4. Yards and fences

| building (its lot) | street · setback | outline x · y | size | gate | fence | panels · posts · gates | fence run (world units) | mown |
|---|---|---|---|---|---|---|---:|---|
| `school` | Green Row · 1.0 | -16.35..-7.40 · 29.98..39.00 | 9.0 × 14.0 m | (-11.4, 29.98) | PostRail | 16 · 0 · 0 | 34.13 | Striped |
| `general_store` | Green Row · 0.0 | -5.10..8.25 · 29.59..39.50 | 13.3 × 15.4 m | (2.5, 29.59) | open forecourt | — | — | Striped |
| `post_office` | Green Row · 1.0 | 9.25..22.75 · 29.59..40.00 | 13.5 × 16.2 m | (18.18, 29.59) | open forecourt | — | — | Kept |
| `cream_saltbox` | Green Row · 1.0 | 23.75..34.00 · 29.98..41.00 | 10.2 × 17.1 m | (28.5, 29.98) | Wire | 16 · 0 · 0 | 40.73 | Kept |
| `white_gable` | Green Row · 0.5 | 48.00..58.50 · 29.98..40.50 | 10.5 × 16.4 m | (53, 29.98) | Picket | 19 · 6 · 1 | 40.23 | Kept |
| `red_saltbox` | East Walk · 1.0 | 14.40..32.00 · 8.93..26.61 | 17.6 × 27.5 m | (14.4, 20.3) | PostRail | 27 · 0 · 0 | 68.77 | Kept |
| `white_farmhouse` | Farm Walk · 0.5 | 35.15..49.50 · 11.50..26.61 | 14.4 × 23.5 m | (35.15, 18.9) | Picket | 27 · 6 · 1 | 57.13 | Kept |
| `sage_cottage` | Shore Walk · 1.7 | -25.50..-17.40 · 35.38..45.50 | 8.1 × 15.7 m | (-21.5, 35.38) | Picket | 18 · 6 · 1 | 34.64 | Striped |
| `harbour_gable` | Bluff Path · 0.0 | -13.00..0.50 · 41.50..49.50 | 13.5 × 12.4 m | (-6.25, 41.5) | Stone | 20 · 6 · 0 | 41.20 | Kept |

**One per home, distinct and separate:**
- Every yard holds its own building's footprint plus 0.6 m; the least hold is 0.70 m (`sage_cottage`).
- No two yards touch. The least gap is 1.00 (`general_store` and `post_office`).
- Every gate is on the home's path (T2: the approach, then the gate, then the door).

**Fences:**
- **Kept:** the school's post and rail, the farmhouse's and the sage cottage's pickets, and the red saltbox's post and rail (`StPetersYards.cs:120-156`).
- **Open forecourts:** the store and the post office. The store's reason stands: "you walk up to a counter, you do not open a gate to buy flour" (`StPetersYards.cs:133-134`). The post office has no yard today, and gets one on the same terms.
- **New:** the cream saltbox gets page wire (a working house), the white gable pickets (a family's kept front), and the harbour gable a low fieldstone wall with capped pillars.

**YardPlan's rules:**
- A gate opens 1.8 m (`YardPlan.GateWidthMetres`, `YardPlan.cs:196`).
- A fence never crosses a lane (`municipal-infrastructure.md:335`). The check "fences never cross a lane" passes on all 7 fenced yards.
- Every fence run tiles with whole panels (the check "fences tile without offcuts").

**Gate leaves.** Only the picket style draws a gate leaf; post and rail, wire and stone leave a 1.8 m gap (`YardDressing.cs:96-108`). The plan uses that as it stands. Gate leaves for the other three are an art ask (§10).

**Dressing that says whose.** There are 45 pieces from the yard kit. Each is settled inside its own yard, off the house, the porch, the front path and the other pieces (the check "dressing"). For example:
- the school: a flagpole, a swing set and a bench;
- the post office: a mailbox, a planter box and a bicycle;
- the cream saltbox: an oil tank, a clothesline and a trap stack;
- the white gable: a sandbox, a doghouse and a swing set;
- the sage cottage: a buoy post, an oilskin line and a dory planter;
- the harbour gable: an anchor, a trap bench and a woodpile.

**Mown.** The owner's three striped lawns stay striped: the school, the store and the sage cottage (`StPetersYards.cs:141-142`). Every other village yard is kept. Ginny's stays rough, so the island still shows all three (`StPetersLawnTests:54`).

---

## 6. Paths and lights (charter §3.6)

**A path to every door.** Each building's row in T2 gives its lane and its approach; the path runs from the approach, through the gate, to the door. T3 gives the classes. A shell walk at the store and the post office waits on decision 8.

**The lights, within the island's power ruling.** The ruling (`municipal-infrastructure.md:341`): "There is not one pole on St Peters. Not one wire in from the mainland. Not one street lamp." Instead there is one source per job (`:352-361`).

#### T6. The lights

| light | at | of | energy | lit |
|---|---|---|---|---|
| doorstepLantern | (-10.5, 33.27) | school | oil (a house's light at dusk) | dusk to bedtime, by the household |
| doorstepLantern | (3.4, 32.51) | general_store | oil (a house's light at dusk) | dusk to bedtime, by the household |
| doorstepLantern | (19.08, 33.33) | post_office | oil (a house's light at dusk) | dusk to bedtime, by the household |
| doorstepLantern | (29.4, 33.28) | cream_saltbox | oil (a house's light at dusk) | dusk to bedtime, by the household |
| doorstepLantern | (53.9, 32.78) | white_gable | oil (a house's light at dusk) | dusk to bedtime, by the household |
| doorstepLantern | (17.92, 19) | red_saltbox | oil (a house's light at dusk) | dusk to bedtime, by the household |
| doorstepLantern | (38.16, 18.3) | white_farmhouse | oil (a house's light at dusk) | dusk to bedtime, by the household |
| doorstepLantern | (-20.6, 39.35) | sage_cottage | oil (a house's light at dusk) | dusk to bedtime, by the household |
| doorstepLantern | (-5.35, 43.77) | harbour_gable | oil (a house's light at dusk) | dusk to bedtime, by the household |
| lanternPost | (3.92, 26.52) | green_walk | hurricane lantern (lanternPost) | lit at dusk by Marguerite, closing up (a routine block) |
| lanternPost | (3.92, 8.3) | green | hurricane lantern (lanternPost) | lit at dusk by whoever is last up from the slip |
| lanternPost | (32.58, 26.52) | farm_walk | hurricane lantern (lanternPost) | lit at dusk by Eileen, home from the school |
| lanternPost | (42.42, 30.09) | barren_gap | hurricane lantern (lanternPost) | lit at dusk by the cannery hands at the cream saltbox |
| lanternPost | (-16.08, 26.52) | bluff_walk | hurricane lantern (lanternPost) | lit at dusk by Junior or Basil, whoever is home first |
| firePit | (0, 14) | square | wood (the hearth ring) | a hall night or a warm evening only |
| batteryShedLight | (-17.9, 44.9) | sage_cottage | battery set (a shed light) | a gear night |

- **Doorstep lanterns (9).** An oil lantern on a bracket by each door, lit at dusk and put out at bedtime by the household. In the ruling's table, "light in a house at dusk" is an oil lamp (`:354`). The plan counts each as a `WindowGlow` pool (reach 3.4) until the lantern preset exists.
- **Lantern posts (5).** The kit's `lanternPost` (`Assets/_Project/Art/Sprites/Wharf/Decor/lanternPost.png`), each lit at dusk by a named villager, as a routine block:
  - Marguerite, closing up, at the Green Walk's head;
  - whoever is last up from the slip, at the green, where the two roads come in;
  - Eileen, home from the school, at the Farm Walk's head;
  - the cannery hands, at the Barren Gap;
  - Junior or Basil, whoever is home first, at the foot of the Bluff Walk.

  The ruling gives the hurricane lantern on a `lanternPost` to "working after dark in a dooryard" (`:358`), and one lantern to the slip head, "lit by whoever is last off the water" (`:359`). The slip head keeps its own lantern, outside the village. These five posts read "dooryard" as the village's shared ground as well as a household's; that reading is yours to confirm (decision 3).
- **The fire pit.** A wood fire in the hearth ring, on a hall night or a warm evening only.
- **The battery light.** A small shed light at the sage cottage on a gear night: the ruling's battery layer (`:360`).

**The preset (an art ask, municipal §6.2 #2):**
- Today `LampPosts.PresetFor` maps `lanternPost` to `Lightpost` (`LampPosts.cs:102`): a steady road lamp's pool.
- §6.2 #2 asked for a `Lantern` preset: warm, small, with a flicker of about 0.15 (`municipal-infrastructure.md:693-715`).
- A `Lantern` kind has shipped since, but it is the walker's CARRIED lantern (`LightPresets.cs:71`, `:234-241`: flicker 0.09, reach 3; used by `WalkerLights.cs:228-229`).
- So the ask is now narrower: point `lanternPost` and the doorstep lantern at a hung-lantern preset, either `Kind.Lantern` reused or a hung case with the stronger flicker. That is one enum case and one mapping line (V3).

**Per on-foot window** (every window's count is in T9):
- **Pools:** the most lit pools in one window are 2 lantern posts; 3 with the doorsteps; 3 with everything lit. The renderer draws 8 (`LampShadowProfile.MaxPools`, ADR 0016 `:1054`).
- **Shadows:** if every light cast shadows, the most lamp-to-caster pairs in one window would be 18, of the 24 `MaxShadows` (ADR 0016 `:502`; `lighting-and-daynight.md:168`: past 24 the nearest pairs win, and the scan is O(lamps × casters) at 10 Hz).
- **Casters:** the island's go from 438 to 440.

**A village lamp line, separately: what it would change.** The prototype also measured a `streetLamp` (4.48 m, `municipal-infrastructure.md:95`) every 12 m along Green Row, the Green Walk, the East Walk and the Cross Walk: 12 lamps.
- **It breaks the ruling three ways:** it needs poles, it needs a wire (from a genset run every night, or from the mainland), and it is street lamps.
- **It changes what the island is at night.** "The contrast is VARIED versus UNIFORM" (`municipal-infrastructure.md:49-53`). A line of identical lamps is the mainland's look, and it is what the player would see from the water.
- **It goes over budget:** 6 pools in one window (of 8), and 26 lamp-to-caster pairs in one window if every light cast (of 24).

---

## 7. Every screen a scene (charter §3.7)

**The method:**
- `StPetersGroundCoverBudgetTests` tiles the island into 16 × 9 windows on the grid (16i, 9j) (`:77`). The plan uses the same tiling over x −38 to 80, y 0 to 57.
- It keeps every window a route crosses: the plan's lanes, each front path, and pass 9's roads and footpaths. That makes 36 windows.
- Each window is rendered on node from the kits' rigs at the plan's facings, on pass 9's ground and biomes, with the yards, fences, dressing, commons, pass 9's trees and the lights.
- Each window is named by its key and its centre, in world units.

![Every on-foot window](st-peters-village-plan/windows.png)

#### T9. The on-foot windows (16 × 9 world units, the on-foot camera), every one a route crosses

| window | centre | routes | buildings (facing, door in frame) | yards | light pools: posts · doorsteps · all · line | focal | verdict |
|---|---|---|---|---|---|---|---|
| B6 | (-24, 58.5) | Shore path (pass 9) | — | — | 0 · 0 · 0 · 0 | pass 9's harbour shore | a scene |
| C6 | (-8, 58.5) | Shore path (pass 9) | harbour_gable S (no door) | — | 0 · 0 · 0 · 0 | pass 9's roadside meadow | a scene |
| H6 | (72, 58.5) | Barren path (pass 9) | — | — | 0 · 0 · 0 · 0 | pass 9's blueberry barren | a scene |
| B5 | (-24, 49.5) | Shore path (pass 9) | sage_cottage S (no door) | sage_cottage | 0 · 0 · 1 · 0 | sage_cottage (its roof) | a scene |
| E5 | (24, 49.5) | Barren path (pass 9) | — | — | 0 · 0 · 0 · 0 | pass 9's blueberry barren | a scene |
| G5 | (56, 49.5) | Barren path (pass 9) | — | — | 0 · 0 · 0 · 0 | pass 9's blueberry barren | a scene |
| H5 | (72, 49.5) | Barren path (pass 9) | — | — | 0 · 0 · 0 · 0 | pass 9's blueberry barren | a scene |
| A4 | (-40, 40.5) | Shore path (pass 9) | — | — | 0 · 0 · 0 · 0 | the woods (3 trees and shrubs) | a scene |
| B4 | (-24, 40.5) | Shore path (pass 9), front path, sage_cottage | sage_cottage S (door ✓) | school, sage_cottage | 0 · 1 · 2 · 1 | sage_cottage's door | a scene |
| C4 | (-8, 40.5) | Bluff Path, front path, harbour_gable | school S (no door); general_store S (no door); harbour_gable S (no door) | school, general_store, harbour_gable | 0 · 2 · 3 · 2 | harbour_gable's front | a scene |
| E4 | (24, 40.5) | Barren path (pass 9) | post_office S (no door); cream_saltbox S (no door) | post_office, cream_saltbox | 0 · 2 · 2 · 2 | cream_saltbox (its upper front and roof) | a scene |
| F4 | (40, 40.5) | Barren Gap, Barren path (pass 9) | cream_saltbox S (no door) | cream_saltbox | 0 · 0 · 0 · 0 | pass 9's roadside meadow | a scene |
| G4 | (56, 40.5) | Barren path (pass 9) | white_gable S (no door) | white_gable | 0 · 1 · 1 · 1 | white_gable (its upper front and roof) | a scene |
| A3 | (-40, 31.5) | Shore Walk, Shore path (pass 9) | — | — | 0 · 0 · 0 · 0 | the bench | a scene |
| B3 | (-24, 31.5) | Bluff Walk, Garden Walk, Green Row, Shore Walk, front path, sage_cottage | — | school, sage_cottage | 1 · 2 · 2 · 3 | pass 9's roadside meadow | a scene |
| C3 | (-8, 31.5) | Bluff Path, Green Row, front path, school | school S (door ✓); general_store S (no door) | school, general_store | 1 · 3 · 3 · 5 | school's door | a scene |
| D3 | (8, 31.5) | East Walk, Green Row, Green Walk, front path, general_store | general_store S (door ✓); post_office S (no door) | general_store, post_office | 1 · 3 · 3 · 6 | general_store's door | a scene |
| E3 | (24, 31.5) | Green Row, front path, cream_saltbox, front path, post_office | post_office S (door ✓); cream_saltbox S (door ✓) | post_office, cream_saltbox | 1 · 3 · 3 · 5 | cream_saltbox's door | a scene |
| F3 | (40, 31.5) | Barren Gap, Farm Walk, Green Row | — | cream_saltbox | 2 · 3 · 3 · 5 | the fish flakes | a scene |
| G3 | (56, 31.5) | Ginny's path, Green Row, front path, white_gable | white_gable S (door ✓) | white_gable | 0 · 1 · 1 · 2 | white_gable's door | a scene |
| B2 | (-24, 22.5) | Garden Walk | — | — | 1 · 1 · 1 · 2 | the vegRows | a scene |
| D2 | (8, 22.5) | East Walk, Green Walk, front path, red_saltbox | — | red_saltbox | 1 · 2 · 2 · 6 | the notice board | a scene |
| E2 | (24, 22.5) | front path, red_saltbox | red_saltbox W (door ✓) | red_saltbox | 1 · 2 · 2 · 5 | red_saltbox's door | a scene |
| F2 | (40, 22.5) | Farm Walk, front path, white_farmhouse | white_farmhouse W (door ✓) | white_farmhouse | 2 · 3 · 3 · 5 | white_farmhouse's door | a scene |
| G2 | (56, 22.5) | Ginny's path | white_farmhouse W (no door) | white_farmhouse | 0 · 0 · 0 · 1 | pass 9's roadside meadow | a scene |
| H2 | (72, 22.5) | Ginny's path | — | — | 0 · 0 · 0 · 0 | the GinnyPlot | a scene |
| B1 | (-24, 13.5) | Garden Path, Garden Walk | — | — | 0 · 0 · 0 · 0 | the vegRows | a scene |
| C1 | (-8, 13.5) | Cross Walk, Garden Path | — | — | 0 · 0 · 1 · 3 | the hearth and its fire pit | a scene |
| D1 | (8, 13.5) | Cross Walk (east), East Walk, Green Walk, Green step | — | red_saltbox | 1 · 2 · 3 · 5 | the hearth and its fire pit | a scene |
| F1 | (40, 13.5) | Farm Walk | white_farmhouse W (no door) | white_farmhouse | 0 · 1 · 1 · 1 | white_farmhouse's yard dressing | a scene |
| A0 | (-40, 4.5) | Bar-head road (pass 9), Shore path (pass 9) | — | — | 0 · 0 · 0 · 0 | the roadside: a chalked board at the bar head: the tide times for the crossing | a scene |
| B0 | (-24, 4.5) | Bar-head road (pass 9) | — | — | 0 · 0 · 0 · 0 | the roadside: a salt-fish flake on the meadow above the bar-head road | a scene |
| C0 | (-8, 4.5) | Bar-head road (pass 9) | — | — | 0 · 0 · 0 · 1 | the roadside: the garden's roadside stand: an honesty box by the road | a scene |
| D0 | (8, 4.5) | Bar-head road (pass 9), Green step, Slip road (pass 9) | — | — | 1 · 1 · 1 · 3 | a lantern post | a scene |
| E0 | (24, 4.5) | Slip road (pass 9) | — | red_saltbox | 0 · 0 · 0 · 0 | the roadside: a flake on the meadow above the slip road | a scene |
| F0 | (40, 4.5) | Slip road (pass 9) | — | — | 0 · 0 · 0 · 0 | pass 9's woods floor | a scene |

36 windows; 36 are "a scene". The most light pools in one window: posts 2, with doorsteps 3, everything lit 3, with a lamp line 6 (of `MaxPools` 8). The most lamp-shadow pairs in one window, if every light cast: everything lit 18, with a lamp line 26 (of `MaxShadows` 24).

**The rule** (charter §3.7): no window may show only bare ground, a building's back, or an edge-on door with nothing to mark it. Every window has a focal:
- a door, or a building's front;
- the square's hearth, the notice board, the flakes or a garden;
- a lantern post;
- a roadside piece placed for it (A0 to C0, and E0): a chalked board of the tide times at the bar head, a salt-fish flake, the garden's honesty box, and a flake above the slip road;
- or one of pass 9's biomes: the harbour shore, the blueberry barren, the roadside meadow or the woods floor.

No back is shown: every building faces S or W, and the camera looks north.

**The wide views at 14 m** (the default and dory framing, `CameraFollow.cs:55`):

#### T10. The wide views at 14 m

| view | centre | buildings | light pools (all lit) | focal |
|---|---|---|---|---|
| Up from the slip: the green, the Green step and the square | (2.5, 9) | — | 2 | the hearth and its fire pit |
| The square: the hearth, the benches, the notice board, the maples, and Rose's door across the East Walk | (9, 18.5) | red_saltbox | 4 | red_saltbox's door |
| The bluff: the harbour gable at the Bluff Path's head, the sage cottage on the Shore Walk, the school below | (-12, 41) | school, general_store, sage_cottage, harbour_gable | 5 | harbour_gable's door |
| Green Row east: the farmhouse down the Farm Walk, Rose's back garden, the cream saltbox and the flake yard | (38, 25) | cream_saltbox, red_saltbox, white_farmhouse | 4 | white_farmhouse's door |

![Up from the slip](st-peters-village-plan/wide-arrival.png)
![The square](st-peters-village-plan/wide-square.png)
![The bluff](st-peters-village-plan/wide-bluff.png)
![Green Row east](st-peters-village-plan/wide-east.png)

### 7.1 The door frames: a finding (decision 10)

The prototype also framed each gate at the default on-foot rung: 15 × 8.44 units (`GameConfig.asset:405-412`; the on-foot view is 9 units tall, `CameraFollow.cs:59`).

![The door frames](st-peters-village-plan/gates.png)

#### T11. The door frames

| frame | rect | the building in it | focal | verdict |
|---|---|---|---|---|
| gate_school | -18.9..-3.9 · 25.8..34.2 | school: its front, the door out of frame | school's front | a scene |
| gate_general_store | -5.0..10.0 · 25.4..33.8 | general_store: its front, the door out of frame | general_store's front | a scene |
| gate_post_office | 10.7..25.7 · 25.4..33.8 | post_office: its front, the door out of frame; cream_saltbox: its front, the door out of frame; red_saltbox: its door end, the door out of frame | post_office's front | a scene |
| gate_cream_saltbox | 21.0..36.0 · 25.8..34.2 | cream_saltbox: its front, the door out of frame; red_saltbox: its long south wall and roof, the door out of frame | cream_saltbox's front | a scene |
| gate_white_gable | 45.5..60.5 · 25.8..34.2 | white_gable: its front, the door out of frame; white_farmhouse: its long south wall and roof, the door out of frame | white_gable's front | a scene |
| gate_red_saltbox | 6.9..21.9 · 16.1..24.5 | red_saltbox: its door (side-on) and porch | red_saltbox's door | a scene |
| gate_white_farmhouse | 27.6..42.6 · 14.7..23.1 | white_farmhouse: its door (side-on) and porch | white_farmhouse's door | a scene |
| gate_sage_cottage | -29.0..-14.0 · 31.2..39.6 | school: its front, the door out of frame; sage_cottage: its front, the door out of frame | sage_cottage's front | a scene |
| gate_harbour_gable | -13.8..1.2 · 37.3..45.7 | school: its upper front and roof; general_store: its upper front and roof; harbour_gable: its door and front | harbour_gable's door | a scene |

- **At the five Green Row gates and the sage cottage's,** the frame holds the building's front, but the top of the door is cut: by 0.23 (the white gable) to 1.41 (the sage cottage) units.
  - Standing on the street's centre line, it is cut by 1.72 to 2.58 at the default rung, and by 0.31 to 1.18 at the widest (11.25 units tall).
- **The door is whole a few steps up the path,** 1.1 to 3.0 m inside the gate.
- **While you walk, you see it.** The camera looks ahead as you move (0.35 s, up to 2.5, `CameraFollow.cs:199-206`). So walking up to a door you see it; standing still at the gate you do not see its top.
- **The other three doors are in frame at their gates:** the two W doors (the red saltbox and the farmhouse) and the harbour gable's.

---

## 8. The people (charter §3.8)

**Who lives in the new homes.** Today four roofs hold six people (settlement-population §1.2, `:80-93`). The cannery's four hands have no beds (`:280-296`), and Q8 asks whether they cross the water daily (`:655-661`).
- **The cream saltbox:** two of the cannery's four hands.
- **The harbour gable:** the other two. Housing all four answers Q8 with "on the island". That is yours to rule (decision 4).
- **The white gable:** a family with two school-age children, so the school gets its pupils. The children need Q6 (`:645-646`): do children exist as NPCs? (`BoyBuild` and `GirlBuild` are baked and unused.) If Q6 is no, the white gable is a couple's home.

**Their Defs, proposed (names only; NPC content is V2).**
- The ids follow `npc.first_last` and `routine.first_last`.
- The names are placeholders for the roster's author, who may rename them before V2 ships; the ids are stable once shipped.
- None repeats a surname already on the roster (`Data/NPCs/`).

| home | NpcDef | RoutineDef | their day, in a line |
|---|---|---|---|
| cream saltbox | `npc.theresa_gillis`, `npc.angus_gillis` | `routine.theresa_gillis`, `routine.angus_gillis` | a shift at the cannery, lunch on a square bench, the flakes on a fine day, the Barren Gap's lantern at dusk |
| harbour gable | `npc.dougie_macneil`, `npc.rene_chiasson` | `routine.dougie_macneil`, `routine.rene_chiasson` | a shift at the cannery, the flakes, an evening at the hearth |
| white gable | `npc.paul_landry`, `npc.monique_landry`; if Q6, also `npc.lucie_landry` and `npc.sam_landry` | one per person, as above | the children at Eileen's school, the family's rows in the west garden; the parents' work is V2's to author |

**What that adds to the routines (V2):**
- **Stations:**
  - `home_cream_a`, `home_cream_b`, `home_harbour_a`, `home_harbour_b` and `home_white_gable_a` to `_d`, in the `home_*` pattern (`StPetersRoutines.cs:898-937`);
  - the dooryards `cream_dooryard`, `harbour_dooryard` and `white_gable_dooryard`;
  - the cannery's work stations, off `cannery_yard`;
  - the commons' nine (T5).
- **Lanes:**
  - the tree's eleven new nodes (T7);
  - `route.stpeters.cannery_walk` (`slip_head` to `cannery_yard`, municipal `:249`), a leaf, as proposed there.
- **Inhabitants:** six today become twelve, or fourteen with the children. `StPetersInhabitantsTests:69` ("four to six named inhabitants") changes its premise (§9).
- **Lighting at dusk:** four of the five lantern posts are routine blocks of named villagers (T6).

---

## 9. Sequencing with terrain pass 9 (charter §3.9)

**Every frozen item the plan moves, by how much, and whose premise moves.** Tests are named with their production subject, as *TestClass (subject)*:

| moved | by (world units) | guards whose premise moves |
|---|---:|---|
| the school's pad | 2.5 | *StPetersVillageTests (StPetersBuilder, StPetersVillage)*: `:690` (no overlap and a lane between, measured on circles of each footprint's diagonal: 6 pairs of the plan would fail it; the rule becomes footprints 4 m apart), `:856` (every door faces the green) |
| the store's pad | 4.7 | *StPetersShopsTests (StPetersShops, StPetersBuilder)*: `:55` (the store did not move), `:140` (every shop door turns toward the green); *StPetersMachinesTests (StPetersMachines)*: `:61` (the row from the store's yard gate: with the store facing S the row would land on Green Row, so it turns into the forecourt) |
| the post office | 7.4 | *StPetersShopsTests*: `:77` (every shop clears every house), `:140` |
| the red saltbox | 11.8 | *StPetersVillageTests*: `:856` (its door faces the East Walk, not the green) |
| the white farmhouse | 23.5 | the same |
| the sage cottage | 69.2 | the same |
| every village yard | redrawn | *StPetersYardTests (StPetersYards, YardPlan)*: `:83` (the meadow outside every yard is what it was), `:130` (each yard holds a 6.40 m circle round its building: 8 of 9 would fail it, so the rule becomes the footprint + 0.6), `:215` (every yard belongs to a building), `:282` (the gate faces the way you arrive: the lot's approach, not the green); *StPetersLawnTests (StPetersLawns)*: `:117`, `:139` |
| the lanes | `school_lane` drawn, `plot_path` redrawn, 12 new | *StPetersRoutineContentTests (StPetersRoutines, RoutineLanes)*: `:142` (the new stations are declared), `:190` (still a tree, now 23 nodes), `:264` (the woods keep off the painted lanes: the new ones too) |
| three new buildings | new | *StPetersVillageTests*: `:636` (the five buildings the docs ask for become eight in the village), `:773` (the clearing holds every footprint: the plan's reach 62.5 m from the hearth, past `VillageClearingRadius` 44; the rule becomes 3 m from every trunk); *StPetersInhabitantsTests (StPetersInhabitants)*: `:296` (nobody stands inside the five, then nine); *StPetersDecorTests (StPetersDecor)*: `:217`, `:267` (a grass clearing each) |
| the lantern post's preset | `Lightpost` to a hung lantern | *LampPostsTests (LampPosts)*: `:66` (each kit piece takes the preset its height asks for) |

**Held, and re-run** (their premise holds):
- *StPetersVillageTests* `:707` (the nearest building to the hearth: 15.16 m (`general_store`), of 8 m) and `:744` (the bar sightline: the least margin is 2.61 m (`sage_cottage`));
- *StPetersInhabitantsTests* `:261`, `:425`, `:444`, and *StPetersStoreCounterTests (StoreCounter)* `:39` (the counter is on the green's side: the green is due south of the store);
- *StPetersYardTests* `:174` (no two yards claim the same ground);
- *StPetersLayoutTests* `:316`, *StPetersStarterSplatTests* `:710`;
- *StPetersWoodlandZoneTests* `:187`, `:195`;
- *StPetersRoutineContentTests* `:235`, `:383`;
- *StPetersCamperLotTests*, *StPetersWoodsTests*, *StPetersGroundCoverBudgetTests*, *SceneWeightGuardTests* `:162`.

**PR 5's guard.** *StPetersTerrainPlanGuardTests (the derived maps)* says "the berths, wharf, buildings, roads, arrival route, cliffs and woods are unchanged":
- in order (a) it is written once, against the plan's pads, yards and lanes;
- in order (b) its "buildings" premise moves in the village's PR.

**The two orders, measured:**
- **(a) The village before PR 5.**
  - V1's Defs land first. PR 5's frozen mask then takes the plan's pads, yards and lanes together with today's, so V3 can swap the village without a second derivation.
  - PR 5's derivation reads the village's `RouteDef`s beside its own `PathDef`s (the pass-9 doc, `:689`), and paints both in one pass.
  - The cost: V1 (CI only, about 0.1 MB) must land before PR 5. Nothing is derived twice, and PR 5's guard is written once.
- **(b) The village after PR 5,** through a village step of its own in PR 4b's pattern.
  - The cost: a second derivation of the maps (+0.3 to +0.5 MB, PR 5's own estimate for its maps), and a premise change to *StPetersTerrainPlanGuardTests*.
  - The paths would be painted by a second tool.

In both orders, V3's scene edit goes through a village step in `StPetersLayerRefresh` (PR 4b's file), never through `Build()` (ADR 0019).

---

## 10. The art asks, and the paste for Claude Design (charter §3.10)

**Planned with today's kits.** Every picture renders from these rigs on node:
- the houses kit: the `Buildings.json` presets on `houseIsoRig.js`;
- the shop kit: `harbourStore` and `villagePost`;
- the yard kit: fences, gates and dressing;
- the wharf kit: the bench, the notice board, the flakes, the trap stack and `lanternPost`;
- the tree kit's red maple.

**What no kit has** (the jobs are the package's, README-FIRST §4 to §6):

| # | the art | used for | in the pictures today | relation to Claude Design's jobs |
|---|---|---|---|---|
| 1 | a side entry for the two W homes: `entry: 'left'` (the west wall, on the left as you look at the show face), with the show face to the south, listed at facing 4 as `'signpost'` | the red saltbox, the white farmhouse | the W facing, the door side-on | **Job 1** (camera-first shells: the `entry` option), with **Job 4**'s signposts where the walk turns the corner. The JSON's `jobOneAsk` gives each pivot and footprint: both fit their lots, and their roofs stay under Green Row. |
| 2 | three new presets, with rooms: `cream_saltbox`, `white_gable`, `harbour_gable` | the three new homes | rendered from the houses rig with the JSON's option sets | **Jobs 1 and 2** (camera-first shells and rooms). V3 bakes them from today's rig so the scene has them; V4 swaps in Claude Design's. |
| 3 | a doorstep lantern on a bracket | the nine doors | a mark on the plan map | **Job 1** lists a lantern among a side entry's pieces; **Job 4** dresses the way in |
| 4 | a hung-lantern light preset | `lanternPost` and the doorstep lanterns | `Lightpost` and `WindowGlow` pools | not Claude Design's: code, one enum case (municipal §6.2 #2), in V3 |
| 5 | gate leaves for post and rail, wire and stone | three yards | a 1.8 m gap | new; beside **Job 4**'s gate that lines up with the path |
| 6 | the path from each gate to its door | nine front paths | dirt | **Job 4** ("a path to the door") |
| 7 | a commons set: a gravel hearth ring with log benches, and a low potting bench | the square, the west garden | the yard kit's fire pit, a white bench and the tool lean-to | new; with **Job 4**'s landscaping |
| 8 | plans A and B, and the manor's site | the village | this plan is B | **Job 5:** plan B is this plan (the JSON); plan A is today's pads turned camera-first |

### 10.1 The follow-up paste (for the owner to send; this session never sends it)

Attach `plan-map.png` and `st-peters-village-plan.json` from `docs/design/st-peters-village-plan/`.

```text
Follow-up for the St Peters village. Please read it with the package you already have.

The game's village plan is now drawn. Attached: plan-map.png (the plan), and
st-peters-village-plan.json (your Job 5 schema, extended with lots, commons, lights, fences
and frames). Please make your work follow it.

1. Job 5: plan B is this plan (the JSON's option B). Plan A stays today's pads, turned
   camera-first. For the manor, the game measured three sites, and only one works: the east
   end of Green Row, in the white gable's place (lot.st_peters.white_gable, and 4 m east of
   it). Draw it there, or not at all.
2. Job 1: two homes enter from the west, down walks of their own: the red saltbox and the
   white farmhouse. Please give each a side entry, entry 'left' (the west wall, with the show
   face to the south), listed at facing 4 as 'signpost', with Job 4's signposts where its
   walk turns the corner. buildings[].jobOneAsk gives the pivot and the footprint each must
   fit.
3. Three new homes, all clapboard, from the houses kit: cream_saltbox, white_gable and
   harbour_gable (a home above the harbour shore, not a harbourmaster's). Their options are
   in buildings[].optsJs. Each needs a camera-first shell and room (Jobs 1 and 2). Every door
   in the plan is on its porch front, and seven of the nine face south.
4. Job 4: a gate leaf for post and rail, wire and stone (today only the picket has one); the
   path from each gate to its door; and a doorstep lantern on a bracket by each door.
5. The commons are built from the kits you have. If you draw a commons set, draw these
   first: a gravel hearth ring with log benches round the fire pit, and a low potting bench.
6. The island has no poles, no wires and no street lamps (the owner's ruling). Its lights are
   oil lanterns at the doors, and hurricane lanterns on posts that the villagers light at
   dusk.

Everything else in the package stands.
```

---

## 11. The plan is DATA (charter §3.11)

### 11.1 The Defs
One entity per file, each with a stable, append-only id (ADR 0003). The names are proposals; the fields are what the prototype uses. The JSON holds every value.

| Def (proposed) | Ids | Count | Holds |
|---|---|---:|---|
| `VillagePlanDef` | `village_plan.st_peters` | 1 | The lines (Green Row, the axis, the walks); the tunables (the fence offset 0.4, the walk gap 0.5, the yard's hold 0.6, the class widths); the lists of the Defs below. |
| `LotDef` | `lot.st_peters.<building>`: `…school`, `…general_store`, `…post_office`, `…cream_saltbox`, `…white_gable`, `…red_saltbox`, `…white_farmhouse`, `…sage_cottage`, `…harbour_gable` | 9 | The building's id and bake key; its street (a route id); its place along it; its facing cell (data, no longer derived); its setback; its outline; its gate; its approach. The building's position, door and footprint are derived from these and the bake's own sizes. |
| `YardDef` | `yard.st_peters.<building>` | 9 | Its lot; the fence (`YardFence`); the gate; the lawn's care; the dressing (kit piece and place). It replaces the village's rows in `StPetersYards.cs:120-156`; Ginny's and the camper's rows stay in code. |
| `RouteDef` | `route.stpeters.<name>` (T3) | 14 | Class, width and points, with municipal §2.4's ids. `school_lane` and `plot_path` keep their shipped ids. Pass 9's derivation paints them as it paints its `PathDef`s (order (a)). If lead-architect prefers one type, they can be `PathDef`s that carry the route id. |
| `CommonsDef` | `commons.st_peters.square`, `…west_garden`, `…flake_yard` | 3 | The outline; the furniture (kit piece, place, facing); the stations it offers. |
| `LightPostDef` | `light.st_peters.<where>`: 9 doorsteps, 5 posts, the fire pit, the shed light | 16 | The kit piece; its place; its preset; who lights it and when (a routine block's station, or "the household"). |

Stations and lane nodes stay in `StPetersRoutines`' tables (`:877-937`), as today. V2 adds the new rows, and each reads its position from the Defs.

### 11.2 The editor step, without `Build()`
- **A village step in PR 4b's `StPetersLayerRefresh`** (the pass-9 doc's §10, and its PR 4b at `:925`). It writes YAML patches to the village's roots, each gated by named deletions:
  - `IslandVillage`, `IslandShops`, `Yards`, `IslandInhabitants`, `IslandRoutines`, `GeneralStoreCounter` and the store's machine rows;
  - the new commons' and lights' roots.
- **The builder's calls are updated to agree.** `StPetersBuilder`'s positions (`:747-751`), `StPetersShops.cs:98` and `StPetersYards.cs:120-156` read the Defs, so a builder run (never made on St Peters) would give the same village as the patch.
- **Its test** goes in `StPetersLayerRefreshTests` (PR 4b's), in that file's pattern.
- **If PR 4b has not merged** by V3, V3 carries hand-written YAML with named deletions instead (the charter's second way).

### 11.3 The plan's JSON
`docs/design/st-peters-village-plan/st-peters-village-plan.json` is in the package's Job 5 schema (README-FIRST §6: `option`, `buildings`, `lanes`, `yards`, `furniture`), extended with:
- `lots`, `commons`, `lights` (with `lamps`, the pools as counted), `fences` and `frames` (every window, and the wide views);
- `stations`, `tree`, `cycleLinks`, `roadside`, `lines`, `derived`, `tunables` and `checks`;
- per building, `jobOneAsk` for the two W homes.

It is `option: "B"`: a new layout. Option A, today's pads turned camera-first, is Claude Design's to draw.

---

## 12. The PR plan (charter §3.12)

**Every PR:** the MB figures are estimates. Tests are named with their production subject, as *TestClass (subject)*.

### Summary
| PR | Lane | Slot | Adds (estimate) | After |
|---|---|---|---|---|
| **V1: data and guards** | world-content, with tools-editor | CI only | about 0.1 MB: 52 small Def assets | this plan's ruling; before terrain PR 5 (order (a)) |
| **V2: the people** | world-content | CI only | about 0.05 MB | V1 |
| **V3: the scene** | world-content, with art-pipeline | one editor slot: the bake of the three new homes, and the plate | +1.8 to +2.2 MB of sheets (three homes with rooms, as today's `Village_*` sheets at 0.30 to 0.42 MB and a room at about 0.31 MB); +0.3 to +0.5 MB of scene YAML | V1, V2, PR 4b, and terrain PR 5 (both patch `StPeters.unity`; one at a time) |
| **V4: the art** | art-pipeline | one slot: the bake | +1 to +3 MB, set by Claude Design's return; V3's three homes are replaced, not added | V3, the seat's check of the return, and the owner's ruling |

### V1: data and guards
**Files:**
- `Code/World/Village/`: `VillagePlanDef`, `LotDef`, `YardDef`, `RouteDef`, `CommonsDef` and `LightPostDef`;
- `Data/Regions/StPetersVillage/`: the assets (one entity per file);
- a plain C# `VillagePlanDerivation`, engine-light: positions, doors, footprints and paths from the lines and the bakes' sizes.

**Tests added:**
- *StPetersVillagePlanTests (VillagePlanDerivation, the Defs)*: the charter's rules as checks on the Defs:
  - the tree;
  - fences never cross a lane;
  - each yard holds its footprint + 0.6, and no two yards touch;
  - 4 m between footprints (ground metres);
  - the hearth 8 m, and the bar sightline r + 40;
  - 3 m from every trunk, and on the plateau;
  - the five fixed points untouched, and the ids unique.
- *StPetersVillageWindowsTests (VillagePlanDerivation, the window tiling)*: §7's windows as checks:
  - every window a route crosses has a focal;
  - no window shows a building's back;
  - an edge-on door is marked;
  - pools ≤ `MaxPools` and pairs ≤ `MaxShadows` per window.
- *StPetersVillagePlanDeterminismTests (VillagePlanDerivation)*: two derivations are identical.

**Tests changed:** *ContentValidationTests (the Def registry)* covers the new Def types' ids.

**Tests retired:** none.

### V2: the people
**Files:**
- `Data/NPCs/` and `Data/Routines/`: the NpcDefs and RoutineDefs of §8;
- `StPetersRoutines.cs`: the new stations and lane nodes (T5, T7, §8) and `route.stpeters.cannery_walk`.

**Tests added:** *StPetersVillageRoutinesTests (StPetersRoutines, RoutineDef)*:
- each new home has its sleepers;
- each commons station is used by a routine that names it;
- each lantern post is lit by a routine block at dusk.

**Tests changed** (each needs the owner's word):
- *StPetersInhabitantsTests (StPetersInhabitants)* `:69` (the count) and `:296` (nine buildings);
- *StPetersRoutineContentTests (StPetersRoutines)* `:142` and `:190`;
- *NpcContentValidationTests* re-runs on the new Defs.

**Tests retired:** none.

### V3: the scene
**Files:**
- `Scenes/StPeters.unity`: through the village step in `StPetersLayerRefresh`, gated by named deletions (§11.2);
- the builder's calls updated to agree: `StPetersBuilder.cs:747-751`, `StPetersShops.cs:98`, `StPetersYards.cs:120-156`, and `StPetersMachines` (the row into the forecourt);
- `LampPosts.PresetFor` and `LightPresets`: the hung lantern;
- the three new presets in `Buildings.json`, baked by `BuildingRigBaker` and `InteriorRigBaker` from today's rig;
- the scene exporter re-run.

**Tests added:**
- *StPetersVillageSceneTests (the committed scene, VillagePlanDef)*: the buildings, yards, fences, lights and commons stand where the Defs say, in the pattern of *StPetersMachinesTests* `:270`;
- *StPetersLayerRefreshTests* gains the village step.

**Tests changed** (their premise moves; each is named in §9 and needs the owner's word):
- *StPetersVillageTests* `:636`, `:690`, `:773`, `:856`;
- *StPetersShopsTests* `:55`, `:77`, `:140`;
- *StPetersYardTests* `:83`, `:130`, `:215`, `:282`;
- *StPetersLawnTests* `:117`, `:139`; *StPetersDecorTests* `:217`, `:267`;
- *StPetersMachinesTests* `:61`;
- *LampPostsTests* `:66`.

**Re-run, with their premise held:** §9's list, plus St Peters' PlayMode tests.

**Tests retired:** none.

**Measured before and after:** the scene (at main 6,179,999 B, 7,555 objects and 1,508 GameObjects, against *SceneWeightGuardTests*' ceilings of 32 MiB, 50,000 and 12,000, `SceneWeightGuardTests.cs:45`, `:56`, `:65`), and the island's casters (438 to 440).

### V4: the art
**Files:** Claude Design's return, after the seat's check and the owner's ruling:
- the three new homes' shells and rooms;
- the two side entries;
- the gate leaves, the path to the door and the doorstep lantern (Job 4);
- any commons set.

**Tests changed:** *YardDressingTests (YardDressing)* `:109`, if gate leaves join the fence styles.

**Tests added:** the return's own guards, as the seat's check names them.

**Tests retired:** none.

---

## 13. Risks
1. **The facing becomes data.** The tests that derive a facing, or turn a door toward the green, move their premise (§9), and each needs the owner's word. V1 carries the facing in `LotDef`, and its guards check the windows instead.
2. **The machines' row.** `StPetersMachines` stands the row at the store's gate plus 2 m forward (`StPetersMachines.cs:90`, `:105-118`). With the store facing S, forward is Green Row, at the Green Walk's head. V3 turns the row into the forecourt, and *StPetersMachinesTests* `:61` changes.
3. **The two W doors stay side-on until Job 1's art comes.** Each is marked by a lantern and a gate, and windows E2 and F2 hold them. If no side entry comes, they stay as they are.
4. **Q6 and Q8.** The white gable's children need Q6; the cannery hands' homes answer Q8. If the owner rules otherwise, who lives in the homes changes, not the layout.
5. **The canon's count.** The canon says three houses (`docs/vision-and-pillars.md:76`, `:152`; `docs/design/world-and-regions.md:283`, `:288`). This PR changes each to six homes, as the owner's words. If the owner rules another count, it changes before the merge.
6. **The door frames** (§7.1, decision 10).
7. **PR 4b is not in main yet.** The village step needs `StPetersLayerRefresh`. If PR 4b slips, V3 waits, or carries hand-written YAML with named deletions.
8. **Past the woods' clearing.** The plan's footprints reach 62.5 m from the hearth, past `VillageClearingRadius` 44 (`StPetersWoods.cs:67`). No trunk is within 3 m, and outside the clearing trees grow only in the woods' stands (the seat's correction to the package). But a future woods edit must keep the village's 3 m, and *StPetersVillageTests* `:773` changes.
9. **The scene grows.** The plan places 170 fence pieces (143 panels, 24 posts and 3 gates), 45 dressing pieces, 46 commons pieces, lantern posts and roadside pieces, and three buildings with rooms. Today `Yards` holds 153 placed objects, and the scene is far under its ceilings (V3's measure).
10. **The lantern preset is code (V3).** Until it lands, a lantern post glows as a steady road lamp (`Lightpost`).
11. **#853 stays open**, conflicting and red. The plan does not depend on it (decision 7).
12. **Pass 9's barren and shore paths.** The Barren Gap and the Shore Walk meet them (T8). Pass 9's paths keep their room: footprints and yards stay at least half the path's width plus 0.5 from them (the check "pass 9's footpaths keep their room").

---

## 14. Decisions for the owner
1. **The plan,** judged on its pictures and windows.
   - *Recommendation:* take it.
2. **The grid and the villagers' lanes:** a tree over the grid, with no code change; or a lane graph (an ADR, for lead-architect).
   - *Recommendation:* a tree. T8's five links stay the player's. The only cost is that some evening walks go round by Green Row. A lane graph stays deferred to the first city (municipal §2.3).
3. **The lights:** within the island's power ruling, or a change to it.
   - *Recommendation:* within the ruling: nine doorstep lanterns, five lantern posts lit by named villagers, the fire pit and the shed light, with no lamp line. Confirm that the posts' reading of "dooryard" (§6) is yours.
4. **The six homes:** which three are new, who lives in them, and the manor.
   - *Recommendation:*
     - the cream saltbox and the harbour gable for the cannery's four hands, which answers Q8 with "on the island";
     - the white gable for a family, with its children if you rule yes on Q6;
     - the manor not now. If later, M2, as one of the six.
5. **Each moved pad, and any fixed point touched.** Six pads move (T2: 2.5 to 69.2), and no fixed point is touched.
   - *Recommendation:* approve the six moves.
6. **The order with terrain pass 9's PR 5.**
   - *Recommendation:* (a), the village's data before PR 5.
7. **#853:** fold it into the build, or close it; and its R2 and R3.
   - *Recommendation:* close it. Its kit reached Claude Design in the package (`1-houses/`), and V4 takes the return. R1, the manor's site, is §3.1's proposals. R2, the honest door anchor, is not needed by this plan, since every door is on its porch front. R3, the floor option, is V4's to decide with the rooms.
8. **Front walks** in shell at the two public doors, or none.
   - *Recommendation:* none now. The store's and the post office's front paths are 1.5 m footpaths, ready to become shell walks if you rule for Nine Mile Creek's (`municipal-infrastructure.md:316-319`).
9. **The Claude Design follow-up paste** (§10.1), for you to send.
   - *Recommendation:* send it, with `plan-map.png` and the JSON attached.
10. **New: the door frames** (§7.1).
    - *Recommendation:* accept, and look again on V3's plate. If the plate shows the cut, add a small upward lean to the on-foot camera in villages (gameplay-systems; a `GameConfig` tunable). The layout does not change for it.
11. **New: the canon's count.** Both canon docs say three houses. This PR changes both to six homes (the PR body marks it as your change).
    - *Recommendation:* approve.

---

## Appendix A. The prototype's checks

The prototype (Python and node scripts, kept outside the repo in the session's `Evidence~`) derives the plan from its lines and checks it. Each check family and its cases:

#### T12. The prototype's checks

| check | cases | failing |
|---|---:|---:|
| sightline | 9 | 0 |
| hearth 8 m | 9 | 0 |
| lane gap 4 m (proposed, footprints in ground metres) | 36 | 0 |
| lane gap 4 m between diagonal circles (today's premise, report) | 1 | 0 |
| woods clearing (report) | 1 | 0 |
| trunk 3 m | 31 | 0 |
| road band 3.75 | 18 | 0 |
| plateau | 18 | 0 |
| frozen paint | 31 | 0 |
| Ginny's plot | 18 | 0 |
| barren | 18 | 0 |
| yards apart | 36 | 0 |
| fences never cross a lane | 1 | 0 |
| villagers' lanes form a tree | 1 | 0 |
| every villager lane runs on a drawn path | 1 | 0 |
| most homes face S (report) | 1 | 0 |
| setbacks and lots (report) | 1 | 0 |
| art clear of the front | 9 | 0 |
| art clear of the side fences | 7 | 0 |
| yard holds its footprint + 0.6 (proposed) | 9 | 0 |
| yard holds a 6.40 m circle (today's premise, report) | 1 | 0 |
| dressing | 2 | 0 |
| commons, lanterns and roadside | 1 | 0 |
| fences tile without offcuts | 1 | 0 |
| roadside, commons and lights | 1 | 0 |
| nobody walks behind a roof (lanes, front paths, pass 9's barren and shore paths) | 1 | 0 |
| no building's art over another's door | 1 | 0 |
| pass 9's footpaths keep their room | 1 | 0 |
| edge-on door marked | 2 | 0 |
| art seen | 9 | 0 |
| ids unique | 5 | 0 |

281 checks, 0 failing.

The least margins, each with the case that holds it:

| rule | least | where |
|---|---:|---|
| the bar sightline: the margin over r + 40 (`StPetersBuilder.cs:736-737`) | 2.61 m | `sage_cottage` |
| the hearth, 8 m clear (`HearthClearanceRadius`) | 15.16 m | `general_store` |
| two footprints, 4 m apart in ground metres (`LaneGap`) | 4.06 m | `school` and `sage_cottage` |
| a trunk, 3 m clear (pass 9 froze 3 m round each) | 3.38 m | `white_farmhouse`'s yard |
| the roads' band, 3.75 m | 3.80 m | `red_saltbox`'s yard |
| the ground under a footprint, at least 5.9 m | 5.91 m | `sage_cottage`'s footprint |
| the ground under a yard, at least 5.5 m | 5.62 m | `sage_cottage`'s yard |
| two yards apart | 1.00 | `general_store` and `post_office` |
| a yard's hold on its footprint, at least 0.6 m | 0.70 m | `sage_cottage` |
| the art clear of its yard's front | 0.50 m | `harbour_gable` |
| the art clear of its yard's side fences | 0.34 m | `sage_cottage` |
| a sprite under a building drawn in front of it, at most 20% (the most) | 0% | `school` |

## Appendix B. Pictures and the JSON

All are under `docs/design/st-peters-village-plan/`. The PNGs are in LFS.

| file | pixels | bytes | shows |
|---|---|---:|---|
| `plan-map.png` | 1872 × 792 | 64,785 | the plan: the buildings and their facings, lots and yards, fences, paths, the commons, the lights |
| `village-today.png` | 920 × 620 | 68,136 | today's village, read from the scene: each door's facing, and pass 9's frozen items by name |
| `windows.png` | 1888 × 994 | 647,417 | the 36 on-foot windows (16 × 9 world units), rendered on node from the rigs |
| `gates.png` | 1472 × 1094 | 266,074 | the nine gate frames at the default on-foot rung (15 × 8.44) |
| `wide-arrival.png` | 796 × 512 | 100,391 | the wide view at 14 m: up from the slip |
| `wide-square.png` | 796 × 512 | 89,010 | the wide view at 14 m: the square |
| `wide-bluff.png` | 796 × 512 | 68,222 | the wide view at 14 m: the bluff |
| `wide-east.png` | 796 × 512 | 69,760 | the wide view at 14 m: Green Row east |
| `st-peters-village-plan.json` | — | 89,051 | the plan as data (§11.3); not in LFS |

The 8 PNGs come to 1,373,795 B.
