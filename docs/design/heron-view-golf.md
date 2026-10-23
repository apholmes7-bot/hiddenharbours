# Heron View Golf Club — step 1, Phase A

Phase A is STOPPED at base `f9276ec4b118957952046769961a77faa4564eb3` on
`feat/heron-view-golf-step1`. All work was headless. This is a place for recreation;
there is no ledger, booking, payment, membership, ball flight or swing simulation.

## Stop finding: the routine catalog has no region boundary

Final review found a conflict that needs a Planner amendment before Phase B:

- `StPetersRoutines.LoadRoutines` searches all of `Assets/_Project/Data/Routines`
  with `AssetDatabase.FindAssets`, including subfolders. It has no region filter.
- `StPetersRoutineContentTests.EveryStationNamedByARoutineIsOneThisIslandDeclares`
  iterates that entire result and requires every station to be in the village's
  declared set. The golf station ids cannot pass it.
- The charter puts the new routine assets in `Data/Routines` and protects
  `StPetersRoutines.cs` from edits. A region filter would change a protected file;
  putting golf assets elsewhere would change the prescribed content layout.
  A subfolder does not isolate the assets from the recursive search.

No golfer asset has been written, so the existing catalog still passes the
headless pose comparison. The drafted engine rule, derivation, builders, proposed
roster and tests below are preserved for review. They do not resolve this catalog
conflict. Charter section 7 requires a stop for a finding that changes the plan.
No protected loader change or alternate asset location has been attempted.

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
`HeronViewRoutinesBuilder.Build` creates missing routines only, preserving every
field of an existing asset. Rewrite has a separate confirmation dialog; its CLI
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
error occurred. Protected builders, village assets, all Data, all scenes and Core
remain byte-identical to the base. No Library directory was created.

## Phase B handoff

After the catalog amendment, estimate one 90 minute keeper turn, including approximately 20–30 minutes for the
cold import, 25–30 for approved assets, builder wiring and named tests, and 25–30
for views and profiling. Import time is an estimate, not a measurement.

The next turn needs the approved names, looks and ids, then NPC, dialogue and build
assets made on the character art available at that base. Run content validation
for those assets and the new routines, the new fixtures, the existing village
routine fixtures and the scene-dependent first-tee/hidden-party PlayMode test.
Check the three course views at 09:30 on day 6, the shared first-tee view at 07:20,
and a back-pair view on green 18. Record p95 frame time and memory against the
ground pass. No scene build, art choice, screenshot, profile or editor test is
claimed by Phase A. Stop here for the Planner amendment and a later Phase B paste.
