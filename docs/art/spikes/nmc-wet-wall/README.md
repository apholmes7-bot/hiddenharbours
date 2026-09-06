# The wall that holds water (Nine Mile Creek berth trench)

Plates for the wet-wall PR. The owner, after a playtest: *"the bullpen should always have water at low
tide so all the lobster boats can park on the wall."*

| plate | what it shows |
|---|---|
| `01-the-fleet-afloat-at-dead-low-spring` | **the acceptance.** Dead low spring (−2.08 m, the lowest daylight water in the cycle): four boats lying afloat against the wall in deep water, skippers on deck, the ladders and fenders behind them |
| `02-the-same-wall-on-mains-ground` | the same wall on main's ground at −1.60 m of tide — *higher* water than the plate above — with the fleet sitting on bared mud |

⚠️ **`01` puts the quay face on #734's rung at runtime.** Without that fix the moored fleet is drawn
inside the wall and no plate of this branch could show boats at all. Nothing of this PR's own code is
involved in that.

⚠️ **There is no live before/after pair, and that is a measurement rather than an omission.** Stripping
the trench from the terrain component mid-play does *not* change the picture: the water's own height
field is baked once into a scratch texture and does not follow a live terrain edit, and the splat
ground reads the committed painted map. A true A/B needs the old code rebuilt, which this PR did not
spend. The numbers are the evidence instead:

| | main | this branch |
|---|---:|---:|
| bed under berth 3 | −1.60 m | −4.21 m |
| water there at spring low | **dries by 0.60 m** | **2.01 m** |
| water 3 m outboard (the widest hull's own side) | dries | 1.60 m |
| shallowest metre of the way out, berth 1 → the bay | dries | 1.70 m |
| painted seabed texels moved | — | 5 613 of 1 702 400 (0.330 %) |
