# ADR 0036 — A second storey is a second interior LAYER on one footprint

**Status:** Accepted · **Date:** 2026-08-18 · **Amended 2026-08-23** (see
*[Amendment — the layer is drawn a storey up, and the player walks there](#amendment-2026-08-23--the-layer-is-drawn-a-storey-up-and-the-player-walks-there)*)
· **Deciders:** lead-architect (direction given in the 2026-08-18 cabin-upstairs handoff),
world-content (this PR); the amendment: the owner's 2026-08-23 ruling · **Supersedes nothing** ·
**Related:** ADR 0032 (sorting band rebase), ADR 0037 (rest anchor), the owner's 2026-07-30
seamless-interiors ruling

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
  than two. **⚠️ AMENDED 2026-08-23** — the same footprint *coordinate*, yes, and that half still
  holds; but the storeys are now drawn a storey apart, so that one coordinate is a real distance up
  the screen and the player **walks** it. See the amendment at the foot of this file.
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
  policy puts them — *not* at the bed. Making "wake where you slept" true needs a **save-schema
  bump** and belongs to whoever adds player position, not to this PR. (Do not assume the next
  version number is free: the wardrobe PR #573 already claims v12 for `WornOutfitId`.)

---

## Amendment (2026-08-23) — the layer is drawn a storey up, and the player walks there

**Status:** Accepted · **Decider:** the owner's 2026-08-23 ruling · **Amends** the Decision above;
everything not restated here stands unchanged.

### The defect

A storey was a layer, and the layer went up at the building's **own transform**. Both room sheets,
both furniture roots and the whole upper collider set were placed at the same position, so the
bedroom drew **pixel-for-pixel over the kitchen**. Nothing threw. Nothing sorted wrong. The
colliders were all in the right place *for a room at that position*. The only symptom was that going
upstairs did not look like going anywhere — the classic silent failure this repo keeps writing tests
for.

The layer model was right. What was missing is that a storey has a **height**, and that ADR 0036's
"nothing moves" — true and load-bearing for the *swap* — was quietly extended into "the two storeys
are drawn at the same place", which nothing had ever ruled.

### The ruling

1. **The upper level draws at its true Y**: the house rig's **declared storey height**, projected at
   the shared ¾ camera.
2. **The player walks up** — a short position-driven path along the stair footprint, with the gait
   **held**.
3. **The upper level's sheet joins ADR 0032's Y-sort band** (2…2402) rather than sitting under it.

### 1. Where the storey is — read from the rig, never typed

`interiorIsoRig` now **declares** `storeyZ` — this storey's ceiling plus `0.34 m` of joists, the same
allowance `shopInteriorRig` already uses for the flat above a shop, so both building families stack
their storeys identically. The bake reads it (`anchors().storeyZ`), writes it into the interiors
contract as `storeyHeightMetres`, and the placement reads it back. **The sage cottage's floors are
3.1025 m apart**, and no line of C# says so.

This follows the offset law the kit already lives by: the room that stands under exterior facing `f`
is interior facing `f + 4`, and that **4 is measured at bake time and written into the contract**
rather than typed into a builder. A storey height typed into C# is the same mistake one storey up —
it goes quietly wrong the first time a rig moves a ceiling, and it goes wrong *silently*.

**A height is not a depth.** Under the shared 40° camera one metre of northward GROUND travel draws
`sin 40° ≈ 0.643` world units up the screen; one metre of HEIGHT draws `cos 40° ≈ 0.766`. Run the
storey height through the ground squash and the bedroom sits 19% low; use the raw metres and it sits
31% high. Both read as "the art is a bit off". `InteriorLevelLayout.UpperLevelY` is the **one** place
the projection happens (`IsoGround.HeightScale`, pinned to `SpriteLightMath.HeightScale` by
`IsoGroundTests`) — the cottage's 3.1025 m becomes **2.38 world units** up the screen.

`InteriorLevelLayout.DrawsOverGroundFloor` is the guard, and `BuildingInterior.ConfigureUpperLevel`
**logs an error** when a storey is stood at the ground floor's own Y — including the 0 that a
contract baked before `storeyHeightMetres` existed reports. It still stands the storey: a stale
contract should cost the owner a re-bake and a red line in the console, not Ginny's upstairs.

### 2. The inside test now knows which floor it is asking about

This is the consequence that had to be paid for, and it is paid in one place. `BuildingInterior`
tests containment against **`FootprintFor(Level)`** — the same rectangle, the same walls, the same
threshold, lifted by the storey you are standing on. ADR 0036's "one footprint, one threshold, one
definition of being in this building" is unchanged in kind: there is still exactly one rectangle,
and it is still derived from one set of numbers.

`Footprint` itself still answers the **ground floor** at every level, because that is what "this
building's footprint" means to everyone outside these walls — an NPC routine pathing to the door, a
placement test — and none of them should have to know which storey the player is on.

The rest anchor (ADR 0037) follows the same rule: a bed upstairs stands one storey up, so its
recorded position does too, and `WakeAt` tests it against the footprint of the storey the anchor
*names*. The one-shot is now also **refused** if the body is not actually standing on the storey it
names — a stale anchor draws a bedroom around a player standing in the kitchen otherwise.

### 3. The climb

`StairTraversal` is a plain object with a playhead: foot → head, eased with a **monotonic**
`SmoothStep` (an overshooting ease puts a body through the bedroom ceiling and drops it back), over
`GameConfig.StairClimbSeconds` (**0.5 s** ruled; **0 is legal** and is exactly the instant swap this
mechanism had before — a real accessibility answer for anyone who does not want the camera moved for
them).

- **The storey swaps on the frame of the press; only the body takes the half second.** `Level`, the
  interact registry, the colliders and the drawn room are all true when `TryGoToLevel` returns. The
  climb is presentation and never state, so an interrupted climb leaves a consistent house behind it.
- **`BuildingInterior` drives the walk, not `InteriorStair`.** The stair's own root is switched off
  the instant the level changes — that is what takes it out of `Interactables` — so a climb driven
  from the fixture would stop dead on the frame it started.
- **⚠️ The gait is HELD, in `Update`.** `IsoCharacterSprite` picks its gait by measuring position
  deltas in `LateUpdate`. A climb writes ~2.4 world units of **rise** in half a second, so a measured
  walker reads ~5 m/s and draws a flat sprint *on the spot* — the fisher is not covering ground, she
  is going up. `StairTraversal.GaitSpeedMetresPerSecond` is **0**, and 0 is the honest number: the
  rise is height, there is no stride to draw, and nothing in the character kit is baked climbing. The
  hold must be stated **before** the measurement, which means `Update` — a hold applied in
  `LateUpdate` is a frame late every frame. The day there is climb art, this becomes that clip's
  speed and every caller follows it.
- **The walk owns the body only while nothing else does.** A spawn, a region hop or a dev teleport
  all move the occupant; the climb notices it is no longer where it last wrote, hands the body back
  and lets that frame's threshold test run. It also claims the move axis (`MoveActionClaim`) while it
  runs, and gives it back on every exit — arrival, interruption, or the house being disabled.

### 4. Sorting — the upper sheet joins the band

The ground room sheet keeps its fixed `SortingBands.InteriorFloor` (1, under the whole band): it is
buried inside a footprint with nothing drawn over it, and a floor must never out-sort the people
standing on it. The **upper** sheet cannot keep that, because it is now lifted up-screen over ground
that has grass, trees and a dooryard on it — every one of them a four-digit Y-sorted order. At order
1 the meadow behind the house would draw straight through a first-floor bedroom.

So the upper sheet takes a band order (`InteriorLevelLayout.UpperRoomSortingOrder`) computed from its
**far (northmost) edge** — the sheet's lowest order, since order falls as Y rises. Everything
standing anywhere on that floor therefore still outranks it: the guarantee the fixed order gives
downstairs, kept while joining the band. Taking the room's *centre* instead would hide the player
behind their own bedroom floor the moment they walked past the middle of it.

The band itself is untouched, and the property ADR 0036 claimed still holds: **Y-sort is never asked
to rank the two storeys against each other**, because the one you are not on is switched off.

### What this costs

- **The upstairs greybox now draws the storey below's sheet 2.38 units up the screen**, so the
  ground floor's doorway is visible in it at head height. Same honest ugliness as before, in a new
  place, and the same fix: an `upstairs` room bake named in `UpperLevelPlan.RoomKey`.
- **A player standing upstairs is 2.38 world units north of where they'd be downstairs**, in ground
  terms, which is what Y-sorts them correctly against the lifted room. Nothing else in the game reads
  an indoor position for anything but sorting today; when NPC routines learn about levels (still the
  logged follow-up) they will have to learn this at the same time.
- **The two storeys' rectangles OVERLAP in world XY, and that is not fixable.** The cottage lifts
  2.377 units and its own floor is 2.587 units deep on screen, so the middle of the kitchen sits
  **0.028 units** — about one pixel at PPU 32 — inside the bedroom's rectangle. The ¾ view draws
  depth and height on the same screen axis, so any storey shorter than its room is deep will do this.
  It is why the inside test asks *"am I on the storey I am ON"* rather than *"which storey am I in"*:
  the second question has no answer, and a system that asked it would be tuning a hair's-breadth
  margin forever. The one place it can still bite is the rest anchor's one-shot, which spends itself
  only if the body is standing on the storey the anchor names — a body at the far north end of the
  ground floor passes that test. It needs a wake whose restorer did not move the player and an anchor
  from a different storey, and its cost is opening on the wrong floor once; not worth machinery.
- **The owner must re-run the interiors bake and re-stand the region** for the storey height to be in
  the contract. Until then `ConfigureUpperLevel` logs the error above and the upstairs draws where it
  used to.
