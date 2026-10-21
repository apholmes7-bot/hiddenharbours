using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.Tests.EditMode.StPetersVillageTestData;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class StPetersVillagePlanTests
    {
        [Test]
        public void G01_RoutineTreeHas23NodesAndLeavesFivePlayerLinksOutsideIt()
        {
            var d = Load();
            Check(d.Plan.Tree.Length == 23, 1, d.Plan.Id, "23 routine nodes");
            Check(d.Plan.Tree.Select(n => n.Node).Distinct().Count() == 23, 1, d.Plan.Id, "unique routine nodes");
            var nodes = d.Plan.Tree.ToDictionary(n => n.Node);
            Check(d.Plan.Tree.Count(n => string.IsNullOrEmpty(n.Parent)) == 1, 1, d.Plan.Id, "one root");
            foreach (var node in d.Plan.Tree)
            {
                var visited = new HashSet<string>(); var current = node;
                while (current != null)
                {
                    Check(visited.Add(current.Node), 1, node.Node, "no cycle");
                    if (string.IsNullOrEmpty(current.Parent)) break;
                    Check(nodes.ContainsKey(current.Parent), 1, node.Node, "parent resolves");
                    current = nodes[current.Parent];
                }
            }
            CollectionAssert.AreEquivalent(new[] { "east_walk", "garden_walk", "ring", "barren_gap", "shore_walk" },
                d.Plan.CycleLinks.Select(l => l.Link), "V1 guard 1: T8: five player links");
            // The ring already has ONE parent edge. T8's other way round it is never added as another edge.
            Check(nodes["yard_saltbox"].Parent == "east_walk_head" && nodes["garden_gate"].Parent == "cross_walk" &&
                  nodes["green_walk_foot"].Parent == "cross_walk" && nodes["yard_sage_cottage"].Parent == "bluff_walk_foot",
                1, "T8", "routine tree keeps its ruled approaches, without the five alternate player connections");
        }

        [Test]
        public void G02_NoFenceCrossesAnyRouteIncludingHouseWalks()
        {
            var d = Load(); var derived = d.Derive();
            foreach (var yard in derived.Yards)
                foreach (var run in yard.FenceRuns)
                    foreach (var route in derived.Routes)
                        for (int i = 1; i < route.Points.Length; i++)
                        {
                            // Painted segments have square ends: a rounded end cap invents ground beyond
                            // the authored endpoint (notably Bluff Walk below the cottage's south fence).
                            var a = route.Points[i - 1]; var b = route.Points[i];
                            var direction = VillageGeometry.Ground(b - a).normalized;
                            var offset = new Vector2(-direction.y, direction.x * IsoGround.GroundDepthScale) * (route.WidthMetres / 2);
                            var corridor = new[] { a + offset, b + offset, b - offset, a - offset };
                            bool crosses = VillageGeometry.Contains(corridor, run[0]) || VillageGeometry.Contains(corridor, run[1]);
                            for (int j = 0; j < corridor.Length; j++)
                                crosses |= VillageGeometry.Intersects(run[0], run[1], corridor[j], corridor[(j + 1) % corridor.Length]);
                            Check(!crosses, 2, yard.Id + "/" + route.Id, "fence clears the full painted route strip");
                        }
        }

        [Test]
        public void G03_EveryYardHoldsItsFootprintAndGroundMargin()
        {
            var d = Load(); var derived = d.Derive();
            foreach (var yard in derived.Yards)
            {
                var f = derived.Buildings.Single(b => b.LotId == yard.LotId).Footprint;
                float hold = d.T("YARD_HOLD");
                var expanded = VillageGeometry.Rectangle(new Vector4(f.x - hold, f.y - hold * IsoGround.GroundDepthScale,
                    f.z + hold, f.w + hold * IsoGround.GroundDepthScale));
                foreach (var corner in expanded)
                    Check(VillageGeometry.Contains(yard.Outline, corner), 3, yard.Id, "holds footprint plus YARD_HOLD");
            }
        }

        [Test]
        public void G04_YardsKeepTheirGroundGap()
        {
            var d = Load(); var yards = d.Derive().Yards;
            for (int i = 0; i < yards.Length; i++)
                for (int j = i + 1; j < yards.Length; j++)
                    Check(VillageGeometry.PolygonDistance(yards[i].Outline, yards[j].Outline) + d.T("EPSILON") >= d.T("YARD_GAP"),
                        4, yards[i].Id + "/" + yards[j].Id, "yards keep YARD_GAP ground metres");
        }

        [Test]
        public void G05_FootprintsKeepFourGroundMetres()
        {
            var d = Load(); var buildings = d.Derive().Buildings;
            for (int i = 0; i < buildings.Length; i++)
                for (int j = i + 1; j < buildings.Length; j++)
                    Check(VillageGeometry.PolygonDistance(VillageGeometry.Rectangle(buildings[i].Footprint),
                        VillageGeometry.Rectangle(buildings[j].Footprint)) + d.T("EPSILON") >= d.T("FOOTPRINT_GAP"),
                        5, buildings[i].Id + "/" + buildings[j].Id, "footprint separation");
        }

        [Test]
        public void G06_HearthKeepsEightGroundMetres()
        {
            var d = Load();
            foreach (var building in d.Derive().Buildings)
                Check(VillageGeometry.DistanceToBounds(StPetersBuilder.VillageHearthPos, building.Footprint) >= d.T("HEARTH_CLEARANCE"),
                    6, building.Id, "hearth clearance");
        }

        [Test]
        public void G07_BarSightlineKeepsRadiusPlusFortyGroundMetres()
        {
            var d = Load();
            foreach (var b in d.Derive().Buildings)
            {
                float radius = VillageGeometry.Distance(new Vector2(b.Footprint.x, b.Footprint.y), new Vector2(b.Footprint.z, b.Footprint.w)) / 2;
                Check(VillageGeometry.PointSegment(b.Position, StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo) >= radius + d.T("BAR_SIGHTLINE"),
                    7, b.Id, "bar sightline: footprint radius plus clearance");
            }
        }

        [Test]
        public void G08_FiveFixedPointsMatchTheExistingBuilders()
        {
            var d = Load();
            var expected = new Dictionary<string, Vector2>
            {
                ["hearth"] = StPetersBuilder.VillageHearthPos, ["spawn"] = StPetersBuilder.StartSpawnPos,
                ["green"] = StPetersBuilder.VillageGreen, ["ginnyMark"] = StPetersBuilder.GinnyPos,
                ["nedLetter"] = StPetersBuilder.NedsLetterPos
            };
            Check(d.Plan.FixedPoints.Length == expected.Count, 8, d.Plan.Id, "five fixed points");
            foreach (var p in d.Plan.FixedPoints)
                Check(expected.TryGetValue(p.Name, out var position) && p.Position == position, 8, p.Name, "fixed point unchanged");
        }

        [Test]
        public void G09_WalksMeetTheirStreetGateAndDoorAsLeaves()
        {
            var d = Load(); var derived = d.Derive();
            foreach (var lot in d.Lots)
            {
                var route = d.Routes.Single(r => r.Id == lot.WalkId);
                var street = d.Routes.Single(r => r.Id == lot.StreetId);
                var building = derived.Buildings.Single(b => b.LotId == lot.Id);
                Check(route.LotId == lot.Id && route.Points.Length == 3 && route.Class == "footpath", 9, lot.Id, "three-point dirt house walk");
                Check(VillageGeometry.Distance(route.Points[0], lot.Approach) <= d.T("EPSILON") &&
                    Enumerable.Range(1, route.Points.Length - 1).Any(i => VillageGeometry.PointSegment(lot.Gate, route.Points[i - 1], route.Points[i]) <= d.T("EPSILON")) &&
                    VillageGeometry.Distance(route.Points[2], building.Door) <= d.T("EPSILON"), 9, lot.Id, "approach, gate, door");
                Check(Enumerable.Range(1, street.Points.Length - 1).Any(i => VillageGeometry.PointSegment(lot.Approach,
                    street.Points[i - 1], street.Points[i]) <= d.T("EPSILON")), 9, lot.Id, "approach meets its street");
                foreach (var other in d.Routes.Where(r => r.Id != route.Id && r.Id != street.Id))
                    for (int i = 1; i < route.Points.Length; i++)
                        for (int j = 1; j < other.Points.Length; j++)
                            Check(!VillageGeometry.Intersects(route.Points[i - 1], route.Points[i], other.Points[j - 1], other.Points[j]),
                                9, lot.Id + "/" + other.Id, "walk remains a leaf");
            }
        }

        [Test]
        public void G10_EntityIdsAreUniqueStableAndContainNoPosition()
        {
            var d = Load();
            var ids = new[] { d.Plan.Id }.Concat(d.Lots.Select(x => x.Id)).Concat(d.Yards.Select(x => x.Id))
                .Concat(d.Routes.Select(x => x.Id)).Concat(d.Commons.Select(x => x.Id)).Concat(d.Lights.Select(x => x.Id)).ToArray();
            Check(ids.Length == 61 && ids.Distinct().Count() == 61, 10, d.Plan.Id, "61 distinct Def ids");
            Check(d.Plan.Pieces.Length == 121 && d.Plan.Pieces.Select(p => p.Id).Distinct().Count() == 121,
                10, d.Plan.Id, "121 distinct package pieces");
            foreach (var id in ids.Concat(d.Plan.Pieces.Select(p => p.Id)))
                Check(id != null && Regex.IsMatch(id, "^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)+$"), 10, id, "type.snake_case, no coordinate suffix");
        }

        [Test]
        public void G11_FootprintTableHasOneSourcedRowPerBuilding()
        {
            var d = Load();
            Check(d.Plan.Footprints.Length == 9 && d.Plan.Footprints.Select(f => f.Id).Distinct().Count() == 9,
                11, d.Plan.Id, "nine unique footprint records");
            Check(d.Plan.Footprints.Count(f => f.Provisional) == 3, 11, d.Plan.Id, "three new-home measurements await V3 bake");
            foreach (var f in d.Plan.Footprints)
                Check(!string.IsNullOrWhiteSpace(f.Source) && f.LocalBounds.z > f.LocalBounds.x && f.LocalBounds.w > f.LocalBounds.y &&
                    d.Lots.Count(l => l.FootprintId == f.Id) == 1, 11, f.Id, "one consumer, positive bounds, named source");
        }
    }
}
