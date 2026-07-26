# ADR 0026 — A rig `pivot` is a CONTINUOUS cell-corner coordinate, not a pixel index — so `(H − pivotY)/H` is correct, and the tree's `pad/h` is correct too

- **Status:** **Accepted.** Ratifies behaviour already shipping on `main` (`c328614`). **No code
  behaviour changes.** This ADR exists because the question was raised as a suspected off-by-one-row
  bug in the shared helper, was investigated, and the helper was found **correct** — and a finding
  that lives only in a session transcript gets re-investigated from scratch the next time somebody
  notices the two formulas.
- **Date:** 2026-07-26
- **Decision owner:** lead-architect (a convention three bake paths across art-pipeline and
  tools-editor depend on — CLAUDE.md rule 4).
- **Serves:** **P3 "Living Working Coast"** indirectly, by keeping the fleet, the characters and the
  trees standing in one space. Mostly it serves rule 8 and the repo's standing rule that geometry
  facts are **measured, never declared** — the same discipline `RigAzimuthProbe` enforces for the
  facing order after that one shipped defects in five separate kits.
- **Related:** `0021-in-engine-js-rig-baking.md` §4 (*cell geometry, pivot and the crop rect come
  from the rig instead of a README* — the rule that makes the tree's own `pad` authoritative),
  `0006-boat-art-pipeline.md` (the sheet/pivot conventions the boat bake emits).

## Context

Two pivot formulas coexist, and they differ by exactly one row:

| Path | Formula | Files |
|---|---|---|
| Shared helper (boats, characters) | `(H − pivotY) / H` | `RigCatalog.cs` → `RigGeometry.UnityNormalisedPivot` |
| Fishing kit | `(Cell.y − PivotTopLeftPx.y) / Cell.y` — the same form | `FishingSheetSlicer.cs` → `KitSpec.NormalizedPivot` |
| Tree bake (PR #298) | `pad / cellH`, where the rig exports `pad = h − 1 − pivotY` | `TreeKitCatalog.cs` → `NormalizedPivot` |

`(H − pivotY)/H` places the pivot on the **top edge** of the pivot row; `pad/h` places it on the
**bottom edge** — one row lower. Both cannot be right for the same quantity, and the suspicion was
that the shared helper is a row high for every boat and character in the game.

**It is not.** The two convert *different quantities*.

## The evidence

### 1. The rigs' `pivot` is a continuous coordinate whose origin is the cell's top-left corner

Every boat and character rig projects with the same expression (`puntIsoRig.js:322`,
`characterIsoRig.js:425`):

```js
sx: cx + xr*S,   sy: cy - (yr*B.se + zr*B.ce)*S - B.heave
```

The rig origin — *amidships, keel bottom, centreline* for a hull; *ground contact* for a character —
therefore projects to exactly `(cx, cy)`. The rasterizer that consumes those coordinates samples
**pixel centres** (`puntIsoRig.js:346`, `characterIsoRig.js:447`):

```js
const px = x + 0.5, py = y + 0.5;
```

So the coordinate space is one in which pixel `j` spans `[j, j+1)`. A feature at continuous `94.0`
sits on the seam between rows 93 and 94 — the **top edge of row 94** — which is exactly what
`(H − pivotY)/H` produces.

### 2. The clincher: every rig centres its pivot *exactly*, and no pixel index could

All **19** rigs under `docs/art/rigs/**` that declare standard geometry set `cx = W/2`, and every
cell width is even:

```
punt 92/184 · lobsterBoat & capeIslander 228/456 · character 64→32 · dory 80/160
console & sportSkiff 122/244 · fish 32/64 · rod & shovel 56/112 · fishTote 32/64
fishTub 22/44 · sideDragger 448/896 · sternTrawler (both) 672/1344 · tanker 960/1920
coastalPacket 1056/2112 · house 496/992 · wharfBuilding 600/1200
```

- As a **continuous** coordinate from the left edge, dead centre of a `W`-wide cell is `W/2` — an
  integer. Every rig writes precisely that.
- As a **0-based column index**, dead centre is `(W − 1)/2` — a half-integer for every even `W`
  (91.5 for the punt). No rig writes that, and none *could* write it as the integer it does write.

Were `pivot` a pixel index, every boat and character in the repo would be laterally off-centre by
half a pixel. `pivot.x` and `pivot.y` come out of the *same* `projVert` expression in the same
units, so whatever `x` is, `y` is. **`pivot` is continuous; `(H − pivotY)/H` is exact.**

Corroboration already in the repo: the punt's hull cell (184×168, pivot 92,94) and its **wider**
motor cell (212×168, pivot 106,94) must normalise to the *same* pivot for the outboard to land on
the transom, and `92/184 = 106/212 = 0.5` exactly. Under the index reading the corrected values
would be `92.5/184 = 0.502717` and `106.5/212 = 0.502358` — no longer equal, and the motor layer
would shear off the transom. `PuntSheetSliceTests` already asserts that identity.

### 3. Measured from pixels, not just argued

`RigPivotConventionProbe` renders a rig at a bow-on/stern-on heading, where the silhouette is
symmetric about its centreline, and mirrors the alpha mask under both hypotheses:

- corner reading → axis at continuous `pivotX`, column `j` reflects to `(2·pivotX − 1) − j`
- index reading → axis at `pivotX + 0.5`, `j` reflects to `2·pivotX − j`

The true axis reproduces the mask; the false one is displaced one column and disagrees along every
near-vertical edge. `RigPivotConventionTests` asserts the verdict, and requires a minimum number of
decisive samples so it cannot pass vacuously.

### 4. Why the tree is different, and still right

A tree's pivot is not a projected point. `treeIsoRig.js:434-438` **chooses a row** —
`pivotY = Math.ceil(MG − top)` — and exports `pad = h − 1 − pivotY` itself, so per ADR 0021 §4 the
tree bake reads the rig's own quantity. `pad/h` puts the pivot on that row's **bottom** edge, and it
has to: the same fraction is also the wind shader's `_TrunkAnchor`, and taking `(h − pivotY)/h`
would put the ground plane one row *above* the anchor, leaving the lowest row of near-root flare
outside the planted band where it would sway. `TreeKitCatalog.NormalizedPivot` already documents
this at length.

Both formulas are edge-aligned, which is what keeps sprites pixel-snapped at PPU 32.

## Decision

1. **`RigGeometry.UnityNormalisedPivot` and `FishingSheetSlicer.KitSpec.NormalizedPivot` keep
   `(H − pivotY)/H`.** It is correct. No boat or character asset changes; **no re-bake is required
   of the owner.**
2. **`TreeKitCatalog.NormalizedPivot` keeps `pad/cellH`.** Also correct, for a different quantity.
3. **Do not "unify" the two.** `RigPivotConventionTests.TreePivot_IsDeliberatelyOneRowLowerThanTheSharedHelper`
   fails in either direction if someone tries, and points back here.
4. A rig whose `pivot.x` is **not** exactly `W/2` breaks the argument in §2. The test asserts this
   per rig; if it ever fires, re-derive the convention for that rig rather than relaxing the assert.

## Consequences

- **Nothing shifts.** The helper was right, so there is no drift to correct and nothing downstream
  was silently compensating for one — checked: no per-hull pivot nudge or sprite offset exists in
  `Assets/_Project/Code`, which is itself corroboration. Had the helper been a row high, presenters
  tuned by eye would likely carry a systematic ~1 px counter-offset; there is none.
- Had it been wrong, the cost would have been **1 px = 0.031 m at PPU 32** on every boat and
  character — small per asset, but it is the boat origin, so it would have moved the waterline, the
  wake attachment and every deck anchor together.
- The repo gains a **pixel-measuring probe for pivots**, alongside the one for azimuth. The lesson is
  the same one this project keeps re-learning: when two declarations about rig geometry disagree,
  render the rig and look.
- `docs/art/rigs/**` was read but **not modified** — it is the art-director role's lane.
