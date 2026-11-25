# Room fixture reach

Room fixture selection and final dispatch now check the architectural walls of
the floor the fixture belongs to. A nearby fixture across a partition or on
another storey cannot win the press. The existing inclusive reach distance,
facing filter, priority and id ordering remain unchanged.

This builds on the parent-based floor ownership introduced by the fixture-facing
fix: `BuildingInterior.FurnishedLevelOf` identifies the floor, rather than an XY
overlap with the storey above. No geometry, placement or appearance is changed.

`IRoomFixtureReach` is the Core selection/dispatch contract. `IRoomReachSource`
lets a building answer fixtures from other modules without a concrete World
dependency. `RoomReach` tests closed polygon intersections as plain data. A
touching corner, collinear overlap, start/end in a wall, nonfinite coordinate or
unknown floor refuses the press. An open doorway remains open.

The building adapter reads the existing `Walls`, `WallsGround` and `WallsUpper`
groups, including their current polygon paths, transforms, offsets and enabled
flags. It does not query physics or add/modify colliders. Furniture geometry is
excluded. Floor ownership selects architectural groups even when the room's art
has hidden its furniture root while the player stands outside. Missing groups
or an unsupported solid collider refuse rather than assume an empty room.

Buildings register while enabled. An exterior fixture also checks building walls
along its segment; this includes the older freezer key path, whose fixture is
outside the cottage hierarchy. The range check supplies the same actor position
to that spatial check. A fixture placed inside a building without floor ownership
refuses. Stop-washing and get-up remain local release actions.

Existing `InteractVerbTests` and `RoomFixtureFacingTests` are extended. The latter
uses every applicable room-plan row and its current partition data, including
planned rows that are not installed yet. `RoomReachTests` covers polygon edges,
real component wiring, live wall changes, doorway/outdoor cases and unchanged
distance boundaries. The upstairs-bed fixture now supplies actual wall polygons
and a clear bedside stance before asserting its wake storey. The chooser case
supplies two candidates before closing the selected fixture's reach. These
premises preserve the geometry and chooser contracts exposed by the prior
automated failures. Main's storey-reporting lifecycle from #1543 is retained.
No local test or player build ran for this update; fresh automated execution is
pending. These tests do not claim visual acceptance.

The adapter scans architectural polygons with reused lists: work is proportional
to the walls and vertices considered per eligible fixture. Exterior queries first
reject buildings outside the segment bounds. No tuning value or saved field is
added. Cabin bunks and merchant conversation paths retain their existing policies;
their separate scope and doorway classification remain in the broader
[wall reach proposal](fixture-wall-reach-proposal.md).
