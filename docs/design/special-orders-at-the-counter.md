# Special orders at the store counter

The counter keeps a book of things the store does not have on its shelves. The player chooses an
item and an amount, sees the full price and delivery promise, and pays when he places the order.
After the delivery reaches the store, he returns to the counter and carries his goods away in his
hands or paper bag. An order is a reason to come back to a working place. It is never a remote
purchase or a delivery into his pocket.

Status: interaction and delivery design, 2026-11-23. No screen, scene or artwork is changed by this
page. The [companion logic proposal](https://github.com/apholmes7-bot/hiddenharbours/pull/1503) supplies
session-only payment, day-based arrival, collection and snapshots. The first week and demo stay on
St Peters: the player stays with Ginny, earns and repairs Basil's broken dory at the island slip, and
an islander's word opens the way to Nine Mile Creek around day seven. Weather closes the other
regions. Special orders must not require leaving the island to complete that week.

## At the counter, in the book's own hand

Use the established [Notebook and counter-book language](shop-catalog-and-dialogue-choices.md):
the seller's catalogue is a book at the counter, distinct from the player's personal Notebook.
The left leaf lists orderable goods; the right leaf gives the selected good, quantity, full price,
delivery promise and outstanding limit. Use the existing type, spacing, ink, rules and focus marks.
This calls for no new skin, portrait, clerk conversation tree or nested dialogue choices.

The counter interaction opens the catalogue directly. A short bubble from the nearby keeper may
acknowledge it: "I can bring these in." The person remains in the room; the speech comes from that
person. Closing the book returns to standing at the counter. Buying something already stocked
continues to happen at its shelf, with each item running out separately.

Two plain leaves within the book are enough: **Order goods** and **Your orders**. A row states what
will happen in words; colour is supplementary. Reuse the existing book's pagination and confirm/back
intents. Only an explicit confirmation of the displayed total spends money. Moving between rows,
changing quantity and closing the book do not.

| Entry | What the player reads | Action |
|---|---|---|
| Orderable | "Herring - 3 coins each. Six cost 18. Expected Thursday. Pay when ordering." | "Order six for 18" |
| Paid, travelling | "Six herring. Paid 18. Expected Thursday." | Read; no second payment |
| Arrived | "Six herring. Paid. Here at the counter." | "Collect six" |
| Collected | A crossed-through personal receipt: "Collected Thursday." | No collection action |

The dates above illustrate the wording, not a fixed Thursday delivery schedule. Display calendar
days from the game's clock; do not invent an hour countdown or require the player to own a watch.
The book should say "two days" alongside the calendar date when that helps a new player. A zero-based
day index is never player-facing. A promise from day one for two days later means day three.

## Walking up, ordering, and carrying away

1. The player walks into the store and approaches the counter. He can read the catalogue without
   buying anything. His hands may be occupied while ordering; the goods are not here yet.
2. He selects a line. Its existing item name and description explain what it is. He chooses a
   quantity of individual items, with the unit price and total always together. The limit counts
   all his units of that line still on order, including goods already waiting for collection.
3. The confirmation repeats the item, quantity, total, arrival promise and "paid now". The wallet
   decides whether he can pay at this moment. A failed payment leaves his money and orders unchanged.
4. On success the page records a paid order and a small numbered receipt. Two orders of the same
   line remain distinct receipts, even if placed on the same day. The personal Notebook may copy
   this receipt as something he now knows; it must not become a remotely actionable shop catalogue.
5. He closes the book and gets on with the island day. Sleeping advances delivery through the same
   calendar as waiting. Neither leaving the store nor opening the book again charges him again.
6. When the order is here, he returns to the same counter and selects its receipt under Your orders.
   The quantity and "Paid" are visible; the collection action carries no price.
7. The goods are brought onto the counter and handed to him. The carrying integration checks the
   actual free hand and bag capacity for the entire selected order. A free bag slot counts only if
   that bag can receive these goods. A full bag may still permit a suitable free hand, and vice versa.
8. Successful handover marks that receipt collected and removes it from the open-order book. He
   walks out holding the goods or their filled bag. He carries them to wherever he wants them and
   unloads by hand. Nothing goes directly into his boat, home, storage or ownership list.

For the first interaction slice, collection is all-or-nothing per receipt. If the quantity will not
fit, every unit remains at the counter and the receipt stays paid. He can put down what he is holding,
make room in his bag, or return with suitable carrying space. Smaller separate orders let him plan
separate trips; partial collection needs a later explicit rule for remaining quantities and receipts.

Refusals are brief local speech or a note beside the selected entry. They do not open a new modal:

| Situation | Suggested words |
|---|---|
| Cannot afford the whole order | "I haven't enough for that." |
| Line no longer offered at this counter | "I can't order that here." |
| Outstanding limit reached | "I've still got some coming. Best collect those first." |
| Not yet arrived | "It hasn't come in yet." |
| Hands and bag cannot receive the order | "I need room to carry this." |
| Already collected or stale selection | "I've already taken that one." |

The presenter rebuilds a stale selection rather than treating it as a fresh purchase. Quantity
controls admit positive whole items only. Technical configuration failures are not dialogue content.

## Finding out that an order is here

An arrival event changes the store's order record. It does not interrupt fishing, open the Notebook,
play a global notification or add a HUD marker. On the player's next visit, the keeper can say
"Your order came in." once in an ordinary speech bubble when he is close enough to hear. With several
arrivals, one line acknowledges them together. The counter book always lists exactly which receipts
are ready, so missing the bubble never loses access to the goods.

Once he hears the news or reads the counter book, his own receipt can say "At the store". Until then
the personal Notebook keeps the expected date he was told: it does not remotely discover a delivery.
Later world work may show the labelled packages behind the counter and the runner unloading them.
That is a requirement for the physical delivery slice, not permission to add new pictures here.

An arrived order he never collects remains his, paid and reserved at that counter. It is not sold to
another customer, swept into ordinary shelf stock, charged again, or silently removed at day seven.
It keeps using the line's outstanding allowance. No expiry, storage fee or automatic refund is
proposed for the first version. The player can check it on any later visit.

The six example lines are bait, which makes storage a real question. The lean is that the store keeps
paid bait properly until handover, with no surprise deterioration bill. The first visible slice must
show that storage and use the existing bait rules; the order book itself does not add a second
freshness simulation. If suitable storage cannot be represented, keep the perishable catalogue out
of the visible build until that integration is settled.

## Which supply system carries it

This plan follows [Island supply](island-supply.md), especially its carriers, recommended run plan,
back-room ledger and shelves sections, and [St Peters: a living island economy](st-peters-living-economy.md),
especially sections 1.5, 3, 4 and 5. Those pages identify a **quad with racks and a trailer over the
bar**, with a mainland pickup on the far side and a separate foot errand role. They do not establish
a scheduled supply ferry. The fish-plant truck flow is for landed fish going out; it should not be
quietly repurposed to refill the shop. The store's special orders should travel on the existing
inbound supply run, alongside its normal goods.

Use `supplyrole.quad_runner` and its authored cadence and carrying kit. `IslandRunDays.Possible` and
`IslandRunCalendar.RunsOn` already determine feasible tide windows and scheduled or made-up runs.
`IslandStoreLedger` models the back room and `IslandStoreShelves` models public shelf quantities.
Special orders remain a separate list of prepaid customer goods; they are not shelf purchases or
ordinary household demand. For bait, carry them as a reserved portion of the bait load. Count their
weight, volume and packaging against the real carrier, without counting the same load twice.

The delivery integration should follow these steps:

1. Treat the line's whole-day wait as its procurement lead time. At placement, find the next feasible
   supply run after that lead time and reserve room for the parcel before quoting a delivery date.
   Normal island essentials retain their existing capacity allowance. A limit on customer quantity
   alone does not prove the trailer has room for it.
2. Account for loading, travel and unloading. If the run returns after the counter closes, quote the
   next opening day for collection. Do not say "Here" at midnight for a parcel still on the mainland.
3. Hand-load the reserved parcels, carry them over the actual route and unload at the store. World
   presentation reads Core contracts and completed events; it must not reach into Economy classes.
   Watching, sleeping or being elsewhere must produce the same arrival result for the same clock,
   world seed, route conditions and paid orders.
4. Mark the order ready only after the completed delivery. If weather prevents that run, retain the
   paid order and show "Held up by the weather" at the counter. Never charge again or fabricate a
   delivery. A revised estimate is explained locally when he next visits.

The companion logic is intentionally smaller: it calculates `DayDue = DayPlaced + DeliveryDays`
and marks arrival on that absolute day. **It does not reserve carrier space, consult weather, or
wait for unloading.** Before a visible catalogue can promise a real delivery, a separate integration
must feed the route's quoted day into the order and gate arrival on completed delivery, with tests
for missed runs and closing hours. Keep the existing fixed-wait tests as the baseline contract and
explicitly migrate that rule when route-backed arrival replaces it. This document does not claim
that linking the current arrival event to a parcel picture would be enough.

The first-week closure needs one ruling before that integration: does its weather also stop the
supply runner? Tide feasibility alone is not permission to cross storm conditions. Until ruled,
assume unsafe weather stops the run too. The catalogue may explain and record orders for delivery
after safe access resumes, but must not promise a two-day crossing through an impassable storm.
If the intended carrier becomes a supply boat or truck, it needs an authored safe route and capacity
in the island supply system first. This page neither creates one nor exempts it from the weather.

## What belongs in the first week

Keep the shelf useful: basic fishing gear, the existing everyday bait and groceries stay obtainable
there, subject to each item's stock. Ordering is optional and must not gate clam digging, shore
fishing, buying the broken dory, repairing her by hand or launching. The store offers no boat
hardware through either shelves or special orders. Fitted instruments, engines, hulls and repair
parts do not belong in this catalogue. The dory repair beat needs its own on-island source of parts.

The companion's six example lines use existing, currently unlisted bait items. Their prices are the
existing per-bait prices, not the usual ten-bait counter lot; the order page must state that unit.
Each example has a two-day model wait and a maximum of twenty individual units outstanding. These
are authored examples for the logic, not an accepted delivery schedule or a balance verdict.

| Existing item | Price per unit | First-week reason to consider it |
|---|---:|---|
| `bait.herring` | 3 | Compare oily bait with the everyday capelin |
| `bait.mackerel` | 4 | Another existing oily bait to try once the player knows his usual catch |
| `bait.fish_scrap` | 1 | An inexpensive optional bait; never a requirement to buy pot gear |
| `bait.sea_worm` | 4 | A shore-fishing experiment |
| `bait.shucked_clam` | 3 | Another familiar shore bait |
| `bait.squid_strip` | 6 | A tougher strip to try with a better-earned purse |

Offer the six as a small catalogue of optional choices, not six errands. Prefer one naturally
motivated order and one collection during the demo if a safe supply window is approved. Introduce
it after the player has earned and spent money at a shelf; let him decline. A line being purchasable
does not grant a fishing licence. Every catch still needs its licence to be kept; an unlicensed
catch is let go. Ordering never bypasses that rule or the island's weather boundaries.

If the supply route remains shut throughout the demo, introduce the book and its later delivery
promise without manufacturing a collection beat. Receipts still awaiting delivery, and arrived
receipts not collected, must carry forward when the demo's save continues into the full game.

## State and the next implementation steps

The current format remains version 25. The companion book has no save hookup. Its plain snapshot
contains the seller, the next order number, and every open order's line id, quantity, placed day,
due day, arrived flag and unique order number. Stable numbers distinguish repeated purchases of
the same line. Capture and restore must not spend money or replay collection events.

The later version-26 step must store both travelling and arrived-but-uncollected paid orders, restore
them after the game clock, and catch up arrival once. Retired line ids need an explicit migration;
silently dropping a prepaid order loses the player's money. The carrying handover and removing an
open order must share a save boundary, so a reload cannot duplicate or lose goods. Collected receipt
history and "heard the news" are presentation needs beyond the open-order snapshot; do not add saved
fields for them in this slice. A later save proposal should decide their minimal representation.

Build the visible interaction only after the route promise, physical receiving adapter and save
step exist. Verify full hands and bag, two same-line receipts, an order delayed by a missed run, a
sleep over delivery, collection after reopening, and leaving a paid order untouched through day
seven. A final physical playthrough must follow the goods from carrier to counter to hands to storage.

## Short choices for the owner

| Question | Lean |
|---|---|
| Does first-week weather stop supply runs too? | Yes, while unsafe |
| Show one completed delivery in the demo? | Yes, if safely routed |
| May uncollected bait spoil at the store? | No, store keeps it |
| Allow cancelling a paid order before dispatch? | Full refund before dispatch |
| Keep all six bait choices in the first catalogue? | Yes, all optional |

These are unresolved choices. Cancellation and refunds are not implemented by the companion book;
until decided, the page must disclose payment and waiting before the player confirms an order.
