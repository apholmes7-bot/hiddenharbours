using System;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The single most important correctness fixture in the baker.
    ///
    /// The azimuth mislabel — cell i labelled +45°·i while depicting −45°·i — has caused defects in
    /// FIVE separate kits. Every time, the cause was the same: somebody trusted a DECLARATION (the
    /// rig's own `order` array, a README table, a hand-maintained flag) instead of measuring the
    /// rendered art. So these tests measure pixels, and cross-check that measurement against an
    /// independent analytic source.
    /// </summary>
    public class RigAzimuthConventionTests
    {
        /// <summary>How far the pixel-measured bearing may sit from the analytic anchor bearing.
        /// ADR 0021 measured agreement "within ~1°" at fractional dir; a few degrees of slack
        /// absorbs dither fringe and the keyline.</summary>
        const double BearingToleranceDegrees = 8.0;

        // ---- The measurement itself -----------------------------------------------------------

        [Test]
        public void Punt_MeasuresAsCounterClockwise_FromPixels()
        {
            var r = Measure("punt");
            Debug.Log("[rig-baker] punt azimuth probe:\n" + r.Report);

            Assert.AreEqual(AzimuthConvention.CounterClockwise, r.Convention,
                "The punt rig renders counter-clockwise. If this flipped, either the rig changed " +
                "or the probe broke — establish which before touching the baker.");
            Assert.Greater(r.Elongation, 2.0,
                "The silhouette is not elongated enough at a quarter turn for PCA to be trusted.");
            Assert.Less(r.BowBeam, r.SternBeam,
                "The bow-taper test found the bow no narrower than the transom, so the 180° " +
                "ambiguity was not actually resolved.");
        }

        [Test]
        public void LobsterBoat_MeasuresAsCounterClockwise_FromPixels()
        {
            var r = Measure("lobsterBoat");
            Debug.Log("[rig-baker] lobster boat azimuth probe:\n" + r.Report);
            Assert.AreEqual(AzimuthConvention.CounterClockwise, r.Convention);
            Assert.Greater(r.Elongation, 2.0);
        }

        /// <summary>
        /// THE CROSS-CHECK, and the strongest evidence available that the pixel probe is right.
        ///
        /// The punt's two tub anchors are BOTH at x:0, dead on the centreline, so the screen angle
        /// between them is the analytic hull bearing — computed by the rig's own projection maths,
        /// entirely independently of the silhouette. If PCA-plus-bow-taper agrees with it, the probe
        /// is measuring the hull and not an artefact.
        ///
        /// ⚠️ Do NOT port this to the console skiff. Its TUBS[0] is at x:-0.48, an aft quarter, so
        /// the line between its tubs is not the centreline and the check shows a ~12° gap. That is
        /// the PROBE being wrong, not the rig — and it has already misled one investigation.
        /// </summary>
        [Test]
        public void Punt_PixelBearing_AgreesWithItsCollinearTubAnchors()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("punt");
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            byte[] rgba = host.EvaluateBytes($"{g}.render(2,{{}})");
            var probe = RigAzimuthProbe.MeasureFromQuarterTurn(rgba, geo.Width, geo.Height);
            double pixelBearing = RigAzimuthProbe.BearingOf(probe);

            // TUBS[0] is at y:-1.00 (aft), TUBS[1] at y:+0.60 (forward) — bow is +y in rig space.
            double ax = host.EvaluateNumber($"{g}.tubMounts(2,{{}})[0].x");
            double ay = host.EvaluateNumber($"{g}.tubMounts(2,{{}})[0].y");
            double fx = host.EvaluateNumber($"{g}.tubMounts(2,{{}})[1].x");
            double fy = host.EvaluateNumber($"{g}.tubMounts(2,{{}})[1].y");

            Assert.Greater(Math.Abs(fx - ax) + Math.Abs(fy - ay), 1e-6,
                "The two tub anchors projected to the same point — the probe has nothing to measure.");

            double analytic = RigAzimuthProbe.AnalyticBearingFromCollinearAnchors((ax, ay), (fx, fy));
            double delta = RigAzimuthProbe.AngleDelta(pixelBearing, analytic);

            Debug.Log($"[rig-baker] punt @ dir 2 — pixel bearing {pixelBearing:F1}°, " +
                      $"analytic (centreline tubs) {analytic:F1}°, delta {delta:F1}°");

            Assert.Less(Math.Abs(delta), BearingToleranceDegrees,
                $"The silhouette says the bow points {pixelBearing:F1}° but the rig's own " +
                $"centreline anchors say {analytic:F1}°. Those must agree — if they do not, do not " +
                "bake anything until you know why.");
        }

        // ---- The correction the baker applies --------------------------------------------------

        [Test]
        public void DirForCell_CounterClockwiseRig_EmitsGenuinelyClockwiseCells()
        {
            // 8 facings: cell k must render dir (8-k)%8, so cell 1 (NE) renders dir 7.
            Assert.AreEqual(0.0, RigBaker.DirForCell(0, 8, AzimuthConvention.CounterClockwise), 1e-9);
            Assert.AreEqual(7.0, RigBaker.DirForCell(1, 8, AzimuthConvention.CounterClockwise), 1e-9);
            Assert.AreEqual(6.0, RigBaker.DirForCell(2, 8, AzimuthConvention.CounterClockwise), 1e-9);
            Assert.AreEqual(4.0, RigBaker.DirForCell(4, 8, AzimuthConvention.CounterClockwise), 1e-9);

            // 32 facings: one rig dir unit is 45°, so the step is 8/32 = 0.25.
            Assert.AreEqual(0.0,  RigBaker.DirForCell(0,  32, AzimuthConvention.CounterClockwise), 1e-9);
            Assert.AreEqual(7.75, RigBaker.DirForCell(1,  32, AzimuthConvention.CounterClockwise), 1e-9);
            Assert.AreEqual(6.0,  RigBaker.DirForCell(8,  32, AzimuthConvention.CounterClockwise), 1e-9);
            Assert.AreEqual(4.0,  RigBaker.DirForCell(16, 32, AzimuthConvention.CounterClockwise), 1e-9);
        }

        /// <summary>
        /// A blanket correction is WRONG. characterIsoRig and rodIsoRig were fixed at source by the
        /// art director; applying the CCW correction to them would re-mirror art that is already
        /// right — which is exactly the mistake docs/art/rigs/README.md warns about.
        /// </summary>
        [Test]
        public void DirForCell_ClockwiseRig_IsLeftAlone_NotReMirrored()
        {
            for (int k = 0; k < 8; k++)
                Assert.AreEqual(k, RigBaker.DirForCell(k, 8, AzimuthConvention.Clockwise), 1e-9,
                    $"Cell {k} of a clockwise-correct rig must render dir {k} unchanged.");
        }

        // ---- SABOTAGE: prove the guard actually bites -------------------------------------------

        /// <summary>
        /// The wrong convention is not a subtle error and this asserts so: applying the CCW
        /// correction where it does not belong sends every non-axial cell to a different heading —
        /// 90° out at the diagonals, and it swaps NE with NW. If this test can ever pass with the
        /// conventions transposed, the guard is decorative.
        /// </summary>
        [Test]
        public void Sabotage_TheWrongConvention_DivergesFarBeyondAnyMeasurementError()
        {
            int wrong = 0;
            for (int k = 1; k < 8; k++)
            {
                double right = RigBaker.DirForCell(k, 8, AzimuthConvention.CounterClockwise);
                double bad   = RigBaker.DirForCell(k, 8, AzimuthConvention.Clockwise);
                double errDeg = Math.Abs(RigAzimuthProbe.AngleDelta(right * 45.0, bad * 45.0));
                if (errDeg > 1e-6) wrong++;
                Debug.Log($"[rig-baker] sabotage cell {k}: correct dir {right}, wrong dir {bad}, " +
                          $"heading error {errDeg:F0}°");
            }
            // SIX, not seven. North (cell 0) and south (cell 4) sit ON the mirror axis, so
            // (8−k)%8 == k for both and they are invariant under the correction. That is not a
            // loophole in the test — it is the fingerprint of this exact bug, and it shows up
            // independently in the golden master, where cells 0 and 4 match the shipped sheet
            // byte-for-byte while the diagonals are 90° out and E/W are 180° out. The known
            // scar-tissue formula (error = −2h) predicts precisely that pattern.
            Assert.AreEqual(6, wrong,
                "Every cell except the two on the mirror axis (N and S) must move when the " +
                "convention is flipped. If fewer moved, the correction is not being applied where " +
                "it is claimed to be.");
        }

        /// <summary>
        /// Sabotage in the other direction: feed the probe a rendered facing with the image
        /// horizontally mirrored, which is precisely what a wrong convention produces, and prove the
        /// probe reports the opposite answer. A probe that cannot be fooled by a mirror is not
        /// measuring handedness at all.
        /// </summary>
        [Test]
        public void Sabotage_MirroringTheArt_FlipsTheMeasuredConvention()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("punt");
            var geo = RigCatalog.Install(host, entry);

            byte[] rgba = host.EvaluateBytes($"{entry.GlobalName}.render(2,{{}})");
            var honest = RigAzimuthProbe.MeasureFromQuarterTurn(rgba, geo.Width, geo.Height);

            byte[] mirrored = MirrorHorizontally(rgba, geo.Width, geo.Height);
            var fooled = RigAzimuthProbe.MeasureFromQuarterTurn(mirrored, geo.Width, geo.Height);

            Debug.Log($"[rig-baker] sabotage mirror: honest={honest.Convention}, " +
                      $"mirrored={fooled.Convention}");

            Assert.AreNotEqual(honest.Convention, fooled.Convention,
                "Mirroring the artwork did not change the measured convention. The probe is " +
                "therefore not measuring handedness, and every guard built on it is worthless.");
        }

        static byte[] MirrorHorizontally(byte[] rgba, int w, int h)
        {
            var outp = new byte[rgba.Length];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int s = (y * w + x) * 4;
                int d = (y * w + (w - 1 - x)) * 4;
                outp[d] = rgba[s]; outp[d + 1] = rgba[s + 1];
                outp[d + 2] = rgba[s + 2]; outp[d + 3] = rgba[s + 3];
            }
            return outp;
        }

        static RigAzimuthProbe.Result Measure(string rigKey)
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get(rigKey);
            var geo = RigCatalog.Install(host, entry);
            byte[] rgba = host.EvaluateBytes(
                $"{entry.GlobalName}.render({2.0.ToString("R", CultureInfo.InvariantCulture)},{{}})");
            return RigAzimuthProbe.MeasureFromQuarterTurn(rgba, geo.Width, geo.Height);
        }
    }
}
