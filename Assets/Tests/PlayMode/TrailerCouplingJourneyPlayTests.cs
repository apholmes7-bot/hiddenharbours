using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>THE COUPLING JOURNEY</b> — the whole loop the handoff asks for, driven end to end under a
    /// running frame pump, on all four tractor/trailer pairings:
    ///
    /// <para><i>drive up → the offer is ABSENT while she is misaligned and PRESENT once backed true →
    /// couple → the legs rise → tow through a turn with visible off-tracking → stop → crank the legs
    /// down → uncouple → drive away bobtail.</i></para>
    ///
    /// <para><b>Why PlayMode and not another EditMode table.</b> Three of those steps only exist
    /// across frames. The follow is driven from an ODOMETER DELTA in <c>LateUpdate</c>, so a fixture
    /// with no pump reads one step of it and calls that off-tracking. The legs are a timed crank that
    /// <see cref="VehicleDoors.Advance"/> walks per frame, so "the legs rise" is a thing that takes
    /// 2.4 s of frames rather than an assignment. And the uncouple refusal is a race in disguise: it
    /// must still refuse on the frame AFTER the crank started, which is exactly the frame a
    /// snapped-state fixture would let through.</para>
    ///
    /// <para><b>Real assets, on purpose.</b> Unlike <c>DriveModePlayTests</c> — which builds defs in
    /// code because it pins the drive MODE and would go red every time the owner tuned a truck — this
    /// file's subject IS the baked geometry. The capture window, the fold cap, and the follow length
    /// are the sidecars' published numbers, so loading the shipped meshes is the point: if a re-bake
    /// moved the slot or the kingpin, the journey should notice.</para>
    ///
    /// <para>⚠️ <b>No key is ever pressed</b> (the #555 lesson). The truck is driven through
    /// <see cref="VehicleController.Throttle"/> / <c>SteerDemand</c> and stepped with
    /// <c>StepPhysics</c>, which is the production path minus the input device.</para>
    /// </summary>
    public class TrailerCouplingJourneyPlayTests
    {
        const string AeroMesh = "Assets/_Project/Data/Vehicles/Meshes/AeroSemiVehicleMesh.asset";
        const string ClassicMesh = "Assets/_Project/Data/Vehicles/Meshes/ClassicSemiVehicleMesh.asset";
        const string Pup = "Assets/_Project/Data/Vehicles/Meshes/TrailerReefer28VehicleMesh.asset";
        const string Long = "Assets/_Project/Data/Vehicles/Meshes/TrailerFlatbed53VehicleMesh.asset";

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- the fixture ------------------------------------------------------------------------

        private static VehicleMeshDef LoadMesh(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(path);
            Assert.That(def, Is.Not.Null, $"{path} did not load — re-run the vehicle bake.");
            return def;
        }

        private sealed class Pair
        {
            public GameObject TractorGo, TrailerGo;
            public VehicleController Controller;
            public VehicleHitch Hitch;
            public TowedBody Trailer;
            public VehicleDoors Gear;
        }

        /// <summary>A driven tractor. Her <c>VehicleDef</c> is built in code — this file is about the
        /// coupling, and the drive envelope is the owner's to tune — but her MESH is the shipped one,
        /// because the fifth wheel lives there.</summary>
        private VehicleDef DrivableFrom(VehicleMeshDef mesh, string id)
        {
            var def = ScriptableObject.CreateInstance<VehicleDef>();
            _spawned.Add(def);
            def.Id = id;
            def.Mesh = mesh;
            def.MaxSpeedMetersPerSecond = 8f;
            def.AccelerationMetersPerSecondSquared = 6f;
            def.BrakingMetersPerSecondSquared = 8f;
            def.SteerRateFullLocksPerSecond = 2f;
            def.CameraWorldHeightMeters = 22f;
            return def;
        }

        /// <summary>Build a tractor at the origin heading north, and a trailer parked at
        /// <paramref name="trailerAt"/> on the heading given — her legs DOWN, as she bakes.</summary>
        private Pair Build(string tractorMesh, string trailerMesh,
                           Vector2 trailerAt, float trailerHeading)
        {
            VehicleMeshDef tm = LoadMesh(tractorMesh), bm = LoadMesh(trailerMesh);
            Assert.IsTrue(tm.CanTow, $"{tractorMesh} publishes no fifth wheel — re-run the bake.");
            Assert.IsTrue(bm.IsTowable, $"{trailerMesh} publishes no kingpin — re-run the bake.");

            var tractorGo = new GameObject("Tractor", typeof(Rigidbody2D));
            _spawned.Add(tractorGo);
            var controller = tractorGo.AddComponent<VehicleController>();
            controller.SetVehicle(DrivableFrom(tm, "vehicle.journey_tractor"));

            var hitch = tractorGo.AddComponent<VehicleHitch>();
            hitch.Configure(tm, controller, "vehicle.journey_tractor");

            var trailerGo = new GameObject("Trailer");
            _spawned.Add(trailerGo);
            trailerGo.transform.position = new Vector3(trailerAt.x, trailerAt.y, 0f);

            var doors = trailerGo.AddComponent<VehicleDoors>();
            doors.Configure(bm);
            doors.SnapAllShut();                       // 0 = legs DOWN, the pose she bakes parked at

            var body = trailerGo.AddComponent<TowedBody>();
            body.Configure(bm);
            body.HeadingDegrees = trailerHeading;

            return new Pair
            {
                TractorGo = tractorGo, TrailerGo = trailerGo,
                Controller = controller, Hitch = hitch, Trailer = body, Gear = doors,
            };
        }

        /// <summary>Why a capture did or did not happen, in the tractor's own frame — so a red here
        /// reports the geometry rather than just "null".</summary>
        private static string Window(Pair p)
        {
            VehicleFifthWheel w = p.Hitch.FifthWheel;
            Vector2 pinWorld = p.Trailer.KingpinWorld;
            Vector3 local = p.TractorGo.transform.InverseTransformPoint(
                new Vector3(pinWorld.x, pinWorld.y, 0f));
            float aft = Mathf.Min(w.RampMouthY, w.SlotSeatY), fore = Mathf.Max(w.RampMouthY, w.SlotSeatY);
            return $"pin local ({local.x:0.#####}, {local.y:0.#####}); slot x {w.CouplingPointLocal.x:0.###}" +
                   $"±{w.SlotHalfWidthMeters:0.###}, y [{aft:0.#####} … {fore:0.#####}]; heading Δ " +
                   $"{Mathf.DeltaAngle(p.Hitch.HeadingDegrees, p.Trailer.HeadingDegrees):0.###}° of " +
                   $"{VehicleCouplingMath.CaptureHeadingToleranceDegrees(w):0.###}°";
        }

        /// <summary>Put the tractor where her plate sits exactly under a point, on a heading — the
        /// arithmetic a driver does with a mirror, done directly so the journey can start from
        /// "backed true" without asserting anything about a steering model.
        ///
        /// <para>Her body is stilled as well as moved: a Rigidbody2D carries velocity across a
        /// teleport, and a truck that arrives already rolling would drift out of the slot between the
        /// placement and the read.</para></summary>
        private static void PlacePlateAt(Pair p, Vector2 plateWorld, float headingDegrees)
        {
            var rb = p.TractorGo.GetComponent<Rigidbody2D>();
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            p.TractorGo.transform.rotation = Quaternion.Euler(0f, 0f, headingDegrees);
            p.TractorGo.transform.position = Vector3.zero;
            Vector2 plateAtOrigin = p.Hitch.CouplingPointWorld;
            Vector2 shift = plateWorld - plateAtOrigin;
            p.TractorGo.transform.position = new Vector3(shift.x, shift.y, 0f);
        }

        /// <summary>
        /// Drive her for real — and nothing here steps anything by hand.
        ///
        /// <para>⚠️ <b>Frames are not time in batch mode</b> (~0.4 ms each), and
        /// <c>VehicleController.FixedUpdate</c> is what runs the drive model, so a loop of plain
        /// <c>yield return null</c> would pump hundreds of frames and move the truck almost nowhere.
        /// Each step below waits for a REAL fixed step — 0.02 s of driving — and then takes one frame
        /// so the <c>LateUpdate</c> that follows the pin actually runs. That ordering (drive, then
        /// follow) is the production one; the test does not substitute for either.</para>
        ///
        /// <para>Calling <c>StepPhysics</c> here instead would double-integrate, because FixedUpdate
        /// is already calling it — and it only sets rigidbody velocities, so the engine is what moves
        /// her either way.</para>
        /// </summary>
        private static IEnumerator Drive(Pair p, float throttle, float steer, int steps)
        {
            p.Controller.Throttle = throttle;
            p.Controller.SteerDemand = steer;
            p.Controller.Brake = false;
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
            p.Controller.Throttle = 0f;
            p.Controller.SteerDemand = 0f;
        }

        /// <summary>Turn the crank, pumping frames — the legs are timed, not toggled. Advanced
        /// explicitly because a batch-mode frame is ~0.4 ms, so <c>VehicleDoors.Update</c>'s own
        /// advance would need thousands of frames to walk a 2.4 s crank.</summary>
        private static IEnumerator Crank(Pair p, float seconds)
        {
            float t = 0f;
            while (t < seconds) { p.Gear.Advance(0.05f); t += 0.05f; yield return null; }
        }

        // =============================================================================================
        //  THE JOURNEY — run four times, once per pairing
        // =============================================================================================

        [UnityTest]
        public IEnumerator TheWholeCouplingJourney_AeroAndThePup() =>
            Journey(AeroMesh, Pup, "aero + 28' pup");

        [UnityTest]
        public IEnumerator TheWholeCouplingJourney_AeroAndTheFiftyThree() =>
            Journey(AeroMesh, Long, "aero + 53' flatbed");

        [UnityTest]
        public IEnumerator TheWholeCouplingJourney_ClassicAndThePup() =>
            Journey(ClassicMesh, Pup, "classic + 28' pup");

        [UnityTest]
        public IEnumerator TheWholeCouplingJourney_ClassicAndTheFiftyThree() =>
            Journey(ClassicMesh, Long, "classic + 53' flatbed");

        /// <summary>
        /// ⭐ The loop itself. Every step below is a thing the player does, in the order they do it,
        /// and each assertion is the thing that would be wrong on screen if the step failed.
        /// </summary>
        private IEnumerator Journey(string tractorMesh, string trailerMesh, string who)
        {
            Pair p = Build(tractorMesh, trailerMesh, new Vector2(0f, 40f), 0f);
            Vector2 pin = p.Trailer.KingpinWorld;

            // ---- 1. MISALIGNED: the offer is not there ------------------------------------------
            // Square onto her nose but a metre off to the side — the slot's throat is 6 cm, so this
            // is not a near miss, it is a driver who has to go round again.
            PlacePlateAt(p, pin + new Vector2(1.0f, 0f), 0f);
            yield return null;
            Assert.IsNull(p.Hitch.CapturedTrailer(),
                $"{who}: a pin a metre out of the slot was captured — the throat is not being read.");
            Assert.IsFalse(p.Hitch.IsAvailable,
                $"{who}: the release handle offered itself with nothing on the plate and nothing " +
                "in the slot.");

            // Lined up, but crooked. Her tolerance is the slot's own aspect (~8.5°), so 25° is a
            // trailer the ramps would shoulder aside rather than swallow.
            PlacePlateAt(p, pin, 25f);
            p.Trailer.HeadingDegrees = 0f;
            yield return null;
            Assert.IsNull(p.Hitch.CapturedTrailer(),
                $"{who}: a pin arriving 25° crooked was captured — the heading gate is not being read.");

            // ---- 2. BACKED TRUE: the offer appears ----------------------------------------------
            PlacePlateAt(p, pin, 0f);
            yield return null;
            TowedBody captured = p.Hitch.CapturedTrailer();
            Assert.AreSame(p.Trailer, captured,
                $"{who}: backed square onto the pin and nothing was captured — {Window(p)}");
            Assert.IsTrue(p.Hitch.IsAvailable, $"{who}: captured, but the handle offered nothing.");
            Assert.AreEqual("Couple the trailer", p.Hitch.VerbLabel,
                $"{who}: the handle read as a release before anything was on the plate.");

            // ---- 3. COUPLE, and the legs come up ------------------------------------------------
            Assert.IsTrue(p.Hitch.Couple(captured), $"{who}: the couple was refused.");
            Assert.IsTrue(p.Trailer.IsCoupled, $"{who}: coupled, but she does not know it.");
            Assert.AreEqual("Pull the release", p.Hitch.VerbLabel,
                $"{who}: on the plate, and the handle still offers to couple.");

            // ⚠️ The legs are SENT up, not snapped: on the very next frame they are still down, which
            // is the honest picture the kit warns about — "nothing in the rig stops a game dragging
            // grounded shoes, and it will render exactly that."
            yield return null;
            Assert.That(p.Gear.Openness("LandingGearShoes"), Is.LessThan(1f),
                $"{who}: her shoes teleported to fully raised the instant the pin dropped — the " +
                "crank is supposed to take its published time, and a driver who couples and floors " +
                "it should get exactly the dragged-shoes picture the kit warns about.");
            // ⚠️ And ONE frame of crank is enough to make dropping her wrong: her shoes have left
            // the ground the moment they start to lift, so the refusal must already be in force here
            // rather than waiting until they are half way up.
            p.Gear.Advance(0.05f);
            yield return null;
            Assert.IsFalse(p.Trailer.LegsAreDown,
                $"{who}: her shoes are lifting and she still reads as standing on them.");
            Assert.IsFalse(p.Hitch.TryUncouple(out string tooSoon),
                $"{who}: she was dropped mid-crank, onto shoes that have left the ground.");
            Assert.IsNotNull(tooSoon, $"{who}: refused without saying why.");

            yield return Crank(p, 3.0f);               // past the published 2.4 s
            Assert.IsFalse(p.Trailer.LegsAreDown,
                $"{who}: the crank ran its full time and her legs are still on the ground.");

            // ---- 4. TOW THROUGH A TURN, and watch her cut the corner ----------------------------
            float straightFold = p.Trailer.ArticulationAgainst(p.Hitch.HeadingDegrees);
            Assert.That(Mathf.Abs(straightFold), Is.LessThan(0.5f),
                $"{who}: she is folded before anybody has turned a wheel.");

            Vector2 trailerStart = p.TrailerGo.transform.position;
            yield return Drive(p, 1f, 0f, 60);         // gather way
            yield return Drive(p, 1f, 1f, 120);        // and put the wheel over

            float fold = p.Trailer.ArticulationAgainst(p.Hitch.HeadingDegrees);
            Assert.That(Mathf.Abs(fold), Is.GreaterThan(2f),
                $"{who}: the tractor turned and the trailer stayed square behind her — that is a " +
                "tow bar, not a fifth wheel.");

            // ⭐ OFF-TRACKING, stated as the thing you can see: her pin follows the tractor exactly,
            // so the proof is that her BODY did not — she is inside the tractor's path, and the
            // longer the trailer the further inside she cuts.
            Assert.That(p.Trailer.HeadingDegrees, Is.Not.EqualTo(p.Hitch.HeadingDegrees).Within(1f),
                $"{who}: her heading is the tractor's — she is not trailing at all.");
            Vector2 pinNow = p.Trailer.KingpinWorld;
            Assert.That(Vector2.Distance(pinNow, p.Hitch.CouplingPointWorld), Is.LessThan(0.02f),
                $"{who}: her pin has come off the plate during the tow — she is being dragged " +
                "alongside rather than pulled by the coupling.");
            Assert.That(Vector2.Distance(trailerStart, p.TrailerGo.transform.position),
                Is.GreaterThan(1f), $"{who}: the tractor drove off and left the trailer standing.");

            // And the fold never exceeded what the pair can physically do.
            Assert.That(Mathf.Abs(fold), Is.LessThanOrEqualTo(p.Hitch.JackknifeCapDegrees + 0.01f),
                $"{who}: folded past the cap the geometry allows.");

            // ---- 5. STOP, and refuse to drop her on raised legs --------------------------------
            p.Controller.Throttle = 0f;
            p.Controller.SteerDemand = 0f;
            p.Controller.Brake = true;
            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
            p.Controller.Brake = false;
            Assert.That(Mathf.Abs(p.Controller.SpeedMetersPerSecond), Is.LessThan(0.05f),
                $"{who}: she would not come to a stand.");

            Assert.IsFalse(p.Hitch.TryUncouple(out string refusal),
                $"{who}: the pin was pulled with her legs up — that drops her nose in the yard.");
            StringAssert.Contains("legs", refusal.ToLowerInvariant(),
                $"{who}: refused, but not for the reason the player needs to hear.");
            Assert.IsTrue(p.Trailer.IsCoupled, $"{who}: refused the uncouple and let her go anyway.");

            // ---- 6. CRANK THEM DOWN, then let her go -------------------------------------------
            p.Gear.SetGroupTarget("gear", 0f);
            yield return Crank(p, 3.0f);
            Assert.IsTrue(p.Trailer.LegsAreDown, $"{who}: the crank did not put her back on her feet.");

            Vector2 restingAt = p.TrailerGo.transform.position;
            float restingHeading = p.Trailer.HeadingDegrees;

            Assert.IsTrue(p.Hitch.TryUncouple(out string none),
                $"{who}: legs down and square, and the release still refused: {none}");
            Assert.IsNull(none, $"{who}: released, but complained while doing it.");
            Assert.IsFalse(p.Trailer.IsCoupled, $"{who}: released, and she still thinks she is on.");
            Assert.IsNull(p.Hitch.Trailer, $"{who}: released, and the plate still holds her.");

            // ---- 7. DRIVE AWAY BOBTAIL ----------------------------------------------------------
            yield return Drive(p, 1f, 0.4f, 120);

            Assert.That(Vector2.Distance(restingAt, p.TrailerGo.transform.position),
                Is.LessThan(1e-3f),
                $"{who}: the tractor drove away and the trailer came with her — the release did not " +
                "let go of the follow.");
            Assert.That(p.Trailer.HeadingDegrees, Is.EqualTo(restingHeading).Within(1e-3f),
                $"{who}: a parked trailer turned on her own after the tractor left.");
            Assert.That(Vector2.Distance(p.TractorGo.transform.position, restingAt),
                Is.GreaterThan(3f), $"{who}: bobtail, and she has not gone anywhere.");

            // Standing bobtail well clear of the yard, the handle offers nothing at all.
            Assert.IsFalse(p.Hitch.IsAvailable,
                $"{who}: a bobtail tractor parked away from any trailer still offers her release.");
        }

        // =============================================================================================
        //  The 53's tail — the case that differs
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The long trailer cuts the corner harder</b>, which is the whole reason the handoff
        /// names the 53 separately.
        ///
        /// <para>Both bodies are pulled by the same tractor through the SAME turn, and the fold is
        /// compared. The pup's kingpin-to-axle is 6.265 m against the 53's 13.275 m, and the follow
        /// rate is <c>v·sin(φ)/L</c> — so the long one swings toward the line of travel more slowly
        /// and therefore hangs at a WIDER angle behind the cab. That is the off-tracking a driver
        /// feels, and it falls out of the published length rather than out of a per-body number.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheFiftyThreeHangsWiderThroughTheSameTurnThanThePup()
        {
            float foldPup = 0f, foldLong = 0f;

            foreach (string body in new[] { Pup, Long })
            {
                Pair p = Build(AeroMesh, body, new Vector2(0f, 40f), 0f);
                PlacePlateAt(p, p.Trailer.KingpinWorld, 0f);
                yield return null;

                Assert.IsTrue(p.Hitch.Couple(p.Hitch.CapturedTrailer()), "fixture: couple");
                p.Gear.SetGroupTarget("gear", 1f);
                yield return Crank(p, 3.0f);

                yield return Drive(p, 1f, 0f, 60);
                yield return Drive(p, 1f, 1f, 120);

                float fold = Mathf.Abs(p.Trailer.ArticulationAgainst(p.Hitch.HeadingDegrees));
                if (body == Pup) foldPup = fold; else foldLong = fold;

                foreach (var o in new Object[] { p.TractorGo, p.TrailerGo }) Object.Destroy(o);
                yield return null;
            }

            Assert.That(foldLong, Is.GreaterThan(foldPup),
                $"the 53 ({foldLong:0.##}°) did not hang wider than the pup ({foldPup:0.##}°) " +
                "through the same turn — the follow is not using her published length.");
        }
    }
}
