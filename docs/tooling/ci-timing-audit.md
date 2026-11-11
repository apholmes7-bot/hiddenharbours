# CI timing audit — 2026-11-10

## Sample and method

This freezes the 30 most recently created **completed** CI runs returned on 2026-11-10; queued and active runs are excluded. The cohort spans 2026-11-07 to 2026-11-10. Every available attempt is retained for the reliability audit. Timing ranks use only the final attempt of each run, and each case or fixture is ranked by its median recorded duration. Skipped cases are excluded from case rankings. Fixture times include their setup and cleanup; nested fixture and case times must not be added together.

30 runs were read: 23 supplied results, 7 supplied none. The available artifacts contain 46 suite results. Compressed archives were read in memory; XML entries were streamed and discarded. No complete XML or CI log was saved for this audit.

Run ids below identify Actions runs; the attempt identifies a rerun of that run. A case key is its parsed, XML-decoded full name, preserving parameters. No cross-commit pass/fail change is labelled a flake.

## End-to-end cost

The 18 successful final jobs ranged from 101.27 to 119.58 minutes (median 110.27). EditMode steps ranged from 24.78 to 39.12 minutes; PlayMode from 72.57 to 76.38. These are step wall times, distinct from NUnit durations.

Pinned-main run 42743400071 ran 15,936 EditMode cases (15,644 passed, 292 skipped) and 1,291 PlayMode cases (1,065 passed, 226 skipped). Its steps took 29.32 and 73.80 minutes. The arrival-over-real-terrain fixture alone took 721.79 seconds. Its real-time route, physics and tide assertions require independent mutable sessions; this audit does not share that state or shorten the passage to produce an artificial speedup.

## Harness change and limits

The workflow now streams the failure summary with `tools/ci/results.py`, releasing each case, fixture and captured output node. The previous reporter built a complete XML tree, including all captured Unity output. All existing Unity assertions, case names, waits, scene loads, tunables and skip gates are unchanged. The new reporter retains the first 40 failure messages, the existing 800-character message limit and the first four stack frames, while counting every case and failure.

This lowers reporter memory and parsing cost; it does not claim a material reduction in the 74-minute PlayMode simulation. No unmeasured assertion removal, shared mutable scene, higher time scale or relaxed threshold is proposed. A larger reduction needs a separately reviewed scheduling or fixture change; the hanging attempts are diagnosed in [the stall audit](ci-playmode-stalls.md). The 150-minute job timeout is unchanged.

## Reporter measurement

The following single-run comparison used the pinned-main artifacts and the same Python process with allocation tracing enabled. Times are local measurements, not runner projections. Both parsers read from the same in-memory ZIP entries; complete XML was never written to disk. Peak figures are Python traced allocations, not total process RSS. Both retained identical root attributes, case counts and outcomes.

| XML | Size bytes | Tree seconds | Stream seconds | Tree peak MiB | Stream peak MiB |
|---|---:|---:|---:|---:|---:|
| `editmode-results.xml` | 13,580,464 | 0.975 | 0.681 | 52.24 | 0.30 |
| `playmode-results.xml` | 316,358,026 | 4.057 | 3.311 | 469.44 | 31.23 |

The 316 MB PlayMode report used 93.3% less peak traced memory and parsed 18.4% faster in this sample. The change saves less than one measured second of report parsing here; it is not presented as a minutes-scale suite speedup. Standalone negative controls exercise diagnostic preservation and nonzero exit behavior; all 47 available suite records (including the extra stall run) also matched their root execution counts.

## Coverage

| Run | Head commit | Final conclusion | Result modes by attempt |
|---|---|---|---|
| 42783595491 | `29cf0dad1401e6b73ee208cad77cb28923e3aa0d` | cancelled | No result artifact |
| 42779958520 | `0dbc1d7fc30d6afa5458656adb0a9acaa7edfc40` | cancelled | No result artifact |
| 42779076223 | `67b268f45a0298b9cdb1a59817bb0d2982ac75be` | cancelled | No result artifact |
| 42778513162 | `88fbbf1e3efc1a188cd692e1f5e3a28c3d01dff4` | cancelled | No result artifact |
| 42772608647 | `6cd6bf191db91cf97ab13f57cff620d97ffc3c54` | cancelled | No result artifact |
| 42771620626 | `907f9fca1d499245a51e2b033b9b0d28101d7b9b` | cancelled | No result artifact |
| 42765557884 | `faba6fce83b41c56a4bc78c90967508e7cd2c953` | cancelled | No result artifact |
| 42765404391 | `a2b2fe09d7b7beb8a33770111f0b723e1f971b22` | success | a2 EditMode: 15946 cases, 0 failed, a2 PlayMode: 1292 cases, 0 failed |
| 42756907671 | `e31f3095ad02c272e9fe0efd004cfa4ba06f7d12` | failure | a1 EditMode: 15952 cases, 21 failed, a1 PlayMode: 1302 cases, 9 failed |
| 42756851143 | `4adbc3db698bbf03a964b198dc5cc43dfc6eb4b2` | success | a1 EditMode: 15979 cases, 0 failed, a1 PlayMode: 1293 cases, 0 failed |
| 42755606918 | `4992e6f3036590fe8bd14d8697219b7e256d2206` | success | a1 EditMode: 15954 cases, 0 failed, a1 PlayMode: 1291 cases, 0 failed |
| 42754823984 | `c5eefec80546c5a91c8bf491390ce54d1d134a14` | success | a1 EditMode: 15953 cases, 0 failed, a1 PlayMode: 1291 cases, 0 failed |
| 42752647205 | `773292ca3f2dd98999a8f83a49876b8612aa1ce2` | failure | a1 EditMode: 15946 cases, 2 failed, a1 PlayMode: 1292 cases, 0 failed |
| 42751146911 | `920c38391a188d7fa92710781483c32c54b307af` | success | a1 EditMode: 15947 cases, 0 failed, a1 PlayMode: 1292 cases, 0 failed |
| 42749360538 | `d6c9bb3c18fe862abbc5f855821e4e348da9c84b` | success | a1 EditMode: 15941 cases, 0 failed, a2 EditMode: 15941 cases, 0 failed, a2 PlayMode: 1291 cases, 0 failed |
| 42748990660 | `b1ef6a5274d06aa1586f99f935c97a4a1acc6e78` | success | a1 EditMode: 15957 cases, 0 failed, a1 PlayMode: 1291 cases, 0 failed |
| 42747690769 | `e151bd3587c40af6fe5b94efbcbfea16df795cbc` | cancelled | a1 EditMode: 15965 cases, 0 failed |
| 42743400071 | `3e7188a426c00dde0b18f94ab9512ea1a2bc0c21` | success | a1 EditMode: 15936 cases, 0 failed, a1 PlayMode: 1291 cases, 0 failed |
| 42647003504 | `6060d870eabf29b7993da7552f54feffc391a953` | success | a1 EditMode: 15936 cases, 0 failed, a1 PlayMode: 1291 cases, 0 failed |
| 42641855537 | `d81f247dc3e19e7fa8eb3de1cea6f690a9f883dd` | success | a1 EditMode: 15924 cases, 0 failed, a1 PlayMode: 1288 cases, 0 failed |
| 42539309312 | `f5a23aaa0b7fbae005418a296276f68da54ce361` | success | a1 EditMode: 15924 cases, 0 failed, a1 PlayMode: 1288 cases, 0 failed |
| 42528443985 | `73b2132aa93e54ac08f4ffc48a4137103aafc859` | failure | a1 EditMode: 15924 cases, 4 failed, a1 PlayMode: 1288 cases, 0 failed |
| 42527760026 | `4585b97efe0913454eaeacdb6ab591dcc31875b7` | success | a1 EditMode: 15887 cases, 0 failed, a1 PlayMode: 1288 cases, 0 failed |
| 42517854460 | `f732e99498404e1ba621ce43e030aacbfe619ff9` | success | a1 EditMode: 15887 cases, 0 failed, a1 PlayMode: 1288 cases, 0 failed |
| 42506975036 | `0757b18b3a41738e8ef2e1d2a805a39243508d9e` | failure | a1 EditMode: 15887 cases, 4 failed, a1 PlayMode: 1288 cases, 8 failed |
| 42463649114 | `4c9fbc1136edcfcab5baf13f91aa317961677042` | success | a1 EditMode: 15863 cases, 0 failed, a1 PlayMode: 1288 cases, 0 failed |
| 42439748834 | `55df19b4b6659533b7bce085ec3c3f9e0fe56c4f` | success | a1 EditMode: 15863 cases, 0 failed, a1 PlayMode: 1288 cases, 0 failed |
| 42430160513 | `9b30ca92c152fe1c7eeac9619f26c6b2da75d752` | success | a1 EditMode: 15859 cases, 0 failed, a1 PlayMode: 1287 cases, 0 failed |
| 42392969775 | `fb65d16ea7a2c617baad1f59a058257eb7b9874f` | success | a1 EditMode: 15859 cases, 0 failed, a1 PlayMode: 1287 cases, 0 failed |
| 42383623189 | `7fe4fc5192ab159aaad7b0d13e04aa980ec963db` | success | a1 EditMode: 15859 cases, 0 failed, a1 PlayMode: 1285 cases, 0 failed |

## EditMode: 50 slowest cases

| # | Full name | Median seconds | Maximum seconds | Samples | Maximum run |
|---|---|---:|---:|---:|---|
| 1 | `HiddenHarbours.Tests.EditMode.CumberlandGroundTests.CumberlandPaint_CoversEveryTexelWithoutRoadPlacement` | 31.292 | 39.249 | 23 | 42392969775 |
| 2 | `HiddenHarbours.Tests.RigBaking.CharacterSkinnedExportTests.V9_TheRigsOwnChecksReproduceTheGoldenReport` | 31.186 | 40.379 | 23 | 42392969775 |
| 3 | `HiddenHarbours.Tests.RigBaking.CharacterSkinnedExportTests.V10_TheRigsOwnChecksReproduceTheGoldenReport` | 31.020 | 46.622 | 23 | 42392969775 |
| 4 | `HiddenHarbours.Tests.EditMode.EveryInteriorHullReachesItsDoorFromItsStandTests.FromHerBoardSpot_SheWalksToEveryDoorHerDefNames("ExpeditionYacht120")` | 30.034 | 32.386 | 23 | 42528443985 |
| 5 | `HiddenHarbours.Tests.EditMode.WaveBathymetryImporterTests.ReimportTwice_KeepsBytesAndObjectIdentifiers("NineMileCreek")` | 28.212 | 35.436 | 23 | 42392969775 |
| 6 | `HiddenHarbours.Tests.EditMode.WaveBathymetryImporterTests.ReimportTwice_KeepsBytesAndObjectIdentifiers("StPeters")` | 27.475 | 33.995 | 23 | 42392969775 |
| 7 | `HiddenHarbours.Tests.EditMode.IntroCabinDoorwayReplayTests.D2_S1_OnEveryHullWithAMeasuredDoorway_OneHeldKeyCarriesHerThrough` | 27.466 | 32.949 | 23 | 42392969775 |
| 8 | `HiddenHarbours.Tests.EditMode.EveryInteriorHullReachesItsDoorFromItsStandTests.FromWhereLeavingTheHelmStandsHer_SheWalksToEveryDoorHerDefNames("ExpeditionYacht120")` | 24.601 | 26.363 | 23 | 42528443985 |
| 9 | `HiddenHarbours.Tests.RigBaking.ShipyardIsoBakeTests.EveryCellReproducesFromTheLiveRig` | 22.170 | 25.630 | 23 | 42392969775 |
| 10 | `HiddenHarbours.Tests.EditMode.RicePointGroundTests.RicePointPaint_CoversEveryTexelWithoutRoadPlacement` | 20.077 | 25.322 | 23 | 42392969775 |
| 11 | `HiddenHarbours.Tests.EditMode.StoreWiringTests.ChangedOnPurpose_OneIdChanged_RefusesTheVendorCarryingTheOldId_AndTouchesNothingOutsideItsRoots` | 19.826 | 25.118 | 23 | 42392969775 |
| 12 | `HiddenHarbours.Tests.RigBaking.IsoPackBakeTests.EveryCellReproducesFromTheLiveRig` | 18.888 | 22.398 | 23 | 42392969775 |
| 13 | `HiddenHarbours.Tests.RigBaking.TreeRigBakeTests.CommittedSheets_AreBitExact_AgainstAFreshRigRender` | 18.837 | 21.765 | 23 | 42527760026 |
| 14 | `HiddenHarbours.Tests.RigBaking.InteriorRigBakeTests.EveryRoomCropsSmallEnoughToPackUnderTheImportCap` | 17.692 | 19.382 | 23 | 42392969775 |
| 15 | `HiddenHarbours.Tests.RigBaking.TreeWindMathRigParityTests.TheTwin_GathersTheSameSourceTexelAsTheRig_FromTheBakedMaps` | 16.927 | 19.152 | 23 | 42527760026 |
| 16 | `HiddenHarbours.Tests.EditMode.WaveBathymetryImporterTests.ReimportTwice_KeepsBytesAndObjectIdentifiers("Cumberland")` | 16.706 | 20.977 | 23 | 42392969775 |
| 17 | `HiddenHarbours.Tests.Art.EditMode.FlowerPrefabBuilderTests.Builder_IsRerunnable_RebuildingInPlaceWithoutDuplicating` | 14.489 | 19.848 | 23 | 42392969775 |
| 18 | `HiddenHarbours.Tests.RigBaking.RoadFleetBakeTests.TheStationWindowsClaimExactlyOneAxleEachWithNoGap(classicSemi)` | 13.890 | 18.529 | 23 | 42392969775 |
| 19 | `HiddenHarbours.Tests.Art.EditMode.AcadianTreePlacementTests.Builder_IsRerunnable_RebuildingInPlaceWithoutDuplicating` | 13.761 | 17.917 | 23 | 42392969775 |
| 20 | `HiddenHarbours.Tests.RigBaking.HullMeshFleetTests.EveryCommittedHullMesh_MatchesAFreshExtractionFromItsRig` | 12.962 | 15.797 | 23 | 42392969775 |
| 21 | `HiddenHarbours.Tests.EditMode.IntroCabinDoorwayReplayTests.D2_S6_OnEveryHullWithAMeasuredDoorway_TheDoorwayNeverCrossesHerByItself` | 12.929 | 16.447 | 23 | 42755606918 |
| 22 | `HiddenHarbours.Tests.RigBaking.RoadFleetBakeTests.TheStationWindowsClaimExactlyOneAxleEachWithNoGap(aeroSemi)` | 12.676 | 21.015 | 23 | 42527760026 |
| 23 | `HiddenHarbours.Tests.RigBaking.VillageReturnIntakeTests.TheReturnedHouseInClassicDrawsTodaysHousesPixelForPixel` | 12.177 | 14.974 | 23 | 42517854460 |
| 24 | `HiddenHarbours.Tests.EditMode.WaveBathymetryImporterTests.ReimportTwice_KeepsBytesAndObjectIdentifiers("RicePoint")` | 11.174 | 13.701 | 23 | 42755606918 |
| 25 | `HiddenHarbours.Tests.RigBaking.WharfPass2IntakeTests.AllSidecarsMatchLiveModelsAndPinTheCharacterTheyName` | 10.841 | 12.578 | 23 | 42392969775 |
| 26 | `HiddenHarbours.Tests.EditMode.StoreWiringTests.NineMileCreek_TheStorePatch_IsTheCommittedScene` | 10.131 | 12.962 | 23 | 42392969775 |
| 27 | `HiddenHarbours.Tests.RigBaking.TreeRigBakeTests.RigRenders_AreBitIdenticalAcrossCalls_SoAnExactDiffIsMeaningful` | 9.352 | 10.803 | 23 | 42747690769 |
| 28 | `HiddenHarbours.Tests.EditMode.TerrainArrayImporterTests.TextureImporterSettings_DoNotChangeArrayBytes` | 9.163 | 11.316 | 23 | 42392969775 |
| 29 | `HiddenHarbours.Tests.RigBaking.RoadFleetBakeTests.TheStationWindowsClaimExactlyOneAxleEachWithNoGap(convBox)` | 8.956 | 12.587 | 23 | 42392969775 |
| 30 | `HiddenHarbours.Tests.EditMode.TerrainArrayImporterTests.ManifestChange_ReimportsWithoutRecipeReimport` | 8.912 | 10.748 | 23 | 42392969775 |
| 31 | `HiddenHarbours.Tests.EditMode.StPetersVillageRefreshTests.VillagePass_ComposesRootsOnce_KeepsCounterData_AndReplansToNothing` | 8.798 | 12.027 | 23 | 42392969775 |
| 32 | `HiddenHarbours.Tests.EditMode.TerrainArrayImporterTests.FailedReimport_CannotServeLastGoodTextures_AndRecovers` | 8.720 | 10.460 | 23 | 42392969775 |
| 33 | `HiddenHarbours.Tests.RigBaking.TreeSheetImportTests.TheContract_RecordsTheProvenanceOfThePixels` | 8.165 | 9.499 | 23 | 42392969775 |
| 34 | `HiddenHarbours.Tests.RigBaking.TreePass4BakeTests.EverySpecies_ExportsTheRigsGeometry_AndItsWindResponse` | 7.687 | 8.835 | 23 | 42392969775 |
| 35 | `HiddenHarbours.Tests.EditMode.TerrainArrayImporterTests.SourcePngChange_ReimportsWithoutRecipeReimport` | 7.590 | 8.913 | 23 | 42392969775 |
| 36 | `HiddenHarbours.Tests.RigStudio.VillageBuildingStudioAdapterTests.Preview_IsBitIdentical_ToTheBakeAtTheSamePoint_ForEveryFacing` | 7.070 | 8.724 | 23 | 42439748834 |
| 37 | `HiddenHarbours.Tests.EditMode.WaveBathymetryTests.ThePhase_IsContinuous("StPeters")` | 6.957 | 8.329 | 23 | 42392969775 |
| 38 | `HiddenHarbours.Tests.EditMode.NineMileCreekRicePointDoorTests.TheStep_PlansExactlyTheHalvesTheLoadersListAndTheWaysIn_OrNothingOnceBaked` | 6.386 | 7.613 | 23 | 42392969775 |
| 39 | `HiddenHarbours.Tests.RigBaking.ShopKitBakeTests.EveryBuildPacksInsideTheImportCap` | 6.366 | 7.106 | 23 | 42392969775 |
| 40 | `HiddenHarbours.Tests.EditMode.SeabedBakeCombTests.TheDrawnWaterline_RAGGEDNESS_IsAQuantumDividedByTheSeabedSlope` | 6.248 | 6.616 | 23 | 42528443985 |
| 41 | `HiddenHarbours.Tests.EditMode.TerrainArrayImporterTests.InvalidSource_ReportsPathAndPublishesNoPartialKit("Missing")` | 5.984 | 6.957 | 23 | 42392969775 |
| 42 | `HiddenHarbours.Tests.EditMode.NmcPlanRoadTests.TheRigidTrucksPickupsAndVan_DriveThePublicAndWorkingRoutes_BothWays_OnTheirCarriageways` | 5.949 | 7.798 | 23 | 42528443985 |
| 43 | `HiddenHarbours.Tests.EditMode.TerrainArrayImporterTests.InvalidSource_ReportsPathAndPublishesNoPartialKit("WrongSize")` | 5.922 | 7.121 | 23 | 42392969775 |
| 44 | `HiddenHarbours.Tests.EditMode.StPetersNorthPocketTests.PaintedPocketIsInstalledAndRepeatable_AndEveryOutsideTexelKeepsItsBaseBytes` | 5.891 | 7.043 | 23 | 42755606918 |
| 45 | `HiddenHarbours.Tests.EditMode.TerrainArrayImporterTests.InvalidSource_ReportsPathAndPublishesNoPartialKit("LfsPointer")` | 5.741 | 7.207 | 23 | 42392969775 |
| 46 | `HiddenHarbours.Tests.EditMode.TerrainArrayImporterTests.InvalidSource_ReportsPathAndPublishesNoPartialKit("CorruptPng")` | 5.735 | 6.792 | 23 | 42392969775 |
| 47 | `HiddenHarbours.Tests.EditMode.TerrainArrayImporterTests.InvalidSource_ReportsPathAndPublishesNoPartialKit("SixteenBitPng")` | 5.704 | 7.116 | 23 | 42527760026 |
| 48 | `HiddenHarbours.Tests.RigBaking.CharacterSkinBakeGuardTests.V10_EachOfTheTenAimsInsideTheRigsAimBar` | 5.656 | 7.720 | 23 | 42392969775 |
| 49 | `HiddenHarbours.Tests.EditMode.TerrainArrayImporterTests.InvalidSource_ReportsPathAndPublishesNoPartialKit("PalettedPng")` | 5.654 | 6.703 | 23 | 42392969775 |
| 50 | `HiddenHarbours.Tests.RigBaking.InteriorRigBakeTests.AMisspelledKeyChangesNothing_WhichIsWhyEveryKeyIsGrepVerified` | 5.504 | 6.787 | 23 | 42392969775 |

## EditMode: 50 slowest fixtures

| # | Full name | Median seconds | Maximum seconds | Samples | Maximum run |
|---|---|---:|---:|---:|---|
| 1 | `HiddenHarbours.Tests.EditMode.TerrainArrayImporterTests` | 98.247 | 116.022 | 23 | 42392969775 |
| 2 | `HiddenHarbours.Tests.EditMode.WaveBathymetryImporterTests` | 95.881 | 118.517 | 23 | 42392969775 |
| 3 | `HiddenHarbours.Tests.RigBaking.CharacterSkinnedExportTests` | 71.093 | 99.638 | 23 | 42392969775 |
| 4 | `HiddenHarbours.Tests.EditMode.EveryInteriorHullReachesItsDoorFromItsStandTests` | 61.911 | 66.522 | 23 | 42528443985 |
| 5 | `HiddenHarbours.Tests.Art.EditMode.AcadianTreePlacementTests` | 56.805 | 77.395 | 23 | 42392969775 |
| 6 | `HiddenHarbours.Tests.RigBaking.RoadFleetBakeTests` | 55.764 | 67.084 | 23 | 42527760026 |
| 7 | `HiddenHarbours.Tests.EditMode.IntroCabinDoorwayReplayTests` | 46.639 | 57.852 | 23 | 42392969775 |
| 8 | `HiddenHarbours.Tests.RigBaking.TreeRigBakeTests` | 39.127 | 45.000 | 23 | 42392969775 |
| 9 | `HiddenHarbours.Tests.Art.EditMode.FlowerPrefabBuilderTests` | 39.099 | 53.807 | 23 | 42392969775 |
| 10 | `HiddenHarbours.Tests.EditMode.CumberlandGroundTests` | 38.580 | 48.139 | 23 | 42392969775 |
| 11 | `HiddenHarbours.Tests.EditMode.StoreWiringTests` | 34.719 | 44.876 | 23 | 42392969775 |
| 12 | `HiddenHarbours.Tests.RigBaking.RoadFleetKitProbeTests` | 31.231 | 41.626 | 23 | 42392969775 |
| 13 | `HiddenHarbours.Tests.RigBaking.CharacterSkinBakeGuardTests` | 31.054 | 40.345 | 23 | 42392969775 |
| 14 | `HiddenHarbours.Tests.RigBaking.InteriorRigBakeTests` | 29.019 | 32.733 | 23 | 42392969775 |
| 15 | `HiddenHarbours.Tests.EditMode.StPetersTerrainPlanDeterminismTests` | 26.913 | 31.565 | 23 | 42755606918 |
| 16 | `HiddenHarbours.Tests.RigBaking.ShipyardIsoBakeTests` | 26.171 | 30.260 | 23 | 42392969775 |
| 17 | `HiddenHarbours.Tests.EditMode.RicePointGroundTests` | 23.938 | 29.776 | 23 | 42392969775 |
| 18 | `HiddenHarbours.Tests.EditMode.StPetersVillageRefreshTests` | 23.230 | 31.498 | 23 | 42392969775 |
| 19 | `HiddenHarbours.Tests.RigBaking.TreeWindMathRigParityTests` | 20.278 | 22.556 | 23 | 42527760026 |
| 20 | `HiddenHarbours.Tests.RigBaking.IsoPackBakeTests` | 19.765 | 23.313 | 23 | 42392969775 |
| 21 | `HiddenHarbours.Tests.RigBaking.WharfPass2IntakeTests` | 17.126 | 20.194 | 23 | 42755606918 |
| 22 | `HiddenHarbours.Tests.EditMode.StPetersTerrainPlanGuardTests` | 16.948 | 21.329 | 23 | 42392969775 |
| 23 | `HiddenHarbours.Tests.EditMode.StPetersLegacyGrassFieldFixtureTests` | 16.793 | 19.572 | 23 | 42517854460 |
| 24 | `HiddenHarbours.Tests.RigBaking.HullMeshFleetTests` | 16.381 | 19.793 | 23 | 42392969775 |
| 25 | `HiddenHarbours.Tests.EditMode.StPetersDecorTests` | 15.453 | 17.182 | 23 | 42392969775 |
| 26 | `HiddenHarbours.Tests.EditMode.NineMileCreekBerthRefreshTests` | 14.935 | 19.763 | 23 | 42392969775 |
| 27 | `HiddenHarbours.Tests.RigBaking.TreePass4BakeTests` | 14.646 | 17.057 | 23 | 42392969775 |
| 28 | `HiddenHarbours.Tests.RigBaking.ShopFixtureKitTests` | 14.093 | 17.502 | 23 | 42517854460 |
| 29 | `HiddenHarbours.Tests.RigBaking.HullLevelTagBakeTests` | 13.836 | 17.064 | 23 | 42392969775 |
| 30 | `HiddenHarbours.Tests.RigBaking.VillageReturnIntakeTests` | 13.599 | 16.547 | 23 | 42392969775 |
| 31 | `HiddenHarbours.Tests.RigBaking.CharacterBuilderOracleTests` | 13.306 | 16.319 | 23 | 42392969775 |
| 32 | `HiddenHarbours.Tests.EditMode.NineMileCreekRicePointDoorTests` | 13.181 | 16.723 | 23 | 42392969775 |
| 33 | `HiddenHarbours.Tests.EditMode.WaveBathymetryTests` | 12.324 | 14.826 | 23 | 42392969775 |
| 34 | `HiddenHarbours.Tests.EditMode.SeabedBakeCombTests` | 12.275 | 13.415 | 23 | 42755606918 |
| 35 | `HiddenHarbours.Tests.EditMode.StPetersLegacyGreenOverFixtureTests` | 12.073 | 13.667 | 23 | 42528443985 |
| 36 | `HiddenHarbours.Tests.RigBaking.HullPaintSchemeBakeTests` | 11.127 | 14.777 | 23 | 42392969775 |
| 37 | `HiddenHarbours.Tests.Art.EditMode.FishingKitSheetSliceTests` | 11.112 | 15.386 | 23 | 42392969775 |
| 38 | `HiddenHarbours.Tests.RigBaking.WharfBuildingPass2IntakeTests` | 10.631 | 12.400 | 23 | 42392969775 |
| 39 | `HiddenHarbours.Tests.RigBaking.GasStationKitTests` | 10.537 | 11.705 | 23 | 42392969775 |
| 40 | `HiddenHarbours.Tests.EditMode.CumberlandRegionTests` | 9.936 | 12.026 | 23 | 42392969775 |
| 41 | `HiddenHarbours.Tests.EditMode.NineMileCreekLayerRefreshTests` | 9.725 | 12.018 | 23 | 42392969775 |
| 42 | `HiddenHarbours.Tests.RigBaking.TreeSheetImportTests` | 9.398 | 10.841 | 23 | 42755606918 |
| 43 | `HiddenHarbours.Tests.RigBaking.RigMeshExtractionTests` | 9.280 | 10.290 | 23 | 42392969775 |
| 44 | `HiddenHarbours.Tests.EditMode.RicePointRegionTests` | 9.268 | 12.317 | 23 | 42392969775 |
| 45 | `HiddenHarbours.Tests.EditMode.TerrainPaintAnalyticExportTests` | 9.245 | 10.959 | 23 | 42528443985 |
| 46 | `HiddenHarbours.Tests.EditMode.StPetersKeyScenesContentTests` | 8.957 | 11.864 | 23 | 42392969775 |
| 47 | `HiddenHarbours.Tests.Art.EditMode.CrestGlowTests` | 8.729 | 11.853 | 23 | 42528443985 |
| 48 | `HiddenHarbours.Tests.EditMode.NmcPlanRoadTests` | 7.680 | 9.839 | 23 | 42528443985 |
| 49 | `HiddenHarbours.Tests.RigBaking.BuildingLifecyclePassTests` | 7.556 | 9.341 | 23 | 42392969775 |
| 50 | `HiddenHarbours.Tests.RigBaking.HullDriveFaceSetTests` | 7.258 | 7.591 | 23 | 42755606918 |

## PlayMode: 50 slowest cases

| # | Full name | Median seconds | Maximum seconds | Samples | Maximum run |
|---|---|---:|---:|---:|---|
| 1 | `HiddenHarbours.Tests.PlayMode.RicePointPlayablePlayTests.DirectPlay_OneCore_WalkStrand_RowPoint_CastHasNoEligibleSpecies` | 159.835 | 160.254 | 22 | 42539309312 |
| 2 | `HiddenHarbours.Tests.PlayMode.NineMileCreekLoopPlayTests.TheLoop_LicenceRodClamsDoryRepairOutboardFuel_TheReturn_AndASaveReload` | 134.372 | 137.054 | 22 | 42392969775 |
| 3 | `HiddenHarbours.Tests.PlayMode.CharacterCreatorOpeningPlayTests.NewGameCommit_ChangesTheLivePlayer_PersistsAndResumesBeforeTheCaptain` | 133.005 | 136.627 | 22 | 42392969775 |
| 4 | `HiddenHarbours.Tests.PlayMode.ArrivalAtSeaPlayTests.NewGameOpensAtSeaCrossesWithoutASeatSnapAndStepsAshore` | 128.287 | 130.707 | 22 | 42392969775 |
| 5 | `HiddenHarbours.Tests.PlayMode.CharacterCreatorOpeningPlayTests.NewGameCancel_RestoresBakedLookWornOutfitAndNoName_ThenTheCaptain` | 126.375 | 128.993 | 22 | 42392969775 |
| 6 | `HiddenHarbours.Tests.PlayMode.NineMileCreekLoopPlayTests.TheDually_DrivesDownTheRamp_RoundThePad_AndBackUp` | 122.564 | 124.720 | 22 | 42392969775 |
| 7 | `HiddenHarbours.Tests.PlayMode.CumberlandPlayablePlayTests.DirectPlay_OneCore_WalkNorthBeach_RowBasinAndBay_CastHasNoEligibleSpecies` | 113.818 | 113.995 | 22 | 42392969775 |
| 8 | `HiddenHarbours.Tests.PlayMode.ArrivalOverRealTerrainPlayTests.SheRidesTheTiedUpBoatUntilShePressesTheExitKey` | 81.628 | 81.640 | 22 | 42528443985 |
| 9 | `HiddenHarbours.Tests.PlayMode.ArrivalOverRealTerrainPlayTests.OnceAshore_SheCanActuallyWalkAway` | 72.215 | 72.224 | 22 | 42392969775 |
| 10 | `HiddenHarbours.Tests.PlayMode.ArrivalOverRealTerrainPlayTests.ThePassageIsFramedForTheHullSheIsRidingOn_AndTheWalkerAfterwards` | 71.637 | 71.639 | 22 | 42756907671 |
| 11 | `HiddenHarbours.Tests.PlayMode.ArrivalOverRealTerrainPlayTests.WhereSheLiesAgainstTheWharf_IsMeasuredAndReported` | 71.631 | 71.644 | 22 | 42749360538 |
| 12 | `HiddenHarbours.Tests.PlayMode.ArrivalOverRealTerrainPlayTests.SheRunsTheRealFairwayAndDocks_AtSpringLow` | 71.630 | 71.633 | 22 | 42754823984 |
| 13 | `HiddenHarbours.Tests.PlayMode.ArrivalOverRealTerrainPlayTests.SheRunsTheRealFairwayAndDocks_AtSpringHigh` | 71.630 | 71.633 | 22 | 42748990660 |
| 14 | `HiddenHarbours.Tests.PlayMode.ArrivalOverRealTerrainPlayTests.OnceSheIsAshore_DisablingTheArrivalSaysNothingMoreAboutTheControlMode` | 71.629 | 71.643 | 22 | 42751146911 |
| 15 | `HiddenHarbours.Tests.PlayMode.ArrivalOverRealTerrainPlayTests.ThePassengerIsTakenOutOfThePhysicsWorld_SoSheCannotShoveTheBoat` | 71.625 | 71.631 | 22 | 42748990660 |
| 16 | `HiddenHarbours.Tests.PlayMode.ArrivalOverRealTerrainPlayTests.SheStillTiesUpAndHandsOver_WithWindAndASeaRunning` | 71.610 | 71.618 | 22 | 42752647205 |
| 17 | `HiddenHarbours.Tests.PlayMode.PilotableFleetPlayTests.TheFiveNewFamilies_MeasureToTheirDerivedSpeeds` | 70.392 | 70.423 | 22 | 42754823984 |
| 18 | `HiddenHarbours.Tests.PlayMode.ArrivalOverRealTerrainPlayTests.SheClearsEveryMarkOnTheWayIn_AndTheMooredDory` | 66.256 | 66.260 | 22 | 42756907671 |
| 19 | `HiddenHarbours.Tests.PlayMode.PilotableFleetPlayTests.TheDory_IsTheSlowestBoatAfloat` | 65.656 | 65.702 | 22 | 42754823984 |
| 20 | `HiddenHarbours.Tests.PlayMode.NmcWharfWaterSidePlayTests.TheLiveDesignHull_TracesEveryRegisteredTrack_WithOneMetreClearance` | 65.198 | 65.198 | 1 | 42756907671 |
| 21 | `HiddenHarbours.Tests.PlayMode.RoadFleetJourneyPlayTests.TheHightopVanDrivesFromHerBayToWharfRoad` | 64.900 | 65.067 | 22 | 42392969775 |
| 22 | `HiddenHarbours.Tests.PlayMode.RoadFleetJourneyPlayTests.TheConventionalBoxDrivesFromHerBayToWharfRoad` | 59.951 | 59.995 | 22 | 42749360538 |
| 23 | `HiddenHarbours.Tests.PlayMode.RoadFleetJourneyPlayTests.TheClassicSemiDrivesFromHerBayToWharfRoad` | 56.791 | 56.871 | 22 | 42392969775 |
| 24 | `HiddenHarbours.Tests.PlayMode.PilotableFleetPlayTests.TheMeasuredLadder_MatchesTheDerivation` | 51.022 | 51.188 | 22 | 42751146911 |
| 25 | `HiddenHarbours.Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheClassicSemiTurnsThePredictedCircles` | 50.672 | 50.684 | 22 | 42751146911 |
| 26 | `HiddenHarbours.Tests.PlayMode.RoadFleetJourneyPlayTests.TheCaboverBoxDrivesFromHerBayToWharfRoad` | 50.410 | 50.472 | 22 | 42506975036 |
| 27 | `HiddenHarbours.Tests.PlayMode.RoadFleetJourneyPlayTests.TheAeroSemiDrivesFromHerBayToWharfRoad` | 49.210 | 49.253 | 22 | 42743400071 |
| 28 | `HiddenHarbours.Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheConvBoxTurnsThePredictedCircles` | 45.831 | 45.834 | 22 | 42430160513 |
| 29 | `HiddenHarbours.Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheModern3500TurnsThePredictedCircles` | 45.551 | 45.557 | 22 | 42392969775 |
| 30 | `HiddenHarbours.Tests.PlayMode.RoadFleetJourneyPlayTests.TheClassicSemiCouplesTowsAndReleasesInTheYard` | 45.289 | 45.351 | 22 | 42527760026 |
| 31 | `HiddenHarbours.Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheHightopVanTurnsThePredictedCircles` | 45.251 | 45.258 | 22 | 42647003504 |
| 32 | `HiddenHarbours.Tests.PlayMode.BoatCabinOnASwappedHullPlayTests.SwappedOnUnderHer_SheWalksFromTheHelmThroughHerDoor("ExpeditionYacht120")` | 44.602 | 51.974 | 22 | 42528443985 |
| 33 | `HiddenHarbours.Tests.PlayMode.RoadFleetJourneyPlayTests.TheAeroSemiCouplesTowsAndReleasesInTheYard` | 44.574 | 44.671 | 22 | 42439748834 |
| 34 | `HiddenHarbours.Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheAeroSemiTurnsThePredictedCircles` | 44.520 | 44.523 | 22 | 42463649114 |
| 35 | `HiddenHarbours.Tests.PlayMode.BoardingWhileCarriedPlayTests.CarriedDownAWholePassage_AFingerOnE_SheNeverBoardsTheMooredDory` | 44.294 | 44.306 | 22 | 42392969775 |
| 36 | `HiddenHarbours.Tests.PlayMode.PilotableFleetPlayTests.LobsterBoat_IsQuickerThanTheCapeIslander_ButHeavierWithIt` | 42.604 | 42.643 | 22 | 42463649114 |
| 37 | `HiddenHarbours.Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheModern350TurnsThePredictedCircles` | 40.912 | 40.928 | 22 | 42765404391 |
| 38 | `HiddenHarbours.Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheDually3500TurnsThePredictedCircles` | 39.311 | 39.326 | 22 | 42517854460 |
| 39 | `HiddenHarbours.Tests.PlayMode.ArrivalOpeningPlayTests.AFreshSave_IsPilotedIn_AndEndsAshoreWithTheControls` | 38.665 | 38.670 | 22 | 42383623189 |
| 40 | `HiddenHarbours.Tests.PlayMode.BreakerSurfDriftPlayTests.TheDriftArrivesInBEATS_NotAsASteadyLean` | 38.441 | 38.451 | 22 | 42528443985 |
| 41 | `HiddenHarbours.Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheModern2500TurnsThePredictedCircles` | 37.151 | 37.153 | 22 | 42430160513 |
| 42 | `HiddenHarbours.Tests.PlayMode.ArrivalOpeningPlayTests.HeldOffHerStop_SheIsTiedUpWhereSheIs_AndNeverSnappedOntoTheBerth` | 36.790 | 36.796 | 22 | 42506975036 |
| 43 | `HiddenHarbours.Tests.PlayMode.IntroPassengerMeshFigurePlayTests.TheArrival_DrawsHerAsHerMesh_BelowAndOnDeck_TurnedByHerHeldHeading_AndHandsHerBackAshore` | 36.617 | 36.628 | 22 | 42439748834 |
| 44 | `HiddenHarbours.Tests.PlayMode.IntroCabinPassagePlayTests.TheOpening_StartsBelow_ComesUpThroughHerAftDoor_AndFinishesAlongside` | 36.394 | 36.420 | 22 | 42517854460 |
| 45 | `HiddenHarbours.Tests.PlayMode.SailDrivePlayTests.SheMakesHerPolarSpeed_CloseHauledAndOnABroadReach` | 36.005 | 36.023 | 22 | 42392969775 |
| 46 | `HiddenHarbours.Tests.PlayMode.ArrivalOpeningPlayTests.TheStepAshoreIsAMove_NotARelocatedTeleport` | 35.656 | 35.667 | 22 | 42517854460 |
| 47 | `HiddenHarbours.Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheCaboverBoxTurnsThePredictedCircles` | 34.791 | 34.793 | 22 | 42430160513 |
| 48 | `HiddenHarbours.Tests.PlayMode.ArrivalOpeningPlayTests.SheRunsInUnderHerNavigationLights_AndDousesThemWhenHerLinesGoFast` | 31.616 | 31.629 | 22 | 42751146911 |
| 49 | `HiddenHarbours.Tests.PlayMode.NavBuoyCollisionPlayTests.TheLightHullIsShoulderedOffHerLine_AndTheHeavyOneIsNot` | 30.002 | 30.017 | 22 | 42641855537 |
| 50 | `HiddenHarbours.Tests.PlayMode.SailTrianglePlayTests.Sloop30_SailsTheTriangle_OnHerRudderAlone_Run_Gybe_Reach_Beat_InIrons` | 28.780 | 28.823 | 22 | 42506975036 |

## PlayMode: 50 slowest fixtures

| # | Full name | Median seconds | Maximum seconds | Samples | Maximum run |
|---|---|---:|---:|---:|---|
| 1 | `HiddenHarbours.Tests.PlayMode.ArrivalOverRealTerrainPlayTests` | 721.744 | 721.831 | 22 | 42392969775 |
| 2 | `HiddenHarbours.Tests.PlayMode.RoadFleetJourneyPlayTests` | 406.114 | 406.566 | 22 | 42392969775 |
| 3 | `HiddenHarbours.Tests.PlayMode.PilotableFleetPlayTests` | 389.603 | 389.870 | 22 | 42647003504 |
| 4 | `HiddenHarbours.Tests.PlayMode.RoadFleetTurningCirclePlayTests` | 384.075 | 384.132 | 22 | 42647003504 |
| 5 | `HiddenHarbours.Tests.PlayMode.NineMileCreekLoopPlayTests` | 311.620 | 330.998 | 22 | 42392969775 |
| 6 | `HiddenHarbours.Tests.PlayMode.CharacterCreatorOpeningPlayTests` | 263.451 | 271.207 | 22 | 42392969775 |
| 7 | `HiddenHarbours.Tests.PlayMode.RicePointPlayablePlayTests` | 160.553 | 160.890 | 22 | 42765404391 |
| 8 | `HiddenHarbours.Tests.PlayMode.ArrivalOpeningPlayTests` | 146.921 | 146.982 | 22 | 42392969775 |
| 9 | `HiddenHarbours.Tests.PlayMode.ArrivalAtSeaPlayTests` | 128.316 | 130.750 | 22 | 42392969775 |
| 10 | `HiddenHarbours.Tests.PlayMode.CumberlandPlayablePlayTests` | 114.539 | 114.976 | 22 | 42392969775 |
| 11 | `HiddenHarbours.Tests.PlayMode.SailDrivePlayTests` | 102.122 | 102.314 | 22 | 42754823984 |
| 12 | `HiddenHarbours.Tests.PlayMode.BreakerSurfDriftPlayTests` | 100.799 | 100.910 | 22 | 42517854460 |
| 13 | `HiddenHarbours.Tests.PlayMode.NmcWharfWaterSidePlayTests` | 97.649 | 97.649 | 1 | 42756907671 |
| 14 | `HiddenHarbours.Tests.PlayMode.BoatCabinOnASwappedHullPlayTests` | 83.587 | 91.378 | 22 | 42528443985 |
| 15 | `HiddenHarbours.Tests.PlayMode.NineMileCreekWallPlayTests` | 62.239 | 72.080 | 22 | 42392969775 |
| 16 | `HiddenHarbours.Tests.PlayMode.LadderBoardingPlayTests` | 58.709 | 59.010 | 22 | 42647003504 |
| 17 | `HiddenHarbours.Tests.PlayMode.IntroCabinPassagePlayTests` | 54.767 | 54.967 | 22 | 42392969775 |
| 18 | `HiddenHarbours.Tests.PlayMode.NavBuoyCollisionPlayTests` | 53.032 | 53.144 | 22 | 42751146911 |
| 19 | `HiddenHarbours.Tests.PlayMode.RicePointCrossingPlayTests` | 47.137 | 53.888 | 22 | 42392969775 |
| 20 | `HiddenHarbours.Tests.PlayMode.BoardingWhileCarriedPlayTests` | 44.451 | 44.528 | 22 | 42392969775 |
| 21 | `HiddenHarbours.Tests.PlayMode.ReverseIntoThePinPlayTests` | 43.771 | 43.845 | 22 | 42392969775 |
| 22 | `HiddenHarbours.Tests.PlayMode.NpcConversationPlayTests` | 39.669 | 40.179 | 22 | 42430160513 |
| 23 | `HiddenHarbours.Tests.PlayMode.IntroPassengerMeshFigurePlayTests` | 36.628 | 36.645 | 22 | 42392969775 |
| 24 | `HiddenHarbours.Tests.PlayMode.TrailerCouplingJourneyPlayTests` | 36.281 | 36.361 | 22 | 42527760026 |
| 25 | `HiddenHarbours.Tests.PlayMode.SailTrianglePlayTests` | 28.801 | 29.009 | 22 | 42517854460 |
| 26 | `HiddenHarbours.Tests.PlayMode.SunCastersEverywherePlayTests` | 25.835 | 31.240 | 22 | 42506975036 |
| 27 | `HiddenHarbours.Tests.PlayMode.FixedCameraStopsPlayTests` | 24.633 | 24.681 | 22 | 42392969775 |
| 28 | `HiddenHarbours.Tests.PlayMode.MooringLinePlayTests` | 24.185 | 24.256 | 22 | 42430160513 |
| 29 | `HiddenHarbours.Tests.PlayMode.CharacterMeshCastAshorePlayTests` | 21.598 | 36.189 | 22 | 42506975036 |
| 30 | `HiddenHarbours.Tests.PlayMode.DockBoardingPlayTests` | 20.433 | 21.073 | 22 | 42527760026 |
| 31 | `HiddenHarbours.Tests.PlayMode.StPetersEastDoorPlayTests` | 20.088 | 35.365 | 22 | 42506975036 |
| 32 | `HiddenHarbours.Tests.PlayMode.OverTheSidePlayTests` | 18.596 | 18.638 | 22 | 42755606918 |
| 33 | `HiddenHarbours.Tests.PlayMode.NmcShipyardPlayTests` | 18.134 | 24.788 | 22 | 42392969775 |
| 34 | `HiddenHarbours.Tests.PlayMode.NineMileCreekLaydownPlayTests` | 17.267 | 17.467 | 22 | 42392969775 |
| 35 | `HiddenHarbours.Tests.PlayMode.AnchorPlayTests` | 17.182 | 17.271 | 22 | 42392969775 |
| 36 | `HiddenHarbours.Tests.PlayMode.HelmAtTheBankedBerthPlayTests` | 16.514 | 16.552 | 22 | 42383623189 |
| 37 | `HiddenHarbours.Tests.PlayMode.IntroCabinDoorwayPlayTests` | 16.240 | 16.402 | 22 | 42506975036 |
| 38 | `HiddenHarbours.Tests.PlayMode.BoardingMovePlayTests` | 14.695 | 14.957 | 22 | 42392969775 |
| 39 | `HiddenHarbours.Tests.PlayMode.GullHauntPlayTests` | 13.946 | 14.148 | 22 | 42392969775 |
| 40 | `HiddenHarbours.Tests.PlayMode.BoatCabinJourneyPlayTests` | 13.806 | 13.894 | 22 | 42392969775 |
| 41 | `HiddenHarbours.Tests.PlayMode.VillageReturnScenesPlayTests` | 12.858 | 18.923 | 22 | 42506975036 |
| 42 | `HiddenHarbours.Tests.PlayMode.StPetersKeyScenesPlayTests` | 12.032 | 21.183 | 22 | 42506975036 |
| 43 | `HiddenHarbours.Tests.PlayMode.WaveBathymetryLoadingTests` | 11.950 | 16.914 | 22 | 42392969775 |
| 44 | `HiddenHarbours.Tests.PlayMode.VehicleSunShadowPlayTests` | 11.653 | 16.702 | 22 | 42392969775 |
| 45 | `HiddenHarbours.Tests.PlayMode.PlantFieldSunShadowPlayTests` | 10.594 | 16.553 | 22 | 42506975036 |
| 46 | `HiddenHarbours.Tests.PlayMode.AtvPlayerRidePlayTests` | 10.359 | 10.408 | 22 | 42751146911 |
| 47 | `HiddenHarbours.Tests.PlayMode.SeeEqualsFeelForcesPlayTests` | 9.610 | 9.673 | 22 | 42765404391 |
| 48 | `HiddenHarbours.Tests.PlayMode.NpcStopsToTalkPlayTests` | 9.304 | 9.376 | 22 | 42751146911 |
| 49 | `HiddenHarbours.Tests.PlayMode.CameraWheelDeviceTests` | 8.182 | 9.905 | 22 | 42506975036 |
| 50 | `HiddenHarbours.Tests.PlayMode.FuelStateEventsPlayTests` | 8.074 | 8.113 | 22 | 42439748834 |
