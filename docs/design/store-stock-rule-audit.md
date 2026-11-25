# Separate stock rule audit

The last item must be consumed. Buying it must not reduce another item's count, and an empty shelf waits for a delivery or a return. This audit uses base `d46bde303e6aa256d23d47a05ede5d7d6f38e51e` and distinguishes the two stock models already present.

## Corrected fault: a sale at the floor created unlimited stock

`ShopRestock.TryTakeOne` used `max(Floor, count - 1)` and `Settle` raised a saved count back to `Floor` even on a day with no delivery. A three-item shelf with floor one therefore accepted thirty purchases and still reported one item. The shipped dormant pilot has floor one for capelin and ice. The smallest fix subtracts exactly one per sale and clamps settlement to zero instead of the legacy floor. Floor stays in the schema and assets for compatibility; it no longer manufactures an item. No GameConfig or authored price changed.

The existing `ShopStockRulesTests` essential-floor cases now require exactly three successes, thirty refusals and count zero; restored zero stock remains zero on Tuesday and Wednesday, then gets two on Thursday. This deliberately migrates the old guaranteed-essentials rule to the newer every-item-runs-out rule. Its exact assertions were strengthened, not removed. Existing calendar, bounds, sparse snapshot, determinism and allocation tests remain.

## Added coverage

- Two independently counted lines: draining the first from three to zero leaves the second at four, including an extra refused sale.
- Different delivery calendars and amounts, with seeds 17 and 9841: first line Monday/Thursday +2, second Tuesday +1. Day-zero-to-eight expectations are `(0,0), (0,1), (0,1), (2,1), (2,1), (2,1), (2,1), (3,1), (3,2)`. Watched and lazy settlement agree under repeated reads.
- Generic stock notifications: sale counts are exactly 2, 1, 0; failed purchases and repeated reads emit nothing; the Thursday return to two emits one restock notification.
- Live island model: taking all four gas cans leaves all eighteen grocery boxes. Resident use and player purchases draw down the same gas balance: 80 L minus two 20 L purchases minus 10 L resident use at midday leaves one full can; taking it leaves ten litres and no full can. Grocery counts remain sixteen at midday and fourteen at closing.
- A day-two island run blocked on both day two and its day-three fallback leaves the gas shelf empty through day three; the next allowed day-four run restores four cans and eighteen boxes, without exceeding either shelf.
- The existing live host publishes exactly one `IslandShelvesChanged` at zero and one at sixteen after a delivery, despite repeated polling and failed takes. These are the existing count-change events, not new event types. The test drives a synthetic host and never judges sign pixels.

The three existing fixtures extended are `ShopStockRulesTests`, `IslandStoreShelvesTests` and `StoreSoldOutTests`. There is no individually simulated NPC checkout buyer on this base; island residents are modeled as continuous daily demand, so the resident/player case uses that real demand path.

## Shelf separation now on main

Main incorporated shelf purchasing in PR 1491 (`00912f3e4c`). Bread, oatcakes and beans now have separate line IDs and shares of the grocery good; the former single retail grocery line is no longer their binding. The grocery reserve keeps a quarter share without a retail listing. `ShopAtTheShelfTests` and `ShopAtTheShelfShippedTests` cover this purchase path. The earlier first-day shared-line fault is therefore historical, not a current claim.

The synthetic independent-good and host-event cases added here remain useful and are not copies of those physical purchase tests. Their goods occupy whole shares, so their exact 18-box and four-can expectations are unchanged. No scene or shelf asset was edited.

Independent history across midnight is a separate boundary: `IslandStoreShelves.DrawnOn` still aggregates prior purchases by good for ledger replay, while same-day deductions identify a line. These tests do not establish that buying one food leaves every other food unchanged on later mornings. Save round trips are covered separately by `Save/BagAndPurchasesSavedTests`; that is not proof of independent food depletion across every delivery window.

## Verification and save boundary

Whitespace and protected-file checks are local. No local compiler, Unity, EditMode execution, visual check or save write ran. The previous CI run passed; the refreshed head still needs all three fixtures executed; eight additional test cases and three migrated cases are expected. A static arithmetic check independently verified the stated day table and resident-use example, not C# execution.

No saved field or save-version change is added. The dormant service already exposes `ShopStockRecord` snapshots through its memory state store. A future save step would need to persist those records (shop, line, count, settled day). Main now persists per-line timed shelf purchases and shopping bags in save version 26 through PR 1543. This draft adds no saved fields and does not alter that restoration path. The dormant fix changes no visible shelf until that service is composed. PR 1524 proposes retiring this dormant service and includes the useful live-stock cases; those alternative changes must not be combined by restoring the retired service.
