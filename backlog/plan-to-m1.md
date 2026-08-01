# Hidden Harbours — The Plan to M1

> **Status:** Working plan. Rewritten 2026-07-24 after the owner's M1 reframe (see §1). Audited against
> `main` @ `9df75c6`; §7.7 re-checked against `16ee546` (ADR 0022 phase 7 complete).
> **Canon:** [`../docs/vision-and-pillars.md`](../docs/vision-and-pillars.md) wins on any conflict, then
> [`../CLAUDE.md`](../CLAUDE.md), then this file.
> **What this is:** the M1 the game actually wants — a **world-first** vertical slice on St Peters Island —
> what it takes to build it, what's already built, and the decisions that gate it.
> **Supersedes:** the milestone scope in [`milestone-1-vertical-slice.md`](milestone-1-vertical-slice.md),
> which is mechanic-first and no longer describes the game. §9 carries the amended Definition of Done.

---

## 1. The reframe (why this document was rewritten)

The old M1 spec is a **mechanic-first** slice: a force model, a tension band, a price curve, six fish, one
cove. It treats the vertical slice as a *proof that the verbs are fun.*

That is the wrong test for this game. Hidden Harbours is a **world game**. Its nearest neighbours — Stardew
Valley, Big Ambitions — do not hold players with verb depth. They hold them with a place worth waking up in
and a ladder you can see the next rung of. Sailing and fishing being fun is necessary and (per the last two
months of playtests) already achieved. It is not sufficient, and it is not what M1 should be proving.

**M1's real question is therefore not "are the verbs fun?" but "is this a world I want to spend a season
in?"** Everything below follows from that.

---

## 2. What a vertical slice looks like in the games this one resembles

**Stardew Valley's slice is Spring Year 1, week one — not "grow a parsnip."** What ships in it:

- **Ten named people** with faces, houses, schedules, and an opinion of you — before you have done anything.
- **The whole town map, complete**: shops with opening hours, the beach, the bus stop, the mine door you
  cannot open yet.
- **Verb depth: almost nil.** Hoe, water, harvest, hand-pick forage. Four verbs, no skill ceiling.
- **A relentless drip**: parsnips day 1 → Willy gifts you the rod day 2 → forage and the shipping bin →
  the mines open day 5 → the Egg Festival day 13 → backpack upgrade when you can afford it.

**Big Ambitions' slice is one tiny shop — in a Manhattan that is entirely there on day one.** The shop
mechanics are trivial: buy stock, set a price, hire a clerk. What sells it is that you can walk real
districts, read real addresses, ride the bus, and stand in a competitor's store. The map tells you there are
fifty rungs above the one you're on.

**The shared formula, and the one to copy:**

1. A **small, complete, inhabited place** — not a big empty one.
2. **Shallow verbs, deep world.**
3. **A visible ladder** — you can see the next rung before you can reach it.
4. **A new rung every couple of days.**
5. **A reason tomorrow is different from today.**

Point 5 is where **Hidden Harbours has a structural advantage over Stardew.** Stardew's day-to-day variation
is weather RNG plus a fixed festival calendar — you don't learn it, you just receive it. Hidden Harbours'
variation is the **tide**: deterministic, forecastable, learnable, and able to gate content. A player who
reads the tide table and plans around it is exercising a *skill* Stardew never offers. **The tide is M1's
engine, and the tide-gated crossing to the mainland is the best idea in this design.** Build the slice around
it.

---

## 3. The M1 arc — the ladder

The owner's arc, laid out as rungs. Target pacing: **something new roughly every other day for the first two
weeks.** The grind is intentional; the drip is what makes it bearable.

| Day (≈) | Rung | New thing the player gets | Teaches |
|---|---|---|---|
| 1 | **Arrive** | You come to St Peters to be with your **aunt** after your uncle's death. The island, the cast, the cottage. | The place, the people, the tone (bittersweet, warm) |
| 1 | **The shovel** | Aunt's shovel, gifted. Dig shellfish on the flats **at low water**. | Verb 1 (dig) · the **tide** as your first constraint (P1) |
| 1 | **The clam licence** | You've a bucket of clams and no papers. Aunt fronts the fee; you walk to the **general store** and buy the licence yourself. | Licences gate what you may land · the store exists · a warm debt to your aunt |
| 1–2 | **First sale** | Sell shellfish to the store. First coin — and you owe Ginny for the licence. | Money · the island's economy is *small* |
| 2–3 | **The used rod** | Buy it at the store. Fish from shore. | Verb 2 (fish) · gear as progression |
| 3–4 | **The white bucket & the freezer** | Fish **rots**. Aunt freezes your catch while you fill a whole bucket. | **Freshness** — the pressure that makes time matter |
| 4–5 | **The crossing** | The springs come. Walk the **tide-gated sandbar** to the mainland with a frozen bucket. | The world is bigger · tide as a **gate** · a returning-tide clock (P5) |
| 5 | **Nine Mile Creek** | The wharf. A real fish buyer — better prices than the store. And there, hauled out: **a dory in disrepair.** | Where you sell matters · **the visible next rung** |
| 6–9 | **The dory** | Earn it. Buy her. Pay to repair her. **Sail her home** to the St Peters dock. | Verb 3 (sail) · the slice's emotional peak (P2/P4) |
| 9–11 | **Offshore** | New species reachable only by boat, further out. | The boat *unlocks world*, not just stats |
| 11–13 | **Shellfish gear** | Traps/pots — catch that works while you do something else. | Verb 4 · the first taste of P4 (earn it, then automate it) |
| 13–15 | **The used outboard** | Bought secondhand from someone at the wharf and **hung on the dory's transom**. She stops being rowed. Range and speed. | Progression that **opens map**, and the promise of more |

**The outboard goes on the dory — it is not the Punt.** Worth stating plainly, because the two are easy to
conflate: `boat.dory` is `Propulsion: Oars` (she is rowed, `OarPower: 300`), and `boat.punt_upgraded` is
`Propulsion: Engine` with a 14-unit hold — "The Punt (**Upgraded**)", where the upgrade *is* the motor. So
the Punt already has an outboard, and buying one for the dory is a **different rung**, not a substitute.

Making it the M1 climax is deliberate. It upgrades a boat you are attached to instead of discarding her,
it is cheap enough to actually reach inside a slice, and it buys **range** — more world — rather than a
bigger number. That is Stardew's rod-upgrade shape, not its farm-expansion shape, and it is the right one
this early. **The Punt stays on the ladder as the rung above**, reached late in M1 if pacing allows or just
after it: she is the step up in *hold*, which is the pressure the trap loop starts to apply.

How the outboard actually lands in data is a small architecture call — see **D8**.

---

## 4. The tide gate — how to do it without breaking the sim

The brief says *"the tide doesn't lower enough until the player completes the introduction."* Taken
literally that means a scripted tide, which breaks CLAUDE.md rule 5 (the sim is a pure function of
`(worldSeed, gameTime)`) — and, worse, breaks the pillar. If the tide is a story flag, it stops being a force
the player learns to read, and P1 dies in the tutorial.

**Do it the honest way instead: gate the calendar, not the tide.**

Tides run a spring/neap cycle of about 14.75 days. **Choose the world's start date so that the first big
spring low falls on day 4–5** — exactly when the arc wants the crossing. The tide stays 100% deterministic and
honest; the *story* is placed against it rather than the other way round. Aunt Ginny says "Thursday's the big
drain — that's your day," and the tutorial has now taught the player to read a tide table, on a real crossing,
with a real consequence. That is P1 delivered perfectly rather than faked.

**And handle the dawdler.** If the player misses the springs, the next ones are ~14 days out — brutal. So make
the crossing **possible on any decent low but comfortable only near springs**: off-peak the channel window is
narrower, there's more wading, and the returning tide is a tighter clock. Dawdling then costs *risk and
nerve*, not two lost weeks. Cozy, but with teeth.

This one decision converts the tutorial from a scripted sequence into the game's best teaching moment. It is
also nearly free — it is a start-date constant, not a system.

---

## 5. What changes against the old spec

| | Old M1 (mechanic-first) | New M1 (world-first) |
|---|---|---|
| **Home region** | Coddle Cove | **St Peters Island** |
| **Second region** | Port Greywick (services) | **Nine Mile Creek** — the *same place, renamed* (D1); a working wharf, not yet a town |
| **Opening** | Inherit Ned's dory | **Accompany your aunt**; earn the dory |
| **First verb** | Handline fishing | **Digging shellfish at low water** |
| **First market** | Wharf fish buyer | **The island general store** (worse prices — a reason to cross) · also sells the **clam licence** |
| **The gate** | — | **The tide-gated sandbar crossing** |
| **Pressure** | Hold capacity | **Freshness/rot** — freeze it, keep it alive, or lose value |
| **Climax purchase** | The Punt (a bigger boat, straight away) | **A used outboard for the dory** (range on the boat you love); the Punt becomes the rung above |
| **Cast** | Ned (departed) + 1–2 neighbours | **Aunt + a small inhabited island**: schoolhouse, general store, a few homes, 4–6 named people |
| **Proves** | "Are the verbs fun?" | **"Is this a world worth a season?"** |

**Out of M1** (not cancelled — re-phased): the mainland port as a **full town** (M2-13 — now Nine Mile Creek grown up, not a second settlement), Coddle Cove as the
home harbour (M2), the Punt purchase, blue-mussel and pollock, everything above the dory in the hull ladder.

---

## 6. The good news — how much of this exists

The last audit called the St Peters work "scope drift past M1." **That was wrong, and this document corrects
it.** The owner was building the *right* M1 against a spec that described the wrong one. Concretely, already
built and playtested:

- **The clam dig** (`ClamDig`, `ClamDigger`, `ClamSpot`, the "two squirting holes" tell) and the **shovel** and
  **bucket** as owned gear (`Data/Gear/Shovel.asset`, `ClamBucket.asset`, `ClamBucket : IHold`).
- **The tide-gated sandbar seam** — `Core.TidalExposure`, `IEnvironmentService.WaterLevelAt`,
  `TidalWalkability`, `PaintedTidalTerrain` (ADR 0009/0014). The hard part of the crossing is done.
- **Buy-and-repair** (`RepairLedger`, `DamagedDoryOffer`, `Shipwright`, boarding gated on repair).
- **Licences and vendors** (`LicenseService`, `GearShop`, `PotShop`, `LicenseVendor`).
- **Traps and pots**, soak-and-haul, the deck-work loop.
- **Aunt Ginny**, dialogue, the onboarding director already walking this exact arc.
- **The dory sails** — the force model (VS-09, the old spec's #1 risk) shipped and has been owner-playtested
  repeatedly. Wind pushes, tide sets, she carries way.
- **The sea itself** — deterministic tide, wind, displaced 3D water, tide-aware shoreline, day/night grade.
- Market with supply/demand and two channels; sell screen; save at schema v4; CI green.

**The verbs are done. What's missing is the world, the pressure, and the pacing.**

---

## 7. The gaps — seven workstreams

Ordered by what blocks what.

### 7.1 · Make St Peters a place — `world-content` + `owner` + `art-pipeline` — **the biggest item in M1**
Today the island is: one cottage, Aunt Ginny, Ned's letter, a dock. The arc needs an inhabited village.

- **Aunt's house** (interior or at minimum a lived-in exterior with the **freezer** as an interactable),
  **the schoolhouse**, **the general store**, and **2–3 more homes**.
- **4–6 named inhabitants** with portraits, a line of dialogue with an opinion, and a fixed spot. Anchored, not
  scheduled — routines are M2, and Stardew's week one works fine with people who mostly stand still.
- The flats where you dig; the sandbar head; the dock.
- **Owner authors the scene** (ADR 0019 — see §8, D3). Agents build the tooling and the logic layer.
- **Exit:** you can walk the island in two minutes, meet everyone, and know what each building is for.

> **Don't preclude the built waterfront (M2-J).** In M2 the player buys and sites **wharf buildings** from
> `wharfBuildingRig.js` (whose presets are already the ladder: net shed → ice house → cannery) onto decking
> from `wharfKitRig.js`. **Neither rig has ever been baked** — every building in the game today is a
> hand-made sprite and the wharves use a flat `WharfDeck.png`. So if M1's wharves and sheds are authored
> from one-off art, M2 inherits *two* systems and a re-authoring job. **Bake both rigs first (M2-40, M2-46)
> and author M1's waterfront from them.** Not new M1 scope: the same wharf and the same shed, built from
> the pieces the player will later buy.

### 7.2 · Nine Mile Creek — `world-content` + `owner` + `art-pipeline`
A new small region: the wharf, the **fish buyer** (better prices than the island store), the **derelict dory**
hauled out where you can see her from arrival, the **used-outboard seller**, and a couple of buildings for
flavour. A working creek, not a town. This is **Greywick renamed** (D1, mechanics in §7.10) — the region is
kept and re-dressed, so its outdated art is **replaced from the baked rigs** rather than repainted by hand.

- **Exit:** you arrive off the sandbar, sell, see the dory, and understand she is the next rung.

### 7.3 · Freshness & rot — `economy-sim` + `gameplay-systems` — **the one genuinely new system**
Load-bearing in the new arc. `CatchSpoilMath` draws the rot but its own header says "Who sets spoil: nobody
yet."

**Landed** (`feat/freshness-clock`): the Core contract and arithmetic — a **settle-on-read** accumulator
(spoil banked, the instant it was banked, the mode held since), so spoil is a pure function of `(state, now)`
and a reload, a sleep-skip and a fast-forward all agree. Changing mode settles first, so an hour in the sun
before the freezer is remembered forever. Three modes (ambient rots; **frozen** and **kept alive** arrest),
tunables in a `SpoilPolicy` struct.

**Fresh pays; rotten is rubbish.** Value falls linearly to **nothing** — no floor. Past the policy threshold
no buyer will take it at any price, and it **still occupies hold space until it is dumped**. The loss is coin
and a wasted trip, never a ruined save.

**What cold actually sells the player is TIME.** This is the frame to design and tune against, not coin.
Freshness puts a clock on your catch: the moment you land the first fish you are on your way back. Every
form of cold buys slack against that clock, and each buys it somewhere different:

| | Buys you | Costs |
|---|---|---|
| **Ice** | **Time at sea** — stay out, fill the hold, make one trip where you'd have made two | Recurring, per trip |
| **A lid** | **More time per unit of ice** — the same load stretches further | One-off |
| **The freezer** | **Time ashore** — bank the catch and wait: for a full bucket, for a better price, for the tide | Free, but only at home |
| **Kept alive** | Time, free — but only for shellfish | Species-limited |

Two things follow from that framing. First, **ice must not buy so much time that the clock stops mattering** —
its duration should extend a trip, never erase the constraint. Second, it slots cleanly beside the other
rungs, because each upgrade buys a *different* axis of "more per trip": the **outboard buys range**, **ice
buys endurance**, **the Punt buys capacity**. Three different answers to the same pressure, which is why they
should arrive days apart rather than together.

And it is P1 doing its job. The tide already sets your schedule — low water for digging, the returning tide
on the sandbar. Spoilage is the third clock, and ice is the **first tool that lets the player push back
against one of them**. That is a good thing to earn in a slice about learning to read the sea.

> The freezer's "time ashore" is deliberately shallow in M1 — it exists so Ginny can hold your catch until
> you've a whole bucket to carry across. Holding stock to time the *market* is M2-21's job; don't build it
> here, but note that this is its seed.

**Ice and lids — the portable cold chain.** The freezer is free but only at home, so cold has to be
*carriable* for the arc to work at sea. Three pieces, and they make the general store a repeat destination
rather than a one-visit shop:

- **Ice is a consumable** added to a storage container. It holds the catch like a freezer does, but only
  until it melts — **protection is a duration, not a state**. That framing is load-bearing: it keeps spoil
  piecewise-constant so the sleep-skip guarantee survives (`Freshness.SettleThroughProtection`, landed), and
  "six hours of ice left" is a legible resource where "chill 0.62" is not.
- **A lid** placed over a container slows the melt (and is a cheap one-off buy against ice's recurring cost —
  a nice early "spend to stop spending" beat).
- Together they make a **real decision**: how much ice do I buy for a long day offshore, and is it worth the
  margin? That decision does not exist while the only cold is at home.
- Container-side work: ice quantity + melt rate on `DeckContainerDef` (which today carries only id, name and
  fill sprites), a lid state and sprite, the melt maths (lid-modified), and the store selling both.

**Frozen visual.** Fish get a frozen look the way they get a rotten one. The pattern is already set:
`catchKit.js` exposes `tintSpoil`, and `CatchSpoilMath` is its byte-parity-tested C# twin feeding
`CatchFillRenderer.SetSpoil`. A frost tint is the exact sibling — `tintFrost` in the rig, a
`CatchFrostMath` twin, `SetFrost` on the renderer — so a glance at a tote tells you whether it is keeping.
`art-director` owns the rig recipe; `art-pipeline` owns the twin and the wiring.

**Still to build, each its own slice:**
- Stamp catches at landing; **perishability per species** on `FishSpeciesDef` (fast-rotting mackerel vs
  hardy shellfish).
- **Price + refusal wiring.** Note the trap: the value multiplier alone cannot make a catch worthless at the
  till, because `SellPricing.UnitPrice` floors every unit at 1₲. **Refusal must be a hard gate in front of
  the pricing maths**, not a price that trends to zero.
- The **freezer** (Ginny's), **live-bucket** and **ice/lid** interactables that set the mode.
- A **dispose verb** — empty the bucket, dump it over the side. Without it a spoiled hold is a soft-lock.
- Wire the existing rot **visual** to the clock, plus a **freshness read on the hold**. Non-negotiable now
  that a catch can become worth nothing: watching a bucket rot is a lesson; a buyer refusing one that looked
  fine is a bug.
- **Persist hold contents.** `SaveData` does not carry them at all today, so "freshness survives a save"
  needs a **v4→v5 bump** with a migration.

- **Exit:** filling a bucket over three days without the freezer visibly and financially costs you, and past
  a point nobody will buy it and you must dump it; freezing or keeping them alive saves them; a save/reload
  and a sleep-skip both preserve freshness exactly.

### 7.4 · The pacing model — `economy-sim` — **do this first, it's the cheapest de-risk**
"A new rung every couple of days" is a balance problem, and it is far cheaper to solve in a spreadsheet than
in a build. Model the whole ladder before building content against it: clam density and dig rate, store vs
wharf prices, rod cost, dory price, repair cost, trap cost, outboard cost, offshore catch rates, spoilage loss.

- **Exit:** a day-by-day projection showing rungs landing on the §3 schedule for an average player, tuned
  in `GameConfig`/Def assets (no magic numbers), and re-checkable after any price change.

### 7.5 · The island general store — market, gear shop, and licence vendor — `economy-sim`
Selling today happens at a wharf stall. The arc needs the **general store** to do three jobs:

- **Buy shellfish** at **deliberately worse prices** than Nine Mile Creek — so the crossing has an economic
  reason on top of a story one, and "where you sell matters" is taught in the first hour. `MarketId` already
  supports channels; add the island as one.
- **Sell the used rod** (`GearShop` exists; `Data/Gear/Rod.asset` exists).
- **Sell the clam licence** — a new `LicenseDef` (`license.clam`) beside the existing `license.cod`, vended
  by the existing `LicenseVendor`. Data-only; no new code.

**The chicken-and-egg, and how to dodge it.** `CatchLicensePolicy.MayLand` gates *landing* a species and
**fails closed** — no wallet, no gated catch. So a clam licence you must buy with clam money is a deadlock.
The fix is a character beat rather than a mechanic: **Aunt Ginny fronts the fee**, and the player walks to
the store and buys the licence themselves. The transaction is still the player's (they meet the vendor UI,
they learn licences gate species), the deadlock never happens, and it plants a small warm debt to pay back.

Then the **cod licence at Nine Mile Creek is the one you pay for yourself** — the same system, second time,
now with real money on the line. Licence one is taught; licence two is earned.

> **Housekeeping:** `Data/Licenses/CodLicense.asset` flavour text still reads *"Greywick's harbourmaster
> signs you off…"*. Retargeted as part of the rename (§7.10). The `id` (`license.cod`) is stable and does
> **not** change.

**LANDED — the economy half.** `MarketId.StPetersStore`, the clam licence, and the store's stock all ship;
what is left is *placement*, which is the NPC/world-content lane's this wave (the components exist and are
wired to their Defs; nobody has stood them on the counter yet).

**LANDED — the placement half.** `StPetersBuilder` now stands a `GeneralStoreCounter` in the store's
dooryard, one stride out past its keeper: four vendors on one GameObject (rod / capelin by the lot / ice /
the clam licence, listed together by `BuyCatalog`), plus a `Market` **explicitly on the `StPetersStore`
channel** behind a `FishBuyer`, so a bucket of clams sells here for the worse price the level was built to
express. The spot is *derived* from the store's own site toward the village green, so moving the building
moves its counter, its keeper and its door together. `FrontedFeeGrant` is placed too — on Aunt Ginny, where
the beat belongs — and her `GinnyOpening.asset` gates a two-line acknowledgement on the same authored flag
key the grant persists, so she mentions the fee only once it has actually been fronted and neither module
references the other (rule 4). Everything goes through the **builder**: a counter hand-placed in
`StPeters.unity` would be undone by the next run.

One finding worth carrying forward, because this item's own framing was slightly wrong. *"`MarketId` already
supports channels; add the island as one"* is true but **not sufficient**: demand `D` only ever appears as
`S/D` inside `1/(1+e·S/D)`, so at zero supply that term is `1` for **every** value of `D`. On a market nobody
has sold into yet — which is exactly the state of the world for the player's first bucket of clams — a
low-demand village counter and a high-demand wharf quote the *same coin*. Demand is a glut-*absorption*
lever; "deliberately worse prices" is a price-*level* difference. So the channel gained a **`PriceLevel`**
beside its demand, which is the `demandMood`/`P0` level term the canon formula already has
(`economy-and-business` §1.2) — the island store now pays a tunable fraction of dockside from the very first
unit, and the M2 demand random-walk multiplies onto it rather than replacing it.

The second finding is a **balance** one and belongs to §7.4, not here: every unit is floored at ₲1
(`SellPricing.UnitPrice`), so on a **2₲ clam** the level has barely 2₲ of room to move. The mechanism is
right and tunable, but the gap a player can actually *read* on a bucket of clams is bounded by the clam's
base value, not by the multiplier. If the crossing is meant to *feel* worth walking on day five, that is a
`FishSpeciesDef.BaseValue` question for the pacing model to answer with the whole ladder in view.

### 7.6 · The reads the player needs — `ui-ux` + `gameplay-systems`
Cut to what the new arc actually requires, in priority order:

1. **The in-game tide table** (VS-06) — currently editor-only. **Now essential**: it is how the player plans
   the crossing. Highs and lows for today and tomorrow, a now-marker, time frozen while reading.
2. **Freshness read** on the bucket/hold — how long has this been sitting.
3. **The wind widget and compass** (VS-19) — text today; needed once the dory is sailing.
4. Set-&-drift ghost track — **defer to M2**; the text read is enough for a forgiving inshore slice.
5. Sell-screen chalkboard skin — keep, it's cheap and it sells the diegetic promise.

### 7.7 · The dory's outboard — `art-pipeline` (+ `art-director`) + `gameplay-systems`
**Mostly delivered by ADR 0022 phase 7 while this plan was being written.** What is left is a dory-sized
motor mesh, three lines of data, and one wire.

**What's already there — and it's the hard part.** The dory is a **mesh hull**: `DoryIso.asset` binds
`HullMesh` → `DoryIsoHullMesh.asset` (ADR 0022 phases 6–7). Her **oars are real prop meshes**
(`DoryOarPortPropMesh`, `DoryOarStarPropMesh`) posed through the generic `IHullPropRenderer` /
`IsoFacetPropRenderer` — which is exactly the socket a transom motor wants. And the mesh path solves the
problem an outboard actually has on a 4.5 m boat: per `DoryOarMeshLayer`'s own notes, a mesh fitting is
parented to the hull's **posed** mesh child and so **inherits roll, pitch and heave for free**, where the
sprite oars needed five hand-tuned rock-coupling knobs to keep an overlay on the gunwale.

**What landed while this plan was being written.** ADR 0022 **phase 7 completed** on `main` (#286,
`16ee546`) and it did most of this item:

- **`OutboardMotorMeshLayer`** + **`OutboardMotorMeshPose`** exist — the mesh motor layer, and it reuses
  `OutboardMotorMath` throughout (one state machine, not two, exactly as the codebase requires).
- **Four outboard prop meshes are baked**: `PuntMotorBasic`, `PuntMotorUpgraded`, `SkiffMotorSport`,
  `SkiffMotorWork`.
- **`BoatVisualDef` gained a `MotorMesh` field**, and the dory's asset now carries the slot.

**All four items landed — with one art ask left over:**
1. ~~A motor mesh suited to the dory.~~ **Her own kicker rig already existed and nobody had noticed**:
   `docs/art/rigs/doryMotorRig.js` is a complete little tiller two-stroke (0.23 m cowl against the punt's
   0.30, 76 mm prop, registered to her transom board). It has never been **baked**, and an agent cannot bake
   — so she wears the **punt's basic outboard on loan** meanwhile, shifted onto her own transom. **THE ART
   ASK is one menu item in the owner's editor**, not a drawing job: give the kicker a `HullPropFleet` entry
   and run *3D Hulls ▸ Bake ALL hull fittings*. Until then the loan's cost is a short leg — see below.
2. ~~Assign it.~~ `DoryIso.asset` carries the fitting, wired through the **prop catalog** (so a re-bake
   cannot quietly un-wire it) and pinned by `DoryOutboardContentTests` / `HullPropFleetWearerTests`.
3. ~~A real transom mount.~~ Measured off `doryIsoRig`'s own MOTOR BOARD: **`(0, -2.35, 0.80)`**
   (`MOUNT = TR.y − 2·BOARD.t, TR.zTop + BOARD.rise`, projected at `y − 0.01`). The skiff default she used
   to carry was **1.18 m astern and 0.08 m low** on a 4.5 m boat.
4. ~~Ship the oars when the motor runs.~~ `DoryOarMath.RestingColumn` wired to the engine — immediately, no
   rest-grace to wait out — with the stroke accumulators frozen, so cutting the motor puts the oars back
   mid-sweep instead of at the top of a cycle.

This is the **visual half of D8**, and the gameplay half came with it: `boat.dory_outboard` (Propulsion =
Engine, every other stat copied from `boat.dory` so the pair cannot drift) wears the **same visual**, and
`BoatHullSkinner` draws the engine only on the hull that has one. She is a picker rung and a fleet-registry
hull; **no `ShipwrightOffer` — what she costs and who sells her is the Nine Mile Creek purchase beat.**

- **⚠️ The loan's one cost, stated:** the punt's leg is 0.185 m shorter than the reach the dory's high
  transom needs, so the borrowed engine is hung by its **prop** (which lands exactly where her own kicker's
  does) and its clamp therefore sits a hand's breadth below her motor board. Reducing its scale — §7.7's
  original suggestion — makes this worse, not better: it lifts the prop clear of the water. The purpose bake
  fixes both, and zeroes the fitment offset.
- **Exit:** the repaired dory wears a secondhand kicker that swivels with the helm and rides the wave field
  with the hull; cutting the motor ships the oars back out; the sprite path is untouched.

### 7.8 · The shell — title, new game, settings, pause — `ui-ux` + `lead-architect`
**There is no shell around the game today.** No title screen, no main menu, no pause menu, no settings UI, no
`Application.Quit`. `GameRoot` boots straight into play and `SaveService.Load()` does
`loaded ?? SaveMigration.NewGame()`.

**The forcing item is New Game, not the title art.** With one auto-loaded slot and no reset, a tester cannot
get a second first-impression and cannot recover from a bug except by hand-deleting a file. For a milestone
whose acceptance criterion is *"did they come back for a third session?"*, that is disqualifying.

**Minimum shell for a closed playtest:**
- **New Game** (confirm before overwriting an existing save) — the item that actually blocks the playtest.
- **Continue** — load the existing save. One slot is plenty for M1.
- **Settings**: master / ambience / SFX / music sliders, wired to `AudioDirector`'s existing serialized
  fields, plus fullscreen/windowed. The M1 DoD already promises "independent volume sliders" and there is no
  player-facing surface for them.
- **Quit.**
- **A build/version stamp on screen.** Cheap, and it makes every playtest bug report attributable to a build
  instead of a guess.
- **A pause menu** carrying the same settings plus quit-to-title — testers need it mid-session, and the
  tide-table panel already establishes the pause-the-world pattern (`timeFlowMultiplier = 0`).

**Deliberately not M1:** save slots, cloud saves, an animated or music-synced title sequence, key rebinding,
graphics presets beyond fullscreen/windowed.

**The title image is nearly free.** The project renders a whole world — frame the dory at a mooring at dawn
from an in-game camera and put the wordmark over it. Do **not** spend hand-painted art budget on a title
before the GO/POLISH/PIVOT verdict is in.

- **Exit:** a tester can install the build, start a new game, adjust volume, pause, quit, relaunch and
  continue — and can tell you which build they were on.

### 7.10 · Rename Port Greywick → Nine Mile Creek — `lead-architect` + `world-content`
The owner's call (D1): it is **one place, renamed**, not a new region beside the old one. Canon's mid-size
town (M2-13) becomes Nine Mile Creek grown up, so the coast has **one** mainland port.

**Canon leads, and has already been changed.** A region's name is a locked canon fact, so the docs went
first (CLAUDE.md): canon, the roadmap, all ten design docs and this backlog now say **Nine Mile Creek** —
162 replacements. **The ADRs deliberately keep "Greywick"**: an ADR records a decision as it was made on its
date, and rewriting one falsifies the record. Only the **code and assets** remain, below.

**Do the id properly — and do it now.** `RegionDef.Id` is `region.port_greywick`, and ids are append-only
and stable (CLAUDE.md §5). That rule exists to protect **shipped saves**, and there are none: M1 isn't
finished, nothing has been released, and the only saves in existence are the owner's. So the choice is
between carrying `region.port_greywick` forever as the id of a place called Nine Mile Creek — a papercut for
every future reader — or changing it while the cost is nearly zero.

**Change it, now, before the region content is authored against it.** This is the cheapest this will ever be.

**The surface is smaller than the mention count suggests** (≈278 code / 231 test / 261 doc mentions, but most
are prose):
- `Data/Regions/PortGreywick.asset` → id `region.nine_mile_creek`, `DisplayName: Nine Mile Creek`,
  `SceneName: NineMileCreek`. Rename the asset file too.
- The id appears as a **literal in only ~8 code and ~13 test sites**, plus 3 fish assets that list it in
  `RegionIds` (`AtlanticCod`, `Haddock`, `Mackerel`).
- `MarketId.Greywick` → `NineMileCreek`. **The enum value stays 1** — only the symbol changes, so nothing
  serialized moves.
- `GreywickBuilder.cs`, `GreywickMarketTests.cs`, `GreywickDockTests.cs` → renamed files.
- `GreywickHouseRed.png` / `GreywickHouseTeal.png` → renamed (and superseded anyway when the building rigs
  bake, M2-40/46).
- `Core.RegionDisplayNames`, `WorldStrings`, `RegionFade`, `OnboardingDirector` copy.
- `Data/Licenses/CodLicense.asset` flavour: *"Greywick's harbourmaster…"*. (`license.cod` id is unaffected.)

**One save migration.** `SaveData.PlacedTrapDto.Region` stores a region **string**, so a placed trap saved
before the rename would orphan. Add a guarded `SaveMigration` step rewriting `region.port_greywick` →
`region.nine_mile_creek` — the same shape as the four steps already there.

- **Exit:** no `Greywick` anywhere outside changelog history; a pre-rename save loads with its traps intact;
  the content-validation test passes; CI green.

### 7.9 · Audio, localization, acceptance — `audio` · `lead-architect` · `qa-test`
Unchanged from the previous audit and still real:

- **Audio: zero asset files in the repo.** The music bus ducks correctly but has no stem. The rising-wind
  tell is canon-sacred and unbuilt. Longest real-world lead time in M1 — see D4.
- **Localization** is a hard DoD line with nothing behind it; `HudStrings`/`WorldStrings`/`NpcDef` are seams
  built for exactly this. Cheap now, brutal after M2.
- **Acceptance**: desktop-baseline profiling, an automated core-loop smoke test, a v1→v4 save-migration test,
  then the external playtest and the written **GO / POLISH / PIVOT** verdict.
- **The playtest that matters most is the pacing one**: does a fresh player hit the §3 rungs on schedule, and
  do they want a third session?

---

## 8. Decisions (these gate work)

| # | Decision | Recommendation |
|---|---|---|
| **D1** | Is Nine Mile Creek a rename of Port Greywick, or a new region? | **DECIDED by the owner: a rename. Greywick *is* Nine Mile Creek.** One place, renamed — not two. That also means canon's mid-size town (M2-13) is **Nine Mile Creek grown up**, not a separate settlement, so the coast has one mainland port rather than two. See §7.10 for how the rename is done without breaking saves — and note it is a **canon change**, so `vision-and-pillars.md` and `world-and-regions.md` §6.3 must be updated *first* (CLAUDE.md: canon leads). |
| **D2** | Is **Coddle Cove** in M1 at all? | **No — and rename the milestone.** Your arc ends with the dory moored at St Peters; the Cove never appears. It stays in the repo, committed and unbroken, as the M2 home harbour canon already says you settle into. M1 becomes **"Vertical Slice — St Peters"**: two regions, which is the right size. Three regions taxes every art, audio, and perf pass for a place the slice doesn't use. |
| **D3** | Ratify ADR 0019 and author the two region scenes? | **Yes — this is the critical path.** Agents cannot author a `.unity`; the tooling is ready and the scenes are yours to build. With the reframe, this is *most of M1*: the world **is** the deliverable. Nothing in §7.1–7.2 finishes without you at the editor. |
| **D4** | Where does audio come from — commissioned, licensed, or procedural-only? | **License or commission the music bed and the wind tell; keep procedural ambience.** Longest lead time in the milestone — decide it now even though it lands last. A world game with no music will read as a tech demo no matter how good the water looks. |
| **D5** | Wire Unity Localization now, or ship English-only against the seams? | **Now.** The seams were built for it, so no call site changes. A day today; weeks after M2 triples the string count. |
| **D6** | Freeze later-milestone work? | **Freeze M3+ only** — the offshore hulls, trawlers, tanker, and the eleven-hull fleet are genuine drift. The St Peters batch is **not** drift; it is this M1, and the last audit was wrong to call it scope creep. Keep building it. |
| **D7** | Do pollock and blue-mussel come back? | **Pollock: already landed** (`Pollock.asset`, on `main` via #291). **Blue-mussel: no** — M3-16 aquaculture owns mussels. The species that matter now are the island shellfish (dig), the shore fish (rod), 2–3 **offshore** species that reward the dory, and the trap shellfish. Species earn their place by which **rung** they unlock, not by filling a list of six. |
| **D8** | How does the outboard land on the dory — a **hull variant asset** or a **real component swap**? | **Variant asset for M1; component split in M2-17.** `BoatHullDef.Propulsion` is a fixed field on the hull, so the cheap route is a second asset — `boat.dory_outboard`, `Propulsion: Engine` — that the purchase swaps the active hull to. Zero code, and it is exactly the pattern the repo already uses (`boat.punt_upgraded` *is* the Punt-with-a-motor variant). The cost is two assets to keep in sync for one boat, which is fine at one upgrade and bad at ten. **M2-17 (component swaps at the shipwright) is the item that should do the proper Hull/Engine/Hold split** — with a save migration, when there are many upgrades to justify it. Do not refactor `BoatHullDef` for M1. **The visual half is §7.7** — the motor does not exist as a prop mesh yet. |

---

## 9. The amended Definition of Done

Supersedes §6 of [`milestone-1-vertical-slice.md`](milestone-1-vertical-slice.md) once ratified. Bars are the
ADR 0005 desktop baseline (60fps on a typical desktop/laptop GPU, KB/mouse + gamepad).

**The world** *(new — and the point of the milestone)*
- [ ] **St Peters is an inhabited place**: aunt's house, schoolhouse, general store, 2–3 homes, and 4–6 named
      people with faces and opinions. You can walk it in two minutes and know what everything is for.
- [ ] **Nine Mile Creek is a working wharf**: fish buyer, the derelict dory in plain sight, the outboard
      seller. Reached across the sandbar.
- [ ] Both regions are **committed scenes** that load additively without breaking the persistent core.
- [ ] The opening is **warm and bittersweet** — you came for your aunt, not for a fishing career.

**The ladder**
- [ ] A first-time player hits a **new rung every ~2 days for the first two weeks**, on the §3 schedule,
      validated in playtest — not just modelled.
- [ ] Each rung is **visible before it is reachable** (the derelict dory is the template).
- [ ] The **clam licence** is bought by the player at the general store on day one, with Ginny's fee — and
      the **cod licence** later is paid for out of their own earnings.
- [ ] The **used outboard** closes the slice: bought secondhand, hung on the dory, extends range, and points
      at the Punt above it.

**The sea (P1)**
- [ ] The **tide is the engine**: it gates the dig, gates the crossing, and is read from an **in-game tide
      table**. It is never scripted — the calendar is placed against a fully deterministic sim.
- [ ] The crossing is **possible off-peak and comfortable at springs**; missing it costs nerve, not a week.
- [ ] You sail the repaired dory with the force model — wind pushes, tide sets, she carries way. ✅ *done*

**The pressure (P5)**
- [ ] **Fish rot.** Fresh pays best; neglected catch loses value and eventually **no buyer will take it** —
      rubbish you must dump before the hold is any use again. Freezing, icing or keeping them alive arrests
      it, and the state is **visible on the catch** — rotten and frozen both read at a glance, well before
      worthless. Freshness survives save and time-skip exactly.
- [ ] **Cold is carriable and it costs.** Ice bought at the store holds a container for a while and melts;
      a lid slows it. Deciding how much ice a long day is worth is a real call, and the store is worth
      returning to.

**The craft**
- [ ] Content is data-driven; environment is a pure function of `(seed, gameTime)`. ✅
- [ ] PPU=32 / true metric scale holds everywhere. ✅
- [ ] All player-facing strings go through **localization tables**.
- [ ] Music, ambience, and the **rising-wind tell** are in and mixed.
- [ ] The build has a **shell**: title, **New Game**, Continue, volume sliders, pause, quit, and a visible
      build stamp. A tester can restart from scratch without touching a file.
- [ ] Desktop frame budget profiled; **core-loop smoke test** passes in CI; a v1 save migrates to current.

**The verdict**
- [ ] External testers completed the arc **and came back for a third session**; `qa-test` delivered the
      written soft-launch-readiness verdict with a GO / POLISH / PIVOT recommendation.

---

## 10. The route

**Wave 0 — decide (owner).** D1–D7. Ratify ADR 0019. Start audio sourcing (longest lead time).

**Wave 1 — the numbers and the tooling, before the content.**
`economy-sim`: the pacing model (§7.4) — this determines every price and catch rate downstream, so it comes
first. `tools-editor`: CREATE/REFRESH for the two new regions. `lead-architect`: localization. `economy-sim` +
`gameplay-systems`: the freshness clock (§7.3).

**Wave 2 — build the world.**
`world-content` + `art-pipeline` + **owner**: St Peters village and Nine Mile Creek. The island store as a
market channel. The cast, their portraits, their lines. The tide-table panel. **This is the bulk of M1 and
the owner is in the loop for all of it.**

**Wave 3 — dress and pace it.**
Audio (beds, theme, wind tell, catch sting, home warmth). Wind widget and compass. Sell-screen skin. **The
dory's outboard prop mesh and its mesh motor layer (§7.7)** — start the bake early in this wave, since the
slice's closing rung cannot land without it. **The shell (§7.8)** — New Game before anything else in it, since
Wave 4's playtest cannot run without a reset. Tune the ladder against the Wave 1 model with real play data.

**Wave 4 — prove it.**
Profiling, smoke test, save migration, external playtest, the verdict. **Test the pacing, not just the
absence of bugs** — the question is whether they come back on day three.

---

## 11. Risks

| Risk | Why it bites | Mitigation |
|---|---|---|
| **The world is owner-serialized** | The milestone is now mostly scene authoring, and agents cannot author a `.unity`. Owner hours are the schedule. | Ratify ADR 0019 first. Agents pre-build every prefab, interactable, and logic root so authoring is placement, not construction. |
| **Pacing is invisible until it's wrong** | "A rung every couple of days" fails quietly — it feels fine to the person who tuned it and grindy to everyone else. | Model it in Wave 1 before content exists. Then test it on people who have never seen it. |
| **Freshness makes the game stressful, not cozy** | A rot timer is the easiest way to turn cozy into anxious. | Generous windows, an always-available arrest (the freezer is free and adjacent), value loss only — never destroyed catch, never a failed day. |
| **Audio lead time** | Zero assets; a canon-sacred wind tell can't be conjured in a sprint. | Decide D4 in Wave 0; build all cue logic against placeholder stems so only the audio swaps in. |
| **The verbs pull focus again** | Fishing and sailing are the fun part to work on, and they're already done. Every hour there is an hour the village doesn't get. | D6's freeze, and this document: M1 is the world now. |
| **Coddle Cove steps out of M1** | A committed, fully art-passed region leaves the slice. | It isn't deleted — canon already has it as the home harbour you settle into, so it is M2's, scoped and waiting. Greywick isn't lost either: it is **renamed**, not retired, and its logic carries straight over. |
