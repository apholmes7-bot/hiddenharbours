# Hidden Harbours — Roadmap

> **Status:** Production roadmap. Subordinate to [`vision-and-pillars.md`](vision-and-pillars.md) (CANON).
> This document sequences the canon vision into shippable milestones. It does **not** add new design — it
> *phases* the design that already lives in [`design/`](design/). When this doc and canon disagree, canon wins.
>
> **Companion backlogs:** [`../backlog/backlog.md`](../backlog/backlog.md) (the full seed backlog, M0–M4) and
> [`../backlog/milestone-1-vertical-slice.md`](../backlog/milestone-1-vertical-slice.md) (the detailed M0→M1 spec).
>
> **Pillars (shorthand used throughout):** **P1** The Sea Has Moods · **P2** From Dory to Dynasty ·
> **P3** A Living Working Coast · **P4** Earn It, Then Automate It · **P5** Cozy, but with Teeth.

---

## 0. Scope reality — read this first (a blunt, kind note to the owner)

You have written a **genuinely great, genuinely huge** design. Nine production-grade design docs, 100 fish,
eight boat tiers, nine regions, a living market, a staff/automation layer, weather fronts, fleet logistics.
Built all at once, that is **several years** of work — canon already says so ([`vision-and-pillars.md`](vision-and-pillars.md) §7).
That is not a problem. It only becomes a problem if we try to build it in the order it was *written* instead of
the order it should be *played into existence*.

Here is the honest situation and how we handle it:

- **We do not build the design top-to-bottom. We build it as a series of small, complete, fun games** that each
  grow into the next. The first one is tiny — one cove, one little boat, six fish, one buyer — and it must be
  *fun on its own* before we add anything.
- **The golden rule, never broken:** **there is always a playable build.** At the end of every milestone (and
  ideally every week inside one) you can open the game on a phone and *play the loop*. We never enter a state
  where "it'll be playable again in three months." If a feature can't be added without breaking the playable
  build for long, it gets sliced smaller.
- **Why slice this hard?** Three reasons. (1) **Fun is discovered, not designed** — the fishing→sell loop is
  either satisfying in your hand or it isn't, and you can only find out by playing a rough version *early*,
  before tons of art and systems are poured on top. (2) **Performance-first** — every system has to earn its
  frame budget against the **PC-first desktop baseline** (60fps on a typical desktop/laptop GPU), and that's only
  knowable by building and profiling, not by planning. *We keep the mobile-portability discipline (pooling,
  draw-call/texture-memory budgets) so the later mobile port stays cheap — see ADR 0005.* (3) **Money and morale**
  — a playable slice is something you can show, soft-launch, and decide
  *whether this game is worth the multi-year climb* before you've spent the years.
- **The owner's job is to steer with go/no-go calls, not to write code.** Each milestone ends with a clear
  decision point (see §6 "How the owner steers"). The single most valuable thing you can do is **play the M0 loop
  yourself, repeatedly, and be honest about whether it's fun** — because everything else is built on it.

> **The one-sentence strategy:** *Build the smallest delightful thing (the cove, the dory, the catch, the sale),
> make it genuinely good, then grow it one ring outward at a time — and never stop being able to press Play.*

**What this means in practice for the AI agent team:** any agent that proposes building a later-phase system
before its milestone is reached should be redirected here. The boat-physics force model, the 100-fish content
fill, the staff automation layer, the freight economy — all are *designed* and *waiting*, but they are built
**in milestone order**, each only when the milestone before it is playable and fun.

---

## 1. The milestones at a glance

| Milestone | Name | Goal in one line | Shippable? | Fun lives here |
|---|---|---|---|---|
| **M0** | **Greybox Prototype** | Prove the fishing→sell loop is fun with placeholder art. | Internal only | The core loop: read tide → catch by hand → sell before it spoils. |
| **M1** | **Vertical Slice — "Coddle Cove"** | Make that loop *genuinely good* in one real region. | **Yes — soft-launch candidate (Steam / itch.io closed playtest)** | "Is this game worth making?" One beautiful cove, real wind/tide on the dory, Ned, the Punt. |
| **M2** | **The Working Coast** | Expand to a living inshore+mid coast with danger and the first business steps. | Yes (Early-Access shape) | Grounding/rescue teeth, more regions/boats, supply-and-demand, storage, refining, NPC routines. |
| **M3** | **Offshore & Enterprise** | Open the Banks and turn laborer into owner. | Yes | The offshore industry; the *first* automation (a staffed second boat); contracts. |
| **M4** | **Dynasty** | Fleet command, freight empire, the full content fill, multi-platform. | Yes (1.0) | Directing a freight empire; Ironbound/Smother capstones; all 100 fish; desktop/console. |

The progression of milestones deliberately mirrors the **world gradient** (sheltered/shallow/forgiving →
open/deep/lethal) and the **boat ladder** (dory → tanker). We sail *up the difficulty curve as we build outward*.

---

## 2. The phases in detail

Each milestone below specifies: **Goal · Player experience at end of phase · Systems built (mapped to agent
roles) · Explicitly deferred · Pillars advanced · Shippable? / Fun here?**

---

### M0 — Greybox Prototype

> **The question this answers:** *Is the core loop fun with the lights off?* If catching a fish and selling it
> isn't satisfying with coloured rectangles for art, no amount of pixel-art will save it. This is the cheapest,
> most important experiment in the whole project.

**Goal.** Prove the **fishing→sell loop** is fun, with placeholder art, in a greybox Coddle Cove.

**Player experience at end of phase.** You spawn on a greybox wharf beside the cottage. A clock runs and a
simple tide rises and falls (you can read it on a basic HUD). You board the dory, putter out into the cove with
simple movement, drop a handline over a fishing spot, play a cozy one-thumb tension/landing mini-interaction, and
land one of ~6 fish. You bring a small hold of fish back to the wharf, sell it to one buyer whose price moves a
little as you sell, watch your coin go up, and feel the small triumph of a hold sold. You can sleep to the next
day, and your money/day persist across a quit-and-relaunch (save/load works). The loop is repeatable in a few
minutes. **No town, no physics-based wind, no grounding, no NPCs.**

**Systems built (by role).**

| System | Owner role |
|---|---|
| Unity 6.3 project bootstrap (2D URP, mobile build target), asmdef module layout, Git LFS, persistent-core + additive-scene scaffold | `lead-architect` |
| Input System with **intent abstraction** (`Move`, `SetThrottle`, `SetHeading`, `Interact`, `Haul`) — touch first | `lead-architect` |
| Save/load scaffold (versioned schema; save = `{seed, gameTime, playerState}`) | `lead-architect` |
| `EnvironmentService` v0: `gameTime` (double) clock + **deterministic semidiurnal tide** (the §3.4 formula), `OnDayRollover`, sleep/skip | `gameplay-systems` |
| Dory controller v0 (simple top-down move + a follow-cam) — *kinematic, not yet the force model* | `gameplay-systems` |
| Fishing interaction v0: spot → bite → tension band + landing gauge + strain bar (cozy, one-thumb) | `gameplay-systems` + `ui-ux` |
| `FishSpecies` ScriptableObject schema + **6 Coddle Cove species** as data | `economy-sim` / `world-content` |
| Catch resolver v0 (context → weighted table → one fish + size roll) | `economy-sim` |
| One **Fish Buyer** at the wharf + the supply/demand price formula (single market, sliced sells) | `economy-sim` |
| HUD v0: clock + **tide gauge** (rising/falling, height, time-to-turn) + money + hold fullness | `ui-ux` |
| Greybox Coddle Cove scene: wharf, cottage (sleep + save point), dory mooring, a stretch of water, a few fishing spots | `world-content` |
| Placeholder-art convention + Unity import settings locked (PPU=32, Point, no compression) | `art-pipeline` |
| Minimal ambient audio bed (calm sea + gulls) so the cove feels alive | `audio` |
| Greybox playtest harness + the M0 acceptance pass | `qa-test` |
| (Light) an in-editor tide/clock inspector so designers can scrub time | `tools-editor` |

**Explicitly deferred.** Port Greywick and *all* town/economy depth; the boat **force model** (wind/current as
physical forces) — M0 uses simple movement; grounding/capsize/rescue; weather fronts/fog/storms; auction,
contracts, processing, storage; NPCs and dialogue; the Punt and the rest of the boat ladder; real art; the other
8 regions; map/charts; licenses/proficiencies/reputation; offline accrual.

**Pillars advanced.** **P1** (tide is real and readable, even if simple) · **P4** (you catch *by hand* — the
foundation of earn-it-then-automate-it) · the seed of **P5** (selling before it spoils is a gentle clock).

**Shippable?** No — internal only. **Fun here?** This is *the* fun-check milestone: if the loop isn't fun in
greybox, stop and fix the loop before anything else. Do not proceed to M1 until the answer is "yes, I keep
wanting one more run."

---

### M1 — Vertical Slice ("Coddle Cove")

> **The question this answers:** *Is this game worth making?* M0 proved the loop works; M1 makes one slice of it
> genuinely, shippably **good** — the candidate for a first soft-launch / TestFlight. This is the milestone the
> whole project is currently pointed at; its full spec is [`../backlog/milestone-1-vertical-slice.md`](../backlog/milestone-1-vertical-slice.md).

**Goal.** Take the M0 loop and make it *good* in one real, art-passed region — with the signature P1 feel (wind
and tide as forces you read), a couple of named NPCs including **Uncle Ned**, the first taste of the market town,
and the first boat purchase (the **Punt**).

**Player experience at end of phase.** Coddle Cove now *looks and sounds* like the canon home harbour — painterly
¾ water, a tide-aware shoreline that visibly moves, warm light that shifts with the time of day, gulls and hull
slap. You arrive to your late **Uncle Ned's** cottage and his inherited dory; his dog-eared logbook (the
*"Ned's Unfinished Lines"* framing) and **Aunt Ginny** teach the loop through a gentle, bittersweet-but-warm
onboarding. *(M2 note: the owner has since **dropped the inherited-dory framing** — in the full arc the dory is
**earned and repaired** at Greywick — so this M1 onboarding's inherited-dory beat is **flagged for rework** when St
Peters lands; the cottage stays inherited. Canon §5.8.)* Out on the water the dory now feels like a *boat*: wind pushes you, the tide sets you, you carry way
and you crab into a breeze — the [`boats-and-navigation.md`](design/boats-and-navigation.md) force model is live
(at the inshore, forgiving end). The **tide table** is a readable tool and the HUD tide/wind widgets are
first-class. You can make a short hop to **Port Greywick** where market basics work (the Fish Buyer/auction spot
price, a chalkboard of prices that move), and once you've earned a stake you buy your first boat, the **Punt**,
from the shipwright — the "I'm a real fisher now" beat. The whole thing is tuned for **3-minute and 30-minute**
sessions and saves/resumes anywhere.

**Systems built (by role).**

| System | Owner role |
|---|---|
| **Boat force model v1** (engine thrust + anisotropic hydro-drag + windage + rudder authority; the dory as a planar rigid body) consuming `EnvironmentSample` | `gameplay-systems` |
| `EnvironmentService` v1: wind field (smooth noise + prevailing), sea-state value, tide table generation, the full FORCES `EnvironmentSample` struct | `gameplay-systems` |
| Boat as composable components (Hull/Engine/Hold/Gear) — minimal, to support Dory→Punt | `lead-architect` + `gameplay-systems` |
| The **Punt** as the first purchasable boat; a minimal **Shipwright** buy flow | `gameplay-systems` + `economy-sim` |
| Coddle Cove **art pass** (tile-aware shoreline, water shader/anim, day-night grade, cottage, wharf, dory & Punt sprites at true metric scale) | `art-pipeline` |
| A first, small **Port Greywick** scene (wharf, buyer, shipwright, a couple of buildings) — services, not a full town | `world-content` |
| **Uncle Ned** + 1–2 other named NPCs: static or lightly-anchored, with dialogue + the onboarding flow | `world-content` + `ui-ux` |
| HUD v1: bespoke **tide gauge + wind widget + compass**, conditions cluster, market chalkboard, sell screen (marginal-price slider) | `ui-ux` |
| Market basics: Fish Buyer + a simple auction/spot price at Greywick; per-commodity demand & recovery over days | `economy-sim` |
| Reactive ambient + adaptive music v1 (region beds, rising-wind tell, catch sting, "made it home" warmth) | `audio` |
| Save schema v1 (boat owned + components, money, day, Ned/onboarding flags) | `lead-architect` |
| Tide-table / environment editor tooling; placeholder→real art swap workflow | `tools-editor` + `art-pipeline` |
| Slice acceptance + a small external **playtest** (the soft-launch readiness pass) | `qa-test` |

**Explicitly deferred.** Grounding/capsize/rescue (M2 — the cove is still forgiving); weather fronts, fog, storms
(M2); supply/demand *depth*, storage, refining, contracts (M2); NPC daily *routines* (M1 NPCs are anchored, not
yet on schedules); the Sunkers/Drownded Lands/Rips and beyond (M2+); Cape Islander and up (M2+); licenses,
proficiencies, reputation as systems (M2 — onboarding may *fake* the first license narratively); housing decor
(M2); offline accrual (M3-ish).

**Pillars advanced.** **P1** in full inshore form (wind + tide as forces you read, the tide table as a tool) ·
**P2** (the first rung of the boat ladder — Dory→Punt — visible on screen) · the warm opening of **P3** (Ned, a
glimpse of Greywick) · **P5** as gentle seasoning (the cove still won't kill you, but spray, wind, and spoilage
give the coziness stakes).

**Shippable?** **Yes — this is the soft-launch candidate (a Steam / itch.io closed playtest; mobile = later port).**
It is a complete, polished, small game: inherit the dory, learn the cove, fish, sell, buy the Punt. **Fun here?** If M0 was "does the loop work," M1 is
"does the *world* make me want to live in it." A "go" here greenlights the multi-year build; a "no" tells you to
keep iterating on the slice (cheaply) rather than pour years into M2+.

---

### M2 — The Working Coast

> **The question this answers:** *Does the world have teeth, breath, and a reason to grow?* M2 turns a lovely
> demo into a game with stakes (danger), a heartbeat (NPC routines + a breathing market), and a growth ladder
> (more regions, boats, the first business steps).

**Goal.** Expand from one cove to a **living inshore-and-mid coast**: more regions, more boats up to the Cape
Islander, the *proper* supply/demand market, storage and the first refining, NPC routines, housing upgrades, and
the first real **danger** — grounding, capsize, stranding, and rescue.

**Player experience at end of phase.** The inshore cluster opens up: **The Sunkers** (reef field, the first
grounding teeth — read the tide or hole your hull), then **The Drownded Lands** (the walkable-seabed wonder,
gated behind owning and reading a tide table) and the **Fundy Rips** as a tide-timed graduation gate. **Port
Greywick** becomes a real town — auction house, shops, the shipwright, the harbourmaster, named NPCs keeping
**daily routines**. The market now genuinely *breathes*: an NPC fleet lands fish, gluts and scarcity happen
without you, prices recover over days, and you learn not to dump your whole catch at once. You can **store** to
time sales, run a **first refining** step (salt cod / smoked herring) to beat spoilage, **upgrade the cottage**
and decorate it, climb to the **Cape Islander** (and choose the Lobster-Boat branch), and — crucially — get into
and out of **trouble**: run aground on a falling tide and wait for the water, swamp a tender dory you overloaded,
break down, and call (or wait for) a tow. The teeth arrive, kindly.

**Systems built (by role).** Danger model — grounding (draught vs water depth), broach/capsize (sea-state vs
seaworthiness vs load), taking-on-water, breakdown/fuel, and the **rescue/tow** set-piece (`gameplay-systems`).
Weather & wind v2 — fronts, fog, storms, the forecast tools: barometer, harbourmaster, radio (`gameplay-systems`).
Regions: The Sunkers, The Drownded Lands (tide-driven walkable seabed), Fundy Rips, and a built-out Port Greywick
(`world-content`). Tide-revealed geography + per-region seabed heightfields (`world-content` + `tools-editor`).
Boat ladder to Cape Islander + Lobster Boat branch; boat upgrades/instruments/safety as component swaps at the
shipwright (`gameplay-systems` + `economy-sim`). Full supply/demand market sim, auction house, NPC-fleet landings,
multiple buyers, first standing contracts (`economy-sim`). Storage (ice/well, cold storage) + perishability +
**first processing facilities** (salt house, smokehouse) (`economy-sim`). NPC routines + relationships + named
core cast for Greywick; dialogue system v2 (`world-content`). Housing: cottage refit + decor/Comfort + storage;
the licenses/proficiencies/reputation currency systems come online here (`world-content` + `ui-ux`). Art passes
for each new region + the danger/weather VFX + bigger boats (`art-pipeline`). Audio: storm/fog/region beds, danger
cues, town hum (`audio`). Map/chart UI + fog-of-war reveal + the tide table tiers (`ui-ux`). Save migration from
M1; performance pass for scene streaming at passages (`lead-architect` + `qa-test`).

**Owner-ratified additions folded into M2 (2026 — see [`../backlog/backlog.md`](../backlog/backlog.md) M2 epics
and the design docs).** The **St Peters Island opening** — the tide-gated home-island prologue, **built first as a
greybox prototype** and now a **decided arc**: dig clams by hand → **walk the tide-gated sandbar to Greywick at low
water** → buy a **cod licence + rod**, sell clams, **buy a damaged dory at the Greywick shipwright and pay to
repair it** → **sail it home to Coddle Cove**. The start and the Ned/Ginny onboarding **relocate** here from the M1
Coddle Cove stand-in, **reusing** the dialogue/onboarding system, not rebuilding it. **Note:** the owner has
**dropped the "inherit Uncle Ned's dory" framing** — the dory is now *earned and repaired*, which **partly
invalidates the built VS-21 onboarding** (the inherited-dory beat needs rework — a flagged later task), while Ned's
cottage + memory remain inherited (canon [`vision-and-pillars.md`](vision-and-pillars.md) §5.8;
[`design/world-and-regions.md`](design/world-and-regions.md) §6.0). The **lobster gear loop** (trap + buoy + bait; lay alongside, leave the helm, gaff and haul; the
powered-winch upgrade — [`design/boats-and-navigation.md`](design/boats-and-navigation.md) §6.3) joins the
Cape-Islander / Lobster-Boat branch. The **weather/winter/fog** wave gains the owner's **waves that push boats**,
**gusts that travel across the water**, **lightning + heavy rain**, and **winter freezing in *some* regions**
([`design/time-tides-weather.md`](design/time-tides-weather.md) §4.8), plus the **wet-surface tide effects** (the
~3–4 m tide range that bares wet walls and flats — [`design/art-and-audio-bible.md`](design/art-and-audio-bible.md)
§6.1). All stay deterministic and cozy-with-teeth.

**Explicitly deferred.** The Banks and everything offshore; dragger/trawler tiers; the **staff & automation**
layer (M3 — you still do every job by hand here, which is the *point*: earn it before you automate it, P4);
production *chains* beyond the first one or two facilities; freight/shipping; Ironbound, The Smother; fleet
command; offline accrual (may begin in a limited form late M2 for build timers only).

**Pillars advanced.** **P1** at full strength (the Sunkers, the Drownded Lands, the Rips are tide made
life-or-death) · **P2** several rungs up the ladder · **P3** comes alive (routines + a breathing market) ·
**P5** delivers its teeth (grounding, capsize, rescue) · **P4**'s *foundation* (every job still by hand, so it
has weight) is fully laid, ready to be automated in M3.

**Shippable?** Yes — this is an **Early-Access-shaped** game: a complete cozy-with-teeth inshore fishing RPG, even
before the offshore/empire half exists. **Fun here?** This is where "a nice demo" becomes "a game I'd put hours
into." The fun-check is whether the danger feels *fair and teaching* (never a wipe) and the market feels *alive*.

---

### M3 — Offshore & Enterprise

> **The question this answers:** *Does scaling up — and stepping back from doing every job yourself — feel
> earned and good?* M3 is where the title's second half ("…to Dynasty") and pillar **P4** ("Earn It, Then
> Automate It") finally pay off.

**Goal.** Open **The Banks** and the offshore industry, and introduce the **staff & automation layer** — the
first time a job runs *without you*.

**Player experience at end of phase.** You climb into a **Side Dragger** (and later **Stern Trawler**), transit
the Rips to the open grounds, and fish becomes an *industry* — net/trawl gear, big holds, overnight steaming, the
loneliness and exposure of being out of sight of land. The bottleneck shifts from "can I catch enough?" to "I'm
catching more than I can sell or process" — so you build **production chains** (cannery, reduction plant, pack
house) and, the headline beat, you **hire**: a deckhand eases the grind, then a **skipper runs a second boat on a
route without you**, then a **processor** runs a facility, then a **hauler** moves product between sites. You set
**policies** instead of doing chores, take **standing/freight contracts** for stable income, and feel yourself
become an *owner*. **Reputation and contracts** structure the mid-late economy.

**Systems built (by role).** The Banks + offshore passages, set-a-course fast travel (`world-content`).
Dragger/Stern-Trawler tiers + net/trawl gear + offshore instruments (radar/GPS/sounder) (`gameplay-systems`).
The **staff & automation engine** — roles, hiring, wages, morale, skill growth, AI work routines, and the
**policy** system (`economy-sim` + `gameplay-systems`). Production chains + multi-facility ops + manager role
(`economy-sim`). Reputation/contracts depth, mainland/distant-market hooks (`economy-sim`). Offline accrual for
delegated operations, within bounds (`lead-architect` + `economy-sim`). Management UIs — dashboard-first, card-
based, "manage by exception" (`ui-ux`). Offshore art (big boats, banks, weather spectacle) + crewed-deck sprites
(`art-pipeline`). Offshore/industry audio (engine voices that scale with tier, busy decks) (`audio`). Performance
& save scaling for an abstracted NPC/freight fleet (`lead-architect` + `qa-test`).

**Owner-ratified additions folded into M3 (2026).** **Mussel/oyster aquaculture leasing** — lease a patch of
water, set buoys-in-series with grow-ropes, season-grow the crop, harvest at maturity — sits beside the staff /
automation layer as another P4 "earn it, then automate it" engine (`economy-sim` + `world-content`;
[`design/fish-and-content.md`](design/fish-and-content.md) §3.5(c)). The **advanced rendering pass**
([`design/art-and-audio-bible.md`](design/art-and-audio-bible.md) §6.1) — **3D water baked to a 2D surface**,
**dynamic shadows on the 24-h clock**, **cloud shadows**, and the **parallax underwater / shallow-water preview** —
deepens the look as the world scales offshore, held to the desktop perf budget (`art-pipeline` + `lead-architect`).

**Explicitly deferred.** Ironbound and The Smother (M4 capstones); the freighter/tanker tier and full freight
empire (M4); the complete 100-fish fill (M4 — M3 fills what the Banks needs); multi-platform port (M4).

**Pillars advanced.** **P2** into industry scale · **P4** *realized* (the laborer→owner turn: you automate the
tedium you previously did by hand) · **P3** at fleet scale (a simulated working coast you're now part of) ·
**P5** offshore (distance, weather, the stakes of a staffed boat in a blow).

**Shippable?** Yes — a substantially complete game with a satisfying mid-late arc. **Fun here?** The fun-check is
the **automation guardrail**: delegated output must be *good but never strictly better* than skilled hand-play,
so delegating is about scale and your time, not a power button — and hand-fishing must stay worth dropping back
into.

---

### M4 — Dynasty

> **The question this answers:** *Does the top of the ladder deliver the fantasy the title promises?* M4 is the
> endgame, the content fill, and the platform expansion — 1.0.

**Goal.** Deliver the **dynasty** endgame — fleet command and a freight empire — plus the apex fishing frontiers,
the full content fill, and the multi-platform port.

**Player experience at end of phase.** You reach the **commerce tier**: the **Coastal Packet/Freighter** and the
**Coastal Tanker/Cargo Ship**, the literal payoff of "From Dory to Dynasty." You run **freight routes** on **The
Shipping Lanes** to mainland markets, fulfill bulk contracts, and **command a fleet** you direct rather than
crew — operating by exception from a dashboard, with the option to drop back into the dory for a quiet morning's
fishing whenever you like. The two endgame frontiers open in parallel: **Ironbound** (apex weather, the rarest
and legendary fish) and **The Smother** (the permanent fogbank, navigate by instrument, cryptid catches). The
full **100-fish** content is in, the **end-game economy** (money sinks, prestige property, the freight empire) is
balanced, and the game ships on **desktop/console** alongside mobile via the input-intent abstraction laid down
in M0.

**Systems built (by role).** Freighter/Tanker tiers + tug-assisted docking + fleet command UX (`gameplay-systems`
+ `ui-ux`). The Shipping Lanes + off-map mainland ports + freight/logistics layer (`world-content` + `economy-sim`).
Ironbound + The Smother (weather-/instrument-gated capstone regions) + the legendary/cryptid catches
(`world-content` + `economy-sim`). The full 100-species content fill via the parallel-authoring workflow
(`world-content` + `economy-sim`). End-game economy balance, money sinks, prestige housing/estate
(`economy-sim` + `world-content`). Full art/audio fill for the outer world and the biggest hulls (`art-pipeline`
+ `audio`). **Multi-platform port** — mouse/keyboard + gamepad mappings of the existing intents, responsive UI
reflow (`lead-architect` + `ui-ux`). Full-content performance, save-migration, and certification passes
(`lead-architect` + `qa-test`).

**Explicitly deferred / post-1.0.** The optional endless outer-grounds procedural zone (a post-launch flex);
DLC/seasonal-event fish (the flex reserve in the fish allocation); deeper piracy/insurance economy beyond the
light version; any feature still in design "open questions" that didn't earn its way in.

**Pillars advanced.** All five at full volume — **P2** complete (dory to tanker), **P4** complete (the
self-running dynasty, earned), **P1/P5** at their apex (Ironbound/Smother), **P3** as a fully living coast and
economy.

**Shippable?** Yes — **1.0**, multi-platform. **Fun here?** The fun-check is whether **directing** an empire is
as engaging as *building* it was (operate-by-exception, not spreadsheet-tending), and whether late-game coin
stays meaningful (money sinks).

---

## 3. Cross-cutting tracks (run through every phase)

These are not phases — they are disciplines that must be tended *continuously*, a little in every milestone,
never bolted on at the end. Each has a standing owner.

| Track | Owner(s) | The standing commitment, every milestone |
|---|---|---|
| **Art pipeline & style** | `art-pipeline` | Hold the PPU=32 / 1 m / true-metric-scale rule (P2 depends on it); master palette discipline; placeholder→final swap workflow; atlas/LFS hygiene; profile texture memory on mobile. Art grows region-by-region, never all at once. |
| **Audio** | `audio` | The sea must be *heard* changing before it's dangerous (P1/P5). Responsive ambient beds + adaptive music grow with each region; rising-wind tell is sacred. |
| **Tools & editor** | `tools-editor` | Data-driven content (ADR-0003) only pays off if authoring is fast: SO inspectors, the tide/clock scrubber, the fish/economy balance dashboards, the unlock-graph validator. Build tools *just ahead* of the content that needs them. |
| **QA & playtest** | `qa-test` | Every milestone ends with an acceptance pass; the loop is **playtested by humans** at M0 and externally at M1. Maintain a smoke-test of the core loop that must pass on every build. |
| **Performance (desktop baseline; mobile-portable)** | `lead-architect` + `qa-test` | PC-first means **profile against the desktop baseline (60fps on a typical desktop/laptop GPU) every milestone**, not at the end. Watch the frame budget (water/lighting/HUD), draw calls, texture memory, scene-streaming at passages, GC in the hot path. **Keep the mobile-portability guardrails** (pooling, draw-call/texture budgets) so the later mobile port stays cheap (ADR 0005). A feature that can't hit budget gets cut or simplified. |
| **Save-system stability & migration** | `lead-architect` | Saves are tiny by design (seed + gameTime + player/world mutations). Keep a **versioned schema** from M0; every milestone that changes the save must ship a **migration** and a test that loads an old save. Never strand a player's save. |
| **Localization-readiness** | `ui-ux` + `world-content` | All player-facing strings (fish names, flavor text, dialogue, UI) go through **localization tables from M0**, never inline. Not localized at launch necessarily, but never blocked from it by hardcoded strings. |
| **Determinism & data-driven discipline** | `lead-architect` | Environment is a pure function of `(seed, gameTime)`; content is ScriptableObjects with stable `id`s. Guard these invariants in review — they are what make saves tiny, forecasts honest, and parallel content authoring conflict-free. |

---

## 4. Risks & how we de-risk

| Risk | Why it's scary | How we de-risk |
|---|---|---|
| **Boat physics feel** (the #1 design risk) | "Throttle + heading" steering against wind/current is the riskiest UX bet (UX doc OQ1) — if sailing isn't *fun* and **comfortable on KB/mouse + gamepad**, P1's whole skill fantasy collapses. | **Prototype the dory force model on desktop (KB/mouse + gamepad) in M1, early and in isolation**, against the virtual-stick/arcade alternate. Tune the assist defaults (heading-hold/leeway-compensation) so it's approachable but the sea still bites. Don't build the rest of M1's content until sailing feels right. *(The touch/one-thumb feel is validated in the later mobile-port pass — same intents, retargeted bindings.)* |
| **Performance (desktop baseline)** | Animated tide-aware water + 2D lights + a frame-by-frame HUD + scene streaming is a real budget even on desktop. | Performance is a **cross-cutting track**, profiled every milestone against the desktop baseline (§3). Water shader vs overlay decided by a profiled spike (art OQ2). One active boat dominates physics cost by design; NPC/freight fleets are abstracted, not fully simulated. Cap on-screen boats with LOD. *Keep the mobile-portability budgets so the port stays viable (ADR 0005).* |
| **Scope creep** (the project's existential risk) | The design is vast and every part is tempting to build "while we're in there." | **This roadmap is the gate.** Milestone order is enforced; later-phase work is redirected here. The Definition of Done per milestone (and per work item) is the contract. The owner's go/no-go at each milestone is the throttle. |
| **Market-sim balance** | Supply/demand + elasticity + storage + processing + contracts can easily become unfun (prices that swing wrongly, gluts that punish, a "correct" exploit). | Build the **balance dashboard** (fish + economy) early as a tools-track deliverable; run the "day-in-the-life" sim against the economic progression curve. Introduce the market *shallow* in M1 and deepen it only in M2 with tuning data in hand. |
| **Save migration / corruption** | A multi-year game with evolving systems can strand or corrupt long-lived saves — the worst possible player experience. | Tiny deterministic saves + **versioned schema + a migration and an old-save load test every milestone** (§3). Stable `id`s everywhere so content additions append, never break. |
| **The loop isn't actually fun** | Everything is built on the fishing→sell loop; if it's mediocre, the whole tower is. | **M0 exists precisely to find this out cheaply**, in greybox, before investment. The owner playtesting the M0 loop is the single highest-value de-risking act in the project (§6). |
| **Art volume** (esp. big hulls + many regions) | T5–T7 sprites are huge; nine regions of tiles/props is a lot of art; it could become the bottleneck. | Art grows **region-by-region with the milestones**, never front-loaded. Section/atlas the big hulls; reuse modular tiles/crew/decor. Placeholder→final swap workflow means systems never wait on art. |
| **"Cozy vs teeth" balance** | Too safe and P5 is lost; too punishing and the cozy-first promise breaks (an anti-pillar). | Danger is always **telegraphed, survivable, and resolves to time/money/partial-load — never a wipe or the boat**. Tune the gentlest version in the tutorial regions (Cove/Sunkers) first; validate the "the sea warned me, not ambushed me" feel in M2 playtests. |

---

## 5. The release/visibility shape (how milestones map to the outside world)

- **M0** — internal prototype. Shown to no one but the owner (and trusted playtesters of the loop).
- **M1** — **soft-launch candidate: a Steam / itch.io closed playtest** (PC-first; mobile = later port). The
  first thing real players touch. The go/no-go on the whole multi-year build.
- **M2** — **Early-Access shape.** A complete cozy-with-teeth inshore game; a credible paid/launch-able product
  even though the empire half isn't built.
- **M3** — content/feature update over the EA shape (offshore + the first automation).
- **M4** — **1.0**, multi-platform.

This shape means the project produces a **showable, shippable artifact early and often**, instead of one
all-or-nothing launch years away.

---

## 6. How the owner steers (go/no-go without writing code)

You don't need to read C# to steer this well. You need to **play the build and make a small number of clear
decisions at the right moments.**

**At every milestone, you make one of three calls:**

1. **GO** — it's fun and solid; proceed to the next milestone.
2. **POLISH** — it's promising but not there; iterate *within this milestone* (cheap) before proceeding.
3. **PIVOT/STOP** — it isn't working and iterating won't fix it; change direction (or stop) now, before the
   expensive next phase.

**What to judge at each gate (the questions that matter):**

| Gate | The decision | What you're really asking |
|---|---|---|
| **End of M0** | Is the **core loop** fun in greybox? | "Do I keep wanting one more run with rectangles for art?" If no, fix the loop — do **not** spend M1's art/world budget on a loop that isn't fun. |
| **End of M1** | Is this game **worth making**? | "Does the world make me want to live in it? Did the soft-launch testers come back?" A GO here greenlights the multi-year climb. |
| **End of M2** | Do the **teeth and the breathing world** land? | "Is the danger fair and teaching (never a wipe)? Does the coast feel alive?" |
| **End of M3** | Does **becoming an owner** feel earned and good? | "Is delegating about scale and my time, not a power button? Is hand-play still worth dropping into?" |
| **End of M4** | Does **directing the empire** deliver the fantasy? | "Is operate-by-exception engaging? Does late-game money still matter?" |

**The single most important thing you can do:** **play the M0 loop, repeatedly, before any further building.**
It is the cheapest moment to learn the most important truth in the project — *is the fundamental thing fun?* —
and every later milestone is built on that answer. Be honest at M0 and the rest of the climb is on solid ground;
skip it, and you risk pouring years onto a loop that never had the magic.

**A few standing principles for your calls:**
- **Trust the playable build over the design doc.** The docs are excellent, but fun is found in the hand. If the
  build disagrees with the plan, the build wins.
- **Protect the golden rule.** If an agent says a feature needs the playable build broken for a long stretch,
  push back — ask for a smaller slice that keeps Play working.
- **Honor milestone order.** When something later sounds exciting, note it in the backlog for its milestone;
  don't let it jump the queue. Scope discipline *is* the project's survival.
- **Cozy first, always.** When a "teeth" feature starts to feel punishing, it's drifted out of canon — pull it
  back. The sea is seasoning, not the meal.
