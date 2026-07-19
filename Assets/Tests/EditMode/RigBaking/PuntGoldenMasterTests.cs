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

                // --- whole-sheet similarity ---------------------------------------------------
                var whole = Compare(a, b);
                Debug.Log($"[golden-master] whole sheet: {whole.MatchPercent:F2}% of pixels within " +
                          $"±{ChannelTolerance}, mean channel delta {whole.MeanDelta:F2}, " +
                          $"alpha silhouette agreement {whole.AlphaAgreePercent:F2}%");

                // --- per-cell, so a single bad facing is named -------------------------------
                for (int k = 0; k < Facings; k++)
                {
                    var cell = CompareCell(a, b, baked.width, k);
                    Debug.Log($"[golden-master] cell {k}: {cell.MatchPercent:F2}% match, " +
                              $"mean delta {cell.MeanDelta:F2}, alpha {cell.AlphaAgreePercent:F2}%");
                }

                // The silhouette is the load-bearing claim: same hull, same projection, same
                // orientation per cell. Shading may drift between rig revisions; the outline
                // should not.
                Assert.Greater(whole.AlphaAgreePercent, 99.0,
                    "The baked silhouette does not match the shipped sheet. Before suspecting the " +
                    "baker, check whether the shipped PNG was baked from the OLDER puntIsoRig.js " +
                    "(docs/art/rigs/README.md records an md5 difference).");

                Assert.Greater(whole.MatchPercent, 95.0,
                    "Pixel agreement is below 95%. See the per-cell log above: if ONE cell is bad " +
                    "the correction is wrong; if ALL cells drift slightly the rig revision differs.");
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

        static Diff CompareCell(Color32[] a, Color32[] b, int sheetW, int cell)
        {
            long match = 0, alphaAgree = 0, sum = 0, n = 0;
            int x0 = cell * CellW;
            for (int y = 0; y < CellH; y++)
            for (int x = x0; x < x0 + CellW; x++)
            {
                int i = y * sheetW + x;
                int dr = Math.Abs(a[i].r - b[i].r), dg = Math.Abs(a[i].g - b[i].g);
                int db = Math.Abs(a[i].b - b[i].b), da = Math.Abs(a[i].a - b[i].a);
                sum += dr + dg + db + da;
                if (dr <= ChannelTolerance && dg <= ChannelTolerance &&
                    db <= ChannelTolerance && da <= ChannelTolerance) match++;
                if ((a[i].a >= 128) == (b[i].a >= 128)) alphaAgree++;
                n++;
            }
            return new Diff(100.0 * match / n, sum / (4.0 * n), 100.0 * alphaAgree / n);
        }
    }
}
