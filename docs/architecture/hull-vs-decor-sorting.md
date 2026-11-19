# Hulls against decor: why a rock can draw over a beached boat, and the fix

**Status:** decided 2026-11-17 (§8: the owner took all three recommendations). Rollout in progress, §7.
**Serves:** P1 (the sea has moods: a boat run up on the foreshore must read as lying *on* it),
P3 (a living working coast: boats, people and scenery interleave by where they are), rule 7.
**Related:** ADR 0032 (the Y-sort band, and "a fixed order on an upright world object is a bug by
default"), ADR 0022/0023 (mesh hulls, the displaced water and their shared depth), ADR 0056 (the
facet-id budget and the deck-occupant split), draft ADR 0059 (the decision this document argues for).
**Measured on:** `main` at `428ab210d7`. Line numbers below are on that commit. Open draft PR #1281 is
quoted where it changes the picture, and marked as such.

---

## 1. The report

> "it seem that sprites appear above the boat when they should be below/behind" (the owner, 2026-11-16)

Seen at St Peters with a Cape Islander run aground: rockweed and grass drew over her stern. PR #1281
fixes the plant-field part. Its own notes leave two things open, and this document takes both up:

- **Known limitation.** Decor-band sprites that are not plant-field sites (`YSortSprite` rocks and
  grass) can still draw over a beached hull. A full fix moves hulls into the decor band, and that
  collides with the fixed-order water effects that sit just above hulls.
- **Side effect.** Lying weed offshore now sorts under the sea plane.

## 2. The ladder today

There is one sorting layer ("Default"), so a renderer's order is its only claim to a place in the
stack (`Core/Art/SortingBands.cs:3-9`). Low draws first. Everything below is a fixed order unless it
says "Y-sort".

| Order | What | Where it is set |
|---|---|---|
| -21 | Terrain splat ground | `Art/TerrainSplatSurface.cs:31` |
| -21 to -18 | Painted seabed | `Core/Art/SortingBands.cs:30-32`; St Peters shore paint `App/Editor/StPetersShorePainter.cs:50-52` |
| -9 | Ground prints (footprints, tyre marks) | `Core/GroundPrints/GroundPrintSettings.cs:194`, applied `Art/GroundPrintRenderer.cs:130` |
| -8 | Submerged shore plant | `SortingBands.cs:36`; `Art/ShorePlantDef.cs:112`; plant field `Art/PlantFieldDefinition.cs:21` |
| **-6** | **(PR #1281) `UnderHulls`: lying weed, ground cover, a standing plant behind a hull** | `SortingBands.UnderHulls` in #1281 |
| **-5** | **The sea plane**, and the displaced water's overlay at the same slot | `SortingBands.cs:39`; scene templates `App/Editor/WaterSceneTemplate.cs:330`, `App/Editor/LandSceneTemplate.cs:263`; overlay `Art/DisplacedWaterSurface.cs:651-653` |
| -4 to 1 | Wharf deck rows, north to south; NMC wharf faces and float | `SortingBands.cs:43-45`; `App/Editor/NineMileCreekDressing.cs:1048-1057`; `App/Editor/NineMileCreekWharf.cs:433,462` |
| -3 | Drifting seaweed (floats on the water) | `Art/SeaweedDef.cs:165` |
| -3 | Fish schools | `Fishing/FishSchoolPresenter.cs:47` |
| -3, -2, -1 | Wake plume, crest lines, foam; wake spray and bubbles -1 | `Boats/BoatWakeEmitter.cs:188-204` (parented to the emitter's own host, not the boat: `:1022-1023`, `:555`) |
| -2 | Sea mist | `Art/SeaMistEmitter.cs:40` |
| -1 | Dory oar splashes | `Boats/DoryOarSplashSystem.cs:120` |
| -1 / 1 | A hull north of the St Peters pier drops behind it | `App/StPetersPierBoatSorting.cs:11-29` |
| 0 | Ambient fleet hulls | `Boats/AmbientFleetDef.cs:41`, applied `Boats/AmbientFleetPresenter.cs:314-359` |
| **1** | **Every hull picture** (sprite or mesh overlay, in its `SortingGroup`) | `Boats/BoatVisualDef.cs:154`; sprite `Boats/BoatHullSkinner.cs:263`; mesh `Boats/BoatHullSkinner.cs:440` via `Art/IsoFacetHullRenderer.cs:551-560` |
| 1 | Interior room sheet | `SortingBands.cs:49` |
| hull -2 to hull +5 | Outboard parts (by heading) | `Boats/OutboardMotorMath.cs:245-253` |
| hull +1, +2 | Dory oars | `Boats/BoatHullSkinner.cs:763-765` |
| hull +1 | Moored boat's skipper | `Boats/MooredBoat.cs:275` |
| 2 | Boat interior room above the hull | `Boats/BoatInteriorInstaller.cs:76,172` |
| hull overlay -1 | Hull sun shadow, legacy arm | `Art/HullSunShadowCaster.cs:113-115` |
| **2 to 2402** | **The decor band: Y-sorted, `round(1202 - 4·y)`** | `SortingBands.cs:57-86`; `Art/YSortSprite.cs:153-169` |
| 3 | Emergent shore plant (the old sprite path) | `Art/ShorePlantDef.cs:116`, `Art/ShorePlantTideView.cs:241` |
| 3 | Trap buoys, ambient fleet buoys | `Boats/TrapBuoyPresenter.cs:40`; `Boats/AmbientFleetDef.cs:126` |
| 5 | Wind spray off the crests; surf spray | `Art/SprayEmitter.cs:65`; `Art/SurfSprayEmitter.cs:36` |
| 6 | Wade splash; the "three moments" burst | `Art/WadeSplashEmitter.cs:185`; `Art/MomentBurstEmitter.cs:120` |
| 8 | Dust motes | `Art/DustMotes.cs:26` |
| 15, 20, 50, 51 | Rod fight: ripple, fish shadow, line, splash | `Player/RodFightPresenter.cs:161-165` |
| 30 | NPC pot day catch | `Fishing/NpcPotDayDef.cs:170` |
| 50, 51 | Deck containers, their freshness tag | `Boats/DeckContainerPresenter.cs:46`; `Boats/DeckFreshnessTag.cs:53` |
| 60 | Masthead telltale | `Boats/MastheadTelltale.cs:50` |
| Y-sort | Grass field chunks (one order per chunk) | `Art/GrassField.cs:791-801` |
| Y-sort | Plant field chunks (one order per chunk); in #1281 lying growth and standing-behind go to `UnderHulls` | `Art/PlantField.cs:227-228,286-288`; #1281 `Art/PlantHullSorting.cs` |
| Y-sort | Cliff walls | `Art/CliffWallSurface.cs:544-546` |
| Y-sort | Decor shadows, legacy arm (clamped into the band) | `Art/SpriteShadow.cs:925-929` |
| Y-sort | Stern deck gear | `Boats/SternDeckGearPresenter.cs:443,678` |
| Y-sort | The player and every figure (sprite, or the mesh figure's overlay from its sort source) | `YSortSprite` on the player; `Art/IsoCharacterFigureRenderer.cs:1073-1077` |
| Y-sort | The rider aboard mirrors the player's Y-sorted order | `Player/DeckRiderVisual.cs:100-104,812` |
| Y-sort, one rung behind | Mesh vehicles (PR #1266) | `Vehicles/VehicleMeshDriver.cs:124-148` |
| Y-sort | A beached wreck's hull | `Boats/BeachedWreck.cs:68-73` |
| Y-sort + cut-away | The NMC fuel-stop canopy (PR #1266): a Y-sorted set piece whose roof goes thin while the player is under it | `App/Editor/NineMileCreekWharfLandSide.FuelStop.cs:145,171-200` (the `BuildingInterior` rule) |
| 2402 + 100 | `AboveDecor`: mooring rope, anchor rode, trap-haul rope, rain, gulls aloft | `SortingBands.cs:98`; `Boats/BoatMooring.cs:763`; `Boats/BoatAnchor.cs:353`; `Fishing/TrapHaulController.cs:604`; `Art/RainEmitter.cs:57`; `Art/GullFlock.cs:343` |
| 32760 to 32767 | Day/night tint, sun shade (screen-space arm for hulls, decks and plants), lamp glow and lamp shadow | `SortingBands.cs:107-144`; `Art/HullSunShadowCaster.cs:113`; `Art/HullDeckShadowCaster.cs:81-82`; `Art/PlantField.cs:338-339` |

Three things in the hull stack are **not** decided by sorting order at all, and any option must leave
them alone:

- **The waterline on the hull.** Water and hull facets share one private depth buffer; a hull fragment
  under the lifted sea fails the depth test and the sea shows there (`Art/IsoFacetHullFeature.cs:55-67`).
- **Deck occupants behind the wheelhouse.** A figure aboard is hidden by the cabin through the facet-id
  split, not by order (`Art/IsoFacetHullRenderer.cs:133-140`, `:447`; `Art/IsoFacetHullFeature.cs:69-80`;
  the comment at `Boats/MooredBoat.cs:272-274` says why order cannot express it).
- **Foam.** Hull-contact foam, the wake fan and the foam transport are composited inside the sea's own
  draw through a global buffer (`Art/FoamInjectionRegistry.cs:311`), so they live at the sea's order.

## 3. Why a rock or a grass tuft draws over a beached hull

1. A rock is a `SpriteRenderer` with a static `YSortSprite`. Its order is
   `clamp(round(1202 - 4·y), 2, 2402)` (`Art/YSortSprite.cs:165-169`). **It is never below 2.**
2. A hull's picture sorts at `BoatVisualDef.SortingOrder`, which is **1** for every hull
   (`Boats/BoatVisualDef.cs:154`; every library entry `App/Editor/BoatVisualLibraryBuilder.cs:189-411`).
   The tooltip there still says the on-foot player sits at 10; that predates ADR 0032 and is stale.
3. Same layer, so 2 beats 1 everywhere. Position never enters into it: **any decor sprite whose picture
   overlaps a hull's picture draws over the hull**, north of her or south of her.
4. At sea this almost never shows, because almost nothing in the decor band is out on the water. On
   the foreshore, at a slip, beside a wharf or beached, decor is all round her and it shows at once.

The same is true of grass-field chunks (`Art/GrassField.cs:791`), cliff walls
(`Art/CliffWallSurface.cs:544`), emergent shore-plant sprites at 3 (`Art/ShorePlantDef.cs:116`) and any
sprite figure ashore, because all of them sort at 2 or above. A villager walking north of a moored
hull draws over her too. Mesh figures may be partly protected, because their own pass depth-tests against
hull facets (`Art/IsoFacetFigureTarget.cs:20-80`). That is not verified in a play-look and is not
relied on below.

ADR 0032 already names this class of bug: "A fixed sorting order on an upright world object is now a
bug by default." Hulls are the largest remaining case. Two precedents have closed it for one object
each: `BeachedWreck` (`Boats/BeachedWreck.cs:68-73`) and mesh vehicles (`Vehicles/VehicleMeshDriver.cs:118-148`).

## 4. Why "just put hulls in the band" is not one line

Moving `BoatVisualDef.SortingOrder` from 1 to a Y-sorted ~1200 fixes the rock. It also breaks:

- **Everything that rides on the hull at a fixed order that happens to be low in the band.** Deck
  containers at 50, their tag at 51 and the telltale at 60 would drop under their own hull. So would the
  interior room at 2 (`Boats/BoatInteriorInstaller.cs:172`). Oars, outboard and skipper are already
  hull-relative and survive.
- **The player aboard.** The rider mirrors the player's own Y-sort (`Player/DeckRiderVisual.cs:812`). On
  a hull sorted by her waterline pivot, a player standing aft of the pivot on a boat heading south would
  sort *under* the deck he stands on. A 27 m purse seiner spans about 108 orders.
- **Free water effects parked at 3 to 8.** Wind and surf spray (5), wade splash and the moment burst
  (6), dust motes (8) and buoys (3) would all draw under every hull, including a buoy or a splash south
  of her. This is the "fixed-order water effects" collision #1281 names.
- **Fishing.** The rod line (50) and splash (51) would draw under the hull the player fishes from.
- **The St Peters pier rule** (`App/StPetersPierBoatSorting.cs`) writes the same `SortingGroup` order
  every `LateUpdate`. Two writers on one value is a race.

Nothing in the water stack itself breaks. Wake (-1 to -3), foam (inside the sea), seaweed (-3) and fish
schools (-3) are all already under every hull and stay there. The waterline and the cabin occlusion are
not order-based. Hulls against wharf decks are unchanged, because decks sit below the band.

## 5. The options

### A. Hulls Y-sort in the decor band; everything aboard rides with the boat (recommended)

**What.** A boat is an upright world object, like a vehicle. Give the **boat root** a `SortingGroup`
whose order is the decor band's Y-sort at her waterline pivot, less one rung, which is the
`VehicleMeshDriver.OrderAt` rule. Everything parented under the root then sorts **as one unit** at that
order, with its existing local orders keeping their meaning inside the group: hull 1, oars +1/+2,
outboard -2 to +5, skipper +1, interior room 2, deck gear, containers 50, telltale 60. That covers
the player too, because he is parented to the boat's root while aboard
(`Player/ControlSwitcher.cs:2953-2958`). His Y-sorted ~1200 becomes a local order above the hull, the
cabin still hides him through the facet-id split, and on disembark he leaves the group and sorts in the
band again.

Re-anchor the free water effects that are parked at fixed orders 3 to 60: Y-sort each one at its
emission point when it spawns, or for rod line and splash, at the player's order +1. Retire the pier
rule as a second writer: the boat's sorting rule takes "behind this pier" as an input.

**What it fixes.** Rocks, tufts, grass chunks, cliff walls, shore-plant sprites and figures all
interleave with hulls by position. That covers beached, at a slip and alongside a wharf. Two hulls now
sort by Y; today they tie at 1 (the ambient fleet at 0 always draws under the player's boat, even north
of her). A hull passing north of a sea rock goes behind it.

**What it breaks or risks.**
- Anything under the boat root that must stay above all decor: the mooring rope and anchor rode are
  children of the boat (`Boats/BoatMooring.cs:750-751`, `Boats/BoatAnchor.cs:343-344`) at `AboveDecor`.
  Inside the group they would sort with the boat. Each gets its own `SortingGroup` with "sort at root",
  or moves to a world host. (Verify the "sort at root" option on Unity 6.5 in the PR.)
- **Long hulls lying up and down the screen.** One sort point per hull is a compromise. Decor inside her
  footprint and south of the pivot still draws over her stern. Lying growth is already handled by #1281's
  `LiesFlat` rule. What remains is standing decor *inside* a hull's footprint, which is a boat resting on a
  rock. Accepted, or refined later by sorting a grounded hull at her nearest waterline point (owner
  decision 3).
- The legacy hull sun-shadow arm sorts at the overlay -1 (`Art/HullSunShadowCaster.cs:114`), so it would
  move into the band with her. The screen-space arm (`SunShade`) is unaffected.
- `SortingBandsTests`' "nothing fixed in the band" intent tightens. The low-band literals listed above
  must all go (that is part of the work, not a side effect).

**Tests that pin it.**
- EditMode `HullSortingTests`: the order is `OrderAt(pivot.y)`, one rung behind; a rock north of the pivot
  sorts under her and one south over her; two hulls sort by Y; the pier input wins north of the pier.
- EditMode `BoatSortingGroupTests`: after `BoatHullSkinner.Apply` (sprite and mesh paths) the root
  carries the group; every attached renderer is inside it; the local relations hold (hull < oars, hull <
  skipper, hull < interior, hull < containers); the ropes escape it.
- EditMode guard: no `sortingOrder` literal between `DecorFloor` and `DecorFloor + 100` outside
  `SortingBands` (catches the next parked 5 or 50). This is the source-scan style the project already
  uses for other rules.
- EditMode `WaterEffectSortingTests`: spray, splash, burst and buoy orders follow their emission Y.
- PlayMode plate at St Peters (on a granted warm box): a beached Cape Islander with a rock and a tuft
  north and south, and the player north, south and aboard.

**Performance (rule 7).** One `RoundToInt` and a compare per boat per `LateUpdate`, with a write only
when the order changes. That is the vehicle pattern, about 30 boats at most in a region. One more
`SortingGroup` per boat. A hull now sits inside the decor run, so it can split the decor's batches into
before and after her: at most one or two extra batches per hull on screen. Effects compute their order
once per spawn from pooled state, with no allocation. Nothing touches the 35,000 static tufts, which keep
sort-once-then-disable.

### B. A per-hull footprint rule that pushes overlapping decor under

**What.** Keep hulls at 1. When a hull is near decor, find the decor inside her drawn footprint (and
behind her pivot), flip it to an under-hull order, and restore it when she leaves. This generalises
#1281's `PlantHullSorting.BehindAHull` from the plant field to every decor renderer.

**What it breaks.** Static decor self-disables after one sort (`Art/YSortSprite.cs`, the static vs
dynamic paragraph), so every flip needs a registry of 35,000 sprites by cell. Grass and plant fields
sort **per chunk**, so flipping one tuft means splitting or rebuilding a chunk mesh while a hull moves.
The only free order under the hull is under the sea plane (`UnderHulls` is -6, the sea is -5), so a
standing rock in the shallows pushed under would read as submerged. Figures (sprite or mesh) need the
same rule separately. Two hulls overlapping each other are still unsolved.

**Tests.** The #1281 `PlantHullSortingTests` shape, per renderer kind, plus registry restore tests.

**Performance.** A spatial query per moving hull, plus chunk rebuilds whose cost grows with fleet size
and density. That is exactly the cost the grass field was built to avoid. **Not recommended**, except
where it already exists: #1281 for lying growth.

### B'. The same rule on the GPU (a hull-coverage mask)

**What.** The hull feature already renders every hull off-screen with true view depth
(`Art/IsoFacetHullFeature.cs:17-28`). Publish a hull mask carrying the sort-Y of the hull covering each
pixel. Decor shaders then discard where a hull covers them and their own root is behind it. This is the
`SilhouetteThrough` idiom turned round (`Art/SilhouetteMaskFeature.cs:11-30`).

**What it breaks.** Every decor shader (sprite-lit decor, grass, plant field, cliff) gains a texture
read. Like the custom-axis sort ADR 0032 rejected, it lives on the render path and **cannot be asserted
headless**, because CI has no graphics device. Per-pixel cost on all decor fragments is a fill-rate
tax, which the later mobile port pays. **Not recommended.**

### C. Decor near the tide line moves to the under-hull band by rule

**What.** Anything authored in the intertidal strip, or flagged "lies flat", sorts at `UnderHulls`.

**What it breaks.** It is right for flat things only. A standing boulder at the tide line would draw
under a hull moored north of it, and under the player walking north of it, because the player
Y-sorts in the band above it. It also inherits the under-the-sea placement (§6). **Keep it for flat
decor only**, which is what #1281's `LiesFlat` already is for plants. Extend it to non-plant flat decor
(wrack lines, pebble scatters) as a data flag.

**Tests.** Flag-driven order tests, as `PlantHullSortingTests.LiesFlat`.

**Performance.** Zero runtime cost: decided once at build or enable.

### D. Real sorting layers (ADR 0032's deferred item)

Layers would make "under all decor" and "over all decor" structural, but a hull must **interleave**
with decor, which no layer boundary expresses. It is worth doing for the water stack's crowding (§6
shows -4 to 1 is shared by wharf decks and water effects), but it does not fix this report. **Not this
change.**

### E. Hull-attached renderers take explicit offsets instead of a group

A variant of A: no root `SortingGroup`; a single writer sets every attached renderer to `base + offset`
each time the base moves. It avoids nested groups, but every attachment needs registering, and the
player aboard needs a "carrier order" override in `DeckRiderVisual` and the figure renderer. It has
more seams than the group. Keep it as the fallback if nested groups misbehave with the hull overlay
quads in a PR 2 play-look.

## 6. Offshore lying weed: under the sea plane or over it?

**Under, by default.** #1281 sorts lying weed at -6, under the sea (-5). On dry ground the sea draws
nothing, so nothing changes. Where it now "reads through the water", the plant field's tide state
(`ShorePlantTideMath`) says emergent and the **drawn** sea, which lifts with the swell, covers it. The
drawn sea is what the player sees. Weed under it reads as weed under water, tinted by it, as the painted
seabed does. Over the sea, it was the old look of weed lying on top of the water, which is part of what the
owner reported.

But some weed really is at the surface when covered. Bladderwrack and knotted wrack float upright on
their air bladders, and sugar kelp and eelgrass trail along the top at low water. Irish moss and sea
lettuce stay down. That is a fact about the **species**, so it belongs in the species asset, not in the
field and not in code (rule 2):

- Append to `PlantSpeciesDef` (`Art/PlantSpeciesDef.cs:9-12`) a `WhenCovered` choice: `UnderTheSea`
  (default, value 0, so every existing asset reads it unchanged) or `OnTheSurface`.
- Add one Core constant, `SortingBands.FloatingWeed = -3`. That is the drifting seaweed's existing
  order (`Art/SeaweedDef.cs:165`): over the sea, under every hull and wake.
- `PlantHullSorting.OrderFor` (#1281) picks `FloatingWeed` for an `OnTheSurface` species where the sea
  covers it, and `UnderHulls` otherwise.
- Test: one EditMode case per value, plus a content test that every species with a tide ladder declares
  it deliberately.

While there: #1281 decides "lies flat" on the **field** (`GroundCoverMaxHeightM`, `LyingHabitats`
strings). The same species-not-field argument says it should become a per-species `Posture` (stands /
lies flat), with the height and habitat rule kept as the editor default that fills it in. The current
data catches six species by habitat (fringe: eelgrass, Irish moss, sugar kelp; mid: bladderwrack,
knotted wrack, sea lettuce) and a few dry-ground ones by height (beach pea, glasswort, marsh marigold,
lowbush blueberry).

## 7. Recommendation and the order of small PRs

**Option A**, with C kept for flat decor and the weed choice made per species. Each PR below leaves
`main` green and playable, and none changes the save.

1. **Core, no behaviour change.** `SortingBands.HullYieldRungs` (1, the vehicle rule) and `FloatingWeed`
   (-3); a pure `HullSorting.OrderAt(pivotY)` beside the vehicle's; `HullSortingTests`. (lead-architect)
   *As built:* `FloatingWeed` is left out, because the owner ruled that covered weed draws under the sea
   and no species floats yet (§8 decision 2). `HullSorting.OrderFor(HullFootprint)` adds the sort point
   (decision 3: the footprint centre, which is the root).
2. **Re-anchor the parked effects first**, while hulls still sit at 1, so nothing vanishes later: spray,
   surf spray, wade splash, moment burst, buoys, emergent shore-plant sprites, NPC catch and rod fight
   line and splash. Add the low-band literal guard test. Visible change: these now interleave with decor
   by position instead of drawing under all of it. (art-pipeline, gameplay-systems for the rod)
   *As built:* Core `EffectSorting` holds the rungs (spray 0, a splash at somebody's feet -1, a trap
   splash +1 over its buoy, buoys 0, emergent plants 0, the NPC catch 0 at the boat's pivot when it has
   no skipper, the rod line and splash +1 over the fisher). The pooled emitters (spray, surf spray, wade
   splash, moment burst, trap splash) drop their one host `SortingGroup` for one per particle, ordered
   at spawn. `EffectSortingTests` pins the table and the call sites and adds the low-floor guard, with a
   listed row for every literal still on main. Left as open points, not named in this step: dust motes
   (8), the NPC haul rope and the pot at the rail (2), the rod fight's bobber and surfaced fish (49),
   deep shape (20) and sink ripple (15), and the editor-baked nav buoys (3, a re-bake). The deck-borne
   orders (interior 2, containers 50, telltale 60, pot-deck props 51 and 52) wait for step 3's group.
3. **The boat group.** A root `SortingGroup` installed by `BoatHullSkinner` on both paths, Y-sorted
   each `LateUpdate`. The ropes escape it. The pier rule becomes an input. Ambient fleet, NPC pot boats,
   moored boats and the beached wreck all go through the same rule (`BeachedWreck` loses its private copy).
   `BoatSortingGroupTests`, plus the St Peters plate on a granted warm box. **This is the PR that fixes
   the report.** (gameplay-systems, lead-architect review)
4. **Retire `PlantHullSorting.BehindAHull`** (#1281). With hulls in the band, a standing plant's
   ordinary Y-sort gives "behind the pivot, under her" by itself, and without sending it under the sea.
   Keep `LiesFlat`. (art-pipeline)
5. **Species data.** `WhenCovered` and `Posture` on `PlantSpeciesDef`, the content test, and the six
   intertidal species set as the owner rules. (art-pipeline) *After the 2026-11-17 ruling:* `Posture`
   only; `WhenCovered` waits until a species is ruled to float.
6. **Flat non-plant decor** (option C) as a data flag, if a play-look after PR 3 still shows flat decor over
   a hull. (world-content)

#1281 should land first as it is. Nothing in it is wasted: `UnderHulls` and `LiesFlat` stay, and only
`BehindAHull` is retired in step 4.

## 8. Decisions for the owner

**Ruled 2026-11-17:** the owner took all three recommendations ("i go with leans"): option A and ADR 0059
accepted; covered weed under the sea plane, with no species floating; the sort point is the waterline
pivot amidships.

1. **Hulls are upright world objects and sort among the scenery by where they lie** (option A, draft ADR
   0059). Yes or no.
2. **Offshore lying weed: under the sea plane** (recommended) or over it. And should any species float at
   the surface when the tide covers it? The candidates are bladderwrack, knotted wrack, sugar kelp and
   eelgrass.
3. **A hull's sort point: her waterline pivot amidships** (recommended; matches the beached wreck and
   #1281) or, when she is aground, her nearest waterline point. The second hides more of a rock under a
   long hull lying up the screen, at the cost of drawing her over things beside her stern.
