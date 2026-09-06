using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>YOU CAN GET ON FROM EITHER SIDE</b> — the seam a machine you sit ASTRIDE needs and a cab
    /// does not.
    ///
    /// <para><b>The defect this closes, measured on the committed art.</b> A driver's door is ONE
    /// <see cref="IInteractable"/> at ONE point with ONE reach, and <see cref="InteractResolver"/> is a
    /// pure static that measures <c>WorldPosition − actor.Position</c> — a property cannot know where the
    /// actor is standing. The ATV pack publishes TWO reach points per machine and they are
    /// <b>1.96 m</b> apart on the enduro, <b>2.30 m</b> on the trike and <b>2.38 m</b> on the quad, every
    /// one past <c>VehicleDoor</c>'s 1.5 m reach. So a rider standing exactly where her own sidecar says
    /// she may mount was <b>refused, silently</b> — and on two of the three that is the side the art
    /// prefers.</para>
    ///
    /// <para><b>Why a second candidate rather than a wider reach.</b> A reach of 2.4 m would cover the
    /// curb side and also let her mount from over the NOSE, which the art never published; and it would
    /// state a number the art does not carry (rule 6). Two discs is what the sidecar actually says.</para>
    ///
    /// <para><b>Both directions.</b> A cab publishes no alternate side, registers a candidate that is
    /// never available, and lands her driver exactly where it always did — the trucks and the Otter are
    /// what they shipped.</para>
    /// </summary>
    public class AtvSaddleMountTests
    {
        const int PxPerMetre = 32;

        readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => Interactables.Clear();

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // =============================================================================================
        //  1. THE COMMITTED ART — the measurement that made this necessary
        // =============================================================================================

        /// <summary>The three saddle machines, as (mesh asset, the gap between her two sides).</summary>
        public static readonly string[] Saddles =
        {
            "Assets/_Project/Data/Vehicles/Meshes/Enduro250VehicleMesh.asset",
            "Assets/_Project/Data/Vehicles/Meshes/Trike200VehicleMesh.asset",
            "Assets/_Project/Data/Vehicles/Meshes/UtilityQuadVehicleMesh.asset",
        };

        /// <summary>The cabs, which must be untouched by all of this.</summary>
        public static readonly string[] Cabs =
        {
            "Assets/_Project/Data/Vehicles/Meshes/Dually3500VehicleMesh.asset",
            "Assets/_Project/Data/Vehicles/Meshes/Otter8x8VehicleMesh.asset",
            "Assets/_Project/Data/Vehicles/Meshes/AeroSemiVehicleMesh.asset",
        };

        static VehicleMeshDef Load(string path)
        {
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(path);
            Assert.That(def, Is.Not.Null, $"{path} did not load — re-run the vehicle bake.");
            return def;
        }

        [Test]
        public void EverySaddleMachinePublishesTwoSides_FurtherApartThanOneDoorCanReach(
            [ValueSource(nameof(Saddles))] string path)
        {
            VehicleMeshDef mesh = Load(path);

            Assert.That(mesh.HasAltDriveDoor, Is.True,
                $"{path} publishes no alternate side. Her sidecar's `ride` interaction carries an " +
                "alt_reach_point; if it has stopped, she is a one-sided machine and this whole seam is " +
                "dead weight for her — check the art before deleting anything.");

            float gap = Vector2.Distance(mesh.DriveDoorLocal, mesh.AltDriveDoorLocal);
            float reach = NewDoor().ReachMeters;

            Assert.That(gap, Is.GreaterThan(reach),
                $"{path}: her two sides are {gap:F2} m apart against a {reach:F2} m reach. If the art " +
                "has brought them inside one disc, ONE candidate would now cover both and the alternate " +
                "registration could retire — take that deliberately, and mind that it would also mean a " +
                "rider can mount from over her nose.");

            Assert.That(mesh.AltDriveDoorLocal.x, Is.EqualTo(-mesh.DriveDoorLocal.x).Within(1e-4f),
                "the two sides are mirrored about the centreline, as the art derives them " +
                "(width/2 + 0.55 m outboard at the seat reference, on both sides).");
            Assert.That(mesh.AltDriveDoorLocal.y, Is.EqualTo(mesh.DriveDoorLocal.y).Within(1e-4f),
                "…and at the same station: you mount at the saddle, not at the nose.");
        }

        /// <summary>⚠️ The other direction, and the half that keeps this honest: a cab has ONE driver's
        /// door and nothing here may give her a second.</summary>
        [Test]
        public void NoCabPublishesASecondSide([ValueSource(nameof(Cabs))] string path)
        {
            VehicleMeshDef mesh = Load(path);
            Assert.That(mesh.HasAltDriveDoor, Is.False,
                $"{path} has grown an alternate way in. A truck's seats are inside a room; a second " +
                "'door' on her would be a place to stand that the art never drew.");
            Assert.That(mesh.AltDriveDoorLocal, Is.EqualTo(Vector2.zero),
                "(0,0) is the 'one way on' sentinel — a point at her own origin is inside her, so it " +
                "can never be a real value.");
        }

        // =============================================================================================
        //  2. THE RESOLVER REACHES BOTH SIDES — through the real one, never a re-implementation
        // =============================================================================================

        [Test]
        public void ARiderStandingOnEitherPublishedSideIsOfferedTheMachine()
        {
            VehicleDoor door = Saddle(new Vector2(-0.98f, -0.30f), new Vector2(0.98f, -0.30f),
                                      at: Vector3.zero);

            Assert.That(Resolve(door.DoorWorldPosition), Is.Not.Null,
                "the preferred (street) side no longer resolves — that is the path that always worked.");
            Assert.That(Resolve(door.AltDoorWorldPosition), Is.Not.Null,
                "⚠️ the CURB side does not resolve. That is the defect: her sidecar publishes that point " +
                "as a place to mount from, and it is 1.96 m from the other one — outside a single " +
                "door's 1.5 m disc.");
        }

        /// <summary>⚠️ …and NOT from anywhere else. Two discs, not one big one: the failure a widened
        /// reach would have introduced is a rider mounting from over the nose.</summary>
        [Test]
        public void ButNotFromOverHerNoseOrHerTail()
        {
            VehicleDoor door = Saddle(new Vector2(-0.98f, -0.30f), new Vector2(0.98f, -0.30f),
                                      at: Vector3.zero);

            // ⚠⚠ THE PREMISE, FIRST. A negative control has to assert that the thing it is denying
            // is reachable at all: on an empty registry — which is exactly what a door gets in EditMode,
            // where OnEnable never runs — both nulls below are true and this test proves nothing.
            Assert.That(Resolve(door.DoorWorldPosition), Is.Not.Null,
                "nothing resolves anywhere, so the two nulls below are not evidence about her nose and " +
                "tail — they are evidence that no candidate is registered.");

            Assert.That(Resolve(new Vector2(0f, 2.0f)), Is.Null, "she was mounted from over the nose.");
            Assert.That(Resolve(new Vector2(0f, -2.4f)), Is.Null, "she was mounted from over the tail.");
        }

        /// <summary>A cab registers her alternate candidate too — the registration cannot be conditional,
        /// because <c>OnEnable</c> runs before the caller has said which vehicle this is — but it is never
        /// AVAILABLE, so the resolver's answer for a truck is what it always was.</summary>
        [Test]
        public void ACabsAlternateCandidateIsRegisteredAndNeverAvailable()
        {
            VehicleDoor door = Saddle(new Vector2(-1.75f, 0.10f), Vector2.zero, at: Vector3.zero);

            Assert.That(door.HasAltDoor, Is.False, "a cab publishes one way in.");
            Assert.That(door.AltDoorWorldPosition, Is.EqualTo(door.DoorWorldPosition),
                "a caller that reads the alternate side without checking gets a real place to stand, " +
                "never a point inside her.");

            int available = 0;
            foreach (IInteractable c in Interactables.Active) if (c.IsAvailable) available++;
            Assert.That(available, Is.EqualTo(1),
                "a truck offers exactly one way in. Two would put a second prompt on her curb side, " +
                "over a point the art never published.");
        }

        /// <summary>Working the curb side asks for the same SEAT — the machine, not the side you walked
        /// up to. Two prompts, one wheel.</summary>
        [Test]
        public void WorkingEitherSideAsksForTheSameSeat()
        {
            VehicleDoor door = Saddle(new Vector2(-0.98f, -0.30f), new Vector2(0.98f, -0.30f),
                                      at: Vector3.zero);

            var asked = new List<IDriveSeat>();
            void OnRequest(DriveSeatRequested e) => asked.Add(e.Seat);
            EventBus.Subscribe<DriveSeatRequested>(OnRequest);
            try
            {
                Resolve(door.DoorWorldPosition).Interact(Actor(door.DoorWorldPosition));
                Resolve(door.AltDoorWorldPosition).Interact(Actor(door.AltDoorWorldPosition));
            }
            finally { EventBus.Unsubscribe<DriveSeatRequested>(OnRequest); }

            Assert.That(asked, Has.Count.EqualTo(2), "one of the two sides published nothing.");
            Assert.That(asked[0], Is.SameAs(door));
            Assert.That(asked[1], Is.SameAs(door),
                "the curb side asked for a different seat. The side is where you STAND; the seat is the " +
                "machine, and handing over anything else would drive something you are not next to.");
        }

        /// <summary>Their ids are distinct — <see cref="InteractResolver"/>'s last tie-break has to be
        /// total across live registrants, and the diegetic highlight lights one candidate by id.</summary>
        [Test]
        public void TheTwoSidesCarryDistinctIds()
        {
            VehicleDoor door = Saddle(new Vector2(-0.98f, -0.30f), new Vector2(0.98f, -0.30f),
                                      at: Vector3.zero);

            var ids = new HashSet<string>();
            foreach (IInteractable c in Interactables.Active)
                Assert.That(ids.Add(c.Id), Is.True, $"two live candidates share the id '{c.Id}'.");
            Assert.That(ids, Has.Count.EqualTo(2));
            Assert.That(door.Id, Is.Not.Empty);
        }

        // =============================================================================================
        //  3. WHICH SIDE SHE IS SET DOWN ON
        // =============================================================================================

        [Test]
        public void SheIsSetDownOnTheSideSheIsOver()
        {
            VehicleDoor door = Saddle(new Vector2(-0.98f, -0.30f), new Vector2(0.98f, -0.30f),
                                      at: Vector3.zero);

            Vector2 street = door.DoorWorldPosition;
            Vector2 curb = door.AltDoorWorldPosition;

            Assert.That(DriveSeatSides.NearestDoor(door, curb + new Vector2(0.4f, 0f)),
                Is.EqualTo(curb), "a rider over the curb side stepped off through the machine.");
            Assert.That(DriveSeatSides.NearestDoor(door, street + new Vector2(-0.4f, 0f)),
                Is.EqualTo(street));
        }

        /// <summary>⚠️ A tie goes to the art's PREFERRED side — the enduro's stand is on the street side
        /// and a rider steps off over it. Deterministic, rather than whichever float came out smaller.</summary>
        [Test]
        public void ATieGoesToThePreferredSide()
        {
            VehicleDoor door = Saddle(new Vector2(-0.98f, -0.30f), new Vector2(0.98f, -0.30f),
                                      at: Vector3.zero);

            Assert.That(DriveSeatSides.NearestDoor(door, door.DriverSeatWorldPosition),
                Is.EqualTo(door.DoorWorldPosition),
                "sat exactly between them — which is where a rider IS — she stepped off the unpreferred " +
                "side. The comparison must be strict, so the tie falls the way the art says.");
        }

        /// <summary>A machine with one way on hands it straight back, so nothing about a cab moves.</summary>
        [Test]
        public void ACabHandsBackHerOneDoor()
        {
            VehicleDoor door = Saddle(new Vector2(-1.75f, 0.10f), Vector2.zero, at: Vector3.zero);
            Vector2 far = new Vector2(50f, 50f);

            Assert.That(DriveSeatSides.NearestDoor(door, far), Is.EqualTo(door.DoorWorldPosition));
            Assert.That(DriveSeatSides.NearestDoor(door, door.DoorWorldPosition),
                Is.EqualTo(door.DoorWorldPosition));
        }

        /// <summary>A null seat is answered with the asker's own position rather than an exception — the
        /// switcher reads this on a path where the machine may have died under the driver.</summary>
        [Test]
        public void ANullSeatIsAnsweredWithWhereYouAlreadyAre()
        {
            Vector2 here = new Vector2(3f, -7f);
            Assert.That(DriveSeatSides.NearestDoor(null, here), Is.EqualTo(here));
        }

        /// <summary>Both sides swing with her — read through the ROOT, so they are where she IS, not
        /// where she was parked. The one thing a latched point gets wrong.</summary>
        [Test]
        public void BothSidesTurnWithTheMachine()
        {
            VehicleDoor door = Saddle(new Vector2(-0.98f, -0.30f), new Vector2(0.98f, -0.30f),
                                      at: Vector3.zero, headingDeg: 90f);

            // Pointing EAST: the street side (−x in her frame) is to the NORTH of her.
            Assert.That(door.DoorWorldPosition.y, Is.GreaterThan(0.5f),
                "her street side did not swing with her heading.");
            Assert.That(door.AltDoorWorldPosition.y, Is.LessThan(-0.5f),
                "her curb side did not swing with her heading.");
            Assert.That(Vector2.Distance(door.DoorWorldPosition, door.AltDoorWorldPosition),
                Is.EqualTo(1.96f).Within(1e-3f),
                "turning her changed the distance between her own two sides.");
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        static InteractActor Actor(Vector2 at) =>
            new InteractActor(at, Vector2.zero, InteractContext.OnFoot);

        /// <summary>The winner the REAL resolver picks for someone standing at <paramref name="at"/>, or
        /// null. Facing is unknown, which is how the resolver is fed on foot when nothing else says.</summary>
        static IInteractable Resolve(Vector2 at) =>
            InteractResolver.TryResolve(Interactables.Active, Actor(at), 360f, out IInteractable best)
                ? best : null;

        VehicleDoor NewDoor()
        {
            var go = new GameObject("ReachProbe");
            _spawned.Add(go);
            return go.AddComponent<VehicleDoor>();
        }

        /// <summary>A machine at <paramref name="at"/> publishing <paramref name="door"/> and, when it is
        /// non-zero, <paramref name="alt"/> — the shape of a saddle machine, or of a cab when the
        /// alternate side is zero.</summary>
        VehicleDoor Saddle(Vector2 door, Vector2 alt, Vector3 at, float headingDeg = 0f)
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            mesh.triangles = new[] { 0, 1, 2 };
            _spawned.Add(mesh);

            var meshDef = ScriptableObject.CreateInstance<VehicleMeshDef>();
            _spawned.Add(meshDef);
            meshDef.Id = "vehiclemesh.saddle_test";
            meshDef.Mesh = mesh;
            meshDef.Ramps = new[]
            {
                new HullMeshDef.Ramp { Colors = new Color32[] { new Color32(255, 255, 255, 255) } },
            };
            meshDef.Bayer16 = new float[16];
            meshDef.PxPerMetre = PxPerMetre;
            meshDef.CellW = 256;
            meshDef.CellH = 192;
            meshDef.WheelbaseMeters = 1.48f;
            meshDef.FrontTrackMeters = 0f;
            meshDef.WheelRadiusMeters = 0.33f;
            meshDef.DriveDoorLocal = door;
            meshDef.AltDriveDoorLocal = alt;
            meshDef.DriverSeatLocal = new Vector3(0f, -0.30f, 0.94f);

            var vehicle = ScriptableObject.CreateInstance<VehicleDef>();
            _spawned.Add(vehicle);
            vehicle.Id = "vehicle.saddle_test";
            vehicle.KindToken = "road_vehicle";
            vehicle.Mesh = meshDef;
            vehicle.CameraWorldHeightMeters = 14f;

            var go = new GameObject("Saddle", typeof(Rigidbody2D));
            _spawned.Add(go);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, -headingDeg);  // +Z is CCW; a bearing is CW
            var controller = go.AddComponent<VehicleController>();
            controller.SetVehicle(vehicle);
            var vDoor = go.AddComponent<VehicleDoor>();

            // ⚠️⚠️ EDITMODE NEVER FIRES OnEnable, so the door does NOT register itself here and
            // Interactables.Active would be EMPTY. Registering the pair explicitly — the real door and the
            // real curb-side candidate, which is exactly what OnEnable does at runtime — is the split this
            // repo settled on after #350: EditMode proves the RULE with the shipped components, PlayMode
            // proves the self-registration. Leaving it out does not merely fail the positive cases; it
            // makes every NEGATIVE one below pass on an empty registry, which is the worse failure.
            Interactables.Register(vDoor);
            Interactables.Register(vDoor.AltSide);
            return vDoor;
        }
    }
}
