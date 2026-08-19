# `hiddenharbours.scene/1` — the contract, as far as the repo can reconstruct it

**What this is.** The scene editor's package format, rebuilt from
[`scene-editor-review.md`](scene-editor-review.md) (#571, corrected by #576) — the only
description of it the repo holds. The exporter at `tools/scene-export/` emits against this
reconstruction, and this file records exactly where the reconstruction is grounded and where it
is a guess, so the guesses can be replaced by bytes from the editor's own reference package
rather than discovered by an export that renders wrong.

**Lane:** tools-editor. **Direction:** outbound only. Import stays gated (review §9).

---

## 1. What the review pins down

These are stated in the review, several of them with measurements behind them, and the exporter
follows them exactly.

| Element | What is settled | Where |
|---|---|---|
| Format id | the string `hiddenharbours.scene/1` | §2 |
| Region table | must come from `Data/Regions/*.asset`; the importer validates `region.worldSizeMeters` against the `RegionDef` and **refuses on mismatch** | §6.2 |
| `terrain.cols/rows`, `originNW` | derived from the region size; NW corner of a 760 × 560 region at `[-380, 280]` | §6.2 |
| Grid | one cell per metre (a 160 × 120 m region gives a 160 × 120 grid) | §6.2 |
| Layers | `ground`, `cliff`, `road`, each carrying an `rle` and a `legend` | §5 Q1 |
| RLE coverage | `sum(runs) == cols × rows`, refuse otherwise — a short stream cannot be told from a truncated one | §5 Q1, §8.2 |
| `stats.tiles` | a stamp count, **not** a cell count; never validate against it | §5 Q1 |
| `pieces` | nested under the cliff layer, shaped `{note, legend, rle}`; the legend is `CliffRig.PIECES` in order, 1-indexed, `0 = autotile` | §5 Q1, Q4 |
| Pivot | ship `unityPivot` only — `[px/w, (h − py)/h]`, the normalised **bottom-left** form (ADR 0026). Do not ship the rig's top-left `pivot` | §5 Q3 |
| PPU | 32, safe to bake in (`CameraFollow.AssetsPPU`). **Not** the water shader's `_PixelsPerUnit = 24`, a different grid entirely | §5 Q3 |
| `call` / `rigSource` | provenance only, never an engine instruction; `rigSource` paths are flattened and do not resolve | §5 Q2 |
| Rig pinning | each rig referenced should carry the **LF sha256** of the bytes that produced it; recompute and refuse on mismatch | §6.1 |
| Entity ids | export-local ordinals (`dory_001` renumbers on delete) — never world identity | §4, §8.3 |
| `sortBias` | maps onto `YSortSprite._baseOrder` | §9.3 |
| `frame.camera` | prose citing ADR-0006/0022. Documentation; never parse it | §6.3 |
| Road cells | present but redundant — a preview artefact, never truth. The RLE carries surface material only, no mask index | §5 Q5 |

## 2. What the review names but does not specify

Every row here is a field the review mentions — so it exists — without saying enough to write a
reader or a writer against. **The coordinator holds the editor's reference package locally; each
row names the exact bytes that would settle it.**

Two of these decide whether a package loads and renders at all, and are marked ⚠.

| # | Question | Why it matters | Bytes that settle it |
|---|---|---|---|
| 1 | ⚠ **Are unknown keys tolerated?** | The exporter adds `x-` prefixed fields for everything the contract does not name. If the reader refuses unknown keys, no package produced here loads. | Load a sample with one junk key added, or the reader's validation branch. |
| 2 | ⚠ **Does an entity render without `call`/`opts`?** | The sheets are baked; the option axes that produced a given cell are not recorded per instance, so this export carries no `call`. If the editor renders by *calling* the rig, entities without one draw nothing and the package is blank. | One entity record from `sample-scene.json`, and whether the renderer keys off `call` or off `cell`. |
| 3 | **Where does an entity say it is?** | The review never names the position field; `cell` is taken (it is the sprite's `{w, h, pivot}`). This export ships both `x-atMeters` and `x-cellAt` because it cannot tell which is wanted. | One entity record. Also: cell (col,row) vs metres, and origin at `originNW` or world centre. |
| 4 | **RLE encoding** | Flat `[value, count, value, count, …]` or pairs `[[value, count], …]`? Sums are quoted in the review; the encoding is not. This export emits pairs. | The first 40 bytes of any layer's `rle`. |
| 5 | **Legend key convention** | The review notes two conventions in one document — prefixed strings (`g1`, `r3`) versus numeric strings (`1`…`12`) — without saying which a *layer's own* legend uses, or whether legend keys are the RLE values or ids that map to them. | One layer's `legend` object. |
| 6 | **Row order** | Row-major is stated. Row 0 at the north edge (`originNW`) or the south? A wrong guess mirrors the whole region. | `originNW` plus the first run of a layer whose north edge is known. |
| 7 | **Termination and the meaning of 0** | The review *rules* full coverage, but the format has none. Does a trailing zero run mean "empty"? Is `0` a legend value or the absence of one? | The tail of a `ground` rle and its legend. |
| 8 | **`layers` container** | A map keyed by layer name, or an array of `{name, …}`? This export emits a map. | The `terrain` object's shape. |
| 9 | **`paths[]` shape** | `nodes`, `material`, `widthMeters`, `tiles` are named. Node element shape (`[x,y]` or `{x,y}`; cells or metres), the `material` vocabulary, whether `widthMeters` is required, whether a path carries an id. | One `paths` entry. |
| 10 | **`family` vocabulary** | ~490 palette items encode families. Is an unknown `family` refused, or ignored? This export uses the sprite's own name stem, which will not match the palette's keys for most sheets. | The palette's family key list, or the loader's unknown-family branch. |
| 11 | **Is `cell` required, and does it need `pivot`?** | The review says ship `unityPivot` only, but the rock defect shows `cell.pivot` alongside it. If the renderer reads `cell.pivot`, omitting it breaks rendering. | One entity's `cell` block. |
| 12 | **`sortBias` units** | Named, never defined. `YSortSprite._baseOrder` is an absolute order (~1202 at `DecorBase`), not an offset. Is `sortBias` an absolute order, or a delta from a band base? | One entity's `sortBias` next to a known object. |
| 13 | **`footprint`** | The review quotes an entity's inline `footprint` carrying the correct `pivot: {x, y}`. Shape and whether it is required are unstated. | One entity's `footprint`. |
| 14 | **Top-level envelope** | Which key carries `hiddenharbours.scene/1` (this export uses `format`), and what else is required beside `region`, `frame`, `terrain`, `paths`, `entities`, `stats`. | The first 30 lines of `sample-scene.json`. |
| 15 | **Container** | The reference is a `.zip`. Does the editor open a bare `.json`, and must it be named `sample-scene.json` / `<something>.scene.json`? | The zip's file list, or the open dialog's filter. |
| 16 | **`gameplaySidecar`** | Named as a hardcoded `family → filename` map. Per entity, or one top-level table? | Wherever it appears in the sample. |
| 17 | **`stats` shape** | `stats.tiles` is keyed by layer; the rest of the object is unstated. | The `stats` object. |

## 3. What this export deliberately does not emit

- **`call` / `opts`** — see §2 #2. Reconstructing an invocation from a baked sheet would be a
  second definition of the bake, and the review rules the call record provenance-only (§5 Q2).
- **Painted terrain of any kind** — the ground is an iso-contour of an R8 height texture held in
  Git LFS (ADR 0014), whose bytes are absent from a checkout. Layers are emitted zero-filled at
  full coverage, flagged `x-readOnly` / `x-derived` / `x-authorable: false`, with the map pinned
  by its LFS oid. Inferring height from tiles is the review's §7(c) trap and is not attempted.
- **Road cells** — none exist in the committed scenes (the Nine Mile Creek roads landed after
  the scenes were last banked), and the review rules them a preview artefact regardless.
- **A guessed rig** — a sheet with no sidecar the exporter will trust resolves to `null` and is
  listed by name. The review's own lesson from the sport-skiff rename is that a confident wrong
  link costs more than an admitted gap.

## 4. Ours, and marked as ours

Any key beginning `x-` is this exporter's, not the contract's. They exist because a picture of
a region is worth little without knowing which region, which vintage, and how much of it is
trustworthy:

`x-provenance` (source commit, the commit the scene was last banked at, builder drift since,
what was read from where, and the unresolved-sheet list), `x-rigs` and `x-rigSha256` (the pin
table the review asks for but does not name a key for), `x-atMeters` / `x-cellAt` /
`x-inBounds`, `x-name` / `x-path` (the scene hierarchy path), `x-pivotSource`, `x-declaredBy`
(which sidecar linked a sheet to its rig), `x-readOnly` / `x-derived` / `x-authorable`,
`x-heightMap`, `x-rleRule`, `x-unavailable`.

If §2 #1 comes back "unknown keys are refused", they move behind a single `--strict` flag that
drops them; nothing else in the exporter changes.
