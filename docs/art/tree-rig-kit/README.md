# Acadian Tree Rig — handoff

Two files. Everything else is output.

    Art/treeIsoRig.js   the rig (plain JS, no imports, no build step)
    Art/_treeBake.js    headless batch harness

Handoff page (in-project): `Tree Rig.dc.html` — species/growth/season/variant pickers, live channel
switch, mass audit, sway loop, family lineup, per-sprite + per-sheet downloads.

## Use the rig

    <script src="treeIsoRig.js"></script>

    TreeRig.render(key, { variant, season, frame, stage })   // or { size: 0.0-1.4 }

Returns, per pixel:

    rgba                  composited sprite, binary alpha, no AA
    masks.front           N·L from the fixed upper-left key
    masks.rim             back rim, gated on local mass thickness
    masks.depth           surface depth, normalised per sprite
    nx, ny, nz            view-space surface normals (Float32Array)
    alpha, dist, thick, mat
    pivot {x, y}          the trunk foot — NOT the cell's bottom-centre
    report                measured rule compliance for this sprite

    TreeRig.packMask(res)     -> RGBA: R = key, G = rim, B = depth, A = coverage
    TreeRig.normalView(res)   -> RGBA normal map
    TreeRig.massView(res)     -> rule-1 audit view
    TreeRig.sheetSpec(key, size) -> cell, pivot, sheet dims, 2048 check
    TreeRig.cellOf(sp, size)  -> measured cell for a species at a size

Channels are computed from the geometry on every call. Change a species row in `SPECIES` and every
channel changes with it — nothing here is a stored bake.

## Batch the set

    (0,eval)(await readFile('Art/treeIsoRig.js'));
    (0,eval)(await readFile('Art/_treeBake.js'));
    await TREE_BAKE({ createCanvas, saveFile, log,
      stages: ['mature'], seasons: ['summer'], channels: ['albedo','mask','normal'] });

Defaults to all 10 species × 4 stages × 3 seasons × 3 channels. Each sheet is 4 variant cols × 4
sway rows. Writes `Trees.json` beside them: per species+stage cell, pivot, true metres, sheet
layout, audit numbers, and the camera/rule constants.

## Contract

- **PPU 32** — 32 px = 1 m. `h` in the species table is TRUE world height.
- **Camera** — ADR-0006/0022: ¾ from the south at 40°, orthographic. Height ×0.766, ground depth
  ×0.643, depth key along the view axis. Same as the boat, rock and shoreline bakes.
- **Pivot** — the trunk foot, read it from `sheetSpec().pivot`. The near root flare projects below
  it, so it is not the cell's bottom row (same as the dory bake at 80,88 in a 160×156 cell).
- **Sway** — 4 frames, a per-scanline shear pinned at the pivot. Masks shear with it, frame for
  frame. Play 5–7 fps.
- **Sheets** — asserted ≤ 2048 px per axis; over that Unity silently downscales.

## The three rules, enforced in code

1. **Mass** — a 2 px rim must leave 6 px of interior, so nothing is emitted below a 5 px clump
   radius and a bough tier is 10 px tall. Rings that cannot carry clumps at full size drop their
   count instead of shrinking them. `report.thinPct` measures the result per sprite.
2. **Silhouette** — outer lobes sit on an arc-length-even ring, and a de-speckle pass strips every
   1-px hair and pinhole before shading, so the rim traces a deliberate edge. `report.despeckled`
   counts what it removed.
3. **Rim gate** — `rim × smoothstep(local mass thickness)`. A twig too thin to hold a rim never
   lights up.

Growth: `MIN_R` does not scale. A young tree carries FEWER masses, not smaller ones. Under ~1.06 m
the floor takes over and `report.underFloor` flags it — that is a shrub, not a tree, at this PPU.

Palette: cold ambient bounce #1d3b4a + one warm key #e8b06a. The only warm colour in the ramp is the
light itself.
