# The first week's purse: what it costs, what it pays, and the prices that close it

Status: **the price set in section 1 is applied in data** (owner, 2026-11-25: "the grind should feel
stardew valley paced"; "cod license cheaper but dory still more because fishing cod should earn more
money"). The exact figures are still his to approve. Every number marked **(guess)** is a model
assumption, not data; everything else is read off the shipped Defs and `GameConfig` and priced with the
shipped formulas.

The same arithmetic is checked by `Assets/Tests/EditMode/FirstWeekPricesTests.cs`, which reads the
shipped assets, prices through `SellPricing`, and runs both players' weeks below. The generated model in
`docs/design/first-week-purse.md` (open draft, written before these prices) is not on this branch.

## 1. What changed

| Data | Before | Now | Why |
|---|---:|---:|---|
| `Data/Fish/SoftShellClam.asset` BaseValue | 2 | **8** | clams pay 2 a clam at LeBlanc's (section 3) |
| `GameConfig.MarketPriceLevelStPetersStore` | 0.6 | **0.9** | the counter still pays less than the creek (1.0), but enough that a clam clears 2 |
| `GameConfig.MarketDemandStPetersStore` | 0.7 | **1.0** | the counter takes a day of cod without crashing on the first load (still under the creek's 1.4) |
| `Data/Fish/AtlanticCod.asset` SupplyElasticity | 0.2 | **0.04** | cod holds its price: every buyer wants cod, so a catch of it no longer collapses to 2 a fish |
| `Catalog/Licenses/CodLicense.asset` Price | 120 | **60** | the owner's ruling; one Def, so it is **60 at the Nine Mile Creek harbourmaster too** |
| Basil's dory (`slipsale.basils_dory`, open draft 1539) | 80 | 80 | not edited here; modelled at 80 |

Nothing is added to the save. No code changed.

## 2. The bottom line

| | Careful | Ordinary |
|---|---:|---:|
| Before | 108 short (cod licence out of reach) | 228 short (has only the clam licence) |
| **Now** | **everything by day 3, 512 coins at the word** | **everything by day 6, 75 coins at the word** |

The forced purchases are now 235 (clam licence 15, rod 60, bait 20, dory 80, cod licence 60) plus 93 of
food. The mending supplies are given, not bought (ruling 2026-11-25), and are in no figure here.

## 3. Clams: two coins a clam, and why it takes two edits

The till pays `round(base x glut x level)`, never under 1, and the glut factor never falls under 0.25
(`MarketMath.MinMultiplier`, code). So the settled price of a clam is `base x 0.25 x level`, rounded. To
round to 2 it must reach 1.5, and it must still reach 1.5 for a hungry seller, whose level is cut to the
needs sell floor (`GameConfig.Needs.SellFloor` 0.92): `base x level >= 6.52`.

- On level 0.6 that needs a clam base of 11, which would make a bucket at Nine Mile Creek about 90.
- On level 0.9 a base of **8** does it (8 x 0.25 x 0.9 = 1.8, hungry 1.66), and a bucket at the creek is 59.
- The level must stay under the creek's 1.0 (`MarketPriceGapTests`), so 0.9 and 8 is the smallest pair
  that keeps the creek's side effect down.

What a bucket fetches at LeBlanc's now: **52 on a fresh counter** (7, 5, 4, 3, 3, then 2 each) and
**40 once the counter has had clams** (2 each; the glut never pushes a clam under 2). Before: 20.

## 4. A cod day from the dory against two buckets

Assumptions **(guess)**: one cod day is three dory loads; the dory's hold is 6 (`Data/Boats/Dory.asset`
`HoldUnits`), so 18 cod, each landed on one capelin (2 coins, `BaitSpentOnCatchOnly` on). The bite wait
defaults to 1.5 to 4 real seconds (`FishingController`), so the hold, the rowing and the counter limit
the day, not the bites; whether three loads fit between the morning bucket and dark is not checked in a
playthrough. A deep-water catch off the island with capelin on is assumed to be all cod (capelin favours
cod, and cod's depth bands are the deep ones); mackerel and others would mix in **(guess)**. The counter
settles half its glut each dawn (`MarketDailyRecovery` 0.5).

Two buckets on a settled counter: **80** (before: 40).

| Straight cod days at LeBlanc's | Gross | Bait | Net | Over two buckets | Running |
|---|---:|---:|---:|---:|---:|
| Day 1 (fresh counter) | 174 | 36 | **138** | +58 | 58 |
| Day 2 | 135 | 36 | 99 | +19 | 77 |
| Day 3 | 122 | 36 | 86 | +6 | **83: the dory (80) is paid for** |
| Day 4 | 117 | 36 | 81 | +1 | 84 |

Before, the same day netted 23, then nothing (every cod fetched 2, the price of its bait).

So one good cod day earns 138 against two buckets' 80, and what cod earns over clams pays for Basil's dory
in three straight days at the island counter, sooner if he carries some of it to Nine Mile Creek. By day
4 the island counter is full of cod and the catch is worth taking across the bar: the creek still pays
more for the same day (206 against 174 gross on a fresh counter).

**Rod fish from the shore beat their bait** (five fish on a fresh counter, against 10 of bait): mackerel
29, winter flounder 18, striped bass 43. Smelt (base 3) earns 8 and does not; it is a bait fish.

## 5. The two players' weeks

The rule for both: buy in order (clam licence, rod, bait, dory, cod licence) as soon as the purse covers
the price and still keeps the day's food; the clam licence is bought from Ginny's fronted 20 before any
clam is kept. A bucket is 20 clams dug at one daylight low tide and sold at LeBlanc's **(guess: the
tides and walking allow it)**. Fishing starts the day after he holds what it needs. Food is 9 on the
arrival day and 14 a day after (section 6). "Extra bait" is a further lot of 10 bought when the tackle
box runs short.

### Careful player: both daylight low tides; once he holds the dory and the cod licence, the morning bucket and a cod day

Before:

| Day | Work | Earned | Extra bait | Food | Bought | Purse at night |
|---|---|---:|---:|---:|---|---:|
| 1 | 2 buckets | 40 | 0 | 9 | clam licence 15 | 36 |
| 2 | 2 buckets | 40 | 0 | 14 | rod 60 | 2 |
| 3 | 2 buckets | 40 | 0 | 14 | bait 20 | 8 |
| 4 | 2 buckets | 40 | 0 | 14 | - | 34 |
| 5 | 2 buckets | 40 | 0 | 14 | - | 60 |
| 6 | 2 buckets | 40 | 0 | 14 | dory 80 | 6 |
| 7 | 1 bucket | 20 | 0 | 14 | - | 12 |

108 short: the cod licence (120) never comes in reach.

Now:

| Day | Work | Earned | Extra bait | Food | Bought | Purse at night |
|---|---|---:|---:|---:|---|---:|
| 1 | 2 buckets | 92 | 0 | 9 | clam licence 15, rod 60, bait 20 | 8 |
| 2 | 2 buckets | 80 | 0 | 14 | - | 74 |
| 3 | 2 buckets | 80 | 0 | 14 | dory 80, cod licence 60 | 0 |
| 4 | bucket + cod day | 214 | 20 | 14 | - | 180 |
| 5 | bucket + cod day | 175 | 40 | 14 | - | 301 |
| 6 | bucket + cod day | 162 | 40 | 14 | - | 409 |
| 7 | bucket + cod day | 157 | 40 | 14 | - | 512 |

He has everything on day 3 and ends the week with 512 to spend on what comes next.

### Ordinary player: one bucket a day; once he has the rod and bait, a short session from the shore (five mackerel); once he holds the dory and the cod licence, one dory load instead

Before (the rod never comes in reach, so the week is buckets only):

| Day | Work | Earned | Extra bait | Food | Bought | Purse at night |
|---|---|---:|---:|---:|---|---:|
| 1 | bucket | 20 | 0 | 9 | clam licence 15 | 16 |
| 2 | bucket | 20 | 0 | 14 | - | 22 |
| 3 | bucket | 20 | 0 | 14 | - | 28 |
| 4 | bucket | 20 | 0 | 14 | - | 34 |
| 5 | bucket | 20 | 0 | 14 | - | 40 |
| 6 | bucket | 20 | 0 | 14 | - | 46 |
| 7 | bucket | 20 | 0 | 14 | - | 52 |

228 short.

Now:

| Day | Work | Earned | Extra bait | Food | Bought | Purse at night |
|---|---|---:|---:|---:|---|---:|
| 1 | bucket | 52 | 0 | 9 | clam licence 15 | 48 |
| 2 | bucket | 40 | 0 | 14 | rod 60 | 14 |
| 3 | bucket | 40 | 0 | 14 | bait 20 | 20 |
| 4 | bucket + shore session | 69 | 0 | 14 | - | 75 |
| 5 | bucket + shore session | 58 | 0 | 14 | dory 80 | 39 |
| 6 | bucket + shore session | 55 | 20 | 14 | cod licence 60 | 0 |
| 7 | bucket + one dory load | 109 | 20 | 14 | - | 75 |

Something to buy on five of the seven days, a big item every two or three days, and the cod licence on
day 6 with a day to spare.

**If he only ever digs one bucket a day and never fishes**, he is 16 short: the dory on day 6, and 44 in
the purse on day 7 against the licence's 60. A bucket nets 26 a day after food, and the week's purchases
need about 33 a day. He cannot finish the week that way anyway (Ginny's word needs a fish kept from the
dory), but if the owner wants a buckets-only week to close, the lever is outside this set: for example a
rod at 40, or cheaper food.

## 6. Food

From the shipped Defs: hunger rises 0.0625 an hour (`GameConfig`), so a full day needs 1.5 and the
arrival day (from 07:30) needs about 1.03. Cheapest whole items on LeBlanc's shelf
(`Data/Resources/Food/*.asset`, prices in `Catalog/Supplies/*.asset`): tinned beans 3 (0.35), oatcakes 2
(0.15), bread 4 (0.30). Arrival day: 3 tins = 9. Each full day: 4 tins + 1 oatcake = 14. Week: **93**.
Eating his own catch would save some of this; cooking on the island is not checked.

## 7. Side effects off the island

| At Nine Mile Creek (level 1.0, demand 1.4) | Before | Now |
|---|---:|---:|
| Cod licence at the harbourmaster | 120 | 60 |
| A fresh bucket of clams | 22 | 59 (8, 6, 5, 4, 4, 3, 3, then 2 each) |
| A bucket once the wharf has had clams | 20 | 40 |
| One dory hold of 6 cod, fresh | 64 | 79 |
| 18 cod, fresh | 130 | 206 |
| The first clam / the first cod | 2 / 14 | 8 / 14 |

Clams become a fair earner for the rest of the game (2 a clam anywhere once the market has had some) and
cod keeps its price through a long day everywhere, which raises mid-game cod income at the creek by about
half. The cod elasticity is per species, so the cove and Rice Point see the same. No other species moved.
The island still pays less than the creek for both a bucket and a day of cod, so carrying a catch across
the bar still pays.

## 8. What is not checked

- In a playthrough: walking and selling time, whether two buckets fit in the two daylight low tides,
  whether three dory loads fit in a day, rowing time, and how much of a deep-water catch is cod.
- Whether the island's clam diggers (NPCs) sell into the same counter; the model assumes only the player
  adds supply.
- Mackerel being in season in the first week (`Seasons` on `Mackerel.asset`).
- The other open draft that writes `first-week-purse.md` still carries the old prices.
