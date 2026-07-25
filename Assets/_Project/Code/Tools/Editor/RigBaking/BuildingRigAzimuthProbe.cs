using System;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Determines a BUILDING rig's azimuth convention, and cross-checks that the rig's anchors and its
    /// renderer describe the same building.
    ///
    /// <para><b>Why the boat probe cannot be reused.</b> <see cref="RigAzimuthProbe"/> works by PCA-ing
    /// the silhouette at a quarter turn and breaking the 180° ambiguity with a bow-taper test: a hull is
    /// pointed at one end and blunt at the other. <b>A building has no bow.</b> It is a box with a gabled
    /// roof, and at a quarter turn its silhouette is very nearly mirror-symmetric — so the taper test has
    /// nothing to bite on and PCA would return noise dressed as an answer. Running the boat probe on a
    /// shed would produce a confident, arbitrary result, which is worse than no probe at all.</para>
    ///
    /// <para><b>What this measures instead: the DOOR.</b> Both building rigs put the main door on the
    /// <c>+Y</c> gable and expose it through <c>anchors(dir, opts).door</c>, already projected to screen
    /// pixels. The door is the building's face — which way it points IS the building's heading — so its
    /// screen-x at a quarter turn answers the question directly:</para>
    /// <list type="bullet">
    ///   <item>Cell 2 is LABELLED <c>'E'</c> by the rig's own <c>order</c> array.</item>
    ///   <item>If the door lands LEFT of the pivot (screen −x, i.e. west), the cell labelled east
    ///         depicts west: cell <c>i</c> is <c>−45°·i</c> — <b>counter-clockwise</b>.</item>
    ///   <item>If it lands right, the labels are honest — <b>clockwise</b>.</item>
    /// </list>
    ///
    /// <para><b>Why an anchor is legitimate here when the README says "read the pixels".</b> That rule
    /// exists because a rig's <c>order</c> array, a README table and a hand-kept flag are all
    /// DECLARATIONS — text written by a human, which is what has been wrong five times. <c>anchors()</c>
    /// is not a declaration: in both rigs it calls the same <c>camBasis</c> and <c>projVert</c> that
    /// <c>render()</c> draws with (verified by reading the source — the comment in wharfBuildingRig even
    /// says "identical to houseIsoRig, so wharf buildings composite with the fleet"). The door anchor is
    /// therefore not a claim ABOUT where the door is drawn; it is the same arithmetic that draws it. It
    /// is measurement, evaluated rather than eyeballed.</para>
    ///
    /// <para><b>What the pixel cross-check adds.</b> The above holds only while <c>anchors()</c> and
    /// <c>render()</c> resolve the SAME building — the realistic failure is a rig drop where one of them
    /// starts resolving options differently, at which point the shared-projection argument quietly stops
    /// applying. So this also measures the rendered silhouette's width at a face-on and a quarter turn
    /// and checks them against the <c>Wd</c>/<c>Ln</c> that <c>anchors()</c> reports, at 32 px = 1 m.
    /// It uses no colour heuristics and no PCA: just how wide the drawn thing is. If those disagree the
    /// probe REFUSES, because the door reading would no longer be evidence of anything.</para>
    ///
    /// <para><b>What this deliberately does NOT claim.</b> The handedness itself is not independently
    /// re-derived from pixels — there is no building feature as unambiguous as a bow taper. It rests on
    /// the shared-projection argument above, guarded by the width check. Stated plainly so nobody later
    /// mistakes this for the same class of evidence as the punt's byte-identical golden master.</para>
    /// </summary>
    public static class BuildingRigAzimuthProbe
    {
        public readonly struct Result
        {
            public readonly AzimuthConvention Convention;
            /// <summary>Door screen-x at the quarter turn, relative to the pivot. Sign IS the answer.</summary>
            public readonly double DoorOffsetPx;
            /// <summary>Building footprint the rig reports, in metres.</summary>
            public readonly double WidthMeters, LengthMeters;
            /// <summary>Rendered silhouette width (px) face-on and broadside.</summary>
            public readonly int FaceOnPx, BroadsidePx;
            public readonly string Report;

            public Result(AzimuthConvention convention, double doorOffsetPx,
                          double widthMeters, double lengthMeters,
                          int faceOnPx, int broadsidePx, string report)
            {
                Convention = convention; DoorOffsetPx = doorOffsetPx;
                WidthMeters = widthMeters; LengthMeters = lengthMeters;
                FaceOnPx = faceOnPx; BroadsidePx = broadsidePx; Report = report;
            }
        }

        /// <summary>
        /// How far the measured silhouette width may exceed the rig's stated footprint before the probe
        /// refuses, as a multiple of the footprint. Roof overhang, corner boards and the 1 px keyline all
        /// draw OUTSIDE the wall box, so the render is legitimately wider than <c>Wd</c>/<c>Ln</c> — but
        /// only by trim, never by half again.
        /// </summary>
        public const double WidthTolerance = 0.55;

        /// <summary>
        /// The door must land at least this many pixels off the pivot at a quarter turn for the sign to
        /// be trusted. At a quarter turn the offset is <c>(Ln/2)·32</c> — over 70 px even for the
        /// smallest shack — so anything near zero means the rig is not shaped the way this probe assumes.
        /// </summary>
        public const double MinDoorOffsetPx = 16.0;

        /// <summary>
        /// Measure a building rig loaded into <paramref name="host"/>.
        /// <paramref name="optsJs"/> is the JS options literal (e.g. <c>{type:'shack'}</c>) — the SAME
        /// one the bake will use, so the probe measures the build actually being baked and not a default
        /// that might resolve differently.
        /// </summary>
        public static Result Measure(IRigScriptHost host, string globalName, string optsJs,
                                     int width, int height, double pivotX)
        {
            string g = globalName;
            string o = string.IsNullOrWhiteSpace(optsJs) ? "{}" : optsJs;

            // ---- 1. the door at a quarter turn (rig-native dir units: 2 of 8 == 90°) --------------
            double doorX = host.EvaluateNumber($"{g}.anchors(2,{o}).door.x");
            double wd = host.EvaluateNumber($"{g}.anchors(2,{o}).Wd");
            double ln = host.EvaluateNumber($"{g}.anchors(2,{o}).Ln");
            double offset = doorX - pivotX;

            // ---- 2. the rendered silhouette, face-on and broadside --------------------------------
            int faceOn = SilhouetteWidthPx(host, g, o, dir: 0, width, height);
            int broadside = SilhouetteWidthPx(host, g, o, dir: 2, width, height);

            var sb = new StringBuilder();
            sb.AppendLine("BUILDING AZIMUTH PROBE");
            sb.AppendLine($"  footprint (rig)  : Wd {wd:F2} m × Ln {ln:F2} m");
            sb.AppendLine($"  silhouette px    : face-on {faceOn}, broadside {broadside}");
            sb.AppendLine($"  expected px      : face-on ~{wd * 32:F0}, broadside ~{ln * 32:F0} (+ roof trim)");
            sb.AppendLine($"  door screen-x    : {doorX:F1} px, pivot {pivotX:F1} px → offset {offset:+0.0;-0.0} px");

            // ---- 3. refuse if anchors and render disagree about the building ----------------------
            Check(faceOn, wd, "face-on", "Wd", sb);
            Check(broadside, ln, "broadside", "Ln", sb);

            if (Math.Abs(offset) < MinDoorOffsetPx)
                throw new InvalidOperationException(
                    "BUILDING AZIMUTH PROBE REFUSED: the door sits essentially ON the pivot at a " +
                    $"quarter turn (offset {offset:F1} px, need ≥{MinDoorOffsetPx:F0}).\n\n" + sb +
                    "\nThis probe reads the door's side to tell which way the rig turns, so a centred " +
                    "door means the rig is not shaped the way it assumes — refusing rather than " +
                    "coin-flipping a convention.");

            // ---- 4. the answer ---------------------------------------------------------------------
            // Cell 2 is labelled 'E'. Door to screen-LEFT (−x, west) ⇒ the label is a lie by −90° ⇒ CCW.
            var convention = offset < 0
                ? AzimuthConvention.CounterClockwise
                : AzimuthConvention.Clockwise;

            sb.AppendLine($"  ⇒ cell 2 is labelled 'E' and its door points " +
                          $"{(offset < 0 ? "WEST (screen −x)" : "EAST (screen +x)")} ⇒ {convention}");

            return new Result(convention, offset, wd, ln, faceOn, broadside, sb.ToString());
        }

        static void Check(int measuredPx, double meters, string what, string field, StringBuilder sb)
        {
            double expected = meters * 32.0;
            if (expected <= 0)
                throw new InvalidOperationException(
                    $"BUILDING AZIMUTH PROBE REFUSED: the rig reports {field} = {meters} m.\n\n{sb}");

            double ratio = measuredPx / expected;
            if (ratio < 1.0 - WidthTolerance || ratio > 1.0 + WidthTolerance)
                throw new InvalidOperationException(
                    $"BUILDING AZIMUTH PROBE REFUSED: the {what} silhouette is {measuredPx} px but the " +
                    $"rig's own {field} of {meters:F2} m is {expected:F0} px at 32 px = 1 m " +
                    $"(ratio {ratio:F2}, tolerance ±{WidthTolerance:P0}).\n\n" + sb +
                    "\nanchors() and render() are describing DIFFERENT buildings, so the door reading " +
                    "is not evidence of anything. Refusing rather than baking on a broken assumption.");
        }

        /// <summary>Width in px of the opaque silhouette at a facing.</summary>
        static int SilhouetteWidthPx(IRigScriptHost host, string g, string optsJs, int dir,
                                     int width, int height)
        {
            string d = ((double)dir).ToString("R", CultureInfo.InvariantCulture);
            byte[] rgba = host.EvaluateBytes($"{g}.render({d},{optsJs})");

            if (rgba.Length != width * height * 4)
                throw new InvalidOperationException(
                    $"Probe render came back {rgba.Length} bytes, expected {width * height * 4} " +
                    $"for {width}×{height} RGBA.");

            AlphaBounds(rgba, width, height, out int xMin, out _, out int xMax, out _);
            return xMax < xMin ? 0 : xMax - xMin + 1;
        }

        /// <summary>
        /// Tight bounds of the non-transparent pixels, in the rigs' TOP-LEFT-origin image space.
        /// Returns <c>xMax &lt; xMin</c> for a fully transparent cell. Shared with the baker's crop —
        /// one definition of "where the art actually is", so a probe and a bake can never disagree.
        /// </summary>
        public static void AlphaBounds(byte[] rgba, int width, int height,
                                       out int xMin, out int yMin, out int xMax, out int yMax)
        {
            xMin = width; yMin = height; xMax = -1; yMax = -1;

            for (int y = 0; y < height; y++)
            {
                int row = y * width * 4;
                for (int x = 0; x < width; x++)
                {
                    if (rgba[row + x * 4 + 3] == 0) continue;
                    if (x < xMin) xMin = x;
                    if (x > xMax) xMax = x;
                    if (y < yMin) yMin = y;
                    if (y > yMax) yMax = y;
                }
            }
        }
    }
}
