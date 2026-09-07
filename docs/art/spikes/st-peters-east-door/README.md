# St Peters — the east door

The wall the game opens through. The owner ruled the intro out in the water east of St Peters
(2026-09-06) and ruled that water a real, returnable place rather than a set; this is the seam
that makes it reachable.

![the east door](east-door-map.png)

## What the plate is

A top-down map of St Peters' region with the new east sea door on it, **drawn from geometry
measured out of a live editor** — an EditMode dump read the builder's own members and the
committed `RegionDef`, and the renderer drew from that JSON. Nothing on it is a number re-typed
into a script: a map drawn from a second transcription would agree with itself and prove nothing.

The seabed strip along the bottom is a real sweep of `TidalTerrain.ElevationAt` along the door's
own latitude, west → east — the bed the game evaluates, not an assumption about it.

## What it is for

The door is not the west door mirrored. The west sea door was sited by what it had to **avoid**
(the bar, the reef apron, ground that bares); this one is sited by what it has to **catch**. The
entrance channel's landfall is the seaward mark of the only marked route in or out of the harbour,
so every boat that leaves or comes home crosses the wall on that latitude — and the door stands on
it, read off the channel rather than re-typed.

The plate is what makes that judgeable at a glance: the door against the fairway, the arrival
inboard of the landfall and inside the marked water, and the depth she floats in when she gets
there.

## What is NOT shown

The east water itself. `region.east_water` ships here as a committed `RegionDef` only — the scene,
its fish and its deep bed are the next PR's, and the deep rungs wait on an owner ruling (the
visible-fish depth rule wants 12.5 m and 37.5 m; the deepest water in the shipped world is 6 m).

Shot for PR #764 (`feat/st-peters-east-door`).
