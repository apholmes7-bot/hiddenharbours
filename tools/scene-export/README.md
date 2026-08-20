# Scene export — the repo's authored regions, in the scene editor's format

Exports **Nine Mile Creek** and **St Peters** out of the committed repo and into a
`hiddenharbours.scene/1` document, so the owner can open his own harbours in the Claude-Design
scene editor and look at them. The format is the one settled in
[`docs/tools/scene-export-contract.md`](../../docs/tools/scene-export-contract.md) — every field
name there is a citation from the editor's own reference package, not a guess.

This is the **outbound** direction only. Import — the editor authoring back into the repo — is
a separate, gated spike (`docs/tools/scene-editor-review.md` §9) and nothing here builds it.

```bash
python3 tools/scene-export/hh_scene_export.py                 # writes tools/scene-export/packages/
python3 tools/scene-export/hh_scene_export.py --check         # fails if the committed packages are stale
python3 -m unittest discover -s tools/scene-export/tests -v   # 33 tests
```

No arguments needed and no Unity: python3 (3.8+), standard library only. It runs in a bare
container in about seven seconds.

## What it reads, and from where

Every fact is taken from its **source of truth**, and the one fact that has none available
outside Unity is stamped as such rather than quietly trusted.

| Fact | Read from | Why there |
|---|---|---|
| Region size, centre, id, name | `Assets/_Project/Data/Regions/*.asset` | The `RegionDef` is the region table. The review found the editor's own copy wrong for two of three regions (§6.2), including one that only exists as a C# field default. |
| Sprite cell + pivot | the sheet's `.png.meta` import settings | The baker already resolved each rig's anchor into the import settings, so `unityPivot` is read, never re-derived — which sidesteps the review's §8.1 defect entirely (see below). |
| Rig identity | `docs/art/rigs/**` bytes, LF-normalised sha256 | The road kit's convention, reproduced exactly: `tr -d '\r' | sha256sum`. |
| Sheet → rig link | the sidecar JSON beside each baked sheet | Committed data, so no `family → filename` table of ours can drift (review §6.3). |
| Painted height | the `PaintedHeightMap` asset + the Git LFS pointer's `oid` | The texture's bytes are an LFS object, absent from a plain checkout. The pointer's oid pins it exactly without them. |
| Placements | the committed `.unity` | **A derived copy.** Both builders `NewScene(EmptyScene)` and rebuild from zero, so the source of truth is builder C#, which cannot run outside Unity. The package stamps which commit the scene was banked at and how many builder commits have landed since. |

## Two things worth knowing about the output

**The pivots are better than the editor's own.** The review's §8.1 found the editor falls back
to the cell box for rigs that publish their pivot per render (a measured 10 px error on the
rock). That failure mode cannot occur here: the pivot comes from the import settings the baker
wrote, in Unity's normalised bottom-left form, which *is* `unityPivot`. Both forms ship in
`cell` as the format wants, and the top-left one is derived from the normalised one under
ADR 0026, so the two cannot disagree. Every entity records `x-pivotSource`.

**Half the placements pin to a rig; the rest say so.** 11 kits ship a `*.contract.json` or an
equivalent sidecar naming the rig they were baked from, and those resolve exactly. Sheets with
no trustworthy sidecar — chiefly the hand-drawn wharf tilesets — resolve to nothing and are
listed by name under `x-provenance.entityNotes.unresolvedSheets`. Nothing is guessed: a
folder holding four per-hull anchor files and no index resolves a dory to *nothing* rather than
to whichever hull sorts first.

## Layout

```
hh_scene_export.py          CLI. --out, --region, --check.
hhexport/unityyaml.py       Unity Force-Text YAML reader (stdlib; keeps every scalar a string)
hhexport/repo.py            GUIDs, sprite import settings, region defs, rig resolution + sha256
hhexport/scene.py           hierarchy, world transforms, the scene's own ordering
hhexport/package.py         the hiddenharbours.scene/1 emitter
hhexport/provenance.py      what vintage of the world a package is a picture of
packages/                   the committed output (regenerate with the command above)
tests/                      33 tests: parser, rig pinning, the contract compared block-for-block
                            against docs/tools/reference/sample-scene.json, portability, determinism
```

The YAML reader is hand-written rather than PyYAML on purpose. Unity serialises `int[]` as a
bare hex blob, and a general YAML 1.1 loader reads an all-digit one as **octal** — `_viaStart:
0000000002000000` comes back as an integer with the bytes destroyed. Keeping every scalar a
string until something asks for a number makes that class of bug impossible, and drops the
dependency. It is cross-checked against PyYAML over both scenes: 7,086 documents, and the only
differences are places where PyYAML is the one that is wrong.

## Determinism

Same commit in, byte-identical package out — no timestamps, no run ids, no dictionary-order
dependence (entities follow the scene's own `SceneRoots` walk; rigs and paths are sorted).
`--check` re-derives and compares without writing, so a stale committed package fails a test
rather than surfacing as a wrong-looking harbour. `DeterminismTests` pins both halves.
