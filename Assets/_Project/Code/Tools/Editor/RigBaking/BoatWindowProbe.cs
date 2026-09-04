using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Every window in the fleet, derived from each hull's own rig</b> (owner's ruling,
    /// 2026-09-03: a glow is confined to its space, and an interior's reaches the outside only
    /// THROUGH THE WINDOWS) — the measurement behind <see cref="HullMeshDef.Panes"/>, published as a
    /// table a reviewer can re-run rather than as 236 numbers in a commit nobody can check.
    ///
    /// <para><b>Why this one WRITES where <see cref="BoatLampAnchorProbe"/> only prints.</b> That
    /// probe deliberately refuses to author <c>Lamps</c>, and the reason is a good one: six rows per
    /// hull is hand-authorable, and hand-authoring keeps the door shut until the rig export contract
    /// grows a boat-local <c>NAV</c> table for the baker to read. Windows are not that. There are
    /// <b>236 panes across twenty-seven hulls</b>, each nine floats, and a human transcribing them
    /// would introduce errors nobody could see — a pane 20 cm off reads as a slightly odd glow, not
    /// as a defect. So this probe derives them and writes them, and the join test re-derives every
    /// one and goes red when a def has drifted from the rig it came from. The def stays GAME-SIDE
    /// (the mesh baker never touches <c>Panes</c>, so a re-bake cannot lose them); what changed is
    /// only who types the numbers.</para>
    ///
    /// <para><b>⚠️ TWO FAMILIES, AND `hxAt` MEANS DIFFERENT THINGS IN THEM.</b> The wheelhouse rigs
    /// (cape, lobster, the eighteen variants) publish a FLAT house whose <c>hxAt</c> takes <b>y</b>;
    /// the ship and sport rigs (dragger, both trawlers, packet, tanker, both sport fishers) publish
    /// <c>decks</c> whose <c>hxAt</c> takes <b>z</b>. Handing one the other's argument does not
    /// throw — it returns a plausible half-width for the wrong reason, and every window on that side
    /// of the boat lands somewhere near but not on her wall. The two are read down separate paths
    /// here and <see cref="BoatWindowPaneTests"/> cross-checks each against the flat <c>hx</c> the
    /// same record publishes, so a swap is caught by arithmetic rather than by eye.</para>
    ///
    /// <para><b>⚠️ AND `aftGlass` HAS TWO SCHEMAS UNDER ONE NAME.</b> A wheelhouse's is a single
    /// strip, <c>{x0,x1,z0,z1}</c>; a bridge's is a pane LIST, <c>{z0,z1,panes:[[x0,x1]…]}</c>.
    /// Reading either shape with the other's keys yields <c>undefined</c> → NaN, which silently
    /// becomes a pane at the origin. Both are decoded explicitly and an unrecognised shape is
    /// REFUSED with a message rather than defaulted.</para>
    ///
    /// <para><b>Which room is lit, and which is not.</b> This probe measures every glazed wall a rig
    /// publishes, and the report prints them all — but only the room the hull's existing
    /// <see cref="HullLampKind.CabinGlow"/> already lit is written into the def. For the wheelhouse
    /// family that is the wheelhouse; for the ship and sport family it is the ACCOMMODATION, whose
    /// windows are her portholes, because that is the room <c>BoatLampAnchorProbe.ReadHouse</c>
    /// places the cabin glow in. A ship's BRIDGE carries no glow today and gains none here: this
    /// lane confines an existing light, it does not light a new room. The bridge panes are measured
    /// and printed so that lighting her later is a data change and not another measurement.</para>
    /// </summary>
    public static class BoatWindowProbe
    {
        /// <summary>
        /// <b>A porthole is round and a pane is a rectangle, so the rectangle is the one INSCRIBED in
        /// it</b> — half-extent = radius / √2 — rather than the published band's full width.
        ///
        /// <para>Stated because the alternative is worse in both directions. The band a ship rig
        /// publishes (<c>portholes:{ys,z0,z1}</c>) is the IRON SURROUND, not the glass: on the stern
        /// trawler the published band is 0.66 m and the <c>glas</c> face inside it is 0.42 × 0.50 m.
        /// Taking the band whole would draw a lit square half again too big; re-deriving the rig's
        /// own inset would be transcribing private draw code, which is how a number goes stale in a
        /// file nobody thinks to look at. The inscribed square is neither: it is what a rectangle
        /// standing in for a disc IS, it needs nothing from the rig but the band, and it lands within
        /// two centimetres of the glass the rig actually draws (0.707 against a measured 0.64 × 0.76).
        /// The wheelhouse family needs no such rule — <b>there the published rectangle IS the
        /// glass</b>, and the trim is drawn outside it.</para>
        /// </summary>
        public static readonly float PortholeInscribed = 1f / Mathf.Sqrt(2f);

        /// <summary>
        /// <b>How far a pane's furthest corner may sit from the hull's own geometry before the probe
        /// refuses to place it</b>, metres.
        ///
        /// <para><b>Why a derivation from a published record needs checking against the mesh at all.</b>
        /// A rig's HOUSE record is a SUMMARY, and a summary can be coarser than the thing it
        /// summarises. Measured across the fleet, twenty-five hulls place every corner within 0.203 m
        /// of a real vertex — the rounded corners the rigs cut (0.05) plus the proud offset a glazed
        /// panel is drawn at (0.065) account for all of it. The two SPORT FISHERS do not: their
        /// accommodation publishes a FLAT <c>hx</c> for a side that CURVES IN PLAN (her portholes are
        /// drawn on <c>V.P(t,z)</c>, a profile the record does not carry), so a pane placed at ±hx
        /// floats 0.32–0.51 m outboard of her actual side — worst at her ends, least amidships, which
        /// is exactly the shape of the taper.</para>
        ///
        /// <para><b>So the probe refuses them rather than shipping a lit window hanging off the boat</b>,
        /// and those hulls fall back to the glow they have today. This is a general rule, not a list
        /// of names: any future rig whose record outruns its geometry is caught by the same
        /// arithmetic. The fix is upstream — a per-station half-width beside <c>hx</c>, the way the
        /// wheelhouse family's <c>hxAt(y)</c> already tapers — and until that lands those two keep
        /// their disc.</para>
        ///
        /// <para>0.30 m comes from the measurement: it clears the worst honest corner (0.203 m, the
        /// lobster boat) and refuses the best dishonest one (0.453 m), with room either side. The report prints every
        /// hull's worst, so this margin can be re-read rather than remembered.</para>
        /// </summary>
        public const float OnHullToleranceMetres = 0.30f;

        /// <summary>One hull's windows, as the probe derived them.</summary>
        public sealed class HullWindows
        {
            public string Key;
            public string MeshAssetPath;
            /// <summary>The room whose windows were WRITTEN — "wheelhouse" or "accommodation".</summary>
            public string LitRoom;
            /// <summary>The panes of <see cref="LitRoom"/>: what goes into the def.</summary>
            public readonly List<HullPane> Panes = new List<HullPane>();
            /// <summary>The panes of every OTHER glazed room (a ship's bridge), measured and printed
            /// but not written — see the class remarks.</summary>
            public readonly List<HullPane> UnlitPanes = new List<HullPane>();
            /// <summary>Null when she was measured; otherwise why she has no lit windows. An open
            /// boat's refusal is not a defect — it is the answer.</summary>
            public string Refusal;
            /// <summary>The furthest any surviving pane's corner sits from a vertex of her own hull
            /// mesh, metres — the number <see cref="OnHullToleranceMetres"/> is set from. NaN when her
            /// mesh could not be loaded, which is a "not measured", never a pass.</summary>
            public float WorstCornerMetres = float.NaN;
        }

        // -------------------------------------------------------------------------------------------
        //  the menu
        // -------------------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Rig Baking/Probe: boat windows (print the table)")]
        public static void PrintTable()
        {
            string report = Report(Measure());
            string path = Path.Combine(RigCatalog.RepoRoot, "artifacts", "boat-window-panes.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report);
            Debug.Log(report);
            Debug.Log("[boat-windows] table written to " + path);
        }

        [MenuItem("Hidden Harbours/Rig Baking/Write: boat windows into the hull defs")]
        public static void WriteIntoDefs()
        {
            List<HullWindows> all = Measure();
            int hulls = 0, panes = 0, missing = 0;
            var sb = new StringBuilder();

            foreach (HullWindows w in all)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(w.MeshAssetPath);
                if (def == null)
                {
                    missing++;
                    sb.AppendLine("  MISSING DEF  " + w.Key + "  (" + w.MeshAssetPath + ")");
                    continue;
                }

                HullPane[] wanted = w.Panes.ToArray();
                if (SamePanes(def.Panes, wanted)) continue;   // idempotent: an unchanged def is not dirtied

                Undo.RecordObject(def, "Write boat window panes");
                def.Panes = wanted;
                EditorUtility.SetDirty(def);
                hulls++;
                panes += wanted.Length;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0,-42} {1,3} panes  ({2})", w.Key, wanted.Length, w.LitRoom ?? "none"));
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[boat-windows] wrote {panes} panes into {hulls} hull defs " +
                      $"({all.Count - hulls} already current, {missing} defs missing).\n" + sb);
        }

        /// <summary>Element-wise equality, so a re-run that changes nothing writes nothing and the
        /// working tree stays clean (Unity rewrites a def it is merely told is dirty).</summary>
        static bool SamePanes(HullPane[] a, HullPane[] b)
        {
            if (a == null) return b == null || b.Length == 0;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].Wall != b[i].Wall) return false;
                if (a[i].CentreMetres != b[i].CentreMetres) return false;
                if (a[i].HalfAcrossMetres != b[i].HalfAcrossMetres) return false;
                if (a[i].HalfUpMetres != b[i].HalfUpMetres) return false;
            }
            return true;
        }

        // -------------------------------------------------------------------------------------------
        //  the report
        // -------------------------------------------------------------------------------------------

        public static string Report(List<HullWindows> all)
        {
            var sb = new StringBuilder();
            sb.AppendLine("BOAT WINDOWS — derived from each hull's own published HOUSE glazing.");
            sb.AppendLine("Rig metres: +x starboard, +y toward the bow, +z up from the keel.");
            sb.AppendLine("'outward' is up x across, computed from the pane's own two vectors — never declared.");
            sb.AppendLine();

            int litHulls = 0, litPanes = 0, unlitPanes = 0;
            float widest = 0f, narrowest = float.MaxValue;
            float worstKept = 0f, bestRefused = float.MaxValue;
            string worstKeptHull = "", bestRefusedHull = "";

            foreach (HullWindows w in all)
            {
                sb.AppendLine("── " + w.Key + "   (" + Path.GetFileName(w.MeshAssetPath) + ")");

                // ⚠️ Counted BEFORE the refusal check. A hull whose lit room was refused still had
                // her bridge measured, and a total that quietly stopped including it would drift from
                // what the tests count — two numbers for one fact, which is how a report starts lying.
                unlitPanes += w.UnlitPanes.Count;

                if (w.Refusal != null)
                {
                    sb.AppendLine("     NO LIT WINDOWS — " + w.Refusal);
                    if (!float.IsNaN(w.WorstCornerMetres) && w.WorstCornerMetres < bestRefused)
                    { bestRefused = w.WorstCornerMetres; bestRefusedHull = w.Key; }
                    if (w.UnlitPanes.Count > 0)
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "     ({0} bridge panes were still measured on her){1}",
                            w.UnlitPanes.Count, System.Environment.NewLine);
                    sb.AppendLine();
                    continue;
                }

                litHulls++;
                if (!float.IsNaN(w.WorstCornerMetres) && w.WorstCornerMetres > worstKept)
                { worstKept = w.WorstCornerMetres; worstKeptHull = w.Key; }
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "     lit room: {0}   ({1} panes; worst corner {2:F3} m off her own mesh)",
                    w.LitRoom, w.Panes.Count, w.WorstCornerMetres);
                sb.AppendLine();
                foreach (HullPane p in w.Panes)
                {
                    Vector3 n = p.Outward;
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "     {0,-10} centre ({1,8:F3},{2,8:F3},{3,8:F3})  {4,5:F2} x {5,5:F2} m  " +
                        "outward ({6,6:F3},{7,6:F3},{8,6:F3})",
                        p.Wall, p.CentreMetres.x, p.CentreMetres.y, p.CentreMetres.z,
                        p.WidthMetres, p.HeightMetres, n.x, n.y, n.z);
                    sb.AppendLine();
                    litPanes++;
                    widest = Mathf.Max(widest, p.WidthMetres);
                    narrowest = Mathf.Min(narrowest, p.WidthMetres);
                }

                if (w.UnlitPanes.Count > 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "     + {0} bridge panes measured but NOT written (this lane confines an " +
                        "existing glow; it lights no new room)", w.UnlitPanes.Count);
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            sb.AppendLine("── the numbers the presets are bounded by ──");
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "  {0} lit panes over {1} hulls (and {2} measured bridge panes left dark).",
                litPanes, litHulls, unlitPanes);
            sb.AppendLine();
            if (litPanes > 0)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "  pane widths run {0:F2} m to {1:F2} m — the wash is scaled off a WINDOW, so the " +
                    "two ends of that range must both read as one.",
                    narrowest, widest);
                sb.AppendLine();
            }
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "  ON-HULL MARGIN (the {0:F2} m tolerance): worst corner KEPT {1:F3} m ({2}); " +
                "best corner REFUSED {3:F3} m ({4}). Re-read these before moving the tolerance.",
                OnHullToleranceMetres, worstKept, worstKeptHull,
                bestRefused == float.MaxValue ? 0f : bestRefused,
                bestRefusedHull.Length > 0 ? bestRefusedHull : "none");
            sb.AppendLine();
            return sb.ToString();
        }

        // -------------------------------------------------------------------------------------------
        //  the measurement
        // -------------------------------------------------------------------------------------------

        /// <summary>Every hull in the fleet, measured. One V8 host per rig FILE, so the eighteen
        /// lobster variants share one load and a generator's module state cannot leak from one
        /// variant into the next (each call passes its own descriptor) — the same discipline
        /// <see cref="BoatLampAnchorProbe.Measure"/> keeps.</summary>
        public static List<HullWindows> Measure()
        {
            var results = new List<HullWindows>();
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

                foreach (FleetHull hull in pair.Value) results.Add(MeasureOne(host, hull));
            }
            return results;
        }

        static HullWindows MeasureOne(IRigScriptHost host, FleetHull hull)
        {
            var w = new HullWindows { Key = hull.Key, MeshAssetPath = hull.MeshAssetPath };

            string g = hull.Extraction != null ? hull.Extraction.ScopeOr(hull.GlobalName) : hull.GlobalName;
            string opts = hull.Extraction != null && !string.IsNullOrEmpty(hull.Extraction.ViewOptions)
                        ? hull.Extraction.ViewOptions : "";

            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
            {
                w.Refusal = "her rig publishes no global by that name.";
                return w;
            }

            // BIND THE HOUSE ONCE. houseOf() runs a generator, and reading forty fields off a fresh
            // call each time would run it forty times per hull — and, worse, would let a rig that
            // memoises differently answer two of those reads from two different objects.
            host.Execute(
                $"globalThis.__hhHouse = (typeof {g}.HOUSE === 'object' && {g}.HOUSE !== null) ? {g}.HOUSE" +
                $" : (typeof {g}.houseOf === 'function' ? {g}.houseOf({opts}) : null);");
            const string H = "globalThis.__hhHouse";

            if (!host.EvaluateBool($"{H} != null"))
            {
                w.Refusal = "she publishes no HOUSE — an open boat, with no room to light. " +
                            "Absence is data, not a defect.";
                return w;
            }

            if (host.EvaluateBool($"{H}.decks != null")) ReadShip(host, H, w);
            else ReadWheelhouse(host, H, w);

            if (w.Refusal == null && w.Panes.Count == 0)
                w.Refusal = "her " + (w.LitRoom ?? "house") + " publishes no glazing at all.";

            KeepOnlyWhatLandsOnHer(w);
            return w;
        }

        /// <summary>
        /// <b>Check every derived pane against the hull's own baked geometry, and drop the ones that
        /// do not land on her</b> (see <see cref="OnHullToleranceMetres"/> for the measurement and the
        /// one family this catches).
        ///
        /// <para><b>This VALIDATES; it does not derive.</b> The distinction is load-bearing. A pane is
        /// still read entirely from the rig's published record, so the join test's comparison of the
        /// def against the record is still two independent computations meeting; the mesh only ever
        /// says "this one is not on the boat" and never says where it should have gone. A probe that
        /// SNAPPED panes onto the mesh would have no independent oracle left at all.</para>
        ///
        /// <para><b>All or nothing per hull.</b> A hull with three of five portholes landing is not a
        /// hull with three portholes — it is a hull whose record is wrong about her side, and lighting
        /// the three would leave a lit room with holes in it and no sign that anything was missing. So
        /// the whole room is refused and she keeps the glow she has today.</para>
        /// </summary>
        static void KeepOnlyWhatLandsOnHer(HullWindows w)
        {
            if (w.Panes.Count == 0) return;

            var mesh = AssetDatabase.LoadAssetAtPath<HullMeshDef>(w.MeshAssetPath);
            if (mesh == null || mesh.Mesh == null) return;   // not measured; never a silent pass

            Vector3[] verts = mesh.Mesh.vertices;
            if (verts.Length == 0) return;

            float worst = 0f;
            HullPane worstPane = default;
            foreach (HullPane p in w.Panes)
                for (int ax = -1; ax <= 1; ax += 2)
                    for (int up = -1; up <= 1; up += 2)
                    {
                        float d = NearestVertexMetres(verts, p.Corner(ax, up));
                        if (d > worst) { worst = d; worstPane = p; }
                    }

            w.WorstCornerMetres = worst;
            if (worst <= OnHullToleranceMetres) return;

            int dropped = w.Panes.Count;
            w.Panes.Clear();
            w.Refusal = string.Format(CultureInfo.InvariantCulture,
                "her rig's published HOUSE record does not describe the side her glass is actually " +
                "drawn on: {0} panes derived from it, and the worst corner ({1} at {2}) lands " +
                "{3:F3} m from any vertex of her own hull — past the {4:F2} m these rigs' rounded " +
                "corners and proud offsets account for. A window hanging off the boat is worse than " +
                "no window, so her room keeps the glow it has today. UPSTREAM ASK: publish a " +
                "per-station half-width beside hx, the way the wheelhouse family's hxAt(y) already " +
                "tapers.",
                dropped, worstPane.Wall, worstPane.CentreMetres, worst, OnHullToleranceMetres);
        }

        static float NearestVertexMetres(Vector3[] verts, Vector3 at)
        {
            float best = float.MaxValue;
            for (int i = 0; i < verts.Length; i++)
            {
                float d = (verts[i] - at).sqrMagnitude;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        // -------------------------------------------------------------------------------------------
        //  the WHEELHOUSE family — cape, lobster boat, and the eighteen variants
        // -------------------------------------------------------------------------------------------

        /// <summary>
        /// A flat wheelhouse record: a raked FRONT with a pane list, side lights on both sides, and
        /// one aft strip. Every rectangle published here IS the glass — the rigs draw the dark trim
        /// OUTSIDE it — so nothing is inset.
        /// </summary>
        static void ReadWheelhouse(IRigScriptHost host, string H, HullWindows w)
        {
            w.LitRoom = "wheelhouse";

            float eaveZ = (float)host.EvaluateNumber($"{H}.eaveZ");
            float yAft = (float)host.EvaluateNumber($"{H}.yAft");

            // ---- the raked windscreen ----------------------------------------------------------
            if (host.EvaluateBool($"{H}.front != null && {H}.front.glass != null"))
            {
                float yBot = (float)host.EvaluateNumber($"{H}.front.yBot");
                float yTop = (float)host.EvaluateNumber($"{H}.front.yTop");
                float zBot = (float)host.EvaluateNumber($"{H}.front.zBot");
                float z0 = (float)host.EvaluateNumber($"{H}.front.glass.z0");
                float z1 = (float)host.EvaluateNumber($"{H}.front.glass.z1");

                // The rake, as the rigs themselves compute it: the in-plane UP is (0, dy, dz) and the
                // outward normal is its 90-degree turn, (0, dz, -dy). Transcribed from nothing — it
                // is the only vector perpendicular to the wall that points away from the room, and
                // HullPane derives it from these two rather than taking it on trust.
                float dy = yTop - yBot, dz = eaveZ - zBot;
                if (Mathf.Abs(dz) < 1e-4f)
                {
                    w.Refusal = "her wheelhouse front spans no height (eaveZ == front.zBot), so the " +
                                "rake has no direction and a pane on it would face nowhere.";
                    return;
                }
                float rake = Mathf.Sqrt(dy * dy + dz * dz);          // the wall's own length per unit z-span
                Vector3 up = new Vector3(0f, dy, dz) / rake;

                int n = (int)host.EvaluateNumber($"{H}.front.glass.panes.length");
                float zc = 0.5f * (z0 + z1);
                float yc = yBot + dy * (zc - zBot) / dz;
                float halfH = 0.5f * (z1 - z0) * rake / dz;          // height measured UP THE RAKE, not vertically
                for (int i = 0; i < n; i++)
                {
                    float x0 = (float)host.EvaluateNumber($"{H}.front.glass.panes[{i}][0]");
                    float x1 = (float)host.EvaluateNumber($"{H}.front.glass.panes[{i}][1]");
                    w.Panes.Add(new HullPane(HullWall.Front,
                        new Vector3(0.5f * (x0 + x1), yc, zc),
                        new Vector3(0.5f * (x1 - x0), 0f, 0f),       // +x across: outward = up x across = +y-ish
                        up * halfH));
                }
            }

            // ---- the side lights, both sides ---------------------------------------------------
            if (host.EvaluateBool($"{H}.sideGlass != null"))
            {
                float z0 = (float)host.EvaluateNumber($"{H}.sideGlass.z0");
                float z1 = (float)host.EvaluateNumber($"{H}.sideGlass.z1");
                int n = (int)host.EvaluateNumber($"{H}.sideGlass.runs.length");
                for (int i = 0; i < n; i++)
                {
                    float y0 = (float)host.EvaluateNumber($"{H}.sideGlass.runs[{i}][0]");
                    float y1 = (float)host.EvaluateNumber($"{H}.sideGlass.runs[{i}][1]");
                    float yc = 0.5f * (y0 + y1);
                    // ⚠️ hxAt takes Y on this family (it takes Z on the ship family — see the class
                    // remarks). Handing it the wrong one returns a plausible number for the wrong
                    // reason and puts every side window slightly off her wall.
                    float hx = HalfWidth(host, H, "y", yc);
                    AddSidePanes(w, yc, 0.5f * (y1 - y0), 0.5f * (z0 + z1), 0.5f * (z1 - z0), hx);
                }
            }

            // ---- the aft light: ONE strip, {x0,x1,z0,z1} ---------------------------------------
            if (host.EvaluateBool($"{H}.aftGlass != null"))
            {
                if (!host.EvaluateBool($"{H}.aftGlass.x0 != null"))
                {
                    w.Refusal = "her aftGlass is not the wheelhouse family's {x0,x1,z0,z1} strip. " +
                                "Two schemas ship under that one name and this one was not decoded " +
                                "rather than being read with the wrong keys.";
                    return;
                }
                float x0 = (float)host.EvaluateNumber($"{H}.aftGlass.x0");
                float x1 = (float)host.EvaluateNumber($"{H}.aftGlass.x1");
                float z0 = (float)host.EvaluateNumber($"{H}.aftGlass.z0");
                float z1 = (float)host.EvaluateNumber($"{H}.aftGlass.z1");
                AddAftPane(w, 0.5f * (x0 + x1), yAft, 0.5f * (z0 + z1), 0.5f * (x1 - x0), 0.5f * (z1 - z0));
            }
        }

        // -------------------------------------------------------------------------------------------
        //  the SHIP and SPORT family — dragger, both trawlers, packet, tanker, both sport fishers
        // -------------------------------------------------------------------------------------------

        /// <summary>
        /// A decked house record. The room the cabin glow lights here is <c>decks.house</c>, the
        /// ACCOMMODATION, whose windows are her portholes; her BRIDGE is measured into
        /// <see cref="HullWindows.UnlitPanes"/> and left dark (see the class remarks).
        /// </summary>
        static void ReadShip(IRigScriptHost host, string H, HullWindows w)
        {
            w.LitRoom = "accommodation";
            string acc = H + ".decks.house";

            if (!host.EvaluateBool($"{acc} != null"))
            {
                w.Refusal = "she declares decks but no accommodation among them, so the room her " +
                            "cabin glow lights cannot be identified.";
                return;
            }

            float y0 = (float)host.EvaluateNumber($"{acc}.y0");

            // ---- her side portholes ------------------------------------------------------------
            if (host.EvaluateBool($"{acc}.portholes != null"))
            {
                float pz0 = (float)host.EvaluateNumber($"{acc}.portholes.z0");
                float pz1 = (float)host.EvaluateNumber($"{acc}.portholes.z1");
                float zc = 0.5f * (pz0 + pz1);
                float r = 0.5f * (pz1 - pz0) * PortholeInscribed;   // the square inscribed in the disc
                // ⚠️ hxAt takes Z on this family. See the class remarks.
                float hx = HalfWidth(host, acc, "z", zc);
                int n = (int)host.EvaluateNumber($"{acc}.portholes.ys.length");
                for (int i = 0; i < n; i++)
                {
                    float y = (float)host.EvaluateNumber($"{acc}.portholes.ys[{i}]");
                    AddSidePanes(w, y, r, zc, r, hx);
                }
            }

            // ---- her aft-wall portholes, where she has them -------------------------------------
            if (host.EvaluateBool($"{acc}.aftPorts != null"))
            {
                float pz0 = (float)host.EvaluateNumber($"{acc}.aftPorts.z0");
                float pz1 = (float)host.EvaluateNumber($"{acc}.aftPorts.z1");
                float r = 0.5f * (pz1 - pz0) * PortholeInscribed;
                int n = (int)host.EvaluateNumber($"{acc}.aftPorts.xs.length");
                for (int i = 0; i < n; i++)
                {
                    float x = (float)host.EvaluateNumber($"{acc}.aftPorts.xs[{i}]");
                    AddAftPane(w, x, y0, 0.5f * (pz0 + pz1), r, r);
                }
            }

            ReadBridge(host, H, w);
        }

        /// <summary>
        /// The bridge's glazing — a raked front pane list, side runs and an aft pane list. Measured
        /// into <see cref="HullWindows.UnlitPanes"/> only: today no hull carries a glow up here, and
        /// giving her one would be lighting a new room rather than confining an old one. Printed so
        /// that the owner's "and the wheelhouse too" is a data change, not another measurement.
        /// </summary>
        static void ReadBridge(IRigScriptHost host, string H, HullWindows w)
        {
            string br = H + ".decks.bridge";
            if (!host.EvaluateBool($"{br} != null && {br}.ceilZ != null && {br}.front != null")) return;

            float soleZ = (float)host.EvaluateNumber($"{br}.soleZ");
            float ceilZ = (float)host.EvaluateNumber($"{br}.ceilZ");
            float yBot = (float)host.EvaluateNumber($"{br}.front.yBot");
            float yTop = (float)host.EvaluateNumber($"{br}.front.yTop");
            float y0 = (float)host.EvaluateNumber($"{br}.y0");
            float dy = yTop - yBot, dz = ceilZ - soleZ;
            if (Mathf.Abs(dz) < 1e-4f) return;
            float rake = Mathf.Sqrt(dy * dy + dz * dz);
            Vector3 up = new Vector3(0f, dy, dz) / rake;

            var into = w.UnlitPanes;

            if (host.EvaluateBool($"{br}.frontGlass != null"))
            {
                float z0 = (float)host.EvaluateNumber($"{br}.frontGlass.z0");
                float z1 = (float)host.EvaluateNumber($"{br}.frontGlass.z1");
                float zc = 0.5f * (z0 + z1);
                float yc = yBot + dy * (zc - soleZ) / dz;
                float halfH = 0.5f * (z1 - z0) * rake / dz;
                int n = (int)host.EvaluateNumber($"{br}.frontGlass.panes.length");
                for (int i = 0; i < n; i++)
                {
                    float x0 = (float)host.EvaluateNumber($"{br}.frontGlass.panes[{i}][0]");
                    float x1 = (float)host.EvaluateNumber($"{br}.frontGlass.panes[{i}][1]");
                    into.Add(new HullPane(HullWall.Front,
                        new Vector3(0.5f * (x0 + x1), yc, zc),
                        new Vector3(0.5f * (x1 - x0), 0f, 0f), up * halfH));
                }
            }

            if (host.EvaluateBool($"{br}.sideGlass != null"))
            {
                float z0 = (float)host.EvaluateNumber($"{br}.sideGlass.z0");
                float z1 = (float)host.EvaluateNumber($"{br}.sideGlass.z1");
                float hx = HalfWidth(host, br, "z", 0.5f * (z0 + z1));
                int n = (int)host.EvaluateNumber($"{br}.sideGlass.runs.length");
                for (int i = 0; i < n; i++)
                {
                    float ya = (float)host.EvaluateNumber($"{br}.sideGlass.runs[{i}][0]");
                    float yb = (float)host.EvaluateNumber($"{br}.sideGlass.runs[{i}][1]");
                    AddSidePanes(into, 0.5f * (ya + yb), 0.5f * (yb - ya),
                                 0.5f * (z0 + z1), 0.5f * (z1 - z0), hx);
                }
            }

            // ⚠️ The bridge's aftGlass is the PANE-LIST schema, not the wheelhouse's strip.
            if (host.EvaluateBool($"{br}.aftGlass != null && {br}.aftGlass.panes != null"))
            {
                float z0 = (float)host.EvaluateNumber($"{br}.aftGlass.z0");
                float z1 = (float)host.EvaluateNumber($"{br}.aftGlass.z1");
                int n = (int)host.EvaluateNumber($"{br}.aftGlass.panes.length");
                for (int i = 0; i < n; i++)
                {
                    float x0 = (float)host.EvaluateNumber($"{br}.aftGlass.panes[{i}][0]");
                    float x1 = (float)host.EvaluateNumber($"{br}.aftGlass.panes[{i}][1]");
                    AddAftPane(into, 0.5f * (x0 + x1), y0, 0.5f * (z0 + z1),
                               0.5f * (x1 - x0), 0.5f * (z1 - z0));
                }
            }
        }

        // -------------------------------------------------------------------------------------------
        //  the two shapes every family shares
        // -------------------------------------------------------------------------------------------

        static void AddSidePanes(HullWindows w, float yc, float halfLen, float zc, float halfH, float hx) =>
            AddSidePanes(w.Panes, yc, halfLen, zc, halfH, hx);

        /// <summary>
        /// One window run, on BOTH sides. The handedness is the whole point: seen from outside, the
        /// starboard wall runs the opposite way along y from the port wall, so its ACROSS vector is
        /// negated — and that is what makes <see cref="HullPane.Outward"/> come out +x to starboard
        /// and −x to port instead of both the same way.
        /// </summary>
        static void AddSidePanes(List<HullPane> into, float yc, float halfLen, float zc, float halfH, float hx)
        {
            Vector3 up = new Vector3(0f, 0f, halfH);
            into.Add(new HullPane(HullWall.Starboard, new Vector3(hx, yc, zc),
                                  new Vector3(0f, -halfLen, 0f), up));
            into.Add(new HullPane(HullWall.Port, new Vector3(-hx, yc, zc),
                                  new Vector3(0f, halfLen, 0f), up));
        }

        static void AddAftPane(HullWindows w, float xc, float y, float zc, float halfW, float halfH) =>
            AddAftPane(w.Panes, xc, y, zc, halfW, halfH);

        /// <summary>One window in an aft wall. ACROSS is −x for the same reason: from astern, her
        /// starboard side is on your left.</summary>
        static void AddAftPane(List<HullPane> into, float xc, float y, float zc, float halfW, float halfH) =>
            into.Add(new HullPane(HullWall.Aft, new Vector3(xc, y, zc),
                                  new Vector3(-halfW, 0f, 0f), new Vector3(0f, 0f, halfH)));

        /// <summary>
        /// The room's half-width where a window sits — through the record's own <c>hxAt</c> when it
        /// has one (the lobster's house TAPERS, 1.50 m aft to 1.08 m forward, so a constant would put
        /// every forward window 40 cm outside her wall), and the flat <c>hx</c>/<c>hxAft</c> when it
        /// does not.
        ///
        /// <para><paramref name="axis"/> is "y" or "z" and is the whole trap this method exists to
        /// contain: see the class remarks.</para>
        /// </summary>
        static float HalfWidth(IRigScriptHost host, string record, string axis, float at)
        {
            string a = at.ToString("R", CultureInfo.InvariantCulture);
            if (host.EvaluateBool($"typeof {record}.hxAt === 'function'"))
            {
                double v = host.EvaluateNumber($"{record}.hxAt({a})");
                if (!double.IsNaN(v) && v > 0) return (float)v;
            }
            foreach (string key in new[] { "hx", "hxAft", "hxFwd" })
                if (host.EvaluateBool($"{record}.{key} != null"))
                    return (float)host.EvaluateNumber($"{record}.{key}");

            // Nothing to stand on. Zero puts the pane on the centreline, where it is visibly wrong
            // rather than subtly wrong — a window inside the boat is a bug report; a window 20 cm
            // off her wall is a shrug. The axis argument is named in the log so a swap is findable.
            Debug.LogWarning($"[boat-windows] {record} publishes neither hxAt({axis}) nor hx — " +
                             "her side windows have no wall to sit on.");
            return 0f;
        }
    }
}
