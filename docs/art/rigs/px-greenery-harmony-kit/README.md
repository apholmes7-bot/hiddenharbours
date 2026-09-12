# Pixel kit + greenery + harmony — one drop

Everything in the pixel terrain pack, the greenery families it was designed against, and the four
harmony scenes, at **mirrored repo paths** — import is a pure copy. Plus two new gameplay sidecars
that did not exist before this drop.

**1,261 files** — 1,248 of them art, the rest the two sidecars, the ten rigs, this README and
the hash list. Nothing here was re-baked: every PNG is the byte the bakers last wrote.

```
Art/Textures/TerrainPx/            358 files   21 materials x 3 steps x 5 channels (+ _v2/_v3 on four)
Art/Textures/TerrainPx/Edges/      777 files   16 strip pairs x N S E W + 8 corners each, + Wrack
Art/Foliage/Shrubs/                5 files   5 species x 3 variants, 4 channels
Art/Foliage/Flowers/               38 files   FlowersPx sheet (4 channels) + the flower rig set, albedo
Art/Sprites/Grass/                 31 files   tufts, species, varieties + both manifests
Art/Sprites/Scatter/               10 files   Rocks + Stones, 4 channels each  (see "what is missing")
Art/Sprites/Shore/Drift/           5 files   4 drift-weed sheets + manifest
gallery/harmony/                   24 files   4 scenes x (4 relit hours + normal + height)
Art/gameplay/                      2 files    NEW - the sidecars
Art/*.js, scene/harmonyScenes.js   10 files   the rigs the sidecars are stamped against
```

## The two sidecars

Neither existed before. Both follow the house pattern: art numbers **read** from the shipped
manifests, gameplay numbers **declared** and labelled as such, every entry carrying its own
`provenance`, and a `_confirm` block for what is a position rather than a fact.

### `Art/gameplay/TerrainPx.gameplay.json`

Per material, for all 21: `traverse` (mode · speed_mul · footstep · grip · vehicle · hazard ·
tide_state), the `art` row it was reasoned from (relief, lit range, base, L, rank, fringe,
directional, face), and `paint` (`markLead` · `grain` · `bias`). The whole `blend` block —
the choose rule, `hKit`, `heightGain`, and the two frontier rules (`shadeMul` 0.78,
`litRimMul` 1.14) — travels verbatim from `TerrainPx.json`, so a paint tool reading the sidecar
never has to open a second file.

The ordering is the claim, not the absolute values: **path 1.08 > grass 1.00 > foreshore 0.95 >
sand 0.82 > shingle 0.68 > mud 0.55 > silt 0.42 > oysterreef 0.35.** Grip bottoms out at
**rockweed 0.18** — drained Ascophyllum on rock is the most slippery thing on this coast, and the
kit should let a player find that out. `bank` is `mode: face`: it is in the pack because it bakes
with the pack, not because anything walks on it.

### `Art/gameplay/Greenery.gameplay.json`

Six families. Per plant: collision (shape · radius · block · speed_mul), height, occluder flag,
hazard, footstep overlay, and the state axes.

- **shrubs_px** — 5 species. Alder is the only `solid`; wild rose carries `thorn`; blueberry is
  walk-through. Summer only: phenology is 8 phases in `shrubIsoRig.js` and is not in the sheet.
- **flowers_px / flowers_rig** — never block. The rig set ships albedo only; only the px sheet has channels.
- **grass** — 27 frames with `climb_px` and `stiffness` carried through, which is what the
  wind/footstep bend shader reads.
- **shore_plants** — 16 species x 5 tide steps x 3 seasons x 3 growth stages, with
  `covered_at_water_m` / `awash_at_water_m` computed off zone base + height. **The sheets are not
  in this drop** — the rig renders them (11.42 MB) exactly as in `export/shore-plant-rig-kit`.
- **drift_weed** — buoy, snag points and drag tail per variant, in water only.

## Light maps

Per the brief: **the channels already baked**, nothing new. Every terrain material and every px
sheet ships `_unlit` (authored bands, no directional light), `_mask` (R key N·L · G rim · B height ·
A coverage), `_normal` (R x · G y-up · B out) and — terrain only — `_blend` (R coverage order ·
G height on the shared ruler · B mark id). One key vector for all of it, in both sidecars under
`light`. `gallery/harmony/` carries each scene's normal and height buffers as **reference renders**,
not as engine-bound light maps; the compositor that made them is `scene/harmonyScenes.js`.

## Import settings

Wrap Repeat (Clamp on `Edges/`) · Filter Point · Compression None · sRGB on, **off** on `_normal`,
`_mask` and `_blend` · Mip Maps off at 1:1. 256 px = 8 m at 32 px/m.

## What is missing, on purpose

- **Cliff.** `Sandstone_*` was deleted out of the TerrainPx copy (72 files) and
  `Art/Tilesets/CliffPx` and the pixel cliff-face rig are not here.
- **Rock.** `Art/Sprites/Shore/RockPx` (the iso rock kit) is not here. But
  `Art/Sprites/Scatter/Rocks.png` — the pixel kit's own pass-three boulder sheet — **is**, because
  it is part of this kit and its README references it. Delete it if the exclusion was meant wider.
- **Trees**, harvest hooks, scene walkable masks, and any re-bake of the harmony buffers.

## Verify before trusting any of it

`SHA256SUMS.txt` carries both sidecars, the eight rigs they are stamped against, and the eight
manifests their art numbers were read from. If a rig hash has moved, the art moved: regenerate the
sidecar rather than editing a number in it.
