using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// NMC H9, the fuel stop's lot (owner ruling 2026-11-14: "a mix of concrete and ashphalt with painted lines for
    /// parking, lanes etc at gas station. Pump pads etc. add see through rule to canopy"). The plan is the road
    /// network's forecourt section (tools/nmc-roads/forecourt.json, copied in by network.cjs, baked into the road floor
    /// by bake-chunks.cjs); these read it and the key scene, and hold: everything sits inside the forecourt, the entry
    /// meets Wharf Road and the exit the stream lane, the islands stand on the slab pads, every pump stands on concrete,
    /// the bays take the game's vehicles, and the canopy's see-through ground covers the pumps under it.
    /// </summary>
    public class NmcForecourtLotTests
    {
        const string Asphalt = "fc_asphalt", Slab = "fc_slab", Island = "fc_island";
        const string WharfRoad = "road.nmc_wharf_road", StreamLane = "road.nmc_stream_lane";
        const string ExitId = "lot.nmc_fuel_exit", LotId = "lot.nmc_fuel_lot";
        const float Tolerance = 1e-3f;

        /// <summary>The game's vehicles, width x length in metres over their meshes (mesh extents x 2): the widest and
        /// longest pickups and the quad. Store bays take the quad; truck bays take every pickup.</summary>
        static readonly (string Name, float Width, float Length) Dually = ("Dually 3500", 2.72f, 6.76f);
        static readonly (string Name, float Width, float Length) Modern3500 = ("Modern 3500", 2.70f, 7.09f);
        static readonly (string Name, float Width, float Length) Quad = ("Utility quad", 1.28f, 2.16f);

        static NineMileCreekRoadNetwork.Network Net => NineMileCreekRoadNetwork.Load();
        static NineMileCreekRoadNetwork.Forecourt Lot => Net.Forecourt;

        // ---- geometry: a kit route is a flat-ended band of half-width w/2 about its polyline ----

        static bool OnRoute(NineMileCreekRoadNetwork.Route r, Vector2 p, float slack = Tolerance)
        {
            for (int i = 1; i < r.Points.Length; i++)
            {
                Vector2 a = r.Points[i - 1], b = r.Points[i], ab = b - a;
                float len2 = ab.sqrMagnitude;
                if (len2 < 1e-8f) continue;
                float t = Vector2.Dot(p - a, ab) / len2;
                float tSlack = slack / Mathf.Sqrt(len2);
                if (t < -tSlack || t > 1f + tSlack) continue;
                Vector2 q = a + Mathf.Clamp01(t) * ab;
                if ((p - q).magnitude <= r.HalfWidth + slack) return true;
            }
            return false;
        }

        static IEnumerable<Vector2> Corners(NineMileCreekRoadNetwork.Route r)
        {
            for (int i = 1; i < r.Points.Length; i++)
            {
                Vector2 a = r.Points[i - 1], b = r.Points[i], d = (b - a).normalized, n = new Vector2(-d.y, d.x) * r.HalfWidth;
                yield return a + n; yield return a - n; yield return b + n; yield return b - n;
            }
        }

        static Rect Bounds(IEnumerable<Vector2> pts)
        {
            var l = pts.ToList();
            return Rect.MinMaxRect(l.Min(p => p.x), l.Min(p => p.y), l.Max(p => p.x), l.Max(p => p.y));
        }

        static IEnumerable<Vector2> Corners(Rect r)
        {
            yield return new Vector2(r.xMin, r.yMin); yield return new Vector2(r.xMax, r.yMin);
            yield return new Vector2(r.xMin, r.yMax); yield return new Vector2(r.xMax, r.yMax);
        }

        static IEnumerable<NineMileCreekRoadNetwork.Route> Group(string g) => Lot.Routes.Where(r => r.Group == g);
        static bool OnAny(IEnumerable<NineMileCreekRoadNetwork.Route> routes, Vector2 p) => routes.Any(r => OnRoute(r, p));
        static IEnumerable<NineMileCreekRoadNetwork.Route> Paved => Group(Asphalt).Concat(Group(Slab)).Concat(Group(Island));
        static bool Inside(Rect area, Vector2 p) =>
            p.x >= area.xMin - Tolerance && p.x <= area.xMax + Tolerance && p.y >= area.yMin - Tolerance && p.y <= area.yMax + Tolerance;

        // ---- the plan ----

        [Test]
        public void TheNetwork_CarriesTheLot_AsphaltConcreteIslandsAndPaint()
        {
            Assert.IsNotNull(Lot, "the road network has no forecourt section: run node tools/nmc-roads/network.cjs");
            Assert.That(Group(Asphalt).Count(), Is.GreaterThanOrEqualTo(2), "asphalt: the lot and its exit");
            Assert.That(Group(Slab).Count(r => r.Surface == "slab"), Is.GreaterThanOrEqualTo(2), "concrete: the pump pads");
            Assert.That(Group(Island).Count(), Is.EqualTo(5), "an island under each of the three car pumps and the two semi pumps");
            Assert.IsTrue(Lot.Routes.Any(r => r.Id.StartsWith("apron.")), "the concrete apron at the shop door");
            Assert.That(Lot.Bays.Sum(b => b.Count), Is.GreaterThanOrEqualTo(4), "a few painted parking bays");
            Assert.That(Lot.Arrows.Count, Is.GreaterThanOrEqualTo(4), "painted lane arrows");
            Assert.IsTrue(Lot.Routes.All(r => r.Surface == "asphalt" || r.Surface == "slab"), "asphalt or concrete, nothing else");
            Assert.IsTrue(Group(Island).All(i => i.Lift > Group(Slab).Max(s => s.Lift)), "the islands stand proud of the pads");
            Assert.AreEqual(Lot.Routes.Count, Lot.Routes.Select(r => r.Id).Distinct().Count(), "a route id is used twice");
        }

        [Test]
        public void EveryRouteBayAndLine_IsInsideTheForecourt_SaveTheEntryAndTheExit()
        {
            Rect area = NineMileCreekWharfLandSide.ForecourtArea();
            foreach (var r in Lot.Routes.Where(r => r.Id != ExitId))
            {
                Rect b = Bounds(Corners(r));
                Assert.That(b.xMin, Is.GreaterThanOrEqualTo(area.xMin - Tolerance), r.Id + " west of the forecourt");
                Assert.That(b.xMax, Is.LessThanOrEqualTo(area.xMax + Tolerance), r.Id + " east of the forecourt");
                Assert.That(b.yMax, Is.LessThanOrEqualTo(area.yMax + Tolerance), r.Id + " north of the forecourt");
                float south = r.Id == LotId ? area.yMin - Lot.EntrySouthOverlap : area.yMin;
                Assert.That(b.yMin, Is.GreaterThanOrEqualTo(south - Tolerance), r.Id + " south of the forecourt");
            }
            foreach (var bays in Lot.Bays)
                for (int k = 0; k < bays.Count; k++)
                    foreach (Vector2 c in Corners(bays.Bay(k)))
                        Assert.IsTrue(Inside(area, c), $"{bays.Id}.{k} corner {c} is outside the forecourt {area}");
            foreach (var arrow in Lot.Arrows)
                Assert.IsTrue(Inside(area, arrow.At), $"{arrow.Id} at {arrow.At} is outside the forecourt");
        }

        [Test]
        public void TheEntry_MeetsWharfRoad_AlongTheFrontage()
        {
            Rect area = NineMileCreekWharfLandSide.ForecourtArea();
            var lot = Lot.Routes.Single(r => r.Id == LotId);
            Rect b = Bounds(Corners(lot));
            Assert.That(b.yMin, Is.LessThanOrEqualTo(area.yMin + Tolerance), "the lot stops short of Wharf Road's kerb: grass between");
            var road = Net.All(WharfRoad).ToList();
            Assert.IsNotEmpty(road, WharfRoad);
            foreach (float x in new[] { b.xMin + 1f, b.center.x, b.xMax - 1f })
                Assert.IsTrue(road.Any(r => OnRoute(r, new Vector2(x, b.yMin))), $"the lot's south edge at x {x} is not on Wharf Road");
        }

        [Test]
        public void NoGround_IsLeftUnpaved_InTheForecourt()
        {
            // The kit v3 pad no longer paves it (NineMileCreekGroundPlan.KeptV3Pads), so the lot must: every metre
            // cell of the forecourt is on one of its routes, or grass shows between the pumps and the store.
            Rect area = NineMileCreekWharfLandSide.ForecourtArea();
            var bare = new List<Vector2>();
            for (float x = Mathf.Floor(area.xMin) + 0.5f; x < area.xMax; x += 1f)
                for (float y = Mathf.Floor(area.yMin) + 0.5f; y < area.yMax; y += 1f)
                {
                    var p = new Vector2(x, y);
                    if (Inside(area, p) && !OnAny(Lot.Routes, p)) bare.Add(p);
                }
            Assert.IsEmpty(bare, $"{bare.Count} forecourt cells on no lot route, first {(bare.Count > 0 ? bare[0].ToString() : "")}");
        }

        [Test]
        public void TheExit_LeavesTheLotAndMeetsTheStreamLane()
        {
            var exit = Lot.Routes.Single(r => r.Id == ExitId);
            Vector2 start = exit.Points[0], end = exit.Points[exit.Points.Length - 1];
            Assert.IsTrue(OnAny(Paved.Where(r => r.Id != ExitId), start), "the exit does not start on the lot");
            var lane = Net.All(StreamLane).ToList();
            Assert.IsNotEmpty(lane, StreamLane);
            Assert.IsTrue(lane.Any(r => OnRoute(r, end)), $"the exit ends at {end}, off the stream lane");
        }

        [Test]
        public void TheIslands_StandOnTheSlabPads()
        {
            foreach (var island in Group(Island))
                foreach (Vector2 c in Corners(island))
                    Assert.IsTrue(OnAny(Group(Slab), c), $"{island.Id} corner {c} is off the concrete pads");
        }

        [Test]
        public void EveryPump_StandsOnConcrete_AndEachCarMachineOnItsIsland()
        {
            var concrete = Group(Slab).Concat(Group(Island)).ToList();
            foreach (var pump in NineMileCreekWharfLandSide.FuelPumps)
            {
                Vector3 at = NineMileCreekWharfLandSide.FuelStopRowAnchor(pump.Row, pump.Anchor);
                Assert.IsTrue(OnAny(concrete, at), $"{pump.Id} ({pump.Row} {pump.Anchor}) stands at {(Vector2)at}, off the concrete");
            }
            // The car machines: six nozzles each, three a side; their mean is the machine, and it is on an island.
            foreach (var machine in NineMileCreekWharfLandSide.FuelPumps.Where(p => p.Anchor.StartsWith("nozzle.") && p.Row == NineMileCreekWharfLandSide.CanopyRowId && p.Anchor != "nozzle.kero")
                         .GroupBy(p => p.Id.Substring(0, p.Id.LastIndexOf('_')).TrimEnd('a', 'b')))
            {
                Vector2 mean = Vector2.zero;
                foreach (var p in machine) mean += (Vector2)NineMileCreekWharfLandSide.FuelStopRowAnchor(p.Row, p.Anchor);
                mean /= machine.Count();
                Assert.IsTrue(OnAny(Group(Island), mean), $"machine {machine.Key} at {mean} is not on an island");
            }
        }

        [Test]
        public void TheBays_TakeTheGamesVehicles_AndLieOnTheLot()
        {
            var store = Lot.Bays.Single(b => b.Id == "bays.nmc_fuel_store");
            var trucks = Lot.Bays.Single(b => b.Id == "bays.nmc_fuel_trucks");
            Assert.That(store.Size.x, Is.GreaterThan(Quad.Width), "store bay narrower than the quad");
            Assert.That(store.Size.y, Is.GreaterThan(Quad.Length), "store bay shorter than the quad");
            foreach (var v in new[] { Dually, Modern3500 })
            {
                Assert.That(trucks.Size.x, Is.GreaterThan(v.Width), "truck bay narrower than the " + v.Name);
                Assert.That(trucks.Size.y, Is.GreaterThan(v.Length), "truck bay shorter than the " + v.Name);
            }
            foreach (var bays in Lot.Bays)
                for (int k = 0; k < bays.Count; k++)
                {
                    Rect bay = bays.Bay(k);
                    foreach (Vector2 c in Corners(bay))
                        Assert.IsTrue(OnAny(Group(Asphalt).Concat(Group(Slab)), c), $"{bays.Id}.{k} corner {c} is off the lot");
                    foreach (var island in Group(Island))
                        Assert.IsFalse(bay.Overlaps(Bounds(Corners(island))), $"{bays.Id}.{k} overlaps {island.Id}");
                }
        }

        [Test]
        public void ThePaint_LiesOnThePaintedSurfaces()
        {
            var painted = Group(Asphalt).Concat(Group(Slab)).ToList();
            float half = Lot.ArrowLength * 0.5f;
            foreach (var arrow in Lot.Arrows)
                foreach (float s in new[] { -half, 0f, half })
                    Assert.IsTrue(OnAny(painted, arrow.At + arrow.Dir * s), $"{arrow.Id} runs off the lot at {arrow.At + arrow.Dir * s}");
            foreach (var line in Lot.Lines.Where(l => l.Of == null))
                foreach (Vector2 p in line.Points)
                    Assert.IsTrue(OnAny(painted, p), $"{line.Id} point {p} is off the lot");
        }

        // ---- the canopy's see-through rule (BuildingInterior's footprint, no scene) ----

        [Test]
        public void TheCanopyRule_IsData_AndIsAGhostNotAHole()
        {
            Assert.That(Lot.CanopyGhostAlpha, Is.GreaterThan(0f).And.LessThan(1f), "the ghost is a see-through roof, not none and not solid");
            Assert.That(Lot.CanopyMarginMetres, Is.GreaterThanOrEqualTo(0f));
            Assert.That(Lot.CanopyHysteresisMetres, Is.GreaterThan(0f), "no hysteresis: the roof flickers at the edge");
        }

        [Test]
        public void TheCanopyRule_HoldsUnderTheRoof_AndNotAtTheShopDoorOrOnTheRoad()
        {
            Rect under = NineMileCreekWharfLandSide.CanopyUnder();
            var room = new InteriorFootprint(under.center, under.width, under.height, 0, 1, 1f);
            foreach (var pump in NineMileCreekWharfLandSide.FuelPumps.Where(p => p.Row == NineMileCreekWharfLandSide.CanopyRowId))
            {
                Vector2 at = NineMileCreekWharfLandSide.FuelStopRowAnchor(pump.Row, pump.Anchor);
                Assert.IsTrue(room.Contains(at, 0f), $"{pump.Id} at {at}: a player at this pump keeps the solid roof");
            }
            foreach (var lane in Lot.Arrows.Where(a => a.Id.StartsWith("arrow.nmc_fuel_lane_")))
                Assert.IsTrue(room.Contains(lane.At + lane.Dir * (Lot.ArrowLength + 3f), 0f), $"a vehicle up {lane.Id} under the roof keeps the solid roof");

            Rect area = NineMileCreekWharfLandSide.ForecourtArea();
            var apron = Lot.Routes.Single(r => r.Id.StartsWith("apron."));
            Vector2 door = new Vector2((apron.Points[0].x + apron.Points[1].x) * 0.5f, apron.Points[0].y);
            Assert.IsFalse(room.Contains(door, 0f), "the shop door is under the canopy rule: the roof fades from the store");
            Assert.IsFalse(room.Contains(new Vector2(under.center.x, area.yMin - 3f), 0f), "Wharf Road is under the canopy rule");
            Assert.IsTrue(room.Contains(under.center, 0f), "the middle of the pad");
        }
    }
}
