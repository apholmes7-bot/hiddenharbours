# Hidden Harbours — World & Regions

> Module doc. Subordinate to `../vision-and-pillars.md` (CANON). Region names, gates, scale,
> and pillars are locked there; this doc designs *how those regions work* in detail. If this
> doc ever contradicts the canon, the canon wins — fix it here.
>
> Sibling docs referenced: `time-tides-weather.md`, `fish-and-content.md`,
> `boats-and-navigation.md`, `economy-and-business.md`, `npcs-and-routines.md`,
> `progression-and-housing.md`, `art-and-audio-bible.md`, and the ADRs
> `../adr/0002-procedural-vs-handcrafted.md`, `../adr/0003-data-driven-content.md`.

---

## 1. Purpose & scope

This document defines the **overworld** of the Sablewick Banks: how the archipelago is laid
out, how the player moves through it, how it is revealed, what is authored versus simulated,
and a detailed brief for every region. It is the spatial backbone the other systems hang on —
fish live in regions, tides reshape regions, NPCs inhabit regions, the economy flows between
them.

Two things this doc deliberately does **not** own:

- **Fish species lists.** Regions name the *categories* of catch they offer (inshore,
  shellfish, pelagic, flats, deepwater, storm-grounds, legendary, cryptid). The 100 species
  and their spawn rules live in `fish-and-content.md`.
- **Tide/weather simulation math.** Regions describe *how the place changes* with tide and
  weather and reference the named mechanics, but the clock, the semidiurnal tide curve, the
  spring/neap cycle, wind/current fields, and forecasting all live in
  `time-tides-weather.md`. This doc consumes that simulation; it does not define it.

---

## 2. World shape: the Sablewick Banks

The Sablewick Banks is a **small fictional archipelago off Atlantic Canada** — a cluster of
islands, reefs, tidal flats, and narrows wrapped around one sheltered home harbour and one
market town, with open offshore grounds beyond and storm-lashed outer islands at the edge of
the world. Cold North Atlantic. Enormous tides. Fog. Lighthouses, weathered clapboard,
lobster traps, working wharves.

### 2.1 Mental map (relative geography, not to scale)

```
                              ~ open North Atlantic ~

        IRONBOUND  ·  ·  ·  ·  ·  ·  ·  THE SMOTHER (fogbank, far)
        (outer storm islands)                  ·
              \                                ·
               \                              ·
            THE BANKS  ················  THE SHIPPING LANES ——→ mainland markets
          (offshore grounds)                  /
                |                             /
                |                            /
            FUNDY RIPS  (narrows, fierce current)
                |
   THE DROWNDED LANDS ——— THE SUNKERS  (reef field)
     (tidal flats)             |
            \                  |
             \                 |
              PORT GREYWICK ———+——— CODDLE COVE   ← M1 slice start; home base in the full arc
              (market town)    |    (home harbour, sailed to AFTER the dory is repaired)
                               ·
                 (tide-gated SANDBAR — bares as a walking path at low water)
                               ·
                        ST PETERS ISLAND   ← the opening (M2 prologue)
                        (home island, tide-gated)

        ←——— sheltered, shallow, inshore        offshore, deep, dangerous ———→
```

> **St Peters Island is the owner-ratified prologue (phased M2, built as a greybox prototype — see
> `../roadmap.md`).** The arc is now **DECIDED** (owner-ratified 2026): **St Peters → [walk the
> tide-gated sandbar] → Port Greywick (buy cod licence + rod; buy + repair a damaged dory) → Coddle
> Cove (sail the repaired dory home; it becomes the home base).** St Peters **prepends** the arc
> **without deleting** the Coddle Cove opening — Coddle Cove stays central and **everything built for
> it is reused**; it is simply **reordered** to be the home you *arrive at* once the dory is yours
> (it remains the **M1 vertical-slice** start). St Peters links to the mainland **only across a
> tide-gated SANDBAR at low water** (pure P1): the **first crossing is on foot to Greywick**, before
> you own any boat. Its full brief is §6.0; everything about it below is **future (M2)**, captured
> here so the world stays a single source of truth. (Canon: `../vision-and-pillars.md` §5.8.)

The deliberate design gradient runs **bottom-left to top-right**: from sheltered, shallow,
forgiving, populated inshore water (Coddle Cove, Greywick) outward and upward to open, deep,
lonely, lethal water (The Banks, Ironbound, The Smother). This gradient is the spine of
progression (P2) and of escalating danger (P5). The player literally sails *up the difficulty
curve* as they sail away from home.

### 2.2 Is it one contiguous sea, or discrete zones?

**Decision: a hybrid — contiguous local clusters joined by explicit transitions.** This is the
single most important structural decision in this doc, so it is spelled out fully.

- **Within a region, the sea is fully contiguous and free.** You sail a continuous water
  surface, with no invisible walls inside the playable area. Coddle Cove, The Sunkers, and the
  inshore edge of The Drownded Lands form one continuous **Inshore cluster** you can roam
  without a loading transition once they are unlocked. Contiguity is what sells "a place,"
  and free local sailing is what makes tide and wind feel like forces you fight in real space
  (P1), not menu states.

- **Between clusters, travel is an explicit transition** — a short sail to a marked **passage**
  (a channel mouth, a strait, a sea-lane buoy line) that streams the next cluster in. The
  player still *drives the boat through* the passage (it is not a teleport menu), but the
  passage is where we hide a streaming load and a difficulty/identity gate. Examples: the mouth
  of Coddle Cove opening to The Sunkers; the strait into the **Fundy Rips**; the run offshore
  to **The Banks**.

- **Far/late regions can also be reached by a "set a course" fast-travel** once discovered and
  once the boat is capable — you plot a destination from the chart, a short stylised sailing
  sequence plays (with a weather/fuel/risk check), and you arrive. This keeps late-game fleet
  logistics from becoming a tedious manual slog (P4) while preserving the *first* journey to
  each place as a hand-driven, earned event.

> **Why hybrid and not pure-open-world?** A fully seamless open ocean would (a) blow the mobile
> streaming budget, (b) flatten the authored sense of distinct *places*, and (c) remove the
> ritual of "making passage" that the fishing fantasy lives on. A fully menu-driven
> zone-to-zone map would kill P1 (you can't read tide and wind from a menu) and P5 (no space
> to get into and out of trouble). Hybrid gets both: real sailing where it matters, controlled
> transitions where it pays for itself.

### 2.3 Region adjacency graph

| From | Reachable directly via | Transition type |
|------|------------------------|-----------------|
| St Peters Island *(prologue, M2)* | **Port Greywick** (the mainland) | **Tide-gated SANDBAR** — bares as a **walking path** near **low water** (boat-crossable channels narrow as the tide falls); cut off at high tide (P1). The opening's **first crossing is on foot to Greywick** before any boat is owned. |
| Coddle Cove | The Sunkers, Port Greywick | Contiguous (inshore cluster). *(In the full arc, Coddle Cove is **first reached by sailing the repaired dory from Greywick**, then it is the home base — `../vision-and-pillars.md` §5.8.)* |
| The Sunkers | Coddle Cove, The Drownded Lands | Contiguous |
| The Drownded Lands | The Sunkers, Port Greywick, Fundy Rips | Contiguous inshore / passage to Rips |
| Port Greywick | Coddle Cove, The Drownded Lands, The Shipping Lanes, **St Peters Island** *(tide-gated sandbar, M2)* | Contiguous harbour + lane buoys; St Peters via the tide-gated sandbar (on foot at low water) |
| Fundy Rips | The Drownded Lands, The Banks | Timed passage (tide-gated strait) |
| The Banks | Fundy Rips, Ironbound, The Smother, The Shipping Lanes | Open-water passages / set-a-course |
| Ironbound | The Banks | Open-water passage (weather-gated) |
| The Smother | The Banks | Instrument-gated passage |
| The Shipping Lanes | Port Greywick, The Banks, → mainland | Lane network (freight) |

`MapGraph` (a ScriptableObject, see §8) holds these edges; each edge carries its gate
requirement and transition type. The graph is the single source of truth for "where can I go
from here," used by both the chart UI and the set-a-course router.

---

## 3. Handcrafted macro + procedural detail (per ADR-0002)

The canon principle is **"authored where identity and story live; procedural where scale and
variety live."** Applied to the world:

**Authored (hand-built, version-controlled, identical every playthrough):**

- The **macro-layout** of every region: coastline shape, the position of landmarks, the
  channel and passage network, where the safe water and the killing water are, depth contours
  (the bathymetry that drives grounding), and which tiles are land/water/intertidal.
- All **landmarks**: the lighthouse, the wreck on the flats, the auction house, named sunkers,
  the gut of the Rips.
- **Town and buildings** in Port Greywick and the home wharf in Coddle Cove.
- The placement of **named NPCs** and their home/work anchors (see `npcs-and-routines.md`).
- **Quest geography** — where story beats happen.

**Procedural / simulated (generated per session or per tick, varies):**

- **Fish spawn distribution** within each region's authored habitat zones (`fish-and-content.md`
  owns the rules; the region only supplies the habitat zones and category weights).
- **Tide-pool and flats contents** — what specific clams, shellfish, flotsam, and small
  secrets are exposed when water recedes, rolled against the region's loot tables.
- **Weather, wind vector, and tidal-current fields** over the region (`time-tides-weather.md`).
- **Flotsam and drift** — debris, lost gear, message-in-a-bottle style finds that wash through
  on the current.
- **"Extra" background NPCs** — dock workers, market crowds, other skippers' boats passing
  (see `npcs-and-routines.md`).
- The **optional endless outer grounds** beyond The Banks/Ironbound — a procedural late-game
  zone for players who want to keep fishing forever (canon §5.7).

**The seam between them:** authored regions expose a small, fixed set of **habitat zones** and
**loot/spawn tables** as data. The procedural systems read those and fill in the variety. A
region is therefore "a hand-drawn stage + a set of labelled buckets the simulation pours
content into." This keeps every region recognisably *itself* every time, while no two tides
feel identical.

Per-region, §6 calls out explicitly what is authored vs simulated so an implementer can see the
split at a glance.

---

## 4. Travel, navigation & the feel of being at sea

(Boat-handling physics, draught, fuel, and instruments are owned by `boats-and-navigation.md`;
this section covers only how travel reads at the *world* level.)

- **You always drive the boat.** Even fast-travel is framed as "plotting and making passage,"
  never a silent teleport. The boat is the avatar at sea exactly as the character is on land —
  one perspective everywhere (canon §5.2).
- **Distance is felt, then compressed.** Early game, every metre is hand-sailed and the world
  feels large because your dory is slow and tide/wind shove you around. Later, faster hulls and
  set-a-course compress the same distances — the world *shrinks as you grow*, which is itself a
  progression reward (P2).
- **The sea is never neutral.** Wind pushes, current sets, tide raises and lowers the floor
  under you. Reading these to pick a route, a departure time, and a tide window is core
  navigation (P1). The chart, tide table, and barometer are your instruments.
- **Getting lost is possible** in fog and in The Smother — see §7 and `time-tides-weather.md`.
  Fog reduces draw/sensor range; without instruments you navigate by landmark, sound, and dead
  reckoning, and you *can* go the wrong way (P5).

### 4.1 Map reveal & exploration

The world is **not** handed to the player as a finished chart. Revelation is a progression
system in itself, layering three mechanisms:

1. **Fog of war by presence.** Each region's chart starts blank/indistinct. Sailing through
   water reveals local bathymetry, hazards, and landmarks into your **personal chart** as
   discovered. What you have personally seen is drawn; what you haven't is grey. This rewards
   exploration directly and makes the first transit of anywhere meaningful.

2. **Charts you buy or earn.** The **chart shop / chandlery in Port Greywick** sells regional
   **sea charts** that pre-reveal the safe channels, marked sunkers, and depth contours of a
   region *before* you go — at a price. A chart is the difference between groping blind and
   knowing where the rocks are. Some charts are gated behind story or NPC relationships rather
   than money (e.g., the lighthouse keeper's hand-annotated chart of Ironbound; a smuggler's
   chart of The Smother). Charts are also a soft progression gate: you *can* enter an unlocked
   region with no chart, but it is far more dangerous (P5).

3. **Tide tables & instruments as a layer of the map.** A purchased/earned **tide table**
   (`time-tides-weather.md`) overlays predicted water levels onto the chart, which is what
   actually makes The Drownded Lands and the Fundy Rips legible. Late-game instruments (sounder,
   radar/sonar) extend reveal range and are mandatory for The Smother.

> **Charts vs fog-of-war, reconciled:** buying a chart fills in the *authored* features
> (channels, named hazards, contours) immediately. Fog-of-war still governs the *living* layer
> — where fish are biting today, where flotsam has drifted, what the flats exposed this tide.
> You can buy knowledge of the rocks; you still have to go *look* for the fish.

---

## 5. The regions at a glance

| # | Region | Cluster | Identity (one line) | Unlock gate (canon) |
|---|--------|---------|---------------------|---------------------|
| 0 | **St Peters Island** *(prologue, M2)* | Inshore (home island) | Tide-gated home island; the opening — clam-dig at low water, **walk the sandbar to Greywick** | The opening (no gate; built M2) |
| 1 | **Coddle Cove** | Inshore | Home harbour; sheltered tutorial water | Start zone (M1 slice); in the full arc, **home base sailed to after the dory is bought + repaired at Greywick** |
| 2 | **The Sunkers** | Inshore | Tidal reef field; shellfish & grounding hazard | Basic boat + reading tide |
| 3 | **Port Greywick** | Inshore | The market town; shops, auction, most NPCs | Story unlock (early) |
| 4 | **The Drownded Lands** | Inshore→mid | Tidal flats that become walkable seabed at low water | Tide mastery + tide table |
| 5 | **Fundy Rips** | Mid | Narrows with ferocious tidal current; time the tide | Capable hull + navigation skill |
| 6 | **The Banks** | Offshore | Open offshore grounds; deepwater & big pelagics | Seaworthy offshore boat (dragger-class) |
| 7 | **Ironbound** | Outer | Cold storm-lashed outer islands; end-game grounds | Weather-capable boat + skill |
| ✦ | **The Smother** | Outer (opt) | Permanent fogbank; navigate by instrument & sound | Late-game instruments |
| ⚓ | **The Shipping Lanes** | Commerce | Freight/cargo routes to mainland & wider markets | Freighter tier + business unlocks |

---

## 6. Region briefs

Each brief follows the same template so an implementer can scan them: **Identity & mood ·
Physical layout & landmarks · Hazards · Catch & resources (categories) · Tide behaviour · NPCs
present · Authored vs simulated · Role in progression.**

---

### 6.0 St Peters Island — *the home island* (prologue · phased **M2**)

> **Phasing banner.** St Peters is the **owner-ratified opening**, built in **M2** as a **greybox
> prototype** (`../roadmap.md`). It **prepends** the arc and does **not** delete the Coddle Cove
> opening, which remains the **M1 vertical-slice** start and is **reused** (reordered to be the home
> base sailed to later). Everything in this brief is **future work** captured now so the world stays
> consistent. The Ginny/Ned onboarding **relocates** here at M2 and the **dialogue/onboarding system
> is reused, not rebuilt**. **The decided arc:** St Peters → **walk the tide-gated sandbar to
> Greywick** → buy a **cod licence + rod**, sell clams, **buy a damaged dory at the Greywick
> shipwright and pay to repair it** → **sail the dory home to Coddle Cove** (canon §5.8).

**Identity & mood.** Where the game begins. A tiny, weathered **home island** off the mainland —
three clapboard houses, a one-room **school**, and a **general store**. Cut off from the mainland
**except when the tide bares the sandbar**. Quiet, close, the whole world the size of a low-tide
walk. The mood is childhood's-end and first independence: you learn the sea here in miniature before
you ever leave home. Cozy, safe, formative.

**Physical layout & landmarks.** A small island holding: the **village** (3 houses + the **school**
where the aunt teaches the compass and hand skills + the **general store** for basic gear); **clam
flats** that bare at low water; and the **tide-gated SANDBAR** (a cobble-and-sand bar) that bares as
a **walking path to Port Greywick** only near low water. Everything within an easy walk; shallow,
soft, forgiving water all around. *(No dory here — your first boat is bought damaged and repaired at
the **Greywick shipwright**, §6.3; the earlier "uncle's broken dory on the slip" framing is retired,
canon §5.8.)*

**Hazards.** Deliberately minimal — the gentlest tutorial of all. The one teeth-of-tide lesson: **the
sandbar floods.** Walk across toward Greywick and dawdle past the turn of the tide and you are **cut
off until the next low water** (lost time, never worse — P5 at its kindest). Soft mud can briefly
mire a careless step. No rocks, no current, no weather of consequence.

**Catch & resources (categories).** **Tidepool & Flats** is the whole economy here: **clam /
shellfish digging** on the bared flats — the **shovel** and the **"two squirting holes"** tell (see
`fish-and-content.md`) — **licence-gated** (the **clam licence** is owned by the licence system in
`economy-and-business.md`, not authored here). This by-hand income is what funds the **dory** you buy
and repair at Greywick (P4 in miniature: do the humblest job by hand to earn your way up). St Peters
offers no rod-fishing of consequence — the **rod** is bought at **Greywick**, after the walk.

**Tide behaviour.** Tide is the island's defining force from minute one (the purest P1 primer): low
water bares the clam flats **and** opens the **sandbar** to Greywick; high water drowns the flats and
**seals the island off**. Before anything else, the player learns that the sea's clock decides *what
they can do* and *where they can go* — the ideal warm-up for the Sunkers, the Drownded Lands, and the
Rips. **Spring lows** bare the most flat (best digging) and the widest sandbar window; neaps the
least. (Curves owned by `time-tides-weather.md`.)

**NPCs present.** The **aunt** — the teaching / home figure of the opening (*reconcile with Aunt
Ginny; see `../vision-and-pillars.md` §5.8 and `npcs-and-routines.md` — intent is one teaching-aunt
placed to serve the arc*); a handful of island **neighbours**; the **storekeeper** (sells basic
gear). The departed **Uncle Ned** is felt here through the cottage he left (in the Cove), the aunt's
stories, and his logbook — **not** through an inherited dory (the boat is now earned + repaired at
Greywick, canon §5.8). A small cast by design — intimacy over bustle.

**Authored vs simulated.** *Authored:* island layout, the village (houses/school/store), the
clam-flat and **sandbar** geography, the opening quest beats. *Simulated:* which clams/flotsam are
exposed this tide (scaled by spring/neap), tide/weather, and the **sandbar window** timing.

**Role in progression.** The **prologue / the cradle before the cradle.** It teaches, at the smallest
possible scale, the spine of the whole game: **read the tide → work by hand (dig clams) → earn →
walk the sandbar → spend (cod licence + rod, then buy + repair the dory at Greywick) → sail home.**
Completing it (owning the repaired dory and sailing to the cove) **graduates the player to Coddle
Cove** as the home base and the rest of the arc. Because it is **M2**, the M1 slice still *starts* at
Coddle Cove; when St Peters lands, the start and onboarding move here and Coddle Cove is reused as
the reordered home base.

---

### 6.1 Coddle Cove — *the home harbour*

**Identity & mood.** The first place you ever see and the emotional anchor of the whole game.
A small, sheltered, working cove on a hard but beautiful stretch of coast — your late uncle's
weathered clapboard cottage above a tired wooden wharf, a handful of neighbours, gulls, the
smell of salt and diesel. Safe, intimate, *yours*. The mood is hope tinged with grief: you
inherited very little and a lot of weather. This is the cozy heart the whole game radiates out
from (P5).

**Physical layout & landmarks.** A horseshoe cove with a narrow mouth opening seaward toward
The Sunkers, and an inshore passage hugging the coast toward Port Greywick. Landmarks: the
**uncle's cottage & wharf** (your home base, save/rest, basic storage, the dory's mooring); a
small **stony beach** for beachcombing; a **fish shack / bait stand**; a couple of neighbour
cottages; a leaning **cove marker beacon** at the mouth. Shallow, soft-bottomed, forgiving
water throughout — the one place you almost can't get into serious trouble.

**Hazards.** Deliberately minimal — this is the tutorial sandbox. The narrow mouth can ship a
little chop in a blow; the inner cove can get *too* shallow at extreme low spring tide near the
beach, gently teaching grounding without punishing it. No sunkers, no rips, no fog of
consequence.

**Catch & resources (categories).** **Inshore** finfish (the beginner category) on handline and
in the shallows; basic **shellfish** near the rocks; **beachcombing/flotsam** on the stony
beach. The first cod you haul by hand happens here — the canon's defining "earned triumph"
moment (P4). Species in `fish-and-content.md`.

**Tide behaviour.** The gentle teacher of tide. High water floods the beach and opens the full
cove; low water exposes a strip of stony foreshore (light beachcombing) and shoals the inner
corner. Tide here is *legible and safe* — the player learns to read the tide table
(`time-tides-weather.md`) where mistakes cost minutes, not a hull.

**NPCs present.** The departed **uncle's** memory anchors this home base (the cottage, the logbook —
see `npcs-and-routines.md`); a small set of neighbours (e.g., the first hireable deckhand lives
nearby). Low population by design — intimacy over bustle. *(In M2 the teaching/onboarding relocates
to St Peters; the Cove keeps the hearth — `../vision-and-pillars.md` §5.8.)*

**Authored vs simulated.** *Authored:* cove shape, cottage/wharf, beach, beacon, neighbour
cottages, tutorial quest geography. *Simulated:* inshore fish spawns, beachcombing loot,
weather/tide, the odd passing boat.

**Role in progression.** The cradle. Tutorialises the full core loop — fish → store → sell —
at the smallest, safest scale, and is the vertical-slice region (canon §7). In the **full arc** it
is the **home base you sail your newly-repaired dory home to** after St Peters + Greywick (canon
§5.8), and remains a cozy home base forever; you return to it to rest, to the cottage, and
eventually to *miss* it as you spend your days at the edge of the world.

---

### 6.2 The Sunkers — *the reef field*

**Identity & mood.** The first taste of teeth (P5). A scatter of low islets and **submerged
rocks** ("sunkers" — canon vernacular) just seaward of Coddle Cove, where the inshore cluster
starts to bite. Beautiful and rich and quietly dangerous: gorgeous tide pools and shellfish
beds laid over rocks that will hole your hull if you misread the water.

**Physical layout & landmarks.** A field of named and unnamed reefs threaded by a few safe
channels. Landmarks: the **Three Brothers** (a trio of barnacled rocks that show and hide with
the tide and serve as a natural tide-gauge), a **kelp gut**, a tilted **navigation spindle**
marking the worst sunker, rich **tide-pool shelves**. The authored bathymetry here is dense and
deliberate — this is a region you *read*.

**Hazards.** The signature hazard is **grounding on the sunkers** at or near low water. A
submerged rock that is harmless at high tide is a hull-holing strike at low tide. Tidal current
funnels through the channels. This is where the game first teaches that *the same cove is a
different place at high vs low tide* (P1) — and that getting it wrong has consequences (P5).
Grounding/strike resolution is owned by `boats-and-navigation.md`.

**Catch & resources (categories).** **Shellfish** (the headline — the richest shellfish in the
inshore cluster), **inshore** finfish around the structure, **tide-pool** harvest (exposed at
low water). The natural home grounds for the lobster-boat specialist branch later.

**Tide behaviour.** The most tide-expressive of the early regions. At low water the reefs
emerge, safe channels narrow, sunkers surface as visible hazards, and the tide pools open for
harvest. At high water the whole field drowns to navigable (but now *invisibly* hazardous)
water. The Three Brothers function as an in-world tide indicator. Spring lows expose the most;
neap lows the least (`time-tides-weather.md`).

**NPCs present.** Largely unpeopled — a lonely working reef. Other skippers' boats pass; a
named NPC may run traps here. Population mostly from the procedural "extras" boat traffic.

**Authored vs simulated.** *Authored:* reef/sunker placement, channels, bathymetry, the named
landmarks, tide-pool shelf locations. *Simulated:* which shellfish/tide-pool contents are
exposed this tide, fish spawns, current field, flotsam caught in the kelp gut.

**Role in progression.** The skill-check that proves you can read tide before the game lets you
go further. Gate: **basic boat + reading the tide** (canon). It teaches grounding cheaply and is
the gateway from the safe cove to the wider inshore cluster.

---

### 6.3 Port Greywick — *the market town*

**Identity & mood.** The beating commercial and social heart of the Banks. A larger working
harbour town of weathered clapboard, a stone breakwater, a busy public wharf, and the
**auction house** where the day's catch is sold. Warm, bustling, a little rough at the edges —
this is where the *living working coast* (P3) is most visible: people with routines, a market
that breathes, the rhythm of a town that runs with or without you.

**Physical layout & landmarks.** A protected harbour behind a stone breakwater, a long public
wharf with multiple berths, and a walkable town above it. Key buildings (each a service hub —
details in the relevant docs): the **auction house / fish market** (`economy-and-business.md`),
the **chandlery / gear shop**, the **shipwright's yard** (boat purchase & upgrades,
`boats-and-navigation.md`), the **chart shop**, the **tavern**, the **harbourmaster's office**,
the **marine-supply / processing plant** (value-add, `economy-and-business.md`), and
**housing/commercial property** for sale (`progression-and-housing.md`). A small **lighthouse**
on the point. This is the densest authored content in the game.

**Hazards.** Minimal on the water (sheltered harbour) — Greywick is a *safe haven*, the place
you run *to*. The "hazards" here are economic and social: market timing, prices, reputation.

**Catch & resources (categories).** Greywick is primarily a **services and market** region, not
a fishing ground. The harbour itself offers only marginal inshore/scavenge fishing; the value
of Greywick is selling, buying, refining, hiring, and storing — not catching.

**Tide behaviour.** The harbour is dredged/deep enough to stay workable across the tide (a
deliberate gameplay convenience — your home market should not strand you), but tide still shows:
the wharf pilings reveal their barnacle line, small craft sit differently at the floats,
low-tide mud flats edge the inner harbour. Tide is *atmospheric* here rather than dangerous.

**NPCs present.** The bulk of the **named core cast** lives and works here — harbourmaster,
auctioneer, shipwright, gear-shop owner, tavern keeper, processing-plant owner, rival skipper,
and more (full cast in `npcs-and-routines.md`). Plus heavy procedural "extras" foot traffic to
make the town feel populated.

**Authored vs simulated.** *Authored:* the entire town layout, every named building and
service, breakwater, wharf berths, named NPC anchors, quest geography. *Simulated:* market
prices (supply/demand, `economy-and-business.md`), crowd "extras," ambient harbour boat
traffic, weather/tide ambiance.

**Role in progression.** The hub that turns fish into money into capability. **Story-unlocked
early** (canon) — opening it is the moment the game expands from "subsistence in the cove" to
"a player in the coast's economy." Every other progression system (boats, business, housing,
staff, relationships) routes through Greywick.

---

### 6.4 The Drownded Lands — *the walkable seabed*

**Identity & mood.** The game's signature "wow, the tide *does that?*" region and the purest
expression of P1. A vast expanse of **tidal flats** that, at low water, drains into **walkable
seabed** — you beach the boat, step out onto the exposed bottom, and harvest clams, pick over
revealed wrecks, and find secrets in a landscape that is *ocean* a few hours later. Eerie,
beautiful, faintly melancholy; the bones of old shipwrecks in the mud, the line of the
incoming tide on the horizon. ("Drownded" = flooded, canon vernacular.)

**Physical layout & landmarks.** A huge gently-shelving flat cut by **tidal channels** (guts)
that drain and fill. Landmarks: the **bones** (a large half-buried **wreck** you can board only
at low water), **clam beds**, a **drowned forest** of old stumps, an eel grass meadow, and a
**refuge marker** (high ground / a staging islet you retreat to as the tide turns). At high
water it is open, shallow, navigable sea; at low water it is a walkable mudscape.

**Hazards.** The defining hazard is the **returning tide** (P5). Walk out too far, lose track of
the clock, and the flood catches you — channels fill first and *cut off your retreat*, water
rises faster than you expect across a flat plain, and a misjudged harvest run can leave you
stranded or worse. This is the canon "genuine gut-punch" of being caught by the water. Soft mud
can also mire the boat on a falling tide (grounding). The region is *only* legible with a tide
table, which is exactly why it is gated on tide mastery. Stranding/rescue resolution in
`boats-and-navigation.md`; the tide clock that drives the danger is in `time-tides-weather.md`.

**Catch & resources (categories).** A unique **flats** category: **clam/shellfish digging** on
the exposed beds, **wreck salvage / flotsam** (the standout "secrets" loot), eel-grass and
intertidal species, and at high water, shallow **inshore** finfish. The risk/reward sweet spot —
the best flats loot is furthest out, where the tide is most dangerous.

**Tide behaviour.** The single most tide-driven region in the game; tide doesn't *modify* it,
tide *defines* it. The state of the region is a direct function of the water level: a continuous
low→high transformation from walkable mudscape to open sea and back, twice a tidal day. The
**spring/neap cycle matters enormously** — only the big **spring low tides** drain it fully and
expose the furthest beds, the deepest wreck, and the rarest secrets; neap tides barely uncover
it. This makes spring lows a *scheduled event the player plans their week around* (P1, and a
satisfying mastery loop). All curves and timing owned by `time-tides-weather.md`.

**NPCs present.** Sparse and atmospheric — a clam-digger or two working the beds at low water
(a possible named-NPC anchor or recurring extra); otherwise empty and quiet. The emptiness is
the mood.

**Authored vs simulated.** *Authored:* the flat's bathymetry, channel/gut network, the wreck,
clam-bed and drowned-forest locations, the refuge marker, secret/landmark placement. *Simulated:*
which clams/secrets/flotsam are exposed this particular tide (and how far out, scaled by
spring/neap), high-water fish spawns, the tide-cutoff timing of each channel.

**Role in progression.** The reward for mastering the tide. **Gate: tide mastery + a tide
table** (canon). It is the first region that *requires* using a tool (the table) to be safe,
graduating the player from "I noticed the tide" to "I plan around the tide." Also a recurring
late-game destination, because spring-low secret runs stay valuable.

---

### 6.5 Fundy Rips — *the narrows*

**Identity & mood.** A white-knuckle threshold region. Tight **narrows** where the enormous
tides of the Banks squeeze into a strait and tear through as **ferocious tidal currents** and
standing **rips** (canon vernacular). The water *races*. Transit is a timing puzzle and a
genuine test of nerve — the first place the sea openly tries to take you somewhere you didn't
choose. The gateway, both literally and in difficulty, between the inshore world and the open
ocean.

**Physical layout & landmarks.** A constricted channel (the **Gut**) between headlands, with
back-eddies, a notorious **overfall** where the current piles into standing waves, **whirlpool**
zones at peak flow, and a couple of **eddy refuges** where you can hold station and wait out the
current. Landmarks: the **Gut**, the **Boiling Ground** (the overfall), an old **tide mill ruin**
on the shore that historically harnessed the race, range markers for lining up the safe transit.

**Hazards.** The defining hazard is the **tidal current itself** (P1, P5). At peak flood/ebb the
current is too strong to fight — it sets you sideways, into the overfall, onto the rocks. You
**must time your transit to slack water** (the brief window at the turn of the tide when current
drops near zero). Misjudge it and you are swept through the Boiling Ground, spun in the
whirlpools, or pinned against a headland. Wind-against-tide makes the rips far worse. Current
physics and the slack-water window are owned by `time-tides-weather.md` and
`boats-and-navigation.md`; this region is where they first become life-or-death.

**Catch & resources (categories).** **Fast pelagic** fish (canon) that ride the current and the
nutrient-rich rips — a new, faster, more active fishing category than anything inshore. High
reward for fishing a dangerous place, fishable mainly in the calmer slack/eddy windows.

**Tide behaviour.** Tide *is* the gate. The region cycles flood → slack → ebb → slack, and only
the **slack windows** are safely transitable or fishable; peak current is a wall. The strength
of the current scales with the tidal range, so **spring tides make the Rips far more dangerous**
than neaps — an advanced player reads not just the tide clock but the spring/neap state before
attempting a transit. This is the most demanding application of tide-reading before the offshore
regions.

**NPCs present.** Effectively none on the water — too dangerous to inhabit. The tide-mill ruin
may carry environmental story / a lore hook. A rival skipper might be encountered making the same
risky transit.

**Authored vs simulated.** *Authored:* the strait geometry, the Gut, the overfall and whirlpool
zones, eddy refuges, range markers, the tide-mill ruin. *Simulated:* the live current field and
its strength (driven by tide phase + spring/neap), pelagic fish spawns in the eddies, the timing
of each slack window.

**Role in progression.** The graduation gate from inshore to offshore. **Gate: a capable hull +
navigation skill** (canon) — your dory/skiff cannot fight this water; you need a real boat (Cape
Islander class) and the navigation skill to read slack water. Passing the Rips is a major
P2 milestone: it is the door to The Banks and everything beyond. It also remains a *toll* you
pay (timing the tide) every time you go offshore, keeping the sea's moods relevant late-game.

---

### 6.6 The Banks — *the open grounds*

**Identity & mood.** The open sea. Past the Rips the coast falls away and you are on the
**offshore grounds** — big water, big sky, big fish, and the first real loneliness of being out
of sight of land. This is the historical heart of the Atlantic fishery (cod on the banks) and
the region where the game's scale fantasy fully lands: you are a small boat on a vast,
indifferent ocean (P2, P5). Awe and exposure in equal measure.

**Physical layout & landmarks.** Open water over productive **shoal banks** (raised seabed where
fish concentrate) separated by deeper troughs. Largely landmark-sparse by design — the point is
openness — but anchored by a few features for orientation and content: named **banks/shoals**
(the productive fishing marks), a lone **weather buoy**, a distant **gas/lighthouse platform** or
sea-mark, and the **edge of soundings** where the bottom drops away toward Ironbound and the
Smother. This is where the procedural endless outer-grounds zone (canon §5.7) can attach at the
far edge.

**Hazards.** **Distance and weather** (P5). You are far from help; a breakdown or a sudden blow is
serious. **Storms** and heavy sea state can roll in, and out here there is no cove to duck into —
you read the barometer/forecast and decide whether to run for home. **Fog** can settle. Big-fish
fights can be punishing. The danger shifts from *terrain* (rocks, current) to *exposure* (weather,
range, isolation).

**Catch & resources (categories).** **Deepwater groundfish** and **big pelagics** (canon) — the
serious commercial catch. This is where the **dragger/trawler net** gameplay opens up (big hauls,
big holds) and where fishing becomes an *industry* rather than a handline. The volume here is what
feeds the value-add and freight economy.

**Tide behaviour.** Out in open water, tide expresses as **broad tidal streams** (offshore
current sets that nudge your position and affect where fish concentrate) and as **sea state**
amplified by wind-over-tide, rather than as exposed/drowned terrain. Tide is less about grounding
here and more about current set and drift over the long hours of an offshore trip
(`time-tides-weather.md`). The day/night clock matters more — offshore trips can span a full
in-game day, introducing overnight steaming.

**NPCs present.** Other skippers and draggers working the grounds (procedural "extras" boat
traffic; possibly a named rival working the same marks). No settlements — this is workplace, not
home.

**Authored vs simulated.** *Authored:* the productive bank/shoal locations and their bathymetry,
the weather buoy and sea-marks, the edge-of-soundings boundary, the seams to Ironbound/Smother.
*Simulated:* fish spawns/schools over the banks, the live weather and sea state, tidal-stream
drift, the optional endless outer grounds.

**Role in progression.** The transition from *laborer to industry* (P4) and the mid-to-late game
core grind. **Gate: a seaworthy offshore boat, dragger-class** (canon). Reaching The Banks means
you have climbed the boat ladder into real commercial fishing; the catch volume here bankrolls
the jump to the commerce tier and unlocks the two end-game frontiers (Ironbound, the Smother).

---

### 6.7 Ironbound — *the storm coast*

**Identity & mood.** The edge of the world. Cold, **storm-lashed outer islands** of bare rock and
breaking surf at the seaward limit of the Banks — the most dangerous *terrain* in the game married
to the most dangerous *weather* in the game. Grim, magnificent, and lethal. Going to Ironbound is
a statement: you have a boat and the skill to survive where almost nothing does. The end-game
fishing grounds for the bravest skippers (P5 at full volume).

**Physical layout & landmarks.** A cluster of harsh, cliff-bound outer islands ringed by reefs and
breaking water, with very few safe approaches. Landmarks: a lone **lighthouse** (and its keeper —
see `npcs-and-routines.md`) standing watch over the worst of it, a notorious **shipwreck graveyard**
on the windward reefs, sea caves, a single grudging **storm-anchorage** where a capable boat can
shelter. Beautiful, brutal, and largely uninhabitable.

**Hazards.** *Everything at once* (P5): frequent and violent **storms** (the canon nor'easter),
heavy **breaking seas** against a **rock-and-reef coast**, **fog**, and cold. This is the region
that can actually sink you if you misjudge the weather window — the apex of "cozy, but with teeth."
Survival depends on reading the forecast, picking your weather window, and having a boat seaworthy
enough to take the punishment. Weather severity and the sinking/rescue stakes are owned by
`time-tides-weather.md` and `boats-and-navigation.md`.

**Catch & resources (categories).** **Storm-grounds** species and **rare / legendary** fish
(canon) — the rarest, most valuable, most prestigious catches in the natural world, found only
where the weather keeps everyone else away. The ultimate risk/reward fishing.

**Tide behaviour.** Tide compounds the danger: tidal current wrapping the islands stacks against
storm swell to make the approaches even worse (wind-and-tide-against-sea), and tide-covered reefs
around the islands are grounding hazards like a lethal version of the Sunkers. Reading tide *and*
weather *and* sea state together is the mastery ceiling of the natural-world game.

**NPCs present.** Almost none — the **lighthouse keeper** is the lone human presence and a key
late-game NPC (a hermit who knows these waters and can hand-annotate a chart of them). Otherwise
empty; the isolation is the point.

**Authored vs simulated.** *Authored:* the island cluster, reefs, cliffs, the lighthouse, the
wreck graveyard, sea caves, the storm-anchorage, the keeper's anchor. *Simulated:* the (frequently
severe) weather and sea state, fish spawns including legendaries, tide/current around the reefs.

**Role in progression.** A true end-game frontier. **Gate: a weather-capable boat + skill**
(canon — stern-trawler/seiner class that can survive offshore weather, plus the skill to read it).
Ironbound is where late-game *natural-world* mastery is proven and where the rarest content lives;
it runs parallel to the commerce-tier endgame rather than blocking it.

---

### 6.8 The Smother — *the fogbank* (late / optional)

**Identity & mood.** The uncanny one. A **permanent fog bank** out beyond the Banks where you
**navigate by instrument and sound** alone (canon) — eerie, disorienting, hushed, and home to
**cryptid fish** that exist nowhere else. This is the game's brush with the strange (the *Dredge*
inspiration, canon §4): not horror exactly, but unease; the menacing side of fishing. Optional,
late, and unforgettable. ("Smother" = a thick fog, coastal usage.)

**Physical layout & landmarks.** Deliberately *illegible by sight* — visibility is near zero, so
the "landmarks" are **non-visual**: a mournful **bell buoy** and a **whistle buoy** you home in on
by sound, the **foghorn** of a half-glimpsed structure, soundings off the bottom. What land/hazard
exists looms out of the murk at the last second. The authored layout is real and fixed, but the
player perceives it through instruments, not eyes — making the chart, sounder, and radar
indispensable.

**Hazards.** **Disorientation and collision** (P5). Without working instruments you *will* get lost
and *can* go in circles or pile onto an unseen hazard — fog removes the visual navigation the rest
of the game relies on. The danger is psychological and navigational rather than violent weather
(though the two can combine). Getting lost in fog is an explicit canon danger; this is its temple.
Sensor/fog mechanics owned by `time-tides-weather.md` (fog) and `boats-and-navigation.md`
(instruments).

**Catch & resources (categories).** **Cryptid / uncanny** fish (canon) — a special, strange
category found only here: the weird, the eyeless, the impossibly old. The collection/prestige
payoff for braving the fog. (Species in `fish-and-content.md`.)

**Tide behaviour.** Muted and ominous — tide still sets a current you must account for *blind*
(you feel yourself drifting without being able to see by how much), making dead reckoning harder.
Tide here is an unseen hand rather than a visible transformation; you trust your instruments over
your senses.

**NPCs present.** None human — the absence of people is core to the dread. The "presence" is the
buoys, the sounds, and the cryptids. (A smuggler or hermit NPC elsewhere might *sell the chart*
to it.)

**Authored vs simulated.** *Authored:* the (hidden) hazard layout, the bell/whistle buoy
positions, the foghorn structure, the chart of safe water. *Simulated:* the fog itself, cryptid
spawns, the unseen tidal drift, ambient sound cues.

**Role in progression.** Optional late-game capstone for the curious. **Gate: late-game
instruments** (canon) — you literally cannot navigate it until you own sounder/radar-class gear,
which ties it to the top of the boat/equipment ladder. It rewards the player who wants every fish
and the strangest corner of the world; it is intentionally skippable so it never blocks the
commerce endgame.

---

### 6.9 The Shipping Lanes — *the commerce layer* ⚓

**Identity & mood.** A different game mode layered over the same sea: the **freight and cargo**
endgame where you stop fishing for yourself and start **moving goods** along the **sea-lanes to
the mainland and the wider markets** (canon). The fantasy shifts from *skipper* to *shipping
magnate* — big ships, bulk hauls, contracts, schedules, and a fleet you command rather than
crew. The "Dynasty" end of "From Dory to Dynasty" (P2), realised as logistics.

**Physical layout & landmarks.** Not a fishing ground but a **network**: marked **sea-lanes**
(buoyed shipping routes) radiating from Port Greywick out past the Banks toward off-map
**mainland ports** and distant markets. Landmarks: **lane buoys / sea-marks** defining the
routes, **traffic separation** zones, the **Greywick freight wharf / bulk terminal**, and
**off-map port nodes** (represented as destinations on the chart / map graph rather than fully
explorable places). The big ships (freighter/tanker tiers) operate here.

**Hazards.** Commercial and logistical more than terrain: **weather along the route** (a storm can
threaten a laden freighter and a schedule), **piracy/loss risk** on long hauls (kept light and
optional, tunable), and the economics of **timing, fuel, and contracts**. The teeth here are
financial — a missed contract or a load lost to weather hurts the bottom line (P5 expressed as
business risk).

**Catch & resources (categories).** Not a catch region. It deals in **bulk goods, refined
products, and freight contracts** — the *outputs* of the fishing-and-value-add economy, plus
trade goods moved for profit. Owned almost entirely by `economy-and-business.md`; this doc only
provides the spatial lane network.

**Tide behaviour.** Largely tide-agnostic on the open routes (deep water), but **tide gates the
terminals**: big ships may need a tide window to enter/leave port (deep-draught vessels and tidal
harbours), and the Fundy Rips remain a tide-timed choke point on any route that threads them.
Tide thus stays relevant even at the top of the ladder (P1).

**NPCs present.** Freight **brokers / contract-givers** and **port agents** (likely a mix of named
NPCs at the Greywick freight office and procedural contacts at off-map ports). Your own **hired
crew/captains** run the fleet (staff, owned by `economy-and-business.md`). Other shipping traffic
as procedural extras.

**Authored vs simulated.** *Authored:* the lane network, buoy/sea-mark positions, the freight
terminal, the off-map port nodes and the routes to them. *Simulated:* contract generation and
pricing (`economy-and-business.md`), weather along routes, traffic, and fleet-automation outcomes.

**Role in progression.** The commerce **endgame and the literal payoff of the title's arc**
(P2, P4). **Gate: freighter tier + business unlocks** (canon) — you must have climbed both the
boat ladder (to freighter/tanker class) and the business ladder (warehouses, capital, reputation)
to open it. This is where "earn it, then automate it" reaches its conclusion: you have done every
job by hand, and now you direct a fleet and a freight empire that runs while you watch the tide
from the cottage in Coddle Cove.

---

## 7. Tide-revealed geography (cross-region)

Tide is the world's defining force (P1), and several regions are *literally reshaped* by it. This
section gathers the cross-cutting rules; the simulation lives in `time-tides-weather.md`.

- **St Peters Island — the tide-gated SANDBAR to Greywick** *(prologue, M2)*. As the deterministic
  tide **falls**, exposed seabed becomes **walkable** and a **sandbar between St Peters and Port
  Greywick bares as a walking path at low water** (with **boat-crossable channels that narrow as the
  tide falls**); the flood **covers it and seals the island off**. It is the **first way to reach the
  mainland — on foot, before any boat is owned**. Mechanically identical to the Drownded Lands cutoff
  (a `seabedElevation`/terrain-elevation threshold compared against the deterministic water level —
  §3.5; consumed via the Core tidal-exposure seam `IsExposed(elevation)` so terrain authoring and the
  walkability sim agree on one source of truth), tuned to be the *kindest* version — being cut off
  costs only **time**. This is the player's very first tide lesson (P1), authored before the more
  dangerous tide-gates downstream.

- **The Drownded Lands — walkable seabed.** At low water the flats drain to a **walkable
  seabed** the player can disembark onto and traverse on foot (clams, wrecks, secrets); at high
  water it floods to navigable sea. The water level is a continuous driver: terrain state is a
  function of tide height, and the **returning tide is a lethal clock** (P5). Spring lows expose
  the most and the rarest; neap lows barely uncover it.

- **The Sunkers — grounding hazard.** Submerged rocks that are safe to sail over at high water
  become **hull-holing grounding hazards** at low water. The same channel is forgiving or
  fatal depending on the tide. The Three Brothers serve as an in-world tide gauge. The Sunkers
  teach this lesson cheaply so Ironbound's reefs and the flats' cutoff channels don't have to.

- **General intertidal reveal.** Across the inshore regions, low water exposes a **foreshore /
  intertidal band** for light beachcombing and harvest (Coddle Cove beach, harbour edges,
  Sunkers shelves), and high water covers it. This is the gentle, low-stakes version of the
  Drownded Lands transformation, present everywhere as ambient P1 texture.

- **Fundy Rips — tide as a wall.** Not terrain reveal but **current**: peak tidal flow is a
  barrier that only opens at slack water. Tide here gates *passage* rather than *terrain*.

- **Implementation shape.** Each tide-driven region authors a set of **tidal-height thresholds**
  on tiles/features (e.g., "this tile is land below 1.2 m of tide, intertidal 1.2–2.4 m, water
  above"; "this channel cuts off retreat above 1.8 m"). A region's visual and walkable state is
  recomputed from the current tide height each frame/tick. See §8 and `time-tides-weather.md`.

---

## 8. Region unlock structure & pacing

The unlock order and gates are **locked in canon** (§5.3). This section explains *why the
ordering is good pacing* and how the gates combine.

### 8.1 The four gate types

Regions are gated by one or more of:

- **Story** — a narrative beat opens it (Greywick).
- **Boat capability** — your hull must be physically capable (draught for shallows, hull
  strength/seaworthiness for current and weather, range for distance). Owned by
  `boats-and-navigation.md`.
- **Skill** — navigation/seamanship skill unlocked through play (reading tide, timing slack,
  reading weather). Owned by `progression-and-housing.md`.
- **Tide knowledge / tools** — having (and being able to use) the tide table and, later,
  instruments. Owned by `time-tides-weather.md`.

Most regions combine gate types so progression feels *earned across multiple axes* rather than
bought with one currency (P2, P4).

### 8.2 The ordering and why it paces well

| Order | Region | Gate (canon) | Why here / what it teaches |
|-------|--------|--------------|----------------------------|
| 0 | St Peters Island *(prologue, M2)* | The opening (no gate) | The pre-cradle: read the tide, dig clams by hand, **walk the tide-gated sandbar to Greywick**, buy a cod licence + rod, **earn a damaged dory and pay to repair it**, sail home. The whole game's spine, in miniature. |
| 1 | Coddle Cove | Start (M1 slice) | Cradle: learn the core loop safely. |
| 2 | The Sunkers | Basic boat + reading tide | First teeth; learn grounding & tide-reading cheaply. |
| 3 | Port Greywick | Story (early) | Open the economy/social hub once you have something to sell. |
| 4 | The Drownded Lands | Tide mastery + tide table | First region requiring a *tool*; deepen tide mastery. |
| 5 | Fundy Rips | Capable hull + nav skill | Graduation gate: timing slack water; door to offshore. |
| 6 | The Banks | Offshore dragger-class boat | Industry scale; laborer→owner; bankrolls commerce. |
| 7 | Ironbound | Weather-capable boat + skill | Apex natural danger; rarest fish; weather mastery. |
| ✦ | The Smother | Late instruments | Optional uncanny capstone; navigation-by-instrument. |
| ⚓ | Shipping Lanes | Freighter tier + business unlocks | Dynasty endgame; logistics; "earn it then automate it." |

**The pacing logic, in prose.** The ramp is a clean escalation along three braided strands —
**danger, scale, and the kind of mastery demanded** — so the player is always being stretched on
exactly one new thing while standing on solid ground for the rest.

The first three regions establish the loop and the economy in safe water; the player learns to
fish, to read the tide a little, and to sell. The Drownded Lands then demands they *use a tool*
(the tide table) to be safe — the first real cognitive step up — but in a region where the
penalty for failure is mostly lost time and a soggy walk back. The Fundy Rips is the deliberate
hard gate in the middle of the game: it requires a real boat *and* real tide-timing skill *and*
nerve, and passing it is the threshold from the cozy inshore world to the dangerous open ocean —
a clean, memorable graduation. The Banks open industry-scale fishing and the laborer→owner turn,
which produces the capital and the reason to climb into the commerce tier. Ironbound and the
Smother are *parallel* late-game frontiers (one weather-mastery, one instrument-mastery) that the
player can chase in either order for the rarest content, while the Shipping Lanes provide the
*other* kind of endgame — business and automation — so late-game players who prefer logistics to
storm-fishing have an equally deep path. Crucially, the two endgames (Ironbound/Smother fishing
vs Shipping-Lanes commerce) are not sequential: a player can lean into whichever fantasy they
prefer, satisfying P2 (the ladder forks near the top, mirroring the boat tree) and P4 (you
automate the fishing economy into a freight empire only after you've mastered the fishing by
hand).

The result is that **each region introduces exactly one new dominant challenge** (grounding →
tools → current/timing → distance/weather → apex weather / instrument navigation / logistics)
while reusing the skills learned before it. No region is a difficulty cliff with no preparation,
and none is a flat repeat of the last.

---

## 9. Implementation notes

(Engineering shape only — defers to the ADRs and sibling docs for specifics.)

### 9.1 Scene-per-region strategy

- **One Unity scene per region** (additively loaded), e.g. `Scene_CoddleCove`,
  `Scene_TheSunkers`, `Scene_PortGreywick`, `Scene_DrowndedLands`, `Scene_FundyRips`,
  `Scene_TheBanks`, `Scene_Ironbound`, `Scene_TheSmother`, plus the lane network for
  `Scene_ShippingLanes`. A **persistent core scene** (player, boat, time/tide/weather sim, UI,
  audio, save) stays loaded across all of them; regions load/unload additively around it.
- **Contiguous clusters** (the inshore cluster: Coddle Cove + Sunkers + inshore Drownded Lands)
  may be authored as **one scene** or as adjacent scenes streamed without a visible load, so the
  player roams them seamlessly (§2.2). Cross-cluster passages are the scene/streaming boundaries.

### 9.2 Streaming (mobile-first)

- **Stream at the passages.** The marked channel/strait/lane that joins two clusters is where the
  next region scene is loaded and the previous one unloaded — the transit animation/duration
  covers the load. This keeps only one or two regions resident at once, respecting the mobile
  memory budget (canon §5.6).
- **Async + addressables.** Load region scenes and their content (tilemaps, habitat data, NPC
  sets) via Unity Addressables, asynchronously, with the transit acting as the loading screen.
  Pre-warm the adjacent region when the player approaches a passage to hide latency.
- **LOD the far edge.** The open regions (Banks, Ironbound, Smother, Lanes) lean on procedural
  content and sparse landmarks specifically so they are cheap to stream; the optional endless
  outer grounds generate on demand and discard behind the player.

### 9.3 How regions connect (data)

- **`MapGraph` ScriptableObject** holds the region nodes and edges from §2.3; each edge stores
  the **gate requirement**, **transition type** (contiguous / passage / set-a-course / lane), and
  the **scene reference / addressable key** to load. Both the chart UI and the set-a-course router
  read this one asset. (Data-driven content per `../adr/0003-data-driven-content.md`.)
- **Gate evaluation** is data-driven: each edge's gate is a small condition list (story flag,
  boat-capability check, skill level, tool ownership) the game evaluates to decide if the passage
  is open, blocked, or open-but-dangerous (no chart).

### 9.4 Data each region needs (as ScriptableObjects / authored data)

- **`RegionDefinition`**: id, display name, cluster, identity/mood text, scene/addressable key,
  unlock gate, role-in-progression tag.
- **Bathymetry / tide layer**: per-tile or per-feature **tidal-height thresholds** (land /
  intertidal / water bands; channel-cutoff heights) so the region's walkable & visual state can
  be recomputed from current tide height (§7). Consumed from `time-tides-weather.md`.
- **Habitat zones + spawn/category weights**: the labelled buckets the fish/loot systems fill
  (the authored↔procedural seam, §3). References categories in `fish-and-content.md`.
- **Loot/flotsam/tide-pool tables** for procedural detail (clams, secrets, salvage, beachcombing).
- **Landmark & POI list**: positions and types of authored landmarks (lighthouse, wreck, buoys,
  named sunkers, the Gut, etc.), including non-visual landmarks for the Smother (bell/whistle
  buoys, foghorn).
- **NPC anchor set**: which named NPCs and which "extras" templates inhabit the region and where
  their home/work anchors are (consumed from `npcs-and-routines.md`).
- **Chart asset**: the authored channel/hazard/contour data a purchased sea-chart reveals (the
  "buy a chart" layer, §4.1), distinct from the fog-of-war "go look" layer.
- **Hazard config**: grounding/current/weather/fog parameters for the region (consumed from
  `boats-and-navigation.md` and `time-tides-weather.md`).

---

## 10. Open questions / decisions deferred

- **Cluster boundaries — exact scene partition.** Is the whole inshore cluster (Cove + Sunkers +
  inshore Drownded Lands) one scene, or three streamed-without-load scenes? Depends on mobile
  memory profiling once art is in. (§9.1) — defer to a prototyping spike.
- **Set-a-course vs hand-sailing balance.** Exactly which regions allow fast-travel, after which
  unlock, and how much of the journey is auto-resolved vs played. Needs playtesting to find where
  manual sailing stops being romantic and starts being a chore (P4 vs P1 tension). (§2.2)
- **Off-map mainland ports — how represented?** Are the Shipping-Lanes destination ports purely
  abstract nodes (a name + a market on the chart) or lightly visitable? Leaning abstract for
  scope; confirm with `economy-and-business.md`. (§6.9)
- **The endless outer grounds — where exactly does it attach,** and does it sit off The Banks, off
  Ironbound, or both? And how does it reveal on the chart (it can't be a fixed authored chart)?
  Coordinate with canon §5.7 and `fish-and-content.md`. (§6.6)
- **The Smother's degree of menace.** How far toward *Dredge*-style unease do we push it without
  breaking the cozy-first promise (P5 / anti-pillar "not a grim survival sim")? A tone dial to set
  with the art/audio bible. (§6.8)
- **Do any regions change with season** (not just tide/weather)? e.g., ice/cold affecting Ironbound
  or the Banks in winter, seasonal fish runs through the Rips. Canon has four seasons; how strongly
  they reshape *geography* (vs just fish availability) is open. Coordinate with
  `time-tides-weather.md` and `fish-and-content.md`.
- **Greywick harbour tide convenience.** Confirmed-deliberate that the home market never strands
  you (§6.3), but should *extreme* spring lows still mildly inconvenience deep-draught freighters
  at the bulk terminal (tying P1 to the commerce tier)? Likely yes; confirm with
  `economy-and-business.md`. (§6.9)
- **Named-place inventory.** This doc names many landmarks (Three Brothers, the Gut, the Boiling
  Ground, the bones, etc.). Final naming pass for player-facing consistency and localization, in
  step with `npcs-and-routines.md` and the narrative pass.
