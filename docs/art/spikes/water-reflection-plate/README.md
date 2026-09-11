# The raised `_ReflectionFadeChop`, priced — the plate series

**Shot 2026-09-11** against the owner's ruling of the same day: *"shoot the plate first."*
Register **row 6** stands — **ask, never choose**. **Nothing here ships and nothing here rules.**
The numbers land in `docs/design/water-fidelity-register.md` §3 item 10 as a **PROPOSAL** column with
the ruling column **empty**.

Fixture: `Assets/Tests/EditMode/WaterFidelityPlateSweepTests.cs` →
`TheRaisedFadeChop_PricedAgainstEveryTermInTheFrame`. Full report: [`DIAGNOSTIC-FADECHOP.txt`](DIAGNOSTIC-FADECHOP.txt).

## Zero production change

Every value swept below is a **`MaterialPropertyBlock` override inside the fixture, restored after
each shot**. The nine water materials, `Water.shader` and the production C# are untouched; the
shipped look is bit-identical at the shipped values. This is possible because **a property-block
write is not clamped by a `Range(0,1)` declaration** — that clamp is an inspector / `Material.SetFloat`
concern. So the plate can walk the dial past 1.0 without the shader edit the charter had permitted.

To stop that becoming a *silent* false negative, the test carries a **clamp discriminator**: if an MPB
clamp did exist at 1.0, `|luma(fade 64) − luma(fade 1.0)|` would collapse into the noise and the test
fails **naming the clamp**, instead of reporting "raising it past 1 buys nothing" — which would be a
measurement of a clamp, not of the sea.

## The frame these lumas were shot through

> bare `Camera` → `ARGBHalf` RenderTexture → `RGBAFloat` readback, **no URP Volume stack**; the only
> post-water op is the ADR 0013 day/night tint **multiply** replayed in C#.

These are the **water's own output, PRE-GRADE** — not the final frame. **Rule on the ratios between
seas, not on the absolutes.** One viewpoint (`ww-open`, WestWater region centre, orthographic 40 m
across at 960 px), one clock (mean tide, noon), so no arm needed re-aiming. Wet fraction 100 % of the
plate; every luma is the mean over the wet set only.

## The plates

| | what it is | mean wet luma |
|---|---|---|
| `plate-01-blow-fade0.60-shipped.png` | the blow **as it ships today** (fade 0.6) | **0.01114** |
| `plate-02-blow-fade1.12-option-a-as-costed.png` | fade **1.12** — the value option (a) was costed at | **0.05508** |
| `plate-03-blow-fade2.00-where-the-target-lands.png` | fade **2.00** — where the register's 0.088 target *actually* lands | **0.08894** |
| `plate-04-blow-fade2.00-at-stormgrey-master-0.15.png` | the same raise with the master pinned to `Water_StormGrey`'s **0.15** | **0.02387** |
| `plate-05-blow-fade64-positive-control.png` | fade **64** — **not a candidate**: the positive control *and* the clamp discriminator | **0.11370** |
| `plate-06-blow-all-54-weights-zeroed-residual.png` | all 54 weight-like terms zeroed at once | **0.01509** |
| `plate-07-glass-fade0.60-shipped.png` | the **negative arm** at its shipped value | **0.34171** |
| `plate-08-glass-fade64-negative-control.png` | the negative arm with the dial slammed to 64 | **0.34168** |

## Controls (all four passed; each is an assertion, not a claim)

- **Noise floor** — the shipped frame re-shot 3×, worst departure **0.000068** (0.61 % of the frame).
  EditMode `_Time` is real time, so this is not zero and **no row inside it is a number**.
- **Positive control** — the ladder moves at all: fade 0.6 → 64 swings the blow by **0.1026**, 1517× the floor.
- **Clamp discriminator** — fade 1.0 → 64 still moves the blow by **0.0677**, so the render path has
  **no upper clamp at 1.0**. The `Range(0,1)` is an inspector clamp, not physics.
- **Master control** — at fade 2.0 the 0.70 and 0.15 masters are **0.0781** apart, so the two masters
  the owner's table names are genuinely different pictures and are priced separately.
- **Negative control (the sabotage)** — the same sweep at a **glass calm** moves the frame by
  **0.00004**, 0.0 % of it. Ratio blow : glass = **2777×**. The knob reaches exactly **one** side of
  the comparison, so the comparator measures the sea and not the instrument.

## What the plates say

1. **Raising the fade past its declared ceiling keeps buying light.** 1.00 → 0.0460, 1.12 → 0.0551,
   2.00 → 0.0889, 64 → 0.1137.
2. **The register's open question on row (a-today) is settled — and the answer is "neither".** That row
   carried two rival blow figures at fade 1.00, **0.075** (old constant) and **0.034** (the blow arm's),
   with *"we do not yet know which is right, and one more plate settles it."* Measured: **0.046**,
   between them.
3. **1.12 does not reach the 0.088 target.** At the master this weather actually blends to (0.623) the
   target needs fade ≈ **1.9**; with the master pinned at 0.70, ≈ **1.6**.
4. **At `Water_StormGrey`'s own 0.15 master the fade cannot get there at any value** — the ladder tops
   out at **0.028** even at fade 64. There the **master** is the binding constraint, not the fade, so
   (a) and (c2) are not alternatives: without (c2), (a) cannot arrive.
5. **At the dial's declared ceiling, (a) and (d) buy the same light.** Fade → 1.00 is **+0.0349**;
   `_SwellReadStrength` → 0 is **+0.0345**. Same to within 1 % — but (a) costs nothing the owner asked
   for, and (d) costs 37 % of the swell band contrast he asked for on 2026-09-09.
6. **The reflection is the 6th largest term in the blow frame**, at 9.76 %, behind
   `_PaletteGradeStrength` 76.4 %, `_EnvelopeBandStrength` 59.6 %, `_StormFoamLaneStrength` 27.4 %,
   `_RippleStrength` 18.2 % and `_PalettePullStrength` 11.0 %. This corroborates row 6's existing ⭐⭐
   correction: the reflection is **not** the owner of the blow's darkness.
7. **The weights are not where the darkness lives.** All 54 zeroed at once leaves **135 %** of the
   shipped frame — the colour anchors and the shape knobs are the floor no weight switches off.

The 54 terms were **enumerated from the shader at runtime**, not hand-picked, because #829's charter
named its suspect by arithmetic and the plate then found `_SwellReadStrength` — never suspected —
carrying most of the blow.

## What these plates do **not** say

- They do not rule, and they do not recommend. Row 6: **ask, never choose.**
- They are **not the final frame** — no MoodGrade Volume in this path. Do not read an absolute off them.
- They do not re-derive anything from #829's fitted slope k = 0.3208. That fit was taken at **glass**
  and is falsified at a blow. Every number here is measured at the cell it is quoted for.
- They say nothing about whether *shipping* option (a) needs the `Range(0,1)` widened. A property-block
  write is not clamped by the declaration; a material-asset write is. That is a **shipping** question
  for whichever option is chosen, not a measurement one.
