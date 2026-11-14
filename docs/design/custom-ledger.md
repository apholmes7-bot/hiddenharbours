# Till identities and daily custom

Every catalog purchase signal now carries a `TradeCounterparty`: `MerchantId` is the
keeper's stable NPC id, `VendorId` identifies the seller definition, and `TradeId` is
the seller's authored kind. The old constructors and original payload fields remain
available. Missing fields, including a default struct, report `unknown`.

`SellerDef` assets under `Data/Resources/Sellers` describe the six existing catalog
and fish-buying counters. Vendors resolve their existing seller id through those
assets; a multi-seller listing never chooses the first seller as the counterparty.
Keepers were copied from the existing NPC → dialogue → seller-pointer links.
This identity directory does not own stock, pricing, or the purchase flow.

Fuel stations and homes already have their own definitions. Those definitions now
have `KeeperNpcId` and `TradeId`; their existing definition id is the vendor id.
No keeper has been assigned to either fuel station or the home till. Empty keeper
data remains empty and becomes `unknown` in a signal.

`CustomLedger` is a pure, session-only Core object. Its caller supplies the absolute
game day (`IGameClock.DayIndex`) and each completed `ICustomSignal` to `Record`.
It has no subscription, clock, save field, player-facing display, or relationship
service dependency. A future consumer must subscribe once to each canonical signal
and pass the game day explicitly. The caller can hand over a detached day's snapshot
at `SEAM(relationships)` and then explicitly forget that day.

Units are the actual quantity traded: one supply, gear item, pot, instrument, licence,
hull, home purchase or repair; the bait lot's count; litres of fuel; and units of catch
sold. Price and the total inventory owned never determine custom. Starter-home grants
contribute zero. A free completed transaction still counts. Fractional fuel quantities
are converted to decimal before accumulation; invalid quantities contribute zero.

Snapshots sort by trade id, then merchant id, using ordinal comparisons. Each merchant's
share is its units divided by all custom in that trade on the same absolute day.
Unknown keepers participate in their authored trade's denominator; an unknown trade
is its own group. Multiple counters belonging to one merchant in the same trade
combine. Days remain independent even when recorded out of order. Snapshots do not
change when later transactions arrive, and reading one triggers no side effects.

`CounterSellReported` also carries the till identity, but it is a conversation summary
of `CatchSold`. It deliberately does not implement `ICustomSignal`, preventing a sale
from being counted twice. `FeeFronted` is a grant and contributes no custom.

## Parked catch-sale publisher wiring

The append-only `CatchSold` payload and ledger support are present. Its two publishers
in `SellService` still use the legacy constructor and therefore report `unknown`.
That file belongs to open PR 1208 and was left untouched under the lane ownership rule.

The remaining integration is to carry the actual counter's data-authored identity into
both `SellSpecies` and `SellAll`, including instant buyer and sell-screen callers.
Do not derive a merchant from a market's price-channel enum or a scene-wide object
search: several tills can share a price channel. The known conversation summary must
not be counted in addition to the canonical sale. No scene or UI change was made here.

## Verification

The two pure fixtures run without Unity: 26 signal cases and 14 ledger cases, including
all 120 permutations of a mixed day's transactions. Thirteen publisher cases and three
asset/resolution cases require Unity and are compiled for CI; they were not executed
locally. Native compilation covers the full Core and Economy sources and all four new
fixtures against installed managed libraries. Text checks cover seller references,
keeper dialogue chains, empty unassigned keepers, and the identity addition files.
Unity discovery and the existing identity/content validation suites remain CI proof.
