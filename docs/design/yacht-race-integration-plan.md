# Yacht-club race integration plan

Status: proposed integration sequence, based on main
`00b030cf8d286bc9d3b1464acd55c025e5a26a32`. Documents only. This plan serves P1
(sailing against real wind), P3 (club competitors) and P5 (recoverable race mistakes).
It authorizes no scene wiring, art acceptance, balance change, prize or save schema.

## Reuse and actual gaps

Read alongside the [original race model](sailboat-race-logic.md),
[club implementation contract](yacht-race-logic.md) and
[sail controls](sail-controls.md). The model has already grown beyond the original
reference replay. The integration should use the club session and incremental skipper.

| Existing source | Reuse / integration constraint |
| --- | --- |
| [SailRaceCourseDef](../../Assets/_Project/Code/Boats/Racing/SailRaceCourseDef.cs) | `BuildClubSession`, `HandicapFor`, ordered course geometry and `PhaseAt`; preserve authored ids, validation and ratings. Phase boundaries are derived from the gun, not wall time. |
| [SailRaceSession](../../Assets/_Project/Code/Boats/Racing/SailRaceSession.cs) | Authoritative entrant progress, recall, contacts, owed turns, retirement and sorted results. Initial samples must share one time at or before the gun; every subsequent tick needs the complete roster in registration order. |
| [SailRaceNpcDef](../../Assets/_Project/Code/Boats/Racing/SailRaceNpcDef.cs) / [SailRaceNpc](../../Assets/_Project/Code/Boats/Racing/SailRaceNpc.cs) | `BuildSkipper`, `Skipper.Tick`, fixed-step navigation and the existing hull polar/no-go threshold. `At` remains a separate bounded replay; do not call it for every render frame. |
| [IRaceWindSource](../../Assets/_Project/Code/Core/Services/IRaceWindSource.cs) | The Core seam already exists: `Vector2 WindAt(int worldSeed, double gameTime)`. Add an implementation, not a competing interface. |
| [WeatherModel](../../Assets/_Project/Code/Environment/WeatherModel.cs) / [EnvironmentService](../../Assets/_Project/Code/Environment/EnvironmentService.cs) | Reuse `SampleWind` with the selected region's profile and exact clock scale. `Sample()` reads the current clock; `WaveWindAt` also reads mutable active-region/dev state. Neither is an immutable race input by itself. |
| [IGameClock](../../Assets/_Project/Code/Core/Services/IGameClock.cs) / [GameClock](../../Assets/_Project/Code/Environment/GameClock.cs) | Absolute `TotalSeconds` is race time. The held hour lets `FlowSeconds` and physics continue while `TotalSeconds` stops; race integration must handle that explicitly. |
| [BoatController](../../Assets/_Project/Code/Boats/BoatController.cs) | Actual hull, `Velocity`, Rigidbody position and `transform.up` bow direction for a Boats-owned track adapter. Keep normal sail/physics control; a race result never supplies a transform. |
| [EventBus](../../Assets/_Project/Code/Core/Events/EventBus.cs) | Notify observers after state commits. Entry success and scoring never depend on a subscriber receiving an event. |

**Blocker: two progress authorities.** `SailRaceNpc.Skipper.Tick` calls its own
`Race.Advance` and uses that entrant to select route retries. `SailRaceSession.Tick`
separately suppresses course credit while a turn is owed. Feeding skipper positions
into a session after contact can leave the skipper routing to a later mark that the
session has not awarded. It also has no session-directed penalty-turn maneuver.
Failing case: charge an NPC a turn while it crosses its next mark; the session keeps
the old mark while the skipper advances its private entrant. A physical adapter
cannot treat both as authoritative. This is a source-derived integration risk, not a
claim of a defect observed in a playable race. No live race is wired at this base.

Before physical NPC integration, extract navigation state/intent from adjudication
inside Boats. The navigator must read the session's next mark, lap and owed turns;
it must not award course credit. Its intent drives normal steering/trim, and only
the resulting observed hull track reaches the session. Preserve the existing `At`
and `BuildSkipper` contracts and tests as reference APIs, or migrate their exact
rules in a separately reviewed change. Do not suppress penalties to hide divergence.

## Entry authority and start signals

The following Core shapes are proposals, not APIs implemented by this document:

| Boundary | Proposed request/result or notification | Authority |
| --- | --- | --- |
| Club interaction to race coordinator | `IRaceEntryService.TryEnter(request)` with request id, club/course ids, `racer.*` id, current runtime boat binding and expected roster revision; typed accepted/refused result with current revision | Boats owns the in-memory registry. App injects it through Core. World/UI never hold a `SailRaceSession`, hull Def or NPC Def. |
| Participant withdrawal | `TryWithdraw` before roster freeze; `TryRetire` afterwards, both revision-checked | Coordinator validates the binding and commits once. Retirement cannot erase a finished result. |
| Late observer / re-enabled consumer | Read-only `RaceSnapshot(sessionId)` with phase, roster revision, scheduled gun, status and result availability | A snapshot is current truth; missed events do not recreate races. Expose value views, not mutable model entrants. |
| Entry committed | `RaceEntryChanged(sessionId, revision, racerId, entered)` | Publish once per successful roster change, never on refusal or an identical retry. |
| Start sequence | `RaceStartSignal(sessionId, sequence, kind, scheduledGameTime)` for warning, preparatory and gun | Derive thresholds from `PhaseAt` and authored rule values. Events describe scheduled boundaries, not a render timestamp. |
| Progress / end | `RaceEntrantChanged`, `RaceCompleted`, `RaceCancelled`, carrying ids and value outcomes | Publish after a complete model tick or explicit lifecycle transition. No mutable arrays or model references cross Core. |

Request ids are caller-supplied opaque retry keys scoped to a session and operation.
The same id with the same payload returns the original result; reuse with a changed
payload refuses. Two requests with different ids cannot enter the same racer or bind
one boat twice. Validate the roster revision before mutation. A session id is a
transient deterministic key from world seed, club/course identity, scheduled gun and
an explicit in-memory occurrence ordinal; it is not a save identity or a wall-clock GUID.

Entry checks use the authoritative local interaction/boat binding, not a position or
hull id asserted by UI. Require available clock, course, wind, sailing hull/polar,
authored handicap and valid starting samples. Check scene/region and binding
lifetime through composition. Return specific failures such as `RosterClosed`,
`StaleRevision`, `BoatAlreadyBound`, `MissingContent`, `ClockHeld` or `InvalidTrack`.
Do not introduce a membership fee, access price or new unlock in this slice.

Registration closes at the warning boundary. Snapshot roster order, course/rules,
ratings, seed, wind inputs and initial observations together before publishing warning.
Choose ordinal racer id order once so hierarchy traversal never determines pair order.
Initial observations share one time no later than the gun. If a complete valid roster
cannot be constructed, cancel preparation and publish no successful start sequence.
Do not accept entrants after the gun or initialize an entrant without gun-time evidence.

The coordinator schedules the gun far enough ahead to include the entire authored
warning/preparatory sequence. Additional registration lead, entrant limits and an
overall race timeout are future integration data requiring explicit authoring, not
new hard-coded defaults or edits to current Def values.

After a committed advance, enumerate every crossed start threshold in chronological
order, even if several fit within one permitted tick. Equality belongs to the new
phase. Persist each emitted sequence ordinal only in the running coordinator;
duplicate pumps emit nothing. Invalid jumps cancel before signals or scoring. Signals
do not drive phase state: a listener exception cannot cancel the gun or score twice.
No audible or visible signal implementation is part of this plan.

## Deterministic wind adapter

Implement a plain Environment-owned `RaceWindSource : IRaceWindSource`, constructed
with a copied `WindProfile` and positive finite `secondsPerHour`. App already composes
Environment and Boats and supplies the adapter through Core. Boats must not import
Environment; Environment needs only its existing Core dependency.

Its entire wind law is the existing
`WeatherModel.SampleWind(gameTime, worldSeed, secondsPerHour, profile)`.
Use the same double calculation as `EnvironmentService.Sample`:
`config.SecondsPerDay / 24.0`. Do not substitute the float `SecondsPerHour` property,
the test fixture's scale, a race-local wind randomizer, `TimeScale`, `FlowSeconds`
or a `GameServices.Clock` fallback. Vectors point downwind in world metres/second;
do not invert to a meteorological bearing or convert the returned speed to knots.

Snapshot the resolved active-region profile at preparation, then keep it immutable
for the race. Reject preparation if the profile/config is unavailable, or if the dev
weather override is active. A profile, clock-scale, seed or region change during a
race cancels it; do not let boats sail one wind while their judges use another.
Normal `TimeScale` changes only how fast absolute time arrives, not the wind law.
Wind is recomputed, never saved or sampled from future live environment state.

Test exact vector equality with the existing weather law across seeds and times,
out-of-order queries, negative finite times permitted by the seam, profile isolation
and clock-scale changes. Also compare with the live service at the same instant with
dev override off and identical inputs. Missing/nonfinite data must fail explicitly.

## Slow-tick Boats coordinator

There is no shared slow-tick event in the inspected clock/Core APIs. Add a Boats-owned
plain coordinator with an injected clock/time pump and track sources; a small future
MonoBehaviour may pump it. Existing ambient fleet wall-time throttles are scheduling
examples, not the race simulation clock.

Use an authored common simulation step `h`. Initially require all selected incremental
NPC profiles to declare exactly that step; refuse incompatible profiles rather than
retuning them or mixing partial `Skipper.Tick` calls. Require `h` within course track
gap limits and route movement within maximum segment limits. Derive lattice times as
`gun + integerTick * (double)h`, avoiding accumulated float delta time. Include the gun
boundary explicitly and hold reference NPCs at their configured starts before it.

Capture actual player/physical NPC observations separately from heavy adjudication.
App must establish observation order after clock/physics advancement; never tick
`GameClock` twice. Retain timestamped world position, bow vector and velocity in a
bounded preallocated ring. Repeated timestamp observations do not advance the race.
Physical boats can turn between samples, so cadence must satisfy time, distance and
turn-heading bounds; an adapter cannot infer a curved path from distant endpoints.

For each due lattice time supported by all track buffers:

1. Obtain simultaneous observations in frozen roster order. Interpolate only between
   real bracketing observations within the course bounds; never extrapolate. NPC
   reference mode uses one complete authored step, not random-access `At` replay.
   Finished/retired entries remain in the required batch with their last committed
   pose and zero velocity; they cannot acquire new progress or contacts. Their
   departed source cannot stall the remaining active roster.
2. Validate the complete batch, clock continuity, wind and any turn-heading limits
   before committing progress. Do not subdivide an invalid sparse segment merely to
   make validation pass. Reuse the model's straight-segment interpretation.
3. Evaluate navigation from committed authoritative session state. Before introducing
   shared navigation, resolve the two-authority blocker above. Stage runner updates
   so a later failure cannot leave one NPC a tick ahead of the committed roster.
4. Call `SailRaceSession.Tick` once with the complete batch and wind at that time.
   The session applies contact/turn rules and gate progression. Commit a fresh read
   snapshot, then emit only changed status/start/end notifications in stable order.
5. Cache results when `Complete` first becomes true. Build/sort results outside the
   hot tick, publish completion once, and reuse the immutable result view on queries.

The adapter's committed tick advances only on success. If staging/validation cannot
complete, invalidate the pending batch without success events; a running model fault
cancels the session instead of continuing with partially advanced skippers. No such
exception is converted into a finish, skipped mark or automatic successful turn.
Contact adjudication remains sampled; it does not infer swept collisions between
observations. Ordinary physical collision/avoidance safety remains necessary.

Pump work is capped by authored maximum steps per pump and buffer capacity. Retain a
due tick for later processing only while actual observations remain available and
valid. If backlog exceeds capacity, or a clock jump lacks intervening player motion,
cancel explicitly. Never reconstruct a player's path by replaying an NPC route.
Race timeout may retire unfinished entrants by an explicitly authored policy; a
clock/data failure cancels the event without producing standings or season points.

Normal pause performs no duplicate ticks or signals. Entry while the hour is held
is refused; entering a held-hour state during the race cancels it because physical
motion may continue at frozen `TotalSeconds`. Backwards seek, load/new-game, region
unload, boat replacement and unsupported fast-forward cancel once and release all
runtime bindings. Ordinary player withdrawal becomes retirement. The whole race is
transient: loading does not resume it and no course, clock or result bytes are saved.

## Physical integration boundary and cost

First implement/test the coordinator using synthetic track sources only. A later
scene integration must use actual physics observations for **all** participants,
including NPCs. Never assign skipper positions directly to a Rigidbody/transform,
teleport a boat through a mark, suspend grounding or bypass ordinary sail controls.
The reference skipper omits current, grounding, collisions and physical acceleration;
its successful replay is not proof that a hull can sail a real region safely.

Before scene work, the owning scene/art lanes must supply safe course/depth review,
buoy/support assets and visible contact/rounding proof. Entry/turn/start/results
presentation and NPC avoidance need their owning lanes. This plan supplies no
greybox, rendering flag, authored content, engine-use rule or accepted look.

Preallocate roster, motion, pair-contact, navigation staging and observation buffers.
Reuse the existing session's zero-allocation tick discipline. Contact work is
quadratic in entrant count; navigation is bounded by heading-search count. Choose
entrant/pump/buffer limits from measured workloads and authored data. Report actual
step time, peak backlog and bytes after warmup, with calibrated allocation controls;
do not claim a performance budget from the isolated replay alone.

## Delivery and proof gates

| Order / owner | Small reviewable delivery | Required proof before the next delivery |
| --- | --- | --- |
| 1 — lead-architect, Boats | Core value-only entry/query/signal contracts and plain roster/start coordinator; no scene | Exact refusal/no-mutation and retry tests, freeze/equality boundary tests, stable ordering, missing-listener behavior and one notification per commit. |
| 2 — Environment, App composition | Immutable implementation of existing `IRaceWindSource`, injected through Core | Exact weather-law equality and scale/profile/seed isolation; compile existing assembly graph unchanged. No weather tuning. |
| 3 — Boats | Navigation/session authority repair and synthetic slow-tick adapter | NPC owed-turn/mark divergence case; actual turn completion before new gate credit; simultaneous roster and atomic fault cases; no lost or duplicated ticks/signals. Preserve original race assertions. |
| 4 — Boats, qa-test | Buffering, lifecycle and resource limits | Same outcomes under different pump grouping; sparse samples/jumps refuse; pause/held-hour/load/unload cancellation; timeout versus fault distinction; measured allocation/time bounds. |
| 5 — scene, art, UI owners | Separately authorized playable club integration | Native import/serialization plus safe physical sailing, correct wind, no NPC teleportation, real buoy contact/support plates, entry/start/turn/finish feedback. No pixel rebaseline by this lane. |

Regression suites to retain: `SailRaceCourseTests`, `SailRaceTrackTests`,
`SailRaceRoundingTests`, `SailRaceRightOfWayTests`, `SailRaceNpcTests`,
`YachtRaceClubTests`, `YachtRaceSkipperTests`, `YachtRaceAllocationTests`,
`SailDriveTests`, `ModuleBoundaryTests` and identity/content guards if later Defs
are authored. Native runs must report discovered/executed counts per fixture;
source compilation and managed facades do not prove native lifecycle or a playable club.

Verification for this document: inspected the linked model, skipper, wind, clock,
controller and event sources; checked relative links and symbol references; reviewed
the diff for code, art, tuning and save changes. No gameplay test, Unity editor,
scene proof, performance measurement or current model fix is claimed by this plan.
