# Hidden Harbours — Progression & Housing

> Design module. Subordinate to `../vision-and-pillars.md` (CANON). Uses locked names, the
> boat ladder, the region order, and the PPU=32 / 1 tile = 1 m scale standard. Serves primarily
> **P2 (From Dory to Dynasty)** and **P4 (Earn It, Then Automate It)**, with **P5 (Cozy, but
> with Teeth)** flavouring the risk/reward of every rung.
>
> Sibling docs referenced: `economy-and-business.md` (what commercial property *does*),
> `boats-and-navigation.md` (boat stats), `world-and-regions.md` (region detail),
> `time-tides-weather.md` (time/season costs), `ux-and-mobile-controls.md` (session length &
> screens), `npcs-and-routines.md` (who issues licenses/reputation).

---

## 1. Design goals

1. **A ladder you can see.** Every advance should change the *world on screen* — a bigger hull in
   your berth, a new horizon you can sail to, a warehouse with your name on it (P2). No purely
   numerical "power" that the player can't point at.
2. **Earned, then delegated.** You hand-fish before you hire a deckhand; you run freight by hand
   before you schedule a route. Each automation is a *graduation*, not a purchase off a menu (P4).
3. **Legible, not grindy.** The player should always know the *next* meaningful goal and roughly
   what it costs. We gate with a small number of clear currencies (money, proficiency, licenses,
   reputation), never with opaque XP soup.
4. **Cozy home base.** Housing is the warm counter-weight to the cold sea — a place that is yours,
   that you furnish and improve, and that gives the daily loop a "home" to return to (Stardew /
   Big Ambitions DNA).
5. **Respects mobile time.** No rung requires a marathon session. Progress banks in minutes; the
   long arc is made of many short, satisfying runs (see `ux-and-mobile-controls.md` §Session).

---

## 2. The progression currencies

Progression is gated by **five legible currencies**. Keeping the count small is deliberate — the
player should be able to recite their own state.

| Currency | What it is | How earned | Mostly gates |
|---|---|---|---|
| **Money (¢ / $)** | Liquid cash | Selling catch & cargo, contracts, rents | Boats, property, gear, licenses (the *fee*) |
| **Proficiencies** | Four hands-on skills (below) | *Doing the activity* | Technique unlocks, efficiency, license *eligibility* |
| **Licenses & Permits** | Legal right to do a thing | Pay fee + meet proficiency/reputation bar | Region access, gear classes, business activities |
| **Reputation** | Standing with factions/town | Quests, fair dealing, reliability | Story unlocks, contracts, better prices, some licenses |
| **Time / Season** | In-game days & the calendar | Spent, not earned | Build/refit timers, seasonal grounds, slow approvals |

> **Currency, not XP.** We surface money, four named skill bars, a license wallet, and a
> reputation meter per faction. We do **not** show a single global level. "Level" is an emergent
> read of all four, not a number.

### 2.1 The four proficiencies

Skills rise by *performing the activity*, never by spending points. Each runs **0–100** with named
bands. Bands are the legible bit; the underlying number can be finer.

| Proficiency | Rises by | Bands (0–100) | Representative unlocks |
|---|---|---|---|
| **Seamanship** | Sailing distance, handling wind/current, surviving rough seas, safe dockings | Greenhorn → Deckhand → Mate → Skipper → Master | Tighter handling assists, higher sea-state tolerance, reef/rip transit confidence, eligibility for bigger-hull licenses |
| **Fishing** | Catches landed, species variety, clean (non-snapped) lands | Handliner → Hauler → Highliner → Master Fisher | New techniques (jigging→longline→trap→net→dragging), faster bites, less line-snap, rare-fish odds |
| **Navigation** | Charting routes, dead-reckoning in fog, reading tide tables, instrument use | Coaster → Pilot → Navigator → Master Pilot | Fog/instrument transit (The Smother), Fundy Rips timing aids, offshore route planning, freight route automation |
| **Business / Management** | Trades made, staff managed, facilities run, contracts fulfilled | Hand → Trader → Manager → Magnate | Larger staff caps, more facilities, logistics automation, better contract tiers, lower license fees |

**Tuning intent (P4):** the *first* hour of any activity moves its bar fast (the player feels
themselves getting good); mastery tails off so late gains are about *unlocking capabilities*, not
grind. A band-up should fire roughly when a new capability is ready, so "I leveled" and "I can now
do X" coincide.

> See `ux-and-mobile-controls.md` for how the four bars surface (a thin skills card, not a
> cluttered RPG sheet) and how a band-up is celebrated without interrupting a fishing run.

### 2.2 Licenses & permits

Licenses are the *explicit gate* — the thing that makes a region or capability "click" open. They
are issued by named authorities (the Harbourmaster at **Port Greywick**, the regional
Lighthouse-Keepers, the **Banks** fisheries board), so unlocking is a *social/world* act, not a
silent flag (P3). Each license has: **fee (money)**, **eligibility (proficiency + sometimes
reputation)**, and occasionally a **processing time (days)**.

Representative licenses (full list lives with the data; see §6):

| License | Eligibility (typical) | Unlocks |
|---|---|---|
| **Inshore Handline** | none (granted in tutorial) | Hand-fishing in Coddle Cove |
| **Tide-Reader's Note** | Navigation: Coaster + a Sunkers run | Safer Sunkers reef work; tide-table tool upgrade |
| **Port Greywick Trade Seal** | Story (early) | Use the auction house; rent storage |
| **Flats Permit** | Navigation: Pilot + tide table | Walk the seabed in **The Drownded Lands** at low water |
| **Rips Transit Endorsement** | Seamanship: Mate + Navigation: Pilot | Legal/safe transit of **Fundy Rips** |
| **Offshore Groundfish License** | Seamanship: Mate + dragger-class hull | Fish **The Banks** |
| **Ironbound Weather Endorsement** | Seamanship: Skipper + weather-capable hull | Work **Ironbound** in dangerous weather |
| **Instrument Pilotage Cert** | Navigation: Navigator | Navigate **The Smother** by instrument |
| **Freight Carrier's License** | Business: Manager + freighter-tier hull | Take cargo contracts on **The Shipping Lanes** |
| **Processing / Warehouse Permits** | Business: Trader+ | Own & operate plants/warehouses (see `economy-and-business.md`) |

### 2.3 Reputation

Reputation is **per-faction**, not a single global number, so the working coast feels political and
alive (P3). Starting factions:

- **Coddle Cove neighbours** (your uncle's old friends) — warm start; small early favours.
- **Port Greywick townsfolk / Harbourmaster** — gates the Trade Seal, berth quality, some licenses.
- **The Banks fishing fleet** — respect earned offshore; better grounds intel, crew recruits.
- **Mainland shippers / brokers** — the freight tier; reliability raises contract value.

Reputation rises with **reliability** (delivering contracts on time, not abandoning crew, fair
trades) and quests; it can dip (missed contracts, dumping spoiled fish on the market, leaving a
deckhand stranded). It is *never* a hard wall on its own — it sweetens prices, unlocks contracts,
and is a *secondary* requirement on a few prestige licenses.

---

## 3. The progression spine (the intended arc)

The spine is the canonical "happy path" through the boat ladder and region order. It is a **tree
near the top** (lobster specialist vs offshore trawler) exactly as the boat ladder specifies, then
converges into the commerce tier. Region order and boat tiers below are **locked canon** — this
section only describes *how the currencies move you between them*.

### 3.1 Stage map

| Stage | Identity | Boat (tier) | Region(s) opened | Primary gate to *leave* this stage |
|---|---|---|---|---|
| **0. Handline** | "By hand, in the dory" | The Dory (T0) | Coddle Cove | Earn first stake + Fishing→Hauler |
| **1. First boat & first reef** | Owning a hull, reading tide | Punt/Skiff (T1) | + The Sunkers | Money for a Cape Islander + Seamanship→Deckhand + Tide-Reader's Note |
| **2. Town & market** | Trading, not just catching | Cape Islander (T2) *or* Lobster Boat (T3) | + Port Greywick, + Drownded Lands (with Flats Permit) | Business→Trader + Offshore License eligibility |
| **3. Offshore** | Working the open grounds | Side Dragger (T4) → Stern Trawler (T5) | + Fundy Rips, + The Banks, + Ironbound | Business→Manager + Freight Carrier's License |
| **4. Freight & fleet** | Owner, not laborer | Coastal Packet (T6) → Tanker (T7) | + The Shipping Lanes, (+ The Smother, optional) | End-game: a self-running dynasty |

### 3.2 The arc in prose

**Coddle Cove, the dory, your two hands.** You start with the uncle's dory (T0) and his cottage.
The Inshore Handline license is already in your pocket. You jig and handline in sheltered water;
every cod is hauled up by *you*. The first hold you sell is the canonical triumph beat from the
vision. Money trickles in; Fishing climbs fast from Handliner toward Hauler. This is the whole
game in one cove — short, complete, repeatable (P4: *do it by hand first*).

**The first boat and the first reef.** With a small stake you buy the **Punt/Skiff (T1)** from the
Greywick shipwright. A little hold, a little reach — enough to nose into **The Sunkers**. Now tide
*bites*: the Sunkers ground a careless hull at low water (P1/P5). Earning the **Tide-Reader's
Note** (Navigation: Coaster + one Sunkers run) upgrades your tide tool and makes the reef workable.
Seamanship begins to matter as wind shoves your light hull around.

**The town and the market.** The story opens **Port Greywick** (the Trade Seal). Suddenly catching
is only half the game: you can *store to time sales*, watch supply/demand, and start trading
(Business proficiency begins). Here the ladder forks: commit to the **Lobster Boat (T3)** trap
specialist branch, or the all-rounder **Cape Islander (T2)** — both are real workboats with real
range. With a **Flats Permit** you can walk the exposed seabed of **The Drownded Lands** at low
tide for clams and wrecks (a pure-P1 set-piece). This is the "mid-game town life" plateau: cozy,
profitable, many short loops.

**Offshore.** Ambition pushes you to deepwater. The **Side Dragger (T4)** is the first hull that
can *safely* work **The Banks** (gated by the Offshore Groundfish License + Seamanship: Mate). To
even reach the right grounds you transit **Fundy Rips** — and that demands timing the tidal current
(Rips Transit Endorsement; pure P1 navigation skill). The **Stern Trawler (T5)** is weather-capable
and unlocks storm-lashed **Ironbound** (end-game fishing, dangerous weather, legendary fish). This
is where Seamanship and Navigation peak and the sea is at its most *toothed* (P5).

**Freight and the dynasty.** The capstone shifts you from laborer to *owner* (P4's thesis). The
**Coastal Packet/Freighter (T6)** begins the cargo tier: you take freight contracts on **The
Shipping Lanes**, run routes to the mainland, and — crucially — start *delegating*. Hired skippers
run your old boats; staffed warehouses and plants add value to goods (see
`economy-and-business.md`); logistics get *scheduled* rather than hand-steered. The **Coastal
Tanker (T7)** is the apex: fleet command, a freight empire, your name on berths and buildings
across the Banks. The optional **Smother** run (instrument pilotage) is the prestige sidetrack for
masters. Late-game play is *directing*, with the option to drop back into a dory for a quiet,
hands-on fishing morning whenever you like — because the hand-fishing loop never stops being good.

### 3.3 Pacing & "milestone" money beats

To keep the ladder legible, the **boat purchases** are the headline money milestones, spaced so
each feels like a chapter. Indicative gaps (tuned later against the real economy in
`economy-and-business.md` — treat as *shape*, not final balance):

| Milestone | Feel | Rough relative cost |
|---|---|---|
| First stake (gear, bait) | minutes | trivial |
| Punt/Skiff (T1) | a few good runs | 1× |
| Cape Islander (T2) / Lobster Boat (T3) | a chapter | ~8–12× |
| First commercial property (a shed/berth) | parallel goal | ~6–10× |
| Side Dragger (T4) | a serious campaign | ~40–60× |
| Stern Trawler (T5) | major | ~120–160× |
| Coastal Packet (T6) | empire pivot | ~300–400× |
| Coastal Tanker (T7) | end-game | ~800–1000× |

The curve is roughly geometric so the *fantasy* of scale matches the *price* of scale, but always
with a smaller parallel goal (a home upgrade, a license, a warehouse) available between the big
hulls so the player is never staring at one distant number (anti-grind).

---

## 4. Housing

Housing is the cozy heart of the home loop (Stardew comfort; Big Ambitions property depth). It is
**ownership + customization + light, opt-in buffs** — never a wall in front of the fishing game.

### 4.1 The residence ladder

You always have *a* home. The progression is from inherited shelter to owned property to a small
portfolio of residences.

| # | Residence | Where | Acquired | Notes |
|---|---|---|---|---|
| **R0** | **Uncle's Cottage** | Coddle Cove (on the home wharf) | Inherited (start) | Small, weathered, a few decor slots, a sea-chest for storage. Sentimental anchor. |
| **R1** | **Cottage refit** | Coddle Cove | Buy upgrades | Expand rooms, better bed, more storage, workshop nook |
| **R2** | **Greywick Townhouse** | Port Greywick | Buy | In-town, close to market; more decor & storage; status |
| **R3** | **Outport / coastal homes** | various unlocked regions | Buy | Multiple residences; a base near distant grounds |
| **R4** | **Captain's House / Estate** | premium lot | Buy + Business: Magnate | End-game flex; large decor canvas; trophy room for legendary fish |

> **The uncle's cottage is never taken away.** Even after you own grander homes it remains a
> furnishable, ownable space — the emotional baseline of the whole game.

### 4.2 What you do with a home

Three layers, in order of importance:

1. **Customize (the main fun).** Place and arrange furniture and decor on a tile grid (PPU=32, so
   furniture footprints are honest metres — a table is ~1×2 tiles). Wallpaper/flooring swaps,
   exterior touches (paint the trim, a buoy on the wall, nets on the rail), and a **trophy display**
   for record catches and legendary fish. Decor is bought from Greywick shops, found, crafted, or
   awarded.
2. **Storage (utility).** Homes hold a **sea-chest / lockers** — your at-home stash (separate from
   the boat hold and from commercial warehouses). Bigger/upgraded homes = more home storage. This
   is the *organizational* reason to upgrade, distinct from commercial bulk storage (which is an
   economic tool — see `economy-and-business.md`).
3. **Comfort buffs (light, opt-in, P5-aware).** Sleeping in a *comfortable, decorated* home grants
   a gentle morning buff — e.g. a small stamina/energy bonus or a tiny luck nudge on the day's
   first runs. **Design rule:** buffs are *mild and never mandatory*. A player who ignores decor is
   never gated out of content; decorators get a cozy edge, not a power requirement. This keeps
   housing firmly in the "warmth" column, not the "grind" column.

### 4.3 Comfort & buff model (concrete, tunable)

A home has a derived **Comfort** score (0–100) from: number/quality of furnished decor, bed tier,
cleanliness/upkeep, and a small "personal touch" bonus for variety. Comfort maps to a capped,
gentle buff:

| Comfort band | Morning buff (first ~2 hrs of play after sleeping) |
|---|---|
| 0–24 (bare) | none |
| 25–49 (homely) | +5% stamina recovery |
| 50–74 (cozy) | +10% stamina recovery, small "well-rested" UI glow |
| 75–100 (beloved) | +12% stamina recovery + a tiny one-shot luck nudge on the first catch/sale |

Numbers are placeholders for balance, but the **ceiling stays low on purpose**. Comfort is a
reward for caring about your space, not a meta to optimize.

### 4.4 Commercial property (ownership/upgrade/customization side)

Commercial property is the *owner's* side of the empire (P4). This doc owns the **ownership,
upgrade, and customization mechanics**; their **economic function** (throughput, margins, market
effects, staffing economics) lives in `economy-and-business.md`.

| Property type | What you own/upgrade here | Customization | Econ function → |
|---|---|---|---|
| **Berths / slips** | Buy/lease a berth; upgrade size (to fit bigger hulls), add a crane, a fuel point | Name it, paint it, signage | Faster turnaround, fleet basing (`economy-and-business.md`) |
| **Warehouses** | Buy/build; upgrade capacity tier & climate (cold storage); add loading docks | Shelving layout, signage, your mark | Bulk storage to time sales; logistics hub |
| **Processing plants** | Buy/build; upgrade processing lines & quality; add machines | Floor layout of stations | Refine raw catch → value-add goods |
| **Shops / market stalls** | Buy a Greywick storefront; upgrade frontage & shelf count | Window dressing, sign, stock display | Sell direct; passive retail income |

**Ownership mechanics shared by all commercial property:**

- **Acquire:** buy outright (cash) or lease (rent/day) where offered. Some require a permit
  (Warehouse/Processing permits) and a Business proficiency band.
- **Upgrade:** discrete **tiers** (e.g., Warehouse I/II/III), each a money + **build-time (days)**
  cost (see §5 on time). Upgrades visibly enlarge/improve the building sprite (P2 — power is
  visible).
- **Customize:** lighter than home decor but present — paint, signage, your **harbour mark** (a
  small player crest you design early), and interior station/shelf layout that can give small
  efficiency bonuses (detailed in `economy-and-business.md`).
- **Staff & automate:** assign hired staff to run a property so it operates without you (P4). The
  *staffing/automation rules* and offline behaviour are specified in `economy-and-business.md` and
  `ux-and-mobile-controls.md` (§Offline) respectively.

> **Naming & identity:** letting the player **name and mark** their berths, warehouses, and the
> first freighter is a cheap, high-impact ownership beat — it turns "a building" into "*my*
> building" and reinforces the dynasty fantasy (P2).

---

## 5. Progression × time & seasons

Time is a *currency you spend* (P1's calendar made consequential). Several advances deliberately
**take in-game days** so the world keeps moving while you wait — but never in a way that blocks the
core loop.

- **Boat refits & builds:** purchasing a new hull from the shipwright, or a major refit, takes a
  small number of **in-game days** (e.g., T1 ~1 day, T4 ~3–4 days, T7 ~7+ days). You keep fishing
  in your current boat meanwhile.
- **Commercial builds/upgrades:** warehouses/plants take **days** to build/upgrade; bigger tiers
  take longer. Staff can be hired to start working the moment a build completes.
- **License processing:** a few prestige licenses have a short **approval delay** (paperwork with
  the Harbourmaster), reinforcing that the bureaucracy is part of the living coast (P3).
- **Seasonal grounds & prices:** some fish and some markets are **seasonal** (see
  `fish-and-content.md`, `economy-and-business.md`). Certain region content (e.g., the safest
  Ironbound windows) is weather/season-bound, so the calendar shapes *when* you push the frontier
  (P1/P5).
- **Reputation & quests** can have day-scale beats (deliver by a date), tying social progress to the
  clock.

**Key rule — time costs never stall the fun.** Every "this takes days" cost runs *in the
background* while the player keeps doing the moment-to-moment loop in their current boat/home. You
are never staring at a "come back later" wall; you are choosing what to do *today* while a refit
finishes. This is essential for mobile (see below).

---

## 6. Progression on mobile session lengths

The spine must feel great in **3-minute and 30-minute** sittings (full treatment in
`ux-and-mobile-controls.md` §Session Design). Progression-specific commitments:

- **Bank progress in minutes.** A single fishing run (a few minutes) yields money + proficiency
  ticks — always *some* forward motion. No advance requires an unbroken long session.
- **Always a near goal.** The UI surfaces the **next milestone** (next license, next decor piece,
  next berth upgrade) alongside the headline next-boat goal, so a short session can complete a
  *small* rung even when the big one is far off.
- **Background timers respect absence.** Builds/refits and (optionally) staff-run operations
  advance while away (stance defined in `ux-and-mobile-controls.md` §Offline; economics in
  `economy-and-business.md`), so logging in after a break shows *progress made*, not penalty.
- **Save/resume anywhere.** The player can stop mid-run; state persists (see §7 and the UX doc).
- **One-tap "what's next."** A lightweight progression panel answers "what should I do?" at a
  glance — money toward next boat, proficiency bars, pending licenses, active builds.

---

## 7. Implementation notes

**Data-driven unlock graph (per `../adr/0003-data-driven-content.md`).** Model progression as a
directed acyclic **unlock graph** of ScriptableObject nodes, not hard-coded `if` checks.

- **`UnlockNodeSO`** — id, display name, `Requirements[]`, `Grants[]`, optional `processingDays`.
- **`RequirementSO`** (polymorphic): `MoneyRequirement`, `ProficiencyRequirement(skill, band)`,
  `LicenseRequirement(id)`, `ReputationRequirement(faction, level)`, `StoryFlagRequirement`,
  `BoatTierRequirement`, `DateRequirement`.
- **`GrantSO`**: unlock region, unlock license, enable gear class, enable business capability,
  reveal shop stock, etc.
- **Boats, regions, licenses, properties** each reference their gating node; an `UnlockService`
  evaluates the graph reactively and fires events when a node opens (drives the "new horizon" /
  band-up celebration). Authoring stays in data so designers/agents add rungs without code changes.
- A small **debug/validator** should detect unreachable nodes and circular requirements at build
  time.

**Proficiency model.** Four float skills 0–100 with named band thresholds in data; gains via an
event bus (`OnFishLanded`, `OnDistanceSailed`, `OnTradeCompleted`, etc.) feeding tunable curves
(fast-start, slow-tail). Bands, not raw floats, drive unlock requirements for legibility.

**Housing model.** Residences and commercial buildings are placeable entities with a tile-based
**decor/layout grid** (PPU=32 footprints). A home's furnished items derive a **Comfort** score →
buff (capped, §4.3). Storage is per-container with capacity tiers. Customization (paint, signage,
harbour mark, layout) is data-driven so new decor is pure content.

**Save state.** Persist: money; the four proficiencies; the **set of satisfied unlock nodes**
(authoritative — recompute derived grants on load, don't store every grant); license wallet;
per-faction reputation; owned boats (+ current hull); owned residences & commercial properties with
their **tier, customization data, decor layout, and storage contents**; in-flight **timers**
(builds, refits, license approvals) as `(nodeId, completeOnGameDate)`; current game date/season.
On load, advance any timers whose completion date has passed (supports offline progress; coordinate
with the offline stance in `ux-and-mobile-controls.md`). Use a versioned save schema for
forward-compat.

**Cross-references to honour:** boat stats/branches → `boats-and-navigation.md`; commercial econ →
`economy-and-business.md`; license-granting NPCs/factions → `npcs-and-routines.md`; time/season
math → `time-tides-weather.md`; HUD/menus/sessions → `ux-and-mobile-controls.md`.

---

## 8. Open questions

1. **Energy/stamina system?** Comfort buffs assume a stamina resource (Stardew-style). Do we want a
   hard daily energy budget, a soft one, or none? This decides how much "teeth" the day has and how
   much housing comfort actually matters. *(Coordinate with `time-tides-weather.md` and the UX doc.)*
2. **Reputation as a hard gate — how often?** Current stance: mostly soft (prices/contracts), rarely
   a hard license requirement. Confirm the exact prestige licenses that *require* reputation.
3. **Renting vs buying property.** Do we offer leasing (rent/day, lower entry, no equity) alongside
   outright purchase for all commercial types, or only some? Affects the early-empire on-ramp.
4. **Multiple residences — fast travel?** If you own homes in several regions, do they enable fast
   travel / respawn points? If so, how does that interact with the "sailing is the game" feel and
   tide/wind risk (P1)? *(Touch `ux-and-mobile-controls.md` and `boats-and-navigation.md`.)*
5. **Decor sourcing balance.** Split of bought vs crafted vs found vs awarded decor — how much is a
   gold sink vs a reward for exploration/legendary catches?
6. **Skill caps & respec.** Do proficiencies hard-cap at 100 with no respec (mastery is permanent),
   and is there any prestige beyond Master band for very long-term players?
7. **Does the uncle's cottage get a story arc?** Opportunity: a slow refurbishment questline that
   doubles as the housing tutorial and an emotional throughline. Decide scope with
   `npcs-and-routines.md`.
