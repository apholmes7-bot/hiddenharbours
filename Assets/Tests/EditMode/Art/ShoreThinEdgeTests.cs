using System.IO;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the marbled thin edge. On a near-flat bed the beach swash's along-shore offset is
    /// keyed to a shore direction the bed cannot give, so it turned to speckle, and the drawn edge's hard cut
    /// drew its contours as nested rings of water and sand. The fix is two dials: the offset fades out below a
    /// seabed slope (<see cref="BreakerSettings.SwashSteadyEdgeSlope"/>), and the water thins to a film over the
    /// last few centimetres of level inside the edge (<see cref="BreakerSettings.EdgeWetFilmMeters"/>).
    /// 0 on either gives the previous look. CI has no GPU, so the C# twins carry the law and the shader source
    /// is pinned to call them.
    /// </summary>
    public sealed class ShoreThinEdgeTests
    {
        private const string WaterShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const string BridgePath = "Assets/_Project/Code/Art/WaveFieldBridge.cs";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        [Test]
        public void TheSteadyEdge_IsOne_WhenTheDialIsOff()
        {
            for (float slope = 0f; slope <= 2f; slope += 0.005f)
            {
                Assert.AreEqual(1f, BreakerMath.SwashDesyncKeep01(slope, 0f), 0f, "0 = the previous look");
                Assert.AreEqual(1f, BreakerMath.SwashDesyncKeep01(slope, -1f), 0f);
            }
        }

        [Test]
        public void TheSteadyEdge_KeepsRealBeaches_AndStillsFlats()
        {
            float steady = BreakerSettings.Default.SwashSteadyEdgeSlope;
            Assert.AreEqual(0f, BreakerMath.SwashDesyncKeep01(0f, steady), 0f, "a flat keeps no offset");
            Assert.AreEqual(0f, BreakerMath.SwashDesyncKeep01(0.25f * steady, steady), 1e-6f);
            Assert.AreEqual(1f, BreakerMath.SwashDesyncKeep01(steady, steady), 1e-6f);
            Assert.AreEqual(1f, BreakerMath.SwashDesyncKeep01(0.5f, steady), 0f, "a real beach is untouched");
            float prev = 0f, maxStep = 0f;
            for (float slope = 0f; slope <= 0.3f; slope += 0.0005f)
            {
                float k = BreakerMath.SwashDesyncKeep01(slope, steady);
                Assert.That(k, Is.InRange(0f, 1f));
                Assert.GreaterOrEqual(k + 1e-6f, prev, "the keep must not fall as the slope grows");
                maxStep = Mathf.Max(maxStep, k - prev);
                prev = k;
            }
            Assert.Less(maxStep, 0.02f, "no step in the keep between slope samples 0.0005 apart");
        }

        [Test]
        public void OnAFlat_NeighbouringStretchesSwashAsOne()
        {
            // The marbling, as the twin sees it: on a flat the along-shore noise sample jumps between
            // neighbouring pixels (the tangent turns round at every bump). Without the steady edge their
            // swash offsets differ; with it they are one line.
            float steady = BreakerSettings.Default.SwashSteadyEdgeSlope;
            const float vary = 0.35f, flatSlope = 0.01f, beachSlope = 0.2f;
            for (float t = 0f; t < 10f; t += 0.37f)
            {
                float a = WaterSurface.SwashOffset(t, 0.16f, 1f, 0.05f, 1.2f, vary * BreakerMath.SwashDesyncKeep01(flatSlope, steady), 0.1f, true);
                float b = WaterSurface.SwashOffset(t, 0.16f, 1f, 0.05f, 1.2f, vary * BreakerMath.SwashDesyncKeep01(flatSlope, steady), 0.9f, true);
                Assert.AreEqual(a, b, 1e-6f, "a flat swashes as one line");
                float c = WaterSurface.SwashOffset(t, 0.16f, 1f, 0.05f, 1.2f, vary * BreakerMath.SwashDesyncKeep01(beachSlope, steady), 0.1f, true);
                float d = WaterSurface.SwashOffset(t, 0.16f, 1f, 0.05f, 1.2f, vary, 0.1f, true);
                Assert.AreEqual(d, c, 0f, "a real beach keeps its offset exactly");
            }
            float spread = 0f;
            for (float t = 0f; t < 10f; t += 0.37f)
                spread = Mathf.Max(spread, Mathf.Abs(
                    WaterSurface.SwashOffset(t, 0.16f, 1f, 0.05f, 1.2f, vary, 0.1f, true) -
                    WaterSurface.SwashOffset(t, 0.16f, 1f, 0.05f, 1.2f, vary, 0.9f, true)));
            Assert.Greater(spread, 0.3f, "without the steady edge the same two stretches disagree (the defect)");
        }

        [Test]
        public void TheWetFilm_IsOne_WhenTheDialIsOff()
        {
            for (float q = -0.1f; q <= 0.5f; q += 0.005f)
                Assert.AreEqual(1f, BreakerMath.EdgeWetFilm01(q, 0f), 0f, "0 = the hard cut");
        }

        [Test]
        public void TheWetFilm_ThinsToClearAtTheDrawnEdge()
        {
            float film = BreakerSettings.Default.EdgeWetFilmMeters;
            Assert.AreEqual(0f, BreakerMath.EdgeWetFilm01(0f, film), 1e-6f, "clear at the drawn edge");
            Assert.AreEqual(1f, BreakerMath.EdgeWetFilm01(film, film), 1e-6f, "full water past the film");
            Assert.AreEqual(1f, BreakerMath.EdgeWetFilm01(1f, film), 0f, "deep water is untouched");
            float prev = 0f, maxStep = 0f;
            float dq = film / 120f;   // the film's own scale: a ramp, not a step, at any shipped width
            for (float q = 0f; q <= 0.1f; q += dq)
            {
                float a = BreakerMath.EdgeWetFilm01(q, film);
                Assert.That(a, Is.InRange(0f, 1f));
                Assert.GreaterOrEqual(a + 1e-6f, prev, "the film thickens inward");
                maxStep = Mathf.Max(maxStep, a - prev);
                prev = a;
            }
            Assert.Less(maxStep, 0.02f, "the film has no band: no step between samples 1/120 of its width apart");
        }

        [Test]
        public void TheShippedConfig_CarriesTheDials_AndOffMeansOff()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath);
            Assert.AreEqual(BreakerSettings.Default.SwashSteadyEdgeSlope, config.Breakers.SwashSteadyEdgeSlope, 1e-6f);
            Assert.AreEqual(BreakerSettings.Default.EdgeWetFilmMeters, config.Breakers.EdgeWetFilmMeters, 1e-6f);
            Assert.Greater(config.Breakers.SwashSteadyEdgeSlope, 0f);
            Assert.Greater(config.Breakers.EdgeWetFilmMeters, 0f);
            // The breakers-off publish uses default settings: both dials 0, the previous edge.
            var off = default(BreakerSettings);
            Assert.AreEqual(0f, off.SwashSteadyEdgeSlope, 0f);
            Assert.AreEqual(0f, off.EdgeWetFilmMeters, 0f);
        }

        [Test]
        public void TheShaderAndTheBridge_UseTheTwins()
        {
            string src = File.ReadAllText(WaterShaderPath);
            StringAssert.Contains("float4 _ShoreThinEdge;", src);
            StringAssert.Contains("return steady > 0.0 ? smoothstep(0.25 * steady, steady, SeabedSlopeMag(worldXY)) : 1.0;", src);
            Assert.AreEqual(2, CountOf(src, "* _SwashAlongShoreVary * 6.2831853 * SwashDesyncKeep(worldXY);"),
                "the swash and its rate twin must keep the same desync");
            StringAssert.Contains("if (_ShoreThinEdge.y > 0.0) col.a *= smoothstep(0.0, _ShoreThinEdge.y, washEdgeQ);", src);
            StringAssert.Contains("#include \"Assets/_Project/Art/Shaders/Include/CausticPattern.hlsl\"", src);
            StringAssert.DoesNotContain("#include \"Include/CausticPattern.hlsl\"", src);
            string bridge = File.ReadAllText(BridgePath);
            StringAssert.Contains("Shader.PropertyToID(\"_ShoreThinEdge\")", bridge);
            StringAssert.Contains("breakers.SwashSteadyEdgeSlope", bridge);
            StringAssert.Contains("breakers.EdgeWetFilmMeters", bridge);
        }

        private static int CountOf(string text, string part)
        {
            int n = 0;
            for (int i = text.IndexOf(part, System.StringComparison.Ordinal); i >= 0;
                 i = text.IndexOf(part, i + part.Length, System.StringComparison.Ordinal)) n++;
            return n;
        }
    }
}
