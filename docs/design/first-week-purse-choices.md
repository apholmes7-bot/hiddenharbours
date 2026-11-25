# The first week's purse: what "short" means, and three ways to close it

Status: **every number in the three price sets below is a PROPOSAL.** No price data is changed by this
page. The owner picks a set (or a mix); the data change is then one small follow-up.

The generated day-by-day model of the same week lives in `docs/design/first-week-purse.md`, written by
`FirstWeekPurseTests` (open draft). This page reads the same data, checks it by hand, and adds the choices.

## 1. The bottom line

| | Coins |
|---|---:|
| What he starts with (wallet 0 + Ginny's fronted fee 20) | 20 |
| Careful player's clam income, days 1-7 (13 bucket loads x 20 clams x 1 coin) | 260 |
| **Money in** | **280** |
| Every purchase the week's end forces (section 3), cod licence included | 295 |
| Food to stay fit to dig (section 4) | 93 |
| **Money out** | **388** |
| **Short at the word on day 7** | **108** |

**The dory's mending supplies are not bought (owner ruling, 2026-11-25).** The patch plank, copper nails,
oakum and linseed paint (60 coins together in the earlier draft of this page) are given to him by three
people through conversation, and LeBlanc's does not sell them. They are out of every figure on this page.

**Correction to the earlier trial figure.** The trial said "about 475 needed against 20 plus about 260",
which is 195 short. That counted the cod licence twice, and it left out food. With the supplies gone
too, the true figure is **388 needed against 280: 108 short.** Without food it would be 15 short, but a
hungry player is refused heavy work (digging refuses at hunger 0.97), so food is not optional.

An ordinary player (one bucket a day) earns 140 and is 228 short.

The careful player reaches the rod on day 2, bait on day 3 and the dory on day 6, ends day 7 with 12
coins, and is 108 short of the cod licence. The cod licence is the whole gap.

## 2. What he starts with

| Item | Coins | Where it comes from |
|---|---:|---|
| Wallet at New Game | 0 | `PlayerWallet._money` in `Assets/_Project/Scenes/StPeters.unity` |
| Ginny fronts the shellfish licence fee, once | 20 | `GameConfig.FrontedLicenceFee` (`Assets/_Project/Data/Config/GameConfig.asset`) |
| Shovel | free | placed in the world; its catalogue offer `gear.shovel` (25) is unlisted (`Data/Resources/Catalog/Gear/Shovel.asset`) |
| Bucket (20 clams) | free | placed in the world, `_capacity: 20`; its offer `gear.bucket` (15) is unlisted (`Catalog/Gear/ClamBucket.asset`) |

## 3. Every purchase the week's end forces

Ginny gives her word only when he holds `boat.dory` repaired, holds `license.cod`, has kept fish from the
dory and has sold shellfish (`IslandWeek.asset`). Working back from that:

| Purchase | Coins | Asset | On main? |
|---|---:|---|---|
| Shellfish licence | 15 | `Data/Resources/Catalog/Licenses/ClamLicense.asset` | yes, LeBlanc's counter |
| Rod | 60 | `Data/Resources/Catalog/Gear/Rod.asset` | yes, LeBlanc's |
| Bait, one lot of 10 capelin | 20 | `Data/Resources/Catalog/Bait/Capelin.asset` (2 each, lot of 10) | yes, LeBlanc's shelf |
| Basil's dory, on the slip | 80 | `Data/Regions/StPetersVillage/slipsale.basils_dory.asset` | open draft (1539) |
| Cod licence | 120 | `Data/Resources/Catalog/Licenses/CodLicense.asset` | yes; sold at LeBlanc's from this change, and still at the Nine Mile Creek harbourmaster |
| **Total** | **295** | | |

Not bought: the four mending supplies the dory's repair needs (patch plank, copper nails, oakum, linseed
paint; 24, 8, 12 and 16 in open draft 1508). They are given through dialogue by three people, and
LeBlanc's does not stock them (ruling 2026-11-25). One bait lot is assumed to be enough to keep a fish
from the dory.

## 4. Food

From the shipped Defs: hunger rises 0.0625 an hour (`GameConfig`), so a full day needs 1.5 and the
arrival day (from 07:30) needs about 1.03. The cheapest whole-item meals on LeBlanc's shelf
(`Data/Resources/Food/*.asset`, prices in `Catalog/Supplies/*.asset`):

| Food | Price | Hunger relief |
|---|---:|---:|
| Tinned beans | 3 | 0.35 |
| Oatcakes | 2 | 0.15 |
| Bread | 4 | 0.30 |

Arrival day: 3 tins = 9. Each full day: 4 tins + 1 oatcake = 14. Week: 9 + 6 x 14 = **93**.

## 5. What a careful player earns

**Clams are the week's income, and every clam sells for exactly 1 coin.** At LeBlanc's the price is
`base 2 x price level 0.6 x glut factor` (`SellPricing`, `MarketMath`, `GameConfig.MarketPriceLevelStPetersStore`
and `MarketDemandStPetersStore` 0.7). The first clam is 2 x 0.6 = 1.2, which rounds to 1, and the
price never goes below 1. So the glut never matters and income is simply clams carried.

Assumptions (the same as the generated model):

- one bucket load (20) per low tide, carried and sold before the next tide;
- the careful player works both daylight low tides: two loads a day on days 1-6, one on day 7 (the
  evening tide falls after the word at 17:00 or after bedtime), 13 loads;
- the flats have plenty of holes: 76 placed holes, one clam each per tide, and the daytime count stays at
  22 or more all week, so the bucket, not the beach, is the limit;
- the clam licence is bought on day 1 before any clam is kept.

Days 1-6: 12 loads = **240**. Day 7's morning tide: 20 more = **260** by the word.

**Rod fish add almost nothing at today's island prices.** With `BaitSpentOnCatchOnly` on, a landed fish
costs one capelin, 2 coins. A mackerel (base 10, the best common rod fish here) fetches 6, 4, 3, 2, 2 on
a fresh day and settles at 2 after a glut, which is the bait's cost. Five mackerel a day for six days
earn 68 against 60 of bait: **about +8 for the week.** Striped bass pays better per fish (10, 6, 4, 3)
but bites rarely. Assumption: the store buys finfish at the same level and demand (no category limit
was found in the selling code).

Not counted, and both could help a careful player:
- a second bucket trip inside one low tide (about six hours) if walking time allows; early in the week
  there are 76 clams on the flats and the model takes 20;
- eating his own fish instead of buying beans: a grilled mackerel relieves 0.45 hunger, more than a tin
  of beans (3 coins), if he can cook on the island.

## 6. Three ways to close the gap (PROPOSAL)

Each set is a short list of data edits. "Careful" and "ordinary" are the same two players as above;
the purchase order is licence, rod, bait, dory, cod licence, each bought as soon as the purse allows.
Without any set, the careful player is 108 short and the ordinary player 228 short.

### Set 1: cheaper big tickets

| Data | Today | PROPOSAL |
|---|---:|---:|
| `CodLicense.asset` Price | 120 | 30 |
| `slipsale.basils_dory` Price (1539) | 80 | 40 |
| `Rod.asset` Price | 60 | 40 |

Purchases fall from 295 to 145. Careful: rod and bait on day 2, the dory on day 4, the cod licence on
day 5, and **42 coins to spare** at the word. Ordinary: **still 78 short** (he reaches the rod on day 5
and nothing after it).

What it leaves: the careful week closes with room; the ordinary week does not.

Feel: the week stays a clam grind with a steady drip of purchases; every day buys something. The cost
is elsewhere: there is one cod licence Def, so the harbourmaster's price drops to 30 too, and a 30-coin
licence no longer feels like a milestone. A rod at 40 also cheapens the mainland chandlery's rod.

### Set 2: better clam and fish prices on the island

| Data | Today | PROPOSAL |
|---|---:|---:|
| `Data/Fish/SoftShellClam.asset` BaseValue | 2 | 7 |
| `GameConfig.MarketPriceLevelStPetersStore` | 0.6 | 0.9 |

Clam income does not move smoothly: it is 1 coin a clam until base x level reaches 6, then 2 coins a
clam (the first clam of the day fetches 6). With 7 x 0.9 = 6.3 the careful week earns **527** instead of
260. Careful: rod and bait on day 1, the dory on day 3, the cod licence on day 4, and **159 coins to
spare** at the word. Ordinary: 287 earned, **81 short** (he has the dory on day 6 but not the cod
licence). Rod fish at level 0.9: about +25 for the week.

What it leaves: the careful week has a large surplus; the ordinary player still falls 81 short.

Feel: digging pays, the week relaxes after day 3, and the careful player ends with money to spend.
Side effects: the island still pays less than Nine Mile Creek for the first clam (6 against 7), so the
"where you sell matters" lesson holds, but a bucket of clams at Nine Mile Creek rises from about 22 to
about 54, which makes clams a strong earner for the rest of the game. A smaller step (base 5, level 0.9)
earns nothing extra at all, because 4.5 is under the 6 threshold.

### Set 3: Ginny and Basil front part, paid back from the catch

| Data | Today | PROPOSAL |
|---|---:|---:|
| Basil lets him take the dory now, owed | 80 cash | 0 now, 80 owed to Basil |
| Ginny fronts part of the cod licence (like the shellfish fee) | 0 | 60 fronted, owed |
| Share of each sale that goes to the debt, starting after Ginny's word | - | 25% |

(The mending supplies were part of this set in the earlier draft; they are now given, so there is
nothing left to front there.)

Cash purchases fall from 295 to 155 (licence, rod, bait, half the cod licence). Careful: the dory is his
as soon as he asks, the rod on day 2, bait on day 3, the cod licence on day 5, **32 coins to spare**, and
he ends the week owing 140. (If the 25% share started on day 1 instead, the careful week would lose 65
of its 260 and fall 33 short.) Ordinary: **still 88 short** (the rod and the cod licence's cash half are
out of reach).

What it leaves: the careful week closes and carries a 140 debt into week two; the ordinary week does
not close. Because the supplies are now given, this set gains nothing from the ruling: it was already
fronting them.

Feel: the most "Living Working Coast" of the three: the island carries him, and the second week has a
purpose (pay Basil and Ginny back). It keeps every price as it is, so nothing at Nine Mile Creek moves.

**Saved state.** A debt paid back from the catch **needs a new saved field**, because the save holds only
yes/no flags and lists of ids, not a running balance:

- `SaveData.Debts`: a list of `{ CreditorId: string (for example npc.basil), Owed: int }`, with a schema
  bump and a migration that gives older saves an empty list;
- the amounts fronted and the share of each sale come from data (a Def or `GameConfig`), not the save.

A simpler variant needs **no new field**: each front is a one-off grant guarded by a flag (exactly how
Ginny's shellfish fee works today), and the debt is settled in one payment at a later beat, recorded by a
second flag. That loses the "a share of every sale" feel and cannot record a part payment. Nothing here
is built.

## 7. Where the numbers come from

Read from data on main at `98f9f3c55a` plus the open drafts named in section 3. The week's money was
worked with the shipped formulas (`SellPricing.UnitPrice`, `MarketMath.PriceMultiplier`, daily recovery
`GameConfig.MarketDailyRecovery` 0.5, one supply per sale) in a hand model of 20-clam loads and the
purchase order above. The generated model in `first-week-purse.md` (written before the supplies ruling)
gives the same 260 income and 93 food; its 355 purchases and 168 short still include the 60 of supplies.
Not checked in a playthrough: walking and selling time, whether two trips fit in one tide, bite rates,
and cooking on the island.

### At a glance (supplies given, not bought)

| | Careful | Ordinary |
|---|---:|---:|
| No change | 108 short | 228 short |
| Set 1, cheaper big tickets | +42 | 78 short |
| Set 2, better island prices | +159 | 81 short |
| Set 3, Basil and Ginny front | +32, owing 140 | 88 short |
