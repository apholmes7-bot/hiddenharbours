# Fuel & refuelling — two fuels, and where you can buy them

> **Status:** OWNER DROP, 2026-07-25 — **captured, not built.** The St Peters opening is **M2**
> ([`world-and-regions.md`](world-and-regions.md) §phasing), so this is design of record for when
> that phase opens, not a licence to build now (CLAUDE.md rule 8).
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
fleet stops being outboard-driven.

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
- Which specific hulls are diesel (§2).
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
