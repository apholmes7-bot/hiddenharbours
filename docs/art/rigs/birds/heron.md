# Great blue heron

| Rig | Global | Sidecar | Sheet |
|---|---|---|---|
| `heronIsoRig.js` | `HeronIso` | `gameplay/heronIsoRig.gameplay.json` | `birds/sheets/Heron.png` (3072 x 768: 32 columns x 8 facings, cell 96 x 96, pivot 48, 72) |

It draws 1.17 m bill to tail and a 1.83 m wingspan, standing about 1.1 m (the brief: 0.97 to 1.37 m, 1.67 to
2.01 m). Adult: blue-grey back and upperwing coverts, dark slate flight feathers, greyish-buff neck, white
face and crown with a black stripe and plume, yellow dagger bill, dusky legs. Proposed species id:
`bird.great_blue_heron`.

## Its own body, and its own cell

- **Cell 96 x 96, pivot (48, 72).** At 32 px to the metre a heron stands taller than the gull's 64 px cell,
  and its open wing is 59 px. This is the cell the brief asked to be proposed.
- Two-jointed legs: planted, striding and lifting in the contact frame; hanging at take-off and landing;
  trailing straight behind in flight.
- A five-segment neck bent by three angles: the standing S, the coil, the strike, and the fold back onto
  the shoulders in flight.

## States

`fly, glide, takeoff, land, stand, stalk, poise, strike, roost`.

- `stand`: tall and still in the shallows. `stalk`: the slow wade, one foot lifted clear at a time.
  `poise`: frozen over a fish, neck coiled. `strike`: the stab, then head up.
- **`stand, stalk, poise, strike` are baked wading.** The pivot is on the bottom and the water surface is
  baked 0.12 m above it, so the legs under it take the depth tint. The sidecar's `WADE` section gives the
  depths it should be placed in. On dry ground the heron uses `roost`.
- `roost`: hunched and dry, neck folded down, for a rock, a bank or a treetop.
- `fly`: slow heavy beats (150 ms a frame), neck folded, legs trailing.
- Not drawn: `swoop, dive, float, splash, preen, peck, walk, perch`, and a stalk on dry ground.
- Alone: the sidecar's `GROUP` section says one bird, and how far apart two keep.
