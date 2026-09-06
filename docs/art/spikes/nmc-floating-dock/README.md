# The floating dock that was never drawn (Nine Mile Creek)

Plates for **#735**, shot from the running game at its own framing (`live-editor-plate-recipe`) with the
real builder re-placed live, so every one of them is the code the owner's Build click will run.

The finding: `NineMileCreekWharf.PlaceFloat` built a `FloatingPlatform`, a `GangwayPlatform` and eight
`FloatCleat`s — 48 m of dock at y = 70, a 12 m brow off the apron's east face, eight berths, two of them
occupied — and gave them **no `SpriteRenderer` anywhere**. Bernard Celeste's and Dan Peters' boats lay in
open water, tied to a dock that was not there.

| plate | what it shows |
|---|---|
| `01-the-float-run-before-and-after` | top: two boats in empty water. bottom: both lying alongside a timber float run, drawn over it. Same frame, same clock, same freshly-placed dressing — the only difference is this PR |
| `02-the-dock-from-the-apron-mean-water` | the run from the wharf. The brow in this frame is the walkable `GangwayPlatform`'s line, not a drawn ramp |
| `03-the-run-at-spring-low-and-spring-high` | top spring low, bottom spring high. The dock rides: 4.28 m of deck travel, 3.28 units of screen. She never grounds — the 2026-08-19 channel runs under her |
| `04-REFUSED-the-brow-at-spring-high` | **why the gangway is not in this PR.** The only drawn ramp the pack owns is baked into `floatSet`'s raft cell, so it rides with the raft. Its hinge sits at `deck + 2.38 m` against this wharf's +3.00 m apron — exact at mean water, and here, at spring high, hanging 2.0 m in the air off the end of the wharf |

**Known and not fixed, visible in 03:** `timberFloat` bakes the guide piles and the mooring chain into the
raft's own cell — and the rig tags exactly those `fixed`, because they are driven into the seabed — so they
ride with the dock instead of standing still. The fix is a re-bake at `{ guidePiles:false, chain:false }`
plus the fixed furniture as its own cell, not code.
