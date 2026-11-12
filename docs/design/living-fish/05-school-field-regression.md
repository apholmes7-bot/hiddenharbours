# SchoolField soak and slow-tick cost

This guards the disabled, engine-light field foundation. It does not activate a consumer, author habitat, change species assets, or prove the cost of rendering, world sampling, catches or saves. It serves the living coast and the existing deterministic simulation and performance rules.

## Determinism envelope

`SchoolFieldSoakTests` checks 1,000 distinct seeds, including both signed integer extremes, for each of the five `LivingFishKind` values. Nine instants include zero, fractional seconds, both sides of a day boundary, one year, one hundred years, and stress instants beyond single-precision clock resolution up to 1,000,000,000,000.25 Core game seconds. The fixture day is 1,800 game seconds; these are synthetic test inputs, not new balance values.

Every populated sample is compared field by field, including exact float/double bits, after reversed queries, unrelated cell queries, truncated buffers, changed catalogue position and fresh field construction. Active reactions are also tested at long instants. The soak checks certified-disc containment, depth bands, finite bounded tightness, counts and sessile immobility. A separate 1,000-seed sparse case exercises both presence and absence through long time jumps; it checks repeatability without freezing the current hash outputs. These tests establish repeatability within a runtime, not cross-platform transcendental-function bit identity or serialized save compatibility.

## CPU regression envelope

`SchoolFieldCostTests.SlowTick_512SuccessfulSamples_MedianStaysWithinTwoMilliseconds` queries **32 certified cells × 16 enabled synthetic species = 512 successful samples per tick**. Species cover all five movement kinds, with preferred vegetation, deep water and an active reaction. A caller-owned result buffer is reused. Setup, native asset creation, assertions and logging sit outside the timed section. There are 32 warmup ticks, then seven timed rounds of 16 ticks. The median round time divided by 16 must be **at most 2 ms per tick**. Run it with the ordinary sequential EditMode runner; it requires no graphics device, scene or Play mode.

At a proposed four refreshes per real second, that allowance is 8 ms of CPU per second, or 0.8% of one core. This is a bounded query workload for a future slow-tick integration, not a production scheduling change or measured frame-time promise. More cells/species, habitat certification, consumers, rendering and garbage collection elsewhere need separate profiling before activation. The median reduces scheduler noise; it deliberately does not claim a maximum or percentile frame budget. Investigate a regression rather than raising the limit to match it.

The separate allocation case demands zero managed allocation events over 16 warmed ticks (8,192 successful samples). Its current-thread `GC.Alloc` recorder must first detect a retained allocation and then an empty scope. An unavailable or unresponsive recorder fails explicitly. This supersedes the earlier positive thread-byte probe that skipped unsupported counters; the zero-allocation rule, workload and successful-sample assertion are unchanged. See the [allocation guard audit](../../verification/managed-allocation-guards.md).

## Recorded validation

On 2026-11-11, Unity 6000.5.0f1 ran all 33 requested EditMode cases: **32 passed, zero failed, one explicitly skipped**. Counts were SchoolFieldTests 24/24, LivingFishContentValidationTests 1/1, SchoolFieldSoakTests 6/6, and SchoolFieldCostTests one pass plus the allocation-counter skip. Each movement kind completed 18,000 exact sample comparisons (90,000 total); the sparse case exercised 497 present and 503 absent seeds. The measured native median was **0.1356 ms/tick** against the unchanged 2 ms allowance.

A supplementary standalone run on the pinned Mono, with no editor or graphics device, passed all eight new cases and measured **0.1647 ms/tick**, with **zero managed bytes over 8,192 successful samples** after a positive allocation-counter probe. It linked the actual field/profile/hash sources and Unity's managed Vector2. Only native species creation/destruction was replaced by a plain managed species stand-in; all NUnit assertions remained intact. This supplies arithmetic and CPU evidence, not native asset/serialization/save proof.

The keeper refused the native editor's no-graphics flag, so the native run used its required D3D11 configuration. These fixtures themselves use no graphics API; native no-device execution remains for CI. Offline Core, Environment, Fishing and broad EditMode compilation also passed with zero errors. The field/profile/hash audit found no active hidden RNG or wall-clock reads. No prior test assertion, scene, visual asset, runtime logic, save format or owner tuning is changed by these fixtures.
