using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Proves, from pixels, that a hull's interior composites under her exterior 1:1</b> — the one
    /// property the whole boat-interiors program rests on (ADR 0038, and the kit's own
    /// <c>cell.note</c>: "baked to the full hull cell at the hull pivot — composites under the
    /// exterior 1:1").
    ///
    /// <para><b>Why this exists rather than a declaration.</b> The registration is TRUE BY
    /// CONSTRUCTION in the rig — <c>boatInteriorRig.js</c> renders into <c>env.L.cell</c>, which IS
    /// the exterior's <c>loft.cell</c>, on a camera basis it builds from the exterior's own
    /// <c>defaultElev</c>. Construction is exactly the kind of truth that stops being true quietly.
    /// A bake that only read the numbers would still pass the day a revised exterior rig moved its
    /// cell and the interior kept the old one, because both sides would have moved together in the
    /// FILE the bake happened to read.</para>
    ///
    /// <para><b>The handedness is measured from a BEARING, never from a silhouette.</b>
    /// <see cref="RigAzimuthProbe"/> reads a principal axis out of a rendered outline, and on this
    /// fleet it is simply wrong: all eighteen lobster VARIANTS measure "Clockwise" through it, at
    /// elongations well past the threshold that would have warned, while the ground-plane bearing of
    /// their own +X axis gives +45.000°/step with zero deviation — counter-clockwise, agreeing with
    /// the independent measurement already recorded in <c>RigCatalog.LobsterVariants.cs</c>. Baked on
    /// the silhouette's answer, every facing of all eighteen would be reversed, and every single cell
    /// would still look like a perfectly good cabin. So the silhouette's verdict is RECORDED and NOT
    /// USED (<see cref="Report.SilhouetteConvention"/>), and <see cref="MeasureConvention"/> is the
    /// authority. The interior inherits it rather than being probed itself, because the two share one
    /// turntable — which gate 3 below then checks from pixels instead of assuming.</para>
    /// </summary>
    public static class BoatInteriorRegistrationProbe
    {
        /// <summary>One hull's registration evidence.</summary>
        public sealed class Report
        {
            public string HullKey = "";
            public string HullStem = "";

            /// <summary>Cell and pivot as the INTERIOR rig reports them (<c>cellOf</c>).</summary>
            public int InteriorW, InteriorH, InteriorPivotX, InteriorPivotY, InteriorScale;

            /// <summary>Cell and pivot as the KIT's exterior rig declares them (<c>W/H/pivot</c>).</summary>
            public int KitExteriorW, KitExteriorH, KitExteriorPivotX, KitExteriorPivotY;

            /// <summary>Cell and pivot as the REPOSITORY's shipped exterior rig declares them. Zero
            /// when that rig is absent or exposes no cell.</summary>
            public int RepoExteriorW, RepoExteriorH, RepoExteriorPivotX, RepoExteriorPivotY;

            /// <summary>Cell and pivot as the interior SIDECAR states them.</summary>
            public int SidecarW, SidecarH, SidecarPivotX, SidecarPivotY;

            /// <summary>
            /// <b>The convention the cells are baked under</b>, measured from the exterior's
            /// un-squashed ground-plane bearing. See <see cref="MeasureConvention"/>.
            /// </summary>
            public AzimuthConvention Convention;

            /// <summary>Mean signed bearing step per 45° of <c>dir</c>. +45 is counter-clockwise.</summary>
            public double MeanStepDegrees;

            /// <summary>Worst departure of any single step from the mean. Doubles as the proof that
            /// the un-squash divisor is right: a wrong one makes the steps visibly uneven.</summary>
            public double MaxStepDeviationDegrees;

            /// <summary>
            /// What the SILHOUETTE heuristic (<see cref="RigAzimuthProbe"/>) said — recorded for the
            /// record and <b>deliberately not used</b>.
            ///
            /// <para>⚠️ On this fleet it is wrong for eighteen of the twenty-four hulls. Every lobster
            /// VARIANT measures "Clockwise" through it, at elongations of 2.3–3.2 — comfortably past
            /// the 1.5 threshold that would have produced a warning — while the ground-plane bearing
            /// gives +45.000°/step with zero deviation, counter-clockwise, agreeing with the
            /// independent measurement recorded in <c>RigCatalog.LobsterVariants.cs</c>. A confident
            /// wrong answer, exactly as the building-interior kit found. Baked on it, all eighteen
            /// variants would have had every facing reversed and every cell would still have looked
            /// like a plausible cabin.</para>
            /// </summary>
            public AzimuthConvention SilhouetteConvention;

            /// <summary>True when the silhouette heuristic disagreed with the bearing oracle.</summary>
            public bool SilhouetteDisagrees;

            public double Elongation;
            public string ConventionReport = "";

            /// <summary>Worst distance, in pixels, that any interior ink stood OUTSIDE the exterior
            /// hull's ink bounding box, over every level and every facing. Zero is the expected
            /// answer: a cabin is inside its boat.</summary>
            public int WorstOverhangPixels;

            /// <summary>The (level, facing) that produced <see cref="WorstOverhangPixels"/>.</summary>
            public string WorstOverhangCell = "";

            /// <summary>Cells compared.</summary>
            public int CellsProbed;

            public readonly List<string> Errors = new List<string>();

            public bool Ok => Errors.Count == 0;

            public override string ToString() =>
                $"{HullStem}: cell {InteriorW}×{InteriorH} pivot ({InteriorPivotX},{InteriorPivotY}) " +
                $"@{InteriorScale} px/m · {Convention} " +
                $"({MeanStepDegrees:+0.000;-0.000}°/step, dev {MaxStepDeviationDegrees:F3}°) · " +
                $"{CellsProbed} cells probed, worst overhang {WorstOverhangPixels} px" +
                (WorstOverhangPixels > 0 ? $" at {WorstOverhangCell}" : "") +
                (SilhouetteDisagrees
                     ? $" · NOTE the silhouette heuristic says {SilhouetteConvention} " +
                       $"(elongation {Elongation:F2}) — recorded, NOT used"
                     : "");
        }

        /// <summary>
        /// An interior whose ink reaches this far outside the exterior hull's ink bounding box is
        /// REFUSED.
        ///
        /// <para><b>Measured, across all 24 cleared hulls, every level, every facing.</b> The five
        /// ships (dragger, both trawlers, packet, tanker) and the 12 m lobster boat come out at
        /// exactly <b>0</b>. The worst case in the fleet is <b>3 px</b> — the Northumberland lobster
        /// variants' cuddy, at the two aft diagonals, where the sole's forward section lip clears the
        /// hull's own silhouette along the BOTTOM edge only.</para>
        ///
        /// <para><b>Why 4 px is still a tight guard and not a shrug.</b> In that same worst case the
        /// LATERAL margins are 11 px on the near side and 190 px on the far side, and no hull in the
        /// fleet has a lateral margin under 11 px. A turntable disagreeing by even one 45° step throws
        /// the room a large fraction of the cell sideways, so it lands hundreds of pixels out and
        /// trips this at the first diagonal. The tolerance absorbs a section-lip edge; it cannot
        /// absorb a rotation.</para>
        /// </summary>
        public const int OverhangTolerancePixels = 4;

        /// <summary>The rectangle of non-transparent pixels, or <c>x1 &lt; 0</c> when there are none.</summary>
        public static void InkBounds(byte[] rgba, int w, int h,
                                     out int x0, out int y0, out int x1, out int y1)
        {
            x0 = int.MaxValue; y0 = int.MaxValue; x1 = -1; y1 = -1;
            if (rgba == null || rgba.Length < w * h * 4) return;

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (rgba[(row + x) * 4 + 3] == 0) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }
        }

        /// <summary>
        /// How far <paramref name="inner"/>'s ink reaches outside <paramref name="outer"/>'s, in
        /// pixels. Negative-free: 0 means fully contained. An empty inner is 0 (nothing to contain);
        /// an empty OUTER with a non-empty inner is <see cref="int.MaxValue"/>, because a room drawn
        /// where the hull drew nothing is the failure this looks for, not a vacuous pass.
        /// </summary>
        public static int Overhang(byte[] inner, byte[] outer, int w, int h)
        {
            InkBounds(inner, w, h, out int ix0, out int iy0, out int ix1, out int iy1);
            if (ix1 < 0) return 0;

            InkBounds(outer, w, h, out int ox0, out int oy0, out int ox1, out int oy1);
            if (ox1 < 0) return int.MaxValue;

            int over = 0;
            if (ox0 - ix0 > over) over = ox0 - ix0;
            if (ix1 - ox1 > over) over = ix1 - ox1;
            if (oy0 - iy0 > over) over = oy0 - iy0;
            if (iy1 - oy1 > over) over = iy1 - oy1;
            return over;
        }

        /// <summary>
        /// Measure one hull. <paramref name="host"/> must already carry the KIT's exterior rig and the
        /// interior renderer; <paramref name="repoGeometry"/> is the shipped rig's cell read from a
        /// SEPARATE host (both files install the same global, so one host can only hold one of them —
        /// and whichever ran last would silently answer for both).
        /// </summary>
        public static Report Measure(IRigScriptHost host, string interiorHullKey, string hullStem,
                                     string exteriorGlobal,
                                     string variantSize, string variantStyle, string variantRegion,
                                     IReadOnlyList<string> levels,
                                     int sidecarW, int sidecarH, int sidecarPivotX, int sidecarPivotY,
                                     RigGeometry? repoGeometry)
        {
            var r = new Report { HullKey = interiorHullKey, HullStem = hullStem };

            // ⚠️ TWO SPELLINGS OF ONE FACT. The interior takes the variant NESTED under `variant`;
            // the exterior takes size/style/region at the TOP LEVEL of its own options. Each rig
            // falls through to a default boat when handed the other's shape, with no error — so the
            // probe builds both forms here rather than letting a caller pick one.
            string variantJs = BoatInteriorRigHost.VariantLiteral(variantSize, variantStyle, variantRegion);
            string extOpts = BoatInteriorRigHost.ExteriorOptsLiteral(variantSize, variantStyle, variantRegion);

            string cell = $"{BoatInteriorRigHost.Global}.cellOf('{interiorHullKey}')";
            if (!host.EvaluateBool($"!!{cell}"))
            {
                r.Errors.Add(
                    $"the interior rig resolves no environment for hull '{interiorHullKey}'. That is " +
                    "how it reports a missing or loft-less exterior rig — it returns null rather than " +
                    "throwing, and render() would then hand back a 4-byte buffer.");
                return r;
            }

            r.InteriorW = (int)host.EvaluateNumber($"{cell}.W");
            r.InteriorH = (int)host.EvaluateNumber($"{cell}.H");
            r.InteriorPivotX = (int)host.EvaluateNumber($"{cell}.cx");
            r.InteriorPivotY = (int)host.EvaluateNumber($"{cell}.cy");
            r.InteriorScale = (int)host.EvaluateNumber($"{cell}.S");

            if (!host.EvaluateBool($"typeof ({exteriorGlobal}).W === 'number'"))
            {
                r.Errors.Add(
                    $"'{exteriorGlobal}' exposes no numeric W — it is not a hull object. A multi-hull " +
                    "exterior publishes geometry PER HULL and the binding's pick must address the " +
                    "sub-hull (byId). A refusal here beats the InvalidCastException this read as before.");
                return r;
            }

            r.KitExteriorW = (int)host.EvaluateNumber($"{exteriorGlobal}.W");
            r.KitExteriorH = (int)host.EvaluateNumber($"{exteriorGlobal}.H");
            r.KitExteriorPivotX = (int)host.EvaluateNumber($"{exteriorGlobal}.pivot.x");
            r.KitExteriorPivotY = (int)host.EvaluateNumber($"{exteriorGlobal}.pivot.y");

            r.SidecarW = sidecarW; r.SidecarH = sidecarH;
            r.SidecarPivotX = sidecarPivotX; r.SidecarPivotY = sidecarPivotY;

            if (repoGeometry.HasValue)
            {
                r.RepoExteriorW = repoGeometry.Value.Width;
                r.RepoExteriorH = repoGeometry.Value.Height;
                r.RepoExteriorPivotX = (int)Math.Round(repoGeometry.Value.PivotX);
                r.RepoExteriorPivotY = (int)Math.Round(repoGeometry.Value.PivotY);
            }

            // ---- gate 1: the four declarations of one cell must be one number -------------------
            Agree(r, "the KIT's exterior rig", r.KitExteriorW, r.KitExteriorH,
                  r.KitExteriorPivotX, r.KitExteriorPivotY);
            Agree(r, "the interior SIDECAR", r.SidecarW, r.SidecarH, r.SidecarPivotX, r.SidecarPivotY);
            if (repoGeometry.HasValue)
                Agree(r, "the REPOSITORY's shipped exterior rig", r.RepoExteriorW, r.RepoExteriorH,
                      r.RepoExteriorPivotX, r.RepoExteriorPivotY);

            if (!r.Ok) return r;

            // ---- gate 2: the convention, from the EXTERIOR's ground-plane bearing ---------------
            byte[] quarter = host.EvaluateBytes($"{exteriorGlobal}.render(2,{extOpts})");
            if (quarter.Length != r.InteriorW * r.InteriorH * 4)
            {
                r.Errors.Add($"{exteriorGlobal}.render() returned {quarter.Length} bytes where the " +
                             $"cell needs {r.InteriorW * r.InteriorH * 4}. The exterior rig is not " +
                             "drawing into the cell its own loft publishes.");
                return r;
            }

            MeasureConvention(host, exteriorGlobal, extOpts, r);
            if (!r.Ok) return r;

            // The silhouette heuristic's answer, RECORDED AND NOT USED. See the field's own note: on
            // this fleet it is wrong for eighteen of the twenty-four hulls, and confidently so.
            var silhouette = RigAzimuthProbe.MeasureFromQuarterTurn(quarter, r.InteriorW, r.InteriorH);
            r.SilhouetteConvention = silhouette.Convention;
            r.Elongation = silhouette.Elongation;
            r.ConventionReport = silhouette.Report;
            r.SilhouetteDisagrees = silhouette.Convention != r.Convention;

            // ---- gate 3: containment, every level, every facing ---------------------------------
            for (int li = 0; li < levels.Count; li++)
            {
                for (int k = 0; k < BoatInteriorRigHost.Facings; k++)
                {
                    double dir = RigBaker.DirForCell(k, BoatInteriorRigHost.Facings, r.Convention);
                    string d = dir.ToString("R", CultureInfo.InvariantCulture);

                    byte[] ext = host.EvaluateBytes($"{exteriorGlobal}.render({d},{extOpts})");
                    byte[] inn = host.EvaluateBytes(
                        BoatInteriorRigHost.RenderExpression(interiorHullKey, levels[li], variantJs, d));

                    r.CellsProbed++;

                    if (inn.Length != r.InteriorW * r.InteriorH * 4)
                    {
                        r.Errors.Add(
                            $"level '{levels[li]}' facing {k} rendered {inn.Length} bytes, expected " +
                            $"{r.InteriorW * r.InteriorH * 4}. The rig returns a 4-byte buffer when it " +
                            "cannot resolve the hull — a refusal that looks like a tiny picture.");
                        return r;
                    }

                    int over = Overhang(inn, ext, r.InteriorW, r.InteriorH);
                    if (over > r.WorstOverhangPixels)
                    {
                        r.WorstOverhangPixels = over;
                        r.WorstOverhangCell = $"{levels[li]}/d{k}";
                    }
                }
            }

            if (r.WorstOverhangPixels > OverhangTolerancePixels)
                r.Errors.Add(
                    $"interior ink stands {r.WorstOverhangPixels} px outside the hull's own silhouette " +
                    $"at {r.WorstOverhangCell} (tolerance {OverhangTolerancePixels} px). The room and " +
                    "the boat are not on the same turntable, so the sheet would not composite under " +
                    "her — which is the entire point of this art.");

            return r;
        }

        /// <summary>
        /// A single step of <c>dir</c> may deviate from the mean by no more than this. The oracle
        /// returns 0.000 on all eight of this fleet's exterior rigs, so the bar is a formality — but
        /// it is the bar that catches a wrong un-squash divisor, which is the one way this measurement
        /// can go quietly wrong (÷1.0 gives 12.27°, sin 30° gives 7.12°, sin 50° gives 5.00°; only
        /// sin 40° returns 0.00, which independently confirms the elevation as well).
        /// </summary>
        public const double MaxStepDeviationDegrees = 0.5;

        /// <summary>
        /// <b>The handedness of the turntable, from the un-squashed ground-plane bearing of the +X
        /// axis.</b>
        ///
        /// <para>Every hull rig in this fleet exposes <c>navMounts(dir, opts)</c> with a
        /// <c>port</c>/<c>star</c> pair that is a PURE ±X separation at equal y and z. Projected, that
        /// pair's screen displacement is <c>(Δxr·S, −Δyr·S·sin e)</c> — so dividing the screen Δy by
        /// <c>sin(elev)</c> BEFORE taking any angle recovers the true ground bearing, and <c>S</c>
        /// cancels. Step that bearing through the eight headings: <b>+45° per step is
        /// counter-clockwise, −45° is clockwise</b>, and the answer is in the SIGN.</para>
        ///
        /// <para><b>Why not the silhouette probe.</b> Because it is wrong here, and it says so with a
        /// straight face — see <see cref="Report.SilhouetteConvention"/>. Handedness is a property of
        /// the camera basis, and a screen-space measurement of a mirrored rig returns the same
        /// magnitude with the opposite sign, which a silhouette's principal axis simply does not
        /// carry. This is the same oracle, and the same reasoning, that
        /// <c>RigCatalog.LobsterVariants.cs</c> records having used and sabotage-proved.</para>
        ///
        /// <para>⚠️ <b><c>RigMeshAssetBaker</c> measures the same thing, privately, and states its sign
        /// the other way up.</b> Its <c>GroundBearingOfAbeamPair</c> takes ONE bearing at a quarter
        /// turn without negating the screen Δy, so there <c>bearing &gt; 0</c> means CLOCKWISE and this
        /// fleet lands on exactly −90.00°; here the Δy is negated and eight bearings are STEPPED, so
        /// <c>+45°/step</c> means COUNTER-clockwise. <b>The two agree on every hull</b> — they are the
        /// same measurement written from opposite ends — and the stepped form additionally proves the
        /// divisor by returning a zero deviation. They are separate only because that one is a private
        /// static inside another lane's baker; <b>folding both into one shared helper is worth doing,
        /// and deliberately was not done inside an art PR.</b> If you change one, change both.</para>
        /// </summary>
        public static void MeasureConvention(IRigScriptHost host, string exteriorGlobal,
                                             string extOpts, Report r)
        {
            if (!host.EvaluateBool($"typeof {exteriorGlobal}.navMounts === 'function'"))
            {
                r.Errors.Add(
                    $"{exteriorGlobal} exposes no navMounts(), so the ±X pair the handedness oracle " +
                    "needs is not there. Refusing rather than falling back to the silhouette " +
                    "heuristic, which is wrong on most of this fleet.");
                return;
            }
            if (!host.EvaluateBool($"typeof {exteriorGlobal}.defaultElev === 'number'"))
            {
                r.Errors.Add($"{exteriorGlobal} states no defaultElev, so the ground plane cannot be " +
                             "un-squashed and no bearing can be taken.");
                return;
            }

            double elev = host.EvaluateNumber($"{exteriorGlobal}.defaultElev");
            double se = Math.Sin(elev * Math.PI / 180.0);
            if (se < 1e-6)
            {
                r.Errors.Add($"{exteriorGlobal}.defaultElev is {elev}° — a camera on the ground plane " +
                             "has no vertical to un-squash by.");
                return;
            }

            var bearings = new double[BoatInteriorRigHost.Facings];
            for (int k = 0; k < bearings.Length; k++)
            {
                string nav = $"{exteriorGlobal}.navMounts({k},{extOpts})";
                double dx = host.EvaluateNumber($"{nav}.star.x") - host.EvaluateNumber($"{nav}.port.x");
                // Screen y grows DOWNWARD while the ground plane's y grows away from the camera, so
                // the sign flips here and the un-squash happens BEFORE the angle, never after.
                double dy = -(host.EvaluateNumber($"{nav}.star.y") - host.EvaluateNumber($"{nav}.port.y")) / se;

                if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
                {
                    r.Errors.Add($"{exteriorGlobal}.navMounts({k}) puts port and starboard on the same " +
                                 "point, so there is no ±X vector to take a bearing from.");
                    return;
                }
                bearings[k] = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            }

            double sum = 0;
            for (int k = 0; k < bearings.Length; k++)
                sum += Wrap(bearings[(k + 1) % bearings.Length] - bearings[k]);
            r.MeanStepDegrees = sum / bearings.Length;

            for (int k = 0; k < bearings.Length; k++)
            {
                double dev = Math.Abs(Wrap(bearings[(k + 1) % bearings.Length] - bearings[k]) -
                                      r.MeanStepDegrees);
                if (dev > r.MaxStepDeviationDegrees) r.MaxStepDeviationDegrees = dev;
            }

            if (r.MaxStepDeviationDegrees > MaxStepDeviationDegrees)
            {
                r.Errors.Add(
                    $"the bearing of {exteriorGlobal}'s +X axis steps unevenly through the eight " +
                    $"headings — worst departure {r.MaxStepDeviationDegrees:F3}° from a mean of " +
                    $"{r.MeanStepDegrees:F3}°. On a well-formed turntable this is 0.000. An uneven " +
                    "step means the un-squash divisor (or the elevation it comes from) is wrong, so " +
                    "the SIGN of the mean cannot be trusted either — and the sign is the whole answer.");
                return;
            }

            r.Convention = r.MeanStepDegrees > 0
                               ? AzimuthConvention.CounterClockwise
                               : AzimuthConvention.Clockwise;

            // Cross-check against the catalog for the rigs that are registered there. Two of the eight
            // are (the hero lobster boat and the variants generator); the five ships live in
            // HullMeshFleet and declare nothing, which is why the oracle has to stand on its own.
            foreach (var kv in RigCatalog.Entries)
            {
                if (!string.Equals(GlobalOf(kv.Value), exteriorGlobal, StringComparison.Ordinal)) continue;
                if (kv.Value.DeclaredConvention == r.Convention) continue;

                r.Errors.Add(
                    $"MEASURED {r.Convention} ({r.MeanStepDegrees:+0.000;-0.000}°/step) but " +
                    $"RigCatalog['{kv.Key}'] DECLARES {kv.Value.DeclaredConvention}. Refusing rather " +
                    "than guessing: getting this wrong does not fail, it writes every facing in " +
                    "reverse order and each cell still looks correct. Settle it by eye, then correct " +
                    "the catalog.");
                return;
            }
        }

        static string GlobalOf(in RigEntry e) => e.GlobalName;

        static double Wrap(double deg)
        {
            while (deg > 180) deg -= 360;
            while (deg <= -180) deg += 360;
            return deg;
        }

        static void Agree(Report r, string who, int w, int h, int px, int py)
        {
            if (w == r.InteriorW && h == r.InteriorH && px == r.InteriorPivotX && py == r.InteriorPivotY)
                return;

            r.Errors.Add(
                $"{who} says cell {w}×{h} pivot ({px},{py}) but the interior renders into " +
                $"{r.InteriorW}×{r.InteriorH} pivot ({r.InteriorPivotX},{r.InteriorPivotY}). " +
                "The interior sheet must land in the SAME cell at the SAME pivot as the exterior or it " +
                "does not composite under her — refusing rather than baking art that is quietly off " +
                "its boat.");
        }

        /// <summary>A one-line summary of a whole run, for the bake log.</summary>
        public static string Summarise(IReadOnlyList<Report> reports)
        {
            var sb = new StringBuilder();
            int worst = 0; string worstAt = "";
            int cells = 0, ccw = 0, cw = 0, disagreed = 0;
            double worstDev = 0;
            foreach (var r in reports)
            {
                cells += r.CellsProbed;
                if (r.WorstOverhangPixels > worst) { worst = r.WorstOverhangPixels; worstAt = $"{r.HullStem} {r.WorstOverhangCell}"; }
                if (r.MaxStepDeviationDegrees > worstDev) worstDev = r.MaxStepDeviationDegrees;
                if (r.Convention == AzimuthConvention.CounterClockwise) ccw++; else cw++;
                if (r.SilhouetteDisagrees) disagreed++;
            }
            sb.Append($"registration: {reports.Count} hull(s), {cells} cells probed, ");
            sb.Append(worst == 0
                          ? "every interior fully inside its hull's silhouette"
                          : $"worst overhang {worst} px at {worstAt} (tolerance {OverhangTolerancePixels})");
            sb.Append($" · handedness {ccw} CCW / {cw} CW, worst step deviation {worstDev:F3}°");
            if (disagreed > 0)
                sb.Append($" · ⚠ the silhouette heuristic disagreed on {disagreed} hull(s) and was " +
                          "overruled — see the per-hull lines");
            return sb.ToString();
        }
    }
}
