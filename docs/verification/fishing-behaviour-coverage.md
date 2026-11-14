# Fishing behaviour and test map

Snapshot: 2026-11-12, base `51d30dca25fa836696be476b8b134b9ff990a9bd`.
Scope: all 74 C# files in `Assets/_Project/Code/Fishing`, plus the Core pot
locker used by that module. This is a source and assertion audit, not an
instrumented line-coverage percentage. No gameplay, content, price, save format,
existing assertion, visual expectation or tuning value changes accompany it.

**Pinned** means a test asserts the stated outcome. A test mentioning a type,
creating a component, or configuring a dependency alone does not pin its
behaviour. The API index below names every public method family; overloads and
readouts belong to the behaviour rows, rather than counting as separate features.
Constructors and public data fields carry inputs or snapshots, not independent
proof that every possible input is safe. The remaining-gaps section states the
limits of the assertions.

## Player-facing contracts, in priority order

Test names in this section are fixtures under `Assets/Tests/EditMode`, except
where `PlayMode` is stated. The API index covers the supporting methods as well.

| Behaviour and current rule | Assertions that pin it | Limits and remaining gaps |
|---|---|---|
| Setting a trap requires an owned spare, sufficient depth and bait. Refusal spends nothing. | `PotStockGatingTests.TryPlaceGated_NoOwnedPots_RefusesNoPotStock_NothingPlacedNoBaitSpent`, `TryPlaceGated_SpendsTheLockerDown_ThenRefuses`; `TrapPlacementTests.TryPlaceGated_TooShallow_Refuses_NothingPlacedNoBaitSpent`, `TryPlaceGated_NoBaitInStock_Refuses` | The low-level `PlaceTrap` entry deliberately bypasses the gate. These tests do not make it a safe player entry. Nonfinite terrain/depth is not comprehensively validated; see finding F2. |
| A hauled pot aboard is unavailable for a second fresh set; a worked prebaited pot can be reset without consuming another owned pot. | `PotStockGatingTests.AboardPot_CountsAgainstTheLocker_ViaTheRegisteredCounter`, `DeckController_RegistersItselfAsTheAboardSource_AndTracksThePot`, `TryPlacePreBaited_IsNotStockGated_TheDeckPotIsAlreadyYours`; `PotLockerTests.HaulToDeck_ThenReSet_IsStockNeutral_AvailableStaysZero` | Aboard-counter exceptions and every invalid reconfiguration sequence are not pinned. |
| Soak is recomputed from placement time and configured day length. It is ready at the exact span, not before it. | `TrapSoakTests.NotReady_BeforeSoakSpanElapses`, `Ready_AtAndAfterSoakSpan`, `RuntimeWrappers_ReadTheLiveClockConfiguration`; new `PassiveGearBoundaryTests.InvalidDayLength_UsesTheReferenceClockScale`, `Readiness_KeepsFractionalSeconds_AtLongGameTimes` | Infinite/NaN soak hours and timestamps are not a specified safe-input contract. New tests pin valid fractional timestamps and invalid *day length*, not all malformed doubles. |
| A ready pot starts with one animal and fills monotonically up to capacity. Each extra slot has a fixed independent threshold. | `TrapFillTests.ResolveCount_OneAtReady_CapacityAtFull`, `ResolveCount_MonotonicallyFills_AsTheSoakGrows`; new `PassiveGearBoundaryTests.SlotAtItsExactFillThreshold_IsStillEmpty_ThenFillsAboveIt`, `FillCount_ClampsTheFraction_BeforeResolvingSlots`, `NonPositiveCapacity_UsesTheExistingReadyFloor` | Count resolution alone does not check readiness; the caller must gate it. Allocation and unbounded authored capacities are not covered by these count tests. |
| Species and size in a pot replay from placement facts, including after save/load; each animal has its own indexed stream. Bait changes species weights, not the number of animals. | `PlacedTrapCatchTests.SameSeed_SameInputs_YieldsIdenticalCatch`, `BuildWeightedPool_RepeatsFavouredSpecies`; `TrapFillTests.ResolveMany_AnimalsRideIndependentIndexedStreams`, `ResolveMany_BaitLeansSpeciesPerAnimal_NeverTheCount`; `PlacedTrapRuntimeTests.SaveLoad_YieldsIdenticalCatchList` | Weighted-pool null input as a direct public call, duplicate favours, output-list reuse and all invalid multiplier values have no dedicated pin. |
| Unready traps stay down. A ready catch must all fit in the hold before the legacy haul removes the trap and its DTO. | `PlacedTrapRuntimeTests.Haul_NoRoomForTheWholeCatch_IsRefused_AndTheTrapStays`, `Haul_BeforeSoak_YieldsNothing_AndLeavesTrapDown`, `Haul_LandsCatch_RemovesTrapAndDto`, `Restore_RepublishesTrapPlaced_ForTheBuoy` | Failure partway through a hostile `IHold.TryAdd` implementation is not a demonstrated transaction rollback. The current legacy path is characterized, not approved as a future physical-transfer model. |
| Holding takes line; releasing retains the line won. Lifts help, drops resist; calm and zero coupling remain forgiving. | `TrapHaulControllerTests.Holding_TakesLine_TowardTheSurface`, `NotHolding_HoldsTheLine_WhereItIs`, `GainingLine_PublishesProgressSnapshots_WithTheBuoyPosition`; `TrapHaulMathTests.CleanHaul_HoldingTheLiftsOnly_SurfacesFasterThanHoldingThrough`, `HoldLineRate_CouplingZero_IgnoresSeaState` | Component destruction midway through a haul and every terminal cancellation/event order lack a dedicated pin. These are logical rope quantities, not proof of the rendered rope. |
| Gear keys act only on deck; leaving the deck cancels a live haul without removing the wet pot. | `TrapDeckGatingTests.TrapHaul_GearKeys_LiveOnlyOnDeck`, `TrapHaul_LeavingTheDeck_DropsALiveHaul_TrapStaysDown`, `DevTrapInput_GearKeys_StandDownUnderAModalDialogue` | Native input-device delivery still requires integration execution. |
| Deck work preserves each resolved animal: grab, nip/retry, return shorts/berried animals, band keepers, rebait, reset. Full holds retain keepers on deck. | `DeckPotStateTests.TheBandedItem_IsExactlyTheResolvedCatchItem`, `ANip_LeavesTheAnimalInThePot_AndTheRetryIsAFreshDeterministicRoll`, `Shorts_GoBackOverTheSide_ValueZero`, `BerriedHens_GoBack_RegardlessOfSize`, `AFullHold_LeavesTheKeeperOnDeck_NoPenalty`, `TheFullCycle_PickBandBait_ReachesReadyToSet`, `AutoResolve_NeverDupes_WhatWasAlreadyBandedOrReturned` | All externally mutated `Animal` fields, negative/NaN held times and corrupted arrays are not covered. `DeckWork.SizeMm`'s inverted-window comment differs from implementation; F4. |
| Pot availability is owned minus deployed minus positive aboard count; it may be negative and does not write the blob. Stock merges per kind. | `PotLockerTests.AddOwned_AddsThenMerges_OneRecordPerKind_ReturnsNewTotal`, `AvailableCount_CanReadNegative_CallersGateOnPositive`; new `FishingSupplyContractTests.Locker_MissingListsReadEmpty_WithoutRepairingOrWritingTheBlob`, `Locker_QueriesArePerKind_AndDoNotNormalizeCanonicalIds` | Duplicate stock rows and integer overflow are not safe-input guarantees; F3. All test blobs are in memory. |
| Bait grants merge rows and reject empty ids/nonpositive quantities. The once flag belongs to `GrantOnce`, not `Grant`. An empty flag allows repeated grants with explicit saves. | new `FishingSupplyContractTests.BaitGrant_MergesRepeatedKinds_InInputOrder_AndCountsAcceptedRows`, `BaitGrant_IsAdditive_AndDoesNotOwnTheOnceOnlyGate`; new `StartingBaitOnceTests.FlaggedGrant_HappensOnceAcrossComponents_AndDoesNotCallSaveDirectly`, `MissingFlagKey_LeavesTheGrantRepeatable_AndSavesEachAcceptedGrant`, `EmptyGrant_StillConsumesANamedOnceFlag` | Deferred `Start`/`SaveReady` lifecycle is unpinned here. Tests use a memory-only `ISaveService`; no disk save is read or written. |
| Catch eligibility applies region, gear, season, feeding windows, tide and moving water. Starts are inclusive; feeding-window ends exclusive; either tide endpoint is allowed; unknown tide rate is permissive. | `CatchResolverTests.Matches_RespectsGear`, `Matches_RespectsWrappingNightWindow`; new `CatchResolverBoundaryTests.TideGate_IncludesBothEndpoints`, `SeasonGate_UsesTheAuthoredMask`, `TwoFeedingWindows_IncludeStartsAndExcludeEnds`, `MovingWaterGate_UsesMagnitude_IncludesThreshold_AndAllowsUnknown` | Region and bait comparisons are ordinal, unlike registry and trap bait weighting. New `RegionAndBaitIds_AreOrdinal_WhileCombinedGearAcceptsAnyAllowedBit` pins that difference. Unrecognized season values and nonfinite threshold inputs are not pinned. |
| A positive catch pool consumes one random draw; empty/gated pools consume none. The exact cumulative boundary belongs to the preceding species. Nonpositive spawn weights use the existing positive floor. | new `CatchResolverBoundaryTests.WeightedPick_UsesOneDraw_AndIncludesTheCumulativeBoundary`, `EmptyOrEntirelyGatedPool_DoesNotAdvanceTheCatchStream`, `NonPositiveSpawnWeight_RemainsCatchableAtTheExistingFloor` | Zero *effective* weight is different from the spawn-weight floor: F1 is a defect case, not a passing expectation. Nonfinite weights and null RNG handling remain unpinned. |
| Depth, bait, lure and school affect species weights. Sea bias reshapes the same size draw; calm preserves the old weight and stream. | `DepthDropResolverTests`, `BaitAndTackleTests`, `FishSchoolModelTests`, `FishSchoolHonestyTests`; new `CatchResolverBoundaryTests.SizeRoll_UsesOneDraw_AndTheSpeciesRange`, `CalmSizeOverload_PreservesBothWeightAndStream` | These assertions do not certify biological occupancy or physical ownership of a living occupant. Overload documentation drift is recorded as F5. |
| Rod action selects cast/depth drop, obtains a bite, lands or loses a fish, observes licence/hold/hand gates and reports result. | `FishingControllerTests`, `FishingDepthDropControllerTests`, `FishingCastReadsTests`, `FishingOnFootTests`, `FishingLicenseGateTests`, `CatchInHandTests`, `CatchHonestyTests`, `BaitEconomyPacingTests`, `CatchRollDeterminismTests` | These suites pin particular paths. Exhaustive state × input × modal × service-loss transitions, every cancellation readout and every bait/tackle swap during a live fight remain gaps. This map does not infer total coverage from the controller's many references. |
| False nibbles are early strikes; a strike in the true take is honored before elapsed time; misses may return within the pass bound. Terminals keep their feedback. | `BiteSequenceSimTests.AStrikeOnTheLapsingTick_StillHooks`, `Passes_AreBoundedByMaxPasses`, `SameSeed_SameInputs_ReplaysBitForBit`; new `FishingSequenceBoundaryTests.BiteWireMapping_PinsEveryCurrentPhase`, `Bite_StrikeIsJudgedEvenWithNonPositiveTime`, `Bite_TerminalOutcomeAndFeedback_SurviveFurtherTicks`; follow-up `BiteSequenceTickContractTests` | Terminal `BitePhases` map values are the current fallback, not the controller's handoff. F6's large-step and partitioned-step outcomes are now pinned under the real-time tick decision below; F7 records the separate RNG follow-up. |
| Legacy fight reels up tension and landing, eases tension, pulses to land and snaps a blind hold. If both meters fill together, enabled snap wins. | `FishFightTests.Holding_RaisesTension`, `Releasing_LowersTension`, `PacedReelAndEase_LandsTheFish`, `HandGather_Tend_NeverSnaps_AndLandsByHolding`; new `FishingSequenceBoundaryTests.Fight_SimultaneousFullMeters_GiveSnapPriorityWhenEnabled`, `Fight_NonPositiveTime_DoesNotReelOrStartASurge` | New fixtures pass explicit tuning rather than freezing balance defaults. Null-RNG contract work is already owned separately; F7. |
| V2 fight crosses once from deep to surface; runs give back bounded progress; steering, stance and sea pressure matter; effort and motion replay from a seed. | `RodFightSimTests.SameSeed_ReplaysTheWholeFightBitForBit`, `TheCrossing_StaysOneWay_EvenWhenSheTakesLineBack`, `SheCanNeverDragYouBelowTheFloor_HoweverLongSheRuns`, `AStorm_TakesTheFishOffAPassiveHand_ButNotOffOneThatLeans`; `RodFightMathTests`, `RodFightRhythmTests`, `RodFightMotionMathTests`, `DeckAngleMathTests`, `SeaFightMathTests` | Not every tick overload and terminal readout has a dedicated direct assertion. No guarantee of real-time partition invariance is inferred. |
| Longline hooks bite at stable per-hook instants, sample depth/time then, use the line's region and hard depth band, and can be stripped later. | `LonglineCatchTests.SpeciesOutsideDepthBand_NeverBites_AndDepthUsesTheBiteMoment`, `DawnOnlySpecies_BitesAcrossDawn_NeverInsideNight`, `SaveRoundTrip_RecomputesTheSameHooksAndSizes`; new `LonglineCatchBoundaryTests.Hook_IsEmptyBeforeItsBite_FishAtBite_AndStripsOnlyAfterTheExactDeadline`, `InvalidHaulTime_RefusesBeforeSamplingTheWorld`, `InvalidHookIndex_OnAnOtherwiseValidLine_Throws`, `BiteChance_ClampsAtSetAndPeak_AndInvalidDefReadsZero` | Invalid dependency/identity combinations and every nonfinite set-time/terrain combination are not exhaustively pinned. The helper is not a complete hauling/physical-transfer implementation. |
| Clams require exposed ground, shovel/licence/reach and somewhere to put the catch; depletion is remembered over the tide and reloaded. | `ClamDigTests`, `ClamRefinementsTests`, `ClamGateSentenceTests`, `ToolsInHandTests`, `CatchInHandTests`, `CatchHonestyTests`, `CatchRollDeterminismTests` | `ClamHoleLedger` public null/duplicate/capacity boundaries and every load/depletion event permutation lack isolated tests. No clam visual change is proposed. |
| Restore resolves canonical species metadata and midpoint weight, leaves freshness for the saved-state caller, and refuses unknown species. Registry lookup is case-insensitive, first-wins and ordered. | new `SpeciesCatchItemFactoryTests.Restore_UsesRegisteredMetadataAndMidpointWeight_WithDefaultFreshness`, `UnknownSpecies_RefusesAndClearsTheOutput`, `Registry_FirstRegistrationWins_AndResolvePreservesOrderAndDuplicates`, `Registry_InvalidEntriesDoNotPublishFacts_AndResetRemovesLookups`, `Library_RegisterAll_SkipsNullEntriesAndKeepsTheFirstDuplicate`, `Library_MissingArray_DoesNotClearAnExistingRegistry` | Native destruction of registered assets and registrar bootstrap without pre-registration remain unpinned. These tests do not exercise save serialization or migrate a format. |
| Schools, ecology and benthic motion are deterministic functions/data, separate from catch ownership. | `SchoolFieldTests`, `SchoolFieldRegressionTests`, `BenthicMovementTests`, `FishEcologyReferenceTests`, `FishSchoolModelTests`, `FishSchoolHonestyTests`, `FishSchoolDensityContentValidationTests` | Continuous `SchoolField` and the legacy visible-school producer are distinct subjects. Tests of one do not prove another is wired into the scene or into catch depletion. |
| NPC pot schedules repeat deterministically by day and advance through travel, haul, deck work, setting and homeward travel. | `NpcPotDayTests`, `NpcPotDayContentTests`; `NpcPotBoatsPlayTests` (PlayMode) | These do not prove a future NPC catch-composition implementation or persistence/ownership cutover. |

## New tests and execution evidence

Seven new fixture files add **91** ordinary NUnit cases. They characterize
current logic; no old test is removed, weakened, renamed or migrated.

| Fixture | Cases | Primary gap closed |
|---|---:|---|
| [CatchResolverBoundaryTests](../../Assets/Tests/EditMode/CatchResolverBoundaryTests.cs) | 31 | Exact weighted selection and draw consumption, tide/season/feeding/moving-water boundaries, size draws |
| [PassiveGearBoundaryTests](../../Assets/Tests/EditMode/PassiveGearBoundaryTests.cs) | 18 | Invalid day scale, fractional long-time readiness, fill clamps and strict slot thresholds |
| [FishingSequenceBoundaryTests](../../Assets/Tests/EditMode/FishingSequenceBoundaryTests.cs) | 16 | Bite wire vocabulary, strike/time ordering, immutable outcomes and snap priority |
| [FishingSupplyContractTests](../../Assets/Tests/EditMode/FishingSupplyContractTests.cs) | 6 | Bait merge/refusal behaviour and read-only locker derivation with missing lists |
| [SpeciesCatchItemFactoryTests](../../Assets/Tests/EditMode/SpeciesCatchItemFactoryTests.cs) | 9 | Restore metadata, unknown ids, registry precedence and library publication |
| [LonglineCatchBoundaryTests](../../Assets/Tests/EditMode/LonglineCatchBoundaryTests.cs) | 7 | Exact bite/strip instants, invalid haul times and index bounds |
| [StartingBaitOnceTests](../../Assets/Tests/EditMode/StartingBaitOnceTests.cs) | 4 | Once-flag ownership, repeated unflagged grant and memory-only save calls |

Local no-editor proof executes the actual compiled assemblies and unmodified
NUnit methods: 112 passed, zero failed/skipped, with an exact expected count for
each class. This comprises the 40 new cases in `PassiveGearBoundaryTests` (18),
`FishingSequenceBoundaryTests` (16), `FishingSupplyContractTests` (6), and 72
existing cases: `FishFightTests` (7), `BiteSequenceSimTests` (12),
`DeckWorkMathTests` (11), `TrapHaulMathTests` (13), `DeckAngleMathTests` (12),
`FlipbookMathTests` (8), `PotLockerTests` (9). No engine shim or altered runtime
source supplies these results.

Native Unity did **not** run: the six assigned editor labels were already
consumed. The complete EditMode assembly is compile-checked against refreshed
source assemblies. The remaining 51 new cases require native asset/component
creation and await EditMode CI; no pass or scene/lifecycle proof is claimed for
them. CI is not polled or represented as green by this document. No plates are
needed for this tests-and-documents change, and none are re-baselined.

## Findings: descriptions only, no implementation fixes

Line numbers below refer to the snapshot above. These are source-derived
reproduction instructions, not claims of executed native failures. Each case
states an observation or a requested failing test; none changes an existing rule.

- **F1: A zero effective-weight candidate can win a zero roll.**
  [CatchResolver.cs:213](../../Assets/_Project/Code/Fishing/CatchResolver.cs#L213)
  draws against the positive total, but the walk at line 220 accepts `roll <= acc`
  even when this candidate contributes zero. Reproduction: two matching species;
  the first is unfavoured by the bait; explicit `WrongBaitDamp01 = 0`; the second
  is favoured with a positive weight; an injected RNG returns zero. A failing
  expectation would require the positive-weight second species; current code
  returns the zero-weight first species. No passing test blesses that result.
- **F2: Trap placement accepts positive infinite depth.**
  [TrapPlacement.cs:29](../../Assets/_Project/Code/Fishing/TrapPlacement.cs#L29)
  compares `depth >= minimum` without finite validation. With depth `+Infinity`
  and a finite positive minimum, `IsDeepEnough` is true. A failing safety case
  would reject the placement without spending stock/bait when terrain or water
  sampling produced infinity. Existing tests cover ordinary depth and missing
  services, not this input. Whether sampling contracts should reject it upstream
  needs a separate change.
- **F3: Owned-stock addition can overflow.**
  [PotLocker.cs:47](../../Assets/_Project/Code/Core/Save/PotLocker.cs#L47)
  adds two `int` counts without a bound. A blob owning `int.MaxValue` pots of one
  kind plus `AddOwned(..., 1)` wraps negative under the current unchecked build;
  subsequent availability reads no spare. The analogous bait accumulation is at
  [StartingBait.cs:75](../../Assets/_Project/Code/Fishing/StartingBait.cs#L75).
  A failing case would require refusal/preservation, not a wrapped negative
  balance. The correct limit/policy is not invented by this audit.
- **F4: Inverted size-window prose differs from implementation.**
  [DeckWork.cs:55](../../Assets/_Project/Code/Fishing/DeckWork.cs#L55)
  says a reversed window collapses to min; lines 59–61 sort the endpoints and
  interpolate. `SizeMm(0x80000000, 140, 60)` yields 100, not 140.
  `DeckWorkMathTests.SizeMm_CollapsesADegenerateWindow_ToTheMin` covers equal
  endpoints only. This is documentation/input-contract ambiguity, not permission
  to alter authored data or legal-size sorting.
- **F5: The legacy resolver-overload comment promises depth neutrality.**
  [CatchResolver.cs:172](../../Assets/_Project/Code/Fishing/CatchResolver.cs#L172)
  says a depth-carrying context rolls flat; line 177 forwards the same context and
  `DepthDropSettings.Default`, and lines 254–256 apply depth when `HasDepth`.
  Reproduction: depth-sensitive species and a depth-bearing context through the
  three-argument overload. A literal reading of the comment would expect flat
  weights, but implementation applies depth. Existing no-depth callers remain
  neutral. This audit does not change overload behaviour or assertions.
- **F6: A large bite tick discards phase overshoot.**
  [BiteSequenceSim.cs:114](../../Assets/_Project/Code/Fishing/BiteSequenceSim.cs#L114)
  advances at most one phase; `EnterPhase` resets elapsed time at line 168. A
  zero-nibble profile with 0.25-second approach and 0.5-second take remains in
  `TakeOpen` after one quiet 1-second tick; four quiet 0.25-second ticks have
  already exhausted the take and ended a single-pass profile. A failing
  partition-invariance case would compare those results. The sim is real-time
  interaction, so adopting such an invariant needs a separate decision; the
  current tests do not claim it. Follow-up decision:
  [real-time tick contract](../design/bite-sequence-tick-contract.md) retains one transition
  per call so a newly entered take cannot be consumed before publication.
  `BiteSequenceTickContractTests` now pins both exact outcomes and the different strike
  results after equal supplied time. Partition invariance is explicitly not the rule;
  this follow-up changes no shipped timing or tuning.
- **F7: Legacy null-RNG fallbacks use hidden time-seeded randomness.**
  [FishFight.cs:117](../../Assets/_Project/Code/Fishing/FishFight.cs#L117) and
  [BiteSequenceSim.cs:94](../../Assets/_Project/Code/Fishing/BiteSequenceSim.cs#L94)
  construct `new System.Random()` on null. A failing explicit-RNG-contract case
  would require rejection before state is created. All new fight/bite tests pass
  an explicit RNG. Draft PR 1077 already owns the contract fix; no overlapping
  runtime edit or alternate fix is made here. The real-time fight is distinct
  from the deterministic world simulation.

## Remaining work by kind

- **Pure-logic boundaries:** weighted-pool duplication/case/output-buffer reuse;
  nonfinite weights, soak inputs and tuning; invalid public enum values;
  `DeckWorkDef.TryGetRule` missing/duplicate/case policy; comprehensive
  `ClamHoleLedger` null/duplicate/capacity rules; NPC day edges and invalid
  schedules beyond the current fixtures. The API map marks known partial areas;
  it does not turn these into passing guarantees.
- **Lifecycle and integration:** registrar bootstrap without a prefilled
  registry; deferred starting-bait grant after a real load; cancellation and
  destruction during hauls; controller state/readout/event matrices; hostile
  hold failure during transfer; content-library absence and native asset
  destruction. These need focused native/integration work rather than asserting
  that a component can be configured.
- **Picture and content:** `TrapSplashFx`, library cells, swim/jump presentation,
  clam escape visuals and visibility all require their existing art/content or
  GPU proof. A logical frame index or geometry test does not approve a look.
  No sprite, animation, shader, material, scene or plate is changed here.
- **Later wiring:** living-occupant ownership/depletion, NPC catch composition
  and trawl gameplay are separate work. This audit neither switches catches to
  a living model nor creates the later systems. Current table-roll tests remain
  intact.

## Public API index

The following inventory names each source file and its public callable families.
The test and gap columns refer to asserted behaviours, not just source-name
matches. Readouts, configuration overloads and data-only types are called out
where they have additional limits.

| Source and public callable/readout families | Test pins | Remaining limits |
|---|---|---|
| [BenthicMovement.cs](../../Assets/_Project/Code/Fishing/BenthicMovement.cs) — `BenthicPoint`, `BenthicSegment`, `BenthicMovementProfile`, `TrySample`, `LengthMetres` | BenthicMovementTests: supported routes, stationary clams, deterministic shelter/walk/scuttle, invalid inputs. | No presentation or live occupancy wiring. |
| [BiteDef.cs](../../Assets/_Project/Code/Fishing/BiteDef.cs) — `BiteProfile`, `ToProfile` | BiteContentValidationTests; BiteSequenceSimTests: valid authored profiles and profile-driven sequences. | Not every profile sanitization boundary is isolated. |
| [BitePhases.cs](../../Assets/_Project/Code/Fishing/BitePhases.cs) — `ToFishingPhase` | FishingSequenceBoundaryTests.BiteWireMapping_PinsEveryCurrentPhase. | Unknown numeric enum values not separately pinned. |
| [BiteSequenceSim.cs](../../Assets/_Project/Code/Fishing/BiteSequenceSim.cs) — `BiteSequenceSim`, `Tick`, `Phase`, `LastMiss`, `PassIndex`, `NibbleIndex`, `PhaseSeconds`, `TakeWindow01`, `IsOver` | BiteSequenceSimTests; FishingSequenceBoundaryTests: strike priority, retry bound, replay, terminal feedback. | F6/F7; no tick-partition invariant. |
| [CastWaterMath.cs](../../Assets/_Project/Code/Fishing/CastWaterMath.cs) — `TryClampToWater` | FishingOnFootTests: water clamp through the on-foot cast path. | Direct null callback, every ray boundary and malformed tuning lack isolated pins. |
| [CatchResolver.cs](../../Assets/_Project/Code/Fishing/CatchResolver.cs) — `CatchContext`, `BaitFavours_Contains`, `Matches`, `Resolve`, `EffectiveWeight`, `BaitAffinity`, `LureAffinity`, `RollWeight`, `HasDepth`, `HasBait` | CatchResolverTests; CatchResolverBoundaryTests; DepthDropResolverTests; BaitAndTackleTests; FishSchoolHonestyTests: eligibility, weights, draw counts, size. | F1/F5; nonfinite values and null RNG remain gaps. |
| [ClamDig.cs](../../Assets/_Project/Code/Fishing/ClamDig.cs) — `WithinReach`, `TryDig`, `MarkConsumed`, `OnGameLoaded`, `IsExposedNow`, `ShovelInHand`, `Interact`, `Configure`, `ConfigureInteract`, `ShowingSquirt`, `SpurtRise`, `SpurtU`, `SpurtPeriodMs`, `SpurtPhaseMs`, `Consumed`, `SpentAtSeconds`, `ReachRadius`, `SpotPos`, `Id`, `WorldPosition`, `ReachMeters`, `Priority`, `Contexts`, `RequiresFacing`, `IsAvailable`, `VerbLabel` | ClamDigTests; ClamRefinementsTests; ToolsInHandTests; CatchInHandTests; CatchHonestyTests; CatchRollDeterminismTests: reach, gates, depletion, reload, hand transfer. | Not every Configure/Interact/property/event permutation; no native visual proof added. |
| [ClamDigger.cs](../../Assets/_Project/Code/Fishing/ClamDigger.cs) — `TryGetPlayerPosition`, `PublishPlayerPosition`, `ClearPlayerPosition`, `TryDigNearest`, `NearestDiggable`, `Configure` | ClamDigTests; ClamRefinementsTests; CatchHonestyTests: nearest available hole and published player reach. | Explicit position-clear/lifecycle matrix and ties are not exhaustive. |
| [ClamHoleLedger.cs](../../Assets/_Project/Code/Fishing/ClamHoleLedger.cs) — `SameTide`, `TryGetSpentAt`, `MarkSpent` | CatchHonestyTests: consumed-hole persistence/reload over a tide. | SameTide/TryGetSpentAt/MarkSpent null, duplicate and trim bounds not isolated. |
| [ClamHoleVisual.cs](../../Assets/_Project/Code/Fishing/ClamHoleVisual.cs) — `Tick`, `Configure`, `ConfigureEscape`, `ConfigureSquirtOffset`, `HoleRenderer`, `SquirtRenderer`, `IsSinking`, `IsHidden`, `LingerTimer` | ClamRefinementsTests; CatchHonestyTests: escape/depletion integration. | ConfigureSquirtOffset and renderer appearance lack direct logical proof; art proof separate. |
| [ClamSpurtMath.cs](../../Assets/_Project/Code/Fishing/ClamSpurtMath.cs) — `TimingFor`, `TrySpurt`, `JetHeightPixels`, `ShowsDroplet`, `DutyCycle` | ClamSpurtMathTests: timing, phase, rise, droplet and duty-cycle rules. | No new look or animation approval. |
| [DeckAngleMath.cs](../../Assets/_Project/Code/Fishing/DeckAngleMath.cs) — `WorldToDeckFrame`, `AcrossHull01`, `TensionPerSec` | DeckAngleMathTests: frame transform, rail stance gradient, tension and off switch. | No geometric assertion is a rendered-deck plate. |
| [DeckPot.cs](../../Assets/_Project/Code/Fishing/DeckPot.cs) — `DeckPot`, `NextInPot`, `NextOnDeck`, `TryGrabNext`, `TryBandNext`, `MarkBaited`, `AutoResolve`, `Trap`, `Def`, `InstanceId`, `PlacementGameTimeSeconds`, `WorldSeed`, `Baited`, `LoadedBait`, `Animals`, `InPotCount`, `OnDeckCount`, `BandedCount`, `ReturnedCount`, `PotEmpty`, `NeedsBait`, `ReadyToSet` | DeckPotStateTests; FreshnessStampingTests: animal identity/fates, counts, pick/band/bait, full hold, auto-resolve conservation. | Public mutable Animal fields and corrupt tuning are not safe-input contracts. |
| [DeckWork.cs](../../Assets/_Project/Code/Fishing/DeckWork.cs) — `AnimalHash`, `U01`, `SizeMm`, `RollBerried`, `Care01`, `NipChance01`, `RollNip`, `IsKeeper` | DeckWorkMathTests: channel/attempt identity, U01, size, berried, care, nip and legal keeper decision. | F4; malformed/nonfinite inputs not exhaustive. |
| [DeckWorkDef.cs](../../Assets/_Project/Code/Fishing/DeckWorkDef.cs) — `TryGetRule` | DeckWorkContentValidationTests; DeckPotStateTests: authored rules and known species lookup. | TryGetRule missing/duplicate/case policy lacks isolated tests. |
| [DepthDropMath.cs](../../Assets/_Project/Code/Fishing/DepthDropMath.cs) — `IsWeightedRig`, `SinkSpeedMps`, `FallStep`, `ReelStep`, `FloorMeters`, `Depth01`, `IsBottomed`, `InBottomSweetWindow`, `ZoneForDepth`, `SpeciesDepthAffinity` | DepthDropMathTests; DepthDropResolverTests: weighted rigs, falling/reeling, reachable floor, zones, bottom window, affinity. | All malformed/nonfinite values are not certified. |
| [DevFishingInput.cs](../../Assets/_Project/Code/Fishing/DevFishingInput.cs) — `OnControlModeChanged`, `ConfigureRodGate`, `TickFishing`, `FishingLive`, `RodIsToHand` | DevFishingInputTests; ToolsInHandTests; StrikeGestureTests: mode, held-rod and gesture forwarding. | Native device delivery and all modal transitions not exhaustive. |
| [DevTrapInput.cs](../../Assets/_Project/Code/Fishing/DevTrapInput.cs) — `OnControlModeChanged`, `DropTrap`, `GrantDevSupply`, `Configure`, `GearKeysLive` | TrapDeckGatingTests; PotStockGatingTests; DevFishingInputTests: deck/modal gating, drop/grant notices and stock refusal. | Configure is fixture wiring; no independent lifecycle proof for every field. |
| [FishEcologyDef.cs](../../Assets/_Project/Code/Fishing/FishEcologyDef.cs) — Data/enum/constants only; no public method. | FishEcologyReferenceTests: reference records and content ids. | Data-only fields; not a hard occupancy certificate. |
| [FishEcologyValidation.cs](../../Assets/_Project/Code/Fishing/FishEcologyValidation.cs) — `Validate` | FishEcologyReferenceTests: validator acceptance/refusal rules. | Reference validity does not enforce spawn occupancy. |
| [FishFight.cs](../../Assets/_Project/Code/Fishing/FishFight.cs) — `FishFightTuning`, `For`, `IsHandGathered`, `FishFight`, `Tick`, `Tension01`, `Landing01`, `Result`, `IsSurging`, `IsOver` | FishFightTests; FishingSequenceBoundaryTests: tension, landing, hand gather, pace, no-op ticks, terminal snap priority. | F7; all tuning combinations/surge overshoot not certified. |
| [FishingController.cs](../../Assets/_Project/Code/Fishing/FishingController.cs) — `OnControlModeChanged`, `Tick`, `Configure`, `CancelCastGesture`, `ConfigureDepthDrop`, `SetBait`, `SetTackle`, `State`, `Phase`, `Gear`, `LastCast`, `Angler`, `Depth01`, `HasSomethingOnTheHook`, `JigQuality01`, `Bait`, `Tackle`, `BaitBox`, `TackleBoxItems` | FishingControllerTests; FishingDepthDropControllerTests; RodFightControllerTests; BiteSequenceControllerTests; FishingCastReadsTests; CatchRollDeterminismTests; CatchInHandTests; FishingLicenseGateTests. | Specific cast/drop/fight/bait/tackle/cancel paths only; state × modal × service-loss matrix remains partial. |
| [FishingStrings.cs](../../Assets/_Project/Code/Fishing/FishingStrings.cs) — Data/enum/constants only; no public method. | CatchHonestyTests: player-facing catch honesty text. | Constants only; no complete wording snapshot. |
| [FishSchoolMath.cs](../../Assets/_Project/Code/Fishing/FishSchoolMath.cs) — `CellIndex`, `SlotIndex`, `SlotStartSeconds`, `CellSlotKey`, `Rand01`, `Range`, `RangeInt`, `AppearanceChance01`, `BaseChanceForDensity`, `SeasonAppearance`, `IsPresent`, `PresenceRoll01`, `SpeciesPresenceRoll01`, `CentreFor`, `RadiusFor`, `DepthFor`, `MarkCountFor`, `WindowFor`, `SlotSeconds`, `SpeciesCountFor`, `SpeciesScore`, `BiteRateMultiplier`, `DepthMatch01`, `Sanitized` | FishSchoolModelTests; FishSchoolHonestyTests; VisibleFishTests; VisibleFishSeenTests: seed/cell/slot, presence, distributions, windows and depth/bite factors. | Helper-level malformed input domain not exhaustive; this is the legacy visible-school model. |
| [FishSchoolModel.cs](../../Assets/_Project/Code/Fishing/FishSchoolModel.cs) — `FishSchoolModel`, `SchoolsAt`, `MarksAt`, `SchoolsInView`, `Query`, `Settings`, `DepthSettings`, `SecondsPerHour` | FishSchoolModelTests; FishSchoolHonestyTests; FishSchoolDensityContentValidationTests: query/view/marks identity, replay, content density. | Not proof of continuous SchoolField consumer wiring. |
| [FishSchoolPresenter.cs](../../Assets/_Project/Code/Fishing/FishSchoolPresenter.cs) — `Rebuild`, `SpreadMetresFor`, `SchoolKey`, `MaxSwimmersPerSchool`, `SortingOrder`, `VisibleSwimmers`, `VisibleSchools`, `Instance` | FishDutyCycleAtTheLandingTests; VisibleFishPlatePlayTests (PlayMode): producer/presenter and school key usage. | Rebuild/SpreadMetresFor/SchoolKey do not have full independent pure contracts; pixel proof separate. |
| [FishSpeciesDef.cs](../../Assets/_Project/Code/Fishing/FishSpeciesDef.cs) — `LureFavored`, `RegionAllowed`, `GearAllowed`, `TideAllowed`, `SeasonAllowed`, `TimeAllowed`, `MovingWaterAllowed`, `StatesSchoolSize`, `StatesShoalSpread`, `StatesSchoolDensity`, `Jumps`, `IsShellfish`, `IsBottomFish` | CatchResolverBoundaryTests; CatchResolverTests; BaitAndTackleTests; ContentValidationTests: region/gear/tide/season/time/movement/lure gates and valid content. | Public data is not all boundary-tested; invalid season/mask/NaN policies remain partial. |
| [FishSpeciesLibrary.cs](../../Assets/_Project/Code/Fishing/FishSpeciesLibrary.cs) — `RegisterAll` | SpeciesCatchItemFactoryTests.Library_RegisterAll_SkipsNullEntriesAndKeepsTheFirstDuplicate; Library_MissingArray_DoesNotClearAnExistingRegistry. | Resource load/bootstrap is separate. |
| [FishSpeciesRegistrar.cs](../../Assets/_Project/Code/Fishing/FishSpeciesRegistrar.cs) — Data/enum/constants only; no public method. | No isolated bootstrap assertion found. | ResourcesPath is public data; private Bootstrap must be tested without pre-registering dependencies. |
| [FishSpeciesRegistry.cs](../../Assets/_Project/Code/Fishing/FishSpeciesRegistry.cs) — `Register`, `Jumps`, `FightJumpPeriodSeconds`, `Get`, `Has`, `Resolve`, `Reset`, `Facts`, `Count` | SpeciesCatchItemFactoryTests: Register/Get/Has/Resolve/Count/Reset, first-wins, case/order/duplicate behaviour and facts publication; HookedJumperTests. | Native destruction of a registered Def is not pinned. |
| [FishSwimSpriteLibrary.cs](../../Assets/_Project/Code/Fishing/FishSwimSpriteLibrary.cs) — `Cell`, `FramesFor`, `Has`, `KindFor`, `TrySwimKindFor`, `LengthMetresFor`, `Configure` | FishDutyCycleAtTheLandingTests; VisibleFishPlatePlayTests (PlayMode); BucketFillTests; HodFillTests: species-kind/cell use through consumers. | Cell/FramesFor/Has/KindFor/TrySwimKindFor/LengthMetresFor fallback matrix not isolated; no image acceptance. |
| [FlipbookMath.cs](../../Assets/_Project/Code/Fishing/FlipbookMath.cs) — `LoopFrame`, `OneShotFrame`, `OneShotSeconds` | FlipbookMathTests: loop/one-shot frame and duration, empty/degenerate inputs. | Tests pin indices only. |
| [Gear.cs](../../Assets/_Project/Code/Fishing/Gear.cs) — Data/enum/constants only; no public method. | FishingV2ContractTests; ContentValidationTests; resolver and gear-specific fixtures exercise flags. | Enums carry no methods; not every numeric identity has an individual assertion. |
| [GullSplashLog.cs](../../Assets/_Project/Code/Fishing/GullSplashLog.cs) — `GullSplashLog`, `Subscribe`, `Unsubscribe`, `Clear`, `RecordAt`, `TryScatter`, `Listening`, `Held` | GullSplashScatterTests; SeagullLandingPlayTests (PlayMode): event recording, bounded lifetime and scatter consumption. | Every Subscribe/Unsubscribe/Clear lifecycle permutation is not independently pinned. |
| [IFishSchoolWorld.cs](../../Assets/_Project/Code/Fishing/IFishSchoolWorld.cs) — `LiveFishSchoolWorld`, `HourOfDayAt`, `TideRateMetresPerHourAt`, `WaterColumnAt`, `SeaState01At`, `SeasonAt`, `WorldSeed`, `RegionId`, `Settings`, `DepthSettings`, `SecondsPerHour` | DayCalendarTests; FishSchoolModelTests; FishSchoolHonestyTests: live clock, season, tide, depth/sea projection. | Null/missing-service combinations are partial; fixture implementations are not producer proof. |
| [JigMath.cs](../../Assets/_Project/Code/Fishing/JigMath.cs) — `Quality01`, `BiteDelayScale`, `GetTarget`, `RatioMatch01` | JiggingTests: quality, target stroke, ratio matching and bite-delay effect. | All nonfinite tuning combinations not covered. |
| [JigWork.cs](../../Assets/_Project/Code/Fishing/JigWork.cs) — `Reset`, `Tick`, `StrokesPerSec`, `StrokeMetres` | JiggingTests: reset and stroke-driven tick/readouts. | Native input delivery is outside this POCO. |
| [LivingFishBehaviour.cs](../../Assets/_Project/Code/Fishing/LivingFishBehaviour.cs) — `IsValid` | SchoolFieldTests; SchoolFieldRegressionTests; LivingFishContentValidationTests: profile use and valid content. | Plain fields/enums; not physical catch ownership. |
| [LonglineCatch.cs](../../Assets/_Project/Code/Fishing/LonglineCatch.cs) — `LonglineHookResult`, `BiteChance01`, `ResolveHook` | LonglineCatchTests; LonglineCatchBoundaryTests: hook timing, strip deadline, seed/save replay, depth/time/gear gates and bounds. | Invalid dependency/identity combinations partial; transfer not implemented by this helper. |
| [LonglineDef.cs](../../Assets/_Project/Code/Fishing/LonglineDef.cs) — `HasValidTuning` | LonglineDefTests: HasValidTuning content/numeric validity. | Data-only inputs; no asset changed. |
| [LonglineGeometry.cs](../../Assets/_Project/Code/Fishing/LonglineGeometry.cs) — `TryValidate`, `HookPosition`, `TryBuild` | LonglineGeometryTests: validation, bounded counts, endpoint/hook interpolation and construction. | No rendered line or handling proof. |
| [LonglinePlacement.cs](../../Assets/_Project/Code/Fishing/LonglinePlacement.cs) — `CanPlaceAt` | LonglinePlacementTests: full-span depth/terrain placement checks. | No scene/input wiring claimed. |
| [NpcPotDay.cs](../../Assets/_Project/Code/Fishing/NpcPotDay.cs) — `NpcPotBoatPose`, `NpcPotState`, `DayIndexOf`, `Problem`, `Along`, `Starboard`, `Bearing`, `Hash01`, `PotPosition`, `StationFor`, `PotInstanceId`, `Build`, `FitsTheDay`, `PoseAt`, `PotAt`, `Move`, `Dwell`, `UnderWay`, `DayIndex`, `DayStartSeconds`, `DepartSeconds`, `ReturnSeconds`, `PotCount`, `SetPositions`, `GaffStart`, `HaulStart`, `HaulEnd`, `SetStart`, `SetEnd` | NpcPotDayTests; NpcPotDayContentTests: Build/PoseAt/PotAt, repeatable positions and bounded schedule; NpcPotBoatsPlayTests (PlayMode). | Not every negative day/invalid scalar helper input; nested Builder methods are inside a private implementation. |
| [NpcPotDayDef.cs](../../Assets/_Project/Code/Fishing/NpcPotDayDef.cs) — Data/enum/constants only; no public method. | NpcPotDayContentTests; NpcPotDayTests: authored route/timing validity. | Data-only inputs, no NPC catch-composition proof. |
| [NpcPotDayLibrary.cs](../../Assets/_Project/Code/Fishing/NpcPotDayLibrary.cs) — Data/enum/constants only; no public method. | NpcPotDayContentTests: authored library entries. | Data-only library; no independent runtime publication lifecycle pin. |
| [PlacedTrap.cs](../../Assets/_Project/Code/Fishing/PlacedTrap.cs) — `Progress01`, `IsReady`, `StateAt`, `TryGetLastCatch`, `Configure`, `ResolveCatches`, `TryHaul`, `Trap`, `Bait`, `InstanceId`, `PlacementGameTimeSeconds`, `RegionId`, `BaitId`, `WorldSeed`, `Hauled` | PlacedTrapRuntimeTests; TrapHaulControllerTests: readiness/state, last catch, resolution, full/empty/full-hold haul and save recreation. | Configure overloads/readouts are exercised, not every invalid combination independently asserted. |
| [PlacedTrapCatch.cs](../../Assets/_Project/Code/Fishing/PlacedTrapCatch.cs) — `Resolve`, `ResolveMany`, `BuildWeightedPool` | PlacedTrapCatchTests; TrapFillTests: Resolve/ResolveMany/BuildWeightedPool seed, bait and per-index stream rules. | Direct null weighted-pool call, reused outputs, duplicate/case favours remain partial. |
| [PlacedTrapService.cs](../../Assets/_Project/Code/Fishing/PlacedTrapService.cs) — `SetAboardPotCounter`, `AboardPotCount`, `PlaceTrap`, `TryPlaceGated`, `TryPlacePreBaited`, `TryConsumeBaitFromStock`, `ResolveBait`, `HaulTrap`, `RemoveTrap`, `OnGameLoaded`, `RestoreFromSave`, `Configure`, `Live` | PlacedTrapRuntimeTests; TrapPlacementTests; PotStockGatingTests: stock/bait/depth gate, prebaited reset, DTO/seed restore and removals. | Low-level PlaceTrap bypasses gates; no hostile hold rollback or all dependency-lifecycle permutations. |
| [PotDeckWorkController.cs](../../Assets/_Project/Code/Fishing/PotDeckWorkController.cs) — `CurrentBeat`, `PublishBeat`, `OnControlModeChanged`, `SeedDeckGate`, `Configure`, `CountAboardOf`, `BringAboard`, `ClearPot`, `AutoResolvePot`, `TickWork`, `ResolveVerb`, `HasPotAboard`, `Pot`, `SetState`, `GearKeysLive`, `CurrentVerb` | PotDeckWorkControllerTests; PotStockGatingTests; DeckWorkLoopPlayTests (PlayMode): aboard source, work verbs/beats, pick/band/bait/reset and mode gates. | Every ResolveVerb/CurrentBeat/PublishBeat terminal transition not isolated; native deck look separate. |
| [PotRopeMath.cs](../../Assets/_Project/Code/Fishing/PotRopeMath.cs) — `Scope`, `IsDrowned`, `RiseMetersPerHour`, `Stream`, `FloatOffset`, `DrawnAtDepth`, `SeenThroughWater01` | PotRopeMathTests; NpcPotDayContentTests: scope/drowning, rise/current, float offset, depth drawing and visibility factors. | No rendered rope/buoy acceptance. |
| [RodFightDef.cs](../../Assets/_Project/Code/Fishing/RodFightDef.cs) — Data/enum/constants only; no public method. | RodFightDataTests; FishingV2ContractTests: valid authored fight tuning and invariants. | Data-only fields; not a balance freeze. |
| [RodFightMath.cs](../../Assets/_Project/Code/Fishing/RodFightMath.cs) — `PhaseFor`, `TensionRatePerSec`, `LandingRatePerSec`, `RodBend01`, `LineStrain01`, `SlackWindowOpen01`, `PullAloneSnapsBeforeLanding`, `MaintainOutbleedsTheRun`, `MaintainOutbleedsTheRunAtTheWorstStance`, `ProgressOutrunsTheGiveBack` | RodFightMathTests; RodFightDataTests: phases, rates, bend/strain/slack windows and snap/maintain/progress feasibility. | Every malformed float input not covered. |
| [RodFightMotionMath.cs](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs) — `Offset`, `DartDir`, `SteerAlignment` | RodFightMotionMathTests: Offset/DartDir/SteerAlignment replay, movement shapes and lean direction. | No fish animation/pixel acceptance. |
| [RodFightPhases.cs](../../Assets/_Project/Code/Fishing/RodFightPhases.cs) — `ToFishingPhase`, `TryFromFishingPhase`, `RunPressureScale`, `EffectiveRunPressure` | RodFightSimTests.PhaseMap_RoundTrips_TheTwoFightPhases; PhaseMap_RefusesNonFightPhases_WithTheSafeDefault; EffectiveRunPressure_ScalesMonotonically_AndIsNeutralAtTheDefault. | Unknown numeric enum values not exhaustively pinned. |
| [RodFightRhythm.cs](../../Assets/_Project/Code/Fishing/RodFightRhythm.cs) — `RodFightRhythm`, `Tick`, `IsRunning`, `Effort01` | RodFightRhythmTests: held-time effort build, ease recovery and reset-on-new-instance behaviour. | Nonfinite ticks/tuning not certified. |
| [RodFightSim.cs](../../Assets/_Project/Code/Fishing/RodFightSim.cs) — `FishOffset`, `RodFightSim`, `Tick`, `Tension01`, `Landing01`, `Result`, `IsOver`, `Phase`, `Effort01`, `SurfaceSeconds`, `FightSeconds`, `DartDir`, `LandingFloor01`, `Approach01`, `RoamRadiusScale` | RodFightSimTests; RodFightControllerTests: seeded fight, floor/approach/high-water/phase and sea/stance inputs. | Tick overload/readout terminal matrix partial; native presenter is separate. |
| [SchoolField.cs](../../Assets/_Project/Code/Fishing/SchoolField.cs) — `SchoolHabitatPatch`, `SchoolFieldEnvironment`, `SchoolReaction`, `SchoolFieldSample`, `SchoolField`, `FillCell`, `TrySample` | SchoolFieldTests; SchoolFieldRegressionTests: FillCell/TrySample, habitat/motion, bounded density, determinism and regression budget. | No implicit activation, catch reservation/depletion, graphics or allocation-counter claim from this audit. |
| [SchoolInfluence.cs](../../Assets/_Project/Code/Fishing/SchoolInfluence.cs) — `SchoolInfluence`, `AffinityFor`, `At`, `None`, `OnFish`, `BiteDelayScale` | FishSchoolModelTests; FishSchoolHonestyTests; VisibleFishTests: At/AffinityFor and bite/strength/depth falloff neutrality. | Malformed school contents and every numerical edge not exhaustive. |
| [SeaFightMath.cs](../../Assets/_Project/Code/Fishing/SeaFightMath.cs) — `TensionPerSec`, `BiteDelayScale`, `WeightBias01`, `BiasedWeight01`, `MaintainOutbleedsTheEverydaySea`, `AStormCanBeatYou` | SeaFightMathTests; RodFightDataTests; CatchResolverBoundaryTests: sea tension/delay/weight, neutral off switches, maintain/storm feasibility. | Math proof does not certify sea appearance. |
| [ShoalEventMath.cs](../../Assets/_Project/Code/Fishing/ShoalEventMath.cs) — `AnimShape`, `DurationSeconds`, `At`, `FrameAt`, `JumpArc01`, `ScatterAt`, `ScatterStrength01`, `ScatterArc01`, `ScatterOffset` | VisibleFishTests; HookedJumperTests; GullSplashScatterTests: events/duration/frame/arc/scatter deterministic geometry. | No new rig/animation/pixel approval. |
| [ShoalMath.cs](../../Assets/_Project/Code/Fishing/ShoalMath.cs) — `Swimmer`, `Mulberry32`, `Next`, `InitialState`, `SeedFor`, `DirOf`, `Fill`, `Offset` | VisibleFishTests; VisibleFishSeenTests; FishDutyCycleAtTheLandingTests: seeded swimmers, heading and fill parity. | Public generator/initial-state methods are used by fixtures; all raw malformed inputs not separately pinned. |
| [SpeciesCatchItemFactory.cs](../../Assets/_Project/Code/Fishing/SpeciesCatchItemFactory.cs) — `TryCreate` | SpeciesCatchItemFactoryTests: failure output, canonical metadata, midpoint size and default freshness. | Serialization and registrar bootstrap not proved. |
| [StableHash.cs](../../Assets/_Project/Code/Fishing/StableHash.cs) — `Fold`, `Finalize`, `TrapCatchSeed`, `CatchSeed` | CatchRollDeterminismTests.CatchSeed_MatchesGoldenVectorsAndLegacyTrapSeed; PlacedTrapCatchTests; DeckWorkMathTests: seed lineage and channel/identity changes. | Not every public Fold/Finalize input has its own golden vector. |
| [StartingBait.cs](../../Assets/_Project/Code/Fishing/StartingBait.cs) — `GrantOnce`, `Grant`, `Configure` | FishingSupplyContractTests; StartingBaitOnceTests: Grant merge/refusal, GrantOnce flag/save behaviour and Configure-driven quantities. | Start/SaveReady real-load lifecycle unpinned; F3 overflow. |
| [StrikeGesture.cs](../../Assets/_Project/Code/Fishing/StrikeGesture.cs) — `BackwardSpeed`, `Reset`, `Tick` | StrikeGestureTests: BackwardSpeed, Reset/Tick, axis/deadzone/arming and strike edges. | Hardware delivery and every malformed pointer value outside scope. |
| [SwimmerVisibility.cs](../../Assets/_Project/Code/Fishing/SwimmerVisibility.cs) — `For`, `Draws` | VisibleFishTests; FishDutyCycleAtTheLandingTests; VisibleFishPlatePlayTests (PlayMode): For/Draws alpha/scale eligibility. | Logical visibility does not prove unobscured screen pixels. |
| [TackleDef.cs](../../Assets/_Project/Code/Fishing/TackleDef.cs) — Data/enum/constants only; no public method. | No direct TackleDef assertion found; BaitAndTackleTests pins the resolver's lure weights. | Data-only fields; asset validation and every authored field invariant remain unpinned. |
| [TrapDef.cs](../../Assets/_Project/Code/Fishing/TrapDef.cs) — Data/enum/constants only; no public method. | ContentValidationTests; DeckWorkContentValidationTests; TrapKitContentValidationTests; PotOfferContentValidationTests: identity, catch/rules/kit/stock references. | Data fields only; no art change or tuning freeze. |
| [TrapFill.cs](../../Assets/_Project/Code/Fishing/TrapFill.cs) — `Fill01`, `SlotHash`, `AnimalCatchSeed`, `ResolveCount` | TrapFillTests; PassiveGearBoundaryTests: Fill01/ResolveCount monotonic fill, hashes/streams and strict threshold. | Invalid/huge capacities and nonfinite values not comprehensive. |
| [TrapHaulController.cs](../../Assets/_Project/Code/Fishing/TrapHaulController.cs) — `OnControlModeChanged`, `TryStartHaul`, `CancelHaul`, `TickHaul`, `RopeStartPoint`, `Configure`, `IsHauling`, `Line01`, `Strain01`, `GearKeysLive` | TrapHaulControllerTests; TrapDeckGatingTests; PotDeckWorkControllerTests: start/cancel/tick, line/readout/events and RopeStartPoint. | Destruction/service-loss terminal permutations partial. |
| [TrapHaulMath.cs](../../Assets/_Project/Code/Fishing/TrapHaulMath.cs) — `LiftSignal`, `HoldLineRate`, `SwellRopeLoad01`, `FightStrain01`, `RopeStrain01`, `RopeTaut01`, `SampleHaulRope` | TrapHaulMathTests: LiftSignal/HoldLineRate and all public load/strain/rope sample helpers. | Logical curve only; no pixel guarantee. |
| [TrapPlacement.cs](../../Assets/_Project/Code/Fishing/TrapPlacement.cs) — `IsDeepEnough`, `CanPlaceAt`, `DepthAt` | TrapPlacementTests: IsDeepEnough/CanPlaceAt; depth calculation exercised by placement paths. | DepthAt missing-service sentinel not directly asserted; F2 nonfinite depth. |
| [TrapSoak.cs](../../Assets/_Project/Code/Fishing/TrapSoak.cs) — `SoakSeconds`, `ElapsedSeconds`, `Progress01`, `IsReady`, `SecondsPerHour` | TrapSoakTests; PassiveGearBoundaryTests: live and explicit SoakSeconds/Progress01/IsReady, elapsed and SecondsPerHour. | Malformed soak/timestamps not fully specified. |
| [TrapSplashFx.cs](../../Assets/_Project/Code/Fishing/TrapSplashFx.cs) — `Play`, `ActiveCount` | No isolated Play invocation assertion found; TrapKitContentValidationTests validates the supplied frames/fps. | Pooling, playback/lifetime and visible burst require native/art proof; not inferred from content validity. |
| [Core/Save/PotLocker.cs](../../Assets/_Project/Code/Core/Save/PotLocker.cs) — `OwnedCount`, `AddOwned`, `DeployedCount`, `AvailableCount` | PotLockerTests; FishingSupplyContractTests: counts, merge, invalid requests, conservation, null lists and read-only queries. | F3; duplicate rows and all overflow policies remain unpinned. |
