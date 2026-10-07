using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// THE SHADER TWIN AGAINST THE RIG. <see cref="TreeWindMath"/> is the C# copy of
    /// <c>TreeWindMaps.hlsl</c>'s gather. Here it reads the maps the baker actually WROTE, decoded back
    /// from the PNGs, and for every pixel of every frame it must pick the same source texel that the
    /// rig's own <c>treeMaps4.shade()</c> picks from its rest frame. The hash and the constants are
    /// pinned to the maps' and the glue's own, bit for bit.
    ///
    /// <para>The pixel parity is MEASURED, not exact: the twin runs in float and the rig in double, so a
    /// texel on a rounding edge can land one texel over. The bars are 99.9% overall and 99% in the worst
    /// case. The sabotage, the same wind one frame late, lands near 45%.</para>
    /// </summary>
    public class TreeWindMathRigParityTests
    {
        /// <summary>The rig's default gust depth, which the shader's <c>_TreeGust</c> carries.</summary>
        const float Gust = 0.4f;

        static readonly (float ww, int dir)[] Winds = { (0f, 1), (0.5f, 1), (0.5f, -1), (1f, 1), (1f, -1) };
        static readonly int[] Frames = { 0, 5, 10 };
        static readonly int[] ParityVariants = { 0, 3 };

        TreePass4TempBake.Run _run;

        [OneTimeSetUp]
        public void BakeOnce() => _run = TreePass4TempBake.BakeInto("parity");

        [OneTimeTearDown]
        public void DeleteTheBake()
        {
            if (_run != null) TreePass4TempBake.DeleteFolder(_run.Folder);
        }

        static string Js(string s) => TreePass4TempBake.Js(s);

        /// <summary>
        /// For each species, season, variant 0 and 3, five winds (calm, half and full, both ways) and three
        /// frames: the twin's <see cref="TreeWindMath.SourceMap"/> over the BAKED wind and phase maps must
        /// match the rig's <c>shade().src</c> on the drawn pixels, and must fall back to the gap row on the
        /// same leaf texels as the rig's <c>st == -1</c>.
        /// </summary>
        [Test]
        public void TheTwin_GathersTheSameSourceTexelAsTheRig_FromTheBakedMaps()
        {
            _run.Require();
            var contract = TreePass4TempBake.ReadContract(_run.ContractPath);
            using var host = TreePass4TempBake.CreateHost();

            int cases = 0;
            long drawnAll = 0, agreeAll = 0, fbAll = 0, fbMisAll = 0, lateDrawn = 0, lateAgree = 0;
            double worst = 1;
            string worstAt = null;
            var log = new StringBuilder("[TreeWindParity] twin against rig (drawn px | agreement | worst case | gap-leaf fallbacks, disagreeing):\n");

            foreach (var e in contract.trees)
            {
                // The glue's bake first, then rest() and shade(): the order the harness measured in.
                TreePass4TempBake.GlueBake(host, contract, e);
                var p = new TreeWindMath.Params
                {
                    BendPx = e.wind.bendPx,
                    LimbPx = e.wind.limbPx,
                    Bob = e.wind.bob,
                    Flutter = e.wind.flutter,
                    ShimmerCalm = TreeKitCatalog.ShimmerCalm(e.wind),
                    Conifer = e.wind.conifer,
                    Gust = Gust,
                    Response = 1f,
                };

                foreach (string season in e.seasons)
                {
                    var row = TreeKitCatalog.SeasonRowFor(e, season);
                    Assert.IsNotNull(row, $"{e.species}/{season}: no season row");
                    var windPx = TreePass4TempBake.ReadSheet(
                        TreePass4TempBake.SheetOf(_run, e, row.maps, TreeKitCatalog.Channel.Wind), out int sw, out int sh);
                    var phasePx = TreePass4TempBake.ReadSheet(
                        TreePass4TempBake.SheetOf(_run, e, row.maps, TreeKitCatalog.Channel.Phase), out int pw, out int ph);
                    Assert.IsTrue(pw == sw && ph == sh, $"{e.species}/{row.maps}: the wind and phase sheets differ in size");

                    foreach (int v in ParityVariants)
                    {
                        string at = $"{e.species}/{season} v{v}";
                        var maps = new TreeWindMath.Maps(e.cellW, e.cellH,
                            TreePass4TempBake.CellFromSheet(windPx, sw, sh, e.cellW, e.cellH, v),
                            TreePass4TempBake.CellFromSheet(phasePx, sw, sh, e.cellW, e.cellH, v));
                        host.Execute($"globalThis.HHTestRF = TreeMaps4.rest({Js(e.species)}, " +
                                     $"{{ stage: {Js(e.stage)}, season: {Js(row.maps)}, variant: {v} }});");
                        Assert.AreEqual(e.cellW, (int)host.EvaluateNumber("HHTestRF.v.w"), $"{at}: the rest frame's width");
                        Assert.AreEqual(e.cellH, (int)host.EvaluateNumber("HHTestRF.v.h"), $"{at}: the rest frame's height");
                        int n = e.cellW * e.cellH;

                        long drawnHere = 0, agreeHere = 0, fbHere = 0, fbMisHere = 0;
                        double worstHere = 1;
                        foreach (var (ww, dir) in Winds)
                            foreach (int f in Frames)
                            {
                                host.Execute($"globalThis.HHTestSH = TreeMaps4.shade(HHTestRF, " +
                                             $"{{ w: {TreePass4TempBake.F(ww)}, gust: {TreePass4TempBake.F(Gust)}, dir: {dir} }}, {f});");
                                int[] rig = TreePass4TempBake.Ints(host, "HHTestSH.src");
                                int[] st = TreePass4TempBake.Ints(host, "HHTestSH.v.st");
                                Assert.AreEqual(n, rig.Length, $"{at}: shade().src");
                                Assert.AreEqual(n, st.Length, $"{at}: shade().v.st");

                                var wind = new Vector2(dir * ww, 0f);
                                var fb = new bool[n];
                                int[] twin = TreeWindMath.SourceMap(TreeWindMath.FrameOf(wind, f, p), maps, fb);
                                Assert.AreEqual(n, twin.Length, $"{at}: the twin's source map");

                                var s = Score(rig, st, twin, fb, maps);
                                cases++;
                                drawnHere += s.drawn;
                                agreeHere += s.agree;
                                fbHere += s.fb;
                                fbMisHere += s.fbMis;
                                double pct = s.drawn == 0 ? 1 : (double)s.agree / s.drawn;
                                if (pct < worstHere) worstHere = pct;
                                if (pct < worst)
                                {
                                    worst = pct;
                                    worstAt = $"{at} w {TreePass4TempBake.F(ww)} dir {dir} f {f}";
                                }

                                if (v == 0)
                                {
                                    int[] late = TreeWindMath.SourceMap(TreeWindMath.FrameOf(wind, (f + 1) % TreeWindMath.Loop, p), maps);
                                    var l = Score(rig, st, late, null, maps);
                                    lateDrawn += l.drawn;
                                    lateAgree += l.agree;
                                }
                            }

                        drawnAll += drawnHere;
                        agreeAll += agreeHere;
                        fbAll += fbHere;
                        fbMisAll += fbMisHere;
                        log.AppendLine($"  {at}: {drawnHere} | {100.0 * agreeHere / Math.Max(1, drawnHere):F3}% | " +
                                       $"{100.0 * worstHere:F3}% | {fbHere}, {fbMisHere}");
                    }
                }
                host.Execute("HHTreePass4.release(); TreeRig4.clearCache();");
            }
            host.Execute("delete globalThis.HHTestRF; delete globalThis.HHTestSH; delete globalThis.HHTreePass4Last;");

            double overall = (double)agreeAll / Math.Max(1, drawnAll);
            double fbAgree = fbAll == 0 ? 0 : (double)(fbAll - fbMisAll) / fbAll;
            double lateOverall = (double)lateAgree / Math.Max(1, lateDrawn);
            log.AppendLine($"  overall {100.0 * overall:F3}% over {drawnAll} drawn px in {cases} cases; worst {100.0 * worst:F3}% ({worstAt}); " +
                           $"gap-leaf fallbacks {fbAll}, agreeing {100.0 * fbAgree:F3}%; one frame late {100.0 * lateOverall:F1}%");
            Debug.Log(log.ToString());

            Assert.AreEqual(2 * 2 * ParityVariants.Length * Winds.Length * Frames.Length, cases, "cases");
            Assert.AreEqual(120, cases, "cases");
            Assert.GreaterOrEqual(overall, 0.999, $"The twin agrees with the rig on {100.0 * overall:F3}% of drawn pixels.");
            Assert.GreaterOrEqual(worst, 0.99, $"The worst case agrees on {100.0 * worst:F3}% ({worstAt}).");
            Assert.Greater(fbAll, 0, "No leaf texel fell back to the gap row, so the fallback went unchecked.");
            Assert.GreaterOrEqual(fbAgree, 0.999, $"The twin and the rig agree on {100.0 * fbAgree:F3}% of the gap-leaf fallbacks.");
            Assert.Less(lateOverall, 0.90,
                        $"SABOTAGE: one frame late, the twin still agrees on {100.0 * lateOverall:F1}%; the parity check cannot tell frames apart.");
        }

        /// <summary>The harness's score: drawn pixels are those either side draws, the agreement is the drawn
        /// pixels on which both pick the same source, and a fallback counts where either side sends a LEAF
        /// texel to the gap row (the rig's <c>st == -1</c>, the twin's <c>fallbackOut</c>).</summary>
        static (long drawn, long agree, long fb, long fbMis) Score(int[] rig, int[] st, int[] twin, bool[] fb,
                                                                  TreeWindMath.Maps maps)
        {
            long drawn = 0, eq = 0, fbN = 0, fbMis = 0;
            int n = rig.Length, w = maps.Width;
            for (int i = 0; i < n; i++)
            {
                if (rig[i] >= 0 || twin[i] >= 0) drawn++;
                if (rig[i] != twin[i]) continue;
                eq++;
                if (rig[i] < 0 || fb == null) continue;
                var tx = maps.At(rig[i] % w, rig[i] / w);
                bool leaf = TreeWindMath.ClassOf(tx.Word) == TreeWindMath.ClassLeaf;
                bool rigFb = st[i] == -1 && leaf;
                bool twinFb = fb[i] && leaf;
                if (rigFb || twinFb) fbN++;
                if (rigFb != twinFb) fbMis++;
            }
            return (drawn, eq - (n - drawn), fbN, fbMis);
        }

        /// <summary>The twin's integer hash is treeMaps4's <c>hsh()</c> times 2³², bit for bit, over the
        /// whole stamp-id range and every salt the gather and the flutter use.</summary>
        [Test]
        public void TheTwinsHash_IsTheMapsHash_BitForBit()
        {
            int[] ns = Enumerable.Range(0, 8192).Where(i => i % 37 == 0).Concat(new[] { -1, 0, 1, 8190, 8191 }).ToArray();
            const int Salts = 18;
            using var host = TreePass4TempBake.CreateHost();
            string list = string.Join(",", ns.Select(i => i.ToString(CultureInfo.InvariantCulture)));
            string text = host.EvaluateString(
                "(function () { var ns = [" + list + "], out = []; " +
                "for (var i = 0; i < ns.length; i++) for (var k = 0; k < " + Salts + "; k++) " +
                "out.push(Math.floor(TreeMaps4.hsh(ns[i], k) * 4294967296)); return out.join(' '); })()");
            string[] got = text.Split(' ');
            Assert.AreEqual(ns.Length * Salts, got.Length, "values");

            int bad = 0, wrongAgrees = 0, idx = 0;
            string firstBad = null;
            foreach (int n in ns)
                for (int k = 0; k < Salts; k++, idx++)
                {
                    uint rig = uint.Parse(got[idx], NumberStyles.None, CultureInfo.InvariantCulture);
                    uint twin = TreeWindMath.Hash(n, k);
                    if (rig != twin)
                    {
                        bad++;
                        firstBad ??= $"hsh({n}, {k}): the maps {rig}, the twin {twin}";
                    }
                    if (WrongHash(n, k) == rig) wrongAgrees++;
                }
            Assert.AreEqual(0, bad, $"{bad} of {got.Length} hashes differ; first {firstBad}");
            Assert.LessOrEqual(wrongAgrees, got.Length / 100,
                               $"SABOTAGE: a hash one bit off in its first multiplier still agrees on {wrongAgrees} of {got.Length}.");
            Debug.Log($"[TreeWindParity] hash: {got.Length} of {got.Length} bit for bit; the one-bit sabotage agrees on {wrongAgrees}.");
        }

        /// <summary><see cref="TreeWindMath.Hash"/> with its first multiplier one off: the sabotage.</summary>
        static uint WrongHash(int n, int k)
        {
            unchecked
            {
                uint h = ((uint)n ^ ((uint)(k + 1) * 0x9e3779b9u)) * 0x85ebca6cu;
                h ^= h >> 13;
                h *= 0xc2b2ae35u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>The twin's constants are the rig's, the maps' and the glue's own: the wave spans, the
        /// sway reach, the depth margin, the snow scale, the loop, the gap-snow floor, the four classes, the
        /// channel order and the palette width.</summary>
        [Test]
        public void TheTwinsConstants_AreTheRigsAndTheGluesOwn()
        {
            using var host = TreePass4TempBake.CreateHost();
            double N(string expr) => host.EvaluateNumber(expr);

            Assert.AreEqual((float)N("TreeMaps4.X_SPAN"), TreeWindMath.XSpan, "X_SPAN");
            Assert.AreEqual((float)N("TreeMaps4.J_SPAN"), TreeWindMath.JSpan, "J_SPAN");
            Assert.AreEqual((float)N("TreeMaps4.R_MAX"), TreeWindMath.RMax, "R_MAX");
            Assert.AreEqual((int)N("TreeMaps4.MARGIN"), TreeWindMath.Margin, "MARGIN");
            Assert.AreEqual((float)(N("TreeMaps4.SNOW_NEVER") - 1), TreeWindMath.SnowScale, "SNOW_NEVER - 1");
            Assert.AreEqual((int)N("TreeRig4.LOOP"), TreeWindMath.Loop, "LOOP");
            Assert.AreEqual((int)N("HHTreePass4.GAP_SNOW_MIN"), TreeWindMath.GapSnowMin, "GAP_SNOW_MIN");
            Assert.AreEqual((int)N("HHTreePass4.OUTSIDE"), TreeWindMath.ClassOutside, "OUTSIDE");
            Assert.AreEqual((int)N("HHTreePass4.WOOD"), TreeWindMath.ClassWood, "WOOD");
            Assert.AreEqual((int)N("HHTreePass4.BETWEEN"), TreeWindMath.ClassBetween, "BETWEEN");
            Assert.AreEqual((int)N("HHTreePass4.LEAF"), TreeWindMath.ClassLeaf, "LEAF");
            CollectionAssert.AreEqual(
                TreeKitCatalog.Pass4Channels.Select(c => TreePass4Baker.GlueChannel(c)).ToArray(),
                FishingKitBaker.ReadStringArray(host, "HHTreePass4.CHANNELS").ToArray(),
                "The glue's channels, in the catalog's order.");
            Assert.AreEqual(TreeKitCatalog.PaletteWidth, (int)N("HHTreePass4.PAL"), "PAL");
            Assert.AreEqual(TreePass4Glue.Version, (int)N("HHTreePass4.VERSION"), "VERSION");
        }
    }
}
