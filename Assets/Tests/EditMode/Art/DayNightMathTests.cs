using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Determinism + correctness guard for the pure day/night maths (ADR 0013, CLAUDE.md rule 5). These run
    /// headless — no scene, no GPU — and pin the clock→sun→tint→weather model so the look stays a reproducible
    /// function of <c>(hour, weather, profile)</c>. The runtime <see cref="DayNightController"/> is a thin
    /// shell over these, so guarding the maths guards the system.
    /// </summary>
    public class DayNightMathTests
    {
        private const float Sunrise = 6f;
        private const float Sunset = 20f;   // solar noon = 13:00, half-day = 7h
        private const float Eps = 1e-3f;

        private static DayNightProfile MakeProfile() => DayNightProfile.CreateDefault();

        // ---- SolarX ------------------------------------------------------------------------------------

        [Test]
        public void SolarX_IsZeroAtSolarNoon()
        {
            float noon = (Sunrise + Sunset) * 0.5f;
            Assert.AreEqual(0f, DayNightMath.SolarX(noon, Sunrise, Sunset), Eps);
        }

        [Test]
        public void SolarX_IsPlusMinusOneAtSunriseAndSunset()
        {
            Assert.AreEqual(-1f, DayNightMath.SolarX(Sunrise, Sunrise, Sunset), Eps, "sunrise -> -1");
            Assert.AreEqual(1f, DayNightMath.SolarX(Sunset, Sunrise, Sunset), Eps, "sunset -> +1");
        }

        [Test]
        public void SolarX_IsContinuousAcrossMidnight()
        {
            float justBefore = DayNightMath.SolarX(23.99f, Sunrise, Sunset);
            float justAfter = DayNightMath.SolarX(0.01f, Sunrise, Sunset);
            // Symmetric about midnight: nearly equal magnitude, opposite sign, no 24h jump discontinuity.
            Assert.AreEqual(Mathf.Abs(justBefore), Mathf.Abs(justAfter), 0.02f);
        }

        [Test]
        public void SolarX_IsClampedToTwo()
        {
            for (float h = 0f; h < 24f; h += 0.5f)
                Assert.LessOrEqual(Mathf.Abs(DayNightMath.SolarX(h, Sunrise, Sunset)), 2f + Eps);
        }

        // ---- SunElevation ------------------------------------------------------------------------------

        [Test]
        public void SunElevation_PeaksAtNoonZeroAtHorizonNegativeAtNight()
        {
            float noon = (Sunrise + Sunset) * 0.5f;
            Assert.AreEqual(1f, DayNightMath.SunElevation(noon, Sunrise, Sunset), Eps, "noon = zenith");
            Assert.AreEqual(0f, DayNightMath.SunElevation(Sunrise, Sunrise, Sunset), Eps, "sunrise = horizon");
            Assert.AreEqual(0f, DayNightMath.SunElevation(Sunset, Sunrise, Sunset), Eps, "sunset = horizon");
            Assert.Less(DayNightMath.SunElevation(0f, Sunrise, Sunset), 0f, "midnight = below horizon");
            Assert.Less(DayNightMath.SunElevation(3f, Sunrise, Sunset), 0f, "deep night = below horizon");
        }

        [Test]
        public void SunElevation_StaysInRange()
        {
            for (float h = 0f; h < 24f; h += 0.25f)
            {
                float e = DayNightMath.SunElevation(h, Sunrise, Sunset);
                Assert.GreaterOrEqual(e, -1f - Eps);
                Assert.LessOrEqual(e, 1f + Eps);
            }
        }

        [Test]
        public void IsDaylight_TracksTheHorizon()
        {
            Assert.IsTrue(DayNightMath.IsDaylight(13f, Sunrise, Sunset), "1pm is daylight");
            Assert.IsFalse(DayNightMath.IsDaylight(2f, Sunrise, Sunset), "2am is night");
            Assert.IsFalse(DayNightMath.IsDaylight(23f, Sunrise, Sunset), "11pm is night");
        }

        // ---- ShadowDirection / SunDirection ------------------------------------------------------------

        [Test]
        public void ShadowDirection_DawnPointsWest_DuskPointsEast_NoonPointsNorth()
        {
            Vector2 dawn = DayNightMath.ShadowDirection(Sunrise, Sunrise, Sunset, 0.2f, 0.9f);
            Vector2 dusk = DayNightMath.ShadowDirection(Sunset, Sunrise, Sunset, 0.2f, 0.9f);
            float noonH = (Sunrise + Sunset) * 0.5f;
            Vector2 noon = DayNightMath.ShadowDirection(noonH, Sunrise, Sunset, 0.2f, 0.9f);

            Assert.Less(dawn.x, 0f, "dawn shadow falls WEST (-x)");
            Assert.Greater(dusk.x, 0f, "dusk shadow falls EAST (+x)");
            Assert.Less(Mathf.Abs(noon.x), 0.1f, "noon shadow has little E-W component");
            Assert.Greater(noon.y, 0.9f, "noon shadow points NORTH (+y, up the screen)");
        }

        [Test]
        public void ShadowDirection_IsAlwaysUnitLength()
        {
            for (float h = 0f; h < 24f; h += 0.5f)
            {
                Vector2 d = DayNightMath.ShadowDirection(h, Sunrise, Sunset, 0.2f, 0.9f);
                Assert.AreEqual(1f, d.magnitude, Eps, $"unit at hour {h}");
            }
        }

        [Test]
        public void SunDirection_IsOppositeTheShadow()
        {
            for (float h = 7f; h <= 19f; h += 2f)
            {
                Vector2 shadow = DayNightMath.ShadowDirection(h, Sunrise, Sunset, 0.2f, 0.9f);
                Vector2 sun = DayNightMath.SunDirection(h, Sunrise, Sunset, 0.2f, 0.9f);
                Assert.AreEqual(-shadow.x, sun.x, Eps);
                Assert.AreEqual(-shadow.y, sun.y, Eps);
            }
        }

        // ---- DayNightTint ------------------------------------------------------------------------------

        [Test]
        public void DayNightTint_IsDeterministic()
        {
            var p = MakeProfile();
            Color a = DayNightMath.DayNightTint(8.3f, p, 0.8f, SeaState.Light);
            Color b = DayNightMath.DayNightTint(8.3f, p, 0.8f, SeaState.Light);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void DayNightTint_NullProfile_IsWhite()
        {
            Assert.AreEqual(Color.white, DayNightMath.DayNightTint(12f, null, 1f, SeaState.Glass));
        }

        [Test]
        public void DayNightTint_NightIsDarkerThanNoon()
        {
            var p = MakeProfile();
            float noon = Brightness(DayNightMath.DayNightTint(13f, p, 1f, SeaState.Glass));
            float night = Brightness(DayNightMath.DayNightTint(2f, p, 1f, SeaState.Glass));
            Assert.Less(night, noon * 0.6f, "night must be GENUINELY dark vs noon (boat lights will matter)");
        }

        [Test]
        public void DayNightTint_AlwaysOpaque()
        {
            var p = MakeProfile();
            for (float h = 0f; h < 24f; h += 1f)
                Assert.AreEqual(1f, DayNightMath.DayNightTint(h, p, 1f, SeaState.Glass).a, Eps);
        }

        [Test]
        public void DayNightTint_OvercastDimsTheNoonLight()
        {
            var p = MakeProfile();
            float clear = Brightness(DayNightMath.DayNightTint(13f, p, 1f, SeaState.Glass));
            float foggy = Brightness(DayNightMath.DayNightTint(13f, p, 0f, SeaState.Glass));
            Assert.Less(foggy, clear, "thick fog dims the daylight");
        }

        // ---- WeatherDim --------------------------------------------------------------------------------

        [Test]
        public void WeatherDim_ClearWeatherIsZero()
        {
            var p = MakeProfile();
            Assert.AreEqual(0f, DayNightMath.WeatherDim(1f, SeaState.Glass, p), Eps);
        }

        [Test]
        public void WeatherDim_IsCappedAtProfileMax()
        {
            var p = MakeProfile();   // WeatherDimMax default 0.6
            float full = DayNightMath.WeatherDim(0f, SeaState.Storm, p);
            Assert.LessOrEqual(full, p.WeatherDimMax + Eps);
            Assert.Greater(full, 0f);
        }

        [Test]
        public void WeatherDim_RisesWithWorseningSeaState()
        {
            var p = MakeProfile();
            float calm = DayNightMath.WeatherDim(1f, SeaState.Calm, p);
            float gale = DayNightMath.WeatherDim(1f, SeaState.Gale, p);
            float storm = DayNightMath.WeatherDim(1f, SeaState.Storm, p);
            Assert.AreEqual(0f, calm, Eps, "calm seas add no gloom");
            Assert.Greater(storm, gale, "a storm is gloomier than a gale");
            Assert.GreaterOrEqual(gale, 0f);
        }

        // ---- ShadowStrength ----------------------------------------------------------------------------

        [Test]
        public void ShadowStrength_IsZeroAtNight()
        {
            Assert.AreEqual(0f, DayNightMath.ShadowStrength(2f, Sunrise, Sunset, 0f, 0.85f), Eps);
        }

        [Test]
        public void ShadowStrength_FullOvercastErasesTheShadow()
        {
            // overcastFadesShadow = 1, weatherDim = 1 -> no shadow even at high noon.
            Assert.AreEqual(0f, DayNightMath.ShadowStrength(13f, Sunrise, Sunset, 1f, 1f), Eps);
        }

        [Test]
        public void ShadowStrength_NoonClearIsStrong_AndWeatherFadesIt()
        {
            float clear = DayNightMath.ShadowStrength(13f, Sunrise, Sunset, 0f, 0.85f);
            float cloudy = DayNightMath.ShadowStrength(13f, Sunrise, Sunset, 0.6f, 0.85f);
            Assert.Greater(clear, 0.9f, "high clear noon casts a firm shadow");
            Assert.Less(cloudy, clear, "cloud fades the cast shadow");
            Assert.Greater(cloudy, 0f);
        }

        // ---- ShadowLength (PR 2 projection) ------------------------------------------------------------

        [Test]
        public void ShadowLength_IsShortAtNoon_LongAtLowSun()
        {
            float noon = DayNightMath.ShadowLength(1f, 0.4f, 6f, 10f);   // overhead sun
            float low = DayNightMath.ShadowLength(0.1f, 0.4f, 6f, 10f);  // sun near the horizon
            Assert.AreEqual(0.4f, noon, Eps, "overhead sun -> short stub (length-at-noon)");
            Assert.Greater(low, noon, "a low sun casts a LONGER shadow than a high one");
        }

        [Test]
        public void ShadowLength_IsZeroAtOrBelowHorizon()
        {
            Assert.AreEqual(0f, DayNightMath.ShadowLength(0f, 0.4f, 6f, 10f), Eps, "horizon -> no shadow");
            Assert.AreEqual(0f, DayNightMath.ShadowLength(-0.5f, 0.4f, 6f, 10f), Eps, "below horizon -> no shadow");
        }

        [Test]
        public void ShadowLength_IsClampedSoDawnDuskDontShootToInfinity()
        {
            // A tiny positive elevation would extrapolate toward the horizon length; the clamp caps it.
            float max = 3f;
            float len = DayNightMath.ShadowLength(0.001f, 0.4f, 100f, max);
            Assert.LessOrEqual(len, max + Eps, "length is clamped to maxLength");
        }

        [Test]
        public void ShadowLength_IsMonotonicAsTheSunSinks()
        {
            float prev = DayNightMath.ShadowLength(1f, 0.4f, 50f, 1000f);
            for (float e = 0.95f; e > 0.05f; e -= 0.05f)
            {
                float len = DayNightMath.ShadowLength(e, 0.4f, 50f, 1000f);
                Assert.GreaterOrEqual(len, prev - Eps, $"longer (or equal) as the sun sinks at elev {e}");
                prev = len;
            }
        }

        // ---- ShadowSkewOffset --------------------------------------------------------------------------

        [Test]
        public void ShadowSkewOffset_PointsAlongShadowDir_ScaledByHeightAndLength()
        {
            Vector2 dir = new Vector2(-1f, 0f);   // shadow falls west
            // length multiplier at elev 0.5: lerp(horizon=4, noon=0.4, 0.5) = 2.2 ; height 2 -> world 4.4
            Vector2 off = DayNightMath.ShadowSkewOffset(dir, 0.5f, 2f, 0.4f, 4f, 100f);
            Assert.AreEqual(-4.4f, off.x, 1e-3f, "offset = dir * length * height");
            Assert.AreEqual(0f, off.y, Eps);
        }

        [Test]
        public void ShadowSkewOffset_IsZeroAtNight_AndForZeroHeight()
        {
            Vector2 dir = new Vector2(-1f, 0.2f).normalized;
            Assert.AreEqual(Vector2.zero, DayNightMath.ShadowSkewOffset(dir, -0.3f, 2f, 0.4f, 4f, 100f), "night -> no offset");
            Assert.AreEqual(Vector2.zero, DayNightMath.ShadowSkewOffset(dir, 0.8f, 0f, 0.4f, 4f, 100f), "zero height -> no offset");
        }

        [Test]
        public void ShadowSkewOffset_SwingsWestAtDawnEastAtDusk()
        {
            // Use the real ShadowDirection so this guards the full chain dawn->west, dusk->east.
            Vector2 dawnDir = DayNightMath.ShadowDirection(Sunrise + 1f, Sunrise, Sunset, 0.2f, 0.9f);
            Vector2 duskDir = DayNightMath.ShadowDirection(Sunset - 1f, Sunrise, Sunset, 0.2f, 0.9f);
            float dawnElev = DayNightMath.SunElevation(Sunrise + 1f, Sunrise, Sunset);
            float duskElev = DayNightMath.SunElevation(Sunset - 1f, Sunrise, Sunset);

            Vector2 dawn = DayNightMath.ShadowSkewOffset(dawnDir, dawnElev, 2f, 0.4f, 6f, 100f);
            Vector2 dusk = DayNightMath.ShadowSkewOffset(duskDir, duskElev, 2f, 0.4f, 6f, 100f);
            Assert.Less(dawn.x, 0f, "dawn shadow offset runs WEST");
            Assert.Greater(dusk.x, 0f, "dusk shadow offset runs EAST");
        }

        // ---- ShadowAlpha -------------------------------------------------------------------------------

        [Test]
        public void ShadowAlpha_IsMaxAlphaTimesStrength_AndClamped()
        {
            Assert.AreEqual(0.3f, DayNightMath.ShadowAlpha(0.6f, 0.5f), Eps, "0.6 * 0.5 = 0.3");
            Assert.AreEqual(0f, DayNightMath.ShadowAlpha(0.6f, 0f), Eps, "no strength (night) -> invisible");
            Assert.AreEqual(1f, DayNightMath.ShadowAlpha(2f, 2f), Eps, "clamped to 1");
            Assert.AreEqual(0f, DayNightMath.ShadowAlpha(-1f, 0.5f), Eps, "negative maxAlpha clamps to 0");
        }

        [Test]
        public void ShadowAlpha_FadesWithTheSunAndWeather()
        {
            // Drive it through ShadowStrength: bright clear noon is darker than a cloudy noon, and night is 0.
            float noon = DayNightMath.ShadowAlpha(0.5f, DayNightMath.ShadowStrength(13f, Sunrise, Sunset, 0f, 0.85f));
            float cloudy = DayNightMath.ShadowAlpha(0.5f, DayNightMath.ShadowStrength(13f, Sunrise, Sunset, 0.6f, 0.85f));
            float night = DayNightMath.ShadowAlpha(0.5f, DayNightMath.ShadowStrength(2f, Sunrise, Sunset, 0f, 0.85f));
            Assert.Greater(noon, cloudy, "cloud softens the shadow");
            Assert.AreEqual(0f, night, Eps, "no shadow at night");
        }

        private static float Brightness(Color c) => c.r + c.g + c.b;
    }
}
