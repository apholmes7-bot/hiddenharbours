# Yacht-club racing: isolated logic

The requested play is: "Sailboat racing from yacht club. Players sail around designated buoys with sailing mechanics and gameplay" (2026-11-12). This change supplies the course data and deterministic adjudication for that play, serving P1 through wind-dependent sailing and P3 through club competitors. It activates nothing in a region. Entry interaction, physical buoys, boat controllers, NPC figures, input, HUD, prizes and persistence need separately scoped integration work.

## Existing sailing reused

Read before implementation: `Core/Boats/SailDrive.cs`, `SailPolarDef.cs`, `BoatKinematics.cs`, `Core/Environment/TelltaleMath.cs`, `Boats/BoatHullDef.cs`, `BoatController.ApplySailDrive`, and [sail controls](sail-controls.md). The new classes live inside Boats, whose existing assembly references Core. No assembly edge is added.

- `SailRaceNpcDef.Hull` uses the existing sailing hull's `SailPolar` and `NoGoTrueWindDeg`. Neither hull nor polar is edited. The hull's threshold, rather than the polar's reference threshold or the sprite's apparent-wind threshold, is the same choice the controller makes.
- `SailDrive.ResolveWind` and `TargetSpeedKn` supply true-wind angle and polar speed; `ToMetresPerSecond` supplies the unit conversion. Core's apparent-wind and bearing calculations remain authoritative. Wind vectors point **downwind** in world metres/second. Compass bearings run clockwise from north.
- Shipped sloop polars retain their `Reference` provenance. Reusing them does not claim measured performance. The NPC model follows their kinematic speed targets; it does not duplicate Rigidbody thrust, hull drag, yaw, currents, manual trim or rig animation.

## Data and authoring contract

`SailRaceCourseDef` is one course entity per future asset, with stable `racecourse.snake_case` identity. Its ordered `RaceMark` values are course parts with unique course-local `racemark.snake_case` ids, not independent buoy visuals. `SailRaceNpcDef` is one competitor profile per future asset (`racenpc.snake_case`); an entrant has a separate stable `racer.snake_case` id. There are no authored race assets in this change. Numeric fields intentionally require authoring rather than silently choosing balance for a live club.

| Data | Meaning and validation |
| --- | --- |
| Start / finish | Directed finite segments A to B. Cross from their right side to their left; endpoints count. The normal points in legal travel direction. A zero-length line is invalid. |
| Ordered marks | Centre, entry and exit radial bearings, rounding side, inner buoy clearance, outer rounding-zone radius. Port rounds anticlockwise; starboard clockwise. Each sweep is greater than zero and at most 180 degrees; complex courses use additional marks. |
| Laps | Positive count; all ordered marks and a legal finish crossing are required on every lap. |
| Warning / preparatory seconds | Positive warning lead; preparatory lead is nonnegative and strictly shorter. The externally supplied gun time is absolute simulation time. |
| Track bounds | Maximum time gap and segment length. Non-increasing, too sparse or discontinuous tracks are rejected before any progress changes. The caller must sample actual boat motion within these limits. |
| Contact / release metres | Contact threshold and a strictly wider release radius, preventing repeated penalties while boats remain together. |
| Right-of-way rules | Aft overtaking cone half-angle (0 to 90 degrees exclusive), calm-wind threshold, nonnegative time penalty. These are simplified game rules, not a sporting rulebook implementation. |
| NPC profile | Hull, starting position/bearing, fixed tick seconds, replay horizon, heading-search count, seeded skill speed range in (0,1], line clearance, rounding lane fraction, arc step and gate overshoot. Route chords must clear the buoy, and ticks must fit the course's sample bounds. |

Building a course validates data and copies its mark array. Inspector changes cannot move gates inside an existing course snapshot. A competitor profile and its referenced polar are read-only inputs for a race; changing a polar during replay requires constructing a new race. This is not save data and no identity of existing content is changed. The Def identity ledger only registers the two new types, without adding content identities or weakening the ledger guard.

## Decisions made for this first model

These are lane decisions within the requested logic scope, not claims of additional owner rulings.

1. **Start and recall.** Phases are before warning, warning, preparatory and racing. At the gun, interpolate the boat position on the supplied track. A boat strictly beyond the supporting start line is over early. It must return strictly behind that line, around an end if desired, then legally cross the finite line. A pre-gun excursion corrected before the gun is clear. Exactly on the line at the gun is permitted. A boundary point needs a later sample on the positive side to establish a crossing, credited at the interpolated boundary time.
2. **Rounding.** Only the next ordered mark can count. Cross its directed radial entry gate, stay within the outer circle and clear the inner circle along every straight track segment, then cross its directed exit gate with the required net signed angular sweep. Returning through the entry, cutting the buoy or leaving the zone cancels the attempt. Tacking within the zone may briefly reverse angular motion; it must still complete the prescribed net rounding. A straight chord through the buoy never earns credit. Same-segment events are consumed in temporal order, using only the remainder after the previous event. Samples describe straight segments; unsampled curved motion cannot be inferred.
3. **Timing.** All entrants score elapsed time from the gun, including late starts and recalls. Start time is recorded separately. Each lap duration starts at the gun or the preceding finish crossing. Final time is interpolated at the last required finish. Corrected time adds penalties without altering track, lap or raw finish times. Future standings should order finished entrants by corrected seconds, with ordinal entrant id as the deterministic tie-break; unfinished entrants have no numeric finish result.
4. **Simple right of way.** An overtaker closing from the authored aft cone keeps clear first. Otherwise port tack gives way to starboard, then windward gives way to leeward on the same tack. Calm, head-to-wind, dead-astern or geometrically indeterminate cases receive no invented entitlement. A penalty is assigned only on a sampled contact episode at or after the gun, once until separation exceeds the release radius. Pair order is irrelevant; observations cannot go backwards; applying the same contact twice is idempotent. Events after an entrant's finish do not count. The observer requires simultaneous, sufficiently frequent boat snapshots; it does not infer swept collisions, protests, mark-room, rule changes during contact or collision damage.
5. **NPC replay.** Anchor fixed ticks at the gun and sample a caller-provided pure `(worldSeed, absoluteGameTime) -> windVector` function at each tick. A stable ordinal hash of entrant id and world seed chooses a skill speed fraction and tie order; no global RNG or wall clock is read. Follow sailable course legs directly. In the true-wind no-go, search headings for positive velocity toward the waypoint using the same polar. Retry a missed mark or finish approach rather than skipping it. Partial-time queries interpolate the same full tick, and finished boats freeze at the interpolated finish position. Query order does not affect replay. Before the gun the boat holds position; in calm it makes no polar-driven progress.

## Integration seam and limits

Create a validated course snapshot, register each entrant with a track sample **at or before the gun**, then call `Advance` with chronological game-time samples. This intentionally refuses joining a replay with no evidence of where the boat was at the gun. Send eligible contact results to `ApplyPenalty`. `PhaseAt` is available for a later start signal publisher. No EventBus, service locator, static global or MonoBehaviour is needed for the isolated model.

NPC queries replay from the race origin within the authored horizon. This is a bounded reference model, allocating one entrant/result per query, with work proportional to elapsed fixed ticks and, in the no-go, heading-search count. It is not intended for a render-loop call. A future Boats-owned slow-tick adapter can cache the same trajectory without changing sampling; a cross-module consumer must receive a Core contract. Race entry, a wind-at-time adapter, physical avoidance, grounding/current safety, real hull steering and rendering are future integration work. Do not attach the reference navigation directly to a scene boat. An impossible course or prolonged adverse weather may remain unfinished; the horizon rejects an unbounded replay rather than teleporting it through a mark.

No scene, prefab, input map, UI, sprite, material, light, terrain, rig, animation, price, existing tuning or save format changes. All new behavior is unreachable until separately wired.

## Proof to run

The five `SailRace*Tests` EditMode fixtures cover authored validation, phase boundaries, exact gun crossings and recalls, finite-line endpoints, ordered marks, both rounding sides, compass wrap, a half-turn, shortcuts, tacks, laps, interpolated timing, subdivision equivalence, rejection without mutation, pair-order rules, contact deduplication and corrected time. NPC tests cover calm, pre-gun holding, seeded pace, clock-varying wind, partial-tick/query-order determinism, post-finish freezing, invalid data and direct use of the main sail drive. Both shipped sloop polars must finish the fixture course under eight wind bearings; no polar or expectation is retuned to pass.

Native Def creation and asset loading require Unity EditMode. Managed harness results must be reported separately; they do not prove Unity import, serialization, object lifecycle or a playable club. Existing `SailDriveTests`, `ModuleBoundaryTests` and `DefIdentityContractTests` are the regression checks. No pixel proof is required because nothing is instantiated or connected to presentation.
