# NPC roster against declared and placed stations

The pinned content contains 31 NPC Defs: 29 people and two readable objects. Sixteen people have routine assets and matching scene components; thirteen have no routine. Eight of those thirteen are planned St Peters residents, one is the unplaced mainland storekeeper, and four are placed mainland contacts. Eight golfers have complete recreational days but no authored home or work activity. In total, **21 people lack a routine Home activity**; this does not mean the village plan has no intended address for them.

All ordinary routine destinations exist in their region declarations and committed scene tables. The three missing routine references are in one Wednesday override: the mainland bar landing twice and the fuel stop once. Rose’s ordinary `home_saltbox` exists in both places; the missing home is her proposed **post-sale** `home_post_office`. Claudette is still absent from the mainland scene. There is no visible station-sharing conflict in the nominal weekly target windows; three golf destinations deliberately share hidden positions.

Audit base: `832c6f78f9e607cfbdede4845a3d205551439c91`. No content, scenes, prefabs, appearance, clock, or save format were changed. This is a source and serialized-text audit, not a playtest.

## Scope and reading the tables

Every committed `RegionDef` is covered below, including regions with no NPCs and West Water with no committed scene. The unassigned `Greywick.unity` scene is also searched; it has no NPC or routine table components. Canon regions that have no RegionDef at this base have no authored roster to audit; this document does not invent one.

Files are relative to `Assets/_Project/Data/`: NPC filenames below mean `NPCs/<file>`; routine filenames mean `Routines/<file>`. The ID of each routine is `routine.<NPC suffix>`. Station suffixes in each table expand to `station.<region suffix>.<suffix>`. **D/S = declared / serialized in that region’s scene**. “All Y/Y” covers every home, work, leisure and errand station in that row. “—/—” means no station is authored, not a successful validation.

A home is a nonblank station tagged `RoutineActivity.Home` in the NPC’s own region. A doorstep counts because that is what current data authors; it does not prove an indoor bed. Work and leisure use the `Work` and `Recreation` tags; errands are listed separately. Readable objects are enumerated but exempt from living-person rules. Planned houses in prose are labelled as plans rather than runnable home stations.

`RoutineDef`, `RoutineSchedule`, `RoutineLegs.EntriesFor` and [the routine design](npcs-and-routines.md#2-the-routine-system-p3--p1) define start times as departures. The last block wraps midnight. The ordinary day applies to all seven weekdays unless a matching weekly day replaces it. Sixteen ordinary days plus Junior’s Wednesday override have strictly ascending hours in [0,24), and their circular intervals total 24 hours. This gives 112 selected daily schedules across one week, **not proof every destination is reachable**. Junior’s weekly trip has missing mainland stations; travel, gates and interruptions can change occupancy.

Declarations: `StPetersRoutines.AllStationIds` (26), `NmcWholesalerKeepers.All` counter/home pairs (4 across two local tables), and `CumberlandCourse.Stations` (43). Each declaration set equals its committed scene station IDs. There is no general mainland station network. Older statements that the mainland has no routine engine omit the two wholesale clerks.

## region.st_peters

Region Def: `Regions/StPeters.asset`; scene: `Assets/_Project/Scenes/StPeters.unity`.

| NPC Def / file | Placement / routine file | Home stations | Work stations | Leisure; errands | D/S | Full day / weekdays | Sharing |
|---|---|---|---|---|---|---|---|
| `npc.angus_gillis`<br>`AngusGillis.asset` | 0 scene references<br>No routine | Plan: cream saltbox; no station | None authored | None authored | —/— | No / none | No routine to compare |
| `npc.aunt_ginny`<br>`AuntGinny.asset` | 1 scene reference<br>`AuntGinnyRoutine.asset` | `cottage_step` | `ginny_village_mark`, `cottage_garden` | None authored; errands: None authored | All Y/Y | 24h / all 7 | No shared occupied station |
| `npc.basil_samson`<br>`BasilSamson.asset` | 1 scene reference<br>`BasilSamsonRoutine.asset` | `home_sage_b` | `wharf_head`, `slip_head` | `wharf_head`; errands: None authored | All Y/Y | 24h / all 7 | No shared occupied station |
| `npc.dougie_macneil`<br>`DougieMacNeil.asset` | 0 scene references<br>No routine | Plan: harbour gable; no station | None authored | None authored | —/— | No / none | No routine to compare |
| `npc.eileen_doiron`<br>`EileenDoiron.asset` | 1 scene reference<br>`EileenDoironRoutine.asset` | `farmhouse_dooryard`, `home_farmhouse` | `school_desk` | `flats_lookout`; errands: `store_customer_c` | All Y/Y | 24h / all 7 | No shared occupied station |
| `npc.junior_poirier`<br>`JuniorPoirier.asset` | 1 scene reference<br>`JuniorPoirierRoutine.asset` | `sage_dooryard`, `home_sage_a` | `flats_head` | `green_d`; errands: `store_customer_b`; Wednesday: `station.nine_mile_creek.bar_landing`, `station.nine_mile_creek.fuel_stop` | Ordinary Y/Y; mainland N/N | 24h / all 7; Wed overrides | No shared occupied station |
| `npc.lucie_landry`<br>`LucieLandry.asset` | 0 scene references<br>No routine | Plan: white gable; no station | None authored | None authored | —/— | No / none | No routine to compare |
| `npc.marguerite_leblanc`<br>`MargueriteLeBlanc.asset` | 1 scene reference<br>`MargueriteLeBlancRoutine.asset` | `home_store` | `store_counter` | `green_c`; errands: `post_office_door` | All Y/Y | 24h / all 7 | No shared occupied station |
| `npc.monique_landry`<br>`MoniqueLandry.asset` | 0 scene references<br>No routine | Plan: white gable; no station | None authored | None authored | —/— | No / none | No routine to compare |
| `npc.ned_letter`<br>`NedLetter.asset` | 1 scene reference<br>No routine | Object; not applicable | Object; not applicable | Object; not applicable | —/— | Not applicable | Not a person |
| `npc.paul_landry`<br>`PaulLandry.asset` | 0 scene references<br>No routine | Plan: white gable; no station | None authored | None authored | —/— | No / none | No routine to compare |
| `npc.rene_chiasson`<br>`ReneChiasson.asset` | 0 scene references<br>No routine | Plan: harbour gable; no station | None authored | None authored | —/— | No / none | No routine to compare |
| `npc.rose_macisaac`<br>`RoseMacIsaac.asset` | 1 scene reference<br>`RoseMacIsaacRoutine.asset` | `saltbox_dooryard`, `home_saltbox`; post-sale `home_post_office` (missing) | `post_office_counter` | `green_a`; errands: `store_customer_a` | Routine Y/Y; post-sale N/N | 24h / all 7 | No shared occupied station |
| `npc.sam_landry`<br>`SamLandry.asset` | 0 scene references<br>No routine | Plan: white gable; no station | None authored | None authored | —/— | No / none | No routine to compare |
| `npc.theresa_gillis`<br>`TheresaGillis.asset` | 0 scene references<br>No routine | Plan: cream saltbox; no station | None authored | None authored | —/— | No / none | No routine to compare |

The eight planned residents belong here by [the village plan §8](st-peters-village-plan.md) and [the living economy roster §1.2](st-peters-living-economy.md). None has a routine asset, scene NPC reference, or home/work/leisure station assignment. Ginny’s night is on `cottage_step`; her garden is tagged Work. Basil’s leisure reuses his own `wharf_head`, not another person’s simultaneous slot.

## region.nine_mile_creek

Region Def: `Regions/NineMileCreek.asset`; scene: `Assets/_Project/Scenes/NineMileCreek.unity`.

| NPC Def / file | Placement / routine file | Home stations | Work stations | Leisure; errands | D/S | Full day / weekdays | Sharing |
|---|---|---|---|---|---|---|---|
| `npc.bernadette_pitre`<br>`BernadettePitre.asset` | 1 scene reference<br>`BernadettePitreRoutine.asset` | `fishing_gear_keeper_home` | `fishing_gear_counter` | None authored; errands: None authored | All Y/Y | 24h / all 7 | No shared occupied station |
| `npc.claudette_boudreau`<br>`ClaudetteBoudreau.asset` | 0 scene references<br>No routine | None authored | None authored | None authored | —/— | No / none | No routine to compare |
| `npc.hector_bernard`<br>`HectorBernard.asset` | 1 scene reference<br>No routine | None authored | None authored | None authored | —/— | No / none | No routine to compare |
| `npc.lionel_cormier`<br>`LionelCormier.asset` | 1 scene reference<br>`LionelCormierRoutine.asset` | `boat_gear_keeper_home` | `boat_gear_counter` | None authored; errands: None authored | All Y/Y | 24h / all 7 | No shared occupied station |
| `npc.nmc_harbourmaster`<br>`NmcHarbourmaster.asset` | 1 scene reference<br>No routine | None authored | None authored | None authored | —/— | No / none | No routine to compare |
| `npc.nmc_yard`<br>`NmcYard.asset` | 1 scene reference<br>No routine | None authored | None authored | None authored | —/— | No / none | No routine to compare |
| `npc.wendell_arsenault`<br>`WendellArsenault.asset` | 1 scene reference<br>No routine | None authored | None authored | None authored | —/— | No / none | No routine to compare |

`NineMileCreekPeople.People` names Claudette’s general-store step, but no committed NPC reference places her. Wendell, Hector, the harbourmaster and yard hand have anchored contacts, not RoutineDef home/work/leisure stations. A road trip or boat timetable is not a villager day. The two keeper home stations are deliberately `OutOfSight`, not authored house interiors. Junior is a Wednesday visitor with no mainland proxy placement.

## region.cumberland

Region Def: `Regions/Cumberland.asset`; scene: `Assets/_Project/Scenes/Cumberland.unity`.

| NPC Def / file | Placement / routine file | Home stations | Work stations | Leisure; errands | D/S | Full day / weekdays | Sharing |
|---|---|---|---|---|---|---|---|
| `npc.elsi_brindle`<br>`ElsiBrindle.asset` | 1 scene reference<br>`ElsiBrindleRoutine.asset` | None authored | None authored | `clubhouse_door`, `tee_1_c`, `green_1_c`, `tee_2_c`, `green_2_c`, `tee_3_c`, `green_3_c`, `tee_4_c`, `green_4_c`, `tee_5_c`, `east_edge_out`; errands: None authored | All Y/Y | 24h / all 7 | Hidden shared stops; see below |
| `npc.ione_merrow`<br>`IoneMerrow.asset` | 1 scene reference<br>`IoneMerrowRoutine.asset` | None authored | None authored | `clubhouse_door`, `east_edge_in`, `green_18_d`; errands: None authored | All Y/Y | 24h / all 7 | Hidden shared stops; see below |
| `npc.mara_fenwick`<br>`MaraFenwick.asset` | 1 scene reference<br>`MaraFenwickRoutine.asset` | None authored | None authored | `clubhouse_door`, `tee_1_a`, `green_1_a`, `tee_2_a`, `green_2_a`, `tee_3_a`, `green_3_a`, `tee_4_a`, `green_4_a`, `tee_5_a`, `east_edge_out`; errands: None authored | All Y/Y | 24h / all 7 | Hidden shared stops; see below |
| `npc.nella_tarn`<br>`NellaTarn.asset` | 1 scene reference<br>`NellaTarnRoutine.asset` | None authored | None authored | `clubhouse_door`, `east_edge_in`, `green_18_d`; errands: None authored | All Y/Y | 24h / all 7 | Hidden shared stops; see below |
| `npc.orrin_vale`<br>`OrrinVale.asset` | 1 scene reference<br>`OrrinValeRoutine.asset` | None authored | None authored | `clubhouse_door`, `tee_1_d`, `green_1_d`, `tee_2_d`, `green_2_d`, `tee_3_d`, `green_3_d`, `tee_4_d`, `green_4_d`, `tee_5_d`, `east_edge_out`; errands: None authored | All Y/Y | 24h / all 7 | Hidden shared stops; see below |
| `npc.pell_wickett`<br>`PellWickett.asset` | 1 scene reference<br>`PellWickettRoutine.asset` | None authored | None authored | `clubhouse_door`, `east_edge_in`, `green_18_c`; errands: None authored | All Y/Y | 24h / all 7 | Hidden shared stops; see below |
| `npc.tobin_rell`<br>`TobinRell.asset` | 1 scene reference<br>`TobinRellRoutine.asset` | None authored | None authored | `clubhouse_door`, `tee_1_b`, `green_1_b`, `tee_2_b`, `green_2_b`, `tee_3_b`, `green_3_b`, `tee_4_b`, `green_4_b`, `tee_5_b`, `east_edge_out`; errands: None authored | All Y/Y | 24h / all 7 | Hidden shared stops; see below |
| `npc.vey_corren`<br>`VeyCorren.asset` | 1 scene reference<br>`VeyCorrenRoutine.asset` | None authored | None authored | `clubhouse_door`, `east_edge_in`, `green_18_c`; errands: None authored | All Y/Y | 24h / all 7 | Hidden shared stops; see below |

All eight routines tag every stop Recreation, including `clubhouse_door`; no block is Home or Work. Each golfer has a distinct slot on the front holes. Back-green slots `green_18_c` and `green_18_d` are reused at different times. No occupational work station is expected for a golf visit, but an off-course home is not represented.

## region.rice_point

Region Def: `Regions/RicePoint.asset`; scene: `Assets/_Project/Scenes/RicePoint.unity`.

| NPC Def / file | Placement / routine file | Home stations | Work stations | Leisure; errands | D/S | Full day / weekdays | Sharing |
|---|---|---|---|---|---|---|---|
| `npc.rpt_wreck`<br>`RicePointWreck.asset` | 2 scene references<br>No routine | Object; not applicable | Object; not applicable | Object; not applicable | —/— | Not applicable | Not a person |

## region.coddle_cove

Region Def: `Regions/CoddleCove.asset`; scene: `Assets/_Project/Scenes/Greybox.unity`.

| NPC Def / file | Home / work / leisure | D/S | Full day / weekdays | Sharing |
|---|---|---|---|---|
| None | Not applicable | No station list / no stations | No routines | Not applicable |

## region.east_water

Region Def: `Regions/EastWater.asset`; scene: `Assets/_Project/Scenes/EastWater.unity`.

| NPC Def / file | Home / work / leisure | D/S | Full day / weekdays | Sharing |
|---|---|---|---|---|
| None | Not applicable | No station list / no stations | No routines | Not applicable |

## region.south_water

Region Def: `Regions/SouthWater.asset`; scene: `Assets/_Project/Scenes/SouthWater.unity`.

| NPC Def / file | Home / work / leisure | D/S | Full day / weekdays | Sharing |
|---|---|---|---|---|
| None | Not applicable | No station list / no stations | No routines | Not applicable |

## region.west_water

Region Def: `Regions/WestWater.asset`; scene: `Assets/_Project/Scenes/WestWater.unity`. **Scene file absent.**

| NPC Def / file | Home / work / leisure | D/S | Full day / weekdays | Sharing |
|---|---|---|---|---|
| None | Not applicable | No station list / no scene | No routines | Not applicable |

## Shared stations and minute coverage

Compared all pairs of selected schedule target windows on Monday through Sunday, splitting midnight and treating the next departure as the end. The result is a conservative **target overlap** audit: walking occupies part of each window, so these intervals are not measured arrival/dwell times. St Peters has no shared station ID between different routine owners; the mainland clerks use separate tables. No two declared station IDs in a region have the same serialized position. This does not test pedestrian separation along paths or occupied indoor furniture.

| Region / shared station | NPCs and nominal overlap | Could two visible people stack there? |
|---|---|---|
| Cumberland / `clubhouse_door` | All eight before 06:30 and after the last front golfer returns at about 19:00; smaller subsets at other times | Shared coordinate, deliberately hidden (`OutOfSight: 1`) |
| Cumberland / `east_edge_out` | Front four: Mara, Tobin, Elsi, Orrin; target windows overlap from about 18:36 to 18:56, with all four overlapping about 18:44–18:48 | Shared coordinate, deliberately hidden |
| Cumberland / `east_edge_in` | Nella, Vey, Ione, Pell all target it 11:00–14:00; then subsets until 16:34 | Shared coordinate, deliberately hidden |
| Cumberland / `green_18_d` | Nella 14:00–15:00:38; Ione 16:30–17:30:38 | No target-window overlap; zero authored jitter |
| Cumberland / `green_18_c` | Vey 14:04–15:04:38; Pell 16:34–17:34:38 | No target-window overlap; zero authored jitter |

The three hidden shared destinations are not proof of visible stacking. Travel-to-hide thresholds, footsteps through common lanes, conversations and indoor visibility require a runtime check. Departure jitter is 0 for golfers; St Peters and clerk jitter cannot create a cross-person station conflict because their destinations are disjoint. Junior’s Wednesday adds ±10 game minutes of day jitter, but the mainland destinations are missing and no other authored routine uses them. Beach-day substitutions and service/vehicle figures are outside these authored RoutineDef windows.

## Concrete gaps and smallest follow-ups

1. **Rose’s post-sale address:** `home.st_peters_red_saltbox.ResidentMovesTo = station.st_peters.home_post_office` is absent from `AllStationIds` and `StPeters.unity`. `home_saltbox` is present; do not “repair” that correct row. The smallest declaration fix is to add the intended post-office home station to the region’s authoritative table, then have the scene owner derive and place its indoor station. A list-only addition cannot make it playable. `HomeDef` documents resident relocation as a future hook; no runtime reader currently applies `ResidentMovesTo`. Keep the approved destination; do not redirect it to the work counter just to pass validation.

2. **Wednesday mainland trip:** `routine.junior_poirier`, Wednesday entries 2 and 4 name `station.nine_mile_creek.bar_landing`; entry 3 names `station.nine_mile_creek.fuel_stop`. Neither has a declaration or a scene station. The bar crossing’s `ToStationId` also names the landing. Smallest data specification: two stable mainland station records and their lane/crossing connections, preserving the existing routine IDs and timetable. There is no honest playable data-only repair until the mainland scene owner supplies those locations and visitor presentation. Do not substitute a wholesale counter.

3. **Eight unplaced St Peters residents:** Angus, Theresa, Dougie, Rene, Paul, Monique, Lucie and Sam have only planned households, not runnable days. Smallest data addition: one RoutineDef per NPC with Home plus approved work/school/errand and leisure blocks, and separate slots for simultaneous housemates/pupils. Cream saltbox, harbour gable and white gable lack routine home stations. Scene/lane wiring and placements must follow through the region owner; do not borrow Rose’s house or Eileen’s desk for several people at once. The plan’s later jobs are not silently shipped by this audit.

4. **Claudette:** `npc.claudette_boudreau` / `NPCs/ClaudetteBoudreau.asset` exists and the placement builder names her, but the scene does not. She has no routine, home, work station or leisure station. Smallest data work: author her day after a home is assigned and give the general-store counter its own station ID. Placing her requires the scene owner; changing only the Def cannot fix absence.

5. **Four anchored mainland contacts:** `npc.hector_bernard`, `npc.wendell_arsenault`, `npc.nmc_harbourmaster`, `npc.nmc_yard` are placed but have no home or RoutineDef. Smallest data work: individual routine assets and home/work station assignments around their existing services; leisure slots only where the intended day calls for them. Homes are not selected by this audit. Scene stations and walking connections remain prerequisites.

6. **Eight golf visitors without homes:** Elsi, Ione, Mara, Nella, Orrin, Pell, Tobin and Vey have all-day Recreation loops. Smallest data specification: author a genuine off-course home destination and a Home block, retaining the golf visit. Do not retag the shared clubhouse as eight homes merely to silence the check. Choosing and presenting off-course homes is follow-up content work; their lack of a Work tag during a leisure visit is not a broken shop.

7. **No leisure blocks for Ginny or the two wholesale clerks:** their current cycles still cover 24 hours. If leisure is required for every resident, the smallest addition is a Recreation block at an approved existing compatible station with sufficient walking time and no simultaneous occupant. New destinations require region wiring; this audit does not choose their leisure activity.

8. **Empty and unbuilt regions:** Coddle Cove, East Water and South Water contain no NPC Def placements/routines; West Water has a RegionDef but no committed scene. These are scope facts, not missing residents to invent. Rice Point has two references to the same readable wreck Def and no living NPCs; duplicate object interaction points are not two villagers sharing a home.

## Validation and evidence boundaries

Before adding checks, inspected `NpcContentValidationTests.cs`, `StPetersRoutineContentTests.cs`, `RoutineCatalogTests.cs`, `RoutineCrossRegionContentTests.cs`, `HomeListingTests.cs` and `StoreClerkContentTests.cs`. Extended the existing **RoutineCatalogTests.cs**; the NPC station/placement extension is already in draft PR #1460 and its files are left alone. Its scene-reference checks and this audit’s pure declaration/home-activity checks have different inputs.

- `RoutineCatalog_AllDaysNameDeclaredStations_ExceptKnownGaps` checks ordinary entries and every weekly override against the destination region’s declarations. Its exact baseline contains the three Wednesday references above; a new gap or a repaired stale exception fails.
- `RoutineCatalog_PeopleHaveAHomeActivityOnEveryDay_ExceptKnownGaps` sweeps every Talk NPC, including those without routines. Its exact baseline is thirteen no-routine people plus eight recreational-only visitors. It requires a nonblank Home station in the routine’s own region for every authored day. Read objects are excluded by their interaction kind.
- Existing assertions are unchanged. The baseline records current debt; it does not certify a complete resident roster. Home existence in this check means an authored home activity, not property ownership or indoor furnishing.

Serialized evidence: St Peters table begins at scene line 88606, the two mainland tables at 438427 and 438762, Cumberland at 15345. Scene text contains 71 station rows, 16 walkers and 23 NPC references (including Ned’s letter and two references to the wreck). Searched all eight committed scene files and recursively checked 20 referenced prefab assets for NPC/routine/station components; none adds a hidden prefab resident. These are component-reference counts, not runtime activation or visual proof.

Both added C# tests passed against the extracted content in an isolated harness; 14 deliberate mutations were rejected, including ordinary/weekly missing stations, wrong regions, stale exceptions, lost homes and new people without homes. A new readable object remained exempt. The harness compiled the modified test file with stand-ins for the asset database, domain objects, station providers and assertions. This verifies the added checks against serialized input, not native imports or the real test runner. A separate text audit checked all 112 selected days and all 10,080 minutes of the week for shared target stations, and compared the declaration/scene station sets.

No Unity editor, scene loading, native EditMode/PlayMode suite, player build, or visual verification was run. The charter grants no editor lease. Native test compilation and execution remain for CI; isolated checks cannot establish scene import or reachability.
