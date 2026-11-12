# App → Boats

Audit base: `7276a5c018160fecec7508bff0cbc51a0afbf45c`. Documentation only; static source inspection, no Unity run.

**Priority: first carried-goods slice. Classification: permitted composition containing a goods-authority blocker.** App is explicitly a composition layer under [project structure §5](../project-structure.md#5-assembly-definitions-asmdefs--the-dependency-rules-and-the-real-graph). Constructing or adapting boat components is allowed; the global hold binding below remains incompatible with the delivery rule in [ADR 0055](../../adr/0055-goods-are-carried.md). Removing the assembly reference alone would not fix that rule.

## Complete runtime inventory

[App asmdef:8](../../../Assets/_Project/Code/App/HiddenHarbours.App.asmdef#L8) declares the edge. There are 7 importing runtime files, 21 distinct referenced target types and 99 explicit type-token sites below. Counts include enums, structs and interfaces where present; method calls through inferred variables are described separately, not counted as new type tokens. Comments and strings are excluded.

| Runtime source | Referenced target types (source lines) |
|---|---|
| [ArrivalCabinWalk.cs:51](../../../Assets/_Project/Code/App/ArrivalCabinWalk.cs#L51) | `BoatCabinDoor` 52, 69, 94, 167; `BoatCabinWalkMath` 84, 240, 245, 246, 283, 286, 305, 326; `BoatController` 155; `BoatCutaway` 53, 69, 98, 195; `BoatInterior` 51, 69, 91, 163; `BoatInteriorEntryPolicy` 165; `BoatInteriorInstaller` 159, 197, 198; `BoatVisualDef` 193 |
| [ArrivalDeckWalk.cs:97](../../../Assets/_Project/Code/App/ArrivalDeckWalk.cs#L97) | `BoatCabinDoor` 188, 294, 315; `BoatCabinThreshold` 319; `BoatCabinWalkMath` 322; `BoatDeckAreas` 97; `BoatDeckDef` 97, 105, 190, 245, 270, 296, 315; `DeckAreaMath` 226, 326 |
| [ArrivalOpening.cs:111](../../../Assets/_Project/Code/App/ArrivalOpening.cs#L111) | `BoatCabinDoor` 436, 472, 508, 2254, 2265, 2276, 2301; `BoatCabinWalkMath` 1150; `BoatController` 291, 415, 739; `BoatCutaway` 440, 1252; `BoatDeckAreas` 1206, 2510; `BoatDeckDef` 1206, 2510, 2516, 2536; `BoatHullDef` 1920; `BoatHullPresenterHost` 296, 2121, 2140; `BoatInterior` 433; `BoatInteriorInstaller` 1152, 1153, 1238; `BoatMooring` 310, 770, 771; `BoatOwnerDef` 111, 533; `BoatVisualDef` 1227; `DeckAreaMath` 1154, 2143, 2492; `HullTideRide` 741; `IBoatHullPresenter` 2123, 2142; `MooredBoat` 324, 789, 1171, 1173, 1469, 2010 |
| [NpcPotBoats.cs:113](../../../Assets/_Project/Code/App/NpcPotBoats.cs#L113) | `BuoyWaveVisual` 443, 539; `MooredBoat` 113, 248, 256, 259, 519 |
| [PersistentHoldProxy.cs:29](../../../Assets/_Project/Code/App/PersistentHoldProxy.cs#L29) | `ShipHold` 29 |
| [PilotageHelm.cs:78](../../../Assets/_Project/Code/App/PilotageHelm.cs#L78) | `BoatController` 78, 81 |
| [RegionTravelCoordinator.cs:33](../../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L33) | `BoatController` 261; `ShipHold` 33, 293 |

`App.Editor` is a separate Editor-only assembly with its own declared Boats edge and 15 import directives to this target. Its scene builders, authoring tools and capture helpers are editor composition; they are excluded from the runtime table and are not shipped feature-to-feature violations.

## The offending responsibility and its replacement

[PersistentHoldProxy.cs:29](../../../Assets/_Project/Code/App/PersistentHoldProxy.cs#L29) resolves any scene `ShipHold` when no provider was bound. Its `Items`, `TryAdd` and `Clear` forward at lines 33–41. [RegionTravelCoordinator.cs:284–289](../../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L284) binds **every** destination-region proxy to its single persistent hold, declared at line 33. This correctly preserves one old hold across travel, but a counter proxy therefore grants remote mutation of boat stock. Core's existing `IHold` is a contract, yet it lacks actor, source identity, reach, support and revision checks: routing through Core is necessary but not sufficient for ADR 0055.

Use the already [proposed first-slice contracts](../../design/goods-handling/04-first-slice.md): bind each physical source/receiving area to its own `IGoodsContainer` identity and `IGoodsSupport`; route requested movement through `IGoodsTransfer.TryMove`. Give a buyer only `IDeliveredGoodsSale` for its delivered selection. App may compose these providers, but must not manufacture a global fallback or grant the buyer `Clear` on boat stock. Keep old `IHold` readers only for explicitly bounded compatibility; do not let a compatibility proxy reintroduce remote sale. Publish `GoodsMoved`/`GoodsLocationChanged` after authoritative commits, never as the inventory store. Stable ids, location persistence and migration remain the architect's separate ADR 0055 handoff.

**Static failing case, not executed:** configure a destination `WharfSellPoint` whose `_holdProvider` carries a `PersistentHoldProxy`, with coordinator hold, wallet and buyer wired. Leave a catch in the persistent boat hold, travel to that region, and offer the counter no delivered goods. Coordinator binding makes the counter's proxy report that catch. [WharfSellPoint.cs:49,65–73](../../../Assets/_Project/Code/Economy/WharfSellPoint.cs#L49) resolves that `IHold` and opens selling against it; its explicit sale seam delegates the same hold to the buyer at lines 81–97. This is a source-derived fixture, not verification of current scene wiring. ADR 0055 expects no sale until a physical delivery. Pin this future regression with distinct boat/counter container ids and assert zero removal/payment for the undelivered source, then one removal/payment for a committed delivery. Do not weaken existing sale assertions before migrating their setup to the delivered-goods rule.

## Composition that should stay bounded

`ArrivalCabinWalk` and `ArrivalDeckWalk` use measured cabin/deck geometry and traversal math; `ArrivalOpening` builds and steers an opening boat, uses mooring/presenter adapters and places its passenger; `HelmedBoat` delegates control to `BoatController`; travel stops the arriving boat. `NpcPotBoats` binds `MooredBoat`, adjusts its way/pose, and composes buoy presentation. None of these references alone demonstrates lost or duplicated goods. `IBoatHullPresenter` is an interface owned by Boats, included in the inventory rather than mislabeled a concrete class.

For goods access, consume the proposed Core `IGoodsSupport` snapshot in the live hull's local frame. Existing Core `IHullPresence`, `IHullTideRide`, `IMooringCleat` and `IStandableSurface` already separate relevant reads; they do not yet authorize a container transfer. Moving all boat rendering types into Core would expand the wrong boundary and risks visible changes. Leave that presentation intact in this documentation item.
