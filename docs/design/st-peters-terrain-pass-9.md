# St Peters, terrain pass 9: the plan

> - **Status:** PLAN, RULED. Session 1 of the terrain pass 9 arc. Docs only: no code, no asset, no scene. On 2026-09-25 the owner took every recommendation in §13.
> - **Lane:** art-pipeline, wearing world-content's hat for the island plan. Charter: `seat/HANDOFF-2026-09-25-terrain-pass-9-st-peters.md`.
> - **Pillars:**
>   - **P1 Sea Has Moods:** the tide floods the marsh and cuts three paths off.
>   - **P3 Living Working Coast:** named beaches, points and coves, and brooks that run to the sea.
>   - **P5 Cozy but with Teeth:** the plants take the trees' wind and snow, and a spring low bares flats 65 m out that flood back.
> - **Siblings:** `docs/design/seabed-and-terrain-look.md` · `docs/architecture/grass-field.md` · `docs/design/time-tides-weather.md` · ADRs 0003, 0004, 0010, 0011, 0012, 0014, 0019, 0021, 0028.
> - **The drop:** `Pixel art capabilitiesterrainpass9.zip`: 16,163,044 B, sha256 `5e69ecbf7f8c32fbfb5c8a76199391be03d0ce338beff31b13a79924e476c388`.
> - **Pictures:** 15 PNGs, 1.44 MB in all, in LFS under `docs/design/st-peters-terrain-pass-9/` (listed in Appendix B).

---

## 0. In plain words

🔴 **The stop comes first.** The handoff (§3.8) says to report it now.

Seven St Peters layers stand on ground this plan moves, and each of them can change only through `StPetersBuilder.Build()`:
- the grass field;
- the shore plants;
- the shrubs;
- the flowers;
- the shoreline rocks;
- the clam holes;
- the reef north mark's recorded depth.

St Peters has no refresh command. Its builder's only entry point is the full rebuild, which wipes the hand-authored scene (`StPetersBuilder.cs:1745-1747`, `RegionBuildGuard`; ADR 0019), so `Build()` is never run on it. As chartered, **that stops PR 5.** This PR does not work around it: it runs no `Build()`, edits no scene and adds no refresh code. Two ways forward are in §10. **On 2026-09-25 the owner ruled for the first (decision 10):**
1. The ruled way:
   - retire the four plant layers at St Peters in favour of the new plant field, which is computed when the scene loads and so never goes stale;
   - add a small **PR 4b** that refreshes the other three layers in place.
2. Not chosen: hand-write every moved object into PR 5's scene YAML.

**A second finding the handoff did not list.** St Peters' simulation reads the *analytic* terrain, not the painted height map. The scene has one enabled `TidalTerrain` and no `PaintedTidalTerrain`.
- So a new coast in the painted map changes only the picture. Walking, sailing, fishing, the fleet and the shore plants' tide response do not see it until the scene adopts the painted map.
- At 8 bits, that adoption would move the bar crest from +0.88 to +0.86 m, which shifts the owner's tide window.
- So the plan stores the height map at 16 bits (decision 11), and a guard holds the protected ground to one 16-bit step.

**What the plan builds:**
- **The north shore gets seven named sections,** from the bar head round to the cannery: Lagoon Flats, Lagoon Marsh, North Point, North Cove, Reef Point, Ginny's Cove and Cannery Shingle.
  - These make two rock points (North Point and Reef Point), a shingle foreland (Cannery Point) and three bays (the lagoon, North Cove and Ginny's Cove).
  - The intertidal comes to 18,875 m² along the new shore.
  - A spring low bares 9,429 m² more ground than today.
- **Three brooks run to the sea:** Barren Brook, Cove Brook and Alder Run.
  - Two of them come out of ponds: Bog Pond on the barren and Fen Pool in the swale.
  - Each brook has a fresh reach above the tide and a tidal reach that the sea floods by its height alone.
  - The marsh also has two tidal side creeks and four salt pans.
- **Paths:**
  - a shore path loops the whole new coast, from the bar head to the cannery;
  - a barren path, a reef walk and Ginny's cart track tie in the ponds, the reef and the cove;
  - three crossings flood: the bar walk (as today), the outer end of the reef walk, and the foot of Ginny's track.
- **Plants:** the five example scenes grow where their ground is found, at each scene's own density, material by material.
  - That is 18,182 plants, stored in one field of 56,044 B (at most one plant per 0.5 m cell).
  - The worst screen holds fewer ground-cover renderers than today's worst screen, but more batches. Both stay inside the budget test's limits (§6.4).
- **Nothing that must not move moves.** This covers:
  - the buildings, roads, yards and wharf;
  - the berths, the dredged approach and the arrival route;
  - the cliff walls and the woods;
  - the bar and its gut, the passages, and the fleet's grounds.

  All of it is inside a frozen mask of 937,122 pixels, where the ground changes by at most 0.0002 m. 546 placed objects keep their ground exactly (§9).

**What I would like the owner to look at first:** `plan-north.png` for the new shore, `flood.png` for the tide, and the seven previews. Then the decisions in §13.

---

## 1. Phase 0: the drop

### 1.1 Identity
- **The zip:**
  - 16,163,044 B, and its sha256 matches the paste;
  - 75 entries, all of them files under `Export/pixel-terrain-pass-9/`, 16,709,886 B unpacked;
  - every entry has the same mtime, 2026-09-24 23:32:28, four minutes after the README's `Generated 2026-09-24T23:28:22.116Z`.
- **The manifest:**
  - bundle `pixel-terrain-pass-9`, generatedAt `2026-09-24T23:28:22.116Z`;
  - render settings 520 × 450, frame 0, particlesTick 0, tideSlider 0.45, windDir 1.
- **No SHA256SUMS.**
  - I checked the manifest's `rigHashes` against the files: **11 of 11 MATCH**.
    - In `kit/lib/`: `pixelLanguage`, `pxKit`, `pxKit2`, `weatherSky`, `terrainLight4`, `pxKit8`, `plantIsoRig4`, `treeIsoRig4`.
    - In `kit/`: `terrainLight5`, `pxKit9`, `plantIsoRig5`.
  - Every file's sha256 is in `Evidence~/drop.sha256` (evidence, not committed).
- **Plants: the drop has 48 species of kind `plant`.** The handoff counted 47. They are 20 shrubs, 16 shore plants, 8 flowers and 4 grasses.

### 1.2 Node checks
- **Setup:**
  - Node v24.19.0, on plain Node with no browser.
  - I drove the kit only through its public API, from my own script (`Evidence~/harness/verify_drop.js`), and never edited the kit.
- **Rebuild:** each example scene was rebuilt from `pxKit9`, `terrainLight5` and `plantIsoRig5` at the manifest's render settings.
  - Placements are **identical in 5 of 5 scenes**. Sprites per scene: coast 149, marsh 147, meadow 138, barren 133, swale 96.
  - **All 55 PNGs are identical to the drop's, compared by pixel:** 0 pixels differ, and the largest channel difference is 0. The 55 are 5 scenes × (7 lighting presets + 4 maps).
- Every rig ran, and no re-render needs explaining.

### 1.3 Table 1: the 19 materials against main's 22
**The two sets.**
- Main's 22 materials are its 20 splat slots (A to E) plus `bank` (the edge strips) and `sandstone` (the cliff faces).
- The drop paints 19: the 18 it shares with main, plus the new `path`.
- `oysterreef` and `bank` are in the drop's kit, but no scene paints them.

**The columns.**
- *Lo, Mid, Hi:* the pixels (out of 65,536) where the drop's pass-8 albedo differs from main's live tile, at each ladder step (`_Lo`, plain, `_Hi`).
- *Luma:* the mean brightness of main's tile, then of the drop's, at Mid.
- *Last column:* the pixels where `terrainLight5`'s unlit differs from pass 8's unlit, at Mid.

| Material | Main slot | Drop scenes that paint it | Lo | Mid | Hi | Luma, main → drop (Mid) | TL5 unlit vs pass-8 unlit (Mid) |
|---|---|---|---:|---:|---:|---|---:|
| grass | A.r | coast, barren, marsh, meadow, swale | 28,780 | 33,423 | 40,611 | 84.4 → 82.7 | 3,895 |
| marram | A.g | coast, marsh | 18,851 | 28,556 | 43,251 | 147.6 → 148.8 | 9,134 |
| sand | A.b | coast, marsh | 9,498 | 10,804 | 16,434 | 171.8 → 171.9 | 182 |
| shingle | A.a | coast | 0 | 0 | 0 | 77.5 → 77.5 (unchanged) | 7,566 |
| ripple | B.r | coast, marsh | 32,156 | 45,115 | 44,000 | 101.7 → 97.7 | 3,458 |
| shelf | B.g | coast, barren | 37,887 | 43,810 | 51,059 | 102.2 → 99.1 | 2,652 |
| silt | B.b | coast, barren, marsh, swale | 9,751 | 18,659 | 42,307 | 85.3 → 80.9 | 480 |
| dirt | B.a | coast, barren, meadow, swale | 15,995 | 18,191 | 21,688 | 99.0 → 97.9 | 1,472 |
| marsh | C.r | coast, marsh | 64,991 | 60,032 | 62,796 | 57.5 → 79.1 | 22,933 |
| sedge | C.g | coast, barren, marsh, meadow, swale | 46,507 | 47,060 | 50,282 | 56.2 → 56.8 | 23,211 |
| foreshore | C.b | coast | 25,773 | 37,326 | 38,181 | 125.2 → 121.3 | 674 |
| talus | C.a | coast, barren, meadow | 61,830 | 60,923 | 63,489 | 83.0 → 84.4 | 16,595 |
| ledge | D.r | coast | 43,093 | 40,995 | 48,151 | 89.2 → 86.5 | 3,144 |
| rockweed | D.g | coast | 50,127 | 57,755 | 62,274 | 78.5 → 68.2 | 23,930 |
| musselbed | D.b | coast, marsh | 35,640 | 61,029 | 61,390 | 63.4 → 50.6 | 32,245 |
| oysterreef | D.a | — | 19,713 | 33,774 | 50,153 | 54.8 → 56.6 | 13,684 |
| eelgrass | E.r | coast | 0 | 0 | 0 | 92.1 → 92.1 (unchanged) | 7,592 |
| irishmoss | E.g | coast | 62,115 | 63,977 | 64,780 | 70.6 → 84.7 | 31,818 |
| lawn | E.b | — | main only | | | 77.7 | — |
| mud | E.a | barren, marsh, meadow, swale | 16,195 | 10,214 | 18,174 | 72.7 → 72.3 | 1,678 |
| bank | edge strips | — | 0 | 0 | 0 | 66.2 → 66.2 (unchanged) | 2,560 |
| sandstone | cliff faces | — | main only | | | 71.3 | — |
| **path** | none (needs _SplatF) | coast, barren, meadow, swale | new | new | new | — → 125.3 | 5,206 |

What the table says:
- **The comparison is sound.** The drop's older kits reproduce main exactly: pass 6 (`pxKit`/`pxKit2`) gives 0 differing pixels against main's live tiles for every shared material.
- **Pass 8 repaints 17 of main's 20 kit materials.** It keeps `shingle`, `eelgrass` and `bank` byte for byte, and adds `path`. The biggest shifts in brightness:
  - `marsh`, 57.5 → 79.1;
  - `irishmoss`, 70.6 → 84.7;
  - `musselbed`, 63.4 → 50.6;
  - `rockweed`, 78.5 → 68.2.
- **`terrainLight5`'s unlit differs from every pass-8 tile,** by 182 to 32,245 pixels. So PR 2 bakes the albedo from `terrainLight5`'s unlit, not from pass 8's tiles.
- **`lawn` and `sandstone` exist only on main.**
  - The plan keeps `lawn` for the village greens.
  - It repaints the two roads in the new `path` material.
  - `sandstone` stays on the cliff faces, which are CLIFFS' lane.

### 1.4 Table 2: the 48 plants against main's four families
Main's four families hold 125 PNGs at St Peters (grass 26, shore plants 48, shrubs 18, flowers 33). 116 have a pass-9 successor; 9 do not (the 6 tufts and the flower WildRose's 3).

**2a. Shrubs.** The drop's 20 are the same keys as main's `docs/art/rigs/shrubIsoRig.js`.

| Species | Habitat | St Peters sprite today | Pass 9 |
|---|---|---|---|
| LowbushBlueberry | barren | `LowbushBlueberry_full_atGreen`, `LowbushBlueberry_full_atGreen_calendar`, `LowbushBlueberry_full_atGreen_light` | replaces |
| SheepLaurel | barren | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| Rhodora | barren | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| BlackHuckleberry | barren | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| CommonJuniper | barren | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| Leatherleaf | bog | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| SweetGale | bog | `SweetGale_full_atGreen`, `SweetGale_full_atGreen_calendar`, `SweetGale_full_atGreen_light` | replaces |
| WinterberryHolly | bog | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| SpeckledAlder | swale | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| PussyWillow | swale | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| RedOsierDogwood | swale | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| Meadowsweet | swale | `Meadowsweet_full_atGreen`, `Meadowsweet_full_atGreen_calendar`, `Meadowsweet_full_atGreen_light` | replaces |
| Steeplebush | swale | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| Raspberry | edge | `Raspberry_full_atGreen`, `Raspberry_full_atGreen_calendar`, `Raspberry_full_atGreen_light` | replaces |
| WildRose | edge | `WildRose_full_atGreen`, `WildRose_full_atGreen_calendar`, `WildRose_full_atGreen_light` | replaces |
| StaghornSumac | edge | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| Serviceberry | edge | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| BeakedHazelnut | woods | `BeakedHazelnut_full_atGreen`, `BeakedHazelnut_full_atGreen_calendar`, `BeakedHazelnut_full_atGreen_light` | replaces |
| WildRaisin | woods | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |
| RedElderberry | woods | — (key in main's `shrubIsoRig.js`, never baked for St Peters) | first bake |

**2b. Shore plants.** The drop's 16 are the same species as main's 16 `ShorePlantDef`s.

| Species | Tide zone | Def today | Sprite today | Pass 9 |
|---|---|---|---|---|
| SugarKelp | fringe | `shoreplant.sugar_kelp` | `SugarKelp_summer_full_v0` (+ `_light`, `_tide`) | replaces; **no drop scene places it** (main places 90 today) |
| IrishMoss | fringe | `shoreplant.irish_moss` | `IrishMoss_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| Eelgrass | fringe | `shoreplant.eelgrass` | `Eelgrass_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| KnottedWrack | mid | `shoreplant.knotted_wrack` | `KnottedWrack_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| Bladderwrack | mid | `shoreplant.bladderwrack` | `Bladderwrack_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| SeaLettuce | mid | `shoreplant.sea_lettuce` | `SeaLettuce_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| Cordgrass | lowmarsh | `shoreplant.cordgrass` | `Cordgrass_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| Glasswort | lowmarsh | `shoreplant.glasswort` | `Glasswort_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| SaltmeadowHay | highmarsh | `shoreplant.saltmeadow_hay` | `SaltmeadowHay_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| BlackRush | highmarsh | `shoreplant.black_rush` | `BlackRush_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| Cattail | highmarsh | `shoreplant.cattail` | `Cattail_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| Threesquare | highmarsh | `shoreplant.threesquare` | `Threesquare_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| MarramGrass | upland | `shoreplant.marram_grass` | `MarramGrass_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| Bayberry | upland | `shoreplant.bayberry` | `Bayberry_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| SweetFern | upland | `shoreplant.sweet_fern` | `SweetFern_summer_full_v0` (+ `_light`, `_tide`) | replaces |
| BeachPea | upland | `shoreplant.beach_pea` | `BeachPea_summer_full_v0` (+ `_light`, `_tide`) | replaces |

**2c. Flowers.** The drop has 8. Main has 33 PNGs over 9 kinds.

| Species | Habitat | Sprites today | Pass 9 |
|---|---|---|---|
| Lupin | roadside | `LupinBlueClump`, `LupinBlueSingle`, `LupinPatch`, `LupinPinkClump`, `LupinPinkSingle`, `LupinPurpleClump`, `LupinPurpleSingle`, `LupinWhiteClump`, `LupinWhiteSingle` | replaces; the four colours become the rig's four morphs |
| LadySlipper | woods | `LadySlipperClump`, `LadySlipperPatch`, `LadySlipperSingle` | replaces |
| Fireweed | roadside | `FireweedClump`, `FireweedPatch`, `FireweedSingle` | replaces |
| QueenAnne | roadside | `QueenAnneClump`, `QueenAnnePatch`, `QueenAnneSingle` | replaces |
| Goldenrod | roadside | `GoldenrodClump`, `GoldenrodPatch`, `GoldenrodSingle` | replaces |
| OxeyeDaisy | meadow | `OxeyeDaisyClump`, `OxeyeDaisyPatch`, `OxeyeDaisySingle` | replaces |
| BlueFlag | wet | `BlueFlagClump`, `BlueFlagPatch`, `BlueFlagSingle` | replaces |
| Buttercup | meadow | `ButtercupClump`, `ButtercupPatch`, `ButtercupSingle` | replaces |
| — |  | `WildRoseClump`, `WildRosePatch`, `WildRoseSingle` | **no flower successor**: the shrub WildRose (Table 2a) continues |

**2d. Grasses.** The drop has 4. Main has 26 tufts. They map onto 7 pass-9 species, 3 of which are shore species (Table 2b), and 6 tufts have no successor.

| Tufts today (`Art/Sprites/Grass`) | PNGs | Pass 9 species | Pass 9 |
|---|---|---|---|
| `GrassTuft_MeadowMidA`, `GrassTuft_MeadowMidB`, `GrassTuft_MeadowMidC`, `GrassTuft_MeadowShortA`, `GrassTuft_MeadowShortB`, `GrassTuft_MeadowShortC`, `GrassTuft_MeadowTallA`, `GrassTuft_MeadowTallB` | 8 | MeadowGrass | replaces |
| `GrassTuft_Timothy`, `GrassTuft_TimothyLean` | 2 | Timothy | replaces |
| `GrassTuft_Rush`, `GrassTuft_RushLean` | 2 | SoftRush | replaces |
| `GrassTuft_Sedge`, `GrassTuft_SedgeLow` | 2 | TussockSedge | replaces |
| `GrassTuft_Cattail`, `GrassTuft_CattailPair` | 2 | Cattail (a shore species, Table 2b) | replaces |
| `GrassTuft_MarramA`, `GrassTuft_MarramB` | 2 | MarramGrass (a shore species, Table 2b) | replaces |
| `GrassTuft_Saltmeadow`, `GrassTuft_SaltmeadowSwirl` | 2 | SaltmeadowHay (a shore species, Table 2b) | replaces |
| `GrassTuft_ClumpWideA`, `GrassTuft_ClumpWideB`, `GrassTuft_FringeA`, `GrassTuft_FringeB`, `GrassTuft_HeadlandA`, `GrassTuft_HeadlandB` | 6 | — | **no successor** |

What the table says:
- **No species is new to the game.** Each of the 48 already has a rig key, a sprite or a Def on main. The grasses have them under older tuft names: `Meadow*` → MeadowGrass, `Rush` → SoftRush, `Sedge` → TussockSedge.
  - 14 shrubs get their first bake for St Peters.
  - 32 species get their first Def (rule 2): shrubs, flowers and grasses are catalog entries today, while the 16 shore plants already have `shoreplant.*` Defs.
- **Main's sprites with no successor:**
  - the tufts `FringeA/B`, `HeadlandA/B` and `ClumpWideA/B`;
  - the flower WildRose's three sprites. The shrub WildRose carries on.
- **SugarKelp has a gap.** PlantRig5 draws it, but none of the drop's scenes places it, so the plan's field has none. Main places 90 kelp at St Peters today. Decision 3 adds it to the fringe recipe.
- **The old families are shared with Nine Mile Creek.** `NineMileCreekFieldsTests` and `NineMileCreekShoreTests` guard them there. So they retire at St Peters only, by removing St Peters' roots in PR 5. Their code, Defs and tests stay.

### 1.5 Table 3: the engine contract's maps against today's shader inputs
| Map (the drop's README) | Channels | Feeds today | Note |
|---|---|---|---|
| terrain `_unlit` | the authored bands, un-baked, under a flat sky | `_DetailArr256` (the splat shader's albedo array) | Same slot, new meaning. Today it holds the baked px albedo; PR 2 re-bakes it from this map. |
| terrain `_normal` | R x · G y-up · B up, in floor space | **none** | The splat shader has no normal input. |
| terrain `_light` | R sky visibility · G porosity · B height | **none** | |
| terrain `_detail` | RG mark id · B surface class × 28 | **none** | |
| plant `_unlit` | structure bands under a flat sky | `_MainTex` (the grass, shore-plant, shrub and flower shaders) | |
| plant `_normal` | R x · G y-up · B toward the camera | `_LightNormal` | Same packing. Only the trees read it today. |
| plant `_light` | R sky visibility · G translucency · B height | **none as-is** | Today's `_LightMask` is R key · G rim · B depth · A coverage, which means something else. PR 3's shader reads the new map directly, or a converter re-packs it. |
| plant `_detail` | RG stamp id + 1 · B part | **none** | |

**Main's maps with no pass-9 source:**
- the shrubs' `_calendar` (R veil · G ornament · B snow · A coverage);
- the shore plants' `_tide` sheets.

PlantRig5 draws a sheet per season and draws a weed's depth itself: dark below 0.9 m, gone below 1.7 m. So PR 3's baker makes the tide ladder from the rig.

### 1.6 Not stops, and the PR that needs each
| Missing from the drop | What I used this session | Needed by |
|---|---|---|
| SHA256SUMS over every file | the manifest's `rigHashes` (11/11), and `Evidence~/drop.sha256` | PR 2 and PR 3, to commit the kit's bytes with their provenance |
| The checkers and their output | a pixel re-render of all 55 PNGs (0 pixels differ) | PR 2's plate, and PR 3's bake |
| A sheet spec per species | none; `PlantRig5.sheet()` exists | PR 3's baker and catalog |

The Claude Design paste asks for all three (decision 8).

---

## 2. Today's island

**How the pictures were made.**
- Both are drawn from the committed `Data/Terrain/StPetersSeabed_HeightTex.png` and `StPetersSplatA–E.png`.
- Each carries the five tide lines: spring low −2.2, neap low −0.99, mean 0, neap high +0.99 and spring high +2.2 m.
- Magenta marks what must not move.

**The pictures.**
- [`today-region.png`](st-peters-terrain-pass-9/today-region.png) is the whole region: 1520 × 1040 px at 0.5 m per pixel, which is 760 × 520 m.
- `today-island.png` (below) is the island at 2×: x −80 to 250, y −100 to 115.

![Today's island, with the tide lines and everything that must not move](st-peters-terrain-pass-9/today-island.png)

### 2.1 What must not move, and where it comes from
Code paths are under `Assets/_Project/Code/`. `StPetersBuilder.cs` is `App/Editor/StPetersBuilder.cs`.

| Feature | Where (world metres) | Source | Plan |
|---|---|---|---|
| **The beach slip** (berth) | (240, 0) → (190, 0), half-width 8 m, bed **−1.0 m** | `StPetersBuilder.cs:311-322` | frozen |
| **The dredged approach** ("the east dock always has water") | (255, 0) → (206, 0), half-width 8 m. It holds **0.40 m** under the arrival hull's keel at the lowest water (`:358`). | `StPetersBuilder.cs:379-418`, `:1097-1127` | frozen |
| **The dock, disembark and arrival points** | (211.5, −5.8), (211.5, −1.9) and (213.5, −5.8). Ground −4.00 m, so **1.80 m** of water at spring low. | `StPetersBuilder.cs:1051-1072`, `:1076`, `:1080` | frozen |
| **The dory berth** | (213.5, 4.2). Ground −3.96 m, so **1.76 m** of water at spring low. | `StPetersBuilder.cs:986-997` | frozen |
| **The arrival route and entrance** | (340, 40) → (262, 26) → (211.5, −5.8), half-width 10 m | `App/Editor/StPetersNavMarks.cs:94-110`, `App/Editor/StPetersArrivalOpening.cs:51-64` | 0.0000 m |
| **The wharf** | x 183 to 213.5, y −3 to 3 (201 placed objects) | `App/Editor/StPetersWharf.cs:44-60` | 0.0000 m |
| **The sandbar** | (−45, 0) → (−350, 0), half-width 30 m, crest **+0.88 m**, which floods at every tide | `StPetersBuilder.cs:426-445` | frozen; the bar head is paint-only |
| **The bar gut** | x −234.1, half-width 15 m, bed **−0.6 m**: a boat can cross it at high water | `StPetersBuilder.cs:446-448` | frozen |
| **The buildings** (ground +6.0) | school (−12, 33), general store (4, 31), white farmhouse (21, 26), red saltbox (25, 8), sage cottage (10, −20) | `StPetersBuilder.cs:747-751` | 8 m round each, frozen |
| **The post office** | (15, 43) | the scene's `IslandShops/postOffice` | frozen |
| **The cannery** | (170, 16) | `App/Editor/StPetersCannery.cs` | 12 m round it, frozen |
| **Ginny's camper and freezer** | (84, 42) and (78, 24) | `App/Editor/StPetersCamperLot.cs`, `App/Editor/StPetersGinnyPlot.cs:159` | frozen |
| **The village hearth and the start spawn** | (0, 14) and (5, 0) | `StPetersBuilder.cs:697`, `:597` | frozen |
| **The wet bucket** | (−59, 0), ground +3.14 | `StPetersBuilder.cs:727` | frozen |
| **The roads** | the slip road and the bar-head road, cart tracks 1.5 m wide | `App/Editor/StPetersStarterSplat.cs:215-372` (`VillageToSlipPath`, `VillageToBarHeadPath`, `BentPath`) | 3 m each side, frozen; lowest ground +6.0 |
| **The yards** | the 153 placed objects under `Yards` | `App/Editor/YardDressing.cs:45` (`RootName = "Yards"`) | each yard's outline + 3 m, frozen |
| **The passages** | Nine Mile Creek (−356, 0); West Water (−356, 150); East Water (356, 40), arriving at (316, 40) | `StPetersBuilder.cs:600`, `:614`, `:650`, `:669` | 10 m round each, and a 20 m map edge, frozen |
| **The cliff walls** | 79 walls under `CliffWalls` | `StPetersCliffWalls` (CLIFFS' lane) | 4 m round each wall, plus the whole south coast (bearing 96.6° to 252°), frozen |
| **The woods** | 259 trees under `IslandWoods` | `StPetersWoods`, `StPetersWoodsPlanter` | 3 m round each trunk, frozen; the woods floor within 7 m keeps today's understorey |
| **The ambient fleet's grounds** | the rectangle x −37.5 to 47.5, y −43 to −21. Its lanes are planned each day from the height map. | `Data/Boats/StPetersAmbientFleet.asset:23-24` (`GroundsCenter` (5, −32), `GroundsSize` 85 × 22); `Boats/AmbientFleetPresenter.cs:405-430` | frozen |
| **Anchorages** | **none exist.** The fleet uses moorings nowhere; the dory lies at the wharf. | `App/Editor/StPetersNavMarks.cs:30` | — |
| **The fishing grounds** | not places: a lattice over the whole region, banded by depth (tidepool to 0.6 m, shallows 0.6 to 3 m, inshore 3 to 10 m) | `GameServices.FishSchools` (`Fishing/FishSchoolModel.cs`); the bands are `Data/Config/GameConfig.asset:321-323` | the bands' areas move (§9) |
| **The nav marks** | 13 marks | `App/Editor/StPetersNavMarks.cs` | checked, not frozen (§9) |
| **The clam holes** | 66 holes | `ScatterClamHoles`, `StPetersBuilder.cs:1694` (defined at `:2620`) | checked (§9) |

### 2.2 Two findings about today
- **The ambient fleet's grounds lie on land.** 7,300 of the rectangle's 7,480 cells are dry at mean tide. Only 12 are deep water at spring low.
  - The boats do not fish on land. The fleet fishes only spots with at least `MinDepthMeters` of water at spring low, and a boat that finds none is hidden for the day (`Boats/AmbientFleetPresenter.cs:419-440`). So the grounds give the fleet almost nowhere to fish.
  - This is already true today. The plan leaves the rectangle untouched.
  - It is for gameplay-systems (decision 12).
- **Nothing to protect for anchorages.** St Peters has no anchorage or mooring field, and its fishing grounds are depth bands, not places.

---

## 3. The new coast

![The plan: the new north shore, with the tide lines, the materials and the preview windows](st-peters-terrain-pass-9/plan-north.png)

### 3.1 How a section is made
- **The reference line.** One line runs roughly along the new mean-tide line, from west to east with the land on its right: from the bar head (−60.6, 20.4) to the cannery (189, 37.5). A label starts each section, and the section runs to the next label.
- **The profile.** Each section is a cross-shore profile: distance from the line → elevation.
  - A `fill` section only raises the land side, so the old bluff stays behind what is laid in front of it.
  - A `cut` section *is* the ground between its knots.
  - Beyond the last seaward knot, the ground relaxes back to the analytic sea floor.
- **Blending.** Sections split by bearing about the island's centre, which is CoastPlan's own frame.
  - At the shore, the blend is 3° wide. Offshore it widens by 0.22° per metre past 8 m.
  - The line's two ends fade back to today's coast over 6°.
  - A small domain warp (1.4 m amplitude, 7 m wavelength) adds cusps and crooks.
  - Each section's edge wobble moves its shoreline along the normal.
- **The south coast and the bar stay as they are.** South of bearing 96.6° (the cliffs, the walls, the wharf) and the bar keep today's elevation. Only their paint is re-derived, as two paint-only sections (§3.5).
- **The order the ground is laid in:**
  1. each section type's recipe, by elevation;
  2. then the water (channels, pans, ponds);
  3. the barren's granite outcrops;
  4. the swale;
  5. the paths.

  Wherever the protection is total, the frozen mask keeps today's paint.

### 3.2 The sections
| Section | Id | Type | Reference line, from → to | Along the shore | Intertidal width | Spring-high line | Spring-low line | Intertidal area | Materials in the intertidal (m²) |
|---|---|---|---|---:|---:|---:|---:|---:|---|
| **Lagoon Flats** | `coast.stp_lagoon_flats` | sandy flats | (−60.6, 20.4) → (−24.0, 64.0) | 58 m | 76.3 m | +11.0 m | −65.3 m | 5,312 m² | ripple 3,133 · eelgrass 1,183 · silt 517 · sand 479 |
| **Lagoon Marsh** | `coast.stp_lagoon_marsh` | salt marsh | (−24.0, 64.0) → (18.0, 91.0) | 54 m | 97.7 m | +40.0 m | −57.7 m | 4,527 m² | silt 2,391 · ripple 1,320 · marsh 587 · musselbed 125 · mud 104 |
| **North Point** | `coast.stp_north_point` | ledge platform | (18.0, 91.0) → (35.0, 79.0) | 28 m | 22.1 m | +3.5 m | −18.6 m | 370 m² | rockweed 133 · ledge 111 · ripple 94 · talus 26 · irishmoss 5 |
| **North Cove** | `coast.stp_north_cove` | sand beach | (35.0, 79.0) → (64.0, 80.0) | 36 m | 46.9 m | +6.9 m | −40.0 m | 1,839 m² | ripple 728 · foreshore 642 · eelgrass 164 · silt 120 · sand 78 |
| **Reef Point** | `coast.stp_reef_point` | ledge platform | (64.0, 80.0) → (86.0, 74.0) | 32 m | 19.4 m | +4.6 m | −14.9 m | 631 m² | rockweed 242 · ledge 172 · ripple 76 · irishmoss 51 · eelgrass 42 |
| **Ginny's Cove** | `coast.stp_ginnys_cove` | mud flat | (86.0, 74.0) → (135.0, 75.0) | 51 m | 64.7 m | +4.4 m | −60.2 m | 3,778 m² | mud 1,730 · silt 1,245 · ripple 578 · musselbed 88 · marsh 74 |
| **Cannery Shingle** | `coast.stp_cannery_shingle` | shingle cobble | (135.0, 75.0) → (189.0, 37.5) | 68 m | 30.4 m | +5.3 m | −25.1 m | 2,418 m² | ripple 1,237 · shingle 435 · foreshore 412 · eelgrass 256 · rockweed 78 |

*Spring-high line* and *spring-low line* are distances from the reference line in metres, with + inland. The intertidal width is the distance between them.

![The whole island: the new coast, the paint-only sections and today's south coast](st-peters-terrain-pass-9/plan-island.png)

![What moves: orange is raised, blue is cut, grey is frozen](st-peters-terrain-pass-9/change.png)

The ground changes over 35,058 m². The largest raise is +4.89 m and the largest cut is −6.04 m.
- The raises are the marsh platform and the flats laid in front of the old bluff.
- The cuts are North Cove, Ginny's Cove and the Cannery Shingle, taken out of today's shelf edge.

### 3.3 The height profile across each intertidal
Each profile is given as distance from the line in m (+ inland) → elevation in m.

- **Lagoon Flats** (sandy flats, `fill`): 12 → +2.40 · 6 → +1.20 · 0 → 0.00 · −6 → −0.70 · −14 → −1.00 · −30 → −1.20 · −48 → −1.45 · −60 → −1.80 · −68 → −2.40. Relaxes to the sea floor over 14 m; edge wobble 3.0 m over 18.0 m.
- **Lagoon Marsh** (salt marsh, `fill`): 44 → +2.30 · 34 → +2.05 · 24 → +1.85 · 14 → +1.60 · 9 → +1.25 · 6 → +0.95 · 4.5 → +0.62 · 3 → +0.28 · 0 → 0.00 · −12 → −0.35 · −25 → −0.75 · −40 → −1.20 · −52 → −1.70 · −60 → −2.40. Relaxes to the sea floor over 12 m; edge wobble 3.5 m over 22.0 m.
- **North Point** (ledge platform, `fill`): 14 → +5.20 · 9 → +4.40 · 6 → +3.20 · 3.5 → +2.20 · 1.5 → +1.00 · 0 → +0.10 · −6 → −0.30 · −12 → −0.80 · −15 → −1.70 · −20 → −2.40. Relaxes to the sea floor over 10 m; edge wobble 2.0 m over 9.0 m, rock edge at −15 m.
- **North Cove** (sand beach, `cut`): 18 → +6.00 · 14 → +5.00 · 11 → +3.60 · 8 → +2.60 · 5 → +1.50 · 0 → 0.00 · −8 → −0.80 · −18 → −1.25 · −30 → −1.70 · −40 → −2.20 · −48 → −2.80. Relaxes to the sea floor over 12 m; edge wobble 2.0 m over 11.0 m.
- **Reef Point** (ledge platform, `fill`): 16 → +5.60 · 10 → +4.60 · 6 → +3.00 · 3.5 → +1.60 · 2 → +0.60 · 0 → +0.10 · −6 → −0.25 · −10 → −0.70 · −12 → −1.70 · −16 → −2.40 · −19 → −3.30. Relaxes to the sea floor over 8 m; edge wobble 2.5 m over 8.0 m, rock edge at −13 m.
- **Ginny's Cove** (mud flat, `cut`): 10 → +6.00 · 7 → +4.20 · 5 → +2.60 · 4 → +1.90 · 3 → +1.40 · 1.5 → +0.60 · 0 → +0.25 · −15 → −0.20 · −30 → −0.70 · −45 → −1.20 · −55 → −1.60 · −62 → −2.40. Relaxes to the sea floor over 12 m; edge wobble 2.5 m over 16.0 m.
- **Cannery Shingle** (shingle cobble, `cut`): 16 → +6.00 · 12 → +4.40 · 9.5 → +3.10 · 8 → +2.80 · 6 → +2.50 · 0 → 0.00 · −5 → −1.10 · −14 → −1.60 · −24 → −2.10 · −32 → −2.80. Relaxes to the sea floor over 12 m; edge wobble 1.5 m over 10.0 m.

**The recipes.** Each section type paints its ground by elevation, as below. These are the kit's bands, mapped onto St Peters' tide.

| Section type | Ground material by elevation (m) |
|---|---|
| sand beach | `eelgrass` below −2.00 · `ripple` −2.00 to −1.00 · `foreshore` −1.00 to +0.60 · `shingle` +0.60 to +1.10 · `sand` +1.10 to +2.40 · `marram` +2.40 to +3.80 · `grass` above +3.80 |
| shingle cobble | `eelgrass` below −2.00 · `ripple` −2.00 to −1.20 · `foreshore` −1.20 to −0.40 · `shingle` −0.40 to +2.90 · `marram` +2.90 to +3.60 · `grass` above +3.60 |
| ledge platform | `eelgrass` below −2.30 · `irishmoss` −2.30 to −1.43 · `rockweed` −1.43 to −0.05 · `ledge` −0.05 to +2.40 · `shelf` +2.40 to +4.20 · `grass` above +4.20 |
| salt marsh | `ripple` below −1.30 · `silt` −1.30 to +0.66 · `marsh` +0.66 to +2.35 · `sedge` +2.35 to +2.80 · `grass` above +2.80 |
| mud flat | `ripple` below −1.80 · `mud` −1.80 to −0.30 · `silt` −0.30 to +0.60 · `marsh` +0.60 to +1.50 · `shingle` +1.50 to +2.60 · `sedge` +2.60 to +3.40 · `grass` above +3.40 |
| sandy flats | `eelgrass` below −1.60 · `ripple` −1.60 to −0.50 · `sand` −0.50 to +2.40 · `marram` +2.40 to +3.80 · `grass` above +3.80 |

**Area by tide band,** in m²:

| Section | below −2.2 | −2.2 to −0.99 | −0.99 to 0 | 0 to +0.99 | +0.99 to +2.2 | +2.2 to +4 | above +4 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Lagoon Flats | 1,024 | 4,088 | 952 | 141 | 132 | 172 | 3,098 |
| Lagoon Marsh | 1,120 | 1,832 | 1,829 | 341 | 525 | 254 | 1,712 |
| North Point | 0 | 126 | 134 | 54 | 55 | 130 | 318 |
| North Cove | 1,422 | 1,002 | 653 | 96 | 88 | 135 | 208 |
| Reef Point | 194 | 210 | 267 | 98 | 56 | 78 | 782 |
| Ginny's Cove | 1,240 | 1,669 | 1,525 | 491 | 94 | 110 | 256 |
| Cannery Shingle | 1,080 | 1,688 | 366 | 170 | 195 | 443 | 537 |

### 3.4 Why each sits where it does
- **The wind.** St Peters' prevailing wind blows from the south-west. `Data/Wind/Wind_StPeters.asset` gives `PrevailingDirectionDeg` 40, the bearing it blows *toward*.
  - Today's south and west coasts take it: the cliffs and the ledges. The plan leaves them alone.
  - The whole new coast is on the lee side.
- **Shelter along the lee falls from west to east:**
  - the lagoon is closed by the bar to the west and North Point to the east;
  - the two coves are half-closed by their rock points;
  - the cannery shoulder faces open water to the north-east, with nothing in front of it.
- **So the grain coarsens from west to east:** fine sand and mud where the water is still, a sand beach between the points, and shingle on the open shoulder.
- **The stream mouths** bring the fine sediment. The marsh sits where Barren Brook comes out, and the mud where Alder Run comes down.
- **The rock points** are small on purpose. Each one shuts a bay.

- **Lagoon Flats** — The lee of the bar and the island's north-west shoulder: the one place fine sand settles. Today's shelf, carried twice as far out and kept inside the clam holes' tide band (+−1.8 m), so a spring low bares a flat.
- **Lagoon Marsh** — Sheltered twice (the bar to the west, North Point to the east) and fed by the Barren Brook: a marsh platform laid in front of the old bluff, high marsh +1.5..+2.1, low marsh +0.6..+1.5, a 0.35 m scarp, then mud. Neap highs reach the low marsh; spring highs drown the lot.
- **North Point** — A rock horn is what makes the marsh a marsh: it shuts the lagoon from the east. Small on purpose.
- **North Cove** — Between two rock points the sand stays: the definite beach. Dune and marram on a low bluff, a steep upper face, a wide low-tide terrace that a spring low bares, eelgrass beyond.
- **Reef Point** — The reef the north cardinal already guards (mark.stpeters_reef_north stands 22 m off its tip): a benched platform zoned by the tide, Irish moss low, rockweed mid, bare ledge above.
- **Ginny's Cove** — Below Ginny's place, in the lee of the reef, where Alder Run comes down: a low bank, a cordgrass fringe, then mud out to the spring-low line. The cove the tide empties.
- **Cannery Shingle** — The open shoulder, with nothing between it and the water to the north-east: only cobbles stay. A storm berm at +2.6..+3.1, a steep face, a sandy low-tide terrace. Cannery Point is a cuspate foreland of the same shingle.

### 3.5 Paint-only sections: today's elevation, new paint
- **Bar-Head Flats** (`coast.stp_bar_head_flats`, sandy flats) — The bar root and the clam flats: every hole keeps its ground; only the paint changes (ripple, sand, runnels).
- **South Ledges** (`coast.stp_south_ledges`, ledge platform) — The cliff toes and the LedgeCliff benches (bench −1.65): weed zoned by the tide and talus at the foot, under walls that do not move.

---

## 4. Water

**Two kinds of water.**
- **Below +2.2 m, the sea floods by height alone.** Today's water already does this: one tide level per region, read against one height map (ADRs 0010 and 0014).
- **Above +2.2 m, nothing holds water today.** The ponds and the brooks' fresh reaches need PR 4's still-water seam.

### 4.1 The streams
| Stream | Id | Source → mouth | Length | Fresh reach | Tidal reach | Head of tide | Bed, source → mouth (m) | Width | Water depth | Deepest | Largest rise downstream |
|---|---|---|---:|---:|---:|---|---|---|---:|---:|---:|
| **Barren Brook** | `stream.stp_barren_brook` | Bog Pond → (−11.0, 99.0) | 76.4 m | 35.8 m | 40.6 m | (8.9, 65.1) | +5.09 → −1.39 | 1.0 → 2.2 m | 0.14 m | 0.33 m | +0.009 m |
| **Cove Brook** | `stream.stp_cove_brook` | a seep at (56.0, 55.5) → (50.0, 82.0) | 27.5 m | 8.2 m | 19.2 m | (52.7, 63.0) | +4.93 → −1.23 | 0.8 → 1.3 m | 0.10 m | 0.17 m | −0.003 m |
| **Alder Run** | `stream.stp_alder_run` | Fen Pool → (102.0, 108.0) | 53.3 m | 9.0 m | 44.3 m | (105.7, 63.9) | +5.01 → −2.11 | 1.0 → 2.0 m | 0.14 m | 0.21 m | +0.013 m |

**How a brook's bed is laid:**
- **The bed** is the lower of two things: the designed bed, or the ground less a 0.22 m incision. It never climbs downstream.
- **A pond's outlet** starts at the pond's sill, which is the surface less the brook's water depth. Its incision ramps in over 3 m.
  - So each pond meets its brook at one water level. The step between them is 0.000 m for both ponds.
- **A tidal side creek** ramps its incision in over 3 m from where it joins the brook.
- **The head of tide** is derived, not typed: it is where the bed drops below +2.2 m.
- **The largest rise downstream** is 1.3 cm, less than one 8-bit step (3.9 cm). It comes from the stepping-stone crossings and from bilinear sampling.
- **The deepest water** is 0.33 m, in Barren Brook's pool where the two marsh creeks join it.

### 4.2 The ponds, creeks and pans
| Pond | Id | Centre | Radii, rotation | Surface (m) | Bed (m) | Deepest | Water | Outlet | Step, pond → brook | Leaks |
|---|---|---|---|---:|---:|---:|---:|---|---:|---|
| **Bog Pond** | `pond.stp_bog_pond` | (46.0, 49.0) | 5.2 × 3.3 m, 12° | +5.30 | +4.65 | 0.65 m | 123.25 m² | Barren Brook at (41.50, 50.50) | 0.000 m | no |
| **Fen Pool** | `pond.stp_fen_pool` | (108.0, 51.0) | 6.5 × 4.0 m, −8° | +5.25 | +4.70 | 0.55 m | 138.25 m² | Alder Run at (106.93, 55.50) | 0.000 m | no |

**The leak check.** A flood fill from each pond's centre, below its surface + 0.05 m, never escapes except through the outlet.

| Creek | Id | Joins | Line, junction → head | Bed (m) | Width |
|---|---|---|---|---|---|
| **West Creek** | `creek.stp_marsh_west` | Barren Brook | (−5.0, 81.0) → (−10.0, 78.0) → (−15.0, 76.0) → (−19.0, 72.5) | +0.45 → +1.50 | 1.2 → 0.8 m |
| **East Creek** | `creek.stp_marsh_east` | Barren Brook | (−3.0, 83.0) → (3.0, 81.0) → (8.0, 79.0) → (12.0, 75.5) | +0.35 → +1.45 | 1.2 → 0.8 m |

| Pan | Centre | Radii, rotation | Water level | Water | Deepest | Level range (flat = 0) |
|---|---|---|---|---|---|---|
| `pan.stp_lagoon_marsh_a` | (−13.0, 68.0) | 2.4 × 1.6 m, 20° | +0.706 m | 4.00 m² | 0.109 m | 0.000 m |
| `pan.stp_lagoon_marsh_b` | (−6.0, 72.0) | 1.9 × 1.3 m, −15° | +1.063 m | 3.75 m² | 0.109 m | 0.000 m |
| `pan.stp_lagoon_marsh_c` | (6.0, 72.5) | 2.2 × 1.5 m, 5° | +1.537 m | 6.25 m² | 0.112 m | 0.000 m |
| `pan.stp_lagoon_marsh_d` | (−18.0, 80.5) | 1.6 × 1.1 m, 35° | −0.140 m | 3.50 m² | 0.112 m | 0.000 m |

**The salt pans** are dished 0.12 m into the marsh. Each holds flat water at the height of its lowest rim point.

### 4.3 Where each drains, and what it needs from the still-water seam
**Where each drains:**
- **Bog Pond** → Barren Brook → the marsh's main creek → Lagoon Marsh's mud → the lagoon, at (−11, 99).
- **Fen Pool** → Alder Run → a gully in the cove's bank → Ginny's Cove, at (102, 108).
- **Cove Brook** rises in a seep at (56, 55.5), cuts through North Cove's dune, and fans across the sand, reaching (50, 82).
- **West Creek and East Creek** → Barren Brook, inside the marsh.
- **The pans** drain nowhere. They hold rain and the last of the flood.

**What PR 4's seam must give:**
1. **A still level per pixel, not one level per pond.** A pond is flat, but a brook's fresh reach steps down its channel: the bed plus 0.10 to 0.14 m of water.
2. **The water level is the higher of the tide and the still level,** wherever the still level stands above the ground.
   - That is how a fresh reach meets the tide at its head of tide.
   - It is also how a salt pan keeps its water after the ebb.
3. **The still map is derived from the Defs** (ponds, streams, pans). It is recomputed and never saved (rule 5).
   - The water shader and `TidalWalkability` read the same map (rule 4, and the handoff's PR 4).
4. **Depths against the wade model** (`WadeDepth` 0.5 m):
   - Bog Pond is 0.65 m deep and Fen Pool 0.55 m. Their middles are deeper than `WadeDepth`, so the player swims there and wades round the edges.
   - The brooks are cut for 0.10 to 0.14 m of water. It stands deeper in three places: 0.17 m near the head of Cove Brook, 0.21 m just below Fen Pool's outlet, and 0.33 m in the confluence pool. All of it can be waded.
   - The pans are about 0.11 m deep.
5. **393 m² of still water in all.**

### 4.4 Flood maps at the five tide levels
In each panel, the water is tinted over the ground it floods, and the waterline is white. The captions give the dry ground.

![Flood maps at spring low, neap low, mean, neap high and spring high](st-peters-terrain-pass-9/flood.png)

| Tide | Level | Dry today | Dry in the plan | Change |
|---|---:|---:|---:|---:|
| spring low | −2.20 m | 53,042 m² | 62,471 m² | +9,429 m² |
| neap low | −0.99 m | 39,621 m² | 45,768 m² | +6,147 m² |
| mean | 0 m | 35,365 m² | 36,638 m² | +1,273 m² |
| neap high | +0.99 m | 30,182 m² | 30,511 m² | +329 m² |
| spring high | +2.20 m | 29,394 m² | 29,033 m² | −361 m² |

**The ground between spring low and spring high** grows from 23,648 m² today to 33,438 m² in the plan.
- The new flats, marsh mud and cove floors dry at low water: 9,429 m² more ground is dry at spring low.
- The marsh platform floods at the top of a spring tide: 361 m² less ground stays dry at spring high.
  - The platform stands at +1.5 to +2.1 m, with low marsh at +0.6 to +1.5.
  - So neap highs reach the low marsh, and spring highs drown almost all of it.

### 4.5 Where the flood cuts a path off, and for how long
- **The rule is the game's own.**
  - On foot, the player walks while the water is no deeper than `WadeDepth` (0.5 m in `Data/Config/GameConfig.asset`), and swims when it is deeper, up to `SwimLimit` (2.0 m). The code is `TidalExposure.IsWalkable` and `BandForDepth`.
  - So a path is **cut** while the tide stands more than 0.5 m over its lowest ground. Below that the player wades, slowed.
  - The prototype reads both values from `GameConfig.asset`, so a retune of `WadeDepth` moves these numbers with it.
- **The clock.** A tidal cycle is 12.42 game hours. A game hour is 75 s of real time. The spring–neap envelope is 14 days.
- **Which paths are cut:**
  - The **bar walk** (lowest ground −0.60 m, at (−234.2, 0.0)) is cut for 6.4 h of each spring cycle and 6.6 h of each neap cycle: 175.6 game h, or 219.5 real minutes, in a fortnight. It is wet underfoot for 7.3 h of each spring cycle.
  - The **reef walk** (lowest ground +0.03 m, at (70.0, 88.0)) is cut for 5.2 h of each spring cycle and 4.0 h of each neap cycle: 128.5 game h, or 160.7 real minutes, in a fortnight. It is wet underfoot for 6.2 h of each spring cycle.
  - **Ginny's track** (lowest ground +0.11 m, at (101.0, 69.5)) is cut for 5.1 h of each spring cycle and 3.6 h of each neap cycle: 122.0 game h, or 152.5 real minutes, in a fortnight. It is wet underfoot for 6.0 h of each spring cycle.
- **The bar walk is today's crossing** over the gut, and the plan leaves it as it is, so its times are today's.
- **Never cut:** the slip road, the bar-head road, the shore path and the barren path.
- The full numbers are in §5's table.

---

## 5. Paths
- **The material.** Every path is drawn in the kit's `path` material. That is a new slot, `_SplatF.r` (PR 2).
- **Ruts.** The cart tracks also carry the kit's ruts. The splat cannot hold them at 0.5 m per pixel, so they are strips:
  - PR 2 adds a rut strip beside the edge strips (`docs/art/rigs/terrain/edges.json`);
  - PR 5 lays the ruts along the cart tracks.
- **The two roads are today's lines,** repainted in `path`. The villagers' lanes follow them unchanged (§9).
- **Stepping stones.** Where a footpath crosses a brook's fresh reach, it steps over on `talus`:
  - Barren Brook near (15, 61.5);
  - Cove Brook near (55, 57);
  - Alder Run near (106, 59.5).
- **"The defined paths"** is read here as footpaths and tracks. The owner confirms it in decision 2.

| Path | Id | Kind | Width | Length | Lowest ground | Cut when the tide stands | Cut per spring cycle | Cut per neap cycle | Cut over a 14-day fortnight | Cut length at spring high | Wet feet per spring cycle |
|---|---|---|---:|---:|---|---|---:|---:|---|---:|---:|
| **Slip road** | `path.stp_slip_road` | cart track (today's) | 1.5 m | 188.7 m | +6.00 at (11.9, 5.5) | never | — | — | — | — | — |
| **Bar-head road** | `path.stp_bar_head_road` | cart track (today's) | 1.5 m | 48.6 m | +6.00 at (−6.7, 3.7) | never | — | — | — | — | — |
| **Bar walk** | `path.stp_bar_walk` | tidal crossing (today's) | 3.0 m | 255.0 m | −0.60 at (−234.2, 0.0) | above −0.10 m | 6.39 h | 6.61 h | 175.6 game h = 219.5 real min | 238.5 m | 7.30 h |
| **Shore path** | `path.stp_shore_path` | footpath | 1.0 m | 291.1 m | +2.68 at (149.8, 57.2) | never | — | — | — | — | — |
| **Barren path** | `path.stp_barren_path` | footpath | 0.9 m | 64.0 m | +5.65 at (45.4, 43.3) | never | — | — | — | — | — |
| **Reef walk** | `path.stp_reef_walk` | footpath | 0.9 m | 24.1 m | +0.03 at (70.0, 88.0) | above +0.53 m | 5.24 h | 3.96 h | 128.5 game h = 160.7 real min | 4.0 m | 6.15 h |
| **Ginny's track** | `path.stp_ginnys_track` | cart track | 1.6 m | 29.6 m | +0.11 at (101.0, 69.5) | above +0.61 m | 5.09 h | 3.57 h | 122.0 game h = 152.5 real min | 8.0 m | 6.01 h |

- **Slip road** — Today's: StPetersStarterSplat.VillageToSlipPath (StPetersStarterSplat.cs:215-372); NPC lanes follow it.
- **Bar-head road** — Today's: StPetersStarterSplat.VillageToBarHeadPath; NPC lanes follow it.
- **Bar walk** — Today's: the sandbar crest; the gut bed −0.6 (StPetersBuilder.cs:426-445).
- **Shore path** — The walk the owner's 'defined paths' most likely means: the whole new shore in one loop, bar head to cannery, over the Barren Brook on stepping stones just above its head of tide.
- **Barren path** — Post office to the reef across the barren, past the bog pond's south shore.
- **Reef walk** — Out along the point's spine to the platform: dry at every tide to the bluff foot, then only at low water.
- **Ginny's track** — A cart track from Ginny's place down the bank to the cove, where a skiff would be hauled up.

![The paths, coloured by the tide that cuts them: cut when the water stands deeper than WadeDepth over them](st-peters-terrain-pass-9/paths.png)

---

## 6. Where each example scene grows

### 6.1 How the plants are placed
- **Five biome zones,** each growing one example scene's recipe:
  - harbour shore (the `coast` scene);
  - salt marsh (`marsh`);
  - blueberry barren (`barren`);
  - roadside meadow (`meadow`);
  - alder swale (`swale`).
- **Density is each scene's own,** measured per ground material from the scene's JSON: plants per m² of that material in that scene, capped at 3.0.
  - So "the scenes' density" holds material by material.
  - A biome's average then depends on how much of each material it has.
  - Each scene's own figure (plants per m² over its whole window, trees apart) is in brackets in the table.
- **No plants grow on:**
  - lawn and path cells, or within 0.3 m of a path;
  - channels and still water;
  - protected objects' footprints;
  - ground outside every biome;
  - the woods floor, within 7 m of a trunk. Today's understorey stays there (decision 3).
- **Tide-zoned species keep to their kit band,** mapped onto St Peters' tide.
  - The mapping is kit metres = (E + 2.2) × 4 / 4.4.
  - The windows, in elevation E:
    - fringe −3.45 to −1.07;
    - mid −1.47 to +0.25;
    - low marsh −0.15 to +1.25;
    - high marsh +0.85 to +2.40;
    - upland above +2.0.
- **At most one plant per 0.5 m cell.**
  - Its species, stage (30% young), variant (0 to 3) and jitter are pure functions of the cell and the seed, 9109. Nothing is saved (rule 5).
  - The species comes from the scene's own mix for that material.

### 6.2 The biomes
| Biome | Id | Zone | Scene (its plants per m²) | Area | Plantable | Plants | Per m² | Per plantable m² | Plants, lighter option | Species mix (share of its plants) |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---|
| **Harbour shore** | `biome.stp_harbour_shore` | shore materials between −3.0 and +4.0 m outside the marsh and the mud cove | coast (0.639) | 37,341 m² | 7,603 m² | 5,995 | 0.161 | 0.789 | 5,864 | IrishMoss 48%, Eelgrass 22%, Bladderwrack 10%, KnottedWrack 7%, MarramGrass 6%, SeaLettuce 5%, BeachPea 1%, Bayberry 1%, SweetFern 1% |
| **Salt marsh** | `biome.stp_salt_marsh` | the Lagoon Marsh and Ginny's Cove fringe: marsh, silt, mud and sedge above −1.5 m | marsh (0.630) | 6,854 m² | 6,277 m² | 3,946 | 0.576 | 0.629 | 3,946 | Cordgrass 30%, Glasswort 19%, SeaLettuce 18%, Bladderwrack 12%, KnottedWrack 10%, SaltmeadowHay 4%, Eelgrass 2%, SoftRush 2%, BlackRush 1% |
| **Blueberry barren** | `biome.stp_blueberry_barren` | the north upland above +3.8 m, and a heath strip along the south cliff tops | barren (0.556) | 4,495 m² | 2,838 m² | 2,529 | 0.563 | 0.891 | 2,476 | LowbushBlueberry 29%, TussockSedge 9%, Rhodora 7%, Leatherleaf 7%, BlueFlag 6%, MeadowGrass 6%, CommonJuniper 6%, SoftRush 5%, SheepLaurel 5% |
| **Roadside meadow** | `biome.stp_roadside_meadow` | every other open plateau metre: the roads, the village edges, the south-west and east fields | meadow (0.582) | 13,116 m² | 9,289 m² | 5,511 | 0.420 | 0.593 | 5,191 | Lupin 19%, MeadowGrass 12%, Timothy 12%, OxeyeDaisy 11%, Buttercup 9%, QueenAnne 8%, Goldenrod 7%, Fireweed 6%, WildRose 4% |
| **Alder swale** | `biome.stp_alder_swale` | the hollow round the Fen Pool and Alder Run | swale (0.398) | 369 m² | 236 m² | 201 | 0.545 | 0.852 | 201 | BlueFlag 23%, Cattail 22%, SpeckledAlder 9%, Steeplebush 9%, SweetGale 6%, SoftRush 6%, Leatherleaf 4%, TussockSedge 4%, Meadowsweet 4% |

**The other ground:** woods floor 9,557 m² (no plants: today's understorey stays), outside every biome 323,468 m² (the sea, the village lawns, the protected ground).

**Reading the table:**
- **The harbour shore's average is low on purpose.** Most of it is bare intertidal.
  - The coast scene never plants ripple, shelf or shingle.
  - It puts marram on sand and glasswort on silt. Neither can grow on the low sand and silt of the new flats and coves, so those stay bare.
  - Where the scene's species can grow, the plan matches the scene ground by ground. Irish moss is the exception: it is capped at 3 plants per m².

| Ground | The coast scene's species on it | The coast scene (plants per m²) | This ground in the harbour shore | The plan, over all of it | Where those species can grow | The plan, there |
|---|---|---:|---:|---:|---:|---:|
| `irishmoss` | IrishMoss | 4.94 (capped at 3.0) | 942 m² | 3.04 | 942 m² | 3.04 |
| `talus` | Bladderwrack | 2.48 | 220 m² | 1.26 | 111 m² | 2.50 |
| `rockweed` | Bladderwrack, KnottedWrack, SeaLettuce | 1.61 | 737 m² | 1.21 | 564 m² | 1.57 |
| `marram` | Bayberry, BeachPea, MarramGrass, SweetFern | 1.19 | 480 m² | 0.96 | 384 m² | 1.20 |
| `grass` | Buttercup, CommonJuniper, Fireweed, Goldenrod, LowbushBlueberry, Lupin, MeadowGrass, OxeyeDaisy, QueenAnne, Raspberry, RedOsierDogwood, SheepLaurel, StaghornSumac, Timothy, WildRose | 0.94 | 24 m² | 0.94 | 23 m² | 1.01 |
| `silt` | Glasswort | 0.83 | 848 m² | 0.01 | 12 m² | 0.87 |
| `eelgrass` | Eelgrass | 0.29 | 4,360 m² | 0.30 | 4,360 m² | 0.30 |
| `foreshore` | SeaLettuce | 0.15 | 1,142 m² | 0.14 | 1,118 m² | 0.15 |
| `sand` | MarramGrass | 0.14 | 6,472 m² | 0.00 | 89 m² | 0.17 |

- **Marsh, barren and swale** land within the scenes' 0.40 to 0.64 over their plantable ground, or above it where their ground is richer than the scene's window.
- **The meadow's gross figure** is lowered by the lawns, roads and village it contains.

![The biome zones, with a speckle of the plant field and the preview windows](st-peters-terrain-pass-9/biomes.png)

### 6.3 The whole island
- **18,182 plants** (5,460 young), of 47 species: shore plants 9,967, grasses 1,994, flowers 3,806, shrubs 2,415.
- **The field is one byte per 0.5 m cell,** run-length coded: 56,044 B, or 74,728 characters of Base64 on one component. It is the `GrassField` pattern: stored once, derived at load, drawn in chunks.
- **The lighter option** halves the density within 6 m of a road and around the village. It gives 17,678 plants, 54,631 B and 72,844 characters, a saving of only 504 plants (2.8%).
- **Not placed by any scene:** SugarKelp.

**Every species, in the whole field:** IrishMoss 2,861 · Eelgrass 1,388 · Cordgrass 1,177 · Bladderwrack 1,074 · Lupin 1,074 · SeaLettuce 998 · MeadowGrass 868 · KnottedWrack 822 · Glasswort 749 · LowbushBlueberry 732 · Timothy 642 · OxeyeDaisy 628 · Buttercup 517 · Goldenrod 470 · QueenAnne 451 · Fireweed 399 · MarramGrass 339 · SoftRush 252 · TussockSedge 232 · CommonJuniper 231 · BlueFlag 224 · WildRose 211 · Raspberry 209 · Rhodora 178 · Leatherleaf 174 · SaltmeadowHay 163 · SweetFern 134 · SheepLaurel 120 · SweetGale 119 · BlackHuckleberry 108 · StaghornSumac 83 · Bayberry 81 · BeachPea 62 · WildRaisin 54 · BlackRush 51 · BeakedHazelnut 49 · Cattail 49 · Serviceberry 48 · LadySlipper 43 · RedElderberry 40 · SpeckledAlder 19 · Threesquare 19 · Steeplebush 18 · Meadowsweet 8 · RedOsierDogwood 7 · WinterberryHolly 4 · PussyWillow 3.

### 6.4 Renderers and batches at gameplay framing
**The method is `StPetersGroundCoverBudgetTests`' own:**
- windows of 16 × 9 m (`CameraFollow.OnFootWorldHeightMeters` = 9, at 16:9), one on each occupied bucket;
- renderers = the plants inside the window;
- batches = distinct pairs of (species, sorting order), at `SortingBands.OrdersPerMetre` = 4.

It assumes one texture per species, as PR 3's sheets would give. A species split across sheets adds batches.

|  | Renderers in the camera | Batches | Worst window (centre) |
|---|---:|---:|---|
| Today, as `StPetersGroundCoverBudgetTests` records it (the 2026-08-05 retune) | 395 | ≤ 114 | its worst screen |
| The test's budget (`OnScreenTuftBudget`, `OnScreenBatchBudget`) | 900 | 260 | — |
| The plan, the scenes' density everywhere | 319 | 130 | (120.0, −67.5) and (56.0, 49.5) |
| The plan, lighter in the village and along the roads | 309 | 130 | (−24.0, −49.5) and (56.0, 49.5) |
| Upper bound: the plan's worst plus today's worst on one screen, nothing retired | 714 | 244 | — |

**What this means:**
- **The plan's worst screen holds fewer renderers than today's, but more batches.**
  - It has 319 renderers against today's 395: about a third of the budget.
  - It has 130 batches against today's 114 at most: half the budget.
  - The old families stand only 6 more plants in that window today. So even if nothing retires, that screen stays inside both budgets.
- **The upper bound** assumes nothing retires and both worst screens fall on one spot. Even then it stays inside the budget, with 16 batches spare. That is the reason to retire the old families where the field covers (decision 3).

---

## 7. Previews on Node

**The seven windows** are each 520 × 450 px at 32 px per m, which is 16.25 × 14.06 m.
- The ground, heights, water and plants come from the plan's maps: the 0.5 m cells, with heights sampled bilinearly.
- They are handed to the kit as a scene in its own JSON form. The kit draws them through `terrainLight5` and `PlantRig5` at the afternoon preset, 14:00.

**Water in the windows.** The kit holds one sea level per scene. So the windows add the still water at its own level, as the game will (§12).

| Picture | Window | Biome | World coordinates (m) | Tide | Water | Sprites | Trees | Preset |
|---|---|---|---|---:|---|---:|---:|---|
| `preview-shore.png` | North Cove and Reef Point | `biome.stp_harbour_shore` | x 50.00 to 66.25, y 62.50 to 76.56 | 0.0 m | the sea | 112 | 0 | afternoon 14:00 |
| `preview-marsh.png` | Lagoon Marsh, mean tide | `biome.stp_salt_marsh` | x −9.00 to 7.25, y 75.50 to 89.56 | 0.0 m | the sea | 302 | 0 | afternoon 14:00 |
| `preview-barren.png` | Bog Pond in the barren | `biome.stp_blueberry_barren` | x 37.50 to 53.75, y 42.00 to 56.06 | — | Bog Pond | 389 | 0 | afternoon 14:00 |
| `preview-meadow.png` | The bar-head road | `biome.stp_roadside_meadow` | x −34.00 to −17.75, y −8.00 to 6.06 | — | none | 165 | 0 | afternoon 14:00 |
| `preview-swale.png` | Fen Pool and Alder Run | `biome.stp_alder_swale` | x 94.00 to 110.25, y 49.00 to 63.06 | — | Fen Pool | 163 | 1 | afternoon 14:00 |
| `preview-marsh-low.png` | Lagoon Marsh, spring low | `biome.stp_salt_marsh` | x −9.00 to 7.25, y 75.50 to 89.56 | −2.2 m | the sea | 302 | 0 | afternoon 14:00 |
| `preview-marsh-high.png` | Lagoon Marsh, spring high | `biome.stp_salt_marsh` | x −9.00 to 7.25, y 75.50 to 89.56 | +2.2 m | the sea | 302 | 0 | afternoon 14:00 |

| | |
|---|---|
| ![Harbour shore: North Cove and Reef Point](st-peters-terrain-pass-9/preview-shore.png) | ![Salt marsh at mean tide](st-peters-terrain-pass-9/preview-marsh.png) |
| ![Blueberry barren: Bog Pond](st-peters-terrain-pass-9/preview-barren.png) | ![Roadside meadow: the bar-head road](st-peters-terrain-pass-9/preview-meadow.png) |
| ![Alder swale: Fen Pool and Alder Run](st-peters-terrain-pass-9/preview-swale.png) | ![Salt marsh at spring low](st-peters-terrain-pass-9/preview-marsh-low.png) |
| ![Salt marsh at spring high](st-peters-terrain-pass-9/preview-marsh-high.png) | |

---

## 8. The plan is DATA

### 8.1 The Defs
One entity per file, each with a stable, append-only id (ADR 0003). The names are proposals; the fields are what the prototype uses. Appendix A holds every value.

| Def (proposed) | Ids | Count | Holds |
|---|---|---:|---|
| `RegionTerrainPlanDef` | `terrain_plan.st_peters` | 1 | The seed (9109); the reference line; the section feathers and warp; the kit's tide frame; the bed rules (incision 0.22 m, join ramp 3 m); the pan depth; the density cap; the woods-floor distance; the lists of the Defs below. |
| `CoastSectionDef` | `coast.stp_lagoon_flats`, `…_lagoon_marsh`, `…_north_point`, `…_north_cove`, `…_reef_point`, `…_ginnys_cove`, `…_cannery_shingle`; paint-only: `coast.stp_bar_head_flats`, `coast.stp_south_ledges` | 9 | Type; start on the reference line; the cross-shore profile; fill or cut; edge wobble; sea tail; rock edge. A paint-only section holds a polygon, or a bearing sector and a height limit, instead of a profile. |
| `CoastRecipeDef` | `coast_recipe.sand_beach`, `…shingle_cobble`, `…ledge_platform`, `…salt_marsh`, `…mud_flat`, `…sandy_flats` | 6 | The ground material by elevation. |
| `PondDef` | `pond.stp_bog_pond`, `pond.stp_fen_pool` | 2 | Centre, radii, rotation, surface, bed, basin, outlet. |
| `StreamDef` | `stream.stp_barren_brook`, `stream.stp_cove_brook`, `stream.stp_alder_run` | 3 | Source (a pond or a seep), control points, designed bed, widths, water depth, bank. |
| `TidalCreekDef` | `creek.stp_marsh_west`, `creek.stp_marsh_east` | 2 | The stream it joins, points, bed, widths. |
| `SaltPanDef` | `pan.stp_lagoon_marsh_a` to `_d` | 4 | Centre, radii, rotation. |
| `PathDef` | `path.stp_slip_road`, `…_bar_head_road`, `…_bar_walk`, `…_shore_path`, `…_barren_path`, `…_reef_walk`, `…_ginnys_track` | 7 | Kind (footpath, cart track or tidal crossing), width, points. The two roads point at `StPetersStarterSplat`'s own lines, so the village keeps one source. |
| `BiomeDef` | `biome.stp_harbour_shore`, `…_salt_marsh`, `…_blueberry_barren`, `…_roadside_meadow`, `…_alder_swale` | 5 | The zone (a rule or a polygon), the scene recipe it grows, and a density scale: 1.0 is the scene's own; the lighter option is 0.5 near roads. |
| `BiomeRecipeDef` | `biome_recipe.coast`, `.marsh`, `.barren`, `.meadow`, `.swale` | 5 | Per ground material: plants per m² and the species mix. The tool measures these from the drop's scene JSON and records the source file's sha256. They are re-measured, never hand-typed. |
| `PlantSpeciesDef` (PR 3) | The 16 shore plants keep their `shoreplant.*` ids; 32 new ids `plant.<snake_case>` (e.g. `plant.lowbush_blueberry`) | 48 | Rig key, family, habitat, tide zone, sheet spec. |

### 8.2 The editor tool that derives the maps
**Two parts:**
- **`TerrainPlanDerivation`** is a plain C# class: engine-light and testable in EditMode.
- **The tools-editor window** runs it and writes the maps.

**Its inputs:**
- the plan's Defs;
- the analytic terrain, as `StPetersBuilder.ConfigureTidalTerrain` (`StPetersBuilder.cs:2382`) sets it up. Outside the sections the ground is this model, bit for bit.
- today's committed splat, which is the paint outside the sections;
- the protected set, taken from the builders' constants and the scene's placed roots. A line scanner reads the scene; it never opens it.
- the biome recipes.

**Its steps,** in the prototype's order:
1. The coast sections.
2. The protection, which puts today's elevation back on the frozen mask.
3. The biomes.
4. The water: ponds first, then streams (with the bed and sill rules), creeks and pans.
5. The protection again.
6. The paint, in this order:
   - today's splat;
   - the paint-only sections;
   - the section recipes;
   - the barren's outcrops;
   - the swale;
   - the fresh-water edges;
   - the channels;
   - the new paths, with stepping stones where a footpath crosses a fresh reach;
   - the protected paint, which is kept;
   - the roads, repainted in `path`.
7. The plant field.

**Deterministic.**
- Every random choice is a hash of (cell, seed + a fixed salt). There is no global randomness (rule 5).
- The same inputs give the same bytes. The prototype ran twice from scratch, and every map and the field hashed the same: **IDENTICAL** (Appendix B).

**Never a hand-painted PNG without its source.**
- The maps are outputs. The Defs are the source. A manifest records the sha256 of every input and output.
- A guard fails when a committed map differs from a fresh derivation. So a hand stroke has to become data: either an edit to a Def, or a sparse override layer that the deriver applies last. The override layer comes later, and only if the owner wants it.

### 8.3 What it writes
| Map | Format | Size | Today |
|---|---|---:|---|
| Height, `StPetersSeabed_HeightTex` | R16, 1520 × 1040, −4 to +7 m: 0.000168 m per step (decision 11) | 0.2 to 0.4 MB | 8-bit, −4 to +6 m: 0.039 m per step |
| Splat A–E | RGBA8, 1520 × 1040 each | as today | the same five |
| Splat F | RGBA8: R = `path`; G, B and A spare | about 30 KB | new (PR 2) |
| Still level | R16 on the height map's scale; 0 = no still water | about 20 KB | new (PR 4) |
| Plant field | one byte per cell (0 = none, else 1 + the species index), run-length coded, as Base64 on one component | 56,044 B → 74,728 characters | new (PR 3) |
| Manifest | JSON: the seed, the tool version, and the sha256 of every input and output | under 5 KB | new |

---

## 9. What the prototype checked
The prototype (Appendix B) derives the maps from Appendix A's data and checks them against the committed maps and the scene, parsed by a script.

- **The frozen mask:** 937,122 pixels (234,280 m²) of protected ground. The largest change is 0.0002 m, which is float rounding.
- **Placed objects that the plan does not re-derive:** 546 items under 26 roots. The ground within 1 m of each moved by **0.0000 m**. By root: AuntGinny 1, CliffWalls 79, Dory 9, Enduro250AtTheStore 1, GeneralStoreCounter 1, GinnyPlot 26, IslandInhabitants 5, IslandShops 6, IslandVillage 42, Main Camera 1, NedsLetter 1, PassageToEastWater 1, PassageToNineMileCreek 1, PassageToWestWater 1, Player 2, StPetersArrival 1, StPetersCannery 1, StPetersDisembark 1, StPetersDockZone 1, StPetersEastWaterArrival 1, StPetersWharf 201, Tools 7, Trike200AtTheStore 1, UtilityQuadAtTheStore 1, WetBucketSpot 1, Yards 153.
- **The woods:** 259 trees, none moved; the ground within 3 m of every trunk moved at most 0.0000 m; the lowest is +6.00 m.
- **The cliff walls:** 79 walls, **0.0000 m** (they are in the list above).
- **The clam holes:** 66 holes; 9 stand on ground that moved (at most 0.543 m); all 66 stay inside the ±1.8 m band (today −1.76 to +1.66, plan −1.76 to +1.66).
- **The nav marks:**
  - All 13 keep at least 0.6 m of water at spring low.
  - The only change is under the reef north cardinal: its ground goes from −4.00 to −3.03 m, so its spring-low water goes from 1.80 to 0.83 m. The mark does not move.
  - Its probe still reads the hazard to the south and deep water to the north: north of the mark −3.28 m (today −4.00), south +1.74 m (today −1.03).
- **The fleet grounds, the entrance and the arrival route:** **0.0000 m**.
- **The villagers:** 11 lanes (the deck lane excepted): largest ground change 0.0 m, 0 newly below spring high; 24 of 26 stations are outdoors: largest change 0.0 m, 0 below spring high. This mirrors `StPetersRoutines.ValidateLanes` and `ValidateStations`.
- **The ponds:** the step to the brook is 0.000 m for both, and neither leaks. **The pans** hold flat water.
- **The streams:** none is perched, and none climbs by more than 0.013 m.
- **The fish bands** at mean tide, today → plan:

| Band at mean tide | Today | Plan | Change |
|---|---:|---:|---:|
| tidepool | 2,630 m² | 5,506 m² | +2,876 m² |
| shallows | 19,924 m² | 29,542 m² | +9,618 m² |
| inshore | 337,280 m² | 323,514 m² | −13,766 m² |

  The new shallows and tidepools are the flats, the marsh and the coves. Decision 9.
- **8-bit terraces.** In 8-bit, the waterline jumps this far per height step: Lagoon Flats 1.42 m, Lagoon Marsh 0.82 m, North Point 0.29 m, North Cove 0.51 m, Reef Point 0.25 m, Ginny's Cove 0.74 m, Cannery Shingle 0.55 m. The marsh platform covers 642 m², from +0.6 to +2.2 m, and sits on only 42 distinct 8-bit levels.
  - At R16 the step is 0.000168 m, and the terraces go.
- **Above the map's ceiling.** The barren's granite outcrops lift the ground to +6.65 m, and 285.5 m² lie above today's +6.0 ceiling. So the range goes to +7, or the outcrops are capped (decision 11).
- **Determinism:** two fresh derivations gave **IDENTICAL** maps and field.

---

## 10. The scene's layers, and the stop

**Layers that refresh in place, or by themselves:**

| Layer | Builder | Refreshes | What the reshaping does to it |
|---|---|---|---|
| **Height map** `StPetersSeabed_HeightTex.png` | `TerrainPaintTool` (menu at `App/Editor/TerrainPaintTool.cs:138`); `BakeStPetersSeabed` (`:1139`); `RebakeStPetersSeabedFromCommandLine` (`:1319`, which calls `:1343`) | in place | replaced by the derived R16 map (PR 5) |
| **The painted terrain's adoption** | `TerrainPaintTool.AdoptOnOpenScene` (`:1811`) swaps the scene's analytic `TidalTerrain` for a `PaintedTidalTerrain` | in place, but it saves the scene, so at St Peters it becomes hand-written YAML | **needed**, or the sim never sees the new coast (§0) |
| **Splat A–E and the lawns** | `StPetersStarterSplat`'s menu "Regenerate St Peters Starter Splat (replaces hand-painting)" (`App/Editor/StPetersStarterSplat.cs:767`; in batch, `PaintStarterSplatFromCommandLine` at `:790`); `StPetersLawns.PaintInto` (`:854`) | in place | replaced by the derived splat. The Regenerate menu would overwrite the plan, so PR 5 retires it or points it at the deriver. |
| **The splat quad** (`TerrainSplat`) | `TerrainSplatSurface` binds `_SplatA` to `_SplatE` | reads the maps at runtime | gains `_SplatF` (PR 2) |
| **The water** | `WaterSurface`, `HiddenHarboursWater`, `WaterOverlay`, `TidalFace` | read the painted height at runtime | follows the map; the still level is new (PR 4) |
| **The shore plants' tide response** | `ShorePlantTideView` reads `GameServices.TidalTerrain.ElevationAt` (`Art/ShorePlantTideView.cs:157-170`) | at runtime | follows whichever terrain the scene has: the analytic one today, so it sees the new coast only after the adoption |
| **Walkability, fleet lanes, traps and fish** | `TidalWalkability`; `AmbientFleetPresenter.PlanFleetDay` (`Boats/AmbientFleetPresenter.cs:405`); `TrapPlacement.DepthAt` (`Fishing/TrapPlacement.cs:52`); `GameServices.FishSchools` | at runtime | the same: each reads the scene's terrain |
| **Colliders** | none on the terrain. The three passage triggers (`BoxCollider2D` at `StPetersBuilder.cs:2165`, `:2188`, `:2217`) are fixed. | — | untouched |
| **Shrub sprites** | the `StPetersShrubBake` menu (`App/Editor/StPetersShrubBake.cs:73`) | in place | not derived from the ground: it makes sprites, not placements |

**Layers that change only through `Build()`:**

| Layer | Builder | What the reshaping does to it |
|---|---|---|
| 🔴 **IslandGrass** (the grass field) | `StPetersWoodsPlanter.Plant` (`StPetersBuilder.cs:1652`) → `StPetersGrassField.BakeField` (`App/Editor/StPetersWoodsPlanter.cs:489`) | its tufts are baked from today's ground, so grass would stand in the new marsh, streams and paths |
| 🔴 **ShorePlants** | `StPetersWoodsPlanter.Plant` (`:1652`) | see the table below |
| 🔴 **IslandShrubs** | same | see below |
| 🔴 **IslandFlowers** | same | see below |
| 🔴 **Shoreline** (the shore rocks) | `StPetersShorePainter.Paint` (`:1639`) | see below |
| 🔴 **ClamHoles** | `ScatterClamHoles` (`:1694`, defined at `:2620`) | see below; all holes stay inside the band |
| 🔴 **StPetersNavMarks** | `StPetersNavMarks.Place` (`:1852`) | no mark moves, but the reef north cardinal's recorded spring-low depth goes stale (1.80 → 0.83 m) |
| IslandWoods, IslandUnderstorey | `StPetersWoodsPlanter.Plant` (`:1652`) | untouched: none moved, because the woods are protected |
| CliffWalls | `StPetersCliffWalls.Build` (`:1552`); CLIFFS' lane | untouched |
| IslandInhabitants, IslandRoutines | `StPetersInhabitants.Place` (`:2099`), `StPetersRoutines.Place` (`:2125`) | untouched: they only check the ground, and every lane and station keeps its ground (§9) |
| StPetersArrivalOpening | `StPetersArrivalOpening.Place` (`:2142`) | untouched: fixed geometry on frozen ground |

**The Build()-only layers the reshaping touches,** item by item:

| Root | Items | On ground that moved | Newly between the tides | In fresh water | On a path |
|---|---:|---:|---:|---:|---:|
| ShorePlants | 384 | 243 | 2 | 3 | 2 |
| IslandShrubs | 116 | 21 | 1 | 3 | 3 |
| IslandFlowers | 33 | 7 | 1 | 2 | 0 |
| IslandUnderstorey | 32 | 0 | 0 | 0 | 0 |
| Shoreline | 21 | 11 | 0 | 0 | 0 |
| IslandWoods | 259 | 0 | 0 | 0 | 0 |
| ClamHoles | 66 | 9 | 0 | 0 | 0 |

🔴 **The stop.**
- **Seven layers** change only through `Build()`, and the reshaping touches them: IslandGrass, ShorePlants, IslandShrubs, IslandFlowers, Shoreline, ClamHoles and the reef north mark's record.
- **The handoff's rule** is that a layer that can change only through `Build()` stops PR 5, so this is reported here.
- **This PR does not work around it.**

**Two ways through (decision 10, ruled (a) on 2026-09-25):**
- **(a) Ruled:**
  - The four plant roots **retire** at St Peters (decision 3). The plant field replaces them; it is computed at load, so it needs no refresh.
  - **PR 4b** gives the other three layers an in-place refresh:
    - an editor step per layer recomputes that one root with the builder's own function on the new terrain (`ScatterClamHoles`, the shore painter's rock placement, `StPetersNavMarks`' records);
    - it writes the result as a YAML patch to that root alone, gated by named deletions, without ever opening and saving the scene.
- **(b) Not chosen:** PR 5 hand-writes every moved object in YAML. This works, but it has to be redone each time the plan is tuned.

---

## 11. The PR plan

The handoff's §4 is the starting point. This session corrects it in three ways:
- **R16 and the adoption of the painted terrain join PR 4.** The sim and the water must read one height source, as they must read one still-water source.
- **A PR 4b is added** (§10).
- **PR 5 retires the old families at St Peters only.**

**Every PR:** the MB figures are estimates. Tests are named with their production subject, as *TestClass (subject)*.

### Summary
| PR | Lane | Slot | Adds (estimate) | After |
|---|---|---|---|---|
| **2: the ground** | art-pipeline | one editor slot (the bake and the plate) | +11 to +12 MB: 189 new 256² maps. The albedo re-bake is about the size of today's 3.9 MB, so about ±0. | — (can run beside PR 4) |
| **3: the plants** | art-pipeline, with tools-editor | one (the bake) | +8 to +15 MB; the sheet spec decides it | the tree PR |
| **4: still water, and one height source** | lead-architect, with gameplay-systems | CI, plus one slot for a plate | about 0.1 MB | — |
| **4b: the layer refresh** | world-content, with tools-editor | CI only (the refresh runs in PR 5's slot) | about 0 | — |
| **5: St Peters** | world-content | one | +0.3 to +0.5 MB of maps; +0.08 MB of scene; about −2.2 MB when the four plant roots retire (decision 3; ShorePlants alone is 1.5 MB) | PRs 2, 3, 4 and 4b, and CLIFFS' Phase C |

### PR 2: the ground
**Files:**
- `docs/art/rigs/terrain/pass9/`: the drop's `terrainLight5.js`, `pxKit9.js`, `pxKit8.js`, `terrainLight4.js` and `lib/`, as bytes, with SHA256SUMS; and a bake driver that uses only the kit's public API.
- `docs/art/rigs/terrain/materials.json`: adds `path`.
- `docs/art/rigs/terrain/edges.json`: adds the rut strip.
- `Art/Terrain/`: per material, the contract's `_normal`, `_light` and `_detail` at the three ladder steps; and the albedo, re-baked from `terrainLight5`'s unlit.
- `Art/Editor/TerrainTexArrayBuilder.cs`: the new arrays.
- `Art/Shaders/HiddenHarboursTerrainSplat.shader`: `terrainLight5`'s relight in HLSL, `_SplatF`, and the ladder kept.
- `Code/Art/TerrainLight5.cs`: the line-for-line C# twin.
- `Code/Art/TerrainSplatSurface.cs`: binds `_SplatF`.
- `Code/App/Editor/TerrainSplatAssets.cs`, `TerrainSplatBrush.cs` and `TerrainPaintTool.cs`: slot 20, `path`.
- `Code/App/Editor/TerrainPass9Plate.cs`: lays the five example scenes out from their JSON into a scratch scene and commits no scene (`GrassTestBuilder` is the precedent). The plate is compared with the drop's PNGs.

**Tests added:**
- `TerrainLight5TwinTests` (TerrainLight5): the twin equals the rig at sampled G-buffer texels.
- `TerrainSplatPathSlotTests` (TerrainSplatSurface, TerrainSplatBrush): `_SplatF` is bound, and the brush paints slot 20.
- `TerrainKitPass9BytesTests` (TerrainTexArrayBuilder): the new arrays equal the rig's bake, byte for byte.

**Tests changed:**
- `TerrainKitAlbedoBytesTests` (TerrainTexArrayBuilder): the albedo now comes from `terrainLight5`'s unlit.
- `TerrainSplatBandPinTests` (StPetersShoreMap, TerrainTexArrayBuilder): the slot count goes from 20 to 21.
- `TerrainSplatBrushTests` (TerrainSplatBrush): the path slot.
- `StPetersSplatGroundTests` (TerrainSplatSurface): the new binding.
- `TerrainPxFlipPlatePlayTests` (TerrainSplatSurface): the plate reads the five splat ids, `_SplatA` to `_SplatE`. It gains `_SplatF`.

**Tests retired:** none.

**Supersedes** #858's never-chartered PRs 2 and 3.

### PR 3: the plants
**Files:**
- `docs/art/rigs/plants/`: the drop's `plantIsoRig5.js` and its shared `lib/` bytes, with SHA256SUMS; a catalog (48 keys, stages, seasons and the sheet spec); a bake driver.
- `Art/Editor/PlantRig5Baker.cs`, modelled on `TreeRigBaker` and `ShrubBaker`. Sheets are at most 2048 px and carry the contract's four maps. The tide ladder is made from the rig.
- `Art/Shaders/HiddenHarboursPlant.shader`: ONE shader on the trees' globals (`_WindWorld`, the snow float, the light law). It bends at each plant's root pivot, never by an atlas's `uv.y`, which is the trap `FlowerCatalogTests` guards.
- `PlantSpeciesDef` × 48, one file each. The 16 shore species keep their `shoreplant.*` ids.
- `BiomeRecipeDef` × 5, measured from the drop's scene JSON.
- `Code/Art/PlantField.cs`, `PlantFieldCodec.cs` and `PlantFieldScatter.cs`: the field, modelled on `GrassField` (stored once, derived at load, chunked). It keeps the footstep trails (`GrassFootstep`'s hooks) and the shore plants' tide response (`ShorePlantTideMath`, per instance).

**Tests added:**
- `PlantFieldCodecTests` (PlantFieldCodec): round trip and the byte format.
- `PlantFieldScatterTests` (PlantFieldScatter): deterministic; each material's density equals its recipe's.
- `PlantRig5CatalogTests` (the catalog against the rig): 48 keys, and sheets of at most 2048 px.
- `PlantSpeciesDefValidationTests` (PlantSpeciesDef): the ids are unique, stable and one per file.
- `PlantShaderGlobalsTests` (the plant shader): it reads `_WindWorld` and the snow float, and bends at the root.

**Tests changed:** `StPetersGroundCoverBudgetTests` (StPetersGrass, GrassLibraryCatalog → the plant field). Its premise moves from tufts to the field. It is named here, and needs the owner's word.

**Tests retired:** none. The old families keep running at Nine Mile Creek.

### PR 4: water above the tide, and one height source
**Files:**
- `Code/Core/Environment/IStillWater.cs`: the seam. It answers the still level at a point. Water = max(tide, still). It is registered in `GameServices`.
- `Code/World/PaintedStillWater.cs`: reads the still-level map.
- `docs/adr/0046-…md`: still water above the tide. #877 takes 0045, so check the number when this is written.
- `Player/TidalWalkability.cs`: reads the same water level.
- `HiddenHarboursWater`, `WaterOverlay` and `TidalFace`: sample the still map.
- `World/PaintedHeightMap`, `PaintedTidalTerrain` and `TerrainHeightPalette`: R16, over −4 to +7 m (decision 11).
- `App/Editor/TerrainPaintTool.cs`: its two height writers encode R8 today (`:1018` and `:1730`). Both write R16.
- The shaders decode with `lerp(_HeightMin, _HeightMax, r)` and take the range from the map (`TerrainSplatSurface.cs:321-322`, `WaterSurface.cs:1394-1395`), so their decode stays. PR 4 checks that both sample the R16 map at 16 bits.

**Tests added:**
- `StillWaterSeamTests` (the Core seam): max(tide, still), and deterministic.
- `TidalWalkabilityStillWaterTests` (TidalWalkability): the wading depth in a pond at every tide.
- `PaintedHeightMapR16Tests` (PaintedHeightMap): the decode is within one step.

**Tests changed:**
- `WaterFidelityPlateSweepTests` (WaterSurface): the still-level input.
- `SeabedHeightImportTests` (PaintedTidalTerrain, PaintedHeightMap): R16.

**Tests retired:** none.

### PR 4b: the in-place refresh (decision 10)
**Files:** `Code/App/Editor/StPetersLayerRefresh.cs`, with one step each for the shore rocks, the clam holes and the nav marks' records. Each writes a YAML patch to its own root, with named deletions. It adds nothing to the scene until PR 5 runs it.

**Tests added:** `StPetersLayerRefreshTests` (ScatterClamHoles, StPetersShorePainter, StPetersNavMarks): each refresh equals the builder's own function on the same terrain.

**Tests changed and retired:** none.

### PR 5: St Peters
**Files:**
- the plan's Defs under `Data/Terrain/StPetersPlan/` (Appendix A);
- `TerrainPlanDerivation` and its editor tool;
- `Data/Terrain/StPetersSeabed_HeightTex.png` as R16; `StPetersSplatA–F.png`; `StPetersStillWater.png`; the manifest;
- `Scenes/StPeters.unity`, **hand-written YAML only**, gated by named deletions:
  - the painted terrain adopted, with the analytic one disabled;
  - the plant field component;
  - the `_SplatF` and still-map bindings;
  - the retired roots removed;
  - the refreshed roots.
- the scene exporter re-run;
- `StPetersStarterSplat`'s Regenerate menu, retired or pointed at the deriver.

**Tests added:**
- `StPetersTerrainPlanDeterminismTests` (TerrainPlanDerivation): two derivations are identical, and the committed maps equal a fresh one.
- `StPetersTerrainPlanGuardTests` (the derived maps):
  - the frozen mask equals the analytic terrain within one R16 step;
  - the berths, wharf, buildings, roads, arrival route, cliffs and woods are unchanged;
  - the nav marks have at least 0.6 m at spring low;
  - the clams stay inside ±1.8 m;
  - the ponds do not leak, and the streams never climb.

**Tests changed** (their premise moves; each is named here and needs the owner's word):
- `StPetersCoastTests` (StPetersBuilder, CoastClass, CoastPlan);
- `StPetersShoreMapTests` (StPetersShoreMap, ShoreMaterial): the north shore's materials now come from the plan, not from elevation × sector;
- `StPetersShorelineRenderTests` (WaterSurface);
- `StPetersTerrainTests` (TidalExposure, TidalTerrain): the painted terrain replaces the analytic one, and the bar crest and gut must hold;
- `StPetersLayoutTests` (StPetersBuilder, TidalTerrain, TidalExposure): it measures the island's layout on the analytic terrain;
- `StPetersStarterSplatTests` (StPetersStarterSplat);
- `StPetersGrassFieldTests` (StPetersGrassField);
- `StPetersGreenOverTests` (StPetersGrass, StPetersShorePlants);
- `ClamScatterTests` (ScatterClamHoles): 9 holes move;
- `NavMarkPlacementTests` (NavMarkPlan, StPetersNavMarks): it builds the analytic terrain;
- `TerrainPaintAnalyticExportTests` (TerrainPaintTool, PaintedHeightMap): the export is no longer the analytic model;
- `FishDutyCycleAtTheLandingTests` (FishSchoolModel, FishSchoolMath): the bands shift;
- `SceneWeightGuardTests` (GrassField, the committed scenes): it must also catch a saved plant-field chunk, and St Peters gets lighter;
- `LitDecorCasterBudgetTests` (SpriteShadow, StPetersWoods, StPetersWoodsPlanter): shrubs and flowers leave.

**Re-run, with their premise held:**
- `StPetersWharfWalkabilityTests`, `StPetersDoryBerthTests`, `StPetersAlongsideBerthTests`, `StPetersEastBerthTests`, `StPetersVillageTests`;
- `StPetersCliffWallTests`, `StPetersWoodsTests`, `StPetersWoodlandZoneTests`;
- `ShorelineConvergenceTests`, `TidePacingInvariantTests`, `StPetersContentValidationTests`;
- `ArrivalOverRealTerrainPlayTests`, `HullsRideTheTidePlayTests`, `HullsRideTheTidePlatePlayTests`.

**Tests retired:** only the St Peters cases of the retired roots, named in PR 5 with their subjects. The families' own tests stay:
- `NineMileCreekFieldsTests` and `NineMileCreekShoreTests`;
- `FlowerCatalogTests`, `ShorePlantTideTableTests` and `ShorePlantContractTests`;
- `ShrubRigBakeTests` and `ShorePlantRigBakeTests`.

**Measured before and after:** the scene's size (6.18 MB at main), and the budget test's numbers.

---

## 12. Risks
1. **The Build()-only layers** (§10). Decision 10 is ruled: the four plant layers retire, and PR 4b refreshes the other three before PR 5.
2. **The analytic sim against the painted map.** The new coast reaches gameplay only when St Peters adopts the painted map.
   - In 8-bit, that moves the bar crest from +0.88 to +0.86 m, and with it the tide window that `TidePacingInvariantTests` pins.
   - The recommendation is to derive from the analytic model and store the height at R16, and to have a guard hold the frozen mask to one R16 step.
3. **8-bit terraces.** On the flats, the waterline would jump up to 1.42 m per height step. R16 removes this.
4. **The outcrops above +6.0 m** (285.5 m²). Ruled: the range widens to +7 (decision 11).
5. **The Lagoon Flats bare 65 m out at spring low.** Their flattest part, between −1.0 and −1.45 m, slopes at 1:72 to 1:80. There, the waterline of a spring flood advances at up to about 1 m per second of real time. That is the P5 teeth, and it should be felt in a playtest before it ships.
6. **The ruts need a strip layer.** The splat cannot hold them. PR 2 makes the strip kind, and PR 5 places the ruts.
7. **Batch headroom.** If nothing retires, and both worst screens fell on one spot, 16 batches would be spare.
8. **The harbour shore's average density** is low, because its ground is mostly bare intertidal (§6.2).
9. **The alder swale is small** (369 m²), because the woods round it are protected.
10. **The fish bands shift** (decision 9).
11. **The confluence pool.** Barren Brook's pool where the creeks join it is 0.33 m deep. That is fine to wade, but it is the deepest fresh water outside the ponds.
12. **The preview composite.** The kit's scenes hold one water level each, and the previews add the still water on top. PR 4's plate must show the same thing in the engine.
13. **The sedge pools** in the barren and swale previews are the kit's own look, not still water from the plan.
14. **48 species, not 47:** the handoff miscounted by one.
15. **The StarterSplat Regenerate menu** would overwrite the plan's splat (§10).
16. **St Peters cannot be rebuilt.** Every scene change is hand-written YAML.
17. **SugarKelp has no successor placement** in the drop's scenes. Ruled: it joins the fringe recipe (decision 3).

---

## 13. Decisions for the owner

🟢 **Ruled on 2026-09-25: the owner took every recommendation below.** Each *Recommendation* is now the ruling.
- Decision 8 is the owner's to act on: sending the Claude Design paste.
- Decision 12 goes to gameplay-systems.

1. **The plan.** The sections, points and coves, the streams, the ponds, the paths and the biome zones, judged on `plan-north.png`, `plan-island.png`, `flood.png` and the previews.
   - *Recommendation:* approve. Every shape is data (Appendix A), so any of them can be moved, renamed or re-typed later without code.
2. **"The defined paths".** Footpaths and tracks, as read here (§5), or something else.
   - *Recommendation:* as read.
3. **Density and succession:**
   - **(a) The scenes' density everywhere, or lighter in the village and along the roads.** Lighter saves only 504 plants (2.8%).
     - *Recommendation:* everywhere.
   - **(b) Which old families retire.**
     - *Recommendation:* retire the grass field, the shore plants, the shrubs and the flowers **at St Peters only**, where the field covers.
       - Keep the woods, the woods-floor understorey, and every family's code, Defs and tests for Nine Mile Creek.
       - Add SugarKelp to the fringe recipe, so the 90 kelp have a successor.
   - **(c) Whether the footstep trails carry over.**
     - *Recommendation:* yes.
4. **Ground snow:** now (`terrainLight5` has it), or with M2's winter wave.
   - *Recommendation:* with M2. The plants take the tree lane's snow float in PR 3 anyway.
5. **The foam:** `terrainLight5`'s shoreline foam, or the water's own. Only one of them draws it.
   - *Recommendation:* the water's own. It already follows the tide and the shore (ADR 0012).
6. **Streams above the tide:** real water (PR 4's seam), or a drawn brook material with no seam.
   - *Recommendation:* real water. A drawn brook cannot flood, cannot be waded, and cannot meet the tide at its head.
7. **The order:** one PR at a time, or two boxes in parallel.
   - *Recommendation:* PR 2 and PR 4 in parallel. They share one file, `TerrainPaintTool.cs`, in different places: PR 2 adds the path slot, PR 4 the R16 writers.
   - Then PR 3, after the tree PR; then 4b; then 5.
8. **Claude Design:** ask now for the checksums, checkers and sheet spec, or let PR 3 derive the sheet spec from the rig.
   - *Recommendation:* send the paste now.

**Four more that the plan raises:**

9. **The fish bands.** At mean tide: tidepool 2,630 → 5,506 m², shallows 19,924 → 29,542 m², inshore 337,280 → 323,514 m².
   - *Recommendation:* accept, and let gameplay-systems check the fish tables (`FishSchoolModel`, and the depth bands in `GameConfig`) in PR 5.
10. **The Build()-only layers:** (a) retire the plant roots, and a PR 4b refreshes the rest; or (b) hand-write the moved objects in PR 5.
    - *Recommendation:* (a).
11. **The height map's range and depth.**
    - *Recommendation:* R16, from −4 to +7 m, carried by PR 4. The alternative is to cap the outcrops at +6 m.
12. **The ambient fleet's grounds lie on land** today (§2.2). This is not part of the plan.
    - *Recommendation:* route it to gameplay-systems.

---

## Appendix A. The plan's data
This is every value the prototype derives from: the proposed Defs, in YAML. Coordinates are world metres (x east, y north). Elevations are metres about mean sea level (springs ±2.2, neaps ±0.99).

```yaml
# terrain_plan.st_peters (RegionTerrainPlanDef)
id: "terrain_plan.st_peters"
seed: 9109
kit_tide:
  kit_spring_range_m: 4.0
  st_peters_spring: 2.2
  rule: "kit_m = (E + 2.2) * 4.0 / 4.4"
shore_reference_line:
  - [-60.6, 20.4, "coast.stp_lagoon_flats"]
  - [-56.0, 28.7]
  - [-47.5, 39.1]
  - [-38.0, 47.5]
  - [-29.0, 53.7]
  - [-24.0, 64.0, "coast.stp_lagoon_marsh"]
  - [-18.0, 75.0]
  - [-9.0, 84.0]
  - [2.0, 88.5]
  - [12.0, 89.5]
  - [18.0, 91.0, "coast.stp_north_point"]
  - [23.0, 95.0]
  - [29.0, 93.0]
  - [32.0, 86.0]
  - [35.0, 79.0, "coast.stp_north_cove"]
  - [41.0, 73.0]
  - [48.0, 70.5]
  - [55.0, 71.5]
  - [61.0, 76.0]
  - [64.0, 80.0, "coast.stp_reef_point"]
  - [67.5, 86.0]
  - [72.5, 87.5]
  - [77.0, 81.0]
  - [81.0, 76.5]
  - [86.0, 74.0, "coast.stp_ginnys_cove"]
  - [95.0, 70.0]
  - [106.0, 68.0]
  - [117.0, 69.5]
  - [127.0, 72.0]
  - [135.0, 75.0, "coast.stp_cannery_shingle"]
  - [141.0, 76.5]
  - [147.0, 72.0]
  - [155.0, 64.0]
  - [163.0, 57.3]
  - [171.0, 52.5]
  - [178.0, 47.5]
  - [184.0, 42.4]
  - [189.0, 37.5]
section_feather_deg: 3.0
feather_growth_deg_per_m: 0.22
end_feather_deg: 6.0
warp_amp_wavelength_m: [1.4, 7.0]
incise_m: 0.22
join_ramp_m: 3.0
pan_depth_m: 0.12
density_cap_per_m2: 3.0
woods_floor_m: 7.0
pond_outlet_rule: "a pond-sourced stream starts at surface - depth (the sill); its incision ramps in over join_ramp_m"

# coast sections (CoastSectionDef, one file each)
- id: "coast.stp_lagoon_flats"
  name: "Lagoon Flats"
  type: "sandy_flats"
  mode: "fill"
  edge: [3.0, 18.0]
  knots:
    - [12, 2.4]
    - [6, 1.2]
    - [0, 0.0]
    - [-6, -0.7]
    - [-14, -1.0]
    - [-30, -1.2]
    - [-48, -1.45]
    - [-60, -1.8]
    - [-68, -2.4]
  sea_tail: 14
  why: "The lee of the bar and the island's north-west shoulder: the one place fine sand settles. Today's shelf, carried twice as far out and kept inside the clam holes' tide band (+-1.8 m), so a spring low bares a flat."
- id: "coast.stp_lagoon_marsh"
  name: "Lagoon Marsh"
  type: "salt_marsh"
  mode: "fill"
  edge: [3.5, 22.0]
  knots:
    - [44, 2.3]
    - [34, 2.05]
    - [24, 1.85]
    - [14, 1.6]
    - [9, 1.25]
    - [6, 0.95]
    - [4.5, 0.62]
    - [3, 0.28]
    - [0, 0.0]
    - [-12, -0.35]
    - [-25, -0.75]
    - [-40, -1.2]
    - [-52, -1.7]
    - [-60, -2.4]
  sea_tail: 12
  why: "Sheltered twice (the bar to the west, North Point to the east) and fed by the Barren Brook: a marsh platform laid in front of the old bluff, high marsh +1.5..+2.1, low marsh +0.6..+1.5, a 0.35 m scarp, then mud. Neap highs reach the low marsh; spring highs drown the lot."
- id: "coast.stp_north_point"
  name: "North Point"
  type: "ledge_platform"
  mode: "fill"
  edge: [2.0, 9.0]
  rock_edge: -15.0
  knots:
    - [14, 5.2]
    - [9, 4.4]
    - [6, 3.2]
    - [3.5, 2.2]
    - [1.5, 1.0]
    - [0, 0.1]
    - [-6, -0.3]
    - [-12, -0.8]
    - [-15, -1.7]
    - [-20, -2.4]
  sea_tail: 10
  why: "A rock horn is what makes the marsh a marsh: it shuts the lagoon from the east. Small on purpose."
- id: "coast.stp_north_cove"
  name: "North Cove"
  type: "sand_beach"
  mode: "cut"
  edge: [2.0, 11.0]
  knots:
    - [18, 6.0]
    - [14, 5.0]
    - [11, 3.6]
    - [8, 2.6]
    - [5, 1.5]
    - [0, 0.0]
    - [-8, -0.8]
    - [-18, -1.25]
    - [-30, -1.7]
    - [-40, -2.2]
    - [-48, -2.8]
  sea_tail: 12
  why: "Between two rock points the sand stays: the definite beach. Dune and marram on a low bluff, a steep upper face, a wide low-tide terrace that a spring low bares, eelgrass beyond."
- id: "coast.stp_reef_point"
  name: "Reef Point"
  type: "ledge_platform"
  mode: "fill"
  edge: [2.5, 8.0]
  rock_edge: -13.0
  knots:
    - [16, 5.6]
    - [10, 4.6]
    - [6, 3.0]
    - [3.5, 1.6]
    - [2, 0.6]
    - [0, 0.1]
    - [-6, -0.25]
    - [-10, -0.7]
    - [-12, -1.7]
    - [-16, -2.4]
    - [-19, -3.3]
  sea_tail: 8
  why: "The reef the north cardinal already guards (mark.stpeters_reef_north stands 22 m off its tip): a benched platform zoned by the tide, Irish moss low, rockweed mid, bare ledge above."
- id: "coast.stp_ginnys_cove"
  name: "Ginny's Cove"
  type: "mud_flat"
  mode: "cut"
  edge: [2.5, 16.0]
  knots:
    - [10, 6.0]
    - [7, 4.2]
    - [5, 2.6]
    - [4, 1.9]
    - [3, 1.4]
    - [1.5, 0.6]
    - [0, 0.25]
    - [-15, -0.2]
    - [-30, -0.7]
    - [-45, -1.2]
    - [-55, -1.6]
    - [-62, -2.4]
  sea_tail: 12
  why: "Below Ginny's place, in the lee of the reef, where Alder Run comes down: a low bank, a cordgrass fringe, then mud out to the spring-low line. The cove the tide empties."
- id: "coast.stp_cannery_shingle"
  name: "Cannery Shingle"
  type: "shingle_cobble"
  mode: "cut"
  edge: [1.5, 10.0]
  knots:
    - [16, 6.0]
    - [12, 4.4]
    - [9.5, 3.1]
    - [8, 2.8]
    - [6, 2.5]
    - [0, 0.0]
    - [-5, -1.1]
    - [-14, -1.6]
    - [-24, -2.1]
    - [-32, -2.8]
  sea_tail: 12
  why: "The open shoulder, with nothing between it and the water to the north-east: only cobbles stay. A storm berm at +2.6..+3.1, a steep face, a sandy low-tide terrace. Cannery Point is a cuspate foreland of the same shingle."

# paint-only sections (CoastSectionDef, elevation kept)
- id: "coast.stp_bar_head_flats"
  name: "Bar-Head Flats"
  type: "sandy_flats"
  poly: [[-115, -32], [-40, -32], [-30, 0], [-40, 34], [-115, 34]]
  why: "The bar root and the clam flats: every hole keeps its ground; only the paint changes (ripple, sand, runnels)."
- id: "coast.stp_south_ledges"
  name: "South Ledges"
  type: "ledge_platform"
  sector: [96.6, 252.0]
  below: 2.4
  why: "The cliff toes and the LedgeCliff benches (bench -1.65): weed zoned by the tide and talus at the foot, under walls that do not move."

# coast recipes (CoastRecipeDef coast_recipe.<type>): [top elevation m, material], first match from below
coast_recipe.sand_beach:
  - [-2.0, "eelgrass"]
  - [-1.0, "ripple"]
  - [0.6, "foreshore"]
  - [1.1, "shingle"]
  - [2.4, "sand"]
  - [3.8, "marram"]
  - [99, "grass"]
coast_recipe.shingle_cobble:
  - [-2.0, "eelgrass"]
  - [-1.2, "ripple"]
  - [-0.4, "foreshore"]
  - [2.9, "shingle"]
  - [3.6, "marram"]
  - [99, "grass"]
coast_recipe.ledge_platform:
  - [-2.3, "eelgrass"]
  - [-1.43, "irishmoss"]
  - [-0.05, "rockweed"]
  - [2.4, "ledge"]
  - [4.2, "shelf"]
  - [99, "grass"]
coast_recipe.salt_marsh: [[-1.3, "ripple"], [0.66, "silt"], [2.35, "marsh"], [2.8, "sedge"], [99, "grass"]]
coast_recipe.mud_flat:
  - [-1.8, "ripple"]
  - [-0.3, "mud"]
  - [0.6, "silt"]
  - [1.5, "marsh"]
  - [2.6, "shingle"]
  - [3.4, "sedge"]
  - [99, "grass"]
coast_recipe.sandy_flats: [[-1.6, "eelgrass"], [-0.5, "ripple"], [2.4, "sand"], [3.8, "marram"], [99, "grass"]]

# ponds (PondDef)
- id: "pond.stp_bog_pond"
  name: "Bog Pond"
  kind: "bog_pond"
  c: [46.0, 49.0]
  r: [5.2, 3.3]
  rot: 12
  surface: 5.3
  bed: 4.65
  basin: [11.0, 0.35]
  outlet: "stream.stp_barren_brook"
  why: "A kettle in the barren, peat-brown, leatherleaf and sedge to the edge. The kit's barren has one; so does ours."
- id: "pond.stp_fen_pool"
  name: "Fen Pool"
  kind: "fen_pool"
  c: [108.0, 51.0]
  r: [6.5, 4.0]
  rot: -8
  surface: 5.25
  bed: 4.7
  basin: [15.0, 0.45]
  outlet: "stream.stp_alder_run"
  why: "Where the swale holds its water between the woods and Ginny's Cove: a fen pool with flag and cattail."

# streams (StreamDef): z = designed bed at each point
- id: "stream.stp_barren_brook"
  name: "Barren Brook"
  source: "pond.stp_bog_pond"
  pts:
    - [41.5, 50.5]
    - [35.0, 53.0]
    - [27.0, 56.0]
    - [19.0, 59.5]
    - [12.0, 63.0]
    - [6.0, 67.5]
    - [1.0, 73.0]
    - [-4.0, 78.0]
    - [-7.0, 84.0]
    - [-9.0, 91.0]
    - [-11.0, 99.0]
  z: [5.2, 4.95, 4.5, 3.6, 2.6, 1.85, 1.2, 0.55, -0.1, -0.75, -1.35]
  w: [1.0, 1.0, 1.05, 1.1, 1.2, 1.3, 1.4, 1.5, 1.7, 1.9, 2.2]
  depth: 0.14
  bank: [0.3, 0.55]
  why: "The main brook: out of the bog pond, down through the barren in a shallow valley, into the marsh as its main creek, out across the mud to the lagoon."
- id: "stream.stp_cove_brook"
  name: "Cove Brook"
  source: "seep"
  pts: [[56.0, 55.5], [54.5, 59.5], [52.5, 63.5], [51.5, 67.0], [51.0, 71.0], [50.5, 76.0], [50.0, 82.0]]
  z: [5.55, 5.1, 3.8, 2.4, 0.9, -0.25, -1.1]
  w: [0.8, 0.85, 0.9, 1.0, 1.1, 1.2, 1.3]
  depth: 0.1
  bank: [0.4, 0.6]
  why: "A seep off the barren that cuts a runnel through North Cove's dune and fans across the sand."
- id: "stream.stp_alder_run"
  name: "Alder Run"
  source: "pond.stp_fen_pool"
  pts:
    - [107.0, 55.0]
    - [106.5, 58.5]
    - [106.0, 62.0]
    - [105.5, 65.5]
    - [105.0, 69.0]
    - [104.5, 76.0]
    - [104.0, 86.0]
    - [103.0, 97.0]
    - [102.0, 108.0]
  z: [5.15, 4.9, 3.7, 2.1, 0.85, 0.2, -0.3, -0.8, -1.4]
  w: [1.0, 1.0, 1.05, 1.1, 1.2, 1.3, 1.5, 1.7, 2.0]
  depth: 0.14
  bank: [0.3, 0.55]
  why: "The swale's drain: out of the fen pool, down a gully in the cove's bank, a channel across the mud."

# tidal creeks (TidalCreekDef)
- id: "creek.stp_marsh_west"
  name: "West Creek"
  joins: "stream.stp_barren_brook"
  pts: [[-5.0, 81.0], [-10.0, 78.0], [-15.0, 76.0], [-19.0, 72.5]]
  z: [0.45, 0.85, 1.2, 1.5]
  w: [1.2, 1.0, 0.9, 0.8]
- id: "creek.stp_marsh_east"
  name: "East Creek"
  joins: "stream.stp_barren_brook"
  pts: [[-3.0, 83.0], [3.0, 81.0], [8.0, 79.0], [12.0, 75.5]]
  z: [0.35, 0.8, 1.15, 1.45]
  w: [1.2, 1.0, 0.9, 0.8]

# salt pans (SaltPanDef pan.stp_lagoon_marsh_a..d)
- id: "pan.stp_lagoon_marsh_a"
  c: [-13.0, 68.0]
  r: [2.4, 1.6]
  rot: 20
- id: "pan.stp_lagoon_marsh_b"
  c: [-6.0, 72.0]
  r: [1.9, 1.3]
  rot: -15
- id: "pan.stp_lagoon_marsh_c"
  c: [6.0, 72.5]
  r: [2.2, 1.5]
  rot: 5
- id: "pan.stp_lagoon_marsh_d"
  c: [-18.0, 80.5]
  r: [1.6, 1.1]
  rot: 35

# paths (PathDef)
- id: "path.stp_slip_road"
  name: "Slip road"
  kind: "cart_track"
  existing: true
  width: 1.5
  src: "StPetersStarterSplat.VillageToSlipPath (StPetersStarterSplat.cs:215-372); NPC lanes follow it"
- id: "path.stp_bar_head_road"
  name: "Bar-head road"
  kind: "cart_track"
  existing: true
  width: 1.5
  src: "StPetersStarterSplat.VillageToBarHeadPath; NPC lanes follow it"
- id: "path.stp_bar_walk"
  name: "Bar walk"
  kind: "tidal_crossing"
  existing: true
  width: 3.0
  pts: [[-45.0, 0.0], [-140.0, 0.0], [-234.1, 0.0], [-300.0, 0.0]]
  src: "the sandbar crest; the gut bed -0.6 (StPetersBuilder.cs:426-445)"
- id: "path.stp_shore_path"
  name: "Shore path"
  kind: "footpath"
  width: 1.0
  pts:
    - [-36.0, 3.0]
    - [-40.0, 14.0]
    - [-40.5, 26.0]
    - [-36.5, 37.0]
    - [-30.0, 45.5]
    - [-23.0, 51.5]
    - [-14.0, 56.0]
    - [-4.0, 59.0]
    - [5.0, 61.0]
    - [12.0, 61.5]
    - [19.0, 61.5]
    - [27.0, 60.5]
    - [36.0, 58.5]
    - [45.0, 57.5]
    - [54.0, 57.0]
    - [62.0, 60.0]
    - [70.0, 64.0]
    - [79.0, 63.0]
    - [88.0, 60.5]
    - [97.0, 59.0]
    - [106.0, 59.5]
    - [116.0, 60.5]
    - [126.0, 62.5]
    - [135.0, 64.5]
    - [143.0, 62.0]
    - [151.0, 56.0]
    - [158.0, 47.0]
    - [161.0, 36.0]
    - [160.5, 24.0]
    - [158.5, 12.0]
    - [157.0, 5.0]
  why: "The walk the owner's 'defined paths' most likely means: the whole new shore in one loop, bar head to cannery, over the Barren Brook on stepping stones just above its head of tide."
- id: "path.stp_barren_path"
  name: "Barren path"
  kind: "footpath"
  width: 0.9
  pts:
    - [17.0, 47.0]
    - [25.0, 46.5]
    - [33.0, 44.5]
    - [41.0, 43.5]
    - [50.0, 43.5]
    - [58.0, 46.0]
    - [64.0, 51.5]
    - [68.0, 58.0]
    - [70.0, 64.0]
  why: "Post office to the reef across the barren, past the bog pond's south shore."
- id: "path.stp_reef_walk"
  name: "Reef walk"
  kind: "footpath"
  width: 0.9
  pts: [[70.0, 64.0], [70.5, 70.0], [70.0, 76.0], [70.5, 82.0], [70.0, 88.0]]
  why: "Out along the point's spine to the platform: dry at every tide to the bluff foot, then only at low water."
- id: "path.stp_ginnys_track"
  name: "Ginny's track"
  kind: "cart_track"
  width: 1.6
  pts: [[86.0, 45.0], [91.5, 50.0], [96.0, 55.5], [99.0, 61.0], [100.5, 65.0], [101.0, 69.5]]
  why: "A cart track from Ginny's place down the bank to the cove, where a skiff would be hauled up."

# biomes (BiomeDef)
- id: "biome.stp_harbour_shore"
  name: "Harbour shore"
  recipe: "coast"
  density: 0.639
  rule: "shore materials between -3.0 and +4.0 m outside the marsh and the mud cove"
- id: "biome.stp_salt_marsh"
  name: "Salt marsh"
  recipe: "marsh"
  density: 0.63
  rule: "the Lagoon Marsh and Ginny's Cove fringe: marsh, silt, mud and sedge above -1.5 m"
- id: "biome.stp_blueberry_barren"
  name: "Blueberry barren"
  recipe: "barren"
  density: 0.556
  poly:
    - [14, 50]
    - [24, 44.5]
    - [40, 41.5]
    - [58, 41.0]
    - [70, 46.0]
    - [90, 47.5]
    - [97, 53.0]
    - [100, 72.0]
    - [8, 76.0]
  rule: "the north upland above +3.8 m, and a heath strip along the south cliff tops"
- id: "biome.stp_roadside_meadow"
  name: "Roadside meadow"
  recipe: "meadow"
  density: 0.582
  rule: "every other open plateau metre: the roads, the village edges, the south-west and east fields"
- id: "biome.stp_alder_swale"
  name: "Alder swale"
  recipe: "swale"
  density: 0.398
  poly: [[95, 43.5], [121, 43.5], [123.5, 58.0], [114, 63.5], [101, 63.0], [94, 55.0]]
  rule: "the hollow round the Fen Pool and Alder Run"

# the node previews (520 x 450 at 32 px/m; x0, y0 = the south-west corner)
- key: "shore"
  biome: "biome.stp_harbour_shore"
  x0: 50.0
  y0: 62.5
  tide: 0.0
  water: "sea"
  title: "North Cove and Reef Point"
- key: "marsh"
  biome: "biome.stp_salt_marsh"
  x0: -9.0
  y0: 75.5
  tide: 0.0
  water: "sea"
  title: "Lagoon Marsh, mean tide"
- key: "barren"
  biome: "biome.stp_blueberry_barren"
  x0: 37.5
  y0: 42.0
  tide: null
  water: "pond.stp_bog_pond"
  title: "Bog Pond in the barren"
- key: "meadow"
  biome: "biome.stp_roadside_meadow"
  x0: -34.0
  y0: -8.0
  tide: null
  water: null
  title: "The bar-head road"
- key: "swale"
  biome: "biome.stp_alder_swale"
  x0: 94.0
  y0: 49.0
  tide: null
  water: "pond.stp_fen_pool"
  title: "Fen Pool and Alder Run"
- key: "marsh_low"
  biome: "biome.stp_salt_marsh"
  x0: -9.0
  y0: 75.5
  tide: -2.2
  water: "sea"
  title: "Lagoon Marsh, spring low"
- key: "marsh_high"
  biome: "biome.stp_salt_marsh"
  x0: -9.0
  y0: 75.5
  tide: 2.2
  water: "sea"
  title: "Lagoon Marsh, spring high"
```

## Appendix B. The prototype

**What it is.** It lives under `Evidence~/harness/`, and is not committed.
- A Python and Node port of the deriver described in §8, used to make every number and picture in this document.
- It reads the committed maps and the scene (with a line scanner, never the whole scene into memory).
- It drives the drop's kit only through its public API.

**Determinism:** two fresh derivations produced **IDENTICAL** outputs.

| Output | sha256 (first 16) |
|---|---|
| `maps.E` | `210e7821cdb40ec1` |
| `maps.zone` | `11342b97b1f92a33` |
| `maps.still` | `7e44fdc17e0b32be` |
| `maps.biome` | `a7acbf7c8746f7f1` |
| `maps.chan` | `81369d0becbb66a2` |
| `maps.tidal` | `b67e7514f101b2a7` |
| `field` | `29651ae75f2d456f` |

**Inputs:**

| Input | sha256 (first 16) |
|---|---|
| `Evidence~/harness/plan9_data.py` | `de5867d8ce58b2b1` |
| `Evidence~/harness/plan9_lib.py` | `b983cf394b14cad7` |
| `Evidence~/harness/plan9_run.py` | `97f323ad83d44bd7` |
| `Evidence~/harness/plan9_check.py` | `f5ca1a5624929e7f` |
| `Evidence~/harness/analytic.py` | `2955f7cf5b9f9158` |
| `Evidence~/harness/stp.py` | `943379feefadaac0` |
| `Assets/_Project/Data/Terrain/StPetersSeabed_HeightTex.png` | `dd4780eeeb0b81a9` |
| `Assets/_Project/Data/Terrain/StPetersSplatA.png` | `218a09b02a0898c1` |
| `Assets/_Project/Data/Terrain/StPetersSplatB.png` | `738e85fe1c8cf79c` |
| `Assets/_Project/Data/Terrain/StPetersSplatC.png` | `3e0c5aab043c6cd3` |
| `Assets/_Project/Data/Terrain/StPetersSplatD.png` | `940829e1390a6012` |
| `Assets/_Project/Data/Terrain/StPetersSplatE.png` | `24e51ee3377a25cd` |
| `Evidence~/scene/StPeters.json` | `f2409d83e2cea332` |
| `Evidence~/plan/features.json` | `13da0bc40bdc2b59` |
| `Evidence~/plan/kit_density.json` | `3d0c6301bd93ebbc` |
| `Evidence~/plan/species.json` | `4e1480e0b3da985f` |
| `Evidence~/drop/Export/pixel-terrain-pass-9/kit/plantIsoRig5.js` | `a80c485993a1a059` |

| Harness file (`Evidence~/harness/`, not committed) | Lines | sha256 (first 16) |
|---|---:|---|
| `analytic.py` | 127 | `2955f7cf5b9f9158` |
| `compare_drop.py` | 38 | `15a07d5a09f361cb` |
| `compare_materials.py` | 49 | `a5863b7e2e0915c5` |
| `hh9.js` | 110 | `339e8a44e0e4f67a` |
| `kit_density.js` | 21 | `8a31cab6fcaa8a57` |
| `materials.js` | 52 | `be4a705907c78671` |
| `overview.py` | 147 | `2ac47417519b71d1` |
| `plan9_check.py` | 290 | `f5ca1a5624929e7f` |
| `plan9_data.py` | 205 | `de5867d8ce58b2b1` |
| `plan9_lib.py` | 716 | `b983cf394b14cad7` |
| `plan9_pics.py` | 194 | `e5a1c4d6d6b25d13` |
| `plan9_preview.py` | 190 | `5915bc71eea2445d` |
| `plan9_run.py` | 147 | `97f323ad83d44bd7` |
| `preview.js` | 87 | `325cd7f5f73013bb` |
| `routines_check.py` | 101 | `d8b0fa3f9e4e4cb7` |
| `scene_parse.py` | 148 | `a0c51ad4fc466829` |
| `stp.py` | 151 | `943379feefadaac0` |
| `verify_drop.js` | 45 | `1c347aec9a64d6b0` |

**Pictures:**

| Picture | Pixels | Size |
|---|---|---:|
| `biomes.png` | 784 × 550 | 67,653 B |
| `change.png` | 1100 × 434 | 56,534 B |
| `flood.png` | 550 × 1036 | 174,425 B |
| `paths.png` | 1100 × 608 | 58,715 B |
| `plan-island.png` | 784 × 490 | 63,121 B |
| `plan-north.png` | 1100 × 516 | 61,127 B |
| `preview-barren.png` | 520 × 450 | 140,226 B |
| `preview-marsh-high.png` | 520 × 450 | 82,903 B |
| `preview-marsh-low.png` | 520 × 450 | 136,262 B |
| `preview-marsh.png` | 520 × 450 | 135,691 B |
| `preview-meadow.png` | 520 × 450 | 122,254 B |
| `preview-shore.png` | 520 × 450 | 98,973 B |
| `preview-swale.png` | 520 × 450 | 119,230 B |
| `today-island.png` | 1320 × 860 | 65,685 B |
| `today-region.png` | 1520 × 1040 | 58,290 B |
