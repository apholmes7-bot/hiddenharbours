# The one-stroke rowing clip

The rig's `oars_row` clip (baked as `walk_oars`) holds two strokes in its eight frames: its pose reads
`ph = frac(2*u)`. One stroke therefore plays on four poses, which reads as choppy at the oars.
`CharacterSkinPose` forbids blending skin frames (it samples and holds, because rig steps can reach
178 degrees). So a smoother stroke needs more baked frames, not a blend.

`row-stroke.contract.json` names a finer sampling of the same clip, without editing the 10.3 rig:

| field | value | meaning |
|---|---|---|
| `state` | `row_oars` | the skin state the clip is stored and switched on under |
| `source` | `oars_row` | the rig clip it is read from (`CARRY_CLIPS`) |
| `strokesInSource` | 2 | strokes in one loop of the source |
| `frames` | 16 | frames in the new clip, one stroke (four times the source's four) |
| `ms` | 55 | frame time; playback is locked to the oar, so this is nominal |

Frame k is the rig's own pose at `u = k / (frames x strokesInSource)`. It is read by
`CharacterSkinExtractor.ReadSubLoopClip9`, which makes the rig's own `clip()` calls (`evalClip`,
`exportLocals`) with only the frame count and `u` changed. Because the source's two strokes are
identical, frame 16 would equal frame 0, so the clip loops without a seam. `RowStrokeContractTests`
checks this on the rig, and checks that every fourth frame equals the source clip's frames.

The contract pins both rig files by LF SHA256, as the seated helms do. If the rig changes, the bake
refuses to run.

## The bake (needs a warm box, a Unity editor)

`CharacterSkinAssetBaker.BakeRowStrokeCli` adds the clip to the ten committed skins under
`Assets/_Project/Data/Characters/Skin/` and appends `row_oars` to each skin's `MeshStates`. It keeps
every other clip, state, bind mesh and material. A full skin refresh (`Bake`) also appends the clip,
without switching it on, so a later re-bake does not drop it.

The batch entry, from the box's project root:

```
Unity.exe -batchmode -nographics -projectPath <box> \
  -executeMethod HiddenHarbours.Tools.RigBaking.CharacterSkinAssetBaker.BakeRowStrokeBatchCli \
  -logFile <box>/Logs/row-stroke-bake.log
```

It exits 0 on success. The log carries `[row-stroke] baked 10 skins, row_oars at 16 frames`, and
`Evidence~/rig-facts/production/row-stroke-bake.json` records the counts.

The same commit as the baked skins must update the switch guards, because the player's committed
switch then gains `row_oars`:

- `CharacterSkinCastBakeTests.PlayerStates`: append `"row_oars"` after `"dig"`.
- `CharacterSkinCastBakeTests` (the rig-coverage test): add `"row_oars"` to `calibrated`. The 36
  count stays the same.
- `CharacterSkinAssetBaker.CastMeshStates`: append `"row_oars"` after the fishing and dig states.

The gameplay side picks `row_oars` over `walk_oars` once a skin draws it as a mesh, and locks its
frame to the oar stroke phase.
