# F2 transport repair — Phases A and A2, 2026-10-10

P1 / rule 7. Repairs the shared-material transport recording and CPU field preparation
from #930. All material dials remain zero. No gameplay, surf, source/decay shader,
water shader, package, save, cadence or tunable changes.

## Draw ownership

`HullPass.RecordTransport` obtains a clean temporary `MaterialPropertyBlock` from
`RasterGraphContext.renderGraphPool` **during execution**, resolves the declared texture
handles there, and calls `DrawProcedural` with that block. The command buffer copies
the block's values for that draw. The graph returns the scratch block to its pool at
the end of the pass; subsequent steps/cameras can reuse it without altering a recorded
draw. This avoids a material per matching and has no steady managed allocation after
pool warmup. The same fix covers the foam source pass, whose material was shared across
cameras. Both shaders already require shader model 3.5 or higher and use the same
full-screen triangle vertex shader as the previous Blitter call. `_BlitScaleBias`
remains `(1,1,0,0)`.

The zero-strength gate and Hold route are unchanged: no transport exchange is recorded.
`FoamTransportRepairTests.Repair_RecordedFoamDrawUsesCopiedPropertyBlock` rejects the
old recording on Null-device CI. It is a structural regression guard, not a GPU proof.
`FoamTransportGpuTests` remains the actual endpoint/code/boundary gate. The four new
`FoamTransportBindingGpuTests` cases assert every coverage and freshness code after
one/two camera submissions and one/four steps, sharing one material throughout.
They extend F2E's production-arm diagnostic into assertions; its untracked files remain
untouched. Null devices skip these GPU fixtures explicitly.

## Field changes and exact triggers

| Input / existing source | Work |
|---|---|
| Camera whole cells: `WorldCellOrigin`, plus retained `DriftResidual` | Reuse ring-cached terrain bounds; rebuild entering strips. Copy overlapping mask rows, rebuild entering one-metre tiles. Velocity changes only when its world-metre origin changes. A changed fractional **drift residual** invalidates terrain; a sub-cell camera pan within the same snapped cell does nothing. |
| Hull footprints: `HullPresences`; authored contacts: `FoamTransportContacts.Capture` | Compare all captured shape fields, including stable key and wet interval. Rebuild the old/new footprint tiles of changed entries and compose their complete solid union. Collection support is recomputed only if collection is active. Add/remove/enable/disable/rotate/resize are changes too. |
| Waves: `WaveFieldBridge.ReadPublishedField` | Exact comparison of count, active trains (direction, wave number, amplitude) and wrapped phases; rebuild velocity only. No new phase or time source. |
| Drift: `FoamInjectionRegistry.DriftVelocity` | Rebuild velocity and its bounded collection support; leave mask alone. |
| Sea: `FoamInjectionRegistry.TransportSeaLevel` | Reclassify only cells crossing cached dry thresholds, in one-metre tiles. Re-evaluate contacts' existing wet intervals. No terrain-byte resampling. |
| Strengths / drawn scale: `FoamInjectionRegistry` | Rebuild velocity; scale also rebuilds its slope basis. Zero strengths retain the production early-out/release. |
| Bed/still maps: `SeabedGlobals.CpuMap`, `StillWaterGlobals.CpuMap` | Texture identity/updateCount, mapping rectangle, decode range or filter changes invalidate terrain. Validate readability, clamp/linear format and finite inputs every call. Invalid input holds movement; recovery rebuilds valid state. |

There is no slow-tick approximation. Every prepared field uses the current explicit
state. Unchanged preparation rebuilds zero cells/nodes and subsequent Upload does no
Apply. Pending uploads survive multiple Prepare calls until actually uploaded.
Velocity-only changes upload 78,408 bytes at 96 m; a changed mask additionally uploads
589,824 bytes. Texture2D Apply still uploads the whole changed texture, not dirty rectangles.

For terrain, `bed >= max(sea, still)` is exactly `sea <= dryAt`, where `dryAt` is bed
when bed >= still and negative infinity otherwise. One float replaces two cached
bounds. Tile extrema skip tide intervals with no crossing; negative-infinity thresholds
are excluded from extrema. Tiles are eight cells (the existing metre/foam-cell ratio),
an implementation partition, not a new look or scheduling parameter.

Velocity preserves the reviewed law. Double-precision angle addition factors each
train's regular grid into x/y sin/cos evaluations, retaining train accumulation order
and float weights. It is recomputed from world coordinates, never integrated across
frames. It may differ from scalar evaluation by float rounding; tests bound the error
to 2e-6 m/s and retain the unchanged scalar law as the oracle. Collection evaluates
only a conservative rectangle around each contact's existing support, preserving
maximum-weight selection and the existing geometry/key tie-break.

## Budget and verification boundary

At 768² / 99², auxiliary CPU payload is approximately 3,962,704 bytes (excluding object
headers, existing source maps and driver staging), below the approved 5,395,016-byte
ceiling returned by `FoamTransport.CpuBytes`. GPU storage and the maximum upload size
are unchanged. No new package is required.

Normal eight-train changing-wave work: 78,408 slope accumulations, 3,168 trig calls
(previously 78,408 cosines), 9,801 final velocity nodes, at most 27,648 cheap tile-flag
checks, plus bounded contact support and dirty mask tiles. Estimate: 0.10 ms capture
and dirty checks + 0.45 ms velocity + 0.15 ms local mask/collection + 0.15 ms upload =
0.85 ms. This is an estimate, **not** measured Unity p95. Cold map replacement, a large
window jump and 128 large contacts are separate worst cases, not covered by that number.

**Superseded estimate:** Phase B measured advancing-wave Prepare + Upload at **1.2971 ms
p95**, so Phase A did not meet its CPU gate. The second CPU design below is authorized
by the owner's 10-10 ruling. Its estimates also require a new slot measurement.

Phase A uses standalone Roslyn with this checkout's warm references and host-side
texture/API shims for CPU logic checks. Neither executes Unity. Phase B must run the
unchanged foam/surf/shader regressions, the new EditMode guards, both GPU fixtures,
production-graph tracer controls, and the normal/worst-case profile through the keeper.
CPU preparation+upload p95 <= 1 ms, full added GPU including uploads <= 1 ms, and steady
production-graph GC 0 B remain acceptance gates. Isolated exchange timings and the old
manual-submission tracer/profile harness cannot substitute for those graph measurements.

## Other material-binding audit finding

`HullPass`'s ordinary hull keyline resolve still mutates the feature-wide
`ResolveMaterial` in its render function, including the keyline/figure-ink helpers.
It has no ownership protection for multiple outstanding camera draws and relies on
submission ordering. Treat it as unsafe; it is left for the seat under this repair's
scope. Mesh-reflection resolve owns a material per camera snapshot/lease. Renderer-list
passes, snapshot mesh draws, silhouette and history initialization do not mutate a
shared material in their render functions. The complete file/line audit is in the
local Phase A evidence report.

## Phase A2: preserve the field, remove CPU overhead

No schedule, new period, GPU evaluator, package, resolution or strength change. Preserve
the Phase A float operation order, train order, phase evaluation and CFL maximum. The
draw/property-block binding repair and all shaders are unchanged. The optimization is
CPU-only; it serves P1 without changing the transported field or its sampling cadence.

### What Phase B actually measured

At the combined p95 sample: Prepare **1.2704 ms**, Upload **0.0267 ms**. Separate stage
percentiles were Prepare 1.2707 and Upload 0.0371; they cannot be summed to reconstruct
the combined percentile. Frozen-input warm Prepare + Upload p95 was 0.0463. Thus almost
all extra work in the advancing-wave arm is the slope build and the velocity/max scan.
The old CSV has **no internal timestamps**: the following internal split is a planning
estimate constrained by that total, not a retrospective measurement.

| Step | Phase B estimated contribution (ms) | A2 estimate (ms) |
|---|---:|---:|
| Input validation, live contact capture/sort, map/dirty checks, final state | 0.05 | 0.05 |
| Wave factors: wrapped angles and sine/cosine | 0.25 | 0.13 |
| 78,408 train/node slope accumulations | 0.52 | 0.46 |
| Separate 9,801-node normalization pass | 0.07 | fused below |
| Velocity compose, finite/uniform/max scan and native writes | 0.38 | 0.14 |
| Single retained-buffer native copy | included above | 0.01 |
| Prepare total | 1.27 | 0.79 |
| Upload allowance | measured 0.0267 at combined p95 | 0.04 |
| Additional planning margin | — | 0.06 |
| Prepare + upload, normal-case planning estimate | measured 1.2971 | **0.89** |

The allocation/memory-safety cost on Unity's editor VM is the uncertain term. The estimate
allows roughly 0.20 ms saved by bulk-copying velocity and simplifying its component loop;
B2 must test that assumption. A host .NET/shim comparison measured dynamic p95 0.2137 ms
before versus 0.1738 after (about 19% lower), **without Unity native safety checks or
upload**. That ratio alone would not establish a sub-1-ms Unity result. It supports the
direction of the arithmetic change, not acceptance or a promised p95.

### Implementation and invalidation

- Retain the per-train Y-axis sine/cosine factors. Published phase appears only in the
  X-axis angle. Advancing phase therefore needs 1,584 trig calls instead of 3,168 at
  eight trains / 99 nodes per axis. Y factors refresh on initial/invalid state, an added
  train, origin Y, scale, direction Y or wave number. No phase is integrated over time.
- Keep slope components in two float arrays, with row addresses hoisted out of the
  inner loop. Accumulate trains in the old order. Divide by the same normalizer in the
  final consumer and use the old parentheses for strain, curl, drift and collection.
- Compose all 9,801 velocities into one retained managed array, then call
  `NativeArray<Vector2>.Copy` once. This replaces 9,801 checked native indexer writes.
  Max component, finite checks and the Uniform/Local decision use those same floats.
- Scroll masks with two bulk copies total (snapshot, overlapping linear interval),
  instead of snapshot plus up to 768 individual row copies. Clear the interval's
  entering row edges before use. Count departing occupied cells from the unchanged
  snapshot. Terrain ring addressing and entering-cell selection stay the same.
- Hoist terrain ring row addresses and world Y out of the cell loop. Map bound
  evaluation uses scalar components with the same filtering, thresholds and operations.

Only wave factors, slope accumulation and final velocity compose/copy are forced by an
advancing phase. Collection does not rebuild merely because phase advances; neither do
terrain or mask. The existing map/revision/filter/rect/decode, tide, contact, drift,
strength and cell-origin triggers remain in force. No-change Prepare still rebuilds
zero cells and Upload sends nothing. Fail/hold and dirty-upload recovery remain intact.

At 96 m, extra retained payload versus A is 78,408 bytes for velocity staging, 23,760
bytes for the expanded axis factors, and 32 bytes for weights: **102,200 bytes**. Total
declared CPU payload becomes **4,064,904 bytes**, below the existing 5,395,016-byte budget
(object headers/native driver overhead excluded, same convention as Phase A). No GPU
texture or upload size changes. There is no allocation in the preparation loops.

### Sailing arm and remaining risk

B2 adds **3 m/s (5.83 knots), 60 Hz**, advancing the same rough-wave spectrum while
the camera moves along world X. `WorldCellOrigin` produces alternating two/three-frame
whole-cell crossings; the flow lattice crosses every metre. The profile captures both
ordinary crossings and those that invalidate collection/spatial factors. A separate
full-production arm moves the actual camera at this speed.

Planning estimate: **0.89 + 0.12 terrain/scroll + 0.05 mask + 0.04 collection/lattice
crossing + 0.02 extra upload = 1.12 ms p95**. This estimate is **over 1 ms**; it is not
approved or relabelled acceptable. B2 must report its measured number for the owner's
ruling. The bulk scroll is intended to reduce the old 1.6263-ms pan spike, not to hide
it in an average. Retain the old forced pan, cold, tide, dense and camera/step arms.

### Observation, tests and B2 gate

`FoamTransportPreparationStage` and the per-field optional `StageCompleted` callback
carry only stage labels. Production owns no timestamps, stopwatch, schedule or observer.
The untracked B2 harness supplies one cached delegate per field and preallocated counters.
Its CSV separates input/contact capture, map inputs, terrain/scroll, mask, wave factors,
slope accumulation, collection, velocity compose/max, bulk copy and final state. The
outer Prepare measurement includes callback overhead, conservatively; no subtraction.

`FoamTransportCpuRepairTests` adds ten cases: four train counts (0/1/4/8) checked against
the Phase A arithmetic by exact float bits at advancing phases; spatial/spectrum cache
invalidation; phase-visit independence; sailing versus fresh masks/fields; zero allocation
with observer/bulk copy; observer transparency/steady no-work; and a positive control
requiring the allocation counter to detect a known allocation. Existing tests are
unchanged. If the counter control fails in Unity, zero-byte samples are **not verified**.

Headless shims are only arithmetic checks. The acceptance gate remains Unity B2:
normal Prepare + Upload p95 <= 1 ms, full added GPU including upload <= 1 ms, valid
whole-production-graph steady GC 0 B, unchanged numerical/binding tests, and the sailing
result explicitly reported. Request **45 minutes**, including cold import and reporting:

- Import/compile and device setup: 10 minutes.
- EditMode: every F2 class including `FoamTransportCpuRepairTests`, and the same foam,
  surf, history-state and `WaterShaderCompileGuardTests` regression set: 12 minutes.
- PlayMode: `FoamTransportGpuTests`, all four `FoamTransportBindingGpuTests`,
  `FoamHistoryInitializationGpuTests`, `WakeFoamWindowPlayTests`: 8 minutes.
- `F2D_ProfileNormalAndWorstCases`, `F2D_GpuExchangeEvents`, full-production profile
  including sailing, and all `F2B_*` controls: 8 minutes.
- Restore named Unity rewrites, audit results, disarm and return keeper slot: 7 minutes.

The eight unchanged SurfSequence reds are confirmed by the seat on plain main. History
GPU cases require D3D12 and skip under the keeper's D3D11 policy. Neither is silently
changed. The six local harness files remain behind `F2_REPAIR_PHASE_B_MANUAL`, with no
lease granted and the B2 arm file disarmed. No Unity or push in A2.
## Phase A3: sailing, exact Mono rounding and a calibrated allocation counter

Owner ruling, 2026-10-11: both advancing-wave normal and sailing at 3 m/s must
remain at or below 1 ms p95 CPU preparation plus upload. No Unity was run in A3.

The terrain already used toroidal storage. A3 also retains the composed mask in
that ring: a pan advances the two origins, samples only entering terrain and
composes only entering mask cells. Contact/tide changes retain their existing
dirty-tile rules. Filter-support ranges are cached separately on each ring axis;
an entering column computes one X range instead of repeating divisions/floors
for every cell. Contiguous cells with identical filter support share its decoded
bound, without changing any sample or max/min operation.

The shader still receives its original linear R8 mask. Two retained staging
arrays provide one non-overlapping bulk copy of the retained linear view, followed
by entering-strip writes and one checked copy into texture backing. The ring
itself never moves. This deliberately retains a shader-facing bulk copy: removing
that last linear-view copy would require changing the texture/shader contract.
No shader, material, asset, resolution, strength, cell law or scheduling change is
part of A3. The normal unchanged frame still rebuilds and uploads nothing.

Mono's overlapping `Buffer.BlockCopy` was slower in the headless experiment, so
the two linear buffers exchange roles after a non-overlapping copy. Retained CPU
array/texture payload is **5,269,128 B**, checked against the unchanged **5,395,016 B**
budget. Object headers, borrowed maps and driver storage are excluded consistently
with the existing payload accounting. GPU storage and upload sizes are unchanged.

The velocity loop retains Phase A's single-precision storage boundaries explicitly.
It combines finite checking with its existing magnitude/max calculation and stops
rechecking uniformity once disproved. Ordered magnitude comparisons reject NaN and
infinity and preserve both finite endpoints; exact-output and endpoint tests cover
this change. No approximation or ULP tolerance was introduced.

### Arithmetic estimate, not an acceptance measurement

Milliseconds per frame, including the expensive sailing crossing. These are
planning allowances anchored to B2 stage timings and comparative Mono experiments;
the headless texture/NativeArray shims do not model Unity safety checks or uploads.
Neither headless time nor an estimate certifies the 1 ms p95 bar. B3 must measure it.

| Stage | Normal | Sailing, 3 m/s at 60 Hz |
|---|---:|---:|
| Inputs/contact capture | .002 | .002 |
| Map checks | .004 | .004 |
| Terrain/support ranges/linear staging | .006 | .120 |
| Mask composition/native snapshot | .006 | .040 |
| Wave factors | .025 | .025 |
| Wave accumulation | .380 | .380 |
| Collection (including a flow-lattice crossing) | .001 | .030 |
| Velocity compose/finite/max | .295 | .295 |
| Velocity copy | .004 | .004 |
| Finish | .002 | .002 |
| **Prepare** | **.725** | **.902** |
| Upload allowance | .035 | .055 |
| Timing/load allowance | .040 | .030 |
| **Total estimate** | **.800** | **.987** |

The sailing estimate has little headroom. Its uncertain terms are the map reads,
native snapshot/upload and Mono compose cost; the ceiling remains 1 ms if B3 exceeds
these allowances. Frozen frames retain the zero-work fast path. Advancing waves
alone still rebuild wave factors, accumulation, compose and velocity copy; they do
not dirty terrain or mask. No slower tick or temporal approximation is used.

### Exact-value failure evidence

The standalone bundled Mono runtime reproduced all four B2 exact-value failures,
including their identical signed integer bit patterns. A minimal diagnostic shows
the A2 scalar result equals a double-intermediate compose; Phase A's Vector2 stores
round each intermediate to single precision. Explicit float conversions at those
same boundaries remove the discrepancy. This is not a cache invalidation defect or
an unavoidable runtime difference, and no test tolerance was loosened.

| B2 failing case | Expected bits | Actual B2 bits |
|---|---|---|
| Advancing phases, 1 train, node (2,0) Y | BECC5456 | BECC5457 |
| Advancing phases, 4 trains, node (0,0) Y | BF1262C5 | BF1262C6 |
| Advancing phases, 8 trains, node (5,0) Y | 3D80D0AA | 3D80D0A9 |
| Spatial/spectrum change, node (3,0) Y | 3DF44136 | 3DF44135 |

Assertions now print round-trip floats and hex bits on a mismatch. The existing
31 repair cases plus 26 CPU repair cases pass in both .NET and standalone Mono with
headless engine/storage shims. This does not claim a Unity test run. Twelve new pan
cases cover 1/4/8 pans, positive X, positive Y and both diagonal directions, each in
Point and Bilinear filtering over R16 seabed and R8 still-water maps. Each starts at
a physical ring seam and checks every terrain bit, mask byte and velocity component
against a fresh field, plus occupancy, route and entering-cell work counts. The two
additional cases pin the retained-memory ceiling and finite/NaN/infinity behavior.
Two further cases pin logical mask upload after a ring reset caused by a map revision
or invalid-frame recovery, even when the physical ring bytes remain unchanged.

### Managed allocation and thread coverage

The fifth B2 red exposed Unity's zero-returning `GC.GetAllocatedBytesForCurrentThread`.
The committed tests now use `ProfilerRecorder` on `GC.Alloc` with
`CollectOnlyOnCurrentThread`, without per-frame aggregation. **Count measures
allocation events; Value is nanoseconds, not bytes.** Each zero-allocation test
requires a known `new byte[256]` to register, then verifies an empty scope. The
standalone headless build uses the host byte counter with the same controls. Existing
steady/changing-Prepare guards retain their zero-allocation assertion.

The managed production call chain is synchronous:

- Unity's main thread calls `IsoFacetHullFeature.HullPass.RecordRenderGraph`, which
  calls `PrepareTransport`, live contact capture, `Prepare` and `Upload`.
- The installed core pipeline's `RenderGraph.ExecuteNativeRenderGraph` calls
  `NativePassCompiler.ExecuteGraph`. Its ordinary pass loop calls
  `ExecuteRenderGraphPass`, then `pass.Execute`, then the registered `renderFunc`.
  This covers the advect and transport property-block recording/draw callbacks.
- F2 creates no managed job, task, worker thread, async continuation or deferred
  managed callback. A graphics/render thread, native graphics workers and the GPU
  execute native commands/shaders, not F2 C#; their native/VRAM allocations are not
  allocations in the managed GC heap. B2's device log specifically reported
  `GfxThreadingModeNonThreaded`. B3 records the actual graphics-threading mode too.

Thus the calibrated current-main-thread scope covers this graph's managed allocation
work. B3 must validate that premise: assert UnitySynchronizationContext, capture the
main thread ID, verify submission and every measured field's preparation callbacks
stay on it, and record zero allocation events over the entire production submission.
Controls run before and after capture. A failed control/thread assertion invalidates
the proof; it is never converted to a zero-byte pass.

The unsupported B2 all-thread recorder configuration is removed. B3 uses the working
current-thread option plus the call-chain audit and runtime thread checks; no claim
is made that the invalid all-thread attempt produced data. The disabled temporary
harness is preserved under `Evidence~/f2-repair/phase-a3/b3-harness/` for review.

### B3 request: 30 minutes, cold import included

Run the normal/sailing/worst stage profile, isolated GPU events, actual production
GPU/upload/GC profile, the four binding combinations, FoamTransportGpuTests, every
F2 EditMode class, main's foam regression set, FoamHistoryInitializationGpuTests,
WakeFoamWindowPlayTests, WaterShaderCompileGuardTests and every F2B control. The known
main surf reds and unchanged D3D12-only skips must remain visible. Budget 10 minutes
for import/compile, 8 for regressions, 6 for profiles/GPU/controls and 6 for evidence,
restoration, disarm and return. No slot is taken, no Unity launched and nothing is
armed during A3. Main 640e3cc7 is not incorporated.
