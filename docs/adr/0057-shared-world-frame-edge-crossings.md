# ADR 0057 — One world frame: the whole wall is a door, and doors do not teleport

- **Status:** Proposed (2026-11-15).
- **Date:** 2026-11-15
- **Decision owner:** owner; technical steward lead-architect. Review: gameplay-systems (the walker and
  the boat), world-content (region extents and scenes), qa-test (the rewritten crossing tests).
- **Serves:** P1 Sea Has Moods and P5 Cozy but with Teeth (distance at sea is real: no crossing is a
  shortcut that saves fuel or time), P3 Living Working Coast (the coast reads as one place).
- **Related:** ADR 0004 (scene per region, loaded additively: unchanged), ADR 0003 (the layout is data).
  The layout table is [`docs/architecture/world-frame.md`](../architecture/world-frame.md). The audit that
  motivated it: the 2026-11-15 world picture and doors audit (findings D1 to D10).

## Context

The owner ruled, verbatim: *"Shouldn't move to wharf. If you enter via water you should enter the new
scene at the same location you exited the last. Doors do not teleport. ... Fix the 70-90m jump. ...
Landings need to be the full vertical or horizontal position of the scene. The whole wall is a door. Fix
south water and east water to respect these rules. Add missing door/walls for every region that has an
intersection. Fix NMC scene doors."* He also said he would have one big scene if he could, and that he
wants no shortcuts that save fuel and time.

Regions were joined by keyed `RegionPassage` trigger bands. Each band stood about 24 m inside the edge and
landed the mover at one authored point about 40 m beyond the far edge, so every crossing jumped 70 to 90 m,
and up to 260 m sideways. A walk over the bar also moved the boat (D1); Nine Mile Creek had a band to the
Coddle Cove greybox with no way back (D2); South Water and East Water had bands on the wrong edges and
landings that did not face their exits (D3, D4, D6); Cumberland had no way in (D5); and an authored berth
overrode the way in (D8).

## Decision

1. **A shared world frame, as data.** Each `RegionDef` gains `WorldOrigin`, the world position of its
   local (0, 0). Scenes are not moved or rebuilt; the frame only says where each region's local frame sits.
   A `WorldMap` asset under `Resources` lists the regions that make up the world. The pure Core
   `WorldFrame` builds seams, rejects overlaps and answers `Resolve`.
2. **The whole wall is a door.** In a listed region, every edge stretch shared with a neighbour is a
   crossing, and every other edge is a boundary. The persistent `RegionTravelCoordinator` checks the
   mover each physics step and, once it is `RegionCrossingHysteresisMetres` (GameConfig, 1 m) past a shared
   edge, travels to the neighbour and translates the mover by `from.WorldOrigin - to.WorldOrigin`. Position
   along the edge, heading and velocity are unchanged. Re-triggering is stopped by that hysteresis band,
   never by moving anyone. A scene's own closing walls must not stand in the way: once per region scene
   the coordinator disables every static solid lying wholly beyond a SHARED edge
   (`WorldFrame.StandsDownOnSharedEdge`); boundary edges keep theirs.
3. **Keyed bands stand down** in listed regions (`RegionPassage.StandsDownForWorldFrame`). Two exceptions:
   no frame at all (fixtures, unlisted regions) and the arrival opening, whose scripted sea leg keeps its
   authored door until it is re-cut.
4. **Doors do not teleport.** A foot crossing moves only the walker. The boat stays where she was left, in
   her own region (`BoatRegionId`), and is hidden until the player is back there. The legacy keyed path
   also stops moving the boat for a named arrival that has no boat point (the bar), so the old intro path
   obeys the same rule.
5. **No free distance or time.** The clock is untouched by a crossing and runs through the load; the mover
   keeps its physics through the load and the shift applies to wherever it is when the neighbour
   activates.
6. **A Core contract for where things are.** `Whereabouts` (region id plus local position and heading) and
   `IWorldWhereabouts` (`Player`, `Boat`, `RestoreBoat`), published on `GameServices.Whereabouts`, so the
   save and sleep work can record and restore positions without touching travel internals.

## Consequences

- No 70 to 90 m jump and no sideways jump: the seams line up by construction, and `WorldLayoutTests`
  holds every seam to the same-world-point rule both ways.
- D8 (the berth overriding the way in) cannot happen on an edge crossing, which never consults a berth.
- D2 is closed: the Coddle Cove band is refused in the creek, and the greybox is off the map.
- Tests that pinned the old keyed landings were rewritten deliberately (`PerPassageArrivalTests`,
  `PerPassageArrivalPlayTests`, `RicePointCrossingPlayTests`).
- The keyed `RegionPassage` objects remain in the scenes, inert in listed regions. Removing them is a
  later editor cleanup.

## Follow-ups (not in this decision's first PR)

- **South Water flush under everything** (the owner's "fix south water"): extend Nine Mile Creek and Rice
  Point 180 m south and East Water 40 m south, then move South Water to origin (60, -1060). That changes
  scene extents, so it needs an editor rebake.
- **Cumberland's x alignment** against the creek's north beach is assumed centred and must be checked in
  the editor.
- **Driving** (a vehicle on a road) is not yet a crossing mover; a vehicle is held by the scene as before.
- **Tide-gated foot crossings** (D10): with the bar now an edge, its access rule is no longer applied to an
  edge crossing. Whether the tide still closes the bar on foot is the walker's own wading rule; a separate
  gate is not added here.
