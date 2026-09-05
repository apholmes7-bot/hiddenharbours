using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE FISH BUYER'S RUN — is the road she is put on a road, and is it dry?</b>
    ///
    /// <para>The claims that matter about a scheduled trip are about the WORLD, not about the plan: every
    /// metre she covers is on paved ground, every metre of it is dry at every tide, both bays are ground
    /// the region actually authored, and her driver's walk at each end is a walk rather than a hike. The
    /// plan's own arithmetic is <c>VehicleTripPlanTests</c>'s business; this file asks whether the answer
    /// it gives lands somewhere a truck can be.</para>
    ///
    /// <para><b>Sampled off the CLOCK, not off the waypoints.</b> A route whose nodes are all on the road
    /// can still cut a corner across the marsh between two of them — the bar road shipped exactly that
    /// defect once (dry NODES over a wet middle). So the machine is walked at metre resolution through
    /// the plan itself, which is also the only thing that proves the plan and the geometry agree.</para>
    ///
    /// <para>Terrain through the same <see cref="NineMileCreekMainland.ConfigureTerrain"/> the builder
    /// calls; paving is a pure function of the published plan plus that terrain. No assets, no scene, no
    /// bake — honest headless.</para>
    /// </summary>
    public class NineMileCreekTripsTests
    {
        private const float SecondsPerGameHour = 1800f / 24f;
        private const float Cruise = 7f;
        private const float Walk = 1.4f;
        private const float OutboundHour = 4.75f;
        private const float ReturnHour = 20.5f;

        // The Dually's own door, off her mesh: the sidecar's 'drive' reach point. Stated here rather than
        // loaded so this file stays an arithmetic test with no asset dependency — the SHIPPED value is
        // the component's business, and NineMileCreekTripsPlayTests drives the real one.
        private static readonly Vector2 DoorLocal = new(-1.75f, 0.10f);

        private GameObject _terrainGo;
        private MainlandTidalTerrain _terrain;
        private NineMileCreekRoads.Paving _paving;

        [SetUp]
        public void SetUp()
        {
            _terrainGo = new GameObject("NineMileCreekMainland_TripsTest");
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

        private static VehicleTripPlan Plan()
        {
            var spec = new VehicleTripSpec(
                NineMileCreekTrips.OutboundRoute(), NineMileCreekTrips.ReturnRoute(),
                NineMileCreekTrips.ParkPost(), NineMileCreekTrips.ParkPostFacing(),
                NineMileCreekTrips.WharfPost(), NineMileCreekTrips.WharfPostFacing(),
                DoorLocal, OutboundHour, ReturnHour, Cruise, Walk);

            VehicleTripPlan plan = VehicleTripPlan.Build(spec, SecondsPerGameHour, out string problem);
            Assert.That(plan, Is.Not.Null, $"the creek's own geometry did not make a trip: {problem}");
            return plan;
        }

        // =============================================================================================
        //  1. THE TWO BAYS ARE GROUND THE REGION AUTHORED
        // =============================================================================================

        [Test]
        public void SheRestsOnTheTruckPark()
        {
            Vector2 bay = NineMileCreekTrips.HomeBay();
            Assert.That(NineMileCreekRoads.TruckParkArea().Contains(bay), Is.True,
                $"her home bay {bay} is not inside the truck park {NineMileCreekRoads.TruckParkArea()}.");
        }

        [Test]
        public void SheStandsOnTheBuyersGravelAtTheWharfAndNotOnTheWinchApron()
        {
            Vector2 bay = NineMileCreekTrips.WharfBay();
            Assert.That(NineMileCreekRoads.ParkingArea().Contains(bay), Is.True,
                $"her wharf bay {bay} is not on the parking pad {NineMileCreekRoads.ParkingArea()}.");
            Assert.That(NineMileCreekRoads.WinchApronArea().Contains(bay), Is.False,
                "the winch apron is a working surface, not a car park — the region says so itself.");
        }

        [Test]
        public void BothBaysArePaved()
        {
            AssertPaved(NineMileCreekTrips.HomeBay(), "her home bay");
            AssertPaved(NineMileCreekTrips.WharfBay(), "her wharf bay");
        }

        // =============================================================================================
        //  2. EVERY METRE SHE COVERS — sampled off the clock, not off the nodes
        // =============================================================================================

        /// <summary>⭐ The load-bearing one. Walked at metre resolution through the PLAN, so a route whose
        /// nodes are all on the road but whose middle cuts the marsh fails here.</summary>
        [Test]
        public void EveryMetreOfHerDayIsOnPavedGround()
        {
            VehicleTripPlan plan = Plan();
            int offPaving = 0;
            Vector2 firstOff = Vector2.zero;
            float firstOffHour = 0f;

            foreach (float hour in EveryMinute())
            {
                Vector2 at = plan.SampleAt(hour).MachinePosition;
                if (_paving.Paved.Contains(NineMileCreekRoads.CellOf(at))) continue;
                if (offPaving++ == 0) { firstOff = at; firstOffHour = hour; }
            }

            Assert.That(offPaving, Is.Zero,
                $"{offPaving} sampled minutes stand her off the paving, the first at {firstOff} " +
                $"({firstOffHour:00.00}). Her route leaves the road, the spur, the park or the buyers' " +
                "gravel somewhere between two of its nodes.");
        }

        [Test]
        public void EveryMetreOfHerDayIsDryAtEveryTide()
        {
            VehicleTripPlan plan = Plan();
            int wet = 0;
            Vector2 firstWet = Vector2.zero;

            foreach (float hour in EveryMinute())
            {
                Vector2 at = plan.SampleAt(hour).MachinePosition;
                // Spring HIGH water, not the tide of the moment: a road a truck fords twice a month is
                // not a road, and the paving's own dry-ground rule uses the same bar.
                if (_terrain.ElevationAt(at) > NineMileCreekMainland.SpringHighWater) continue;
                if (wet++ == 0) firstWet = at;
            }

            Assert.That(wet, Is.Zero,
                $"{wet} sampled minutes stand her on ground the spring tide covers, the first at {firstWet}.");
        }

        /// <summary>
        /// ⭐ <b>"Dry ground under every wheel"</b> — the charter's own words, and a stronger claim than
        /// the origin test above makes. A truck is 6.7 × 2.8 m (the region's own published envelope), so
        /// a centre-line that is on the road can still hang a corner over the verge; her four corners are
        /// swung through her live heading at every sampled minute.
        ///
        /// <para>DRY rather than PAVED, deliberately. Paving is claimed to a way's own half-width, and a
        /// 2.8 m machine on a 2.5 m half-width carriageway has corners within it but a bay's margins are
        /// not promised to anybody — so a paved-corner claim would be asserting something the region
        /// never offered. What she must never do is put a wheel in the water.</para>
        /// </summary>
        [Test]
        public void EveryCornerOfHerIsOverDryGroundAllDay()
        {
            VehicleTripPlan plan = Plan();
            float halfLength = NineMileCreekRoads.ParkedVehicleLengthMetres * 0.5f;
            float halfWidth = NineMileCreekRoads.ParkedVehicleWidthMetres * 0.5f;

            int wet = 0;
            Vector2 firstWet = Vector2.zero;
            float firstWetHour = 0f;

            foreach (float hour in EveryMinute())
            {
                VehicleTripPose pose = plan.SampleAt(hour);
                Vector2 up = pose.MachineDirection == Vector2.zero
                    ? Vector2.up : pose.MachineDirection.normalized;
                Vector2 right = new(up.y, -up.x);

                for (int corner = 0; corner < 4; corner++)
                {
                    float x = (corner & 1) == 0 ? -halfWidth : halfWidth;
                    float y = (corner & 2) == 0 ? -halfLength : halfLength;
                    Vector2 at = pose.MachinePosition + right * x + up * y;
                    if (_terrain.ElevationAt(at) > NineMileCreekMainland.SpringHighWater) continue;
                    if (wet++ == 0) { firstWet = at; firstWetHour = hour; }
                }
            }

            Assert.That(wet, Is.Zero,
                $"{wet} corner samples put a wheel on ground the spring tide covers, the first at " +
                $"{firstWet} ({firstWetHour:00.00}). Her CENTRE is on the road there; her flank is not.");
        }

        [Test]
        public void OnTheRoadSheIsOnTheCarriageway()
        {
            VehicleTripPlan plan = Plan();
            Vector2[] road = NineMileCreekMainland.WharfRoad;

            // Only the stretch of the drive that is BETWEEN the spur join and the pull-off is a claim
            // about Wharf Road; the spur, the park and the gravel are their own surfaces and the paving
            // test above is what covers them.
            float alongJoin = NineMileCreekTrips.DistanceAlong(road, NineMileCreekTrips.SpurJoin());
            float alongPull = NineMileCreekTrips.DistanceAlong(road, NineMileCreekTrips.PullOff());
            float lo = Mathf.Min(alongJoin, alongPull) + 1f, hi = Mathf.Max(alongJoin, alongPull) - 1f;

            int sampled = 0, off = 0;
            float worst = 0f;
            foreach (float hour in EveryMinute())
            {
                VehicleTripPose pose = plan.SampleAt(hour);
                if (pose.Stage != VehicleTripStage.Driving) continue;

                float along = NineMileCreekTrips.DistanceAlong(road, pose.MachinePosition);
                if (along <= lo || along >= hi) continue;

                sampled++;
                float offLine = RouteFollowMath.OffCentreLineMetres(road, 0, road.Length,
                                                                    pose.MachinePosition);
                if (offLine > worst) worst = offLine;
                if (offLine > NineMileCreekRoads.CarriagewayHalfWidthMetres) off++;
            }

            Assert.That(sampled, Is.GreaterThan(0),
                "no sampled minute put her on Wharf Road between the spur and the pull-in — this test "
                + "would pass vacuously, which is worse than failing.");
            Assert.That(off, Is.Zero,
                $"{off} of {sampled} sampled minutes on Wharf Road stand her outside the carriageway "
                + $"(worst {worst:0.##} m of {NineMileCreekRoads.CarriagewayHalfWidthMetres} m).");
        }

        // =============================================================================================
        //  3. HER DRIVER
        // =============================================================================================

        [Test]
        public void HisWharfPostIsTheSpotTheCastAlreadyStandsHimOn()
        {
            Assert.That(NineMileCreekTrips.WharfPost(),
                        Is.EqualTo(NineMileCreekPeople.Named("WendellArsenault").Position),
                        "the trip must READ the buyer's spot, never recompute it — a second copy of a "
                        + "villager's position is #345 waiting to happen.");
        }

        [Test]
        public void EveryMetreHeWalksIsDryGround()
        {
            VehicleTripPlan plan = Plan();
            int wet = 0;
            Vector2 firstWet = Vector2.zero;

            foreach (float hour in EveryMinute())
            {
                VehicleTripPose pose = plan.SampleAt(hour);
                if (pose.DriverAboard) continue;              // he is in the cab; his position is hers
                if (_terrain.ElevationAt(pose.DriverPosition) > NineMileCreekMainland.SpringHighWater) continue;
                if (wet++ == 0) firstWet = pose.DriverPosition;
            }

            Assert.That(wet, Is.Zero,
                $"{wet} sampled minutes stand the buyer in water, the first at {firstWet}.");
        }

        [Test]
        public void BothOfHisWalksAreAWalkAndNotAHike()
        {
            VehicleTripPlan plan = Plan();
            float toTheDoor = plan.DriverLegs.LengthMetres[VehicleTripPlan.LegBoardAtOrigin];
            float toHisStall = plan.DriverLegs.LengthMetres[VehicleTripPlan.LegAlightAtDestination];

            Assert.That(toTheDoor, Is.GreaterThan(0.5f).And.LessThan(15f),
                $"{toTheDoor:0.#} m from his park post to her door — he is either standing in the truck "
                + "or across the yard from it.");
            Assert.That(toHisStall, Is.GreaterThan(0.5f).And.LessThan(30f),
                $"{toHisStall:0.#} m from her door to his stall — the parking pad and the stall are "
                + "supposed to be one place you walk between, not two ends of the wharf.");
        }

        [Test]
        public void HeIsNotStandingInsideHisOwnTruck()
        {
            float apart = Vector2.Distance(NineMileCreekTrips.ParkPost(), NineMileCreekTrips.HomeBay());
            Assert.That(apart, Is.GreaterThan(NineMileCreekRoads.ParkedVehicleWidthMetres * 0.5f),
                $"his park post is {apart:0.##} m from her centre — inside her own body.");
        }

        [Test]
        public void HisParkPostIsOnTheParkNotOnTheVerge()
        {
            Vector2 post = NineMileCreekTrips.ParkPost();
            Assert.That(NineMileCreekRoads.TruckParkArea().Contains(post), Is.True,
                $"his park post {post} is outside the park {NineMileCreekRoads.TruckParkArea()}.");
            AssertPaved(post, "his park post");
        }

        // =============================================================================================
        //  4. THE ROADS THEMSELVES
        // =============================================================================================

        [Test]
        public void TheRoadHomeIsTheRoadOutInReverse()
        {
            Vector2[] outbound = NineMileCreekTrips.OutboundRoute();
            Vector2[] home = NineMileCreekTrips.ReturnRoute();

            Assert.That(home.Length, Is.EqualTo(outbound.Length));
            for (int i = 0; i < outbound.Length; i++)
                Assert.That(home[i], Is.EqualTo(outbound[outbound.Length - 1 - i]).Using(Near),
                    $"point {i} of the road home is not point {outbound.Length - 1 - i} of the road out.");
        }

        [Test]
        public void TheRunIsAsLongAsTheVillageIsWideRatherThanAHopOrAJourney()
        {
            float metres = NineMileCreekTrips.Length(NineMileCreekTrips.OutboundRoute());
            Assert.That(metres, Is.GreaterThan(100f).And.LessThan(400f),
                $"{metres:0.#} m from the park to the wharf. The village is ~320 m of Wharf Road end to "
                + "end; a run much shorter than that is not going anywhere and one much longer has left "
                + "the region.");
        }

        [Test]
        public void ThePullInGivesHerRoomToLineUpRatherThanTurningOffAtRightAngles()
        {
            Vector2[] road = NineMileCreekMainland.WharfRoad;
            Vector2 pullOff = NineMileCreekTrips.PullOff();
            Vector2 bay = NineMileCreekTrips.WharfBay();

            Vector2 alongRoad = NineMileCreekTrips.DirectionOn(road, pullOff);
            Vector2 intoTheBay = (bay - pullOff).normalized;
            float turn = Vector2.Angle(alongRoad, intoTheBay);

            Assert.That(turn, Is.LessThan(45f),
                $"she leaves the carriageway through a {turn:0.#}° turn. A pull-in sharper than 45° is a "
                + "truck pivoting off a road, not driving off one — lengthen "
                + "NineMileCreekTrips.ApproachRunMetres.");
        }

        [Test]
        public void TheRouteKeepsClearOfTheBuyersOwnStall()
        {
            var stall = new Vector2(NineMileCreekMainland.FishBuyerPos.x,
                                    NineMileCreekMainland.FishBuyerPos.y);
            VehicleTripPlan plan = Plan();

            float nearest = float.MaxValue;
            Vector2 nearestAt = Vector2.zero;
            foreach (float hour in EveryMinute())
            {
                Vector2 at = plan.SampleAt(hour).MachinePosition;
                float d = Vector2.Distance(at, stall);
                if (d < nearest) { nearest = d; nearestAt = at; }
            }

            Assert.That(nearest, Is.GreaterThan(NineMileCreekRoads.ParkedVehicleWidthMetres),
                $"she passes {nearest:0.##} m from the fish stall at {nearestAt} — closer than her own "
                + "width, so she is driving through it.");
        }

        [Test]
        public void MovingTheParkMovesTheWholeRun()
        {
            // Not by moving the constant (it is readonly and shared): by asserting that every published
            // point of the town end is DERIVED from it, which is the property that survives the walk.
            Vector2 park = NineMileCreekTrips.HomeBay();
            Assert.That(park, Is.EqualTo(new Vector2(NineMileCreekMainland.TruckParkPos.x,
                                                     NineMileCreekMainland.TruckParkPos.y)));
            Assert.That(NineMileCreekTrips.OutboundRoute()[0], Is.EqualTo(park).Using(Near));
            Assert.That(NineMileCreekTrips.ReturnRoute()[^1], Is.EqualTo(park).Using(Near));
            Assert.That(NineMileCreekTrips.SpurJoin(),
                        Is.EqualTo(NineMileCreekRoads.ParkSpurRoute()[0]).Using(Near),
                        "the trip must leave the park by the spur the region already published, not by "
                        + "a second join of its own.");
        }

        // =============================================================================================
        //  HELPERS
        // =============================================================================================

        /// <summary>Every minute of the game day, as an hour value. 1440 samples of a pure function.</summary>
        private static System.Collections.Generic.IEnumerable<float> EveryMinute()
        {
            for (int m = 0; m < 24 * 60; m++) yield return m / 60f;
        }

        private void AssertPaved(Vector2 at, string what)
        {
            Assert.That(_paving.Paved.Contains(NineMileCreekRoads.CellOf(at)), Is.True,
                $"{what} at {at} is not paved — the dry-ground rule trimmed it, or nothing claims it.");
        }

        /// <summary>Vector2 equality to the millimetre — float arithmetic over a 300 m route does not
        /// come back bit-identical and a test that demanded it would be measuring the FPU.</summary>
        private static readonly System.Collections.IComparer Near = new NearComparer();

        private sealed class NearComparer : System.Collections.IComparer
        {
            public int Compare(object x, object y)
            {
                if (x is Vector2 a && y is Vector2 b) return Vector2.Distance(a, b) <= 1e-3f ? 0 : 1;
                return 1;
            }
        }
    }
}
