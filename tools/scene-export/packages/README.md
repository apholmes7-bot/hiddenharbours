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

These are pictures of the regions **as they were last banked**. At this regeneration (2026-09-07) St Peters had
just been re-banked at `0c7c03d8` (#764, the east door) and reads
`x-provenance.builderDrift.builderCommitsSinceScene` 0; Nine Mile Creek is still on its 2026-09-06 bank at
`31f0d08a` and **reads 1, which is `117174fe`** (#765, the sea climbing the quay) — the very commit whose law
§9 of the contract now states. ⚠ Drift is measured per region, against that region's own builder glob
(`provenance.BUILDER_GLOBS`), which is why #764 moved St Peters' number and left Nine Mile Creek's package
byte-identical: a package does not go stale every time an unrelated commit lands. That is why no
course of quay face in the committed scene carries a `TidalFaceWaterline`, and why every number under
`x-tidalFace` is resolved from the placement rather than read off a component. That number is the one to trust,
not this paragraph: the scenes are builder output that somebody ran and committed, the builders keep moving,
and each package counts and names every builder commit that has landed since its scene was banked. When it is
non-zero, re-bank the scenes in Unity and re-run the exporter.

`x-provenance.historyIsComplete` says whether the checkout could see far enough back to be sure
of those numbers: in a shallow clone the drift count is a floor, and the package says so.

## The tide (owner ruling 2026-09-07)

Each package now carries the terms for **water at any state of the tide** — the owner's *"ok yes i want faces
and hulls to ride it too"*. Four keys, and the full account is
[`docs/tools/scene-export-contract.md` §9](../../../docs/tools/scene-export-contract.md):

* **`x-tideRules`** — the three laws as strings plus `heightScale` (0.766), so nothing is re-derived from
  prose. `seaLevel = waterLevelMeters + amplitudeMeters * carrier`; water is `groundElevation < seaLevel`;
  a face's waterline and a hull's rise both scale by `heightScale`.
* **`terrain.x-heightFieldFull`** — the ground at **1 m**, one sample per terrain cell, run-length encoded in
  metres on the same datum as `waterLevelMeters`. This is the field you threshold. `§8.4`'s 8 m field stays
  for readers that only want to shade. 51,585 runs at Nine Mile Creek, 19,053 at St Peters.
* **`x-tidalFace`** — on each of Nine Mile Creek's 24 courses of quay face: 19 carry a lip and a foot, 5 are
  `null` because a north–south run has no drawn face at this camera. St Peters cuts none and says why.
* **`x-tidalRide`** / **`x-tidalHulls`** — every declared hull's draught and bed. Most of them are not
  entities: `MooredBoat` draws at runtime, so the seven boats at the wall and the harbour float carry no
  sprite in the scene at all.

**Nothing here is an evaluated tide.** The exporter has no clock and rule 5 says the water level is recomputed
from `(worldSeed, gameTime)` and never stored, so the carrier is the reader's to choose. Applying the rules to
the package alone puts the five hulls at the north wall **0.298 units above the water's edge at spring low,
mean and spring high alike** — inside the 0.2–0.7 u that #765 measured in the game.

## The doors between regions (2026-09-07)

The entity list is a walk of `SpriteRenderer`s, so it never held a `RegionPassage` (a trigger box) or an
arrival point (a bare `Transform`) — **six doors were missing from these packages, including the east door
#764 had just built**. Two keys now carry them, the full account in
[`docs/tools/scene-export-contract.md` §10](../../../docs/tools/scene-export-contract.md):

* **`x-passages`** — every way out, with its position, the trigger band it fires on, the region it leads to
  (id **and** scene name) and the arrival key it asks for on the far side. `target.exportedHere` says
  whether that region is one of the two this export ships: Coddle Cove, West Water and East Water are all
  real places with real `RegionDef`s that are simply not pictured here.
* **`x-arrivals`** — every way in: each region's default arrival point, dock zone and disembark point, plus
  every named arrival with its key and resolved point.

St Peters' **`PassageToEastWater` at (356, 40)** now names `region.east_water` / scene `EastWater`, and the
anchor's named arrival **`east_water` at (316, 40)** is the far half of the same door. A door needs both
halves to be legible, which is why both keys ship. ⚠ An `arrivalKey` resolves against the TARGET region's
`x-arrivals` — a lookup that crosses a package boundary and is deliberately left to a reader holding both.

Fields prefixed `x-` are ours. That is allowed by the contract, not a liberty taken with it:
readers of `hiddenharbours.scene/1` must ignore unknown keys and `x-` is the reserved extension
prefix (`docs/tools/scene-export-contract.md` §0). Everything under one is a fact the repo can
state that the format does not name — chiefly provenance and staleness.
