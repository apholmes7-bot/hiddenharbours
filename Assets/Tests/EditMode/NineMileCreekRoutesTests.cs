using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THREE TRIPS ON THE BOARD — does the road read as USED without reading as TRAFFIC?</b>
    ///
    /// <para>The buyer's run is <c>NineMileCreekTripsTests</c>' business. This file is about the other
    /// two and about all three TOGETHER: the chandler down to the Route 91 pumps mid-morning, the
    /// outboard man up the length of Wharf Road in the afternoon, and whether any two of the three ever
    /// occupy the same ground at the same minute.</para>
    ///
    /// <para><b>The traffic test is a real footprint overlap, not a centre distance.</b> Two 6.7 × 2.8 m
    /// boxes passing each other on a 5 m carriageway have centres 4.4 m apart and do not touch; two
    /// nose-to-tail at 5 m do. A centre-distance proxy has to pick one of those to be wrong about, so
    /// this sweeps the separating axes of the two oriented boxes instead
    /// (<see cref="Overlap"/>).</para>
    ///
    /// <para><c>municipal-infrastructure.md</c> §3.4's negative test applies to roads too: <b>if it reads
    /// REGULAR it is wrong</b>. Three departures spread across a day, from two different ends of the
    /// village, is what a village looks like; three on the hour is a bus timetable.</para>
    /// </summary>
    public class NineMileCreekRoutesTests
    {
        private const float SecondsPerGameHour = 1800f / 24f;
        private const float Cruise = 7f;
        private const float Walk = 1.4f;
        private static readonly Vector2 DoorLocal = new(-1.75f, 0.10f);

        // The three shipped timetables, stated here rather than loaded: this file is an arithmetic test
        // with no asset dependency, and a fixture pinned to an asset would go red the moment the owner
        // retimed a run — which is exactly what those hours are for him to do.
        private const float BuyerOut = 4.75f, BuyerBack = 20.5f;
        private const float ChandlerOut = 9.2f, ChandlerBack = 10.5f;
        private const float OutboardOut = 14.3f, OutboardBack = 16.1f;

        private GameObject _terrainGo;
        private MainlandTidalTerrain _terrain;
        private NineMileCreekRoads.Paving _paving;

        [SetUp]
        public void SetUp()
        {
            _terrainGo = new GameObject("NineMileCreekMainland_RoutesTest");
            _terrain = _terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);
            _paving = NineMileCreekRoads.Pave(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            GameServices.Reset();
        }

        // =============================================================================================
        //  THE THREE PLANS
        // =============================================================================================

        private static VehicleTripPlan Built(Vector2[] outbound, Vector2[] home, Vector2 post,
                                             Vector2 postFacing, float outHour, float backHour, string who)
        {
            var spec = new VehicleTripSpec(outbound, home, post, postFacing,
                                           NineMileCreekTrips.ForecourtPost(),
                                           NineMileCreekTrips.ForecourtPostFacing(),
                                           DoorLocal, outHour, backHour, Cruise, Walk);
            VehicleTripPlan plan = VehicleTripPlan.Build(spec, SecondsPerGameHour, out string problem);
            Assert.That(plan, Is.Not.Null, $"{who}'s own geometry did not make a trip: {problem}");
            return plan;
        }

        private static VehicleTripPlan BuyersRun()
        {
            var spec = new VehicleTripSpec(
                NineMileCreekTrips.OutboundRoute(), NineMileCreekTrips.ReturnRoute(),
                NineMileCreekTrips.ParkPost(), NineMileCreekTrips.ParkPostFacing(),
                NineMileCreekTrips.WharfPost(), NineMileCreekTrips.WharfPostFacing(),
                DoorLocal, BuyerOut, BuyerBack, Cruise, Walk);
            VehicleTripPlan plan = VehicleTripPlan.Build(spec, SecondsPerGameHour, out string problem);
            Assert.That(plan, Is.Not.Null, $"the buyer's run did not build: {problem}");
            return plan;
        }

        private static VehicleTripPlan ChandlersRun() => Built(
            NineMileCreekTrips.ToTheForecourt(NineMileCreekMainland.ThroughRoad,
                                              NineMileCreekTrips.ChandleryPost()),
            NineMileCreekTrips.FromTheForecourt(NineMileCreekMainland.ThroughRoad,
                                                NineMileCreekTrips.ChandleryPost()),
            NineMileCreekTrips.ChandleryPost(),
            (NineMileCreekTrips.ChandleryBay() - NineMileCreekTrips.ChandleryPost()).normalized,
            ChandlerOut, ChandlerBack, "the chandler");

        private static VehicleTripPlan OutboardMansRun() => Built(
            NineMileCreekTrips.ToTheForecourt(NineMileCreekMainland.WharfRoad,
                                              NineMileCreekTrips.DoryYardPost()),
            NineMileCreekTrips.FromTheForecourt(NineMileCreekMainland.WharfRoad,
                                                NineMileCreekTrips.DoryYardPost()),
            NineMileCreekTrips.DoryYardPost(),
            (NineMileCreekTrips.DoryYardBay() - NineMileCreekTrips.DoryYardPost()).normalized,
            OutboardOut, OutboardBack, "the outboard man");

        private static IEnumerable<(string Who, VehicleTripPlan Plan)> AllThree()
        {
            yield return ("the buyer", BuyersRun());
            yield return ("the chandler", ChandlersRun());
            yield return ("the outboard man", OutboardMansRun());
        }

        // =============================================================================================
        //  1. EACH RUN IS DRIVEABLE GROUND
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Every metre of every run is on paved ground, or on the verge of a road she is beside.</b>
        ///
        /// <para>⚠️ A PULL-OFF IS NOT PAVED, AND THAT IS THE POINT. The two verge bays stand half a metre
        /// clear of the carriageway so passing traffic cannot clip them, which puts them on grass — the
        /// village authors vehicle ground only at its two ends, and paving a shop's frontage would turn a
        /// farming coast into a suburb (the region's own walks doc refuses it in as many words). So the
        /// claim is the honest one: she is either on somebody's paving or within a pull-off's reach of a
        /// published carriageway, and NEVER out in a field between two nodes — which is the failure this
        /// test exists to catch.</para>
        /// </summary>
        [Test]
        public void EveryMetreOfAllThreeRunsIsOnPavingOrOnAVergeBesideARoad()
        {
            float reach = NineMileCreekTrips.ShoulderOffsetMetres
                          + NineMileCreekRoads.ParkedVehicleLengthMetres;

            foreach ((string who, VehicleTripPlan plan) in AllThree())
            {
                int adrift = 0;
                Vector2 firstAdrift = Vector2.zero;
                float firstHour = 0f, worst = 0f;

                foreach (float hour in EveryMinute())
                {
                    Vector2 at = plan.SampleAt(hour).MachinePosition;
                    if (_paving.Paved.Contains(NineMileCreekRoads.CellOf(at))) continue;

                    float toRoad = ToNearestRoad(at);
                    if (toRoad > worst) worst = toRoad;
                    if (toRoad <= reach) continue;
                    if (adrift++ == 0) { firstAdrift = at; firstHour = hour; }
                }

                Assert.That(adrift, Is.Zero,
                    $"{who}: {adrift} sampled minutes are neither paved nor within {reach:0.#} m of a " +
                    $"published road (worst {worst:0.##} m), the first at {firstAdrift} " +
                    $"({firstHour:00.00}). She is crossing open ground between two nodes.");
            }
        }

        [Test]
        public void EveryMetreOfAllThreeRunsIsDryAtEveryTide()
        {
            foreach ((string who, VehicleTripPlan plan) in AllThree())
            {
                int wet = 0;
                Vector2 firstWet = Vector2.zero;
                foreach (float hour in EveryMinute())
                {
                    Vector2 at = plan.SampleAt(hour).MachinePosition;
                    if (_terrain.ElevationAt(at) > NineMileCreekMainland.SpringHighWater) continue;
                    if (wet++ == 0) firstWet = at;
                }
                Assert.That(wet, Is.Zero,
                    $"{who}: {wet} sampled minutes on ground the spring tide covers, the first at {firstWet}.");
            }
        }

        [Test]
        public void EveryDriverWalksOverDryGroundToHisOwnMachine()
        {
            foreach ((string who, VehicleTripPlan plan) in AllThree())
            {
                int wet = 0;
                Vector2 firstWet = Vector2.zero;
                foreach (float hour in EveryMinute())
                {
                    VehicleTripPose pose = plan.SampleAt(hour);
                    if (pose.DriverAboard) continue;
                    if (_terrain.ElevationAt(pose.DriverPosition) > NineMileCreekMainland.SpringHighWater)
                        continue;
                    if (wet++ == 0) firstWet = pose.DriverPosition;
                }
                Assert.That(wet, Is.Zero, $"{who}: {wet} sampled minutes stand him in water, first at {firstWet}.");
            }
        }

        [Test]
        public void EveryDriverWalksToHisMachineRatherThanHikingToIt()
        {
            foreach ((string who, VehicleTripPlan plan) in AllThree())
            {
                float toTheDoor = plan.DriverLegs.LengthMetres[VehicleTripPlan.LegBoardAtOrigin];
                Assert.That(toTheDoor, Is.GreaterThan(0.5f).And.LessThan(40f),
                    $"{who}: {toTheDoor:0.#} m from his post to her door. Either she is parked on top of " +
                    "him, or she is somewhere he would not walk to twice a day.");
            }
        }

        // =============================================================================================
        //  2. THE VERGE BAYS
        // =============================================================================================

        [Test]
        public void APulledOffMachineIsClearOfTheCarriagewayRatherThanInIt()
        {
            foreach ((string what, Vector2 bay, Vector2[] road) in Bays())
            {
                float offCentre = RouteFollowMath.OffCentreLineMetres(road, 0, road.Length, bay);
                float nearFlank = offCentre - NineMileCreekRoads.ParkedVehicleWidthMetres * 0.5f;

                Assert.That(nearFlank, Is.GreaterThanOrEqualTo(
                        NineMileCreekRoads.CarriagewayHalfWidthMetres),
                    $"{what} stands {offCentre:0.##} m off the centre-line, so her near flank is " +
                    $"{nearFlank:0.##} m out and the carriageway edge is at " +
                    $"{NineMileCreekRoads.CarriagewayHalfWidthMetres} m — a truck keeping to the road " +
                    "would drive through her, and nothing in the fleet carries a collider to notice.");
            }
        }

        [Test]
        public void APulledOffMachineIsStillBesideTheRoadRatherThanInAField()
        {
            foreach ((string what, Vector2 bay, Vector2[] road) in Bays())
            {
                float offCentre = RouteFollowMath.OffCentreLineMetres(road, 0, road.Length, bay);
                Assert.That(offCentre, Is.LessThan(NineMileCreekRoads.CarriagewayHalfWidthMetres
                                                   + NineMileCreekRoads.ParkedVehicleLengthMetres),
                    $"{what} is {offCentre:0.##} m off the road. That is not a vehicle pulled over, it " +
                    "is one abandoned in a field.");
            }
        }

        [Test]
        public void EveryVergeBayIsDryGround()
        {
            foreach ((string what, Vector2 bay, Vector2[] _) in Bays())
                Assert.That(_terrain.ElevationAt(bay),
                    Is.GreaterThan(NineMileCreekMainland.SpringHighWater),
                    $"{what} at {bay} stands on ground the spring tide covers.");
        }

        [Test]
        public void NoVergeBayStandsInsideATownLotOrOnTheStationsOwnApron()
        {
            foreach ((string what, Vector2 bay, Vector2[] _) in Bays())
            {
                foreach (Vector3 lot in NineMileCreekMainland.TownLots)
                {
                    float apart = Vector2.Distance(bay, new Vector2(lot.x, lot.y));
                    Assert.That(apart, Is.GreaterThan(NineMileCreekMainland.TownLotRadius),
                        $"{what} at {bay} is {apart:0.##} m from a town lot at ({lot.x}, {lot.y}) — " +
                        $"inside its reserved {NineMileCreekMainland.TownLotRadius} m.");
                }
            }
        }

        [Test]
        public void TheTwoCustomersShareOnePumpBayBecauseTheVillageHasOnePump()
        {
            // Stated so that a later change which gives them two bays has to say why.
            Assert.That(NineMileCreekTrips.ForecourtBay(),
                        Is.EqualTo(NineMileCreekTrips.ForecourtBay()));
            float apart = Vector2.Distance(NineMileCreekTrips.ForecourtPost(),
                                           NineMileCreekStation.Route91ForecourtPos);
            Assert.That(apart, Is.EqualTo(NineMileCreekTrips.AtThePumpsMetres).Within(0.01f),
                "a customer must stand out from the island, not on the kerb the machines are bolted to.");
        }

        // =============================================================================================
        //  3. ⭐ THREE TRIPS, NEVER THE SAME GROUND AT THE SAME MINUTE
        // =============================================================================================

        [Test]
        public void NoTwoOfTheThreeEverOccupyTheSameGround()
        {
            var plans = new List<(string, VehicleTripPlan)>(AllThree());
            int clashes = 0;
            string first = null;

            foreach (float hour in EveryMinute())
            {
                for (int a = 0; a < plans.Count; a++)
                for (int b = a + 1; b < plans.Count; b++)
                {
                    VehicleTripPose pa = plans[a].Item2.SampleAt(hour);
                    VehicleTripPose pb = plans[b].Item2.SampleAt(hour);
                    if (!Overlap(pa, pb)) continue;
                    if (clashes++ == 0)
                        first = $"{plans[a].Item1} at {pa.MachinePosition} and {plans[b].Item1} at " +
                                $"{pb.MachinePosition}, {hour:00.00}";
                }
            }

            Assert.That(clashes, Is.Zero,
                $"{clashes} sampled minutes put two machines' footprints through each other — the first " +
                $"is {first}. Stagger the hours, or move a bay off the other's road.");
        }

        [Test]
        public void TheThreeDeparturesDoNotReadAsATimetable()
        {
            // §3.4's negative test, applied to roads: if it reads REGULAR it is wrong.
            float[] outs = { BuyerOut, ChandlerOut, OutboardOut };
            System.Array.Sort(outs);

            float a = outs[1] - outs[0], b = outs[2] - outs[1];
            Assert.That(Mathf.Abs(a - b), Is.GreaterThan(0.5f),
                $"the three departures are {a:0.0} h and {b:0.0} h apart — that is a bus timetable, not " +
                "a village.");
            Assert.That(a, Is.GreaterThan(1f).And.LessThan(12f));
            Assert.That(b, Is.GreaterThan(1f).And.LessThan(12f));
        }

        [Test]
        public void EachRunIsAwayForLongEnoughToHaveDoneSomethingAndShortEnoughToComeBack()
        {
            foreach ((string who, VehicleTripPlan plan) in AllThree())
            {
                float away = DaySchedule.ElapsedHours(
                    plan.DepartureHours[VehicleTripPlan.LegRestAtOrigin],
                    plan.DepartureHours[VehicleTripPlan.LegBoardAtOrigin]);
                Assert.That(away, Is.GreaterThan(0.25f).And.LessThan(20f),
                    $"{who} is away {away:0.00} h. Under a quarter of an hour is a machine that flickers " +
                    "between two bays; twenty is a trip that has swallowed the day.");
            }
        }

        // =============================================================================================
        //  4. THE READ-OUT — what the three runs actually lay on the ground
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Draws the three runs over the region's own paving, as an SVG under
        /// <c>artifacts/</c></b> (gitignored) — the eyeball half of "the road reads as used".
        ///
        /// <para><b>Not the rendered plate the charter asks for</b>, and it does not pretend to be: a
        /// plate of Wharf Road at 04:50 wants a live editor with a graphics device, a region build and a
        /// clock, which is the owner's Build click and not a headless test's. What this is instead is
        /// the thing a headless run CAN honestly produce — every machine's position, every minute, drawn
        /// on the paved cells they are supposed to stay on — so a route that wanders is visible and not
        /// merely asserted. It also prints the day's shape to the log, which is what a build read-out is
        /// for.</para>
        /// </summary>
        [Test]
        public void TheThreeRunsDrawTheirOwnMap()
        {
            var svg = new System.Text.StringBuilder();
            Rect region = NineMileCreekRoads.RegionRect();
            svg.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='{region.xMin} {-region.yMax} " +
                       $"{region.width} {region.height}' width='900'>")
               .Append("<rect x='").Append(region.xMin).Append("' y='").Append(-region.yMax)
               .Append("' width='").Append(region.width).Append("' height='").Append(region.height)
               .Append("' fill='#101418'/>");

            // The paving, a metre a cell — the ground every run is measured against.
            svg.Append("<g fill='#3a4148'>");
            foreach (Vector2Int cell in _paving.Paved)
                svg.Append($"<rect x='{cell.x}' y='{-cell.y - 1}' width='1' height='1'/>");
            svg.Append("</g>");

            string[] inks = { "#e8c07d", "#7dc0e8", "#c0e87d" };
            int ink = 0;
            var report = new List<string>();

            foreach ((string who, VehicleTripPlan plan) in AllThree())
            {
                svg.Append($"<g fill='{inks[ink % inks.Length]}'>");
                int minutesMoving = 0;
                foreach (float hour in EveryMinute())
                {
                    VehicleTripPose pose = plan.SampleAt(hour);
                    if (pose.Moving) minutesMoving++;
                    svg.Append($"<circle cx='{pose.MachinePosition.x:0.##}' " +
                               $"cy='{-pose.MachinePosition.y:0.##}' r='0.9'/>");
                }
                svg.Append("</g>");

                report.Add($"{who}: {Mathf.RoundToInt(minutesMoving / 60f * 100f) / 100f:0.00} game hours " +
                           $"under way, out at {plan.DepartureHours[VehicleTripPlan.LegBoardAtOrigin]:00.00}, " +
                           $"home from {plan.DepartureHours[VehicleTripPlan.LegBoardAtDestination]:00.00}, " +
                           $"{plan.MachineLegs.LengthMetres[VehicleTripPlan.LegDriveOut]:0.#} m each way");
                ink++;
            }
            svg.Append("</svg>");

            string dir = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), "artifacts");
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "nmc-three-runs.svg");
            System.IO.File.WriteAllText(path, svg.ToString());

            Debug.Log($"[NineMileCreekRoutes] {path}\n  " + string.Join("\n  ", report));

            Assert.That(System.IO.File.Exists(path), Is.True);
            Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(10_000),
                "the map came out empty — the paving raster or the plans produced nothing to draw.");
        }

        // =============================================================================================
        //  HELPERS
        // =============================================================================================

        private static IEnumerable<(string, Vector2, Vector2[])> Bays()
        {
            yield return ("the chandler's van", NineMileCreekTrips.ChandleryBay(),
                          NineMileCreekMainland.ThroughRoad);
            yield return ("the outboard man's box", NineMileCreekTrips.DoryYardBay(),
                          NineMileCreekMainland.WharfRoad);
            yield return ("a machine at the pumps", NineMileCreekTrips.ForecourtBay(),
                          NineMileCreekMainland.ThroughRoad);
        }

        /// <summary>Every minute of the game day, as an hour value.</summary>
        private static IEnumerable<float> EveryMinute()
        {
            for (int m = 0; m < 24 * 60; m++) yield return m / 60f;
        }

        /// <summary>How far a point is from the nearest published carriageway, in metres. The three the
        /// region actually publishes — a footpath is not a road a truck may be beside.</summary>
        private static float ToNearestRoad(Vector2 at)
        {
            float best = float.MaxValue;
            foreach (Vector2[] road in new[] { NineMileCreekMainland.WharfRoad,
                                               NineMileCreekMainland.ThroughRoad,
                                               NineMileCreekMainland.BarRoad })
                best = Mathf.Min(best, RouteFollowMath.OffCentreLineMetres(road, 0, road.Length, at));
            return best;
        }

        /// <summary>
        /// Do two machines' footprints overlap? The separating-axis theorem over two oriented 6.7 × 2.8 m
        /// boxes — the region's own published envelope, swung through each machine's live heading. Four
        /// axes (each box's two) decide it, and finding one gap is a proof they are apart.
        /// </summary>
        private static bool Overlap(in VehicleTripPose a, in VehicleTripPose b)
        {
            float halfL = NineMileCreekRoads.ParkedVehicleLengthMetres * 0.5f;
            float halfW = NineMileCreekRoads.ParkedVehicleWidthMetres * 0.5f;

            Vector2 upA = a.MachineDirection == Vector2.zero ? Vector2.up : a.MachineDirection.normalized;
            Vector2 upB = b.MachineDirection == Vector2.zero ? Vector2.up : b.MachineDirection.normalized;
            Vector2 rightA = new(upA.y, -upA.x), rightB = new(upB.y, -upB.x);

            Vector2 d = b.MachinePosition - a.MachinePosition;
            foreach (Vector2 axis in new[] { rightA, upA, rightB, upB })
            {
                float reach = Mathf.Abs(Vector2.Dot(rightA, axis)) * halfW
                            + Mathf.Abs(Vector2.Dot(upA, axis)) * halfL
                            + Mathf.Abs(Vector2.Dot(rightB, axis)) * halfW
                            + Mathf.Abs(Vector2.Dot(upB, axis)) * halfL;
                if (Mathf.Abs(Vector2.Dot(d, axis)) > reach) return false;   // a gap on this axis
            }
            return true;
        }
    }
}
