# Fuel & refuelling — two fuels, and where you can buy them

> **Status:** OWNER DROP, 2026-07-25 — **retail is BUILT (2026-08-20); burn is not.** §8 records
> exactly what ships, what the proposed prices are, and what the owner still has to rule on.
> **§9 is the BUILD SPEC for the missing half** (the tank, the burn model, running dry) — added
> 2026-08-20, `economy-sim`, **docs-only, nothing in it is built.**
> Everything from §5 down (the opening beat, the oar gameplay) remains design of record for **M2**
> ([`world-and-regions.md`](world-and-regions.md) §phasing), not a licence to build (CLAUDE.md rule 8).
>
> Subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md) (canon). Siblings:
> [`boats-and-navigation.md`](boats-and-navigation.md) §2.6 / §3.6 (fuel burn, running dry,
> stranding — **already canon**), [`economy-and-business.md`](economy-and-business.md) (where money
> moves), [`nine-mile-creek-wharf.md`](nine-mile-creek-wharf.md), and
> [`progression-and-housing.md`](progression-and-housing.md) (the licence/gate ladder).

**The owner's drop, in substance:** gas is required for all motors; diesel is required for some
boats. Nine Mile Creek has both gas and diesel pumps. St Peters has **gas only**, sold at the
general store. Jerry cans and other fuel storage will need artwork. And the player's first motor is
**Ned's old two-stroke**, handed over by Aunt Ginny at St Peters after a stretch of rowing.

---

## 1. What is already canon, and what this adds

**Already canon** in [`boats-and-navigation.md`](boats-and-navigation.md) — do not re-invent:

- Fuel is a real resource measured in **fuel-units (FU)**; tank size is a column in the boat tier
  table (§1.1).
- **Burn** = `f(throttle, engineLoad, seaState)` — punching a head sea or a foul tide costs more (§2.6).
- **Range** is a soft gate: you *can* push it and risk running dry (P5).
- **Running out is a breakdown-class event** → drift → **stranded** → tow. The dory and punt can
  **row or sail home fuel-free**; bigger boats cannot (§3.6, §3.7).
- Saves store fuel as boat state (§ determinism note).
- The engine ladder already reads *"stock outboard → larger outboard → **inboard diesel** →
  high-output → twin-screw"* (§5).

**What this drop adds — all of it new:**

1. Fuel has a **TYPE**: gas or diesel. Until now FU was one undifferentiated resource.
2. **Where each type is sold is a place fact**, and therefore a soft geographic gate.
3. **Portable fuel storage** (jerry cans and larger) exists as carried goods.
4. It answers an open question in [`economy-and-business.md`](economy-and-business.md) §593
   (*"Where does fuel/operating cost sit?"*) — at least for retail: fuel is bought at specific
   places, in a named type, and the type constrains the place.

---

## 2. The two fuels

| | **Gas** | **Diesel** |
|---|---|---|
| Burns in | **every motor** — all outboards, from Ned's two-stroke up | the bigger hulls' **inboard** engines |
| Maps to | the outboard rungs of the §5 engine ladder | the *"inboard diesel"* rung and everything above |
| Sold at | Nine Mile Creek (pump) · St Peters general store | **Nine Mile Creek only** (pump) |

**The rule of thumb, stated plainly so content can be authored against it:** if you drive it with a
tiller or a wheel over an outboard, it drinks gas. If it has an engine room, it drinks diesel.
Exactly which hulls fall each side is a per-`BoatHullDef` fact to be authored in M2, not guessed
here — but the split follows the existing tier ladder, so the boundary is around the point where the
fleet stops being outboard-driven. **→ §9.10 proposes the split over all 38 shipped hulls** (10 gas ·
21 diesel · 6 on the line · the rowed dory), using the signal the art lane already laid down: a hull
with a baked below-decks interior is an inboard boat. **Owner's call.**

---

## 3. Where you can buy it — and why that is the interesting part

| Place | Gas | Diesel | Form |
|---|---|---|---|
| **Nine Mile Creek** | ✅ pump | ✅ pump | dockside pumps — you lie alongside and fill the tank |
| **St Peters** | ✅ | ❌ | **general store**, not a pump |

⭐ **This is a progression pressure expressed as geography, and it is the best thing in the drop.**
St Peters is home. The moment you own a diesel boat, **home can no longer fuel her** — every tank of
diesel is a trip to Nine Mile Creek. That is P2 *Dory to Dynasty* made physical: growing up means
outgrowing your home harbour's supply, and it costs you time and planning rather than a stat.

It also gives the general store a job beyond the clam licence
([`progression-and-housing.md`](progression-and-housing.md) §82 already sells that there).

**⚠️ A store is not a pump.** St Peters selling gas *at the general store* implies you buy it **in
cans and carry it to the boat**, not through a hose. That is a different verb from lying alongside a
pump, and it is probably the *point* — it is humble, it is a chore, and it makes the jerry can a
real object rather than an inventory line. Worth confirming (§7 Q2).

---

## 4. Fuel storage — jerry cans and up

Portable fuel is a **carried good**, and it interacts with canon that already exists:

- **A spare can is the counterplay to running dry.** Canon makes running out a stranding event with
  a tow bill; carrying a can turns a disaster into an inconvenience you planned for. That is exactly
  the P5 shape — teeth, not brutality.
- **It extends range** beyond tank size, at the cost of hold space (HU) — a real trade against
  carrying catch, and a decision the player makes before leaving.
- **It is how gas reaches a boat at St Peters** (§3).

Larger storage (drums, a shed tank, a wharf-side bowser) is the natural upgrade path and ties to the
shipwright/property hub, but is **not specified here** — M2+ and the owner's call.

Art request: [`../art/briefs/fuel-and-fuel-storage.md`](../art/briefs/fuel-and-fuel-storage.md).

---

## 5. How the player gets their first motor — the opening beat

> ⚠️ **This section AMENDS the ratified opening. See §7 Q1 before building anything from it.**

The owner's sequence:

1. The player travels to **Nine Mile Creek** and **gets the dory** there.
2. They bring her back to **St Peters dock — under oars.**
3. That row is **deliberate friction**: a stretch of real rowing that teaches the water and makes
   engine power feel like a gift rather than a purchase.
4. On arrival, **Aunt Ginny presents Ned's old motor** — the small two-stroke.

**Why this is good, and worth protecting in implementation:** the motor is not bought. It is
*inherited*, like the boat and like the uncle. The player earns it with their back, not their
wallet, and the first thing they must then do is *buy gas for it* — which is how the fuel economy
introduces itself, in the smallest possible dose, on a boat that can always row home if it goes
wrong. It is a near-perfect P4 *Earn It Then Automate It* opening.

Ginny already exists as the NPC who *"teaches the buy-and-repair loop"*, so handing over the motor
sits naturally with her.

### 5.1 The oar gameplay — intent captured, mechanic OPEN

The owner's requirement is a *feeling*, not a mechanic: **the row must be long or demanding enough
that the motor is a relief.** Too gentle and the gift lands flat; too punishing and the opening is a
slog before the game has earned any patience.

What already exists to build it from:
- **Per-oar rowing is shipped and feels good** — `BoatController.LeftOar` / `RightOar`, forward /
  back / idle per side, so a one-sided stroke turns her. The oars are now meshes and sweep
  continuously (ADR 0022 phase 7).
- **Stamina already exists as a design concept** — [`progression-and-housing.md`](progression-and-housing.md)
  owns money/stamina, and [`boats-and-navigation.md`](boats-and-navigation.md) already spends it on
  the manual bilge pump and field repairs. Rowing is the obvious second consumer.
- **The environment already pushes back** — wind, current, tide and sea state are live and
  deterministic. A row *against* a foul tide is free difficulty that teaches P1 at the same time.

**Not chosen here** (owner's call — §7 Q3): whether the friction is stamina, distance, a tide
window, weather, or simply the honest length of the crossing.

---

## 6. What this does NOT decide

- Prices, burn rates, tank sizes in FU — balance, and the owner tunes those in Def assets (rule 6).
  *(§9.6.2 now PROPOSES tanks and burn rates per hull. Still Def-authored, still the owner's to tune.)*
- Which specific hulls are diesel (§2). *(§9.10 now proposes it.)*
- Whether fuel is a per-boat tank only, or also a stored commodity you own ashore.
- Any UI. A fuel gauge is already canon (§3.6); nothing here adds a screen.

---

## 7. ⏳ Open — the owner must settle these

1. ~~**⚠️ Where does the dory come from?**~~ ✅ **SETTLED 2026-07-25 — see §7.1 below.**
2. **Is St Peters gas sold in CANS** (carry it to the boat) rather than through a pump? (§3)
3. **What makes the row demanding?** (§5.1)
4. **⚠️ Does Ned's two-stroke need OIL MIXED WITH THE GAS?** A real two-stroke outboard burns a
   petrol/oil premix, not straight gas. That is a lovely diegetic detail for an old inherited engine
   — and it is also a second consumable to buy, measure and forget, on the player's very first
   motor. **Charming or fiddly is a taste call, not a research one.** If yes, it wants art (an oil
   tin) and a line from Ginny; if no, the engine simply drinks gas like everything else and nobody
   will mind.

### 7.1 ✅ Settled 2026-07-25 — where the dory comes from

**Nine Mile Creek IS Port Greywick's working wharf** — one region, two zones (owner ruling; see
[`nine-mile-creek-wharf.md`](nine-mile-creek-wharf.md) §1). That dissolves the apparent conflict:
the wharf *is* the landing at the far end of the sandbar, so canon's "cross the bar on foot to
Greywick" and the drop's "go to Nine Mile Creek to get the dory" are **the same crossing described
from two ends.** No canon amendment is needed for the geography.

Two further rulings the same day, which **do** amend canon:

- **There is no shipwright in this region.** The damaged dory lies at the wharf and is sold by
  someone else there — a fisherman, the harbourmaster, whoever the world-content lane decides.
  Canon's "buy her at the **Greywick shipwright**" is superseded on the *seller*, not on the
  *transaction*: she is still **bought**, not inherited (the ratified earn-the-boat beat stands).
- **She still arrives damaged and must be put right** before she will swim.

⏳ **Consequently open, and NOT invented here:** (a) **who repairs her**, now that no shipwright
stands in the region — the recommendation is *you do, by hand*, which is the strongest available
P4 beat and already has a hook (stamina is spent on the manual bilge pump and field repairs);
(b) **where the shipwright's yard lives instead**, since boat purchase and upgrades for the whole
rest of the game hang off it (`Data/Shipwright/` is built and shipping today).

**The opening, end to end, as it now stands:** dig clams at St Peters → cross the bar on foot →
find the damaged dory at the Nine Mile Creek wharf → earn her, buy her, put her right → **row**
her home to St Peters → Aunt Ginny hands over Ned's old motor → your first errand is buying gas.

---

## 8. ✅ What is BUILT — fuel retail, 2026-08-20

> **Owner direction, 2026-08-20:** *a couple of fuel pumps at the Nine Mile Creek wharf which charge
> a HIGHER price; a full gas station at the end of the wharf road's intersection with Route 91.*
> This section is the record of what that became. **Prices below are PROPOSALS** — they live in Def
> assets precisely so the owner overrides them without touching code (rule 6).

### 8.1 The five grades

`gas` · `diesel` · `mixed` (two-stroke premix) · `oil` · `stove_oil`. A **fixed contract shared with
the art lane**: the fuel rig bakes a colourway per grade, `FuelContainerDef.Grade` carries the same
strings, and `HiddenHarbours.Core.FuelGrades` states them once so nothing spells one as a literal.

`stove_oil` is new here, and it exists because the island **burns oil** — see
[`municipal-infrastructure.md`](municipal-infrastructure.md). It is a purchasable grade and nothing
more: **no delivery loop, no furnace, no heating demand** is modelled, and none is implied.

### 8.2 The two sites, and the trade they create

| | **`station.nmc_wharf_pumps`** | **`station.route_91`** |
|---|---|---|
| What it is | a couple of hoses on the wharf | the full station, up at the junction |
| gas | **2.05** /L | 1.55 /L |
| diesel | **1.85** /L | 1.40 /L |
| mixed | *authored, switched off* (2.95) | 2.35 /L |
| oil | *authored, switched off* (10.50) | 8.50 /L |
| stove_oil | not stocked | 1.20 /L |

⭐ **The wharf is convenient and dear and incomplete; the road station is cheap and complete and a
walk.** That is the whole design, and it is a decision the player makes every time they need fuel —
a ~32% markup for not leaving the water, and two grades you simply cannot buy dockside at all. It is
the same shape as §3's St Peters/Nine Mile Creek split, one zoom level in: geography as a price.

Canon holds: **Nine Mile Creek sells both gas and diesel** (§3), at the wharf, where a boat lies
alongside. A test pins both the canon and the direction — if a re-price ever made the wharf the
cheaper option, `FuelStationContentValidationTests` goes red.

**Two rows are authored but switched off** (`mixed`, `oil` at the wharf). Availability and price are
separate fields for exactly this: the owner ticks one box to open a grade, and the price he tuned is
already sitting there. Whether a working wharf should sell premix is a taste call — see §8.5 Q3.

### 8.3 The verb

Walk up to a pump **holding a fuel container**, press interact, and it fills and charges you.

- The **level lives on the can**, where the diegetic-UI direction wants it — `FuelLevelPresenter`
  already drew a fill fraction and now *is* the container's fuel, through the Core seam
  `IFuelVessel`. There is no fuel screen and no fuel HUD.
- The charge **rounds up to whole coin, once, on the final figure** — so no fill is ever free and
  nobody is charged twice for the same rounding.
- **Short of money the pump pours what you can pay for** rather than refusing, and says so. A pump
  that gives you nothing because you cannot afford a full tank can strand a broke player. ⏳ The
  consequence is that "fill her up" with an empty purse spends everything — see §8.5 Q1.
- **A can's grade is what it holds.** There is nowhere to record "this gas can currently has diesel
  in it", so the diesel pump refuses a gas can, in words. The art brief's colour split is the
  warning; this is the consequence.

### 8.4 ⚠️ What is NOT wired — read this before believing fuel works

1. ~~**NO BOAT TANK, and therefore no refuelling a boat.**~~ ✅ **BUILT** (F1–F4, 2026-08-20).
   `BoatHullDef` carries the tank (§9.2), `BoatFuelTank` implements `IFuelVessel` on the hull, both
   verbs work (§9.4), and the pump's pricing did not change — the promise it made in its own remarks
   was kept. **Still unbuilt:** the burn is written and tested but **nothing consumes it yet**, and
   the running-dry states are F5. So a tank fills, pours and persists, and does not yet EMPTY under
   way. ⚠ Every shipped hull is still `FuelCapacityLitres = 0` until the §9.6.2 authoring pass (F6),
   so the fleet is inert by default and behaves exactly as it did before.
2. ~~**Fuel does not persist.**~~ ✅ **THE TANK PERSISTS** (save v14, F4, 2026-08-20). A hull's level
   is saved per hull id, sparse, with a missing row meaning brim-full. **⭐ This closes the leak: a
   pump beside a boat is now safe to place**, which is the gate the world lane was waiting on.
   ⚠ **A CAN's level is still session-local**, exactly as its position is — so fuel bought into a
   jerry can and left on the wharf does not survive a reload, while fuel poured into a boat does.
   Buy-and-pour in one session; that gap is carriable-state work, not the tank's.
3. **Litres, not fuel-units.** Canon measures a tank in FU; the shipped container Defs measure
   litres, so retail speaks litres. ⏳ One of the two has to give when the tank lands — see §8.5 Q2.
   **→ §9.1 proposes the answer: `1 FU ≡ 1 L`, the identity**, chosen because the money lands
   (a dory tank ≈ one cod). Still the owner's to ratify.
4. **No stove-oil container exists.** All 84 baked containers are gas/diesel/mixed/oil; the rig
   predates the grade. So stove oil is on sale at Route 91 and **you cannot carry any away** until
   the art lane bakes a colourway. Named as a known gap in the validation test rather than hidden.
   **→ §9.14 flags it upstream to `art-director` and confirms it does NOT block the tank**: no hull
   burns stove oil, so the burn model is complete without it.
5. **Nothing is placed.** No pump stands anywhere in any scene — placement is the art/world lanes'.
6. **St Peters' over-the-counter gas is NOT this.** §3's general-store gas is a *filled can sold as
   an item*, which is a `SupplyDef` beside the ice (as `FuelContainerDef`'s own remarks anticipate),
   not a station. Separate piece of work; the PR #496 sibling ruling is honoured, not overturned.

### 8.5 ⏳ Open — the owner's calls

1. **Should "fill her up" be allowed to empty the purse?** Today it pours what you can afford and
   tells you it did. The alternatives are refusing outright (worse — it can strand you) or a
   how-much dial (ui-ux work, and the seam for it already exists: the quote takes a litres cap).
2. **Is a fuel-unit a litre?** The cheapest answer is yes, and it keeps one number for one quantity.
   Needed before the burn model, not before now. **→ §9.1 makes the case for yes** and prices the
   whole tier ladder under it. Owner to ratify.
3. **Should the wharf pumps sell premix and oil?** Rows are authored and switched off. Every
   outboard on that wharf burns one or the other, so "yes" is defensible; "no" makes the walk up the
   road mean more. Currently **no**. **→ §9.11 shows the answer is forced to "yes" only under premix
   Option A**; under the recommended Option B it stays a free taste call.
4. **§7 Q4 is still open and now has a price tag.** If Ned's two-stroke needs premix, the player's
   first errand is buying `mixed` — which the wharf does not currently stock, so their first fuel
   run would be up Route 91 rather than a step down the dock. Charming or fiddly is still the call.
   **→ §9.11 costs both options** (and finds that mixing your own needs **no new art** — the `oil`
   grade is already baked in every carriable size). Recommends **no, for now**, because the change
   is one string on one Def whenever the owner wants it.

### 8.6 ⭐ Where the two sites stand — placed 2026-08-20

Both sites are on the ground at Nine Mile Creek, builder-authored (`NineMileCreekStation`), and they
appear on the next **Hidden Harbours ▸ Build Nine Mile Creek Scene** click.

**The wharf — two dock pedestals on the apron's seaward face**, gas to the north and diesel four
metres south of it. The wharf plan had already sited them in words since Phase A ("the west wall is
the service end — the fuel pump and the oil tank are both on it") and nothing had built it; this is
that sentence taken literally. It is also the only answer the yard allows, because the spit is full.

Nothing about the pair is typed. The standback from the lip is the pedestal's own published nozzle
reach (1.06 m) plus a body, plus half the #609 quay wall, plus 0.30 m of daylight — **1.755 m** —
so the body stands on the deck by construction rather than by luck. The latitudes come off the brow
and the pump's own 2 m reach. And a hull lying half a beam off that face at either pedestal is inside
the dredged channel at spring low, which matters here: the basin bares, so "alongside the wharf" is
not everywhere a wet berth.

**Route 91 — the full forecourt on the south-west corner of the junction**, 16.6 m out. Wharf Road
dead-ends on the through-road, so the junction is a T and this corner is straight ahead as you drive
up off the wharf. Island, two multi-product machines, a two-bay canopy, the pylon sign at the road,
the C-store with its seamless sales floor, and the tanker fill cluster behind. The layout is
`StationIso.plan()`'s own output, not a hand-composed arrangement; the site was chosen by the
max-min-slack search the restaurant used, and clears everything the village claims by **3.48 m** at
its tightest while its apron meets Route 91's corridor with 1.33 m to spare.

**⭐⭐ One hose per dispenser SIDE, and it is a finding rather than a taste call.** A multi-product
machine's three nozzles publish the *same* ground reach point and differ only in height, so several
`FuelPump`s there would be several candidates at one world position — and `InteractResolver`'s order
(priority, then distance, then lowest id) would hand every press to the same one forever, leaving two
of the three grades drawn on the machine and unreachable. So a side sells one grade, named in the
offer label before the press, and Route 91's four faces carry **gas · diesel · mixed · gas**.
Giving one pump a grade *choice* is a change to the interact seam and belongs to that lane.

⚠️ **Consequences worth knowing before the walk.** `oil` and `stove_oil` are posted at Route 91 and
have **no hose to draw them** — the committed art is baked for three grades and a fourth is a re-bake
(§8.5 Q3's sibling, and the kit's own open grade-count call). The six things for sale inside the
C-store are placed and measured but have **no verb** — they stand on the standing spots a whole-scene
reach pass verified, waiting for the interactions §8.4 lists as missing.

⭐ **And these hoses fill a BOAT — which became true six minutes before the placement PR landed.**
#618 put the tank on the hull as data, #619 gave `IFuelVessel` its `Draw` half, and **#620 landed
the runtime**: `BoatFuelTank`, `GameServices.ActiveBoatFuel`, and a `FuelPump.TargetVessel()` that
falls back from your hands to the boat you are aboard, with `OnDeck` in its `Contexts`. So the wharf
Def's own flavour — *"you lie alongside and fill without leaving the water"* — is a promise the verb
keeps too, and the dock pedestal is doing the job it was sited for rather than waiting for one.

⚠️ **The placement PR shipped a paragraph here saying the opposite**, written against a `main` that
was one merge behind. It is corrected above; the lesson is that a "not wired" note wants a PR number
and a date, because on a day like this one it goes stale between the test run and the merge.

**The siting carries it, measured.** The pump component stands **0.695 m** in from the apron's lip,
so a body at a hull's inboard rail — hard against the wharf, which is where you stand to take a hose
— is **0.69 m** from it, inside the pump's 2 m reach; on a dory even her centreline (1.59 m) reaches.
From amidships on a lobster boat (3.19 m) you are out of range and walk to the rail, which is honest
behaviour rather than a defect. ⚠️ A **can's** level is still session-local (#623 persists the tank,
not the can), so the old leak closes for boats and not for cans.

---

## 9. 🔧 The build spec — the tank, the burn, and running dry

> **Status:** SPEC, 2026-08-20 (`economy-sim`). Written against the retail half that shipped this
> morning (§8) and the canon that has been waiting for it since §1. **Nothing here is built.** It is
> the sheet a build lane works from, and it settles by proposal — not by decree — the three open
> questions that were blocking the tank: **§8.5 Q2** (is a fuel-unit a litre? — §9.1), **§7 Q4 /
> §8.5 Q4** (does Ned's motor need premix? — §9.11), and **§2**'s "which hulls are diesel" (§9.10).
>
> **Every number below is a PROPOSAL in a table the owner tunes in a Def or `GameConfig`** (rule 6).
> Where a call is not mine to make it is labelled **OWNER** or **LEAD-ARCHITECT** and collected in
> §9.15. Nothing here is presented as final, and nothing here belongs inline in a `.cs`.

### 9.0 What this section decides, and whose lane each part is

| Part | Lane | Why |
|---|---|---|
| Units, tank capacity, burn rates, prices, the tunables | **economy-sim** | balance is data, and fuel is a running cost |
| The tank as an `IFuelVessel`, the two refuel verbs | **economy-sim** | the pump already exists and already promises this seam |
| The save shape + migration | **economy-sim** → **lead-architect** sign-off | save format is a shared contract |
| A `Drain`/transfer half on Core's `IFuelVessel` | **LEAD-ARCHITECT** | it is a Core interface change |
| The **breakdown → drift → stranded → tow state machine** | **gameplay-systems** | canon §3.6/§3.7 is theirs; the charter is explicit that "tow as a *gameplay event*" is not mine. I raise the event and price the tow |
| The fuel gauge / low-fuel telegraph | **ui-ux** (diegetic) | §8.3 already rules: the level reads off the object, not a screen |
| A stove-oil container colourway | **art-director** | §9.14 — flagged, not solved here |

---

### 9.1 Units — a fuel-unit is a litre (answers §8.5 Q2)

**Proposal: `1 FU ≡ 1 L`, exactly. The conversion is the identity, and FU stops being a separate
word.** Canon's boat-tier "Fuel (FU)" column ([`boats-and-navigation.md`](boats-and-navigation.md)
§1.1) is re-read as litres with **no re-tuning of a single number**, and everything shipped in §8
already speaks litres — the 84 container Defs, `IFuelVessel.CapacityLitres`, `FuelGradeOffer.PricePerLitre`.

**Why this is the right answer and not merely the cheap one — the money lands.** Under FU ≡ L,
canon's tank sizes priced at §8.2's posted prices give (charges as the pump actually bills them —
**rounded up to whole ₲ once, on the final figure**, per `FuelPricing`):

| Boat (canon tier) | Tank | Fill at Route 91 | Fill at the wharf | For scale |
|---|---|---|---|---|
| Dory | 10 L | **16 ₲** | 21 ₲ | a cod is 14 ₲; a load of ice is 6 ₲ |
| Punt | 25 L | 39 ₲ | 52 ₲ | the punt herself is ~1,800 ₲ |
| Cape Islander | 90 L | 126 ₲ | 167 ₲ | she is ~14,000 ₲ |
| Stern Trawler | 700 L | 980 ₲ | 1,295 ₲ | she is ~190,000 ₲ |
| Tanker | 9,000 L | 12,600 ₲ | 16,650 ₲ | she is ~2,400,000 ₲ |

⭐ **A tank of gas for the dory costs about one cod.** That is precisely the weight a first recurring
cost should have — heavier than ice, lighter than the clam licence, and impossible to ignore for a
player whose whole day is four fish. No other conversion constant lands the opening this well, so
the identity is chosen on the strength of the result, not to save arithmetic.

**The alternative, stated so the owner can overrule it.** A scale constant (`1 FU = k litres`) would
let canon's FU column stay verbatim while the *money* per tank moved by `k`. It buys nothing — canon's
column is already exactly the litre figure we want — and it costs a conversion every reader of every
call site has to hold in their head, plus a second unit in the save. **Recommend against.**

⚠ **Consequence to accept:** litres are then a *game* quantity, not a realistic one. A real 9.9 hp
outboard burns ~4 L/h and a real lobster boat 20–40 L/h; §9.6 proposes 23 and 105. The world is
compressed ~1:10 in space ([`scene-sizing-and-world-scale.md`](scene-sizing-and-world-scale.md)) and
a game day is 30 real minutes, so fuel is compressed with it. **Litres are the unit; realism is not
the calibration.** The calibration is §9.6's right-hand columns.

---

### 9.2 The tank as data — the fields on `BoatHullDef`

Three fields, mirroring how `EnginePower` already sits on the hull:

```csharp
[Header("Fuel (docs/design/fuel-and-refuelling.md §9)")]

[Tooltip("How much fuel she carries when brim-full, in LITRES (= canon's fuel-units, §9.1).\n\n" +
         "0 = THIS HULL HAS NO TANK. Not 'an empty tank' — no tank at all: nothing burns, the " +
         "gauge does not exist, and running dry cannot happen to her. That is the correct reading " +
         "for the rowed dory, and it is also what every hull asset written before this field " +
         "existed deserializes to — so the fleet loads unchanged and stays playable while the " +
         "owner authors the table in §9.6 one hull at a time. (The RodeMeters / HelmAheadNotches " +
         "convention: an untouched hull is inert, never accidentally enrolled.)")]
[Min(0f)] public float FuelCapacityLitres = 0f;

[Tooltip("Which grade her engine drinks — one of FuelGrades (gas · diesel · mixed). Empty = she " +
         "has no engine to feed. This is the field that makes §3's geography bite: a hull set to " +
         "\"diesel\" cannot be fuelled at St Peters at all.")]
public string FuelGrade = "";

[Tooltip("Litres per hour at FULL THROTTLE, light, in a glass sea — the reference burn every " +
         "other duty in §9.5 is a fraction or a multiple of. Owner-tunable per hull; 0 with a " +
         "non-zero capacity is an authoring error the content-validation test fails on.")]
[Min(0f)] public float FullThrottleLitresPerHour = 0f;
```

**Why the hull and not an `EngineDef`.** Canon §4 wants Hull + **Engine** + Hold + Gear as separate
component assets, and the engine ladder (*stock outboard → larger outboard → inboard diesel → …*) is
a component swap. **No `EngineDef` exists today** — `EnginePower`, `RudderAuthority` and the whole
drive live on `BoatHullDef`. Putting fuel anywhere else would invent half of a component system to
hold three floats.

⏳ **LEAD-ARCHITECT, for when `EngineDef` lands:** `FuelGrade` and `FullThrottleLitresPerHour` move
to the **engine** (they are facts about what drinks); `FuelCapacityLitres` stays on the **hull** (a
tank is part of the boat, and re-engining does not shrink it). Naming them now in that split is why
they are three fields rather than one struct.

---

### 9.3 The tank in the save — `SaveData` v14

One sparse list, keyed by hull id, exactly as `HullInstrument` and `SounderPrefsDto` already are:

```csharp
/// <summary>How much fuel each owned hull has aboard, in litres. SPARSE BY CONSTRUCTION:
/// a hull with no row here is BRIM-FULL — see the migration note. Added in v14.</summary>
public List<HullFuelDto> HullFuel = new();

[Serializable]
public struct HullFuelDto
{
    public string HullId;    // stable hull id, e.g. "boat.dory_outboard"
    public float  Litres;    // clamped to the hull's FuelCapacityLitres on load
}
```

**A missing row means FULL, not empty.** Three reasons, and the third is the binding one:

1. **A boat you just bought arrives fuelled.** That is what a sale does, and a shipwright who hands
   you a dry boat you cannot move off his wharf is a bug shaped like realism.
2. **Failing full is the forgiving direction** (P5). A wrong default that strands you is the exact
   spiral canon forbids.
3. ⚠ **The migration contract forbids the alternative.** `SaveMigration`'s own rule is that a step
   *only adds* — "it never reinterprets existing values". A v13 save belongs to a player for whom
   fuel did not exist; deciding on load that their boat is empty *invents a consequence for them*.
   Full is the only reading that gives them what they had.

**Writer rule that keeps it sparse and unambiguous:** write a row for **every owned hull whose level
is not full**, and no row for the ones that are. A dry boat therefore always has an explicit `0`.

**Migration v13 → v14** adds the empty list and stamps the version. Nothing else — no heal, no
back-fill, no invented levels. `SaveMigration.CurrentVersion` 13 → **14**.

⚠ **This closes the leak §8.4.2 named.** Today a pump in a live scene spends money that persists into
a fuel level that does not. With v14 shipped, cans still do not persist (they are session-local like
their position) but **the tank does** — so a pump beside a boat is finally safe to place. Say so to
the world lane when this lands; it is the gate they are waiting on.

---

### 9.4 The two verbs — you **FILL** her, or you **POUR** into her

They are different actions on different objects with different costs, and the doc names both because
the world will need two prompts and two sounds.

| | **FILL** | **POUR** |
|---|---|---|
| Prompt | *"Fill her up with diesel"* | *"Pour the can in"* |
| Where | at a pump — you lie alongside, or stand at the hose | anywhere you can reach the filler with a can in your hands |
| Source | the station's infinite supply | **the can you are carrying**, and only that |
| Costs | **money**, at the site's posted price (§8.2) | **nothing** — you already paid at the pump or the counter |
| Needs | `FuelPump` + `FuelStationDef` | no station, no wallet, no price |
| Refuses when | grade not sold · grade mismatch · tank full · broke | grade mismatch · can empty · tank full |

⭐ **Two verbs is not tidiness, it is the whole of §3.** St Peters has **no pump** — Ginny's store
sells gas *in a can over a counter*. So at home you can only ever **POUR**, and the pump verb is
something you go to Nine Mile Creek to use. The verb split *is* the geography.

**FILL needs no new code.** `FuelPump` already states this in its own remarks: the tank implements
`IFuelVessel`, the pump adds `InteractContext.Aboard`, and the pricing is unchanged. That promise
was written to be kept.

**POUR needs one Core addition**, and it is the only one this feature requires. `IFuelVessel` today
is deliberately one-way — *"There is deliberately no `Drain`. Burning fuel is the boat's business and
the boat has no tank yet"*. The boat now has a tank, so the far side of that seam exists. Proposed
shape, mirroring `FuelPricing` so the two read as siblings:

```csharp
// Core — pure, no Unity, no money, EditMode-testable.
public enum PourRefusal { None, NoSource, NoDestination, GradeMismatch, SourceEmpty, DestinationFull }

public readonly struct PourQuote { PourRefusal Refusal; float Litres; bool CanPour; }

public static class FuelTransfer
{
    public static PourQuote Quote(IFuelVessel from, IFuelVessel to, float maxLitres = 0f);
    // and the one new interface member the quote needs to be actable:
    //   float IFuelVessel.Draw(float litres);   // returns what it ACTUALLY gave, ≤ Litres
}
```

⏳ **LEAD-ARCHITECT call:** `Draw` on the interface, or a `Quote` + caller-does-both-writes pattern
that leaves `IFuelVessel` untouched. `Draw` is symmetric with `Deliver` and honest about what a
container is; the alternative keeps Core's surface smaller. **Recommend `Draw`** — every future
vessel (a wharf bowser, a shed tank, a boat pumping into another boat under tow) wants it, and
`Deliver` without `Draw` is half an interface.

⚠ **The grade check stands on both verbs, for the same reason §8.3 gives:** a can's grade *is* what
it holds, because there is nowhere to record "this gas can currently has diesel in it". So a gas can
will not pour into a diesel boat — in words, not silently.

---

### 9.5 The burn model

Canon fixes the shape: **burn = `f(throttle, engineLoad, seaState)`**
([`boats-and-navigation.md`](boats-and-navigation.md) §2.6). This is a concrete proposal for that `f`.

#### 9.5.1 The formula

```
litresPerHour = R × throttleTerm × loadTerm × seaTerm × BurnScale

  throttleTerm = IdleFraction + (1 − IdleFraction) · |throttle|^ThrottleExponent
  engineLoad01 = clamp01( HoldLoadWeight · holdFraction + SlipLoadWeight · slip01 )
      loadTerm = 1 + LoadSurcharge · engineLoad01
       seaTerm = 1 + SeaSurcharge · seaState01

         slip01 = clamp01( 1 − wayThroughWater / (|throttle| · MeasuredTopSpeedMps) )

  ΔL = litresPerHour × Δt_hours          // integrated on the FIXED tick — see 9.5.3
```

- `R` is the hull's `FullThrottleLitresPerHour` (§9.2). Everything else is a dimensionless multiplier,
  so **one hull's thirst is one number the owner can move** and the *shape* is shared by the fleet.
- `throttle` is `BoatController.Throttle`, −1..+1. **The absolute value** — backing down burns fuel
  too, and astern is weaker thrust for the same gulp, which is fair.
- `holdFraction` is the hold's used/capacity — a full hold is a heavy boat.
- `slip01` is **how badly she is failing to make the speed her throttle asked for**. This is the
  elegant part: a head sea, a foul tide, a fouled bottom, a towed load and a dragging anchor all
  produce the *same* physical signature, so one term covers every cause without enumerating any of
  them, and it is exactly canon's *"burning more punching into a head sea or against a foul tide"*.
- `seaState01` is `EnvironmentSample.SeaState01` — the **continuous** axis, not the stepped enum.
  Burn is a sim quantity, and canon reserves the stepped enum for gameplay gates and the HUD readout;
  stepping the burn would make fuel cost lurch at a band edge for no reason a player could read.

#### 9.5.2 Two honest caveats

⚠ **`slip01` needs a per-hull reference speed that does not exist yet.** `MeasuredTopSpeedMps` would
be a new `BoatHullDef` field — **gameplay-systems' lane, and it must be MEASURED, never derived**.
`BowSprayGrading`'s own remarks are a standing warning about exactly this: *"Do not re-derive that
top speed from the stats… the old note read '300/120 ≈ 2.5 m/s' and was wrong on both counts… Measure;
never restate."* A harness already exists (`PilotableFleetPlayTests.RunToTerminal`), so the field can
be authored **and pinned by a test that re-measures it** — see §9.12.

⚠ **The sea is counted twice, a little.** A head sea slows her, which raises `slip01`, which raises
`loadTerm` — and then `seaTerm` charges for the same weather again. That is why `SeaSurcharge` is
proposed **modest (0.60)** rather than the 0.9 it would want if it were carrying the head-sea cost
alone. Naming the overlap so nobody later "fixes" it by raising both.

⭐ **Setting `SlipLoadWeight = 0` reduces the model exactly to the simple version** — load is hold
weight only, and the weather is priced entirely by `seaTerm`. That is the recommended **first cut
for the build lane**: it needs no new hull field, no measured speed and no gameplay-systems
dependency, and it becomes the full model later by moving one `GameConfig` number off zero. One
formula, two phases, no branch.

#### 9.5.3 Determinism (rule 5), stated precisely

- **The rate function is pure.** Same `(throttle, hold, slip, seaState01, R, config)` → same litres
  per hour, forever. No RNG, no Unity types, no clock — a static function in the shape of
  `FuelPricing.Quote`, and EditMode-testable the same way.
- **The level is integrated boat state, and therefore SAVED** — the same class of thing as
  `bilgeLevel` and `engineHealth`, which canon already saves for the same reason. It is *not*
  recomputed from `(seed, gameTime)`, because it is path-dependent: it records what the player did.
  Canon already commits to this (§1, and `boats-and-navigation.md` §9's save payload).
- ⚠ **Integrate on the FIXED tick with `Time.fixedDeltaTime`, never on a frame delta.** A frame-rate
  spike must not change how much fuel a crossing costs. This is the one implementation detail that
  can silently break determinism, and it is cheap to get right and invisible when wrong.
- ⚠ **Meter against ENGINE-RUNNING seconds, not game-clock hours.** `GameConfig.SecondsPerDay` is an
  owner tunable (1,800 today — a 30-minute game day), and the boat moves at world m/s in real time.
  Bill fuel per *game* hour and **doubling the day length silently halves the fuel cost of every
  crossing in the game.** No owner tuning tide pacing would expect to re-price fuel. So an "hour" in
  §9.5.1 is an hour of the engine actually turning, as the player experiences it, and fuel-per-
  kilometre stays fixed no matter what the clock does.
- ⏳ **Forward note for M3 (staff/automation).** A boat run by a hired skipper while you are ashore
  has no ticks to integrate. Offline trips must charge a **derived trip cost** from this same
  formula at a stated reference duty — not a second, drifting model. Flagged now so the automation
  lane does not invent one.

---

### 9.6 The tunables, and the proposal over the shipped fleet

#### 9.6.1 `GameConfig.Fuel` — the shape of the curve (one block, whole-fleet)

| Field | Proposed | What it does | Move it if… |
|---|---|---|---|
| `IdleFraction` | **0.08** | burn at zero throttle with the engine running | ticking over while you fish feels free (raise) / punitive (lower) |
| `ThrottleExponent` | **2.0** | how steeply burn climbs with throttle | throttling back should save more (raise) / less (lower) |
| `LoadSurcharge` | **0.35** | most a fully-loaded, fully-bogged engine adds | a full hold should cost more to carry home |
| `HoldLoadWeight` | **0.5** | how much of load is the catch aboard | — |
| `SlipLoadWeight` | **0.5** → **0.0 for the first cut** | how much of load is failing to make her speed (§9.5.2) | 0 until `MeasuredTopSpeedMps` exists |
| `SeaSurcharge` | **0.60** | most a storm adds over glass | raise **only** if `SlipLoadWeight` is 0 |
| `BurnScale` | **1.00** | ⭐ **the one dial that re-prices fuel for the whole game** | the whole economy is too thirsty / too lean |
| `LowFuelFraction` | **0.15** | where the low-fuel telegraph arms (§9.9) | — |
| `NewBoatArrivesFull` | **true** | a bought boat comes fuelled | — |

⚠ **`GameConfigAssetCoverageTests` will fail this PR if the block is not hand-added to
`Data/Config/GameConfig.asset`'s YAML in the same commit.** That test exists because a declared-but-
unshipped block deserializes to a zeroed struct and the feature ships silently dead — which has
already happened twice on this repo. `BurnScale = 0` would mean *fuel is free and nothing burns*.

**What the curve does, so the owner can read it without arithmetic:**

| Throttle | 0 (idle) | ¼ | ½ | 0.7 (cruise) | 0.85 | full |
|---|---|---|---|---|---|---|
| Fraction of full-throttle burn | 0.08 | 0.14 | 0.31 | 0.53 | 0.74 | 1.00 |
| …for this fraction of top speed | 0 | 0.25 | 0.50 | 0.70 | 0.85 | 1.00 |

⭐ **That gap is the lesson.** Half throttle is half the speed for **31%** of the fuel — so easing off
buys you **~60% more range**. Nobody has to be told; a player who runs low once works it out, and
that is P1 taught by the fuel gauge instead of a tutorial.

| Sea | Glass | Calm | Light | Moderate | Lively | Rough | Gale | Storm |
|---|---|---|---|---|---|---|---|---|
| `seaTerm` | 1.00 | 1.09 | 1.17 | 1.26 | 1.34 | 1.43 | 1.51 | **1.60** |

#### 9.6.2 The per-hull proposal — grade, tank, burn, and what it buys

**All 38 shipped `BoatHullDef` assets, grouped by family.** Columns 2–4 are what the owner authors;
columns 5–7 are *derived* from them and are the reason the numbers are what they are.

*Cruise* = throttle 0.7, half hold, Moderate sea, no slip (the reference duty, multiplier **0.726**).
*Range* is at that duty. *Days/tank* is against a **reference working day of 12 minutes of engine
running** — about 40% of the 30-minute game day under power, the rest fishing, hauling or ashore.

| Hull id(s) | Grade | Tank (L) | `R` (L/h) | Cruise (L/h) | Endurance | Range | Days/tank | Fill @ R91 |
|---|---|---|---|---|---|---|---|---|
| `boat.dory` | *(none — she rows)* | **0** | — | — | — | — | — | — |
| `boat.dory_outboard` | gas | **10** | **23** | 16.7 | 36 min | 2.6 km | 3.0 | 16 ₲ |
| `boat.fishing_skiff` | gas | 12 | 28 | 20.3 | 35 min | 3.7 km | 3.0 | 19 ₲ |
| `boat.punt` | gas | **25** | 49 | 35.6 | 42 min | 4.1 km | 3.5 | 39 ₲ |
| `boat.punt_upgraded` | gas | 25 | 49 | 35.6 | 42 min | 5.1 km | 3.5 | 39 ₲ |
| `boat.console_skiff` | gas | 45 | 78 | 56.6 | 48 min | 7.8 km | 4.0 | 70 ₲ |
| `boat.sport_skiff` | gas | 60 | 105 | 76.2 | 47 min | 9.2 km | 3.9 | 93 ₲ |
| `boat.sport_skiff_mk2` | gas | 70 | 120 | 87.1 | 48 min | 10.5 km | 4.0 | 109 ₲ |
| `boat.sport_skiff_twin` | gas | 90 | 155 | 112.5 | 48 min | 11.4 km | 4.0 | 140 ₲ |
| `boat.zodiac_frc` | gas | 55 | 95 | 68.9 | 48 min | 11.5 km | 4.0 | 86 ₲ |
| `boat.zodiac_hurricane` | gas | 75 | 130 | 94.3 | 48 min | 11.8 km | 4.0 | 117 ₲ |
| `boat.lobster_inshore_*` **(6)** | ⚠ diesel | 45 | 69 | 50.1 | 54 min | 8.8 km | 4.5 | 63 ₲ |
| `boat.cape_islander` | diesel | **90** | 115 | 83.5 | 65 min | 11.4 km | 5.4 | 126 ₲ |
| `boat.lobster_boat` · `boat.lobster_standard_*` **(7)** | diesel | **85** | 105 | 76.2 | 67 min | 12.2 km | 5.6 | 119 ₲ |
| `boat.lobster_offshore_*` **(6)** | diesel | 160 | 170 | 123.4 | 78 min | 15.0 km | 6.5 | 224 ₲ |
| `boat.sport_fisher_convertible` | diesel | 320 | 340 | 246.7 | 78 min | 17.6 km | 6.5 | 448 ₲ |
| `boat.sport_fisher_skybridge` | diesel | 900 | 885 | 642.2 | 84 min | 17.6 km | 7.0 | 1,260 ₲ |
| `boat.side_dragger` | diesel | **320** | 275 | 199.6 | 96 min | 14.1 km | 8.0 | 448 ₲ |
| `boat.stern_trawler` | diesel | **700** | 535 | 388.2 | 108 min | 15.7 km | 9.0 | 980 ₲ |
| `boat.stern_trawler_mk2` | diesel | 750 | 575 | 417.3 | 108 min | 15.7 km | 9.0 | 1,050 ₲ |
| `boat.coastal_packet` | diesel | **2,000** | 1,250 | 907.1 | 132 min | 17.7 km | 11.0 | 2,800 ₲ |
| `boat.tanker` | diesel | **9,000** | 4,425 | 3,211.1 | 168 min | 18.4 km | 14.0 | 12,600 ₲ |

**Bold tanks are canon's** ([`boats-and-navigation.md`](boats-and-navigation.md) §1.1) read as litres
per §9.1 — unchanged, not re-tuned. The rest are placed by size within the family.

**Two things to read off the derived columns, both deliberate:**

⭐ **Reach grows up the ladder — 2.6 km → 4.1 → 7.8 → 11.4 → 15.0 → 17.6.** That is P2 made physical
in the one currency the player feels every trip. A dory tank is about four laps of St Peters; a Cape
Islander's is the coast.

⚠ **Range flattens above ~15 km, and that is fine.** The big hulls are *slow* (a stern trawler makes
3.45 m/s against a sport skiff's 5.6), so more tank buys more *endurance* than *distance*. Canon
already gates the far regions by **seaworthiness and draught**, not by fuel — fuel is the soft gate
(§1), and a soft gate that stopped mattering at the top would be the honest outcome of a design that
gates hard elsewhere. If the owner wants reach to keep climbing, raise the top-end tanks; the column
will show it immediately.

> ⚠ **On the speeds behind the derived columns.** They are computed from the shipped hull stats via
> the terminal-speed derivation the fleet tests use (`thrust·0.01 / (ForwardDrag·0.01 + mass/100 ·
> 0.2)`), which the tests themselves warn runs a little *fast* against real physics — the lobster
> boat derives 4.35 m/s and **measures 4.24**. Close enough to size a tank; **not** close enough to
> author `MeasuredTopSpeedMps` from (§9.5.2). Measure that one.

---

### 9.7 Worked example — the dory outboard

`boat.dory_outboard` · tank **10 L** · `R` = **23 L/h** · top speed ≈ **1.69 m/s**. Ned's motor.

| # | What she is doing | thr | hold | slip | sea | L/h | Over an hour | Speed |
|---|---|---|---|---|---|---|---|---|
| A | ambling out, empty, calm | 0.5 | 0 | 0 | Calm | **7.7** | 7.7 L | 0.84 m/s |
| B | cruise, half hold, moderate | 0.7 | ½ | 0 | Moderate | **16.7** | 16.7 L | 1.18 m/s |
| C | flat out, laden, punching home | 1.0 | full | 0.40 | Lively | **38.5** | 38.5 L | 1.01 m/s |
| D | ticking over while you hand-line | 0.0 | ½ | 0 | Moderate | **2.5** | 2.5 L | — |

⭐ **Read A against C.** She is doing the *same* 1 m/s over the ground in both — and burning **five
times** as much in C. The player who eases off and lets a fair tide do some of the work gets home;
the one who pins the throttle into a foul tide watches the can empty for nothing. That is P1 charged
to the fuel gauge, and it needs no words on screen.

**A reference day in the dory** — out to the shoal, hand-line, home laden:

| Leg | thr | sea | Minutes | L/h | Used |
|---|---|---|---|---|---|
| out to the shoal, empty | 0.6 | Calm | 4 | 10.3 | 0.68 L |
| ticking over, hand-lining | 0.0 | Calm | 10 | 2.1 | 0.35 L |
| home, laden, a bit of lop | 0.6 | Light | 5 | 13.2 | 1.10 L |
| **Total** | | | **19 min** | | **2.14 L of 10 L** |

**≈ 4.7 fishing days on a tank**, or ~3.0 days at the harder-working reference duty in §9.6.2 — so
**a fill every three or four days, at 16 ₲.** Against a 14 ₲ cod and a 6 ₲ load of ice, that is a real
line in the day's arithmetic and never a wall.

⭐ **And the opening survives it.** Ginny hands over the motor (§5); the player's first errand is a
can of gas at the store; a **20 L jerry can is two full tanks** and a **10 L can is exactly one**.
The humblest carried object in the game is a unit of range, which is the best possible way for a
fuel economy to introduce itself.

---

### 9.8 Worked example — a lobster boat

`boat.lobster_boat` · tank **85 L** · `R` = **105 L/h** · top speed ≈ **4.35 m/s** (measured 4.24).

| # | What she is doing | thr | hold | slip | sea | L/h | Over an hour |
|---|---|---|---|---|---|---|---|
| A | steaming out light | 0.85 | 0 | 0 | Light | **91.6** | 91.6 L |
| B | cruise, half hold, moderate | 0.7 | ½ | 0 | Moderate | **76.2** | 76.2 L |
| C | working along the string | 0.3 | 0.6 | 0.20 | Moderate | **24.5** | 24.5 L |
| D | flat out, full, in a gale | 1.0 | full | 0.45 | Gale | **199.3** | 199.3 L |

**The same day, run two ways.** Steam out, haul the string, steam home:

| | Sensible day | Greedy day |
|---|---|---|
| steam out | 6 min @ 0.85, light — 9.2 L | 10 min @ **full**, light — 20.5 L |
| hauling | 12 min @ 0.3 — 4.9 L | 20 min @ 0.35, hold filling, lively — 10.9 L |
| steam home | 6 min @ 0.85, laden, building — 12.8 L | 12 min @ **full**, full hold, **gale** — 39.9 L |
| **Burned** | **26.9 L** | **71.3 L** |
| Of an 85 L tank | 32% | **84%** |
| Days per tank | **3.2** | **1.2** |

⭐ **The tank is three days or one, and the boat has nothing to do with which.** The greedy day is not
punished — she gets home, with a bigger catch. She just gets home with **under 14 L in the tank**, and the
next morning's decision is whether to steam up the road station before or after the tide. That is
"cozy, but with teeth" priced in litres: no failure state, just a consequence that arrives one day
late and is entirely the player's own work.

⚠ **And it is a diesel boat.** So under §3 and §9.10, *neither* of those days can be refuelled at St
Peters. The lobster boat is where the geography gate closes.

---

### 9.9 Running dry — states, events, and who builds what

Canon is already settled (§1, [`boats-and-navigation.md`](boats-and-navigation.md) §3.6/§3.7): out of
fuel is a **guaranteed breakdown-class stop**, you lose propulsion, you drift, you are **stranded**,
and the small boats **row or sail home free**. This does not re-decide any of that. It states the
states and the wire, and hands the machine to the lane that owns it.

#### 9.9.1 The states

| State | Enters when | Player can | Leaves when |
|---|---|---|---|
| `Running` | fuel > `LowFuelFraction` | everything | fuel falls |
| `LowFuel` | fuel ≤ `LowFuelFraction` × capacity | **everything** — it is a *warning*, not a limp mode | refuelled, or dry |
| `Dry` | fuel ≤ 0 | no engine; helm and rudder still answer as she carries way | fuel is poured/filled in |
| `Adrift` | `Dry`, and she has a way home | **row / sail / anchor / wait** | under way again |
| `Stranded` | `Dry`, and she has **no** way home | radio a tow · flare · drift · anchor | help arrives |

`Adrift` and `Stranded` are canon's, not new: §3.7 lists drift, self-recover and tow as the four
options in order of player agency. The only thing §9.9 adds is **which of the two a dry boat enters,
and on what data.**

#### 9.9.2 The rowable boats — and the field that is missing

Canon: *"The dory and punt can row or sail home fuel-free; bigger boats cannot."* So a dry dory goes
to **`Adrift`** and never to `Stranded` — the oars stay live, which `BoatController.ApplyOarDrive`
already guarantees ("oar drive is applied REGARDLESS of grounding — the oars never stop answering").

⚠ **There is no honest field to test for this today, and the obvious one is a trap.** `OarPower` looks
like the signal — until you read the shipped assets:

> `boat.side_dragger` has **`OarPower: 300`**. So does `boat.cape_islander`, `boat.lobster_boat`,
> `boat.console_skiff`, `boat.sport_skiff` and `boat.sport_skiff_twin`. It is the field's default,
> left untouched on hulls that were never rowed — a 90-tonne side dragger is carrying a pair of oars
> in her data. Meanwhile all eighteen lobster variants and the zodiacs correctly carry 0.

Reading `OarPower > 0` would therefore let a **dragger row home from a dead engine**, and the bug
would look like a physics bug for a week. Proposed instead — an explicit field, defaulting to the
honest answer so a new hull is never accidentally rescued:

```csharp
[Tooltip("Can she get herself home with the engine dead — oars, or a sail she carries? Canon: the " +
         "dory and the punt can; bigger boats cannot (boats-and-navigation.md §3.6). This is what " +
         "decides whether running dry means ADRIFT (row home, free) or STRANDED (radio a tow).\n\n" +
         "Deliberately NOT inferred from OarPower: that field's DEFAULT is 300 and it was never " +
         "zeroed on several engine hulls, so the side dragger would row home.")]
public bool CanRowHome = false;
```

**Proposed true for exactly:** `boat.dory`, `boat.dory_outboard`, `boat.punt`, `boat.punt_upgraded`,
`boat.fishing_skiff`. **False for all other 33.** *(⏳ OWNER — the fishing skiff is the debatable one.)*

⚠ **`boat.dory_outboard` matters most here.** She is `PropulsionType.Engine`, so a naive "does she use
the oar helm" test would strand her — and she is the *one boat in the game canon promises can always
get you home*, on the player's first motor, at the moment the fuel economy introduces itself. Getting
this wrong breaks the opening. It is why the field is a field.

#### 9.9.3 The wire — one Core event, transitions only

Following `MooringLineChanged`'s stated convention exactly, and for the same reason:

```csharp
namespace HiddenHarbours.Core
{
    public enum BoatFuelState { Running, LowFuel, Dry }

    /// <summary>Raised on a fuel-state TRANSITION for one hull. Never per tick.</summary>
    public readonly struct BoatFuelStateChanged
    {
        public readonly string HullId;
        public readonly BoatFuelState From, To;
        public readonly float Litres;       // at the transition
        public readonly float Fraction01;   // litres / capacity
    }
}
```

⚠ **Transitions only, and this is not a style preference.** The live level changes every fixed tick;
an event per tick is a per-tick allocation on the hot path, and `MooringLineChanged` already documents
the pattern for consumers that want a continuous read: **take the beat to arm yourself, then read the
boat's own live value.** The gauge does that. The event does not carry the gauge.

**Who listens, and nobody reaches across a lane** (rule 4):

| Listener | Lane | Does |
|---|---|---|
| the fuel gauge / the can's fill read | **ui-ux** (diegetic) | arms on `→ LowFuel`; reads the live level thereafter |
| engine note, the cough, the silence | **audio** | one beat per transition is exactly what adaptive audio wants |
| `RescueController` / the danger systems | **gameplay-systems** | translates `→ Dry` into its **existing** breakdown → drift → stranded path |

⭐ **`→ Dry` does not raise a stranding.** It raises a *fuel* fact, and the danger lane — which already
owns `FuelCheck`, `OnBreakdown` and `OnStranded` in canon's §9 architecture — decides what that means
alongside grounding, holing and a fouled prop. Fuel must not open a second, parallel road to the same
set-piece; there is one stranding system and it has one door. **economy-sim raises the event and
prices the tow; gameplay-systems owns the machine.** (The charter is explicit: *"tow as a gameplay
event → gameplay-systems (you set tow pricing)"*.)

#### 9.9.4 The counterplay the player already has

Nothing new is needed to make running dry fair, because §4 already built the answer: **a spare can.**
A 20 L jerry can in the dory is a second full tank, bought for 31 ₲ and paid for in hold space. A can
aboard turns `Dry` into a thirty-second **POUR** (§9.4) and a story about being careless; no can
aboard turns it into a tow bill. That is the whole P5 shape — the disaster is one purchase away from
being an inconvenience, and the purchase is a decision made *before* leaving, which is the decision
worth having.

---

### 9.10 Which hulls are diesel — PROPOSAL (⏳ **OWNER'S CALL**)

Owner canon (§2) is the rule: *"if you drive it with a tiller or a wheel over an outboard, it drinks
gas. If it has an engine room, it drinks diesel."* Applied to the 38 shipped hulls — and the fleet
turns out to have **already answered it in the art**: the boat-interiors kit gave a below-decks space
to exactly the inboard boats.

| Grade | Hulls | Count | Signal |
|---|---|---|---|
| **gas** | `dory_outboard` · `fishing_skiff` · `punt` · `punt_upgraded` · `console_skiff` · `sport_skiff` · `sport_skiff_mk2` · `sport_skiff_twin` · `zodiac_frc` · `zodiac_hurricane` | **10** | outboard-driven; no interior in the kit |
| **diesel** | `cape_islander` · `lobster_boat` · `lobster_standard_*` (6) · `lobster_offshore_*` (6) · `sport_fisher_convertible` · `sport_fisher_skybridge` · `side_dragger` · `stern_trawler` · `stern_trawler_mk2` · `coastal_packet` · `tanker` | **21** | all carry a baked interior; the sport fishers' rigs name an `engine_room` outright |
| **⚠ the line** | `lobster_inshore_*` (6) | **6** | 8.6 m, hardtop and open, *with* interiors |
| **none** | `boat.dory` | **1** | she rows |

**The one real decision is the six inshore lobster variants**, and it is a design call, not a fact:

| | Make them **diesel** (proposed) | Make them **gas** |
|---|---|---|
| The moment the gate closes | at the **first lobster boat** — the step up from the punt | later, at the 12 m standard boats |
| Feel | *"the day you go lobstering, home stops being able to fuel you"* — the P2 beat lands hard and early | the ladder's first real workboat still lives out of St Peters; the gate arrives with the *career* boat |
| Risk | the fuel run to Nine Mile Creek becomes routine early; if it grates, it grates for a long stretch of the game | the geography gate is deferred, and may land after the player has stopped noticing distances |
| Realism | 8.6 m Maritime inshore lobster boats are overwhelmingly diesel inboards | some are gas; nobody will object |

⭐ **Recommend diesel**, on the strength of §3's own argument: *"the moment you own a diesel boat, home
can no longer fuel her"* is called the best thing in the drop, and it is worth **more** the earlier it
arrives — provided it arrives when the player has a boat worth making the trip for. The first lobster
boat is exactly that boat.

⏳ **This is the owner's, and it is one field on six assets either way.** Nothing else in this spec
changes with the answer.

---

### 9.11 The premix question — costed both ways (⏳ **OWNER'S CALL**; answers §7 Q4 / §8.5 Q4)

Does Ned's old two-stroke burn `mixed`? The grade exists, is baked in 21 container Defs, and is
priced at Route 91 (2.35 /L) with the wharf row **authored and switched off**.

#### Option A — she burns `mixed`

The player's first fuel errand becomes premix.

| What it costs | Detail |
|---|---|
| ⚠ **The opening beat** | St Peters sells **gas only**, over a counter (§3). The wharf pumps do **not** stock `mixed` (§8.2). So the first errand is a walk up **Route 91** — not a can from Ginny's shelf, and not a step down the dock. That is the actual price of Option A, and it is not small: §5's *"your first errand is buying gas"* stops being true. |
| Fixing that | one of — (a) St Peters stocks premix too, (b) tick `mixed` on at the wharf (the price is already sitting there, §8.2), or (c) **you mix it yourself** |
| Art | **none.** `oil` is already baked in every carriable size (`jug_s1_oil` … `jerry_s25_oil`). An oil tin exists. |
| Content | one Ginny line, and a `mixed` row on `StPetersGeneralStore` or the wharf |
| Player cost per dory tank | premix **23.5 ₲** vs gas 15.5 ₲ — **+52%** |

⭐ **If Option A, choose (c).** Mixing your own is the only version that *earns* the fiddliness:

| Doing it | Dory tank (10 L) | vs. buying premix |
|---|---|---|
| buy `mixed` at Route 91 | 23.5 ₲ | — |
| buy `gas` + `oil`, mix at 50:1 (0.20 L oil) | 15.5 + 1.7 = **17.2 ₲** | **−27%** |
| buy `gas` + `oil`, mix at 24:1 (0.42 L oil, an *old* motor) | 15.5 + 3.6 = **19.1 ₲** | **−19%** |

That is P4 in miniature — **do it by hand and it is a fifth cheaper; buy it ready-made and pay for the
convenience** — on the very first consumable in the game, using art that already exists. The ratio is
a tunable, not a fact (`GameConfig.Fuel.PremixRatio`).

#### Option B — she burns straight `gas` (recommended)

Costs **nothing**. `mixed` stays exactly what it is today: a real, priced, purchasable grade that the
NPC fleet burns and the player can ignore. The opening is untouched.

⭐ **The binding argument is that Option B forecloses nothing.** Turning Ned's motor onto premix later
is **one string on one Def** (`boat.dory_outboard.FuelGrade = "mixed"`) plus a store row. There is no
version of this that gets harder by waiting, and there is a version of it that risks the opening by
rushing. **Ship B; revisit when the opening is playable and the owner can feel whether the errand
wants more texture.**

⏳ **Owner's call. Charming or fiddly is still a taste question and still not a research one** — but it
is now a taste question with a price tag on both sides.

---

### 9.12 Acceptance criteria

**Data & units**
- [ ] `1 FU = 1 L` is stated once, in `FuelGrades`' or the tank field's remarks; **no conversion
      constant exists anywhere in the code.**
- [ ] `FuelCapacityLitres`, `FuelGrade`, `FullThrottleLitresPerHour` on `BoatHullDef`; all 38 shipped
      hulls authored to §9.6.2 (or deliberately left at 0).
- [ ] Content validation fails on: a non-empty `FuelGrade` outside `FuelGrades.All`; a capacity > 0
      with `R` = 0; a capacity > 0 with an empty grade; `boat.dory` carrying a tank.
- [ ] **Not one fuel number appears in a `.cs` file.** Tanks and burn rates are Def fields; curve
      shape is `GameConfig.Fuel`. (A float *tolerance* is not a tunable — `FuelPricing.MinLitres` is
      the precedent.)
- [ ] `GameConfig.Fuel` is hand-added to `Data/Config/GameConfig.asset`'s YAML **in the same PR**;
      `GameConfigAssetCoverageTests` green.

**The burn**
- [ ] The rate function is **pure and static** — no Unity types, no RNG, no clock — and EditMode-
      testable in the shape of `FuelPricing.Quote`.
- [ ] Monotonicity tests: burn is non-decreasing in `|throttle|`, in load and in `seaState01`.
- [ ] Determinism test: the same input sequence integrated twice gives **bit-identical** litres.
- [ ] Frame-rate independence test: the same trip at two fixed-step counts consumes the same fuel to
      within tolerance; **integration reads `Time.fixedDeltaTime`, never a frame delta.**
- [ ] Clock-independence test: **doubling `GameConfig.SecondsPerDay` changes fuel used on a fixed
      trip by 0.** (The regression §9.5.3 exists to prevent.)
- [ ] The §9.7 and §9.8 worked examples are pinned as test cases, so a re-tune shows up as a diff in
      numbers a human has read rather than a silent drift.

**The tank, the verbs, the save**
- [ ] The tank implements `IFuelVessel`; **`FuelPump` is unchanged** except for adding
      `InteractContext.Aboard` — if the pump needed edits, the seam was wrong.
- [ ] **FILL** at a pump charges at the site's posted price and honours every §8.3 rule unchanged
      (round up once, pour-what-you-can-afford, grade refusals in words).
- [ ] **POUR** from a carried can moves fuel, charges **nothing**, refuses on grade mismatch in
      words, and empties the can by exactly what the tank took.
- [ ] `SaveData.HullFuel` + `SchemaVersion` 14; a v13 save loads with **every boat full** and no row
      written; a dry boat round-trips as an explicit `0`; levels clamp to capacity on load.
- [ ] `SaveMigration` v13→v14 **only adds** — no heal, no back-fill, no reinterpretation.

**Running dry**
- [ ] `BoatFuelStateChanged` fires on transitions **only**; a PlayMode test asserts **zero** events
      across a long steady run.
- [ ] `CanRowHome` exists as its own field; a test asserts `boat.dory_outboard` is **true** and
      `boat.side_dragger` is **false** *(the `OarPower: 300` trap, pinned)*.
- [ ] A dry rowable boat keeps full oar authority and can make way; a dry non-rowable boat loses
      propulsion and the danger lane's existing stranded path takes it.
- [ ] **economy-sim ships no stranding state machine.** If this lane wrote one, the handoff failed.

**Standing**
- [ ] `docs/design/fuel-and-refuelling.md` §8.4's "what is NOT wired" list is corrected in the same
      PR that wires each item. A stale warning is worse than none.
- [ ] The playable build still runs with **zero hulls authored** — a fleet of `FuelCapacityLitres = 0`
      boats behaves exactly as today.

---

### 9.13 The backlog — one build lane

Sized for a single local lane, in order. **F1–F4 and F6 are `economy-sim`; F5 is a handoff, not a
build.** Each is a small, single-purpose PR that leaves the build playable (rule 10).

| # | Item | Lane | Depends on | Acceptance |
|---|---|---|---|---|
| **F1** | **The tank as data.** Three fields on `BoatHullDef` + content validation. No behaviour. | economy-sim | — | §9.12 "Data & units", minus the authoring pass |
| **F2** | **The burn model.** Pure rate function + `GameConfig.Fuel` (+ the asset YAML) + tests. Nothing consumes it yet. | economy-sim | F1 | §9.12 "The burn" |
| **F3** | **The tank is a vessel, and the two verbs.** `IFuelVessel` on the boat; `FuelTransfer` + `Draw` in Core; **FILL** aboard; **POUR** from a can. | economy-sim (+ **lead-architect** for the Core seam) | F1 | §9.12 "The tank, the verbs" |
| **F4** | **Fuel persists.** `HullFuel` + save v14 + migration + round-trip tests. **Closes the §8.4.2 leak — tell the world lane, pumps can be placed after this.** | economy-sim → lead-architect sign-off | F1, F3 | §9.12 save rows |
| **F5** | **Running dry — the event and the handoff.** `BoatFuelState` + `BoatFuelStateChanged` in Core; `CanRowHome` on `BoatHullDef` + the 5 hulls set; the low-fuel telegraph fires. **The breakdown → drift → stranded machine is `gameplay-systems`' and is NOT in this lane.** | economy-sim raises · **gameplay-systems** consumes | F2, F4 | §9.12 "Running dry" |
| **F6** | **The numbers pass.** Author all 38 hulls to §9.6.2; pin the worked examples; a test that re-derives endurance/range from the authored Defs so a re-tune cannot silently break the ladder. | economy-sim | F1, F2 | the ladder in §9.6.2 holds; §9.7/§9.8 pinned |

**Deliberately NOT in this lane**, and each has a reason:

- **The fuel gauge.** `ui-ux`, and diegetic — §8.3's ruling stands: the level reads off the object,
  not a screen. F5 gives it the beat it arms on.
- **Tow pricing.** Real economy-sim work (§3.7 scales it by distance and boat size), but it belongs
  with the rescue machine, not with the tank.
- **St Peters' over-the-counter gas.** §8.4.6 already places it as a `SupplyDef` beside the ice. It
  is the thing that makes **POUR** matter at home, and it is a separate small piece of work.
- **Shore storage** (drums, a shed tank, a wharf bowser). §4 defers it to M2+; nothing here needs it.
- **Fuel as an operating cost line** in the business layer ([`economy-and-business.md`](economy-and-business.md)
  §620). M3, and it wants §9.5.3's derived trip cost, not per-tick simulation.

---

### 9.14 ⚠ Flagged, not solved

**Stove oil is sellable and uncarryable — and it is an art ask, not an economy one.**

Route 91 stocks `stove_oil` at 1.20 /L (§8.2) and **there is no container in the game that can hold
it.** All 84 baked `FuelContainerDef` assets are `gas` · `diesel` · `mixed` · `oil` — the fuel rig
predates the grade, which was added later because the island burns oil
([`municipal-infrastructure.md`](municipal-infrastructure.md)). So the pump will always refuse, in
words, with `FuelRefusal.NoVessel`.

- **Nothing in §9 changes this**, and nothing in §9 should. **No hull burns stove oil** — it heats
  houses, not engines — so the tank, the burn model and every verb above are complete without it.
  The gap is entirely on the **retail + carry** side.
- **The ask goes upstream to `art-director`:** one colourway per carriable size in the fuel rig
  (`docs/art/rigs/gas-station-rig/`, whose internal key for the grade is `kero`), landing as
  `fuelstore.stove_oil_*` Defs through the existing `FuelContainerDefBuilder`. The brief is
  [`../art/briefs/fuel-and-fuel-storage.md`](../art/briefs/fuel-and-fuel-storage.md).
- Until then it stays exactly as §8.4.4 left it: **named in the validation test as a known gap**, not
  hidden and not worked around.

**Two smaller flags, both raised above and repeated here so they are not lost in prose:**

- ⚠ **`OarPower: 300` is a stale default on six engine hulls**, including `boat.side_dragger`. It is
  harmless today and becomes a rescue bug the moment anything reads it as "she has oars" (§9.9.2).
  Worth zeroing on the hulls that never row, independently of this feature — `gameplay-systems`.
- ⚠ **`MeasuredTopSpeedMps` must be measured, not derived** (§9.5.2), and the fleet tests already
  warn about exactly this mistake in exactly this repo. Until the field exists, ship with
  `SlipLoadWeight = 0`.

---

### 9.15 ⏳ Open calls, collected

**OWNER**

1. **Are the six `lobster_inshore_*` variants diesel?** (§9.10) — decides how early home stops being
   able to fuel you. *Recommended: yes.* One field on six assets either way.
2. **Does Ned's two-stroke burn premix?** (§9.11 — the standing §7 Q4 / §8.5 Q4, now costed)
   *Recommended: no, and revisit — the change is one string whenever you want it.* If yes,
   **mix-it-yourself** is the version worth having.
3. **Is `1 FU = 1 L`?** (§9.1 — the standing §8.5 Q2) *Recommended: yes.* Everything shipped already
   speaks litres and the money lands where it should.
4. **Are the §9.6.2 tanks and burn rates about right?** The columns to read are **Days/tank** and
   **Fill @ R91** — 3 days and 16 ₲ for the dory, 5.6 days and 119 ₲ for the lobster boat. If fuel
   feels too thirsty or too free *across the board*, that is **one number** (`BurnScale`), not 38.
5. **Can `boat.fishing_skiff` row home?** (§9.9.2) — the one genuinely debatable entry in the five.
6. **Standing, unchanged:** should the wharf pumps sell premix and oil? (§8.5 Q3). §9.11 Option A
   would force the answer to yes; Option B leaves it as the owner's taste call it is today.

**LEAD-ARCHITECT**

7. **`Draw` on `IFuelVessel`, or a transfer that leaves Core's surface alone?** (§9.4)
   *Recommended: `Draw`.*
8. **Save v14's shape**, and the "missing row = full" reading of it (§9.3).
9. **Where fuel lives when `EngineDef` arrives** — grade + burn to the engine, capacity stays on the
   hull (§9.2). Naming it now so the eventual split is a move, not a redesign.

**GAMEPLAY-SYSTEMS**

10. **`CanRowHome` as a `BoatHullDef` field**, and the `OarPower` default it exists to avoid (§9.9.2).
11. **`MeasuredTopSpeedMps`**, measured on the existing fleet harness, whenever `SlipLoadWeight` is to
    come off zero (§9.5.2).
