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

## Phase B result

The approved eight visitors now have NPC, routine, dialogue and build assets, and
stand in Cumberland's saved scene on the derived stations and lanes. The
missing-only check kept all 32 assets byte-identical. The six village routines
also remain byte-identical to A2, and its routine content test file is unchanged.

`HeronViewGolferColours` is new runtime code in Art. It resolves each golf build's
colour fields through `CharacterBuilder.TryBuild`, then paints its ashore figure.
`CumberlandRoutines.PlaceGolfers` adds and configures this component. The rendered
ramps of all eight visitors matched their resolved colours: the front party at
07:20, the first back pair at 14:56, and the later pair at 17:26. This is a new
path for cast build colours, alongside the existing player resolver path.

Two existing validation premises changed. `EveryBakedPreset_HasABuildAsset` now
checks preset coverage as a set, allowing multiple builds to share a preset.
`CharacterBuildContentTests.RampsPath` now selects `CharacterRamps10.asset`, the
shipped resolver's ramps. `EveryColourOverride_ResolvesAgainstTheRamps` passed
over every build, including the existing builds.

### Named checks

Each row below was a separate editor launch. Counts are total / passed / skipped /
failed. All five new B checks ran once; none was a zero-match filter.

| Filter | Counts |
|---|---|
| `CharacterBuildContentTests` | 9 / 9 / 0 / 0 |
| `CharacterBuilderContentTests` | 10 / 10 / 0 / 0 |
| `HeronViewContent_AllEightDefsResolve_AndKeepTheApprovedWords` | 1 / 1 / 0 / 0 |
| `HeronViewAssets_BuildKeepsAllThirtyTwoFiles` | 1 / 1 / 0 / 0 |
| `RoutineOutOfSight_OffByDefault_StPetersUnchanged` | 1 / 1 / 0 / 0 |
| `CumberlandCourseTests` | 12 / 12 / 0 / 0 |
| `RoutineOutOfSightTests` | 4 / 4 / 0 / 0 |
| `RoutineCatalogTests` | 4 / 4 / 0 / 0 |
| `StPetersRoutineContentTests` | 26 / 26 / 0 / 0 |
| `ConversationContentValidationTests` | 9 / 9 / 0 / 0 |
| `StoreClerkContentTests` | 14 / 14 / 0 / 0 |
| `NpcContentValidationTests` | 8 / 8 / 0 / 0 |
| `VillagerRoutinePlayTests` | 13 / 13 / 0 / 0 |
| `HeronViewGolfer_StandsOnTheFirstTee_AtItsHour` | 1 / 1 / 0 / 0 |
| `HeronViewGolf_FiveCoursePlates` | 1 / 1 / 0 / 0 |
| `CumberlandPlayablePlayTests.Measure_SeparateSixtySecondWalkAndRow_AfterWarmup` | 1 / 1 / 0 / 0 |

Totals: EditMode 99 / 99 / 0 / 0; PlayMode 16 / 16 / 0 / 0. Results and logs are
under `artifacts/golf/b2/`, with one XML per filter. The golf content case checks
all approved words and ids, every new Def's links and walkability, and `TryLook`
for all eight builds. The plate case also compares the drawn palette ramps.

### Plates and fixed-preset limits

The five 1920 by 1080 plates use the 14 m high CD frames. All were written after
the new floor at 16:02:25 UTC on 2026-10-23. The clock uses day index 6.

| File under `artifacts/golf/b/plates/` | Hour | Centre |
|---|---|---|
| `first-tee-0930.png` | 09:30 | (214, 94) |
| `green-18-0930.png` | 09:30 | (289, 96) |
| `clubhouse-0930.png` | 09:30 | (238, 60) |
| `first-tee-party.png` | 07:20 | (214, 94) |
| `green-18-back-pair.png` | 14:56 | (289, 96) |

The first-tee party and back-pair plates show their separate slots and colour
choices. The clubhouse frame shows its empty ground pad; the later key-scene
pieces remain CMB3's. The later pair's palette was checked at 17:26 within the
same test, after the five required captures.

The approved look sentences remain unchanged. The table records what the existing
structures and colour fields can draw. The plate review still decides the fit of
these approximations.

| Visitor | Preset | Look details the preset cannot draw, and colour limits |
|---|---|---|
| Mara Fenwick | `hand` | Pullover becomes work shirt; a fixed cap covers much of the cropped hair. Silver uses `grey`; moss shirt and cream slacks are set. |
| Tobin Rell | `hand` | Curls and polo become crop and work shirt, with a fixed cap. Rust shirt uses `red`; slate slacks are set. |
| Elsi Brindle | `cutter` | Braid and cardigan become ponytail and apron. Copper uses `ginger`, blue uses `sky`; dark slacks use `char`. |
| Orrin Vale | `skipper` | Cap and pullover become souwester and oilskins. The beard uses `grey`; ochre hat uses `oat`, and navy is set on outfit and shirt. |
| Nella Tarn | `ginny` | Windbreaker and pale slacks become plum overalls with a cream shirt. The bob stays, partly covered by the fixed hood. |
| Vey Corren | `hand` | Polo becomes work shirt, with a fixed cap over the crop. Teal shirt uses `moss`; charcoal slacks use `char`. |
| Ione Merrow | `nan` | Curls, cardigan and slacks become bun, shawl and skirt, with a kerchief. Amber uses `ochre`; brown uses `khaki`. |
| Pell Wickett | `hand` | Pullover becomes work shirt. Sandy hair uses `sand`, blue cap uses `navy`, and cream is set on shirt and outfit. |

### Frame time and memory

The existing CMB0 walking and rowing case ran at 640 by 480, Very Low, D3D11,
vsync 0 and target frame rate -1. Both 60-second routes met the 16.67 ms p95 bar.
The route clock is paused at 05:12:37, when the golf visitors are out of sight.
The first-tee and back-pair renders are covered by the separate plate test.

| Route | Samples | Distance m | Median ms | Golf p95 ms | CMB0 p95 ms |
|---|---:|---:|---:|---:|---:|
| Walk, six-second warmup | 36621 | 179.993 | 1.553 | 2.086 | 7.002 |
| Row, three-second warmup | 38574 | 72.638 | 1.454 | 2.099 | 2.498 |

CMB0's first rowing measure was 26.069 ms p95; its accepted repeat was 2.498 ms.
The comparison above uses that accepted repeat. These are editor measurements.

Memory is in MiB, with golf / CMB0 side by side. These totals describe the two
recorded editor runs.

| Point | Allocated | Reserved | Textures | Meshes |
|---|---:|---:|---:|---:|
| Before plan and scene | 610.957 / 622.38 | 1186.512 / 1176.51 | 57.986 / 57.99 | 6.263 / 6.26 |
| Loaded | 1137.636 / 1102.04 | 1687.781 / 1631.78 | 601.379 / 549.70 | 172.012 / 169.59 |
| After walk | 1140.115 / 1104.17 | 1687.781 / 1631.78 | 601.379 / 549.70 | 171.981 / 169.56 |
| After row | 1142.811 / 1106.54 | 1687.781 / 1631.78 | 601.385 / 549.71 | 172.210 / 169.79 |

The keeper returned at 16:14:44 UTC with all sixteen launched editor PIDs gone
and the save unchanged after every launch and on return. The unrelated
`ProjectSettings/ProjectSettings.asset` rewrite stays unstaged.

### CI forecast

Against main `58de4973`, the supplied counts are EditMode 15086 / 14820 / 266 / 0
and PlayMode 1159 / 998 / 161 / 0, in total / passed / skipped / failed order.
The branch adds 23 EditMode cases and two PlayMode cases. If those cases pass and
the baseline outcomes repeat, the forecast is EditMode 15109 / 14843 / 266 / 0
and PlayMode 1161 / 1000 / 161 / 0. This is a forecast for the eventual merge-ref
run. The local named filters above are the recorded runs.

New EditMode names, grouped by fixture:

- `CumberlandCourseTests` (12): `CumberlandCourse_StationsDeriveFromThePlansHoles`,
  `CumberlandCourse_FourStationsATee_NeverStack`,
  `CumberlandCourse_LanesAreOneTree_RootedAtTheClubhouse`,
  `CumberlandCourse_EdgeStationsSitOnTheEdge`,
  `CumberlandCourse_NoLaneThroughWaterOrSand`,
  `HeronViewRound_EveryWalkArrivesBeforeItsNextDeparture`,
  `HeronViewRound_FrontPartyOutBeforeSunset`, `HeronViewParty_WalksInFile`,
  `HeronViewRound_FirstTeeHasAnHourWithAllFourOnTheirSlots`,
  `HeronViewRound_IsAPureFunctionOfTheClock`,
  `HeronViewRoutines_BuilderCreatesMissing_NeverOverwrites`,
  `HeronViewProposals_IdsAndDaysAreComplete`.
- `RoutineOutOfSightTests` (4): `RoutineOutOfSight_OccupantNeitherDrawnNorTalkable`,
  `RoutineOutOfSight_WalkDrawnToAndFromItsNode`,
  `RoutineOutOfSight_BlockBetweenTwo_HasNoWalk`,
  `RoutineOutOfSight_OffByDefault_MatchesUnmarkedPlan`.
- `RoutineCatalogTests` (4, A2): `RoutineCatalog_EveryRoutineNamesARegionThatExists`,
  `RoutineCatalog_IdsAreUniqueAcrossEveryRegion`,
  `RoutineCatalog_EachRegionReadsOnlyItsOwn`, `RoutineCatalog_EveryDayIsWellFormed`.
- `HeronViewContentTests` (3, B):
  `HeronViewContent_AllEightDefsResolve_AndKeepTheApprovedWords`,
  `HeronViewAssets_BuildKeepsAllThirtyTwoFiles`,
  `RoutineOutOfSight_OffByDefault_StPetersUnchanged`.

New PlayMode names in `HeronViewGolfPlayTests` (2, B):
`HeronViewGolfer_StandsOnTheFirstTee_AtItsHour` and
`HeronViewGolf_FiveCoursePlates`.

The changed existing checks are `EveryBakedPreset_HasABuildAsset` and
`EveryColourOverride_ResolvesAgainstTheRamps`, as described above; their names
and case counts stay the same.

### Gate accounting

The lane's two approved serialization exceptions account for 47 added lines:
32 empty metadata keys and 15 scene parent keys. Every other added line and
each commit message passed the full word list. Per-file counts follow.

| File | Empty metadata key | Scene parent key |
|---|---:|---:|
| `Assets/_Project/Data/Characters/Builds/ElsiBrindleBuild.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Characters/Builds/IoneMerrowBuild.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Characters/Builds/MaraFenwickBuild.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Characters/Builds/NellaTarnBuild.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Characters/Builds/OrrinValeBuild.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Characters/Builds/PellWickettBuild.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Characters/Builds/TobinRellBuild.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Characters/Builds/VeyCorrenBuild.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/Dialogue/ElsiBrindleFirst.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/Dialogue/IoneMerrowFirst.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/Dialogue/MaraFenwickFirst.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/Dialogue/NellaTarnFirst.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/Dialogue/OrrinValeFirst.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/Dialogue/PellWickettFirst.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/Dialogue/TobinRellFirst.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/Dialogue/VeyCorrenFirst.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/ElsiBrindle.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/IoneMerrow.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/MaraFenwick.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/NellaTarn.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/OrrinVale.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/PellWickett.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/TobinRell.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/NPCs/VeyCorren.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Routines/ElsiBrindleRoutine.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Routines/IoneMerrowRoutine.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Routines/MaraFenwickRoutine.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Routines/NellaTarnRoutine.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Routines/OrrinValeRoutine.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Routines/PellWickettRoutine.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Routines/TobinRellRoutine.asset.meta` | 1 | 0 |
| `Assets/_Project/Data/Routines/VeyCorrenRoutine.asset.meta` | 1 | 0 |
| `Assets/_Project/Scenes/Cumberland.unity` | 0 | 15 |


Asset and placement commit: `dc1465e974325014efde03d9f2a5ea070d22d63f` (71 files).
