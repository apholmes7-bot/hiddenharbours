using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One region placed in the SHARED WORLD FRAME (ADR 0057): its stable id, where its local origin sits
    /// in the world, and its rectangle in its own local frame. Every region scene keeps building around
    /// its own origin (nothing in a scene moves); the frame only says where those local frames sit
    /// relative to each other, so that two neighbours meet edge to edge.
    ///
    /// <para>PURE value type, no scene, no Unity object, so the whole layout is EditMode-assertable.</para>
    /// </summary>
    public readonly struct RegionFrame
    {
        /// <summary>Stable region id (<c>region.st_peters</c>).</summary>
        public readonly string RegionId;
        /// <summary>World position of the region's local (0, 0).</summary>
        public readonly Vector2 Origin;
        /// <summary>The region's rectangle in its OWN local frame (RegionDef.WorldCenter ± size / 2).</summary>
        public readonly Rect LocalRect;

        public RegionFrame(string regionId, Vector2 origin, Rect localRect)
        {
            RegionId = regionId;
            Origin = origin;
            LocalRect = localRect;
        }

        /// <summary>The region's rectangle in the WORLD frame.</summary>
        public Rect WorldRect => new Rect(LocalRect.position + Origin, LocalRect.size);

        public Vector2 ToWorld(Vector2 local) => local + Origin;
        public Vector2 ToLocal(Vector2 world) => world - Origin;

        public bool IsValid => !string.IsNullOrEmpty(RegionId) && LocalRect.width > 0f && LocalRect.height > 0f;
    }

    /// <summary>Which side of a region's rectangle.</summary>
    public enum RegionEdge { West, East, South, North }

    /// <summary>A stretch of edge two regions share in the world frame: the whole stretch is one crossing.</summary>
    public readonly struct RegionSeam
    {
        public readonly string RegionA;
        public readonly string RegionB;
        /// <summary>The side of <see cref="RegionA"/> the seam lies on (B sees the opposite side).</summary>
        public readonly RegionEdge EdgeOfA;
        /// <summary>World-frame end points of the shared stretch.</summary>
        public readonly Vector2 From;
        public readonly Vector2 To;

        public RegionSeam(string a, string b, RegionEdge edgeOfA, Vector2 from, Vector2 to)
        {
            RegionA = a; RegionB = b; EdgeOfA = edgeOfA; From = from; To = to;
        }

        public float Length => Vector2.Distance(From, To);
    }

    /// <summary>What the frame says about a mover at a local position in its current region.</summary>
    public enum CrossingKind
    {
        /// <summary>Inside the region (or inside the hysteresis band past its edge): nothing happens.</summary>
        Stay,
        /// <summary>Past an edge into a neighbour: travel there, shifting by <see cref="CrossingDecision.LocalShift"/>.</summary>
        Cross,
        /// <summary>Past an edge that faces no neighbour: a world boundary; hold the mover at <see cref="CrossingDecision.ClampedLocal"/>.</summary>
        Boundary,
    }

    /// <summary>The frame's answer for one mover, one frame. PURE.</summary>
    public readonly struct CrossingDecision
    {
        public readonly CrossingKind Kind;
        /// <summary>The neighbour entered (valid only for <see cref="CrossingKind.Cross"/>).</summary>
        public readonly RegionFrame To;
        /// <summary>Add this to a local position in the region left to get the same WORLD point in
        /// <see cref="To"/>'s local frame: <c>from.Origin - to.Origin</c>. A pure translation, so distance,
        /// heading and velocity are all preserved.</summary>
        public readonly Vector2 LocalShift;
        /// <summary>The nearest point inside the region (valid for <see cref="CrossingKind.Boundary"/>).</summary>
        public readonly Vector2 ClampedLocal;
        /// <summary>The edge passed (Cross or Boundary).</summary>
        public readonly RegionEdge Edge;

        public CrossingDecision(CrossingKind kind, RegionFrame to, Vector2 shift, Vector2 clamped, RegionEdge edge)
        {
            Kind = kind; To = to; LocalShift = shift; ClampedLocal = clamped; Edge = edge;
        }

        public static readonly CrossingDecision Stay = default;
    }

    /// <summary>
    /// ⭐ <b>THE SHARED WORLD FRAME</b> (ADR 0057; owner ruling 2026-11-15: "the whole wall is a door",
    /// "doors do not teleport"). The regions are still separate scenes loaded additively (ADR 0004), each
    /// built around its own origin; this registry places them in one world so a mover who walks or sails
    /// across a shared edge comes out at the SAME world point on the other side — same coordinate along
    /// the edge, same heading, same speed, no landing point, no inset, no clearance jump.
    ///
    /// <para>It is built from data (<c>RegionDef.WorldOrigin</c> + extent, listed by the <c>WorldMap</c>
    /// asset) and published on <see cref="GameServices.WorldFrame"/>. Null there means "no frame", and every
    /// consumer then behaves exactly as it did before the frame existed (legacy keyed passages).</para>
    ///
    /// <para>Everything here is pure and deterministic; nothing is saved (the layout is data).</para>
    /// </summary>
    public sealed class WorldFrame
    {
        /// <summary>Geometric tolerance (m) for "these two edges touch" and "this point is on that edge".
        /// A float-comparison epsilon, not a tunable: region extents are authored in whole metres.</summary>
        public const float ContactToleranceMetres = 0.01f;

        private readonly List<RegionFrame> _regions = new List<RegionFrame>();

        public IReadOnlyList<RegionFrame> Regions => _regions;

        /// <summary>Add (or replace, by id) a region. Invalid frames are ignored.</summary>
        public void Register(RegionFrame frame)
        {
            if (!frame.IsValid) return;
            for (int i = 0; i < _regions.Count; i++)
            {
                if (!string.Equals(_regions[i].RegionId, frame.RegionId, StringComparison.Ordinal)) continue;
                _regions[i] = frame;
                return;
            }
            _regions.Add(frame);
        }

        public bool Contains(string regionId) => TryGet(regionId, out _);

        public bool TryGet(string regionId, out RegionFrame frame)
        {
            frame = default;
            if (string.IsNullOrEmpty(regionId)) return false;
            for (int i = 0; i < _regions.Count; i++)
            {
                if (!string.Equals(_regions[i].RegionId, regionId, StringComparison.Ordinal)) continue;
                frame = _regions[i];
                return true;
            }
            return false;
        }

        // ---- geometry (pure statics) ---------------------------------------------------------------

        /// <summary>A local position in <paramref name="from"/> expressed in <paramref name="to"/>'s local
        /// frame: the same world point. Exact inverse of itself with the arguments swapped.</summary>
        public static Vector2 MapAcross(RegionFrame from, RegionFrame to, Vector2 localInFrom) =>
            localInFrom + (from.Origin - to.Origin);

        /// <summary>True when two regions' world rectangles share AREA (more than the contact tolerance on
        /// both axes). Touching edges is not overlapping.</summary>
        public static bool Overlaps(RegionFrame a, RegionFrame b)
        {
            Rect ra = a.WorldRect, rb = b.WorldRect;
            float dx = Mathf.Min(ra.xMax, rb.xMax) - Mathf.Max(ra.xMin, rb.xMin);
            float dy = Mathf.Min(ra.yMax, rb.yMax) - Mathf.Max(ra.yMin, rb.yMin);
            return dx > ContactToleranceMetres && dy > ContactToleranceMetres;
        }

        /// <summary>The stretch of edge <paramref name="a"/> and <paramref name="b"/> share, if any (a
        /// positive-length segment where one's edge lies on the other's). False for corner contact.</summary>
        public static bool TryGetSeam(RegionFrame a, RegionFrame b, out RegionSeam seam)
        {
            seam = default;
            Rect ra = a.WorldRect, rb = b.WorldRect;
            const float tol = ContactToleranceMetres;

            float yLo = Mathf.Max(ra.yMin, rb.yMin), yHi = Mathf.Min(ra.yMax, rb.yMax);
            float xLo = Mathf.Max(ra.xMin, rb.xMin), xHi = Mathf.Min(ra.xMax, rb.xMax);

            if (yHi - yLo > tol)
            {
                if (Mathf.Abs(ra.xMax - rb.xMin) <= tol)
                {
                    seam = new RegionSeam(a.RegionId, b.RegionId, RegionEdge.East,
                                          new Vector2(ra.xMax, yLo), new Vector2(ra.xMax, yHi));
                    return true;
                }
                if (Mathf.Abs(ra.xMin - rb.xMax) <= tol)
                {
                    seam = new RegionSeam(a.RegionId, b.RegionId, RegionEdge.West,
                                          new Vector2(ra.xMin, yLo), new Vector2(ra.xMin, yHi));
                    return true;
                }
            }
            if (xHi - xLo > tol)
            {
                if (Mathf.Abs(ra.yMax - rb.yMin) <= tol)
                {
                    seam = new RegionSeam(a.RegionId, b.RegionId, RegionEdge.North,
                                          new Vector2(xLo, ra.yMax), new Vector2(xHi, ra.yMax));
                    return true;
                }
                if (Mathf.Abs(ra.yMin - rb.yMax) <= tol)
                {
                    seam = new RegionSeam(a.RegionId, b.RegionId, RegionEdge.South,
                                          new Vector2(xLo, ra.yMin), new Vector2(xHi, ra.yMin));
                    return true;
                }
            }
            return false;
        }

        /// <summary>The side opposite <paramref name="edge"/>.</summary>
        public static RegionEdge Opposite(RegionEdge edge) => edge switch
        {
            RegionEdge.West => RegionEdge.East,
            RegionEdge.East => RegionEdge.West,
            RegionEdge.South => RegionEdge.North,
            _ => RegionEdge.South,
        };

        /// <summary>Every shared edge in the layout, each pair once (A before B in registration order).</summary>
        public List<RegionSeam> Seams()
        {
            var seams = new List<RegionSeam>();
            for (int i = 0; i < _regions.Count; i++)
                for (int j = i + 1; j < _regions.Count; j++)
                    if (TryGetSeam(_regions[i], _regions[j], out RegionSeam s)) seams.Add(s);
            return seams;
        }

        /// <summary>Layout faults, in words: overlapping regions and duplicate ids. Empty = sound.</summary>
        public List<string> Validate()
        {
            var faults = new List<string>();
            for (int i = 0; i < _regions.Count; i++)
                for (int j = i + 1; j < _regions.Count; j++)
                    if (Overlaps(_regions[i], _regions[j]))
                        faults.Add($"{_regions[i].RegionId} overlaps {_regions[j].RegionId} " +
                                   $"({_regions[i].WorldRect} vs {_regions[j].WorldRect})");
            return faults;
        }

        /// <summary>The region other than <paramref name="exceptId"/> whose world rectangle holds
        /// <paramref name="world"/> (edges inclusive, within the contact tolerance).</summary>
        public bool TryFindRegionAt(Vector2 world, string exceptId, out RegionFrame found)
        {
            found = default;
            for (int i = 0; i < _regions.Count; i++)
            {
                if (string.Equals(_regions[i].RegionId, exceptId, StringComparison.Ordinal)) continue;
                Rect r = _regions[i].WorldRect;
                if (world.x < r.xMin - ContactToleranceMetres || world.x > r.xMax + ContactToleranceMetres ||
                    world.y < r.yMin - ContactToleranceMetres || world.y > r.yMax + ContactToleranceMetres) continue;
                found = _regions[i];
                return true;
            }
            return false;
        }

        /// <summary>True when some other region shares a stretch of <paramref name="regionId"/>'s
        /// <paramref name="edge"/> (a seam of positive length). An edge with no neighbour is a world boundary.</summary>
        public bool HasNeighbourAlong(string regionId, RegionEdge edge)
        {
            if (!TryGet(regionId, out RegionFrame self)) return false;
            for (int i = 0; i < _regions.Count; i++)
            {
                if (string.Equals(_regions[i].RegionId, self.RegionId, StringComparison.Ordinal)) continue;
                if (TryGetSeam(self, _regions[i], out RegionSeam seam) && seam.EdgeOfA == edge) return true;
            }
            return false;
        }

        /// <summary>How far (m) a solid's near face may reach back INSIDE a region's rectangle and still count
        /// as lying beyond the edge. A geometric epsilon (physics bounds are float AABBs of shapes authored
        /// flush with the edge), not a tunable.</summary>
        public const float EdgeSolidToleranceMetres = 0.05f;

        /// <summary>The edge a solid lies WHOLLY beyond, in the region's local frame: its whole AABB
        /// <paramref name="solidLocal"/> is outside <paramref name="localRect"/> past that side. A corner
        /// solid beyond two sides answers the first of East, West, North, South. False for anything that
        /// reaches into the rectangle. PURE.</summary>
        public static bool TryGetEdgeBeyond(Rect localRect, Rect solidLocal, out RegionEdge edge)
        {
            const float tol = EdgeSolidToleranceMetres;
            if (solidLocal.xMin >= localRect.xMax - tol) { edge = RegionEdge.East; return true; }
            if (solidLocal.xMax <= localRect.xMin + tol) { edge = RegionEdge.West; return true; }
            if (solidLocal.yMin >= localRect.yMax - tol) { edge = RegionEdge.North; return true; }
            if (solidLocal.yMax <= localRect.yMin + tol) { edge = RegionEdge.South; return true; }
            edge = default;
            return false;
        }

        /// <summary>
        /// ⭐ <b>Must this scene solid stand down?</b> (owner ruling 2026-11-15: "the whole wall is a door").
        /// A region scene closes its rectangle with solids laid just outside each edge (the land template's
        /// <c>ClosedEdges</c>). Beyond an edge that HAS a neighbour, that space is the neighbour's, and a
        /// solid there stops every walker and hull short of the seam, so the crossing can never fire. True
        /// for a solid wholly beyond an edge with a neighbour; false inside the rectangle, and false beyond
        /// a world boundary, which keeps its wall. Where an edge is only partly shared the frame's own
        /// Boundary hold keeps the unshared stretch. PURE.
        /// </summary>
        public bool StandsDownOnSharedEdge(string regionId, Rect solidLocal)
        {
            if (!TryGet(regionId, out RegionFrame self)) return false;
            return TryGetEdgeBeyond(self.LocalRect, solidLocal, out RegionEdge edge) && HasNeighbourAlong(regionId, edge);
        }

        /// <summary>
        /// ⭐ <b>Which neighbour should be warming up?</b> (PR 1223, the first-entry freeze). The neighbour
        /// across the NEAREST edge of <paramref name="fromId"/> that lies within
        /// <paramref name="withinMetres"/> of <paramref name="local"/> and has a neighbour opposite the
        /// mover's place along it; false when no such edge is that close, or for a distance of 0 or less
        /// (preloading off). The point probed is the mover's own position carried just across the edge, so
        /// a partly shared edge answers only along its shared stretch. PURE; the crossing itself is still
        /// <see cref="Resolve"/>'s to decide.
        /// </summary>
        public bool TryGetApproachingNeighbour(string fromId, Vector2 local, float withinMetres, out RegionFrame neighbour)
        {
            neighbour = default;
            if (!(withinMetres > 0f) || !TryGet(fromId, out RegionFrame from)) return false;
            Rect r = from.LocalRect;
            Vector2 along = new Vector2(Mathf.Clamp(local.x, r.xMin, r.xMax), Mathf.Clamp(local.y, r.yMin, r.yMax));
            const float across = 2f * ContactToleranceMetres;   // just over the line, inside the neighbour

            float best = float.MaxValue;
            bool found = false;
            RegionFrame pick = default;
            Probe(from, r.xMax - local.x, new Vector2(r.xMax + across, along.y), withinMetres, ref best, ref pick, ref found);
            Probe(from, local.x - r.xMin, new Vector2(r.xMin - across, along.y), withinMetres, ref best, ref pick, ref found);
            Probe(from, r.yMax - local.y, new Vector2(along.x, r.yMax + across), withinMetres, ref best, ref pick, ref found);
            Probe(from, local.y - r.yMin, new Vector2(along.x, r.yMin - across), withinMetres, ref best, ref pick, ref found);
            neighbour = pick;
            return found;
        }

        void Probe(RegionFrame from, float distance, Vector2 probeLocal, float withinMetres,
                   ref float best, ref RegionFrame pick, ref bool found)
        {
            if (distance > withinMetres || distance >= best) return;
            if (!TryFindRegionAt(from.ToWorld(probeLocal), from.RegionId, out RegionFrame n)) return;
            best = distance;
            pick = n;
            found = true;
        }

        /// <summary>
        /// ⭐ <b>The crossing rule, for one mover at <paramref name="local"/> in region
        /// <paramref name="fromId"/>.</b> Inside the rectangle, or less than <paramref name="hysteresisMetres"/>
        /// past an edge: <see cref="CrossingKind.Stay"/>. Further past an edge: if the world point lies in a
        /// neighbour, <see cref="CrossingKind.Cross"/> with the pure translation into its frame; otherwise
        /// <see cref="CrossingKind.Boundary"/> with the nearest point inside.
        ///
        /// <para><b>Why the hysteresis band and not a jump.</b> The mover arrives at the same world point,
        /// which is <paramref name="hysteresisMetres"/> (or more) INSIDE the neighbour, so going back needs a
        /// genuine move of that much back across the seam. Nothing is ever displaced to stop the bounce.</para>
        /// </summary>
        public CrossingDecision Resolve(string fromId, Vector2 local, float hysteresisMetres)
        {
            if (!TryGet(fromId, out RegionFrame from)) return CrossingDecision.Stay;
            float h = Mathf.Max(0f, hysteresisMetres);
            Rect r = from.LocalRect;

            RegionEdge edge;
            if (local.x < r.xMin - h) edge = RegionEdge.West;
            else if (local.x > r.xMax + h) edge = RegionEdge.East;
            else if (local.y < r.yMin - h) edge = RegionEdge.South;
            else if (local.y > r.yMax + h) edge = RegionEdge.North;
            else return CrossingDecision.Stay;

            if (TryFindRegionAt(from.ToWorld(local), from.RegionId, out RegionFrame to))
                return new CrossingDecision(CrossingKind.Cross, to, from.Origin - to.Origin, local, edge);

            var clamped = new Vector2(Mathf.Clamp(local.x, r.xMin, r.xMax), Mathf.Clamp(local.y, r.yMin, r.yMax));
            return new CrossingDecision(CrossingKind.Boundary, default, Vector2.zero, clamped, edge);
        }
    }
}
