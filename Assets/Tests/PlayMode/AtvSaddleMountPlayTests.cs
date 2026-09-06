using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Vehicles;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>ON FROM EITHER SIDE, OFF OVER THE STAND</b> — the saddle mount under a running frame pump,
    /// which is the only place the ORDER of it can be seen.
    ///
    /// <para><b>What the EditMode half already settles</b> (<c>AtvSaddleMountTests</c>): that both
    /// published sides resolve, that a cab grows no second one, that the two candidates carry distinct
    /// ids and ask for the same seat. All of that is one frame's worth of arithmetic.</para>
    ///
    /// <para><b>What only a pump settles, and it is the interesting half.</b> After a ride the rider is
    /// not standing beside the machine — she has been re-seated onto the machine's own root by
    /// <c>ControlSwitcher.RideVehicle</c> and then offset onto the SADDLE by
    /// <c>PlayerDrivePresenter</c>, every frame. Every ATV's seat is on the centreline
    /// (<c>seat_ref.x == 0</c> on all three), so at the moment she steps off she is <b>exactly
    /// equidistant from her two doors</b>. The dismount is therefore always a TIE, and the tie-break is
    /// doing the whole job — which is why <see cref="DriveSeatSides.NearestDoor"/> compares with
    /// <c>&lt;</c> rather than <c>&lt;=</c> and the art's preferred side wins.</para>
    ///
    /// <para>⚠️ That is not a shortcoming, it is the ruling: <b>the enduro's stand is on the street
    /// side and a rider steps off over it</b>. This fixture pins the outcome (she lands street side, and
    /// she lands there having got on from the CURB) so that a later change to the tie — or a seat that
    /// stops being centred — cannot quietly start putting her down through her own machine.</para>
    ///
    /// <para><b>No key is ever pressed</b> — a headless fixture cannot drive the keyboard, so the gates
    /// are worked directly, the shape <see cref="DriverAtTheWheelPlayTests"/> uses.</para>
    /// </summary>
    public class AtvSaddleMountPlayTests
    {
        // ---- the enduro's committed art (atvIsoRig.atvPack.gameplay.json → bodies.dirtbike) ----------
        const float PoseSeatZ = 0.40f;                                   // OffDeck_mounts.json drive.seatZ
        static readonly Vector3 BikeSeat = new Vector3(0f, -0.30f, 0.94f);   // SADDLE.seat_ref
        static readonly Vector2 StreetSide = new Vector2(-0.98f, -0.30f);    // ride.reach_point
        static readonly Vector2 CurbSide = new Vector2(0.98f, -0.30f);       // ride.alt_reach_point

        /// <summary>A cab, for the arm that must not change: one door, no alternate.</summary>
        static readonly Vector2 CabDoor = new Vector2(-1.75f, 0.10f);

        const int Directions = 8;
        const int DriveFrames = 6;

        readonly List<Object> _spawned = new();

        sealed class DryGround : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 4f;   // land everywhere: water is her wall
        }

        sealed class StillWater : IEnvironmentService
        {
            public int WorldSeed => 4242;
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
            public GameObject PlayerGo, MachineGo;
        }

        Sprite[] Sheet(int count)
        {
            var set = new Sprite[count];
            for (int i = 0; i < set.Length; i++)
            {
                var tex = new Texture2D(4, 8);
                _spawned.Add(tex);
                var s = Sprite.Create(tex, new Rect(0, 0, 4, 8), new Vector2(0.5f, 0.1f), 32f);
                _spawned.Add(s);
                set[i] = s;
            }
            return set;
        }

        /// <summary>Build a machine and a fisher standing at <paramref name="standAt"/> (machine-local).
        /// <paramref name="alt"/> of <see cref="Vector2.zero"/> is the "one way on" sentinel — a cab.</summary>
        Rig BuildRig(Vector2 door, Vector2 alt, Vector3 seat, Vector2 standAt,
                     float machineHeadingDeg = 0f)
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            mesh.triangles = new[] { 0, 1, 2 };
            _spawned.Add(mesh);

            var meshDef = ScriptableObject.CreateInstance<VehicleMeshDef>();
            _spawned.Add(meshDef);
            meshDef.Id = "vehiclemesh.enduro_250";
            meshDef.Mesh = mesh;
            meshDef.Ramps = new[]
                { new HullMeshDef.Ramp { Colors = new Color32[] { new Color32(255, 255, 255, 255) } } };
            meshDef.Bayer16 = new float[16];
            meshDef.PxPerMetre = 32;
            meshDef.CellW = 256;
            meshDef.CellH = 192;
            meshDef.WheelbaseMeters = 1.48f;      // SPECS.dirtbike — single-track, so the front track is
            meshDef.FrontTrackMeters = 0f;        // a MEASURED zero, not a placeholder
            meshDef.WheelRadiusMeters = 0.33f;    // the REAR radius: roll is revolutions of the rear wheel
            meshDef.FrontAxleY = 0.74f;
            meshDef.RearAxleY = -0.74f;
            meshDef.MaxInnerSteerDegrees = 35f;
            meshDef.MaxOuterSteerDegrees = 35f;
            meshDef.DriveDoorLocal = door;
            meshDef.AltDriveDoorLocal = alt;
            meshDef.DriverSeatLocal = seat;

            var vehicleDef = ScriptableObject.CreateInstance<VehicleDef>();
            _spawned.Add(vehicleDef);
            vehicleDef.Id = "vehicle.enduro_250";
            vehicleDef.KindToken = "road_vehicle";
            vehicleDef.Mesh = meshDef;
            vehicleDef.CameraWorldHeightMeters = 18f;

            var machineGo = new GameObject("Enduro250", typeof(Rigidbody2D));
            _spawned.Add(machineGo);
            machineGo.transform.rotation = Quaternion.Euler(0f, 0f, -machineHeadingDeg);
            machineGo.AddComponent<VehicleController>().SetVehicle(vehicleDef);
            var vDoor = machineGo.AddComponent<VehicleDoor>();

            var playerGo = new GameObject("Fisher", typeof(SpriteRenderer), typeof(Rigidbody2D));
            _spawned.Add(playerGo);
            playerGo.transform.position =
                machineGo.transform.TransformPoint(new Vector3(standAt.x, standAt.y, 0f));
            var walk = playerGo.AddComponent<PlayerWalkController>();

            var skin = ScriptableObject.CreateInstance<CharacterVisualDef>();
            _spawned.Add(skin);
            skin.Id = "visual.rider_play";
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
            mounts.Id = "offdeckmounts.rider_play";
            mounts.DriveSeatZ = PoseSeatZ;

            var drive = playerGo.AddComponent<PlayerDrivePresenter>();
            drive.ConfigureMounts(mounts);

            var switcherGo = new GameObject("Switcher");
            _spawned.Add(switcherGo);
            var switcher = switcherGo.AddComponent<ControlSwitcher>();
            switcher.Configure(walk, null, null, null, 0f, null);

            return new Rig
            {
                Switcher = switcher, Drive = drive, Door = vDoor,
                PlayerGo = playerGo, MachineGo = machineGo,
            };
        }

        static Vector2 World(Rig rig, Vector2 local) =>
            rig.MachineGo.transform.TransformPoint(new Vector3(local.x, local.y, 0f));

        /// <summary>The candidate the real resolver offers a rider standing at <paramref name="at"/>, or
        /// null. Through <see cref="InteractResolver"/> itself — never a re-implementation of its
        /// distance rule.</summary>
        static IInteractable Offered(Vector2 at) =>
            InteractResolver.TryResolve(Interactables.Active,
                new InteractActor(at, Vector2.zero, InteractContext.OnFoot),
                360f, out IInteractable best) ? best : null;

        // =============================================================================================
        //  1. ON FROM THE CURB — the side that was silently refused
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>The whole loop from the side the art publishes and the code used to refuse.</b> She
        /// stands at the curb reach point — 1.96 m from the street one, outside any single door's
        /// 1.5 m disc — is offered the machine, works it, and ends up drawn on the saddle.
        /// </summary>
        [UnityTest]
        public IEnumerator SheGetsOnFromTheCurbSideAndIsDrawnOnTheSaddle()
        {
            Rig rig = BuildRig(StreetSide, CurbSide, BikeSeat, standAt: CurbSide);
            yield return null;

            IInteractable offered = Offered(rig.PlayerGo.transform.position);
            Assert.That(offered, Is.Not.Null,
                "⚠️ standing exactly where her sidecar says she may mount, she was offered nothing. " +
                "That is the defect: the curb reach point sits outside the street door's disc.");

            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door),
                "she could not be boarded from the curb side.");
            yield return null;

            Assert.IsTrue(rig.Drive.IsShowing,
                "nobody is drawn on her. A saddle machine has no inside to be in — a hidden rider is a " +
                "bike riding itself.");

            // …and she is ON the saddle, not at the machine's root: the seat is 0.30 m aft and 0.94 m up,
            // and the lift is what puts her on the cushion rather than in the swingarm.
            Vector2 seatGround = World(rig, new Vector2(BikeSeat.x, BikeSeat.y));
            float lift = (BikeSeat.z - PoseSeatZ) * HiddenHarbours.Art.SpriteLightMath.HeightScale;
            Assert.That(((Vector2)rig.PlayerGo.transform.position).x,
                Is.EqualTo(seatGround.x).Within(1e-3f));
            Assert.That(((Vector2)rig.PlayerGo.transform.position).y,
                Is.EqualTo(seatGround.y + lift).Within(1e-3f),
                "she is drawn at the machine's root, not her saddle.");
        }

        // =============================================================================================
        //  2. OFF OVER THE STAND — the tie, and why it is the only path
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>She gets on from the CURB, rides, and steps off on the STREET side — over the stand.</b>
        ///
        /// <para>The mechanism is the tie, and this asserts the tie really is the state she is in: at the
        /// moment of stepping off she is on the saddle, and the saddle is on the centreline, so both
        /// doors are the same distance away to the last float. The <c>&lt;</c> in
        /// <see cref="DriveSeatSides.NearestDoor"/> is therefore what decides it, and it decides for the
        /// art's preferred side. The side she GOT ON from does not enter into it, which is the point of
        /// mounting from the far side here.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator RidingThenSteppingOffPutsHerDownOverTheStand()
        {
            Rig rig = BuildRig(StreetSide, CurbSide, BikeSeat, standAt: CurbSide);
            yield return null;
            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door));

            // A few frames of being ridden — enough for the switcher's re-seating and the presenter's
            // offset to have fought over her position more than once.
            for (int i = 0; i < 4; i++) yield return null;

            // ⚠️ The premise, asserted rather than assumed: she is equidistant from her two sides.
            Vector2 rider = rig.PlayerGo.transform.position;
            float toStreet = Vector2.Distance(rider, World(rig, StreetSide));
            float toCurb = Vector2.Distance(rider, World(rig, CurbSide));
            Assert.That(toStreet, Is.EqualTo(toCurb).Within(1e-3f),
                "she is no longer centred between her two sides. The seat_ref is on the centreline on " +
                "all three machines, so this tie is the state every dismount happens in — if it has " +
                "stopped being true, the tie-break is no longer what decides where she is put down and " +
                "this fixture is measuring the wrong thing.");

            Assert.IsTrue(rig.Switcher.LeaveDriving(), "she could not get off on dry ground.");
            yield return null;

            Assert.IsFalse(rig.Drive.IsShowing, "still drawing a rider on a machine nobody is on.");
            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.OnFoot));

            Vector2 landed = rig.PlayerGo.transform.position;
            Assert.That(Vector2.Distance(landed, World(rig, StreetSide)), Is.LessThan(0.25f),
                "she stepped off somewhere other than the street side. The stand is on the street side " +
                "and a rider steps off over it — the art's own `mount.preferred`.");
            Assert.That(Vector2.Distance(landed, World(rig, CurbSide)), Is.GreaterThan(1.5f),
                "she was put down on the curb side — 1.96 m across the machine she was sitting on, " +
                "which means walking her through it.");
        }

        // =============================================================================================
        //  3. THE ARM THAT MUST NOT MOVE — a cab has one door and gets it back unchanged
        // =============================================================================================

        /// <summary>
        /// <b>A truck is byte-for-byte what she shipped.</b> No alternate side is published, so
        /// <see cref="DriveSeatSides.NearestDoor"/> hands her single door straight back and she is set
        /// down exactly where she always was — whatever the rider's position happens to be.
        /// </summary>
        [UnityTest]
        public IEnumerator ACabSetsHerDownAtHerOneDoor()
        {
            Rig rig = BuildRig(CabDoor, Vector2.zero, seat: Vector3.zero, standAt: CabDoor,
                               machineHeadingDeg: 45f);
            yield return null;

            Assert.IsFalse(rig.Door.HasAltDoor, "a cab has grown a second way in.");
            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door));
            for (int i = 0; i < 3; i++) yield return null;

            Assert.IsFalse(rig.Drive.IsShowing,
                "a hard cab is drawing a driver — this machine publishes no open seat (ADR 0035).");

            Assert.IsTrue(rig.Switcher.LeaveDriving());
            yield return null;

            Assert.That(Vector2.Distance(rig.PlayerGo.transform.position, World(rig, CabDoor)),
                Is.LessThan(0.25f),
                "the truck's landing moved. Her one door is the only answer there has ever been, and " +
                "the saddle seam must hand it back untouched.");
        }
    }
}
