# The first week's purse

**Known shortfalls:** the ordinary player's rod is 0 coins short on day 3. The careful player's complete week is 0 coins short on day 7. The shellfish licence is funded on day 1. All three cash ledgers stay nonnegative; the careless player leaves 36 coins of planned food unfunded.

These are conditional cash budgets for one carried pail per visited tide, not a maximum harvest or a successful playthrough. Finite shelf stock, travel and eating access can make the week harder. Proposed hull and repair prices are included; a cash milestone does not create an island seller for an unavailable item.

## Careful

| Day | Visits | Refused | Clams sold | Income | Food paid | Food unfunded | Purchases / earmarks | End purse |
|---|---:|---:|---:|---:|---:|---:|---|---:|
| 1 | 2 | 0 | 40 | 92 | 9 | 0 | Shellfish licence; Rod; Bait lot | 8 |
| 2 | 2 | 0 | 40 | 80 | 14 | 0 | - | 74 |
| 3 | 2 | 0 | 40 | 80 | 14 | 0 | Dory (proposal); Patch plank (proposal); Copper nails (proposal); Oakum (proposal); Paint (proposal) | 0 |
| 4 | 2 | 0 | 40 | 80 | 14 | 0 | Cod licence | 6 |
| 5 | 2 | 0 | 40 | 80 | 14 | 0 | - | 72 |
| 6 | 2 | 0 | 40 | 80 | 14 | 0 | - | 138 |
| 7 | 1 | 0 | 20 | 40 | 14 | 0 | - | 164 |

Seven-day income: 532. Food paid: 93. Unfunded planned hunger relief: 0. Remaining cash shortfall, including missed food: 0.

## Ordinary

| Day | Visits | Refused | Clams sold | Income | Food paid | Food unfunded | Purchases / earmarks | End purse |
|---|---:|---:|---:|---:|---:|---:|---|---:|
| 1 | 1 | 0 | 20 | 52 | 9 | 0 | Shellfish licence | 48 |
| 2 | 1 | 0 | 20 | 40 | 14 | 0 | Rod | 14 |
| 3 | 1 | 0 | 20 | 40 | 14 | 0 | Bait lot | 20 |
| 4 | 1 | 0 | 20 | 40 | 14 | 0 | - | 46 |
| 5 | 1 | 0 | 20 | 40 | 14 | 0 | - | 72 |
| 6 | 1 | 0 | 20 | 40 | 14 | 0 | Dory (proposal) | 18 |
| 7 | 1 | 0 | 20 | 40 | 14 | 0 | Patch plank (proposal); Copper nails (proposal); Oakum (proposal) | 0 |

Seven-day income: 292. Food paid: 93. Unfunded planned hunger relief: 0. Remaining cash shortfall, including missed food: 76.

## Careless

| Day | Visits | Refused | Clams sold | Income | Food paid | Food unfunded | Purchases / earmarks | End purse |
|---|---:|---:|---:|---:|---:|---:|---|---:|
| 1 | 1 | 0 | 20 | 52 | 9 | 0 | Shellfish licence; Unneeded bait | 28 |
| 2 | 0 | 0 | 0 | 0 | 14 | 0 | - | 14 |
| 3 | 1 | 0 | 20 | 40 | 14 | 0 | - | 40 |
| 4 | 1 | 0 | 20 | 40 | 14 | 0 | Rod | 6 |
| 5 | 0 | 0 | 0 | 0 | 6 | 8 | - | 0 |
| 6 | 1 | 1 | 0 | 0 | 0 | 14 | - | 0 |
| 7 | 1 | 1 | 0 | 0 | 0 | 14 | - | 0 |

Seven-day income: 132. Food paid: 57. Unfunded planned hunger relief: 3.95. Remaining cash shortfall, including missed food: 256.

## First affordable purchase in the spending order

Food is budgeted each day before progression purchases. Days below include all earlier purchases; '-' means not reached by day 7. The careless player's extra bait is wasted and does not replace the planned fishing lot.

| Purchase | Coins | Source / availability | Careful | Ordinary | Careless |
|---|---:|---|---:|---:|---:|
| Shellfish licence | 15 | Island counter | 1 | 1 | 1 |
| Rod | 60 | Island counter | 1 | 2 | 4 |
| Bait lot | 20 | Island shelf | 1 | 3 | - |
| Dory (proposal) | 80 | PR 1539 | 3 | 6 | - |
| Patch plank (proposal) | 24 | PR 1508 | 3 | 7 | - |
| Copper nails (proposal) | 8 | PR 1508 | 3 | 7 | - |
| Oakum (proposal) | 12 | PR 1508 | 3 | 7 | - |
| Paint (proposal) | 16 | PR 1508 | 3 | - | - |
| Cod licence | 60 | Island counter | 4 | - | - |

Purchase list: 295 coins. Starting purse plus the one-time fronted fee: 20. Seven-day full ration budget: 93. The careful route needs 0 more coins at day 7; at the current one-coin clam price, that is the same number of additional saleable clams. Repeated trips within a tide could change this result; this model does not claim they are impossible.

## The three written strategies

All start at 07:30, then wake at 06:00 and turn in at 22:00 (the configured maximum 8-hour sleep). The licence is paid before retaining any clams. The placed shovel and pail are collected for free. Setup, walking, hand transfers and selling time are not simulated.

- Careful: visit every low-water cycle overlapping waking time, with one load of at most 20 clams per visit.
- Ordinary: visit the first such window each day, with one load.
- Careless: follow the ordinary route, miss days 2 and 5, and spend on one unnecessary bait lot before food as soon as he can. Buy the affordable units of the planned ration; show every unpaid unit as a food shortfall.

Each visit uses the greatest exposed-hole count sampled during that awake window, capped by one pail. A hole yields one clam per tide. Tide cycle ids prevent counting a midnight tail as a fresh tide. The player carries and sells each load before the next visit; income is booked on that day. This assumes fresh, saleable catch and ordinary price factors. No fish income, gifts beyond the first fee, off-island trade, free working boat or resale windfall is credited.

Hunger and tiredness advance minute by minute. Food is bought from available cash at waking and after a sale; a held food unit is eaten while awake once hunger can use its full relief. Sleep reduces tiredness but hunger keeps rising. Each visit occurs at the first minute attaining that window's greatest awake exposure. EffortGate checks the actual needs before awarding any clams. Refused visits produce no income. The careless route runs out of food money, and hunger refuses his later visits once the food money is gone.

## Tide and carry data

World seed 12345; start season 1, day 1; 1800 seconds per game day. The tide is seed-independent; the seed is read and recorded, never randomly replaced. Mean 0 m, amplitude 2.2 m, phase 1 h, period 12.4206 h, lunar month 28 days, neap fraction 0.45. Read 76 placed holes and the carried pail's 20-unit capacity.

Low water here means at or below the region's mean level, not a safe bar crossing. Times are rounded to minutes. Hole exposure uses the shipped painted terrain and TidalExposure; awake counts use one-minute samples. The last column lists exposed holes / carried clams for each awake cycle, including evening windows whose minimum falls after bedtime.

| Day | All low-water spans | Awake exposure / one-load yield |
|---|---|---|
| 1 | 05:13-11:25; 17:38-23:50 | 76 / 20; 76 / 20 |
| 2 | 06:03-12:16; 18:28-24:00 | 76 / 20; 76 / 20 |
| 3 | 00:00-00:41; 06:54-13:06; 19:19-24:00 | 76 / 20; 73 / 20 |
| 4 | 00:00-01:31; 07:44-13:57; 20:09-24:00 | 71 / 20; 58 / 20 |
| 5 | 00:00-02:22; 08:35-14:47; 21:00-24:00 | 65 / 20; 33 / 20 |
| 6 | 00:00-03:12; 09:25-15:38; 21:50-24:00 | 54 / 20; 22 / 20 |
| 7 | 00:00-04:03; 10:15-16:28; 22:41-24:00 | 49 / 20 |

## Food and sale arithmetic

Hunger rises at 0.0625 per hour for the reference body, including sleep: 1.031 on the arrival day and 1.5 per full day. The ration is the cheapest whole-item combination covering that day's rise; ties choose less excess relief. Unused paid food can be eaten later, but each day's allowance is still purchased in full; hunger relief is never banked above fed. Body changes, traits, exercise, calories, stomach capacity and spoilage are outside this base hunger allowance.

| Food | Price | Hunger relief | Shelf line | Day 1 units | Full-day units |
|---|---:|---:|---|---:|---:|
| supply.bread | 4 | 0.3 | line.bread | 0 | 0 |
| supply.oatcakes | 2 | 0.15 | line.oatcakes | 0 | 1 |
| supply.tinned_beans | 3 | 0.35 | line.tinned_beans | 3 | 4 |

The island buyer has price level 0.9 and demand 1. A clam has base value 8, elasticity 0.45; each sale adds 1 supply and daily recovery is 0.5. SellPricing rounds and floors each sale, with MarketMath carrying the glut across days. One bait lot contains 10 units. Digging's EffortGate refuses at hunger 0.97 or tiredness 0.9. HeavyWorkNeeds is a separate stage-based requirement, not this numeric gate. Missing meals and refused visits are both reported.

## Scope and regeneration

This is a price-and-carry budget. It assumes the listed food and bait can be supplied; it does not promise shelf replenishment or subtract islanders' purchases. The cod licence has no island listing on this baseline. The dory and material offers are proposals, and physical pail sales and eating still have separate work in progress. Travel reach, safe routes, late sales, tide-window throughput, repairs, launch and the complete week need execution proof. The deficits above already exist under these optimistic access assumptions. No gameplay, asset, tuning or save change is made or required by this report.

The inputs are GameConfig, StPeters RegionDef, the scene's wallet/fee/market/pail/holes, the referenced PaintedHeightMap, the island seller and shelf definitions, catalogue licence/rod/bait/supply definitions, and FoodDefs. Only proposed prices are constants: PR 1539 for the hull and PR 1508 for one plank, one nail packet, one oakum and one paint tin.

[FirstWeekPurseTests](../../Assets/Tests/EditMode/FirstWeekPurseTests.cs) renders this entire page and compares it byte-for-byte after newline normalization. Run its `WriteDocument` editor entry point to regenerate after reviewing changed data. The fixture reuses TideModel, TidalExposure, PaintedHeightMap, BodyMath, EffortGate, BaitShop, SellPricing, MarketMath and the existing SceneYaml reader. It complements BaitEconomyPacingTests, TidePacingInvariantTests, ClamRefinementsTests and the food content tests. Known target deficits have exact assertions; they are not skipped tests.
