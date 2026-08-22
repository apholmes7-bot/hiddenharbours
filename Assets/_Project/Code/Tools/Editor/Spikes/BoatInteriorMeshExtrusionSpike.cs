using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.Editor.Spikes
{
    /// <summary>
    /// <b>SPIKE (spike/interior-mesh) — questions A and B, in numbers.</b>
    ///
    /// <para><b>A. Is the plan extrudable?</b> Take each hull's committed
    /// <see cref="BoatInteriorDef"/> — the LAYOUT, already resolved against that hull's own loft by
    /// the interior rig and already inset by the rig's WT — and emit the room's shell in the hull's
    /// own mesh space: sole, walls, ceiling, door aperture. Report the vertex and triangle cost per
    /// hull per level, and test every emitted vertex for containment inside the hull's existing
    /// shell.</para>
    ///
    /// <para><b>B. Does one tag serve both?</b> Write the level id into TexCoord1.x on the interior
    /// faces AND on the hull's own house faces, and report how well those house faces can be
    /// IDENTIFIED from LAYOUT heights alone — which is the question "are the 53 level subsets
    /// across 24 hulls derivable, or do they need the art director's hand".
    /// ⚠️ The count in the handoff is 53. The committed defs carry <b>59</b> — see FLEET SHAPE in
    /// the report, which prints the tally per level id rather than restating the number.</para>
    ///
    /// <para><b>It writes no asset and re-bakes nothing.</b> Committed data in, a text report out.
    /// The renders and the shader proof live in the EditMode fixture
    /// <c>BoatInteriorMeshSpikeRenderTests</c>, because they need a GPU.</para>
    ///
    /// <para>Run: <b>Hidden Harbours ▸ Dev ▸ Spikes ▸ Boat Interior — Extrude To Mesh</b>, or
    /// headless with <c>-executeMethod HiddenHarbours.Tools.Editor.Spikes.BoatInteriorMeshExtrusionSpike.Cli</c>
    /// (<c>-spikeOut &lt;path&gt;</c> to redirect the report).</para>
    /// </summary>
    public static class BoatInteriorMeshExtrusionSpike
    {
        const string InteriorFolder = "Assets/_Project/Data/Boats/Interiors";
        const string VisualFolder = "Assets/_Project/Data/Boats/Visuals";
        const string DefaultOut = "boat-interior-extrusion-spike.txt";

        /// <summary>Smallest/middle/worst case, as the handoff names them. Asset stems.</summary>
        public static readonly string[] TheThree = { "LobsterBoatIso", "SternTrawlerIso", "CoastalPacketIso" };

        /// <summary>Containment tolerance, metres. A hair over the float32 quantisation the mesh
        /// buffer imposes on double-precision rig coordinates, so a vertex sitting exactly ON the
        /// shell is not counted as a violation.</summary>
        public const float ContainmentEpsilonMeters = 0.001f;

        /// <summary>Half-height of the z band a per-level SECTION hull is cut from, metres. Wide
        /// enough that a hull with coarse vertical parameterisation still contributes points at any
        /// query height, narrow enough that the section is the hull HERE and not her whole sheer.</summary>
        public const float SectionBandMeters = 0.35f;

        [MenuItem("Hidden Harbours/Dev/Spikes/Boat Interior — Extrude To Mesh")]
        public static void Run()
        {
            string report = Measure();
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DefaultOut);
            File.WriteAllText(path, report);
            Debug.Log("[BoatInteriorMeshExtrusionSpike] wrote " + path + "\n" + report);
        }

        public static void Cli()
        {
            string outPath = null;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], "-spikeOut", StringComparison.Ordinal)) outPath = args[i + 1];
            try
            {
                string report = Measure();
                if (string.IsNullOrEmpty(outPath))
                    outPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DefaultOut);
                File.WriteAllText(outPath, report);
                Debug.Log("[BoatInteriorMeshExtrusionSpike] OK -> " + outPath + "\n" + report);
            }
            catch (Exception e)
            {
                Debug.LogError("[BoatInteriorMeshExtrusionSpike] FAILED: " + e);
            }
        }

        // ------------------------------------------------------------------ the report

        public static string Measure()
        {
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            sb.AppendLine("BOAT INTERIORS AS MESH — QUESTIONS A AND B, MEASURED (spike/interior-mesh)");
            sb.AppendLine("Committed defs in, numbers out. No asset written, nothing re-baked.");
            sb.AppendLine();

            // ---------- the fleet-wide shape of the problem (question B's second half) ----------
            string[] allGuids = AssetDatabase.FindAssets("t:BoatInteriorDef", new[] { InteriorFolder });
            Array.Sort(allGuids);
            int hullCount = 0, levelCount = 0, meshBacked = 0;
            var levelIdTally = new Dictionary<string, int>();
            foreach (string g in allGuids)
            {
                var d = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(AssetDatabase.GUIDToAssetPath(g));
                if (d == null) continue;
                hullCount++;
                if (d.Levels != null)
                    foreach (BoatInteriorLevel l in d.Levels)
                    {
                        if (l == null) continue;
                        levelCount++;
                        int c;
                        levelIdTally[l.Id] = levelIdTally.TryGetValue(l.Id, out c) ? c + 1 : 1;
                    }
                var v = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                    VisualFolder + "/" + Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) + ".asset");
                if (v != null && v.Variant == BoatHullVariant.Mesh && v.HullMesh != null && v.HullMesh.Mesh != null)
                    meshBacked++;
            }
            sb.AppendLine("FLEET SHAPE");
            sb.AppendLine("   interior-bearing hulls        : " + hullCount);
            sb.AppendLine("   levels across them            : " + levelCount);
            sb.AppendLine("   of those hulls, MESH-backed   : " + meshBacked);
            var ids = new List<string>(levelIdTally.Keys); ids.Sort();
            var bits = new List<string>();
            foreach (string id in ids) bits.Add(id + " x" + levelIdTally[id]);
            sb.AppendLine("   level ids in use              : " + string.Join(", ", bits));
            sb.AppendLine();

            // ---------- the three hulls, in detail ----------
            foreach (string stem in TheThree)
            {
                sb.AppendLine(new string('=', 78));
                Hull(sb, inv, stem);
                sb.AppendLine();
            }

            // ---------- B, fleet-wide: can the house faces be found from LAYOUT heights? --------
            sb.AppendLine(new string('=', 78));
            sb.AppendLine("B (FLEET) — CAN A LEVEL'S OWN HULL FACES BE FOUND FROM LAYOUT HEIGHTS ALONE?");
            sb.AppendLine("   Rule under test, and it uses NOTHING the classifier already rejected (no");
            sb.AppendLine("   material name, no normal direction, no face bias, no inset threshold):");
            sb.AppendLine("     a hull face belongs to the HIGHEST level whose sole is at or below the");
            sb.AppendLine("     face's own lowest point, among the levels whose outline contains its");
            sb.AppendLine("     centroid in xy — i.e. everything STANDING ON a level comes off with it.");
            sb.AppendLine("     (The first rule tried was 'the whole face lies between sole and ceiling'.");
            sb.AppendLine("     A deckhouse WALL fails that — its top is the roof — so the wall stayed");
            sb.AppendLine("     drawn and the room stayed invisible at 3.1% of its own pixels.)");
            sb.AppendLine("   AMBIGUOUS = the face STRADDLES THE SOLE PLANE (bottom below, top above) — the deck");
            sb.AppendLine("   plate itself, which belongs to the level and to the hull at once. That count is");
            sb.AppendLine("   what decides whether this is derivable or needs the art director's hand.");
            sb.AppendLine();
            sb.AppendLine(string.Format("   {0,-40} {1,7} {2,8} {3,8} {4,9} {5,8}",
                                        "hull", "faces", "tagged", "ambig", "ambig%", "levels"));
            int fleetFaces = 0, fleetTagged = 0, fleetAmbig = 0, fleetHulls = 0;
            foreach (string g in allGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                string stem = Path.GetFileNameWithoutExtension(p);
                var def = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(p);
                var vis = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(VisualFolder + "/" + stem + ".asset");
                if (def == null || vis == null || vis.HullMesh == null || vis.HullMesh.Mesh == null) continue;
                TagStats t = TagHullFaces(def, vis.HullMesh.Mesh);
                fleetHulls++; fleetFaces += t.Faces; fleetTagged += t.Tagged; fleetAmbig += t.Ambiguous;
                sb.AppendLine(string.Format("   {0,-40} {1,7} {2,8} {3,8} {4,8:0.00}% {5,8}",
                                            stem, t.Faces, t.Tagged, t.Ambiguous,
                                            t.Faces > 0 ? 100.0 * t.Ambiguous / t.Faces : 0.0, t.Levels));
            }
            sb.AppendLine(string.Format("   {0,-40} {1,7} {2,8} {3,8} {4,8:0.00}%",
                                        "TOTAL (" + fleetHulls + " hulls)", fleetFaces, fleetTagged, fleetAmbig,
                                        fleetFaces > 0 ? 100.0 * fleetAmbig / fleetFaces : 0.0));
            return sb.ToString();
        }

        // ------------------------------------------------------------------ one hull

        static void Hull(StringBuilder sb, CultureInfo inv, string stem)
        {
            var def = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(InteriorFolder + "/" + stem + ".asset");
            var vis = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(VisualFolder + "/" + stem + ".asset");
            if (def == null) { sb.AppendLine(stem + ": NO INTERIOR DEF"); return; }
            if (vis == null || vis.HullMesh == null || vis.HullMesh.Mesh == null)
            { sb.AppendLine(stem + ": NO BAKED HULL MESH — cannot test containment"); return; }

            HullMeshDef hm = vis.HullMesh;
            Mesh mesh = hm.Mesh;
            Vector3[] hullVerts = mesh.vertices;
            int[] hullTriIdx = mesh.triangles;
            int hullTris = hullTriIdx.Length / 3;
            List<BoatInteriorExtruder.HullFace> hullFaces =
                BoatInteriorExtruder.FacesOf(hullVerts, hullTriIdx);

            sb.AppendLine(stem + "   (" + def.Id + ")");
            sb.AppendLine(string.Format(inv,
                "   hull mesh: {0} verts / {1} tris   px/m {2}   cell {3}x{4}   azimuth {5} (MEASURED at bake)",
                mesh.vertexCount, hullTris, hm.PxPerMetre, hm.CellW, hm.CellH,
                hm.AzimuthCounterClockwise ? "CCW" : "CW"));
            Bounds b = mesh.bounds;
            sb.AppendLine(string.Format(inv,
                "   hull extent (rig m): x [{0:0.##} .. {1:0.##}]  y [{2:0.##} .. {3:0.##}]  z [{4:0.##} .. {5:0.##}]",
                b.min.x, b.max.x, b.min.y, b.max.y, b.min.z, b.max.z));
            sb.AppendLine(string.Format(inv,
                "   def: px/m {0}   watertight deck {1:0.###} m   levels {2}",
                def.PixelsPerMetre, hm.WatertightDeckHeightMeters,
                def.Levels != null ? def.Levels.Length : 0));

            // How many palette slots a room could have without widening the shader. The facet pass
            // declares float4 _RampMeta[16] and the zodiac already collided with that ceiling once
            // (the fleet-pack arc: 18 materials declared, 14 actually referenced).
            var uvAttrs = new List<Vector4>();
            mesh.GetUVs(0, uvAttrs);
            var matsUsed = new HashSet<int>();
            foreach (Vector4 a in uvAttrs) matsUsed.Add(Mathf.RoundToInt(a.x));
            sb.AppendLine(string.Format(inv,
                "   MATERIAL BUDGET: this hull's faces reference {0} of the facet pass's 16 ramp slots " +
                "-> {1} free for the interior rig's own ramps (SOLEW / CABIN / BIRCH / SHEET ...)",
                matsUsed.Count, 16 - matsUsed.Count));

            // --- FRAME AGREEMENT, measured, not declared -----------------------------------
            //
            // The sidecar declares "+x starboard, +y bow, +z up" and the extractor declares
            // "rig-space, metres, z-up". Two declarations are not a measurement. The measurement is
            // containment run BOTH ways: the room only fits inside the hull in ONE of them.
            //
            // Second, independent check on the same frame: the deck height the interior classifier
            // derived from geometry alone lands on the def's own lowest sole. Two derivations, one
            // number, or a discrepancy worth naming.
            float lowestSole = float.MaxValue;
            foreach (BoatInteriorLevel l in def.Levels) if (l != null) lowestSole = Mathf.Min(lowestSole, l.SoleZMeters);
            sb.AppendLine(string.Format(inv,
                "   FRAME CROSS-CHECK: lowest published sole {0:0.###} m vs HullMeshDef.WatertightDeckHeightMeters " +
                "{1:0.###} m  ->  delta {2:0.###} m",
                lowestSole, hm.WatertightDeckHeightMeters, lowestSole - hm.WatertightDeckHeightMeters));

            // --- WT, measured off the two published polygons -------------------------------
            BoatInteriorLevel house = FindHouseLevel(def);
            if (house != null && def.FootprintOutline != null && def.FootprintOutline.Length >= 3)
            {
                float minGap = float.MaxValue, maxGap = float.MinValue;
                foreach (Vector2 q in house.Outline)
                {
                    float d = DistanceToPolygon(q, def.FootprintOutline);
                    minGap = Mathf.Min(minGap, d); maxGap = Mathf.Max(maxGap, d);
                }
                sb.AppendLine(string.Format(inv,
                    "   WALL THICKNESS, measured: the house sole outline stands {0:0.###}..{1:0.###} m inside the " +
                    "published FOOTPRINT (rig WT is 0.07)", minGap, maxGap));
            }

            // --- the convex hull of the shell, self-tested ---------------------------------
            string bad; float eps;
            SpikeConvexHull.Hull3D shell = SpikeConvexHull.BuildChecked(hullVerts, out bad, out eps);
            if (shell == null)
                sb.AppendLine("   ⚠ WHOLE-HULL CONVEX TEST UNAVAILABLE (" + bad + ") — the per-level SECTION " +
                              "test below still runs, and it is the STRICTER of the two. A hull whose " +
                              "convex hull will not close is reported, never silently passed.");
            else
                sb.AppendLine(string.Format(inv,
                    "   containment reference: convex hull of the shell, {0} faces, closed={1}, eps {2}, " +
                    "self-test PASSED (inputs inside, sabotage caught)",
                    shell.Faces.Count, shell.Closed, eps));
            sb.AppendLine();

            // --- per level ------------------------------------------------------------------
            int totV = 0, totT = 0;
            sb.AppendLine(string.Format("   {0,-12} {1,7} {2,7} {3,6} {4,6} {5,6} {6,6} {7,8} {8,8}",
                                        "level", "verts", "tris", "sole", "wall", "ceil", "head", "soleZ", "ceilZ"));
            foreach (BoatInteriorLevel level in def.Levels)
            {
                if (level == null || !level.IsUsable()) continue;
                BoatInteriorExtruder.CeilingSource src;
                float ceil = BoatInteriorExtruder.MeasureCeiling(def, level, hullFaces, def.Door, out src);
                BoatInteriorExtruder.LevelMesh lm =
                    BoatInteriorExtruder.Extrude(level, def.Door, ceil, src, false);
                totV += lm.Verts.Count; totT += lm.Tris.Count / 3;
                sb.AppendLine(string.Format(inv, "   {0,-12} {1,7} {2,7} {3,6} {4,6} {5,6} {6,6} {7,8:0.###} {8,8:0.###}",
                                            lm.LevelId, lm.Verts.Count, lm.Tris.Count / 3,
                                            lm.SoleTris, lm.WallTris, lm.CeilingTris, lm.HeaderTris,
                                            lm.SoleZ, lm.CeilingZ));
            }
            sb.AppendLine(string.Format(inv,
                "   TOTAL interior shell: {0} verts / {1} tris  =  +{2:0.0}% on the hull's {3} tris   " +
                "buffer +{4:0.0} KB (pos+nrm+uv0+uv1 = 48 B/vert, 4 B/index)",
                totV, totT, hullTris > 0 ? 100.0 * totT / hullTris : 0.0, hullTris,
                (totV * 48.0 + totT * 3 * 4.0) / 1024.0));
            sb.AppendLine();

            // --- the detail per level: ceiling provenance, door, containment ----------------
            foreach (BoatInteriorLevel level in def.Levels)
            {
                if (level == null || !level.IsUsable()) continue;
                BoatInteriorExtruder.CeilingSource src;
                float ceil = BoatInteriorExtruder.MeasureCeiling(def, level, hullFaces, def.Door, out src);
                BoatInteriorExtruder.LevelMesh lm =
                    BoatInteriorExtruder.Extrude(level, def.Door, ceil, src, false);
                BoatInteriorExtruder.LevelMesh flipped =
                    BoatInteriorExtruder.Extrude(level, def.Door, ceil, src, true);

                float highest = float.MinValue;
                foreach (Vector3 v in hullVerts)
                    if (v.z > level.SoleZMeters &&
                        BoatInteriorExtruder.PointInPolygon(new Vector2(v.x, v.y), level.Outline) &&
                        v.z > highest) highest = v.z;
                var sweep = new List<string>();
                foreach (float hr in BoatInteriorExtruder.HeadroomSweep)
                {
                    BoatInteriorExtruder.CeilingSource cs;
                    float cz = BoatInteriorExtruder.MeasureCeiling(def, level, hullFaces, def.Door, hr, out cs);
                    sweep.Add(hr.ToString("0.0", inv) + "->" + (cz - level.SoleZMeters).ToString("0.##", inv) +
                              (cs == BoatInteriorExtruder.CeilingSource.LevelAbove ? "L" :
                               cs == BoatInteriorExtruder.CeilingSource.HullRoofPlane ? "R" : "?"));
                }
                sb.AppendLine("   [" + lm.LevelId + "]  outline " + lm.OutlinePoints + " pts   " +
                              "headroom " + lm.HeadroomMeters.ToString("0.###", inv) + " m   ceiling from " + src +
                              (highest > float.MinValue
                                 ? "   (highest hull vertex over this outline: " +
                                   highest.ToString("0.###", inv) + " m — the masthead, NOT the roof)"
                                 : ""));
                sb.AppendLine("      HEADROOM SWEEP (search floor -> resulting headroom; L = a level above, " +
                              "R = a hull roof plane): " + string.Join("  ", sweep));
                sb.AppendLine("      door: " + lm.DoorNote + "   (edges cut: " + lm.DoorEdgesCut + ")");

                Containment c = Contain(shell, hullVerts, lm);
                Containment cf = Contain(shell, hullVerts, flipped);
                sb.AppendLine(string.Format(inv,
                    "      CONTAINMENT, bow as published: convex-hull violations {0}/{1} (worst {2:0.####} m)   " +
                    "section violations {3}/{1} (worst {4:0.####} m)",
                    c.HullViolations, lm.Verts.Count, c.WorstHull, c.SectionViolations, c.WorstSection));
                sb.AppendLine(string.Format(inv,
                    "      CONTAINMENT, bow FLIPPED (the control): convex-hull violations {0}/{1} (worst {2:0.####} m)   " +
                    "section violations {3}/{1} (worst {4:0.####} m)",
                    cf.HullViolations, flipped.Verts.Count, cf.WorstHull, cf.SectionViolations, cf.WorstSection));
                sb.AppendLine();
            }

            // --- B on this hull ---------------------------------------------------------------
            TagStats t2 = TagHullFaces(def, mesh);
            sb.AppendLine(string.Format(inv,
                "   B — level tag on the hull's OWN faces: {0} faces, {1} tagged to a level, {2} ambiguous " +
                "(straddling a level boundary) = {3:0.00}%",
                t2.Faces, t2.Tagged, t2.Ambiguous, t2.Faces > 0 ? 100.0 * t2.Ambiguous / t2.Faces : 0.0));
            foreach (var kv in t2.PerLevel)
                sb.AppendLine("      " + kv.Key + ": " + kv.Value + " hull faces");

            // ⚠ TWO LEVELS CAN SHARE ONE SOLE, and then "the highest sole" cannot separate them.
            var byZ = new Dictionary<float, List<string>>();
            foreach (BoatInteriorLevel l in def.Levels)
            {
                if (l == null || !l.IsUsable()) continue;
                List<string> at;
                if (!byZ.TryGetValue(l.SoleZMeters, out at)) { at = new List<string>(); byZ[l.SoleZMeters] = at; }
                at.Add(l.Id);
            }
            foreach (var kv in byZ)
                if (kv.Value.Count > 1)
                    sb.AppendLine(string.Format(inv,
                        "      ⚠ TIE: {0} share sole z {1:0.###} m — the rule cannot separate them and the " +
                        "faces all go to whichever the def lists FIRST. A ceiling per level would break it.",
                        string.Join(" + ", kv.Value), kv.Key));
        }

        // ------------------------------------------------------------------ containment

        public struct Containment
        {
            public int HullViolations, SectionViolations;
            public float WorstHull, WorstSection;
        }

        public static Containment Contain(SpikeConvexHull.Hull3D shell, Vector3[] hullVerts,
                                          BoatInteriorExtruder.LevelMesh lm)
        {
            var r = new Containment { WorstHull = float.MinValue, WorstSection = float.MinValue };

            // The per-level SECTION: the convex hull of the shell's vertices standing in this
            // level's own z band, projected to xy. Far tighter than the whole-hull convex hull,
            // because a boat tapers and a room near the bow could sit inside the global hull while
            // standing outside the actual topsides there.
            var band = new List<Vector2>();
            float lo = lm.SoleZ - SectionBandMeters, hi = lm.CeilingZ + SectionBandMeters;
            foreach (Vector3 v in hullVerts)
                if (v.z >= lo && v.z <= hi) band.Add(new Vector2(v.x, v.y));
            SpikeConvexHull.Hull2D section = SpikeConvexHull.Build2D(band);

            foreach (Vector3 v in lm.Verts)
            {
                if (shell != null)
                {
                    float d = shell.Outside(v);
                    if (d > r.WorstHull) r.WorstHull = d;
                    if (d > ContainmentEpsilonMeters) r.HullViolations++;
                }

                if (section.Points.Length >= 3)
                {
                    float s = section.Outside(new Vector2(v.x, v.y));
                    if (s > r.WorstSection) r.WorstSection = s;
                    if (s > ContainmentEpsilonMeters) r.SectionViolations++;
                }
            }
            if (r.WorstHull == float.MinValue) r.WorstHull = 0f;   // shell unavailable
            if (r.WorstSection == float.MinValue) r.WorstSection = 0f;
            return r;
        }

        // ------------------------------------------------------------------ B: the tag

        public struct TagStats
        {
            public int Faces, Tagged, Ambiguous, Levels;
            public Dictionary<string, int> PerLevel;
        }

        /// <summary>
        /// Which of the hull's own faces belong to which LEVEL, from LAYOUT heights and the level's
        /// published outline — and nothing else. Returns the counts; the mesh is not modified.
        /// </summary>
        public static TagStats TagHullFaces(BoatInteriorDef def, Mesh mesh)
        {
            var st = new TagStats { PerLevel = new Dictionary<string, int>() };
            if (def == null || def.Levels == null || mesh == null) return st;

            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            st.Faces = tris.Length / 3;

            var levels = new List<BoatInteriorLevel>();
            foreach (BoatInteriorLevel l in def.Levels)
            {
                if (l == null || !l.IsUsable()) continue;
                levels.Add(l);
                st.PerLevel[l.Id] = 0;
            }
            st.Levels = levels.Count;
            if (levels.Count == 0) return st;

            foreach (BoatInteriorExtruder.HullFace f in BoatInteriorExtruder.FacesOf(verts, tris))
            {
                int idx;
                BoatInteriorExtruder.FaceTag tag = BoatInteriorExtruder.TagFace(f, levels, out idx);
                if (tag == BoatInteriorExtruder.FaceTag.Level) { st.Tagged++; st.PerLevel[levels[idx].Id]++; }
                else if (tag == BoatInteriorExtruder.FaceTag.StraddlesTheSole) st.Ambiguous++;
            }
            return st;
        }

        // ------------------------------------------------------------------ small helpers

        public static BoatInteriorLevel FindHouseLevel(BoatInteriorDef def)
        {
            if (def == null || def.Levels == null) return null;
            foreach (BoatInteriorLevel l in def.Levels)
                if (l != null && l.IsUsable() && l.Id != null && l.Id.Contains("house")) return l;
            BoatInteriorLevel best = null;
            foreach (BoatInteriorLevel l in def.Levels)
                if (l != null && l.IsUsable() && (best == null || l.Outline.Length > best.Outline.Length)) best = l;
            return best;
        }

        static float DistanceToPolygon(Vector2 q, IList<Vector2> poly)
        {
            float best = float.MaxValue;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float tt = len2 < 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2);
                best = Mathf.Min(best, Vector2.Distance(q, a + tt * ab));
            }
            return best;
        }
    }
}
