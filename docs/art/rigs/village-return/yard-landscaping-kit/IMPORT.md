# Yard & Landscaping Kit — import record (2026-08-20)

What arrived in the owner's drop, what was taken, and what was **measured**. Nothing below is read
off the kit's own README: every number came out of a live `project()` call or a rendered buffer, run
through the repo's **own** ClearScript V8 in a standalone harness (no Unity, ~1.3 s for the whole
sweep — see [[run-rigs-in-standalone-v8-harness]] in the session notes).

## The drop

`Pixel art capabilitiesyardrigkit.zip` → `export/yard-rig-kit/`. **62 pieces across 10 families**
(fences & gates, hedges & shrubs, beds, planters, dooryard fixtures, sitting, play, ornaments,
paths & ground, signs) as parametric geometry — the rig **is** the asset, no PNGs ship.

| file | verdict | action |
|---|---|---|
| `yardIsoRig.js` | **NEW** — installs `YardIso`, 116 KB | imported verbatim → `yardIsoRig.js` |
| `gameplay/yardIsoRig.gameplay.json` | **NEW** — schema `prop-placement-geometry@1`, the repo's first | → `docs/art/rigs/gameplay/props/` |
| `harness.html` | standalone bake + sidecar page | imported verbatim |
| `README.md`, `SHA256SUMS.txt` | the kit's own documentation | imported verbatim |

**No host rigs are bundled**, so there is no fork to diff — the collision hazard that dominated the
nav-buoy and shop-kit intakes simply is not present here. The one global it installs, `YardIso`, is
unclaimed in this repo.

## Gate 1 — the hashes, and the working-tree form

```
sha256 yardIsoRig.js                        d2ab2a906f4f099e3054643034a936327ae547fc5ea375b3198cc8f503d23706
sidecar's derivedFromRigSha256              d2ab2a906f4f099e3054643034a936327ae547fc5ea375b3198cc8f503d23706   ✓
sha256 gameplay/yardIsoRig.gameplay.json    89136c3a18b71cb2c2ccfcc182c52fd9c88df0fefa29c7de8b5c0701dc681fb5   ✓ (SHA256SUMS)
```

The rig ships **LF already** — zero `\r` in 116 KB — so the raw and LF-normalised hashes are the same
number, and the pinned hash is the one git stores. That is the form
[[sidecar-hash-pins-working-tree-bytes]] asks for, arrived at for free rather than by re-stamping.

## Gate 2 — the composition hooks resolve

The bed pieces ask `globalThis.Flowers` and `globalThis.Shrubs` for real sprites and blit them over
their own soil, falling back to native foliage mounds when those are absent. Both symbols exist in
this repo and are exported by exactly the rigs the kit names:

| symbol | provider in this repo |
|---|---|
| `globalThis.Flowers` | `docs/art/rigs/flowerRig.js` |
| `globalThis.Shrubs` | `docs/art/rigs/shrubIsoRig.js` |

So a planted bed will compose rather than fall back — **provided the baker loads all three rigs into
one engine.** A baker that loads `yardIsoRig.js` alone still produces a bed that reads; it produces a
*different* bed, silently. Worth an assert at bake time rather than a discovery in a screenshot.

## Gate 3 — determinism, through the repo's own V8

Six pieces across six families, rendered twice in one engine and again in a **fresh** one:

```
picketPanel      e699ff29749e8f80     mailbox          d2649064b5afb304
cedarHedge       bc9a689f926b4211     buoyPost         85cfd12bac1c012d
foundationBed    0bf701765e71b14b     flagpole         59e09e289472a45c
```

6/6 byte-identical within the engine and across a cold one. And **62/62 pieces render exactly one
470 × 540 × 4 = 1,015,200-byte cell**, so the "one cell for every piece, pivot-stable at runtime"
claim is measured rather than believed.

## Gate 4 — the sidecar really is derived from this rig

`footprint()` and `height()` were called on the landed rig for every piece at every `len` the sidecar
records (0, 0.4, 1) and compared with the sidecar's own `size[]`, **in JS**, so nothing is
re-transcribed on the way ([[bit-equality-is-unattainable-between-two-transcriptions]]):

```
D. sidecar vs rig: 186/186 measurements agree across 62 pieces
```

186 for 186, to within 1.5 mm. The sidecar is generated from the rig, as it claims.

## Gate 5 — the pivot, against ADR 0026

Declared: cell 470 × 540, pivot at (235, 442). Measured: `anchors('picketPanel', 3, {}).ground` comes
back at **exactly (235, 442) in cell pixels** — the pivot IS the ground-centre of the footprint, as
the contract says, so a piece drops on a lawn tile with no offset maths.

Unity normalised pivot, by ADR 0026's `(H − pivotY)/H`:

```
(235/470, (540 − 442)/540) = (0.500000, 0.181481)
```

## ⚠️ Gate 6 — THE BAKE ORDER IS COUNTER-CLOCKWISE, AND THE SIDECAR SAYS CLOCKWISE

This is the one finding that costs a consumer real work, and it is exactly why the handoff said
**MEASURE, never assume** (the fleet's iso art is CCW; the fuel kit was CW).

The model axes were projected at each `dir` and converted back to world bearings. The screen-y sign
that the whole table hangs on was **controlled first** — model `+Z` (up) projects to `dy = −24.51`,
confirming screen y grows downward — because if that sign were wrong the table would mirror and the
conclusion would invert.

| dir | sidecar label | measured `+X` (the run) | measured `+Y` (**the face**) | error |
|---:|:---|:---|:---|:---|
| 0 | N | E | **N** | — |
| 1 | NE | NE | **NW** | 90° |
| 2 | E | N | **W** | 180° |
| 3 | SE | NW | **SW** | 90° |
| 4 | S | W | **S** | — |
| 5 | SW | SW | **SE** | 90° |
| 6 | W | S | **E** | 180° |
| 7 | NW | SE | **NE** | 90° |

The cells advance **counter-clockwise** (N · NW · W · SW · S · SE · E · NE). The sidecar's
`contract.facings` is the clockwise list (N · NE · E · SE · S · SW · W · NW). The two agree only at
`dir 0` and `dir 4`, where the sequences cross; **every other cell is 90° or 180° from its label.**

`+Y` is the face by the kit's own axis contract — *"+Y away from the house (wall-backed pieces put
their back plane toward −Y)"*.

**What this means for the bake and the placement:**

1. **Place from the measured table, never from `contract.facings`.** A consumer that looks up "E" and
   takes cell 2 draws a fence panel facing **west**.
2. It is possible the kit means `dir` as a **camera station** ("seen from the east") rather than an
   object facing — with an orthographic camera the two differ by exactly this mirror. That reading
   makes the art correct and the *word* `facings` wrong. Either way the placement table above is the
   same, so nothing is blocked while the question is open.
3. **Owner/kit-author question, not a refusal:** re-emit `contract.facings` in the baked order, or
   rename the field to say it lists camera stations. The geometry itself is consistent, deterministic
   and correctly pivoted; only the label is at issue.

## S2 — the bake, and §6 confirmed in the baked cells (2026-08-20)

**The BOUNDARY family is baked.** Ten of the 62 pieces — one panel per fence style, the corner posts
the kit actually draws, and the picket gate — at 32 px/m into `Art/Sprites/Yard/`, `spriteMode`
Multiple **and** sliced, metas committed, bake cap = import cap (2048; the widest sheet is
`splitRail` at 848 px, so there is a factor of two in hand). **129.4 KiB of PNG, 1.68 MB RGBA32 at
runtime, 2.4 s through V8.**

| | |
|---|---|
| baked | `picketPanel` `picketGate` `fenceCorner` `postRail` `splitRail` `wireFence` `stoneWall` `stonePillar` `hedgeRun` `hedgeCorner` |
| not baked | the other 52 — beds, planters, dooryard fixtures, sitting, play, ornaments, paths, signs |

The 52 are dooryard DRESSING, and which property gets which is an owner decision still batched on
#604. Adding one is a row in `YardKit.Builds`; the baker, the slicer and the contract are already
written against the table.

⚠️ **It does NOT bake through `IsoPropSheetBaker`, and the reason is a gate rather than a shape.**
Every precondition that baker checks holds here — the fixed 470 × 540 buffer, the
`render(key, dir, opts)` call, the bare RGBA return — and its `MeasureCell` IS the cell rule
`YardSheetBaker` uses, reused rather than restated. What this kit cannot pass is
`IsoPackContract.AssertKeylineGated`, which refuses any rig exposing no `KEYLINE_DEFAULT`. That gate
is the owner's 2026-08-06 ruling about the four ISO-PACK rigs specifically; ADR 0031's standing
consequence for every other family is that it keeps its ring until its own natural redo, and a mixed
period is expected. So this bakes ringed, like the fuel, nav-buoy and shop-fixture kits before it,
and its retirement rides the outline-language arc rather than blocking a dooryard.

### §6 confirmed by the bake, to four decimals

`YardRegistrationProbe` re-derives the whole table at every bake from the rig's own `project()`,
un-squashed to the ground plane against scale factors read from the rig rather than restated, and
**refuses the bake** on a disagreement. The first run:

```
rig dir frame  : steps -45.000°/dir  ⇒ CounterClockwise   (RigCatalog declares CounterClockwise)
baked cells    : step +45.000°/cell, worst 0.0000° from nominal, run−face worst 0.0000° from 90°
pivot moves    : 0.0000 px over all facings        (the ground centre is camera-invariant, measured)
scale          : 32.000 px/m across, 20.569 px/m of ground depth
cell   0    1    2    3    4    5    6    7
dir    0    7    6    5    4    3    2    1
face   N    NE   E    SE   S    SW   W    NW
```

So the SHEETS are a plain clockwise compass and the mirrored `contract.facings` never reaches a
placer. The consumer side reads those measured bearings out of `yardIso.contract.json`
(`cellFaceBearings`) and SEARCHES them — `YardCatalog.FacingFor` — rather than computing
`round(bearing / 45)`, with a negative-control test that hands it a deliberately mirrored table and
fails if the answer does not follow. The kit author's re-emit/rename ask still stands; nothing is
blocked on it.

## The lawn is still not here — but the channel is now CLEARED

**The kit ships no lawn.** The lawn ruling asks for a mown-grass **splat channel** (ADR 0028); the
`ground` family here is four *props* — stepping stones, gravel apron, mown edge, creeping cover.
There is no terrain tile in this drop, so painting a lawn still needs either a terrain-material kit
drop or a ruling to reuse an existing channel.

✅ **The byte-zero gate the lead-architect set on #604 is DONE and it PASSES.** The committed
`SplatE` PNGs were decoded and counted, both regions, all four channels:

```
StPetersSplatE.png        1520×1040   R 9,326 nonzero   G 4,692   B 0/1,580,800   A 0/1,580,800
NineMileCreekSplatE.png   1520×1120   R 32,262          G 4,429   B 0/1,702,400   A 0/1,702,400
```

`E.b` and `E.a` are byte-zero in both regions, so a lawn material may adopt either without the
opaque-default trap firing (`_SplatD`'s Properties default is `"black"` = alpha 1, which is what
makes a previously-unread channel dangerous). Capacity is unchanged: 18 materials over five splat
maps, those two free. **What is still owed is the ART, not the check** — a terrain-material drop, or
the owner's call to reuse an existing family, plus his mow-stripe ruling, which is unanswerable until
there is a lawn texture to stripe.
