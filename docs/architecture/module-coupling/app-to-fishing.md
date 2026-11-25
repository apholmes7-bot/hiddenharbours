# App → Fishing

Audit base: `12bc04b19a2efb33e352b4eea58104f7a5599be1`. Documentation only; static source inspection, no Unity run.

**Priority: later NPC goods integration, after the manual first slice. Classification: permitted composition/presentation.** App combines a Fishing schedule with a Boats hull in `NpcPotBoats`; this is not an additional feature-to-feature exception. The fleet now has deterministic catch and landing plans, distinct from player inventory and market authority; keep that distinction when the [ADR 0055](../../adr/0055-goods-are-carried.md) graph is introduced.

## Complete runtime inventory

[App asmdef:9](../../../Assets/_Project/Code/App/HiddenHarbours.App.asmdef#L9) declares the edge. There are 3 importing runtime files, with 17 distinct referenced target types and 84 explicit type-token sites below. Counts include enums, structs and interfaces where present; method calls through inferred variables are described separately, not counted as new type tokens. Comments and strings are excluded.

| Runtime source | Referenced target types (source lines) |
|---|---|
| [NpcPotBoats.cs:50](../../../Assets/_Project/Code/App/NpcPotBoats.cs#L50) | `NpcPotBoatPose` 238, 295, 318; `NpcPotBoatStage` 297; `NpcPotDay` 129, 200, 215, 216, 246, 330, 331, 554; `NpcPotDayCatch` 112, 217, 555; `NpcPotDayDef` 125, 276, 320, 417, 456, 526, 544; `NpcPotDayLibrary` 50, 68; `NpcPotLanding` 115, 219, 556; `NpcPotPhase` 254, 323, 335, 341, 350, 362, 368, 370, 457, 573; `NpcPotState` 246, 318; `PotRopeMath` 233, 235, 327, 329, 353, 358, 377, 380; `TrapHaulMath` 426; `TrapSplashFx` 373 |
| [NpcPotDeck.cs:28](../../../Assets/_Project/Code/App/NpcPotDeck.cs#L28) | `NpcDeckMoment` 155; `NpcDeckStep` 51, 119, 165, 175, 181, 191, 200; `NpcLandingMoment` 212; `NpcLandingStep` 217; `NpcPotBoatPose` 112; `NpcPotBoatStage` 125, 130, 131, 136, 142, 150; `NpcPotDay` 112, 185, 240, 241; `NpcPotDayCatch` 112, 297; `NpcPotDayDef` 28, 60; `NpcPotLanding` 113, 297; `TrapDef` 289; `TrapSplashFx` 292 |
| [NpcPotLandingWalk.cs:19](../../../Assets/_Project/Code/App/NpcPotLandingWalk.cs#L19) | `NpcLandingMoment` 99; `NpcLandingStep` 37, 112; `NpcPotDayDef` 19, 44; `NpcPotLanding` 96 |

`App.Editor` is a separate Editor-only assembly with its own declared Fishing edge and 9 import directives to this target. Its scene builders, authoring tools and capture helpers are editor composition; they are excluded from the runtime table and are not shipped feature-to-feature violations.

## What the edge does now

[NpcPotBoats.cs](../../../Assets/_Project/Code/App/NpcPotBoats.cs) loads the day library and composes the hull, `NpcPotDeck` and `NpcPotLandingWalk`. `Tick` builds today/yesterday schedules from the existing Core clock, world seed and day length. It now also builds `NpcPotDayCatch` using season and the same water-level function, then `NpcPotLanding` from that catch. The unscaled timer throttles presentation/terrain refresh, not simulation randomness. Its three Fishing-importing files and current type sites are inventoried above; the old one-file inventory is superseded.

[NpcPotDeck.cs](../../../Assets/_Project/Code/App/NpcPotDeck.cs) reads catch work and grade-crate occupancy for sorting, banding and stowing. [NpcPotLandingWalk.cs](../../../Assets/_Project/Code/App/NpcPotLandingWalk.cs) reads `MomentAt` and `AtCounterAt` to draw the crate carried to the buyer and crates set down there. [NpcPotLanding.cs](../../../Assets/_Project/Code/Fishing/NpcPotLanding.cs) derives lift, carry, set-down, weighing and return from the immutable day plan. It moves no money and touches no market. These are permitted App composition operations.

## Remaining goods boundary

The earlier statement that this is only a pot pose with no catch/landing model is obsolete. `NpcPotDayCatch` now owns deterministic animals/work/grade results for this fleet plan, and `NpcPotLanding` owns its clock-indexed crate locations. Neither is the player's `IHold`, a persisted goods transaction, or an Economy sale. `IsSoldAt` describes the plan's completed weighing phase; it does not pay a wallet.

**Future failing case to prevent, static and unexecuted:** crediting a buyer whenever a presenter observes `IsSoldAt` would repeat payment on a clock reread or view rebuild. Preserve the current pure plan and its `NpcPotDayCatchTests` / `NpcPotLandingTests` coverage; a later economic integration needs stable delivery identity and an exactly-once Core transaction result. The [proposed goods contracts](../../design/goods-handling/04-first-slice.md) remain a wider handoff, not a reason to duplicate or remove the shipped fleet model. No NPC view, art, schedule tuning, asset, save or executable changed in this refresh.
