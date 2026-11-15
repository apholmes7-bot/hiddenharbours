# The shared world frame (ADR 0057)

Owner ruling, 2026-11-15: *"If you enter via water you should enter the new scene at the same location
you exited the last. Doors do not teleport. ... The whole wall is a door."* And earlier: *"I want to avoid
shortcuts for the player ... I would have one massive scene if possible."*

The regions stay separate scenes, loaded additively (ADR 0004), each built around its own local origin.
The frame places those local frames in **one world**, so a shared edge is a crossing and a mover who
passes it comes out at the **same world point** on the other side.

## The data

| Piece | Where | What |
|---|---|---|
| `RegionDef.WorldOrigin` | `Assets/_Project/Code/World/RegionDef.cs` | World position of the region's local (0, 0). The only new number per region. |
| `RegionDef.Frame` | same | `RegionFrame(Id, WorldOrigin, WorldCenter ± WorldSizeMeters/2)`. |
| `WorldMap` asset | `Assets/_Project/Data/Resources/WorldMap.asset` | The list of regions that make up the world. **Membership is the switch:** a listed region's edges are crossings and its keyed `RegionPassage` bands stand down. |
| `WorldFrame` | `Assets/_Project/Code/Core/World/WorldFrame.cs` | Pure registry and geometry: seams, overlaps, `Resolve` (Stay / Cross / Boundary). |
| `GameConfig.RegionCrossingHysteresisMetres` | `Assets/_Project/Code/Core/Config/GameConfig.cs` | How far past an edge a mover must be before the crossing fires (default 1 m). |

## The layout (shipped)

All regions have `WorldCenter = (0, 0)`, so local x/y range is ±size/2. World range = local range + origin.

| Region | Id | WorldOrigin | Size (m) | World x | World y |
|---|---|---|---|---|---|
| Nine Mile Creek | `region.nine_mile_creek` | (0, 0) | 760 x 560 | -380 .. 380 | -280 .. 280 |
| St Peters | `region.st_peters` | (760, -200) | 760 x 520 | 380 .. 1140 | -460 .. 60 |
| Rice Point | `region.rice_point` | (-760, -100) | 760 x 360 | -1140 .. -380 | -280 .. 80 |
| East Water | `region.east_water` | (1520, -160) | 760 x 520 | 1140 .. 1900 | -420 .. 100 |
| Cumberland | `region.cumberland` | (0, 560) | 760 x 560 | -380 .. 380 | 280 .. 840 |
| South Water | `region.south_water` | (-820, -880) | 2400 x 1200 | -2020 .. 380 | -1480 .. -280 |

Off the map (legacy keyed behaviour where anything still reaches them): `region.coddle_cove` (the M0
greybox) and `region.west_water` (unbuilt).

### Seams (every one is a crossing on both sides; `WorldLayoutTests` pins this list exactly)

| Pair | Edge | World stretch | Length |
|---|---|---|---|
| Nine Mile Creek / St Peters | x = 380 | y -280 .. 60 | 340 m |
| Nine Mile Creek / Rice Point | x = -380 | y -280 .. 80 | 360 m |
| Nine Mile Creek / Cumberland | y = 280 | x -380 .. 380 | 760 m |
| Nine Mile Creek / South Water | y = -280 | x -380 .. 380 | 760 m |
| Rice Point / South Water | y = -280 | x -1140 .. -380 | 760 m |
| St Peters / East Water | x = 1140 | y -420 .. 60 | 480 m |
| St Peters / South Water | x = 380 | y -460 .. -280 | 180 m |

### Boundaries (edges facing nothing: the mover is held at the edge, outward speed removed)

- Nine Mile Creek east, y 60 .. 280 (above St Peters).
- Rice Point west and north; Cumberland west, east and north.
- St Peters south, x 380 .. 1140 (South Water ends at x 380; see the follow-up below).
- St Peters north, East Water west y 60 .. 100, East Water east, north and south.
- South Water west and south.

### Alignment kept

- **The tidal bar:** the creek's bar end (352, -200) and St Peters' (-356, 0) both sit at world y -200.
- **Rice Point's strand and sea door** keep their y: Rice Point's origin y is -100, so its local y 0 is
  world y -100.
- **Cumberland's x** is assumed centred on the creek. Whether its roads meet the creek's north beach at
  the same x is an editor check (see the ADR's follow-ups).

### Follow-up (needs the editor, not in this PR)

The owner asked for South Water to run flush under every region it faces. Today it touches the creek,
Rice Point and a 180 m stretch of St Peters' west edge, but St Peters' and East Water's south edges face
nothing (180 m and 140 m gaps). The fix: extend the creek and Rice Point 180 m south and East Water 40 m
south, then set South Water's origin to (60, -1060) so it spans x -1140 .. 1260 with its top at y -460.
That changes extents, so the scenes' terrain, water and bounds must be rebaked in the editor.

## The crossing

Each `FixedUpdate`, the persistent `RegionTravelCoordinator` asks the frame about the one mover: the
walker when on foot, the boat when aboard or on deck. `Resolve(region, localPosition, hysteresis)`:

- **Stay** inside the rectangle, or less than the hysteresis past an edge.
- **Cross** further past an edge into a neighbour: the coordinator loads the neighbour (`RegionSceneLoader.Travel`,
  no arrival key) and, when it becomes active, adds `LocalShift = from.WorldOrigin - to.WorldOrigin` to
  whatever moved. That is a pure translation, so the position along the edge, the heading and the
  velocity are all unchanged; the camera is shifted by the same amount.
- **Boundary** past an edge that faces nothing: the mover is held at the edge.

**The closing walls open on seams.** The land template (`LandSceneTemplate.AddClosedEdges`) closes every
region with a solid laid flush outside each edge (`ClosedEdges`, Rice Point and Cumberland). Left on, the
east and south walls held Rice Point's walker 0.35 m (his foot radius) and the dory 2 m (half her hull)
inside the edge, so no crossing, which fires past the edge plus the hysteresis, could ever happen. Once per
region scene the coordinator stands down every static, non-trigger collider that lies wholly beyond an edge
that has a neighbour (`WorldFrame.StandsDownOnSharedEdge`, `RegionTravelCoordinator.OpenSharedEdges`).
A boundary edge keeps its wall; where an edge is only partly shared, the Boundary hold above keeps the
unshared stretch. Nothing in a scene file changes, and a region off the map keeps every wall.

**No bounce, no jump.** The arrival is at the same world point, which is already at least the hysteresis
inside the neighbour; going back needs a genuine move of twice that. Nothing is displaced to stop a re-fire.

**Doors do not teleport.** A foot crossing moves the walker only. The boat stays at her position in the
region she was left in, and is hidden while the player is elsewhere ("the boat is in another region"
means `RegionTravelCoordinator.BoatRegionId` names a region other than the current one; she is shown
again, untouched, when the player comes back). A boat crossing moves the boat; the crew aboard rides
with her as her child and is not shifted twice.

**No free distance, no free time.** The game clock is not touched by a crossing and keeps running during
the load. The mover keeps its physics during the async load and the shift is applied to wherever it is
when the neighbour activates, so the distance on the far side is exactly the distance covered, and fuel
keeps burning in the boat's own update. There is no landing point to skip ahead to.

## The contract for other systems

`Assets/_Project/Code/Core/World/Whereabouts.cs`:

- `struct Whereabouts { string RegionId; float X; float Y; float HeadingDegrees; }`: a position in that
  region's LOCAL frame. An empty `RegionId` means unknown. `TryGetWorld(WorldFrame, out Vector2)` converts.
- `interface IWorldWhereabouts { Whereabouts Player; Whereabouts Boat; void RestoreBoat(Whereabouts); }`,
  published on `GameServices.Whereabouts` by the travel coordinator. The sleep and save lane reads the
  player and boat from here and puts the boat back with `RestoreBoat`.
