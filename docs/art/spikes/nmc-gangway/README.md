# The brow that could not meet both its ends (Nine Mile Creek)

Plates for **#748**, shot from the running game through `Camera.main` by
`NineMileCreekGangwayPlatePlayTests` — which re-places the wharf through `NineMileCreekWharf.Place`,
so every frame is the code the owner's next Build click will run, and **asserts what each plate is
supposed to show** so the pair cannot silently stop matching its own caption.

| plate | |
|---|---|
| `01-the-brow-at-low-water` | drop 4.687 m, rung 8. The basin has dried out under her; the brow stands at better than 1:3 |
| `02-the-brow-at-mean-water` | drop 2.600 m, rung 4 |
| `03-the-brow-at-high-water-735s-refusal-re-shot` | drop 0.413 m, rung 1. **#735's `04-REFUSED` re-shot** — the hinge that hung 2.0 m in the air is on the apron |
| `04-the-piles-stand-while-the-raft-rides` | the same three, cropped on the landing: the piles hold one screen position while the dock travels 3.27 units past them |

All three are shot inside one working day's daylight (09:00–16:00). That is not a look: the tide and
the sun come off ONE clock, so an unconstrained search returns the month's deepest low at 03:00 and its
highest high at dusk — three plates in three different lights, in which the thing that actually changed
is the least visible difference on screen.

---

## What #735 refused, and what this pass found underneath it

#735 named ONE defect in the brow — the drawn ramp was baked into the raft's cell, so it rode with the
raft and its hinge swung 4.3 m through the tide. Fixed: the brow is its own cell, on its own slope axis,
placed once. Measured in the running game, at all three states of tide:

```
brow y  66.0166   66.0166   66.0166      <- the pack's datum line, planY - PackDatumRise
piles y 66.0166   66.0166   66.0166
raft y  64.2494   65.8478   67.5232      <- 3.27 units = 4.27 m of ride
rung         8         4         1
```

Building it found a **second** defect, and the brow is the instrument that could find it. It is the
first thing in this region that has to meet TWO drawn objects at once:

| | placed by | its drawn deck lands at |
|---|---|---|
| the apron's east face | `NineMileCreekQuayFace.PivotForLip` | the wall's **plan lip** |
| the float run (#735) | pivot on the run's plan line, baked deck quoted in the **rig's** frame | `planY + deckGame × 0.766` |

Each looked right alone. Between them sat a **constant 2.298 units** — `3.00 m × 0.766`, the apron's own
drawn height — **in the wrong direction**, at every state of the tide, so a correctly-sloped ramp climbed
from the wharf *up* to the float. Two bugs that pushed opposite ways and partly cancelled:

```
FRAME    the baked deck handed to FloatingPlatformVisual was the RIG's 2.82 m (above LOWEST water),
         against DeckElevationNow(), which answers in the GAME's frame (above MEAN water).
         2.2 m of frame  =  +1.685 u, LIFTING her.
ANCHOR   the plan line is where a piece's DECK belongs, not its PIVOT. The pack pivots at chart datum,
         BakedDeckZMetres x 0.766 below it -- exactly where PivotForLip has always put the apron's.
                          =  -3.983 u, DROPPING her.
                                                              net  +2.298 u, constant
```

Closed by naming the rule: **every wharf-pack piece's chart datum sits on one drawn line**,
`planY − PackDatumRise`. #735's ride is untouched — it was always the delta from the baked state.

---

## The slope axis, and why the shear was refused

**One cell plus a runtime shear was refused by measurement.** Rendering the ramp at both ends of the tide
and differencing it column by column, pivot-aligned:

| facing | residual of a pure per-column vertical shear, over 4.28 m of drop | per-column ink span identical |
|---|---:|---:|
| 0 / 4 — run along screen X | max **1.75 px**, rms 0.43 | 316 / 392 columns |
| 1 — run diagonal | max **7.70 px**, rms 1.76 | **11 / 298 columns** |

At 45° in plan, screen X no longer says where along the ramp a pixel is, so the picture genuinely
*changes* rather than being displaced. A shear serves 2 facings of 8.

So the sheet carries the ladder: 8 facings across, **9 rungs down**, index `rung × 8 + facing`, ends
derived — `[clearance − freeboard, tideRange + clearance − freeboard]` = 0.40 → 4.80 m in 0.55 m steps.
Nine is what the cap and the raft agree on: 8 × 9 packs to 3256 × 3636, 460 px inside the 4096 cap and
still under `sheetedPier`'s 3784, while ten would pack to 4040 px tall — 56 px of headroom on a
*parametric* cell, which is not headroom. In the baked pixels the ladder is monotonic and even: facing 0's
ink grows 59 → 167 px in 13.5 px steps, which is `0.55 m × 0.766 × 32` to the pixel.

**The rung rounds UP**, never to the nearest: flatter than reality lifts the foot off the planks and shows
daylight under the rollers, while steeper settles it into a raft drawing 17.4 px of hull. Worst residual
is one step, 13.5 px.

⚠️ **Two things the plate run caught that no amount of arithmetic did.**

1. **An exact tie cost a full rung.** The ladder and the drop are derived from the same three numbers by
   two routes, so rung 4 came out `2.5999999999999996` against a 2.600 m drop — 4.4e-16 m short — and a
   strict comparison drew rung 5, settling the foot a whole 0.55 m step into the planks. At mean water,
   the most common state of the tide. Fixed with a named 1 mm tolerance
   (`GangwayVisual.RungToleranceMetres`), which is 0.02 of a sprite pixel and cannot let the ramp be
   meaningfully flatter than the truth. That tolerance is load-bearing rather than cosmetic: the
   abutment is MEASURED off the terrain at 3.00022 m, not the authored 3.00.
2. **The collars were left standing.** See below.

---

## The piles — and the collars, which the first plate caught

`timberFloat` is re-baked `{ guidePiles:false, chain:false }` and its cell moves **300×348 → 213×202**.
The fixed furniture becomes `floatPiles`, which measures **300×348 at pivot 149,216 — exactly the cell
`timberFloat` used to have before the split**, so the piles land where #735 drew them.

⚠️ The first plate run drew sixteen galvanised collars hanging three units above a dock that had gone
down the piles without them. **A guide hoop is a sliding collar bolted to the RAFT** — it slides up and
down the pile as the tide takes her, which is the whole mechanism a guide pile is for. It had been split
into the standing half because the rig tags it `fixed`, and that tag answers a DIFFERENT question:
*does this rock with the raft?* A hoop on a pile does not rock, and does ride.

Chasing it found a **third**, pre-existing defect: `fixed` also exempts geometry from the float's own
rock transform, which at frame 0 is 0.52° of pitch and 12 mm of heave. A collar bolted to a rocking raft
rocks; this one never had. Correcting both moves `plasticFloat`'s cell by a pixel and `floatSet`'s and
`plasticSet`'s pixels — all re-measured and recorded. Plate `04` is the after.

Named cost of the split: one cell used to hold both and the rig's own z-buffer sorted them against each
other. Two cells get one rung each, and the piles draw *under* the raft (a guide pile stands on her north
side, away from this camera), so the mooring chain — which lies south, in front — now draws behind the
raft where it used to draw in front of it, hiding the topmost link or two at her corner.

---

## Still open, for the owner

**The worst-case residual lands at high water**, which is when the dock is most visible. At the daylight
high water the drop is 0.413 m against rung 0's 0.400, so the brow takes rung 1 and its foot settles
13.2 px into a raft drawing 17.4 px of hull — inside the stated bound, with 4.2 px to spare. Buying it
down means a non-uniform ladder (finer at the shallow end, where a rung is a bigger fraction of the drop)
or a second sheet. Both are re-bakes, and which — if either — is worth it is a fidelity call.

`plasticFloat` still bakes its own piles and chain in one cell. Nothing uses it and the charter named
only `timberFloat`; the same lie is still in that cell.
