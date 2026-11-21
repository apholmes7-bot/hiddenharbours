# Waves follow the seabed: diagnosis

Gap-table rank 1 (H5): swell and surf should bend toward shore with the depth, so every beach meets its
waves roughly face-on. Today every wave train is a straight plane wave across the whole map. A beach that
faces away from the wind sees crests sliding past it sideways, or no crests at all.

## 1. What the old branch did

`feat/water-toward-hdrp-2-waves-follow-the-seabed` was never pushed. It sits 18 files on top of merge base
`753fa4d6`. In outline:

- **An offline ray-trace bake per region** (`.hhbathymetry`). For 16 travel directions and several tide
  levels it traced wave rays over the seabed and stored, per 4 m cell, the turned direction, a phase
  correction and a height factor (16 bytes per cell per slice, packed into a `Texture2DArray`).
  The bake *descriptors* are already on main (`Art/Terrain/Bathymetry/*.hhbathymetry`, about 800 bytes
  each, plus the importer). Nothing on main reads them at runtime.
- **A full refraction in the shader.** `WaveFieldSample` ran `BathSample` for **all 8 trains** on every call.
  Each call blended 2 direction bins x 2 tide slices, each a 4-corner manual bilinear on two textures
  (pixels and support), and then ran a Newton `tanh` dispersion solve for the local wavenumber.
- **The same field in the simulation.** A new Core interface `IWaveRefraction` was read by
  `BoatController`, `BoatWaveMotion`, `BreakerMath`, `WaveFetch`, `SwellMath`, `GameServices.FetchAt` and
  `VehicleGrounding`, so boats rode the turned waves.
- **Scene wiring at play time.** `WaveBathymetrySceneWiring` (editor `InitializeOnLoad` plus
  `IProcessSceneWithReport`) found `<scene.name>.hhbathymetry` and added a `WaveBathymetryRegion` object.
  Every `LateUpdate`, that object sampled `GameServices.TidalTerrain` into a grid the first time, then
  pushed 10 or more globals.

## 2. Why each failure happened

| Failure | Cause |
|---|---|
| **Draw cost about 25.7 ms vs 5.7 ms** (768 px plate) | Structural. `WaveFieldSample` runs about 13 times per pixel: the main sample, plus 4 taps each for foam convergence, caustics and glitter. The vertex stage adds more. Each run did 8 trains x 4 slices x 8 manual texel loads, plus a Newton solve: on the order of 3,000 dependent loads per pixel. The branch's last idea ("prime the lookup once per pixel, reuse it in the taps") was never measured on a GPU. Even perfect priming leaves 8 trains x 32 loads, so it still costs several times the 1 ms budget. |
| **A GPU test failed** | Shader compile. With 8 `BathSample` calls inlined into every `WaveFieldSample` call site, FXC ran out of time or registers ("compile death" in the branch's own commit note). The `[loop]` workaround got it compiling on the desk, but nothing ever ran it through the GPU-backed test classes. CI has no GPU, so CI could not catch it. |
| **St Peters' seabed did not load** | Wiring, not data. `WaveBathymetryRegion.LateUpdate` returns early unless `SceneManager.GetActiveScene() == gameObject.scene`. Regions load **additively**, and the active scene is not the region scene in every flow (for example the boot/shell flow and the plate stage), so the field was never published. On top of that, the grid was built from `GameServices.TidalTerrain` on the first frame that service existed. St Peters' seabed is the painted, hand-tuned height texture, not the generated terrain the bake was traced against. Not reproduced on a GPU this time; this is read from the code. |
| (not tested, but disqualifying) | It broke two standing limits. Boats, breakers and grounding read the turned field, so boat feel changed and the turn became sim state, not visual. And a component did per-frame CPU work pushing globals. |

## 3. Can that approach fit the budget?

No, not as a per-pixel refraction of all 8 trains inside every `WaveFieldSample` tap. The cost scales as
taps x trains x slices x loads. Removing any factor (say only the dominant train, one tide slice, no Newton
solve) gives a different, simpler technique. The second-order wins it adds, such as wave amplitude focusing
over shoals and correct wavelength shortening, are not what the owner sees from the camera. What the owner
sees is **crests lining up with the beach and rolling in onto it.**

## 4. What was built instead (cheapest approach with the visible result)

**A shore-wave field built once per sea, and a per-train cross-fade in the shader.**

- `ShoreWaveField.Build` (POCO, `Code/Art`) runs once per region, at seabed feed time, in `WaterSurface`.
  It takes the seabed already used for the depth tint (the painted St Peters texture, or the baked
  terrain grid elsewhere) and computes an exact signed Euclidean distance to the shoreline at a reference
  level (Felzenszwalb-Huttenlocher, separable, O(n)). Water is positive, land negative. It smooths the
  distance and takes the gradient. The result is one 256x256 `RGBAHalf` texture: R = metres offshore,
  GB = offshore gradient. It is bound through the renderer's property block, the same way as the height
  texture (the displaced chunks copy it).
- In `WaveFieldSample`, each train gets a second copy whose phase is `phi - k * metresOffshore`. Its crests
  run along the shoreline contours and travel shoreward. The copy fades in within `_ShoreWaveReach` metres
  of shore, up to `_ShoreWaveBend`. Offshore of the band the shader does one texture read and nothing else
  (a `[branch]` skips the second trig set). Inside the band it pays one more `sincos` + `pow` per train.
- **Visual only.** The C# wave math, `BreakerMath`, boats and grounding are untouched. Wave height, tide and
  everything a boat feels stay a function of (seed, game time). Nothing is saved. The field is recomputed
  from the seabed on every load, and no per-frame CPU work is added.
- **Off is exact.** With `_ShoreWaveBend = 0`, or no field bound (no seabed source), every train is drawn
  with `amplitude * 1.0`, identical to main.

Tunables (material, shader defaults so no `.mat` YAML changes): `_ShoreWaveBend` (0.75), `_ShoreWaveReach`
(40 m). On `WaterSurface`: field resolution 256, reference level 0 m, distance clamp 192 m, 3 smoothing
passes.

### Known limits (logged, not fixed here)

- The surf bore phase (`BorePhase` in the water shader, its verbatim twin in `FoamBufferAdvect.shader`,
  and the C# `BreakerMath.BorePhaseDegrees`, all pinned by tests) still follows the straight dominant
  train. The bore line already travels along `ShoreDir`, so it reads as shoreward. Its timing lines up with
  the straight swell, not the turned one. Matching it means editing three pinned twins at once, which is
  a separate small PR.
- No shoaling amplitude change or wavelength shortening. The turned copy keeps each train's own k and
  amplitude.
- The field uses one reference level (chart datum 0 m), not the live tide. Near the shoreline the turn
  therefore lags by at most the tide's horizontal excursion, a few metres on a gentle beach.

## 5. Measured

On this machine, in a PlayMode plate render at 1280x720 + 4x4 readback, same camera and game time,
3 reps x 60 frames. St Peters main beach, branch, interleaved in one run: default 11.87 / 11.44 /
11.62 ms against bend 0 at 11.83 / 11.46 / 11.47 ms. Main in its own run: 12.13 / 11.26 / 11.37 ms.
Nine Mile Creek, branch: default 7.12 / 7.89 / 7.00 ms against bend 0 at 7.30 / 7.15 / 7.64 ms. No cost
resolves above the ±0.7 ms rep noise, inside the +1 ms budget.

The field is built in both regions (256x256 RGBAHalf). At the St Peters main beach it turns the
waves to (-0.05, 1.00), straight up the beach, from the straight train's (-0.63, 0.77). At the east
shore it turns them to (-0.93, -0.38), into a shore the straight swell runs away from.

## 6. What the eye reads, and what is left

From overhead at noon, the brightest "wave lines" on these beaches are the **wind whitecap layer**.
That layer is wind-streaked evolving noise, drifted with the foam and gated by roughness. It is not
the wave trains. A plate with the trains switched off for one render shows the same streaks. A plate
with whitecap roughness 0 removes them. The surf bores already run shore-normal on main
(`_BreakerRefract.x = 1`). So this change turns the swell (its light, glints, caustics and crest
factor), and the visible difference at these cameras is modest.

Remaining, in order of visible payoff:
1. Inside the shore band, key the whitecap streak axis and crest gate on the turned crest direction
   instead of the wind, behind its own dial. This is the change that would make the white lines face
   the beach.
2. Turn the bore clock's phase with the same field (the three pinned twins: the water shader,
   `FoamBufferAdvect.shader`, `BreakerMath.BorePhaseDegrees`) so the bore timing matches the turned
   swell.
3. Optionally, a live-tide reference level instead of chart datum.
