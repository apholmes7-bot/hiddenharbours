using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Vehicles;
using HiddenHarbours.App;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE PLAYER DRIVES THE DUALLY</b> (ADR 0035's ruled follow-up) — getting in, getting out,
    /// staying on the land, and the two contract traps that had to be closed to make room for a fourth
    /// control mode.
    ///
    /// <para><b>What is actually at risk here, and why each of these is a test rather than a read-through.</b>
    /// The drive mode is mostly a state machine and a handful of gates, and state machines fail in ways
    /// that look fine on the screen you were looking at:</para>
    /// <list type="bullet">
    /// <item><b>The append-only enum traps.</b> <see cref="ControlMode"/> is a Core contract that grows,
    /// and two places dispatched on it with a <c>default:</c> arm meaning "the helm". Appending
    /// <see cref="ControlMode.Driving"/> made both of those silently WRONG — a driver reported as standing
    /// at a boat's helm, and a driver pressing the interact key handed to <c>LeaveHelm</c>. Neither would
    /// have failed to compile and neither would have thrown. They are pinned here so the NEXT mode
    /// appended cannot re-open them.</item>
    /// <item><b>Where you are put down.</b> Stepping out into her own turning circle is the one placement
    /// bug that hurts, so the door point is asserted against the sidecar's OWN published swept envelope
    /// rather than against a number typed into this file.</item>
    /// <item><b>Getting stuck.</b> The land gate must refuse the water and must NEVER refuse the way back
    /// off it — a truck stranded nose-to-the-barachois needs a tow truck the village does not have.</item>
    /// </list>
    ///
    /// <para>The vehicle defs are built in code (the <c>VehicleControllerTests</c> pattern), because these
    /// pin the MODEL and a fixture reading the shipped asset would go red every time the owner tuned her.
    /// The one exception is the door-point parity test, which is about the shipped asset on purpose.</para>
    /// </summary>
    public class VehicleDriveModeTests
    {
        // A flat terrain + environment, so the land gate's depth read is deterministic. Same shape as
        // BoardingMoveTests' pair — the two gates read the SAME number from opposite sides.
        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        /// <summary>Dry to the west of <see cref="ShoreX"/>, drowned to the east — a coastline with a
        /// straight edge, so "which side is she on?" has an exact answer.</summary>
        private sealed class HalfDrownedCoast : ITidalTerrain
        {
            public const float ShoreX = 10f;
            public float ElevationAt(Vector2 worldPos) => worldPos.x < ShoreX ? 6f : -6f;
        }

        private readonly List<Object> _spawned = new();
        private readonly List<ControlModeChanged> _modes = new();
        private readonly List<ActiveVehicleChanged> _vehicles = new();

        private void OnMode(ControlModeChanged e) => _modes.Add(e);
        private void OnVehicle(ActiveVehicleChanged e) => _vehicles.Add(e);

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            _modes.Clear();
            _vehicles.Clear();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveVehicleChanged>();
            EventBus.Subscribe<ControlModeChanged>(OnMode);
            EventBus.Subscribe<ActiveVehicleChanged>(OnVehicle);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<ControlModeChanged>(OnMode);
            EventBus.Unsubscribe<ActiveVehicleChanged>(OnVehicle);
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveVehicleChanged>();
            EventBus.Clear<DriveSeatRequested>();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- the fixture ------------------------------------------------------------------------

        /// <summary>The Dually's own published numbers, so every claim below is measured against her
        /// rather than against a convenient invention. All from the 2026-08-17 gameplay sidecar.</summary>
        private const float DoorLocalX = -1.75f;     // INTERACT "drive" reach_point
        private const float DoorLocalY = 0.10f;
        private const float SweptTyreCornerX = 1.22f; // STEERING.swept_envelope.tire_corner_x_m
        private const float FlareHalfWidthX = 1.24f;  // BODY.collider_bbox x extent

        private VehicleMeshDef _mesh;
        private VehicleDef _def;

        private GameObject NewGo(string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            _spawned.Add(g);
            return g;
        }

        /// <summary>A def that passes <see cref="VehicleMeshDef.IsUsable"/> — the render path's whole
        /// checklist, because <c>VehicleDoor.IsDrivable</c> consults it and a def that quietly failed it
        /// would make every entry test pass for the wrong reason (nothing drivable, nothing to assert).</summary>
        private void BuildDefs(bool withDoor = true)
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            mesh.triangles = new[] { 0, 1, 2 };
            _spawned.Add(mesh);

            _mesh = ScriptableObject.CreateInstance<VehicleMeshDef>();
            _spawned.Add(_mesh);
            _mesh.Mesh = mesh;
            _mesh.Ramps = new[]
            {
                new HullMeshDef.Ramp { Colors = new Color32[] { new Color32(255, 255, 255, 255) } },
            };
            _mesh.Bayer16 = new float[16];
            _mesh.PxPerMetre = 32;
            _mesh.CellW = 384;
            _mesh.CellH = 320;
            _mesh.WheelbaseMeters = 4.30f;
            _mesh.FrontTrackMeters = 1.80f;
            _mesh.WheelRadiusMeters = 0.42f;
            _mesh.FrontAxleY = 2.18f;
            _mesh.RearAxleY = -2.12f;
            _mesh.MaxInnerSteerDegrees = 30f;
            _mesh.MaxOuterSteerDegrees = 24.9372f;
            _mesh.DriveDoorLocal = withDoor ? new Vector2(DoorLocalX, DoorLocalY) : Vector2.zero;

            _def = ScriptableObject.CreateInstance<VehicleDef>();
            _spawned.Add(_def);
            _def.Id = "vehicle.test_dually";
            _def.Mesh = _mesh;
            _def.MaxSpeedMetersPerSecond = 11f;
            _def.MaxReverseSpeedMetersPerSecond = 4f;
            _def.AccelerationMetersPerSecondSquared = 4.5f;
            _def.BrakingMetersPerSecondSquared = 8f;
            _def.CoastDecelerationMetersPerSecondSquared = 2.2f;
            _def.SteerRateFullLocksPerSecond = 2f;
            _def.SteerReturnFullLocksPerSecond = 3f;
            _def.SteerFalloffHalfSpeedMetersPerSecond = 9f;
            _def.CameraWorldHeightMeters = 18f;
        }

        private sealed class Rig
        {
            public ControlSwitcher Switcher;
            public PlayerWalkController Walk;
            public VehicleDoor Door;
            public VehicleController Controller;
            public GameObject PlayerGo;
            public GameObject TruckGo;
        }

        /// <summary>Player on foot beside a drivable truck. The truck is authored INACTIVE and activated,
        /// which is the tidy order — <see cref="ParkedVehicle.Configure"/> supports the other one too, and
        /// #556 fixed it for exactly that reason.</summary>
        private Rig BuildRig(Vector3 truckAt, float truckHeadingDeg = 0f)
        {
            BuildDefs();

            var playerGo = NewGo("Player", truckAt + new Vector3(DoorLocalX, DoorLocalY, 0f));
            playerGo.AddComponent<SpriteRenderer>();
            var walk = playerGo.AddComponent<PlayerWalkController>();

            var truckGo = NewGo("Truck", truckAt);
            truckGo.transform.rotation = Quaternion.Euler(0f, 0f, truckHeadingDeg);
            truckGo.AddComponent<Rigidbody2D>();
            var controller = truckGo.AddComponent<VehicleController>();
            controller.SetVehicle(_def);
            var door = truckGo.AddComponent<VehicleDoor>();

            var switcherGo = NewGo("Switcher", Vector3.zero);
            var switcher = switcherGo.AddComponent<ControlSwitcher>();
            switcher.Configure(walk, null, null, null, 0f, null);

            return new Rig
            {
                Switcher = switcher, Walk = walk, Door = door, Controller = controller,
                PlayerGo = playerGo, TruckGo = truckGo,
            };
        }

        // =============================================================================================
        //  1. THE TWO APPEND-ONLY ENUM TRAPS
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>A driver is not standing at a boat's helm.</b> <c>InteractActor.ContextFor</c> ended in
        /// <c>_ =&gt; AtHelm</c>, so the moment a fourth mode existed it reported drivers as helmsmen —
        /// compile-clean, never throwing, and wrong for every candidate that filters on context.
        /// </summary>
        [Test]
        public void DrivingMapsToItsOwnInteractContext()
        {
            Assert.That(InteractActor.ContextFor(ControlMode.Driving),
                        Is.EqualTo(InteractContext.Driving),
                        "a driver must report the cab, not the helm — see the default-arm trap this closes");

            // And the three that were already right must not have moved.
            Assert.That(InteractActor.ContextFor(ControlMode.OnFoot), Is.EqualTo(InteractContext.OnFoot));
            Assert.That(InteractActor.ContextFor(ControlMode.OnDeck), Is.EqualTo(InteractContext.OnDeck));
            Assert.That(InteractActor.ContextFor(ControlMode.Aboard), Is.EqualTo(InteractContext.AtHelm));
        }

        /// <summary>The enum's numeric values are append-only — a Core contract, and one that save data
        /// and serialized scenes both read. Driving must be LAST.</summary>
        [Test]
        public void ControlModeStayedAppendOnly()
        {
            Assert.That((int)ControlMode.OnFoot, Is.EqualTo(0));
            Assert.That((int)ControlMode.Aboard, Is.EqualTo(1));
            Assert.That((int)ControlMode.OnDeck, Is.EqualTo(2));
            Assert.That((int)ControlMode.Driving, Is.EqualTo(3),
                        "Driving is appended AFTER OnDeck so no existing value shifts");
        }

        /// <summary>
        /// ⭐ <b>The interact key in a cab means "get out", not "leave the helm".</b> The switcher's
        /// dispatch had a <c>default:</c> arm that called <c>LeaveHelm</c>; a driver falling into it would
        /// have been stepped onto a boat's deck from inside a truck.
        /// </summary>
        [Test]
        public void InteractInTheCabGetsYouOutAndNotOntoADeck()
        {
            Rig rig = BuildRig(Vector3.zero);
            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door), "fixture: she must be drivable");

            Assert.IsTrue(rig.Switcher.TryInteract(), "the press is always answered in the cab");
            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.OnFoot),
                        "getting out puts you on your feet — NOT onto a boat's deck (the default-arm trap)");
            Assert.IsNull(rig.Switcher.DrivenSeat, "the seat is given up on the way out");
        }

        // =============================================================================================
        //  2. GETTING IN
        // =============================================================================================

        [Test]
        public void WorkingTheDoorPutsYouBehindTheWheel()
        {
            Rig rig = BuildRig(Vector3.zero);

            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door));
            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.Driving));
            Assert.That(rig.Switcher.DrivenSeat, Is.SameAs(rig.Door));
        }

        /// <summary>The camera framing is published BEFORE the mode, the order the helm publishes in: the
        /// camera stores the height on the first signal and commits it on the second, so a mode arriving
        /// first would commit the previous framing for a frame.</summary>
        [Test]
        public void TheFramingIsAnnouncedBeforeTheMode()
        {
            Rig rig = BuildRig(Vector3.zero);
            rig.Switcher.TryEnterDriving(rig.Door);

            Assert.That(_vehicles.Count, Is.EqualTo(1), "one framing announcement");
            Assert.That(_vehicles[0].VehicleId, Is.EqualTo("vehicle.test_dually"));
            Assert.That(_vehicles[0].CameraWorldHeightMeters, Is.EqualTo(18f).Within(1e-4f));
            Assert.That(_modes[_modes.Count - 1].Mode, Is.EqualTo(ControlMode.Driving));
        }

        /// <summary>⭐ <b>SABOTAGE ARM — a blocked interaction gate must refuse the wheel.</b> A modal
        /// dialogue owns the interact key completely; a truck entered under one would put the player in a
        /// cab they cannot get out of, because the same gate blocks the press that would release them.</summary>
        [Test]
        public void ABlockedGateRefusesTheWheel()
        {
            Rig rig = BuildRig(Vector3.zero);
            InteractionGate.IsBlocked = true;

            Assert.IsFalse(rig.Switcher.TryEnterDriving(rig.Door), "a modal owns the key");
            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.OnFoot));
            Assert.IsNull(rig.Switcher.DrivenSeat);
            Assert.That(_modes, Is.Empty, "a refused entry publishes nothing");
        }

        /// <summary>Scenery is not drivable — most trucks in a village should be exactly that, and a def
        /// with no published door cannot be got into either (the (0,0) sentinel).</summary>
        [Test]
        public void SceneryAndDoorlessMachinesAreRefused()
        {
            Rig rig = BuildRig(Vector3.zero);

            // No published door → not drivable, not available, and refused.
            _mesh.DriveDoorLocal = Vector2.zero;
            Assert.IsFalse(rig.Door.IsDrivable, "no published door");
            Assert.IsFalse(rig.Door.IsAvailable, "and so nothing to resolve or highlight");
            Assert.IsFalse(rig.Switcher.TryEnterDriving(rig.Door));
            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.OnFoot));
        }

        [Test]
        public void NullAndAlreadyDrivingAreRefused()
        {
            Rig rig = BuildRig(Vector3.zero);

            Assert.IsFalse(rig.Switcher.TryEnterDriving(null), "no seat");

            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door));
            Assert.IsFalse(rig.Switcher.TryEnterDriving(rig.Door),
                           "you get in from your FEET — a second entry from the cab is not a transition");
        }

        // =============================================================================================
        //  3. GETTING OUT — where you land, and which way you look
        // =============================================================================================

        /// <summary>You are set down at the door you got in by, wherever she has been driven to since.
        /// One point serves both ends, so they cannot drift apart.</summary>
        [Test]
        public void YouAreSetDownAtHerDoorWhereverSheHasBeenDriven()
        {
            Rig rig = BuildRig(Vector3.zero);
            rig.Switcher.TryEnterDriving(rig.Door);

            // She is driven off across the park and turned while she goes.
            rig.TruckGo.transform.position = new Vector3(40f, -12f, 0f);
            rig.TruckGo.transform.rotation = Quaternion.Euler(0f, 0f, 30f);

            Assert.IsTrue(rig.Switcher.LeaveDriving());

            Vector2 expected = rig.Door.DoorWorldPosition;
            Assert.That((Vector2)rig.PlayerGo.transform.position,
                        Is.EqualTo(expected).Using(new Vec2Comparer(1e-4f)),
                        "the landing is her door read LIVE, not the spot you got in at");
        }

        /// <summary>
        /// ⭐ <b>SABOTAGE ARM — never set the player down inside her, or inside her turning circle.</b>
        /// Asserted against the sidecar's own published envelope: the body reaches 1.24 m over the flares
        /// and a front tyre corner sweeps to 1.22 m at full lock, so the door point must clear the larger
        /// of the two. Measured at eight headings, because the door swings round with her and a point that
        /// clears at one bearing and not another would be a bug you only meet while parked askew.
        /// </summary>
        [Test]
        public void TheLandingClearsHerBodyAndHerSweptEnvelope()
        {
            float clearance = Mathf.Max(SweptTyreCornerX, FlareHalfWidthX);

            for (int i = 0; i < 8; i++)
            {
                float heading = i * 45f;
                Rig rig = BuildRig(new Vector3(3f, -7f, 0f), heading);
                rig.Switcher.TryEnterDriving(rig.Door);
                rig.Switcher.LeaveDriving();

                Vector2 truck = rig.TruckGo.transform.position;
                Vector2 landed = rig.PlayerGo.transform.position;
                float outward = Vector2.Distance(truck, landed);

                Assert.That(outward, Is.GreaterThan(clearance),
                    $"at heading {heading}° the player is set down {outward:0.###} m from her centre, " +
                    $"inside the {clearance:0.##} m she occupies or sweeps — the one placement bug that hurts");

                TearDownRigOnly();
            }
        }

        /// <summary>The fisher steps out looking AWAY from the truck, and the bearing is taken on the
        /// GROUND plane (ADR 0034): world XY is foreshortened by sin 40°, so a raw angle is out by up to
        /// 12.6° and buckets to the neighbouring cardinal near the boundaries.</summary>
        [Test]
        public void TheExitFacingIsDerivedOutwardOnGroundBearings()
        {
            // Nose north: her driver's door is due WEST of her, so the fisher ends up facing west.
            Rig rig = BuildRig(Vector3.zero);
            rig.Switcher.TryEnterDriving(rig.Door);
            rig.Switcher.LeaveDriving();

            Assert.That(rig.Walk.CurrentFacing, Is.EqualTo(Facing.Left),
                        "the door is on her street side; nose north puts it due west");
        }

        /// <summary>The four cardinals of the ground-bearing bucket, including the wrap at north — the
        /// one boundary an implementation using a raw comparison gets wrong.</summary>
        [Test]
        public void GroundBearingsBucketToTheNearestCardinal()
        {
            Assert.That(PlayerWalkController.FacingForGroundBearing(0f),
                        Is.EqualTo(Facing.Up));
            Assert.That(PlayerWalkController.FacingForGroundBearing(90f),
                        Is.EqualTo(Facing.Right));
            Assert.That(PlayerWalkController.FacingForGroundBearing(180f),
                        Is.EqualTo(Facing.Down));
            Assert.That(PlayerWalkController.FacingForGroundBearing(270f),
                        Is.EqualTo(Facing.Left));
            Assert.That(PlayerWalkController.FacingForGroundBearing(-10f),
                        Is.EqualTo(Facing.Up), "wraps across north");
            Assert.That(PlayerWalkController.FacingForGroundBearing(350f),
                        Is.EqualTo(Facing.Up), "and from the other side");
        }

        /// <summary>Stepping out leaves her braked rather than coasting away across the park.</summary>
        [Test]
        public void SheIsLeftOnTheBrakeWhenYouGetOut()
        {
            Rig rig = BuildRig(Vector3.zero);
            rig.Switcher.TryEnterDriving(rig.Door);
            rig.Switcher.DriveInput(1f, 0.5f, false);
            Assert.That(rig.Controller.Throttle, Is.EqualTo(1f).Within(1e-4f), "fixture: she took the input");

            rig.Switcher.LeaveDriving();

            Assert.That(rig.Controller.Throttle, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(rig.Controller.SteerDemand, Is.EqualTo(0f).Within(1e-4f));
            Assert.IsTrue(rig.Controller.Brake, "left on the handbrake — a truck you got out of stays put");
        }

        // =============================================================================================
        //  4. THE DOOR AS AN INTERACT CANDIDATE
        // =============================================================================================

        /// <summary>The reach point rides her: read through her ROOT, so it swings as she turns. Read off
        /// the counter-rotated visual child instead and it would never move at all.</summary>
        [Test]
        public void TheDoorPointSwingsWithHer()
        {
            Rig rig = BuildRig(Vector3.zero);

            Vector2 north = rig.Door.DoorWorldPosition;
            Assert.That(north.x, Is.EqualTo(DoorLocalX).Within(1e-4f), "nose north: the door is due west");

            rig.TruckGo.transform.rotation = Quaternion.Euler(0f, 0f, -90f);   // nose east
            Vector2 east = rig.Door.DoorWorldPosition;

            Assert.That(east, Is.Not.EqualTo(north).Using(new Vec2Comparer(1e-3f)),
                        "turning her must move her door");
            Assert.That(east.magnitude, Is.EqualTo(north.magnitude).Within(1e-3f),
                        "and only rotate it — the distance off her centre is a fact about the machine");
        }

        /// <summary>She is a fixture on the candidate ladder, worked on foot, and available only when
        /// there is really something to get into.</summary>
        [Test]
        public void TheDoorDeclaresItselfHonestly()
        {
            Rig rig = BuildRig(Vector3.zero);

            Assert.That(rig.Door.Contexts, Is.EqualTo(InteractContext.OnFoot));
            Assert.That(rig.Door.Priority, Is.EqualTo(InteractPriority.Fixture));
            Assert.IsTrue(rig.Door.IsAvailable);
            Assert.That(rig.Door.Id, Is.Not.Null.And.Not.Empty);
        }

        /// <summary>A DRIVABLE parked machine is given her door; scenery is not. Most trucks in a village
        /// should be scenery, and a door on one would register a candidate that refuses every press.
        /// Skinning twice must not give her two (a second registration under a second id is exactly the
        /// non-total resolver ordering <see cref="IInteractable"/> warns about).</summary>
        [Test]
        public void OnlyDrivableParkedMachinesAreGivenADoor()
        {
            BuildDefs();

            var sceneryGo = NewGo("Scenery", Vector3.zero);
            var scenery = sceneryGo.AddComponent<ParkedVehicle>();
            scenery.Configure(_def, drivable: false);
            Assert.IsNull(scenery.Door, "scenery has no wheel to take");
            Assert.IsNull(sceneryGo.GetComponent<VehicleDoor>());

            var drivableGo = NewGo("Drivable", new Vector3(20f, 0f, 0f));
            drivableGo.AddComponent<Rigidbody2D>();
            var drivable = drivableGo.AddComponent<ParkedVehicle>();
            drivable.Configure(_def, drivable: true);
            Assert.IsNotNull(drivable.Door, "a drivable machine gets a way in");

            drivable.Skin();   // idempotent
            Assert.That(drivableGo.GetComponents<VehicleDoor>().Length, Is.EqualTo(1),
                        "skinning twice must not register a second candidate");
        }

        /// <summary>⭐ Two trucks of the same model in one park must not share an id — the resolver's last
        /// tie-break would stop being total, and the diegetic highlight would light both.</summary>
        [Test]
        public void TwoTrucksOfAModelCarryDistinctDoorIds()
        {
            Rig a = BuildRig(new Vector3(-6f, 0f, 0f));
            var otherGo = NewGo("Truck2", new Vector3(6f, 0f, 0f));
            otherGo.AddComponent<Rigidbody2D>();
            otherGo.AddComponent<VehicleController>().SetVehicle(_def);
            var b = otherGo.AddComponent<VehicleDoor>();

            Assert.That(b.Id, Is.Not.EqualTo(a.Door.Id),
                        "the truck park is three bays wide; ids must be per INSTANCE, not per model");
        }

        // =============================================================================================
        //  5. STAYING ON THE LAND
        // =============================================================================================

        /// <summary>Stopping distance is <c>v²/2a</c> — the term that lets the probe look exactly as far
        /// ahead as it must, with no look-ahead tunable to get wrong (rule 6).</summary>
        [Test]
        public void StoppingDistanceIsTheOrdinaryResult()
        {
            // 11 m/s into 8 m/s² = 121/16 = 7.5625 m
            Assert.That(VehicleGrounding.StoppingDistanceMeters(11f, 8f),
                        Is.EqualTo(7.5625f).Within(1e-3f));
            Assert.That(VehicleGrounding.StoppingDistanceMeters(0f, 8f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(VehicleGrounding.StoppingDistanceMeters(-11f, 8f),
                        Is.EqualTo(7.5625f).Within(1e-3f), "reversing needs the same room");
        }

        /// <summary>The cap is the inverse of the stopping distance — <c>√(2ad)</c> — so it falls
        /// CONTINUOUSLY to zero as the clear road runs out. That continuity is the point: a gate that
        /// refused outright instead would limit-cycle at the water's edge (zeroed, it has no stopping
        /// distance, so the probe pulls back onto dry ground and the throttle is allowed again).</summary>
        [Test]
        public void TheSpeedCapIsTheInverseOfTheStoppingDistance()
        {
            Assert.That(VehicleGrounding.MaxApproachSpeedMetersPerSecond(7.5625f, 8f),
                        Is.EqualTo(11f).Within(1e-3f), "the round trip back to the speed it came from");
            Assert.That(VehicleGrounding.MaxApproachSpeedMetersPerSecond(0f, 8f),
                        Is.EqualTo(0f).Within(1e-6f), "no road left, no speed allowed");

            // Monotone and continuous — no step for a limit cycle to live in.
            float last = 0f;
            for (float d = 0f; d <= 8f; d += 0.5f)
            {
                float cap = VehicleGrounding.MaxApproachSpeedMetersPerSecond(d, 8f);
                Assert.That(cap, Is.GreaterThanOrEqualTo(last), "more road can never allow less speed");
                last = cap;
            }
        }

        /// <summary>⭐ <b>The march leads with whichever end is going first</b>, which is what makes the
        /// refusal escapable. Marching a fixed end would strand her: a truck stopped nose-to-the-water
        /// would go on being refused while the driver begged her to reverse.</summary>
        [Test]
        public void TheRoadIsMarchedFromWhicheverEndLeads()
        {
            var terrain = new HalfDrownedCoast();
            var env = new FlatEnv { Level = 0f };
            var origin = new Vector2(HalfDrownedCoast.ShoreX - 3f, 0f);
            Vector2 nose = Vector2.right;   // pointed at the water

            float ahead = VehicleGrounding.SpeedCapMetersPerSecond(terrain, env, 0.0, origin, nose,
                                                                   speed: 8f, 2.18f, -2.12f, 8f, 0.42f);
            float astern = VehicleGrounding.SpeedCapMetersPerSecond(terrain, env, 0.0, origin, nose,
                                                                    speed: -8f, 2.18f, -2.12f, 8f, 0.42f);

            Assert.That(ahead, Is.LessThan(8f), "driving at the water is capped below the demand");
            Assert.That(astern, Is.GreaterThan(ahead),
                        "backing AWAY from it marches the dry end and is freer");
        }

        /// <summary>The march reports the distance to the water, not merely that there is some — that
        /// distance is what the cap is computed from.</summary>
        [Test]
        public void TheMarchMeasuresHowMuchDryRoadIsLeft()
        {
            var terrain = new HalfDrownedCoast();
            var env = new FlatEnv { Level = 0f };

            float clear = VehicleGrounding.ClearRoadMeters(terrain, env, 0.0,
                                                           new Vector2(HalfDrownedCoast.ShoreX - 4f, 0f),
                                                           Vector2.right, horizonMeters: 10f,
                                                           stepMeters: 0.42f);
            Assert.That(clear, Is.EqualTo(4f).Within(0.5f), "the shore is 4 m east; the march finds it");

            float allDry = VehicleGrounding.ClearRoadMeters(terrain, env, 0.0,
                                                            new Vector2(HalfDrownedCoast.ShoreX - 40f, 0f),
                                                            Vector2.left, horizonMeters: 10f,
                                                            stepMeters: 0.42f);
            Assert.That(allDry, Is.EqualTo(10f).Within(1e-3f), "nothing wet inside the horizon");
        }

        /// <summary>The march is bounded however badly a machine is tuned to brake — a rule-7 budget, so
        /// a pathological braking number coarsens the step instead of spending the frame.</summary>
        [Test]
        public void TheMarchIsBoundedForAnyBrakingTune()
        {
            var terrain = new FlatTerrain { Elevation = 6f };
            var env = new FlatEnv { Level = 0f };

            // A 600 m horizon at a 0.42 m step would be 1400 samples; the budget caps it at 32.
            float clear = VehicleGrounding.ClearRoadMeters(terrain, env, 0.0, Vector2.zero, Vector2.right,
                                                           horizonMeters: 600f, stepMeters: 0.42f);
            Assert.That(clear, Is.EqualTo(600f).Within(1e-2f), "all dry, so the whole horizon is clear");
        }

        /// <summary>No height map means no rule — the gate self-disables in EditMode, in an art scene and
        /// in any region that never authored tidal ground. A missing map must never strand a truck.</summary>
        [Test]
        public void NoTerrainMeansNoGate()
        {
            Assert.IsTrue(VehicleGrounding.IsDryLand(null, null, 0.0, Vector2.zero));
            Assert.IsTrue(VehicleGrounding.IsDryLand(new FlatTerrain { Elevation = -99f }, null, 0.0,
                                                     Vector2.zero),
                          "half-wired is still unwired");
        }

        /// <summary>Bared ground is drivable; drowned ground is not — the SAME number the hull's crossing
        /// gate reads, from the other side.</summary>
        [Test]
        public void BaredGroundDrivesAndDrownedGroundDoesNot()
        {
            var terrain = new FlatTerrain { Elevation = 0.5f };
            var env = new FlatEnv { Level = 0f };
            Assert.IsTrue(VehicleGrounding.IsDryLand(terrain, env, 0.0, Vector2.zero), "ground above water");

            env.Level = 2f;
            Assert.IsFalse(VehicleGrounding.IsDryLand(terrain, env, 0.0, Vector2.zero), "the tide took it");
        }

        /// <summary>
        /// ⭐ <b>Approaching the water holds her below her top speed</b>, by exactly the amount the
        /// remaining gravel allows. Driven through the real <see cref="VehicleController.StepPhysics"/>
        /// rather than the pure helper, so it pins the WIRING as well as the arithmetic.
        ///
        /// <para>The transform does not move here — EditMode runs no physics step — so this is the steady
        /// state at one position, which is the cleanest place to read the cap: she pulls up to what the
        /// road allows and stays there instead of stuttering.</para>
        /// </summary>
        [Test]
        public void ApproachingTheWaterHoldsHerBelowHerTopSpeed()
        {
            BuildDefs();
            GameServices.TidalTerrain = new HalfDrownedCoast();
            GameServices.Environment = new FlatEnv { Level = 0f };

            // Nose EAST at the drowned half, front axle 1.82 m short of the shore line.
            var controller = TruckPointedEast(HalfDrownedCoast.ShoreX - 4f);

            controller.Throttle = 1f;
            for (int i = 0; i < 300; i++) controller.StepPhysics(0.02f);

            // √(2·8·1.82) = 5.40 m/s — she may come at it no faster than she can stop. The tolerance
            // carries the march's own resolution: it steps by her wheel radius, so the clear road it
            // reports is quantised to 0.42 m and the cap lands a little above the exact figure.
            float expected = Mathf.Sqrt(2f * 8f * 1.82f);
            Assert.That(controller.SpeedMetersPerSecond, Is.LessThan(_def.MaxSpeedMetersPerSecond),
                        "the water holds her back");
            Assert.That(controller.SpeedMetersPerSecond, Is.EqualTo(expected).Within(0.6f),
                        "and by the amount the remaining gravel allows, not an arbitrary one");
        }

        /// <summary>⭐ <b>SABOTAGE ARM — a wheel at the water means dead stop, and reverse still works.</b>
        /// The one outcome that must never happen is a truck that cannot be recovered.</summary>
        [Test]
        public void AWheelAtTheWaterStopsHerDeadAndSheCanStillBackOff()
        {
            BuildDefs();
            GameServices.TidalTerrain = new HalfDrownedCoast();
            GameServices.Environment = new FlatEnv { Level = 0f };

            // Her ORIGIN on the shore line, nose east: the front axle is already over the water.
            var controller = TruckPointedEast(HalfDrownedCoast.ShoreX);

            controller.Throttle = 1f;
            for (int i = 0; i < 100; i++) controller.StepPhysics(0.02f);
            Assert.That(controller.SpeedMetersPerSecond, Is.EqualTo(0f).Within(1e-4f),
                        "her leading wheel is in the water — she does not swim");

            controller.Throttle = -1f;
            for (int i = 0; i < 50; i++) controller.StepPhysics(0.02f);
            Assert.That(controller.SpeedMetersPerSecond, Is.LessThan(-0.5f),
                        "backing away must ALWAYS be allowed — a truck the gate could strand would need " +
                        "a tow truck the village does not have");
        }

        private VehicleController TruckPointedEast(float x)
        {
            var go = NewGo("Truck", new Vector3(x, 0f, 0f));
            go.transform.rotation = Quaternion.Euler(0f, 0f, -90f);   // transform.up → +x
            go.AddComponent<Rigidbody2D>();
            var controller = go.AddComponent<VehicleController>();
            controller.SetVehicle(_def);
            return controller;
        }

        // =============================================================================================
        //  6. THE CAMERA HANDOFF
        // =============================================================================================

        /// <summary>Driving asks for her own framing, and the three that existed are untouched.</summary>
        [Test]
        public void DrivingAsksForTheVehicleFraming()
        {
            Assert.That(CameraZoomPolicy.DesiredFraming(ControlMode.Driving, false, false),
                        Is.EqualTo(CameraFraming.Vehicle));
            Assert.That(CameraZoomPolicy.DesiredFraming(ControlMode.Aboard, false, false),
                        Is.EqualTo(CameraFraming.Boat));
            Assert.That(CameraZoomPolicy.DesiredFraming(ControlMode.OnDeck, false, false),
                        Is.EqualTo(CameraFraming.Deck));
            Assert.That(CameraZoomPolicy.DesiredFraming(ControlMode.OnFoot, false, false),
                        Is.EqualTo(CameraFraming.OnFoot));
        }

        /// <summary>A live haul can never tighten a truck: the haul is deck work, and the flag reaching
        /// any other mode would be a bug that only shows on land.</summary>
        [Test]
        public void ALiveHaulCannotTightenTheCabView()
        {
            Assert.That(CameraZoomPolicy.DesiredFraming(ControlMode.Driving, true, true),
                        Is.EqualTo(CameraFraming.Vehicle));
        }

        // =============================================================================================
        //  7. THE SHIPPED ASSET STILL MATCHES HER SIDECAR
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The door point on the baked asset is the one the art side published</b>, re-derived from
        /// the sidecar rather than trusted.
        ///
        /// <para>This is the discipline ADR 0035 §6 asks for: the number is measured art, it lives in data
        /// rather than in C#, and it is pinned to its SOURCE so it cannot silently drift or be "fixed"
        /// toward something someone remembered. It is deliberately the one test here that reads the
        /// shipped asset — everything else pins the model.</para>
        /// </summary>
        [Test]
        public void TheBakedDoorPointMatchesTheSidecar()
        {
            const string assetPath = "Assets/_Project/Data/Vehicles/Meshes/Dually3500VehicleMesh.asset";
            const string sidecarPath =
                "docs/art/rigs/gameplay/vehicles/vehicleIsoRig.dually3500.gameplay.json";

            DirectoryInfo root = Directory.GetParent(Application.dataPath);
            Assert.IsNotNull(root, "cannot locate the repo root above Assets/");
            string sidecarFull = Path.Combine(root.FullName, sidecarPath);
            FileAssert.Exists(sidecarFull);

            (float sx, float sy) = ReadDriveReachPoint(File.ReadAllText(sidecarFull));

            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(assetPath);
            Assert.IsNotNull(def, $"the baked mesh def is missing at {assetPath}");

            Assert.That(def.DriveDoorLocal.x, Is.EqualTo(sx).Within(1e-4f),
                        "the asset's door x has drifted from the sidecar's INTERACT 'drive' reach_point");
            Assert.That(def.DriveDoorLocal.y, Is.EqualTo(sy).Within(1e-4f),
                        "the asset's door y has drifted from the sidecar's INTERACT 'drive' reach_point");
        }

        /// <summary>Pull <c>INTERACT[id=drive].reach_point</c>'s first two numbers out of the sidecar.
        /// A deliberately small scan rather than a JSON dependency — the same hand-rolled approach
        /// <c>DuallyIsoKitProbeTests</c> takes over this very file.</summary>
        private static (float, float) ReadDriveReachPoint(string json)
        {
            int drive = json.IndexOf("\"id\": \"drive\"", System.StringComparison.Ordinal);
            Assert.That(drive, Is.GreaterThanOrEqualTo(0), "the sidecar publishes no INTERACT id 'drive'");

            int reach = json.IndexOf("\"reach_point\"", drive, System.StringComparison.Ordinal);
            Assert.That(reach, Is.GreaterThanOrEqualTo(0), "the 'drive' entry publishes no reach_point");

            int open = json.IndexOf('[', reach);
            int close = json.IndexOf(']', open);
            string[] parts = json.Substring(open + 1, close - open - 1).Split(',');
            Assert.That(parts.Length, Is.GreaterThanOrEqualTo(2), "reach_point is not a pair");

            return (float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                    float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture));
        }

        // ---- helpers ----------------------------------------------------------------------------

        /// <summary>Tear the spawned objects down between loop iterations, so an eight-heading sweep does
        /// not leave eight switchers subscribed to the bus.</summary>
        private void TearDownRigOnly()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            _modes.Clear();
            _vehicles.Clear();
        }

        private sealed class Vec2Comparer : IEqualityComparer<Vector2>
        {
            private readonly float _tol;
            public Vec2Comparer(float tolerance) { _tol = tolerance; }
            public bool Equals(Vector2 a, Vector2 b) => Vector2.Distance(a, b) <= _tol;
            public int GetHashCode(Vector2 v) => 0;
        }
    }
}
