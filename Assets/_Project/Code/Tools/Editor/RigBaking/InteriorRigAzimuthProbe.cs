using System;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Determines the INTERIOR rig's turntable, and — the question the whole pilot rests on — which
    /// interior cell stands under which exterior cell.
    ///
    /// <para><b>Why <see cref="BuildingRigAzimuthProbe"/> cannot be reused, and would be confidently
    /// wrong.</b> That probe reads the door's screen-x at a quarter turn: door to screen-LEFT ⇒ the cell
    /// labelled east depicts west ⇒ counter-clockwise. The step it does not state is that it assumes the
    /// door sits on the <c>+Y</c> gable, which is true of both EXTERIOR rigs
    /// (<c>houseIsoRig.anchors → pj(0, +Ln/2, fH+1)</c>). <b><c>interiorIsoRig</c> puts its door on
    /// <c>−Y</c></b> (<c>pj(0, −Ln/2, fZ)</c>; the hearth takes the <c>+Y</c> wall), because the room is
    /// entered over the near wall the cutaway drops. Same turntable, mirrored anchor — so that probe
    /// reads the sign backwards and returns <c>Clockwise</c>, with a report that looks like evidence.
    /// The bake would then hand <c>DirForCell</c> the opposite correction and every cell of every
    /// interior sheet would depict the wrong heading. Nothing would throw.</para>
    ///
    /// <para><b>What this measures instead, in three layers, none of them a declaration.</b></para>
    /// <list type="number">
    ///   <item><b>Do the two rigs share a camera and a turntable at all?</b> Both expose
    ///   <c>project(dir, [x,y,z], elev)</c> — the same <c>camBasis</c>/<c>projVert</c> pair
    ///   <c>render()</c> draws with. Push identical model points through both at every facing and
    ///   compare screen pixels. Agreement to a fraction of a pixel is not evidence ABOUT the projection;
    ///   it IS the projection, evaluated. <see cref="ProjectionsAgree"/>.</item>
    ///   <item><b>Which gable is each rig's door on?</b> Measured, not read: at <c>dir 0</c> the model's
    ///   <c>+Y</c> projects AWAY from the camera and at <c>dir 4</c> it projects toward it, so
    ///   <c>door.y(4) − door.y(0) = s · Ln · sin(elev) · 32</c> where <c>s = ±1</c> is the gable. <b>The
    ///   door's height above the floor cancels out of that difference</b>, which is what makes it work
    ///   across a rig with a 0.55 m foundation and one whose floor is <c>z = 0</c>.
    ///   <see cref="DoorGable"/>.</item>
    ///   <item><b>Which interior cell registers under which exterior cell?</b> ONE constant offset
    ///   <c>k</c> has to line the door anchors up at ALL eight facings. If the two rigs turned opposite
    ///   ways no constant would do it — only two facings out of eight would ever coincide — so requiring
    ///   one <c>k</c> across the whole turn is simultaneously the handedness proof and the number the
    ///   runtime needs. <see cref="MeasureRegistration"/>.</item>
    /// </list>
    ///
    /// <para><b>What it deliberately does NOT claim.</b> Like the building probe, the handedness is not
    /// re-derived from pixels — a room has no bow taper either. It rests on the projection identity in
    /// layer 1, which is a stronger footing than the building probe's shared-source argument because it
    /// is evaluated per facing rather than read once.</para>
    /// </summary>
    public static class InteriorRigAzimuthProbe
    {
        /// <summary>Screen-px slack when comparing two rigs' <c>project()</c> output. They run the same
        /// double arithmetic on the same constants, so the honest expectation is bit-equality; this is
        /// the margin for a future rig that rounds somewhere.</summary>
        public const double ProjectionTolerancePx = 0.01;

        /// <summary>
        /// The door must move at least this far up the screen between <c>dir 0</c> and <c>dir 4</c> for
        /// its gable to be readable. The true travel is <c>Ln · sin(40°) · 32</c> — over 140 px for the
        /// smallest room — so anything near zero means the rig is not shaped the way this probe assumes.
        /// </summary>
        public const double MinGableTravelPx = 32.0;

        /// <summary>Screen-px slack when matching an interior door anchor to an exterior one. Both are
        /// the same projected ground point, so this is rounding margin, not a fudge factor.</summary>
        public const double RegistrationTolerancePx = 0.5;

        // =================================================================================
        //  layer 1 — do these two rigs share one camera and one turntable?
        // =================================================================================

        /// <summary>
        /// True when <paramref name="globalA"/> and <paramref name="globalB"/> project the same model
        /// points to the same place RELATIVE TO THEIR OWN PIVOTS at every facing.
        /// <paramref name="report"/> carries the worst disagreement either way.
        ///
        /// <para>⚠️ <b>Pivot-relative, and it has to be.</b> These rigs have different cells — the house
        /// is 992×1060 with its ground centre at (496, 676), the room 1180×900 at (590, 560) — so raw
        /// <c>project()</c> pixels are in two different frames and would disagree by the cell offset
        /// even for rigs that are identical. Subtracting each rig's own <c>pivot</c> puts both in the
        /// one frame that means anything: metres from the ground centre.</para>
        ///
        /// <para>The points are deliberately OFF-AXIS and asymmetric: a point on the <c>y</c> axis alone
        /// would agree between a rig and its own mirror image, which is exactly the failure this is
        /// looking for.</para>
        /// </summary>
        public static bool ProjectionsAgree(IRigScriptHost host, string globalA, string globalB,
                                            out string report)
        {
            double[][] points =
            {
                new[] { 1.0, 0.0, 0.0 },
                new[] { 0.0, 1.0, 0.0 },
                new[] { 2.5, -3.5, 1.25 },
                new[] { -1.75, 2.25, 0.5 },
            };

            double apx = host.EvaluateNumber($"{globalA}.pivot.x"), apy = host.EvaluateNumber($"{globalA}.pivot.y");
            double bpx = host.EvaluateNumber($"{globalB}.pivot.x"), bpy = host.EvaluateNumber($"{globalB}.pivot.y");

            double worst = 0;
            string worstWhere = "";

            for (int dir = 0; dir < 8; dir++)
            {
                foreach (var p in points)
                {
                    string js = $"[{Num(p[0])},{Num(p[1])},{Num(p[2])}]";
                    double ax = host.EvaluateNumber($"{globalA}.project({dir},{js}).x") - apx;
                    double ay = host.EvaluateNumber($"{globalA}.project({dir},{js}).y") - apy;
                    double bx = host.EvaluateNumber($"{globalB}.project({dir},{js}).x") - bpx;
                    double by = host.EvaluateNumber($"{globalB}.project({dir},{js}).y") - bpy;

                    double d = Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
                    if (d > worst)
                    {
                        worst = d;
                        worstWhere = $"dir {dir}, point ({p[0]},{p[1]},{p[2]}): " +
                                     $"{globalA} ({ax:F3},{ay:F3}) vs {globalB} ({bx:F3},{by:F3}), " +
                                     "both measured from their own ground centre";
                    }
                }
            }

            bool ok = worst <= ProjectionTolerancePx;
            report = ok
                ? $"{globalA} and {globalB} project identically at all 8 facings " +
                  $"(worst disagreement {worst:F4} px) — one camera, one turntable."
                : $"{globalA} and {globalB} DISAGREE by up to {worst:F3} px — {worstWhere}. They are " +
                  "not on the same turntable, so nothing below is measuring what it thinks it is.";
            return ok;
        }

        // =================================================================================
        //  layer 2 — which gable is the door on?
        // =================================================================================

        /// <summary>
        /// Which gable a rig's door anchor sits on: <c>+1</c> for the <c>+Y</c> gable (the exterior
        /// rigs), <c>−1</c> for <c>−Y</c> (the room, whose door is the wall the cutaway drops).
        ///
        /// <para>At <c>dir 0</c> the model's <c>+Y</c> projects away from the camera and at <c>dir 4</c>
        /// toward it, so <c>door.y(4) − door.y(0) = s · Ln · sin(elev) · 32</c>. The door's HEIGHT above
        /// the floor appears in both terms with the same sign and cancels — which is the only reason one
        /// measurement works on a rig with a 0.55 m stone foundation and on one whose floor is
        /// <c>z = 0</c>.</para>
        ///
        /// <para>Throws rather than guessing when the travel is too small to read.</para>
        /// </summary>
        public static int DoorGable(IRigScriptHost host, string globalName, string optsJs,
                                    out double travelPx)
        {
            string o = string.IsNullOrWhiteSpace(optsJs) ? "{}" : optsJs;
            double y0 = host.EvaluateNumber($"{globalName}.anchors(0,{o}).door.y");
            double y4 = host.EvaluateNumber($"{globalName}.anchors(4,{o}).door.y");
            travelPx = y4 - y0;

            if (Math.Abs(travelPx) < MinGableTravelPx)
                throw new InvalidOperationException(
                    $"INTERIOR AZIMUTH PROBE REFUSED: {globalName}'s door anchor barely moves between " +
                    $"dir 0 and dir 4 ({travelPx:F1} px, need ≥{MinGableTravelPx:F0}). Screen-y grows " +
                    "downward, so a door on the +Y gable must travel DOWN by Ln·sin(elev)·32 as the " +
                    "model turns to face the camera. A door on neither gable means this rig is not " +
                    "shaped the way the probe assumes — refusing rather than coin-flipping a side.");

            // Screen y grows DOWNWARD. A +Y door starts far (small y) and ends near (large y).
            return travelPx > 0 ? +1 : -1;
        }

        // =================================================================================
        //  layer 3 — the registration
        // =================================================================================

        public readonly struct Registration
        {
            /// <summary>Add this to an EXTERIOR facing to get the interior facing that stands under it,
            /// mod the facing count. Measured, never declared.</summary>
            public readonly int FacingOffset;

            /// <summary>The exterior's and the interior's door gable (<c>+1</c> = <c>+Y</c>).</summary>
            public readonly int ExteriorGable, InteriorGable;

            /// <summary>Footprint both rigs resolve for the probed size, in metres. Equal by
            /// construction only if the rigs really do share the footprint surface — measured here.</summary>
            public readonly double WidthMetres, LengthMetres;

            /// <summary>Constant screen-y gap between the exterior door anchor and the interior one
            /// once registered: the exterior's door anchor is a metre up its wall and stands on a
            /// foundation, the room's is on the floor. Constant across all facings, or the match is not
            /// a match.</summary>
            public readonly double DoorHeightGapPx;

            public readonly string Report;

            public Registration(int facingOffset, int exteriorGable, int interiorGable,
                                double widthMetres, double lengthMetres, double doorHeightGapPx,
                                string report)
            {
                FacingOffset = facingOffset;
                ExteriorGable = exteriorGable;
                InteriorGable = interiorGable;
                WidthMetres = widthMetres;
                LengthMetres = lengthMetres;
                DoorHeightGapPx = doorHeightGapPx;
                Report = report;
            }
        }

        /// <summary>
        /// Measure how an interior registers under an exterior. Both rigs must already be installed in
        /// <paramref name="host"/> (the room rig does not overwrite the house rig's global, so one host
        /// holds both).
        ///
        /// <para><paramref name="exteriorOptsJs"/> and <paramref name="interiorOptsJs"/> must resolve the
        /// SAME footprint — that is the pilot's whole premise, and it is asserted here rather than
        /// assumed, because the rigs share the <c>size → Wd/Ln</c> formula only for as long as nobody
        /// re-drops one of them.</para>
        ///
        /// <para>Throws with the full working when no single offset lines the doors up. That is the
        /// failure worth being loud about: without it a room would sit under a house with its doorway
        /// against the wrong wall, and the player would walk through the front door and appear at the
        /// back of the room.</para>
        /// </summary>
        public static Registration MeasureRegistration(IRigScriptHost host,
                                                       string exteriorGlobal, string exteriorOptsJs,
                                                       string interiorGlobal, string interiorOptsJs,
                                                       int facings = 8)
        {
            string eo = string.IsNullOrWhiteSpace(exteriorOptsJs) ? "{}" : exteriorOptsJs;
            string io = string.IsNullOrWhiteSpace(interiorOptsJs) ? "{}" : interiorOptsJs;

            var sb = new StringBuilder();
            sb.AppendLine("INTERIOR REGISTRATION PROBE");

            // ---- 1. one camera, one turntable ----------------------------------------------------
            if (!ProjectionsAgree(host, exteriorGlobal, interiorGlobal, out string projReport))
                throw new InvalidOperationException(
                    "INTERIOR REGISTRATION REFUSED: " + projReport + "\n\n" + sb +
                    "\nEverything below reads anchor positions off the assumption that both rigs place " +
                    "a model point on screen the same way. They do not, so refusing.");
            sb.AppendLine("  projection      : " + projReport);

            // ---- 2. the same footprint ------------------------------------------------------------
            double eWd = host.EvaluateNumber($"{exteriorGlobal}.anchors(0,{eo}).Wd");
            double eLn = host.EvaluateNumber($"{exteriorGlobal}.anchors(0,{eo}).Ln");
            double iWd = host.EvaluateNumber($"{interiorGlobal}.anchors(0,{io}).Wd");
            double iLn = host.EvaluateNumber($"{interiorGlobal}.anchors(0,{io}).Ln");
            sb.AppendLine($"  footprint (ext) : {eWd:F3} × {eLn:F3} m");
            sb.AppendLine($"  footprint (int) : {iWd:F3} × {iLn:F3} m");

            if (Math.Abs(eWd - iWd) > 1e-6 || Math.Abs(eLn - iLn) > 1e-6)
                throw new InvalidOperationException(
                    $"INTERIOR REGISTRATION REFUSED: the exterior resolves {eWd:F3} × {eLn:F3} m and " +
                    $"the interior {iWd:F3} × {iLn:F3} m for the same build.\n\n" + sb +
                    "\nThe pilot's premise is that ONE footprint surface drives both, so a room lands " +
                    "1:1 under its shell. Line the two option sets up (they share the size → Wd/Ln " +
                    "formula) rather than shimming the difference downstream.");

            // ---- 3. the door gables ---------------------------------------------------------------
            int eGable = DoorGable(host, exteriorGlobal, eo, out double eTravel);
            int iGable = DoorGable(host, interiorGlobal, io, out double iTravel);
            sb.AppendLine($"  door gable (ext): {(eGable > 0 ? "+Y" : "−Y")} " +
                          $"(door.y travels {eTravel:+0.0;-0.0} px from dir 0 to dir 4)");
            sb.AppendLine($"  door gable (int): {(iGable > 0 ? "+Y" : "−Y")} " +
                          $"(door.y travels {iTravel:+0.0;-0.0} px from dir 0 to dir 4)");

            // ---- 4. the one offset that lines all eight up ----------------------------------------
            // Pivot-relative, for the same reason ProjectionsAgree is: the two rigs' cells differ, so
            // raw anchor pixels live in different frames. Measured from each ground centre they live in
            // one.
            double ePivX = host.EvaluateNumber($"{exteriorGlobal}.pivot.x");
            double ePivY = host.EvaluateNumber($"{exteriorGlobal}.pivot.y");
            double iPivX = host.EvaluateNumber($"{interiorGlobal}.pivot.x");
            double iPivY = host.EvaluateNumber($"{interiorGlobal}.pivot.y");

            var exDoorX = new double[facings];
            var exDoorY = new double[facings];
            var inDoorX = new double[facings];
            var inDoorY = new double[facings];
            for (int d = 0; d < facings; d++)
            {
                exDoorX[d] = host.EvaluateNumber($"{exteriorGlobal}.anchors({d},{eo}).door.x") - ePivX;
                exDoorY[d] = host.EvaluateNumber($"{exteriorGlobal}.anchors({d},{eo}).door.y") - ePivY;
                inDoorX[d] = host.EvaluateNumber($"{interiorGlobal}.anchors({d},{io}).door.x") - iPivX;
                inDoorY[d] = host.EvaluateNumber($"{interiorGlobal}.anchors({d},{io}).door.y") - iPivY;
            }

            int found = -1;
            double gap = 0;
            for (int k = 0; k < facings && found < 0; k++)
            {
                double firstGap = 0;
                bool all = true;
                for (int d = 0; d < facings; d++)
                {
                    int j = (d + k) % facings;
                    if (Math.Abs(exDoorX[d] - inDoorX[j]) > RegistrationTolerancePx) { all = false; break; }

                    double g = exDoorY[d] - inDoorY[j];
                    if (d == 0) firstGap = g;
                    else if (Math.Abs(g - firstGap) > RegistrationTolerancePx) { all = false; break; }
                }
                if (all) { found = k; gap = firstGap; }
            }

            if (found < 0)
            {
                sb.AppendLine("  ⇒ NO constant offset lines the doors up. Per-facing door screen-x:");
                for (int d = 0; d < facings; d++)
                    sb.AppendLine($"      ext d{d} x={exDoorX[d]:F1}   int d{d} x={inDoorX[d]:F1}");
                throw new InvalidOperationException(
                    "INTERIOR REGISTRATION REFUSED: no single facing offset puts the room's doorway on " +
                    "the shell's door at all " + facings + " facings.\n\n" + sb +
                    "\nIf the two rigs turned opposite ways only two facings out of eight could ever " +
                    "coincide, so this is the handedness check as well as the offset. Refusing rather " +
                    "than shipping a house you enter at the front and appear at the back of.");
            }

            sb.AppendLine($"  ⇒ interior facing = (exterior facing + {found}) mod {facings}; the door " +
                          $"anchors then share a screen-x at every facing and sit a constant " +
                          $"{gap:F1} px apart vertically (the shell's door anchor is up its wall and on " +
                          "a foundation; the room's is on the floor).");

            return new Registration(found, eGable, iGable, iWd, iLn, gap, sb.ToString());
        }

        static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    }
}
