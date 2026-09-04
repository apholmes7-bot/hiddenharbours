# NPC boat pilotage — coming alongside, keeping clear, and the busy wharf

> **Status:** design capture, 2026-08-26. **S1 (the come-alongside) is BUILT** — see §8; everything
> else here is still capture and nothing else is built. Subordinate to
> [`../vision-and-pillars.md`](../vision-and-pillars.md) (canon), then
> [`boats-and-navigation.md`](boats-and-navigation.md) §2/§3.4/§9.6/§9.9 and
> [`nine-mile-creek-mainland.md`](nine-mile-creek-mainland.md) §5–6. Written in the seam style of
> [`deck-boarding-cleats-and-interact-capture.md`](deck-boarding-cleats-and-interact-capture.md):
> the ask, the mechanism, what is data, what is open.
>
> **Pillars:** P1 (a docking plan is a tide plan) · P3 (the wharf's day is legible) ·
> P5 (the sea is dangerous, and the danger stays *yours*).

---

## 0. The ask, and the anti-goal

The owner, 2026-08-26 — five things, and the doc must serve all five:

1. NPC boats **slow down and dock realistically** — a come-alongside, never a teleport, never running
   through ground.
2. **Zero collisions with non-water sprites**: docks, quay walls, shore, moored boats.
3. **Nine Mile Creek is a busy wharf** — many boats docking and undocking through the day.
4. Boats **avoid each other on the water**.
5. They **obey maritime law**, simplified to what a player can read at a glance.

**The anti-goal is in the repo and it is precise.** `ArrivalOpening.TieUp()` writes
`_boatRoot.position = _berth` and `_boatRoot.rotation = -_berthHeadingDegrees`, then freezes the body
kinematic; `HandOver()` writes `_player.position = _stepAshore`. **Two teleports** — the hull snaps to
her berth, and the passenger snaps to the planks. Everything *before* that snap is already right (see
§1); the snap is there because the approach ends *near* the berth on *some* heading and the berth wants
a specific pose. **This design's whole first slice is deleting both snaps by producing the pose
honestly.**

---

## 1. What already exists — and so what this design does not invent

| Piece | Where | What it already gives pilotage |
|---|---|---|
| `ArrivalPilot` | `App` | ⭐ **the approach controller, already correct.** Pure `(pose, velocity, mark, metresToRun) → (throttle, steer)`; closes on **speed over the ground**, unsigned; target speed `min(cruise, √(2·a·d))`; **goes astern** rather than ramping off. Three failure modes already paid for and written down (bow-relative way → orbit; speed-made-good → 150 m departure; a dead `Max`). |
| `BoatController` | `Boats` | the real hull: `SetControl(throttle, steer)`, rudder authority scaling with way, grounding, astern at `DefaultAsternFactor` 0.4. |
| `AmbientFleetSteering` | `Boats` | pure kinematic seamanship: repulsion, gated starboard bias, **turn-with-way**, `HoldStation` with hysteresis, `DepthAvoid` (3-probe, tide-aware), and the no-orbit invariant. |
| `AmbientFleetPlan` / `AmbientFleetSchedule` | `Boats` | ⭐ **the determinism primitives.** Routes gated at the tide's *all-time floor* (spring low), so no plan can ever be stranded; the day's beats **closed-form off the clock** (visit parity, no saved state machine). |
| `RoutinePlan.SampleAt(hour)` | `World/Routines` | the villager precedent: **position IS `f(worldSeed, hourOfDay)`** — nothing ticks, a region loading at 14:20 puts everybody mid-stride. This is the timetable's model. |
| `MooringCleats` · `MooringLineMath` · `BoatCleats` | `Core/Mooring`, `Boats` | ⭐ M2-38 shipped: a made-fast line **actually holds a boat** against tide/wind drift, with the falling-tide slip as the cozy failure. |
| `NavChannel` · `NavMarkPlan` | `World` | the authored fairway and its IALA-B marks. NMC's is seaward-first: `(204,50) → (182,62) → (170,58) → (152,67) → (132,71) → (116,67) → (100,70)`, half-width 12 m over a −3.9 m thalweg. |
| `ITidalTerrain` · `TidalExposure` | `Core/Environment` | the one depth number the render, the walk sim, the crossing gate and the sounder all read. **The map. There is no NavMesh and does not need to be.** |
| `BoatOwnerDef` | `Boats` | the register: `Moorage` (QuayWall · Float) + `BerthIndex`, unique per owner, content-tested. **Berth allocation is already mostly authored.** |
| `BoatAnchor` · `AnchorMath` | `Boats` | rode-gated anchoring — the "she can't get in" fallback, already built. |
| `IRadarContacts` · `RadarContact` | `Core/Nav` | a traffic-contact seam that already exists. Pilotage should publish onto it, not grow a second list. |

**The honest summary: about 70 % of this design already ships.** What is missing is a *phase machine*
that sequences those pieces, one manoeuvre they have never been asked to perform (the come-alongside),
and a timetable.

---

## 2. The shape — a pilotage layer above the helm

### 2.1 The five phases

One state machine per boat under pilotage. Each phase names what it commands, what makes it **hold**,
and what makes it **abort**. Hold means *stay in this phase with way off*; abort means *fall back a
phase and re-plan*. Nothing ever advances on a timer alone.

| # | Phase | Commands | HOLD when | ABORT to |
|---|---|---|---|---|
| 1 | **Passage** | seek the next route mark at cruise; `DepthAvoid` live | a saturating give-way push (`resolve01 → 0`) | — (re-plan the route) |
| 2 | **Approach** | the fairway inbound; speed = `min(harbourSpeed, √(2ad))` measured **along the route**, not straight-line | traffic in the gate; berth not yet clear | **Passage** — go round; stand off in the basin |
| 3 | **Gate** | come to the **approach gate**: one hull-length off the berth, on the berth's own heading, on the up-tide side | not within pose tolerance (heading ±15°, lateral ±1 m) | **Approach** — take another turn |
| 4 | **Alongside** | close laterally at the **set rate** while holding the berth heading; astern takes off the last of the way | closing faster than the set rate; a fender contact reads hard | **Gate** — back off and re-present |
| 5 | **Moored** | helm dead; **the lines hold her** (`MooringLineMath`) | — | **Alongside** if a line slips on the ebb |

**Departure is the same machine run backwards** — `Moored → Alongside` (lines off, spring her off the
wall) `→ Gate → Approach → Passage` — with one asymmetry that is real seamanship rather than a code
shortcut: leaving, she has the whole basin to gather way in and no closing rate to respect, so the
`Alongside` hold conditions are dropped and the `Gate` tolerance widens. Same phases, same code, one
table of thresholds keyed by direction.

### 2.2 ⭐ The come-alongside is the one genuinely new manoeuvre

`ArrivalPilot` steers *to a mark*. A berth is not a mark, it is a **pose**, and the difference is the
whole ask. Three additions, all small, all pure:

- **The approach gate.** A berth's route does not end at the berth. It ends at
  `berth + heading⊥ · (halfBeam + fender + standoff)` displaced one hull-length astern along the berth
  heading. A skipper arrives *parallel and off*, then closes sideways.
- **The set rate.** Lateral closing speed is capped (recommend **0.25 m/s** — a fender's worth of bump,
  the "low-speed bumps are harmless" line of §3.4). This is a *second* speed loop, orthogonal to
  `ArrivalPilot`'s along-track one, and it is the number that makes a docking read as competent.
- **⭐ The last half-metre is the LINES, not the hull.** M2-38 already ships a made-fast line that holds
  a boat against drift. She stops with a fender's gap, the line goes over (the skipper's own toss clip
  already exists on his rig), and `MooringLineMath` pulls and holds her alongside on her berth heading.
  **That is the snap's honest replacement** — the pose the snap was faking is a *constraint*, and the
  constraint is built.

### 2.3 ⚠ ADJUSTMENT — two backends under one machine, and why

The handoff asks for "a pilotage layer above `BoatController`". That is right for the intro skipper and
wrong for ten boats, for two reasons that are not preference:

1. **Rule 7.** `BoatController` is a rigidbody with wave forces and seakeeping. Ten of them plus the
   player is a fixed-step bill the budget has never been asked to carry.
2. **Rule 5.** A rigidbody can be *pushed*. A determinism claim over ten shoveable hulls is unholdable,
   and a player who nudges one has silently broken her timetable.

So: **one phase machine over an abstract helm, two backends.**

| Backend | Drives | Used for |
|---|---|---|
| **Helmed** | `BoatController.SetControl` — real physics, real heel, real astern | the intro skipper; any boat inside the hand-over range of the player |
| **Kinematic** | `AmbientFleetSteering.Step`'s integrator — no rigidbody | every other boat |

The swap is safe **because the plan is the truth**: a boat's pose is a pure function of the clock, so a
promotion writes the pose the schedule says she has and nothing is lost. This is exactly the contract
§9.9 already states ("reads a deterministic sample, isn't bit-deterministic itself").

⚠ **And the honest cost, with its guard.** A promoted hull can be shoved off her plan; a demoted one
would snap back onto it — a teleport in a different costume. Two rules kill that: **demote only outside
the player's view**, and **promote onto the plan pose only if she is already within tolerance of it** —
otherwise the pilot keeps her real pose and re-plans from where she actually is. That is the arrival
fix's own "did I hold?" guard applied to the tier swap: *state you did not create is not yours to
overwrite*.

---

## 3. Zero collisions with non-water sprites

**The guarantee is the ROUTE. The reaction is the backstop.** Stating that the other way round is what
produces boats bouncing off quays.

1. **Plan-time depth gate (exists).** A station is legal only where
   `springLowLevel − elevation ≥ draught + clearance`. `AmbientFleetPlan` already does this, and does it
   at the tide's hard floor, so **no plan can ever be stranded by a falling tide**.
2. **⭐ Plan-time SOLID gate (new).** The same walk, testing the **swept corridor** rather than the
   centreline: half-width = `WatertightHalfBeamMeters + fenderClearance`, tested against every
   non-water collider. A hull is not a point, and the region's own lesson is on the record — *"a
   depth-along-the-line test cannot see this; the guard is a walk of the whole authored way to sea."*
3. **The wall-line offset.** Berths sit on an authored line 2 m off the quay face
   (`FirstBerthPos (98,85)`, face at y = 87). The approach lane is that line pushed out by
   `halfBeam + fender`, and the gate sits on it. **A route never has the wall inside its corridor**, so
   an on-plan boat cannot reach it.
4. **Live probes (exists, extended).** `DepthAvoid` for shoals; the same three probes read the solid
   field for walls. ⭐ **These are not the guarantee** — they exist for the one case the plan cannot
   own, which is *the player pushing a boat off it*, exactly as §9.9 already says about shoals.
5. **Last resort: all-stop and hold, never a bounce.** Astern to a standstill, hold, re-plan when clear.
   A physics impulse off a quay wall is the anti-goal wearing a different hat.

⚠ **What this deliberately does NOT guarantee: a player who rams an NPC.** That collision is real, and
§3.4 already rules that collisions should be *usually your fault*. The guarantee is NPC-vs-world.

> ### ⚠⚠ MEASURED — THE SHIPPED BERTH LINE ALREADY PUTS HULLS INSIDE THE WALL
> This is not a test the design owes; it is a defect the design found by taking the measurement.
> The north wall's south face is **y = 87**; berth centres are **y = 85** — a **2.0 m** standoff. The
> five quay-wall owners on the register, against their hulls' authored half-beams:
>
> | Owner (berth) | Hull | Half-beam | Reaches | vs the face |
> |---|---|---:|---:|---|
> | Arsenault Leo (1) | Lobster Standard Hardtop Northumberland | 2.50 | 87.50 | **0.50 m inside** |
> | Doiron Yvette (6) | Lobster Standard Open Fundy | 2.43 | 87.43 | **0.43 m inside** |
> | Gallant Marie (3) | Cape Islander | 2.40 | 87.40 | **0.40 m inside** |
> | Macdonald Ross (4) | Lobster Inshore Hardtop Newfoundland | 2.04 | 87.04 | **0.04 m inside** |
> | Campbell Hughie (7) | Lobster Inshore Open Northumberland | 2.00 | 87.00 | flush |
>
> **All five overlap or touch.** It is invisible today because the moored fleet is *placed* — nothing
> arrives, and nothing tests hull against wall. The moment a boat has to come alongside under a pilot
> that promises never to intersect the wall, **the berth line itself is the illegal object**, and no
> amount of good steering fixes a destination inside a quay.
>
> **The fix, and it is cheap.** Derive each berth's standoff from the hull that lies there —
> `halfBeam + fender` — rather than from one constant. That is the region's own established principle
> (*"gate the hull where the hull is"*, from the float berths). A uniform fallback of **2.8 m**
> (worst half-beam 2.50 + 0.30 fender) also works and moves the line from y = 85 to **y = 84.2**.
> Two knock-ons, both checked and both clear:
> - **The channel.** Routed 14 m clear of the berths against a 12 m half-width; the move leaves
>   **13.2 m**, so the cut over the berths stays exactly zero and the ruled gate is untouched.
> - **The mooring lines.** A 9 m scope against this wharf's 0.8–5.2 m drop reaches **7.4–8.96 m**
>   horizontally. 2.8 m is well inside it; tying up gets no harder.
>
> ⚠ **One caveat on the number, stated rather than buried.** `WatertightHalfBeamMeters` is a
> **water-render clamp**, authored deliberately *generous* ("slightly generous is safe — a touch
> drier"), not a surveyed beam. Generous is the **safe direction for a clearance**, and the float
> berths' beam gate already uses it exactly this way — so borrowing it is precedented, not novel.
> But it has a different owner and a different purpose, and the real overlaps above may be smaller
> than they read. If pilotage ever needs the exact figure, the clean answer is a real `BeamMetres`
> on `BoatHullDef`, which today carries `LengthMeters` and `DraughtMeters` and no beam at all.

---

## 4. The rules of the road, simplified

Four rules, chosen because each has **one readable tell** and none needs a player to know COLREGs:

| Simplified rule | The real one | The tell a player reads |
|---|---|---|
| **Meeting head-on, both go right** — pass port-to-port | Rule 14 | both boats swing the same way; you learn to swing right too |
| **Give way to the boat on your right** | Rule 15 (crossing) | she eases and passes astern of you — *visibly astern* |
| **The overtaker keeps clear** | Rule 13 | a faster boat goes wide around you and stays wide until past |
| **The working boat stands on** — a boat on her gear, or one committed to a berth, holds her course | Rules 17 / 9 | traffic parts around the boat doing a job |
| **Harbour speed inside the wharf line** | Rule 6 (safe speed) | every boat slows at the same place, every time |

**Legibility is Rule 8, and it is the design principle here: one big early alteration, never a series of
small ones.** A give-way boat that shaves 3° four times reads as drift. One bold 25° alteration, held,
reads as courtesy. This also makes the encounter *resolvable*, because the alteration is committed
early enough that the geometry has room.

⭐ **Half of Rule 14 already ships.** `AmbientFleetSteering.ComposeHeading` has a starboard bias gated
to the near-head-on case, with the orbit failure already diagnosed and fixed. Rules 13/15/17 are the
same shape: a gate on the relative bearing, a bias, and a speed check.

### 4.1 SIMULATED vs STAGED — the honest line

| | | |
|---|---|---|
| **SIMULATED** (live from geometry, every frame) | who gives way; the alteration; harbour speed; the all-stop; separation; the depth and solid probes | These must never be faked. The player will eventually put his own hull in the middle of one. |
| **STAGED** (chosen by the timetable) | **where and when two boats meet.** Departure and arrival windows are derived so that meetings land in the basin or the fairway's wide reaches, with room and sightline, rather than in the entrance at the turn. | ⭐ **The staging picks the encounter. The sim resolves it.** |

**The rule that keeps it honest, and it is P5's own line:** *never stage a collision course the sim
cannot resolve.* A near-miss the sim would not have avoided is a lie the player finds by joining it. A
scheduled meeting that the give-way rules then genuinely resolve is not a lie — it is a director
choosing where the scene happens, which is what a wharf's rhythm is anyway.

---

## 5. The timetable — determinism, and a wharf whose day you can learn

**The law (rule 5): a boat's phase, pose and berth state are a pure function of `(worldSeed, gameTime)`.
Nothing is saved. A boat mid-manoeuvre across a save/load re-derives.**

The two precedents are already in the repo and this borrows both rather than inventing:

- **`RoutinePlan.SampleAt(hour)`** — the villager model. Nothing ticks; a region loading at 14:20 puts
  everybody mid-stride on the right leg with no snap. A boat is a villager with a bigger turning circle.
- **`AmbientFleetSchedule`'s parity trick** — "is a buoy out?" is closed-form off visit parity. **"Is
  berth *n* occupied?"** is the same shape: floor the boat's slot position, and the parity of the
  latest completed sailing decides. No state machine, no save.

**Departure jitter is a personality, not noise** — seeded from `(worldSeed, ownerId, entryIndex)` with
the day index deliberately *not* folded in, so Marie Gallant leaves a few minutes past the same beat
every morning and a player can learn her. That is `RoutineSchedule`'s own ruling, verbatim.

### 5.1 ⭐ The tide writes the timetable, and that is the feature

A boat's windows are **not authored clock hours**. They are derived: her berth has water for a fraction
of the cycle (at NMC's −1.6 m gate against ±2.2 m: **lobster boat 54.4 %, Cape Islander 52.9 %, dory
70.1 %**), so she leaves on the last of the ebb and comes home on the flood **because that is when she
can**. The legible rhythm the owner asked for falls out of the depth gate for free instead of being
typed. P1 and P3 in the same number.

### 5.2 When she cannot reach her berth — the fallback ladder

Deterministic, ordered, and each rung is a system that already exists:

1. **Stand off** — lie-to in the channel. NMC's channel *always holds water*, even at spring low (the
   2026-08-19 ruling): the waterline narrows 24 m → 16 m over the last 0.6 m of ebb and never closes.
   `HoldStation` already does this and already has the anti-orbit hysteresis.
2. **Anchor off** in the bay — `BoatAnchor` / `AnchorMath`, rode-gated, already shipped.
3. **Wait for water.** She comes in on the flood.

**She never grounds on a plan.** Grounding is the player's privilege (P5), and this is the one place the
NPC fleet is deliberately more competent than the player.

### 5.3 Berth allocation

| Kind | Who decides | What it is |
|---|---|---|
| **Owned** | `BoatOwnerDef.Moorage` + `BerthIndex` — **already data, already unique-per-owner and content-tested** | hers all day. She leaves it and comes back to it. Nothing to allocate. |
| **Transient** | the timetable | the unload apron at the winch, and any wall berth whose owner is at sea. For visitors and for the player. |

⚠ **A first-come queue is a saved state and therefore illegal here.** Transient berths are allocated by
a **window**: a berth is assigned to the lowest-ordinal claimant whose window covers this instant. A
window is a function of the clock; a queue is not.

**When hers is taken** — which in practice means *the player parked in it*, since the schedule cannot
double-book:

1. She **stands off** in the basin and waits, visibly, on the plan.
2. After a grace window she takes the **nearest free transient berth** (deterministic ordering).
3. If none, she **anchors off**.

**Her boat is never moved and the player's boat is never moved.** Park in a fisherman's berth and you
watch him wait — and the wharf gets to have an opinion about it. That is P5 pointed the friendly way.

---

## 6. The Core seams (rule 4)

`HiddenHarbours.Boats` references **only** `Core` and `Unity.InputSystem`. That is not a formality here:

> ⚠ **`NavChannel` lives in `HiddenHarbours.World`. Pilotage cannot read the fairway today.** This is
> the one genuinely new Core contract the design needs, and it should be raised with `lead-architect`
> before S3.

| Seam | Status | Direction | Why |
|---|---|---|---|
| `IHarbourPlan` | ⭐ **NEW (Core)** | World → pilotage | the fairway polyline, the berth tables, the wharf line, the approach lanes. The region publishes; pilotage reads. Mirrors `ITidalTerrain`'s register-on-load / null-is-open-water contract. |
| `ITidalTerrain.ElevationAt` | exists | World → pilotage | the depth field. The map, and there is no other. |
| `IEnvironmentService.WaterLevelAt` | exists | Core | tide now, and the spring-low floor the plan gate uses. |
| `IGameClock` | exists | Core | the timetable's only input besides the seed. |
| `MooringCleats` / `IMooringCleat` | exists | both | the lines that hold her alongside — the snap's replacement. |
| `IStandableSurface` / `StandableSurfaces` | exists | Boats → Player | the deck the passenger rides and steps off. |
| `IActiveBoatService` | exists | Core | where the player's boat is, for give-way and separation. |
| `IRadarContacts` / `RadarContact` | exists | pilotage → UI | ⭐ **reuse.** NPC traffic gets a radar return for free, and the fog/Smother payoff (§4.3) gets its traffic. |
| `BoatDocked` / `BoatSailed` on `EventBus` | ⭐ **NEW** | pilotage → world / economy / audio | the wharf's day becomes legible to other lanes with no coupling: the plant lands a catch, the winch starts, the engine note changes. |

---

## 7. What is data (rule 2 / rule 6)

| Def | Carries | Note |
|---|---|---|
| `PilotageDef` (per region) | harbour speed, the wharf line, gate offsets, set rate, give-way radii and bearing gates, hold/abort thresholds, tier hand-over range, how many boats may be helmed at once | the `AmbientFleetDef` mould exactly — one asset per region, indexed by a Resources library |
| `VesselMannersDef` (per hull class, overridable per owner) | cruise, approach decel, astern limit, turning circle, **how boldly she alters and how early she comes off the throttle** | ⭐ **this is what makes ten boats read as ten skippers** instead of one behaviour ×10. Cheapest character in the whole design. |
| `RouteAssignment` + `FishingGroundDef` | owner → ground + the leg out | **already named** in `settlement-population.md` §4 — use those names, do not invent parallel ones |
| `BoatOwnerDef` (extend) | `WorkRhythm` (the day off), `SailsOnTide` | `nine-mile-creek-wharf.md` §9.2 already asks for the work-rhythm field |
| Berth tables | `NineMileCreekMainland.BerthPos` etc. | **stay region constants.** They are geometry, not tunables — the region builder derives them and tests walk them. |

Nothing about a boat's manners, route or rhythm is a magic number in C#.

---

## 8. Phasing

⚠ **Note the split: the intro skipper docks at ST PETERS' pier; the busy wharf is NINE MILE CREEK.**
They are different regions and different slices, and conflating them is how S1 grows into S4.

| Slice | What | Lane | Size |
|---|---|---|---|
| **S1 — the come-alongside** ✅ **SHIPPED** | The phase machine over an abstract helm, **helmed backend only, one boat**: the intro skipper. Approach gate → parallel come-alongside at the set rate → astern stop → **the lines take the last half-metre**. **Both snaps deleted.** | gameplay-systems | **M1-polish** |
| **S1b — the berth line** | Derive each NMC berth's standoff from the hull that lies there, and assert it against the wall face (§3). Independent of S1, needed before any boat *arrives* at NMC. One derivation + one EditMode test. | world-content | **tiny** |
| **S2 — she is a boat, not a fixture** | Departure as the machine reversed. NMC's moored register can leave and come home on a hand-written timetable — one boat at a time, no traffic yet. Proves the machine runs both ways before anything depends on it. | gameplay-systems | small |
| **S3 — the deterministic timetable** | `SampleAt(clock)` over the register: ten boats, windows **derived from tide**, closed-form berth occupancy, save/load re-derive. Needs `IHarbourPlan`. | gameplay-systems + economy-sim | **M2** |
| **S4 — traffic and the rules of the road** | Kinematic backend + the tier hand-over and its two guards; give-way rules; harbour speed inside the wharf line; staged encounters; the all-stop backstop. | gameplay-systems | **M2** |
| **S5 — the wharf tells you** | The day reads without a HUD: engines at first light, the winch working, boats landing at the plant. Rides `BoatDocked` / `BoatSailed`. | audio + ui-ux | M2+ |

**S1 is the whole owner ask #1 and it is buildable now** — it needs no new Core seam, no new data, and
no traffic. Everything it touches is `App` and `Boats`.

### 8.1 What S1 actually shipped, and the five things the build found

`PilotageHelm.cs` (the phase enum + `IPilotageHelm` + the helmed backend), `BerthPilot.cs` (the pure
come-alongside maths), `BerthingPilot.cs` (the phase machine), and the deletions in `ArrivalOpening`.
It needed **no new Core seam** and **no scene or builder change** — §8's claim held. Five things the
build learned that this document did not know:

1. **⭐ The wharf line is the last authored route mark.** §2.1's Approach row wants a speed limit
   "inside the wharf line" and never says where that line is. It does not need to be authored: the
   route's **last mark before the berth** is the harbour's own front door — at St Peters it is
   `ApproachFrom`, the dredged channel's mouth, 43.9 m out. So harbour speed costs the passage
   43.9/3 − 43.9/5 = **5.9 s**, which is the ~6 s Q2 estimated, arrived at from the geometry rather
   than from a guess. Re-cut the channel and the limit moves with it.

2. **⚠ The crab must be capped by the pose tolerance, and §2.2 does not say so.** The set rate's aim
   angle is `atan(closing ÷ alongSpeed)` — a geometry, not a gain — which means it GROWS as she slows,
   because the denominator shrinks. Left uncapped, the last few metres of a come-alongside command a
   bigger and bigger angle and she arrives lying across her own berth, out of pose, holding for ever.
   The cap is `min(maxCrab, headingTolerance)`: *a boat may not aim herself out of the pose she is
   trying to reach.*

3. **⚠ The set rate is the COME-ALONGSIDE's number, not the line-up's** — §2.1 puts it in the
   Alongside row and nowhere else, and §2.2 does not say what governs the Gate. Rate-limiting the
   *line-up* at a fender's 0.25 m/s is a boat who cannot cross her own approach. Measured on the
   real St Peters fairway: the last leg bears **−104°** against a berth on **−90°**, so she meets
   the gate's capture ring about a metre INBOARD of the berth line with **2.96 m** still to come
   across and roughly **5.2 s** of run left. At the set rate that buys 1.3 m — she reaches the
   station 1.7 m off her line, fails the ±1 m pose, and holds there until the settle fallback.
   Worse, the loop actively *undoes* the useful crab the leg's own bearing already gave her. The
   gate therefore closes at the **berthing speed** and only the come-alongside closes at the set
   rate, with the crab cap doing the real bounding either way.

4. **⭐ A route is a set of marks; a hull is not a point — the WHEEL-OVER.** Nothing in §2 says how a
   pilot leaves one leg for the next, and "steer for the mark until you are inside the arrive radius"
   is not it. A 12.9 m hull at the fairway's 5 m/s turns at a **24 m radius**; St Peters' fairway
   turns **65°** at its landfall and **61°** onto its last leg. Turning *at* the mark therefore puts
   her out of the corner about **11 m** off the next leg — and pursuit then hauls her back toward a
   mark already astern, which is a circle. Measured on the real fairway with the corner uncut: she
   came out of the last turn eleven metres seaward, reached the gate's capture ring **7 m** off her
   berth line with twelve metres of berth to fix it in, spent both aborts, and was tied up by the
   settle fallback 7.11 m off — at both spring low and spring high, identically.

   The fix is the number every paper passage plan already carries beside its course changes:
   **wheel over `R · tan(Δ/2)` short of the mark**, with `R = speed ÷ turn rate`. It scales with
   speed, so a boat slowing into the harbour cuts less, exactly as a real one does; it is bounded at
   half the incoming leg, because a turn begun before the mark you are turning *from* is two corners
   overlapping. One declared tunable — `TurnRateDegreesPerSecond`, a statement about the hull rather
   than a taste — and the anticipation falls out of the geometry. A passed-mark arm ("once the buoy
   is abeam you are on to the next one") backs it up for the corner the anticipation still misses.

   ⚠ **And this is the second defect §8.2's collider fault was hiding.** With a 177 m turning circle
   she could not round the corners at all; fixing the collider let her round them, and only then was
   it visible that she rounded them *wide*.

5. **⚠ An abort must actually go round.** §2.1 says Gate aborts to Approach; it does not say she must
   leave the gate first. Without that, "take another turn" is a phase flip: she falls back still inside
   the capture range, is re-captured on the next step, fails the same pose, and ping-pongs through the
   abort budget without ever presenting a second time. **The gate is capturable only from ASTERN of
   it** — which is the seamanship as well as the fix, and it is one line.

### 8.2 ⛔ What the deleted snap was actually hiding: she could not turn

The come-alongside landed green in EditMode and then failed every real-fairway PlayMode test the same
way — *"stuck in Approaching/Passage, 206 m from the berth, throttle 0.92, steer −1.00"*. She was
sailing away from the harbour in a slow circle. The cause is not the pilotage layer, and it is worth
writing down because it is the anti-goal's whole point arriving on schedule.

**`ArrivalOpening` sized the arrival hull's collider to the hull's real dimensions** —
`LengthMeters × 0.37` = **4.77 × 12.9 m** — while every boat the player sails carries
`PersistentCoreBuilder`'s fixed **1.7 × 4.0 m** capsule (`BoatController.SetHull` re-derives her MASS
from the displacement and never touches the collider). **Unity derives a rigidbody's moment of inertia
from its collider, and inertia goes as the square of the dimensions.** At `MassKg/100 = 60 kg`, full
helm gives `RudderAuthority(5150) × RudderFeelScale(0.01) = 51.5 N·m` against `angularDamping = 2.5`,
so her steady turn rate is `T / (I · d)`:

| collider | `I` | turn rate | turning radius at cruise |
|---|---:|---:|---:|
| hull-sized (4.77 × 12.9) | 946 | **1.25 °/s** | **177 m** |
| the shipping capsule (1.7 × 4.0) | 94 | **12.5 °/s** | **17.7 m** |

**A twelve-metre boat with a 177 m turning circle.** St Peters' fairway turns **65°** at its landfall
mark and **67°** back at the channel mouth; rounding those needs about **11 m** of tangent, which the
27 m leg between them affords easily at 17.7 m and never at 177 m.

⭐ **So the arrival has never navigated the fairway.** She ran straight through both corners, passed
the berth about 22 m off, took the way off where the closest-approach guard caught her, and the SNAP
put her on her berth. The passing test was measuring the teleport. This is §0's claim in its strongest
form: *everything before the snap is already right* was itself only true because the snap was there.

The fix is one line — she carries the same capsule as the boat the player is about to be handed, which
is this class's own founding law (*"it had better be how they move"*) kept in the one place it was not
— and it is pinned by a PlayMode test that measures the property rather than the mechanism: full helm,
cruise, and a floor on the degrees per second.

⚠ **And one number the S1 build could not change and the owner may want to.** The arrival's
`_dockingSettleSeconds` — the "tie her up regardless" bound — is serialized at **12 s** in the
committed St Peters scene, and it was measured against the OLD docking (point at the berth, ask for
zero: a few seconds of astern). A come-alongside is about 27 s at the shipped tuning, so a 12 s
stopwatch would fire in the middle of the manoeuvre it is a bound on. S1 floors the bound on the
manoeuvre's own budget in code, derived from the tunables, so the shipped scene behaves correctly
without a rebuild — but the field now says something smaller than it means, and a Build click that
re-serializes it to ~30 s would make it honest.

### 8.3 ⭐ The intro cabin — the game opens BELOW his deck (world-content, 2026-08-27)

The passage now starts inside the skipper's house rather than on his deck. She goes below on the
cape's own `BoatInteriorDef`, can **move about the cabin** while he runs the marks, and comes up on
deck through **his own aft door** for the come-alongside. From the moment she is on deck, S1 above is
untouched — the docking, the tie-up, the moored beat and the step ashore are the same code, in the
same order, with the same numbers.

**It adds no second mechanism, and that is the whole design.** The room, the door, the level map and
the cutaway are the ones `BoatInteriorInstaller` already grows on every hull that spawns (ADR 0038);
going below is `BoatInterior.TryEnter`, so `CabinEntered` is published from its usual place and
`BoatCutaway` opens the house through the seam it already listens on. Coming out is the real
`BoatCabinDoor`, with the cue the sidecar measured. What is genuinely new is one thing: **a walker
inside a moving hull has a position on the sole** — `BoatCabinWalkMath`, which is `DeckAreaMath`'s
transform (called, not copied) over a `BoatInteriorLevel`'s outline and its furniture.

Four things worth writing down:

- **The cutaway needs no ruling here.** `BoatCutaway` refuses the cut for whoever is steering
  (`HelmSlot.PilotedHull`, the occupancy law of #642), and the arrival never declares the player as
  piloting anything — she is *carried*. So a passenger below gets the cut and Armand keeps his wheel,
  read straight off the two facts that already say it.
- **⚠️ The cape's rig has never had a cutaway pass**, so her `HullMeshDef` carries no `LevelTags` and
  `CutawayForDeck` answers `Cut.None`. The gate engages correctly and opens nothing: what the player
  sees below is the room drawn over her *closed* house — the overdraw the cabin gate was opened on
  measured evidence to accept. Seven rigs have the pass (batch 1 + batch 2); hers is not among them.
  **Upstream art item**, not a gameplay gap. `BoatCabinWalkMathTests` pins the join rule
  (`BoatInteriorDef` level ids against `HullMeshDef.LevelTags[].DeckId`) so a bake that arrives in the
  rig's vocabulary rather than the def's goes red instead of silently cutting nothing.
- **⛔ No teleport at the threshold**, in either direction. Both intro teleports died with #661 and
  continuity is the law. Crossing the door changes which *frame* she is placed in — sole to deck —
  and the join is a **seed**, not a placement: coming out, `_passengerDeckOffset` is re-read from
  where she is standing, so the next seat reproduces her exact world point and then carries it round
  with the hull. The visible consequence is that she stands where she came out, at the threshold,
  rather than at a point somebody typed. On a hull with no measured interior nothing above runs and
  the authored offset stands, so the arrival is byte-for-byte the one that shipped.
  *(§8.4: the deck WALK is seeded at the same moment and by the same rule, in its own frame — two
  seats, one law, so whichever of them places her the doorway still moves nobody. Her cabin facing
  crosses with her.)*
- **⚠ `CanStepAshore` gained "and she is not below."** You do not step onto a wharf from inside a
  cabin, and the step is an arc from where she is standing. Nothing is taken away: the offer is not
  on a clock (Q1's ruling), so it is simply waiting the moment she comes up.

**Still the owner's:** her resting look — the surround is drawn at every `doorOpen`, so the aft face
changes whichever pose ships, and this lane ships the kit default (`doorOpen 0`, closed) unchanged.

### 8.4 ⭐ …and she walks the DECK too (gameplay-systems, 2026-09-04)

§8.3 left one thing open — *"whether she may walk the DECK as well as the cabin"* — and the owner
answered it by playing the opening: *"the player is unable to walk on the boat deck in the new intro,
**going outside locks them in place**."* She may.

It was a design gap rather than a regression. The opening was built as *walk the cabin → come up →
ride in → step ashore*, so `ArrivalOpening` ran a walk BELOW and none ABOVE: `_passengerDeckOffset`
was written twice — its serialized default and the threshold seed below — and thereafter only read.

**`ArrivalDeckWalk` is `ArrivalCabinWalk` one deck up, and it adds no second mechanism either.** It is
bookkeeping: it holds a hull-local point and never writes her transform, and the arrival's one
`LateUpdate` still places her. Every quantity in it is computed by the component that already owns it
— the step and the clamp are `DeckWalkController.StepOnDeckPolygon` over this hull's authored
polygons, the projection onto the drawn picture is `DeckAreaMath.DeckToWorld`, the world→hull join is
`DeckWalkController.SeedDeckLocalPure` (made public for this third caller rather than restated), and
her facing is `DeckRiderFacingMath`'s composition of a deck bearing with the hull's drawn heading. One
quantity, one computation: a second clamp is the shape this project has already paid for.

Three consequences worth writing down:

- **The composed facing means a standing passenger turns WITH the hull**, and a walking one faces the
  way she is walking. At a deck bearing of zero it reduces to the shipped `HoldHeading(drawn)` exactly,
  so her picture is unchanged until she presses a key. Her gait is metres of DECK per second, so the
  hull's own five knots still contribute nothing — the walk-in-place defect stays fixed.
- **She does not own a `DeckWalkController` of her own, deliberately.** The `ControlSwitcher` owns
  those and enables them by MODE, and the arrival never sets a mode (she is not aboard *her* boat).
  A controller the switcher believed it had disabled would be a second writer of her position.
- **The open door was never the gate.** `BoatCabinDoor` runs its cue and calls `TryEnter`/`TryExit`;
  it does not touch her transform. The missing deck walk was the whole of it, so no door changed.

---

## 9. Open questions for the owner

**Q1 — Does the player walk herself ashore, or does the game put her there?**
✅ **RULED 2026-08-26, the owner's words:** *"the player is on the boat until they use the exit key to
step onto dock."* She stays aboard as long as she likes; **E** plays the existing step-ashore MOVE; the
skipper's line waits indefinitely. No timer, no auto-hand-over, and `HandOver`'s player teleport
**deleted** rather than relocated. Built in S1.
*(Recommended below, and the recommendation is what was ruled.) Recommend: she walks.* The boat comes alongside; the player presses **E** and the existing step-ashore
MOVE plays (M2-37/38 built it — *"stepping ashore is the same move mirrored"*). Her first act in the
game is her own, and it teaches the verb she will use for the next forty hours. Cost: she can stand on
the deck indefinitely — the skipper's line simply waits for her, which is fine and arguably better.

**Q2 — Harbour speed: 3 m/s or the shipped 5?**
⏳ **NOT RULED — and S1 did not block on it.** Harbour speed is a TUNABLE
(`BerthPilot.Settings.HarbourSpeedMetresPerSecond`, serialized on the arrival beside the pilot's own
settings), shipped at the recommendation below: **3 inside the wharf line, the fairway left at 5**. The
measured cost is **5.9 s** on the 45 s passage — see §8.1, where the wharf line turns out to be the
route's own last mark rather than a second authored number. Retune it without a recompile if 45 + 6 is
too long.
*Recommend: 5 down the fairway, 3 inside the wharf line.* `ArrivalPilot`'s own tooltip calls 3 m/s
(≈ 6 kn) *"harbour speed for a working boat coming in"* and then ships 5 (≈ 9.7 kn). At 5 m/s her stop
is **33 m**; at 3 m/s it is **13 m** — and 13 m is what fits between berths spaced 5.5 m apart. Cost:
the St Peters arrival grows ~6 s on an already-over-budget 45 s. The two levers on that were already
put to you (start inside the landfall buoy, or accept the length); this makes the second one 6 s worse
and the docking much better.

**Q3 — How many boats are helmed (real physics) at once?**
*Recommend: two* — the player's nearest neighbour and whoever is currently manoeuvring closest to her.
One number in `PilotageDef`, and it is the perf budget's only real dial.

**Q4 — A boat whose berth the player has taken: does she wait, or is she re-berthed?**
*Recommend: she waits, visibly, then takes a transient berth, then anchors.* Never move the player's
boat, and never dissolve the consequence. This is the cheapest characterful teeth in the design.

**Q5 — Should the side dragger's exclusion from NMC become a hard docking refusal?**
*Recommend: no new rule.* Pilotage simply refuses to *plan* a berth a hull cannot lie at, so no NPC
dragger ever comes in under her own pilot. A player who sails one in still takes the ground for 70 % of
the cycle — which the 2026-08-06 correction already argued is the better and more honest exclusion.

**Q6 — Move the berth line, or move the wall?**
*Recommend: move the berth line, per-berth, off the hull that lies there.* The §3 box measures all
five quay-wall hulls overlapping or touching the wall face. Moving the wall is a terrain edit with a
photograph behind it; moving the berth line is one derivation, it costs nothing downstream (channel
and mooring reach both checked clear), and it makes the standoff *say* what it means — a fender's gap
outboard of this boat's own beam. The alternative worth naming: **keep 2.0 m and accept a rubbing
strake against the timber**, which is what a real working wharf looks like and which several of these
hulls are within 5 cm of anyway. That reading needs the collision guarantee in §3 to be phrased as
*"never intersects the wall by more than a fender"* rather than *"never intersects"* — a weaker
promise, honestly stated, and yours to prefer.

**Q7 — How busy is "busy"?**
*Recommend: ten boats, but never ten at once.* The register is ruled at **10 for M2** (7 today, capped
by shed lots on the spit, not by art). Movements spread across the tide mean typically **one to three
boats under way** in the basin at a time, which is a working wharf rather than a regatta — and it is
also what keeps the helmed-boat count in Q3 honest. Say the word if you want the wharf busier, and the
lever is the tide spread, not the boat count.
