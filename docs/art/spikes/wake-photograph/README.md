# The wake is off centre — the photograph

**Owner, 2026-09-11, in play:** *"the wake still is not centered behind the boat, the foam is off
centre"*

This is the **second** report of this complaint. The first (2026-09-06, *"the foam seemed off-centred
with three different sections leaving the boat"*) became register row 29 and shipped as #768, which
unified the three families' **root**, **projection** and **width** into `WakeRootMath`. The complaint
survives that fix, so either the fix did not reach the pixels or the cause was never among the three
things #768 unified.

#768's plate said of itself: *"It is a diagram of the shipped arithmetic driven by the shipped data …
**It is not a photograph of rendered foam.**"* **This sheet is that photograph.** No cause may be
proposed until it exists; what it rules out is as much of the value as what it shows.

## What was shot

`SHEET-wake-photograph.png` — two rows (**the cape**, a skinned mesh; **the dory**, a sprite hull) by
three columns (**heading 000°**, **heading 090°**, and **through a sustained turn 000→088°**). Each
panel is the frame as the game renders it, with the reference drawn on:

- **green** — the transom's *swept track*, sampled every frame of the leg. **This is the reference**
  every offset below is measured against.
- **yellow** — the advected sheet's own root path, as its injector's transform actually went.
- **white** — her origin. **Crosses** — each family's drawn centroid.

`SHEET-wake-photograph-families.png` — the same six shots with each family **differenced against a bare
frame**: every pixel that family adds, above a *measured* noise floor. Red = **B**, the sprite deposits.
Magenta = **C**, the crests. Blue = **A**, the advected sheet. Green = the swept track.

**SHOT FROM.** NineMileCreek, hour 11.0, open water, 6.0 m under her at the shallowest point; legs start
at (260.0, 75.0), (280.0, −65.0), (270.0, 235.0). Plate 3662×1600 px, tint RGBA(0.816, 0.827, 0.838,
1.000), measured noise floor 0.0100 luma.

| shot | camera | ortho half-height | m per px |
|---|---|---|---|
| cape 000° | (260.00, 87.06) | 27.66 m | 0.0346 |
| cape 090° | (292.06, −65.00) | 15.60 m | 0.0195 |
| cape turn | (277.63, 242.78) | 23.38 m | 0.0292 |
| dory 000° | (260.00, 87.06) | 18.86 m | 0.0236 |
| dory 090° | (292.06, −65.00) | 8.24 m | 0.0103 |
| dory turn | (277.63, 242.78) | 14.58 m | 0.0182 |

**The frame it was shot through** is the game's own camera — the persistent rig, same entity id, shipped
render path, exposure and grade — with a render texture attached, held still, pulled back, and
un-snapped. `CameraFollow` and the `PixelPerfectCamera` are both parked for the shutter and restored
after; nothing else about the camera is touched. The plate is **pre-grade**, so rule on *ratios* and
*world-metre offsets*, never on post-grade absolutes.

## The measurement

Lateral offset of each family's drawn centroid from the transom's swept track, **in world metres**,
**+ = to port of the course**. Full text in `MEASURED-cape.txt` / `MEASURED-sprite.txt`.

**The cape** — stern offset 6.400 m at 40.0°, band half-width 2.400 m:

| heading | family | drawn px | offset m | ÷ band half-width |
|---|---|---|---|---|
| 000° | ALL drawn | 15235 | −0.135 | −0.06 |
| 000° | A sheet | **0** | — | — |
| 000° | B deposits | 5071 | **−0.558** | −0.23 |
| 000° | C crests | 10164 | +0.053 | 0.02 |
| 090° | ALL drawn | 50458 | +0.493 | 0.21 |
| 090° | A sheet | **0** | — | — |
| 090° | B deposits | 18521 | **+0.704** | 0.29 |
| 090° | C crests | 31937 | +0.393 | 0.16 |
| turn | ALL drawn | 21400 | −0.168 | −0.07 |
| turn | A sheet | **0** | — | — |
| turn | B deposits | 8032 | −0.329 | −0.14 |
| turn | C crests | 13368 | −0.082 | −0.03 |

**The dory** — stern offset 2.250 m at 40.0°, band half-width 0.850 m:

| heading | family | drawn px | offset m | ÷ band half-width |
|---|---|---|---|---|
| 000° | ALL drawn | 17802 | −0.163 | −0.19 |
| 000° | A sheet | **0** | — | — |
| 000° | B deposits | 5162 | −0.310 | −0.36 |
| 000° | C crests | 12640 | −0.115 | −0.13 |
| 090° | ALL drawn | 89643 | +0.283 | 0.33 |
| 090° | A sheet | **0** | — | — |
| 090° | B deposits | 24919 | **+0.614** | **0.72** |
| 090° | C crests | 65172 | +0.153 | 0.18 |
| turn | ALL drawn | 30022 | −0.226 | −0.27 |
| turn | A sheet | **0** | — | — |
| turn | B deposits | 9368 | −0.242 | −0.29 |
| turn | C crests | 20767 | −0.219 | −0.26 |

## What the photograph shows

**1 — The deposits (B) are off the track, and the sign flips with heading.** Up to **0.704 m** on the
cape and **0.72 of the band's own half-width** on the dory. C, the crests, brackets the track almost
symmetrically at every heading. This is the owner's sentence, measured: *the drawn band is displaced
laterally from the course, and B is what displaces it.*

**2 — On the straight legs the displacement resolves to ONE world direction.** Reading the two straight
legs together, with the course rotated out:

| | course | B lies | which is world |
|---|---|---|---|
| heading 000° | +y | 0.558 m to **starboard** | **+x** |
| heading 090° | +x | 0.704 m to **port** | **+y** |

So for the cape the displacement is ≈ **(+0.56, +0.70) m — about 0.90 m toward world north-east**; for
the dory ≈ (+0.31, +0.61) m, about 0.69 m. *A fixed world direction is exactly what "sometimes left,
sometimes right" looks like from the helm as the boat turns under it.*

**3 — And it grows with distance astern.** Visible in `SHEET-wake-photograph-families.png`: at the
transom the deposits straddle the green track; twenty metres astern they are clear of it, on the same
side, at both straight headings. Older foam is further off the track than new foam.

**4 — Through the turn, the sheet's root path leaves the swept track.** The yellow and green curves run
together on the straight legs and separate markedly in both turn panels. This does **not** reach the
player today (see below) but it is a real disagreement between two things #768 was meant to have tied
together, and it is recorded here so it is not re-discovered.

## ⚠️ The "A sheet = 0" row is NOT evidence the sheet is missing — the isolation is inert

**The A row of every table above is uninformative, and the fixture is what makes it so.** `armA − bare`
is **bit-identical** — peak 0.0000 over 1.3–4.5 million pixels — which is not "very faint", it is
"nothing changed at all".

The reason is in the shader, not the sea. The advected sheet is **composited inside the water shader**
(`HiddenHarboursWater.shader`, `WakeFoamCoverage`, gated on the material's `_WakeFoamStrength`), not by
a render feature the fixture can switch. Isolating A toggles the **`FoamInjector`** and zeroes the
registry's look strength — which stops *new injection*, but neither un-draws what the buffer already
holds nor dims the shader's compose. The sheet is therefore present and identical in **every arm,
including `bare`**, so it cancels exactly in every difference.

Two consequences, and both matter:

- **B and C are measured correctly.** The sheet is a constant in both differences, so it subtracts out.
  The control confirms it: `ALL drawn` = `B` + `C` **to the exact pixel** on all six shots
  (15235 = 5071 + 10164; 50458 = 18521 + 31937; 89643 = 24919 + 65172; …).
- **Whether the sheet reaches the picture at all is still an open question**, and this fixture cannot
  answer it. It is the first candidate below.

## ⚠️ What this plate is, and is not

- **It is a photograph of rendered foam**, through the game's own camera at the shipped render path and
  grade — which is what #768's plate explicitly was not. The numbers are read off the pixels, not
  computed from the arithmetic.
- **It is not a cause.** It measures *where the foam is drawn*; it does not name the seam that puts it
  there. Per the lane charter, a structural finding stops at the measurement and the owner rules.
- **It does not touch the look.** Nothing in this PR changes a rendered pixel of the game. The fixture
  is a measurement instrument and the sheets are its output.
- **The turn's centroid is not the straight legs'** and must not be compared to them as if it were
  (#741: 2.11 m straight vs 1.19 m turning). Each heading is its own row.

## ⚠️ One mechanism that looks right and is NOT, recorded so nobody re-spends it

The signature in finding 3 — *foam migrating toward a fixed world direction, further the older it is* —
is precisely what the foam buffer's wind/current drift does (`FoamBuffer.AdvectCells`, banked along the
shared `FoamDriftDir()`; `FoamBuffer.DrawOrigin`). **It cannot be the mechanism here.** That drift
applies to the buffer, which is family **A**; family **B** is `BoatWakeEmitter`'s sprite deposits, and
`BoatWakeEmitter.cs` carries **no drift term at all** — its deposits are never advected after they are
laid. The resemblance is real and the explanation is not.

## For the owner — the candidates, in the order they should be settled

Row 6 discipline: none of these is a look change chosen by this lane.

| # | candidate | what it would settle | cost |
|---|---|---|---|
| 1 | **Confirm family A reaches the picture at all**, on an unmodified camera with no render texture attached. Per-camera foam state keys on `(camera entity id, resolution)`, and *both* configurations that reported A as nothing had a render texture attached — a shared confound never ruled out. | Whether the broad smooth sheet draws for the player today. If it does not, that changes what the wake looks like far more than any offset. | small; one plate, no code |
| 2 | **Find what displaces B toward a fixed world direction with age**, given the emitter does not advect. Not yet examined: the deposit sprite's pivot and iso projection, its sorting or height offset, and anything that offsets the **compose** — #768 unified injection only. | The owner's actual complaint. | medium; a spike |
| 3 | **Decide whether B should displace at all.** If some world-direction drift is wanted (foam does leeway in a real sea), the defect is that the *boat* does not share it, and the fix is a look ruling rather than a bug fix. | Whether this is a bug or a tuning. | a ruling |
| 4 | **The turn-only divergence of the sheet's root path from the swept track** (finding 4). Invisible today because A draws nothing; becomes visible the moment candidate 1 is settled. | Whether #768's unification holds through a turn. | small |

## Re-running it

The fixture is `Assets/Tests/PlayMode/WakeCentrePhotographPlayTests.cs`. It needs a **GPU** and
self-skips on CI with a recorded reason. In the open editor:

```
mode=playmode   filter=WakeCentrePhotograph   filter_type=testName
```

Plates and the `MEASURED-*.txt` land in `Application.temporaryCachePath/wake-photograph/`. The sheets
here are built from them; the 36 raw plates are 224 MB and are deliberately **not** committed.

⚠️ The filter parameters are named above because getting them wrong does not fail — it silently runs the
**entire** PlayMode suite, which cannot complete on this box. Two fifteen-minute stalls were spent on
exactly that before the parameter names were checked.
