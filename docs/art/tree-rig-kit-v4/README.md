# Acadian Tree Rig, PASS 4.1 — weather-lit handoff, gameplay sidecars, snow and wind maps

    treeIsoRig4.js            the rig → globalThis.TreeRig4 (plain JS, no imports, no build step). Unchanged: a25cfba1…
    weatherSky.js             the sky → globalThis.WeatherSky. Unchanged since v4
    _treeGameplay.js          the sidecar writer → globalThis.TREE_GAMEPLAY. Unchanged
    treeMaps4.js              NEW · the snow map, the wind weights, the rest pose and the reference wind shader → globalThis.TreeMaps4
    gameplay/                 treeIsoRig4.<Species>.gameplay.json × 10, schema hidden-harbours/tree-gameplay@1
    maps/                     NEW · treeMaps4.json (shader constants for all 40 species × stages) + one species × stage baked
    Tree Rig Pass 4.dc.html   NEW in the kit · the page, opens from this folder (support.js + lib/)
    support.js · lib/         the page's runtime and its other scripts (pixel terrain, pass-3 rig for the A/B, hashing)
    checks/                   NEW · the checker scripts, the render script, and their output in checks/out/
    renders/                  NEW · 14 review renders
    SHA256SUMS.txt            every file in the kit, this README included (see Checks)

Supersedes `export/tree-rig-kit-v4/`. Sidecars land in `docs/art/rigs/gameplay/trees/`, the same subfolder as
`Art/gameplay/trees/`, so import is a pure copy. The rig, sky, writer and sidecars are byte for byte the
earlier 4.1 zip; everything added reads the rig and changes none of it.

## What changed since PASS 3, file by file

**`treeIsoRig3.js` → `treeIsoRig4.js`** (`TreeRig3` → `TreeRig4`; pass 3 ships in `lib/` for the page's A/B)
- Light is not baked. `frame()` returns a G-buffer (material, stamp-flat normal, sky visibility, depth, height,
  stamp and part ids) and `relight(frame, sky)` lights it from any `WeatherSky.at()`. Pass 3 lit every
  sprite once at the authored key; its `render()` survives as the pass-3 surface, lit at `REF_SKY`.
- Big form first: normals blend leaf → floret → crown envelope (ellipsoid for broadleaves, cone for
  conifers), so a crown has a lit shoulder and a shade side. Pass 3 lit each floret alone.
- Volumetric shade: opacity maps over the real clumps and limbs give sky visibility (5 directions), sun
  transmission and the cast shadow (`castShadow()`, levels canopy · partial · full). Pass 3 had none.
- Back light: thin foliage transmits and the only rim is a sun rim when the sun is behind. Pass 3's fixed
  warm rim is gone.
- Wind: a displacement field (trunk lean ∝ w² and sway ∝ h^1.8, limb sway and bob on a wave that crosses
  the crown downwind), 16 frames a loop, wood re-rasterised bent. Pass 3 slid scanlines, 4 frames.
- Flutter and shimmer: each leaf flutters 1 px on its own slots; deciduous leaves turn over to a paler
  underside. In 4.1 every leaf-bearing stencil is about half its pass-4 size (4–6 px broadleaf, 3–4 px aspen).
- Shapes: domed florets instead of rosettes, layered white-pine sprays, closed birch and aspen ovals,
  limbs curved through their elbows, warm-white birch bark; white pine 6 → 7 whorls; birch and aspen
  crowns re-proportioned.
- Species data: `sway` is replaced by `wind` [bend, limb sway, flutter, bob]; new `sigma` (foliage
  opacity), `trans` (translucency), `shimmer` and `under` (underside colour); colours retuned.
- Weather: cloud, rain (wet bark, glints), fog, snow cover on up-facing stamps and limb tops, wind.
- Pivot: pass 3 put pivot x at the middle of the unioned silhouette (`round(dx + (xl + xr) / 2)`), off
  the trunk on an asymmetric crown. 4.1 puts it on the trunk-foot column in every cell. Pivot y is the
  foot row in both; its value moves with the crown's top and with 4.1's 3 px of extra headroom.
- Cells are wider by the gale reach either side (`windReach()`), and the sheet layout changed: pass 3
  was 4 variants × 4 sway frames; 4.1 is one variant × 16 wind frames, 4 × 4. The table below has both.

**`Trees.json` → `gameplay/*.gameplay.json` + `maps/treeMaps4.json`.** Pass 3's placement contract
covered mature summer only. The sidecars cover every stage and season group per species, stamped with
the rig, sky and writer hashes; `treeMaps4.json` carries cell, pivot and the shader's wind constants
for all 40 species × stages.

**`_treeBake.js` → `TreeRig4.sheet()`, `TreeMaps4.sheet()`, `checks/render.js maps`.** Pass 3's batch
harness is not carried: a baked wind loop is one `TreeRig4.sheet()` call; a rest-pose sheet (four
variants side by side) is one `TreeMaps4.sheet()` call; `node checks/render.js maps <Species> <stage>`
writes a species × stage set to `maps/`.

**New: `weatherSky.js`** — the hour (keyframes blue hour → moon, a stage sun path), cloud, rain, fog, snow,
wind; `lightTile()` relights the pixel terrain under the same sky; `toHarmony()` feeds the compositor.

**New: `_treeGameplay.js` + `gameplay/`** — the collider (the bole only), canopy, shade, wind, shadow and
sort data per species; see Gameplay sidecars.

**New: `treeMaps4.js` + `maps/`** — see The sheets each season costs, Snow and Wind below.

**`Tree Rig Pass 3.dc.html` → `Tree Rig Pass 4.dc.html`.** Weather presets and sliders; the tree on the
pixel terrain with its shadow, relit by the same sky; every light map as a channel; the pass-3 A/B; the
family under the current sky; downloads for the frame, light maps, wind sheet and gameplay sidecar. New in
this drop: SHADER (the wind drawn from the rest pose and weights), SNOW MAP (read against the snow slider),
WIND WEIGHTS, and a download of the rest pose's seven maps. The copy here loads its scripts from this folder.

### Cells, pivots and sheets, pass 3 → 4.1

Pixels. Pivot is x, y in the cell. Pass 3 sheet = 4 variants × 4 sway frames; 4.1 sheet = one variant ×
16 wind frames. Every 4.1 sheet fits the 2048 cap and every 4.1 pivot is on the trunk-foot column
(`checks/out/cells.txt` recomputes this table from both rigs and matches it row for row).

| Species | Stage | Cell 3 → 4.1 | Pivot 3 → 4.1 | Sheet 3 → 4.1 |
|---|---|---|---|---|
| Red Spruce | sapling | 48×96 → 76×99 | 23, 79 → 37, 82 | 192×384 → 304×396 |
| Red Spruce | young | 67×158 → 113×161 | 33, 149 → 55, 152 | 268×632 → 452×644 |
| Red Spruce | pole | 105×233 → 169×236 | 52, 225 → 83, 228 | 420×932 → 676×944 |
| Red Spruce | mature | 156×329 → 244×332 | 77, 318 → 122, 321 | 624×1316 → 976×1328 |
| Black Spruce | sapling | 45×65 → 67×68 | 22, 47 → 33, 50 | 180×260 → 268×272 |
| Black Spruce | young | 50×93 → 82×96 | 24, 84 → 40, 87 | 200×372 → 328×384 |
| Black Spruce | pole | 58×130 → 102×133 | 28, 123 → 51, 126 | 232×520 → 408×532 |
| Black Spruce | mature | 73×179 → 129×182 | 36, 171 → 64, 174 | 292×716 → 516×728 |
| Balsam Fir | sapling | 42×75 → 64×78 | 21, 60 → 31, 63 | 168×300 → 256×312 |
| Balsam Fir | young | 54×126 → 88×129 | 27, 114 → 43, 117 | 216×504 → 352×516 |
| Balsam Fir | pole | 82×181 → 128×184 | 41, 173 → 63, 176 | 328×724 → 512×736 |
| Balsam Fir | mature | 119×252 → 179×255 | 59, 243 → 90, 246 | 476×1008 → 716×1020 |
| E. White Pine | sapling | 78×121 → 108×114 | 39, 106 → 52, 105 | 312×484 → 432×456 |
| E. White Pine | young | 117×207 → 171×209 | 58, 199 → 85, 201 | 468×828 → 684×836 |
| E. White Pine | pole | 188×311 → 267×313 | 94, 301 → 128, 303 | 752×1244 → 1068×1252 |
| E. White Pine | mature | 269×442 → 379×441 | 134, 429 → 190, 428 | 1076×1768 → 1516×1764 |
| E. White Cedar | sapling | 39×70 → 57×73 | 19, 53 → 28, 56 | 156×280 → 228×292 |
| E. White Cedar | young | 48×113 → 74×116 | 23, 97 → 36, 100 | 192×452 → 296×464 |
| E. White Cedar | pole | 68×163 → 102×166 | 34, 145 → 49, 148 | 272×652 → 408×664 |
| E. White Cedar | mature | 96×225 → 140×228 | 47, 202 → 70, 205 | 384×900 → 560×912 |
| Tamarack | sapling | 40×73 → 70×76 | 19, 62 → 34, 65 | 160×292 → 280×304 |
| Tamarack | young | 51×127 → 99×130 | 25, 120 → 48, 123 | 204×508 → 396×520 |
| Tamarack | pole | 78×190 → 148×193 | 38, 182 → 73, 185 | 312×760 → 592×772 |
| Tamarack | mature | 111×266 → 207×269 | 55, 257 → 103, 260 | 444×1064 → 828×1076 |
| White Birch | sapling | 51×67 → 83×70 | 25, 60 → 41, 63 | 204×268 → 332×280 |
| White Birch | young | 76×117 → 134×121 | 37, 110 → 67, 114 | 304×468 → 536×484 |
| White Birch | pole | 109×174 → 195×179 | 54, 166 → 96, 171 | 436×696 → 780×716 |
| White Birch | mature | 162×269 → 283×277 | 81, 260 → 140, 268 | 648×1076 → 1132×1108 |
| Red Maple | sapling | 58×75 → 90×77 | 28, 68 → 42, 70 | 232×300 → 360×308 |
| Red Maple | young | 94×132 → 145×134 | 47, 125 → 73, 127 | 376×528 → 580×536 |
| Red Maple | pole | 141×196 → 210×198 | 70, 187 → 105, 189 | 564×784 → 840×792 |
| Red Maple | mature | 209×320 → 301×321 | 104, 309 → 149, 310 | 836×1280 → 1204×1284 |
| Red Oak | sapling | 77×79 → 103×81 | 38, 72 → 50, 74 | 308×316 → 412×324 |
| Red Oak | young | 148×150 → 191×152 | 74, 142 → 95, 144 | 592×600 → 764×608 |
| Red Oak | pole | 223×234 → 285×235 | 111, 224 → 137, 225 | 892×936 → 1140×940 |
| Red Oak | mature | 331×347 → 407×347 | 165, 334 → 202, 334 | 1324×1388 → 1628×1388 |
| Trembling Aspen | sapling | 43×62 → 77×72 | 21, 55 → 37, 65 | 172×248 → 308×288 |
| Trembling Aspen | young | 61×107 → 113×127 | 30, 100 → 55, 120 | 244×428 → 452×508 |
| Trembling Aspen | pole | 86×194 → 161×183 | 43, 186 → 78, 175 | 344×776 → 644×732 |
| Trembling Aspen | mature | 117×271 → 226×294 | 58, 262 → 113, 285 | 468×1084 → 904×1176 |

## The sheets each season costs

Summer is the base. Autumn shares every geometry map with summer (`_normal _light _detail _snow _wind
_phase`, byte for byte, all four variants): the five species with a fall colour (tamarack, white birch,
red maple, red oak, trembling aspen) add `_unlit` and nothing else, and the five evergreens with no fall
colour are identical in autumn, so they add nothing. Winter shares nothing with summer, evergreens
included: `build()` adds 31 to the seed in winter, so a winter spruce is a different draw of the species,
not the summer tree with snow. Snow cover adds no sheets either way (one `_snow` map covers every cover).

RGBA8, uncompressed, all 40 species × stages, four variants:

| Season | Baked frames (4.1 contract) | Rest pose + weights (`treeMaps4.js`) |
|---|---|---|
| summer | 5,120 sheets · 11.52 GB | 280 sheets · 157.4 MB |
| autumn | +640 sheets (`_unlit`, five species) · +1.56 GB | +20 sheets (`_unlit`, five species) · +12.2 MB |
| winter | +5,120 sheets · +11.52 GB | +280 sheets · +157.4 MB |
| total | 10,880 sheets · 24.59 GB | 580 sheets · 327.1 MB |

Baked frames: a 4 × 4 sheet per species × stage × season × variant × wind level (4) × direction (2) ×
map (`_unlit _normal _light _detail`; `_lit` optional, +25%). Baking every level and direction with no
sharing is 34.55 GB (15,360 sheets); your 8 GB is the one-variant figure, 8.64 GB (8.04 GiB, 3,840
sheets). Rest pose + weights: a 4 × 1 sheet of the four variants per species × stage × season × map,
seven maps (`_unlit _normal _light _detail _snow _wind _phase`), and any wind level, direction, gust and
frame comes from the shader. That is 106× smaller than the unshared bake. `checks/out/budget.txt` has the
arithmetic; `checks/out/seasons.txt` has the byte comparison for every species × stage.

## Snow: one map

`_snow` is one byte per pixel: the snow cover × 254 at which that pixel turns to snow. 1–254; 255 = never
(sheltered, sky visibility ≤ 0.2); alpha 0 outside the silhouette. At any cover:

    snowed = round(cover × 254) >= byte

so the game thresholds one sheet at any cover, and draws a snowed pixel from the SNOW ramp a band up, as
`relight()` does. The byte is `relight()`'s own rule solved for the cover (up-facing normal against a
threshold with per-stamp noise, AO > 0.2, the dark between leaves only above 60 %), so it is exact at every
cover k/254, including 0, 50 % (127) and 100 % (254). It is keyed on the stamp, so it travels with the
leaves; store it with the pose it was made on (rest pose, or each baked frame).

Checked against `relight()` output on every species × stage × season group, at rest and in a gale frame,
at covers 0 · 6 · 64 · 127 · 153 · 200 · 254: 160 frames, 1,120 relights, 0 mismatched pixels; and a full
0–254 sweep on three mature trees, 0 mismatched pixels. Renders: `renders/snow-000.png`, `snow-050.png`,
`snow-100.png`, and the map itself, `renders/snow-map.png`.

In an engine without live light, the `_lit` sheet at cover c is not exactly a pick between `_lit` at 0 and
at 100 %: `relight()` adds a little ground bounce off snow, which lifts some downward-facing pixels a band.
The snowed/not-snowed mask is exact; that bounce is the light's.

## Wind: weights for a shader

Two maps on the rest pose, RGBA8, opaque (A = 255), filled outward from the silhouette so a gather can
start off it. Stamp-flat on leaves: every pixel of a leaf carries its centre's values, so a leaf moves as one.

| map | R | G | B |
|---|---|---|---|
| `_wind` — how far | **lean**: (height / H)^1.8, 0 at the foot → 255 at the top | **sway**: reach from the stem, r / 1.3 | **flutter**: 255 on a leaf (the flutter unit, 1 px), 0 on wood and the dark between leaves |
| `_phase` — when | **wave**: position across the crown, X = (x − cx) / crownR, stored (X / 4 + ½) · 255 | **play**: the mass's own phase offset J ∈ ±0.25 rad, stored (J / 0.5 + ½) · 255 | **depth**: view depth, 0 far → 255 near, to settle overlaps |

Per species × stage the shader also needs `bendPx`, `limbPx`, `bob` and the flutter rates, all in
`maps/treeMaps4.json`. For wind w, gust g, direction dir = ±1 and loop phase φ = frame / 16:

    env   = 1 + 0.9 g sin(2πφ + 0.7)
    trunk = w² bendPx (0.8 + 0.2 env) + 0.42 w bendPx sin(2πφ + 0.3)(0.55 + 0.45 env)
    la    = limbPx w (0.6 + 0.4 env)
    p     = 4πφ − 0.9 dir X + J                      s = G / 255 × 1.3
    dx    = dir (trunk · R/255 + la s^1.2 (sin p + ½))
    dy    = −½ la bob s sin(p + 1.1)

These are `TreeRig4.windAt()`'s terms, sampled where the rig samples them. A pixel moves round(dx, dy). A
leaf (`_detail` gives its stamp id) moves round(dx + dither, dy + dither) plus its flutter push, one offset
for the whole leaf; dither, flutter slots and turn-over are keyed on the stamp id with the rig's integer
hash, `hsh(n, k)` in `treeMaps4.js`, which ports to GLSL ES 3.0 as uint arithmetic. Gather per destination
pixel: step twice S ← D − round(field(S)); of the 25 pixels within 2 of S keep those whose own offset lands
on D; nearer wins (depth, beyond 3/255), then a leaf, then the later stamp. If nothing lands, a foliage
pixel under S draws as the dark between leaves (a leaf has moved off it), wood as wood.
`TreeMaps4.shade(rest, wind, frame)` is the reference and returns a frame `relight()` and `castShadow()`
accept; the page's SHADER channel plays it.

Checked against the rig's own baked frames (`checks/out/wind.txt`): mature summer, every species, calm ·
breeze · gale × west · east, gust 0.5, frames 1 5 9 13. Means over the ten species:

| | silhouette overlap | same material and leaf | same lit colour | rest pose, unmoved: same leaf |
|---|---:|---:|---:|---:|
| calm | 100.0 % | 100.0 % | 99.9–100.0 % | 99.9 % |
| breeze | 99.4 % | 94.1 % | 92.7–92.8 % | 61.5–62.3 % |
| gale | 98.5 % | 88.7–88.8 % | 82.8–83.9 % | 15.6–15.7 % |

The rig lays every leaf's whole footprint each frame; the rest pose keeps only the top leaf at each pixel
(on the mature oak, 49 % of leaf-footprint pixels are visible). Where neighbouring leaves round to
different offsets, the rig uncovers the hidden part of a lower leaf and the shader shows the dark between
leaves instead: that is most of the difference, 2–10 % of pixels on the oak and the pine at breeze and
gale. The lit column is lower again because the rig re-derives sun transmission, wood AO and bark burial
per frame, and the shader carries the rest pose's. The leaf offsets themselves are the rig's: in a spot
check on oak, pine and aspen, 95.5–99.8 % of leaves took the rig's exact offset (the rig interpolates its
field on a 4 px grid; the maps sample it exactly, in 8 bits). Renders: `renders/wind-{calm,breeze,gale}-
{west,east}.png` (shader over rig), `renders/wind-gale-loop.png` (eight frames of a gale, three trees),
`renders/wind-weights.png`.

## Checks

`node checks/run.js` (Node 18+, no packages) runs them all and rewrites `checks/out/`; `checks/treeChecks.js`
also runs in a page or sandbox. The output is deterministic, so a re-run reproduces the files.

| check | what it does | result |
|---|---|---|
| `stamps` | each sidecar's three hashes against the rig, sky and writer as shipped | 10 of 10 match, self-tests 0 failed |
| `sidecars` | re-runs the writer; each committed sidecar is byte for byte what it writes today | 10 of 10 identical, 1,458 self-test checks, 0 failed |
| `cells` | the table above from both rigs; 4.1 pivot on the trunk-foot column; ≤ 2048; README rows | 40 of 40 |
| `snow` | `_snow` against `relight()` output | 1,120 relights + 3 full sweeps, 0 mismatched pixels |
| `wind` | the reference shader against the rig's baked frames | passes (silhouette ≥ 95 %, geometry ≥ 80 % in every row) |
| `seasons` | byte comparison of every map, autumn and winter against summer | as stated above, all 40 |
| `budget` | the bytes in The sheets each season costs | — |
| `sums` | SHA256SUMS.txt against every file, and no file left out | every file matches |

`checks/render.js` makes `renders/` and `maps/` from the same files (`checks/renderReview.js`).
SHA256SUMS.txt lists every file in the kit, this README included, except itself and `checks/out/sums.txt`,
which is the check of the list and so cannot be in it.

## Review renders

    family-seasons.png         the family in summer, autumn and winter, 14:00 clear, on the pixel terrain
    snow-000/050/100.png       winter and summer at snow cover 0, 50 and 100 %, 10:00 after snowfall
    snow-map.png               _snow for the family, summer and winter, with its key
    wind-calm-west/east.png    shader over rig, calm, from the west and from the east
    wind-breeze-west/east.png  shader over rig, breeze 30 %
    wind-gale-west/east.png    shader over rig, gale 100 %
    wind-gale-loop.png         eight frames of a gale for red oak, white pine and aspen, shader over rig
    wind-weights.png           _wind (and R, G, B alone) and _phase for the family
    pass3-vs-4.1.png           the family in pass 3 and in 4.1, same baseline

## Gameplay sidecars

One file per species, written by `TREE_GAMEPLAY.sidecar(TreeRig4, key)` and stamped with three hashes:
`derivedFromRigSha256` (the rig), `skyDerivedFromRigSha256` (the sky, which owns the shadow law) and
`writerDerivedFromRigSha256` (the writer). Generated, never edited: if a hash moves, re-run the writer
(the page's GAMEPLAY SIDECAR button, or over the family) rather than patching a number.

**Frame.** Scene metres, 32 px = 1 m, like every other sidecar. Origin is the trunk foot, which is the
sprite pivot (centre of the pivot column, top edge of the pivot row). +x east, +y south (toward the
camera), +z up. A tree does not turn, so there is no facing split. Trees bake at **0.6 of true height**
(the bible), so the file is in scene metres, the frame boats, buildings and characters share, with the
true botanical sizes beside them.

| Section | What it carries |
|---|---|
| `SPECIES` · `SEASONS` | Name, form, evergreen or bare in winter, true height / crown / dbh, stems. Summer and autumn share geometry. |
| `STAGES.<stage>.SPRITE` | Cell, pivot, pad below the foot, 4 × 4 sheet size and 2048 fit, the gale pad either side. |
| `STAGES.<stage>.TRUNK` | **The only collider:** a circle at the foot, r = the bole's base radius. `foot_r_m` (root buttress reach) is data, not a second collider. |
| `STAGES.<stage>.CANOPY.summer_autumn` / `.winter` | Plan footprint (rect and a round bound), foliage base, lowest outward limb, top, `walk_under` against a 1.80 m head, and the rest-pose screen rect for fade tests. |
| `…CANOPY.*.SHADE` | The sunless shade (canopy + trunk contact) measured with `castShadow`, union of the four variants: rect, centroid, equal-area ellipse, area. The one part of the shadow that does not move with the hour. |
| `WIND` | 16-frame loop, the four bake levels, the species' wind vector and shimmer. The law puts zero displacement at the ground: **the collider and the sort row never move.** |
| `SHADOW` | The live law: `castShadow` levels 0–3, the contact ellipse, the `sky.shear` fallback. Ground-only. |
| `SORT` | Y-sort on the pivot row; how far the crown overhangs the foot toward the camera. |
| `_excluded` · `_confirm` · `_checks` | Absences, open calls, and the self-test tally. |

Mature trees, scene metres (true height in brackets). The walk-under column reads sapling · young ·
pole · mature.

| Species | Height | Trunk r | Foliage base | Crown r | Shade m² | Winter | Walk under | Sheet |
|---|---:|---:|---:|---:|---:|---|---|---|
| Red Spruce | 12.6 (21) | 0.182 | 1.92 | 1.98 | 4.7 | leafed | – – – ✓ | 976 × 1328 |
| Black Spruce | 6.6 (11) | 0.092 | 1.57 | 0.83 | 0.7 | leafed | – – – – | 516 × 728 |
| Balsam Fir | 9.6 (16) | 0.132 | 0.79 | 1.45 | 2.6 | leafed | – – – – | 716 × 1020 |
| E. White Pine | 16.2 (27) | 0.297 | 6.57 | 3.31 | 11.1 | leafed | – – ✓ ✓ | 1516 × 1764 |
| E. White Cedar | 7.8 (13) | 0.149 | 0 | 1.23 | 3.7 | leafed | – – – – | 560 × 912 |
| Tamarack | 10.2 (17) | 0.132 | 2.21 | 1.46 | 2.7 | bare, 0.7 m² | – – – ✓ | 828 × 1076 |
| White Birch | 10.2 (17) | 0.132 | 4.25 | 2.29 | 7.3 | bare, 2.5 m² | – – ✓ ✓ | 1132 × 1108 |
| Red Maple | 12.0 (20) | 0.198 | 4.16 | 2.96 | 12.6 | bare, 4.2 m² | – ✓ ✓ ✓ | 1204 × 1284 |
| Red Oak | 13.2 (22) | 0.281 | 5.66 | 4.89 | 30.0 | bare, 10.2 m² | – ✓ ✓ ✓ | 1628 × 1388 |
| Trembling Aspen | 10.8 (18) | 0.116 | 5.52 | 1.65 | 3.7 | bare, 1.6 m² | – ✓ ✓ ✓ | 904 × 1176 |

**Self-test: 1458 checks, 0 failed.** For each species, stage, season group and variant (320 rest
frames) the writer checks that the pivot is the trunk-foot column, that the bole seen clear of foliage
above the buttresses is centred on it and as wide as the collider (±1.25 px), that the contact shadow
is centred on the foot, that the rest silhouette leaves the gale pad free, and that every sheet fits.

Four things to know before wiring these up:

- **Crowns are drawn about half as deep as they are wide.** The oak's footprint is 9.3 m east–west by
  4.9 m north–south. `plan_rect_m` and `SHADE` are the footprint as drawn; `plan_r_m` is the round
  bound if gameplay wants circles.
- **Fir, black spruce and cedar never clear a standing head**, at any stage: their skirts come to
  within 0.8–1.6 m of the ground (cedar to it). Brush, blocker or fade is a gameplay call.
- **Only the bole blocks.** Limbs and foliage are overhead or see-through, and their heights are data.
  Sapling boles are about 0.05 m; they are published solid, and `_confirm` asks whether that is right.
- **Sort overhang.** The crown reaches up to 2.7 m south of the foot (the mature oak). A character
  standing there sorts in front of the whole tree and draws over the crown's front edge. A split crown
  layer is the fix if that matters.

`_excluded` lists what is not modelled: interaction (chop, climb, forage), stump or felled states,
limb colliders, tree-on-tree shadows, snow load, spring, and footstep data. `_confirm` lists the open
calls: the 1.80 m head height (ours), sapling blocking, low skirts, whether shade shelters from rain,
the playback rate (the rig has no clock; the page previews at 8 fps, a 2 s loop), and the sort overhang.

Verify before trusting any of it:

    sha256 treeIsoRig4.js     -> a25cfba1…  = derivedFromRigSha256 in all ten
    sha256 weatherSky.js      -> 38300697…  = skyDerivedFromRigSha256
    sha256 _treeGameplay.js   -> 768ed2e1…  = writerDerivedFromRigSha256

## Use

    const sky = WeatherSky.at({ time: 19.1, cloud: 0.1, rain: 0, fog: 0, snow: 0, wind: 0.4, gust: 0.5, dir: 1 });
    const fr  = TreeRig4.frame('RedMaple', { stage: 'mature', season: 'summer', variant: 0, wind: sky.wind, frame: f });
    const rgba = TreeRig4.relight(fr, sky);          // w = fr.v.w, h = fr.v.h, pivot = fr.mdl.pivot (trunk foot)
    const sh   = TreeRig4.castShadow(fr, sky);       // {x0, y0, w, h, lv}: 1 canopy · 2 partial · 3 full, cell coords
    WeatherSky.lightTile(tile, PxLang.normals(tile, 1.2), sky, { w, h, level })   // the floor at shade level 0–3

    // the weights pipeline
    const rest = TreeMaps4.rest('RedMaple', { stage: 'mature', season: 'summer', variant: 0 });
    const snow = TreeMaps4.snowMap(rest);            // Uint8Array, snowed = round(cover × 254) >= snow[i]
    const { wind, phase } = TreeMaps4.windMaps(rest);
    const moved = TreeMaps4.shade(rest, sky.wind, f); // a frame: relight(moved, sky), castShadow(moved, sky)
    const sheet = TreeMaps4.sheet('RedMaple', 'mature', 'summer', 'wind');   // four variants, 4 × 1

`TreeRig4.view(fr, ch, sky)` gives any channel: `lit unlit normal ao trans height depth sun stamps parts
wood light detail`; `TreeMaps4.view(fr, ch)` adds `snow wind phase`. `TreeRig4.sheet(key, o, ch, sky)` gives
the wind loop as a 4 × 4 sheet; `sheetSpec()` checks it against the 2048 cap.

## Engine contract (light maps)

Either pipeline uses the same maps on one cell and pivot:

| map | channels |
|---|---|
| `_unlit` | structure bands under a flat sky, no sun |
| `_normal` | R x · G y-up · B toward camera, stamp-flat |
| `_light` | R sky visibility · G leaf thinness × translucency · B height above ground (0 foot → 255 tree height) |
| `_detail` | RG stamp id + 1 (16-bit, 0 = between leaves) · B 255 leaf / 170 bark / 85 twig |
| `_snow` | the cover × 254 at which the pixel turns to snow; 255 never (one channel is enough) |
| `_wind` · `_phase` | the wind weights above (weights pipeline only) |
| `_lit` | optional, `REF_SKY` (14:00 clear): for engines without live light |

**Baked frames:** per species × stage × season × wind level (calm 0 · breeze .3 · wind .6 · gale 1) ×
direction, 16 frames each, `_snow` per frame. **Rest pose + weights:** per species × stage × season, the four
variants at rest, seven maps; the shader moves all of them together (sample every map at the gathered source).

Reference shader = `relight()`: per stamp, `sky·(0.3+0.7·AO)·up-facing + sun·N·L^1.25·T + back·T + ground
bounce`, banded on the foliage steps (.06 .16 .33 .74); harmony's grade, wet, fog steps, snow where `_snow`
says. No live projection? Skew the silhouette by `sky.shear`. Wind `dir` is not a mirror: the light is not
symmetric, so bake each direction you use (the weights pipeline gets both from one set of maps).

Import: Point filter, no compression, mips off at 1:1; sRGB off on `_normal _light _detail _snow _wind
_phase`. `_wind` and `_phase` are data: no premultiply, no alpha-is-transparency.

## Still open

- The bible's top-of-frame key vs the rigs' upper-left: one keyframe in `weatherSky.js`.
- Only one species × stage (red maple, mature) is baked in `maps/`; `node checks/render.js maps <Species> <stage>`
  bakes any other. No baked-frame sheets are shipped.
- Winter reseeds the evergreens, so a winter conifer costs a full set of maps. Keeping the summer seed for
  them would make an evergreen's winter a colour change (`_unlit` only). A rig change; not made here.
- The rig keys snow noise on wood and between leaves to the screen pixel, so in its baked frames that snow
  shifts as a trunk sways. The rest-pose `_snow`, gathered with the pose, keeps it on the bark.
- Trees do not yet shadow each other or other sprites. `castShadow` levels are ground-only; the shader path
  casts the rest pose's shadow.
