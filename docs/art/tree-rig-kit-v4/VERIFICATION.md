# Tree rig kit pass 4 (v4.1) — what the repo MEASURED at intake

The drop's own `README.md` sits beside this file, imported verbatim. **This file is the repo's; where
the two disagree, this one is the evidence.** Every figure below was measured at intake (2026-09-24)
on real Node and in the repo's own V8 host, never read off the README.

Nothing in the rig sources was edited. The game still draws the pass-3 trees: `TreeKitCatalog.RigScriptPath`
stays `treeIsoRig3.js` until the pass-4 bake (Phase B of the intake), and the snow global defaults to 0.

---

## 1. What arrived

| | |
|---|---|
| file | `tree-rig-kit-v4.1.zip` (14,207,559 bytes) |
| sha256 | `8e847274523ad8c52efa3482ee4894ef05ec2284751cb7202084029dde88a9b0` — matches the deploy paste |
| entries | 65 files, 0 folders; all 65 identical on disk after the unzip |
| `SHA256SUMS.txt` | 63 of 63 lines verified. The two files it does not list are itself and `checks/out/sums.txt`, by design (the sums check says so) |

The README says the rig, the sky, the map writer and the sidecars are byte for byte the earlier 4.1 zip,
and that this kit supersedes `export/tree-rig-kit-v4/`.

## 2. The kit's checks, re-run on real Node

A scratch copy of the whole kit, **Node v24.19.0**, `node checks/run.js`, then `node checks/render.js`
(the 14 review renders) and `node checks/render.js maps RedMaple mature` (the 15 sample maps).

| check | delivered | Node v24.19.0 |
|---|---|---|
| stamps | 10/10 | same file |
| sidecars | 10/10 identical, 1,458 self-test checks | same file; the 10 regenerated sidecars are byte-identical to the delivered ones |
| cells | 40/40 | same file |
| snow | 1,120 relights + 3 sweeps, 0 mismatched | same file |
| wind | silhouette ≥ 95 %, geometry ≥ 80 % | same file |
| seasons | all 40 pass | **same rows, different order** (below) |
| budget | passed: 157.4 MB summer, 327.1 MB all three seasons (all 40 species × stages, the kit's own count) | same file |
| sums | every file matches | **1 FAILED: `checks/out/seasons.txt`**, 62 of 63 match |

**The one difference is row order, not content.** The delivered `checks/out/seasons.txt` prints the four
WhitePine rows after Tamarack. The seasons check walks `TreeRig4.SPECIES` in order, and the delivered rig
lists WhitePine fourth, which is where a fresh run prints it. Every row is identical in content. The sums
check hashes `seasons.txt` after the seasons check has rewritten it, so the order difference alone is
what fails the one sums line. Committed as delivered; noted for Claude Design.

`maps/treeMaps4.json` differs from the delivered copy only in its `generated` field. **The repo commits
Node's copy.**

## 3. Pixels, not bytes — three engines agree

- **Claude Design vs Node:** all **29 of 29** PNGs (14 renders + 15 maps) are **pixel-identical** to
  Node's re-render, and none is byte-identical. The encoder differs; the pixels do not.
- **The repo's V8 host** (ClearScript V8, the rigs loaded unmodified):
  - control on pass 3: `TreeRig3` RedSpruce mature summer, variants 0–3, rebuilt as one sheet row, is
    **identical** to the committed pass-3 sheet row for albedo, mask and normal (821,184 bytes each). The
    host reproduces the committed bake;
  - pass 4: `TreeMaps4.sheet('RedMaple', 'mature', …)` for all 15 sample maps (summer and winter × unlit,
    normal, light, detail, snow, wind, phase, plus autumn unlit) is **identical** to the drop's PNG pixels.

## 4. The sheets move — species × cell, pass 3 → 4.1

Metres are unchanged. Every cell widens by 2 × `windReach` so the sway has room, and grows 0–23 px in
height. The pivot stays on the trunk base. `trunkAnchor` is the pivot's fraction of the cell, so it moves
with the cell.

| species | cell p3 → 4.1 | pivot p3 → 4.1 | trunkAnchor p3 → 4.1 | baked row (4 variants) p3 → 4.1 | rig sheet 4.1 | windReach |
|---|---|---|---|---|---|---|
| RedSpruce | 156×329 → 244×332 | 77,318 → 122,321 | 0.0304 → 0.0301 | 624×329 → 976×332 | 976×1328 | 44 |
| BlackSpruce | 73×179 → 129×182 | 36,171 → 64,174 | 0.0391 → 0.0385 | 292×179 → 516×182 | 516×728 | 28 |
| BalsamFir | 119×252 → 179×255 | 59,243 → 90,246 | 0.0317 → 0.0314 | 476×252 → 716×255 | 716×1020 | 30 |
| WhitePine | 269×442 → 379×441 | 134,429 → 190,428 | 0.0271 → 0.0272 | 1076×442 → 1516×441 | 1516×1764 | 70 |
| WhiteCedar | 96×225 → 140×228 | 47,202 → 70,205 | 0.0978 → 0.0965 | 384×225 → 560×228 | 560×912 | 22 |
| Tamarack | 111×266 → 207×269 | 55,257 → 103,260 | 0.0301 → 0.0297 | 444×266 → 828×269 | 828×1076 | 48 |
| WhiteBirch | 162×269 → 283×277 | 81,260 → 140,268 | 0.0297 → 0.0289 | 648×269 → 1132×277 | 1132×1108 | 58 |
| RedMaple | 209×320 → 301×321 | 104,309 → 149,310 | 0.0312 → 0.0312 | 836×320 → 1204×321 | 1204×1284 | 47 |
| RedOak | 331×347 → 407×347 | 165,334 → 202,334 | 0.0346 → 0.0346 | 1324×347 → 1628×347 | 1628×1388 | 39 |
| TremblingAspen | 117×271 → 226×294 | 58,262 → 113,285 | 0.0295 → 0.0272 | 468×271 → 904×294 | 904×1176 | 52 |

- The mature one-row sheets, all 10 species: 2,085,700 px → 3,150,232 px (× 1.51). At RGBA32 that is
  **8.3 MB → 12.6 MB per sheet kind** (albedo, mask, normal, and each new map).
- **No sheet is over 2048 px** at any stage. The largest baked row is RedOak's, 1628×347.
- The rig's `SPECIES` and `Trees.json` hold the same 10 species (in a different order).

## 5. What was committed, and where

| from the drop | landed at | how |
|---|---|---|
| `treeIsoRig4.js`, `treeMaps4.js`, `weatherSky.js`, `_treeGameplay.js` | `docs/art/rigs/` | as delivered |
| `gameplay/treeIsoRig4.<Species>.gameplay.json` × 10 | `docs/art/rigs/gameplay/trees/` | Node's regenerated copies, byte-identical to the delivered ones |
| `README.md`, `support.js`, `Tree Rig Pass 4.dc.html`, `SHA256SUMS.txt` | `docs/art/tree-rig-kit-v4/` | as delivered |
| `checks/run.js`, `render.js`, `renderReview.js`, `treeChecks.js` | `docs/art/tree-rig-kit-v4/checks/` | as delivered |
| `checks/out/*.txt` × 8 | `docs/art/tree-rig-kit-v4/checks/out/` | as delivered (Claude Design's run) |
| `maps/treeMaps4.json` | `docs/art/tree-rig-kit-v4/maps/` | **Node's** copy |
| `renders/*.png` × 14 | `docs/art/spikes/tree-pass-4/` | as delivered, Git LFS |

Every committed text file is LF and pinned `text eol=lf` in `.gitattributes`, because `SHA256SUMS.txt`
hashes LF bytes. The renders stay on the `*.png` LFS rule.

**Not committed:**
- `lib/` × 5 — each is byte-identical to a file the repo already carries:

  | kit path | the repo's copy |
  |---|---|
  | `lib/_sidecarExport.js` | `docs/art/rigs/gas-station-rig/tools/_sidecarExport.js` |
  | `lib/harmonyScenes.js` | `docs/art/rigs/px-greenery-harmony-kit/scene/harmonyScenes.js` |
  | `lib/pixelLanguage.js` | `docs/art/rigs/px-cliff-face-kit/bake/pixelLanguage.js` |
  | `lib/pxKit.js` | `docs/art/rigs/px-greenery-harmony-kit/Art/pxKit.js` |
  | `lib/treeIsoRig3.js` | `docs/art/rigs/treeIsoRig3.js` |

- `maps/*.png` × 15 — one species at one stage (RedMaple mature), a sample of what the bake makes.
  `node checks/render.js maps RedMaple mature` reproduces them pixel for pixel.

Resolved against the committed tree, everything else in the drop is new. The README, `SHA256SUMS.txt`,
`support.js` and one map share a name with files of other kits but not their bytes, and none of them
replaces anything.

## 6. Re-running the kit's checks from the repo

The checkers expect the kit's own layout, so assemble it in a scratch folder (never in the repo):

- `docs/art/tree-rig-kit-v4/` → the kit root (leave out this `VERIFICATION.md`, or the sums check reports
  it as a file it does not list);
- the four rig files from `docs/art/rigs/` → the kit root;
- the five `lib/` files from the table above → `lib/`;
- `docs/art/rigs/gameplay/trees/*.json` → `gameplay/`;
- `docs/art/spikes/tree-pass-4/*.png` → `renders/`.

Then run `node checks/run.js`. Expect every check to pass except `sums`. It fails `checks/out/seasons.txt`
(the row order in section 2), and it fails the 15 map PNGs this repo does not carry. Regenerating them
with `node checks/render.js maps RedMaple mature` gives the same pixels but not the same bytes, so the
sums check still fails them. Only the sums check reads PNGs.

## 7. What the tests pin (Phase A)

Phase A adds 51 EditMode tests and no PlayMode test. None needs a graphics device, and none writes into
the kit: the bake tests export and bake into `Temp/hh-tree-pass4-tests/` and delete it afterwards.

| file | tests | what it pins |
|---|---|---|
| `Assets/Tests/EditMode/RigBaking/TreePass4BakeTests.cs` | 14 | one real Temp bake (BlackSpruce and TremblingAspen, summer and winter): the contract on disk as in memory, and byte-identical twice; all 24 sheets equal to the glue's cells byte for byte; the palette (the snow row at the bottom, then each gap row); the snow sheet counted as one channel; every species' geometry and wind against the rig's `sheetSpec()` and `constants()`; three refused bakes that write nothing; the pass-3 kit guard; the 2048 gate; the mask in our order (R key, G back rim, B depth, A coverage) by rig 3's light |
| `Assets/Tests/EditMode/RigBaking/TreeWindMathRigParityTests.cs` | 3 | the shader twin against `treeMaps4.shade()` over the baked maps (120 cases; 99.9 % overall, 99 % in the worst case), the hash bit for bit, the shared constants |
| `Assets/Tests/EditMode/Art/TreeWindMathTests.cs` | 16 | the HLSL's 21 constants and 34 load-bearing lines against the twin; the twin's arithmetic; the anchor, the catalog, the material, the importer, the mesh type and the one-channel snow sheet |
| `Assets/Tests/EditMode/Art/FoliageSnowBridgeTests.cs` | 7 | the `_FoliageSnow` global |
| `Assets/Tests/EditMode/FoliageSnowMathTests.cs` | 11 | the snow cover through the year |

Every check carries the wrong answer it must reject, run in the same test: the next variant, a row shift,
a swapped mask, a one-bit hash, a frame late, a drifted contract.

Compiled outside Unity against the 6000.5 assemblies: 30 asmdefs, 0 errors, 0 warnings. Two controls:
with the production changes reverted, all three test assemblies that carry the new tests fail to compile;
with one planted error in `TreePass4Baker.cs`, only its own assembly fails, and its six dependents with it.
CI's run is recorded in the PR.
