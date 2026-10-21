# Rig 9 (character v9.2): intake record

Landed 2026-09-25 by the art-pipeline lane: PR 1 of the character rig and builder intake
(charter `HANDOFF-2026-09-23-character-rig-and-builder-intake.md`). This folder is Claude Design's kit
**as delivered**, except for 21 generated files that real Node writes differently (below). Nothing in
the rig was edited. Rig 7's folder (`../rig7/`, `../../characterIsoRig7.js`) is unchanged. Rig 9 is
registered beside it as catalog key `characterRig9`, and since Phase B (below) it is the rig the editor
bakes (`CharacterSkinAssetBaker.LiveRig`).

| | |
|---|---|
| Drop | `C:/hh-drops/character-rig-kit-v9.2.zip`, 6,404,036 bytes, sha256 `6dff1e666b2a2400bdeefe11c07bfa28a6f072f95d5ba74340a419d1a5476130` |
| Kit folder in the zip | `export/character-v9.2-import-kit/`, 194 files |
| On arrival | `SHA256SUMS.txt` lists 193 files: 193 ok, 0 differ, 0 missing, nothing unlisted but itself |
| Rig | `Art/characterIsoRig9.js`, rev 9.2, pass 9, global `CharacterIso9`, sha256 (LF) `02df29ec88dffe9ad68b8761290f4ed66ef73e919346682be43c089be6f49144` |
| Pose library | `Art/characterIsoRig9.poses.js`, sha256 (LF) `42179e4484fa9d0e38a8c467928d7207b5d68d93b97d56fd449acc24468f7f07` |
| Added by the intake | `INTAKE.md` (this file), `INTAKE.SHA256SUMS.txt`, `APPEARANCE-MAP.md` (the builder mapping) |

## Phase B: the bake and the switch (2026-09-26)

All ten committed skins (`Assets/_Project/Data/Characters/Skin/<preset>.asset`, the player and the
nine cast) were re-baked from rig 9 in one headless run. Each keeps its GUID, id and switch states. Each
now pins `characterIsoRig9.js` and `characterIsoRig9.poses.js` at the hashes above and records revision
9.2 and tone rule V9. Each carries 28 bones, 53 clips and a bind mesh of 1,468 to 1,800 corners, and
paints 18 to 23 of the 32 colour slots that V9 allows. The plates were shot in the same slot: ashore,
aboard and mounted at all eight facings, the animation sheets, and a before/after against rig 7. They
are evidence and are not committed; the PR carries their numbers. Rig 7 stays in the catalog. To go
back, set `LiveRig` to `CharacterSkinExtractor.CatalogKey` and re-bake.

## The 21 files Node regenerated

The charter says: commit Node's JSON, not the drop's. On real Node (v24.19.0), the kit's own
`export-builds` checker fails on the delivered builds, then passes once it has rewritten them. These
are the files that changed:

| Files | Why the drop's copy differs | How big the difference is |
|---|---|---|
| `builds/{fisher,ginny,skipper,nan,deckboss,packer,cutter,hand,boy,girl}.v9.json` | The kit's export was not written by Node: the last bit of the floating-point numbers differs | 887–1,448 of about 137,000 numbers per file; at most 6.2e-15 absolute; values near 1e-17 flip sign; no structural or string difference |
| `reports/exports.{txt,json}` | They record the byte sizes of the builds | Sizes only |
| `reports/bodies.{txt,json}` | Printed residues (around 1e-16 m) and clip sizes in KB | Printing of values at or below 1e-15 |
| `reports/rig7.{txt,json}` | They record the rig 7 kit path and the hashes of the v9 builds | Path and hashes |
| `reports/shading.{txt,json}`, `reports/worldfit.{txt,json}` | The delivered reports say `data/shading.v9.json` and `data/world-fit.v9.json` DIFFER from their regeneration. On Node, both regenerate **byte-identical**, so the data files are committed as delivered and the reports now say so | 2 lines each |
| `samples/fisher.walk.clip.v9.json` | Number printing only: 0 of 2,267 values differ | 33,426 → 33,423 bytes |

`sha256sum -c SHA256SUMS.txt` in this folder gives 172 OK and 21 FAILED, and the 21 are exactly the
files above. `sha256sum -c INTAKE.SHA256SUMS.txt` gives 21 OK. Between them, the two lists pin every
file. Every other file is byte for byte as delivered, and Node regenerates each of these byte-identical:
the ten gameplay sidecars, `builds/presets.json`, `builds/rock.v9.json` and the three `data/*.v9.json`.

## What was checked (no Unity)

| Check | Engine | Result |
|---|---|---|
| Zip hash and `SHA256SUMS.txt` | sha256sum | 193 / 193 on arrival |
| The kit's ten checkers, `tools/run-all.cjs` | Node v24.19.0 | **All 10 OK** on this folder as committed, and nothing is rewritten (a copy of the folder diffs empty afterwards) |
| Every creator body, `check-bodies` | Node | 548 / 548 pass: creator 375, children 125, extremes 48. Budget rows are a note, not a failure |
| The renders, by pixel | PIL decode of every PNG against Node's re-render | 120 / 120, 0 differing pixels, manifest pixel hashes OK |
| JSON: drop against Node, and ClearScript V8 against Node | Numeric tree diff | Largest absolute difference 6.2e-15; no structural or string difference |
| V8 harness, the engine the editor bakes with | ClearScript V8 | 6 / 6 guards. Ten presets have 28 bones and 53 clips, and the face and tri counts match `reports/numbers.txt`. 60 / 60 1x strips hash to the manifest. runChecks is 190 / 190 gated, and every value equals `golden-report.json`. The build JSON is not byte-identical to Node's (at most 6.2e-15); the gameplay sidecars are |
| The C# readers, compiled, run headless over the rig | V8 plus the compiled `HiddenHarbours.Tools.RigBaking.Editor` | Ten presets: 28 bones, 53 clips, 438 frames. The rest pose composes to 1e-6, and the bind mesh agrees with the skinning. Painted materials are 18–23 (the limit is 32). The facet sign is NEGATED on every preset, with a smallest margin of 9.70× |
| The whole face, every group of every slot | the same | Rest face / whole face, in materials: fisher 19/21, ginny 19/21, skipper 18/21, nan 21/24, deckboss 20/22, packer 23/25, cutter 21/23, hand 21/23, boy 18/20, girl 19/21. Every group paints only declared materials |
| The creator's options, `data/options.v9.json` | the same | 136 offered options all resolve on the rig, and the only one not offered is `age.child`. Over all 62,400 body and head combinations (garment × bottom, then hat × hair style × beard × eye shape) the worst build paints 28 materials with every face group, 25 with the rest face (apron or skirt, watch cap, crop, stubble). A sample of 400 builds agrees with the rig's own mesh on every material |
| The skinning replay, every clip and frame | the same | Each preset's bind mesh, skinned by its bones and weights, lands on the rig's own posed corners. The worst corner is 4.9e-7 m (deckboss, `astrideStand` frame 3). 2,544 corner-frames per preset belong to face groups the rest face does not show, and are skipped |

**What this means for guards:** an editor guard cannot ask for byte identity with Node on
`builds/*.json`. Compare within a tolerance: the rig's 1e-6 is eight orders of magnitude above the
noise. Gameplay sidecars, renders and runChecks values can be compared exactly.

## Line endings

`.gitattributes` pins this folder to LF by extension (`js cjs json txt md html`), and the PNGs stay in
Git LFS. The kit's `SHA256SUMS.txt` is taken over LF bytes, and the baker hashes the rig the same way
(`CharacterSkinExtractor.LfSha256`), so a Windows checkout does not move a pin.

## Re-running the checkers

`run-all.cjs` rewrites `reports/`, so run it on a copy. The rig 7 report records the path it was given,
so pass the same path for a clean diff:

```bash
cp -r docs/art/rigs/character/rig9 "$TMP/rig9" && cd "$TMP/rig9" && node tools/run-all.cjs C:/hh-drops/2026-09-09-skinned-character-kit
```

It should end `all 10 checkers OK`, and `diff -rq` of the copy against this folder should print nothing.

## Findings for Claude Design (through the owner)

The rig is not edited here. Each item goes back with the evidence named.

1. **Missing README sections.** `README.md` l.9 says the 9.1 README's sections still hold for the rig 7
   comparison, the options file format, shading and the sidecar format. The kit does not ship that
   README. Ask: put those four sections in the 9.2 README, or ship the 9.1 README.
2. **Stale revision labels.** `tools/compare-rig7.cjs` l.140 and `reports/rig7.txt` l.1 call the rig
   "characterIsoRig9 9.1", but the rig reports 9.2.
3. **Stale file name.** `Art/characterIsoRig9.poses.js` l.4 says the solver is in `characterIsoRig8.js`.
   It is in `characterIsoRig9.js`.
4. **Silent fallback for an unknown preset.** `normBuild` (`characterIsoRig9.js` l.340) turns an
   unknown preset name into the Fisher's body labelled "Custom", with no error. `rules.validation`
   documents this for fields, but it also means a mistyped preset at bake time is silent. The baker
   checks the name first (`CharacterSkinExtractor.AssertPreset9`). Ask: a strict mode, or an error for an
   unknown preset name. Low priority.
5. **No dither, tolerance or ink export.** The rig exports no Bayer matrix and no tolerance, so the baker
   carries the standard 4×4 Bayer and 1e-6 itself. It does not export its ink either (`INK`,
   `characterIsoRig9.js` l.227), although the options file says the brow is a mix of the hair with it
   (`brow = mix(hair ramp [0], ink, 0.34)`). The audit searches for the ink that gives all 9 hair
   options their brow, and finds exactly one, `#12181b`, which is `INK[0]`. Ask: export them, so the
   engine reads them instead of copying or searching for them.
6. **No blink or look-at in the renders.** Blink and look-at ship as data (`BLINK`, `faceClips`,
   `overlays.blink`, the `gaze` and `look` checks), and `check-look` passes. But none of the 120 renders
   shows a blink or a look; they cover idle, walk and dig only. The kit knows this (README §12). Ask: a
   strip with a blink and a look-at, so the engine's playback can be checked by pixel (owner ruling 8:
   the engine plays them).
7. **The kit's files were not written on Node.** The builds carry last-bit noise against Node, the
   numbers are not printed shortest-round-trip, and `reports/shading.txt` and `reports/worldfit.txt` say
   the data files differ from their regeneration when on Node they are byte-identical. Ask: run
   `tools/run-all.cjs` on real Node before shipping, so the committed files are Node's.

**Notes, not defects:**

- Counting every face group, seven presets are over 1,000 tris (`reports/numbers.txt`: fisher 1,060,
  ginny 1,065, skipper 1,072, nan 1,042, packer 1,055, cutter 1,028, boy 1,032). That is allowed: owner
  ruling 7, and `rules.budget.enforced` is false. The default face is 756–919 tris.
- The default saddle (0.94 m seat) fits nobody astride or in the mounts (README §6, §12). The fitted
  saddle is `CONTRACT.saddleFit`: scale 0.40, idle spot 0.60 m, and 0.34–0.49 fits every creator body.
  50 of 125 children fit at the fitted saddle. Sizing the machines belongs to the fixture charter
  (owner ruling 6).
