# App → Environment

Audit base: `12bc04b19a2efb33e352b4eea58104f7a5599be1`. Documentation only; static source inspection, no Unity run.

**Priority: preserve during the first slice; no blocking edge removal. Classification: permitted service composition.** App constructs the concrete clock/environment and publishes their Core interfaces. One opening-specific downcast reads authored start time. This is different from making goods freshness depend on an Environment implementation.

## Complete runtime inventory

[App asmdef:7](../../../Assets/_Project/Code/App/HiddenHarbours.App.asmdef#L7) declares the edge. There are 2 importing runtime files, 2 distinct referenced target types and 3 explicit type-token sites below. Counts include enums, structs and interfaces where present; method calls through inferred variables are described separately, not counted as new type tokens. Comments and strings are excluded.

| Runtime source | Referenced target types (source lines) |
|---|---|
| [ArrivalOpening.Sea.cs:35](../../../Assets/_Project/Code/App/ArrivalOpening.Sea.cs#L35) | `GameClock` 35 |
| [GameRoot.cs:35](../../../Assets/_Project/Code/App/GameRoot.cs#L35) | `EnvironmentService` 36; `GameClock` 35 |

`App.Editor` is a separate Editor-only assembly with its own declared Environment edge and 4 import directives to this target. Its scene builders, authoring tools and capture helpers are editor composition; they are excluded from the runtime table and are not shipped feature-to-feature violations.

## Existing seam

[GameRoot.cs:35–37,61–63](../../../Assets/_Project/Code/App/GameRoot.cs#L35) serializes concrete services and publishes them as `GameServices.Clock`, `Environment` and `Wallet`. Teardown at lines 110–113 only clears registrations it owns. This is exactly the composition-root role; consumers continue to read Core `IGameClock` and `IEnvironmentService`.

[ArrivalOpening.Sea.cs:34–35](../../../Assets/_Project/Code/App/ArrivalOpening.Sea.cs#L34) casts `GameServices.Clock` to `GameClock` to obtain `StartSeconds` and call `SeekTo`, then holds the hour through Core. Core `IGameClock` already exposes `SeekTo`; only the authored starting instant is concrete-specific. If another feature needs this operation, inject the authored start value at composition and call Core `IGameClock.SeekTo`, rather than exporting `GameClock`. No additional EventBus signal is needed for a read or a lifecycle seek, and a seek must not impersonate ordinary elapsed time.

## Goods preservation constraint

The [first-slice model](../../design/goods-handling/04-first-slice.md) keeps settle-on-read freshness and settle-before-mode-change. Goods must use the authoritative game clock through Core, with restore handled at the save boundary, never wall time, an App-local timer or a new per-container clock. Test later that carried/container stock preserves the same freshness across pause, region travel and load. This is a migration guard, **not an observed defect** in these App references; no runtime test was executed here.
