using System;
using System.Collections.Generic;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Tests.Support
{
    // =============================================================================================
    // ⭐ Phase B, 2026-09-19, C6 — THE LEGS a live walk is steered along.
    //
    // CabinWalkGraph says WHICH hops take her from where the game stands her to her door; this file
    // says where to put her feet for each one, so a PlayMode case can walk her there on the shipped
    // deck walk — held input, one tick at a time, no teleport. It plans on the same floors the graph
    // reads (the deck def's areas, the interior def's levels) and steers through the same projection
    // the controller inverts (DeckAreaMath.DeckToWorld / WorldDirectionToDeck), so a leg that stalls is
    // a finding about the hull or the walk, named by the leg, never a walker that aimed badly.
    //
    // ⚠ Two properties of the live deck walk this planning respects (both measured in Phase A):
    //   • the clamp is PLAN-blind: stepping off her area onto a point another walkable area contains
    //     puts her on that area whatever its height, the first such area by index — so a leg's open
    //     cells never leave her area except into the one area the hop names, and a crossing target
    //     is a point no other walkable area contains;
    //   • a plan gap between two areas is crossed only if a single step spans it — so a link the graph
    //     makes across a gap is steered straight over it, and a stall there is reported as the gap.
    // =============================================================================================

    /// <summary>
    /// A plan raster for steering: square cells of <see cref="Cell"/> metres over a box, open where a
    /// predicate says. <see cref="Path"/> is an 8-neighbour breadth-first path (no corner cutting),
    /// planned first on the DEEP cells — open cells whose eight neighbours are all open, so she keeps a
    /// cell's clearance from every wall — and only then on every open cell.
    /// </summary>
    public sealed class CabinPlanRaster
    {
        public readonly float Cell;
        public readonly Vector2 Min;
        public readonly int Nx;
        public readonly int Ny;

        private readonly bool[] _open;
        private readonly bool[] _deep;

        public CabinPlanRaster(Vector4 box, float cell, Func<Vector2, bool> open)
        {
            Cell = Mathf.Max(1e-3f, cell);
            Min = new Vector2(box.x, box.y);
            Nx = Mathf.Max(1, Mathf.CeilToInt((box.z - box.x) / Cell));
            Ny = Mathf.Max(1, Mathf.CeilToInt((box.w - box.y) / Cell));
            _open = new bool[Nx * Ny];
            for (int iy = 0; iy < Ny; iy++)
                for (int ix = 0; ix < Nx; ix++)
                    _open[iy * Nx + ix] = open(CentreOf(ix, iy));

            _deep = new bool[Nx * Ny];
            for (int iy = 0; iy < Ny; iy++)
            {
                for (int ix = 0; ix < Nx; ix++)
                {
                    if (!_open[iy * Nx + ix]) continue;
                    bool deep = true;
                    for (int dy = -1; dy <= 1 && deep; dy++)
                        for (int dx = -1; dx <= 1 && deep; dx++)
                            deep = IsOpen(ix + dx, iy + dy);
                    _deep[iy * Nx + ix] = deep;
                }
            }
        }

        /// <summary>A box around several outlines, grown by <paramref name="margin"/>.</summary>
        public static Vector4 BoxAround(float margin, params Vector2[][] outlines)
        {
            var box = new Vector4(float.PositiveInfinity, float.PositiveInfinity,
                                  float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector2[] outline in outlines)
            {
                if (outline == null) continue;
                foreach (Vector2 v in outline)
                {
                    box.x = Mathf.Min(box.x, v.x);
                    box.y = Mathf.Min(box.y, v.y);
                    box.z = Mathf.Max(box.z, v.x);
                    box.w = Mathf.Max(box.w, v.y);
                }
            }
            if (float.IsInfinity(box.x)) return Vector4.zero;
            return new Vector4(box.x - margin, box.y - margin, box.z + margin, box.w + margin);
        }

        public Vector2 CentreOf(int ix, int iy) => Min + new Vector2((ix + 0.5f) * Cell, (iy + 0.5f) * Cell);
        public bool IsOpen(int ix, int iy) => ix >= 0 && ix < Nx && iy >= 0 && iy < Ny && _open[iy * Nx + ix];
        public bool IsDeep(int ix, int iy) => ix >= 0 && ix < Nx && iy >= 0 && iy < Ny && _deep[iy * Nx + ix];

        /// <summary>The open (or deep) cell centre nearest <paramref name="p"/> that
        /// <paramref name="accept"/> takes, if any: a full scan, because these rasters are small.</summary>
        public bool TryNearest(Vector2 p, bool deep, Func<Vector2, bool> accept, out Vector2 centre)
        {
            centre = default;
            float best = float.PositiveInfinity;
            for (int iy = 0; iy < Ny; iy++)
            {
                for (int ix = 0; ix < Nx; ix++)
                {
                    if (!(deep ? _deep[iy * Nx + ix] : _open[iy * Nx + ix])) continue;
                    Vector2 c = CentreOf(ix, iy);
                    float sqr = (c - p).sqrMagnitude;
                    if (sqr >= best || (accept != null && !accept(c))) continue;
                    best = sqr;
                    centre = c;
                }
            }
            return !float.IsInfinity(best);
        }

        /// <summary>The cell centres from the cell nearest <paramref name="from"/> to the cell nearest
        /// <paramref name="to"/>, then <paramref name="to"/> itself — on the deep cells if they connect
        /// the two, else on the open cells; null if neither does.</summary>
        public List<Vector2> Path(Vector2 from, Vector2 to)
            => PathOn(_deep, from, to) ?? PathOn(_open, from, to);

        private List<Vector2> PathOn(bool[] cells, Vector2 from, Vector2 to)
        {
            int start = NearestIndex(cells, from), goal = NearestIndex(cells, to);
            if (start < 0 || goal < 0) return null;
            var cameFrom = new int[cells.Length];
            for (int i = 0; i < cameFrom.Length; i++) cameFrom[i] = -1;
            var queue = new Queue<int>();
            cameFrom[start] = start;
            queue.Enqueue(start);
            while (queue.Count > 0 && cameFrom[goal] < 0)
            {
                int at = queue.Dequeue();
                int ax = at % Nx, ay = at / Nx;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = ax + dx, ny = ay + dy;
                        if (nx < 0 || nx >= Nx || ny < 0 || ny >= Ny) continue;
                        int next = ny * Nx + nx;
                        if (!cells[next] || cameFrom[next] >= 0) continue;
                        // A diagonal only between two open orthogonals: she never clips a corner.
                        if (dx != 0 && dy != 0 && (!cells[ay * Nx + nx] || !cells[ny * Nx + ax])) continue;
                        cameFrom[next] = at;
                        queue.Enqueue(next);
                    }
                }
            }
            if (cameFrom[goal] < 0) return null;

            var path = new List<Vector2>();
            for (int at = goal; ; at = cameFrom[at])
            {
                path.Add(CentreOf(at % Nx, at / Nx));
                if (at == start) break;
            }
            path.Reverse();
            path.Add(to);
            return path;
        }

        private int NearestIndex(bool[] cells, Vector2 p)
        {
            int best = -1;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i]) continue;
                float sqr = (CentreOf(i % Nx, i / Nx) - p).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// One leg of a live walk: the plan-frame path to steer along, and the point it is aiming for.
    /// </summary>
    public sealed class CabinLeg
    {
        public readonly string Name;
        public readonly List<Vector2> Path;
        public readonly Vector2 Target;
        private int _cursor;

        public CabinLeg(string name, List<Vector2> path, Vector2 target)
        {
            Name = name ?? "";
            Path = path;
            Target = target;
        }

        public bool IsWalkable => Path != null && Path.Count > 0;

        /// <summary>
        /// The held move that walks her from <paramref name="at"/> along the path (pure pursuit, one
        /// <paramref name="lookahead"/> ahead), in the WORLD frame the deck walk reads its input in:
        /// <see cref="DeckAreaMath.DeckToWorld"/> of the hull-frame direction, which the walk inverts
        /// with <see cref="DeckAreaMath.WorldDirectionToDeck"/>. Full speed until the last point, then
        /// scaled so her final step lands on it (<paramref name="stepMetres"/> is one full step).
        /// </summary>
        public Vector2 Steer(Vector2 at, float lookahead, float stepMetres, float heading, float elevation)
        {
            if (!IsWalkable) return Vector2.zero;
            while (_cursor < Path.Count - 1 && (Path[_cursor] - at).magnitude < lookahead) _cursor++;
            Vector2 aim = Path[_cursor] - at;
            float distance = aim.magnitude;
            if (distance < 1e-4f) return Vector2.zero;
            Vector2 world = DeckAreaMath.DeckToWorld(aim / distance, 0f, heading, elevation);
            if (world.sqrMagnitude < 1e-10f) return Vector2.zero;
            float magnitude = _cursor == Path.Count - 1 ? Mathf.Clamp01(distance / Mathf.Max(1e-4f, stepMetres)) : 1f;
            return world.normalized * magnitude;
        }

        public bool Arrived(Vector2 at, float within) => (Target - at).sqrMagnitude <= within * within;
    }

    /// <summary>
    /// ⭐ Phase B, 2026-09-19, C6 — the legs for each kind of hop, planned on the graph's own floors.
    /// Every target is chosen INSIDE the bar the graph walked (the band, the reach) by a margin, so the
    /// live walk meets the same bar the guard measured and never grazes its edge.
    /// </summary>
    public static class CabinLegs
    {
        /// <summary>The steering raster's cell: a quarter of a typical 0.2 m aisle's clearance.</summary>
        public const float RasterCell = 0.05f;
        /// <summary>How far inside a band or a reach a target sits.</summary>
        public const float TargetMargin = 0.04f;

        /// <summary>Is <paramref name="p"/> inside a walkable area other than those named?</summary>
        public static bool InAnotherWalkableArea(CabinWalkGraph g, Vector2 p, int keep, int alsoKeep = -1)
        {
            for (int m = 0; m < g.AreaCount; m++)
            {
                if (m == keep || m == alsoKeep) continue;
                DeckArea area = g.AreaAt(m);
                if (CabinWalkGraph.IsWalkable(area) && DeckAreaMath.Contains(area.Outline, p)) return true;
            }
            return false;
        }

        /// <summary>A leg across deck area <paramref name="area"/> to <paramref name="target"/>.</summary>
        public static CabinLeg OnDeck(CabinWalkGraph g, string name, int area, Vector2 from, Vector2 target)
        {
            DeckArea a = g.AreaAt(area);
            if (!CabinWalkGraph.IsWalkable(a)) return new CabinLeg(name, null, target);
            var raster = new CabinPlanRaster(CabinPlanRaster.BoxAround(RasterCell * 2f, a.Outline), RasterCell,
                                             p => DeckAreaMath.Contains(a.Outline, p));
            return new CabinLeg(name, raster.Path(from, target), target);
        }

        /// <summary>
        /// A leg over <paramref name="link"/> from area <paramref name="from"/> onto the other area:
        /// her own area's cells, the other area's cells no third walkable area contains, and — for a
        /// link across a gap — the cells within the tolerance of both. The target is the point of the
        /// other area nearest the link's contact that no other walkable area contains.
        /// </summary>
        public static CabinLeg OverLink(CabinWalkGraph g, CabinDeckLink link, int from, Vector2 at)
        {
            int to = link.Other(from);
            DeckArea a = g.AreaAt(from), b = g.AreaAt(to);
            string name = $"over the {link.Why} link {g.AreaId(from)} -> {g.AreaId(to)}";
            if (a == null || b == null) return new CabinLeg(name, null, link.OnOther(from));
            float tol = g.Tolerance;

            bool Bridge(Vector2 p)
            {
                CabinWalkGraph.NearestOn(a, p, out float da);
                CabinWalkGraph.NearestOn(b, p, out float db);
                return da <= tol * tol && db <= tol * tol;
            }

            bool Open(Vector2 p)
                => DeckAreaMath.Contains(a.Outline, p)
                   || (DeckAreaMath.Contains(b.Outline, p) && !InAnotherWalkableArea(g, p, from, to))
                   || Bridge(p);

            bool Landing(Vector2 p) => DeckAreaMath.Contains(b.Outline, p) && !InAnotherWalkableArea(g, p, to);

            var raster = new CabinPlanRaster(CabinPlanRaster.BoxAround(RasterCell * 2f, a.Outline, b.Outline),
                                             RasterCell, Open);
            Vector2 contact = link.OnOther(from);
            if (!raster.TryNearest(contact, true, Landing, out Vector2 target)
                && !raster.TryNearest(contact, false, Landing, out target))
                target = contact;
            return new CabinLeg(name, raster.Path(at, target), target);
        }

        /// <summary>A target on deck area <paramref name="area"/> inside the disk of
        /// <paramref name="radius"/> about <paramref name="centre"/>, on a floor within the tolerance of
        /// <paramref name="height"/> — a deep cell nearest the centre, else the graph's own sample.</summary>
        public static bool TryDeckTarget(CabinWalkGraph g, int area, Vector2 centre, float radius, float height,
                                         out Vector2 target)
        {
            target = centre;
            DeckArea a = g.AreaAt(area);
            if (!CabinWalkGraph.IsWalkable(a)) return false;
            float inner = Mathf.Max(0f, radius - TargetMargin);
            float tol = g.Tolerance;

            bool Accept(Vector2 p)
                => (p - centre).sqrMagnitude <= inner * inner && DeckAreaMath.Contains(a.Outline, p)
                   && Mathf.Abs(CabinWalkGraph.HeightOn(a, p) - height) <= tol;

            var raster = new CabinPlanRaster(CabinPlanRaster.BoxAround(RasterCell * 2f, a.Outline), RasterCell,
                                             p => DeckAreaMath.Contains(a.Outline, p));
            if (raster.TryNearest(centre, true, Accept, out target) || raster.TryNearest(centre, false, Accept, out target))
                return true;

            List<Vector2> samples = g.DeckPointsNear(area, centre, radius, height);
            if (samples.Count == 0) return false;
            target = samples[0];
            foreach (Vector2 p in samples)
                if ((p - centre).sqrMagnitude < (target - centre).sqrMagnitude) target = p;
            return true;
        }

        /// <summary>A leg across one standable component of a level's sole.</summary>
        public static CabinLeg OnLevel(CabinWalkGraph g, string name, int level, int component, Vector2 from,
                                       Vector2 target)
        {
            CabinLevelGrid grid = g.Grid(level);
            if (grid == null) return new CabinLeg(name, null, target);
            var raster = new CabinPlanRaster(CabinPlanRaster.BoxAround(RasterCell * 2f, grid.Level.Outline),
                                             RasterCell, p => InComponent(grid, component, p));
            return new CabinLeg(name, raster.Path(from, target), target);
        }

        /// <summary>A target in component <paramref name="component"/> of a level inside the disk of
        /// <paramref name="radius"/> about <paramref name="centre"/>: the standable cell nearest it.</summary>
        public static bool TryLevelTarget(CabinWalkGraph g, int level, int component, Vector2 centre, float radius,
                                          out Vector2 target)
        {
            target = centre;
            CabinLevelGrid grid = g.Grid(level);
            if (grid == null) return false;
            float inner = Mathf.Max(0f, radius - TargetMargin);
            var raster = new CabinPlanRaster(CabinPlanRaster.BoxAround(RasterCell * 2f, grid.Level.Outline),
                                             RasterCell, p => InComponent(grid, component, p));

            bool Accept(Vector2 p) => (p - centre).sqrMagnitude <= inner * inner;

            if (raster.TryNearest(centre, true, Accept, out target) || raster.TryNearest(centre, false, Accept, out target))
                return true;

            List<Vector2> cells = grid.ComponentPoints(component, centre, radius);
            if (cells.Count == 0) return false;
            target = cells[0];
            foreach (Vector2 p in cells)
                if ((p - centre).sqrMagnitude < (target - centre).sqrMagnitude) target = p;
            return true;
        }

        /// <summary>Is <paramref name="p"/> on a standable cell of <paramref name="component"/>?</summary>
        public static bool InComponent(CabinLevelGrid grid, int component, Vector2 p)
            => grid.TryCellOf(p, out int ix, out int iy) && grid.LabelAt(ix, iy) == component;

        /// <summary>A point <paramref name="beyond"/> metres from <paramref name="centre"/> on her side of
        /// it, on the floor she is walking: where she backs off to so a spent latch re-arms.</summary>
        public static bool TryBackOff(CabinWalkGraph g, CabinNode node, Vector2 at, Vector2 centre, float beyond,
                                      out Vector2 target)
        {
            target = at;
            float best = float.PositiveInfinity;
            bool found = false;
            Vector2 away = (at - centre).sqrMagnitude > 1e-6f ? (at - centre).normalized : Vector2.up;
            for (int k = 0; k < 16; k++)
            {
                // Straight away from the centre first, then fanning out either side of it.
                float turn = (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * (Mathf.PI / 8f);
                Vector2 dir = Rotate(away, turn);
                Vector2 p = centre + dir * beyond;
                if (!IsFloor(g, node, p)) continue;
                float cost = (p - at).sqrMagnitude;
                if (cost >= best) continue;
                best = cost;
                target = p;
                found = true;
            }
            return found;
        }

        private static bool IsFloor(CabinWalkGraph g, CabinNode node, Vector2 p)
        {
            if (node.Kind == CabinNodeKind.Level)
            {
                CabinLevelGrid grid = g.Grid(node.Index);
                return grid != null && InComponent(grid, node.Component, p);
            }
            DeckArea a = g.AreaAt(node.Index);
            return CabinWalkGraph.IsWalkable(a) && DeckAreaMath.Contains(a.Outline, p)
                   && !InAnotherWalkableArea(g, p, node.Index);
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
