# Paid special orders on the saved rows

Reconciled with main `d46bde303e6aa256d23d47a05ede5d7d6f38e51e` (2026-11-25).
Main already persists shelf purchases and shopping bags through version 26. Those
systems do not construct this book or deliver special orders; the counter and
physical handover integrations below are still required. This proposal adds no
saved field and does not replace the shelf purchase or bag state.

One `SpecialOrderBook` belongs to the player and serves every seller. Its constructor takes the
current `SaveData`, the offers, the wallet and the clock. `SaveData.SpecialOrders` and
`NextSpecialOrderNumber` are the book's only order storage. Placement appends to that list;
arrival and collection update its rows in place. Every existing save point already serializes
those fields. Loading needs no order adoption, export or copy-back step.

The six offer assets reference existing bait Defs and read their current prices when offers are
built. Paid orders retain their original total, per-listing payments, counts and promised dates
when content is retuned. No scene host or counter interaction is installed by this change.

## Saved contract

The book uses the existing Core `SpecialOrderDto` and `SpecialOrderLineDto` directly. No saved
field, field type, schema version or load repair changes. `SpecialOrderRowAdapter` now only makes
a detached snapshot for inspection; it declares no substitute transport or save rows.

| Field | Meaning |
| --- | --- |
| `NextSpecialOrderNumber` | One counter across all sellers, starting at 1. |
| `OrderId` | `order.<seller>.<n>`; n follows the last dot. |
| `SellerId`, `DeliverToId` | Seller and destination; empty destination means his counter. |
| `Lines` | One listing per line: offer id, individual count and the whole line's paid share. |
| `PaidPrice` | Total that left the purse, never recomputed from current prices. |
| `OrderedAtSeconds`, `OrderedDay`, `DueDay` | Original game time and promised days, retained across a restart or retuning. |
| `State` | 0 waiting, 1 arrived, 2 collected. Collected receipts remain saved but leave the outstanding view. |

Repeated requests for a listing within one placement add to the first line's count and payment.
The combined count, outstanding limit, whole-order price and due day are validated before the
wallet is charged once. Different orders never fold together. Multi-line orders use the latest
delivery day among their lines: ordered day plus that offer's delivery wait. Seed and catalogue
iteration order do not change the promise.

The existing load repair folds repeated listings in older rows the same way, preserving the sum
paid per listing and the order total. The book creates canonical rows, so its saved rows survive
that repair unchanged. It never rewrites pre-existing paid lines, even when their payments disagree
with the order total. Already damaged rows are classified for review, not silently reconciled.

`SaveSerialization.FromJson` runs the version-26 repair before the book sees loaded rows. That repair
normalizes negative or non-finite order times to zero and unknown states to waiting. Classification
uses those repaired values. Directly supplied malformed rows are a separate boundary: the book flags
their invalid values without repairing them itself. Neither path changes the intended duplicate-line
folding or substitutes line payments for the recorded order total.

Construction reserves every readable order number, including collected and damaged rows, and only
raises the saved counter. `int.MaxValue` is the exhaustion sentinel: placement refuses before
payment when there is no representable successor. Collected rows are retained as receipts and
cannot grant goods again. There is no API for replacing a live book from a snapshot.

## Owed orders and snapshots

`Capture()` is observational and deeply copies outstanding rows. It exposes derived
`RequiresRefund`, `RefundReasonKey` and `RefundAmount` without saving those fields. Construction
neither charges the wallet nor publishes arrival or collection events. An overdue waiting row
advances only on a day event or an explicit `RefreshArrivals()` after the clock is restored.
An already arrived row stays arrived without replaying its event.

`Collect` never catches up arrivals, including those of other overdue orders. A waiting order still
returns `NotArrived` after a clock seek until the caller refreshes arrivals. Unknown orders and
unavailable or overflowing bait stock leave all saved rows, money, goods and event counts unchanged.
A successful collection changes only its own order and the receiving stock.

Missing or repeated open ids, unavailable or mismatched offers, unusable lines, invalid dates or
times, unsupported destinations, invalid states and inconsistent payments receive a plain
`special_order.*` reason key. Every open participant in a duplicate id is flagged. A seller or
listing that no longer ships remains owed with the exact recorded total and line payments.
Flagged rows cannot arrive or be collected and do not consume an active offer's outstanding limit.

The refund amount is the recorded total, floored at zero for a negative damaged total. Line
payments remain visible even when their sum disagrees with that total. The book never credits a
refund, removes a paid row or guesses a replacement offer. Refund status is derived from current
content and can be recomputed when content becomes available again.

## Integration still required

1. The future counter host must construct one book with `GameServices.Save.Current` after loading
   and restoring the clock, plus all sellers' offers and the live wallet. Dispose it before changing
   saves and construct a new book against the newly loaded `SaveData`.
2. Call `RefreshArrivals()` after restoring or seeking the clock. Day events already advance waiting
   rows in a constructed book. The physical supply run, its arrival presentation and its counter
   notification are not wired. Arrival changes the saved state and grants no bait.
3. Bind `Collect` to a completed handover at the seller's counter, passing that save's `BaitStock`.
   `TackleBox.TryReceiveBait` receives all lines or none. The row becomes collected only after receipt
   succeeds. Refresh arrivals before offering the handover after a seek; collection itself performs
   no refresh. No shopping bag, automatic transport or physical handover presentation is added here.
4. Ordinary save points serialize the current order rows and counter directly. The existing save
   service captures the live wallet and clock; collection must use the same save's bait stock so
   goods and the collected receipt persist together. No new save step or field is needed for orders.
   Save after the transaction completes; the book does not request disk writes itself.
5. Counter refund presentation and settlement remain work to do. Missing or repeated ids require
   unambiguous row selection. The reserved rows contain no refund-settlement marker; there is no
   automatic refund or invented saved field in this change.

## Verification

The existing `SpecialOrderBookTests` covers real save JSON through `SaveSerialization.FromJson`
(and therefore load repair), exact equality of all nine order fields and three line fields, repeated
listing coalescing, all three saved states, numbering after restart, missing order lists, retired
content, original paid terms, deterministic dates, refusals, reentrancy and detached snapshots.
Overdue refusal cases compare the entire saved JSON and snapshot before and after collection, including
unrelated due rows, exact stock and money, and arrival/collection event counts. Separate version-26
JSON cases prove time/state repair precedes classification and leaves the paid goods collectible.
`RestUnderTheCoverTests` checks due-midnight arrival with the real clock and observes the arrived
row in the sleep save's JSON. Existing content and atomic bait-receipt cases remain in
`ShopStockContentTests` and `BaitAndTackleTests`.

The old duplicate-request refusal becomes summed counts/payments with exact combined-limit checks.
Snapshot adoption becomes construction on the saved rows; empty views cannot erase payments.
Collected rows leave the outstanding view but remain saved receipts. These are explicit rule
changes; exact payment, count, event, date and refusal assertions remain.
Collection no longer advances arrivals implicitly. The two collection/restart cases that sought the
clock now refresh explicitly, retaining every payment, receipt, quantity and non-reuse assertion.

The pre-reconciliation head `26f433d082` passed CI run `45535514904`. Its order code,
content and test bodies are unchanged by the reconciliation; only this status note
and accepted main were added. Local verification is source, scope and metadata
inspection. Native tests and a complete build were not run locally; the merged head
still needs CI. No visual or gameplay acceptance is claimed.
