# Packages — generated, committed on purpose

Two `hiddenharbours.scene/1` documents, one per authored region, produced from the repo at the
commit named in each file's `x-provenance.sourceCommit`.

**Regenerate — never hand-edit:**

```bash
python3 tools/scene-export/hh_scene_export.py
```

An edit here is silently undone by the next run, and `--check` (which
`DeterminismTests.test_the_committed_artifacts_are_what_this_commit_produces` runs) will fail
until the files match what this commit produces.

| File | Region | Entities | Lanes | Rigs pinned |
|---|---|---|---|---|
| `nine-mile-creek.scene.json` | `region.nine_mile_creek` | 291 | 0 | 7 |
| `st-peters.scene.json` | `region.st_peters` | 1028 | 10 | 7 |
| `MANIFEST.json` | — | sha256 of each package | | |

**The West Water is deliberately absent**: it is unbanked and awaiting rebuild, so there is no
committed scene to picture.

## Read this before judging what you see

These are pictures of the regions **as they were last banked** — `14b1987`, *"bank the owner's
two builds"*, 2026-08-13. The scenes are builder output that somebody ran and committed; 17
commits have since changed the Nine Mile Creek builders and 18 the St Peters ones. Each package
lists them under `x-provenance.builderDrift`. Most visibly, Nine Mile Creek's roads, truck park,
anchored fleet, fields and woods all landed *after* the bank, so none of them is in the picture,
and neither region has its nav marks. Re-bank the scenes in Unity and re-run the exporter to
close that gap.

`x-provenance.historyIsComplete` says whether the checkout could see far enough back to be sure
of those numbers: in a shallow clone the drift count is a floor, and the package says so.

Fields prefixed `x-` are ours: the review that reconstructs this format
(`docs/tools/scene-editor-review.md`) does not name them, so they are marked rather than passed
off as part of the contract. See `docs/tools/scene-export-contract.md`.
