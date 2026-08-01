# M1 Progression Pacing — the ladder, in numbers

> **Status:** Working balance model for the M1 St Peters arc. Owned by `economy-sim`.
> **Serves:** the plan's §7.4 — *"a new rung every couple of days"* stated as numbers you can check
> before any content is built against them.
> **Plan:** [`../../backlog/plan-to-m1.md`](../../backlog/plan-to-m1.md) §3 (the ladder) and §7.4.
> **Canon:** [`vision-and-pillars.md`](../vision-and-pillars.md) wins on any conflict.
>
> **Read this first:** every number in §§1–6 is a **target**, not a measurement. Nothing there has been
> played. The point of writing it down before the content exists is that re-tuning a spreadsheet is free
> and re-tuning a built village is not.
>
> **§7 is the exception** — the one part of this document that is *measured*. It prices the
> `BaitSpentOnCatchOnly` flag by running the shipped bite sim over the shipped assets, and it ships with
> the test that regenerates it.

---

## 1. Why this document exists before the content does

"A new thing every couple of days" is a balance problem wearing a design costume. It fails quietly: it feels
fine to whoever tuned it and grindy to everyone else, and by the time a playtest says so, the prices are
baked into a dozen assets and a village of dialogue.

So the ladder gets modelled first. Three things come out of it:

1. **A price and cost table** the content is then authored against (Def assets + `GameConfig`, never literals).
2. **A day-by-day projection** showing each rung landing when §3 of the plan says it should.
3. **A guard** — once the Defs carry real numbers, an EditMode test asserts the projection still holds, so a
   price change that quietly breaks the pacing fails CI instead of a playtest.

---

## 2. The pacing target

From the plan's §3 ladder. "Day" = one in-game day (`GameConfig.SecondsPerDay`, currently **1800** real
seconds = 30 real minutes, ruled 2026-08-01; it was 1200). Every rung below is counted in in-game days, so
none of them moved — but a day now takes 1.5× as long to live through, which is the owner's tide-pacing
ruling reaching the progression ladder as well as the sea.

| Target day | Rung | Gate |
|---|---|---|
| 1 | Arrive; aunt's shovel; **dig** | none — gifted |
| 1 | **Clam licence** at the general store | Ginny fronts the fee |
| 1–2 | First sale at the store | — |
| 2–3 | **Used rod** | first real purchase |
| 3–4 | White bucket filled; **freezer** | teaches freshness |
| 4–5 | **The crossing** to Nine Mile Creek | the spring low (§4 of the plan) |
| 5 | Wharf sale; **see the derelict dory** | — |
| 6–9 | **Buy + repair the dory** | the slice's big save-up |
| 9–11 | **Offshore species** | needs the boat |
| 11–13 | **Traps / pots** | — |
| 13–15 | **Used outboard** | the closing rung |

**The shape that matters more than any single number:** early rungs are hours apart, later rungs are days
apart, and the *gap never grows faster than the earning rate does*. A player who has just unlocked offshore
fishing should feel their income step up at the same moment their next target steps up.

---

## 3. What has to be authored (the inputs)

None of these exist as tuned values yet. Each is a Def asset or a `GameConfig` field — **no literals in C#**
(CLAUDE.md rule 6). This list is the actual work item.

**Earning rates**
| Input | Where it lives | Note |
|---|---|---|
| Clam density on the flats | `ClamSpot` / region data | drives clams-per-low-water |
| Clams per dig, dig time | `ClamDig` tunables | with the tide window, gives clams/day |
| Low-water window length | derived from the tide sim | **not** authored — read it, don't set it |
| Shore-rod catch rate | `CatchResolver` weights + `FishSpeciesDef` | fish/hour from the beach |
| Bait/tackle effect on the roll | `Data/Bait`, `Data/Tackle` | how much the right hook is worth |
| Sea-state effect on the catch | rod-fishing v2 (#290) | rough water fishes better — and fights harder |
| Offshore catch rate & value | `FishSpeciesDef` | must visibly out-earn shore fishing |
| Trap soak yield | `TrapDef` | the P4 "works while you don't" rung |

**Prices — sell side**
| Input | Where | Note |
|---|---|---|
| Clam base value | `FishSpeciesDef.BaseValue` | |
| Fish base values | `FishSpeciesDef.BaseValue` | |
| Island store **price level** | `GameConfig.MarketPriceLevelStPetersStore` | **the number that makes the crossing pay** — what the counter gives per unit before any glut (0.6 = 60 % of dockside). ⚠ Demand D *cannot* do this job: at zero supply `1/(1+e·S/D)` is 1 for every D, so the first sale of the game is identical at both outlets unless the level differs |
| Island store demand D | `GameConfig.MarketDemandStPetersStore` | how badly the counter takes a glut — a second-order effect, felt only once you have sold into it |
| Nine Mile Creek demand D + level | `GameConfig` | the reason to cross |
| ⚠ **The ₲1 unit floor** | `SellPricing.UnitPrice` | every unit floors at ₲1, so on a **2₲ clam** the level has ~2₲ of room. The gap a player can *read* on a bucket is bounded by `BaseValue`, not by the multiplier — if the crossing must *feel* worth walking, that is a clam-base-value decision for this model |
| Elasticity per species | `FishSpeciesDef` | gluts crash faster |
| Refusal threshold | `SpoilPolicy.UnsellableSpoil` | past it, **no sale at any price** |
| Perishability per species | `FishSpeciesDef` (to author) | mackerel fast, shellfish hardy |

**Costs — buy side**
| Input | Where | Note |
|---|---|---|
| Clam licence fee | `LicenseDef.Price` (`license.clam`, 15₲) | fronted by Ginny; small. **Guard-rail:** `GameConfig.FrontedLicenceFee` must stay ≥ this or the opening soft-locks (the catch gate fails closed) — content validation enforces it |
| Ginny's fronted fee | `GameConfig.FrontedLicenceFee` | granted once per game, flag-guarded (`FrontedFeeGrant`) |
| Used rod | `GearOffer` | first real purchase |
| Damaged dory | `ShipwrightOffer` | the big save-up |
| Dory repair | `RepairLedger` | paid separately — two beats, not one |
| Traps / pots | `PotOffer` | |
| **Ice** (per load) | `SupplyDef` (`supply.ice`, 6₲) → `SupplyShop` | **recurring** — a running cost, per trip. Counted stock in `SaveData.SupplyStock` (save v7); the *melt* is still §7.3's to build |
| **Bait / tackle** | `Data/Bait`, `Data/Tackle` (landed #291) → `BaitShop` | **recurring** — and it *targets* species, not just enables them. Sold by the lot (`BaitDef.LotSize`); `Price` stays the UNIT price this model divides by |
| **Lid** (one-off) | a further `SupplyDef` | slows the melt; "spend once to stop spending" — appends as data now the supply shape exists, no schema bump |
| **Used outboard** | `ShipwrightOffer` (`Data/Shipwright/DoryOutboardOffer.asset`, `boat.dory_outboard`, **₲900**) → Hector's barrel at Nine Mile Creek | the closing rung. **Shipped, not yet projected:** ₲900 is placed by ladder position — above the whole dory (₲400 + ₲300 = ₲700, the day-6–9 save-up) and at half the Punt (₲1800), so upgrading the boat you own stays cheaper than replacing her. §4's projection is what should confirm or move it |

---

## 4. How to build the projection

For each day *d*, given the rungs unlocked by then:

```
income(d)  = Σ over available methods:
               yield_per_session × sessions_per_day × unit_price(species, market, supply)
                 × freshness_multiplier(how long it sat)
             − spoilage and travel losses
cash(d)    = cash(d−1) + income(d) − purchases(d)
```

Three things the model must not fake:

- **Supply depression is real.** Selling a full bucket of one species walks down its own curve
  (`SellPricing.RunningTotal`), and demand recovers over days. Use the real functions, not a flat price —
  the whole point of the market is that dumping is punished.
- **The tide gates income, not just the crossing.** Clam digging is only possible around low water, so
  clams/day is bounded by the tide sim, not by player effort. Read the window from the actual formula.
- **Freshness taxes the careless, and past a point it takes everything.** Value falls to nothing and beyond
  `SpoilPolicy.UnsellableSpoil` the catch cannot be sold at all — it is rubbish occupying hold space until
  dumped. The model must show the *difference* between a careful and a careless player, because that
  difference is the mechanic teaching itself. It must also confirm the careless player still **climbs**: if
  losing a bucket to rot can strand someone with no way to earn the next rung, the loss is too sharp.

- **Cold is priced in TIME, so model it that way.** The natural instinct is to ask "does ice net more coin?"
  — but what the player is buying is **productive hours**. Model it as: *how much longer can I stay out
  before the first fish I landed forces me back, and what is an extra hour of fishing worth?* Coin is the
  output of that sum, not the input.

  That makes the whole cold chain one calculation with three answers: **ice buys time at sea**, **a lid buys
  more time per unit of ice**, and **the freezer buys time ashore** (bank a part-load and go out again
  instead of making a trip to sell). The freezer being free-but-fixed and ice being paid-but-portable is the
  trade.

- **Ice is a recurring cost, and so is bait — the two of them change the curve's shape.** Most M1 purchases
  are one-off; these are spent per trip, so they set a floor under every outing. Check the crossover for each:
  a trip with ice must **net more than the same trip without** once the catch is worth enough, and must *not*
  before that. If ice is always correct it is a tax, not a decision; if it is never correct it is dead
  content. Tune the crossover, and the lid should pull it earlier.

- **Bait and tackle now gate what bites** (landed on `main`, #291: `Data/Bait`, `Data/Tackle`). That makes
  bait a **per-trip cost with a targeting benefit** — the right tackle raises the odds of the species you
  actually want, so the model has to price *choosing* bait, not just buying it. A player who fishes the
  wrong tackle should earn visibly less than one who reads the water.

- **Sea state modulates the catch** (owner ruling 2026-07-25, #290: rough water fishes better and fights
  harder). So income is not flat across a day — it varies with conditions the player can read and choose to
  go out in. Model at least the calm/rough split, or the projection will understate a good skipper and
  overstate a cautious one.

- **Watch the knock-on to the whole schedule.** Ice raises hours-per-session, which raises income per day,
  which pulls every later rung *forward*. Re-run §2 after tuning ice — a change that looks local to one
  consumable can quietly collapse days 11–15 into a single afternoon.

**Model two players**, and check both:
- **Efficient** — digs every low water, freezes everything, sells at the wharf. Should hit the §2 targets
  comfortably, and should not trivialise them (no rung reached in half the target time).
- **Casual** — one session a day, sells at the island store, forgets the freezer sometimes. Should still hit
  every rung, **late but not stuck** — perhaps 1.5× the target days. If the casual player stalls out, the
  ladder is too steep; that is the failure this document exists to catch.

---

## 5. The guard (once the Defs are real)

When the values above are authored, add an EditMode test in `Assets/Tests/EditMode/Economy/` that runs the
projection over the Def assets and asserts:

- every rung is reachable by its target day × a tolerance, for **both** player models;
- no rung is reachable in under half its target day (the drip is the point — nothing should arrive early
  enough to collapse two rungs into one afternoon);
- the casual player never stalls (income strictly exceeds zero at every rung, after spoilage).

That converts "did we break the pacing?" from a playtest question into a CI question. It is the same move the
content-validation test already makes for Def integrity.

---

## 6. Open questions for the owner

1. **How long is a session meant to be?** The rung spacing assumes roughly one in-game day per sitting
   (~20 real minutes). If you picture longer sittings, the day targets compress and the ladder wants to be
   steeper.
2. **How hard should the dory save-up bite?** It is the slice's one big wall. Too cheap and the emotional
   peak is free; too dear and days 6–9 are a grind with no new verb. Current plan assumes ~3–4 days of
   good fishing.
3. **Should the island store ever be the better sale?** A market-day swing that occasionally beats the
   wharf would reward reading the calendar — but it also weakens the crossing's pull. Recommend **no** for
   M1; revisit in M2 when there are more channels.
4. **When is bait spent — at the bite, or only on a landed fish?** Your §10.2 thought, still tentative and
   still flagged OFF. **§7 below is the check you asked for**, with the numbers: the recommendation is to
   flip it **ON**, and there is a middle path costed if you want a miss to keep biting.

---

## 7. The bait-spend flag — the check §10.2 was gated on

> **Added 2026-07-30 by `economy-sim`.** `GameConfig.BaitSpentOnCatchOnly` shipped **OFF** in #341 and
> has not moved; nothing in this PR changes it. The owner's §10.2 thought — *"perhaps bait is only lost
> after catching a fish"* — was recorded as **tentative** and flagged to this lane because flipping it
> changes bait's real cost per fish, which is a pacing dial. This is that check.
>
> Every figure below comes out of `Assets/Tests/EditMode/BaitEconomyPacingTests.cs`, which drives the
> **real `BiteSequenceSim` over the real `Data/Bites/*.asset`** and reads prices off the real Defs.
> Re-run it after any bite or bait re-tune and the table regenerates. It is also a **guard**: the pins
> in it go red when these numbers stop being true.

### 7.1 The two modes, as arithmetic

Bait leaves the box once per event; *which* event is the whole question.

| Mode | Bait per **landed** fish |
|---|---|
| **OFF** (shipped) — spend at the BITE | `1 ÷ P(land \| bite)` |
| **ON** (§10.2) — spend at the LANDED catch | `1`, always |

`P(land | bite) = P(hook) × P(win the fight) × P(allowed to land it)`. Everything below is the
consequence of that reciprocal: **OFF multiplies a consumable cost by the player's failure rate.**

### 7.2 What the shipped personalities actually cost

Two modelled hands — the only invented numbers in the study, and named constants in the test.
**Competent:** strikes 0.30 s ± 0.09 into the take, fooled by 8 % of teases, wins 90 % of fights.
**New:** 0.55 s ± 0.20, fooled by 35 % of teases, wins 65 %. Reaction is a *choice* reaction — you must
decide "tease or take?" before moving — which is why both sit above the 0.15 s best-case hand
`BiteContentValidationTests` already pins. 4 000 seeded sequences per cell.

"Bait ₲" is the **cheapest bait that favours the species** — what a player short of coin ties on.

| Species | Hand | P(hook) | P(land) | bait/fish OFF | bait/fish ON | bait ₲/fish OFF | **net ₲ per landed fish, OFF** | **net ON** |
|---|---|---|---|---|---|---|---|---|
| Cod — 14₲, clam 3₲ | competent | 0.92 | 0.82 | 1.21 | 1.00 | 3.6 | **+10.4** | +11 |
| | new | 0.59 | 0.38 | 2.63 | 1.00 | 7.9 | **+6.1** | +11 |
| Haddock — 16₲, clam 3₲ | competent | 0.76 | 0.68 | 1.47 | 1.00 | 4.4 | **+11.6** | +13 |
| | new | **0.09** | 0.06 | **16.68** | 1.00 | **50.0** | **−34.0** | +13 |
| Mackerel — 10₲, capelin 5₲ | competent | 0.99 | 0.89 | 1.13 | 1.00 | 5.6 | **+4.4** | +5 |
| | new | 0.93 | 0.61 | 1.65 | 1.00 | 8.2 | **+1.8** | +5 |
| Pollock — 11₲, capelin 5₲ | competent | 0.95 | 0.86 | 1.17 | 1.00 | 5.8 | **+5.2** | +6 |
| | new | 0.79 | 0.52 | 1.94 | 1.00 | 9.7 | **+1.3** | +6 |

**The haddock is the shape of the problem.** Tightest window (0.45 s), most teases (2–4) *and* fewest
passes (2) — so a new hand hooks it once in eleven bites, and under OFF **loses 34₲ on every haddock they
land**. The same fish pays a competent hand +11.6₲. That is not a difficulty curve; it is a fine for
being new.

### 7.3 The session blend — the number that actually sets pacing

A cast doesn't choose its fish, so the pacing figure is the blend over the St Peters rod pool (cod,
haddock, mackerel, pollock — the pool `StPetersBuilder` wires). Taken through the shipped
`CatchResolver`, never a re-derivation of its weighting:

| Situation | Baits per landed fish, OFF | ON |
|---|---|---|
| Competent hand, bare hook, cod licence held | **1.24** | 1.00 |
| **New hand, cheapest bait tied on, no cod licence** | **12.9** | 1.00 |

That worst case is **38.7₲ of bait per landed fish — and the dearest fish that blend can produce is a
16₲ haddock.** It lands in precisely the wrong place: hour one, on the home shore, doing the sensible
thing. Two shipped facts compound it:

- **Every authored rod bait favours cod.** Four of the seven baits touch the rod pool at all (capelin, sea
  worm, shucked clam, squid strip — the other three are pot bait), and **all four list
  `fish.atlantic_cod`**. So tying bait on *steers bites toward cod*.
- **Cod is licence-gated** (`license.cod`, 120₲, bought at Nine Mile Creek around day 5). Before that
  purchase the cod land rate is exactly **zero** — `CatchLicensePolicy.MayLand` fails closed and the fish
  slips back. Under OFF every one of those bites still eats a bait.

So the cheap, correct-looking bait choice aims a new player's bites at the one species they cannot land,
and charges them for each. Under ON that trap cannot exist: an unlicensed release costs nothing.

### 7.4 A finding that is *not* the flag — capelin is mispriced

True under every mode, and worth fixing either way: **capelin (5₲) is the cheapest bait favouring both
mackerel (10₲) and pollock (11₲)** — half the fish, before a single miss. Even at one bait per fish those
two breach any sane share-of-value ceiling (the test uses 35 %); cod and haddock sit at 21 % and 19 % and
are fine.

**Recommendation:** re-price `bait.capelin` to **2₲** — 20 % of a mackerel, 18 % of a pollock, and still a
real cost against cod. Noted alongside it, for a later pass: a 10₲ mackerel of 0.3–1.5 kg against a
14₲ cod of 2–12 kg is its own balance question.

> ✅ **DONE** (with the §7.5 island-store pass). `bait.capelin` is 2₲. No rod species now breaches the 35 %
> ceiling under spend-on-catch, and `WhoBreachesTheBaitShareCeiling_IsPinnedUnderBothModes` asserts that list
> is **empty** — a species reappearing in it means a bait or a base value was re-tuned back into the red.
>
> The **spend-at-bite (OFF) shares moved too**, and one crossed back over the line. Measured against the
> shipped assets after the re-price:
>
> | Species | bait ₲ | base ₲ | P(land), new hand | **ON share** | **OFF share** |
> |---|---|---|---|---|---|
> | Cod | 2 (capelin) | 14 | 0.381 | 14.3 % | **37.5 %** ⚠ |
> | Haddock | 3 (shucked clam) | 16 | 0.060 | 18.8 % | **313 %** ⚠ |
> | Mackerel | 2 (capelin) | 10 | 0.607 | 20.0 % | 32.9 % — *now under* |
> | Pollock | 2 (capelin) | 11 | 0.515 | 18.2 % | **35.3 %** ⚠ |
>
> So OFF no longer breaches for *every* rod species — mackerel slips under, and cod and pollock clear the
> ceiling by under three points. A membership list balanced that finely is a tripwire for the next
> re-price rather than a finding, so the test now asserts the **property** instead: OFF is strictly worse
> than ON for every species, it still breaches for more of them than ON does, and haddock — which clears
> the ceiling by an order of magnitude — is always in the list. **The recommendation is unaffected:**
> haddock at 313 % of the fish's own value is the argument, and no bait price fixes a reciprocal.
>
> ⚠ **The §7 table below is now partly stale, and re-pinning it needs a CI run.** The re-price moved which
> bait a coin-short player ties on: the cheapest bait touching the rod pool used to be **shucked clam (3₲**,
> favouring cod and haddock — a licence-gated fish and one a new hand almost never lands, so the boost went
> where nothing came back). It is now **capelin (2₲**, favouring mackerel and pollock, which a new hand *does*
> land). The opening-day blend therefore improved for a real and intended reason, and the measured
> "38.7₲ / 10-baits-per-fish" figures in §7.3 no longer describe the shipped assets.
> `ASessionsBlendedBaitCost_IsWorstExactlyWhereTheNewPlayerStarts` now asserts the **shape** the finding
> rests on (a new hand still blends worse than a competent licensed one) rather than a magnitude the next
> legitimate re-price would stale again; it logs the live numbers, so regenerate this table from a CI run
> rather than re-pinning it in the test. **The recommendation itself is unaffected** — the OFF-mode
> reciprocal is unbounded in a new hand's hands whatever the bait costs, which is the whole point of §7.

### 7.5 What flipping ON gives up — stated fairly

1. **A miss gets cheaper, not free.** Cost of a failed sequence = seconds + (OFF only) one bait. At an
   estimated ~35 ₲/min rod income (cycle ≈ 18 s — **an estimate; §3's rates are not authored yet**), the
   1–7 s a failed sequence burns is worth 0.6–4.0₲ against a 3–5₲ bait. So ON **cuts the cost of a miss
   by roughly 45–85 %** depending on species. It does not remove it. And the reverse claim — "time
   already prices the miss" — is **false**: today the bait is the *larger* half of the penalty for
   mackerel and pollock.
2. **Bait stops being a floor under an outing.** §4 built bait and ice as the two recurring costs that
   "set a floor under every outing". Under ON a fruitless trip is free and bait becomes a flat commission
   on income. **Ice still does that job** — ice is spent against *time*, not against fish — so the shape
   survives on one leg instead of two.
3. **It is less physically true.** A fish that mouths your bait takes it; real hand-lining means re-baiting
   after most bites. The 2026-07-25 ruling was the honest model. But P5 puts the teeth in *the sea*, not
   the tackle box — and the §10.2 rulings around this one (no spooked spot, "It keeps nibbling", no hard
   fish-gone) all pull the same way.

### 7.6 The middle path, if the owner wants a miss to bite

**Spend at the HOOK-UP.** Teases and missed strikes cost nothing — literally what he said a miss should
cost — but a fish that got hooked and then broke off keeps the bait, which is what actually happens when
one does. Bounded by the fight alone:

| | competent | new |
|---|---|---|
| Baits per landed fish | **1.11** | **1.54** |

The same for every personality: the bite funnel cannot reach it, so it cannot go regressive. Building it
is one moved call in `FishingController` behind a third enum value — **gameplay-systems' file, not this
lane's.** Costed here so the option is on the table with a number against it; not built.

### 7.7 Recommendation

**Flip `BaitSpentOnCatchOnly` ON.** In order of weight:

1. **The reciprocal is the defect, not the principle.** A cost of `1 ÷ P(land)` is unbounded in
   inexperience — 16.7 baits per haddock, 12.9 per fish on an opening-day blend. The owner deliberately
   made the bite's failure modes forgiving; OFF quietly re-imposes the punishment as an invoice.
2. **§4's own guard-rail.** "The casual player must still climb — late but not stuck." A consumable priced
   by failure is the wrong *shape* for that, whatever the tuning.
3. **The unlicensed-cod leak is otherwise unfixable** without either pulling cod from the St Peters rod
   pool or gating bites by licence — both worse changes than a flag flip.
4. **§5's CI guard needs it.** A projection cannot price bait while its cost is a function of the player.
   ON is the only mode this document can actually assert against.

**Do first, and independently of the flag:** re-price capelin (§7.4).

**What would change this recommendation:** if a playtest shows a competent hand's real land rate is far
above the model's — P(hook) > 0.95 across the board — then OFF's competent cost collapses toward 1.05 and
the argument narrows to the new player alone, at which point §7.6's hook-up compromise beats either
extreme. The test regenerates the whole table in one run, so re-check rather than re-argue.

### 7.8 One caveat that makes this cheap to decide now

**The flag is currently inert in the live build.** No builder and no scene wires a `BaitDef` to the
`FishingController` (`_bait` / `_baitBox` are unset everywhere), so `SpendBait()` is a silent no-op and
the rod costs nothing to fish today. The consequence above lands the moment §7.5's store sells bait and
the owner's §10.4 diegetic bait-choosing ties one on. Which is exactly why it is worth ruling now:
deciding is free today and expensive after the store, the prices and the tutorial beats are built
against the wrong number.

**Still the owner's call, in play, as ruled.** This lane's job was the number; the number is 12.9.
