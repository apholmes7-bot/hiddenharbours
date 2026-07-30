# Acadian Tree Rig, PASS 2 — handoff

Two files. No PNGs in this kit on purpose — every channel is generated from the geometry on call, so
the rig IS the asset. Bake when you need sheets.

    treeIsoRig2.js   the rig (plain JS, no imports, no build step)
    _treeBake.js     headless batch harness

Handoff page (in-project): `Tree Rig Pass 2.dc.html` — species/growth/season/variant pickers, live
channel switch, leaf-cell and mass-id views, sway loop, family lineup, per-sprite + per-sheet
downloads. It loads pass 1 alongside pass 2 for side-by-side comparison; nothing here needs pass 1.

This supersedes `export/tree-rig-kit/` (pass 1). Same camera, same lights, same three rules — what
changed is what gets built and how the surface is quantised.

## What pass 2 changed

Pass 1 built real volume and lit it correctly, but every crown came out of one soft-ellipsoid cloud
with per-pixel value noise on top, so the family read as artichokes — no leaf shapes, no branch
shapes, no areas the eye can hold.

1. **Crowns are masses, not clouds.** A broadleaf crown is 5–9 leaf MASSES (a core plus its own ring
   of satellites — a floret), placed by arc length with deliberate gaps, plus a couple of hanging
   masses under the branch line. Every mass carries an id, so the shader draws a hard edge where two
   masses meet instead of blending them into one green wall.
2. **Leaf cells, not noise.** The foliage surface is partitioned into jittered, rotated, domain-warped
   Worley cells clipped to their clump — one leaf sprig each, shaded FLAT from its own mean with the
   lower-right border stepped down. Grain is per species (`GRAINS`): needles are long and thin, cedar
   sprays tall and narrow, oak leaves broad. The old per-pixel noise is gone.
3. **Serrated outline.** `blob()` adds a triangular tooth wave (pitch ~4.5 px, amplitude ~1 px) over
   the low-order lobing, and the de-speckle pass is 8-neighbour tooth-aware so it stops eating teeth.
4. **Branches you can see.** Broadleaves get primaries → secondaries aimed at each mass → twigs in
   the gaps; conifers keep a visible leader between tiers. Bark is banded in vertical striations
   (steps, not noise) and the root flare is 3 splayed buttresses with dark splits.

## Use the rig

    <script src="treeIsoRig2.js"></script>

    TreeRig2.render(key, { variant, season, frame, stage })   // or { size: 0.22-1.4 }

`globalThis.TreeRig2` — same surface as pass 1's `TreeRig`, so a consumer swaps one identifier.
Returns, per pixel:

    rgba                  composited sprite, binary alpha, no AA
    masks.front           N·L from the fixed upper-left key
    masks.rim             back rim, gated on local mass thickness
    masks.depth           surface depth, normalised per sprite
    nx, ny, nz            view-space surface normals (Float32Array)
    alpha, dist, thick, mat, mid, unit
    pivot {x, y}          the trunk foot — NOT the cell's bottom-centre
    clumps, limbs, florets
    report                measured rule compliance for this sprite

    TreeRig2.packMask(res)     -> RGBA: R = key, G = rim, B = depth, A = coverage
    TreeRig2.normalView(res)   -> RGBA normal map
    TreeRig2.massView(res)     -> rule-1 audit view
    TreeRig2.leafView(res)     -> every leaf cell in its own flat colour (mechanism B)
    TreeRig2.massIdView(res)   -> every floret / bough in its own flat colour (mechanism A)
    TreeRig2.sheetSpec(key, size) -> cell, pivot, sheet dims, 2048 check
    TreeRig2.cellOf(sp, size)  -> measured union cell for a species at a size

`report` carries: `pass`, `masses`, `failed`, `minBody`, `bodyRatio`, `despeckled`, `thinPct`,
`florets`, `leafCells`, `foliagePx`, `metres`, `underFloor`.

## Batch the set

    (0,eval)(await readFile('treeIsoRig2.js'));
    (0,eval)(await readFile('_treeBake.js'));
    await TREE_BAKE({ createCanvas, saveFile, log,
      stages: ['mature'], seasons: ['summer'], channels: ['albedo','mask','normal'] });

Defaults to all 10 species × 4 stages × 3 seasons × 3 channels. Each sheet is 4 variant cols × 4 sway
rows. Writes `Trees.json` beside them: per species+stage cell, pivot, true metres, sheet layout,
audit numbers, and the camera/rule constants.

The harness in this kit resolves `globalThis.TreeRig2 || globalThis.TreeRig` — a two-line change from
the copy in `export/tree-rig-kit/`, which was bound to pass 1. It uses nothing pass 2 doesn't expose.

## Contract

- **PPU 32** — 32 px = 1 m. `h` in the species table is TRUE world height.
- **Camera** — ADR-0006/0022: ¾ from the south at 40°, orthographic. Height ×0.766, ground depth
  ×0.643, depth key along the view axis. Same as the boat, rock, shoreline, shore-plant and shrub bakes.
- **Pivot** — the trunk foot, read it from `sheetSpec().pivot`. The near root flare projects below it,
  so it is not the cell's bottom row (same as the dory bake at 80,88 in a 160×156 cell).
- **Cells** — one union cell and one pivot per species per stage, unioned over 4 variants × summer and
  winter. A season swap at runtime is pivot-stable by construction.
- **Sway** — 4 frames, a per-scanline shear pinned at the pivot. Masks shear with it, frame for frame.
  Play 5–7 fps.
- **Sheets** — asserted ≤ 2048 px per axis; over that Unity silently downscales.
- **Seasons** — `summer` · `autumn` mixes 78% toward the species `fall` colour · `winter` strips the
  round, oval and larch forms to primaries/secondaries/twigs and puts snow on up-facing conifer
  normals. Autumn is not neon.

## The three rules, enforced in code

1. **Mass** — a 2 px rim must leave 6 px of interior, so nothing is emitted below a 5 px clump radius
   and a bough tier is 10 px tall. Rings that cannot carry clumps at full size drop their count
   instead of shrinking them. Leaf cells subdivide the SURFACE, never the mass — a 5 px clump is still
   a 5 px clump. `report.thinPct` measures the result per sprite.
2. **Silhouette** — outer masses sit on an arc-length-even ring, teeth at a fixed pitch, then a
   tooth-aware de-speckle strips every accidental hair and pinhole before shading. `report.despeckled`
   counts what it removed.
3. **Rim gate** — `rim × smoothstep(local mass thickness)`. A twig too thin to hold a rim never lights
   up.

Growth: `MIN_R` does not scale. A young tree carries FEWER masses, not smaller ones. Under ~1.06 m the
floor takes over and `report.underFloor` flags it — that is a shrub, not a tree, at this PPU. For the
things that live below that line, use `shrubIsoRig` (`export/shrub-rig-kit/`), which is authored for
the scale rather than shrunk into it.

Palette: cold ambient bounce #1d3b4a + one warm key #e8b06a. The only warm colour in the ramp is the
light itself.

## Species

| species | latin | form | height | crown |
|---|---|---|---|---|
| Red Spruce | Picea rubens | spire | 5.7 m | 13 tiers, drooping |
| Black Spruce | Picea mariana | spire | 5.6 m | 12 tiers, narrow and gappy |
| Balsam Fir | Abies balsamea | spire | 5.0 m | 12 tiers, stiff |
| E. White Pine | Pinus strobus | pine | 6.9 m | 6 wide tufted tiers |
| E. White Cedar | Thuja occidentalis | cedar | 4.75 m | 18 scale sprays |
| Tamarack | Larix laricina | larch | 5.25 m | gold in autumn, bare boughs in winter |
| White Birch | Betula papyrifera | oval | 5.7 m | white bark, gold fall |
| Red Maple | Acer rubrum | round | 5.6 m | 8 florets, scarlet fall |
| Red Oak | Quercus rubra | round | 5.3 m | 9 florets, widest crown |
| Trembling Aspen | Populus tremuloides | oval | 5.6 m | pale bark, the highest sway in the family |
