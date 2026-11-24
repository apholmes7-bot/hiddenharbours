# Separate stock rule audit

The last item must be consumed. Buying it must not reduce another item's count, and an empty shelf waits for a delivery or a return. This audit uses base `37586ae43d2ed0eef2648a642c300a6926ac8bf1` and distinguishes the two stock models already present.

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

## Recorded fault owned by PR 1491

On this base, `StPetersStoreShelves.asset` maps bread, oatcakes and beans to the same `line.grocery_box`. Its 90 kg opening target divided by 5 kg per item yields 18 shared items. Eighteen bread purchases empty that line, so all three listings read zero even if no beans or oatcakes were bought. `IslandCounterShelves.Bind` maps each listing to the same line, and `IslandStoreShelves` rejects a second line for the same good. This is a source/data reproduction, not an executed editor result.

The fix requires the files PR 1491 owns: the shelf asset, `IslandStoreShelves`, `IslandStoreShelvesDef`, and counter binding. They were not edited. The reviewed diff adds per-line shares, separate bread/oatcake/bean lines and shelf purchasing. Those changes still need their own execution proof; this audit does not claim the live store already has separate food stock. Its new different-good tests prove only the independent lines that main supports. No skipped failing test or weakened assertion masks the shared-line fault.

A further integration check is needed after PR 1491: carry each line's purchase history across midnight. That diff records a line id for today's draws but still feeds past draws through the shared good ledger; verify that buying bread does not lower the next morning's untouched beans. This is an inspection concern, not a demonstrated failure on a merged build.

## Verification and save boundary

Whitespace and protected-file checks are local. No local compiler, Unity, EditMode execution, visual check or save write ran. CI must execute the three fixtures; eight additional test cases and three migrated cases are expected. A static arithmetic check independently verified the stated day table and resident-use example, not C# execution.

No saved field or save-version change is added. The dormant service already exposes `ShopStockRecord` snapshots through its memory state store. A future save step would need to persist those records (shop, line, count, settled day). The live island model still needs per-line timed purchase history plus restoration policy; that work is outside this change. The dormant fix changes no visible shelf until that service is composed; the live independent-food fault remains with PR 1491.
