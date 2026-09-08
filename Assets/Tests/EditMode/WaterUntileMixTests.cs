using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>REGISTER ROW 8 — why the shallows wore a lattice, and the knob that was set to the one
    /// value that helped least. ✅ RULED AND SHIPPED (owner, 2026-09-08): <c>_UntileStrength</c> 0.644
    /// → 1.0 on all nine water materials.</b>
    ///
    /// <para>The painted slots are seamless tiles on Repeat wrap. <c>UntileSampleW</c> hides the grid by
    /// blending four hash-translated crops — and then hands back
    /// <c>lerp(raw, untiled, _UntileStrength)</c>. <b>That last line was the defect.</b> The raw tap is
    /// the tile grid, perfectly periodic; keeping <c>1 - s</c> of it keeps that proportion of the grid
    /// drawn, whatever the untiler did.</para>
    ///
    /// <para>And because <c>raw</c> and <c>untiled</c> are DECORRELATED, mixing them costs variance:
    /// <c>Var = (1-s)²·σ²raw + s²·σ²untiled</c>, which has its <b>minimum inside the interval</b>. So a
    /// mid-range strength was the worst of both — it kept a third of the grid AND flattened the layer's
    /// contrast. The superseded <c>0.644</c> was mid-range.</para>
    ///
    /// <para><b>Measured on the shipped tiles</b> (offline, the real PNGs through this arithmetic, five
    /// instants each — <c>_SurfaceTex</c> at <c>_PaintScale</c>, a 4 m cell):</para>
    /// <code>
    ///   _UntileStrength | repeat autocorrelation | field contrast
    ///             0.000 |                 1.0000 |         0.1305
    ///             0.500 |                 0.7110 |         0.0802
    ///             0.644 |                 0.4706 |         0.0770   &lt;- SUPERSEDED, the contrast FLOOR
    ///             0.750 |                 0.3171 |         0.0793
    ///             1.000 |                 0.1927 |         0.0979   &lt;- SHIPPED
    /// </code>
    ///
    /// <para><b>1.0 is strictly better on BOTH axes than 0.644</b> — 59 % less repeat and 27 % more
    /// contrast — and it costs nothing, because a slot already pays for all five fetches at any strength
    /// above zero (the raw tap at <c>s = 1</c> is computed and discarded, not saved).</para>
    ///
    /// <para>⚠️ <b>Why 0.644 existed, and why that reason had expired.</b> Before #443 the blend used two
    /// variants and jumped at every cell boundary; dialling the strength back was the only way to hide
    /// that seam. #443 removed the seam structurally. The number outlived the thing it was compensating
    /// for — this repo's "a number stops being true when you edit what it measured", again.</para>
    ///
    /// <para>The first three tests carry the MECHANISM, not those numbers: they need no texture and no
    /// GPU, and they fail if the mix stops behaving the way the table is explained by. The fourth reads
    /// the materials, so a preset copy cannot quietly put 0.644 back.</para>
    /// </summary>
    public class WaterUntileMixTests
    {
        /// <summary>What ships since the owner's 2026-09-08 ruling.</summary>
        const float Shipped = 1f;

        /// <summary>⚠️ The value that shipped UNTIL that ruling. Kept NAMED rather than deleted because
        /// two guards below exist to say why it moved, and a bare 0.644 sitting in a test would read as
        /// a current fact within a year.</summary>
        const float Superseded = 0.644f;

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
        /// 🔴 <b>THE DEFECT, AND WHY THE FIX IS THE KNOB'S END.</b> The grid still drawn is exactly the
        /// raw tap that was kept. Not approximately, not "some of it" — <c>1 - s</c> of it, linearly, at
        /// every strength. That is why 0.644 left a third of the tile lattice on screen however good the
        /// untiler was, and why 1.0 leaves none of it.
        /// </summary>
        [Test]
        public void TheRepeatStillDrawn_IsExactlyTheRawTapThatWasKept()
        {
            const float Period = 4f;                 // _PaintScale 0.25 -> a 4 m cell
            MixStats(0f, Period, out _, out float full);

            var report = new StringBuilder();
            report.AppendLine("  s      repeat amplitude   (1-s) x full");
            foreach (float s in new[] { 0f, 0.25f, 0.5f, Superseded, 0.75f, 0.9f, Shipped })
            {
                MixStats(s, Period, out _, out float amp);
                report.AppendLine($"  {s:0.000}  {amp,16:0.0000}  {(1f - s) * full,14:0.0000}");
                Assert.AreEqual((1f - s) * full, amp, full * 0.02f,
                    $"at strength {s:0.000} the tile grid still drawn must be {(1f - s) * 100f:0}% of " +
                    "what the raw tiling draws. The untiler never gets a say in this term — it is the " +
                    "lerp's other end.");
            }
            TestContext.WriteLine(report.ToString());

            MixStats(Superseded, Period, out _, out float wasDrawn);
            MixStats(Shipped, Period, out _, out float nowDrawn);

            Assert.Greater(wasDrawn / full, 0.3f,
                "⭐ ROW 8's NUMBER, kept as the record of why the knob moved: at the superseded 0.644 " +
                "more than a third of the tile lattice was still drawn, by construction. Measured on " +
                "the real _SurfaceTex through this same arithmetic, that was an autocorrelation of 0.47 " +
                "at exactly the 4 m cell — a grid, not a texture.");

            Assert.Less(nowDrawn / full, 0.02f,
                "⭐ AND WHAT THE RULING BOUGHT: at the shipped 1.0 the raw tap is dropped entirely, so " +
                "this term is gone. What remains of the repeat in the real layer (0.19) is the " +
                "untiler's own structural residue, not the lerp's.");
        }

        /// <summary>
        /// 🔴 <b>AND WHY MID-RANGE WAS THE WORST PLACE TO STAND.</b> <c>raw</c> and <c>untiled</c> are
        /// decorrelated, so their mix has variance <c>(1-s)²σ² + s²σ²</c> — a parabola with its floor
        /// INSIDE the interval. A strength chosen to "soften" the grid also flattened the layer, and the
        /// superseded value sat within a few percent of that floor.
        /// </summary>
        [Test]
        public void MixingTwoDecorrelatedFields_HasItsCONTRASTFLOOR_InTheMiddle()
        {
            const float Period = 4f;
            MixStats(0f, Period, out float v0, out _);
            MixStats(Shipped, Period, out float v1, out _);
            MixStats(0.5f, Period, out float vMid, out _);
            MixStats(Superseded, Period, out float vWas, out _);

            TestContext.WriteLine(
                $"  contrast (std): s=0 {Mathf.Sqrt(v0):0.0000}  s=0.5 {Mathf.Sqrt(vMid):0.0000}  " +
                $"s={Superseded:0.000} {Mathf.Sqrt(vWas):0.0000}  s={Shipped:0.000} {Mathf.Sqrt(v1):0.0000}");

            Assert.Less(vMid, v0 * 0.75f,
                "⭐ Half-and-half must cost real contrast against either end — that is what made a " +
                "mid-range untile strength the worst of both, and it is why the measured field contrast " +
                "bottomed out at the superseded value rather than at either extreme.");

            Assert.Less(vWas, v1,
                "⭐ THE RULING'S SECOND HALF: the superseded 0.644 must sit BELOW the shipped 1.0 in " +
                "contrast, so raising the knob bought contrast at the same time as it removed grid. " +
                "⚠️ This guard is deliberately written about the OLD value against the NEW one. Written " +
                "the obvious way — 'the shipped value beats the full-untile end' — it would compare 1.0 " +
                "against itself and redden on its own fix.");

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
                    "strength 1 drops the raw tap entirely — which is what the ruling ships");
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

        // =============================================================================================
        //  The ruling, pinned to the assets it changed
        // =============================================================================================

        static readonly string[] WaterMaterials =
        {
            "Assets/_Project/Art/Materials/Water.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_DeepBlue.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_FoggySmother.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_GlassyCalm.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_NorthAtlantic.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_StirredBrown.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_StormGrey.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_Tropical.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_WarmShelter.mat",
        };

        /// <summary>
        /// ✅ <b>THE RULING, ON THE ASSETS.</b> All nine water materials must serialize
        /// <c>_UntileStrength</c> at 1. ⚠️ <c>Apply water preset</c> is a wholesale copy (register row
        /// 3), so a preset that quietly kept 0.644 would stamp it back over the live material the next
        /// time the owner changed the sea's mood — which is exactly how this value would come back.
        /// </summary>
        [Test]
        public void EveryWaterMaterial_CarriesTheRuledUntileStrength()
        {
            string root = Path.Combine(Application.dataPath, "..");
            if (!Directory.Exists(Path.Combine(root, "Assets", "_Project", "Art", "Materials")))
            {
                Assert.Ignore("the project's materials are not reachable from here — this guard reads " +
                              "the .mat files and runs only where they are (EditMode / CI), never in " +
                              "the headless DLL harness.");
            }

            var report = new StringBuilder();
            foreach (string rel in WaterMaterials)
            {
                string path = Path.Combine(root, rel);
                Assert.IsTrue(File.Exists(path), $"missing water material: {rel}");
                var m = Regex.Match(File.ReadAllText(path), @"-\s_UntileStrength:\s*(-?[\d.eE+]+)");
                Assert.IsTrue(m.Success, $"{rel} does not serialize _UntileStrength at all — an absent " +
                                         "key stamps 0 through the preset copy, which is the raw tile grid.");
                float v = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                report.AppendLine($"  {Path.GetFileName(rel),-26} {v:0.###}");
                Assert.AreEqual(Shipped, v, 1e-4f,
                    $"{Path.GetFileName(rel)} carries _UntileStrength {v:0.###}. The owner ruled 1.0 on " +
                    "2026-09-08; 0.644 leaves a third of the tile lattice drawn AND sits at the layer's " +
                    "contrast floor (register row 8, water-rendering.md §42).");
            }
            TestContext.WriteLine(report.ToString());
        }
    }
}
