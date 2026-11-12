# Water, surf, wake and seabed test inventory

Source snapshot: `12bc04b19a2efb33e352b4eea58104f7a5599be1`, refreshed 2026-11-12.
This is a documentation audit for planning the water renderer's future. It changes no
test, expectation, material, scene, import setting, tuned value or render pipeline.
No Unity editor or CI test run was performed for this audit.

**A green Null-device CI run cannot certify the water's look.** It can protect the
wave law, authored heights, foam arithmetic, component wiring and shader source
contracts. The tests that actually sample rendered water generally skip there.
An image written by a CPU diagram test is also different evidence from a frame
rendered by the production water pass.

## Refresh against current main

The earlier snapshot contained 3,019 methods in 476 files. This refresh adds 74 declarations in 16 additional files, retains every previous declaration and its graphics classification, and updates moved source lines. Additions include 17 `HullContactFoamTests` numeric cases; the whole-hull wake photograph; South Water ground, seam and arrival contracts and its GPU plate; Rice Point trawler-berth logic and its GPU plate; and new tide, catch-depth, replay and beach/ground contracts selected by the same scope rules. The trawler-berth plate was added explicitly during the omission review because berth/ground pictures are in scope. No new run or accepted image baseline is claimed.

The surf-drift fixture now locks frame cadence and reads the physics-grid clock in both arms; this changes its source setup, not its graphics eligibility. Rice Point's ground/region fixtures now read the dredged ground, with the boat start asserted at -7 m. These remain source/data or physical-state evidence, not composed-image proof.

## Complete source index and counting rules

The [method index](water-test-methods.md) names every included test declaration,
its file and line, parameterization, and expected Null-device behavior. The scope
includes water/foam/surf/wake/seabed fixtures, their shared wave and weather laws,
terrain import/bake/render contracts, tide gates, flotation, reflection, spray,
rain, wading and directly related integration cases. Broad domain fixtures are
listed in full; mixed gameplay fixtures include their water-related methods.
Some adjacent terrain, chart and berth checks are deliberately retained: they
constrain water inputs without judging the water's image. General fuel/economy
and fish simulation tests that merely consume a sea-state value are outside scope.

The inventory contains **3,093 methods in 492 source files**.

| Suite | Methods | R | R* | G | A | O |
|---|---:|---:|---:|---:|---:|---:|
| EditMode | 2,865 | 2,759 | 30 | 68 | 8 | 0 |
| PlayMode | 228 | 128 | 23 | 76 | 0 | 1 |

These are **declared methods, not expanded NUnit cases or execution counts**.
`TestCase` rows can expand to several cases; `Values`/`ValueSource` and
`TestCaseSource` can expand further. The source index identifies those declarations
without guessing their runtime expansion. Shared helpers are followed for gates;
the wake photograph partial files share the main fixture's helpers. The two
St Peters key-scene methods live in the `WharfBuildingPass2PlatePlayTests` partial
class despite their different filename.

## What CI does, and what the codes mean

[The workflow](../../.github/workflows/ci.yml#L226) invokes both EditMode and
PlayMode with `-batchmode -runTests`, without a test filter or category exclusion.
Its YAML does **not** explicitly pass `-nographics`; this inventory evaluates the
lane's stated CI condition, `SystemInfo.graphicsDeviceType == Null`. A different
runner/device requires rechecking these forecasts. Both suites run in the editor,
so a `#if !UNITY_EDITOR` player-only skip is not an expected CI skip.

| Code | Expected on the stipulated Null device | What it proves about the look |
|---|---|---|
| `R` | Selected; no graphics-device skip found in the test and followed local setup/helpers | Logic, source, data, imported bytes, CPU images, transforms or published state; no production GPU image |
| `R*` | Same graphics eligibility, but a conditional `Assert.Ignore` exists; see linked source for the prerequisite, including editor-only branches | Same limit; missing assets/shaders can still make it skip |
| `G` | Explicit graphics guard skips; includes guards in shared plate helpers | No GPU result in Null CI; a GPU run is still required |
| `A` | Conditional texture-array capability guard; skips if `supports2DArrayTextures` is false | Binding/state checks only even when the capability exists; do not infer the capability from Null alone |
| `O` | Opt-in endpoint skips when its selection variables are absent; also requires graphics | No endpoint or image evidence from default CI |

These are source forecasts, not claims of passing tests. Import failures,
timeouts, fixture setup failures and missing committed content can prevent an
eligible case from running. `G` also covers graphics support/contract probes that
do not themselves judge a whole image. A GPU alone is not sufficient for every
case: the foam-history lifecycle fixture restricts its backend to D3D12;
caustic conformance needs compute support; daylight-swell conformance needs an
ARGBFloat target. The opt-in wake endpoint requires `HH_WAKE_HULL` and
`HH_WAKE_LEG`, then asserts D3D12. Other evidence destination variables usually
only control output; they must not be confused with that opt-in gate.

[The results reporter](../../tools/ci/results.py) rejects missing/empty results
and counts skipped cases separately from passes. That suite-level guard does not
prove a particular fixture ran, and a graphics skip never becomes visual proof.
When a renderer change is validated, record the expanded per-class executed,
passed, failed and skipped counts from that run, together with its device/backend.

## What the existing tests actually guard

Fixture names below are searchable in the complete method index. Each row separates
the invariant protected in CI from the image that still needs graphics evidence.

| Subject | Existing tests and observations | Null CI's limit |
|---|---|---|
| Wave law and sea-state changes | `WaveMath`, `WaveSpectrum`, `WaveFetch`, `WaveFetchGrowth`, `WaveHeightFromFetch`, `WaveMemory`, `WaveFieldAnimator`, `WaveFieldPhaseIsClosedForm`, `WaveStateChangeVibration`, `WaveSwell*`, amplitude/speed measurements, `CurrentField`, `WindField`, `WeatherModel`, tide-model tests | Executes numeric/determinism rules and source/data contracts; cannot see the composed sea or judge transition smoothness |
| See/feel agreement and displaced geometry | `ShippedWaveField`, `WaveFieldSettingsUnification`, `HullRidesTheDrawnSea`, `SeeEqualsFeelForces`, `SharedHeave`, `ShoreFadeMath`, `DisplacedWaterMath`, `DisplacedWaterConfig`, `DeckRiderDisplacedRide`, `SeaPlaneQuad` | Can pin wavelengths, heights, mesh bounds, seams and published transforms; does not rasterize displaced silhouettes |
| Authored seabed and bathymetry | `PaintedHeight*`, `TerrainHeightPalette`, `SeabedHeightImport`, `SeabedBake`, `SeabedBakeComb`, `WaveBathymetry*`, `WaterSeabedBakeOrder`, `RegionWaterBakePlay`, terrain plan/import/array/splat contracts, region shore-map tests | Checks decoding, neutral encodings, grids, interpolation, rebake lifecycle, loaded field identity and CPU colours; these are not proof of underwater appearance |
| Depth tint, absorption, caustics, untile and palettes | `WaterAbsorption`, `WaterDispersion`, `WaterUntile*`, `WaterSiltEnvironment`, `WeatherWaterPalette`, `WaterPaletteGrade`, `CausticMath`, `CausticMoodCopy`, `OneFoamLanguage`, `WakeFoamAgeingShader`, `WaterUniformContinuity` | Math, copied shader text, serialized values and property-block contracts can run. Production HLSL parity in `CausticPatternAgreement` and the rendered whiteout/swirl suite skip |
| Surf, swash and breaking geometry | `BreakerMath`, `BreakerContour`, `BreakerPlunging`, `BreakerBore`, `BreakerDeposit`, `BreakerSurfForce`, `SurfSequence`; rendered arms of `BreakerBoreLook`, `BreakerSurfRender`, and `CurlingBreakerSilhouettePlay` | CPU bore timing, force and field constraints can run. Front/fringe pictures, curling silhouette, boat occlusion, zero-dial pixel identity and view cost need graphics |
| Wake origins, widening, foam age and transport | `OneWakeRoot`, `FoamTrailRoot`, `WakeProjection`, `WakeSternOffsetRig`, `WakeDispersal*`, `WakeParticleSystem`, `WakeBubbleSystem`, `WakeTrailMath`, `WakeFoamAgeing*`, `FoamBuffer*`, `FoamSelection`, `FoamTransport*`, `WakeDepositPlay` | Can protect stern routing, pooled state, deposits, CPU transport and density arithmetic. GPU transport/binding/history suites skip, as do rendered wake photographs and fade/root plates |
| Wake stability during camera motion | `WakeFoamWindowPlay` uses real frames and a camera but checks the C# buffer/window arithmetic | Runs without rendering; its name "TheDrawnBand" does not imply pixel readback. Conversely, `OneWakeRootPlate` draws a CPU diagram rather than the production water |
| Reflections and illumination | `WaterReflection`, `WaterReflectionWarp`, `WaterReflectionLadder`, reflector registration/property tests, `WaterLightBridge`, `BeamWaveRelief`, `HullCausticLighting` CPU arms; GPU `ObjectReflectionProbe`, `BeamReliefRender`, sweep mirror/night/lamp/sun-side arms | Can pin gates, ladders and light uniforms; cannot certify a mirror, glitter, beam relief, reflected hull or final grading on a Null device |
| Hull contact and occlusion | `HullContactFoamTests` (17 numeric methods), the graphics-gated whole-hull wake photograph, hull waterline residual/settle tests, `HullWaterlineAcceptance`, `HullReflectionPivotPlay`, `HullsRideTheTidePlay`, `HullsRideTheTidePlatePlay` | State/height contracts can run. Nine waterline acceptance render methods and all tide plates skip; existing waterline/boat-occlusion GPU tests mean this subject is **partially tested**, not absent |
| Rain, spray and swimming | `RainActivation`, `RainIntensity`, `BowSprayGrading`, `SprayIntensity`, `SurfSpray`, `WadeSplashRate`, submerge/swim tests | Data, launch geometry, rates, event routing and animation state are guarded; that is not proof that water particles visibly sort or blend correctly |
| Whole scene and shader health | `WaterShaderCompileGuard` discovers project material shaders and compiles shipped keyword/pass variants; region water/shore/tide plates, `LeeSeaPlatePlay`, `WaveMemoryPlatePlay`, terrain/shore plates and native wake photographs | Import/compile checks do not prove the image. Named scene plates are graphics-gated, often diagnostic captures rather than an approved image-equality oracle |

## Read the GPU evidence at its actual scope

`WaterFidelityPlateSweepTests` has 17 methods: 15 graphics-gated methods and two
CPU checks (weather pairing and plate-font glyphs). Its primary matrix is four
views (steep surf, sand shoal, island arrival, open sea) across four weathers,
three tides and three hours: 144 base plates, with further diagnostic sheets.
It renders the **flat Universal2D pass** without hulls, displaced vertex lift,
interior masks, wakes or spray. Its mirror, night, foam, sun-side and whitecap
metrics are useful, but a saved sheet is not an accepted look baseline. See the
[fidelity register's instrument contract](../design/water-fidelity-register.md#1-the-instrument--what-a-plate-is-and-what-it-is-not).

`DaylightSwellReadConformanceTests` compares the production HLSL helper against its
C# twin in a small float target. It explicitly does not certify the full water
shader's half precision, composed look or pass cost. See the
[swell-read proof boundary](../design/water-daylight-swell-read.md#proof-and-ci-forecast).
Likewise, the transport/history GPU fixtures guard RenderGraph arithmetic and
resource lifecycle; they are not a composed surf-and-wake look verdict.

`CurlingBreakerSilhouettePlayTests` already has four GPU methods for silhouette
sequences, boat occlusion, zero-dial pixel identity and worst-view budget.
`HullWaterlineAcceptanceTests` has four CPU/data methods plus nine graphics-gated
render methods. Those are existing protection to preserve during a renderer change;
they must not be reported as "no waterline tests" merely because CI skips them.

## Looks with no dedicated rendered assertion found

This is an absence finding from the source inventory, not an assessment of how the
game looks today. The distinction from **an existing test that skips CI** matters.

| Look or acceptance question | Existing nearby evidence | Missing assertion |
|---|---|---|
| HDRP water/seabed output and parity with the current production coast | The manifest declares URP; production water names `Universal2D` and `HHWaterDisplaced` passes. No HDRP package or HDRP-specific test was found in the audited sources | No HDRP scene/render fixture or accepted cross-pipeline image oracle. Existing URP shader/pass/source assertions cannot certify an HDRP implementation |
| Rain streaks visibly sorting in front of the displaced water, and water rings reading at night | `RainActivationTests` explicitly calls these editor checks; activation data, preset keys, ring shape and intensity are pinned | No dedicated rendered ordering/night-readability assertion found. The main sweep has no rain column |
| Bow/surf spray visibly sorting over real decks in rough water | Spray launch/rate/size math and defaults; the fidelity register carries the deck-sorting look as owed | No dedicated spray-versus-deck occlusion pixel assertion found. A correct sorting integer or emission count cannot establish this picture |
| Perceptible snaps from the throttled water push tier in the fully composed scene | Phase-closure/vibration tests and camera-window arithmetic; `WaveMemoryPlatePlay` supplies sequence evidence | No dedicated image-motion acceptance threshold for the complete 8 Hz publication/interpolation path found. Numeric phase continuity and sequence export do not settle perceptual smoothness |

The wider "one coherent sea" aesthetic, organic seabed target and fetch *feel*
also have no universal automated acceptance oracle. They do have substantial
nearby numeric, source, GPU metric and capture coverage listed above; calling
them wholly untested would erase that evidence. Their remaining visual/feel
judgment belongs to a graphics run and an art review. The
[fidelity register](../design/water-fidelity-register.md) contains historical
findings and owner judgments; its old numbers are not fresh measurements here.

## Using this inventory for a renderer change

Keep the shared deterministic wave/height/force laws and their numeric tests as
the reference. Treat URP source-text, pass-name, feature-installation and shader
keyword tests as renderer-specific contracts that must be deliberately migrated
if their rule changes. Preserve their asserted rule and negative controls rather
than deleting assertions to make a new renderer pass.

Use the existing graphics suites at their stated scope, then obtain the missing
scene evidence for rain, spray and temporal continuity on the intended backend.
Compare water **with** its authored seabed, hulls, masks, wake history, lighting
and gameplay camera. Report skipped/opt-in endpoints separately, including any
asset or backend prerequisites. This audit chooses no new look, pipeline,
tuning or baseline and authorizes no such change.
