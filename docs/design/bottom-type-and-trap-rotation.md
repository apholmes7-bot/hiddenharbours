# Bottom type and trap rotation

Status: logic proposal; no scene, rendering, save or existing content change.
Baseline: the main commit pinned for this charter. Serves P1, P3 and P4.

## What exists

| Concern | Classes and data on main | Limit |
| --- | --- | --- |
| Depth | Core `ITidalTerrain.ElevationAt`, `IEnvironmentService.WaterLevelAt`, `TidalExposure.WaterDepth`; World `TidalTerrain`, `PaintedTidalTerrain`, `RectTidalTerrain`, `MainlandTidalTerrain`; painted height maps and seabed assets including `NineMileCreekSeabed` and `StPetersSeabed` | Height is authoritative for wetness, grounding and depth, but does not say rock, mud or sand. |
| Painted surface | Art `TerrainSplatSurface`; `NineMileCreekSplatA` through `F`; ground plans and `ShoreLineDerivation.Substrate` | Visual channels include sand, shingle, shelf, silt, talus, ledge and mud. These are rendering/authoring inputs, not a published gameplay bottom query. Do not read GPU colour as biology or change the paint. |
| Named grounds | Fishing `FishingGroundDef`: rectangle, depth range, `EcologyHabitat Bottom`, hardness and fish ids. `SouthWaterShelf` is sand, `SouthWaterDeepMud` mud, `SouthWaterBank` gravel/rock. `FishEcologyDef` is reference information only. | A ground-wide label is not a position map; overlapping rectangles and compound habitat flags do not decide one bottom at a point. Keep existing trawl content unchanged. |
| Trap placement | `TrapPlacement.CanPlaceAt` samples the shared terrain and tide and enforces `TrapDef.MinSoakDepthMeters`. `LobsterTrap` and `CrabTrap` already exist. | No substrate preference. Existing placement checks a minimum depth; a new reset planner must additionally respect its supplied bounds and depth window. |
| Catch and deck work | `TrapSoak`, `TrapFill.Fill01` and `ResolveCount`, `PlacedTrapCatch.ResolveMany`, then `DeckPot`. `TrapFill` preserves a ready-pot floor of one animal; further slots fill monotonically. Species and animal streams are stable per placement and index. | No position/bottom input to the current fill curve. Preserve readiness, capacity, indexed animal seeds, bait, species gates and physical deck handling. |
| NPC plans | `NpcPotDayDef`, `NpcPotDay`, `NpcPotDayCatch.Build`, `NpcPotDayLibrary`; `NmcArsenaultLeoPotDay`, `NmcDoironYvettePotDay`, `NmcGallantMariePotDay`. `NpcPotDay` derives a daily string from start/bearing/spacing and seeded `DailyShiftMeters`. App `NpcPotBoats` and `NpcPotDeck` present the work. | Daily shift is a bounded random offset along an authored string, not an assessment of recent catch. `NpcPotDayCatch.Build` calls the same fill, resolver and deck rules as player pots. Do not modify the App presentation or fleet plan assets here. |
| Spawn data | World `RegionDef.SpawnFishIds`; Fishing `FishSpeciesDef` region, season, tide, hour, gear and `SpawnWeight`; `FishSpeciesRegistry`, `CatchResolver` and `FishSchoolModel` | `NineMileCreek` has an empty local spawn list; `StPeters` includes `fish.lobster` and `fish.rock_crab`. The pot plans use their explicit `CatchRegionId`. Shellfish do not become swimming schools. No substrate query exists. |
| Sounder | Core `DepthSounder.DisplayDepth`, `SounderPrefs`; UI `SounderOverlayHost`; the shared terrain and tide provide depth. | No bottom material reading. Helm-instruments PR 1123 remains a draft outside this baseline. Extend its model only after it is on main. |

Sources: [terrain contract](../../Assets/_Project/Code/Core/Environment/ITidalTerrain.cs),
[paint channels](../../Assets/_Project/Code/Art/TerrainSplatSurface.cs),
[ground schema](../../Assets/_Project/Code/Fishing/FishingGroundDef.cs),
[NPC catch composition](../../Assets/_Project/Code/Fishing/NpcPotDayCatch.cs),
[fleet plan](../../Assets/_Project/Code/Fishing/NpcPotDayDef.cs),
[spawn schema](../../Assets/_Project/Code/World/RegionDef.cs),
[sounder](../../Assets/_Project/Code/Core/Boats/DepthSounder.cs).

## One bottom query

Add one Core contract, `IBottomMap.BottomAt(Vector2 worldPosition)`, returning exactly one
of `Unknown`, `Rock`, `Mud`, `Sand`. Seed and tuning are fixed at construction. Bottom is
geology: it depends on `(worldSeed, position)`, never on season, haul history, query order,
wall time or a global RNG. Invalid positions return `Unknown`; they are never good ground.

The first implementation is an engine-light, deterministic cell field. A Core
`BottomMapDef` holds a stable id, cell size, origin, salt and rock/mud/sand weights.
Construction validates and copies tuning into a value snapshot. Cell coordinates use
floor, including for negative positions, and a documented integer hash chooses the
material. There is no texture, terrain asset, scene or renderer involved. This is an
explicit proposed simulation map, not a claim that existing painted rocks match it.

Spawn weighting, trap evaluation and the later sounder must receive the SAME instance
through this Core interface. They must not grow separate noise functions, colour
classifiers or hand-maintained tables. A later authored provider can implement this same
interface after terrain ownership and visual agreement are resolved. Do not register or
activate the proposal in a shipped scene until that agreement and composition are reviewed.
Existing `FishingGroundDef.Bottom` remains legacy trawl metadata; adoption must reconcile
it with the shared query rather than consult it as a second authoritative map.

## Habitat and migration as data

One new `TrapHabitatDef` per species names its existing species id and independent
rock/mud/sand affinities, migration centre, travel vector, radius, period in game seconds,
phase and off-centre density floor. Lobster favours rock; crab favours mud for this game
rule. These are gameplay preferences, not a new biological claim about every crab species.
Do not edit the existing lobster or rock-crab species Defs or any existing Def numbers.

The seasonal centre follows a smooth periodic displacement from absolute game time;
the world seed and stable profile id select a reproducible phase. Density is habitat
affinity times distance falloff around that moving centre. A season can move the best
patch while geology remains fixed. All consumers use this same density evaluator.
Game-time periods are explicit data supplied from the calendar, never real-time seconds.

For a single-species pot, a new Fishing adapter supplies that density to the existing
`TrapFill` curve, reducing only additional-slot fill. Evaluate density at placement time
so a trap cannot lose an already filled animal when the season changes during its soak.
The existing ready floor, full-capacity ceiling and per-animal catch streams stay intact.
The adapter then invokes `PlacedTrapCatch.ResolveMany` and yields the ordinary per-animal
catch items for `DeckPot`; it does not teleport goods, land value or invent a new roller.
Mixed-species pots need separately reviewed weighting; reject them in this first adapter
rather than bias a combined pool incorrectly. Haul resolution may allocate like the
existing resolver; the planner's slow tick must allocate zero bytes after setup.

This is callable logic only. Production adoption seams are `NpcPotDayCatch.Build` and
the placed-pot haul caller; NPC movement adoption is `NpcPotDay` scheduling. No call site
is silently enabled, no terrain/World/App class is edited, and no fish is added to a
swimming school. Existing aggregate catch is a legacy boundary: this work preserves its
per-animal indexed outcome but does not claim persistent individual population ownership,
depletion, occupant transactions or save migration. Those remain explicit activation gates.

## Rotation is a work order

The pure planner takes placed-trap snapshots with stable instance ids, positions and
recent catch per unit effort; caller-supplied candidate reset positions; a region rectangle;
the shared bottom map, shared elevation source, water level at the planning instant and
absolute game time; a species profile; and planner tuning plus a caller-owned output buffer.
Candidates should come from surveyed reachable water, not an unbounded search over the world.

In stable input order it considers poor traps with valid history, scores legal candidates
with the shared density function, and emits only a strict improvement over current density
and the configured minimum gain. Missing history keeps a trap in place. Reject dry land,
unknown bottom, nonfinite inputs, out-of-bounds positions, wrong depth, excessive travel,
occupied positions and resets too close to another trap or an earlier order. Equal scores
use candidate order; capacity limits stop output without allocating. Never mutate placement
facts, spend bait or credit a catch while planning.

An NPC consumes orders by travelling, hauling, working the individual animals on deck,
carrying the empty pot, baiting and setting it at the new position. The planner is not a
teleport or a replacement fleet schedule. Route safety, berth ownership, dynamic obstacles
and the full low-water passage remain the existing navigation/composition responsibility;
valid endpoints alone do not certify a safe voyage.

## Proof and adoption gates

EditMode tests must name and prove repeatable bottom/density/plans, opposite lobster/crab
preferences through actual fill counts, a changed best ground across a season, bounded wet
reset orders, retained good/unknown-history traps, deterministic tie handling, spacing,
invalid-input rejection and zero allocated bytes for repeated successful slow ticks.
Run existing fill/placement/deck regression tests as available; report exact executed counts.
Managed compiler/test execution is no-editor proof, never a Unity or player-build claim.

New crab species are proposals with unused ids only, one future species per file; none
enters a registry, spawn pool, trap, save, market or art library in this change. Activation
requires content validation, individual ownership policy, shared-map composition and
the reviewed NPC work route. The helm sounder extension waits for PR 1123 on main.
