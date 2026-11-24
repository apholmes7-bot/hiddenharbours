# Two players: money and goods

Keep the first week about two friends earning and repairing one dory together. The
least disruptive choice is the existing shared purse and outfit-owned fleet, with
separate hands and personal catch records. Bought goods still go into a paper bag at
the shelf and travel by hand. Shared money is not shared reach: neither player gains
access to an item simply because the outfit paid for it.

The lean here is shared working goods and catch, with either player able to carry them
to a buyer and sell them into the common purse. The person who caught a fish keeps the
personal Notebook credit. Ordinary licences follow the current outfit rule; limited
licences remain scarce world rights, with explicit rules for who may work under them.
These goods, sale and finite-holder details are recommendations, not new approvals.

This is a source-based options document at `c626cea10d1cb496df41cca2f9ab2f580ad47b28`,
save version 25. No code, saved field, content or existing tuning value changes here.
The comparison concerns the island-only first week and demo: arrival and lodging,
clams, shore fishing, the broken dory at the St Peters slip, repair by hand, launch and
fishing aboard. Other regions remain closed by weather until the islander's word
around day seven. None of the options supplies a second boat for free or introduces
later licence auctions, leases or business accounts into that week automatically.

## What is settled, and what main actually provides

The [shared-business ruling](multiplayer-scoping.md#611-the-shared-purse-ruling-owner-2026-08-04)
already says one purse, fleet and licence position for the outfit, with personal
location and hands. Separate purses below are alternatives requiring a deliberate
reversal, not an unresolved default. The later
[campaign proposal](campaign-coop-plan.md#six-choices-to-settle-before-implementation)
is explicitly unaccepted; its personal-licence wording is not a superseding ruling.
The [limited-licence design](limited-licences-and-crewing.md) introduces finite rights,
named holders, crewing arrangements and seasonal tags; much of that is later-phase
design rather than current runtime state.

| Current source | Consequence for two players |
|---|---|
| [PlayerWallet](../../Assets/_Project/Code/Player/PlayerWallet.cs), [IWallet](../../Assets/_Project/Code/Core/Economy/IWallet.cs), `GameServices.Wallet`, `SaveData.Money` | One balance. Existing vendor overloads accept an `IWallet`, which is useful, but scene providers and global reads select only one today. There is no account identity, transfer or joint-approval policy. |
| [BuyCatalog.Confirm](../../Assets/_Project/Code/Economy/BuyCatalog.cs#L344), [IslandStoreShelves](../../Assets/_Project/Code/Economy/Island/IslandStoreShelves.cs#L127) | Count is checked, the vendor spends/grants, then the shelf takes a unit. This sequential solo path is not a coordinated two-actor commit. `_draws` records purchases in memory; time/seed alone cannot restore them. Stock is one pool per shop/line, never one pool per player. |
| [SupplyShop](../../Assets/_Project/Code/Economy/SupplyShop.cs#L81), `SupplyLocker`, `BaitShop`, `PlayerGear` | Purchases currently mutate flat stock/presence lists. The shelf/paper-bag work is pending separately; no shopping-bag type is present on this pinned main. Its integration must replace these destination assumptions, not add a competing bag implementation. |
| [StoredContainers](../../Assets/_Project/Code/Core/Save/StoredContainers.cs), [StoredContainerDto and StoredItemDto](../../Assets/_Project/Code/Core/Save/SaveRowsV25.cs) | Existing keyed storage is the starting point: `Key`, `ContainerDefId`, `Items`, `Catch`; item rows have `ItemId`, `InstanceId`, `Count`. They do not express a player owner, bag carrier or permission. Legacy pail/boat/freezer catch lists still coexist. |
| [LicenseService](../../Assets/_Project/Code/Economy/LicenseService.cs), `LicenseVendor`, `LicenseDef`, `ILicenseService` | One set of held class ids in `SaveData.OwnedLicenses`. The runtime has no finite-holder registry or tag ledger. `IsLicensed(id)` has no actor, water or season argument. Unlicensed catches are released, not prevented from being caught. |
| [OwnedFleet](../../Assets/_Project/Code/Boats/OwnedFleet.cs), `Shipwright`, `RepairLedger`, [HelmSlot](../../Assets/_Project/Code/Core/Boats/HelmSlot.cs) | A roster of hull-type ids drives one active runtime hull and one piloted slot. Repair is currently a saved boolean membership, not stages of hand-work. Buying together does not by itself produce a two-person boarding or helm-claim system. |
| [WharfSellPoint](../../Assets/_Project/Code/Economy/WharfSellPoint.cs#L142), `CarriedHold`, [SellService](../../Assets/_Project/Code/Economy/SellService.cs#L131), `Market` | A carried-pail path exists, alongside wired-hold paths. Sale takes an `IHold` and `IWallet`, removes fish, updates market supply and pays; it knows neither catcher nor owner. Price uses the current player's trait factor. |
| [CatchItem](../../Assets/_Project/Code/Core/Economy/CatchItem.cs), `HeldCatchDto`, `CatchRecordLog`, `CatchRecords` | Catch has species, value/weight and freshness, but no owner/catcher instance identity. The one global catch log receives actorless events. Personal Notebook credit must be separated from cargo custody and sale proceeds. |
| [SaveService](../../Assets/_Project/Code/Core/Save/SaveService.cs#L174), `SaveData` | `DeferWrites` can defer disk writes while an operation completes; it does not supply rollback, ownership checks or a two-actor transaction. Sleep is the deliberate checkpoint; existing pause/quit/mutation writes also need coherent state. |

## Purse options

Cost labels are relative changes to the named current types, not day estimates.
Every option still needs one authority to order purchases and sales; passing two
wallet references around does not make stock and goods changes safe.

| Option | Cost in the code on main | First-week feel | What a future save step would need |
|---|---|---|---|
| **One shared purse — existing ruling; lean** | Lowest extra account work: keep one `IWallet`/`PlayerWallet`. Give `BuyCatalog`, vendors and `SellService` an actor context for permission, traits and receipts while using the same balance. Revalidate stock, funds and reachable delivery together; a completed action debits and grants exactly once. | Both clam hauls visibly fund the same broken dory. Either can buy bait while the other fishes. An unannounced large purchase can still spoil a friend's plan, so decide the consent rule for exceptional spending. | Keep `SaveData.Money` as the outfit balance. A durable spending history, if wanted, needs actor/account/purchase identity and amount; it is not another wallet. Preserve the old balance once, not once for each player. |
| **One purse each** | Medium-to-high: existing vendor `IWallet` parameters help, but replace the single provider/global wallet throughout buying, selling and displays. Define direct transfers, payment for jointly used goods, and which actor's traits set a quote. `MoneyChanged` and purchase/sale events need actor/account attribution. | Each player sees his own work pay. Saving for the dory becomes lending, pooling or choosing who buys it; unequal playtime can leave one behind. This changes the existing shared-business fantasy and needs approval. | Add player/account identity and per-player balances, conceptually `PlayerAccounts(PlayerId, Money)`. Define where old `Money` goes and the guest's starting balance; never duplicate it. Persist a transfer as one debit and one credit with a common operation identity. |
| **One purse each plus a household account** | Highest: all personal-account work plus account selection, deposits/withdrawals, spending rights and explicit rules for paying a bill. Extend Core contracts around `IWallet`; individual features must not reach into another feature's accounts. No silent partial split across purses. | Personal treats and common boat savings can coexist, but every bag of bait risks becoming bookkeeping. It is more natural once a household/business exists than during the small island week. | Preserve legacy `Money` as the household starting balance unless a different migration is chosen. Add personal balances, account identity and any persistent account permissions; define atomic transfers and a chosen payer for pending purchases. No such fields exist today. |

The same displayed amount is not permission to spend twice. If the purse can afford
only one purchase, two confirmed requests must produce one debit, one set of goods,
and one clear refusal, regardless of who pressed first locally. Do not automatically
reserve half of a shared purse for each player; that is a fourth account policy in disguise.

## Goods at a shelf and in a bag

All options preserve one finite stock per shelf line, a reachable paper bag and physical
transport. The island store sells basic fishing gear and supplies, not boat hardware.
If a bag is in the other player's hands elsewhere, the purchase cannot put goods into
it remotely. Buying rights, legal ownership, carrying and permission to consume are
four different facts.

| Option | Cost in the code on main | First-week feel | What a future save step would need |
|---|---|---|---|
| **Outfit-owned goods, individually carried bags — lean** | Lowest ownership overhead, though physical bag integration is still required. Build on the pending shelf/bag implementation and `StoredContainers`, `CarryHands`/`HandSlots`, `IStorageContainer`, `SupplyLocker` and kitchen storage. One bag instance has one carrier or one world/storage location. Consumption checks that location and access, not merely `SupplyStock`. | One player can shop for both and hand over the bag or put its goods away. They can work separate errands if they acquire separate bags; shared ownership does not conjure another bag or another shovel. | Keep each bag/container instance and contents once. Extend location/custody records with container identity, carrier player/hand or grounded region/pose or enclosing storage. Existing `StoredContainerDto.Key` and item `InstanceId` help, but do not supply those missing location facts. Reconcile `SupplyStock`, `BaitStock` and `OwnedGear` with physical contents without duplicating totals. |
| **Goods belong to the buyer, even when someone else carries them** | Higher: attach owner identity at purchase and carry it through `StoredItemDto`-style stacks, `CarryHands`, put-away, cooking, bait use, gifts and sale. A bag handoff changes carrier, not owner, unless the player explicitly gives it. Merging stacks must retain ownership or refuse the merge. | Personal snacks and tools feel protected, but helping with errands needs permission. A friend carrying the bait may be unable to use it; the distinction must be legible without routine prompts for every action. | Above custody data plus an `OwnerPlayerId` or an owner account reference per physical item/lot; bag ownership alone is insufficient after unpacking. Keep permissions only if they survive a session. Existing counted rows have no owner column. |
| **Shared provisions, personal tools and keepsakes** | Highest policy coverage: classify item ownership/access in data and make all consumers use the same Core rule. `PlayerGear`'s presence list cannot represent two individually owned shovels. `GiftInventory`, cooking, storage and buying need consistent rules for mixed contents; moving something into a shared bag must not quietly change its owner. | A useful long-term compromise: both can cook with the groceries while a player's keepsake remains his. In the first week it needs explanation about which objects are shared and when a gift changes ownership. | Shared instance/location records plus owner ids for personal rows and stable classification in Def data. A bag may contain both kinds, so preserve item/lot ownership through transfer and save. Migration must classify existing items explicitly and conserve their counts. |

Two players taking the final loaf must produce one purchase. The other player sees the
same sold-out truth. `SoldOutSignRule` remains derived from stock, never a second saved
flag. Shelf withdrawals and delivery/use history that depend on actions must survive
a later save step; neither shelf fullness nor a paid bag can be regenerated from the
clock alone. `SaveData` v25 has no shelf withdrawal history or bag carrier field.

## Licences, finite rights and crewing

An ordinary counter licence and one of a fixed number of fishery rights are not the
same kind of stock. The [limited-licence design, sections 8–11](limited-licences-and-crewing.md#8-determinism-and-the-save-rule-5)
proposes `LimitedLicenceDef`, `ILimitedLicenceRegistry` and `ICatchAllowance`; these
types are not implemented on the audited main. Its later save step is separate from
the pending version-26 work. This document does not approve or implement either step.

| Option | Cost in the code on main | First-week feel | What a future save step would need |
|---|---|---|---|
| **Licences held by the outfit, both partners entitled — current baseline; lean for ordinary licences** | Retains one `LicenseService`/`OwnedLicenses` set. A finite right still needs a single holder record, water/season checks and one shared cap/tag counter. `CatchLicensePolicy` can continue asking a Core entitlement service, but “class id is held” alone cannot implement limited rights. Simultaneous catches must compete for the same remaining allowance. | Ordinary licence spending advances their shared livelihood once. A partner does not have to repeat every early purchase. The later scarce right feels like an outfit achievement; it must not grant twice the pot allotment or tags. | Keep ordinary ids once in `OwnedLicenses`. A later finite-right state needs licence instance, holder (outfit or named legal holder), transfer deviations, arrangements and `TagsUsed` by licence/season. These are proposed semantic fields, not new code. Preserve existing entitlements on migration. |
| **Every player holds his own licences** | Higher: actor-scoped `ILicenseService` views and grants, remove global actor assumptions in release rules and catch signals, attribute all licence spending. Finite rights require unique world instances; giving two players the same class id must not mint a second right. This reverses the existing outfit entitlement rule. | Both must earn ordinary cover. One may fish legally while the other releases the same species, and an expensive finite right can separate friends' progress. It creates a stronger personal career but a more uneven island week. | Per-player ordinary licence membership and globally unique finite-right holder ids. Define who receives the old `OwnedLicenses`; do not copy finite rights. A deliberate ordinary-licence grandfathering rule could cover both, but needs approval. |
| **One named holder, partners work under explicit crew/permission rules — lean for resolving later finite-right ambiguity** | Most rule work: retain one finite right and implement an actor-aware permission view of it, using boat, water, season and story arrangement. The proposed `ICatchAllowance` must consume the holder's shared tags. `PlacedTrapService`, pot work and `CatchLicensePolicy` need the same entitlement decision through Core. Do not make proximity to the holder an accidental grant. | Two friends can work toward one scarce right and crew it together, preserving “hand first, holder later.” A partner fishing alone ashore may still release the catch unless the arrangement actually permits it. The first week needs no auction or lease system to explain this future rule. | One licence holder; durable crew/under-licence agreements if chosen; licence/season usage; transfer and lease overrides. Player identity is required even with shared money. Existing `PlacedTrapDto` lacks the acting player/licence attribution needed to enforce a shared allotment. |

For all three, every catch is evaluated: an uncovered catch is let go rather than
blocking the fishing action. Catcher credit can record that release. A right held by
an NPC must not silently become the outfit's just because a friend joins. Seeded NPC
holder history can remain computed; player-caused transfers and agreements cannot.

## The dory and everything aboard

| Option | Cost in the code on main | First-week feel | What a future save step would need |
|---|---|---|---|
| **One outfit-owned dory, either partner may crew or take the helm — existing fleet direction; lean** | Lowest ownership policy cost, but real occupancy work remains. Extend `OwnedFleet`, `ControlSwitcher`, `HelmSlot` and `IActiveBoatService` so each actor has support/location and only one holds the helm. `RepairLedger`'s boolean does not implement the required shared hand-repair stages. Keep hull/container state separate from whoever is steering. | Both hauls buy one visible boat at the island slip; both can help mend and launch her. One steers while the other works only where the boat has valid physical support. Do not promise room for two from this source review. | One boat instance with hull type, owner outfit, world region/pose and repair facts; per-player aboard/support state. `OwnedBoats`, `RepairedBoats`, `ActiveHullId`, `RestBoat*`, `BoatHoldCatch` must be mapped without duplicating the dory. Fuel/fit rows currently keyed by hull type need instance identity if multiple identical hulls become possible. |
| **The purchaser owns the dory and lends access** | More permission work around the same occupancy changes: assign owner and govern boarding, helm, alterations, moving, sale and guest access through Core. `Shipwright` receives a wallet today, not an owner identity. Payment from a shared purse does not resolve who owns it. | A recognisable skipper and helper, but a friend whose clam money helped buy her may feel like a passenger. Lending can support generosity; access prompts and absent-owner locks can undermine the first week's cooperation. | Boat `OwnerPlayerId`, persistent permissions if any, shared physical pose/cargo, and per-player support. Define ownership when the purchaser stops playing. Never move the boat into the other player's save automatically. |
| **Each earns a personal boat later; the first dory's ownership is explicitly chosen** | Highest fleet work: `OwnedFleet` currently swaps one runtime hull, while `ActiveHullId`, `HelmSlot` and global active-boat services select one. Need multiple boat instances, separate holds/fuel/fit and actor-specific control/travel. The same hull type can no longer serve as instance identity. | Independence eventually, but two parallel repair ladders dilute the first shared boat milestone. Do not accelerate the first week by gifting a duplicate dory. This is a later scope decision. | A world boat-instance table, ownership and location per boat; active/aboard boat per player; hold/fuel/equipment records keyed by instance. Migration creates one instance for each existing physical boat, never one per joining player. |

What is aboard is a separate choice from who owns the hull:

| Cargo/gear policy | Code cost and feel | Save implications |
|---|---|---|
| **Shared working cargo — lean with shared purse** | `ShipHold`/`StoredContainers` are world containers; either partner can move their contents when physically present and allowed. Fuel and installed instruments stay with the boat (`BoatFuelTank`, `InstrumentLocker`), not with the last helmsman. This makes loading and unloading useful cooperative jobs. | One inventory/location per container; reference boat instance. `BoatHoldCatch` cannot be copied into two players' inventories. Counted pots aboard, set and stored must be reconciled against `PotStock`. |
| **Personal cargo in shared storage** | Needs item/lot owner metadata and access rules through `IHold`/`IStorageContainer`, transfer, gift and sale. The owner of the boat cannot automatically sell a passenger's fish. It protects property but adds friction when sorting mixed catch. | Owner on each lot or a strict separation into individually owned container instances. `HeldCatchDto` aggregates identical units without ownership; mixed-owner stacks need a new representation, not an inferred split. |

Dropping a bag or pail aboard changes its supporting container/location; it does not
teleport its contents into a personal wallet. A hull transfer carries its installed
equipment and fuel unless an explicit, physical removal says otherwise. Cargo access,
boat sale and disposal are separate permissions, even if either partner may steer.

## Who may sell whose catch

| Option | Cost in the code on main | First-week feel | What a future save step would need |
|---|---|---|---|
| **Either partner sells outfit catch he physically delivers — lean** | Preserve the `SellService(IHold, IWallet, Market)` shape with Core actor/access validation in front. Use `WharfSellPoint`'s carried-container path; disallow remote wired-hold sale in the shared flow. Credit the actor's `CatchRecordLog` at landing, independently of sale. Apply the actual seller's traits, not an arbitrary global player. | One digs or fishes while the other carries the pail to the buyer. Both see the common purse grow. A handoff shares labour without erasing who caught the fish. | No per-fish legal owner is necessary for shared goods. Save each container/freshness once and per-player `CaughtSpecies` once; actor-attributed events/operation identities prevent duplicate credit/payment. The market's action-dependent supply also needs a coherent world record. |
| **Only the catcher may sell** | High provenance cost: `CatchItem`, `HeldCatchDto`, aggregate stacks, gifts and storage have no catcher id. Every split/merge must retain it. `SellService` must partition mixed holds and pay the chosen account without consuming a friend's fish. | Protects personal earnings, but the friend who did the carrying cannot finish the errand. Mixed clam pails become awkward. This weakens the shared-purse division of labour. | Catcher/owner identity per lot, retained through storage, transfers and sale; per-player proceeds if purses are separate. Reconstructing provenance for old mixed holds is impossible without an explicit migration default. |
| **Owner may delegate sale of a delivered container or lot** | Highest permission work: a durable or session-scoped delegation, scope/revocation rules, and validation against the exact remaining contents. `CarriedHold` identifies what is carried, not permission to sell. Decide whether adding fish invalidates the permission. | Personal control with useful errands, but repeated consent interrupts a short cosy loop. It may fit valuable later catches better than ordinary first-week clams. | If consent survives reload, store grantor/grantee, container/lot, permitted quantity or contents revision, and proceeds account. If session-only, expire on load and say so. The save must never contain a debit/removal without its matching payment. |

Selling is never permission to reach through a friend's hands or a distant boat. The
catch remains an object carried, put down, loaded and unloaded by hand. If two sale
requests target the same pail, only one may remove and pay for its fish. Shared money
does not make double payment harmless. A quote must be revalidated against freshness,
market supply and seller traits at commit; the second sale sees the first sale's market
effect. Catch already released cannot be sold, gifted or re-counted on a handoff.

## Minimum future persistence and acceptance work

All options need stable player identity and world item/container identity before
durable shared play. The names below identify existing fields and missing facts;
they do not authorize a schema or allocate a version number. Keep old saves readable,
map existing personal state to one original player, and account for every old coin,
licence, boat and unit of goods exactly once.

- **World facts:** `Money` if shared; shelf withdrawals/delivery history; market supply
  affected by sales; `OwnedBoats`/`RepairedBoats` mapped to physical boats; `HullFuel`,
  `HullInstruments` and instrument preferences; `PlacedTraps`; container contents,
  freshness and locations; limited-right transfers/usage when that phase is approved.
- **Personal facts:** player id; hands/custody and support; `CaughtSpecies`,
  `PlayerNotes`, `PlayerNeeds`, `PlayerBody`, `PlayerCleanliness`, `PlayerTraits` and
  the `Rest*` anchor; money/licence membership only under the chosen alternatives.
  A shared purse never makes personal notes, traits or bunk locations shared.
- **Transactions:** future commits must preserve a complete before-or-after state
  across funds, shelf stock, delivery, cargo removal, licence usage and payout. A
  repeated request cannot perform a second mutation. Persist the minimum operation
  history needed for the chosen reconnect/recovery policy; do not treat `DeferWrites`
  as a rollback system or assume every technical write happens only at bedtime.

Useful later acceptance cases are two buyers competing for the last affordable item;
two sales of one pail; one holder's final seasonal tag contested by two legal crew;
handoff while the recipient's hands are full; put-away into full storage; save/reload
with a bag in one player's hand and the dory in another region; and interruption at
the purchase/sale commit boundary. Each must conserve exact money, stock and physical
items. These are proposed tests, not results from this document. No Unity run,
two-player playtest, save migration or visual verification was performed here.

## Questions only the owner can settle

1. **May either partner commit large purchases from the shared purse without the
   other agreeing?** Lean: ordinary supplies need no prompt; buying or selling a boat
   or scarce licence requires both present players to agree. This adds a small
   approval state for exceptional acts while preserving the shared-purse ruling.
2. **Are ordinary working goods and catches outfit property, or should the buyer/
   catcher retain personal ownership?** Lean: share working goods and catch, keep
   personal Notebook credit and explicitly named keepsakes personal. This permits
   useful shopping and carrying errands without mixed-owner clam stacks.
3. **For a limited licence, is the legal holder the outfit or one named partner,
   and may the other fish independently under it?** Lean: one conserved right, a
   named holder where the story needs one, explicit partner/crew permissions and one
   combined pot/tag limit. Independent use only if that right's arrangement permits
   it. Ordinary outfit licences retain their existing scope.
4. **May either partner move the earned dory while the other is ashore or absent?**
   Lean: either may steer when aboard; no distant recall or relocation. Agree on the
   absence policy before durable shared play, and require joint agreement for sale.
5. **May a partner sell all shared catch he carries, including a fish the catcher
   meant to keep?** Lean: ordinary shared catch is sellable by the carrier; an
   explicitly reserved personal item needs permission. Reserve it before handoff,
   rather than expecting the sale code to infer sentimental value.
6. **If personal purses or a household account are desired after all, how is the
   old shared balance divided and how are joint purchases paid?** Lean: retain the
   shared purse for the first week; do not invent a retroactive split. A later
   account change should begin with an explicit transfer rule, never duplicated cash.
7. **Whose selling bonus applies when one friend catches and the other sells?**
   Lean: the player actually making the delivered sale. The catcher keeps his
   catch record, and the seller's personal trait supports a distinct cooperative job.
8. **May the host spend, sell or consume shared goods while the friend is away?**
   Lean: allow routine shared work if solo continuation is allowed, retain the absent
   player's identity and personal possessions, and postpone jointly approved asset
   disposal until both return. Offline elapsed wall time must not simulate extra
   income or move goods.
