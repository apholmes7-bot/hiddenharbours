# Plant rig 5.1

Claude Design's plant rig 5.1 (`plantIsoRig5.js`, from kit `HH-plant-rig-5-1`, sha256 1c71a9b2eecc28b395da4ce2a932ab150c2f96ec82880774a9afc66824483833). The kit's `SHA256SUMS.txt` has sha256 04445d2c8f9e27f78fcbab63d3d3c2adb00588aca8cfe85b7cdacd73f8a9a7af.

The rig is byte-for-byte intact. It is standalone: its IIFE has no imports and reads no other rig global. Rig 5.1 includes OstrichFern and MarshMarigold as CD's pass 6 draws them, draws ten formerly dormant Fireweed and Goldenrod states, and throws an error naming an unknown key. The byte-exact `afPieces.js` is still loaded after the rig: its guarded plant block skips both existing species, installs its own pieces and changes none of the rig's cells. Its StationLight2 calls are deferred inside the fallen-alder and tin-cup drawing functions; the plant bake never calls those functions. No shared library copy is needed to bake plants. `SHA256SUMS.txt` pins this folder. `source-chain.json` pins the existing terrain tools used to derive the five provisional recipe tables. Those tools stay in `../terrain/pass9/`.

## Bake and data

`bakePlants.js` defines the catalog and the four-map atlas layout. `loadRig.cjs` loads and hash-checks the rig, then `afPieces.js`, then the dormant data and bake wrapper. `bake.cjs` uses Node 24 and only built-in modules. It verifies both source hashes, rejects unknown keys and checks every cell's dimensions and pivot against the catalog. Run `node docs/art/rigs/plants/bake.cjs <output-folder>` for a headless bake. The Unity menu `Hidden Harbours/Art/Bake Plant Rig 5` runs that driver, imports the maps, fills the new Defs and creates the plant material.

There are 50 species: 21 shrubs, 16 shore plants, 9 flowers and 4 grasses. Each has three stages, four seasons and four variants. The shore plants add five water depths over the root: -0.85, 0, 0.5, 1 and 2 metres. The rig's native sheet is a 4 by 4 wind loop. Gameplay bakes one calm pose; the shader bends each plant about its own pivot. The diagnostic contact sheets use loop frame 4 at wind 0.45, gust 0.2 and direction 1.

Each species occupies one page, at most 2048 on either side. Cells use height-sorted packing with a one-pixel clear border. Their dimensions and pivots can differ; the field consumes each cell's own rect and pivot. All four channels use the same packing:

- unlit: the rig's neutral albedo output, sRGB;
- normal: encoded view normal, linear;
- light: sky visibility, transmission and height, linear;
- detail: stamp index in RG and material kind in B, linear.

Maps import with Point sampling, no mipmaps, no compression and no rescaling. The baker sets TextureImporterType.Default and SpriteImportMode.None, then clears TextureImporter.spritesheet so the old Sprite slice rows do not remain. ArtImportPipeline is unchanged. The EditMode map guard checks all three settings; run it before the next import for a red result on the old metas, then after import for green. The normal basis carries the plant rig's north axis into the shared sun helper. Wind, snow, sun elevation and weather use existing globals. The scene overlay applies the day/night grade. No shared lighting include is edited. Four maps cannot reproduce the rig's voxel sun occlusion or palette ramp selection; an engine plate must assess that remaining difference.

Of 5,472 state cells, 5,448 render and 24 are explicitly DORMANT. `dormant-states.json` lists the six winter seedling states (Lupin, LadySlipper, Fireweed, QueenAnne, Goldenrod and BlueFlag) and all four variants. The bake wrapper returns null from frame, sheet, model and cellOf for these states without entering the rig; runtime cells carry dormancy and do not count as missing art. The pin test guards the exact list, all four wrapper paths and all 2,400 native rig cells: exactly 24 are empty and the other 2,376 draw. Read `empty: true` on model(), cellOf() or frame().mdl; frame().empty is undefined. Fireweed and Goldenrod now draw seedlings in spring, summer and autumn, and young and mature plants in spring. The rig remains intact.

`afPieces.js` (pass 6, sha256 9f46015cd644965c6c358d5c61715746379bc69aa55edb0696ff80a1aeae5fe8) stays pinned after the rig. Both species' 96 cells draw unchanged. `unresolved-species.json` has an empty list. Code 49 is `plant.ostrich_fern`; code 50 is `plant.marsh_marigold`. The alder fall key scene already places six of each species. Their maps stay byte-equal, and this intake adds no field-recipe weight or planting.

The rig 5.1 re-bake changes eight maps: Fireweed and Goldenrod in all four channels. The other 192 maps are byte-equal to the reproduced baseline. All 48 previously drawn cells in those two species keep their pixels in every channel, even where packing moves them. Fireweed's page changes from 249 by 205 to 249 by 274; Goldenrod's from 446 by 121 to 415 by 171. Both remain single pages. All 50 catalog keys, the 11 keys used by 26 key-scene plant pieces and the 50 Def keys are present in rig 5.1.

## Field contract

`IPlantFieldGround` is the Core seam for a region's ground sample, exclusions, water level and season. PR 3 supplies no region adapter and changes no scene. PR 5p supplies the St Peters adapter after its ground and set pieces settle. Existing plant systems and their content remain in service.

`species-ids.json` pins the byte codes. Codes 1 to 48 never reorder; the two additions use 49 and 50. The 16 shore ids retain `shoreplant.*`; the other 34 use `plant.*`. One entity has one Def. A cell stores its species in bits 0 to 5 and stage in bits 6 to 7; zero is bare and stage 3 is reserved. PLF1 records the decoded length followed by uint16 runs and byte values, then Base64. The plane is derived at load and is not saved.

Seed 9109, half-metre cells, 30 percent young plants, four variants and root jitter define the default scatter. Density and species weights live in the five recipe Defs. `GroundMetresPerY` defaults to 1/sin(40), matching the one-scene scatter's true-ground density conversion. It is a region setting, not a light setting. Chunks group by x bucket, sorting order, species and page; slow tide/season updates replace only changed groups. Footstep trails consume the existing eight-walker pool of 24 points. Tide states and submerged sorting use `ShorePlantTideMath`.

## Catalog notes: PROVISIONAL recipes

`recipe-seed.json` records PxKit9 recipe requests and raster material areas at 520 by 450, 32 pixels per metre. The five accepted placement JSON files used in the design plan were absent from the allowed sources. Requests total coast 151, barren 128, marsh 146, meadow 133 and swale 91; the plan accepted 146, 127, 144, 133 and 91. Requests include two SugarKelp on irishmoss, although the accepted coast scene had none. These weights satisfy the kelp succession slot but are not calibrated to the 90 old kelp.

The density is the requested count divided by material area, capped at 3. Species weights retain request counts and shore elevation bands filter eligible choices. These are diagnostic starting values. They are not an approved match to the HH-st-peters-one-scene-package-2026-10-07 planting. The five recipes ship as PROVISIONAL. PR 5p remeasures weights and zones after exact ground masks: within 10 percent of list density for materials with at least 20 listed plants and 10 square metres, and within 5 percentage points for species holding at least 5 percent of a frame. Rare and fixed rows are accounted for. Exact frame lists are the fallback only if five recipes cannot meet this gate without per-frame hacks. No runtime provisional flag is added.

## Validation and next phase

Headless checks cover codec round trips, deterministic scatter, statistical density and mix, catalog keys and bounds, stable Def ids, and shader-global/root wiring. New C# compiles against installed Unity modules without starting Unity. The .NET 8 runner covers engine-free cases; it is not a full Unity suite or a shader compiler.

Standalone V8 reproduces the committed Glasswort summer/full/v0 sheet: 131,200 bytes, zero differences. A plant-rig-5 Blueberry sample also matches Node byte-for-byte. Scratch holds the 48-species contact sheets, fifteen frame comparisons and decoded atlas checks. None of that scratch is shipped.

The first 48-species Unity bake and board-time lineup are complete; the later 50-species import uses each map kind's narrowest exact format. The earlier native texture allocation was 442.234 MiB across 192 maps, before the two additions. Phase A reproduces the 200-map baseline, then compares the rig 5.1 bake by map bytes and by the pixels of every previously drawn cell. Only Fireweed's and Goldenrod's maps and catalog entries change, plus the rig hash. The next granted slot must run the map guard, import the changed maps, refresh the two Defs, run the plant tests and plate, and measure the current native texture total and before/after page sizes. Compression and the dawn/sunset colour gap are outside this PR. The existing St Peters ground-cover budget test remains intact for PR 5p.
