# Golf logic foundation

This implements the requested minigame as isolated logic and content under World.
It serves P1 through the simulation's wind and P3 through recreation on the coast.
It adds no component, scene, prefab, input binding, HUD, payment, inventory or save
field. A normal game never constructs a round yet. The existing golfer routines
keep their previous behaviour.

## Reused work and content identity

The earlier golf implementation and `heron-view-golf.md` establish the course's
stations, visitors, walks and hidden transfers. `CumberlandCourse` reads the ground
plan to build those walks. That code has no ball, swing, score or playable hole
model; this change neither replaces nor edits it.

The new assets retain the plan's `course.cmb`, `region.cumberland` and
`hole.cmb_1` through `hole.cmb_4`, including their number, par, tee and green
coordinates. Pond ids and `stream.cmb_brook` also remain source ids. New club,
surface and rule ids use `golf_club.*`, `golf_surface.*` and `golf_rules.*`.
New bunker references use `golf_hazard.cmb_bunker_01` through `_09`; the plan's
bunker triples have no ids to reuse. All new Def types and asset identities are
added to the append-only identity ledger; existing records are preserved.

Only four source holes have both endpoints. Hole 5 has no green and hole 18 has
no tee. No endpoint, off-frame hole or full eighteen-hole course is invented.
`GolfRound` accepts an ordered set of unique holes from one course and region;
these assets can form a four-hole practice round. No catalog or boot code loads it.

All positions, distances and slopes are in **ground metres**, x east and y north.
These are the plan coordinates, before the existing isometric y projection. Aim
is a direction in that same frame. A future view must apply the existing Core
projection once when drawing a result, and convert pointer input back to ground
coordinates before creating a swing.

## Authored data and snapshots

Each hole, club, surface and rule set is one ScriptableObject asset. There are
four holes, four clubs, six surfaces and one practice rule set. The initial
numbers are editable starting values for isolated practice logic, not accepted
gameplay balance. `Build()` validates ids, finite numbers, positive simulation
budgets, surface kinds, friction, simple polygons and tee/cup lies, and produces
snapshots. Editing an asset or its polygon arrays cannot mutate a built round.

The hole has boundary, tee, fairway, green and hazard polygons, plus a rough
surface outside its mown areas. Both polygon windings work; edges count as inside.
Water takes precedence over bunker, then green, tee, fairway and rough. Outside
the hole boundary is out of bounds. Crossing narrow water on the ground is a
penalty even when a full integration step would land beyond it.

The practice polygons are derived from the committed ground plan and surface
rules: the course area is the boundary; fairways are mitered corridors with the
source line and full width; tees use the source 4.5 m radius; greens use the
source 13 m radius. Source ponds, bunkers and the variable-width brook become
polygons, with circles/ellipses sampled at 32 vertices. Each hole holds a snapshot
of all thirteen course hazards so off-fairway shots can still encounter them.
These are gameplay footprints; they do not author, sample or change terrain art.

The first data uses the source green elevation as a **flat logic plane**. The
height model supports a constant slope per hole, with height equal to elevation
plus the dot product of slope and ground position. It does not claim to match
the rendered terrain's elevations, pond shore pixels, or every fairway edge.
Before scene integration, ground sampling and collision agreement need their own
review; a later adapter must consume a Core ground contract rather than read an
art module's concrete classes. No terrain texture was read or modified here.

## Swing, flight and roll

The caller supplies a club, positive power up to one, and a nonzero aim vector.
Power is raised to the club's authored exponent, then scales launch speed; the
starting lie applies its launch multiplier. Loft separates horizontal and
vertical velocity. A putter's zero loft starts directly on the ground.

`GolfShotContext` carries world seed, double game seconds, round shot index and
the Core `EnvironmentSample.WindVector` captured at that instant. The vector is
held for this short shot. The model does not create weather, use wall time, read
global randomness or look up a current environment service. The future caller
must capture the sample at the supplied game time. Club aim spread uses an
explicit integer hash of seed, all game-time bits, shot index and stable club id;
zero spread gives exact aim. The same state, data and intent replay identically
on the same runtime. Cross-platform floating-point lockstep is not promised.

Flight integrates gravity, horizontal drag and wind acceleration. First terrain
contact is interpolated inside the step; surface landing retention and restitution
control runout and further bounces. An airborne ball can cross water and the
course boundary; the landing determines its lie. There is no mesh collision,
spin, drag lift, gust resampling, tree collision or rendered ball in this model.

Rolling uses downhill gravity and surface friction. A stationary ball holds only
when static friction can resist the slope. Braking stops rather than reversing
the ball. Motion splits at polygon edges to resolve surface changes; the cup is
checked along the rolling segment rather than only at sample positions. A ball
captures only on the green within the authored radius and maximum speed. A fast
putt can pass over it. Geometry tolerances and hash constants are numerical
implementation constants; gameplay and simulation-budget numbers live in Defs.

The solver is called once for a shot intent, with bounded steps, travel per step
and elapsed time. It returns first landing, peak height, flight time, final lie,
position, status and executed-step count. Reaching either budget returns
`Unresolved`; it never invents a resting position or successful score. There is
no per-frame callback, trajectory history allocation or new background service.
This synchronous API is an isolated foundation; frame-budget measurements and
an incremental presentation adapter belong to the later integration task.

## Round rules

An accepted shot counts one stroke. Water and out-of-bounds each add their
authored penalty and use **stroke and distance**: the next shot starts at the
previous playable position. The result still reports the hazard contact. There
is no lateral drop search, ball item, ball purchase, booking, reward or payment.
This is a rule of the isolated round, not a scene teleport or goods transfer.

Only a holed result advances to the next tee. Per-hole strokes include penalties;
the running total includes the current hole, while score to par uses completed
holes only. Completed rounds reject further shots. A stale shot index, invalid
intent, arithmetic overflow or unresolved solve cannot consume a stroke, change
the ball position or advance the round. There is deliberately no persistence
contract: a round exists only as an explicitly constructed in-memory object.

## Verification and next integration boundary

`GolfSimulationTests` checks analytic flat stopping distance and zero-drag carry,
peak and flight time; power scaling; repeatability and seed/time/shot sensitivity;
headwind, tailwind and crosswind; slope, friction, lies and bounce; continuous
water/cup checks; airborne penalties; time/iteration guards; snapshots; invalid
data; penalties; multi-hole scoring and atomic failure. `GolfContentTests` checks
all fifteen assets, the four source holes' stable ids and coordinates, and the
absence of fabricated endpoints. A 144-shot sweep across four holes, four clubs,
three power levels and three wind samples requires every authored practice shot
to resolve; it also guards against an edge intersection rounding back to the
current position and repeatedly clipping without spatial progress.
Existing assertions and visual expectations are
unchanged. Local source-host results are described separately from Unity results
in the PR; source facades are not an editor test-run result.

Future integration needs its own authorization and proof: how a player starts
and leaves a round, a Core ground-sampling seam, instrument/input decisions,
physical club/ball handling, and ball/swing presentation reviewed by an art lane.
Any decision to retain a round across loads requires a separate save design.
