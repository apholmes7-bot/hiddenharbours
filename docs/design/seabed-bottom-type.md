# Bottom type: one seabed property, three readers (2026-11-12)

**Owner's direction, verbatim:** "lobster boats will need to rotate traps as catch migrates, crab
and lobster for crustacenas out of NMC, crab likes mud, lobsters need rock, sounder shows
rock/bottom types"

**Status.** A one-page plan, not a build. Read against main `12bc04b19a`. Paths are under
`Assets/_Project/` unless they start with `docs/`. It goes with `docs/design/helm-tools-review.md`
section 4.

**Scope.** The Core interface, the deterministic bottom-type model (rock, mud, sand), the habitat
preference data, the migration term and the trap-rotation planner are the fish lane's, on its own
branch. This note does not define them. It says what the NPC boats, the scenes and the sounder need
from that seam and how they use it.

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

## What the users need from the seam

Requirements on the fish lane's work, not a definition of it:

- **One answer for one point of sea floor:** rock, sand or mud, and how hard. The same answer for
  every reader, the same every run, nothing saved.
- **Answerable for a region that is not loaded.** NPC days are planned for regions the player is not
  in, so the question must take a region, not assume the loaded one.
- **Answerable everywhere, not only on fishing grounds.** The sounder must read a bottom where no
  one fishes. `FishingGroundDef.Bottom` must agree with it under each ground.
- **A never-null default** that answers "unknown", so a scene with no bottom data draws and plans as
  it does today.
- **A word on gravel.** The Bank is authored rock and gravel; the player-facing words are three.

## How it is used

1. **What a pot lands.** The player's traps and the NPC pots resolve catch through the same code
   (`PlacedTrapCatch`, `NpcPotDayCatch`). Both ask the seam for the bottom under the pot and the
   fish lane's preference data for what that bottom holds. A lobster pot on mud and a crab pot on
   rock should land little or nothing, by the same rule for player and NPC.
2. **Where the NPC boats set.** A pot day stops owning one fixed string. It lists candidate strings
   (start, bearing) as data, and each day asks the fish lane's rotation planner which one to work.
   The pot day keeps what is its own: the passage to the string, the timings, the deck work. This
   is the rotation "as catch migrates": the boats move ground on a rhythm the player can learn and
   are never seen setting on the wrong bottom. A content test checks every candidate string lies on
   a bottom its trap's species wants.
3. **The sounder.** One read on `IHelmInstruments` beside the depth read, throttled the same way.
   The fish finder's bottom bands (`Code/UI/Draw/FishRigRender.cs:343-358`) are driven by kind and
   hardness; the depth sounder gains one bottom word. No instrument asks Fishing.

## What the player sees

- At Nine Mile Creek, lobster boats work the rock ledges and a crab boat works the mud, and over the
  weeks each shifts its string to another piece of the same kind of bottom.
- Running a sounder over a string of NPC buoys shows why they are there: hard bottom under the
  lobster pots, soft under the crab pots.
- The player's own pots obey the same rule, so watching the fleet teaches where to set.

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

- Cost: every reader here already reads on the slow tick or a throttle. Nothing per frame, nothing
  saved, no save-format change on this side.
- It changes catch: a player's lobster trap on mud that fishes today will fish poorly or empty. That
  is the owner's rule, and it wants the sounder to ship in the same step or before, so the player
  can see why.
- Order, after the fish lane's seam is on main: (1) the creek's bottom authored, and the South
  Water's to match its grounds; (2) the sounder and finder read; (3) the species' regions, and pot
  catch reading the bottom; (4) candidate strings on the pot days, rotation, and the crab boat.

## Open, for the seat

- Who authors the bottom of each region: this lane (scene content) or the fish lane with its model.
- Whether a wrong-bottom trap fishes empty or only poorly; the fish lane's preference data decides.
- Whether changing `CatchRegionId` from St Peters to the creek changes what the three boats land
  today; to be read before step 3, not assumed.
