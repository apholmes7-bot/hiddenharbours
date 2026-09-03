using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Where every hull in the fleet wears her lamps, DERIVED FROM HER OWN RIG</b> (ADR 0016,
    /// boat-lights PR 2) — the measurement behind <see cref="HullMeshDef.Lamps"/>, published as a
    /// table a reviewer can re-run rather than as a number in a commit nobody can check.
    ///
    /// <para><b>It PRINTS; it does not write.</b> Deliberately, and the field's own documentation says
    /// why: the mesh baker must not author <c>Lamps</c> until the export contract grows a boat-local
    /// <c>NAV</c> table beside <c>navMounts</c> (the upstream ask #686 filed, which only
    /// <c>sportFisherIsoRig2</c> has answered so far). Until then the def is hand-authored data and
    /// this is the instrument that says what it should contain — so a rig revision that moves a
    /// sidelight is caught by <c>BoatLampAnchorTests</c> going red, and fixed by re-running this and
    /// copying the row, rather than by somebody nudging numbers until a picture looks right.</para>
    ///
    /// <para><b>⭐ The inversion, and why it is exact rather than a fit.</b> The rigs publish
    /// <c>navMounts(dir)</c> as SCREEN points — the projected answer, one per facing — because that is
    /// what a sprite bake needs. A mesh hull needs the boat-local triple behind the projection. At
    /// rest (no roll, no pitch, no heave) that projection is AFFINE in (x, y, z), so eight facings are
    /// sixteen linear equations in three unknowns: solve them in the least-squares sense and the
    /// residual is not a fitting error, it is floating-point noise. Measured across all twenty-seven
    /// hulls: worst 2.3e-13 px. Anything above <see cref="ResidualLimitPx"/> means the assumption
    /// broke — a rig that heaves its nav lamps, or a non-linear camera — and the probe says so instead
    /// of quietly reporting an average.</para>
    ///
    /// <para><b>Two independent checks that the inversion is right, both run by the tests.</b> (1) The
    /// sport fisher's rig publishes her boat-local <c>NAV</c> table directly; the inversion reproduces
    /// it to 3.6e-15 m. (2) The Cape Islander's shipped def was measured by a different lane, by a
    /// different method, four days earlier; the inversion reproduces her six values to 1.8e-15 m.</para>
    ///
    /// <para><b>And the projection used here is the RUNTIME's</b> (<see cref="IsoFacetMath.RigToWorld"/>),
    /// not a transcription of the rig's — so the numbers this yields are the numbers the game will
    /// draw with, and the join test that re-projects them against <c>navMounts</c> is still comparing
    /// two independent computations rather than one with itself.</para>
    /// </summary>
    public static class BoatLampAnchorProbe
    {
        /// <summary>
        /// Above this the affine assumption has broken and the answer is not a measurement.
        ///
        /// <para><b>A hundredth of a pixel, and the floor is set by FLOAT, not by the maths.</b> The
        /// inversion is solved in double, but its rows come from <see cref="IsoFacetMath.RigToWorld"/>,
        /// which returns a <c>Matrix4x4</c> — single precision, deliberately, because that is the map
        /// the game actually poses lamps with. So the residual floor is float epsilon times the cell,
        /// and the tanker's cell is 1920 px wide. (Measured in double against a transcription of the
        /// same projection, the residual is 2.3e-13 px: the model is exact, and what is left here is
        /// the runtime's own arithmetic.) A hundredth of a pixel is still ten times tighter than the
        /// join test's own 0.1 px tolerance, and nothing real drifts by less than that.</para>
        /// </summary>
        public const double ResidualLimitPx = 1e-2;

        /// <summary>How far AFT of the forward face of the room she is conned from a searchlight
        /// bracket sits, in metres. Calibrated on the Cape Islander's shipped mount, which #686
        /// measured by hand: her wheelhouse front is at y 2.54 and her searchlight at y 2.40.</summary>
        public const float SearchlightBracketAftMetres = 0.14f;

        /// <summary>And how far ABOVE that room's roof. Same calibration: her roof is at z 3.02 and
        /// her searchlight at z 3.10.
        ///
        /// <para>Descriptive rather than load-bearing — <see cref="HullLampKind.Spotlight"/> reads only
        /// x and y, because a beam aimed in the boat's plane lights the same patch of sea from one
        /// metre higher. It is recorded so the row says where the lamp actually is.</para>
        /// </summary>
        public const float SearchlightBracketUpMetres = 0.08f;

        /// <summary>
        /// How far the shipped <see cref="BoatSpotlight"/> throws, in metres — the number that decides
        /// which hulls are given a searchlight at all (see <see cref="ClearsHerOwnStem"/>). Read from
        /// the component's own default rather than restated, so a retune moves the verdict with it.
        /// </summary>
        public static float ShippedBeamRangeMetres => BoatSpotlight.DefaultRangeMetres;

        // -------------------------------------------------------------------------------------------

        /// <summary>One lamp station, as the probe derived it.</summary>
        public readonly struct Station
        {
            public readonly HullLampKind Kind;
            public readonly Vector3 RigLocalMetres;
            /// <summary>Worst disagreement, in cell pixels, between the derived triple re-projected
            /// and the rig's own published screen point — across every facing. Zero for a station the
            /// rig does not project (the cabin and the searchlight, which are placed against her
            /// published HOUSE box instead).</summary>
            public readonly double ResidualPx;
            /// <summary>What the rig calls this station, or how it was placed. For the printed table.</summary>
            public readonly string Source;

            public Station(HullLampKind kind, Vector3 p, double residualPx, string source)
            {
                Kind = kind; RigLocalMetres = p; ResidualPx = residualPx; Source = source;
            }
        }

        /// <summary>Everything the probe found out about one hull.</summary>
        public sealed class HullLamps
        {
            public string Key;
            public string MeshAssetPath;
            public readonly List<Station> Stations = new List<Station>();
            /// <summary>Distance between her two sidelights, metres. The number that bounds the
            /// sidelight preset's radius fleetwide — red and green must never overlap into yellow.</summary>
            public double SidelightSeparationMetres;
            /// <summary>Her stem station in rig metres, and how far short of it (negative) or past it
            /// (positive) the shipped beam reaches from her searchlight bracket.</summary>
            public double StemY, BeamClearanceMetres;
            /// <summary>Null when she clears her own stem; otherwise why she carries no searchlight.</summary>
            public string SearchlightRefusal;
        }

        // -------------------------------------------------------------------------------------------
        //  the menu
        // -------------------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Rig Baking/Probe: boat lamp anchors (print the table)")]
        public static void PrintTable()
        {
            string report = Report();
            string path = Path.Combine(RigCatalog.RepoRoot, "artifacts", "boat-lamp-anchors.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report);
            Debug.Log(report);
            Debug.Log("[boat-lamps] table written to " + path);
        }

        /// <summary>The whole fleet's lamp table, as text — one block per hull, plus the two numbers
        /// the presets are bounded by.</summary>
        public static string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("BOAT LAMP ANCHORS — derived from each hull's own rig (ADR 0016, PR 2).");
            sb.AppendLine("Rig metres: +x starboard, +y toward the bow, +z up from the keel.");
            sb.AppendLine();

            double worstResidual = 0, tightestPair = double.MaxValue;
            string tightestHull = "";
            int withSearchlight = 0;

            foreach (HullLamps h in Measure())
            {
                sb.AppendLine("── " + h.Key + "   (" + Path.GetFileName(h.MeshAssetPath) + ")");
                foreach (Station s in h.Stations)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "     {0,-19} ({1,10:F4}, {2,10:F4}, {3,10:F4})   {4}",
                        s.Kind, s.RigLocalMetres.x, s.RigLocalMetres.y, s.RigLocalMetres.z, s.Source);
                    if (s.ResidualPx > 0)
                        sb.AppendFormat(CultureInfo.InvariantCulture, "  [residual {0:E2} px]", s.ResidualPx);
                    sb.AppendLine();
                    worstResidual = Math.Max(worstResidual, s.ResidualPx);
                    if (s.Kind == HullLampKind.Spotlight) withSearchlight++;
                }
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "     sidelight separation {0:F4} m · stem y {1:F3} · beam clearance {2:+0.000;-0.000} m",
                    h.SidelightSeparationMetres, h.StemY, h.BeamClearanceMetres);
                sb.AppendLine();
                if (h.SearchlightRefusal != null)
                    sb.AppendLine("     NO SEARCHLIGHT — " + h.SearchlightRefusal);
                if (h.SidelightSeparationMetres < tightestPair)
                {
                    tightestPair = h.SidelightSeparationMetres;
                    tightestHull = h.Key;
                }
                sb.AppendLine();
            }

            sb.AppendLine("── the two numbers the presets are bounded by ──");
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "  tightest sidelight pair in the fleet: {0:F4} m ({1}) — the radius must stay under " +
                "half of it ({2:F4} m) or red and green overlap into yellow.",
                tightestPair, tightestHull, tightestPair / 2);
            sb.AppendLine();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "  worst inversion residual anywhere: {0:E3} px (limit {1:E0}).",
                worstResidual, ResidualLimitPx);
            sb.AppendLine();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "  hulls carrying a searchlight: {0} (shipped beam {1:F1} m).",
                withSearchlight, ShippedBeamRangeMetres);
            sb.AppendLine();
            return sb.ToString();
        }

        // -------------------------------------------------------------------------------------------
        //  the measurement
        // -------------------------------------------------------------------------------------------

        /// <summary>Every hull whose rig publishes nav mounts, measured. One V8 host per rig FILE, so
        /// eighteen lobster variants share one load and a generator's module state cannot leak from
        /// one variant into the next (each call passes its own descriptor).</summary>
        public static List<HullLamps> Measure()
        {
            var results = new List<HullLamps>();
            var byScript = new Dictionary<string, List<FleetHull>>();
            foreach (FleetHull hull in HullMeshFleet.Hulls)
                (byScript.TryGetValue(hull.ScriptPath, out var l) ? l : byScript[hull.ScriptPath] = new List<FleetHull>())
                    .Add(hull);

            foreach (var pair in byScript)
            {
                string full = Path.Combine(RigCatalog.RepoRoot, pair.Key);
                if (!File.Exists(full)) continue;

                using var host = new V8RigScriptHost();
                host.Execute(File.ReadAllText(full));

                foreach (FleetHull hull in pair.Value)
                {
                    HullLamps measured = MeasureOne(host, hull);
                    if (measured != null) results.Add(measured);
                }
            }
            return results;
        }

        static HullLamps MeasureOne(IRigScriptHost host, FleetHull hull)
        {
            // ⚠️ ScopeOr, never string concatenation: HullScope is written WITHOUT its separator
            // ("byId('convertible')"), and "the global unless scoped" is a rule that already has
            // exactly one home. Gluing the two together yields SportFisherIso2byId(...), which is a
            // ReferenceError rather than a wrong answer — but only on the two hulls that use it.
            string g = hull.Extraction != null ? hull.Extraction.ScopeOr(hull.GlobalName) : hull.GlobalName;
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null")) return null;
            if (!host.EvaluateBool($"typeof {g}.navMounts === 'function'")) return null;   // absence is data

            string opts = hull.Extraction != null && !string.IsNullOrEmpty(hull.Extraction.ViewOptions)
                        ? hull.Extraction.ViewOptions : null;
            string arg = opts != null ? ", " + opts : "";

            double px = host.EvaluateNumber(g + ".PX");
            double cx = host.EvaluateNumber(g + ".pivot.x");
            double cy = host.EvaluateNumber(g + ".pivot.y");
            double elev = host.EvaluateNumber(g + ".defaultElev");
            int dirs = (int)host.EvaluateNumber(g + ".DIRS");

            var m = new HullLamps { Key = hull.Key, MeshAssetPath = hull.MeshAssetPath };

            // ---- the four (or five) the rig projects -------------------------------------------------
            Vector3 port = default, star = default, mast = default;
            bool hasMast = false;
            foreach (var (rigName, kind) in NavStations)
            {
                if (!host.EvaluateBool($"{g}.navMounts(0{arg}).{rigName} != null")) continue;
                Vector3 p = Invert(host, g, arg, rigName, dirs, elev, px, cx, cy, out double residual);
                m.Stations.Add(new Station(kind, p, residual, "navMounts()." + rigName));
                if (kind == HullLampKind.PortSidelight) port = p;
                if (kind == HullLampKind.StarboardSidelight) star = p;
                if (kind == HullLampKind.Masthead) { mast = p; hasMast = true; }
            }
            m.SidelightSeparationMetres = Mathf.Abs(star.x - port.x);

            // ---- the two the rig publishes no anchor for, placed against her published HOUSE ---------
            HouseBox house = ReadHouse(host, g, opts);

            if (house.HasGlowRoom)
                m.Stations.Add(new Station(HullLampKind.CabinGlow,
                    new Vector3(0f, house.GlowY, house.GlowZ), 0,
                    "HOUSE " + house.GlowRoom + " box centre, at her glass band"));

            // ---- the anchor light: at the masthead, because that is the highest point she declares ---
            if (hasMast)
                m.Stations.Add(new Station(HullLampKind.AnchorLight, mast, 0, "navMounts().mast (hoisted there)"));

            // ---- the searchlight, where the shipped beam would actually reach the sea ----------------
            m.StemY = StemStation(host, g, opts);
            if (double.IsNaN(m.StemY))
                m.SearchlightRefusal = "her stem station could not be read from her rig, so whether a " +
                                       "beam clears her bow cannot be measured — no mount is declared " +
                                       "on a guess.";

            if (house.HasConnedRoof)
            {
                float mountY = house.ConnedRoofFrontY - SearchlightBracketAftMetres;
                m.BeamClearanceMetres = mountY + ShippedBeamRangeMetres - m.StemY;
                if (ClearsHerOwnStem(m.BeamClearanceMetres))
                    m.Stations.Add(new Station(HullLampKind.Spotlight,
                        new Vector3(0f, mountY, house.ConnedRoofZ + SearchlightBracketUpMetres), 0,
                        "front of the " + house.ConnedRoom + " roof"));
                else
                    m.SearchlightRefusal = string.Format(CultureInfo.InvariantCulture,
                        "she is conned from y {0:F2} and her stem is at y {1:F2}, so the shipped {2:F1} m " +
                        "beam ends {3:F2} m SHORT of her own bow — it would rake her deck, not the sea. " +
                        "A per-hull throw would unblock her; that is a preset change, not a measurement.",
                        mountY, m.StemY, ShippedBeamRangeMetres, -m.BeamClearanceMetres);
            }

            return m;
        }

        /// <summary>
        /// Her STEM, in rig metres — how far forward her bow actually is.
        ///
        /// <para><b>Three families publish it in three places, and none of them is the global.</b> The
        /// singles hang <c>station</c> off <c>loft</c>; the lobster generator answers per variant
        /// through <c>loftOf(v)</c>/<c>resolve(v)</c>; the sport fisher registry puts it on the hull
        /// object itself. Asking the wrong one does not throw — it yields <c>undefined</c>, which
        /// becomes NaN, which compares false against every threshold. That is exactly how a silent
        /// "no searchlight" would get shipped, so the candidates are tried in order and a total
        /// failure is REFUSED loudly rather than defaulted.</para>
        ///
        /// <para>LOA halved is the last resort and is only sound because these hulls are modelled about
        /// amidships (the cape's stern light sits at y -6.35 against an LOA of 12.8). It is a fallback,
        /// not the measurement.</para>
        /// </summary>
        static double StemStation(IRigScriptHost host, string g, string opts)
        {
            string o = opts ?? "";
            foreach (string expr in new[]
                     {
                         $"{g}.loft.station(1).y",
                         $"{g}.station(1).y",
                         $"{g}.loftOf({o}).station(1).y",
                         $"{g}.resolve({o}).station(1).y",
                     })
            {
                double v = TryNumber(host, expr);
                if (!double.IsNaN(v)) return v;
            }
            foreach (string expr in new[] { $"{g}.loft.L", $"{g}.L", $"{g}.resolve({o}).L" })
            {
                double v = TryNumber(host, expr);
                if (!double.IsNaN(v)) return v * 0.5;
            }
            return double.NaN;
        }

        /// <summary>A number, or NaN when the rig does not publish it there. JavaScript answers a
        /// missing member with <c>undefined</c>, and <c>undefined</c> arithmetic is NaN rather than an
        /// error, so the miss has to be detected rather than caught.</summary>
        static double TryNumber(IRigScriptHost host, string expr)
        {
            try
            {
                if (!host.EvaluateBool($"(function(){{ try {{ var v = {expr}; return typeof v === 'number' && isFinite(v); }} catch (e) {{ return false; }} }})()"))
                    return double.NaN;
                return host.EvaluateNumber(expr);
            }
            catch { return double.NaN; }
        }

        /// <summary>Does the shipped beam get past her own bow? The one physical question behind which
        /// hulls carry a searchlight — measured, not a size class. The fleet separates cleanly: every
        /// hull that clears does so by 2.3 m or more, and every hull that does not falls short by
        /// 0.9 m or more, so nothing sits on this line.</summary>
        public static bool ClearsHerOwnStem(double clearanceMetres) => clearanceMetres > 0;

        static readonly (string RigName, HullLampKind Kind)[] NavStations =
        {
            ("port",  HullLampKind.PortSidelight),
            ("star",  HullLampKind.StarboardSidelight),
            ("stern", HullLampKind.SternLight),
            ("mast",  HullLampKind.Masthead),
            ("range", HullLampKind.RangeLight),
        };

        // -------------------------------------------------------------------------------------------
        //  the HOUSE, in its three published shapes
        // -------------------------------------------------------------------------------------------

        struct HouseBox
        {
            public bool HasGlowRoom, HasConnedRoof;
            public float GlowY, GlowZ;
            public float ConnedRoofFrontY, ConnedRoofZ;
            public string GlowRoom, ConnedRoom;
        }

        /// <summary>
        /// Read the room a lamp has to respect. Three families publish three shapes and they are read
        /// as they are written, never guessed at:
        /// <list type="bullet">
        /// <item><b>wheelhouse</b> (cape, lobster boat) — a flat record: yAft/yFwd, soleZ/eaveZ/roofZ,
        /// sideGlass. The eighteen lobster variants hand back the same shape from
        /// <c>houseOf(v)</c>, per variant, with an eave and no roof.</item>
        /// <item><b>ship</b> (dragger, both trawlers, packet, tanker) — <c>decks.house</c> is the
        /// accommodation (the portholes that glow) and <c>decks.bridge</c> is where she is conned.</item>
        /// <item><b>sport</b> (both sport fishers) — <c>decks.house</c> is the salon; the convertible's
        /// bridge is an OPEN flybridge (<c>external:true</c>, no ceiling), so her searchlight goes on
        /// the salon roof instead, which is what the tower straddles.</item>
        /// </list>
        /// </summary>
        static HouseBox ReadHouse(IRigScriptHost host, string g, string opts)
        {
            var h = new HouseBox();
            string hs = null;
            if (host.EvaluateBool($"typeof {g}.HOUSE === 'object' && {g}.HOUSE !== null")) hs = g + ".HOUSE";
            else if (host.EvaluateBool($"typeof {g}.houseOf === 'function'")) hs = $"{g}.houseOf({opts ?? ""})";
            if (hs == null) return h;

            bool hasDecks = host.EvaluateBool($"{hs}.decks != null");
            if (!hasDecks)
            {
                // the flat wheelhouse record
                float yAft = (float)host.EvaluateNumber(hs + ".yAft");
                float yFwd = (float)host.EvaluateNumber(hs + ".yFwd");
                h.HasGlowRoom = true;
                h.GlowRoom = "wheelhouse";
                h.GlowY = 0.5f * (yAft + yFwd);
                h.GlowZ = 0.5f * ((float)host.EvaluateNumber(hs + ".sideGlass.z0")
                                + (float)host.EvaluateNumber(hs + ".sideGlass.z1"));
                h.HasConnedRoof = true;
                h.ConnedRoom = "wheelhouse";
                h.ConnedRoofFrontY = yFwd;
                // roofZ is the crown and eaveZ the front edge; the variants publish only the eave.
                h.ConnedRoofZ = host.EvaluateBool($"{hs}.roofZ != null")
                              ? (float)host.EvaluateNumber(hs + ".roofZ")
                              : (float)host.EvaluateNumber(hs + ".eaveZ");
                return h;
            }

            string acc = hs + ".decks.house";
            string glass = host.EvaluateBool($"{acc}.portholes != null") ? ".portholes" : ".sideGlass";
            if (host.EvaluateBool($"{acc} != null && {acc}{glass} != null"))
            {
                h.HasGlowRoom = true;
                h.GlowRoom = "accommodation";
                h.GlowY = 0.5f * ((float)host.EvaluateNumber(acc + ".y0") + (float)host.EvaluateNumber(acc + ".y1"));
                h.GlowZ = 0.5f * ((float)host.EvaluateNumber(acc + glass + ".z0")
                                + (float)host.EvaluateNumber(acc + glass + ".z1"));
            }

            string br = hs + ".decks.bridge";
            bool enclosedBridge = host.EvaluateBool($"{br} != null && {br}.ceilZ != null && {br}.front != null");
            if (enclosedBridge)
            {
                h.HasConnedRoof = true;
                h.ConnedRoom = "bridge";
                h.ConnedRoofFrontY = (float)host.EvaluateNumber(br + ".front.yBot");
                h.ConnedRoofZ = (float)host.EvaluateNumber(br + ".ceilZ");
            }
            else if (host.EvaluateBool($"{acc}.ceilZ != null"))
            {
                // an OPEN flybridge has no roof of its own — the salon's is what carries her lamp.
                h.HasConnedRoof = true;
                h.ConnedRoom = "salon (open flybridge above)";
                h.ConnedRoofFrontY = (float)host.EvaluateNumber(acc + ".y1");
                h.ConnedRoofZ = (float)host.EvaluateNumber(acc + ".ceilZ");
            }
            return h;
        }

        // -------------------------------------------------------------------------------------------
        //  the inversion
        // -------------------------------------------------------------------------------------------

        /// <summary>
        /// Solve for the boat-local triple behind one published station, across every facing, and
        /// report the worst residual in cell pixels.
        /// </summary>
        static Vector3 Invert(IRigScriptHost host, string g, string arg, string rigName, int dirs,
                              double elev, double px, double cx, double cy, out double residual)
        {
            int rows = dirs * 2;
            var a = new double[rows, 3];
            var b = new double[rows];

            for (int d = 0; d < dirs; d++)
            {
                // The RUNTIME's own rig-to-world map for this facing — the same matrix the game poses
                // her lamps with, so the triple this yields is the triple the game will draw.
                Matrix4x4 m = IsoFacetMath.RigToWorld(d, elev);
                // cell pixels = pivot + (world.x, −world.y) * pxPerMetre; the y flip is the cell's own
                // top-left origin, the single convention this file asserts on its own behalf.
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 unit = m.MultiplyPoint3x4(axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward);
                    a[d * 2, axis] = unit.x * px;
                    a[d * 2 + 1, axis] = -unit.y * px;
                }
                b[d * 2] = host.EvaluateNumber($"{g}.navMounts({d}{arg}).{rigName}.x") - cx;
                b[d * 2 + 1] = host.EvaluateNumber($"{g}.navMounts({d}{arg}).{rigName}.y") - cy;
            }

            double[] sol = SolveLeastSquares(a, b, rows);

            residual = 0;
            for (int r = 0; r < rows; r++)
            {
                double predicted = a[r, 0] * sol[0] + a[r, 1] * sol[1] + a[r, 2] * sol[2];
                residual = Math.Max(residual, Math.Abs(predicted - b[r]));
            }
            return new Vector3((float)sol[0], (float)sol[1], (float)sol[2]);
        }

        /// <summary>Normal equations, then Gauss-Jordan on the 3x3. Small enough to be obvious, and
        /// obvious is what a measurement wants.</summary>
        static double[] SolveLeastSquares(double[,] a, double[] b, int rows)
        {
            var n = new double[3, 4];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double s = 0;
                    for (int r = 0; r < rows; r++) s += a[r, i] * a[r, j];
                    n[i, j] = s;
                }
                double t = 0;
                for (int r = 0; r < rows; r++) t += a[r, i] * b[r];
                n[i, 3] = t;
            }

            for (int c = 0; c < 3; c++)
            {
                int pivot = c;
                for (int r = c + 1; r < 3; r++) if (Math.Abs(n[r, c]) > Math.Abs(n[pivot, c])) pivot = r;
                if (pivot != c)
                    for (int j = 0; j < 4; j++) { double t = n[c, j]; n[c, j] = n[pivot, j]; n[pivot, j] = t; }
                double diag = n[c, c];
                for (int j = c; j < 4; j++) n[c, j] /= diag;
                for (int r = 0; r < 3; r++)
                {
                    if (r == c) continue;
                    double f = n[r, c];
                    for (int j = c; j < 4; j++) n[r, j] -= f * n[c, j];
                }
            }
            return new[] { n[0, 3], n[1, 3], n[2, 3] };
        }
    }
}
