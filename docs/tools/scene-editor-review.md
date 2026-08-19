# Scene Editor v1 — review: can the owner author scenes outside Unity?

- **Reviewed:** `Downloads/Pixel art capabilitiessceneeditorreviewpackage.zip` — `Scene Editor
  (standalone).html` (1.15 MB, 51 rigs inlined), `sample-scene.json` (`hiddenharbours.scene/1`),
  `README.md`.
- **Date:** 2026-08-18 · **Lane:** tools-editor · **Ruling owed by:** lead-architect (new authored-data
  contract = cross-cutting) and the owner (the height question, §7).
- **Status:** review only. No importer code in this PR.

---

## 1. The verdict

**Yes — usable, for one specific job, behind an importer that refuses bad input. No, it must never
become the source of truth for a region.**

The tool is better than it needed to be. It renders from the real rigs, it measures cell geometry
from the art instead of storing it, its pivot maths is exactly ADR 0026, and its cell metrics agree
with the road kit's real 32 × 64 geometry to the row. Those are not small things — they are the
things that are hard to get right, and they are right.

What it is **good for**: the artistic layer. Decor props, scatter, planting hints, path and clifftop
*centrelines* — the composition work that is genuinely faster with a brush and a palette than with a
C# builder, and that nothing in the simulation reads.

What it must **never** do: own terrain height, own gameplay placements that carry save identity, or
hand the engine rasterised cells as truth. Each of those is a second definition of something the repo
already defines once, and the repo has been bitten by second definitions before.

The honest caveat, and it is the finding that matters most: **the parity premise is real but
unpinned.** 35 of the 51 rigs inlined in the editor are byte-identical to the repo's. 15 are not, and
none of those 15 matches *any* commit in the repo's history — they are divergent working copies, in
both directions. One rig in the palette does not exist in the repo at all. Nothing in the export
records which rig bytes produced it. Until that is pinned, "it renders from the same rigs" is true of
two thirds of the palette and unverifiable for the rest (§6.1, §8.1).

**Recommendation:** approve a narrow spike (§9) — splines + decor props, one region, behind a Dev
menu, non-destructive. Do not approve terrain, and do not approve entity import for anything the save
system keys on.

---

## 2. What I actually ran

Every claim below is measured. The method, so it can be re-run:

| Check | How |
|---|---|
| Rig geometry (W/H/pivot) for 8 families | Standalone ClearScript V8 harness over the repo's own `Assets/_Project/Plugins/Editor/JsEngine` DLLs, running `docs/art/rigs/*.js` unmodified (~2 s, no Unity) |
| Rig parity | Decoded the editor's `__bundler/manifest` (gzip+base64), split the 2.06 MB concatenated rig bundle on its `/* ===== Art/x.js ===== */` markers, compared each against the repo LF-normalised |
| Drift direction | `git hash-object` of each bundled rig vs **every** blob for that path in `git rev-list --all` |
| Region sizes | `Assets/_Project/Data/Regions/*.asset` vs `RegionDef.cs` field initialisers |
| Export integrity | Decoded the RLE streams and checked they cover `cols × rows` |

The bundle-splitting is trustworthy because 35 files came out byte-identical; a faulty extraction
would not produce exact matches.

Harness source is throwaway (scratchpad) — if this becomes routine, it belongs in
`Code/Tools/Editor/RigBaking` next to the existing bake menus.

---

## 3. The architectural frame — with one correction

The handoff framed it as: *scenes are generated; C# builders materialise regions from data; therefore
the editor's JSON must enter as authored data a builder consumes.*

That is correct about **today** and I have verified it — `StPetersBuilder` and `NineMileCreekBuilder`
both call `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, ...)` and rebuild from zero. But it
is not the whole picture, and the difference changes what question the owner is really being asked.

**`docs/adr/0019-hand-authored-scenes-and-refresh.md` already decided the opposite** — that a
region's committed `.unity` becomes the source of truth, that builders CREATE once and then REFRESH
logic only, and that *the owner designs the levels himself, in Unity, with no AI in the loop*. It has
been sitting at **"Proposed — awaiting owner sign-off"** since 2026-07-04 (merged as docs in #153).
Phase 1 was never built: the only Refresh command in the codebase is still `Refresh Cove Logic`, the
ADR 0011 pilot. Meanwhile the builders kept being written — the NMC arc, the camper lots, the road
painter — all of them from-zero rebuilds, each one carrying an "ADR 0019 §1 guard" comment
acknowledging that it wipes hand work.

So the Scene Editor is not arriving into a settled architecture. It is a **third answer to a question
that has been open for six weeks**:

1. **Builders generate the scene** (today's reality) — the owner tunes data, agents write C#.
2. **The owner authors in Unity, builders refresh logic only** (ADR 0019, unsigned) — the owner
   paints and places in the editor he already has.
3. **The owner authors outside Unity, an importer feeds the builders** (this package).

These are not mutually exclusive — (3) can feed either (1) or (2) — but the owner should know that
approving an importer does not resolve (1) vs (2), and that ADR 0019 is the ruling he actually owes.
If he signs ADR 0019, the Scene Editor's natural output is a scene he then hand-finishes in Unity. If
he rejects it, the Scene Editor's output is builder input forever. **Either way the importer's shape
is the same**, which is why the spike in §9 is safe to build before that ruling — but the *long-term*
role of this tool depends on it.

> **Ruling requested (lead-architect):** does ADR 0019 get signed, rejected, or superseded? The
> Scene Editor makes the question concrete but does not answer it.

---

## 4. The ownership line

The rule that makes this tool viable at all is CLAUDE.md rule 2 — content is data. The rule that
keeps it safe is narrower: **the editor may author things nothing in the simulation reads.**

| Layer | Owner | Why |
|---|---|---|
| Terrain **height** | Unity Terrain Paint Tool (ADR 0014) | The sim samples this map. Paint = sail. §7. |
| Ground / cliff **tile paint** | Contested — see §7 | Today it is derived from height. A second painter is a second truth. |
| Road & path **centrelines** | **Editor** (authored) | A curve is authoring, not rasterisation. §5, Q5. |
| Road **cells** | Builder (`RoadKitContract`) | Blob-47 correctness is combinatoric; the repo refuses to re-derive mask→index twice. |
| Decor props, scatter, planting hints | **Editor** | Pure composition. Nothing reads it. This is the win. |
| Lots, moorings, NPC stations, gameplay markers | Builder | These carry laws and tests. |
| Anything with **save identity** | Builder, always | ADR 0020 (world-placed object persistence) keys on stable ids. An editor-generated `dory_001` is not a stable id — it is an ordinal that renumbers when the artist deletes an earlier entity. |

That last row is the one to hold hardest. `id: "dory_001"` is `family + index`, assigned at export
time. Delete `dory_001` and re-export, and the old `dory_002` becomes `dory_001`. Anything that
persisted state against that id now points at a different boat. Editor ids must be treated as
**export-local ordinals**, never as world identity — the importer should either refuse to place
save-bearing entities or mint proper ids on the Unity side.

**On NPC routes specifically:** `Assets/_Project/Code/World/Routines/RoutineLanes.cs` already exists on
main and already holds node positions, a parent tree, names and flattened bend points — the polylines
the editor's `paths` would duplicate. Roads and lanes are *already* two tables today
(`NineMileCreekRoads` decides every cell without consulting lanes); importing editor paths as a third
would make it worse. If path import lands, it should converge on the lane table, not sit beside it.

---

## 5. The six README questions

### Q1 — Is RLE-per-layer the right terrain wire format?

**Yes, with two fixes.** The format is fine; this export is not internally consistent.

- **The road layer's RLE does not cover the grid.** Its runs sum to **18,879** cells of a
  160 × 120 = **19,200**-cell grid — it stops 321 cells early, at row 117. Ground and cliff both
  terminate at exactly 19,200 with a trailing zero run. So the format has no stated termination rule
  and this export uses two different ones. A decoder cannot know whether a short stream means
  "remainder is empty" or "truncated". **Fix: require `sum(runs) == cols × rows` and refuse
  otherwise.** Cheap, and it turns a silent wrong-scene into a loud failure.
- **`stats.tiles` is not a cell count.** It reports `road: 196`; the RLE holds **186** painted road
  cells. `paths[0].tiles` also says 196 — consistent with a stamp count that double-counts cells hit
  by overlapping stamps along the curve. Harmless as a stat, dangerous as a checksum. Either make it
  a true cell count or rename it so nobody validates against it.
- Minor: the README says the sample's 2520 ground tiles compress to **71** runs; the actual export
  has **91**. The prose has drifted from the build.
- The nested `pieces` block is shaped `{note, legend, rle}` while a layer's own `rle` is a bare
  array, and the two legends use different key conventions (prefixed strings `g1`/`r3` vs numeric
  strings `1`…`12`). Not wrong, but one convention would be kinder to a parser.

**Sizing** is not the problem. Row-major RLE's worst case is thin diagonal features on a wide grid —
a road crossing St Peters costs ~2 runs per row plus gaps, and at 760 × 520 that is a few thousand
runs, tens of KB. A dense village ground fill compresses far better. Don't switch to chunks for perf;
switch only if you want partial/streaming updates, which the "one region, one document" model does
not need yet.

### Q2 — Does the `call` record work as an engine contract?

**No — resolve it at import, and keep the record only as provenance.**

The `call` record is a good *debugging* artefact and the right thing for the editor to emit. But as an
engine contract it has three problems:

1. **`rigSource` paths are flattened and do not resolve.** The export says `Art/cliffRig.js`; the
   repo's file is `docs/art/rigs/cliff-face-kit/bake/cliffRig.js`. `Art/roadPathRig3.js` is really
   `road-path-kit-v3/roadPathRig3.js`. Two of the eight rigs in the sample lose a subdirectory. Worse,
   `docs/art/rigs/roadPathRig.js` (the **retired v1** kit) sits at the top level, so a naive resolver
   that strips `Art/` and looks in the rig root can land on the wrong kit generation for a
   near-miss name.
2. **The repo does not invoke rigs this way at runtime.** Rigs are baked to sprite sheets by the
   Unity bakers (ADR 0021); slicing, import settings and metas live there. An imported entity should
   become a **resolved def/prefab id plus validated options**, not an instruction to call a JS
   function.
3. **The options are unvalidated against the rig's real axes.** Nothing checks that `build: 'oars'` is
   in `DoryIso.BUILDS`, and the rigs fail *soft* — the road kit's `seed: 0` silently means 7, and a
   mistyped species key has previously made a rig render a different object at another cell size
   rather than throw.

**So:** import should map `family` → a def/prefab the repo owns, validate every `opts` key against
the rig's enumerated axes (which the harness can read directly), and **refuse** on an unknown key
rather than passing it through. Keep `rig`, `rigSource` and `call` in the document as provenance —
they are exactly what you want when an import looks wrong — but let the pipeline decide how the art
is produced.

### Q3 — Which pivot, and is 32 px/m the number?

**Ship `unityPivot` only. Yes, 32 is the number.**

- The pivot maths is **exactly right**. `unityPivot = [px/w, (h − py)/h]` is ADR 0026's
  `(H − pivotY)/H`, the convention two sessions were spent settling. I verified it against five rigs
  measured live in V8 — dory (156 h, pivot y 88 → 0.435897), lobster boat (420/258 → 0.385714),
  character (92/82 → 0.108696), pot (36/32 → 0.111111), rock (44/44 → 0). Every one matches the
  export to six decimals. This is the single strongest signal that the tool was built against the
  repo's real conventions rather than around them.
- Ship **only** the normalised bottom-left form. The top-left `pivot` is the rig's own coordinate and
  is already recoverable from `cell` — shipping both invites a consumer to pick the wrong one, and
  Unity wants the normalised form anyway.
- **32 px/m is correct and safe to bake in.** It is `CameraFollow.AssetsPPU`, `const int = 32`,
  commented "one PPU never changes". Do not be confused by the other 32-vs-24 pixel grid in this
  repo: `_PixelsPerUnit = 24` is the **water shader's sampling grid**, a different quantity that has
  never been the sprite PPU. The export is on the right one.
- ⚠ **But see §8.1 — the pivot is only *measured* for rigs that publish a static one.** The formula
  is right; one class of input to it is wrong.

### Q4 — Cliffs as tiles-plus-`rows`, or real elevation?

**Real elevation is needed before this drives gameplay — but that does not mean the editor should
author it.** This is the deep one; §7 lays out the options and the owner's call.

For the narrow question: `rows` + `aspect` + `step` is a faithful description of the *cliff kit's*
inputs, and the export's `pieces` legend is exactly `CliffRig.PIECES` in order (verified in V8:
`faceS, cornSW, cornSE, innSW, innSE, sideW, sideE, diagSW, diagSE, roundSW, roundSE, notch`,
1-indexed). So as a *picture* the cliff data is honest and rig-derived. It is simply not elevation,
and the sim reads elevation.

### Q5 — Should authored splines be authoritative?

**Yes. Import the curve; let Unity rasterise. This is the clearest call in the review.**

The road kit v3 already has the proven contracts and they cannot be reproduced in the editor's
preview:

- **A road is two tilemaps.** Each cell contributes a TOP (headroom + ground square) and a SKIRT
  (kerbs, south drop faces, contact shadow), painted to `RoadTop` at sorting order −17 and
  `RoadSkirt` at −16. *No single tilemap does that at any sort order* — painter-order over whole
  cells lets the southern tile erase the northern tile's kerbs. The editor draws whole cells, so its
  preview is structurally incapable of matching the shipped composite wherever there is height.
- **Blob-47 correctness is combinatoric.** A tile from the wrong atlas cell is a perfectly good tile
  that passes every per-tile check and fails only at junctions. That is precisely why C# never
  re-derives mask→index — `RoadKitContract` looks up the rig's own exported masks. A second
  rasteriser is a second definition of a law the repo deliberately keeps in one place.

The good news: the export does **not** ship a mask index for roads — the RLE carries only the surface
material. So the engine derives the index through the proven contract regardless. The cells are
therefore *safe* to import, they are just **redundant**, and they will diverge from what Unity paints.

Treat the editor's road cells as a **preview artefact**: import them if useful for diffing, never as
truth. Say so in the schema — an unmarked derived field will eventually be trusted by someone.

(Cliff lines are the same shape of answer, with one difference: the cliff layer *does* ship authored
`pieces`, and `0 = autotile`. Those are real authoring decisions — a nose rounded here, a notch there
— and are worth importing as **overrides** on top of engine autotiling.)

### Q6 — Is scene import worth building?

**Yes — and it is the difference between a toy and a tool.** Without it, every scene is a one-way
trip: the artist cannot open last week's work, cannot correct an import, and cannot iterate after
anything changes on the Unity side. The README notes the reader already exists internally
(round-tripped in the export preview), so this is not a large piece of work.

But **round-trip is not the same as the engine becoming the source of truth**, and the two should not
be conflated. The right model:

- The `.scene.json` is the **artistic layer's** source of truth, committed to the repo, editable in
  the editor, re-importable.
- The Unity scene is the **assembled** result, and everything the builder owns (§4) lives only there.
- Import is therefore **non-destructive and scoped**: it touches its own tagged root and nothing else
  — the same reconciler shape as `Refresh Cove Logic`, which already works and is already test-pinned
  (`CoveLogicRefreshTests`).

That model also answers the README's "single-user, no merge story" limit: a committed JSON document
diffs and merges far better than a `.unity`, which is the whole reason scenes were kept out of git in
the first place.

---

## 6. The drift list — every table the editor hardcodes about the world

The handoff asked for this enumerated. Anything here will drift again unless it is derived or
validated.

### 6.1 Rig sources — 51 inlined, 16 not matching the repo

| | Count | Detail |
|---|---|---|
| Byte-identical to repo | **35** | The parity claim, honoured |
| Divergent | **15** | `buoyIsoRig`, `capeIslanderIsoRig`, `deckGearRig`, `lobsterBoatIsoRig`, `shipyardIsoRig`, `shoreFindsRig`, `shorePlantRig`, `shrubIsoRig`, `sportSkiffIsoRig`†, `trapIsoRig`, `trayIsoRig`, `treeIsoRig2`, `utilityIsoRig`, `wharfDecorRig`, `wharfIsoRig` |
| Not in the repo at all | **1** | `grassTuftRig.js` (repo has `grassRig.js`, `grassSpeciesRig.js`) |

† **`sportSkiffIsoRig` is divergent by *filename* only — its content landed.** The count of 15 is a
filename count and stays literally true, but this entry is resolved and owes the art workspace
nothing. See the "Editor ahead — withdrawn" bullet below.

**Fourteen of the 15 match no blob in the repo's history *for their own path*** — so those are not
"the editor is behind at commit X"; they are working copies from the art workspace that never landed.
The fifteenth, `sportSkiffIsoRig`, is the exception, and it exposes a blind spot in the method: §2
hashed each bundled rig against every blob **for that path**, which cannot see a file that landed
under a different name. The other fourteen **have** since been re-checked by content — each was
matched against the whole of `docs/art/rigs/**`, and each best-matches its own same-named file
(ratio 0.83–0.99), with no near-match anywhere else. So the rename explanation is **excluded** for
them and their rows stand; the sport skiff is the only rename case. The drift runs **both ways**:

- **Repo ahead:** `lobsterBoatIsoRig` gained the 12-scheme `paint` axis (`PAINTS` / `paintRamps`) —
  a shipped feature the editor's copy predates, so *the artist cannot choose a hull colour in the
  editor at all*. `trayIsoRig` gained the `keyline`/`outline` alias fix (#463, #477) and now defers to
  `isoSolid`'s `KEYLINE_DEFAULT`; the editor's copy hardcodes `keyline: false`, so a tray previews
  differently from how it bakes.
- **Editor ahead — *withdrawn*.** This review first read `sportSkiffIsoRig` as **PASS 5** in the
  editor (66 KB) against **PASS 1** in the repo (19 KB, untouched since #227) and concluded that four
  passes of rig work existed only in the art workspace. **That conclusion is wrong.** The editor's
  PASS 5 *did* land — as `docs/art/rigs/sportSkiffMk2IsoRig.js` (67,537 B), which is a **ruled second
  hull**, not a replacement. The editor's copy differs from the landed rig by a **two-line rename**:
  #534 re-issued the exported global as `SportSkiffMk2Iso` because the Mk2 was installing
  `globalThis.SportSkiffIso` — the shipped skiff's name — and that commit moved zero pixels
  (LF sha256 `2aa8fe2b…` → `fc0dcbc9…`). `sportSkiffIsoRig.js` remains PASS 1 **by design**: it is the
  original 7.0 m hull and still the shipped one, and the Mk2 is its sibling. **Do not land the
  editor's copy over `sportSkiffIsoRig.js`** — there is nothing to reconcile on this file.

**What survives is a lesson about the audit, not a debt owed by the art workspace.** A rig that
landed under a **new name** was invisible to a path-scoped comparison, exactly as a rename always is.
Drift audits must therefore match by **content across the whole history** — hash first, path second
(`git rev-list --all --objects`) — which is precisely how this was caught. Recorded on the merge of
#571:

> "the 'editor ahead: sportSkiff PASS 5 never landed' claim is wrong — PASS 5 landed as
> `docs/art/rigs/sportSkiffMk2IsoRig.js` (#534 renamed the global to fix a collision; only the
> identifier differs). The comparison was by filename, so the rename read as unlanded. The other
> divergent-rig findings stand. Nothing is owed by the art workspace on that file."

**Fix direction — the repo already has the idiom.** Road kit v3 pins its rig by LF sha256
(`d45e9ac6…`) and the sidecar convention treats an absent hash as grounds for refusal. Apply it here:
the export should carry, per rig it references, the **LF sha256 of the rig bytes that produced it**;
the importer recomputes against `docs/art/rigs/**` and **refuses on mismatch**. That converts an
invisible parity drift into a loud, specific error naming the rig. It also makes the editor's build
step self-checking — a build that inlines a stale rig fails at bundle time, not months later in a
scene.

### 6.2 Region table — two of three wrong, one missing

| Region | Editor | Repo truth | |
|---|---|---|---|
| St Peters | 760 × 520 | 760 × 520 | ✅ |
| Nine Mile Creek | 160 × 120 | **760 × 560** | ❌ |
| Coddle Cove | 320 × 240 | **160 × 120** | ❌ |
| The West Water | *absent* | 760 × 520 | ❌ |

Two details make this more instructive than a stale number:

- **160 × 120 is the `RegionDef.cs` C# field initialiser** (`WorldSizeMeters = new Vector2(160f,
  120f)`). The editor's NMC entry is the class default, not any region's real size.
- **Coddle Cove's asset omits `WorldSizeMeters` entirely**, so it *is* 160 × 120 at runtime — the C#
  default. The editor says 320 × 240. So the editor is wrong about both regions, in opposite
  directions, and one of them is wrong about a value that only exists as a code default.

This matters more than it looks: `terrain.cols/rows` and `originNW` are derived from the region size.
A Nine Mile Creek scene authored in the editor gets a 160 × 120 canvas with `originNW = [-80, 60]`,
when the real region is 760 × 560 with a north-west corner at `[-380, 280]`. So the artist can reach
**under 5 % of the region's area** (19,200 m² of 425,600 m²), and every cell they paint resolves to
the wrong world position —
the canvas is both too small and in the wrong frame.

**Fix:** derive the region table from `Data/Regions/*.asset` at editor build time, and have the
importer validate `region.worldSizeMeters` against the `RegionDef` and refuse on mismatch. Deriving
alone is not enough — an editor built today and used in three months is stale again.

### 6.3 Other hardcoded tables

- **`gameplaySidecar` paths** are a hardcoded `family → filename` map (`camper: 'camperIsoRig'`,
  …), and carry the same flattened-path problem as `rigSource`. Derive from the rig list.
- **Palette items (~490)** encode each family's option axes. These *are* read from the inlined rigs at
  runtime, so they track whatever rig the bundle holds — which is the §6.1 problem again, not a
  separate one.
- **`frame.camera`** cites "ADR-0006/0022" as prose. Fine as documentation; just never parse it.

---

## 7. The height gap — the owner's call

This is the one the review cannot decide, and it should not pretend to.

**The situation.** In this game terrain is *elevation*, not pictures. `PaintedHeightMap` holds an R8
texture plus a world rect and an elevation range (St Peters: 1520 × 1040 texels over 760 × 520 m,
−4 m to +6 m). ADR 0014 makes that one map serve render and sim together — **paint = sail** — and the
coastline itself is an iso-contour of painted height, which is why the shoreline reads as organic
rather than tiled. The Scene Editor's ground and cliff layers are *pictures of* terrain. Its height
overlay is explicitly a preview aid and does not export.

So the editor cannot currently author terrain, and the question is what to do about it.

**(a) Editor stays props/roads/decor only; terrain stays in the Unity paint tool.**
Cheapest, safest, ships now. The artist composes in the editor and paints height in Unity — two
tools, one boundary, no possibility of two terrain truths. Cost: the editor's ground/cliff painting
becomes a *sketching* aid whose output is discarded, which is a real disappointment if the owner
enjoys painting there.

**(b) The editor gains a height layer that exports; tiles become derived from it.**
The principled answer. It matches how the game already works — paint height, let the look fall out —
and it would let the artist author the coastline shape where they are composing everything else. Cost:
a real feature in the editor (a height brush that is pleasant to use is not a small job), plus an
export/import path for a height raster, plus the ground tiles become preview rather than authoring.
This is the option that could eventually replace the Unity paint tool rather than sit beside it.

**(c) The importer infers height from tiles. — Recommend against.**
It sounds convenient and it is a trap. Tile paint is lossy about elevation (a "cliff" tile with
`rows: 3` says how tall the *art* is, not what the ground does between cells), and inferring a height
field from it would produce a *second* terrain truth that disagrees with the painted map wherever both
exist. The failure mode is the worst kind: the coastline looks approximately right and the sim
disagrees with it, so boats ground where there is visibly water. Only revisit this if something
surprising makes it sound.

**My recommendation: (a) now, (b) as a later arc if the owner finds he wants to shape coastline in the
editor.** They are compatible — (a) is a strict subset of (b), and nothing in (a) has to be undone to
get to (b). What must not happen is drifting into (c) by accident, which is what "just import the
cliff tiles and see how it looks" turns into.

> **Owner's call:** (a) or (b)? Everything else in this review works either way.

---

## 8. Defects found

### 8.1 ⚠ The pivot is wrong for rigs that publish their pivot at render time

**Measured, not inferred.** `RockIso` publishes no static `pivot` global — it returns anchors per
render. Running the exact call the export records
(`RockIso.render({key:'Erratic',variant:0,stone:'sandstone',dress:0,tide:'dry'})`) in V8:

```
rig says   anchors.pivot = { x: 26, y: 34 }     ← the ground contact row
export says      cell.pivot = [ 26, 44 ]        ← the cell bottom
                unityPivot = [ 0.5, 0 ]         ← should be 0.2273
```

A **10 px error at 32 px/m = 0.3125 m**. Row 44 is not even the sprite's lowest opaque row (opaque
rows run 16…42) — it is the cell edge, i.e. the fallback.

**Root cause:** the string `anchors` does not appear anywhere in the 1.15 MB editor. It never reads
any rig's anchor block. For the rigs that publish `W`/`H`/`pivot` as globals — dory, lobster boat,
character, pot, all verified matching — the README's "measured from the rig, cannot drift" claim is
exactly true. For render-anchored rigs it silently falls back to the cell box, **and nothing in the
export distinguishes the two cases.**

This is not a rock-only bug. The pattern (pivot = ground contact, published per render) is how the
nature and prop rigs generally work, and the same fallback would misplace any of them. Note also that
the rock's *inline* `footprint` block ships the correct `pivot: {x:26, y:34}` right next to the wrong
`cell.pivot` — the export literally contains both answers.

**Fix:** read `anchors.pivot` when present, in preference to any global; and add a per-entity flag
recording which source was used, so the importer can refuse the fallback rather than place art a third
of a metre out.

### 8.2 The road RLE does not cover the grid

Covered in Q1 — 18,879 of 19,200 cells, no stated termination rule, and `stats.tiles` disagreeing with
the stream. Grouped here because it is a defect, not a design question.

### 8.3 Entity ids are export-local ordinals presented as identity

Covered in §4. `dory_001` renumbers on delete. Not a bug in the editor — it is a correct choice for a
document — but it becomes one the moment anything persists against it.

---

## 9. The spike, scoped (build only on lead-architect + owner nod)

**Smallest slice that proves the contract end to end, and is useless if the contract is wrong:**

> Import **path/road centrelines** and **decor props** from a `hiddenharbours.scene/1` document into
> **one region** (Nine Mile Creek — it already has the road painter and the wharf yard), behind
> `Hidden Harbours ▸ Dev ▸ Import Scene Document…`, writing only into its own tagged root and leaving
> every other object untouched.

**In scope**

1. **A validating reader that refuses.** Refusal is the feature; the importer is worth nothing if it
   places a scene it should have rejected. Refuse on: region size ≠ the `RegionDef`; any RLE whose
   runs do not sum to `cols × rows`; a rig sha256 that does not match `docs/art/rigs/**`; an unknown
   `opts` key for a family; a `cell.pivot` that came from the fallback (§8.1).
2. **Paths → the existing rasteriser.** Import `nodes` + `material` + `widthMeters`; hand them to
   `NineMileCreekRoads`/`RoadKitContract` and let Unity paint both tilemaps. Ignore the imported road
   cells entirely.
3. **Decor props → resolved prefabs.** A `family → prefab/def` map on the Unity side, per-instance
   options validated against the rig's real axes, `unityPivot` and `sortBias` applied
   (`sortBias` maps naturally onto `YSortSprite._baseOrder`, which is already a per-instance
   serialized field defaulting to `SortingBands.DecorBase`).
4. **Non-destructive re-import.** Same reconciler shape as `Refresh Cove Logic`: destroy and
   regenerate only the tagged subtree, idempotent, painted layer untouched. Pin it with an EditMode
   test the way `CoveLogicRefreshTests` pins the cove.

**Out of scope** — terrain of any kind, cliffs, entities with save identity, and any change to the
`.unity` outside the tagged root.

**Prerequisite on the editor side** (not our lane, but the spike is weaker without it): the export
should carry rig sha256s, and §8.1's pivot fix should land, or the importer's refusal rules will
reject most real exports.

**Roughly:** the reader + validator is the bulk of it; paths and props are small once the rigs
resolve. It is a contained piece of work, and every part of it is reusable if the contract later
widens.

---

## 10. An ADR will be needed

If the verdict is accepted, this introduces a new authored-data contract (a committed
`hiddenharbours.scene/1` document that a builder consumes) and a new refusal seam. That wants an ADR,
drafted but **not signed**, once lead-architect has ruled on §3 and the owner on §7 — those two
answers change what the ADR says. Deliberately not drafted here rather than guessing at both.

---

## 11. What I did not check

Stated so nobody reads more assurance into this than it carries.

- **I did not run the editor.** The HTML runs local-only and was not opened; every finding above comes
  from its source, its bundle, and the sample export. Its *interaction* — whether it is pleasant to
  paint in — is unassessed and is the owner's to judge.
- **I did not verify rendered pixel parity.** I verified that the rig *bytes* match for 35 of 51 and
  that geometry (W/H/pivot/cell metrics) matches for every rig I probed. I did not render a cell in
  both the editor and the baker and compare pixels.
- **I did not check the ~490 palette items** individually against their rigs' real option axes — only
  the five families in the sample export.
- **No Unity run.** Nothing here required one, and nothing in this PR touches Unity assets.
