# Mending the dory

The proposed route has the player buy the broken dory on the St Peters slip,
carry her materials there, and patch, caulk and paint her himself. The proposed
materials cost **60 coin** and the three jobs take **75 game minutes**. The current
mending draft describes a hull supported on dry ground above high water: these
three jobs do not wait for low tide. Completing them records a repaired boat but
does not move or float her. A supported route from the slip to the water remains
separate unfinished work.

This is a proposed days 4–5 beat within the island-only first week. The repair
materials are affordable from a week of clams; **buying and repairing the boat by
day 4–5 is not established by main's economy**. The arithmetic below identifies the
gap before anyone places a promise the player cannot afford.

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
| Patch the plank | One patch plank and one packet of copper nails | 30 game minutes | 24 + 8 = 32 coin |
| Caulk the seam | One hank of oakum | 25 game minutes | 12 coin |
| Paint the patch | One tin of linseed paint | 20 game minutes | 16 coin |
| Total | Four purchased units | 75 game minutes | 60 coin |

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

## What the first-week purse can actually pay

These figures were rechecked against pinned main `d46bde303e6aa256d23d47a05ede5d7d6f38e51e`.
No price or existing configuration value is changed by this proposal.

| Existing main data | Coin | Consequence |
| --- | ---: | --- |
| Damaged dory offer | 400 | Main's existing yard offer supplies this price; the island sale is a separate draft |
| Existing paid repair option | 300 | The hand-work path replaces this charge when placed; do not charge both |
| Rod | 60 | Budgeted as a purchase |
| Cod licence | 120 | Required before keeping the cod used in the example |
| Shellfish licence | 15 | The fronted fee is 20, leaving 5 after buying it |
| Bread | 4 | Daily living costs must come out of any remaining purse |
| Bag of ice | 6 | Optional recurring catch care, not a repair material |
| Soft-shell clam base value | 2 per unit | This is not the island buyer's final payment |
| Atlantic cod base value | 14 per unit | This is also before outlet, supply and freshness adjustments |

The wallet starts at zero. If the existing fee grant has occurred and the shellfish
licence has been bought, the useful starting remainder is `0 + 20 − 15 = 5` coin.
If that grant or purchase has not occurred, do not assume the remainder is present.

The fixed bill is:

- Boat and hand repairs: `400 + 32 + 12 + 16 = 460` coin.
- Including a purchased rod and cod licence: `460 + 60 + 120 = 640` coin.
- Income needed after the 5-coin remainder: `640 − 5 = 635` coin, before food, bait
  or ice. A gifted rod would reduce this by 60; no gift is assumed here.
- The old boat-plus-paid-repair total is `400 + 300 = 700`. This proposal's boat
  and material bill is 240 coin lower. That is a proposed pacing consequence,
  not a silent change to the old offer.

Main's island store has price level **0.6** and demand **0.7**. The existing sale
calculation, for fresh catches and neutral selling traits, is:

`max(1, round(baseValue × clamp(1 / (1 + elasticity × supply / 0.7), 0.25, 1.6) × 0.6))`

Each sold unit adds 1 to its category's supply; the daily settle removes half.
Clams use elasticity 0.45 and cod 0.2. Weight does not multiply these per-unit
prices. A fresh clam therefore sells for **1 coin** at this outlet even at zero
supply: `round(2 × 0.6) = 1`. A first fresh cod sells for **8**, then its price falls
as finfish supply rises. Other catches in the same category can lower it further.
Spoiled, refused or unlicensed catches contribute no assumed income here.

**The materials alone are within reach:** 32 clams pay for the patch materials,
12 for the oakum and 16 for the paint. That is 60 saleable clams, or 55 after using
the initial 5 coin. At an illustrative 30 clams per day, two days cover the materials.
This is a threshold calculation; it does not establish how many holes he can find,
dig, carry and sell during a real low water.

**A mixed week does not establish the full purchase.** The following deliberately
optimistic cash model gives him 30 fresh saleable clams and 10 licensed cod every
day, initial category supplies of zero, no other sales, neutral traits and free
bait. It even grants access to the rod and licence from day 1 while counting their
cost in the fixed bill. Actual purchase timing can only make that assumption harder.
Catch rates are chosen examples, not measured yields or authored guarantees.

| Day | 30 clams | 10 cod after that day's marginal pricing | Daily gross |
| --- | ---: | ---: | ---: |
| 1 | 30 | 43 | 73 |
| 2 | 30 | 24 | 54 |
| 3 | 30 | 21 | 51 |
| 4 | 30 | 20 | 50 |
| 5 | 30 | 20 | 50 |
| 6 | 30 | 20 | 50 |
| 7 | 30 | 20 | 50 |
| Total | 210 | 168 | 378 |

The resulting purse is `5 + 378 = 383`. It is **77 short** even of the 460-coin
boat-and-material bill and **257 short** of the 640-coin bill including gear and
licence, before eating. Even by day 4 its optimistic cash is only
`5 + 73 + 54 + 51 + 50 = 233`, so this model does not buy a 400-coin boat that day.
With the same seven-day cod proceeds, the full bill needs
`635 − 168 = 467` saleable clams in the week, about 67 per day, plus the 70 cod.
That workload needs observation before adoption.

For a bait sensitivity check, main authors shucked-clam bait at 3 coin per unit,
with the ten-unit default lot costing 30. Seventy paid units would cost
`70 × 3 = 210`, reducing the mixed week's 378 gross to 168 before fixed costs.
That item is currently unlisted, so this is a cost sensitivity, not a claim that
he can buy it on the island. The shipped catch-only bait policy limits waste on
misses; it does not make bait free. Bread, ice, freshness loss and travel time are
additional costs or constraints, not hidden allowances in the table.

Main also lists the cod licence at the mainland harbourmaster. The island-only
week requires a reachable way to obtain the appropriate licences before this
cod income is possible. The arithmetic prices that licence but does not move it.

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
| Is day 4–5 a firm promise or a likely outcome for a practiced player? | Make it a likely outcome with a measured purse target. Keep the 60-coin labour-material proposal, then measure low-water yields and cash before deciding whether the 400-coin purchase price, a credit arrangement or island sale terms should change. The current numbers do not prove the promised day. |
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
