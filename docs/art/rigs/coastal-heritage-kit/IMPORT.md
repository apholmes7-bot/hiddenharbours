# Coastal heritage pass — import record (2026-09-15)

What arrived, what was taken, and what was **measured**. Nothing below is read off the bundle's own
manifest or its verification files: every number came out of a live call through the repo's own
ClearScript V8 in a standalone harness, or out of the CI results XML. No Unity editor was opened for
any of it.

## The drop

`C:/hh-drops/2026-09-14-gpt-art/extracted/01-houses/`, read-only.
`revised/` is the complete standalone source bundle and is what was taken.
`patches/03-coastal-heritage.patch` is the delta from the 2026-09-13 review.
`proofs/`, `overview.png`, `cottage-before-after.png`, `manor-interior-before-after.png` and
`stair-proof.png` are evidence to read, not intake.

| file | verdict | action |
|---|---|---|
| `Art/manorIsoRig.js` | **NEW** — installs `ManorIso` | → `docs/art/rigs/manorIsoRig.js` |
| `Art/manorUnitIsoRig.js` | **NEW** — installs `ManorUnitIso` | → `docs/art/rigs/manorUnitIsoRig.js` |
| `Art/coastalPass.js` | **NEW** — installs `CoastalPass` | → `docs/art/rigs/coastalPass.js` |
| `Art/houseIsoRig.js` | changed — +21 lines, a new `entrance()` | replaces the repo copy |
| `Art/interiorIsoRig.js` | changed — +394 | replaces the repo copy |
| `Art/interiorPropRig.js` | changed — +387 | replaces the repo copy |
| `Art/wharfBuildingRig.js` | **byte-identical to `main`** | listed and hashed, no diff |
| 7 sidecars, 5 designs, 5 layouts, `stair-contracts.json`, `support.js`, `manifest.json` | intake | → `docs/art/rigs/coastal-heritage-kit/` |

Three of the four globals are unclaimed in this repo. `PropIso`, `HouseIso`, `InteriorIso` and
`WharfBuilding` already existed and are replaced in place.

## Gate 1 — the hashes, and the working-tree form

All seven rigs hash to the `rigHashes` the manifest pins, **LF-normalised**. They do not hash raw:
the drop ships LF, `core.autocrlf=true` checks them out CRLF, and a raw hash of the working tree
would miss on every file.

`.gitattributes` now pins the kit folder and the seven rigs to `text eol=lf`, so the manifest hashes
stay checkable in a working copy rather than only in the object store. Verified in the branch's
EditMode guard, which normalises before hashing and would fail if the attribute were dropped.

`wharfBuildingRig.js`: working-tree sha256 == `HEAD` sha256 == manifest hash
`76dbecb0e1ccadfbcacc66b9ec5787d2ebff8112b7d5976668fa953a08ed8163`. The seventh source is a
signature member with no content change.

## Gate 2 — the load order is real, and it fails silently

`ManorUnitIso` reads shell geometry off `ManorIso`. Measured in the harness:

| install order | `ManorUnitIso.dims(preset)` |
|---|---|
| `ManorIso` then `ManorUnitIso` | `{Wd: 13.25, Ln: 17.50, topZ: …}` |
| `ManorUnitIso` alone | `{Wd: 0, Ln: 0, topZ: 0}` — **no throw** |

So the repo asserts the order **by consequence** rather than by inspecting an install list: a test
that reads the catalog's prerequisite array would still pass if `InstallPrerequisites` stopped
honouring it.

`CoastalPass` needs `PropIso` ahead of it, and is given `interiorProp` as a prerequisite and
deliberately **not** `house`: `house -> coastalPass -> house` is a cycle and
`InstallPrerequisites` has no cycle guard.

## Gate 3 — a reader in the bake path stopped meaning what it said

This is the one that would have shipped silently.

`InteriorRigBaker` asked each interior rig for its storey height with `<rig>.anchors(0, opts).storeyZ`.

| | `storeyZ` means | value on a domestic ground room |
|---|---|---|
| the old rigs | `ceilZ + joistZ`, floor to floor | 3.145 |
| this pass | `fH`, the ground plate | **0.55** |

The next bake through the old reader would have written 0.55 over 3.1025 and **nothing would have
thrown**. The upper storey collapses into the ground floor and the stairs end in a ceiling.

Fixed by reading the rise as the difference between storeys, guarded by
`typeof <rig>.dims === 'function'` so a rig without `dims` keeps its old reading (C# `&&`
short-circuits, so the difference is never evaluated for those). Four independent expressions agree
on each rise to the last digit — the floor-to-floor difference, `stair.floorRise`, `stair.top.z`, and
`steps × riser`: sageCottage **2.65**, school **2.59**, redSaltbox **2.74**, whiteFarmhouse **2.92**.

Also measured, and worth knowing: the companion ON *lowers* the ground plate (2.41 against 2.7625 on
sageCottage) to tuck it under the slab above. `fH + plate + slabThickness` equals
`dims(upper).storeyZ` exactly on all four rooms. **The pass is what makes the two storeys stack.**

## Gate 4 — the opt-out is aesthetic only

Charter rule 3 verified rather than quoted. With `CoastalPass.enabled = false`: rise, `floorRise` and
step count **unchanged to the last digit** on all four rooms. Only the plate moves
(`plateOff > plateOn`). The look is revertible; the stairs are not.

## Gate 5 — the bundle's own checks, run from a copy

Never from the read-only drop.

- `package-pass.cjs` — "Seven source/sidecar pairs verified; five hashed layout proposals saved."
- `verify-stairs.cjs` — `{"manorFlights":32,"pairedCottageCases":5,"allPassed":true}`
- `verify-dramatic.cjs` — **crashes** on this bundle: `Cannot convert undefined or null to object`.
  It expects a sibling review folder the drop does not ship. Replaced with a cell-by-cell render
  reconcile: **208 of 208 render cells identical**.
- `verification.json` and `stair-verification.json` both reproduce **byte-identical** to the copies
  the drop shipped.

⚠️ These are **source geometry** checks. Not a Unity playtest, not a building-code certification.
Roof pitches or sizes beyond the checked presets need validating again.

## Gate 6 — what the intake broke, measured against CI

Run 35047910987, EditMode: **12 failed**. Streamed out of the 3 MB artifact zip by zip-member read
plus `iterparse`; the 245 MB PlayMode sibling was never downloaded.

| failures | cause | disposition |
|---|---|---|
| 2 `RigCatalogAssemblyTests` | the ledger did not know the three new keys, and `house` prerequisites changed | bookkeeping, done |
| 2 `BuildingLifecyclePassTests.house_*` | the **test's own** hook-strip helper restored `const` on bindings that `houseIsoRig.js:891` still assigns | test fix, done |
| 6 on the door anchor | `HouseIso.anchors().door` left the +Y gable | **finding — needs a ruling** |
| 1 `AMisspelledKeyChangesNothing…` | the `floor` option stops applying with the companion on | **finding — needs a ruling** |
| 1 `TheRoomDeclaresHowFarItIsToTheFloorAbove…` | `anchors().roomH` is gone; contract stale | needs a re-bake |

**Re-measured on `5e75036c`** (run 35053588764), after the four fixes and the package re-pin:
EditMode **12740 total / 12482 passed / 8 failed / 250 skipped**; PlayMode **894 / 854 / 0 failed /
40 skipped**; the scene-export job **green**. The eight are exactly the residue above — six on the
door anchor (the probe refusal on `EveryRoomCropsSmallEnoughToPackUnderTheImportCap`,
`TheCropMovesThePivotWithIt…` and `TheShellsDoorIsOnPlusY…`, the guard
`TheRigStillRoutesItsDoorTheWayTheGableRuleAssumes`, and the registration pair
`TheRoomStandsUnderItsShellAtAFacingOffsetOfFour` /
`TheBuildingProbeWouldMeasureTheRoomBackwards…`), one on the `floor` option, one on the missing
`roomH`. **Skipped is unchanged at 250 and PlayMode is unmoved from the 09-15 stamp** — nothing was
silenced and nothing else moved.

### The door anchor was wrong on `main` before this drop

Measured `HouseIso.anchors(dir, opts).door` travel between dir 0 and dir 4, on this branch:

| build | travel | anchor sits on |
|---|---|---|
| (default) | **0.0 px** | +X eave |
| shingleCottage | 156.9 px | +Y gable |
| whiteFarmhouse | 376.2 px | +Y gable |
| redSaltbox | **0.0 px** | +X eave |
| gothicRevival | 213.1 px | +Y gable |
| dormerCape | **0.0 px** | +X eave |

On `main` the anchor was `pj(0, b.Ln/2, b.fH+1.0)` — the +Y gable, unconditionally — while the same
file **drew** the door on the +X eave whenever `isCape || (!hasPorch && shape!=='ell')`. For three of
six builds the label, glow and interaction anchor pointed at a blank wall, and the interior
registration scheme (facing offset of 4; "shell door +Y, room door −Y") was calibrated against that
lie. The new `entrance()` mirrors `build()`.

Accepting the fix means re-registering placed interiors against a door that moved 90°, which is
owner-visible and touches St Peters. **Not decided in this lane.**

### The `floor` option

| render | CoastalPass ON | CoastalPass OFF |
|---|---|---|
| `InteriorIso.render(0,{floor:'stoneFlag'})` | `f842d004d522` | `b00f7c064f79` |
| `InteriorIso.render(0,{})` | `f842d004d522` | `807a3f1e155c` |

`FLOORS` is unchanged in both revisions — five finishes still declared, one rendered.

The mechanism: `coastalPass.js` `cottage()` drops every face on the floor layer (`line 241`) and then
lays its own fixed bands (`line 246`, `tile` hall + `oak` parlour on the ground, `oak` upstairs). The
host rig still honours `floor` and renders it faithfully; the companion discards the result. That is
a deliberate authored floor, not an accident — but it leaves five documented options inert and
anything setting `floor:` silently ignored. Either derive the band palette from `b.floor`, or take
`floor` off the option surface while the pass is on. Reported rather than patched; it is the pass's
design call.

## What is not here

The sprite bake, the regenerated room contract, manor stair colliders, floor openings, transition
targets, the cottage StairUp/StairDown fixtures, and the PlayMode walk. All need the Unity editor,
which is a single machine-wide slot this lane does not hold.

And the manors themselves: `grep -ri manor Assets docs` outside this branch returns two hits, both
the word inside one comment. **No manor sprite, prefab or scene placement exists in this repository.**
The colliders and transition targets are not stale — they have never existed, and a manor has no site
in any region.
