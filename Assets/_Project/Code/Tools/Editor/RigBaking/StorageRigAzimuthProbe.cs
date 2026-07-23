using System;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Measures the azimuth convention of the storage kit's two DIRECTIONAL container rigs — the
    /// fish tote (<c>FishTote</c>) and the bucket kit (<c>BucketIso</c>) — from rendered pixels,
    /// per the README's standing correction: a rig's <c>±dir·45°</c> sign term is NOT proof and
    /// the mislabel has shipped defects in five kits. Neither existing probe fits, for measured
    /// reasons:
    ///
    /// <list type="bullet">
    /// <item><see cref="RigAzimuthProbe"/> (boats: PCA + bow-taper) needs an elongated silhouette
    /// with a tapered heading end. A tote is a square box; a pail is a near-cylinder — no taper,
    /// no bearing.</item>
    /// <item><see cref="FishingRigAzimuthProbe"/> (head-mass / blank-off-the-grip) needs mass
    /// displaced along the heading. A container's mass is centred by design.</item>
    /// </list>
    ///
    /// WHAT IS MEASURED INSTEAD — each rig's one honestly-handed feature:
    ///
    /// <para><b>Tote: the leaning lid hangs on the +x wall.</b> <c>lid:'lean'</c> stands the lid
    /// against the tote's +x side; <c>lid:'off'</c> is the identical hollow shell without it. The
    /// difference mask (pixels opaque in LEAN but not in OFF) is therefore the lid alone — a whole
    /// 1 m slab, hundreds of pixels. At the row labelled East a genuinely clockwise rig
    /// (th = −dir·45°) maps world +x to screen-DOWN (sy grows by +x·sin(elev)·S), and at West to
    /// screen-UP — so the lid-mask centroid sits LOWER at E than at W iff the rig is clockwise.
    /// The geometric separation is ≈ 2·0.72 m·sin40°·32 px ≈ 29 px; the floor sits at 6.</para>
    ///
    /// <para><b>Bucket: the fish tray's footprint is chiral at the diagonals.</b> Tier 3 at rest is
    /// a rectangle, LX 0.31 m &gt; LY 0.22 m. At the rows labelled NE and NW the long axis lies on
    /// a screen diagonal whose SLOPE SIGN is the handedness: for a counter-clockwise rig
    /// (th = +dir·45°) the NE row lays the long axis up-right — opaque-pixel covariance
    /// cov(x, y_screen-down) NEGATIVE — and the NW row mirrors it positive; clockwise is the exact
    /// opposite. The covariance is dominated by the long axis (∝ LX²−LY²) and the render is exact
    /// palette geometry, not noise; the floor sits at 1.5 px².</para>
    ///
    /// Like every sibling: the caller cross-checks the measurement against the catalog declaration
    /// and REFUSES to bake on a mismatch rather than silently picking a side.
    /// </summary>
    public static class StorageRigAzimuthProbe
    {
        const byte AlphaThreshold = 128;

        /// <summary>Minimum pixels in the lid difference mask before the tote read is trusted —
        /// the lean lid is a ~1 m slab and should paint hundreds.</summary>
        const int MinLidPixels = 60;

        /// <summary>Tote floor: the lid centroids separate by ≈29 px between the E and W rows
        /// (2·0.72 m·sin 40°·32 px); 6 px keeps a wide margin over dither-edge jitter.</summary>
        const double ToteMinSeparationPx = 6.0;

        /// <summary>Minimum opaque pixels before the tray silhouette is trusted.</summary>
        const int MinTrayPixels = 200;

        /// <summary>Bucket floor, in px² of covariance. The tray's long-axis term is ∝ LX²−LY²
        /// (several px² after iso squash); 1.5 clears residual wall/keyline asymmetries.</summary>
        const double BucketMinCovariance = 1.5;

        public readonly struct Result
        {
            public readonly AzimuthConvention Convention;
            public readonly string Report;
            public Result(AzimuthConvention convention, string report)
            {
                Convention = convention; Report = report;
            }
        }

        // ---- the tote -------------------------------------------------------------------------

        /// <summary>Tote colour used for the probe — any colour works (the mask is geometric);
        /// the fleet default keeps the report reproducible.</summary>
        public const string ToteProbeColour = "navy";

        /// <summary>
        /// Decides the tote rig's convention from where the LEANING LID lands at the rows
        /// labelled E and W (see class remarks). <paramref name="dirs"/> is the bake recipe's
        /// facing count (the rig declares no DIRS).
        /// </summary>
        public static Result MeasureTote(IRigScriptHost host, string globalName, in RigGeometry geo,
                                         int dirs)
        {
            string g = globalName;
            int e = dirs / 4, w = 3 * dirs / 4;

            double yE = LidCentroidY(host, g, geo, e, out int nE);
            double yW = LidCentroidY(host, g, geo, w, out int nW);

            var sb = new StringBuilder();
            sb.AppendLine($"tote probe: colour {ToteProbeColour}, lid 'lean' minus lid 'off' (the lid alone)");
            sb.AppendLine($"row labelled E (dir {e}): {nE} lid px, centroid y {yE:F2}");
            sb.AppendLine($"row labelled W (dir {w}): {nW} lid px, centroid y {yW:F2}");

            if (nE < MinLidPixels || nW < MinLidPixels)
                throw new InvalidOperationException(
                    "TOTE PROBE INCONCLUSIVE — the lean-lid difference mask is too small to " +
                    "measure.\n" + sb +
                    "Either lid:'lean' no longer differs from lid:'off' by the lid alone, or the " +
                    "render failed. Do not bake until the probe reads real artwork.");

            double separation = yE - yW;   // screen y grows DOWN
            sb.AppendLine($"lid centroid separation yE − yW: {separation:F2} px (screen-down positive)");

            AzimuthConvention convention;
            if (separation > +ToteMinSeparationPx) convention = AzimuthConvention.Clockwise;
            else if (separation < -ToteMinSeparationPx) convention = AzimuthConvention.CounterClockwise;
            else
                throw new InvalidOperationException(
                    "TOTE PROBE INCONCLUSIVE — the leaning lid does not land on mirrored sides " +
                    "at the E and W rows.\n" + sb +
                    "Either the lean pose moved off the +x wall or the camera basis changed. " +
                    "Do not bake until this is understood.");

            sb.Append($"=> MEASURED CONVENTION: {convention}");
            return new Result(convention, sb.ToString());
        }

        static double LidCentroidY(IRigScriptHost host, string g, in RigGeometry geo, int dir,
                                   out int count)
        {
            byte[] lean = RenderCell(host, geo,
                $"{g}.render({Num(dir)},{{colour:'{ToteProbeColour}',lid:'lean'}})");
            byte[] off = RenderCell(host, geo,
                $"{g}.render({Num(dir)},{{colour:'{ToteProbeColour}',lid:'off'}})");

            long n = 0; double sumY = 0;
            for (int y = 0; y < geo.Height; y++)
            for (int x = 0; x < geo.Width; x++)
            {
                int i = (y * geo.Width + x) * 4 + 3;
                if (lean[i] >= AlphaThreshold && off[i] < AlphaThreshold)
                {
                    n++; sumY += y;
                }
            }
            count = (int)n;
            return n > 0 ? sumY / n : 0;
        }

        // ---- the bucket -----------------------------------------------------------------------

        /// <summary>The probed tier: 3 = the fish tray, the kit's only rectangular (chiral)
        /// footprint. Pails are near-cylinders and carry no bearing; the convention is a property
        /// of the shared camera basis, identical for every tier.</summary>
        public const int BucketProbeTier = 3;

        /// <summary>
        /// Decides the bucket rig's convention from the tray footprint's diagonal slope at the
        /// rows labelled NE and NW (see class remarks). Rendered at REST (base-centre frame,
        /// no roll/pitch), empty.
        /// </summary>
        public static Result MeasureBucket(IRigScriptHost host, string globalName,
                                           in RigGeometry restGeometry, int dirs)
        {
            string g = globalName;
            int ne = dirs / 8, nw = 7 * dirs / 8;

            double covNE = FootprintCovariance(host, g, restGeometry, ne, out int nNE);
            double covNW = FootprintCovariance(host, g, restGeometry, nw, out int nNW);

            var sb = new StringBuilder();
            sb.AppendLine($"bucket probe: tier {BucketProbeTier} (fish tray), rest, empty");
            sb.AppendLine($"row labelled NE (dir {ne}): {nNE} px, cov(x, y-down) {covNE:F2} px²");
            sb.AppendLine($"row labelled NW (dir {nw}): {nNW} px, cov(x, y-down) {covNW:F2} px²");

            if (nNE < MinTrayPixels || nNW < MinTrayPixels)
                throw new InvalidOperationException(
                    "BUCKET PROBE INCONCLUSIVE — too little tray silhouette to measure.\n" + sb +
                    "Do not bake until the probe reads real artwork.");

            bool neUpRight = covNE < -BucketMinCovariance;   // long axis up-right at NE
            bool neDownRight = covNE > +BucketMinCovariance;
            bool nwDownRight = covNW > +BucketMinCovariance; // the mirror at NW
            bool nwUpRight = covNW < -BucketMinCovariance;

            AzimuthConvention convention;
            if (neUpRight && nwDownRight) convention = AzimuthConvention.CounterClockwise;
            else if (neDownRight && nwUpRight) convention = AzimuthConvention.Clockwise;
            else
                throw new InvalidOperationException(
                    "BUCKET PROBE INCONCLUSIVE — the tray's long axis does not lie on mirrored " +
                    "diagonals at the NE and NW rows.\n" + sb +
                    "Either the tray stopped being rectangular (LX vs LY) or the camera basis " +
                    "changed. Do not bake until this is understood.");

            sb.Append($"=> MEASURED CONVENTION: {convention}");
            return new Result(convention, sb.ToString());
        }

        static double FootprintCovariance(IRigScriptHost host, string g, in RigGeometry geo,
                                          int dir, out int count)
        {
            byte[] rgba = RenderCell(host, geo,
                $"{g}.render({Num(dir)},{{tier:{Num(BucketProbeTier)},rest:true,fill:'empty'}})");

            long n = 0; double sumX = 0, sumY = 0;
            for (int y = 0; y < geo.Height; y++)
            for (int x = 0; x < geo.Width; x++)
            {
                if (rgba[(y * geo.Width + x) * 4 + 3] < AlphaThreshold) continue;
                n++; sumX += x; sumY += y;
            }
            count = (int)n;
            if (n == 0) return 0;

            double mx = sumX / n, my = sumY / n, cov = 0;
            for (int y = 0; y < geo.Height; y++)
            for (int x = 0; x < geo.Width; x++)
            {
                if (rgba[(y * geo.Width + x) * 4 + 3] < AlphaThreshold) continue;
                cov += (x - mx) * (y - my);
            }
            return cov / n;
        }

        // ---- shared ---------------------------------------------------------------------------

        static byte[] RenderCell(IRigScriptHost host, in RigGeometry geo, string expr)
        {
            byte[] rgba = host.EvaluateBytes(expr);
            if (rgba.Length != geo.Width * geo.Height * 4)
                throw new InvalidOperationException(
                    $"Probe render `{expr}` came back {rgba.Length} bytes, expected " +
                    $"{geo.Width * geo.Height * 4} for {geo.Width}×{geo.Height} RGBA.");
            return rgba;
        }

        static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }
}
