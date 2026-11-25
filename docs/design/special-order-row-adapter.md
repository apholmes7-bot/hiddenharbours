# Paid special orders on the saved rows

The transaction model uses the existing version-26 order rows. Main already persists shelf
purchases and shopping bags; neither system constructs this book. There is still no counter
interaction, physical parcel, delivery host or handover in this change.

## One book, two to four players

Construct one `SpecialOrderBook` per saved world and share it across counters and players.
Every `Place`, `Capture` and `Collect` call names the acting player. The host must obtain that
identity from the authenticated actor, never from another player's editable request data.
The shared book serializes callbacks through one transaction guard and allocates order numbers
across all players and sellers. It has no static player or singleton player state.

Core's `ISpecialOrderAccounts` supplies two operations: pay and assign an order to its payer,
and look up that owner. `SessionSpecialOrderAccounts` binds explicit player identities to
`IWallet` instances. It charges only the named player's purse, assigns ownership before the
placement event and never reassigns an existing order. Separate purses and an explicitly shared
purse can both be supplied; parcel ownership always stays with the player who ordered it.

Each player's book view contains only his outstanding orders. Collection checks ownership
before reading the row's state or receiving goods. Another player and an unknown owner receive
`UnknownOrder`, with no changes to money, goods, rows or events. Outstanding offer limits apply
per player. Arrival and collection receipts include the payer identity so a host can route them.
There is no cancellation or refund API for any player: the saved rows have no settlement marker.

**Ownership is session-only.** The exact missing saved value is the payer's stable `PlayerId`
for each `SpecialOrderDto.OrderId`. `SellerId` identifies the seller and `DeliverToId` identifies
the collection place; neither can be reused for the payer. This change adds no saved field or
schema version. A future approved save step must persist that association atomically with the
payment and order, restore it before exposing the book, and define how older ownerless rows are
assigned. It must also bind the correct persisted purse and receiving inventory to each player.
Until then, keep the same accounts object when reconstructing a book within a session. A fresh
session cannot infer ownership: existing rows remain intact but are hidden, cannot advance and
cannot be collected. New orders can still be owned for that session.

`CaptureOwnership()` exposes detached, sorted plain session facts. Editing that snapshot cannot
transfer ownership; it is not an import method or a saved DTO.

## Promised day and actual arrival

The six offers reference existing bait Defs for prices. Their `DeliveryDays` is a minimum lead,
not a delivery timer. A multi-line order takes the longest lead, then selects the first eligible
run on or after that day. `SpecialOrderSupplySchedule` requires the seller's existing
`IslandStoreLedger.RunsOn` policy, or the same `IslandRunCalendar.RunsOn` composed with
`IslandRunDays.Possible` and the runner's authored cadence. It does not simulate tide or weather.
The host supplies a positive configured search bound. No run within that bound means refusal
before payment, without allocating an order number. The search safely ends at the last integer day.

With a two-day cadence and open crossings, an order on day 1 with a two-day lead promises day 4.
If day 4's crossing is closed and day 5 is possible, the existing calendar promises day 5.
That promised `DueDay` is saved once and never recomputed after retuning or loading.

Calendar availability is not completed delivery. A separate host callback reports the last run
actually unloaded at that seller by the supplied game seconds, or no run. It must be deterministic
for that world's seed and game time. `RefreshArrivals()` requires that completed run to be a valid
calendar day, at or after the promise and no later than the current day. Midnight alone cannot
release a parcel. If a promised unloading is missed, a later completed run can fulfil the order
while the original promise remains on its receipt. Repeated refreshes do not repeat arrival events.

The host must refresh after unloading and after restoring or seeking the clock. Day events also
refresh, but still require completed-delivery evidence. Arrival changes only the order state and
never grants goods. `Collect` does not refresh any order; it follows completed handover at the
seller's counter. It atomically receives every bait line into the acting player's supplied stock,
then marks only that receipt collected. A waiting order, unknown owner, unusable paid row, missing
stock or stock overflow leaves all rows, money, goods and events unchanged.

## Saved contract and damaged orders

`SaveData.SpecialOrders` and `NextSpecialOrderNumber` remain the only saved order storage.
Placement appends a row; arrival and collection update it. Original quantities, total price,
per-listing paid shares, game time and promised day remain unchanged after content retuning.
Repeated listing requests add to their first line's count and payment; separate orders remain
separate. The whole count, price, lead, run and number are validated before payment.

| Existing field | Meaning |
| --- | --- |
| `NextSpecialOrderNumber` | One monotonically reserved number across every player and seller. |
| `OrderId` | `order.<seller>.<n>`; every readable retained number remains reserved. |
| `SellerId`, `DeliverToId` | Seller and collection place; empty destination means that counter. |
| `Lines` | Listing id, count and whole line's paid share. |
| `PaidPrice` | Original order total; never recalculated from current offers. |
| `OrderedAtSeconds`, `OrderedDay`, `DueDay` | Original game time, order day and promised run day. |
| `State` | 0 waiting, 1 arrived, 2 collected. Collected receipts are retained. |

`SaveSerialization.FromJson` still performs the existing version-26 repair. The book neither
changes that repair nor silently rewrites paid terms. It classifies unusable rows with derived
reason keys and preserves them. Removed content stays owed at the recorded amount.

ID collisions count **every retained row**, including collected receipts and rows owned by other
players. Every outstanding participant is blocked from arrival and collection. A collected row
plus an arrived or waiting duplicate cannot grant goods again. Both records and all their paid
terms survive JSON loading. There is no automatic erasure or refund of an ambiguous payment.

Snapshots deeply copy outstanding rows and expose `RequiresRefund`, `RefundReasonKey` and
`RefundAmount`; these fields are not saved. The amount is the original total, floored at zero
for a negative damaged total. Settlement remains absent. Snapshots cannot replace live paid rows.

## Integration and verification

The future host must supply all sellers' offers, the restored game clock, the same supply calendar
as the shelves, completed unloading information and the world accounts. Dispose the book before
changing saves. It must complete physical handover before collection and persist the entire
wallet/order/stock transaction together. No disk write, bag transfer, remote delivery, scene
host, saved payer identity, cancellation or refund settlement is implemented here.

Main already supplies `IslandRunCalendar`, `IslandRunDays`, `IslandStoreLedger`, save migration,
stock-content, bait/tackle and sleep-clock tests. This change extends `SpecialOrderBookTests`
and `RestUnderTheCoverTests`; it does not introduce another calendar or inventory store.
The midnight rule is migrated to completed daylight unloading, with exact payment, receipt,
stock, event and JSON assertions retained. Most transaction fixtures use a daily confirmed test
carrier; the calendar cases explicitly exercise two-day cadence, a closed crossing, bounded
search, missed unloading, late completion and unchanged promised dates. The real-clock cases
check both waiting and sleeping, a still-waiting sleep save and the later arrived save.

Ownership cases cover two, three and four players, separate money and stock, private book views,
cross-player collection refusal, per-player limits, failed payment, callback reentrancy,
detached ownership snapshots, reconstruction within a session and a fresh session without payer
identity. Source checks are not executable proof. No local compiler, native test run, live-save
write or gameplay capture was performed; the new head requires CI. No appearance change is made.
