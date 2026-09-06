# sidecars/ — the rigs' data, for an engine that will not run the JS
Schema `hidden-harbours/rig-sidecar@1`. Five files, one per rig, written by `../_catchSidecars.js`
(`CATCH_SIDECARS`). Regenerate rather than hand-edit.

```
fishIsoRig2.rig.json      FishIso2      species · anims · rests · motion · mouth table · shoal contract · sheet order
crustaceanRig2.rig.json   Crustacean2   kinds · poses · motion · heading / sandZ contracts · pose remaps (verified)
shellfishRig2.rig.json    Shellfish2    item + handful cells · palettes · tiny-kind pixel patterns · bed / flat / heap contracts
clamHodRig.rig.json       ClamHod       cell · grip pivot · dims · rim opening + depth per heading · layer order
catchKit2.rig.json        CatchKit2     the 14 kinds · item anchors · fill / heap / hold rules · spoil
```

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
`derivedFromRigSha256` sits straight after `exportSymbol`, the canonical position `Art/_sidecarExport.js`
uses, so these diff cleanly against the gameplay sidecars already committed.

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
