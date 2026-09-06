# The apron's north–south face runs — a band at every seam, and where it came from

Owner playtest, 2026-09-06 evening:

> "wharfs facing north south need to hide the south face between two sections"

## What the bands were

Not a rig defect. `logCrib` is a closed 9.6 × 5.0 m crib and its end return is correct art — the end
of a run has to be drawn. **There was a gap at every seam for it to stand in.**

The sheet is baked at 40° of elevation, so the pixels carry the foreshortening: a metre of ground
travel draws `GroundDepthScale` ≈ 0.643 of a unit in Y and a whole unit in X. The region's ground is
authored 1 unit = 1 m in both axes (ADR 0042's "move-and-measure" regime), so **a 9.6 m crib covers
9.6 units of an east–west wall and only 6.17 units of a north–south one.** `CoverRun` divided the
wall by the plan length either way.

Measured on the placement tables themselves (`NineMileCreekDressing.FacePieces()`, run headless
against the compiled `HiddenHarbours.App.Editor` — no editor):

| run | length | drawn/piece | before | pitch | bare wall per seam | after | pitch |
|---|---|---|---|---|---|---|---|
| NorthWall (E–W) | 78.0 | 9.60 | 9 | 8.667 | — | 9 | 8.667 |
| **WestWall (N–S)** | 43.0 | **6.17** | 5 | 8.600 | **2.429** × 4 seams | **7** | 6.143 |
| **ApronWest (N–S)** | 48.0 | **6.17** | 5 | 9.600 | **3.429** × 4 seams | **8** | 6.000 |
| ApronSouth (E–W) | 10.0 | 9.60 | 2 | 5.000 | — | 2 | 5.000 |
| **QuayHead (N–S)** | 9.0 | **6.17** | 1 | 9.000 | 2.83 undrawn | **2** | 4.500 |
| Breakwater (E–W) | 92.0 | 9.60 | 10 | 9.200 | — | 10 | 9.200 |

**23.4 units of bare wall** across the two long north–south runs, plus 2.83 at the wharf head. The
three east–west runs do not move by a decimal, which is why the mooring face never showed this and
why nothing about it changes here.

## The plates

| | |
|---|---|
| `01-a-band-at-every-seam.png` | the placement as it shipped — 5 + 5 pieces, the two runs pitched 8.6 and 9.6 units, so their bands interleave across the apron |
| `02-one-return-at-the-seaward-corner.png` | the same view at the drawn pitch — 7 + 8 pieces, the deck continuous, and the return drawn **once**, at the seaward end where the wall really does stop |

⚠️ **These two are sprite composites, not game-view captures.** Every number in them is taken from
the code and the committed sheet — PPU 32, `logCrib.png` cell 350 × 386 at pivot (174, 242) from
`wharfIsoRig.contract.json`, cell 6 for seaward EAST and cell 2 for WEST per
`IsoPackSprites.FacingForHeading`, and `PivotForLip` for the offset — and the compositor was
validated against the shipped game plate before either was drawn (below). They are honest about the
geometry and say nothing about lighting, the painted apron's real texture, or anything else a
capture would show.

## How the composite was validated

`docs/art/spikes/nmc-wharf/5-the-aprons-west-side.png` is a real game capture of this exact stretch,
committed with #471. Measured down the middle of the apron's east face in that plate:

- band-to-band pitch **266 px** — against 8.6 units × 32 = 275 px predicted for a 5-piece run
- deck run between bands **193 px** — against 199 px predicted
- band height **≈ 74 px** — against **70 px** predicted for the bare wall between two pieces, and
  against 154 px if the far piece drew over the near one

That last row is also how the draw order was established: the pieces of a run share one sorting
order (`FaceSortingOrder` is a rung per WALL and the band has none to spare), and the shipped picture
resolves them **near over far**. Closing the gap makes that load-bearing — each piece's return now
stands inside its southern neighbour's deck and is hidden only because the neighbour draws over it —
so `FaceRun` must keep authoring the north–south runs from their south end. There is a guard on it.

## What was NOT done, and why

The charter for this lane (`HANDOFF-2026-09-06-wharf-face-end-returns.md`) asked for an `ends` option
on the rig's `crib` family and a new baked key with the return omitted. That was built and measured
first, and it is the wrong fix:

- the option works — `ends:'both'` reproduces the committed `logCrib.png` **byte for byte** (4 323 200
  bytes of RGBA through a standalone ClearScript V8 harness), and `ends:'none'` bakes a 340 × 386 cell
- but removing the end header **opens the crib**, and with a 71 px hole at the seam you then see
  straight into it: the interior headers at x ±1.6 m, the stone ballast and the far wall. A hole with
  its wall taken away is still a hole.
- and the return is not the defect. Once the pieces butt, the return has nothing to stand in, and the
  one place it still shows is the one place a wall really does end.

So no rig change, no new key, no re-bake, and the 19 committed sheets are untouched.
