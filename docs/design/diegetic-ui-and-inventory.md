# Hidden Harbours — Diegetic UI & Physical Inventory (the world is the interface)

> **Status: DESIGN DIRECTION — ratified in principle (owner, 2026-07-05); details still forming;
> NOT yet built.** This is a *vision* document, not an implementation spec. It captures the owner's
> ratified direction for how the player reads information, carries things, and trades, so that all
> future UI/inventory/economy work bends toward it. The current M1 build's always-on HUD and its
> buy/sell menus are **M1 scaffolding** (they exist to prove the loop is fun) — this doc is the
> **M2/M3 shape** those scaffolds grow into. Nothing here changes any of the five pillars; it is a
> *presentation and structure* direction that serves them. When a concrete system is built, its data
> model and any save-format change become their **own ADR** (see §9).
>
> Design module. Subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md) (CANON — wins
> on conflict). This doc is the **front line of the diegetic promise**: it serves every pillar but
> lives closest to **P1 (The Sea Has Moods)** — the sea stays mysterious until you earn the tools to
> read it — and to **P2 (Dory to Dynasty)** and **P4 (Earn It, Then Automate It)** — progression *is*
> the growth of what you can carry, hold, store, and read.
>
> Sibling docs: [`ux-and-mobile-controls.md`](ux-and-mobile-controls.md) (the current UI/HUD/screens
> spec — **this doc reframes its HUD and its menus**, see §7), [`economy-and-business.md`](economy-and-business.md)
> (the market this doc turns into *conversation*), [`progression-and-housing.md`](progression-and-housing.md)
> (the storage/transport ladder this doc makes the *spine* of progression),
> [`boats-and-navigation.md`](boats-and-navigation.md) (the boats whose holds are the largest
> containers; the instruments that gate their readouts), [`time-tides-weather.md`](time-tides-weather.md)
> (the tide/wind/clock the instruments read), [`art-and-audio-bible.md`](art-and-audio-bible.md) (the
> hand-crafted pixel-art objects and diegetic cues this doc leans on),
> [`../adr/0008-save-schema-and-versioning.md`](../adr/0008-save-schema-and-versioning.md) (the save
> schema world-persistence will extend).

---

## 1. The philosophy — the world IS the interface

Hidden Harbours wants to feel like a place you *inhabit*, not a place you *operate through a menu*.
The design north star, ratified by the owner, is **diegetic**: wherever we can, information and
interaction live **in the fiction** — objects you see and handle, readouts on instruments you own,
water that rises past your knees — rather than in an abstract HUD or a spreadsheet floating over the
world.

This is not anti-UI dogma. Menus and overlays are tools; some jobs genuinely need them (see §8 on
merchants). The rule is a *bias*, applied hardest where it pays off most:

> **Minimize the HUD. Prefer the world. Make information something you *earn the ability to read*,
> and make possessions something you *see, hold, and put down*.**

Three load-bearing ideas carry the whole direction. They reinforce each other, and each is a
concrete, honest expression of a pillar:

1. **Physical, limited inventory** — you have two hands, pockets, and a bag, not an infinite grid.
   You carry things because you *chose to pick them up*, and you handle hand-crafted pixel-art
   objects, not rows in a list. (§4)
2. **Progression is logistics** — the grind and the reward are the *upgrading of storage and
   transport*: bucket → tool rack → backpack → cart → boat hold. Every rung is "I can carry / hold /
   store / haul more." This is the most honest form of **P2** and **P4**. (§5)
3. **Information is an earned instrument** (the keystone) — you start knowing *nothing*, not even
   the time of day. Every readout is **gated behind owning the instrument that reads it**: a watch
   gives you the clock; a compass gives you heading; a depth finder gives you depth. "True UI" is
   reserved for these bought **navigation instruments** — they *are* the progression rewards, and
   the sea stays mysterious until you earn the tools to read it (**P1**). (§3)

The rest of this doc develops each idea, states honestly where the current build sits relative to it
(§6), phases the work against the roadmap (§7), and lists what is still open (§9).

---

## 2. Design goals (what "diegetic" buys us, concretely)

1. **The world stays legible without a wall of HUD.** A calm, uncluttered screen where the *sea and
   your boat* are the read — not a cockpit of gauges bolted over everything. Clutter is removed by
   moving reads into the fiction, not by hiding information the player has earned.
2. **Every readout is a reward.** Turning "reduce the HUD" into a *rule* — a gauge only appears once
   you own its instrument — means the HUD grows with the player. A new instrument is a visible new
   capability (**P2**), and the sea's mystery is a resource we spend deliberately (**P1**).
3. **Possessions have weight because they're physical.** A tool you must grab and put back, a bucket
   you fill and carry, a bag that can be full — these make "getting more capacity" a felt, tangible
   goal instead of a number going up (**P2/P4**).
4. **Coziness through tactility.** Handling hand-crafted objects, placing a bucket on the wharf,
   fitting your gear into a bag — this is the warm, Stardew-adjacent, "my stuff, my place" feeling,
   applied to *carrying* as well as *decorating* (**P5**, the cozy column).
5. **Honesty about menus.** Where a menu is genuinely the right tool (a merchant's stock, a
   management dashboard), we don't pretend otherwise — we make it *fast, structured, and diegetically
   dressed*, and we keep it rare. Good UX beats purity (§8).

---

## 3. Information is an earned instrument (the keystone rule)

This is the idea that makes the whole direction concrete and testable. It converts the vague goal
"reduce the HUD" into a hard rule with a clear first proof.

### 3.1 The rule

> **Every HUD readout is gated behind owning the instrument that produces it.** No instrument, no
> readout. The player starts with *nothing to read* — not even the time of day — and each acquired
> instrument lights up its readout for good.

The player begins with the sea, the sky, and their own senses. They *feel* the wind on the water and
*see* the tide against the piling, but they cannot **read** any of it as a number or a gauge until
they own the tool that reads it. Acquisition is the moment a new "true-UI" element earns its place
on screen.

### 3.2 The instrument ladder (illustrative — the data owns the real list)

Each instrument is a bought/earned object that unlocks exactly one class of readout. Ordering,
prices, and gates are tuned later against the economy and progression docs; this table shows the
*shape*, not final content.

| Instrument | Unlocks (the "true UI" it grants) | Rough era |
|---|---|---|
| **Watch / clock** | The **time and date** — the cheapest first proof (§3.3) | Very early (St Peters) |
| **Compass** | **Heading** (cardinal + degrees) | Early — first real navigation |
| **Tide almanac / tide staff read** | The **tide table** and a numeric height/turn read | Early–mid |
| **Barometer** | **Pressure + a short forecast hint** | Mid |
| **Wind gauge / masthead vane** | **Wind direction + strength** as a read (not just felt) | Mid |
| **Depth finder / sounder** | **Depth under the hull** (the anti-grounding read) | Mid–offshore |
| **GPS** | **Position / a plotted fix** on the chart | Offshore |
| **Radar** | **Contacts / land in fog** | Offshore / The Smother |
| **Night vision** | **Sight at night** beyond your boat's own lights | Late |

> The exact instruments and what each one gates are **content and progression decisions**, owned
> jointly with [`progression-and-housing.md`](progression-and-housing.md) (the unlock graph) and
> [`boats-and-navigation.md`](boats-and-navigation.md) (which instruments are boat-fitted vs
> hand-carried). This doc owns the *rule*; those docs own the *list*.

### 3.3 The on-ramp: hide the clock, sell a watch (the cheapest first proof)

The rule needs one small, unmistakable first instance to prove itself. It is this:

> **On St Peters, at the very start, there is no clock on screen. You buy a watch — and the time and
> date appear.**

This is the cheapest, most legible proof of the whole philosophy. It reframes an existing HUD
element (the clock, which the M1 build shows unconditionally) as a *reward*: the player's first
"true-UI" element is *earned*, and from that first moment they understand that the instruments they
buy are how they learn to read the sea. It also sits perfectly inside the ratified St Peters opening
(canon §5.8): a humble island, hand-work first, and the slow lighting-up of the world as you acquire
your tools.

Everything downstream is the same move at a larger scale — a compass, a depth finder, a barometer —
so the HUD the player ends the game with is a record of everything they earned the right to read.

### 3.4 What stays felt, not read

The rule gates *readouts*, not *forces*. The sea still acts on the player before they can measure it:
wind still pushes the boat, the tide still floods the flats, fog still blinds — these are **P1**
forces the player *feels and respects* whether or not they own the gauge that quantifies them. The
instrument doesn't turn a force on; it turns *legibility* on. A player without a barometer still gets
caught by weather; the barometer just lets them *see it coming*. That is exactly the P1/P5 tension we
want: buying the instrument is buying foresight, and foresight is the reward.

---

## 4. Physical, limited inventory

The player is a person on a coast, not a bottomless satchel. Inventory is **physical and limited**,
and it is made of **hand-crafted pixel-art objects you see and handle**, not abstract list rows.

### 4.1 What you carry on your body

- **Two hands.** Realistically, **one tool at a time.** You *grab* the shovel when you need it and
  *put it back* when you're done; you cannot walk the coast bristling with seventeen tools. Swapping
  tools is a deliberate act, not a menu flick.
- **Pockets.** A few small items — the humble everyday things.
- **A backpack / bag.** Small items and money live here. The bag is itself a **container** (§4.2)
  with limited room, and it is an early upgrade target (§5).

The felt consequence: *what to bring* is a real decision. You don't carry the whole shed to the
flats; you bring the shovel and the bucket, and if you want the rod too, something has to give or you
need more capacity. Scarcity of carry is what makes the storage/transport ladder (§5) matter.

### 4.2 The container model

Capacity is expressed through **containers**, and containers are the heart of the model.

- **Capacity is a fullscreen grid.** Opening a container shows **the container itself, fullscreen**,
  and items occupy **cells by their physical size** — a Tetris / Resident-Evil-attaché-case feel.
  Capacity is **size-based, not weight-based**: a shovel is long and awkward; a clam is small; a
  wallet is tiny. You arrange your things, and a big awkward tool *costs* the room it would really
  take.
- **Nesting is allowed — but always visible and type-constrained.** Containers can hold containers,
  but there is **no hidden black-box nesting**: when a container holds another, you *see* it, and you
  can open it. Nesting is a **taxonomy, not "any object in any container."** Each container declares
  which item **types** it accepts:
  - a **fish-storage container** holds **buckets of fish**,
  - a **bucket** holds **fish**,
  - a **wallet** holds **cash**,
  - a **backpack** holds a **wallet** (and other small items).

  This taxonomy is what keeps the physical model coherent and readable — you don't stuff a boat's
  worth of cod loose into a coin purse. It also makes the ladder legible: you know exactly what the
  next container *up* lets you carry.
- **Over-encumbrance = you simply can't pick it up.** There is no weight meter and no slow-crawl
  punishment for now: if there's **no room**, you **cannot pick the item up**. Full is full. (A
  situational *slowing* for specific cases — e.g. hauling something genuinely awkward — may be added
  later as a teeth beat; it is explicitly deferred, §9.)
- **Objects, not rows.** Every item is a hand-crafted pixel-art object the player sees in the world
  and in the container — the clam, the shovel, the bucket, the wallet. The container UI shows the
  *things*, arranged in their grid, not a text inventory. This is the diegetic promise applied to
  possessions (art direction in [`art-and-audio-bible.md`](art-and-audio-bible.md)).

### 4.3 Placing and carrying containers in the world

Containers are **things in the world**, not abstract slots:

- You **place items into** a container and you can **carry the container** and **place it down**. The
  first fish container is the **bucket**: a ~20-clam / fish pail you fill by digging, carry to
  Greywick, and empty at the stall. (The M1 build already models this bucket as a hand-carried hold —
  see §6 — which is the seed of this model.)
- **World persistence: items and containers set down in the world stay there.** A bucket left on the
  wharf is *there* when you come back; a tool rack you place is a fixture. This is a genuinely
  physical world of objects, and it is what makes "I put my stuff down and it's mine, here" real.

  > **Flag (touches the save system, ADR 0008).** World-placed items and containers are *mutable
  > world state* that must persist across save/load. The current save (ADR 0008) records money,
  > owned boats, owned gear, and flags — **not** world-placed object positions or container contents.
  > World persistence therefore **extends the save schema** and is its own future ADR (§9). Do not
  > assume the current schema covers it.

---

## 5. Progression IS logistics (the storage & transport ladder)

If inventory is physically limited (§4), then the natural, honest engine of progression is **making
the limit bigger** — and doing it through *things you can point at*. The grind and the reward are the
**upgrading of storage and transport**.

### 5.1 The ladder

Every rung answers the same sentence — *"I can carry / hold / store / haul more"* — and every rung is
a visible object in the world:

| Rung | Container / transport | "I can now…" |
|---|---|---|
| **Bucket** | A ~20-fish pail | …carry a hold of clams to market by hand (the first fish container) |
| **Tool rack** | Holds shovels / tools | …keep and swap tools without carrying them all on my body |
| **Bag / backpack** | Body-worn container | …carry more small items and my wallet at once |
| **Cart** | Wheeled transport | …move far more than two hands and a bucket over land |
| **Boat storage (the hold)** | The vessel's hold | …carry a *voyage's* worth — the largest container, and the whole point of a boat |

The ladder deliberately runs from *your hands* to *your boat*, so it dovetails exactly with the boat
ladder (canon §5.4) and the progression spine ([`progression-and-housing.md`](progression-and-housing.md)
§3). Getting a bigger boat is, among other things, *getting a much bigger container* — which is the
most physical, honest reading of **P2 (Dory to Dynasty)** we have.

### 5.2 Why this is the truest P2 / P4

- **P2 (Dory to Dynasty):** the fantasy of scale becomes *literally* the fantasy of capacity. You
  grow from a person with a bucket to an owner with holds, warehouses, and carts moving between them.
  Power is never an invisible stat — it's a bigger thing you can see and fill.
- **P4 (Earn It, Then Automate It):** you carry it by hand first, so hauling *has weight*. Then the
  higher rungs (carts, boat holds, and — later, in the economy layer — warehouses and staffed
  logistics) let you move more with less personal effort. The logistics ladder is the on-ramp to the
  automation layer that [`economy-and-business.md`](economy-and-business.md) owns: hand-hauling →
  cart → hold → *hired hauler moving product between your sites*. The physical bucket and the
  freight route are the same idea at two ends of the game.

### 5.3 Relationship to housing/commercial storage

[`progression-and-housing.md`](progression-and-housing.md) already owns **home storage** (the
sea-chest / lockers) and **commercial storage** (warehouses), and
[`economy-and-business.md`](economy-and-business.md) owns storage *as an economic tool* (store to
time sales). This doc doesn't replace those — it **reframes them as the top of the same physical
ladder**: the sea-chest is a big fixed container in your home, a warehouse is a huge fixed container
you staff, and both obey the same "containers hold typed things, and you can see what's in them"
model. The through-line — from the clam bucket to the cannery's cold store — is one coherent idea of
*capacity you earn and can point at*.

---

## 6. Where the current M1 build sits (honest scaffolding vs. target)

**Read this plainly: the current build is not wrong — it is scaffolding, and it is doing its job.**
M1 exists to prove the fishing→sell loop is *fun* (roadmap §M1), and the fastest way to do that is an
always-on HUD and simple menus. This section states honestly what exists today and how it relates to
the target above, so nobody mistakes the scaffolding for canon and nobody tears down working M1 code
prematurely.

### 6.1 What exists today (grounded in the code)

- **The HUD is always-on and un-gated.** `HudController` (`Assets/_Project/Code/UI/HudController.cs`)
  builds a top-band overlay that shows **clock, tide, wind, sea state, money** unconditionally, plus
  a **compass / heading + set-&-drift cluster** when at sea. None of it is gated behind owning an
  instrument — the player reads the whole sea from the first second. *This is the direct opposite of
  the §3 rule, and that is fine for M1's job (prove the loop).* It is the primary thing §3 reframes.
- **A physical bucket already exists — as a flat count, not a grid.** `ClamBucket`
  (`Assets/_Project/Code/Player/ClamBucket.cs`) is a hand-carried clam hold that implements the same
  Core `IHold` contract (`Assets/_Project/Code/Core/Economy/IHold.cs`) as the boat's `ShipHold`, with
  a tunable **20-unit capacity** and gating behind owning `gear.bucket`. This is genuinely the *seed*
  of the container model (§4.2): a carried, capacity-limited, gear-gated container that the dig
  interaction fills and the stall empties. **But** `IHold` is a **flat item count** (`CapacityUnits`
  / `UsedUnits` / `TryAdd`), not a **size-based fullscreen grid**, and it does not nest or declare
  accepted types. The target model is a superset of what's here.
- **Gear is a flat owned-id list, not "two hands, one tool."** `PlayerGear`
  (`Assets/_Project/Code/Player/PlayerGear.cs`) maps a flat list of owned gear ids
  (`SaveData.OwnedGear` — shovel / bucket / rod) to boolean *capabilities* (can dig, has bucket, can
  rod-fish). There is **no notion of holding one tool at a time, grabbing, or putting back** — owning
  the shovel simply enables digging anywhere. The §4.1 "two hands, one tool" model is not yet
  represented.
- **Buy and sell are functional list menus.** `BuyScreen` (`Assets/_Project/Code/Economy/BuyScreen.cs`)
  and `SellScreen` (`Assets/_Project/Code/Economy/SellScreen.cs`) are code-driven overlays: a list of
  offers with name / price / description / Confirm, or a sell flow with a marginal-price read. They
  are *menus*, exactly as §8 concedes trade will functionally be — they are simply not yet dressed as
  *conversation*.
- **The "flood making — head in" signal already exists as a seam, not yet as a cue.**
  `OnFootWaterStateChanged` (`Assets/_Project/Code/Core/Events/WadingSignals.cs`) fires on a genuine
  dry↔wade↔swim transition and is explicitly documented as *"nothing consumes it yet — the HUD
  warning + splash VFX are follow-ups."* Today the intended consumer is a **HUD text warning**. The
  target (§7.4) is that this becomes a **diegetic cue** — water visibly at the knees + a sound —
  rather than HUD text. The seam is already the right shape to drive either.
- **The save schema doesn't yet cover world-placed objects.** ADR 0008's `SaveData` records
  `Money`, `OwnedBoats[] + ActiveHullId`, `OwnedGear[]`, and flags — **not** the positions or
  contents of items/containers set down in the world. World persistence (§4.3) is net-new save state.

### 6.2 The relationship, stated once

- The **always-on HUD** and the **buy/sell menus** are **M1 scaffolding, not canon.** They will be
  **reworked** — the HUD into instrument-gated readouts (§3), the menus into structured conversation
  (§8) — in **M2/M3**.
- **Do not tear down M1 to chase this.** The diegetic model is the **shape the game grows into**, not
  a demand to rebuild the vertical slice. M1 finishing *as-is* is correct (§7.1). The existing seams
  (`IHold`, `PlayerGear`, `OnFootWaterStateChanged`, the vendor buy/sell flows) are, encouragingly,
  the *right seams* to grow from — the container grid is a richer `IHold`, the conversation is a
  reskin over the vendor flow, and the wade cue is a new consumer of an event that already fires.

---

## 7. Phasing (tied to the roadmap)

This direction is **M2/M3 work**. It must not jump the queue ahead of finishing M1 (roadmap §0,
canon §7). The sequence:

### 7.1 M1 — finish as-is (no change from this doc)

M1 ships with the **always-on HUD** and the **buy/sell menus** it has. This doc changes *nothing*
about M1's build order or acceptance. The slice's job is to prove the loop is fun; the scaffolding
serves that job. This doc simply records where M1 evolves *to*, so the direction isn't lost.

### 7.2 The first proof — the watch-gated clock (early M2, alongside St Peters)

The cheapest, highest-signal first step (§3.3): **hide the clock at the St Peters start and sell a
watch that reveals the time and date.** This is a small, self-contained change that:

- proves the instrument-gated-HUD rule with one legible instance,
- fits naturally into the ratified St Peters opening (canon §5.8) — hand-work first, the world
  lighting up as you earn tools,
- and de-risks the whole direction before we invest in the full instrument ladder.

It is the recommended *first* diegetic-UI task, done as part of the M2 St Peters greybox.

### 7.3 M2/M3 — the full systems

Built in roadmap order, only as their milestone arrives:

- **M2:** the physical-inventory + container model (the fullscreen size-grid, nesting-with-taxonomy,
  world persistence), the first rungs of the storage/transport ladder (bucket → rack → bag → cart),
  more instruments gated in (compass, tide almanac, barometer, wind gauge), and the merchant
  conversation (§8) replacing the raw list menus. World persistence lands with its ADR (§9). This
  aligns with M2 being where the working coast, danger, storage, and the St Peters opening live
  (roadmap §M2).
- **M3:** the higher instruments (depth finder / sounder, GPS, radar, night vision) as the offshore
  fleet demands them, the top of the logistics ladder (boat holds at scale, and the hand-hauling →
  hired-hauler automation seam into the staff layer), tying the physical ladder into the
  economy/automation systems M3 introduces (roadmap §M3).

### 7.4 The wading cue becomes diegetic

The just-shipped **"flood making — head in"** signal (`OnFootWaterStateChanged`, §6.1) is, today,
intended to drive a **HUD text warning**. In the diegetic direction it eventually becomes a
**diegetic cue**: the water visibly rising to the player's **knees**, plus a **sound** (the wade
splash, rising water) — the player *reads the world*, not a text label. The seam already exists and
carries everything a cue needs (band, direction, depth); this is a later reskin of the *consumer*,
owned across ui-ux / audio / art-pipeline, not a change to the seam.

---

## 8. Merchants as conversation (menus, honestly)

Buying and selling should feel like **talking to a person on a working coast**, not selecting a
quantity from a dropdown. The direction is that trade is **dialogue** — you ask what's for sale, you
haggle or agree, you hand over clams and take coin — dressed in the fiction of the town's people
(**P3**).

**The honest concession:** a trade screen is, functionally, **still a menu.** You are choosing items
and amounts, and pretending otherwise would be dishonest and would hurt usability. The bar is not
"no menu" — the bar is:

> **The conversation is *structured well* so it's fast and easy to navigate.** Good UX, not
> anti-menu dogma.

So the target is a merchant interaction that is *diegetically dressed as conversation* (a person, a
stall, natural back-and-forth) but is, underneath, a **tight, well-structured menu**: quick to read,
quick to confirm, no fumbling. The existing `BuyScreen` / `SellScreen` (§6.1) are the functional
core; the M2/M3 work is to wrap them in conversation and make the flow feel like dealing with a
character, while keeping the speed of a good menu. The economic model underneath — supply/demand,
slicing a big lot down your own demand curve, freshness — is unchanged and remains owned by
[`economy-and-business.md`](economy-and-business.md); this doc reframes only the *interaction's
skin and structure*.

---

## 9. Open questions / deferred decisions (each becomes an ADR when built)

This is a *direction*, not a finished spec. The following are deliberately unresolved and will be
decided — and where they touch data or save format, recorded as their own **ADR** — when their
milestone arrives. Naming them here keeps the vision honest about what is decided vs. still forming.

1. **The container grid & item-size rules (data model).** Exact grid dimensions per container, item
   footprints (cells per object), rotation, and how the size-grid relates to the existing `IHold`
   count contract. *Becomes the inventory data-model ADR.*
2. **Nesting depth & the type taxonomy.** How deep nesting goes (bag → wallet → cash is two levels;
   is there a hard cap?), the full list of container **types** and what each accepts, and how the
   taxonomy is authored as data (ADR 0003 — content is data). *Part of the inventory ADR.*
3. **World-persistence save format.** How world-placed items and containers (positions + contents)
   are persisted, extending ADR 0008's schema, with a migration and an old-save load test. *Its own
   save-schema ADR — flagged in §4.3.*
4. **The instrument ↔ readout mapping (the gated-HUD list).** The authoritative list of instruments,
   which readout each gates, boat-fitted vs hand-carried, and how the HUD is driven off "owned
   instruments" rather than always-on. Coordinated with
   [`progression-and-housing.md`](progression-and-housing.md) (unlock graph) and
   [`boats-and-navigation.md`](boats-and-navigation.md). The **watch-gated clock** (§7.2) is the
   first concrete instance and can prototype the pattern.
5. **The trade-conversation grammar.** How the merchant conversation is *structured* (the dialogue
   shape that stays fast), how it reskins the existing vendor buy/sell flows, and where it sits
   relative to the dialogue system. Coordinated with
   [`economy-and-business.md`](economy-and-business.md) and the dialogue work.
6. **Over-encumbrance teeth (deferred).** Whether/where a situational **slowing** is added on top of
   the "full = no pickup" baseline (§4.2), tuned to stay cozy-with-teeth, not punishing (**P5**).
7. **How much reads stay felt-only.** The line between "you feel it" and "you can read it" per force
   (§3.4) — e.g. is there ever a *coarse* felt read of a force before you own its instrument, or is
   it strictly binary? A P1 tuning question.

> **Specific implementation decisions become ADRs when built.** The inventory data model (items 1–2)
> and the world-persistence save format (item 3) in particular are architectural/save-touching and
> will each get their own ADR under [`../adr/`](../adr/), reviewed by lead-architect, at the point
> they are implemented — not before. This doc is the *why*; the ADRs will be the *how*.

---

## 10. Cross-references — what this doc reframes

This direction sits on top of the existing design docs and, in a few places, **reframes** them. Those
docs remain the owners of their mechanics; this doc changes how the player *experiences* them.

- **[`ux-and-mobile-controls.md`](ux-and-mobile-controls.md) — reframed.** That doc specifies an
  **always-glanceable HUD** (§4: clock/tide/wind/sea/compass always visible) and **list-based
  inventory / market screens** (§5.1 inventory grids, §5.2 the sell flow). This doc **gates those HUD
  readouts behind owned instruments** (§3) and **turns the market screen into conversation** (§8) and
  the **inventory into a physical container grid** (§4). Treat `ux-and-mobile-controls.md` as the
  spec for *layout, input, accessibility, and the M1 scaffolding*, and this doc as the *diegetic
  direction those elements evolve toward in M2/M3*. Where they disagree on "always-on vs. gated," this
  doc is the ratified future direction; the UX doc is the current build. (The accessibility
  requirements in that doc — redundant coding, colourblind-safe reads, reduce-motion — carry over
  unchanged and apply to every instrument readout and container UI.)
- **[`economy-and-business.md`](economy-and-business.md) — reframed at the surface, unchanged
  underneath.** The supply/demand market, slicing, freshness, and storage-to-time-sales are unchanged
  (§8); this doc reskins the *buy/sell interaction* as conversation and folds *storage* into the
  physical container ladder (§5.3).
- **[`progression-and-housing.md`](progression-and-housing.md) — reframed as physical.** The storage
  ladder (sea-chest, warehouses) and the unlock graph become the **top of the physical
  container/instrument ladder** (§5.3); instruments join the unlock currencies as gated rewards (§3).
- **[`boats-and-navigation.md`](boats-and-navigation.md).** The boat hold is the largest container
  (§5.1); boat-fitted instruments are part of the gated-HUD list (§3.2).
- **[`art-and-audio-bible.md`](art-and-audio-bible.md).** Every carried item and container is a
  hand-crafted pixel-art object (§4.2); the diegetic wade cue (§7.4) is water-at-the-knees VFX + a
  sound.
- **Pillars.** Keystone P1 (mystery until you earn the instruments, §3), P2 & P4 (progression is
  capacity you can point at, §5), P5 (cozy tactility + gentle teeth, §4/§9). Canon
  [`../vision-and-pillars.md`](../vision-and-pillars.md) wins on any conflict.
