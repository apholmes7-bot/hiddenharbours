# App → Fishing

Audit base: `7276a5c018160fecec7508bff0cbc51a0afbf45c`. Documentation only; static source inspection, no Unity run.

**Priority: later NPC goods integration, after the manual first slice. Classification: permitted composition/presentation.** App combines a Fishing schedule with a Boats hull in `NpcPotBoats`; this is not an additional feature-to-feature exception. The pictured pot sequence is not authoritative catch ownership and must not become that authority when the [ADR 0055](../../adr/0055-goods-are-carried.md) graph is introduced.

## Complete runtime inventory

[App asmdef:9](../../../Assets/_Project/Code/App/HiddenHarbours.App.asmdef#L9) declares the edge. There is one importing runtime file, with 10 distinct referenced target types and 43 explicit type-token sites below. Counts include enums, structs and interfaces where present; method calls through inferred variables are described separately, not counted as new type tokens. Comments and strings are excluded.

| Runtime source | Referenced target types (source lines) |
|---|---|
| [NpcPotBoats.cs:50](../../../Assets/_Project/Code/App/NpcPotBoats.cs#L50) | `NpcPotBoatPose` 215, 270, 294; `NpcPotBoatStage` 272; `NpcPotDay` 117, 180, 195, 196, 221, 306, 307, 527; `NpcPotDayDef` 113, 251, 296, 393, 432, 500, 518; `NpcPotDayLibrary` 50, 68; `NpcPotPhase` 229, 299, 311, 317, 326, 338, 344, 346, 433, 543; `NpcPotState` 221, 294; `PotRopeMath` 210, 212, 303, 305, 329, 334, 353, 356; `TrapHaulMath` 402; `TrapSplashFx` 349 |

`App.Editor` is a separate Editor-only assembly with its own declared Fishing edge and 9 import directives to this target. Its scene builders, authoring tools and capture helpers are editor composition; they are excluded from the runtime table and are not shipped feature-to-feature violations.

## What the edge does

[NpcPotBoats.cs:50,68](../../../Assets/_Project/Code/App/NpcPotBoats.cs#L50) loads a `NpcPotDayLibrary`; `TryBind` at lines 113–147 validates a `NpcPotDayDef` and composes a Boats hull/gear view. `Update` at lines 175–230 builds today/yesterday schedules from world seed, game time and day length, then derives pose and pot phase. The unscaled timer at lines 166–169 schedules view/terrain refresh; it does not seed the day simulation. `DrawPot` at lines 294–374 drives gear visibility and splash presentation. `TrapHaulMath` samples rope geometry at line 402. These are deliberate App-side composition operations, not a feature reaching into a peer's inventory.

## Goods boundary before extending this work

`NpcPotState`/`NpcPotPhase.Aboard` describe an animation/schedule state; they carry no authoritative item/container membership. `Unbind`/`Release` at lines 151–157 and 496–504 destroy the gear view and restore the hull berth. **Future failing case to prevent, not a current reproduced bug:** interpreting an Aboard pose as a landed-catch transaction would create or remove stock again when a view is rebuilt or time is re-read. An EventBus-only inventory would likewise disappear when the scene/view is absent.

Keep any later fish-occupant/harvest authority in its owning simulation and cross the module boundary with stable ids and a Core transaction result. Use the [proposed `IGoodsContainer`, `IGoodsTransfer`, `IGoodsSupport`](../../design/goods-handling/04-first-slice.md) for an actual item route; publish `GoodsMoved` after commit and preserve `CatchLanded` for a genuinely new landing. App receives read-only work/pose results and drives views. Do not expose `NpcPotDay`, `TrapDef`, a sprite, or a presentation phase as a substitute for those contracts. NPC/staff handling beyond the bounded manual slice stays in the ADR's later phase.
