# Hidden Harbours — Vertical Slice Spec (M0 → M1)

> **Status:** Working backlog. The detailed, agent-ready spec for the **fishing→sell vertical slice** in
> **Coddle Cove** with the **Dory** — from a greybox prototype (M0) to a polished, soft-launchable slice (M1).
> **Canon:** [`../docs/vision-and-pillars.md`](../docs/vision-and-pillars.md) wins on any conflict.
> **Phasing:** [`../docs/roadmap.md`](../docs/roadmap.md) (M0, M1 sections). **Full backlog:** [`backlog.md`](backlog.md).
> **Workflow:** pull the top *unblocked* item for your role; follow the Definition of Done (§6); see
> `../agents/coordination.md` for the multi-agent protocol.
>
> **Pillars:** **P1** The Sea Has Moods · **P2** From Dory to Dynasty · **P3** A Living Working Coast ·
> **P4** Earn It, Then Automate It · **P5** Cozy, but with Teeth.

---

## 1. What this slice is (and the M0/M1 split)

The vertical slice is the **smallest delightful version of the whole game**: inherit the dory, read the tide,
catch fish by hand in Coddle Cove, sell them to a buyer whose price moves, and earn your way to your first boat.
It is built in two passes:

- **M0 — Greybox Prototype.** The loop, proven fun with placeholder art. *Is the core loop fun with the lights
  off?* (Items tagged **[M0]**.)
- **M1 — Vertical Slice.** That same loop made *genuinely good* — real art, the boat *force model* (wind + tide
  as forces), the tide table, Uncle Ned, a glimpse of Port Greywick, and the first boat purchase (the Punt).
  *Is this game worth making?* (Items tagged **[M1]**; the soft-launch / TestFlight candidate.)

> **The golden rule applies inside the slice too:** keep a playable build at all times. Every item below is sized
> so the build still runs after it lands.

---

## 2. Scope — IN vs OUT (read before estimating)

### 2.1 IN scope

**[M0] — prove the loop (greybox):**
- One Unity 6.3 project (2D URP, mobile target), module/asmdef layout, Git LFS, persistent-core + additive scenes.
- Input via **intents** (touch-first): `Move`, `SetThrottle`, `SetHeading`, `Interact`, `Haul`.
- Save/load scaffold: a save is `{ worldSeed, gameTime, playerState }`, versioned.
- `EnvironmentService` v0: a `gameTime` (double) clock + the **deterministic semidiurnal tide** formula; sleep/skip.
- Dory controller v0: **simple kinematic** top-down movement + follow-cam (NOT the force model yet).
- Fishing interaction v0: fishing spot → bite → **tension band + landing gauge + strain bar** (one-thumb, cozy).
- `FishSpecies` ScriptableObject schema + **6 Coddle Cove species** as data assets.
- Catch resolver v0: context → weighted candidate table → one fish + size roll (+ a miss/flotsam entry).
- One **Fish Buyer** at the wharf + the **supply/demand price formula** (single market, sliced sells).
- HUD v0: clock + **tide gauge** + money + hold-fullness.
- Greybox **Coddle Cove** scene: wharf, cottage (sleep/save), dory mooring, water, a few fishing spots.
- Placeholder-art convention + locked Unity import settings (PPU=32, Point, no compression).
- Minimal ambient audio (calm sea + gulls).
- M0 acceptance + a human playtest of the loop.

**[M1] — make it good (the slice):**
- **Boat force model v1** for the dory: engine thrust + anisotropic hydro-drag + windage + speed-scaled rudder,
  consuming the FORCES `EnvironmentSample` — wind pushes, tide sets, the boat carries way (inshore/forgiving tune).
- `EnvironmentService` v1: wind field, sea-state value, the full `EnvironmentSample` struct, **tide-table** generation.
- **Tide table** tool (readable; the Cove + a time-to-turn read) and HUD **tide gauge + wind widget + compass**.
- Coddle Cove **art pass**: tile-aware moving shoreline, animated water, day-night colour grade, cottage, wharf,
  dory & Punt sprites at true metric scale, fish sprites.
- A small **Port Greywick** scene: a wharf with the Fish Buyer / auction spot price + a **Shipwright** buy flow.
- The **Punt** as the first purchasable boat (Dory→Punt via composable components).
- **Uncle Ned** + 1–2 named NPCs (anchored, with dialogue) and a gentle **onboarding** flow that teaches the loop.
- Market basics: Fish Buyer + a simple Greywick spot/auction price; demand recovers over days; **sell screen**
  with a marginal-price slider.
- Reactive ambient + **adaptive music v1** (region beds, rising-wind tell, catch sting, "made it home" warmth).
- Save schema v1 (owned boat + components, money, day, Ned/onboarding flags) + a migration test.
- M1 acceptance + a small **external playtest** (soft-launch readiness).

### 2.2 OUT of scope (deferred to M2+ — do not build here)

Grounding / capsize / taking-on-water / breakdown / **rescue-tow** (M2 — the cove stays forgiving). Weather
**fronts / fog / storms** (M2; M1 has wind + a calm-ish sea state only). The Sunkers / Drownded Lands / Fundy
Rips and all regions beyond Coddle Cove + a *minimal* Greywick (M2+). Cape Islander and up; net/trawl/trap gear
(M2+). **Supply/demand depth**, multiple buyers, contracts, the NPC-fleet landings model (M2 — M1 has a single
recovering demand curve). **Storage, refining/processing** (M2). **Staff & automation** (M3 — you do every job by
hand here; that is the point, P4). NPC daily **routines** (M2 — M1 NPCs are anchored, not scheduled). Housing
**decor/Comfort**, the residence ladder (M2). **Licenses / proficiencies / reputation** as systems (M2 — M1's
onboarding may *narratively* hand you the first "license" without the system behind it). **Map / charts / fog-of-
war** (M2 — M1 navigation is direct, within one cove + a short hop). **Offline accrual** (M3-ish). Sailing-by-sail
on the dory (flavor; engine/oars only in the slice). Diurnal tide inequality (semidiurnal-only here).

---

## 3. Minimal systems & content checklist

| Need | Minimal version for the slice | Where specced |
|---|---|---|
| **Clock + tide** | `gameTime` (double); semidiurnal tide as a pure function of `(seed, gameTime)`; sleep-to-next-day | [`time-tides-weather.md`](../docs/design/time-tides-weather.md) §1, §3.4 |
| **Dory** | M0: kinematic move. M1: the force model (thrust/drag/windage/rudder), inshore-forgiving tune | [`boats-and-navigation.md`](../docs/design/boats-and-navigation.md) §1.1 (T0), §2 |
| **Fishing** | One active handline interaction: bite → tension band + landing gauge + strain bar | [`fish-and-content.md`](../docs/design/fish-and-content.md) §3.4; [`ux-and-mobile-controls.md`](../docs/design/ux-and-mobile-controls.md) §3.3 |
| **Fish (6)** | `cod, haddock, pollock, mackerel, rock-crab, blue-mussel` — all Coddle Cove, handline/jig or hand-gather | [`fish-and-content.md`](../docs/design/fish-and-content.md) §4 (seed exemplars) |
| **Buyer + price** | One Fish Buyer; `priceMult = clamp(1/(1+e·S/D), floor, 1)`; sell in slices; demand recovers daily | [`economy-and-business.md`](../docs/design/economy-and-business.md) §1.2–1.3 |
| **HUD** | Clock, tide gauge, money, hold fullness (M0); + wind widget + compass + sell screen (M1) | [`ux-and-mobile-controls.md`](../docs/design/ux-and-mobile-controls.md) §4 |
| **Coddle Cove** | Wharf, cottage (sleep/save), dory mooring, water, fishing spots; M1 art pass | [`world-and-regions.md`](../docs/design/world-and-regions.md) §6.1 |
| **Port Greywick (minimal)** | A wharf, the Fish Buyer/auction spot, the Shipwright — services only, not a town | [`world-and-regions.md`](../docs/design/world-and-regions.md) §6.3 |
| **The Punt** | First purchasable boat (T1); a buy flow at the Shipwright | [`boats-and-navigation.md`](../docs/design/boats-and-navigation.md) §1.1 (T1) |
| **Ned + onboarding** | late Uncle Ned (brief prologue + his logbook); inherit the dory; 1–2 NPCs; a teach-the-loop flow | [`vision-and-pillars.md`](../docs/vision-and-pillars.md) §5.8; [`npcs-and-routines.md`](../docs/design/npcs-and-routines.md) |
| **Save** | `{seed, gameTime, money, ownedBoats+components, dayIndex, flags}`; versioned + migration | [`progression-and-housing.md`](../docs/design/progression-and-housing.md) §7 |
| **Art convention** | PPU=32, true metric scale, Point filter, no compression; placeholder→final swap | [`art-and-audio-bible.md`](../docs/design/art-and-audio-bible.md) §3, §9.2 |
| **Audio** | Calm-sea + gull bed (M0); reactive beds + adaptive music + rising-wind tell (M1) | [`art-and-audio-bible.md`](../docs/design/art-and-audio-bible.md) §8 |

---

## 4. Work items (dependency-ordered — start at the top)

> **Reading a work item:** `ID · Title · [phase] · owner-role · dependencies`. Each has a 1–3 sentence
> description and explicit **Acceptance criteria** (AC). Items are ordered so an agent can start at the top; an
> item is workable once all its dependency IDs are Done. Where work can run in parallel, dependencies make that
> explicit (e.g. several items depend only on VS-01/02).

---

### Track A — Bootstrap & foundations

**VS-01 · Unity project bootstrap & module layout · [M0] · `lead-architect` · deps: none**
Create the Unity 6.3 (2D URP) project targeting iOS + Android (portrait-primary). Establish the asmdef module
boundaries (e.g. `HiddenHarbours.Core`, `.Environment`, `.Boats`, `.Fishing`, `.Economy`, `.World`, `.UI`,
`.Audio`), a persistent **Core/bootstrap scene** + additive scene-loading scaffold, and Git LFS for binary art/
audio. Lock 2D URP render pipeline settings.
- **AC:** Repo opens in Unity 6.3 with no console errors; builds and launches a black/empty scene on an Android
  device (or emulator) and in the Editor. asmdefs compile with the stated module boundaries and no cyclic refs.
  `.gitattributes` tracks `*.png *.aseprite *.wav *.ogg` via LFS. A persistent Core scene loads and can additively
  load/unload a child scene by key.

**VS-02 · Input intents (touch-first) · [M0] · `lead-architect` · deps: VS-01**
Stand up Unity Input System with an `InputService` that exposes **intents**, not raw input: `Move(Vector2)`,
`SetThrottle(float)`, `SetHeading(point/dir)`, `Interact`, `Haul(hold/release)`. Touch bindings first; structure
so mouse/keyboard + gamepad can map to the same intents later (no rewrite).
- **AC:** Gameplay code consumes only intents (never raw touch). On a touch device: a floating left-thumb control
  drives `Move`/`SetThrottle`; a right-side tap/drag drives `SetHeading`; an Action button raises `Interact`/
  `Haul`. Switching/adding a device (e.g. an editor keyboard) maps to the same intents with no gameplay code
  change. A throwaway test scene proves all five intents fire.

**VS-03 · Save/load scaffold (versioned) · [M0] · `lead-architect` · deps: VS-01**
A `SaveService` that serializes/deserializes a small, **versioned** save document. M0 payload:
`{ schemaVersion, worldSeed, gameTime (double), money, dayIndex }`. Auto-save on background/exit and on sleep;
load on launch. Saves are tiny because the world is deterministic from `(seed, gameTime)`.
- **AC:** Quitting and relaunching restores `worldSeed`, `gameTime`, `money`, `dayIndex` exactly (gameTime to
  sub-minute precision — stored as `double`). A `schemaVersion` field is present and checked on load; an unknown/
  older version routes through a (currently no-op) migration hook rather than crashing. Backgrounding the app
  writes a save.

---

### Track B — The sea (environment)

**VS-04 · EnvironmentService v0 — clock + deterministic tide · [M0] · `gameplay-systems` · deps: VS-01**
Implement `gameTime` (double, in-game seconds since epoch) advancing at the canon scale (day ≈ 20 real min,
`timeScale = 72`), with `timeFlowMultiplier` for pause/fast-forward. Implement the **semidiurnal tide** as a pure
function of `gameTime` per [`time-tides-weather.md`](../docs/design/time-tides-weather.md) §3.4 (mean level +
spring/neap-modulated cosine), a `Coddle Cove` `RegionTideProfile` (small amplitude, gentle), and the derived
tide **rate** (rising/falling/slack). Expose `OnDayRollover(dayIndex)` and a sleep/skip that fast-forwards
`gameTime` to a target. Sleep fires `OnDayRollover` once per crossed midnight.
- **AC:** Tide height is reproducible: same `(seed, gameTime)` → identical height (unit test). Over a day there
  are ~two highs and two lows, and highs walk ~50 min later each calendar day. `TideState` reports rising/falling
  and "time to next turn." Sleeping to next morning advances `gameTime` and fires `OnDayRollover` exactly once
  (and N times across an N-day skip). No `System.Random` with hidden state in the tide path.

**VS-05 · EnvironmentService v1 — wind, sea-state & the FORCES sample · [M1] · `gameplay-systems` · deps: VS-04**
Extend the service to produce the full `EnvironmentSample` struct (the contract in
[`time-tides-weather.md`](../docs/design/time-tides-weather.md) §5.1): `TideHeight`, `TideRateNorm`, `WaterDepth`,
`CurrentVector` (gentle in the cove), `WindVector` (smooth value-noise + Coddle Cove prevailing wind + gusts),
`SeaStateTier`/`Roughness` (calm range only for the slice), `Visibility` (clear), region/seabed context. Sample
at 4 Hz per active boat.
- **AC:** `EnvironmentService.Sample(pos, gameTime)` returns a fully-populated, immutable `EnvironmentSample`;
  identical inputs → bit-stable output (no hidden RNG). Wind direction/strength vary smoothly over time (no
  popping) and read out on the HUD wind widget (VS-19). Cove current is gentle (~0.2 m/s peak). Sampling at 4 Hz
  shows negligible cost in the profiler on a mid-range phone.

**VS-06 · Tide-table tool + generation · [M1] · `gameplay-systems` (+ `ui-ux` for the panel) · deps: VS-04, VS-19**
Implement tide-event generation (sample the formula forward, find HW/LW extrema) and a **readable tide-table
panel** for Coddle Cove (today + the next day; the Tier-0 "Uncle's booklet" scope): times/heights of each high
and low, a "now" marker, and a simple curve. Reading it pauses the world (`timeFlowMultiplier = 0`).
- **AC:** The panel lists the correct upcoming highs/lows for Coddle Cove (matches the live tide within rounding).
  Opening it pauses time; closing resumes. The HUD tide gauge's "time to next turn" agrees with the table. Forecast
  is exact (it's a deterministic sample, not a simulation).

---

### Track C — The boat

**VS-07 · Dory controller v0 (kinematic) + follow-cam · [M0] · `gameplay-systems` · deps: VS-02, VS-04**
A **simple, kinematic** top-down dory: `SetThrottle` drives forward/back speed, `SetHeading` turns the bow toward
the target at a handling-limited rate. A smooth follow-cam (pixel-perfect, slight lookahead) at the intimate
default zoom (~12–16 m of world height). **No wind/current forces yet** — this exists only to make the loop
playable in M0.
- **AC:** The player boards the dory from the wharf, putters around the cove with one-thumb controls, and returns;
  the camera follows smoothly without shimmer. Movement feels controllable on a touch device. Boarding/disembark
  works at the mooring. (Explicitly: no drift from wind/tide yet.)

**VS-08 · Boat as composable components (minimal) · [M1] · `lead-architect` + `gameplay-systems` · deps: VS-07**
Introduce the `Boat` aggregate composed of ScriptableObject components — minimally **Hull + Engine + Hold** (and
a `Gear` slot for the handline) — per [`boats-and-navigation.md`](../docs/design/boats-and-navigation.md) §9.1.
Author **Dory** and **Punt** chassis data (mass/inertia, draught, hold HU, drag coefficients, engine thrust) from
the §1.1 stats. Physics reads stats from components, never a hardcoded per-boat block.
- **AC:** Dory and Punt each exist as data (Hull/Engine/Hold assets); swapping the active boat swaps its handling/
  hold purely via data. Hold capacity (Dory 6 HU, Punt 14 HU) is read from the component. No code special-cases a
  boat by name. Save persists which boat + component set is active (extends VS-03 payload).

**VS-09 · Dory force model v1 (the riskiest item — prototype first) · [M1] · `gameplay-systems` · deps: VS-05, VS-08**
Replace the kinematic controller with the planar **rigid-body force model**: engine thrust (forward), anisotropic
hydrodynamic drag (resists sideslip ≫ forward), windage on relative air velocity, and **speed-scaled rudder
authority** (can't turn dead in the water), all assembled each `FixedUpdate` from the `EnvironmentSample`. Tune to
the **inshore-forgiving** end (the cove never grounds you; this is P1 taught at low stakes). Default the
heading-hold/leeway assist on, with the residual set still felt. **Build and tune this on a real phone before the
rest of M1 content** (it's the #1 risk).
- **AC:** With engine off, the dory **drifts** with the gentle cove current and is nudged by wind (it "sets").
  Under power it overcomes them but you feel the medium move. The boat **carries way** when you cut throttle and
  cannot pivot at zero speed. Crabbing into a crosswind to hold a line is possible and reads on the set-&-drift
  predictor (VS-19). On a mid-range phone the steering feels one-thumb-comfortable on a long-ish hop (validated by
  `qa-test` against the virtual-stick alternate as part of VS-29). No grounding/capsize behavior present (deferred).

---

### Track D — Fishing & fish content

**VS-10 · FishSpecies schema (ScriptableObject) · [M0] · `economy-sim` (+ `world-content`) · deps: VS-01**
Implement the `FishSpecies` ScriptableObject and its sub-structs (`TideWindow`, `TimeWindow`, `SeasonModifier`,
`SizeRange`, enums for `FishCategory`/`Rarity`/`DepthBand`/`GearTag`/`RegionId`) per
[`fish-and-content.md`](../docs/design/fish-and-content.md) §1, scoped to what the slice needs (region, depth,
tide/time windows, gear, size range, baseValue, valuedBy, perishability, elasticity, sprite ref, flavor). Stable
lowercase-kebab `id` is the filename stem and validated unique.
- **AC:** A `FishSpecies` asset can be authored entirely in the Inspector with no code change. `id` uniqueness is
  validated on import. `displayName`/`flavorText` reference localization tables, not raw strings. The schema
  compiles behind `HiddenHarbours.Economy` (or a shared content module) and is consumable by the resolver.

**VS-11 · Six Coddle Cove species (data) · [M0] · `world-content` (+ `economy-sim`) · deps: VS-10**
Author the six seed species as assets, tuned to Coddle Cove and beginner gear:
`atlantic-cod`, `haddock`, `pollock` (handline/jig, `PerKg`), `atlantic-mackerel` (jig, `PerKg`, a schooling
glut), `rock-crab` and `blue-mussel` (hand-gather/`ClamFork`, `PerUnit`). Use the §4 seed exemplars as templates;
give each a non-trivial tide and/or time window so the tide table matters even in the cove (P1).
- **AC:** All six assets pass the uniqueness/required-field check (non-empty `regions` incl. Coddle Cove,
  non-empty `requiredGear`). Each catchable by the slice's gear. At least 4 of 6 carry a real tide *or* time
  window (so "when you fish" matters). Values/sizes are plausible per the exemplars. Placeholder sprite refs are
  flagged (allowed in M0).

**VS-12 · Catch resolver v0 · [M0] · `economy-sim` · deps: VS-10, VS-04**
Build the candidate-table resolver ([`fish-and-content.md`](../docs/design/fish-and-content.md) §3): from a
`FishingContext` (region, depth, tideState/height, hour, season, gear), hard-filter the species pool, compute
weights from rarity × tide × time × season, add a `MissOrFlotsam` entry, normalize, roll one outcome, then roll
**size** on the right-skewed curve. Seed the RNG from `(spot, time-bucket, worldSeed)` so a spot is consistent in
a short window but rerolls as conditions change.
- **AC:** Given a context, the resolver returns either one species (with a rolled size) or a miss/flotsam. A
  strictly-diurnal fish does not appear at night (weight → 0). Re-rolling the same spot within a time bucket is
  stable; advancing time changes the mix. Unit test: over many rolls in a fixed cove context, the catch mix is
  sane (commons dominate; the miss entry is non-zero but not overwhelming).

**VS-13 · Fishing interaction v0 (cozy, one-thumb) · [M0] · `gameplay-systems` + `ui-ux` · deps: VS-07, VS-12, VS-02**
Implement the catch mini-interaction ([`fish-and-content.md`](../docs/design/fish-and-content.md) §3.4;
[`ux-and-mobile-controls.md`](../docs/design/ux-and-mobile-controls.md) §3.3): approach a **fishing spot** →
`Interact` to cast → a bite (tug + haptic + audio) → a **tension band + landing gauge + strain bar** where
**hold-to-reel raises tension, release eases it**; fill the landing gauge before the strain bar maxes. On success
the fish lands and is added to the hold; on a snap, "it threw the hook" (no damage, cozy). The whole thing works
one-thumb.
- **AC:** A full catch is playable with one thumb on a phone. Landing a fish adds it to the hold (respecting hold
  capacity) with a satisfying confirmation (flip + popup + sting). A failed catch is cozy (no penalty beyond the
  lost fish). Hand-gather species (crab/mussel) resolve via the lighter "tend/haul" variant. Tuning is forgiving
  (not twitchy).

**VS-14 · Fishing polish & technique feel · [M1] · `gameplay-systems` + `ui-ux` · deps: VS-13, VS-26**
Polish the interaction to "satisfying in seconds" quality: real fish sprites on the line/deck, juicy land
feedback (the canon first-cod triumph beat), tuned tension curves per size, haptics, and audio stings wired to
the adaptive music (VS-28). Bigger fish = a slightly longer, tenser (never twitch) fight.
- **AC:** Landing the first cod *feels* like a triumph (art + audio + a coin/stat popup). The tension fight reads
  clearly with real art; bigger species feel weightier without becoming a reflex test. One-thumb still holds.
  Playtesters describe it as "satisfying," not "fiddly" (checked in VS-29).

---

### Track E — Economy (buyer & price)

**VS-15 · Fish Buyer + supply/demand price formula · [M0] · `economy-sim` · deps: VS-10**
Implement a single **Fish Buyer** market and the price math from
[`economy-and-business.md`](../docs/design/economy-and-business.md) §1.2: per commodity, hold `P0`, `D`, `S`,
elasticity `e`, `floorMult`; `priceMult = clamp(1/(1+e·S/D), floorMult, 1)`; `effPrice = P0·priceMult`
(M0 may skip `demandMood`/season). Selling raises `S`; a daily settle (on `OnDayRollover`) decays `S` so price
recovers. Each `FishSpecies` resolves to a commodity by id.
- **AC:** Selling a large lot of one species visibly **depresses its price within the lot** (you slide down your
  own curve) and **recovers over the next day(s)**. Price never drops below `P0·floorMult`. A glut species
  (mackerel, higher `e`) crashes faster than cod. Selling is reflected immediately in money (extends VS-03).

**VS-16 · Greywick market basics + Shipwright buy flow · [M1] · `economy-sim` (+ `gameplay-systems` for the boat grant) · deps: VS-15, VS-08, VS-22**
Add the Port Greywick selling channel (a Fish Buyer / simple auction *spot* price — slightly different demand
than the cove) and a minimal **Shipwright** flow: browse the **Punt** (stats + price ~1,800 ₲), pay, and **own
it**; switch the active boat to the Punt. (No build-timer in the slice — instant, or a 1-day stub.) Demand at
Greywick recovers over days like the cove.
- **AC:** The player can sell at Greywick at a different price than the cove (a reason to consider where to sell).
  With enough coin, buying the Punt deducts money, adds the Punt to owned boats, and lets the player crew it (the
  "I'm a real fisher now" beat). Insufficient funds blocks the purchase gracefully. The owned Punt persists across
  save/load.

---

### Track F — UI / HUD

**VS-17 · HUD v0 — clock, tide gauge, money, hold · [M0] · `ui-ux` · deps: VS-04, VS-13**
The minimum glanceable HUD ([`ux-and-mobile-controls.md`](../docs/design/ux-and-mobile-controls.md) §4): a 24h
**clock** + day, a **tide gauge** (rising/falling arrow — *shape, not just colour* — current height, time-to-next-
turn), **money**, and a **hold-fullness** read that appears when fishing/hauling. Top band only (out of thumb
zones).
- **AC:** Time, tide state/height/time-to-turn, money, and hold fullness are all readable at a glance in under a
  second without opening a menu. The tide gauge uses redundant coding (arrow shape + number), not colour alone.
  HUD updates every frame with no per-frame allocation (profiled).

**VS-18 · Sell screen (marginal-price slider) · [M0→M1] · `ui-ux` · deps: VS-15**
The sell flow ([`ux-and-mobile-controls.md`](../docs/design/ux-and-mobile-controls.md) §5.2): pick a species from
the hold → a quantity slider/stepper that shows the **marginal price and running total** as you drag (so the
player *sees* self-glutting), confirm; plus "sell all of type" / "sell all" shortcuts. (M0: functional. M1: the
chalkboard/diegetic skin.)
- **AC:** Dragging the quantity slider updates the marginal price and total live, and the displayed total matches
  the coin actually received on confirm. "Sell all" works. The price-impact of dumping a big lot is visible before
  confirming (teaches the market).

**VS-19 · HUD v1 — wind widget + compass + set-&-drift predictor · [M1] · `ui-ux` · deps: VS-05, VS-17**
Add the bespoke **wind widget** (direction relative to heading + strength via arrow length/barbs/label — redundant
coding; it animates/strengthens before conditions change) and a **compass** ribbon/rose, plus the **set-&-drift
predictor** (a faint ghost-track/arrow showing where you'll actually go given heading + wind + current). Fuse
wind/sea/weather into a conditions cluster; keep the tide gauge visually distinct.
- **AC:** At sea, wind direction + strength are readable at a glance and visibly change as the wind shifts. The
  set-&-drift predictor shows the boat's true course-over-ground vs heading (so the player learns to crab). All
  sea-reads use redundant coding (validated against colourblind palettes). Ashore, tide/wind shrink to icons but
  stay present.

---

### Track G — World / scenes / NPCs

**VS-20 · Greybox Coddle Cove scene · [M0] · `world-content` · deps: VS-01, VS-07**
A playable greybox Coddle Cove ([`world-and-regions.md`](../docs/design/world-and-regions.md) §6.1): a horseshoe
cove of water, the **wharf**, the **cottage** (a sleep/save interactable), the **dory mooring**, a stretch of
sailable water, and a few **fishing spots**. Placeholder shapes/colours only. Forgiving, soft-bottomed (no
grounding). Authored to true metric scale (1 tile = 1 m).
- **AC:** The player can walk the wharf, board the dory at the mooring, sail the cove, fish at the spots, return,
  and sleep at the cottage (advancing the day + saving). The whole M0 loop is runnable end-to-end in this scene.
  Scale is metric (a ~4.5 m dory reads correctly against a ~1.8 m character footprint).

**VS-21 · Uncle Ned + onboarding flow · [M1] · `world-content` + `ui-ux` · deps: VS-20, VS-13, VS-25**
Add **Uncle Ned** (your late uncle — departed figure who anchors the opening, canon §5.8) via a brief prologue, and
a gentle **onboarding**: you arrive to the inherited dory and cottage, and his logbook (*"Ned's Unfinished Lines"*)
plus **Aunt Ginny** walk you through the loop (cast off → fish → return → sell), plus 1–2 anchored
neighbour NPCs for warmth. Dialogue via a simple panel (portrait + text + tappable choices). NPCs are **anchored,
not yet on daily routines** (routines are M2).
- **AC:** A first-time player is guided through one full fishing→sell loop by Ned without confusion. Ned, the
  dory-lending, and onboarding completion are saved as flags (you aren't re-tutorialised on reload). Dialogue is
  short, skimmable, warm, and Maritime-voiced. The opening reads as hopeful, not grim (P5 tone).

**VS-22 · Minimal Port Greywick scene · [M1] · `world-content` · deps: VS-20, VS-16**
A small Greywick scene reachable by a short hop from the cove (an explicit passage/transition is fine — no full
streaming needed for two scenes): a wharf with the **Fish Buyer / auction spot** and the **Shipwright**, plus a
couple of buildings for flavour. **Services, not a town** (the full town is M2). Deep/dredged harbour (never
strands you).
- **AC:** The player sails (or transitions) from Coddle Cove to Greywick, sells at the buyer, and visits the
  Shipwright to buy the Punt. The harbour is workable at any tide. The scene loads/unloads cleanly without
  breaking the persistent Core scene (clock/tide/save keep running).

---

### Track H — Art pipeline

**VS-23 · Placeholder-art convention + import settings lock · [M0] · `art-pipeline` · deps: VS-01**
Define the greybox placeholder convention (flat-colour blocks at honest metric footprints, labeled) and **lock
the Unity import settings** ([`art-and-audio-bible.md`](../docs/design/art-and-audio-bible.md) §9.2): Sprite
(2D/UI), **PPU=32**, **Point filter**, **Compression None**, mip maps off, consistent pivots (feet for
characters, hull center/stern for boats), Pixel Perfect Camera on. Provide a one-page "how to add an asset"
checklist for art agents.
- **AC:** A documented import preset/convention exists and is applied to all slice sprites. Placeholders are at
  true metric scale (so M0 reads the scale fantasy even in greybox). Pixel Perfect Camera is on with no sub-pixel
  shimmer at the default zoom.

**VS-24 · Coddle Cove art pass — terrain, shoreline, water · [M1] · `art-pipeline` · deps: VS-20, VS-05, VS-23**
The headline P1 visual: replace greybox with the Coddle Cove art pass — modular 32×32 tiles (wharf decking,
rock, sand, grass), an **animated water** surface (shader or overlay), a **tide-aware autotiled shoreline** that
visibly moves as `WaterLevel` changes, a day-night **colour grade** driven by the clock, and the cottage/wharf
buildings (¾, front faces). Master-palette discipline; warm "home" grade.
- **AC:** The drawn shoreline/foreshore **visibly advances and retreats with the tide** (the same `WaterLevel`
  that drives the sim — what you see matches what the physics does). Water animates and reads as water; the scene
  recolours believably from dawn→day→dusk→night. Runs within the mobile frame budget (profiled with VS-30).

**VS-25 · Character + NPC sprites (player, Ned, neighbours) · [M1] · `art-pipeline` · deps: VS-23**
Author the player and NPC sprites at the ¾ scale anchor (~64 px heroic, ~1 m footprint) with the 4-facing sheet
and the needed states for the slice: idle, walk, the **fish/haul** work verb, **row/board**, and a catch
celebration. Uncle Ned + 1–2 neighbours as distinct sprites.
- **AC:** Player + Ned + neighbours render at the correct metric scale (a person reads ~64 px against the dory's
  ~144 px). Walk + fish/haul + board animations play cleanly with no sub-pixel jitter. Sprites are atlased per the
  LFS/atlas guidance.

**VS-26 · Boat & fish sprites (Dory, Punt, 6 fish) · [M1] · `art-pipeline` · deps: VS-23, VS-08, VS-11**
Author the **Dory** (~144×51 px hull) and **Punt** (~192×64 px) at true metric footprint with a wake/spray layer
and continuous rotation (engine-rotated single sprite is fine for these small boats), and the **6 fish** sprites
+ icons. Crew/deck-activity overlay anchors stubbed for later.
- **AC:** Dory and Punt render at honest metric scale (the Punt visibly larger) with a speed/turn-scaled wake. The
  six fish have sprites + inventory icons that read at small size. Rotation has no ugly shimmer at the default
  zoom. Replacing a placeholder with the final sprite requires no code change (swap the ref).

---

### Track I — Audio

**VS-27 · Ambient audio bed v0 · [M0] · `audio` · deps: VS-20**
A minimal responsive ambient bed for the greybox cove: lapping calm sea + gulls, with hull slap when aboard the
dory. Just enough that the cove feels alive in M0.
- **AC:** The cove has a calm-sea + gull ambient bed audible on land and at sea; an engine/hull layer is added
  when aboard. Independent volume control exists (at least SFX/ambience). No audio errors on scene load.

**VS-28 · Reactive ambient + adaptive music v1 · [M1] · `audio` · deps: VS-27, VS-05, VS-14**
Layer the ambient bed by sea-state/wind/time and add **adaptive music v1**: a warm Coddle Cove theme that thins
as wind rises (the **rising-wind audio tell** is sacred — P1/P5 early warning), a **catch sting**, and a "made it
home" warmth on return to the wharf. Duck music under important diegetic cues.
- **AC:** Rising wind is **audible before** anything dangerous would happen (and is mirrored by the HUD wind
  widget). A good catch fires a music sting; returning to the wharf resolves to warmth. Beds cross-fade smoothly
  with conditions (no abrupt cuts). Mix respects independent volume sliders.

---

### Track J — Tools, QA & acceptance

**VS-29 · Tide/clock + content authoring tools · [M0→M1] · `tools-editor` · deps: VS-04, VS-10**
An in-editor **time/tide scrubber** (scrub `gameTime`, see tide height/state and the day/season update live) and
friendly inspectors/property drawers for `FishSpecies` (tide/time-window widgets) so designers author content
without code. (A light fish-balance dashboard is a stretch goal here, fuller in M2.)
- **AC:** A designer can scrub time in the Editor and watch the tide gauge/clock respond, and can author a
  `FishSpecies` entirely via friendly inspectors. The tools live behind editor-only assemblies (no runtime cost).

**VS-30 · M0 acceptance + human playtest of the loop · [M0] · `qa-test` · deps: VS-13, VS-15, VS-17, VS-20, VS-27**
Run the **M0 acceptance pass** and a **hands-on human playtest** (the owner + a few testers) of the greybox loop.
Maintain a **core-loop smoke test** that must pass on every build. Capture the verdict: *is the loop fun in
greybox?* (the M0 go/no-go from the roadmap).
- **AC:** The full M0 loop (board → sail → fish → return → sell → sleep → reload) passes end-to-end on a mid-range
  Android device with no blocking bugs and a stable framerate. A documented smoke test exists and passes. A short
  written playtest verdict on loop fun is delivered to the owner (with the explicit GO/POLISH/PIVOT recommendation).

**VS-31 · M1 acceptance + external playtest (soft-launch readiness) · [M1] · `qa-test` · deps: VS-09, VS-14, VS-16, VS-19, VS-21, VS-22, VS-24, VS-26, VS-28, save-migration · deps-note: gates the whole slice**
Run the **M1 acceptance pass** against the Definition of Done (§6) and a small **external playtest** (TestFlight/
closed group). Validate the riskiest bet — that **sailing the dory against wind/tide feels like seamanship and is
one-thumb-comfortable** — by comparing the force model (VS-09) against the virtual-stick alternate. Confirm a save
made on an earlier build still loads (migration). Deliver the M1 go/no-go verdict.
- **AC:** Every §6 DoD item is met on a mid-range phone. External testers complete the slice (inherit dory → learn
  cove → fish → sell → buy Punt) and the loop holds them for repeat sessions. The sailing scheme is judged fun and
  one-thumb-comfortable (documented, with the stick-vs-heading comparison). An old save migrates without loss. A
  written soft-launch-readiness verdict + GO/POLISH/PIVOT recommendation is delivered to the owner.

---

## 5. Dependency map (quick view)

```
VS-01 bootstrap ──┬─ VS-02 input ──┬─ VS-07 dory v0 ── VS-13 fishing v0 ── VS-14 fishing polish
                  │                └─ VS-09 dory force v1 (needs VS-05, VS-08)
                  ├─ VS-03 save
                  ├─ VS-04 clock+tide ─┬─ VS-05 env v1 (sample/wind) ── VS-06 tide table
                  │                    └─ VS-12 resolver ── (VS-13)
                  ├─ VS-10 fish schema ─┬─ VS-11 six fish ── (VS-26 art)
                  │                     ├─ VS-12 resolver
                  │                     └─ VS-15 buyer+price ─┬─ VS-16 Greywick+Punt buy (needs VS-08, VS-22)
                  │                                           └─ VS-18 sell screen
                  ├─ VS-17 HUD v0 ── VS-19 HUD v1 (needs VS-05)
                  ├─ VS-20 Coddle Cove greybox ─┬─ VS-21 Ned+onboarding (needs VS-13, VS-25)
                  │                             ├─ VS-22 Greywick scene (needs VS-16)
                  │                             ├─ VS-24 Cove art pass (needs VS-05, VS-23)
                  │                             └─ VS-27 ambient v0 ── VS-28 audio v1 (needs VS-05, VS-14)
                  ├─ VS-23 art convention ─┬─ VS-24 / VS-25 / VS-26 (art passes)
                  └─ VS-08 boat components ── VS-26 boat/fish art
VS-29 tools (needs VS-04, VS-10)   VS-30 M0 acceptance   VS-31 M1 acceptance (gates the slice)
```

**Critical path to a playable M0:** VS-01 → VS-02/03/04 → VS-07 → VS-10 → VS-11/12 → VS-13 → VS-15 → VS-17/18 →
VS-20 → VS-27 → **VS-30**.
**Critical path to a shippable M1:** the above, then VS-05 → VS-08 → VS-09 (the risk), VS-06/19 (read the sea),
VS-16/22 (Greywick + Punt), VS-23→24/25/26 (art), VS-28 (audio), VS-21 (Ned), → **VS-31**.

---

## 6. Definition of Done for the Vertical Slice (M1)

The slice is **Done** when all of the following are true on a mid-range Android phone (and in the Editor), with no
blocking bugs and a stable framerate:

**The loop**
- [ ] You inherit the **dory** from **Uncle Ned** and complete a guided onboarding that teaches the full loop.
- [ ] You read the **tide** (HUD gauge + the tide table) and it is a real, deterministic force you can plan around.
- [ ] You sail the dory with the **force model** — wind pushes, tide sets, the boat carries way — and it feels
      like **seamanship**, one-thumb-comfortable (validated vs the stick alternate).
- [ ] You **fish** the cozy one-thumb interaction and land any of the **six** Coddle Cove species; the first cod
      *feels* like a triumph.
- [ ] You **sell** to a buyer whose price **moves as you sell and recovers over days**; the sell screen shows the
      marginal price so you learn not to glut.
- [ ] You earn a stake and **buy the Punt** at the Greywick Shipwright (the "real fisher now" beat).
- [ ] You can **sleep** to advance the day and **save/resume anywhere**; an older save **migrates** without loss.

**The feel (P1/P5 + cozy)**
- [ ] Coddle Cove **looks and sounds** like the canon home harbour: tide-aware moving shoreline, animated water,
      day-night grade, gulls + hull slap + adaptive music, the **rising-wind tell** audible before trouble.
- [ ] The opening is **warm and hopeful** (bittersweet — you inherit late Ned's dory & cottage), not grim.
- [ ] The sea reads at a glance (tide gauge, wind widget, compass, set-&-drift), with **redundant coding** that
      works on colourblind palettes.

**The craft (production health)**
- [ ] **PPU=32 / true metric scale** holds everywhere (a person against the dory against the Punt reads correctly).
- [ ] Content is **data-driven**: the six fish, the boats (Dory/Punt), and the tide profile are ScriptableObjects
      authorable with no code change; environment is a pure function of `(seed, gameTime)`.
- [ ] All player-facing strings go through **localization tables**.
- [ ] The build hits the **mobile frame budget** (profiled); the **core-loop smoke test** passes.

**The verdict**
- [ ] An **external playtest** completed the slice and came back for repeat sessions; `qa-test` delivered a
      written **soft-launch-readiness verdict** with a GO/POLISH/PIVOT recommendation for the owner.

> When this checklist is green, M1 is the soft-launch / TestFlight candidate and the owner makes the "is this game
> worth making?" call (see [`../docs/roadmap.md`](../docs/roadmap.md) §6).
