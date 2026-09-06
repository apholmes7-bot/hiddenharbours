# The sun shade — a receiver reads shaded

Evidence for `feat/sun-shade-buffer`, the fix [#720](https://github.com/apholmes7-bot/hiddenharbours/pull/720)
traded away. Every plate is **A | B — the LEGACY ARM on the left, the SHADE ARM on the right**
(`SpriteShadowProfile.ScreenSpaceShade` off / on, and it **ships off**).

Both arms are shot in one main-thread call against one camera with the sun frozen and nothing running on
`_Time`, so two shots of one arm are byte-identical: the noise floor is **0 px** and "darker" means darker.

## Why there is anything to look at

`SpriteShadow` sorts a sun shadow one order UNDER its caster, so it darkens the GROUND and nothing else —
anything standing in it draws over it at full brightness, by construction rather than by tuning. That is
why #720 could ship "the ground under a crown is 6.1 % darker" and could not ship *"the fisher reads
shaded"*. The shade arm composites the same silhouettes over the assembled frame as one multiply, in the
band the lamp shadows already occupy, so every pixel under the shade loses the same fraction whoever drew
it.

## The greybox plates

From `SunShadeReceiverRenderTests` (EditMode, GPU, self-skips on CI). A mid-grey ground, one solid
1.5 × 2 m caster, and a receiver standing north of it inside the rake — deliberately plain, so the number
is unambiguous.

| plate | what it answers | legacy arm | shade arm |
|---|---|---|---|
| `plate-01-a-receiver-in-a-cast-shadow.png` | ⭐ **the item #720 could not deliver** — a receiver standing in a cast shadow, measured over HER OWN PIXELS | **0.00 %** | **23.45 %** darker |
| `plate-02-a-receiver-at-the-trunk-foot.png` | the other half — standing in the ground-contact pool a crown throws straight down | **0.00 %** | **21.50 %** |
| `plate-03-a-mesh-hull-under-the-shade.png` | a boat reads shaded — the receiver is a real `IsoFacetHullRenderer` dory, not a sprite | **0.00 %** | **25.15 %** |
| `plate-04-the-cost-something-over-the-shade.png` | ⚠️ **the cost, not hidden** — the same figure sorted ABOVE everything: a gull, a boat's upper works, a roof edge | **0.00 %** | **23.45 %** |

**The ground did not change hands.** Measured *inside the rake*, the ground reads **23.96 %** darker in
the legacy arm and **24.22 %** in the shade arm. The two arms compose differently — alpha-over pulls a
pixel toward the shadow tint, a multiply scales it down by a fixed fraction — so they are not expected to
be equal; the guard is that the shade arm must not take shade AWAY from the ground to give it to what
stands on it.

**The two arms differ on 6.00 % of the greybox frame.** An A/B whose arms agree has proved nothing, so
that is asserted, not merely reported.

## The scene plate — the one his eye rules from

`plate-05-st-peters-the-fisher-under-a-tree.png` — the real St Peters wood at noon (elevation 0.90, clear),
438 sun casters ticked, the fisher's own idle sprite stood twice: once at the trunk foot of the tallest
white pine (13.81 m) and once out in its 11.26 m rake.

| | legacy arm | shade arm |
|---|---|---|
| the fisher **in the rake** | — | **21.22 %** darker |
| the fisher **at the trunk foot** | — | **24.37 %** darker |
| the frame as a whole | mean luma 83.1 | mean luma 76.5 — **75.95 %** of pixels change, by a mean of **11.75 %** |

⚠️ **That last row is the honest warning.** In a dense wood at noon most of the frame is under someone's
canopy, so most of the frame moves. Whether that reads as *shade* or as *a darker game* is exactly the
call the plates cannot make — judge it in play.

## The cost on this GPU (RTX 4060), in the real scene

60 renders of St Peters at 720 px, timed **four times in both orders** so neither arm can win on cache
warm-up:

| | pass 1 | pass 2 | mean |
|---|---|---|---|
| legacy arm | 6.315 ms | 4.078 ms | **5.197 ms/frame** |
| shade arm | 4.831 ms | 3.185 ms | **4.008 ms/frame** |

**The shade arm is 1.19 ms/frame CHEAPER (−22.9 %)**, and it is cheaper in both orders — including the
pass where the shade arm ran first. The reason is batching: 438 shadow renderers stop being interleaved
through the decor band and collapse onto two fixed sorting orders sharing one material.

> This is an offscreen editor render with `RenderTexture.Release()` in **both** arms (needed for the
> stencil — see below), not a play-session frame time. The **delta** is the signal, not the absolute.

## The question the plates put to the owner

Plate 04 is the whole trade in one frame. **Both arms are wrong in some frame:**

- **Legacy (shipped).** Nothing standing in a sun shadow is *ever* shaded. A fisher at a trunk foot at
  noon reads as standing in a field.
- **Shade arm.** Everything under the shade in SCREEN space is darkened, including something that is
  above it in the world rather than standing in it.

The lamp shadows already accept the second cost (`LampShadowSystem`, #698). Whether the sun should too is
the ruling this asks for, and it is one checkbox in `Resources/SpriteShadowProfile.asset`.

## ⚠️ A fixture trap for whoever renders sun shadows next

The sun shadows claim each pixel through the STENCIL (#720: first shadow at a pixel wins). A persistent
`RenderTexture` driven by repeated `Camera.Render()` in EDIT MODE is **not stencil-cleared between calls**
— measured: a stencilled sprite drew **1600 px on the first render and 0 px on every render after it**,
while the same sprite at `Comp Always` drew 1600 every time. Read the second shot of a pair — the standing
habit that guards against a cold shader cache — and the feature looks completely dead. Every number here
read `0.00 %` until this was found. `RenderTexture.Release()` before each render is the fix.
