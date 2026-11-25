# Test time audit - 22 November 2026

The latest successful main CI run found on 22 November took **7,305.000 seconds (121.75 minutes)**. The two test steps consumed **7,035.000 seconds**; all other steps and inter-step gaps consumed **270.000 seconds**. Summed leaf test durations were **6,343.270 seconds**. The difference between a test step and its leaf cases is not automatically import time.

## Evidence and method

- Source: [main CI run 45035679830, attempt 1](https://github.com/apholmes7-bot/hiddenharbours/actions/runs/45035679830), commit `6aa5ae903f54f3dfb436a49d85960913fed23100`, event `push`, conclusion `success`. Run created 2026-11-22T14:58:18Z; job started 2026-11-22T14:58:25Z, completed 2026-11-22T17:00:10Z. Discovery snapshot: 2026-11-22 20:23 UTC, using `gh run list` filtered to main, successful CI, then the workflow-runs REST endpoint with the same filters.
- [Test job 137248815018](https://github.com/apholmes7-bot/hiddenharbours/actions/runs/45035679830/job/137248815018); step timings from `actions/runs/45035679830/attempts/1/jobs`. Artifact `test-results`, id `14763792816`, compressed size 19,284,341 bytes; not expired at collection.
- Branch base is `f1b4ab2f59f417eda06f2239f7dee25d4c52ad23`. The measured green run predates that base: these numbers describe its inventory, not unrun additions on the branch base. Recommendation source files were checked against the measured commit; differences are identified where applicable.
- This extends the [10 November multi-run audit](ci-timing-audit.md) with a single recent main snapshot. Existing streaming coverage is in `tools/ci/results.py`; no new test, validator, workflow or game code is introduced here.
- The compressed archive stayed in `BytesIO`; each XML entry was opened through `zipfile.ZipFile.open` and consumed by `xml.etree.ElementTree.iterparse` with start/end events. Only `name`, `classname`, `result` and `duration` were retained. At every end event the element was cleared and detached from its parent, including captured output. No complete ZIP, XML or job log was written to disk. Memory still includes the compressed archive and the largest individual XML text node.
- Rankings use leaf `test-case` seconds, not nested suite sums. Class and namespace totals include every leaf outcome; top-case rankings exclude skipped cases. Namespace is the classname before its last dot. The common product namespace prefix is omitted from identifiers below; remaining identifiers and parameters are unchanged. Ties use classname then case name. Values are rounded only for display.
- Every emitted case occurrence is retained. EditMode has 105 repeated `(classname, name)` keys, with 122 additional occurrences; PlayMode has none. These four retained attributes do not establish duplicate execution or justify deduplication. Class totals group the emitted `classname`, not a reconstructed fixture identity. Both XML root results are `Skipped:Ignored`; the zero-failure claim below comes from leaf outcomes, and the job conclusion separately is successful.
- These are observed durations, not a performance benchmark or a measured speedup. No local editor, build, visual check or optimization trial ran. Skipped cases did not execute their full assertions; a successful headless run is not visual acceptance.

## Counts and time reconciliation

| Mode | XML bytes | Cases | Passed | Skipped | Failed / inconclusive | Leaf seconds | XML root seconds | Step seconds | Step minus leaf |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| EditMode | 18,014,978 | 21,473 | 21,191 | 282 | 0 | 1,606.060 | 2,055.043 | 2,165.000 | 558.940 |
| PlayMode | 411,750,322 | 1,425 | 1,168 | 257 | 0 | 4,737.211 | 4,757.529 | 4,870.000 | 132.789 |

`job - leaf = 961.730 s`; this is **outside recorded leaf cases**, not proven non-test work. It comprises 270.000 s outside the two test steps plus 691.730 s inside them. Root-minus-leaf includes suite setup/teardown and runner work; step-minus-root includes editor startup/import/compile/discovery/shutdown/reporting and action overhead. Per-case setup/teardown may already be inside a leaf duration. Those components are not separately timed by the API, so their individual costs remain unknown. No player-build step exists in this job; that does not imply compilation is free.

### Every API step

| Step | Outcome | Seconds | Attribution |
|---|---|---:|---|
| Set up job | success | 3 | Outside test steps |
| Checkout (LFS pointers only) | success | 17 | Outside test steps |
| Check the results gate | success | 7 | Outside test steps |
| Compute LFS manifest (cache key) | success | 2 | Outside test steps |
| Cache LFS objects | success | 4 | Outside test steps |
| Pull LFS objects | success | 3 | Outside test steps |
| Cache Library | success | 41 | Library cache restore, not asset import |
| Restore the Unity editor | success | 61 | Editor cache restore |
| Drop the runner image's Google Chrome apt source | success | 1 | Outside test steps |
| Set up Unity | success | 93 | Editor/Hub setup, not a player build |
| Save the Unity editor (main only) | skipped | 0 | Outside test steps |
| Activate Unity license (Personal) | success | 4 | Licence activation/cleanup |
| EditMode tests | success | 2165 | Mixed: cases plus editor/import/compile/runner overhead |
| Install gdb for the hang watchdog | success | 5 | Outside test steps |
| PlayMode tests | success | 4870 | Mixed: cases plus editor/import/compile/runner overhead |
| Name the last PlayMode test | success | 0 | Outside test steps |
| Upload test results | success | 3 | Outside test steps |
| Summarise test failures | success | 1 | Outside test steps |
| Save LFS objects (main only) | skipped | 0 | Outside test steps |
| Save Library (main only) | success | 21 | Outside test steps |
| Red X verdict — environment fault or code failure? | skipped | 0 | Outside test steps |
| Post Activate Unity license (Personal) | success | 3 | Licence activation/cleanup |
| Post Checkout (LFS pointers only) | success | 0 | Outside test steps |
| Complete job | success | 0 | Outside test steps |
| Unassigned job/step gaps | - | 1 | Job duration minus summed API steps |

API timestamps have one-second resolution. The job started 7 seconds after run creation; that pre-job interval is excluded from job totals and cannot be split into queueing versus dispatch here. Checkout/LFS, cache restore/save and licence costs above are measured; import and compilation have no separate steps. No job logs were downloaded to manufacture a finer breakdown.

## EditMode: 40 slowest tests

| Rank | Class and case | Result | Seconds |
|---:|---|---|---:|
| 1 | `Tests.World.EditMode.NotebookGearPageTests.TheShippedPage_LoadsAndNamesEveryEntryAsItsOwnDefDoes` | Passed | 113.315 |
| 2 | `Tests.RigBaking.CharacterSkinnedExportTests.V9_TheRigsOwnChecksReproduceTheGoldenReport` | Passed | 28.755 |
| 3 | `Tests.RigBaking.CharacterSkinnedExportTests.V10_TheRigsOwnChecksReproduceTheGoldenReport` | Passed | 25.619 |
| 4 | `Tests.EditMode.CumberlandGroundTests.CumberlandPaint_CoversEveryTexelWithoutRoadPlacement` | Passed | 25.291 |
| 5 | `Tests.EditMode.EveryInteriorHullReachesItsDoorFromItsStandTests.FromHerBoardSpot_SheWalksToEveryDoorHerDefNames("ExpeditionYacht120")` | Passed | 24.949 |
| 6 | `Tests.EditMode.IntroCabinDoorwayReplayTests.D2_S1_OnEveryHullWithAMeasuredDoorway_OneHeldKeyCarriesHerThrough` | Passed | 24.486 |
| 7 | `Tests.EditMode.WaveBathymetryImporterTests.ReimportTwice_KeepsBytesAndObjectIdentifiers("NineMileCreek")` | Passed | 23.018 |
| 8 | `Tests.EditMode.WaveBathymetryImporterTests.ReimportTwice_KeepsBytesAndObjectIdentifiers("StPeters")` | Passed | 22.757 |
| 9 | `Tests.EditMode.StoreWiringTests.ChangedOnPurpose_OneIdChanged_RefusesTheVendorCarryingTheOldId_AndTouchesNothingOutsideItsRoots` | Passed | 22.681 |
| 10 | `Tests.EditMode.EveryInteriorHullReachesItsDoorFromItsStandTests.FromWhereLeavingTheHelmStandsHer_SheWalksToEveryDoorHerDefNames("ExpeditionYacht120")` | Passed | 20.417 |
| 11 | `Tests.RigBaking.ShipyardIsoBakeTests.EveryCellReproducesFromTheLiveRig` | Passed | 18.486 |
| 12 | `Tests.EditMode.RicePointGroundTests.RicePointPaint_CoversEveryTexelWithoutRoadPlacement` | Passed | 16.678 |
| 13 | `Tests.RigBaking.TreeRigBakeTests.CommittedSheets_AreBitExact_AgainstAFreshRigRender` | Passed | 16.521 |
| 14 | `Tests.Art.EditMode.FlowerPrefabBuilderTests.Builder_IsRerunnable_RebuildingInPlaceWithoutDuplicating` | Passed | 16.498 |
| 15 | `Tests.RigBaking.IsoPackBakeTests.EveryCellReproducesFromTheLiveRig` | Passed | 16.152 |
| 16 | `Tests.Art.EditMode.AcadianTreePlacementTests.Builder_IsRerunnable_RebuildingInPlaceWithoutDuplicating` | Passed | 15.760 |
| 17 | `Tests.RigBaking.TreeWindMathRigParityTests.TheTwin_GathersTheSameSourceTexelAsTheRig_FromTheBakedMaps` | Passed | 13.823 |
| 18 | `Tests.RigBaking.InteriorRigBakeTests.EveryRoomCropsSmallEnoughToPackUnderTheImportCap` | Passed | 13.758 |
| 19 | `Tests.EditMode.WaveBathymetryImporterTests.ReimportTwice_KeepsBytesAndObjectIdentifiers("Cumberland")` | Passed | 13.417 |
| 20 | `Tests.EditMode.StoreWiringTests.NineMileCreek_TheStorePatch_IsTheCommittedScene` | Passed | 12.462 |
| 21 | `Tests.RigBaking.RoadFleetBakeTests.TheStationWindowsClaimExactlyOneAxleEachWithNoGap(aeroSemi)` | Passed | 11.350 |
| 22 | `Tests.RigBaking.HullMeshFleetTests.EveryCommittedHullMesh_MatchesAFreshExtractionFromItsRig` | Passed | 10.939 |
| 23 | `Tests.RigBaking.VillageReturnIntakeTests.TheReturnedHouseInClassicDrawsTodaysHousesPixelForPixel` | Passed | 10.581 |
| 24 | `Tests.EditMode.IntroCabinDoorwayReplayTests.D2_S6_OnEveryHullWithAMeasuredDoorway_TheDoorwayNeverCrossesHerByItself` | Passed | 10.555 |
| 25 | `Tests.EditMode.StPetersVillageRefreshTests.VillagePass_ComposesRootsOnce_KeepsCounterData_AndReplansToNothing` | Passed | 9.976 |
| 26 | `Tests.RigBaking.BoatInteriorMeshSpikeRenderTests.A_TheExtrusionMeasurements_AreWritten` | Passed | 9.920 |
| 27 | `Tests.RigBaking.RoadFleetBakeTests.TheStationWindowsClaimExactlyOneAxleEachWithNoGap(classicSemi)` | Passed | 9.470 |
| 28 | `Tests.EditMode.WaveBathymetryImporterTests.ReimportTwice_KeepsBytesAndObjectIdentifiers("RicePoint")` | Passed | 9.451 |
| 29 | `Tests.RigBaking.WharfPass2IntakeTests.AllSidecarsMatchLiveModelsAndPinTheCharacterTheyName` | Passed | 9.011 |
| 30 | `Tests.EditMode.TerrainArrayImporterTests.TextureImporterSettings_DoNotChangeArrayBytes` | Passed | 7.837 |
| 31 | `Tests.EditMode.RicePointTrawlerBerthTests.EachCutMeetsTheOldGroundThroughABlend_SmallBounded_AndNeverBelowItsBed` | Passed | 7.722 |
| 32 | `Tests.RigBaking.RoadFleetBakeTests.TheStationWindowsClaimExactlyOneAxleEachWithNoGap(convBox)` | Passed | 7.616 |
| 33 | `Tests.EditMode.TerrainArrayImporterTests.ManifestChange_ReimportsWithoutRecipeReimport` | Passed | 7.550 |
| 34 | `Tests.RigBaking.TreeRigBakeTests.RigRenders_AreBitIdenticalAcrossCalls_SoAnExactDiffIsMeaningful` | Passed | 7.356 |
| 35 | `Tests.EditMode.TerrainArrayImporterTests.FailedReimport_CannotServeLastGoodTextures_AndRecovers` | Passed | 7.260 |
| 36 | `Tests.EditMode.TerrainArrayImporterTests.SourcePngChange_ReimportsWithoutRecipeReimport` | Passed | 6.688 |
| 37 | `Tests.RigBaking.TreeSheetImportTests.TheContract_RecordsTheProvenanceOfThePixels` | Passed | 6.618 |
| 38 | `Tests.RigBaking.TreePass4BakeTests.EverySpecies_ExportsTheRigsGeometry_AndItsWindResponse` | Passed | 6.218 |
| 39 | `Tests.EditMode.RicePointRegionTests.RicePointRefresh_DisposableSceneCopy_PreservesMovedOwnerVisual` | Passed | 6.204 |
| 40 | `Tests.EditMode.NineMileCreekRicePointDoorTests.TheStep_PlansExactlyTheHalvesTheLoadersListAndTheWaysIn_OrNothingOnceBaked` | Passed | 6.171 |

## EditMode: top 30 classes by summed leaf seconds

| Rank | Class | Cases | Passed | Skipped | Seconds |
|---:|---|---:|---:|---:|---:|
| 1 | `Tests.World.EditMode.NotebookGearPageTests` | 12 | 12 | 0 | 113.348 |
| 2 | `Tests.EditMode.TerrainArrayImporterTests` | 17 | 17 | 0 | 82.150 |
| 3 | `Tests.EditMode.WaveBathymetryImporterTests` | 18 | 18 | 0 | 77.904 |
| 4 | `Tests.Art.EditMode.AcadianTreePlacementTests` | 14 | 14 | 0 | 65.314 |
| 5 | `Tests.RigBaking.CharacterSkinnedExportTests` | 16 | 16 | 0 | 63.085 |
| 6 | `Tests.EditMode.EveryInteriorHullReachesItsDoorFromItsStandTests` | 63 | 61 | 2 | 51.637 |
| 7 | `Tests.Art.EditMode.FlowerPrefabBuilderTests` | 6 | 6 | 0 | 44.310 |
| 8 | `Tests.EditMode.StoreWiringTests` | 8 | 8 | 0 | 42.842 |
| 9 | `Tests.RigBaking.RoadFleetBakeTests` | 128 | 128 | 0 | 41.657 |
| 10 | `Tests.EditMode.IntroCabinDoorwayReplayTests` | 9 | 9 | 0 | 40.262 |
| 11 | `Tests.RigBaking.TreeRigBakeTests` | 13 | 13 | 0 | 32.470 |
| 12 | `Tests.EditMode.CumberlandGroundTests` | 7 | 7 | 0 | 31.167 |
| 13 | `Tests.RigBaking.CharacterSkinBakeGuardTests` | 45 | 45 | 0 | 26.622 |
| 14 | `Tests.RigBaking.RoadFleetKitProbeTests` | 75 | 75 | 0 | 25.601 |
| 15 | `Tests.EditMode.StPetersVillageRefreshTests` | 15 | 15 | 0 | 24.753 |
| 16 | `Tests.RigBaking.InteriorRigBakeTests` | 15 | 15 | 0 | 22.452 |
| 17 | `Tests.RigBaking.ShipyardIsoBakeTests` | 12 | 12 | 0 | 21.590 |
| 18 | `Tests.EditMode.RicePointGroundTests` | 6 | 6 | 0 | 19.991 |
| 19 | `Tests.RigBaking.IsoPackBakeTests` | 22 | 22 | 0 | 16.567 |
| 20 | `Tests.EditMode.StPetersLegacyGrassFieldFixtureTests` | 8 | 8 | 0 | 14.924 |
| 21 | `Tests.EditMode.NineMileCreekBerthRefreshTests` | 9 | 9 | 0 | 14.260 |
| 22 | `Tests.RigBaking.WharfPass2IntakeTests` | 16 | 16 | 0 | 13.911 |
| 23 | `Tests.RigBaking.TreeWindMathRigParityTests` | 3 | 3 | 0 | 13.900 |
| 24 | `Tests.RigBaking.HullMeshFleetTests` | 17 | 17 | 0 | 13.647 |
| 25 | `Tests.EditMode.StPetersDecorTests` | 14 | 14 | 0 | 12.908 |
| 26 | `Tests.RigBaking.HullLevelTagBakeTests` | 19 | 19 | 0 | 12.774 |
| 27 | `Tests.EditMode.NineMileCreekRicePointDoorTests` | 13 | 13 | 0 | 12.534 |
| 28 | `Tests.EditMode.RicePointRegionTests` | 8 | 8 | 0 | 12.284 |
| 29 | `Tests.RigBaking.ShopFixtureKitTests` | 20 | 20 | 0 | 11.651 |
| 30 | `Tests.RigBaking.CharacterBuilderOracleTests` | 8 | 8 | 0 | 11.557 |
| - | Remaining 1378 classes | 20837 | 20557 | 280 | 617.988 |

## PlayMode: 40 slowest tests

| Rank | Class and case | Result | Seconds |
|---:|---|---|---:|
| 1 | `Tests.PlayMode.RicePointPlayablePlayTests.DirectPlay_OneCore_WalkStrand_RowPoint_CastIsAnswered` | Passed | 160.194 |
| 2 | `Tests.PlayMode.CharacterCreatorOpeningPlayTests.NewGameCommit_ChangesTheLivePlayer_PersistsAndResumesBeforeTheCaptain` | Passed | 131.231 |
| 3 | `Tests.PlayMode.NineMileCreekLoopPlayTests.TheLoop_LicenceRodClamsDoryRepairOutboardFuel_TheReturn_AndASaveReload` | Passed | 127.538 |
| 4 | `Tests.PlayMode.ArrivalAtSeaPlayTests.NewGameOpensAtSeaCrossesWithoutASeatSnapAndStepsAshore` | Passed | 126.037 |
| 5 | `Tests.PlayMode.CharacterCreatorOpeningPlayTests.NewGameCancel_RestoresBakedLookWornOutfitAndNoName_ThenTheCaptain` | Passed | 125.004 |
| 6 | `Tests.PlayMode.NineMileCreekLoopPlayTests.TheDually_DrivesDownTheRamp_RoundThePad_AndBackUp` | Passed | 119.992 |
| 7 | `Tests.PlayMode.CumberlandPlayablePlayTests.DirectPlay_OneCore_WalkNorthBeach_RowBasinAndBay_CastIsAnswered` | Passed | 114.157 |
| 8 | `Tests.PlayMode.ArrivalOverRealTerrainPlayTests.SheRidesTheTiedUpBoatUntilShePressesTheExitKey` | Passed | 81.638 |
| 9 | `Tests.PlayMode.PilotableFleetPlayTests.TheFiveNewFamilies_MeasureToTheirDerivedSpeeds` | Passed | 79.570 |
| 10 | `Tests.PlayMode.ArrivalOverRealTerrainPlayTests.OnceAshore_SheCanActuallyWalkAway` | Passed | 72.215 |
| 11 | `Tests.PlayMode.ArrivalOverRealTerrainPlayTests.ThePassengerIsTakenOutOfThePhysicsWorld_SoSheCannotShoveTheBoat` | Passed | 71.639 |
| 12 | `Tests.PlayMode.ArrivalOverRealTerrainPlayTests.WhereSheLiesAgainstTheWharf_IsMeasuredAndReported` | Passed | 71.631 |
| 13 | `Tests.PlayMode.ArrivalOverRealTerrainPlayTests.SheRunsTheRealFairwayAndDocks_AtSpringLow` | Passed | 71.631 |
| 14 | `Tests.PlayMode.ArrivalOverRealTerrainPlayTests.SheRunsTheRealFairwayAndDocks_AtSpringHigh` | Passed | 71.631 |
| 15 | `Tests.PlayMode.ArrivalOverRealTerrainPlayTests.OnceSheIsAshore_DisablingTheArrivalSaysNothingMoreAboutTheControlMode` | Passed | 71.629 |
| 16 | `Tests.PlayMode.ArrivalOverRealTerrainPlayTests.ThePassageIsFramedForTheHullSheIsRidingOn_AndTheWalkerAfterwards` | Passed | 71.626 |
| 17 | `Tests.PlayMode.ArrivalOverRealTerrainPlayTests.SheStillTiesUpAndHandsOver_WithWindAndASeaRunning` | Passed | 71.586 |
| 18 | `Tests.PlayMode.PilotableFleetPlayTests.TheDory_IsTheSlowestBoatAfloat` | Passed | 70.811 |
| 19 | `Tests.PlayMode.ArrivalOverRealTerrainPlayTests.SheClearsEveryMarkOnTheWayIn_AndTheMooredDory` | Passed | 66.257 |
| 20 | `Tests.PlayMode.NmcWharfWaterSidePlayTests.AllNineRegisteredLadders_AreClimbedDownAndUp_AtBothTides` | Passed | 65.884 |
| 21 | `Tests.PlayMode.RoadFleetJourneyPlayTests.TheHightopVanDrivesFromHerBayToWharfRoad` | Passed | 64.890 |
| 22 | `Tests.PlayMode.NmcWharfWaterSidePlayTests.TheLiveDesignHull_TracesEveryRegisteredTrack_WithOneMetreClearance` | Passed | 63.148 |
| 23 | `Tests.PlayMode.RoadFleetJourneyPlayTests.TheConventionalBoxDrivesFromHerBayToWharfRoad` | Passed | 59.952 |
| 24 | `Tests.PlayMode.RoadFleetJourneyPlayTests.TheClassicSemiDrivesFromHerBayToWharfRoad` | Passed | 56.791 |
| 25 | `Tests.PlayMode.PilotableFleetPlayTests.TheMeasuredLadder_MatchesTheDerivation` | Passed | 56.189 |
| 26 | `Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheClassicSemiTurnsThePredictedCircles` | Passed | 50.671 |
| 27 | `Tests.PlayMode.RoadFleetJourneyPlayTests.TheCaboverBoxDrivesFromHerBayToWharfRoad` | Passed | 50.411 |
| 28 | `Tests.PlayMode.RoadFleetJourneyPlayTests.TheAeroSemiDrivesFromHerBayToWharfRoad` | Passed | 49.190 |
| 29 | `Tests.PlayMode.NineMileCreekWallPlayTests.SheIsHeldAtEveryGuardedEdgeOfTheQuay` | Passed | 46.075 |
| 30 | `Tests.PlayMode.NineMileCreekWallPlayTests.Sabotage_WithTheWallGone_SheWalksStraightOff` | Passed | 46.066 |
| 31 | `Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheConvBoxTurnsThePredictedCircles` | Passed | 45.831 |
| 32 | `Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheModern3500TurnsThePredictedCircles` | Passed | 45.552 |
| 33 | `Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheHightopVanTurnsThePredictedCircles` | Passed | 45.252 |
| 34 | `Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheAeroSemiTurnsThePredictedCircles` | Passed | 44.504 |
| 35 | `Tests.PlayMode.BoardingWhileCarriedPlayTests.CarriedDownAWholePassage_AFingerOnE_SheNeverBoardsTheMooredDory` | Passed | 44.298 |
| 36 | `Tests.PlayMode.PilotableFleetPlayTests.LobsterBoat_IsQuickerThanTheCapeIslander_ButHeavierWithIt` | Passed | 43.783 |
| 37 | `Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheModern350TurnsThePredictedCircles` | Passed | 40.911 |
| 38 | `Tests.PlayMode.BoatCabinOnASwappedHullPlayTests.SwappedOnUnderHer_SheWalksFromTheHelmThroughHerDoor("ExpeditionYacht120")` | Passed | 40.017 |
| 39 | `Tests.PlayMode.RoadFleetTurningCirclePlayTests.TheDually3500TurnsThePredictedCircles` | Passed | 39.312 |
| 40 | `Tests.PlayMode.ArrivalOpeningPlayTests.AFreshSave_IsPilotedIn_AndEndsAshoreWithTheControls` | Passed | 38.666 |

## PlayMode: top 30 classes by summed leaf seconds

| Rank | Class | Cases | Passed | Skipped | Seconds |
|---:|---|---:|---:|---:|---:|
| 1 | `Tests.PlayMode.ArrivalOverRealTerrainPlayTests` | 13 | 13 | 0 | 721.577 |
| 2 | `Tests.PlayMode.PilotableFleetPlayTests` | 22 | 22 | 0 | 414.638 |
| 3 | `Tests.PlayMode.RoadFleetTurningCirclePlayTests` | 9 | 9 | 0 | 383.976 |
| 4 | `Tests.PlayMode.RoadFleetJourneyPlayTests` | 12 | 12 | 0 | 370.465 |
| 5 | `Tests.PlayMode.NineMileCreekLoopPlayTests` | 8 | 7 | 1 | 287.682 |
| 6 | `Tests.PlayMode.CharacterCreatorOpeningPlayTests` | 3 | 3 | 0 | 259.142 |
| 7 | `Tests.PlayMode.IntroStormPlayTests` | 13 | 13 | 0 | 209.523 |
| 8 | `Tests.PlayMode.NmcWharfWaterSidePlayTests` | 11 | 7 | 4 | 169.421 |
| 9 | `Tests.PlayMode.RicePointPlayablePlayTests` | 3 | 2 | 1 | 161.208 |
| 10 | `Tests.PlayMode.ArrivalOpeningPlayTests` | 10 | 10 | 0 | 146.819 |
| 11 | `Tests.PlayMode.ArrivalAtSeaPlayTests` | 2 | 1 | 1 | 126.044 |
| 12 | `Tests.PlayMode.NineMileCreekWallPlayTests` | 5 | 5 | 0 | 118.714 |
| 13 | `Tests.PlayMode.CumberlandPlayablePlayTests` | 3 | 2 | 1 | 115.182 |
| 14 | `Tests.PlayMode.SailDrivePlayTests` | 4 | 4 | 0 | 98.413 |
| 15 | `Tests.PlayMode.BoatCabinOnASwappedHullPlayTests` | 60 | 49 | 11 | 91.534 |
| 16 | `Tests.PlayMode.LadderBoardingPlayTests` | 12 | 12 | 0 | 58.548 |
| 17 | `Tests.PlayMode.IntroCabinPassagePlayTests` | 9 | 9 | 0 | 54.617 |
| 18 | `Tests.PlayMode.RicePointCrossingPlayTests` | 3 | 3 | 0 | 53.279 |
| 19 | `Tests.PlayMode.NavBuoyCollisionPlayTests` | 3 | 3 | 0 | 52.972 |
| 20 | `Tests.PlayMode.BoardingWhileCarriedPlayTests` | 9 | 9 | 0 | 44.336 |
| 21 | `Tests.PlayMode.ReverseIntoThePinPlayTests` | 3 | 3 | 0 | 43.675 |
| 22 | `Tests.PlayMode.NpcConversationPlayTests` | 10 | 10 | 0 | 39.621 |
| 23 | `Tests.PlayMode.IntroPassengerMeshFigurePlayTests` | 1 | 1 | 0 | 36.624 |
| 24 | `Tests.PlayMode.TrailerCouplingJourneyPlayTests` | 5 | 5 | 0 | 36.222 |
| 25 | `Tests.PlayMode.RicePointSouthWaterDoorPlayTests` | 1 | 1 | 0 | 33.066 |
| 26 | `Tests.PlayMode.StPetersBuildingWalkInPlayTests` | 1 | 1 | 0 | 28.690 |
| 27 | `Tests.PlayMode.SailTrianglePlayTests` | 1 | 1 | 0 | 28.617 |
| 28 | `Tests.PlayMode.BreakerSurfDriftPlayTests` | 9 | 9 | 0 | 26.233 |
| 29 | `Tests.PlayMode.FixedCameraStopsPlayTests` | 3 | 1 | 2 | 24.587 |
| 30 | `Tests.PlayMode.MooringLinePlayTests` | 6 | 6 | 0 | 24.124 |
| - | Remaining 226 classes | 1171 | 935 | 236 | 477.661 |

## Every namespace by summed leaf seconds

| Mode | Namespace | Cases | Seconds |
|---|---|---:|---:|
| EditMode | `Tests.EditMode` | 11186 | 769.572 |
| EditMode | `Tests.RigBaking` | 2519 | 516.934 |
| EditMode | `Tests.Art.EditMode` | 5857 | 169.669 |
| EditMode | `Tests.World.EditMode` | 614 | 120.219 |
| EditMode | `Tests` | 68 | 7.877 |
| EditMode | `Tests.RigStudio` | 25 | 7.757 |
| EditMode | `Tests.UI.EditMode` | 437 | 4.389 |
| EditMode | `Tests.RigBaking.EditMode` | 53 | 3.502 |
| EditMode | `Tests.EditMode.RigBaking` | 1 | 2.740 |
| EditMode | `Tests.Economy` | 250 | 1.636 |
| EditMode | `Tests.Audio` | 78 | 0.987 |
| EditMode | `Tests.EditMode.Washrooms` | 212 | 0.530 |
| EditMode | `Tests.EditMode.Economy` | 32 | 0.110 |
| EditMode | `Tests.GoodsModel` | 60 | 0.056 |
| EditMode | `Tests.EditMode.Regression` | 14 | 0.024 |
| EditMode | `Tests.Sell` | 19 | 0.023 |
| EditMode | `Tests.LandedCatch` | 40 | 0.023 |
| EditMode | `Tests.Art` | 8 | 0.012 |
| PlayMode | `Tests.PlayMode` | 1423 | 4,737.176 |
| PlayMode | `Tests.Audio` | 2 | 0.035 |

## Ranked ten lowest-cost follow-ups

Rank balances estimated implementation effort, isolation risk and likely return; it is not a descending list of theoretical maximum savings. **No saving below has been measured.** Where setup has no timer, a scenario states its assumption explicitly: the defensible lower bound is zero, and the affected measured time is an upper bound, not a promise. Savings refer to this job shape; queue delay and future content growth are excluded. Rows overlap and must not be summed into a promised reduction. All existing cases, assertions, byte comparisons, skip gates and failure diagnostics must remain effective.

The source files linked in these rows and the workflow are byte-identical between the measured commit and the mandated branch base. Their timings remain from the measured commit only. This is a follow-up shortlist, not permission to change art or accept new pixels.

Two large pools deserve restraint. `ArrivalOverRealTerrainPlayTests` spends **721.577 s** exercising real passages and handovers; these cases need fresh mutable sessions. `RoadFleetTurningCirclePlayTests` spends **383.976 s** measuring complete circles at two speeds. Removing a route, reducing the circle, increasing time scale or sharing an already-arrived scene would change what the test proves. The **448.983 s** EditMode root-minus-leaf gap is also worth profiling, but it is not a measured cache miss or a ready-to-bank saving. Keep `SceneSettle`'s destruction/load guards and the hang watchdog: a few avoided settle frames are not worth reintroducing a stalled job.

| Rank | Concrete change to investigate | Seconds saved: conditional planning estimate | Effort and risk; proof required |
|---:|---|---|---|
| 1 | Index declared ids once in [NotebookGearPageTests](../../Assets/Tests/EditMode/World/NotebookGearPageTests.cs#L311). The shipped-page case reads all asset texts, then searches them again with a new id regex for every entry. Build an immutable id-to-declaration lookup for this case, preserving the same matching/name rules. | **56.657 s if its 113.315 s falls by half**; range 0 to less than 113.315 s. The run does not isolate file reading, regex search or first asset load. | Small; low/medium risk. Preserve every entry, duplicate-page-id check, missing-def failure, name comparison and fallback. Match the existing declaration-selection behavior and prove that malformed/missing data still fails. Time the components before claiming the estimate. |
| 2 | Share one scratch build among the first four read-only inspections in [FlowerPrefabBuilderTests](../../Assets/Tests/EditMode/Art/FlowerPrefabBuilderTests.cs#L37): names, component shape, material and neutral pose. Keep rebuilding/idempotence and layering cases isolated. | **About 16.980 s**, using three avoided builds times the 5.660 s names/build case as a setup proxy; that proxy includes assertions and cleanup. Class pool 44.310 s; actual saving can be zero. | Small; low/medium risk. Read-only fixture lifetime must not alter assets, leak objects or depend on test order. The two-build idempotence case must still build twice in fresh scratch. Preserve every shape/material/pose assertion; no asset or baseline changes. |
| 3 | Apply the same narrow read-only split to names, component shape, variant and material inspections in [AcadianTreePlacementTests](../../Assets/Tests/EditMode/Art/AcadianTreePlacementTests.cs#L70). Each presently calls `BuildAll`. Leave anchor, layering, lineup and rerun behavior isolated. | **About 16.082 s**, three times the 5.361 s names/build case as an upper-biased setup proxy. Class pool 65.314 s; actual saving can be zero. | Small; low/medium risk. Retain every species and all exact component checks. No shared mutable instances, no production-prefab edits and no re-baseline. Verify cases independently as well as together. |
| 4 | Cache only immutable source text and the widened script per rig within [RoadFleetKitProbeTests.BuilderHost](../../Assets/Tests/EditMode/RigBaking/RoadFleetKitProbeTests.cs#L823); keep a fresh script host and fresh execution for each probe. It currently reads and widens the same source at repeated call sites. | **2.560 s if source preparation is 10% of the 25.601 s class total and is eliminated**. Actual preparation share is unmeasured; plausible gain may be near zero. | Small; low risk if cache lifetime is one fixture and source/options form the key. Never cache a measurement, host state, mutable geometry or the expected result. Profile preparation first; stop if it is insignificant. |
| 5 | Snapshot immutable input file bytes once for [TerrainArrayImporterTests.Fixture](../../Assets/Tests/EditMode/TerrainArrayImporterTests.cs#L372), then write independent scratch copies per case instead of rereading the production kit each time. Preserve the full kit, per-case imports, invalidations and cleanup. | **16.430 s if repeated source I/O is 20% of the 82.150 s class total and can be removed**; otherwise proportionally less, possibly zero. This cannot reclaim the required import/reimport work. | Small/medium; medium risk. Mutation, corrupt-file, missing-file and recovery cases must never share scratch assets or cached imported objects. Retain both explicit reimports and every byte/object-id assertion. Measure source-copy time before implementation. |
| 6 | Investigate suppressing duplicate success-only captured output at its producer while keeping test results, failure diagnostics and watchdog events. The PlayMode XML alone is 411,750,322 bytes uncompressed. Do not change the results gate or discard failure evidence. | **0-4 s of directly timed reporting/upload work** (1 s summary plus 3 s upload); serialization or logging gains inside tests are unknown and not credited. | Small/medium; medium diagnostic risk. First identify repeated output with a bounded stream, then compare outcomes and failure detail on a failing control. This audit did not read output text, so neither a producer nor a specific removable message is yet proven. |
| 7 | Tune Library archive transfer/compression before considering cache-content exclusions. The existing [workflow](../../.github/workflows/ci.yml#L145) already restores Library and only saves it on main. Keep the same cache contents and invalidation key for the first experiment. | **15.500 s if the measured 41 s restore plus 21 s save falls by 25%**. A PR already omits saving, so its corresponding scenario is only **10.250 s**. Range 0-62 s on this main job; import effects unknown. | Medium; medium operational risk. Compression level or an alternative cache transport must be supported and measured; compare hit/miss behavior and import cost. Never trade 15 s of transfer for minutes of rebuilding. No claim that this was a cold Library or that changing the key guarantees a hit. |
| 8 | Run the five independent hulls concurrently **within each** of the two five-hull measurement cases in [PilotableFleetPlayTests](../../Assets/Tests/PlayMode/PilotableFleetPlayTests.cs#L523), only after proving spatial and service isolation. Retain each hull's own fixed-step observations and settle criterion. | Equal-duration ideal: **108.607 s = (79.570 + 56.189) x 4/5**. Unequal settle times, added load and setup reduce this; zero if isolation cannot be proved. These two cases are a 135.759 s pool, not the entire 414.638 s class. | Medium/high; medium/high risk of physics or global-service cross-talk. Each hull must still start fresh and satisfy every per-hull assertion. Compare isolated versus concurrent trajectories. Do not speed up time, shorten routes, loosen thresholds or reuse an earlier case's measured speed. |
| 9 | Consider a narrowly proven prose-only CI exemption, after checking required-check policy and all test/build inputs. The [workflow](../../.github/workflows/ci.yml#L62) currently runs the full suite on every PR. A blanket `docs/**` exclusion is unsafe: rig sources and other consumed inputs live there. | **Up to 7,305 s of runner/job work per eligible change**, less the lightweight classification/check cost. **0 s on code, data, workflow, rig or unclassified changes.** This saves executions, not test duration. | Small implementation, medium/high coverage-policy risk. Fail closed, explicitly enumerate safe prose, retain a required successful classification check, and keep full main validation. No exemption is implemented or assumed for this PR. |
| 10 | If concurrency and licence capacity permit, run EditMode and PlayMode as independent CI jobs using isolated workspaces and retained result gates. Keep both required and retain every case. This changes scheduling, not coverage. | Ideal wall reduction **2,165 s**, the shorter current test step. With an assumed **250 s** extra critical-path setup cost, planning saving is **1,915 s**. Runner-seconds increase; on a serial/shared queue the wall saving can be **0 s or negative**. | Largest effort here; high operational risk. Confirm both modes work independently (PlayMode currently follows EditMode), two concurrent jobs are allowed, licence activation is supported, and cache/result publication remains correct. Benchmark actual queue and duplicate import costs before adopting. |
