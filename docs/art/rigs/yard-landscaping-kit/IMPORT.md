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

## What was NOT done here

**No bake, no import, no sheets.** S2 (bake at 32 px/m into `Art/Sprites/Yard/`, spriteMode Multiple
*and* sliced, metas committed, bake cap = import cap) and S3 (placement) are a separate slice — this
lane landed the rig, the sidecar and the measurements a baker will need.

**And the kit ships no lawn.** The lawn ruling asks for a mown-grass **splat channel** (ADR 0028);
the `ground` family here is four *props* — stepping stones, gravel apron, mown edge, creeping cover.
There is no terrain tile in this drop, so painting a lawn still needs either a terrain-material kit
drop or a ruling to reuse an existing channel. Capacity today: 18 materials over five splat maps,
with `E.b`/`E.a` free — and ⚠️ adopting a previously-free channel has its own trap (check the
committed PNG's bytes are zero in that channel first; `_SplatD`'s default is opaque black).
