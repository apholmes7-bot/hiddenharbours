# Acadian Tree Rig, PASS 3 — handoff

Three files. The rig IS the asset — every channel is generated from the geometry on call; bake when
you need sheets.

    treeIsoRig3.js   the rig (plain JS, no imports, no build step) → globalThis.TreeRig3
    _treeBake.js     headless batch harness (resolves TreeRig3, falls back to TreeRig2 / TreeRig)
    Trees.json       the placement contract for mature/summer, as baked by this drop

Handoff page (in-project): `Tree Rig Pass 3.dc.html` — species / growth / season / variant pickers,
live channel switch (sprite · stamps · masses · wood · key · rim · depth · normals · mass audit),
keyline A/B, sway loop, to-scale family lineup against a 1.75 m fisher, per-sprite + per-sheet
downloads. It loads pass 2 alongside for comparison; nothing here needs pass 2.

This supersedes `export/tree-rig-kit-v2/`. Same camera, same lights, same three rules, same API.

## What pass 3 changed

**A. Skeleton first.** A broadleaf is built from its branch architecture — fork height, number of
primaries, the curve a limb takes to its target (oak: out nearly flat then up, kinked; maple:
straight ascending co-dominants; birch: steep then arching, two stems from one foot on even variants;
aspen: a long clean pole with short limbs high up) — and the florets hang off the limb tips. The
crown silhouette is a consequence of the wood, the wood is visible under the crown, and winter is the
SAME skeleton with twig fans. Conifers own their tier system per species: whorl count, taper, droop,
tip-up plumes (white pine), gaps (black spruce, tamarack), windswept asymmetry (pine), club top
(black spruce), flat top (mature pine), twin leaders (cedar), tufts strung along bare boughs
(tamarack), dead stubs under the live crown (spruces).

**B. Leaf stamps, not cells.** The foliage surface is covered by authored 4–9 px leaf-cluster
stencils — one grain per species (`GRAINS` / `STENCILS`: oak lobes, maple points, birch drops, aspen
coins, spruce combs, fir shelves, pine tuft-fans, cedar fans, tamarack rosettes) — scattered on a
rotated, warped lattice and painted lower-over-upper, so each stamp's authored TOP CONTOUR is what
shows. A stamp is shaded flat from its own mean; its down/right seam steps one band down, its
key-ward tip one up; pixels no stamp reaches are the dark between leaves.

**C. Edge shape per species.** The silhouette tooth wave has a profile (`EDGES.shape`): `spike`
(needles), `round` (oak, birch, aspen, tamarack), `fan` (cedar), `tri` (maple, fir).

**D. True heights × SCALE.** Every species carries `real` (mature height, m), `crown` (spread, m) and
`dbh` (trunk diameter, m). `SCALE = 0.6` maps them to the bake so the tallest (white pine, 27 m)
still fits a 4-row sheet under the 2048 cap. Relative scale is real: a white pine is 2.5× a black
spruce; an oak crown is 3× an aspen's. `report.metres` / `sheetSpec().metres` are BAKED metres;
`trueMetres` is the real height. To go taller, raise `SCALE` and re-bake — cells, pivots and sheets
re-measure themselves; `sheetSpec().fits` tells you when a sheet has to split.

**E. Ringless (ADR 0031).** `KEYLINE_DEFAULT = false`. `render(key, {outline:true})` is the live A/B,
never the shipping bake. `Trees.json.keyline` records the state the sheets were baked in.

Also: the cavity (AO) falloff is scaled to the sprite instead of a fixed 6.5 px; bark grain is per
species (furrow · plate · scale · shred · smooth · paper); sway amplitude scales with height.

## Use the rig

    <script src="treeIsoRig3.js"></script>

    TreeRig3.render(key, { variant, season, frame, stage | size, outline })

Returns, per pixel: `rgba` (binary alpha, no AA), `masks.front` (N·L, upper-left key), `masks.rim`
(back rim, thickness-gated), `masks.depth`, `nx/ny/nz`, `alpha`, `dist`, `thick`, `mat`, `mid`,
`unit` (stamp id, −1 = none), `pivot {x,y}` (the TRUNK FOOT — not the cell's bottom-centre),
`clumps`, `limbs`, `report`.

    TreeRig3.packMask(res)      -> RGBA: R = key, G = rim, B = depth, A = coverage
    TreeRig3.normalView(res)    -> RGBA normal map
    TreeRig3.massView(res)      -> rule-1 audit view
    TreeRig3.leafView(res)      -> every leaf stamp in its own flat colour
    TreeRig3.massIdView(res)    -> every floret / bough / limb in its own flat colour
    TreeRig3.woodView(res)      -> the skeleton, foliage ghosted
    TreeRig3.sheetSpec(key, size) -> cell, pivot, sheet dims, 2048 check, metres + trueMetres
    TreeRig3.cellOf(sp, size)

`report`: `pass`, `masses`, `failed`, `minBody`, `bodyRatio`, `despeckled`, `thinPct`, `florets`,
`stamps`, `foliagePx`, `metres`, `trueMetres`, `trunkPx`, `underFloor`. All 480 sprites pass.

## Batch the set

    (0,eval)(await readFile('treeIsoRig3.js'));
    (0,eval)(await readFile('_treeBake.js'));
    await TREE_BAKE({ createCanvas, saveFile, log,
      stages: ['mature'], seasons: ['summer'], channels: ['albedo','mask','normal'] });

Defaults to all 10 species × 4 stages × 3 seasons × 3 channels; each sheet is 4 variant cols × 4 sway
rows. `Trees.json` carries per species+stage cell, pivot, baked + true metres, sheet layout, audit
numbers, `scale`, `keyline`, and the camera/rule constants.

## Contract

- **PPU 32** — 32 px = 1 m of BAKED world. A species' `h` is `real × SCALE × 32`.
- **Camera** — ADR-0006/0022: ¾ from the south at 40°, orthographic. Height ×0.766, depth ×0.643.
- **Key** — upper-left, as every rig in this project shades. (The bible's top-of-frame ruling is a
  project-wide move and is flagged, not taken here.)
- **Pivot** — the trunk foot; read it from `sheetSpec().pivot`.
- **Cells** — one union cell and one pivot per species per stage over 4 variants × summer + winter.
- **Sway** — 4 frames, per-scanline shear pinned at the pivot, amplitude ∝ height. Play 5–7 fps.
- **Sheets** — asserted ≤ 2048 px per axis. Largest at SCALE 0.6: white pine mature 1076 × 1764.
- **Seasons** — `summer` · `autumn` (78% toward `fall`) · `winter` (bare skeleton + twig fans for the
  round/oval/larch forms; snow on up-facing conifer normals).
- **Keyline** — off. `{outline:true}` for the A/B only.

## The three rules, enforced in code

1. **Mass** — a 2 px rim leaves 6 px of interior; nothing under a 5 px clump radius (6 px on the
   spike-edged needle species). Rings drop count rather than shrink clumps. Stamps subdivide the
   surface, never the mass. `report.thinPct` / `report.failed` measure it; the audit follows
   connectivity through wood but holds only foliage-bearing components to the body rule.
2. **Silhouette** — arc-length-even masses, authored teeth in a species shape, tooth-aware
   de-speckle. `report.despeckled`.
3. **Rim gate** — `rim × smoothstep(local mass thickness)`.

## Species

| species | latin | form | true height | crown | baked | architecture |
|---|---|---|---|---|---|---|
| Red Spruce | Picea rubens | spire | 21 m | 6.0 m | 12.6 m | 17 drooping whorls, leader between tiers, 3 dead stubs |
| Black Spruce | Picea mariana | spire | 11 m | 2.8 m | 6.6 m | narrow, gappy, club top, 4 dead stubs |
| Balsam Fir | Abies balsamea | spire | 16 m | 4.6 m | 9.6 m | stiff symmetrical shelves, sharp spire |
| E. White Pine | Pinus strobus | pine | 27 m | 11 m | 16.2 m | 6 whorls of bare arms, up-swept plumes, windswept, flat top |
| E. White Cedar | Thuja occidentalis | cedar | 13 m | 4.2 m | 7.8 m | dense column of fans, twin leaders on even variants |
| Tamarack | Larix laricina | larch | 17 m | 5.2 m | 10.2 m | open crown, rosettes strung along bare boughs; bare in winter |
| White Birch | Betula papyrifera | oval | 17 m | 7.0 m | 10.2 m | steep-then-arching limbs, two stems on even variants, hanging skirt |
| Red Maple | Acer rubrum | round | 20 m | 9.0 m | 12 m | ascending co-dominant forks, oval crown |
| Red Oak | Quercus rubra | round | 22 m | 15 m | 13.2 m | 4 heavy kinked limbs, broadest crown, open underside |
| Trembling Aspen | Populus tremuloides | oval | 18 m | 5.0 m | 10.8 m | clean pole to 0.54 H, short limbs, narrow high crown |
