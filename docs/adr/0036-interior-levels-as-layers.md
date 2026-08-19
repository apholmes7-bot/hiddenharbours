# ADR 0036 — A second storey is a second interior LAYER on one footprint

**Status:** Accepted · **Date:** 2026-08-18 · **Deciders:** lead-architect (direction given in the
2026-08-18 cabin-upstairs handoff), world-content (this PR) · **Supersedes nothing** ·
**Related:** ADR 0032 (sorting band rebase), the owner's 2026-07-30 seamless-interiors ruling

## Context

The owner's 2026-08-18 ruling gives Ginny's cottage a **second level**: her bedroom and the
player's, with the player able to **save at their own bed**. This is the first authored player bed
in the game, and the first building with more than one storey.

The constraint that shapes everything is the owner's 2026-07-30 ruling, which is canon and not
negotiable: interiors are **seamless** (no scene load, no separate screen, no camera cut, no input
mode change) and **true to the footprint** (a cottage interior is cottage-sized, because felt size
*is* the progression fantasy). A second storey has to keep both.

`BuildingInterior` — the runtime half of the interiors pilot — models exactly **one** interior
volume per building. It holds a shell renderer, a room renderer and a furniture root, and swaps
between shell and room on a pure geometric test (`InteriorFootprint.Contains`) of where the occupant
is standing. There was no notion of a level anywhere in the system.

Three mechanisms were on the table:

1. **A pocket room elsewhere in the scene**, entered by teleport. Explicitly overruled by the
   handoff, and rightly: it breaks seamlessness, it needs a camera cut to hide the jump, and it
   makes "upstairs" a different *place* rather than a different part of the same house. It also
   duplicates the footprint, so the two can drift.
2. **A second `BuildingInterior`** stacked on the same building, each owning its own footprint,
   walls and threshold. Honest, but it puts **two** definitions of "inside this building" in the
   scene, and they have to agree forever.
3. **One `BuildingInterior` with a level**, owning both storeys' renderers and roots.

## Decision

**Option 3. A storey is a LAYER, not a place.** `BuildingInterior` gains an optional upper level —
a second room renderer, a second furniture root, and a third root for the colliders that exist only
upstairs — plus an integer `Level`. Exactly one storey is drawn at a time; going up hides the ground
floor and shows the storey above.

Four properties follow, and each is a bug that did not have to be written:

- **The inside test does not change at all.** There is one footprint, one threshold, one set of
  walls and one definition of being in this building, shared by both storeys. True-to-footprint is
  not merely preserved — it is structurally unable to fail, because there is only one footprint.
- **Nothing moves when you change storey.** The stairwell stands at the **same footprint
  coordinate** on both levels, so the occupant is already standing where the other storey's stair
  is. No teleport, no camera cut, no spawn point. This is the whole reason the layer swap is cheap,
  and it is why the builder places the two stair fixtures from one shared model coordinate rather
  than two.
- **Y-sort is never asked to rank the two storeys.** The storey you are not on is switched **off**,
  not sorted behind. There is never a frame in which a bed upstairs and a table downstairs are both
  drawn and have to be ranked, so the band (ADR 0032) is not asked a question it has no answer to.
- **The level resets when the occupant leaves.** You cannot normally walk out while upstairs, but a
  spawn, a region hop or a dev teleport can all cross the threshold without using the door — and a
  house that remembered "upstairs" would open on its bedroom the next time you walked in.

### The cheaper seam the audit found

The handoff proposed that the upper layer carry **its own wall colliders**. The audit rejected that
as more than is needed. The house's four walls are the *same* walls on both storeys — same
footprint, same thickness, same positions — and they are already standing and already always-on. The
only thing genuinely different about the upper storey's boundary is the **front doorway**: downstairs
it is a gap you walk through, upstairs there is nothing outside it but air.

So the upper level adds **one quad** to close that gap, plus the plan's partitions. The plug is
derived by `InteriorFootprint.DoorwayPlugQuad` from the same `gap0`/`gap1` arithmetic that
`WallQuads` uses to *cut* the gap, so the two cannot disagree about where the door is — which
matters, because two of the three shops have measured off-centre doors and a plug that assumed the
middle would seal a wall and leave the doorway open.

### The plan is keyed by SITE, not by build

Ginny's cottage and the village's pilot cottage are the **same build** (`sageCottage`, reused
deliberately). Hanging the upstairs off the room key would therefore have given the village cottage
an upstairs too — two beds, a wardrobe, and the player able to save in a stranger's bedroom.
`StPetersInteriors.Stand` takes an optional **plan key**, and only Ginny's site passes one.

### The manual save

The player's bed is the game's first **manual** save; every write until now was an autosave on
suspend/quit or the shell's own. The bed publishes a Core signal (`RestSaveRequested`) and stops —
it never touches the save system. `RestSaveResponder` answers it, asks `GameServices.Save` to write,
and reports the outcome as `GameSaved`.

The bed could legally have called `GameServices.Save.Save()` itself; `ISaveService` is a Core
interface and rule 4 would be satisfied. The split is because the **request** and the **write** have
different futures: the beat around the write (a fade now, the `Fisher_sleep` animation when the
owner's sheet lands, a clock move to morning after that) belongs to whoever is presenting the game,
not to a bed and not to the save service. It also makes the bed EditMode-testable with no save
service in the scene at all.

## Consequences

**Good.**
- One footprint, one threshold, one inside test — for any number of storeys.
- No scene load, no camera cut, no input-mode change: the 2026-07-30 ruling holds unchanged.
- `Level` is an `int`, not a `bool`, so a third storey or a loft is one more rung rather than a
  rewrite. Only 0 and 1 are reachable today and `TryGoToLevel` refuses anything else.
- The storey you are not on un-registers its own fixtures from `Interactables` (its root is
  inactive, so `OnDisable` runs). The press cannot reach a bed through a ceiling — not because the
  resolver is careful, but because the candidate is not in the room.
- Every building that does not declare an upper level is byte-for-byte unaffected: all three
  references stay null and `HasUpperLevel` is false.

**Costs and limits, stated plainly.**
- **The greybox upstairs draws the storey below's room sheet**, because the footprint is identical
  so the floor and far walls are already the right size. That sheet has a front door drawn in it,
  and upstairs that door goes nowhere. It is plugged solid so you cannot walk out of it, but it is
  still *drawn*. This is the one honest ugliness, and it is the art lane's to fix: an `upstairs`
  room bake named in `UpperLevelPlan.RoomKey` replaces it without touching a line of the builder.
- **The interior mask / roof-off reveal was not extended.** It keys off the shell, which is hidden
  for both storeys alike, so it behaves correctly today by not distinguishing them. A per-level
  reveal (seeing the upstairs from outside) is not in scope and is not needed until the upper storey
  has art of its own.
- **NPC routines do not yet know about levels.** Giving Ginny her upstairs bedroom as a night
  station would need the routine engine — one pure function of the clock — to path to a station on a
  storey that is currently switched off, which is not cheap. Logged as the follow-up; her existing
  blocks and the #551 commute maths are untouched.
- **The save does not record where the player is.** `SaveData` has no position or region field (it
  carries money, clock, flags, gear, fleet, and so on). Sleeping at the bed therefore persists
  everything the game already persists, and a reload lands the player wherever the shell's start
  policy puts them — *not* at the bed. Making "wake where you slept" true is a save-format change
  (schema v11 → v12) and belongs to whoever adds player position, not to this PR.
