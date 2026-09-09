using System;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Measures the clam spade's azimuth convention (<c>ShovelIso</c>) from rendered pixels.
    ///
    /// <para><b>Why the rod's probe does not fit, measured rather than assumed.</b>
    /// <see cref="FishingRigAzimuthProbe.MeasureRod"/> reads the opaque CENTROID's offset from the
    /// pivot column and requires it past a 5.0 px floor. That works for a rod because the grip IS
    /// the pivot and the entire 0.95–1.55 m blank lies on the heading side of it. A spade is
    /// two-ended: the pivot is the grip low on the shaft, the blade reaches +0.665 m forward, and
    /// the D-grip reaches −0.375 m BACK across the pivot, dragging the mass toward it. Transcribing
    /// the rod's read onto the spade in the standalone V8 harness produced <b>−5.76 px at east and
    /// +4.76 px at west</b> — correctly mirrored, but one side UNDER the rod's own floor, so
    /// <c>MeasureRod</c>'s <c>Decide</c> would have thrown "INCONCLUSIVE" on art that is perfectly
    /// legible. A floor lowered to fit that is a floor sitting in dither noise.</para>
    ///
    /// <para><b>What is measured instead: the blade's own extreme.</b> The far end of the silhouette
    /// from the pivot column is the one quantity that says which way a two-ended tool POINTS, and
    /// the D-grip cannot compete with it — it is 0.375 m against the blade's 0.665 m. Measured in
    /// the same harness run at <c>rest:'ground'</c> (blade face up, laid flat along the heading):
    /// <b>−22 px at east, +21 px at west</b>, against the same run's rod at +16.89 / −17.96. Four
    /// times the rod's floor, from art the centroid could not adjudicate.</para>
    ///
    /// <para><b>And the anchor cross-check, because the anchor is the deliverable.</b> The spoil FX
    /// launch from <c>ShovelIso.tip()</c> and the mount pins <c>ShovelIso.pivot</c> to the
    /// character's grip. A <c>tip()</c> that disagreed with the rendered blade would poison every
    /// exported anchor even with the sheets coming out right — so the probe asserts the rig's own
    /// tip lands on the measured blade side at both rows and throws rather than bakes when it does
    /// not. Measured: <c>tip()</c> reads −21.28 / +21.28 px, agreeing with the pixels to under a
    /// pixel at both rows.</para>
    ///
    /// <para><b>⚠️ The answer this probe returns is COUNTER-CLOCKWISE, and the rig's own header says
    /// otherwise.</b> See the measurement record on the catalog entry in
    /// <c>RigCatalog.ToolKit.cs</c>. Like every sibling probe, the caller cross-checks against the
    /// catalog declaration and REFUSES to bake on a mismatch rather than silently picking a side —
    /// so the day the sign fix lands upstream, this reddens instead of quietly mirroring the kit.</para>
    /// </summary>
    public static class ShovelRigAzimuthProbe
    {
        /// <summary>Alpha threshold for "this pixel is artwork". The spade's rasteriser emits alpha
        /// 0 or 255 only (<c>_toRGBA</c> writes no intermediate value), so any mid threshold reads
        /// identically — kept at the fishing probe's number so the two agree by construction.</summary>
        const byte AlphaThreshold = 128;

        /// <summary>Minimum opaque pixels before a silhouette is trusted at all. The spade's ground
        /// rest measured 153 px per cell at the E/W rows.</summary>
        const int MinOpaquePixels = 40;

        /// <summary>
        /// Blade-extreme floor. Measured ±21–22 px (see the class remarks), so a 5.0 px floor —
        /// the rod's, deliberately the same number — keeps a 4× margin. This is the floor the
        /// CENTROID could not clear; the extreme clears it easily, which is the whole reason this
        /// probe exists rather than a call to the rod's.
        /// </summary>
        const double MinOffsetPx = 5.0;

        /// <summary>The pose probed: flat along the heading, blade face up. The rig's own
        /// <c>REST</c> name, read here rather than restated as a literal elsewhere.</summary>
        public const string ProbeRest = "ground";

        public readonly struct Result
        {
            public readonly AzimuthConvention Convention;

            /// <summary>Far extreme minus pivot column, px, at the rows LABELLED east / west.</summary>
            public readonly double EastBladeOffsetPx, WestBladeOffsetPx;
            public readonly int EastOpaquePixels, WestOpaquePixels;
            public readonly string Report;

            public Result(AzimuthConvention convention, double east, double west,
                          int eastPixels, int westPixels, string report)
            {
                Convention = convention;
                EastBladeOffsetPx = east; WestBladeOffsetPx = west;
                EastOpaquePixels = eastPixels; WestOpaquePixels = westPixels;
                Report = report;
            }
        }

        /// <summary>One measured cell: opaque mass and the extreme furthest from the pivot column.</summary>
        public readonly struct BladeRead
        {
            public readonly int OpaquePixels;
            public readonly int MinX, MaxX;
            public readonly double CentroidX;

            public BladeRead(int opaquePixels, int minX, int maxX, double centroidX)
            {
                OpaquePixels = opaquePixels; MinX = minX; MaxX = maxX; CentroidX = centroidX;
            }

            /// <summary>
            /// Signed distance from <paramref name="pivotX"/> to whichever horizontal extreme is
            /// FURTHER from it — the blade end on a spade laid flat, because the blade out-reaches
            /// the D-grip 0.665 m to 0.375 m.
            /// </summary>
            public double BladeOffset(double pivotX)
            {
                if (OpaquePixels <= 0) return 0.0;
                double lo = MinX - pivotX, hi = MaxX - pivotX;
                return Math.Abs(lo) > Math.Abs(hi) ? lo : hi;
            }
        }

        /// <summary>
        /// The pure measurement: opaque mass, horizontal extent and centroid of one rendered cell.
        /// Engine-free (bytes in, numbers out) so it is testable without a script host — the same
        /// arrangement, for the same reason, as <see cref="FishingRigAzimuthProbe.ReadSilhouette"/>.
        /// </summary>
        public static BladeRead ReadBlade(byte[] rgba, int width, int height)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            if (rgba.Length != width * height * 4)
                throw new ArgumentException(
                    $"Buffer is {rgba.Length} bytes, expected {width * height * 4} for {width}×{height} RGBA.");

            long n = 0; double sumX = 0;
            int minX = int.MaxValue, maxX = int.MinValue;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (rgba[(y * width + x) * 4 + 3] < AlphaThreshold) continue;
                n++; sumX += x;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
            }
            return n > 0 ? new BladeRead((int)n, minX, maxX, sumX / n) : new BladeRead(0, 0, 0, 0);
        }

        /// <summary>
        /// Decides the spade's convention from which side of the grip column the BLADE extends at
        /// the rows labelled east and west, then cross-checks <c>tip()</c> against the measured side.
        /// <paramref name="dirs"/> is the bake recipe's facing count (the rig declares no DIRS).
        /// </summary>
        public static Result Measure(IRigScriptHost host, string globalName, in RigGeometry geo,
                                     int dirs)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (dirs <= 0) throw new ArgumentException("dirs must be positive", nameof(dirs));

            string g = globalName;
            int e = dirs / 4, w = 3 * dirs / 4;
            string opts = "{rest:'" + ProbeRest + "'}";

            BladeRead east = RenderAndRead(host, g, geo, e, opts);
            BladeRead west = RenderAndRead(host, g, geo, w, opts);
            double eOff = east.BladeOffset(geo.PivotX);
            double wOff = west.BladeOffset(geo.PivotX);

            var sb = new StringBuilder();
            sb.AppendLine($"shovel probe: rest:'{ProbeRest}' (flat along the heading, blade face up)");
            sb.AppendLine($"row labelled E (dir {e}): {east.OpaquePixels} px, blade extreme from grip " +
                          $"{eOff:F2} px (centroid {east.CentroidX - geo.PivotX:F2} px — the read the " +
                          "rod's probe would have used, and the one that is too small to adjudicate)");
            sb.AppendLine($"row labelled W (dir {w}): {west.OpaquePixels} px, blade extreme from grip " +
                          $"{wOff:F2} px (centroid {west.CentroidX - geo.PivotX:F2} px)");

            if (east.OpaquePixels < MinOpaquePixels || west.OpaquePixels < MinOpaquePixels)
                throw new InvalidOperationException(
                    "SHOVEL PROBE INCONCLUSIVE — too little silhouette to measure.\n" + sb +
                    "Do not bake until the probe reads real artwork.");

            bool eRight = eOff > +MinOffsetPx, eLeft = eOff < -MinOffsetPx;
            bool wRight = wOff > +MinOffsetPx, wLeft = wOff < -MinOffsetPx;

            AzimuthConvention convention;
            if (eRight && wLeft) convention = AzimuthConvention.Clockwise;
            else if (eLeft && wRight) convention = AzimuthConvention.CounterClockwise;
            else
                throw new InvalidOperationException(
                    "SHOVEL PROBE INCONCLUSIVE — the east and west rows do not read to opposite " +
                    "sides of the grip.\n" + sb +
                    $"Either rest:'{ProbeRest}' stopped lying flat along the heading, the grip moved " +
                    "off the pivot, or the blade stopped out-reaching the D-grip. Do not bake until " +
                    "this is understood.");

            double tipEx = host.EvaluateNumber($"{g}.tip({Num(e)},{opts}).x") - geo.PivotX;
            double tipWx = host.EvaluateNumber($"{g}.tip({Num(w)},{opts}).x") - geo.PivotX;
            sb.AppendLine($"tip().x − grip at E/W rows: {tipEx:F2} / {tipWx:F2} px");
            if ((tipEx > 0) != (eOff > 0) || (tipWx > 0) != (wOff > 0))
                throw new InvalidOperationException(
                    "SHOVEL PROBE: tip() contradicts the rendered pixels.\n" + sb +
                    "The pixels put the blade on one side of the grip and the rig's own tip anchor " +
                    "on the other. The spoil FX launch from tip() and the mount pins the pivot to " +
                    "her grip — baking would ship anchors on the wrong end of the tool. This is a " +
                    "rig defect: flag it to the art director's workspace, do not shim it host-side.");

            sb.Append($"=> MEASURED CONVENTION: {convention}");
            return new Result(convention, eOff, wOff, east.OpaquePixels, west.OpaquePixels, sb.ToString());
        }

        static BladeRead RenderAndRead(IRigScriptHost host, string g, in RigGeometry geo,
                                       int dir, string optsJs)
        {
            byte[] rgba = host.EvaluateBytes($"{g}.render({Num(dir)},{optsJs})");
            if (rgba.Length != geo.Width * geo.Height * 4)
                throw new InvalidOperationException(
                    $"Probe render at dir {dir} came back {rgba.Length} bytes, expected " +
                    $"{geo.Width * geo.Height * 4} for {geo.Width}×{geo.Height} RGBA.");
            return ReadBlade(rgba, geo.Width, geo.Height);
        }

        static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }
}
