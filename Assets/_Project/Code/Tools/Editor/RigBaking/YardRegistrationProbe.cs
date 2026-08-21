using System;
using System.Globalization;
using System.Text;
using HiddenHarbours.Art.Editor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// ⭐ <b>THE MEASURED FACING TABLE — re-derived at every bake, and the bake REFUSES on a
    /// disagreement.</b>
    ///
    /// <para>This kit's gameplay sidecar publishes a <c>contract.facings</c> list that is MIRRORED
    /// against its own art: it reads clockwise while the rig steps counter-clockwise, so the two agree
    /// only at <c>dir 0</c> and <c>dir 4</c> and every other cell is 90° or 180° from its label. A
    /// consumer that trusts the label asks for "E" and draws a panel facing WEST — a plausible picture,
    /// with no error anywhere. That is why the ruling on PR #604 was <i>place from the MEASURED table,
    /// never <c>contract.facings</c></i>, and why the table is measured here at bake time instead of
    /// being transcribed into a constant that could go stale against a re-emitted rig.</para>
    ///
    /// <para><b>How the bearing is measured, and why not any other way.</b> The rig hands back SCREEN
    /// pixels, and screen y is foreshortened by <c>sin(elev)</c> — so an angle read straight off
    /// <c>(dx, dy)</c> is not a compass bearing, it is a squashed one, and the error is largest exactly
    /// on the diagonals where the mislabel lives. The projection is un-squashed FIRST, against scale
    /// factors read from the rig's own <c>project()</c> at <c>dir 0</c> rather than from any constant
    /// here, and the bearing is taken on the ground plane. (Same law as the iso anchor angles: divide
    /// out the foreshorten before you read an angle.)</para>
    ///
    /// <para><b>⚠️ What must NOT be used to settle this.</b> Neither the rig's <c>th = dir·π/4</c> sign
    /// term nor a screen "°/step" figure is a handedness test — the fuel kit measured the SAME
    /// ∓46.75°/step as its counter-clockwise sibling while turning the opposite way. Only the
    /// un-squashed ground-plane bearing distinguishes them.</para>
    /// </summary>
    public static class YardRegistrationProbe
    {
        /// <summary>How far a measured bearing may sit from its nominal before the probe refuses
        /// (degrees). The measurement is analytic — a projection of two exact model points — so the only
        /// error is double rounding; a tenth of a degree is four orders of magnitude of headroom and
        /// still nowhere near the 45° that would be needed to hide a mislabelled cell.</summary>
        public const double ToleranceDegrees = 0.1;

        public sealed class Report
        {
            /// <summary>The handedness measured in the RIG's own dir frame.</summary>
            public AzimuthConvention MeasuredInRigFrame;

            /// <summary>Face bearing per BAKED CELL, degrees clockwise from north.</summary>
            public double[] CellFaceBearings;

            /// <summary>Run-axis bearing per BAKED CELL, degrees clockwise from north.</summary>
            public double[] CellRunBearings;

            /// <summary>Face bearing per RIG dir — the table the kit's IMPORT.md records.</summary>
            public double[] RigDirFaceBearings;

            /// <summary>Degrees the face steps per baked cell. Positive is clockwise.</summary>
            public double CellStepDeg;

            /// <summary>Worst deviation of a baked cell's face from its nominal 45k (degrees).</summary>
            public double WorstCellErrorDeg;

            /// <summary>Worst deviation of (run − face) from 90° over the cells (degrees).</summary>
            public double WorstRunOffsetErrorDeg;

            /// <summary>How far the projected origin moves over the facings (px). The pivot is the
            /// ground centre, which the camera cannot move; measured rather than assumed.</summary>
            public double PivotDeviationPx;

            /// <summary>Horizontal px per metre, and px per metre of GROUND depth, read from the rig.
            /// </summary>
            public double PixelsPerMetre, GroundPixelsPerMetre;

            public string Text;
        }

        /// <summary>
        /// Measure the kit's facing table through an installed host and refuse if it contradicts
        /// <paramref name="declared"/>.
        /// </summary>
        public static Report Measure(IRigScriptHost host, string g, int facings,
                                     AzimuthConvention declared, double elevation)
        {
            if (facings <= 0) throw new ArgumentOutOfRangeException(nameof(facings));

            // ---- the two scale factors, read from the rig rather than restated --------------------
            // At dir 0 the model axes land on the screen axes: +X is pure horizontal (so its dx IS the
            // px/m), and +Y is pure vertical (so its −dy IS the FORESHORTENED px per metre of ground
            // depth). Everything below divides by these, so the bearings do not carry a hard-coded 32
            // or a hard-coded sin(40°) — a drop that re-seats either moves the measurement with it.
            Vector2 o0 = Project(host, g, 0, 0, 0, 0, elevation);
            double ppm = Project(host, g, 0, 1, 0, 0, elevation).x - o0.x;
            double groundPpm = o0.y - Project(host, g, 0, 0, 1, 0, elevation).y;

            if (ppm <= 1e-6 || groundPpm <= 1e-6)
                throw new InvalidOperationException(
                    $"{g}.project() reports {ppm:F4} px/m across and {groundPpm:F4} px/m of ground " +
                    "depth. Both must be positive — a non-positive value means the projection changed " +
                    "sign or shape, and every bearing below would be measured against a mirror.");

            // ---- the rig's own dir frame ----------------------------------------------------------
            var rigDirFace = new double[facings];
            for (int dir = 0; dir < facings; dir++)
                rigDirFace[dir] = FaceBearing(host, g, dir * (8.0 / facings), elevation, ppm, groundPpm);

            var measuredInRigFrame = StepOf(rigDirFace) < 0
                ? AzimuthConvention.CounterClockwise
                : AzimuthConvention.Clockwise;

            // ---- the BAKED CELL frame — after DirForCell's correction -----------------------------
            var cellFace = new double[facings];
            var cellRun = new double[facings];
            double pivotDev = 0;
            Vector2 pivot0 = Vector2.zero;

            for (int cell = 0; cell < facings; cell++)
            {
                double dir = RigBaker.DirForCell(cell, facings, declared);
                cellFace[cell] = FaceBearing(host, g, dir, elevation, ppm, groundPpm);
                cellRun[cell] = RunBearing(host, g, dir, elevation, ppm, groundPpm);

                Vector2 origin = Project(host, g, dir, 0, 0, 0, elevation);
                if (cell == 0) pivot0 = origin;
                else pivotDev = Math.Max(pivotDev, Vector2.Distance(origin, pivot0));
            }

            double step = 360.0 / facings;
            double worstCell = 0, worstRun = 0;
            for (int cell = 0; cell < facings; cell++)
            {
                worstCell = Math.Max(worstCell, AngleGap(cellFace[cell], step * cell));
                worstRun = Math.Max(worstRun, AngleGap(cellRun[cell] - cellFace[cell], 90.0));
            }

            var report = new Report
            {
                MeasuredInRigFrame = measuredInRigFrame,
                CellFaceBearings = cellFace,
                CellRunBearings = cellRun,
                RigDirFaceBearings = rigDirFace,
                CellStepDeg = StepOf(cellFace),
                WorstCellErrorDeg = worstCell,
                WorstRunOffsetErrorDeg = worstRun,
                PivotDeviationPx = pivotDev,
                PixelsPerMetre = ppm,
                GroundPixelsPerMetre = groundPpm,
            };
            report.Text = Describe(report, declared, facings);

            if (measuredInRigFrame != declared)
                throw new InvalidOperationException(
                    $"HANDEDNESS MISMATCH on '{YardKit.RigKey}' — refusing to bake.\n" +
                    $"  RigCatalog declares : {declared}\n" +
                    $"  the rig measures    : {measuredInRigFrame}\n\n" +
                    report.Text + "\n" +
                    "A wrong answer here does NOT fail: RigBaker.DirForCell simply writes the eight " +
                    "facings in reverse order, and every fence panel in both regions then faces the " +
                    "wrong way with a correct-looking sheet. Do not settle it from the rig's sign term " +
                    "or from a screen °/step figure — re-measure the un-squashed ground-plane bearing " +
                    "and correct whichever side the measurement contradicts.");

            if (worstCell > ToleranceDegrees)
                throw new InvalidOperationException(
                    $"BAKED CELLS ARE NOT A CLOCKWISE COMPASS on '{YardKit.RigKey}' — worst cell is " +
                    $"{worstCell:F3}° from its nominal, over a {ToleranceDegrees}° tolerance.\n\n" +
                    report.Text + "\n" +
                    "Every consumer reads cell k as bearing 45k. If that is no longer true the sheets " +
                    "cannot be placed by compass at all, and the placer's facing table would have to " +
                    "become a lookup — which is exactly the arrangement this kit's mirrored sidecar " +
                    "shows the cost of.");

            if (worstRun > ToleranceDegrees)
                throw new InvalidOperationException(
                    $"THE RUN AXIS IS NOT SQUARE TO THE FACE on '{YardKit.RigKey}' — worst offset is " +
                    $"{worstRun:F3}° from 90°.\n\n" + report.Text + "\n" +
                    "The kit's axis contract is '+X along the run, +Y away from the house'. A panel is " +
                    "placed by its FACE (the outward normal of the boundary segment) and drawn with its " +
                    "own length along that segment; if the two are not square, one of those two is " +
                    "wrong and the fence would stand at an angle to its own wall.");

            return report;
        }

        // ---- the measurement ---------------------------------------------------------------------

        /// <summary>Bearing of the model +Y axis — THE FACE, by the kit's own axis contract — in degrees
        /// clockwise from north.</summary>
        static double FaceBearing(IRigScriptHost host, string g, double dir, double elev,
                                  double ppm, double groundPpm) =>
            AxisBearing(host, g, dir, 0, 1, elev, ppm, groundPpm);

        /// <summary>Bearing of the model +X axis — the panel's own length.</summary>
        static double RunBearing(IRigScriptHost host, string g, double dir, double elev,
                                 double ppm, double groundPpm) =>
            AxisBearing(host, g, dir, 1, 0, elev, ppm, groundPpm);

        /// <summary>
        /// The world ground-plane bearing of a model axis, degrees clockwise from north.
        ///
        /// <para>⚠️ The <c>y</c> term is divided by the GROUND px/m, not by the horizontal one. Screen y
        /// carries <c>sin(elev)</c>; dividing by the wrong one leaves the bearing squashed, which is
        /// exactly zero error at the cardinals and its maximum on the diagonals — i.e. it looks right in
        /// the two places anybody spot-checks.</para>
        /// </summary>
        static double AxisBearing(IRigScriptHost host, string g, double dir, double mx, double my,
                                  double elev, double ppm, double groundPpm)
        {
            Vector2 o = Project(host, g, dir, 0, 0, 0, elev);
            Vector2 p = Project(host, g, dir, mx, my, 0, elev);

            double east = (p.x - o.x) / ppm;
            double north = (o.y - p.y) / groundPpm;    // screen y grows DOWNWARD

            double deg = Math.Atan2(east, north) * 180.0 / Math.PI;
            return deg < 0 ? deg + 360.0 : deg;
        }

        static Vector2 Project(IRigScriptHost host, string g, double dir,
                               double x, double y, double z, double elev)
        {
            string call = $"{g}.project({Num(dir)},[{Num(x)},{Num(y)},{Num(z)}],{Num(elev)})";
            return new Vector2((float)host.EvaluateNumber($"{call}.x"),
                               (float)host.EvaluateNumber($"{call}.y"));
        }

        /// <summary>Mean signed step between consecutive bearings, wrapped into (−180, 180]. Positive is
        /// clockwise. Taken as a MEAN rather than off one pair so a single bad cell cannot decide the
        /// handedness of a whole kit.</summary>
        static double StepOf(double[] bearings)
        {
            double sum = 0;
            for (int i = 1; i < bearings.Length; i++) sum += Wrap180(bearings[i] - bearings[i - 1]);
            return bearings.Length > 1 ? sum / (bearings.Length - 1) : 0;
        }

        static double Wrap180(double deg)
        {
            deg %= 360.0;
            if (deg > 180.0) deg -= 360.0;
            if (deg <= -180.0) deg += 360.0;
            return deg;
        }

        /// <summary>Smallest absolute angle between two bearings.</summary>
        public static double AngleGap(double a, double b) => Math.Abs(Wrap180(a - b));

        // ---- reporting ---------------------------------------------------------------------------

        static readonly string[] Compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        /// <summary>The nearest compass point to a bearing — for the human-readable table only. Never
        /// used to place anything.</summary>
        public static string NearestCompass(double bearingDeg)
        {
            int i = (int)Math.Round(((bearingDeg % 360.0) + 360.0) % 360.0 / 45.0) % 8;
            return Compass[i];
        }

        static string Describe(Report r, AzimuthConvention declared, int facings)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"  yard kit facing table — {facings} cells, elev {YardKit.Elevation}°, " +
                          $"{r.PixelsPerMetre:F3} px/m across, {r.GroundPixelsPerMetre:F3} px/m of " +
                          "ground depth (both read from the rig)");
            sb.AppendLine($"  rig dir frame  : steps {StepOf(r.RigDirFaceBearings):+0.000;-0.000}°/dir " +
                          $"⇒ {r.MeasuredInRigFrame}   (catalog declares {declared})");
            sb.AppendLine($"  baked cells    : step {r.CellStepDeg:+0.000;-0.000}°/cell, " +
                          $"worst {r.WorstCellErrorDeg:F4}° from nominal, " +
                          $"run−face worst {r.WorstRunOffsetErrorDeg:F4}° from 90°");
            sb.AppendLine($"  pivot moves    : {r.PivotDeviationPx:F4} px over all facings");

            sb.AppendLine("  cell : dir : face bearing        : run bearing");
            for (int cell = 0; cell < facings; cell++)
            {
                double dir = RigBaker.DirForCell(cell, facings, declared);
                sb.AppendLine($"   {cell}   :  {dir,-3:0.##} : {r.CellFaceBearings[cell],7:F2}° " +
                              $"{NearestCompass(r.CellFaceBearings[cell]),-2} : " +
                              $"{r.CellRunBearings[cell],7:F2}° {NearestCompass(r.CellRunBearings[cell])}");
            }

            sb.AppendLine("  ⚠️ the kit's own sidecar publishes contract.facings = " +
                          "[N,NE,E,SE,S,SW,W,NW] against DIR, which is the mirror of the rig-dir table " +
                          "above. It is documentation, not a placement table — see " +
                          "docs/art/rigs/yard-landscaping-kit/IMPORT.md §6.");
            return sb.ToString();
        }

        static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    }
}
