# Simulation determinism audit

Audited base: `51d30dca25fa836696be476b8b134b9ff990a9bd`.
Scope: all 1,043 runtime C# files under `Assets/_Project/Code`, excluding Editor
directories. No production code, authored content, prices, scenes, or save schema
changes accompany this audit. P1 and P3 depend on a sea and working coast that can
be reconstructed after loading.

## Meaning of deterministic

Autonomous world models should reconstruct from world seed, game time, authored
content and explicit spatial inputs. Player actions additionally require the same
input history and persisted mutations: a purchased boat or a caught fish cannot
be inferred from seed and clock alone. A seeded, stateful fight is reproducible
given the same input/delta sequence; that does not establish arbitrary frame-rate
independence. Unity physics, rendering and floating point across different engine
or runtime versions are not certified here.

The scan removes comments and literals before checking entropy and time calls;
it inventories collection declarations and iteration sites separately, then traces
their consumers. It found 200 `Time` lines, 26 RNG/declaration candidate lines,
155 collection declaration lines across 68 files, and 445 iteration/query lines.
A supplemental hash search found the process-dependent presentation seeds below.
There are **no executable `UnityEngine.Random`, `DateTime`, `DateTimeOffset`,
`Guid.NewGuid` or `TickCount` calls** in the scanned runtime source. Four
parameterless `System.Random` constructors remain; all are the null fallbacks in
D09. Explicit RNG parameters and constructor calls are not themselves defects.
This is a source audit with targeted tests, not a whole-game replay certificate.

## Findings, ranked by player risk

Paths in this section are relative to `Assets/_Project/Code`; line numbers refer
to the audited base. A reproduction below describes a failing case for a future
fix; it is not a deliberately failing test committed to the suite.

| ID / risk | Evidence and player consequence | Failing case / disposition |
| --- | --- | --- |
| D01 / high, confirmed state omission | `Economy/Market.cs:40,101,119,140,147` holds supply only in a component dictionary; a reconstructed market starts unglutted. Prices can recover by leaving/reloading instead of waiting. | Register sales, reconstruct the market at the same seed/time, compare supply and quote. See the existing [market persistence proposal](market-supply-persistence.md). Fix needs a persistence contract and changes sale outcomes; excluded from this audit and trial. |
| D02 / medium, confirmed clock-step discrepancy | `Environment/GameClock.cs:144-160` publishes only the final day when an ordinary `Advance` crosses several midnights. `FastForwardTo:105-127` explicitly publishes each day. `Economy/Market.cs:140` settles once per event. | Compare three one-day `Advance` calls with one three-day call: final clock agrees, day-event count and supply disagree. Ordinary frames rarely span days; extreme time scale or an injected step exposes it. A fix needs intermediate event/clock semantics and affects market outcomes. |
| D03 / medium, confirmed historical integration | `Boats/AmbientFleetPresenter.cs:208-231,190-195,626-660` integrates steering and buoy transitions from the last clock sample; plans refresh by day. `AmbientFleetSteering.cs:35-46,225-270` adds live obstacle forces and integrates position/heading. | Load/seek into the same working interval after different steering histories: schedule target agrees, integrated pose and transition need not. Same-day world-seed replacement does not itself satisfy the day-change invalidation. Requires reconstruction/avoidance policy and art review; do not replace it with a snap. |
| D04 / medium, restore policy incomplete | `World/Routines/VillagerRoutine.cs:216-251,262-273` samples a clock plan, then overlays a conversation hold and frame-delta catch-up; shelter and seed invalidation are throttled by unscaled time. | Compare cold load, re-enable and in-place seek during/releasing a conversation; displayed/talkable state can differ despite the same planned pose. A temporarily unavailable plan dependency can also leave a latched failed plan. The underlying schedule remains deterministic. Existing routine restore design work owns these policy questions. |
| D05 / medium, external geometry/arbitration | `Vehicles/ScheduledTrip.cs:154-190,239-304,339-406` samples a pure trip plan but obtains door/trailer geometry from live machines and arbitrates seats/trailers. Failed planning can latch before late mesh readiness. | Load mid-trip with a trailer at its mid-trip transform, or change which claimant enables first; the accepted plan/driver visibility may differ. Needs authored start geometry and claim/refusal policy, not a seeded RNG patch. |
| D06 / medium, conditional roster timing | `World/Beach/BeachLifeDirector.cs:130-164,181-201` correctly sorts routine IDs, but the input roster is discovered live and cached on wall time; that rank is used to build the day's outing. | Enable the same villagers in different batches around `RosterTrustSeconds`, then request a plan before the next scan. The roster membership/rank can differ. `VillagerRoutine` can retain the resulting day plan. Proposed failing case, not reproduced in an editor here. PR 1095 owns the beach implementation. |
| D07 / medium, session history by design needs restore ruling | `World/Conversations/NpcConversationDirector.cs:68-78,187-213,228-238` tracks already-run days/cooldowns and a live roster. `World/InnerVoiceDirector.cs:148,211-216` uses wall-time cooldowns. | Reload after hearing an exchange or inner line; session history and first eligible tick differ. Some repetition suppression is presentation policy, not an autonomous clock model. Decide what a restore preserves before adding save fields or changing dialogue timing. |
| D08 / medium boundary, not a new autonomous defect | `Fishing/DevFishingInput.cs:208`, `PotDeckWorkController.cs:370`, `TrapHaulController.cs:248`, `Player/MooringController.cs:155`, `Vehicles/VehicleController.cs:296`, `Boats/BoatFuelTank.cs:435` consume frame/fixed deltas with player state. | Equal final clock alone cannot reproduce a cast, held work, fuel burn or vehicle motion. A valid replay must include controls, fixed-step sequence, collisions and transactions. Tests that replay identical input sequences are valid; do not relabel them frame-partition proofs. |
| D09 / low live risk, confirmed latent entropy | `Fishing/BiteSequenceSim.cs:94`, `FishFight.cs:117`, `RodFightRhythm.cs:53`, `RodFightSim.cs:99` create unseeded RNGs for null callers. Live controller callers pass an RNG. | Construct two null-RNG fights and compare traces: reproducibility is not guaranteed. PR 1077 already removes this fallback; do not duplicate or edit its files. |
| D10 / low gameplay risk, confirmed presentation entropy | `Art/BoatSpotlight.cs:622-623` and `Boats/BoatWakeEmitter.cs:1003` seed presentation from `string.GetHashCode`; spotlight flicker also reads wall time. | Across processes/runtimes, matching seed/time need not reproduce flicker/wake phase. They do not select catch, price or saved mutations. Tier C visual work; no acceptance of a different look is implied. PR 1098 owns wake work. |
| D11 / low, numerical/order risk | `Boats/AmbientFleetPresenter.cs:195` copies dictionary values into an obstacle buffer; `AmbientFleetSteering.cs:40-46` sums in buffer order. `RadarContactSource.cs:126-130` emits contacts in dictionary order. | Insert/remove/reinsert identical buoy entries in different orders; float summation can differ in the low bits, and contact list order differs. Material steering drift or a different radar image is **not measured here**. Stable ordering would touch presentation/steering; requires a visual proof. |
| D12 / bounded visual/interaction history | `Art/GullFlock.cs:340,360-391` uses a persistent decision stream, slow ticks and live bird state around a deterministic schedule. Perch claims and school reactions are external inputs. | Same schedule instant after different perching/splash histories is not a claim of the same bird pose. No theft/pot-fouling implementation was found in `SeagullGameplay` (those fields describe deferred behavior). Art review must distinguish ambient motion from fish-school reaction consequences. |
| D13 / low, conditional initialization boundary | `Fishing/ClamDig.cs:173-176,286` and `FishingController.cs:385-388,425` reseed each default action from seed/time, falling back to zero when services are absent. Explicit serialized overrides retain an action stream. | Trigger an action before environment/clock registration versus afterward: inputs differ. The repaired normal path is deterministic; readiness gating is a separate composition question. Do not undo the seed fix or claim it restores an unfinished player fight. |
| D14 / low, latent deterministic tie | `Fishing/ClamDigger.cs:103-113` chooses the first equally near exposed hole from Unity object discovery. The overload orders by instance identity rather than stable content ID. | Construct equidistant eligible holes in opposite creation orders: the selected hole can change. Runtime source has no caller of `TryDigNearest`; `NearestDiggable` is called only by that unused entry point. The live interaction path therefore does not establish a player-visible defect here. This is an engine-discovery ordering issue, not dictionary iteration. |

## Models and boundaries inspected

| Module / runtime files | Result and reused contracts |
| --- | --- |
| Environment / 7 | `TideModel`, `WeatherModel`, `CurrentModel` are explicit-input samples. Weather noise is integer-hashed; no mutable global RNG. `GameClock` is the sanctioned wall-time integration boundary, with separate seek, fast-forward, hold and flow semantics; D02 remains. `EnvironmentService` samples through Core. |
| Fishing / 74 | `StableHash`, `TrapFill`, `LonglineCatch`, `PlacedTrapCatch`, `FishSchoolMath/Model`, `SchoolField`, `DeckWork`, `NpcPotDay` use stable identities and explicit time/index streams. Weighted catch pools retain authored list order, so equal pools means equal order. Historical longline bite time is sampled at the bite, not rerolled at haul. Live school reactions, player actions and placement mutations are additional inputs. D09, D13, D14 delimit the guarantee. |
| Economy / 41 | Price/freshness/fee/fuel helpers are arithmetic over explicit inputs; buys and sales are ordered transactions. `Market` dictionary updates are independent by category; `SellService` prices in hold order and only enumerates category totals afterward. No RNG or wall time. D01 is a missing mutation restore, not unstable dictionary iteration. |
| World / 148 | `RoutineSchedule/Plan`, road/path plans, `BeachPlanning`, calendar/day schedules use explicit inputs. Hosts add discovery, holds, dialogue history, scene loading and claims (D04-D07). Region/voice/flag registries are keyed lookups. Terrain plan generation/validation is authoring computation rather than a running simulation; do not rebake or alter its ordering under this task. |
| Boats / 97 | Player physics, fuel, mooring, containers, trap holds, school/radar consumers, fleet schedule and steering were separated. `AmbientFleetSchedule` is clock-addressable; its presenter integrates additional state (D03/D11). Navigation and hold mutations are explicit, not inferred from seed alone. |
| Vehicles / 15 | `VehicleTripPlan` is clock-addressable; `ScheduledTrip` binds live geometry and claims. Player vehicle/tow physics require control and collision history. Doors use input/presentation time, not random scheduling. |
| Core / 285 | Calendar, freshness, fuel, navigation-light plans, stable schedule and save contracts inspected. Registries/events coordinate state; insertion order is not a world seed. `SnagTargets` uses an indexed list plus lookup dictionary; claims depend on explicit registration/player state. Object/hash implementations used as cache keys do not automatically seed a simulation. |
| App / 34 | Composition, restore, arrival input and NPC pot host. `NpcPotBoats.cs:177-215` invalidates by day **and seed and day length**, reconstructs today/yesterday, and samples absolute time; this is stronger than D03's older fleet host. Its slow water/rope refresh is presentation timing. PR 1097 owns further deck work. |
| Player / 39 | Walking, boarding, deck movement, mooring and fights consume controls/deltas. Animators and feedback are presentation; no unseeded RNG found. A world-model replay does not replace input playback. |
| Art / 204 | Included in entropy/time/collection inventory because living animals and vegetation can cross the gameplay boundary. Pure schedules/growth inputs differ from mutable render/perch/reaction state. D10/D12 and dictionary seaweed obstacle order are reserved for art review; no look is judged or changed. |
| UI / 88; Audio / 11 | Clocks are alarm/blink/sweep/fade/reading/mix timers and interaction-frame guards. Cache hashes are not simulation seeds. Instrument/contact ordering can affect presentation, so no promise of pixel reproducibility follows from the pure-model tests. |

## Collection order dispositions

The following appendix lists **every file declaring a dictionary or hash set**.
Declarations alone are not findings. Important consumer distinctions:

- `Market` enumerates keys only to apply the same independent operation to each
  category. `SellService` enumerates totals after computing payout in hold order.
- `NavLightPhasePlan` groups by character, then **ordinal-sorts IDs inside each
  group**. Groups write disjoint keys; group iteration cannot change phases.
- `FlagStore`, `LicenseService`, `FishSpeciesRegistry`, region/voice/icon/services
  registries, missing-resource warning sets, and perch claims use keyed lookup or
  membership. No dictionary traversal feeds a random roll in these paths.
- Conversation selection follows the authored conversation list; its dictionaries
  track keyed villagers/history. Duplicate IDs in discovered objects remain a
  content/registration ambiguity, not evidence that dictionary traversal selects
  a conversation.
- `AmbientFleetPresenter` and `RadarContactSource` expose order directly (D11).
  `SeaweedPresenter` similarly copies buoy entries for presentation avoidance.
- Notebook/catalog results preserve a separate ordered list or explicitly sort
  content IDs. Terrain/import/validation collections can affect generated assets
  or diagnostics, but are outside runtime behavioral changes; no bake was run.
- Render batching, caches, palettes, dirty-chunk sets and disposal loops are
  presentation concerns. They are inventoried, not certified pixel-equivalent.

## Verification

`SimulationReplayAuditTests` adds 26 cases: 4 channel replay, 4 pot-fill replay,
4 routine reconstruction, 4 current-field reverse sampling, 4 freshness clock
jumps, 1 navigation grouping-order guard, 4 tide/weather reconstruction and
1 market insertion-order guard. Twenty-one pure cases executed successfully
under .NET against freshly compiled current sources. Core (285), Environment
(7), Fishing (74), World (148), Economy (41), and the new fixture compiled with
zero errors. Five cases requiring Unity objects await the EditMode runner.
No local Unity run, scene transfer, visual plate or full-module green is claimed.

The fixture reuses production APIs rather than implementing another simulator.
Existing catch, tide, weather, fish-school, longline, trip, NPC pot-day, beach,
freshness and fleet tests remain unchanged. In particular, no failing-case
description above was turned into a weaker assertion of the defect.

All findings are documentation only. Follow-ups must retain the same distinctions:
saved player mutations, deterministic world samples, explicit input histories,
and presentation state require different proofs and restoration policies.

## Appendix A: complete collection-declaration inventory

Paths below are relative to `Assets/_Project/Code`. Lines are declaration locations;
consumer dispositions are explained above.

| File | Lines | Disposition |
| --- | --- | --- |
| `Art/CatchFillMath.cs` | 97 | Presentation cache/lookup/batching; no pixel certification |
| `Art/CatchItemLibrary.cs` | 105, 106, 116, 122 | Presentation cache/lookup/batching; no pixel certification |
| `Art/FoamInjectionRegistry.cs` | 170 | Presentation cache/lookup/batching; no pixel certification |
| `Art/GrassField.cs` | 587, 625 | Presentation cache/lookup/batching; no pixel certification |
| `Art/IsoCharacterFigureRenderer.cs` | 92 | Presentation cache/lookup/batching; no pixel certification |
| `Art/IsoFacetFigureTarget.cs` | 205 | Presentation cache/lookup/batching; no pixel certification |
| `Art/IsoFacetHullFeature.cs` | 372, 415, 418, 424, 429, 433, 438 | Presentation cache/lookup/batching; no pixel certification |
| `Art/IsoFacetHullRegistry.cs` | 29 | Presentation cache/lookup/batching; no pixel certification |
| `Art/IsoFacetHullRenderer.cs` | 183 | Presentation cache/lookup/batching; no pixel certification |
| `Art/IsoFacetMath.cs` | 162, 171 | Presentation cache/lookup/batching; no pixel certification |
| `Art/LampShadowSystem.cs` | 123, 162 | Presentation cache/lookup/batching; no pixel certification |
| `Art/MoodGradeDirector.cs` | 66 | Presentation cache/lookup/batching; no pixel certification |
| `Art/PlantField.cs` | 53, 55, 56, 59, 60, 169, 235, 240 | Presentation cache/lookup/batching; no pixel certification |
| `Art/RoadFloorManifest.cs` | 81 | Presentation cache/lookup/batching; no pixel certification |
| `Art/SeagullGameplay.cs` | 251, 252 | Presentation cache/lookup/batching; no pixel certification |
| `Art/SeagullVisualDef.cs` | 173, 220 | Presentation cache/lookup/batching; no pixel certification |
| `Art/SeaweedPresenter.cs` | 72 | Buoy buffer reaches presentation avoidance |
| `Art/ShoreLineDerivation.cs` | 344, 518 | Presentation cache/lookup/batching; no pixel certification |
| `Art/SilhouetteMaskFeature.cs` | 129 | Presentation cache/lookup/batching; no pixel certification |
| `Art/SunCasterInstaller.cs` | 25, 28 | Presentation cache/lookup/batching; no pixel certification |
| `Art/TerrainLight6.cs` | 136, 338, 348 | Presentation cache/lookup/batching; no pixel certification |
| `Art/UnderWaterRegistry.cs` | 41 | Presentation cache/lookup/batching; no pixel certification |
| `Boats/AmbientFleetPresenter.cs` | 53 | D03/D11: ordered buffer reaches steering |
| `Boats/BoatInteriorCells.cs` | 41 | Presentation cache/lookup/batching; no pixel certification |
| `Boats/DeckContainerPresenter.cs` | 323, 331 | Presentation cache/lookup/batching; no pixel certification |
| `Boats/RadarContactSource.cs` | 52 | D11: contact output order |
| `Boats/SternDeckGearPresenter.cs` | 686 | Presentation cache/lookup/batching; no pixel certification |
| `Boats/TrapBuoyPresenter.cs` | 49 | Presentation cache/lookup/batching; no pixel certification |
| `Core/Environment/SnagTargets.cs` | 61 | Dictionary lookup; separate list supplies targets |
| `Core/Environment/WindProfileDef.cs` | 77, 78 | Keyed lookup, membership, registry or warning suppression |
| `Core/IGullPerch.cs` | 20 | Keyed lookup, membership, registry or warning suppression |
| `Core/Iso/CarryAnchorTableDef.cs` | 209, 411, 413 | Presentation cache/lookup/batching; no pixel certification |
| `Core/Iso/CharacterClothingValidation.cs` | 25, 48, 70, 90, 97, 127, 128, 129, 186, 188 | Presentation cache/lookup/batching; no pixel certification |
| `Core/Iso/CharacterSkinStateMap.cs` | 75, 76 | Presentation cache/lookup/batching; no pixel certification |
| `Core/Nav/NavLightPhasePlan.cs` | 74, 76, 79 | Groups independent; IDs ordinal-sorted |
| `Core/Services/GameServices.cs` | 14 | Keyed lookup, membership, registry or warning suppression |
| `Core/Services/IconRegistry.cs` | 31 | Keyed lookup, membership, registry or warning suppression |
| `Core/Services/RegionDisplayNames.cs` | 27 | Keyed lookup, membership, registry or warning suppression |
| `Core/Vehicles/VehicleKind.cs` | 66, 67 | Keyed lookup, membership, registry or warning suppression |
| `Economy/LicenseService.cs` | 28 | Keyed lookup, membership, registry or warning suppression |
| `Economy/Market.cs` | 40 | Independent per-category settlement |
| `Economy/SellScreen.cs` | 119 | Membership deduplication; hold order retained |
| `Economy/SellService.cs` | 171 | Independent category totals after ordered sale |
| `Fishing/FishEcologyValidation.cs` | 52 | Validator membership/lookup |
| `Fishing/FishSpeciesRegistry.cs` | 34 | Keyed lookup, membership, registry or warning suppression |
| `Fishing/FishSwimSpriteLibrary.cs` | 98, 99, 100, 113, 119, 125 | Keyed lookup, membership, registry or warning suppression |
| `Fishing/PotDeckWorkController.cs` | 121 | Keyed lookup, membership, registry or warning suppression |
| `Player/ControlSwitcher.cs` | 528, 529 | Keyed lookup, membership, registry or warning suppression |
| `Player/DeckRiderMeshPresenter.cs` | 138 | Presentation cache/lookup/batching; no pixel certification |
| `UI/CharacterCreatorPanel.cs` | 67, 68, 69 | Presentation cache/lookup/batching; no pixel certification |
| `UI/Draw/DepthRigRender.cs` | 88, 92, 93, 485, 487 | Presentation cache/lookup/batching; no pixel certification |
| `UI/Draw/FishRigRender.cs` | 183 | Presentation cache/lookup/batching; no pixel certification |
| `UI/Draw/RigDrawUtil.cs` | 273, 288, 294, 304 | Presentation cache/lookup/batching; no pixel certification |
| `UI/Draw/WatchRigRender.cs` | 106, 107 | Presentation cache/lookup/batching; no pixel certification |
| `World/Conversations/NpcConversationDirector.cs` | 68, 69, 73, 74, 77, 78 | D07: keyed roster/history; authored selection order |
| `World/DialogueVoiceCatalog.cs` | 29, 30 | Keyed lookup, membership, registry or warning suppression |
| `World/FlagStore.cs` | 21 | Keyed lookup, membership, registry or warning suppression |
| `World/InnerVoiceDirector.cs` | 61, 66 | D07: membership/cooldown history |
| `World/NotebookContent.cs` | 162, 163 | Grouping lookup; separate ordered tabs |
| `World/RegionRegistry.cs` | 14 | Keyed lookup, membership, registry or warning suppression |
| `World/TerrainPlan/GroundFileImport.cs` | 158, 229, 658 | Authoring/validation; no runtime change or rebake |
| `World/TerrainPlan/TerrainPlanDerivation.Part1.cs` | 185 | Authoring/validation; no runtime change or rebake |
| `World/TerrainPlan/TerrainPlanDerivation.Still.cs` | 426, 429 | Authoring/validation; no runtime change or rebake |
| `World/TerrainPlan/TerrainPlanFlood.cs` | 131 | Authoring/validation; no runtime change or rebake |
| `World/TerrainPlan/TerrainPlanMath.cs` | 518 | Authoring/validation; no runtime change or rebake |
| `World/TerrainPlan/TerrainPlanSources.cs` | 94, 122 | Authoring/validation; no runtime change or rebake |
| `World/TerrainPlan/TerrainPlanSourcesJson.cs` | 188, 264, 267, 268, 270, 273, 276, 279, 283, 290, 339, 341 | Authoring/validation; no runtime change or rebake |
| `World/TerrainPlan/TerrainPlanValidation.cs` | 47, 53, 68, 105, 106, 128, 150 | Authoring/validation; no runtime change or rebake |

## Appendix B: complete engine-time call-site inventory

Every executable `Time.*` candidate is included, including frame guards and
presentation. This prevents the narrower simulation conclusions from hiding
render/input exclusions.

| File | Lines | Boundary |
| --- | --- | --- |
| `App/ArrivalOpening.cs` | 1330, 1331, 1357, 1396, 1730 | Input/physics/work integration, sometimes also presentation |
| `App/CameraFollow.cs` | 702, 703, 1243 | Presentation, cache refresh or interaction-frame guard |
| `App/FightStrainCamera.cs` | 137, 152 | Presentation, cache refresh or interaction-frame guard |
| `App/HitStopHost.cs` | 71, 75, 81, 90, 98 | Presentation, cache refresh or interaction-frame guard |
| `App/NmcWharfCraneVisual.cs` | 14, 17 | Presentation, cache refresh or interaction-frame guard |
| `App/NpcPotBoats.cs` | 166, 169 | Slow presentation refresh; absolute clock pose plan |
| `Art/BoatSpotlight.cs` | 584, 596, 623 | Presentation, cache refresh or interaction-frame guard |
| `Art/ChimneySmoke.cs` | 107 | Presentation, cache refresh or interaction-frame guard |
| `Art/DayNightController.cs` | 133 | Presentation, cache refresh or interaction-frame guard |
| `Art/DisplacedWaterSurface.cs` | 309 | Presentation, cache refresh or interaction-frame guard |
| `Art/DustMotes.cs` | 89, 109 | Presentation, cache refresh or interaction-frame guard |
| `Art/FoamInjectionRegistry.cs` | 399 | Presentation, cache refresh or interaction-frame guard |
| `Art/FoamInjector.cs` | 282, 305, 433, 454 | Presentation, cache refresh or interaction-frame guard |
| `Art/GrassDevWalker.cs` | 32 | Presentation, cache refresh or interaction-frame guard |
| `Art/GrassDevWind.cs` | 35, 36 | Presentation, cache refresh or interaction-frame guard |
| `Art/GrassFootstep.cs` | 300, 301 | Presentation, cache refresh or interaction-frame guard |
| `Art/GrassWindBridge.cs` | 73 | Presentation, cache refresh or interaction-frame guard |
| `Art/GullFlock.cs` | 360 | Host/session scheduling boundary; ranked findings |
| `Art/HoldCatchFillSource.cs` | 153, 154 | Presentation, cache refresh or interaction-frame guard |
| `Art/InteractAffordancePresenter.cs` | 218, 375 | Presentation, cache refresh or interaction-frame guard |
| `Art/IsoFacetFigureTarget.cs` | 98, 115 | Presentation, cache refresh or interaction-frame guard |
| `Art/IsoFacetHullFeature.cs` | 879, 881 | Presentation, cache refresh or interaction-frame guard |
| `Art/LampPoolSystem.cs` | 167 | Presentation, cache refresh or interaction-frame guard |
| `Art/LampShadowSystem.cs` | 284 | Presentation, cache refresh or interaction-frame guard |
| `Art/MomentBurstEmitter.cs` | 275 | Presentation, cache refresh or interaction-frame guard |
| `Art/MoodGradeDirector.cs` | 172 | Presentation, cache refresh or interaction-frame guard |
| `Art/MoonCycle.cs` | 113 | Presentation, cache refresh or interaction-frame guard |
| `Art/NavLight.cs` | 168 | Presentation, cache refresh or interaction-frame guard |
| `Art/PlantField.cs` | 184, 188, 189 | Presentation, cache refresh or interaction-frame guard |
| `Art/PlayerOutfitVisual.cs` | 226 | Presentation, cache refresh or interaction-frame guard |
| `Art/PlayerShadowInstaller.cs` | 67 | Presentation, cache refresh or interaction-frame guard |
| `Art/PlayerSilhouetteInstaller.cs` | 57 | Presentation, cache refresh or interaction-frame guard |
| `Art/PlayerSubmergeVisual.cs` | 298, 430 | Presentation, cache refresh or interaction-frame guard |
| `Art/RainEmitter.cs` | 132 | Presentation, cache refresh or interaction-frame guard |
| `Art/ReflectiveObject.cs` | 142 | Presentation, cache refresh or interaction-frame guard |
| `Art/SceneLight.cs` | 325, 438 | Presentation, cache refresh or interaction-frame guard |
| `Art/SeaMistEmitter.cs` | 107 | Presentation, cache refresh or interaction-frame guard |
| `Art/SeaweedPresenter.cs` | 209, 211, 226, 227 | Presentation, cache refresh or interaction-frame guard |
| `Art/SetPieceWindVane.cs` | 58 | Presentation, cache refresh or interaction-frame guard |
| `Art/ShorePlantTideDriver.cs` | 105, 106 | Presentation, cache refresh or interaction-frame guard |
| `Art/SprayEmitter.cs` | 146 | Presentation, cache refresh or interaction-frame guard |
| `Art/SpriteShadow.cs` | 505 | Presentation, cache refresh or interaction-frame guard |
| `Art/SunCasterInstaller.cs` | 79 | Presentation, cache refresh or interaction-frame guard |
| `Art/SurfSprayEmitter.cs` | 121 | Presentation, cache refresh or interaction-frame guard |
| `Art/TerrainSplatSurface.cs` | 423 | Presentation, cache refresh or interaction-frame guard |
| `Art/WadeSplashEmitter.cs` | 310 | Presentation, cache refresh or interaction-frame guard |
| `Art/WaterSurface.cs` | 675, 679 | Presentation, cache refresh or interaction-frame guard |
| `Art/WaveFieldBridge.cs` | 215 | Presentation, cache refresh or interaction-frame guard |
| `Audio/AudioDirector.cs` | 224 | Presentation, cache refresh or interaction-frame guard |
| `Audio/FishingAudio.cs` | 155, 172 | Presentation, cache refresh or interaction-frame guard |
| `Boats/AmbientFleetPresenter.cs` | 202, 204, 217, 220 | Host/session scheduling boundary; ranked findings |
| `Boats/BoatCabinDoor.cs` | 400, 402 | Presentation, cache refresh or interaction-frame guard |
| `Boats/BoatFuelTank.cs` | 435 | Input/physics/work integration, sometimes also presentation |
| `Boats/BoatMooring.cs` | 704 | Input/physics/work integration, sometimes also presentation |
| `Boats/BoatRotationTestRig.cs` | 65 | Presentation, cache refresh or interaction-frame guard |
| `Boats/BoatRowAnimator.cs` | 83 | Presentation, cache refresh or interaction-frame guard |
| `Boats/BoatWakeEmitter.cs` | 338, 378 | Presentation, cache refresh or interaction-frame guard |
| `Boats/BoatWaveMotion.cs` | 443, 444 | Presentation, cache refresh or interaction-frame guard |
| `Boats/BuoyWaveVisual.cs` | 262, 263 | Presentation, cache refresh or interaction-frame guard |
| `Boats/DeckContainerPresenter.cs` | 171, 173 | Presentation, cache refresh or interaction-frame guard |
| `Boats/DeckFreshnessTag.cs` | 89 | Presentation, cache refresh or interaction-frame guard |
| `Boats/DevBoatInput.cs` | 239, 292 | Presentation, cache refresh or interaction-frame guard |
| `Boats/DoryOarLayer.cs` | 185 | Presentation, cache refresh or interaction-frame guard |
| `Boats/DoryOarMeshLayer.cs` | 125 | Presentation, cache refresh or interaction-frame guard |
| `Boats/HelmControlRelay.cs` | 334 | Presentation, cache refresh or interaction-frame guard |
| `Boats/MastheadTelltale.cs` | 78, 119 | Presentation, cache refresh or interaction-frame guard |
| `Boats/MeshHullDriver.cs` | 307 | Presentation, cache refresh or interaction-frame guard |
| `Boats/OutboardMotorLayer.cs` | 342 | Presentation, cache refresh or interaction-frame guard |
| `Boats/OutboardMotorMeshLayer.cs` | 153 | Presentation, cache refresh or interaction-frame guard |
| `Core/Environment/WaveFieldAnimator.cs` | 108, 112 | Presentation, cache refresh or interaction-frame guard |
| `Core/Interaction/CancelKeyClaim.cs` | 52, 61 | Presentation, cache refresh or interaction-frame guard |
| `Core/Iso/CharacterClipPlayer.cs` | 285 | Presentation, cache refresh or interaction-frame guard |
| `Core/Iso/IsoCharacterSprite.cs` | 270, 334 | Presentation, cache refresh or interaction-frame guard |
| `Environment/GameClock.cs` | 137 | Master clock integration; D02 |
| `Fishing/ClamDig.cs` | 211 | Reveal/spurt animation; catch roll uses game clock |
| `Fishing/ClamHoleVisual.cs` | 134 | Presentation, cache refresh or interaction-frame guard |
| `Fishing/DevFishingInput.cs` | 208 | Input/physics/work integration, sometimes also presentation |
| `Fishing/FishSchoolPresenter.cs` | 180 | Presentation, cache refresh or interaction-frame guard |
| `Fishing/PotDeckWorkController.cs` | 370, 705, 755, 803 | Input/physics/work integration, sometimes also presentation |
| `Fishing/TrapHaulController.cs` | 248, 484, 485, 643 | Input/physics/work integration, sometimes also presentation |
| `Fishing/TrapSplashFx.cs` | 123 | Presentation, cache refresh or interaction-frame guard |
| `Player/ControlSwitcher.cs` | 826, 831, 3130 | Input/physics/work integration, sometimes also presentation |
| `Player/DeckRiderMeshPresenter.cs` | 152, 164 | Presentation, cache refresh or interaction-frame guard |
| `Player/DeckRiderVisual.cs` | 1146 | Presentation, cache refresh or interaction-frame guard |
| `Player/DeckWalkController.cs` | 1051, 1066, 1085, 1112, 1125, 1203 | Input/physics/work integration, sometimes also presentation |
| `Player/DevToast.cs` | 109 | Presentation, cache refresh or interaction-frame guard |
| `Player/LiftFeedback.cs` | 87 | Presentation, cache refresh or interaction-frame guard |
| `Player/MooringController.cs` | 155 | Input/physics/work integration, sometimes also presentation |
| `Player/PlayerFishingAnimator.cs` | 310 | Presentation, cache refresh or interaction-frame guard |
| `Player/PlayerHaulAnimator.cs` | 416 | Presentation, cache refresh or interaction-frame guard |
| `Player/PlayerSleepPresenter.cs` | 129 | Presentation, cache refresh or interaction-frame guard |
| `Player/PlayerWalkController.cs` | 473, 526, 527, 528 | Input/physics/work integration, sometimes also presentation |
| `Player/RodFightPresenter.cs` | 410, 695, 959 | Presentation, cache refresh or interaction-frame guard |
| `UI/FishFinderOverlayHost.cs` | 209, 212 | Presentation, cache refresh or interaction-frame guard |
| `UI/HelmOverlayHost.cs` | 290, 337, 490 | Presentation, cache refresh or interaction-frame guard |
| `UI/HudController.cs` | 403, 716, 749 | Presentation, cache refresh or interaction-frame guard |
| `UI/RadarOverlayHost.cs` | 289, 461, 462 | Presentation, cache refresh or interaction-frame guard |
| `UI/RegionFadeOverlay.cs` | 105 | Presentation, cache refresh or interaction-frame guard |
| `UI/SounderOverlayHost.cs` | 170 | Presentation, cache refresh or interaction-frame guard |
| `UI/WardrobePicker.cs` | 176, 241, 323, 345 | Presentation, cache refresh or interaction-frame guard |
| `Vehicles/VehicleController.cs` | 296 | Input/physics/work integration, sometimes also presentation |
| `Vehicles/VehicleDoors.cs` | 150 | Presentation, cache refresh or interaction-frame guard |
| `World/AmbientSpeechPresenter.cs` | 362, 684 | Presentation, cache refresh or interaction-frame guard |
| `World/Beach/BeachLifeDirector.cs` | 183 | Host/session scheduling boundary; ranked findings |
| `World/BuildingInterior.cs` | 525 | Input/physics/work integration, sometimes also presentation |
| `World/Conversations/NpcConversationDirector.cs` | 187 | Host/session scheduling boundary; ranked findings |
| `World/DialoguePresenter.cs` | 534 | Presentation, cache refresh or interaction-frame guard |
| `World/InnerVoiceDirector.cs` | 148, 211, 216 | Host/session scheduling boundary; ranked findings |
| `World/NotebookPresenter.cs` | 403, 591, 986 | Presentation, cache refresh or interaction-frame guard |
| `World/OnboardingDirector.cs` | 113 | Presentation, cache refresh or interaction-frame guard |
| `World/RegionPassage.cs` | 90, 109, 143 | Interaction re-entry debounce, not world schedule |
| `World/Routines/VillagerRoutine.cs` | 233, 241, 243 | Host/session scheduling boundary; ranked findings |
| `World/WharfLadder.cs` | 154, 160 | Presentation, cache refresh or interaction-frame guard |

## Appendix C: RNG construction sites

| Source under `Assets/_Project/Code` | Lines | Contract |
| --- | --- | --- |
| `Fishing/BiteSequenceSim.cs` | 94 | Unseeded null fallback, D09 / PR 1077 |
| `Fishing/ClamDig.cs` | 176 | Explicit stable seed; same ordered inputs required |
| `Fishing/FishFight.cs` | 117 | Unseeded null fallback, D09 / PR 1077 |
| `Fishing/FishingController.cs` | 388 | Explicit stable seed; same ordered inputs required |
| `Fishing/LonglineCatch.cs` | 76 | Explicit stable seed; same ordered inputs required |
| `Fishing/PlacedTrapCatch.cs` | 111 | Explicit stable seed; same ordered inputs required |
| `Fishing/RodFightRhythm.cs` | 53 | Unseeded null fallback, D09 / PR 1077 |
| `Fishing/RodFightSim.cs` | 99 | Unseeded null fallback, D09 / PR 1077 |
| `UI/CharacterCreatorModel.cs` | 20 | Unseeded null fallback, D09 / PR 1077 |
