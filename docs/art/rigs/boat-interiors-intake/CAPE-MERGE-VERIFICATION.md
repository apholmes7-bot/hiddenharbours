# The Cape Islander rig merge — verification

**The third sha: `60d127c3e77817ea5961419440d5c3ea6d563014c2aabdb621b33376cdb0873f`** (LF, 38,124 B,
594 lines), landed in **both** `docs/art/rigs/capeIslanderIsoRig.js` and the kit's
`hull-rigs/capeIslanderIsoRig.js` — the same bytes in both places, which is what lifts the
do-not-adopt flag.

Everything below was measured, not argued. No Unity ran in this lane; the whole proof is the
repo's own ClearScript V8 (`Assets/_Project/Plugins/Editor/JsEngine/`) driven from a standalone
`net8.0` console app, which runs the rig `.js` **unmodified**.

---

## The parents, and which one is the base

| | sha256 (LF) | lines | carries |
|---|---|---|---|
| base — the #227 import (`4f048395`) | `47714ce0…` | 394 | the common ancestor of both axes |
| **ours — repo main** (`1b35453d`) | `92c3061b…` | 534 | #247 washboards · #508 OKLCH paint |
| theirs — the interiors kit | `a3be1d61…` | 454 | the aft DOOR · the published loft |
| **merged** | **`60d127c3…`** | 594 | all four |

**Repo main is the base of the merge**, per bar point 1. Exactly two things are re-applied from the
kit. The merge was built by classifying all nine hunks of `diff main kit` and resolving each:

| hunk | what it is | resolution |
|---|---|---|
| 1 | header docstring + export list | **union** — main's PASS 2 (colourways) kept, the kit's door/loft para joins as PASS 3 |
| 2 | the OKLCH paint mixer (137 lines) | **MAIN** — the kit does not have it |
| 3 | `DOOR` record, `FYb`/`FYt`/`AY` at module scope | kit |
| 4 | **washboards** | **MAIN** — the kit's copy reverts #247 |
| 5 | aft wall: one flat face → three bands | kit |
| 6 | flat `'dark'` panel → vestibule | kit |
| 7 | `palette(opts)` inside `_paint` | **MAIN** — the kit does not have it |
| 8 | `doorFaces()` + `render` composes it | kit |
| 9 | `doorMount`/`halfAtZ`/`sheerZ`/`HOUSE`/`loft` + exports | kit + **union** |

Three hunks are where main wins, and they are exactly the two survivals: #508 (hunks 2 and 7) and
#247 (hunk 4). **The kit's file reverts both** — which is why neither parent could be taken whole.

---

## THE CONTROL — run first, because an A/B from an unfaithful harness means nothing

The harness reproduces **both committed sheets byte-for-byte** from the #227 rig:

```
CapeIslanderIso.png      3648×420    8/8   cells byte-identical
CapeIslanderIsoRock.png  3648×3360  64/64  cells byte-identical
CONTROL: PASS
```

It also caught two real errors before they became false results:

- The intake's named axis-B base (`d8bb0caa`, blob `a556b07f`) is the **lobster's** rig, quoted in
  the handoff as the analogous #660 reference. The harness's `CapeIslanderIso missing` guard caught
  it on the first run. The cape's real base is `47714ce0` at `4f048395`.
- A colourway id used for the paint cross-check (`harbour-slate`) **does not exist**, and the rig
  silently fell back to its default — 0 bytes differed, which would have read as a passing test.
  Re-run on a real id (`wharf-teal`).

### ⚠️ A pre-existing fact this measured, which is NOT caused by this PR

Main's rig **already** disagrees with her committed exterior sheets, by 323–999 bytes per cell. The
sheets were baked at **#224 — before the rig was imported at #227** — and the difference is entirely
#247's washboards (committed shows sage-green hull where the rig now draws wood side-deck). The
exterior sheet re-bake is its own queued lane. Flagged, not fixed.

---

## Point 1 — the diff classifies to door + loft, asserted by classification

| | result |
|---|---|
| #508 OKLCH paint block (6,874 chars) | **byte-identical** main → merged |
| #247 washboard block (879 chars) | **byte-identical** main → merged |
| changed lines by class | door 45 · loft 8 · export/docstring plumbing 22 · blank 1 |
| exports dropped from main | **none** |

---

## Point 2 — the interior-measured inputs, against BOTH parents *and* the base

`L`/`TH`/`DECK`, `NSEG`, the house envelope (`HX`/`HY0`/`HY1`/`HZ0`/`HZ1`), `ROOFZ`, `SOLE_U`, the
offsets table `T`, `station()`, `skin()`, `dfrac()` — **identical in all four files**.

`FYb`/`FYt` keep their values (`2.54` / `3.10`) and change only **scope**, hoisted to module level
so `HOUSE` can read them. That hoist was measured in isolation and is a **0-pixel no-op** across all
72 poses (variant `vBC` below).

---

## Point 3, as ruled — the pixel proof, all 72 shipped poses

Poses are the shipped grid: `CapeIslanderIso.png` 8×1 + `CapeIslanderIsoRock.png` 8×8 = 72 cells at
456×420 RGBA. Boxes are **computed from the rig's own `DOOR` record through the rig's own
`projVert`/`camBasis`** — nothing re-transcribed, nothing fitted to the diff.

```
differing pixels, merged vs main    28,700 of 13,789,440   (0.208%)
  outside box A (door assembly)         38
  outside box B (A + house height)      38
  outside box C (the aft wall)           0     <- the face the doorway is cut into
```

**Zero differing pixels fall outside the aft wall, on any of the 72 poses.**

![all eight facings: main, merged, and the difference mask inside the aft-wall box](qa/cape-merge-facings.png)

*Rows: main · merged · the difference mask, with the computed aft-wall box in blue. Rows 1 and 2 are indistinguishable outside the doorway; every orange pixel sits inside a blue box.*

![the aft face, before and after](qa/cape-merge-aft-face.png)

*The change itself, at 3× on dir 0: main's single flat `'dark'` panel becomes a real opening with cream jambs and header, a wood sill, the `moto` track tube, the iron guide rail, and the sliding leaf resting closed at `doorOpen 0`.*

Every pixel is attributed by staged variants, each rendered and diffed against main:

| variant | what it adds | diff px | outside box A |
|---|---|---|---|
| `vBC` | the module-scope const hoist only | **0** | 0 |
| `vD` | + the aft wall split into three bands | 970 | **38** |
| `vDE` | + vestibule and slider hardware | 20,230 | 38 |
| `merged` | + the posed leaf | 28,700 | 38 |

So **all 38** of the pixels outside the tight door-assembly box come from **one edit** — the aft
wall becoming three bands, i.e. the opening itself. They sit at the house's port-aft corner, ~26 px
from the box, where the aft wall meets the port wall at equal depth: splitting one quad into three
changes the interpolated depth in its last bits and flips the tie-break. They are recolours at a
seam, no silhouette change, and they are not reachable by any correct merge that cuts a real
opening. Nothing from the paint or the washboards is involved.

### ⚠️ The bar's second clause was inverted, and no merge could have met it

Point 3 asked for *"full byte-identity on the bow-on facings (N/NE/NW)"*. Measured:

| dir | 0 N | 1 NE | 2 E | 3 SE | 4 S | 5 SW | 6 W | 7 NW |
|---|---|---|---|---|---|---|---|---|
| px changed | 10,675 | 7,848 | 1,403 | **5** | **3** | **112** | 863 | 7,791 |

The aft face is **visible** on dirs 0/1/7 — the ones labelled N/NE/NW — and **hidden** on dirs
3/4/5. The labels are nominal; `CapeIslanderSheetSliceTests` already warns the art is baked
counter-clockwise. And no facing is perfectly zero: the stern-hidden three still carry 120 px over
27 poses, all recolour, no silhouette change.

The aft-wall bound above is what the clause was reaching for, and it holds: #508's paint touches
every hull pixel, so a paint regression cannot hide behind the door; #247's washboards are side-deck
geometry well outside it. Both survivals stay proven.

---

## #508 is LIVE through the merge, not merely present in the source

Re-rendered the entire 72-pose grid under a real non-default colourway (`wharf-teal`):

- 28,700 px differ merged-vs-main — **identical count and identical per-facing distribution** to the
  default colourway, so paint and door are orthogonal;
- **0** outside the aft wall;
- the colourway genuinely repaints (69,881 B on main, 69,947 B on merged, one cell).

---

## Point 4 — the deck re-stamp is a hash move, not a re-measure

Proved before re-stamping: **1,648 / 1,648 checks identical** between main's rig and the merged rig —
`W`/`H`/`PX`/`DIRS`/`pivot`/`defaultElev`/`order`, `HELM`, `HAULER`, `TUBS`, `ROCK` and `rock(i)`
for all 8 frames, and `helmSeat`/`haulerMount`/`tubMounts`/`navMounts` at all 8 facings under the
base pose **and** all 8 rock poses.

**The convention is preserved deliberately.** Her existing pin `425ff37d…` is verified here to be
the **CRLF** hash of the LF file, and `DeckSidecarImportParityTests` asserts string equality, so the
new pin is the CRLF hash `e1004316…`. Switching this field to LF is a separate standing ask and was
explicitly overruled for this PR.

## Point 5 — the kit's interior sidecar

`hullRigSha256` re-stamped to the third sha in its own (LF) convention, `60d127c3…`, with no
re-measure — which point 2's byte-identity is what authorises. One entry, one stem, so the
unanimity rule is satisfied.

---

## Still open, and deliberately not decided here

**Her resting look is the owner's call** (`s0-verdicts.json` → `_open_owner_item`). The jambs,
header, sill, track and rail are drawn at *every* `doorOpen`, so the aft face changes whichever pose
ships — he is choosing **how**, not whether. This merge ships the kit's published default,
`doorOpen 0` (closed), and does not pre-empt him. If he rules otherwise, the single literal to change
is `RigMeshExtractor.Reconstructions["capeIslanderIsoRig.js"]["F"]`.
