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
    /// ⭐⭐ <b>THE DRIVER, UNDER A RUNNING FRAME PUMP</b> — get in an open machine and you are THERE, sat
    /// on her cushion, turning with her; get out and you are gone from her again. The sequence, which the
    /// EditMode half (<c>DriverAtTheWheelTests</c>) cannot see because every part of it lands in a
    /// different phase of the same frame.
    ///
    /// <para><b>Three things only a real pump settles.</b> (1) The SEATING survives
    /// <c>ControlSwitcher.RideVehicle</c>, which re-writes the driver onto the machine's own root in ITS
    /// LateUpdate every single frame — the presenter's offset is applied after it, and if the two ever
    /// swap order the fisher slides 0.36 m aft and nine pixels down and nothing else notices. (2) The
    /// FACING follows the machine through a turn, which is a claim about a value re-derived per frame and
    /// is untestable in one. (3) A machine destroyed under the driver hands control back without a
    /// <c>MissingReferenceException</c> one frame later.</para>
    ///
    /// <para><b>⚠️ THE ONE THAT MATTERS IS THE FACING</b>
    /// (<see cref="HerFacingIsTheMachineSAndNotTheOneSheBoardedAt"/>). The clip's own doc states the law —
    /// <i>"the facing row is the VEHICLE's, re-derived every frame … boarding input must never be cached
    /// as a heading"</i> — and the failure it forbids is invisible at the moment of boarding and obvious
    /// two seconds later: a driver who boarded facing north still facing north while her machine points
    /// east. So the fisher here deliberately walks up facing ONE way and the machine points ANOTHER, and
    /// then she is driven round.</para>
    ///
    /// <para><b>No key is ever pressed</b> — a headless fixture cannot drive the keyboard, so the gates
    /// are driven directly (<c>TryEnterDriving</c>, <c>DriveInput</c>, <c>LeaveDriving</c>), which is the
    /// #555 lesson and the shape <see cref="DriveModePlayTests"/> and <see cref="OtterLaunchPlayTests"/>
    /// both use.</para>
    /// </summary>
    public class DriverAtTheWheelPlayTests
    {
        // ---- the two committed art contracts (see DriverAtTheWheelTests for the provenance) ---------
        const float PoseSeatZ = 0.40f;                                  // OffDeck_mounts.json, drive.seatZ
        static readonly Vector3 OtterSeat = new Vector3(0f, 0.36f, 0.76f); // SEATS[front_bench].seat_ref
        const float DoorLocalX = -1.4f;                                 // INTERACT[id=drive].reach_point
        const float DoorLocalY = 0.2f;

        const int Directions = 8;
        const int DriveFrames = 6;                                      // 6 f × 170 ms, LOOPING

        readonly List<Object> _spawned = new();

        /// <summary>Dry ground everywhere, so nothing here is about the water: this fixture is about a
        /// figure on a machine, and <c>OtterLaunchPlayTests</c> owns the launch.</summary>
        sealed class DryGround : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 4f;
        }

        /// <summary>A still, low tide. Nothing here is about the water either.</summary>
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
            public PlayerWalkController Walk;
            public PlayerDrivePresenter Drive;
            public CharacterClipPlayer Clip;
            public CharacterVisualDef Skin;
            public SpriteRenderer Body;
            public VehicleDoor Door;
            public VehicleController Vehicle;
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

        Rig BuildRig(Vector3 machineAt, float machineHeadingDeg, Vector3 seat, bool withDriveArt = true)
        {
            // --- the machine ---------------------------------------------------------------------
            // A real UnityEngine.Mesh with vertices, because VehicleMeshDef.IsUsable checks for one and
            // VehicleDoor.IsDrivable refuses an unusable def — without it she cannot be boarded at all.
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            mesh.triangles = new[] { 0, 1, 2 };
            _spawned.Add(mesh);

            var meshDef = ScriptableObject.CreateInstance<VehicleMeshDef>();
            _spawned.Add(meshDef);
            meshDef.Id = "vehiclemesh.otter_8x8";
            meshDef.Mesh = mesh;
            meshDef.Ramps = new[]
                { new HullMeshDef.Ramp { Colors = new Color32[] { new Color32(255, 255, 255, 255) } } };
            meshDef.Bayer16 = new float[16];
            meshDef.PxPerMetre = 32;
            meshDef.CellW = 256;
            meshDef.CellH = 192;
            meshDef.WheelbaseMeters = 2.04f;
            meshDef.FrontTrackMeters = 1.26f;
            meshDef.WheelRadiusMeters = 0.32f;
            meshDef.FrontAxleY = 1.02f;
            meshDef.RearAxleY = -1.02f;
            meshDef.MaxInnerSteerDegrees = 0f;      // SKID STEER — her rig publishes no lock angle
            meshDef.MaxOuterSteerDegrees = 0f;
            meshDef.DriveDoorLocal = new Vector2(DoorLocalX, DoorLocalY);
            meshDef.DriverSeatLocal = seat;

            var vehicleDef = ScriptableObject.CreateInstance<VehicleDef>();
            _spawned.Add(vehicleDef);
            vehicleDef.Id = "vehicle.otter_8x8";
            vehicleDef.KindToken = "amphibious_vehicle";
            vehicleDef.Mesh = meshDef;
            vehicleDef.CameraWorldHeightMeters = 18f;

            var machineGo = new GameObject("Otter", typeof(Rigidbody2D));
            _spawned.Add(machineGo);
            machineGo.transform.position = machineAt;
            machineGo.transform.rotation = Quaternion.Euler(0f, 0f, -machineHeadingDeg);
            var vehicle = machineGo.AddComponent<VehicleController>();
            vehicle.SetVehicle(vehicleDef);
            var door = machineGo.AddComponent<VehicleDoor>();

            // --- the fisher ----------------------------------------------------------------------
            var playerGo = new GameObject("Fisher", typeof(SpriteRenderer), typeof(Rigidbody2D));
            _spawned.Add(playerGo);
            playerGo.transform.position =
                machineGo.transform.TransformPoint(new Vector3(DoorLocalX, DoorLocalY, 0f));
            var walk = playerGo.AddComponent<PlayerWalkController>();

            var skin = ScriptableObject.CreateInstance<CharacterVisualDef>();
            _spawned.Add(skin);
            skin.Id = "visual.driver_play";
            skin.FacingCount = Directions;
            skin.IdleFrameCount = 4;
            skin.IdleSheet = Sheet(Directions * 4);
            if (withDriveArt)
                skin.DriveClip = new CharacterClipSheets
                {
                    FrameCount = DriveFrames, FramesPerSecond = 1000f / 170f, Loops = true,
                    Sheet = Sheet(Directions * DriveFrames),
                };

            var iso = playerGo.AddComponent<IsoCharacterSprite>();
            iso.Configure(skin);
            var clip = playerGo.AddComponent<CharacterClipPlayer>();

            var mounts = ScriptableObject.CreateInstance<CharacterOffDeckMountsDef>();
            _spawned.Add(mounts);
            mounts.Id = "offdeckmounts.driver_play";
            mounts.DriveSeatZ = PoseSeatZ;

            var drive = playerGo.AddComponent<PlayerDrivePresenter>();
            drive.ConfigureMounts(mounts);

            // --- the switcher --------------------------------------------------------------------
            // No _drivePresenter wired: it auto-resolves off the walk controller's own object, which is
            // exactly how the deck rider is found and how the built player will find it too.
            var switcherGo = new GameObject("Switcher");
            _spawned.Add(switcherGo);
            var switcher = switcherGo.AddComponent<ControlSwitcher>();
            switcher.Configure(walk, null, null, null, 0f, null);

            return new Rig
            {
                Switcher = switcher, Walk = walk, Drive = drive, Clip = clip, Skin = skin,
                Body = playerGo.GetComponent<SpriteRenderer>(), Door = door, Vehicle = vehicle,
                PlayerGo = playerGo, MachineGo = machineGo,
            };
        }

        /// <summary>Point the machine, body and transform together — <c>Physics2D.autoSyncTransforms</c>
        /// is off, so a transform written alone leaves the body at the old angle until the next step and
        /// everything that samples the BODY answers about where she used to be.</summary>
        static IEnumerator PointMachine(Rig rig, float headingDeg)
        {
            var rb = rig.MachineGo.GetComponent<Rigidbody2D>();
            rig.MachineGo.transform.rotation = Quaternion.Euler(0f, 0f, -headingDeg);
            rb.rotation = -headingDeg;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        static float MachineHeading(Rig rig) => BoatKinematics.BearingDegrees(rig.MachineGo.transform.up);

        /// <summary>Where the drive cell's pivot should be this instant, computed the long way round from
        /// the machine's live transform rather than from the presenter's own arithmetic.</summary>
        static Vector2 ExpectedSeat(Rig rig)
        {
            Vector3 ground = rig.MachineGo.transform.TransformPoint(
                new Vector3(OtterSeat.x, OtterSeat.y, 0f));
            float lift = (OtterSeat.z - PoseSeatZ) * HiddenHarbours.Art.SpriteLightMath.HeightScale;
            return new Vector2(ground.x, ground.y + lift);
        }

        // =============================================================================================

        /// <summary>
        /// ⭐ <b>GET IN AND YOU ARE THERE.</b> The driver is drawn, playing the rig's drive cycle, sat on
        /// the cushion — and she stays on it frame after frame, against a switcher that re-seats her on
        /// the machine's bare root every one of them.
        /// </summary>
        [UnityTest]
        public IEnumerator SheIsDrawnAtTheWheelAndStaysOnTheCushionEveryFrame()
        {
            Rig rig = BuildRig(new Vector3(3f, -2f, 0f), machineHeadingDeg: 0f, OtterSeat);
            yield return null;

            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door), "she could not be boarded at all.");
            yield return null;

            Assert.IsTrue(rig.Drive.IsShowing, "the drive presenter is not showing a driver.");
            Assert.IsTrue(rig.Body.enabled,
                          "the driver is hidden. ADR 0035 hid her in every machine; an OPEN one must draw " +
                          "her, and ApplyPlayerFor is the only thing that decides it.");
            Assert.That(rig.Clip.Clip, Is.EqualTo(CharacterClip.Drive),
                        "she is drawn, but not by the drive clip — that is a fisher STANDING on a truck.");

            // Three frames, because the failure this guards is an ordering one and shows on the frame
            // AFTER the entry: ControlSwitcher.RideVehicle puts her back on the machine's root in its own
            // LateUpdate, and the presenter's offset has to land after it every time, not just once.
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                Vector2 want = ExpectedSeat(rig);
                Vector2 got = rig.PlayerGo.transform.position;
                Assert.That(got.x, Is.EqualTo(want.x).Within(1e-3f), $"frame {i}: she slid off the cushion.");
                Assert.That(got.y, Is.EqualTo(want.y).Within(1e-3f),
                            $"frame {i}: she is drawn at the machine's root, not her seat — the switcher's " +
                            "re-seating is winning the LateUpdate.");
            }

            // And the cell is genuinely animating, not held on frame 0.
            int first = rig.Clip.Frame;
            float waited = 0f;
            while (rig.Clip.Frame == first && waited < 1f) { waited += Time.deltaTime; yield return null; }
            Assert.That(rig.Clip.Frame, Is.Not.EqualTo(first),
                        "the drive cycle never advanced — a looping clip re-played every frame would do " +
                        "exactly this.");
        }

        /// <summary>
        /// ⭐⭐ <b>THE FACING IS HER MACHINE'S, RE-DERIVED — never the one the driver boarded at.</b>
        ///
        /// <para>She walks up to the machine facing one way, boards a machine pointing ANOTHER, and is
        /// then swung through two more headings. At every step the drawn row is the row the machine's own
        /// heading picks. A cached boarding heading passes the first assert and fails the second, which is
        /// the whole point of doing it in that order.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator HerFacingIsTheMachineSAndNotTheOneSheBoardedAt()
        {
            // The machine points EAST; the fisher walks up looking NORTH.
            Rig rig = BuildRig(Vector3.zero, machineHeadingDeg: 90f, OtterSeat);
            rig.Walk.Face(PlayerWalkController.FacingForGroundBearing(0f));
            yield return null;

            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door));
            yield return null;

            int east = rig.Skin.ClipFacingRowFor(CharacterClip.Drive, 90f);
            int north = rig.Skin.ClipFacingRowFor(CharacterClip.Drive, 0f);
            Assert.That(east, Is.Not.EqualTo(north), "the fixture's two headings share a row; it proves nothing.");
            Assert.That(rig.Clip.FacingRow, Is.EqualTo(east),
                        "on boarding she faces the way she WALKED UP rather than the way the machine " +
                        "points. The boarding input must never become a heading.");

            // Now swing the machine under her, twice, without touching the player at all.
            foreach (float heading in new[] { 180f, 270f })
            {
                yield return PointMachine(rig, heading);
                yield return null;      // one more, so Update states the heading and LateUpdate draws it

                Assert.That(MachineHeading(rig), Is.EqualTo(heading).Within(0.5f),
                            "the fixture failed to point the machine; the assert below is meaningless.");
                Assert.That(rig.Clip.FacingRow,
                            Is.EqualTo(rig.Skin.ClipFacingRowFor(CharacterClip.Drive, heading)),
                            $"the machine points {heading}° and her driver does not. The facing is hers, " +
                            "re-derived every frame — a value cached at boarding looks exactly like this.");
            }
        }

        /// <summary>
        /// ⭐ <b>And it holds when she is really DRIVEN</b> — the skid-steer machine spun on her own
        /// controls, not posed by the fixture. This is the end-to-end the go-signal asked for: a real
        /// machine, her real drivetrain, and a driver who turns with her.
        /// </summary>
        [UnityTest]
        public IEnumerator DrivenRoundOnHerOwnControlsTheDriverTurnsWithHer()
        {
            Rig rig = BuildRig(Vector3.zero, machineHeadingDeg: 0f, OtterSeat);
            yield return null;
            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door));
            yield return null;

            float startHeading = MachineHeading(rig);
            int startRow = rig.Clip.FacingRow;

            // Full left lock, no throttle: a skid machine turns on the spot, which is her drivetrain's
            // whole character (SkidSteerMath — she has no steering axle to point).
            //
            // ⚠️ RE-STATED EVERY FRAME, and that is not belt-and-braces. ControlSwitcher.Update reads the
            // keyboard on every frame it is driving and hands the result straight to the machine, so a
            // demand set once is wiped by the very next Update. Restating it is what "holding A down"
            // actually is, and the coroutine resumes AFTER Update, so the value is the one every
            // FixedUpdate of the following frame sees.
            float elapsed = 0f;
            while (elapsed < 1.5f)
            {
                rig.Switcher.DriveInput(0f, 1f, false);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Let go and let her settle. The wheel self-centres at the def's return rate and the yaw dies
            // with it; reading a heading while she is still coming round would race the assert against a
            // facing boundary.
            float settle = 0f;
            while (settle < 0.6f) { settle += Time.deltaTime; yield return null; }
            yield return null;

            float turned = Mathf.Abs(Mathf.DeltaAngle(startHeading, MachineHeading(rig)));
            Assert.That(turned, Is.GreaterThan(45f),
                        $"she only came round {turned:0.0}° under full lock — less than one facing step, " +
                        "so the driver could not have been expected to change row and this proves nothing.");

            Assert.That(rig.Clip.FacingRow, Is.Not.EqualTo(startRow),
                        "the machine has swung past a whole facing step and her driver is still drawn at " +
                        "the row she started on.");
            Assert.That(rig.Clip.FacingRow,
                        Is.EqualTo(rig.Skin.ClipFacingRowFor(CharacterClip.Drive, MachineHeading(rig))),
                        "the driver's row is not the one her machine's live heading picks.");
        }

        /// <summary>Get out and she is gone from the machine: the clip handed back, the presenter idle,
        /// and the fisher back on her own two feet at the door.</summary>
        [UnityTest]
        public IEnumerator SteppingOutTakesTheDriverOffHer()
        {
            Rig rig = BuildRig(new Vector3(1f, 1f, 0f), machineHeadingDeg: 45f, OtterSeat);
            yield return null;
            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door));
            yield return null;
            Assert.IsTrue(rig.Drive.IsShowing);

            Assert.IsTrue(rig.Switcher.LeaveDriving(), "she could not get out on dry ground.");
            yield return null;

            Assert.IsFalse(rig.Drive.IsShowing, "the presenter is still showing a driver on a machine " +
                                                "nobody is in.");
            Assert.IsNull(rig.Drive.Seat, "the machine must be forgotten, not merely stopped drawing.");
            Assert.That(rig.Clip.Clip, Is.EqualTo(CharacterClip.None),
                        "the drive clip is still running on a fisher standing on the gravel.");
            Assert.IsTrue(rig.Body.enabled, "she is invisible ashore.");
            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.OnFoot));
        }

        /// <summary>
        /// <b>A machine that shows no driver keeps hers hidden</b> — byte-for-byte what ADR 0035 shipped,
        /// and the arm that protects the Dually. The whole difference is one unpublished field.
        /// </summary>
        [UnityTest]
        public IEnumerator InAHardCabMachineTheDriverStaysHidden()
        {
            Rig rig = BuildRig(Vector3.zero, machineHeadingDeg: 0f, seat: Vector3.zero);
            yield return null;

            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door), "she must still be drivable.");
            yield return null;

            Assert.IsFalse(rig.Drive.IsShowing);
            Assert.IsFalse(rig.Body.enabled,
                           "a machine with no open seat drew her driver anyway — inside a cab that is a " +
                           "fisher standing on the roofline.");
            Assert.That(rig.Clip.Clip, Is.EqualTo(CharacterClip.None));
            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.Driving), "she is still driving her.");
        }

        /// <summary>With no drive art baked the answer is the same: hidden, and driving works exactly as
        /// it did. Clip art is all-or-nothing, so this is the state every un-baked cast preset is in.</summary>
        [UnityTest]
        public IEnumerator WithNoDriveArtTheDriverStaysHiddenAndDrivingIsUnaffected()
        {
            Rig rig = BuildRig(Vector3.zero, machineHeadingDeg: 0f, OtterSeat, withDriveArt: false);
            yield return null;

            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door));
            yield return null;

            Assert.IsFalse(rig.Drive.IsShowing);
            Assert.IsFalse(rig.Body.enabled);
            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.Driving));

            Vector3 before = rig.MachineGo.transform.position;
            float held = 0f;
            while (held < 0.5f)
            {
                rig.Switcher.DriveInput(1f, 0f, false);   // restated per frame — see the note in the turn test
                held += Time.deltaTime;
                yield return null;
            }

            Assert.That(Vector3.Distance(before, rig.MachineGo.transform.position), Is.GreaterThan(0.05f),
                        "the missing art stopped her being driven. Presentation must never gate the sim.");
        }

        /// <summary>
        /// ⭐ <b>A machine destroyed under the driver.</b> The switcher hands control back; the presenter
        /// must not have dereferenced a dead root on the way — an interface-typed reference sails straight
        /// past Unity's fake-null, so this is a real trap and not a defensive nicety.
        /// </summary>
        [UnityTest]
        public IEnumerator AMachineDestroyedUnderHerLetsTheDriverGoWithoutThrowing()
        {
            Rig rig = BuildRig(Vector3.zero, machineHeadingDeg: 0f, OtterSeat);
            yield return null;
            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door));
            yield return null;
            Assert.IsTrue(rig.Drive.IsShowing);

            Object.DestroyImmediate(rig.MachineGo);
            yield return null;
            yield return null;

            Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.OnFoot),
                        "control was not handed back after the machine died.");
            Assert.IsFalse(rig.Drive.IsShowing, "the presenter is still drawing a driver in a dead truck.");
            Assert.That(rig.Clip.Clip, Is.EqualTo(CharacterClip.None));
            Assert.IsTrue(rig.Body.enabled, "the fisher was left invisible, which reads as a hung game.");
        }
    }
}
