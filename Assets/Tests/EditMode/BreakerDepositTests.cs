using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>The bore's foam DEPOSIT (ADR 0040 rev 3): the advected wake buffer gains a source under every
    /// bore front, twinned to <see cref="BreakerMath.SurfAt"/>.</b>
    ///
    /// <para><b>The copy is the contract.</b> The advect pass cannot include the water shader, so the
    /// surf physics it needs — the fetch march, the breaker contour, the surf march and the bore — is
    /// COPIED into it, verbatim, between marker comments. A copy that can drift is a second bore; the
    /// first test here makes drift a red. The GPU test then runs the pass on a synthetic beach the C# can
    /// evaluate exactly and compares the laid foam texel by texel with <c>SurfAt</c>'s own
    /// <c>Breaking01 × Whitewater01 × Bore01</c>.</para>
    ///
    /// <para><b>⚠ The GPU test self-skips without a graphics device</b> — the standing CI law: a skip is
    /// "NOT VERIFIED", never "passed". The source, seam and registry guards run everywhere.</para>
    /// </summary>
    public class BreakerDepositTests
    {
        const string WaterPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const string AdvectPath = "Assets/_Project/Art/Shaders/HiddenHarboursFoamBufferAdvect.shader";

        // =============================================================================================
        //  The copy
        // =============================================================================================

        [Test]
        public void TheTwinnedSurfPhysics_IsByteIdentical_BetweenTheWaterAndTheAdvectShaders()
        {
            string water = File.ReadAllText(WaterPath, Encoding.UTF8).Replace("\r\n", "\n");
            string advect = File.ReadAllText(AdvectPath, Encoding.UTF8).Replace("\r\n", "\n");
            foreach (string twin in new[] { "TWIN A", "TWIN B" })
            {
                string a = Between(water, twin, "the water shader");
                string b = Between(advect, twin, "the advect shader");
                Assert.That(a.Length, Is.GreaterThan(200), $"{twin} in the water shader is implausibly short");
                if (a != b)
                {
                    int at = 0;
                    while (at < a.Length && at < b.Length && a[at] == b[at]) at++;
                    int line = a.Substring(0, at).Split('\n').Length;
                    Assert.Fail($"{twin} differs between the two shaders from line {line} of the region — the " +
                                "advect pass is running a DIFFERENT bore from the one the water draws. Edit the water " +
                                "shader's region and copy it verbatim (scratch: the PR 2 apply script regenerates it).");
                }
            }
            // …and the deposit reads what the water reads.
            StringAssert.Contains("float front = breaking * alive * bore;", advect);
            StringAssert.Contains("fresh = max(fresh, step(0.5, front));", advect, "freshness is a GATE, never an add");
        }

        static string Between(string src, string twin, string where)
        {
            string begin = "// ==== " + twin + " (begin)";
            string end = "// ==== " + twin + " (end) ====";
            int i = src.IndexOf(begin, StringComparison.Ordinal);
            Assert.That(i, Is.GreaterThanOrEqualTo(0), $"{twin} has no begin marker in {where}");
            i = src.IndexOf('\n', i) + 1;
            int j = src.IndexOf(end, i, StringComparison.Ordinal);
            Assert.That(j, Is.GreaterThan(i), $"{twin} has no end marker in {where}");
            j = src.LastIndexOf('\n', j) + 1;
            return src.Substring(i, j - i);
        }

        [Test]
        public void TheDepositRate_IsOneSeam()
        {
            string advect = File.ReadAllText(AdvectPath, Encoding.UTF8);
            var m = System.Text.RegularExpressions.Regex.Match(advect, @"#define\s+SURF_DEPOSIT_RATE\s+([0-9.]+)");
            Assert.That(m.Success, "SURF_DEPOSIT_RATE is gone from the advect shader");
            Assert.AreEqual(FoamBuffer.SurfDepositRatePerSecond, float.Parse(m.Groups[1].Value,
                            System.Globalization.CultureInfo.InvariantCulture), 1e-6f,
                "the deposit rate must be the same number on both sides of the seam");
            StringAssert.Contains("SURF_DEPOSIT_RATE * max(_HHSurfDeposit.z, 0.0)", advect,
                "the deposit is a RATE times dt — a per-frame amount would make the foam frame-rate dependent");
        }

        [Test]
        public void TheAdvectShader_ReadsNoMaterialOfTheWater()
        {
            // The pass has no water material. Every name the twinned text needs is a published global or a
            // macro over one; a material-bound name here would compile to 0 and break silently.
            string advect = File.ReadAllText(AdvectPath, Encoding.UTF8);
            string code = CodeOnly(advect);
            foreach (string forbidden in new[] { "_HeightTex", "_HeightWorldMin", "_HeightWorldSize", "_PixelsPerUnit",
                                                 "_OceanSwellScale", "_SwashMaxEdgeShift", "_SurfStrength", "_Time" })
                StringAssert.DoesNotContain(forbidden, code, $"the advect shader reaches {forbidden}");
            StringAssert.Contains("#define _WaterLevel (_HHSeaLevelWorld.x)", advect);
            StringAssert.Contains("#define _ShoreSampleStep (_HHSeabedRange.z)", advect);
        }

        static string CodeOnly(string src)
        {
            var sb = new StringBuilder();
            foreach (string raw in src.Split('\n'))
            {
                int at = raw.IndexOf("//", StringComparison.Ordinal);
                sb.Append(at >= 0 ? raw.Substring(0, at) : raw).Append('\n');
            }
            return sb.ToString();
        }

        // =============================================================================================
        //  The gate
        // =============================================================================================

        [Test]
        public void TheRegistry_RunsForADepositWithNoHull_AndNeverWithoutTheLookDial()
        {
            float look = FoamInjectionRegistry.LookStrength;
            float deposit = FoamInjectionRegistry.SurfDepositStrength;
            float scale = FoamInjectionRegistry.DrawnWaveScale;
            try
            {
                Assume.That(FoamInjectionRegistry.Count, Is.EqualTo(0), "an EditMode fixture with a live injector — run this alone");
                FoamInjectionRegistry.PublishLookStrength(0f);
                FoamInjectionRegistry.PublishSurfDeposit(1f, 2.8f);
                Assert.IsFalse(FoamInjectionRegistry.ShouldRun, "no _WakeFoamStrength = nothing draws the buffer = no pass");
                FoamInjectionRegistry.PublishLookStrength(0.85f);
                Assert.IsTrue(FoamInjectionRegistry.ShouldRun, "a deposit runs the pass with no hull on the water");
                Assert.AreEqual(2.8f, FoamInjectionRegistry.DrawnWaveScale, 1e-6f);
                FoamInjectionRegistry.PublishSurfDeposit(0f, 1f);
                Assert.IsFalse(FoamInjectionRegistry.ShouldRun, "deposit 0 and no hull: the zero-cost-when-idle contract holds");
                FoamInjectionRegistry.PublishSurfDeposit(-3f, 0f);
                Assert.AreEqual(0f, FoamInjectionRegistry.SurfDepositStrength, "a negative dial is 0");
                Assert.That(FoamInjectionRegistry.DrawnWaveScale, Is.GreaterThan(0f), "a zero scale is floored");
            }
            finally
            {
                FoamInjectionRegistry.PublishLookStrength(look);
                FoamInjectionRegistry.PublishSurfDeposit(deposit, scale);
            }
        }

        [Test]
        public void TheSeabedGlobals_UnsetIsEverywhereDeep()
        {
            SeabedGlobals.PublishUnset();
            Assert.IsFalse(SeabedGlobals.IsBound);
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector(SeabedGlobals.Range), "w = 0 is the unbound flag the shader tests");
            var tex = new Texture2D(4, 4, TextureFormat.RFloat, false, true);
            try
            {
                SeabedGlobals.Publish(tex, new Vector2(10f, 20f), new Vector2(64f, 32f), -5f, 1.4f, 0.4f);
                Assert.IsTrue(SeabedGlobals.IsBound);
                Vector4 rect = Shader.GetGlobalVector(SeabedGlobals.Rect);
                Assert.AreEqual(new Vector4(10f, 20f, 64f, 32f), rect);
                Vector4 range = Shader.GetGlobalVector(SeabedGlobals.Range);
                Assert.AreEqual(new Vector4(-5f, 1.4f, 0.4f, 1f), range);
                SeabedGlobals.Publish(null, Vector2.zero, Vector2.one, 0f, 1f, 0.4f);
                Assert.IsFalse(SeabedGlobals.IsBound, "a null texture unsets rather than binding garbage");
            }
            finally
            {
                Object.DestroyImmediate(tex);
                SeabedGlobals.PublishUnset();
            }
        }

        // =============================================================================================
        //  The GPU twin
        // =============================================================================================

        /// <summary>A plane beach the C# can evaluate exactly: elevation rises linearly with x, clamped
        /// to the published rect's half-texel interior the way the GPU's clamp addressing does.</summary>
        sealed class PlaneBeach : ITidalTerrain
        {
            public const float Size = 64f, Min = -5f, Max = 1.4f, Slope = 0.1f;
            public const int Texels = 256;
            const float Half = Size / Texels * 0.5f;
            public float ElevationAt(Vector2 p)
            {
                float x = Mathf.Clamp(p.x, Half, Size - Half);
                return Min + Slope * x;
            }
            public Texture2D Bake()
            {
                var tex = new Texture2D(Texels, Texels, TextureFormat.RFloat, false, true)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "PlaneBeach" };
                var px = new Color[Texels * Texels];
                for (int y = 0; y < Texels; y++)
                for (int x = 0; x < Texels; x++)
                {
                    float wx = (x + 0.5f) * Size / Texels;
                    float r = (Min + Slope * wx - Min) / (Max - Min);
                    px[y * Texels + x] = new Color(r, 0f, 0f, 1f);
                }
                tex.SetPixels(px);
                tex.Apply(false, false);
                return tex;
            }
        }

        [Test]
        public void TheDeposit_IsLaidUnderTheBoresFront_TwinnedToBreakerMath_Measured()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device; the deposit pass needs a GPU.");

            var shader = Shader.Find("Hidden/HiddenHarbours/FoamBufferAdvect");
            Assert.IsNotNull(shader, "the advect shader must exist");

            var beach = new PlaneBeach();
            Texture2D heightTex = beach.Bake();
            Material mat = null;
            RenderTexture rt = null, prevRt = null;
            Texture2D readback = null;
            float look = FoamInjectionRegistry.LookStrength, dep = FoamInjectionRegistry.SurfDepositStrength, sc = FoamInjectionRegistry.DrawnWaveScale;
            try
            {
                // ---- the sea, exactly as the water would publish it --------------------------------
                var wind = new Vector2(6f, -5.3f);
                WaveTrains trains = WaveMath.TrainsFrom(wind, 0.55f, GameServices.WaveField);
                WaveFetchSettings fetch = GameServices.WaveFetch;
                BreakerSettings breakers = GameServices.Breakers;
                float gravity = GameServices.WaveField.Gravity;
                const float level = 0f, strength = 1f, dt = 0.1f;
                float drawnScale = 0.07f / 0.025f;

                WaveFieldBridge.PublishGlobals(WaveFieldBridge.Pack(in trains));
                WaveFieldBridge.PublishFetchGlobals(fetch, wind);
                WaveFieldBridge.PublishBreakerGlobals(trains.Dominant, fetch, breakers);
                Shader.SetGlobalVector("_HHSeaLevelWorld", new Vector4(level, 1f, 1f, 1f));
                SeabedGlobals.Publish(heightTex, Vector2.zero, new Vector2(PlaneBeach.Size, PlaneBeach.Size),
                                      PlaneBeach.Min, PlaneBeach.Max, 0.4f);

                BreakerContour contour = BreakerMath.ContourFor(trains.Dominant, WaveFetch.Envelope01(0f, in fetch), breakers);
                Assert.IsTrue(contour.Breaks, "the shot sea must break");

                // ---- the pass, blitted by hand ------------------------------------------------------
                const float extent = PlaneBeach.Size;
                int res = FoamBuffer.ResolutionForExtent(extent);
                Vector2 origin = FoamBuffer.WorldCellOrigin(new Vector2(extent * 0.5f, extent * 0.5f), extent);
                mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                var black = new RenderTexture(res, res, 0, RenderTextureFormat.ARGBFloat) { filterMode = FilterMode.Point };
                prevRt = black;
                black.Create();
                var clear = RenderTexture.active; RenderTexture.active = black; GL.Clear(false, true, Color.clear); RenderTexture.active = clear;
                mat.SetTexture(FoamShaderIds.Prev, black);
                mat.SetVector(FoamShaderIds.BufferWorld, new Vector4(origin.x, origin.y, extent, 1f / extent));
                mat.SetVector(FoamShaderIds.Resolution, new Vector4(res, res, 1f / res, 1f / res));
                mat.SetVector(FoamShaderIds.Shift, Vector4.zero);
                mat.SetFloat(FoamShaderIds.Decay, 1f);
                mat.SetFloat(FoamShaderIds.AgeDecay, 1f);
                mat.SetVectorArray(FoamShaderIds.InjectSeg, new Vector4[FoamBuffer.MaxInjectors]);
                mat.SetVectorArray(FoamShaderIds.InjectShape, new Vector4[FoamBuffer.MaxInjectors]);
                mat.SetVector(FoamShaderIds.SurfDeposit, new Vector4(strength, drawnScale, dt, 0f));
                mat.SetVector("_BlitScaleBias", new Vector4(1f, 1f, 0f, 0f));

                rt = new RenderTexture(res, res, 0, RenderTextureFormat.ARGBFloat) { filterMode = FilterMode.Point };
                rt.Create();
                var cmd = new CommandBuffer { name = "HH deposit twin" };
                cmd.SetRenderTarget(rt);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, 3, 1);
                Graphics.ExecuteCommandBuffer(cmd);
                cmd.Release();

                readback = new Texture2D(res, res, TextureFormat.RGBAFloat, false, true);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                readback.Apply();
                RenderTexture.active = prev;
                Color[] px = readback.GetPixels();

                // ---- the C# twin, texel by texel ---------------------------------------------------
                int surfTexels = 0, agreeing = 0, gateAgree = 0, gateJudged = 0, laid = 0;
                float worstR = 0f;
                Vector2 worstAt = Vector2.zero;
                double gpuSum = 0, cpuSum = 0;
                for (int ty = 0; ty < res; ty += 3)
                for (int tx = 0; tx < res; tx += 3)
                {
                    var world = origin + new Vector2((tx + 0.5f) / FoamBuffer.CellsPerUnit, (ty + 0.5f) / FoamBuffer.CellsPerUnit);
                    float env = WaveFetch.EnvelopeAt(world, wind, level, beach, in fetch);
                    SurfState s = BreakerMath.SurfAt(world, level, beach, in contour, env, in trains, gravity, in breakers, drawnScale);
                    float front = s.Breaking01 * s.Whitewater01 * s.Bore01;
                    float expected = Mathf.Clamp01(front * strength * FoamBuffer.SurfDepositRatePerSecond * dt);
                    Color c = px[ty * res + tx];
                    gpuSum += c.r; cpuSum += expected;
                    if (c.r > 0.01f) laid++;
                    if (expected <= 0.01f && c.r <= 0.01f) continue;
                    surfTexels++;
                    float d = Mathf.Abs(c.r - expected);
                    if (d <= 0.02f) agreeing++;
                    if (d > worstR) { worstR = d; worstAt = world; }
                    if (Mathf.Abs(front - 0.5f) > 0.05f)
                    {
                        gateJudged++;
                        bool cpuGate = front > 0.5f, gpuGate = c.g > 0.5f;
                        if (cpuGate == gpuGate) gateAgree++;
                    }
                }
                float share = surfTexels > 0 ? agreeing / (float)surfTexels : 1f;
                float gateShare = gateJudged > 0 ? gateAgree / (float)gateJudged : 1f;
                Debug.Log($"[deposit] {laid} of {res * res / 9} sampled texels carry foam; {surfTexels} judged; " +
                          $"{share:P1} agree within 0.02 (worst {worstR:F3} at {worstAt}); the freshness gate agrees " +
                          $"on {gateShare:P1} of {gateJudged}; GPU sum {gpuSum:F1} vs C# {cpuSum:F1}.");

                Assert.That(laid, Is.GreaterThan(100), "the pass laid foam on too few texels — the deposit is not running");
                Assert.That(surfTexels, Is.GreaterThan(100), "no surf texels to judge");
                Assert.That(share, Is.GreaterThan(0.97f),
                    $"only {share:P1} of the surf texels agree with BreakerMath.SurfAt within 0.02 (worst {worstR:F3} at {worstAt}) — " +
                    "the advect pass is laying a different bore from the one the C# computes");
                Assert.That(gateShare, Is.GreaterThan(0.97f), "the freshness gate disagrees with the C# front");
                Assert.That(gpuSum, Is.EqualTo(cpuSum).Within(0.06 * Math.Max(cpuSum, 1.0)), "the total laid foam differs from the twin's");
            }
            finally
            {
                FoamInjectionRegistry.PublishLookStrength(look);
                FoamInjectionRegistry.PublishSurfDeposit(dep, sc);
                SeabedGlobals.PublishUnset();
                Shader.SetGlobalVector("_HHSeaLevelWorld", Vector4.zero);
                WaveFieldBridge.PublishGlobals(PackedWaveField.Empty);
                WaveFieldBridge.PublishBreakersOff();
                WaveFieldBridge.PublishFetchOff();
                if (readback != null) Object.DestroyImmediate(readback);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (prevRt != null) { prevRt.Release(); Object.DestroyImmediate(prevRt); }
                if (mat != null) Object.DestroyImmediate(mat);
                Object.DestroyImmediate(heightTex);
            }
        }
    }
}
