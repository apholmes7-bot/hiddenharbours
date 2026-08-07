using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// One authored stretch of a MAINLAND coast: the class, and the distance ALONG the coast run it
    /// STARTS at. A sector runs to the next entry's distance, so a plan cannot be authored with a gap or
    /// an overlap — the same guarantee <see cref="CoastSector"/> gives an island, expressed in metres
    /// along an open run instead of degrees around a closed ring.
    ///
    /// <para><b>Namespace-level, deliberately not nested</b> — the same CS0426 lesson
    /// <see cref="CoastClass"/> carries. Additive members only: the ordinals serialize into every region
    /// scene that authors a plan.</para>
    /// </summary>
    [System.Serializable]
    public struct CoastRunSector
    {
        [Tooltip("Distance (m) ALONG the coast run this sector starts at, measured from the run's first " +
                 "authored point. The sector runs to the next entry's distance (or to the end of the run).")]
        public float FromMetres;

        [Tooltip("What this stretch of coast is.")]
        public CoastClass Class;

        public CoastRunSector(float fromMetres, CoastClass cls)
        {
            FromMetres = fromMetres;
            Class = cls;
        }
    }

    /// <summary>
    /// The pure geometry of a <b>mainland</b> coast — a coastline authored as an OPEN POLYLINE rather
    /// than as a ring around an island, with the same sector vocabulary, the same aspect law, and the
    /// same feathered joins.
    ///
    /// <para><b>Why this exists at all, next to <see cref="CoastPlan"/>.</b> <see cref="CoastPlan"/>
    /// measures a coast as a BEARING from an island centre and reads the aspect law off the outward
    /// normal of an ELLIPSE. A mainland has no centre and no ellipse: it has a shoreline that runs from
    /// one edge of the region to the other, water on one side and fields on the other, and its outward
    /// normal is a property of the line, not of a radius. Forcing St Peters' model onto it would mean
    /// authoring a 10 km ellipse to fake a 600 m run — and, worse, would have to touch
    /// <see cref="CoastPlan"/>, which St Peters sits 0.02° inside the aspect law of. So the mainland gets
    /// its own plan, as the handoff requires, and the two share only the vocabulary
    /// (<see cref="CoastClass"/>) and the law (<see cref="CoastPlan.AspectSnapError"/>) — which is
    /// exactly the pair that must never diverge.</para>
    ///
    /// <para><b>Engine-light and total.</b> Every method is a pure function of its arguments (no scene,
    /// no RNG, no time), so the whole plan is EditMode-assertable without loading anything and the coast
    /// is the same on every machine and every rebuild (CLAUDE.md rule 5).</para>
    ///
    /// <para><b>⚠ THE SEAWARD SIDE IS THE RIGHT HAND.</b> Walking the polyline in its authored direction
    /// (first point → last), the SEA is on the right and the LAND is on the left.
    /// <see cref="Project"/> returns a signed offset that is POSITIVE seaward, so "how far out to sea am
    /// I" and "how far inland am I" are one number with a sign, the way
    /// <c>TidalTerrain.IslandDistance</c> is one distance from a centre. Nine Mile Creek's coast is
    /// authored SOUTH → NORTH with the water EAST, which puts the sea on the right.</para>
    /// </summary>
    public static class MainlandCoast
    {
        // ---- the run's own arithmetic ---------------------------------------------------------------

        /// <summary>True if the polyline describes a real run (at least one segment).</summary>
        public static bool IsRun(Vector2[] points) => points != null && points.Length >= 2;

        /// <summary>Number of segments in the run.</summary>
        public static int SegmentCount(Vector2[] points) => IsRun(points) ? points.Length - 1 : 0;

        /// <summary>Total length (m) of the coast run.</summary>
        public static float RunLength(Vector2[] points)
        {
            if (!IsRun(points)) return 0f;
            float total = 0f;
            for (int i = 0; i < points.Length - 1; i++) total += Vector2.Distance(points[i], points[i + 1]);
            return total;
        }

        /// <summary>Distance (m) along the run at which segment <paramref name="index"/> starts.</summary>
        public static float SegmentStart(Vector2[] points, int index)
        {
            if (!IsRun(points)) return 0f;
            int end = Mathf.Clamp(index, 0, points.Length - 1);
            float total = 0f;
            for (int i = 0; i < end; i++) total += Vector2.Distance(points[i], points[i + 1]);
            return total;
        }

        /// <summary>Length (m) of segment <paramref name="index"/>.</summary>
        public static float SegmentLength(Vector2[] points, int index)
        {
            if (!IsRun(points) || index < 0 || index >= points.Length - 1) return 0f;
            return Vector2.Distance(points[index], points[index + 1]);
        }

        /// <summary>Index of the segment a distance-along falls in, clamped to the run's ends.</summary>
        public static int SegmentIndexAt(Vector2[] points, float along)
        {
            if (!IsRun(points)) return -1;
            float travelled = 0f;
            for (int i = 0; i < points.Length - 1; i++)
            {
                float len = Vector2.Distance(points[i], points[i + 1]);
                if (along < travelled + len || i == points.Length - 2) return i;
                travelled += len;
            }
            return points.Length - 2;
        }

        /// <summary>The world position a distance-along the run, clamped to the run's ends.</summary>
        public static Vector2 PositionAt(Vector2[] points, float along)
        {
            if (!IsRun(points)) return Vector2.zero;
            int i = SegmentIndexAt(points, along);
            float start = SegmentStart(points, i);
            float len = SegmentLength(points, i);
            float t = len <= 1e-6f ? 0f : Mathf.Clamp01((along - start) / len);
            return Vector2.Lerp(points[i], points[i + 1], t);
        }

        // ---- the aspect law, measured on the real normal ---------------------------------------------

        /// <summary>
        /// Compass azimuth (degrees, N = 0, clockwise) of the SEAWARD normal of segment
        /// <paramref name="index"/> — the direction a cliff standing there would FACE, and therefore the
        /// only angle the aspect law may be tested against (the same rule
        /// <see cref="CoastPlan.OutwardNormalAzimuth"/> spells out for an island).
        ///
        /// <para>The seaward side is the RIGHT hand of the authored walk direction, so for a segment
        /// heading <c>d</c> the outward normal is <c>(d.y, −d.x)</c>: a coast running due north with the
        /// sea to the east faces 90°, dead on the kit's E aspect.</para>
        /// </summary>
        public static float OutwardNormalAzimuth(Vector2[] points, int index)
        {
            if (!IsRun(points) || index < 0 || index >= points.Length - 1) return 0f;
            Vector2 d = points[index + 1] - points[index];
            if (d.sqrMagnitude < 1e-12f) return 0f;
            Vector2 n = new Vector2(d.y, -d.x);
            return CoastPlan.Wrap360(Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg);
        }

        /// <summary>The seaward normal's azimuth at a distance-along the run.</summary>
        public static float OutwardNormalAzimuthAt(Vector2[] points, float along) =>
            OutwardNormalAzimuth(points, SegmentIndexAt(points, along));

        /// <summary>True where a cliff may honestly stand on segment <paramref name="index"/>: its
        /// seaward normal snaps to one of the kit's five authored facings within tolerance.</summary>
        public static bool CliffIsLegalOn(Vector2[] points, int index) =>
            CoastPlan.AspectSnapError(OutwardNormalAzimuth(points, index))
                <= CoastPlan.AspectSnapToleranceDegrees + 1e-3f;

        /// <summary>
        /// The WORST aspect-snap error (degrees) over every segment the span
        /// <c>[fromMetres, toMetres)</c> touches — the number a cliff sector must satisfy.
        ///
        /// <para><b>⚠ Per SEGMENT, not per sample.</b> A polyline's normal is constant along a segment and
        /// discontinuous at a vertex, so sampling the span at intervals can walk straight past a short
        /// segment that turns the coast through an illegal facing. Iterating the segments cannot.</para>
        /// </summary>
        public static float WorstAspectError(Vector2[] points, float fromMetres, float toMetres)
        {
            if (!IsRun(points)) return 180f;
            float lo = Mathf.Min(fromMetres, toMetres);
            float hi = Mathf.Max(fromMetres, toMetres);
            float worst = 0f;
            float travelled = 0f;
            for (int i = 0; i < points.Length - 1; i++)
            {
                float len = Vector2.Distance(points[i], points[i + 1]);
                float segLo = travelled;
                float segHi = travelled + len;
                travelled = segHi;
                if (segHi <= lo || segLo >= hi) continue;                  // no overlap
                float e = CoastPlan.AspectSnapError(OutwardNormalAzimuth(points, i));
                if (e > worst) worst = e;
            }
            return worst;
        }

        /// <summary>Metres of coast a cliff MAY stand on — the aspect law's own ceiling for this
        /// coastline, independent of what the plan actually authors.</summary>
        public static float CliffLegalLength(Vector2[] points)
        {
            if (!IsRun(points)) return 0f;
            float total = 0f;
            for (int i = 0; i < points.Length - 1; i++)
                if (CliffIsLegalOn(points, i)) total += Vector2.Distance(points[i], points[i + 1]);
            return total;
        }

        // ---- the sector ring, opened out into a run --------------------------------------------------

        /// <summary>
        /// Index of the sector a distance-along falls in. Sectors are ordered by
        /// <see cref="CoastRunSector.FromMetres"/> and the LAST one runs to the end of the coast — an
        /// OPEN run has no wrap, which is the one structural difference from
        /// <see cref="CoastPlan.SectorIndexAt"/>. Returns −1 only for an empty plan (the caller's cue to
        /// fall back to the soft profile).
        /// </summary>
        public static int SectorIndexAt(CoastRunSector[] sectors, float along)
        {
            if (sectors == null || sectors.Length == 0) return -1;
            int found = 0;
            for (int i = 0; i < sectors.Length; i++)
                if (along >= sectors[i].FromMetres) found = i;
            return found;
        }

        /// <summary>The class standing at a distance-along, unfeathered — what the plan SAYS, before the
        /// blend softens the joins. The decider the cliff-share measure, the paint and the decor read.</summary>
        public static CoastClass ClassAt(CoastRunSector[] sectors, float along)
        {
            int i = SectorIndexAt(sectors, along);
            return i < 0 ? CoastClass.Beach : sectors[i].Class;
        }

        /// <summary>Length (m) of the sector at an index — the run's own arithmetic in one place, because
        /// "how wide is this sector" is asked by the width law, the blend guard and the share measure
        /// alike. The last sector's width runs to <paramref name="runLength"/>.</summary>
        public static float SectorLength(CoastRunSector[] sectors, int index, float runLength)
        {
            if (sectors == null || sectors.Length == 0) return 0f;
            int i = Mathf.Clamp(index, 0, sectors.Length - 1);
            float from = sectors[i].FromMetres;
            float to = i + 1 < sectors.Length ? sectors[i + 1].FromMetres : runLength;
            return Mathf.Max(0f, to - from);
        }

        /// <summary>Metres of coast the plan authors as one of the three rock classes.</summary>
        public static float CliffLength(CoastRunSector[] sectors, float runLength)
        {
            if (sectors == null || sectors.Length == 0) return 0f;
            float total = 0f;
            for (int i = 0; i < sectors.Length; i++)
                if (CoastPlan.IsCliff(sectors[i].Class)) total += SectorLength(sectors, i, runLength);
            return total;
        }

        /// <summary>
        /// The two classes whose height profiles meet at a distance-along, and how much of the way it is
        /// to the PRIMARY one (0.5 exactly on a join, 1 in a sector's interior).
        ///
        /// <para>Same reason as <see cref="CoastPlan.BlendAt"/>: a cliff sector's profile drops six metres
        /// in three, a beach sector's takes twenty-four metres to do a seventh of it, and butted hard that
        /// join is a wall running out to sea rather than a headland. The window is a LENGTH here rather
        /// than an angle, because a run has no centre to measure degrees from — which also means it is
        /// the same width of blend everywhere, instead of narrowing at whichever end of an ellipse is
        /// fat.</para>
        ///
        /// <para><b>The run's two ENDS do not blend.</b> A ring's first and last sector are neighbours; a
        /// run's are not, and pretending otherwise would feather the region's north edge into its south
        /// one. At the ends the sector simply holds its own profile.</para>
        /// </summary>
        public static void BlendAt(CoastRunSector[] sectors, float along, float runLength,
                                   float featherMetres,
                                   out CoastClass primary, out CoastClass secondary, out float weight)
        {
            primary = CoastClass.Beach;
            secondary = CoastClass.Beach;
            weight = 1f;
            if (sectors == null || sectors.Length == 0) return;

            int i = SectorIndexAt(sectors, along);
            primary = sectors[i].Class;
            secondary = primary;
            if (sectors.Length == 1 || featherMetres <= 0f) return;

            float from = sectors[i].FromMetres;
            float to = from + SectorLength(sectors, i, runLength);

            float toStart = along - from;
            float toEnd = to - along;
            bool nearStart = toStart <= toEnd;
            float distance = Mathf.Max(0f, nearStart ? toStart : toEnd);
            if (distance >= featherMetres) return;              // interior: this sector's profile, undiluted

            int neighbour = nearStart ? i - 1 : i + 1;
            if (neighbour < 0 || neighbour >= sectors.Length) return;   // an open end has no neighbour

            secondary = sectors[neighbour].Class;
            // 0.5 exactly ON the join (so the two sides agree there and the field is continuous), easing
            // to 1 by the full feather.
            weight = 0.5f + 0.5f * Mathf.SmoothStep(0f, 1f, distance / featherMetres);
        }

        // ---- where am I, relative to the coast --------------------------------------------------------

        /// <summary>
        /// Project a world position onto the coast run: how far ALONG the run the nearest point is, and
        /// how far to SEAWARD the position lies (negative = inland).
        ///
        /// <para><b>⚠ A concave notch is answered by its nearest EDGE.</b> Deep inside a re-entrant (the
        /// creek mouth) the nearest segment can be the far bank, so the sign there says "inland" for
        /// ground that is really the head of an inlet. That is why Nine Mile Creek authors its
        /// barachois and its basin as CARVES rather than as folds in this polyline — a coastline that
        /// folds back on itself is a coastline this projection cannot answer, and the terrain says so
        /// instead of guessing.</para>
        /// </summary>
        public static void Project(Vector2[] points, Vector2 worldPos, out float along, out float seaward)
        {
            along = 0f;
            seaward = 0f;
            if (!IsRun(points)) return;

            float bestDistance = float.MaxValue;
            float travelled = 0f;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[i + 1];
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float len = Mathf.Sqrt(len2);
                float t = len2 <= 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(worldPos - a, ab) / len2);
                Vector2 proj = a + t * ab;
                float d = Vector2.Distance(worldPos, proj);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    along = travelled + t * len;
                    // Right hand of the walk direction is the sea (see the type note).
                    Vector2 rel = worldPos - a;
                    float cross = ab.x * rel.y - ab.y * rel.x;
                    seaward = cross <= 0f ? d : -d;
                }
                travelled += len;
            }
        }
    }
}
