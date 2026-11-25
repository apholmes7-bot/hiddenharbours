# Benthic movement arithmetic

Status: inactive logic foundation. No scene, species asset, presenter, capture consumer or save
path calls this model. It supplements the living-fish design in draft #1044 without modifying
the school field or its data owned by draft #1041. P3: a living working coast.

`BenthicMovement.TrySample` is a pure query of world seed, stable occupant id, double Core game
time, immutable profile and a certified straight bed segment. A lobster-style walker rests at
its shelter entrance, walks to the other end, rests, walks home and repeats. A crab-style
scuttler uses the same bounded route with distinct scuttling/resting activity. An attached clam
always returns its burrow coordinate. These are motion modes supplied by future species data,
not a hard-coded species-id table. No biological speeds or durations are asserted here.

## Support and time

The segment includes ground-plane XY and actual bed elevation at each endpoint. Its caller
must certify continuous traversable support along the **entire straight 3D segment**. Interpolating
two valid endpoints alone cannot prove support across a rock, hollow or shoreline. Unknown
support rejects the query. The model never substitutes the conservative school-patch depth,
current water level, or an arbitrary zero-height plane. Position is the substrate contact point;
body clearance, sprite projection and facing belong to a later reviewed consumer.

For segment length L and supplied speed v, travel duration is L/v. The cycle is home rest,
outward travel, away rest, return travel. Its period is `homeRest + 2*L/v + awayRest`. The existing
stable integer hash of seed and occupant id selects a permanent phase; it is not redrawn on
ticks, days or queries. Double time is reduced modulo the period before phase is added.
Negative times wrap consistently. Travel fraction is linear in each direction, and position
is continuous at rests, turns and cycle wrap; speed along the sloped segment is bounded by v.
Velocity direction may change instantaneously: acceleration/turn animation is outside this slice.

Every speed and duration is an explicit input, with no shipping default. Seconds mean the
existing compressed Core game clock. The future species Def adapter must expose and review
these inputs before activation. There is no wall-clock sampling, global RNG or mutable phase.
Invalid/nonfinite inputs, unknown modes, unusable routes and overflowing periods fail closed.

## Activation remains separate

This does not certify geography, assign populations, change tide exposure, respond to bait,
reserve a catch, or retreat in response to a new disturbance. Do not replace the current route
mid-cycle and claim continuity: connected multi-segment paths, changing support and shelter
selection require their own bounded transition proof. Future world adapters communicate through
reviewed Core contracts. Clam depletion, hooked individuals and carried goods need explicit
transaction state in their later design; background movement is recomputed, never saved.

EditMode tests cover exact shelter/walk/rest durations, crab scuttling, fixed clams over 1,000
seeds, replay and query-order independence at long times, subsecond precision, supported sloped
positions, cycle/turn continuity, speed bounds, invalid inputs and zero allocation on warmed
queries. These are arithmetic proofs, not visual or terrain-integration approval.
