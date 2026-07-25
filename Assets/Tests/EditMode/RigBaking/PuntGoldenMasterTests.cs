using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// THE ACCEPTANCE TEST FOR THE WHOLE BAKER.
    ///
    /// Everything else in this suite checks the baker against our own constants. This one checks it
    /// against REAL SHIPPED PIXELS: bake the punt at 8 facings and diff the result with
    /// Assets/_Project/Art/Boats/PuntIso.png, which the art director hand-exported from a browser
    /// long before any of this existed. Equivalence — modulo the deliberate counter-clockwise
    /// correction, which the baker applies and the shipped sheet does not — is the strongest
    /// evidence available that the bake path is faithful.
    ///
    /// ⚠️ IF THIS DOES NOT MATCH, DO NOT ASSUME THE BAKER IS BROKEN. docs/art/rigs/README.md records
    /// that the imported puntIsoRig.js DIFFERS (md5) from the older copy that previously lived at
    /// docs/art/punt-iso-rig/ — the shipped PNG may simply have been baked from the older rig.
    /// Establish which before chasing a phantom bug.
    ///
    /// ⚠️ AS OF 2026-07-25 THAT IS EXACTLY THE STATE. The small-craft rig kit v2 revised the punt
    /// (oar layer, floor battens, breasthook, quarter knees, two ring bolts, paint-as-data), so the
    /// shipped PNG is one revision behind and the paired cells now sit at ~98.2% rather than ~100%.
    /// The assertion that discriminates a rig revision from a broken bake is therefore the SPREAD
    /// across cells, not the absolute match — see the block in the test body.
    /// </summary>
    public class PuntGoldenMasterTests
    {
        const string ShippedSheet = "Assets/_Project/Art/Boats/PuntIso.png";
        const int Facings = 8, CellW = 184, CellH = 168;

        /// <summary>Per-channel difference below which two pixels count as the same. The rigs use
        /// ordered dither, so a hair of slack is honest; this is tight enough that a genuinely
        /// different render cannot sneak through.</summary>
        const int ChannelTolerance = 2;

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        [Test]
        public void BakedPunt_MatchesTheShippedSheet_ModuloTheCounterClockwiseCorrection()
        {
            // --- bake to scratch; the shipped art is never touched ---------------------------
            string outFolder = "artifacts/rig-bake-test";
            Directory.CreateDirectory(Path.Combine(RepoRoot, outFolder));
            var result = RigBaker.Bake(new BakeRequest("punt", Facings, rockFrames: 0,
                                                       outFolder, "PuntIsoGolden"));

            Assert.AreEqual(1, result.Pages.Count, "8 facings should fit on one page.");
            var page = result.Pages[0];

            byte[] bakedPng = File.ReadAllBytes(Path.Combine(RepoRoot, page.AssetPath));
            byte[] shippedPng = File.ReadAllBytes(Path.Combine(RepoRoot, ShippedSheet));

            var baked = Decode(bakedPng);
            var shipped = Decode(shippedPng);

            try
            {
                Debug.Log($"[golden-master] baked {baked.width}×{baked.height}, " +
                          $"shipped {shipped.width}×{shipped.height}");

                Assert.AreEqual(Facings * CellW, shipped.width,
                    "The shipped sheet is not 8 cells wide — the fixture's assumptions are stale.");
                Assert.AreEqual(CellH, shipped.height);

                // The baker lays 8 cells as 8 columns × 1 row, same as the shipped sheet.
                Assert.AreEqual(shipped.width, baked.width,
                    "Baked sheet width differs — layout, not pixels, is the first suspect.");
                Assert.AreEqual(shipped.height, baked.height);

                var a = baked.GetPixels32();
                var b = shipped.GetPixels32();

                // ── THE POINT OF THIS TEST ────────────────────────────────────────────────────
                //
                // The baked sheet must NOT match the shipped sheet cell-for-cell, and if it ever
                // does, something has broken. The shipped PNG was hand-exported in the rig's RAW
                // order — cell i is literally render(i) — which is why it needs
                // BoatVisualDef.FacingsAreCounterClockwise = true to be corrected at RUNTIME. The
                // baker instead applies the correction at BAKE time, emitting cell k as
                // render((8−k)%8).
                //
                // So the two sheets should be related by exactly that permutation:
                //
                //     baked cell k  ==  shipped cell (8−k)%8
                //
                // Asserting the PAIRED comparison is what proves the whole pipeline at once: the
                // rig executes identically under V8 as it did in the art director's browser, the
                // pixel path is faithful, AND the counter-clockwise correction is applied in the
                // right direction. A naive same-index comparison, by contrast, "fails" for the
                // healthiest possible reason.
                // ──────────────────────────────────────────────────────────────────────────────

                double worstPaired = 100.0, bestPaired = 0.0, bestNaive = 0.0;
                for (int k = 0; k < Facings; k++)
                {
                    int shippedCell = (Facings - k) % Facings;
                    var paired = CompareCellPair(a, b, baked.width, k, shippedCell);
                    var naive  = CompareCellPair(a, b, baked.width, k, k);

                    Debug.Log($"[golden-master] baked cell {k} vs shipped cell {shippedCell}: " +
                              $"{paired.MatchPercent:F2}% match, mean delta {paired.MeanDelta:F3}" +
                              $"   |   naive same-index: {naive.MatchPercent:F2}%");

                    worstPaired = Math.Min(worstPaired, paired.MatchPercent);
                    bestPaired  = Math.Max(bestPaired,  paired.MatchPercent);
                    if (k != shippedCell) bestNaive = Math.Max(bestNaive, naive.MatchPercent);
                }

                double spread = bestPaired - worstPaired;
                Debug.Log($"[golden-master] paired cells {worstPaired:F2}%..{bestPaired:F2}% " +
                          $"(spread {spread:F2} pp), best off-axis naive cell {bestNaive:F2}%");

                // ⚠️ THE SHIPPED SHEET IS NOW ONE RIG REVISION BEHIND, AND THAT IS DELIBERATE ART.
                //
                // PuntIso.png was hand-exported from the pre-v2 puntIsoRig.js. The small-craft rig
                // kit v2 (2026-07-25) gave the punt an oar layer, floor battens, a breasthook,
                // quarter knees, two ring bolts and paint-as-data, so the current rig no longer
                // draws the boat in that PNG. MEASURED at import: 1.78% of her pixels move across
                // the 8 headings — i.e. ~98.2% agreement, which is exactly what the paired cells
                // now report.
                //
                // This fixture's own triage rule (kept verbatim below) says how to read that:
                // "If ALL cells drift by a similar small amount, that rig revision is the
                // explanation, not a bug. If ONE cell is wrong, the correction is." So the
                // discriminating assertion is no longer the absolute match — it is the SPREAD.
                // Measured 2026-07-25: cells span 98.08%..98.42%, a spread of 0.34 pp. A broken
                // permutation puts one cell near the off-axis figure (~70-85%) and blows the
                // spread open by ~28 pp; a uniform art revision cannot.
                //
                // The absolute floor stays as a guard with real bite (0.58 pp of headroom over
                // the measured worst cell), so a bake that degrades further still goes red.
                //
                // To get the STRONG form back, the art director re-exports PuntIso.png from the
                // v2 rig — it must stay a hand export from his browser, because re-baking it in
                // engine would make this test compare the baker against itself.
                Assert.Greater(worstPaired, 97.5,
                    $"Worst paired cell is only {worstPaired:F2}%, below the floor a known rig " +
                    "revision explains. The baked art does not reproduce the shipped art under " +
                    "the counter-clockwise permutation.\n" +
                    "⚠️ Before assuming the baker is broken: docs/art/rigs/README.md records that " +
                    "the imported puntIsoRig.js differs by md5 from the older copy that used to " +
                    "live at docs/art/punt-iso-rig/. If ALL cells drift by a similar small amount, " +
                    "that rig revision is the explanation, not a bug. If ONE cell is wrong, the " +
                    "correction is.");

                Assert.Less(spread, 0.75,
                    $"The paired cells disagree with the shipped sheet by UNEVEN amounts " +
                    $"({worstPaired:F2}%..{bestPaired:F2}%, spread {spread:F2} pp). A rig revision " +
                    "moves every heading by about the same amount; a single bad cell means the " +
                    "counter-clockwise correction, the facing order, or one cell's bake is wrong. " +
                    "Find the outlier cell in the per-cell log above — that is the broken one.");

                // And the correction must actually be doing something: off-axis cells must NOT
                // line up at the same index. (N and S sit on the mirror axis and legitimately do,
                // which is why they are excluded from bestNaive.)
                Assert.Less(bestNaive, 95.0,
                    $"An off-axis cell matched the shipped sheet at its own index ({bestNaive:F2}%). " +
                    "That means the counter-clockwise correction was NOT applied — the baker is " +
                    "reproducing the raw, uncorrected export.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
                UnityEngine.Object.DestroyImmediate(shipped);
            }
        }

        /// <summary>Loading the committed PNG into a throwaway Texture2D reads its pixels without
        /// flipping isReadable on the shipped asset.</summary>
        static Texture2D Decode(byte[] png)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            Assert.IsTrue(t.LoadImage(png, markNonReadable: false), "Failed to decode PNG.");
            return t;
        }

        readonly struct Diff
        {
            public readonly double MatchPercent, MeanDelta, AlphaAgreePercent;
            public Diff(double m, double d, double a) { MatchPercent = m; MeanDelta = d; AlphaAgreePercent = a; }
        }

        static Diff Compare(Color32[] a, Color32[] b)
        {
            long match = 0, alphaAgree = 0, sum = 0;
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                int dr = Math.Abs(a[i].r - b[i].r), dg = Math.Abs(a[i].g - b[i].g);
                int db = Math.Abs(a[i].b - b[i].b), da = Math.Abs(a[i].a - b[i].a);
                sum += dr + dg + db + da;
                if (dr <= ChannelTolerance && dg <= ChannelTolerance &&
                    db <= ChannelTolerance && da <= ChannelTolerance) match++;
                if ((a[i].a >= 128) == (b[i].a >= 128)) alphaAgree++;
            }
            return new Diff(100.0 * match / n, sum / (4.0 * n), 100.0 * alphaAgree / n);
        }

        /// <summary>Compares cell <paramref name="cellA"/> of the baked sheet against cell
        /// <paramref name="cellB"/> of the shipped sheet. The two indices differ on purpose — see
        /// the permutation argument in the test body.</summary>
        static Diff CompareCellPair(Color32[] a, Color32[] b, int sheetW, int cellA, int cellB)
        {
            long match = 0, alphaAgree = 0, sum = 0, n = 0;
            int ax0 = cellA * CellW, bx0 = cellB * CellW;
            for (int y = 0; y < CellH; y++)
            for (int x = 0; x < CellW; x++)
            {
                int ia = y * sheetW + ax0 + x;
                int ib = y * sheetW + bx0 + x;
                int dr = Math.Abs(a[ia].r - b[ib].r), dg = Math.Abs(a[ia].g - b[ib].g);
                int db = Math.Abs(a[ia].b - b[ib].b), da = Math.Abs(a[ia].a - b[ib].a);
                sum += dr + dg + db + da;
                if (dr <= ChannelTolerance && dg <= ChannelTolerance &&
                    db <= ChannelTolerance && da <= ChannelTolerance) match++;
                if ((a[ia].a >= 128) == (b[ib].a >= 128)) alphaAgree++;
                n++;
            }
            return new Diff(100.0 * match / n, sum / (4.0 * n), 100.0 * alphaAgree / n);
        }
    }
}
