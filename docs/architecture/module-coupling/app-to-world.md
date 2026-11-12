# App → World

Audit base: `12bc04b19a2efb33e352b4eea58104f7a5599be1`. Documentation only; static source inspection, no Unity run.

**Priority: after the first goods transaction boundary. Classification: permitted composition; no feature-to-feature rule-4 violation identified.** App wires region content and a wharf presentation adapter. The [composition exception](../project-structure.md#5-assembly-definitions-asmdefs--the-dependency-rules-and-the-real-graph) applies. World must still expose reachable storage/support through Core when goods are integrated; these direct references are not a shortcut to inventory ownership.

## Complete runtime inventory

[App asmdef:6](../../../Assets/_Project/Code/App/HiddenHarbours.App.asmdef#L6) declares the edge. There are 4 importing runtime files, 3 distinct referenced target types and 6 explicit type-token sites below. Counts include enums, structs and interfaces where present; method calls through inferred variables are described separately, not counted as new type tokens. Comments and strings are excluded.

| Runtime source | Referenced target types (source lines) |
|---|---|
| [ArrivalOpening.cs:113](../../../Assets/_Project/Code/App/ArrivalOpening.cs#L113) | `RegionSceneLoader` 113 |
| [ArrivalOpening.Sea.cs:22](../../../Assets/_Project/Code/App/ArrivalOpening.Sea.cs#L22) | `RegionSceneLoader` 22 |
| [ArrivalSeaDef.cs:11](../../../Assets/_Project/Code/App/ArrivalSeaDef.cs#L11) | `RegionDef` 11, 12 |
| [NmcWharfBrowSpanVisual.cs:8](../../../Assets/_Project/Code/App/NmcWharfBrowSpanVisual.cs#L8) | `GangwayVisual` 8, 11 |

`App.Editor` is a separate Editor-only assembly with its own declared World edge and 106 import directives to this target. Its scene builders, authoring tools and capture helpers are editor composition; they are excluded from the runtime table and are not shipped feature-to-feature violations.

## Existing composition and future goods seam

`ArrivalOpening` holds a `RegionSceneLoader`, and `ArrivalOpening.Sea.ConfigureSea` accepts it to orchestrate a staged region crossing. `ArrivalSeaDef` points to two authored `RegionDef` assets. `NmcWharfBrowSpanVisual` reads `GangwayVisual` and its brow/landing geometry to fit the picture. These are content/scene/presentation wiring, not goods membership or sale policy. No change to their appearance or authored data is proposed here.

For goods crossing a brow or entering a receiving area, use the [proposed `IGoodsSupport` and `IGoodsTransfer`](../../design/goods-handling/04-first-slice.md) with region/level/parent identity and live access/support. World supplies fixtures; the authority checks reach and supported placement. App only binds those providers. `GoodsLocationChanged` describes a completed parent/location change, not permission to teleport cargo during a scene hop. Existing `RegionTravelCover` is presentation state and must not be treated as a goods-transfer receipt.

## Documentation defect

[ArrivalOpening.cs:82–83](../../../Assets/_Project/Code/App/ArrivalOpening.cs#L82) and [2445–2448](../../../Assets/_Project/Code/App/ArrivalOpening.cs#L2445) say App may not reference World. The same file imports World at line 5, stores `RegionSceneLoader` at line 113, and App's asmdef explicitly references World at line 6. The current architecture explicitly permits composition layers. **Failing review case, static and unexecuted:** a maintainer follows those comments and duplicates a World contract solely to avoid an allegedly forbidden App reference. The premise contradicts the actual allowed graph. Record for later comment correction; no runtime defect is inferred from the comments.

Lexical false positives were excluded: `ArrivalOpening.Route` at line 422 is its own property, and `_mooring.ShoreCleat` at line 1577 is a member accessed as Core `IMooringCleat`, not a use of World's concrete `ShoreCleat` type. Comment-only `World.FloatingPlatform` and `World.OnboardingFlags` mentions are not source dependencies.
