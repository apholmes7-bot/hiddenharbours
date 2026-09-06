# The brow that could not meet both its ends (Nine Mile Creek)

The gangway pass for **#735**'s two refusals: the brow that was built, plated and taken out, and the
guide piles that rode 4.4 m of tide with a dock they are driven into the bottom beside.

---

## What #735 refused, and what this pass found underneath it

#735 named ONE defect in the brow — the drawn ramp was baked into the raft's cell, so it rode with the
raft and its hinge swung 4.3 m through the tide. That is fixed here: the brow is its own cell, on its
own slope axis, placed once.

Building it found a **second** defect, and the brow is the instrument that could find it. It is the
first thing in this region that has to meet TWO drawn objects at once:

| | placed by | its drawn deck lands at |
|---|---|---|
| the apron's east face | `NineMileCreekQuayFace.PivotForLip` | the wall's **plan lip** |
| the float run (#735) | pivot on the run's plan line, baked deck quoted in the **rig's** frame | `planY + deckGame × 0.766` |

Each looked right alone. Between them sat a **constant 2.298 units** of screen height — `3.00 m × 0.766`,
the apron's own drawn height — **in the wrong direction**, at every state of the tide. A correctly-sloped
ramp between those two ends climbs from the wharf *up* to the float.

It decomposes into two bugs that pushed opposite ways and partly cancelled, which is why the dock looked
nearly right:

```
FRAME    the baked deck handed to FloatingPlatformVisual was the RIG's 2.82 m (above LOWEST water),
         against FloatingPlatform.DeckElevationNow(), which answers in the GAME's frame (above MEAN
         water).  2.2 m of frame = 1.685 units, LIFTING her.
ANCHOR   the plan line is where a piece's DECK belongs, not its PIVOT. The pack pivots at chart datum,
         BakedDeckZMetres × 0.766 = 3.983 units below it — which is exactly where PivotForLip has
         always put the apron's face.  3.983 units, DROPPING her.
                                                              net  +2.298 u, constant
```

The fix is one rule, and it is now named: **every wharf-pack piece's chart datum sits on one drawn
line**, `planY − NineMileCreekQuayFace.PackDatumRise`. Put two pieces' datums there and every height
either of them draws is measured from one zero. #735's *ride* is untouched — it was always the delta
from the baked state, and a delta does not move.

---

## The slope axis, and why the shear was refused

A brow's picture is a function of the tide rather than merely of its position: hinge bolted to fixed
ground, landing riding a float, so the drop is the whole tidal range and the slope swings from 1:30 at
spring high to 1:2.6 at spring low. One cell cannot be right at more than one water level.

**Option (b) from the handoff — one cell plus a runtime shear — was refused by measurement.** Rendering
the ramp at both ends of the tide and differencing it column by column:

| facing | residual of a pure per-column vertical shear, over the full 4.28 m of drop |
|---|---|
| 0 / 4 (the run lies along screen X) | max **1.75 px**, rms 0.43 px; per-column ink span identical in 316/392 columns |
| 1 (the run lies diagonal) | max **7.70 px**, rms 1.76 px; per-column ink span identical in **11/298** columns |

At 45° in plan, screen X no longer determines position along the ramp, so the picture genuinely changes
rather than being displaced. A shear can serve 2 facings of 8. A kit cannot ship that.

**So the sheet carries the ladder.** `gangway` is the only key in the pack with a second sheet axis:
8 facings across, **9 rungs down**, cell index `rung × 8 + facing`. The ladder's ends are derived, never
typed — `[clearance − freeboard, tideRange + clearance − freeboard]` = 0.40 m to 4.80 m in 0.55 m steps,
4.40 m of travel, which is the tide itself.

**Nine rungs is what the cap and the raft agree on.** Every rung shares one cell (measured: 407×404 at
pivot 203,278 — the union across all 72) so the object never moves and the hinge is exact by
construction. 8 × 9 packs to 3256 × 3636, 460 px inside the 4096 cap, and still under `sheetedPier`'s
3784 so the family's binding side does not change. Ten rungs would pack to 4040 px tall — 56 px of
headroom on a *parametric* cell, which is not headroom.

**The rung rounds UP**, never to the nearest. A rung flatter than reality lifts the foot off the planks
and shows daylight under the rollers, which reads as broken; a steeper one settles the foot into a raft
that draws 0.71 m — 17.4 px — of hull below her deck. Worst residual is one step, 0.55 m = 13.5 px,
covered with 3.9 px to spare, and the test walks 41 states of tide asserting exactly that.

---

## The piles

`timberFloat` is re-baked `{ guidePiles:false, chain:false }` and its cell moves **300×348 → 199×195**.
The fixed furniture becomes `floatPiles`, which measures **300×348 at pivot 149,216 — exactly the cell
`timberFloat` used to have**. The split is a decomposition at one pivot rather than a re-draw, so the
piles land where #735 drew them and only the raft moves.

Named cost: one cell used to hold both and the rig's own z-buffer sorted them against each other. Two
cells get one rung each. The piles draw *under* the raft (a guide pile stands on her north side, away
from this camera), so the mooring chain — which lies south, in front — now draws behind the raft where
it used to draw in front of it, hiding the topmost link or two at her corner.

---

## Plates

**None yet, and that is stated rather than glossed.** Every plate this pass owes — the run from the
wharf head at spring low / mean / spring high, the brow at all three, #735's `04-REFUSED` re-shot, and
the piles standing still while the raft rides — needs a live editor, and the machine could not start one
(see the PR body). The arithmetic every plate would show is asserted headless in
`NineMileCreekGangwayTests` instead, and the plates are owed before this merges.
