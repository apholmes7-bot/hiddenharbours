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
    /// ⭐ <b>THE DRIVE MODE UNDER A RUNNING FRAME PUMP</b> (ADR 0035's ruled follow-up) — the three
    /// things about driving a truck that only a real <c>Update</c>/<c>LateUpdate</c> can settle.
    ///
    /// <list type="number">
    /// <item><b>The driver RIDES her.</b> Seating happens in <c>LateUpdate</c>, after physics has moved
    /// the machine, so a fixture with no frame pump can only ever see the first frame of it.</item>
    /// <item><b>⭐ A truck destroyed under the driver hands control back.</b> This is the arm that
    /// cannot be faked: the notice lives in the <c>Update</c> pump, the failure it guards against is a
    /// <c>MissingReferenceException</c> thrown one frame later, and the reason it is possible at all is
    /// that an INTERFACE-typed reference does not see Unity's fake-null. It is also why the driver is
    /// seated by position rather than parented — a Unity child dies with its parent, and a player
    /// destroyed along with the truck is nobody to hand control back to.</item>
    /// <item><b>The interact gate is not wedged</b> by any of the above. A held
    /// <see cref="InteractionGate"/> outliving its holder turns interaction off for the whole game.</item>
    /// </list>
    ///
    /// <para><b>No key is ever pressed.</b> A headless PlayMode fixture cannot drive the keyboard, so the
    /// gates are exposed and driven directly — <c>TryEnterDriving</c>, <c>DriveInput</c>,
    /// <c>LeaveDriving</c> — which is the #555 lesson and is also how the production path is reached
    /// without asserting anything about an input device.</para>
    /// </summary>
    public class DriveModePlayTests
    {
        private readonly List<Object> _spawned = new();
        private readonly List<ControlModeChanged> _modes = new();
        private void OnMode(ControlModeChanged e) => _modes.Add(e);

        private VehicleMeshDef _mesh;
        private VehicleDef _def;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            _modes.Clear();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Subscribe<ControlModeChanged>(OnMode);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<ControlModeChanged>(OnMode);
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveVehicleChanged>();
            EventBus.Clear<DriveSeatRequested>();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- the fixture ------------------------------------------------------------------------

        private const float DoorLocalX = -1.75f;   // her sidecar's INTERACT "drive" reach_point
        private const float DoorLocalY = 0.10f;

        private sealed class Rig
        {
            public ControlSwitcher Switcher;
            public PlayerWalkController Walk;
            public VehicleDoor Door;
            public GameObject PlayerGo;
            public GameObject TruckGo;
        }

        /// <summary>Defs built in code rather than loaded: these pin the MODE, and a fixture reading the
        /// shipped asset would go red every time the owner tuned her.</summary>
        private void BuildDefs()
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
            _mesh.DriveDoorLocal = new Vector2(DoorLocalX, DoorLocalY);

            _def = ScriptableObject.CreateInstance<VehicleDef>();
            _spawned.Add(_def);
            _def.Id = "vehicle.test_dually";
            _def.Mesh = _mesh;
            _def.CameraWorldHeightMeters = 18f;
        }

        private Rig BuildRig(Vector3 truckAt)
        {
            BuildDefs();

            var playerGo = new GameObject("Player", typeof(SpriteRenderer), typeof(Rigidbody2D));
            playerGo.transform.position = truckAt + new Vector3(DoorLocalX, DoorLocalY, 0f);
            _spawned.Add(playerGo);
            var walk = playerGo.AddComponent<PlayerWalkController>();

            var truckGo = new GameObject("Truck", typeof(Rigidbody2D));
            truckGo.transform.position = truckAt;
            _spawned.Add(truckGo);
            truckGo.AddComponent<VehicleController>().SetVehicle(_def);
            var door = truckGo.AddComponent<VehicleDoor>();

            var switcherGo = new GameObject("Switcher");
            _spawned.Add(switcherGo);
            var switcher = switcherGo.AddComponent<ControlSwitcher>();
            switcher.Configure(walk, null, null, null, 0f, null);

            return new Rig
            {
                Switcher = switcher, Walk = walk, Door = door,
                PlayerGo = playerGo, TruckGo = truckGo,
            };
        }

        // =============================================================================================

        /// <summary>
        /// ⭐ <b>A truck does not fall south through a top-down world.</b> A Rigidbody2D ships with
        /// gravityScale 1, the player's own body zeroes it, and nothing on the vehicle path did —
        /// which no scene exposed while nothing placed a vehicle, and which surfaced instead as this
        /// file's ride test flaking: the fixture trucks were accumulating exactly one fixed step of g
        /// between a teleport and a read (the "~0.2 m/s of residual" #566 diagnosed). The flake was
        /// the bug's shadow. <c>VehicleController.Awake</c> now zeroes it where the reference is
        /// taken; this pins that through the REAL Awake, on a body deliberately created dirty.
        /// </summary>
        [UnityTest]
        public IEnumerator HerBodyIgnoresGravity_BecauseAwakeZeroesIt_NotBecauseAFixtureDid()
        {
            Rig rig = BuildRig(Vector3.zero);
            var rb = rig.TruckGo.GetComponent<Rigidbody2D>();
            Assert.AreEqual(0f, rb.gravityScale,
                "VehicleController.Awake did not zero her gravityScale — in a top-down world that " +
                "is a truck accelerating south, and in this file it is the ride test flaking.");

            rig.TruckGo.transform.position = Vector3.zero;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(((Vector2)rig.TruckGo.transform.position).magnitude, Is.LessThanOrEqualTo(1e-4f),
                "a parked truck with no throttle moved on its own — something is still applying a " +
                "force to a machine nobody is driving.");
        }

        /// <summary>The driver rides the machine: move her, pump a frame, and the player is on her.</summary>
        [UnityTest]
        public IEnumerator TheDriverRidesTheMachineAcrossFrames()
        {
            Rig rig = BuildRig(Vector3.zero);
            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door), "fixture: she must be drivable");

            for (int i = 1; i <= 4; i++)
            {
                rig.TruckGo.transform.position = new Vector3(i * 5f, i * -3f, 0f);
                yield return null;

                float apart = Vector2.Distance(rig.PlayerGo.transform.position,
                                               rig.TruckGo.transform.position);
                Assert.That(apart, Is.LessThanOrEqualTo(1e-3f),
                            $"frame {i}: the driver is {apart:0.###} m off her — they must be carried " +
                            "with the machine, not left standing on the gravel");
            }
        }

        /// <summary>While driving she is not drawn: the rig models no visible driver and her cab glass is
        /// opaque at 32 px/m, so a body left on would be a fisher standing on the truck's roofline.</summary>
        [UnityTest]
        public IEnumerator TheFisherIsHiddenInTheCabAndDrawnAgainOnGettingOut()
        {
            Rig rig = BuildRig(Vector3.zero);
            var sprite = rig.PlayerGo.GetComponent<SpriteRenderer>();

            rig.Switcher.TryEnterDriving(rig.Door);
            yield return null;
            Assert.IsFalse(sprite.enabled, "the fisher is inside the cab");

            rig.Switcher.LeaveDriving();
            yield return null;
            Assert.IsTrue(sprite.enabled, "and is drawn again the moment she steps out");
        }

        /// <summary>Getting out restores the on-foot shape in full — the walk live, the body simulated,
        /// the footprint collider back. A player left frozen, un-simulated and unable to walk is
        /// indistinguishable from a hung game.</summary>
        [UnityTest]
        public IEnumerator GettingOutGivesTheWalkBack()
        {
            Rig rig = BuildRig(Vector3.zero);
            rig.Switcher.TryEnterDriving(rig.Door);
            yield return null;
            Assert.IsFalse(rig.Walk.enabled, "the walk is frozen behind the wheel");

            rig.Switcher.LeaveDriving();
            yield return null;

            Assert.IsTrue(rig.Walk.enabled, "the walk comes back");
            Assert.IsTrue(rig.PlayerGo.GetComponent<Rigidbody2D>().simulated, "and so does her body");
            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.OnFoot));
        }

        /// <summary>
        /// ⭐⭐ <b>SABOTAGE ARM — the truck is destroyed under the driver.</b>
        ///
        /// <para>Three things must hold and each has its own failure mode: the player still EXISTS (which
        /// is why the driver is seated rather than parented — a Unity child dies with its parent); control
        /// is handed back to their feet (rather than left in a cab nothing is ticking); and the interact
        /// gate is not left held (which would turn interaction off for the whole game).</para>
        ///
        /// <para>It also pins the fake-null read that makes the notice possible at all: the seat is held
        /// through an INTERFACE, whose <c>==</c> is plain reference equality and sails straight past a
        /// destroyed component, so the check has to ask the object itself.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ATruckDestroyedMidDriveHandsControlBack()
        {
            Rig rig = BuildRig(Vector3.zero);
            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door));
            yield return null;
            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.Driving), "fixture: she is being driven");

            Object.DestroyImmediate(rig.TruckGo);
            yield return null;   // the Update pump notices

            Assert.IsTrue(rig.PlayerGo != null,
                          "the player must SURVIVE the truck — this is why the driver is seated by " +
                          "position and never parented to a machine a region can despawn");
            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.OnFoot),
                        "control comes back to their feet");
            Assert.IsNull(rig.Switcher.DrivenSeat, "and the dead seat is let go of");
            Assert.IsFalse(InteractionGate.IsBlocked,
                           "the interact gate must NOT be left held — that would wedge interaction off " +
                           "for the whole game");
            Assert.IsTrue(rig.Walk.enabled, "and they can walk away");

            // And the frames after it are quiet: no MissingReferenceException from a stale seat.
            yield return null;
            yield return null;
            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.OnFoot));
        }

        /// <summary>A destroyed truck publishes the mode change exactly once — a handler that re-noticed
        /// every frame would flood the bus and re-frame the camera for ever.</summary>
        [UnityTest]
        public IEnumerator TheHandBackIsAnnouncedOnce()
        {
            Rig rig = BuildRig(Vector3.zero);
            rig.Switcher.TryEnterDriving(rig.Door);
            yield return null;

            _modes.Clear();
            Object.DestroyImmediate(rig.TruckGo);
            yield return null;
            yield return null;
            yield return null;

            Assert.That(_modes.Count, Is.EqualTo(1),
                        "one hand-back, not one per frame for the rest of the session");
            Assert.That(_modes[0].Mode, Is.EqualTo(ControlMode.OnFoot));
        }

        /// <summary>⭐ Nothing is offered from inside a cab. The press there means "get out" and is
        /// answered before the verb is consulted, so a candidate highlighted while driving could never be
        /// acted on — and a highlight that outlives its actionability teaches the player a lie.</summary>
        [UnityTest]
        public IEnumerator NothingIsHighlightedFromInsideTheCab()
        {
            Rig rig = BuildRig(Vector3.zero);

            yield return null;
            Assert.That(InteractVerb.CurrentCandidateId, Is.EqualTo(rig.Door.Id),
                        "fixture: on foot at her door, she is the candidate");

            rig.Switcher.TryEnterDriving(rig.Door);
            yield return null;

            Assert.IsNull(InteractVerb.CurrentCandidateId, "nothing is reachable from the driver's seat");

            rig.Switcher.LeaveDriving();
            yield return null;
            Assert.That(InteractVerb.CurrentCandidateId, Is.EqualTo(rig.Door.Id),
                        "and her door is offered again the moment you are back on the gravel");
        }

        /// <summary>The door's own registration reaches the switcher through Core and nothing else — a
        /// published request, answered by whoever owns the modes.</summary>
        [UnityTest]
        public IEnumerator WorkingTheDoorThroughTheVerbTakesTheWheel()
        {
            Rig rig = BuildRig(Vector3.zero);
            yield return null;

            var actor = new InteractActor(rig.PlayerGo.transform.position, Vector2.zero,
                                          InteractContext.OnFoot);
            Assert.IsTrue(InteractVerb.TryPerform(actor, 180f), "the verb resolved and acted on her door");
            yield return null;

            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.Driving),
                        "the ONE interact verb is what gets you in — no key of its own");
        }

#if UNITY_EDITOR
        /// <summary>
        /// 🔴 <b>The SHIPPED truck answers the verb — not a synthetic stand-in.</b> Every other test in
        /// this file builds its def in code, which is exactly the gap a shipped-asset defect hides in:
        /// the owner stood at the placed truck's door (#567) and E was dead, while all of this stayed
        /// green. This test is that walk, in code: the SHIPPED `Dually3500.asset` (and through it the
        /// shipped mesh asset), the builder's exact placement shape, a player at her real
        /// <see cref="VehicleDoor.DoorWorldPosition"/>, the real verb. Each gate is asserted separately
        /// so a failure names the broken term instead of shrugging.
        /// </summary>
        [UnityTest]
        public IEnumerator TheShippedDuallyAssetsAnswerTheVerbAtHerRealDoor()
        {
            var shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<VehicleDef>(
                "Assets/_Project/Data/Vehicles/Dually3500.asset");
            Assert.IsNotNull(shipped, "the shipped Dually def is missing from Data/Vehicles.");

            // The gates IsDrivable ANDs together, each named. A single false from the composite is
            // the owner's dead E press; decomposed, it is a diagnosis.
            Assert.IsNotNull(shipped.Mesh, "the shipped def carries no mesh asset.");
            Assert.IsTrue(shipped.Mesh.Mesh != null && shipped.Mesh.Mesh.vertexCount > 0,
                "the mesh ASSET's baked Mesh is null/empty — IsUsable refuses her and the door " +
                "reports unavailable: E is dead with no error anywhere.");
            Assert.IsTrue(shipped.IsUsable(),
                "the shipped def fails IsUsable — the door registers but IsAvailable is false, so " +
                "the resolver skips her silently. Decompose against VehicleMeshDef.IsUsable's terms.");
            Assert.AreNotEqual(Vector2.zero, shipped.Mesh.DriveDoorLocal,
                "the shipped mesh asset publishes no driver's door (the (0,0) sentinel) — HasDoor " +
                "refuses and E is dead exactly as observed.");

            // The builder's placement shape, byte-for-byte (NineMileCreekTruckPark.Place lives in an
            // editor assembly this one cannot reference; what it DOES is four lines, mirrored here).
            var truckGo = new GameObject("DuallyAtThePark_ShippedRepro", typeof(Rigidbody2D));
            _spawned.Add(truckGo);
            truckGo.transform.position = Vector3.zero;
            truckGo.GetComponent<Rigidbody2D>().gravityScale = 0f;
            var parked = truckGo.AddComponent<HiddenHarbours.Vehicles.ParkedVehicle>();
            parked.Configure(shipped, drivable: true);
            yield return null;

            var door = truckGo.GetComponent<HiddenHarbours.Vehicles.VehicleDoor>();
            Assert.IsNotNull(door, "the parked truck grew no door at play.");
            Assert.IsTrue(door.IsDrivable, "her door reports NOT drivable with the shipped assets.");

            var playerGo = new GameObject("Player", typeof(SpriteRenderer), typeof(Rigidbody2D));
            _spawned.Add(playerGo);
            playerGo.transform.position = door.DoorWorldPosition;
            var walk = playerGo.AddComponent<PlayerWalkController>();

            var switcherGo = new GameObject("Switcher");
            _spawned.Add(switcherGo);
            var switcher = switcherGo.AddComponent<ControlSwitcher>();
            switcher.Configure(walk, null, null, null, 0f, null);
            yield return null;

            var actor = new InteractActor(playerGo.transform.position, Vector2.zero,
                                          InteractContext.OnFoot);
            Assert.IsTrue(InteractVerb.TryPerform(actor, 180f),
                "standing ON the shipped door point, the verb resolved nothing — the owner's dead E, " +
                "reproduced.");
            yield return null;

            Assert.That(switcher.Mode, Is.EqualTo(ControlMode.Driving),
                "the verb acted but nobody took the wheel — the request/subscription seam broke.");
        }
#endif
    }
}
