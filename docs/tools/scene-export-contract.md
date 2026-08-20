# `hiddenharbours.scene/1` — the contract

**What this is.** The scene editor's package format. It began as a reconstruction from
[`scene-editor-review.md`](scene-editor-review.md) (#571, corrected by #576) with seventeen
fields the review named but never specified; **lead-architect settled all seventeen on PR #588**
from the editor's own reference package. That package's `sample-scene.json` is now committed at
[`reference/sample-scene.json`](reference/sample-scene.json), so every row of §2 is checkable
against bytes in this repo rather than quoted second-hand — and the exporter's tests compare
their output to it block for block. The exporter at `tools/scene-export/` implements exactly
this.

**Lane:** tools-editor. **Direction:** outbound only. Import stays gated (review §9).

---

## 0. The two rulings that shape everything else

**Unknown keys MUST be ignored, and `x-` is the reserved extension prefix.** Nothing can refuse
them today because nothing *reads* a package: the standalone editor ships `doExport()` and
`copyExport()` and has no import branch at all — no `FileReader`, no file input, no path from
parsed JSON into editor state. The format is outbound-only on both ends. The eventual repo-side
importer is ours to write, and this is its first rule.

**`call` / `opts` are write-only.** The editor renders from its own live state
(`RigKit.rigNow(...)`) and `buildExport()` writes the `call` record *out of* that state; no
renderer reads one back. An export without them is valid. The cost, and it is a real one: a
document with no `call`/`opts` can never seed a round-trip **into** the editor, whose internal
model is family + dir + opts.

## 1. The envelope

```
schema        'hiddenharbours.scene/1'          ← the key is `schema`, not `format`
generatedBy   string
generatedAt   ISO-8601
region        { id, sceneName, worldCenter, worldSizeMeters }   ← id in def form
frame         { ppu, cellMeters, originNW, axes, camera, sort }
terrain       { cols, rows, note, legend, layers }
entities      [ … ]
cliffLines    [ … ]
paths         [ … ]
collision     { note, sidecars }                ← derived, never authored
stats         { entities, tiles: { ground, road, cliff } }
```

## 2. The seventeen, settled

| # | Question | Ruling, from the reference package |
|---|---|---|
| 1 | Are unknown keys tolerated? | **Yes — nothing reads a package at all.** Keep `x-`; it is the reserved extension prefix. See §0. |
| 2 | Does an entity render without `call`/`opts`? | **Yes.** `call` is written out of live state, never read back. See §0. |
| 3 | Where does an entity say it is? | **`pos: [x, y]`, metres, origin = region centre.** `frame.axes` verbatim: `'+x east, +y north, origin = region centre'`. |
| 4 | RLE encoding | **Pairs**: `[[0, 5492], ["g1", 56], [0, 104], …]`. |
| 5 | Legend | **One top-level `terrain.legend`**, prefixed string keys (`g1`–`g6`, `r1`–`r11`, `c1`–`c3`), each value `{layer, rig, rigSource, material}` — and those keys **are** the RLE values. Layer objects carry **no** legend. The numeric convention exists only in `cliff.pieces.legend` (`"1"`–`"12"` → piece names), whose RLE values are numbers. *(The review's per-layer-legend claim was wrong.)* |
| 6 | Row order | **Row 0 = north edge**, per `terrain.note`. |
| 7 | Termination, and what `0` means | `0` is a real RLE value meaning **no tile (water — never baked, the shader owns it)**, and it counts toward full coverage. The sample's first ground run is `[0, 5492]`. |
| 8 | `layers` container | **Map keyed by layer name.** |
| 9 | `paths[]` shape | `{id, layer, material, widthMeters, closed, curve: {kind: 'catmull-rom', uniform, tangentScale}, nodes: [[x,y]… metres], polyline: [[x,y]… derived]}`. Sibling top-level `cliffLines[]` is the same shape plus `landSide`, `corners`, `tiles`. |
| 10 | `family` vocabulary | The editor's `RigKit.byId` ids — `dory`, `punt`, `lobsterboat`, `capeislander`, `sportskiff`, `console`, `camper`, `character`, `pot`, `rock`, … No reader exists to refuse an unknown one. |
| 11 | Is `cell` required, and does it need `pivot`? | **Nullable**, by the editor's own hand: `cell: b && b.ok ? {w, h, pivot: [px, py], pxPerM, unityPivot} : null`. Both pivot forms ship together; nothing reads them back. |
| 12 | `sortBias` units | A per-entity **tie-break delta** (`sortBias: e.bias \|\| 0`). `frame.sort`: *"painter, descending world y (north draws first); sortBias breaks ties"*. **It is not `YSortSprite._baseOrder`'s absolute order** — the review's mapping row overstated it. |
| 13 | `footprint` | **Optional**, present only when the family has a footprint fn; the value is the rig's gameplay measurement object (the rock: `{footprint: {rx, ry, ground}, perch, snags, hazard, pool, weedLine, pivot}`). |
| 14 | Top-level envelope | The key is **`schema`**. Full top level in §1. |
| 15 | Container | A bare **`<sceneName>.scene.json`** download. The zip was the review package's wrapper, not the format. No open dialog exists. |
| 16 | `gameplaySidecar` | Per-entity string, from a hardcoded 7-family map (six boats + camper); non-vehicle entities omit it. Plus the top-level `collision.sidecars` array. |
| 17 | `stats` shape | `{entities: N, tiles: {ground, road, cliff}}` — tiles keyed by layer, **stamp counts**, not a checksum. |

## 3. What this export ships, and what it cannot

Honoured exactly: the envelope and every key name above · region frame from the `RegionDef`
(never a hardcoded size) · `sum(runs) == cols × rows` on every layer · row 0 north · `0` as the
reserved no-tile value · one top-level legend · both pivot forms · `sortBias` as a delta from
`SortingBands.DecorBase`, read from the C# rather than hardcoded · rig pinning by LF sha256.

Deliberately absent, each for a stated reason:

- **`call` / `opts`** — §0. Reconstructing an invocation from a baked sheet would be a second
  definition of the bake.
- **`cliffLines`** — empty. Cliff lines are an editor authoring artefact; the repo's cliffs are
  placed `CliffWallSurface` components and ship as entities.
- **`footprint`, `gameplaySidecar`** — both are rig gameplay measurements, and no rig is
  executed here (rigs are hashed, not evaluated).
- **`opts` inside `call`** — see §7.
- **A guessed rig** — a sheet with no sidecar the exporter will trust resolves to `null` and is
  listed by name under `x-provenance`.

See §7 for what the terrain layers now carry. One field is honest-but-mismatched, and every
entity says so: **`family` is the sprite name's
stem, not a `RigKit` id** (§2 #10), because a baked sheet does not record which palette family
drew it. Each entity carries `x-familyIsSpriteStem: true`.

`generatedAt` is the committer date of the newest **input** commit, never the wall clock — a
run timestamp would make the output non-reproducible and the `--check` gate meaningless.

## 4. Ours, and marked as ours

Any key beginning `x-` is this exporter's, not the contract's — permitted by §0 and used for
what the repo can state and the format does not name: `x-provenance` (source commit, the commit
the scene was last banked at, builder drift since, what was read from where, the
unresolved-sheet list), `x-rigs` and `x-rigSha256` (the pin table the review asks for but names
no key for), `x-cellAt` / `x-inBounds`, `x-name` / `x-path` (the scene hierarchy path),
`x-pivotSource`, `x-declaredBy` (which sidecar linked a sheet to its rig), `x-readOnly` /
`x-derived` / `x-authorable`, `x-heightMap`, `x-familyIsSpriteStem`.

## 5. What reading the bytes added

The reference landed in `66f03140` and reading it directly corrected one thing the relay had not
covered and confirmed everything else:

- **`cellMeters` and `originNW` belong to `terrain`, not `frame`.** The ruling gave the top-level
  envelope and `frame.axes`, so the two were plausible in either block and the exporter had them
  in the wrong one. `frame` is `{units, scale_px_per_m, axes, camera, sort, pivots}` — note
  `scale_px_per_m`, not `ppu`.
- **Entities carry `group` and `flipX`**, and the cliff layer alone carries a `pieces` block
  (`{note, legend, rle}`, numeric keys, covering the grid). `paths[]` ends with `tiles`, a stamp
  count.
- **The reference itself violates the coverage rule.** Its road RLE sums to 18,879 of 19,200 —
  exactly the defect review §8.2 reported. So `sum(runs) == cols × rows` is a requirement *on
  us*, not a description of the sample; this exporter satisfies it and a test enforces it.

Four entity fields the format names are absent here for stated reasons: `call` and `opts` (§0),
`facing`/`facingIndex` (the editor's own view state), and `gameplaySidecar` (a rig gameplay
measurement — no rig is executed by this exporter).

## 6. Portability

The output is a pure function of the repo, and that has to hold on any machine, not just the one
that wrote it. A second run on a Windows full-LFS checkout found three ways it did not:

- **The height-map hash.** A Git LFS pointer's `oid sha256` **is** the sha256 of the content, so
  a pointer checkout and a full checkout agree — but only if both report it under one key. The
  document carries `textureSha256` from whichever is on disk, and no longer says which; that was
  a fact about the machine, not about the harbour.
- **Subprocess decoding.** `text=True` alone decodes in the platform locale, and every em-dash in
  a commit subject came back mojibake'd through cp1252. The git calls pass `encoding="utf-8"`.
- **Line endings.** A checkout with autocrlf rewrites the committed packages, and `--check` was
  comparing raw bytes. It now reads with universal newlines — what it means is "does this commit
  still produce this document", not "is your working tree LF" — and `.gitattributes` pins the
  packages to LF as well.

A fourth is not a bug but a boundary, and it needs stating plainly: **determinism is per-commit
*and* per-LFS-state.** The seabed textures are Git LFS objects, so the same commit exports a
contoured ground where their bytes are present and an empty one where they are pointers (§7.3).
Both are correct for what they could read. Two consequences:

- Every package stamps `x-provenance.heightMap.textureBytesRead` (and the same flag under
  `terrain.x-heightMap`), so which of the two you are holding is a fact in the file rather than
  something to infer from whether the ground looks empty.
- `--check` names that cause instead of printing a bare `STALE`, which would otherwise send a
  reader hunting for a scene re-bank that never happened.

This is not hypothetical for CI: `.github/workflows/ci.yml` runs an unscoped `git lfs pull`, so
**a `--check` gate wired into that job would fail against packages generated without the bytes.**
Closing it properly means regenerating the packages in an LFS-present checkout — which would also
ship the real coast rather than an empty layer, and is the better fix of the two.


## 7. Making the package renderable

The editor draws **only** by calling rigs — it loads no images — so an entity with no rig gives
it nothing, and an empty terrain layer draws nothing. Three enrichments, each derived from a
source of truth and each declaring how far it goes.

### 7.1 Entities call a rig

Every entity whose sheet resolves to a rig now carries that rig's **installed global** and a
`call` block. The global comes from `RigCatalog`'s registrations where the catalog knows the rig,
and otherwise from the rig's own `root.X = …` publication — both are declarations, and the
second matters because the catalog registers only the rigs the Unity bakers bake, which is a
fraction of the palette.

**`opts` is deliberately `{}`.** A baked sheet does not record which option axes produced a given
cell, and the rigs resolve an unknown or wrong key as a *silent fallback* rather than an error —
the #571 review measured a mistyped species key rendering a different object at a different cell
size. An empty opts draws the rig's default build, which is true; a guessed one draws something
confidently wrong. Every `call` carries `x-synthesised: true`: it was reconstructed by the
exporter, never recorded from a bake.

### 7.2 Roads are stroked from the declared route table

`NineMileCreekMainland` declares each way's vertices and `NineMileCreekRoads` its surface,
half-width and rank; the exporter strokes them into one-metre cells, lowest rank first. That is a
**surface-material** raster, which is exactly what the format's road RLE carries — the review is
explicit that the export ships no mask index and that blob-47 stays derived in one place.

Ways whose route is *computed* rather than declared are **omitted and named**: the truck-park
spur solves for the nearest point on any road, and the town walks solve from each lot to the
nearest carriageway. St Peters declares no road table at all, and the package says so rather
than shipping an empty layer that reads as an oversight.

### 7.3 Ground is an iso-contour, at two honesty levels

The R8 height texture is a Git LFS object. On a pointer-only checkout the ground layer stays
unpainted with `x-unavailable` naming the reason; on a checkout with the bytes, the exporter
decodes the PNG and bands each cell by the shore map's **declared floor elevations**.

⚠ **That contour is not the ground Unity paints, and the package says so in
`layers.ground.x-derived`.** `ShoreMaterialAt` also wiggles the elevation so the rings meander,
tests a sandbar segment with its own spine rule, and chooses between two band tables by weather
sector. Those are logic, not declarations. Reimplementing them here would be a second definition
of the coastline — the review's §7(c) trap, whose failure mode is a shoreline that looks
approximately right and disagrees with the sim. The contour is true to the height map and
coarser than the paint, and that is the whole of what it claims.

### 7.4 Families are the editor's own wire vocabulary

The editor's `family` list is **closed** — 43 prop families and 5 tile layers, transcribed at
[`reference/family-names.json`](reference/family-names.json). An entity's family is resolved by
normalising its rig's filename (lowercase, drop a trailing pass digit, then the `rig`/`iso`
suffixes) and matching a listed name **exactly**.

**A near miss is never aliased.** `wharfIsoRig` normalises to `wharf`, and the list holds both
`wharfbuilding` and `wharfmodule`; choosing between them is guessing, so those placements keep
the sprite stem, flag `x-familyIsSpriteStem`, and appear under
`x-provenance.entityNotes.unlistedFamilies` with the candidate name and a placement count. That
block is the **request list for the editor side**, not a defect in the export.

Currently unresolved: `wharf` (24 placements, Nine Mile Creek) and `interior` / `interiorprop`
(4 + 26, St Peters — the gap the editor maintainer already named).

### 7.5 Region dimensions stand as exported

760 × 560 for Nine Mile Creek is the `RegionDef`'s truth. The editor's own region table is stale
(review §6.2 measured it at the C# field default). Ruled: **the package declares, the tool
conforms.** Nothing here compensates for a stale table on the other side.


## 8. The editor's second round (relayed 2026-08-20 evening)

Five additions, and one question that had to be measured before it could be answered.

### 8.1 `x-rigVersions` — one hash per family

Top-level, `family -> {rigSource, sha256}`, for the families the scene actually uses. The editor
hashes its own copy and badges a mismatch pink rather than refusing to draw. A family that
resolved through **more than one rig** in a scene gets no single hash — it is reported under
`x-ambiguous` with each rig named, because one number there would be a lie. Neither region hits
that case today; the guard is for when one does.

### 8.2 Entity ids are minted from row identity — and carry no vocabulary

Formerly `family_001` ordinals — the defect the #571 review warns about in §8.3 and the editor
now depends on not having, since its write-back matches our rows by `id`. An id is now
`sha256(path|x|y)[:12]`: the builder names each object and computes where it stands, and that
pair is the row's identity. Measured on both regions — path alone repeats (94 objects are called
`ShorePlants/Eelgrass`), path + position does not collide once.

**No family prefix**, ruled 2026-08-20. A first draft read `{family}_{hash}`, matching the
reference package's `character_001` shape — but that re-keyed a row whenever a *vocabulary*
ruling renamed its family: 30 rows when the editor published `interior`/`interiorprop`, 24 more
waiting on `wharf`. Stability is the whole point of the field, and the entity already carries
`family` separately, so the id carries content identity alone.

Twelve hex characters rather than ten. Width can only ever be widened by re-keying, and this
ruling was the one moment re-keying was free; 48 bits leaves a far larger scene than either of
these clear of the birthday bound instead of parking a forced re-key in a future region.

### 8.3 `terrain.waterLevelMeters` — and why one number needs three

`RegionDef.TideMeanLevel`, in metres relative to **chart datum** — the same datum the height
map's elevations use, so elevation and water level compare directly. Both regions declare `0`.

One number is what was asked for and one number ships, but a wash drawn at it is drawn at *mean*
water, not the water now: `TideModel.Height` is `MeanLevel + amplitude * carrier`, and both
regions declare an amplitude of 2.2 m — a 4.4 m swing the mean says nothing about. So
`terrain.x-tide` carries the amplitude and phase alongside. The exporter states the model's
declared terms and never evaluates it: the tide is recomputed from `(worldSeed, gameTime)` and
never stored (CLAUDE.md rule 5), and this document has no clock.

### 8.4 `terrain.x-heightField` — a wash, not a survey

The referenced height file is unreadable from the editor's sandbox, so a **downsampled** copy
ships inline: `strideMeters` 8, nearest texel, no interpolation, row 0 north, metres on the datum
above. A 760 x 560 region becomes 95 x 70 samples instead of 425,600. `null` marks a sample
outside the painted map. Full resolution stays in `terrain.x-heightMap`, pinned by `textureSha256`.

Two-state on the LFS bytes, exactly like the ground layer (§6): present, it carries values;
absent, it carries `x-unavailable` naming why. `x-provenance.heightMap.textureBytesRead` says
which you are holding.

### 8.5 `x-interiorOf` — hierarchy, not geometry

The editor was drawing interior props on roofs. The builders already answer it: an interior
stands at `IslandVillage/school/Interior` and its furniture under
`IslandVillage/school/Furniture/`, so the container is the **nearest ancestor path that is itself
an exported entity**. All 30 St Peters interiors resolve, to 4 buildings. A point-in-footprint
test was available and rejected: at a shared wall it would put a prop in the wrong house, and the
scene already declares the answer.

### 8.6 Is per-instance variety enumerated or continuous? — **both, and the split is the answer**

Asked because the editor's bake cache keys on `family|facing|opts`, and a free-float per-instance
seed would make that cache 1:1 and useless. Measured against the builder tables rather than
assumed:

**The bake axes are enumerated.** Every sprite selector in the repo takes integers or enums —
`SpriteFor(int state)`, `SpriteFor(string kind, int variant)`,
`SpriteFor(stance, gait, int facingRow, int frame)`. Variant rolls are cast to `int`
(`GrassFieldScatter.VariantRoll` is `(int)(Hash01(...) * 1024f)`), mirroring is a `bool`, tide
state is an `int`. **Nothing anywhere selects a sprite by a continuous value.**

**But two genuinely continuous per-instance axes exist**, and neither is a bake key:

| Axis | Where | Reaches the package as |
|---|---|---|
| Uniform scale | `Mathf.Lerp(ScaleMin, ScaleMax, Hash01(...))` at five scatter sites, folded into `transform.localScale` | `x-scale` — 384 St Peters shore plants, 387 distinct values in 387 entities |
| Tint brightness | `Mathf.Lerp(0.9f, 1.1f, shapeRoll)`, a shader multiply over the sprite | **not exported** — applied at runtime; 1,029 of 1,032 banked renderers are pure white |

`NineMileCreekShorePainter` says it outright: *"the species' own PlantedScale x this site's
jitter, folded into the TRANSFORM"*. So the cache is safe **only if the scale is applied as a
draw-time transform on the baked sprite and never folded into `opts`**. It ships verbatim and
unquantised: enumerating it here would be this exporter inventing an axis the pipeline does not
have, which is what the ask explicitly forbade.

