# Plant rig 5 draft

Claude Design's plant rig 5 (`plantIsoRig5.js`, from its cliff-rock kit v6, sha256 a80c485993a1a05930849298d688572864deaa873d6aa62435135bfc3ff0059b).

The rig is byte-for-byte intact. It is standalone: its IIFE has no imports and reads no other rig global. No shared library copy is needed to bake plants. `SHA256SUMS.txt` pins this folder. `source-chain.json` pins the existing terrain tools used to derive the five provisional recipe tables. Those tools stay in `../terrain/pass9/`.

## Bake and data

`bakePlants.js` defines the catalog and the four-map atlas layout. `bake.cjs` uses Node 24 and only built-in modules. It verifies the rig hash, rejects unknown keys and checks every cell's dimensions and pivot against the catalog. Run `node docs/art/rigs/plants/bake.cjs <output-folder>` for a headless bake. The Unity menu `Hidden Harbours/Art/Bake Plant Rig 5` runs that driver, imports the maps, fills the new Defs and creates the plant material.

There are 48 species: 20 shrubs, 16 shore plants, 8 flowers and 4 grasses. Each has three stages, four seasons and four variants. The shore plants add five water depths over the root: -0.85, 0, 0.5, 1 and 2 metres. The rig's native sheet is a 4 by 4 wind loop. Gameplay bakes one calm pose; the shader bends each plant about its own pivot. The diagnostic contact sheets use loop frame 4 at wind 0.45, gust 0.2 and direction 1.

Each species occupies one page, at most 2048 on either side. Cells use height-sorted packing with a one-pixel clear border. Their dimensions and pivots can differ; the field consumes each cell's own rect and pivot. All four channels use the same packing:

- unlit: the rig's neutral albedo output, sRGB;
- normal: encoded view normal, linear;
- light: sky visibility, transmission and height, linear;
- detail: stamp index in RG and material kind in B, linear.

Maps import with Point sampling, no mipmaps, no compression and no rescaling. The normal basis carries the plant rig's north axis into the shared sun helper. Wind, snow, sun elevation and weather use existing globals. The scene overlay applies the day/night grade. No shared lighting include is edited. Four maps cannot reproduce the rig's voxel sun occlusion or palette ramp selection; an engine plate must assess that remaining difference.

Of 5,376 state cells, 5,312 render. The rig throws on 64 flower cells across variants: seedling winter for Lupin, LadySlipper, QueenAnne and BlueFlag; every seedling season plus young and mature spring for Fireweed and Goldenrod. The catalog lists these gaps. A missing cell draws nothing and increments `MissingCells`; it never changes species or season. Adoption must settle the gap policy first.

## Field contract

`IPlantFieldGround` is the Core seam for a region's ground sample, exclusions, water level and season. PR 3 supplies no region adapter and changes no scene. PR 5p supplies the St Peters adapter after its ground and set pieces settle. Existing plant systems and their content remain in service.

`species-ids.json` pins the byte codes. Codes 1 to 48 never reorder. The 16 shore ids retain `shoreplant.*`; the other 32 use `plant.*`. One entity has one Def. A cell stores its species in bits 0 to 5 and stage in bits 6 to 7; zero is bare and stage 3 is reserved. PLF1 records the decoded length followed by uint16 runs and byte values, then Base64. The plane is derived at load and is not saved.

Seed 9109, half-metre cells, 30 percent young plants, four variants and root jitter define the default scatter. Density and species weights live in the five recipe Defs. `GroundMetresPerY` defaults to 1/sin(40), matching the one-scene scatter's true-ground density conversion. It is a region setting, not a light setting. Chunks group by x bucket, sorting order, species and page; slow tide/season updates replace only changed groups. Footstep trails consume the existing eight-walker pool of 24 points. Tide states and submerged sorting use `ShorePlantTideMath`.

## Provisional recipes

`recipe-seed.json` records PxKit9 recipe requests and raster material areas at 520 by 450, 32 pixels per metre. The five accepted placement JSON files used in the design plan were absent from the allowed sources. Requests total coast 151, barren 128, marsh 146, meadow 133 and swale 91; the plan accepted 146, 127, 144, 133 and 91. Requests include two SugarKelp on irishmoss, although the accepted coast scene had none. These weights satisfy the kelp succession slot but are not calibrated to the 90 old kelp.

The density is the requested count divided by material area, capped at 3. Species weights retain request counts and shore elevation bands filter eligible choices. These are diagnostic starting values. They are not an approved match to the HH-st-peters-one-scene-package-2026-10-07 planting. Local density and mix calibration, exact terrain exclusions and the treatment of the 26 key-scene plant rows belong to PR 5p's data decision.

## Validation and next phase

Headless checks cover codec round trips, deterministic scatter, statistical density and mix, catalog keys and bounds, stable Def ids, and shader-global/root wiring. New C# compiles against installed Unity modules without starting Unity. The .NET 8 runner covers engine-free cases; it is not a full Unity suite or a shader compiler.

Standalone V8 reproduces the committed Glasswort summer/full/v0 sheet: 131,200 bytes, zero differences. A plant-rig-5 Blueberry sample also matches Node byte-for-byte. Scratch holds the 48-species contact sheets, fifteen frame comparisons and decoded atlas checks. None of that scratch is shipped.

Phase B still needs a cold Unity import, the bake, shader compilation, EditMode checks across plants and NMC, tide/footstep/wind checks and the species line-up at board times. Atlas memory and changed-chunk costs need an engine measurement. The existing St Peters ground-cover budget test remains intact pending its separate change decision.
