# Washrooms plan: heads, bathrooms, outhouses

Owner ask, 2026-11-15: showers and baths as stations in boats and buildings; sinks for a basic wash
that cannot fully clean; dirty players noticed by villagers; the missing bathrooms added to buildings,
with a toilet you can sit on that does nothing; outhouses with doors that open where there are none;
villagers use the washrooms; washrooms on boats that would typically have them.

Slice 1 (`feat/cleanliness-and-wash-stations`) shipped the logic and the data only. This file is for
the placement lanes that follow: what goes where, and the exact join for each piece.

## 1. What slice 1 shipped

| Piece | Where | Notes |
|---|---|---|
| Dirt as the third need | `Core/Cleanliness/*`, `NeedKind.Dirtiness` | 0 clean .. 1 filthy; Mild = grubby, Strong = stinky |
| Wash stations, one per kind | `Data/Resources/WashStations/` | `washstation.sea`, `.sink`, `.shower`, `.bath`, `.toilet` |
| The outhouse type | `Data/Resources/Washrooms/Outhouse.asset` | one toilet, a door that opens, `Prefab` empty |
| The stinky reaction lines | `Data/Resources/NeedReactions/Stinky.asset` | `ReplyTone.PutOff` |
| The reply seam | `Core/Needs/NeedsNotice.cs` | `NeedsNotice.ToneNow()`: Warm, Cranky or PutOff |
| The routine tag | `World/Routines/RoutineActivity.Washroom` (5) | no station targets it yet |

Default numbers (all tunable: `GameConfig.Cleanliness` and the station assets; one game hour is 75
real seconds at the shipped 1800 s day):

- Idle, just living: grubby after 24 game hours, stinky after 36.
- Hard work (pot hauling): stinky from clean in about 7.4 game hours. Engine work: about 3.4. Gym: 4.4.
- One fish handled adds 0.015 dirt; one pot rebaited 0.01.
- Shower: from stinky (0.75) to fully clean in 15 game minutes. Bath: 22.5 minutes, also fully clean.
- Sink: 1.0 dirt per game hour, stops at 80 % clean (dirt 0.2): from stinky in about 33 game minutes.
- Sea (swimming): pulls toward 55 % clean (dirt 0.45, just under grubby) from either side at 1.2 per
  hour: rinses a stinky player in 15 game minutes, and salts a freshly showered one down in 22.

## 2. Boats: which hulls get a head, a sink, a shower

Rule of thumb by size and type. Open boats and small skiffs get nothing; a cabin boat gets a sink; a
hull you can live aboard gets a head (toilet) and, when it is big enough, a shower.

| Class | Hulls | Head (toilet) | Sink | Shower |
|---|---|---|---|---|
| Open, no cabin | Dory, DoryOutboard, Punt, PuntUpgraded, JetSki, ZodiacFrc, ZodiacHurricane, YachtTender, ConsoleSkiff, SportSkiff, SportSkiffMk2, SportSkiffTwin, Bowrider | - | - | - |
| Open lobster boats | Lobster*Open* (9) | - | - | - |
| Hardtop lobster boats | Lobster*Hardtop* (9), LobsterBoat, CapeIslander | - | sink in the wheelhouse | - |
| Small cabin | GoFast, Sloop30 | marine head | sink | - |
| Cruisers and sport fishers | ExpressCruiser, SportFisherConvertible, SportFisherSkybridge, Sloop88 | head | sink | shower |
| Working vessels with crew | SideDragger, SternTrawler, SternTrawlerMk2, CoastalPacket | head | sink | shower |
| Yachts and the tanker | MotorYacht84, ExpeditionYacht120, Tanker | head (more than one) | sink | shower (and a bath on the 120) |

**Join for the boat lane.** Each fixture is an anchor in the hull's interior sidecar carrying the verb
`wash` (`WashStationDef.ActionWash`) and a station id, or the verb `sit`
(`WashStationDef.ActionSit`, spelt exactly as the galley lane's `BoatInteriorDef.ActionSit`) with the
id `washstation.toilet`. Slice 1 deliberately did NOT touch the interior sidecars or
`Data/Boats/Interiors/*.asset`: the galley lane (box `galley-g1`) is editing every one of them and adds
the `sit` anchor and its furniture fields. After it merges, the head anchors go in as one more pass
over the same files. Pressing a `wash` anchor calls `GameServices.Cleanliness.BeginWash(station)`;
walking off or pressing again calls `EndWash()`.

## 3. Buildings: bathrooms

| Building | Region | Needs |
|---|---|---|
| Player homes: CumberlandRouteBungalow, CumberlandTwoStorey, NmcShoreLaneSaltbox, NmcSouthShoreEstate, NmcWaterfrontHouse, RicePointPointHouse, StPetersRedSaltbox | various | bathroom: toilet, sink, shower or bath (the estate and the two-storey get both) |
| GinnyLotCamper | St Peters | camper wet room: toilet, sink, small shower |
| Aunt Ginny's cottage and villagers' homes | St Peters | bathroom (used by their owners' routines; no player access needed) |
| The store, the post office, the bar | St Peters | a back-room toilet and sink (staff and customers) |
| The cannery / fish plant | St Peters, NMC | staff washroom: toilets, sinks, a shower (dirty work) |
| The wharf buildings, the fuel stop | NMC | toilet and sink; a wharf shower is the natural place to rinse after a day on the water |
| Main Street shops, the plaza | NMC | toilet and sink in each shop's back room |
| The campground | NMC | a shower block (toilets, sinks, showers) |

**Placed so far (the first two, `feat/wash-stations-placed`).** A placed station is a prefab carrying
`World/Washrooms/WashStationPoint` (an `IInteractable`: press to wash, press again or walk off to stop,
it stops itself at the Def's floor `1 - Ceiling`). The rows live in
`App/Editor/WashStationPlacement.cs`, which builds the prefabs and stands them by a named text patch
(step `wash-stations-01`; re-plans empty), never by opening and re-saving the scene.

| Station | Where | Def | Prefab |
|---|---|---|---|
| Washstand | St Peters, Ginny's cottage, the player's room upstairs (model 0.45, 3.55 on level 1) | `washstation.sink` | `Prefabs/Washrooms/WashStand.prefab` |
| Wharf shower | Nine Mile Creek, the wharf's land side (90.2, 108.2), under the new root `NmcWashStations` | `washstation.shower` | `Prefabs/Washrooms/WharfShower.prefab` |

Both wear placeholder art (the catch kit's empty pail; the shower's post and head are tinted greybox
`Square.png`) until basin and shower art exists. `StPetersGinnyPlot` re-stands the washstand on a
rebuild. Add a station by adding a `Sites` row and re-running the step.

## 4. Outdoor areas: outhouses

An outhouse is `washroom.outhouse`: one toilet seat behind a door that opens (the seamless-interiors
door, press moves the leaf). Candidate spots where there is no building with a bathroom nearby:

- St Peters: the landing / main beach, the east end, Ginny's plot (until the camper has its wet
  room), the North-East light, Whelps lookout.
- NMC: the campground (beside the shower block), the falls, the north creek, the weather face.
- The farm plots (one per plot cluster).

**Join for the world lane.** Author the outhouse prefab (a small shed, a door, one toilet anchor
with verb `sit`) and set it on `Outhouse.asset`'s `Prefab`; place instances in the region scenes.

## 5. Villagers using the washrooms

Routines are a pure function of the clock (`RoutinePlan.SampleAt`). `RoutineActivity.Washroom` (5) is
the tag for a block spent in a washroom. To use it: add a washroom station to the region's station
table (a bathroom behind a home's door, a shop's back room, an outhouse) and one row on one routine
(`StartHour`, that station, `Washroom`). The villager walks there and is hidden past the door like any
interior. No St Peters station is a washroom yet, so slice 1 added no row. Draft PR 1231 changes
`RoutineDef.cs` (per-stop regions); slice 1 did not touch that file.

## 6. Villagers noticing

`NeedsNotice.ToneNow()` returns `PutOff` when the player is stinky, else `Cranky` when hungry or worn
out, else `Warm`. The dialogue lane's join (World/`DialogueModel`): when the tone is `PutOff`, play a
line from the `NeedReactionDef` whose tone matches (a seeded pick, deterministic) before the villager's
own lines; when it is not `Warm`, hide the friendly or bonus options. Becoming stinky is the needs'
own `NeedStageChanged(Dirtiness, ..., Strong)` signal: hang a bark, a step back, flies or stink lines
on it. Washing is `PlayerWashChanged` (on and off).

**Stink lines (slice 2, built):** `Player/StinkLines.cs` (`StinkLinesPresenter`, added to the player by
`PlayerWalkController`) draws a few small wavy lines rising from his head once dirt reaches
`GameConfig.StinkLines.StartAtDirt` (shipped 0.5, grubby): 1 line there, 2 at stinky (0.75), 3 from
about 0.83. They fade as he washes, ride the body's Y-sorted band (a wall in front hides them with
him), follow the deck rider aboard, and hide at a helm, in a cab and below decks. Derived from the
saved dirt, never saved. The line is a placeholder squiggle drawn in code; a baked sprite can replace
it.

Art and audio this still needs (none made yet): a baked stink-line sprite or a few flies around a
stinky character (a small looping particle or sprite overlay, pooled); a damp, darker hair and skin tint for a short while
after washing or swimming; a wash pose (hands at a basin) and a shower stance; the toilet sit pose
(the galley's seat pose should serve); water-running loops for sink, shower and bath; a door creak and
latch for the outhouse; a villager "phew" bark.

## 7. What has no signal yet

Clam digging, farm work, engine work and the gym have no signal on main. They arrive through the body
lane's activity entry point (box `body-mass`, slice 2: `ActivityDef` and its work-signal reporting):
its report of a stretch of work should call `GameServices.Cleanliness.BeginActivity(kind)` /
`EndActivity(kind)`, mapping its activity to a `DirtActivity`. Bait handling on hooks (not pots) has
no signal either. `NeedsWater` / `NeedsPower` on a station are read by nothing until a water or power
supply exists. The responder cannot tell the sea from fresh water: if the walk raises its wading
signal in a pond (not checked in slice 1), swimming there rinses like the sea for now.
