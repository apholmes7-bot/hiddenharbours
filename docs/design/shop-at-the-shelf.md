# Buying at the shelf, and the counter catalogue

Status: plan, with slice 1 built (this PR). Owner direction, 2026-11-23:

> "the player doesnt deal with the clerk, they approach the item and its price appears, if they accept
> the purchase it then goes into their hands/storage, there can be a catalogue at the shopkeepers counter
> for special order items that are not normally stored, there is a delivery wait on these items and they
> are paid for on order"

Owner ruling relayed the same day: special orders are saved, **in v26** (the save change already
approved for the island economy's slice 8). So v26 is planned below as **one** change holding both the
store purchase rows and the paid special orders awaiting delivery.

Pillars: P3 (a living, working coast: the shelf empties and fills with the island's supply run),
P5 (cozy with teeth: a bare shelf and a short purse are real), P2 (the catalogue is where bigger gear
comes from before he can reach the mainland).

---

## 1. In one paragraph

In a shop, the goods sit where they are kept. Walk up to one and its name and price appear in the same
speech bubble every other "press to do this" uses. Press, and he pays and the good goes where that kind
of good is kept (the supply locker, the tackle box, his hands). The clerk no longer sells what is on the
shelves. Her counter keeps the **catalogue**: things the shop does not stock, paid for when ordered, that
come over on the island's supply run and are collected at the counter.

---

## 2. Every shop and seller on main, and what each sells

Taken from the catalogue data on main (`Data/Resources/Catalog/**`, `Data/Resources/Sellers/*`).

| Seller | Where | Keeper | What it sells today | Shelf plan |
|---|---|---|---|---|
| `seller.leblancs` (Leblanc's general store) | St Peters, `generalStore` | Marguerite Leblanc | at her counter in the scene: Bread ₲4, Oatcakes ₲2, Tin of Beans ₲3, Bag of Ice ₲6, Bar of Soap ₲3, Capelin ₲2, Fishing Rod ₲60, Depth Sounder ₲140, Shellfish Licence ₲15. The data also lists her as a stockist of the other baits and the shellfish gear, but her counter does not offer them today | the only shop with counted island shelves (`StPetersStoreShelves`: grocery, ice, bait, gas lines). Slice 1 moves bread, oatcakes, beans, ice and capelin to the shelf. Slice 2 moves soap, and adds the baits and shellfish gear the data already gives her. |
| `seller.nmc_chandlery` | Nine Mile Creek | Claudette Boudreau | Fishing Rod | rod rack on the wall (slice 5) |
| `seller.nmc_fishing_gear_wholesaler` | Nine Mile Creek | Bernadette Pitre | the bait list and the shellfish gear above, Rod Rack | bait cooler, gear racks (slice 5) |
| `seller.nmc_boat_gear_wholesaler` | Nine Mile Creek | Lionel Cormier | Dome Compass, Flush Compass, Wind Dial, Wind Display, Fish Finder, Loran-C, Chartplotter, Radar, Autopilot | display cases (slice 5); the costliest stay "fitted to your boat" through the counter |
| `seller.nmc_harbourmaster` | Nine Mile Creek | the harbourmaster | Cod Fishing License | stays a counter service (a licence is paper, not a good on a shelf) |
| `seller.nmc_yard` | Nine Mile Creek | the yard | boats (punt to side dragger), Crab Pot, Lobster Pot | boats stay at the counter (they are sold by the hull, not carried); pots could stand in the yard as stacked goods (slice 5, optional) |
| `seller.hectors_barrel` | St Peters | Hector Bernard | Used Outboard, fitted to your dory | stays a counter deal (it is fitted, not carried) |
| `seller.nmc_vehicle_dealer` | Nine Mile Creek | none | ATVs, bikes, trucks, a van | stays as is (vehicles stand on the lot already) |
| `seller.nmc_industrial_yard` | Nine Mile Creek | none | box trucks and semis | stays as is |
| `seller.nmc_wharf_market` | Nine Mile Creek | Wendell Arsenault | buys fish only, sells nothing | not affected |

Fuel is sold at the pump, which is already "walk up to the thing". The `gas` line on Leblanc's shelves
keeps its sold-out card; nothing about the pump changes.

### How a good maps to a shelf slot

The room art is painted. A **slot** (`ShopShelfSlotsDef`, one asset per shop) is a place on that art,
given as an offset from the room's pivot, standing on one **shelf line** (the counted stock in
`IslandStoreShelvesDef`). Each slot can now carry:

- the **listing** it sells (`ListingId`, e.g. `supply.bread`). A slot with a listing is a good he can buy.
- an **empty-shelf cover** sprite (laid over the painted goods, so the shelf can be genuinely bare) and a
  **goods** sprite (laid over the cover while the line has stock).
- whether the **sold-out card** shows there (one card per line).
- whether buying needs a **free hand** (for goods carried off in a hand).

A line is marked `SoldAtShelf` in the shelves data. Every listing on such a line must have exactly one
slot, and every slot's listing must come off its own line; a content test holds both ways.

Slice 1, St Peters general store:

| Slot | Line | Sells | Art | Card |
|---|---|---|---|---|
| groceries | grocery | (the gondola itself) | empty-gondola cover, stocked-gondola goods | yes |
| bread, oatcakes, beans | grocery | Loaf of Bread, Oatcakes, Tin of Beans | (drawn by the gondola) | no |
| ice | ice | Bag of Ice | empty chest cover, ice bags | yes |
| bait | bait | Capelin | bait box | yes |
| gas | gas | (pump stock) | none | yes |

The three grocery goods share one line, so they empty together. That is the island's rule today (one
grocery count for the store); a later data change can split the line if the owner wants bread to run out
before beans.

---

## 3. Hands and storage, with what main has

What the systems on main do today, and so where a bought good goes:

| Kind of good | Where it goes on main | Hands involved? |
|---|---|---|
| Supplies (bread, oatcakes, beans, ice, soap) | the **supply locker** (`SupplyLocker`, saved as `SupplyStock`), the same place the counter puts them | no |
| Bait | the **tackle box** (`TackleBox`, saved `BaitStock`) | no |
| Gear (rod, shovel, rake, tongs, bucket, basket, sack) | owned-gear flags (`GearShop`), drawn in his hands when used | not at purchase |
| Instruments (sounder, compass, ...) | fitted to the boat he is buying for | no |
| Licences | a saved flag | no |
| A carried thing (a pail, a box) | `GameServices.Hands`, two hand slots | yes |

So in slice 1 nothing goes into his hands: every shelf good goes to the locker or the tackle box, exactly
as when the clerk sold it. The "hands full" refusal is built and data-driven (`NeedsFreeHand` on the slot)
for goods that will be carried off in a hand. It is off for every shipped slot, and a content test says
so. When a good does need a hand, one free hand is enough.

The storage the owner has ruled (storage with memory at Ginny's, the camper and shed) is where carried
goods are later put down. Nothing in this plan changes it.

---

## 4. The price prompt

- **Where it shows.** The interact speech bubble that already shows every "press to ..." label (the
  Notebook and chat-bubble design language). No new HUD panel.
- **What it says.** `Buy Loaf of Bread — ₲4`. On an empty shelf: `Loaf of Bread — sold out` (still
  offered, so a press answers why). The words are format constants beside the label code (the
  localisation seam), the names and prices are data.
- **Several goods in reach** (the three on the gondola): the existing chooser opens with one row per good,
  each with its price. Step and confirm with the existing chooser controls.
- **Input.** The existing interact intent: keyboard and mouse, and the gamepad's interact button. The
  chooser uses its existing step and confirm. Nothing new is bound.
- **The answer.** Every outcome is a line in his own bubble, from data (`InnerVoiceLineDef`):
  bought ("Paid, and put by."), sold out ("Shelf's bare. Next run, maybe."), too dear ("Can't afford
  that."), hands full ("My hands are full.").
- **Cost per frame.** None. The label is a cached string rebuilt only when the price, name or sold-out
  state changes; the shelf asks for a fresh quote only on a stock change or on walking in.

---

## 5. The counter catalogue (special orders)

Not built in slice 1. Planned for slice 4, after the save change.

- **Which goods.** Things the shop does not keep on its shelves. A listing is offered for special order by
  data: a new `SpecialOrder` block on the catalogue listing (`SpecialOrderSellers`, `LeadDays`, an
  optional `Deposit` that defaults to the full price). Proposed first set at Leblanc's, all already defined
  as goods on main: the Depth Sounder, the Dome Compass, the Wind Dial, the Rod Rack, and the baits
  Leblanc's does not keep on the shelf. Big-ticket items (radar, chartplotter, boats, vehicles) stay with
  the mainland sellers.
- **Paid on order.** The full price leaves his purse when he orders, through the same purchase seam the
  counter uses today. The order is a saved row (section 6). No refund unless the good is removed from the
  game's data (then the heal refunds it on load).
- **The wait is the island's supply run.** The order arrives on the **first supply run day at least
  `LeadDays` after the order day**, using the same run calendar the shelves restock on
  (`IslandRunDays`, daylight runs on the island's real tide). A storm week means a late parcel, which is
  the island's rule for bread too. The due day is worked out when he orders and saved with the order, so
  a later data tweak does not move a parcel already promised.
- **What the catalogue shows.** The clerk's book, on a "Catalogue" tab: each good, its price, and "comes
  on the next run after <day>". After ordering: the order with its due day.
- **Where it is collected.** At the counter: a wrapped parcel appears on the counter top on the due day
  (the same slot-and-sprite pattern as the shelf goods, with a "Collect <good>" label). Pressing it puts
  the good where that kind of good goes (section 3). Several parcels: the chooser. Uncollected parcels wait
  with no limit.

---

## 6. Saved state: save v26, one change

Nothing in slice 1 is saved. Draws on the shelves today are session-only: a reload rebuilds the shelves
from the calendar, which is the island as the calendar says.

**v26 is ONE bump, holding two lists.** The first is the island economy's approved
`EconomyLedgerRows` (see `st-peters-living-economy.md` §4.1), which carries the store purchase rows. The
second is the paid special orders awaiting delivery.

### 6.1 Row shapes

```
EconomyLedgerRows: List<EconomyLedgerRowDto>
  LedgerId           string   the shop id ("shop.st_peters_general")
  KeyId              string   the shelf line ("grocery")
  Units              int      items taken
  Day                int      game day of the purchase
  Seconds            double   game seconds of the purchase (same-day ordering)
  SettledThroughDay  int      once a day settles, its rows fold to one per line

SpecialOrders: List<SpecialOrderDto>
  OrderId            string   "order.<seller>.<n>", n from a saved counter, never reused
  SellerId           string   "seller.leblancs"
  ListingId          string   "instrument.depth_sounder"
  Count              int      1 for now
  PaidPrice          int      what left his purse
  OrderedAtSeconds   double
  DueDay             int      the run day it comes on, fixed at order time
  Collected          bool     true once picked up (the row is then dropped on the next save)
NextSpecialOrderNumber: int
```

### 6.2 Migration v25 to v26

- `CurrentVersion` 25 to 26. A v25 save loads with both lists empty and the counter at 1: "the island as
  the calendar says, no orders". No data is transformed.
- The heal (run on every load) makes both lists non-null, drops ledger rows with empty ids or units under
  1, drops orders whose `Count` is under 1, and refunds and drops an order whose listing no longer exists.
- Self-clearing: a ledger row is dropped once a run day after it has topped its line up fully (the same
  rule the shelves use in memory today). An order is dropped once collected.

### 6.3 Replay on load

- **Shelves:** after the shelves are built from the calendar, each surviving ledger row is applied as the
  same draw the counter makes in a session (`IslandStoreShelves` take at `Seconds`), oldest first. The
  shelves then count exactly as a session that never closed.
- **Orders:** for each uncollected order with `DueDay` at or before today, the parcel shows at the
  counter. Nothing is charged on load.

### 6.4 Tests (to write with v26)

- Round trip: both lists survive save and load unchanged.
- Migration: a v25 fixture loads at v26 with empty lists, and the rest of the save unchanged.
- Replay determinism: buy on day D, save, load at later times; every count equals the session that never
  closed, including across a supply run that tops the line up (the row then clears).
- Orders: order (money leaves once), save, load before the due day (no parcel, no second charge), load on
  the due day (parcel shown), collect (good where it belongs, row gone), save and load (gone stays gone).
- Heal: an order for a removed listing is refunded once; bad rows are dropped.
- Content: every special-order listing names a seller that has a counter and a positive `LeadDays`.

---

## 7. What happens to the clerk's book

- **Slice 1:** the book drops every good sold at the shelf (bread, oatcakes, beans, ice, capelin). The
  rest stays: the rod, the soap, the depth sounder and the shellfish licence (checked in the game: those four are what her book shows after the change). Buying at the
  counter is otherwise unchanged.
- **Slice 2:** as the rest of the store's goods reach the shelves, they leave the book too.
- **Slice 4:** the book becomes the counter's **catalogue** (special orders) plus the services a counter
  really sells (the licence). Selling to the clerk is unchanged.
- The book is the same presenter; only its rows change. Its rows come from the same builder the shelf
  uses, so a price is never different at the shelf and in the book.

---

## 8. Slice order

| # | Slice | What the owner sees | Save | Needs |
|---|---|---|---|---|
| 1 | **Groceries, ice and capelin at the shelf** (this PR) | walk to the bread, see its price, buy it; bare gondola and card when sold out; the book is shorter | none | architecture review of one small Core contract |
| 2 | **The rest of Leblanc's at the shelf** | soap and the other baits on shelves; rod and shellfish gear on a wall rack, bought into a free hand where it is a hand tool | none | art for the rack and the new goods; shelf lines for them |
| 3 | **Save v26** | a bought-out shelf stays bought out after a reload | **v26** (approved) | lead-architect |
| 4 | **The counter catalogue** | order a sounder, pay now, collect the parcel at the counter after the next run | v26 rows from slice 3 | catalogue tab, parcel art |
| 5 | **Nine Mile Creek shops at the shelf** | the chandlery rack, the wholesalers' coolers, racks and cases | none | art; whether NMC stock is counted (it has no island run) |
| 6 | **Tidy the book** | empty buy tabs gone | none | |

---

## 9. Open questions (with a lean on each)

1. **Should bread, oatcakes and beans empty separately?** Lean: not yet. They share the island's one
   grocery count today; splitting is a data change when the owner wants it.
2. **Rod and shellfish gear: on a rack (bought into the hand) or kept behind the counter?** Lean: on a rack,
   into a free hand, in slice 2. It is the clearest "approach the item" case.
3. **Is the shellfish licence a good or a counter service?** Lean: a counter service. It stays with the
   clerk.
4. **Which goods are special-order at Leblanc's?** Lean: the depth sounder moves from the book to the
   catalogue; add the dome compass, wind dial, rod rack and the baits she does not shelve.
5. **Special-order wait: run calendar only, or a fixed minimum too?** Lean: both: the first run at least
   `LeadDays` (data, 2 to start) after ordering.
6. **Can an order be cancelled?** Lean: no, paid on order means paid (cozy with teeth). Only a good removed
   from the game is refunded.
7. **Nine Mile Creek: counted stock or always stocked?** Lean: always stocked for now (no island run feeds
   it), shelves only for the look and the walk-up.
