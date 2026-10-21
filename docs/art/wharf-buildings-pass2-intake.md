# Wharf buildings pass 2 — drop 13

This PR adds Claude Design's drop 13 to the repo without changing it, and registers its editor-only load chain beside pass 1. One byte-identical library stays at its single home on main instead of being copied in.

- **Phase A** (the intake) changed nothing the game draws.
- **Phase B** (the slot, 2026-09-29) re-baked the four placed wharf buildings in pass 2, in albedo, as the owner ruled. Their sheets, sidecars and `Buildings.json` came through the baker, and each sheet keeps its eight sprite names and ids.
- Nothing places a new building. Pass 1's rig stays in the repo, unchanged. The scenes, the slicers and readers, and both light twins are unchanged.

The rulings are at the end of this page, followed by the slot's numbers.

The owner retired the scene exporter on 2026-09-27 at 01:17:31Z, and #900 removed its packages and its CI job. Its intake steps no longer apply, so the exporter was not run and no package changes. The CI gate is `EditMode + PlayMode tests`.

## Delivery and checks

The archive is `f29ed9d6-Pixel_art_capabilities_13wharfrigkitv2.zip`: 2,395,727 bytes, SHA-256 `58f13232f48d754e80c9a4c4d8a92b3aefbd96e772c1dbf19f5c83f3339d352f`, 29 files. The [file table](wharf-buildings-pass2-file-table.md) lists each file, says whether main already has it, and names who reads it.
- **New:** 22 files.
- **Already on main:** 7 files, `support.js` and the six in `lib/`. Each equals main's copy by Git blob id, so no library differs from main.
- **Manifest:** `SHA256SUMS.txt` covers the other 28 files, and all 28 check OK.

**28 of the 29 files land unchanged in `docs/art/rigs/wharf-building-kit-v2/`, with `lib/`.** The 29th is `lib/interiorPropRig.js`. It is byte-identical to main's `docs/art/rigs/interiorPropRig.js` and stays there, because the prop rig has one home. `MultiunitKitIntakeTests.OneInteriorPropRig` fails on a second copy anywhere under `docs/art/rigs/`. This follows drop 14's houses kit, which did the same (`VillageReturnIntakeTests.LandedElsewhere`).
- **Where this differs from the charter:** the charter leaned towards landing the kit whole. Landing it whole would turn that test red.
- **How the test checks it:** it checks this file's manifest line against main's copy, and fails if the kit ever holds a second one.
- **The viewer:** the kit's README lists `lib/` as the viewer's scripts. To open the viewer from the repo, copy main's prop rig into `lib/` first.

`.gitattributes` lines 281–287 pin the kit's `.js`, `.json`, `.txt`, `.md` and `.html` files to LF, so the manifest's hashes hold on every checkout. The block sits after drop 12's `wharf-rig-kit-v2` block (lines 276–280). The nine board PNGs keep LFS and `-text` from the existing `*.png` rule.

These checks ran on real Node v24.19.0, with the rigs concatenated in the README's order from main's paths. Pictures were compared by decoded pixels, never by PNG bytes.

- **Checks.** `WharfBuilding2.checks()` ran on all seven presets. Below its own header, the output is identical to the delivered `checks.txt`.
- **Sidecars.** The seven sidecars were regenerated from the live model with `gameplay()`. Each is byte-identical to the delivered JSON.
- **Facing direction.** `BuildingRigAzimuthProbe` reads the door anchor at a quarter turn and cross-checks it against the silhouette. It reads counter-clockwise on all seven presets. At dir 2 the door anchor lands exactly where pass 1's does: netShed 518.4, redShed 511.2, tealShack 513.6, gambrelBarn 452.8, iceHouse 460.0, fishPlant 372.8, cannery 353.6.
- **Pass 1 is unchanged.** `wharfBuildingRig.js` is untouched. Its four placed sheets were re-rendered at the baker's eight facings in three separate V8 contexts:
  - A: pass 1 with today's lifecycle pass.
  - B: pass 2's chain loaded before pass 1.
  - C: pass 2's chain loaded after pass 1.

  The four sheets are `Village_ginnyWoodshed`, `Village_ginnyNetStore`, `Village_ginnyLeanTo` and `Village_stPetersCannery`. Each context reproduces every committed sheet: 0 pixels differ, and every crop matches its contract. The three contexts also match each other (A = B = C).

## Catalog

`RigCatalog.Buildings.cs` gains two entries beside `wharfBuilding`. Nothing else in the catalog moves.

| Key | Completion object | Convention | Prerequisites | Basis |
|---|---|---|---|---|
| `wharfBuilding2` | `WharfBuilding2` | CounterClockwise | `coastalPass`, `buildingLifecycle`, `wharfBuilding2Geometry` | `BuildingRigAzimuthProbe`'s reading on all seven presets (Node). A test re-measures three with the C# probe, and the bake probes again. |
| `wharfBuilding2Geometry` | `WharfBuildingGeo2` | Clockwise (unused) | none | A geometry builder with no rendered heading, so it uses the placeholder the other non-directional entries use. |

The prerequisites install depth-first, which gives the README's load order:
1. Main's `interiorPropRig.js`, installed through `coastalPass`.
2. `coastalPass.js`.
3. `buildingLifecycleRig.js`.
4. The kit's geometry.
5. The kit's rig.

Two other libraries need a note:
- **`weatherSky` is left out.** The README calls it optional, and loading it changes no pixel of the seven presets or the four placed builds (measured).
- **`coastalPass` is required.** Without `CoastalPass.light`, the rig's `render()` hands the call to pass 1 when `WharfBuilding` is loaded, and throws when it is not.

The cell (1200 × 1160), the pivot (600, 780), the eight native facings and the 40° elevation all match pass 1.

**The lifecycle hook.** The hook sits inside the rig's model build; pass 1 has it after `build(b)`. The lifecycle pass is declared a prerequisite, as it is for pass 1.
- **Measured:** the seven presets with no lifecycle keys render byte-identical whether the pass is loaded or absent. The decayed placed builds differ.
- **Tested:** the test repeats both checks on three presets. It also checks that each of the six earlier phases and each of the four decay states draws a shell that differs from the finished building.

**`BuildingAxes.RigKeys`: not joined in this PR**, so `BuildingAxesTests.BothBuildingRigs_AreDrivable` (line 252) does not move. `RigKeys` is the Building Studio's dial set, and `BuildingAxes.For` has no dial set for pass 2. Pass 1's dials do not fit pass 2, for two reasons:
- Pass 2 reads new options: `doorOpen`, `bayOpen`, `loftOpen`, `floorH`, and `cutaway` with `storey`.
- Pass 2 has no `night` option: its light goes through `lights()` and `relight()`.

A pass-2 dial set is a studio change for a later tools-editor PR.

## The light: L2 maps or albedo in the new look

**Ruled 2026-09-29: albedo only.** The wharf buildings take no L2 maps, and this PR adds no `BuildingLightFrame` adapter. L3 stays an option only.

On 09-27 the owner ruled drop 14's light as "L2 houses first, the rest albedo in the new look". #898 then landed the emit term in the shared law. Phase A priced both options for the wharf buildings, below.

Units and baking assumptions for every price on this page:
- **MiB** is 2²⁰ bytes of uncompressed texture at one sample per texel, with no mips, one variant and all eight facings resident.
- **L2** adds a mask (RGBA32), a normal (RGBA32) and an emitter (R8) to the RGBA32 albedo, so an L2 sheet costs 3.25 × its albedo.
- **Today's baker** (`BuildingRigBaker`) crops to the union of drawn pixels over the eight facings, plus 1 px of padding. It then takes the widest grid that fits under 2048, and falls back to 4096 only if none fits.

**Can L2 work for these rigs?**
- The rig publishes the API `BuildingLightFrame` reads for the houses: `frame()`, `lights()` and `relight()`.
- It needs no shader change, no second pass, no mask reorder and no rig edit.
- It does need a C# adapter in `BuildingLightFrame` (a later PR), for two measured reasons:
  - **The frame size differs from the render.** `frame()` returns a cropped frame (at dir 2: netShed 515 × 377, gambrelBarn 627 × 508, fishPlant 951 × 682, cannery 991 × 720), while `render()` draws the 1200 × 1160 cell. `BuildingLightFrame` refuses a size mismatch, so the adapter must place the frame at its crop offset.
  - **The lamps belong to no room.** Every lamp measured has `room: null`. The net shed's and gambrel barn's door lamp sits at level 0, and the fish plant's and cannery's front and three bay lamps sit at level 1. The house occupancy (kitchen, parlour, upper, hall) changes none of them. The wharf buildings need an occupancy rule of their own.

**L3 is an option only.** L3 is the night by occupancy, with per-frame ground pools. It is not ruled, so the rig's `SCHEDULES` table (fisher, store, plant, closed) drives nothing here. L3 is priced only as far as L2's maps; its runtime cost is not measured.

### The seven presets under today's baker

| Preset | Tight cell @ offset in the 1200 × 1160 cell | Grid, sheet (cap) | Albedo MiB | L2 MiB | Render ms per facing |
|---|---|---|---:|---:|---:|
| netShed | 300 × 305 @ (450, 572) | 6 × 2, 1800 × 610 (2048) | 4.19 | 13.61 | 251 |
| redShed | 280 × 311 @ (460, 559) | 7 × 2, 1960 × 622 (2048) | 4.65 | 15.11 | 277 |
| tealShack | 304 × 315 @ (448, 563) | 6 × 2, 1824 × 630 (2048) | 4.38 | 14.25 | 253 |
| gambrelBarn | 398 × 429 @ (401, 477) | 5 × 2, 1990 × 858 (2048) | 6.51 | 21.17 | 706 |
| iceHouse | 384 × 413 @ (408, 488) | 5 × 2, 1920 × 826 (2048) | 6.05 | 19.66 | 534 |
| fishPlant | 676 × 591 @ (262, 406) | 3 × 3, 2028 × 1773 (2048) | 13.72 | 44.58 | 1262 |
| cannery | 714 × 629 @ (243, 381) | 5 × 2, 3570 × 1258 (**4096**) | 17.13 | 55.68 | 1642 |
| **All seven** | | | **56.63** | **184.06** | |

Every pivot lands on the cell's (600, 780), for example netShed at 450 + 150, 572 + 208.

The baker's grid rule leaves slots empty. A tight 4 × 2 or 8 × 1 grid would cost less:

| Preset | Today (MiB) | Tight grid (MiB) |
|---|---:|---:|
| netShed | 4.19 | 2.79 |
| redShed | 4.65 | 2.66 |
| tealShack | 4.38 | 2.92 |
| gambrelBarn | 6.51 | 5.21 |
| iceHouse | 6.05 | 4.84 |

This is a note for a later baker change, not part of this PR.

## The 4096 question

**The cannery is the only preset that needs 4096**, and it stays one sheet: 5 × 2, 3570 × 1258, 17.13 MiB albedo, 55.68 MiB L2. Its 714-pixel cell does not fit three across under 2048.

**Ruled 2026-09-29: the fish plant is one 2048 sheet (3 × 3).** The slot's bake made exactly that: 2028 × 1773, 13.72 MiB.

**The fish plant does not need 4096.** Its tight cell is 676 × 591, so three fit across under 2048. The README's claim that it needs 4096 does not hold under the tight crop. The seat's 715 × 629 figure is the cannery's cell (the kit's own `tightCell` for the cannery), not the fish plant's.

| Fish plant option | Sheet(s) | Albedo MiB | L2 MiB | Notes |
|---|---|---:|---:|---|
| **One 2048 sheet** | 3 × 3, 2028 × 1773 | 13.72 | 44.58 | What today's baker does with no change; one sheet, eight names |
| One 4096 sheet | 6 × 2, 4056 × 1182 | 18.29 | 59.44 | The largest option; no reason to take it |
| Two 2048 sheets | 2 × (2 × 2, 1352 × 1182) | 12.19 | 39.62 | The smallest; splits one building over two sheets, a slicer and placement change |

## The placed cannery's floor

These measurements are on Node, with the pass-2 cannery preset.

**Pass 2's default floor is 1.2 m.**
- Both doors sit at z 1.2 m: the roll-up door (3.4 × 3.4 m) and the personnel door (0.9 × 2.1 m).
- The apron and the dock are at 1.2 m, and each draws its own stairs down to z 0.
- The mezzanine is at 3.84 m.
- At dir 4, the door anchor is at y 884.5.

**`floorH: 0.6` lowers everything by 0.6 m**, and the door anchor moves to y 899.2.

**Pass 1's door anchor is at y 899.16**, so pass 1's floor is effectively 0.6 m. Pass 2's default raises the doors 0.6 m, which is 14.7 px on screen at dir 4.

**The site.** `StPetersCannery.Site` is (170, 16), on the shore 20.6 m from the pier root. It stands beside the deck, not on it: the deck occupies x ∈ [183, 213], y ∈ [−3, 3]. Its doors face the pier root, and the ground is the island's flat 6 m plateau, well above the 2.2 m spring high tide. No door meets a deck, so the deck forces neither floor height.

**Recommendation: 1.2 m**, the drop's default, whose stairs are drawn for it. `floorH: 0.6` reproduces pass 1's door height exactly, if the owner wants the placed building to keep that look.

**Ruled 2026-09-29: 1.2 m**, pass 2's default. The placed cannery bakes from the preset with no `floorH` override.

## Doors and interiors (options only: ADR 0036, a later PR)

Nothing in this section is committed in this PR. The owner allowed one door-open net shed sheet, baked for the slot's plate only; it stays in the lane's evidence. The options are:
- **Door open:** `doorOpen: 1`. `persOpen` follows it.
- **Door half-open:** `doorOpen: 0.5`.
- **Bays open:** `bayOpen: 1`, fish plant and cannery only, facings 0–4.
- **Loft open:** `loftOpen: 1`, gambrel barn and ice house only, facings 2–6.
- **Section:** `cutaway: 'section'`, with `storey: 'ground'` or `storey: 'loft'`.

How the table reads:
- Each state is one still frame per facing, so an animated door adds its half-open frame on each facing the door changes.
- An open state never grows the crop: it keeps the shut sheet's cell and pivot. The section cells are smaller.
- Render time is 0.24–1.7 s per facing.
- An open door shows the furnished interior.

Figures are albedo MiB for a full sheet. The number in parentheses is the cost of a sheet holding only the facings that change, where that is smaller.

| Preset | Door open: facings changed · MiB | Half-open: facings · MiB | Bays / loft open: facings · MiB | Section, ground | Section, loft |
|---|---|---|---|---:|---:|
| netShed | 8 · 4.19 | 7 · 4.19 | — | 3.27 | 3.43 |
| redShed | 8 · 4.65 | 8 · 4.65 | — | 3.62 | 3.77 |
| tealShack | 7 · 4.38 | 6 · 4.38 (2.19) | — | 3.45 | 3.58 |
| gambrelBarn | 5 · 6.51 (3.26) | 5 · 6.51 (3.26) | loft 5 · 6.51 (3.26) | 4.83 | 5.44 |
| iceHouse | 8 · 6.05 | 5 · 6.05 (3.02) | loft 5 · 6.05 (3.02) | 4.51 | 5.06 |
| fishPlant | 6 · 13.72 (9.14) | 5 · 13.72 (9.04) | bays 5 · 13.72 (8.63) | 11.16 | 13.28 |
| cannery | 6 · 17.13 (10.28) | 5 · 17.13 (10.16) | bays 5 · 17.13 (9.75) | 11.05 (2048, 2 × 4) | 16.48 (4096) |
| **All that apply** | **56.63 (41.95)** | **56.63 (36.51)** | bays 30.85 (18.38) · loft 12.56 (6.28) | **41.89** | **51.04** |

Under L2, multiply by 3.25. For example:
- Door open for all seven costs 184.06 MiB (136.34 with changed facings only).
- The ground section costs 136.15 MiB, and the loft section 165.86 MiB.

For the seven presets, the door-open state plus both sections totals:
- **Full sheets:** 149.56 MiB albedo, 486.07 MiB under L2.
- **Changed facings only:** 134.88 MiB albedo, 438.35 MiB under L2.

The net shed with its door open, at all eight facings, is in the lane's evidence as a 2400 × 610 strip, not committed. Plating it in Unity needs the owner's word to bake one door-open sheet (4.19 MiB).

## The re-bake list

**Ruled 2026-09-29: re-bake the four in pass 2, in albedo.** The slot's bake landed every albedo figure below exactly: each sheet's cell, grid, sheet size and MiB. The L2 figures in parentheses were not taken.

| Placed sheet (build) | Pass 1 today: cell · grid · sheet · MiB | Pass 2: cell @ offset · grid · sheet · MiB (L2) | Silhouette overlap, facings 0–7 | Pixels differing, all 8 |
|---|---|---|---|---:|
| `Village_ginnyWoodshed` (redShed, neglected) | 252 × 298 · 8 × 1 · 2016 × 298 · 2.29 | 292 × 310 @ (454, 560) · 7 × 2 · 2044 × 620 · 4.83 (15.71) | 0.931–0.955 | 333,395 |
| `Village_ginnyNetStore` (netShed, ruin) | 328 × 224 · 6 × 2 · 1968 × 448 · 3.36 | 376 × 255 @ (412, 643) · 5 × 2 · 1880 × 510 · 3.66 (11.89) | 0.704–0.777 | 252,106 |
| `Village_ginnyLeanTo` (tealShack, collapsing) | 388 × 346 · 5 × 2 · 1940 × 692 · 5.12 | 402 × 348 @ (399, 561) · 5 × 2 · 2010 × 696 · 5.34 (17.34) | 0.746–0.834 | 390,116 |
| `Village_stPetersCannery` (cannery, collapsing) | 840 × 673 · 4 × 2 · 3360 × 1346 (4096) · 17.25 | 892 × 681 @ (154, 378) · 4 × 2 · 3568 × 1362 (4096) · 18.54 (60.25) | 0.822–0.914 | 1,857,858 |
| **Four sheets** | **28.03** | **32.37 (105.19)** | | |

What each column means:
- **Silhouette overlap** is the covered pixels the two passes share, divided by the larger coverage, in the same cell.
- **The before/after pictures** at facings 0–7 are in the lane's evidence, not committed.

The re-bake keeps these unchanged:
- **Names and count.** Each sheet keeps its eight sprites named `{stem}_d{facing}`. `VillageBuildingKit.SpriteNameFor` builds the names without reference to the grid, so a new grid changes no name.
- **Pivot at the ground centre.** For the cannery, pass 1's (420, 404) and pass 2's (446, 402) both land on the cell's (600, 780).
- **Caps.** The cannery stays one 4096 sheet, and the three sheds stay at or under 2048.
- **Registration.** The re-bake runs through the baker and regenerates `Buildings.json`, which is never hand-edited.

## Tests and verification

**New: `WharfBuildingPass2IntakeTests`, 8 methods and 12 cases, all EditMode:**

| Test | Cases | Production subject |
|---|---:|---|
| `DeliveredKitMatchesPinnedManifestAndLfBytes` | 1 | The delivered kit: the manifest's own hash, 28 lines, LF text, 28 files on disk, and the prop rig's line against its one home |
| `TheCatalogLoadsTheReadmeChain_FromMainsLibraries` | 1 | `RigCatalog.Buildings.cs`: install order, pass 1's geometry, presets, no viewer library, and the `lib/` copies equal to main's |
| `TheRigTurnsCounterClockwise_AsTheBuildingProbeReadsIt` | 3 (netShed, gambrelBarn, cannery) | The `wharfBuilding2` convention, with the C# `BuildingRigAzimuthProbe` |
| `TheLifecycleReachesPassTwo_AndEveryStateDrawsItsOwnShell` | 1 | The `buildingLifecycle` prerequisite |
| `WithTheHookOff_ASoundBuildDrawsTheSameBytes` | 3 (netShed, gambrelBarn, fishPlant) | The hook inside the model build |
| `EverySidecarParses_NamesADefinedPreset_AndIsTheLiveModelsBytes` | 1 | The seven gameplay sidecars |
| `TheFourPlacedSheets_ArePassTwosBake_PixelForPixel` | 1 | The four committed placed sheets and their `Buildings.json` rows: no row is left on pass 1, each row's options are the kit's build, and every facing is pass 2's render, pixel for pixel. Pass 1's chain is loaded first in the same host, so a match also proves the two globals do not collide. |
| `TheFourPlacedSheets_KeepTheirSpriteNamesAndCount_AndEverySpriteStPetersDraws` | 1 | The four sheets' metas and `StPeters.unity`: eight sprites named `{stem}_d0`…`d7`, and every sprite id the scene holds still in its sheet |

**Replaced in Phase B (never on main):** Phase A's `PassOnesFourPlacedSheets_DrawTheirCommittedPixels_WithPassTwoLoaded`. The committed sheets are now pass 2's, so pass 1 no longer draws them. The first new test above keeps its subject, the committed pixels, with pass 1 loaded beside pass 2.

**Moved (data only):**
- Two Baseline rows were added to `RigCatalogAssemblyTests`, and no assertion changed. As a result, `TheCatalogHoldsExactlyTheKeysMainDeclared` and `EveryEntryCarriesTheValuesMainDeclared` now cover the two new keys. Their subject is `RigCatalog.Buildings.cs`.
- In `VillageReturnIntakeTests.EveryDoorTheProbeCanReadCirclesThePivotTheWayItsSideSays`, the wharf row probes `wharfBuilding2`, the rig the kit's four wharf builds now name. On `wharfBuilding` it would select no build and silently probe nothing. Its subject is `BuildingRigAzimuthProbe`'s door-loop reading.

**New, PlayMode: `WharfBuildingPass2PlatePlayTests`, 1 case.** `StPeters_TheCanneryAndGinnysSheds_AtNoonGoldenHourAndNight` plates the cannery and Ginny's three sheds, and reads which pass is placed from the contract. It also records the four sheets' texture MiB, their draw pairs, SetPass calls and frame time, with and without the four. With no GPU it skips, so CI skips it.

No test is renamed, retired or disabled on main.

**Forecast.** Counts are total/passed/skipped/failed.

| Against | Its EditMode | Forecast EditMode | Its PlayMode | Forecast PlayMode |
|---|---|---|---|---|
| Phase A: the base 45a09dd6 (run 36429463471) | 13317/13065/252/0 | 13328/13076/252/0 | 1007/923/84/0 | 1007/923/84/0 |
| Phase A's own run 36505315023, merged with main f0d88ebe | — | 13385/13133/252/0 (landed) | — | 1007/923/84/0 (landed) |
| **Phase B: settle-f0d8, main f0d88ebe (run 36504563216)** | 13374/13122/252/0 | **13386/13134/252/0** | 1007/923/84/0 | **1008/923/85/0** |

**Compiled without Unity (Phase A).** Each asmdef was built with Unity 6000.5.0f1's own Roslyn: 30 asmdefs, with no borrowed HiddenHarbours DLL read. All five runs pass:
- the working tree;
- the base;
- the production edit reverted with the tests kept (they name the new key as data, so a lost row fails at run time, by name);
- a broken catalog row, which fails `RigBaking.Editor` alone;
- broken test files, which fail the test assembly alone.

**Run without Unity (Phase A)** (.NET 8, over the compiled assemblies): 18 cases, covering the new fixture, `RigCatalogAssemblyTests` and `NavBuoyRegistrationProbeTests.EveryRegisteredGlobalIsUnique`.
- **17 pass.** Phase A's pixel test needed the engine (`Texture2D.LoadImage`), so CI ran it; the same comparison passed on Node, above.
- **`MultiunitKitIntakeTests.OneInteriorPropRig`** needs the engine for its repo root. Its rule was checked on the box directly and holds: exactly one `interiorPropRig.js` under `docs/art/rigs/`, at the top level.
- **Three controls fail the tests they should:**
  - the key without `buildingLifecycle`;
  - the key without `coastalPass` (nine cases fail on `CoastalPass.light`);
  - the catalog without the rows (the Baseline reports "DROPPED 2").

**Not measured in Phase A:** bake time in Unity, texture residency, draw calls and frame time. Phase B measured them, below.

## Decisions (ruled 2026-09-29)

1. **The four placed buildings: re-baked in pass 2.** +4.34 MiB of albedo (28.03 → 32.37), measured in Unity. Names, count, sprite ids, pivots and caps stay as they were.
2. **The light for the wharf buildings: albedo in the new look.**
   - The seven presets cost 56.63 MiB as albedo (184.06 MiB under L2).
   - The four placed sheets cost 32.37 MiB as albedo (105.19 MiB under L2).
   - No L2 maps and no `BuildingLightFrame` adapter. L3 is an option only.
3. **The fish plant's sheet: one 2048 sheet (3 × 3, 13.72 MiB).**
4. **The placed cannery's floor: 1.2 m**, pass 2's default.
5. **Door-open plate: one door-open net shed sheet**, baked for the plate and kept in the lane's evidence.
6. **Noted, not in this PR:**
   - the doors' open states and the interiors (ADR 0036, a later PR);
   - Nine Mile Creek's sheds (M2-40/46, placement);
   - `CHAR_REQUESTS`;
   - the sign lettering;
   - a pass-2 dial set for the Building Studio;
   - the baker's grid waste.

## Phase B (the slot, 2026-09-29)

Setup:
- One batch-mode editor at a time, with `-force-d3d11`.
- Unity 6000.5.0f1, Direct3D 11, on an NVIDIA GeForce RTX 4060.
- The box's first Library, so launch 1 was a full import.

All five launches exited 0, and the save's sha256 was unchanged after each.

| Launch | What | Minutes | Result | Lowest headroom (GiB) |
|---|---|---:|---|---:|
| 1 | The import | 2.7 | 0 compile errors | 11.48 |
| 2 | The before plates | 0.8 | 1/1 passed | 14.30 |
| 3 | The one bake | 0.5 | 12 sheets baked, 0 failed; 9 of 9 placed sheets verify | 15.78 |
| 4 | The after plates and the door plate | 0.8 | 2/2 passed | 13.47 |
| 5 | EditMode, the 15 fixtures nearest the change | 0.8 | 223/223 passed | 15.20 |

### The bake

The bake ran in one launch, through the committed entry points:
- **The placed four** (`VillageBuildingBakeMenu.BakeAlbedoOnlyBuildsFromCommandLine`), 4.9 s. This wrote their sheets, their sidecars and `Buildings.json`.
- **The slice** of every sheet under the village root, 2.7 s: 24 sheets, 0 failed. The verify passed 9 of 9 placed sheets.
- **The seven presets and the door-open net shed**, 6.9 s.

| Placed sheet | Grid of cell | Sheet (cap) | MiB | PNG bytes | Render / total |
|---|---|---|---:|---:|---|
| `Village_ginnyWoodshed` | 7 × 2 of 292 × 310 | 2044 × 620 (2048) | 4.83 | 194,582 | 0.1 / 0.8 s |
| `Village_ginnyNetStore` | 5 × 2 of 376 × 255 | 1880 × 510 (2048) | 3.66 | 210,601 | 0.1 / 0.5 s |
| `Village_ginnyLeanTo` | 5 × 2 of 402 × 348 | 2010 × 696 (2048) | 5.34 | 301,938 | 0.4 / 0.8 s |
| `Village_stPetersCannery` | 4 × 2 of 892 × 681 | 3568 × 1362 (4096) | 18.54 | 1,222,879 | 0.8 / 2.1 s |
| **Four sheets** | | | **32.37** (was 28.03) | | |

What the bake left unchanged:
- In `Buildings.json`, only the four rows changed: rig, global, options, grid, crop, pivot, sheet size and bytes. The five house rows and the header are unchanged.
- Each sheet's meta keeps its guid and its eight `{stem}_d0`…`d7` names, with the same file ids.

| Preset (evidence only) | Grid of cell | Sheet (cap) | MiB | PNG bytes | Render / total |
|---|---|---|---:|---:|---|
| netShed | 6 × 2 of 300 × 305 | 1800 × 610 (2048) | 4.19 | 182,654 | 0.1 / 0.5 s |
| redShed | 7 × 2 of 280 × 311 | 1960 × 622 (2048) | 4.65 | 186,527 | 0.2 / 0.5 s |
| tealShack | 6 × 2 of 304 × 315 | 1824 × 630 (2048) | 4.38 | 195,280 | 0.2 / 0.4 s |
| gambrelBarn | 5 × 2 of 398 × 429 | 1990 × 858 (2048) | 6.51 | 340,804 | 0.4 / 0.8 s |
| iceHouse | 5 × 2 of 384 × 413 | 1920 × 826 (2048) | 6.05 | 367,336 | 0.3 / 0.8 s |
| fishPlant | 3 × 3 of 676 × 591 | 2028 × 1773 (2048) | 13.72 | 834,330 | 0.7 / 1.4 s |
| cannery | 5 × 2 of 714 × 629 | 3570 × 1258 (4096) | 17.13 | 988,251 | 0.7 / 1.4 s |
| **All seven** | | | **56.63** | | |
| netShed, door open | 6 × 2 of 300 × 305 | 1800 × 610 (2048) | 4.19 | 181,723 | 0.2 / 0.5 s |

- Every size and MiB above matches Phase A's figures.
- The preset sheets and the door-open sheet stay in the lane's evidence and are not committed. No wharf preset sheet has ever been committed, and nothing places one.
- Unity's render times are well under Phase A's per-facing figures from Node. The preset table above keeps the Node figures as measured.

### The plates

**Where and when.** St Peters, clear sky, at 12:00, 19:30 and 23:00, before (pass 1) and after (pass 2):
- The cannery and Ginny's yard are framed at ladder step 1, the 1:1 pivot, 33.75 m tall.
- Every subject was drawn and wholly in frame. The cannery shows facing d3; the yard shows the lean-to at d3, the net store at d7 and the woodshed at d5.

| | Before (pass 1) | After (pass 2) |
|---|---|---|
| The four sheets' GPU texture | 28.03 MiB | 32.37 MiB |
| The same in the editor (GPU plus CPU copy) | 56.06 MiB | 64.74 MiB |
| Format | R8G8B8A8_SRGB, 1 mip | the same |
| Draws from the four | 4 sheet+material pairs, Sprite-Lit-Default, so at least 4 | the same |
| SetPass calls at the cannery, with / without the four | 474 / 473 | 474 / 473 |
| SetPass calls at Ginny's yard, with / without the four | 1397 / 1397 | 1394 / 1394 |
| CPU frame at the cannery, median of 120, with / without | 6.806 / 6.739 ms | 7.003 / 7.003 ms |
| CPU frame at Ginny's yard, median of 120, with / without | 11.510 / 11.507 ms | 11.653 / 11.300 ms |

How to read the numbers:
- A batch-mode editor reads its draw-call and batch counters, and GPU time, as 0. The draws are therefore counted from the renderers.
- The frame is the editor's whole frame, not a player build.
- The four add 0.00–0.35 ms. The frame without them moved 0.21–0.26 ms between the two runs, so the difference is within the noise.

**The golden-hour plates are nearly black, before and after alike.** At 19:30 `_DayNightTint` is (0.075, 0.061, 0.103), darker than 23:00's (0.167, 0.188, 0.258). Drop 14's plates on main read the same tint at 19:30, so this is main's light curve, not this PR's.

**The door plate.** Pass 2's netShed preset stands on Ginny's net store's spot (95, 22), with the net store switched off:
- It is shut and open at the same three hours, framed at ladder step 3 (the widest a walker sees, 11.25 m).
- It shows facing d4, where the open door changes the most pixels (7005; d0 changes 141).
- Both sheets are 4.19 MiB.

### Returning the slot

- **Editors closed:** PIDs 30600, 32660, 23712, 19520 and 28184. None was running afterwards.
- **The save:** unchanged (5339 B, sha256 `EFC83B37…C9D03A`).
- **TestResults.xml:** the test launches rewrote the one beside the save (last at 11:47Z, 193,722 B), and it was left there.
- **Headroom:** the lowest was 11.48 GiB, during the import, against a floor of 10.
- **Library:** 4.86 GiB.
- **Reverted:** `runInBackground` was reverted by name. The untracked metas Unity wrote are not committed.
