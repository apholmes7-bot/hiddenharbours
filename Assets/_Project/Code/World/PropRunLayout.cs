using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// How a kit lays a run of pieces along a key scene's line: bays tiled along each segment, a post at each end
    /// and at each sharp turn, an open gate in a short gap, a bend where a floor run turns, and the walk-blocking
    /// band along a fence. Plain values in, plain values out: the scene step writes what this returns.
    ///
    /// <para>Positions are world (screen) metres; lengths and turns are measured on the GROUND
    /// (<see cref="IsoGround.ToGround"/>), where the bays were drawn.</para>
    /// </summary>
    public static class PropRunLayout
    {
        /// <summary>Two segment ends this close (world metres) are one vertex: the run goes on.</summary>
        public const float JoinMetres = 0.02f;

        public enum Role { Bay, Alternate, Corner, End, Gate, Bend }

        /// <summary>One picture to stand: what it is, where, which way its run goes on screen, and its flips.</summary>
        public struct Part
        {
            public Role Role;
            public Vector2 At;
            public Vector2 Direction;
        }

        public sealed class Layout
        {
            public readonly List<Part> Parts = new List<Part>();
            public readonly List<Vector2[]> Walls = new List<Vector2[]>();
        }

        /// <summary>The picture index drawn nearest a screen direction, every <paramref name="stepDegrees"/> from 0
        /// (east) to 90 (north); a falling run takes the rising picture flipped in x.</summary>
        public static int DirectionIndex(Vector2 direction, float stepDegrees, int count, out bool flipX)
        {
            flipX = false;
            if (count <= 1 || !(stepDegrees > 0f)) return 0;
            Vector2 d = direction.x < 0f || (direction.x == 0f && direction.y < 0f) ? -direction : direction;
            float deg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;   // -90 .. 90
            if (deg < 0f) { flipX = true; deg = -deg; }
            return Mathf.Clamp(Mathf.RoundToInt(deg / stepDegrees), 0, count - 1);
        }

        /// <summary>
        /// The picture and flips for a piece along a direction. A plain run piece looks the same either way along
        /// its run; an <paramref name="oriented"/> one (a walk's end, whose stone lies beyond its far edge) points
        /// along the direction, so a run going west is its east picture turned half round (flipped in x and y).
        /// </summary>
        public static int PictureFor(Vector2 direction, float stepDegrees, int count, bool oriented, out bool flipX, out bool flipY)
        {
            int i = DirectionIndex(direction, stepDegrees, count, out flipX);
            flipY = false;
            if (oriented && (direction.x < 0f || (direction.x == 0f && direction.y < 0f))) { flipX = !flipX; flipY = true; }
            return i;
        }
        /// <summary>A world delta's length on the ground, metres.</summary>
        public static float GroundLength(Vector2 worldDelta) => IsoGround.ToGround(worldDelta).magnitude;

        /// <summary>The turn at a vertex between two world directions, degrees on the ground (0 = straight on).</summary>
        public static float GroundTurnDegrees(Vector2 a, Vector2 b) =>
            Vector2.Angle(IsoGround.ToGround(a), IsoGround.ToGround(b));

        /// <summary>The segments joined into chains: a chain goes on while each segment starts where the last ended.</summary>
        public static List<List<Vector2>> Chains(IReadOnlyList<Vector2[]> segments)
        {
            var chains = new List<List<Vector2>>();
            List<Vector2> cur = null;
            foreach (Vector2[] s in segments)
            {
                if (s == null || s.Length != 2) throw new ArgumentException("a run segment is two points.");
                if ((s[1] - s[0]).sqrMagnitude < JoinMetres * JoinMetres) continue;
                if (cur != null && (s[0] - cur[cur.Count - 1]).sqrMagnitude <= JoinMetres * JoinMetres) cur.Add(s[1]);
                else { cur = new List<Vector2> { s[0], s[1] }; chains.Add(cur); }
            }
            // A chain that ends where any other chain starts goes on into it (a fence
            // authored from its gate round to the gate again is one run with a gap, not two runs meeting).
            for (bool merged = true; merged;)
            {
                merged = false;
                for (int a = 0; a < chains.Count && !merged; a++)
                    for (int b = 0; b < chains.Count && !merged; b++)
                    {
                        if (a == b) continue;
                        List<Vector2> ca = chains[a], cb = chains[b];
                        if ((cb[0] - ca[ca.Count - 1]).sqrMagnitude > JoinMetres * JoinMetres) continue;
                        ca.AddRange(cb.GetRange(1, cb.Count - 1));
                        chains.RemoveAt(b);
                        merged = true;
                    }
            }
            return chains;
        }

        static bool Closed(List<Vector2> chain) =>
            chain.Count > 2 && (chain[0] - chain[chain.Count - 1]).sqrMagnitude <= JoinMetres * JoinMetres;

        /// <summary>
        /// A fence: each chain's segments tiled with bays of <paramref name="bayPlanMetres"/> (rounded up, so bays
        /// meet or overlap a little, never gap), every <paramref name="alternateEvery"/>th bay (counted along the
        /// whole run, from 1) the worn one; an end post at each chain's ends, a corner post where a chain turns by
        /// more than <paramref name="cornerTurnDegrees"/>; an open gate in each gap between chains no wider than
        /// <paramref name="gateGapMaxMetres"/>, hinged at the gap's first post and swung
        /// <paramref name="gateSwingDegrees"/> towards the middle of the run; and, when
        /// <paramref name="wallThicknessMetres"/> is above zero, a walk-blocking band along every segment (none
        /// across a gateway).
        /// </summary>
        public static Layout Fence(IReadOnlyList<Vector2[]> segments, float bayPlanMetres, int alternateEvery,
                                   float cornerTurnDegrees, float gateGapMaxMetres, float gatePlanMetres,
                                   float gateSwingDegrees, float wallThicknessMetres, bool hasGate)
        {
            if (!(bayPlanMetres > 0f)) throw new ArgumentException("a bay needs a length.");
            var layout = new Layout();
            List<List<Vector2>> chains = Chains(segments);
            Vector2 middle = Middle(chains);
            int bay = 0;
            for (int c = 0; c < chains.Count; c++)
            {
                List<Vector2> chain = chains[c];
                for (int i = 0; i + 1 < chain.Count; i++)
                {
                    Vector2 a = chain[i], b = chain[i + 1], d = b - a;
                    int n = Mathf.Max(1, Mathf.CeilToInt(GroundLength(d) / bayPlanMetres - 1e-3f));
                    for (int k = 0; k < n; k++)
                    {
                        bay++;
                        bool alt = alternateEvery > 0 && bay % alternateEvery == 0;
                        layout.Parts.Add(new Part { Role = alt ? Role.Alternate : Role.Bay, At = a + d * ((k + 0.5f) / n), Direction = d });
                    }
                    if (wallThicknessMetres > 0f) layout.Walls.Add(Band(a, b, wallThicknessMetres));
                    if (i > 0 && GroundTurnDegrees(chain[i] - chain[i - 1], d) > cornerTurnDegrees)
                        layout.Parts.Add(new Part { Role = Role.Corner, At = a, Direction = d });
                }
                if (Closed(chain))
                {
                    if (GroundTurnDegrees(chain[chain.Count - 1] - chain[chain.Count - 2], chain[1] - chain[0]) > cornerTurnDegrees)
                        layout.Parts.Add(new Part { Role = Role.Corner, At = chain[0], Direction = chain[1] - chain[0] });
                    continue;
                }
                layout.Parts.Add(new Part { Role = Role.End, At = chain[0], Direction = chain[1] - chain[0] });
                layout.Parts.Add(new Part { Role = Role.End, At = chain[chain.Count - 1], Direction = chain[chain.Count - 1] - chain[chain.Count - 2] });
            }
            if (hasGate)
                // A gate in each short gap from one chain's end to a chain's start (its own, when the fence goes
                // round to its gateway), one gate per end.
                foreach (List<Vector2> from in chains)
                {
                    if (Closed(from)) continue;
                    Vector2 hinge = from[from.Count - 1];
                    foreach (List<Vector2> to in chains)
                    {
                        if (Closed(to)) continue;
                        float gap = GroundLength(to[0] - hinge);
                        if (gap <= JoinMetres || gap > gateGapMaxMetres) continue;
                        layout.Parts.Add(Gate(hinge, to[0], middle, gatePlanMetres, gateSwingDegrees));
                        break;
                    }
                }
            return layout;
        }

        /// <summary>
        /// A floor run (a plank walk): each segment laid with pieces of <paramref name="piecePlanMetres"/> (rounded
        /// up), a bend piece where the run turns by more than <paramref name="bendTurnDegrees"/>, and an end piece at
        /// each end of each chain, turned to face out of the run (its far edge is the run's end).
        /// </summary>
        public static Layout Walk(IReadOnlyList<Vector2[]> segments, float piecePlanMetres, float endPlanMetres,
                                  float bendTurnDegrees, bool hasBend, bool hasEnd)
        {
            if (!(piecePlanMetres > 0f)) throw new ArgumentException("a walk piece needs a length.");
            var layout = new Layout();
            foreach (List<Vector2> chain in Chains(segments))
            {
                Vector2 start = chain[0], end = chain[chain.Count - 1];
                if (hasEnd)
                {
                    // The end pieces stand inside the run, their outer edges on its ends.
                    Vector2 d0 = chain[1] - chain[0], d1 = end - chain[chain.Count - 2];
                    start += d0 * Fraction(d0, endPlanMetres);
                    end -= d1 * Fraction(d1, endPlanMetres);
                    layout.Parts.Add(new Part { Role = Role.End, At = chain[0] + d0 * Fraction(d0, endPlanMetres * 0.5f), Direction = -d0 });
                    layout.Parts.Add(new Part { Role = Role.End, At = chain[chain.Count - 1] - d1 * Fraction(d1, endPlanMetres * 0.5f), Direction = d1 });
                }
                var pts = new List<Vector2>(chain) { [0] = start, [chain.Count - 1] = end };
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1], d = b - a;
                    int n = Mathf.Max(1, Mathf.CeilToInt(GroundLength(d) / piecePlanMetres - 1e-3f));
                    for (int k = 0; k < n; k++) layout.Parts.Add(new Part { Role = Role.Bay, At = a + d * ((k + 0.5f) / n), Direction = d });
                    if (hasBend && i > 0 && GroundTurnDegrees(pts[i] - pts[i - 1], d) > bendTurnDegrees)
                        layout.Parts.Add(new Part { Role = Role.Bend, At = a, Direction = (pts[i] - pts[i - 1]).normalized + d.normalized });
                }
            }
            return layout;
        }

        /// <summary>The fraction of a segment a ground length is (clamped to the segment).</summary>
        static float Fraction(Vector2 d, float groundMetres)
        {
            float len = GroundLength(d);
            return len > 0f ? Mathf.Clamp01(groundMetres / len) : 0f;
        }

        static Part Gate(Vector2 hinge, Vector2 latch, Vector2 middle, float leafPlanMetres, float swingDegrees)
        {
            Vector2 g = IsoGround.ToGround(latch - hinge).normalized;
            Vector2 toMiddle = IsoGround.ToGround(middle - hinge);
            float sign = g.x * toMiddle.y - g.y * toMiddle.x >= 0f ? 1f : -1f;
            float r = swingDegrees * Mathf.Deg2Rad * sign;
            var leaf = new Vector2(g.x * Mathf.Cos(r) - g.y * Mathf.Sin(r), g.x * Mathf.Sin(r) + g.y * Mathf.Cos(r));
            var world = new Vector2(leaf.x, leaf.y * IsoGround.GroundDepthScale);
            return new Part { Role = Role.Gate, At = hinge + world * (leafPlanMetres * 0.5f), Direction = world };
        }

        static Vector2 Middle(List<List<Vector2>> chains)
        {
            Vector2 sum = Vector2.zero; int n = 0;
            foreach (List<Vector2> c in chains) foreach (Vector2 p in c) { sum += p; n++; }
            return n > 0 ? sum / n : Vector2.zero;
        }

        /// <summary>A band <paramref name="thickness"/> across, centred on a segment (world metres).</summary>
        public static Vector2[] Band(Vector2 a, Vector2 b, float thickness)
        {
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * (thickness * 0.5f);
            return new[] { a + n, b + n, b - n, a - n };
        }
    }
}
