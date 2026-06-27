# Lighting & day/night — design + tuning recipe

> Companion to **ADR 0013** (the architecture decision). This doc is for the **owner** (how to art-direct
> the day) and for the **next agent** (the PR-2 projected-shadow spec). The canon look reference is
> Kingdoms Two Crowns' painterly atmospheric light (`vision-and-pillars.md` §4); the system serves **P1
> "The Sea Has Moods"** and lays the groundwork for **P5** night-sailing.

## 1. What it is

One deterministic 24-hour cycle controls the **whole game's** look. It is computed every tick as a pure
function of the clock hour + the weather, against an owner-tunable **`DayNightProfile`** asset, and applied
as a **single full-screen multiply tint** over the composited frame (so unlit sprites, tilemaps, water and
grass all darken/warm together) plus two **sun globals** (`_SunDir`, `_SunElevation`) that drive the water
specular today and the projected shadows in PR 2. See ADR 0013 for *why* the overlay (short version: the
sprites are unlit and sample no 2D light, so only an output-stage tint darkens everything without migrating
every sprite).

**It self-installs.** Nothing to place in a scene. Press Play in any scene and the cycle runs.

## 2. How the owner art-directs the day (no code)

1. **Create the profile:** `Assets ▸ Create ▸ Hidden Harbours ▸ Lighting ▸ Day-Night Profile`.
2. **Save it at exactly** `Assets/_Project/Resources/DayNightProfile.asset` (the name/path is how the
   controller finds it; without it the controller uses a built-in default).
3. **Edit and watch it live.** Press Play, open the **Tide Scrubber / DevFastTide** (or set the clock's
   `TimeScale` up) and scrub the day — the screen warms at dawn, brightens at noon, goes orange at dusk,
   and dark blue at night as you move the clock.

### The tunable set (`DayNightProfile`)

| Field | What it does | Default |
|---|---|---|
| **Sky tint** (Gradient) | The whole-screen MULTIPLY colour across the day fraction (0 = midnight, 0.5 = noon, 1 = midnight). This is the main dial — paint the *mood* of each hour here. | warm low dawn → bright cool noon → orange-red dusk → dark blue night |
| **Intensity** (Curve, 0..1) | Overall brightness multiplied into the tint. Pull the night down HARD here for a darker night without changing hue. | ~0.18 at night, 1.0 at noon |
| **Sunrise hour / Sunset hour** | When the sun crosses the horizon. Solar noon sits halfway between; the sun arc + specular + (PR 2) shadows derive from these. | 6 / 20 |
| **Shadow south-bias** | How far north every shadow leans even at a low sun (the sun sits in the south). | 0.2 |
| **Shadow noon-lift** | Extra northward push at noon so the midday shadow points straight up (reads as "short, sun overhead"). | 0.9 |
| **Fog visibility for full dim** | Visibility (1 clear → 0 fog) at/below which fog fully dims the light. | 0.15 |
| **Sea-state dim start** | Sea-state fraction (Glass 0 → Storm 1) where storm gloom begins (~0.6 ≈ a Gale). | 0.6 |
| **Weather dim max** | The most weather *alone* may dim/cool the daylight — caps it so a storm at noon never blacks out. | 0.6 |
| **Overcast tint** | The cool grey the light shifts toward under cloud/storm. | (0.5, 0.55, 0.62) |
| **Overcast fades shadow** | How much full overcast erases a cast shadow (no sun → no shadow). | 0.85 |

> **Coordinates with `CottageDayNight`.** The cottage window day↔night swap (dawn 6 / dusk 19) already
> exists; keep the profile's sunrise/sunset near those so the windows light up as the global tint goes dark.

## 3. Determinism & the seam (for reviewers)

- The look is a **pure function** of `(HourOfDay, EnvironmentSample, DayNightProfile)` — recomputed every
  tick, **saved nothing** (rule 5). The pure model is `DayNightMath` (unit-tested headless,
  `DayNightMathTests`).
- Time + weather are read **only** through `GameServices.Clock` / `GameServices.Environment` (Core
  interfaces, rule 4). Nothing is written back to the sim.
- The water shader only **adds** a `_SunDir` read with a fallback to its authored `_LightDir`, so the
  owner's committed `Water.mat` look is unchanged until the controller drives the sun.

## 4. Performance (rule 7)

One overlay quad + one material, three global uniform sets on a ~10 Hz throttled tick, no per-frame
allocation, no per-sprite cost. Mobile-portable. The overlay draws above world sprites and below the
screen-space HUD (the HUD stays readable at night — you must still read the sea's mood).

## 5. Projected shadows — the PR-2 spec (NOT built in PR 1)

The recommended pixel-art approach (ADR 0013): a `SpriteShadow` component that draws a **projected** copy
of a caster's sprite — darkened, semi-transparent, **skewed + length-scaled** by the sun — so the player
**reads the time of day from a shadow's angle and length** (long west at dawn → short north at noon → long
east at dusk → faded/gone at night and under heavy cloud).

**It builds on the primitives already shipped in PR 1** (so PR 2 is mostly wiring + the shear matrix):
- direction: `DayNightMath.ShadowDirection(hour, sunrise, sunset, southBias, noonLift)` (already published
  inversely as `_SunDir`);
- strength/fade: `DayNightMath.ShadowStrength(hour, sunrise, sunset, weatherDim, overcastFadesShadow)`
  (already returns 0 at night and fades under overcast — the weather hook);
- elevation: `_SunElevation` (already a global).

**Shadow transform (the PR-2 maths to add + unit-test, mirroring the PR-1 test style):**
- **Length** scales **inversely** with sun elevation: `len = baseLen · lerp(longAtHorizon, shortAtNoon,
  elevation01)` — a long shadow at a low sun, short at noon, clamped so dawn/dusk don't shoot to infinity.
- **Skew/shear**: the shadow's top vertices are sheared along `ShadowDirection` by `len` while the base
  stays pinned at the caster's feet (anchor at the sprite's ground pivot). Express as a shear matrix on a
  shadow mesh, or a tiny vertex-shader shear driven by `_SunDir`/`_SunElevation` (a `HiddenHarbours/SpriteShadow`
  shader) — the latter keeps it GPU-cheap and pixel-snappable.
- **Alpha** = `maxAlpha · ShadowStrength(...)` — fades to 0 at night and under heavy cloud.
- **Sort** just under the caster; **pool** the shadow renderers (rule 7); **pixel-snap** the projected
  offset (the project's PixelSnap discipline) so the shadow reads as crisp pixel art, not a smeared blob.

**Casters:** trees, buildings, the player, the boat. Keep it a drop-on component (like `CottageDayNight`)
plus optional auto-attach for tagged casters.

**Alternative noted (not preferred):** URP `ShadowCaster2D` + a `Light2D` — but that needs the Sprite-Lit
migration ADR 0013 rejects for now, and gives less control over the stylized skew. Prefer the projected
sprite.

## 6. Future: night lights (M2/M3, the owner's vision)

A multiply overlay darkens uniformly; it cannot by itself let a lantern/boat-light punch a bright hole in
the dark. That is fine for this foundation (night IS dark — the point). When boat-lights arrive, the
**durable model** (`DayNightProfile` + `DayNightMath` + the published globals) carries over; only the
*output stage* changes — either additive light sprites drawn above the overlay, or a migration to URP 2D
lights driven by the same profile (ADR 0013 "migration path"). Decide at M2.
