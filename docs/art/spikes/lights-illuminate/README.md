# A lamp lights the ground — the plates behind world-lighting PR 2c

**The owner, 2026-09-04:** *"dock lights are just a round glow, it should glow from within the lamp
reasilitcally."*

[#733](https://github.com/apholmes7-bot/hiddenharbours/pull/733) answered the first half: every lamp's
additive quad came down to the size of its lit fitting, so a lantern reads as a lantern. It also left the
pier honestly dark, because ADR 0016's quad is the *source's own bloom* and all it can do is lay a sheet of
cream over the frame. This is the other half, and it is a different picture of the same lamp: the patch of
ground the lantern makes brighter.

| plate | what it is |
|---|---|
| `01-pier-0200-no-pool-BEFORE.png` | #733's frame — the lantern glows, the planks are dark |
| `02-pier-0200-pool-AFTER.png` | the same lamp lighting the ground it stands over |
| `03-pier-0200-pool-and-shadows.png` | the pool with the lamp shadows cut into it |
| `04-pier-noon-control.png` | the same lamps by day |

Both arms are the same frame with one field moved (`LampShadowProfile.PoolsEnabled`), so they differ by the
thing under review and nothing else. Re-shoot with:

```bash
unity test . --mode PlayMode --filter HiddenHarbours.Tests.PlayMode.LightsIlluminatePlatePlayTests
```

## What the plates measure

At 1200 × 900, tint (0.118, 0.134, 0.177) at 02:00, on the St Peters pier:

| | no pool | pool |
|---|---|---|
| pixels the lamps light | — | **39,973 (3.70 % of the frame)** |
| mean luminance inside the pool | 0.0328 | **0.1003 — 3.06×** |
| relative local contrast there | 0.1765 | **0.1455 (0.824×)** |

**The third row is the one that matters.** That the planks are lit is easy to show and easy to fake — the
disc the owner refused lit them too, in the sense that their pixels got brighter. What the disc *did* was
flatten them: it drove this same measure from 0.21 down to **0.0118**, a factor of eighteen
(`docs/art/spikes/lights-are-sources/`, plate 01). The pool holds it at **0.82×**, because it is a
MULTIPLY: `Blend DstColor One` computes `dst × (1 + gain)`, and relative contrast is a ratio, so a uniform
scale cancels out of it exactly. The planks survive by construction rather than by tuning. (The 18 % that
does move is the pool's own spatial gradient — the incidence and edge falloff vary across the patch — not
the flattening of anything.)

## Two things that were nearly shipped wrong

**⚠️⚠️ A multiply is bounded by what it multiplies, so the first working version was invisible.** ADR 0013's
tint has crushed the pier to a mean luminance around 0.04 by the time this pass runs, so a naive `dst × 1.6`
lifts a plank by six values in 255. The first measured run changed **zero** pixels. The gain is therefore
divided by the night's own luminance on the CPU: the factor that reconstructs `albedo × (ambient + lamp)`
from a frame holding `albedo × ambient` is exactly `1 + lamp/ambient`, so the darker the night the larger
the multiplier and the lit ground lands in the same place either way. Same compensation the lit-decor path
and the water's moon glitter already make, and the same law as
*a pre-multiply lift cannot make a lit window*: **check what your pixel is multiplied by before designing a
lift into it.**

**⚠️⚠️ A URP pass with no `LightMode` tag is silently never drawn.** Before `Tags { "LightMode" =
"Universal2D" }` went into the pass, the renderer was enabled, correctly posed, at the right sorting order,
carrying a material whose shader had the name expected — and contributing exactly nothing. No error, no
magenta, no warning.

## The ladder

All three lamp quads sit at `SceneLight.MaxSortingOrder`; the 2D renderer breaks the tie back-to-front along
the view axis, so the depth pins are the whole ordering. Farther draws first:

| rung | depth in front of the camera | what it does |
|---|---|---|
| the **pool** | `LampPoolSystem.PoolDepthOffset` 0.14 | multiplies the ground **up** |
| the **bloom** | `SceneLight.DefaultCameraDepthOffset` 0.10 | adds the lit fitting, so the lamp stays hottest |
| the **shadows** | `LampShadowSystem.ShadowDepthOffset` 0.06 | multiply back **down**, cutting into the light |

That last row is what makes a lamp's shadow and its pool two halves of one picture: a shadow is the
*absence* of this term rather than a separate thing drawn beside it. Pinned by constant in
`LampPoolTests.TheDepthPins_PutThePoolUnderTheBloom_AndTheShadowsOverBoth` and on the live quads in
`LightsIlluminatePlatePlayTests`.

⚠️ **What plate 03 does NOT prove: the pixels.** The obvious measurement — shoot the pool with the shadows
off and on and require the difference to be darker — comes back with the frames identical, and the cause is
not the ladder: the one caster this pier pairs offers a silhouette the fixture has not managed to land in
frame. The pairing and the ordering are both asserted; the photographic version is owed rather than quietly
dropped.
