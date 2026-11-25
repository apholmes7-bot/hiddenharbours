using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>Where a placed mesh dwelling's walls and fixtures go</b> (ADR 0060 section 6): the pure geometry
    /// <see cref="DwellingSite"/> lays out at enable and the prefab builder lays out at build. Model-frame in,
    /// model-frame out; the turn onto the ground and the squash onto the screen stay
    /// <see cref="InteriorFootprint.ModelToWorld"/>'s, so there is still one picture transform.
    ///
    /// <para><b>The model frame is the rig's</b>: origin the ground centre of the body footprint, +x the curb
    /// (door) side, +y the nose. A site's ground bearing is the compass heading of +y, so +x points at the
    /// bearing plus 90 degrees.</para>
    ///
    /// <para>Pure and headless. <see cref="WallRun"/> allocates its result; it runs once per enable, never
    /// per frame.</para>
    /// </summary>
    public static class DwellingSiteLayout
    {
        /// <summary>One footprint facing per compass degree. A dwelling's footprint turns by its site's
        /// ground bearing in whole degrees, through <see cref="InteriorFootprint"/>'s facing arithmetic
        /// (yaw = -360 * facing / facings), so a bearing of b degrees is facing b of 360.</summary>
        public const int FacingsPerTurn = 360;

        /// <summary>The compass sides a wall can face, clockwise from north.</summary>
        public enum Side { North = 0, East = 1, South = 2, West = 3 }

        /// <summary>A bearing folded into [0, 360).</summary>
        public static int NormalizeBearing(int degrees) => ((degrees % FacingsPerTurn) + FacingsPerTurn) % FacingsPerTurn;

        /// <summary>
        /// <b>The wall line, open at the door.</b> The floor outline walked from one side of the doorway all
        /// the way round to the other: the points an edge collider follows, so every edge of the floor is a
        /// wall except the stretch of <paramref name="gapWidthMetres"/> centred on the point of the outline
        /// nearest <paramref name="doorModel"/>.
        ///
        /// <para>The gap is measured ALONG the outline, so a door that falls on a run of short edges (a
        /// rounded corner, a tumblehome) still gets its full width. Returns an empty array for an outline
        /// that does not close, or a gap as long as the whole perimeter (no wall left to draw).</para>
        /// </summary>
        public static Vector2[] WallRun(Vector2[] outline, Vector2 doorModel, float gapWidthMetres)
        {
            if (!DwellingMeshRules.FloorCloses(outline)) return System.Array.Empty<Vector2>();
            int n = outline.Length;

            // Arc length at each vertex, and the perimeter.
            var arc = new float[n];
            float perimeter = 0f;
            for (int i = 0; i < n; i++)
            {
                arc[i] = perimeter;
                perimeter += Vector2.Distance(outline[i], outline[(i + 1) % n]);
            }

            float gap = Mathf.Max(0f, gapWidthMetres);
            if (gap >= perimeter) return System.Array.Empty<Vector2>();

            float doorArc = NearestArc(outline, arc, doorModel);
            float start = doorArc + gap * 0.5f;          // one jamb
            float end = start + (perimeter - gap);       // round to the other jamb

            var run = new List<Vector2>(n + 2) { PointAt(outline, arc, perimeter, start) };
            // Three laps of vertex arcs cover any start in [0, 1.5 perimeter) and its end.
            for (int lap = 0; lap < 3; lap++)
                for (int i = 0; i < n; i++)
                {
                    float a = arc[i] + lap * perimeter;
                    if (a > start && a < end) run.Add(outline[i]);
                }
            run.Add(PointAt(outline, arc, perimeter, end));
            return run.ToArray();
        }

        /// <summary>
        /// The outward normal, in the model frame, of the floor edge nearest <paramref name="model"/>: which
        /// way the wall a fixture backs onto faces. Zero for an outline that does not close.
        /// </summary>
        public static Vector2 OutwardNormalNearest(Vector2[] outline, Vector2 model)
        {
            if (!DwellingMeshRules.FloorCloses(outline)) return Vector2.zero;
            int n = outline.Length, best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float d = GroundPolygon.DistanceToSegment(model, outline[i], outline[(i + 1) % n]);
                if (d < bestD) { bestD = d; best = i; }
            }
            Vector2 e = outline[(best + 1) % n] - outline[best];
            // Counter-clockwise outline (positive area): the floor is on the left, so out is the right.
            Vector2 right = new Vector2(e.y, -e.x);
            if (DwellingMeshRules.SignedArea(outline) < 0f) right = -right;
            return right.sqrMagnitude > 0f ? right.normalized : Vector2.zero;
        }

        /// <summary>The compass bearing, degrees in [0, 360), of a model-frame direction on a dwelling
        /// whose nose (+y) points at <paramref name="groundBearingDegrees"/>.</summary>
        public static float CompassOf(Vector2 modelDirection, int groundBearingDegrees)
        {
            float local = Mathf.Atan2(modelDirection.x, modelDirection.y) * Mathf.Rad2Deg;
            return Mathf.Repeat(groundBearingDegrees + local, 360f);
        }

        /// <summary>The compass side nearest a bearing: north within 45 degrees of 0, and so on clockwise.
        /// A bearing exactly between two sides goes to the later one.</summary>
        public static Side SideOf(float compassDegrees) =>
            (Side)(Mathf.FloorToInt(Mathf.Repeat(compassDegrees + 45f, 360f) / 90f) % 4);

        /// <summary>The compass side of the wall nearest <paramref name="model"/> on a floor
        /// <paramref name="outline"/>, for a dwelling parked at <paramref name="groundBearingDegrees"/>.</summary>
        public static Side WallSide(Vector2[] outline, Vector2 model, int groundBearingDegrees) =>
            SideOf(CompassOf(OutwardNormalNearest(outline, model), groundBearingDegrees));

        static float NearestArc(Vector2[] outline, float[] arc, Vector2 p)
        {
            int n = outline.Length;
            float bestD = float.MaxValue, bestArc = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % n];
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                float d = Vector2.Distance(p, a + ab * t);
                if (d < bestD) { bestD = d; bestArc = arc[i] + t * Mathf.Sqrt(len2); }
            }
            return bestArc;
        }

        static Vector2 PointAt(Vector2[] outline, float[] arc, float perimeter, float s)
        {
            int n = outline.Length;
            s = Mathf.Repeat(s, perimeter);
            for (int i = n - 1; i >= 0; i--)
            {
                if (s < arc[i]) continue;
                Vector2 a = outline[i], b = outline[(i + 1) % n];
                float len = Vector2.Distance(a, b);
                return len > 0f ? Vector2.Lerp(a, b, (s - arc[i]) / len) : a;
            }
            return outline[0];
        }
    }
}
