# The grass edge band — how a field stops

**Lane:** art-pipeline · **Owner ask:** playtest, 2026-08-26 (St Peters — Ginny's plot, the sparse
field, the tread verges) · **Code:** `StPetersGrass`, `StPetersWoodsPlanter.GrassArtChooser`,
`GrassFieldScatter`, `GrassField` · **Tests:** `StPetersGrassEdgeBandTests`

---

## 1. The defect

The owner walked the retuned meadow and split his verdict cleanly:

> Tall-tuft fields end in a hard line against the flat compacted-grass splat. Dense areas read as
> full cover; **the transitions are the defect.** Coverage itself is ruled good.

A field gated on a hard predicate has no choice about that. A cell either cleared `ChanceOpen`
(0.97) or planted nothing, so the meadow met bare ground at a step from near-full cover to nothing
across one 0.85 m cell. All three of his spots are that one defect wearing different clothes:

| spot | what the boundary actually is |
|---|---|
| Ginny's plot edge | a yard polygon's mow line (`StPetersYards`) |
| the sparse field | a contour of the swathe mosaic (`SwatheThreshold`) |
| the tread verges | a walked path's bare tread (`PathBareHalfWidthMetres`) |

## 2. Two changes, and they only work together

**A density ramp.** Every accept chance is multiplied by `EdgeFalloff` — 1 in the interior, easing
to a floor at the boundary, across a band ~3.2 m deep whose **width itself meanders**
(`BandWidthAt`, its own coherent noise at an 11 m feature size).

> ⚠ **A falloff of constant width is a stripe.** Ramping over a fixed distance draws a perfectly
> parallel border inside every boundary on the island — round each building, along each fence, down
> both sides of each path. The eye reads a constant-width gradient as painted trim: a different
> artificial edge, not the absence of one. The meander is why the band is not that.

**A height step-down.** The same distance steps the ART down a class (`GrassTier`): interior wears
whatever its habitat has, the outer band drops the tall blades, the last metre is short.

> A field that thins toward its edge but stays knee-high to the last blade still ends in a line —
> just a dotted one. **The height step-down is the primary cue; the density ramp supports it.**

## 3. ⭐⭐ Two floors, because the two kinds of edge are not the same edge

The first cut ramped every boundary to zero. **It was wrong twice over, and measurement caught both:**

1. **The meadow stopped reading as a field.** Whole-island coverage fell 82.6% → **76.9%**, under the
   80% the owner ratified. A *quarter* of this island's plantable ground lies within a band of some
   boundary — the swathe mosaic's contour is intricate, so its perimeter is enormous.
2. **It deleted the verge.** `HabitatVerge` exists *only* in the 1.2 m ribbon either side of a walked
   tread — exactly the ground a ramp-to-zero erases. The island went from a hem of trodden clumps
   along both paths to **seventeen tufts**. A pass meant to soften an edge had removed a habitat.

The fix is not a narrower band, it is the right shape — and both failures agree about what that is.
Stand at a real mow line: the wild grass is **dense right up to it**, because a mower cuts a line
rather than tapering one. What changes across it is *height*. Stand where a field peters out into
thin ground and the density genuinely does fade.

| edge kind | measured by | floor | why |
|---|---|---|---|
| **cut / trodden / built** — mow lines, treads, buildings, the wharf | `ClearanceDistanceMetres` (exact: discs, segments, polygons) | **0.80** | something cut this line; grass stands dense against it and the transition is height |
| **field contour** — the swathe mosaic, the grass floor | `FieldContourDistanceMetres` (first-order `(v−t)/‖∇v‖`) | **0.45** | the field really is running out here |

Ramping a *cut* edge to zero does not soften it — it digs a bald moat around every fence and
doorstep, which is its own artefact and a more obvious one than the line it replaced.

Neither floor is zero. A fixed-width band ramped to nothing reads as a moat drawn around the thin
ground rather than as thin ground.

### Why a gradient and not a search

Both soft boundaries are **iso-contours of continuous fields**, not authored shapes, so there is no
geometry to measure a distance *to*. Ring-probing outward for the first failing sample would cost
tens of predicate evaluations per site and quantise the answer to the probe spacing; one central
difference costs four samples and is exact for the linear part — which over 3 m of a 24 m-scale
field is very nearly all of it.

> ⚠ **A flat field has no nearby contour, and that is the common case.** St Peters is a flat-topped
> plateau, so the elevation gradient inland is zero and the honest answer for the whole interior is
> `FarInsideMetres` — not 0 (which would band the entire island) and not a division by zero.

## 4. The art, and why it needed no new bakes

`GrassArtChooser.ForTier` / `BroadForTier` filter the habitat's baked pool to the tier's height
classes and then run **the existing ally machinery unchanged**. `Widen` already lends only art of a
height class the pool *already has* — the rule that keeps the verge from borrowing tall timothy — so
a seed pre-filtered to `short` borrows only short. The tier just decides what the seed is.

The band drops **tall** rather than insisting on **medium**, and that matters: measured on the
committed manifest, meadow is the only island habitat with a `medium` bake at all. A medium-only
band would be empty for the other five and fall straight back to the full pool, leaving them a
two-step from tall interior to short hem — the hard line again with an extra metre on it.

Measured on the committed manifest, every island habitat clears `MinHabitatVariety` at every tier:

| habitat | interior | band | hem |
|---|---|---|---|
| meadow | 17 (tall) | 12 (medium) | 6 (short) |
| fringe | 4 (tall) | 4 (short) | 4 (short) |
| dune | 4 (tall) | 4 (short) | 4 (short) |
| sward · headland · verge | 4 (short) | 4 (short) | 4 (short) |

The last row is a correct no-op: that ground is already hem-height.

> ⚠ **If a future retag breaks this, `StPetersGrassEdgeBandTests` names the pool and the tier —
> bake, don't lower the floor.** Lowering `MinHabitatVariety` would hide it everywhere at once.

**Every narrowing falls back rather than emptying.** Two filters over a library that does not bake
every combination will find holes. A missing broad pool drops to that tier's normal art; a missing
tier walks *up* toward the interior (hem → band → interior). A bald hem is a worse artefact than a
slightly tall one, and one the owner would have to diagnose from a screenshot.

### Broad clumps at the hem

`BandBroadClumpShare` 0.70 / `HemBroadClumpShare` 0.85, against the interior's 0.60 — and the reason
is the ground, not the budget: **grass at a cut or trodden edge spreads sideways instead of standing
up.** The library already says so: the only art baked for `HabitatVerge` is the two wide low
`ClumpWide` variants. It also pays for itself — a wide clump hides twice the ground for one
renderer.

## 5. The field format

The tier rides in two spare bits of the slot byte:

```
bits 0..3  habitat id   (0 = nothing grows here)
bit  4     broad
bits 5..6  GrassTier    ← new
bit  7     spare
```

`GrassTier.Interior` is **0 on purpose**: every field baked before 2026-08-26 has zeros in those bits
and therefore decodes as all-interior, drawing exactly the meadow it always drew. Pinned by
`TheSlotByte_CarriesTheTier_WithoutDisturbingWhatWasThere`.

## 6. What it measures

| | before | after |
|---|---|---|
| whole-island meadow coverage | 82.6% | **80.8%** (floor 80%) |
| tufts | 35,026 | **29,730** (budget 42,000) |
| broad share | 45% | 50% |
| band width | — | 2.09–4.31 m (meandering) |
| candidate cells untouched | 100% | **74.8%** |

Density across the band, from the boundary inward — predicted by walking the planter's own deciders,
measured from the scatter:

| bucket (0 = boundary) | 0 | 1 | 2 | 3 | 4 | 5 (interior) |
|---|---|---|---|---|---|---|
| predicted | 39.6% | 43.4% | 46.8% | 54.8% | 60.0% | 73.1% |
| measured | 38.2% | 43.9% | 47.2% | 54.7% | 60.8% | 73.0% |

At a cut edge the ground carries ×0.81 of its unbanded density (floor 0.80); at a field contour,
×0.47 (floor 0.45).

## 7. The interior guarantee

*"Coverage itself is ruled good — do not retune the field-interior density."*

`TheInterior_IsUntouched_CellForCell` reproduces the **pre-band** accept decision (`InStand ?
ChanceWoods : ChanceOpen`, no falloff, no ring) and requires it to be reproduced **exactly** wherever
the new deciders leave the ground alone — that is, where the falloff is exactly 1 *and* the stand
ring is unanimous. 74.8% of candidate cells qualify, and a second assertion requires that majority so
the first cannot be vacuous.

## 8. Scope

- **The stand edge is softened too** (`ChanceAt` / `StandFraction`): 0.97 → 0.10 across one cell was
  the same defect drawn round every wood. A ring of 8 predicate probes, because `InStand` folds a
  noise field and a shelter taper into one comparison and has no single value to differentiate.
- **Nine Mile Creek is deliberately unchanged.** `NineMileCreekFields.ScatterGrass` has its own gates
  (`OnATownLot`, `OnAnyCarriageway`) and its sites stay at `GrassTier.Interior`. The machinery is
  region-agnostic and NMC can adopt it, but doing so is its own density change and its own eyeball.
- **No scene is in this change.** The island only changes on the owner's next **Build St Peters** run.

## 9. Owed

- Owner eyeball at the three playtest spots: Ginny's plot edge, the sparse field, any tread verge.
- The **splat under-tone pass** (a generated pass darkening the splat under high-density tuft regions,
  fading over the same band) was the brief's optional third item and is **not in this change** —
  flagged for a follow-up, and it must respect the non-idempotent exclusive-stroke trap.
