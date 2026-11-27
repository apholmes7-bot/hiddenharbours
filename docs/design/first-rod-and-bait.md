# Counted rods and supply deliveries

The island store books four rods at 60 coins each. The rod line is plain data in the
existing shelf definition, ready for a fishing section to name by `line.fishing_rod`.
It still sells through the counter book; no purchase point or physical delivery is added.
The free rod, starting bait, and all existing physical shelf assignments remain as authored.

## Booked stock

`ReservedForSales` marks the rod line as a booked reserve. Its initial quantity is the
smaller of its shelf capacity and its share of the back-room target: four rods, using
10 percent of the hardware category and 1.25 units per rod. These units are supply
accounting quantities; this adds no boat hardware listing to the island store.

A sale removes its full quantity from this line. Background island consumption and
midnight do not consume or restore the reserve. On a delivery, the reserve receives
the same fraction of its own shortfall as the back room received of its order. A full
delivery restores four; a partial delivery restores only that fraction, a zero-load
run restores nothing, and excess never exceeds the booked share or shelf capacity.
Purchases made later on a delivery day remain deducted. Repeated reads do not deliver again.

The existing `IslandStoreLedger` replay reports each day's actual delivery to
`IslandStoreShelves`. The shelves reconstruct the reserve from purchase rows, then
cache the day's morning balance. A read before a purchase still gives the earlier
balance. Store stock belongs to the store; successive buyers consume the same reserve.
There is no new singleton or per-player state.

Other lines retain their existing background-consumption model. Their fractional
purchase accounting is not migrated by the reserved-line option.

## Purchase and save boundaries

The rod listing still spends 60 and grants gear ownership. Capelin still costs 20 for
ten and credits bait stock. Neither path places its purchase in a hand or paper bag.
The bag's current supply contract cannot safely represent either path by toggling a flag.
Physical sale and delivery remain separate work.

Existing version-26 `StorePurchases` rows hold the reserved sales; no saved field,
format, or migration is added. Reserved rows are retained beyond the normal lookback
so an extended blocked crossing cannot erase a sale. Replay work grows with elapsed
days since the first retained reserved sale and purchase count, only when the day or
purchases change. Reads within a settled day reuse the balance. A future compaction
must retain the delivered balance and replay boundary atomically; simply dropping
old reserved rows would invent stock again.

A physical-delivery save step must record the paid listing and lot, its exclusive
hand/bag/storage location, and restore those together with purse and shelf debit.
Existing rod ownership and bait rows alone do not establish that delivery contract.

## Verification

Existing `IslandStoreShelvesTests` covers zero, partial and full deliveries, repeated
reads, consecutive buyers, and a later sale on the delivery day. Existing
`IslandShelvesShippedTests` pins one sale to three remaining and four sales to zero,
through closing, the next opening, an extended blocked crossing, JSON reload, and
a full delivery restoring exactly four. The first capacity-limited delivery after an extended
blocked crossing restores only its fraction. Reverse reads preserve the blocked-night counts.

The real-tide rule for a reserved line is now exact constancy without sales; ordinary
consumable lines still must vary. `StoreSoldOutShippedTests` pins the reserve flag,
prices, count and listing wiring. Existing confirmation tests retain the 85 to 25 to 5
purse sequence and exact ownership/bait credits. The counted-listing validator accepts
gear as well as supply and bait, retaining uniqueness and seller checks.

Compilation, EditMode, PlayMode and content validation await CI. No local editor or
visual verification was run, and no physical purchase or first-week playthrough is proved.
