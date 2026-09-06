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
| `05-creek-wall-0200-no-pool-BEFORE.png` | the Nine Mile Creek mooring wall, the fleet alongside |
| `06-creek-wall-0200-pool-AFTER.png` | the same wall, lit — a taller lamp pooling broader and flatter |
| `07-beam-on-the-dock-no-pool-BEFORE.png` | the searchlight aimed at the pier from seaward |
| `08-beam-on-the-dock-pool-AFTER.png` | the same beam putting a wedge of light on the planks |

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

## The charter's other two locations

**Nine Mile Creek, the mooring wall with the fleet alongside** (05 → 06). A different question from the
pier, and two of them:

| | no pool | pool |
|---|---|---|
| pixels the lamps light | — | **62,449 (5.78 %)** |
| mean luminance there | 0.1055 | **0.1864 — 1.77×** |
| relative local contrast | 0.1196 | **0.1051 (0.878×)** |

⭐ **The pool is broader and flatter than the pier's, and nothing tuned it.** Both lamps carry the same
preset and the same 3.6 m reach; the creek's is a 4.48 m `streetLamp` where the pier's is a 2.46 m
`lanternPost`, and `h/√(h²+d²)` does the rest — 0.831 against 0.634 at three metres out. The lift is
correspondingly gentler (1.77× against the pier's 3.06×) because the same light is spread over more
ground. If the two regions' pools looked alike, the shape would be decoration rather than geometry.

It also puts the pass over the one receiver the pier has none of: **a mesh hull**. The fleet along that
wall is `IsoFacetHullRenderer` geometry rather than sprites, and a screen-space multiply lights her exactly
as it lights the quay, with the facet path untouched.

⚠️ The two small white beads on the quay in **both** arms are pre-existing lamps of the region's own — they
are identical in the BEFORE plate and are not this change.

**The searchlight on the dock it sweeps** (07 → 08). The half of the owner's sentence #733 could only half
answer: he said the beam *"doesnt read on water or enviroement"*, and #733 fixed the WATER by pulling the
quad back so the sea's own N·L relief (#691) could read through it. This is the ENVIRONMENT — a cone lamp
is a point lamp with an angular gate, so the same pool machinery lays a wedge of light on the planks:
**30,910 px, 1.47× brighter**, at the beam's **full 9 m reach against its 2.7 m bloom**. That last pair is
the point: #733's source-glow dial shortens what the lamp LOOKS like and not what it LIGHTS.

⚠️ **Shot at 02:00, where the charter names 06:13.** Declared rather than glossed: at 06:13 the night gate
is already closing, and #691's own correction to this arc records that the shipped searchlight photographs
faintly at that hour by design. 02:00 is where the mechanism is visible; the hour does not change what is
being shown, and the owner's judgement of the beam at dawn is a separate question about the gate's timing
rather than about whether a beam lights ground.

## ⭐⭐ The glow was at the post's FEET, and the owner caught it on these plates

**Him, 2026-09-05:** *"the glow emitters seem to always be at the base and not the lantern lens."*

He was right, and it had been true since lamp posts were placed — every plate in this folder and in
`lights-are-sources/` showed it before this fix, and I had read the bead at the bottom of the pier's post
as being at its head. The cause is a fossil: both iso packs pivot these pieces at the **ground centre**, and
the `Lightpost` preset's origin offset is `(0, −0.2)` — nudged DOWN, which was right while the thing being
drawn was a POOL that should fall just below the head, and became wrong the moment #733 made the quad the
FITTING. A fitting-sized glow at a post's foot is a lamp glowing out of its own base.

`SceneLight.BloomLiftMetres` raises the bloom to the lamp, from each piece's own **lens height** read off
its rig — 2.09 m for `lanternPost` (the flame prism under its cap, `wharfDecorRig.js:990-994`), 4.19 m for
`streetLamp`, 7.02 m for `yardLight`, 7.63 m for `floodMast`. Deliberately *below* each piece's published
drawn height, because a lantern hangs under its cap and the eye checks a glow against the drawn glass.

⚠️ **`WorldOrigin` is NOT lifted, and that separation is the point.** The pool centres on it and every cast
shadow measures its lamp-to-caster distance from it, and both of those are asking where the lamp *stands*.
Lift them with the glow and the pool floats up the screen with it.

⚠️ **Vertical only — the horizontal is owed.** A swan-neck lamp's lens hangs 0.8 m to one side of its mast
and a cobra head 1.53 m, and that offset **rotates with the piece's facing**, so it cannot be a constant.
The right answer is the `lamp` ANCHOR the pack already declares a kind for
(`pointKinds: [wires, secondary, lamp, drop]`) and does not yet publish coordinates for — reading it needs a
rig re-bake, which is its own PR. Until then a swan-neck lamp glows from the top of its mast rather than the
end of its arm: much closer than its feet, and honestly short of the lens.

## ⏳ The owner's second point, not in this PR

**Him, same message:** *"the lenses should glow on anything emitting light, glass, lense, windows."*

Correct, and it is a different mechanism from anything here. A lamp's own lens pixels are part of its
SPRITE, drawn below ADR 0013's whole-frame multiply, so they are crushed to near-black however bright the
lamp is — a bloom sits *over* the glass rather than making the glass itself read as lit. #723 solved exactly
this for a boat's cabin (`BoatWindowGlow` draws the panes as their own rectangles above the tint, because
*a pre-multiply lift cannot make a lit window*), and the same treatment is owed to every land lens, pane and
piece of glass. It is its own PR and it is named here so it is not lost.

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
