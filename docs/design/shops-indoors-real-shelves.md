# Shops indoors, real shelves: audit and sliced build plan

> **Status: PROPOSAL, 2026-11-14.** A document only. No code, asset, scene, save format or balance
> value changes in the PR that adds this file. Nothing here is claimed or scheduled until the owner
> answers section 6.
>
> Subordinate to `../vision-and-pillars.md` (canon). Builds on
> [`shop-catalog-and-dialogue-choices.md`](shop-catalog-and-dialogue-choices.md) (the wares book and
> the seller id), [`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) (physical inventory,
> merchants as conversation), ADR 0036 (seamless interiors), ADR 0053 (held props), ADR 0055 (goods
> are carried, proposed) and ADR 0008 (save versioning).
>
> Read at main `e177cfe26e`. Scenes were read by a parser script and by search, never opened in the
> editor. Nothing was run.

## 0. The ask and the reading

The owner's words (2026-11-14): "yes move all stands inside their stores, audit anywhere else needing
to moved inside, fill shelving with real game items that can be replensihed, real inventory, that
players can hold pickup, they walk to cash with item wanting to be purchased, if trying to leave the
shopkeeper does an exclamantion point explaining they need to pay, perhaps some places offer credit in
the future if a customer has sales at x amount."

The reading this plan works to:

- Every shop counter that stands outside its own building moves inside it.
- Shelves hold real items, with real counts that go down when bought and come back on a delivery.
- The player picks an item up, carries it to the till and pays there.
- Walking out with an unpaid item is **not** a crime system. The shopkeeper shows an exclamation
  mark and says you need to pay. That is all.
- Credit is a later idea. This plan leaves one data hook for it and builds nothing.

**In one paragraph.** There are 16 places on main where money changes hands: 9 in the live regions
and 7 in two old test scenes. **None of the 16 is inside a room the player can walk into.** Only one
shop building has a room at all (the island general store, 8 x 10 m), and its counter stands on the
square about 9 m from the door. Shop stock today is an endless price list: no counts, no restock,
nothing saved per shop. Most of what the feature needs for the player's side already exists: two
hands, pick up and put down, held items drawn in the hand, a counter that buys only what is carried
to it, an exclamation mark, and a room that knows when the player is inside. The missing parts are
shop stock as data, shelves, a "not paid for yet" tag, the till, and the doorway check.

## 1. Audit: every place the player buys or sells today

"Indoors" means inside a room the player can walk into. "UI" names: **book** = the seller's wares
book opened from a conversation row (`CatalogBookPresenter`); **sell row** = a conversation row that
sells the carried pail (`CounterSellDesk`). All owning classes are in the Economy module unless said.

### 1.1 Live regions

| # | Place (scene object) | Region, position | Seller id | Goods | Owning classes | UI | Counter is | Building has a room? |
|---|---|---|---|---|---|---|---|---|
| 1 | General store counter (`GeneralStoreCounter`) | St Peters (1.9, 26.4) | `seller.leblancs` | Buys: rod, capelin bait, ice, depth sounder, clam licence (shellfish tools are authored but not yet listed). Sells: the carried pail (clams, fish) into market 2 | `GearShop`, `BaitShop`, `SupplyShop`, `InstrumentShop`, `LicenseVendor`; `WharfSellPoint` + `FishBuyer` + `Market` | book + sell row, keeper Marguerite | **Outdoors**, on the square | **Yes**: `IslandShops/generalStore` at (2.5, 35.4), 8 x 10 m painted room, no furniture objects |
| 2 | Camper door (`Camper (Clipper 26)`) | St Peters (84, 42) | none | The camper home, one purchase | `HomeDoor`, `HomeDef` | prompt at the door | Outdoors (it is the door) | No (shell only) |
| 3 | Old general store (`GeneralStore`) | Nine Mile Creek (-206, 116) | `seller.nmc_chandlery` | Rod | `GearShop` | book, anchored storekeeper | **Outdoors** | No interior component in the scene. Due to retire when Water Street lands |
| 4 | Wharf market counter (`NmcWharfMarketCounter`) | Nine Mile Creek (92.3, 124.2) | `seller.nmc_wharf_market` | Sells: the carried pail into market 1 | `WharfSellPoint`, `FishBuyer`, `Market`, `CarriedHold` | sell row, Wendell | **Outdoors** | No |
| 5 | Wharf office (`NmcWharfOfficeVendor`) | Nine Mile Creek (83.6, 112.3) | `seller.nmc_harbourmaster` | Cod licence | `LicenseVendor` | book | **Outdoors** | No |
| 6 | Boat yard (`NmcWharfYardCatalogue`, `NmcWharfYardDoryTrade`) | Nine Mile Creek (197.8, 123.9) | `seller.nmc_yard` | Damaged dory, punt, repairs, lobster pot, crab pot | `Shipwright`, `PotShop` | book | Outdoors | No |
| 7 | Used outboard (`NmcWharfYardOutboardTrade`) | Nine Mile Creek (198.0, 88.5) | `seller.hectors_barrel` | Dory outboard | `Shipwright` | book, Hector | Outdoors | No |
| 8 | Wharf pumps (`station.nmc_wharf_pumps`) | Nine Mile Creek, wharf | none | Fuel by the litre, four grades | `FuelPump`, `FuelStationDef`, `FuelPricing` | prompt at the hose, pay at the pump | Outdoors | n/a |
| 9 | Route 91 station (`station.route_91`) | Nine Mile Creek, road | none | Fuel by the litre, five grades | same | same | Outdoors | No (the new gas bar with a store is in an open draft PR) |

Rice Point, Cumberland, East Water and South Water: **no buy or sell point** in the scene.

### 1.2 Old test scenes (in the build list, not part of the campaign as far as read)

| # | Place | Scene, position | Goods | Classes | Counter is |
|---|---|---|---|---|---|
| 10 | `FishBuyerStall` | Greywick (-8, -3) | sells catch, market 0 | `WharfSellPoint`, `FishBuyer`, `Market` | Outdoors |
| 11 | `GeneralStore` | Greywick (-12, -9) | rod | `GearShop` | Outdoors |
| 12 | `HarbourmasterOffice` | Greywick (-12, 9) | licence | `LicenseVendor` | Outdoors |
| 13 | `ShipwrightDoryYard` | Greywick (-8, 8) | dory | `Shipwright` | Outdoors |
| 14 | `ShipwrightShed` | Greywick (-8, 3) | punt, pots | `Shipwright`, `PotShop` | Outdoors |
| 15 | `Wharf` logic object | Greybox | sells catch | `WharfSellPoint`, `FishBuyer`, `Market` | no building |
| 16 | `Shipwright` logic object | Greybox | boat | `Shipwright` | no building |

**Count: 16 places, 16 outdoors (9 of 9 in the live regions).** Recommendation for rows 10 to 16:
leave them alone. They are test fixtures; moving them buys the player nothing.

### 1.3 What moves inside, what stays out

"Clear" = the owner's words plainly cover it (a shop's stand outside its own shop). "Judgement" = by
the same logic, the plan's call.

| Place | Clear or judgement | Recommendation |
|---|---|---|
| St Peters general store counter, keeper and till | **Clear** | **Move inside. This is the pilot** (section 5, slice 1). The room exists and the keeper already has a home station inside. |
| Nine Mile Creek chandlery (rod) | **Clear** | Do not move the old `GeneralStore`. Build the chandlery counter indoors from the start when Water Street is placed, and retire the old root then. |
| St Peters post office | Judgement | Room exists (7 x 8.5 m) and is empty. Give it a counter and fixtures when it gets a service to offer. No trade there today, so not in this plan's first slices. |
| St Peters bait store (wharf end) | Judgement | Today a solid block with props outside; bait is sold at the general store. Bait belongs here once the shed is opened as a walk-in (the interiors audit's slice 4). Until then bait stays on the general store's shelves. |
| Nine Mile Creek wharf office (licence) | Judgement | Move inside when the office trailer gets a room. A licence is paper over a desk; it is never a shelf item. |
| Nine Mile Creek wharf market (Wendell, catch sale) | Judgement | **Stays at the wharf, outdoors or under an open front.** A buyer's scale belongs where the fish lands; carrying a dripping pail through a shop is wrong for the place. |
| Boat yard, used outboard | Judgement | **Stay outdoors.** Boats and engines are sold where they stand. A yard office with a desk can come with the yard's own interior. |
| Fuel pumps (both stations) | Judgement | **Pumps stay outdoors, pay at the pump stays.** The new gas bar's store can later carry shelf goods (cans, ice) under this plan; that is a rollout row, not a fuel change. |
| Camper door | Judgement | Stays. It is a house sale at the house. |
| Water Street grocery, restaurant, bar; plaza grocery and lumber; fish plant office; cannery hiring desk; a bank | Judgement | **Not built yet.** Rule for all of them: the counter is authored indoors from day one. The grocery and restaurants exist only as supply-chain data (`FishOutletDef`, with real storage Defs) that non-player trucks deliver to; no player trade. |

### 1.4 What shop stock is today

- **An endless price list.** A listing is an offer asset (`GearOffer`, `BaitDef`, `SupplyDef`,
  `InstrumentOffer`, `LicenseDef`, `ShipwrightOffer`, `PotOffer`) with an id, a price and a
  `CatalogListing` tag naming the sellers that stock it. `CatalogSource` sweeps them on every open.
  There is **no quantity, no shelf, no restock, and no per-shop state**.
- **The only limits are on the player's side:** one-off gear and licences refuse a second purchase;
  bait and ice add to counts the player owns.
- **Saved:** money, owned boats, licences and gear, the player's bait and supply counts. No field
  describes a shop. Schema version was 17 when last reviewed.
- **Fish selling** has a per-market supply figure that sags when you sell and recovers each day. It
  lives in memory only; a proposal to save it is with lead-architect
  ([`market-supply-persistence.md`](market-supply-persistence.md)).
- **Deterministic:** prices are authored values. Nothing in a shop rolls dice.

### 1.5 What already exists to reuse

| Need | Already on main | Gap |
|---|---|---|
| Two hands, pick up, put down | `CarryHands` (Player) over Core `HandSlots`, `ICarriable`, `ICarrier`; full hands refuse with a reason | none for the carry itself |
| Items that can be held | `CarriableTool` (rod, shovel), `CarriableBucket`, `CarriableFuelContainer`, `CarriableCatch` | no carriable form for bait, ice, pots or instruments |
| Item drawn in the hand | ADR 0053 held props; `ToolDef.HeldMeshFor` picks the mesh variant from data (#1199) | shelf goods without a held prop need one, or a generic parcel |
| A counter that takes only what you carried | `CarriedHold` + `CounterSellDesk` (the carried counter, #1096): the sale reads the pail in your hands, never the boat | this is the sell side; the buy side has no equal |
| Seller id, wares book, conversation hand-off | `CatalogSource`, `CatalogBookPresenter`, `CatalogViewRequested` / `CatalogClosed`, re-arming picker | the book buys by row, not by what you hold |
| Reach gate at a counter | `StallReach`, `StallGate` | reuse for the till |
| Exclamation mark over a figure | `BigOneReactionPresenter` (Player): a placeholder "!" built in code, timed from `GameConfig`, triggered by a Core cue | it lives in Player and marks the player only; a shopkeeper needs the same mark from a shared place |
| Speech bubble with no portrait | the dialogue bubble and ambient speech signals | none |
| Knowing the player is inside a building | `BuildingInterior.IsInside`, a pure footprint test every frame, self-healing after travel or load; the doorway is a gap in the walls | no event is published on enter or leave; there is no door object |
| Shopkeeper hours | `RoutineDef`: Marguerite is at `store_counter` 07:00 to 13:00 and 14:30 to 19:00, home in the store at 21:00 | Nine Mile Creek has no routine engine; its clerks are anchored and never close |
| Interiors at Nine Mile Creek | none in the scene on main | every creek shop needs a room before it can have shelves |
| Co-op | **not on main.** The trial is an open draft. Its plan: host's world, one host save, shared purse, separate hands, clients send intent and the host validates; "new physical goods appear at a reachable hand or pickup point" | design for it now (section 3.4), build after it lands |

## 2. Design

### 2.1 Data model

All new content is Def assets, one per file, ids append-only. Shapes are for lead-architect to
settle; the fields are what the loop needs.

- **`ShopDef`** (Economy, `shop.st_peters_general`). The seller id it belongs to; the premises id;
  a list of **stock lines**; a credit block that is empty for now (section 2.5).
- **Stock line** (rows on the `ShopDef`). The listing it sells (an existing offer id, so price and
  ownership rules stay where they are); the fixture and slot range it is displayed in; `MaxOnShelf`;
  `UnitsPerItem` (one tub of bait is N baits, so a consumable is one object in the hand); the
  **restock rule**: delivery weekdays, units per delivery, and a floor the line never drops below.
- **`ShelfFixtureDef`** (`fixture.shop_shelf_low_01`). The art key, the footprint and collider, and
  a list of **slot anchors** (local position, facing, size class). A fixture is furniture: it sits
  under the room's furniture group, so it is hidden with the room when the player is outside.
- **Premises** (scene data, written by the region's patcher, never by hand in code). Which
  `ShopDef`; the placed fixtures; the **till anchor**; the **keeper post** (a routine station id,
  not a second position); the **exit volume**, which is the room's own footprint, not a new shape.
- **`GameConfig`**: reach at the till, how long the mark shows, the default restock hour. No number
  lives in code.

**Stock: saved counts, restocked by a rule that is a pure function. Recommended, and why.**

A shelf count cannot be recomputed from `(worldSeed, gameTime)` alone, because it depends on what the
player bought. Deliveries can. So:

- The **delivery schedule** is derived, never saved: which days a line is restocked comes from the
  Def and the day index (with the world seed for any jitter).
- The **count** is saved, sparsely: one small record per line that is below full (`shopId`,
  `lineId`, `count`, `settledThroughDay`). A full shelf saves nothing.
- Restock is **settled on read**, on the slow tick at day start and on load: add the deliveries
  between `settledThroughDay` and today, clamp to max. Ten days away costs the same as one. This is
  the same shape as the market supply proposal and the freshness rule.

A fully derived shelf ("always full at dawn") would need no save change, and slices 2 to 6 use
exactly that so nothing waits on the schema. It is not the end state: it makes "sold out until the
boat comes Thursday" impossible, and that is the part of the ask that gives a shop a pulse (P3).

The save change is one new list and a version bump. **Lead-architect owns it** (ADR 0008), with a
tested migration.

### 2.2 The loop

1. **Walk in.** Shelves show what is in stock: one sprite per filled slot.
2. **Pick up.** The ordinary pick-up verb on a filled slot. The slot empties on screen and the item
   goes to a free hand, tagged **unpaid**. The saved count does **not** change yet.
3. **Carry limit.** Two hands, the existing rule. A long item (the rod) may take both, as its
   carriable already says. Full hands refuse with the existing refusal line.
4. **Put back.** The put-down verb anywhere in the shop returns the item to its own slot. An unpaid
   item is never left loose on the floor.
5. **Till.** At the till, with the keeper at their post, a conversation row "I'll take these" opens
   a short **receipt** in the wares book's hand: each held unpaid item, its price, the total, the
   purse after. For a consumable the receipt offers "the same again" up to what the shelf holds, so
   ten tubs of bait is not ten trips. Confirm pays once, through the existing `TryBuy` seams, and
   the saved count drops by what was bought. The items lose the unpaid tag and are yours.
6. **Sell to the shop.** Unchanged, same till: the sell row sells the carried pail (the carried
   counter). Buying and selling in one conversation already works.
7. **Things with no shelf form** (licence, boat, engine, instrument fitted to a hull, fuel) stay as
   they are: asked for at the counter, read in the wares book, or pumped at the pump.

**What happens to the screens.** The wares book stays. For shelf goods it stops being the way to
buy and becomes the receipt at the till; for counter-only goods it is unchanged. `SellScreen` and
bulk fish and fuel are untouched by this plan.

An unpaid item **cannot be used, stowed in a container, handed to a vehicle or boat, or sold**. It
is in your hand or on its shelf, nowhere else.

### 2.3 Leaving unpaid: the item goes back to its shelf, the player is never stopped

**One behaviour:** the moment a player holding an unpaid item leaves the shop's footprint, the item
returns to its shelf slot, the hand is empty, the shopkeeper shows the exclamation mark and says a
line ("You'll want to pay for that first, dear."). The player keeps walking. Nothing is charged,
nothing is remembered, no reputation moves.

Why this one and not "turn the player back": a wall that appears only sometimes is where soft-locks
come from (keeper gone home, a friend in the doorway, a load that drops you on the wrong side). An
item that can never exist outside the room cannot be stolen, duplicated or lost, and the player can
never be trapped.

| Case | What happens |
|---|---|
| Shop closes with the player inside | The keeper leaves on the routine. The doorway is a gap and stays open. No keeper at the till means no sale; the player puts the item back or walks out and it returns itself. A quiet inner-voice line says nobody is at the till. |
| Keeper off shift or at lunch | Same. Browsing and picking up still work; paying waits for the keeper. |
| Save or load while holding an unpaid item | Unpaid is never saved. The saved count never dropped, so after a load the item is on its shelf and the hand is empty. No money moved. |
| Putting an unpaid item down | It returns to its slot. It cannot be dropped through the doorway. |
| Player already owns a one-off item (the rod) | The slot refuses the pick-up with the existing "already yours" line. |
| Cannot afford at the till | The receipt says how much short, as the book does today. The player puts something back. |
| Region travel, sleep, cutscene, boarding a vehicle | All of these leave the footprint. Same rule: the item returns. |
| The player's own goods carried into the shop | Never tagged. Only an item taken from this shop's shelf this visit is unpaid. |
| Co-op guest holding an unpaid item | The tag is per player. The same exit rule runs for whoever holds it; a disconnect returns it. |

Edge that must be tested, not assumed: the inside test has a small slack at the doorway so the room
does not flicker. The exit check uses the same test, so "left" means the same thing to the picture
and to the rule.

### 2.4 Module boundaries and co-op

- **Core** gains the contracts: a read-only shop stock view, the unpaid tag on a carried item, and
  signals for item taken, item returned, sale completed and unpaid exit. Events report what
  happened; the stock itself is state in Economy.
- **Economy** owns `ShopDef`, stock, restock and the till. It never writes a line of dialogue.
- **Player / gameplay-systems** own the carriables and the hands.
- **World** owns the premises, fixtures, the keeper and what the keeper says and shows. It hears
  "unpaid exit" on the bus and plays the mark and the line.
- The exclamation mark moves from a Player-only presenter to a shared one any figure can show.
- **Co-op (after the trial lands):** the host owns shelf counts and the till; a client sends "take
  slot", "put back", "pay"; each request carries who and a retry key, so a retried pay cannot
  charge twice. The shared purse pays.

### 2.5 The credit hook (not built)

A `ShopDef` carries an empty credit block: an on/off flag, a minimum **lifetime sales to this
seller** (the "sales at x amount"), and a ceiling. The only thing it would read is a per-seller
running total of what the player has sold there, which the sale event already reports and nothing
yet sums. No ledger, no debt, no interest, no code in this plan.

### 2.6 Performance

- One sprite renderer per slot, created once per fixture and switched on or off. No per-frame work:
  slots redraw only when a stock signal arrives.
- All shelf sprites for a shop come from one sheet and one material, so a full shop is a handful of
  batches. A pilot shop is about 40 to 60 slots.
- Shelves live under the room's furniture group, so they cost nothing while the player is outside.
- Only an item in a hand becomes a real carriable (pooled). The shelf never holds live objects.
- Restock runs on the slow tick, once a day per line, with no allocation.
- Nothing here is beyond a phone: no new shader, no new light, no physics.

## 3. Pillars and phase

Serves **P3** (a shop is a room with a person and a delivery day), **P4** (hand-carry now; a hired
hand restocks or shops for you later) and **P5** (a kind word at the door, not a penalty).

**Phase, stated plainly.** By the roadmap this is **M2 work**: "shops" and a real town are M2's
promise, and physical inventory is scheduled M2 to M3 in the diegetic UI plan and in ADR 0055's
phasing. The project is still closing M1. **This is ahead of phase.** The owner has asked for it
anyway; the plan keeps the cost down by piloting in one shop and reusing what M1 already built.

## 4. Sliced plan

"Headless" = no editor needed; the CI run is the proof. "Editor" = one editor run on a granted slot.
St Peters cannot be rebuilt: every scene change there goes through the text patcher or hand-written
scene text, never an editor save.

| # | Slice (what the owner sees) | Role | Areas | Editor? | Proof (tests named by behaviour) | Depends on | Risk |
|---|---|---|---|---|---|---|---|
| 1 | **The island general store's counter, till and keeper are inside the store.** You walk in and Marguerite is behind the counter; the book and the clam sale open there; nothing stands on the square. | world-content | village patcher (it already owns the counter and the stations), the `store_counter` station, loop and arrival tests that name the old spot | One patch run (dry run and diff first), or hand-written scene text | the keeper stands behind the indoor counter in open hours; the book opens at the indoor counter; a carried pail sells at the indoor counter; no vendor is left on the square; the opening quests still reach the store | #1198 (walk every island building on the real scene) merged first, as the safety net | Low to medium. Quest markers, routine paths or the opening may point at the old spot. What the shop room sheet shows was not checked. |
| 2 | Nothing yet: shop stock exists as data and rules. | economy-sim, lead-architect for the Core contracts | `ShopDef`, stock lines, restock rule (pure), Core read contract and signals, content validation | Headless | restock gives the same shelf for the same seed and day; a shelf never exceeds its maximum or drops below its floor; settling ten days at once equals ten single days; every stock line names a listing that seller really stocks and a slot that exists | none | Low |
| 3 | **Shelves in the pilot store, filled with the real goods, to look at.** | world-content, art-pipeline | `ShelfFixtureDef`, fixture art and shelf sprites, premises patch | Editor (patch run, one plate) | filled slots equal stock; shelves are hidden when the player is outside; drawing a full shop allocates nothing per frame | 1, 2; shelf and item art | Medium: needs art for fixtures and for goods with no sprite yet (bait tub, ice bag) |
| 4 | **You can take an item off the shelf and put it back.** | gameplay-systems, economy-sim | the unpaid tag, shelf pick-up and put-back, carriable forms for consumables | Headless (PlayMode in CI) | taking an item empties its slot and fills a hand; full hands refuse; putting it down anywhere in the shop returns it to its slot; an unpaid item cannot be used, stowed or sold; an owned one-off cannot be taken | 2, 3 | Medium: touches the hands, which every carry test leans on |
| 5 | **You pay at the till for what you are holding.** | economy-sim, ui-ux | till row, the receipt leaf in the wares book's hand, "the same again" for consumables | Headless; one plate for the receipt | money and ownership move exactly once; the result equals the same purchase made through the book; too dear says how much short; no keeper at the till means no sale | 4 | Medium |
| 6 | **Walk out without paying: the item is back on the shelf and the keeper calls after you with a "!".** | gameplay-systems (exit), world-content (line, mark on the keeper), lead-architect (shared mark) | exit check on the room footprint, shared exclamation presenter, the keeper's line as authored data | Headless; one plate | leaving with an unpaid item returns it and changes no count; the player is never stopped; the mark shows for the configured time; a load while holding unpaid leaves it on the shelf; with the keeper away the item still returns | 4 | Low to medium |
| 7 | **Shelves can sell out and refill on delivery day, and it survives a save.** | lead-architect (save), economy-sim | save schema bump and migration, sparse stock records, day-start settle | Headless | an old save loads with full shelves; a bought-out line stays empty after save and load; deliveries missed while away arrive on load; buying is not undone by reloading | 5; lead-architect sign-off | Medium: save format |
| 8 | The book no longer sells shelf goods at the pilot shop; counter-only goods (licence, sounder) still do. | ui-ux, economy-sim | catalog rows marked "on the shelf", validation | Headless | a shelf good cannot be bought two ways; every listed good is reachable one way | 5 | Low |
| 9 | **The Nine Mile Creek chandlery opens indoors with shelves; the old store root is gone.** Then the gas bar store and the St Peters bait shed as they get rooms. | world-content | Water Street placement, creek premises, the rod leg of the loop test | Editor | you buy the rod from a shelf in the chandlery; no old store root in the scene | 2 to 6; Water Street's kits and placement; a room system at the creek; the open gas bar draft | High: three open dependencies, none in this plan's hands |
| 10 | In co-op, both players shop from the same shelves. Credit stays a data hook. | lead-architect, economy-sim | host-validated take, put back, pay | Headless plus a two-machine check | two players cannot take the same item; a retried pay charges once; a guest leaving returns their unpaid item | the co-op trial on main | Medium |

**Order and cost.** 1 can start now and is the only slice the owner needs to see before deciding on
the rest. 2 runs beside it. 3 to 6 are the feature; 7 makes stock real; 8 tidies; 9 and 10 wait on
other work. Roughly ten small PRs, two of them needing art.

**Why the island general store is the pilot.** It is the only shop on main with a room. Its keeper
already keeps hours and already has a station inside. Its patcher already owns the counter. It sells
the widest mix (a tool, bait, ice, an instrument, a licence, and it buys clams), so one shop proves
shelf goods, counter-only goods and selling. And it is the first shop a new player meets.

## 5. Not checked

- Nothing was run; no test result was read. Every "works today" is from code and committed tests.
- What the two shop room sheets actually show (fixtures may be painted into the sheet).
- World positions of the fuel hoses (nested objects; the scan did not resolve them).
- Whether the two old test scenes can still be reached in play.
- Whether what is in the player's hands is saved today.
- The open drafts (gas bar, co-op trial, the walk-in test) were read by title only.
- Whether the village patcher runs clean on today's scene.
- The four Water Street kit files are not on main; the chandlery's room was not read.

## 6. Questions for the owner

1. **Is the island general store the right first shop?** Recommended: **yes.** It is the only shop
   with a room today and the first one a player meets.
2. **If you walk out without paying, the item simply goes back on its shelf and the shopkeeper calls
   after you with a "!". You are never blocked at the door. Right?** Recommended: **yes.** It cannot
   trap you and nothing can be taken.
3. **Fish buyers, boat yards and fuel pumps stay outdoors; licences stay a chat at the desk.
   Agreed?** Recommended: **yes.** A scale on the wharf and a pump on the forecourt are right where
   they are. Clams still sell at the store's indoor till.
4. **Can a shelf really sell out until the next delivery?** Recommended: **yes, gently.** Deliveries
   on set days, and the basics (bait, ice) never drop to nothing, so a new player is never stuck.
5. **This is work the roadmap puts in the next milestone. Start now anyway, one shop first?**
   Recommended: **yes, slice 1 now**, then decide on the rest after you have walked into the store.
