# Water fidelity register — the judged list, for the owner to RANK (2026-09-01)

> **Status: DRAFT, awaiting the owner's ranking.** PR 0 of the water-fidelity charter
> (`HANDOFF-2026-09-01-water-fidelity.md`): *"improve water fidelity. I want to continue work on crashing
> washes using the new water physics and improve the overall look and feel entirely."* This is the ADR 0027
> judge pass that has been owed since August, done as an **instrument** rather than an opinion: every row
> below names a plate you can look at, the mechanism that draws it (verified in source, file and line), the
> lane's read of its severity, and what fixes it. **You rank it; PRs 4+ of the charter follow your order.**
> PRs 1–3 (the crashing washes) are already sequenced and are rows 1–4 here.
>
> The instrument is `Assets/Tests/EditMode/WaterFidelityPlateSweepTests.cs`; its output lives under
> `artifacts/water-plates/` (gitignored, regenerated in ~50 s on a GPU). Re-run it before and after every
> later PR of the charter: the before/after pair of sheets **is** the evidence a look PR ships with.

---

## 1. The instrument — what a plate is, and what it is not

**Four world-locked viewpoints**, each photographed across **{glass, light, blow, gale} × {spring low,
mean, spring high} × {noon, golden hour, night with the searchlight}** — 36 plates per viewpoint, a contact
sheet per viewpoint (`SHEET-<viewpoint>.png`, weather across, tide within each weather, hour down), and a
`MANIFEST.txt` beside the plates recording every uniform each plate was pinned to, **read back** from the
globals and the property block after the push, never assumed.

| viewpoint | where the camera looks | why |
|---|---|---|
| `nmc-steep` | Nine Mile Creek, the steepest point of the break contour — ξ = 2.19, **plunging**, weight 1.0 — at (76, −14) | the one stretch that earns a lip and a barrel |
| `nmc-sand` | Nine Mile Creek, the point on the break contour from which the surf runs furthest shoreward (23 m), biased so the beach is in frame — (120.7, −242.3) | the widest spilling beach |
| `stp-arrival` | St Peters, the reef door and apron on the arrival line — (206.5, 0) | the coast every new game sees first; the apron dries at spring low |
| `ww-open` | West Water, the region centre — 6 m of water in every direction, no seabed in frame | the open-water **control**: whatever it shows is the sea, not the shore |

**Every plate is shot through the shipped components.** A fake environment holds one weather and one tide
still; the shipped `WaterSurface` then pushes `_Chop`, `_Roughness`, `_Flow`, `_WindDir`, `_WaterLevel`,
`_RainIntensity`, the weather-mood blend across the preset anchors and the palette seam, exactly as in Play;
the shipped bridge calls publish the wave field, the fetch and the breaker contour from the same
`GameConfig.asset`; the shipped day/night maths publishes the hour; the searchlight is published through
the shipped light bridge. Nothing is written to `Water.mat`.

**The pinned world** (the manifest carries the numbers per plate):
- weather = the sim's own pairing: each sea state's wind is `WeatherModel.WindStrengthFor(seaState)`, so a plate never pairs a gale's foam with a zephyr's wind. glass 0.00 / 0 m·s⁻¹ · light 0.25 / 1.63 · **blow 0.55 / 5.70** · gale 0.95 / 12.95. ⚠ #680 shot its "working day" at 8 m/s and 0.55 together and measured a 0.92 m break depth; the sim's pairing at 0.55 is 5.7 m/s and breaks in **0.60 m**. Both are legitimate; the plates use the sim's.
- tide = the region's own spring low / mean / spring high (−2.20 / 0 / +2.20 m).
- hour = noon 12:00; **golden hour 17:00** — found on the shipped profile as the warmest afternoon tint that is still bright, tint (0.866, 0.529, 0.356); **night 02:00**, tint (0.016, 0.020, 0.040), a **new moon below the horizon** published explicitly (`MoonCycle` is Play-only, and the shader's fallback for an UNSET moon is a full moon at the camera anchor — the first draft photographed one).
- the searchlight = `BoatSpotlight`'s serialized defaults, which are what the presentation service mints for the cape: 2.5 m up, warm (1, 0.88, 0.62), intensity 1.5 × water strength 0.8, **9 m range, 26° half-cone**, night gate 0.12/0.35; placed 6 m left of the aim, throwing +x.
- tidal current 0; visibility 1; the object-reflection target bound clear (no reflector exists in a fixture).
- 40 m across at 960 px = **24 px/m**, one plate pixel per cell of the shader's own world pixelize grid.

**What the plates are NOT.** They are the flat `Universal2D` pass: `DisplacedWaterSurface` registers and
ticks only in Play, so the **vertex lift of the displaced sea is absent**, and so is every hull (no
waterline, no wake, no spray, no interior mask). The fragment — every colour, foam, surf, caustic,
reflection and light term — is the same program the displaced pass runs on lifted geometry. A still also
cannot show a beat, a snap or a drift. §3 lists what is owed on those.

**The laws the instrument is built under**, each of which has cost a PR before: a per-pixel diff of two shots
of the same sea measures `_Time`, so every assertion is on the MECHANISM (wet fraction from the terrain,
the published globals, the file's orientation) and judgement is left to the eye; a property block is sticky,
so every scrubbed property is written on every shot; a cold shader cache fakes a regression, so the sweep
warms until nothing is compiling; the night is captured in HDR and the day/night multiply applied on
readback, or it photographs as cream; and **the published file is asserted, never the buffer** — the sheet is
re-loaded from disk and its sea must lie on the side the bathymetry says.

**The knob-by-knob diagnostic** (`TheGlassStripesAndTheGaleShards_MeasuredKnobByKnob`, output in
`artifacts/water-plates/diagnostic/DIAGNOSTIC.txt`) shoots the open-water control at glass and at gale, once
as shipped and once per layer with that ONE property zeroed after the shipped push, and reports the mean
luma, the horizontal-band contrast (std of the per-row mean), the vertical-streak contrast, and the share of
near-black pixels beside a lit neighbour. Whatever a plate shows, the row whose zeroing removes it is the
layer that draws it. Rows 5, 6 and 7 below were named this way, not guessed.

---

## 2. The register — ranked by the lane, to be re-ranked by the owner

Severity is the lane's read of how much of the owner's stated bar each row costs, at the sea the player
sails in (the blow). **The owner's ranking replaces this column.**

| # | in the owner's words | what the plates show | the mechanism, verified in source | severity | ruling owed? | fix |
|---|---|---|---|---|---|---|
| **1** | *"crashing washes"* — **the surf never crashes; it boils in place.** | `nmc-sand-blow-low-noon`: one flat white band hugging the break contour, uniform along its whole length, soft on both sides. No crest arrives, nothing runs up, nothing drains between crests — there is no "one". `nmc-steep-blow-low-noon`: the plunging stretch (ξ 2.19) draws a thin bright line; the lip, barrel and pocket bands sit inside 1–2 m of the break line and read as one line. | `HiddenHarboursWater.shader` ~4385–4470: `surfBreaking` is a depth gate; `ageM` a 16-tap geometric march; `surfAlive = exp(−age/√(g·d)/τ)`; the sheet an `EvolvingField` boil drifting at `_Flow·t·0.5`; the lip a band centred `throwM` past the break line. **No `_WavePhases`/train phase enters the surf block.** | the charter's core | no — already sequenced | **PR 1** #696 (the bore: travel-time march, `Bore01` from the published phase, run-up) + **PR 2** (this PR — the look: `_SurfBeatStrength` beats the sheet from the front and ages it behind, the lip/barrel/pocket travel with the front; `_SurfRunUpStrength` carries the wet edge up the beach on a travel-time whitewater; all dials ship at 0 = today; ADR 0040 rev 3 PR 2 section) |
| **2** | *"churns through different shades of blue, distorts and fades into the ambient ocean"* — **two foam languages.** | `nmc-sand-blow-low-noon`: the surf is flat white and stays white to its outer edge; the wake's blue walk cannot reach it. | Compose at ~4568–4586: `col.rgb = lerp(col.rgb, _SurfColor.rgb, SurfBandValue(cover)·strength)` — a posterized flat white. The surf never enters the foam buffer (`FoamInjectionRegistry`: only hulls inject, through `FoamInjector`), so `WakeFoamAgeing`'s walk through the palette's blues never touches it; the shore fringe is a third churn (`_FoamColor`). | high | no | **PR 2** (this PR): the advect pass gains `4. THE BORE'S DEPOSIT` behind `_SurfDepositStrength` (0 = today; draws only through `_WakeFoamStrength`), the surf physics copied verbatim into it between `TWIN` markers and pinned byte-for-byte; freshness = a GATE. The owner's dial to turn: the deposit is OFF until he does |
| **3** | (tuning) — **the surf dials are on no material.** | — (the plates draw the shader's `Properties` defaults for all 17 `_Surf*` keys) | `grep -c '_Surf[A-Z]'` = **0** on `Water.mat` and on all eight presets. The keys are not in `MoodFloatNames`, so once serialized they are live — today the owner cannot reach the surf at all. ⚠ `Apply water preset` is a wholesale copy: a preset missing a key stamps 0. | medium (blocks tuning) | no | **PR 2** (this PR): all 17 + the 4 new (`_SurfBeatStrength`, `_SurfRunUpStrength`, `_SurfFrontSlope`, `_SurfDepositStrength`) + the 3 colours serialized on `Water.mat` and the eight presets at today's values; `EveryWaterMaterial_CarriesEverySurfDial_Serialized` keeps it so |
| **4** | *"waves crashing in and out"* — **the swash runs on its own clock.** | The wet edge in `nmc-sand-blow-low-noon` / `stp-arrival-blow-low-noon`. A still cannot show the beat; it shows the edge (and its comb, row 9). | `BeachSwash` (~2639): `base = t·_SwashSpeed·2π + depth·_SwashWavelength`; shipped `_SwashSpeed 0.16` = one run-up every 6.25 s, `_SwashAmplitude 1` (contour metres, slope-true), `_SwashEdgeShift 0.6`, cap `_SwashMaxEdgeShift 0.35`, `_SwashCalmGate 0.7`. Reads `_Time` and `depth` only — neither the trains' phase nor the surf's `ageM`. A bore and the swash do not know each other. | high once row 1 lands | no | **PR 2** (this PR): with `_SurfRunUpStrength` up the drawn edge is `lerp(swash, runUp, breaking)` — it rides the bore where one is alive and drains between crests, and keeps its cosmetic beat where nothing breaks; measured +2342 px of drawn water up the sand shoal's beach, 0 removed |
| **5** | *"glass calm is sacred"* — **the mirror is a striped rug.** | `ww-open-glass-mean-noon`, `nmc-sand-glass-mean-noon`, every glass and light column: hard horizontal wavy bands every ~1.6 m across the whole sea, vertical glint streaks through them; at 17:00 the same stripes in orange. | **Named by the diagnostic:** `_ReflectionStrength = 0` takes the row-band contrast from **0.165 to 0.001** and the sea's mean luma from **0.46 to 0.05** — the stripes are the sky reflection and nothing else, and without it the calm sea is nearly black. `SkyReflection` (~2995–3010): `smearLen = _ReflectionSmear(1.6)·lerp(4,1,sharp)`; `band = 0.5+0.5·sin(2π·(pp.y+(surf−0.5)·smearLen)/smearLen)`; `band = pow(band, lerp(0.4, 3, sharp))` — at calm `sharp = 1`, so the "mirror" is a **1.6 m sine stripe, cubed**, in the sky colour (`_DayNightTint` at `_ReflectionSkyTint 0.85`, strength 0.92 from the calm anchor). The sun streak and the clouds each add ~0.1 luma but no band. | high — this is what the sacred state looks like | **yes** — the mirror's form is a look call | theme: **swell legibility / the mirror's form** (Tier A; `col.rgb` only) |
| **6** | *"waves must be physical"* — **in a blow the water body is black, and everything you see is foam.** | `ww-open-blow-mean-noon`, `nmc-sand-blow-mean-noon`: a camouflage of soft grey blobs and diagonal lanes on black; no crest, no trough, no face is legible as a wave. Mean luma of wet pixels at noon **0.06–0.07** in a blow against **0.45–0.67** at glass. | **Diagnostic (gale):** `_Roughness = 0` → luma **0.002** (the sea without its foam is black); `_FoamConvergenceStrength(0.4) = 0` → **0.016** (the convergence lanes are three quarters of the light); `_StormFoamLaneStrength(0.75) = 0` → 0.049; `_SwellReadStrength(0.35) = 0` → 0.098 (the read **darkens** the sea). The body: `_PaletteDeep` (0.07, 0.10, 0.13) under the storm anchor, `_DeepBlueStrength`, and `ReflectionStrength()` gone by `_ReflectionFadeChop 0.6`, so nothing lights it; `_SwellFaceShade 0.22` is not legible at 24 px/m. | high (P1: the owner's "rigid pattern" verdict, "hard to tell" the big wave is coming) | **yes** — and the `_OceanSwellScale 0.07` ride≠drawn question sits here: **ask, never choose** | theme: **swell legibility and crest form under the relief light** (Tier A for shading; Tier B if anything hulls ride moves) |
| **7** | *"whitecaps… foggy white soup"* — **in a gale the whitecaps are dark shards.** | `ww-open-gale-mean-noon`, `nmc-steep-blow-low-noon`: hard-edged angular **black** shapes, a repeated mirrored silhouette, cut into the foam lanes. | **Named by the diagnostic:** `_WhitecapTexStrength(0.865) = 0` and the shards vanish (the lanes become speckle); `_ObjectReflectStrength = 0` changes nothing (they are not the reflection target). The whitecap block: `capField = lerp(capField, capPat, _WhitecapTexStrength)` — the painted `_WhitecapTex` stamp sheet (16 mirrored copies of the owner's one mark, `_WhitecapTexScale 0.0625`) REPLACES the cap field; the mark's silhouette is hard-edged, and where the sheet is empty the field falls to 0 — a hole in the lane. Same slot measured 2026-08-05 at **1.35 % coverage in a blow where the procedural field gives 9.95 %**. Painted slots PLACE, not decorate. | high in a gale, medium in a blow | **yes** — the mark and the slot's strength are the owner's art | theme: **foam-language unification** (Tier A) — or the owner repaints the sheet |
| **8** | *"as long as the end look gets pixelated"* — **the shallows wear a patchwork of 4 m rounded squares.** | `nmc-sand-blow-low-noon` and `-golden`, right of the surf band: rounded-square patches ~4 m across with wavy lines inside and hard borders, offset by half cells; present in every low-water plate over sand. | The caustic slot: `_CausticTex` (`_CausticTexStrength 0.78`, `_CausticAmount 0.45`) through `UntileSampleW` (~4349–4351) at `_PaintScale 0.25` = **4 m cells** with `_UntileStrength 0.644`. The untiler translates the tile per hashed cell; #443's four-corner blend removed the seam LINES, but a directional tile translated per cell still reads as a patchwork of cells. | medium-high (very visible over sand at low water) | look | theme: **the shore and shallows** (Tier A) |
| **9** | (the shoreline) — **the wet edge is a comb.** | `nmc-sand-blow-low-noon` (left), `stp-arrival-blow-low-noon`: the dry/wet boundary and the drop-off wear 1–3 m "teeth" — hair-like streaks aligned with one axis. | Candidates, to be settled by the knob method before anyone builds: the organic fringe `_ShoreNoise 0.75` (contour metres, slope-true through `ShoreCosmeticSlope`, `_ShoreSlopeFloor 0.15`) offsetting `depthC` on an 8-bit height texture (3.91 cm per code — a whole texel run crosses the contour at once, the 2026-08-01 judge-pass mechanism), and `_FoamEdgeDither 0.02`. | medium | look | theme: **the shore and shallows** |
| **10** | — **the deep/shallow boundary is a wall.** | `nmc-sand-blow-low-noon`: pale shallows meet black deep water at a hard, hairy edge ~1 m wide. | The seabed absorption (`_Turbidity 0.25`, posterized to `_AbsorptionBands 6`) composited under the depth ramp: six transmission steps over the drop-off, the last of them to black. | medium | look | theme: **the shore and shallows** |
| **11** | *"the light needs to affect the environment"* — **golden hour tints the frame; the sea has no sun side.** | `nmc-sand-blow-low-golden`, `ww-open-glass-mean-golden`: everything multiplied orange — surf band, mirror stripes, beach alike; no warm/cool split, no face turned to the low sun. | ADR 0013's whole-frame multiply (tint (0.866, 0.529, 0.356) at 17:00) over a fragment whose only sun terms are `_SwellFaceShade 0.22` and the glitter (`_SunGlitterStrength 0.6`, `_ReflectionSunStreak 1`); none is legible against the stripes of row 5. | medium | look | theme: **light on water** (with the relief dials from #691) |
| **12** | *"navigable later by lights"* — **at night the surf vanishes and the searchlight lights nothing you can name.** | `nmc-sand-blow-low-night`, `ww-open-blow-mean-night`: black; the surf that was white at noon is invisible; the shipped lamp is a faint wedge. | The profile's 02:00 tint is (0.016, 0.020, 0.040). The water's light term **multiplies the sea's own colour** by the cone (`_BoatLightBrighten 2.5`), and a black sea × 2.5 is black; `DN_COMP_MIN_CHANNEL 0.02` floors the compensation. ⚠ **#691's eyeball plates were shot at tint (0.075, 0.092, 0.150) with a 2.6 / 30 m lamp** — a 4× brighter night, 1.7× the intensity, 3.3× the range of the lamp the cape actually carries (`BoatSpotlight` defaults 1.5 × 0.8, 9 m). | medium-high for P5 nights | **yes** — the night level, and the shipped lamp against the plates he nodded at | theme: **light on water**; the boat-lights PR 2 gate |
| **13** | — **the mirror does not fade with the light; it fades with the wind.** | `ww-open-light-mean-noon`: at sea state 0.25 the stripes of row 5 still dominate at three quarters of their glass contrast. | `ReflectionStrength()`: `1 − smoothstep(0, _ReflectionFadeChop 0.6, _Chop)` × wind dim — at chop 0.25 the mirror is still about 0.6 strong (0.62 × a 0.93 wind dim). | low-medium | look | with row 5 |
| **14** | — **a bright patch at the channel mouth.** | `stp-arrival-blow-low-noon`, far right: a white blob where the dredged approach meets the reef at low water. | Observed, not yet named — the fringe and the surf both thicken where the depth contours crowd against the flat-bottomed cut (ADR 0040 ruling 2 says a steep wall should THIN the surf, so this wants the knob method before it is called a defect). | low | — | theme: **the shore and shallows** |

### Rows carried from the charter that the plates cannot show

| # | item | why the plates cannot show it | who owes what |
|---|---|---|---|
| 15 | **ride ≠ drawn at `_OceanSwellScale 0.07`** — the vertex stage draws the swell ×2.8 shorter than the field the rock was tuned on | the flat pass has no vertex lift | **owner CALL — ask, never choose** (charter §1) |
| 16 | the wake round-2 eyeball (#669) | no hull in a fixture | owner's eye in Play |
| 17 | `SprayEmitter` sorting over decks (order 5 against every `BoatVisualDef`'s 1) | no hull | a Play-mode look in rough water |
| 18 | the interior-mask "dry island" when a crest stands in front of a hull | no hull, no displaced pass | owner verdict pending since 2026-07-25 |
| 19 | rain streaks and rain rings in the editor | no rain condition in the matrix (visibility pinned 1) | a second sweep with a fog/rain column, if ranked |
| 20 | the interval snaps the 8 Hz push tier may still leave | a still is a still | a frame-pair capture in Play |
| 21 | the fetch feel verdict (`WaveFetch.Strength`) | felt, not seen | owner, at the helm |
| 22 | **the moon's DISC and GLITTER PATH are not cloud-aware** — a full moon behind full overcast still lays a glitter path on the water, on a night whose tint lift the cloud correctly took away | it IS shown now: `SHEET-night-corners.png`, FULL MOON · OVERCAST (added by PR 4) | mechanism named: `MoonCycle.ComputeState` packs `_MoonPhaseState` brightness + presence from the CLOCK alone (phase, arc height) and reads no weather; the water's moon content is gated by those two numbers, so nothing downstream can know it is cloudy. Fix is one shared cloud factor — the same one `MoonlightLift` now uses — applied where the moon is published. Deliberately NOT folded into PR 4 (one theme per PR) |
| 23 | **there is no CLOUD-COVER axis in the sim** — "cloudy" is inferred from fog visibility ⊔ sea-state gloom | every overcast plate in this register is a LOW-VISIBILITY plate wearing the word "cloud" | `EnvironmentSample` carries `Visibility` and `SeaState01` and nothing else; `DayNightMath.WeatherDim` takes the larger. Adequate for M1 and stated rather than invented (PR 4 was explicitly told not to add one). An M2 weather item: a real cloud axis would separate "thick fog on a still clear night" from "overcast", which today are the same input |

---

## 3. Owed verdicts and rulings (the owner's, in one place)

1. **Rank this register** — PRs 4+ take his order.
2. **Row 5**: what should a glass calm look like — a mirror, a stripe, a sheen?
3. **Row 6 / 15**: the `_OceanSwellScale` question — one sea at the field's true wavelengths (the visible waves 2.8× longer than today), or today's look with ride ≠ drawn kept?
4. **Row 7**: the whitecap mark and the slot strength — repaint, retune, or let the procedural field place the caps.
5. ~~**Row 12**: the night level and the shipped searchlight against #691's plates.~~ **RULED 2026-09-02 and ACTED ON in water-fidelity PR 4:** *"It should be dark enough at night that the player feels the need to use radar and the lighting, a clear calm night with moonlight should be brighter if not cloudy."* Delivered as (a) `Resources/DayNightProfile.asset` SHIPPED, so the night is an inspector value at last; (b) `MoonlightLiftMax` 0.05 → 0.45, with the moonless/set-moon floor bitwise unmoved; (c) cloud now takes ALL of the moon's lift instead of 40 % of it (the old `1 - weatherDim` was capped at `WeatherDimMax` 0.6, so full overcast was never reachable); (d) `_BeamLitStrength`, an ADDITIVE lit-water term on the lamp's cone, because the shipped multiply-reveal cannot light a black sea. Measured over `night-*` and `lamp-*`: moonless 0.0096 mean wet luma / 0.0095 break-line contrast, clear full moon 0.1278 / 0.1315 (x13, x14); the shipped lamp's in-beam luma 0.051 -> 0.193 at glass, 0.057 -> 0.217 in a blow, the sea outside the cone unmoved. Sheets: `SHEET-night-corners.png`, `SHEET-night-lamp.png`. **This clears the gate on boat-lights PR 2.**
6. The check-ins PR 2 brings (in its PR body, shot by `CheckIn_TheSpillingBeachInBeats_AndThePlungingLedgeAsAnEvent` at 35 m with all three dials at 1): (a) the spilling beach in beats, (b) the plunging ledge as an event. Both are owed his eye; the dials ship at 0 until he turns them.
7. **The lip spray ships ON** — the one default in PR 2 that is not today's look, because a burst cannot be judged from a plate. A Play-mode look at the steep stretch (114, 157) in a blow. The dial is now **`GameConfig` ▸ `SurfSprayIntensityOffset`** (0 = the shipped burst, −1 silent, +1 double): PR 3 moved it out of the code at #699's review, because the emitter installs its own host at runtime and a value serialized there was a default the owner could never reach. Stored as an offset because a missing YAML key deserializes to ZERO, and for a plain intensity that zero would have switched the ruling off in silence.
8. **PR 3 ships the FEEL on while PR 2's LOOK dials are still at 0** — for exactly the reason above (a shove and a lift exist only at the helm; no plate can judge them). The consequence is worth his eye: until check-in 6 gets its nod, the hull BEATS and LIFTS to a bore the water does not yet DRAW. Turning the four `_Surf*` dials up is what puts the two back in step, and both are one nod away. The feel's own dials — `GameConfig.Seakeeping.SurfBorePulse01` and `SurfLiftScale` — are the other direction if he would rather the hull waited for the water.

---

## 4. Limits of the instrument, stated

- Flat pass only (no vertex lift, no hull, no wake, no spray, no interior mask, no foam buffer pass) — §1.
- One instant: nothing that moves can be judged from it; `_Time` differs between any two plates.
- Moonless: `MoonCycle` needs a clock. A lit-moon row is one publish away (`PublishTheNewMoon` is the hook).
- The weather pairing is the sim's. A plate at #680's 8 m/s / 0.55 is not in the matrix.
- The aims are derived from the shipped terrain and the reference sea (blow, spring low), so they move if the terrain or the wave settings move — the manifest records where each sweep looked.

## 5. Re-running it

```
"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" -runTests -batchmode -projectPath <worktree> -testPlatform EditMode -testFilter HiddenHarbours.Tests.EditMode.WaterFidelityPlateSweepTests -testResults <abs>/plates.xml -logFile <abs>/plates.log
```

No `-nographics` (the sweep self-skips on the Null device and reports NOT VERIFIED). ~50 s on the RTX 4060
once the shader cache is warm; the four sheets, 144 plates, the manifests and the diagnostic land under
`<worktree>/artifacts/water-plates/`. Keep the previous run's sheets beside the new ones: the pair is the
evidence.
