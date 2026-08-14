# Hidden Harbours — Diegetic Devices (the calendar, the notebook, the phone, the computer)

> **Status: DESIGN DIRECTION — owner-directed 2026-08-14; details forming; NOT yet built.** This is a
> *capture* document, not an implementation spec and not a scope commitment. It records a new owner
> direction — that the in-game UI grows a suite of **diegetic devices** — reconciles it against the
> already-ratified UI doctrine and against what the build actually contains today, and hands the owner
> a short list of **rulings** (§10) that unblock the design. Nothing here authorises out-of-phase
> construction (CLAUDE.md rule 8); proposed work is listed as **PROPOSALS** in §11 and is neither
> claimed nor scheduled.
>
> Design module. Subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md) (CANON — wins on
> conflict) and to [`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) (the **ratified UI
> doctrine** this must serve: information is an earned instrument). Where this direction and that one
> genuinely cannot both hold, the conflict is written down in §10 for the owner to rule — **it is not
> resolved here**, and a ratified direction is never quietly designed away.
>
> Sibling docs: [`dialogue-and-knowledge.md`](dialogue-and-knowledge.md) (the ratified law that
> knowledge lives in **things and people** — and the doc that already named cellphones and computers),
> [`diegetic-instruments-and-consoles.md`](diegetic-instruments-and-consoles.md) (the built instrument
> arc, and the carried-vs-fitted ruling this direction reopens),
> [`ux-and-mobile-controls.md`](ux-and-mobile-controls.md) (HUD, screens, accessibility — the layout
> and input spec every device must obey), [`time-tides-weather.md`](time-tides-weather.md) (the clock,
> weekday, moon and tide-table tiers the calendar and the tide app read),
> [`npcs-and-routines.md`](npcs-and-routines.md) (the shipped routine engine phone availability would
> read), [`economy-and-business.md`](economy-and-business.md) and
> [`progression-and-housing.md`](progression-and-housing.md) (the market and the property ladder the
> for-sale apps would list). ADRs touched if built:
> [`../adr/0008-save-schema-and-versioning.md`](../adr/0008-save-schema-and-versioning.md),
> [`../adr/0020-world-placed-object-persistence.md`](../adr/0020-world-placed-object-persistence.md)
> (the *store only irreducible facts* precedent), [`../adr/0025-ui-rig-runtime-rendering.md`](../adr/0025-ui-rig-runtime-rendering.md)
> (how a device's glass would draw), [`../adr/0030-per-hull-instrument-ownership.md`](../adr/0030-per-hull-instrument-ownership.md).

---

## 1. The owner's words (verbatim, 2026-08-14)

> The in-game UI grows a suite of diegetic devices:
>
> 1. A CALENDAR — a world object that enlarges to readable when interacted with.
> 2. A NOTEBOOK — the quest guide: tracks tasks given by NPCs; has TABS for help areas that explain
>    how to complete certain tasks, gameplay, etc.
> 3. A MOBILE PHONE — contact NPCs by text or call; carries apps: world map (GPS), tide chart,
>    boats-for-sale, properties-for-sale.
> 4. COMPUTERS — the phone's features plus more.

This is a **development of a direction the owner has already ratified**, not a new one. Six weeks
earlier ([`dialogue-and-knowledge.md`](dialogue-and-knowledge.md) §1, 2026-07-30) the owner wrote:
*"Instead of menus there will be cellphones, computers, documents and the other npcs who contain
gameplay knowledge."* That doc ratified the devices in principle and explicitly left open *"who owns a
phone; what's on the buyer's computer; which knowledge is device vs person"* (§5 Q3). **This directive
answers part of that open question and opens a harder one** — which is §2.

---

## 2. The keystone tension (the central question of this doc)

Two owner directions now point in opposite directions, and naming that plainly is this document's main
job.

**The ratified doctrine** ([`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) §3) is that
**information is an earned instrument**:

> *Every HUD readout is gated behind owning the instrument that produces it. No instrument, no readout.
> The player starts with nothing to read — not even the time of day.*

That rule is not an aspiration; it is the spine of a built arc. A watch sells you the clock
(`Core/Time/WatchFaceState.cs`, `Player/PlayerGear.HasWatch()`). A per-hull sounder sells you depth, a
fish finder sells you the school, a GPS sells you the plotted chart — all bought as `InstrumentOffer`
assets, bolted to one hull, and persisted at save schema v8 under
[ADR 0030](../adr/0030-per-hull-instrument-ownership.md). Ahead of it, **M2-27** sells charts *region by
region under fog-of-war* and grows the tide table's horizon in tiers.

**The new directive** puts a world map (GPS), a tide chart, a boats-for-sale listing and a
properties-for-sale listing in the player's pocket, on one object.

> ### The question
>
> **Is an app an instrument?**
>
> If yes — if the phone is a rung and each app is its own rung, priced, gated and coverage-limited —
> then both directions are true at once and the phone *strengthens* the ladder by adding rungs to it.
> If no — if the phone is a free convenience that arrives with a menu of everything — **the ladder is
> deleted**: the watch is redundant, the chartplotter is a worse phone, M2-27's buyable charts have
> nothing left to sell, and the sea stops being mysterious on the day you get a signal.

§3 proposes the resolutions that keep both true. Everything the resolutions cannot cover goes to §10
as a ruling, unresolved.

**The subtler risk, stated once.** A phone is a menu with a bezel. The anti-menu doctrine can be
satisfied in letter and broken in spirit — a pocket device that is always with you and always works
*is* the spreadsheet floating over the world, just skinned. The proposed test:

> **A device earns its place when it can be somewhere you are not, or can be off.**

The calendar passes because it hangs on a wall. The computer passes because it sits on a desk. The
phone passes only if something can take it away from you — which is what §3.2 (coverage) is for.

---

## 3. Proposed resolutions (how both directions stay true)

These are proposals. Each is designed so that **nothing already ratified has to be given up**.

### 3.1 An app IS an instrument (the ladder grows, it does not collapse)

The phone is a **purchase**, and each app is a **separate purchase or unlock** on the same ladder as
the watch and the sounder. Buying the phone buys you a *slate*, not the information on it. The
progression reads exactly like the instrument arc already reads:

| Rung | What it grants | Why it is a rung and not a freebie |
|---|---|---|
| **The phone itself** | Contacts you already have; nothing else | The object, empty — the same beat as owning a console with a bare brow |
| **Tide chart app** | The almanac page, in your pocket, at its own tier | Competes with the paper page on *horizon and coverage*, not on existence (§4.2) |
| **Map app** | Your position, on the chart knowledge you already own | Fog-of-war and owned charts still bind it (§4.1) |
| **Boats-for-sale app** | Listings at ports you have visited | The dynasty ladder made legible — P2's "long, *legible* ladder" (§4.4) |
| **Properties-for-sale app** | The same, for buildings and homes | (§4.4) |

The project already has the two purchase shapes this needs, and the split is already documented:
`GearOffer` is *"a presence-only wallet you carry between boats"*, `InstrumentOffer` is *"bolted into
one hull"* ([`diegetic-instruments-and-consoles.md`](diegetic-instruments-and-consoles.md) §4). A phone
is carried, so it wants the `GearOffer` shape — **which reopens a standing ruling**, see §10 R2.

An app, though, is neither: it is a capability *on a carried object*. The cheapest honest shape is that
apps ride the existing owned-gear list as their own ids (`gear.phone`, `app.tides`, `app.chart`,
`app.boats`, `app.property`), the way `gear.watch` already rides `SaveData.OwnedGear` with **no
save-schema change at all**. That is a proposal, not a decision; it is architecture, so it would be a
lead-architect call at build time.

### 3.2 Coverage — the signal fades offshore, so the sea keeps its moods (P1)

The single most load-bearing mechanic in this whole direction:

> **Signal is a coverage field over the world, and it runs out.** Strong in Nine Mile Creek and
> Finnigan's Landing; patchy at St Peters and East Point; **gone** on The Banks, at Ironbound, and in
> The Smother.

Everything that needs the network — texts, calls, the for-sale listings, any live forecast — **goes
dead where the game gets dangerous.** So at the exact moment the sea is trying to kill you, the things
that still work are the ones you bolted to your boat and the paper in your pocket: the barometer, the
radio (M2-09), the fitted sounder and chartplotter, the almanac page. **The offshore end of the
instrument ladder is not merely preserved — it becomes the reason you climbed it.** That is P1 and P5
in one mechanic, and it costs no new simulation: coverage is a static authored value per region (or a
distance-from-port falloff), deterministic and unsaved like every other environmental read (rule 5).

**GPS is the honest exception, and it is a feature.** A real GPS receiver needs no network — so a
coverage rule cannot switch it off without lying. Two honest answers, recommendation first:

- **(a) — recommended — the phone gives you a FIX, the plotter gives you the CHART.** Offshore, the map
  app still knows *where you are* and shows it on a coarse coastline. What it cannot show is the
  **survey**: soundings, banded depth, hazards, your waypoints, your route, your track. Those are the
  chartplotter's, and the chartplotter is a bought instrument bolted to a hull. This is technically
  true to how the devices actually differ, it keeps the plotter obviously worth buying, and it gives
  the phone a genuinely useful-but-insufficient role — the best kind.
- **(b) the map app is a network map** (tiles fetched over the air) and simply dies with the signal.
  Simpler, and also defensible for the era; but it makes the phone feel arbitrary offshore.

### 3.3 Era and price

The world already carries outboards, freezers, a gas pump, a VHF radio (M2-09) and a fish plant;
[`dialogue-and-knowledge.md`](dialogue-and-knowledge.md) §3 already ruled *"the era allows them."* The
open questions are **which** era of phone (a flip phone with three apps reads very differently from a
smartphone) and **where on the money ladder** it sits. Design preference: the phone should land *after*
the watch and *around* the punt/Cape rung — late enough that it feels like a step up in the business,
early enough that the calls have a town to reach. Both are owner taste (§10 R6).

### 3.4 Computers are the late rung, and there are two kinds

"The phone's features plus more" most plausibly means **M3-11** — the dashboard-first,
manage-by-exception management UIs ([`ux-and-mobile-controls.md`](ux-and-mobile-controls.md) §5.3):
today's net, properties, staff, routes, contracts, alerts. Sited on a desk in your house or your
office, that whole screen stops being a menu and becomes a device you sit down at, which is precisely
the anti-menu test in [`dialogue-and-knowledge.md`](dialogue-and-knowledge.md) §3. It is also a clean
**P4** beat: you hauled by hand, and now you sit down and run it.

And there is a second kind the ratified doc already asked about — *"what's on the buyer's computer"*
(§5 Q3). **Other people's computers are knowledge you go and ask for**, needing no ownership at all:
the buyer's price history, the harbourmaster's berth book, the yard's build queue. That is P3, and it
is much cheaper than it sounds — a computer you don't own is a document with a screen.

Computers are **fixtures, never carried**, which also resolves cleanly against the carried/fitted line
in §10 R2: watch and phone carried, console fitted, computer sited.

---

## 4. Reconciling with what exists or is planned — by name

This section is the honest reconciliation the direction needs. Nothing below is speculation about the
build; every claim is checked against the code or the doc cited.

### 4.1 The map app vs. M2-27's chart, fog-of-war and buyable charts — and the *shipped* chartplotter

**What is already built, which the brief for this doc did not assume:** the **GPS chartplotter has
shipped** (ADR 0025 S6). `UI/ChartplotterOverlayHost.cs` draws the real surveyed seabed under the boat
with the player's own waypoints, route and track, in the pilothouse brow's GPS slot — *"while the player
pilots a hull whose effective fit carries a GPS."* Its survey comes from `UI/Draw/NavChartSource.cs`,
sampled from `ITidalTerrain.ElevationAt` — *"the SAME height map the water shader renders, the tide
exposes and the depth sounder reads. One height map, one truth."* Waypoints, route and track persist
through `NavLocker` (`SaveData.NavWaypoints` / `NavRoute` / `NavTrack`).

**What is not built:** there is no fog-of-war anywhere in the codebase, and no region-by-region chart
ownership. M2-27 (*"Nautical chart, discovered-by-presence reveal, buyable charts, table tiers"*) is
unstarted. Today the chartplotter shows the whole current region's survey to anyone who owns a GPS.

**The proposed seam — one chart knowledge, many viewers.** Chart knowledge (which regions you have
sailed, which charts you have bought, which hazards are revealed) becomes **one model**, and the phone
map, the fitted chartplotter, and any paper chart are **three presentations of it**. Uncharted stays
uncharted on every screen. This is not a new invention — it is the discipline this codebase already
applies twice:

- the almanac's *"No forked maths"*: `TideAlmanac.FindTurns` walks the same `TideReadout.Derive` the
  HUD gauge uses, so *"owner tool, player page, HUD gauge and Tide Scrubber cannot drift apart"*;
- the plotter's *"One raster, two presentations"*: the flush face and the expanded card are the same
  texture at two rects, *"so they cannot disagree about where the boat is."*

A phone map that forked its own world model would be the first thing in this project allowed to lie
about the sea. It must not be.

### 4.2 The tide chart app vs. the shipped almanac page (#355) and the tide-table tiers

**What is already built.** `UI/TidePanel.cs` is the tide table the player reads — *"It is paper, not a
gauge… drawn as the almanac page it would be: warm stock, ruled columns, ink."* It freezes the world
through `IGameClock.IsPaused` (*"the project's one pause path"*), is built once on open and does no
per-frame work, computes every value through `IEnvironmentService.TideHeightAt` and stores nothing. It
is ruled for **two day-columns** (`MaxColumns = 2`) for the **active region only**. It opens on `N`
(`UI/TidePanelInput.cs`), and `TidePanel.Open()` is deliberately public and argument-free *"so a
diegetic world interaction… can call it later with no change here."*

**And it is not gated.** PR #355 shipped it free and flagged that to the owner in as many words:

> *"⚠️ Flagged for the owner — should the table be an earned instrument? … Per the handoff I've shipped
> it freely available and not wired any store dependency. Gating it later is a data change (a
> licence/almanac Def plus one ownership check at `TidePanel.Open`), not a rework. Your call."*

**That question was never answered, and the tide app now makes it load-bearing** — because the app puts
the same page in your pocket, and if both are free then M2-27's tide-table tiers have nothing left to
tier. §10 R3.

**The proposed reconciliation.** The app is a **fourth reader of the one turn-finder**, never a second
maths. What separates the rungs is **horizon and coverage**, which is exactly where
[`time-tides-weather.md`](time-tides-weather.md) §3.6 already puts the tiers (Tier 0 booklet: today + 1
day, two regions → Tier 2 harbourmaster's charts: 28 days, all known regions). The app is simply *a
tier with a screen*. Where it sits on that ladder is content, and it is the owner's.

### 4.3 The phone as forecast source vs. M2-09's barometer / harbourmaster / radio

M2-09 is *"Three escalating instruments give foresight — barometer trend reliable; harbourmaster
~24–48h; radio live at sea."* [`time-tides-weather.md`](time-tides-weather.md) §Principle 2 makes this
canon: *"The signs come before the event… the forecast tools (tide table, barometer, harbourmaster,
radio) are how the player earns foresight."*

A weather app would flatten all three into one free readout, and it is the clearest case where the
directive could quietly delete a pillar mechanic. **Note that the owner's list does not include a
weather app** — the four named apps are map, tide chart, boats-for-sale, properties-for-sale. So the
proposal is simply: **don't add one.** Weather foresight stays with the three escalating instruments,
and the phone's honest weather role is a *social* one — you **ring the harbourmaster** and he tells you
(§4.5), which is the harbourmaster's own canon service hook
([`npcs-and-routines.md`](npcs-and-routines.md) §3.3) delivered over the wire instead of in person, and
still limited by whether he is at his desk and whether you have a signal. That serves P3 and costs P1
nothing.

### 4.4 The boats/properties apps vs. the shipwright buy flow and M2-42

**What is already built.** A boat for sale is a `ShipwrightOffer` asset (`BoatId`, `DisplayName`,
`Price`, `StartsDamaged`, `RepairCost`) — *"add a boat to the showroom by creating one of these assets,
never by hard-coding a price."* `Economy/Shipwright.cs` checks the price, spends through `IWallet`, and
raises Core `BoatPurchased`; the damaged→repair path raises `BoatRepaired` and writes `RepairLedger`.
Buying a *building* is **M2-42** (*"Purchase from the ladder, site it on wharf frontage, name it"* —
*"Siting uses the M2-39 interact verb; persists on the ADR 0020 pattern"*), over `WharfBuildingDef`
(M2-41). Buying or leasing a **property** is [`progression-and-housing.md`](progression-and-housing.md)
(*"buy outright (cash) or lease (rent/day) where offered"*).

**The proposed seam, and it is the answer to the brief's question:** the apps are a **browse-remotely
layer over exactly the same offer data** — the same `ShipwrightOffer` / `WharfBuildingDef` assets, read
into a catalogue view. Content is never forked; a boat listed on the phone and a boat in the yard are
one asset (rule 2).

But the seam has a second half, and it matters more:

> **Browsing is remote. Transacting is not.**

You see the Cape Islander listed at East Point, with her price and her stats; **you still have to go
there, look at her, and buy her from a person at a yard.** Three reasons this is the right line, not a
timid one:

- **P3.** Canon §5.3 rules that *"a yard is a commercial business with a local name… each has an
  interior and a working yard (boats on supports under repair, perhaps boats for sale)."* An app that
  completes the sale makes every one of those yards a room nobody enters.
- **P2/P4.** The dynasty ladder is supposed to be *legible* (canon P2: *"a long, legible ladder of
  scale"*). The app is the thing that makes the rung above you legible — you can see what a Cape
  Islander costs long before you can afford her. That is the app doing real pillar work.
- **It is what an app is actually for.** Saving you a wasted trip across the bay is a genuine,
  era-appropriate convenience. Closing a boat deal from your couch is not.

> **⚠️ This is the one place a device risks serving NO pillar.** A for-sale app that also transacts is
> pure convenience: it serves no pillar, and it actively costs **P3** by emptying the yards and the
> shops. Under CLAUDE.md §1 (*"Every change must serve at least one pillar. If it serves none, don't
> build it"*) that version should not be built. The browse-remotely/buy-in-person split is what turns
> the same feature into a P2 feature. §10 R5 is the ruling.

**A consistency bonus, offered as an option:** you see listings only for ports you have **been to** —
the same discovered-by-presence rule M2-27 applies to the chart. One rule, two systems, and your phone
grows with your reach.

### 4.5 Calls and texts vs. M2-23/M2-24 (routines and relationships)

**What is already built, precisely.** M2-23 phase 1 shipped on 2026-08-12 and
[`npcs-and-routines.md`](npcs-and-routines.md) §2.6 is its as-built record. It is *"DATA plus one pure
function"*: `RoutineDef`/`RoutineEntry` assets under `Data/Routines`, `RoutineStations`, `RoutineLanes`,
and a pure `RoutineSchedule`/`RoutinePlan` whose `SampleAt(hour)` is *"pure and allocation-free."* A
villager's position **is** `f(worldSeed, hourOfDay)`; nothing ticks and **nothing is saved**. Six St
Peters villagers live one full day on it. Four activity tags exist, append-only: `Home`, `Work`,
`Errand`, `Recreation`.

**Not shipped:** conditional schedules and every reactivity rule (weather, tide, season, day-of-week,
story flags); the relationship layer of §5 entirely. M2-24 (dialogue v2 + relationships) is unstarted.

> **Premise correction, for the record.** This doc's brief cited **#524** as the routines/relationships
> reference. #524 is *"the villagers tread the grass — a footstep each, at the rank below the player"* —
> a `GrassFootstep` wiring PR. It touches the St Peters villagers, but the routines record is
> [`npcs-and-routines.md`](npcs-and-routines.md) §2.6 and the M2-23 shipping note, which is what this
> section is grounded in.

**The proposed availability model — a pure read, not a routines redesign.** "Can I reach this person
right now?" is *already computable* from the shipped engine: sample their `RoutinePlan` at the current
hour and look at the activity and the station. So availability is a **pure predicate over data
world-content already authors**, which keeps it in the right lane (`World/` is world-content's per
`agents/coordination.md` §1) and costs no new state.

| Where they are | What the phone does |
|---|---|
| **Home**, **Work** | They answer. The harbourmaster at his desk, Ginny at the hearth. |
| **Errand**, **Recreation** | They may answer, briefly, or not — a per-person trait, not a global rule. |
| **At sea / away** | **Nobody answers.** It rings out. |

> **⚠️ The "at sea" case is not representable today.** There is no shipped activity tag meaning *out on
> the water*, and no NPC-fleet-at-sea agent model (M2-19 is an *aggregate* landings curve, not agents).
> The honest proposal is an **append-only fifth tag** (`AtSea` / `Away`) that world-content adds when it
> authors a fisher's day — an append to an append-only enum, exactly the discipline §2.6 describes for
> growing that data. **This is a proposal for world-content, not a redesign by ui-ux.**

**A fisher who doesn't answer is the best P3 beat in this whole direction.** The coast runs on its own
rhythms with or without you (canon P3), and a phone that always connects would be the single loudest
denial of that. A phone that rings out at 05:40 because Wally is already on the water *proves* the
world is there. Leave a message; he rings back when his day brings him ashore.

**Relationship gating (M2-24, when it exists).** You can only ring people whose **number you have**, and
you get a number by meeting them and earning it. Your phone book becomes a legible read of your
standing in the town — a progression surface that costs one contact list and no new system.

**⚠️ One real presentation conflict.** [`dialogue-and-knowledge.md`](dialogue-and-knowledge.md) §2 is
ratified: *"No portrait dialogue boxes, ever… The speech bubble anchors at the speaker, in the world…
The character moves while speaking… The sound IS the bubble populating."* **A phone call has no body to
hang any of that on.** The speaker is not in the scene; there is nothing to face, nothing to animate,
and the populate-sound has no bubble. Proposal: **texts are the default** — a thread on the phone's
screen, which is a *document*, a form the ratified doctrine already blesses — and **voice calls are
reserved** for the few things a text can't carry (a tow request, the harbourmaster's read on the
weather), drawn as a minimal call screen with the speaker's name and a handset. That keeps the ratified
bubble model intact by not pretending a call is a face-to-face conversation. §10 R7.

### 4.6 The calendar vs. the watch, and what the clock already knows

`IGameClock` already exposes everything a calendar page needs, and `Core/Time/WatchFaceState.cs` proves
it: `Weekday` (Monday = 0), `DayOfSeason` (1–28), `Season`, `Year`, `IsMarketDay`. The watch mapper's
own note is the warning to heed — *"do not re-derive the weekday/market from an absolute-day index;
`clock.Weekday` and `clock.IsMarketDay` already are that."* Moon phase and the spring/neap envelope are
likewise already specified as pure functions of `gameTime`
([`time-tides-weather.md`](time-tides-weather.md) §3.3).

So the calendar reads existing state and derives nothing new. §5.1 covers what it shows.

---

## 5. The four devices

Each device below states what it is, how it opens, which pillars it serves, and what it would cost.

### 5.1 The CALENDAR — the cheapest win, and it is genuinely cheap

**What it is.** A wall calendar — in the cottage, the general store, the harbourmaster's office — that
you walk up to and read.

**What it shows** (all of it already derivable, §4.6):

| Read | Source | Why it's on the page |
|---|---|---|
| Date: weekday · day of season · season · year | `IGameClock.Weekday/DayOfSeason/Season/Year` | The basic read |
| **Market Day** | `IGameClock.IsMarketDay` (Friday) | Canon §5.8: the town's week is six working days + one rest day, one of which is Market Day at Nine Mile Creek. **The market is a reason to plan** |
| **Rest day** | the same weekday model | M2-23's next phase keys routines off the weekday; the calendar is where the player *learns* the town's week |
| **Moon phase, and the spring/neap band** | `moonAge` / `springNeap01`, [`time-tides-weather.md`](time-tides-weather.md) §3.3 | The highest-value read on the page. *"Spring tides are flagged by the moon phase"* — a player who learns *springs come at new and full* has learned to plan a flats trip a week out. Pure **P1 literacy** |
| Season boundaries | 4 × 28 days | When the water changes |
| *(later)* build/refit completion dates, contract deadlines, festivals | `progression-and-housing.md` stores these as `(nodeId, completeOnGameDate)`; festivals are Father Tomas's canon hook | Turns the calendar into the planning surface it should be |

**How it opens — reusing the two shipped patterns, inventing nothing.**

1. **The interaction** is M2-39's Core seam, already built and already the project's answer to *"a
   feature that wants a button registers a candidate and gets the existing interact press, contextually,
   with no new binding"* (`Core/Interaction/InteractVerb.cs`). The calendar registers an
   `IInteractable` (`Core/Interaction/IInteractable.cs`) with `Priority = InteractPriority.Fixture`,
   `Contexts = InteractContext.OnFoot`, `RequiresFacing = true`, an id on the documented convention
   (`fixture.st_peters.calendar`), and an `Interact()` that opens the page. **No new key.**
2. **The page** is `TidePanel`'s recipe, line for line: a self-building code-driven overlay that freezes
   the world through `IGameClock.IsPaused` (restoring whatever it found — *"a page opened during an
   already-paused moment does not resume the world on close"*), builds once, does no per-frame work,
   and closes on Esc / gamepad East. Redundant coding per
   [`ux-and-mobile-controls.md`](ux-and-mobile-controls.md) §8: a moon *glyph* **and** the words, never
   colour alone — the same rule `TidePanel` follows for high/low water.

**Both halves already exist.** This is why the calendar is the cheapest of the four: it needs no new
interaction system, no new pause path, no new page recipe, and no new clock maths.

**Free furniture, or bought?** Recommendation: **free, fixed furniture.** It does not undercut the
watch, because the two divide cleanly — **the watch is portable and now; the calendar is fixed and
ahead.** Without a watch you must *walk home to find out what day it is*, which is a lovely early-game
texture and exactly the P1/P4 shape the doctrine wants. §10 R8 if the owner disagrees.

**Pillars: P1** (moon → springs → planning), **P3** (market day and rest day are the town's rhythm),
**P5** (planning a crossing is how the teeth stay fair).

### 5.2 The NOTEBOOK — the deepest new system by a wide margin

#### 5.2.1 There is no quest system today. None.

Checked, not assumed:

- **Onboarding is flags plus one hint label.** `World/OnboardingDirector.cs` walks a seven-beat opening
  by subscribing to Core signals already on the bus — `FishCaught`, `LicensePurchased`, `GearPurchased`,
  `BoatPurchased`, `BoatRepaired` — and showing a single line of text at the bottom of the screen. Its
  own summary: *"Deliberately minimal: no quests, no routines (that's M2), just one self-dismissing
  nudge."*
- **State is three booleans.** `World/OnboardingFlags.cs` names `met_ginny`, `read_logbook`,
  `onboarded` over an `IFlagStore`; `World/SaveFlagStore.cs` delegates them to
  `GameServices.Save.GetFlag/SetFlag`, so they land in `SaveData.OnboardingFlags` (a `List<SaveFlag>`)
  *"in the same save slot as money/time/fleet"*.
- **Dialogue is linear lines, with no choices and no grants.** `World/DialogueDef.cs` carries
  `FirstLines` / `RepeatLines` / `ConditionalLines` gated by one `ConditionalFlag`;
  `World/DialoguePresenter.cs` is *"a pure view"* driven by `WorldInteractor`, sequenced by
  `DialogueRunner`. There is no option picker (that is ratified but unbuilt —
  [`dialogue-and-knowledge.md`](dialogue-and-knowledge.md) §2) and no way for a conversation to hand
  anything to anything.
- **The 14 questlines in [`npcs-and-routines.md`](npcs-and-routines.md) §3 are hooks**, all unbuilt.
- **`SaveData` records no task state**: money, seed, time, day, owned boats + active hull, onboarding
  flags, licences, repaired boats, owned gear, placed traps, bait/pot/supply stock, held catch, per-hull
  instruments and their prefs, nav waypoints/route/track. Nothing about a task anyone gave you.

**So the notebook is not a UI over an existing system. It is the system, plus a UI.** That is why it is
the deepest piece here and why it is the one that touches the save.

#### 5.2.2 A task, as DATA (rule 2)

One asset per task, `Data/Tasks/`, stable append-only id `task.snake_case` — the same shape as every
other content type in this project. Sketch, not a schema:

| Field | What it is |
|---|---|
| `Id` | `task.ginny_first_clams` — stable, append-only |
| `Title`, `Summary` | What the notebook prints. Plain copy now; the `WorldStrings`/`DialogueDef` localization seam later |
| `GiverNpcId` | The giver **as an id string**, not a reference — `NpcDef` lives in `World/`, and an id keeps the task data reachable from any lane without a cross-module ref (rule 4) |
| `Prerequisites[]` | Flag keys that must be set before this can be granted |
| `Steps[]` | Each: a stable step id, a line, and **one completion predicate** |
| `CompletionFlag` | The flag key set when the task closes — so other content (a `DialogueDef.ConditionalFlag`) can react to it with no new plumbing |

**The discipline that keeps it honest — and it is the whole design.** A step's completion predicate must
be **something the game already records or already announces**: a flag key read through `IFlagStore`
(exactly the seam `DialogueDef.ConditionalFlag` already uses to reach *"a flag another module persists…
as DATA rather than through a cross-module reference"*), or a Core EventBus signal that already fires.
The notebook then **reads** progress; it does not keep a second copy of it that can drift from the world.
That is the same *recompute-don't-store* discipline this project applies to tide, weather, routines and
building appearance — applied to progress.

#### 5.2.3 How dialogue hands a task to the notebook (rule 4 — through Core)

Additively, with today's behaviour preserved bit-for-bit when the new field is empty — the pattern
`DialogueDef.ConditionalFlag` itself established:

1. `DialogueDef` grows an optional **`GrantsTaskId`** (append-only; empty = exactly today's behaviour).
2. On conversation completion, `World` publishes a **Core signal — `TaskGranted(taskId, giverId)`** — on
   the `EventBus`, beside `LicensePurchased` and `BoatRepaired`.
3. The **notebook subscribes** (ui-ux lane). `World` never references the notebook; the notebook never
   references `DialogueDef`. A new Core event is exactly the *"propose a Core contract"* handoff
   `agents/coordination.md` §7 prescribes, and lead-architect reviews it.

> **The onboarding director is the prototype of the notebook, and saying so is the point.**
> `OnboardingDirector` is already a seven-step task list driven by Core signals and closed by one
> persisted flag. The notebook is **that, generalised into data** — and the St Peters opening becomes
> its first authored `TaskDef` rather than a bespoke C# ladder. That would retire the hint label, which
> overlaps **M2-31c** (the VS-21 inherited→earned-dory rework). **Flagged, not committed** (§10 R10).

#### 5.2.4 The TABS — and whether help pages are earned

[`dialogue-and-knowledge.md`](dialogue-and-knowledge.md) §3's law is that knowledge lives **in things
and people**, and its test for any information feature is *"would a menu do this?" — then it must
instead be a device, a document, or a person.*

**The notebook is a thing — and specifically a thing the player writes into.** That is compliant *only
if the pages are written by play*. A notebook that ships pre-printed with a full strategy guide is a
manual with a leather cover; it passes the letter of the law and breaks it entirely.

> **Recommendation: help pages are EARNED, and there are exactly two ways to earn one.**
>
> 1. **Someone teaches you.** A conversation grants a page — the same seam as a task
>    (`PageLearned(pageId)` on the bus). Ginny teaches you the tide. Iris teaches you the sounder.
>    Joachim teaches you where the legendary fish are said to run. **This makes the cast into the
>    manual**, which is P3 doing structural work rather than decorative work.
> 2. **You do it once.** The first clam dug writes the clam page. The first grounding writes the
>    tide-and-draught page — the page you wanted *ten minutes ago*, which is exactly the cozy-with-teeth
>    beat (**P5**): the sea taught you, and your notebook now says so in your handwriting.

**The honest counter-case, stated rather than buried:** a lost first-time player has nowhere to look.
Mitigation, and the proposed line:

- **Always present:** the **task list itself**, and a small **controls/verbs** section (what the interact
  key does, what the throttle does). These are not gameplay knowledge — they are the manual for the
  *controller*, and gating them is hostile, not diegetic.
- **Earned:** everything that is knowledge *about the world* — tides, grounds, prices, weather signs,
  gear, licences.

§10 R9 is where the owner draws that line.

#### 5.2.5 Save, and the ADR it needs

Task state — *granted*, *step done*, *closed* — is **irreducible**: you cannot recompute "Ginny asked
you to do this." It must persist. That is a save-schema extension, and on this project's standing
practice it is **its own ADR**, on the [ADR 0020](../adr/0020-world-placed-object-persistence.md)
pattern: *store only the irreducible facts, recompute the rest.* Everything else the notebook shows —
titles, step text, ordering, which pages exist — is recomputed from the `TaskDef` assets at load and is
never saved (rule 5). Do **not** assume the current schema covers this; §5.2.1 lists exactly what it
records, and none of it is a task.

**Pillars: P3** (tasks come from people; pages are taught by people), **P4** (the notebook is the record
of what you earned by hand), **P2** (it is where the ladder becomes legible).

### 5.3 The PHONE

Covered in §3 and §4. In summary:

| Piece | Shape | Pillar |
|---|---|---|
| The object | A bought, carried instrument — a slate, empty (§3.1) | P2 |
| Texts & calls | Availability read from the shipped routine plan; nobody answers from the water; texts default, calls reserved (§4.5) | **P3** |
| Coverage | Fades offshore; the network apps die where the sea gets dangerous (§3.2) | **P1**, P5 |
| Map (GPS) | A **fix**, not the survey; bound by the one chart-knowledge model and its fog-of-war (§3.2, §4.1) | P1, P2 |
| Tide chart | A fourth reader of the one turn-finder; its rung is horizon + coverage (§4.2) | P1 |
| Boats / properties for sale | Browse remotely over the same offer assets; **transact in person** (§4.4) | **P2/P4** |
| *(no weather app)* | Forecast stays with barometer / harbourmaster / radio (§4.3) | protects P1 |

### 5.4 COMPUTERS

Covered in §3.4. **Yours** is the M3-11 management dashboard given a desk — properties, staff, routes,
contracts, alerts, the ledger — the late rung, and the P4 moment where you stop doing it by hand.
**Theirs** is a knowledge surface you visit: the buyer's price history, the harbourmaster's berth book
(P3). A fixture in both cases, never carried.

---

## 6. Readability — why interact→enlarge is load-bearing, not decorative

Canon §5.2 locks **PPU = 32** and **1 world unit = 1 metre**, with humans at a slightly heroic ~1.8 m.
Run the arithmetic on a screen at gameplay zoom:

| Object | Real size | On screen at PPU 32 |
|---|---|---|
| A phone in a character's hand | ~0.14 m | **≈ 4 px** |
| A wall calendar | ~0.4 m | **≈ 13 px** |
| A notebook, open | ~0.3 m | **≈ 10 px** |
| A computer monitor | ~0.4 m | **≈ 13 px** |

**None of these can carry a single legible character at gameplay zoom.** The interact→enlarge pattern
in the owner's directive is therefore not a nicety — it is the *only* mechanism by which any of the four
devices can show text at all. It is load-bearing for all four, exactly as the directive says of the
calendar.

**And the project has already shipped two shapes for it. Devices should take one, not invent a third:**

- **The full page** (`UI/TidePanel.cs`) — an overlay that fills the screen, freezes the world, and is
  put away. Right for the **calendar**, the **notebook**, and the **computer**, all of which you *sit
  down with*.
- **The flush → expanded flip** (`UI/HelmInstrumentExpansion.cs`, ADR 0025 S4.5) — the owner's own
  earlier direction, *"shown on the dash and not blown up by default; this should be selectable, which
  UI can be expanded"*. One expanded at a time, collapsed by clicking away or Esc, **never persisted**
  (*"which view a player is looking through is where their eyes are, not a preference"*), and both
  states are *"the same texture at two rects, so they cannot disagree."* Right for the **phone**, which
  you glance at and then look properly at.

**Art dependency, filed honestly.** `docs/art/rigs/ui/` currently holds watch-face, compass,
chartplotter, radar, depth-finder, fish-finder, four helms, lever, tiller and wheel. **There is no
phone, calendar, notebook or computer rig.** All four are art-director asks (`docs/art/rigs/**` is
art-director's, per `agents/coordination.md` §1, and it is the one exception to the docs-ownership
rule). Per §7 of that doc, the right move at build time is a greybox placeholder plus a filed item —
never authoring in someone else's folder. Listed in §11.

**Accessibility carries over unchanged** ([`ux-and-mobile-controls.md`](ux-and-mobile-controls.md) §8,
which [`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) §10 confirms applies to *"every
instrument readout and container UI"*): redundant coding, never colour alone; S/M/L/XL text scaling;
high contrast. A pixel-art phone screen is the hardest legibility case in the project — harder than the
tide gauge — and it should get the same treatment the tide widget got: a legibility pass at the smallest
supported window size, across the colourblind palettes.

---

## 7. Pillars, per device (and where one serves none)

| Device | P1 | P2 | P3 | P4 | P5 |
|---|---|---|---|---|---|
| **Calendar** | ✔ moon → springs → planning | | ✔ market day, rest day | | ✔ planning keeps the teeth fair |
| **Notebook** | | ✔ the ladder made legible | ✔ people give tasks and teach pages | ✔ the record of what you earned | ✔ the page the sea wrote for you |
| **Phone** | ✔ coverage; earned apps | ✔ the rung above you, priced | ✔ people who don't answer | ✔ browse ≠ transact | ✔ no signal offshore |
| **Computers** | | ✔ | ✔ *their* computers | ✔ the owner's desk | |

> **The one thing here that would serve no pillar** is a for-sale app that also *completes the sale*
> (§4.4): pure convenience, no pillar served, and a live cost to **P3**. Under CLAUDE.md §1 that version
> should not be built. The browse-remotely/buy-in-person split is precisely what converts it into a P2
> feature.
>
> A **weather app** would be worse than pillar-neutral — it would actively delete M2-09's escalating
> foresight ladder. The owner's list doesn't ask for one (§4.3); the proposal is to keep it that way.

---

## 8. What exists today (grounded in the code)

Everything above rests on this; every line is a file or a doc, checked on 2026-08-14 at `main`
(`9a0fabf`).

| Claim | Where |
|---|---|
| **No quest/task system exists.** Onboarding is 3 flags + Core signals + one hint label | `Assets/_Project/Code/World/OnboardingDirector.cs`, `OnboardingFlags.cs`, `SaveFlagStore.cs` |
| **Dialogue is linear lines**, no options, no grants; one flag-gated conditional pool | `World/DialogueDef.cs`, `World/DialoguePresenter.cs`, `World/DialogueModel.cs` |
| **The save records no task state** (money, seed, time, boats, flags, licences, repairs, gear, traps, stock, catch, per-hull instruments + prefs, nav waypoints/route/track) | `Core/Save/SaveData.cs` |
| **The tide almanac page shipped, free and ungated**; paper, not a gauge; freezes the clock via the one pause path; two day-columns, active region; `Open()` is public and argument-free *for a future world interaction* | `UI/TidePanel.cs`, `UI/TideAlmanac.cs`, `UI/TidePanelInput.cs`; PR #355 |
| **PR #355 asked the owner whether the table should be gated. It was never answered** | PR #355 review notes |
| **The GPS chartplotter shipped** — per-hull, purchased, surveyed seabed + waypoints/route/track | `UI/ChartplotterOverlayHost.cs`, `UI/Draw/NavChartSource.cs`, `NavLocker`, ADR 0025 S6, ADR 0030 |
| **No fog-of-war exists anywhere**; no region-by-region chart ownership. M2-27 unstarted | codebase search; `backlog/backlog.md` M2-27 |
| **The interact verb exists and is the project's answer to "a feature that wants a button"** — registry + pure resolver + dispatch, all Core, no new key | `Core/Interaction/{IInteractable,Interactables,InteractResolver,InteractVerb}.cs`; M2-39 ◐ |
| **The flush→expanded pattern exists**, one at a time, never persisted, same texture at two rects | `UI/HelmInstrumentExpansion.cs`, `UI/HelmInstrumentMountLayout.cs`, ADR 0025 S4.5 |
| **The clock already knows the calendar**: `Weekday`, `DayOfSeason`, `Season`, `Year`, `IsMarketDay` | `Core/Time/Calendar.cs`, `Core/Time/WatchFaceState.cs` |
| **Routines shipped as a pure function** of `(worldSeed, hourOfDay)`; four append-only activity tags; nothing saved; no "at sea" tag; no relationship layer | `World/Routines/**`; `npcs-and-routines.md` §2.6 |
| **Boats are sold from data by a person**: `ShipwrightOffer` (+ damaged/repair), `IWallet`, Core `BoatPurchased`/`BoatRepaired` | `Economy/Shipwright.cs`, `Economy/ShipwrightOffer.cs`, `Economy/RepairLedger.cs` |
| **Instrument purchases are a separate asset type from gear** — gear is a carried wallet, an instrument is bolted to one hull | `Economy/InstrumentOffer.cs`, `Economy/InstrumentShop.cs`; ADR 0030 |
| **Only the watch is carried** (owner ruling 2026-07-24); every other instrument is per-hull | `diegetic-instruments-and-consoles.md` §0, §4 |
| **No phone / calendar / notebook / computer rig exists** | `docs/art/rigs/ui/` |

---

## 9. Phasing (nothing here jumps the queue)

This direction is **M2/M3 work**, and it must not displace finishing what is in flight (CLAUDE.md rule
8; `roadmap.md` §0). Proposed ordering, by dependency rather than by appetite:

- **Earliest — the calendar.** Both halves it needs are shipped (§5.1), and it reads state the clock
  already exposes. It sits naturally beside the **M2 St Peters** opening, where a wall calendar in the
  general store or the school is at home. *It is not in the backlog, and this doc does not put it
  there.*
- **M2 — the notebook**, with its task-data ADR and its save-schema ADR (§5.2.5). Deepest, touches the
  save, and wants M2-24's dialogue v2 to grant tasks properly (though §5.2.3's additive seam works
  against today's linear dialogue too).
- **M2 — the phone's map and tide apps**, landing *with* **M2-27**, because they are viewers of the
  chart-knowledge and tide-tier models M2-27 creates. Building them first would mean building those
  models twice.
- **M2/M3 — texts and calls**, after **M2-23**'s conditional schedules and **M2-24**'s relationship
  layer, since availability and the contact list read both.
- **M2/M3 — the for-sale apps**, alongside **M2-42** (buy & site a building) and the property ladder.
- **M3 — computers**, with **M3-11**.

---

## 10. Owner rulings needed

**This section is the product of this document.** Each ruling is written so it can be answered from the
PR page, in a sentence. Recommendations are marked; none is a decision.

**R1 · Is an app an instrument?** *(the keystone — §2, §3.1)*
Does the phone join the instrument ladder — the device bought, each app bought or unlocked separately,
priced and gated — or is it a free convenience that arrives with everything on it?
→ **Recommend: an app is an instrument.** It is the only reading under which the new directive and the
ratified earned-information doctrine are both true. **If the owner rules the phone free, the ratified
§3 rule of `diegetic-ui-and-inventory.md` is materially changed** and that doc must be amended first
(CLAUDE.md: change canon/doctrine *first*, then propagate) — not silently outvoted by a feature.

**R2 · Does the phone join the watch as CARRIED?** *(§3.1)*
The 2026-07-24 ruling is *"Only the watch is carried. Every other instrument is per-boat equipment."* A
phone is carried by nature. Does it become the second carried instrument (and do its apps ride
`SaveData.OwnedGear` like `gear.watch`, needing no schema change), or does it need a third category?
→ **Recommend: carried, as gear; computers are sited fixtures; consoles stay per-hull.** Clean line,
no schema change.

**R3 · Is the tide table gated — the almanac, the app, or both?** *(§4.2)*
PR #355 shipped the paper page free and asked this question; it was never answered. It is now
load-bearing, because the app makes the same page pocketable and M2-27 wants to sell tide-table tiers.
→ **Recommend: gate both, by tier.** A basic booklet early (2 days, home region), a bought almanac
mid, the app as a later tier with wider horizon. Gating the existing page is *"a data change… not a
rework"* (#355).

**R4 · What does the map app show, and does fog-of-war bind it?** *(§3.2, §4.1)*
Option (a): the phone gives a **fix on a coarse coast**; the survey, waypoints, route and track stay
the fitted chartplotter's. Option (b): the map is a **network map** that dies with the signal.
Either way — **does M2-27's fog-of-war apply to the phone as it does to every other viewer?**
→ **Recommend (a), and yes, fog-of-war binds every viewer.** One chart knowledge, many presentations.

**R5 · Browse remotely, buy in person?** *(§4.4)*
Do the for-sale apps let you *complete* a purchase, or only *see* what's on the market?
→ **Recommend: browse only.** A transacting app serves no pillar and costs P3 by emptying the yards
and shops (§7). Optional add-on: listings only for ports you've visited.

**R6 · Era and price.** *(§3.3)*
Which phone — a flip phone with a few apps, or a smartphone? And roughly where on the money ladder
does it land?
→ Suggest: **after the watch, around the punt/Cape era.** The look is owner taste and an art-director
brief.

**R7 · Does a phone call use the ratified bubble model?** *(§4.5)*
`dialogue-and-knowledge.md` §2 is ratified: the bubble anchors at the speaker, who moves and whose text
populating *is* the sound. **A call has no body on screen.**
→ **Recommend: texts by default** (a thread on the phone's screen is a *document*, already blessed),
**voice calls reserved** for a few things a text can't carry, drawn as a minimal call screen. Keeps the
ratified model intact rather than stretching it.

**R8 · Is the wall calendar free furniture, or bought?** *(§5.1)*
→ **Recommend: free and fixed.** The watch is portable-and-now; the calendar is fixed-and-ahead.
Walking home to find out what day it is, before you own a watch, is a *good* early beat.

**R9 · Are the notebook's help pages earned, and where is the line?** *(§5.2.4)*
→ **Recommend: gameplay knowledge is earned** (taught by an NPC, or written by doing it once);
**controls and the task list are always present.** A pre-printed strategy guide would break the
knowledge law it is meant to serve.

**R10 · Does the notebook retire the onboarding hint label?** *(§5.2.3)*
`OnboardingDirector` is the notebook's prototype: a seven-step task list on Core signals. Should the St
Peters opening become the first authored `TaskDef` — folding into **M2-31c**'s rework rather than
sitting beside it?
→ **Recommend: yes, when the notebook is built** — one task system, not a task system plus a legacy
nudge. Flagged so it isn't silently dropped.

**R11 · Battery?** *(§2)*
A phone that can run flat is a P5 teeth beat and a strong answer to *"can it be off?"*.
→ **Recommend: no.** It risks the anti-pillar (*"danger so punishing it stops being cozy"*), and
**coverage already does the same job better** — it takes the phone away exactly where the game wants it
gone, and never at a moment that feels like bookkeeping.

**R12 · Do NPCs go unreachable, and may world-content add an "at sea" activity tag?** *(§4.5)*
Availability is computable from the shipped routine plan today, but *out on the water* has no tag.
→ **Recommend: yes to both.** An append to an append-only enum, authored by world-content. A fisher
who doesn't answer at 05:40 is the strongest P3 beat in this direction.

---

## 11. Proposed backlog items — **PROPOSALS ONLY**

**None of these is claimed, scheduled, or committed.** This doc proposes; the roadmap and the owner
decide scope (CLAUDE.md rule 8; `agents/coordination.md` §8). Ids are placeholders, deliberately not in
the `M2-nn` sequence, so nothing here can be mistaken for a scheduled row. Most are **blocked on the
rulings in §10** — which is the point.

| Proposed | Title | Would own | One-liner | Blocked on |
|---|---|---|---|---|
| **P-DEV-01** | Wall calendar — world object + readable page | ui-ux (+ world-content to place) | An `IInteractable` fixture on the M2-39 verb opening a `TidePanel`-recipe page: weekday/date/season, market + rest day, moon and the spring/neap band | R8 |
| **P-DEV-02** | Calendar art rig | art-director | A calendar rig under `docs/art/rigs/ui/`, on the ADR 0021/0025 contract | — |
| **P-DEV-03** | `TaskDef` — a task as data | world-content + lead-architect | One asset per task, stable ids, steps whose completion predicates are **existing flags or existing Core signals** | R9 |
| **P-DEV-04** | Task grant seam (`GrantsTaskId` → `TaskGranted`) | world-content + lead-architect | Additive `DialogueDef` field + a Core EventBus signal; empty field = today's behaviour exactly | P-DEV-03 |
| **P-DEV-05** | Task-state save schema **(its own ADR)** | lead-architect | Persist only the irreducible facts (granted / step done / closed) on the ADR 0020 pattern; migration + old-save load test | P-DEV-03 |
| **P-DEV-06** | The notebook — task list + tabs | ui-ux | The page: tasks by giver, steps, and the help tabs | P-DEV-03/04/05, R9 |
| **P-DEV-07** | Earned help pages (`PageLearned`) | world-content + ui-ux | Pages granted by conversation or written by doing it once | R9 |
| **P-DEV-08** | Fold St Peters onboarding into the notebook | world-content | The opening becomes the first authored `TaskDef`; retire the hint label. **Coordinate with M2-31c** | R10 |
| **P-DEV-09** | Phone + app ownership model | economy-sim + lead-architect | `gear.phone` and per-app ids on the existing owned-gear list; shop offers | **R1, R2** |
| **P-DEV-10** | Signal coverage field | gameplay-systems | Authored per-region coverage; deterministic, unsaved; network apps dead offshore | R1 |
| **P-DEV-11** | Phone shell + flush/expanded presentation | ui-ux | The device and its two-state presentation on the S4.5 pattern; PPU-32 legibility pass | P-DEV-09, R6 |
| **P-DEV-12** | Phone rig | art-director | A phone rig under `docs/art/rigs/ui/` | R6 |
| **P-DEV-13** | Tide app as a fourth reader of `TideAlmanac` | ui-ux | No forked maths; its rung is horizon + coverage. **Coordinate with M2-27's tiers** | **R3** |
| **P-DEV-14** | Chart knowledge as one model, many viewers | ui-ux + lead-architect | Owned charts + discovered-by-presence, read by phone map, chartplotter and paper alike. **This is M2-27's core, not a separate build** | **R4** |
| **P-DEV-15** | Map app (position, on owned chart knowledge) | ui-ux | The fix, not the survey | P-DEV-14, R4 |
| **P-DEV-16** | Boats-for-sale app | ui-ux + economy-sim | Catalogue over the same `ShipwrightOffer` assets; **browse only** | **R5** |
| **P-DEV-17** | Properties-for-sale app | ui-ux + economy-sim | The same over `WharfBuildingDef` / the property ladder. **Coordinate with M2-42** | **R5** |
| **P-DEV-18** | `AtSea` routine activity tag | world-content | Append-only fifth tag so a fisher can be genuinely unreachable | **R12** |
| **P-DEV-19** | Contacts, texts and calls | ui-ux + world-content | Availability read from the routine plan; number earned by relationship; texts default, calls reserved | P-DEV-18, M2-24, **R7** |
| **P-DEV-20** | Computer — the management desk | ui-ux | **M3-11 given a desk** rather than a menu; plus *their* computers as knowledge surfaces | M3-11 |
| **P-DEV-21** | Computer / notebook rigs | art-director | Rigs under `docs/art/rigs/ui/` | — |

---

## 12. Cross-references — what this doc touches

- **[`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) — served, not reframed.** Its §3
  keystone rule is the constraint this whole document works inside. §3.1 here proposes that apps *join*
  the instrument ladder rather than bypass it. **If R1 is ruled the other way, that doc's §3 changes and
  must be amended there first.**
- **[`dialogue-and-knowledge.md`](dialogue-and-knowledge.md) — extended.** Its §3 already ratified
  cellphones and computers as devices and left §5 Q3 open (*who owns a phone; what's on the buyer's
  computer*). §3.4 and §4.5 here propose answers. Its §2 conversation model meets its one genuine edge
  case at a phone call (R7).
- **[`diegetic-instruments-and-consoles.md`](diegetic-instruments-and-consoles.md) — reopened at one
  point.** Its 2026-07-24 ruling *"only the watch is carried"* is directly challenged by a carried
  phone (R2). Its S4.5 flush/expanded presentation is reused, not replaced.
- **[`ux-and-mobile-controls.md`](ux-and-mobile-controls.md).** §5.3's management dashboard is what a
  **computer** is (§3.4); §5.4's chart is what the **map app** views (§4.1); §8's accessibility
  requirements apply unchanged to every device screen (§6).
- **[`time-tides-weather.md`](time-tides-weather.md).** §3.3's moon and spring/neap envelope are the
  calendar's best read; §3.6's tide-table tiers are the tide app's rung (§4.2); §4's forecast tools stay
  with the barometer/harbourmaster/radio (§4.3).
- **[`npcs-and-routines.md`](npcs-and-routines.md).** §2.6's shipped routine engine is what phone
  availability reads; the fifth activity tag is an append-only ask (§4.5).
- **[`economy-and-business.md`](economy-and-business.md) / [`progression-and-housing.md`](progression-and-housing.md).**
  The market and the property ladder the for-sale apps list — over the same assets, browse-only (§4.4).
- **Backlog rows this bears on:** M1-04 (the tide-table tool the almanac page shipped), M2-09 (forecast
  tools), M2-23/24 (routines, dialogue v2 + relationships), M2-25 (licences/reputation — the contact
  list rhymes), M2-27 (chart, fog-of-war, tide-table tiers), M2-31c (the onboarding rework the notebook
  would absorb), M2-39 (the interact verb every device opens on), M2-41/42 (buildings as data, and
  buying them), M3-11 (the management UIs a computer would house).
- **Pillars.** Canon [`../vision-and-pillars.md`](../vision-and-pillars.md) wins on any conflict. §7
  states which device serves which pillar, and names the one variant that would serve none.
