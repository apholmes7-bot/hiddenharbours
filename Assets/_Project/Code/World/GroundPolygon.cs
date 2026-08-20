using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A bounded piece of ground, as a closed polygon in world XY.</b> The shape a yard, a working
    /// apron or a quay edge IS — one authored fact that placement, ground painting and collision all
    /// read, so none of them can disagree about where the boundary runs.
    ///
    /// <para>Pure and struct-shaped for the same reason <see cref="InteriorFootprint"/> is: no scene, no
    /// assets, no <c>MonoBehaviour</c>, so the arithmetic is unit-tested headless. It has to be, because
    /// a boundary in the wrong place is SILENT — the ground still paints, the fence still stands, and the
    /// only symptom is a player stopped somewhere that looks like nothing.</para>
    ///
    /// <para><b>⚠ THIS IS WORLD XY, THE SAME SPACE THE REGION BUILDERS SITE BUILDINGS IN.</b> Not the
    /// unsquashed ground plane and not screen space. A yard declared here shares its space with
    /// <c>StPetersGrass.BuildingSites</c>, <c>NineMileCreekMainland.TownLots</c> and the player's own
    /// transform, which is the only arrangement in which "keep the grass out of the yard" and "stop the
    /// player at the fence" can be the same polygon.</para>
    ///
    /// <para><b>⚠ <see cref="Contains"/> IS A RAY CROSSING, so a point exactly ON an edge is undefined</b>
    /// — it answers whichever side the arithmetic lands on. That is deliberate: this predicate is asked
    /// about tens of thousands of candidate sites per region build and may not afford a distance test.
    /// Any caller making a CLAIM about containment (a test asserting a building is inside its yard, say)
    /// wants <see cref="ContainsCircle"/>, which is unambiguous by construction.</para>
    /// </summary>
    public readonly struct GroundPolygon
    {
        readonly Vector2[] _vertices;

        /// <summary>The axis-aligned box the polygon fits in — computed once at construction, because
        /// every hot predicate here opens with it. A yard is a few tens of metres of a region hundreds
        /// of metres across, so the box rejects the overwhelming majority of asks in four compares.
        /// </summary>
        public readonly Rect Bounds;

        /// <summary>Twice the signed area, kept from construction: positive is counter-clockwise in a
        /// right-handed XY frame. Sign is the winding; magnitude is <see cref="Area"/> doubled.</summary>
        public readonly float DoubleSignedArea;

        public GroundPolygon(IReadOnlyList<Vector2> vertices)
        {
            if (vertices == null || vertices.Count < 3)
                throw new ArgumentException(
                    $"[GroundPolygon] a polygon needs at least three corners; got {vertices?.Count ?? 0}. " +
                    "A boundary with two corners is a line, and a line encloses nothing.",
                    nameof(vertices));

            var copy = new Vector2[vertices.Count];
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float twiceArea = 0f;

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 v = vertices[i];
                copy[i] = v;
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y;
                if (v.y > maxY) maxY = v.y;

                Vector2 w = vertices[(i + 1) % vertices.Count];
                twiceArea += v.x * w.y - w.x * v.y;
            }

            _vertices = copy;
            Bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            DoubleSignedArea = twiceArea;
        }

        /// <summary>The corners, in the order they were authored. Never null on a constructed polygon —
        /// the constructor refuses fewer than three.</summary>
        public IReadOnlyList<Vector2> Vertices => _vertices;

        /// <summary>How many corners — and therefore how many edges, since the polygon is closed.
        /// Zero on a <c>default</c> struct, which is the one way to hold one that never went through the
        /// constructor; <see cref="IsValid"/> is the test for that.</summary>
        public int Count => _vertices?.Length ?? 0;

        /// <summary>Did this come out of the constructor? A <c>default(GroundPolygon)</c> holds no
        /// vertices and answers false to everything — worth being able to ASK rather than crashing three
        /// calls later.</summary>
        public bool IsValid => _vertices != null && _vertices.Length >= 3;

        /// <summary>Ground enclosed, in square metres. Unsigned — see <see cref="DoubleSignedArea"/> for
        /// the winding.</summary>
        public float Area => Mathf.Abs(DoubleSignedArea) * 0.5f;

        /// <summary>Counter-clockwise winding in world XY (+X east, +Y north)?</summary>
        public bool IsCounterClockwise => DoubleSignedArea > 0f;

        /// <summary>Total edge length (m) — the metres of fence a closed run would need.</summary>
        public float Perimeter
        {
            get
            {
                if (!IsValid) return 0f;
                float total = 0f;
                for (int i = 0; i < _vertices.Length; i++)
                    total += Vector2.Distance(_vertices[i], _vertices[(i + 1) % _vertices.Length]);
                return total;
            }
        }

        /// <summary>The vertex average — NOT the area centroid. It is what a caller wants for "roughly
        /// where is this yard", and it is stated as the cheap one on purpose; nothing here needs the
        /// second moment.</summary>
        public Vector2 Centre
        {
            get
            {
                if (!IsValid) return Vector2.zero;
                Vector2 sum = Vector2.zero;
                for (int i = 0; i < _vertices.Length; i++) sum += _vertices[i];
                return sum / _vertices.Length;
            }
        }

        /// <summary>Edge <paramref name="i"/>, from corner <c>i</c> to corner <c>i + 1</c> (wrapping).
        /// </summary>
        public void Edge(int i, out Vector2 a, out Vector2 b)
        {
            a = _vertices[i];
            b = _vertices[(i + 1) % _vertices.Length];
        }

        /// <summary>
        /// Is <paramref name="p"/> inside? Crossing-number ray cast along +X, opened by the bounding box.
        ///
        /// <para>⚠ Undefined exactly ON an edge — see the type remarks. Cheap on purpose.</para>
        /// </summary>
        public bool Contains(Vector2 p)
        {
            if (!IsValid) return false;
            if (p.x < Bounds.xMin || p.x > Bounds.xMax || p.y < Bounds.yMin || p.y > Bounds.yMax)
                return false;

            bool inside = false;
            for (int i = 0, j = _vertices.Length - 1; i < _vertices.Length; j = i++)
            {
                Vector2 a = _vertices[i], b = _vertices[j];
                if ((a.y > p.y) == (b.y > p.y)) continue;              // edge does not straddle the ray
                float t = (p.y - a.y) / (b.y - a.y);
                if (p.x < a.x + t * (b.x - a.x)) inside = !inside;
            }
            return inside;
        }

        /// <summary>Metres from <paramref name="p"/> to the nearest point ON the boundary — the same
        /// answer inside and out, so a caller wanting a signed depth combines it with
        /// <see cref="Contains"/>.</summary>
        public float DistanceToEdge(Vector2 p)
        {
            if (!IsValid) return float.MaxValue;
            float best = float.MaxValue;
            for (int i = 0; i < _vertices.Length; i++)
            {
                Edge(i, out Vector2 a, out Vector2 b);
                float d = DistanceToSegment(p, a, b);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Does the whole disc of radius <paramref name="radius"/> at <paramref name="centre"/> lie
        /// inside? The unambiguous containment question, and the one a test asking <i>"does this yard
        /// hold its house"</i> means: a building reserves a footprint disc, not a point.
        /// </summary>
        public bool ContainsCircle(Vector2 centre, float radius) =>
            Contains(centre) && DistanceToEdge(centre) >= radius;

        /// <summary>
        /// The boundary as one closed run of points (first corner repeated at the end) — what a fence
        /// line or a wall builder walks when there are no gaps in it.
        /// </summary>
        public Vector2[] ClosedRun()
        {
            var run = new Vector2[Count + 1];
            for (int i = 0; i < Count; i++) run[i] = _vertices[i];
            run[Count] = _vertices[0];
            return run;
        }

        /// <summary>
        /// <b>The boundary broken into OPEN runs, with a gap cut at each of <paramref name="gapCentres"/>.
        /// </b> A gate is a hole in a fence and a hole in the wall behind it — one gap list, both
        /// consumers, so a gate you can see is a gate you can walk through.
        ///
        /// <para>A gap is cut where the boundary passes within <c>gapWidth / 2</c> of a gap centre,
        /// measured ALONG the perimeter rather than through the air, so a gate near a corner cuts one
        /// opening rather than nicking two edges. With no gaps the result is the single closed run
        /// <see cref="ClosedRun"/> returns.</para>
        ///
        /// <para>⚠ Corners are preserved: the walk emits every vertex it passes, so a run that turns a
        /// corner is one polyline with a bend in it, not two runs. A wall builder that wants straight
        /// quads splits on the bends itself — it knows its own thickness and this does not.</para>
        /// </summary>
        public List<Vector2[]> PerimeterRuns(IReadOnlyList<Vector2> gapCentres, float gapWidth)
        {
            var runs = new List<Vector2[]>();
            if (!IsValid) return runs;

            bool anyGap = gapCentres != null && gapCentres.Count > 0 && gapWidth > 0f;
            if (!anyGap)
            {
                runs.Add(ClosedRun());
                return runs;
            }

            float perimeter = Perimeter;
            float half = gapWidth * 0.5f;

            var gapAt = new List<float>(gapCentres.Count);
            for (int g = 0; g < gapCentres.Count; g++)
            {
                float s = ArcLengthOfNearestPoint(gapCentres[g]);
                if (s >= 0f) gapAt.Add(s);
            }
            if (gapAt.Count == 0)
            {
                runs.Add(ClosedRun());
                return runs;
            }

            // Walk the closed boundary, emitting points while outside every gap. The subdivision is fine
            // enough that a gap edge lands within a few centimetres of where it was asked for — the
            // fence's own segment length is far coarser, so nothing downstream sees the quantisation.
            // ⚠ THE WALK IS FINE-GRAINED BUT THE OUTPUT IS NOT. Every step is tested against the gaps;
            // only three kinds of point are EMITTED — a polygon corner, the point where a run leaves a
            // gate, and the point where it enters one. Everything between is collinear filler, and a
            // fence built from a point every 10 cm is a hundred one-decimetre panels.
            const float WalkStep = 0.1f;
            var current = new List<Vector2>();
            float travelled = 0f;

            // Start as though inside a gap, so whatever the first open point is, it opens a run — corner
            // 0 when it is clear, the gate's far lip when a gate straddles it.
            bool prevInGap = true;
            Vector2 lastOpen = Vector2.zero;

            for (int i = 0; i < _vertices.Length; i++)
            {
                Edge(i, out Vector2 a, out Vector2 b);
                float len = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / WalkStep));

                for (int s = 0; s <= steps; s++)
                {
                    if (i > 0 && s == 0) continue;            // corner already walked as the last edge's end
                    float f = (float)s / steps;
                    Vector2 p = Vector2.Lerp(a, b, f);
                    float here = travelled + len * f;

                    bool inGap = false;
                    for (int g = 0; g < gapAt.Count; g++)
                        if (ArcDistance(here, gapAt[g], perimeter) < half) { inGap = true; break; }

                    if (inGap)
                    {
                        // Entering a gate: the run ends at the last point that was still outside it —
                        // which is usually filler, and would otherwise never be emitted at all.
                        if (!prevInGap)
                        {
                            AppendIfNew(current, lastOpen);
                            if (current.Count >= 2) runs.Add(current.ToArray());
                            current = new List<Vector2>();
                        }
                    }
                    else
                    {
                        bool isCorner = s == 0 || s == steps;
                        if (prevInGap || current.Count == 0 || isCorner) AppendIfNew(current, p);
                        lastOpen = p;
                    }

                    prevInGap = inGap;
                }
                travelled += len;
            }

            if (!prevInGap) AppendIfNew(current, lastOpen);
            if (current.Count >= 2) runs.Add(current.ToArray());

            // The walk started at corner 0. If that corner was NOT in a gap, its run and the final run
            // are one physical stretch of fence split by where the walk happened to begin — join them, or
            // a boundary with a single gate comes back as two runs with a seam at corner 0.
            if (runs.Count >= 2)
            {
                Vector2[] first = runs[0], last = runs[runs.Count - 1];
                if (Vector2.Distance(last[last.Length - 1], first[0]) < 1e-3f)
                {
                    var joined = new List<Vector2>(last);
                    for (int i = 1; i < first.Length; i++) joined.Add(first[i]);
                    runs.RemoveAt(runs.Count - 1);
                    runs[0] = joined.ToArray();
                }
            }

            return runs;
        }

        /// <summary>Append unless it would repeat the point already there. A run that carries the same
        /// point twice becomes a zero-length wall segment, which builds nothing and reads as a gap.
        /// </summary>
        static void AppendIfNew(List<Vector2> list, Vector2 p)
        {
            if (list.Count > 0 && Vector2.Distance(list[list.Count - 1], p) < 1e-4f) return;
            list.Add(p);
        }

        /// <summary>How far along the perimeter the boundary's nearest point to <paramref name="p"/>
        /// lies, or −1 if this polygon holds nothing.</summary>
        public float ArcLengthOfNearestPoint(Vector2 p)
        {
            if (!IsValid) return -1f;
            float best = float.MaxValue, bestArc = 0f, travelled = 0f;
            for (int i = 0; i < _vertices.Length; i++)
            {
                Edge(i, out Vector2 a, out Vector2 b);
                float len = Vector2.Distance(a, b);
                float t = len <= 0f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (len * len));
                float d = Vector2.Distance(p, Vector2.Lerp(a, b, t));
                if (d < best) { best = d; bestArc = travelled + len * t; }
                travelled += len;
            }
            return bestArc;
        }

        /// <summary>Shortest way round a closed loop of length <paramref name="loop"/> between two arc
        /// lengths — so a gap straddling corner 0 is one gap, not two half ones.</summary>
        public static float ArcDistance(float a, float b, float loop)
        {
            float d = Mathf.Abs(a - b);
            return loop <= 0f ? d : Mathf.Min(d, loop - d);
        }

        /// <summary>
        /// <b>A rectangle of ground that FACES something</b> — a house's dooryard facing the road, a
        /// working apron facing the quay. The orientation is derived from the thing it faces rather than
        /// authored as an angle, because an angle is a magic number that stops agreeing with the world
        /// the moment the road moves (rule 6).
        /// </summary>
        /// <param name="centre">Centre of the rectangle in world XY.</param>
        /// <param name="sizeMetres">Width ACROSS the front × depth from front to back.</param>
        /// <param name="frontToward">A point the front edge faces — the road, the green, the path.
        /// Degenerate input (a point at the centre) falls back to facing +Y.</param>
        public static GroundPolygon Rectangle(Vector2 centre, Vector2 sizeMetres, Vector2 frontToward)
        {
            Vector2 forward = frontToward - centre;
            forward = forward.sqrMagnitude < 1e-6f ? Vector2.up : forward.normalized;
            Vector2 across = new Vector2(forward.y, -forward.x);

            float halfW = sizeMetres.x * 0.5f, halfD = sizeMetres.y * 0.5f;
            return new GroundPolygon(new[]
            {
                centre - across * halfW - forward * halfD,
                centre + across * halfW - forward * halfD,
                centre + across * halfW + forward * halfD,
                centre - across * halfW + forward * halfD,
            });
        }

        /// <summary>Metres from a point to a segment. Local because the World module owes nothing to the
        /// region builders that carry the other copy of this arithmetic.</summary>
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq <= 1e-12f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return Vector2.Distance(p, a + ab * t);
        }

        public override string ToString() =>
            IsValid
                ? $"GroundPolygon({Count} corners, {Area:0.0} m2, {Perimeter:0.0} m round)"
                : "GroundPolygon(empty)";
    }
}
