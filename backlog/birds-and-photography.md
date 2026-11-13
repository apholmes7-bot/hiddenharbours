# Birds and photography

Owner-directed, 2026-11-13. Pillars: P3 first (a living working coast); P1 (birds follow tide,
weather, season and clock); P5 (a quiet goal beside a dangerous sea); P2 lightly (a region-by-region
album rewards reach). It sits in M2 as an owner-directed addition
([`docs/roadmap.md`](../docs/roadmap.md) section 0.5). It moves nothing else.
Source: the approved build plan `seat/v80/BIRDS-AND-PHOTOGRAPHY-PLAN-2026-11-13.md` (untracked, in
the coordinator seat's folder). This file sequences that plan; it does not add design.

The objective is **see more kinds of bird where each belongs → carry a camera → photograph a bird
in range → the photo sits in the notebook's Photograph page → hand it to the photo keeper → receive
a unique reward → save/reload**.

## The owner's asks, verbatim

The ask, 2026-11-13:

> "Also the co op game had way too many gulls. Lets add the other missing birds art. Add diving
> birds/ ducks/ herons/eagles/Add flock and individual behavior. Make them prefer their desired
> habitat, perch on trees, objects. They hunt appropriate fish types with fish reacting. Player has
> objectives to photograph with new camera tool with unique ui if player is in range. One npc on st
> peters keeps the photos and gives player unique rewards for photos of different species"

His answers the same day:

> "Start with diving birds and herons. Photos are stored on photograph page in notebook with clear
> little photo holder slots you would see in photograph book. Rewards should be unique items/
> clothing. Birds can vary by region. Bird numbers will be high. This encourages exploration.  New
> npc with new unique home not in village"

His approval of the plan's leans the same day:

> "Ok i go with those leans."

## What that approval ruled

- **Bird numbers.** "Bird numbers will be high" means many species and many birds to find **across
  the world**. On screen, about **32 birds drawn** in normal play, hard ceiling **48**, gulls capped
  at **16**. The gull-count fix (BP-00) stays.
- **The photo keeper's home** is at the **Heath pond**, outside the St Peters village (the source of
  the Heath brook; fresh water for herons and ducks at the door).
- **Small defaults:** the tern follows the cormorant as the second diving bird; the keeper lends the
  camera on the first visit; photos are per player in co-op; a photo is saved as a "recipe", not as
  pixels; item rewards ship first and garments follow CW-04.

## Performance budget

All of these are tunables in data (rule 6), never code defaults.

| Budget | Number |
| --- | --- |
| Birds scheduled per region (not drawn; arithmetic on the slow tick only) | ceiling 400. St Peters already schedules about 190 gulls |
| Bird places per region | 32 (the plan reads this as today's limit) |
| Birds drawn in normal play | about 32, from one shared pool |
| Birds drawn, hard ceiling | 48 |
| Cap per species in view | so that no one bird fills the view; gulls 16 |
| Species in view | cap 5. One sheet per species, so about one batch per species in view plus one for shadows |
| Culling | only birds near the view take a renderer; the rest are counted, as today |
| Allocation | none per frame |
| Saved bird state | none |
| Texture memory | checked when the fourth species sheet lands |

The ceiling of 48 is confirmed by a frame-time plate at the busiest spot before it is raised. If
the owner later wants bigger flocks on screen, the pool ceiling is the number to raise, after that
plate.

## Rules that span the deliveries

- **Content is data.** One `BirdSpeciesDef` per bird, one file each, with a stable `bird.` id:
  sheet, habitat kinds, diet (fish ids and reach depth), group size, perch kinds, how it hunts, and
  how close a photographer may come. One bird list per region. Places already carry a region id, so
  a species only appears where a region lists it. The camera is a `tool.camera` Def beside the rod
  and the shovel. Rewards are data, one per file. No species, count, range or reward is written in
  C#.
- **Determinism.** Every bird is placed from the world seed and the clock, as the gulls are today.
  Nothing about a bird is saved. A hunt's strike time is a hash of seed, place, slot and time
  bucket, decided on the 4-per-second tick. No hidden randomness.
- **Module boundaries.** The camera reads the existing Core seam `IBirdPresence`; the Player module
  never touches bird code. New cross-module facts (perch kind, the species on the hunt signal) go
  through Core and are `lead-architect`'s.
- **Bite rates stay untouched**, as ruled 2026-09-09. A bird's hunt reuses the live scatter; it
  changes what the fish do on screen, not what the player catches.
- **Pooling.** Birds stay pooled. No per-frame allocation.
- **The save.** Only BP-11 changes the save. Read the then-current save version before bumping it;
  it was 17 when this was written. Never test on the owner's save.
- **Region variety is the pull.** The album can show a region's birds as unknown slots, which is
  what draws the player to explore.

## Delivery and acceptance

"Plate" means a screenshot owed for review; "owner plate" means the owner looks at it. "Editor"
says whether the item needs the Unity editor beyond a plate.

| Item | Role | Size | Needs | The player sees | Acceptance (tests and owed plates) | Editor | New art | Core / save |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| BP-00: the gull-count fix | art-pipeline, with qa-test for the plates | S | none | Fewer gulls over open water | `GullRoster_SchoolGullsNeverExceedTheViewCap`; `GullFlock_PoolSizeComesFromTheLibrary`; a count plate before and after in EastWater and at the St Peters wharf | plate only | none | no |
| BP-01: land the R1.1 gull kit | art-director lands the rig; tools-editor bakes | M | BP-00 | Brown young gulls and big black-backed gulls | the kit's `check.cjs` PASS; `SeagullBake_ThreeLooks`; owner plate | yes | none (already drawn and exported; not yet imported) | no |
| BP-02: species as data, with a list per region | art-pipeline, world-content, qa-test | M | BP-01 | Black-backs only where their data says; a region can have birds no other has | `BirdSpeciesContentTests`; `RegionBirdList_EverySpeciesHasAPlaceInItsRegion` | no | none | no |
| BP-03: the cormorant (first diving bird) | a Claude art lane, then tools-editor | L | BP-02 | A black bird on the rocks with wings spread, diving for fish | the kit checker PASS; owner plate | yes | **yes**, Claude lane (briefed 2026-10-14) | no |
| BP-04: the great blue heron | a Claude art lane, then tools-editor | L | BP-02, BP-06 | A tall lone bird stalking the flats at low tide | the kit checker PASS; owner plate | yes | **yes**, Claude lane; a new body (briefed 2026-10-14) | no |
| BP-05: flock and solitary behaviour | art-pipeline | M | BP-02 | Gulls and cormorants in groups; a heron alone | `BirdGroup_SizeIsWithinTheDefsRange`; `Solitary_KeepsItsDistance`; a plate | plate | none | no |
| BP-06: habitat preference | world-content places; art-pipeline code | M | BP-02 | Each bird only at the kinds of place it likes; new place kinds: marsh and pond, treeline, sheltered water, rock ledge | `BirdHauntContentTests` extended; a plate | yes | none | no |
| BP-07: perching on trees and objects | lead-architect (perch kind in Core); world-content places | M | BP-02 | Birds on treetops, pilings, roofs, buoys and boat rails | `Perch_KindMustMatchTheSpecies` | yes | none | **Core yes**; save no |
| BP-08: hunting, with fish reacting | lead-architect (the signal), gameplay-systems, art-pipeline | M | BP-02, BP-03 | Each bird hunts the fish it eats; the school bolts | `BirdHunt_IsAPureFunctionOfSeedAndTime`; `BirdHunt_OnlyTakesItsDiet`; `BirdHunt_AllocatesNothing`; a plate | plate | none | **Core yes** (the signal gains a species); save no |
| BP-09: the camera tool and its in-range UI | gameplay-systems, ui-ux | M | BP-02 | A camera in the hand; a viewfinder that names the bird when it is close enough and in frame | `CameraFrame_FindsTheNearestBirdInRange`; owner plate | yes | **yes**: the camera prop and the viewfinder frame | no |
| BP-10: the Photograph page in the notebook | ui-ux, world-content | M | BP-09 | An album page: one little holder slot per species, empty and visible until filled | `PhotoPage_ShowsOneSlotPerKnownSpecies`; owner plate | yes | **yes**: the page and the holder | no |
| BP-11: the photo record in the save | lead-architect | M | BP-09 | The slot shows your own photo after a reload | `SaveMigration_V17LoadsWithEmptyAlbum`; a save round trip | no | none | **Save yes**: schema 17 to 18 with a migration (read the then-current version first) |
| BP-12: photo objectives | world-content | S | BP-10 | Notebook tasks such as "photograph a cormorant drying its wings" | the `QuestBoard` tests, with photo flags | no | none | no (existing quest flags) |
| BP-13: the photo keeper, a new NPC with a new home at the Heath pond | world-content, art-pipeline | L | BP-10 | A new person at a new house away from the village; your photos on the wall | a routine test; owner plate | yes | **yes**: the home, the NPC and the photo wall | no |
| BP-14: rewards as data | economy-sim, world-content | M | BP-13 | A unique item or garment for each species or set | `PhotoRewardContentTests` (each reward is given once) | no | **yes**: one item or garment each | item rewards: no. Clothing waits on CW-04 |
| BP-15: ducks, then eagles | a Claude art lane | L each | BP-06, BP-07 | Rafts of ducks; one eagle in a tall tree | as BP-03: the kit checker PASS; owner plate | yes | **yes**; the 2026-10-14 brief must be amended first (it has no duck and no eagle) | no |
| BP-16: the second region's bird list | world-content | M | BP-02, BP-03, BP-04 | Nine Mile Creek has birds St Peters lacks | the region list test; a plate | yes | per species | no |

## Notes on the items

**BP-00, the gull-count fix. In progress on branch `fix/gull-count-is-data`.** It is a fix on main
and needed no roadmap amendment. Cause, read from the code and not measured: co-op adds no gulls.
The count comes from main, so single-player has it too: 6 gulls for every fish school near the
surface (`SchoolBirds: 6` in `Resources/BirdHauntLibrary.asset`), up to 8 schools at once, with the
only ceiling a pool of 40 that was a code default the owner could not edit. The fix lowers the
gulls per school to 2 and adds two owner-editable numbers: the most school gulls in view (8) and
the most birds drawn. The owner's ruling sets the drawn pool at about 32 (ceiling 48, gulls 16);
see "Open points".

**BP-01.** The R1.1 kit holds the herring gull adult with a corrected wing, the first-year gull and
the great black-backed gull. It is drawn and exported and sits in the drops folder; nothing is
imported yet.

**BP-02.** Everything later hangs on this. It resolves the open note in
[`backlog.md`](backlog.md) that the `bird.` ids do not yet resolve to a species file. Start the
cormorant and heron art charter the same day BP-02 starts, because drawing is the longest wait.

**BP-03.** The common tern follows the cormorant as the second diving bird (briefed 2026-10-14, not
drawn). It takes the same shape as BP-03: a Claude art lane, then tools-editor, the kit checker and
an owner plate. Sandpipers are briefed too but were not asked for; they are not in this backlog.

**BP-08.** Reuses the scatter that is already live (a gull hitting the water scatters the school).
The Core signal gains a species. Diet is matched against the school's own species list and depth.
Not in scope, as before: a bird stealing from the player, fouling gear, or any bite or catch change.

**BP-09.** A `tool.camera` Def beside the rod and the shovel (`Data/Tools`). It reads
`IBirdPresence`. The photo keeper lends the camera on the first visit (wired in BP-13).

**BP-10 and BP-11, what a slot shows: a recipe photo.** The save keeps species, pose, facing,
place, day, hour and weather, about 50 bytes a photo. The slot redraws that moment from the bird's
own sheet over a small backdrop. It looks like the player's own shot, costs almost nothing, and the
save stays readable text. Stored pixels and one shared print per species were considered and not
chosen. Photos are per player in co-op.

**BP-13, the photo keeper.** The home is at the Heath pond, outside the village. The exact spot
needs a look at the scene in the editor; none was measured. The home needs: one small house unlike
the village's, a photo wall or album stand inside or on a porch, a hide or blind outside, a tripod
prop, a path joining the cliff walk, and a door lantern. All authored as prefabs in the St Peters
region scene, one `NpcDef` with a stable id, built on `Code/World/Routines`. The routine is a
function of the clock:

- before dawn, at the hide;
- morning, walking the cliff path with the camera;
- midday, at home pinning photos (the time to hand photos in);
- low tide, out on the flats;
- dusk, on the porch;
- indoors in fog and gales;
- one day a week, to the general store.

**BP-14.** Clothing cannot be granted on main today: the save holds only the outfit being worn
(`WornOutfitId`), and owned clothes arrive with CW-04 in
[`character-creator-and-wardrobe.md`](character-creator-and-wardrobe.md), which is pending. Unique
items can be granted now through the save's existing `OwnedGear` list. So item rewards ship first
and garments follow CW-04.

## Recommended order

1. BP-00, the gull-count fix.
2. BP-01, land the R1.1 gull kit: two more birds whose art is already drawn.
3. BP-02, species as data with per-region lists, with the cormorant and heron art charter started
   the same day.

## Open points

- **The drawn pool.** BP-00 was dispatched authoring 24 birds drawn. The owner's ruling is about 32
  (ceiling 48, gulls 16). The number is data after BP-00, so it is a value to set, not a code
  change; which PR sets it to 32 is the seat's call.
- **The camera before the keeper.** BP-09 lands before BP-13, and the keeper is who lends the
  camera. Until BP-13, the camera needs a stand-in way to reach the player's hand.
- **Objectives before the save.** BP-12 needs only BP-10, but a photo persists only from BP-11.
- **The Heath pond is small.** The terrain plan gives it as a bog pond of about 4.5 by 3.0 m on the
  heath. Whether herons and ducks read well on it is for the BP-06 and BP-13 plates.
- **The save version.** It is 17 today. Other active work may take 18 first; BP-11 reads the
  current version before it bumps.

## Completion evidence

Reviewed PRs and passing named tests for each item; the owed plates above; a frame-time plate at the
busiest spot with the full drawn pool; a playable photograph → album → keeper → reward → reload run
on a disposable save; and the content-validation test green for every new Def.
