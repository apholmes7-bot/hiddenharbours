using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Vehicles;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE FISHER AT THE WHEEL</b> — the placement arithmetic, the seat contract, and every reason the
    /// driver is NOT drawn. The half that needs no frame pump; the sequence lives in
    /// <c>DriverAtTheWheelPlayTests</c>.
    ///
    /// <para><b>The acceptance worth reading first</b> is
    /// <see cref="HerBakedPoseReachesHerPublishedHandlebar"/>: two committed art contracts, written months
    /// apart by different rigs — the character rig's <c>OffDeck_mounts.json</c> and the Otter's own
    /// gameplay sidecar — agree to within half a pixel once the seats are aligned. That is the whole
    /// justification for aligning by SEAT and letting the hands fall where they fall, and if a re-bake
    /// ever breaks it, this is the test that says so with the number.</para>
    /// </summary>
    public class DriverAtTheWheelTests
    {
        // ---- the two contracts, as committed -------------------------------------------------------
        //
        // Restated here rather than read off the assets on purpose: these are the numbers the placement
        // was DESIGNED against, so a test that read whatever the asset says today could not notice the
        // asset changing. The shipped assets are pinned against their own sidecars elsewhere
        // (AmphibiousVehicleTests for the machine, CharacterOffDeckMountsTests for the pose).

        // OffDeck_mounts.json — the drive pose's own geometry, world metres on the machine.
        const float PoseSeatZ = 0.40f;
        const float PoseWheelZ = 0.655f;    // above the vehicle floor the pose is pivoted on
        const float PoseWheelY = 0.315f;    // forward of the seat

        // amphibIsoRig.otter8x8.gameplay.json — SEATS[front_bench].seat_ref, role "driver + one".
        static readonly Vector3 OtterSeat = new Vector3(0f, 0.36f, 0.76f);
        // COCKPIT.helm.bar_axis — the centred handlebar. "There is no driver's side on this machine."
        static readonly Vector3 OtterBar = new Vector3(0f, 0.66f, 1.03f);
        // INTERACT[id=drive].reach_point.
        static readonly Vector2 OtterDoor = new Vector2(-1.4f, 0.2f);

        const int PxPerMetre = 32;          // frame.scale_px_per_m, both files
        const float OnePixel = 1f / PxPerMetre;

        readonly System.Collections.Generic.List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => Interactables.Clear();

        [TearDown]
        public void TearDown()
        {
            // A VehicleDoor registers itself as an interact candidate on enable. Left behind, the next
            // fixture resolves a press against a truck that no longer exists.
            Interactables.Clear();
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures -------------------------------------------------------------------------------

        /// <summary>A machine's mesh def. <paramref name="seat"/> zero is a machine that publishes no
        /// open seat — the Dually's shape, and the shape of every def baked before the field existed.</summary>
        VehicleMeshDef Mesh(Vector3 seat)
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            mesh.triangles = new[] { 0, 1, 2 };
            _spawned.Add(mesh);

            var def = ScriptableObject.CreateInstance<VehicleMeshDef>();
            _spawned.Add(def);
            def.Id = "vehiclemesh.driver_test";
            def.Mesh = mesh;
            def.Ramps = new[] { new HullMeshDef.Ramp { Colors = new Color32[] { new Color32(255, 255, 255, 255) } } };
            def.Bayer16 = new float[16];
            def.PxPerMetre = PxPerMetre;
            def.CellW = 256;
            def.CellH = 192;
            def.WheelbaseMeters = 2.04f;
            def.FrontTrackMeters = 1.26f;
            def.WheelRadiusMeters = 0.32f;
            def.DriveDoorLocal = OtterDoor;
            def.DriverSeatLocal = seat;
            return def;
        }

        VehicleDef VehicleAsset(VehicleMeshDef mesh)
        {
            var def = ScriptableObject.CreateInstance<VehicleDef>();
            _spawned.Add(def);
            def.Id = "vehicle.driver_test";
            def.KindToken = "amphibious_vehicle";
            def.Mesh = mesh;
            def.CameraWorldHeightMeters = 18f;
            return def;
        }

        /// <summary>A machine standing at <paramref name="at"/>, pointing at <paramref name="headingDeg"/>
        /// (compass, 0 = N, and <c>transform.up</c> is the nose — the fleet's one convention).</summary>
        VehicleDoor Machine(Vector3 seat, Vector3 at, float headingDeg = 0f)
        {
            VehicleMeshDef mesh = Mesh(seat);
            var go = new GameObject("Machine", typeof(Rigidbody2D));
            _spawned.Add(go);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, -headingDeg);  // +Z is CCW; a bearing is CW
            var controller = go.AddComponent<VehicleController>();
            controller.SetVehicle(VehicleAsset(mesh));
            return go.AddComponent<VehicleDoor>();
        }

        CharacterOffDeckMountsDef Mounts()
        {
            var def = ScriptableObject.CreateInstance<CharacterOffDeckMountsDef>();
            _spawned.Add(def);
            def.Id = "offdeckmounts.driver_test";
            def.DriveSeatZ = PoseSeatZ;
            def.DriveWheelZ = PoseWheelZ;
            def.DriveWheelY = PoseWheelY;
            return def;
        }

        Sprite[] Sheet(int count)
        {
            var set = new Sprite[count];
            for (int i = 0; i < set.Length; i++)
            {
                var tex = new Texture2D(4, 8);
                _spawned.Add(tex);
                var s = Sprite.Create(tex, new Rect(0, 0, 4, 8), new Vector2(0.5f, 0.1f), PxPerMetre);
                _spawned.Add(s);
                set[i] = s;
            }
            return set;
        }

        /// <summary>A skin with a body, and optionally the rig's 6-frame drive cycle at all eight
        /// facings.</summary>
        CharacterVisualDef Skin(bool withDriveClip)
        {
            var d = ScriptableObject.CreateInstance<CharacterVisualDef>();
            _spawned.Add(d);
            d.Id = "visual.driver_test";
            d.FacingCount = 8;
            d.IdleFrameCount = 4;
            d.IdleSheet = Sheet(8 * 4);
            if (withDriveClip)
                d.DriveClip = new CharacterClipSheets
                {
                    FrameCount = 6, FramesPerSecond = 1000f / 170f, Loops = true, Sheet = Sheet(8 * 6),
                };
            return d;
        }

        sealed class Fisher
        {
            public GameObject Go;
            public PlayerDrivePresenter Drive;
            public CharacterClipPlayer Clip;
            public SpriteRenderer Renderer;
        }

        Fisher Driver(bool withDriveClip = true, bool withMounts = true)
        {
            var go = new GameObject("Fisher", typeof(SpriteRenderer));
            _spawned.Add(go);
            var iso = go.AddComponent<IsoCharacterSprite>();
            iso.Configure(Skin(withDriveClip));

            var clip = go.AddComponent<CharacterClipPlayer>();     // reads the iso skin's own def
            var drive = go.AddComponent<PlayerDrivePresenter>();
            if (withMounts) drive.ConfigureMounts(Mounts());

            return new Fisher
            {
                Go = go, Drive = drive, Clip = clip, Renderer = go.GetComponent<SpriteRenderer>(),
            };
        }

        // =============================================================================================
        // The arithmetic
        // =============================================================================================

        /// <summary>
        /// The lift is the DIFFERENCE between the two seats, and its sign is the point: a machine whose
        /// cushion is higher than the pose's raises the figure, a lower one drops her, and an equal one
        /// moves her not at all.
        /// </summary>
        [Test]
        public void TheLiftIsTheDifferenceBetweenTheTwoSeats()
        {
            Assert.That(DriveSeatMath.LiftMeters(PoseSeatZ, PoseSeatZ), Is.EqualTo(0f).Within(1e-6f),
                        "a machine seated exactly where the pose was baked must not move the figure at all.");

            float up = DriveSeatMath.LiftMeters(OtterSeat.z, PoseSeatZ);
            Assert.That(up, Is.GreaterThan(0f),
                        "the Otter's bench (0.76) is higher than the bake (0.40), so the fisher rises.");
            Assert.That(up, Is.EqualTo((OtterSeat.z - PoseSeatZ) * SpriteLightMath.HeightScale).Within(1e-5f),
                        "a HEIGHT draws cos 40° up the screen — the game's one height scale, never a raw metre.");

            Assert.That(DriveSeatMath.LiftMeters(0.20f, PoseSeatZ), Is.LessThan(0f),
                        "a machine sat LOWER than the bake must drop the figure, not clamp at zero.");
        }

        /// <summary>
        /// ⭐ <b>The Otter's driver would be drawn NINE PIXELS into her cockpit floor without the lift.</b>
        /// The number is worth pinning: it is the whole reason this arithmetic exists rather than dropping
        /// the cell at the seat's ground point and calling it done.
        /// </summary>
        [Test]
        public void WithoutTheLiftTheOtterSDriverSitsNinePixelsTooLow()
        {
            float lift = DriveSeatMath.LiftMeters(OtterSeat.z, PoseSeatZ);
            float px = lift * PxPerMetre;
            Assert.That(px, Is.EqualTo(8.8f).Within(0.2f),
                        $"the Otter's lift measured {px:0.00} px, not the ~8.8 the two contracts imply. " +
                        "Either a contract moved or the height scale did.");
        }

        /// <summary>
        /// A height is not a place. The pivot takes its X straight from the seat's ground point, adds the
        /// lift to Y only, and carries the character's own Z across untouched — the iso sort band lives
        /// there (ADR 0032) and a seating that wrote it would quietly re-layer the fisher.
        /// </summary>
        [Test]
        public void TheSeatPivotLiftsInYOnlyAndKeepsTheCharacterSDepth()
        {
            var ground = new Vector2(12.5f, -3.25f);
            const float depth = 0.375f;

            Vector3 pivot = DriveSeatMath.SeatPivotWorld(ground, OtterSeat.z, PoseSeatZ, depth);

            Assert.That(pivot.x, Is.EqualTo(ground.x).Within(1e-5f), "the lift must not move her sideways.");
            Assert.That(pivot.y, Is.EqualTo(ground.y + DriveSeatMath.LiftMeters(OtterSeat.z, PoseSeatZ)).Within(1e-5f));
            Assert.That(pivot.z, Is.EqualTo(depth).Within(1e-6f),
                        "the character's own z is the sort band. Writing it here would re-layer her.");
        }

        /// <summary>
        /// ⭐⭐ <b>THE TWO ART CONTRACTS AGREE.</b> Align the pose's seat with the Otter's bench and her
        /// hands land on the handlebar her own rig publishes — 0.015 m out in each axis, which at 32 px/m
        /// is HALF A PIXEL.
        ///
        /// <para>Nothing was tuned to make this true: the character rig baked <c>drive</c> at
        /// seatZ 0.40 / wheelY 0.315 / wheelZ 0.655 with no machine in mind, and the Otter's sidecar
        /// measured her bench and her bar off her own geometry. That they meet is what makes
        /// seat-alignment the right rule rather than a convenient one — and it is why nothing here
        /// re-bakes the pose per machine, which the rig would allow
        /// (<i>"opts.seatZ/wheelZ/wheelY; soft-clamped"</i>).</para>
        /// </summary>
        [Test]
        public void HerBakedPoseReachesHerPublishedHandlebar()
        {
            // Seated on her bench, where the pose puts the grips:
            float handY = OtterSeat.y + PoseWheelY;
            float handZ = OtterSeat.z + (PoseWheelZ - PoseSeatZ);

            Assert.That(handY, Is.EqualTo(OtterBar.y).Within(OnePixel),
                        $"the pose's grips sit {Mathf.Abs(handY - OtterBar.y) * PxPerMetre:0.00} px fore/aft " +
                        "of her handlebar. Over a pixel and the fisher is reaching at nothing.");
            Assert.That(handZ, Is.EqualTo(OtterBar.z).Within(OnePixel),
                        $"the pose's grips sit {Mathf.Abs(handZ - OtterBar.z) * PxPerMetre:0.00} px above/below " +
                        "her handlebar.");

            // And her bar is CENTRED — which is why the seat's x is 0 and the pose needs no lateral term
            // at all. A machine with an offset helm would want one, and would say so here first.
            Assert.That(OtterBar.x, Is.EqualTo(0f).Within(1e-6f),
                        "her helm is centre-steer; if that ever changes the seat needs a lateral term.");
        }

        // =============================================================================================
        // The seat contract, on the real VehicleDoor
        // =============================================================================================

        /// <summary>
        /// The seat's GROUND point swings with her heading; the seat's HEIGHT never does. They are read
        /// through the same machine and must not be composed into one rotatable thing.
        /// </summary>
        [Test]
        public void TheSeatTravelsAndTurnsWithHerButItsHeightDoesNot()
        {
            var at = new Vector3(7f, 2f, 0f);

            Vector2 north = Machine(OtterSeat, at, headingDeg: 0f).DriverSeatWorldPosition;
            Assert.That(north.x, Is.EqualTo(at.x).Within(1e-4f));
            Assert.That(north.y, Is.EqualTo(at.y + OtterSeat.y).Within(1e-4f),
                        "pointing north, a seat 0.36 m forward of her centre is 0.36 m north of it.");

            VehicleDoor east = Machine(OtterSeat, at, headingDeg: 90f);
            Vector2 seatE = east.DriverSeatWorldPosition;
            Assert.That(seatE.x, Is.EqualTo(at.x + OtterSeat.y).Within(1e-4f),
                        "swung to face east, the same seat is 0.36 m EAST of her — the offset turns with " +
                        "the machine, exactly as her door does.");
            Assert.That(seatE.y, Is.EqualTo(at.y).Within(1e-4f));

            Assert.That(east.DriverSeatHeightMeters, Is.EqualTo(OtterSeat.z).Within(1e-5f),
                        "her cushion is 0.76 m up whichever way she is pointing. A height that turned " +
                        "would be the one bug this split exists to prevent.");
        }

        /// <summary>A machine that publishes no open seat answers no, and her two seat members are never
        /// to be read — the Dually's case, and the case of every def baked before the field existed.</summary>
        [Test]
        public void AMachineWithNoOpenSeatShowsNoDriver()
        {
            VehicleDoor cab = Machine(Vector3.zero, Vector3.zero);
            Assert.IsFalse(cab.ShowsDriver,
                           "(0,0,0) is the 'not published' sentinel — a seat at her own origin, on the " +
                           "ground, under her own centre, can never be a real one.");

            Assert.IsTrue(Machine(OtterSeat, Vector3.zero).ShowsDriver);
        }

        // =============================================================================================
        // Every reason she is NOT drawn
        // =============================================================================================

        /// <summary>The happy path, stated once so the refusals below are refusals of something.</summary>
        [Test]
        public void OnAnOpenMachineTheDriverIsDrawnSeatedOnHerCushion()
        {
            Fisher fisher = Driver();
            var at = new Vector3(4f, 1f, 0f);
            VehicleDoor otter = Machine(OtterSeat, at);

            Assert.IsTrue(fisher.Drive.SetSeat(otter), "an open machine with baked art must draw her driver.");
            Assert.IsTrue(fisher.Drive.IsShowing);
            Assert.That(fisher.Clip.Clip, Is.EqualTo(CharacterClip.Drive),
                        "she is drawn, but not by the DRIVE clip — the pose is a standing fisher.");

            Vector3 expected = DriveSeatMath.SeatPivotWorld(otter.DriverSeatWorldPosition,
                                                           OtterSeat.z, PoseSeatZ,
                                                           fisher.Go.transform.position.z);
            Vector3 drawn = fisher.Go.transform.position;
            Assert.That(drawn.x, Is.EqualTo(expected.x).Within(1e-4f),
                        "she is not on the cushion. SetSeat must seat her on the spot rather than " +
                        "waiting for a frame that an EditMode caller will never pump.");
            Assert.That(drawn.y, Is.EqualTo(expected.y).Within(1e-4f));
        }

        /// <summary>
        /// No drive art → nothing at all happens, and crucially the answer is FALSE: the switcher reads
        /// it to decide whether the player's renderer is on, so a yes here would be a fisher drawn
        /// STANDING on a truck.
        /// </summary>
        [Test]
        public void WithNoDriveArtSheStaysHidden()
        {
            Fisher fisher = Driver(withDriveClip: false);
            VehicleDoor otter = Machine(OtterSeat, Vector3.zero);

            Assert.IsFalse(fisher.Drive.WouldShow(otter));
            Assert.IsFalse(fisher.Drive.SetSeat(otter));
            Assert.IsFalse(fisher.Drive.IsShowing);
            Assert.That(fisher.Clip.Clip, Is.EqualTo(CharacterClip.None), "no clip may have been started.");
        }

        /// <summary>
        /// No mount contract → hidden. Without <c>DriveSeatZ</c> the seat the pose was baked on is
        /// unknown, and guessing it would put her a dozen pixels above the cushion.
        /// </summary>
        [Test]
        public void WithNoMountContractSheStaysHidden()
        {
            Fisher fisher = Driver(withMounts: false);
            Assert.IsFalse(fisher.Drive.SetSeat(Machine(OtterSeat, Vector3.zero)));
            Assert.IsFalse(fisher.Drive.IsShowing);
        }

        /// <summary>A hard-cab machine keeps her driver hidden however good the art is.</summary>
        [Test]
        public void InAMachineThatShowsNoDriverSheStaysHidden()
        {
            Fisher fisher = Driver();
            Assert.IsFalse(fisher.Drive.SetSeat(Machine(Vector3.zero, Vector3.zero)));
            Assert.IsFalse(fisher.Drive.IsShowing);
        }

        /// <summary>
        /// A DISABLED presenter answers no. It would get no Update and no LateUpdate, so a yes would be a
        /// driver frozen on frame 0 at the machine's own centre — worse than the hidden driver it replaced.
        /// </summary>
        [Test]
        public void ADisabledPresenterDrawsNobody()
        {
            Fisher fisher = Driver();
            fisher.Drive.enabled = false;
            Assert.IsFalse(fisher.Drive.SetSeat(Machine(OtterSeat, Vector3.zero)));
        }

        /// <summary>Null — "not driving" — is the tear-down, and it hands the renderer back.</summary>
        [Test]
        public void HandingOverNoMachineTearsHerDown()
        {
            Fisher fisher = Driver();
            Assert.IsTrue(fisher.Drive.SetSeat(Machine(OtterSeat, Vector3.zero)));

            Assert.IsFalse(fisher.Drive.SetSeat(null));
            Assert.IsFalse(fisher.Drive.IsShowing);
            Assert.That(fisher.Clip.Clip, Is.EqualTo(CharacterClip.None),
                        "the clip is still running on a fisher who is no longer in anything.");
            Assert.IsNull(fisher.Drive.Seat, "the machine must be forgotten, not merely stopped drawing.");
        }

        /// <summary>
        /// Re-handing the SAME machine leaves the clip alone. A region hop re-asserts the drive mode
        /// through the very same path, and a looping clip re-played every time would freeze the driver on
        /// frame 0 for good.
        /// </summary>
        [Test]
        public void ReAssertingTheSameMachineDoesNotRestartTheClip()
        {
            Fisher fisher = Driver();
            VehicleDoor otter = Machine(OtterSeat, Vector3.zero);
            Assert.IsTrue(fisher.Drive.SetSeat(otter));

            fisher.Clip.Advance(0.5f);                     // a few frames into the cycle
            int frame = fisher.Clip.Frame;
            Assert.That(frame, Is.GreaterThan(0), "the fixture's clip did not advance; the test proves nothing.");

            Assert.IsTrue(fisher.Drive.SetSeat(otter));    // the re-assert
            Assert.That(fisher.Clip.Frame, Is.EqualTo(frame),
                        "the same machine handed over twice restarted the cycle — a re-asserted drive mode " +
                        "would hold the driver on one frame forever.");
        }

        /// <summary>A machine destroyed under the driver is refused rather than dereferenced — the
        /// interface-typed reference does not see Unity's fake-null, so this is a real trap.</summary>
        [Test]
        public void AMachineDestroyedUnderHerIsRefused()
        {
            Fisher fisher = Driver();
            VehicleDoor otter = Machine(OtterSeat, Vector3.zero);
            Object.DestroyImmediate(otter.gameObject);

            Assert.IsFalse(fisher.Drive.WouldShow(otter));
            Assert.IsFalse(fisher.Drive.SetSeat(otter));
        }
    }
}
