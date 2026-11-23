# Island supply: what St Peters imports, what carries it, how often it crosses

Slice S1 of the island supply plan (2026-11-15). Data and a pure calculator only: nothing walks, drives,
stocks or sells yet. It serves P1 (the sea sets the day: the bar decides when the island is supplied),
P3 (a working coast with real errands) and P5 (lose the quad and the island feels it).

The owner's ask: "Note which items must be sourced off St. Peter's island from NMC to keep the island
running ... do math on how much items they must take to meet island demand/consumption. Plus extra for
player purchase. Calculate frequency of crossings required." and "adequate storage tools, wagon, cart,
pail, bags ... Accurate storage size."

Every number below is in a Def or the route config and is the owner's to tune. The EditMode tests in
`Assets/Tests/EditMode/IslandSupply/` read the shipped assets, the shipped `GameConfig` and the St Peters
region's tide profile through the real `TideModel.Height`, and pin the results in this page. Change a
number and the test that moves tells you what changed.

## Code and data

| What | Where |
|---|---|
| Types (Core, readable by every module) | `Assets/_Project/Code/Core/IslandSupply/` |
| Window and crossing maths | `CrossingMath` (takes `waterAt(totalSeconds)`; callers pass `TideModel.Height` with the island region's profile) |
| Demand, capacity, loads, buffer | `SupplyPlanMath` |
| Goods, carriers, roles, bulk materials | `Assets/_Project/Data/Resources/IslandSupply/{Goods,Carriers,Roles,Bulk}/` |
| Route config | `Assets/_Project/Data/Resources/IslandSupply/IslandSupplyConfig.asset` |

## Goods that must come from NMC over the bar

Daily, whole island (18 residents, the store, school, post office and cannery), as carried.

| Good | Id | Per day | Carried kg | Carried L | Player allowance |
|---|---|---|---|---|---|
| Groceries and dry goods | `islandgood.groceries` | 18 kg | 18 | 40 | +25% |
| Gasoline | `islandgood.gasoline` | 20 L | 17 | 24 | — |
| Stove oil (×2 in winter) | `islandgood.stove_oil` | 15 L | 15 | 18 | — |
| Propane | `islandgood.propane` | 2.5 kg net | 4.7 | 9 | — |
| Mail and parcels | `islandgood.mail_parcels` | 3 kg | 3 | 10 | — |
| Ice | `islandgood.ice` | 20 kg | 20 | 22 | +25% |
| Bait top-up | `islandgood.bait` | 10 kg | 10 | 12 | +25% |
| Hardware, lumber, gear | `islandgood.hardware` | 10 kg | 10 | 15 | +25% |
| Medicine and misc | `islandgood.medicine_misc` | 2 kg | 2 | 5 | — |
| **Island use** | | | **99.7** | **155** | |
| **With player allowance** | | | **114.2** | **177.25** | (+14.5 kg, +22.25 L) |

About 3.2 t per 28-day season. Fleet diesel, bulk heating oil and bulk lobster bait come **by sea** at
high water and are not counted. Fish, clams, firewood, garden produce and seaweed are the island's own.

## Carriers

Practical load (on sand, or in the hands) and volume. Both limits apply.

| Carrier | Id | kg (rated) | L |
|---|---|---|---|
| Shopping bag | `carrier.shopping_bag` | 8 | 20 |
| Pail | `carrier.hand_pail` | 8 | 11 |
| Rucksack | `carrier.rucksack` | 15 | 40 |
| Hand cart (beach cart) | `carrier.hand_cart` | 70 (100) | 150 |
| Garden wagon | `carrier.garden_wagon` | 80 (150) | 120 |
| Quad front rack | `carrier.atv_front_rack` | 45 | 50 |
| Quad rear rack | `carrier.atv_rear_rack` | 90 | 50 |
| Quad utility trailer | `carrier.atv_trailer` | 250 | 320 |
| Pickup bed (mainland only) | `carrier.pickup_bed` | 1,000 | 1,700 |
| Tractor pallet forks | `carrier.tractor_forks` | 600 | 1,486 (one pallet, 1.2 m high) |
| Tractor bucket | `carrier.tractor_bucket` | 600 | 350 |
| 20 L jerry can | `carrier.jerry_can_20l` | 14.9 contents, 2.2 tare | 20 |
| 205 L drum | `carrier.drum_205l` | 172.2 contents, 18 tare | 205 |
| Wooden pallet (GMA) | `carrier.pallet_gma` | 1,000, 20 tare | 1,486 |

The jerry can and the drum are pinned equal to `fuelstore.gas_jerry_s20` and `fuelstore.diesel_drum_s205`.

Bucket fills (0.35 m³): raw fish with ice 297.5 kg, wet seaweed 210 kg, potatoes 234.5 kg. All are
volume-bound; the 600 kg cap does not bite. (Gravel at 1.6 t/m³ would be 560 kg, also under the cap;
the plan's note that the cap bites for gravel was wrong.) The forks take a 2-drum pallet (400.4 kg) and
refuse a 4-drum pallet (20 + 4 × 190.2 = 780.8 kg).

## The bar and the clock

The tide is St Peters' own (`Data/Regions/StPeters.asset`: mean 0, amplitude 2.2 m, phase 1 h) through
`TideModel.Height` (`Assets/_Project/Code/Environment/TideModel.cs:21-37`), with `GameConfig.asset:17`
(1800 clock s per game day: one game hour is 75 real seconds) and `:27-29` (period 12.4206 h, lunar
month 28 d, neap fraction 0.45). Movers' speeds are metres per real second on the same axis, so a leg of
d metres takes d / v clock seconds.

The route config: bar 611 m (bar head to the NMC landing), crest 0.88 m, gut bed −0.6 m, gut 189 m from
the island end and 30 m wide (all pinned to `StPetersBuilder.cs:439-461`), island path 52 m, mainland road
372 m (BarRoad 272 + Wharf Road 100, to the wharf market and pumps), working day 06:00–20:00.

**Crossing rule:** the crest must be dry the whole time a mover is on the bar, and while in the gut the
water must be shallower than the mover's wade limit.

| Pinned result | Value |
|---|---|
| Bar windows per 28 days | 54 |
| Shortest (spring) / longest (neap) bar window | 7.84 h / 10.52 h |
| Gut window at 0.3 m wade (cart), neap / spring | 4.99 h / 5.67 h |
| Foot pair over the bar (1.4 m/s) | 5.82 h; **1 crossing per window** in all 54, so they stay over |
| Quad, bar head to bar head (5 m/s sand, 6.5 m/s road, 0.5 h loading) | 5.29 h; 5.50 h village to village; **a round trip fits every window** |
| Days with a quad round trip inside 06:00–20:00 | **24 of 28** (none on days 12, 13, 14, 27) |
| Longest gap between days a daylight quad run is possible | 4 days (day 11 to day 15) |

## The recommended run plan

| Role | Id | Crew (proposal) | Kit | Cadence |
|---|---|---|---|---|
| Quad supply run | `supplyrole.quad_runner` | Junior Poirier | front + rear rack + trailer: 385 kg / 420 L | every 2nd day, same window |
| Foot errand pair | `supplyrole.foot_errand_pair` | Theresa Gillis, Lucie Landry | hand cart + garden wagon + 2 rucksacks + 2 bags: 196 kg / 390 L | weekly on market day, stay over |
| Islanders' pickup | `supplyrole.mainland_pickup` | — | pickup bed, 1,000 kg / 1,700 L | as needed, NMC side only (not counted) |

| Pinned result | Weight | Volume |
|---|---|---|
| Weekly demand | 799.4 kg | 1,240.75 L |
| Weekly capacity (quad 3.5 runs + foot pair 1) | 1,543.5 kg | 1,860 L |
| Headroom | +93.1% | **+49.9% (binds)** |
| Quad alone | | +18.5% |
| One quad run's load (2 days) | 228.4 kg (59%) | 354.5 L (84%, binds) |
| Store buffer, 4 days | 456.8 kg | 709 L |

4.5 round trips and 11 bar crossings a week (quad 7, the foot pair 4 person-crossings): about 1.6 a day.
The 4-day buffer covers one missed quad run (floor(4 / 2) − 1 = 1), and exactly covers the 4-day gap
around springs when no daylight quad run is possible. A gale in that stretch would empty the back room.
If the quad is down, walkers with the cart kit (93 kg / 210 L) need 1.23 round trips a day; weight binds.

## Where this differs from the plan's arithmetic

- Daily totals: 114.2 kg / 177.25 L, not ≈115 / 180 (the plan rounded the allowance volume up to 25 L;
  it is 22.25). Weekly demand is 799.4 kg / 1,240.75 L, not 805 / 1,260.
- The foot pair's kit is a cart and a wagon, as the plan's words say: 196 kg / 390 L. The plan's 190 / 420
  counted two cart kits. Total weekly capacity is 1,543.5 kg / 1,860 L (plan: 1,540 / 1,890). Volume
  still binds; headroom is +93% / +50%.
- The walker kit is 93 kg, not ≈95 (cart 70 + rucksack 15 + one bag 8).
- The neap window measured over the month is 10.52 h; 10.54 is the closed form at exact neap.
- **Daylight quad runs:** the plan said there is always a usable daylight window. For the crest that is
  true, but a same-window quad round trip inside 06:00–20:00 is impossible on days 12–14 and 27, when low
  water falls near 07:00 and 19:00. The worst gap is 4 days, equal to the buffer. S2/S3 should either
  allow a run to leave before 06:00 or come back after 20:00 on those days, or raise the buffer.
- Traffic is about 1.6 crossings a day (the plan's own table), not "about 2" (its verdict).
- 4-drum pallet: 780.8 kg with a 20 kg pallet (plan ≈785); still refused by 600 kg forks.
- Gravel in the bucket is 560 kg, under the 600 kg cap (the plan said the cap bites).

## Not in this slice

The bar window query and access policy (S2), the cross-region runner (S3), the tractor and forklifts
(S4, S5, S7), carried props (S6), store stock and NPC consumption (S7).

## The back-room ledger (living island economy, slice 1)

`Assets/_Project/Code/Economy/Island/` turns the plan above into a day-by-day back room for the store,
recomputed rather than saved: `IslandRunCalendar` says which days the quad run goes (every
`CadenceDays` from day 0; a run the tide forbids is made up on the first possible day before the next
one, else missed), and `IslandStoreLedger` replays from a full back room `LookbackDays` earlier (data:
`Data/IslandLedgers/StPetersIslandLedger.asset`, `islandledger.st_peters`) to give each good's level,
delivery and shortfall for any day. Nothing reads it in game yet. The plan it belongs to is
`docs/design/st-peters-living-economy.md`; the tests are `Tests/EditMode/Economy/IslandStoreLedgerTests.cs`
and `Tests/EditMode/IslandSupply/IslandLedgerShippedTests.cs`.
