using System;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>What a rig's <c>pivot</c> numbers actually MEAN, in pixels.</summary>
    public enum PivotOrigin
    {
        /// <summary>
        /// <b>The value is a continuous coordinate whose origin is the cell's top-left CORNER.</b>
        /// <c>pivot.x = 92</c> means "92.0 px right of the left edge", which falls on the seam
        /// between columns 91 and 92 — not inside either. Unity conversion:
        /// <c>(x/W, (H − y)/H)</c>.
        /// </summary>
        CellCornerContinuous,

        /// <summary>
        /// <b>The value is a 0-based pixel INDEX.</b> <c>pivot.x = 92</c> would mean "column 92",
        /// whose centre sits at continuous 92.5. Unity conversion would be
        /// <c>((x + 0.5)/W, (H − 1 − y)/H)</c> for an edge-aligned pivot on that row's lower edge —
        /// i.e. one row LOWER than <see cref="CellCornerContinuous"/> gives.
        /// </summary>
        PixelIndex,
    }

    /// <summary>
    /// Determines what a rig's <c>pivot</c> numbers mean BY MEASURING RENDERED PIXELS — the same
    /// discipline <see cref="RigAzimuthProbe"/> applies to the facing order, and for the same
    /// reason: the repo has been burned repeatedly by trusting a declaration about rig geometry
    /// instead of looking at the art.
    ///
    /// <para><b>Why this probe exists.</b> Two pivot formulas coexist in the repo and differ by
    /// exactly one row (ADR 0026):</para>
    /// <list type="bullet">
    ///   <item><c>RigGeometry.UnityNormalisedPivot</c> and
    ///   <c>FishingSheetSlicer.KitSpec.NormalizedPivot</c> use <c>(H − pivotY)/H</c>.</item>
    ///   <item><c>TreeKitCatalog.NormalizedPivot</c> uses the tree rig's own exported
    ///   <c>pad = h − 1 − pivotY</c>, i.e. <c>pad/h</c>.</item>
    /// </list>
    /// <para>Those are both correct because they convert DIFFERENT QUANTITIES — see ADR 0026. This
    /// probe pins down the one that the boat and character bakes depend on, so the question cannot
    /// be re-opened from first principles a third time.</para>
    ///
    /// <para><b>THE METHOD.</b> Render a rig at a heading where its silhouette is
    /// LEFT-RIGHT SYMMETRIC about its own centreline (bow-on <c>dir 0</c>, or stern-on
    /// <c>dir N/2</c>: at those headings the azimuth term contributes no screen-x shear, so a hull
    /// that is symmetric port-to-starboard in 3D is symmetric in the render). The centreline
    /// projects to screen-x <c>pivotX</c>. Then:</para>
    ///
    /// <list type="number">
    ///   <item>Under <see cref="PivotOrigin.CellCornerContinuous"/> the mirror axis is at
    ///   continuous <c>pivotX</c>, so column <c>j</c> reflects to <c>(2·pivotX − 1) − j</c>.</item>
    ///   <item>Under <see cref="PivotOrigin.PixelIndex"/> the axis is at <c>pivotX + 0.5</c>
    ///   (the centre of column <c>pivotX</c>), so <c>j</c> reflects to <c>2·pivotX − j</c>.</item>
    /// </list>
    ///
    /// <para>Reflect the alpha mask under each hypothesis and count the pixels that disagree. The
    /// TRUE axis reproduces the mask almost exactly; the false one is displaced by a single column
    /// and therefore disagrees along every near-vertical edge of the silhouette — hundreds of
    /// pixels on a hull. The separation is not subtle.</para>
    ///
    /// <para>Using every pixel rather than the two extremes is deliberate: a lone dithered or
    /// keylined pixel at one end would swing a min/max test and cannot swing this one. The extreme
    /// span is still reported (<see cref="Result.ExtremeSum"/>) as an independent corroboration —
    /// it equals <c>2·pivotX − 1</c> under the corner reading and <c>2·pivotX</c> under the index
    /// reading.</para>
    ///
    /// <para>⚠️ <b>Probe only at a symmetric heading, and only a rig that is actually symmetric.</b>
    /// A hull with an asymmetric fitting (a single-side console, a tiller swung over) will report a
    /// high mismatch under BOTH hypotheses; <see cref="Result.IsDecisive"/> is what says the sample
    /// was worth reading. Do not "fix" an indecisive result by loosening the margin.</para>
    /// </summary>
    public static class RigPivotConventionProbe
    {
        /// <summary>Alpha threshold for "this pixel is artwork" — matched to
        /// <see cref="RigAzimuthProbe"/> so the two probes silhouette a rig identically.</summary>
        const byte AlphaThreshold = 128;

        /// <summary>
        /// How many times better the winning hypothesis must be before the result is trusted. A
        /// one-column displacement of a hull silhouette disagrees on hundreds of pixels while the
        /// true axis disagrees on ~zero, so a real measurement clears this by orders of magnitude.
        /// </summary>
        const double DecisiveRatio = 8.0;

        public readonly struct Result
        {
            public readonly PivotOrigin Origin;
            /// <summary>Pixels that disagree when the mask is mirrored about continuous
            /// <c>pivotX</c> (the corner reading). Near zero if that reading is right.</summary>
            public readonly int CornerMismatch;
            /// <summary>Pixels that disagree when the mask is mirrored about <c>pivotX + 0.5</c>
            /// (the index reading).</summary>
            public readonly int IndexMismatch;
            /// <summary><c>minX + maxX</c> of the silhouette — the independent extreme check.</summary>
            public readonly int ExtremeSum;
            public readonly int OpaquePixels;
            /// <summary>True when one hypothesis beat the other by <see cref="DecisiveRatio"/>.
            /// False means the render was not symmetric enough to read — a bad probe heading or a
            /// genuinely asymmetric rig, NOT evidence for either convention.</summary>
            public readonly bool IsDecisive;
            public readonly string Report;

            public Result(PivotOrigin origin, int cornerMismatch, int indexMismatch, int extremeSum,
                          int opaque, bool decisive, string report)
            {
                Origin = origin; CornerMismatch = cornerMismatch; IndexMismatch = indexMismatch;
                ExtremeSum = extremeSum; OpaquePixels = opaque; IsDecisive = decisive; Report = report;
            }
        }

        /// <summary>
        /// Measures the pivot convention from a render taken at a left-right symmetric heading.
        /// </summary>
        /// <param name="rgba">RGBA buffer straight from the rig's <c>render()</c>.</param>
        /// <param name="pivotX">The rig's own <c>pivot.x</c>, unconverted.</param>
        public static Result MeasureFromSymmetricRender(byte[] rgba, int width, int height, int pivotX)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            if (rgba.Length != width * height * 4)
                throw new ArgumentException(
                    $"Buffer is {rgba.Length} bytes, expected {width * height * 4} for {width}×{height} RGBA.");

            var opaque = new bool[width * height];
            int n = 0, minX = int.MaxValue, maxX = int.MinValue;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (rgba[(y * width + x) * 4 + 3] >= AlphaThreshold)
                {
                    opaque[y * width + x] = true;
                    n++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                }

            if (n < 64)
                throw new InvalidOperationException(
                    $"Only {n} opaque pixels — the rig rendered (almost) nothing, so there is no " +
                    "silhouette to mirror. Check the render expression before trusting any convention.");

            // The two candidate mirror axes, expressed as the reflection of column j.
            int cornerMismatch = Mismatch(opaque, width, height, 2 * pivotX - 1);
            int indexMismatch  = Mismatch(opaque, width, height, 2 * pivotX);

            bool cornerWins = cornerMismatch < indexMismatch;
            int win = Math.Min(cornerMismatch, indexMismatch);
            int lose = Math.Max(cornerMismatch, indexMismatch);
            // +1 keeps a perfect (zero-mismatch) win decisive rather than 0/0.
            bool decisive = lose >= (win + 1) * DecisiveRatio;

            var origin = cornerWins ? PivotOrigin.CellCornerContinuous : PivotOrigin.PixelIndex;

            var sb = new StringBuilder();
            sb.AppendLine($"opaque={n}px  silhouette columns [{minX}..{maxX}]  minX+maxX={minX + maxX}");
            sb.AppendLine($"mirror about continuous {pivotX}.0   (corner reading) => {cornerMismatch} px disagree" +
                          $"   [predicts minX+maxX = {2 * pivotX - 1}]");
            sb.AppendLine($"mirror about {pivotX}.5 = centre of col {pivotX} (index reading) => {indexMismatch} px disagree" +
                          $"   [predicts minX+maxX = {2 * pivotX}]");
            sb.Append(decisive
                ? $"=> MEASURED PIVOT ORIGIN: {origin}"
                : $"=> INDECISIVE ({cornerMismatch} vs {indexMismatch}): this render is not symmetric " +
                  "enough to read. Wrong probe heading, or the rig carries an asymmetric fitting.");

            return new Result(origin, cornerMismatch, indexMismatch, minX + maxX, n, decisive, sb.ToString());
        }

        /// <summary>
        /// Counts pixels whose mirror image disagrees, where column <c>j</c> reflects to
        /// <c>sum − j</c>. Anything reflecting outside the cell counts as a disagreement if it was
        /// opaque, which is what makes an axis that is off-centre entirely (rather than by one
        /// column) score badly too.
        /// </summary>
        static int Mismatch(bool[] opaque, int width, int height, int sum)
        {
            int bad = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int m = sum - x;
                bool here = opaque[y * width + x];
                bool there = m >= 0 && m < width && opaque[y * width + m];
                if (here != there) bad++;
            }
            return bad;
        }
    }
}
