# M1 Progression Pacing — the ladder, in numbers

> **Status:** Working balance model for the M1 St Peters arc. Owned by `economy-sim`.
> **Serves:** the plan's §7.4 — *"a new rung every couple of days"* stated as numbers you can check
> before any content is built against them.
> **Plan:** [`../../backlog/plan-to-m1.md`](../../backlog/plan-to-m1.md) §3 (the ladder) and §7.4.
> **Canon:** [`vision-and-pillars.md`](../vision-and-pillars.md) wins on any conflict.
>
> **Read this first:** every number below is a **target**, not a measurement. Nothing here has been played.
> The point of writing it down before the content exists is that re-tuning a spreadsheet is free and
> re-tuning a built village is not.

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

From the plan's §3 ladder. "Day" = one in-game day (`GameConfig.SecondsPerDay`, currently 1200 real seconds).

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
| Offshore catch rate & value | `FishSpeciesDef` | must visibly out-earn shore fishing |
| Trap soak yield | `TrapDef` | the P4 "works while you don't" rung |

**Prices — sell side**
| Input | Where | Note |
|---|---|---|
| Clam base value | `FishSpeciesDef.BaseValue` | |
| Fish base values | `FishSpeciesDef.BaseValue` | |
| Island store demand D | `GameConfig` | **deliberately worse than the wharf** |
| Nine Mile Creek demand D | `GameConfig` | the reason to cross |
| Elasticity per species | `FishSpeciesDef` | gluts crash faster |
| Refusal threshold | `SpoilPolicy.UnsellableSpoil` | past it, **no sale at any price** |
| Perishability per species | `FishSpeciesDef` (to author) | mackerel fast, shellfish hardy |

**Costs — buy side**
| Input | Where | Note |
|---|---|---|
| Clam licence fee | `LicenseDef.Price` | fronted by Ginny; small |
| Used rod | `GearOffer` | first real purchase |
| Damaged dory | `ShipwrightOffer` | the big save-up |
| Dory repair | `RepairLedger` | paid separately — two beats, not one |
| Traps / pots | `PotOffer` | |
| **Ice** (per load) | store `GearOffer` / consumable | **recurring** — the only running cost in M1 |
| **Lid** (one-off) | store `GearOffer` | slows the melt; "spend once to stop spending" |
| **Used outboard** | new `ShipwrightOffer` | the closing rung |

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

- **Ice is the first recurring cost, so it changes the shape of the curve.** Every other purchase in M1 is
  one-off; ice is spent per trip. The model must show that a trip with ice **nets more than the same trip
  without it** once the catch is worth enough — and that early on, when it isn't, going without is the right
  call. If ice is always correct it is a tax, not a decision; if it is never correct it is dead content. The
  crossover point is the number to tune, and the lid should move it earlier.

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
