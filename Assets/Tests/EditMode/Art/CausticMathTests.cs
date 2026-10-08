using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>Independent numeric examples plus the serialized C1 material contract.
    /// File reads deliberately require explicit authored values, rather than shader-default fallback.
    /// These cases can also execute against the real NUnit library without starting Unity.</summary>
    public class CausticMathTests
    {
        [TestCase("Water")]
        [TestCase("Water_DeepBlue")]
        [TestCase("Water_FoggySmother")]
        [TestCase("Water_GlassyCalm")]
        [TestCase("Water_NorthAtlantic")]
        [TestCase("Water_StirredBrown")]
        [TestCase("Water_StormGrey")]
        [TestCase("Water_Tropical")]
        [TestCase("Water_WarmShelter")]
        public void CausticActivation_AllPresetsRetainRuledDials(string name)
        {
            string path = "Assets/_Project/Art/Materials/" +
                          (name == "Water" ? "" : "WaterPresets/") + name + ".mat";
            string yaml = File.ReadAllText(path);
            AssertSerialized(yaml, "_CausticCurvatureBlend", 0.5f);
            AssertSerialized(yaml, "_CausticCurvatureStep", 0.5f);
            AssertSerialized(yaml, "_CausticCurvatureGain", 12f);
            AssertSerialized(yaml, "_CausticDayGate", 1f);
            // S1's accepted settings must survive this activation.
            AssertSerialized(yaml, "_AbsorptionBands", 6f);
            AssertSerialized(yaml, "_UseSeabedTex", 1f);
        }

        [TestCase(-0.4f, 3f, 1f, 0f)]
        [TestCase(0f, 3f, 1f, 0f)]
        [TestCase(0.25f, 3f, 1f, 0.25f)]
        [TestCase(1f, 3f, 1f, 1f)]
        [TestCase(2f, 3f, 1f, 1f)]
        [TestCase(-0.4f, 3f, 0f, 1f)]
        [TestCase(-0.4f, 3f, 0.5f, 0.5f)]
        [TestCase(-0.4f, 0f, 1f, 1f)]
        [TestCase(-0.4f, 0.001f, 1f, 1f)]
        [TestCase(-0.4f, 0.0011f, 1f, 0f)]
        [TestCase(-0.4f, 3f, -1f, 1f)]
        [TestCase(-0.4f, 3f, 2f, 0f)]
        public void CausticDayGate_FadesAtNight(float sun, float tintSum, float strength, float expected)
        {
            Assert.That(CausticMath.DayGate(sun, tintSum, strength), Is.EqualTo(expected).Within(1e-6f));
        }

        [TestCase(0f, 0f, 0f, 0f, 0f)]
        [TestCase(0.75f, 0.75f, 0.75f, 0.75f, 0.75f)]
        [TestCase(1f, 1.5f, 0.5f, 1.25f, 0.75f)] // tilted plane, still no curvature
        public void CausticCurvature_FlatWaveHasNoFocusedContribution(
            float centre, float positiveX, float negativeX, float positiveY, float negativeY)
        {
            Assert.That(CausticMath.FocusedBrightness(centre, positiveX, negativeX,
                positiveY, negativeY, 0.5f, 12f), Is.EqualTo(0f));
        }

        [Test]
        public void CausticCurvature_ConvexSignSpacingGainAndClampMatchNumericExamples()
        {
            // Numerator -1/64; spacing squared 1/4; negative Laplacian 1/16; times 12 = 3/4.
            Assert.That(CausticMath.FocusedBrightness(1f, 1f, 1f, 0.984375f, 1f, 0.5f, 12f), Is.EqualTo(0.75f));
            // Double spacing divides curvature by four, and gain scales before saturation.
            Assert.That(CausticMath.FocusedBrightness(1f, 1f, 1f, 0.984375f, 1f, 1f, 12f), Is.EqualTo(0.1875f));
            Assert.That(CausticMath.FocusedBrightness(1f, 1f, 1f, 0.984375f, 1f, 0.5f, 6f), Is.EqualTo(0.375f));
            Assert.That(CausticMath.FocusedBrightness(1f, 1f, 1f, 1.015625f, 1f, 0.5f, 12f), Is.EqualTo(0f));
            Assert.That(CausticMath.FocusedBrightness(1f, 0f, 0f, 0f, 0f, 0.5f, 12f), Is.EqualTo(1f));
            Assert.That(CausticMath.FocusedBrightness(1f, 0f, 0f, 0f, 0f, 0.5f, -12f), Is.EqualTo(0f));
            Assert.That(CausticMath.FocusedBrightness(1e-8f, 0f, 0f, 0f, 0f, 0f, 12f), Is.EqualTo(0.48f).Within(1e-6f));
        }

        [Test]
        public void CausticCurvature_BlendThresholdAndZeroEnergyFallbackStayDistinct()
        {
            Assert.That(CausticMath.BlendWithField(0.8f, 0.2f, 0f, 1f), Is.EqualTo(0.8f));
            Assert.That(CausticMath.BlendWithField(0.8f, 0.2f, 0.001f, 1f), Is.EqualTo(0.8f));
            Assert.That(CausticMath.BlendWithField(0.8f, 0f, 0.5f, 0f), Is.EqualTo(0.8f));
            // Half blend times half-live envelope: 0.8 + (0.2 - 0.8) / 4 = 0.65.
            Assert.That(CausticMath.BlendWithField(0.8f, 0.2f, 0.5f, 0.0125f), Is.EqualTo(0.65f).Within(1e-6f));
            Assert.That(CausticMath.BlendWithField(0.8f, 0.2f, 0.5f, 0.025f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(CausticMath.BlendWithField(0.8f, 0.2f, 2f, 0.025f), Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(CausticMath.BlendWithField(0.8f, 0f, 1f, 0.025f), Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(6f)]
        public void CausticTransmission_IsMonotoneInDepthAndTurbidity(float bands)
        {
            // At d = 0, exp(0) = 1 on every channel (also exactly 6/6 when banded).
            // Both sweeps start above the inactive-sigma threshold; increasing sigma or 2d
            // cannot increase transmission, including the plateaus introduced by six bands.
            foreach (float clarity in new[] { 0.5f, 1f })
            {
                Vector3 previousDepth = Vector3.one;
                Vector3 previousTurbidity = Vector3.one;
                for (int i = 0; i <= 100; i++)
                {
                    Vector3 byDepth = CausticMath.ClarityTransmission(
                        WaterAbsorption.Sigma(3f, WaterAbsorption.DefaultRatio), i * 0.1f, clarity, bands);
                    Vector3 byTurbidity = CausticMath.ClarityTransmission(
                        WaterAbsorption.Sigma(0.01f + i * 0.03f, WaterAbsorption.DefaultRatio),
                        0.5f, clarity, bands);
                    for (int channel = 0; channel < 3; channel++)
                    {
                        if (i == 0) Assert.That(byDepth[channel], Is.EqualTo(1f));
                        Assert.That(byDepth[channel], Is.InRange(0f, previousDepth[channel]));
                        Assert.That(byTurbidity[channel], Is.InRange(0f, previousTurbidity[channel]));
                    }
                    previousDepth = byDepth;
                    previousTurbidity = byTurbidity;
                }
            }
        }

        [Test]
        public void CausticTransmission_ZeroStrengthIsPassthrough()
        {
            // Exactly IEEE-754 1.0 (0x3f800000), with no tolerance, for every output channel.
            foreach (float depth in new[] { -1f, 0f, 0.5f, 100f })
            foreach (float bands in new[] { 0f, 6f })
            {
                foreach (Vector3 sigma in new[] { Vector3.zero, new Vector3(3f, 0.54f, 0.24f),
                    new Vector3(100f, 100f, 100f) })
                    AssertOneBits(CausticMath.ClarityTransmission(sigma, depth, 0f, bands));
                foreach (float clarity in new[] { -1f, 0f, 0.5f, 1f, 2f })
                foreach (Vector3 sigma in new[] { Vector3.zero, new Vector3(0.00002f, 0.00002f, 0.00002f),
                    new Vector3(0.0001f, 0f, 0f) })
                    AssertOneBits(CausticMath.ClarityTransmission(sigma, depth, clarity, bands));
            }
        }

        [TestCase(1f, 0f, 0.04978707f, 0.58274825f, 0.78662786f)]
        [TestCase(0.5f, 0f, 0.52489353f, 0.79137413f, 0.89331393f)]
        [TestCase(1f, 6f, 0f, 0.5f, 0.83333333f)]
        [TestCase(0.5f, 6f, 0.5f, 0.75f, 0.91666667f)]
        public void CausticTransmission_StirredBrownMatchesWorkedNumbers(
            float clarity, float bands, float red, float green, float blue)
        {
            // Turbidity 3 * (1, .18, .08) = (3, .54, .24); at .5 m, path = 1 m.
            // exp(-3, -.54, -.24) gives the first row. Round(T*6)/6 gives (0, 3/6, 5/6).
            // The half-strength rows average those independent numbers with (1, 1, 1).
            Vector3 actual = CausticMath.ClarityTransmission(
                WaterAbsorption.Sigma(3f, WaterAbsorption.DefaultRatio), 0.5f, clarity, bands);
            Assert.That(actual.x, Is.EqualTo(red).Within(1e-6f));
            Assert.That(actual.y, Is.EqualTo(green).Within(1e-6f));
            Assert.That(actual.z, Is.EqualTo(blue).Within(1e-6f));
        }

        [Test]
        public void CausticTransmission_ClampsStrengthAndNegativeDepth()
        {
            var sigma = new Vector3(3f, 0.54f, 0.24f);
            AssertOneBits(CausticMath.ClarityTransmission(sigma, 0.5f, -1f, 6f));
            AssertOneBits(CausticMath.ClarityTransmission(sigma, -1f, 1f, 6f));
            Vector3 full = CausticMath.ClarityTransmission(sigma, 0.5f, 2f, 6f);
            Assert.That(full.x, Is.EqualTo(0f));
            Assert.That(full.y, Is.EqualTo(0.5f));
            Assert.That(full.z, Is.EqualTo(5f / 6f).Within(1e-6f));
        }

        [TestCase("Water")]
        [TestCase("Water_DeepBlue")]
        [TestCase("Water_FoggySmother")]
        [TestCase("Water_GlassyCalm")]
        [TestCase("Water_NorthAtlantic")]
        [TestCase("Water_StirredBrown")]
        [TestCase("Water_StormGrey")]
        [TestCase("Water_Tropical")]
        [TestCase("Water_WarmShelter")]
        public void CausticClarity_AllMaterialsSerializeZero(string name)
        {
            string path = "Assets/_Project/Art/Materials/" +
                          (name == "Water" ? "" : "WaterPresets/") + name + ".mat";
            AssertSerialized(File.ReadAllText(path), "_CausticClarity", 0f);
        }

        [Test]
        public void CausticTransmission_ShaderUsesSharedLawInsideCausticGate()
        {
            string source = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursWater.shader");
            string compact = Regex.Replace(source, @"\s+", "");
            string block = compact.Substring(compact.IndexOf("if(causticGate>0.001&&_CausticAmount>0.001)"));
            block = block.Substring(0, block.IndexOf("//----layer4specularglints"));
            StringAssert.Contains("float3causticTransmission=float3(1.0,1.0,1.0);" +
                "if(_CausticClarity>0.0){float3causticSigma=AbsorptionSigma();" +
                "if(dot(causticSigma,float3(1.0,1.0,1.0))>ABSORPTION_EPS){" +
                "float3causticT=AbsorptionTransmission(causticSigma,depth);" +
                "causticT=AbsorptionBand(causticT,_AbsorptionBands);" +
                "causticTransmission=lerp(float3(1.0,1.0,1.0),causticT,saturate(_CausticClarity));}}", block);
            StringAssert.Contains("col.rgb+=_CausticColor.rgb*caustic*_CausticAmount*causticGate*causticDay*causticTransmission;", block);
            StringAssert.Contains("float_CausticClarity;", compact);
            StringAssert.Contains("_CausticClarity(\"Causticclarity(0=off,1=seabedtransmission)\",Range(0,1))=0.0", compact);
        }

        private static void AssertOneBits(Vector3 actual)
        {
            for (int channel = 0; channel < 3; channel++)
                Assert.That(System.BitConverter.ToInt32(System.BitConverter.GetBytes(actual[channel]), 0),
                    Is.EqualTo(0x3f800000));
        }

        private static void AssertSerialized(string yaml, string key, float expected)
        {
            var matches = Regex.Matches(yaml, @"(?m)^    - " + Regex.Escape(key) + @": ([^\r\n]+)");
            Assert.That(matches.Count, Is.EqualTo(1), key + " must be serialized exactly once");
            float actual = float.Parse(matches[0].Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(actual, Is.EqualTo(expected), key);
        }
    }
}
