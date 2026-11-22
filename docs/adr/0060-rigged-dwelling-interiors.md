# ADR 0060: Rigged dwelling interiors: a parked home is a facet mesh that does not move

- **Status: Accepted** (2026-11-19). The owner accepts or rejects it in its PR (decisions for the owner,
  below). The index row in `README.md` is added when it is accepted, the same way as ADR 0059, so this draft
  does not collide with the open index edits for 0058 and 0059.
- **Date:** 2026-11-17
- **Decision owner:** owner; technical steward lead-architect (the Core contracts and the sorting rule).
  Review: art-pipeline (the bake and the presentation service), world-content (the prefab, the placement
  and the fixtures), tools-editor (the builder), economy-sim (the stores and the deed gate), qa-test (the
  coverage and parity guards). Rig sources stay with art-director.
- **Serves:** P2 Dory to Dynasty (the camper is the first home he owns, and the first rung of the homes
  ladder), P5 Cozy but with Teeth (a stocked, warm room of his own is the cozy answer to hunger, sleep and
  cold), P3 Living Working Coast (homes you can walk into, true to their size). Rule 7: one draw per dwelling.
- **Related:** ADR 0003 (content is data), ADR 0004 (scene per region, prefabs), ADR 0019 (create once,
  refresh), ADR 0020 and save v25 (placed and stored things), ADR 0021 (in-engine rig bake), ADR 0022 (mesh
  hulls), ADR 0034 (a facing is a ground bearing), ADR 0035 (vehicles: a second door onto the facet
  renderer), ADR 0036 (a storey is a layer), ADR 0037 (the rest anchor), ADR 0038 and 0041 (boat interiors,
  full mesh rooms), ADR 0048 (each hull's rig carries its room), ADR 0056 (the facet-id budget), ADR 0059
  draft (hulls sort among the scenery).

## Context

The owner's rulings that shape this:

- **2026-07-30:** interiors are seamless (no scene load, no separate screen, no camera cut, no input-mode
  change) and true to the footprint.
- **2026-11-14:** mesh interiors with cutaways on every boat and on the St Peters buildings. The cutaway
  rule is: standing inside a level cuts that level; outside, the exterior is whole.
- **2026-11-17:** the camper on Ginny's lot is "just a better home", never towable. Storage that remembers
  goes in at Ginny's, in the camper and in a small shed on the camper's lot (save v25). Kitchens and baths
  go on north walls in homes and workplaces the player can buy or work in.

What the repository has today:

| Thing | Source | How it is drawn now |
|---|---|---|
| Boat hulls and their rooms | hull rig plus room rig or walk sidecar (ADR 0048) | `HullMeshDef` (shell and room in one mesh, level tags, `_RampMetaInterior[24]`), `IsoFacetHullRenderer`, `BoatCutaway` |
| Road vehicles and trailers | vehicle rig plus `road_vehicle` sidecar | `VehicleMeshDef`, installed by `IsoFacetVehiclePresentationService` onto the same `IsoFacetHullRenderer`, without foam or reflection (ADR 0035) |
| Houses and wharf buildings | `houseIsoRig.js`, `wharfBuildingRig*.js`, `interiorIsoRig.js` | sprite shell (`BuildingRigBaker`), sprite room (`InteriorRigBaker`), Y-sorted furniture sprites, swapped by `BuildingInterior` on a rectangular `InteriorFootprint` |
| The camper shell (Bantam, Clipper) | `camperIsoRig.js` plus `gameplay/dwellings/*.gameplay.json` | sprite sheets (`CamperSheetBaker`), covered by `DwellingRigFleet` |
| The Clipper's room | `camperInteriorRig.js` plus `camperInteriorRig.clipper.interior.json` (open PR, catalog key `camperInterior`, prerequisite `camper`) | **not drawn** |
| The lot's small shed | `smallShedRig.js` plus `smallShedRig.lot_shed.gameplay.json` (open PR, one rig for shell and room) | **not drawn** |

The gap is that nothing bakes or renders the room of a home that does not move. The camper door therefore
still refuses entry (`_interiorStood: 0`). Three existing paths come close, and each is wrong in one way:

- `HullMeshDef` and `BoatCutaway` know rooms and cuts, but they also carry a boat: rock, waterline clamp,
  foam, reflection, deck sidecars, flotation, and a cut that listens to `CabinSignals` and `HelmSlot`. The
  hull coverage law finds rigs by their `rollA`, which a parked dwelling has zero times. That is why
  `DwellingRigFleet` exists.
- `VehicleMeshDef` is a mesh without a boat, but it needs a chassis and wheels (`IsUsable` refuses without
  a chassis), and it has no room and no cut.
- `BuildingInterior` is the seamless-interior runtime (one footprint, one threshold, storeys as layers,
  the one-shot wake storey), but it swaps `SpriteRenderer`s and its inside test is a rectangle. The
  camper's floor is the shell's 82-point `SOLE` polygon, with tumblehome.

The question is how a land dwelling (the camper, the shed, and later the cottages) goes from rig source to
a baked mesh, then to a placed prefab in its region scene with a seamless interior. It also covers how its
fixtures bind to Def ids, how it sorts under the hull-sort rule, and how the existing sprite dwellings
migrate.

## Options

### A. A dwelling is a hull that does not float

Bake the camper and its room through `RigMeshAssetBaker` into a `HullMeshDef`, install it with
`IsoFacetHullPresentationService`, and cut it with `BoatCutaway`.

- **For:** no new Core type, and the cut, the level gate and the interior ramp table already work.
- **Against:** every hull law has to learn an exception. The presentation service attaches a
  `FoamInjector` and a `ReflectiveObject`, which would lay foam on the gravel and reflect the camper in a
  harbour it is not near (the same reason ADR 0035 gave vehicles their own door). `BoatCutaway` takes its
  "inside" from `BoatInterior` and `CabinSignals`, so a second definition of inside would sit beside
  `BuildingInterior`, which ADR 0036 rules out. The hull fleet tables, deck occupancy tables and the
  `MeshInteriorHulls` retirement predicate would all report a camper as a boat. The cost is low now and
  keeps growing with every hull law added later.

### B. A third door onto the facet renderer: `DwellingMeshDef` (recommended)

A new Core data type, `DwellingMeshDef`, sits beside `HullMeshDef` and `VehicleMeshDef`. It is baked by a
dwelling baker that reuses the room extraction from ADR 0041 and 0048. A dwelling presentation service
installs it onto the same `IsoFacetHullRenderer` with a still pose. `BuildingInterior` stays the one
definition of inside, and gains a mesh reveal beside its sprite swap and a polygon floor beside its
rectangle.

- **For:** it follows the vehicle precedent exactly ("a different door onto the same room, not a second
  room"). The shader, the level gate, both ramp tables and the surface generators stay unchanged. It meets
  the 2026-11-14 mesh ruling. The polygon floor handles the camper's tumblehome. The same path takes the
  shed now and the cottages later, so the house sprite path can retire building by building rather than
  living forever beside a second path.
- **Against:** one new Core type, one baker, one presentation service, and a seam inside
  `BuildingInterior`: four PRs before the player walks in. Each dwelling takes facet ids (ADR 0056) and pays
  the level gate's per-fragment discard always (ADR 0041). The bake needs an editor slot, and the first
  look needs the owner's eye.

### C. Sprite rooms on the house path

Bake the camper room and the shed room to cut sprite sheets through an `InteriorRigBaker`-style bake, and
swap them with today's `BuildingInterior`. The shells stay on their sprite sheets.

- **For:** smallest change, no Core type, and the earliest walk-in.
- **Against:** it contradicts the 2026-11-14 ruling and the owner's earlier rejection of sprite fit-out
  (ADR 0041, "the hybrid mesh does not work, we need full mesh interiors"). It still needs the polygon floor,
  so the cheapest part is not cheap. It costs eight cut sheets per room in texture memory, and it is work
  the mesh path throws away when it lands.

## Decision

**Option B.** A parked dwelling is a facet mesh that does not move. Its room is geometry in the same mesh as
its shell, cut by level the way a converted hull's is. Its inside is decided by `BuildingInterior`, and
nothing else.

### 1. Population and coverage

- `DwellingRigFleet` stays the coverage table for parked dwellings. Each dwelling is **sheet-baked**,
  **mesh-baked**, or **not baked, with a reason**. A drop that is none of these fails
  `DwellingRigFleetTests`, as today. A dwelling entry names its shell rig key and, where the room is a
  separate rig, its room key (the camper: `camper` plus `camperInterior`). It names one key where one rig
  draws both (the shed).
- The upstream ask in `DwellingRigFleet` stands and is widened. Every dwelling sidecar should carry a
  top-level `"kind": "dwelling"`, so the table can key on art's own word, as `VehicleRigFleet` does, instead
  of on the folder.
- Room rigs register in `RigCatalog` with their shell as a prerequisite, as `camperInterior` does.

### 2. The bake: rig to `DwellingMeshDef`

- A `DwellingMeshAssetBaker` (Tools, editor only) runs the shell and room rigs in the embedded V8 host
  (ADR 0021). It builds one mesh per dwelling variant (`subMeshCount = 1`), with the extraction rules of ADR
  0041 and 0048: the union over all eight facings, the rig's hand-drawn `cut` faces dropped, the per-face
  level tag in `TexCoord1.x`, the interior flag, the interior ramps in their own table, and the surface
  generators carried per face. The shared extraction is factored out of `RigMeshAssetBaker`, not copied.
- **Levels.** Shell faces are tagged from the shell's own published data. Faces of the body above the floor
  (`SOLE` z) belong to the room's level, and the roof is that level's lid. Faces below (chassis, wheels,
  steps, hitch) are exterior and are never cut. The room rig's walkable levels (the camper's saloon and its
  point-clamped wet bath) become further levels. If a rig does not publish enough to tag a face, the bake
  **refuses** and names the missing field (ADR 0041's rule: a room tagged level 0 is never cut and never
  hidden).
- **Refusals**, each with a sabotage test: more than 24 interior ramps; a fixture anchor whose verb is not
  in the verb table (section 4); a floor polygon that does not close; a room sidecar whose shell stamp does
  not match the shell rig's hash.
- **Output.** A committed `DwellingMeshDef` asset, one per variant, with a stable id `dwelling.<key>`
  (`dwelling.camper_clipper`, `dwelling.lot_shed`), the same way hull and vehicle mesh defs are committed.
  Moving these into an importer under ADR 0047 is a separate decision.
- **No shader change.** A dwelling uses the hull's facet program, keyword and tables as they are. A dwelling
  that seems to need a shader change comes back to this ADR first.

### 3. The runtime: install, reveal and cut

- **Core** gains `DwellingMeshDef` (mesh, ramps, interior ramps, level tags, cell geometry, the floor polygon
  per level, door and threshold, fixture anchors, the cut for each level as the renderer's existing cut
  type) and `IDwellingMeshPresentationService` with its static locator. This is additive only.
- **Art** gains `IsoFacetDwellingPresentationService`. It installs `IsoFacetHullRenderer` with a still pose
  (rock zero, waterline clamp off), fits the sun and lamp shadow casters once, and attaches no foam injector
  and no reflection, as the vehicle service does.
- **World:** `BuildingInterior` keeps its inside test, its storeys, its threshold and its one-shot wake
  storey, and gains two additive seams:
  - **A reveal interface.** What happens at the threshold becomes a small interface. The sprite swap is one
    implementation and stays byte-identical for every existing house. The mesh reveal is the other: outside,
    no cut; inside on level *n*, the cut for level *n*.
  - **A polygon floor.** `InteriorFootprint` gains a polygon inside test, with today's rectangle as its
    four-point case. A mesh dwelling's floor comes from its `DwellingMeshDef`, never retyped.
- The cut follows the 2026-11-14 rule exactly: standing inside a level cuts that level, and nothing else
  opens. It is driven by the inside state `BuildingInterior` already computes. A small `DwellingCutaway`
  writes the cut to the renderer on each real transition and polls nothing. It invents no third source of
  truth, the same law `BoatCutaway` keeps.

### 4. Fixtures bind to Def ids; the site owns the save key

- **The rig carries what a fixture is, never which one it is.** Each anchor in a room sidecar names
  `anchor`, `verb`, `defId`, `level`, a position, a reach point and a footprint, for example
  `freezer`, `freeze_catch`, `freezer.camper_freezer`.
- **The site carries the instance.** The save key of a placed store (`storage.<region>.<site>_<anchor>`, the
  v25 `StorageIds` pattern) comes from a per-site plan, `HomeStoragePlan` (from the storage PR), never from
  the rig. Two campers placed from one rig therefore get two keys. This is ADR 0036's rule again: the plan is
  keyed by site, not by build.
- **The Def is the authority for the deed gate** (`DeedHomeId` on `StowageDef`, `FreezerDef` and
  `KitchenStorageDef`). The room sidecar's `deed_gated` field is informational. A test holds the two equal
  until the rig drops the field.
- **One verb table** (editor, beside the builder) maps a verb to the component the builder adds on the anchor:
  `sleep_save` to `InteriorBed` (player bed), `freeze_catch` to the catch-hold freezer, `storage` to
  `StowageRack`, `food_storage` to the kitchen store fixture, `cook` and `prep` to `CookingStationFixture`,
  washing verbs to `WashStationPoint`, `sit` to `SitPoint`, and `step_out` to the door. Capacities, rates and
  prices stay on the Defs (rule 6). An unknown verb is a bake refusal, not a silent prop.
- **What a fixture looks like is part of the room mesh.** A fixture part that moves (a freezer lid, a door
  leaf, a shower's water) is a facet prop on the dwelling (`IsoFacetPropRenderer`, as wheels and oars are),
  never a sprite laid over the mesh.
- **North walls.** Which wall a kitchen or bath piece backs onto is read from the rig frame and the site's
  ground bearing. A test per placed dwelling holds every kitchen and bath anchor to a north, east or west
  wall unless it is on a named exception list with a reason, which is the rule in the appliance-wall plan.
  Moving a piece is a rig change.

### 5. Sorting: the hull-sort rule, with the dwelling written once

- **A dwelling is an upright world object** (ADR 0032, ADR 0059 decision 1). Its root carries a
  `SortingGroup` whose order is the decor band's Y-sort at its ground pivot, one rung behind: the Core rule
  that ADR 0059 adds (`HullSorting.OrderAt`, `SortingBands.HullYieldRungs`). It is written **once**, at
  enable, because a dwelling never moves (the static-decor rule). A feature module cannot call
  `VehicleMeshDriver.OrderAt` (rule 4), so this depends on the Core rule landing. If ADR 0059 is rejected,
  the same arithmetic moves into Core under a dwelling name and nothing else here changes.
- **Everything that belongs to the dwelling rides its group**: the mesh, the moving fixture parts, the door
  leaf. Local orders keep their meaning inside the group. No fixed order is added in the band's low floor
  (ADR 0059 decision 3).
- **The occupant is not parented to a dwelling.** While `BuildingInterior` says he is inside, his own sort
  writer takes the dwelling's order as an input and places him at that order plus one rung. There is still
  one writer per order (ADR 0059 decision 4). Decor and figures south of the dwelling still draw over him,
  and those north of it still draw under him.
- **Tall fit-out in front of him** (a bunk end, the wet-bath partition) hides him through the deck-occupant
  facet-id split, the same mechanism as a wheelhouse, not through order. An occupied dwelling therefore takes
  a facet block lazily, the way a boarded hull does (ADR 0056), and gives it back when he leaves. An empty
  dwelling holds a single id. The bake PR records each region's new count in
  `docs/architecture/facet-id-budget.md`.
- Storeys are never ranked against each other: the level he is not on is cut or switched off (ADR 0036).

### 6. The prefab and the placement

- **One prefab per dwelling variant** (ADR 0004), generated by an editor builder from the `DwellingMeshDef`
  and its sidecars. It holds the root (sorting group, mesh host, `BuildingInterior` with the mesh reveal),
  wall colliders from the floor polygon with the door gap cut, the threshold, the door, and one child per
  fixture anchor with the component from the verb table. The prefab is derived, so it is regenerated, never
  hand-edited.
- **The scene holds the instance and the site**: position, ground bearing (ADR 0034) and site key. Nothing
  else is overridden. St Peters is patched by a named refresh step and never rebuilt (ADR 0019).
- **The seamless rules hold unchanged.** There is no scene load. The dwelling lives in its region scene, and
  a crossing never consults it (ADR 0057). The rest anchor wakes him in the bunk, and `BuildingInterior`'s
  one-shot storey opens the right level (ADR 0037). `HomeDef` stays the purchasable record. Its
  `BuildingKey` names the site, and the site names the prefab.

### 7. Determinism and save

Nothing new is saved. The cut, the reveal and the occupant's order are recomputed from where he stands.
Store contents ride the v25 `StoredContainers` rows and the v23 `KitchenStores` rows under the site's keys.
No save version change.

### 8. Migration: coexist, then retire per dwelling at parity

- Sprite dwellings keep working. The switch is one list in the dwelling baker (`MeshDwellings`), the
  dwelling version of `MeshInteriorHulls`. A dwelling on that list whose sprite shell or room is still wired
  fails a test, because it would draw twice.
- **The camper (Clipper):** mesh shell and room together. Its sprite sheets retire once the owner passes the
  look. Only then does the shell's stale `LAYOUT.clipper` retire, so the sprites are not rebaked for a look
  the mesh replaces. The Bantam stays on sheets until a room rig exists for it.
- **The shed:** straight to mesh. It has no sprite path to retire.
- **The houses** (Ginny's cottage, the red saltbox, then the other buyable homes and the workplaces) migrate
  one building per PR after the camper proves the path. Each needs its house and room rigs to publish a
  level vocabulary. Two-storey houses keep ADR 0036's layers, as mesh levels. The sprite shell, room and
  furniture retire per building at parity.

## Consequences

- One definition of inside a building, for sprite and mesh rooms alike.
- The facet pass draws hulls, vehicles and dwellings without knowing which. Every shader, keyword and table
  guard stays where it is.
- Each mesh dwelling costs one draw, the always-on level-gate discard, and facet ids. Each bake PR records its
  tris, ramps and facet-id count (rule 7).
- The camper becomes enterable only after four PRs: the Core contracts, the bake, the presentation service
  and the interior seam. Option C would be quicker, but its work would be thrown away when the mesh lands.
- The rig owns the room's layout, including which wall the kitchen backs onto. The site owns identity and
  save keys. The Defs own numbers and gates.

## Follow-up PRs, in order

Sizes: **S** is under 300 changed lines, **M** is 300 to 1,000, **L** is over 1,000 (generated assets
excluded).

| # | PR | Owner | Size | Needs | Editor |
|---|---|---|---|---|---|
| 1 | **Core contracts.** `DwellingMeshDef`, `IDwellingMeshPresentationService` and locator, the fixture-anchor struct, EditMode tests. No behaviour change. **In review** (`feat/dwelling-mesh-core-contracts`): `Core/Dwellings/` adds `DwellingMeshDef`, `DwellingLevel`, `DwellingThreshold`, `DwellingFixtureAnchor`, `DwellingMeshRules`, `IDwellingMeshPresentationService` and `DwellingMeshPresentation`; tests `DwellingMeshContractTests`. | lead-architect | S | the hull-sort Core rule merged (it is) | no |
| 2 | **The dwelling bake.** Shared room extraction factored out of `RigMeshAssetBaker`; `DwellingMeshAssetBaker`; `DwellingRigFleet` gains mesh-baked and room keys; refusals with sabotage tests; the Clipper baked to `dwelling.camper_clipper`; tris, ramps and facet ids in the PR body. | art-pipeline, tools-editor | M (plus the asset) | 1, the camper room rig merged | **yes** |
| 3 | **Presentation.** `IsoFacetDwellingPresentationService` (still pose, casters, no foam or reflection), lazy facet block when occupied, a plate at several bearings. **In review** (`feat/dwelling-mesh-draw`): `Art/IsoFacetDwellingPresentationService` (self-registers on `DwellingMeshPresentation`; the lazy block is the shared renderer's ADR 0056 rule, proved on the dwelling path); tests `DwellingPresentationServiceTests`. | art-pipeline | S to M | 2 | **yes** (plate) |
| 4 | **The interior seam.** `BuildingInterior` reveal interface (sprite path byte-identical), polygon `InteriorFootprint`, `DwellingCutaway`, the occupant sort rule; tests that every existing house is unchanged. | world-content, lead-architect review | M | 1 | no |
| 5 | **Prefab builder and fixture binding.** Generated prefab, verb table, site storage keys through `HomeStoragePlan`, the deed-gate agreement test, the north-wall test. | tools-editor, world-content, economy-sim review | M | 2, 4, the storage PR (v25), the bathrooms PR (fixture prefabs, `SitPoint`) | no |
| 6 | **The camper walk-in at St Peters.** Refresh step places the prefab at the lot; the lot's nose direction reconciled with what is drawn; the door enters; PlayMode: walk in, sleep, store, reload. | world-content | M | 3, 5 | **yes** |
| 7 | **The shed.** `smallShed` in `RigCatalog` and `DwellingRigFleet`, baked, placed at the reserved spot. | art-pipeline, world-content | S to M | 6, the shed rig merged | **yes** |
| 8 | **Clipper sprite retirement.** Retire the Clipper's sheets and `LAYOUT.clipper` after the owner's look. | art-director, art-pipeline | S | 6 and the owner's look | **yes** (rebake check) |
| 9+ | **Houses, one per PR.** Red saltbox first (the one St Peters home for sale), then Ginny's cottage (two storeys), then the other buyable homes and the workplaces as their rigs publish levels. | world-content, art-pipeline | M each | 6, each house's rig levels | **yes** |

PRs 4 and 2 can run side by side once 1 merges. PR 5 can be written against PR 2's asset before PR 3 lands.

## Rejected

- **A dwelling is a hull (A).** It is cheap now, then costly with every later hull law, and it puts a second
  definition of inside beside `BuildingInterior`.
- **Sprite rooms (C).** It contradicts the mesh ruling, and the work is thrown away when the mesh lands.
- **A new `DwellingInterior` component beside `BuildingInterior`.** That would give two definitions of
  inside a building, which ADR 0036 rejected for storeys and which applies here for the same reason.
- **Parenting the occupant to the dwelling** to reuse the aboard rule. A building is not a vehicle he rides.
  Parenting would reach into the crossing, the whereabouts and the control switcher for a question that a
  sort input answers.
- **Save keys in the rig.** One rig placed twice would share one store.

## Decisions for the owner

1. Accept this ADR: parked homes are facet meshes with cut rooms, on one interior system (option B).
2. The camper's sprite shell retires when you pass the mesh look (recommended), not before.
3. After the camper and the shed, the houses move to mesh one at a time, starting with the red saltbox, then
   Ginny's cottage (recommended).
4. The cut opens as he crosses the threshold, the same moment today's houses swap (recommended), rather than
   only once the door closes behind him.
