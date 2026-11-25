# Small-club yacht racing

This is the logic and authoring contract for the requested yacht racing beside golf.
Reading the wind serves P1; a club with recurring competitors serves P3; recovering from
a missed start or a penalty serves P5. This slice supplies isolated simulation and data.
A playable club, rewards and persistence need later, separately chartered integration.

## What main already has

- [SailDrive](../../Assets/_Project/Code/Core/Boats/SailDrive.cs) resolves true and apparent
  wind, interpolates a polar, applies the hull's true-wind no-go angle and computes trim.
  Wind vectors point **downwind**, in metres per second; sailing angles measure the wind
  **from** direction. Compass headings run clockwise from north. The controller uses these
  Core calculations; racing must use the same conventions, polar and no-go threshold.
- [SailPolarDef](../../Assets/_Project/Code/Core/Boats/SailPolarDef.cs) holds the speed table.
  [Sloop 30](../../Assets/_Project/Data/Boats/Sloop30.asset) and
  [Sloop 88](../../Assets/_Project/Data/Boats/Sloop88.asset) are existing sailing hulls with
  their own polar references and auxiliary engines. Their polar provenance is reference,
  not measured race performance. Existing hull numbers and polars stay authoritative.
  [Sail controls](sail-controls.md) describes the wider helm and station work; the race
  model neither revises those rulings nor enables engines in a race.
- [WeatherModel.SampleWind](../../Assets/_Project/Code/Environment/WeatherModel.cs) already
  derives wind from seed, absolute simulation seconds, clock scale and a regional
  [WindProfileDef](../../Assets/_Project/Code/Core/Environment/WindProfileDef.cs).
  Slow variation, gusts and a shared weather-system channel produce one deterministic
  wind. A race must consume that wind through a Core interface, without a private weather
  generator or a read of today's wind while replaying yesterday's tick.
- [NPC pot days](../../Assets/_Project/Code/Fishing/NpcPotDay.cs) already derive working
  boat routines from seed and clock. [NpcPotBoats](../../Assets/_Project/Code/App/NpcPotBoats.cs)
  is their App presenter, binding Fishing to Boats. Village routines have their own
  schedule/route model. Neither is a race skipper, and neither should be altered to race.
- [Existing race logic](sailboat-race-logic.md) already supplies `SailRaceCourseDef`, a
  warning/preparatory/gun sequence, recall, ordered radial rounding, laps, finite start
  and finish crossings, a contact judge and a bounded NPC replay. It has no club in a
  scene. Its judge covers overtaking, port/starboard and windward/leeward, with an additive
  time penalty. Its `At` replay allocates per query. Extend these tested seams for club
  rules; preserve the existing contact/time-penalty API for its callers.

## The course is data

Reuse one `SailRaceCourseDef` per course, stable `racecourse.snake_case` id. A course owns
its start line, ordered marks, finish line, lap count and sampling/start thresholds.
Marks have unique course-local ids, centre, required port or starboard rounding,
entry/exit radial bearings, buoy clearance and outer zone radius. They are logical
parts of the course, not sprites. Leave a port mark to port by turning anticlockwise;
a starboard rounding is clockwise. The existing validator permits sweeps in (0,180]
degrees; add another ordered mark for a longer authored manoeuvre.

Start and finish are finite, directed A-to-B segments: legal travel is right side to
left side. Every lap requires all marks in order and then a finish crossing. A finish
crossing with an unrounded mark earns nothing. Enter a mark's gate, follow the required
net angular sweep within its annulus and leave its exit gate. Cutting inside clearance,
leaving the zone or returning through the entry resets that attempt. Sampling describes
straight track segments; it cannot prove a curve that was never sampled. Reject gaps,
teleports and reversed time before changing progress.

No real club course, rating or new hull tuning is invented here. New rule/profile Defs
have stable ids, one entity per future asset, validated immutable runtime snapshots,
and no picture attached. Fixture numbers are test examples, not balance decisions.

## Start and race state

The host chooses a gun in absolute game seconds. Authored warning and preparatory leads
produce `BeforeWarning -> Warning -> Preparatory -> Racing`; they are boundaries on the
same clock, not coroutines. Register entrants with a track sample at or before the gun.
At the gun, a boat strictly over the supporting start line must return behind it before
crossing the finite line legally. Crossing early and returning before the gun is clear.
Exactly on the line is legal when the next positive-side sample proves a crossing.

Each entrant is waiting/over early, racing, finished or retired. Elapsed time always
starts at the gun, so a late start costs real time. Crossings interpolate their times.
The session ends when every entrant has finished or retired; a cutoff is a host decision,
not a hidden wall-clock timeout. Unfinished/retired entrants receive no numeric finish.
Retirement and race state are in-memory only, with no save-format change.

## Three club right-of-way rules

These are deliberately simplified game rules, not certification against a sporting
rulebook. A pure pair judge accepts two simultaneous boat states and the shared wind:

1. **Port gives way to starboard.** Use the sign of true wind over the bow, as SailDrive
   does. Calm, head-to-wind and dead-astern samples establish no tack entitlement.
2. **Windward gives way to leeward** on the same established tack, using separation
   projected onto the downwind vector. Equal projection establishes no entitlement.
3. **Room at the mark.** For boats approaching the same next mark on the same lap and
   tack, both inside its authored zone and overlapping along their mean heading, the
   boat farther from the mark must give the inside boat room. Overlap length is authored
   per boat, rather than guessed from a sprite. Equal radius or opposing headings give
   no mark-room entitlement. This local rule takes precedence over windward/leeward;
   opposite tacks retain port/starboard priority. It ends when those conditions cease.

This first mark-room rule uses current geometry, not historical zone-entry rights.
It does not settle protests, obstruction rights or fleet-wide chains of entitlement.
The existing overtaking judge remains available separately; the club judge returns a
named rule and the give-way id, independent of argument order. A session issues one turn
on entry into a contact episode, using the course's contact and wider release radii.
The pure entitlement result alone does not mean a collision happened.

## Penalty: sail a turn

A club rules Def authors the required turn angle (one full turn for the first club),
minimum seconds per turn and maximum observed heading change per sample. A contact adds
one owed turn. Clear a turn by accumulating one continuous signed rotation from successive
heading samples; reversal starts the sweep in the new direction. Time alone never clears
a penalty. A partial swing back and forth never becomes a completed turn.

While turns are owed, track samples still update continuity but cannot earn starts,
marks or finish. A pending rounding is reset. The segment completing the last turn also
earns no progress; the next segment resumes ordinary adjudication. This prevents sailing
a penalty and collecting a mark in the same movement. The real manoeuvre therefore costs
elapsed time, with an authored minimum turn duration preventing a stationary instant spin.
Additional offences queue more turns. No teleport, synthetic finish time or automatic
movement is applied. A motion-only proof cannot establish sea room or prevent a collision;
physical avoidance belongs to the future controller adapter.

## Handicap, results and season

One new handicap Def per hull id holds a strictly positive time multiplier. It does not
edit the hull. Lower corrected time wins: `elapsed seconds * hull multiplier`; any legacy
time penalties are included before multiplication. A faster-rated hull has a larger
multiplier. Example fixture: 100 seconds at 1.2 loses to 110 seconds at 1.0. Actual ratings
require club authoring and review, not claims inferred from reference polars.

Results order finishers by corrected time, then ordinal entrant id for exact ties;
unfinished entrants follow, ordered by id. Publish raw elapsed time, multiplier, corrected
time and status together so a handicap never disguises who crossed first.

The season is a fixed, explicitly ordered set of race ids. A season Def supplies points
by place and a non-finisher score; low total wins, no discards in this slice. Record every
registered entrant exactly once per race; duplicate or incomplete results are rejected.
Only completed races contribute. Equal totals share the same sporting score and sort by
ordinal entrant id for stable display. The table exposes completed-race count. There are
no payouts, purchases, calendar scheduler or persistence in the logic slice.

## NPC skipper and cost

Keep existing course route generation and radial rounding. Add a reusable fixed-tick
runner, allocated once at registration, alongside the bounded `At` reference replay.
All ticks are anchored at the gun and ask a Core wind-at-time interface for `(seed,time)`.
The future App/Environment adapter must delegate to the existing WeatherModel with the
race region's profile and clock scale. No feature assembly references Environment.

Sail a reachable waypoint directly when it is outside the hull's true-wind dead zone.
For an upwind leg, search the existing polar for the two close-hauled headings with best
upwind velocity. The rays through the waypoint are lay lines: stay on a selected tack
until reaching the opposite lay line, then tack toward the waypoint. An authored margin
and minimum tack interval suppress chatter. A header beyond an authored angle can choose
the other tack when it improves progress. Seed and stable entrant id settle symmetric
choices. Never choose a moving heading inside the no-go angle. Calm means no movement.
This is kinematic course choice, not Rigidbody steering, currents or obstacle avoidance.

Setup may allocate course snapshots, routes and arrays. The valid tick path, pair judge,
turn tracker and entrant progression must allocate **zero managed bytes**, including
first contact, first turn and lap completion. Build result/season tables outside ticks.
Fixed inputs and the same sequence of fixed ticks reproduce the same state; arbitrary
query order remains the separate reference replay's contract.

## What the scene lane must author later

- A club location and entry interaction, safe race water with depths for each admitted
  hull, non-conflicting start/finish and marks, actual buoy visuals and grounded supports.
- Course, rules, skipper and hull-rating assets, competitor identities, starting positions,
  schedule and reviewed season points. Check the rounding annuli and sailing routes in
  local water; a mathematical course is not a navigable scene.
- App wiring for the one wind source, clock, real hull track/heading samples, retirement,
  signals, start recall, next mark, owed turn feedback, raw/corrected results and club table.
- NPC physical steering, avoidance and grounding safety; diegetic signals and readable
  boat identities with separately reviewed art. Never drive a live boat by replay teleport.
- Separate decisions for access, entry fees, prizes, engine enforcement, save migration
  and persistence. Goods remain carried by hand; racing introduces no cargo shortcut.

The logic change does not touch a scene, prefab, art, existing Def value or save bytes.
EditMode proofs cover missed marks, each club rule in both directions, turn timing,
handicap reversal, season validation, upwind tacking, headers, shared wind determinism
and zero-byte ticks. Headless managed proofs and compilation must be reported separately
from Unity EditMode and scene acceptance; no editor lease is granted for this work.
