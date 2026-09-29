# hh-village-return · phase 5 · outbuildings, water and the work yard

2026-09-26. The phase after the dooryards covers the buildings and water round each house. The rig is `yard-landscaping-kit/outbuildingIsoRig.js`. The lot planner is the phase 5 part of `yard-landscaping-kit/yardLots.js`. The boards are in `boards-outbuildings/`, and the review is the top section of `Village Houses Phase 2.dc.html`. The owner pointed at the new road kit (`export/road-path-kit-v4/`), so every yard path is now one of its routes, and the boards stand on its floor.

## Calls made here

The owner did not answer these three questions. Each one is a single table entry in `YardLots.OUT5`.

- **How many outbuildings.** One per house. The farmhouse has two (a barn and a woodshed), the general store has a store shed, and the post office has none.
- **Wells.** Each household has its own water. The three bigger lots (sage cottage, white gable, farmhouse) have a dug well. The four smaller ones (school, harbour gable, cream saltbox, red saltbox) have a yard pump. A shared well on the green would be a plan B change.
- **Fences.** They still close round every lot, as the plan names them. They are low and open, and every door was measured with the fence in place. The farmhouse fence opens for the barn lane.

## Outbuilding (`yard-landscaping-kit/outbuildingIsoRig.js`)

Six kinds: `woodshed`, `shed`, `barn`, `henHouse`, `well` and `pump`. They use the same camera, light engine (`CoastalPass.light`), tags and emitters as HouseIso. There is no dither and no keyline. Canvas 640 × 620, pivot 320, 450.

- **woodshed:** an open-fronted lean-to with two bays of cordwood between three posts (one bay is half burned). A tall chopping block with an axe stands in front, with chips round it.
- **shed:** a gable tool shed with a ledge-and-brace door in the gable and a four-light sash on the side. Inside: a tool rack on the back wall (spade, fork, rake, hoe), a potting bench with pots and a shelf of cans, and sacks and a watering can.
- **barn:** a small gambrel barn with sliding doors on a track, a loft door under a hay hood with a hay beam and pulley, a cupola and a vane. A stone ramp leads up to the sill, and a lamp hangs beside the doors. Inside: a stall with a manger, a feed bin, harness pegs, baled hay and a ladder to the loft.
- **henHouse:** a raised coop with a people door, a pop hole with a cleated ramp, a nest box with a lid on the front, roosts, a feeder and a waterer.
- **well:** a fieldstone curb and coping, a windlass with a crank under a gabled hood, and a pail on the coping.
- **pump:** a cast-iron pump on a plank cover, a trough under the spout and a pail.

**Options:** `body` (the house colours, plus `barnRed`, `raw` and `whitewash`), `siding` (`clapboard`, `shingle`, `board` or `boards`), `roof` (the six house roofs), `trim`, `doorPaint`, `size`, `mirror`, `doorOpen` (hinged leaves swing out, the barn's slide along the track), `loftOpen`, `cutaway: 'section'`, `cutH`, `weather`, `kept` and `lamp`.

**Camera-first rules, as for the houses:**
- The show face is +Y and carries the door. `placement()` lists facings 3, 5 and 4 (door SW, SE or S, `via: 'door'`), then 2 and 6 (door W or E, `side`).
- Door share visible at the listed door facings, measured on the kind's own frame against the door drawn alone:

  | kind | door share visible |
  |---|---|
  | barn | 100 % |
  | shed | ≥ 98 % |
  | hen house | ≥ 92 % |
  | woodshed (cordwood face) | 99 % square-on, 75 % at the diagonals |
  | well (pail) | 100 % |
  | pump (trough) | 83 % |

- **Stations.** Every fixture height is inside the v9.2 creator fit, and every stand point is reachable from the approach on a 0.1 m grid round the walls.

  | kind | stations |
  |---|---|
  | barn | doors (walk, 2.8 m), feed bin (lift 0.84), harness (reach 1.0), stall rail (rail 0.66), ladder (rung 0.25) |
  | shed | door (walk, 0.85 m), tool rack (reach 0.95), potting bench (bench 0.66 above the floor, and a place surface) |
  | woodshed | cordwood (lift 0.80), chopping block (chop 0.62) |
  | hen house | door (walk, 0.78 m), nest box (reach 0.96), feeder (reach 0.42) |
  | well | pail (lift 0.78), crank (reach 1.0) |
  | pump | trough (lift 0.74), handle (reach 0.98) |

  `pitchHay` is listed in `requests`, because v9.2 has no fork clip.
- **Section.** Walls that face the camera drop to the sill (`cutH` above the floor). The others stand to the eave. The roof, loft and hay come off, and every cut gets a pale cap. The door leaf goes with a dropped wall, and a threshold is left.
- **Night.** The barn lamp burns from dusk (`lamp: 'dusk' | 'off' | 0..1`). `lights()` gives its level, model point, screen anchor and ground pool.

## Lots (`yardLots.js`, phase 5)

- **`plan(name)` places the outbuildings and water**, largest first, in the side yard the camera sees.
  - The lot grows on the work side (`OUT5.work`, never the street side) and a little to the back.
  - Every piece stands with its door to the south.
  - A piece is scored down if it stands behind the house or across its show face, or in another piece's doorway view. It is scored up for standing a little behind the house's midline, on the work side, and near the walk.
  - The street edge and the gate stay where phase 4 put them.
- **Dressing.**
  - Flowers go to the front.
  - Crops go to the sunniest spot the camera sees. The sun test uses three suns (09:00, 12:00, 15:00), with the house and the outbuildings as boxes. Every crop placed is in full sun.
  - Work pieces go to the seen sides, not behind the house (phase 4 put them there).
- **Routes are RoadKit4 routes.**
  - The front walk is `walk` / `stones`, or `walk` / `gravel` for the school and the shops.
  - A `footpath` of trodden earth runs from the walk to each outbuilding and to the water.
  - A `lane` with ruts runs from the lot edge to the barn ramp.
  - Each route carries lot-frame points, world metres from the building pivot, scene px (32 px = 1 m, y down) and the surface's foot sound and grip. `YardLots.routes(name)` gives them alone.
- **`compose(name, dir, o)`.**
  - It z-composites every sprite by its own depth buffer, so the painter's order no longer decides.
  - It stands them on the pass-9 floor with the routes painted in by RoadKit4 and lit by TerrainLight5 under the same sky, when the terrain kit, WeatherSky and RoadKit4 are loaded. Otherwise it uses flat grass with the routes drawn flat.
  - `visibility` gives the share of each door's pixels that survive the compositing. `o.night` is 21:00.
- **Checks.** All nine lots pass.
  - From the gate, the house door and every outbuilding approach can be reached, and so can every dressing piece.
  - Every station reaches and fits.
  - On the boards, every outbuilding door, open bay, pail and trough shows 100 % of what its own frame shows.
  - The red saltbox's and the farmhouse's doors are on side walls, edge-on at facing 4, so they read by their portico and lamp post (the signpost rule).
  - `yard-landscaping-kit/lots-phase5.json` has every lot's pieces, routes and checks.
- `plan(name, {outbuildings:false})` and `compose(name, dir, {outbuildings:false})` give phase 4 exactly.

## Road kit v4: what was used

- **Route classes.** St Peters' own classes: `walk` (stones, gravel), `footpath` (trodden earth) and `lane` (ruts and a grass crown). There is no kerb and no paving.
- **Surfaces.** `SURF` foot sounds and grip go into each route.
- **Floor.** The full floor pipeline builds the boards: `network`, `zones`, `PxKit8.floor`, `paint`, `gbuf`, `patchG`, `faces`, `relight`, `weather`, `relightFaces` and `composite`.
- **Projection.** The v4 floor is a plan (32 px = 1 m both ways). The boards sample it through the house camera, which squashes the ground by sin 40°, so the walks meet the doors. The game should paint the routes' world points into its own pass-9 chunk.
- **Timing.** A lot board takes 3 to 6 s in this workspace (the farmhouse at night takes 10 s), about half of it the floor.

## To do on node

- Add the outbuilding rig and `lots-phase5.json` to the yards kit's manifest, and write a sidecar (`outbuildingIsoRig.params.json`). Run `restamp-kit`.
- Port the station and lot checks to a `checks/camera-outbuildings.cjs`. They ran here through the workspace's own evaluator, not on node.

## Open

- A shadow falls only on the ground: the light engine shades within one model, so the house's shadow does not fall on the shed.
- The hen house has no hens. Animals are a separate rig.
- Match the lot rectangles to the plan's own when it is frozen. They are still margins round each building, now grown for the outbuildings.
