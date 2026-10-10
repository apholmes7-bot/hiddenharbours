# F2 transport repair — Phase A, 2026-10-10

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
