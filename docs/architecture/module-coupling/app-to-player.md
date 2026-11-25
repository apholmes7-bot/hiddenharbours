# App → Player

Audit base: `12bc04b19a2efb33e352b4eea58104f7a5599be1`. Documentation only; static source inspection, no Unity run.

**Goods priority: medium, as a preservation requirement during first-slice integration, not a demonstrated defect. Classification: permitted composition.** App wires player control, input and wallet implementations. The [composition exception](../project-structure.md#5-assembly-definitions-asmdefs--the-dependency-rules-and-the-real-graph) permits these references. Goods ownership and transfer policy should not migrate into these cinematic/travel adapters.

## Complete runtime inventory

[App asmdef:11](../../../Assets/_Project/Code/App/HiddenHarbours.App.asmdef#L11) declares the edge. There are 5 importing runtime files, 6 distinct referenced target types and 13 explicit type-token sites below. Counts include enums, structs and interfaces where present; method calls through inferred variables are described separately, not counted as new type tokens. Comments and strings are excluded.

| Runtime source | Referenced target types (source lines) |
|---|---|
| [ArrivalDeckWalk.cs:199](../../../Assets/_Project/Code/App/ArrivalDeckWalk.cs#L199) | `DeckWalkController` 199, 274 |
| [ArrivalOpening.cs:294](../../../Assets/_Project/Code/App/ArrivalOpening.cs#L294) | `DeckRiderMeshPresenter` 1688; `DeviceDeckIntentSource` 2104; `PlayerWalkController` 294, 710 |
| [GameRoot.cs:37](../../../Assets/_Project/Code/App/GameRoot.cs#L37) | `PlayerWallet` 37 |
| [LandPassageAccess.cs:10](../../../Assets/_Project/Code/App/LandPassageAccess.cs#L10) | `ControlSwitcher` 10, 15 |
| [RegionTravelCoordinator.cs:31](../../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L31) | `ControlSwitcher` 31, 107, 128, 293 |

`App.Editor` is a separate Editor-only assembly with its own declared Player edge and 15 import directives to this target. Its scene builders, authoring tools and capture helpers are editor composition; they are excluded from the runtime table and are not shipped feature-to-feature violations.

## Existing composition

[GameRoot.cs:37,63](../../../Assets/_Project/Code/App/GameRoot.cs#L37) wires `PlayerWallet` into Core `IWallet`. [LandPassageAccess.cs:10–19](../../../Assets/_Project/Code/App/LandPassageAccess.cs#L10) adapts `ControlSwitcher` to Core `IRegionPassageAccess`; World therefore does not depend on Player. Travel's `ApplyArrival` at [RegionTravelCoordinator.cs:128–167](../../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L128) rebinds the dock and reasserts the existing control mode.

`ArrivalOpening` enables/disables the player's walking controller, detects its mesh presenter for a boarding presentation, and lazily creates `DeviceDeckIntentSource` behind Core `IControlIntentSource<DeckIntents>` (lines 2098–2110). `ArrivalDeckWalk` reuses `DeckWalkController`'s pure frame-conversion/seed helpers at lines 199 and 274. These are explicit adapter/presentation choices; no goods authority is present in these calls. If multiple non-composition consumers need the math, promote engine-light frame math to Core with equivalent tests rather than exposing the Player controller. Do not move player input or rendering implementations into Core.

## Goods boundary

For hands and support across control changes or region travel, use the [proposed Core goods contracts](../../design/goods-handling/04-first-slice.md); the actor's hand assignment and parent container/support identity must remain authoritative while presentation changes. Existing `ControlModeChanged` is a control notification, not permission to stow or sell goods; `IRegionPassageAccess.CanTravel` is a travel gate, not a cargo-capacity check. Any later lifecycle migration must assert that an in-hand item keeps one identity/location across a region hop and that failed access changes no goods. This is a future regression requirement, **not an observed defect** in these App references. No runtime checks ran in this audit.
