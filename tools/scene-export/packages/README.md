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
| `NineMileCreek.scene.json` | `region.nine_mile_creek` | 2757 | 0 | 12 |
| `StPeters.scene.json` | `region.st_peters` | 1330 | 11 | 10 |
| `MANIFEST.json` | — | sha256 of each package | | |

**The West Water is deliberately absent**: it is unbanked and awaiting rebuild, so there is no
committed scene to picture.

## Read this before judging what you see

These are pictures of the regions **as they were last banked**. At this regeneration (2026-09-07) both scenes
had been re-banked on 2026-09-06 — Nine Mile Creek at `31f0d08a`, St Peters at `00872ed6` — and
no builder commit had landed since either, so `x-provenance.builderDrift.builderCommitsSinceScene` reads 0 in
both. That number is the one to trust, not this paragraph: the scenes are builder output that somebody ran and
committed, the builders keep moving, and each package counts and names every builder commit that has landed
since its scene was banked. When it is non-zero, re-bank the scenes in Unity and re-run the exporter.

`x-provenance.historyIsComplete` says whether the checkout could see far enough back to be sure
of those numbers: in a shallow clone the drift count is a floor, and the package says so.

Fields prefixed `x-` are ours. That is allowed by the contract, not a liberty taken with it:
readers of `hiddenharbours.scene/1` must ignore unknown keys and `x-` is the reserved extension
prefix (`docs/tools/scene-export-contract.md` §0). Everything under one is a fact the repo can
state that the format does not name — chiefly provenance and staleness.
