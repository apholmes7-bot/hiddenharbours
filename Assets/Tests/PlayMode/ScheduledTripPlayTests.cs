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
    /// ⭐ <b>A VILLAGER WALKS TO A TRUCK, GETS IN, DRIVES, PARKS AND GETS OUT</b> — the owner's ask, over
    /// real frames, on a real <see cref="ScheduledTrip"/> driven by a clock this test moves by hand.
    ///
    /// <para><b>What only PlayMode can show.</b> The rules are EditMode's — <c>VehicleTripPlanTests</c>
    /// owns the closed form and <c>NineMileCreekTripsTests</c> owns the creek's own geometry. What is left
    /// is the WIRING, which is where a seam actually fails: that the component plans itself once the
    /// services are up, that it writes the machine's pose AND her heading onto her root every frame, that
    /// her driver is hidden while he is in the cab and shown again when he is not, that his talk point
    /// goes with him, and that her wheel is claimed for the whole trip so the player is not offered a
    /// truck that is about to pull away.</para>
    ///
    /// <para><b>⚠️ DRIVEN IN GAME HOURS, NEVER IN FRAMES.</b> A frame count is not time, and least of all
    /// headless where <c>yield return null</c> spins as fast as the box allows. The clock is a fake this
    /// test SETS, so "she is on the road at 05:00" is asserted by putting the clock at 05:00.</para>
    ///
    /// <para><b>Headless-safe by construction (⚠️ do not relax this).</b> Nothing renders, reads pixels or
    /// calls <c>Camera.Render</c>: CI runs with a null graphics device, where a ReadPixels PlayMode test
    /// does not fail — it KILLS the editor with no results XML. Every assertion is on STATE.</para>
    ///
    /// <para><b>Synthetic geometry, on purpose.</b> A straight 200 m road between two bays exercises the
    /// same component the creek does, and a resident region reds other files. Nothing here loads a scene
    /// or an asset.</para>
    /// </summary>
    public class ScheduledTripPlayTests
    {
        const float SecondsPerGameHour = GameConfig.DefaultSecondsPerDay / 24f;   // 75 s at the pinned day
        const float OutboundHour = 5f;
        const float ReturnHour = 20f;

        static readonly Vector2 HomeBay = new(0f, 0f);
        static readonly Vector2 FarBay = new(200f, 0f);
        static readonly Vector2 ParkPost = new(0f, 5f);
        static readonly Vector2 WharfPost = new(200f, 7f);

        /// <summary>A clock this test moves by hand. Only the hour matters to a trip.</summary>
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
        GameObject _truck;
        GameObject _driver;
        SpriteRenderer _driverRenderer;
        Behaviour _driverTalkable;
        ParkedVehicle _parked;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            DriveSeats.Reset();
            _clock = new DrivenClock { Hour = 0f };
            GameServices.Clock = _clock;

            _def = ScriptableObject.CreateInstance<VehicleTripDef>();
            _def.Id = "trip.test_run";
            _def.DisplayName = "Test Run";
            _def.OutboundDepartureHour = OutboundHour;
            _def.ReturnDepartureHour = ReturnHour;
            _def.CruiseMetresPerSecond = 8f;
            _def.WalkMetresPerSecond = 1.4f;
            _spawned.Add(_def);

            // A machine with a USABLE mesh — the door lives there (a trip with no door has nowhere to
            // walk to) and so does everything VehicleMeshDef.IsUsable checks, which is what makes her
            // IsDrivable and therefore what makes the seat claim below mean anything. Synthetic rather
            // than the shipped asset, for DriveModePlayTests' reason: a fixture pinned to a shipped def
            // would go red every time the owner tuned her.
            var geometry = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            geometry.triangles = new[] { 0, 1, 2 };
            _spawned.Add(geometry);

            var mesh = ScriptableObject.CreateInstance<VehicleMeshDef>();
            mesh.Mesh = geometry;
            mesh.Ramps = new[]
            {
                new HullMeshDef.Ramp { Colors = new Color32[] { new Color32(255, 255, 255, 255) } },
            };
            mesh.Bayer16 = new float[16];
            mesh.PxPerMetre = 32;
            mesh.CellW = 384;
            mesh.CellH = 320;
            mesh.DriveDoorLocal = new Vector2(-1.75f, 0.10f);
            mesh.WheelbaseMeters = 4.3f;
            mesh.FrontTrackMeters = 1.8f;
            mesh.FrontAxleY = 2.18f;
            mesh.RearAxleY = -2.12f;
            mesh.MaxInnerSteerDegrees = 30f;
            mesh.MaxOuterSteerDegrees = 24.9372f;
            mesh.WheelRadiusMeters = 0.42f;
            _spawned.Add(mesh);

            var vehicle = ScriptableObject.CreateInstance<VehicleDef>();
            vehicle.Id = "vehicle.test_truck";
            vehicle.Mesh = mesh;
            _spawned.Add(vehicle);

            // ⚠️ INACTIVE first, then Configure, then activate — AddComponent on a LIVE object runs
            // OnEnable before the caller has said what she is (the #556 trap that took out five fixtures).
            _truck = new GameObject("TestTruck", typeof(Rigidbody2D));
            _truck.SetActive(false);
            _parked = _truck.AddComponent<ParkedVehicle>();
            _parked.Configure(vehicle, drivable: true);
            _spawned.Add(_truck);

            _driver = new GameObject("TestDriver");
            _driverRenderer = _driver.AddComponent<SpriteRenderer>();
            _driverTalkable = _driver.AddComponent<TalkStub>();
            _spawned.Add(_driver);

            _trip = _truck.AddComponent<ScheduledTrip>();
            _trip.Configure(_def, _parked, _driver.transform,
                            new[] { HomeBay, FarBay }, new[] { FarBay, HomeBay },
                            ParkPost, Vector2.down, WharfPost, Vector2.down,
                            _driverRenderer, _driverTalkable);
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

        /// <summary>Stand-in for whatever the player walks up to and talks to. A plain
        /// <see cref="Behaviour"/> is all <see cref="ScheduledTrip"/> asks for, deliberately: Vehicles may
        /// not name the World module's <c>Interactable</c> (rule 4).</summary>
        sealed class TalkStub : MonoBehaviour { }

        IEnumerator At(float hour)
        {
            _clock.Hour = hour;
            // Two frames: the first is the one the component reads the new hour on, the second lets
            // anything that reacts to what it wrote land before the assertion.
            yield return null;
            yield return null;
        }

        // =============================================================================================
        //  THE DAY
        // =============================================================================================

        [UnityTest]
        public IEnumerator SheRestsInHerBayOvernightWithHerDriverBesideHer()
        {
            yield return At(2f);

            Assert.That(_trip.Plan, Is.Not.Null, "the trip never planned itself — see the warning it logs.");
            Assert.That((Vector2)_truck.transform.position, Is.EqualTo(HomeBay).Using(Near));
            Assert.That((Vector2)_driver.transform.position, Is.EqualTo(ParkPost).Using(Near));
            Assert.That(_driverRenderer.enabled, Is.True, "nobody is aboard — he must be drawn.");
            Assert.That(_trip.Pose.Stage, Is.EqualTo(VehicleTripStage.Resting));
        }

        [UnityTest]
        public IEnumerator AtHisHourHeWalksToHerDoorAndSheHasNotMoved()
        {
            yield return At(OutboundHour + 0.0005f);

            Assert.That(_trip.Pose.Stage, Is.EqualTo(VehicleTripStage.Boarding));
            Assert.That((Vector2)_truck.transform.position, Is.EqualTo(HomeBay).Using(Near),
                "she must not pull away before he is in.");
            Assert.That(_driverRenderer.enabled, Is.True);
            Assert.That(Vector2.Distance(_driver.transform.position, ParkPost), Is.LessThan(2f),
                "he has only just set off.");
        }

        [UnityTest]
        public IEnumerator OnTheRoadSheIsUnderWayAndNobodyIsWalkingBesideHer()
        {
            float driveAt = _trip.Plan.DepartureHours[VehicleTripPlan.LegDriveOut];
            float half = _trip.Plan.MachineLegs.TravelHours(VehicleTripPlan.LegDriveOut, SecondsPerGameHour)
                         * 0.5f;
            yield return At(driveAt + half);

            Assert.That(_trip.Pose.Stage, Is.EqualTo(VehicleTripStage.Driving));
            Assert.That(_truck.transform.position.x, Is.EqualTo(100f).Within(6f),
                "halfway through the drive she should be halfway down the road.");
            Assert.That(_driverRenderer.enabled, Is.False, "he is in the cab — he must not be drawn.");
            Assert.That(_driverTalkable.enabled, Is.False,
                "a villager who answers from inside a truck is the through-the-wall defect with a door "
                + "in place of the wall.");
            Assert.That(_truck.transform.up.x, Is.GreaterThan(0.9f),
                "her nose must follow the direction she is travelling — transform.up is the fleet's one "
                + "heading convention and the mesh driver reads it back.");
        }

        [UnityTest]
        public IEnumerator SheParksAtTheFarBayAndHeIsAtHisPost()
        {
            yield return At(12f);

            Assert.That(_trip.Pose.Stage, Is.EqualTo(VehicleTripStage.Resting));
            Assert.That((Vector2)_truck.transform.position, Is.EqualTo(FarBay).Using(Near));
            Assert.That((Vector2)_driver.transform.position, Is.EqualTo(WharfPost).Using(Near));
            Assert.That(_driverRenderer.enabled, Is.True);
            Assert.That(_driverTalkable.enabled, Is.True, "he is at his post — the player may talk to him.");
        }

        [UnityTest]
        public IEnumerator SheIsBackInHerOwnBayByTheEndOfTheDay()
        {
            yield return At(23.5f);

            Assert.That((Vector2)_truck.transform.position, Is.EqualTo(HomeBay).Using(Near));
            Assert.That((Vector2)_driver.transform.position, Is.EqualTo(ParkPost).Using(Near));
            Assert.That(_driverRenderer.enabled, Is.True);
        }

        // =============================================================================================
        //  RE-DERIVATION — the save/load claim, over real frames
        // =============================================================================================

        /// <summary>
        /// ⭐ A save taken mid-trip has nothing to save. The proof: put the clock BACK to an hour already
        /// visited after driving the whole day through, and she is where that hour says — a component
        /// that had integrated anything would be somewhere else.
        /// </summary>
        [UnityTest]
        public IEnumerator SeekingTheClockBackwardsPutsHerWhereThatHourSays()
        {
            float driveAt = _trip.Plan.DepartureHours[VehicleTripPlan.LegDriveOut];
            float third = _trip.Plan.MachineLegs.TravelHours(VehicleTripPlan.LegDriveOut, SecondsPerGameHour)
                          / 3f;

            yield return At(driveAt + third);
            Vector2 first = _truck.transform.position;

            // Live the rest of the day, then come back.
            for (float h = 6f; h < 24f; h += 2f) yield return At(h);
            yield return At(driveAt + third);

            Assert.That((Vector2)_truck.transform.position, Is.EqualTo(first).Using(Near),
                "she is not where the clock says — something in the trip is accumulating, and a save "
                + "would have to carry it.");
        }

        // =============================================================================================
        //  THE SEAT — she is her driver's for the whole trip
        // =============================================================================================

        [UnityTest]
        public IEnumerator HerWheelIsClaimedFromTheWalkToTheDoorUntilHeIsBackOnHisFeet()
        {
            yield return At(2f);
            Assert.That(DriveSeats.IsOccupied(_parked.Door), Is.False,
                "parked and empty overnight — anybody may take her.");
            Assert.That(_parked.Door.IsAvailable, Is.True);

            yield return At(OutboundHour + 0.0005f);
            Assert.That(DriveSeats.IsOccupied(_parked.Door), Is.True,
                "her driver is three metres away and closing — she is not a truck to offer the player.");
            Assert.That(_parked.Door.IsAvailable, Is.False,
                "an unavailable door is neither resolved nor highlighted: a refusal by silence, not a "
                + "notice.");

            yield return At(12f);
            Assert.That(DriveSeats.IsOccupied(_parked.Door), Is.False,
                "she is parked at the wharf with nobody in her — the player may drive her.");
            Assert.That(_parked.Door.IsAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator ADisabledTripGivesHerWheelBackAndLeavesNobodyHidden()
        {
            float driveAt = _trip.Plan.DepartureHours[VehicleTripPlan.LegDriveOut];
            yield return At(driveAt + 0.01f);
            Assert.That(_driverRenderer.enabled, Is.False);
            Assert.That(DriveSeats.IsOccupied(_parked.Door), Is.True);

            _trip.enabled = false;
            yield return null;

            Assert.That(DriveSeats.IsOccupied(_parked.Door), Is.False,
                "a trip switched off mid-drive must not hold her wheel for ever.");
            Assert.That(_driverRenderer.enabled, Is.True,
                "never leave a villager hidden — an invisible, un-talkable person reads as broken "
                + "dialogue, not as somebody who is out.");
            Assert.That(_driverTalkable.enabled, Is.True);
        }

        // =============================================================================================
        //  HELPERS
        // =============================================================================================

        /// <summary>Vector2 equality to the centimetre — a pose written through a transform does not come
        /// back bit-identical and a test that demanded it would be measuring the FPU.</summary>
        static readonly System.Collections.IComparer Near = new NearComparer();

        sealed class NearComparer : System.Collections.IComparer
        {
            public int Compare(object x, object y)
            {
                if (x is Vector2 a && y is Vector2 b) return Vector2.Distance(a, b) <= 0.01f ? 0 : 1;
                return 1;
            }
        }
    }
}
