# Bottom type: one seabed property, three readers (2026-11-12)

**Owner's direction, verbatim:** "lobster boats will need to rotate traps as catch migrates, crab
and lobster for crustacenas out of NMC, crab likes mud, lobsters need rock, sounder shows
rock/bottom types"

**Status.** A one-page design, not a build. Read against main `12bc04b19a`. Paths are under
`Assets/_Project/` unless they start with `docs/`. It goes with `docs/design/helm-tools-review.md`
section 4 and replaces that section's step 3 (see "Where the data lives").

## What main already has

- **The animals and the gear.** `fish.rock_crab` and `fish.lobster` (`Data/Fish/`), their ecology
  assets (`Data/FishEcology/`), `trap.crab` (catches only crab, bait `bait.fish_scrap`) and
  `trap.lobster` (only lobster, bait `bait.herring`), and a crab pot offer at the shipwright.
- **Three Nine Mile Creek pot boats**, all lobster (`Data/Traps/Nmc*PotDay.asset`). Each sets one
  string of six pots from a fixed start, moved 3 m a day (`DailyShiftMeters`).
- **Neither crab nor lobster lists `region.nine_mile_creek`.** Both list Coddle Cove, St Peters and
  the East Water. The three pot boats borrow `CatchRegionId: region.st_peters`.
- **The two animals want the same bottom today.** Both ecology assets carry `Habitats: 30`, which is
  sand, mud, gravel and rock together.
- **Trap catch never reads the bottom.** `PlacedTrapCatch` builds its pool from the trap's allowed
  species and the bait; `TrapFill` from seed, trap and soak time.
- **Two bottom vocabularies, both in Fishing:** `EcologyHabitat` (open water, sand, mud, gravel,
  rock, estuary) and `FishHabitat` (no gravel).
- **Bottom is authored in one place only:** `FishingGroundDef.Bottom` and `Hardness01`, on three
  South Water grounds. No runtime code reads them. Nine Mile Creek has none.
- **No Core seam.** `ITidalTerrain` answers elevation only. The fish finder's bottom picture is
  invented (helm tools review, defect 1).

## The property

One answer for one point of sea floor: **a kind (rock, sand or mud) and a hardness from 0 to 1.**
It is authored, it depends on position only, and it is never saved. It does not change with the
seed, the tide or the clock.

```
Core:  enum SeabedKind { Unknown, Mud, Sand, Rock }
       readonly struct SeabedSample { SeabedKind Kind; float Hardness01; }
       interface ISeabed { SeabedSample At(string regionId, Vector2 worldPos); }
       GameServices.Seabed   // never null; the empty default answers Unknown
```

The region id is an argument because NPC plans are built for regions that are not loaded.

## Where the data lives

One `SeabedDef` per region (`Data/Seabed/`, id `seabed.<region>`): a default kind and hardness, and
an ordered list of patches (a shape, a kind, a hardness; the later patch wins where two overlap).
The provider answers by containment. This is the single source.

`FishingGroundDef.Bottom` and `Hardness01` stay as they are for now, and a content test requires
each ground to agree with the seabed under its centre. That is why the provider is not built on the
grounds, as the review first proposed: the bottom exists where no one fishes, and the sounder must
answer there too.

Lean: gravel is not a fourth word. It is authored as rock at a lower hardness. The Bank (rock and
gravel, 0.8) becomes rock at 0.8.

## The three readers

1. **What can be caught (the spawn side).** Lobster's ecology becomes rock (and gravel); crab's
   becomes mud, with sand allowed. Two places read the seam:
   - the trap pool: a species whose habitat does not match the bottom under the trap is dropped
     from the pool, so a lobster trap on mud fishes empty and a crab trap on rock does the same.
     The player's traps and the NPC pots go through the same code;
   - the school field's habitat patch, where it is built, takes its bottom from the seam.
   The mapping from `SeabedKind` to the two Fishing enums is one function in Fishing.
2. **Where the NPC boats set.** A pot day stops owning one fixed string. It lists candidate strings
   (start, bearing). Each day the plan picks one, as a function of `(worldSeed, day)` and a rotation
   period that is a Def field, from the candidates whose pots all lie on a bottom its trap's species
   wants. That is the rotation "as catch migrates": the boats move ground on a rhythm the player can
   learn, and never set on the wrong bottom. A content test checks every candidate.
3. **The sounder.** One read on `IHelmInstruments` beside the depth read, throttled the same way.
   The fish finder's bottom bands (`Code/UI/Draw/FishRigRender.cs:343-358`) are driven by kind and
   hardness; the depth sounder gains one bottom word. No instrument asks Fishing.

## Crab as a Nine Mile Creek catch

- Append `region.nine_mile_creek` to both species; the pot days' `CatchRegionId` becomes the creek's
  own.
- Author the creek's seabed: rock ledges for lobster, mud in the creek and the flats for crab, sand
  between. This is the real missing content.
- One new pot day with `trap.crab`, or one existing boat turned to crab. No new species, trap, bait
  or art is needed.
- **Possible STOP:** if the Nine Mile Creek buyer needs an offer or a price for crab, that is a price
  change and is the seat's to rule. Crab's base value already exists on its asset.

## Cost, order and what it changes

- Cost: a containment test over a few patches per read; readers already read on the slow tick or a
  throttle. Nothing per frame, nothing saved, no save-format change.
- It changes catch: a player's lobster trap on mud that fishes today will fish empty. That is the
  owner's rule, and it wants the sounder to ship in the same step or before, so the player can see
  why.
- Order: (1) Core seam, `SeabedDef`, provider, the South Water authored to match its grounds;
  (2) the sounder and finder read; (3) Nine Mile Creek seabed, the species' regions and habitats,
  the trap pool filter; (4) candidate strings and rotation, and the crab boat.

## Open, for the seat

- Whose the provider is. This note puts the Def type in Core and the provider in World.
- Whether a wrong-bottom trap fishes empty (this note) or only poorly.
- Whether changing `CatchRegionId` from St Peters to the creek changes what the three boats land
  today; to be read before step 3, not assumed.
