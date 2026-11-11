# NMC carried counter: measure (PR 2, economy-sim)

The ask: Wendell's counter at Nine Mile Creek sells catch carried to it by hand (owner ruling 11-10, no teleporting). The smallest slice proposed is that a carried tote presented to the counter becomes the `IHold` the sale reads, through Core interfaces only. Wendell's counter would get a stable seller identity that `CounterSellDesk` resolves, and `WendellFirst` a `SellAtSellerId` row. The St Peters counters stay unchanged.

**Verdict: STOP before code.** The slice as written needs three things the brief lists as STOPs:
- a goods-handling decision the record does not hold, and in fact contradicts
- a price-channel choice for a counter that has no sell stack
- a "tote" that does not exist at runtime

Readings follow, then the lean.

## 1. What the record already rules

| Source | What it says | Bearing on the slice |
|---|---|---|
| `backlog/proposed-goods-handling.md`, choice 5 "First sale", **RULED 2026-11-11** | The first sale is **one fish set on the counter** before quote and confirmation. Delivered pails come later, and the empty pail is retained. Parking a carrier never sells its load. | A whole container sold from the hand is the *later* "delivered pail" step, and it skips "set on the counter". |
| ADR 0055 §1 (Proposed, 2026-11-10) | "A counter buys only the selected goods delivered into its receiving area." | A tote still in hand is not in a receiving area, and `SellAll` sells everything rather than selected goods. |
| GH-03 "delivered-only buyer" (depends on GH-02, which depends on GH-01) | A counter receiving-container interface. A clerk quotes and sells only physically delivered *selected ids*. An empty receiving pan sells zero. Remote provider sale access is removed in every production buyer adapter. | The receiving container is the ruled `IHold` for a counter sale, not the carried container. Its Core contract is GH-01's to ratify (lead-architect). |
| `docs/design/goods-handling/01-real-gear.md`, `02-model.md` | Hands → container → receiving pan. Ids persist through every transfer. | Same as above. |

The slice's own wording, "a carried tote presented to the counter is the IHold the sale reads", is therefore a third option. It is neither the ruled first sale (one fish on the counter) nor the ruled later step (a pail delivered into the receiving area). Building it would mean deciding goods handling in code.

## 2. What exists at runtime

- **No tote carriable.** `Data/Characters/HeldProps` holds 13 `BucketTote*` held-mesh assets. The only reference to them is `Data/Art/CatchItemLibrary.asset` (the tote rig's catch-fill art). No component, prefab or scene places a tote the player can lift.
- **The pail is the only carried `IHold`.** `CarriableBucket` (Player) implements `IInteractable, ICarriable, ICarryAnchored, IHandLoad, IHold`. Nine Mile Creek has one: `_defId: container.bucket`, `_id: container.nine_mile_creek.bucket`, `_capacity: 20`, `_reachMeters: 1.5`.
  - Its contents already persist through `SaveData.BucketCatch` (`List<HeldCatchDto>`, migrated in `SaveMigration`), so reusing the pail needs **no save change**.
  - A tote would need a new runtime carriable plus wiring for its held art, which counts as a new asset.
- **`IHold`** (Core/Economy) exposes only `CapacityUnits`, `UsedUnits`, `Items`, `TryAdd` and `Clear`. There is no per-item removal, so a sale through it is all-or-nothing (`FishBuyer.SellAll` → `SellService.SellAll`). "Selected ids" (GH-03) cannot be expressed through `IHold` today.
- **Wendell's counter has no sell stack.**
  - `WendellArsenault` carries a Transform, an `IsoCharacterSprite`, a `YSortSprite` and an `Interactable`. It has no vendor arm, no `WharfSellPoint`, no `FishBuyer` and no `Market`.
  - The creek's only `WharfSellPoint` is on `NmcWharfMarketCounter`. That one has its own `_market` and `_buyer`, and its `_holdProvider` is the boat hold.
- **How `CounterSellDesk` resolves a seller.** It handles `CounterSellRequested(sellerId)` → `CounterFor(sellerId)` → `BuyCatalog.ArmsFor(sellerId)` vendor arms (Gear, Bait, Supply, Instrument, License, Pot, Shipwright) → the `WharfSellPoint` on the same GameObject → `SellOverTheCounter()` → `Sell(_hold, _wallet)`.
  - Its own design note rules out a separate seller-id field ("one authored fact — `_sellerId` — answering both").
  - So a seller identity for Wendell means either a vendor arm on his counter (an offer surface, which needs stock) or a second resolution path (against the desk's recorded design).
- **The dialogue side is ready.** `DialogueOption.SellAtSellerId`, plus `DialoguePresenter.CounterSellAnswer` speaking `ReplyLines` / `NothingToSellLines`, already work for St Peters. `WendellFirst.asset` has no `Options` today.

## 3. What the slice would have to decide

1. **Which container the sale reads.** The brief says the carried tote. The ruling says one fish set on the counter, then delivered pails. **This is the unrecorded decision.**
2. **What Wendell pays.** He has no `Market` or `FishBuyer`. Two options:
   - Wire his counter to the creek's market. He then pays the wharf price.
   - Give him a store channel, the way St Peters' store "pays deliberately less than the creek".

   Either is a price/offer choice.
3. **How Wendell's counter is named to the desk.** A vendor arm (an offer surface) or a new resolution path (a desk design change, economy-sim plus lead-architect).
4. **Tote or pail.** A tote is new runtime content and art wiring. The pail exists and is saved.

## Lean

**Do not build the in-hand tote sale.** Bring the owner one question with a recommended answer:

> At Nine Mile Creek, may Wendell's first sale be the ruled **"delivered pail"** step ahead of GH-03? The player sets the carried pail on his counter, he buys what is in it at the creek's wharf price, and the empty pail stays with the player. Or does the creek wait for the GH-01–03 receiving pan (one fish set on the counter)?

- **If yes**, the slice is small and needs no save change and no new art:
  - Reuse the existing pail, whose contents `BucketCatch` already saves.
  - Pricing goes through `FishBuyer`/`SellService` against the creek's existing `Market`, so no price or tuning change.
  - The counter and the `Sell` seam are wired through the builders.

  It still needs the owner to record "the creek's interim first sale is a delivered pail" in the ruling table. It also needs lead-architect to accept a Core `ICounterReceiving` stand-in (a receiving surface that yields an `IHold`) as the forerunner of GH-01's receiving-container contract. The St Peters counters are untouched.
- **If no**, the creek's carried counter is GH-03 itself, built after GH-01/GH-02. That is a different item with lead-architect, gameplay-systems, world-content (pan site) and art (receiving pan) collaborators, not a one-PR slice.

Nothing in this branch changes code, data, scene, save, price or look. PR 1 (vendor anchors) stands on its own.
