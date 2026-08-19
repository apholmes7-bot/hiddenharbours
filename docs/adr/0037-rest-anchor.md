# ADR 0037 — The save carries a region-stamped, storey-stamped REST ANCHOR

**Status:** Accepted · **Date:** 2026-08-19 · **Deciders:** gameplay-systems (this PR),
lead-architect (save-schema review) · **Supersedes nothing** ·
**Related:** ADR 0008 (save schema & versioning), ADR 0036 (interior levels as layers),
ADR 0004 (scene per region), CLAUDE.md rule 5

## Context

[#574](https://github.com/apholmes7-bot/hiddenharbours/pull/574) gave the player a bed and made
sleeping in it the game's first **manual save** — `InteriorBed` → `RestSaveRequested` →
`RestSaveResponder` → `ISaveService.Save()`. But loading still put the player at the authored dev
spawn, so the moment had no consequence: you went to bed in Ginny's spare room and woke up on the
wharf. The save recorded the *day*; it did not record the *place*.

The obvious fix — "store the player's position" — is wrong twice over, and both are already
established facts in this repo rather than speculation:

1. **A position is meaningless outside its region.** `SaveData` says so in its own comments, and
   `NavWaypointDto` / `PlacedTrapDto` are region-stamped for exactly this reason (ADR 0004: scene
   per region, each with its own world frame).
2. **A position is meaningless outside its storey.** ADR 0036 made a second floor a *layer* over the
   *same footprint*. The game's one authored player bed is upstairs at Ginny's, directly above her
   front room — so a position-only anchor restores the right coordinates into the wrong room, and
   does it silently. That failure looks exactly like the feature working.

## Decision

**The save carries a rest anchor: `(region, storey, x, y)` — all four, always together, and it is
player state, not sim state.**

- **Schema v13** appends four flat fields to `SaveData` (`RestRegion`, `RestLevel`, `RestPosX`,
  `RestPosY`), read and written only through `RestLocker`, which hands them out as one `RestAnchor`.
  `RestRegion == ""` means **has never turned in**, which is a new game, every save older than v13,
  and the fallback for anything unreadable — under it the player wakes at the authored spawn, i.e.
  precisely today's behaviour.
- **The write** happens in `RestSaveResponder`, immediately before `save.Save()`. The bed supplies
  the storey it is standing on and the position of **the actor**, not of itself; Core supplies the
  region from `GameServices.CurrentRegionId`. Each fact comes from whoever actually holds it.
- **The read** happens in `RestWakeRestorer`, a static installer on the `GameLoaded` edge — the
  mirror of the responder, owned by the same `SaveService`. It moves
  `GameServices.PlayerTransform` and publishes `PlayerWokeAt`.
- **The storey is applied by World, not Core.** Core has no idea what a building is, so it states
  where the player now is and `BuildingInterior` answers, opening on the anchored storey via a
  one-shot spent by the next inside-transition.

### Why this is state, not something to recompute (rule 5)

Rule 5 keeps tide, wind and weather out of the save because they are **derivable** from
`(worldSeed, gameTime)`. Where a person chose to go to sleep is derivable from nothing: it is an
authored decision by the player, in the same family as `WornOutfitId` (v12) and `PlacedTraps` (v3).
Storing it is not a rule-5 violation; recomputing it is not possible.

### Why the position is the PLAYER'S, not the bed's

A bed's transform is the middle of its footprint, so waking there is waking *in* the mattress. An
offset from it would be a tuned constant guessing at furniture, partitions and walls the save system
cannot see — a magic number (rule 6) that would need re-tuning for every bed ever placed. The player,
meanwhile, is standing on floor they walked to and is inside the bed's own `ReachMeters` by
construction. So "feet at the bedside" is not computed at all; it is read. It is also the *same*
quantity the wake writes back (the player transform's position), which keeps this off the project's
most-repeated rake: never compute one quantity two ways.

### What is NOT healed, and why

The migrator drops an anchor that cannot be a **position** (NaN, infinite, negative storey), because
there is no honest replacement — planting it at the origin would wake the player in the corner of the
map with nothing in the file to say why. It repairs nothing else. A region no scene claims, a
position outside that region, a storey in a building that has since lost its upstairs: all are
*content* questions, all can flip between two builds of the same game, and a loader that erased them
would throw the player's bed away the one time an asset failed to import. They are declined at the
point of use, where it costs one session instead of the save. This is the same line the v11→v12 step
draws around an unresolvable outfit id.

## Scope: SAME-REGION wake, declined out loud

This slice honours an anchor **only when it names the region that booted**. An anchor from another
region is refused with a warning naming both, and never applied.

Waking in a *different* region means driving the additive region load from the save — a travel-rig
change, not a save-schema one. The anchor already carries everything that change would need, so it is
a read-side extension and needs no second migration. In the meantime the alternative had to be ruled
out explicitly: St Peters coordinates applied inside Nine Mile Creek put the player in the sea or in a
cliff, with nothing in the log to explain it. Today the restriction costs nothing real — the game's
one authored player bed is in the start region.

## Consequences

- **Good:** the manual save becomes a place, not just a timestamp. The schema is append-only and
  v12 saves load untouched. The anchor is general — a camper bunk, an inn, a bought house all fit it
  with no further schema change, because a bed is only ever "a region, a storey and a spot".
- **Good:** `BuildingInterior` still knows nothing about saves; it hears a signal (rule 4). The same
  signal is where a fade, a "morning" line or the `Fisher_sleep` animation will hang.
- **Cost:** `BuildingInterior` gains a one-shot storey — a genuine exception to ADR 0036's "the level
  resets on the way out". It is spent by the next inside-transition and by nothing else, so every
  ordinary walk through a front door still lands on the ground floor.
- **Cost:** cross-region wake is deferred, and until it lands a player who somehow anchors in another
  region gets a warning rather than a wake.
- **Open:** waking should eventually move the clock to morning and play the sleep animation. Neither
  belongs to the save system; both belong on `PlayerWokeAt`.
