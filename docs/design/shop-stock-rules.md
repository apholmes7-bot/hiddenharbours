# Pilot shop stock: data and pure rules

Slice 2 of the indoor shops plan. This serves P3's delivery rhythm and P5's gentle essentials
guarantee. The model is dormant: nothing constructs the service in the running game, and the wares
book, prices, ownership, scenes, fixtures and save format are unchanged.

`ShopDef` owns one `ShopStockSpec` per shop asset. The pilot is `shop.st_peters_general`, belonging
to `seller.leblancs`, with the future premises key `premises.st_peters_general`. Shelf lines refer
to existing catalog listing ids. There is no copied price or new offer. The licence and fitted
sounder remain counter-only; unlisted shellfish tools are not introduced.

| Line | Existing listing | Objects / maximum | Units per object | Delivery | Floor |
|---|---|---|---|---|---|
| `line.rod` | `gear.rod` | 4 | 1 | Monday and Thursday, 4 objects | 0 |
| `line.capelin` | `bait.capelin` | 20 | 10 | Monday and Thursday, 20 objects | 1 |
| `line.ice` | `supply.ice` | 20 | 1 | Monday and Thursday, 20 objects | 1 |

These capacities and delivery amounts are initial data for the dormant pilot, not a rebalance of
the live book. One bait object matches the existing purchase lot; its price remains the listing's
unit price times that lot size. Rod and ice objects each match one existing purchase.

`Shelves` declares logical fixture-instance ids and slot capacities, with no position, footprint,
sprite, collider or art key. Each stock line reserves `[FirstSlot, FirstSlot + MaxOnShelf)` on one
fixture. Validation rejects unknown fixtures, overlapping ranges, duplicate lines/listings, missing
seller listings, invalid bounds and mismatched purchase pack sizes. Slice 3 must bind these 44 slots
to physical fixture anchors and validate that mapping; this slice does not claim those anchors exist.
The credit block is disabled and empty; it has no behaviour.

## Runtime contract

`IShopStockView` and `IShopStock` live in Core. Economy's `ShopStockService` snapshots validated
specifications and takes a seed, `IGameClock` and `IShopStockStateStore`. It has no MonoBehaviour,
automatic registration, Resources sweep or frame update. Later composition supplies the service
and calls `SlowTick` from the day-start/slow tick. Reads and stock operations also settle first.

The day index is the existing absolute `IGameClock.DayIndex`: day zero is Monday. Delivery occurs
at day start. No restock-hour field is added to the shared config in this slice. Weekday schedules
have no jitter, so different seeds deliberately share the same delivery days. Counts depend on the
same purchase history or restored counts as well as seed and time.

`ShopRestock` counts deliveries in `(settledThroughDay, today]` using full weeks and at most seven
weekday checks, then clamps to the authored maximum. No loop walks elapsed days. Repeating a read
or slow tick cannot deliver twice. Rewinding the clock requires constructing a new service with
the restored state; a future watermark is rejected rather than silently duplicating deliveries.
Slow ticks allocate no model memory after setup; notifications and caller-owned export collections
have their own costs.

**The floor is takeable.** Bait and ice immediately replenish their authored minimum when a committed
take would consume it. A floor of one is not a reserved last object that refuses a buyer. This is an
explicit essentials guarantee, not a simulation of physical delivery. A zero-floor line can sell out.
Put-back refuses a full line and never exceeds capacity. `ShopStockChanged` reports actual count
changes through Core; taking at the floor succeeds without a count-change notification.

**These operations are committed stock changes, not unpaid pickup.** Slice 4 must maintain transient
hand reservations separately, so taking an unpaid object does not decrement the persisted count.
At payment, the till commits stock together with the existing purchase rules. Put-back and leaving
unpaid cancel the reservation. This service does not charge money, grant ownership, carry goods,
or implement transaction/host validation; those belong to the later slices.

## Slice 7 persistence handoff

There is no save-format change. `MemoryShopStockStateStore` is the only implementation here.
The charter's delivery-day rule is implemented in memory now; unlike a provisional always-full-at-dawn
model, a nonessential shelf can stay empty between delivery days. Reloading the current game does
not preserve this dormant state. Later slices must not connect it as persistent gameplay before
the save migration lands.

Slice 7 must persist a sparse list of **`shopId`, `lineId`, `count`, `settledThroughDay`**. Count is in
shelf objects (bait tubs, not individual baits); the watermark is the absolute day already settled.
Only below-full lines have records. An absent record means full at the current time. Before export,
settle at the save's game time, then use the state store's `CopyTo` seam. No schedule, full line,
unpaid reservation, derived weekday, price or credit balance is saved. World seed and game time
remain the existing save fields.

The save owner adds the version bump, migration and adapter. Old saves provide an empty list;
newly constructed services settle missed deliveries on load. Restore the clock before constructing
the service. Restore valid records through the store interface, then compose the service; do not
mutate that backing store behind a running service. Migration must decide retired/unknown ids and
duplicate records explicitly. Counts above a revised capacity clamp down and essentials below their
floor repair on settlement. Buying, saving and loading must not reset a below-full shelf or replay
its settled deliveries. Persist only committed sales, never objects merely held unpaid.

## Proof and limits

`ShopStockRulesTests` exercises bounds, the takeable floor, delivery dates, repeated slow ticks,
read-before-tick ordering, deterministic replay, ten-day catch-up, all 127 weekday combinations,
very long absences, sparse restore, rewind refusal, shop isolation and content-rule rejection.
`ShopStockContentTests` imports the actual pilot in Unity and validates it against the real catalog.
The new Def type and pilot identity are separate additions; the frozen identity ledger is untouched.

Offline proof compiles the full Core and Economy source assemblies and both new test classes against
the project's own Unity and NUnit references, and executes the engine-free tests with NUnit. Asset
text checks supplement that proof; they do not replace Unity import, identity inventory or the
content tests in CI. No Unity editor or player was launched for this slice.
