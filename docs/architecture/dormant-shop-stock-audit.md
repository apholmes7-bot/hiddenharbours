# Retired shop stock path and the live shelves

The old shop stock service was constructed only by tests at the audited main
revision. It had no running-game reader, component, registration or save adapter.
This change removes that service and its private arithmetic/state contract. The
island shelves already run in the game: their host builds them, the counter reads
and spends their counts, and the shelf directory supplies other modules. The
retirement therefore changes no live stock, price or purchase route.

This document supersedes PR 1485's audit. Its claim that the newer shelves were
uncomposed described an older revision and is no longer true. PR 1491 owns the
pending shelf purchase and paper-bag work; none of its files is changed here.

This retirement also supersedes #1506's changes to the unused restock service.
Its useful live-model coverage is retained here: resident use together with player
purchases, an emptied shelf waiting through blocked runs, and exact sold-out and
restocked notification counts from `IslandShelvesHost.Recheck`. The separate
last-item example is already covered for both lines by the stricter parameterized
depletion test below. No test that references the retired service is copied back.

## Reachability proof at the pinned main

Revision: `541276cbc9308ba60ded8b905308366306438570`. These are source observations,
not an executed player trace. Searches covered C# and serialized asset, scene,
prefab and JSON text; the full PR 1491 diff was also searched for the retired
symbols. The reference set contained only the four retired implementation files
and `Assets/Tests/EditMode/Economy/ShopStockRulesTests.cs`.

| Before deletion | Evidence in that revision |
|---|---|
| Construction | `ShopStockRulesTests.cs:43–45` test factory and `:223` isolation test are the only `new ShopStockService` sites. |
| Service implementation | `Economy/ShopStockService.cs:12,24–31` is a plain class; its default event publisher is reached only by constructing it. |
| Stock reads and changes | `ShopStockService.cs:52–100` calls only its private state store and restock functions. Every external call is in `ShopStockRulesTests`. |
| State implementation | `MemoryShopStockStateStore.cs:8–30` is the sole implementation of the old state interface. No serializer or production constructor exists. |
| Core contracts and events | `Core/Economy/IShopStock.cs:6–57` declares the view, mutations, state record and notification. No production subscriber or registration was found. |
| PR 1491 | Zero occurrences of `ShopStockService`, `IShopStock`, `ShopRestock`, `MemoryShopStockStateStore`, `ShopStockRecord` or `ShopStockChanged` in its inspected diff. Its purchase path uses the island shelves. |

The four implementation files and their metadata are removed. The old service's
behaviour tests are retired or migrated as listed below. A build still must verify
there is no reference missed by static search; no Unity run was performed here.

## What runs now

| Stage | Main source and responsibility |
|---|---|
| Automatic composition | [IslandShelvesHost](../../Assets/_Project/Code/Economy/Island/IslandShelvesHost.cs): `Bootstrap` at line 50 creates the persistent host after scene load; `Update` at 83 waits for the clock and calls `TryBuild`. |
| Authored shelf loading | The same host, lines 135–175: loads `Resources/IslandSupply/Shelves`, builds each store, registers it in `IslandShelvesDirectory`, and binds `IslandCounterShelves`. `Build` at 179–190 supplies the tide-driven run calendar. |
| Price-list availability | [BuyCatalog](../../Assets/_Project/Code/Economy/BuyCatalog.cs): line 211 resolves a listing's shelf binding; line 351 takes its shelf item on successful purchase. Unbound listings are not counted by this mechanism. |
| Counter-to-line mapping | [IslandCounterShelves](../../Assets/_Project/Code/Economy/Island/IslandCounterShelves.cs): `Bind` consumes `CounterListingIds`, keyed by seller and listing. It never reads the old service. |
| Counts and draws | [IslandStoreShelves](../../Assets/_Project/Code/Economy/Island/IslandStoreShelves.cs): `CountAt`, `TryTakeOne` and `SlowTick` combine a bounded ledger replay with intraday use and session purchase draws. |
| Background supply | [IslandStoreLedger](../../Assets/_Project/Code/Economy/Island/IslandStoreLedger.cs): target stock, route capacity, cadence and the tide predicate determine each run. It has no old weekday-mask or instant-floor dependency. |

The shipped shelf asset has grocery, ice, bait and gas lines. Grocery listings
currently share one bulk line; PR 1491 owns their separate sale units and the
physical bag transaction. This retirement does not claim that pending change is
already shipped. A shelf count is an accounting read, not proof of physical
transport. Goods still need to be loaded, carried and unloaded by hand.

## Rules retained, migrated and replaced

The existing `IslandStoreShelvesTests` and `IslandStoreLedgerTests` were read
before adding coverage. Applicable assertions move to those models; no parallel
stock simulator is introduced. The following table accounts for every old runtime
test group. Spec-validation tests remain in `ShopStockRulesTests` with their
assertions unchanged because the historical Def and its data still exist.

| Old test or rule | Current assertion or explicit replacement |
|---|---|
| Last item and empty/full bounds | `TakingTheLastItemSellsOutOnlyThatLine_AndRepeatedTakesCannotCrossZero` counts down exactly 4 and 18 items, asserts zero, refuses ten further takes, and pins the other line unchanged. Existing count tests keep maximum bounds. |
| Infinite takeable bait/ice floor; floor repaired on restore | Replaced by the current finite-goods rule. `EssentialBaitAndIceAlsoRunOut_ThereIsNoInstantFloorRefill` exhausts both four-item lines, asserts zero today and tomorrow, then exactly four at the next run. No instant or load-time floor is retained. |
| Thursday delivery, repeated ticks, read-before-tick | The existing tide/cadence calendar replaces weekday masks. `ReadingBeforeTheSlowTickSettlesOnlyOnce_AndDoesNotRepeatADelivery` asserts 4 → 3 after a purchase, still 3 after ten ticks, 2 tomorrow and 4 at the next run. The old unused notification type is retired; the existing host continues to publish its own Core change signal. |
| Identical seed/time/purchases | `TheSameCalendarTimeAndPurchasesGiveTheSameStock_WithExtraPolling` compares both line counts, purchase results and draw counts over 60 days. The shelf model takes a deterministic calendar predicate; it has no random stream to seed. |
| Ten-day catch-up and all weekday combinations | Existing `TheSameMomentGivesTheSameShelves_InAnyReadOrder_AndWhetherOrNotEarlierDaysWereWatched` and the ledger's calendar and replay-convergence tests pin arbitrary read order and bounded catch-up under the new cadence/tide rules. Weekday-mask arithmetic has no runtime successor. |
| Extremely long absence and integer overflow | Bounded replay replaces elapsed-day multiplication. `TheMaximumDayFinishesWithinTheLookback_WithoutIntegerWraparound` preserves the exact `int.MaxValue` boundary, pins 44 calendar queries and dawn/dusk levels of 60/40. Migrating this assertion exposed a loop increment overflow; the ledger now stops before incrementing the final day. |
| Sparse full-stock records, watermark restore, invalid records | Retired with the unused in-memory record format. Current session draws are bounded by `HeldPurchasesStayBounded_ByTheLedgersLookback`; persistence is not implemented or claimed. No saved field is added. |
| Clock rewind refusal and future watermark refusal | Replaced by the existing any-order query rule: an earlier read recomputes the earlier day, and a purchase does not rewrite the past (`ThePlayersPurchaseComesOffTheShelf_LastsTheDay_AndTheNextFullRunClearsIt`). There is no restore watermark in this model. |
| Independent shops and unknown ids | `SameLineIdInDifferentShopsHasIndependentCounts` pins 3 versus 4 and rejects an unknown line. Existing bad-ask tests also remain. |
| Snapshot of authored spec | `AuthoredShelfLineMutationsCannotChangeRunningCountsOrIdentity` changes source line ids, units and capacity after construction and pins the original 4 → 3 counts. The model still reads good assets as before; this is not a claim that it clones every asset. |
| Warm settlement allocates nothing | `WarmSlowTicksAndReadsAllocateNoMemory_AfterARecordedPurchase` preserves the exact zero-allocation assertion and checks the final count is four. |
| Resident and player consumption together | `ResidentUseAndPlayerPurchasesConsumeTheSameRemainingStock` preserves the two morning purchases, exact midday gas and grocery counts, final refusal, and next-run refill from #1506. |
| Empty shelves during blocked runs | `SoldOutStockWaitsForAnAllowedRunAndRefillsOnlyToItsShelf` preserves zero and rejected purchases through days 1–3, then exact gas/grocery maxima on day 4 despite repeated settlement. |
| Live sold-out and restocked notifications | `HostPublishesSoldOutAndBackInStockOnceDespiteRepeatedPolling` exhausts 16 bags, drives the real host, and requires exactly one zero-count event and one 16-count refill event with matching shop and line ids. |
| Committed put-back | The current Core shelf contract has no return operation. The old return API is retired, not silently mapped to a purchase. Unpaid bag returns and till rollback belong to PR 1491; no physical return flow is introduced here. |

## Data deliberately retained

`ShopDef`, `ShopStockSpec`, its nested authoring types and
[StPetersGeneral.asset](../../Assets/_Project/Data/Shops/StPetersGeneral.asset)
remain. The stable `shop.st_peters_general` identity and GUID stay intact.
`IslandShelvesShippedTests:53–64` resolves that shop and checks that new shelf line
ids do not reuse its old weekday line ids. `ShopStockContentTests` and
`HeadlampGearDefTests` also validate this historical payload. Their assertions,
the Def registrations and the frozen identity ledger are unchanged.

The remaining legacy payload describes the rod/capelin/ice pilot. It is not a
live replenishment policy. Removing or simplifying it needs a separate identity
and content migration after the shelf integration settles; this change does not
remove it merely because its simulation has gone.

## Verification and save boundary

Static symbol and protected-file checks were performed before publication. The
new shelf tests and retained content tests need CI execution; Unity, compilation,
player behaviour and allocation results were not verified locally. Nothing in
this audit claims otherwise.

Save version 25 is unchanged. Current shelves recompute calendar supply and hold
player draws only for the session. Any future save step must persist those draws
and their line/time/unit identity, reconcile them with the pending purchase/bag
model, and test reload without a duplicate draw or replenishment. It must not
revive the deleted `ShopStockRecord` as a second stock authority.
