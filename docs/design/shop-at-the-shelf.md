# Buying at the shelf, and the counter catalogue

Status: plan, with slice 1 (goods at the shelf) and slice 2 (the paper bag, and groceries that run out
one by one) built. Owner direction, 2026-11-23:

> "the player doesnt deal with the clerk, they approach the item and its price appears, if they accept
> the purchase it then goes into their hands/storage, there can be a catalogue at the shopkeepers counter
> for special order items that are not normally stored, there is a delivery wait on these items and they
> are paid for on order"

Owner ruling relayed the same day: special orders are saved, **in v26** (the save change already
approved for the island economy's slice 8). So v26 is planned below as **one** change holding both the
store purchase rows and the paid special orders awaiting delivery.

Owner answers to this plan's questions, the same day:

> "perhaps when they grab an item a storage paper bag appears in the players hands if its an item that
> would appear in a storage bag, the player can fill the bag with multiple items then transfer the items
> manuallly to their storage via walking home or to storage on a nearby vehicle or anything considered
> players storage."

1. Every good runs out on its own ("all items are seperate").
2. Rod and shellfish gear go on a rack, bought into a free hand.
3. The licence is a counter service.
4. Boat hardware is left to Nine Mile Creek. At St Peters the rod rack and the baits stand on the floor,
   with a couple of tiers of rods and everything needed for basic fishing from a dory and shellfish work
   from the island.
5. The special-order wait: the first supply run at least 2 days after ordering (the lean, taken).
6. No cancellations.
7. Nine Mile Creek shops are supplied by larger supply routes using the player's company trucks and
   trailers, from the larger city (not built yet).

The first week of the game is St Peters only, and that is the demo. Everything the demo needs is in the
St Peters column below.

Pillars: P3 (a living, working coast: the shelf empties and fills with the island's supply run),
P5 (cozy with teeth: a bare shelf, a short purse and a walk home with the shopping are real), P2 (the
catalogue is where bigger gear comes from before he can reach the mainland).

---

## 1. In one paragraph

In a shop, the goods sit where they are kept. Walk up to one and its name and price appear in the same
speech bubble every other "press to do this" uses. Press, and he pays. A good that would go in a shopping
bag goes into a **paper bag in his hand**; the next one goes into the same bag, until it is full. He
carries the bag to anything that is his storage (home, the camper) and puts the shopping away there. A
hand tool (a rod, a rake) goes into a free hand instead. The clerk no longer sells what is on the
shelves. Her counter keeps the **catalogue**: things the shop does not stock, paid for when ordered, that
come over on the island's supply run and are collected at the counter.

---

## 2. Every shop and seller on main, and what each sells

Taken from the catalogue data on main (`Data/Resources/Catalog/**`, `Data/Resources/Sellers/*`).

| Seller | Where | Keeper | What it sells today | Shelf plan |
|---|---|---|---|---|
| `seller.leblancs` (Leblanc's general store) | St Peters, `generalStore` | Marguerite Leblanc | at her counter in the scene: Bread ₲4, Oatcakes ₲2, Tin of Beans ₲3, Bag of Ice ₲6, Bar of Soap ₲3, Capelin ₲2, Fishing Rod ₲60, Depth Sounder ₲140, Shellfish Licence ₲15. The data also lists her as a stockist of the other baits and the shellfish gear, but her counter does not offer them today | the only shop with counted island shelves. Slice 1 moved bread, oatcakes, beans, ice and capelin to the shelf; slice 2 gives bread, oatcakes and beans a count each and the paper bag. Slice 3 is the St Peters stock list below. The depth sounder leaves her (boat hardware is Nine Mile Creek's). |
| `seller.nmc_chandlery` | Nine Mile Creek | Claudette Boudreau | Fishing Rod | rod rack on the wall (slice 6) |
| `seller.nmc_fishing_gear_wholesaler` | Nine Mile Creek | Bernadette Pitre | the bait list and the shellfish gear above, Rod Rack | bait cooler, gear racks (slice 6) |
| `seller.nmc_boat_gear_wholesaler` | Nine Mile Creek | Lionel Cormier | Dome Compass, Flush Compass, Wind Dial, Wind Display, Fish Finder, Loran-C, Chartplotter, Radar, Autopilot; **the Depth Sounder joins here** | display cases (slice 6); the costliest stay "fitted to your boat" through the counter |
| `seller.nmc_harbourmaster` | Nine Mile Creek | the harbourmaster | Cod Fishing License | stays a counter service (a licence is paper, not a good on a shelf) |
| `seller.nmc_yard` | Nine Mile Creek | the yard | boats (punt to side dragger), Crab Pot, Lobster Pot | boats stay at the counter (they are sold by the hull, not carried); pots could stand in the yard as stacked goods (slice 6, optional) |
| `seller.hectors_barrel` | St Peters | Hector Bernard | Used Outboard, fitted to your dory | stays a counter deal (it is fitted, not carried) |
| `seller.nmc_vehicle_dealer` | Nine Mile Creek | none | ATVs, bikes, trucks, a van | stays as is (vehicles stand on the lot already) |
| `seller.nmc_industrial_yard` | Nine Mile Creek | none | box trucks and semis | stays as is |
| `seller.nmc_wharf_market` | Nine Mile Creek | Wendell Arsenault | buys fish only, sells nothing | not affected |

Fuel is sold at the pump, which is already "walk up to the thing". The `gas` line on Leblanc's shelves
keeps its sold-out card; nothing about the pump changes.

### 2.1 What St Peters stocks, and what Nine Mile Creek stocks

The owner's ruling: no boat hardware at St Peters. Leblanc's keeps what a player needs for the first week
on the island: food, ice, soap, bait, rods, and the gear for fishing from a dory and working the flats.

| Good | St Peters (Leblanc's) | Nine Mile Creek | Defined on main? |
|---|---|---|---|
| Bread, Oatcakes, Tin of Beans | shelf, **into the bag**, each counted on its own | | yes |
| Bar of Soap | shelf, into the bag (slice 3) | | yes |
| Bag of Ice | ice chest, straight to the supply locker (too wet and cold for the paper bag) | | yes |
| Capelin | bait box, straight to the tackle box | | yes |
| Herring, Mackerel, Squid Strip, Sea Worm, Shucked Clam, Fish Scrap | bait box on the floor (slice 3), straight to the tackle box | fishing gear wholesaler | yes |
| Fishing Rod | rod rack on the floor, into a free hand (slice 3) | chandlery | yes (one tier only) |
| A cheaper first rod and a better second rod ("a couple tiers of rods") | rod rack (slice 3) | chandlery | **no**: needs new gear defs and art |
| Shovel, Shellfish Rake, Oyster Tongs | floor rack, into a free hand (slice 3) | fishing gear wholesaler | yes |
| Clam Bucket, Shellfish Basket, Mussel Sack | floor, into a free hand (slice 3) | fishing gear wholesaler | yes |
| Shellfish Licence | counter service | | yes |
| Rod Rack (the fixture for a boat) | | fishing gear wholesaler | yes |
| Depth Sounder | **leaves Leblanc's** (needs scene wiring as well as data, see section 7) | boat gear wholesaler | yes |
| Dome/Flush Compass, Wind Dial, Wind Display, Fish Finder, Loran-C, Chartplotter, Radar, Autopilot | | boat gear wholesaler | yes |
| Cod Fishing License | | harbourmaster | yes |

**Nine Mile Creek supply.** The owner's ruling: Nine Mile Creek shops are fed by larger supply routes run
with the player's company trucks and trailers, from the city on the mainland, which is not built yet.
**Until the city and those routes exist, Nine Mile Creek shops are simply always stocked.** That is a
temporary rule, written down so that nothing is built that assumes it is permanent: when the city lands,
each NMC shop gets a counted stock fed by the truck route, the same way St Peters is fed by the island run.

### 2.2 How a good maps to a shelf slot

The room art is painted. A **slot** (`ShopShelfSlotsDef`, one asset per shop) is a place on that art,
given as an offset from the room's pivot, standing on one **shelf line** (the counted stock in
`IslandStoreShelvesDef`). Each slot can carry:

- the **listing** it sells (`ListingId`, e.g. `supply.bread`). A slot with a listing is a good he can buy.
- an **empty-shelf cover** sprite (laid over the painted goods, so the shelf can be genuinely bare) and a
  **goods** sprite (laid over the cover while the line has stock).
- whether the **sold-out card** shows there (one card per line).
- whether buying needs a **free hand** (`NeedsFreeHand`, for hand tools carried off in a hand).
- whether the good goes **into the paper bag** (`IntoBag`; the shop's slots asset names the bag, a
  `ShoppingBagDef`).

A line is marked `SoldAtShelf` in the shelves data. Every listing on such a line must have exactly one
slot, and every slot's listing must come off its own line; a content test holds both ways.

### 2.3 Every good is counted on its own

The owner's ruling: all items are separate, so bread can run out before beans. The island supply run
still delivers one **groceries** good to the store (that is how the island's supply works and is
unchanged). The store's shelf lines now split it: each line on the groceries good takes a **share** of it
(`ShareOfGood`, data), the shares of one good add up to no more than the whole, and each line counts its
own items with its own size per item and its own shelf maximum. A purchase draws only from its own line.
The counts stay a pure function of the island calendar and the session's purchases, so they are the same
every time for the same world and time.

St Peters general store, after slice 2:

| Slot | Line | Sells | Into | Art | Card |
|---|---|---|---|---|---|
| groceries | grocery (the rest of the dry goods) | (the gondola itself) | | empty-gondola cover, stocked-gondola goods | yes |
| bread | bread | Loaf of Bread | the bag | loaves | yes |
| oatcakes | oatcakes | Oatcakes | the bag | oatcake packets | yes |
| beans | beans | Tin of Beans | the bag | tins | yes |
| ice | ice | Bag of Ice | the supply locker | empty chest cover, ice bags | yes |
| bait | bait | Capelin | the tackle box | bait box | yes |
| gas | gas | (pump stock) | | none | yes |

---

## 3. Hands, the paper bag, and storage

### 3.1 Where a bought good goes

| Kind of good | Where it goes | Hands involved? |
|---|---|---|
| Groceries and soap (anything a shop would bag) | **the paper bag in his hand**, then put away into his storage | yes, one hand for the bag |
| Ice | the supply locker, as before | no |
| Bait | the tackle box (`TackleBox`, saved `BaitStock`), as before | no |
| Hand tools (rod, shovel, rake, tongs, bucket, basket, sack), slice 3 | into a free hand; refused if both hands are full | yes |
| Instruments (Nine Mile Creek only) | fitted to the boat he is buying for | no |
| Licences | a saved flag | no |

### 3.2 The paper bag

- **The first bagged good** puts a paper bag in a free hand, through the same two-hand carry system that
  holds a pail or a catch. On the mesh figure it hangs from the hand socket like the pail.
- **More bagged goods** go into the same bag, until the bag's **capacity** (data, on the bag's
  `ShoppingBagDef`) is reached. A full bag refuses before any money moves, in his bubble ("The bag's
  full.").
- **Hands full** with things that are not a bag (a pail in one hand, a rod in the other): refused before
  any money moves ("My hands are full.").
- **Putting it away.** Near anything that is his storage (Ginny's freezer, the camper, his stowage rack
  once the house is his) the bag offers "Put the shopping away". The goods then count as his supplies,
  where the stove, the soap and the recipes already look. The bag leaves his hand.
- **Nothing in the bag can be eaten or used** until it is put away: the stove and the soap do not see
  bagged goods. The shopping must be carried home.
- **Storage on a nearby vehicle.** The rule is "anything that is his storage": every storage registered
  in the game's player-storage registry counts. On main today that is Ginny's freezer, the camper and the
  stowage rack. The boat hold is registered without a place in the world (it lives with the region, not
  at the boat), and road vehicles have no storage yet, so neither offers "put away" yet; when either gets
  a place, the bag works with it with no change here.

### 3.3 The bag and the save (no save change)

Nothing a player holds is saved today: a catch in hand is not saved either. The save format is not
changed for the bag. Instead:

- The purchase itself is saved exactly as before (the goods are added to his supplies and the game is
  saved at once, as the counter always did).
- While the bag is in his hand, the goods in it are **set aside**: his supplies do not count them, so the
  stove and soap cannot use them until he puts the bag away.
- If the game is reloaded with the bag still in hand, there is no bag after the load, and the goods are
  found **already put away** in his supplies. Nothing is lost and nothing is paid twice.

**Question for the owner:** should held things (the shopping bag, a fish in hand) be saved, so that a
reload puts the bag back in his hand? That needs a save version change. Until then the rule above
holds.

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
  bought ("Paid, and put by."), into the bag ("Paid. In the bag."), the bag is full ("The bag's full."),
  sold out ("Shelf's bare. Next run, maybe."), too dear ("Can't afford that."), hands full ("My hands are
  full."), put away ("Shopping's put away.").
- **Cost per frame.** None. The label is a cached string rebuilt only when the price, name or sold-out
  state changes; the shelf asks for a fresh quote only on a stock change or on walking in.

---

## 5. The counter catalogue (special orders)

Not built yet. Planned for slice 5, after the save change.

- **Which goods.** Things the shop does not keep on its shelves. A listing is offered for special order by
  data: a new `SpecialOrder` block on the catalogue listing (`SpecialOrderSellers`, `LeadDays`). Proposed
  first set at Leblanc's, all already defined as goods on main: the Rod Rack and the baits Leblanc's does
  not keep on the shelf. Boat hardware is not ordered at Leblanc's (the owner's ruling: it is Nine Mile
  Creek's). Big-ticket items (radar, chartplotter, boats, vehicles) stay with the mainland sellers.
- **Paid on order, no cancellations.** The full price leaves his purse when he orders, through the same
  purchase seam the counter uses today. The order is a saved row (section 6). An order cannot be
  cancelled (the owner's ruling). Only a good removed from the game's data is refunded (the heal refunds
  it on load).
- **The wait is the island's supply run.** The order arrives on the **first supply run day at least
  `LeadDays` after the order day** (`LeadDays` is data, 2 to start: the owner took the lean), using the
  same run calendar the shelves restock on (`IslandRunDays`, daylight runs on the island's real tide). A
  storm week means a late parcel, which is the island's rule for bread too. The due day is worked out when
  he orders and saved with the order, so a later data tweak does not move a parcel already promised.
- **What the catalogue shows.** The clerk's book, on a "Catalogue" tab: each good, its price, and "comes
  on the next run after <day>". After ordering: the order with its due day.
- **Where it is collected.** At the counter: a wrapped parcel appears on the counter top on the due day
  (the same slot-and-sprite pattern as the shelf goods, with a "Collect <good>" label). Pressing it puts
  the good where that kind of good goes (section 3). Several parcels: the chooser. Uncollected parcels wait
  with no limit.

---

## 6. Saved state: save v26, one change

Nothing in slices 1 and 2 is saved beyond what the counter already saved. Draws on the shelves today are
session-only: a reload rebuilds the shelves from the calendar, which is the island as the calendar says.

**v26 is ONE bump, holding two lists.** The first is the island economy's approved
`EconomyLedgerRows` (see `st-peters-living-economy.md` §4.1), which carries the store purchase rows. The
second is the paid special orders awaiting delivery. If the owner says yes to saving held things
(section 3.3), the shopping bag's contents are a third, small list in the same bump.

### 6.1 Row shapes

```
EconomyLedgerRows: List<EconomyLedgerRowDto>
  LedgerId           string   the shop id ("shop.st_peters_general")
  KeyId              string   the shelf line ("line.bread")
  Units              int      items taken
  Day                int      game day of the purchase
  Seconds            double   game seconds of the purchase (same-day ordering)
  SettledThroughDay  int      once a day settles, its rows fold to one per line

SpecialOrders: List<SpecialOrderDto>
  OrderId            string   "order.<seller>.<n>", n from a saved counter, never reused
  SellerId           string   "seller.leblancs"
  ListingId          string   "gear.rod_rack"
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

- **Slice 1:** the book dropped every good sold at the shelf (bread, oatcakes, beans, ice, capelin). The
  rest stayed: the rod, the soap, the depth sounder and the shellfish licence.
- **Slice 3:** soap, the rod and the shellfish gear reach the shelves and racks and leave the book; the
  depth sounder leaves Leblanc's for the Nine Mile Creek boat gear wholesaler.
- **The depth sounder move is not data only.** The store wiring guard holds every listed (ware, seller)
  pair to exactly one vendor in a committed scene, and the St Peters scene has the sounder's counter
  vendor selling as Leblanc's. Changing the offer's seller alone fails that guard both ways (a St Peters
  vendor selling a ware not listed for Leblanc's, and a wholesaler listing with no vendor). The move needs
  the same steps the Loran took: drop the St Peters vendor and its wiring row and builder block, add a
  vendor on the boat gear wholesaler's counter, and give the wiring test that copies the sounder vendor a
  new template. It is left for its own small change.
- **Slice 5:** the book becomes the counter's **catalogue** (special orders) plus the services a counter
  really sells (the licence). Selling to the clerk is unchanged.
- The book is the same presenter; only its rows change. Its rows come from the same builder the shelf
  uses, so a price is never different at the shelf and in the book.

---

## 8. Slice order

| # | Slice | What the owner sees | Save | Needs |
|---|---|---|---|---|
| 1 | **Groceries, ice and capelin at the shelf** (built) | walk to the bread, see its price, buy it; bare gondola and card when sold out; the book is shorter | none | architecture review of one small Core contract |
| 2 | **The paper bag, and every good counted on its own** (built) | bread, oatcakes and beans go into a paper bag in his hand; more go in the same bag; he puts the shopping away at home or the camper; bread can sell out while beans are still there | none (held bag set aside, see 3.3) | the owner's answer on saving held things |
| 3 | **The rest of the St Peters stock** | soap into the bag; the rod rack and the bait box on the floor; rods and shellfish gear into a free hand; the depth sounder moves to Nine Mile Creek | none | art for the rack, bait box and goods; new gear defs for the extra rod tiers |
| 4 | **Save v26** | a bought-out shelf stays bought out after a reload | **v26** (approved) | lead-architect |
| 5 | **The counter catalogue** | order a rod rack, pay now, collect the parcel at the counter on the first run 2 or more days later; no cancelling | v26 rows from slice 4 | catalogue tab, parcel art |
| 6 | **Nine Mile Creek shops at the shelf** | the chandlery rack, the wholesalers' coolers, racks and cases; always stocked for now | none | art; the truck routes from the city come later and give NMC counted stock |
| 7 | **Tidy the book** | empty buy tabs gone | none | |

The demo (the first week, St Peters only) needs slices 1 to 3; 4 and 5 make it hold across reloads and
add ordering.

---

## 9. Questions

Answered by the owner on 2026-11-23 (see the top): separate counts, the rack, the licence at the counter,
no boat hardware at St Peters, the 2-day wait, no cancellations, Nine Mile Creek by truck routes.

Still open:

1. **Save held things?** Should the shopping bag (and a fish in hand) be saved, so a reload puts it back
   in his hand? Needs a save change. Lean: fold it into v26 as a small third list; until then the bag's
   goods are found put away after a reload (section 3.3).
2. **Ice and bait in the bag?** Lean: no. Ice is wet and goes to the locker; bait goes to the tackle box.
   Both are data (`IntoBag` on the slot), so either can change without code.
3. **The extra rod tiers** need new gear definitions (a cheaper first rod and a better rod), with prices
   and art. Lean: two more rods, defined in slice 3.
