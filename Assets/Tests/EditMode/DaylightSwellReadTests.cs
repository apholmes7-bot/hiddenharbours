using System;
using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Headless arithmetic and wiring guards. GPU execution is a separate, named proof.</summary>
    public class DaylightSwellReadTests
    {
        internal const string WaterShader = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        internal const string Helper = "Assets/_Project/Art/Shaders/Include/DaylightSwellRead.hlsl";
        internal const string Probe = "Assets/Tests/EditMode/DaylightSwellReadConformance.shader";

        static readonly Vector3[] Colours =
        {
            new Vector3(0.04f, 0.1f, 0.25f), new Vector3(1e-6f, 0f, 2f),
            new Vector3(-0.1f, 0.02f, -2f), Vector3.zero
        };

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(4f)]
        [TestCase(1.6f)]
        public void DaylightSwellRead_HasBoundedSignedContrast(float bands)
        {
            foreach (Vector3 colour in Colours)
            foreach (float strength in new[] { 0f, 0.1f, 0.2f, 0.35f, 1f, 2f })
            foreach (float gate in new[] { 0f, 0.393586f, 1f })
            foreach (float band in new[] { -1f, -0.7f, 0f, 0.6f, 1f })
            {
                Vector3 result = DaylightSwellRead.Apply(colour, band, strength, gate, bands, 1f);
                float fraction = Mathf.Clamp01(strength) * gate;
                for (int c = 0; c < 3; c++)
                {
                    if (colour[c] < 0f) Assert.AreEqual(colour[c], result[c]);
                    else
                    {
                        float tolerance = 1e-6f * Mathf.Max(1f, colour[c]);
                        Assert.GreaterOrEqual(result[c], colour[c] * (1f - fraction) - tolerance);
                        Assert.LessOrEqual(result[c], colour[c] * (1f + fraction) + tolerance);
                    }
                }
            }
            // Independent hand-worked endpoints, not another implementation of Apply.
            Vector3 neutral = Vector3.one * 0.04f;
            Assert.That(DaylightSwellRead.Apply(neutral, -1f, 0.35f, 1f, bands, 1f).x,
                Is.EqualTo(0.026f).Within(1e-7f));
            Assert.That(DaylightSwellRead.Apply(neutral, 1f, 0.35f, 1f, bands, 1f).x,
                Is.EqualTo(0.054f).Within(1e-7f));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void DaylightSwellRead_ZeroStrengthPreservesBaseline(float relativeSwitch)
        {
            foreach (Vector3 colour in Colours)
            foreach (float gate in new[] { 0f, 0.4f, 1f })
            foreach (float bands in new[] { 0f, 1f, 4f, 1.6f })
                AssertBits(colour, DaylightSwellRead.Apply(colour, -1f, 0f, gate, bands, relativeSwitch));

            // Signed zero must survive a real bypass, not multiply/add by zero.
            Vector3 signedZero = new Vector3(BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)), 2f, -3f);
            AssertBits(signedZero, DaylightSwellRead.Apply(signedZero, 1f, 0f, 1f, 4f, relativeSwitch));
        }

        [TestCase(0f)]
        [TestCase(0.25f)]
        [TestCase(0.55f)]
        [TestCase(0.95f)]
        public void DaylightSwellRead_PreservesCalmGate(float sea)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(WaterShader);
            Assert.IsNotNull(shader);
            var material = new Material(shader);
            try
            {
                float lo = material.GetFloat("_SwellReadSeaStateLo");
                float hi = material.GetFloat("_SwellReadSeaStateHi");
                Assert.AreEqual(0.1f, lo);
                Assert.AreEqual(0.45f, hi);
                float gate = WaterSurface.Smoothstep(lo, hi, sea);
                float expected = sea == 0f ? 0f : sea == 0.25f ? 0.393586f : 1f;
                Assert.That(gate, Is.EqualTo(expected).Within(1e-6f));
                Assert.AreEqual(0f, WaterSurface.Smoothstep(lo, hi, lo));
                Assert.That(WaterSurface.Smoothstep(lo, hi, 0.275f), Is.EqualTo(0.5f).Within(1e-6f));
                Assert.AreEqual(1f, WaterSurface.Smoothstep(lo, hi, hi));
                float previous = 0f;
                for (int i = 0; i <= 100; i++)
                {
                    float next = WaterSurface.Smoothstep(lo, hi, i / 100f);
                    Assert.GreaterOrEqual(next, previous);
                    previous = next;
                }

                Vector3 input = Vector3.one * 0.04f;
                foreach (float mode in new[] { 0f, 1f })
                {
                    Vector3 result = DaylightSwellRead.Apply(input, -1f, 0.35f, gate, 0f, mode);
                    if (sea == 0f) AssertBits(input, result);
                    else
                    {
                        Vector3 full = DaylightSwellRead.Apply(input, -1f, 0.35f, 1f, 0f, mode);
                        Assert.That(result.x - input.x, Is.EqualTo((full.x - input.x) * gate).Within(1e-7f));
                    }
                }
                // The swash retains the same ruled axis, with its own nonzero calm floor.
                const float swashCalm = 0.8f;
                Assert.That(WaterSurface.SwashSeaStateGate(sea, lo, hi, swashCalm),
                    Is.EqualTo(0.2f + 0.8f * expected).Within(1e-6f));

                string src = Compact(File.ReadAllText(WaterShader));
                StringAssert.Contains("floatsrLo=saturate(_SwellReadSeaStateLo);floatsrHi=max(_SwellReadSeaStateHi,srLo+1e-3);floatswellReadGate=smoothstep(srLo,srHi,_Chop);", src);
                StringAssert.Contains("floatSwashSeaStateGate(){floatlo=saturate(_SwellReadSeaStateLo);", src);
                StringAssert.Contains("if(_SwellReadStrength>0.001&&swellReadGate>0.001)", src);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void DaylightSwellRead_DefaultDialsAndShaderWiringPreserveLegacy()
        {
            string src = Compact(File.ReadAllText(WaterShader));
            StringAssert.Contains("[ToggleUI]_SwellReadRelative(", src);
            StringAssert.Contains("float_SwellReadRelative;", src);
            StringAssert.Contains("floatreadBand=swellReadSigned;", src);
            StringAssert.Contains("#include\"Assets/_Project/Art/Shaders/Include/DaylightSwellRead.hlsl\"", src);
            StringAssert.Contains("if(_SwellReadRelative>=0.5)col.rgb=HHDaylightSwellReadRelative(col.rgb,readBand,_SwellReadStrength,swellReadGate);elsecol.rgb+=readBand*_SwellReadStrength*swellReadGate*0.25;", src);
            Assert.IsFalse(Regex.IsMatch(File.ReadAllText(WaterShader), @"#pragma[^\r\n]*SwellRead", RegexOptions.IgnoreCase));
            int read = src.IndexOf("HHDaylightSwellReadRelative(col.rgb", StringComparison.Ordinal);
            Assert.Greater(src.IndexOf("floatfaceSigned=0.0;", StringComparison.Ordinal), read);
            Assert.Greater(src.IndexOf("col.rgb=PaletteGrade(col.rgb,dayNightLuma);", StringComparison.Ordinal), read);

            // The conformance wrapper uses the exact settled-main quantizer, not a second candidate law.
            const string quantizer = "floatb01=readBand*0.5+0.5;b01=floor(b01*_SwellReadBands+0.5)/_SwellReadBands;readBand=b01*2.0-1.0;";
            StringAssert.Contains(quantizer, src);
            string probe = Compact(File.ReadAllText(Probe));
            StringAssert.Contains(quantizer, probe);
            StringAssert.Contains("#include\"Assets/_Project/Art/Shaders/Include/DaylightSwellRead.hlsl\"", probe);
            StringAssert.Contains("HHDaylightSwellReadRelative(col.rgb,readBand,_SwellReadStrength,swellReadGate)", probe);
            StringAssert.Contains("floatamount=clamp(readBand,-1.0,1.0)*saturate(strength)*gate;returncolour+max(colour,0.0)*amount;", Compact(File.ReadAllText(Helper)));

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(WaterShader);
            Assert.IsNotNull(shader);
            var bare = new Material(shader);
            try
            {
                Assert.AreEqual(0f, bare.GetFloat("_SwellReadRelative"));
                Assert.AreEqual(0.35f, bare.GetFloat("_SwellReadStrength"));
                Assert.AreEqual(0f, bare.GetFloat("_SwellReadBands"));
                CheckMaterial("Assets/_Project/Art/Materials/Water.mat", bare);
                foreach (string path in Directory.GetFiles("Assets/_Project/Art/Materials/WaterPresets", "*.mat"))
                    CheckMaterial(path.Replace('\\', '/'), bare);
            }
            finally { UnityEngine.Object.DestroyImmediate(bare); }
        }

        static void CheckMaterial(string path, Material scratch)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(asset, path);
            Assert.IsTrue(asset.HasProperty("_SwellReadRelative"), path);
            Assert.AreEqual(0f, asset.GetFloat("_SwellReadRelative"), path);
            Assert.AreEqual(0.1f, asset.GetFloat("_SwellReadSeaStateLo"), path);
            // Preset copying must restore the new default without changing a serialized asset.
            scratch.SetFloat("_SwellReadRelative", 1f);
            scratch.CopyPropertiesFromMaterial(asset);
            Assert.AreEqual(0f, scratch.GetFloat("_SwellReadRelative"), path);
        }

        [Test]
        public void DaylightSwellRead_PosterizedEndpointCannotEscapeItsBound()
        {
            Vector3 colour = Vector3.one * 0.04f;
            // N=1.6 produces b=1.5 today. Only the relative endpoint clamps that overshoot.
            Assert.That(DaylightSwellRead.Apply(colour, 1f, 0.35f, 1f, 1.6f, 0f).x,
                Is.EqualTo(0.17125f).Within(1e-7f));
            Assert.That(DaylightSwellRead.Apply(colour, 1f, 0.35f, 1f, 1.6f, 1f).x,
                Is.EqualTo(0.054f).Within(1e-7f));
        }

        [Test]
        public void DaylightSwellRead_NegativeInputDoesNotCreateLight()
        {
            foreach (float band in new[] { -1f, 0f, 1f })
            {
                Vector3 dark = new Vector3(-0.04f, -2f, 0f);
                AssertBits(dark, DaylightSwellRead.Apply(dark, band, 0.35f, 1f, 0f, 1f));
            }
            // The neutral smooth signal leaves positive colour alone too.
            AssertBits(Colours[0], DaylightSwellRead.Apply(Colours[0], 0f, 0.35f, 1f, 0f, 1f));
        }

        [Test]
        public void DaylightSwellRead_SwitchHasNoIntermediateLaw()
        {
            Vector3 colour = Colours[0];
            Vector3 off = DaylightSwellRead.Apply(colour, -1f, 0.35f, 1f, 0f, 0f);
            Vector3 on = DaylightSwellRead.Apply(colour, -1f, 0.35f, 1f, 0f, 1f);
            Assert.AreNotEqual(off.x, on.x);
            AssertBits(off, DaylightSwellRead.Apply(colour, -1f, 0.35f, 1f, 0f, 0.49f));
            AssertBits(on, DaylightSwellRead.Apply(colour, -1f, 0.35f, 1f, 0f, 0.5f));
            AssertBits(on, DaylightSwellRead.Apply(colour, -1f, 0.35f, 1f, 0f, 0.75f));
        }

        internal static void AssertBits(Vector3 expected, Vector3 actual)
        {
            for (int c = 0; c < 3; c++)
                Assert.AreEqual(BitConverter.SingleToInt32Bits(expected[c]), BitConverter.SingleToInt32Bits(actual[c]));
        }

        static string Compact(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/|//[^\r\n]*", "", RegexOptions.Singleline);
            return Regex.Replace(source, @"\s+", "");
        }
    }
}
