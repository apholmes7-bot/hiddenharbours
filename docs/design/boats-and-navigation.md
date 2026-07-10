# Hidden Harbours — Boats & Navigation

> **Status:** Design module (production-grade, implementable).
> **Canon parent:** [`../vision-and-pillars.md`](../vision-and-pillars.md) — when in doubt, that file wins.
> **Sibling docs:** [`time-tides-weather.md`](time-tides-weather.md) (provides the FORCES interface: `EnvironmentSample` — wind, current, sea state, tide height, water depth, visibility), [`fish-and-content.md`](fish-and-content.md) (species detail; this doc owns only the gear→catch *interface*), [`economy-and-business.md`](economy-and-business.md) (boat purchase, freight contracts, crew wages, tow costs), [`progression-and-housing.md`](progression-and-housing.md) (money/stamina, the shipwright as a property/upgrade hub), [`npcs-and-routines.md`](npcs-and-routines.md) (the shipwright NPC, rescue crews), [`world-and-regions.md`](world-and-regions.md) (region gates, seabed depth fields).
>
> **Pillars served:** **P2 From Dory to Dynasty** is the spine (the 8-tier ladder, branching near the top); **P1 The Sea Has Moods** (navigation is a *skill* because wind+current+tide push you around); **P5 Cozy, but with Teeth** (grounding, capsize, breakdown, stranding & rescue); **P4 Earn It, Then Automate It** (hand-handling first, crew/instruments later).

---

## 0. Design intent (read first)

A boat in Hidden Harbours is a **character you grow into**, not a stat block you swap. Four principles:

1. **The ladder must *read physically*** (P2). Constant PPU=32, 1 world unit = 1 m (canon): a tanker genuinely dwarfs a dory on screen. Going up a tier should feel like trading a kayak for a truck — more reach and capability, but heavier, slower to stop, and out of place in shallow water. **Bigger ≠ strictly better; it's a different tool.**
2. **The sea drives the boat as much as the engine does** (P1). Wind and tidal current (from [`time-tides-weather.md`](time-tides-weather.md)) apply real forces; momentum and inertia mean you *plan* a manoeuvre. A skilled skipper uses the current and wind; a careless one is used by them.
3. **Danger is cozy-with-teeth, never brutal** (P5). You can run aground, swamp, break down, get lost in fog, run out of fuel — and the consequence is **time, money, a lost part of the load, and a tense wait for help**, *not* a punishing death-loss spiral. The first grounding is a gut-punch (canon) and a lesson, not a wipe.
4. **A boat is composable data** (P4 / ADR-0003). Hull + Engine + Hold + Gear + Instruments + Safety are **separate components** assembled from ScriptableObjects, so upgrades are data swaps and the shipwright is a clean UI over them. See [§9 Implementation](#9-implementation-notes).

> **Tuning philosophy:** every number is a default in a `ScriptableObject` (`BoatHullData`, `EngineData`, etc.). Values here feel right but expect playtest tuning. Nothing hard-coded in C#.

---

## 1. The boat ladder (canon "Dory to Dynasty")

Eight tiers, **a branching tree near the top** (canon): the **Lobster Boat** (shellfish specialist) and the **Side Dragger / Trawler** (offshore) are *parallel branches* off the Cape Islander, then both converge into the **commerce tier** (Coastal Packet → Coastal Tanker). You don't have to own every boat — you pick a lane and grow.

```
 Tier 0   Tier 1     Tier 2            Tier 3              Tier 4                Tier 5                  Tier 6              Tier 7
 Dory ──► Punt ──►  Cape Islander ──┬─ Lobster Boat ───┐                                                                
                                    │  (shellfish)     ├─► (branches converge) ─► Coastal Packet ─────► Coastal Tanker
                                    └─ Side Dragger ───┤                           / Freighter            / Cargo Ship
                                       (offshore)      │  ▲ Stern Trawler/Seiner ──┘   (commerce tier begins)
                                                       └──┘ (weather-capable offshore, reaches Ironbound)
```

> Read the tree as: **inshore generalist (Cape Islander) → choose a specialty (lobster vs offshore trawl) → the offshore branch grows to the weather-capable Stern Trawler → everyone converges into freight/commerce (Packet → Tanker)**. The lobster branch is a *viable lifestyle endpoint* and a feeder of capital into the commerce climb (you can stay a lobsterman, or sell up and buy a freighter).

### 1.1 Stats table (the master reference)

> Columns mirror canon §5.4 ("Every tier defines length, draught, hold, crew, range, seaworthiness, handling…") plus fuel/cost/unlock. **Draught varies meaningfully** so deep-draught boats ground in shallow/tidal areas (ties directly to [`time-tides-weather.md`](time-tides-weather.md) §3.5). Units: length & draught in metres (canon scale); hold in **hold-units (HU)**, an abstract capacity unit (1 HU ≈ one standard fish tote / 0.5 m³ — exact mapping in `economy-and-business.md`); range as a relative reach tier; seaworthiness = `maxSafeSeaState` (the 0–8 named scale in [`time-tides-weather.md`](time-tides-weather.md) §4.3); handling = responsiveness rating; fuel as tank size in fuel-units (FU); cost approximate in game currency (₲).

| Tier | Boat | Length (m) | Draught (m) | Hold (HU) | Crew slots | Range | Seaworthiness (max safe sea state) | Handling / responsiveness | Fuel (FU) | ~Cost (₲) | Unlocks at |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **0** | **The Dory** (uncle's) | 4.5 | **0.3** | 6 | 1 | Inshore only | **4 — Popple** | ★★★★★ Nimble but tender | 10 (small outboard) + oars | — (inherited) | Start (Coddle Cove) |
| **1** | **Punt / Skiff** | 6.0 | **0.5** | 14 | 1 | Inshore + near sheltered | 4 — Popple | ★★★★☆ Lively | 25 | ~1,800 | First purchase (shipwright, Coddle Cove/Greywick) |
| **2** | **Cape Islander** (inshore longliner) | 13.0 | **1.1** | 60 | 2 | Coastal | 6 — Knockabout | ★★★☆☆ Sure-footed workboat | 90 | ~14,000 | Greywick story unlock + basic skill |
| **3** | **Lobster Boat** (specialist branch) | 12.0 | **1.0** | 70 (trap-biased) | 2 | Coastal (shellfish grounds) | 6 — Knockabout | ★★★☆☆ Stable, deck-roomy | 85 | ~18,000 | Branch off Cape Islander (lobster path) |
| **4** | **Side Dragger / Trawler** (offshore branch) | 25.0 | **2.4** | 200 | 3 | Offshore (works The Banks) | 7 — Gale | ★★☆☆☆ Heavy, deliberate | 320 | ~70,000 | Branch off Cape Islander (offshore path); gates The Banks |
| **5** | **Stern Trawler / Seiner** | 38.0 | **3.6** | 420 | 5 | Far offshore / weather-capable | **8 — Storm-tolerant (survives gale, hates storm)** | ★★☆☆☆ Big, slow to answer | 700 | ~190,000 | Upgrade from Dragger; gates Ironbound |
| **6** | **Coastal Packet / Freighter** (commerce tier begins) | 60.0 | **4.8** | 1,400 (cargo) | 6 | Inter-island + mainland runs | 8 — Storm-tolerant | ★☆☆☆☆ Ponderous; plan every turn | 2,000 | ~520,000 | Freight/business unlock; Shipping Lanes |
| **7** | **Coastal Tanker / Cargo Ship** | 110.0 | **6.5** | 6,000 (bulk) | 8 (+ delegable) | Long-haul, wider markets | 8 — Storm-tolerant | ☆ Glacial; tug-assisted in harbour | 9,000 | ~2,400,000 | End-game logistics / fleet command |

**How to read the draught column (the tide tie-in):**

- The **Dory (0.3 m)** floats almost anywhere — it can nose into the Sunkers and onto the edge of the Drownded Lands at most tides. That's *why* it's the tutorial boat: forgiving on depth.
- By the **Cape Islander (1.1 m)** you must start respecting low water in the reef and the flats.
- The **Dragger (2.4 m)** and **Stern Trawler (3.6 m)** **cannot enter the shallow tidal regions at low water at all** — they live offshore. Take one into the Sunkers on a falling spring tide and you *will* ground (P5).
- The **Packet (4.8 m)** and **Tanker (6.5 m)** are **deep-water / Shipping-Lanes only**; they need dredged channels and tide windows even to approach a harbour (canon: Tanker is end-game logistics, not a fishing boat). This makes the scale *physical* (P2): the biggest boats literally can't go where you started.

### 1.2 Per-tier feel & role (prose, for the agents writing them)

- **Tier 0 — The Dory.** Uncle's. Oars + a cranky small outboard. Tiny hold, inshore only, *tender* (rolls easily), spray comes aboard in a Popple. Every trip in the dory is intimate and a little precarious — this is where P1+P5 are taught at low stakes. Sentimental; never sold outright (kept as a keepsake/tender even after you move up).
- **Tier 1 — Punt / Skiff.** First *purchase*; the "I'm a real fisher now" beat. A bit more hold and reach, a real (if small) outboard, slightly drier. Still inshore. The proof you can earn and spend (P2 onramp).
- **Tier 2 — Cape Islander.** The iconic Maritime workboat and the **hub of the tree**. Real range, a proper hold, can mount lines *and* traps, a wheelhouse out of the weather. The first boat that feels like a *career*. From here you choose a lane.
- **Tier 3 — Lobster Boat (specialist).** Wide working deck, trap-hauler mount, stable. Optimized for shellfish grounds (the Sunkers, inshore reefs). A *complete life* if you want it — and a cash engine for the commerce climb. Parallel to, not above, the Dragger.
- **Tier 4 — Side Dragger / Trawler (offshore).** The first **offshore-seaworthy** hull — big hold, net gear, can safely work **The Banks** (canon gate). Heavy and deliberate; you feel the inertia. Crosses into open-water danger (P5) and needs real instruments.
- **Tier 5 — Stern Trawler / Seiner.** Larger, weather-capable; reaches **Ironbound** (canon gate). Survives a gale; *respects* a storm. Big crew, big hold, big fuel bills — the top of the *fishing* ladder before commerce.
- **Tier 6 — Coastal Packet / Freighter.** **Commerce tier begins** (canon). Bulk hold, freight contracts, inter-island and mainland runs. You stop thinking like a fisher and start thinking like an operator (P2→ business; ties to `economy-and-business.md`). Ponderous — plan every harbour approach.
- **Tier 7 — Coastal Tanker / Cargo Ship.** End-game logistics, **fleet command** (canon). You rarely hand-steer it; you *direct* it (and a fleet) — the apex of "From Dory to Dynasty." Tug-assisted docking; tide-window-dependent; the world's biggest object, dwarfing everything (P2 scale fantasy fully paid off).

---

## 2. Navigation & handling physics (Unity Box2D-v3 2D)

> Engine: **Unity 6.5, 2D URP**, Box2D-v3 backed `Rigidbody2D`/physics. ¾ top-down, so the boat is a **planar rigid body** (top-down boat sim), not a side-on platformer body. We approximate marine handling with a small, tunable force model — *believable, not a CFD sim* — driven by the boat's controls **and** the environment forces from [`time-tides-weather.md`](time-tides-weather.md) §5.

### 2.1 The rigid body

- Each boat is a `Rigidbody2D` (dynamic), `gravityScale = 0` (top-down), with **mass scaled to displacement** (bigger tier → much larger mass + moment of inertia). Mass and `inertia` come from `BoatHullData` (roughly `mass ∝ length³` so the ladder's inertia gap is dramatic — a tanker has orders of magnitude more mass than a dory).
- **Linear & angular drag** model water resistance, but we override Unity's simple drag with a **directional hydrodynamic model** (§2.4) because boats resist *sideways* motion far more than forward motion — that anisotropy is what makes a boat feel like a boat (it tracks forward, skids reluctantly sideways).

### 2.2 Controls (throttle / rudder / sail)

**Touch-first (mobile, canon).** Default scheme:

- **Throttle:** a vertical slider / two buttons (ahead / astern) giving `throttle01 ∈ [-1, +1]` (reverse is weak, like real props). Bigger boats accelerate **slowly** toward target speed (engine power vs mass).
- **Rudder/helm:** a steering control (on-screen wheel, tilt option, or left-thumb drag) giving `helm ∈ [-1,+1]`. **Rudder authority scales with water flow over the rudder** — i.e. you can barely steer at zero way (dead in the water you can't turn), and steering bites as you make speed. This is *crucial* marine feel: to turn in tight quarters you give a burst of throttle.
- **Sail (sail-relevant craft only).** The **Dory** can ship a small lugsail and the **Punt** a sprit/lug; later working boats are engine-primary (some carry steadying sails, modeled as a passive stability bonus, not propulsion). For sail craft we model: a **sail trim** control (sheet in/out) and the boat gains **thrust from the wind component along the sail's drive direction** (no upwind dead-zone subtlety beyond a simple "can't sail straight into the wind" no-go cone). Sailing is an *optional, fuel-free, weather-dependent* way to move the small boats — pure P1 (you must read the wind). Most players will outboard; sailing is flavor + a fuel-saver + a quiet-mode.

### 2.3 Force assembly (per physics tick)

Each `FixedUpdate` (and at the **4 Hz** environment cadence from [`time-tides-weather.md`](time-tides-weather.md) §8, interpolated between), assemble forces:

```
sample = EnvironmentService.Sample(rb.position, gameTime)   // wind, current, seaState, depth, etc.

// --- 1. Engine thrust (along boat forward) ---
F_engine = forward * engine.maxThrust * throttle01 * engineHealth
//   (reverse: throttle01<0 -> weaker, scaled by engine.reverseFactor ~0.4)

// --- 2. Sail thrust (sail craft only) ---
F_sail   = SailModel(sample.WindVector, boatHeading, sailTrim)   // 0 if no sail / in no-go cone

// --- 3. Tidal current: the water itself moves (boat floats in a moving medium) ---
v_water  = rb.velocity - sample.CurrentVector      // velocity through the water
// --- 4. Wind windage on the relative air velocity ---
v_air    = rb.velocity - sample.WindVector

// --- 5. Directional hydrodynamic drag (anisotropic, §2.4) ---
F_drag   = HydroDrag(v_water, boatHeading, hull)   // strong resist sideways, weak forward

// --- 6. Windage (push on exposed area; big for high-sided/sail craft) ---
F_wind   = -0.5 * RHO_AIR * hull.windageCd * hull.exposedArea * |v_air| * v_air
F_wind  *= sample.GustFactor                       // gusts spike -> heel/broach checks (§3.2)

// --- 7. Rudder torque (authority scales with speed through water) ---
speedThroughWater = |Vector2.Dot(v_water, forward)|
T_rudder = helm * rudder.authority * f(speedThroughWater) * hull.turnResponse
//   f(0)=~0 (can't steer dead in water), rises and saturates with speed

rb.AddForce(F_engine + F_sail + F_drag + F_wind)
rb.AddTorque(T_rudder + stabilizingYawDamping)
```

**Net effect:** with engine off, the boat **sets** (drifts) with the current and is shoved by the wind — exactly the P1 behaviour. With engine on, you overcome them but always *relative* to a moving, blowing medium.

### 2.4 Anisotropic hydrodynamic drag (why a boat feels like a boat)

Resolve `v_water` into the boat's local axes (forward / sideways):

```
v_fwd  = Vector2.Dot(v_water, forward)
v_side = Vector2.Dot(v_water, right)

F_drag_fwd  = -sign(v_fwd)  * hull.dragFwd  * v_fwd^2      // moderate (you glide forward)
F_drag_side = -sign(v_side) * hull.dragSide * v_side^2     // LARGE (hull resists sideslip)
//   dragSide >> dragFwd  (e.g. 6–12×). This makes the boat track, carry way forward,
//   refuse to slide sideways, and skid in turns realistically.
F_drag = forward*F_drag_fwd + right*F_drag_side
```

- **Momentum & inertia:** because mass is large (esp. high tiers) and forward drag is modest, boats **carry way** — you cut throttle and *keep gliding*. Stopping needs reverse or time. **Turning radius** emerges from speed × turn-response ÷ how hard the hull resists the turn; **big boats have wide turning circles and long stopping distances** (canon: heavier/larger = more inertia, harder to stop/turn). Small boats spin on a dime but get knocked about by wind/sea (canon: nimble but vulnerable).
- **Handling rating (★)** in §1.1 maps to `turnResponse`, `engine.maxThrust/mass`, and `rudder.authority`. A dory's ★★★★★ = quick to answer the helm; a tanker's ☆ = you commit to a turn a long way out.

### 2.5 Wind + current = navigation skill (P1)

Concrete skill expressions the model produces *for free*:

- **Ferry-gliding / crabbing:** to hold a straight track across a 2 m/s tidal current, you must **angle the bow up-current** and let the set crab you sideways onto the line. The HUD can show **course-over-ground vs heading** (a "where you're actually going" vector) so the player learns this.
- **Using the tide:** ride a fair tide (current with you) to save fuel/time; **time the slack** to cross Fundy Rips (the 3 m/s rip from [`time-tides-weather.md`](time-tides-weather.md) §3.7 will overpower a small boat at mid-tide — you *must* go near slack).
- **Lee shores & docking:** wind pins you onto or off a wharf; docking a big boat in a cross-wind/cross-tide is a genuine skill moment (and why the Tanker gets tug assist). A **gentle docking assist** (auto-fender / snap when slow & close) keeps it cozy for small boats; big boats stay manual-ish for the satisfaction.
- **Windage matters by size:** the high-sided Packet/Tanker get blown around far more than a low dory for the same wind — different boats, different problems (P2).

### 2.6 Speed, range & fuel

- **Range** (the §1.1 column) is the practical reach before fuel/time forces a return; it scales with tank size, burn rate, and cruising speed. It's a *soft* gate (you *can* push it and risk running dry — P5) reinforced by hard region gates (seaworthiness/draught) and story unlocks.
- **Fuel burn** = `f(throttle, engineLoad, seaState)` — burning more punching into a head sea or against a foul tide. Sailing the small boats burns nothing. Fuel is bought at wharves (`economy-and-business.md`); **running out = breakdown-class event** (§3.6).

### 2.7 The boat rocks on the waves (ADR 0018 — B2 shipped, visual-only)

**Built** (the first seakeeping consumer of the shared deterministic wave field, ADR 0018 Arc B2).
`BoatWaveMotion` (on the boat root) samples `WaveMath` under the hull every frame and decomposes the
surface **slope against the hull's heading** (`BoatWaveMotionMath`, EditMode-pinned): the component
along the bow axis **pitches** (bow riding up the face / dipping into the trough), the component
along the starboard axis **rolls**, and the height **bobs** the whole boat — so *a wave to the beam
rocks the vessel, sailing through the waves to the bow rocks the bow and stern* (the owner's ask,
verbatim), and the response **retargets live as the player turns**. Glass calm is dead still (the
field's amplitudes are exactly 0 at sea state 0 — glass is sacred).

- **Visual-only, by phasing:** the motion is applied to the boat's child *visual* (roll = a small
  additive z-rotation routed through `DirectionalBoatSprite.VisualTiltDegrees`, which composes it
  after that component's per-frame rotation reset; pitch = a subtle screen-vertical offset + tiny
  y-squash; bob = a small screen-vertical lift). The physics body, colliders and `BoatController`
  forces are untouched — **B3** adds the forces (per-hull response on `BoatHullDef`, behind a
  `GameConfig` toggle, punishing-by-place-and-time per the owner's ruling) after the owner's feel
  verdict on B2.
- **Tunables** live on `BoatWaveMotion` (master strength with 0 = off, roll °/slope + cap, pitch
  offset/squash + caps, bob per metre + cap, output smoothing, animator ease/glass-snap). Caps sit
  where the owner's feel pass put them (±9° max roll): readable sea, not broken sprites.
- **Smooth + doubled (owner feel pass, 2026-07-03):** the first playtest read "jittery… especially
  in calm seas" and "could likely be doubled". Cause: the old throttled `TrainsFrom` refresh jumped
  the phase whenever the drifting wind moved the dominant wavelength (k and its dispersion-derived
  c changed under a large running t). Now the trains ride a per-frame **`WaveFieldAnimator`** tick
  (ADR 0018 addendum) — eased parameters, incrementally accumulated phase, continuous by
  construction, glass snap intact — plus a short fps-independent output damping (~0.2 s) on
  roll/pitch/bob, and the default motion amplitudes/caps are **doubled**. The animator is
  presentation-only; B3 forces keep the pure `WaveMath` path.
- **Settings parity note:** the component carries a `WaveFieldSettings` starting from
  `WaveFieldSettings.Default` — the *same* defaults the Art-side shader bridge (B1) publishes, so
  the hull rocks on the waves the player sees. B3/GameConfig will unify the two settings instances
  into one owner-tunable source; until then tune the field's *shape* identically in both places.

---

## 3. Danger (P5) — "cozy, but with teeth"

> The teeth. Each hazard is **telegraphed**, **survivable**, and resolves into **time/money/partial-load** costs and a **rescue beat**, never a brutal wipe. The first time each happens should *teach*, and sting, not crush.

### 3.1 Grounding (draught vs local water depth)

The signature danger, and the tide tie-in.

```
// from the environment sample (time-tides-weather.md §5):
underKeel = sample.WaterDepth - hull.draught     // metres of water under the keel

if (underKeel <= GROUNDING_TOUCH)        // e.g. 0.0–0.2 m
    -> TOUCHING BOTTOM: speed bleeds hard, scraping SFX, helm sluggish (warning state)
if (underKeel <= 0)                      // keel is on the bottom
    -> AGROUND: boat stops, stuck fast
```

- **Telegraph:** a **depth sounder** (instrument, §5) shows under-keel clearance and **alarms** as it shrinks; the water visibly shoals (colour/sprite cues from [`time-tides-weather.md`](time-tides-weather.md) §10 OQ1); the tide table told you low water was coming. Ignoring all three is how you ground.
- **Severity scales with how/where:**
  - **Soft ground** (mud/sand flats, e.g. Drownded Lands): you're just *stuck*. No hull damage. **Wait for the rising tide to float you off**, or kedge/get a tow.
  - **Hard ground / holing** (rock, e.g. **the Sunkers** at speed): possible **hull damage** → *taking on water* (§3.3). Hitting a hidden sunker at speed on a high tide is the nasty one — exactly why you read the tide to keep them visible.
- **Falling vs rising tide (the gut-punch, canon):** ground on a **falling** tide and it gets *worse* — the boat settles, may **list** as the water leaves, and you're **stranded until the tide returns** (could be hours; check the tide table for the next high water). Ground on a **rising** tide and you'll likely float off soon. The tide table turns this from random cruelty into a readable risk.
- **Resolution:** float-off on the tide (free, costs **time**), **kedge/winch** off (minor, if you have the gear), or **call a tow** (§3.7; costs **money**). Hull damage from holing adds a **repair bill** at the shipwright and a bilge-pump fight to get home.

### 3.2 Broaching / capsize (stability vs sea state vs load vs handling)

Bigger seas + bad seamanship = going over. A **stability score** gates it:

```
stability = hull.baseStability
          * loadFactor          // overloaded or badly-trimmed hold lowers it (see §3.5/Hold)
          * (1 - heelStress)    // current heel from wind/turn/wave
          + steadyingSailBonus  // if rigged

// danger driver each tick:
broachRisk = clamp(
      (sample.SeaStateTier - hull.maxSafeSeaState)        // over your seaworthiness?
    + abruptHelmInput * handlingPenalty                   // hard helm in a seaway
    + beamSeaFactor(boatHeading vs waveDir)               // beam-on to big seas is worst
    + sample.GustFactor_spike                             // a gust caught wrong
    - stability , 0, 1)
```

- `broachRisk` doesn't instantly capsize; it drives an escalating **heel/roll** and a **knockdown → capsize** threshold if sustained. The player gets **clear feedback** (the boat heels hard, alarms, spray, screen tilt) and **agency to recover**: ease the throttle, turn bow-to-sea, shed deck load, deploy nothing fancy — just **good seamanship** pulls you back. So capsize is the result of *ignoring* mounting warnings, not a dice roll.
- **Beam seas** (waves on the side) + a **sharp turn at speed** in a high sea state is the classic broach; the model rewards taking big seas **bow-on** and slowing down (real seamanship → P1 mastery).
- **maxSafeSeaState** (the §1.1 seaworthiness column) is the bright line: at/under it you're fine with care; above it, risk climbs fast. This is the direct consumer of [`time-tides-weather.md`](time-tides-weather.md)'s sea-state output.

### 3.3 Taking on water (swamping / leaks)

- Sources: **holing** (hard grounding/collision), **swamping** (a sea breaks aboard in high sea state, esp. an overloaded low-freeboard boat like the dory), or **a sprung leak** (rare wear event on an un-maintained hull).
- Modeled as a **`waterIngress` rate** filling a **bilge level**. Rising bilge **lowers freeboard → lowers stability → raises broachRisk** (a feedback spiral if ignored).
- **Counterplay (P5 "teeth, not brutal"):** a **bilge pump** (manual on small boats — a stamina mini-action; automatic with the powered-pump upgrade, §5) removes water faster than it comes in for *minor* leaks. A bad holing can outpace the pump → you must **run for the nearest harbour/shallows** before the bilge wins. If it wins → **swamp/sink event** = a **capsize-class** outcome (§3.8), not instant death.

### 3.4 Collisions

- Hitting **terrain** (rocks/wharves/land), **other boats** (NPC traffic, esp. busy Greywick & the Shipping Lanes), or **fixed hazards** (sunkers, wrecks, ice floes-as-flavor).
- Box2D handles the impulse; we add **damage scaled by impact speed × relative mass** → hull damage / possible holing (§3.3), plus a **collision penalty** (minor cargo jostle, a scratch repair). Low-speed bumps (docking) are harmless (cozy). High-speed ramming a wharf is expensive but recoverable.
- **NPC boats** give way per simple right-of-way so collisions are *usually your fault* (readable, fair). Fog (low visibility) makes collisions a real risk in The Smother → **radar** (§5) is the answer (P2/P5 payoff).

### 3.5 Load, trim & stability (the cozy-with-teeth of greed)

- Filling the **Hold** past comfortable lowers freeboard and stability (`loadFactor` in §3.2). **Overloading** (a tempting full hold of rare fish in worsening weather) is a *choice* with teeth: a heavy boat in a building sea is far likelier to swamp/broach. This makes the "one more haul vs run for home" decision (the worked example in [`time-tides-weather.md`](time-tides-weather.md) §7) mechanically real.
- **Trim:** wildly uneven loading (all weight aft/forward/one side) adds a **list** and a stability penalty. A light **auto-trim assist** keeps it cozy by default; min-maxers can hand-trim for an edge. (Detail of hold value/sorting in `economy-and-business.md`.)

### 3.6 Engine failure / breakdown & running out of fuel

- **Breakdown** triggers from: **low engine health** (wear from neglect/overrev/overheating — rising probability as `engineHealth` drops), a **collision** to the drive, or **debris fouling the prop** (occasional, region-flavored). **Out of fuel** is a guaranteed breakdown-class stop.
- **Telegraph:** engine note roughens, temperature/oil warning, health bar in the boat panel — **maintenance at the shipwright prevents it** (P4: own your gear). Running low on fuel shows a clear gauge + low-fuel warning.
- **Effect:** you **lose propulsion** — now you're at the mercy of wind + current (you **drift**, §3.8). On small craft you can **row** (the dory's oars!) or **sail** (if rigged) to limp home — a lovely fallback that rewards the humble boats. Bigger boats are stuck and need a **tow** (§3.7). A **minor breakdown** may be field-fixable with a quick stamina/parts action (a tense little repair); a **major** one needs a tow to the shipwright.

### 3.7 The RESCUE / TOW system (canon: stranded & vulnerable until help comes)

When you're aground (and can't kedge off), swamped-but-afloat, broken down, or out of fuel, you're **stranded**. This is the **central P5 set-piece** and it must be **tense but kind**.

**Options, in order of player agency:**

| Option | How | Cost | Feel |
|---|---|---|---|
| **Self-recover** | Float off on the rising tide (grounding); pump out a minor leak; field-fix a minor breakdown; **row/sail** a small boat home; kedge off with an anchor. | **Time** (+ minor stamina/parts). | The cozy, satisfying out — *you handle it.* Always preferred when possible. |
| **Radio for a tow** | **Marine radio** (instrument, §5) calls a **tow operator** out of Port Greywick. They steam to you and tow you to the nearest harbour/shipwright. | **Money** (₲), scaling with **distance from harbour** and **boat size** (towing a tanker costs a fortune). Set in `economy-and-business.md`. | The reliable paid safety net. Costs enough to sting, not enough to ruin. |
| **Harbour rescue** | If you have **no radio** (early game) or can't afford a tow, a **harbour/coastguard rescue** eventually comes (a help NPC notices you're overdue, or you fire a flare from the **safety kit**). Slower to arrive. | **Smaller money penalty** or a **favor/relationship cost** (P3); possibly **lose part of the load** (see below). | The "the town looks after its own" safety net — warm, but humbling. You always get home. |
| **Drift to safety** | Do nothing active: **wind + current carry you** (§3.8). Sometimes drifts you off a bar or toward shore; sometimes into worse water. You can **anchor** to stop drifting and wait. | **Time + risk.** | The gamble. Reading wind/tide (P1) tells you whether drifting helps or hurts. |

**Penalties when rescued (kept gentle — P5 "danger is seasoning, not the meal"):**

- **Time:** the rescue takes in-game hours (you may lose the rest of the working day).
- **Money:** the tow/rescue fee (scaled by distance & boat size).
- **Partial load loss:** in a *bad* event (swamping/capsize, a long exposure), you may **lose a portion of the hold** (washed overboard / spoiled) — **never the whole load** by default, and **never your boat permanently**. Tunable; tutorial regions are gentlest.
- **Repair bill:** any hull/engine damage is fixed (paid) at the shipwright.
- **No permadeath of the boat or skipper.** You're towed in, you pay, you patch up, you go again. The *sting* is real (a lost day, a dented wallet, a humbling); the *spiral* is forbidden (canon anti-pillar: "Danger so punishing it stops being cozy").

**Telegraph & fairness:** every stranding is preceded by ignored warnings (depth alarm, sea-state vs seaworthiness, fuel gauge, engine health, storm warning). The game **always gives you a way home**. Help **takes time and costs**, so you *feel* the consequence and *learn* to read the signs next time (P1) — but you're never actually lost.

### 3.8 Capsize / swamp outcome (the worst case, still cozy)

When stability fully fails (sustained broachRisk → knockdown → capsize) or the bilge wins (§3.3):

- The boat **capsizes / swamps** — dramatic, scary, a genuine gut-punch.
- **Outcome:** you are **not** killed and the **boat is not destroyed**. You end up **stranded & awaiting rescue** (§3.7) — typically the **harbour rescue** path — with the **heaviest (but still partial) load loss** and a **repair bill** to right/refloat and fix the boat, plus the lost time. Possibly a brief "soaked/recovering" stamina hit.
- **Frequency by design:** capsize should be **rare** and **earned** (you ignored escalating warnings, or pushed a tender boat into seas way over its `maxSafeSeaState`, or overloaded into a gale). The dory swamping in a Popple because you greedily overloaded it is a *teaching* capsize; a Stern Trawler only capsizes in a genuine storm you were warned to avoid.

### 3.9 Danger summary table

| Hazard | Trigger | Telegraph | Counterplay | If unresolved → |
|---|---|---|---|---|
| **Grounding (soft)** | draught > waterDepth on mud/sand | depth sounder alarm, shoaling water, tide table | wait for rising tide, kedge, tow | stranded (time) |
| **Grounding (hard/holing)** | draught > depth on rock at speed (Sunkers) | as above + visible rocks at low tide | slow down, read tide; pump if holed | taking on water → tow/repair |
| **Broach/capsize** | seaState > maxSafeSeaState + bad helm/beam seas/overload | heel, alarms, sea-state vs seaworthiness | slow, bow-to-sea, shed load | capsize → rescue (§3.8) |
| **Taking on water** | holing / swamp / leak | rising bilge gauge, lower freeboard | bilge pump, run for shelter | swamp → rescue (§3.8) |
| **Collision** | impact vs terrain/boat (esp. fog) | proximity, radar in fog, NPC give-way | slow, radar (Smother), watch traffic | hull damage → repair/tow |
| **Engine breakdown** | low engine health / fouled prop / collision | engine note, temp/oil warning, health bar | maintain it; field-fix minor; row/sail small boats | stranded → tow |
| **Out of fuel** | empty tank | fuel gauge + low-fuel warning | refuel at wharf; row/sail home | stranded → tow |

---

## 4. Boat upgrades & customization

> Upgrades are **the texture of P2 progression between tiers** and the **answer to every P5 danger**. They're sold/installed by the **Shipwright** NPC (Port Greywick; canon §5.3) and gated by money (and some by story/region). All upgrades are **data swaps on the boat's components** (§9), so the shipwright UI is a clean "slot → choose part" screen.

### 4.1 The shipwright (where upgrades happen)

- **Location:** Port Greywick wharf (canon). A named NPC with routines (P3 — see `npcs-and-routines.md`); relationship can unlock better stock / discounts.
- **Services:** **buy boats** (move up the ladder / branch), **install upgrades** (swap components below), **repair** (hull/engine damage, post-grounding), **maintain** (engine health — preventive; P4), **paint/cosmetics** (pure customization, no stats — express ownership, P2). Costs flow through `economy-and-business.md`.
- **Customization vs upgrade:** *upgrades* change stats; *customization* (hull colour, name, trim, deck details) is cosmetic ownership expression. Both matter for "this boat is **mine**."

### 4.2 Upgrade categories (each maps to a component slot & mitigates a danger)

| Category | Component slot | Examples (tiered) | Effect / danger mitigated |
|---|---|---|---|
| **Engine** | `Engine` | Stock outboard → larger outboard → inboard diesel → high-output → twin-screw | More thrust/top speed, better reverse & rudder authority at low speed, more range; higher tiers needed to push the big hulls. **Maintenance/condition** reduces breakdown risk (§3.6, P4). |
| **Hull** | `Hull` | Reinforced planking → steel plating → ice-strengthened; **freeboard/flare** add-ons | More `baseStability` & `maxSafeSeaState` (seaworthiness), more collision/holing resistance, higher freeboard resists swamping (§3.3). The path to surviving Ironbound storms. |
| **Hold** | `Hold` | Hold expansion, **insulated/iced hold**, live-well, **trim ballast** | More HU capacity; insulation slows catch spoilage (value — `economy`); ballast/trim assist improves stability under load (§3.5). |
| **Gear mounts** | `Gear[]` | Handline rig, **longline drum**, net/trawl winch, **trap hauler** (manual → **electric/powered winch**), dredge | Determines *what fishing methods* the boat can run (§6). Branch-defining (lobster = trap hauler; offshore = trawl winch). The **powered winch** upgrade automates the hand-haul (P4 — §6.3). |
| **Navigation instruments** | `Instruments[]` | **Compass** → **depth sounder** → **radar** → **GPS/chartplotter** → integrated suite | Awareness & danger-warning (next table). The **fog answer** for The Smother. |
| **Safety gear** | `Safety[]` | **Bilge pump** (manual→powered), **life raft**, **flares**, **EPIRB/radio beacon**, fire kit | Directly mitigate §3.3/§3.7/§3.8 — pump out leaks, summon rescue faster, reduce penalties. The "teeth-filing" kit. |

### 4.3 Navigation instruments (detail — ties to fog/The Smother, P1/P5)

| Instrument | Gives | Mitigates / enables |
|---|---|---|
| **Compass** | Reliable heading even with no landmarks (fog/night). | Basic fog/night nav; baseline for The Smother. |
| **Depth sounder** | Live **under-keel clearance** + shoaling alarm. | **Grounding warning** (§3.1) — read the bottom before you hit it. |
| **Radar** | Detects **terrain & other boats through fog** (a sweep overlay). | **Collisions in fog** (§3.4); makes **The Smother** navigable (canon: navigate by instrument). Huge P5→P2 payoff. |
| **GPS / chartplotter** | Your **position + course-over-ground** on a chart even blind; waypoints/routes. | Confident navigation in zero visibility; supports the Smother and long Shipping-Lane runs; enables **route automation** for crewed/fleet boats (P4). |
| **Marine radio** | Live **weather warnings at sea** ([`time-tides-weather.md`](time-tides-weather.md) §4.7) **and** the **tow call** (§3.7). | Early storm warning offshore; the paid rescue net. |
| **Barometer (boat-mounted)** | On-boat pressure trend. | Early weather telegraph without returning to the cottage instrument. |

> **Design payoff:** The Smother (permanent fog) is *unplayable* with a compass alone and *navigable, even cozy,* with radar+GPS — a perfectly legible "upgrade unlocks a region" moment (P2), where the danger (getting lost in fog, colliding) is real until you earn the instruments (P5).

### 4.4 Safety gear (detail — files the teeth, P5)

| Item | Effect |
|---|---|
| **Bilge pump** (manual → powered/auto) | Removes `waterIngress`; manual = stamina action, powered = passive. The leak counterplay (§3.3). |
| **Flares** | Summon **harbour rescue** faster / when radio-less (§3.7). |
| **EPIRB / radio beacon** | Auto-broadcasts your position when stranded → **faster, cheaper rescue**, **smaller load loss**. The premium safety net. |
| **Life raft** | Reduces the personal/penalty severity of a capsize/swamp (§3.8) — you're never in real danger, but it softens the event further (cozy reassurance). |
| **Fire kit / extras** | Handle minor onboard incidents; mostly flavor + small mitigation. |

### 4.5 Upgrade → progression mapping (P2/P4)

- Upgrades are **incremental power between the big tier jumps**: you can't afford the next boat yet, but a bigger engine + a depth sounder + a bilge pump makes your current boat safer and more capable *now*. This keeps progression dense and legible (no dead stretches).
- **Earn-it-then-automate-it (P4):** early you **hand-pump the bilge, hand-haul gear, hand-steer**; later upgrades (powered pump, line/trap haulers, GPS route-following with crew) **automate the tedium** — the canon arc from laborer to owner, expressed in boat hardware.
- **Money sink & sequencing:** upgrade costs (in `economy-and-business.md`) are tuned so the player is always weighing *upgrade the current boat* vs *save for the next tier* — a constant, healthy economic tension (ties to the market loop, P2).

---

## 5. Boat customization data note (so §6 reads cleanly)

The boat the player drives is the **sum of its installed components** (Hull/Engine/Hold/Gear/Instruments/Safety). Every danger and capability above reads from those components, not from a monolithic "boat stat block." The tier (§1.1) defines the **chassis** (mass, base hull, slot counts, the floor/ceiling on what fits); upgrades fill the slots. This is the architecture in §9 — surfaced here because the **fishing interface (§6)** depends on which **Gear** is mounted.

---

## 6. Fishing gear interface (high level)

> This doc owns the **interface** — how gear + boat + region + tide *gate* what's catchable. **Species detail, catch rates, and tables live in [`fish-and-content.md`](fish-and-content.md)** (canon: 100 species as data assets). Here we define the contract.

### 6.1 Gear methods (mounted as `Gear` components)

| Method | Boat fit | How it plays | Typical catch class (defer to fish doc) |
|---|---|---|---|
| **Handline** | Any (the dory's starting method — hand-hauled, P4) | Drop/jig a line; an active, hands-on mini-interaction. Low volume, high engagement. | Inshore groundfish, the tutorial catch. |
| **Longline** | Cape Islander+ (longline drum) | Set a baited line, **soak** it over time, haul it (drum-assisted on bigger boats). Volume scales with line length. | Coastal groundfish, some pelagics. |
| **Net / Trawl** | Dragger/Stern Trawler (trawl winch) | Tow a net through a region for a duration → bulk haul. The offshore branch's bread and butter. | The Banks groundfish, big pelagic volume. |
| **Traps / Pots** | Lobster Boat / any with trap hauler | Set pots, leave them to soak, haul later (the lobster branch's loop). Spatial + time management. | Shellfish — lobster, crab (the Sunkers). |
| **Dredge** (optional/late) | Specialist mount | Drag the bottom for shellfish on flats/banks. | Scallops/clams, flats & banks. |

### 6.2 The catch-gating contract

What you can catch at a given moment is the **intersection** of four inputs — this doc defines the inputs; the fish doc resolves them into a species/quantity roll:

```
catchContext = {
   region        : RegionId,                 // where you are (gates the species pool)
   gearMethod    : GearMethod,               // what you're fishing with (gates accessible species/sizes)
   gearQuality   : tier/condition,           // better gear -> better odds/volume
   boatTier      : BoatTier,                 // hold capacity caps a haul; range gates which regions you reached
   tideHeight    : float,                    // some species/spots only at certain tide states (flats clams at low water)
   tideRateNorm  : float,                    // slack vs running tide affects some pelagics/rips
   seaState/season/timeOfDay : from EnvironmentSample,  // weather & clock weighting
}
// -> fish-and-content.md consumes catchContext and returns the actual catch.
```

- **Region** sets the species pool (inshore vs Banks vs Ironbound vs flats vs Smother).
- **Gear** decides *which* of that pool you can take and *how much* (you won't trawl up a lobster; a handline won't fill a hold).
- **Boat** caps the haul (hold HU) and — via range/seaworthiness/draught — *whether you could even get to that region at that tide/weather*.
- **Tide** opens specific opportunities (clams on the Drownded Lands at low water; fast pelagics through Fundy Rips on a running tide) — the P1 tie-in: *when* you fish matters as much as *where*.

> **Boundary discipline:** if an agent needs to know *what fish, how many, at what value* — that's [`fish-and-content.md`](fish-and-content.md). If they need *what gear/boat can reach/work a region at a given tide & weather* — that's here.

### 6.3 Trap-hauling interaction — the lobster loop (phased **M2**)

> **Future work (M2 — the lobster gear / specialist branch).** Captured here because it is an
> on-water *boat* interaction; the species / bait / soak side lives in
> [`fish-and-content.md`](fish-and-content.md) §3.5(b).

The lobster loop the owner specifies, expressed as a boat interaction:

1. **Set** a baited trap (`Pots`/`Trap`); it drops to the bottom marked by a **surface buoy**.
2. **Return and lay alongside.** You bring the boat **beside the buoy** and **hold station** — a real
   handling beat, because wind, current, and tide **set you off the mark**, so approaching the buoy
   cleanly is itself a small navigation skill (P1).
3. **Leave the helm to haul.** You **step off the wheel to port or starboard**, **gaff the buoy**, and
   **haul the trap** — while the boat, helm unattended, **drifts with wind and current** (§2.3). You
   pick your moment and your side, or you re-approach. This "**leave the helm, work the rail**" beat
   is the tactile heart of trap fishing and a deliberate cozy-with-teeth bit of seamanship — drift
   onto a sunker while you're heads-down hauling and that's on you (P5).
4. **Haul by hand, then winched (P4).** Hauling without a powered mount is a **stamina action**; the
   **electric-winch upgrade** (a powered `trap hauler` in the **Gear mounts** slot, §4.2) hauls the
   pot for you — the canon "earn it, then automate it" arc expressed in deck hardware. Some boats
   mount the winch, some don't (branch/tier-gated).

> **Built — the playable manual loop (trap arc Build 4, greybox; haul redesigned Build 6).** The whole
> hand loop is now playable end-to-end: **set → soak → lay alongside → haul with the swell → collect →
> sell**. Two new pure, EditMode-pinned pieces plus a driver, all Fishing-lane (`Code/Fishing`):
> - **Depth-gated placement** (`TrapPlacement` + `PlacedTrapService.TryPlaceGated`) — a pot may be set
>   only where the water is deep enough for the Def's `MinSoakDepthMeters` (the **inverse** of the clam
>   dig's exposure gate; the *same* `waterLevel − terrainElevation` the walkability/boat-cross/shader
>   read) and only with the required **bait in stock**, consuming one. Refusals are cozy no-ops.
> - **The haul-with-the-swell minigame (the owner's redesign: a richer, faster, DIEGETIC action)**
>   (`TrapHaulController` + `TrapHaulMath`) — lay the boat **alongside** a buoy, interact to start, then
>   **HOLD with the swell**: as the sea **lifts** the boat and pot the rope eases — **hold to take line
>   in**; as it **drops** into the trough the rope **loads up** — holding through the drop **strains and
>   slips line back** (the rope fights you). So the play is **hold on the lift, ease on the fall** —
>   continuous engagement, physically true, read straight off the **shared deterministic wave field**
>   under the buoy (the same height read the buoy bobs on and the hull rocks to, §2.7). **Calm ⇒ a quick,
>   forgiving steady wind-in (no swell to time); a big sea ⇒ a real fight** where a clean haul (hold the
>   lifts) far outpaces a sloppy one (P5 teeth — the swell-coupling knob). **Diegetic, low-HUD (owner's
>   strong direction):** the read is the **rope in the world** — **slack on the lift** (take now),
>   **taut + shuddering on the drop** (ease off), shaded by strain, the pot rising — plus a
>   `TrapHaulStateChanged` **audio hook** (creak/strain cue for the audio lane). **No HUD meter/bar and no
>   per-pull timing TEXT** — the rope carries the timing; the toasts carry only OUTCOMES. Mapped to
>   **KB/mouse + gamepad** (H to start, Space/click/gamepad-South to hold). **Cozy — no penalty (owner's
>   M2 call):** missing the phase slips line back and costs **time**, but you never lose the catch, the
>   pot, or take damage.
>
> Only a **ready (soaked)** trap yields; an unsoaked haul surfaces empty ("not ready yet"). The minigame
> is the **ACT** of retrieving — it does **not** re-roll or gate *what* is caught (that's fixed by
> soak + bait + seed in Build 3, rule 5); on surface it lands Build 3's deterministic catch into the
> hold via the rod/clam land path (sellable through the existing sell point). **Still to come (later
> builds):** the **winch** (automates the hand-haul, Build 6), the on-deck **free-roam walk / leave the
> helm** (Build 5/7 — the greybox hauls from the boat, not yet a walked deck), and a real
> trap-**purchase** economy offer (the greybox dev-grants trap + bait). The **catch region** currently
> uses `region.coddle_cove` because the lobster/crab are authored for the cove; region-tagging them for
> St Peters is an economy-sim/world follow-up.

**Fuel reminder (already canon — §2.6 / §3.6):** the boats you run these from are **engine boats that
consume fuel (FU)**. Every soak-and-haul run spends fuel, fuel is bought at wharves
([`economy-and-business.md`](economy-and-business.md)), and **running dry is a breakdown-class event**
(§3.6). The dory and punt can **row or sail** home fuel-free; bigger boats cannot. *(St Peters note:
the prologue dory begins **broken and hauled out** — the opening's whole goal is to **repair** it,
[`world-and-regions.md`](world-and-regions.md) §6.0 — after which it burns fuel like any outboard
craft. Phased **M2**.)*

---

## 7. Cross-doc data flow (summary)

```
 time-tides-weather.md
   └─ EnvironmentService.Sample() ──► EnvironmentSample {wind, current, seaState, tideHeight, waterDepth, visibility, gust}
                                          │
                                          ▼
            ┌──────────────── Boat physics (this doc §2) ───────────────┐
            │  forces: engine + sail + hydroDrag + windage + rudder     │
            │  reads waterDepth/draught -> grounding (§3.1)             │
            │  reads seaState/maxSafeSeaState/load -> broach (§3.2)     │
            │  reads visibility -> instruments value (§4.3)            │
            └───────────────────────────────────────────────────────────┘
                                          │
            ┌──────────── catchContext (this doc §6) ──────────────┐
            │  region + gear + boatTier + tide + weather           │──► fish-and-content.md (resolves catch)
            └──────────────────────────────────────────────────────┘
                                          │
        money/upgrades/tow costs ◄──────────────────────► economy-and-business.md
        shipwright / rescue NPCs ◄────────────────────────► npcs-and-routines.md
```

---

## 8. Worked "buying up & getting caught" (feel check, ties to pillars)

> *Skipper has saved for the Cape Islander (P2 milestone) and rigged a depth sounder + bilge pump (P5 mitigation).*
> **Skill (P1):** crossing toward the Sunkers on a falling tide, the **depth sounder alarms** — under-keel down to 0.4 m. The 1.1 m draught means the channel that was fine for the dory is now marginal. The skipper **eases off, crabs up-current** (the flood is setting them toward a sunker), and threads the visible-at-low-water rocks. Pure navigation-as-skill.
> **Teeth (P5):** greed kicks in — a great lobster soak means staying past low water. On the way out, distracted, the skipper clips a sunker at speed → **holing**, **bilge rising**. The **pump** buys time; they **run for Greywick**, pumping, bilge gaining slowly. They *just* make the wharf. **Repair bill, a humbling, half a day lost — but home.** Next spring tide, they read the table first and stay clear. **That loop — capability earned, danger survived, lesson learned — is the game.**

---

## 9. Implementation notes

### 9.1 Component architecture (a boat is composable data — P4 / ADR-0003)

A boat = a `Boat` aggregate composed of swappable component-data + small runtime behaviours:

```csharp
class Boat {
    BoatHullData      Hull;          // tier chassis: mass, inertia, baseStability, maxSafeSeaState floor,
                                     //   draught, dragFwd/dragSide, windageCd/exposedArea, turnResponse, slot counts
    EngineComponent   Engine;        // EngineData (maxThrust, reverseFactor, burnCurve) + runtime engineHealth, fuel
    HoldComponent     Hold;          // capacity HU, current load, trim/loadFactor, spoilage params
    List<GearMount>   Gear;          // installed fishing methods (handline/longline/trawl/trap/dredge)
    List<Instrument>  Instruments;   // compass/sounder/radar/gps/radio/barometer (capabilities/flags)
    List<SafetyItem>  Safety;        // bilge pump/flares/EPIRB/raft
    CosmeticData      Cosmetics;     // paint/name/trim (no stats)

    // runtime state
    Rigidbody2D rb;                  // Box2D body (mass/inertia from Hull)
    float bilgeLevel, engineHealth, fuel, heelStress;
}
```

- **All component data are ScriptableObjects** (mirrors fish/economy/environment data-driven approach). Upgrading = replacing a component reference at the shipwright; nothing in physics special-cases a boat by name.
- **Stats derive from components:** `maxSafeSeaState`, `baseStability`, `mass`, drag coefficients, capacity, instrument capabilities are all read from the assembled components — the §1.1 table is the *default chassis + stock fit*; upgrades modify it.
- **`BoatPhysicsController`** (one MonoBehaviour) reads components + the per-tick `EnvironmentSample` and assembles the forces in §2.3. **Danger systems** (`GroundingCheck`, `StabilityCheck`, `IngressCheck`, `EngineHealth`, `FuelCheck`) are small components that read the same sample + boat state and raise events (`OnAground`, `OnBroaching`, `OnTakingWater`, `OnBreakdown`, `OnStranded`) consumed by a `RescueController` (§3.7) and the HUD.

### 9.2 Physics tuning

- **Mass/inertia** from `Hull` (`mass ∝ length³` scaled to feel; clamp so the dory isn't *too* twitchy and the tanker isn't unmovably slow). Tune `dragSide/dragFwd` ratio (~6–12×) for the "tracks forward, skids reluctantly" feel.
- **Rudder authority curve** `f(speedThroughWater)`: zero at zero way, rises, saturates — tune so tight-quarters handling needs throttle bursts (real, satisfying).
- **Stability/broach thresholds**: tune `broachRisk` weights so capsize is **rare and earned** (§3.8) — a tender dory swamps if abused; a Stern Trawler only goes over in a true storm. Validate against the named sea-state tiers.
- **Determinism note:** physics is *not* required to be bit-deterministic across machines (it's real-time, single-player, player-driven), but **the environment it reads is** ([`time-tides-weather.md`](time-tides-weather.md) §9). Saves store **boat state** (position, velocity may be reset to rest on load, component fit, health/fuel/bilge, load) — *not* a physics-frame snapshot.

### 9.3 Mobile performance (canon mobile-first)

- **One active player boat** dominates; AI/crew/freight boats are **abstracted** when off-screen (they don't run full physics — they move on routes/timers; see `economy-and-business.md`/`npcs-and-routines.md`) and only spin up a lightweight body when visible.
- Environment sampled at **4 Hz** per active boat (interpolated) — cheap (§ env doc §8). Danger checks piggyback on the same cadence; no per-frame allocation (struct samples, pooled effects).
- Keep colliders simple (a few-vertex hull polygon, not pixel-perfect); use Box2D-v3's solver settings tuned for stability over precision; cap simultaneous on-screen boats (Greywick/Shipping Lanes traffic) with LOD/abstraction.
- Instruments (radar/GPS overlays) render on demand, not continuously when stowed.

### 9.4 Save data (what persists)

`{ boatTier, componentFit (Hull/Engine/Hold/Gear/Instruments/Safety/Cosmetics refs), engineHealth, fuel, bilgeLevel, hullDamage, hold load, lastPosition/harbour, ownedBoats[] }`. Combined with the environment doc's `{seed, gameTime}`, the world reconstructs fully on load.

### 9.5 Board / disembark verb & control re-bind (greybox)

The on-foot ⇄ aboard control loop is the `ControlSwitcher` (Player lane); several playtest fixes hardened it:

- **Disembark only onto a standable step-off** (never over open or merely-shallow-but-submerged water).
  Aboard, INTERACT disembarks when the boat is **at an authored dock/wharf** (`InDockZone()` — you step onto
  the planks) **OR over standable LAND** (`OnLand()`). `OnLand()` reads two independent tells (either
  suffices, but **both require actual land**): the authored **tidal terrain is EXPOSED** under the boat —
  the deterministic `WaterDepth = WaterLevel − groundElevation` (via `BoatCrossing.DepthAt`) is **≤ 0**, i.e.
  the ground is at/above the water line (a bared flat/bar) — and/or a **physical land/shore collider** within
  a probe radius (for non-tidal regions like the cove, whose hard shore-edge has no height map). The earlier
  **"shallow-but-submerged depth" allowance is gone** (owner playtest): merely-shallow water that's still
  submerged, with no dock or land under you, is *not* a step-off — you can't disembark onto water. At the
  dock you land tidily on the planks; away from the dock you step off at the boat onto the bared land.
- **Board from anywhere** within reach of the boat (`WithinBoardReach()` — a pure proximity radius), not only
  at a dock zone (owner playtest). So you can step aboard a boat nudged up to a beach, not just one at the
  wharf. (The damaged-dory repair gate still applies on top, P5.)
- **Hold / root the mooring line** — the **rope / mooring mechanic** (`BoatMooring`, Boats lane; P1 + P5).
  This *replaces* the earlier auto-tie-on-disembark with the owner's refinement:
  - **On disembark the player HOLDS the rope** (`Hold(player)`): the line is made fast to the **player's own
    position**, so the boat is tethered to the player and trails them on the leash as they move. A quick
    hop-off never loses the boat (P5 cozy).
  - **Press `Q` to ROOT the line to the ground** at the player's feet (`ToggleMooring` → `Root`): the boat
    now tethers to that **fixed spot** and the player is free to roam. **`Q` again** takes the line back in
    hand (`Hold`). Re-boarding (`E`) **stows** the rope (the helm takes over).
  - **The boat always drifts on its current tether** (the player's hand while held, the ground spot while
    rooted) via the deterministic wind + tidal-current force model (`BoatMooring.DriftForce` — the same
    set-with-the-weather model the helm applies with the throttle let go).
  - **The rope behaves like a ROPE, not a rubber band.** Inside rope-length the line is **slack** and does
    nothing — the boat moves freely (bobs/swings) on wind + tide. At the end of the rope it hits a **FIRM,
    near-inextensible limit**: `BoatMooring.TetherForce` applies a stiff restoring force only on the small
    *excess past `ropeLength + give`* plus strong outward-velocity damping (so she's arrested cleanly at the
    limit, not pulled back softly in proportion to stretch), and a **hard positional clamp**
    (`ConstrainToRope`) guarantees she can never sit more than the tiny `give` past rope-length (the
    "inextensible" part). The greybox `LineRenderer` draws the **slack rope as a drooping catenary** that
    straightens and goes taut only at the limit (`Slack01` / `SampleRopeCurve`).
  - **Tunables are owner-editable serialized fields, no magic numbers**: rope length, the firm-limit give /
    stiffness / damping, and the slack-sag amount on `BoatMooring`. Drift uses only the deterministic
    `EnvironmentSample`; the tether is a pure physics constraint (firm limit + damping + positional clamp) —
    nothing saved, no RNG (CLAUDE.md rule 5). The constraint + drift + curve math are pure static helpers,
    EditMode-tested (slack-inside vs firm-limit; held-at-the-rope's-end vs untethered-runs-away;
    inextensible clamp; the hold/root/board state machine; disembark-only-on-land; board-from-anywhere;
    force determinism). The greybox rope is a placeholder; the FEEL is the point — the pretty rope is a
    later art pass.
- **Control survives a region hop.** The persistent rig (player/boat/switcher) is `DontDestroyOnLoad`
  and carries the control **mode** across an additive region toggle, but nothing re-enabled the active
  boat's controller + input to match it on arrival — so a re-activated region (especially a **return**
  trip) could leave the helm dead. `RegionTravelCoordinator.ApplyArrival` now calls
  `ControlSwitcher.ReassertControlMode()` (idempotent) on **every** arrival, re-enabling boat-or-foot
  control to match the persisted mode and re-raising the camera signals; the just-teleported boat is
  also `Stop()`-ed so a stale velocity doesn't carry it off the arrival mark. Works for both the rowed
  Dory and the engine Punt.
- **The region passage can't re-fire on the just-arrived boat (helm-drop fix).** A `RegionPassage` is a
  forgiving trigger band at the shore↔open-water boundary; **any** collider entering it took the crossing.
  Two ways it double-fired and dropped the helm — and *every* fire re-runs travel, which teleports +
  `Stop()`s the boat and re-binds control (a beat of dead helm, then recovery): (1) the boat **lingered in
  / nudged back into** the wide band while crossing; (2) when the destination region's scene root is
  toggled back on, Unity **re-raises `OnTriggerEnter2D` on the boat already overlapping** the passage (the
  scene-toggle "bounce"). `RegionPassage` now guards with three layers so it fires **once per genuine
  crossing, never on the boat that just arrived**: a **leave-then-enter latch** (it won't re-arm until the
  body has exited and re-entered), a **cooldown debounce** after a fire, and **priming OFF on enable** (a
  freshly activated/arrived region starts un-primed). The decision is a pure, EditMode-tested function
  (`RegionPassage.ShouldFire`), owner-tunable (`_reentryCooldownSeconds`), nothing saved. So the helm stays
  live crossing the boundary repeatedly, for both the rowed Dory and the engine Punt.

### 9.6 Mooring — future work (cleats / posts / placed tie items, and a second line) *(NOT built)*

The current rope makes fast to one of two **tie targets**: the **player's hand** (held) or a **fixed ground
spot** (rooted). Both are an `IMooringAnchor` (`MooringAnchor.cs`) — an interface the tether reads a live
`Position` from each tick — so the mechanic is already structured to grow **without reworking the rope
physics**:

- **Dedicated cleats / posts / user-placeable tie items.** A cleat on the wharf, a piling, or a tie-post the
  player **places** is just another `IMooringAnchor` (a `FixedAnchor` at its position, or a component that
  supplies its own). Rooting to one instead of the bare ground becomes a target-selection choice (snap to the
  nearest in-reach cleat); the firm/slack tether and the LineRenderer are unchanged. A placed tie-item would
  be **content/data** (a small `Def` + a world prop), keeping it in-lane with ADR-0003. This pairs with the
  shipwright/harbour build-out and the lobster-buoy work (§6.3).
- **Two lines (a bow line + a stern line).** A larger or more exposed berth wants the boat held at **two
  points** so she lies alongside instead of swinging on a single leash. That is two `BoatMooring`/anchor
  pairs (a bow anchor + a stern anchor, each its own rope), with the per-line firm/slack physics applied at
  the bow and stern attach points rather than the hull centre — a natural extension of the single-line model,
  not a new mechanic. It also unlocks **springs/breast-lines** flavour and a real "make her fast fore-and-aft"
  docking beat (P1 seamanship).

Both are deliberately deferred (out of the current greybox phase); the single rope-to-player/ground is the
working mechanic. Captured here so the structure stays honest and the later pass is a fill-in, not a rewrite.

### 9.7 Boat wake (the foam trail) — visual-only, reads the sim

A moving boat leaves a **foam-particle wake** that **follows the boat, travels with the tidal current as the
waves distort it, and dissipates once it loses force a distance astern** (the owner's brief). It is a pooled,
self-installing, **visual-only** effect that **reads** the deterministic sim (boat `Velocity`/`IsAground`/bow,
and the Core `EnvironmentSample`'s `CurrentVector` + `SeaState` — the *same* current and sea-state the water
shader reads, so wake and water move together) and **drives no sim, saves nothing** (rule 5). Full design,
the four-point mapping, the tunable list and the test coverage live in
[`boat-wake.md`](boat-wake.md); the code is `Code/Boats/WakeParticleSystem.cs` (pure feel-math) +
`Code/Boats/BoatWakeEmitter.cs` (the self-installing driver — no builder change). Because it self-installs
(a `RuntimeInitializeOnLoadMethod` host, like the grass-wind bridge), no scene or builder needs editing.

### 9.8 On-deck camera zoom (control-mode-keyed, pixel-perfect steps)

Owner playtest (2026-07-08): *"when in the back of the boat the screen zooms in more, allowing for more
detailed boat gameplay."* Built as a **diegetic zoom, not a picture-in-picture window** (a PiP is HUD and
against the low-HUD direction; the zoom feeds the same goal and the coming deck-workspace vision):

- **Stepping ON DECK steps the camera IN one discrete pixel-perfect step** past the on-foot framing
  (default 6.75 m of world height = the exact **×5** PPU-32 step at 1080p; on foot is ×4, the helm keeps the
  hull's data-driven framing). The boat fills the screen and deck work — pots, bait, the rail — reads in
  detail. The helm (`Aboard`) and walking ashore (`OnFoot`) keep their existing framing untouched.
- **A LIVE trap haul (tunably) tightens one step more** (default 5.625 m = the exact **×6** step) so the
  rope-and-buoy action is the star; it **releases the moment the pot surfaces or the haul goes idle**. The
  extra tighten can be disabled entirely (`_haulTightensZoom`).
- **Never an arbitrary ortho zoom** — every stop is a PPU-integer Pixel-Perfect step (the ratified
  per-context discrete-zoom vision); a short ease bridges the steps with the Pixel Perfect Camera paused for
  just those frames, then snaps crisp onto the new step (the same mechanism as the boat-upgrade beat).
- **Signal-driven through Core only** (rule 4): the App camera (`CameraFollow`) listens to
  `ControlModeChanged` / `TrapHaulStateChanged` on the EventBus — it never references Player/Boats/Fishing.
  The decision (mode→step mapping + a **commit hold** so rapid helm⇄deck hops collapse into one re-zoom, and
  a there-and-back hop re-zooms zero times) is a pure, EditMode-tested POCO (`CameraZoomPolicy`).
- **Owner-tunable, no magic numbers** (rule 6), serialized on the camera: the deck and haul step heights,
  the haul-tighten toggle, the deck-step ease seconds (0 = snap), and the anti-thrash hold seconds.
  Nothing is saved; the zoom is derived state, recomputed from the live control mode.

---

## 10. Open questions

1. **Direct boat control vs point-to-move (mobile).** Do we offer **full manual** throttle+helm (best for P1 skill, harder on touch) and an optional **assisted/auto-pilot-to-waypoint** for transits/accessibility — and where exactly does autopilot disengage near danger? Likely both, with autopilot for known-safe transits and manual demanded in tide/weather pinch points. Needs UX prototyping.
2. **Sailing depth.** How much sail nuance for the dory/punt — simple drive-component only, or a richer points-of-sail/no-go-zone model? Keep it light (it's flavor + fuel-saver), but confirm it's *fun* not fiddly on touch.
3. **Capsize consequence ceiling.** Exact partial-load-loss % per event severity, repair cost curves, and whether a worst-case can ever *cost the boat* (recommend **never** — only money/time/partial load; keep the boat). Lock with economy.
4. **Tow economy balance.** Tow/rescue pricing vs the player's wallet at each tier so it *stings but never spirals* (canon). Coordinate with `economy-and-business.md`; tutorial regions should have a cheap/free safety net.
5. **Fuel friction.** Is fuel a meaningful resource throughout, or mostly an early/mid concern (with later boats so capable it fades)? Decide whether running dry stays a real threat at high tiers or becomes a non-issue.
6. **NPC traffic & right-of-way fidelity.** How smart must NPC boats be to keep collisions "your fault" and fair, especially in busy Greywick and the Shipping Lanes, without heavy AI cost on mobile?
7. **Branch convergence requirement.** Must a player who took the **Lobster** branch buy back through an offshore boat to reach the **commerce tier**, or can a successful lobsterman jump straight to a Coastal Packet with enough capital? (Recommend: capital-gated jump allowed — money is the great converger — but confirm it doesn't skip needed seamanship learning.)
8. **Multi-boat / fleet control UX (Tier 6–7).** When you command a fleet (canon end-game), how much is hand-steered vs dispatched-on-routes? This is where P4 automation peaks — needs its own design pass (likely in `economy-and-business.md` for the logistics layer, with this doc owning the per-hull physics).
9. **Depth representation handoff.** Confirm with [`time-tides-weather.md`](time-tides-weather.md) §10 OQ1 and `world-and-regions.md` exactly how `seabedElevation`/`waterDepth` is authored per region (heightfield texture vs tile metadata) so grounding reads cleanly against the same data the water visuals use.
```