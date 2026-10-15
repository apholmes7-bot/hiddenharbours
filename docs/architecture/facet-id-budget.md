# The facet-id budget: who takes the 255 ids, who gives them back, and what to change

**Status:** design note, PR D Phase A (no code). It proposes an ADR (§6). **The owner rules**; Phase B
implements the ruling in a separate session.
**Role:** lead-architect. **Serves:** P3 (a living working coast: the harbour's boats, trucks and
people all draw correctly at once), with rule 7 (performance budget) and rule 4 (the pool is Art's; nothing
outside Art reaches into it).
**Related:** ADR 0022 (3D boat hulls: the hull id in the screen texture), ADR 0044 §7.3 (figure ids
ashore, #861), `HANDOFF-2026-09-23-tools-five-known-bugs.md` PR D, and the ledger's 09-18 22:05Z
finding A.
**Measured on:** branch `docs/facet-id-budget` at `bc947ddd`. Scene counts come from the committed
scenes and the scene-export packages in `tools/scene-export/packages/`. NMC's package was built from
scene commit `cf76ac09`, which is the scene's last commit.

---

## 0. The short version

- **The problem.** Every mesh hull and every mesh truck takes **13 ids when it is enabled**: 1 for
  the hull and a block of 12 for deck occupants. It keeps them whether or not anyone is on its deck.
  The 8-bit alpha holds 255 ids, so the pool runs out after **19** hulls.
- **NMC wants more than that.** A cold load of Nine Mile Creek has **33** takers: 7 owned moored
  boats, 23 review hulls and 3 trucks. That is **429** ids.
  - Hulls 20 to 33 get no block, and 7 of them also share id 255.
  - A figure then gets **0**, so the player stays a sprite beside the trucks. That is the known red,
    `Ashore_BesideTheTrucks_NineMileCreekNoon`.
- **In play it is worse.** Arriving at NMC from St Peters brings **37 to 41** takers (§1.3).
- **Recommendation: take fore blocks lazily.** A hull or truck holds its single id always, but takes
  its 12-id block only when something first claims a deck slot, and gives it back when the last one
  leaves (§4.1). The ids used then drop:
  - **Cold NMC:** 105 used, **150 free**.
  - **Worst case in play:** 125 used, **130 free**.
- **Nothing else is needed.** No shader changes and no new render target. The two things that make
  NMC expensive, the 23 review hulls and the trucks, carry nobody, so they would cost 1 id each.

---

## 1. Count the takers

### 1.1 What counts as a taker

| Taker | Where it takes | Cost today |
|---|---|---|
| Mesh boat hull: `MooredBoat`, `BoatController`, ambient-fleet boat | `IsoFacetHullRenderer.OnEnable` (`IsoFacetHullRenderer.cs:778-790`) | 1 single + 12-block |
| Mesh truck: `ParkedVehicle` (and `ParkedTrailer`) | Same renderer, installed by `IsoFacetVehiclePresentationService.Install` (`IsoFacetVehiclePresentationService.cs:46-61`) | 1 single + 12-block, the same as a boat |
| Mesh figure ashore | `IsoCharacterFigureRenderer.EnterAshore` → `RegisterFigure` (`IsoCharacterFigureRenderer.cs:418-450`, `:438`) | 1 single |

A sprite hull takes nothing. A hull is a mesh when `BoatHullSkinner.ShouldPresentMesh` says so
(`BoatHullSkinner.cs:365-377`: `Variant == Mesh` and the visual has a hull mesh).

**Only the player takes a figure id today.** `EnterAshore` has one caller,
`DeckRiderMeshPresenter.cs:406`, and `LeaveAshore` one, at `:718`. The cast's presenter does not take
figure ids yet; ADR 0044 §7.3 leaves that to the ashore charter's PR 2.

### 1.2 Nine Mile Creek (cold load)

| Kind | Count | Mesh? | Notes |
|---|---:|---|---|
| Owned moored boats (`NineMileCreekFleet/Moored_owner.*`) | 7 | all 7 | Punt, DoryOutboard, CapeIslander, four lobster boats. **6 stand a skipper on deck** (`MooredBoat.StandTheSkipper`, `MooredBoat.cs:288-318`); `CampbellHughie` has no skipper. |
| Review hulls (`FleetReviewAnchorage`, owner ruling 2026-08-14 "every boat model must be VISIBLE") | 23 | all 23 | No owner, so no skipper (`MooredBoat.cs:293`). |
| Parked trucks: Dually, Otter 8×8, Modern 3500 | 3 | all 3 | |
| `BoatController` / `PersistentObject` in the scene | 0 | | |
| Parked trailers | 0 | | |
| **Hull takers** | **33** | | **429 ids** wanted today |
| Figures | 1 now (player); 3 once the cast is wired (player, Wendell Arsenault, Hector Bernard) | | 1 each |

**What happens at load today.** The pool hands out ids from a high-water mark
(`IsoFacetHullRegistry.cs:185-264`):

1. **Hulls 1 to 19** take 13 ids each, so `_nextId` ends at 248.
2. **Hulls 20 to 26** get singles 248 to 254. `TakeIdBlock` returns 0 for each, with a warning
   (`:239-244`).
3. **Hull 27** gets 255. **Hulls 28 to 33** share 255 (`TakeId`, `:192-205`).
4. **Any figure** gets 0 (`TakeFigureId`, `:213-224`, refuses at `_nextId >= 255`).

That is **14 "no deck-occupant block" warnings**, and 33 − 19 = 14 is exactly the count in the
ledger's run 2. **Which** hulls lose out follows scene order, so it can be an owned boat. When it is,
her skipper draws over her own cabin.

### 1.3 St Peters, and NMC reached from St Peters

**St Peters on its own:**

| Kind | Count | Notes |
|---|---:|---|
| The player's Dory (`BoatController`, `PersistentObject`, so `DontDestroyOnLoad`, `PersistentObject.cs:50`) | 1 | Travels with the player. |
| Parked vehicles: UtilityQuad, Trike200, Enduro250 | 3 | All mesh. |
| Ambient fleet: `StPetersAmbientFleet.asset`, `BoatCount` 4, `PuntIsoBasic` (Mesh) | 4 | `DontDestroyOnLoad`; active only while St Peters is the active scene (§2.1). No skippers. |
| **Hull takers** | **8** | **104 ids.** Fits. |
| Figures | 1 now; 8 with the cast (player + 7 villagers and the camper) | |

**NMC reached from St Peters.** Regions are never unloaded (§2.1). Travelling St Peters → NMC leaves:

- **Still holding ids:**
  - the 3 St Peters vehicles;
  - the Dory, which comes along;
  - the 4 ambient punts, until the fleet gate runs, up to 0.5 s after the switch.
- **Added:** NMC's 33.

The worst case is **41 takers (533 ids)**, falling to **37 (481)** once the punts free. The ledger's
run 1 had **19 warnings, 5 more than cold**. That fits the 4 punts plus the Dory still holding their
ids while NMC's hulls ran `OnEnable`. A test that loads St Peters first ends up in the same place,
because the fleet and the Dory survive `LoadSceneMode.Single`.

### 1.4 How many are on screen at once

Only takers the camera can see need distinct ids to draw correctly. I windowed the NMC package's hull
and truck positions at 16:9 and counted the best placement for each view. The camera:

- PixelPerfect, PPU 32, design height 1080 px (`CameraFollow.cs:75`), so a crisp step shows
  33.75 m ÷ n (upscale) or 33.75 m × n (downscale) (`CameraZoomPolicy.cs:131-137`).
- On foot the range is 5.625 to 11.25 m (`GameConfig.PlayerZoom`).
- Aboard, the hull's ruled `CameraWorldHeightMeters` is the start point, and the wheel can go
  ±2 crisp stops around it (`AboardStopsWider` 2, `CameraZoomPolicy.BandWorldHeightMeters`, `:396-406`).

| View | Window (m) | Most takers in one view at NMC |
|---|---|---:|
| On foot, closest | 10.0 × 5.6 | 2 |
| On foot, farthest | 20.0 × 11.25 | **3** (three owned boats) |
| Aboard the Dory, ruled | 24.9 × 14 | 5 |
| Aboard a Cape Islander, ruled | 42.7 × 24 | 7 (the whole owned fleet) |
| Band +1 or +2 stops (33.75 m) | 60 × 33.75 | 9 (review lobster boats) |
| Dory or Cape Islander at +2 stops (67.5 m) | 120 × 67.5 | **16** |
| 26 m-ruled offshore lobster boat at +2 stops (101.25 m) | 180 × 101.25 | **21** |

Add 1 for the player's own hull in every aboard row. **Worst in-game view: 22 hulls.** At today's 13
ids each that is **286 > 255**, so even perfect culling would not fit NMC's widest view (§4.2).

---

## 2. Who takes ids and who gives them back

### 2.1 Region unload: settled

- **In the game, a region is never unloaded.**
  - `RegionSceneLoader.Travel` loads with `LoadSceneMode.Additive` (`RegionSceneLoader.cs:95`).
  - It re-activates an already-loaded scene rather than reloading it (`:69-75`).
  - No runtime code calls `SceneManager.UnloadSceneAsync`. The only callers are in editor and test
    code.
- **So St Peters' hulls do not give their ids back when the player leaves:**
  - **The 3 parked vehicles** keep theirs for the rest of the session.
  - **The Dory** is `DontDestroyOnLoad` and is the player's boat, so it is meant to keep its ids.
  - **The ambient fleet is the one thing that frees.** `AmbientFleetPresenter` is `DontDestroyOnLoad`
    (`AmbientFleetPresenter.cs:72-80`). It checks the active scene every 0.5 s (`:43`, `:202-204`,
    `EvaluateGate` `:237-278`). On leaving it calls `fleet.Root.SetActive(false)` (`:274`), which runs
    each hull's `OnDisable`. That happens **after** the new region's hulls have registered.
- **In PlayMode tests the picture is different.** `WharfNightStage.Load` uses `LoadSceneMode.Single`
  (`WharfNightStage.cs:99`), so a test's previous region *is* destroyed and its scene hulls free
  through `OnDisable`. `DontDestroyOnLoad` hulls (the fleet, and the Dory) survive. The pool is
  static per domain (`IsoFacetHullRegistry.cs`), so within one PlayMode run the pool's state depends
  on the order the tests run in.

**Verdict.** St Peters gives back only its ambient punts, and late. Its parked vehicles never give
back in the game. In tests they give back on a Single load.

### 2.2 Disable and destroy

- `IsoFacetHullRenderer` is `[ExecuteAlways]` (`:97`).
  - `OnEnable` (`:778-790`) takes the single (`Register`, `IsoFacetHullRegistry.cs:41-46`) and the
    block (`RegisterForeBlock`, `:73`), **only when `_hullId == 0`**.
  - `OnDisable` (`:792-806`) frees both (`Unregister` `:75-79`, `UnregisterForeBlock` `:83`) and
    calls `ClearDeckSlots`.
  - `OnDestroy` → `ReleaseOwned` (`:809`).
- `FreeId` never pools 0 or 255 (`IsoFacetHullRegistry.cs:252-256`), so a hull that was sharing 255
  gives nothing back, which is correct.
- **Weak spot:** a hull refused a block in `OnEnable` **never asks again**, even after blocks free
  up. The block is taken in only one place.

### 2.3 Hull swap

- `BoatController.SetHull` (`BoatController.cs:494`) → `BoatHullSkinner.ApplyHull` (`:185`).
  - **Mesh → mesh:** `IsoFacetHullPresentationService.Install` (`:31-60`) gets the existing renderer
    and re-`Configure`s it (`IsoFacetHullRenderer.cs:512`). It **keeps its ids**. Nothing leaks and
    nothing is taken twice.
  - **Mesh → sprite (or unskinnable):** `RemoveMeshPresentation` (`BoatHullSkinner.cs:619-630`) →
    `Service.Remove` (`IsoFacetHullPresentationService.cs:413-427`) → `Destroy(renderer)`. Its
    `OnDisable` frees both.
- Trucks follow the same path: `VehicleSkinner.Apply` and `Remove`, `ParkedVehicle.cs:97` and `:102`.

### 2.4 A figure entering and leaving ashore

- **Entering:** `EnterAshore` (`IsoCharacterFigureRenderer.cs:418-450`) refuses under a hull, then
  takes one single (`:438`). If it gets 0 it returns false, and the figure stays a sprite.
- **Leaving:** `LeaveAshore` (`:456`) → `ReleaseAshoreId` (`:473-482`) → `UnregisterFigure`
  (`IsoFacetHullRegistry.cs:110-114`). That is double-release safe through the `s_FigureIds` set.
- **Disable:** `OnDisable` → `ReleaseAshoreId` (`:633`).
- Figures and hulls share **one singles stack** (`TakeFigureId` pops `_freeIds` first). A figure can
  therefore reuse a freed hull single, but **never** a freed block. Blocks and singles never cross.

### 2.5 Deck-slot claims (what a lazy block would hang on)

**Callers.** Everything that stands on a deck goes through one choke point, `ClaimSlot` and
`ReleaseSlot` (`IsoFacetHullRenderer.cs:1044-1075`, exposed as `DeckOccupants.Claim` and `Release` at
`:1145-1146`):

- **The moored skipper:** claims at `MooredBoat.cs:438`, releases at `:171`.
- **The player aboard:** claims at `DeckRiderVisual.cs:829`, releases at `:870`.
- **Stern-deck gear:** claims at `SternDeckGearPresenter.cs:381`, releases at `:596`. Nothing
  installs the gear presenter yet.
- **The legacy single occupant:** `SetDeckOccupant` at `:968-979`. It deliberately claims nothing
  while "nobody is aboard".

**How the block reaches the shader.** The fore id is written to the property block in every
`LateUpdate` (`ApplyPose`, `:813`, `:941-943`). A block that appears or goes away between frames
therefore reaches the shader on the next frame with no extra work.

---

## 3. What any fix must keep

- **The fore block stays contiguous and exactly `DeckOccupantSlots` wide.** The shader matches it as
  a range, `_HullIdFore … +span` (`HiddenHarboursIsoFacetOverlay.shader:66-68`,
  `HiddenHarboursDeckOccludedSprite.shader:139-141`, `HiddenHarboursLampShadow.shader:136-138`).
- **No id is handed out while it is still held.** Phase B's third test.
- **Running out still refuses loudly and draws un-occluded.** It is never silent, and it is never a
  shared figure id. (A figure never shares 255, `:213-224`.)
- **Nothing here touches simulation state.** Ids are presentation only, so rule 5 (determinism) is
  not in play. Ids are never saved.

---

## 4. The options

Headroom figures below are for **cold NMC** / **the worst case in play** (§1.3: 41 hulls, with the
player aboard her own hull).

### 4.1 Fore blocks taken lazily, only when a rider boards: **recommended**

**The change.**

- A hull takes its single in `OnEnable` as now.
- It takes its 12-block on the **first** `ClaimSlot`, and frees it when the **last** slot is released
  (and in `OnDisable` as now).
- A refused claim behaves as a refused slot does today: a loud warning, and the occupant draws
  un-occluded.
- A hull refused once **retries on the next claim**. That fixes the "never asks again" weak spot in
  §2.2 by construction.

**Who pays a block at NMC.**

- 6 skippers stand aboard their moored boats from `Present` on, so they hold blocks all the time.
- The player's own hull holds one while she is aboard.
- The 23 review hulls, the 3 trucks, `CampbellHughie`'s boat and the ambient punts carry nobody, so
  each pays **1**.

| | Singles | Blocks × 12 | Used | **Free** |
|---|---:|---:|---:|---:|
| Cold NMC | 33 | 6 × 12 = 72 | 105 | **150** |
| Worst in play (41 hulls; the Dory boarded) | 41 | 7 × 12 = 84 | 125 | **130** |
| … plus the cast ashore at NMC (3 figures) | 44 | 84 | 128 | **127** |

**Put another way,** the worst case in play still has room for about 10 more hulls with people
aboard, or 130 more empty hulls or figures.

**Why it holds up over a session.** Freed blocks go back on the block stack and are reused first, and
singles recycle the same way. So the high-water mark is bounded by the *peak held at one time*:
peak singles + 12 × peak boarded hulls. The late free from the ambient fleet (§2.1) therefore costs
at most one transient overlap. It no longer costs a permanent loss.

**Cost.**

- **Files:** `IsoFacetHullRenderer` (the `OnEnable` / `OnDisable` split, and claim and release
  counting), and `IsoFacetHullRegistry` unchanged, or with one helper. No shader, no render target,
  no scene or data change.
- **Risk to watch:** the `OnDisable` → `ClearDeckSlots` → `OnEnable` path must keep the rule that a
  holder re-claims. `SternDeckGearPresenter` re-asserts every draw. `MooredBoat` claims once in
  `StandTheSkipper`, which `Present` runs from `OnEnable` (`MooredBoat.cs:167`). Phase B checks that
  a disable and re-enable brings back both the block and the skipper's slot.
- **Headroom depends on deck traffic.** It is not a fixed number of hulls. A future scene of 20
  crewed boats in one harbour would cost 260 again. §5 names the guard for that.

### 4.2 Ids held only by hulls in or near the view

**The change.** A hull outside the camera's rect plus a margin gives back its single and block. It
re-takes them when it comes back into view.

- **It fails on its own.** The worst view is 22 hulls (§1.4), and 22 × 13 = 286. The widest aboard
  band on a 26 m-ruled boat cannot be served with eager blocks.
- **Combined with 4.1 it works, but 4.1 alone already leaves 130 free.** The culling would add
  moving parts for nothing:
  - a per-frame visibility pass over every registered hull;
  - hysteresis so a hull on the edge does not churn;
  - slot holders re-claiming against a block that changed hands. `MooredBoat` claims once (`:438`),
    so its index would go stale.
- **Many cameras.** The hull-screen target is per camera (`GetResolveTarget` keyed by camera,
  `IsoFacetHullFeature.cs:945-957`). "In view" means in view of *any* rendering camera, and that
  makes the test harder to get right.
- **Worth keeping** as the next step if a later scene outgrows 4.1. Not now.

### 4.3 Fewer slots per block

With eager blocks, the budget is **255 ÷ (1 + slots)** hulls.

| Slots | Hulls that fit | Cold NMC (33) | In play (41) |
|---:|---:|---|---|
| 12 (today) | 19 | 429: **no** | 533: **no** |
| 10 (the measured worst beat, no margin) | 23 | 363: **no** | 451: **no** |
| 6 | 36 | 231: **24 free** | 287: **no** |
| 5 | 42 | 198: 57 free | 246: **9 free** |

**Five slots or fewer fits, and it breaks the measured case.** The class note on `DeckOccupantSlots`
(`IsoFacetHullRenderer.cs:373-395`) records ten things on a lobster boat's deck in the stern-deck
loop's worst beat. It also costs a lockstep change to the shader literal `HH_DECK_OCCUPANT_SLOTS`,
guarded by `DeckOccupantSlotTests`. And it still spends 6 ids on each of the 26 hulls that carry
nobody. **Rejected.**

### 4.4 A 16-bit id channel, and its mobile cost

**Where the id lives.** It is carried as `alpha × 255` in the facet colour targets (`R8G8B8A8_SRGB`,
`MakeColorTarget`, `IsoFacetHullFeature.cs:933-943`). It then reaches the persistent per-camera
`_HHHullScreenTex` (`ARGB32`, `:945-957`), whose RGB is the resolved hull colour the overlay draws
(`HiddenHarboursIsoFacetOverlay.shader:89-95`). So there is no spare 8-bit channel to borrow.

**What 16 bits needs.**

- A second target for the id: R16, or RG8 holding a high and a low byte.
- It is written alongside the facet colour target and carried through the resolve into a second
  persistent texture.
- Every `* 255.0` decode is rewritten in the overlay (both passes, `:66-68`, `:153-155`), the
  deck-occluded sprite (`:139-141`), the resolve (`HiddenHarboursIsoFacetResolve.shader:22`, which writes `id/255`) and the lamp shadow (`HiddenHarboursLampShadow.shader:136-146`).
- That work sits in art-pipeline's shaders and `IsoFacetHullFeature`, and it needs GPU plates.

**Capacity:** 65,535 ids (UNorm16), or 2,048 exact in half float.

**Cost:**

- **Memory:** 2 bytes per pixel per id target, and at least two (the per-frame graph target and the
  per-camera resolve):

  | Screen | Per target | Two targets |
  |---|---:|---:|
  | 1920×1080 | 4.1 MB | **~8.3 MB** |
  | Phone at 2400×1080 | 5.2 MB | **~10.4 MB** |

- **Bandwidth:** one more full-screen 2-byte write and several reads every frame.
- **On mobile:**
  - **Tile memory.** On tile-based GPUs an extra attachment uses on-chip tile memory, which can force
    smaller tiles or spills.
  - **GLES3 support.** A 16-bit integer target (R16_UInt) is renderable but needs integer sampling.
    R16_UNorm is not core (it needs `EXT_texture_norm16`). R16_SFloat needs
    `EXT_color_buffer_half_float`. RG8 is renderable everywhere, at the same 2 bytes per pixel.
- **Rule 7:** this is a permanent per-frame cost paid to seat hulls that carry nobody. That is
  exactly the corner rule 7 says not to paint the port into.

**Verdict.** It is the right answer only if a future scene needs more than about 250 *simultaneously
boarded or visible* takers. Nothing on the roadmap does. **Not now.**

### 4.5 Considered and folded in

- **Take the review anchorage off meshes.** That breaks the owner's 2026-08-14 ruling. It is also
  unnecessary, because under 4.1 the 23 review hulls cost 23 ids in total.
- **Give trucks singles only.** 4.1 does this for free, and a truck still gets a block the day a
  rider stands in its bed.
- **Unload regions on travel.** It would free St Peters' vehicles. It is a world and scene decision
  (ADR 0004 additive regions), not a facet-budget one, and 4.1 makes it unnecessary for this bug.

---

## 5. Recommendation

**Take option 4.1.** Fore blocks are taken on the first deck claim and freed on the last release.
Everything else stays as it is: 8-bit ids, 12 slots, eager singles and eager figure ids.

- **Headroom at NMC:**
  - **150 of 255 free** on a cold load.
  - **130 free** in the worst case in play (41 hulls, 7 boarded).
  - **127 free** with the cast ashore.
- **`Ashore_BesideTheTrucks_NineMileCreekNoon`** should pass. The player's figure id comes from a
  pool with 150 free, and the known red's own failure message names a starved pool
  (`AshoreFigurePlatePlayTests.cs:655-690`).

**Phase B, proposed for the owner's ruling:**

1. EditMode pool tests, as the handoff lists them:
   - **Budget:** 33 unboarded hulls + 6 boarded + 1 figure fit.
   - **Recycling:** a freed block is reused as a block, and a freed single serves a figure.
   - **No id reused while held.**
2. **Lazy-block tests:**
   - The first claim takes the block and the last release frees it.
   - A hull refused once gets a block on a later claim after one frees.
   - A disable and re-enable brings back the block and the skipper's slot.
3. **Refresh the stale budget prose.** "A roadmap whose largest scene is a harbour of a dozen"
   (`IsoFacetHullRegistry.cs:66-71`, `IsoFacetHullRenderer.cs:386-390`) predates the 23-hull
   anchorage.
4. **Optional, and the owner's call:** a no-GPU EditMode guard. It reads each shipped region's
   scene-export package and fails when *singles + 12 × skippered hulls + figures* exceeds 255 minus a
   reserve. That guard is what would warn before a future crowded harbour (§4.1, last point) spends
   the headroom.
5. **One owner-granted GPU slot** in which `Ashore_BesideTheTrucks_NineMileCreekNoon` passes.

---

## 6. ADR form: a new ADR 0045, not an amendment to 0044

**Why not amend 0044.** The pool is shared by three kinds of taker, and the three were introduced in
three places:

- hull ids, in ADR 0022;
- deck-occupant fore blocks, in the boat-interior and occupant work;
- figure ids, in ADR 0044 §7.3 and #861.

The rule being decided is about **hulls and trucks**: when a hull holds a block. Amending ADR 0044,
which is about characters, would put a hull-side budget rule where the next hull or truck lane will
not look.

**Proposal:**

- **New ADR 0045, "The facet id space is a budget".** It states:
  - the 255-id space and its three kinds of taker;
  - the lazy-block rule (§4.1);
  - the retry-on-claim rule;
  - that running out is loud and draws un-occluded, never shared for figures;
  - the headroom at the time of the ruling;
  - the guard, if the owner wants it.
- **One-line pointers** to 0045 from ADR 0044 §7.3 (figure ids, at about :547) and from ADR 0022.

0045 is the next free number on this branch. If another branch claims it first, take the next one.

---

## 7. Found and not fixed (for the seat and the owner)

1. **Regions never unload in the game** (§2.1). St Peters' vehicles hold their ids for the rest of the
   session. Harmless under 4.1, but it is a world-lane fact worth knowing for any per-region budget.
2. **The ambient fleet frees late,** up to 0.5 s after the active scene switches, which is after the
   next region registered. That explains the ledger's run-1 extra 5 warnings.
3. **A hull refused a block never retries** (§2.2). This fixes itself under 4.1.
4. **Which hulls go without is arbitrary** (scene order). Today it can be an owned boat whose skipper
   then draws over her cabin.
5. **PlayMode pool state depends on test order.** The pool is static per domain and some hulls are
   `DontDestroyOnLoad`, so a test's budget depends on which tests ran before it in the same run.
6. **The budget comments are stale** ("a harbour of a dozen"; §5 item 3).
