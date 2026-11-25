# Main integration check — 2026-11-12

PASS — [ProjectSettings/ProjectVersion.txt:1](../../ProjectSettings/ProjectVersion.txt#L1) — all 36 enabled project assemblies compile at both audited revisions; main has zero compiler errors and 37 new warnings, listed individually below.

LIMIT — [Assets/Tests/EditMode/RigSpike/HiddenHarbours.Tests.RigSpike.EditMode.asmdef:20](../../Assets/Tests/EditMode/RigSpike/HiddenHarbours.Tests.RigSpike.EditMode.asmdef#L20) — the 37th assembly is intentionally excluded by `HH_JS_RIG_SPIKE` at both revisions; its required Jint and Acornima libraries are absent, so its two source files were inventoried but not compiled.

PASS — [Assets/Tests/EditMode/Fixtures/DefIdentityLedger.json:1](../../Assets/Tests/EditMode/Fixtures/DefIdentityLedger.json#L1) — all 1,918 baseline identities survive unchanged; main has 1,951 rows, 33 additions, no duplicate GUID, and no duplicate `(Type, Id)`.

PASS — [docs/design/nmc-key-scenes/key-scene-id-map.json:1](../../docs/design/nmc-key-scenes/key-scene-id-map.json#L1), [docs/design/st-peters-key-scenes/key-scene-id-map.json:1](../../docs/design/st-peters-key-scenes/key-scene-id-map.json#L1), [docs/design/st-peters-village-plan/village-id-map.json:1](../../docs/design/st-peters-village-plan/village-id-map.json#L1) — the three append-only maps retain all 167, 70, and 88 baseline mappings, respectively, with unique old/new ids.

CHANGE — [docs/art/rigs/boat-interiors-intake/s0-verdicts.json:244](../../docs/art/rigs/boat-interiors-intake/s0-verdicts.json#L244) — the mutable kit registry deliberately removes `sternTrawlerIsoRig` in #1115, from eight families to seven; its correction entry and `BoatInteriorS0LedgerTests` explain migration to the hull-walk format. This registry does not satisfy literal row retention; the append-only identity ledgers do.

PASS — [Assets/_Project/Code/Core/HiddenHarbours.Core.asmdef.meta:2](../../Assets/_Project/Code/Core/HiddenHarbours.Core.asmdef.meta#L2) — the repository-wide scan of all 9,952 tracked `.meta` files finds no orphan, invalid/duplicate GUID, or tracked importable Assets file missing its meta; directory metas were paired with tracked directories.

PASS — [Assets/_Project/Code/Core/HiddenHarbours.Core.asmdef:1](../../Assets/_Project/Code/Core/HiddenHarbours.Core.asmdef#L1) — all 186 references across 37 project asmdefs resolve; no feature-to-feature edge was added and all 3,028 tracked C# sources under Assets have an assembly owner.

GAP — [Assets/_Project/Code/Tools/Editor/PlayerBuild.cs:24](../../Assets/_Project/Code/Tools/Editor/PlayerBuild.cs#L24) — #1110 and #1116 edit the same explicit player-scene list, but no test directly exercises that build entry point; its six listed scene paths exist, and compilation alone does not validate a player export.

LIMIT — [Assets/Tests/EditMode/RegionSeamAuditTests.cs:283](../../Assets/Tests/EditMode/RegionSeamAuditTests.cs#L283) — the integrated tests deliberately pin South Water as unreachable from the normal start until a northern neighbour opens its half; synthetic arrival tests do not establish reciprocal authored travel.

No confirmed defect was found within the compiler, metadata, reference, and append-only identity checks. The risks below are inspection findings, not observed failing tests. No Unity editor, native test case, player build, plate, or visual acceptance ran in this audit.

## Scope and method

Audited main: `c9c9ef73dee59ee9b6e8c072ea98487992f82c2e`. Comparison baseline: `51d30dca25fa836696be476b8b134b9ff990a9bd`. The first-parent range contains **20 merged PRs**; every merge is listed below. The report is the only tracked change. The revisions were already available locally; no fetch, pull, rebase, source edit, asset edit, or editor launch was needed.

The pinned Unity 6000.5.0f1 toolchain supplied .NET SDK 8.0.318/Roslyn, engine reference assemblies, and Unity source generators/analyzers. The audit rebuilt repository assemblies in dependency order, deriving source ownership and repository references from each revision’s tracked asmdefs rather than stale generated project source lists. Every repository dependency used that revision’s freshly compiled DLL; no cached HiddenHarbours DLL supplied a dependency. Generated projects supplied external references and Windows editor/test defines. The Unity version and package manifest/lock are unchanged across the range. Cached external package DLLs were used; package recompilation, Unity import, IL post-processing, other platform/define configurations, and standalone-player compilation are outside this result.

Compiler settings include C# 9, warning level 4, the project’s `0169`/`USG0001` suppressions, and each asmdef’s unsafe setting. Baseline sources were extracted without switching away from main. Warnings were compared by repository path, diagnostic code, message, and occurrence count, ignoring line movement. Main emits 676 source/compiler-analyzer warnings versus 639 at baseline; 37 are new and none disappeared. An identical CS2023 response-file `/noconfig` invocation warning occurred once per assembly at both revisions and is excluded from those source totals. No assertion changed or additional warning was suppressed to obtain these results.

All 889 tracked JSON files parse with duplicate object keys rejected. The ledger discovery includes the Def ledger, three rename maps, the S0 kit registry, and 313 recipe records. Recipes are unchanged; the only removed JSON files are the three old stern-trawler sidecars retired in #1115. Runtime save-state ledgers and the external slot log are not committed identity ledgers and were not read or written. Def ids are unique within their declared Type, matching `DefIdentityContractTests`; this audit does not incorrectly require cross-type string uniqueness. Native AssetDatabase/TypeCache agreement remains unexecuted.

## Ten integration risks, ranked

Order reflects shared implementation/data, state transitions, breadth, and proof gaps; it is not a measured failure probability. “Indirect” explicitly means a consumer or shared-service interaction without the same edited path.

1. RISK — [Assets/_Project/Code/App/NpcPotBoats.cs:212](../../Assets/_Project/Code/App/NpcPotBoats.cs#L212) — PRs #1097, #1112, #1114: The same per-boat rebuild now constructs the catch and landing plans for three owners; a stale cache or shared deck/walk binding can duplicate or lose a crate across reload, midnight, or another boat. Run the mid-day reload, hand-to-crate, landing, and every-boat route cases together. Tests: `NpcPotBoatsPlayTests`, `NpcPotFleetPlayTests`, `NpcPotBoatsPlatePlayTests`.

2. RISK — [Assets/_Project/Code/World/Beach/BeachPlanning.cs:85](../../Assets/_Project/Code/World/Beach/BeachPlanning.cs#L85) — PRs #1095, #1111, #1113: Gatherings extend the same planner and activity data as wading and stone skipping; group allocation must still preserve the authored work/sleep day, dry positions, walking time, and a cut-short return. The plate PR consumes those shared files without editing them. Tests: `StPetersBeachLifeTests`, `BeachLifePlayTests`, `BeachGatheringPlayTests`, `BeachLifePlatePlayTests`.

3. RISK — [Assets/Tests/EditMode/RegionSeamAuditTests.cs:261](../../Assets/Tests/EditMode/RegionSeamAuditTests.cs#L261) — PRs #1107, #1116, #1119: The graph gained a region and then three outgoing doors while its northern neighbours remain unchanged. Run exact graph, named-key, coordinate, and live-arrival cases together: an individually green synthetic arrival does not establish reciprocal authored reachability. Tests: `RegionSeamAuditTests`, `SouthWaterTests`, `SouthWaterArrivalPlayTests`.

4. RISK — [Assets/_Project/Code/App/NpcPotDeck.cs:19](../../Assets/_Project/Code/App/NpcPotDeck.cs#L19) — PRs #1097, #1112, #1114: Deck stowage and landing share the same catch/Def and crate transitions. Conservation must include aboard, carried, counter, and sold states at identical boundaries, now for each fleet member; zero compilation errors cannot establish this handoff. Tests: `NpcPotDayCatchTests`, `NpcPotLandingTests`, `NpcPotBoatsPlayTests`, `NpcPotFleetTests`.

5. RISK — [Assets/_Project/Code/App/Editor/SouthWaterPlan.cs:20](../../Assets/_Project/Code/App/Editor/SouthWaterPlan.cs#L20) — PRs #1116, #1119: Approach floors, door bands, arrival offsets, and the three fishing-ground depth intervals changed together in the second PR. Validate committed scene versus plan and each ground against the actual bed; the arrival fixtures construct their terrain from the plan. Tests: `SouthWaterTests`, `FishingGroundContentTests`, `SouthWaterArrivalPlayTests`, `SouthWaterPlatePlayTests`.

6. RISK — [Assets/_Project/Resources/BeachLifeLibrary.asset:1](../../Assets/_Project/Resources/BeachLifeLibrary.asset#L1) — PRs #1095, #1111, #1113: Both feature PRs update the activity roster and existing beach assets. A retained duplicate or missing row, readiness flag, weather gate, or seasonal window can leave a gathering unused while isolated planner tests pass. Exercise the full-year roster and authored library checks. Tests: `StPetersBeachLifeTests`, `BeachGatheringPlayTests`, `BeachLifePlatePlayTests`.

7. RISK — [Assets/Tests/PlayMode/BeachGatheringPlayTests.cs:310](../../Assets/Tests/PlayMode/BeachGatheringPlayTests.cs#L310) — PRs #1111, #1113, #1114, #1098: Indirect integration risk: gathering, fleet, and foam now coexist and use clock/environment services. Local allocation/time budgets and singleton cleanup can interfere when fixtures run in one editor. These PRs do not all edit the same path; no combined performance or visual result is claimed. Tests: `BeachGatheringPlayTests`, `BeachLifePlatePlayTests`, `NpcPotFleetTests`, `NpcPotFleetPlayTests`, `HullContactFoamTests`.

8. RISK — [Assets/Tests/EditMode/RicePointTrawlerBerthTests.cs:59](../../Assets/Tests/EditMode/RicePointTrawlerBerthTests.cs#L59) — PRs #1115, #1117, #1116, #1119: Indirect asset-consumer risk: the rebuilt stern trawler retains identity while berth/approach tests and cabin journeys consume its authored hull, deck, and interior. Draught clearance does not prove the new geometry or cabin landing; run the numeric clearance and swapped-hull journey checks together. Tests: `RicePointTrawlerBerthTests`, `RicePointGroundTests`, `SouthWaterTests`, `BoatCabinOnASwappedHullPlayTests`, `TrawlersAtRestPlatePlayTests`.

9. RISK — [Assets/Tests/EditMode/Fixtures/DefIdentityLedger.json:1](../../Assets/Tests/EditMode/Fixtures/DefIdentityLedger.json#L1) — PRs #1095, #1108, #1101, #1111, #1103, #1114, #1116, #1117, #1115: Nine PRs append to one identity ledger. Static parse, duplicate, and baseline-retention checks pass, reducing this risk; the native TypeCache/AssetDatabase inventory must still agree with all authored objects, including new paint rows and structural Defs. Tests: `DefIdentityContractTests`.

10. RISK — [Assets/_Project/Code/Tools/Editor/PlayerBuild.cs:24](../../Assets/_Project/Code/Tools/Editor/PlayerBuild.cs#L24) — PRs #1110, #1116: The private build helper and new South Water scene edit the same explicit scene list. The listed tests cover EditorBuildSettings/scene topology, not PlayerBuild.BuildPlayer itself. Static review confirms all six selected scenes exist; no exported-player test or build was run. Tests: `SouthWaterTests`, `RegionSeamAuditTests`.

## Every merged PR and the shared-file test map

These are source-reviewed test selections, not instrumented coverage or executed cases. Exact file ownership is reproduced in the next table; consumer-only and indirect mappings are marked explicitly. The complete changed-test inventory follows separately.

| PR / integrated commit | Overlap and interpretation | Tests to run together |
|---|---|---|
| #1095 / `77a49ead5177` | Beach planner, Defs, existing activities and library; #1111; shared ledger with eight other PRs. | `StPetersBeachLifeTests`, `BeachLifePlayTests`, `BeachGatheringPlayTests`, `BeachLifePlatePlayTests`, `DefIdentityContractTests` |
| #1096 / `36a25b0e8814` | No exact edited-path overlap. Its counter tests are listed in the inventory; no shared-path coverage is inferred from feature proximity. | None attributable to an exact shared edited path. |
| #1097 / `c770b61891bb` | Pot boat presenter/deck/day Def and authored day; #1112 and #1114. | `NpcPotDayCatchTests`, `NpcPotLandingTests`, `NpcPotFleetTests`, `NpcPotBoatsPlayTests`, `NpcPotFleetPlayTests`, `NpcPotBoatsPlatePlayTests` |
| #1102 / `c3c3652fa9e1` | No exact edited-path overlap; SimulationReplayAuditTests adds explicit-input replay checks, not coverage of the new presenter lifecycle. | None attributable to an exact shared edited path. |
| #1108 / `9c9e3831f128` | Def type ledger only; nine PRs share it. Sail-race logic tests do not load that ledger. | `DefIdentityContractTests` |
| #1107 / `679c564c83a4` | RegionSeamAuditTests and graph audit document; #1116 and #1119 expand the exact graph expectations. | `RegionSeamAuditTests`, `SouthWaterTests`, `SouthWaterArrivalPlayTests` |
| #1101 / `1406e6c53861` | Def ledger and .gitattributes; #1115 also changes .gitattributes. No direct .gitattributes test identified; figure/tree partial classes are inventoried under their actual class name. | `DefIdentityContractTests` |
| #1105 / `4ee6e23f2065` | No exact edited-path overlap. Its seven fishing-boundary classes are listed below; no authored South Water/trawl coverage is implied. | None attributable to an exact shared edited path. |
| #1111 / `88f37b910a05` | Same beach planner/Defs/assets/library and StPetersBeachLifeTests as #1095; #1113 adds a consumer; shared ledger. | `StPetersBeachLifeTests`, `BeachLifePlayTests`, `BeachGatheringPlayTests`, `BeachLifePlatePlayTests`, `DefIdentityContractTests` |
| #1098 / `2a65bee8b1c1` | No exact edited-path overlap. Foam changes GameServices/configuration used by other fixtures; indirect coexistence/cleanup risk only (rank 7). | `HullContactFoamTests`, `WakeCentrePhotographPlayTests` |
| #1112 / `05befa97914a` | Pot boat/deck/day Def and single-boat PlayMode tests with #1097; presenter/plate fixture with #1114. | `NpcPotDayCatchTests`, `NpcPotLandingTests`, `NpcPotBoatsPlayTests`, `NpcPotFleetTests`, `NpcPotFleetPlayTests`, `NpcPotBoatsPlatePlayTests` |
| #1110 / `c34ad86a41d4` | PlayerBuild.cs with #1116. No direct build-entry-point test; adjacent EditorBuildSettings/scene checks do not close that gap. | `SouthWaterTests`, `RegionSeamAuditTests` |
| #1113 / `cf933bb264a1` | No exact edited-path overlap; BeachLifePlatePlayTests consumes the beach planner/Defs/library edited by #1095/#1111. | `BeachLifePlatePlayTests`, `StPetersBeachLifeTests`, `BeachGatheringPlayTests` |
| #1103 / `5fda873dfe7a` | Def ledger only; H7 asset/row tests additionally inspect its own authored content, not other PRs’ append operations. | `DefIdentityContractTests`, `NmcH7AssetImportTests`, `NmcH7ContentValidationTests`, `NmcH7RowTests` |
| #1114 / `9bd82bb9d2d0` | NpcPotBoats presenter with #1097/#1112, pot plate fixture with #1112, and shared ledger. | `NpcPotFleetTests`, `NpcPotFleetPlayTests`, `NpcPotBoatsPlayTests`, `NpcPotBoatsPlatePlayTests`, `NpcPotLandingTests`, `DefIdentityContractTests` |
| #1109 / `132473efeecf` | No exact edited-path overlap. TrawlSchoolFieldBridgeTests uses synthetic species/sample inputs and is not proof that authored new grounds are eligible. | None attributable to an exact shared edited path. |
| #1116 / `3504d3ae7d50` | South Water builder/plan/scene, ground Def/assets and tests with #1119; region audit with #1107; PlayerBuild with #1110; shared ledger. | `SouthWaterTests`, `FishingGroundContentTests`, `RegionSeamAuditTests`, `SouthWaterPlatePlayTests`, `SouthWaterArrivalPlayTests`, `DefIdentityContractTests` |
| #1117 / `11f95bb28be4` | Exact overlap is the Def ledger. Rice Point tests additionally consume the trawler assets rebuilt by #1115 and neighbour data used by South Water (indirect rank 8). | `DefIdentityContractTests`, `RicePointTrawlerBerthTests`, `RicePointGroundTests`, `RicePointRegionTests`, `SouthWaterTests` |
| #1119 / `12bc04b19a2e` | South Water builder/plan/scene and ground Def/assets/tests with #1116; region audit test/document with #1107. | `SouthWaterTests`, `FishingGroundContentTests`, `RegionSeamAuditTests`, `SouthWaterArrivalPlayTests`, `SouthWaterPlatePlayTests` |
| #1115 / `c9c9ef73dee5` | Exact overlaps are the Def ledger and .gitattributes. Its rebuilt assets are consumed by Rice Point/South Water and cabin tests (indirect rank 8); no direct .gitattributes test identified. | `DefIdentityContractTests`, `RicePointTrawlerBerthTests`, `SouthWaterTests`, `BoatCabinOnASwappedHullPlayTests`, `TrawlersAtRestPlatePlayTests` |

## All 35 shared paths

A path is shared only when at least two of the 20 integrated first-parent diffs edit it. This includes shared tests and documents, which are not themselves evidence of runtime interaction.

| File at audited main | PRs editing it |
|---|---|
| [.gitattributes:1](../../.gitattributes#L1) | #1101, #1115 |
| [Assets/Tests/EditMode/FishingGroundContentTests.cs:1](../../Assets/Tests/EditMode/FishingGroundContentTests.cs#L1) | #1116, #1119 |
| [Assets/Tests/EditMode/Fixtures/DefIdentityLedger.json:1](../../Assets/Tests/EditMode/Fixtures/DefIdentityLedger.json#L1) | #1095, #1108, #1101, #1111, #1103, #1114, #1116, #1117, #1115 |
| [Assets/Tests/EditMode/RegionSeamAuditTests.cs:1](../../Assets/Tests/EditMode/RegionSeamAuditTests.cs#L1) | #1107, #1116, #1119 |
| [Assets/Tests/EditMode/SouthWaterTests.cs:1](../../Assets/Tests/EditMode/SouthWaterTests.cs#L1) | #1116, #1119 |
| [Assets/Tests/EditMode/StPetersBeachLifeTests.cs:1](../../Assets/Tests/EditMode/StPetersBeachLifeTests.cs#L1) | #1095, #1111 |
| [Assets/Tests/PlayMode/NpcPotBoatsPlatePlayTests.cs:1](../../Assets/Tests/PlayMode/NpcPotBoatsPlatePlayTests.cs#L1) | #1112, #1114 |
| [Assets/Tests/PlayMode/NpcPotBoatsPlayTests.cs:1](../../Assets/Tests/PlayMode/NpcPotBoatsPlayTests.cs#L1) | #1097, #1112 |
| [Assets/_Project/Code/App/Editor/SouthWaterBuilder.cs:1](../../Assets/_Project/Code/App/Editor/SouthWaterBuilder.cs#L1) | #1116, #1119 |
| [Assets/_Project/Code/App/Editor/SouthWaterPlan.cs:1](../../Assets/_Project/Code/App/Editor/SouthWaterPlan.cs#L1) | #1116, #1119 |
| [Assets/_Project/Code/App/NpcPotBoats.cs:1](../../Assets/_Project/Code/App/NpcPotBoats.cs#L1) | #1097, #1112, #1114 |
| [Assets/_Project/Code/App/NpcPotDeck.cs:1](../../Assets/_Project/Code/App/NpcPotDeck.cs#L1) | #1097, #1112 |
| [Assets/_Project/Code/Fishing/FishingGroundDef.cs:1](../../Assets/_Project/Code/Fishing/FishingGroundDef.cs#L1) | #1116, #1119 |
| [Assets/_Project/Code/Fishing/NpcPotDayDef.cs:1](../../Assets/_Project/Code/Fishing/NpcPotDayDef.cs#L1) | #1097, #1112 |
| [Assets/_Project/Code/Tools/Editor/PlayerBuild.cs:1](../../Assets/_Project/Code/Tools/Editor/PlayerBuild.cs#L1) | #1110, #1116 |
| [Assets/_Project/Code/World/Beach/BeachActivityDef.cs:1](../../Assets/_Project/Code/World/Beach/BeachActivityDef.cs#L1) | #1095, #1111 |
| [Assets/_Project/Code/World/Beach/BeachDef.cs:1](../../Assets/_Project/Code/World/Beach/BeachDef.cs#L1) | #1095, #1111 |
| [Assets/_Project/Code/World/Beach/BeachPlanning.cs:1](../../Assets/_Project/Code/World/Beach/BeachPlanning.cs#L1) | #1095, #1111 |
| [Assets/_Project/Data/Beaches/BeachClamDig.asset:1](../../Assets/_Project/Data/Beaches/BeachClamDig.asset#L1) | #1095, #1111 |
| [Assets/_Project/Data/Beaches/BeachSkipStones.asset:1](../../Assets/_Project/Data/Beaches/BeachSkipStones.asset#L1) | #1095, #1111 |
| [Assets/_Project/Data/Beaches/BeachSwim.asset:1](../../Assets/_Project/Data/Beaches/BeachSwim.asset#L1) | #1095, #1111 |
| [Assets/_Project/Data/Beaches/BeachTideLineWalk.asset:1](../../Assets/_Project/Data/Beaches/BeachTideLineWalk.asset#L1) | #1095, #1111 |
| [Assets/_Project/Data/Beaches/BeachWade.asset:1](../../Assets/_Project/Data/Beaches/BeachWade.asset#L1) | #1095, #1111 |
| [Assets/_Project/Data/Beaches/BeachWatchTheWater.asset:1](../../Assets/_Project/Data/Beaches/BeachWatchTheWater.asset#L1) | #1095, #1111 |
| [Assets/_Project/Data/Beaches/BeachWinterWalk.asset:1](../../Assets/_Project/Data/Beaches/BeachWinterWalk.asset#L1) | #1095, #1111 |
| [Assets/_Project/Data/Beaches/StPetersBarHead.asset:1](../../Assets/_Project/Data/Beaches/StPetersBarHead.asset#L1) | #1095, #1111 |
| [Assets/_Project/Data/FishingGrounds/SouthWaterBank.asset:1](../../Assets/_Project/Data/FishingGrounds/SouthWaterBank.asset#L1) | #1116, #1119 |
| [Assets/_Project/Data/FishingGrounds/SouthWaterDeepMud.asset:1](../../Assets/_Project/Data/FishingGrounds/SouthWaterDeepMud.asset#L1) | #1116, #1119 |
| [Assets/_Project/Data/FishingGrounds/SouthWaterShelf.asset:1](../../Assets/_Project/Data/FishingGrounds/SouthWaterShelf.asset#L1) | #1116, #1119 |
| [Assets/_Project/Data/Traps/NmcDoironYvettePotDay.asset:1](../../Assets/_Project/Data/Traps/NmcDoironYvettePotDay.asset#L1) | #1097, #1112 |
| [Assets/_Project/Resources/BeachLifeLibrary.asset:1](../../Assets/_Project/Resources/BeachLifeLibrary.asset#L1) | #1095, #1111 |
| [Assets/_Project/Scenes/SouthWater.unity:1](../../Assets/_Project/Scenes/SouthWater.unity#L1) | #1116, #1119 |
| [docs/design/fishing-methods.md:1](../../docs/design/fishing-methods.md#L1) | #1097, #1112, #1114 |
| [docs/design/npcs-and-routines.md:1](../../docs/design/npcs-and-routines.md#L1) | #1095, #1111 |
| [docs/design/region-seam-audit.md:1](../../docs/design/region-seam-audit.md#L1) | #1107, #1119 |

## Every added or changed test class

Roslyn parsed all test sources with the editor/test defines. Comparing normalized class text against baseline excludes unchanged companion classes; namespace and partial declarations are retained. There are **62 distinct changed test classes, 63 changed declarations, and 65 changed test-source files**. Two files contain only support/CLI code. The two figure/tree files extend one existing `WharfBuildingPass2PlatePlayTests`; the foam file extends the existing `WakeCentrePhotographPlayTests`. File names are not used as guessed class names. No changed test source was found outside Assets/Tests.

| Class (fully qualified) | Change | File and line | PRs touching the file |
|---|---|---|---|
| `HiddenHarbours.Tests.BoatCabinEntryLandingTests` | modified | [Assets/Tests/EditMode/BoatCabinEntryLandingTests.cs:14](../../Assets/Tests/EditMode/BoatCabinEntryLandingTests.cs#L14) | #1115 |
| `HiddenHarbours.Tests.EditMode.BoatInteriorPlacementTests` | modified | [Assets/Tests/EditMode/BoatInteriorPlacementTests.cs:24](../../Assets/Tests/EditMode/BoatInteriorPlacementTests.cs#L24) | #1115 |
| `HiddenHarbours.Tests.EditMode.CatchResolverBoundaryTests` | added | [Assets/Tests/EditMode/CatchResolverBoundaryTests.cs:11](../../Assets/Tests/EditMode/CatchResolverBoundaryTests.cs#L11) | #1105 |
| `HiddenHarbours.Tests.EditMode.FishingGroundContentTests` | added | [Assets/Tests/EditMode/FishingGroundContentTests.cs:18](../../Assets/Tests/EditMode/FishingGroundContentTests.cs#L18) | #1116, #1119 |
| `HiddenHarbours.Tests.EditMode.FishingSequenceBoundaryTests` | added | [Assets/Tests/EditMode/FishingSequenceBoundaryTests.cs:7](../../Assets/Tests/EditMode/FishingSequenceBoundaryTests.cs#L7) | #1105 |
| `HiddenHarbours.Tests.EditMode.FishingSupplyContractTests` | added | [Assets/Tests/EditMode/FishingSupplyContractTests.cs:8](../../Assets/Tests/EditMode/FishingSupplyContractTests.cs#L8) | #1105 |
| `HiddenHarbours.Tests.EditMode.HullContactFoamTests` | added | [Assets/Tests/EditMode/HullContactFoamTests.cs:14](../../Assets/Tests/EditMode/HullContactFoamTests.cs#L14) | #1098 |
| `HiddenHarbours.Tests.EditMode.LonglineCatchBoundaryTests` | added | [Assets/Tests/EditMode/LonglineCatchBoundaryTests.cs:8](../../Assets/Tests/EditMode/LonglineCatchBoundaryTests.cs#L8) | #1105 |
| `HiddenHarbours.Tests.EditMode.NmcCarriedCounterTests` | added | [Assets/Tests/EditMode/NmcCarriedCounterTests.cs:28](../../Assets/Tests/EditMode/NmcCarriedCounterTests.cs#L28) | #1096 |
| `HiddenHarbours.Tests.EditMode.NmcH7AssetImportTests` | added | [Assets/Tests/EditMode/NmcH7AssetImportTests.cs:10](../../Assets/Tests/EditMode/NmcH7AssetImportTests.cs#L10) | #1103 |
| `HiddenHarbours.Tests.EditMode.NmcH7ContentValidationTests` | added | [Assets/Tests/EditMode/NmcH7ContentValidationTests.cs:15](../../Assets/Tests/EditMode/NmcH7ContentValidationTests.cs#L15) | #1103 |
| `HiddenHarbours.Tests.EditMode.NmcH7RowTests` | added | [Assets/Tests/EditMode/NmcH7RowTests.cs:14](../../Assets/Tests/EditMode/NmcH7RowTests.cs#L14) | #1103 |
| `HiddenHarbours.Tests.EditMode.NpcPotDayCatchTests` | added | [Assets/Tests/EditMode/NpcPotDayCatchTests.cs:15](../../Assets/Tests/EditMode/NpcPotDayCatchTests.cs#L15) | #1097 |
| `HiddenHarbours.Tests.EditMode.NpcPotFleetTests` | added | [Assets/Tests/EditMode/NpcPotFleetTests.cs:18](../../Assets/Tests/EditMode/NpcPotFleetTests.cs#L18) | #1114 |
| `HiddenHarbours.Tests.EditMode.NpcPotLandingTests` | added | [Assets/Tests/EditMode/NpcPotLandingTests.cs:16](../../Assets/Tests/EditMode/NpcPotLandingTests.cs#L16) | #1112 |
| `HiddenHarbours.Tests.EditMode.PassiveGearBoundaryTests` | added | [Assets/Tests/EditMode/PassiveGearBoundaryTests.cs:8](../../Assets/Tests/EditMode/PassiveGearBoundaryTests.cs#L8) | #1105 |
| `HiddenHarbours.Tests.EditMode.RegionSeamAuditTests` | added | [Assets/Tests/EditMode/RegionSeamAuditTests.cs:16](../../Assets/Tests/EditMode/RegionSeamAuditTests.cs#L16) | #1107, #1116, #1119 |
| `HiddenHarbours.Tests.EditMode.RicePointGroundTests` | modified | [Assets/Tests/EditMode/RicePointGroundTests.cs:12](../../Assets/Tests/EditMode/RicePointGroundTests.cs#L12) | #1117 |
| `HiddenHarbours.Tests.EditMode.RicePointRegionTests` | modified | [Assets/Tests/EditMode/RicePointRegionTests.cs:17](../../Assets/Tests/EditMode/RicePointRegionTests.cs#L17) | #1117 |
| `HiddenHarbours.Tests.EditMode.RicePointTrawlerBerthTests` | added | [Assets/Tests/EditMode/RicePointTrawlerBerthTests.cs:20](../../Assets/Tests/EditMode/RicePointTrawlerBerthTests.cs#L20) | #1117 |
| `HiddenHarbours.Tests.EditMode.SailRaceCourseTests` | added | [Assets/Tests/EditMode/SailRaceCourseTests.cs:40](../../Assets/Tests/EditMode/SailRaceCourseTests.cs#L40) | #1108 |
| `HiddenHarbours.Tests.EditMode.SailRaceNpcTests` | added | [Assets/Tests/EditMode/SailRaceNpcTests.cs:11](../../Assets/Tests/EditMode/SailRaceNpcTests.cs#L11) | #1108 |
| `HiddenHarbours.Tests.EditMode.SailRaceRightOfWayTests` | added | [Assets/Tests/EditMode/SailRaceRightOfWayTests.cs:8](../../Assets/Tests/EditMode/SailRaceRightOfWayTests.cs#L8) | #1108 |
| `HiddenHarbours.Tests.EditMode.SailRaceRoundingTests` | added | [Assets/Tests/EditMode/SailRaceRoundingTests.cs:7](../../Assets/Tests/EditMode/SailRaceRoundingTests.cs#L7) | #1108 |
| `HiddenHarbours.Tests.EditMode.SailRaceTrackTests` | added | [Assets/Tests/EditMode/SailRaceTrackTests.cs:8](../../Assets/Tests/EditMode/SailRaceTrackTests.cs#L8) | #1108 |
| `HiddenHarbours.Tests.EditMode.SimulationReplayAuditTests` | added | [Assets/Tests/EditMode/SimulationReplayAuditTests.cs:16](../../Assets/Tests/EditMode/SimulationReplayAuditTests.cs#L16) | #1102 |
| `HiddenHarbours.Tests.EditMode.SouthWaterTests` | added | [Assets/Tests/EditMode/SouthWaterTests.cs:17](../../Assets/Tests/EditMode/SouthWaterTests.cs#L17) | #1116, #1119 |
| `HiddenHarbours.Tests.EditMode.SpeciesCatchItemFactoryTests` | added | [Assets/Tests/EditMode/SpeciesCatchItemFactoryTests.cs:10](../../Assets/Tests/EditMode/SpeciesCatchItemFactoryTests.cs#L10) | #1105 |
| `HiddenHarbours.Tests.EditMode.StPetersBeachLifeTests` | modified | [Assets/Tests/EditMode/StPetersBeachLifeTests.cs:25](../../Assets/Tests/EditMode/StPetersBeachLifeTests.cs#L25) | #1095, #1111 |
| `HiddenHarbours.Tests.EditMode.StartingBaitOnceTests` | added | [Assets/Tests/EditMode/StartingBaitOnceTests.cs:9](../../Assets/Tests/EditMode/StartingBaitOnceTests.cs#L9) | #1105 |
| `HiddenHarbours.Tests.EditMode.StoreWiringTests` | modified | [Assets/Tests/EditMode/StoreWiringTests.cs:37](../../Assets/Tests/EditMode/StoreWiringTests.cs#L37) | #1096 |
| `HiddenHarbours.Tests.EditMode.TrawlDeckBatchTests` | added | [Assets/Tests/EditMode/TrawlDeckBatchTests.cs:7](../../Assets/Tests/EditMode/TrawlDeckBatchTests.cs#L7) | #1109 |
| `HiddenHarbours.Tests.EditMode.TrawlOperationTests` | added | [Assets/Tests/EditMode/TrawlOperationTests.cs:7](../../Assets/Tests/EditMode/TrawlOperationTests.cs#L7) | #1109 |
| `HiddenHarbours.Tests.EditMode.TrawlSchoolFieldBridgeTests` | added | [Assets/Tests/EditMode/TrawlSchoolFieldBridgeTests.cs:8](../../Assets/Tests/EditMode/TrawlSchoolFieldBridgeTests.cs#L8) | #1109 |
| `HiddenHarbours.Tests.EditMode.TrawlTowSolverTests` | added | [Assets/Tests/EditMode/TrawlTowSolverTests.cs:9](../../Assets/Tests/EditMode/TrawlTowSolverTests.cs#L9) | #1109 |
| `HiddenHarbours.Tests.PlayMode.BeachGatheringPlayTests` | added | [Assets/Tests/PlayMode/BeachGatheringPlayTests.cs:23](../../Assets/Tests/PlayMode/BeachGatheringPlayTests.cs#L23) | #1111 |
| `HiddenHarbours.Tests.PlayMode.BeachLifePlatePlayTests` | added | [Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs:29](../../Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs#L29) | #1113 |
| `HiddenHarbours.Tests.PlayMode.BeachLifePlayTests` | modified | [Assets/Tests/PlayMode/BeachLifePlayTests.cs:22](../../Assets/Tests/PlayMode/BeachLifePlayTests.cs#L22) | #1095 |
| `HiddenHarbours.Tests.PlayMode.BoatCabinJourneyPlayTests` | modified | [Assets/Tests/PlayMode/BoatCabinJourneyPlayTests.cs:53](../../Assets/Tests/PlayMode/BoatCabinJourneyPlayTests.cs#L53) | #1115 |
| `HiddenHarbours.Tests.PlayMode.BoatCabinOnASwappedHullPlayTests` | modified | [Assets/Tests/PlayMode/BoatCabinOnASwappedHullPlayTests.cs:39](../../Assets/Tests/PlayMode/BoatCabinOnASwappedHullPlayTests.cs#L39) | #1115 |
| `HiddenHarbours.Tests.PlayMode.NmcCarriedCounterPlayTests` | added | [Assets/Tests/PlayMode/NmcCarriedCounterPlayTests.cs:30](../../Assets/Tests/PlayMode/NmcCarriedCounterPlayTests.cs#L30) | #1096 |
| `HiddenHarbours.Tests.PlayMode.NpcPotBoatsPlatePlayTests` | added | [Assets/Tests/PlayMode/NpcPotBoatsPlatePlayTests.cs:17](../../Assets/Tests/PlayMode/NpcPotBoatsPlatePlayTests.cs#L17) | #1112, #1114 |
| `HiddenHarbours.Tests.PlayMode.NpcPotBoatsPlayTests` | modified | [Assets/Tests/PlayMode/NpcPotBoatsPlayTests.cs:21](../../Assets/Tests/PlayMode/NpcPotBoatsPlayTests.cs#L21) | #1097, #1112 |
| `HiddenHarbours.Tests.PlayMode.NpcPotFleetPlayTests` | added | [Assets/Tests/PlayMode/NpcPotFleetPlayTests.cs:22](../../Assets/Tests/PlayMode/NpcPotFleetPlayTests.cs#L22) | #1114 |
| `HiddenHarbours.Tests.PlayMode.RicePointTrawlerBerthPlatePlayTests` | added | [Assets/Tests/PlayMode/RicePointTrawlerBerthPlatePlayTests.cs:24](../../Assets/Tests/PlayMode/RicePointTrawlerBerthPlatePlayTests.cs#L24) | #1117 |
| `HiddenHarbours.Tests.PlayMode.SouthWaterArrivalPlayTests` | added | [Assets/Tests/PlayMode/SouthWaterArrivalPlayTests.cs:23](../../Assets/Tests/PlayMode/SouthWaterArrivalPlayTests.cs#L23) | #1119 |
| `HiddenHarbours.Tests.PlayMode.SouthWaterPlatePlayTests` | added | [Assets/Tests/PlayMode/SouthWaterPlatePlayTests.cs:30](../../Assets/Tests/PlayMode/SouthWaterPlatePlayTests.cs#L30) | #1116 |
| `HiddenHarbours.Tests.PlayMode.TrawlersAtRestPlatePlayTests` | added | [Assets/Tests/PlayMode/TrawlersAtRestPlatePlayTests.cs:23](../../Assets/Tests/PlayMode/TrawlersAtRestPlatePlayTests.cs#L23) | #1115 |
| `HiddenHarbours.Tests.PlayMode.WakeCentrePhotographPlayTests` | modified (new partial) | [Assets/Tests/PlayMode/WakeCentrePhotographPlayTests.WholeHull.cs:22](../../Assets/Tests/PlayMode/WakeCentrePhotographPlayTests.WholeHull.cs#L22) | #1098 |
| `HiddenHarbours.Tests.PlayMode.WharfBuildingPass2PlatePlayTests` | modified (new partial) | [Assets/Tests/PlayMode/NextIterationFigurePlatePlayTests.cs:21](../../Assets/Tests/PlayMode/NextIterationFigurePlatePlayTests.cs#L21) | #1101 |
| `HiddenHarbours.Tests.PlayMode.WharfBuildingPass2PlatePlayTests` | modified (new partial) | [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:23](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L23) | #1101 |
| `HiddenHarbours.Tests.RigBaking.BoatInteriorDefContractAgreementTests` | modified | [Assets/Tests/EditMode/RigBaking/BoatInteriorAgreementTests.cs:36](../../Assets/Tests/EditMode/RigBaking/BoatInteriorAgreementTests.cs#L36) | #1115 |
| `HiddenHarbours.Tests.RigBaking.BoatInteriorDefShapeTests` | modified | [Assets/Tests/EditMode/RigBaking/BoatInteriorDefShapeTests.cs:24](../../Assets/Tests/EditMode/RigBaking/BoatInteriorDefShapeTests.cs#L24) | #1115 |
| `HiddenHarbours.Tests.RigBaking.BoatInteriorS0LedgerTests` | modified | [Assets/Tests/EditMode/RigBaking/BoatInteriorS0LedgerTests.cs:20](../../Assets/Tests/EditMode/RigBaking/BoatInteriorS0LedgerTests.cs#L20) | #1115 |
| `HiddenHarbours.Tests.RigBaking.BoatInteriorSheetTests` | modified | [Assets/Tests/EditMode/RigBaking/BoatInteriorSheetTests.cs:35](../../Assets/Tests/EditMode/RigBaking/BoatInteriorSheetTests.cs#L35) | #1115 |
| `HiddenHarbours.Tests.RigBaking.BoatWindowPaneTests` | modified | [Assets/Tests/EditMode/RigBaking/BoatWindowPaneTests.cs:29](../../Assets/Tests/EditMode/RigBaking/BoatWindowPaneTests.cs#L29) | #1115 |
| `HiddenHarbours.Tests.RigBaking.CharacterSkinBakeGuardTests` | modified | [Assets/Tests/EditMode/RigBaking/CharacterSkinBakeGuardTests.cs:43](../../Assets/Tests/EditMode/RigBaking/CharacterSkinBakeGuardTests.cs#L43) | #1101 |
| `HiddenHarbours.Tests.RigBaking.FullMeshInteriorRenderTests` | modified | [Assets/Tests/EditMode/RigBaking/FullMeshInteriorRenderTests.cs:35](../../Assets/Tests/EditMode/RigBaking/FullMeshInteriorRenderTests.cs#L35) | #1115 |
| `HiddenHarbours.Tests.RigBaking.HullLevelTagBakeTests` | modified | [Assets/Tests/EditMode/RigBaking/HullLevelTagBakeTests.cs:41](../../Assets/Tests/EditMode/RigBaking/HullLevelTagBakeTests.cs#L41) | #1115 |
| `HiddenHarbours.Tests.RigBaking.HullMaterialDitherTests` | modified | [Assets/Tests/EditMode/RigBaking/HullMaterialDitherTests.cs:18](../../Assets/Tests/EditMode/RigBaking/HullMaterialDitherTests.cs#L18) | #1115 |
| `HiddenHarbours.Tests.RigBaking.NoHullReadsBothLayoutsTests` | modified | [Assets/Tests/EditMode/RigBaking/NoHullReadsBothLayoutsTests.cs:11](../../Assets/Tests/EditMode/RigBaking/NoHullReadsBothLayoutsTests.cs#L11) | #1115 |
| `HiddenHarbours.Tests.RigBaking.RigCatalogAssemblyTests` | modified | [Assets/Tests/EditMode/RigBaking/RigCatalogAssemblyTests.cs:39](../../Assets/Tests/EditMode/RigBaking/RigCatalogAssemblyTests.cs#L39) | #1101 |
| `HiddenHarbours.Tests.RigBaking.RigMeshVariantExtractionTests` | modified | [Assets/Tests/EditMode/RigBaking/RigMeshVariantExtractionTests.cs:30](../../Assets/Tests/EditMode/RigBaking/RigMeshVariantExtractionTests.cs#L30) | #1115 |

Support-only additions: [Assets/Tests/EditMode/SailRaceVerificationCli.cs:1](../../Assets/Tests/EditMode/SailRaceVerificationCli.cs#L1), [Assets/Tests/EditMode/TrawlTestData.cs:1](../../Assets/Tests/EditMode/TrawlTestData.cs#L1). Unchanged companion classes excluded: `BoatInteriorCellHandednessTests` and `HullDitherUploadTests`.

## Compilation results

Runtime assemblies were built in the Windows editor configuration. Categories below distinguish production, editor, shared test support, EditMode, and PlayMode source ownership; they do not imply execution. Every enabled assembly passes on both revisions. The disabled experimental assembly is shown explicitly.

| Assembly | Category | Sources: baseline → main | Result |
|---|---|---:|---|
| `HiddenHarbours.App` | runtime | 34 → 36 | PASS / PASS |
| `HiddenHarbours.App.Editor` | editor | 193 → 196 | PASS / PASS |
| `HiddenHarbours.Art` | runtime | 204 → 206 | PASS / PASS |
| `HiddenHarbours.Art.Editor` | editor | 93 → 93 | PASS / PASS |
| `HiddenHarbours.Audio` | runtime | 11 → 11 | PASS / PASS |
| `HiddenHarbours.Audio.Editor` | editor | 2 → 2 | PASS / PASS |
| `HiddenHarbours.Boats` | runtime | 97 → 103 | PASS / PASS |
| `HiddenHarbours.Core` | runtime | 285 → 287 | PASS / PASS |
| `HiddenHarbours.Economy` | runtime | 41 → 42 | PASS / PASS |
| `HiddenHarbours.Environment` | runtime | 7 → 7 | PASS / PASS |
| `HiddenHarbours.Fishing` | runtime | 74 → 82 | PASS / PASS |
| `HiddenHarbours.Player` | runtime | 39 → 39 | PASS / PASS |
| `HiddenHarbours.Tests.Art.EditMode` | EditMode | 209 → 209 | PASS / PASS |
| `HiddenHarbours.Tests.Audio.EditMode` | EditMode | 8 → 8 | PASS / PASS |
| `HiddenHarbours.Tests.Audio.PlayMode` | PlayMode | 1 → 1 | PASS / PASS |
| `HiddenHarbours.Tests.Economy.EditMode` | EditMode | 18 → 18 | PASS / PASS |
| `HiddenHarbours.Tests.EditMode` | EditMode | 646 → 677 | PASS / PASS |
| `HiddenHarbours.Tests.GoodsModel.EditMode` | EditMode | 6 → 6 | PASS / PASS |
| `HiddenHarbours.Tests.LandedCatch.EditMode` | EditMode | 1 → 1 | PASS / PASS |
| `HiddenHarbours.Tests.PlayMode` | PlayMode | 250 → 262 | PASS / PASS |
| `HiddenHarbours.Tests.RigBaking.EditMode` | EditMode | 163 → 163 | PASS / PASS |
| `HiddenHarbours.Tests.RigSpike.EditMode` | EditMode | 2 → 2 | excluded at both revisions; missing opt-in symbol and optional libraries |
| `HiddenHarbours.Tests.RigStudio.EditMode` | EditMode | 5 → 5 | PASS / PASS |
| `HiddenHarbours.Tests.Sell.EditMode` | EditMode | 2 → 2 | PASS / PASS |
| `HiddenHarbours.Tests.Support` | test support | 4 → 4 | PASS / PASS |
| `HiddenHarbours.Tests.UI.EditMode` | EditMode | 36 → 36 | PASS / PASS |
| `HiddenHarbours.Tests.World.EditMode` | EditMode | 38 → 38 | PASS / PASS |
| `HiddenHarbours.Tools.Editor` | editor | 15 → 16 | PASS / PASS |
| `HiddenHarbours.Tools.GoodsModel` | editor | 7 → 7 | PASS / PASS |
| `HiddenHarbours.Tools.JsEngine.Editor` | editor | 1 → 1 | PASS / PASS |
| `HiddenHarbours.Tools.LandedCatch.Editor` | editor | 2 → 2 | PASS / PASS |
| `HiddenHarbours.Tools.RigBaking.Editor` | editor | 194 → 196 | PASS / PASS |
| `HiddenHarbours.Tools.RigStudio.Editor` | editor | 8 → 8 | PASS / PASS |
| `HiddenHarbours.UI` | runtime | 88 → 88 | PASS / PASS |
| `HiddenHarbours.UI.Editor` | editor | 8 → 8 | PASS / PASS |
| `HiddenHarbours.Vehicles` | runtime | 15 → 15 | PASS / PASS |
| `HiddenHarbours.World` | runtime | 148 → 151 | PASS / PASS |

Main compiled **3,026 source files** versus **2,953** at baseline across 12 runtime, 10 editor, 11 EditMode, two PlayMode, and one test-support assembly. Two additional spike sources remain excluded. Zero test cases ran.

## All 37 new warnings

Each row is one diagnostic at audited main, including its column when two warnings share a line. CS0649 on serialized or JSON-populated fields is not by itself evidence of an uninitialized runtime value. CS0618 identifies deprecated API use, not a failed call. Existing warnings are excluded by the baseline comparison described above.

| Location | Diagnostic | Message |
|---|---|---|
| [Assets/Tests/PlayMode/BeachGatheringPlayTests.cs:46](../../Assets/Tests/PlayMode/BeachGatheringPlayTests.cs#L46), column 26 | CS0649 | Field 'BeachGatheringPlayTests.FakeSea.Water' is never assigned to, and will always have its default value 0 |
| [Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs:86](../../Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs#L86), column 37 | CS0618 | 'Object.FindObjectsByType<T>(FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType<T>() or FindObjectsByType<T>(FindObjectsInactive) instead. InstanceID will be replaced in the future with EntityId and previous sort order cannot be maintained.' |
| [Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs:86](../../Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs#L86), column 78 | CS0618 | 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use the FindObjectsByType overloads that do not take a FindObjectsSortMode parameter.' |
| [Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs:90](../../Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs#L90), column 40 | CS0618 | 'Object.FindFirstObjectByType<T>()' is obsolete: 'FindFirstObjectByType has been deprecated because it relies on instance ID ordering. Use FindAnyObjectByType instead, which does not depend on ordering.' |
| [Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs:273](../../Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs#L273), column 43 | CS0618 | 'Object.FindObjectsByType<T>(FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType<T>() or FindObjectsByType<T>(FindObjectsInactive) instead. InstanceID will be replaced in the future with EntityId and previous sort order cannot be maintained.' |
| [Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs:273](../../Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs#L273), column 85 | CS0618 | 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use the FindObjectsByType overloads that do not take a FindObjectsSortMode parameter.' |
| [Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs:298](../../Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs#L298), column 35 | CS0618 | 'Object.FindObjectsByType<T>(FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType<T>() or FindObjectsByType<T>(FindObjectsInactive) instead. InstanceID will be replaced in the future with EntityId and previous sort order cannot be maintained.' |
| [Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs:298](../../Assets/Tests/PlayMode/BeachLifePlatePlayTests.cs#L298), column 80 | CS0618 | 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use the FindObjectsByType overloads that do not take a FindObjectsSortMode parameter.' |
| [Assets/Tests/PlayMode/NextIterationFigurePlatePlayTests.cs:46](../../Assets/Tests/PlayMode/NextIterationFigurePlatePlayTests.cs#L46), column 25 | CS0618 | 'Object.FindObjectsByType<T>(FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType<T>() or FindObjectsByType<T>(FindObjectsInactive) instead. InstanceID will be replaced in the future with EntityId and previous sort order cannot be maintained.' |
| [Assets/Tests/PlayMode/NextIterationFigurePlatePlayTests.cs:46](../../Assets/Tests/PlayMode/NextIterationFigurePlatePlayTests.cs#L46), column 66 | CS0618 | 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use the FindObjectsByType overloads that do not take a FindObjectsSortMode parameter.' |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:26](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L26), column 65 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeWind.bendPx' is never assigned to, and will always have its default value 0 |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:26](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L26), column 73 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeWind.limbPx' is never assigned to, and will always have its default value 0 |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:26](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L26), column 81 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeWind.bob' is never assigned to, and will always have its default value 0 |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:26](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L26), column 86 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeWind.flutter' is never assigned to, and will always have its default value 0 |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:26](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L26), column 110 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeWind.shimmer' is never assigned to, and will always have its default value null |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:26](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L26), column 131 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeWind.conifer' is never assigned to, and will always have its default value false |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:27](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L27), column 68 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeSeason.season' is never assigned to, and will always have its default value null |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:27](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L27), column 87 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeSeason.paletteRow' is never assigned to, and will always have its default value 0 |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:30](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L30), column 27 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeEntry.species' is never assigned to, and will always have its default value null |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:30](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L30), column 36 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeEntry.stage' is never assigned to, and will always have its default value null |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:30](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L30), column 54 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeEntry.cellW' is never assigned to, and will always have its default value 0 |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:30](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L30), column 61 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeEntry.cellH' is never assigned to, and will always have its default value 0 |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:30](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L30), column 68 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeEntry.sheetW' is never assigned to, and will always have its default value 0 |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:30](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L30), column 76 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeEntry.sheetH' is never assigned to, and will always have its default value 0 |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:30](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L30), column 97 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeEntry.trunkAnchor' is never assigned to, and will always have its default value 0 |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:31](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L31), column 33 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeEntry.wind' is never assigned to, and will always have its default value null |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:31](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L31), column 63 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeEntry.seasonRows' is never assigned to, and will always have its default value null |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:33](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L33), column 70 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeContract.rig' is never assigned to, and will always have its default value null |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:33](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L33), column 98 | CS0649 | Field 'WharfBuildingPass2PlatePlayTests.NextTreeContract.trees' is never assigned to, and will always have its default value null |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:78](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L78), column 46 | CS0618 | 'Object.FindObjectsByType<T>(FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType<T>() or FindObjectsByType<T>(FindObjectsInactive) instead. InstanceID will be replaced in the future with EntityId and previous sort order cannot be maintained.' |
| [Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs:78](../../Assets/Tests/PlayMode/NextIterationTreePlatePlayTests.cs#L78), column 88 | CS0618 | 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use the FindObjectsByType overloads that do not take a FindObjectsSortMode parameter.' |
| [Assets/Tests/PlayMode/TrawlersAtRestPlatePlayTests.cs:82](../../Assets/Tests/PlayMode/TrawlersAtRestPlatePlayTests.cs#L82), column 26 | CS0618 | 'Object.FindFirstObjectByType<T>()' is obsolete: 'FindFirstObjectByType has been deprecated because it relies on instance ID ordering. Use FindAnyObjectByType instead, which does not depend on ordering.' |
| [Assets/_Project/Code/Economy/CounterSellDesk.cs:109](../../Assets/_Project/Code/Economy/CounterSellDesk.cs#L109), column 36 | CS0618 | 'Object.FindObjectsByType<T>(FindObjectsInactive, FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType<T>() or FindObjectsByType<T>(FindObjectsInactive) instead. InstanceID will be replaced in the future with EntityId and previous sort order cannot be maintained.' |
| [Assets/_Project/Code/Economy/CounterSellDesk.cs:109](../../Assets/_Project/Code/Economy/CounterSellDesk.cs#L109), column 106 | CS0618 | 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use the FindObjectsByType overloads that do not take a FindObjectsSortMode parameter.' |
| [Assets/_Project/Code/Economy/WharfSellPoint.cs:37](../../Assets/_Project/Code/Economy/WharfSellPoint.cs#L37), column 39 | CS0649 | Field 'WharfSellPoint._sellsWhatIsCarried' is never assigned to, and will always have its default value false |
| [Assets/_Project/Code/World/Beach/BeachLifeDirector.Gatherings.cs:343](../../Assets/_Project/Code/World/Beach/BeachLifeDirector.Gatherings.cs#L343), column 43 | CS0618 | 'Object.FindObjectsByType<T>(FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType<T>() or FindObjectsByType<T>(FindObjectsInactive) instead. InstanceID will be replaced in the future with EntityId and previous sort order cannot be maintained.' |
| [Assets/_Project/Code/World/Beach/BeachLifeDirector.Gatherings.cs:343](../../Assets/_Project/Code/World/Beach/BeachLifeDirector.Gatherings.cs#L343), column 78 | CS0618 | 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use the FindObjectsByType overloads that do not take a FindObjectsSortMode parameter.' |

## Required remaining proof

Run the native EditMode and PlayMode suites on the integrated revision, recording nonzero case counts for the named classes and any skips. The risk map prioritizes investigation; it does not replace a full integrated run. AssetDatabase/TypeCache, scene lifecycle, singleton cleanup, player export, GPU plates, and visual fidelity remain unproven here. No art judgment or baseline change is proposed.
