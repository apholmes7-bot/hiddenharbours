# Goods handling: what main does today

Audit base: `3e7188a426c00dde0b18f94ab9512ea1a2bc0c21` (2026-11-10). This is a source trace, not a playtest. Line numbers below refer to that base. The remaining files in this directory are proposals; they do not describe shipped behaviour.

## Where a catch goes

| Catch | Source → destination today | What still bypasses carrying |
|---|---|---|
| Rod fish, on foot | `FishingController` creates a weighted `CatchItem` with a landing freshness stamp, then attempts `ICatchHands.TryPutInHand`; `CarryHands` creates a `CarriableCatch`. Giving it to a pail moves the same item and publishes `FishCaught`. | If the hand path is disabled **or refuses**, the controller tries its configured `IHold`. A full hand can therefore fall through into storage. At sea the normal path adds directly to the hold. [FishingController:728–761](../../../Assets/_Project/Code/Fishing/FishingController.cs#L728), [CarryHands:210–287](../../../Assets/_Project/Code/Player/CarryHands.cs#L210). |
| Dug clam | `ClamDig` checks shovel, then hands; a catch already held refuses before the bucket check. It rolls a clam, stamps freshness, and normally puts it in the hand. E on the pail stacks it. | The no-hands/disabled-hand path adds directly to the configured bucket. Even after a failed `TryLandInHand`, the final fallback attempts bucket insertion. [ClamDig:220–289](../../../Assets/_Project/Code/Fishing/ClamDig.cs#L220), [ClamBucket:123–216](../../../Assets/_Project/Code/Player/ClamBucket.cs#L123). |
| Trapped lobster, direct haul | A placed trap resolves deterministic animals; `TryHaul` preflights count-based room and adds each directly to an `IHold`, stamped at haul. The service then removes the trap and its saved placement. | All animals enter the hold without an intervening hand/container trip. The raw trap component's hauled flag is session-only; the normal service removal is the persistent safeguard. [PlacedTrap:180–234](../../../Assets/_Project/Code/Fishing/PlacedTrap.cs#L180), [PlacedTrapService:251–275](../../../Assets/_Project/Code/Fishing/PlacedTrapService.cs#L251). |
| Trapped lobster, deck work | The pot holds per-animal state. A successful grab sorts a keeper onto deck; short/berried animals return. Banding adds the keeper directly to the supplied hold. | Banding includes stowing in one operation. `AutoResolve` also adds remaining keepers directly on lifecycle exits; no-room animals are counted separately. These are alternate paths, not two hauls of one pot. [DeckPot:225–270](../../../Assets/_Project/Code/Fishing/DeckPot.cs#L225), [DeckPot:289–316](../../../Assets/_Project/Code/Fishing/DeckPot.cs#L289), [PotDeckWorkController:266–268,344–357](../../../Assets/_Project/Code/Fishing/PotDeckWorkController.cs#L344). |

`CatchLanded` means a catch reached the hand; `FishCaught` currently means it entered a hold. Those meanings matter to onboarding and fill refresh. Moving an old fish between containers must not count as catching another fish.

## What each storage actually is

| Place | Runtime truth, capacity and access | Persistence today |
|---|---|---|
| Hands | Two `HandSlots`: one tool, one container or one catch; different kinds may share, same kinds may not. A long fish occupies both hands. `CarryHands` can temporarily sling a tool during landing. A loose catch deliberately has no floor-drop interactable. | No hand-catch DTO or save capture in these classes. Do not promise reload safety for a fish still in hand. [HandSlots:110–161](../../../Assets/_Project/Code/Core/Interaction/HandSlots.cs#L110), [CarryHands:174–254](../../../Assets/_Project/Code/Player/CarryHands.cs#L174), [CarriableCatch:30–34](../../../Assets/_Project/Code/Player/CarriableCatch.cs#L30). |
| Player/belt pail | `ClamBucket` is a player-mounted `IHold`, default 20 units, gear ownership gated; it is always in reach. A physically held container suppresses its stow candidate. This is not the separate world pail. | `BucketCatch`, captured on add/clear and restored on `GameLoaded`. [ClamBucket:25–113,173–186](../../../Assets/_Project/Code/Player/ClamBucket.cs#L25). |
| World pail | `CarriableBucket` owns another count-based list (20 default), can be picked up, filled and set down; moving its GameObject carries its list. It is always considered liftable regardless of contents mass. | Its `TryAdd`/`Clear` do not capture save state; no world-position/content restoration contract here. [CarriableBucket:40–106](../../../Assets/_Project/Code/Player/CarriableBucket.cs#L40), [CarryHands:345–416](../../../Assets/_Project/Code/Player/CarryHands.cs#L345). |
| Boat hold | `ShipHold` counts animals, capacity from `BoatHullDef.HoldUnits`. It has `TryAdd`, `Clear`, and surgical `ReplaceAll`, but no individual take verb. A deck tray/tote is its presenter, not another inventory. | One global `BoatHoldCatch` list, not a list keyed by boat instance. `OwnedFleet` swaps the hull/capacity of the same aggregate, retaining catch. [ShipHold:41–114](../../../Assets/_Project/Code/Boats/ShipHold.cs#L41), [OwnedFleet:152–165](../../../Assets/_Project/Code/Boats/OwnedFleet.cs#L152). |
| Freezer | `GinnyFreezer` is fixed `IHold`. E exchanges with `ClamBucket`: if bucket nonempty, deposit every item that fits; otherwise withdraw what fits. Deposit settles then freezes; withdraw settles then returns to Ambient. Neither path requires lifting one fish. | `FreezerCatch`, on mutations. [GinnyFreezer:46–98,109–156](../../../Assets/_Project/Code/Player/GinnyFreezer.cs#L46). |
| Truck/ATV | Existing road vehicles have geometry, driving, doors, towing and authored rack/bed anchors. No catch inventory or manual load/unload API was found in `Vehicles`. A bed anchor is not an implemented container. | Vehicle persistence does not establish cargo ownership. See [vehicles proposal](05-vehicles.md) for exact rig/runtime sources. |
| Counter | A `WharfSellPoint` resolves a wired `IHold`; it does not own a receiving tray. `CounterSellDesk` resolves the seller's counter and calls that sell point. | Only the existing source hold and economic state change; there is no saved counter consignment. [CounterSellDesk:60–99](../../../Assets/_Project/Code/Economy/CounterSellDesk.cs#L60). |

## What the sale reads

`WharfSellPoint.Awake` resolves `_holdProvider` to `IHold`; `SellOverTheCounter` passes that same hold to `FishBuyer.SellAll`. The screen path also opens against the wired hold. There is no proof at this layer that the goods were carried to the seller. [WharfSellPoint:49–73,81–121](../../../Assets/_Project/Code/Economy/WharfSellPoint.cs#L49).

`SellService` walks that hold's items in order, rejects spoiled units, applies marginal category supply pricing, rebuilds the source with unsold items, pays once and publishes `CatchSold`. Individual-species sale does the same kind of source rebuild. The transaction has no physical receiving point. [SellService:111–153,165–209](../../../Assets/_Project/Code/Economy/SellService.cs#L111).

The region wiring shim can expose the persistent ship hold directly: [PersistentHoldProxy:25–41](../../../Assets/_Project/Code/App/PersistentHoldProxy.cs#L25). The Nine Mile Creek builder configures that proxy as the sell provider [NineMileCreekBuilder:788–810](../../../Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs#L788). This is a source/wiring risk, not a claim that every seller currently sells a berthed boat: caller wiring determines the hold. Proximity to a seller does not prove proximity to, or delivery of, its source cargo.

## Save and freshness limits

`HeldCatchDto` saves species id, quantity and the freshness triple `(SpoilAccrued, LastSettleGameSeconds, Mode)`, separately for hold, belt bucket and freezer. It does **not** save a catch instance id, rolled weight, dimensions, hand occupancy, portable-container identity or support surface. The codec groups consecutive equal species/freshness records; restore resolves a species template and expands quantity. The factory uses the species' **midpoint weight**, so a rolled fish's exact size is not round-tripped. [SaveData:96–109,606–636](../../../Assets/_Project/Code/Core/Save/SaveData.cs#L606), [HoldCatchCodec:19–77](../../../Assets/_Project/Code/Core/Save/HoldCatchCodec.cs#L19), [SpeciesCatchItemFactory:18–28](../../../Assets/_Project/Code/Fishing/SpeciesCatchItemFactory.cs#L18).

Freshness already uses settle-on-read game time, not real-world timers. Mode changes settle the old interval first. Ambient spoils; Live/Frozen/Iced have zero default spoil multipliers, and buyers refuse at the default 0.9 accrued spoil. Ice ends at a computed protection boundary. These are current policy defaults, not new balance choices. [Freshness:3–69,140–205](../../../Assets/_Project/Code/Core/Economy/Freshness.cs#L3). Remaining ice and lid state are explicitly **not saved** in the deck ice implementation [DeckIceBox:32–36](../../../Assets/_Project/Code/Boats/DeckIceBox.cs#L32).

## Complete bypass register for the traced catch/storage paths

1. Rod at sea → hold; failed on-foot hand landing → configured hold.
2. Clam no-hands/disabled or failed-hand fallback → bucket.
3. Direct trap haul → whole catch into hold; deck banding → hold; lifecycle auto-resolution → hold.
4. Freezer ↔ belt bucket, batch exchange at a press.
5. Sale → deletion from whichever hold the counter/screen was wired to, without a delivery stage.
6. `CatchDumpInput` X enumerates **all scene IHold components** and removes spoiled catch without carrying or per-container reach; `CatchDisposal` rebuilds each with its keepers. [CatchDumpInput:23–52](../../../Assets/_Project/Code/Player/CatchDumpInput.cs#L23), [CatchDisposal:23–41](../../../Assets/_Project/Code/Core/Economy/CatchDisposal.cs#L23).
7. Changing active hull retains the aggregate's catch; it is not a cargo transfer between independently saved boats.
8. Save restore rebuilds inventories (legitimate reconstruction, not a gameplay delivery), currently with midpoint-weight substitution. Re-mode operations such as wetting a bucket rebuild the same contents; those are preservation-state changes, not extra catch. [WetBucketPoint:120–141](../../../Assets/_Project/Code/Player/WetBucketPoint.cs#L120).

Scope: catch, clam, lobster, their containers, sale and disposal. Bait stock purchasing/consumption is a separate abstract supply ledger; converting it to carried bait is proposed M2 work, not claimed here as implemented transport.

## Existing physical inventory and held-prop work to preserve

- [Diegetic inventory §4](../diegetic-ui-and-inventory.md#4-physical-limited-inventory): two hands, size grid, constrained visible nesting and world placement. Its §6 snapshot predates parts of main; the source trace above takes precedence for implementation facts.
- [Boats and navigation](../boats-and-navigation.md): deck work, holds, physical boarding and the deck container as a read-only view of `ShipHold` (§11 catch container). [Deck boarding/interact capture](../deck-boarding-cleats-and-interact-capture.md) governs access; a remote hold menu cannot bypass it.
- [Economy and business §2–3](../economy-and-business.md#2-storage): perishability and storage progression. Its general storage multipliers differ from M1 defaults; keep the current `Freshness` policy until a separately owned balance change.
- [Fleet deck occupancy](../fleet-deck-occupancy.md): authored deck geometry and obstruction concerns. [Fuel and refuelling](../fuel-and-refuelling.md): another physical carrier precedent, not authority to treat fuel and fish alike.
- `CarryAnchorTableDef`, `ICarryAnchored`, `CharacterCarryPoseDef`, `HandSlots`, `CarryHands`, `CarriableCatch`, `CarriableBucket`, `BucketFillPresenter`, `CatchFillRenderer`, `CatchStorageAnchors` and `DeckContainerPresenter` already separate items, grip facts and fill presentation.
- [ADR 0053](../../adr/0053-held-props-are-facet-meshes-on-the-figures-sockets.md): bake data exists for held facet meshes; runtime mounting is a staged rollout. This proposal must not restore sprite-only held props or assume that rollout is finished. Visible storage contents remain the requested sprites; presentation assets and look decisions belong to the art roles.
- Active PR 1033 owns catch-honesty code; PR 1039 owns the wharf scene/builders; PR 1036 owns ground blends. This documentation changes none of their files. Implementers must re-trace at their landing base.
