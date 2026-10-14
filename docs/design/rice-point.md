# Rice Point — RPT0 and the land-region template

Owner ruling, 2026-10-14: build Rice Point first, with Cumberland one wave behind. The place comes now;
owning and growing the wharf, the stationed fleet and its staff stay M3. P1 and P3 lead this wave.
This is Phase A: data and tools, checked headlessly. The scene and build-list entry are Phase B outputs.

## Source and ground

The source is `HH-rice-point-ground-part-1-reissue-2-2026-10-12`, second issue, with sums manifest
SHA-256 `4b3c36be952d589e117a3145ca4808b847e9472d270660f1b05ac83f882ff779` (64 entries).
All entries re-hashed correctly before intake; there were no unlisted files. No kit was imported.
The committed `Data/Terrain/RicePointGround/plan.json` retains the package's calls and future plans as evidence;
only today's two rasters are ground. Future stages, roads, coast faces and key scenes are not placed.

`RicePoint.asset` defines `region.rice_point`, display name Rice Point, scene `RicePoint`, centre (0, 0),
760 × 360 metres and 2 texels per metre. It shares the existing bay tide: mean 0, amplitude 2.2 m, phase 1 hour.
Its wind profile uses the same asset as NMC. Fish eligibility and `SpawnFishIds` remain RPT4's.

`GroundFileIntake.RicePoint.cs` adds a strict profile for `hidden-harbours/rpt-ground-plan@1`,
`terrain_plan.rpt`, second issue. It validates identity, frame, tide, resolution, greyscale bit depth and
both decoded pixel hashes before a builder can write. The St Peters schema check is unchanged.
The read/decode and starter-paint methods are pure; they do not load assets or create scenes.

`RicePointSeabed.asset` binds the exact 1520 × 720 height and still PNGs. Height is −6 + 12 × code / 65535;
observed range −5.749 to +6.000 m. Still code zero is absent; 8,576 texels hold water, including the five
ledge pools. `PaintedTidalTerrain`, walking, boat grounding and the shared water shader use these same bytes.
Still levels are filtered as codes before decoding, by the existing `PaintedStillWater` implementation.

Height and still import settings come from St Peters' ground base: readable linear R16, uncompressed,
no mipmaps, clamp and bilinear sampling. These are data textures, not pixel-art sprites. All PNGs use LFS.
Only the two NMC third-issue input rasters are retained in `SeamEvidence`; there are no input scenes or kit files.
Every row of Rice Point column 1519 equals NMC column 0 at row +400, in both rasters.

The six surface maps are a deliberately coarse starter paint, with their rules in `surface-rules.json`.
They select silt, ripple, sand, marram and grass by height, marsh inside the package's cove ellipse,
and mud under still water. `RicePointStarterPaint` reproduces every channel. No path or asphalt is painted.
RPT1 supplies coast faces; RPT2 supplies roads; RPT3 supplies standing pieces. This paint still needs the owner's eye.

## Frames and template

`LandScenePlanDef` holds the region, ground, source, paint, doors, arrivals, dev hull/fish roster, spawn,
edge thickness and rendering bands. `RicePointBuilder` validates the source then calls `LandSceneTemplate`.
Cumberland supplies another plan; the template has no Rice Point dimensions or tide literals.

ADR 0042 keeps height fields, travel bands and independent sitings in unsquashed ground metres.
The package's older `units.world` projection sentence is not applied to those systems. Projection within a
baked piece remains that piece's art contract. Both the RegionDef and the height map therefore span 760 × 360.

For W2 only: Rice Point (x, y) maps to NMC (x − 760, y − 100). Its east edge meets NMC's west edge over
NMC y −280..80. With the proposed NMC bay offset, Rice Point is about (−1520, +104.5) from St Peters.
No offset is written to RegionDef, and this wave changes no MapGraph or West Water.

CREATE refuses an existing scene. It builds `LandRegion/Logic` and `LandRegion/Visuals`, painted terrain,
surface paint, shared `Water.mat`, review camera, loader, name registrar, anchor, guarded doors and closed edges.
It creates an inactive DEV CORE through `PersistentCoreBuilder` and selects it through `DevRegionBootstrap`.
`LandDevCoreRoots` releases its children before their Awake calls, so persistent roots remain roots.
All region content is outside the disposable DEV CORE. Direct Play starts on the existing raised ground at
(−56, −108.4), with the dory at (−53.285, −109.825), heading 28°. The ground there is +3.4 m and the keel bed −2.0 m.
At the default first-day 06:00 start the dory floats; the spring low grounds it, as it should.

REFRESH requires the correct open scene and rewires existing logic in place. It does not replace objects,
move owner-authored transforms, regenerate paint, reset visuals or assign new file IDs. A newly available neighbour
without a previously authored door is reported for explicit authoring. A no-change refresh must yield an empty scene diff.

Reuse: the inactive-before-configuration splat convention and guarded own-half doors follow NMC;
the tide data follows St Peters/NMC; shared sea wiring, palette and displaced-water setup use `WaterSceneTemplate`;
the full walking/boating/fishing rig calls `PersistentCoreBuilder`, avoiding a second copy of NMC's older walk-only core.
No existing region builder is edited.

## Door contract for NR1

All positions and sizes below are ground metres. Every outgoing target is `region.nine_mile_creek`.
An absent target produces no passage; loader lists exclude absent targets. Arrivals sit beyond the trigger bands.

| Door | Band centre | Band size | Target arrival in NMC | Own arrival for NMC | Own arrival position |
|---|---|---|---|---|---|
| Sea | (377, −162.5) | 6 × 35 | `rpt_sea` | `nmc_sea` | (366, −162.5) |
| Strand | (377, −96) | 6 × 8 | `rpt_strand` | `nmc_strand` | (366, −96) |
| Route 91 | (377, −42) | 6 × 5.8 | `rpt_route_91` | `nmc_route_91` | (366, −42) |

The strand and road accept a known on-foot controller only. Walking arrivals leave the boat point unset.
`IRegionPassageAccess` is the small new Core contract; App reads the control mode, and World evaluates the policy.
It fails closed if a configured policy has the wrong type. Existing passages without policies retain their behaviour.
The seat's lead-architect review is needed for that Core interface. No truck is transferred.

NR1 must author NMC's reciprocal bands and these target keys. Until then, do not attempt the trips:
the current loader would fall back to NMC's default arrival when a named arrival is absent.
This wave proves each band's target, key, size and guard, not end-to-end travel or crossing-card timing.

## Edges and later capability

The south edge shows open bay; the west shows open water, plain beach, dune, pasture and wood continuing unbuilt.
North is farmland and woodland with no road. Solid perimeter bands sit just outside all four extents;
the east travel triggers sit inside them. There are no west/south/north travel doors and nothing may leave those edges.
The camera and height frame end together. There is no invented land or scenic extension beyond them.

Package items the game cannot yet express or this wave deliberately leaves to their owners:

- RPT4: all ten fish eligibility changes, area-specific habitat and season/time rules, clam flats, surf, rip and streaks.
  The ground-only phase has a rod rig and species art roster, but a Rice Point cast currently returns no eligible catch.
  The charter's RPT0 request to catch once conflicts with its explicit RPT4 eligibility step; do not silently move that work.
- A tidal current that pushes the boat in the gut and dawn mist need separate simulation/fog work.
- Midwater herring at the 5–6 m hole and the freshwater brook pond need RPT4's explicit capability review.
- Proposed razor clam, smelt, eel, oyster lease, lumpfish, mussels, gaspereau and trout wait on owner rulings.
- RPT1/RPT2/RPT3: coast faces, roads/lanes and key scenes. Houses remain empty until RPT5.
- M3: wharf ownership, growth stages, fleet staffing and interiors. No buoy is placed here; whose buoys remains open.
- NMC's next issue owns `field.nmc_west_farm_south`, `hedge.nmc_y_minus96` and the bluff-column correction.
  Package seam evidence is authoritative for this wave; a comparison to the game's NMC ground follows H2.

## Validation and Phase B handoff

Pure tests, executed headlessly:

- `RicePointGround_DecodesTheBase_TexelForTexel`
- `RicePointStillWater_CodeZeroIsNone_ThePoolsKeepTheirLevels`
- `RicePointSeam_Column1519_CarriesNmcThirdIssueColumn0`
- `RicePointIntake_RejectsOtherProfilesAndChangedPixels`
- `RicePointPaint_CoversEveryTexelWithoutRoadPlacement`
- `RicePointRoadAccess_RequiresKnownOnFootController`

The six existing `RegionPassageGuardTests` also pass headlessly: genuine entry, consumed latch,
within-cooldown refusal, exact cooldown boundary, same-region linger and scene-toggle bounce.

Editor/asset tests, compiled only; run in Phase B:

- `RicePointRegion_IsData_IdSizeSceneAndTide`
- `RicePointDoors_EachIsGuarded_AndNamesItsArrival`
- `LandSceneTemplate_TwoPlans_TwoFrames`
- `RicePointContent_AllReferencesAndImportSettingsResolve`
- `RicePointPassagePolicy_RejectsMissingControllerAndWrongPolicy`
- Existing `ContentValidationTests` over the whole Data catalog.

Phase B estimate: one 75-minute keeper turn, including 25 minutes for cold import/derived art, 10 for CREATE and
REFRESH/diff, 15 for tests, 15 for tide plates and 10 for measurements. Cold import can exceed the estimate;
record its actual duration and stop at the keeper boundary rather than taking another slot.

1. Cold import and check the console; build required shared terrain detail/relight and hull art if absent.
2. Run the new EditMode classes and the existing content validator. CREATE through `RicePointBuilder.Create`;
   the builder saves `RicePoint.unity` and registers it. Record that diff separately.
3. Run `RicePointBuilder.Refresh`, save, and compare scene bytes to CREATE's output. Require no change.
   Also move an owner visual in a disposable scene copy and prove REFRESH preserves it.
4. Capture strand, cove marsh, gut and ledges at day 1 (DayIndex 0) spring high 02:06:16 (about +2.1995 m), mean ebb
   05:12:37 (0 m), and spring low 08:18:44 (about −2.1927 m). These use the shipped 12.4206-hour carrier,
   28-day lunar cycle, 0.45 neap fraction and phase 1 hour, not an imposed fixed water plane.
5. Author/run `RicePointPlayablePlayTests`: direct Play seeds one core, walk the strand, board and row around
   the point at mean water, cast and verify the current no-eligible-species result. The positive catch assertion
   remains blocked on RPT4 or an explicit earlier eligibility ruling. Test road refusal while driving. Do not cross
   into NMC before NR1. Exercise real-core arrival to prove the inactive DEV CORE is discarded.
6. Measure median and p95 frame time after warm-up over a fixed 60-second walk and row path, resolution/settings
   stated, and report total allocated/reserved memory plus texture/mesh memory. Target p95 ≤16.67 ms on the slot box.
   Report cold scene-load memory separately. The four-region retained-memory trip belongs to NR1.

No runtime, scene, rendering, frame-time or memory result is claimed by Phase A. No new state is saved.
Scene/build-list output, visual acceptance, save-resume, crossing names and actual travel remain unproved.

Shared-file audit: no files from #951, W4-2 or #916 are edited. The only existing runtime file changed is
`World/RegionPassage.cs`; the new Core interface needs the seat's review. NMC and St Peters builders/scenes,
water code and MapGraph are unchanged.

Owner recommendations for the next paste: keep the positive catch gate with RPT4 unless explicitly moved earlier;
keep the coarse starter paint for the first plate review, then refine by the subsequent waves; leave buoy ownership
with the Art desk. No new Phase A decision is required.
