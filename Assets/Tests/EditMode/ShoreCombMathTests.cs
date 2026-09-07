using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>THE INSTRUMENT, PROVED AGAINST A COMB IT ALREADY KNOWS THE ANSWER TO.</b>
    ///
    /// <para>Register rows 9 + 10 cost one editor slot to a fixture that could not see the defect: its
    /// transect ran across the shoreline instead of along it, and every arm returned the same number.
    /// The charter's own law is that a guard must be able to <b>represent</b> the defect — <i>"a fixture
    /// that samples only texel centres photographs a clean shore"</i>. So before another slot is spent,
    /// the metric is fed synthetic combs of KNOWN tooth length and must recover them.</para>
    ///
    /// <para>The shipped seabed grid is 256 texels over a 760 m region — <b>2.97 m per texel</b> — so
    /// 2.97 m is the tooth this instrument must see, and 1.48 m (a 512 grid) and 0.74 m (1024) are the
    /// ones it must tell apart from it. Those three numbers are the whole point of the resolution arm.</para>
    ///
    /// <para>All pure arithmetic: no scene, no clock, no graphics device.</para>
    /// </summary>
    public class ShoreCombMathTests
    {
        /// <summary>The contour is walked at this spacing; it must be well under the smallest tooth the
        /// sweep needs to resolve (0.74 m at a 1024 grid), or the instrument aliases the very thing it
        /// is measuring.</summary>
        const float Spacing = 0.10f;

        /// <summary>What counts as "the edge did not move" — metres. Above the render's noise, well
        /// below a comb's step.</summary>
        const float StepTolerance = 0.02f;

        /// <summary>A comb: the drawn edge holds still for one texel, then jumps by a step.</summary>
        static float[] SyntheticComb(float toothMetres, float stepMetres, int samples)
        {
            var d = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                int tooth = Mathf.FloorToInt(i * Spacing / toothMetres);
                d[i] = (tooth % 2 == 0 ? 0f : stepMetres);
            }
            return d;
        }

        [Test]
        public void TheInstrument_RecoversAToothItWasGiven_AtEveryGridTheSweepShoots()
        {
            // 760 m over 256 / 512 / 1024 texels — the three arms, and the numbers that must be told apart.
            foreach (float tooth in new[] { 760f / 256f, 760f / 512f, 760f / 1024f })
            {
                float[] d = SyntheticComb(tooth, 0.25f, 4000);
                float mean = ShoreCombMath.MeanToothLengthMetres(d, Spacing, StepTolerance);
                float period = ShoreCombMath.DominantPeriodMetres(d, Spacing);

                TestContext.WriteLine($"  a {tooth:0.00} m tooth reads: mean run {mean:0.00} m, " +
                                      $"dominant period {period:0.00} m (a full cycle is {2 * tooth:0.00} m)");

                Assert.AreEqual(tooth, mean, tooth * 0.15f,
                    $"The mean run length must recover a {tooth:0.00} m tooth to within 15%. If it " +
                    "cannot, the sweep cannot tell a 256 grid from a 512 one and the resolution arm is " +
                    "worthless.");
                // The signal alternates, so one PERIOD is two teeth.
                Assert.AreEqual(2f * tooth, period, tooth * 0.35f,
                    "...and the autocorrelation's first crest must land on the repeat, not a harmonic.");
            }
        }

        /// <summary>
        /// 🔴 <b>THE DEAD CONTROL the first fixture never had.</b> A smooth edge — one that moves a
        /// little at every sample — must NOT report a tooth. Without this the instrument could return
        /// "2.97 m" for a clean shore and nobody would know.
        /// </summary>
        [Test]
        public void ASmoothEdge_ReportsNoTooth_AndNoPeriod()
        {
            var smooth = new float[4000];
            for (int i = 0; i < smooth.Length; i++)
                smooth[i] = 0.4f * Mathf.Sin(i * Spacing * 0.07f);      // a long gentle wander, no steps

            float mean = ShoreCombMath.MeanToothLengthMetres(smooth, Spacing, StepTolerance);
            TestContext.WriteLine($"  a smooth wandering edge reads a mean run of {mean:0.000} m " +
                                  $"(one sample is {Spacing:0.00} m)");
            Assert.AreEqual(0f, mean, 0f,
                "A smooth edge has NO STEPS, so it must report NO COMB. Counting runs alone reported a " +
                "400 m tooth here — the instrument calling a slow wander one enormous tooth — which is " +
                "exactly the false positive that would have made every sweep arm look combed.");
        }

        /// <summary>A constant offset is a BIAS, not a comb: the drawn edge sitting 0.3 m seaward of the
        /// true contour everywhere is a different defect, and must not be reported as a period.</summary>
        [Test]
        public void AConstantOffset_IsNotAComb()
        {
            var biased = new float[2000];
            for (int i = 0; i < biased.Length; i++) biased[i] = 0.3f;
            Assert.AreEqual(0f, ShoreCombMath.DominantPeriodMetres(biased, Spacing), 1e-4f,
                "A bias has no repeat; reporting one would make an offset shoreline look like a comb.");
        }

        /// <summary>
        /// Row 10's wall: a posterised fall must report both its WIDTH and its step count, and a smooth
        /// fall of the same width must report the same width with far fewer steps — otherwise the
        /// instrument cannot tell "a wall" from "a gradient".
        /// </summary>
        [Test]
        public void TheWall_ReportsItsWidthAndItsSteps_AndTellsAGradientFromAStaircase()
        {
            const int n = 400;                       // 40 m of profile at 0.1 m
            var stair = new float[n];
            var ramp = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = Mathf.Clamp01((i - 100) / 100f);          // the fall occupies 10 m
                ramp[i] = 1f - t;
                stair[i] = 1f - Mathf.Floor(t * 6f) / 6f;           // six bands, the shipped count
            }

            float rampWidth = ShoreCombMath.WallWidthMetres(ramp, Spacing, 0.02f, out int rampSteps);
            float stairWidth = ShoreCombMath.WallWidthMetres(stair, Spacing, 0.02f, out int stairSteps);
            TestContext.WriteLine($"  a smooth 10 m fall: {rampWidth:0.0} m wide, {rampSteps} steps");
            TestContext.WriteLine($"  a 6-band fall:      {stairWidth:0.0} m wide, {stairSteps} steps");

            Assert.AreEqual(8f, rampWidth, 1.5f, "the 90->10% width of a 10 m linear fall is 8 m");
            Assert.AreEqual(rampWidth, stairWidth, 2f,
                "Both falls occupy the same distance — WIDTH alone cannot tell a wall from a gradient, " +
                "which is why the step count is reported beside it.");
            Assert.LessOrEqual(stairSteps, 8,
                "A six-band fall crosses about six steps; many more means the tolerance is counting noise.");
            Assert.Less(rampSteps, stairSteps,
                "DEAD CONTROL: a smooth fall must cross FEWER steps than a posterised one, or the step " +
                "count is not measuring posterisation.");
        }
    }
}
