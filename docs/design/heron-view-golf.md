# Heron View Golf Club — step 1, Phase A

Phase A2 is complete at base `f9276ec4b118957952046769961a77faa4564eb3` on
`feat/heron-view-golf-step1`. All work was headless. This is a place for recreation;
there is no ledger, booking, payment, membership, ball flight or swing simulation.

## Catalog boundary, resolved in Phase A2

The Phase A stop was accepted. Charter amendment 1 authorizes the catalog boundary
in one commit on this branch, followed by the golf builder's region in its own commit.
The original conflict was:

- `StPetersRoutines.LoadRoutines` searches all of `Assets/_Project/Data/Routines`
  with `AssetDatabase.FindAssets`, including subfolders. It has no region filter.
- `StPetersRoutineContentTests.EveryStationNamedByARoutineIsOneThisIslandDeclares`
  iterates that entire result and requires every station to be in the village's
  declared set. The golf station ids cannot pass it.
- The charter puts the new routine assets in `Data/Routines` and protects
  `StPetersRoutines.cs` from edits. A region filter would change a protected file;
  putting golf assets elsewhere would change the prescribed content layout.
  A subfolder does not isolate the assets from the recursive search.

`RoutineDef.RegionId` now sits immediately after `Id`, defaults to empty, and says
which region owns the day. `RoutineCatalog.LoadRegion(string regionId)` reads the
shared folder, selects that region by ordinal equality and orders the result by id.
An empty request returns no routines; an empty or unknown asset region fails the
new catalog guard. No region is assigned by default.

The village's existing `LoadRoutines()` signature now delegates on
`StPetersLayerRefresh.StPetersRegionId`. Its builder writes that region on created
or rewritten days and repairs it on kept days without retiming them. Its roster
and `StPetersRoutineContentTests.cs` remain byte-identical to the Phase A stop.
The six village assets each gain exactly one line immediately after `Id`:
`RegionId: region.st_peters`. The field order matches the source declaration.

The golf builder writes `region.cumberland` on every created, rewritten or kept
golf day, repairing only that bookkeeping field on a kept asset. Cumberland reads
through the same loader. Every routine stays in `Data/Routines/`. No golfer asset
has been written; the proposed slate below still awaits the Phase B word.

## Stations and walks

`CumberlandCourse` reads `course` and the clubhouse lot from the committed ground
`plan.json`, with tee radius from `surface-rules.json`. It uses the ground intake's
JSON parser. The paint's `MownHole` reader is private inside a protected file, so
it remains untouched; tee, green and line coordinates come from the same document.
Station offsets are named constants; no tee or green coordinate is repeated in code.

There are 43 proposed stations: four at each of five tees, four at each of five
greens (1–4 and 18), the clubhouse door and two edge stations. The tee slots form
a file 0.1, 1.5, 2.9 and 4.3 m behind the markers, within the plan's 4.5 m tee radius.
The green slots stand on a 9 m arc inside the plan's 13 m radius, facing the flag.
Their angular steps are 18 degrees. Slot separation is at least 1.4 m.

One tree has 53 named nodes and its root on the clubhouse lot's south face. Each
slot has a node, ordered along the branch, so arrival and departure do not pass
through a waiting party member. The return pair uses green 18's slots d then c.
The front plays slots a through d in that order. No fairway stop is added.

Measured along the tree, including the slots and the clubhouse turns:

| Branch | Metres |
|---|---:|
| Clubhouse, tees and greens 1–4, tee 5, east edge out | 1,265.163 |
| East edge in, green 18, clubhouse | 193.880 |
| Total tree | 1,459.043 |

The first hole follows its source line with a detour through the cart path's
intersection with the brook: approximately (45.4198, 133.4916), the cart bridge site.
It approaches on landings 6 m either side. Hole 2 passes west of its pond using
the pond's radii plus a 2 m clearance. The link to tee 4 passes south of its nearest
bunker, also with 2 m clearance. Green approaches enter outside the standing arc.
Analytic segment checks cover all lane edges and station links against the pond
ellipses, bunker circles and brook widths. The brook has exactly one crossing.

Both edge positions are intersections with the region's derived east edge:
out on hole 5 at (380, 107.2414), in on hole 18 at (380, 84).
The clubhouse door is (240, 53), derived from its lot's centre, size and south facing.

## Out of sight

`RoutineStationEntry.OutOfSight` is a new boolean, default false. Only the clubhouse
door and the two east edge stations carry it here. It is independent of `Interior`.
`RoutineShelter.OutOfSight` is the pose answer; the shared visibility decision
disables both the renderer and the talk component.

A walk stays visible up to the destination's lane node and becomes visible from
the origin's lane node. Between two hidden stations the route contains only the
destination, has zero walked length and is hidden from its departure onward.
Existing unmarked stations retain their earlier thresholds. Sampling remains pure
and allocation free; no round state is added to persistence or Core.

## Roster and hours

All eight visitors walk at 1.5 m/s, with zero jitter and a catch-up multiplier of 1.6.
Each party's visible departures are four game minutes apart. Every block is tagged
`Recreation`. An asset's entries, pace and jitter become its editable timing truth.
The table computes initial hours from the longest slot walk for each leg, using
the game's 75 seconds per game hour. It does not recompute an existing asset.

The front four leave at 06:30, 06:34, 06:38 and 06:42. The table below gives the
leader's departures toward each destination, rounded to the nearest game minute.
Add 4, 8 or 12 minutes for the other members. Arrival depends on the member's slot.

| Destination | Leader departure |
|---|---|
| Tee 1 | 06:30 |
| Green 1 | 07:23 |
| Tee 2 | 10:21 |
| Green 2 | 10:51 |
| Tee 3 | 12:21 |
| Green 3 | 12:45 |
| Tee 4 | 16:34 |
| Green 4 | 17:01 |
| Tee 5 | 18:04 |
| East edge out | 18:32 |
| Clubhouse, hidden transfer | 18:48 |

The first tee has a 16 minute dwell floor (16.00–18.24 minutes across the four).
Other visible front blocks have a 4 minute floor (4.00–6.27 minutes). These margins
keep every departure in file while allowing different station walk lengths.
All four stand on their first tee slots at 07:20. They reach the east edge at
approximately 18:42, 18:47, 18:51 and 18:56, before the loaded profile's 20:00 sunset.
The hidden transfers back to the clubhouse finish at 18:48, 18:52, 18:56 and 19:00.

Both back pairs move, hidden, from clubhouse to east edge in at 11:00. Their visible
walks start at 14:00/14:04 and 16:30/16:34. The green has a 12 minute dwell floor;
the second member dwells 13.50 minutes. They leave the green at about 15:01/15:05
and 17:31/17:35, arriving hidden at the clubhouse at about 15:55/16:01 and 18:25/18:31.
Each has walked 193.880 m. Each front visitor has walked 1,265.163 m.

## Proposed visitors

These names, looks and lines are invented proposals awaiting the Phase B word.
No NPC, routine, build or dialogue asset has been created.

| Party | Name | Look | Line |
|---|---|---|---|
| Front | Mara Fenwick | Short silver hair, moss pullover, cream slacks. | A quiet start leaves room for a better finish. |
| Front | Tobin Rell | Dark curls, rust polo, slate slacks. | That last putt will keep me thinking all day. |
| Front | Elsi Brindle | Copper braid, blue cardigan, dark slacks. | There is always time to take the long view. |
| Front | Orrin Vale | Grey beard, ochre cap, navy pullover. | The walk is half the reason I come. |
| Back 1 | Nella Tarn | Dark bob, plum windbreaker, pale slacks. | One good shot can carry an afternoon. |
| Back 1 | Vey Corren | Close cropped hair, teal polo, charcoal slacks. | I count the pleasant stretches before the strokes. |
| Back 2 | Ione Merrow | White curls, amber cardigan, brown slacks. | The last green still has a surprise in it. |
| Back 2 | Pell Wickett | Sandy hair, blue cap, cream pullover. | A steady pace gets me round. |

Every proposed golfer id, for the seat's check:

| Name | NPC | Routine | Dialogue | Build |
|---|---|---|---|---|
| Mara Fenwick | `npc.mara_fenwick` | `routine.mara_fenwick` | `dialogue.mara_first` | `char_build.mara_fenwick` |
| Tobin Rell | `npc.tobin_rell` | `routine.tobin_rell` | `dialogue.tobin_first` | `char_build.tobin_rell` |
| Elsi Brindle | `npc.elsi_brindle` | `routine.elsi_brindle` | `dialogue.elsi_first` | `char_build.elsi_brindle` |
| Orrin Vale | `npc.orrin_vale` | `routine.orrin_vale` | `dialogue.orrin_first` | `char_build.orrin_vale` |
| Nella Tarn | `npc.nella_tarn` | `routine.nella_tarn` | `dialogue.nella_first` | `char_build.nella_tarn` |
| Vey Corren | `npc.vey_corren` | `routine.vey_corren` | `dialogue.vey_first` | `char_build.vey_corren` |
| Ione Merrow | `npc.ione_merrow` | `routine.ione_merrow` | `dialogue.ione_first` | `char_build.ione_merrow` |
| Pell Wickett | `npc.pell_wickett` | `routine.pell_wickett` | `dialogue.pell_first` | `char_build.pell_wickett` |

Every proposed station id:

```text
station.cumberland.clubhouse_door
station.cumberland.tee_1_a
station.cumberland.tee_1_b
station.cumberland.tee_1_c
station.cumberland.tee_1_d
station.cumberland.green_1_a
station.cumberland.green_1_b
station.cumberland.green_1_c
station.cumberland.green_1_d
station.cumberland.tee_2_a
station.cumberland.tee_2_b
station.cumberland.tee_2_c
station.cumberland.tee_2_d
station.cumberland.green_2_a
station.cumberland.green_2_b
station.cumberland.green_2_c
station.cumberland.green_2_d
station.cumberland.tee_3_a
station.cumberland.tee_3_b
station.cumberland.tee_3_c
station.cumberland.tee_3_d
station.cumberland.green_3_a
station.cumberland.green_3_b
station.cumberland.green_3_c
station.cumberland.green_3_d
station.cumberland.tee_4_a
station.cumberland.tee_4_b
station.cumberland.tee_4_c
station.cumberland.tee_4_d
station.cumberland.green_4_a
station.cumberland.green_4_b
station.cumberland.green_4_c
station.cumberland.green_4_d
station.cumberland.tee_5_a
station.cumberland.tee_5_b
station.cumberland.tee_5_c
station.cumberland.tee_5_d
station.cumberland.east_edge_out
station.cumberland.green_18_a
station.cumberland.green_18_b
station.cumberland.green_18_c
station.cumberland.green_18_d
station.cumberland.east_edge_in
```

No additional region, flag, piece or words id is proposed. The 32 golfer ids have
no collision in the existing NPC, routine or build catalog. The seat still checks
the complete slate before any asset is written.

## Builders and verification

The region's Create and Refresh call `CumberlandRoutines.BuildAndSave` for the
derived tables. Separate menu actions build tables or place the approved golfers.
`HeronViewRoutinesBuilder.Build` creates missing routines only, preserving authored
fields of an existing asset while repairing region bookkeeping. Rewrite has a separate confirmation dialog; its CLI
entry is explicitly named `RewriteConfirmedCli`.

CLI entry points are `CumberlandRoutines.BuildCli`, `CumberlandRoutines.PlaceGolfersCli`,
`HeronViewRoutinesBuilder.BuildCli` and `HeronViewRoutinesBuilder.RewriteConfirmedCli`,
all under `HiddenHarbours.App.Editor`. They report failure through the editor exit
code. They have not been executed in Phase A.

The two new EditMode fixtures contain these 16 tests:

- `CumberlandCourse_StationsDeriveFromThePlansHoles`
- `CumberlandCourse_FourStationsATee_NeverStack`
- `CumberlandCourse_LanesAreOneTree_RootedAtTheClubhouse`
- `CumberlandCourse_NoLaneThroughWaterOrSand`
- `CumberlandCourse_EdgeStationsSitOnTheEdge`
- `RoutineOutOfSight_OffByDefault_MatchesUnmarkedPlan`
- `RoutineOutOfSight_OccupantNeitherDrawnNorTalkable`
- `RoutineOutOfSight_WalkDrawnToAndFromItsNode`
- `RoutineOutOfSight_BlockBetweenTwo_HasNoWalk`
- `HeronViewRound_EveryWalkArrivesBeforeItsNextDeparture`
- `HeronViewRound_FrontPartyOutBeforeSunset`
- `HeronViewParty_WalksInFile`
- `HeronViewRound_IsAPureFunctionOfTheClock`
- `HeronViewRoutines_BuilderCreatesMissing_NeverOverwrites`
- `HeronViewRound_FirstTeeHasAnHourWithAllFourOnTheirSlots`
- `HeronViewProposals_IdsAndDaysAreComplete`

The headless source host passes all 16, plus `StPeters_PreChangePlannerParity`:
six committed village routines, 26 committed stations, their lane tree and indoor
door geometry, three seeds and every minute of a day. All 25,920 poses match the
pre-change planner and sampler at the base. This host uses small component and
asset facades; it is not an editor test-run result. Its inputs, scripts and logs
are kept in ignored `artifacts/golf/`.
Party spacing is sampled every ten game seconds, including every whole minute.

Five geometry tests also pass directly from the rebuilt test assembly using the
engine's managed types, without component facades. Headless compilation rebuilds
World (138 source files), App.Editor (157) and both new test fixtures without errors.
The permitted donor inputs include World and Core DLLs dated 2026-10-22 10:18:51 UTC
and App.Editor dated 10:18:54 UTC. They were copied read-only into the work folder;
the changed assemblies were compiled from this checkout's source. No stale-symbol
error occurred. At the Phase A stop, protected builders, village assets, all Data,
all scenes and Core were byte-identical to the base. A2 changes only the permitted
catalog hunks and the six region lines there. All scenes and Core remain unchanged.
No Library directory was created.

The six asset additions, with no other asset changes:

```diff
diff --git a/Assets/_Project/Data/Routines/AuntGinnyRoutine.asset b/Assets/_Project/Data/Routines/AuntGinnyRoutine.asset
index 7f3ce1ca7c..452ea6987c 100644
--- a/Assets/_Project/Data/Routines/AuntGinnyRoutine.asset
+++ b/Assets/_Project/Data/Routines/AuntGinnyRoutine.asset
@@ -15,0 +16 @@ MonoBehaviour:
+  RegionId: region.st_peters
diff --git a/Assets/_Project/Data/Routines/BasilSamsonRoutine.asset b/Assets/_Project/Data/Routines/BasilSamsonRoutine.asset
index 5963e9ee61..2124878cb6 100644
--- a/Assets/_Project/Data/Routines/BasilSamsonRoutine.asset
+++ b/Assets/_Project/Data/Routines/BasilSamsonRoutine.asset
@@ -15,0 +16 @@ MonoBehaviour:
+  RegionId: region.st_peters
diff --git a/Assets/_Project/Data/Routines/EileenDoironRoutine.asset b/Assets/_Project/Data/Routines/EileenDoironRoutine.asset
index 5ceecf734a..6b6874701c 100644
--- a/Assets/_Project/Data/Routines/EileenDoironRoutine.asset
+++ b/Assets/_Project/Data/Routines/EileenDoironRoutine.asset
@@ -15,0 +16 @@ MonoBehaviour:
+  RegionId: region.st_peters
diff --git a/Assets/_Project/Data/Routines/JuniorPoirierRoutine.asset b/Assets/_Project/Data/Routines/JuniorPoirierRoutine.asset
index e8fdc4a0d0..9d6b8fd176 100644
--- a/Assets/_Project/Data/Routines/JuniorPoirierRoutine.asset
+++ b/Assets/_Project/Data/Routines/JuniorPoirierRoutine.asset
@@ -15,0 +16 @@ MonoBehaviour:
+  RegionId: region.st_peters
diff --git a/Assets/_Project/Data/Routines/MargueriteLeBlancRoutine.asset b/Assets/_Project/Data/Routines/MargueriteLeBlancRoutine.asset
index d2efdb7a04..e9b5fbc17c 100644
--- a/Assets/_Project/Data/Routines/MargueriteLeBlancRoutine.asset
+++ b/Assets/_Project/Data/Routines/MargueriteLeBlancRoutine.asset
@@ -15,0 +16 @@ MonoBehaviour:
+  RegionId: region.st_peters
diff --git a/Assets/_Project/Data/Routines/RoseMacIsaacRoutine.asset b/Assets/_Project/Data/Routines/RoseMacIsaacRoutine.asset
index d27446dfd8..9ea96af01e 100644
--- a/Assets/_Project/Data/Routines/RoseMacIsaacRoutine.asset
+++ b/Assets/_Project/Data/Routines/RoseMacIsaacRoutine.asset
@@ -15,0 +16 @@ MonoBehaviour:
+  RegionId: region.st_peters
```

## A2 verification and commits

The 17 headless source checks pass again. The village comparison now reads its six
days through the production shared loader in the source host, with an asset facade
and a synthetic golf day to check separation. All 25,920 village poses still match
the pre-change planner and sampler exactly. Golf geometry, clocks, hours and the
two branch lengths did not move. The builder preservation test also checks that
region repair keeps an edited speed, entries and NPC assignment.

The five pure geometry checks pass again directly from the rebuilt assembly.
World (138 sources), App.Editor (158 sources) and the three lane test fixtures
compile with no errors. Rebuilding World before App.Editor supplies the new field;
the borrowed World DLL is not used as its substitute. The donor World/Core DLLs
are dated 2026-10-22 10:18:51 UTC and App.Editor 10:18:54 UTC. There were no CS1061
errors on `RegionId` or other stale-symbol errors.

Four added guards compile but have NOT run. They read the asset database and wait
for Phase B:

- `RoutineCatalog_EveryRoutineNamesARegionThatExists`
- `RoutineCatalog_IdsAreUniqueAcrossEveryRegion`
- `RoutineCatalog_EachRegionReadsOnlyItsOwn`
- `RoutineCatalog_EveryDayIsWellFormed`

Boundary commit `3f96a61562a177fee402a09d7f50a346bdb14ca7`, fourteen files:

- `Assets/_Project/Code/World/Routines/RoutineDef.cs`
- `Assets/_Project/Code/App/Editor/RoutineCatalog.cs`
- `Assets/_Project/Code/App/Editor/RoutineCatalog.cs.meta`
- `Assets/_Project/Code/App/Editor/StPetersRoutines.cs`
- `Assets/_Project/Code/App/Editor/RoutineDefsBuilder.cs`
- `Assets/_Project/Data/Routines/AuntGinnyRoutine.asset`
- `Assets/_Project/Data/Routines/BasilSamsonRoutine.asset`
- `Assets/_Project/Data/Routines/EileenDoironRoutine.asset`
- `Assets/_Project/Data/Routines/JuniorPoirierRoutine.asset`
- `Assets/_Project/Data/Routines/MargueriteLeBlancRoutine.asset`
- `Assets/_Project/Data/Routines/RoseMacIsaacRoutine.asset`
- `Assets/Tests/EditMode/RoutineCatalogTests.cs`
- `Assets/Tests/EditMode/RoutineCatalogTests.cs.meta`
- `docs/design/npcs-and-routines.md`

The separate golf follow-up contains five files; its SHA is in the A2 delivery:

- `Assets/_Project/Code/App/Editor/HeronViewRoster.cs`
- `Assets/_Project/Code/App/Editor/HeronViewRoutinesBuilder.cs`
- `Assets/_Project/Code/App/Editor/CumberlandRoutines.cs`
- `Assets/Tests/EditMode/CumberlandCourseTests.cs`
- `docs/design/heron-view-golf.md`

## Phase B handoff

Re-estimate one 105 minute keeper turn: 20–30 minutes for cold import, 35–40 for
approved assets, builder wiring and named tests, and 30–35 for views and profiling.
The increase from 90 minutes allows the village bookkeeping build and the expanded
test set. Import time is an estimate, not a measurement.

The next turn needs the approved names, looks and ids, then NPC, dialogue and build
assets made on the character art available at that base. Run content validation
for those assets and the new routines, the new fixtures, the existing village
routine fixtures and the scene-dependent first-tee/hidden-party PlayMode test.
Also run `RoutineDefsBuilder.Build` once, confirming all six kept assets remain
byte-identical to their A2 versions, all four new catalog guards,
`ConversationContentValidationTests`, `StoreClerkContentTests` and
`VillagerRoutinePlayTests`. The seat's trial merge with the village pass precedes
this turn. The boundary rides step 1's draft PR; no separate PR was opened.
Check the three course views at 09:30 on day 6, the shared first-tee view at 07:20,
and a back-pair view on green 18. Record p95 frame time and memory against the
ground pass. No scene build, art choice, screenshot, profile or editor test is
claimed by Phase A or A2. Stop here for the seat's Phase B paste and the approved slate.
