using UnityEngine;
using HiddenHarbours.Core;   // IsoGround — world XY is the squashed ground plane

namespace HiddenHarbours.World
{
    /// <summary>
    /// The PURE lane maths: <b>which way you walk from here to there, and where you are along it</b>.
    /// Split out (the <c>AmbientFleetSchedule</c> / <c>AmbientFleetPlan</c> pattern) so every rule here is
    /// EditMode-testable headless — no engine state, no <c>Time</c>, no RNG, no allocation beyond the
    /// buffer the caller hands in.
    ///
    /// <para><b>⭐ THE LANE NETWORK IS A TREE, AND THAT IS A DESIGN DECISION, NOT A SIMPLIFICATION.</b>
    /// A village's lanes radiate from where its life is: at St Peters, the green is the root, the
    /// east–west lane and the two painted dirt paths hang off it, and every dooryard hangs off one of
    /// those. In a tree there is EXACTLY ONE path between any two nodes, so:</para>
    /// <list type="bullet">
    ///   <item>there is nothing to search — walk up to the common ancestor and back down — so no Dijkstra,
    ///   no priority queue, no allocation, and no tie-break to be non-deterministic about;</item>
    ///   <item>a route cannot silently change when an unrelated lane is added, which a shortest-path
    ///   solver's route can;</item>
    ///   <item>"is this network sane?" is one checkable property (<see cref="IsTree"/>) instead of a
    ///   judgement.</item>
    /// </list>
    /// <para>The day a village genuinely needs a loop — a lane that comes back on itself, where two routes
    /// are equally good — this becomes a graph and gains a tie-break rule. Until then a loop would only
    /// be a chance for two runs to disagree about which way somebody walked.</para>
    ///
    /// <para><b>Edges carry their own shape.</b> An edge may be a straight line or a polyline: the two
    /// painted dirt paths on St Peters bend, and a villager who cut the corner would be walking beside the
    /// path the ground draws. The bend points live on the CHILD end of each edge in child→parent order,
    /// and are reversed when the walk goes the other way — see <see cref="WritePolyline"/>.</para>
    /// </summary>
    public static class RoutineLaneTree
    {
        /// <summary>Sentinel parent for the root.</summary>
        public const int NoParent = -1;

        /// <summary>
        /// Is <paramref name="parent"/> a well-formed tree — exactly one root, every node reaching it, no
        /// cycles? Reports the first offending node in <paramref name="offender"/> (−1 when valid) so a
        /// builder can name it rather than say "the graph is bad".
        ///
        /// <para>Cheap and total: it walks up from every node with a step budget of the node count, which
        /// no acyclic chain can exceed and every cycle does.</para>
        /// </summary>
        public static bool IsTree(int[] parent, out int offender)
        {
            offender = -1;
            if (parent == null || parent.Length == 0) return false;

            int roots = 0;
            for (int i = 0; i < parent.Length; i++)
            {
                int p = parent[i];
                if (p == NoParent) { roots++; continue; }
                if (p < 0 || p >= parent.Length || p == i) { offender = i; return false; }
            }
            if (roots != 1) { offender = roots == 0 ? 0 : NoParent; return false; }

            for (int i = 0; i < parent.Length; i++)
            {
                int steps = 0, n = i;
                while (parent[n] != NoParent)
                {
                    n = parent[n];
                    if (++steps > parent.Length) { offender = i; return false; }
                }
            }
            return true;
        }

        /// <summary>How many edges from <paramref name="node"/> up to the root. −1 if the walk runs away
        /// (a malformed table), so callers never spin.</summary>
        public static int Depth(int[] parent, int node)
        {
            if (parent == null || node < 0 || node >= parent.Length) return -1;
            int d = 0, n = node;
            while (parent[n] != NoParent)
            {
                n = parent[n];
                if (n < 0 || n >= parent.Length || ++d > parent.Length) return -1;
            }
            return d;
        }

        /// <summary>The lowest node that is an ancestor of (or equal to) both — the point the walk turns
        /// around at. −1 on a malformed table.</summary>
        public static int CommonAncestor(int[] parent, int a, int b)
        {
            int da = Depth(parent, a), db = Depth(parent, b);
            if (da < 0 || db < 0) return -1;
            while (da > db) { a = parent[a]; da--; }
            while (db > da) { b = parent[b]; db--; }
            while (a != b)
            {
                a = parent[a];
                b = parent[b];
                if (a < 0 || b < 0) return -1;
            }
            return a;
        }

        /// <summary>
        /// The node path from <paramref name="from"/> to <paramref name="to"/> inclusive, written into
        /// <paramref name="into"/>; returns how many nodes were written, or −1 if the buffer is too small
        /// or the table is malformed. Up to the common ancestor and back down, which in a tree is the
        /// whole answer.
        /// </summary>
        public static int WriteNodePath(int[] parent, int from, int to, int[] into)
        {
            if (into == null) return -1;
            int lca = CommonAncestor(parent, from, to);
            if (lca < 0) return -1;

            int n = 0;
            for (int x = from; x != lca; x = parent[x])
            {
                if (n >= into.Length) return -1;
                into[n++] = x;
            }
            if (n >= into.Length) return -1;
            into[n++] = lca;

            // The downward half, written backwards then reversed in place — the tree only knows parents.
            int tailStart = n;
            for (int x = to; x != lca; x = parent[x])
            {
                if (n >= into.Length) return -1;
                into[n++] = x;
            }
            for (int i = tailStart, j = n - 1; i < j; i++, j--)
                (into[i], into[j]) = (into[j], into[i]);

            return n;
        }

        /// <summary>
        /// Expand a node path into the walked polyline: each node's position, with every edge's bend points
        /// laid in between (reversed when the walk climbs from child to parent... see below). Written into
        /// <paramref name="into"/>; returns the point count, or −1 if the buffer is too small.
        ///
        /// <para><b>Which way the bends go.</b> An edge's bend points are stored on the CHILD in
        /// child→parent order. Walking from <paramref name="nodePath"/>[i] to [i+1]: if [i] is the child,
        /// the stored order is the walking order; if [i] is the parent, they are laid in reverse. Getting
        /// this backwards is the one error here that is invisible in a unit test that only measures length
        /// — the walk would be the right distance along the mirror image of the path — which is why
        /// <c>RoutineLaneTreeTests</c> asserts the actual point sequence.</para>
        /// </summary>
        public static int WritePolyline(int[] parent, Vector2[] nodePos,
                                        int[] viaStart, int[] viaCount, Vector2[] via,
                                        int[] nodePath, int nodePathCount, Vector2[] into)
        {
            if (into == null || nodePos == null || nodePath == null || nodePathCount <= 0) return -1;

            int n = 0;
            if (!Push(into, ref n, nodePos[nodePath[0]])) return -1;

            for (int i = 0; i + 1 < nodePathCount; i++)
            {
                int a = nodePath[i], b = nodePath[i + 1];
                bool aIsChild = parent != null && a < parent.Length && parent[a] == b;
                int child = aIsChild ? a : b;

                int start = viaStart != null && child < viaStart.Length ? viaStart[child] : 0;
                int count = viaCount != null && child < viaCount.Length ? viaCount[child] : 0;
                if (count > 0 && via != null)
                {
                    for (int k = 0; k < count; k++)
                    {
                        // child→parent order as stored; reversed when we are walking parent→child.
                        int idx = aIsChild ? start + k : start + count - 1 - k;
                        if (idx < 0 || idx >= via.Length) return -1;
                        if (!Push(into, ref n, via[idx])) return -1;
                    }
                }

                if (!Push(into, ref n, nodePos[b])) return -1;
            }
            return n;
        }

        /// <summary>Append unless it repeats the last point — two stations on one node, or a dooryard that
        /// IS its lane node, would otherwise leave a zero-length hop in the middle of a walk, and a
        /// zero-length segment is a division waiting to happen in <see cref="PointAlong"/>.</summary>
        static bool Push(Vector2[] into, ref int n, Vector2 p)
        {
            const float SameSq = 1e-6f;   // 1 mm², i.e. the same spot
            if (n > 0 && (into[n - 1] - p).sqrMagnitude <= SameSq) return true;
            if (n >= into.Length) return false;
            into[n++] = p;
            return true;
        }

        /// <summary>Total walked length of a polyline slice, in metres. 0 for fewer than two points.</summary>
        public static float PolylineLength(Vector2[] points, int start, int count)
            => Polyline.Length(points, start, count);

        /// <summary>
        /// The point <paramref name="distance"/> metres along a polyline slice — the whole of "where is she
        /// on this walk". Clamped at both ends: before the start you are at the start, past the end you are
        /// at the end (which is what standing at the destination IS, so arrival needs no separate branch).
        ///
        /// <para>Allocation-free and total: a zero-length segment is skipped rather than divided by, so a
        /// polyline with a repeated point cannot produce a NaN position.</para>
        /// </summary>
        public static Vector2 PointAlong(Vector2[] points, int start, int count, float distance)
            => Polyline.PointAlong(points, start, count, distance);

        /// <summary>
        /// The compass heading (degrees, 0 = North, clockwise) the walk is travelling at
        /// <paramref name="distance"/> metres along the slice — the direction of the segment you are ON.
        ///
        /// <para>Taken from the segment rather than from frame-to-frame motion because it has to be right
        /// at the FIRST frame of a leg too: a villager who spawns mid-stride on region load has no previous
        /// position to difference, and one drawn facing North on her first frame would visibly snap round.
        /// Returns <paramref name="fallback"/> for a degenerate slice.</para>
        ///
        /// <para><b>A GROUND bearing</b> (<see cref="IsoGround.BearingDegrees(Vector2)"/>), because the
        /// waypoints are world positions and world XY is the squashed ground plane, while the facing row it
        /// picks depicts a ground bearing. It must also be the same arithmetic
        /// <see cref="IsoCharacterMath.GroundHeadingFor"/> applies to the measured step, or a stated
        /// heading and a walked one would disagree by up to 12.56° and the villager would turn on
        /// arrival — the very seam this function exists to close.</para>
        /// </summary>
        public static float HeadingAlong(Vector2[] points, int start, int count, float distance,
                                        float fallback)
        {
            Vector2 tangent = Polyline.TangentAlong(points, start, count, distance);
            // ⭐ A GROUND bearing, and the un-squash is THIS caller's, not the primitive's. Polyline hands
            // back a raw direction precisely so that a walker states her own convention here and a vehicle
            // states hers (world-XY, transform.up) in Vehicles — the two differ by up to 12.5° and a body
            // posed in the other one's is visibly crabbed. See Polyline's class note.
            return tangent == Vector2.zero ? fallback : IsoGround.BearingDegrees(tangent);
        }
    }
}
