# Wharf buildings pass 2 — drop 13, Phase A

This PR adds Claude Design's drop 13 to the repo without changing it, and registers its editor-only load chain beside pass 1. One byte-identical library stays at its single home on main instead of being copied in. The game does not bake, place or load anything from it. These stay unchanged: pass 1's rig, the four placed sheets, `Buildings.json`, the bakers, slicers and readers, the scenes, and both light twins. Pass 1 still draws what it drew before: this was measured, and a test re-measures it on every run. Phase B (the bake and the plates) waits for the owner's slot grant and the rulings at the end of this page.

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

## The light: L2 maps or albedo in the new look (a new decision)

On 09-27 the owner ruled drop 14's light as "L2 houses first, the rest albedo in the new look". #898 then landed the emit term in the shared law. Whether the wharf buildings take L2's maps is a new decision, so both options are priced below.

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

## Doors and interiors (options only: ADR 0036, a later PR)

Nothing in this section bakes in this PR. The options are:
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

## The re-bake list (if the owner rules a pass-2 re-bake)

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

**New: `WharfBuildingPass2IntakeTests`, 7 methods and 11 cases, all EditMode:**

| Test | Cases | Production subject |
|---|---:|---|
| `DeliveredKitMatchesPinnedManifestAndLfBytes` | 1 | The delivered kit: the manifest's own hash, 28 lines, LF text, 28 files on disk, and the prop rig's line against its one home |
| `TheCatalogLoadsTheReadmeChain_FromMainsLibraries` | 1 | `RigCatalog.Buildings.cs`: install order, pass 1's geometry, presets, no viewer library, and the `lib/` copies equal to main's |
| `TheRigTurnsCounterClockwise_AsTheBuildingProbeReadsIt` | 3 (netShed, gambrelBarn, cannery) | The `wharfBuilding2` convention, with the C# `BuildingRigAzimuthProbe` |
| `TheLifecycleReachesPassTwo_AndEveryStateDrawsItsOwnShell` | 1 | The `buildingLifecycle` prerequisite |
| `WithTheHookOff_ASoundBuildDrawsTheSameBytes` | 3 (netShed, gambrelBarn, fishPlant) | The hook inside the model build |
| `EverySidecarParses_NamesADefinedPreset_AndIsTheLiveModelsBytes` | 1 | The seven gameplay sidecars |
| `PassOnesFourPlacedSheets_DrawTheirCommittedPixels_WithPassTwoLoaded` | 1 | Pass 1 and the four committed placed sheets |

**Moved (data only):** two Baseline rows were added to `RigCatalogAssemblyTests`, and no assertion changed. As a result, `TheCatalogHoldsExactlyTheKeysMainDeclared` and `EveryEntryCarriesTheValuesMainDeclared` now cover the two new keys. Their subject is `RigCatalog.Buildings.cs`.

No test is renamed, retired or disabled.

**Forecast.** Counts are total/passed/skipped/failed. PlayMode is unchanged in both cases.

| Against | Its EditMode | Forecast EditMode | PlayMode |
|---|---|---|---|
| The base 45a09dd6 (run 36429463471) | 13317/13065/252/0 | **13328/13076/252/0** | 1007/923/84/0 |
| The seat's forecast for main 91ff97f2, which CI merges | 13331/13079/252/0 | **13342/13090/252/0** | 1007/923/84/0 |

**Compiled without Unity.** Each asmdef was built with Unity 6000.5.0f1's own Roslyn: 30 asmdefs, with no borrowed HiddenHarbours DLL read. All five runs pass:
- the working tree;
- the base;
- the production edit reverted with the tests kept (they name the new key as data, so a lost row fails at run time, by name);
- a broken catalog row, which fails `RigBaking.Editor` alone;
- broken test files, which fail the test assembly alone.

**Run without Unity** (.NET 8, over the compiled assemblies): 18 cases, covering the new fixture, `RigCatalogAssemblyTests` and `NavBuoyRegistrationProbeTests.EveryRegisteredGlobalIsUnique`.
- **17 pass.** The pixel test needs the engine (`Texture2D.LoadImage`), so CI runs it; the same comparison passed on Node, above.
- **`MultiunitKitIntakeTests.OneInteriorPropRig`** needs the engine for its repo root. Its rule was checked on the box directly and holds: exactly one `interiorPropRig.js` under `docs/art/rigs/`, at the top level.
- **Three controls fail the tests they should:**
  - the key without `buildingLifecycle`;
  - the key without `coastalPass` (nine cases fail on `CoastalPass.light`);
  - the catalog without the rows (the Baseline reports "DROPPED 2").

**Not measured in Phase A:** bake time in Unity, texture residency, draw calls and frame time. No slot has been used.

## Decisions for the owner

1. **The four placed buildings: re-bake them in pass 2, or keep pass 1.** The seat leans re-bake, so that St Peters reads in one light. A re-bake costs +4.34 MiB of albedo (28.03 → 32.37), and names, count, pivots and caps stay as they are.
2. **The light for the wharf buildings (new): L2 maps, or albedo in the new look.**
   - The seven presets cost 184.06 MiB under L2, or 56.63 MiB as albedo.
   - The four placed sheets cost 105.19 MiB under L2, or 32.37 MiB as albedo.
   - L2 needs the `BuildingLightFrame` adapter and a wharf occupancy rule. L3 is an option only.
3. **The fish plant's sheet.** Recommendation: **one 2048 sheet (3 × 3, 13.72 MiB)**, which today's baker already produces. The alternatives are one 4096 sheet (18.29 MiB) or two 2048 sheets (12.19 MiB).
4. **The placed cannery's floor.** Recommendation: **1.2 m**. `floorH: 0.6` keeps pass 1's door height.
5. **Noted, not in this PR:**
   - the doors' open states and the interiors (ADR 0036, a later PR);
   - Nine Mile Creek's sheds (M2-40/46, placement);
   - `CHAR_REQUESTS`;
   - the sign lettering;
   - a pass-2 dial set for the Building Studio;
   - the baker's grid waste.

## Phase B (the slot)

Before launch:
- `git lfs checkout`, offline.
- Record the save's sha256.

The bake:
- The seven presets in the ruled light, plus the four placed sheets if they are ruled a re-bake.
- `Buildings.json` regenerated through its baker.
- Each sheet's MiB and the bake time.

The plates:
- The St Peters cannery and Ginny's three sheds at noon, golden hour and night, before and after.
- The net shed with its door open.
- Texture MiB, draw calls and frame time.
