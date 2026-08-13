# The grass field — the meadow is data, the tufts are derived

**Status:** shipped 2026-08-09 (art-pipeline, to lead-architect's brief).
**Serves:** rule 7 (performance is a feature), rule 5 (deterministic, recomputed, never saved),
P3 (a living working coast — the island still reads green).
**Related:** ADR 0019 (hand-authored scenes; builders refresh, never destroy), ADR 0032 (the Y-sort
band), ADR 0023 (mesh renderers sort through a `SortingGroup`), ADR 0003 (content is data).

---

## What went wrong

The owner rebuilt `StPeters.unity` at the density he had ratified. It came back at **114 MB**
against main's **15.8 MB**.

The cause was not a leak or a bad asset. The grass pass emitted **a GameObject per tuft** — a
`GameObject`, a `Transform`, a `SpriteRenderer` and a `YSortSprite`, four serialized objects and
about **3.1 KB of scene YAML** each. At a 0.85 m grain the island carries roughly **27,000 tufts**,
so the meadow alone was ~98 MB of the file.

What actually stopped it was **GitHub's 100 MB file limit**, at `git push`, with the work already
done. Rule 7 had condemned it long before that and had no way to say so. Both halves of that
sentence are the problem this change fixes.

Measured on main (`7b9b300e`), for reference:

| | `StPeters.unity` (main) | the owner's rebuild |
|---|---|---|
| file size | 15,818,001 B (15.1 MiB) | ~114 MB |
| serialized objects | 20,193 | ~128,000 |
| GameObjects | 4,766 | ~32,000 |

## The idea

**Not one of those objects was authored.** Every tuft was already a deterministic function of the
painted ground: which metre is meadow, which is verge, which is dune, how dense, how straw-bleached.
So the ground's *answer* is the only thing worth storing, and the tufts grow from it.

- **The scene stores the FIELD** — one byte per candidate site, run-length encoded and Base64'd onto
  one line of a single component (`GrassField`). St Peters' whole meadow is a few tens of kilobytes.
- **The tufts are DERIVED at load** from (field, seed) by `GrassFieldScatter` — a pure function with
  no `Random`, no `Time`, no scene. Same field, same seed, same meadow, on every machine, for ever
  (rule 5). Saves carry nothing; there is nothing to carry.
- **The meadow DRAWS BATCHED** — chunked meshes rebuilt at load, a few dozen renderers instead of
  27,000.
- **It cannot come back.** Every chunk carries `HideFlags.DontSave`, so grass *cannot* be written
  into a `.unity` file — not by a save, not in edit mode, not by accident.

### The slot byte

One byte per site is the whole format:

```
bits 0..3   habitat id.  0 = NOTHING GROWS HERE (a long run of these is what empty ground costs)
bit  4      broad — this site asked for the widest art its habitat carries
bits 5..7   spare
```

Everything else a tuft needs — its jittered position, which variant it wears, its scale, whether it
is mirrored, its brightness jitter — is a **hash of (cell, slot, seed)** and is therefore not stored
at all. Straw is a smooth field, so it is baked as its own **coarse 4 m plane** and sampled
bilinearly: a smooth signal is the one thing run-length encoding is bad at, and what it drives is a
tint multiply where a 4 m gradient step is invisible.

### One definition of the meadow

`StPetersGrass.Scatter` still owns every gate (the meadow's clearings, the swathe field, the walked
paths and their verges, the habitat bands, the per-cell chance). The bake only **encodes** what it
decided, site by site, using each site's own cell and slot index. And the *positions* come from
`GrassFieldScatter` — the runtime's own arithmetic — so the point the bake gates and the point the
renderer draws are the same point by construction. A twinned copy on either side would be a second
definition of the meadow, and the first re-tune would put grass on a footpath the pass believed it
had kept clear.

### Idempotence (ADR 0019)

The stroke that doubled the owner's scene **appended** objects and rejected on min-spacing, so
dragging over the same ground twice laid more grass. **A field cannot append**: there is one byte
per site, and writing it a second time writes the same value. Re-running the pass is a no-op by
construction, not by a clear-first convention somebody has to remember. Pinned by
`StPetersGrassFieldTests.BakingTwice_WritesTheIdenticalField`.

---

## What had to survive, and how it does

### Wind — the shader was not touched, not a line

`HiddenHarboursGrass.shader` reads exactly two things: **the vertex's world position** and **the
sprite's own `uv`**. A merged chunk mesh supplies both, identically:

- vertices are built from `Sprite.vertices` (the sprite's own **tight, tessellated** local geometry
  in metres about its pivot), scaled, mirrored, and translated to the tuft's world position;
- the chunk transform carries the world offset, so `GetVertexPositionInputs` produces the same world
  position it produced for a `SpriteRenderer`;
- `uv` is copied straight from `Sprite.uv`.

So the bend is identical **by construction**, not by resemblance.
`GrassFieldWindParityTests.EveryBatchedVertex_LandsWhereTheSpriteRendererWouldHavePutIt` checks the
whole vertex set, world position and uv together.

Two traps that had to be honoured:

- **`Sprite.vertices`, never a harvested `SpriteRenderer` mesh.** A `SpriteRenderer` submits its mesh
  already in **world space** with an identity object-to-world matrix; harvest that and every tuft
  lands at the world origin of whatever scene it was captured in.
- **The sprite's mesh, never a quad.** The shader bends by `uv.y²`. On a four-vertex FullRect quad
  that is evaluated at `uv.y` 0 and 1 only and interpolates linearly, so the squaring does nothing
  (0² = 0, 1² = 1) — the exact defect `WindBendTessellationTests` pins for the trees. The tufts
  import Tight, which is why the grass escapes it and why the batched path must carry that same
  tessellation.

### Footstep bend — it needed nothing

The shader parts each blade from the global `_GrassTrail` using the blade's own **world position**.
There was never any per-tuft state to fit. `AChunkCarriesOnlyTheChannelsASpriteRendererSubmits`
checks that literally: a chunk carries position, uv0 and colour — the three channels a
`SpriteRenderer` submits — and no fourth.

That still holds now `_GrassTrail` is a **pool** of walker segments with a companion `_GrassWalkers`
record array (2026-08-13 — see [the design doc](../design/grass-wind-and-footstep.md#the-trail-pool--many-walkers-not-one-2026-08-13)).
Both are shader **globals** read off world position, so a batched chunk needs no extra channel for a
second walker any more than it did for the first.

### The lit-sprite shared path — untouched

Grass is on its own unlit wind shader and always was. Nothing here forks a third lighting path.

### Sorting vs the player — **the one open design point**

A renderer has one sorting order, so *N* distinct orders need *N* renderers. The old meadow had one
renderer per tuft and therefore every order it wanted. A chunk covering a band of world Y must pick
one, and every tuft in the band takes it.

**Decision: (a) row-sliced chunks riding ADR 0032's band.** A chunk is
`column × row × texture`; the row's order is `YSortSprite.OrderFor(RowAnchorY(row), …)` — the *same*
mapping every Y-sorted sprite uses, so no order is hand-picked. The row height is
`RowOrderSteps / SortingBands.OrdersPerMetre` metres, **derived, never chosen** (rule 6), and it is
also exactly the worst-case sorting error against the player.

> **⚠ The row decision ASKS the band; it does not restate it.** `GrassField.RowOf` calls
> `YSortSprite.OrderFor`, turns the answer into an order index below `DecorBase`, and groups
> `RowOrderSteps` of those indices into one row. Whatever `OrderFor` decides, the row inherits.
>
> This went wrong **twice**, and both times because the rounding was recomputed here instead:
>
> 1. **floor vs round.** Rows were bucketed with `floor` and anchored at the row *centre*. `OrderFor`
>    is `round(base − y·perUnit)`, so the world-Y bands it collapses onto one order are centred on the
>    lattice `y = k/perUnit` — their *edges* fall half a step off the multiples. Measured: **50% of
>    world-Y positions disagreed.**
> 2. **An exact float32 tie.** Round-bucketing fixed the phase but still worked the answer out
>    independently, and the two arithmetics don't lose precision in the same places. At
>    `y = −30.12501` the true `base − y·perUnit` is 1322.50004, but float32 near 1322 is spaced
>    ~0.000122 apart — so the 0.00004 that decides the rounding vanishes and the subtraction lands on
>    exactly 1322.5, where `Mathf.RoundToInt` breaks the tie toward even. The row's own arithmetic is
>    exact and never sees the tie, so the two answered differently on **2 positions out of ~328,000.**
>
> Routing through `OrderFor` closes both permanently. At one order step per row the chunk's order is
> now *exactly* the order the tuft had as a sprite, ties included — 0 disagreements across a
> region-wide sweep — and at N steps the error is bounded by N/2 orders, half a row, symmetric.
> `TheRowMapping_AgreesWithTheBandAcrossAWholeRegionOfWorldY` sweeps rather than samples, and
> `ACoarserRow_StaysInsideHalfItsOwnHeight` pins the bound at the other end of the knob.

`RowOrderSteps` is the single knob, and it makes the trade legible:

| `RowOrderSteps` | row height | sorting | draws |
|---|---|---|---|
| 1 | 0.25 m | **bit-identical** to the old sprite meadow — `YSortSprite` already rounded every tuft onto this quantum | most |
| **4 (shipped)** | **1 m** | a tuft can be mis-sorted against the player by ≤ 1 m — at ankle-to-knee height, grass in front of their boots | ~4× fewer bands |

Both ends are pinned:
`OneOrderStepPerRow_SortsExactlyAsTheOldSpritePerTuftMeadowDid` proves the fidelity end, and
`TheRowHeightIsDerivedFromTheBand_NeverPicked` proves the knob is derived rather than a number
somebody liked.

**Why the other two lose:**

- **(b) per-tuft depth in the vertex stream + the band's order range.** *Failure case:* the 2D
  renderer resolves sprite-against-sprite by sorting order, not by the depth buffer, and the grass
  shader is `ZWrite Off` in the Transparent queue. Written as-is it changes nothing — the depth is
  simply ignored. To make it decide grass-vs-**player** you must turn `ZWrite` on for the grass *and*
  make the player, and every decor sprite a character can stand among, write depth at a Y-derived Z.
  That is a second sorting model living beside ADR 0032's band, reaching into the shared lit-sprite
  path — the third path this work was told not to fork. It sorts grass against grass beautifully and
  is useless for the only case that matters.
- **(c) an under/over two-layer split.** *Failure case:* "under" and "over" are relative to the
  player's **live** Y, so the split is not a property of the field. It has to be re-decided every
  time the player crosses a chunk, which means re-uploading that chunk's mesh on a moving player. And
  once you ask "which tufts are under?", it collapses into (a) with rows = chunk height — at a 16 m
  chunk that is the player drawing over grass 8 m away. It only becomes correct by getting finer, at
  which point it *is* (a).

### Why there is still no sprite atlas

A chunk is split **per texture** because a mesh binds one texture and the library is 29 separate
PNGs. The obvious fix — pack them onto one page — is closed, and it is worth writing down again: an
atlas remaps every sprite's uv into a page rectangle, and this shader bends by `IN.uv.y`. "How far up
this blade" would become "how far up the page", and every tuft would bend by an arbitrary constant.
Same texture-space-uv trap as the shadow-shear defect of #431.

The lever that *is* available is `RowOrderSteps` — it cuts the number of **bands** a screen of grass
is chopped into, which is the multiplier that actually hurts.

---

## The tripwire

`SceneWeightGuardTests` walks **every committed scene** and weighs it — bytes on disk, serialized
objects, GameObjects — against ceilings with quoted headroom over what main actually carries:

| ceiling | value | main's worst today |
|---|---|---|
| file size | 32 MiB | 15.1 MiB (`StPeters.unity`) |
| serialized objects | 50,000 | 20,193 |
| GameObjects | 12,000 | 4,766 |

32 MiB is about a third of GitHub's hard limit, so a scene that trips this **fails in CI, at review**,
rather than at `git push` with the work finished. It parses no scene and loads no asset — it counts
`--- !u!` document markers — so it costs milliseconds and needs no graphics device. A guard that is
expensive is a guard somebody turns off.

It is proved against a **synthetic fixture**, not the owner's file: the test synthesizes a scene of
exactly the shape that broke (27,000 tufts × 4 objects × 3.1 KB) in a temp directory and requires the
guard to reject it — and separately requires it to *pass* a modest 1,000-object scene, because a
guard that fails on everything gets deleted rather than fixed.

---

## The owner's rebuild — one click

The clean scene is produced by the owner, not by an agent, and banked separately.

1. Open Unity on this branch and let it import.
2. **Hidden Harbours ▸ Build St Peters Scene.**
3. Save the scene (**Ctrl+S**).

That is it. The console will print the field's size, for example:

```
[StPetersWoodsPlanter] Baked a grass FIELD of 27,xxx tufts (dune …, headland …, meadow …) into
xx,xxx characters of scene — a 283x165 grid at 0.85 m, 2 sites per cell, seed 0. The tufts are
DERIVED at load and drawn in chunked meshes; none of them is saved.
```

**What to check before banking it:**

- `StPeters.unity` is back in the tens of megabytes, not 114 MB (`ls -l`, or just look at git's diff
  stat).
- The `IslandGrass` object has **one `GrassField` component and no children in the saved file** —
  chunks appear in the hierarchy while the scene is open and are never written.
- Press **Play**: the grass sways, and walking through it parts a trail behind you.

Running the build a second time rewrites the same field. It cannot double the meadow.

---

## What is still owed (and was not done here)

This work was implemented in a headless container with **no Unity, no GPU and no .NET** — so
nothing below was run, and none of it should be reported as verified until it has been:

- **The EditMode suites have not been executed.** They are written to the acceptance criteria and
  reviewed by hand; the "0 failed / 1 known skip" run belongs to whoever opens Unity next. The one
  expected skip is
  `StPetersGrassFieldTests.AScreenOfMeadow_CostsFewerDrawsBatchedThanItDidAsSprites` in a checkout
  whose grass PNGs are still LFS pointers.
- **The GPU numbers.** `AScreenOfMeadow_CostsFewerDrawsBatchedThanItDidAsSprites` computes the
  draw-call comparison headlessly on the same deciders the build uses, and logs it — but frame time
  on the RTX 4060 is a measurement only the owner's machine can take, exactly as
  `StPetersGroundCoverBudgetTests` already says about the old path.
- **The screenshot pair** for the sorting decision: the player standing in the meadow at
  `RowOrderSteps = 1` and at `4`. The argument above is derived rather than seen, and the shipped
  default should be confirmed by eye before it is treated as settled.
- **Build time**: how long the derive-plus-mesh pass takes at St Peters density on load. The design
  keeps it to a hash per site plus a vertex copy, but that is a prediction, not a stopwatch.

## Known gap

**`GrassPaintTool` still emits GameObjects.** It is the owner's hand brush and out of this change's
scope, but its *fill* can lay thousands of objects and its *brush* is the non-idempotent stroke that
started all of this. It is now behind the tripwire rather than in front of it — a scene it inflates
fails `SceneWeightGuardTests` in CI instead of at `git push`. Moving the brush onto the field is the
natural follow-up.
