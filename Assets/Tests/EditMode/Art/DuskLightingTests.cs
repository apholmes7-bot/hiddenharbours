using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The dusk lighting pass (owner ruling 2026-11-20: dusk may be fixed with game lighting changes). The day tint
    /// held a single long blend from the warm key (~18:04) to night (22:00), so 19:00-21:00 read as a dark red. The
    /// profile now holds the warm key until <see cref="DayNightProfile.NightBlendHours"/> before night, and the
    /// water keeps its moon and stars off while the sun is up. Noon and night must not move.
    /// </summary>
    public class DuskLightingTests
    {
        const string ProfilePath = "Assets/_Project/Resources/DayNightProfile.asset";
        const string WaterShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";

        DayNightProfile _shipped;   // the code defaults (pinned equal to the asset by DayNightProfileAssetTests)
        DayNightProfile _legacy;    // the same profile with the blend covering the whole evening: the old curve

        [SetUp]
        public void SetUp()
        {
            _shipped = DayNightProfile.CreateDefault();
            _legacy = DayNightProfile.CreateDefault();
            typeof(DayNightProfile).GetField("_nightBlendHours", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_legacy, 100f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_shipped != null) Object.DestroyImmediate(_shipped);
            if (_legacy != null) Object.DestroyImmediate(_legacy);
        }

        static float Lum(Color c) => LightMath.Luminance(c);
        static Color Tint(float h, DayNightProfile p) => DayNightMath.DayNightTint(h, p, 1f, 0f);

        [Test]
        public void TheEvening_HoldsTheWarmKey_UntilTheNightBlendBegins()
        {
            float blendFrom = _shipped.NightHour - _shipped.NightBlendHours;
            float warm = _shipped.SunsetHour - _shipped.WarmKeyBeforeSunsetHours;
            Assert.AreEqual(0.75f, DayNightMath.TintPhase(warm, _shipped), 1e-5f, "the warm key still lands where the sun says");
            for (float h = warm + 0.1f; h < blendFrom - 1e-3f; h += 0.25f)
                Assert.AreEqual(0.75f, DayNightMath.TintPhase(h, _shipped), 1e-5f, $"{h:0.00}h holds the warm key");
            Assert.AreEqual(0.80f, DayNightMath.TintPhase(_shipped.NightHour, _shipped), 1e-5f, "night still starts at NightHour");
            float mid = (blendFrom + _shipped.NightHour) * 0.5f;
            Assert.AreEqual(0.775f, DayNightMath.TintPhase(mid, _shipped), 1e-4f, "halfway through the blend, halfway to night");
        }

        [Test]
        public void ALongBlend_IsTheOldCurveExactly()
        {
            float warm = _legacy.SunsetHour - _legacy.WarmKeyBeforeSunsetHours, night = _legacy.NightHour;
            for (float h = warm; h < night; h += 0.1f)
                Assert.AreEqual(Mathf.Lerp(0.75f, 0.80f, Mathf.InverseLerp(warm, night, h)),
                                DayNightMath.TintPhase(h, _legacy), 1e-6f, $"{h:0.00}h");
        }

        [Test]
        public void TheSummerEvening_IsGoldenNotDarkRed()
        {
            Color at20 = Tint(20f, _shipped);
            Assert.Greater(Lum(at20), 0.7f, "20:00 must read as evening light, not dusk dark (was ~0.43)");
            Assert.Greater(Lum(at20), Lum(Tint(20f, _legacy)) + 0.2f, "brighter than the old long blend at 20:00");
            Assert.Less(at20.r / at20.b, 2.2f, "the red-to-blue cast stays modest (was ~1.7 at 20:00 on a dark frame)");
            Assert.Greater(at20.r, at20.b, "…but still warm");
        }

        [Test]
        public void NoonAndNight_AreUnmoved()
        {
            foreach (float h in new[] { 6f, 9f, 12f, 13f, 15f, 17f, 22f, 22.5f, 23.5f, 0f, 2f, 4f })
                Assert.AreEqual(Tint(h, _legacy), Tint(h, _shipped), $"{h}h tint must be bit-identical to the old curve");
            for (float h = 0f; h < 24f; h += 0.25f)
                Assert.AreEqual(DayNightMath.IntensityPhase(h, _legacy), DayNightMath.IntensityPhase(h, _shipped), 0f,
                    $"{h}h brightness timing does not move");
        }

        [Test]
        public void TheEvening_OnlyEverDarkens()
        {
            float last = Lum(Tint(18f, _shipped));
            for (float h = 18.05f; h <= 24f; h += 0.05f)
            {
                float l = Lum(Tint(h, _shipped));
                Assert.LessOrEqual(l, last + 1e-5f, $"{h:0.00}h");
                last = l;
            }
        }

        [Test]
        public void TheShippedAsset_CarriesTheBlendAndTheSunsetColour()
        {
            var asset = AssetDatabase.LoadAssetAtPath<DayNightProfile>(ProfilePath);
            Assert.IsNotNull(asset, "the profile must ship");
            Assert.AreEqual(DayNightProfile.DefaultNightBlendHours, asset.NightBlendHours, 1e-5f);
            Color a = asset.SkyTint.Evaluate(0.75f), c = _shipped.SkyTint.Evaluate(0.75f);
            Assert.AreEqual(c.r, a.r, 1e-4f); Assert.AreEqual(c.g, a.g, 1e-4f); Assert.AreEqual(c.b, a.b, 1e-4f);
            for (float h = 18f; h <= 22f; h += 0.25f)
                Assert.AreEqual(Lum(Tint(h, _shipped)), Lum(Tint(h, asset)), 1e-4f, $"{h:0.00}h asset == code");
        }

        static DayNightProfile WithGlow(float strength)
        {
            var p = DayNightProfile.CreateDefault();
            typeof(DayNightProfile).GetField("_sunsetGlowStrength", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(p, strength);
            return p;
        }

        [Test]
        public void GlowZero_IsTheGoldenHoldExactly()
        {
            var noGlow = WithGlow(0f);
            try
            {
                for (float h = 0f; h < 24f; h += 0.05f)
                {
                    Assert.AreEqual(0f, DayNightMath.SunsetGlowWeight(h, noGlow), 0f, $"{h:0.00}h no weight");
                    if (DayNightMath.SunsetGlowWeight(h, _shipped) == 0f)
                        Assert.AreEqual(Tint(h, noGlow), Tint(h, _shipped), $"{h:0.00}h outside the glow: bit-identical");
                }
            }
            finally { Object.DestroyImmediate(noGlow); }
        }

        [Test]
        public void TheGlowWeight_RisesToSunsetAndFallsByNight()
        {
            float sunset = _shipped.SunsetHour, night = _shipped.NightHour, lead = _shipped.SunsetGlowLeadHours;
            Assert.AreEqual(0f, DayNightMath.SunsetGlowWeight(sunset - lead - 0.01f, _shipped), 0f, "nothing before the lead");
            Assert.AreEqual(_shipped.SunsetGlowStrength, DayNightMath.SunsetGlowWeight(sunset, _shipped), 1e-5f, "full at sunset");
            Assert.AreEqual(0f, DayNightMath.SunsetGlowWeight(night, _shipped), 1e-6f, "gone by night");
            foreach (float h in new[] { 6f, 12f, 17f, 18f, 19f, 22.5f, 0f })
                Assert.AreEqual(0f, DayNightMath.SunsetGlowWeight(h, _shipped), 0f, $"{h}h untouched");
            float last = 0f;
            for (float h = sunset - lead; h <= sunset; h += 0.05f)
            {
                float w = DayNightMath.SunsetGlowWeight(h, _shipped);
                Assert.GreaterOrEqual(w, last - 1e-6f, $"{h:0.00}h rises");
                last = w;
            }
        }

        [Test]
        public void TheSunset_ReadsRedderThanTheGoldenHold()
        {
            Color hold = Tint(19f, _shipped), sunset = Tint(21f, _shipped);
            Color.RGBToHSV(hold, out float hHold, out _, out _);
            Color.RGBToHSV(sunset, out float hSun, out float sSun, out _);
            Assert.Less(hSun, hHold - 0.02f, "21:00 leans from gold toward red-rose");
            Assert.Greater(sSun, 0.4f, "a sunset colour, not a grey haze");
            Assert.Greater(sunset.r, sunset.g * 1.5f, "red over green: no khaki");
        }

        [Test]
        public void TheShippedAsset_CarriesTheSunsetGlow()
        {
            var asset = AssetDatabase.LoadAssetAtPath<DayNightProfile>(ProfilePath);
            Assert.IsNotNull(asset, "the profile must ship");
            Assert.AreEqual(DayNightProfile.DefaultSunsetGlowStrength, asset.SunsetGlowStrength, 1e-5f);
            Assert.AreEqual(DayNightProfile.DefaultSunsetGlowLeadHours, asset.SunsetGlowLeadHours, 1e-5f);
            Color a = asset.SunsetGlowTint, c = DayNightProfile.DefaultSunsetGlowTint;
            Assert.AreEqual(c.r, a.r, 1e-4f); Assert.AreEqual(c.g, a.g, 1e-4f); Assert.AreEqual(c.b, a.b, 1e-4f);
            for (float h = 18f; h <= 22f; h += 0.25f)
            {
                Color x = Tint(h, _shipped), y = Tint(h, asset);
                Assert.AreEqual(x.r, y.r, 1e-4f, $"{h:0.00}h r"); Assert.AreEqual(x.g, y.g, 1e-4f, $"{h:0.00}h g"); Assert.AreEqual(x.b, y.b, 1e-4f, $"{h:0.00}h b");
            }
        }

        [Test]
        public void TheWaterNightGate_StaysOffWhileTheSunIsUp()
        {
            const float thr = 0.35f, soft = 0.3f, cut = WaterReflection.DefaultNightSunCutoff;
            float darkLum = 0.25f;   // a frame dark enough that the old gate reads full night
            Assert.Greater(WaterReflection.NightFactor(darkLum, thr, soft), 0.9f);
            Assert.AreEqual(0f, WaterReflection.NightFactor(darkLum, thr, soft, 0.29f, cut), 1e-6f, "20:00 sun: no moon or stars");
            Assert.AreEqual(0f, WaterReflection.NightFactor(darkLum, thr, soft, cut, cut), 1e-6f, "at the cutoff: none");
            float half = WaterReflection.NightFactor(darkLum, thr, soft, cut * 0.5f, cut);
            Assert.That(half, Is.GreaterThan(0f).And.LessThan(WaterReflection.NightFactor(darkLum, thr, soft)), "a soft fade near the horizon");
            foreach (float e in new[] { 0f, -0.2f, -1f })
                Assert.AreEqual(WaterReflection.NightFactor(darkLum, thr, soft), WaterReflection.NightFactor(darkLum, thr, soft, e, cut), 0f,
                    $"sun at/below the horizon ({e}): the darkness gate exactly");
            foreach (float e in new[] { 0f, 0.29f, 0.9f })
                Assert.AreEqual(WaterReflection.NightFactor(darkLum, thr, soft), WaterReflection.NightFactor(darkLum, thr, soft, e, 0f), 0f,
                    "cutoff 0: the old darkness-only gate exactly");
        }

        [Test]
        public void TheShaderDefault_MatchesTheTwin()
        {
            string src = File.ReadAllText(WaterShaderPath);
            Match m = Regex.Match(src, @"^\s*_NightSunCutoff\s*\(""[^""]*"",\s*Range\(0,\s*0\.5\)\)\s*=\s*([0-9.]+)", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, "_NightSunCutoff must be a water shader property");
            Assert.AreEqual(WaterReflection.DefaultNightSunCutoff, float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), 1e-6f);
            StringAssert.Contains("night *= 1.0 - smoothstep(0.0, _NightSunCutoff, _SunElevation);", src,
                "the shader applies the same gate as WaterReflection.NightFactor(…, sunElevation, sunCutoff)");
        }
    }
}
