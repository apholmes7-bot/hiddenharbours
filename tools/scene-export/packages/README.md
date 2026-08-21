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
| `NineMileCreek.scene.json` | `region.nine_mile_creek` | 291 | 0 | 7 |
| `StPeters.scene.json` | `region.st_peters` | 1028 | 10 | 7 |
| `MANIFEST.json` | — | sha256 of each package | | |

**The West Water is deliberately absent**: it is unbanked and awaiting rebuild, so there is no
committed scene to picture.

## Read this before judging what you see

These are pictures of the regions **as they were last banked** — `14b1987`, *"bank the owner's
two builds"*, 2026-08-13. The scenes are builder output that somebody ran and committed, and the
builders have moved on since. Each package counts and names every builder commit that has landed
since, under `x-provenance.builderDrift` — read the number there rather than one quoted here,
which would go stale the same way the scenes did.

Most visibly, Nine Mile Creek's roads, truck park, anchored fleet, fields and woods all landed
*after* the bank, so none of them is in the picture; neither region has its nav marks; and St
Peters has neither the dredged east berth nor the arrival that opens the game on it (#584 says
so in its own commit message — *"StPeters.unity must be rebuilt in a real editor"*). Re-bank the
scenes in Unity and re-run the exporter to close the whole gap at once.

`x-provenance.historyIsComplete` says whether the checkout could see far enough back to be sure
of those numbers: in a shallow clone the drift count is a floor, and the package says so.

Fields prefixed `x-` are ours. That is allowed by the contract, not a liberty taken with it:
readers of `hiddenharbours.scene/1` must ignore unknown keys and `x-` is the reserved extension
prefix (`docs/tools/scene-export-contract.md` §0). Everything under one is a fact the repo can
state that the format does not name — chiefly provenance and staleness.
