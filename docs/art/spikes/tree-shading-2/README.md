# The wood's shade — plates for `feat/tree-shading-2`

#715 made the trees read the sun. Its plates then measured what the **shadows** were doing, and this PR
is that list. Shot from the **live editor in play mode on the real St Peters scene** (259 trees, 148
shrubs, 384 shore plants) at the **shipped exposure**, clock frozen per hour.

⭐ **Every before/after pair renders both arms inside ONE main-thread call**, so the grass wind and every
other `_Time` term are bit-identical between them and the noise floor is **0 px by construction, not by
estimate**. The "BEFORE" arm is `SpriteShadowProfile.CreateDefault()` (main's numbers) plus a **runtime
copy** of the shadow material whose `_StencilComp` is `Always` — nothing on disk is touched.

| plate | what it answers |
|---|---|
| `01-the-wood-at-noon.png` | Does a stand's floor read as shade instead of a patchwork of blots? |
| `02-the-wood-at-1600.png` | The worst case — a long low rake through a dense stand. |
| `03-the-wood-at-dawn.png` | Long faint rakes: does anything still stack? |
| `04-the-shade-under-a-crown.png` | Standing at a trunk foot at noon — are you in shade now? |
| `05-the-rake-cap.png` | **The owner picks:** a 41 m dawn rake or a 55 m one — and how little it turns out to matter. |
| `06-overcast-dims-the-shade.png` | Does cloud fade the pool and the cast shadow together? |

## The numbers

**Stacking is measured directly, not against a baseline.** Alpha-over of one shadow of alpha `a` removes
at most `a` of the light at a pixel; two remove `1−(1−a)²`. So the single-shadow **ceiling is `a`**, and
any pixel above it has been darkened more than once — exact, and needing no median to compare against.
(A first attempt scored ">2× the median darkening" and was wrong in both directions: the ground pool's
feathered rim fills the low end of the distribution and drags the median down.)

| | stacked pixels | worst darkening vs the single-shadow ceiling | shade landing on a crown |
|---|---|---|---|
| **07:00** before | 15.27 % | 0.550 vs **0.090** (six times) | 15.5 % |
| **07:00** after | **0.000 %** | 0.089 vs 0.090 | **0.3 %** |
| **13:00** before | 4.37 % | 0.887 vs **0.394** | 36.0 % |
| **13:00** after | **0.000 %** | 0.391 vs 0.394 | **3.7 %** |
| **16:00** before | 22.09 % | 0.910 vs **0.352** (nearly three times) | 34.4 % |
| **16:00** after | **0.000 %** | 0.349 vs 0.352 | **0.9 %** |

Noise floor (pixels that got *lighter* when shadows were added): **0** in every arm, at every hour.

**The shade under a crown.** Ground inside the crown's footprint at noon: **6.1 % darker**, worst 35 LSB,
56 % of the pool region changed — with the ground just south of the pool measuring **0.00 %** change as
its own control.

**Cost.** The pool is one extra quad per caster on the SAME material and the SAME shared unit sprite, and
the height gate (3 m) keeps it to the **331 trees** rather than all 439 casters — the 148 shrubs and 384
shore plants draw no pool at all. Timed by alternating trials at 900×900: **21.92 ms with pools vs
20.51 ms without**, but the trial spreads overlap (20.87–23.35 against 20.46–24.88), so **1.4 ms is an
upper bound from an editor render-to-texture, not a frame-time claim.** The deterministic fact is the
quad count.

⭐ **The 55 m rake was never the visible problem its number suggested.** At dawn a shadow is drawn at
9 % alpha, so capping a mature pine from 54.8 m to 41 m changes only **11.65 % of the frame by a mean
2.0 LSB** (worst 6.1) — plate 05's third panel is amplified 24× to show it at all. The cap is worth
having because a dead clamp is worth making live, not because the long rake was hurting anything, and
the owner should pick with that in front of him.

**What is not fixed.** 3.7 % of the noon shade still lands on a crown: sorting by the far end drops a
shadow by `shadowDir.y × length × OrdersPerMetre`, and at noon the rake is short so the drop is small and
a very close neighbour can still be crossed. The honest fix is a receiver that knows it is in shade — a
screen-space shade buffer — which is its own PR.
