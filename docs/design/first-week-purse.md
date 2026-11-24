# First-week purse

At the island's current prices, a sellable clam pays **1 coin**. A careful two-bucket day therefore brings in **40**, and a one-bucket day brings in **20**, before food. With the present **400** damaged-dory price, those two routines reach the purchase on days **11** and **25**, respectively, under the assumptions below. Neither reaches it on days 3–5. This is a budget calculation, not a measured playthrough: enough exposed holes, carrying time, fatigue and food access still need to be demonstrated.

There is also an access problem that price arithmetic cannot solve. On base `37586ae43d2ed0eef2648a642c300a6926ac8bf1`, the damaged-dory offer and cod licence are at Nine Mile Creek. The intended first week is entirely on St Peters, with the damaged dory on its slip and repairs done by hand. Until those access and repair changes arrive, **no island-only day of purchase or launch is established**, even with sufficient money. No price, offer, scene, saved field or existing design page is changed here.

## Inputs read from main

All amounts are coin; calendar day 1 means the first playable day, after the initial grant and licence purchase. Source links point to the existing assets or logic. Earlier pacing prose describes an older itinerary and some superseded bait prices; the numbers here come from the actual files.

| Input | Main value | Source / qualification |
|---|---:|---|
| Initial wallet | 0 | `PlayerWallet._money` in the committed St Peters scene |
| One-time clam-fee grant | 20 | [GameConfig](../../Assets/_Project/Data/Config/GameConfig.asset), `FrontedLicenceFee`; [FrontedFeeGrant](../../Assets/_Project/Code/Economy/FrontedFeeGrant.cs) credits it once, with no debt repayment modeled |
| Clam licence | 15 | [ClamLicense](../../Assets/_Project/Data/Resources/Catalog/Licenses/ClamLicense.asset), island store; **5 remains** after the grant pays it |
| Shovel and bucket | Starting gear | [StartingGear](../../Assets/_Project/Code/Player/StartingGear.cs); replacement offers 25 and 15 exist but are unlisted, so do not charge these again in the opening budget |
| Bucket capacity | 20 catches | [ClamBucket](../../Assets/_Project/Code/Player/ClamBucket.cs), also serialized as 20 in St Peters; a capacity, not an automatic daily yield |
| Clam yield | 1 per successful hole per tide | [ClamDig](../../Assets/_Project/Code/Fishing/ClamDig.cs); re-exposure after high water renews spent holes; missed/startled holes yield nothing |
| Authored holes | 76 ClamDig components | Counted in the committed St Peters scene, without loading it; exposure, reachability and completion time were not measured |
| Clam value / elasticity | 2 / 0.45 | [SoftShellClam](../../Assets/_Project/Data/Fish/SoftShellClam.asset); sale is per catch, not per kilogram |
| Island demand / price level | 0.7 / 0.6 | GameConfig; the scene market has no category overrides and adds one supply unit per sale |
| Daily market recovery | 0.5 | GameConfig; half of accumulated category supply clears each day |
| Rod | 60 | [Rod](../../Assets/_Project/Data/Resources/Catalog/Gear/Rod.asset), listed at the island store |
| Capelin | 2 each, ten for 20 | [Capelin](../../Assets/_Project/Data/Resources/Catalog/Bait/Capelin.asset), plus `BaitDef.LotSize` default 10; the asset omits the field |
| Bait policy | Spent on catch only | GameConfig `BaitSpentOnCatchOnly: 1`; no miss-spend penalty is assumed |
| Cod licence | 120 | [CodLicense](../../Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset), names only the mainland harbour office |
| Bread / beans / oatcakes | 4 / 3 / 2 | [Supplies](../../Assets/_Project/Data/Resources/Catalog/Supplies); 600 / 420 / 350 kcal respectively in the FoodDefs |
| Ice / soap | 6 / 3 | Same supply folder; optional below, not quietly included in food |
| Damaged dory / repair | 400 / 300 | [DamagedDoryOffer](../../Assets/_Project/Data/Resources/Catalog/Shipwright/DamagedDoryOffer.asset), `seller.nmc_yard` |
| Mending materials | No priced shopping list | Main charges the 300 repair through [Shipwright](../../Assets/_Project/Code/Economy/Shipwright.cs); planks, fasteners and sealant are not separate repair offers |

The six starting herring in `StartingBait` are a separate starting grant for trap use. They are not a free ten-pack of capelin, so the budget retains a capelin purchase. No rental charge at Ginny's is included: there is no first-week rent price in these offers. The 900 camper and 140 sounder are not required opening purchases.

## What a day of clams pays

The actual sale path is [SellService](../../Assets/_Project/Code/Economy/SellService.cs) → [SellPricing](../../Assets/_Project/Code/Economy/SellPricing.cs) → [MarketMath](../../Assets/_Project/Code/Economy/MarketMath.cs). For catch index `i` in a sale:

```text
multiplier = clamp(1 / (1 + elasticity × (supplyBefore + i) / demand), 0.25, 1.6)
price_i = max(1, roundToEven(baseValue × multiplier × freshness × outletLevel × characterFactor))
lot = sum(price_i)
nextDaySupply = 0.5 × (supplyBefore + numberSold)
```

The scenarios use a fed, rested player without a sale trait (`characterFactor = 1`) and immediate sale (`freshness = 1`). On the island the first clam is `round(2 × 1 × 0.6) = round(1.2) = 1`. Every later clam has an equal or smaller multiplier, so the one-coin floor keeps **every sellable clam at 1**, even when the category is glutted. Selling a fresh twenty-clam bucket is therefore `20 × 1 = 20`, not 40. Daily recovery does not raise that price above 1.

| Day's work | Sale revenue | Less illustrative 4-coin food top-up | Net saving |
|---|---:|---:|---:|
| One successful dig, then sell | 1 | Not a daily plan | 1 before food |
| One 20-clam bucket | 20 | 4 | 16 |
| Two 20-clam buckets, carried and sold separately | 40 | 4 | 36 |
| All 76 authored holes once | 76 | 4 | 72, an unverified throughput ceiling |
| All 76 holes across two complete exposures | 152 | 4 | 148, an unverified two-exposure bound, not a guaranteed calendar day |

The player must physically carry, put away and sell each load. Forty clams means forty successful digs and two full bucket trips; it does not mean a crate appears at the counter. Seventy-six components do not prove seventy-six usable holes during one low water. Two tidal exposures are not guaranteed inside every 24-hour play day, and a hole spent by startling does not earn its coin.

Clam spoil is 0.5 per day at ambient rate one; the unsellable threshold is 0.9. A clam held continuously at ambient becomes refused after about `0.9 / 0.5 = 1.8` days. Before refusal the one-coin floor masks the falling value. A careless player who sells later the same day still gets 20 from twenty clams; a player who lets a bucket cross the refusal threshold gets **0**, not a guaranteed minimum 20. Ice, frozen or live storage have separate rate rules, and using them still requires the corresponding physical handling.

The 4-coin daily food figure is **a supplement**, such as two oatcakes, only 700 kcal. It assumes other meals at his lodging or from prepared catch; that provision is not priced or guaranteed by this audit. If he must buy six oatcakes for 2,100 kcal at 12 coin daily, net clam saving becomes 28/8 and the present hull takes days **15/50**, before other needs. Neither menu is a nutrition or stamina simulation. This missing food contract materially changes the answer.

## What shore fish pay

These are exact fresh, trait-neutral ten-catch sales into an initially empty island category, with marginal glut applied after each catch. They are **sale illustrations, not ten guaranteed catches per day**. Region, season, depth, bite success, licence access and landing time decide whether such a load exists.

| Species | Base / elasticity | First fresh unit | Ten fresh units | Ten at half freshness | Fresh ten minus a 20-coin capelin pack | First-week eligibility on this base |
|---|---:|---:|---:|---:|---:|---|
| Cod | 14 / 0.2 | 8 | 43 | 21 | 23 | St Peters is in its region list; all seasons; cod licence is mainland-only; suitable depth still needed |
| Mackerel | 10 / 0.35 | 6 | 27 | 14 | 7 | St Peters listed, but season mask 6 is summer/autumn, not early spring; no species licence offer on this base |
| Pollock | 11 / 0.2 | 7 | 34 | 17 | 14 | St Peters is absent from its region list; exclude from island-income budget |
| Haddock | 16 / 0.2 | 10 | 49 | 25 | Not costed with capelin | South Water only; exclude from island-income budget |

Sources: [cod](../../Assets/_Project/Data/Fish/AtlanticCod.asset), [mackerel](../../Assets/_Project/Data/Fish/Mackerel.asset), [pollock](../../Assets/_Project/Data/Fish/Pollock.asset), [haddock](../../Assets/_Project/Data/Fish/Haddock.asset). The first cod lot's prices are `8+7+5+5+4+3+3+3+3+2 = 43`; capelin is explicitly authored to favour cod, mackerel and pollock. The mackerel lot is `6+4+3+2+2+2+2+2+2+2 = 27`.

Do not repeat the virgin-category total every day. For cod, ten sales leave supply 10; dawn halves it to 5, so the following ten fresh cod pay **24**, leaving **4** after ten replacement capelin. Sustained glut eventually prices fresh cod at 2 each, exactly the capelin unit cost. Fresh mackerel also reaches 2; stale fish can pay only 1 while still consuming a 2-coin bait. Ice at 6 per load can turn a thin margin negative. Fish value is per catch despite weight differences.

The newer rule says every catch needs a licence and unlicensed catches are released. Main's legacy policy gates only species named by a LicenseDef, currently clams and cod; it does not supply an island mackerel licence price. Treat that as missing content, **not permission to budget unlicensed fish income**. PRs 1493 and 1495 address related policy; no invented licence fee fills the gap here. Shore-fish income is excluded from the guaranteed clam-only reserve calculation. The pre-dory rod, bait and licence remain expenses the complete first-week plan must fund.

## The week's shopping and daily purse

| Purchase | Quantity / rule | Week cost at main prices |
|---|---|---:|
| Clam licence | Once; paid from the grant | 15 |
| Cod licence | Once; requires a new island purchase route | 120 |
| Rod | Once | 60 |
| Capelin | One ten-pack as an initial reserve; every further ten landed fish costs another pack | 20 |
| Food supplement | Two oatcakes daily × seven days | 28 |
| Damaged dory | Once | 400 |
| Repair allowance | Existing charge as a **proxy**, not a priced material bill | 300 |
| **Total** | `15 + 120 + 60 + 20 + 28 + 400 + 300` | **943** |
| Less initial grant | 20 | **923 to earn** |

A seven-day careful clam routine earns `7 × 40 = 280`; a careless one earns `7 × 20 = 140`. Including the grant gives 300/160, leaving **643/783 short** of that complete 943 budget. Removing the cod licence or repair from a calculation does not make them free; it only postpones a required step. Optional soap adds 3; one ice bag adds 6; buying food entirely at the shelf raises the food line; losses and extra bait increase costs.

For the hull-only purchase test, postpone rod, cod licence, bait and repair. He starts with `20 − 15 = 5`. Define careful as two successful buckets sold per day, and careless as one (misses a window, takes longer, but sells before rot); both spend the same 4 on supplemental food. This isolates effort, rather than also imposing an arbitrary spending penalty.

```text
careful_purse(d) = 5 + d × (40 − 4) = 5 + 36d
careless_purse(d) = 5 + d × (20 − 4) = 5 + 16d
purchase_day(P) = ceil((P − 5) / daily_net), if daily_net > 0
```

| Day | Careful purse before hull/kit | Careless purse before hull/kit |
|---:|---:|---:|
| 1 | 41 | 21 |
| 2 | 77 | 37 |
| 3 | 113 | 53 |
| 4 | 149 | 69 |
| 5 | 185 | 85 |
| 6 | 221 | 101 |
| 7 | 257 | 117 |

At 400 the hull needs `ceil(395/36)=11` or `ceil(395/16)=25` days. Reserving the rod/licence/bait first adds 200: `ceil(595/36)=17` or `ceil(595/16)=38`. Funding the hull, repair and that kit needs `ceil(895/36)=25` or `ceil(895/16)=56`. Beyond seven days these are constant-routine affordability extrapolations, not forecasts of season, travel, consumption or changed fishing opportunities.

A very efficient player completing 152 successful digs and deliveries per calendar day could fund 400 on day 3 and the complete hull/repair/kit threshold on day 7 (`ceil(895/148)=7`). That explains why throughput measurement matters: the same prices can be a three-day sprint or a multiweek grind. This audit did not establish that such throughput is possible or pleasant.

## Three candidate price sets, without changing an asset

These are alternatives for review, not accepted balance. Keep the current clam value, market rules, rod 60, cod licence 120, clam licence 15, capelin 20 per pack and food 4/day. Change only the two prices that define the dory step. The repair column is a budget envelope pending an actual hand-mending materials list.

| Set | Damaged hull | Repair envelope | Hull day, careful / careless | Hull + repair + 200 kit day, careful / careless |
|---|---:|---:|---|---|
| Lean (main prices) | 400 | 300 | 11 / 25 | 25 / 56 |
| Comfortable | 140 | 100 | 4 / 9 | 13 / 28 |
| Generous | 80 | 60 | 3 / 5 | 10 / 21 |

Example: generous hull-only days are `ceil((80−5)/36)=3` and `ceil((80−5)/16)=5`. Its full threshold is `80+60+200=340`: `ceil(335/36)=10`, `ceil(335/16)=21`. Thus **even the generous set meets only the hull-purchase target**, under subsidized meals and deferred fishing kit. It does not deliver the complete launch-and-fish week. With an island-only restriction, all dates also remain conditional on the offer and licence moving onto the island and the mending loop existing.

A workable seven-day budget under these routines permits at most 257/117 for hull, materials and kit after food and the clam licence. The current kit alone is 200. The careless profile therefore cannot fund the complete week even if the hull and repair are free. That is a content/support/earnings decision, not a rounding error.

## Which single prices decide it, and where buyers stop

- **Damaged hull price** is the direct purchase-day control. For both profiles to buy during days 3–5, the common integer price window is **78–85**: careful must not afford it after day 2 (77), and careless must afford it by day 5 (85). The 80 candidate lies inside it. Buying other kit first destroys that window.
- **Repair charge 300** is three quarters of the hull cost. Cutting only the hull can make ownership arrive while leaving the boat unusable for many days. Do not count the 300 proxy as proof that materials exist or can be fetched on the island.
- **Cod licence 120** is 7.5 careless net days and more than his entire seven-day 117 reserve. An early licence grant, temporary permission or smaller price would alter the week substantially, but only an approved licence design can supply it.
- **Rod 60** costs 1.7 careful net days or 3.75 careless net days. It does not immediately pay itself back at the island's glutted fish prices. The 20-coin bait-pack minimum also matters to the first transaction even though the unit price is only 2.
- **Clam base value 2** cannot be assessed as two coins earned: the island multiplier and integer floor flatten it to one. At high glut the unrounded value is `base × 0.25 × 0.6 = base × 0.15`; raising base to 3 or 4 still leaves the floor at 1. Base 10 reaches 1.5, rounded to 2 with the shipped arithmetic. Raising clam value affects every buyer and the low-glut first catches; it is a broader change than lowering one hull offer.
- **Food provision** decides whether the 4/day case is credible. A free roof is not a demonstrated free meal. Purchased food shortage would also affect effort and sale factors, which these arithmetic cases hold constant.

The island buyer has **no finite cash reserve or daily purchase quota** in `FishBuyer`, `SellService` or `Market`. The transaction adds money to the player's wallet; it does not debit a shop wallet. Demand is a price-curve parameter, not a stock of money that reaches zero. Supply gluts accumulate separately by fish category, recover by half daily, and price multipliers bottom at 0.25. A still-sellable catch floors at one coin; rotten catches are refused outright. The counter's opening/access and hand delivery still matter.

The seller's **outgoing** stock is separate: on this base the three foods share 18 grocery items, bait and ice have their own lines, and the island run may fail. Those shelves can block supplies even while the buyer has unlimited money for catch. PR 1491 owns the split-food change. No assumption of unlimited purchased food belongs in a finished demo validation.

## Verification and questions for the owner

Prices and rules were checked against main's assets and source; the 76-hole count and serialized market/wallet/bucket values came from a filtered scene census. A separate arithmetic calculation reproduced marginal prices with single-precision steps, checked the day tables and summed the shopping list. No local compiler, Unity run, test suite, physical carry route, tide-window timing, seasonal fishing trial or nutritional simulation ran. No claim is made that the projected catches can all be obtained or that the full first week is currently playable. Existing `m1-progression-pacing.md`, seller/content tests, market math and stock tests were read rather than duplicated as a new simulator.

1. Should days 3–5 mean buying the damaged hull, or launching a repaired boat with legal fishing kit? **Lean: launching by the end of day 5**, with buying earlier; review the whole bill, not only the hull price.
2. Is the careless profile meant to remain within the seven-day demo without repeated perfect low-water trips? **Lean: yes**, with an earned island task or explicit help covering the missing licence/material budget rather than requiring every clam hole.
3. Does lodging include actual meals, and which ones does he carry to work? **Lean: explicit meals at home plus paid portable food**, with authored portions; do not balance around an undocumented 4-coin diet.
4. Which licence authorizes each first-week shore catch, at which island counter, and for how much? **Lean: a small, explicit island licence package**, so every kept catch is legal without a mainland visit; retain the later specialist licence ladder.
5. Who supplies the mending materials, how many hand-carried pieces are required, and what is their total price? **Lean: an island repair bundle tied to the damaged dory**, separate from general-store boat hardware; validate the physical work before choosing the envelope.
6. Should clam money carry the whole boat step, or should shore fishing and an island job materially contribute? **Lean: a mixed week**, with measured legal shore earnings that stay positive after bait and freshness; the present glut floor cannot support repeated virgin-market income.
7. Which provisional price set should be tried once access, food and materials are authored? **Lean: use the generous hull price only as a throughput experiment**, then rebalance the entire week; it meets the hull days but still fails the launch budget.
