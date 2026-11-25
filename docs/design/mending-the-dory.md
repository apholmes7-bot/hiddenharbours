# Mending the dory

The accepted three-job plan has the player buy the broken dory on the St Peters slip,
carry her materials there, and patch, caulk and paint her himself. The proposed
materials originally cost **PROPOSAL 60 coin** and the three jobs take **75 game minutes**. The current
mending draft describes a hull supported on dry ground above high water: these
three jobs do not wait for low tide. Completing them records a repaired boat but
does not move or float her. A supported route from the slip to the water remains
separate unfinished work.

The patch, caulk and paint sequence on days 4–5 is accepted within the island-only
first week. Prices remain **PROPOSAL**, and the calendar describes the intended
beat rather than a forced gate. The purse alternatives below separate the accepted
hand work from the unsettled cost and from unfinished physical integration.

The [first-week direction](../adr/0062-the-first-week-is-closed-by-weather.md)
keeps him on St Peters until an islander's word and suitable weather open the way
around day 7. Every retained catch needs its licence. This beat does not send him
to the mainland for materials, licences or paid repairs.

## The three jobs as he lives them

The sequence below gives him time for ordinary island life between jobs. It is an
example, not a forced calendar gate: all three jobs may fit in one daytime visit
if he brings everything and chooses to stay. Nobody performs a job for
him, and nothing arrives from a remote inventory.

| Job | What he carries to the slip | Hand work | Material cost |
| --- | --- | --- | --- |
| Patch the plank | One patch plank and one packet of copper nails | 30 game minutes | PROPOSAL 24 + 8 = 32 coin |
| Caulk the seam | One hank of oakum | 25 game minutes | PROPOSAL 12 coin |
| Paint the patch | One tin of linseed paint | 20 game minutes | PROPOSAL 16 coin |
| Total | Four purchased units | 75 game minutes | PROPOSAL 60 coin |

These are the authored proposals in the
[mending rules draft](https://github.com/apholmes7-bot/hiddenharbours/pull/1508).
They are new prices, not prices already sold on main. The items have stable ids
`supply.patch_plank`, `supply.copper_nails`, `supply.oakum` and
`supply.linseed_paint`; each job has its own Def. They currently have no art and no
shop listing. A hammer, caulking iron and brush are proposed reusable implements
at the slip, with their ownership and placement still to settle; they are not
silently added as consumable costs.

**Day 4, the patch.** Having bought the dory, he collects the
materials and carries them down himself. The plank is a carried object; the small
items can travel in the paper bag. He stands on dry ground beside the damaged
plank, outside the supported hull. Starting reserves the plank and nail packet
against other use; they remain owned until the job completes, when the whole
reservation is consumed once. The physical hand-work adapter still needs to
connect that reservation to the goods he actually carried. An unfinished patch
keeps its partial work while paused in the same session; a finished patch should
be visibly fitted and fastened. That visible
change is a brief for later art and placement, not an accepted appearance.

**Day 4, the seam.** He brings the oakum within reach
and works along the patched seam from the dry side of the hull. The earlier patch
must be finished. The seam should read as packed when this job is done. He can
pause for errands and resume during the same session, or end the session and
restart the unfinished job with its materials still owned. The rules support
separate visits without requiring high-water waits or night work.

**Day 5, paint.** He brings the tin back to the same
supported boat, opens it at the work position and coats the repaired area. The
completed patch should visibly read as cared for; colour, finish, wetness and the
exact hull changes belong to the art work. Twenty minutes completes the job and
marks `boat.dory` repaired. There is no extra 300-coin repair bill on this path.

The mending draft keeps **−0.2 m work** and **0.8 m launch** thresholds for wet-slip
rules, but its three island job assets set `NeedsLowWater` false and its island
sequence sets `RequiresSupportedLaunch` true. Those heights are not a way to
float a hull standing above high water. The generic wet-job rule still stops work
at the first flood and uses strict threshold comparisons; it is not the rule for
these three dry jobs.

He could patch at 08:30–09:00 and caulk at 09:10–09:35 on day 4, then paint at
09:00–09:20 on day 5. These are example appointments, not measured play pacing.
He can use the rest of the day for clams, food and visiting, but no afternoon tide
automatically launches the boat. The separate launch work must maintain support
through movement, establish flotation, and only then enable sailing and clear
the slip state. No physical launch or paint-curing timer is implemented by the
logic draft. The [dry-slip placement draft](https://github.com/apholmes7-bot/hiddenharbours/pull/1539)
and the mending draft are still separate, unmerged work.

## Prices that fit the week

**PROPOSAL — every price alternative, day, purse, quantity and result in this
section is a conditional budget, not an accepted balance value.** Reference values
below are identified separately as existing data. No asset or configuration value
is changed here. The three jobs are accepted; their prices are not.

The model is [the first-week purse](https://github.com/apholmes7-bot/hiddenharbours/pull/1558),
read at `c56f56d8c53ab7fb1c1bd4f230e722ca261aabe8` and checked against pinned main
`f808af6ae5d5a0041f3d2147d74b1ddef7dce609`. Its `FirstWeekPurseTests` already owns
this model; this document does not introduce another runtime budget or test fixture.
The earlier optimistic cod-income example is superseded.

Existing data: the initial fee leaves `20 - 15 = 5` coins after the shellfish
licence. Rod costs 60; capelin bait costs 20 for a lot of 10; the cod licence costs
120 and has no island seller. A fresh clam pays 1 coin after marginal sale pricing
and its minimum-price rule. A pail carries 20. Careful visits produce 40 coins on
each of days 1–6 and 20 on day 7, for 260; ordinary visits produce 20 daily, for
140. No fish earnings, additional fee grant, free rod or off-island trade is added.

The existing cheapest whole-item ration is three bean tins on arrival (9 coins)
and four bean tins plus an oatcake on each later day (14), totalling 93. Food is
funded before progression purchases. Purchase order remains shellfish licence,
rod, bait, hull, plank, nails, oakum, paint, cod licence. Purchases are end-of-day
cash earmarks, as in the source model; they do not establish opening hours or that
work can finish that evening. Carrying, repairs and supported launch still take time.

Independent arithmetic reproduces the source's careful closing purses
`36, 2, 8, 34, 60, 6, 12` and ordinary purses `16, 22, 28, 34, 40, 46, 52`.
The original **PROPOSAL** hull/material set gives the reported deficits:
`15 + 60 + 20 + 80 + 24 + 8 + 12 + 16 + 120 + 93 - 20 - 260 = 168`
for the careful full list; `60 - 28 = 32` for the ordinary rod on day 3.
The mainland yard's existing 400-coin offer is a different route, not the island
hull's **PROPOSAL 80** reference price.

### Alternative prices — all PROPOSAL, in coins

| PROPOSAL set | Hull | Plank | Nails | Oakum | Paint | Hull + materials | Other changes needed |
|---|---:|---:|---:|---:|---:|---:|---|
| A: least retuning | 60 | 12 | 4 | 6 | 8 | 90 | None for the careful mended-hull cash target; ordinary and cod remain out of reach |
| B: earlier hand work | 40 | 8 | 2 | 4 | 6 | 60 | None for the careful mended-hull cash target; ordinary and cod remain out of reach |
| C: whole-week package | 20 | 6 | 2 | 3 | 4 | 35 | Tinned beans price 1; cod licence price 100; an island cod seller still required |

Set C's two additional price changes are **PROPOSAL only**, specifically
`Catalog/Supplies/TinnedBeans.asset` and `Catalog/Licenses/CodLicense.asset`.
They are not implemented here or implied by the mending data. Rod stays at its
accepted price of 60 in every set. No nutrition or configuration change is assumed.
At bean price **PROPOSAL 1**, the same cheapest-whole-ration search chooses three
tins on arrival and five per later day: **PROPOSAL 3 + 6 × 5 = 33** coins per week.
This requires **PROPOSAL 33 tins**, versus the reference **27 tins and 6 oatcakes**;
finite stock and other shoppers must be measured before adopting it.

### Set A: PROPOSAL day-by-day purses

All entries in this table are **PROPOSAL**. Licence in the purchase column means
shellfish unless explicitly labelled cod. Income is shown before meals.

| PROPOSAL day | Careful income / food | Careful buys | Careful end | Ordinary income / food | Ordinary buys | Ordinary end |
|---|---:|---|---:|---:|---|---:|
| 1 | 40 / 9 | licence | 36 | 20 / 9 | licence | 16 |
| 2 | 40 / 14 | rod | 2 | 20 / 14 | - | 22 |
| 3 | 40 / 14 | bait | 8 | 20 / 14 | - | 28 |
| 4 | 40 / 14 | - | 34 | 20 / 14 | - | 34 |
| 5 | 40 / 14 | hull | 0 | 20 / 14 | - | 40 |
| 6 | 40 / 14 | plank, nails, oakum | 4 | 20 / 14 | - | 46 |
| 7 | 20 / 14 | paint | 2 | 20 / 14 | - | 52 |

### Set B: PROPOSAL day-by-day purses

All entries in this table are **PROPOSAL**. Licence in the purchase column means
shellfish unless explicitly labelled cod. Income is shown before meals.

| PROPOSAL day | Careful income / food | Careful buys | Careful end | Ordinary income / food | Ordinary buys | Ordinary end |
|---|---:|---|---:|---:|---|---:|
| 1 | 40 / 9 | licence | 36 | 20 / 9 | licence | 16 |
| 2 | 40 / 14 | rod | 2 | 20 / 14 | - | 22 |
| 3 | 40 / 14 | bait | 8 | 20 / 14 | - | 28 |
| 4 | 40 / 14 | - | 34 | 20 / 14 | - | 34 |
| 5 | 40 / 14 | hull, plank, nails, oakum, paint | 0 | 20 / 14 | - | 40 |
| 6 | 40 / 14 | - | 26 | 20 / 14 | - | 46 |
| 7 | 20 / 14 | - | 32 | 20 / 14 | - | 52 |

### Set C: PROPOSAL day-by-day purses

All entries in this table are **PROPOSAL**. Licence in the purchase column means
shellfish unless explicitly labelled cod. Income is shown before meals.

| PROPOSAL day | Careful income / food | Careful buys | Careful end | Ordinary income / food | Ordinary buys | Ordinary end |
|---|---:|---|---:|---:|---|---:|
| 1 | 40 / 3 | licence | 42 | 20 / 3 | licence | 22 |
| 2 | 40 / 5 | rod | 17 | 20 / 5 | - | 37 |
| 3 | 40 / 5 | bait, hull, plank, nails, oakum | 1 | 20 / 5 | - | 52 |
| 4 | 40 / 5 | paint | 32 | 20 / 5 | rod | 7 |
| 5 | 40 / 5 | - | 67 | 20 / 5 | bait | 2 |
| 6 | 40 / 5 | cod licence | 2 | 20 / 5 | - | 17 |
| 7 | 20 / 5 | - | 17 | 20 / 5 | hull, plank, nails, oakum | 1 |

### Recommendation — PROPOSAL C as a package

**PROPOSAL C** meets the intended difference between the two players: the careful
player can fund every repair supply by **PROPOSAL day 4**, leaving the accepted
hand-work beat room on days 4–5. He funds the cod licence by **PROPOSAL day 6** and
ends with **PROPOSAL 17 coins**. This is affordability evidence, not proof that he
has performed the work, launched or landed a fish.

The ordinary player ends with **PROPOSAL 1 coin**, having paid for the hull, plank,
nails and oakum. The paint is **PROPOSAL 4**, so his visible remaining repair target
is **PROPOSAL 3 coins / 3 saleable clams**. He can see exactly what remains at the
slip. He still lacks the cod licence: the complete demo gate is **PROPOSAL 103 coins**
away, and the rod arrives on **PROPOSAL day 4**, not day 3 (**PROPOSAL 8 short** on
day 3). Do not describe this as both players finishing the demo in the same week.
An extra carried load, later fishing income or a later finish must be measured,
not silently credited. Catch licensing remains mandatory.

For comparison, **PROPOSAL A** funds all careful materials at the end of
**PROPOSAL day 7** with **PROPOSAL 2 left**, so same-day physical completion is
unproven. His cod shortfall is **PROPOSAL 118**. **PROPOSAL B** funds materials at
the end of **PROPOSAL day 5**, leaves **PROPOSAL 32** on day 7 and has a cod
shortfall of **PROPOSAL 88**. Ordinary remains **PROPOSAL 8 short of the rod** on
day 7 under either set; total shortfall to rod, bait and mended hull is
**PROPOSAL 118 for A / 88 for B**, and full-list shortfall is
**PROPOSAL 238 for A / 208 for B**. Neither satisfies the ordinary-nearly-there goal.

Changing only the five repair/purchase prices cannot fix this: the ordinary
post-food budget is **PROPOSAL 52**, below the existing rod alone. Even the careful
budget is **PROPOSAL 172**, below the existing rod, bait and cod bill of 200 before
any hull. Set C is therefore a conditional recommendation for explicit wider
balance decisions. If cheaper beans and the cod change are declined, use B only
as the careful repair target and retain the ordinary/full-week shortfalls openly.

These tables reuse the source's tide/carry yields and food-first cash ordering;
they do not rerun the terrain exposure or minute-by-minute needs simulation.
The independent ration search and ledger reproduce both source deficits and
check nonnegative purses. The lower meal bill still buys full hunger relief;
stock, eating access, timing, physical work, multiplayer supply competition and
supported launch remain unverified. Every purse is per player, never a shared
household subsidy. No editor or C# test execution was performed for this document.

## When something goes wrong gently

**He stops halfway through.** Pausing keeps the unfinished work and its material
reservation together. Resuming in the same session does not charge or reserve
again. Ending the session releases unconsumed materials and discards partial
time; the unfinished job then starts afresh. Work does not continue while he is
visiting or fishing. On a future wet-slip job, rising water also stops work at
the first closing edge; the three dry island jobs are not tide-interrupted.

**He has no oakum.** The next job says to bring the missing material. Starting
consumes nothing if any required item is missing, and advances no time. The fitted
plank remains complete. Nothing is fetched from home, the shop, a boat hold or an
unheld bag. He must bring the material back himself.

**He tries to paint first or launch too early.** Earlier jobs must be finished;
completed jobs cannot be purchased or worked twice. The boat becomes repaired at
the last job, but the dry-slip sequence still refuses launch with
`SupportedLaunchRequired`. High water alone never clears that refusal. The
placing work must provide the supported journey into water before sailing or
boarding can treat her as afloat.

**He saves during unfinished work.** Finished jobs already use the existing saved
flag list, with keys `st_peters.dory_mend.patch_plank`,
`st_peters.dory_mend.caulk_seam` and `st_peters.dory_mend.paint`. The current rules
draft exposes partial elapsed work and `MaterialsReserved` as a session snapshot
only; the snapshot cannot restore work. Materials are consumed only on completion.
Disposing the session, including when the same save is reloaded, releases any
unfinished reservation. The completed flags, final repaired entry and material
debit are grouped before a deferred save write. That groups writes without
providing rollback if the final write fails.

Main is now **save version 26**: store purchase rows and shopping-bag contents
survive reload, and special-order rows are reserved in the schema. The mending
draft adds no saved field or migration. Its live carrier adapter and a temporary
disk-save integration test are still missing, so current bag persistence is not
proof that mending is connected to it. The adapter must lock physically held
goods, release unfinished reservations on exit, and debit completion without an
independent save write or callback.

Keeping partial work across restart would need a separately authorized save
step: atomically store boat/place and loaded-save identity, active job, elapsed
work, reservation quantities and physical carrier identity alongside the carried
contents. This proposal uses completed jobs as checkpoints and retains no such
new saved fields.

## Choices for the owner

| Question | Lean and reason |
| --- | --- |
| Which counted shelf positions supply the four materials? | Follow the canon's store-bought repair consumables: four separate counted lines at the island general store, collected and carried by hand. The mainland boat-hardware range stays separate. Seller wiring, physical goods, capacity and delivery shares remain unfinished; do not substitute a remote parcel or unlimited stock. |
| Is day 4–5 a firm promise or a likely outcome for a practiced player? | Make it a likely outcome with a measured purse target. Use the conditional price alternatives above, then measure real carrying, eating, work and launch time. The accepted job sequence does not accept a price or guarantee an exact completion day. |
| Can he borrow the hammer, caulking iron and brush at the slip? | Yes, as physically present reusable implements. It gives the hand work a clear place without an extra first-week shopping bill. |
| Does paint need a separate drying wait? | No additional timer in this proposal. A later finish or launch design may justify one, but high water is not a drying or launch gate for the dry hull. |
| Must the jobs span separate visits? | No. Keep their order and physical materials, and let errands create the two-day rhythm. All three dry jobs can fit into one 75-minute work visit. |
| What happens to unfinished work across a save? | Keep the existing draft's completion-only checkpoints: release unfinished reservations on exit and restart partial time. If exact partial-work persistence is later required, authorize the atomic save step described above first. |
| How does the repaired hull reach the water? | Require a continuous supported movement and confirmed flotation before sailing. The exact manual launch interaction and its checks remain separate work; a height threshold or repaired flag cannot move the boat. |

## Source trail and handoff

The existing-source links below refer to main content used for the arithmetic:

- [Damaged dory offer](../../Assets/_Project/Data/Resources/Catalog/Shipwright/DamagedDoryOffer.asset),
  [rod](../../Assets/_Project/Data/Resources/Catalog/Gear/Rod.asset),
  [shellfish licence](../../Assets/_Project/Data/Resources/Catalog/Licenses/ClamLicense.asset)
  and [cod licence](../../Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset).
- [Clam data](../../Assets/_Project/Data/Fish/SoftShellClam.asset),
  [cod data](../../Assets/_Project/Data/Fish/AtlanticCod.asset),
  [shucked-clam bait](../../Assets/_Project/Data/Resources/Catalog/Bait/ShuckedClam.asset),
  [bread](../../Assets/_Project/Data/Resources/Catalog/Supplies/Bread.asset) and
  [ice](../../Assets/_Project/Data/Resources/Catalog/Supplies/Ice.asset).
- [Configuration](../../Assets/_Project/Data/Config/GameConfig.asset),
  [sale pricing](../../Assets/_Project/Code/Economy/SellPricing.cs),
  [market calculation](../../Assets/_Project/Code/Economy/MarketMath.cs),
  [market supply and daily recovery](../../Assets/_Project/Code/Economy/Market.cs),
  [wallet](../../Assets/_Project/Code/Player/PlayerWallet.cs) and
  [fee grant](../../Assets/_Project/Code/Economy/FrontedFeeGrant.cs).
- [Island tide profile](../../Assets/_Project/Data/Regions/StPeters.asset) and
  [tide model](../../Assets/_Project/Code/Environment/TideModel.cs).

The [tide-window draft](https://github.com/apholmes7-bot/hiddenharbours/pull/1501)
supplies general forecast queries. The
[mending rules draft](https://github.com/apholmes7-bot/hiddenharbours/pull/1508)
lists the calls, Core events, refusal text keys, carry adapter and save boundary
for placement. Its final repair calls `RepairLedger.MarkRepaired`; it uses the
existing `RepairedBoats` record and emits the existing `BoatRepaired` event as well
as the new mending events. The old paid-repair interaction must be reconciled when
the new route is placed.

This document adds no scene, supplier, prompt, artwork, licence placement, price
change or save field. The arithmetic was independently evaluated from the listed
values; the editor, player journey and visual changes were not run or verified.
