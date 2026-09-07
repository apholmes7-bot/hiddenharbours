using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>GET ON, RIDE AWAY, GET OFF BESIDE HER</b> — the whole verb the owner asked for
    /// (2026-09-07, ruling (d) of the ATV pack: the PLAYER may ride one), under a running physics
    /// pump, on the quad that now stands outside the shop at St Peters.
    ///
    /// <para><b>What is NOT here, deliberately.</b> <see cref="AtvSaddleMountPlayTests"/> already
    /// settles the two ways on and the tie-broken dismount, and it does it standing still. This
    /// fixture only asks the thing that needs the machine to actually MOVE: that the seam carries a
    /// demand from the player's hands to a machine's wheels for thirty metres, that the rider stays on
    /// the saddle for all of them, and that when she gets off she is on the ground beside the machine
    /// and the machine is stopped rather than rolling on across the green.</para>
    ///
    /// <para><b>⚠️ The bar is the ODOMETER, not the frame count.</b> A fixture that rode "for 400
    /// frames" is measuring the machine it happens to run on
    /// (<c>playmode-frames-buy-hardware-not-time</c>): the loop below runs until she has covered the
    /// distance and fails loudly at a step cap, which is how <see cref="RoadFleetJourneyPlayTests"/>
    /// pumps every leg it drives.</para>
    ///
    /// <para><b>And no key is ever pressed</b> — a headless fixture cannot drive the keyboard, so the
    /// demand is held on a <see cref="HeldDriveInput"/> through the seam the real keyboard source
    /// implements. <c>Reads</c> is asserted so a ride that moved for some other reason cannot pass as
    /// a ride the player asked for.</para>
    /// </summary>
    public class AtvPlayerRidePlayTests
    {
        // ---- the quad's committed art (atvIsoRig.atvPack.gameplay.json → bodies.quad) ---------------
        static readonly Vector3 QuadSeat = new Vector3(0f, -0.30f, 0.90f);    // SADDLE.seat_ref
        static readonly Vector2 StreetSide = new Vector2(-1.19f, -0.30f);     // ride.reach_point
        static readonly Vector2 CurbSide = new Vector2(1.19f, -0.30f);        // ride.alt_reach_point
        const float Wheelbase = 1.28f;                                        // SPECS.quad axF − axR
        const float FrontTrack = 0.96f;
        const float WheelRadius = 0.315f;
        const float InnerLock = 30f, OuterLock = 21.94f;

        // ---- her envelope, as this PR tunes it (Data/Vehicles/UtilityQuad.asset) --------------------
        const float TopSpeed = 9f;
        const float Accel = 4f;
        const float Braking = 6.5f;
        const float Coast = 3f;

        const float PoseSeatZ = 0.40f;                 // OffDeck_mounts.json → drive.seatZ
        const float RideMetres = 30f;
        const int MaxSteps = 3000;                     // 60 s of physics — fails loudly, never hangs
        const int MaxStepsToStop = 400;
        const int Directions = 8, DriveFrames = 6;

        readonly List<Object> _spawned = new();
        HeldDriveInput _held;

        sealed class DryGround : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 6f;   // St Peters is +6 m: dry at every tide
        }

        sealed class StillWater : IEnvironmentService
        {
            public int WorldSeed => 12345;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            EventBus.Clear<ControlModeChanged>();
            GameServices.Environment = new StillWater();
            GameServices.TidalTerrain = new DryGround();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveVehicleChanged>();
            EventBus.Clear<DriveSeatRequested>();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            GameServices.Reset();
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- the fixture ----------------------------------------------------------------------------

        sealed class Rig
        {
            public ControlSwitcher Switcher;
            public PlayerDrivePresenter Drive;
            public VehicleDoor Door;
            public VehicleController Controller;
            public GameObject PlayerGo, MachineGo;
        }

        Sprite[] Sheet(int count)
        {
            var set = new Sprite[count];
            for (int i = 0; i < set.Length; i++)
            {
                var tex = new Texture2D(4, 8);
                _spawned.Add(tex);
                Sprite s = Sprite.Create(tex, new Rect(0, 0, 4, 8), new Vector2(0.5f, 0.1f), 32f);
                _spawned.Add(s);
                set[i] = s;
            }
            return set;
        }

        Rig BuildQuad(Vector2 standAt)
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            mesh.triangles = new[] { 0, 1, 2 };
            _spawned.Add(mesh);

            var meshDef = ScriptableObject.CreateInstance<VehicleMeshDef>();
            _spawned.Add(meshDef);
            meshDef.Id = "vehiclemesh.utility_quad";
            meshDef.Mesh = mesh;
            meshDef.Ramps = new[]
                { new HullMeshDef.Ramp { Colors = new Color32[] { new Color32(255, 255, 255, 255) } } };
            meshDef.Bayer16 = new float[16];
            meshDef.PxPerMetre = 32;
            meshDef.CellW = 256;
            meshDef.CellH = 192;
            meshDef.WheelbaseMeters = Wheelbase;
            meshDef.FrontTrackMeters = FrontTrack;
            meshDef.WheelRadiusMeters = WheelRadius;
            meshDef.FrontAxleY = 0.64f;
            meshDef.RearAxleY = -0.64f;
            meshDef.MaxInnerSteerDegrees = InnerLock;
            meshDef.MaxOuterSteerDegrees = OuterLock;
            meshDef.DriveDoorLocal = StreetSide;
            meshDef.AltDriveDoorLocal = CurbSide;
            meshDef.DriverSeatLocal = QuadSeat;
            meshDef.WayInInteractId = "ride";           // her INTERACT block publishes `ride`

            var vehicleDef = ScriptableObject.CreateInstance<VehicleDef>();
            _spawned.Add(vehicleDef);
            vehicleDef.Id = "vehicle.utility_quad";
            vehicleDef.KindToken = "road_vehicle";
            vehicleDef.Mesh = meshDef;
            vehicleDef.MassKg = 300f;
            vehicleDef.MaxSpeedMetersPerSecond = TopSpeed;
            vehicleDef.AccelerationMetersPerSecondSquared = Accel;
            vehicleDef.BrakingMetersPerSecondSquared = Braking;
            vehicleDef.CoastDecelerationMetersPerSecondSquared = Coast;
            vehicleDef.CameraWorldHeightMeters = 12f;

            var machineGo = new GameObject("UtilityQuad", typeof(Rigidbody2D));
            _spawned.Add(machineGo);
            machineGo.GetComponent<Rigidbody2D>().gravityScale = 0f;
            VehicleController controller = machineGo.AddComponent<VehicleController>();
            controller.SetVehicle(vehicleDef);
            VehicleDoor door = machineGo.AddComponent<VehicleDoor>();

            var playerGo = new GameObject("Fisher", typeof(SpriteRenderer), typeof(Rigidbody2D));
            _spawned.Add(playerGo);
            playerGo.transform.position =
                machineGo.transform.TransformPoint(new Vector3(standAt.x, standAt.y, 0f));
            PlayerWalkController walk = playerGo.AddComponent<PlayerWalkController>();

            var skin = ScriptableObject.CreateInstance<CharacterVisualDef>();
            _spawned.Add(skin);
            skin.Id = "visual.rider_ride";
            skin.FacingCount = Directions;
            skin.IdleFrameCount = 4;
            skin.IdleSheet = Sheet(Directions * 4);
            skin.DriveClip = new CharacterClipSheets
            {
                FrameCount = DriveFrames, FramesPerSecond = 1000f / 170f, Loops = true,
                Sheet = Sheet(Directions * DriveFrames),
            };

            playerGo.AddComponent<IsoCharacterSprite>().Configure(skin);
            playerGo.AddComponent<CharacterClipPlayer>();

            var mounts = ScriptableObject.CreateInstance<CharacterOffDeckMountsDef>();
            _spawned.Add(mounts);
            mounts.Id = "offdeckmounts.rider_ride";
            mounts.DriveSeatZ = PoseSeatZ;

            PlayerDrivePresenter drive = playerGo.AddComponent<PlayerDrivePresenter>();
            drive.ConfigureMounts(mounts);

            var switcherGo = new GameObject("Switcher");
            _spawned.Add(switcherGo);
            ControlSwitcher switcher = switcherGo.AddComponent<ControlSwitcher>();
            switcher.Configure(walk, null, null, null, 0f, null);
            _held = new HeldDriveInput();
            switcher.ConfigureDriveInput(_held);

            return new Rig
            {
                Switcher = switcher, Drive = drive, Door = door, Controller = controller,
                PlayerGo = playerGo, MachineGo = machineGo,
            };
        }

        static Vector2 World(Rig rig, Vector2 local) =>
            rig.MachineGo.transform.TransformPoint(new Vector3(local.x, local.y, 0f));

        // =============================================================================================
        //  THE RIDE
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>The owner's ask, end to end.</b> She walks to the machine's published mount point, is
        /// offered her, gets on, rides thirty metres, and steps off onto the ground beside a machine
        /// that has stopped.
        ///
        /// <para>The rider's position is checked EVERY step of the ride, not once at the end: the
        /// switcher re-seats her on the machine's root in its <c>LateUpdate</c> and the presenter
        /// offsets her onto the saddle in its own at order 100, so "she is on the saddle" is a claim
        /// about every frame and a single sample at the end would miss a rider who spent the ride
        /// snapping between two places.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SheRidesThirtyMetresAndStepsOffBesideAMachineAtRest()
        {
            Rig rig = BuildQuad(standAt: StreetSide);
            yield return null;

            // She is offered the machine where she stands — through the real resolver, never a
            // re-implementation of its distance rule.
            Assert.IsTrue(InteractResolver.TryResolve(
                    Interactables.Active,
                    new InteractActor(rig.PlayerGo.transform.position, Vector2.zero,
                                      InteractContext.OnFoot),
                    360f, out IInteractable offered),
                "standing at the quad's own published mount point she was offered nothing.");
            Assert.That(offered.VerbLabel, Is.EqualTo(VehicleDoor.SaddleVerb),
                "the popup offers her something a quad does not have. You GET ON one.");

            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door), "she could not get on.");
            yield return null;
            Assert.IsTrue(rig.Drive.IsShowing,
                "nobody is drawn on her — a quad has no inside to be in.");

            Vector2 from = rig.MachineGo.transform.position;
            float odometerAt = rig.Controller.OdometerMeters;
            _held.Set(1f, 0f, false);

            int steps = 0;
            float worstSeatMiss = 0f;
            while (rig.Controller.OdometerMeters - odometerAt < RideMetres)
            {
                Assert.That(steps++, Is.LessThan(MaxSteps),
                    $"{MaxSteps} physics steps at full throttle and she has covered only " +
                    $"{rig.Controller.OdometerMeters - odometerAt:0.##} m — the seam is not carrying " +
                    "the demand from the player's hands to the wheels.");
                yield return new WaitForFixedUpdate();

                // She is ON the saddle for the whole ride, not only at the end of it. The seat is on
                // the centreline, 0.30 m aft, so the check is against the machine's own live seat.
                Vector2 seat = World(rig, new Vector2(QuadSeat.x, QuadSeat.y));
                worstSeatMiss = Mathf.Max(
                    worstSeatMiss, Vector2.Distance(rig.PlayerGo.transform.position, seat));
            }

            Assert.That(_held.Reads, Is.GreaterThan(0),
                "the switcher never asked the input source — the ride happened for some other reason.");
            Assert.That(worstSeatMiss, Is.LessThan(1f),
                $"the rider drifted {worstSeatMiss:0.##} m off the saddle during the ride. Half a " +
                "metre is already the whole seat.");
            Assert.That(Vector2.Distance(from, rig.MachineGo.transform.position),
                Is.GreaterThanOrEqualTo(RideMetres - 1f),
                "her odometer says thirty metres and she has not gone anywhere — the odometer is " +
                "counting something other than ground covered.");

            // ---- off ---------------------------------------------------------------------------
            _held.Release();
            Assert.IsTrue(rig.Switcher.LeaveDriving(), "she could not get off on dry ground.");
            yield return null;

            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.OnFoot));
            Assert.IsFalse(rig.Drive.IsShowing, "still drawing a rider on a machine nobody is on.");

            Vector2 landed = rig.PlayerGo.transform.position;
            float toStreet = Vector2.Distance(landed, World(rig, StreetSide));
            float toCurb = Vector2.Distance(landed, World(rig, CurbSide));

            Assert.That(Mathf.Min(toStreet, toCurb), Is.LessThan(0.25f),
                "she was set down somewhere that is neither of her two published sides.");
            Assert.That(Vector2.Distance(landed, rig.MachineGo.transform.position),
                Is.GreaterThan(1f),
                "she is standing IN the machine she just got off. Her sides are 1.19 m out.");

            // ---- and the machine stays where she left her ---------------------------------------
            int stopping = 0;
            while (Mathf.Abs(rig.Controller.SpeedMetersPerSecond) > 0.01f)
            {
                Assert.That(stopping++, Is.LessThan(MaxStepsToStop),
                    $"she is still doing {rig.Controller.SpeedMetersPerSecond:0.##} m/s " +
                    $"{MaxStepsToStop} steps after the rider stepped off. Leaving a machine must " +
                    "leave her PARKED, not coasting off across the green.");
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(rig.Controller.Brake,
                "the handbrake is off on a machine nobody is on — ReleaseControls leaves it ON " +
                "deliberately, so a quad the player stepped away from is where they left her.");
            Assert.That(rig.Controller.Throttle, Is.EqualTo(0f).Within(1e-4f));
        }

        /// <summary>
        /// <b>Getting off at the far end puts her down at the far end</b> — the door travels with the
        /// machine, which is the whole reason <see cref="IDriveSeat.DoorWorldPosition"/> is a property
        /// read live rather than a point latched when she got on.
        /// </summary>
        [UnityTest]
        public IEnumerator TheWayOffTravelsWithTheMachine()
        {
            Rig rig = BuildQuad(standAt: CurbSide);
            yield return null;

            Vector2 mountedAt = rig.PlayerGo.transform.position;
            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door));

            float odometerAt = rig.Controller.OdometerMeters;
            _held.Set(1f, 0f, false);

            int steps = 0;
            while (rig.Controller.OdometerMeters - odometerAt < RideMetres)
            {
                Assert.That(steps++, Is.LessThan(MaxSteps), "she never got going.");
                yield return new WaitForFixedUpdate();
            }

            _held.Release();
            Assert.IsTrue(rig.Switcher.LeaveDriving());
            yield return null;

            Assert.That(Vector2.Distance(rig.PlayerGo.transform.position, mountedAt),
                Is.GreaterThan(RideMetres - 2f),
                "she stepped off back where she got on, thirty metres behind the machine. The door " +
                "is a place on a machine that MOVES.");
        }
    }
}
