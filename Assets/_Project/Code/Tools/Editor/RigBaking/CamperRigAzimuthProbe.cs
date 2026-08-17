using System;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Determines the CAMPER rig's azimuth convention, and cross-checks that its anchors and its
    /// renderer describe the same trailer.
    ///
    /// <para><b>Why neither existing probe can be reused.</b> <see cref="RigAzimuthProbe"/> PCAs the
    /// silhouette and breaks the 180° ambiguity with a bow-taper test — a hull is pointed at one end
    /// and blunt at the other. The camper's loft rolls off into <i>a rounded dome at each end</i>, so
    /// the taper test has nothing to bite on and would return noise dressed as an answer.
    /// <see cref="BuildingRigAzimuthProbe"/> solves the same problem for a house by reading the DOOR,
    /// which works because a house's door is on its <c>+Y</c> gable — the face IS the heading. <b>A
    /// camper's door is on the CURB side</b> (<c>+X</c>, per the kit's own README), so at a quarter
    /// turn it projects 5.8 px (bantam) / 2.6 px (clipper) off the pivot, under that probe's
    /// <see cref="BuildingRigAzimuthProbe.MinDoorOffsetPx"/> guard. It would refuse — correctly, and
    /// that refusal is the reason this file exists rather than a reason to relax the guard.</para>
    ///
    /// <para><b>What this measures instead: the HITCH.</b> A trailer's heading end is its coupler.
    /// <c>anchors(dir).hitch</c> projects it at <c>[0, bodyL/2 + tongue − 0.06, 0.44]</c> — squarely on
    /// <c>+Y</c>, 107 px (bantam) / 158 px (clipper) off the pivot at a quarter turn, which is a sign
    /// that can be read without squinting.</para>
    ///
    /// <para><b>Why an anchor is legitimate when the rule is "read the pixels".</b> That rule exists
    /// because an <c>order</c> array, a README table and a hand-kept flag are all DECLARATIONS — text a
    /// human wrote, which is what has been wrong five times in this repo. <c>anchors()</c> is not a
    /// declaration: it calls <c>project</c> → <c>projVert</c> → <c>camBasis</c>, the same arithmetic
    /// <c>render()</c> draws with. It is not a claim ABOUT where the hitch is drawn; it is the
    /// computation that draws it.</para>
    ///
    /// <para><b>What the pixel cross-check adds.</b> The above holds only while <c>anchors()</c> and
    /// <c>render()</c> resolve the SAME camper — the realistic failure is a drop where one of them
    /// starts resolving options differently, at which point the shared-projection argument quietly
    /// stops applying. So this also measures the rendered silhouette BROADSIDE and checks it against
    /// the <c>loa</c> that <c>anchors()</c> reports, at 32 px = 1 m.</para>
    ///
    /// <para>⚠️ <b>Broadside, and deliberately not face-on.</b> The obvious second check — face-on
    /// width against <c>width</c> — is <b>awning-dependent and therefore useless as a tolerance</b>:
    /// measured, a Clipper reads 85 px face-on unawninged against its 70 px body (ratio 1.21) and
    /// 138 px with its default awning fitted (ratio 1.96), because the awning is a 2.30 m curb-side
    /// deployable. A tolerance loose enough to admit 1.96 checks nothing; one tight enough to be worth
    /// having refuses a correct bake of the variant's own default build. Broadside is immune — 1.04
    /// (bantam) and 1.02 (clipper), identical in both awning states — because an awning extends the
    /// camper sideways, never lengthways.</para>
    /// </summary>
    public static class CamperRigAzimuthProbe
    {
        public readonly struct Result
        {
            public readonly AzimuthConvention Convention;
            /// <summary>Hitch screen-x at the quarter turn, relative to the pivot. Sign IS the answer.</summary>
            public readonly double HitchOffsetPx;
            /// <summary>Overall length the rig reports, in metres — body plus tongue.</summary>
            public readonly double LengthOverallMeters;
            /// <summary>Rendered silhouette width (px) broadside.</summary>
            public readonly int BroadsidePx;
            public readonly string Report;

            public Result(AzimuthConvention convention, double hitchOffsetPx,
                          double lengthOverallMeters, int broadsidePx, string report)
            {
                Convention = convention; HitchOffsetPx = hitchOffsetPx;
                LengthOverallMeters = lengthOverallMeters; BroadsidePx = broadsidePx; Report = report;
            }
        }

        /// <summary>
        /// How far the broadside silhouette may deviate from the rig's stated overall length before the
        /// probe refuses, as a fraction. Jacks, chocks and the coupler all draw a little outside the
        /// body-plus-tongue box, so the render is legitimately the wider of the two — measured at 1.04
        /// (bantam) and 1.02 (clipper), so ±0.25 leaves six times the observed margin and would still
        /// catch anchors and render describing different-length trailers.
        /// </summary>
        public const double LengthTolerance = 0.25;

        /// <summary>
        /// The hitch must land at least this many pixels off the pivot at a quarter turn for the sign
        /// to be trusted. At a quarter turn the offset is <c>(bodyL/2 + tongue)·32·cos(0)</c> — 107 px
        /// on the shorter of the two lengths — so anything near zero means the rig is not shaped the
        /// way this probe assumes.
        /// </summary>
        public const double MinHitchOffsetPx = 32.0;

        /// <summary>
        /// The quarter turn. The rig's own <c>order</c> labels cell 2 <c>'E'</c>, and dir units are 45°
        /// apiece (<c>camBasis</c> does <c>dir·π/4</c>), so 2 is a quarter turn from N.
        /// </summary>
        public const int QuarterTurnDir = 2;

        /// <summary>
        /// Measure a camper rig loaded into <paramref name="host"/>. <paramref name="optsJs"/> is the
        /// JS options literal the bake will use, so the probe measures the build actually being baked
        /// and not a default that might resolve differently.
        /// </summary>
        public static Result Measure(IRigScriptHost host, string globalName, string optsJs,
                                     int width, int height, double pivotX)
        {
            string g = globalName;
            string o = string.IsNullOrWhiteSpace(optsJs) ? "{}" : optsJs;
            string q = QuarterTurnDir.ToString(CultureInfo.InvariantCulture);

            // ---- 1. the hitch at a quarter turn (rig-native dir units: 2 of 8 == 90°) -------------
            double hitchX = host.EvaluateNumber($"{g}.anchors({q},{o}).hitch.x");
            double doorX = host.EvaluateNumber($"{g}.anchors({q},{o}).door.x");
            double loa = host.EvaluateNumber($"{g}.anchors({q},{o}).loa");
            double offset = hitchX - pivotX;

            // ---- 2. the rendered silhouette, broadside -------------------------------------------
            int broadside = SilhouetteWidthPx(host, g, o, QuarterTurnDir, width, height);

            var sb = new StringBuilder();
            sb.AppendLine("CAMPER AZIMUTH PROBE");
            sb.AppendLine($"  overall length   : {loa:F2} m (body + tongue)");
            sb.AppendLine($"  silhouette px    : broadside {broadside}, expected ~{loa * 32:F0} (+ jacks, coupler)");
            sb.AppendLine($"  hitch screen-x   : {hitchX:F1} px, pivot {pivotX:F1} px → offset {offset:+0.0;-0.0} px");
            sb.AppendLine($"  door screen-x    : {doorX:F1} px → offset {doorX - pivotX:+0.0;-0.0} px " +
                          "(the CURB side, not the heading — this is why the building probe cannot read this rig)");

            // ---- 3. refuse if anchors and render disagree about the camper ------------------------
            CheckLength(broadside, loa, sb);

            if (Math.Abs(offset) < MinHitchOffsetPx)
                throw new InvalidOperationException(
                    "CAMPER AZIMUTH PROBE REFUSED: the hitch sits essentially ON the pivot at a " +
                    $"quarter turn (offset {offset:F1} px, need ≥{MinHitchOffsetPx:F0}).\n\n" + sb +
                    "\nThis probe reads the coupler's side to tell which way the rig turns, so a " +
                    "centred hitch means the rig is not shaped the way it assumes — refusing rather " +
                    "than coin-flipping a convention.");

            // ---- 4. the answer ---------------------------------------------------------------------
            // Cell 2 is labelled 'E'. Hitch to screen-LEFT (−x, west) ⇒ the label is a lie by −90° ⇒ CCW.
            var convention = offset < 0
                ? AzimuthConvention.CounterClockwise
                : AzimuthConvention.Clockwise;

            sb.AppendLine($"  ⇒ cell {QuarterTurnDir} is labelled 'E' and its hitch points " +
                          $"{(offset < 0 ? "WEST (screen −x)" : "EAST (screen +x)")} ⇒ {convention}");

            return new Result(convention, offset, loa, broadside, sb.ToString());
        }

        static void CheckLength(int measuredPx, double meters, StringBuilder sb)
        {
            double expected = meters * 32.0;
            if (expected <= 0)
                throw new InvalidOperationException(
                    $"CAMPER AZIMUTH PROBE REFUSED: the rig reports loa = {meters} m.\n\n{sb}");

            double ratio = measuredPx / expected;
            if (ratio < 1.0 - LengthTolerance || ratio > 1.0 + LengthTolerance)
                throw new InvalidOperationException(
                    $"CAMPER AZIMUTH PROBE REFUSED: the broadside silhouette is {measuredPx} px but " +
                    $"the rig's own loa of {meters:F2} m is {expected:F0} px at 32 px = 1 m " +
                    $"(ratio {ratio:F2}, tolerance ±{LengthTolerance:P0}).\n\n" + sb +
                    "\nanchors() and render() are describing DIFFERENT trailers, so the hitch reading " +
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

            BuildingRigAzimuthProbe.AlphaBounds(rgba, width, height,
                                                out int xMin, out _, out int xMax, out _);
            return xMax < xMin ? 0 : xMax - xMin + 1;
        }
    }
}
