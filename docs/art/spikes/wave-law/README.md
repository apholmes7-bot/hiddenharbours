# The peak wavelength law, before and after — register row 30

**Owner, 2026-09-06:** *"waves seem to move across the screen too fast, should be realistic to actual
waves and windspeed/conditions"* → *"make it realistic."*

`SHEET-row30-wave-law.png` — three rows (light, blow, gale), two columns. **Left = the legacy linear
line** `λ = 6 + 1.5·U`; **right = the derived fetch-limited peak** at the shipped 25 km. Shot at the
**cape's own framing** (`CameraWorldHeightMeters` 24 m), noon, mean tide, through the shipped
`WaterFidelityPlateSweepTests` machinery — the two arms differ in `SeaFetchKilometres` and in nothing
else.

## What the plate shows

| | legacy λ | derived λ | ratio |
|---|---|---|---|
| **light** (1.63 m/s) | 8.4 m | **2.2 m** | **0.26×** |
| blow (5.70 m/s) | 14.6 m | 15.6 m | 1.07× |
| gale (12.95 m/s) | 25.4 m | 27.3 m | 1.07× |

**Row 1 is the change.** The legacy sea draws long smooth swell bands in a 1.6 m/s breeze; the derived
sea draws a fine busy ripple. That is what a light air actually raises — at that wind the sea is fully
developed after 5 km, so it never gets to build swell. The numbers agree with the eye: mean luma barely
moves (0.1670 → 0.1600) while the standard deviation rises **0.0555 → 0.0747**, which is the finer,
higher-contrast chop.

**Rows 2 and 3 are a 7 % nudge and look it.** That is the finding of #762, not a failure of the plate:
`6 + 1.5·U` was already a good straight-line fit to a fetch-limited sea at ~25 km, so blow and gale were
close to right all along.

## ⚠️ What this plate cannot tell you

- **Rows 2 and 3 are weak evidence either way.** At blow and above the sea draws near-black — that is
  **register row 25**, which the owner has already ruled is intended. A 7 % wavelength change is not
  legible on water that dark, so read those rows as "consistent with the measurement", not as proof.
- **It is a still.** "Too fast" is a rate, and no single frame carries it. The crossing and crest-interval
  numbers are in `water-rendering.md` §37 and §39; the plate is about what the sea *looks* like, not how
  fast it moves.
- **No hull, no ride.** This is a SIM change — the boat rides this field — and a helm-feel verdict is
  owed in Play. The precedent is the owner's own *"the cape has weight"* on #739.

## Re-running it

```
"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" -runTests -batchmode -projectPath <worktree> -testPlatform EditMode -testFilter HiddenHarbours.Tests.EditMode.WaterFidelityPlateSweepTests.Row30_TheSeaBeforeAndAfterTheRealisticLaw_AtTheCapesFraming -testResults <abs>/row30.xml -logFile <abs>/row30.log
```

One method, not the sweep. It self-skips on the Null device and says NOT VERIFIED rather than passing
quietly.
