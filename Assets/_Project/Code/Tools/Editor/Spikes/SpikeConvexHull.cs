using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Tools.Editor.Spikes
{
    /// <summary>
    /// <b>SPIKE ONLY — convex hulls, 3D and 2D, so containment is a MEASUREMENT and not a claim.</b>
    ///
    /// <para>Question A asks whether every interior face lies inside the hull's existing shell.
    /// "Inside a triangle soup" is not a well-posed test (a hull mesh is not guaranteed
    /// watertight and its inner strakes make ray parity unreliable — the interior classifier
    /// already met that), so the spike asks a question that IS well-posed and strictly
    /// conservative in the direction that matters: <b>is the point inside the convex hull of the
    /// hull's own vertices?</b> A point outside that is outside the boat, full stop. A point
    /// inside it may still be outside the real hull where she is concave, which is why the spike
    /// also runs the far tighter per-level SECTION test (<see cref="Hull2D"/>): the convex hull of
    /// the hull vertices standing in that level's own z band.</para>
    ///
    /// <para><b>Self-checked, because a wrong hull would silently pass everything.</b>
    /// <see cref="SelfTest"/> runs three arms: every input point is inside its own hull; the
    /// hull is closed (every edge shared by exactly two faces); and a point pushed well outside
    /// is caught. The spike refuses to report containment numbers if any arm fails.</para>
    /// </summary>
    public static class SpikeConvexHull
    {
        public sealed class Face
        {
            public int A, B, C;
            public Vector3 N;     // outward unit normal
            public float D;       // plane: dot(N, p) = D
        }

        public sealed class Hull3D
        {
            public Vector3[] Points;
            public List<Face> Faces = new List<Face>();
            public bool Closed;

            /// <summary>Signed distance outside the hull: &lt;= 0 means inside every face plane.</summary>
            public float Outside(Vector3 p)
            {
                float worst = float.MinValue;
                for (int i = 0; i < Faces.Count; i++)
                {
                    Face f = Faces[i];
                    float d = Vector3.Dot(f.N, p) - f.D;
                    if (d > worst) worst = d;
                }
                return Faces.Count == 0 ? float.MaxValue : worst;
            }
        }

        /// <summary>Incremental 3D convex hull. Degenerate (coplanar) input returns an unclosed
        /// hull with <see cref="Hull3D.Closed"/> false, which the caller must treat as a failure
        /// rather than as "everything is inside".</summary>
        public static Hull3D Build3D(IList<Vector3> pts, float eps = 1e-5f)
        {
            var h = new Hull3D();
            if (pts == null || pts.Count < 4) return h;

            // --- seed tetrahedron: extreme point pair, then farthest from that line, then plane.
            int i0 = 0, i1 = 0;
            float best = -1f;
            // cheap: extremes along the three axes give a good first edge without an O(n^2) sweep
            int[] ext = Extremes(pts);
            for (int a = 0; a < ext.Length; a++)
                for (int b = a + 1; b < ext.Length; b++)
                {
                    float d = (pts[ext[a]] - pts[ext[b]]).sqrMagnitude;
                    if (d > best) { best = d; i0 = ext[a]; i1 = ext[b]; }
                }
            if (best <= eps) return h;

            int i2 = -1; best = -1f;
            for (int i = 0; i < pts.Count; i++)
            {
                float d = Vector3.Cross(pts[i1] - pts[i0], pts[i] - pts[i0]).sqrMagnitude;
                if (d > best) { best = d; i2 = i; }
            }
            if (i2 < 0 || best <= eps) return h;

            Vector3 n0 = Vector3.Cross(pts[i1] - pts[i0], pts[i2] - pts[i0]).normalized;
            int i3 = -1; best = -1f;
            for (int i = 0; i < pts.Count; i++)
            {
                float d = Mathf.Abs(Vector3.Dot(n0, pts[i] - pts[i0]));
                if (d > best) { best = d; i3 = i; }
            }
            if (i3 < 0 || best <= eps) return h;

            h.Points = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++) h.Points[i] = pts[i];

            AddFace(h, i0, i1, i2, pts[i3]);
            AddFace(h, i0, i1, i3, pts[i2]);
            AddFace(h, i0, i2, i3, pts[i1]);
            AddFace(h, i1, i2, i3, pts[i0]);

            // --- add every remaining point that is outside the current hull.
            var visible = new List<int>();
            var horizon = new List<long>();
            var seed = new HashSet<int> { i0, i1, i2, i3 };
            for (int p = 0; p < pts.Count; p++)
            {
                if (seed.Contains(p)) continue;
                Vector3 q = pts[p];
                visible.Clear();
                for (int f = 0; f < h.Faces.Count; f++)
                    if (Vector3.Dot(h.Faces[f].N, q) - h.Faces[f].D > eps) visible.Add(f);
                if (visible.Count == 0) continue;

                // horizon = edges belonging to exactly one visible face
                var count = new Dictionary<long, int>();
                var dir = new Dictionary<long, bool>();
                foreach (int f in visible)
                {
                    Face fc = h.Faces[f];
                    Tally(count, dir, fc.A, fc.B);
                    Tally(count, dir, fc.B, fc.C);
                    Tally(count, dir, fc.C, fc.A);
                }
                horizon.Clear();
                foreach (var kv in count) if (kv.Value == 1) horizon.Add(kv.Key);

                // remove visible faces (descending index so removal stays valid)
                visible.Sort();
                for (int k = visible.Count - 1; k >= 0; k--) h.Faces.RemoveAt(visible[k]);

                // cone from the horizon to q; interior reference = centroid of the seed tet
                Vector3 inside = (pts[i0] + pts[i1] + pts[i2] + pts[i3]) * 0.25f;
                foreach (long e in horizon)
                {
                    int a = (int)(e >> 32), b = (int)(e & 0xffffffffL);
                    if (!dir[e]) { int t = a; a = b; b = t; }
                    AddFace(h, a, b, p, inside);
                }
            }

            h.Closed = IsClosed(h);
            return h;
        }

        static void Tally(Dictionary<long, int> count, Dictionary<long, bool> dir, int a, int b)
        {
            bool forward = a < b;
            long key = forward ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int c;
            count[key] = count.TryGetValue(key, out c) ? c + 1 : 1;
            if (c == 0) dir[key] = forward;
        }

        static void AddFace(Hull3D h, int a, int b, int c, Vector3 interiorRef)
        {
            Vector3 pa = h.Points != null ? h.Points[a] : Vector3.zero;
            Vector3 pb = h.Points[b], pc = h.Points[c];
            Vector3 n = Vector3.Cross(pb - pa, pc - pa);
            if (n.sqrMagnitude <= 0f) return;
            n = n.normalized;
            float d = Vector3.Dot(n, pa);
            if (Vector3.Dot(n, interiorRef) - d > 0f) { n = -n; d = -d; int t = b; b = c; c = t; }
            h.Faces.Add(new Face { A = a, B = b, C = c, N = n, D = d });
        }

        static bool IsClosed(Hull3D h)
        {
            var count = new Dictionary<long, int>();
            foreach (Face f in h.Faces)
            {
                Bump(count, f.A, f.B); Bump(count, f.B, f.C); Bump(count, f.C, f.A);
            }
            foreach (var kv in count) if (kv.Value != 2) return false;
            return h.Faces.Count >= 4;
        }

        static void Bump(Dictionary<long, int> count, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int c;
            count[key] = count.TryGetValue(key, out c) ? c + 1 : 1;
        }

        static int[] Extremes(IList<Vector3> p)
        {
            int xa = 0, xb = 0, ya = 0, yb = 0, za = 0, zb = 0;
            for (int i = 1; i < p.Count; i++)
            {
                if (p[i].x < p[xa].x) xa = i; if (p[i].x > p[xb].x) xb = i;
                if (p[i].y < p[ya].y) ya = i; if (p[i].y > p[yb].y) yb = i;
                if (p[i].z < p[za].z) za = i; if (p[i].z > p[zb].z) zb = i;
            }
            return new[] { xa, xb, ya, yb, za, zb };
        }

        // ---------------------------------------------------------------- 2D (monotone chain)

        public sealed class Hull2D
        {
            public Vector2[] Points = new Vector2[0];

            /// <summary>Signed distance outside: &lt;= 0 means inside every edge.</summary>
            public float Outside(Vector2 q)
            {
                if (Points.Length < 3) return float.MaxValue;
                float worst = float.MinValue;
                for (int i = 0; i < Points.Length; i++)
                {
                    Vector2 a = Points[i], b = Points[(i + 1) % Points.Length];
                    Vector2 e = b - a;
                    float len = e.magnitude;
                    if (len < 1e-9f) continue;
                    // ccw hull: outward normal is (e.y, -e.x)/len
                    float d = ((q.x - a.x) * e.y - (q.y - a.y) * e.x) / len;
                    if (d > worst) worst = d;
                }
                return worst;
            }
        }

        public static Hull2D Build2D(IList<Vector2> pts)
        {
            var h = new Hull2D();
            if (pts == null || pts.Count < 3) return h;
            var p = new List<Vector2>(pts);
            p.Sort((u, v) => u.x != v.x ? u.x.CompareTo(v.x) : u.y.CompareTo(v.y));
            var lower = new List<Vector2>();
            foreach (Vector2 q in p)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], q) <= 0)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(q);
            }
            var upper = new List<Vector2>();
            for (int i = p.Count - 1; i >= 0; i--)
            {
                Vector2 q = p[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], q) <= 0)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(q);
            }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            h.Points = lower.ToArray();
            return h;
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        // ---------------------------------------------------------------- self-test

        /// <summary>
        /// Deduplicate to a millimetre grid before hulling. A rig mesh stores three UNSHARED
        /// vertices per face (RigMeshBuilder), so a 1,384-face hull hands the incremental hull
        /// 4,152 points of which most are exact duplicates and many are coplanar — the shape that
        /// left the trawler's hull unclosed on the first run.
        /// </summary>
        public static List<Vector3> Dedupe(IList<Vector3> pts, float grid = 1e-3f)
        {
            var seen = new HashSet<long>();
            var outp = new List<Vector3>(pts.Count);
            foreach (Vector3 v in pts)
            {
                long kx = (long)Mathf.Round(v.x / grid);
                long ky = (long)Mathf.Round(v.y / grid);
                long kz = (long)Mathf.Round(v.z / grid);
                long key = kx * 73856093L ^ ky * 19349663L ^ kz * 83492791L;
                if (seen.Add(key)) outp.Add(v);
            }
            return outp;
        }

        /// <summary>Build a hull that passes <see cref="SelfTest"/>, walking an epsilon ladder.
        /// Returns null (and the reason) when no tolerance works — never a hull that would report
        /// everything as contained.</summary>
        public static Hull3D BuildChecked(IList<Vector3> pts, out string failure, out float epsUsed)
        {
            List<Vector3> p = Dedupe(pts);
            float[] ladder = { 1e-5f, 1e-4f, 1e-3f, 5e-3f };
            failure = "no points";
            epsUsed = 0f;
            foreach (float e in ladder)
            {
                string bad = SelfTest(p, e);
                if (bad == null) { failure = null; epsUsed = e; return Build3D(p, e); }
                failure = bad + " (at eps " + e + ")";
            }
            return null;
        }

        /// <summary>Three arms; returns null when all pass, else why it failed.</summary>
        public static string SelfTest(IList<Vector3> pts, float eps)
        {
            Hull3D h = Build3D(pts, eps);
            if (h.Faces.Count < 4) return "hull has fewer than 4 faces (degenerate input?)";
            if (!h.Closed) return "hull is not closed (an edge is not shared by exactly two faces)";

            float worstIn = float.MinValue;
            for (int i = 0; i < pts.Count; i++) worstIn = Mathf.Max(worstIn, h.Outside(pts[i]));
            if (worstIn > 1e-3f)
                return string.Format("an INPUT point lies {0:0.####} m outside its own hull", worstIn);

            // sabotage: push a point far out along the worst face normal
            Vector3 sab = pts[0];
            float span = 0f;
            foreach (Vector3 v in pts) span = Mathf.Max(span, v.magnitude);
            sab += h.Faces[0].N * (span + 10f);
            if (h.Outside(sab) <= 0f) return "SABOTAGE NOT DETECTED — a point far outside reads inside";
            return null;
        }
    }
}
