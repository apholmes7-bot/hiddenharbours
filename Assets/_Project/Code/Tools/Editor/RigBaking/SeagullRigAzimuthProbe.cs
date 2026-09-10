using System;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Measures the seagull's azimuth convention (<c>SeagullIso</c>) from rendered pixels, by
    /// finding the BILL.
    ///
    /// <para><b>Why none of the three existing probes fits a bird, measured rather than assumed.</b>
    /// All three sibling probes break the 180° ambiguity with a SHAPE cue, and a gull has none of
    /// them. Every number below is from the repo's own ClearScript V8 running the committed
    /// <c>seagullIsoRig.js</c>, cell 64×64, pivot (32,46):</para>
    ///
    /// <list type="bullet">
    /// <item><b><see cref="ShovelRigAzimuthProbe"/> — the far extreme — answers BACKWARDS.</b> A
    /// spade's blade out-reaches its D-grip, so "the silhouette end furthest from the pivot column
    /// is the pointing end" holds. A gull's does not: at <c>stand</c> dir 2 the extremes are
    /// <b>+7.6 px on the bill side and −11.1 px on the tail side</b>, so the far-extreme read picks
    /// the TAIL and returns <c>CounterClockwise</c> — the exact opposite of the truth. Copying that
    /// probe onto this rig would have produced a confident, mirrored, wrong answer that the
    /// catalog's refusal check would then have blamed on the catalog.</item>
    /// <item><b><see cref="RigAzimuthProbe.MeasureFromQuarterTurn"/> — the bow taper — has nothing
    /// to separate.</b> Its test bins the silhouette along the PCA long axis and calls the narrower
    /// end the bow. At <c>stand</c> dir 2 the two end beams measure <b>4.1 px and 4.2 px</b>: one
    /// tenth of a pixel apart, inside dither noise. A gull is blunt at both ends.</item>
    /// <item><b>The alpha centroid is a 1–2 px signal.</b> −1.95 px at dir 2 against +0.96 px at
    /// dir 6. It happens to have the right sign (the body trails the bill) but no margin worth
    /// baking 384 cells on.</item>
    /// <item><b>The crown — the top row of the silhouette — flips sign inside one animation.</b>
    /// +2.69 px at <c>stand</c> dir 6 frame 0 and −7.00 px at frame 1. The highest point of a
    /// standing gull is the shoulder, not the head, and which shoulder wins is a dither
    /// coin-toss.</item>
    /// </list>
    ///
    /// <para><b>What IS decisive: the bill is the only YELLOW thing on the bird.</b>
    /// <c>MATS.bill</c> is a five-step ramp off <c>#d4a83a</c>, and every one of those five steps
    /// satisfies <c>r &gt; g &gt; b</c> with a wide blue gap. No other material can: <c>white</c>,
    /// <c>mantle</c>, <c>tip</c> and <c>eye</c> all have <c>r ≤ g</c> at every ramp step, and
    /// <c>leg</c> (<c>#c99298</c>, a pink) has <c>g &lt; b</c> at every step. The rasteriser picks
    /// ramp ENTRIES rather than blending, and depth shading scales channels uniformly, so neither
    /// dither nor shading can carry a non-bill pixel across
    /// <see cref="IsBill"/>'s test — an ordering is preserved by a positive scale.</para>
    ///
    /// <para><b>The measurement, over all 160 cells of the six posed states.</b> The bill cluster's
    /// centroid, relative to the pivot column, per direction index:</para>
    /// <code>
    ///   dir 0:  −1.3 px (3 px of bill visible at all — a gull facing away hides it)
    ///   dir 1:  +6.0      dir 2:  +8.1      dir 3:  +5.4
    ///   dir 4:  −0.5 px (facing the camera — on the centreline, as it must be)
    ///   dir 5:  −6.3      dir 6:  −9.1      dir 7:  −7.0
    /// </code>
    /// <para>Not one cell at dir 1/2/3 reads negative and not one at dir 5/6/7 reads positive. A
    /// bill that swings EAST as the index rises is a cell <c>i</c> depicting heading <c>+45°·i</c>:
    /// <b>CLOCKWISE</b> — the minority convention in this repo, which is why it was measured.</para>
    ///
    /// <para><b>The probe pose is <c>walk</c>, chosen because it never hides the bill.</b> Its six
    /// frames put <b>9 bill pixels at dir 2 and 9 at dir 6</b> with no blank cell in either row.
    /// <c>perch</c> was rejected on measurement: its dir-2 frame 0 renders ZERO bill pixels, and a
    /// probe that can read nothing is a probe that throws on good art.</para>
    ///
    /// <para><b>And the anchor cross-check, because the anchors are the deliverable.</b>
    /// <c>SeagullIso.anchors(dir,…).bill</c> reads +10 px at dir 2 and −10 px at dir 6 — the same
    /// sign as the pixels, about 2 px beyond the last drawn pixel because the anchor is the bill
    /// TIP and the silhouette ends one pixel short of it. If the rig's own bill anchor ever
    /// disagrees in SIGN with the yellow the rig itself drew, the sidecar's <c>ANCHORS_PX</c> and
    /// every consumer of it are pointing out of the back of the bird — so this throws rather than
    /// bakes, exactly as the shovel probe does for <c>tip()</c>.</para>
    ///
    /// <para>Like every sibling probe the caller cross-checks the answer against the catalog
    /// declaration and REFUSES to bake on a mismatch (<see cref="FishingKitBaker.RefuseOnMismatch"/>)
    /// rather than silently picking a side.</para>
    /// </summary>
    public static class SeagullRigAzimuthProbe
    {
        /// <summary>Alpha threshold for "this pixel is artwork". The rig's <c>_toRGBA</c> writes
        /// 0 or 255 for dry cells, so any mid threshold reads identically — kept at the fishing and
        /// shovel probes' number so the four agree by construction.</summary>
        const byte AlphaThreshold = 128;

        /// <summary>
        /// Minimum bill pixels summed across the probe row before the read is trusted. Measured 9
        /// at each of the east and west rows over <c>walk</c>'s six frames, so this keeps a 2×
        /// margin. It is deliberately NOT zero-tolerant: a classifier that stopped matching (a
        /// recoloured bill upstream, a blend introduced into the rasteriser) must throw, not
        /// silently return 0 px of offset and let the sign fall out of noise.
        /// </summary>
        const int MinBillPixels = 4;

        /// <summary>
        /// Offset floor, px. Measured +8.33 / −9.33 (see the class remarks), so 3.0 keeps a ~2.8×
        /// margin — and unlike the shovel's floor this one is not competing with a near-symmetric
        /// silhouette: the two rows are 17.7 px apart across zero.
        /// </summary>
        const double MinOffsetPx = 3.0;

        /// <summary>The pose probed. The rig's own animation id, chosen on measurement — see the
        /// class remarks on why not <c>perch</c>.</summary>
        public const string ProbeAnim = "walk";

        public readonly struct Result
        {
            public readonly AzimuthConvention Convention;

            /// <summary>Bill-cluster centroid minus pivot column, px, at the rows LABELLED east and
            /// west, summed over the probe animation's frames.</summary>
            public readonly double EastBillOffsetPx, WestBillOffsetPx;
            public readonly int EastBillPixels, WestBillPixels;
            public readonly string Report;

            public Result(AzimuthConvention convention, double east, double west,
                          int eastPixels, int westPixels, string report)
            {
                Convention = convention;
                EastBillOffsetPx = east; WestBillOffsetPx = west;
                EastBillPixels = eastPixels; WestBillPixels = westPixels;
                Report = report;
            }
        }

        /// <summary>One accumulated row: bill mass and its centroid column.</summary>
        public readonly struct BillRead
        {
            public readonly int BillPixels;
            public readonly int OpaquePixels;
            public readonly double CentroidX;

            public BillRead(int billPixels, int opaquePixels, double centroidX)
            {
                BillPixels = billPixels; OpaquePixels = opaquePixels; CentroidX = centroidX;
            }

            public double Offset(double pivotX) => BillPixels > 0 ? CentroidX - pivotX : 0.0;
        }

        /// <summary>
        /// Is this RGB one of the bill ramp's five steps (or a uniformly shaded one)? See the class
        /// remarks for the proof that no other material in <c>MATS</c> can satisfy it.
        ///
        /// <para>The thresholds are the ramp's own worst case: the darkest bill step
        /// <c>#594718</c> = (89,71,24) has <c>g−b = 47</c> and <c>r−b = 65</c>, so 25 and 50 sit
        /// comfortably under it while staying far above the pink leg ramp, which fails on
        /// <c>g &gt; b</c> outright.</para>
        /// </summary>
        public static bool IsBill(byte r, byte g, byte b) =>
            r > g && g > b && (g - b) >= 25 && (r - b) >= 50;

        /// <summary>
        /// The pure measurement: bill mass and centroid of one rendered cell. Engine-free (bytes
        /// in, numbers out) so it is testable without a script host — the same arrangement, for the
        /// same reason, as <see cref="ShovelRigAzimuthProbe.ReadBlade"/>.
        /// </summary>
        public static BillRead ReadBill(byte[] rgba, int width, int height)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            if (rgba.Length != width * height * 4)
                throw new ArgumentException(
                    $"Buffer is {rgba.Length} bytes, expected {width * height * 4} for {width}×{height} RGBA.");

            long bill = 0, opaque = 0; double sumX = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                if (rgba[i + 3] < AlphaThreshold) continue;
                opaque++;
                if (!IsBill(rgba[i], rgba[i + 1], rgba[i + 2])) continue;
                bill++; sumX += x;
            }
            return new BillRead((int)bill, (int)opaque, bill > 0 ? sumX / bill : 0.0);
        }

        /// <summary>
        /// Decides the seagull's convention from which side of the pivot column the BILL is drawn
        /// at the rows labelled east and west, then cross-checks <c>anchors().bill</c> against the
        /// measured side. <paramref name="dirs"/> is the bake recipe's facing count (the rig
        /// declares no <c>DIRS</c> global — see <see cref="SeagullBaker.Dirs"/>).
        /// </summary>
        public static Result Measure(IRigScriptHost host, string globalName, in RigGeometry geo,
                                     int dirs)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (dirs <= 0) throw new ArgumentException("dirs must be positive", nameof(dirs));

            string g = globalName;
            int e = dirs / 4, w = 3 * dirs / 4;

            int frames = (int)host.EvaluateNumber($"{g}.ANIMS[{Js(ProbeAnim)}].n");
            if (frames <= 0)
                throw new InvalidOperationException(
                    $"SeagullIso.ANIMS.{ProbeAnim} declares {frames} frames — the probe pose has " +
                    "left the rig. Pick another state that never hides the bill and re-measure " +
                    "before changing this constant.");

            BillRead east = AccumulateRow(host, g, geo, e, frames);
            BillRead west = AccumulateRow(host, g, geo, w, frames);
            double eOff = east.Offset(geo.PivotX);
            double wOff = west.Offset(geo.PivotX);

            var sb = new StringBuilder();
            sb.AppendLine($"seagull probe: anim '{ProbeAnim}' × {frames} frames, bill pixels only " +
                          "(MATS.bill is the only r>g>b ramp on the bird)");
            sb.AppendLine($"row labelled E (dir {e}): {east.BillPixels} bill px of " +
                          $"{east.OpaquePixels} opaque, centroid {eOff:F2} px from the pivot column");
            sb.AppendLine($"row labelled W (dir {w}): {west.BillPixels} bill px of " +
                          $"{west.OpaquePixels} opaque, centroid {wOff:F2} px from the pivot column");

            if (east.BillPixels < MinBillPixels || west.BillPixels < MinBillPixels)
                throw new InvalidOperationException(
                    "SEAGULL PROBE INCONCLUSIVE — too little bill to measure.\n" + sb +
                    $"Fewer than {MinBillPixels} px means either the bill ramp was recoloured " +
                    "upstream, the rasteriser started blending instead of picking ramp entries, or " +
                    $"'{ProbeAnim}' stopped showing the head. Do not bake until the probe reads " +
                    "real artwork — a zero-pixel read would put the sign in dither noise.");

            bool eRight = eOff > +MinOffsetPx, eLeft = eOff < -MinOffsetPx;
            bool wRight = wOff > +MinOffsetPx, wLeft = wOff < -MinOffsetPx;

            AzimuthConvention convention;
            if (eRight && wLeft) convention = AzimuthConvention.Clockwise;
            else if (eLeft && wRight) convention = AzimuthConvention.CounterClockwise;
            else
                throw new InvalidOperationException(
                    "SEAGULL PROBE INCONCLUSIVE — the east and west rows do not put the bill on " +
                    "opposite sides of the pivot column.\n" + sb +
                    $"Measured at intake: +8.33 px and −9.33 px against a {MinOffsetPx:F1} px " +
                    "floor. Do not bake until this is understood.");

            double billE = host.EvaluateNumber(
                $"{g}.anchors({Num(e)},{{anim:{Js(ProbeAnim)},frame:0}}).bill.dx");
            double billW = host.EvaluateNumber(
                $"{g}.anchors({Num(w)},{{anim:{Js(ProbeAnim)},frame:0}}).bill.dx");
            sb.AppendLine($"anchors().bill.dx at E/W rows: {billE:F2} / {billW:F2} px " +
                          "(the anchor is the TIP, ~2 px beyond the last drawn pixel)");
            if ((billE > 0) != (eOff > 0) || (billW > 0) != (wOff > 0))
                throw new InvalidOperationException(
                    "SEAGULL PROBE: anchors().bill contradicts the pixels the rig itself drew.\n" + sb +
                    "The yellow is on one side of the pivot and the rig's own bill anchor on the " +
                    "other. ANCHORS_PX in the gameplay sidecar is derived from these anchors and " +
                    "the whole creature contract hangs off them — baking would ship a bird whose " +
                    "bill fires out of its tail. This is a rig defect: flag it to the art " +
                    "director's workspace, do not shim it host-side.");

            sb.Append($"=> MEASURED CONVENTION: {convention}");
            return new Result(convention, eOff, wOff, east.BillPixels, west.BillPixels, sb.ToString());
        }

        /// <summary>Sums one direction row's frames into a single bill read — nine pixels spread
        /// over six cells is a firmer number than one cell's one or two.</summary>
        static BillRead AccumulateRow(IRigScriptHost host, string g, in RigGeometry geo,
                                      int dir, int frames)
        {
            long bill = 0, opaque = 0; double sumX = 0;
            for (int f = 0; f < frames; f++)
            {
                byte[] rgba = host.EvaluateBytes(
                    $"{g}.render({Num(dir)},{{anim:{Js(ProbeAnim)},frame:{Num(f)}}})");
                if (rgba.Length != geo.Width * geo.Height * 4)
                    throw new InvalidOperationException(
                        $"Probe render at dir {dir} frame {f} came back {rgba.Length} bytes, " +
                        $"expected {geo.Width * geo.Height * 4} for {geo.Width}×{geo.Height} RGBA.");
                BillRead r = ReadBill(rgba, geo.Width, geo.Height);
                bill += r.BillPixels; opaque += r.OpaquePixels;
                sumX += r.CentroidX * r.BillPixels;
            }
            return new BillRead((int)bill, (int)opaque, bill > 0 ? sumX / bill : 0.0);
        }

        static string Js(string s) => FishingKitBaker.Js(s);
        static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }
}
