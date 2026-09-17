# Rig 7 follow-up — jobs 1–4, as landed

The four art jobs of the 2026-09-17 rig 7 brief (helm and oars, the rock table, the mount boot, the
children's noses), merged into ONE edit of three rig files and landed in one PR. Nothing here runs in the
game; the rigs it describes are the ones beside it in `docs/art/rigs/`.

| file | landed sha256 (LF) | was (40f4656f) |
|---|---|---|
| `characterIsoRig6.js` | `e440095542dce0c8c68b65e59a7a62f6f9eab10f98a11745f1a139f34a20bb28` | `c2eab86b…` |
| `characterIsoRig7.js` | `f2c51c3be2a98b05c08a278e5a8308ead6a3228df0ab45320886cffdc816ceb8` | `0dddf453…` |
| `characterFaceStudy.js` | `001dbcc01c592294170e83ac43fb640bfda83a6ba08d479b25553bebe941ed99` | `2adcfb56…` |

No bone added, removed or renamed; `ANIMS` is still 35 rows; revision strings unbumped (rig 7 `7.1`,
rig 6 `6.10`).

## The four jobs

| job | what landed | the kit's own words |
|---|---|---|
| 1 · helm and oars | `CARRY_CLIPS`: four piloting stances (`helm_idle`, `helm_walk`, `oars_idle`, `oars_row`) as named clips riding `idle`/`walk` plus a carry, a table of their own so `ANIMS` stays append-only. The skin bake carries them after the 35 rows, keyed `idle_helm`, `walk_helm`, `idle_oars`, `walk_oars`. | [1-helm-oars/README.md](1-helm-oars/README.md) |
| 2 · the rock | A FINDING, not a fix. `additive()` lands **inert**: nothing reads it and no frame changes. The owner picks the exact table or the lean + IK from a helm plate. | [2-rock/README.md](2-rock/README.md) · [2-rock/ROCK-FINDING.md](2-rock/ROCK-FINDING.md) |
| 3 · the mount boot (v2) and `lift` | `mountUp`, `mountDown`, `mountCab`, `mountCabDown` and `lift` re-authored inside the 8 m fence; on 40f4656f, 172 frames across the ten presets stepped past it. | [3-mount/README.md](3-mount/README.md) · [3-mount/MOUNT-BOOT-TABLES.md](3-mount/MOUNT-BOOT-TABLES.md) |
| 4 · the children's noses | The child nose pushed 3 mm forward and 3 mm wide, gated on `b.age === 'child'`: boy and girl land 2, 2, 2, 4, 2 px at the five facing headings at 32 px/m (were 0, 1, 0, 0, 1). The eight adults' composed faces are byte-identical. | [4-noses/README.md](4-noses/README.md) |

The kits' READMEs are as delivered. Their per-kit `check/` scripts and `reference/` images stay with the
delivered kits; the checker below replaces them for the merged bytes.

## The combined checker

`check/check-rig7-followup-combined.cjs` holds every posing gate of the four kits at once on the MERGED
chain, and re-bases each kit's "as delivered" pins to the merged bytes by name (`REBASE` lines, old → new).
Node only, no dependencies; it loads every file unmodified, in the bake's catalog order (`check/load-kit.cjs`).
`check/face-render.cjs` and `check/cast-engine.js` are job 4's rasteriser and cast engine, carried so the
nose gates measure what the kit measured.

```
node check/check-rig7-followup-combined.cjs --main <rigs at 40f4656f> --merged <docs/art/rigs> \
     --j1 <job 1 rigs/> --j2 <job 2 rigs/> --j3 <job 3 rigs/> --j4 <job 4 rigs/> [--out report.txt]
```

`--main` is the nine chain and face files as they were at 40f4656f
(`git show 40f4656f:docs/art/rigs/<file>`); `--j1`…`--j4` are the four kits' `rigs/` folders as delivered.
Hashes are taken of the LF form, so a CRLF checkout reads the same.

[checks.txt](checks.txt) is its run on this landing: **48 gates, 0 failed**.

## What holds it on CI

The checker is the art-side proof. The game-side guards, all EditMode and none needing a GPU:

- `CharacterSkinBakeGuardTests.EveryCommittedSkinDef_PinsTheRigsAsTheyAreToday`: every committed skin def
  was baked from the rigs in the repo (re-bake, never re-pin).
- `CharacterSkinBakeGuardTests.TheCommittedBindMeshIsTheFaceTheChainComposesToday`: the committed bind mesh
  is the face the unhashed face chain composes now.
- `CharacterSkinDefContentTests.EveryClipTheDefCarriesIsEntirelyInsideTheFence`: every clip, reachable or not.
- `CharacterSkinBakeGuardTests.TheDefPosesTheRigsOwnGeometry_OnEveryClipRowAndEveryFrame`: now sweeps the four
  carry rows against `facesOf(pose(anim, u, build, {carry}))` and says it met them.
- `CharacterFaceCompositionTests.TheChildrenResolveANoseAtEveryFacingHeading` and
  `TheChildNosePushMovesNoAdultsFace`: job 4's measure, run in the editor's V8.
