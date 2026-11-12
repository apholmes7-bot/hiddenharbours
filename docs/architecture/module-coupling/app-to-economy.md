# App → Economy

Audit base: `12bc04b19a2efb33e352b4eea58104f7a5599be1`. Documentation only; static source inspection, no Unity run.

**Priority: housekeeping only. Classification: declared assembly edge without a runtime source dependency.** App's asmdef lists Economy, but the runtime source scan finds no Economy import, qualified type use, or unqualified Economy type reference resolved from an import. The separate editor assembly does use Economy. An assembly declaration is not evidence of a concrete-class violation by itself.

## Complete runtime inventory

[App asmdef:10](../../../Assets/_Project/Code/App/HiddenHarbours.App.asmdef#L10) declares the edge. There are 0 importing runtime files, 0 distinct referenced target types and 0 explicit type-token sites below. Counts include enums, structs and interfaces where present; method calls through inferred variables are described separately, not counted as new type tokens. Comments and strings are excluded.

No source sites.

`App.Editor` is a separate Editor-only assembly with its own declared Economy edge and 6 import directives to this target. Its scene builders, authoring tools and capture helpers are editor composition; they are excluded from the runtime table and are not shipped feature-to-feature violations.

## Disposition

There is no offending App runtime use site to migrate. Do not invent a new Core interface just to replace an unused reference. A later isolated cleanup can remove the redundant asmdef reference after compilation and serialized-reference validation; this audit changes no assembly definitions. Reflection strings, comments and separate editor sources were not counted as runtime concrete-class uses.

The economically significant App issue is the **App → Boats** hold proxy described in [that edge's page](app-to-boats.md): a Core-shaped proxy can still expose remote sale authority without importing Economy. Replacing Economy references would not close that route. The [proposed `IDeliveredGoodsSale`](../../design/goods-handling/04-first-slice.md) should become the buyer's capability; `CatchSold` reports a completed sale, with exactly-once payment enforced by the authority. No observed defect is attributed to this unused asmdef declaration.
