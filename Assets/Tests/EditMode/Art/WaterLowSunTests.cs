using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The water's LOW SUN layer (the sheen, the hue hold and the sun sparkles): the pure twins in
    /// <see cref="WaterLowSun"/>, and pins on the shader source and the shipped material so the two cannot
    /// drift. The rule that matters most: at high sun, at night and with no day/night cycle the layer is an
    /// exact passthrough, so the approved noon sea cannot move.
    /// </summary>
    public class WaterLowSunTests
    {
        const string WaterShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const string WaterMaterialPath = "Assets/_Project/Art/Materials/Water.mat";

        // The shipped window (Water.mat _LowSunWindow) and a live sunset / noon / night tint.
        const float BelowEnd = -0.2f, HighStart = 0.35f, HighEnd = 0.5f, FullSat = 0.4f;
        static readonly Color Sunset = new Color(0.858f, 0.536f, 0.433f);
        static readonly Color Noon = new Color(0.993f, 0.987f, 0.968f);
        static readonly Color Night = new Color(0.12f, 0.16f, 0.30f);
        static readonly Color Near = new Color(1f, 0.72f, 0.42f), Far = new Color(0.62f, 0.40f, 0.50f);

        static float Weight(float elevation, Color tint) =>
            WaterLowSun.Weight(elevation, tint, BelowEnd, HighStart, HighEnd);

        // ---- the window -------------------------------------------------------------------------------

        [Test]
        public void Weight_IsExactlyZero_AtAndAboveTheHighSunEnd()
        {
            for (float e = HighEnd; e <= 1.0001f; e += 0.01f)
                Assert.AreEqual(0f, Weight(e, Noon), $"the high-sun day is untouched (elevation {e})");
        }

        [Test]
        public void Weight_IsExactlyZero_WellBelowTheHorizon()
        {
            for (float e = -1f; e <= BelowEnd; e += 0.01f)
                Assert.AreEqual(0f, Weight(e, Night), $"the night is untouched (elevation {e})");
        }

        [Test]
        public void Weight_IsFull_FromTheHorizonToTheFadeStart()
        {
            for (float e = 0f; e <= HighStart; e += 0.01f)
                Assert.AreEqual(1f, Weight(e, Sunset), 1e-6f, $"the low sun is in full (elevation {e})");
        }

        [Test]
        public void Weight_IsZero_WhileNoDayNightCycleRuns()
        {
            Assert.AreEqual(0f, Weight(0.1f, new Color(0f, 0f, 0f, 0f)));
        }

        [Test]
        public void Weight_StaysAWeight_AndHasNoStep()
        {
            float prev = Weight(-1f, Sunset);
            for (float e = -1f; e <= 1f; e += 0.002f)
            {
                float w = Weight(e, Sunset);
                Assert.That(w, Is.InRange(0f, 1f));
                Assert.Less(Mathf.Abs(w - prev), 0.03f, $"no pop as the sun moves (elevation {e})");
                prev = w;
            }
        }

        // ---- the sheen --------------------------------------------------------------------------------

        [Test]
        public void Colourful_IsZeroForAGreyTint_AndFullAtSunset()
        {
            Assert.AreEqual(0f, WaterLowSun.Colourful(new Color(0.5f, 0.5f, 0.5f), FullSat));
            Assert.AreEqual(0f, WaterLowSun.Colourful(new Color(0f, 0f, 0f), FullSat));
            Assert.AreEqual(1f, WaterLowSun.Colourful(Sunset, FullSat), 1e-6f);
            Assert.Less(WaterLowSun.Colourful(Noon, FullSat), 0.1f, "a near-white noon tint carries almost none");
        }

        [Test]
        public void Bright_IsTheLightsStrongestChannel_SoADarkDuskGlowsLittle()
        {
            Assert.AreEqual(0.858f, WaterLowSun.Bright(Sunset), 1e-6f);
            Assert.AreEqual(0f, WaterLowSun.Bright(new Color(0f, 0f, 0f)));
            Assert.AreEqual(1f, WaterLowSun.Bright(new Color(1.4f, 1f, 1f)), "a weight, never a gain");
            // A storm's dusk (dark, nearly grey) and the blue hour (dark, coloured) against a clear sunset.
            Color storm = new Color(0.17f, 0.16f, 0.18f);
            float sunset = WaterLowSun.Colourful(Sunset, FullSat) * WaterLowSun.Bright(Sunset);
            Assert.Less(WaterLowSun.Colourful(storm, FullSat) * WaterLowSun.Bright(storm), sunset * 0.1f);
            Assert.Less(WaterLowSun.Colourful(Night, FullSat) * WaterLowSun.Bright(Night), sunset * 0.4f);
        }

        [Test]
        public void Shader_SheenAmount_IsWeightTimesColourTimesBrightness()
        {
            string src = File.ReadAllText(WaterShaderPath);
            StringAssert.Contains("float amount = max(_LowSunSheen.x, 0.0) * weight * LowSunColourful() * LowSunBright()", src);
            StringAssert.Contains("* (1.0 - saturate(_RainIntensity));", src);
            Assert.AreEqual(1f, WaterLowSun.Clear(0f));
            Assert.AreEqual(0.25f, WaterLowSun.Clear(0.75f), 1e-6f);
            Assert.AreEqual(0f, WaterLowSun.Clear(1f), "no glowing sky at full rain");
            Assert.AreEqual(0f, WaterLowSun.Clear(3f));
            StringAssert.Contains("return saturate(max(_DayNightTint.r, max(_DayNightTint.g, _DayNightTint.b)));", src);
        }

        [Test]
        public void Sheen_IsExactlyNothing_AtWeightZero_AtStrengthZero_AndUnderAGreyTint()
        {
            Assert.AreEqual(Vector3.zero, WaterLowSun.Sheen(Near, Far, 0.3f, 0.5f, 0.8f, 0.5f, 1f, 0f, 0f, 1f, Noon));
            Assert.AreEqual(Vector3.zero, WaterLowSun.Sheen(Near, Far, 0f, 0.5f, 0.8f, 0.5f, 1f, 0f, 1f, 1f, Sunset));
            Assert.AreEqual(Vector3.zero, WaterLowSun.Sheen(Near, Far, 0.3f, 0.5f, 0.8f, 0.5f, 1f, 0f, 1f, 0f, Sunset));
        }

        [Test]
        public void Sheen_OnScreen_IsTheAuthoredSkyColour_OnceTheOverlayHasTintedIt()
        {
            // Level water, outside the lane, full weight: on screen = sky colour * strength.
            Vector3 nearAdd = WaterLowSun.Sheen(Near, Far, 0.3f, 0.5f, 0.8f, 1f, 0f, 0f, 1f, 1f, Sunset);
            Assert.AreEqual(Near.r * 0.3f, nearAdd.x * Sunset.r, 1e-5f);
            Assert.AreEqual(Near.g * 0.3f, nearAdd.y * Sunset.g, 1e-5f);
            Assert.AreEqual(Near.b * 0.3f, nearAdd.z * Sunset.b, 1e-5f);
            Vector3 farAdd = WaterLowSun.Sheen(Near, Far, 0.3f, 0.5f, 0.8f, 0f, 0f, 0f, 1f, 1f, Sunset);
            Assert.AreEqual(Far.b * 0.3f, farAdd.z * Sunset.b, 1e-5f);
        }

        [Test]
        public void Sheen_IsBrighterOnFacesTippedToTheSun_AndInTheLaneTowardTheSun()
        {
            float lit = WaterLowSun.Sheen(Near, Far, 0.3f, 0.5f, 0.8f, 0.5f, 0f, 1f, 1f, 1f, Sunset).x;
            float level = WaterLowSun.Sheen(Near, Far, 0.3f, 0.5f, 0.8f, 0.5f, 0f, 0f, 1f, 1f, Sunset).x;
            float away = WaterLowSun.Sheen(Near, Far, 0.3f, 0.5f, 0.8f, 0.5f, 0f, -1f, 1f, 1f, Sunset).x;
            Assert.Greater(lit, level);
            Assert.Greater(level, away);
            Assert.Greater(away, 0f, "a face tipped away keeps some sky at the shipped relief");
            float inLane = WaterLowSun.Sheen(Near, Far, 0.3f, 0.5f, 0.8f, 1f, 1f, 0f, 1f, 1f, Sunset).x;
            float offLane = WaterLowSun.Sheen(Near, Far, 0.3f, 0.5f, 0.8f, 1f, 0f, 0f, 1f, 1f, Sunset).x;
            Assert.AreEqual(1.8f, inLane / offLane, 1e-4f, "the lane lifts the sheen by its dial toward the sun");
        }

        [Test]
        public void SheenToward_IsHalfAtTheViewer_AndClampsAtTheReach()
        {
            Assert.AreEqual(0.5f, WaterLowSun.SheenToward(0f, 14f));
            Assert.AreEqual(1f, WaterLowSun.SheenToward(14f, 14f));
            Assert.AreEqual(1f, WaterLowSun.SheenToward(50f, 14f));
            Assert.AreEqual(0f, WaterLowSun.SheenToward(-14f, 14f));
        }

        [Test]
        public void HullWaveDilute_IsExactlyOneWithNoSheen_AndTheSeasShareWithOne()
        {
            Assert.AreEqual(1f, WaterLowSun.HullWaveDilute(0.2f, 0f, 0f));
            Assert.AreEqual(1f, WaterLowSun.HullWaveDilute(0f, 0f, 0.5f), "a black sea with no sheen is still a passthrough");
            Assert.AreEqual(0.4f, WaterLowSun.HullWaveDilute(0.2f, 0.3f, 0f), 1e-6f);
            Assert.AreEqual(0.7f, WaterLowSun.HullWaveDilute(0.2f, 0.3f, 0.5f), 1e-6f);
            Assert.AreEqual(1f, WaterLowSun.HullWaveDilute(0.2f, 0.3f, 1f), 1e-6f);
        }

        // ---- the hue hold -----------------------------------------------------------------------------

        [Test]
        public void HueHoldFactor_IsExactlyOne_AtShareZero()
        {
            foreach (Color tint in new[] { Sunset, Noon, Night })
                Assert.AreEqual(Vector3.one, WaterLowSun.HueHoldFactor(tint, 0f));
        }

        [Test]
        public void HueHoldFactor_AtFullShare_LeavesOnlyTheTintsBrightness()
        {
            Vector3 f = WaterLowSun.HueHoldFactor(Sunset, 1f);
            float grey = Sunset.r * 0.299f + Sunset.g * 0.587f + Sunset.b * 0.114f;
            Assert.AreEqual(grey, f.x * Sunset.r, 1e-5f);
            Assert.AreEqual(grey, f.y * Sunset.g, 1e-5f);
            Assert.AreEqual(grey, f.z * Sunset.b, 1e-5f);
        }

        [Test]
        public void HueHoldFactor_KeepsTheTintsBrightness_AtEveryShare()
        {
            float grey = Sunset.r * 0.299f + Sunset.g * 0.587f + Sunset.b * 0.114f;
            for (float s = 0f; s <= 1f; s += 0.1f)
            {
                Vector3 f = WaterLowSun.HueHoldFactor(Sunset, s);
                float got = f.x * Sunset.r * 0.299f + f.y * Sunset.g * 0.587f + f.z * Sunset.b * 0.114f;
                Assert.AreEqual(grey, got, 1e-5f, $"the hold moves hue, never brightness (share {s})");
            }
        }

        // ---- the warm light above the low sun ---------------------------------------------------------

        // The shipped _WarmLight saturation edges, and the 18:30 tint of day 59 at St Peters (measured).
        const float WarmStart = 0.1f, WarmFull = 0.4f;
        static readonly Color GoldenHour = new Color(1f, 0.75f, 0.56f);
        static readonly Color ShippedNoon = new Color(0.897f, 0.904f, 0.904f);

        [Test]
        public void WarmLightWeight_IsExactlyZero_AtNoon_ForAGreyLight_AndWithNoCycle()
        {
            Assert.AreEqual(0f, WaterLowSun.WarmLightWeight(Noon, WarmStart, WarmFull));
            Assert.AreEqual(0f, WaterLowSun.WarmLightWeight(ShippedNoon, WarmStart, WarmFull));
            Assert.AreEqual(0f, WaterLowSun.WarmLightWeight(new Color(0.5f, 0.5f, 0.5f), WarmStart, WarmFull));
            Assert.AreEqual(0f, WaterLowSun.WarmLightWeight(Color.clear, WarmStart, WarmFull));
        }

        [Test]
        public void WarmLightWeight_IsFull_UnderTheGoldenHourLight_AndRisesWithoutAStep()
        {
            Assert.AreEqual(1f, WaterLowSun.WarmLightWeight(GoldenHour, WarmStart, WarmFull), 1e-6f);
            Assert.AreEqual(1f, WaterLowSun.WarmLightWeight(Sunset, WarmStart, WarmFull), 1e-6f);
            float last = 0f;
            for (float b = 1f; b >= 0.5f; b -= 0.01f)
            {
                float w = WaterLowSun.WarmLightWeight(new Color(1f, 1f, b), WarmStart, WarmFull);
                Assert.GreaterOrEqual(w, last, "the weight only rises as the light warms");
                Assert.Less(w - last, 0.1f, "no step");
                last = w;
            }
        }

        [Test]
        public void WarmLight_AddsNothingInsideTheLowSunWindow_SoTheApprovedDuskCannotMove()
        {
            float outside = WaterLowSun.WarmLightOutsideWindow(1f, 1f);
            Assert.AreEqual(0f, outside);
            Assert.AreEqual(0.85f, WaterLowSun.MirrorSkyTintShare(0.85f, 0.7f, outside));
            Assert.AreEqual(0.4f, WaterLowSun.HueHoldShare(0.4f, 1f, 0.6f, outside));
        }

        [Test]
        public void WarmLight_AtWeightZero_IsTheShippedMirrorAndHold_BitForBit()
        {
            foreach (float tintShare in new[] { 0f, 0.7f, 0.85f, 0.9f, 1f })
                Assert.AreEqual(tintShare, WaterLowSun.MirrorSkyTintShare(tintShare, 0.7f, 0f));
            Assert.AreEqual(0f, WaterLowSun.HueHoldShare(0.4f, 0f, 0.6f, 0f));
        }

        [Test]
        public void WarmLight_AboveTheWindow_TheMirrorKeepsItsSky_AndTheSeaKeepsItsHue()
        {
            float outside = WaterLowSun.WarmLightOutsideWindow(1f, 0f);
            Assert.AreEqual(1f, outside);
            Assert.AreEqual(0.85f * 0.3f, WaterLowSun.MirrorSkyTintShare(0.85f, 0.7f, outside), 1e-6f);
            Assert.AreEqual(0.6f, WaterLowSun.HueHoldShare(0.4f, 0f, 0.6f, outside), 1e-6f);
            // Under the golden-hour light the held sea comes out bluer than the light alone would leave it.
            Vector3 f = WaterLowSun.HueHoldFactor(GoldenHour, 0.6f);
            Assert.Greater(f.z, 1f);
            Assert.Less(f.x, 1f);
        }

        [Test]
        public void ShippedMaterial_CarriesTheWarmLight_AndNoonSitsBelowIt()
        {
            string mat = File.ReadAllText(WaterMaterialPath);
            Match w = Regex.Match(mat, @"(?m)^    - _WarmLight: \{r: ([-0-9.]+), g: ([-0-9.]+), b: ([-0-9.]+), a: ([-0-9.]+)\}$");
            Assert.IsTrue(w.Success, "Water.mat carries _WarmLight");
            float hold = float.Parse(w.Groups[1].Value, CultureInfo.InvariantCulture);
            float keep = float.Parse(w.Groups[2].Value, CultureInfo.InvariantCulture);
            float start = float.Parse(w.Groups[3].Value, CultureInfo.InvariantCulture);
            float full = float.Parse(w.Groups[4].Value, CultureInfo.InvariantCulture);
            Assert.That(hold, Is.InRange(0f, 1f));
            Assert.That(keep, Is.InRange(0f, 1f));
            Assert.Greater(hold + keep, 0f, "the warm light is on in the shipped sea");
            Assert.Less(start, full);
            Assert.AreEqual(WarmStart, start, 1e-6f);
            Assert.AreEqual(WarmFull, full, 1e-6f);
            Assert.AreEqual(0f, WaterLowSun.WarmLightWeight(ShippedNoon, start, full), "noon cannot move");
        }

        [Test]
        public void Shader_WarmLight_DefaultsOff_AndIsSetBeforeTheMirror()
        {
            string src = File.ReadAllText(WaterShaderPath);
            float[] d = ShaderVector(src, "_WarmLight");
            Assert.AreEqual(0f, d[0], "the hold defaults to off");
            Assert.AreEqual(0f, d[1], "the mirror's sky defaults to the shipped share");
            int set = src.IndexOf("hhWarmLight = WarmLightWeight() * (1.0 - lowSun);", System.StringComparison.Ordinal);
            int mirror = src.IndexOf("col.rgb += SkyReflection(worldXY, waveSlope, surf, swellCrest, t);", System.StringComparison.Ordinal);
            Assert.That(set, Is.GreaterThan(0));
            Assert.That(mirror, Is.GreaterThan(set), "the weight is set before the mirror reads it");
            StringAssert.Contains("float skyTintShare = saturate(_ReflectionSkyTint) * (1.0 - saturate(_WarmLight.y) * hhWarmLight);", src);
            StringAssert.Contains("return smoothstep(_WarmLight.z, max(_WarmLight.w, _WarmLight.z + 1e-4), sat);", src);
        }

        // ---- the sparkles -----------------------------------------------------------------------------

        [Test]
        public void SparkleLitShare_PutsMoreGlintsInTheLaneTowardTheSun_NeverABrighterOne()
        {
            float outside = WaterLowSun.SparkleLitShare(0.45f, 0.06f, 0f, 0.5f, 0.5f);
            float laneFar = WaterLowSun.SparkleLitShare(0.45f, 0.06f, 1f, 0f, 0.5f);
            float laneNear = WaterLowSun.SparkleLitShare(0.45f, 0.06f, 1f, 1f, 0.5f);
            Assert.Greater(laneFar, outside);
            Assert.Greater(laneNear, laneFar);
            Assert.AreEqual(0.45f, laneNear, 1e-6f, "the lane's sunward end holds the cell share itself");
            Assert.That(WaterLowSun.SparkleLitShare(1f, 1f, 1f, 1f, 1f), Is.InRange(0f, 1f));
        }

        [Test]
        public void Sparkle_LivesInThreeSteps_ShortLongShort()
        {
            Assert.IsFalse(WaterLowSun.SparkleLit(0f));
            Assert.IsFalse(WaterLowSun.SparkleLit(0.99f));
            Assert.IsTrue(WaterLowSun.SparkleLit(0.1f));
            Assert.AreEqual(0f, WaterLowSun.SparkleLength(0.1f));
            Assert.AreEqual(0.5f, WaterLowSun.SparkleLength(0.25f));
            Assert.AreEqual(1f, WaterLowSun.SparkleLength(0.5f));
            Assert.AreEqual(0.5f, WaterLowSun.SparkleLength(0.75f));
            Assert.AreEqual(0f, WaterLowSun.SparkleLength(0.9f));
            for (float l = 0f; l < 1f; l += 0.01f)
                Assert.That(WaterLowSun.SparkleLength(l), Is.EqualTo(0f).Or.EqualTo(0.5f).Or.EqualTo(1f));
        }

        // ---- the shader source and the shipped material ------------------------------------------------

        static float[] ShaderVector(string src, string name)
        {
            Match m = Regex.Match(src, @"^\s*" + name + @"\s*\(""[^""]*"",\s*(?:Vector|Color)\)\s*=\s*\(([^)]*)\)",
                RegexOptions.Multiline);
            Assert.IsTrue(m.Success, name + " is declared in the shader's Properties");
            string[] p = m.Groups[1].Value.Split(',');
            var v = new float[p.Length];
            for (int i = 0; i < p.Length; i++) v[i] = float.Parse(p[i].Trim(), CultureInfo.InvariantCulture);
            return v;
        }

        static float ShaderFloat(string src, string name)
        {
            Match m = Regex.Match(src, @"^\s*" + name + @"\s*\(""[^""]*"",\s*Range\([^)]*\)\)\s*=\s*([-0-9.]+)",
                RegexOptions.Multiline);
            Assert.IsTrue(m.Success, name + " is declared in the shader's Properties");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        [Test]
        public void ShaderDefaults_AreThePassthrough()
        {
            string src = File.ReadAllText(WaterShaderPath);
            Assert.AreEqual(0f, ShaderVector(src, "_LowSunSheen")[0], "the sheen's strength defaults to off");
            Assert.AreEqual(0f, ShaderFloat(src, "_LowSunHueHold"));
            Assert.AreEqual(0f, ShaderFloat(src, "_LowSunWakeHold"));
            Assert.AreEqual(0f, ShaderFloat(src, "_SunGlitterForm"));
            Assert.AreEqual(1f, ShaderFloat(src, "_SunGlitterSpark"));
        }

        [Test]
        public void Shader_AddsTheSheenUnderTheFoam_AndHoldsTheHueAfterTheGrade()
        {
            string src = File.ReadAllText(WaterShaderPath);
            int mirror = src.IndexOf("col.rgb += SkyReflection(worldXY, waveSlope, surf, swellCrest, t);", System.StringComparison.Ordinal);
            int sheen = src.IndexOf("col.rgb += lowSunSheen;", System.StringComparison.Ordinal);
            int content = src.IndexOf("---- SKY CONTENT: drifting CLOUDS", System.StringComparison.Ordinal);
            int lowLight = src.IndexOf("col.rgb *= 1.0 + hullWaveLowLight * lowSunDilute;", System.StringComparison.Ordinal);
            int hold = src.IndexOf("col.rgb *= LowSunHueHoldFactor(_LowSunHueHold * lowSun + saturate(_WarmLight.x) * hhWarmLight);", System.StringComparison.Ordinal);
            Assert.That(mirror, Is.GreaterThan(0));
            Assert.That(sheen, Is.GreaterThan(mirror), "the sheen is laid with the sky mirror, under the foam");
            Assert.That(content, Is.GreaterThan(sheen));
            Assert.That(lowLight, Is.GreaterThan(content), "the hull waves' low-light shade is scaled by the sheen's lift");
            Assert.That(hold, Is.GreaterThan(lowLight), "the hue hold is the last word on the sea, after the grade");
        }

        [Test]
        public void Shader_UsesTheTwinsConstants()
        {
            string src = File.ReadAllText(WaterShaderPath);
            StringAssert.Contains("#define DN_COMP_MIN_CHANNEL 0.02", src);
            Assert.AreEqual(0.02f, WaterLowSun.MinTintChannel);
            StringAssert.Contains("float  grow  = pulse > 0.66 ? 1.0 : (pulse > 0.25 ? 0.5 : 0.0);", src);
            StringAssert.Contains("return dash * step(0.08, pulse);", src);
            Assert.AreEqual(0.66f, WaterLowSun.SparkleFullAbove);
            Assert.AreEqual(0.25f, WaterLowSun.SparkleHalfAbove);
            Assert.AreEqual(0.08f, WaterLowSun.SparkleLitAbove);
        }

        [Test]
        public void ShippedMaterial_CarriesTheLowSunLook_AndItsWindowEndsWhereTheSunGlitterEnds()
        {
            string mat = File.ReadAllText(WaterMaterialPath);
            Match w = Regex.Match(mat, @"(?m)^    - _LowSunWindow: \{r: ([-0-9.]+), g: ([-0-9.]+), b: ([-0-9.]+), a: ([-0-9.]+)\}$");
            Assert.IsTrue(w.Success, "Water.mat carries _LowSunWindow");
            float below = float.Parse(w.Groups[1].Value, CultureInfo.InvariantCulture);
            float start = float.Parse(w.Groups[2].Value, CultureInfo.InvariantCulture);
            float end = float.Parse(w.Groups[3].Value, CultureInfo.InvariantCulture);
            Assert.Less(below, 0f);
            Assert.AreEqual(WaterReflection.SunGlitterFallStart, start, 1e-6f, "one golden-hour window for the sea");
            Assert.AreEqual(WaterReflection.SunGlitterFallEnd, end, 1e-6f);
            // The sun at noon in the shipped day sits far above the window: noon cannot move.
            Assert.AreEqual(0f, WaterLowSun.Weight(0.957f, Noon, below, start, end));
            Assert.AreEqual(1, Regex.Matches(mat, @"(?m)^    - _LowSunSheen: \{r: 0\.35, g: 0\.5, b: 3, a: 0\.8\}$").Count);
            Assert.AreEqual(1, Regex.Matches(mat, @"(?m)^    - _LowSunHueHold: 0\.4$").Count);
            Assert.AreEqual(1, Regex.Matches(mat, @"(?m)^    - _SunGlitterForm: 1$").Count);
        }
    }
}
