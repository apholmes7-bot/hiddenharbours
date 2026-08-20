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
- **Painted terrain** — the ground is an iso-contour of an R8 height texture in Git LFS
  (ADR 0014), whose bytes are absent from a checkout. Layers ship zero-filled at full coverage,
  flagged `x-readOnly` / `x-derived` / `x-authorable: false`, with the map pinned by its LFS
  oid. Inferring height from tiles is the review's §7(c) trap and is not attempted.
- **`cliffLines`** — empty. Cliff lines are an editor authoring artefact; the repo's cliffs are
  placed `CliffWallSurface` components and ship as entities.
- **`footprint`, `gameplaySidecar`** — both are rig gameplay measurements, and no rig is
  executed here (rigs are hashed, not evaluated).
- **A guessed rig** — a sheet with no sidecar the exporter will trust resolves to `null` and is
  listed by name under `x-provenance`.

One field is honest-but-mismatched, and every entity says so: **`family` is the sprite name's
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
