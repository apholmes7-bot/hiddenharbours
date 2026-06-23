# Hidden Harbours — Economy & Business (DESIGN)

> Sibling docs: [`../vision-and-pillars.md`](../vision-and-pillars.md) (CANON — wins on conflict),
> [`fish-and-content.md`](fish-and-content.md), [`boats-and-navigation.md`](boats-and-navigation.md),
> [`npcs-and-routines.md`](npcs-and-routines.md), [`progression-and-housing.md`](progression-and-housing.md),
> [`world-and-regions.md`](world-and-regions.md), [`time-tides-weather.md`](time-tides-weather.md),
> [`ux-and-mobile-controls.md`](ux-and-mobile-controls.md), [`../adr/0003-data-driven-content.md`](../adr/0003-data-driven-content.md).
>
> **Scope:** the **Schedule-I / Big-Ambitions layer** — the living market at **Port Greywick**, storage,
> value-add manufacturing, shipping/logistics to mainland markets, and the **staff & automation** that
> turns the player from laborer into owner. This is where the *dynasty* end of P2 is paid off and where
> P4 ("Earn It, Then Automate It") mostly lives.

---

## 0. Design intent (which pillars this serves)

| Pillar | How this doc serves it |
|---|---|
| **P2 From Dory to Dynasty** | The money ladder: hand-selling a dory's cod at the Greywick wharf → storing & timing sales → processing into value-add goods → shipping bulk freight to the mainland with the Coastal Tanker. Each rung is a *visible* leap in scale and capability. |
| **P3 A Living Working Coast** | The market **breathes**: a simulated NPC fleet lands fish too (gluts and scarcity happen without you), prices move on a sim tick, buyers have moods and contracts. The town runs whether you fish or not. |
| **P4 Earn It, Then Automate It** | You do **every** job by hand first — catch, haul, sell, process — so it has weight. Then you **hire crew** and **build facilities** to automate the tedium and shift to *owner*. Managers progressively remove micro-management as the empire grows. |
| **P5 Cozy, but with Teeth** | Perishable catch creates *time pressure* (sell or process before it spoils); bulk shipping carries *risk* (weather, loss). Stakes, not stress. |

> **Anti-pillar guardrail (canon §6):** this is *not* a pure idle game. Automation is **earned and
> bought**, never free, and there is always a hand-operable version of every job. The fun at the top is
> *running a business and a fleet*, not watching numbers tick.

---

## 1. The supply & demand market at Port Greywick

The market is the heart of P3. Every tradeable thing is a **`Commodity`** (a ScriptableObject —
see §8.1). Raw catch maps to commodities (each `FishSpecies` resolves to a commodity by id; see
[`fish-and-content.md`](fish-and-content.md)), and so do processed goods (salt cod, fish oil, etc., §4).

### 1.1 The core idea

Each commodity, **at each market**, has:
- a **base price** `P0` (reference value of one unit/kg),
- a **demand level** `D` (how much this town wants it right now — sets the ceiling),
- a **local supply level** `S` (how much has recently been landed/sold into this market),
- an **elasticity** `e` (from the commodity/category — perishables and gluttable schooling fish are
  high-`e` and crash fast; rare/luxury goods are low-`e` and hold value).

**Selling raises `S` → price falls (glut). Time passes → `S` decays back down → price recovers
(scarcity).** The simulated NPC fleet also raises `S`, so the market moves on its own.

### 1.2 The price formula

For a unit of commodity `c` at market `m` at time `t`:

```
ratio       = S / D                         // supply-to-demand pressure, ≥0
priceMult   = clamp( 1 / (1 + e · ratio) , floorMult, 1.0 )
effPrice    = P0 · demandMood · seasonDemand · priceMult
unitSale    = effPrice · qualityMult · freshnessMult
```

- `priceMult` falls from **1.0** (supply ≪ demand) toward **`floorMult`** (default 0.15 — a glut never
  pays *nothing*, but it pays badly). Higher `e` ⇒ the curve drops faster for the same `ratio`.
- `demandMood` ∈ ~[0.8, 1.3] — a slow per-market random walk + event shocks (a festival, a mainland
  buyer's order, a storm that closed another harbour) so prices feel alive (P3).
- `seasonDemand` — some goods are worth more in season (smoked fish before winter; fresh lobster in
  tourist summer).
- `qualityMult` — from gear/skill/handling (a well-iced, large, carefully-landed fish grades higher).
- `freshnessMult` — from spoilage (§3): falls from 1.0 toward a salvage floor as raw catch ages.

**Selling moves the market within a transaction:** a large lot is sold in **slices** so you slide *down*
your own demand curve (dumping 2,000 kg of mackerel at once tanks the back half of the lot). The UI
shows the **marginal price** as you set the sell quantity (P3 legibility) — this is the central
"read the market" decision, the trade analogue of reading the tide.

### 1.3 The market simulation tick

Runs on a coarse cadence (every in-game hour, plus a daily settle), independent of the player:

```
each tick, per market m, per commodity c:
  S += npcFleetLandings(c, m, season, weather)      // the simulated coast lands fish (P3)
  S += playerSalesSinceLastTick(c, m)               // applied as you sell, sliced
  S -= consumption(c, m) = D · consumptionRate       // the town/exports eat supply
  S  = max(0, S)                                      // floors at zero (scarcity)
  D  = randomWalk(D, demandMood drift) + eventShocks  // demand drifts & reacts
recompute effPrice from §1.2
```

- **Decay = recovery:** because consumption pulls `S` down every tick, a price you crashed today
  **recovers over days** if landings ease — rewarding patience, storage (§2), and selling across
  *multiple* markets (§1.4). `e` controls *how fast* (perishables both crash and recover faster).
- **NPC fleet landings** are a lightweight model keyed to season/weather/region (the herring run floods
  herring in summer; a long storm suppresses all landings, then a glut on the first calm day). This is
  what makes the world feel *worked* even when the player is ashore (P3). It is **not** a full agent sim
  of every NPC — it's a believable supply curve. (Named NPCs' *social* lives are
  [`npcs-and-routines.md`](npcs-and-routines.md); their *economic output* is this aggregate.)

### 1.4 Multiple buyers / markets, auction house, contracts

Greywick is the hub, but it is **not one price**:

- **The Fish Buyer (wharf):** instant, fair-ish spot price, takes anything, slightly below auction.
  The early-game default — cash now.
- **The Auction House (Greywick):** consign a lot; price found by simulated bidders over a short
  window; higher expected value but slower and variable. Big or premium lots shine here. Unlocked early
  per canon (Port Greywick is an early story unlock).
- **Specialty buyers (shops/restaurants):** the restaurant pays a premium for fresh prize fish and
  live lobster but in small quantity; the cannery buys herring/mackerel cheap in bulk; the apothecary
  wants fish oil; etc. Each has its own `D`/elasticity — different buyers absorb gluts differently.
- **Standing contracts:** a buyer (local or mainland) offers **fixed price × quantity × cadence** for a
  term (e.g., "200 kg haddock/week for the season at a locked rate"). Contracts **trade upside for
  stability** — they're immune to gluts but penalize non-delivery (reputation + fee). The backbone of
  automated income (staff can fulfill them, §5) and the on-ramp to the freight economy (§4/§6).
- **Mainland & distant markets (shipping):** higher base prices and different demand, reachable only by
  freight (§6). This is the dynasty-scale outlet (P2).

> **Reputation** lightly modulates contract availability, auction trust, and `demandMood` with specific
> buyers (deliver well → better offers; default a contract → worse). Detailed rep/relationship systems
> live in [`npcs-and-routines.md`](npcs-and-routines.md); the economy just reads a per-buyer rep scalar.

### 1.5 Elasticity & defaults per category

Elasticity comes from the commodity (raw catch inherits its `FishSpecies` category default; processed
goods set their own). Higher `e` ⇒ crashes & recovers faster.

| Commodity class | Elasticity `e` | floorMult | Why |
|---|---:|---:|---|
| Schooling pelagic raw (mackerel, herring, capelin) | 0.60 | 0.12 | Runs cause huge gluts; cheap, volatile. Push players to process or contract. |
| Estuary/brackish & tidepool raw | 0.55 / 0.50 | 0.15 | Perishable, modest demand. |
| Inshore groundfish raw (cod, haddock, pollock) | 0.45 | 0.15 | Steady demand, floods if overfished. |
| Deepwater/Banks raw (halibut, redfish, monkfish) | 0.40 | 0.18 | Bigger, steadier value. |
| Shellfish raw (lobster, crab, scallop, clam) | 0.35 | 0.20 | Premium, sticky demand; holds value better. |
| Storm-grounds/Ironbound rarities | 0.30 | 0.22 | Rare; resists gluts. |
| **Processed staples** (salt cod, smoked fish, canned, fishmeal) | 0.25 | 0.25 | Shelf-stable, broad/export demand — *the whole point of refining is price stability*. |
| **Premium processed** (packaged scallops, fish oil, cooked-lobster packs) | 0.20 | 0.30 | Luxury/export; high value, slow to crash. |
| Legendary catches | 0.10 | 0.40 | One-offs; trophy/quest, minimal market footprint. |

> **The central economic tension (P2/P4):** *raw* catch is volatile, perishable, and crashes when you
> scale up (your own success gluts your home market). **Processing** (§4) and **shipping** (§6) convert
> volatile, perishable raw into stable, durable, export-grade value — which is *why* you build the
> business. The market is designed so that "just catch more and dump it at Greywick" hits a ceiling,
> and the way *through* the ceiling is value-add + logistics + automation.

---

## 2. Storage

Storage lets you **time the market** (sell when prices recover, not when you happen to land) and
**buffer spoilage** — but capacity is finite and costs money, so hoarding has limits.

| Store | Holds | Effect on spoilage | Notes |
|---|---|---|---|
| **Boat hold** | Raw catch, at sea | Spoils at full/raw rate (ice slows it) | Capacity per boat ([`boats-and-navigation.md`](boats-and-navigation.md)). Forces trips home. |
| **Ice / wet well** (boat or wharf upgrade) | Raw fish / live shellfish | Slows spoilage ×0.5; live well keeps shellfish *alive* (premium) | First storage upgrade; cheap, early. |
| **Cold storage (warehouse)** | Raw & some processed | Slows spoilage ×0.2; arrests it for frozen goods | A building you buy/upgrade. The "time the market" tool. |
| **Dry warehouse** | Shelf-stable processed goods (salt cod, canned, meal, oil) | No spoilage | Bulk capacity for export staging (§6). |
| **Bait locker** | Bait stocks | Slows bait spoilage | Feeds gear/automation. |

- **Capacity is an upgrade axis** (per facility), governed by [`progression-and-housing.md`](progression-and-housing.md)
  for *ownership/upgrade mechanics*; this doc owns the *economic function* (timing vs. spoilage vs.
  carrying cost). Cold storage may carry a small running cost so infinite hoarding isn't free.
- **Strategic use:** crash-proof a glut by *storing* the surplus and selling as price recovers; or
  stockpile cheap in-season raw to *process* in the off-season; or stage processed goods until a
  freight run fills (§6).

---

## 3. Perishability & spoilage (the pressure)

Raw catch is on a clock. Each raw commodity has a `perishability` (from its `FishSpecies`:
`Hardy/Standard/Perishable/HighlyPerishable`). Freshness drives `freshnessMult` (§1.2) from **1.0 → a
salvage floor** over time:

```
freshnessMult = lerp( 1.0, salvageFloor, ageHours / shelfLifeHours )   // clamped
shelfLifeHours scaled by storage modifier (ice ×, cold ×, dry = ∞ for processed)
```

| Perishability | Raw shelf life (no ice) | Salvage floor | Examples |
|---|---|---|---|
| HighlyPerishable | ~half a day | 0.15 (→ fishmeal/bait/oil) | Oily pelagics: mackerel, herring, capelin |
| Perishable | ~1 day | 0.25 | Shellfish, smelt, eel, estuary fish |
| Standard | ~2–3 days | 0.35 | Most groundfish |
| Hardy | several days | 0.5 | A few robust/processed-bound species |

- **Spoiled-but-salvageable:** instead of becoming worthless, badly-aged raw is best routed to
  **low-grade processing** (fishmeal, oil, bait) — a *floor* on bad luck and a feeder for value chains.
  Truly rotten stock can be discarded/composted.
- **This is the engine that makes processing and storage matter (P4 pressure):** you physically cannot
  out-fish your ability to *sell, ice, store, or process* — so you build the infrastructure to do so.
  In the early game it's a gentle "sell today"; at scale it's a logistics problem you solve with
  facilities and staff.

---

## 4. Refine / Manufacture (value-add chains)

**Facilities** (buildings/equipment you buy and run) convert raw catch into **higher-value, more
stable, more shippable** goods. Recipes are data (`ProductionRecipe`, §8.1). A facility runs recipes
over time, consuming inputs (+ optional consumables like salt, brine, cans, oak chips) and producing
outputs. Run by **hand first**, then by **staff** (§5) — the P4 spine.

### 4.1 Facilities (economic role)

| Facility | Runs | Why it matters |
|---|---|---|
| **Salt House / Flake yard** | Salting & drying (salt cod, dried fish) | Cheapest shelf-stable staple; turns volatile groundfish into durable export. |
| **Smokehouse** | Smoking (smoked herring/eel/salmon-class) | Premium, shelf-stable; great margins on cheap oily fish. |
| **Cannery** | Canning (canned fish, packaged shellfish) | Bulk throughput; the herring/mackerel glut-sink; export-grade. |
| **Reduction Plant** | Fishmeal & fish oil | Consumes *bycatch, gluts, and spoiled stock* — the salvage floor and a steady commodity. |
| **Lobster/Shellfish Pound + Pack House** | Live holding, cooking, packaging shellfish | Holds shellfish alive (premium) and produces cooked/packaged premium goods. |

Facilities have **throughput** (recipes in parallel / batch size) and **capacity** upgrades
(ownership/upgrade mechanics in [`progression-and-housing.md`](progression-and-housing.md); economic
function here). Bigger plants amortize fixed cost across volume — the classic scale-up incentive (P2).

### 4.2 Concrete production chains

`inputs → facility/process → time → outputs → value multiplier` (value vs. selling the raw inputs at a
neutral market; the real win is also **lower elasticity + no spoilage**, per §1.5):

| Chain | Inputs (+ consumables) | Facility / process | Time | Output | ≈ Value mult vs raw | Notes |
|---|---|---|---|---|---|---|
| **Salt Cod** | 10 kg Atlantic Cod + 2 Salt | Salt House (salt & dry) | ~2 days (drying) | 1 box Salt Cod | **×1.8** + shelf-stable, `e`=0.25 | The historical staple; ages well, ships well. |
| **Smoked Herring** | 8 kg Herring + 1 Oak chips | Smokehouse | ~8 h | 1 rack Smoked Herring | **×2.6** | Turns near-worthless glut herring into a premium good. |
| **Smoked Eel** | 5 kg American Eel + 1 Oak chips | Smokehouse | ~8 h | 1 rack Smoked Eel | **×2.2** | Niche delicacy; restaurant/export demand. |
| **Canned Fish** | 12 kg Mackerel *or* Herring + 6 Cans | Cannery | ~6 h | 6 tins Canned Fish | **×2.0** | High throughput; the main glut-sink for pelagic runs. |
| **Fish Oil** | 20 kg oily pelagic *or* spoiled stock | Reduction Plant | ~4 h | 4 L Fish Oil | **×1.5** (from raw) / **×3+** (from spoiled) | Apothecary/export buyer; rescues spoilage. |
| **Fishmeal** | 25 kg mixed/bycatch/spoiled | Reduction Plant | ~3 h | 1 sack Fishmeal | **×1.4** (from waste) | Lowest grade, always-saleable commodity; the floor. |
| **Cooked Lobster Pack** | 6 Lobster (alive) + 1 Brine | Pack House | ~3 h | 1 Lobster Pack | **×1.7** + ships frozen | Converts perishable live premium into durable export premium. |
| **Packaged Scallops** | 30 Scallop + 1 Pack mat. | Pack House | ~2 h | 1 Scallop Pack | **×1.9** | Premium export; low elasticity, high value density. |

> Value mults are tuning seeds, not locked. The *design contract* is: **processing always (a) raises
> value, (b) lowers elasticity, and (c) removes/greatly reduces spoilage** — at the cost of facility
> capital, consumables, and time/labor. That trade is what makes building the business correct rather
> than optional.

### 4.3 Why process? (decision the player learns)

Sell raw **now** (fast cash, volatile, perishable) **vs.** process (capital + time + labor → stable,
durable, higher value, exportable). Early game: mostly sell raw. Mid game: process gluts and surplus.
Late game: *most* raw is fed straight into facilities and the freight pipeline by **staff**, and the
player manages flow, not fish. This progression *is* P4.

---

## 5. Staff & automation (the heart of P4)

The arc: **do it by hand → hire someone to do it → hire a manager so you stop micromanaging.** Crew are
hired, paid, kept (reasonably) happy, and given **policies + AI routines** that run jobs autonomously.

> **Boundary with social NPCs (read this):** this section governs **employees as economic
> automation** — their *work* AI (run a fishing route, process a batch, haul a load, mind the shop).
> Their *social* lives, schedules-as-townsfolk, relationships, dialogue, and the handcrafted core cast
> belong to [`npcs-and-routines.md`](npcs-and-routines.md). A hired skipper may *also* be a named
> townsperson with a social routine there; **here we only model their job.** The two systems share a
> character identity but own different behavior: *work routines here, life routines there.*

### 5.1 Roles

| Role | Automates (was a player job) | Key skills | Needs |
|---|---|---|---|
| **Deckhand** | Crews *your* boat: speeds hauling, tending gear, icing, raises catch quality | Hauling, Handling | A boat with a crew slot (you skipper) |
| **Skipper** | Runs a **second/third boat** on an assigned route **without you** | Navigation, Seamanship, Fishing | An owned boat + crew; route policy |
| **Processor** | Runs a facility's recipes (salt/smoke/can/reduce/pack) | Processing | A facility + input supply |
| **Hauler** | Moves product between boat ↔ storage ↔ facility ↔ market ↔ freight | Logistics | Storage/facility nodes to route between |
| **Seller / Shopkeeper** | Sells at wharf/auction/shop per a pricing policy; minds an owned shop | Trade, Haggling | A sell point + price policy |
| **Manager** | Oversees a **site** (a wharf, a plant, a fleet) — auto-assigns the above, smooths schedules, raises a single policy to the site level, and **reduces the player's decision load** | Management (multiplier on subordinates) | A site with staff; the capstone hire |

### 5.2 Hiring, wages, morale, skill

- **Hiring:** recruit at Greywick (a hiring board / the tavern). Candidates have **skill levels**, a
  **wage expectation**, and **traits** (e.g., *Sea-legs* = weather-tolerant, *Thrifty*, *Green* =
  cheap but low skill, *Sticky-fingers* = small shrink risk). You commit to a wage + schedule.
- **Wages** are a recurring cost on the daily settle (§1.3). Underpaying relative to skill/expectation
  erodes morale; the business must *clear its payroll* — the core pressure that keeps automation
  *earned*, not free.
- **Morale** ∈ [0,1] from pay fairness, schedule sanity (rest days, not all-night runs in storms),
  successful trips, and facility quality. Low morale → slower work, lower quality, quit risk. High
  morale → small skill/quality/speed bonus. (Morale is a *work* metric; deeper relationships are
  [`npcs-and-routines.md`](npcs-and-routines.md).)
- **Skill growth:** staff improve with experience on the job (a deckhand becomes skipper-ready), giving
  a "grow your own crew" loop and making good early hires compounding assets (P2).

### 5.3 AI work routines (the automation engine)

Each employed role runs a **behavior routine** — a simple, data-driven state machine the player
configures via **policies**, not code. Routines respect the **same world rules the player does**
(tide, weather, draught, spoilage), so automation is bounded by P1 and never god-mode.

- **Skipper fishing route:** `Depart → transit (obeys tide/weather/draught; may wait out a gale) →
  fish assigned ground with assigned gear/bait per policy → return when hold full or shift ends → land
  to storage/buyer per policy → repeat next shift.` A green skipper in bad weather may abort or (P5
  risk) take losses — better skippers/boats handle worse seas. Output feeds the same catch resolver and
  market as the player's own fishing.
- **Processor:** `Pull inputs from storage → run assigned recipe(s) to fill throughput → push outputs
  to storage → idle if starved of inputs (flag to manager).`
- **Hauler:** `Watch source nodes → when a threshold builds, move product along its route → respect
  storage caps → prioritize spoilage-risk raw first.`
- **Seller:** `Watch sell-able stock → sell into the channel(s) per policy (e.g., "auction lots > 500
  kg; spot-sell the rest; never sell below floorMult×1.3; fulfill contracts first").`
- **Manager:** `Auto-assign idle staff, rebalance routes/recipes to demand & contracts, raise alerts
  only when a policy can't be met (no inputs, payroll short, boat down, contract at risk).` The manager
  is what lets the player **zoom out**: set site-level intent, get exceptions, not chores.

### 5.4 Policies (how the player commands without micromanaging)

Policies are the player's *standing orders*, set per boat/facility/site and inherited/overridden by
managers. Examples:

- **Fishing policy:** *what* to target (species/category), *where* (region/ground), *gear/bait*, *when*
  (shifts, weather limits, tide windows), *landing rule* (to which storage/buyer).
- **Processing policy:** which recipes, priority order, min input reserve, what to do with gluts/spoiled
  (auto-route to reduction).
- **Selling policy:** channel preferences, price floors, contract-first, max lot size per slice (to
  avoid self-glutting, §1.2).
- **Logistics policy:** routes, thresholds, spoilage priority, freight staging (§6).
- **Site policy (manager):** a single high-level intent ("maximize stable export income," "fulfill all
  contracts then auction surplus," "stockpile for the off-season") that the manager translates into the
  above. **As the empire grows, the player operates at this layer** — the realization of "shift from
  laborer to owner" (P4).

### 5.5 The hand-to-automated transition (P4, explicit)

1. **Hand:** player catches, hauls, processes, sells personally. Every job has weight.
2. **First hire (deckhand):** still aboard, but the grind eases; you feel the *value* of help.
3. **Delegate a job (skipper / processor / seller):** a whole task now runs without you — the first
   taste of *owning* rather than *doing*.
4. **Delegate logistics (hauler):** product flows between sites on its own.
5. **Install a manager:** stop assigning tasks; set intent, handle exceptions. Micro-management drops
   sharply by design.
6. **Multi-site empire:** managers per site; the player tunes policy, expands capacity, chases the next
   region/boat/market. The dynasty runs — and you earned every rung.

> **Tuning guardrail:** automated output should be **good but never strictly better** than skilled hand
> play for the same assets — staff take wages and a small efficiency/quality tax vs. a master player, so
> the choice to delegate is about *scale and your time*, not a pure power upgrade. This preserves "earn
> it" (P4) and keeps hand-play meaningful even late.

---

## 6. Shipping & logistics (the dynasty money)

Local demand is finite; you will glut Greywick. **Shipping** moves durable goods (and some iced/live
premium) to **distant/mainland markets** with higher prices and different demand — the outlet that
makes fleet-scale income real (P2). This ties directly to **The Shipping Lanes** commerce layer and the
freighter/tanker tier in [`boats-and-navigation.md`](boats-and-navigation.md).

### 6.1 How it works

- **Freight runs:** load a **Coastal Packet/Freighter (Tier 6)** or **Coastal Tanker/Cargo Ship
  (Tier 7)** with bulk product, dispatch along a **route** through The Shipping Lanes to a destination
  market; goods sell there (or fulfill a mainland contract) after a **transit time**; the ship returns
  (optionally back-hauling goods/supplies — salt, cans, fuel — at a discount).
- **Routes** have: **distance/transit time**, a **destination market** (own base prices/demand), a
  **risk profile** (weather exposure, the Smother, rips), and a **capacity** (the ship's hold).
- **Freight contracts:** mainland buyers post bulk standing orders (e.g., "20 boxes Salt Cod/run").
  These are the high-value, automatable backbone of dynasty income — stable, large, and exactly what a
  staffed pipeline (§5) is built to feed.

### 6.2 Risk / reward of bulk shipping (P5 at scale)

A freight run concentrates a lot of value in one hull crossing open water:

- **Reward:** mainland prices clear gluts and pay a premium; one full tanker run can dwarf a week of
  local selling (P2 payoff).
- **Risk:** weather and hazards can **delay** (spoilage on any perishable cargo; missed contract
  window) or, rarely and only in bad conditions with a poor crew/route, cause **partial loss** of
  cargo. Mitigations: ship **shelf-stable processed goods** (no spoilage), pick weather windows, hire a
  skilled freight skipper, upgrade the hull's seaworthiness, and (optionally) insure a run for a fee.
- This makes the end-game a **logistics optimization with stakes**, not a payout button — coziness with
  teeth, at fleet scale.

### 6.3 Where it sits in the loop

Catch (player + staff fleet) → ice/store → **process** into durable export goods → stage in dry
warehouse → **freight** to mainland/contracts → revenue funds more boats, facilities, staff, routes.
By the top of the ladder the player is **directing this whole machine** rather than working any single
station — P2 and P4 converging.

---

## 7. Properties & facilities the business uses (economic function)

> **Ownership/upgrade mechanics, costs, and the property map are
> [`progression-and-housing.md`](progression-and-housing.md).** Here: what each does *economically*.

| Property / facility | Economic function |
|---|---|
| **Home wharf (Coddle Cove)** | Free starter sell point (Fish Buyer visits) + first ice/well + first berth. Where hand-play begins. |
| **Greywick berths (2nd / 3rd)** | Each berth lets you **operate another boat** (own + staffed). Berths are the gate on *fleet size* — the throttle on the whole catch pipeline. |
| **Cold storage / wet well** | Time-the-market + spoilage buffer + live-shellfish premium (§2/§3). |
| **Dry warehouse** | Bulk, spoil-proof staging for processed goods and freight (§2/§6). |
| **Processing plants** (salt house, smokehouse, cannery, reduction, pack house) | The value-add engine (§4). Throughput/capacity = how much volatile raw you can convert to stable value. |
| **Shop / market stall** | A *sell channel you own* (vs. taking the buyer's spot price): a staffed shopkeeper sells your goods (and maybe others') at retail margins to townsfolk/tourists. |
| **Auction house access** | Not owned, but a key channel (§1.4). |
| **Freight terminal / quay** (Shipping Lanes) | Load/dispatch point for freight runs; staging + contract fulfillment hub (§6). |

The chain of capability is deliberately **legible** (P2): *more berths → more boats → more catch → which
overwhelms selling/spoilage → so you need storage + processing → which produces export goods → which
need freight → which fund more berths.* Each facility unlocks the *need* for the next.

---

## 8. The economic progression curve

Rough money milestones from first dory catch to freight empire. **Coin figures are tuning seeds**
(balance via the day-in-the-life sim in [`fish-and-content.md`](fish-and-content.md) §6.2), but the
*shape* — and how the loop stays engaging at each scale — is the design target.

| Stage | Scale | ~Net worth band | Core loop | What keeps it engaging |
|---|---|---|---|---|
| **0. The Dory** | Hand-fishing inshore, sell raw at the wharf | 0 → ~1k | Read tide/weather → catch by hand → sell same day before it spoils | The first full hold sold = a real triumph (P2). Survival-of-the-day tension (P5). |
| **1. First boat & gear** | Punt/Skiff + better gear; auction unlocked | ~1k → ~8k | Catch more → choose buyer/auction → first ice to time a sale | Discovering the market moves; learning not to glut your own catch. |
| **2. Workboat & first hire** | Cape Islander/Lobster Boat; **first deckhand**; first storage | ~8k → ~40k | Bigger trips; ice + store to time sales; first **standing contract** | The grind eases (P4 step 1–2); contracts give a stable base to plan around. |
| **3. First processing** | A salt house/smokehouse; **processor hire** | ~40k → ~150k | Convert gluts/surplus to durable value; run a facility | Value-add unlocks; gluts become opportunity, not loss. Owning > doing begins (P4). |
| **4. Offshore & second boat** | Dragger to the Banks; **skipper** runs a 2nd boat; 2nd berth | ~150k → ~600k | A staffed boat fishes without you; you process & sell its catch | First true *fleet* moment; you direct two operations (P2/P4). |
| **5. Multi-facility ops** | Cannery/reduction/pack house; **hauler + seller + manager** | ~600k → ~3M | A site runs itself on policies; you set intent & expand | Micro-management drops; the business *hums*. Optimization becomes the game. |
| **6. Freight & dynasty** | Stern trawler → freighter → **Coastal Tanker**; freight routes & mainland contracts | ~3M → 20M+ | Process at scale → freight to mainland → reinvest in fleet/routes | Fleet command; bulk shipping risk/reward; the empire as the toy (P2 endgame). |

**Keeping each scale engaging (the design promise):**
- **New region/boat = new fish = new economy** — each rung opens fresh catch (per
  [`fish-and-content.md`](fish-and-content.md)) *and* fresh market dynamics, so growth is content, not
  just bigger numbers.
- **The bottleneck moves** — early it's *can I catch enough?*; then *can I sell without gluting?*; then
  *can I process & store fast enough?*; then *can I ship & staff it all?* The player always has a fresh,
  legible problem to solve at their current scale.
- **Hand-play never fully vanishes** — chasing a legendary, working a new ground yourself, or riding a
  freight run keeps the player's hands on the world even when most income is automated (P4 guardrail).

---

## 9. Implementation notes

### 9.1 Data-driven commodities & recipes

- **`Commodity` (ScriptableObject):** `id`, `displayName`, `class`/category, `baseValue P0`,
  `elasticity e`, `floorMult`, `perishability` (raw) or shelf-stable flag (processed), `valuedBy`
  (PerKg/PerUnit), `defaultDemand D0`, art/flavor. Raw-catch commodities are derived from / linked to
  `FishSpecies` ids ([`fish-and-content.md`](fish-and-content.md)) so the two systems can't drift.
- **`ProductionRecipe` (ScriptableObject):** `inputs[] (commodity, qty)`, `consumables[]`,
  `facilityType`, `processTime`, `outputs[] (commodity, qty)`, `requiredProcessorSkill`,
  `qualityModel`. Adding a recipe or commodity = **data only**, no code (ADR-0003).
- **`Buyer`/`Market`, `Contract`, `FreightRoute`, `StaffRole`, `Trait`** are likewise data assets +
  small code-side enums. New buyers, contracts, and routes are authorable by content agents in
  parallel (independent assets → no merge conflicts), mirroring the fish-authoring workflow.

### 9.1a St Peters opening — licences, gear & the damaged-dory repair (greybox, minimal)

The St Peters opening (`backlog` M2-31..33, pulled forward by the owner) needs a *minimal but real*
progression layer. The economy side, all **data-driven** and behind **Core seams** (no concrete cross-
module refs):

- **`LicenseDef` (ScriptableObject, `Data/Licenses/`):** `id` (e.g. `license.cod`), `displayName`,
  `Price` (₲ fee), `PermittedSpeciesIds[]` (what it unlocks). The first one is `license.cod`. This is
  the **money-only** seed of the licence currency ([`progression-and-housing.md`](progression-and-housing.md)
  §2.2) — the proficiency/reputation eligibility tower is **deliberately deferred** (it adds fields here
  + a vendor check later, without changing the seam or save shape).
- **`ILicenseService` (Core seam) + `LicenseService` (Economy, self-installing) + save:** the licence
  *wallet* — `IsLicensed(id)`/`Grant(id)`, held licences persisted in `SaveData.OwnedLicenses` (schema
  v2). Fishing reads `GameServices.Licenses` to gate a catch **without referencing Economy** — the
  `IWallet`/`IHold` pattern.
- **The catch gate (`CatchLicensePolicy`, pure helper):** "may this species be landed?" — a species is
  gated iff a licence lists it; cod requires `license.cod`, everything ungated stays catchable. The
  mapping is *derived from the licence data* (one source of truth; cod→`license.cod` isn't duplicated)
  so gameplay-systems calls `MayLand(speciesId, …)` at land-time without an Economy ref. We do **not**
  add a `RequiredLicenseId` field to the Fishing `FishSpeciesDef` (that's their schema) — the gate keys
  by id.
- **The rod / shovel / clam-bucket (`GearOffer`, `Data/Gear/`):** the *economic* side of a purchasable
  item — `id`, `displayName`, `Price`. Bought at a `GearShop`; ownership recorded in
  `SaveData.OwnedGear`. The gear's **capability** (which `Gear` flag the rod maps to, the shovel's dig
  method, the bucket's 20-unit hold) is **gameplay-systems'** — they map an owned-gear id to it. The
  rod fishes cod only once the cod licence is *also* held (owning the rod ≠ being licensed).
- **The soft-shell clam (`FishSpeciesDef`, `fish.soft_shell_clam`):** the opening's by-hand income —
  Shellfish, hand-dug, sellable at Greywick through the existing per-category market. Authored on the
  existing `Handline` (by-hand) gear tag until gameplay-systems reconciles a dedicated `Shovel`/
  `ClamFork` `GearTag` ([`fish-and-content.md`](fish-and-content.md) §3.5a — a new enum tag is review-
  gated). Gated to `region.coddle_cove` as a placeholder until world-content authors the St Peters
  `RegionDef` (then re-gate to `region.st_peters`).
- **Damaged-dory buy + repair (`ShipwrightOffer.StartsDamaged`/`RepairCost` + `Shipwright.TryRepair`):**
  a boat can be sold **damaged** — bought/owned (still raises `BoatPurchased` so the fleet grants it)
  but **unusable** until the player pays the repair fee, which marks it repaired (`SaveData.RepairedBoats`,
  via `RepairLedger`) and raises the Core `BoatRepaired` event. A non-damaged buy is repaired-on-grant.
  Greybox-minimal: **instant repair on payment**. The *usable* gate (boarding) is gameplay-systems' —
  they read `RepairLedger.IsRepaired` off the save. The St Peters dory is a **plain bought + repaired**
  boat (no inheritance).
- **New Core events (additive, in `Core/Events/LicenseSignals.cs`, NOT `GameSignals.cs`):**
  `LicensePurchased`, `BoatRepaired`, `GearPurchased` — for ui-ux toasts / world-content beats /
  gameplay-systems capability enable, all via EventBus so no module references Economy.

> **Out of scope this wave (flagged):** the on-foot dig interaction, the rod→`Gear` mapping, the bucket
> as a 20-unit hold, and the vendor placement in the world are **gameplay-systems / world-content** —
> Economy provides the components + data; the **builders are re-run by those lanes next wave** to surface
> the vendors/clam spots. The full M2 proficiency/reputation licence tower stays deferred.

### 9.2 The market simulation tick

- A single **`MarketSim` service** advances all markets on the hourly/daily cadence (§1.3): updates
  `S`/`D`, applies NPC-fleet landings, consumption, decay, demand walk, and event shocks; recomputes
  `effPrice`. **Deterministic** given (save-seed, tick index) so it's reproducible and testable.
- **Performance (mobile):** the sim is a handful of float updates per (market × commodity) per tick —
  trivial. Catch up missed ticks in a fast loop on resume (e.g., after app backgrounded), capped so a
  long absence settles sensibly rather than running thousands of full ticks.
- **Staff/automation** run on a coarser **shift cadence** (not per-frame): routines resolve a shift's
  fishing/processing/hauling/selling as a batched outcome, so a 50-boat empire is cheap. Visible,
  on-screen staff (in loaded scenes) can be *represented* by lightweight agents for flavor (P3) while
  the *economic* result comes from the batched sim.

### 9.3 Save / load of market & business state

- Persist per market/commodity: `S`, `D`, `demandMood`, active event shocks, and last-tick timestamp
  (to settle elapsed time on load). Persist contracts (terms, progress), staff (role, skill, morale,
  wage, assignment, traits), facility/recipe queues and stored inventory (with freshness timestamps),
  freight runs in transit, and all policies.
- Use **stable `id`s** everywhere (commodity/recipe/buyer/route/staff) so saves survive content
  additions (new species/recipes appended without breaking old saves) — same discipline as fish `id`s.
- Inventory freshness stored as a *timestamp*, not a per-tick countdown, so spoilage resolves correctly
  across save/load and time-skips.

### 9.4 Mobile-friendly management UIs

> Full UX/controls and one-handed layouts are [`ux-and-mobile-controls.md`](ux-and-mobile-controls.md); this doc names
> the *screens the economy needs* and their key decisions.

- **Sell screen:** quantity slider showing **marginal price** and total as you drag (the self-glut read,
  §1.2); channel picker (buyer/auction/contract); one-tap "sell all but reserve."
- **Market board:** prices, trends (up/down arrows + sparkline), per-commodity demand, contract offers.
  Glanceable; the "read the market" tool, sibling to the tide table.
- **Storage/inventory:** capacity bars, freshness indicators (color/countdown), quick-route to a
  facility or freight.
- **Facility panel:** recipe queue, throughput, input reserves, assign processor, gluts-auto-route
  toggle.
- **Staff/roster:** roles, skills, morale, wages, assignment; hire flow; set-policy entry points.
- **Manager/site dashboard:** one screen of intent + an **exceptions/alerts** feed (the endgame's main
  UI — operate by exception, not by chore). Designed for *thumb-glance management* on a phone.
- **Freight screen:** route map (The Shipping Lanes), cargo manifest, transit ETA, risk indicator,
  contract fulfillment.

All management UIs follow the **policy-over-clicks** principle so a growing empire never means more
tapping — it means setting better standing orders (the UX expression of P4).

---

## 10. Open questions

1. **Money sinks at the top:** what keeps late-game coin meaningful besides buying the next boat/plant?
   (Candidates: facility upkeep/maintenance, fuel costs, insurance, prestige property in
   [`progression-and-housing.md`](progression-and-housing.md), philanthropic/town-improvement sinks
   that boost the living coast — needs an anti-inflation pass.)
2. **NPC-fleet model fidelity:** is the aggregate supply curve (§1.3) enough for P3, or do we want a
   handful of *named* rival skippers whose landings you can see and compete with (richer, but more sim
   and ties into [`npcs-and-routines.md`](npcs-and-routines.md))?
3. **Market shocks authoring:** are demand events scripted (festivals, mainland orders, a rival harbour
   storm-closed) data assets on a calendar, fully procedural, or both? Where does the calendar live
   relative to [`time-tides-weather.md`](time-tides-weather.md) and quests?
4. **Staff failure ceiling (P5):** how punishing can an automated skipper's bad-weather loss be before
   it stops being cozy? Cap losses? Insurance mandatory above a tier? Needs a feel pass with P5.
5. **Contract penalties vs. coziness:** how hard do missed-contract penalties bite? Reputation + fee is
   the baseline, but never a "game over." Tune so contracts feel like real commitments, not traps.
6. **Player-set prices on owned shop:** does running your own shop let you *set* retail prices (with
   demand response), adding a pricing minigame, or is it auto-priced? (Leaning auto with a markup
   policy to avoid over-fiddly.)
7. **Processing quality tiers:** do processed goods inherit a quality grade from input quality + skill
   (premium salt cod vs. standard), adding depth, or is processed output flat-quality for simplicity?
8. **Inter-market arbitrage depth:** how freely can the player exploit price gaps between Greywick and
   the mainland before transit time/risk/freight cost should rein it in? Tune so shipping is rewarding
   but not a free money printer (ties to §6 risk tuning).
9. **Where does fuel/operating cost sit?** Per-trip fuel and crew wages as ongoing costs add realism and
   a reason to optimize routes, but risk fiddliness on mobile — include from which stage, and how
   abstracted?
