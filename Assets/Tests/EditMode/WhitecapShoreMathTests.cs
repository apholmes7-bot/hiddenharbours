using System;
using System.IO;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The near-shore whitecap law (WhitecapShoreMath, twin of the water shader's ShoreCapShare): off is
    /// exact, the caps thin smoothly toward the shoreline and never jump, the crest-line turn fades where the
    /// shore field's direction is undefined, and the shader carries the same law and dials.
    /// </summary>
    public class WhitecapShoreMathTests
    {
        const string WaterShader = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const float Reach = 40f;

        [Test]
        public void WhitecapShore_DialsAtZero_AreExactlyOff()
        {
            for (float m = -50f; m <= 200f; m += 0.37f)
            foreach (float g in new[] { 0f, 0.31f, 0.5f, 1f })
            {
                Assert.AreEqual(1f, WhitecapShoreMath.Keep(m, 0f, Reach), "keep at " + m);
                Assert.AreEqual(0f, WhitecapShoreMath.AlignShare(m, g, 0f, Reach), "share at " + m);
            }
        }

        [Test]
        public void WhitecapShore_OutsideTheBand_IsUntouched()
        {
            foreach (float m in new[] { Reach, Reach + 0.01f, 80f, 500f })
            {
                Assert.AreEqual(1f, WhitecapShoreMath.Keep(m, 1f, Reach), "keep at " + m);
                Assert.AreEqual(0f, WhitecapShoreMath.AlignShare(m, 1f, 1f, Reach), "share at " + m);
            }
        }

        [Test]
        public void WhitecapShore_ThinsMonotonicallyToTheShoreline()
        {
            const float fade = 0.85f;
            float prev = WhitecapShoreMath.Keep(-10f, fade, Reach);
            Assert.That(prev, Is.EqualTo(1f - fade).Within(1e-6f), "inland and at the line the caps keep 1 - fade");
            Assert.That(WhitecapShoreMath.Keep(0f, fade, Reach), Is.EqualTo(1f - fade).Within(1e-6f));
            Assert.That(WhitecapShoreMath.Keep(Reach * 0.5f, fade, Reach), Is.EqualTo(1f - fade * 0.5f).Within(1e-6f), "half way");
            for (float m = -10f; m <= Reach + 5f; m += 0.25f)
            {
                float k = WhitecapShoreMath.Keep(m, fade, Reach);
                Assert.GreaterOrEqual(k, prev - 1e-7f, "never thicker toward the shore, at " + m);
                prev = k;
            }
        }

        [Test]
        public void WhitecapShore_ThinsOutNeverSnaps()
        {
            // The steepest slope of a smoothstep over the reach is 1.5 / reach, so 0.5 m of water can change the
            // kept share by at most 0.75 * 0.5 / 40 ~ 0.019 at full fade: a thinning, not an edge.
            float maxStep = 0f, maxShareStep = 0f;
            for (float m = -5f; m <= Reach + 5f; m += 0.5f)
            {
                maxStep = Mathf.Max(maxStep, Mathf.Abs(WhitecapShoreMath.Keep(m + 0.5f, 1f, Reach) - WhitecapShoreMath.Keep(m, 1f, Reach)));
                maxShareStep = Mathf.Max(maxShareStep, Mathf.Abs(WhitecapShoreMath.AlignShare(m + 0.5f, 1f, 1f, Reach) - WhitecapShoreMath.AlignShare(m, 1f, 1f, Reach)));
            }
            Assert.That(maxStep, Is.LessThanOrEqualTo(1.5f * 0.5f / Reach + 1e-5f));
            Assert.That(maxShareStep, Is.LessThanOrEqualTo(1.5f * 0.5f / Reach + 1e-5f));
            // and across the field's gradient collapse (the ridge behind a headland) the turn fades smoothly
            float maxGradStep = 0f;
            for (float g = 0f; g < 1f; g += 0.05f)
                maxGradStep = Mathf.Max(maxGradStep, Mathf.Abs(WhitecapShoreMath.AlignShare(0f, g + 0.05f, 1f, Reach) - WhitecapShoreMath.AlignShare(0f, g, 1f, Reach)));
            Assert.That(maxGradStep, Is.LessThan(0.16f));
        }

        [Test]
        public void WhitecapShore_TurnFadesWhereTheShoreDirectionIsUndefined()
        {
            Assert.AreEqual(0f, WhitecapShoreMath.AlignShare(5f, 0f, 1f, Reach));
            Assert.AreEqual(0f, WhitecapShoreMath.AlignShare(5f, WhitecapShoreMath.GradientGateLow, 1f, Reach));
            float full = WhitecapShoreMath.NearShore(5f, Reach);
            Assert.That(WhitecapShoreMath.AlignShare(5f, 1f, 1f, Reach), Is.EqualTo(full).Within(1e-6f));
            Assert.That(WhitecapShoreMath.AlignShare(5f, 1f, 0.75f, Reach), Is.EqualTo(0.75f * full).Within(1e-6f));
        }

        [Test]
        public void WhitecapShore_ThinningIsByArea_ExactAtFullKeep_AndFollowsTheShare()
        {
            for (float n = 0f; n <= 1f; n += 0.01f)
            {
                Assert.AreEqual(1f, WhitecapShoreMath.Thin(n, 1f), "keep 1 keeps every streak");
                Assert.AreEqual(0f, WhitecapShoreMath.Dissolve(n, 0f), "share 0 turns none");
                Assert.AreEqual(1f, WhitecapShoreMath.Dissolve(n, 1f), "share 1 turns all");
            }
            // over a uniform noise the kept area tracks the share (never all-or-nothing) and grows with it
            float prev = -1f;
            for (float s = 0.05f; s < 1f; s += 0.05f)
            {
                float area = 0f; const int steps = 2000;
                for (int i = 0; i < steps; i++) area += WhitecapShoreMath.Dissolve((i + 0.5f) / steps, s);
                area /= steps;
                Assert.That(area, Is.EqualTo(s).Within(0.02f), "area at share " + s);
                Assert.Greater(area, prev, "more share, more area");
                prev = area;
            }
            // and no streak snaps: a small change of share moves any one streak's gate by a bounded step
            float maxStep = 0f;
            for (float n = 0f; n <= 1f; n += 0.01f)
                for (float s = 0.01f; s < 0.98f; s += 0.01f)
                    maxStep = Mathf.Max(maxStep, Mathf.Abs(WhitecapShoreMath.Dissolve(n, s + 0.01f) - WhitecapShoreMath.Dissolve(n, s)));
            Assert.That(maxStep, Is.LessThan(0.01f * 1.5f / (2f * WhitecapShoreMath.EdgeSoft) + 1e-4f));
        }

        [Test]
        public void WhitecapShore_ShaderCarriesTheSameLawAndDials()
        {
            string src = Compact(File.ReadAllText(WaterShader));
            StringAssert.Contains("_ShoreCapFade(", src);
            StringAssert.Contains("_ShoreCapAlign(", src);
            StringAssert.Contains("float_ShoreCapFade;", src);
            StringAssert.Contains("float_ShoreCapAlign;", src);
            StringAssert.Contains("floatnearShore=1.0-smoothstep(0.0,max(_ShoreWaveReach,1e-3),field.r);", src);
            StringAssert.Contains("keep=1.0-saturate(_ShoreCapFade)*nearShore;", src);
            StringAssert.Contains("returnsaturate(_ShoreCapAlign)*nearShore*smoothstep(SHORE_CAP_GRADIENT_LO,SHORE_CAP_GRADIENT_HI,gl);", src);
            StringAssert.Contains("#defineSHORE_CAP_GRADIENT_LO" + F(WhitecapShoreMath.GradientGateLow), src);
            StringAssert.Contains("#defineSHORE_CAP_GRADIENT_HI" + F(WhitecapShoreMath.GradientGateHigh), src);
            StringAssert.Contains("#defineSHORE_CAP_EDGE_SOFT" + F(WhitecapShoreMath.EdgeSoft), src);
            StringAssert.Contains("returnsmoothstep(1.0-s-SHORE_CAP_EDGE_SOFT,1.0-s+SHORE_CAP_EDGE_SOFT,n);", src);
            StringAssert.Contains("if(keep>=1.0)return1.0;returnShoreCapDissolve(n,keep);", src);
            // both dials 0 returns before the field is read: the caps draw exactly as before
            StringAssert.Contains("if(_ShoreWaveOn<=0.0||(_ShoreCapFade<=0.0&&_ShoreCapAlign<=0.0))return0.0;", src);

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(WaterShader);
            Assert.IsNotNull(shader);
            var bare = new Material(shader);
            try
            {
                var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Water.mat");
                Assert.IsNotNull(water);
                foreach (var m in new[] { bare, water })
                {
                    Assert.That(m.GetFloat("_ShoreCapFade"), Is.InRange(0f, 1f));
                    Assert.That(m.GetFloat("_ShoreCapAlign"), Is.InRange(0f, 1f));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(bare); }
        }

        static string F(float v) => v.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture);
        static string Compact(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"\s+", "");
    }
}
