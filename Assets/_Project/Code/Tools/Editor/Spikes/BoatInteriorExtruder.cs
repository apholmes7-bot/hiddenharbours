using System;
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.Editor.Spikes
{
    /// <summary>
    /// <b>SPIKE ONLY (spike/interior-mesh, question A) — extrude a boat interior LEVEL into hull
    /// mesh space.</b> Nothing here ships, nothing here is baked, and no committed asset is
    /// touched: it reads a <see cref="BoatInteriorDef"/> and emits triangles so the spike can
    /// COUNT them and test whether they lie inside the hull's own shell.
    ///
    /// <para><b>Frame.</b> The sidecars declare <c>"+x starboard, +y bow, +z up"</c>, metres,
    /// origin amidships/keel/centreline — the same frame
    /// <c>RigMeshExtractor</c> reports for the hull's own faces ("rig-space object coordinates,
    /// metres, z-up"). That agreement is DECLARED by two documents, so the spike does not trust
    /// it: <see cref="BoatInteriorMeshExtrusionSpike"/> re-extrudes each level with the bow
    /// flipped and reports containment BOTH ways. The orientation with the violations is the
    /// wrong one, measured.</para>
    ///
    /// <para><b>What is emitted, and what deliberately is not.</b> Sole, walls, ceiling and the
    /// door aperture — the shell of the room. Obstructions (console, stove, locker, bunk) are the
    /// FIT-OUT and are question D's subject, not the shell's; they stay out so the counts answer
    /// "what does the room cost" and not "what does the furniture cost".</para>
    /// </summary>
    public static class BoatInteriorExtruder
    {
        /// <summary>
        /// How far above a sole the ceiling search starts, metres — i.e. the smallest gap that
        /// counts as a room rather than a shelf.
        ///
        /// <para>⚠️ <b>IT IS A TUNABLE, AND THAT IS THE FINDING.</b> At 1.5 m — chosen because it is
        /// under every published door clear height in the kit — the LOBSTER'S CUDDY finds no roof
        /// below it and takes the WHEELHOUSE roof at 2.12 m instead: a crawl-in berth space given
        /// two metres of headroom and a box standing proud of her foredeck. At 0.9 m it finds her
        /// actual foredeck. Neither value is wrong; the question is not answerable from the hull
        /// mesh, and <see cref="BoatInteriorMeshExtrusionSpike"/> prints the whole sweep per level
        /// so the reader can see the answer move. The rig knows this number. It does not publish
        /// it.</para>
        /// </summary>
        public const float StoreyHeadroomMeters = 0.9f;

        /// <summary>The sweep the report prints, to show the ceiling is a continuum and not a fact
        /// the mesh contains.</summary>
        public static readonly float[] HeadroomSweep = { 0.6f, 0.9f, 1.2f, 1.5f, 1.8f };

        /// <summary>Minimum xy overlap (fraction of THIS level's outline bbox area) before a level
        /// above counts as this one's ceiling. Below it the two rooms are side by side — the
        /// lobster's cuddy and wheelhouse — and neither roofs the other.</summary>
        public const float CeilingOverlapFraction = 0.25f;

        public enum CeilingSource
        {
            /// <summary>The sole of the level above IS this level's ceiling — exact, no constant.</summary>
            LevelAbove,
            /// <summary>Measured off the HULL MESH: the LOWEST near-horizontal hull face standing
            /// over this level's outline, i.e. the first thing you would hit going up. It is the
            /// roof's own plane, not its underside, so the room it caps is taller than the real one
            /// by the roof's thickness — and the rig knows that number and does not publish it
            /// (question D).
            ///
            /// <para>⚠️ THE FIRST ATTEMPT WAS THE HIGHEST VERTEX OVER THE OUTLINE, AND IT MEASURED
            /// THE MAST. It gave the lobster's wheelhouse 5.458 m of headroom and the packet's main
            /// deck 14.13 m — a room the size of the whole superstructure, which then failed
            /// containment and swallowed the render. Caught by the picture, not by the number.</para>
            /// </summary>
            HullRoofPlane,
            /// <summary>No hull geometry over the outline at all — fell back to the door's clear
            /// height. Reported loudly; it means the extrusion is guessing.</summary>
            DoorClearHeightFallback,
        }

        public enum Part { Sole, Wall, Ceiling, DoorHeader }

        public sealed class LevelMesh
        {
            public string LevelId;
            public float SoleZ, CeilingZ;
            public CeilingSource Ceiling;
            public float HeadroomMeters { get { return CeilingZ - SoleZ; } }

            public readonly List<Vector3> Verts = new List<Vector3>();
            public readonly List<int> Tris = new List<int>();
            /// <summary>Per-TRIANGLE part tag, parallel to Tris/3.</summary>
            public readonly List<Part> Parts = new List<Part>();
            /// <summary>Per-triangle flat normal, the rig's own (b-a) cross (c-a).</summary>
            public readonly List<Vector3> Normals = new List<Vector3>();

            public int SoleTris, WallTris, CeilingTris, HeaderTris;
            /// <summary>How many wall edges the door aperture actually cut. 0 means the door was
            /// not found on the outline — a finding, not a detail.</summary>
            public int DoorEdgesCut;
            public string DoorNote = "";
            public int OutlinePoints;
        }

        /// <summary>
        /// Extrude one level. <paramref name="ceilingZ"/> is supplied by the caller because it is
        /// MEASURED (see <see cref="CeilingSource"/>) rather than published.
        /// </summary>
        public static LevelMesh Extrude(BoatInteriorLevel level, BoatInteriorDoor door,
                                        float ceilingZ, CeilingSource ceilingSource,
                                        bool flipBow)
        {
            if (level == null) throw new ArgumentNullException("level");
            var m = new LevelMesh
            {
                LevelId = level.Id,
                SoleZ = level.SoleZMeters,
                CeilingZ = ceilingZ,
                Ceiling = ceilingSource,
            };

            Vector2[] outline = Normalise(level.Outline, flipBow);
            m.OutlinePoints = outline.Length;
            if (outline.Length < 3) { m.DoorNote = "outline has fewer than 3 points"; return m; }

            // ---- sole: ear-clipped, normal +z (up, into the room) -------------------------
            int[] fan = EarClip(outline);
            for (int i = 0; i + 2 < fan.Length; i += 3)
                AddTri(m, Part.Sole,
                       new Vector3(outline[fan[i]].x, outline[fan[i]].y, m.SoleZ),
                       new Vector3(outline[fan[i + 1]].x, outline[fan[i + 1]].y, m.SoleZ),
                       new Vector3(outline[fan[i + 2]].x, outline[fan[i + 2]].y, m.SoleZ));
            m.SoleTris = m.Tris.Count / 3;

            // ---- ceiling: the same polygon, wound the other way so it faces DOWN ----------
            for (int i = 0; i + 2 < fan.Length; i += 3)
                AddTri(m, Part.Ceiling,
                       new Vector3(outline[fan[i + 2]].x, outline[fan[i + 2]].y, ceilingZ),
                       new Vector3(outline[fan[i + 1]].x, outline[fan[i + 1]].y, ceilingZ),
                       new Vector3(outline[fan[i]].x, outline[fan[i]].y, ceilingZ));
            m.CeilingTris = m.Tris.Count / 3 - m.SoleTris;

            // ---- the door: which outline edge carries it, and over what span --------------
            //
            // The threshold point is a POINT on the wall line; the aperture is that point widened
            // by the published clear width along the edge it lands on. Found by nearest-edge, not
            // by trusting Door.Side's word ("aft" is a label, the geometry is the fact).
            int doorEdge = -1;
            float doorT0 = 0f, doorT1 = 0f, doorTop = ceilingZ;
            if (door != null && door.ClearWidthMeters > 0f)
            {
                var tp = new Vector2(door.ThresholdPoint.x,
                                     flipBow ? -door.ThresholdPoint.y : door.ThresholdPoint.y);
                float best = float.MaxValue, bestT = 0f;
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                    float t;
                    float d = PointSegmentDistance(tp, a, b, out t);
                    if (d < best) { best = d; doorEdge = i; bestT = t; }
                }
                if (doorEdge >= 0)
                {
                    Vector2 a = outline[doorEdge], b = outline[(doorEdge + 1) % outline.Length];
                    float len = (b - a).magnitude;
                    float half = 0.5f * door.ClearWidthMeters / Mathf.Max(len, 1e-6f);
                    doorT0 = Mathf.Clamp01(bestT - half);
                    doorT1 = Mathf.Clamp01(bestT + half);
                    doorTop = Mathf.Min(ceilingZ, m.SoleZ + door.ClearHeightMeters);
                    m.DoorNote = string.Format(
                        "nearest edge {0} (len {1:0.###} m), threshold off the wall line by {2:0.####} m, " +
                        "span t[{3:0.###}..{4:0.###}], head {5:0.###} m of {6:0.###} m clear",
                        doorEdge, len, best, doorT0, doorT1, doorTop - m.SoleZ, door.ClearHeightMeters);
                    if (doorT1 - doorT0 < 1e-4f) { doorEdge = -1; m.DoorNote += "  [SPAN COLLAPSED]"; }
                }
            }
            else m.DoorNote = "no door published on this def";

            // ---- walls: inward-facing quads, split around the aperture --------------------
            int wallStart = m.Tris.Count / 3;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                if (i != doorEdge)
                {
                    AddWallQuad(m, Part.Wall, a, b, m.SoleZ, ceilingZ);
                    continue;
                }
                m.DoorEdgesCut++;
                Vector2 d0 = Vector2.Lerp(a, b, doorT0);
                Vector2 d1 = Vector2.Lerp(a, b, doorT1);
                if (doorT0 > 1e-4f) AddWallQuad(m, Part.Wall, a, d0, m.SoleZ, ceilingZ);
                if (doorT1 < 1f - 1e-4f) AddWallQuad(m, Part.Wall, d1, b, m.SoleZ, ceilingZ);
                if (ceilingZ - doorTop > 1e-4f)
                {
                    int before = m.Tris.Count / 3;
                    AddWallQuad(m, Part.DoorHeader, d0, d1, doorTop, ceilingZ);
                    m.HeaderTris += m.Tris.Count / 3 - before;
                }
            }
            m.WallTris = m.Tris.Count / 3 - wallStart - m.HeaderTris;
            return m;
        }

        /// <summary>What a level-tagging pass decided about one hull face.</summary>
        public enum FaceTag { None, Level, StraddlesTheSole }

        /// <summary>
        /// <b>Which LEVEL does this hull face belong to?</b> — the one rule, used by the report and
        /// by the render fixture, so the two cannot disagree about what was tagged.
        ///
        /// <para><b>The rule:</b> a face belongs to the HIGHEST level whose sole is at or below the
        /// face's own lowest point, among the levels whose published outline contains the face's
        /// centroid in xy. Nothing else: no material name, no normal direction, no face bias, no
        /// inset threshold — every one of those was measured false by
        /// <c>RigMeshInteriorClassifier</c> and is not re-derived here.</para>
        ///
        /// <para>⚠️ <b>The first rule tried was "the face's whole z span lies between the sole and
        /// the ceiling", and it is WRONG in a way the numbers hid.</b> A deckhouse WALL runs from
        /// the sole to the roof and its roof is above the ceiling, so the wall failed the test and
        /// stayed drawn — the room was then correctly culled-into and still invisible, 3.1% of its
        /// pixels surviving. What has to come off is everything STANDING ON this level, not
        /// everything inside its headroom.</para>
        ///
        /// <para><b>The honest residue</b> is <see cref="FaceTag.StraddlesTheSole"/>: a face whose
        /// bottom is below the sole and whose top is above it — the deck plate itself, which
        /// belongs to the level and to the hull at once. Counted, never silently assigned.</para>
        /// </summary>
        public static FaceTag TagFace(in HullFace f, IList<BoatInteriorLevel> levels,
                                      out int levelIndex, float epsilon = 0.02f)
        {
            levelIndex = -1;
            bool straddles = false;
            float bestSole = float.MinValue;
            for (int i = 0; i < levels.Count; i++)
            {
                BoatInteriorLevel l = levels[i];
                if (l == null || !l.IsUsable()) continue;
                if (!PointInPolygon(f.Centroid, l.Outline)) continue;
                if (f.ZLow >= l.SoleZMeters - epsilon)
                {
                    if (l.SoleZMeters > bestSole) { bestSole = l.SoleZMeters; levelIndex = i; }
                }
                else if (f.ZHigh > l.SoleZMeters + epsilon) straddles = true;
            }
            if (levelIndex >= 0) return FaceTag.Level;
            return straddles ? FaceTag.StraddlesTheSole : FaceTag.None;
        }

        /// <summary>Ceiling for one level, MEASURED. Returns the z and says where it came from.</summary>
        /// <summary>One hull face, reduced to what a ceiling search needs.</summary>
        public struct HullFace
        {
            public Vector2 Centroid;
            public float ZLow, ZHigh, NormalZ;
        }

        /// <summary>|n.z| above which a face counts as a ROOF rather than a wall. 0.85 is about 32
        /// degrees off level — steeper than any wheelhouse roof camber in the kit and far off
        /// vertical, so the two classes cannot be confused.</summary>
        public const float HorizontalNormalZ = 0.85f;

        /// <summary>Reduce a hull mesh to the face list <see cref="MeasureCeiling"/> reads.</summary>
        public static List<HullFace> FacesOf(Vector3[] verts, int[] tris)
        {
            var list = new List<HullFace>(tris.Length / 3);
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                float m = n.magnitude;
                list.Add(new HullFace
                {
                    Centroid = new Vector2((a.x + b.x + c.x) / 3f, (a.y + b.y + c.y) / 3f),
                    ZLow = Mathf.Min(a.z, Mathf.Min(b.z, c.z)),
                    ZHigh = Mathf.Max(a.z, Mathf.Max(b.z, c.z)),
                    NormalZ = m > 0f ? n.z / m : 0f,
                });
            }
            return list;
        }

        public static float MeasureCeiling(BoatInteriorDef def, BoatInteriorLevel level,
                                           List<HullFace> hullFaces, BoatInteriorDoor door,
                                           out CeilingSource source)
        {
            return MeasureCeiling(def, level, hullFaces, door, StoreyHeadroomMeters, out source);
        }

        public static float MeasureCeiling(BoatInteriorDef def, BoatInteriorLevel level,
                                           List<HullFace> hullFaces, BoatInteriorDoor door,
                                           float headroomFloor, out CeilingSource source)
        {
            // 1. a level above, if one genuinely roofs this one.
            float best = float.MaxValue;
            Rect mine = Bounds2(level.Outline);
            float mineArea = Mathf.Max(mine.width * mine.height, 1e-6f);
            if (def != null && def.Levels != null)
            {
                foreach (BoatInteriorLevel other in def.Levels)
                {
                    if (other == null || ReferenceEquals(other, level)) continue;
                    if (other.SoleZMeters < level.SoleZMeters + headroomFloor) continue;
                    if (OverlapArea(mine, Bounds2(other.Outline)) / mineArea < CeilingOverlapFraction) continue;
                    if (other.SoleZMeters < best) best = other.SoleZMeters;
                }
            }
            if (best < float.MaxValue) { source = CeilingSource.LevelAbove; return best; }

            // 2. the hull's own roof over this outline: the LOWEST near-horizontal face standing
            //    over the room, which is what a head hits. Not the highest VERTEX — that is the
            //    masthead (see CeilingSource.HullRoofPlane).
            if (hullFaces != null)
            {
                float roof = float.MaxValue;
                foreach (HullFace f in hullFaces)
                {
                    if (Mathf.Abs(f.NormalZ) < HorizontalNormalZ) continue;
                    if (f.ZLow <= level.SoleZMeters + headroomFloor) continue;
                    if (!PointInPolygon(f.Centroid, level.Outline)) continue;
                    if (f.ZLow < roof) roof = f.ZLow;
                }
                if (roof < float.MaxValue) { source = CeilingSource.HullRoofPlane; return roof; }
            }

            source = CeilingSource.DoorClearHeightFallback;
            return level.SoleZMeters + (door != null && door.ClearHeightMeters > 0f
                                        ? door.ClearHeightMeters : 1.9f);
        }

        // ------------------------------------------------------------------ geometry helpers

        static Vector2[] Normalise(Vector2[] poly, bool flipBow)
        {
            if (poly == null || poly.Length < 3) return new Vector2[0];
            var p = new Vector2[poly.Length];
            for (int i = 0; i < poly.Length; i++)
                p[i] = flipBow ? new Vector2(poly[i].x, -poly[i].y) : poly[i];
            // The sidecars declare ccw_from_above; flipping the bow reverses that, and ear
            // clipping needs to know which side is inside. Fix the winding by SIGNED AREA rather
            // than by the declaration.
            if (SignedArea(p) < 0f) Array.Reverse(p);
            return p;
        }

        public static float SignedArea(IList<Vector2> p)
        {
            double a = 0;
            for (int i = 0; i < p.Count; i++)
            {
                Vector2 u = p[i], v = p[(i + 1) % p.Count];
                a += (double)u.x * v.y - (double)v.x * u.y;
            }
            return (float)(a * 0.5);
        }

        static void AddWallQuad(LevelMesh m, Part part, Vector2 a, Vector2 b, float zLo, float zHi)
        {
            // Wound so the flat normal points INTO the room: with a ccw outline the interior lies
            // to the LEFT of a->b, i.e. along (-dy, dx).
            var v0 = new Vector3(a.x, a.y, zLo);
            var v1 = new Vector3(a.x, a.y, zHi);
            var v2 = new Vector3(b.x, b.y, zHi);
            var v3 = new Vector3(b.x, b.y, zLo);
            AddTri(m, part, v0, v1, v2);
            AddTri(m, part, v0, v2, v3);
        }

        static void AddTri(LevelMesh m, Part part, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = m.Verts.Count;
            m.Verts.Add(a); m.Verts.Add(b); m.Verts.Add(c);
            m.Tris.Add(i); m.Tris.Add(i + 1); m.Tris.Add(i + 2);
            m.Parts.Add(part);
            Vector3 n = Vector3.Cross(b - a, c - a);
            m.Normals.Add(n.sqrMagnitude > 0f ? n.normalized : Vector3.zero);
        }

        /// <summary>Ear clipping, O(n^2), ccw input. Returns index triples into <paramref name="p"/>.</summary>
        public static int[] EarClip(IList<Vector2> p)
        {
            int n = p.Count;
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++) idx.Add(i);
            var outTris = new List<int>((n - 2) * 3);
            int guard = 0;
            while (idx.Count > 3 && guard++ < n * n + 16)
            {
                bool clipped = false;
                for (int k = 0; k < idx.Count; k++)
                {
                    int i0 = idx[(k - 1 + idx.Count) % idx.Count], i1 = idx[k], i2 = idx[(k + 1) % idx.Count];
                    Vector2 a = p[i0], b = p[i1], c = p[i2];
                    if (Cross(b - a, c - b) <= 0f) continue;          // reflex on a ccw polygon
                    bool contains = false;
                    for (int j = 0; j < idx.Count && !contains; j++)
                    {
                        int ij = idx[j];
                        if (ij == i0 || ij == i1 || ij == i2) continue;
                        contains = PointInTriangle(p[ij], a, b, c);
                    }
                    if (contains) continue;
                    outTris.Add(i0); outTris.Add(i1); outTris.Add(i2);
                    idx.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (!clipped) break;   // degenerate; fall through to the fan below
            }
            if (idx.Count == 3) { outTris.Add(idx[0]); outTris.Add(idx[1]); outTris.Add(idx[2]); }
            else for (int k = 1; k + 1 < idx.Count; k++)
            { outTris.Add(idx[0]); outTris.Add(idx[k]); outTris.Add(idx[k + 1]); }
            return outTris.ToArray();
        }

        static float Cross(Vector2 u, Vector2 v) { return u.x * v.y - u.y * v.x; }

        static bool PointInTriangle(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, q - a), d2 = Cross(c - b, q - b), d3 = Cross(a - c, q - c);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        public static bool PointInPolygon(Vector2 q, IList<Vector2> p)
        {
            if (p == null || p.Count < 3) return false;
            bool inside = false;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
                if ((p[i].y > q.y) != (p[j].y > q.y) &&
                    q.x < (p[j].x - p[i].x) * (q.y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            return inside;
        }

        static float PointSegmentDistance(Vector2 q, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            t = len2 < 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2);
            return Vector2.Distance(q, a + t * ab);
        }

        static Rect Bounds2(IList<Vector2> p)
        {
            if (p == null || p.Count == 0) return new Rect();
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (Vector2 v in p)
            {
                x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y);
                x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y);
            }
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        static float OverlapArea(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0 && h > 0 ? w * h : 0f;
        }
    }
}
