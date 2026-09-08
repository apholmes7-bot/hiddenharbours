using System.Text;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>REGISTER ROW 8 — why the shallows wear a lattice, and why the knob that is supposed to
    /// break it up is set to the one value that helps least.</b>
    ///
    /// <para>The painted slots are seamless tiles on Repeat wrap. <c>UntileSampleW</c> hides the grid by
    /// blending four hash-translated crops — and then hands back
    /// <c>lerp(raw, untiled, _UntileStrength)</c>. <b>That last line is the defect.</b> The raw tap is
    /// the tile grid, perfectly periodic; keeping <c>1 - s</c> of it keeps that proportion of the grid
    /// drawn, whatever the untiler did.</para>
    ///
    /// <para>And because <c>raw</c> and <c>untiled</c> are DECORRELATED, mixing them costs variance:
    /// <c>Var = (1-s)²·σ²raw + s²·σ²untiled</c>, which has its <b>minimum inside the interval</b>. So a
    /// mid-range strength is the worst of both — it keeps a third of the grid AND flattens the layer's
    /// contrast. The shipped <c>0.644</c> is mid-range.</para>
    ///
    /// <para><b>Measured on the shipped tiles</b> (offline, the real PNGs through this arithmetic, five
    /// instants each — <c>_SurfaceTex</c> at <c>_PaintScale</c>, a 4 m cell):</para>
    /// <code>
    ///   _UntileStrength | repeat autocorrelation | field contrast
    ///             0.000 |                 1.0000 |         0.1305
    ///             0.500 |                 0.7110 |         0.0802
    ///             0.644 |                 0.4706 |         0.0770   &lt;- SHIPPED, the contrast FLOOR
    ///             0.750 |                 0.3171 |         0.0793
    ///             1.000 |                 0.1927 |         0.0979
    /// </code>
    ///
    /// <para><b>1.0 is strictly better on BOTH axes than 0.644</b> — 59 % less repeat and 27 % more
    /// contrast — and it costs nothing, because a slot already pays for all five fetches at any strength
    /// above zero. ⚠️ It is still a TUNABLE and the owner's (rule 6): proposed with the table, not moved.</para>
    ///
    /// <para>The tests below carry the MECHANISM, not those numbers: they need no texture and no GPU, and
    /// they fail if the mix stops behaving the way the table is explained by.</para>
    /// </summary>
    public class WaterUntileMixTests
    {
        const float Shipped = 0.644f;      // _UntileStrength on Water.mat and all eight presets

        /// <summary>A periodic field — what the RAW tap draws: the tile, repeating exactly.</summary>
        static float Raw(float x, float periodMetres) => Mathf.Sin(2f * Mathf.PI * x / periodMetres);

        /// <summary>A field with the same variance and NO component at that period — what a perfect
        /// untiler would draw. Decorrelated from <see cref="Raw"/> by construction (an irrational
        /// frequency ratio, so no harmonic of one lands on the other).</summary>
        static float Untiled(float x, float periodMetres)
            => Mathf.Sin(2f * Mathf.PI * x / (periodMetres * Mathf.Sqrt(2f)) + 1.234f);

        static void MixStats(float s, float period, out float variance, out float repeatAmplitude)
        {
            const int N = 20000;
            const float Span = 400f;
            double sum = 0, sum2 = 0, cos = 0, sin = 0;
            for (int i = 0; i < N; i++)
            {
                float x = i * Span / N;
                float v = Mathf.Lerp(Raw(x, period), Untiled(x, period), s);
                sum += v; sum2 += (double)v * v;
                // Project onto the repeat's own frequency: the amplitude of the tile grid still drawn.
                double ang = 2.0 * Mathf.PI * x / period;
                cos += v * System.Math.Cos(ang);
                sin += v * System.Math.Sin(ang);
            }
            double mean = sum / N;
            variance = (float)(sum2 / N - mean * mean);
            repeatAmplitude = (float)(2.0 * System.Math.Sqrt(cos * cos + sin * sin) / N);
        }

        /// <summary>
        /// 🔴 <b>THE DEFECT.</b> The grid still drawn is exactly the raw tap that was kept. Not
        /// approximately, not "some of it" — <c>1 - s</c> of it, linearly, at every strength. That is why
        /// a knob at 0.644 leaves a third of the tile lattice on screen no matter how good the untiler is.
        /// </summary>
        [Test]
        public void TheRepeatStillDrawn_IsExactlyTheRawTapThatWasKept()
        {
            const float Period = 4f;                 // _PaintScale 0.25 -> a 4 m cell
            MixStats(0f, Period, out _, out float full);

            var report = new StringBuilder();
            report.AppendLine("  s      repeat amplitude   (1-s) x full");
            foreach (float s in new[] { 0f, 0.25f, 0.5f, Shipped, 0.75f, 0.9f, 1f })
            {
                MixStats(s, Period, out _, out float amp);
                report.AppendLine($"  {s:0.000}  {amp,16:0.0000}  {(1f - s) * full,14:0.0000}");
                Assert.AreEqual((1f - s) * full, amp, full * 0.02f,
                    $"at strength {s:0.000} the tile grid still drawn must be {(1f - s) * 100f:0}% of " +
                    "what the raw tiling draws. The untiler never gets a say in this term — it is the " +
                    "lerp's other end.");
            }
            TestContext.WriteLine(report.ToString());

            MixStats(Shipped, Period, out _, out float shippedAmp);
            Assert.Greater(shippedAmp / full, 0.3f,
                "⭐ ROW 8's NUMBER: at the shipped 0.644 more than a third of the tile lattice is still " +
                "drawn, by construction. Measured on the real _SurfaceTex through this same arithmetic, " +
                "that is an autocorrelation of 0.47 at exactly the 4 m cell — a grid, not a texture.");
        }

        /// <summary>
        /// 🔴 <b>AND WHY MID-RANGE IS THE WORST PLACE TO STAND.</b> <c>raw</c> and <c>untiled</c> are
        /// decorrelated, so their mix has variance <c>(1-s)²σ² + s²σ²</c> — a parabola with its floor
        /// INSIDE the interval. A strength chosen to "soften" the grid also flattens the layer, and the
        /// shipped value sits within a few percent of the floor.
        /// </summary>
        [Test]
        public void MixingTwoDecorrelatedFields_HasItsCONTRASTFLOOR_InTheMiddle()
        {
            const float Period = 4f;
            MixStats(0f, Period, out float v0, out _);
            MixStats(1f, Period, out float v1, out _);
            MixStats(0.5f, Period, out float vMid, out _);
            MixStats(Shipped, Period, out float vShipped, out _);

            TestContext.WriteLine(
                $"  contrast (std): s=0 {Mathf.Sqrt(v0):0.0000}  s=0.5 {Mathf.Sqrt(vMid):0.0000}  " +
                $"s={Shipped:0.000} {Mathf.Sqrt(vShipped):0.0000}  s=1 {Mathf.Sqrt(v1):0.0000}");

            Assert.Less(vMid, v0 * 0.75f,
                "⭐ Half-and-half must cost real contrast against either end — that is what makes a " +
                "mid-range untile strength the worst of both, and it is why the measured field contrast " +
                "bottoms out at the shipped value rather than at either extreme.");
            Assert.Less(vShipped, v1,
                "...and the shipped 0.644 must sit BELOW the full-untile end, so raising the knob buys " +
                "contrast at the same time as it removes grid. If this ever inverts, the proposal in " +
                "this file's summary is stale and must be re-measured before it is repeated.");

            // DEAD CONTROL: the parabola is a property of DECORRELATION. Mix a field with ITSELF and
            // nothing is lost — if this fails, the test above is measuring the sampler, not the mix.
            const int N = 20000;
            double s2 = 0, s1 = 0;
            for (int i = 0; i < N; i++)
            {
                float x = i * 400f / N;
                float v = Mathf.Lerp(Raw(x, Period), Raw(x, Period), 0.5f);
                s1 += v; s2 += (double)v * v;
            }
            float selfVar = (float)(s2 / N - (s1 / N) * (s1 / N));
            Assert.AreEqual(v0, selfVar, v0 * 0.01f,
                "DEAD CONTROL: mixing a field with ITSELF must lose nothing. The variance floor comes " +
                "from the two ends being decorrelated, not from the blend arithmetic.");
        }

        /// <summary>
        /// The ends are the ends: strength 0 must be the raw tiling exactly and strength 1 the untiled
        /// field exactly, or the knob does not mean what the shader's own comment says it means.
        /// </summary>
        [Test]
        public void TheKnobsEnds_AreTheTwoFieldsThemselves()
        {
            const float Period = 4f;
            for (float x = 0f; x < 20f; x += 0.37f)
            {
                Assert.AreEqual(Raw(x, Period), Mathf.Lerp(Raw(x, Period), Untiled(x, Period), 0f), 1e-6f,
                    "strength 0 is the raw tiling, untouched — the shader's single-tap path");
                Assert.AreEqual(Untiled(x, Period), Mathf.Lerp(Raw(x, Period), Untiled(x, Period), 1f), 1e-6f,
                    "strength 1 drops the raw tap entirely — which is the point of the proposal");
            }

            // And the blend the untiled half is built from is still a partition of unity, so none of
            // this changes the layer's MEAN — the property WaterUntile was written to guarantee.
            for (float fx = 0f; fx <= 1f; fx += 0.125f)
            for (float fy = 0f; fy <= 1f; fy += 0.125f)
            {
                WaterUntile.CornerWeights(new Vector2(fx, fy),
                                          out float w00, out float w10, out float w01, out float w11);
                Assert.AreEqual(1f, w00 + w10 + w01 + w11, 1e-5f,
                    $"the corner weights must still sum to 1 at ({fx:0.###}, {fy:0.###}) — raising " +
                    "_UntileStrength must not brighten or darken any slot");
            }
        }
    }
}
