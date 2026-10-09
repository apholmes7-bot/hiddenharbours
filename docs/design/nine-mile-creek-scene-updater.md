# Nine Mile Creek scene updater — N1

N1 uses **decision 1(b), a layer refresh**. Later NMC scene changes, including N2–N9 and Road kit R2, add an explicit named patch for their own roots through this route. A Build click is not the updater.

`NineMileCreekLayerRefresh` reuses `StPetersLayerRefresh.SceneYaml`, `LayerPatch` and `IdAllocator`. It reads the scene as text, validates references and root ownership, and writes only the declared patch. It never opens or saves a scene through Unity. Close NMC before applying; an open scene could later overwrite the file.

## Running the route

Use **Hidden Harbours → Nine Mile Creek Layers → Write patches (dry run)**, read the named operations under `artifacts/nmc-n1/`, then **Apply named patches**. The callable equivalents are `NineMileCreekLayerRefresh.Run(false)` and `Run(true)`.

Read the scene diff, including every deletion, and re-run the planner against its result. The second application must leave the exact file bytes unchanged. N1's measured second-run SHA-256 is `E298C2F1BCBE1B97E4A59B98C384E927972101A65C36D27EC595BCE1FA405D03` before and after. `NineMileCreekLayerRefreshTests.ASecondRunChangesNothing` replans on a copy; it does not merely replay a saved patch.

## Named steps

1. **`01-quay-face-765` — landed by N1.** Refresh only `NineMileCreekDressing/QuayFace` from `NineMileCreekDressing.FacePieces()`: 38 courses, the #734/#755 spacing and fixed sorting, and #765's tide clipping on the 21 courses that actually show a face at this camera. The waterline uses the builder's `FaceLipElevation` over its authored terrain. It does not redraw the ground or apply the newer berth trench. Existing named courses keep their record identities. The ten old `Breakwater_logCrib_14` through `Breakwater_logCrib_23` courses retire; the fourteen surviving NorthWall/WestWall courses lose `YSortSprite`. Each deleted document is named in the patch.
2. **`02-building-interiors-824-carried` — named debt, not an implemented station patch.** The full trial used #824's shared `BuildingInteriorStander` recipe and produced no room/door changes under `CreekShops`; its two existing rooms remain. The Route91Station debt is a coupled prefab change: entry leaves, solid blocker, room wiring and a new `InteriorWalls` ring. It needs a separate patch under `CreekFuel`, with its projection corrections and paired shell/room references audited together. Putting only a wall ring into the QuayFace patch would leave an incomplete doorway. The updater emits this debt by name on every run. The N1 handoff carries fresh shop room/door plates and a station plate. `BakeAllBuildingsMenu.RunAll` is a separate preparation command, never an implicit part of this refresh; N1 does not bake or commit unrelated building art.

## Preservation contract

Everything outside QuayFace remains byte-identical. That includes the 298 placements below `CreekTrees`, their integrated `AcadianTreeCatalog` trunk anchors and wind maps, and all 562 shadow records (298 trees, 236 shoreline, 28 marsh). Match trees by hierarchy and record identity, never by bare species name. A future step that replants trees must use the integrated catalog and prove those bindings again.

S1's `Sea._seabedTexture`, #925's seven seller ids and book, #915's relights, painted terrain, and current serialization remain as committed. The builder and this refresh do not read #929's `KeySceneImport.Nmc*` or `KeyScene_nmc_*` data. The imported key scenes remain UNPLACED.

New terrain, roads, passages, lots, people, fleet or interiors each need an explicit patch and reference validation. This is not a promise that every later builder edit reaches the scene automatically. The existing full builder remains an investigative tool with its overwrite guard; it must not replace this route.

## Why the rebuild trial was rejected

The trial on `032f6239` reproduced the 298 trees and their bindings, but dropped the S1 seabed texture. It also changed shared-helper output beyond Phase A's accepted inventory: local wind fallback values, nav-mark physics/phase overrides, vehicle presentation children, current sprite sizes and grass payloads. The raw trial changed roughly 739,000 lines including regenerated ids. The rejected trial, object ledger and named deletions are retained in the lane's ignored artifacts. N1 restored the base scene before applying its scoped patch; none of those wider trial changes is in the final scene.

N1 adds six EditMode cases: repeatability, outside-root preservation (including the tree/shadow census), builder parity, and negative controls for missing sprites, unknown components and transformed parents. No production test or subject is retired. The scene exporter remains retired under #900.


## Owner hold 10-09: berth depth, placement and pale foam

The live scene lagged behind two existing builder plans. Its `TidalTerrain` carried only the
harbour channel, so the tide riders correctly grounded the wall fleet on the -1.6 m basin at low
water. Its five wall-hull transforms also retained the old packed grid. The plan-only berth tests
could not detect those stale scene placements.

`NineMileCreekBerthRefresh` extends the one-root text-patch route with `03-berth-trench` and
`04-wall-berths`. The first adds the existing `NineMileCreekMainland.BerthTrench` after the
harbour entry. It parses the channel schema and requires exact float values, field order and
entry/waypoint counts; alternate float spelling is accepted, but the original harbour bytes
are retained. The second resolves owners by asset identity within `NineMileCreekFleet` and
moves only the five wall hulls to their existing owner berth plan. Float owners are unchanged.
The combined scene diff is seven added channel lines and five position-line replacements,
with no deleted objects or other serialized changes.

The pale discs were advected wake foam drawn by the displaced-water chunk renderers. A linear
current response continually deposited foam from moored hulls, and seeking the plate clock
changed the wave field while its animator still eased from the old field, producing a false
impact pulse. `FoamInjector` now listens to Core's vessel-way seam, gives moored hulls zero
travel velocity and a serialized cubic current response, and re-primes its wave/history state
on a clock seek. Underway wake shaping remains linear. No boat, water shader or material is
changed; `MooredBoat.cs` is untouched.

Nine new EditMode cases guard exact channel parsing, rejection of a moved waypoint/changed bed,
scene parity, preservation and repeatability. Three new PlayMode cases load the real NMC scene:
all five wall hulls must float and rise with its actual float at four tide times; posed mesh
bounds must leave the fender gap; and a same-frame wake-layer pixel comparison must find no pale
disc under any of the seven resident hulls. The pixel case explicitly skips without a graphics
device. All three controls failed for their named defects against the unfixed copy. No test or
production subject is retired.

The owner review evidence lives under `artifacts/nmc-n1/plates/934-fix/verified/`: the original
low/mean/high camera and times, spring low at game second 24840 (sea -2.1968317 m), and the whole
berth line at mean, with `proof.json`. The night-time spring-low plate retains the actual lighting.
The final scene's second application is byte-identical, SHA256
`B37D074EB976F51E06A3252E0F8D7DEA13FE0C176E22EA0138E4C1A38F7B471B`.
