# Bounded wardrobe assembly proof — 13 September 2026

The same Fisher can wear the starter slate shirt, teal bib overalls and deck boots,
or independently selected ochre long sleeves, navy trousers and those boots. The live
cast viewer's wardrobe selector keeps rotation, animation and frame scrubbing. Both
looks were also rendered at eight headings at 64 px/m and 32 px/m; the metre scale is
unchanged. This is the CW01 authoring proof, not the production creator or shop.

The later cast finish pass tailors Fisher's source body before either outfit is
assembled. Both recipes use the same adjusted source sections and unchanged skeleton;
the source report includes `character-finish.js` and its authored per-preset tuning.
The broader cast finish does not make any other preset a compatible wardrobe fit.

Run `node docs/art/character-workbench/wardrobe-validate.cjs`, then
`node docs/art/character-workbench/build-viewer.cjs`. The first command regenerates
`wardrobe-catalog.json`, `wardrobe-measurements.json`, the four turntable PNGs and
`wardrobe-proof.html`. Generated files are local review output. The five individual
`wardrobe-garment-*.json` files, fit and recipes are the authored source. They use the
same PascalCase DTO fields as Core's `CharacterClothingSchema` and
`CharacterAppearanceRecipe`; the combined catalogue is a transport snapshot.

## Supported fit and sections

Only `fit.character_fisher_study_v1` / `skeleton.character_fisher_rig7_v1`, retaining
Fisher's exact adult body study, is supported. Both bone IDs/order and rest transforms
are checked. There are 45 skeleton bones; the complete assemblies reference 30 and
use at most two influences per corner. A reversed source bone array remaps identically
by stable ID. A missing bone or changed rest transform fails explicitly.

| Source section | Vertices | Purpose |
|---|---:|---|
| `fisher_study.shirt_short` | 372 | Shirt torso/collar and upper sleeves |
| `fisher_study.shirt_long` | 444 | Shirt plus fitted forearm sleeve geometry |
| `fisher_study.trousers` | 368 | Waist, pelvis, legs and repaired knee |
| `fisher_study.bib` | 232 | Bib, straps, buckles and pocket |
| `fisher_study.boots` | 288 | Boot shafts, cuffs, feet and soles |

The long sleeve stream expands the source forearm tube radially by 1.16 around its
actual bind axis, preserving endpoint weights. Its `identity.forearms` coverage tag
hides exactly 16 skin faces. The short shirt restores those faces. Other identity
geometry remains unchanged: full head, skin, hair, eyes, neck and hands. The measured
head vertex difference is exactly **0 m** through all 35 animations for both looks.

Shirts occupy `top`; trousers occupy `bottom`; the combined bib garment occupies
`bottom` and `outerwear`; boots occupy `footwear`. The fit declares `top`, `bottom`
and `footwear` mandatory because this source has no bare trunk, leg or foot variant.
Combining trousers and bibs fails rather than leaving obsolete geometry. The five
garments admit four complete combinations; all four are exercised. No child, elder,
skirt, apron, hat/hood or continuously changed body fit is promised.

Colourway bindings name the actual source materials, semantic roles and existing
production ramp IDs. The proof reads the rig's palette tables and compares them with
`options.json`; it preserves material metadata and deduplicates identical effective
materials. Garment changes never recolour the person's skin or hair. Prices and
`seller.leblancs` stock declarations are proposals, not active offers or purchases;
the three starter garment declarations do not confer the whole review catalogue.

## Measurements

| Assembly | Vertices | Triangles | Used geometry ramps | Conservative mesh bytes |
|---|---:|---:|---:|---:|
| Starter shirt + bibs + boots | 3,108 | 1,630 | 9 | 243,336 |
| Mixed long sleeves + trousers + boots | 2,876 | 1,514 | 8 | 225,240 |

Both remain below the production 16-ramp cap. The shared 308-frame, 45-bone animation
stream is **388,080 bytes**, counted once. Bindposes add **2,880 bytes** per assembly.
Mesh accounting follows the current baker's conservative formula: 72 bytes per corner
(position, normal, UV0 attributes and legacy weights), plus 12 bytes per triangle.
The actual index buffer can be 16-bit; the estimate deliberately uses the baker's
32-bit accounting. These are layout estimates, not a Unity profiler capture.

On this Node 24 Windows host, after 20 warmups and across 100 swaps, median cached
assembly times were **0.98 ms starter / 0.90 ms mixed** and P95 **1.39 / 1.09 ms**.
The generated report retains exact samples' summary and source hashes. Timings exclude
Unity objects, GPU upload, renderer setup and shader state. Production facial UV/state
rendering, deformation normals and gameplay contact remain CW02 measurements; the
review rasterizer's procedural facial colours are separate from the geometry ramp count.

The final no-Unity run passed **1,820 pose samples**, **32 raster checks**, **44
explicit rejection checks** and **four material inheritance checks**. The preview
preflights Core's catalogue/recipe rules before assembling geometry, including
schema versions, unique IDs, slots, fits, sections, coverage, colourways and material
bindings. A second garment cannot reuse an already selected section, even in a
different slot. Empty, omitted or null material binding lists inherit the source
material; partial lists override only the named materials. The inheritance checks
preserve the exact source ramp and shader metadata without changing identity.
Raster checks prove nonempty, unclipped static previews;
pose checks prove finite geometry within the declared envelope. They do not prove
absence of self-intersection or contact against boats, chairs, beds and tools.

## Source provenance

The fit's `SourceRigSha256` pins the unchanged original rig-7 snapshot, with LF-normalized
text hash `0dddf45345f43ddcc887d62dbf317ec506003203eb2341ba32d29b537808abed`.
The validator also checks it against the production source. The preview applies the
reviewed `boot-segment-fix.cjs` patch to that snapshot; the resulting effective source
hash is `cee659c8cfba1badadd879acb7ebcd662a8d5cc24c18ce238777bb52c0d9b60d`.
Both values and every contributing source hash are recorded in the generated report.
The production rig and baked skins remain unchanged. Porting the correction requires
the real Unity rebake, freshness checks and gameplay contact review in CW02.
