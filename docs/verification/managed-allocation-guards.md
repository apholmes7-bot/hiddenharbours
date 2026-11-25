# Managed allocation guard audit

This audit covers the managed allocation assertions and cost reports using
`GC.GetAllocatedBytesForCurrentThread`, `GC.Alloc`, or Unity's
`AllocatingGCMemory` constraint. Heap-size, texture-memory and rendering counters
measure different things and are outside this zero-allocation claim.

## Measurement contract

A zero from an uncalibrated counter is not proof. `ManagedAllocationProbe` uses
current-thread raw `GC.Alloc` event samples. Each instance warms its observation
path, detects a retained 1,024-byte allocation, and checks an empty scope before
measuring the existing workload. Calibration failure throws and releases the
recorder. Each scope resets the recorder; a full sample buffer remains nonzero.
Sample counts are events, not bytes or sample durations. Zero events preserves
the existing zero-managed-allocation rule without depending on a stubbed thread
byte counter. Workload sizes, successful-result checks and zero bounds stay intact.

## Findings and changes

| Guards or reports | Finding and disposition |
| --- | --- |
| SchoolField query; benthic movement | Uncalibrated byte zeros replaced with calibrated event scopes. |
| WaveMemory sampler; WaveSwell evaluation | Same replacement. Swell now warms the complete measured path (compose, shelter, sample); the old warmup only composed. The 10,000 measured evaluations and zero bound stay intact. |
| FoamSelection overflow | Same replacement; slot and identity assertions stay intact. |
| HullSunShadowCaster; HullDeckShadow; both settled SunCasterInstaller guards | Same replacement; only test instrumentation changes. |
| WalkerFitting follower cost | Same replacement; output now says `managed_allocation_events`, never allocated bytes. |
| SchoolField slow-tick cost | Existing positive byte probe correctly skipped unsupported counters. A calibrated event scope now provides zero-allocation proof on those runtimes too; all 8,192 successful samples remain required. |
| FoliageSnowBridge; IsoCharacterFigureLife | Added retained positive allocations before their existing Unity zero-allocation constraints. |
| HullSunShadow rendering cost command | Byte-only report now requires a working thread byte counter before producing a report; an unsupported counter fails explicitly. |
| CharacterBuilder cost | Existing byte bound already had a positive probe. Calibrate before measurement and label unavailable bytes in the report instead of printing a false zero. The CPU bound still runs. |

The following existing guards already have positive controls and remain intact:

- CharacterFigurePresenter, FigurePixelGrid, CurlingBreaker, GullHaunt, EventBus
  and SeaReadout: Unity allocation constraint plus a separately calibrated byte arm.
- FigureHeadingDial, IsoFacetFigureTarget, IsoCharacterFigureV9, HelmBandController,
  HullTrimMath, HullTrimDriver and CurlingBreakerSilhouette: Unity constraint with
  a positive allocation before the zero guard.
- WaterSiltEnvironment and FoamTransportAllocationProbe (including its foam and
  hull-caustic callers): same-recorder retained allocation and empty-scope checks.

## Verification and limits

Two EditMode probe tests verify retained-allocation detection and isolation of a
subsequent empty scope. Supplementary fault injection compiles the actual probe
against a recorder stand-in: unavailable, valid-but-stuck-at-zero and dirty-empty
recorders must fail and dispose their resources; a calibrated sequence must
preserve counts and reset scopes. A host byte-counter positive control is separate
from native Unity proof.

No runtime feature, art source, scene, save schema, balance value or pixel
expectation changes. No performance improvement or appearance acceptance is
claimed. Native results and any unrun cost commands are recorded in the pull
request; compilation alone does not prove their allocation behavior.
