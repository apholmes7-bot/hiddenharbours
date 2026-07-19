using System;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>Which way a rig's <c>dir</c> argument actually turns the artwork.</summary>
    public enum AzimuthConvention
    {
        /// <summary>Cell <c>i</c> depicts heading <c>+45°·i</c> — what the <c>order</c> array claims.</summary>
        Clockwise,

        /// <summary>Cell <c>i</c> depicts heading <c>−45°·i</c>. True of 19 of the 21 directional
        /// rigs, including every boat except none — the two clockwise-correct rigs are
        /// characterIsoRig and rodIsoRig.</summary>
        CounterClockwise,
    }

    /// <summary>
    /// Determines a rig's azimuth convention BY MEASURING RENDERED PIXELS.
    ///
    /// ⚠️ This exists because the declared facing order has been wrong five times, and every single
    /// time the defect shipped because someone trusted a declaration — the rig's own
    /// <c>order:['N','NE','E',…]</c> array, a README table, or a hand-maintained flag — instead of
    /// looking at the art. A blanket "all rigs are counter-clockwise" correction is also wrong: it
    /// would re-mirror characterIsoRig and rodIsoRig, which the art director already fixed at source.
    ///
    /// Note the sign of the `th = dir*Math.PI/4` term is NOT a reliable tell either: puntIsoRig and
    /// lobsterBoatIsoRig both use a POSITIVE sign yet both render counter-clockwise, because the
    /// handedness comes from the iso camera basis, not from that term. Read the pixels.
    ///
    /// THE METHOD, and why each step is the way it is:
    ///
    ///  1. Render a QUARTER TURN — <c>dir = N/4</c>. At a quarter turn the hull is fully broadside,
    ///     which is exactly where the principal axis is longest and least ambiguous. (Do not probe
    ///     at dir 0: the hull is bow-on, the silhouette is nearly symmetric, and PCA is noise.)
    ///
    ///  2. PCA the opaque pixels to get the hull's long axis. This is 180°-ambiguous — it tells you
    ///     the hull lies along a line, not which end is the bow.
    ///
    ///  3. BOW-TAPER TEST to break the ambiguity: a boat tapers to a point at the bow and is blunt
    ///     at the transom, so bin the pixels along the long axis and compare the beam (spread
    ///     across the axis) at each end. The narrower end is the bow.
    ///
    ///  4. Read the sign of the bow's SCREEN-X offset. A quarter turn clockwise from north points
    ///     the bow EAST (screen +x); counter-clockwise points it WEST (screen −x). Screen-x is the
    ///     right axis to test because the ¾-iso projection only foreshortens Y — it squashes the
    ///     vertical, leaves the horizontal alone, and being a positive-determinant linear map it
    ///     cannot flip the sense of rotation. So this one sign is the whole answer.
    ///
    /// Cross-check where a rig allows it: <see cref="AnalyticBearingFromCollinearAnchors"/> derives
    /// the same bearing from the rig's OWN projected anchors, independent of the pixels.
    /// </summary>
    public static class RigAzimuthProbe
    {
        public readonly struct Result
        {
            public readonly AzimuthConvention Convention;
            public readonly double BowScreenX;      // px, signed, relative to the alpha centroid
            public readonly double BowScreenY;      // px, signed, screen-down positive
            public readonly double Elongation;      // major/minor PCA ratio — sanity: want > ~2
            public readonly double BowBeam, SternBeam; // px, the taper evidence
            public readonly int OpaquePixels;
            public readonly string Report;

            public Result(AzimuthConvention convention, double bowX, double bowY, double elongation,
                          double bowBeam, double sternBeam, int opaque, string report)
            {
                Convention = convention; BowScreenX = bowX; BowScreenY = bowY;
                Elongation = elongation; BowBeam = bowBeam; SternBeam = sternBeam;
                OpaquePixels = opaque; Report = report;
            }
        }

        /// <summary>Alpha threshold for "this pixel is hull". The rigs lay a 1px keyline and use
        /// ordered dither, so a mid threshold avoids counting dither fringe as silhouette.</summary>
        const byte AlphaThreshold = 128;

        /// <summary>Fraction of the hull length treated as "the end" when measuring taper.</summary>
        const double EndFraction = 0.18;

        /// <summary>
        /// Measures the convention from an RGBA buffer rendered at a quarter turn
        /// (<c>dir = facings/4</c>, e.g. dir 2 of 8 or dir 8 of 32).
        /// </summary>
        public static Result MeasureFromQuarterTurn(byte[] rgba, int width, int height)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            if (rgba.Length != width * height * 4)
                throw new ArgumentException(
                    $"Buffer is {rgba.Length} bytes, expected {width * height * 4} for {width}×{height} RGBA.");

            // --- 1. Alpha-masked centroid -------------------------------------------------
            double sx = 0, sy = 0; int n = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (rgba[(y * width + x) * 4 + 3] >= AlphaThreshold) { sx += x; sy += y; n++; }

            if (n < 64)
                throw new InvalidOperationException(
                    $"Only {n} opaque pixels — the rig rendered (almost) nothing, so there is no " +
                    "silhouette to measure. Check the render expression before trusting any convention.");

            double cx = sx / n, cy = sy / n;

            // --- 2. PCA long axis ---------------------------------------------------------
            double mxx = 0, mxy = 0, myy = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (rgba[(y * width + x) * 4 + 3] >= AlphaThreshold)
                {
                    double dx = x - cx, dy = y - cy;
                    mxx += dx * dx; mxy += dx * dy; myy += dy * dy;
                }
            mxx /= n; mxy /= n; myy /= n;

            // Closed-form eigenvector of the 2×2 covariance matrix for the larger eigenvalue.
            double tr = mxx + myy;
            double det = mxx * myy - mxy * mxy;
            double disc = Math.Sqrt(Math.Max(0.0, tr * tr / 4.0 - det));
            double l1 = tr / 2.0 + disc, l2 = tr / 2.0 - disc;

            double ux, uy;
            if (Math.Abs(mxy) > 1e-9) { ux = l1 - myy; uy = mxy; }
            else if (mxx >= myy)      { ux = 1; uy = 0; }
            else                      { ux = 0; uy = 1; }
            double ulen = Math.Sqrt(ux * ux + uy * uy);
            ux /= ulen; uy /= ulen;

            double elongation = l2 > 1e-9 ? Math.Sqrt(l1 / l2) : double.PositiveInfinity;

            // --- 3. Bow-taper test --------------------------------------------------------
            // Project onto the axis (t) and across it (w); the blunt transom has the larger beam.
            double tMin = double.MaxValue, tMax = double.MinValue;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (rgba[(y * width + x) * 4 + 3] >= AlphaThreshold)
                {
                    double t = (x - cx) * ux + (y - cy) * uy;
                    if (t < tMin) tMin = t;
                    if (t > tMax) tMax = t;
                }

            double span = tMax - tMin, band = span * EndFraction;
            double posBeam = Beam(rgba, width, height, cx, cy, ux, uy, tMax - band, tMax);
            double negBeam = Beam(rgba, width, height, cx, cy, ux, uy, tMin, tMin + band);

            // The narrower end is the bow; that fixes the sign of the axis.
            double bowSign = posBeam < negBeam ? +1.0 : -1.0;
            double bowBeam = Math.Min(posBeam, negBeam), sternBeam = Math.Max(posBeam, negBeam);

            double bowX = bowSign * ux * (span / 2.0);
            double bowY = bowSign * uy * (span / 2.0);

            // --- 4. The answer ------------------------------------------------------------
            var convention = bowX > 0 ? AzimuthConvention.Clockwise : AzimuthConvention.CounterClockwise;

            var sb = new StringBuilder();
            sb.AppendLine($"opaque={n}px  centroid=({cx:F1},{cy:F1})  elongation={elongation:F2}");
            sb.AppendLine($"long axis=({ux:F3},{uy:F3})  span={span:F1}px");
            sb.AppendLine($"beam at +end={posBeam:F1}px, at -end={negBeam:F1}px  => bow is the " +
                          $"{(bowSign > 0 ? "+" : "-")} end (narrower)");
            sb.AppendLine($"bow offset screen=({bowX:F1},{bowY:F1})  => bow points " +
                          $"{(bowX > 0 ? "EAST (screen +x)" : "WEST (screen -x)")} at a quarter turn");
            sb.Append($"=> MEASURED CONVENTION: {convention}");

            return new Result(convention, bowX, bowY, elongation, bowBeam, sternBeam, n, sb.ToString());
        }

        static double Beam(byte[] rgba, int width, int height, double cx, double cy,
                           double ux, double uy, double tLo, double tHi)
        {
            double wMin = double.MaxValue, wMax = double.MinValue;
            // Perpendicular of (ux,uy).
            double vx = -uy, vy = ux;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (rgba[(y * width + x) * 4 + 3] < AlphaThreshold) continue;
                double dx = x - cx, dy = y - cy;
                double t = dx * ux + dy * uy;
                if (t < tLo || t > tHi) continue;
                double w = dx * vx + dy * vy;
                if (w < wMin) wMin = w;
                if (w > wMax) wMax = w;
            }
            return wMax < wMin ? 0.0 : wMax - wMin;
        }

        /// <summary>
        /// The independent cross-check: for a rig whose anchor points are COLLINEAR ON THE
        /// CENTRELINE, the screen angle between two projected anchors IS the hull bearing,
        /// computed by the rig's own maths rather than read off the pixels. Agreement between this
        /// and the PCA bearing is the strongest evidence available that the probe is right.
        ///
        /// ⚠️ Use a rig whose anchors really are collinear. puntIsoRig is the good probe: both its
        /// TUBS are at x:0, dead on the centreline. consoleIsoRig is NOT — its TUBS[0] sits at
        /// x:-0.48, an aft quarter, so the line between its tubs is not the centreline and the
        /// check shows a ~12° gap. That is the PROBE being wrong, not the rig. This has already
        /// misled one investigation; do not repeat it.
        /// </summary>
        /// <param name="aftAnchor">Projected screen point of the AFTER anchor (nearer the transom).</param>
        /// <param name="foreAnchor">Projected screen point of the FORWARD anchor (nearer the bow).</param>
        /// <returns>Bearing in degrees, measured the same way as <see cref="Result.BowScreenX"/>:
        /// atan2 of the aft→fore vector in screen space.</returns>
        public static double AnalyticBearingFromCollinearAnchors(
            (double x, double y) aftAnchor, (double x, double y) foreAnchor)
        {
            double dx = foreAnchor.x - aftAnchor.x;
            double dy = foreAnchor.y - aftAnchor.y;
            return Math.Atan2(dy, dx) * 180.0 / Math.PI;
        }

        /// <summary>Bearing of the measured bow vector, comparable with
        /// <see cref="AnalyticBearingFromCollinearAnchors"/>.</summary>
        public static double BearingOf(in Result r) =>
            Math.Atan2(r.BowScreenY, r.BowScreenX) * 180.0 / Math.PI;

        /// <summary>Smallest signed difference between two bearings, in degrees.</summary>
        public static double AngleDelta(double a, double b)
        {
            double d = (a - b + 540.0) % 360.0 - 180.0;
            return d;
        }
    }
}
