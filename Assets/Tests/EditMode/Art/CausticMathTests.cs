using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using NUnit.Framework;

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

        private static void AssertSerialized(string yaml, string key, float expected)
        {
            var matches = Regex.Matches(yaml, @"(?m)^    - " + Regex.Escape(key) + @": ([^\r\n]+)");
            Assert.That(matches.Count, Is.EqualTo(1), key + " must be serialized exactly once");
            float actual = float.Parse(matches[0].Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(actual, Is.EqualTo(expected), key);
        }
    }
}
