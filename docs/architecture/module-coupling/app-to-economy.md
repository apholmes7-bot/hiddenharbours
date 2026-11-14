# App → Economy

Audit base: `12bc04b19a2efb33e352b4eea58104f7a5599be1`. Documentation only; static source inspection, no Unity run.

**Priority: housekeeping only. Classification: declared assembly edge without a runtime source dependency.** App's asmdef lists Economy, but the runtime source scan finds no Economy import, qualified type use, or unqualified Economy type reference resolved from an import. The separate editor assembly does use Economy. An assembly declaration is not evidence of a concrete-class violation by itself.

## Complete runtime inventory

[App asmdef:10](../../../Assets/_Project/Code/App/HiddenHarbours.App.asmdef#L10) declares the edge. There are 0 importing runtime files, 0 distinct referenced target types and 0 explicit type-token sites below. Counts include enums, structs and interfaces where present; method calls through inferred variables are described separately, not counted as new type tokens. Comments and strings are excluded.

No source sites.

`App.Editor` is a separate Editor-only assembly with its own declared Economy edge and 6 import directives to this target. Its scene builders, authoring tools and capture helpers are editor composition; they are excluded from the runtime table and are not shipped feature-to-feature violations.

## Disposition

### Isolated cleanup after the audit

The runtime App assembly no longer declares the unused Economy reference. The
original inventory above describes the audit base; the current App runtime scan
still finds no concrete Economy consumer across its 38 C# sources. The separate
App.Editor reference remains intact. No Core seam is needed for this removal.

The cleanup is checked by compiling the complete App runtime source list with only
the project references declared by its updated asmdef, explicitly excluding Economy
from the compiler response. Its dependency sources are rebuilt first. No source,
scene, prefab, content asset, assembly name or meta GUID is changed by the removal.
This is a compiler proof; native editor import, player build and serialized-object
loading still require CI. The asmdef meta and App.Editor definition remain byte-identical.

There was no offending App runtime use site to migrate at the audit base. No new Core interface was needed to replace an unused reference. The original audit changed no assembly definitions; the isolated cleanup above removes only the redundant runtime reference. Reflection strings, comments and separate editor sources were not counted as runtime concrete-class uses.

The economically significant App issue is the **App → Boats** hold proxy described in [that edge's page](app-to-boats.md): a Core-shaped proxy can still expose remote sale authority without importing Economy. Replacing Economy references would not close that route. The [proposed `IDeliveredGoodsSale`](../../design/goods-handling/04-first-slice.md) should become the buyer's capability; `CatchSold` reports a completed sale, with exactly-once payment enforced by the authority. No observed defect is attributed to this unused asmdef declaration.
