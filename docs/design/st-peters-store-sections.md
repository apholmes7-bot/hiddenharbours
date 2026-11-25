# St Peters general store: sections

The owner asked (2026-11-25) for several foods on one shelf, picked with the quick chooser, so the store
has room for the rod and the fishing goods; for the other sections the first week needs; and for every item
to have its own picture.

Standing rules this follows: buy at the shelf (walk up, the price appears, accept; no clerk); every item
runs out on its own; the St Peters store sells basic fishing gear; the first week is St Peters only; special
orders are the counter's catalogue book, not shelves.

## How a section works

- A section is one place on the shelves holding several goods. Walk up and press the use key: the chooser
  opens (the same one used for every other choice: arrow keys or stick to move, the use key or a click to
  buy, number keys to pick a row, Esc or B to put it down).
- Each row says the item, its price and how many are left: `Oatcakes — ₲2 · 5 left`.
- Choosing a row buys one, exactly as a single shelf spot does today: the same money, the same paper bag or
  hand, the same purchase records the save already keeps. Nothing new is saved.
- Each item still has its own stock. A sold-out item reads `Tin of Beans: out (not now)`, is greyed and
  cannot be picked, and its sold-out card still stands on the shelf. Every row, sold out or not, fits the
  chooser's widest line (34 characters with its number and the chooser's reason; a test holds it).
- Sections are data (a list on the store's shelf-slots asset), so adding an item to a section is an asset
  edit, not code. A section holds at most as many items as the chooser shows rows (6 today).

## The room as it is on main (top down, back wall at the top)

```
+------------------------------------------------------------------+
| stairs | back shelf L        | door | back shelf R        | gas  |
|        | painted goods,      |      | painted goods,      | cans |
|        | nothing for sale    |      | nothing for sale    | (1)  |
|                  stove                                           |
|           +----------------+                   +---------+       |
|           | RED GONDOLA    |        ice        | bait box|       |
|           | shelf 1 bread  |       chest       | COUNTER |       |
|           | shelf 2 oatcake|        (2)        | till,   |       |
|           | shelf 3 beans  |                   | book    |       |
|           | shelf 4 beans  |                   +---------+       |
|           +----------------+                                     |
|                          front door                              |
+------------------------------------------------------------------+
(1) gas: a sold-out card only; the jerry cans are sold from the book.
(2) ice: bags of ice in the chest, its own spot.
```

Seven slots on main: groceries cover (the gondola drawn empty), ice, bait box (capelin, on the counter),
gas card, bread, oatcakes, beans. The three foods fill all four gondola shelves, so there is no free place
for anything else.

## The proposed layout, by section

```
+------------------------------------------------------------------+
| stairs | back shelf L        | door | back shelf R        | gas  |
|        | CLAMMING (later)    |      | BOAT MENDING (later)| cans |
|        |                     |      | HOUSEHOLD (later)   |      |
|           +----------------+                   +---------+       |
|           | FOOD           |        ICE        | COUNTER |       |
|           | sh.1 bread+oat |       chest       | special |       |
|           | sh.2 beans     |                   | orders, |       |
|           | FISHING        |                   | licence |       |
|           | sh.3-4 bait,rod|                   +---------+       |
|           +----------------+                                     |
+------------------------------------------------------------------+
```

Prices below come from the game's data unless marked **PROPOSAL** (the owner decides those).

### 1. Food: gondola, top two shelves (this change)

| Item | Price | Picture | First week? |
|---|---|---|---|
| Loaf of bread | ₲4 | own picture (redrawn, top shelf left) | yes |
| Oatcakes | ₲2 | own picture (redrawn, top shelf right) | yes |
| Tin of beans | ₲3 | own picture (redrawn, second shelf) | yes |

One chooser, three rows. Shelves 3 and 4 of the gondola are now empty, for fishing.

### 2. Fishing: gondola, bottom two shelves (the second change)

The owner (2026-11-25): baits for what St Peters' gear catches and what each season's fish take. The store
sells the rod, so the baits are the rod's (hook) baits. What St Peters fish take, read from the fish and
bait data:

| Season | Fish a rod catches here | Baits it takes (data) | Sold here? |
|---|---|---|---|
| all four | Atlantic cod | capelin, sea worm, shucked clam, squid strip | yes, all four |
| high summer, the turn | mackerel | capelin | yes |
| early spring, the turn, hard winter | rainbow smelt | none in the data (jig or bare hook) | n/a |
| high summer, the turn (5 to 9 h) | striped bass | none in the data | n/a |
| early spring, the turn (6 to 18 h) | winter flounder | none in the data | n/a |
| all four | lobster (trap, licence) | herring, mackerel | no: trap bait, waits on the licence |
| all four | rock crab (trap) | fish scrap | no: trap bait |

So the fishing section is four hook baits, each its own listed good with its own picture and shelf line,
each running out on its own: capelin ₲2, sea worm ₲4, squid strip ₲6, shucked clam ₲3 (the book's prices,
unchanged; a bait is bought by the lot, as at the counter). Sea worm, squid strip and shucked clam were in
the book but unlisted; they are now listed for the store and for the Nine Mile Creek fishing wholesaler,
whose counter also gets a vendor for each (the wholesaler list already named them).

The island's bait top-up (10 kg a day) carries all four. Every number below is **PROPOSAL**:

| Shelf line | Share of the bait top-up | Per item | Most on the shelf |
|---|---|---|---|
| capelin (box) | 0.4 (was all of it) | 2.5 kg (unchanged) | 20 (unchanged) |
| sea worm (tub) | 0.2 | 1 kg | 12 |
| squid strip (tray) | 0.2 | 1 kg | 12 |
| shucked clam (tub) | 0.2 | 1 kg | 12 |

The rod: four rods on the bottom shelf at ₲60 (owner: fine). The rod's shelf picture is drawn and ready
(`ShelfGondolaRods`), but the rod has no shelf line on main: the counted rod stock is draft #1554's. When
#1554 lands, it adds one shelf line, `line.fishing_rod` (MaxOnShelf 4, CounterListingIds `gear.rod`), and
the store then needs a slot `slot.rod` at (-0.75, -1.3125) with the rods picture and the slot id added to
`section.fishing`. Rod stats and the fishing controller are another lane's and are not touched here.
Hooks, line and sinkers are not items in the game (the rod is the gear).

Sold-out cards are sized to their slot: a bait that runs out shows a small `OUT` card over its own two tubs
(14 by 8 pixels), and the beans a one-line `SOLD OUT` card over their own band (34 by 8), so a card never
covers the goods beside or above it (the first look: the store's 19 by 15 card on the sea worm hid the tins
above). The bread and oatcakes keep the store's card, which fits their taller places. A test holds that no
two section cards overlap.

### 3. Ice: the chest (unchanged)

Bag of ice, ₲6, own picture, first week.

### 4. Clamming: back shelf left (later)

Clam shovel ₲25, clam bucket ₲15, shellfish rake ₲30, peck basket ₲20, mussel sack ₲10, oyster tongs ₲45.
He starts with a shovel and a bucket, so these are spares and upgrades, not first-day needs. They need the
gear counted on the shelf (the rule draft #1554 adds) and the carry-in-a-free-hand delivery. Each has its
own picture, drawn and ready to bind (see Pictures).

### 5. Boat mending: back shelf right (later, after draft #1508)

Patch plank ₲24, copper nails ₲8, oakum ₲12, linseed paint ₲16: all four prices are **PROPOSAL** from
draft #1508. Each has its own picture, drawn and ready to bind.

### 6. Household: back shelf right, lower (the third change)

Bar of soap ₲3 (in the data, sold from the book today). The owner (2026-11-25): matches, candles and lamp
oil as real items with a use. The third change adds them as supplies, each its own item with a stable id,
listed in Marguerite's book and sold from her counter like the soap, each counted on its own in the supply
locker (no new saved field), each with its own picture:

| Item | Id | Price | Use in the game today |
|---|---|---|---|
| Box of matches | `supply.matches` | ₲1 **PROPOSAL** | none yet: no stove, lamp or fire asks for a light |
| Pack of candles | `supply.candles` | ₲2 **PROPOSAL** | none yet: no placeable or carried light that burns one |
| Tin of lamp oil | `supply.lamp_oil` | ₲5 **PROPOSAL** | none yet: no oil lamp in the game |

What is missing for the uses, on systems already in the game:

- Matches: the cooking stations (`CookingStationDef`, `RecipeRunner`) have no fuel or lighting cost. A
  station field naming a supply to spend when cooking starts (the soap's `SupplyLocker.TrySpend` path)
  would make the stove ask for a match. That is the food lane's call, and it changes cooking for everyone.
- Candles and lamp oil: the lights the player owns are the battery headlamp (no fuel) and the boat lamps
  (switched, no fuel). A house light he lights (a candle on the table, a hurricane lamp on a hook) is a
  `SceneLight` the lighting lane already draws; what is missing is the interactable that lights it, spends
  one candle or a measure of oil, and burns for a set number of game hours (worked out from the clock, not
  saved). That interactable is the open piece.

Shelving them on the back shelf needs a household line in the island's top-up (a new island good) and
slots on the back-shelf picture; until then they sell from the book.

### 7. Clothing

See the clothing draft. The floor in front of the back shelf, left of the door, is kept clear for a
clothes rack.

### 8. The counter (unchanged)

The special-order catalogue book (paid on order, delivered later) and the shellfish licence (₲15). Paper
bags are free: one comes with the first bagged purchase.

## Pictures

Every item sold at the shelf has its own picture, shared with no other item. A content test fails if a
shelf good has no picture or shares one. The shelf pictures are drawn by
`docs/art/shop-shelf-goods/make_shelf_goods.py` on the gondola's 62 x 44 canvas, 32 pixels per metre,
point filtering, in the room's palette. There is no separate hand or bag icon per item: bagged goods show
as the one paper bag, and the baits, rod, rake, tongs, basket and sack already have their own gear icons.

The goods planned but not shelved yet have their own pictures too, drawn by
`docs/art/shop-shelf-goods/make_store_items.py` (bottom-centre pivot, to stand on a shelf, hook or floor
spot) and listed with the id each will bind to in `Data/Shops/StPetersStoreItemPictures.asset`: the rod
(gondola), shovel, bucket, rake, basket, mussel sack, oyster tongs, patch plank, copper nails, oakum,
linseed paint, soap, matches, candles and lamp oil. The same content test holds them to a picture each,
shared with no other good, planned or shelved. The clam shovel and bucket have no gear icon yet; they get
one if they are ever listed for the player to own.
