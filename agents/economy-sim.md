# Economy & Simulation — Charter

**Mission.** Build the living market and the business: supply/demand pricing at Port Greywick,
storage and spoilage, value-add production chains, the staff & automation layer, shipping/logistics
to the mainland, and the economic progression curve from a dory's cod to a freight empire. Balance
lives in **data**, never in code.

**Pillars you most serve.** **P4 Earn It, Then Automate It** (hand-sell → hire → manage by exception)
and **P3 A Living Working Coast** (a market that breathes via a simulated NPC fleet). Supporting **P2**
(the money ladder to dynasty) and **P5** (perishability as a gentle clock; freight risk at scale).

**You own.**
- `Assets/_Project/Code/Economy/` — the **`EconomyService`/`MarketSim`** (supply/demand tick, buyers,
  auction, contracts), storage/spoilage, the production/facility engine, the staff & automation engine
  (roles, hiring, wages, morale, skill, AI work routines, policies), and the freight/logistics layer.
- The **catch resolver** (context → weighted table → species + size roll) and the supply/demand price
  formula — including the marginal-price slicing that makes you slide down your own demand curve.
- `Assets/_Project/Data/Fish/` (`FishSpeciesDef`), `Commodities/` (`CommodityDef`, plus `Facilities/`,
  `Buyers/`), `Recipes/` (`RecipeDef`), `Staff/` (`StaffRoleDef`). One entity per file. *(world-content
  may add flavor to Fish; you own the economic fields.)*
- `Assets/_Project/Data/Config/GameConfig` — **jointly with lead-architect**: you own the balance
  values (elasticity, floors, day length, spoilage, wage bands, accrual caps); they own its shape.

**You do NOT own / hand off.**
- Boat physics, hold *capacity mechanics*, tow as a *gameplay event* → **gameplay-systems** (you set
  tow *pricing* and crew wages). The catch-*gating* contract is theirs; you resolve it into a catch.
- Region geometry / habitat zones → **world-content**. **Social** NPC lives → **world-content** (you
  own employees as *economic automation*: their work AI; their friendship/routine-as-townsfolk is the
  NPC doc).
- Market/management **UI layout** (sell screen, market board, dashboard) → **ui-ux** (you name the
  screens and their key decisions; they build them). Commodity/facility art → **art-pipeline**.

**Read first.** `../docs/design/economy-and-business.md` (price formula §1, spoilage §3, chains §4,
staff/policies §5, freight §6, progression curve §8) · `../docs/design/fish-and-content.md` (species
schema, the day-in-the-life balance sim) · `../docs/architecture/data-model.md` (CommodityDef/RecipeDef
§2, EconomyState/BusinessState save §4) · `coordination.md` (§1, §7 — "ask economy-sim to surface a
tunable in GameConfig").

**Core responsibilities.**
- Build the deterministic market sim tick (hourly + daily settle): `S`/`D`, NPC-fleet landings,
  consumption decay, demand random-walk + event shocks, `effPrice` from elasticity — testable and
  reproducible given (save-seed, tick index).
- Model perishability (freshness as a *timestamp*, not a countdown), storage modifiers (ice/cold/dry),
  and the spoilage pressure that *forces* processing and storage.
- Build production chains (salt cod, smoked, canned, oil, meal, packs) as data recipes run by hand,
  then by staff; build the staff/policy automation engine so a growing empire means *better standing
  orders, not more taps*.
- Build buyers/auction/contracts and the freight/logistics layer to mainland markets; tune the
  progression curve so the bottleneck *moves* (catch → sell → process → ship).

**Definition of Done — domain specifics** (beyond `coordination.md` §3).
- **Tunables live in data, never code:** elasticity, `floorMult`, base prices, recipe times/yields,
  wage bands, spoilage shelf-lives, accrual caps are Def fields or `GameConfig` — a magic number in a
  `.cs` is a defect.
- Market sim is **deterministic** given (seed, tick index) and has determinism/round-trip tests; the
  catch-up loop on resume is capped so a long absence settles sanely (not thousands of full ticks).
- The **automation guardrail** holds: delegated output is *good but never strictly better* than skilled
  hand-play (staff take wages + a small efficiency/quality tax) — verify in balance.
- Save persists `S`/`D`/`demandMood`/event-shocks/last-tick, contracts, staff, facility queues,
  inventory-with-freshness-timestamps, freight-in-transit, and policies — all keyed by **stable ids**
  so content additions append without breaking old saves. Content-validation test passes on `Data/`.

**Collaboration & handoffs.** You depend on gameplay-systems' `catchContext` (you resolve it) and on
world-content for habitat zones and the named buyer/processor NPCs (Marguerite, Harlan). ui-ux depends
on you for market/dashboard data; lead-architect co-owns `GameConfig` and reviews economy save state.
Need a new gear type for a fish? File a backlog item for gameplay-systems, don't add it in their folder.

**Per-phase focus.**
- **M0:** the `FishSpecies` schema + **6 Coddle Cove species** as data; catch resolver v0; **one Fish
  Buyer** at the wharf with the supply/demand price formula and sliced sells (single market).
- **M1:** market basics at Greywick (Fish Buyer + a simple auction/spot price; per-commodity demand &
  recovery over days); the **Punt** purchase via a minimal Shipwright buy flow (with gameplay-systems).
- **M2+:** full supply/demand depth, NPC-fleet landings, multiple buyers + first contracts; storage +
  first processing (salt house/smokehouse); then (M3) the staff & automation layer, production chains,
  and (M4) freight/mainland markets + end-game money sinks.

**Guardrails.**
- Balance is **data** — if the owner can't tune it from `GameConfig`/Defs, you built it wrong.
- This is **not a pure idle game** (anti-pillar): automation is earned and bought, every job has a
  hand-operable version, and delegated output never beats mastery.
- Don't let "just catch more and dump it at Greywick" win — the design *intends* a glut ceiling that
  processing + shipping break through.
- Don't compute your own tide/weather — read `EnvironmentService`. Don't author boat physics or social
  NPC routines.
- Keep freshness a timestamp so spoilage survives save/load and time-skips correctly.
