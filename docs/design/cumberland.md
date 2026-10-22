# Cumberland — CMB0 on the land-region template

The owner's 2026-10-22 deployment starts Cumberland's first wave, following Rice Point on the shared
land-region template. The place comes now; the marina's purchase, charters and the resort's business
remain M3, with economy-sim. P3 and P1 lead this wave. This is Phase A, checked headlessly: scene creation,
its build-list entry, visual acceptance and runtime measurements belong to Phase B.

## Source, region and ground

Source: `HH-cumberland-ground-part-1-2026-10-11`, accepted part 1 after pass C. The sums manifest is
SHA-256 `8688420dc47bbf11fb7f5dc7569d98ee492f0978cc307e10bb5e1bb0b8436210`, 53 entries. All entries
re-hashed correctly before reading the package. Only its `ground/plan.json` and the two ground PNGs
are imported. No checker, kit, tool, picture or input file is imported. Future-wave geometry retained
in the source document is evidence, never a placement instruction for CMB0.

`Cumberland.asset` defines `region.cumberland`, display name Cumberland, scene `Cumberland`, centre
(0, 0), 760 × 560 m, two texels per metre. Tide mean 0 and amplitude 2.2 m match the source and NMC;
phase 1 hour comes from NMC's RegionDef because the package does not carry a phase. Wind uses NMC's
existing profile. There are no swell fields or swell code in this wave. The marina basin, behind its
point, rubble breakwater and short jetty, is the first candidate for W4-5's shelter review.

`GroundFileIntake.Cumberland.cs` accepts only `hidden-harbours/cmb-ground-plan@1`, `terrain_plan.cmb`
and `region.cumberland`. It validates the zero-centred frame, finite positive dimensions and resolution,
tide, range, integral raster dimensions, 16-bit single-channel format, exact PNG hashes and decoded
pixel hashes before CREATE or REFRESH can write. The original `GroundFileIntake.cs` check is unchanged.

`CumberlandSeabed.asset` binds the source's exact 1520 × 1120 rasters. Height is −6 + 12 × code / 65535;
the observed range is −6 to +6 m. Still code zero is absent. The brook and three ponds occupy 18,053
still-water texels: the brook pond is +4.60 m, the brook falls to −0.20 m and the two course ponds are
+5.00 m. Existing `PaintedTidalTerrain`, `PaintedStillWater`, boat grounding and the shared water
surface consume this same ground. There is no separate collision or tide approximation.

Import settings are copied from Rice Point's matching textures, with fresh GUIDs: readable, linear,
uncompressed, no mipmaps, clamp and bilinear; height and still override Standalone to R16. These are
data textures. The six linear RGBA surface maps use Rice Point's splat import settings. All eight PNGs
use Git LFS. Existing metadata is unchanged; every new GUID is checked across the whole tree.

No seam evidence folder is needed. The existing NMC ground PNGs have the source's third-issue file
hashes: height `9a6c465002cb229d2853191757fb498af66cb81a635f6973f59bf95723ed9f47`, still
`0046179b7e66517f79b685807357aa82f5327d70c6d48d1b8d2c566582b5406b`. Their LFS oids and working
bytes were checked. Every column in Cumberland row 1119 equals NMC row 0 in both rasters.

## The basin and course

The imported ground includes the marina's −6 m dredged basin and approach, the apron, launch-ramp
grading, jetty and breakwater heights. CMB1 supplies the bulkhead's face, up to 9.15 m tall, and the
coast faces; CMB3 supplies the standing marina rig; CMB4 supplies its floats, berths, fuel and boats.
No face, dock, road or building is placed here.

`GroundFileIntake.CumberlandPaint.cs` reuses Rice Point's pure height/still starter paint. Silt,
ripple, sand, marram and grass follow height bands; still-water beds keep mud. `surface-rules.json`
holds every additional paint tuning value. The source course polygon and hole lines determine the
mown areas. Four-metre stripes along each fairway alternate Lawn/Grass weights 160/95 and 208/47.
Greens and tees use full Lawn; the 1.2 m green collar mixes Lawn/Grass evenly, and the nine bunkers
use sand. Rough retains the starter grass. The two-green look uses the existing materials; it needs
the owner's plate review. Pond and brook water overrides mowing. No vegetation is placed on the
mown ground, and the Path channel stays empty: roads, drives and the cart path belong to CMB2.

## Builder, frame and direct Play

`CumberlandBuilder` copies Rice Point's small wrapper: source validation, `LoadPlan`, CREATE and
REFRESH menus, plus `CreateCli` and `RefreshCli`. The CLI success marker follows saved-scene wiring
checks. Its `CumberlandPlan.asset` supplies the region-specific data to the existing `LandSceneTemplate`.
The template and `LandScenePlanDef` are unchanged. This preserves the inactive-before-configuration
terrain, shared `Water.mat`, guarded own-half doors, name registrar and isolated DEV CORE already
proved by Rice Point. No second terrain, water or player implementation is introduced.

All sitings remain unsquashed ground metres under ADR 0042. Both region and ground stay centred at
(0, 0). For W2 only, Cumberland (x, y) maps to NMC (x, y + 560): its entire south edge meets NMC's
north edge. With W2's proposed NMC offset, Cumberland is about (−760, +764.5) from St Peters.
The offset lives here only; no MapGraph, West Water or RegionDef centre is changed.

Phase B creates `Scenes/Cumberland.unity` and its build-list entry through the builder. Opening that
scene directly and pressing Play seeds the template's editor-only DEV CORE when no real core exists.
The player starts on the +3.4 m apron at (210.25, −125.25), beside the dory at (210.25, −128.25),
heading 90 degrees, over the −6 m basin. Their three-metre separation supports the existing boarding
reach. The source ground is already raised there; no placeholder dock is added. A real-core arrival
discards the inactive DEV CORE. These are authored sitings; Phase B must prove walking and boarding.

The dev fishing rig carries NMC's seven existing species for art and gear wiring. Their eligibility
and Cumberland's `SpawnFishIds` remain CMB4's. A cast here currently has no eligible species, so Phase B
must assert that result and report the positive-catch acceptance gap by name. No fish def is changed.

## Door contract for the later NMC wave

All three outgoing targets are NMC's existing def. A missing target produces no passage; loader lists
filter missing targets. Only Cumberland's halves are authored. Each band is just inside the south
edge; arrivals are farther inside so an arrival cannot immediately re-enter its own band.

| Door | Band centre | Band size | Target arrival in NMC | Own arrival for NMC | Own arrival position |
|---|---|---|---|---|---|
| Route 91 | (−186, −277) | 5.8 × 6 | `cmb_route_91` | `nmc_route_91` | (−186, −266) |
| North beach | (67, −277) | 8 × 6 | `cmb_north_beach` | `nmc_north_beach` | (67, −266) |
| Bay | (260.5, −277) | 239 × 6 | `cmb_bay` | `nmc_bay` | (240, −266) |

Route 91 crosses the seam at (−186, −280). Its passage and the beach's accept only a known on-foot
controller through `LandPassageAccess`; trucks wait for W3. Walking arrivals leave the boat point
unset. The beach band crosses the low-tide flat and must be walked on a suitable low tide. The bay
band covers x 141..380 over the −6 m floor; its dry shore fallback is (45.25, −266), on the bank.

NMC has no reciprocal bands or these target arrival keys yet. Until its later wave supplies them,
do not test a trip: the current travel coordinator falls back to NMC's default when a key is absent.
CMB0 proves target, key, guard, size and own arrival data, not end-to-end travel or card timing.

Both road exits stop at the frame. Route 91 reaches the north edge near (−213, 280); the road to
Rocky Point reaches the east edge at (380, 18). Neither has a destination region or a travel door.
The template's solid perimeter colliders sit just outside all four extents. Later CMB2 road paint
will end at those edges; it must retain the closed boundary until an onward region is chartered.
West is continuing farmland; south is the NMC seam. No beyond-frame scenery is invented.

## Saves and capabilities left to later waves

No new state is saved and the tide remains recomputed. A direct Play start in Cumberland is the
Phase B entry while NMC's doors are absent. It is not proof of a Cumberland save resuming there.
At this base, `RestWakeRestorer.Wake` retains an anchor for another region but warns and declines
cross-region wake; boot does not choose a scene from that anchor. This ground-only plan authors no
bed/rest point. A manual save does not newly stamp a Cumberland rest anchor. Resuming through the
normal St Peters boot therefore cannot promise a return here. Acceptance 6 remains with NR1 and
the coordinator's save-fix lane; a same-region direct Play with an existing matching rest anchor
uses the existing rest restore. Phase B tests must use an in-memory save and leave the player's save alone.

Deferred by name: CMB1's bulkhead/coast faces, CMB2's roads/lanes/cart path, CMB3's six key scenes
and rigs, CMB4's floats riding 0.5 m over the tide, berths, fuel sale, ambient boats and seven-species
eligibility. The deepest package boat needs a tide window because a −6 m bed cannot keep its
4.6 m draught afloat at spring low; no deeper raster format is introduced. The course is a painted
place, with no golf simulation. Houses stand empty; interiors and households are later work.
The marina purchase, charter operations and resort guests remain M3. The marina, resort and course
have no invented business names, signs or labels. The resort's later key-scene tide remains the
ruled game tide, +1.07 at 19:00 on day 5.

## Tests and Phase B handoff

Pure tests executed headlessly from the compiled test assembly, all passing:

- `CumberlandGround_DecodesTheBase_TexelForTexel`
- `CumberlandStillWater_CodeZeroIsNone_TheBrookAndPondsKeepTheirLevels`
- `CumberlandSeam_Row1119_CarriesNmcThirdIssueRow0`
- `CumberlandIntake_RejectsOtherProfilesAndChangedPixels`
- `CumberlandPaint_CoversEveryTexelWithoutRoadPlacement`
- `CumberlandCourse_MowsStripesGreensAndTees_KeepsRoughWaterAndBunkers`
- `CumberlandRoadAccess_RequiresKnownOnFootController`

The six existing `RegionPassageGuardTests` also pass: `ShouldFire_GenuineEntry_Primed_AndPastCooldown_Fires`,
`ShouldFire_WhileConsumed_DoesNotFire_EvenPastCooldown`, `ShouldFire_Primed_ButWithinCooldown_DoesNotFire`,
`ShouldFire_Primed_ExactlyAtCooldownBoundary_Fires`, `ShouldFire_SameRegionLingerBounce_IsSwallowed_ByTheLatch`
and `ShouldFire_SceneToggleBounce_IsSwallowed_ByTheFreshEnableCooldown`. Total: 13 tests, zero failures.

Headless compilation rebuilt all 32 active assemblies from this box's source, including every active
test assembly, with no errors. References were copied read-only from donor box `nmc-h3`, HEAD
`e6d8c1e5fb44e11b1ab0c79e160b3f0f4f2804dc`. Its project DLLs are dated 2026-10-21 18:34:36–18:34:48 UTC;
the PlayMode test DLL is 19:08:27 UTC. Current source was rebuilt rather than trusting those older
project APIs. The opt-in `HiddenHarbours.Tests.RigSpike.EditMode` asmdef is inactive because
`HH_JS_RIG_SPIKE` is unset; it is not one of the 32 active assemblies. This was a C# compiler check,
with no editor process or engine test runner. Existing deprecation warnings remain in unrelated files.
Static checks also verify the region catalog's unique ids and required fields, new asset GUID
references, all new GUIDs against 8,875 tree GUIDs, exact source bytes, texture import settings,
NMC LFS oids, PNG LFS coverage and the added-line privacy gate. AssetDatabase validation remains below.

Editor/asset tests, compiled in Phase A and run only in Phase B:

- `CumberlandRegion_IsData_IdSizeSceneAndTide`
- `CumberlandDoors_EachIsGuarded_AndNamesItsArrival`
- `CumberlandTemplate_RicePointAndCumberland_KeepSeparateFrames`
- `CumberlandContent_AllReferencesAndImportSettingsResolve`
- `CumberlandPassagePolicy_RejectsMissingControllerAndWrongPolicy`
- `CumberlandCreate_RefusesAPlanThatIsNotAnAsset`
- `CumberlandCreate_PlanOnlyOnDisk_SavesPassagesRegionsHullAndFish`
- `CumberlandRefresh_DisposableSceneCopy_PreservesMovedOwnerVisual`
- Existing `ContentValidationTests` across the whole Data catalog.

Allow 180–240 editor minutes in the keeper queue, including 60–90 minutes for cold import and
20–30 for required derived art. Rice Point required at least five slots; do not assume an empty
Library can finish in one short slot. The keeper's actual boundary governs every run. Suggested work:

1. Cold import; record its actual duration and console outcome. Generate required shared terrain
   detail/relight and hull art if absent. Run the new EditMode classes and whole-catalog validator.
2. CREATE with `CumberlandBuilder.CreateCli`. Reopen the saved scene and require the wiring success
   marker; record the scene/build-list diff. REFRESH through `RefreshCli`, save and require the
   scene bytes to equal CREATE. Run the disposable moved-visual proof. Budget 20–30 minutes.
3. At the marina, north beach and fork, capture day 1 (DayIndex 0) spring high 02:06:16 (about
   +2.1995 m), mean ebb 05:12:37 (0 m), and spring low 08:18:44 (about −2.1927 m). Derive from the
   shipped 12.4206-hour carrier, 28-day lunar cycle, neap fraction 0.45 and phase 1 hour. Use the
   shared tide, with no fixed water-plane override. Budget 20–30 minutes for all nine plates.
4. Author/run `CumberlandPlayablePlayTests`: direct Play yields one core, walk the low-water north
   beach, board and row in the basin and bay, cast and verify the present no-eligible-species result.
   Prove real-core arrival discards the DEV CORE and driving fails the road policy. Do not cross
   into NMC until its halves exist. Use step/frame diagnostics if a test hangs, never a teardown
   time limit. Allow 20–30 minutes for PlayMode checks and any diagnostics.
5. After warm-up, measure a stated 60-second walking route and a separate rowing route. Report
   median/p95 frame time against 16.67 ms, settings and resolution, total allocated/reserved memory,
   texture/mesh memory and cold scene-load memory delta. Allow 15–20 minutes. Four-region retained
   memory belongs to NR1. A silent run ends with the keeper's stop procedure.

No editor, runtime, plate, frame-time or memory result is claimed in Phase A. Phase C needs the
coordinator's separate paste for a draft PR. Nothing is pushed by this wave's Phase A.

Shared-file audit: only the roadmap and world-map-plan are existing files edited. No Core, RegionDef,
water code, template, NMC builder/mainland/door section, Rice Point builder, St Peters scene/builder,
village def or MapGraph is changed. NR1a, W4-5 and the village pass have no shared code edits to order.

Owner questions for the next paste, recommendation first:

1. Keep positive catches with CMB4, as ruled; Phase B should exercise the cast and document its
   no-eligible-species result until those eligibility changes land.
2. Review the Lawn/Grass course treatment in the first plates before requesting additional paint materials.
3. Leave the marina, resort and course unnamed until the owner supplies their names to the Art desk.
   None of these questions blocks Phase A.
