using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class HullCausticLightingTests
    {
        private const float OpticalTolerance = 2e-5f;
        // FP32 lattice hashes amplify CPU/GPU multiply-add rounding; brightness units, not pixels.
        private const float VeinGpuCpuAbsoluteTolerance = 0.02f;
        private const string Probe = "Assets/Tests/EditMode/Art/CausticPatternProbe.compute";
        private static Vector4 ReferenceOptics => new Vector4(.02f, 12, 5, .5f);
        private static Vector2 ReferenceIrradiance => new Vector2(1, .05f);

        [TestCase(60f, .02f)]
        [TestCase(30f, .05f)]
        [TestCase(15f, .24f)]
        [TestCase(5f, .64f)]
        public void FresnelPinsTheFourReferenceElevations(float degrees, float expected)
            => Assert.That(CausticMath.FresnelAtDegrees(degrees, .02f, 5),
                Is.EqualTo(expected).Within(.005f), "Reflectance figures rounded to two decimals in ruling l5");

        [TestCase(0f, .35f)]
        [TestCase(1f, 1.25f)]
        public void ReachPinsTheConfigEndpoints(float chop, float expectedMeters)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try { Assert.That(HullCausticLighting.BandWidth(config, chop), Is.EqualTo(expectedMeters).Within(1e-6f)); }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void ClearNoonBeatsLowSunAndTheHullSlopeFadesToItsCap()
        {
            float Weight(float sun, float upward) => HullCausticLighting.Weight(.1f, .8f,
                true, upward, .5f, true, sun, 3, 1, 0, .6f, .18f, ReferenceOptics, ReferenceIrradiance,
                Vector2.right, Vector2.right, .15f, sun);
            float referenceSun = Mathf.Sin(15 * Mathf.Deg2Rad);
            Assert.That(Weight(1, 0), Is.GreaterThan(Weight(referenceSun, 0)));
            Assert.That(Weight(referenceSun, .25f), Is.EqualTo(Weight(referenceSun, 0) * .5f).Within(1e-6f));
            Assert.That(Weight(referenceSun, .5f), Is.Zero);
        }

        [TestCase(-.1f, true, 0f, true, 1f, .2f)]
        [TestCase(0f, true, 0f, true, 1f, .2f)]
        [TestCase(.8f, true, 0f, true, 1f, .2f)]
        [TestCase(.2f, false, 0f, true, 1f, .2f)]
        [TestCase(.2f, true, 1f, true, 1f, .2f)]
        [TestCase(.2f, true, 0f, false, 1f, .2f)]
        [TestCase(.2f, true, 0f, true, -.3f, .2f)]
        public void ExcludedFacesAndStatesHaveNoLight(float height, bool exterior, float upward,
                                                     bool wet, float sun, float chop)
            => Assert.That(HullCausticLighting.Weight(height, .8f, exterior, upward, .5f,
                wet, sun, 3, 1, chop, .6f, .18f, ReferenceOptics, ReferenceIrradiance), Is.Zero);

        [Test]
        public void BandFadesUpwardAndChopDimsByHalfAtFullBlow()
        {
            float near = HullCausticLighting.Weight(.1f, .8f, true, 0, .5f, true, 1, 3, 1, 0, .6f, .18f, ReferenceOptics, ReferenceIrradiance);
            float high = HullCausticLighting.Weight(.6f, .8f, true, 0, .5f, true, 1, 3, 1, 0, .6f, .18f, ReferenceOptics, ReferenceIrradiance);
            float blow = HullCausticLighting.Weight(.1f, .8f, true, 0, .5f, true, 1, 3, 1, 1, .6f, .18f, ReferenceOptics, ReferenceIrradiance);
            Assert.That(near, Is.GreaterThan(high));
            Assert.That(blow, Is.EqualTo(near * .5f).Within(1e-6f));
        }

        [Test]
        public void SameWorldPlaceHasTheSamePatternWhenTheHullMoves()
        {
            Vector3 world = new Vector3(-8, 12, -4);
            var first = Matrix4x4.Translate(new Vector3(0, 0, 0));
            var second = Matrix4x4.Translate(new Vector3(8, -4, 2));
            Vector3 localFirst = first.inverse.MultiplyPoint3x4(world);
            Vector3 localSecond = second.inverse.MultiplyPoint3x4(world);
            Vector4 plane = HullCausticLighting.Plane(new WaterIsoDepthFrame(-20, .766f, .643f, 0));
            Vector2 a = CausticMath.ProjectToWater(first.MultiplyPoint3x4(localFirst), plane);
            Vector2 b = CausticMath.ProjectToWater(second.MultiplyPoint3x4(localSecond), plane);
            Assert.That(a, Is.EqualTo(b));
            var settings = Settings();
            Assert.That(CausticMath.Veins(a, 14, in settings), Is.EqualTo(CausticMath.Veins(b, 14, in settings)));
        }

        [Test]
        public void RaisedHullMinimumIsAboveWater()
        {
            var points = new[] { Vector3.zero, Vector3.right, Vector3.up };
            Vector4 plane = new Vector4(0, 1, 0, 0);
            Assert.That(HullCausticLighting.MinimumHeight(Matrix4x4.Translate(Vector3.up * 2), points, plane),
                Is.EqualTo(2));
            Assert.That(HullCausticLighting.MinimumHeight(Matrix4x4.Translate(Vector3.down), points, plane),
                Is.EqualTo(-1));
        }

        [Test]
        public void LivePacketIsReadOnlyAndDoesNotAllocatePerFrame()
        {
            var owner = new GameObject("CausticPacketTest");
            var material = new Material(Shader.Find("HiddenHarbours/Water"));
            var source = new MaterialPropertyBlock();
            var target = new MaterialPropertyBlock();
            int amountId = Shader.PropertyToID("_CausticAmount");
            float authoredAmount = material.GetFloat(amountId);
            source.SetFloat(amountId, .71f);
            try
            {
                HullCausticLighting.Publish(owner, material, source, .2f);
                Vector4 plane = new Vector4(0, .766f, -.643f, 0), settings = new Vector4(.8f, .18f, 1, .5f);
                for (int i = 0; i < 8; i++) HullCausticLighting.Apply(target, plane, settings);
                using var allocation = new FoamTransportAllocationProbe();
                allocation.RequirePositiveControl();
                allocation.Begin();
                for (int i = 0; i < 100; i++)
                {
                    HullCausticLighting.Publish(owner, material, source, .2f);
                    HullCausticLighting.Apply(target, plane, settings);
                }
                long allocated = allocation.End();
                Assert.That(allocated, Is.Zero, "GC.Alloc events in 100 warmed per-frame packet publication/delivery calls");
                Assert.That(target.GetFloat(amountId), Is.EqualTo(.71f));
                Assert.That(source.GetFloat(amountId), Is.EqualTo(.71f));
                Assert.That(material.GetFloat(amountId), Is.EqualTo(authoredAmount));
            }
            finally
            {
                HullCausticLighting.Clear(owner);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void RecordedNoonEncodingSurvivesGainAndRisesMonotonicallyThroughTheLadder()
        {
            // Recorded heavy-noon pose: sun=1, chop=.05, width=.395, strength=.18.
            // Three analytic wall samples inside that band's geometry; not fabricated GPU pixels.
            var weights = new float[3]; float[] heights={.05f,.1f,.2f};
            for(int i=0;i<weights.Length;i++) weights[i]=HullCausticLighting.Weight(heights[i],.395f,true,0,.5f,
                true,1,3,1,.05f,.6f,.18f,ReferenceOptics,ReferenceIrradiance,Vector2.right,Vector2.right,.15f,1);
            foreach(bool truncate in new[]{false,true})
            {
                float Mean(float gain) {
                    float sum=0;
                    foreach(float w in weights) {
                        float encoded=CausticMath.EncodeAlpha(gain,.6f,w);
                        float quantized=truncate?Mathf.Floor(encoded*255)/255:CausticMath.QuantizeAlpha8(encoded);
                        sum+=CausticMath.DecodeAlpha(quantized);
                    }
                    return sum/weights.Length;
                }
                float lo=0,hi=256;for(int i=0;i<12;i++){float mid=(lo+hi)*.5f;if(Mean(mid)<4f/255)lo=mid;else hi=mid;}
                float physical=hi;
                Assert.That(Mean(physical),Is.EqualTo(4f/255).Within(2f/255),"One decoded-alpha step is 2/255");
                float previous=0;
                foreach(float rung in new[]{1f,3f,6f,10f,100000f})
                {
                    float next=Mean(physical*rung);Assert.That(next,Is.GreaterThanOrEqualTo(previous));previous=next;
                }
                Assert.That(Mean(0),Is.Zero);
                Assert.That(Mean(physical*100000),Is.GreaterThan(.99f));
            }
        }

        [Test]
        public void NativeArgb32EncodingKeepsTheEndpointAndTheLadderMonotone()
        {
            RequireGpu();
            var type=Type.GetType("HiddenHarbours.App.Editor.WaterHullsPlates, HiddenHarbours.App.Editor",true);
            string output=System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_NATIVE_PIN_EVIDENCE");
            if(string.IsNullOrEmpty(output))output=Path.GetFullPath("Evidence~/water-hulls/native-encoding-pin");
            type.GetMethod("RunNativeEncodingPin",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)
                .Invoke(null,new object[]{output});
        }

        [Test]
        public void IrradianceNoonToFifteenDegreesIsBetweenThreeAndFour()
        {
            float low = CausticMath.Irradiance(Mathf.Sin(15 * Mathf.Deg2Rad), 1, .05f);
            Assert.That(1f / low, Is.InRange(3f, 4f));
            Assert.That(low, Is.EqualTo(.25881905f).Within(1e-6f));
            Assert.That(CausticMath.Irradiance(0, 1, .05f), Is.EqualTo(.05f));
        }

        [TestCase(5f)]
        [TestCase(15f)]
        [TestCase(20f)]
        [TestCase(60f)]
        [TestCase(90f)]
        public void FacetIncidenceFresnelIsMonotoneAsChopTiltsTheFacet(float degrees)
        {
            float sun = Mathf.Sin(degrees * Mathf.Deg2Rad);
            float previous = CausticMath.FacetFresnel(sun, 0, .02f, 5, 12);
            for (int i=1; i<=10; i++)
            {
                float next = CausticMath.FacetFresnel(sun, i/10f, .02f, 5, 12);
                Assert.That(next, Is.LessThanOrEqualTo(previous+1e-7f)); previous=next;
            }
            Assert.That(previous, Is.GreaterThanOrEqualTo(.02f-1e-7f));
        }

        [TestCase(0f)]
        [TestCase(.2f)]
        [TestCase(1f)]
        public void MorningBearingGivesEastTheFullFactorAndWestOnlyChopFloor(float chop)
        {
            float sun = Mathf.Sin(20 * Mathf.Deg2Rad);
            float Weight(Vector2 face, float shadow) => HullCausticLighting.Weight(.1f, .8f,
                true, 0, .5f, true, sun, 3, 1, chop, .6f, .18f, ReferenceOptics, ReferenceIrradiance,
                face, Vector2.right, .15f, shadow);
            float unoccluded = HullCausticLighting.Weight(.1f, .8f, true, 0, .5f,
                true, sun, 3, 1, chop, .6f, .18f, ReferenceOptics, ReferenceIrradiance);
            Assert.That(Weight(Vector2.right, 1), Is.EqualTo(unoccluded).Within(1e-6f));
            Assert.That(Weight(Vector2.left, 1), Is.EqualTo(unoccluded * .15f * chop).Within(1e-6f));
        }

        [TestCase(0f)]
        [TestCase(.34202014f)]
        [TestCase(1f)]
        public void NoPublishedSunMeansNoLightAtAnyElevation(float sun)
            => Assert.That(HullCausticLighting.Weight(.1f, .8f, true, 0, .5f,
                true, sun, 3, 1, .85f, .6f, .18f, ReferenceOptics, ReferenceIrradiance,
                Vector2.right, Vector2.right, .15f, 0), Is.Zero);

        [Test]
        public void AwayFloorHasTheSerializedDefault()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try { Assert.That(config.HullCausticAwayFloor, Is.EqualTo(.15f)); }
            finally { Object.DestroyImmediate(config); }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WeightSample
        {
            public float Height, Exterior, Upward, Width, Strength, Wet, MaxUpward, Sun, TintSum, Gate, Chop, Amount;
            public float NormalX, NormalY, NormalZ, IsoSin, IsoCos, SunX, SunY, AwayFloor, ShadowStrength;
        }

        [Test]
        public void SharedGpuBandGatesAgreeWithTheTwin()
        {
            RequireGpu();
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(Probe);
            var samples = new WeightSample[16];
            for (int i = 0; i < samples.Length; i++) samples[i] = new WeightSample {
                Height = .2f, Exterior = 1, Upward = 0, Width = .8f, Strength = .18f,
                Wet = 1, MaxUpward = .5f, Sun = 1, TintSum = 3, Gate = 1, Chop = .2f, Amount = .6f,
                NormalX = 1, IsoSin = Mathf.Sin(45 * Mathf.Deg2Rad), IsoCos = Mathf.Cos(45 * Mathf.Deg2Rad),
                SunX = 1, AwayFloor = .15f, ShadowStrength = 1 };
            samples[1].Height = -.1f; samples[2].Height = 0; samples[3].Height = .8f;
            samples[4].Exterior = 0; samples[5].Upward = 1; samples[6].Wet = 0;
            samples[7].Sun = -.3f; samples[8].Chop = 1; samples[9].Sun = .1f;
            samples[10].NormalX = -1; samples[11].NormalX = -1; samples[11].Chop = 0;
            samples[12].ShadowStrength = 0; samples[13].ShadowStrength = .35f;
            // Ground north/south projected through the production iso map, then recovered by the vertex helper.
            for (int i = 14; i < 16; i++)
            {
                Vector3 n = IsoFacetMath.RigToWorld(0, 45).MultiplyVector(i == 14 ? Vector3.up : Vector3.down);
                samples[i].NormalX = n.x; samples[i].NormalY = n.y; samples[i].NormalZ = n.z;
                samples[i].SunX = 0; samples[i].SunY = 1;
            }
            using var input = new ComputeBuffer(samples.Length, Marshal.SizeOf<WeightSample>());
            using var output = new ComputeBuffer(samples.Length, sizeof(float));
            input.SetData(samples);
            int kernel = shader.FindKernel("CompareWeight");
            shader.SetVector("_HHHullCausticOptics", ReferenceOptics);
            shader.SetVector("_HHHullCausticIrradiance", new Vector4(1, .05f, 0, 0));
            shader.SetInt("_Count", samples.Length);
            shader.SetBuffer(kernel, "_WeightInputs", input);
            shader.SetBuffer(kernel, "_Outputs", output);
            shader.Dispatch(kernel, (samples.Length + 7) / 8, 1, 1);
            var actual = new float[samples.Length]; output.GetData(actual);
            for (int i = 0; i < samples.Length; i++)
            {
                WeightSample s = samples[i];
                float expected = HullCausticLighting.Weight(s.Height, s.Width, s.Exterior > .5f,
                    s.Upward, s.MaxUpward, s.Wet > .5f, s.Sun, s.TintSum, s.Gate, s.Chop, s.Amount, s.Strength, ReferenceOptics, ReferenceIrradiance,
                    i == 14 ? Vector2.up : i == 15 ? Vector2.down : new Vector2(s.NormalX, 0),
                    new Vector2(s.SunX, s.SunY), s.AwayFloor, s.ShadowStrength);
                Assert.That(actual[i], Is.EqualTo(expected).Within(OpticalTolerance), "sample " + i);
            }
        }

        [Test]
        public void SharedGpuVeinsAgreeWithTheTwinAtWorldPoints()
        {
            RequireGpu();
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(Probe);
            var settings = Settings();
            var samples = new[] { new Vector4(0, 0, 0, 0), new Vector4(.5f, .75f, 0, 0),
                new Vector4(-2, 3, 0, 0), new Vector4(5, -7, 0, 0), new Vector4(0, 0, 14, 0),
                new Vector4(2, 3, 14, 0), new Vector4(0, 0, 0, 1), new Vector4(-2, 3, 14, 1) };
            var texture = new Texture2D(2, 2, TextureFormat.RGBAFloat, false, true);
            texture.SetPixels(new[] { new Color(.5f,.5f,.5f,1), new Color(.5f,.5f,.5f,1),
                new Color(.5f,.5f,.5f,1), new Color(.5f,.5f,.5f,1) }); texture.Apply();
            try
            {
                using var input = new ComputeBuffer(samples.Length, 16);
                using var output = new ComputeBuffer(samples.Length, sizeof(float));
                input.SetData(samples);
                int kernel = shader.FindKernel("CompareVeins");
                shader.SetFloat("_PixelsPerUnit", settings.PixelsPerUnit); shader.SetFloat("_NoiseScale", settings.NoiseScale);
                shader.SetFloat("_BandScaleResponse", settings.BandScaleResponse); shader.SetFloat("_Chop", settings.Chop);
                shader.SetFloat("_Flow", settings.Flow); shader.SetFloat("_CausticScale", settings.Scale);
                shader.SetFloat("_PaintScale", settings.PaintScale); shader.SetFloat("_UntileStrength", settings.UntileStrength);
                shader.SetFloat("_CausticTexStrength", settings.TextureStrength);
                shader.SetTexture(kernel, "_CausticTex", texture);
                shader.SetInt("_Count", samples.Length); shader.SetBuffer(kernel, "_VeinInputs", input);
                shader.SetBuffer(kernel, "_Outputs", output); shader.Dispatch(kernel, 1, 1, 1);
                var actual = new float[samples.Length]; output.GetData(actual);
                Func<Vector2, float> constantTexture = _ => .5f;
                for (int i = 0; i < samples.Length; i++)
                {
                    Vector4 s = samples[i];
                    float expected = CausticMath.Veins(new Vector2(s.x, s.y), s.z, in settings,
                        s.w > .5f ? constantTexture : null);
                    Assert.That(actual[i], Is.EqualTo(expected).Within(VeinGpuCpuAbsoluteTolerance), "world sample " + i);
                }
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void SharedPureHelpersRetainTheSeasArithmetic()
        {
            string sea = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursWater.shader");
            string include = File.ReadAllText("Assets/_Project/Art/Shaders/Include/CausticPattern.hlsl");
            foreach (string name in new[] { "Pixelize", "BandFreq", "Hash21", "Hash22", "ValueNoise", "PaintUV", "UntileSampleW" })
            {
                string original = Function(sea, name);
                string shared = Regex.Replace(Function(include, "HHCaustic" + name), @"\bHHCaustic", "");
                Assert.That(Regex.Replace(shared, @"\s+", ""), Is.EqualTo(Regex.Replace(original, @"\s+", "")), name);
            }
        }

        private static string Function(string source, string name)
        {
            Match m = Regex.Match(source, @"(?:float2|float|half4) " + name + @"\(");
            Assert.That(m.Success, Is.True, name);
            int begin = source.IndexOf('{', m.Index), depth = 1, end = begin + 1;
            while (depth > 0) { if (source[end] == '{') depth++; if (source[end] == '}') depth--; end++; }
            return source.Substring(m.Index, end - m.Index);
        }

        private static CausticMath.VeinSettings Settings() => new CausticMath.VeinSettings {
            PixelsPerUnit = 24, Scale = .9f, Flow = .07f, PaintScale = .25f, UntileStrength = 1,
            NoiseScale = .04f, BandScaleResponse = .25f, Chop = .2f, TextureStrength = .78f };

        private static void RequireGpu()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — the native encoding and GPU agreement pins need a graphics device; expected on CI.");
            if (!SystemInfo.supportsComputeShaders) Assert.Ignore("A compute-capable graphics device is required.");
        }
    }
}
