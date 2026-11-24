using System.IO;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the shore wash's hard lee edge (the pale wedge at Rice Point, dusk). Land
    /// shadows the envelope march on a straight line along the wind or swell, so the envelope the drawn
    /// wash reads jumped from open sea to dead lee in one pixel. Behind it, the drawn edge's break gate is
    /// asked at a fixed 2 cm, so it opened fully within a few thousandths of the envelope: even a softened
    /// lee came back as a step. The fix is two dials: the envelope read across the shadow on a world-fixed
    /// lattice and blended (<see cref="BreakerSettings.BoreLeeSoftenMeters"/>), and the run-up's share of
    /// the drawn edge growing with the break depth (<see cref="BreakerSettings.BoreEdgeFadeDepthMeters"/>).
    /// CI has no GPU, so the C# twins carry the law and the shader source is pinned to call them.
    /// </summary>
    public sealed class ShoreWashLeeFadeTests
    {
        private const string WaterShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const string BridgePath = "Assets/_Project/Code/Art/WaveFieldBridge.cs";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        // The break contour Rice Point published at dusk (measured in the editor, sea state 0.18):
        // break depths at (open, mid, lee floor) envelopes, outer depths, and the lee floor.
        private static readonly Vector3 DuskBreaks = new Vector3(0.3238f, 0.1868f, 0f);
        private static readonly Vector3 DuskOuter = new Vector3(0.3691f, 0.2129f, 0f);
        private const float DuskLee = 0.01f;

        private static float OldEdgeGate(float envelope)
        {
            var contour = new BreakerContour(DuskBreaks, DuskOuter, DuskLee, true);
            return BreakerMath.Breaking01FromContour(BreakerMath.MinDepthMeters, in contour, envelope);
        }

        private static float NewEdgeGate(float envelope, float fadeDepth)
            => OldEdgeGate(envelope)
               * BreakerMath.BoreEdgeLeeFade01(BreakerMath.DepthAtEnvelope(DuskBreaks, DuskLee, envelope), fadeDepth);

        [Test]
        public void TheOldEdgeGate_IsAStepInTheEnvelope()
        {
            // The defect, measured: the whole 0 -> 1 hand-over sits inside a sliver of the envelope.
            float first = -1f, full = -1f;
            for (int i = 0; i <= 10000; i++)
            {
                float e = i / 10000f;
                float g = OldEdgeGate(e);
                if (first < 0f && g > 0.01f) first = e;
                if (full < 0f && g > 0.99f) { full = e; break; }
            }
            Assert.Greater(first, 0f);
            Assert.Less(full - first, 0.02f, "the old gate is expected to switch inside 0.02 of envelope");
        }

        [Test]
        public void TheFadedEdgeGate_RampsOverAWideShareOfTheEnvelope()
        {
            float fade = BreakerSettings.Default.BoreEdgeFadeDepthMeters;
            float first = -1f, full = -1f, maxStep = 0f, prev = 0f;
            for (int i = 0; i <= 10000; i++)
            {
                float e = i / 10000f;
                float g = NewEdgeGate(e, fade);
                Assert.That(g, Is.InRange(0f, 1f));
                Assert.GreaterOrEqual(g + 1e-6f, prev, "the faded gate must not fall as the envelope grows");
                maxStep = Mathf.Max(maxStep, g - prev);
                prev = g;
                if (first < 0f && g > 0.01f) first = e;
                if (full < 0f && g > 0.99f) full = e;
            }
            Assert.Greater(full - first, 0.1f, "the hand-over should spread over at least 0.1 of envelope");
            Assert.Less(maxStep, 0.01f, "no step bigger than 1% between envelope samples 1e-4 apart");
        }

        [Test]
        public void TheFade_IsOne_WhenTheDialIsOff_AndWhereTheBreakIsDeep()
        {
            for (float bd = 0f; bd <= 0.5f; bd += 0.01f)
            {
                Assert.AreEqual(1f, BreakerMath.BoreEdgeLeeFade01(bd, 0f), 0f, "0 = the previous switch");
                Assert.AreEqual(1f, BreakerMath.BoreEdgeLeeFade01(bd, BreakerMath.MinDepthMeters), 0f);
            }
            Assert.AreEqual(1f, BreakerMath.BoreEdgeLeeFade01(0.08f, 0.08f), 1e-6f);
            Assert.AreEqual(1f, BreakerMath.BoreEdgeLeeFade01(0.3f, 0.08f), 1e-6f, "the open sea is untouched");
            Assert.AreEqual(0f, BreakerMath.BoreEdgeLeeFade01(BreakerMath.MinDepthMeters, 0.08f), 1e-6f);
        }

        [Test]
        public void TheLeeLattice_TurnsAShadowLineIntoARampOfItsWidth()
        {
            // A land shadow along +x: envelope 0 for y > 0.37 (behind the land), 0.6 below. The line sits
            // between lattice points, as a real coast does.
            Vector2 shadow = new Vector2(1f, 0f);
            const float width = 6f;
            float Step(Vector2 p) => p.y > 0.37f ? 0f : 0.6f;
            float Soft(Vector2 p)
            {
                if (!BreakerMath.BoreLeeLattice(p, shadow, width, out var a, out var b, out float w)) return Step(p);
                Assert.That(w, Is.InRange(0f, 1f));
                Assert.AreEqual(width, (b - a).magnitude, 1e-4f);
                return Mathf.Lerp(Step(a), Step(b), w);
            }

            float prev = Soft(new Vector2(3.3f, -20f));
            float maxJump = 0f;
            for (float y = -20f; y <= 20f; y += 0.01f)
            {
                float v = Soft(new Vector2(3.3f, y));
                maxJump = Mathf.Max(maxJump, Mathf.Abs(v - prev));
                prev = v;
            }
            Assert.Less(maxJump, 0.6f * 0.01f / width * 1.5f, "the softened envelope is continuous across the line");
            Assert.AreEqual(0.6f, Soft(new Vector2(3.3f, -width - 0.5f)), 1e-5f, "open sea a width away is untouched");
            Assert.AreEqual(0f, Soft(new Vector2(3.3f, width + 0.5f)), 1e-5f, "dead lee a width away stays dead");
        }

        [Test]
        public void TheLeeLattice_IsWorldFixed_AndOffAtZero()
        {
            Assert.IsFalse(BreakerMath.BoreLeeLattice(new Vector2(1f, 2f), new Vector2(0.7f, -0.7f), 0f, out _, out _, out _));
            Assert.IsFalse(BreakerMath.BoreLeeLattice(new Vector2(1f, 2f), Vector2.zero, 6f, out _, out _, out _));
            // Two probes in one lattice cell read the same two points.
            BreakerMath.BoreLeeLattice(new Vector2(10.1f, 3f), new Vector2(0f, 1f), 6f, out var a1, out var b1, out _);
            BreakerMath.BoreLeeLattice(new Vector2(10.9f, 3f), new Vector2(0f, 1f), 6f, out var a2, out var b2, out _);
            Assert.AreEqual(Vector2.Dot(a1, new Vector2(-1f, 0f)), Vector2.Dot(a2, new Vector2(-1f, 0f)), 1e-4f);
            Assert.AreEqual(Vector2.Dot(b1, new Vector2(-1f, 0f)), Vector2.Dot(b2, new Vector2(-1f, 0f)), 1e-4f);
        }

        [Test]
        public void TheShippedConfig_CarriesTheDials()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath);
            Assert.Greater(config.Breakers.BoreLeeSoftenMeters, 0f);
            Assert.Greater(config.Breakers.BoreEdgeFadeDepthMeters, BreakerMath.MinDepthMeters);
        }

        [Test]
        public void TheShaderAndTheBridge_UseTheTwins()
        {
            string src = File.ReadAllText(WaterShaderPath);
            StringAssert.Contains("float4 _BoreLeeFade;", src);
            StringAssert.Contains("float BoreEdgeLeeFade01(float fetchEnv)", src);
            StringAssert.Contains("boreEdgeBlend *= boreEdgeLeeFade;", src);
            StringAssert.Contains("leftFoamA *= boreEdgeLeeFade;", src);
            StringAssert.Contains("float leeSoftM = max(_BoreLeeFade.x, 0.0);", src);
            StringAssert.Contains("surfFetchFactors.low = lerp(leeFa.low, leeFb.low, leeFr);", src);
            StringAssert.Contains("SurfBreaking01(SURF_EDGE_REF_DEPTH", src);
            string bridge = File.ReadAllText(BridgePath);
            StringAssert.Contains("Shader.PropertyToID(\"_BoreLeeFade\")", bridge);
            StringAssert.Contains("breakers.BoreLeeSoftenMeters", bridge);
            StringAssert.Contains("breakers.BoreEdgeFadeDepthMeters", bridge);
        }
    }
}
