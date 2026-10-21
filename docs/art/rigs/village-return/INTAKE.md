# Village return (Claude Design drop 14): intake record

Landed 2026-09-26 by the art-pipeline lane: Phase A of the village return intake (handoff
`HANDOFF-2026-09-26-village-return-14-intake.md`). This folder is the drop **as delivered**, less the
files named below. Nothing in any rig was edited. Phase A wired only three catalog keys, `coastalPass`,
`manorIso` and `manorUnitIso`, carried from #853. Phase B (below) switched the game onto the returned rigs
in the same PR, in the same commit as the re-bake: `house`, `interior`, `shopfront`, `shopInterior` and
`yardIso` in `RigCatalog` now name this folder's copies.

| | |
|---|---|
| Drop | `749b6c41-Pixel_art_capabilities_14buildingsreturn.zip`, 13,861,123 bytes, sha256 `74783c9b160cef82930063fc6705d3373bc098e2ac33a9b70fd6b51bb3ff2bf3` |
| Folder in the zip | `hh-village-return/`, 124 files |
| On arrival | `SHA256SUMS.json` lists 118 files: 118 ok, 0 differ, 0 missing. Unlisted: the stamp file itself and the five `houses-kit/*.dc.html` pages, which were checked against the zip instead |
| Landed here | 59 files: 53 stamped files, `SHA256SUMS.json` and the five pages. The intake adds this file |
| Not landed | 65 stamped files: 63 stay in the intake's `Evidence~`, and 2 are byte-identical to the one copy `docs/art/rigs/` already holds |

## What landed, against main

| Path | Against main |
|---|---|
| `houses-kit/Art/houseIsoRig.js` | returned `HouseIso`: +49,711 bytes against `docs/art/rigs/houseIsoRig.js` |
| `houses-kit/Art/interiorIsoRig.js` | returned `InteriorIso`: +29,255 bytes against `docs/art/rigs/interiorIsoRig.js` |
| `houses-kit/Art/coastalPass.js` | new: the companion pass, version 3.2.0, and the shared light engine (`CoastalPass.light`) |
| `houses-kit/Art/manorIsoRig.js`, `manorUnitIsoRig.js` | new: the manors. They get sources and keys only: no bake, no placement |
| `houses-kit/Art/building-lifecycle-kit/buildingLifecycleRig.js` | byte-identical to `docs/art/rigs/building-lifecycle-kit/buildingLifecycleRig.js` (a library copy) |
| `houses-kit/support.js` | byte-identical to `docs/art/rigs/character/rig9/support.js` |
| `houses-kit/sidecars/*.params.json` (7), `layout-proposals/*.json` (6), `manifest.json`, `README.md`, `IMPORT.md`, the five `.dc.html` pages | new. The wharf sidecar is here because the kit's `manifest.json` covers it. Nothing reads it |
| `shop-building-kit/shopfrontRig.js`, `shopInteriorRig.js` | returned: +4,933 and +5,993 bytes against `docs/art/rigs/shop-building-kit/` |
| `shop-building-kit/shopBuildingRig.js`, `shopBuilding.contract.json`, `README.md`, `README-buildings.md`, `README-exteriors.md`, `README-rooms.md`, `harness.html` | byte-identical to `docs/art/rigs/shop-building-kit/` |
| `shop-building-kit/reference/*.png` (5) | returned: the same five names as `docs/art/rigs/shop-building-kit/reference/`, with different pixels (renders of the returned shop rigs). Git LFS |
| `yard-landscaping-kit/yardIsoRig.js` | returned: +1,651 bytes against `docs/art/rigs/yard-landscaping-kit/yardIsoRig.js` |
| `yard-landscaping-kit/SHA256SUMS.txt` | the kit's own stamps for its returned files: +368 bytes against `docs/art/rigs/yard-landscaping-kit/SHA256SUMS.txt` |
| `yard-landscaping-kit/gameplay/yardIsoRig.gameplay.json` | differs from `docs/art/rigs/gameplay/props/yardIsoRig.gameplay.json`: same size, different bytes. Nothing in the game reads this copy |
| `yard-landscaping-kit/IMPORT.md`, `README.md`, `harness.html` | byte-identical to `docs/art/rigs/yard-landscaping-kit/` |
| `yard-landscaping-kit/outbuildingIsoRig.js`, `yardLots.js`, `lots-phase5.json` | new |
| top level: `NOTES.md` and `NOTES-phase2.md` to `NOTES-phase5.md`, `CAMERA-FIRST.md`, `checks.txt` | new |

No `weatherSky.js` ships in this drop. The two library files the houses kit carries, `support.js` and
`buildingLifecycleRig.js`, are byte-identical to the repo's copies, so there is still one version of each.

**Not landed.**
- **Kept in `Evidence~` (63):** `boards/` 10, `boards-outbuildings/` 12, `boards-phase2/` 8,
  `boards-shops/` 5, `boards-stpeters/` 13, `boards-yards/` 9, `checks/` 1, `phase1-rigs/` 4 and `plan/` 1.
- **`houses-kit/Art/interiorPropRig.js`:** byte-identical to `docs/art/rigs/interiorPropRig.js`. The prop rig
  has one home, and `MultiunitKitIntakeTests.OneInteriorPropRig` pins it there.
- **`houses-kit/Art/wharfBuildingRig.js`:** byte-identical to `docs/art/rigs/wharfBuildingRig.js`. The wharf
  rig is drop 13's family.

`VillageReturnIntakeTests` checks every stamp against the file where the intake put it. It also checks that
the Evidence-only files did not land, that no second copy of either rig appears, and that the folder holds
nothing unaccounted for.

## The catalog: what was carried from #853

- **`coastalPass`** is a pass, not a rig: `InstallModule`, no W/H/pivot. Its prerequisite is **`interiorProp`,
  not `house`**. The pass throws "CoastalPass needs Art/interiorPropRig.js" from its prop emitter
  (`coastalPass.js:52`) the first time it dresses a room. It loads alone. Declaring `house` would close a
  cycle (house → coastalPass → house) that `InstallPrerequisites` does not guard against.
- **`manorIso`** has prerequisite `coastalPass`. **`manorUnitIso`** has `manorIso`, `interiorProp` and
  `coastalPass`, so ManorIso is defined before ManorUnitIso. That is the order the kit's
  `manifest.json` gives (`integration.loadOrder`). Measured install order:
  `PropIso CoastalPass ManorIso ManorUnitIso`.
- **The storey reader** (`InteriorRigBaker.StoreyRiseMetres`) reads the rise as a difference: upper
  `storeyZ` minus ground `storeyZ`, guarded by `typeof dims`. The returned rig's `anchors().storeyZ` is the
  height above grade, 0.55 m, not the rise. Measured rises: sageCottage 2.65, school 2.59, redSaltbox 2.74,
  whiteFarmhouse 2.92 m. They are the same with the companion on, off and not loaded.
- The `RigCatalogAssemblyTests` rows and the `BuildingLifecyclePassTests` hook-strip fix.

## What was checked (no Unity)

| Check | Engine | Result |
|---|---|---|
| Zip hash and `SHA256SUMS.json` | sha256 | 118 / 118 |
| The drop's `checks/camera-shells.cjs` | Node v24.19.0 | OK, 313 / 313 (`checks.txt` counts 308; the count differs, the RESULT line agrees) |
| The seat's checkers (the v33 redesign package), with `houses-kit` as their `kit/` | Node | rigs-intact 24 / 24, door-registration 19 / 19, restamp `--check` 14 / 14. floor-option fails 1 of 4, room-anchors 36 of 40, manor-surface 80 of 94 |
| Classic pin: the 5 house builds × 8 facings | ClearScript V8 and Node, rigs chained in catalog order | 40 / 40 identical to main's `houseIsoRig.js`, with `{classic:true, coastalPass:false}` |
| R2 registration: each room under its own shell, 8 facings | V8 and Node, same numbers | Returned house and room: offset 0. Today's: offset 4 |

The RESULT line of the seat's checkers says 3 of 5 checkers fail. `checks.txt` (phase 1, 09-25) says 2 of 5.
The five extra failures are all "unchanged from the cut" byte pins on `InteriorIso` renders:
- floor-option: `render(0, {})` with the pass on hashes to `68bdea7f…`, where the pin expects `f842d004…`;
- room-anchors: the four shipped rooms' ground and upper renders.

Phases 2 and 3 changed the room plan and cut the walls at the sill on purpose (`NOTES-phase2.md`,
`NOTES-phase3.md`). Those pins belong to the v3 return and are not ported.

**Two things the classic pin taught.**
- `{classic:true}` alone does not reproduce today's house when the companion is loaded. `render()` applies
  the pass to every house whenever `CoastalPass` exists, so the classic look also needs `coastalPass:false`.
- The rooms have no classic twin of today's picture. Their storeys and cutaway changed, so every room
  re-bakes.

**The bake's sky.** The returned rigs light through `CoastalPass.light.skyOf(opts)`. Without
`WeatherSky`, a `time` gives the clock's picture (REF_SKY or NIGHT_SKY, switched by the clock). No build's
options carry `time`, `sky` or `night`, so the bake carries **REF_SKY**.

## Phase B: the switch

The owner ruled on Phase A's report on 2026-09-27: R2 accepted; light option L2, houses first; re-bake the
5 houses, the 4 rooms, the 10 yard pieces and the 4 shopfronts; the rooms now.

1. **The rigs.** `house` → `village-return/houses-kit/Art/houseIsoRig.js`, prerequisites
   `buildingLifecycle`, `coastalPass`. `interior` → `village-return/houses-kit/Art/interiorIsoRig.js`,
   prerequisites `house`, `coastalPass`; the returned room reads `HouseIso.entrance(shell)` and
   `HouseIso.BODY`. `shopfront` and `shopInterior` → `village-return/shop-building-kit/`; `yardIso` →
   `village-return/yard-landscaping-kit/`. Sprite names and counts did not change. The four wharf sheets
   did not move. The props, the shop levels and the counter were not re-baked. `Buildings.json`,
   `Interiors.json` and `shops.contract.json` were regenerated through their bakers only.
2. **R2: each room under its own shell, at offset 0.** The returned room draws its doorway on the gable
   its house's door is on, so `InteriorRigAzimuthProbe` measures offset 0 for each room against its own
   shell. `Interiors.json` carries `exteriorFacingOffset: 0`, and `shops.contract.json` carries
   `shellFacingOffset: 0`. `storeyHeightMetres` takes each rig's measured rise: sageCottage 2.65, school
   2.59, redSaltbox 2.74, whiteFarmhouse 2.92.
3. **The furniture turned with the doorway.** `StPetersInteriors`' plans were laid out for a doorway on
   `−Y`. A half-turn about the room's centre keeps every piece where it was relative to the door: every
   coordinate is negated, every facing turns by 4, and the pillows swap ends. The door-lane and
   bedroom-side tests now read which end the door is on from the bake
   (`InteriorCatalog.DoorModelMetres`) rather than assuming `−Y`.
4. **The placed rooms.** St Peters cannot be rebuilt, so its five placed rooms were re-pointed by hand in
   the scene YAML. Each room's sprite moves four facings on, to the cell that registers under its house at
   offset 0. Nothing else in the scene moved. Ginny's upstairs keeps today's lift of 3.1025 m; a room
   stood from scratch uses the contract's 2.65 m.
5. **L2, the lit houses.** The five house builds bake three sheets beside the albedo:
   - `_mask`, in the trees' order (R key, G back rim, B depth, A coverage);
   - `_normal`;
   - `_emit`, a new one-byte sheet (R8) for the emitters.

   The emit term is part of the shared light law, in `SpriteLightResponse.hlsl` and its twin
   `SpriteLightMath.cs`. With no emitter sheet bound, every sprite draws as before. The placed houses in
   St Peters and Nine Mile Creek were patched by hand to `LitVillageBuilding.mat` with a
   `SpriteLightBinder`, the same binding `VillageBuildingCatalog.Configure` gives a freshly placed house.
   There are no per-frame ground pools, no occupancy and no second shader pass. Every other sheet was
   re-baked albedo-only.
6. **The general store's door.** The returned `generalStore` build draws its door on the `+X` eave. It is
   not placed anywhere (St Peters' store is the shop rig's), and the owner accepted it. At a quarter turn
   the door lands on the pivot, so its side gives no answer. `BuildingRigAzimuthProbe` now reads such a
   door from the loop it traces around the pivot, but only when the rig itself reports the door on an
   eave.
7. **Retired with the switch.** Each is named with its production subject in the PR:
   - `InteriorKitTests.EveryRoomsShellDrawsItsDoorOnTheGableTheRoomOpensOnto` and
     `TheRigStillRoutesItsDoorTheWayTheGableRuleAssumes`, whose subject is
     `VillageBuildingKit.DrawsDoorOnGable`;
   - the offset-4 and door-gable pins in `InteriorRigBakeTests`;
   - `test_export.py`'s `test_the_interior_sits_a_half_turn_from_its_building`. The scene exporter is
     retired, and #900 took its CI job off main; the file stays, so its offset-4 pin is retired here. No
     package was run or committed.

## Line endings

No landed text file carries a CR byte, so the committed blobs are the drop's LF bytes. A Windows
checkout with `core.autocrlf` writes CRLF, so `VillageReturnIntakeTests` hashes text with CRLF read as
LF. PNGs are hashed as they lie, and they live in Git LFS.
