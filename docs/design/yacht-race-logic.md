# Yacht race club logic: implementation contract

The club design draft (PR 1130)
describes the requested start, course, rules, turn, handicap and season. This implementation
extends main's [existing isolated race model](sailboat-race-logic.md). It instantiates no
scene object and serves P1 (read the wind), P3 (club competitors) and P5 (recover from mistakes).

## Authoring and compatibility

Reuse `SailRaceCourseDef` and `SailRaceNpcDef`, with their existing stable ids and one entity
per asset. The course already owns its marks as logical parts. It now also owns club turn
rules, a table of hull-id/time-multiplier pairs and low-point season scoring. The NPC profile
owns its skipper's header threshold, layline margin and minimum tack interval. These are
serialized parts of those entities, not new global club/rating/season content entities.
No course, rating or skipper asset is authored in this change; existing Def numbers, ids
and references are untouched. Independent per-hull rating or season entities remain future
authoring work if sharing across clubs becomes necessary.

`Build` on each existing Def keeps its old behavior. Club users explicitly call
`BuildClubSession` or `BuildSkipper`; zero/unset club rules fail validation. A course's
`HandicapFor` rejects unknown hulls, duplicate ratings and nonpositive/nonfinite multipliers.
The session builder requires entries to use that course's authored rating. `BuildClubSeason`
accepts explicit race-instance and entrant ids and copies the course's points table. Runtime
course geometry, rule values, rating values, roster and season arrays are snapshots.
The existing polar is a read-only input for the race lifetime, as in the original model.

This choice also keeps the shared Def identity ledger untouched while another open change
owns it. No identity guard is modified or bypassed, and no new id-bearing Def type is added.
The design's suggested independent rule/rating Defs are represented as course parts for
this first isolated slice; this distinction is deliberate and has no live-content migration.

## Tick and rules

Construct `SailRaceSession` with a fixed roster and simultaneous initial samples at or
before the gun. Supply one `RaceBoatMotion` per entrant in registration order on each
chronological `Tick`. Use actual sampled positions/headings; a track segment represents a
straight movement between observations. Bad roster, time, distance, wind or pending-turn
heading data is rejected before any entrant changes. Source tracks still use the original
start recall, ordered radial rounding, lap and interpolated finite-line finish logic.

`SailRaceClubJudge.Judge` is a stateless pair judge. Port gives way to starboard; on the
same established tack, an overlapping outside boat gives the inside boat room at their
same next mark/lap; otherwise windward gives way to leeward. Overlap compares projected
separation with the two authored overlap lengths. Both boats must be inside the next
mark's zone, on similar headings (positive heading dot product), with distinct radii.
This is current geometry, not zone-entry rights. Calm, head-to-wind, dead-astern and equal
projections do not invent an entitlement. Argument order cannot change the offender.
The original overtaking/contact/time-penalty judge is preserved as a separate API.

The session evaluates sampled contacts before awarding gates for that segment. Each
pair latches at contact and releases beyond the course's larger release radius; only one
owed turn is issued per episode. Finished/retired boats and pre-gun samples earn no new
turns. A contact episode without a determinable give-way boat receives no later invented
offence until separation. Swept collisions, damage, protests and avoidance are not inferred.

`SailRaceTurn` requires a continuous signed rotation and minimum elapsed turn time.
Reversals restart the sweep, ambiguous heading jumps are rejected and each additional
offence owes another complete turn. Course progress is suppressed during an owed turn,
including the completing segment; partial rounding is reset. Positions/time still update,
so a boat must subsequently cross any missed gate again. A turn costs actual race time,
without synthetic seconds or movement. The in-memory session exposes entrant status,
owed turns, overall completion and explicit retirement. It writes no save data.

Results expose raw time, any legacy time penalty, hull multiplier and corrected time:
`(elapsed + legacy penalty) * multiplier`. Finishers sort by corrected time then ordinal
entrant id; unfinished/retired boats have no numeric finish. Season records require the
whole registered roster, all finished or retired, exactly once per authored race order.
Low-point totals have no discards; equal totals sort by ordinal id. Results and tables
are built outside ticks. No rewards, money, scheduler or persistence is added.

## Skipper, wind and cost

`SailRaceNpc.CreateSkipper` creates a reusable runner beside the original bounded `At`
replay. `Tick()` advances one authored fixed step anchored at the gun. It reuses the
existing course route, buoy-clearance checks, polar speed and hull no-go threshold.
The Core-only [IRaceWindSource](../../Assets/_Project/Code/Core/Services/IRaceWindSource.cs)
receives the world seed and absolute tick time and returns the existing downwind m/s
vector. A future composition-root adapter must call main's weather model with the region
profile and clock scale. The production Boats assembly gains no Environment dependency.

Reach a sailable waypoint directly. Upwind, search the polar for close-hauled VMG, select
a tack deterministically and follow the waypoint's two lay lines. Switch near the opposite
lay line or on an adverse header when the other tack improves progress, respecting the
authored minimum interval. Headers accumulate relative to the wind at the last tack,
including gradual shifts. Every moving heading stays outside the hull's true-wind no-go;
calm produces no movement. Seed/id select the original speed skill and symmetric tie.
Finished runners freeze without further wind queries. The old `At` replay retains its
original query-order/interpolation behavior; the new runner replays the same fixed-tick
sequence when reconstructed with identical inputs, and is not a random-access query.

Setup allocates arrays, routes and entrants. `SailRaceEntrant` now reserves its lap array
at construction, retaining bounds checks on completed lap reads. The club tick path,
first contact, owed-turn transitions, lap completion, pure judge and incremental skipper
allocate zero managed bytes. Roster contact work is O(boats squared); each navigation
step searches at most the profile's heading-sample count. This is a small-club reference
simulation, with no physical hull dynamics, currents, grounding or fleet avoidance.

## Proof and remaining integration

The original five race fixtures retain all 50 cases. New `YachtRaceClubTests` covers
25 cases including missed marks, both directions/orderings of each entitlement, turn
timing/oscillation/queueing, blocked starts and finishes, atomic invalid input, handicap
reordering, complete season results and Def snapshot/validation behavior.
`YachtRaceSkipperTests` has 10 cases: upwind tacking, sudden and gradual headers, calm,
the real main weather model through Core, seed/time reproduction, Def wiring, post-finish
freeze, two laps and both unchanged shipped sloop polars under eight wind bearings.
`YachtRaceAllocationTests` has three calibrated allocation proofs covering a fresh first
contact/turn, a fresh first lap finish, and skipper tacks/rounding/finish. Native Unity
uses a current-thread `GC.Alloc` recorder with a positive control and an empty-scope
control; a zero-returning thread counter cannot masquerade as proof. The independent
managed host uses its calibrated byte counter and real Unity vector/math assembly.
The native verification CLI requires exact class counts, including all three new classes.

The managed source host passes 88 cases. Full Core and Boats source and the selected
native EditMode fixtures/verification entry point compile against Unity references.
The source host supplies object/asset facades: it does not prove Unity import, native
object lifecycle, serialized Inspector roundtrip, PlayMode or a playable yacht club.
No editor lease was granted or used; native CI must supply EditMode execution.

The scene lane must author safe water, depths, real buoy/support visuals, course/profile
assets and reviewed hull ratings. It must wire clock/wind, physical track sampling,
NPC steering/avoidance/grounding safety, entry/retirement, signals, turn feedback and
results. Access, engine enforcement, prizes, persistence and save migration need separate
work. No scene, prefab, art, existing balance number or save bytes change here.
