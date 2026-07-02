# Hidden Harbours — Vision & Pillars (CANON)

> This is the anchor document. Every other design doc, agent, and line of code must stay
> consistent with the names, scales, and decisions locked here. If something here needs to
> change, change it **here first**, then propagate. When in doubt, this file wins.

---

## 1. Logline

> Start with two hands and a tide table on a hard, beautiful stretch of the Atlantic Canadian
> coast. Dig clams at low water, earn your first boat and put it right, read the tides, the wind,
> and a market that never sits still, and work your way up from hauling handlines by hand to
> commanding a cargo fleet.

> **Opening note (owner-ratified 2026; canon below in §5.8).** You begin on **St Peters Island**,
> dig clams on the bared low-water flats, and **earn — then repair — your first dory** at the
> Greywick shipwright before you ever sail it home to the cove. The earlier "inherit your uncle's
> dory" framing has been **retired in favour of an earned/repaired dory**; Uncle Ned remains the
> game's departed emotional anchor (the cottage, the memory), but the boat is now *bought and
> mended*, not handed down (§5.8). *This is a phased **M2** opening built as a greybox prototype —
> a deliberate pull-forward, not scope-creep.*

A cozy-but-dangerous fishing-and-trade RPG. Stardew Valley's warmth and daily rhythm, the
painterly atmosphere of Kingdoms Two Crowns, and the ownership/automation depth of Schedule I
and Big Ambitions — set on a working North Atlantic coast.

## 2. Fantasy & tone

You are not a hero. You are a young skipper who inherited very little and a lot of weather — a
cottage, a memory, and a tide that decides what you can do today. You don't even start with a
boat: you dig clams by hand, earn your first dory, and pay to put it right before you can sail it.
The island is beautiful, the work is real, and the sea does not care about you. Success feels
*earned*: the first hold of clams that buys back a broken dory, and the first hold of cod you
caught by hand, are both genuine triumphs;
the first time you ground your boat on a falling tide and have to wait, exposed, for the tide
or a tow, is a genuine gut-punch. Warm, weathered, salt-stained, hopeful.

## 3. The Five Pillars

Every feature must serve at least one pillar. If it serves none, cut it.

| # | Pillar | What it means | What it forbids |
|---|--------|---------------|-----------------|
| P1 | **The Sea Has Moods** | Tide, wind, weather, season and the 24h clock are *real forces you read and respect*, not set dressing. The same cove is a different place at high vs low tide, calm vs gale. | Static, "always the same" water. Weather as pure cosmetics. |
| P2 | **From Dory to Dynasty** | A long, legible ladder of scale: start hauling line by hand in a borrowed dory; end directing a fleet and shipping freight. Growth is *tangible* — bigger boats, more reach, more capability. | Flat progression. Power that isn't visible in the world. |
| P3 | **A Living Working Coast** | NPCs keep routines, the town runs on its own rhythms, the market breathes with supply and demand. The place feels inhabited and reactive, with or without you. | Quest-dispenser NPCs who stand still. A market with fixed prices. |
| P4 | **Earn It, Then Automate It** | You do every job by hand first so it has weight. Then you hire crew and build infrastructure to automate the tedium, shifting from *laborer* to *owner*. | Automation handed out for free. Busywork with no path out of it. |
| P5 | **Cozy, but with Teeth** | Stardew comfort, but the sea is genuinely dangerous: run aground, capsize, lose a load, get lost in fog, get stranded until help comes. Risk gives the coziness stakes. | Frictionless safety. Danger so punishing it stops being cozy. |

## 4. References (and how we use them)

- **Stardew Valley** — structure, readability, daily loop, NPC relationships, cozy clarity. *We borrow its skeleton.*
- **Kingdoms Two Crowns** — painterly parallax, atmospheric light, limited palette, the romance of a small craft against a big world. *We borrow its mood and light.*
- **Schedule I** — owning the supply chain, hands-on-then-delegated operations, logistics as gameplay. *We borrow its ownership depth.*
- **Big Ambitions** — property empire, hiring/managing staff, errands-to-enterprise arc. *We borrow its business scaffolding.*
- **Dredge** (adjacent) — the unsettling, weather-driven side of fishing. *We borrow its sense of menace at sea.*

## 5. Locked Canon — Quick Reference

### 5.1 Title & setting
- **Title:** Hidden Harbours
- **World:** The **Sablewick Banks** — a small fictional archipelago off Atlantic Canada (a blend of Nova Scotia / Newfoundland / Bay of Fundy). Cold North Atlantic, fog, enormous tides, lighthouses, weathered clapboard, lobster traps, working wharves.
- **Vernacular is canon flavor:** use real Maritime/Newfoundland coastal words — *sunkers* (submerged rocks), *rips* (tidal current races), *dory, punt, longliner, dragger, the banks, nor'easter, drownded* (flooded). Keep it warm, not cartoonish.

### 5.2 Perspective & art scale (see `design/art-and-audio-bible.md`)
- **Canonical camera:** ¾ top-down (Stardew base), with Kingdoms-Two-Crowns-style painterly parallax, water, and lighting for mood. **One perspective everywhere** — land, town, and on-water.
- **Scale standard (LOCKED):** **PPU = 32**, **1 world unit = 1 metre**, **base tile = 32×32 px = 1 m²**.
- **Humans** render at a slightly heroic ~1.8 m for readability. **Boats are sized in real metres** so the ladder *reads physically* on screen — a tanker genuinely dwarfs a dory. Constant PPU is what sells the scale fantasy (P2).

### 5.3 Regions (LOCKED names — a prologue home-island + 7 core + commerce layer). Detail in `design/world-and-regions.md`.
| Order | Region | Identity | Gate / why locked |
|-------|--------|----------|-------------------|
| 0 | **St Peters Island** *(prologue; owner-ratified, phased **M2**)* | Your **home island**, cut off from the mainland **except at low tide** (a pure P1 tide-gate). A village of 3 houses + a school + a general store. Dig clams at low water on the island flats; at low water a **tide-gated sandbar** to **Greywick** bares as a walking path; there you buy a **cod licence** + a **rod**, sell clams, and **save to buy a damaged dory at the Greywick shipwright — then pay to repair it**. **Prepends** Coddle Cove; does not replace it. | The new opening (built **M2**); see §5.8 |
| 1 | **Coddle Cove** | **Home base you sail to after repairing the dory** — uncle's cottage + wharf. Sheltered tutorial waters, inshore beginner fish. | Start zone for the **M1 vertical-slice**; in the full arc it is the **home you arrive at** once the dory is yours (see §5.8) |
| 2 | **The Sunkers** | Tidal reef field of submerged rocks; rich tide pools & shellfish; grounding hazard at low water. | Basic boat + reading tide |
| 3 | **Port Greywick** | The market town: auction house, shops, shipwright, housing, most named NPCs. | Story unlock (early) |
| 4 | **The Drownded Lands** | Vast tidal flats that become *walkable seabed* at low tide (clams, wrecks, secrets); flood at high. | Tide mastery + tide table |
| 5 | **Fundy Rips** | Narrows with ferocious tidal currents; must time the tide to transit; fast pelagic fish. | Capable hull + navigation skill |
| 6 | **The Banks** | Open offshore grounds; deepwater groundfish & big pelagics. | Seaworthy offshore boat (dragger-class) |
| 7 | **Ironbound** | Cold, storm-lashed outer islands; end-game grounds, dangerous weather, rare/legendary fish. | Weather-capable boat + skill |
| ✦ | **The Smother** (late/optional) | Permanent fog bank; navigate by instrument and sound; eerie, cryptid fish. | Late-game instruments |
| ⚓ | **The Shipping Lanes** (commerce layer) | Where freight/cargo gameplay and the big ships operate; routes to the mainland and wider markets. | Freighter tier + business unlocks |

### 5.4 Boat ladder (LOCKED — "Dory to Dynasty"). Detail + stats in `design/boats-and-navigation.md`.
Every tier defines: **length (m)**, **draught (m)** *(how shallow it can go before grounding — ties to tide!)*, **hold capacity**, **crew slots**, **range**, **seaworthiness** *(weather tolerance)*, **handling** *(wind/current vulnerability)*.

| Tier | Boat | ~Length | Role |
|------|------|---------|------|
| 0 | **The Dory** (bought damaged at Greywick, then repaired) | ~4.5 m | Starter. Oars + small outboard. Tiny hold, inshore only, very tide/wind-vulnerable. *Earned with clam money and put right by the shipwright — no longer inherited (§5.8).* |
| 1 | **Punt / Skiff** | ~6 m | First purchase. A little more capacity and reach. |
| 2 | **Cape Islander** (inshore longliner) | ~13 m | The iconic Maritime workboat. Real range, lines & traps, mid capacity. |
| 3 | **Lobster Boat** (specialist) | ~12 m | Shellfish/trap specialist branch. |
| 4 | **Side Dragger / Trawler** | ~25 m | Offshore nets, big hold. First boat that can safely work The Banks. |
| 5 | **Stern Trawler / Seiner** | ~38 m | Larger offshore, weather-capable; reaches Ironbound. |
| 6 | **Coastal Packet / Freighter** | ~60 m | Begins the cargo/commerce tier. Bulk hauling & freight contracts. |
| 7 | **Coastal Tanker / Cargo Ship** | ~110 m | End-game logistics. Fleet command, freight empire. |

> The ladder is a *tree near the top*, not a straight line: lobster specialist vs offshore
> trawler are parallel branches before they converge into the commerce tier.

### 5.5 Core systems at a glance (each has its own design doc)
- **Time:** 24-hour clock; one in-game day ≈ 18–24 real minutes (tunable). Four seasons. → `design/time-tides-weather.md`
- **Tides:** semidiurnal (two highs / two lows per tidal day), spring/neap cycle on a ~28-day moon. Tide height drives water level, grounding, and seabed access. A readable **tide table** is a core tool. → `design/time-tides-weather.md`
- **Weather & wind:** wind vector + sea state + fog + storms; wind and tidal current apply *physical forces* to boats. Forecasts exist (barometer, harbourmaster, radio). → `design/time-tides-weather.md`
- **Fish:** **100 species** as data assets (not hand-coded), spanning inshore, shellfish, pelagic, flats, deepwater, storm-grounds, and legendary. → `design/fish-and-content.md`
- **Economy:** supply-and-demand market at Greywick; storage to time sales; refine/manufacture for value-add; ship to distant markets; hire & manage staff to automate. → `design/economy-and-business.md`
- **NPCs:** handcrafted named core cast with routines + procedurally varied "extras" for crowds/crew. → `design/npcs-and-routines.md`
- **Housing:** start at uncle's cottage; buy / upgrade / refurnish homes, plus commercial property (warehouses, plants, shops). → `design/progression-and-housing.md`

### 5.6 Platform & engine (see `adr/0001-engine-choice.md`, `adr/0005-pc-first-target.md`)
- **Engine:** **Unity 6.5**, 2D URP.
- **Target:** **PC-first** (Windows/desktop primary; **landscape**; **keyboard/mouse + gamepad**), with **mobile (iOS/Android) kept as a viable later port** (and console after). This is a *presentation/target* decision, not a design change — the five pillars and all gameplay/sim/economy/content are unchanged. Recorded in `adr/0005-pc-first-target.md`, which supersedes the earlier "mobile-first" framing. Input stays an **intent abstraction** (VS-02): KB/mouse/gamepad become the primary bindings, touch is retained for the port — *bindings retarget, gameplay does not*.
- **Language:** C#. **Content is data-driven** via ScriptableObjects (see `adr/0003-data-driven-content.md`).

### 5.7 Procedural vs handcrafted (see `adr/0002-procedural-vs-handcrafted.md`)
- **Handcrafted:** region macro-layouts, landmarks, the named core NPC cast, quests, the town.
- **Procedural / simulated:** fish spawn distribution, tide-pool & flats contents, weather, wind/current fields, flotsam, "extra" background NPCs, and an optional endless outer-banks zone for late-game.
- **Principle:** *authored where identity and story live; procedural where scale and variety live.*

### 5.8 Story & integration clarifications (resolved open questions)

These resolve questions raised while writing the system docs. They are now canon.

- **Uncle Ned is the departed figure who anchors the opening — but the dory is now EARNED, not inherited (owner-ratified 2026).** Ned remains the game's emotional origin: you inherit his **cottage** in Coddle Cove and carry his memory, and the tone stays **bittersweet and hopeful, not grim**. **What changed:** the owner has **dropped the "inherit Uncle Ned's dory" framing.** Your first boat is now a **plain damaged dory you buy at the Greywick shipwright with clam money and pay to repair** — you *earn* the boat (a stronger P4 "do it by hand first" beat), and only the cottage + the memory are handed down. Ned is still present at the very start (a remembered moment / his letter); his dog-eared logbook — the **"Ned's Unfinished Lines"** framing questline — together with **Aunt Ginny** still teaches the loop and introduces the town. His memory is carried by the cast (P3). **Reconciliation flag (do not lose this):** the already-built M1 onboarding (`M1-08` / `VS-21`: Ned's logbook + "inherit the dory" at Coddle Cove) is now **partly invalidated** — the inherited-dory beat must be **reworked into a buy-and-repair beat** when St Peters is built. That rework is a **later task**, explicitly logged here and in `design/npcs-and-routines.md` §3.1 so the change is not silently dropped. Full treatment: `design/npcs-and-routines.md` §3.1.
- **The opening arc (owner-ratified 2026, now DECIDED): St Peters Island → [walk the tide-gated sandbar] → Port Greywick → [buy + repair the dory] → Coddle Cove (home base).** *(**Phased to M2**, built as a **greybox prototype** — a deliberate pull-forward of the M2 opening, **not** scope-creep — see `roadmap.md`. It is an onboarding/structure addition that *serves* the pillars, not a change to any pillar or system.)* The decided sequence:
  1. **St Peters Island** — a small **home island cut off from the mainland except at low tide** (a pure P1 tide-gate): three houses, a one-room **school**, a **general store**, and your late uncle's broken-down dory on the slip *(see the rework note below)*. An **aunt** teaches the basics (compass + hand skills). You **dig CLAMS at low water** on the bared island flats with a **shovel** (the tell: "two squirting holes" in the wet sand) — the first "catch," **before any rod**. *(Clams are licence-gated — the clam licence is owned by `economy-and-business.md`'s licence system, not here.)*
  2. **The tide-gated sandbar to Greywick** — as the deterministic tide **falls**, exposed seabed becomes **walkable**, and a **sandbar between St Peters and Greywick bares as a walking path at low water** (with boat-crossable channels that **narrow as the tide falls**). This is the **first way to reach the mainland — on foot at low tide** (see `design/time-tides-weather.md` §3.5 and `design/world-and-regions.md` §7). Dawdle past the turn of the tide and the flood seals you out until the next low — P5 at its kindest.
  3. **Port Greywick** — buy a **cod fishing LICENCE** (a real, minimal licence — owned by `economy-and-business.md`) and a fishing **ROD**; **sell your clams** and **save to buy a DAMAGED DORY at the Greywick shipwright, then PAY THE SHIPWRIGHT TO REPAIR it** to usable. The dory is a **plain bought-and-repaired boat** — *not* inherited (see the Ned bullet above).
  4. **Coddle Cove** — once the dory is yours and seaworthy, you **sail it home to the cove** (the uncle's cottage + wharf), which becomes the **home base** for the rest of the game.

  St Peters **prepends** the Coddle Cove opening; it does **not** delete it — Coddle Cove stays central and **everything built for it is reused** (it is just **reordered** later in the arc). **The current M1 opening — Aunt Ginny + Ned's logbook at Coddle Cove (backlog `M1-08` / `VS-21`) — remains the M1 stand-in:** when St Peters is built (M2) the *start and onboarding relocate* to the island and the **dialogue/onboarding system is reused, not rebuilt**. **Reconciliation flagged for the M2 pass (do not silently resolve here):** (a) the inherited-dory beat in VS-21 must be **reworked to a buy-and-repair beat** *(the Ned bullet above)*; (b) where **Aunt Ginny** lives across the arc (St Peters school vs Coddle Cove hearth) is settled with `design/npcs-and-routines.md` when M2 is scheduled — intent is **one teaching-aunt** placed to serve the arc.
- **Calendar:** a **7-day week** (six working days + one rest day; one weekday is **Market Day** at Greywick). The weekday is owned by `design/time-tides-weather.md` alongside the clock and seasons; NPC routines and the market key off it.
- **Seasons (named):** **Early Spring, High Summer, The Turn** (autumn), **Hard Winter** — 28 days each.
- **Sea-state scale (calm→storm):** **Glass, Calm, Light, Moderate, Lively, Rough, Gale, Storm.**
- **Currency & units:** money is **₲** (informally "coin"); **HU** = hold units (capacity); **FU** = fuel units. Used consistently across the boats and economy docs.
- **Stamina:** a **light energy system** (cozy-genre standard). Hand labour (rowing, hauling, fishing, processing) draws energy; rest, sleep and food restore it; a comfortable home gives a morning buff. Crucially, **hiring staff and automating work removes the energy ceiling as you scale** (P4). Intentionally gentle — never punishing. Owned jointly by `design/time-tides-weather.md` and `design/progression-and-housing.md`.
- **Greywick harbour is deliberately deep/dredged** so the market hub never strands you — an intentional exception to P1's "tide is always a force." Deep-draught freighters at the bulk terminal may still want a tide window.

## 6. What this game is NOT (anti-pillars)

- Not a twitchy action game. Tension comes from weather, tide, and risk management, not combat.
- Not a pure idle/automation game. You earn mastery by hand before you delegate (P4).
- Not a grim survival sim. It's cozy first; danger is seasoning, not the meal (P5).
- Not an open-ended procedural blob. The world has authored identity and a sense of place (P3).

## 7. Scope honesty (read this, non-negotiable)

This is a **big** vision — easily several years of work if built all at once. The roadmap
(`roadmap.md`) deliberately sequences it so there is a *fun, shippable game at every stage*,
starting from a tiny vertical slice (the fishing→sell loop in Coddle Cove). **We build the
smallest delightful thing first and grow it.** Any agent or contributor who proposes building a
later-phase system before its phase is reached should be redirected to the roadmap.
