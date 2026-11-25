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
- Each item still has its own stock. A sold-out item reads `Tin of Beans — sold out`, is greyed and cannot
  be picked, and its sold-out card still stands on the shelf.
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

### 2. Fishing: gondola, bottom two shelves (next change)

| Item | Price | Picture today | First week? |
|---|---|---|---|
| Capelin (bait) | ₲2 each | yes (bait box on the counter; moves here, redrawn) | yes |
| Herring (bait) | ₲3 | none, to draw | yes |
| Mackerel (bait) | ₲4 | none, to draw | yes |
| Sea worm (bait) | ₲4 | none, to draw | yes |
| Squid strip (bait) | ₲6 | none, to draw | later in the week |
| Fishing rod | ₲60 | rod art exists for the hand, none for the shelf | yes, once the counted rod stock (draft #1554) lands |

How many of each bait sit on the shelf, and their share of the store's bait delivery, are new numbers:
**PROPOSAL**. Shucked clam and fish scrap stay in the book: the player can make both himself, and the
chooser holds six rows. Hooks, line and sinkers are not items in the game (the rod is the gear), so they
are not proposed for the first week.

### 3. Ice: the chest (unchanged)

Bag of ice, ₲6, own picture, first week.

### 4. Clamming: back shelf left (later)

Clam shovel ₲25, clam bucket ₲15, shellfish rake ₲30, peck basket ₲20, mussel sack ₲10, oyster tongs ₲45.
He starts with a shovel and a bucket, so these are spares and upgrades, not first-day needs. They need the
gear counted on the shelf (the rule draft #1554 adds) and the carry-in-a-free-hand delivery. No shelf
pictures today: each needs one.

### 5. Boat mending: back shelf right (later, after draft #1508)

Patch plank ₲24, copper nails ₲8, oakum ₲12, linseed paint ₲16: all four prices are **PROPOSAL** from
draft #1508. No pictures today; #1508 lists the canvas and card each needs.

### 6. Household: back shelf right, lower (later)

Bar of soap ₲3 (in the data, sold from the book today). Matches, candles and lamp oil are not items in the
game. The headlamp is starting gear and has no batteries, so nothing to sell for it. Each new item would
need the owner's say and a picture.

### 7. Clothing (later, owner's taste)

Oilskins exist as an outfit, not as something sold. Whether the store sells clothes, and where, is the
owner's call.

### 8. The counter (unchanged)

The special-order catalogue book (paid on order, delivered later) and the shellfish licence (₲15). Paper
bags are free: one comes with the first bagged purchase.

## Pictures

Every item sold at the shelf has its own picture, shared with no other item. A content test fails if a
shelf good has no picture or shares one. The shelf pictures are drawn by
`docs/art/shop-shelf-goods/make_shelf_goods.py` on the gondola's 62 x 44 canvas, 32 pixels per metre,
point filtering, in the room's palette. There is no separate hand or bag icon per item: bagged goods show
as the one paper bag.
