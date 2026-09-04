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
| **1** | **Punt / Skiff** | 6.0 | **0.5** | 14 | 1 | Inshore + near sheltered | 4 — Popple | ★★★★☆ Lively | 25 | ~1,800 | First purchase (shipwright, Coddle Cove/Nine Mile Creek) |
| **2** | **Cape Islander** (inshore longliner) | 13.0 | **1.1** | 60 | 2 | Coastal | 6 — Knockabout | ★★★☆☆ Sure-footed workboat | 90 | ~14,000 | Nine Mile Creek story unlock + basic skill |
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

### 2.7.1 The hull answers the storm (ADR 0018 — B2.5 shipped, visual-only)

**Built** (owner ask 2026-08-05: *"is there steep enough front-to-back rocking to represent the
storm waves? … It must stay smooth and obey gravity though"*). B2's read had a fixed ceiling the
sea could not grow past: the rock-grid/mesh rock was **phase-only** (the baked/def amplitudes drew
the same attitude in a chop and a gale — and a gale's longer swell cycled *slower*, so it read
calmer), and the transform path pinned against caps sized on the calm feel pass while the field's
slope itself **saturates by construction** (dominant wavelength grows with wind, so amplitude ×
wave number flattens above mid-sea). B2.5 makes the response grow off the same deterministic
`SeaState01` axis everything else scales with (`StormRockMath`, policy in **`GameConfig.StormRock`**
— the owner-tunable home):

- **Sea-state-proportional response:** above a **storm-start** sea state (default 0.4, just above
  Chop) a blend curve grows every transform gain AND cap (default ×2.2 at full storm), and a mesh
  hull's def rock amplitudes with them — a gale visibly outranks a chop; at or below the start the
  blend is **exactly 0** and the owner's tuned calm read is **byte-identical** (EditMode-pinned
  negative control).
- **Real storm pitch on the mesh fleet:** continuous hulls additionally take heading-decomposed
  attitude through the presenter seam (`IBoatHullPresenter.SetStormRock`) — the dominant swell's
  slope, split against the heading, **pitches** the bow (up to +10° at defaults) and **rolls**
  the deck (+8°), retargeting as the player turns. The extras are **phase-locked to the same
  dominant-train phase the rock cycle rides** (amplitude from the eased envelope, waveform =
  cos of the posed phase): the drawn channels stay one cycle's sine/cosine pair, so the
  pre-existing mesh-rock smoothness pins hold by algebra — the first cut drove them from the
  multi-train smoothed slope and the smoothness guard caught it on CI (accel ratio 3.68,
  phase reversals). Sprite-frame hulls cannot grow their baked attitude (an art re-bake call);
  they gain the honest **surge** instead — the pitch offset + squash layered *under* the frames,
  which also carries continuity between the 45° frame steps. The frame *selection* (crest → 2,
  trough → 6, forward-phase walk) is untouched and pinned.
- **Weight — the ride obeys gravity:** the displaced-sea ride now passes through a spring-damper
  chase (`StormRockMath.StepHeaveWeight`) whose **downward acceleration is capped at g** (the wave
  field's own `Gravity`): crossing a sharpened storm crest the surface can drop faster than
  gravity, and the hull now unweights, falls at g, and lands (P5's tooth) instead of being bolted
  to the surface. Upward is uncapped (buoyancy). The honesty bounds are **asymmetric by
  necessity**: the submarine side is a hard band (a risen surface yanks the hull up into it), but
  the hover side is closed by the g-capped chase itself — when a surface sustains a
  faster-than-g descent no trajectory can both hug it and obey the cap, and gravity is the
  owner's constraint (a hard hover clamp shipped in the first cut and its own free-fall test
  measured the clamp biting at 149 m/s²). It settles exactly (epsilon snap), engages only with
  the storm blend (calm = exact passthrough), and is per-hull: the chase stiffness bends with the
  hull's existing `BoatHullDef` seakeeping response — a dory re-finds the water fast, a laden
  trader wallows. A permanent sabotage-armed EditMode test proves the g-cap is load-bearing.
- **Smoothing tightens with the storm** (default ×0.4 on the output damping at full blend) —
  velvet is for calm; the storm's snap is not laundered away, and continuity (the
  `WaveFieldAnimator` fix) is untouched.
- **Boundaries:** all visual-only — B3's seakeeping *forces* keep their own pure sim path and
  their own `GameConfig.Seakeeping` policy; nothing here feeds physics or the save (rule 5).

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

- Hitting **terrain** (rocks/wharves/land), **other boats** (NPC traffic, esp. busy Nine Mile Creek & the Shipping Lanes), or **fixed hazards** (sunkers, wrecks, ice floes-as-flavor).
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
| **Radio for a tow** | **Marine radio** (instrument, §5) calls a **tow operator** out of Nine Mile Creek. They steam to you and tow you to the nearest harbour/shipwright. | **Money** (₲), scaling with **distance from harbour** and **boat size** (towing a tanker costs a fortune). Set in `economy-and-business.md`. | The reliable paid safety net. Costs enough to sting, not enough to ruin. |
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

> Upgrades are **the texture of P2 progression between tiers** and the **answer to every P5 danger**. They're sold/installed by the **Shipwright** NPC (Nine Mile Creek; canon §5.3) and gated by money (and some by story/region). All upgrades are **data swaps on the boat's components** (§9), so the shipwright UI is a clean "slot → choose part" screen.

### 4.1 The shipwright (where upgrades happen)

- **Location:** Nine Mile Creek wharf (canon). A named NPC with routines (P3 — see `npcs-and-routines.md`); relationship can unlock better stock / discounts.
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

> **The same verb, at a different weight — the MUSSEL LONGLINE (capture only, not built).** The
> owner's mussel fishery (2026-08-18: *"large sections of buoys with individual ropes"*) harvests by
> **laying alongside a longline and hauling the backbone over the gunwale** — which is steps 2–4
> above, unchanged: hold station off the mark, work the rail, **hold on the lift and ease on the
> fall** off the shared wave field, hand-hauled first and winched later (P4). A backbone is longer
> and heavier than a pot, so it is a **tuning difference, not a second minigame**. The one real
> extension this doc will owe: placed gear here is a **LINE SEGMENT, not a point** — two endpoints
> and a length that must fit the lease and clear the depth band along its whole run, where every
> shipped placement check today takes a single position. Loop, phase and the rest of the contract:
> [`mussel-lease-and-longline.md`](mussel-lease-and-longline.md).

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
> **Teeth (P5):** greed kicks in — a great lobster soak means staying past low water. On the way out, distracted, the skipper clips a sunker at speed → **holing**, **bilge rising**. The **pump** buys time; they **run for Nine Mile Creek**, pumping, bilge gaining slowly. They *just* make the wharf. **Repair bill, a humbling, half a day lost — but home.** Next spring tide, they read the table first and stay clear. **That loop — capability earned, danger survived, lesson learned — is the game.**

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

### 9.1a How a hull LOOKS is data too (the skin binding)

A hull says what it looks like the same way it says what it weighs: **in its asset**. `BoatHullDef.Visual`
points at a **`BoatVisualDef`** (`Data/Boats/Visuals`) — the complete directional skin — and
**`BoatHullSkinner`** is the single, *runtime-callable* installer every consumer goes through. Before
this, the player's skin was a `const bool` + a fistful of `const string` art paths inside the editor-only
start builder, three call sites re-implemented the same rig by hand, and a hull could not be re-skinned
on a swap at all (see the swap gap below).

- **What a `BoatVisualDef` binds:** the hull **compass** (`Facings`, element 0 = North, then clockwise —
  the snap math is generalised to any count, so 16-way art drops in with no code change); the optional
  wave-coupled **rock grid** (`RockGrid` + `RockFrameCount`, element `heading·frames + frame`); the
  optional per-side baked **oar overlays** (`OarPort`/`OarStar` + `OarColumnCount`); and the hull's
  `SortingOrder`. The dory's is `visual.dory_iso` (8 facings · a 64-frame rock grid · two 80-cell oar
  sheets).
- **All-or-nothing, per block** (`HasFullCompass()` / `HasRockGrid()` / `HasOarSheets()`): a partial set
  never half-ships — one missing facing snaps the boat into a stale picture mid-turn — so an incomplete
  block falls back to the block below it, ending at the plain rotating `BoatHullDef.Sprite`. Hulls with no
  facings (the Punt, the `FishingSkiff`) are never stranded: they keep the one-picture-on-a-rotating-root
  rendering exactly as before.
- **The three consumers converge on the skinner:** `PersistentCoreBuilder.ApplyHullSkin` (the player's
  boat — renamed from `ApplyDirectionalFishingBoatVisual`, a misnomer once the dory rowed again: it
  applies no fishing-boat skin and no fishing-boat hull), `OwnedFleet.ApplyHull` (a purchase or a
  save-restore), and `AmbientFleetPresenter` / the rotation-test harness (which carry their own facings
  and adapt them via `BoatVisualDef.CreateRuntime`).
- **The swap gap, fixed.** `OwnedFleet` used to make the picture change by writing
  `_spriteRenderer.sprite = hull.Sprite` — onto the very renderer the skin had **disabled**. So buying the
  Punt swapped your feel, your hold and your camera while the picture stayed the iso dory. The swap now
  goes through `BoatHullSkinner.ApplyHull`, which handles **both** directions (install/refresh the compass,
  or tear it down and bring the base renderer back with the new hull's sprite).
- **Sheet paths are an import concern, not a gameplay one:** `BoatVisualLibraryBuilder`
  (*Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Build Boat Visual Defs*) is the only thing
  that knows where boat art lives on
  disk; it imports the sliced sheets into the def asset, which is committed. Re-run it only when a sheet is
  **re-sliced** (the sprite sub-asset ids change and the def's refs go stale).
- **Invariants the rig rests on** (breaking any of these breaks the boat): bow = `transform.up`; heading 0
  = North, clockwise; `DirectionalBoatSprite` lives on the **physics root** and stomps the visual child's
  world rotation to identity every `LateUpdate` — additive rotation only via `VisualTiltDegrees`, and
  anything that must follow the bow rides the **root** (this ate the boat spotlight once). The visual child
  keeps the historic name `FishingBoatVisual` because `BoatSpotlight` finds it **by name** to read its rock
  without referencing the Boats module (rule 4).
- **Extending it:** a new overlay (e.g. a motor layer) adds its own append-only block of fields to
  `BoatVisualDef` and installs from `BoatHullSkinner.Apply`, which returns a `Rig` handle carrying the
  visual child, the hull renderer and the `DirectionalBoatSprite` to layer onto. Bind sheets to the def —
  never add art paths to a builder.

### 9.1b The hull-presenter seam (ADR 0022 phase 1 — a seam, not a renderer)

ADR 0022 proposes rendering **large** hulls as real-time 3D meshes while small hulls stay sprites, the two
coexisting behind one interface. Phase 1 lands **only that interface**, with today's sprite path behind it
and **no behaviour change** — so the decision can still go either way without this having cost anything.

- **`IBoatHullPresenter`** is the description of a drawn hull that everything layering onto it needs:
  `DrawnHeadingDegrees()`, `FacingCellIndex`/`FacingCount`, `FacingsAreCounterClockwise`,
  `BakeElevationDegrees`, `HasRockGrid`/`RockFrame`, `VisualTiltDegrees`, `Visual`, `Anchors`. It was
  designed from what the six existing consumers (`OutboardMotorLayer`, `DoryOarLayer`, `BoatWakeEmitter`,
  `DeckContainerPresenter`, `DeckWalkController`, `BoatRotationTestRig`) actually read off
  `DirectionalBoatSprite` — not from what a mesh might want. A mesh hull reports `FacingCount` **0** (the
  documented "unquantised" signal the snap math already understands) and `FacingsAreCounterClockwise`
  false; the flag itself is **retained**, because sprite sheets still need it (boats true, characters
  false).
- **`SpriteHullPresenter`** is a POCO adapter over the shipped `DirectionalBoatSprite`. It **decides
  nothing** — every member forwards. If a getter ever grows a rule of its own, the seam has stopped being
  a seam, and `BoatHullPresenterSeamTests` goes red.
- **`BoatVisualDef.Variant`** (`BoatHullVariant.Sprite`/`Mesh`) is the discriminator. `Sprite` is both the
  field's **initialiser** and the enum's **zero value** — two independent guards, because the initialiser
  is what actually protects the already-committed assets (measured) and the zero value covers every path
  where no initialiser runs. Variants are append-only; the value persists as an `int`.
- **The anchor contract** (`IBoatHullAnchors.TryGetPoints`) answers "where is this point on the hull I am
  drawing, right now?", in screen-metre offsets from the cell pivot — the frame `MountedRockPoseMath`
  already returns and every overlay already consumes. Caller owns the list; the callee only appends (rule
  7). A sprite hull projects a boat-local rig point for the drawn **cell** (cell space, not compass space
  — they differ for CCW art, and confusing them is the mirrored-art class of bug); a mesh hull will push
  the same point through its live object transform.
- ⚠️ **The baked `Art/Boats/*Anchors.json` files are not read at runtime.** The rig baker writes them and
  nothing loads them — the runtime's only real anchor today is `BoatVisualDef.MotorMountLocalMeters`,
  hand-transcribed from the rig. The contract is shaped so a JSON-table-backed implementation is a legal
  drop-in, but shipping one would be *new* behaviour and is not phase 1.
- **Nothing consumes the seam yet, deliberately.** `BoatHullSkinner.Rig.Presenter` is the one production
  wiring point; the overlays keep their concrete `DirectionalBoatSprite` field until there is a second
  implementation to justify the churn (ADR 0022 phase 4). The invariants in §9.1a are unchanged — a
  presenter is **not** a licence to move a heading consumer off the physics root.

### 9.2 Physics tuning

- **Mass/inertia** from `Hull` (`mass ∝ length³` scaled to feel; clamp so the dory isn't *too* twitchy and the tanker isn't unmovably slow). Tune `dragSide/dragFwd` ratio (~6–12×) for the "tracks forward, skids reluctantly" feel.
- **Rudder authority curve** `f(speedThroughWater)`: zero at zero way, rises, saturates — tune so tight-quarters handling needs throttle bursts (real, satisfying).
- **Stability/broach thresholds**: tune `broachRisk` weights so capsize is **rare and earned** (§3.8) — a tender dory swamps if abused; a Stern Trawler only goes over in a true storm. Validate against the named sea-state tiers.
- **Determinism note:** physics is *not* required to be bit-deterministic across machines (it's real-time, single-player, player-driven), but **the environment it reads is** ([`time-tides-weather.md`](time-tides-weather.md) §9). Saves store **boat state** (position, velocity may be reset to rest on load, component fit, health/fuel/bilge, load) — *not* a physics-frame snapshot.

### 9.3 Mobile performance (canon mobile-first)

- **One active player boat** dominates; AI/crew/freight boats are **abstracted** when off-screen (they don't run full physics — they move on routes/timers; see `economy-and-business.md`/`npcs-and-routines.md`) and only spin up a lightweight body when visible.
- Environment sampled at **4 Hz** per active boat (interpolated) — cheap (§ env doc §8). Danger checks piggyback on the same cadence; no per-frame allocation (struct samples, pooled effects).
- Keep colliders simple (a few-vertex hull polygon, not pixel-perfect); use Box2D-v3's solver settings tuned for stability over precision; cap simultaneous on-screen boats (Nine Mile Creek/Shipping Lanes traffic) with LOD/abstraction.
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
- **…and the planks you land on are now FLOOR.** `InDockZone()` always let you off at an authored wharf, but
  the *walk* that followed read the seabed under the deck: St Peters' one dock stands over a **dredged −1.0 m
  slip** in a **±3.5 m** tide, so the on-foot sim measured **4.5 m of water at spring high** over the ratified
  disembark point and the fisher swam across her own pier (never blocked — the never-trap rule lifts the wall
  once you are already deep — just a slow-swim crawl with the submerge shader at the neck cap). A built deck
  now registers as a **standable structure** through the Core `IStandableSurface` / `StandableSurfaces` seam,
  so a person's standing height over it is the **deck**, not the bed
  (`architecture/tech-architecture.md` §4.1, `time-tides-weather.md` §3.5). **The seabed is untouched** —
  `BoatCrossing.DepthAt` and `OnLand()` still read the water the terrain authored, because the slip is dredged
  by design and the hull needs that depth. This is also the contract the **M2 walkable deck / washboards**
  (the deck/cleats/interact vision) need: the footprint is a *query*, so a deck that moves and rotates with
  the hull is a later implementation, not a change to the seam.
- **Getting out of a VEHICLE reads the same question, and currently gives a different answer.** Since
  drivable machines (ADR 0035) — and especially the amphibious Otter, who swims by design —
  `ControlSwitcher.LeaveDriving()` checks what is under the **door** before setting the fisher down there,
  through the on-foot depth seam the walk model uses (`TidalWalkability.DepthNow`, so a registered deck
  counts as floor exactly as above). The rule is the ratified three-band wading model
  (`time-tides-weather.md` §3.5): **Dry/Wade → step out · slow-swim band → step out into the existing
  escape state · boat-only water (> `SwimLimit`) → declined**, with the reason, and you stay behind the
  wheel. It is a **depth fact, not a lock** — the machine you are sitting in is the way out of it, so the
  refusal clears as soon as she is driven into the shallows. Two exceptions, both deliberate: a seat that
  has **died** under the player is never refused (no door to read and no machine to drive off in — that
  would be a softlock strictly worse than a wet landing), and a region with no height map or tide service
  reads Dry, so the gate self-disables rather than trapping the walker. Named once in Core as
  `TidalExposure.IsOnFootTraversable(DepthBand)`, shared with the walk model's soft wall.
  ⚠️ **OPEN — one question, two answers (owner's ruling).** The boat rule above is **stricter**: `OnLand()`
  requires `depth ≤ 0`, dry land only, after the playtest tightening. The vehicle rule admits wade and
  slow-swim. Both are defensible — an amphibian's driver refused ankle-deep at a beach landing would read
  as a bug, and stepping off a dory into the sea read as one — but they should be **one** rule, not two
  behaviours that happen to differ by which seat you were in.
- **Board from anywhere** within reach of the boat (`WithinBoardReach()` — a pure proximity radius), not only
  at a dock zone (owner playtest). So you can step aboard a boat nudged up to a beach, not just one at the
  wharf. (The damaged-dory repair gate still applies on top, P5.)
- **Swim up to a hull and climb aboard** (owner, 2026-09-02). The boarding gate above was never the thing
  stopping you doing this from the water — `WithinBoardReach()` is pure proximity with no swim check — the
  **boat-only soft wall** was: a hull lies in exactly the water a person may not enter, so the rule that
  keeps you out of open water also kept you off the gunwale you are supposed to be able to reach. Off St
  Peters' pier both boats float over the dredged **−4 m** pocket, so the water beside your own dory read
  **3.97 m at mean tide and 6.17 m at spring high** — refused, on your own doorstep. (At **spring low** it
  reads **1.77 m**, inside the slow-swim band, and was never refused: the defect was real for most of the
  tide, not all of it.) Hulls now publish their outline through the Core `IHullPresence` / `HullPresences`
  registry — the `StandableSurfaces` / `BoardingLadders` shape — and the wading model's wall **steps aside
  within `GameConfig.SwimBoardReachMetres` (6.0 m) of a hull's OUTLINE**, measured the way the boarding gate
  measures (never to her root: a 12.9 m cape read from her origin is "6 m away" from a swimmer holding her
  quarter). ⚠️ **This is the ONE relaxation of the ratified boats-only rule** (`time-tides-weather.md` §3.5,
  the 2026-07-05 model) and it is deliberately the narrowest sentence that satisfies his: out of reach of
  every hull the wall is untouched, and with no hull registered the walk model is bit-identical to what it
  was. **Open-water swimming remains unruled** — if the owner wants it, that is a separate ruling, not a
  wider reach.
  > ⚠️ **Still open, and older than this change.** The never-trap clause lifts the wall *entirely* once the
  > fisher is already past the wade band (the bullet above says so in passing: *"never blocked — the
  > never-trap rule lifts the wall once you are already deep"*). So a person who is in the water — by a
  > rising tide, or by going over the side (PR 3) — can already swim anywhere, hull or no hull. Nothing in
  > this change touches that, and closing it would mean making the escape valve directional, which costs the
  > absolute "you can never be trapped" promise on a bar surrounded by deep water. **An owner ruling, not a
  > lane decision.**
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
- **Leave-the-helm drift is LIVE on deck (Rod Fishing v2 Wave 4).** While the player walks the deck
  (`ControlMode.OnDeck` — hauling, working pots, **fishing off the deck**) the helm is unattended, and the
  sea keeps working the hull exactly as §3's "leave the helm, work the rail" beat promises: the Player
  lane's `ControlSwitcher` ticks `BoatController.TickUnmannedDrift()` each physics step, which runs the
  **same** force pass as the manned helm (hull drag against the current, wind shove, the seakeeping push +
  yaw — the slow weathervane a deck angler repositions against) with the controls at rest. The controller
  component itself stays **disabled** on deck — `enabled == "helm is manned"` remains the read the
  oar/motor/probe presentation layers key off — so the drift is an explicit unmanned tick, never a second
  force model. (Previously the deck mode suppressed the pass entirely: controller off + mooring stowed
  left the hull frozen in glass while you fished.) The deck-walk also publishes the live **`DeckStance`**
  frame through Core (hull position, drawn facing, the walkable rectangle) each tick; the Fishing lane's
  deck-angle fight term reads it (rod-fishing-v2 §4.2, owner-tunable via `GameConfig.RodFight.DeckAngleFactor`,
  0 = off/dock-parity).
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

### 9.6 Mooring — the painter, and the line made fast to a cleat

The rope makes fast to one of **three** tie targets. All are an `IMooringAnchor` (`MooringAnchor.cs`) — an
interface the tether reads a live `Position` from each tick:

| State | Tie target | Tidal? |
|---|---|---|
| `HeldByPlayer` | the player's hand (`TransformAnchor`) | no |
| `RootedToGround` | a fixed ground spot (`FixedAnchor`) | no |
| `MadeFastToCleat` | a shore cleat, with the other end on one of the hull's own cleats (`CleatAnchor`) | **yes** |

#### The cleat moor (M2-38, built 2026-08-06)

Stand by a cleat, **throw a line with the fishing-cast verb** to a cleat on the other side of the water
(boat→shore or shore→boat), make fast, then tighten or slacken at will. Cleats come from data on both sides:
a hull's from her rig sidecar's `CLEATS` (`BoatDeckDef.Cleats`, published by `BoatCleats`), the shore's from
the wharf builders' own fittings table (`ShoreCleat`). Both register into the Core `MooringCleats` registry;
neither side references the other.

**The tide law — the one thing to understand.** A line has a fixed LENGTH (its *scope*). Its two ends do
not stay the same distance apart: the shore cleat is bolted to the planks and stands still, while the boat's
cleat floats and rides the water. So the line must span a **three-dimensional** gap whose vertical component
the tide drives, and whatever the drop spends is no longer available to reach *across* the water:

```
horizontal reach = √(scope² − verticalDrop²)          MooringLineMath.HorizontalReach
verticalDrop     = |boatCleatElevation − shoreCleatElevation|
boatCleatElevation = waterLevel + (cleatHeightAboveKeel − draught)
```

That single function covers both hazards, because the drop is an absolute value and so grows in *either*
direction from level:

- **Falling tide, short line** (tied bar-taut at high water to a wharf): she drops away, the reach collapses,
  the line hauls her in against her own sheer — and past the working load the loop **slips**. The classic
  way to hang a boat off a wharf.
- **Rising tide, short line** (tied bar-taut at low water to something *low* — a float, a ring at the
  waterline): she now rises *past* her cleat, the drop opens again from the other side, and the line pins
  her down as the water lifts her.
- **Tied short at low water to a HIGH wharf**, though, gets *slacker* as the tide makes — which is what real
  seamanship says, and is the reward P1 is teaching.

**The cozy fail is a slipped loop, not a parted rope.** Past `WorkingLoadFactor` × scope, sustained for
`SlipGraceSeconds` (so one snatching wave never costs the boat), the loop surrenders and she goes quietly
adrift, undamaged. Coil it and try again. No damage model, no breaking strain in v1.

**Sizing it is about the DROP, not the tidal range.** St Peters is the worked example and is a taller pier
than it looks: deck measured at **+5.35 m** above datum, tide swinging **±2.2 m**, so the gap from a bollard
down to a small hull's cleat runs ~2.6 m at high water to **~7.0 m at low**. Hence the shipped defaults
(`GameConfig.MooringLine`): **9 m** of scope to start — she rides the whole ebb, swinging ~8.6 m at high and
~5.7 m at low, visibly drawn in but never hung — against a **2–16 m** range stepped by the metre. Snug her
to ~4 m at high water and it looks perfectly seamanlike; the ebb collects on it. `MooringLineMathTests` pins
that gradient against these numbers, so if a region's wharf height or tide amplitude changes the tuning is
re-checked rather than silently flattened.

**The constraint is a restraint, never a freeze** (rule 5). The drift force is applied every fixed step and
the rope restrains the *result*: the same firm tether + inextensible clamp the painter uses, handed the
tide-derived effective length instead of a fixed one, and centred so the *cleat* is the end being held
rather than the hull's origin. The one exception is while she is **hanging** (reach has gone to zero): the
positional clamp is skipped there, because a clamp with no reach would teleport a 13 m hull onto the
bollard. A hung boat is *hauled* by the (finite, visible) tether force until the loop lets go.

#### Still future work *(NOT built)*

- **Two lines (a bow line + a stern line).** A larger or more exposed berth wants the boat held at **two
  points** so she lies alongside instead of swinging on a single leash. That is two `BoatMooring`/anchor
  pairs, with the per-line physics applied at the bow and stern attach points rather than the hull centre —
  an extension of the single-line model, not a new mechanic. It also unlocks **springs/breast-lines** flavour
  and a real "make her fast fore-and-aft" docking beat (P1 seamanship).
- **A winch that pays out scope on the tide for you.** P4, and much later — this is precisely the
  earn-it-then-automate-it shape, and it only means anything *because* doing it by hand can lose you a boat.
- **Rope damage / breaking strain, and rafting (boat-to-boat).** Deliberately out of scope; the slip is the
  whole failure model for now.

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

**Owner ruling (2026-07-29): the helm framing law is "the WHOLE vessel visible, with margin."**
Verbatim substance: *"cameras should zoom out on larger vessels so the whole vessels are visible; they
seem fine up till lobster boat and then you're too zoomed in on larger vessels."* So: framing is correct
up through the **Lobster Boat (~12 m)**; every larger hull (dragger 25 m → trawler 38 m → freighter
60 m → tanker 110 m) is currently **over-zoomed** and must step the camera **OUT** until the full hull
fits on screen with margin. Constraints that still bind: every stop stays a **PPU-integer pixel-perfect
step** (never an arbitrary ortho size — the ratified discrete-zoom vision above); the framing derives
from the hull's **`LengthMeters` → the nearest step that fits the whole vessel**, as per-hull **data,
never a per-hull hard-code in C#** (rule 6); the on-foot/deck/haul steps of this section are unchanged —
this rules the **helm** framing only. The big-fleet values are tunable data and may land ahead of M2:
the dev key-cycle already sails those hulls, which is exactly how the defect was seen. *(This also
serves canon §5.2's scale fantasy from the other side: constant PPU makes a tanker dwarf a dory —
the camera has to step back far enough to let it.)*

**BUILT 2026-07-30 — and the ruling's two constraints turned out to CONFLICT.** Two findings:

1. **The defect was a silent CAP, not a missing derivation.** The step search ran integer UPSCALE only
   (zoom ×1..×8) and `PixelPerfectCamera`'s own zoom clamps at ≥ 1, so the widest framing expressible
   at all was `screenH / ppu` = **33.75 m** at 1080p. The defs *already* ask for 40 / 60 / 90 / 160 m
   for dragger → tanker; every one was quietly served 33.75 and rendered at the **same** framing as
   the others. The per-hull data was right all along and was being thrown away.
2. ⚠️ **"Whole vessel visible" + "an integer pixel-perfect step" are unsatisfiable above ~37.5 m of
   hull** at the locked PPU 32 (VS-23): a 110 m tanker is 3,520 asset pixels on a 1,920 px screen.
   **Resolved by the coordinator (2026-07-30):** the ladder continues OUTWARD by integer **downscale**
   (2:1, 3:1 …) — still a clean pixel ratio, 2×2 asset pixels to one screen pixel, no blur or shimmer,
   unlike an arbitrary ortho size. Big hulls therefore lose pixel **detail** rather than being cropped.
   A downscale step bypasses `PixelPerfectCamera` (which cannot express it) and drives the ortho.

The derivation is a **floor, not a replacement**: `max(authored framing, hull footprint × margin)`, so
everything up to the lobster boat keeps the intimate framing the owner is happy with and only big
vessels move. The footprint is `length × max(sin(isoElevation), 1/aspect)` — **not** raw length: bow-on
a hull is foreshortened into the short axis, and measuring against length would zoom out ~55 % further
than any heading needs. Margin and elevation are serialized dials; length rides on `ActiveBoatChanged`.

⚠️ **This interacts with the camera bounds clamp (scene-sizing §6 item 4).** A tanker framing is ~135 m
tall / 240 m wide — wider than plenty of regions — so the per-axis *centre* path in `CameraBounds`
stops being an edge case and becomes the normal state for big vessels in small waters.
`HelmFramingTests` exercises framing × bounds together for exactly that reason.

**Owner ruling (2026-08-19): the wheel is the player's eye.** Verbatim: *"Mouse wheel modifies player
zoom — closer to look at interiors, out when outside."* Built as **a second hand on the SAME ladder,
not a second zoom system** — the failure mode a free player zoom would have produced is a wheel
fighting the ruled framings above it for the same orthographic size.

- **On foot she owns a RUNG.** An absolute stop on the ladder, kept for the whole voyage.
  **Disembarking restores her last tier for free** — that tier *is* the on-foot framing, so stepping
  ashore commits `OnFoot` and lands on it with nothing saved or reinstated.
- **Four discrete stops, all crisp.** The range is `11.25 m → 5.625 m` of world height: the ×3, ×4, ×5
  and ×6 PPU-32 steps at 1080p. One stop wider than standing on foot at the far end; at the near end
  the tightest framing the game already ships (the live-haul step) — so the interior close-up is a
  framing the game has already been played at, not a new number. Nothing between two stops exists,
  because a fractional camera scale shimmers on pixel art.
- **Owner-tunable in metres, never in step indices** (rule 6): `GameConfig.PlayerZoom` carries the two
  walking clamps, the wheel enable, the scroll-per-tier, the step ease and the two aboard-band stop
  allowances. Metres because ladder steps count *upward* as
  the view gets *closer*, which reads backwards to anybody tuning a camera; every other camera dial in
  the project is world height in metres and so is this one. A hand-typed height quantises to the nearest
  crisp stop, so the worst it can do is pick a neighbouring tier — never a blurry framing.
- **A modal refuses the wheel.** While `InteractionGate` is raised (dialogue, the wardrobe, the pause
  menu) a notch does nothing and banks nothing, so one wheel can never scroll a list and zoom the world
  at the same time — the case that would otherwise arrive the day a kit UI grows a scrollable page.
  A blocked wheel also banks no scroll, so nothing fires the instant the modal closes.
- **Bindings.** Mouse wheel (unread anywhere else in the project; the only claim on `Mouse.scroll` is
  the stock UI map's `ScrollWheel`, which serves the EventSystem, not the world) and, on the pad,
  **LB / RB** — the one pair unclaimed in both code and `InputSystem_Actions`, and the right *shape*
  for a discrete tier where a stick axis would need a deadzone and a repeat rate to say the same thing.
  (The exhausted A–Z dev-key ledger is about keyboard letters and does not bear on either.)
  New Input System only. The reader is `App/CameraZoomInput`; the rules are `CameraZoomPolicy`.
- **Presentation, never simulation** (rule 5). The tier drives nothing, publishes nothing and is not
  saved — the same standing as tide and weather, which are recomputed rather than stored.

**The kit UIs are out of reach of this, and that was checked rather than assumed.** The obvious worry
is a kit that sizes itself against the *camera* — zoom in and the book no longer fits. Neither does:
the dialogue bubble is a screen-space canvas tracked through the camera each `LateUpdate`, and the
notebook presenter (PR #578) fits against `Screen.width`/`Screen.height` × `RoomFraction`, i.e. real
window pixels. Camera zoom moves neither, so an open page needs no re-fit at any tier and there is no
tier a page can be opened at that it cannot fit. (The kit README's "closest tier 4×" is the *notional*
pixel screen its read budget is priced in, not something the presenter measures.) The modal gate above
is therefore belt-and-braces for these two — it earns its keep the day a kit UI grows a scrollable
list and wants the wheel for itself.

**Owner ruling (2026-08-22): the wheel works aboard and on deck too — and it was dead on foot.**
Two things, from one playtest.

**① The on-foot dead path was a real bug, and reading found it in the liveness gate.** `WheelIsLive`
required a *committed* framing. The camera only commits from `TickZoom`; `TickZoom` refuses to run
until a `ControlModeChanged` has arrived; and **nothing publishes one at boot**. `ControlSwitcher`
speaks on a *transition* (board, disembark, take a wheel) and on the region-arrival re-assert, and
`ArrivalOpening` — which does publish — is skipped for any save that has already arrived
(`ShouldPlay = !hasRestAnchor && !alreadyArrived`). So a returning player loaded their game, walked
down the wharf, and turned a wheel that was switched off with no error anywhere. Boarding once and
stepping back ashore "fixed" it, which is exactly how it survived a build. The repair answers what
the gate was really asking: **un-committed does not mean "no framing on screen", it means the
builder-authored one is — and the builders author the walker's.** She is a walker until the game says
otherwise, so an un-committed camera is a walking camera and its wheel is hers.

> Three other suspects were ruled out by reading rather than by guessing, and each is now pinned by a
> POCO test so it can never become the answer to the same report twice: the carry accumulator vs
> Windows' ±120 per detent (one detent is one rung, and banks no remainder); the clamp collapsing
> because the on-foot height sits *on* a bound (the walker's home rung is strictly inside the shipped
> band, and a deliberately pinned range refuses the nudge rather than faking it); and `WheelEnabled`
> read off the wrong config instance — the shape that actually bites being a `GameConfig` asset
> serialized *before* `PlayerZoom` existed, which deserializes as `default(T)`: wheel off, range 0 m to
> 0 m, all silent. `PlayerZoomSettings.Sanitized()` now heals a wholly-unwritten block, and only a
> wholly-unwritten one — an owner who turns the wheel off keeps the rest of their tuning, so asking
> whether the *whole* struct is blank is what separates "off on purpose" from "never authored".

**② Aboard and on deck she owns an OFFSET, not a rung.** This answers open question 2 below with a
band rather than a second range. The offset is in whole rungs from whatever the context ruled
(`BoatHullDef.CameraWorldHeightMeters` at the helm, the deck step on deck), bounded by
`GameConfig.PlayerZoom.AboardStopsCloser` / `AboardStopsWider`, and **released on every tier change** —
a committed framing change, or a hull whose framing actually differs. That release is the whole reason
a band can exist without taking the framing away from its authority: §9.8's "whole vessel visible"
derivation and the deck step are still what she is *handed* each time she arrives, and the wheel is a
look around from there. Store a rung instead and the first upgrade would frame the new boat at the old
boat's zoom — §9.8's defect, wearing a different hat.

- **A live haul and a road vehicle stay ruled outright.** The haul tighten exists for the seconds a
  pot is coming up and releases itself; a wheel fighting it would be fighting something already
  leaving. A truck at 11 m/s needs every metre of the view her def asks for.
- **Counts of stops, not metres** — the one camera dial in the project that is not a world height, and
  it cannot be one: the thing it is measured *from* is different for every hull the player will ever
  own. A dory and a tanker share an allowance of "two stops"; they share no pair of metre clamps.
- **The band walks the ladder by INDEX, not by step number.** Steps 0 and -1 are not steps, so the
  sequence runs … -3, -2, **1**, 2 … A band centred on a big hull sits at or past that 1:1 pivot, and
  plain `step + 1` would walk into the hole. `CameraZoomPolicy.StepClosestBy` walks indices instead,
  so "one stop wider" always means the next real stop. Every reachable framing, at every offset, on
  every hull, is still an integer pixel-perfect step — swept by
  `NoFramingTheWheelCanReach_IsEverANonIntegerPPU`.
- **`CameraZoomInput` did not change**, and did not need to. Every rule about which tiers exist, when
  the wheel is live, and whether a notch moves a rung or an offset lives in the policy and the camera —
  the split earning its keep.

> ⚠️ **One open question for the owner, deliberately not decided here.**
> 1. **Should an interior CLAMP the far end** — no zooming out through a roof? The camera cannot answer
>    that today: `BuildingInterior.IsInside` is World-side and the camera (App) reaches it only through
>    a Core signal that does not exist yet. Ruling it "yes" is a small Core contract (an occupancy
>    signal) plus a second clamp pair, not a tweak to what shipped here.
>
> *(Question 2 — "should the wheel work at the helm at all" — was answered on 2026-08-22: yes, as a
> band. See above.)*

### 9.9 The ambient fisher fleet (decor tier — canon M2-33, P3 "Living Working Coast")

Owner ask (2026-07-08): *"a few npc fishers… 3-5 boats sailing that can place their own buoys and haul
them. make them avoid collisions, or driving through land."* Built as **decor-level simulation**, NOT the
player's systems: NPC buoys never touch `PlacedTrapService`, the save, or the player's catch/economy —
the fleet is the coast *looking* worked.

- **Deterministic from `(worldSeed, gameTime)` (rule 5), recomputed never saved.** Buoy spots re-plan per
  game day from `(worldSeed, fleetId, boatIndex, dayIndex)` (`AmbientFleetPlan`, FNV-1a + avalanche — the
  `StableHash` idiom); the place → soak → haul beat is **closed-form off the clock**
  (`AmbientFleetSchedule`: the day divides into slots, a boat round-robins her K spots, visits to a spot
  alternate place/haul so a buoy's presence is just visit parity). Join a session at any moment and the
  fleet is exactly where the clock says. Only the frame-to-frame steering track is live (it must dodge the
  player) — the same "reads a deterministic sample, isn't bit-deterministic itself" contract as §2.
- **Land/shoal safety is the height field, twice.** Plan-time: spots and every travel leg (including the
  cycle's closing leg) are accepted only where depth at the tide's **all-time floor** (spring low,
  `mean − amplitude`) keeps the Def's margin — so no planned route can EVER be stranded by a falling tide.
  Live: a 3-probe bow look-ahead on the slow tick (`AmbientFleetSteering.DepthAvoid`, current water level)
  swings a player-displaced boat toward the deeper bow and eases her down. **No NavMesh** — the painted
  seabed (`ITidalTerrain` via Core) is the map.
- **Collision avoidance is local steering**: linear-falloff repulsion from other NPC boats, the player's
  boat (a bigger berth), and the player's placed buoys (positions off the Core `TrapPlaced`/`TrapRemoved`
  signals — Fishing is never referenced), with a starboard bias so a head-on meet curls both boats the
  same way instead of deadlocking. Kinematic transforms + a rate-limited bow swing; no rigidbodies.
- **Seamanship, not spin (owner feedback on #189).** The whole per-boat drive is one pure integrator
  (`AmbientFleetSteering.Step` — the presenter and the EditMode convergence tests run the same code):
  she **turns with way** (bow swing scales with the way she carries, never below a bare-steerage floor,
  and she slows through a hard turn), **arrives and lies-to** (`HoldStation`: settle inside the hold
  radius when the *social* push is faint; hysteresis — a higher wake gate — so a neighbour's residual
  push or the player drifting past never rouses a working boat), and the **starboard bias is gated to
  near-head-on** (glancing repulsion composes straight — curling it sideways is what made "keep clear"
  orbit). The seek *yields* to a saturating push and an argued demand checks her way (`resolve01`), so a
  blocked mark reads as standing off, not ringing the blockage. The shoal correction is stored
  **bow-relative** and re-expressed as the bow swings between slow ticks. No-orbit invariant (guarded by
  a content test): `MaxSpeed < ArriveSlowRadius × TurnRate × SteerageTurnFraction` — the turning circle
  always shrinks inside the distance left, so no stable orbit exists at any radius.
- **Content is data (rule 2/6):** one `AmbientFleetDef` per region (`Data/Boats`,
  `fleet.st_peters_ambient`) carries every tunable — boat count (3-5), hull art, speed band, grounds
  rect, depth margin, work rhythm (slots/day + work window), avoidance radii, buoy palette. Fleets are
  indexed by the Resources `AmbientFleetLibrary` (the `FishSpeciesLibrary` pattern).
- **The fleet wears the owner's boat (owner ask 2026-07-12).** `AmbientFleetDef.HullFacings` takes the
  8-way fishing-boat compass (CW from North — the same art the player sails), **all-or-nothing** like
  the player-boat builder guard: the full set gives each fisher the player's exact snap-directional rig
  (`DirectionalBoatSprite` on the root, the child counter-rotated to screen-identity, the wave roll
  routed through `VisualTiltDegrees`); empty or partial falls back to exactly the pre-compass rendering
  (single `HullSprite`/greybox wedge on a rotating root) — never a partial compass. Each hull is tinted
  ONCE at build time toward that fisher's `BuoyPalette` colour (`HullTintStrength`, default 0.35 —
  multiply-tint shifts the whole sprite, so subtle): hull matches gear, whose-boat-is-whose at a glance.
- **Self-installing host (ADR 0011, the `TrapBuoyPresenter` convention):** `AmbientFleetPresenter`
  bootstraps at `AfterSceneLoad`, gates on the Def's region scene + a registered tidal terrain, owns its
  own root, pools everything at activation (zero per-frame alloc), and never touches builders or authored
  content. Hulls ride the shared wave field via `BoatWaveMotion`; buoys reuse `BuoyWaveVisual`
  (bob + waterline + vanish-under-a-crest) with a **per-fisher float colour** — buoy colour = whose gear
  it is, so NPC pots never read as the player's yellow.

### 9.10 The deck catch container (fish tray) — the diegetic hold read *(first slice of the physical-inventory vision)*

The catch is **visible on the deck**: a fish tray sits at a fixed spot on the boat and its sprite steps
through **fill states** as the hold fills — band a keeper and the tray gains; sell at the wharf and it
empties. No HUD, no counter: the tray IS the "how full am I?" readout (owner canon: *fill-state sprites
are important — you read roughly how full a tray/tote is by looking at it*).

- **A pure read of `ShipHold` — not a container system.** The catch still lands via the unchanged
  `IHold.TryAdd` + `FishCaught` path and leaves via `IHold.Clear` + `CatchSold`. The tray
  (`DeckContainerPresenter`, Boats lane, runtime-spawned by `ShipHold` — no builder wiring) subscribes to
  those Core edges (+ `GameLoaded`, `BoatPurchased`) and re-reads `UsedUnits / CapacityUnits`; sprite
  swaps are event-time only. The full deck-grid / container-nesting / fullscreen-view vision is **M2/M3**.
- **The container ladder is data.** `DeckContainerDef` (`Data/Boats/Containers`, id
  `container.fish_tray`) carries the ordered `FillSprites` (empty first → brim last; the owner's painted
  states drop in with zero code — an empty array falls back to 4 code-built greybox states). Which
  container a hull carries + where it sits are hull data: `BoatHullDef.DeckContainer` /
  `DeckContainerOffset`. Small boats carry the **tray**; the big **blue totes** are new Defs on M2 hulls.
- **Fill mapping (tested):** state 0 is pinned to an EMPTY hold, the last state to a FULL one, partials
  spread linearly between — so one banded keeper always shows, and only a truly full hold heaps the tray.
- **It rides the drawn facing.** The anchor is authored in the **deck frame** (x abeam → starboard,
  y along the keel → bow) and rotated by `DirectionalBoatSprite.DrawnHeadingDegrees()` each LateUpdate —
  the same frame as the §9.5 deck-walk clamp (an EditMode parity test keeps the two maths in lockstep) —
  so the tray snaps with the picture and stays on the same spot of the *pictured* deck; the sprite itself
  stays screen-upright and never anchors to the counter-rotated visual child.

### 9.11 Anchoring — drop the hook where the rode reaches the bottom

*"An anchor option on the boat: it drops only if there is enough depth for the anchor to reach the
bottom. Larger vessels carry longer rodes. An anchored boat holds against wind/tide drift."* (owner
ask, 2026-08-06.) Built as **one rule the player can read off the water**, not a place-flagged
"anchorage zone": she anchors where her line reaches, and nowhere else.

- **The gate is DEPTH, and the depth is the one the game already has.** `depth = waterLevel −
  seabedElevation` via `BoatCrossing.DepthAt` → `TidalExposure.WaterDepth` — the *same* single number
  the water render, the walkability sim, the crossing gate and the sounder read (ADR 0014: paint =
  sail). No second copy of the arithmetic, nothing cached: it is recomputed every tick from
  `(worldSeed, gameTime)` and never saved (rule 5). She anchors iff she is genuinely **afloat**
  (`depth > 0` *and* `depth ≥ draught` — the existing float rule) **and** `depth ≤ rode`. Too deep →
  the hook *"finds no bottom"*: a refusal with no state change. On the flats you do not anchor, you
  are **aground**, which the existing grounding sim already owns. Where a region paints **no seabed
  at all**, the depth is infinite and the tackle refuses — the mirror of the crossing gate's "a
  missing height map never falsely *blocks* a boat" (here: never falsely *claims to hold* one).
- **The ground tackle is DATA** (rule 2) — three fields on `BoatHullDef`, and **every shipped hull
  states all three** (the owner's ruling, 2026-08-23: *an anchor on every hull*; `AnchorContentValidationTests`
  is the guard):
  - `HasAnchor` — does she carry a hook at all. **True** by default, because the ruling is that she
    does; `false` is the deliberate exception, and it is the one gate that is not about the sea (no
    tackle → no dash switch, a dead anchor key, and `BoatAnchor` refuses the drop). ⚠️ A hull asset
    whose YAML *omits* the key deserializes it as `false` — Unity never runs a C# field initializer on
    a loaded asset — so the key is written explicitly on every hull and the content test checks the
    **file**, not just the loaded object.
  - `AnchorMassKg` — what the hook weighs (dory 4 kg → punt 6 → console skiff 8 → lobster boat 20 →
    cape islander 22 → dragger 110 → trawler 320 → packet 900 → tanker 3200). Not decoration: a
    dragging anchor checks the boat by friction along the seabed and friction goes with the weight
    bearing on it, so this **scales the shared drag brake** — twice the iron, twice the check
    (`AnchorMath.DragBrakeStrengthFor`). Because the brake is a *force* and what the hull feels is
    force ÷ her own mass, a big boat still fetches away faster than a dory: the ladder comes out
    right with no second curve to tune. `0` takes `GameConfig.Anchor.ReferenceAnchorMassKg`, which is
    also the mass the shared brake strength is quoted at — so an un-authored hull drags exactly as
    she did before hulls had anchor weights.
  - `RodeMeters` — how much anchor line she carries, and therefore the deepest water she can anchor
    in. It grows up the ladder (dory 6 m → punt 8 → console skiff 12 → lobster boat 30 → dragger 60 →
    trawler 90 → packet 130 → tanker 180), so **deeper anchorages are a thing you buy** (P2). `0`
    takes the shared dinghy-class `GameConfig.Anchor.DefaultRodeMeters`.

  Each of the three is resolved in **exactly one place** (`AnchorMath.CarriesAnchor` / `AnchorMassFor`
  / `RodeFor`), so the switch that draws, the key that presses and the sim that holds always read the
  same tackle.
- **Holding is a swing circle, not a freeze.** She lies within `√(rode² − depth²)` of the drop point —
  the plain geometry of a taut rode, where the vertical leg takes the depth and whatever is left is
  horizontal. **Spare rode is swing**: a short scope in deep water pins her almost over her anchor, the
  same rode in shallow water lets her range nearly its full length. Inside the circle she is
  completely free — wind, tide and sea work her exactly as the unanchored sim says; at the edge the
  rode goes taut and checks her firmly.
- **ONE restraint mechanism, two consumers.** The rode is checked with the *mooring line's* own maths
  (`BoatMooring.TetherForce` + the inextensible `BoatMooring.ConstrainToRope`, §9.5), with the swing
  circle standing in for the rope's length. A boat brought up on her anchor is held exactly the way a
  boat made fast to a cleat is, because it is the same code and the same tuning. No second
  "near-rigid tether" implementation exists.
- **The tide keeps moving — this is where the teeth are (P1/P5).** On the **ebb** the depth shrinks,
  the swing circle *widens*, and if her draught meets the bottom the existing grounding sim takes her:
  the anchor never prevented that; a badly-chosen anchorage did. On the **flood** the depth grows and
  the circle *shrinks* — to nothing at `depth == rode`, the last moment she holds — and the instant the
  water is past her rode the hook **loses the bottom and she DRAGS**: no longer held, creeping off
  downwind/downtide at about `GameConfig.Anchor.DragCreepMetersPerSec` while the tackle skips along
  the seabed. Come the ebb she **brings up again where she has fetched to**, not at the berth she
  lost: dragging costs you your spot, not your anchor.
- **Owner-tunable, no magic numbers** (rule 6): the whole policy is `GameConfig.Anchor` — the
  reference hook, the dinghy-class rode, the swing floor, the firm-limit trio, and the drag creep +
  brake. ⚠️ The **drag
  rate** is flagged `_confirm` — it is the number that decides how nasty losing your bottom feels.
- **Two controls, one verb** (owner ask, 2026-08-23) — and never two states, because both call the
  same `BoatAnchor.Toggle()`:
  - **At a helm: a switch on the dash** (`UI/HelmDashController`), which is the diegetic answer ADR
    0039 asks for. On the **pilothouse** hulls it is the rigs' *already-authored* `ANCH` breaker
    (`ColA[3]` of the bank, drawn dead since that dash shipped and now lit by the hook itself). On the
    **skiffs** it is a third switch bat in the panel, midway between DECK and SPOT — derived from the
    rig's own numbers rather than measured, and flagged to `art-director` to mirror back into
    `consoleRig.js`/`sportRig.js`. It is drawn only on a hull that carries a hook: a switch the boat
    cannot answer is the diegetic version of a readout you have not earned. The UI reaches the tackle
    through Core (`IHelmControl.HasAnchor` / `AnchorState` / `ToggleAnchor`), never by naming a Boats
    type (rule 4).
  - **On a hull with no dash — the rowed dory, the motorised dory, the punt — one key: `Q`.** A
    **reused verb**, not a new letter: the A–Z ledger is spent (ADR 0039 §6), and the game already has
    a ground-tackle verb. `Q` ashore works the mooring you are standing by (`ControlSwitcher.ToggleMooring`);
    `Q` aboard lets go or weighs the hook. The two readings can never both be live —
    `CanToggleMooring()` requires `OnFoot`, the anchor key requires `Aboard`/`OnDeck` — so one letter
    carries both with no modifier, no hold and no arbiter, and aboard it claims a press that did
    nothing at all before.
  - ⚠️ **This retires `R`, and unpicks a live double-booking.** The old dev key claimed `R` as
    "audited free"; it was not. `MooringController._workKey` is `Key.R` — tighten, SHIFT-slacken, hold
    to **cast off** — and that controller is live on a boat's deck, so a press of `R` on deck with a
    line on both worked the rope and toggled the hook. `R` goes back to the mooring lane alone.
  - The key lives only from the boat (helm or deck, never ashore), only on the hull you are on, only
    on a hull that carries a hook, and stands down under a dialogue or a text field. Ownership is
    `GameServices.Helm.IsPlayersBoat` — the **wider** fact, not `IsPlayerHelm`: a rowed dory has no
    helm at all and her anchor still answers to the player.
- **Visual v1 is minimal**: a greybox `LineRenderer` rode from the hull to the hook, dull galvanised
  while holding and red while dragging, on the `SortingBands.AboveDecor` rope tier the mooring line and
  the trap-haul line already share (ADR 0032). No bespoke animation — a windlass clip is routed to the
  art-director.
- **Nothing is saved.** The drop point is live runtime state, like the mooring's tie point; reload and
  the hook is catted. *Persisting an anchored boat across a save is a follow-up, not this slice.*
- Code: `Code/Boats/AnchorMath.cs` (the pure rules — gate, swing, drag brake, and the three tackle
  resolvers), `Code/Boats/BoatAnchor.cs` (state + the per-tick restraint, runtime-spawned by
  `BoatController` so no builder re-run is needed), `Code/Boats/AnchorInput.cs` (the key),
  `Code/Boats/HelmControlRelay.cs` (the Boats side of the Core tackle seam),
  `Code/UI/HelmDashController.cs` + `Code/UI/Draw/HelmDashGeometry.cs` (the dash switch),
  `Code/Core/Boats/AnchorSettings.cs` (the owner's policy, and the shared `AnchorState`). Tests:
  `AnchorMathTests` and `AnchorContentValidationTests` (EditMode — the decision half, the tackle
  ladder, and the `GameConfig.asset` / hull-asset YAML-key guards), `HelmAnchorSwitchTests` (EditMode
  — where the switch sits in both helm families, what a press on it does, and its repaint key),
  `AnchorPlayTests` (PlayMode — the gate on a live tide, the hold under a stiff wind against an
  un-anchored control, the rising-tide drag, and the key's gate) and `AnchorEveryHullPlayTests`
  (PlayMode — the rowed dory with **no helm granted at all**, the switch on both helm families, and
  the switch and the key landing on the same hook).

---

## 10. Open questions

1. **Direct boat control vs point-to-move (mobile).** Do we offer **full manual** throttle+helm (best for P1 skill, harder on touch) and an optional **assisted/auto-pilot-to-waypoint** for transits/accessibility — and where exactly does autopilot disengage near danger? Likely both, with autopilot for known-safe transits and manual demanded in tide/weather pinch points. Needs UX prototyping.
2. **Sailing depth.** How much sail nuance for the dory/punt — simple drive-component only, or a richer points-of-sail/no-go-zone model? Keep it light (it's flavor + fuel-saver), but confirm it's *fun* not fiddly on touch.
3. **Capsize consequence ceiling.** Exact partial-load-loss % per event severity, repair cost curves, and whether a worst-case can ever *cost the boat* (recommend **never** — only money/time/partial load; keep the boat). Lock with economy.
4. **Tow economy balance.** Tow/rescue pricing vs the player's wallet at each tier so it *stings but never spirals* (canon). Coordinate with `economy-and-business.md`; tutorial regions should have a cheap/free safety net.
5. **Fuel friction.** Is fuel a meaningful resource throughout, or mostly an early/mid concern (with later boats so capable it fades)? Decide whether running dry stays a real threat at high tiers or becomes a non-issue.
6. **NPC traffic & right-of-way fidelity.** How smart must NPC boats be to keep collisions "your fault" and fair, especially in busy Nine Mile Creek and the Shipping Lanes, without heavy AI cost on mobile?
7. **Branch convergence requirement.** Must a player who took the **Lobster** branch buy back through an offshore boat to reach the **commerce tier**, or can a successful lobsterman jump straight to a Coastal Packet with enough capital? (Recommend: capital-gated jump allowed — money is the great converger — but confirm it doesn't skip needed seamanship learning.)
8. **Multi-boat / fleet control UX (Tier 6–7).** When you command a fleet (canon end-game), how much is hand-steered vs dispatched-on-routes? This is where P4 automation peaks — needs its own design pass (likely in `economy-and-business.md` for the logistics layer, with this doc owning the per-hull physics).
9. **Depth representation handoff.** Confirm with [`time-tides-weather.md`](time-tides-weather.md) §10 OQ1 and `world-and-regions.md` exactly how `seabedElevation`/`waterDepth` is authored per region (heightfield texture vs tile metadata) so grounding reads cleanly against the same data the water visuals use.
```