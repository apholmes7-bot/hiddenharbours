# ADR 0034 — A facing is a GROUND bearing; the world XY plane is not

- **Status: ACCEPTED (2026-08-13)** — gameplay-systems / art-pipeline decision, on a measurement of the
  shipped character sheets. Closes a defect that pre-dates the NPC routine work and is not owned by it.
- **Date:** 2026-08-13
- **Decision owner:** gameplay-systems (the presenter and its callers); art-pipeline (the bake fact the
  measurement establishes).
- **Serves:** **P3 (A Living Working Coast)** — a fisher walking north who is drawn walking north-east
  reads as broken in a way the player feels before they can name it, and it lands on every character in
  the harbour at once. Also **rule 4**: the number now has exactly one home in Core.
- **Related:** `0006-boat-art-pipeline.md` / `0022-3d-boat-hulls.md` (the shared ¾ bake camera these
  numbers come from),
  `0026-rig-pivot-conventions.md` (the other measured rig convention every consumer must obey),
  `0021-in-engine-js-rig-baking.md` (why the rigs can be re-run and measured at all).

## Context

Two facts about this project were each true, each written down, and never put in the same sentence.

**One — the world XY plane is the SQUASHED ground plane.** The shared bake camera sits 40° above the
horizon, so one metre of NORTHWARD ground travel draws `sin 40° ≈ 0.643` world units up the screen while
one metre EASTWARD draws a full unit across (`HiddenHarbours.Art.SpriteLightMath.GroundDepthScale`). A
room 6.6 × 8.05 m of floor occupies 6.6 × 5.2 world units; `InteriorFootprint` carries that warning in
capitals, and `Art.Editor.BuildingFacing` un-squashes before it takes any angle — a lesson bought in #495,
where the arithmetic version put a schoolhouse door **92°** away from the green it was meant to face.

**Two — the baked character rows are evenly-spaced GROUND bearings.** This one was assumed, not checked,
so it was measured (2026-08-13):

- A standalone V8 harness (the `#433` recipe) loaded `eyeIsoRig → headIsoRig3 → characterIsoRig6` and
  re-rendered the whole `idle` sheet at the baker's own options — row `d` at `dir = d`. The control
  passed: **1 130 496 bytes, byte-identical to the committed `Fisher_idle.png`.** That single fact
  already pins **all eight** rows at `45°·d` of ground azimuth, and it is what makes everything below a
  measurement of the shipped pixels rather than of a plausible re-implementation.
- The first DIAGONAL row — where the two readings differ most — was then matched against a *continuous*
  turntable angle (the rig takes a fractional `dir`). Row 1, the one labelled NE,
  matches at **exactly 45.00° of ground azimuth, at distance 0**, with a clean monotone bowl either
  side (43.2° scores 16 372; 46.8° scores 22 099). The rival hypothesis — that the rows are evenly spaced
  in world-XY/screen bearings, putting the first diagonal at 57.26° — scores **142 370** at the nearest
  sampled azimuth (57.6°), and the sweep is monotone from 45° out to it.
- The rig says the same thing in one line: `characterIsoRig6.js` `camBasis` is `th = -dir*Math.PI/4` — a
  plain rotation about the vertical axis — and the 0.643 squash is applied afterwards, at projection
  (`sy = cy - (yr*se + zr*ce)*S`). The turntable steps in ground azimuth; the camera foreshortens.

Put together: `IsoCharacterSprite` measured its own step in world XY and read it as a compass bearing.

**The size of it.** `tan(ground) = sin 40° · tan(world)`, so the error peaks at **12.56°** (at a world
bearing of 51.28°) and is zero only on the cardinals. A row spans 45°, so most of the time the wrong
bearing still picks the right row — which is exactly why it survived. It does not near a boundary:

| band (world bearing) | drawn before | correct |
|---|---|---|
| 22.5° … 32.79° | NE | N |
| 67.5° … 75.10° | E | NE |

…and the same two bands in each of the four quadrants. **About 20% of all directions drew the
neighbouring facing**, always as a turn toward the nearest diagonal.

## Decision

**A heading that picks a facing is a GROUND bearing. Anything that derives one from a world-space vector
un-squashes first.**

1. **`HiddenHarbours.Core.IsoGround`** is the single home for the depth squash and the world→ground
   bearing. Core references nothing, so it cannot read `SpriteLightMath`; it carries the number and an
   EditMode test pins the two together — the `BuildingInterior` precedent, chosen over a serialized field
   so no existing scene can deserialize a zero into it.
2. **`IsoCharacterMath.GroundHeadingFor`** is what reads a world-space velocity. All four call sites that
   derived a character facing from a world-space vector are corrected: `IsoCharacterSprite` (its own
   motion), `PlayerHaulAnimator.HeadingToBuoy` (fisher → buoy),
   `PlayerFishingAnimMath.FacingRowFor` (angler → fish / cast aim), and
   `ControlSwitcher.HeadingBetween` (the vault and ladder clip legs).
3. **`IsoCharacterMath.HeadingFor` is kept, unchanged, for planes that are NOT squashed** — and its only
   remaining caller is one: `DeckRiderFacingMath.DeckBearing`, whose frame is metres of real deck on both
   axes. Un-squashing a deck step would introduce the very error this ADR removes. The test is not "is it
   a `Vector2`" but "did this come off a world-space transform?".
4. **A STATED heading is already a ground bearing and is not touched.** `IsoCharacterSprite.HoldHeading`,
   the hull headings `DeckRiderVisual` composes, `MooredBoat`'s skipper, and the `_initialHeadingDegrees`
   the scene builders seed all pass through unchanged. Anything that wants to state a facing *toward a
   place* should use `IsoGround.BearingDegrees(from, to)` rather than an `atan2` of its own — that is what
   keeps a stated stance and a measured walk agreeing, which a plain world-XY `atan2` only appears to do.

## Consequences

- Every character in the game — the player on foot, at the rod, hauling a pot, and the harbour's NPCs —
  now faces the direction they are travelling across the ground.
- The gait threshold and the facing dead-band are still measured in WORLD units, deliberately: they ask
  "was that a real step?", which is a question about the motion actually read off the transform.
  Un-squashing before the test would make a northward step 1.56× easier to clear than an eastward one.
- Nothing about the art changed and nothing was re-baked. The rows always meant this.

## Open, and deliberately NOT settled here

- **The boat lane has the same shape and has not been measured.** `BoatKinematics.BearingDegrees` turns a
  world-space velocity into a "compass bearing" with no un-squash, and the iso hull kits are a different
  lineage (still baked counter-clockwise, per `BoatVisualDef.FacingsAreCounterClockwise`). Whether a hull's
  drawn facing agrees with its track is a separate question needing its own pixel measurement — and the
  wake and the spotlight ride the same heading, so a change there moves three things at once. **Do not
  assume this ADR generalises to it.**
- **The FISH rigs likewise** (`RodFightPresenter` picks its shadow/dart rows from a world-space travel
  direction).
- **Ground speed is still direction-dependent.** `PlayerWalkController` moves at uniform speed in world
  XY, so a walk north covers 1.56× the ground metres of a walk east. That is a pacing question, not a
  facing one, and it is left exactly as it was.
