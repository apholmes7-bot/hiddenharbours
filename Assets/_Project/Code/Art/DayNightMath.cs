using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The PURE, deterministic maths of the 24-hour day/night cycle (ADR 0013): clock hour → sun
    /// position, the global tint colour, and the weather→light coupling. Everything here is a
    /// <b>pure function</b> of <c>(hour, profile, weather)</c> — no scene, no time-of-call state, no
    /// hidden randomness — so it is unit-tested headless (the determinism guard, CLAUDE.md rule 5) and
    /// the runtime <see cref="DayNightController"/> is a thin shell that just reads the Core services,
    /// calls these, and pushes the result to the shaders.
    ///
    /// <para><b>Why a separate static class.</b> Mirrors the project's "extract the math, test it
    /// headless" discipline (<see cref="WaterSurface"/>, <see cref="GrassWindBridge.WindToShaderVector"/>).
    /// These functions only use <see cref="Mathf"/> / <see cref="Vector2"/> / <see cref="Color"/> /
    /// <see cref="Gradient"/> / <see cref="AnimationCurve"/> — all evaluable in EditMode with no editor
    /// or GPU — so the whole light model is verified without opening Unity.</para>
    ///
    /// <para><b>Screen-space convention (¾ top-down, ADR 0004).</b> <c>+x</c> = east (right),
    /// <c>+y</c> = north (away from the camera, "up" the screen). The sun rises in the EAST, climbs to
    /// a high SOUTHERN arc at noon, and sets in the WEST; so shadows fall LONG to the west at dawn,
    /// SHORT toward the north at noon, and LONG to the east at dusk (the player reads the time of day
    /// from a shadow's angle and length — the PR-2 shadow pass builds on
    /// <see cref="ShadowDirection"/>).</para>
    /// </summary>
    public static class DayNightMath
    {
        /// <summary>
        /// Normalised <b>solar time</b>: 0 at solar noon, ±1 at sunrise/sunset, beyond ±1 through the
        /// night, clamped to ±2 (deep night). Computed on the 24-hour circle (so it is continuous across
        /// midnight) by taking the shortest signed distance from <paramref name="hour"/> to solar noon
        /// <c>(sunrise+sunset)/2</c> and dividing by the half-day length. The single primitive the
        /// elevation, direction and tint all derive from.
        /// </summary>
        public static float SolarX(float hour, float sunriseHour, float sunsetHour)
        {
            float halfDay = Mathf.Max((sunsetHour - sunriseHour) * 0.5f, 1e-3f);
            float solarNoon = (sunriseHour + sunsetHour) * 0.5f;
            // Shortest signed hour-distance to solar noon on a 24h circle → (-12, 12].
            float d = Mathf.Repeat(hour - solarNoon + 12f, 24f) - 12f;
            return Mathf.Clamp(d / halfDay, -2f, 2f);
        }

        /// <summary>
        /// Sun elevation in <c>[-1, 1]</c>: <c>1</c> at solar noon (zenith), <c>0</c> at the horizon
        /// (sunrise/sunset), negative through the night (below the horizon, down to <c>-1</c> at solar
        /// midnight). A clean <c>cos(SolarX · π/2)</c>: continuous, symmetric about noon, and the natural
        /// driver for specular strength and (PR 2) shadow length/fade — shadows lengthen as elevation
        /// falls toward 0 and vanish once it goes negative (sun down → no shadow).
        /// </summary>
        public static float SunElevation(float hour, float sunriseHour, float sunsetHour)
        {
            float x = SolarX(hour, sunriseHour, sunsetHour);
            return Mathf.Cos(x * (Mathf.PI * 0.5f));
        }

        /// <summary>True when the sun is at or above the horizon (daytime). <see cref="SunElevation"/> ≥ 0.</summary>
        public static bool IsDaylight(float hour, float sunriseHour, float sunsetHour)
            => SunElevation(hour, sunriseHour, sunsetHour) >= 0f;

        /// <summary>
        /// The ground-plane direction a CAST SHADOW points (unit vector), i.e. the direction AWAY from the
        /// sun: WEST at dawn (sun in the east), curving to point NORTH (<c>+y</c>, "up"/away) and SHORT at
        /// noon (sun high in the south), then EAST at dusk (sun in the west). The daytime arc parameter is
        /// <see cref="SolarX"/> clamped to ±1; <paramref name="southBias"/> tilts every shadow slightly
        /// north (the sun sits in the south, so even a low sun throws a shadow with a northward lean) and
        /// <paramref name="noonLift"/> adds the extra northward push that makes the noon shadow point
        /// straight up the screen. Used by the water specular (the glints face the sun — the opposite of
        /// this) and, in PR 2, the projected sprite shadows. NaN-safe: degenerate input falls back to
        /// <see cref="Vector2.up"/>.
        /// </summary>
        public static Vector2 ShadowDirection(float hour, float sunriseHour, float sunsetHour,
                                              float southBias, float noonLift)
        {
            // Daytime arc only: clamp to the lit half so a below-horizon sun holds the horizon heading
            // (the shadow is faded out by elevation/weather anyway — see ShadowStrength).
            float x = Mathf.Clamp(SolarX(hour, sunriseHour, sunsetHour), -1f, 1f);
            // The shadow runs AWAY from the sun on the E–W axis: at dawn (x=-1) the sun is EAST so the
            // shadow points WEST (sx=-1); at dusk (x=+1) the sun is WEST so the shadow points EAST (sx=+1).
            // So sx == x. The north lean is the south-bias plus the noon lift.
            float sx = x;
            float sy = southBias + (1f - Mathf.Abs(x)) * Mathf.Max(noonLift, 0f);
            var d = new Vector2(sx, Mathf.Max(sy, 1e-3f));
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.up;
        }

        /// <summary>
        /// The ground-plane direction TOWARD the sun (unit vector) — the opposite of
        /// <see cref="ShadowDirection"/>. Pushed to the shaders as <c>_SunDir</c> so the water's specular
        /// glints agree with where the light comes from (and, in PR 2, the shadows fall opposite it). This
        /// supersedes the water material's hand-authored <c>_LightDir</c> while the cycle runs.
        /// </summary>
        public static Vector2 SunDirection(float hour, float sunriseHour, float sunsetHour,
                                           float southBias, float noonLift)
            => -ShadowDirection(hour, sunriseHour, sunsetHour, southBias, noonLift);

        /// <summary>
        /// The global day/night MULTIPLY tint for this hour and weather — the single colour the whole scene
        /// is multiplied by (the full-screen overlay, ADR 0013 decision (b)). It is the product of the
        /// owner's two tunable curves over the day fraction <c>0..1</c> — the <see cref="DayNightProfile.SkyTint"/>
        /// gradient (warm low dawn → bright neutral/cool noon → orange-red dusk → dark blue night) and the
        /// <see cref="DayNightProfile.Intensity"/> brightness curve (genuinely LOW at night so boat lights
        /// will matter later) — then dimmed and cooled toward <see cref="DayNightProfile.OvercastTint"/> by
        /// the weather coupling (<see cref="WeatherDim"/>). Alpha is forced to 1 (a multiply overlay uses
        /// rgb only). Pure: identical inputs → identical colour, every time (rule 5).
        /// </summary>
        public static Color DayNightTint(float hour, DayNightProfile profile, float visibility, SeaState seaState)
        {
            if (profile == null) return Color.white;   // no profile → no tint (full daylight); never throw
            float t = Mathf.Repeat(hour, 24f) / 24f;    // 0..1 through the day
            Color sky = profile.SkyTint.Evaluate(t);
            float intensity = Mathf.Max(profile.Intensity.Evaluate(t), 0f);
            Color lit = new Color(sky.r * intensity, sky.g * intensity, sky.b * intensity, 1f);

            float dim = WeatherDim(visibility, seaState, profile);
            // Under cloud the light loses its colour and dims toward a cool overcast grey (also scaled by the
            // time-of-day intensity so overcast at night is darker than overcast at noon).
            Color overcast = profile.OvercastTint;
            Color cloudy = new Color(overcast.r * intensity, overcast.g * intensity, overcast.b * intensity, 1f);
            Color outCol = Color.Lerp(lit, cloudy, dim);
            outCol.a = 1f;
            return outCol;
        }

        /// <summary>
        /// <see cref="DayNightTint(float, DayNightProfile, float, SeaState)"/> plus the MOONLIGHT LIFT
        /// (owner feature: "slightly lit if the moon is visible at night"). When the sun is DOWN and the
        /// moon is UP and LIT, the night tint is pulled slightly toward the profile's cool silver-blue
        /// <see cref="DayNightProfile.MoonlightTint"/> — a full moon at its peak softly brightens the deep
        /// night (default: roughly doubles its luma), while a NEW moon, a moon below the horizon, or a
        /// zeroed <see cref="DayNightProfile.MoonlightLiftMax"/> leaves the tint <b>bitwise identical</b>
        /// to the moonless computation (new-moon nights stay pitch dark — the owner WANTS dark nights,
        /// P1/P5). Overcast suppresses it too: the lift is scaled by the same <c>1 − WeatherDim</c> factor
        /// that gloomes the rest of the light (cloud hides the moon). Pure and deterministic (rule 5): the
        /// moon inputs come from <see cref="MoonMath"/> off the clock, so identical inputs → identical
        /// colour, every time.
        /// </summary>
        /// <param name="moonIllumination">The moon's illuminated fraction 0 (new) .. 1 (full) —
        /// <see cref="MoonMath.IlluminatedFraction"/> of <see cref="MoonMath.Phase01"/>.</param>
        /// <param name="moonElevation">How high the moon is above the horizon, 0 (down) .. 1 (peak of the
        /// nightly arc) — the <c>aboveHorizon</c> out of <see cref="MoonMath.MoonArc"/>.</param>
        public static Color DayNightTint(float hour, DayNightProfile profile, float visibility, SeaState seaState,
                                         float moonIllumination, float moonElevation)
        {
            Color baseTint = DayNightTint(hour, profile, visibility, seaState);
            if (profile == null) return baseTint;

            float sunElevation = SunElevation(hour, profile.SunriseHour, profile.SunsetHour);
            float weatherDim = WeatherDim(visibility, seaState, profile);
            float lift = MoonlightLift(sunElevation, moonIllumination, moonElevation, weatherDim,
                                       profile.MoonlightLiftMax);
            if (lift <= 0f) return baseTint;   // no moon / daytime / feature off → EXACTLY the moonless tint

            Color outCol = Color.Lerp(baseTint, profile.MoonlightTint, lift);
            outCol.a = 1f;
            return outCol;
        }

        /// <summary>
        /// How strongly MOONLIGHT lifts the night tint right now, <c>0</c> (none) .. <c>liftMax</c>: the
        /// product of
        /// <list type="bullet">
        /// <item><b>night depth</b> — <c>saturate(−sunElevation)</c>: exactly 0 while the sun is at/above the
        ///   horizon (daytime is NEVER affected), ramping smoothly through dusk to its deepest at solar
        ///   midnight (≈0.9 for the default 14h day — <see cref="SolarX"/> clamps at ±12/halfDay) — which is
        ///   also where <see cref="MoonMath.MoonArc"/> peaks, so "full moon at peak" composes to (nearly) full
        ///   strength with no pop at the horizons;</item>
        /// <item><b>moon illumination</b> (0 new .. 1 full) — a NEW moon contributes exactly 0;</item>
        /// <item><b>moon elevation</b> (0 down .. 1 peak) — a moon below the horizon contributes exactly 0;</item>
        /// <item><b>clear sky</b> — <c>1 − weatherDim</c>, the SAME weather factor that dims the rest of the
        ///   light (overcast hides the moon);</item>
        /// <item><b>the owner's strength</b> — <see cref="DayNightProfile.MoonlightLiftMax"/>; 0 = feature off.</item>
        /// </list>
        /// Any zero factor returns exactly 0 (so the caller can skip the blend and keep the tint bitwise
        /// stable). Pure / deterministic / monotonic in every factor.
        /// </summary>
        public static float MoonlightLift(float sunElevation, float moonIllumination, float moonElevation,
                                          float weatherDim, float liftMax)
        {
            float nightDepth = Mathf.Clamp01(-sunElevation);   // 0 while the sun is up → daytime unaffected
            if (nightDepth <= 0f) return 0f;
            float lift = nightDepth
                       * Mathf.Clamp01(moonIllumination)
                       * Mathf.Clamp01(moonElevation)
                       * (1f - Mathf.Clamp01(weatherDim))
                       * Mathf.Clamp01(liftMax);
            return Mathf.Clamp01(lift);
        }

        /// <summary>
        /// The WEATHER→LIGHT coupling: how much the current weather DIMS + COOLS the daylight, <c>0</c>
        /// (clear) .. <c>1</c> (full overcast / storm gloom). This is the clean hook the future dynamic-weather
        /// feature plugs into — it is read here from the deterministic <see cref="EnvironmentSample"/>
        /// (<paramref name="visibility"/> = 1 − fog, and the <paramref name="seaState"/> storminess), NOT
        /// from rain/particles (those are a later feature, ADR 0013). Fog and sea-state each push the dim up
        /// and the larger wins; the result is capped by <see cref="DayNightProfile.WeatherDimMax"/> so weather
        /// alone can never black the screen out. Deterministic; monotonic in worsening weather.
        /// </summary>
        public static float WeatherDim(float visibility, SeaState seaState, DayNightProfile profile)
        {
            if (profile == null) return 0f;
            // Fog: visibility 1 (clear) → 0 dim; at/below the profile's "full dim" visibility → 1.
            float fog = Mathf.InverseLerp(1f, Mathf.Clamp01(profile.FogVisibilityForFullDim), Mathf.Clamp01(visibility));
            // Sea-state gloom: Glass(0)..Storm(7) normalised, gated so calm seas add nothing and it ramps in
            // from the profile's start fraction up to a full storm.
            float seaN = (int)SeaState.Storm > 0 ? (int)seaState / (float)(int)SeaState.Storm : 0f;
            float storm = Mathf.InverseLerp(Mathf.Clamp01(profile.SeaStateDimStart), 1f, seaN);
            float dim = Mathf.Max(fog, storm) * Mathf.Clamp01(profile.WeatherDimMax);
            return Mathf.Clamp01(dim);
        }

        /// <summary>
        /// How strongly a CAST SHADOW reads right now, <c>0</c> (none) .. <c>1</c> (full), folding in BOTH
        /// the sun being up and the weather: a shadow needs direct sun, so it scales with
        /// <see cref="SunElevation"/> above the horizon AND fades to nothing under heavy overcast (no sun
        /// through the cloud → no shadow). This is the value the PR-2 <c>SpriteShadow</c> alpha multiplies
        /// by, exposed now as part of the weather hook (and unit-tested). <paramref name="weatherDim"/> is
        /// the <see cref="WeatherDim"/> result; <paramref name="overcastFadesShadow"/> (0..1) is how much
        /// full overcast erases the shadow.
        /// </summary>
        public static float ShadowStrength(float hour, float sunriseHour, float sunsetHour,
                                           float weatherDim, float overcastFadesShadow)
        {
            float elev = SunElevation(hour, sunriseHour, sunsetHour);
            if (elev <= 0f) return 0f;                       // sun at/below the horizon → no cast shadow
            float sun = Mathf.Clamp01(elev);                 // brighter, higher sun → firmer shadow
            float weather = 1f - Mathf.Clamp01(weatherDim) * Mathf.Clamp01(overcastFadesShadow);
            return Mathf.Clamp01(sun * weather);
        }

        // ==== PROJECTED SPRITE SHADOW maths (PR 2, ADR 0013 §"Projected shadows") =========================
        // The pure projection a SpriteShadow draws with: how LONG the shadow is, the SKEW (shear) offset it
        // lays the silhouette along, and its ALPHA. All a deterministic function of the published sun globals
        // (`_SunElevation` + the ShadowDirection above) and the caster's height — no scene, unit-tested.

        /// <summary>
        /// How LONG a cast shadow is, as a multiple of the caster's height, from the sun's
        /// <paramref name="sunElevation"/> (the published <c>_SunElevation</c>; 1 at noon, 0 at the horizon,
        /// ≤0 at night). Physically the shadow length is <c>height / tan(altitude)</c> — it SHORTENS as the
        /// sun climbs and LENGTHENS without bound as the sun sinks — so we model it with a smooth blend from
        /// <paramref name="lengthAtNoon"/> (overhead sun, a short stub under the feet) to
        /// <paramref name="lengthAtHorizon"/> (low sun, a long rake), then CLAMP to
        /// <paramref name="maxLength"/> so dawn/dusk don't shoot the silhouette to infinity (and the stylized
        /// pixel-art shadow stays on-screen). Returns 0 once the sun is at/below the horizon (no shadow at
        /// night). Monotonic: a lower sun → a longer shadow. Pure / deterministic (rule 5).
        /// </summary>
        public static float ShadowLength(float sunElevation, float lengthAtNoon, float lengthAtHorizon,
                                         float maxLength)
        {
            if (sunElevation <= 0f) return 0f;                       // sun down → no shadow to draw
            float e = Mathf.Clamp01(sunElevation);
            // e = 1 (noon) → noon length; e → 0 (horizon) → horizon length. Linear in elevation is a clean,
            // predictable stylized rake (the true 1/tan blows up — the clamp tames it either way).
            float len = Mathf.Lerp(Mathf.Max(lengthAtHorizon, 0f), Mathf.Max(lengthAtNoon, 0f), e);
            return Mathf.Min(len, Mathf.Max(maxLength, 0f));
        }

        /// <summary>
        /// The ground-plane SKEW/shear OFFSET (world units) a caster of <paramref name="casterHeight"/> lays
        /// its shadow silhouette's TOP along: the shadow direction (from
        /// <see cref="ShadowDirection"/> — away from the sun) times the shadow LENGTH
        /// (<see cref="ShadowLength"/> × the height). The silhouette is anchored at the feet and sheared so
        /// its top edge lands at <c>feet + this offset</c> — long west at dawn, a short northward stub at
        /// noon, long east at dusk. NaN-safe (degenerate height/elevation → <see cref="Vector2.zero"/>, i.e.
        /// no shadow). Pure / deterministic.
        /// </summary>
        public static Vector2 ShadowSkewOffset(Vector2 shadowDir, float sunElevation, float casterHeight,
                                               float lengthAtNoon, float lengthAtHorizon, float maxLength)
        {
            float lenMul = ShadowLength(sunElevation, lengthAtNoon, lengthAtHorizon, maxLength);
            float worldLen = lenMul * Mathf.Max(casterHeight, 0f);
            if (worldLen <= 1e-5f) return Vector2.zero;
            return shadowDir * worldLen;
        }

        /// <summary>
        /// The ALPHA a cast shadow draws at, <c>0</c>..<paramref name="maxAlpha"/>: the artist-set darkness
        /// scaled by <see cref="ShadowStrength"/> (so the shadow fades to nothing at night and softens under
        /// overcast — the weather hook). Clamped to <c>[0, 1]</c>. Pure / deterministic.
        /// </summary>
        public static float ShadowAlpha(float maxAlpha, float shadowStrength)
            => Mathf.Clamp01(Mathf.Clamp01(maxAlpha) * Mathf.Clamp01(shadowStrength));
    }
}
