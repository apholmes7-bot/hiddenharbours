# The trees under the sun — plates for `feat/tree-sun-lighting`

Shot from the **live editor in play mode on the real St Peters scene** (259 trees, 148 shrubs, 384
shore plants), at the **shipped exposure** — `Camera.main` into an `ARGBHalf` target, clamped and
gamma-encoded on readback, so the day/night overlay, the light quads and the cast shadows are in the
frame exactly as the player sees them. The game clock was frozen (`Clock.TimeScale = 0`) at each
hour; the weather is whatever the deterministic sim gives that hour, and each plate says which.

| plate | what it answers |
|---|---|
| `01-response-off-vs-on.png` | **The ruling.** Does turning `Tree.mat`'s `_LightResponse` on make the trees read the sun? |
| `02-the-day.png` | The whole day at the shipped exposure, tree beside its shrubs. |
| `03-the-rake.png` | Does the shadow swing and lengthen, and does it agree with the lit side? |
| `04-the-wood.png` | A stand at dawn and at noon — the rake length and the stacking question. |
| `05-ysort-walk.png` | Does the fisher draw wrongly against a trunk or a canopy? |
| `06-the-control.png` | **The regression control.** The shared include is used by the shrubs and shore plants too. |

## The measurements behind them

**The lit side swings.** Simulated per texel on the pass-3 sheets: the catch's centroid sweeps 15 px
of a 269 px white pine and 30 px of a 331 px red oak between dawn and dusk, monotonically. A fixed
left/right half-split reports almost nothing (~5 % of the catch) — a moving highlight is not measured
by a fixed split; the mean over half a crown averages the gradient away.

**No canopy-special dials.** Sun catch as a fraction of the texel's own albedo luminance at 13:00 —
shore plants 0.35–0.71, shrubs 0.23–0.56 (both families already shipped at `_LightResponse 1` and
accepted by the owner), trees at the same dials **0.54–0.69**. The trees land inside the accepted
range, so `Tree.mat` keeps the shared numbers.

**The control is bit-exact.** Plate 06 renders both arms inside ONE main-thread call, so the grass
wind and every other `_Time` term are identical between them — the noise floor is not estimated, it
is **0 pixels**. At 16:00, weather factor exactly 1.0000: **0 of 577,600 pixels differ.** At 13:00
under a light haze (factor 0.875): 1.46 % differ, worst 1.6 LSB — the new agreement working.

**The shadows, wind frozen.** At 07:00 the sun shadows darken 45 % of a wooded frame by a mean 8.6 %
of the pixel they land on (3.6 LSB); 7.5 % of the frame receives more than twice the median single
shadow. At 13:00 they darken 26 % by 24 % (17.5 LSB) and barely stack (1.6 %). So the dawn rake is
long (40.8 m off a red spruce, 54.8 m off a white pine) and faint; the noon shadow is the one that
reads.

**A plate is not the verdict.** A silhouette on a black ground overstates a read defect and a still
frame understates a walk — the owner's eye in the scene is the gate.
