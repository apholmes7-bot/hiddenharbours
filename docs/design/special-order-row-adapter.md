# Paid special orders and reserved rows

One session `SpecialOrderBook` belongs to the player and serves every seller. Payment happens at
placement; arrival holds the goods at the counter; collection hands every line into detached bait
stock through `TackleBox.TryReceiveBait`. Bait does not belong in the supply shopping bag. No scene
host or interaction is installed by this change, and no save is read or written.

The six offer assets reference existing bait Defs. Their unit prices are read from those Defs when
offers are built. A paid order retains its original total and each line's paid share even if an offer
is later repriced, its delivery wait changes, or its outstanding limit falls. Counts are individual
baits. Multi-line orders pay once and use the latest promised delivery day among their lines.

## Session and row contract

`SpecialOrderRowAdapter` exports/imports detached `SpecialOrderRows`. Its public field names and
types match the special-order portion of the reserved version-26 rows; it has no dependency on
those not-yet-integrated Core DTOs. `SpecialOrderRow` maps to `SpecialOrderDto` and
`SpecialOrderLineRow` maps to `SpecialOrderLineDto` field for field.

| Field | Meaning |
| --- | --- |
| `NextSpecialOrderNumber` | One counter across every seller, starting at 1. |
| `OrderId` | `order.<seller>.<n>`; the full seller id is included and n follows the last dot. |
| `SellerId`, `DeliverToId` | Seller and destination; empty destination means his counter. |
| `Lines` | Offer Def id in `ListingId`, individual `Count`, and the entire line's `PaidPrice`. |
| `PaidPrice` | Authoritative total that left the purse, never recomputed from current prices. |
| `OrderedAtSeconds`, `OrderedDay`, `DueDay` | Original game time and integer days; never rederived from a retuned day length. |
| `State` | 0 waiting, 1 arrived, 2 collected. Collected rows are omitted on restore/export. |

Restore advances the counter past all readable numbers, including collected rows, and never lowers
a counter already used by the session. `int.MaxValue` is the exhaustion sentinel; placement refuses
before charging when there is no representable successor. It also reserves numbers on damaged rows.
Null rows carry no payment and are skipped. A missing snapshot is a no-op; an empty snapshot clears
the open book while preserving its used counter.

## Damaged paid rows

Restore evaluates each open row independently and retains every non-null row, including its raw id,
line list, payment shares and total. Empty or duplicate open ids, retired/mismatched offers, invalid
dates or times, unusable lines, unsupported destinations, invalid states and inconsistent payment
shares receive a plain `special_order.*` reason key. Every open participant in a duplicate id is
flagged. Collected rows do not participate in the open book, but still reserve their numbers.

Flagged rows never arrive, cannot be collected and do not consume the active offer's outstanding
limit. Their snapshot exposes `RequiresRefund`, `RefundReasonKey` and `RefundAmount`. The refund
amount is the recorded total, floored at zero for a negative damaged total; line shares are kept for
review and never silently substituted for that total. Restore does not credit a purse or publish
transaction events. Actual refund settlement is a later integration step.

Refund status is derived from row contents and the current catalogue, not saved in an extra field.
Export retains damaged rows unchanged. The adapter does not replace the version-26 load repair:
run that repair first, then let this book classify the repaired rows against available offers.

## Integration after the reserved rows land

1. Construct one book with all sellers' offers and the player's wallet/clock. Restore the clock first.
2. Copy the reserved counter and order DTOs field for field into the adapter envelope and restore it.
   Do not charge for restored orders. Refresh arrivals once after restoring.
3. Keep a detached session bait list. Bind collection to a completed counter handover; a successful
   call adds the whole order atomically, while an unavailable or overflowing bait store changes nothing.
4. At the save boundary, copy the adapter's rows and counter into the existing reserved fields and
   persist the bait stock, purse and open-order removal together. Do not save each seller separately.
5. Expose refund reasons at the counter and settle a flagged row's recorded payment and removal in
   the same transaction. Rows with missing/repeated ids need an unambiguous row selection; refund by
   id alone is unsafe. Keep unresolved rows until that settlement succeeds.
6. Dispose the book with the session. No additional schema version or saved field is required.

## Verification

The existing `SpecialOrderBookTests`, `ShopStockContentTests`, `BaitAndTackleTests` and
`RestUnderTheCoverTests` cover the book, source prices, atomic bait handover and real-clock midnight
arrival. The former all-or-nothing malformed-restore assertion is migrated to paid-row retention
with exact refund reasons and valid neighbours. Numeric ids become global string ids; hands/bag
callback refusals become bait-store refusals with exact stock checks. No prior rule is relaxed.

The row round trip is book to rows to JSON text to rows to book, including multiple lines, sellers,
waiting/arrived states and a retained refund row. Local work is limited to source, metadata and
contract checks. Unity and C# tests are not run locally; compilation and test execution belong to CI.
