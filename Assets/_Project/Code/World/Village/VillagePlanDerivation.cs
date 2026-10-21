using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Resolves the authored village into geometry without AssetDatabase, engine state, clock or randomness.
    /// Inputs are picture-space coordinates. Ground measurements alone undo the camera's depth scale.
    /// Consumers (terrain, placement and routines) share these results instead of copying the plan JSON.
    /// </summary>
    public static class VillagePlanDerivation
    {
        public sealed class Building
        {
            public string Id, LotId;
            public Vector2 Position, Door;
            public Vector4 Footprint, ArtBounds;
            public int FacingCell;
        }

        public sealed class Yard
        {
            public string Id, LotId;
            public Vector2[] Outline;
            public Vector2[][] FenceRuns;
        }

        public sealed class Route
        {
            public string Id, Class, LotId;
            public float WidthMetres;
            public Vector2[] Points;
        }

        public sealed class Light
        {
            public string Id, Preset;
            public Vector2 Position;
            public float Reach;
            public bool CastsShadow;
        }

        public sealed class Window
        {
            public string Key, Focal;
            public Vector4 Bounds;
            public bool Wide;
            public string[] RouteIds, BuildingIds, YardIds, LightIds;
            public int Pools, ShadowPairs;
        }

        public sealed class Result
        {
            public Building[] Buildings;
            public Yard[] Yards;
            public Route[] Routes;
            public Light[] Lights;
            public Window[] Windows;
        }

        public static float Tunable(VillagePlanDef plan, string name)
        {
            var matches = plan.Tunables.Where(t => t.Name == name).ToArray();
            if (matches.Length != 1 || float.IsNaN(matches[0].Value) || float.IsInfinity(matches[0].Value))
                throw new ArgumentException($"Village tunable '{name}' must occur once and be finite.");
            return matches[0].Value;
        }

        public static Result Derive(VillagePlanDef plan, IReadOnlyList<LotDef> lots,
            IReadOnlyList<YardDef> yards, IReadOnlyList<RouteDef> routes, IReadOnlyList<LightPostDef> lights)
        {
            var footprints = plan.Footprints.ToDictionary(f => f.Id, StringComparer.Ordinal);
            var result = new Result
            {
                Buildings = lots.OrderBy(l => l.Id, StringComparer.Ordinal).Select(l =>
                {
                    var f = footprints[l.FootprintId].LocalBounds;
                    var bounds = new Vector4(f.x + l.Position.x, f.y + l.Position.y, f.z + l.Position.x, f.w + l.Position.y);
                    return new Building { Id = l.BuildingId, LotId = l.Id, Position = l.Position,
                        Door = l.Position + l.DoorOffset, Footprint = bounds, FacingCell = l.FacingCell,
                        ArtBounds = new Vector4(bounds.x, l.ArtBottom, bounds.z, l.RoofTop) };
                }).ToArray(),
                Yards = yards.OrderBy(y => y.Id, StringComparer.Ordinal).Select(y => new Yard
                {
                    Id = y.Id, LotId = y.LotId, Outline = (Vector2[])y.Outline.Clone(),
                    FenceRuns = y.Fence == YardFence.None ? Array.Empty<Vector2[]>() :
                        FenceRuns(y.Outline, y.Gate, Tunable(plan, "GATE_WIDTH"))
                }).ToArray(),
                Routes = routes.OrderBy(r => r.Id, StringComparer.Ordinal).Select(r => new Route
                {
                    Id = r.Id, Class = r.Class, LotId = r.LotId, WidthMetres = r.WidthMetres,
                    Points = (Vector2[])r.Points.Clone()
                }).ToArray(),
                Lights = lights.OrderBy(l => l.Id, StringComparer.Ordinal).Select(l => new Light
                {
                    Id = l.Id, Position = l.Position, Preset = l.Preset, Reach = l.Reach, CastsShadow = l.CastsShadow
                }).ToArray()
            };
            result.Windows = Windows(plan, result);
            return result;
        }

        /// <summary>Split the perimeter at its gate. The gap is measured along the ground, including sloped edges.</summary>
        public static Vector2[][] FenceRuns(Vector2[] outline, Vector2 gate, float gateWidth)
        {
            if (outline.Length < 3 || gateWidth <= 0) throw new ArgumentException("A fence needs an outline and a positive gate width.");
            int gateEdge = 0;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < outline.Length; i++)
            {
                float distance = VillageGeometry.PointSegment(gate, outline[i], outline[(i + 1) % outline.Length]);
                if (distance < nearest) { nearest = distance; gateEdge = i; }
            }
            var runs = new List<Vector2[]>();
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                if (i != gateEdge) { runs.Add(new[] { a, b }); continue; }
                Vector2 d = VillageGeometry.Ground(b - a), g = VillageGeometry.Ground(gate - a);
                float length = d.magnitude;
                if (length == 0) continue;
                float middle = Math.Max(0, Math.Min(1, Vector2.Dot(g, d) / d.sqrMagnitude));
                float low = Math.Max(0, middle - gateWidth / (2 * length));
                float high = Math.Min(1, middle + gateWidth / (2 * length));
                if (low > 0) runs.Add(new[] { a, a + (b - a) * low });
                if (high < 1) runs.Add(new[] { a + (b - a) * high, b });
            }
            return runs.ToArray();
        }

        private static Window[] Windows(VillagePlanDef plan, Result result)
        {
            if (plan.WindowWidth <= 0 || plan.WindowHeight <= 0) throw new ArgumentException("Window dimensions must be positive.");
            var windows = new List<Window>();
            int x0 = (int)Math.Floor(plan.WindowExtent.x / plan.WindowWidth);
            int x1 = (int)Math.Ceiling(plan.WindowExtent.z / plan.WindowWidth);
            int y0 = (int)Math.Floor(plan.WindowExtent.y / plan.WindowHeight);
            int y1 = (int)Math.Ceiling(plan.WindowExtent.w / plan.WindowHeight);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    var bounds = new Vector4(x * plan.WindowWidth, y * plan.WindowHeight,
                        (x + 1) * plan.WindowWidth, (y + 1) * plan.WindowHeight);
                    if (!result.Routes.Any(r => VillageGeometry.Crosses(bounds, r.Points))) continue;
                    var annotation = plan.Windows.FirstOrDefault(w => !w.Wide && w.Bounds == bounds);
                    windows.Add(MakeWindow(plan, result, bounds, annotation?.Key ?? $"grid({x},{y})", annotation?.Focal, false));
                }
            // The plan also frames PR 5 B's shore/bar/slip paths. Keep those authored windows even
            // though V1 owns no RouteDef for their ground; also retain any new crossings found above.
            foreach (var w in plan.Windows.Where(w => !w.Wide))
                if (!windows.Any(existing => existing.Bounds == w.Bounds))
                    windows.Add(MakeWindow(plan, result, w.Bounds, w.Key, w.Focal, false));
            windows = windows.OrderBy(w => w.Bounds.y).ThenBy(w => w.Bounds.x).ToList();
            foreach (var w in plan.Windows.Where(w => w.Wide).OrderBy(w => w.Key, StringComparer.Ordinal))
                windows.Add(MakeWindow(plan, result, w.Bounds, w.Key, w.Focal, true));
            return windows.ToArray();
        }

        private static Window MakeWindow(VillagePlanDef plan, Result result, Vector4 bounds, string key, string focal, bool wide)
        {
            var lamps = result.Lights.Where(l => VillageGeometry.DistanceToBounds(l.Position, bounds) <= l.Reach).ToArray();
            // Worst-case all-lit authored village: each fence panel is an individual caster, not one
            // caster at the yard's centre. This is a placement budget; V3 verifies rendered silhouettes.
            var casters = plan.Pieces.Where(p => !p.Id.StartsWith("light.", StringComparison.Ordinal))
                .SelectMany(p => p.CasterAnchors.Length == 0 ? new[] { p.Position } : p.CasterAnchors).ToArray();
            int pairs = lamps.Where(l => l.CastsShadow).Sum(l => casters.Count(p => VillageGeometry.Distance(p, l.Position) <= l.Reach));
            return new Window
            {
                Key = key, Bounds = bounds, Focal = focal ?? "", Wide = wide,
                RouteIds = result.Routes.Where(r => VillageGeometry.Crosses(bounds, r.Points)).Select(r => r.Id).ToArray(),
                BuildingIds = result.Buildings.Where(b => VillageGeometry.Overlaps(bounds, b.ArtBounds)).Select(b => b.Id).ToArray(),
                YardIds = result.Yards.Where(y => VillageGeometry.Crosses(bounds, y.Outline.Concat(y.Outline.Take(1)).ToArray()) ||
                    VillageGeometry.Contains(y.Outline, new Vector2(bounds.x, bounds.y))).Select(y => y.Id).ToArray(),
                LightIds = lamps.Select(l => l.Id).ToArray(), Pools = lamps.Length, ShadowPairs = pairs
            };
        }
    }
}
