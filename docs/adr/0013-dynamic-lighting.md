# ADR 0013 — Deterministic 24-hour dynamic lighting: one global tint over the WHOLE frame (overlay), one sun driving specular + shadows, a weather-dim hook

- **Status:** **Accepted** — art-pipeline + lead-architect. This ADR ships **code** (PR 1: the global
  24-hour cycle + the consistent whole-frame tint + the sun-direction globals + the weather-dim hook +
  EditMode tests). The sun-driven **projected shadows are PR 2** (spec'd in §7 below; not built here).
- **Date:** 2026-06-27
- **Decision owner:** lead-architect (this is a cross-cutting, whole-game render seam — every scene's look,
  the water/grass shaders, and a new Core-read of the clock/weather; `agents/coordination.md` §1.1
  "Water/fog/lighting" seam, CLAUDE.md rule 4). art-pipeline owns the *look* (the gradient/curve, the
  palette of the day).
- **Serves:** **P1 "The Sea Has Moods"** (the 24h clock becomes a *real force you read* — the world warms
  at dawn and goes genuinely dark at night, not set-dressing) and **P5 "Cozy but with Teeth"** (a dark
  night you must light is the foundation for night-sailing risk). The weather-dim hook is the spine the
  future dynamic-weather feature hangs on.
- **Related:** `0010-water-rendering.md` (the self-lit layered water shader this drives the sun into),
  `0012-shoreline-rendering.md` (the recent water-edge work; same shader), `0004-perspective-and-scene-strategy.md`
  (¾ top-down, scene-per-region — why the lighting must self-install in *every* scene), `0005-pc-first-target.md`
  (the 60fps budget, mobile-portable), `vision-and-pillars.md` §5.5 (the 24-hour clock), the owner's
  **night-lighting vision** (genuinely dark nights navigated by **boat lights** later — so night MUST be
  dark and lights MUST matter; M2/M3), `design/lighting-and-daynight.md` (the tunable recipe + the PR-2
  shadow spec).
- **Implementation (this PR):**
  `Assets/_Project/Code/Art/DayNightMath.cs` (pure model),
  `Assets/_Project/Code/Art/DayNightProfile.cs` (the owner's tunable asset),
  `Assets/_Project/Code/Art/DayNightController.cs` (self-installing controller + overlay),
  `Assets/_Project/Art/Shaders/HiddenHarboursDayNight.shader` (+ `Assets/_Project/Resources/DayNight.mat`)
  (the full-screen multiply overlay),
  a minimal `_SunDir` read added to `Assets/_Project/Art/Shaders/HiddenHarboursWater.shader`,
  `Assets/Tests/EditMode/Art/DayNightMathTests.cs`.

## Context

The game needs ONE global day/night look that controls the **whole** scene deterministically from the
clock (`GameServices.Clock.HourOfDay`), with a hook so weather can dim it later. This is the FOUNDATION
to lay **before** dynamic weather visuals: get the light + the sun direction + the weather→light coupling
right first; rain/storm particles come later.

The hard architectural question is **how to apply one tint CONSISTENTLY across two very different kinds of
renderer**:

1. **The sprites/tilemaps are Sprite-UNLIT.** Investigated: the project uses Unity's default
   `Sprite-Unlit` rendering — there is **no `Light2D` anywhere**, and the unlit sprite shader **does not
   sample 2D lights**. So a URP 2D **Global Light 2D would do NOTHING to the sprites** (it only affects
   *Sprite-Lit* materials). Darkening the sprites via lights would require **migrating every sprite to
   Sprite-Lit** — a project-wide pipeline change, out of scope and impossible to validate without opening
   Unity.
2. **The water + grass are custom `Universal2D` SELF-LIT shaders.** They compute their own colour and
   **ignore 2D lights** by construction. A Global Light 2D leaves the **water bright** while sprites (if
   they were lit) darkened — the exact inconsistency to avoid.

So the three candidate mechanisms from the brief, scored against *"darkens EVERYTHING together,
deterministic, tunable, self-installing, no per-sprite migration, validatable without Unity"*:

| Approach | Darkens unlit sprites? | Darkens self-lit water/grass? | Cost / risk |
|---|---|---|---|
| **(a) Global Light 2D + `_DayNightTint` the shaders multiply** | **No** — sprites are unlit; would need a full Sprite-Lit migration | Yes (shaders multiply) | High — migrate every sprite material; can't verify headless |
| **(b) Full-screen colour-grade / MULTIPLY OVERLAY of the composited frame** | **Yes** | **Yes** | **Low — one quad, one shader; consistent by construction** |
| **(c) A global `_DayNightTint` uniform every shader multiplies** | **No** — the default sprite shader won't read it (same migration problem) | Yes | High for sprites (same as (a)) |

## Decision

**Apply the global tint as a single full-screen MULTIPLY OVERLAY of the composited frame (approach (b)).**
A self-installing controller draws one camera-filling quad ABOVE all world sprites (and below the
screen-space HUD) and multiplies the whole frame by a deterministic `_DayNightTint`. Because **every pixel
the world camera drew** — unlit sprites, tilemaps, the self-lit water + grass — is multiplied **in one
place**, they darken and warm/cool **together by construction**; no layer can drift bright while the rest
goes dark, with **zero per-sprite migration** and **no Volume/Renderer-Feature wiring** (neither of which
I can author without opening Unity).

The day/night value is the **single source of truth** published as the global colour `_DayNightTint`
(`Shader.SetGlobalColor`) — the overlay material reads it, and it is available to any future custom or
Sprite-Lit shader that wants per-layer control. Alongside it the controller publishes the **sun**:
`_SunDir` (ground-plane direction toward the sun) and `_SunElevation`, so the water shader's specular
glints agree with where the light comes from (and the PR-2 shadows fall the right way).

Everything is **deterministic** (a pure function of `(clock hour, weather, profile)` — recomputed, saved
nothing, rule 5), **read through Core** (the clock + environment accessors only, rule 4), **tunable** (a
`DayNightProfile` ScriptableObject of curves/colours the owner art-directs, rule 6), and **self-installing**
(a `RuntimeInitializeOnLoadMethod` host like the grass bridge / audio director — works in every scene with
no wiring, ADR 0004's scene-per-region world).

### Why the overlay, concretely

- **It is the only option that darkens the UNLIT sprites without a pipeline migration.** Given the
  investigated fact that sprites sample no light, (a) and (c) cannot touch them without converting the
  whole sprite library to Sprite-Lit. The overlay multiplies the *output*, so it is renderer-agnostic.
- **Consistency is structural, not maintained by discipline.** With (c) every new shader would have to
  remember to multiply the tint or it would pop bright at night. The overlay can't be forgotten.
- **Night is genuinely dark.** A multiply by a low dark-blue value crushes the whole frame down — exactly
  the dark the owner's boat-lights vision needs.
- **The HUD stays readable.** A screen-space-overlay canvas renders *after* the camera, so the tide/wind/
  time HUD is never multiplied into the dark (P1/UX: you must still read the sea's mood at night).

### The trade-off we are accepting (and the migration path)

A multiply overlay is a *global* darkener: it cannot, by itself, let a **light source punch a bright hole
in the dark** (a lantern, a boat's running lights). That is fine for THIS foundation — the night-lighting
vision (boat lights, nightvision/radar) is explicitly **M2/M3**. When it arrives, two clean paths exist
and the **durable part — the deterministic `DayNightProfile` + `DayNightMath` + the published `_SunDir`/
`_DayNightTint` — carries over unchanged**; only the *application* evolves:

1. **Lights render ABOVE the overlay** as additive sprites (cheap, stylized, pixel-art friendly), or
2. **Migrate to URP 2D lights**: convert the relevant sprites to Sprite-Lit and drive a Global Light 2D's
   intensity/colour from the same `DayNightProfile` (the controller swaps its *output* stage; the model
   doesn't change).

This is recorded so the choice is deliberate, not a corner we painted ourselves into.

## What this PR ships (the model + the cycle + the hook)

### 1. The 24-hour global cycle (`DayNightController`, self-installing)

Each throttled tick (~10 Hz — the cycle is slow but a clock-scrub wants smoothness; rule 7) the controller
reads `GameServices.Clock.HourOfDay` (falling back to a tunable daylight hour when there is no clock yet,
so EditMode / bare art scenes render normally), evaluates the pure model, and pushes the result. It
supersedes the per-scene static `m_AmbientSkyColor` as the single source of the global look.

### 2. The tint (`DayNightMath.DayNightTint`, pure)

`tint = SkyTint.Evaluate(dayFraction) × Intensity.Evaluate(dayFraction)`, then dimmed/cooled toward an
overcast grey by the weather coupling. **Two tunable curves** (rule 6): the `SkyTint` **gradient** (warm
low dawn → bright neutral/cool noon → orange-red dusk → dark blue night) and an **intensity curve** (pulled
hard down overnight so night is genuinely dark). Default authored in `DayNightProfile.ApplyDefaults`.

### 3. The sun direction (`DayNightMath`, pure → `_SunDir` / `_SunElevation` globals)

From `HourOfDay`: elevation `cos(SolarX · π/2)` (1 at noon, 0 at the horizon, negative at night) and a
ground-plane direction that rises **east**, is short/overhead toward **north** at noon, and sets **west**
(¾ top-down screen convention). The water shader now reads `_SunDir.xy` for its specular light direction
(falling back to its hand-authored `_LightDir` when the cycle isn't running — so the owner's committed
`Water.mat` look is **unchanged** until the controller drives it). One global, mirroring `WaterSurface`'s
uniform-push pattern — minimal water-shader touch.

### 4. The weather-dim HOOK (`DayNightMath.WeatherDim` / `ShadowStrength`, pure)

Reads the **deterministic** `EnvironmentSample` (via `GameServices.Environment`, rule 4): `Visibility`
(= 1 − fog) and `SeaState` (storminess). Overcast/stormy **dims + cools** the light toward the overcast
grey and **fades the cast shadow** (no sun through the cloud → no shadow). The amount is capped by a
tunable max so weather alone never blacks the screen out — night is the dark, weather is the gloom on top.
**No rain/storm particles are built** — this is the clean coupling the future weather feature plugs into.

## Determinism, performance, seams (the invariants)

- **Deterministic (rule 5).** The whole look is a pure function of `(hour, weather, profile)`; nothing is
  saved or randomised. The pure model lives in `DayNightMath` and is unit-tested headless (the determinism
  guard) — no scene, no GPU.
- **Core-only sim reads (rule 4).** Time + weather are read through `GameServices.Clock` /
  `GameServices.Environment` interfaces; no feature-module concrete types; nothing is written back.
- **No magic numbers (rule 6).** Every curve/colour/threshold is a serialized field on `DayNightProfile`,
  editable by the owner with no code.
- **Performance (rule 7).** One global light-equivalent (the overlay quad), one material, three global
  sets on a throttled tick, no per-frame allocation, no per-sprite cost. Mobile-portable.
- **Shader-compile guarded.** The shipped `DayNight.mat` (Resources) is force-compiled by the existing
  magenta guard (`WaterShaderCompileGuardTests`), as is the `_SunDir` change to the water shader via
  `Water.mat` — a broken overlay/water shader fails CI red, not magenta-in-build.

## Consequences

- **The whole game gains a deterministic day/night look immediately**, consistent across unlit sprites and
  the self-lit water/grass, tunable entirely from one asset, in every scene with no wiring.
- **The owner's `Water.mat` tuning is untouched** — the water shader only *adds* a `_SunDir` read with a
  fallback to the authored `_LightDir`.
- **A clean weather→light seam exists** for the future dynamic-weather feature (no particles built).
- **PR 2 (projected sprite shadows) is teed up** on the same `_SunDir`/`_SunElevation`/`ShadowStrength`
  primitives (spec in `design/lighting-and-daynight.md` §"Projected shadows").
- **PR 2 (projected sprite shadows) is now SHIPPED** — a drop-on `SpriteShadow` component +
  `HiddenHarbours/SpriteShadow` GPU-shear shader (+ `Resources/SpriteShadow.mat`, covered by the same
  magenta guard) draws a darkened, skewed, length-scaled silhouette anchored at the caster's feet, swinging
  and lengthening with the sun and fading at night/under overcast. It consumes the `_SunDir`/
  `_SunElevation` globals (swing + length) and the controller's new `_ShadowStrength` global (alpha) with no
  new controller wiring; the projection maths (`ShadowLength` / `ShadowSkewOffset` / `ShadowAlpha` /
  `ShadowStrength`) are pure + unit-tested. An editor menu ("Build Shadow Test" + "Add Sprite Shadow to
  Selection") lets the owner see/attach it without touching the scene builders. See
  `design/lighting-and-daynight.md` §5 (shipped).
- **PR 2 follow-up: the weather→shadow hook is now LIVE.** Originally the live path overwrote the shadow
  strength with `clamp01(sun elevation)` and never read weather, so `OvercastFadesShadow` was dead in-game.
  The controller now computes `ShadowStrength(hour, sunrise, sunset, WeatherDim(visibility, seaState),
  OvercastFadesShadow)` from the same Core weather it already reads for the tint and publishes it as a new
  global `_ShadowStrength` (one extra `SetGlobalFloat` per tick); `SpriteShadow` reads it (one
  `GetGlobalFloat` per shadow tick) on the live path. The shadow genuinely **softens under overcast/storm**
  now — tunable via `DayNightProfile.OvercastFadesShadow`. A PlayMode test (`ShadowStrengthGlobalPlayTests`)
  pins that the published `_ShadowStrength` is lower in storm/fog than in clear weather (and zero at night).
- **A documented migration path to true 2D lights** for the M2/M3 boat-lights vision — the model is the
  durable part; the overlay is a swappable output stage.

## Rejected alternatives

- **(a) Global Light 2D + lit sprites.** The sprites are unlit and sample no light; this needs a
  project-wide Sprite-Lit migration (every sprite material) that is out of scope, perf-relevant, and
  un-validatable without opening Unity. Kept as the *future* path for boat-lights, not the foundation.
- **(c) A global `_DayNightTint` every shader multiplies.** Works for the custom water/grass shaders but
  **not** the default unlit sprites (same migration problem), and relies on every future shader remembering
  to multiply it — consistency by discipline, not construction.
- **A URP post-process colour-grade Volume.** Tints the final image (good) but requires authoring a Volume
  + a ScriptableRendererFeature/Volume profile in the URP renderer asset — wiring I cannot create or verify
  without opening Unity, and heavier than one quad. The overlay achieves the same multiply, self-installed.
- **Saving the computed light/sun into the save.** Violates rule 5 — it is recomputed from `(clock,
  weather, profile)`; saving it would bloat saves and risk drift. Not saved.
- **Building the shadows / boat-lights now.** Shadows are PR 2 (spec'd, kept small per CLAUDE.md §4);
  boat-lights are M2/M3 (rule 8 — stay in phase). This PR is the foundation only.
