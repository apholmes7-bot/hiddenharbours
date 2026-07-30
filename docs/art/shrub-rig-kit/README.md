# Acadian Shrub / Bush Rig — handoff

One file. No PNGs in this kit on purpose — every channel is generated from the geometry on call, so
the rig IS the asset.

    shrubIsoRig.js   the rig (plain JS, no imports, no build step)

Handoff page (in-project): `Shrub Rig.dc.html` — species-by-habitat picker, the eight-phase calendar,
continuous snow slider with the five bake steps, live channel switch, four-rule audit, the thicket
join test, per-species sheet gate, and per-sprite / per-sheet / contract downloads. The contract JSON
is serialised out of the live rig on that page — download it from there rather than hand-writing one.

20 species × 5 habitats × 8 phases × 5 snow steps × 4 variants × 3 growth stages.

## The gap this fills

The tree rig owns anything with a leader, `shorePlantRig` owns anything the tide reaches, the flower
rig owns anything herbaceous. Everything woody, waist-high and multi-stemmed — the alder swale, the
blueberry barren, the dogwood on the creek bank — was hand-drawn per season, which is why the same
alder had a summer sprite twice the width of its winter one.

Bayberry and Sweet Fern are deliberately NOT here: `shorePlantRig` owns both as dune/upland units,
and a species living in two rigs is how two silhouettes happen.

## Use the rig

    <script src="shrubIsoRig.js"></script>

    Shrubs.render(key, { variant, phase, frame, snow, stage })   // or { size: 0.2-1.3 }

`phase` is one of eight calendar stations, `snow` is metres of pack on the reference habitat. Returns,
per pixel:

    rgba                  composited sprite, binary alpha, no AA
    masks.front           N·L from the fixed upper-left key — ALL materials
    masks.rim             back rim — body and wood only, identically 0 on veil, fleck and bloom
    masks.depth           surface depth, normalised per sprite
    masks.veil            255 on twig-filament pixels — the no-rim / no-keyline flag
    masks.orn             ornament: 255 bloom · 170 fruit · 0 otherwise
    masks.snow            snow load 0-255
    nx, ny, nz            view-space surface normals (Float32Array)
    alpha, dist, thick, mat
    pivot {x, y}          ground contact — the root crown
    snowRow               row where the snow surface crosses, or null if outside the sprite
    buriedOut             true when the shrub is under the pack — do not draw it, draw a drift
    phase, snow           the resolved calendar and snow state
    report                measured rule compliance for this sprite

    Shrubs.packMask(res)    -> LIGHT RGBA: R key, G rim, B depth, A coverage
    Shrubs.packState(res)   -> CALENDAR RGBA: R veil, G ornament, B snow, A coverage
    Shrubs.normalView(res)  -> RGBA normal map
    Shrubs.massView(res)    -> rule-1 audit view (body / wood / edge / veil / fleck / bloom)
    Shrubs.leafView(res)    -> every leaf cell in its own flat colour
    Shrubs.phaseView(res)   -> what the calendar derived for this frame
    Shrubs.windowView(res)  -> the enclosed sky holes rule 4 counts
    Shrubs.sheetSpec(key, size, 'phase'|undefined) -> cell, pivot, sheet dims, 2048 check
    Shrubs.cellOf(sp, size) -> measured union cell for a species at a stage
    Shrubs.phaseOf / snowOf -> resolve either axis without rendering
    Shrubs.sheetAudit(size) -> per-species cell / worst ink / both sheets / cap headroom / MB
    Shrubs.contract(size)   -> the machine-readable import contract

## Phenology, not season

The tree rig has three seasons because a spruce has three. A shrub has eight states, and its identity
lives in the FLOWER and the FRUIT, not the leaf — at 32 px/m a blueberry leaf is 1 px and a rhodora
leaf is 1 px and they are the same 1 px. What tells them apart is that one is magenta in late May
before it has any leaves at all and the other is scarlet in October. Winterberry is invisible for
eleven months and then it is the loudest thing in the swamp.

    dormant Feb · catkin early Apr · bloom late May · leaf Jun
    green Jul · fruit Aug · turn early Oct · bare Nov

ONE table maps (phase, species) → leaf / bloom / fruit / catkin / veil / colour / bright stem, so they
cannot disagree. Two things fall out for free: `bloomFirst` species (rhodora, serviceberry) flower on
naked wood, and `holds` species (winterberry, juniper, rose, sumac) carry fruit through the dormant
frame — which is the whole reason to draw them.

## The snow line

A shrub is short enough for winter to ERASE it. 0.35 m of pack takes lowbush blueberry off the map and
does nothing at all to a 4 m alder. One number decides four things, the way the tide does in the shore
rig: how many rows are CLIPPED, whether a crust sits on the twigs, how much sway is left, and whether
the species is legal to place at that depth at all.

    depth = step.m × habitat.snowK        nominal SNOW_M 1.20 m
    steps: none 0 · dust 0.10 · pack 0.30 · deep 0.65 · drift 1.20
    barren 0.60 · woods 0.72 · edge 1.00 · bog 1.05 · swale 1.35

`buried >= 0.95` → do NOT draw the shrub, draw a drift. Snow CLIPS, it never floods — same ruling as
the rock rig's `awash` sheets. No sprite bakes ground; `snowRow` is reported so the scene can line its
drift tile up with the bake.

## The mechanisms this rig exists for

1. **A bare shrub is filaments, not a dither.** A leafless alder is two hundred twigs 1 px wide. Draw
   them as limbs and you get chicken wire; drop them and the shrub vanishes for four months. So bare
   twig volume is a MATERIAL — `M.VEIL` — resolved as STRANDS along a direction field that radiates
   from the root crown, laid at a fixed pitch, broken into finite runs, inked at the core. Same duty
   cycle as the ordered dither it replaced (`report.veilDuty`), but the pixels are CONNECTED and they
   run along the branch. Authored forking twigs sit under them at every stage. Veil is exempt from the
   mass floor, forbidden a rim, and forbidden a KEYLINE — an outline round every strand is mush.
2. **A shrub is a hollow basket.** A tree crown is a solid cap seen from below; a shrub is open and you
   look INTO it. Foliage masses sit on a shell over the stem bundle with the interior left to wood and
   veil, and `report.windows` counts the enclosed sky holes. A shrub with zero windows has failed no
   matter how nice its edge is. Mats are exempt by form — you do not look into a carpet.
3. **Grain is re-authored for the scale.** The tree rig's `broad` leaf cell is 8.4 × 5.6 px; a
   sheep-laurel canopy is 20 px across, so that cell would be a third of the plant. Every grain here is
   re-measured at shrub scale, 2.2–6.2 px, against the canopy it has to sit in.
4. **A berry inside the bush is a wasted pixel.** Fruit is FLECK — single pixels, never masses. A fleck
   is only emitted if it would be PRESENTED: frontmost at that pixel and within 4 px of the silhouette.
   Sites that fail are dropped, not moved (`report.presented / tried`). Three things make the survivors
   read as fruit rather than confetti: per-species BUNCHES (six to an elderberry cyme, one to a rose
   hip), UNEVEN RIPENESS across four ramps so a laggard stays green, and one key-side tip pixel plus one
   dark seat pixel — the difference between a sphere and a dot.
5. **Thickets tile.** Alder, leatherleaf, sweet gale, blueberry, juniper and raspberry ship as `wrap`
   units: every emitter is modulo the tile width, so a row butt-joins with no seam by construction.
   What is measured is `report.crossings` — how many rows actually cross the join. A tile that never
   crosses tiles perfectly and reads as a fence of separate bushes.

## The three rules, enforced in code

1. **Mass, with declared exemptions.** A 2 px rim must leave 6 px of interior, so no BODY mass leaves
   the emitter under a 5 px radius in any axis — clamped there, not trusted to twenty author sites
   (`report.floored`). VEIL, WOOD, EDGE, FLECK and BLOOM are linear or sub-pixel materials: exempt by
   declaration, and in exchange forbidden a rim. The exemption is a material, not a fudge, which is
   what makes it auditable.
2. **Silhouette.** Per-species tooth pitch and amplitude in PIXELS against the local radius, then a
   veil-aware de-speckle — the tree rig's pass would eat the entire veil. `report.spared` counts the
   pixels it declined to remove.
3. **Rim gate.** `rim × smoothstep(local thickness)`, hard zero on veil, fleck and bloom.
   `report.veilRimLeak` and `report.ornRimLeak` must both be 0.

All 480 states audit clean at full stage.

## Contract

`Shrubs.contract(size)` serialises the import contract out of the live rig, so it cannot drift from the
bake: the projection, six materials with their floor/rim/keyline rules, both bakes' per-channel
semantics, the calendar, the snow law, the habitat table, per-species sheet numbers, and six importer
asserts.

The one thing a consuming shader must be told rather than guess:

- `light.R` is key light for **all** materials.
- `light.G` is the back rim for **body and wood only**, identically 0 on veil, fleck and bloom.
- `state.R` is 255 on veil pixels — the no-rim / no-keyline flag, and the branch. Gate every read of
  `light.G` on it. Never infer veil-vs-mass from the sprite.

Other constants:

- **PPU 32** — 32 px = 1 m. `h` in the species table is TRUE standing world height.
- **Camera** — ADR-0006/0022: ¾ from the south at 40°, orthographic. Height ×0.766, ground depth
  ×0.643. Same as the boat, rock, shoreline, tree and shore-plant bakes.
- **Pivot** — ground contact at the root crown. Read it from `sheetSpec().pivot`.
- **Cells** — ONE union cell and ONE pivot per species per growth stage, unioned over 4 variants × 8
  phases. Snow is NOT in the union: it only ever clips, so it cannot change the cell. Runtime phase
  swaps are pivot-stable by construction.
- **Sway** — 4 frames, pinned at the pivot row AND at the snow line; amplitude scales with
  (1 − buried). Play 5–7 fps.
- **Sheets** — two axes per species: `variant × sway` and `phase × sway`. Both asserted ≤ 2048 px.
- **Stages** — young 0.42 · half 0.70 · full 1.00. Veil strength scales with stage (upright forms 37%
  → 100%, mats held to a 62% floor so a bare carpet still crosses its tile join); young plants sprout
  authored twigs in exchange for less filament.

Palette: cold ambient bounce + one warm key #e8b06a, as everywhere else in the world. Autumn is not
neon.

## Species

| species | latin | habitat | form | unit | height | notes |
|---|---|---|---|---|---|---|
| Lowbush Blueberry | Vaccinium angustifolium | barren | mat | mat | 0.38 m | wrap tile · blue fruit |
| Sheep Laurel | Kalmia angustifolia | barren | clump | clump | 0.81 m | evergreen · pink whorls |
| Rhodora | Rhododendron canadense | barren | plant | plant | 1.00 m | blooms on bare wood |
| Black Huckleberry | Gaylussacia baccata | barren | clump | clump | 0.91 m | near-black fruit |
| Common Juniper | Juniperus communis | barren | mat | mat | 0.63 m | evergreen · wrap · holds fruit |
| Leatherleaf | Chamaedaphne calyculata | bog | thicket | mat | 0.84 m | evergreen · wrap · bell racemes |
| Sweet Gale | Myrica gale | bog | thicket | mat | 1.06 m | wrap · catkins |
| Winterberry | Ilex verticillata | bog | plant | plant | 2.44 m | scarlet fruit held through dormant |
| Speckled Alder | Alnus incana rugosa | swale | thicket | mat | 4.00 m | wrap · catkins · the veil test case |
| Pussy Willow | Salix discolor | swale | clump | clump | 3.38 m | pale catkins |
| Red Osier Dogwood | Cornus sericea | swale | clump | clump | 2.19 m | red winter stems |
| Meadowsweet | Spiraea alba | swale | clump | clump | 1.25 m | white plume, blooms in green |
| Steeplebush | Spiraea tomentosa | swale | clump | clump | 1.06 m | pink steeple |
| Wild Raspberry | Rubus idaeus | edge | thicket | mat | 1.44 m | canes · wrap · bloom-purple winter stems |
| Wild Rose | Rosa virginiana | edge | clump | clump | 1.31 m | one big hip, held |
| Staghorn Sumac | Rhus typhina | edge | plant | plant | 3.44 m | fruit steeple · scarlet turn |
| Serviceberry | Amelanchier canadensis | edge | plant | plant | 3.69 m | blooms on bare wood |
| Beaked Hazelnut | Corylus cornuta | woods | clump | clump | 2.88 m | catkins |
| Wild Raisin | Viburnum nudum cassinoides | woods | plant | plant | 2.63 m | blue-black cymes |
| Red Elderberry | Sambucus racemosa | woods | plant | plant | 3.00 m | six berries to a cyme |

`unit` says what one sprite IS: **plant** = one individual · **clump** = a hummock of many stems ·
**mat** = one square metre of cover, tiling on its wrap axis.
