using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>A VILLAGER COUPLES UP A TRAILER AND HAULS IT ROUND THE DAY</b> — road fleet PR 6a, over real
    /// frames, on a real <see cref="ScheduledTrip"/> and a real <see cref="ParkedTrailer"/>.
    ///
    /// <para><b>What only PlayMode can show.</b> The arithmetic is EditMode's —
    /// <c>VehicleTowingTripTests</c> owns the ten-block day and the refusals,
    /// <c>TowedFollowTrackTests</c> owns the proof that the tow is the player's own. What is left is the
    /// WIRING: that the component finds the trailer the region wired, checks she is the body the
    /// timetable names, poses her onto her own transform every frame with her pin on the plate, works the
    /// gear crank the player turns, claims her while she is under way so nobody can pull the pin at
    /// 25 km/h, and lets her go at the end of the day.</para>
    ///
    /// <para><b>⚠️ DRIVEN IN GAME HOURS, NEVER IN FRAMES</b>, and <b>headless-safe by construction</b> —
    /// nothing renders or reads a pixel. Both for the reasons <c>ScheduledTripPlayTests</c> states at
    /// length next door.</para>
    ///
    /// <para><b>The road is a CIRCUIT, and it has to be.</b> A coupled pair cannot pivot in a bay and a
    /// posed body cannot reverse, so a towing trip only exists where both ends are pull-throughs — she
    /// leaves each bay on the heading she arrived on. Nine Mile Creek's roads are a tree and cannot do
    /// that today (see <c>NineMileCreekTowingTests</c>, which measures it); this fixture builds the
    /// geometry a towing trip needs so the RUNTIME is verified while the region catches up.</para>
    /// </summary>
    public class ScheduledTripTowingPlayTests
    {
        const float SecondsPerGameHour = GameConfig.DefaultSecondsPerDay / 24f;
        const float OutboundHour = 6f;
        const float ReturnHour = 15f;
        const string BodyId = "vehiclemesh.test_box_trailer";

        /// <summary>Out of the bay heading north, two bends, and the far bay heading east.</summary>
        static readonly Vector2[] Outbound =
        {
            new(0f, 0f), new(0f, 60f), new(20f, 80f), new(60f, 80f),
        };

        /// <summary>⭐ Home the LONG way round, so both bays are entered on the heading they are left
        /// on.</summary>
        static readonly Vector2[] Circuit =
        {
            new(60f, 80f), new(100f, 80f), new(120f, 40f), new(120f, -40f),
            new(60f, -60f), new(0f, -60f), new(0f, -20f), new(0f, 0f),
        };

        static readonly Vector2 HomePost = new(-6f, -4f);
        static readonly Vector2 FarPost = new(66f, 86f);

        sealed class DrivenClock : IGameClock
        {
            public float Hour;
            public double TotalSeconds => Hour / 24.0 * GameConfig.DefaultSecondsPerDay;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => Hour;
            public float DayFraction => Hour / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        readonly List<Object> _spawned = new();
        DrivenClock _clock;
        ScheduledTrip _trip;
        VehicleTripDef _def;
        GameObject _truck, _driver, _trailerObject;
        ParkedVehicle _parked;
        ParkedTrailer _trailer;
        VehicleMeshDef _tractorMesh, _trailerMesh;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            DriveSeats.Reset();
            _clock = new DrivenClock { Hour = 0f };
            GameServices.Clock = _clock;

            _tractorMesh = TractorMesh();
            _trailerMesh = TrailerMesh();

            _def = ScriptableObject.CreateInstance<VehicleTripDef>();
            _def.Id = "trip.test_haul";
            _def.DisplayName = "Test Haul";
            _def.OutboundDepartureHour = OutboundHour;
            _def.ReturnDepartureHour = ReturnHour;
            _def.CruiseMetresPerSecond = 8f;
            _def.WalkMetresPerSecond = 1.4f;
            _def.TowedBodyId = BodyId;
            _spawned.Add(_def);

            var vehicle = ScriptableObject.CreateInstance<VehicleDef>();
            vehicle.Id = "vehicle.test_tractor";
            vehicle.Mesh = _tractorMesh;
            _spawned.Add(vehicle);

            // ⚠️ INACTIVE first, then Configure, then activate — AddComponent on a LIVE object runs
            // OnEnable before the caller has said what she is (the #556 trap).
            _truck = new GameObject("TestTractor", typeof(Rigidbody2D));
            _truck.SetActive(false);
            _parked = _truck.AddComponent<ParkedVehicle>();
            _parked.Configure(vehicle, drivable: true);
            _spawned.Add(_truck);

            _driver = new GameObject("TestDriver");
            _driver.AddComponent<SpriteRenderer>();
            _spawned.Add(_driver);

            StandTheTrailerCoupleReady();

            _trip = _truck.AddComponent<ScheduledTrip>();
            _trip.Configure(_def, _parked, _driver.transform, Copy(Outbound), Copy(Circuit),
                            HomePost, Vector2.right, FarPost, Vector2.left,
                            _driver.GetComponent<SpriteRenderer>(), null, _trailer);
            _truck.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            DriveSeats.Reset();
            GameServices.Reset();
        }

        static Vector2[] Copy(Vector2[] source) => (Vector2[])source.Clone();

        /// <summary>Stand her where the tractor's own plate would pick her up: pin in the middle of the
        /// capture window, on the heading the road out leaves the bay. Exactly how
        /// <c>NineMileCreekLaydown</c> stands its couple-ready pair, and nothing about it is typed.</summary>
        void StandTheTrailerCoupleReady()
        {
            VehicleFifthWheel wheel = _tractorMesh.FifthWheel;
            var slot = new Vector2(wheel.CouplingPointLocal.x,
                                   (wheel.RampMouthY + wheel.SlotSeatY) * 0.5f);
            Vector2 pin = Outbound[0] + VehicleCouplingMath.LocalOffsetToWorld(slot, 0f);
            Vector2 origin = VehicleCouplingMath.BodyOriginFromKingpin(pin, 0f, _trailerMesh.Kingpin);

            _trailerObject = new GameObject("TestTrailer");
            _trailerObject.SetActive(false);
            _trailerObject.transform.position = new Vector3(origin.x, origin.y, 0f);
            _trailerObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);   // heading 0 = north
            _trailer = _trailerObject.AddComponent<ParkedTrailer>();
            _trailer.Configure(_trailerMesh);
            _spawned.Add(_trailerObject);
            _trailerObject.SetActive(true);
        }

        // ---- the two machines, synthetic on purpose (a fixture pinned to a shipped def goes red every
        //      time the owner tunes her) -----------------------------------------------------------------

        VehicleMeshDef TractorMesh()
        {
            VehicleMeshDef mesh = BaseMesh("vehiclemesh.test_tractor");
            mesh.DriveDoorLocal = new Vector2(-1.2f, 0.4f);
            mesh.FifthWheel = new VehicleFifthWheel
            {
                Published = true,
                CouplingPointLocal = new Vector3(0f, -2.4f, 1.18f),
                SlotHalfWidthMeters = 0.06f,
                SlotMouthY = -2.8f,
                SlotSeatY = -2.4f,
                RampMouthY = -3.02f,
                ReleaseHandleLocal = new Vector2(1.35f, -2.2f),
                CabClearanceMeters = 1.52f,
            };
            return mesh;
        }

        VehicleMeshDef TrailerMesh()
        {
            VehicleMeshDef mesh = BaseMesh(BodyId);
            mesh.Kingpin = new VehicleKingpin
            {
                Published = true,
                CouplingPointLocal = new Vector3(0f, 3.365f, 1.18f),
                NoseSwingRadiusMeters = 1.516f,
                KingpinToAxleCentreMeters = 6.265f,
                TailSwingRadiusMeters = 7.63f,
                NoseHalfWidthMeters = 1.22f,
                KingpinSetMeters = 0.90f,
                PinRadiusMeters = 0.045f,
            };
            mesh.Wheels = new[] { new VehicleFitment { Slot = "LandingGearShoes" } };
            mesh.DoorGroups = new[]
            {
                new VehicleDoorGroup
                {
                    Id = "gear", Work = VehicleDoorWork.LandingGear,
                    Slots = new[] { "LandingGearShoes" },
                },
            };
            return mesh;
        }

        VehicleMeshDef BaseMesh(string id)
        {
            var geometry = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            geometry.triangles = new[] { 0, 1, 2 };
            _spawned.Add(geometry);

            var mesh = ScriptableObject.CreateInstance<VehicleMeshDef>();
            mesh.Id = id;
            mesh.Mesh = geometry;
            mesh.Ramps = new[]
            {
                new HullMeshDef.Ramp { Colors = new Color32[] { new Color32(255, 255, 255, 255) } },
            };
            mesh.Bayer16 = new float[16];
            mesh.PxPerMetre = 32;
            mesh.CellW = 384;
            mesh.CellH = 320;
            mesh.WheelbaseMeters = 4.3f;
            mesh.FrontTrackMeters = 1.8f;
            mesh.FrontAxleY = 2.18f;
            mesh.RearAxleY = -2.12f;
            mesh.MaxInnerSteerDegrees = 30f;
            mesh.MaxOuterSteerDegrees = 24.9372f;
            mesh.WheelRadiusMeters = 0.42f;
            _spawned.Add(mesh);
            return mesh;
        }

        IEnumerator At(float hour)
        {
            _clock.Hour = hour;
            yield return null;
            yield return null;
        }

        TowedBody Body => _trailer.Trailer;

        /// <summary>How far the trailer's pin is from the tractor's plate, metres — the one number that
        /// has to be zero in every frame of a tow.</summary>
        float PinOffPlate()
        {
            float tractor = BoatKinematics.BearingDegrees(_truck.transform.up);
            Vector2 plate = (Vector2)_truck.transform.position +
                            VehicleCouplingMath.LocalOffsetToWorld(
                                new Vector2(_tractorMesh.FifthWheel.CouplingPointLocal.x,
                                            _tractorMesh.FifthWheel.CouplingPointLocal.y), tractor);
            return (Body.KingpinWorld - plate).magnitude;
        }

        // =============================================================================================
        //  THE DAY
        // =============================================================================================

        [UnityTest]
        public IEnumerator OvernightSheStandsOnHerOwnLegsInTheBay()
        {
            yield return At(2f);

            Assert.That(_trip.Plan, Is.Not.Null,
                $"the trip never planned itself: {_trip.LastRefusalReason}");
            Assert.That(_trip.Plan.Tows, Is.True, "the plan dropped the trailer.");
            Assert.That(_trip.LastRefusalReason, Is.Null, "a clean pair refused for some reason.");

            Assert.That(_trip.Pose.TrailerCoupled, Is.False, "she is on the pin overnight.");
            Assert.That(_trip.Pose.TrailerLegsUp, Is.EqualTo(0f),
                "her gear is up while she stands in a bay — that is a trailer resting on her kingpin.");
            Assert.That(Body.IsHeld, Is.False,
                "the trip is holding a trailer that is standing still, so the player could never take " +
                "her out of the yard.");
            Assert.That(Body.LegsAreDown, Is.True);
        }

        [UnityTest]
        public IEnumerator AtHisHourHeWalksToTheReleaseAndNothingHasCoupledYet()
        {
            yield return At(OutboundHour + 0.0005f);

            Assert.That(_trip.Pose.Stage, Is.EqualTo(VehicleTripStage.Coupling),
                "the day's first beat on a towing run is the walk to the handle.");
            Assert.That(_trip.Pose.TrailerCoupled, Is.False,
                "the pin went in before her driver had reached it.");
            Assert.That(Vector2.Distance(_driver.transform.position, HomePost), Is.LessThan(2f),
                "he has only just set off.");
            Assert.That((Vector2)_truck.transform.position,
                        Is.EqualTo(Outbound[0]).Using(Near),
                "she must not move while he walks.");
        }

        [UnityTest]
        public IEnumerator OnTheRoadThePairIsCoupled_HerPinOnThePlateAndHerBodyBehindTheCab()
        {
            float driveAt = _trip.Plan.DepartureHours[VehicleTripPlan.TowedLegDriveOut];
            float half = _trip.Plan.MachineLegs
                              .TravelHours(VehicleTripPlan.TowedLegDriveOut, SecondsPerGameHour) * 0.5f;
            yield return At(driveAt + half);

            Assert.That(_trip.Pose.Stage, Is.EqualTo(VehicleTripStage.Driving));
            Assert.That(_trip.Pose.TrailerCoupled, Is.True);
            Assert.That(_trip.Pose.TrailerLegsUp, Is.EqualTo(1f),
                "she is being dragged along on her shoes.");

            Assert.That(PinOffPlate(), Is.LessThan(0.02f),
                $"her pin is {PinOffPlate():0.000} m off the plate mid-journey — a coupled pair is " +
                "welded at the pin in every frame, and this is the number a plate of the two of them " +
                "would show as a gap.");

            // ⭐ BEHIND the cab, not through it. Her origin has to lie ASTERN of the tractor's, which is
            // the thing a reader of a plate checks first and the thing a mirrored rotation breaks.
            Vector2 astern = (Vector2)_trailerObject.transform.position - (Vector2)_truck.transform.position;
            Assert.That(Vector2.Dot(astern, _truck.transform.up), Is.LessThan(0f),
                $"the trailer is drawn {Vector2.Dot(astern, _truck.transform.up):0.00} m FORWARD of the " +
                "tractor — she is through the cab.");

            Assert.That(Body.IsHeld, Is.True,
                "nobody has claimed her, so a player standing at the right spot could pull the pin on a " +
                "trailer doing 25 km/h.");
            Assert.That(Body.LegsAreDown, Is.False);
        }

        [UnityTest]
        public IEnumerator SheComesHomeAndIsSetDownWhereSheWasFound()
        {
            Vector2 startedAt = _trailerObject.transform.position;
            float startedOn = Body.HeadingDegrees;

            yield return At(_trip.Plan.DepartureHours[VehicleTripPlan.TowedLegAlightAtOrigin] + 0.001f);

            Assert.That(_trip.Pose.TrailerCoupled, Is.False, "her driver walked off with her still on.");
            Assert.That(Body.IsHeld, Is.False, "the trip never let her go.");
            Assert.That(Vector2.Distance(_trailerObject.transform.position, startedAt), Is.LessThan(0.2f),
                $"she was set down {Vector2.Distance(_trailerObject.transform.position, startedAt):0.00} m " +
                "from where the day found her. Nothing about a trailer is saved, so a day that does not " +
                "close teleports her at midnight.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(Body.HeadingDegrees, startedOn)), Is.LessThan(1f),
                "she was set down lying across the bay she was found in.");
        }

        // =============================================================================================
        //  WHEN THE TRAILER IS NOT THERE
        // =============================================================================================

        [UnityTest]
        public IEnumerator WithNoTrailerWiredSheRunsTheErrandBobtailAndSaysWhy()
        {
            _trip.Configure(_def, _parked, _driver.transform, Copy(Outbound), Copy(Circuit),
                            HomePost, Vector2.right, FarPost, Vector2.left,
                            _driver.GetComponent<SpriteRenderer>(), null, null);
            yield return At(2f);

            Assert.That(_trip.Plan, Is.Not.Null, "the whole errand was dropped with the trailer.");
            Assert.That(_trip.Plan.Tows, Is.False);
            Assert.That(_trip.Plan.BlockCount, Is.EqualTo(VehicleTripPlan.LegCount),
                "a bobtail run is the ordinary eight-block day.");
            Assert.That(_trip.LastRefusalReason, Does.Contain(BodyId),
                $"the refusal has to name the body she wanted: '{_trip.LastRefusalReason}'");
        }

        [UnityTest]
        public IEnumerator TheWrongBodyInTheBayIsRefusedByName()
        {
            _def.TowedBodyId = "vehiclemesh.some_other_trailer";
            _trip.Configure(_def, _parked, _driver.transform, Copy(Outbound), Copy(Circuit),
                            HomePost, Vector2.right, FarPost, Vector2.left,
                            _driver.GetComponent<SpriteRenderer>(), null, _trailer);
            yield return At(2f);

            Assert.That(_trip.Plan, Is.Not.Null);
            Assert.That(_trip.Plan.Tows, Is.False, "she hauled a body the timetable does not name.");
            Assert.That(_trip.LastRefusalReason, Does.Contain(BodyId));
            Assert.That(_trip.LastRefusalReason, Does.Contain("some_other_trailer"));
        }

        [UnityTest]
        public IEnumerator WhenTheLoadIsThePointSheStaysHome()
        {
            _def.WhenTheTrailerIsNotThere = TrailerAbsence.StayHome;
            _trip.Configure(_def, _parked, _driver.transform, Copy(Outbound), Copy(Circuit),
                            HomePost, Vector2.right, FarPost, Vector2.left,
                            _driver.GetComponent<SpriteRenderer>(), null, null);
            yield return At(2f);

            Assert.That(_trip.Plan, Is.Null,
                "the timetable says the load is the whole point and she went anyway.");
            Assert.That((Vector2)_truck.transform.position, Is.EqualTo(Outbound[0]).Using(Near),
                "a trip that does not plan must keep the spot the builder placed her on.");
        }

        [UnityTest]
        public IEnumerator ATrailerSomebodyElseHasCoupledIsLeftAlone()
        {
            // The player backs another tractor under her overnight. The run must NOT fight for her
            // transform: two writers on one body is exactly what the seat claim next door prevents for
            // a wheel, and a trailer is no different.
            var other = new GameObject("OtherTractor", typeof(Rigidbody2D));
            other.SetActive(false);
            VehicleHitch hitch = other.AddComponent<VehicleHitch>();
            hitch.Configure(_tractorMesh, null, "vehicle.other_tractor");
            _spawned.Add(other);
            other.SetActive(true);

            Assert.That(hitch.Couple(Body), Is.True, "the fixture could not take the trailer.");
            Vector2 whereHeLeftHer = _trailerObject.transform.position;

            // …and now the run's own drive hour comes round.
            float driveAt = _trip.Plan.DepartureHours[VehicleTripPlan.TowedLegDriveOut];
            float half = _trip.Plan.MachineLegs
                              .TravelHours(VehicleTripPlan.TowedLegDriveOut, SecondsPerGameHour) * 0.5f;
            yield return At(driveAt + half);

            Assert.That(Vector2.Distance(_trailerObject.transform.position, whereHeLeftHer),
                        Is.LessThan(0.01f),
                "the scheduled run posed a trailer that is on somebody else's pin — two clocks, one " +
                "transform.");
            Assert.That(_trip.LastRefusalReason, Does.Contain("bobtail"),
                $"the run has to say it is going without her: '{_trip.LastRefusalReason}'");
            Assert.That(_truck.transform.position.y, Is.GreaterThan(10f),
                "the errand itself was abandoned — she should still make her run, just without a load.");
        }

        static readonly System.Collections.IComparer Near = new NearComparer();

        sealed class NearComparer : System.Collections.IComparer
        {
            public int Compare(object x, object y)
            {
                if (x is Vector2 a && y is Vector2 b) return Vector2.Distance(a, b) <= 0.2f ? 0 : 1;
                return 1;
            }
        }
    }
}
