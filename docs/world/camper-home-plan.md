# The camper on Ginny's lot: making it a better home

Owner ask, 2026-11-17: "we need to make the camper on ginnys lot functional with the new features, it should
offer something to the player that ginnys cottage does not, perhaps more storage? whats the advangte to the
player buying it".

Owner ruling, same day, on towing it to other regions: "No, just a better home". The camper stays on its lot.
This plan has no towing, hitching or mobile-base work in any phase.

Read at main `2f84412d50` (save v24). Open work this plan leans on: #1293 (bathroom fixtures, piloted in Ginny's
cottage), #1295 (which wall each kitchen and bathroom piece backs onto), #1296 (soap). This PR places nothing and
changes no code, data or scene.

## 0. The short version

- Today the camper (`home.ginny_lot_camper`, 900) can be bought, and the quest "A roof of your own" completes
  when he buys it. After that it does nothing. Its door says the inside is not finished, because the camper kit
  has no interior rig.
- Ginny's cottage already gives him a bed he can sleep in **at any hour** (up to 8 hours), a wardrobe and a
  washstand. Her freezer outside **stops rot completely** and costs nothing. Once the kitchens and bathrooms
  lanes land, the cottage also gets a full kitchen and a full bathroom.
- So two of the seat's leans do not hold up as stated. "A bunk at any hour" is no better than his bed at
  Ginny's, and "an icebox that keeps catch fresher" would be **worse** than Ginny's freezer, because ice runs
  out and her freezer never does. A galley and a wash spot are a match for the cottage, not a step up, once
  R2 lands.
- What the camper can give that the cottage does not: **it is his** (a deed, his own key, the first rung on the
  homes ladder), and **storage that stays put**. That means a second freezer for catch, a fridge and pantry for
  food, and a gear locker and rod rack for his tools. Today he has nowhere to keep tools at all, and nowhere
  at home to keep food.
- Six build slices. Only one needs a new save row: keeping the gear locker and rod rack contents across a save
  (save v25). Everything else rides rows that already exist.

## 1. What Ginny's cottage gives the player today

From code and data at `2f84412d50`.

| What | Today | Where it comes from |
|---|---|---|
| Sleep and save | His bed upstairs (`InteriorFixture.PlayerBed`). It saves the game and records where he wakes (ADR 0037, the bed id since v20). He can turn in at **any hour**, chooses how long, and wakes after **8 hours at most**. Ginny's own bed gives a polite refusal. | `InteriorBed` (`_isPlayerBed`, no deed checked); `SleepPlan.RulingMaxHours = 8`; `GameConfig.SleepMinHours/MaxHours/StepHours`; `StPetersInteriors.cs` upper storey |
| Rules | **No rent, no hours, no curfew.** Nothing locks the cottage at night and nothing gates sleep on being tired. It is lodging: no deed, "lodging is not ownership". | No door-hours code in `BuildingInterior`/`InteriorBed`; `HomeDef` comment and `progression-and-housing.md` §4.1 |
| Catch storage | **Ginny's freezer**, outdoors in her dooryard. It holds **20 units** (one full pail), and catch in it is **frozen**, which stops rot completely (`FrozenRateMultiplier 0`). It never runs out and is free. It is hers, and it moved with her by the 2026-08-16 ruling. Saved as `SaveData.FreezerCatch`. | `GinnyFreezer` (`_capacity = 20`), `StPetersGinnyPlot.FreezerPos`, `GameConfig` freshness block |
| Food storage | **None at home.** Shop food and cooked food go into his supply locker (`SupplyStock`, carried, not a place). The kitchen stores (fridge 30, pantry 24, pantry locker 8, cooler 12) exist as data, but nothing on land uses them. | `Data/Resources/Kitchen/Storage/*.asset`; `KitchenStores` (save v23) |
| Gear storage | **None.** The cottage's sea chests and dresser are props or stair markers. The rod rack (`stowage.rod_rack`, 4 rods, sold for 25) is a boat fixture, and **what is in a rack is not saved** (session-only by design). | `StowageRack` remarks; `Data/Stowage/RodRack.asset` |
| Cooking | **Nothing placed yet.** Kitchen stations work (prep counter, stove, microwave; cook jobs saved in v24), but the plan to place them is not merged. The R2 plan gives the cottage a **full kitchen** (prep, stove, microwave on the north wall). | `CookingStationFixture`; `kitchens-bathrooms-placement-plan.md` §1.1; #1295 |
| Washing | A **washstand** (sink) in his room upstairs, which cleans him up to **0.8** at 1.0 per hour. Open #1293 adds Ginny's **ground-floor bathroom**: toilet, basin, and a **shower**, which cleans to **1.0** at 3.0 per hour. With #1296, a full clean needs soap. | `WashStationPlacement.StandInterior` (#1273); `Data/Resources/WashStations/*.asset` |
| Clothes | A wardrobe in his room (outfit picker). | `InteriorWardrobe`, `InteriorFixture.Wardrobe` |
| Comfort | **No comfort buff anywhere.** Sleep restores the sleep need the same in any bed. | No comfort code in Core; `progression-and-housing.md` §4.1 says so |

What the camper does today: it is placed, priced at 900 and bought at its `HomeDoor` (wallet first, then the
deed `deed.home.ginny_lot_camper`, then `HomePurchased`). The quest `quest.a_roof_of_your_own` completes on that
deed. After that, the door answers "the inside isn't finished" (`_interiorStood: 0`). The test
`ThePlacedCamper_HasADoorThatRefusesEntryWhileThereIsNoRoomBaked` holds the door to that until a room is
baked.

## 2. What the camper should give that the cottage does not

### 2.1 The seat's leans, tested

| Lean | Verdict | Why |
|---|---|---|
| His own place | **Holds.** It is the only home he owns at this point in the game. | Lodging at Ginny's has no deed; the camper does. This is P2: the first rung of the homes ladder (camper 900, red saltbox 6,000, NMC estate 18,000). |
| More storage | **Holds**, and it is the main case. | The cottage has no gear storage and no food storage. Its catch storage is one 20-unit freezer. |
| An icebox that keeps catch fresh longer | **Fails as stated.** | Ginny's freezer already stops rot completely and never runs out. An icebox on ice is the cooler pattern: it is only cold while the ice lasts. It would be *worse*. What does hold is **more cold room**: a second freezer, powered from the camper's shore hookup, keeps catch just as well as hers and doubles what he can hold at home. |
| A bunk he can sleep in at any hour | **Fails as an advantage.** | His bed at Ginny's already works at any hour. The bunk is still needed, because a home has to be somewhere he can sleep and save. It just is not a reason to pay. Making Ginny's bed worse to flatter the camper would go against P5's cozy half, so this plan does not. |
| A small galley and a wash spot | **A match, not a step up**, once R2 and #1293 land. | The cottage gets a full kitchen and a shower. The camper's real extra is the **food stores** that come with a galley (fridge and pantry), which the cottage plan does not have. |
| A rod rack or gear bench | **Holds**, if what is in it is **saved**. | No rack or locker anywhere keeps its contents across a save today. A locker that forgets is not storage. |

### 2.2 The advantage list

In plain words, as the player feels it:

1. **It is his.** He has a key and a deed, and somewhere that is not his aunt's spare room. Ginny says so when
   he buys it. (P2. Already true: the deed exists. Needs the inside to mean anything.)
2. **A second freezer for his catch.** Ginny's freezer takes one pail (20 units). The camper's chest freezer
   takes another 20, and keeps catch just as well. He can hold two pails at home and carry them across the bar
   together, or wait for a better price. (P2, P4: storing to time sales is the roadmap's M2 storage beat.)
3. **A fridge and a pantry.** He can keep food at home: shop food, and his own cooked meals, ready for the
   morning. Hunger slows him down but never stops him, so food at home saves him a trip to the store before a
   day on the water. (P5: the needs are the teeth; a stocked home is the cozy answer.)
4. **A gear locker and a rod rack that remember.** His shovel, spare pail and spare rods stay where he left
   them, across saves. Today he has nowhere to put a tool down and find it again tomorrow. (P2, and the design's
   own reason for home storage: "the organizational reason to upgrade", `progression-and-housing.md` §4.2.)
5. **Everything in one room.** He can sleep, cook, wash, and store his catch and gear without going into
   someone else's house. That is a small comfort, not a power, which is the right size for 900. (P5.)

The bunk, galley stove and wash spot come with it because a home needs them. They are not the reason to buy.

### 2.3 Cheap now, or later

| Item | Cost | When |
|---|---|---|
| Bunk that sleeps and saves | Cheap. `InteriorBed` with `isPlayerBed: true`; the door already stops anyone without the deed getting in. No code. | With the interior |
| Galley stove and prep counter | Cheap. `CookingStationFixture` prefabs exist (#1272); cook jobs are saved (v24). | With the interior |
| Wash spot (basin, toilet) | Cheap once #1293 merges: its fixture prefabs and placement step. | With the interior |
| Shower | Depends on the room. The shell's head is a 0.43 × 0.88 m partition, too small for a shower. See Q2. | With the interior, if the rig makes it a wet bath |
| Second freezer, fridge, pantry | Cheap in data: new `KitchenStorageDef` assets, saved in the v23 `KitchenStores` rows already on main. Probably needs one small fixture to move catch from the pail into a kitchen store (§3, C5). | Any time; placed with the fixtures |
| Gear locker, rod rack | Cheap to place: a `StowageDef` and the existing `StowageRack`. **Keeping the contents needs a save row** (v25). | Placed with the fixtures; saved in C6 |
| Comfort buff, decor, furniture placement | Not cheap. Later phase in the housing design, and it would need its own ADR. | Not in this plan |

**Does 900 feel worth it?** With items 2 to 4 it does. 900 is the same as a used outboard. For that he gets
20 more units of catch held at home with no rot, 16 food and 8 dry goods, 6 tools and 4 rods kept for him, and a
place of his own, with no running cost. Without the storage, it is 900 for a second bed.

## 3. Build slices, in order

The camper is a Clipper 26 (`HomeDef.Variant = "clipper"`): body 7.16 × 2.20 m, standing headroom 2.00 m, floor
0.70 m off the ground (`camperIsoRig.clipper.gameplay.json`). The shell's sidecar already places the fit-out as
footprints: a transverse rear double berth, a wardrobe, a partitioned head, an L-shaped galley ("range, sink,
fridge below the counter"), a dinette whose table drops to make a second berth, a front lounge, and a nose locker.

**Which way it faces (inferred, confirm in the editor).** The lot draws facing 2 (`StPetersCamperLot.DoorFacing`).
In `Camper.json`, at facing 2 the door and step sit below the pivot and the hitch sits far to the east. So the
door (the curb side, rig +x) faces **south**, toward the cottage, and the hitch end faces **east**. That puts the
road side (rig −x) on the **north** wall. The shell's galley is on the road side, so **the galley already backs
onto the north wall**, which is what #1295's rule 3.3.4 asks for. The head is on the south (door) side at the west
end. The rear berth runs across the west end, and the nose locker sits at the east end.

| # | Slice | Owner | Needs | Save row | Editor |
|---|---|---|---|---|---|
| C1 | **Interior rig.** `camperInteriorRig.js` (named in the kit README but never delivered), Clipper only. It fits the shell's published `loft` and `SOLE`, and exports `CamperInterior.interactables({variant})` with an anchor and reach point for each piece: bunk, stove, prep, galley sink, freezer chest, fridge, pantry locker, gear locker (the nose locker), rod rack, wardrobe, head basin, toilet, and a shower only if Q2 says wet bath. Headroom stays 2.00 m. | art-director (rig sources) | nothing | none | no (rig JS); an owner look at a contact sheet |
| C2 | **Mesh body and mesh interior, with a cutaway.** Bake the camper shell and C1's room as mesh, as the everything-mesh ruling asks. Trailers already go through `VehicleMeshAssetBaker`, and boat rooms through ADR 0041 and 0048. When he steps inside, the roof and the south (door-side) wall cut away, the way a boat cabin does. The baked sprite stays until the mesh passes the owner's look. | art-pipeline (bake), lead-architect (the cutaway seam for a building that is not a hull) | C1 | none | **yes** (bake and look) |
| C3 | **Walk in.** The `HomeDoor`'s `Entered` outcome becomes real: the door leaf swings (the boat door ruling: a press moves the leaf), the step and threshold come from the shell sidecar, and the `SOLE` polygon is the walkable floor. `InteriorStood` turns true **because a room is baked**, which is what the camper lot test is written to notice. Waking in the bunk after a reload must open the cutaway (the rest anchor already carries the bed id and position). | world-content (scene, door), economy-sim (review `HomeDoor`) | C2 | none (rest anchor v13/v20) | **yes** (StPeters, layer-refresh path, never a full rebuild) |
| C4 | **Fixtures.** Bunk (`InteriorBed`, `isPlayerBed: true`, place name "your camper"), stove and prep counter (`CookingStationFixture`, ids `fixture.st_peters.camper.stove` and `.prep`), head basin and toilet (#1293's fixtures), wardrobe if wanted. The galley on the north wall, the head at the west end. Rows go in `RoomFixturePlan` (or a camper table beside it) with tests: inside `SOLE`, outside the door's swept arc, clear of the fit-out footprints and of each other, and each id unique. | world-content | C3, #1293 merged | none (cook jobs v24) | **yes** (placement look) |
| C5 | **Storage as data.** New Defs, one per file, with identity additions. Capacities are fields, not code (rule 6). The proposed numbers are Q4's. Store keys follow the pattern `fixture.st_peters.camper.<anchor>`. If no world fixture yet moves catch from the pail into a kitchen store, add one with the `GinnyFreezer.Exchange` shape (carrying: what fits goes in; empty-handed: it comes out), backed by `KitchenStores` rather than a new save list. Tests: each capacity is read from its Def; catch keeps the hours it already spent; a sleep straight through gives the same result as staying awake (rule 5); a full store refuses and nothing is lost. | economy-sim (Defs, numbers), world-content (placement rides C4) | logic: nothing; placement: C4 | **none** (v23 `KitchenStores`) | no for logic and data; placement in C4 |
| C6 | **Keep what is in the locker and rack.** One new `SaveData` list of (rack key, tool id), one migration step to **v25**, written through the rack. This follows ADR 0020's placed-object persistence. Round-trip and old-save tests assert against `SaveMigration.CurrentVersion`, never a literal. This also fixes boat rod racks forgetting. | lead-architect (save), gameplay-systems (`StowageRack`) | C5's locker Def | **yes, v25** | no |
| C7 | **Words.** Ginny's line when he buys it and when he first sleeps there; the door's "isn't finished" line retires once C3 lands. Text in data, in the speech-bubble style. | world-content | C3 | none | no |

Order and overlap: C1 first. C5's data and logic, and C6, can run alongside C1 and C2 because they need no
editor. C3 and C4 follow C2 and are one StPeters writer at a time, behind any other StPeters lane (the R2 to R5
series in the kitchens plan). C7 goes last.

Proposed new ids: `kitchen_storage.camper_freezer`, `kitchen_storage.camper_fridge` and
`stowage.camper_gear_locker`. It reuses `kitchen_storage.pantry_locker` and `stowage.rod_rack`.

Things to check when building C5: whether a `Fridge`-kind store with `ColdMode: Frozen` passes the kitchen content
tests (all four shipped stores use `Iced`), and whether the default `AlwaysOnKitchenPower` is the right stand-in
for shore power. Its "always on" behaviour is what makes the freezer match Ginny's.

## 4. Questions for the owner

Each has a recommended answer.

1. **Does Ginny's cottage stay fully usable after he buys the camper?** Recommended: **yes.** His bed, her
   freezer, her kitchen and bathroom all stay open to him. The camper adds; it never takes away.
2. **A shower in the camper?** The shell's head is too small for one. Recommended: **the interior rig makes the
   head a small wet bath** (toilet and shower in one, as real 26 ft trailers have). If it will not fit, the head
   gets a basin and toilet, and he showers at Ginny's or the wharf.
3. **The second catch store: powered freezer, or an icebox that needs ice?** Recommended: **a powered chest
   freezer on the shore hookup**, so it keeps catch exactly as well as Ginny's. An icebox would keep catch worse
   than the freezer he already has for free, so it would not be an advantage.
4. **The storage numbers** (all one field each, tunable later). Recommended: freezer **20** (one pail, the same
   as Ginny's), fridge **16** (a camper's under-counter fridge, smaller than a house's 30), pantry locker **8**,
   gear locker **6** tools, rod rack **4** rods.
5. **Save the locker and rack contents (save v25), or ship them unsaved first?** Recommended: **save them, in
   the same run as the locker** (C6). Storage that empties on reload would undo the main reason to buy.
6. **Price.** Recommended: **keep 900.** With the storage it is a fair first property next to a used outboard
   at 900.
7. **Bantam or Clipper?** Recommended: **stay Clipper** (he can stand up in it). The interior rig is drawn for the
   Clipper only.
8. **Does buying it move where he wakes up?** Recommended: **no automatic move.** He wakes in whichever bed he
   last slept in, which is how the rest anchor already works.

## 5. What I could not verify

- The facing reading in §3 comes from the facing-2 anchors in `Camper.json`. Nobody has looked at it in the
  editor.
- Whether any world fixture already moves catch from the pail into a `KitchenStores` store. The model accepts
  catch with its own clock (`KitchenStorageModel.Store`), but I found no land fixture that calls it.
- Whether a frozen-mode fridge passes the kitchen content tests.
- The size of a wet bath inside the Clipper's loft. That is the interior rig's call.
- Nothing was compiled, run or placed. This is a plan only.
