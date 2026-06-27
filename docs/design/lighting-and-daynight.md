# Lighting & day/night — design + tuning recipe

> Companion to **ADR 0013** (the architecture decision). This doc is for the **owner** (how to art-direct
> the day) and for the **next agent** (the PR-2 projected-shadow spec). The canon look reference is
> Kingdoms Two Crowns' painterly atmospheric light (`vision-and-pillars.md` §4); the system serves **P1
> "The Sea Has Moods"** and lays the groundwork for **P5** night-sailing.

## 1. What it is

One deterministic 24-hour cycle controls the **whole game's** look. It is computed every tick as a pure
function of the clock hour + the weather, against an owner-tunable **`DayNightProfile`** asset, and applied
as a **single full-screen multiply tint** over the composited frame (so unlit sprites, tilemaps, water and
grass all darken/warm together) plus the **sun globals** (`_SunDir`, `_SunElevation`) that drive the water
specular and the projected shadows, and a **`_ShadowStrength` global** (how firmly a cast shadow reads now —
the sun being up folded with the live weather) that the projected shadows read so they soften under
overcast/storm. See ADR 0013 for *why* the overlay (short version: the
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

## 5. Projected shadows — SHIPPED (PR 2)

A drop-on **`SpriteShadow`** component (`Assets/_Project/Code/Art/SpriteShadow.cs`) draws a **projected**
copy of a caster's sprite — darkened, semi-transparent, **skewed + length-scaled** by the sun — so the
player **reads the time of day from a shadow's angle and length** (long west at dawn → short north at noon →
long east at dusk → faded/gone at night and under heavy cloud). It mirrors `CottageDayNight`'s drop-on
pattern and consumes the controller's published globals — `_SunDir` / `_SunElevation` (the swing + length)
and `_ShadowStrength` (the alpha; how firmly the shadow reads now, folding the sun being up with the LIVE
weather) — with **no new wiring** to the controller and no per-caster sim read.

> **The weather hook is LIVE.** `_ShadowStrength` is `DayNightMath.ShadowStrength(hour, sunrise, sunset,
> WeatherDim(visibility, seaState), OvercastFadesShadow)`, computed once per controller tick where the real
> weather already is, and published as the global the shadows read. So **`OvercastFadesShadow` genuinely
> softens the shadow in-game** under overcast/storm — not just in the unit tests. (Off the cycle — a bare
> art scene with no sim — `SpriteShadow` computes the strength locally from its fallback hour with no
> weather, so the demo still shows.)

**What shipped:**
- **Pure projection maths** in `DayNightMath` (unit-tested in `DayNightMathTests`, mirroring the PR-1 style):
  - `ShadowLength(sunElevation, lengthAtNoon, lengthAtHorizon, maxLength)` — length (× the caster's height)
    that **shortens as the sun climbs** and **lengthens as it sinks**, **clamped** so dawn/dusk don't shoot
    to infinity; 0 once the sun is at/below the horizon.
  - `ShadowSkewOffset(shadowDir, sunElevation, casterHeight, …)` — the ground-plane shear offset the
    silhouette's top is laid along (`ShadowDirection × length × height`), anchored at the feet.
  - `ShadowAlpha(maxAlpha, shadowStrength)` — `maxAlpha · ShadowStrength(…)`, so it fades at night and
    under overcast (the weather hook).
- **The `HiddenHarbours/SpriteShadow` shader** (`Assets/_Project/Art/Shaders/HiddenHarboursSpriteShadow.shader`)
  — does the **shear in the VERTEX stage** driven by `_SunDir`/`_SunElevation` (+ per-renderer tunables the
  component pushes), samples the caster sprite's alpha, and outputs a flat dark silhouette. Shipped with
  `Assets/_Project/Resources/SpriteShadow.mat` so the existing magenta shader-compile guard
  (`WaterShaderCompileGuardTests`, which force-compiles every project material) covers it.
- **The component** pools ONE child shadow renderer (created once, reused — rule 7), anchors it at the
  caster's feet, sorts it just under the caster, **pixel-snaps** the anchor (toggleable), and follows the
  caster every frame with the light recompute on a throttled tick (no per-frame allocation).

**Tunables (per component, rule 6):** max alpha / darkness colour, length-at-noon vs length-at-horizon, a
length clamp, edge softness, sorting offset, pixel-snap + PPU, foot offset, and a fallback daylight hour for
scenes with no clock. The shadow **arc** (south-bias / noon-lift / overcast-fade / sunrise-sunset) is read
from the same `DayNightProfile` the controller uses.

**How to see it / add it (owner):**
- **`Hidden Harbours ▸ Build Shadow Test`** — drops a ground plane + a post, a tree, and a standing figure
  (each already carrying `SpriteShadow`) into the current scene. Press Play, scrub the clock, watch the
  shadows swing + lengthen.
- **`Hidden Harbours ▸ Lighting ▸ Add Sprite Shadow to Selection`** — batch-adds the component to selected
  `SpriteRenderer`s (the player, the boat, trees, buildings). World-content wires real casters this way (or
  later in the scene builders) — the demo/menu never edits the scene builders.

**Alternative noted (not chosen):** URP `ShadowCaster2D` + a `Light2D` — needs the Sprite-Lit migration ADR
0013 rejects for now, and gives less control over the stylized skew. We ship the projected sprite.

## 6. Future: night lights (M2/M3, the owner's vision)

A multiply overlay darkens uniformly; it cannot by itself let a lantern/boat-light punch a bright hole in
the dark. That is fine for this foundation (night IS dark — the point). When boat-lights arrive, the
**durable model** (`DayNightProfile` + `DayNightMath` + the published globals) carries over; only the
*output stage* changes — either additive light sprites drawn above the overlay, or a migration to URP 2D
lights driven by the same profile (ADR 0013 "migration path"). Decide at M2.
