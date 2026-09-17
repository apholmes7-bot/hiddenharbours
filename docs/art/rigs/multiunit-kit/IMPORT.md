# Multi-unit building kit — import record

How the art director's multi-unit building rigs came into `docs/art/rigs/`, and every number measured on the way in. The
base is `main` at `bd5d28b2` (#854). Everything below ran headless from the repository root, on node v24.19.0 and Python
3.14.6; no Unity editor was opened. The scripts are in §11 as they ran. Each number here was printed by one of them or
read from the landed bytes, and checked against the others before this file was written.

1. [The drop](#1-the-drop)
2. [`SHA256SUMS.json`](#2-sha256sumsjson)
3. [The five hashes](#3-the-five-hashes)
4. [The generator, re-run on node](#4-the-generator-re-run-on-node)
5. [What the contract and the stamps cannot see](#5-what-the-contract-and-the-stamps-cannot-see)
6. [Paths the move changed](#6-paths-the-move-changed)
7. [What the drop's prose says that its bytes do not](#7-what-the-drops-prose-says-that-its-bytes-do-not)
8. [`interiorPropRig.js` replaces main's copy](#8-interiorproprigjs-replaces-mains-copy)
9. [Not in this change](#9-not-in-this-change)
10. [Re-checking](#10-re-checking)
11. [The scripts](#11-the-scripts)

## 1. The drop

|  |  |
|---|---|
| File | `Pixel art capabilitiesmultiunitrigexport.zip` |
| sha256 | `b4140d7bbfb619fd90bbea0e386007b8b55b73312cac4746daf50ca1fc8a511e` |
| Size | 608,431 B |
| Entries | 47 files and no directory entries, all under `export/multiunit-rig-export/`, all dated 2026-09-17 00:36:52, all deflated; no entry holds a CR byte |
| Kits | `rowhouse-rig-kit/`, `walkup-rig-kit/`, `stack-rig-kit/`, beside a root `README.md` |

The zip was re-hashed before its unzipped copy was trusted, and the copy matched it name for name and byte for byte.

### Where the 47 files went

| In the drop | Files | Landed | Now, under `docs/art/rigs/` |
|---|---:|---:|---|
| `README.md` at the root | 1 | 0 | not landed: an index of the three kits, read in §7 |
| `CONTRACT.md`, one per kit | 3 | 1 | `multiunit-kit/CONTRACT.md` |
| `rigs/interiorPropRig.js`, one per kit | 3 | 1 | `interiorPropRig.js`, replacing main's copy (§8) |
| `rigs/_multiunitKit.js`, `_buildingGameplay.js`, `_interiorPlacer.js`, `_sidecarExport.js`, per kit | 12 | 4 | `multiunit-kit/shared/` |
| the kit's own two rigs, shell and rooms | 6 | 6 | `multiunit-kit/<phase>/` |
| `README.md` and `SHA256SUMS.json`, per kit | 6 | 6 | `multiunit-kit/<phase>/` |
| `gameplay/*.gameplay.json` | 16 | 16 | `multiunit-kit/<phase>/gameplay/` |
| **total** | **47** | **34** |  |

Each file that comes once per kit (the contract, the prop rig, the four shared modules) is in the drop as three
byte-identical copies, and one of them landed. Every landed file equals its zip entry byte for byte. Two files in
`multiunit-kit/` are not from the drop: its `README.md` and this record.

## 2. `SHA256SUMS.json`

Each kit's `SHA256SUMS.json` (`generatedAt` 2026-09-17T00:30:31.333Z, `algorithm` `sha256`) lists every file in its kit
but itself, by its path inside the kit. Its note reads: "Every file in this kit except this one. Rehash and compare to
verify a sidecar stamp and its source agree."

The layout moved some of those paths (§6), so each name was resolved to where it landed and hashed there, over the
landed bytes, which are LF: `CONTRACT.md` to `multiunit-kit/CONTRACT.md`, `rigs/interiorPropRig.js` to
`interiorPropRig.js`, the four `rigs/_*.js` to `multiunit-kit/shared/`, the kit's two rigs from `rigs/` into the phase
folder, and `README.md` and `gameplay/` into the phase folder as they were.

| Kit, now | Entries | Equal to the landed file |
|---|---:|---:|
| `rowhouse/` | 12 | 12 |
| `walkup/` | 15 | 15 |
| `stack/` | 16 | 16 |

## 3. The five hashes

Every sidecar carries five stamps, each the sha256 of one file it was generated from. All 80 (16 sidecars × 5) equal the
sha256 of the landed file, over its LF bytes. The table has nine rows because the shell and rooms stamps name each
phase's own rigs, while the props, placer and writer stamps name one file shared by all 16:

| Stamp | File, under `docs/art/rigs/` | Bytes | sha256 | Sidecars |
|---|---|---:|---|---:|
| `derivedFromRigSha256` | `multiunit-kit/rowhouse/rowhouseIsoRig.js` | 56,784 | `ff74001f41a8af068ca4e544a28bbea4fe2a54a758d146298d20dbc56db7059c` | 3 |
| `derivedFromRigSha256` | `multiunit-kit/walkup/walkupIsoRig.js` | 83,311 | `b7c2936781e1719994d2676e92b725626c225f7c388d04a9243474cb17901604` | 6 |
| `derivedFromRigSha256` | `multiunit-kit/stack/stackFlatsIsoRig.js` | 87,896 | `8f92d85d9894bd4cdd87a31682a84bbe49e87916d76580837794b5f7ae13dccd` | 7 |
| `interiorDerivedFromRigSha256` | `multiunit-kit/rowhouse/rowhouseUnitIsoRig.js` | 75,212 | `41b8692001c4ad0dcc3defa5cb00f8d63e20b9713f1c8e0c20887b97909e58c1` | 3 |
| `interiorDerivedFromRigSha256` | `multiunit-kit/walkup/walkupUnitIsoRig.js` | 59,661 | `987df029405b0e4aa71b4ef7d4e78b1e338f55652342ab6ff22e0e67c362aab9` | 6 |
| `interiorDerivedFromRigSha256` | `multiunit-kit/stack/stackUnitIsoRig.js` | 59,861 | `a2cfaba01a95ad99cba3b3bc319ee61e8d4e25913234b57e121abcc787a5198e` | 7 |
| `propsDerivedFromRigSha256` | `interiorPropRig.js` | 63,049 | `a6c880e159d355ded27d2202fa8f64577a0aae35829987a2c9643dda183f14a6` | 16 |
| `placerDerivedFromRigSha256` | `multiunit-kit/shared/_interiorPlacer.js` | 14,998 | `ccf6c302b458d7f1b8f0b66bf0805da13fe51421e95a13e6a27174d534bf2206` | 16 |
| `writerDerivedFromRigSha256` | `multiunit-kit/shared/_buildingGameplay.js` | 13,374 | `49594e4c6a6a9b3fae82d630803b465fb90d2a96b9687e5375a2eb750824e8a8` | 16 |

The terrace's three sidecars carry the placer stamp too, although the terrace's rooms rig never names the placer (§5, §7).
A stamp is taken from the bytes on disk when the exporter saves; §5 shows what that does and does not prove.

No stamp names these three, so no stamp moves when one of them changes:

| File, under `docs/art/rigs/` | Bytes | sha256 |
|---|---:|---|
| `multiunit-kit/shared/_multiunitKit.js` | 10,343 | `10943e19d2f32e0a77bff64d76beb936974d5126921ad9bc01ff7510141268a0` |
| `multiunit-kit/shared/_sidecarExport.js` | 4,184 | `8802ab1152d163e69ac1f3ef0f02b801b0924f5ccf07f3bc767d6873f4ed2b9d` |
| `multiunit-kit/CONTRACT.md` | 3,723 | `8fddf36d1a5a3b0a266ab1a039f362915f83063ffb86b88700401735d704f999` |

### What every sidecar states

Read from the 16 landed sidecars:

- `schema` is `hidden-harbours/building-gameplay@1`. The first 15 keys run from `schema` to `frame` and the last four
  are `reach_audit`, `_interiorNote`, `_excluded` and `_confirm`, in that order, in all 16.
- `frame` is in metres at 32 px per metre, heading-independent, and the same for every sidecar of a phase.
- `UNITS` has `build.units` entries: 97 dwellings in all, each with `resident_slots` equal to its number of
  `sleep_anchors`, and at least 1.
- 2,189 `INTERACT` entries, each with `reach.tested`, a verdict and a point. Each `reach_audit` counts all of its
  sidecar's entries, its `ok + moved + flipped + nulled` equals `checked`, and `nulled` is 0.
- `_unplaced` appears in none.
- 48 `STAIRS`: 34 interior flights, 7 `entry_steps` and 7 `service_stair`, which nest 18 more flights in `flights[]`.
  For all 59, `exact.product` equals `floor_rise_m` and `steps × rise_m` is within 1e-9 of it. Every interior flight has
  both landings and a 4-point `void_above`.
- `steps × rise_m` as a double equals `floor_rise_m` exactly in only 32 of the 59; the worst misses by 4.4e-16. A check
  has to compare within a tolerance, and has to look inside a service stair's `flights[]`.

| Phase | `rig` | `interiorRig` | `exportSymbol` | `generator` | `phase` |
|---|---|---|---|---|---|
| `rowhouse/` | `Art/rowhouseIsoRig.js` | `Art/rowhouseUnitIsoRig.js` | `globalThis.RowhouseIso` | `RowhouseIso.gameplayAll(opts)` | multi-unit phase 1 — the terrace |
| `walkup/` | `Art/walkupIsoRig.js` | `Art/walkupUnitIsoRig.js` | `globalThis.WalkupIso` | `WalkupIso.gameplayAll(opts)` | multi-unit phase 2 — the walk-up block |
| `stack/` | `Art/stackFlatsIsoRig.js` | `Art/stackUnitIsoRig.js` | `globalThis.StackFlatsIso` | `StackFlatsIso.gameplayAll(opts)` | multi-unit phase 3 — the three-decker stack |

`propRig` is `Art/interiorPropRig.js` in all 16. The `Art/` prefix is the layout of the project the rigs came from (§6).

| Phase | `frame.cell`, px | Pivot, px |
|---|---|---|
| `rowhouse/` | 1320 × 1160 | (660, 745) |
| `walkup/` | 1600 × 1560 | (800, 940) |
| `stack/` | 1280 × 1320 | (640, 960) |

| Phase | Sidecars | Keys between `frame` and `reach_audit`, in order |
|---|---:|---|
| `rowhouse/` | 2 | `building` `heights` `UNITS` `THRESHOLD` `MAIL` `WALK` `BLOCKERS` `ROOF` `SOLE` `STAIRS` `INTERACT` |
| `rowhouse/` | 1 | `building` `heights` `UNITS` `THRESHOLD` `WALK` `BLOCKERS` `ROOF` `SOLE` `STAIRS` `INTERACT` |
| `walkup/` | 6 | `building` `heights` `UNITS` `THRESHOLD` `ELEVATOR` `SHARED` `WALK` `BLOCKERS` `ROOF` `MAIL` `SOLE` `STAIRS` `INTERACT` |
| `stack/` | 7 | `building` `heights` `UNITS` `THRESHOLD` `SHARED` `PORCH` `BLOCKERS` `ROOF` `SOLE` `STAIRS` `INTERACT` |

## 4. The generator, re-run on node

`_multiunitKit.js` is the drop's generator. `MultiunitKit.contract()` runs the 22 assertions of `CONTRACT.md` over 112
configurations, and `MultiunitKit.all()` writes the 16 sidecars. Its header (:23) says to run it from a page with the
rigs loaded, not on node (§7). The harness in §11, `multiunit-regen.mjs`, runs it on node. It loads the eleven scripts
into one `vm` context with `window` set to the global object, in this order: `interiorPropRig.js`, `_interiorPlacer.js`,
`_buildingGameplay.js`, `rowhouseUnitIsoRig.js`, `walkupUnitIsoRig.js`, `stackUnitIsoRig.js`, `rowhouseIsoRig.js`,
`walkupIsoRig.js`, `stackFlatsIsoRig.js`, `_sidecarExport.js`, `_multiunitKit.js`. It supplies `fetch`, the one browser
API on the generator path: `SidecarExport.rigSha256` fetches a rig to hash it, and the harness answers each URL with the
file of the same name. Then it calls `contract()` and `all()` and compares every sidecar `all()` returns with the
committed file, first as bytes, then as parsed JSON, key by key. If `all()` throws, it asks `MultiunitKit.sidecar()` for
each preset instead, so that one broken phase does not hide the others.

The four runs, from the repository root, with `multiunit-regen.mjs` saved from §11:

```
node multiunit-regen.mjs drop <unzipped>/export/multiunit-rig-export
node multiunit-regen.mjs repo docs/art/rigs
node multiunit-regen.mjs repo docs/art/rigs --omit interiorPropRig.js
node multiunit-regen.mjs repo docs/art/rigs --omit _interiorPlacer.js
```

Run 1 read the kits as unzipped: each kit's own two rigs from its `rigs/`, the prop rig and the four shared modules from
`rowhouse-rig-kit/rigs/` (the three copies are identical, §1), and the committed sidecars from each `gameplay/`. Runs 2 to
4 read `docs/art/rigs/` as landed. In all four, the harness printed the size and the first 12 hex digits of the sha256
of each script it loaded, and each matched the landed file.

| Run | Loaded | Scripts | `contract()` checks / failed | `all()` | Sidecars written | Byte-identical | Parsed-equal |
|---:|---|---:|---:|---|---:|---:|---:|
| 1 | the drop, as unzipped | 11 | 2,464 / 0 | 32 entries, its own contract 2,464 / 0 | 16 | 16 | 16 |
| 2 | the landed files | 11 | 2,464 / 0 | 32 entries, its own contract 2,464 / 0 | 16 | 16 | 16 |
| 3 | the landed files but `interiorPropRig.js` | 10 | 2,464 / 0 | 32 entries, its own contract 2,464 / 0 | 16 | 0 | 0 |
| 4 | the landed files but `_interiorPlacer.js` | 10 | 1,582 / 42 | threw `Cannot read properties of undefined (reading 'clearFor')` | 10 | 3 | 3 |

In runs 1 and 2 all 16 sidecars came out byte-identical to the committed files, with the same top-level key order and
the same five stamps. Runs 3 and 4 are the controls in §5. The script as printed in §11 is the Phase A harness with
five lines added after its line 44, which tally the contract's configurations and failures by phase. Phase A ran it
without them.

`MultiunitKit.configurations()` lists 112: the walk-up has 6 presets and 36 grid configurations (42), and the stack
has 7 and 24 (31). The terrace has its 3 presets under the phase `rowhouse` and its 36 grid configurations under the
phase `terrace`. `CONTRACT.md`:55-57 counts the terrace as 3 + 36 = 39, so the totals agree. `pairs.mjs` (§11) shows
that `all()`'s 32 entries are the 16 sidecars, each under two keys, `Art/gameplay/<phase>/` and
`export/<phase>-rig-kit/gameplay/`, and that the two strings are equal.

What each committed sidecar holds. Runs 1 and 2 printed the same dwellings, resident slots and entry counts for all 16:

| Preset | Phase | `variant` | `width_m` × `depth_m` | Dwellings | Resident slots | `SOLE` | `THRESHOLD` | `STAIRS` | `INTERACT` | `BLOCKERS` | `reach_audit`: checked / ok / moved / flipped / nulled |
|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| `millRow4` | rowhouse | `basic_4u_2bed_2st` | 22.4 × 10.6 | 4 | 12 | 32 | 28 | 4 | 72 | 196 | 72 / 36 / 20 / 16 / 0 |
| `duplexPair` | rowhouse | `basic_2u_3bed_2st` | 12.4 × 10.6 | 2 | 8 | 16 | 14 | 2 | 38 | 98 | 38 / 20 / 14 / 4 / 0 |
| `coastalRow4` | rowhouse | `luxury_4u_3bed_3st` | 28 × 11.6 | 4 | 24 | 64 | 56 | 8 | 154 | 359 | 154 / 94 / 54 / 6 / 0 |
| `walkup8` | walkup | `basic_8u_2bed_2st` | 22.6 × 18.9 | 8 | 24 | 75 | 82 | 1 | 137 | 320 | 137 / 122 / 13 / 2 / 0 |
| `walkup12` | walkup | `basic_12u_2bed_3st` | 22.6 × 18.9 | 12 | 36 | 112 | 122 | 2 | 202 | 474 | 202 / 181 / 18 / 3 / 0 |
| `walkup12Fam` | walkup | `basic_12u_3bed_3st` | 28.2 × 18.9 | 12 | 48 | 132 | 142 | 2 | 306 | 666 | 306 / 178 / 92 / 36 / 0 |
| `walkup4` | walkup | `basic_4u_1bed_2st` | 11.7 × 18.9 | 4 | 8 | 35 | 38 | 1 | 61 | 136 | 61 / 40 / 13 / 8 / 0 |
| `cannery12` | walkup | `luxury_12u_2bed_3st` | 24.7 × 20.9 | 12 | 48 | 124 | 134 | 2 | 307 | 619 | 307 / 156 / 114 / 37 / 0 |
| `cannery8Loft` | walkup | `luxury_8u_3bed_2st` | 30.3 × 20.9 | 8 | 32 | 88 | 95 | 1 | 211 | 439 | 211 / 136 / 48 / 27 / 0 |
| `decker6` | stack | `porch_6u_2bed_3st_2pf` | 17.6 × 12.2 | 6 | 18 | 54 | 77 | 4 | 125 | 248 | 125 / 97 / 25 / 3 / 0 |
| `decker3` | stack | `porch_3u_3bed_3st_1pf` | 11.4 × 12.2 | 3 | 18 | 36 | 47 | 4 | 71 | 149 | 71 / 55 / 13 / 3 / 0 |
| `decker4` | stack | `porch_4u_2bed_2st_2pf` | 17.6 × 12.2 | 4 | 12 | 36 | 52 | 3 | 84 | 168 | 84 / 65 / 17 / 2 / 0 |
| `deckerMixed` | stack | `porch_6u_2bed_3st_2pf` | 17.6 × 12.2 | 6 | 16 | 54 | 77 | 4 | 129 | 258 | 129 / 95 / 25 / 9 / 0 |
| `captains6` | stack | `gambrel_6u_2bed_3st_2pf` | 18.85 × 12.9 | 6 | 18 | 54 | 77 | 4 | 144 | 251 | 144 / 95 / 43 / 6 / 0 |
| `captains2` | stack | `gambrel_2u_3bed_2st_1pf` | 12.15 × 12.9 | 2 | 12 | 24 | 32 | 3 | 51 | 98 | 51 / 40 / 11 / 0 / 0 |
| `captains4` | stack | `gambrel_4u_2bed_2st_2pf` | 18.85 × 12.9 | 4 | 12 | 36 | 52 | 3 | 97 | 170 | 97 / 64 / 29 / 4 / 0 |
| **all 16** |  |  |  | **97** | **346** | **972** | **1,125** | **48** | **2,189** | **4,649** | **2,189 / 1,474 / 549 / 166 / 0** |

## 5. What the contract and the stamps cannot see

Runs 3 and 4 are controls. Each loads every landed script but one and changes nothing else; the file left out stays on
disk. A cell reads resident slots · `INTERACT` entries · `BLOCKERS` entries, where resident slots is the sum of
`resident_slots` over `UNITS`:

| Preset | Committed | Run 3, no `interiorPropRig.js` | Run 4, no `_interiorPlacer.js` |
|---|---|---|---|
| `millRow4` | 12 · 72 · 196 | 0 · 0 · 92 | byte-identical |
| `duplexPair` | 8 · 38 · 98 | 0 · 0 · 46 | byte-identical |
| `coastalRow4` | 24 · 154 · 359 | 0 · 8 · 153 | byte-identical |
| `walkup8` | 24 · 137 · 320 | 0 · 9 · 132 | threw |
| `walkup12` | 36 · 202 · 474 | 0 · 14 · 196 | threw |
| `walkup12Fam` | 48 · 306 · 666 | 0 · 14 · 236 | threw |
| `walkup4` | 8 · 61 · 136 | 0 · 1 · 60 | threw |
| `cannery12` | 48 · 307 · 619 | 0 · 10 · 219 | threw |
| `cannery8Loft` | 32 · 211 · 439 | 0 · 5 · 149 | threw |
| `decker6` | 18 · 125 · 248 | 0 · 14 · 79 | 0 · 0 · 5 |
| `decker3` | 18 · 71 · 149 | 0 · 8 · 52 | 0 · 0 · 5 |
| `decker4` | 12 · 84 · 168 | 0 · 9 · 54 | 0 · 0 · 5 |
| `deckerMixed` | 16 · 129 · 258 | 0 · 14 · 79 | 0 · 0 · 5 |
| `captains6` | 18 · 144 · 251 | 0 · 14 · 77 | 0 · 0 · 3 |
| `captains2` | 12 · 51 · 98 | 0 · 5 · 34 | 0 · 0 · 3 |
| `captains4` | 12 · 97 · 170 | 0 · 9 · 52 | 0 · 0 · 3 |

Every sidecar the controls wrote kept the committed counts of `units`, `SOLE`, `THRESHOLD` and `STAIRS`, kept the
top-level key order, and carried the same five stamps as the committed file.

**Without `interiorPropRig.js`** the contract passed: 2,464 checks, 0 failed, and the same in the contract `all()` runs
itself. All 16 sidecars differ from the committed files. No dwelling in any of them has a resident slot, and `INTERACT`
and `BLOCKERS` are shorter in every one.

**Without `_interiorPlacer.js`** the three rooms rigs part ways:

- The walk-up's rooms rig takes the placer at :483 (`const PL = root.InteriorPlacer && root.InteriorPlacer.create(…)`)
  and calls `PL.clearFor(` at :545 without checking that `PL` exists. All 42 walk-up configurations threw
  `Cannot read properties of undefined (reading 'clearFor')`, `all()` threw the same, and so did
  `MultiunitKit.sidecar()` for each of the six walk-up presets. A configuration that throws counts as one check and one
  failure (`_multiunitKit.js`:118), so the other 70 configurations gave 70 × 22 = 1,540 checks and the 42 throws make it
  1,582, with 42 failed.
- The stack's rooms rig returns early at :489 when `PL` is missing
  (`if(!PL) return { faces:out, ctx, plan:planAll(b) };`), so its `PL.clearFor(` at :538 never ran. None of the 31 stack
  configurations failed. Its seven sidecars have no resident slot, no `INTERACT` entry and 3 to 5 `BLOCKERS`.
- The terrace's three sidecars came out byte-identical. `rowhouseUnitIsoRig.js` never names `InteriorPlacer`, yet all
  three carry `placerDerivedFromRigSha256` (§7).

| Rooms rig | Names `InteriorPlacer` | Without the placer | Names `BuildingGameplay` | `const BG=root.BuildingGameplay; if(!BG) return null;` |
|---|---|---|---:|---|
| `rowhouseUnitIsoRig.js` | 0 | nothing changed (run 4) | 1 | :1191 |
| `walkupUnitIsoRig.js` | 2, both on :483 | `PL.clearFor(` at :545 throws | 1 | :964 |
| `stackUnitIsoRig.js` | 2, both on :485 | returns early at :489 | 1 | :971 |

The shells and `interiorPropRig.js` name neither module. No run left `_buildingGameplay.js` out.

### What noticed what

| Left out | Phase | `contract()` | Stamps equal to the committed | Sidecars, regenerated vs committed |
|---|---|---|---|---|
| `interiorPropRig.js` | all three | 2,464 checks, 0 failed | 80 of 80 | 16 of 16 differ; no resident slot anywhere |
| `_interiorPlacer.js` | walk-up | all 42 configurations threw | none written | none written |
| `_interiorPlacer.js` | stack | 0 of 31 configurations failed | 35 of 35 | 7 of 7 differ; no resident slot, no `INTERACT` |
| `_interiorPlacer.js` | terrace | 0 of 39 configurations failed | 15 of 15 | 3 of 3 byte-identical |

A stamp is the sha256 of a file as it sits on disk when the exporter saves (`_multiunitKit.js`:20-21,
`walkup/README.md`:69-70). `SidecarExport.rigSha256` fetches the file to hash it, so a script that is on disk but never
ran is stamped the same as one that ran. The stamps show which bytes sat beside a sidecar when it was saved, not that
those bytes built it, and a clean contract does not show that any dwelling has a resident slot. What catches sidecars
like the ones these controls wrote is a check on what a sidecar states: every dwelling has a resident slot, and
`INTERACT` is not empty (§10).

## 6. Paths the move changed

The READMEs and `CONTRACT.md` landed verbatim, so they still describe the drop's layout: a contract at the top of each
kit and every script in the kit's `rigs/`. Here a kit's two rigs and its `gameplay/` are in `multiunit-kit/<phase>/`,
the one `CONTRACT.md` is in `multiunit-kit/`, the four shared modules are in `multiunit-kit/shared/`, and the prop rig
is `docs/art/rigs/interiorPropRig.js`. No file was edited to follow the move: the harness in §4 finds each script by its
file name, and §2 resolved each name in `SHA256SUMS.json` to where it landed.

The three phase READMEs quote 21 paths to files, and `CONTRACT.md` quotes none. Of the 21, 16 name a file that landed:

| Landed file, under `docs/art/rigs/` | Quoted as | Quoted at | In the drop, from the README's folder | Now, from the README's folder |
|---|---|---|---|---|
| `multiunit-kit/rowhouse/rowhouseIsoRig.js` | `rigs/rowhouseIsoRig.js` | `rowhouse/README.md`:14 | `rigs/rowhouseIsoRig.js`, as quoted | `rowhouseIsoRig.js` |
| `multiunit-kit/rowhouse/rowhouseUnitIsoRig.js` | `rigs/rowhouseUnitIsoRig.js` | `rowhouse/README.md`:15 | `rigs/rowhouseUnitIsoRig.js`, as quoted | `rowhouseUnitIsoRig.js` |
| `interiorPropRig.js` | `rigs/interiorPropRig.js` | `rowhouse/README.md`:16 | `rigs/interiorPropRig.js`, as quoted | `../../interiorPropRig.js` |
| `interiorPropRig.js` | `interiorPropRig.js` | `walkup/README.md`:63, `stack/README.md`:67 | `rigs/interiorPropRig.js` | `../../interiorPropRig.js` |
| `multiunit-kit/CONTRACT.md` | `CONTRACT.md` | `rowhouse/README.md`:74 | `CONTRACT.md`, as quoted | `../CONTRACT.md` |
| `multiunit-kit/shared/_interiorPlacer.js` | `_interiorPlacer.js` | `rowhouse/README.md`:85, `walkup/README.md`:64, `stack/README.md`:68 | `rigs/_interiorPlacer.js` | `../shared/_interiorPlacer.js` |
| `multiunit-kit/shared/_buildingGameplay.js` | `_buildingGameplay.js` | `rowhouse/README.md`:85, `walkup/README.md`:65, `stack/README.md`:69 | `rigs/_buildingGameplay.js` | `../shared/_buildingGameplay.js` |
| `multiunit-kit/shared/_sidecarExport.js` | `Art/_sidecarExport.js` | `walkup/README.md`:69, `stack/README.md`:73 | `rigs/_sidecarExport.js` | `../shared/_sidecarExport.js` |
| `multiunit-kit/shared/_multiunitKit.js` | `Art/_multiunitKit.js` | `walkup/README.md`:99, `stack/README.md`:102 | `rigs/_multiunitKit.js` | `../shared/_multiunitKit.js` |

Of these 16, 4 resolved as written in the drop and do not now. The other 12 give the file name alone or with `Art/`, and
did not resolve as written in the drop either.

The remaining 5 quotes name files that are not in the drop, and no file in the repository at `main` or in this change
has any of their names:

- `Art/_rowhouseKit.js` (`rowhouse/README.md`:31), the writer the terrace shipped with (§7).
- `Rowhouse Iso.dc.html` (`rowhouse/README.md`:79), `Rowhouse Interiors.dc.html` (`rowhouse/README.md`:80),
  `Walkup Iso.dc.html` (`walkup/README.md`:98), `Stack Flats Iso.dc.html` (`stack/README.md`:102), the builder pages.

### The `Art/` prefix

The 16 sidecars, 7 rigs, 4 shared modules and 3 READMEs name files as `Art/<name>`: 13 names, 156 mentions in 30 landed
files. In the kit's own code they are URLs: `_multiunitKit.js` hashes nine rigs by these names (:128-130), and
`_sidecarExport.js`'s `rigSha256` (:43) fetches what it hashes (:31). There is no `Art/` folder under `docs/art/rigs/`
or at the root of the repository, so here a name resolves only by its file name, as the harness in §4 resolved it.
Resolved that way:

- 10 are one landed file each: `_buildingGameplay.js`, `_interiorPlacer.js`, `_multiunitKit.js`, `interiorPropRig.js`,
  `rowhouseIsoRig.js`, `rowhouseUnitIsoRig.js`, `stackFlatsIsoRig.js`, `stackUnitIsoRig.js`, `walkupIsoRig.js`,
  `walkupUnitIsoRig.js`.
- `_sidecarExport.js` is three files under `docs/art/rigs/`: `gas-station-rig/tools/_sidecarExport.js`,
  `multiunit-kit/shared/_sidecarExport.js`, `sail-rig-kit/_sidecarExport.js`. All three are one git blob, `1a510c45`:
  the exporter the kit brought is the one already in the repository twice.
- `_rowhouseKit.js` is not here (§7).
- `someRig.js` is the placeholder in `_sidecarExport.js`'s usage comment (:13, :15, :16).

## 7. What the drop's prose says that its bytes do not

The READMEs and `CONTRACT.md` landed as they came, and nothing in them was corrected. Where their prose disagrees with
the files beside them, the files are what ran in §4 and what the sidecars stamp.

### Hashes that match no file

Four sha256 values quoted in the drop match none of the files hashed for this import: no zip entry, no landed file and
none of the 1,934 files under `docs/art/rigs/` at `main`, each hashed as it is, with LF line ends and with CRLF:

| Quoted at | For | Quoted sha256 | Landed file, under `docs/art/rigs/` | Its sha256 (§3) |
|---|---|---|---|---|
| `rowhouse/README.md`:6 | the terrace's shell | `1018d53f51ad6e97e77b513253c2fe7ea8a8e6f625222c1318eb9a8678795c93` | `multiunit-kit/rowhouse/rowhouseIsoRig.js` | `ff74001f` |
| `rowhouse/README.md`:7 | the terrace's rooms | `bd7222b69e7409f83c88da016544c0b1bb96a8eb3463ec8099c659eb6ad72ea5` | `multiunit-kit/rowhouse/rowhouseUnitIsoRig.js` | `41b86920` |
| `rowhouse/README.md`:8 | the prop rig | `bc361678ab4f6e7b2d63f04e34f5d54cd9b9696eb2afdeeaf397fba758469307` | `interiorPropRig.js` | `a6c880e1` |
| the root `README.md`:107 | the terrace's shell | `f436cad6407b4a5af061e8d9a5045ef98768d54d792aa5f93036a9137de1ad9a` | `multiunit-kit/rowhouse/rowhouseIsoRig.js` | `ff74001f` |

`rowhouse/README.md`:6-8 are the three hashes its section `## Why three hashes` (:27) explains. The same README
re-issues the terrace at :82 with five hashes (:86) and left the three lines at its top as they were. The root
`README.md` gives a shell and a rooms hash for each phase (:103-107); five of the six are the landed files' sha256, and
the terrace's shell is not. So the terrace's shell has three hashes in the drop: `1018d53f` (`rowhouse/README.md`:6),
`f436cad6` (the root `README.md`:107) and `ff74001f`, the sha256 of the landed file, which is what the terrace's three
sidecars and its `SHA256SUMS.json` carry.

### `millRow4` in the terrace's first table

`rowhouse/README.md`:38, in the table under `## Shipped terraces` (:34), gives `millRow4` 16 resident slots and 76
`INTERACT` entries; its committed sidecar has 12 and 72. The same README explains the change further down: a double bed
now earns a sleep anchor only on a side with floor to stand on (:89-90), `millRow4` goes from 76 to 72 and from 16 to 12
(:94), and of its 2.4 m back bedroom it says "its bed fits, and one side of it does not" (:98). The first table was not
updated. Of the 313 cells in the 35 rows of the drop's 7 preset tables, the root `README.md`'s three included, these 2
are the only ones that disagree with the sidecars.

### The terrace's check count

`rowhouse/README.md`:74-75 gives `CONTRACT.md` as 18 assertions × 48 configurations, 864 checks, and says to re-run it
with `RowhouseKit.contract()`. The drop quotes a check count four more times, at `CONTRACT.md`:3, the root
`README.md`:6, `stack/README.md`:78 and `walkup/README.md`:74; all four say 2,464, and runs 1 and 2 returned 2,464.
Nothing that landed defines `RowhouseKit`: no such global existed once runs 1 and 2 had loaded the eleven scripts (§4),
and no landed file names it outside that line. The same README says at :31 to re-run `Art/_rowhouseKit.js` when a hash
moves; `_multiunitKit.js`:4 names that file as the writer the terrace shipped with before the three phases shared one,
and it is not in the drop (§6). Under the landed contract the terrace is 39 of the 112 configurations (`CONTRACT.md`:57,
§4), so 39 × 22 = 858 of the checks.

### The terrace and the placer

`rowhouse/README.md`:84-86 says the terrace now shares the placement engine (`_interiorPlacer.js`) and the sidecar
writer (`_buildingGameplay.js`) with the walk-up and the stack. The writer half holds: `rowhouseUnitIsoRig.js` takes
`root.BuildingGameplay` at :1191 (§5). The placer half does not, yet. Of the eleven scripts, only `_interiorPlacer.js`
itself and the walk-up's and the stack's rooms rigs name `InteriorPlacer`. The placer's header (:4-6) says it was
extracted from the terrace's rooms rig, which should adopt it on its next pass, and run 4 wrote the terrace's three
sidecars byte-identical with no placer loaded. They carry `placerDerivedFromRigSha256` all the same: `stampFive` in
`_multiunitKit.js` (:137) writes all five stamps into each sidecar it is given, the placer's at :145. So an edit to
`_interiorPlacer.js` marks the terrace's three sidecars stale, and on the evidence of run 4 regenerating them would
change only that stamp.

### Browser only

`_multiunitKit.js`:23 and the root `README.md`:116 say these rigs run in the browser, not on node, and
`walkup/README.md`:98 answers "node — no". `CONTRACT.md`:7 says to run the contract from a page with the rigs loaded,
and `stack/README.md`:102 to open its builder page, which is not in the drop (§6). On node v24.19.0, with a `fetch` that
answers each URL with the file of the same name (§4), the contract gave 2,464 checks and 0 failures, and all 16 sidecars
came out byte-identical to the committed files.

## 8. `interiorPropRig.js` replaces main's copy

The prop rig is not new here. `docs/art/rigs/interiorPropRig.js` came to `main` in
[#227](https://github.com/apholmes7-bot/hiddenharbours/pull/227) (`4f048395`, 2026-07-19), its only commit there, and
`RigCatalog.InteriorKit.cs`:47 hosts it for the interior bake as `interiorProp`, global `PropIso`. The three kits in the
drop carry the same bytes of it (§1), and all 16 sidecars stamp its sha256 as `propsDerivedFromRigSha256` (§3). Those
bytes replace main's at the same path, so everything that names the path now gets the drop's rig, and `docs/art/rigs/`
holds one copy of it.

|  | At `main` | Landed |
|---|---|---|
| Bytes | 37,819 | 63,049 |
| sha256 | `e13a3690f9d1b2afacb5ffb05f011a5edaf7a76c770e4e55780ed6db6e1b7738` | `a6c880e159d355ded27d2202fa8f64577a0aae35829987a2c9643dda183f14a6` |
| `PropIso` members | 22 | 27, adding `ERAS`, `byEra`, `emit`, `height`, `mats` |
| Pieces in `PROPS` | 28 | 44, adding `bookcase`, `fireplace`, `fourPoster`, `fridge`, `hallStand`, `lockers`, `longClock`, `mailboxes`, `piano`, `settee`, `shower`, `sideboard`, `sofa`, `vanity`, `washer`, `writingDesk` |

### What the bake asks of it

`propcheck.mjs` (§11) runs on node and measures what the interior bake asks of `PropIso`, for main's rig and for the
landed one. It hosts each rig twice: alone, as `RigCatalog`'s `interiorProp` entry hosts it for the bake, and beside
main's room rig, `InteriorIso`, as `InteriorRigAzimuthProbe` does. It reads the props, their options and the option keys
out of `InteriorKit.cs` at `main`. Run from the repository root as `node propcheck.mjs bd5d28b2`, it printed:

```
rig      before e13a3690f9d1b2afacb5ffb05f011a5edaf7a76c770e4e55780ed6db6e1b7738 37819 B (bd5d28b2:docs/art/rigs/interiorPropRig.js)
rig      after  a6c880e159d355ded27d2202fa8f64577a0aae35829987a2c9643dda183f14a6 63049 B (working tree, CRLF folded to LF)
W        before 460 | after 460 | same
H        before 460 | after 460 | same
PX       before 32 | after 32 | same
pivot    before {"x":230,"y":330} | after {"x":230,"y":330} | same
project  worst |PropIso - InteriorIso| px over 8 facings x 4 points: before 5.684341886080802e-14 | after 5.684341886080802e-14 | after == before at every coordinate: true
keys     InteriorKit.PropOptionKeys resolved: before 8/8 | after 8/8 | unresolved after: none
members  PropIso: before 22 | after 27 | added ERAS byEra emit height mats | removed none
PROPS    before 28 | after 44 | added bookcase fireplace fourPoster fridge hallStand lockers longClock mailboxes piano settee shower sideboard sofa vanity washer writingDesk | removed none
prop     bed       {"wood":"walnut","fabric":"sage","fabric2":"cream","weather":0.2} | known true/true | footprint 1.500 x 2.050 -> 1.500 x 2.050 | cells identical 0/8 | opaque bbox 82x77 -> 82x77 | alone == beside InteriorIso 8/8 -> 8/8
prop     table     {"wood":"oak","len":0.35,"weather":0.3} | known true/true | footprint 2.315 x 0.780 -> 1.415 x 0.780 | cells identical 8/8 | opaque bbox 52x51 -> 52x51 | alone == beside InteriorIso 8/8 -> 8/8
prop     chair     {"wood":"oak","weather":0.3} | known true/true | footprint 0.440 x 0.420 -> 0.440 x 0.420 | cells identical 8/8 | opaque bbox 22x39 -> 22x39 | alone == beside InteriorIso 8/8 -> 8/8
prop     dresser   {"paint":"blue","wood":"pine","weather":0.25} | known true/true | footprint 1.000 x 0.500 -> 1.000 x 0.500 | cells identical 8/8 | opaque bbox 38x47 -> 38x47 | alone == beside InteriorIso 8/8 -> 8/8
prop     seaChest  {"wood":"walnut","weather":0.45} | known true/true | footprint 0.900 x 0.500 -> 0.900 x 0.500 | cells identical 8/8 | opaque bbox 34x32 -> 34x32 | alone == beside InteriorIso 8/8 -> 8/8
```

`W`, `H`, `PX` and `pivot` did not change. Over 8 facings and 4 points, `PropIso` projects to within 5.7e-14 px of
`InteriorIso` in both rigs, and the landed rig projects every point exactly where main's did. All 8 keys in
`InteriorKit.PropOptionKeys` resolve in both, and no member or piece was removed. `known` says whether each rig's
`PROPS` has the piece. A cell is one facing rendered at 460 × 460 px, and two cells are identical when their pixels are;
each rig renders every cell the same alone as beside `InteriorIso`. The 5 pieces are the props `Interiors.json` at
`main` holds from this rig, with the same options, and the footprints it stores for them are the ones main's rig
reports:

| Piece | `optionsJs` | `Interiors.json` line | Footprint at `main`, m | Landed, m | Cells identical | Opaque box, px |
|---|---|---|---|---|---|---|
| `bed` | `{wood:'walnut',fabric:'sage',fabric2:'cream',weather:0.2}` | :390 | 1.500 × 2.050 | same | 0 of 8 | 82 × 77 |
| `table` | `{wood:'oak',len:0.35,weather:0.3}` | :430 | 2.315 × 0.780 | **1.415 × 0.780** | 8 of 8 | 52 × 51 |
| `chair` | `{wood:'oak',weather:0.3}` | :470 | 0.440 × 0.420 | same | 8 of 8 | 22 × 39 |
| `dresser` | `{paint:'blue',wood:'pine',weather:0.25}` | :510 | 1.000 × 0.500 | same | 8 of 8 | 38 × 47 |
| `seaChest` | `{wood:'walnut',weather:0.45}` | :550 | 0.900 × 0.500 | same | 8 of 8 | 34 × 32 |

The opaque box is the greatest width and the greatest height of opaque pixels over the 8 cells; neither changed.

### The table's footprint

Both rigs draw the table the same: `pTable` is the same function (main :157-161, landed :180-184), and
`const w=1.1+(s.len||0)*0.9` makes its top 1.1 + 0.35 × 0.9 = 1.415 m wide at the catalog's `len` of 0.35. What changed
is what `footprint()` reports. Main's adds the run to `foot[0]`: 2.0 + 0.35 × 0.9 = 2.315 m (:363, :463-465). The
landed one is headed "exact, not nominal: a run prop reports the width its builder actually draws" (:740) and returns
`runBase` + `len` × `runK`: 1.1 + 0.35 × 0.9 = 1.415 m (:625, :741-744).

The bake stores that number, and colliders are built from it. `InteriorRigBaker.cs`:424-425 asks the rig's `footprint()`
for the width and depth, and `Interiors.json` at `main` holds the table's as 2.315 × 0.78 (:456-457).
`StPetersInteriors.cs` gives each prop it places a `PolygonCollider2D` built from that pair (:586-591),
`InteriorCatalog.cs` serves the pair as `FootprintMetres` (:86-88), and `StPetersInteriorsTests.cs`:244 reads the pair.
So at `main` the catalog makes the table 0.9 m wider than the rig draws it. This change bakes nothing: `Interiors.json`
keeps 2.315 m until the interiors are baked from the landed rig.

### The bed's pillows

The bed is the piece drawn differently: none of its cells is identical, while its footprint, 1.5 × 2.05 m, and its
opaque box did not change. Main's `pBed` draws one pillow across the bed (:171). The landed one draws two side by side
when the bed is wider than 1.2 m (:194-196). The catalog's bed gives no `variant`, which resolves to 0 (:670), and
variant 0 is 1.5 m wide (:190, :627). Both pillows lie in the same strip as main's pillow, from −d/2 + 0.10 to −d/2 +
0.54 (:196), so `InteriorKit.BedPillowInsetMetres`, (0.10 + 0.54)/2 = 0.32 m (`InteriorKit.cs`:350-354, :370), and
`PillowSide.MinusY` (`BedPillow.cs`:36-38) still hold.

Three comments at `main` quote main's pillow line, `// pillow (head end, -Y front)`: `InteriorKit.cs`:161-162 and :368,
and `BedPillow.cs`:37. The landed rig's line reads `// pillow(s), head end (-Y front)` (:194). `InteriorKit.cs`:367
quotes `const w=1.5, d=2.05`; the landed `pBed` makes `w` 1.02 m for variant 1, 1.62 m for variant 2 and 1.5 m otherwise
(:190). This change leaves those comments as they are.

### What names the path

At `main`, 16 lines in 8 files under `Assets/` and `tools/` name `interiorPropRig`, not counting the scene-export
packages (below). The rig was replaced in place, so the path each names still resolves:

| File | Lines |
|---|---|
| `Assets/Tests/EditMode/Art/InteriorKitTests.cs` | :90 |
| `Assets/Tests/EditMode/RigBaking/RigCatalogAssemblyTests.cs` | :204 |
| `Assets/_Project/Art/Editor/InteriorKit.cs` | :15, :121, :161, :350, :366 |
| `Assets/_Project/Art/Sprites/Interiors/Interiors.json` | :393, :433, :473, :513, :553 |
| `Assets/_Project/Code/Core/Iso/BedPillow.cs` | :15 |
| `Assets/_Project/Code/Tools/Editor/RigBaking/InteriorRigBaker.cs` | :181 |
| `Assets/_Project/Code/Tools/Editor/RigBaking/RigCatalog.InteriorKit.cs` | :47 |
| `tools/scene-export/hhexport/repo.py` | :523 |

### The scene-export packages

`StPeters.scene.json` pins the prop rig's sha256, and `MANIFEST.json` pins the sha256 of each package. Both files in
this change are what the exporter writes: `hh_scene_export.py --check` (`tools/scene-export/README.md`:14) reports
`up to date (3 files)`. `pkgdiff.py` (§11) compares them with `main` leaf by leaf. A leaf may differ only if it pins
this rig and moves from main's sha256 to the landed one, or is the manifest's sha256 of a package that changed. Run as
`python pkgdiff.py bd5d28b2`, it printed:

```
prop rig bd5d28b2 e13a3690f9d1b2afacb5ffb05f011a5edaf7a76c770e4e55780ed6db6e1b7738 -> working a6c880e159d355ded27d2202fa8f64577a0aae35829987a2c9643dda183f14a6
changed ['tools/scene-export/packages/MANIFEST.json', 'tools/scene-export/packages/StPeters.scene.json'] | untracked [] | deleted []
tools/scene-export/packages/MANIFEST.json               1 differing leaves
tools/scene-export/packages/StPeters.scene.json         39 differing leaves
   tools/scene-export/packages/MANIFEST.json               manifest-sha(StPeters.scene.json)             1
   tools/scene-export/packages/StPeters.scene.json         prop-rig pin entities[*].x-rigSha256          37
   tools/scene-export/packages/StPeters.scene.json         prop-rig pin x-rigVersions.interiorprop.sha256 1
   tools/scene-export/packages/StPeters.scene.json         prop-rig pin x-rigs[3].sha256                 1
   manifest NineMileCreek.scene.json entities 2757->2757 paths 0->0 rigsPinned 12->12 sceneLastBuiltCommit 31f0d08a07597e074224ed3d25cbe9811b6e7853->31f0d08a07597e074224ed3d25cbe9811b6e7853
   manifest StPeters.scene.json    entities 1330->1330 paths 11->11 rigsPinned 10->10 sceneLastBuiltCommit ad852bd2583e03886bffc3c33d5f0238d1c33dc7->ad852bd2583e03886bffc3c33d5f0238d1c33dc7
RESULT: PROVENANCE-ONLY
```

Every leaf that differs is a pin of the prop rig or the manifest's sha256 of `StPeters.scene.json`, and the manifest's
counts and `sceneLastBuiltCommit` for both scenes are the ones at `main`.

### Pull request #853

[#853](https://github.com/apholmes7-bot/hiddenharbours/pull/853), open with head `c8b36d99`, carries the same 63,049
bytes of `interiorPropRig.js` and gives the file a line in `.gitattributes` (:244) with `text eol=lf`. This change gives
it none, so the file has no line-end rule. With `core.autocrlf=true`, as on this machine, git writes it to a working
tree with CRLF line ends, as it does `gas-station-rig/tools/_sidecarExport.js`, which has no rule either and is CRLF on
disk here. Every sha256 of the prop rig in this record, in the sidecars and in `StPeters.scene.json` is over its LF
bytes, and the exporter hashes a rig with its CR bytes stripped (`tools/scene-export/hhexport/repo.py`:46-53, :484), so
a CRLF checkout pins the same sha256.

Its merge base with `main` is `5f691b43`. From there it changes 44 files, and 4 of them this change also changes:
`.gitattributes`, `docs/art/rigs/interiorPropRig.js`, `tools/scene-export/packages/MANIFEST.json`,
`tools/scene-export/packages/StPeters.scene.json`. Both change the two packages, and #853's differ from this change's,
so whichever of the two merges second needs its packages regenerated on top of the first.

## 9. Not in this change

At `main`, nothing under `Assets/`, `tools/`, `ProjectSettings/` or `.github/` names the kit folder, its schema, the ten
scripts in it or the globals they define, and after this change only the test in §10 does. The change bakes and places
nothing:

- No sprite sheet is baked from the six building rigs, and no `.meta` file is added under `docs/`.
- `RigCatalog` has no entry for them. `interiorPropRig.js` keeps the `interiorProp` entry it has at `main` (§8).
- No collider is generated from `SOLE`, `THRESHOLD`, `STAIRS`, `BLOCKERS` or `ROOF`, and there is no Def or reader for
  `hidden-harbours/building-gameplay@1`.
- No scene or prefab places a building. Where the buildings stand is not ruled.
- `Interiors.json` is not re-baked. It keeps main's table footprint until the interiors are baked from the landed prop
  rig (§8).
- The drop's root `README.md` is not landed (§7). Neither are 12 byte-identical copies: every kit carries `CONTRACT.md`
  and the same five scripts, and one copy of each landed (§1).

### Sections the game will have to read

How many sidecars of each phase carry each section:

| Section | Terrace (3) | Walk-up (6) | Stack (7) |
|---|---:|---:|---:|
| `UNITS` | 3 | 6 | 7 |
| `THRESHOLD` | 3 | 6 | 7 |
| `MAIL` | 2 | 6 | — |
| `WALK` | 3 | 6 | — |
| `BLOCKERS` | 3 | 6 | 7 |
| `ROOF` | 3 | 6 | 7 |
| `SOLE` | 3 | 6 | 7 |
| `STAIRS` | 3 | 6 | 7 |
| `INTERACT` | 3 | 6 | 7 |
| `ELEVATOR` | — | 6 | — |
| `SHARED` | — | 6 | 7 |
| `PORCH` | — | — | 7 |

The section tables of the phase READMEs (`rowhouse/README.md`:48-56, `walkup/README.md`:45-55, `stack/README.md`:43-51)
say what each holds. What they describe, the game has yet to build; these, with the `_confirm` notes below, are
follow-up work:

- `ELEVATOR`, the walk-up's only: one car and one shaft serving every storey, with a pit and an overrun, the car's
  inside dimensions and floor z per level, bipart landing doors whose collider is the panel, travel, speed, dwell and
  call points (`walkup/README.md`:46).
- `SHARED`: on the walk-up, the lobby, a corridor per storey as `means_of_escape`, the shared laundry and the lockers
  (`walkup/README.md`:47); on the stack, a vestibule and a landing per storey, a stair hall per storey as
  `means_of_escape`, and the laundry at the yard (`stack/README.md`:45).
- `PORCH`, the stack's only: a street porch per storey, roofed and walkable, open-railed or glazed, and the rear service
  porches the kitchen doors land on (`stack/README.md`:44). Two flats on one storey raise `porch_occupancy`, below.
- `MAIL`: wall boxes with a delivery stand point on the terrace's basic tier (`rowhouse/README.md`:54), and one bank in
  the walk-up's lobby with a box per flat (`walkup/README.md`:53). The stack has no `MAIL`; its vestibule has mail on
  one wall (`LOBBY`, below).
- `STAIRS`: the core flight per storey carries an exact rise, a `void_above` and landings reserved before furnishing
  (`walkup/README.md`:50, `stack/README.md`:47). The stack's rear stair is published per flight, because grade to first
  floor is not a storey rise.
- `ROOF`: the bulkhead where a terrace has roof access (`rowhouse/README.md`:56), the walk-up's lift overrun as plant
  (`walkup/README.md`:55), and on the gambrel tier the widow's walk, a hatch off the top landing onto a railed deck
  (`stack/README.md`:51).
- `THRESHOLD`: street entries with a hinge axis, a 95° outward swing and its swept rect, and every interior door.
  Interior leaves park flat against the wall, so a door's collider is its opening (`rowhouse/README.md`:49).
- Occupancy: `millRow4`'s resident slots went from 16 to 12 (`rowhouse/README.md`:94, §7), and anything reading
  occupancy should re-read it (:99).
- New on the terrace's phase (`rowhouse/README.md`:101-103): `heights`, one rise and one clear height per storey, which
  all 16 sidecars carry; `room_height_m` on every `SOLE` entry; `STAIRS[].landings`; exact flight rises; and a
  `floorAtPivot` option on the room rig.

### What the sidecars ask the game to confirm

Every sidecar ends with `_confirm`, notes for whatever reads it: which figure to trust, what is left out rather than
invented, and what is declared or a rule rather than measured off the bake. A phase gives each key the same text in
every one of its sidecars.

| Key | Sidecars | What it says |
|---|---|---|
| `resident_slots` | all 16 | `UNITS[].resident_slots` counts the sleep anchors the interior built, and `nominal_occupancy` is derived from the bed request. Trust `resident_slots`. |
| `bedsBuilt` | terrace 3 | A plate holds two bedrooms, so a 2-storey 3-bed request builds two and reports it (`bedsBuilt` / `bedsRequested`). |
| `mail` | terrace 3 | Luxury units have no wall box, so `MAIL` is left out rather than invented. |
| `entry_swing` | terrace 3 · walk-up 6 | Street doors get a 95° outward swing, on the walk-up on its basic tier only. The bake draws them shut, so the arc is declared. |
| `flat_door_swing` | walk-up 6 · stack 7 | Flat doors open into the flat. The bake parks every interior leaf against its wall, so this is a rule, not a measurement. |
| `elevator_timings` | walk-up 6 | Car speed, acceleration, dwell and door times are declared as a starting point: the rig draws a car at a storey, not a journey. |
| `lift_pit` | walk-up 6 | The 1.10 m pit and the motor overrun are declared from the drawn overrun box. Nothing below grade is modelled. |
| `porch_occupancy` | stack 7 | Where a storey has two flats, both households get the same porch. Shared ground or split down the middle is a gameplay ruling. |
| `rear_stair` | stack 7 | The rear stair is one run per storey with a declared going. Its geometry landing by landing is not measured. |
| `widows_walk` | stack 7 | The deck is declared as a 4.2 m square on the ridge: the bake draws its rail, not its framing. |

Since a phase writes its notes once for all its sidecars, a key there does not mean the preset has the feature. The four
decker sidecars carry `widows_walk` and exclude `walkable_roof`. The deck is on the gambrel tier, the three captains,
whose `ROOF.deck` is the widow's walk (`stack/README.md`:4, :51).

### What the sidecars leave out

`_excluded` names what a rig does not model, and why. All 16 sidecars exclude `WASHBOARD` and `CLEATS` as "Not a hull."
The rest, with how many sidecars of each phase exclude them:

| Key | Terrace | Walk-up | Stack | Why |
|---|---:|---:|---:|---|
| `ELEVATOR` | 3 | — | 7 | Walk-up by definition. The walk-up block is the phase that carries it. |
| `CORRIDOR` | 3 | — | 7 | No shared circulation: a terrace's unit doors open onto the street, a stack's flat doors off the vestibule or a landing. |
| `LOBBY` | 3 | — | 7 | The terrace has no shared entrance. The stack's 2.95 m vestibule, mail on one wall, is published in `SHARED` as a vestibule. |
| `lockers` | — | — | 7 | No basement and no cages: storage is the flat's own store room and the back porch. |
| `private_stair` | — | 6 | 7 | A flat is single-level, and its stairs are shared. |
| `walkable_roof` | 2 | 4 | 4 | Not walkable and no hatch: a membrane behind a parapet, or on the stack a pitched roof. |
| `rear_garden` | 3 | — | — | The rig bakes the building, not its plot: yards, bins and bike racks are placed by the scene. |
| `refuse_store` | — | 6 | — | Bins are site dressing placed by the scene. |
| `site` | — | 6 | 7 | The plot is the scene's: kerbs, bike racks, fences, paths and planting. The stack publishes its drying yard, whose posts the rig draws. |

The six that do not exclude `walkable_roof` are `coastalRow4`, `cannery12`, `cannery8Loft`, `captains6`, `captains2` and
`captains4`. The scene that places a building dresses its plot: yards, bins, bike racks, kerbs, fences, paths and
planting.

## 10. Re-checking

### In CI

`MultiunitKitIntakeTests` (`Assets/Tests/EditMode/RigBaking/`) runs in CI's `EditMode tests` step
(`.github/workflows/ci.yml`:296). It reads the committed files with `System.IO` and parses the sidecars as JSON; it runs
no rig and bakes nothing. It hashes with CRLF folded to LF, as `.gitattributes` says it does. If
`docs/art/rigs/multiunit-kit/` is absent, every test is ignored and says why, so a missing kit never passes. A failure
names the sidecar, entry or file it is about.

| Test | Asserts | `CONTRACT.md` |
|---|---|---|
| `SidecarsParseWithTheirSchemaAndFrame` | each phase folder's `gameplay/` holds exactly its presets' sidecars, 16 in all, and each parses; `schema` is `hidden-harbours/building-gameplay@1`; `frame` is in metres at 32 px per metre |  |
| `StampsEqualTheCommittedRigs` | each of a sidecar's five stamps equals the sha256 of the committed rig it names (§3) |  |
| `Sha256SumsMatchTheCommittedBytes` | each kit's `SHA256SUMS.json` lists exactly the files §2 says landed for it, and each entry equals the sha256 of that file |  |
| `EveryDwellingHasItsResidentSlots` | `UNITS` is not empty and has `build.units` entries; each has `resident_slots` equal to its number of `sleep_anchors`, and at least 1; `millRow4`'s add up to 12 | #5 |
| `EveryFlightRisesExactlyAndLandsBothEnds` | `STAIRS` is not empty; for every flight, a service stair's `flights[]` included, `steps × rise_m` is within 1e-9 of `floor_rise_m`; every interior flight has both landings, `departure` and `arrival` | #12, #16 |
| `EveryAnchorIsReachableAndEveryPiecePlaced` | `INTERACT` is not empty and no entry's `reach.point` is null; `_unplaced` is absent or empty | #18, #22 |
| `OneInteriorPropRig` | `docs/art/rigs/` holds exactly one `interiorPropRig.js`, at its top level |  |

`StampsEqualTheCommittedRigs` is the drift guard: edit any of the five rigs without regenerating the sidecars, and it
goes red, naming the sidecar and the stamp. `Sha256SumsMatchTheCommittedBytes` goes red on an edit to any file a kit
lists, a sidecar or a `README.md` included, naming the file. The tolerance separates rounding from the defect #12 was
written for: computed in doubles, `steps × rise_m` misses `floor_rise_m` by at most 4.4e-16 (§3), while
`6 × 0.177 = 1.062` misses by 2 mm (`CONTRACT.md`:42-43).

`EveryDwellingHasItsResidentSlots` and `EveryAnchorIsReachableAndEveryPiecePlaced` are the check §5 calls for. The
sidecars the controls wrote that differ from the committed ones, all 16 from run 3 and the stack's seven from run 4,
carry the committed stamps, so `StampsEqualTheCommittedRigs` passes them. None of them has a resident slot, which fails
the first. The second fails on those with no `INTERACT` entry: run 4's seven, and run 3's `millRow4` and `duplexPair`.

What the test cannot see is whether a sidecar is what its rigs write today. A sidecar written by a broken load keeps its
stamps, and if what it states still meets every check above and its `SHA256SUMS.json` entry is recomputed with it, the
test passes. Regenerating and comparing, as runs 1 and 2 of §4 do, catches it, and that is done by hand.

### By hand

Run these from the repository root, with the scripts from §11 saved there. None of them reads its own location or writes
a file.

```
node multiunit-regen.mjs drop <unzipped>/export/multiunit-rig-export
node multiunit-regen.mjs repo docs/art/rigs
node multiunit-regen.mjs repo docs/art/rigs --omit interiorPropRig.js
node multiunit-regen.mjs repo docs/art/rigs --omit _interiorPlacer.js
node pairs.mjs
node propcheck.mjs bd5d28b2
python tools/scene-export/hh_scene_export.py --check
python pkgdiff.py bd5d28b2
```

Runs 1 and 2 should find all 16 sidecars byte-identical, and `--check` should report `up to date (3 files)`. The others
print what §4, §5 and §8 show. The first run needs the zip named in §1, unzipped.

## 11. The scripts

None of the four scripts is committed. Each is printed below as it ran from the repository root, on node v24.19.0 or
Python 3.14.6. `multiunit-regen.mjs` is Phase A's harness with the five lines §4 describes.

| Script | Lines | Run as | Printed the numbers in |
|---|---:|---|---|
| `multiunit-regen.mjs` | 93 | the four commands in §4 | §4, §5 |
| `pairs.mjs` | 24 | `node pairs.mjs` | §4 |
| `propcheck.mjs` | 77 | `node propcheck.mjs bd5d28b2` | §8 |
| `pkgdiff.py` | 95 | `python pkgdiff.py bd5d28b2` | §8 |

<details>
<summary><code>multiunit-regen.mjs</code></summary>

```js
// Runs the art side's own generator (MultiunitKit.contract + MultiunitKit.all) headless on node and
// compares every regenerated sidecar with the committed one: bytes, then parsed JSON by key path.
//   node multiunit-regen.mjs drop <export root>        (…/multiunit-rig-export: <phase>-rig-kit/{rigs,gameplay})
//   node multiunit-regen.mjs repo <docs/art/rigs>      (interiorPropRig.js + multiunit-kit/{shared,<phase>})
//   --omit <file.js>  leave a module out (negative control)   --no-contract  skip the 112-configuration contract
import fs from 'node:fs'; import path from 'node:path'; import vm from 'node:vm'; import crypto from 'node:crypto';
const [layout, root, ...rest] = process.argv.slice(2);
const omit = new Set(rest.flatMap((a, i) => a === '--omit' ? [rest[i + 1]] : []));
const phaseOf = n => /^rowhouse/.test(n) ? 'rowhouse' : /^walkup/.test(n) ? 'walkup' : /^stack/.test(n) ? 'stack' : null;
const rigPath = n => {
  const ph = phaseOf(n);
  if (layout === 'drop') return path.join(root, (ph || 'rowhouse') + '-rig-kit', 'rigs', n);
  if (n === 'interiorPropRig.js') return path.join(root, n);
  return path.join(root, 'multiunit-kit', ph || 'shared', n);
};
const sidecarPath = (ph, stem, k) => layout === 'drop'
  ? path.join(root, ph + '-rig-kit', 'gameplay', `${stem}.${k}.gameplay.json`)
  : path.join(root, 'multiunit-kit', ph, 'gameplay', `${stem}.${k}.gameplay.json`);
const ORDER = ['interiorPropRig.js', '_interiorPlacer.js', '_buildingGameplay.js',
  'rowhouseUnitIsoRig.js', 'walkupUnitIsoRig.js', 'stackUnitIsoRig.js',
  'rowhouseIsoRig.js', 'walkupIsoRig.js', 'stackFlatsIsoRig.js', '_sidecarExport.js', '_multiunitKit.js'];
const sha = b => crypto.createHash('sha256').update(b).digest('hex');
// fetch is the only browser API on the generator path: SidecarExport.rigSha256('Art/x.js') fetches, then hashes.
const fetchShim = async url => {
  const p = rigPath(path.basename(String(url)));
  if (!fs.existsSync(p)) return { ok: false, status: 404, text: async () => '' };
  return { ok: true, status: 200, text: async () => new TextDecoder('utf-8').decode(fs.readFileSync(p)) };
};
const ctx = vm.createContext({ console, fetch: fetchShim, crypto: globalThis.crypto, TextEncoder, TextDecoder });
vm.runInContext('globalThis.window = globalThis;', ctx);
const t0 = Date.now();
for (const n of ORDER) {
  if (omit.has(n)) { console.log(`load  ${n.padEnd(24)} OMITTED`); continue; }
  const b = fs.readFileSync(rigPath(n));
  vm.runInContext(b.toString('utf8'), ctx, { filename: n });
  console.log(`load  ${n.padEnd(24)} ${sha(b).slice(0, 12)}  ${b.length} B`);
}
const G = vm.runInContext('Object.keys(globalThis).filter(k => /^[A-Z]/.test(k)).sort().join(" ")', ctx);
console.log(`globals: ${G}`);
const K = ctx.MultiunitKit;
if (!rest.includes('--no-contract')) {
  const t = Date.now(), c = K.contract();
  console.log(`\ncontract: configurations ${c.configurations} | assertions ${c.assertions} | checks ${c.checks} | failed ${c.failed} | ${Date.now() - t} ms`);
  c.fails.slice(0, 12).forEach(f => console.log('  FAIL ' + f));
  // added at Phase B: which phases the failures belong to, against how many configurations each phase has
  const per = (xs, key) => xs.reduce((m, x) => (m[key(x)] = (m[key(x)] || 0) + 1, m), {});
  const show = m => Object.entries(m).map(([k, v]) => `${k} ${v}`).join(' | ') || 'none';
  console.log(`configurations by phase: ${show(per(K.configurations(), x => x.phase))}`);
  console.log(`failed by phase: ${show(per(c.fails, f => f.split(' ')[0] + (f.split(' ')[2] === 'THREW' ? ' threw' : ' asserted')))}`);
}
const t1 = Date.now(); let A;
try { A = await K.all(); console.log(`\nall(): ${Object.keys(A.files).length} file entries | ${Date.now() - t1} ms | its own contract ${A.contract.checks}/${A.contract.failed}`); }
catch (e) { // a control that breaks one phase must not hide the others: per preset, with all()'s own serialization
  console.log(`\nall() THREW: ${e.message} -> MultiunitKit.sidecar() per preset`); A = { files: {} };
  for (const P of K.phases()) for (const k of P.presets) {
    const key = 'export/' + P.kit + '/gameplay/' + P.stem + '.' + k + '.gameplay.json';
    try { A.files[key] = JSON.stringify(await K.sidecar(P.id, k), null, 1) + '\n'; }
    catch (e2) { A.files[key] = null; console.log(`  ${P.id}/${k} THREW ${e2.message}`); }
  }
}
const diff = (a, b, p, out) => {
  if (out.length > 400) return;
  if (typeof a === 'number' && typeof b === 'number') {
    if (!Object.is(a, b)) out.push({ p, a, b, step: Math.abs(a - b) <= 4 * Number.EPSILON * Math.max(Math.abs(a), Math.abs(b)) });
    return;
  }
  if (a === null || b === null || typeof a !== 'object' || typeof b !== 'object') { if (a !== b) out.push({ p, a, b }); return; }
  if (Array.isArray(a) !== Array.isArray(b)) { out.push({ p, a: 'array?' + Array.isArray(a), b: 'array?' + Array.isArray(b) }); return; }
  const ka = Object.keys(a), kb = Object.keys(b);
  for (const k of new Set([...ka, ...kb])) {
    if (!(k in b)) out.push({ p: p + '.' + k, a: 'present', b: 'ABSENT in committed' });
    else if (!(k in a)) out.push({ p: p + '.' + k, a: 'ABSENT in regenerated', b: 'present' });
    else diff(a[k], b[k], p + '.' + k, out);
  }
};
let byteSame = 0, parsedSame = 0, n = 0;
const stampKeys = ['derivedFromRigSha256', 'interiorDerivedFromRigSha256', 'propsDerivedFromRigSha256', 'placerDerivedFromRigSha256', 'writerDerivedFromRigSha256'];
for (const P of K.phases()) for (const k of P.presets) {
  n++;
  const key = 'export/' + P.kit + '/gameplay/' + P.stem + '.' + k + '.gameplay.json';
  const regen = A.files[key]; const file = sidecarPath(P.id, P.stem, k);
  if (regen == null) continue;
  const committed = fs.existsSync(file) ? fs.readFileSync(file) : null;
  if (!committed) { console.log(`  ${P.id}/${k}: NO COMMITTED FILE at ${file}`); continue; }
  const bytes = Buffer.from(regen, 'utf8').equals(committed); if (bytes) byteSame++;
  const ra = JSON.parse(regen), cb = JSON.parse(committed.toString('utf8')); const d = []; diff(ra, cb, '$', d);
  if (!d.length) parsedSame++;
  const order = JSON.stringify(Object.keys(ra)) === JSON.stringify(Object.keys(cb));
  const slots = ra.UNITS.reduce((s, u) => s + u.resident_slots, 0);
  console.log(`  ${(P.id + '/' + k).padEnd(22)} bytes ${bytes ? 'IDENTICAL' : 'differ   '} | parsed ${d.length ? d.length + ' DIFF' : 'EQUAL'} | top-key order ${order ? 'same' : 'DIFFERS'} | units ${ra.UNITS.length} slots ${slots} SOLE ${ra.SOLE.length} THR ${ra.THRESHOLD.length} STAIRS ${ra.STAIRS.length} INTERACT ${ra.INTERACT.length} BLOCKERS ${ra.BLOCKERS.length} | stamps ${stampKeys.every(s => ra[s] === cb[s]) ? '5/5 equal' : 'DIFFER'}`);
  d.slice(0, 5).forEach(x => console.log(`      ${x.p}: regen ${JSON.stringify(x.a)?.slice(0, 80)} | committed ${JSON.stringify(x.b)?.slice(0, 80)}${x.step ? ' (float step)' : ''}`));
}
console.log(`\nsidecars ${n} | byte-identical ${byteSame} | parsed-equal ${parsedSame} | total ${Date.now() - t0} ms`);
```

</details>

<details>
<summary><code>pairs.mjs</code></summary>

```js
// MultiunitKit.all() writes each sidecar under two keys: are the two the same string? And which configurations does
// MultiunitKit.contract() sweep, by the phase each one names?
//   node pairs.mjs      (cwd = the repository root; reads docs/art/rigs as landed)
import fs from 'node:fs'; import path from 'node:path'; import vm from 'node:vm';
const root = 'docs/art/rigs';
const phaseOf = n => /^rowhouse/.test(n) ? 'rowhouse' : /^walkup/.test(n) ? 'walkup' : /^stack/.test(n) ? 'stack' : null;
const rigPath = n => n === 'interiorPropRig.js' ? path.join(root, n) : path.join(root, 'multiunit-kit', phaseOf(n) || 'shared', n);
const ORDER = ['interiorPropRig.js', '_interiorPlacer.js', '_buildingGameplay.js',
  'rowhouseUnitIsoRig.js', 'walkupUnitIsoRig.js', 'stackUnitIsoRig.js',
  'rowhouseIsoRig.js', 'walkupIsoRig.js', 'stackFlatsIsoRig.js', '_sidecarExport.js', '_multiunitKit.js'];
const fetchShim = async url => {
  const p = rigPath(path.basename(String(url)));
  if (!fs.existsSync(p)) return { ok: false, status: 404, text: async () => '' };
  return { ok: true, status: 200, text: async () => new TextDecoder('utf-8').decode(fs.readFileSync(p)) };
};
const ctx = vm.createContext({ console, fetch: fetchShim, crypto: globalThis.crypto, TextEncoder, TextDecoder });
vm.runInContext('globalThis.window = globalThis;', ctx);
for (const n of ORDER) vm.runInContext(fs.readFileSync(rigPath(n)).toString('utf8'), ctx, { filename: n });
const K = ctx.MultiunitKit, A = await K.all(), byName = {}, conf = {};
for (const [k, v] of Object.entries(A.files)) (byName[k.split('/').pop()] ||= []).push([k, v]);
for (const [b, xs] of Object.entries(byName))
  console.log(`pair ${b} ${xs.map(x => x[0].slice(0, x[0].length - b.length)).join(' ')} ${xs.every(x => x[1] === xs[0][1]) ? 'equal' : 'DIFFER'} ${typeof xs[0][1]}`);
for (const c of K.configurations()) (conf[c.phase] ||= []).push(c.name);
for (const [ph, ns] of Object.entries(conf)) console.log(`conf ${ph} ${ns.length} ${ns.join(' ')}`);
```

</details>

<details>
<summary><code>propcheck.mjs</code></summary>

```js
// What the interior bake asks of PropIso, measured on node for the prop rig at <base> ("before") and in the
// working tree ("after"). Each rig is hosted twice: ALONE, as RigCatalog's "interiorProp" entry hosts it for the
// bake, and BESIDE <base>'s room rig (InteriorIso), as InteriorRigAzimuthProbe does. The props, their options and
// the option keys are read out of InteriorKit.cs at <base>, not typed here.
//   node propcheck.mjs <base>        (from the repository root)
import fs from 'node:fs'; import vm from 'node:vm'; import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
const base = process.argv[2];
if (!base) { console.log('usage: node propcheck.mjs <base>'); process.exit(2); }
const show = p => execFileSync('git', ['show', `${base}:${p}`], { maxBuffer: 1 << 26 });
const sha = b => crypto.createHash('sha256').update(b).digest('hex');
const RIG = 'docs/art/rigs/interiorPropRig.js';
const rigs = { before: show(RIG), after: Buffer.from(fs.readFileSync(RIG, 'utf8').replace(/\r\n/g, '\n'), 'utf8') };
const room = show('docs/art/rigs/interiorIsoRig.js').toString('utf8');
const kit = show('Assets/_Project/Art/Editor/InteriorKit.cs').toString('utf8');
const PROPSET = [...kit.matchAll(/Build\.(?:Bed|Prop)\("\w+", "[^"]*", "(\w+)", new Dictionary<string, object>\s*\{([^}]*)\}/g)]
  .map(m => [m[1], Object.fromEntries([...m[2].matchAll(/\["(\w+)"\]\s*=\s*(?:"([^"]*)"|(-?[0-9.]+))/g)]
    .map(e => [e[1], e[2] !== undefined ? e[2] : Number(e[3])]))]);
const KEYS = [...kit.match(/PropOptionKeys\s*=\s*\{([^}]*)\}/)[1].matchAll(/"(\w+)"/g)].map(m => m[1]);
// InteriorKitTests.ResolvesKey: the same three patterns
const resolves = (src, k) => new RegExp(`g\\(\\s*'${k}'`).test(src) || new RegExp(`opts\\.${k}\\b`).test(src) ||
  new RegExp(`opts\\[\\s*'${k}'\\s*\\]`).test(src);
const POINTS = [[1, 0, 0], [0, 1, 0], [2.5, -3.5, 1.25], [-1.75, 2.25, 0.5]];
const host = (src, withRoom) => {
  const ctx = vm.createContext({ console });
  vm.runInContext('globalThis.window = globalThis;', ctx);
  if (withRoom) vm.runInContext(room, ctx, { filename: 'interiorIsoRig.js' });
  vm.runInContext(src.toString('utf8'), ctx, { filename: 'interiorPropRig.js' });
  return s => vm.runInContext(s, ctx);
};
const pixels = px => Buffer.from(new Uint8Array(px.buffer, px.byteOffset, px.byteLength));
const R = {};
for (const [tag, src] of Object.entries(rigs)) {
  const E = host(src, true), A = host(src, false);
  const r = R[tag] = { sha: sha(src), bytes: src.length, W: E('PropIso.W'), H: E('PropIso.H'), PX: E('PropIso.PX'),
    pivot: E('JSON.stringify(PropIso.pivot)'), proj: [], worst: 0 };
  for (let dir = 0; dir < 8; dir++) for (const p of POINTS) {
    const at = g => E(`(() => { const q = ${g}.project(${dir}, [${p}]); return [q.x - ${g}.pivot.x, q.y - ${g}.pivot.y]; })()`);
    const a = at('InteriorIso'), b = at('PropIso');
    r.worst = Math.max(r.worst, Math.abs(a[0] - b[0]), Math.abs(a[1] - b[1])); r.proj.push(b[0], b[1]);
  }
  r.keys = KEYS.filter(k => resolves(src.toString('utf8'), k));
  r.members = [...E('Object.keys(PropIso)')].sort();
  r.pieces = [...E('Object.keys(PropIso.PROPS)')].sort();
  r.props = {};
  for (const [name, opts] of PROPSET) {
    const args = `${JSON.stringify(name)}, ${JSON.stringify(opts)}`;
    const call = dir => `PropIso.render(${JSON.stringify(name)}, ${dir}, ${JSON.stringify(opts)})`;
    const fp = E(`PropIso.footprint(${args})`), cells = [];
    for (let dir = 0; dir < 8; dir++) {
      const px = pixels(E(call(dir)));
      let x0 = Infinity, y0 = Infinity, x1 = -1, y1 = -1;
      for (let y = 0; y < r.H; y++) for (let x = 0; x < r.W; x++) if (px[(y * r.W + x) * 4 + 3]) {
        x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y);
      }
      let alone; try { alone = sha(pixels(A(call(dir)))); } catch (e) { alone = 'THREW ' + e.message; }
      cells.push({ sha: sha(px), w: x1 - x0 + 1, h: y1 - y0 + 1, alone });
    }
    r.props[name] = { opts, known: E(`PropIso.PROPS[${JSON.stringify(name)}] !== undefined`), fp: [fp.w, fp.d], cells };
  }
}
const b = R.before, a = R.after;
const list = xs => xs.join(' ') || 'none';
console.log(`rig      before ${b.sha} ${b.bytes} B (${base}:${RIG})`);
console.log(`rig      after  ${a.sha} ${a.bytes} B (working tree, CRLF folded to LF)`);
for (const k of ['W', 'H', 'PX', 'pivot']) console.log(`${k.padEnd(8)} before ${b[k]} | after ${a[k]} | ${b[k] === a[k] ? 'same' : 'CHANGED'}`);
console.log(`project  worst |PropIso - InteriorIso| px over 8 facings x 4 points: before ${b.worst} | after ${a.worst} | after == before at every coordinate: ${b.proj.every((v, i) => Object.is(v, a.proj[i]))}`);
console.log(`keys     InteriorKit.PropOptionKeys resolved: before ${b.keys.length}/${KEYS.length} | after ${a.keys.length}/${KEYS.length} | unresolved after: ${list(KEYS.filter(k => !a.keys.includes(k)))}`);
console.log(`members  PropIso: before ${b.members.length} | after ${a.members.length} | added ${list(a.members.filter(k => !b.members.includes(k)))} | removed ${list(b.members.filter(k => !a.members.includes(k)))}`);
console.log(`PROPS    before ${b.pieces.length} | after ${a.pieces.length} | added ${list(a.pieces.filter(k => !b.pieces.includes(k)))} | removed ${list(b.pieces.filter(k => !a.pieces.includes(k)))}`);
for (const [name] of PROPSET) {
  const x = b.props[name], y = a.props[name];
  const fp = p => p.fp.map(v => v.toFixed(3)).join(' x ');
  const box = p => `${Math.max(...p.cells.map(c => c.w))}x${Math.max(...p.cells.map(c => c.h))}`;
  const alone = p => `${p.cells.filter(c => c.alone === c.sha).length}/8`;
  console.log(`prop     ${name.padEnd(9)} ${JSON.stringify(x.opts)} | known ${x.known}/${y.known} | footprint ${fp(x)} -> ${fp(y)} | cells identical ${x.cells.filter((c, i) => c.sha === y.cells[i].sha).length}/8 | opaque bbox ${box(x)} -> ${box(y)} | alone == beside InteriorIso ${alone(x)} -> ${alone(y)}`);
}
```

</details>

<details>
<summary><code>pkgdiff.py</code></summary>

```python
# Proves a scene-export regeneration is PROVENANCE-ONLY, leaf by leaf, against <base> (default HEAD).
# A leaf may differ only if it is (a) a pin whose parent names docs/art/rigs/interiorPropRig.js as its rigSource,
# moving from <base>'s digest of that rig to the working tree's, or (b) the MANIFEST's sha256 of a package that
# itself changed, moving from <base>'s blob digest to the new file's. Anything else -- a key, a length, a type, an
# entity count, any other leaf -- is UNEXPECTED.
#   python pkgdiff.py [<base>]        (from the repository root)
import collections, hashlib, json, os, subprocess, sys

BASE = sys.argv[1] if len(sys.argv) > 1 else 'HEAD'
PKG = 'tools/scene-export/packages'
RIG = 'docs/art/rigs/interiorPropRig.js'


def git(*a, binary=False):
    r = subprocess.run(['git', *a], capture_output=True, check=True)
    return r.stdout if binary else r.stdout.decode('utf-8')


def lf(b):
    return b.replace(b'\r\n', b'\n')


sha = lambda b: hashlib.sha256(b).hexdigest()
OLD_RIG = sha(git('show', BASE + ':' + RIG, binary=True))
NEW_RIG = sha(lf(open(RIG, 'rb').read()))
print('prop rig', BASE, OLD_RIG, '-> working', NEW_RIG)

changed = git('diff', '--name-only', BASE, '--', PKG).split()
untracked = git('ls-files', '--others', '--exclude-standard', '--', PKG).split()
deleted = git('ls-files', '--deleted', '--', PKG).split()
print('changed', changed, '| untracked', untracked, '| deleted', deleted)
bad = len(untracked) + len(deleted)


def walk(a, b, p, out):
    if type(a) is not type(b):
        out.append((p, 'TYPE', type(a).__name__, type(b).__name__)); return
    if isinstance(a, dict):
        if list(a.keys()) != list(b.keys()):
            out.append((p, 'KEYS', list(a.keys()), list(b.keys())))
        for k in a:
            if k in b: walk(a[k], b[k], p + '.' + k, out)
    elif isinstance(a, list):
        if len(a) != len(b):
            out.append((p, 'LEN', len(a), len(b))); return
        for i, (x, y) in enumerate(zip(a, b)): walk(x, y, '%s[%d]' % (p, i), out)
    elif a != b:
        out.append((p, 'LEAF', a, b))


def lookup(root, path):  # '$.a[3].b' -> node
    node = root
    for part in path[2:].replace('[', '.[').split('.'):
        if not part: continue
        node = node[int(part[1:-1])] if part.startswith('[') else node[part]
    return node


pkg_sha = {}
for f in changed:
    if not f.endswith('MANIFEST.json'):
        pkg_sha[os.path.basename(f)] = (sha(git('show', BASE + ':' + f, binary=True)), sha(open(f, 'rb').read()))

tally = collections.Counter()
for f in changed:
    old = json.loads(git('show', BASE + ':' + f))
    new = json.load(open(f, encoding='utf-8'))
    diffs = []
    walk(old, new, '$', diffs)
    for p, kind, a, b in diffs:
        key = p.rsplit('.', 1)[-1]
        verdict = 'UNEXPECTED'
        parent = lookup(new, p.rsplit('.', 1)[0]) if kind == 'LEAF' else None
        if (kind == 'LEAF' and key in ('x-rigSha256', 'sha256') and (a, b) == (OLD_RIG, NEW_RIG)
                and isinstance(parent, dict) and parent.get('rigSource') == RIG):
            where = 'entities[*]' if p.startswith('$.entities[') else p[2:].rsplit('.', 1)[0]
            verdict = 'prop-rig pin %s.%s' % (where, key)
        elif kind == 'LEAF' and f.endswith('MANIFEST.json') and key == 'sha256':
            entry = lookup(new, p.rsplit('.', 1)[0])
            name = os.path.basename(str(entry.get('file', '')))
            if name in pkg_sha and pkg_sha[name] == (a, b): verdict = 'manifest-sha(%s)' % name
        tally[(f, verdict)] += 1
        if verdict == 'UNEXPECTED':
            bad += 1
            if bad <= 25: print('  UNEXPECTED', f, p, kind, str(a)[:90], '->', str(b)[:90])
    print('%-55s %d differing leaves' % (f, len(diffs)))
for (f, v), n in sorted(tally.items()): print('   %-55s %-45s %d' % (f, v, n))
if any(f.endswith('MANIFEST.json') for f in changed):
    f = next(f for f in changed if f.endswith('MANIFEST.json'))
    old = json.loads(git('show', BASE + ':' + f)); new = json.load(open(f, encoding='utf-8'))
    for eo, en in zip(old.get('packages', []), new.get('packages', [])):
        print('   manifest %-22s ' % en.get('file') + ' '.join(
            '%s %s->%s' % (k, eo.get(k), en.get(k)) for k in en if k not in ('region', 'file', 'sha256')))
print('RESULT:', 'PROVENANCE-ONLY' if bad == 0 and changed else 'NOT PROVENANCE-ONLY (%d unexpected)' % bad)
sys.exit(0 if bad == 0 and changed else 1)
```

</details>
