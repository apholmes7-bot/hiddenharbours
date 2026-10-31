# sidecars/ — the rigs' data, for an engine that will not run the JS
Schema `hidden-harbours/rig-sidecar@1`. Five files, one per catch rig, written by `../_catchSidecars.js`
(`CATCH_SIDECARS`, main's harness, verbatim) from the rigs in `../Art/`, with the clock pinned to
2026-10-28T00:00:00.000Z so that they regenerate byte for byte. Regenerate rather than hand-edit.

```
fishIsoRig2.rig.json      FishIso2      species · anims · rests · motion · mouth table · shoal contract · sheet order
crustaceanRig2.rig.json   Crustacean2   kinds · poses · motion · heading / sandZ contracts · pose remaps (verified)
shellfishRig2.rig.json    Shellfish2    item + handful cells · palettes · tiny-kind pixel patterns · bed / flat / heap contracts
clamHodRig.rig.json       ClamHod       cell · grip pivot · dims · rim opening + depth per heading · layer order
catchKit2.rig.json        CatchKit2     the 14 kinds · item anchors · fill / heap / hold rules · spoil
```
The plate rig's sidecar is not one of these: it is `../Art/gameplay/fieldGuidePlateRig.gameplay.json`,
in the notebook kit's form, and it carries the clam's plate drawing (`views`, `plates`).

## What moved from the pass 2 kit's sidecars
Every line not listed here is pass 2's, byte for byte. Line numbers are this kit's.
- Every file: `generated.date` (:7), pinned.
- `fishIsoRig2.rig.json`: `derivedFromRigSha256` (:5); the cell's height, 64 → 80 (:28); each
  species' range, with its lengths and masses at the ends (cod :54-73, haddock :90-109, pollock
  :126-145, mackerel :162-181, striped bass :201-218, flounder :236-256, herring :274-293); the
  herring's flags gain `flash` (:297). Pass B's.
- `crustaceanRig2.rig.json`: `derivedFromRigSha256` (:5); each kind gains `range`, read from
  `SIZES`: the lobster [0.83, 1.79] (:58), the crab [0.63, 1] (:90). Pass D's. The harness holds a
  crustacean at scale 1 only (`hold.at1`), so the masses at a range's ends are not in the file; by
  `hold()`'s law they are 0.40–4.01 kg and 0.10–0.40 kg.
- `shellfishRig2.rig.json`: `derivedFromRigSha256` (:5) only. The harness reads no field of
  `renderPlate` (pass D), and it transcribes the beds (below).
- `catchKit2.rig.json`: the fish item's cell, [64, 64] → [64, 80] (:208), read from FishIso2. The
  rig is main's.
- `clamHodRig.rig.json`: the date only. The rig is main's.

Each file's `note`, and words such as `water.tint`, are the harness's own and still say pass 2 where
they say a pass. Three of its fields are at odds with the rigs now, and the kit's README has them:
- `shellfishRig2.rig.json`'s `bed.densityPerPx` (:165-171) is transcribed, a table written into the
  harness (`_catchSidecars.js:119`) from pass 2's `dens` in `bed()`. Pass B's `bed()` has no such
  table: it tries a mussel w × h × 0.9 times and packs them without overlapping, lays oysters in
  w × h × 0.024 clumps of 4–9 and periwinkles in w × h × 0.02 clusters of 3–7, and tries a scallop
  w × h × 0.05 times, each alone. Its coverage blobs, 2 + 3 × cover, are pass 2's, as the line says.
- `fishIsoRig2.rig.json`'s `water.tint` (:617) gives every underwater pixel the depth's alpha, where
  the shadow anim bakes the `KEY` at 160 with no flash (pass D).
- `catchKit2.rig.json`'s `fill.scale` (:231) gives every kind but the fish [0.85, 1.15], past the
  crab's range at ×1.

## Provenance — three grades, all inside one file
1. **Read from the rig.** Tables the rig exports (`SPECIES`, `ANIMS`, `POSE`, `MOTION`, `RPOSE`,
   `POSES`, `SIZES`, `PAL`, `TINY`, `FRAC`, `DENSE`, cells, pivots, colours) are copied as data.
2. **Computed by the rig.** Anything with a function behind it was called at generation: `mouth`
   (7 species × 6 anims × 8 headings × every frame), `hold` and `sizeOf` at scale 1 and both ends of
   the range, `holes` (400 samples for the spurt timing), `opening` / `cpivot` / `depthPx` for all 8
   headings, `isHeap` per kind. The pose remaps (`rendersAsWalk`) were checked by rendering both
   poses and comparing pixels — `rendersAsWalkVerified` records the result.
3. **Transcribed.** A few internals a rig does not export — bed densities, the item variant → pose
   mapping in `CatchKit2.item`, the heap-lowering rule — are copied from the source and carry a
   `_transcribed` marker. The rig hash is the tripwire for these: if it moves, re-run the harness.

## Common head
```
schema                  hidden-harbours/rig-sidecar@1
rig                     the source file (project path)
exportSymbol            its one global
derivedFromRigSha256    SHA-256 of that file's bytes as read at generation
generated               date · harness · isoSolidSha256 — the shared painter every rig sits on
note                    one line on what the file is
```

## Reading conventions
- Pixel offsets are from the cell **pivot**; +x right, +y down.
- Anything "per heading" is an 8-array indexed by `dir` 0..7 = N NE E SE S SW W NW (45° CW).
- Flattened pairs `[dx0, dy0, dx1, dy1, …]` = one `[dx, dy]` per frame, in frame order.
- Angles in radians, lengths in metres, `ms` = milliseconds per frame, `v` = m/s along the heading.
- `loop: false` marks the one-shot anims (jump, flip, burrow); everything else cycles.
- Pose fields (`sweep`, `curve`, `roll`, `pitch`, `z`, `stretch`, `wph`, `gripU`) are glossed in
  `fishIsoRig2.rig.json → poseGlossary`; motion fields in `crustaceanRig2.rig.json → motionGlossary`.

## Verify
```
sha256 ../Art/fishIsoRig2.js == derivedFromRigSha256 in fishIsoRig2.rig.json   (and so on, one per file)
sha256 ../Art/isoSolid.js    == generated.isoSolidSha256 in every file
```
Sidecar hashes are in `../SHA256SUMS.txt`. A mismatch means the rig moved after these were written:
regenerate (recipe in `../README.md`), do not patch.
