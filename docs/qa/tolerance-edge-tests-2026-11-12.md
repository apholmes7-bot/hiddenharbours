# Tolerances at a sampling boundary — 2026-11-12

Findings only, against main `1c6850e86c151b73b6eaa11de575d50de2398735`.
No test, production source, asset, scene or expectation changes in this report.

The reported punt failure has a reproducible float32 boundary hole. A second
ground-import assertion exceeds its one-code tolerance on the bundled Mono
source host and passes on .NET 8. These deserve attention before changing any
of the many tolerances which already have numerical headroom.

## Search and proof limits

The census reads all **1,436 tracked C# sources** under `Assets/Tests/EditMode`
and `Assets/Tests/PlayMode`. A Roslyn syntax walk covers editor, no-symbol and
standalone-contract conditional branches; text searches also cover comparisons
which accumulate failures before asserting a count. Review includes `Within`,
numeric third arguments to `AreEqual`, absolute-error/distance bounds, polling
conditions, tick/rate/step declarations, code widths, integer rounding, pixels
and half-unit allowances. A third argument which is a diagnostic string is not
a numeric tolerance. There are no assertion aliases in these sources.

The union contains **50,884 assertion calls**, **1,256 `Within` calls**,
**7,227 `AreEqual` third-argument candidates** (including diagnostic-only
overloads) and **22,320 bound/predicate candidates**. Raw candidate counts
overlap; they are not test-case or finding counts.

This is a source audit, **not a run of the whole suite**. Arithmetic witnesses
use the pinned source, real managed Unity math and the repository's NUnit
assembly without starting Unity. The ground-import method itself is run
unchanged on the bundled Mono and .NET 8 hosts. Neither proves native imports,
rendering, object lifecycle or PlayMode scheduling. The reported CI failure is
the charter's observation; no CI log was downloaded or independently replayed.

“Minimum” needs a domain. Below, **witness minimum** means the least absolute
tolerance accepting the finite values actually evaluated, with inclusive NUnit
comparison. For a float tolerance, `up32(x)` is the first float32 value **at or
above** the exact double residual; simply taking the next float after the old
tolerance is not sufficient. A minimum observed on two hosts is not a bound on
all future assets or runtimes. Where a native measurement is unavailable it is
identified, rather than inventing an exact minimum. “Keep” means no numerical
reason to enlarge the existing policy allowance, not a claim that its current
residual equals that allowance.

## The punt: the exact edge, cadence, clamps and update count

The failure is at
[PilotableFleetPlayTests.cs:939](../../Assets/Tests/PlayMode/PilotableFleetPlayTests.cs#L939).
Its preceding wait at
[line 938](../../Assets/Tests/PlayMode/PilotableFleetPlayTests.cs#L938)
accepts `engine.SteerDegrees <= HardPortDegrees + 0.01f`.
`HardPortDegrees` is the **constant** `-32f`, so the threshold expression is
folded to one float32 value:

| Quantity | Exact value |
|---|---:|
| `0.01f` promoted to double | `0.00999999977648258209228515625` |
| `float32(-32f + 0.01f)` promoted to double | `-31.9899997711181640625` |
| Distance from that accepted threshold to `-32f` | `0.0100002288818359375` |
| Excess over the assertion tolerance | `0.00000022910535335540771484375` |

Thus the wait may legitimately finish and the very next assertion reject the
same value. The smallest float tolerance covering **this entire accepted port
boundary** is **`0.010000229f`** (exactly `0.0100002288818359375`). NUnit accepts
it and rejects its immediate predecessor. This does not justify allowing a
whole animation step or changing the punt's authority.

The measured quantity is continuous on the mesh path. **`0.01°` is a stopping
allowance, not the motor's step size.** The path is:

- [OutboardMotorMeshLayer.cs:142](../../Assets/_Project/Code/Boats/OutboardMotorMeshLayer.cs#L142)
  starts the continuous column accumulator at **4**. Hard port targets **0**.
  The layer's default cadence is **8 columns/s**; the test reads that field.
- [OutboardMotorMath.cs:174](../../Assets/_Project/Code/Boats/OutboardMotorMath.cs#L174)
  advances by `8 * max(0, Time.deltaTime)` columns per `LateUpdate` through
  `Mathf.MoveTowards`. An overshooting update lands exactly on target 0.
- [OutboardMotorMath.cs:131](../../Assets/_Project/Code/Boats/OutboardMotorMath.cs#L131)
  clamps the position to `[0,8]` and maps it to `-32 + 8*p` degrees for this
  nine-column, ±32° hull. The final
  [steer clamp](../../Assets/_Project/Code/Boats/OutboardMotorMeshPose.cs#L84)
  clamps to `[-32,+32]`; it does **not** snap an in-range `-31.99` to `-32`.
- Consequently the nominal speed is **64°/s**, and an update's travel is
  `64 * deltaTime` degrees until clamped. A sprite column is 8° on this hull;
  the mesh layer deliberately does not round its accumulator to sprite columns.

There is **no fixed frame count in the current test**. Its
[SpinUntil](../../Assets/Tests/PlayMode/PilotableFleetPlayTests.cs#L115)
yields rendered frames until the predicate or its time deadline wins. At the
default cadence, `budget = (4/8)*4 = 2 s`; full travel takes nominally 0.5 s.
`Time.fixedDeltaTime` is not the integration delta here. The historical
60-frame wait described in the helper's comment is not the current code.

The managed witness executes the actual two motor math methods. Constant
`1f/60f` deltas hit the exact port clamp on update **30**, which passes. With
the first **29** deltas unchanged, the stored position is `0.13333188`; a
30th delta of approximately **`0.0165102351f` s** leaves `p=0.00124999881`
and publishes the failing `-31.98999977` value. Both deltas are ordinary
frame durations. This is a constructive **30-update witness**, not a
claim about how many frames the reported CI run used. The failure message
cannot establish that count. One additional advancing update in this witness
reaches the exact clamp.

The preferable future repair is to make waiting and adjudication use the same
error metric, or wait for the exact `MoveTowards` target with the existing
bounded deadline. Preserve the hard-port assertion and the unmanned-helm
precondition. This report makes no repair.

## Ranked temporal and numerical findings

| Priority | Assertion and mechanism | Quantum / current tolerance | Smallest safe value and evidence |
|---|---|---|---|
| 1 — reported intermittent, reproduced boundary | [Punt hard port:939](../../Assets/Tests/PlayMode/PilotableFleetPlayTests.cs#L939) | Actual step `64*deltaTime` °; wait and assertion both use nominal `0.01°` | **`0.010000229f`** for all float32 values admitted by the rounded port boundary. Eight-check arithmetic witness confirms rejection before and acceptance at this value. |
| 2 — host dependent failure, unchanged assertion executed | [Ground import:68](../../Assets/Tests/EditMode/NineMileCreekGroundImportTests.cs#L68), `RuntimeFieldKeepsNorthUpRangeAndEveryCodeWithoutAnotherFill` | Height code `q=12f/65535f ≈ 0.000183108263 m`; tolerance `q` | **`up32(0.00023849678888154813)` m** across the two measured hosts. Mono fails one of the fixed 4,096 probes; .NET 8 passes. A one-code height allowance does not bound float world→texel coordinate error across a steep bilinear cell. |
| 3 — same potential polling hole | [Skiff hard starboard:986](../../Assets/Tests/PlayMode/PilotableFleetPlayTests.cs#L986), wait at 981 | Step `60*deltaTime` ° at 8 columns/s; tolerance `0.01f` | **`0.010000229f`** for the float32 boundary `float32(30f-0.01f)`. The exact residual is the same as the punt's. This is an arithmetic sibling, not a second observed CI failure; runtime intermediate precision can affect its non-constant predicate. |
| 4 — deterministic tick-sized allowance, ample current margin | [Cabin door:478](../../Assets/Tests/EditMode/BoatCabinDoorWalkthroughTests.cs#L478), `TheSettleIsTheDoorsOwnData_AndADoorThatStatesNoneHasTheDefault` | `Tick=1f/60f = 0.0166666675 s`; tolerance `Tick` | Current accumulator witness minimum **`1.1920928955078125e-7f s`**: 24 additions give `0.400000125` against `0.4f`. Keep the one-tick policy; this fixture does not sit at its edge. For other settle values, include accumulation roundoff if admitting a full tick of lateness. |
| 5 — derivative sampling budget, not a near miss | [Tide times:69](../../Assets/Tests/EditMode/UI/TideAlmanacTests.cs#L69) and [shifted tide:133](../../Assets/Tests/EditMode/UI/TideAlmanacTests.cs#L133) | `SlopeDt=50*0.05f = 2.500000037252903 s`; tolerance `SlopeDt` | Current witness minima **`1.25010562647293 s`** and **`1.2501054774613181 s`** respectively, on both hosts to the shown precision. Keep `SlopeDt`. Forward differencing shifts the extrema by about half that step; 24 bisections refine the 5 s scan bracket. Enlarging to a scan step would discard existing discrimination. |

For the ground failure, Mono's worst probe is
`(165.916046f, 86.1273346f)`: actual `1.48976934f`, double oracle
`1.4895308427725809`. The unchanged NUnit assertion reports those same values.
The first float tolerance at or above that measured Mono residual is
**`0.000238496796f`**; its immediate predecessor is too small. This is a
minimum for the measured probe set, not a universal spatial error bound.
On .NET 8 the same seeded source loop's maximum residual is only
`1.7001348382450487e-6 m`. These are **source-host results**, not native Unity
test results. The sampler normalizes positions, multiplies back to fractional
texel coordinates, floors them, and performs three float lerps; the reference
uses direct double coordinates and interpolation. Near a slope, coordinate
rounding is multiplied by the local height gradient. Quantization alone is
not the error budget. A future fix should decide that precision contract before
merely increasing a tolerance.

## Other code-, sample- and integer-sized allowances

These are also search hits of the requested shape. They are ranked below the
cases above because their current mechanism leaves spare margin or compares
integers. File/line links identify each assertion or the comparison feeding it.

| Risk | Locations | Quantum and tolerance | Safe minimum / disposition |
|---|---|---|---|
| Medium: same sampler family, finite measurements available | Ground import [59](../../Assets/Tests/EditMode/NineMileCreekGroundImportTests.cs#L59), [63](../../Assets/Tests/EditMode/NineMileCreekGroundImportTests.cs#L63), [92](../../Assets/Tests/EditMode/NineMileCreekGroundImportTests.cs#L92); plateau [163](../../Assets/Tests/EditMode/NineMileCreekSceneArrivalTests.cs#L163) | `q=12f/65535f m`, all tolerances `q` | At 59 the 1,702,400-texel maximum is **`9.5367431640625e-7f`** across both hosts; at 63 the five-point maximum is **`up32(2.1942804018237894e-7)`**. These are far below `q`. At 92 both arms filter the same normalized code; the plateau uses the exactly representable +6 endpoint. Keep `q`; native scene minimum unmeasured. |
| Medium: native scene parity is not implied by a file check | [Ground parity:89](../../Assets/Tests/PlayMode/NineMileCreekGroundParityPlayTests.cs#L89), [100→105](../../Assets/Tests/PlayMode/NineMileCreekGroundParityPlayTests.cs#L100) | `q=12f/65535f m`; `Within(code)` and `diff>code` failure count | No native minimum measured. Both healthy arms should decode the same texture into the same sampler; then the residual can be zero. Do not enlarge `q` to excuse a different map or registration. The import probe's host-dependent overrun is a reason to inspect the oracle, not evidence this scene test failed. |
| Low: nearest-code double decode | [Cumberland ponds:48](../../Assets/Tests/EditMode/CumberlandGroundTests.cs#L48), [Rice Point pools:53](../../Assets/Tests/EditMode/RicePointGroundTests.cs#L53) | `q=12d/65535d ≈ 0.000183108263 m`; tolerance `q` | Nearest-code encoding needs **`q/2 + R64`**, with `R64` the double encode/decode roundoff over ±6 m. That is below the existing whole-code allowance; keep `q`. No exact finite-asset minimum was measured. |
| Low: nearest-code bake, native import needed | [R16 bake comparison:107](../../Assets/Tests/EditMode/PaintedHeightMapR16Tests.cs#L107), asserted at [117](../../Assets/Tests/EditMode/PaintedHeightMapR16Tests.cs#L117) | `q=11f/65535f ≈ 0.000167849241 m`; tolerance `OneR16Step=q` | Ideal nearest rounding needs `q/2`; float coordinate/decode error must also fit. Native minimum unmeasured. Keep `q` pending that measurement; the raw-code and texture-format assertions must remain intact. |
| Low: range re-encoding in double | [Uncut heights:177](../../Assets/Tests/EditMode/RicePointTrawlerBerthTests.cs#L177), [still levels:178](../../Assets/Tests/EditMode/RicePointTrawlerBerthTests.cs#L178), [blend ceiling:215](../../Assets/Tests/EditMode/RicePointTrawlerBerthTests.cs#L215) | New range `[-10,6]`: `q=16d/65535d ≈ 0.000244144350347 m`; comparisons allow one `step` | One re-encode requires `q/2 + R64` relative to the decoded issued value, leaving almost half a code of margin. Keep `q`. Thresholds at [229](../../Assets/Tests/EditMode/RicePointTrawlerBerthTests.cs#L229), [230](../../Assets/Tests/EditMode/RicePointTrawlerBerthTests.cs#L230) and [259](../../Assets/Tests/EditMode/RicePointTrawlerBerthTests.cs#L259) also classify “moved” using this same unit; changing it changes classification, not just precision. |
| Low: half-weight filtering halves the level error | [Still-water rim:325](../../Assets/Tests/EditMode/StillWaterSeamTests.cs#L325) | `q=11f/65535f m`; tolerance `OneCode=q` | Current two-host witness minimum **`1.1324882507324219e-5f m`**. Keep `q`: the original pond's rounding error is halved at the rim, not pushed to a whole code. |
| Low: integer repeat count is exactly comparable | [Throttle repeat:105](../../Assets/Tests/EditMode/HelmThrottleStepMathTests.cs#L105) | One emitted repeat; `Within(1)` | Minimum **1**, no epsilon. Both hosts produce 4 against expected 5: 27 additions of `0.05f` stop just below the ideal boundary for the fourth repeat. A tolerance of 0 would change that intentional one-repeat rule. |
| Low: integer display second, host variation already covered | [Watch:57](../../Assets/Tests/EditMode/WatchFaceStateTests.cs#L57) | One truncated second; delta **1** | Minimum **1** across measured hosts: Mono returns 29, .NET 8 returns 30. The integer comparison has no fractional overrun. |
| Low: integer sample index | [Clam crest:284](../../Assets/Tests/EditMode/ClamSpurtMathTests.cs#L284), [jump peak:96](../../Assets/Tests/EditMode/HookedJumperTests.cs#L96) | One sample index; delta **1**. Sampling is 10 ms and `jumpDuration/100` respectively. | **1** covers the stated adjacent-bin allowance exactly; no floating enlargement. Actual current maxima were not separately replayed, so 0 is not claimed as a fixture minimum. |
| Low: integer currency | [Tank quote:239](../../Assets/Tests/EditMode/FuelLadderTests.cs#L239), [carried-can charge:230](../../Assets/Tests/PlayMode/GasStationCanJourneyPlayTests.cs#L230), [station charge:204](../../Assets/Tests/PlayMode/NineMileCreekStationPlayTests.cs#L204) | One currency unit after ceiling; delta / `Within` **1** | **1** is the smallest integer tolerance retaining the present one-unit allowance; no epsilon. The two journey cases' actual native residuals are unmeasured. These are not float-distance comparisons. |

## Pixel budgets and adjacent half-unit boundaries

A pixel allowance is sometimes an acceptance budget on continuous geometry,
rather than quantization of the asserted value. Those cases are included here
so a textual “one pixel” match is not silently turned into a recommendation to
weaken a visual guard. No picture was rendered, judged or re-baselined.

| Locations | Unit / current allowance | Minimum or reason to keep it |
|---|---|---|
| [Landing floor:126](../../Assets/Tests/EditMode/StPetersLandingDeckTests.cs#L126), [127](../../Assets/Tests/EditMode/StPetersLandingDeckTests.cs#L127), [128](../../Assets/Tests/EditMode/StPetersLandingDeckTests.cs#L128), [129](../../Assets/Tests/EditMode/StPetersLandingDeckTests.cs#L129) | Native pixel `q=1/32 m=0.03125f`; all four tolerances `q` | `q` is exact in binary. The independent picture coordinates and contract surface are also held equal by later exact-rectangle assertions. No evidence for widening; a native finite minimum was not measured. |
| [Card centre:178](../../Assets/Tests/EditMode/UI/ChartplotterHostTests.cs#L178), [181](../../Assets/Tests/EditMode/UI/ChartplotterHostTests.cs#L181); [max card:335](../../Assets/Tests/EditMode/UI/ChartplotterMaxFaceTests.cs#L335); [card matrix:68](../../Assets/Tests/EditMode/UI/HelmRaisedCardLayoutTests.cs#L68), [105](../../Assets/Tests/EditMode/UI/HelmRaisedCardLayoutTests.cs#L105) | Screen pixel `q=1`; tolerance `1f` | Floor-to-pixel centering has an error strictly below one pixel in exact arithmetic. Keep 1 for the finite screen matrix. An exact smallest residual requires replaying all listed layout cases; arbitrary screen sizes need a float-roundoff term rather than an assumed strict `<1` margin. |
| [Pose grips:259](../../Assets/Tests/EditMode/DriverAtTheWheelTests.cs#L259), [262](../../Assets/Tests/EditMode/DriverAtTheWheelTests.cs#L262); [radial grip budget:195](../../Assets/Tests/EditMode/RigBaking/AtvAstrideRiderTests.cs#L195) | Art pixel `1/32 m`; per-axis delta `1/32`, radial limit `<1 px` | The documented per-axis mismatch is about **0.015 m**, radial about **0.679 px**. Keep the current allowances. The sidecar coordinates are continuous; `1/32` is a policy budget, not their numeric quantum. No native finite minimum asserted. |
| [Plotter centreline:141](../../Assets/Tests/EditMode/UI/ChartplotterGlassTests.cs#L141); radar [112](../../Assets/Tests/EditMode/UI/RadarGlassTests.cs#L112), [113](../../Assets/Tests/EditMode/UI/RadarGlassTests.cs#L113), [117](../../Assets/Tests/EditMode/UI/RadarGlassTests.cs#L117), [118](../../Assets/Tests/EditMode/UI/RadarGlassTests.cs#L118), [130](../../Assets/Tests/EditMode/UI/RadarGlassTests.cs#L130), [131](../../Assets/Tests/EditMode/UI/RadarGlassTests.cs#L131) | Screen pixel 1; tolerance `1f` / `1.0` | These assert continuous transform coordinates before rasterization; they do not accumulate one-pixel steps. Keep the budget. No boundary evidence and no exact finite minimum measured. |
| [Deck contact:430](../../Assets/Tests/PlayMode/DockLadderContactPlatePlayTests.cs#L430); [fitting handoff:331](../../Assets/Tests/PlayMode/WalkerFittingPlayTests.cs#L331); [quay gap comparison:346](../../Assets/Tests/EditMode/NineMileCreekQuayFaceWaterlineTests.cs#L346), accumulated into the final failure assertion | One native/pack pixel, `1/32 m` for the first two; `PackPixelMetres` for the quay | Transform/distance budgets, not one-tick rounding. Native minima unmeasured. Keep them; increasing a budget could admit a real contact or placement defect. |
| [Still-water pond:273](../../Assets/Tests/EditMode/StillWaterSeamTests.cs#L273) | Half R16 code, `0.5f*(11f/65535f)` | Current two-host witness minimum **`2.2411346435546875e-5f m`**, well below `0.0000839246205 m`. Keep. Unlike an arbitrary half-code boundary, this fixed code/level is not a midpoint. |
| Tide-board [151](../../Assets/Tests/EditMode/StPetersKeyScenesTests.cs#L151), [1046](../../Assets/Tests/EditMode/StPetersKeyScenesTests.cs#L1046); shadow roots [370](../../Assets/Tests/PlayMode/SpriteShadowCastsPlayTests.cs#L370), [374](../../Assets/Tests/PlayMode/SpriteShadowCastsPlayTests.cs#L374), [491](../../Assets/Tests/PlayMode/SpriteShadowCastsPlayTests.cs#L491), [493](../../Assets/Tests/PlayMode/SpriteShadowCastsPlayTests.cs#L493); [pivot family:158](../../Assets/Tests/EditMode/Art/SpriteShadowPivotAnchorTests.cs#L158) | Half art pixel, usually `1/64 m=0.015625f`; board 1046 derives it from the Def's PPU | These budgets are exactly binary at PPU 32, but their measured transforms need not be. Native minima unmeasured. Keep; there is no source evidence they stop at a rounded target-plus-tolerance predicate like the punt. |
| [Foam window:122](../../Assets/Tests/PlayMode/WakeFoamWindowPlayTests.cs#L122) | Half cell, `FoamBuffer.CellSize*0.5f` | A cap on accumulated positional error, not the cell-crossing cadence. Source expects arithmetic dust far below half a cell. Native minimum unmeasured; keep the existing limit. |
| [Water lag:201](../../Assets/Tests/EditMode/Art/WaterUniformContinuityTests.cs#L201); [noon channel:379](../../Assets/Tests/EditMode/SunSideMeasurementTests.cs#L379) | One seabed code `10/255 m` and one channel code `1/255` respectively; strict upper bounds | Budgets on continuous lag/mean colour, not quantized outputs. The lag also has the tighter `<0.02 m` guard at 199, already below a height code. No evidence for numerical widening; exact native minima unmeasured. |

## Matches that already contain margin, or measure another unit

- Punt wake/centre assertions at
  [935](../../Assets/Tests/PlayMode/PilotableFleetPlayTests.cs#L935) and
  [948](../../Assets/Tests/PlayMode/PilotableFleetPlayTests.cs#L948), and skiff
  [977](../../Assets/Tests/PlayMode/PilotableFleetPlayTests.cs#L977) and
  [1001](../../Assets/Tests/PlayMode/PilotableFleetPlayTests.cs#L1001), are zero
  targets. `Abs(value)<=0.01f` and a zero-target absolute tolerance admit the
  same float values. The wake value is assigned exactly zero. They do not
  inherit the nonzero-target subtraction hole.
- The cabin's strict deadline checks at
  [454](../../Assets/Tests/EditMode/BoatCabinDoorWalkthroughTests.cs#L454) and
  [907](../../Assets/Tests/EditMode/BoatCabinDoorWalkthroughTests.cs#L907)
  also add one tick to a settle time. They are sibling boundaries worth keeping
  with the cabin finding, but are not failures in the 24-tick arithmetic witness.
- The caustic assertion at
  [159](../../Assets/Tests/EditMode/Art/HullCausticLightingTests.cs#L159)
  calls `2/255` “one decoded-alpha step.” That is the **per-sample** decoded
  quantum. It asserts the **mean of three samples**, whose quantum is
  `2/(3*255)`; its tolerance spans **three mean quanta**. Do not treat the
  comment as evidence of a one-quantum mean tolerance or widen it again.
- Already guarded: foam drift
  [238](../../Assets/Tests/EditMode/Art/FoamBufferTests.cs#L238) and
  [241](../../Assets/Tests/EditMode/Art/FoamBufferTests.cs#L241) use
  `CellSize+1e-4f`; camera slew
  [282](../../Assets/Tests/EditMode/CameraFeelTests.cs#L282) uses
  `rate*dt+1e-7f`; pixel snap
  [78](../../Assets/Tests/EditMode/PixelGridSnapTests.cs#L78) uses
  `grid/2+1e-6f`; R16 fade
  [508](../../Assets/Tests/EditMode/FoamBufferFadeTests.cs#L508) uses
  `0.5f/65535f+1e-9f`. These are not bare unit tolerances.
- The still-water sweep at
  [284](../../Assets/Tests/EditMode/StillWaterSeamTests.cs#L284) adds `4e-6f`
  beyond half a code; the component assertions at 319, 321, 328, 330 and 411
  add `1e-6f`. The committed-seabed check at
  [StPetersLayoutTests.cs:731](../../Assets/Tests/EditMode/StPetersLayoutTests.cs#L731)
  adds `1e-4` to one R16 code. Terrain export's
  [R8Tolerance](../../Assets/Tests/EditMode/TerrainPaintAnalyticExportTests.cs#L100)
  is `1.5*code+0.01f`. Preserve these explicit numerical margins.
- [Ramp grade:507](../../Assets/Tests/EditMode/NineMileCreekSceneRoutesTests.cs#L507)
  derives its allowance as `CodeMetres/0.25f`: a height code **per quarter-metre
  station**, not a raw height tolerance. It is added to a maximum-grade bound
  at 544; it is not a rounded polling target. A future review must keep its
  grade units and the two height samples' precision together.
- Matching decimal literals alone is insufficient. Fishing depth
  [92](../../Assets/Tests/EditMode/FishingDepthDropControllerTests.cs#L92)
  uses a `0.05` normalized-depth tolerance and a `0.05 s` tick: different
  dimensions. Conversation heading tolerances and time steps have the same
  coincidence. The mesh crest test at
  [357](../../Assets/Tests/EditMode/MeshRockSmoothnessTests.cs#L357) samples
  `360/500=0.72°` and allows 1°; it is not a one-step tolerance. The tide
  crossing invariant at
  [133](../../Assets/Tests/EditMode/TidePacingInvariantTests.cs#L133) permits
  one in-game minute but computes a continuous analytic duration; it has no
  one-minute sampling step.

Future repairs should retain the tested rule and state which numerical
mechanism changed. A tolerance cannot repair a wrong map, missed update,
incorrect authority or weakened contact requirement. None of those rules or
assertions is changed here.
