# One tow session: logic and scene handoff

Status: unwired, engine-light proposal for P1, P2, P4 and P5. No scene, prefab,
input, hull definition, fish asset, save or rendering path constructs this model.
It extends the [existing trawl foundation](trawl-fishing-logic.md), with no second
catch formula or population table.

`TrawlSession` composes `TrawlOperation`, `TrawlTowSolver` and `TrawlDeckBatch`:
`GearReady → Shooting → Towing → Hauling → CatchOnDeck → Sorted → Stowed`.
Each work boundary needs an explicit command. Sorting never grants hold contents.
Even a known empty codend passes the sorting acknowledgement before completion.

`TrawlSessionDef` is a serializable configuration schema with no live defaults.
It supplies draught, minimum keel clearance, planned warp/depth ratio and door
spread, snag hazard per hard metre, shallow chafe per metre, carry time per
individual, initial integrity and increasing weight grade boundaries. Gear and
handling windows, durations, capacities and budgets remain in their existing
Defs. Authoring/identity registration and accepted tuning are future gates.
Grade is a representative lot-weight category, not an individual size, price,
legal retention rule or calibrated regional fishery claim.

The caller passes an explicitly seeded `System.Random`. At construction it
provides one solver seed and one snag threshold draw; no tick calls any RNG.
The production adapter must derive that stream from world seed, stable tow id
and start game time, and pin its algorithm for replay. The existing solver
continues to derive its species/school streams from that supplied seed and
the track's start time. There is no wall clock, service lookup or global RNG.

## Ground and telemetry

`TrawlSessionGround.FromDefinition` copies the existing `FishingGroundDef`:
region/id, rectangle, mean-tide depth range, bottom, hardness and fish ids.
The South Water's existing Shelf, Deep Mud and Bank assets are consumed without
modification. Their declared fish ids restrict the survey; they do not generate
fish, authorize trawling or substitute for a complete living-school query.
The real survey still supplies existing `TrawlPopulationSample` records,
reviewed eligibility, canonical-time depth/season and complete swept coverage.
Unknown ground, mismatched hardness, unlisted species and incomplete coverage
fail closed; no plausible partial catch is published.

Each towing input supplies absolute game time, speed over ground, depth under
keel, sea state and **actual net centre**, in the ground's metre frame. Draught
converts keel clearance to water-column depth for the warp check. Mean-tide
asset depths are not silently treated as current tide-corrected measurements.
The driver supplies the exact scheduled shooting-boundary snapshot, even if a
render update arrives later. Ticks ingest chronological straight net segments;
conditions in each input describe its preceding segment. A ground's convex
rectangle must contain the whole net-width sweep. Crossing a ground boundary
needs a separate certified tow. New ticks beyond the configured duration or
preallocated track-knot budget are refused without changing the history.

Shooting/hauling expose timed warp payout/recovery; towing keeps the paid length
fixed. Too little clearance refuses shooting. An unsafe warp/depth ratio raises
a haul instruction and collapses the predicted spread. Speed outside the tow
window also collapses the spread recorded in the solver track. The solver
independently validates actual net motion and its existing fishing windows.

On bottom contact, hard-ground distance integrates snag hazard. A setup draw
selects an exponential cumulative-hazard threshold; the track stops at its
crossing, so schools beyond a snag cannot enter the codend. An immediate snag
produces an empty codend without inventing elapsed tow time. Snag probability
and a bounded chafe warning are outputs. Excess warp in shallow water adds to
that warning. This shallow warning is advisory, not a second damage solver;
the existing solver alone determines committed hard-bottom integrity loss.
Replay is exact for the same seed, configuration, survey and input sequence.
Varying telemetry partitions across changing conditions is not certified as
equivalent; canonical survey bins remain the existing solver's responsibility.

## What the scene half needs

- Winches and the net drum consume phase and warp length, and confirm actual
  supported deployment/retrieval before issuing the completion commands.
- Warps and doors need physical attachment/support geometry, the actual net
  trail and explicit interruption/recovery handling. Predicted spread and warp
  length are logic outputs, not permission to suspend a boat or teleport gear.
- Crew read the next task: shoot, pay out, tend, recover a snag, haul, sort,
  carry, or make room in the hold. Sorting uses the existing timed hand ledger;
  bycatch stays present until explicitly kept or released.
- Species, representative weight grade, bycatch and counts remain on deck.
  `ConfirmHandStow` records an explicit hand-placement acknowledgement only,
  at the configured work rate and within the supplied free hold kilograms.
  Full storage refuses the acknowledgement without losing any deck catch.
  The scene adapter must validate and commit a supported Core transfer first;
  these counters grant no inventory, ownership, money or depletion rights.

Future World, Boats, crew and storage adapters must communicate through Core
interfaces. This change introduces no feature-to-feature dependency or new
persistence schema. Live occupant identity/depletion, supported storage sprites,
transaction retry semantics, gear recovery, content eligibility and art remain
separate implementation/review gates before activation.

`TrawlSessionTests` reads all three actual South Water ground assets and covers
full sessions, seed/input replay, shallow refusal, full storage, snag cutoffs,
speed/warp warnings, manual conservation, unknown survey data and work budgets.
The allocation case calibrates its byte counter, warms the path, then requires
exactly 4,096 successful towing ticks and exactly zero measured bytes. A stubbed
counter explicitly skips rather than passing. Setup, haul-time batch resolution
and result/deck construction allocate; only towing ingestion claims zero bytes.
