using System;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Determines which SHOP INTERIOR cell stands under which SHOPFRONT cell — the question the whole
    /// seamless-interior reveal rests on, measured rather than inherited.
    ///
    /// <para><b>Why this exists when <see cref="InteriorRigAzimuthProbe"/> already answers it for the
    /// house family.</b> It answers it for THAT family. Its answer is 4, and the reason is specific:
    /// <c>interiorIsoRig</c> puts its door on −Y (the wall the cutaway drops) while <c>houseIsoRig</c>
    /// puts one on +Y, so a room and its shell stand a half-turn apart. The shop kit is not built that
    /// way — its room is the shopfront seen from inside, and its own README says so: "the +Y wall is the
    /// street elevation from the inside". <b>Measured, both doors are on +Y and the offset is 0 at every
    /// one of the nine trades.</b> Carrying the house's 4 across by analogy would rotate every shop half
    /// a turn: the player walks in the street door and comes out behind the counter.</para>
    ///
    /// <para><b>So why measure a number that is expected to be 0?</b> Because "expected to be 0" is the
    /// same kind of claim that made 4 look safe to reuse. A drop that re-seats a doorway changes this
    /// silently and nothing else in the pipeline would notice — the sheets bake, the sprites slice, the
    /// building stands, and only the reveal is wrong. The measurement costs one host and eight anchor
    /// reads, and it fails the bake instead of the game.</para>
    ///
    /// <para><b>What it measures</b> — the same three layers, against the pair the reveal actually uses
    /// (the shell and the whole-building LEVEL, not a single room):</para>
    /// <list type="number">
    ///   <item>One camera and one turntable: both rigs expose <c>project()</c>; identical model points
    ///   must land identically relative to each rig's own pivot. <see cref="ProjectionsAgree"/>.</item>
    ///   <item>Which gable each door is on, from <c>door.y(4) − door.y(0)</c> — the door's height above
    ///   the floor cancels out of that difference. <see cref="DoorGable"/>.</item>
    ///   <item>The ONE offset that lines the doors up at ALL eight facings. If the two rigs turned
    ///   opposite ways no constant would do it, so requiring one is the handedness proof as well as the
    ///   offset. <see cref="Measure"/>.</item>
    /// </list>
    /// </summary>
    public static class ShopRegistrationProbe
    {
        /// <summary>Screen-px slack when comparing two rigs' <c>project()</c>. Measured worst case across
        /// the shop kit is 0.0000 px — they run the same arithmetic on the same constants — so this is
        /// margin for a future rig that rounds somewhere, not a working tolerance.</summary>
        public const double ProjectionTolerancePx = 0.01;

        /// <summary>The door must travel at least this far up the screen between dir 0 and dir 4 for its
        /// gable to be readable. True travel is <c>Ln · sin(elev) · 32</c> — 92 px for the smallest trade
        /// in the kit (the takeout stand), 236 px for the restaurant.</summary>
        public const double MinGableTravelPx = 32.0;

        /// <summary>Screen-px slack when matching a level's door anchor to its shell's. Both are the same
        /// projected ground point, so this is rounding margin.</summary>
        public const double RegistrationTolerancePx = 0.5;

        public readonly struct Registration
        {
            /// <summary>Add this to a SHELL facing to get the level facing that stands under it.</summary>
            public readonly int FacingOffset;

            /// <summary>Each rig's door gable: <c>+1</c> for +Y.</summary>
            public readonly int ShellGable, LevelGable;

            /// <summary>Footprint both rigs resolve, in metres — equal by construction only if the two
            /// really do share the footprint formula, so it is checked here rather than assumed.</summary>
            public readonly double WidthMetres, LengthMetres;

            /// <summary>Constant screen-y gap between the shell's door anchor and the level's once
            /// registered: the shell's sits up its wall, the level's on the floor. Constant across all
            /// eight facings, or the match is not a match.</summary>
            public readonly double DoorHeightGapPx;

            public readonly string Report;

            public Registration(int facingOffset, int shellGable, int levelGable,
                                double widthMetres, double lengthMetres, double doorHeightGapPx,
                                string report)
            {
                FacingOffset = facingOffset;
                ShellGable = shellGable; LevelGable = levelGable;
                WidthMetres = widthMetres; LengthMetres = lengthMetres;
                DoorHeightGapPx = doorHeightGapPx;
                Report = report;
            }
        }

        // =================================================================================
        //  layer 1 — one camera, one turntable?
        // =================================================================================

        /// <summary>
        /// True when the two globals project identical model points to the same place RELATIVE TO THEIR
        /// OWN PIVOTS at every facing.
        ///
        /// <para>Pivot-relative because it has to be: the shell's cell is a fixed 1320×1180 with its
        /// ground centre at (660, 800), while a level's cell is per trade and per level. Raw
        /// <c>project()</c> pixels live in two frames and would disagree by the cell offset even for
        /// rigs that are identical.</para>
        ///
        /// <para>The points are deliberately off-axis and asymmetric: a point on the y axis alone agrees
        /// between a rig and its own mirror image, which is exactly the failure being looked for.</para>
        /// </summary>
        public static bool ProjectionsAgree(IRigScriptHost host,
                                            string globalA, string pivotExprA, string projSuffixA,
                                            string globalB, string pivotExprB, string projSuffixB,
                                            out string report)
        {
            double[][] points =
            {
                new[] { 1.0, 0.0, 0.0 },
                new[] { 0.0, 1.0, 0.0 },
                new[] { 2.5, -3.5, 1.25 },
                new[] { -1.75, 2.25, 0.5 },
            };

            double apx = host.EvaluateNumber($"({pivotExprA}).x"), apy = host.EvaluateNumber($"({pivotExprA}).y");
            double bpx = host.EvaluateNumber($"({pivotExprB}).x"), bpy = host.EvaluateNumber($"({pivotExprB}).y");

            double worst = 0;
            string worstWhere = "";

            for (int dir = 0; dir < 8; dir++)
            {
                foreach (var p in points)
                {
                    string js = $"[{Num(p[0])},{Num(p[1])},{Num(p[2])}]";
                    double ax = host.EvaluateNumber($"{globalA}.project({dir},{js}{projSuffixA}).x") - apx;
                    double ay = host.EvaluateNumber($"{globalA}.project({dir},{js}{projSuffixA}).y") - apy;
                    double bx = host.EvaluateNumber($"{globalB}.project({dir},{js}{projSuffixB}).x") - bpx;
                    double by = host.EvaluateNumber($"{globalB}.project({dir},{js}{projSuffixB}).y") - bpy;

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
                : $"{globalA} and {globalB} DISAGREE by up to {worst:F3} px — {worstWhere}. They are not " +
                  "on the same turntable, so nothing below is measuring what it thinks it is.";
            return ok;
        }

        // =================================================================================
        //  layer 2 — which gable is the door on?
        // =================================================================================

        /// <summary>
        /// Which gable a rig's door anchor sits on: <c>+1</c> for +Y, <c>−1</c> for −Y.
        ///
        /// <para>At dir 0 the model's +Y projects away from the camera and at dir 4 toward it, so
        /// <c>door.y(4) − door.y(0) = s · Ln · sin(elev) · 32</c>. The door's HEIGHT above the floor
        /// appears in both terms with the same sign and cancels, which is what lets one measurement work
        /// on a shell whose anchor is a metre up its wall and on a floor plan whose anchor is on the
        /// deck.</para>
        /// </summary>
        public static int DoorGable(IRigScriptHost host, string anchorsExpr, out double travelPx)
        {
            double y0 = host.EvaluateNumber(anchorsExpr.Replace("$DIR", "0") + ".door.y");
            double y4 = host.EvaluateNumber(anchorsExpr.Replace("$DIR", "4") + ".door.y");
            travelPx = y4 - y0;

            if (Math.Abs(travelPx) < MinGableTravelPx)
                throw new InvalidOperationException(
                    $"SHOP REGISTRATION PROBE REFUSED: the door anchor of '{anchorsExpr}' barely moves " +
                    $"between dir 0 and dir 4 ({travelPx:F1} px, need ≥{MinGableTravelPx:F0}). Screen-y " +
                    "grows downward, so a door on the +Y gable must travel DOWN by Ln·sin(elev)·32 as the " +
                    "model turns to face the camera. A door on neither gable means this rig is not shaped " +
                    "the way the probe assumes — refusing rather than coin-flipping a side.");

            return travelPx > 0 ? +1 : -1;
        }

        // =================================================================================
        //  layer 3 — the registration
        // =================================================================================

        /// <summary>
        /// Measure how a shop LEVEL registers under its SHELL. Both rigs must already be installed in
        /// <paramref name="host"/> (they do not overwrite each other's globals, so one host holds the kit).
        ///
        /// <para><paramref name="shellOptsJs"/> and <paramref name="levelOptsJs"/> must resolve the same
        /// footprint — the premise of the whole kit, asserted here rather than assumed, because the three
        /// rigs share the size → Wd/Ln formula only for as long as nobody re-drops one of them.</para>
        ///
        /// <para>Throws with the full working when no single offset lines the doors up: without it a shop
        /// interior stands under its shell with the doorway against the wrong wall, and it reads as an
        /// art bug rather than a registration one.</para>
        /// </summary>
        public static Registration Measure(IRigScriptHost host,
                                           string shellGlobal, string shellOptsJs,
                                           string levelGlobal, string levelOptsJs,
                                           int facings = 8)
        {
            string so = string.IsNullOrWhiteSpace(shellOptsJs) ? "{}" : shellOptsJs;
            string lo = string.IsNullOrWhiteSpace(levelOptsJs) ? "{}" : levelOptsJs;

            var sb = new StringBuilder();
            sb.AppendLine("SHOP REGISTRATION PROBE");

            // The shell's pivot is a rig constant; a level's comes back with the bake, because its cell
            // is per trade and per level. Both are the ground centre of the footprint.
            string shellPivot = $"{shellGlobal}.pivot";
            string levelPivot = $"{levelGlobal}.anchors(0,{lo}).pivot";
            string shellAnchors = $"{shellGlobal}.anchors($DIR,{so})";
            string levelAnchors = $"{levelGlobal}.anchors($DIR,{lo})";

            // ---- 1. one camera, one turntable ----------------------------------------------------
            // ShopBuilding.project takes (dir, p, elev, opts) — the options carry the plan it is
            // projecting for — so the two calls differ by a trailing argument, not by their meaning.
            if (!ProjectionsAgree(host, shellGlobal, shellPivot, "",
                                        levelGlobal, levelPivot, $",undefined,{lo}", out string projReport))
                throw new InvalidOperationException(
                    "SHOP REGISTRATION REFUSED: " + projReport + "\n\n" + sb +
                    "\nEverything below reads anchor positions off the assumption that both rigs place a " +
                    "model point on screen the same way. They do not, so refusing.");
            sb.AppendLine("  projection      : " + projReport);

            // ---- 2. the same footprint ------------------------------------------------------------
            double sWd = host.EvaluateNumber($"{shellGlobal}.dims({so}).Wd");
            double sLn = host.EvaluateNumber($"{shellGlobal}.dims({so}).Ln");
            double lWd = host.EvaluateNumber($"{levelGlobal}.dims({lo}).Wd");
            double lLn = host.EvaluateNumber($"{levelGlobal}.dims({lo}).Ln");
            sb.AppendLine($"  footprint (shell): {sWd:F3} × {sLn:F3} m");
            sb.AppendLine($"  footprint (level): {lWd:F3} × {lLn:F3} m");

            if (Math.Abs(sWd - lWd) > 1e-6 || Math.Abs(sLn - lLn) > 1e-6)
                throw new InvalidOperationException(
                    $"SHOP REGISTRATION REFUSED: the shell resolves {sWd:F3} × {sLn:F3} m and the level " +
                    $"{lWd:F3} × {lLn:F3} m for the same build.\n\n" + sb +
                    "\nThe kit's premise is that ONE footprint formula drives all three rigs, so a plan " +
                    "lands 1:1 inside its shell. The usual cause is a size carried on one call and not " +
                    "the other (ShopKit.Build.Size exists to stop exactly that), or the interior rig not " +
                    "being loaded — it owns the 0.5 m cell snap the other two read back, and without it " +
                    "the shell resolves an UNSNAPPED footprint that is a few centimetres out.");

            // ---- 3. the door gables ---------------------------------------------------------------
            int sGable = DoorGable(host, shellAnchors, out double sTravel);
            int lGable = DoorGable(host, levelAnchors, out double lTravel);
            sb.AppendLine($"  door gable (shell): {(sGable > 0 ? "+Y" : "−Y")} " +
                          $"(door.y travels {sTravel:+0.0;-0.0} px from dir 0 to dir 4)");
            sb.AppendLine($"  door gable (level): {(lGable > 0 ? "+Y" : "−Y")} " +
                          $"(door.y travels {lTravel:+0.0;-0.0} px from dir 0 to dir 4)");

            // ---- 4. the one offset that lines all eight up ----------------------------------------
            double sPivX = host.EvaluateNumber($"({shellPivot}).x"), sPivY = host.EvaluateNumber($"({shellPivot}).y");
            double lPivX = host.EvaluateNumber($"({levelPivot}).x"), lPivY = host.EvaluateNumber($"({levelPivot}).y");

            var shX = new double[facings]; var shY = new double[facings];
            var lvX = new double[facings]; var lvY = new double[facings];
            for (int d = 0; d < facings; d++)
            {
                string sa = shellAnchors.Replace("$DIR", d.ToString(CultureInfo.InvariantCulture));
                string la = levelAnchors.Replace("$DIR", d.ToString(CultureInfo.InvariantCulture));
                shX[d] = host.EvaluateNumber($"{sa}.door.x") - sPivX;
                shY[d] = host.EvaluateNumber($"{sa}.door.y") - sPivY;
                lvX[d] = host.EvaluateNumber($"{la}.door.x") - lPivX;
                lvY[d] = host.EvaluateNumber($"{la}.door.y") - lPivY;
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
                    if (Math.Abs(shX[d] - lvX[j]) > RegistrationTolerancePx) { all = false; break; }

                    double g = shY[d] - lvY[j];
                    if (d == 0) firstGap = g;
                    else if (Math.Abs(g - firstGap) > RegistrationTolerancePx) { all = false; break; }
                }
                if (all) { found = k; gap = firstGap; }
            }

            if (found < 0)
            {
                sb.AppendLine("  ⇒ NO constant offset lines the doors up. Per-facing door screen-x:");
                for (int d = 0; d < facings; d++)
                    sb.AppendLine($"      shell d{d} x={shX[d]:F1}   level d{d} x={lvX[d]:F1}");
                throw new InvalidOperationException(
                    $"SHOP REGISTRATION REFUSED: no single facing offset puts the plan's street door on " +
                    $"the shell's door at all {facings} facings.\n\n" + sb +
                    "\nIf the two rigs turned opposite ways only two facings out of eight could ever " +
                    "coincide, so this is the handedness check as well as the offset. Refusing rather " +
                    "than shipping a shop you enter at the front and appear at the back of.");
            }

            sb.AppendLine($"  ⇒ level facing = (shell facing + {found}) mod {facings}; the door anchors " +
                          $"then share a screen-x at every facing and sit a constant {gap:F1} px apart " +
                          "vertically (the shell's anchor is up its wall, the plan's is on the floor).");
            if (found == 0)
                sb.AppendLine("     0 — MEASURED, not inherited. The house family's answer is 4 because " +
                              "interiorIsoRig's door is on −Y; both of this kit's doors are on +Y, so " +
                              "nothing needs turning.");

            return new Registration(found, sGable, lGable, lWd, lLn, gap, sb.ToString());
        }

        static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    }
}
